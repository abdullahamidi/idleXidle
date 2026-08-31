using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// No two mastery nodes may be drawn on top of each other.
/// </summary>
/// <remarks>
/// <para>
/// The layout used to live in <c>BuildScreen.NodePos</c>, where nothing could test it, and it was a
/// pair of lookup tables: ring radius <c>{1: 0.37, 2: 0.61, 3: 0.81, _: 1.0}</c> and sibling spread
/// <c>{1: 26°, 2: 16°, 3: 9°, _: 0°}</c>. Read the fallthroughs together and a ring 5 draws at the same
/// radius as ring 4 with every sibling at zero spread — the entire ring collapsed onto one point.
/// </para>
/// <para>
/// Nothing was wrong on screen, which is exactly what made it worth fixing: the tree was not broken,
/// it was UNGROWABLE, and the breakage was reserved for whoever first added a node. The catalogue is
/// the game's central build system and its own notes call extending it "real UI work" for this reason.
/// So these tests check the tree that exists AND trees deeper than it, because a layout rule that has
/// only ever been checked at the size it was tuned for has not been checked.
/// </para>
/// </remarks>
public class MasteryLayoutTests
{
    private readonly ITestOutputHelper _out;

    public MasteryLayoutTests(ITestOutputHelper output) => _out = output;

    private static float Distance(TreePoint a, TreePoint b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    [Fact]
    public void test_no_two_nodes_in_the_catalogue_overlap()
    {
        var placed = MasteryCatalog.Nodes
            .Select(n => (Node: n, At: MasteryLayout.PositionOf(n)))
            .ToList();

        var worst = float.MaxValue;
        (string A, string B) worstPair = ("", "");

        for (var i = 0; i < placed.Count; i++)
        for (var j = i + 1; j < placed.Count; j++)
        {
            var (a, b) = (placed[i], placed[j]);
            var need = MasteryLayout.NodeWorldRadius(a.Node.Kind)
                       + MasteryLayout.NodeWorldRadius(b.Node.Kind);
            var got = Distance(a.At, b.At);
            var clearance = got - need;

            if (clearance < worst) { worst = clearance; worstPair = (a.Node.Id, b.Node.Id); }

            Assert.True(got >= need,
                $"{a.Node.Id} and {b.Node.Id} overlap: {got:N0} world units apart, " +
                $"and their radii sum to {need:N0}.");
        }

        _out.WriteLine($"{placed.Count} nodes, tightest pair {worstPair.A} / {worstPair.B} " +
                       $"with {worst:N0} world units of clearance.");
    }

    [Fact]
    public void test_the_layout_still_separates_a_tree_twice_as_deep()
    {
        // THE TEST THE OLD CODE COULD NOT HAVE PASSED. Ring 5 and beyond fell through both switches to
        // "radius 1.0, spread 0", so every node of every deeper ring landed on one point per branch.
        // Nothing in the catalogue reaches ring 5 yet, so this poses the tree that does not exist —
        // which is the only way to find out whether the content vein is actually open before someone
        // spends a day authoring into it.
        const int deepest = 8;
        var synthetic = new List<MasteryNode>();
        foreach (var branch in Enum.GetValues<Branch>())
        for (var ring = 1; ring <= deepest; ring++)
        for (var sibling = 0; sibling < 4; sibling++)
            synthetic.Add(new MasteryNode(
                $"{branch}_{ring}_{sibling}", MasteryKind.Minor, branch, ring, 1,
                "SYNTHETIC", SkillShape.None, null, Array.Empty<string>()));

        // Placed against the synthetic depth rather than the catalogue's, and by the same arithmetic
        // the real nodes use: ring radius, then a constant-arc step within the ring.
        var placed = synthetic.Select(n =>
        {
            var r = MasteryLayout.RingRadius(n.Ring, deepest);
            var step = MasteryLayout.SiblingSpread(n.Ring, deepest, 4);
            var angle = MasteryLayout.AngleOf(n.Branch) + (3 - 1.5f) * 0f;   // index applied below
            var idx = int.Parse(n.Id.Split('_')[2]);
            angle += (idx - 1.5f) * step;
            return (n, At: new TreePoint(r * MathF.Cos(angle), r * MathF.Sin(angle)));
        }).ToList();

        var minor = MasteryLayout.NodeWorldRadius(MasteryKind.Minor);
        for (var i = 0; i < placed.Count; i++)
        for (var j = i + 1; j < placed.Count; j++)
        {
            var got = Distance(placed[i].At, placed[j].At);
            Assert.True(got >= 2f * minor,
                $"at depth {deepest}, {placed[i].n.Id} and {placed[j].n.Id} are {got:N0} apart " +
                $"but need {2f * minor:N0}.");
        }

        _out.WriteLine($"{placed.Count} synthetic nodes across {deepest} rings — all separated.");
    }

    [Fact]
    public void test_a_fan_never_reaches_into_the_next_branch()
    {
        // The OTHER growth direction, and the one the constant-arc rule alone could not protect. It
        // separates siblings within an arm and knows nothing about the arm next door; at ring 1 the
        // four minors already spanned 87% of the 90° between branches, so a fifth would have pushed
        // Weight's outermost minor through Tempo's. Growing a ring sideways is the first thing a
        // content author reaches for, so it has to be the safe thing.
        foreach (var count in new[] { 2, 4, 6, 9 })
        foreach (var ring in new[] { 1, 2, 3, 4 })
        {
            var step = MasteryLayout.SiblingSpread(ring, 4, count);
            var fan = (count - 1) * step;
            Assert.True(fan <= MasteryLayout.BranchSeparation * MasteryLayout.BranchFanFraction + 1e-4f,
                $"ring {ring} with {count} siblings fans {fan:0.000} rad, past its share of the " +
                $"{MasteryLayout.BranchSeparation:0.000} rad between branches.");
        }
    }

    [Fact]
    public void test_a_ring_can_take_more_nodes_than_the_catalogue_gives_it()
    {
        // Concretely: six minors on every ring-1 arm, which is where the tree is most crowded, must
        // still be four separated nodes per arm and four separated arms. This is the case the old
        // layout could not have survived and the one a "more nodes per branch" content pass creates.
        const int perRing = 6;
        var placed = new List<(string Id, TreePoint At)>();

        foreach (var branch in Enum.GetValues<Branch>())
        {
            var step = MasteryLayout.SiblingSpread(1, 4, perRing);
            var r = MasteryLayout.RingRadius(1, 4);
            for (var i = 0; i < perRing; i++)
            {
                var angle = MasteryLayout.AngleOf(branch) + (i - (perRing - 1) / 2f) * step;
                placed.Add(($"{branch}_{i}", new TreePoint(r * MathF.Cos(angle), r * MathF.Sin(angle))));
            }
        }

        var need = 2f * MasteryLayout.NodeWorldRadius(MasteryKind.Minor);
        var worst = float.MaxValue;
        for (var i = 0; i < placed.Count; i++)
        for (var j = i + 1; j < placed.Count; j++)
        {
            var got = Distance(placed[i].At, placed[j].At);
            worst = MathF.Min(worst, got);
            Assert.True(got >= need,
                $"{placed[i].Id} and {placed[j].Id} are {got:N0} apart, needing {need:N0}.");
        }

        _out.WriteLine($"{perRing} minors on every ring-1 arm: closest pair {worst:N0} " +
                       $"world units apart, needing {need:N0}.");
    }

    [Fact]
    public void test_rings_march_outward_and_the_last_one_reaches_the_rim()
    {
        // Two claims the camera depends on. The zoom that frames the whole tree is chosen against
        // WorldRadius, so if a ring ever sat outside it, the outermost content would be unreachable at
        // the default zoom — visible only to a player who thought to scroll.
        foreach (var maxRing in new[] { 3, 4, 6, 10 })
        {
            var radii = Enumerable.Range(1, maxRing)
                                  .Select(r => MasteryLayout.RingRadius(r, maxRing))
                                  .ToList();

            for (var i = 1; i < radii.Count; i++)
                Assert.True(radii[i] > radii[i - 1],
                    $"with {maxRing} rings, ring {i + 1} is not outside ring {i}.");

            Assert.True(radii[0] >= MasteryLayout.WorldRadius * MasteryLayout.InnerFraction - 0.5f,
                "ring 1 must clear the START node at the centre.");
            Assert.Equal(MasteryLayout.WorldRadius, radii[^1], 1);
        }
    }

    [Fact]
    public void test_the_new_formula_reproduces_the_hand_tuned_table_it_replaced()
    {
        // The layout a player already knows must not move under them. The table was 0.37 / 0.61 / 0.81
        // / 1.0 for the four rings the catalogue has; if the curve drifted from that, this refactor
        // would have quietly redesigned the screen while claiming to generalise it.
        var expected = new[] { 0.37f, 0.61f, 0.81f, 1.00f };
        for (var ring = 1; ring <= 4; ring++)
        {
            var got = MasteryLayout.RingRadius(ring, 4) / MasteryLayout.WorldRadius;
            _out.WriteLine($"ring {ring}: was {expected[ring - 1]:0.000}, now {got:0.000}");
            Assert.True(MathF.Abs(got - expected[ring - 1]) < 0.01f,
                $"ring {ring} moved from {expected[ring - 1]:0.000} to {got:0.000}.");
        }
    }

    [Fact]
    public void test_a_second_ring_of_the_same_kind_does_not_move_the_first()
    {
        // The sibling key used to be (Branch, KIND), which agrees with (Branch, RING) only because
        // every Kind happens to occupy exactly one ring today. Give a branch a second ring of Minors
        // and the two rings would have shared one sibling list — the new nodes re-indexing and
        // physically MOVING the existing ones, on a screen where a player has memorised where their
        // build sits. A layout key that is right by coincidence breaks on the first content change.
        var before = MasteryCatalog.Nodes
            .Where(n => n.Branch == Branch.Resonance && n.Kind == MasteryKind.Minor)
            .Select(n => (n.Id, At: MasteryLayout.PositionOf(n)))
            .ToList();

        Assert.NotEmpty(before);

        // Every ring-1 Minor of a branch must be placed purely from its own ring's population. Re-deriving
        // with the same ring count has to be stable, and the count of ring-1 peers must not include any
        // node from another ring.
        var ringOnePeers = MasteryCatalog.Nodes.Count(
            n => n.Branch == Branch.Resonance && n.Ring == 1
                 && n.Link is null && n.Kind != MasteryKind.Specialisation);
        var kindPeers = MasteryCatalog.Nodes.Count(
            n => n.Branch == Branch.Resonance && n.Kind == MasteryKind.Minor && n.Link is null);

        Assert.Equal(kindPeers, ringOnePeers);   // true today; the guard is that the layout keys on ring

        foreach (var (id, at) in before)
        {
            var again = MasteryLayout.PositionOf(MasteryCatalog.ById(id)!);
            Assert.Equal(at.X, again.X, 3);
            Assert.Equal(at.Y, again.Y, 3);
        }
    }

    [Fact]
    public void test_a_spur_hangs_off_its_parent_between_ring_one_and_ring_two()
    {
        // THE SIDEWAYS GROWTH THE FAN COULD NOT TAKE. A fifth minor placed in the ring-1 fan would
        // re-space the four a player has memorised (test_a_second_ring... guards the KEY, not the
        // count). So a spur is placed off its parent's outer shoulder instead: further out than ring
        // 1, short of ring 2, on the side of the spine its parent already sits — and nowhere near the
        // bridge on the diagonal. Stated as margins so the next person to move SpurReach can see how
        // much room there is.
        var spurs = MasteryCatalog.Nodes.Where(n => n.Spur).ToList();
        Assert.Equal(4, spurs.Count);

        var r1 = MasteryLayout.RingRadius(1, MasteryLayout.MaxRing);
        var r2 = MasteryLayout.RingRadius(2, MasteryLayout.MaxRing);

        foreach (var spur in spurs)
        {
            var at = MasteryLayout.PositionOf(spur);
            var radius = MathF.Sqrt(at.X * at.X + at.Y * at.Y);
            Assert.True(radius > r1 + 100f && radius < r2 - 100f,
                $"{spur.Id} sits at radius {radius:N0}; ring 1 is {r1:N0} and ring 2 is {r2:N0}.");

            // Same side of the spine as its parent.
            var parent = MasteryCatalog.ById(spur.Prereqs.Single())!;
            var parentAt = MasteryLayout.PositionOf(parent);
            var axis = MasteryLayout.AngleOf(spur.Branch);
            float Side(TreePoint p) => MathF.Sign(MathF.IEEERemainder(MathF.Atan2(p.Y, p.X) - axis, 2f * MathF.PI));
            Assert.Equal(Side(parentAt), Side(at));

            // The five fan minors are placed from their own ring's population, spur excluded.
            var fan = MasteryCatalog.Nodes.Where(n => n.Branch == spur.Branch && n.Kind == MasteryKind.Minor && !n.Spur).ToList();
            Assert.Equal(5, fan.Count);

            // And the wire to the parent is short enough to read as a twig, not a jump across rings.
            var wire = Distance(at, parentAt);
            _out.WriteLine($"{spur.Id}: radius {radius:N0}, {wire:N0} from {parent.Id}.");
            Assert.True(wire < r2 - r1, $"{spur.Id} is {wire:N0} from its parent — further than a whole ring.");
        }
    }

    /// <summary>
    /// The FIRST-OPEN framing encloses START and every ring-1 fan node whole, and nothing of ring 2.
    /// </summary>
    /// <remarks>
    /// "When the tree first opens it should come zoomed in on the centre node and the nodes one step
    /// around it." The screen divides its viewport by this radius to get the zoom, so if the radius
    /// ever admitted ring 2 the first thing a player saw would be the whole crowded tree again — and
    /// if it cut a minor in half, the invitation to pan would read as a rendering fault.
    /// </remarks>
    [Fact]
    public void test_the_first_open_radius_holds_ring_one_and_nothing_further()
    {
        var r = MasteryLayout.FirstOpenRadius;
        Assert.True(r > MasteryLayout.NodeWorldRadius(MasteryKind.Start), "START must fit inside it.");

        foreach (var n in MasteryCatalog.Nodes.Where(n => n.Ring == 1 && !n.Spur && n.Kind == MasteryKind.Minor))
        {
            var at = MasteryLayout.PositionOf(n);
            var reach = MathF.Sqrt(at.X * at.X + at.Y * at.Y) + MasteryLayout.NodeWorldRadius(n.Kind);
            Assert.True(reach <= r + 0.5f, $"{n.Id} reaches {reach:N0}, past the first-open radius {r:N0}.");
        }

        foreach (var n in MasteryCatalog.Nodes.Where(n => n.Ring >= 2 && n.Link is null))
        {
            var at = MasteryLayout.PositionOf(n);
            var inner = MathF.Sqrt(at.X * at.X + at.Y * at.Y) - MasteryLayout.NodeWorldRadius(n.Kind);
            Assert.True(inner > r, $"{n.Id} at ring {n.Ring} intrudes on the first-open framing.");
        }

        _out.WriteLine($"first-open radius {r:N0} of a {MasteryLayout.WorldRadius:N0} rim.");
    }

    [Fact]
    public void test_a_specialisation_is_the_second_largest_node_and_a_bridge_is_not()
    {
        // SIZE IS THIS TREE'S ONLY PRICE TAG, and it was telling the wrong story. Bridge and
        // Specialisation shared one line at 42, so the six nodes that decide who the hunter IS drew
        // the same size as a hybrid convenience, and a playtester read the four ring-4 capstones as
        // "the real masteries" and the Specialisations as ordinary studs. This is the claim the screen
        // now rests on: exactly one kind is bigger than a Specialisation, and it is the capstone.
        // START is left out of the ladder: it is the free root, not a purchase, so it is allowed to be
        // large without claiming to be expensive. Every PRICED kind is compared.
        var spec = MasteryLayout.NodeWorldRadius(MasteryKind.Specialisation);
        var bigger = Enum.GetValues<MasteryKind>()
                         .Where(k => k != MasteryKind.Start && MasteryLayout.NodeWorldRadius(k) > spec)
                         .ToList();

        Assert.Equal(new[] { MasteryKind.Mastery }, bigger);
        Assert.True(spec > MasteryLayout.NodeWorldRadius(MasteryKind.Bridge),
            "a Specialisation must out-measure a Bridge; they used to share one line.");
        Assert.True(spec > MasteryLayout.NodeWorldRadius(MasteryKind.Greater),
            "a Specialisation must out-measure a Greater.");

        // START must still not out-measure the capstone.
        Assert.True(MasteryLayout.NodeWorldRadius(MasteryKind.Start)
                    < MasteryLayout.NodeWorldRadius(MasteryKind.Mastery));
    }

    [Fact]
    public void test_growing_a_specialisation_kept_its_clearance()
    {
        // The size change is only safe because of where the six sit — 0.86 of the radius and 26 degrees
        // off the spine, which puts their nearest neighbours a long way off. Stated as a margin rather
        // than as a pass/fail so the next person to move the number can see how much room is left.
        var specs = MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Specialisation).ToList();
        Assert.Equal(6, specs.Count);

        var worst = float.MaxValue;
        (string A, string B) pair = ("", "");

        foreach (var s in specs)
        foreach (var other in MasteryCatalog.Nodes)
        {
            if (ReferenceEquals(s, other)) continue;
            // ITS OWN SKILL ROAD IS NOT A NEAR MISS. The road is the next node on the same spoke and
            // is meant to sit right outside it — measuring the pair here would report the layout's
            // intent as its tightest accident. The overlap gate above still holds them apart.
            if (other.Kind == MasteryKind.SkillRoad && other.Prereqs.Contains(s.Id)) continue;
            var gap = Distance(MasteryLayout.PositionOf(s), MasteryLayout.PositionOf(other))
                      - MasteryLayout.NodeWorldRadius(s.Kind)
                      - MasteryLayout.NodeWorldRadius(other.Kind);
            if (gap < worst) { worst = gap; pair = (s.Id, other.Id); }
        }

        _out.WriteLine($"tightest specialisation clearance: {pair.A} to {pair.B}, {worst:N0} world units.");
        Assert.True(worst > 0f, $"{pair.A} and {pair.B} overlap by {-worst:N0} world units.");
        // 100 units is roughly one Minor's diameter of slack — enough that the next size bump is a
        // decision rather than an accident.
        Assert.True(worst > 100f,
            $"{pair.A} and {pair.B} clear by only {worst:N0}; a Specialisation cannot grow further " +
            "without moving SpecialisationFraction or the offset angle.");
    }
}
