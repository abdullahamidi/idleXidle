namespace IdleXIdle.Core.Builds;

/// <summary>A point in the mastery tree's own coordinate space. Zoom, pan and window know nothing of it.</summary>
public readonly record struct TreePoint(float X, float Y);

/// <summary>
/// Where every mastery node sits, in world units.
/// </summary>
/// <remarks>
/// <para>
/// This lived in <c>BuildScreen.NodePos</c>, which made it untestable — the one thing about a tree
/// layout worth asserting is that no two nodes land on top of each other, and no test could reach the
/// function that decided. It is Core now, and <c>MasteryLayoutTests</c> asserts exactly that.
/// </para>
/// <para>
/// It also stopped being a lookup table. Ring radius was <c>{1: 0.37, 2: 0.61, 3: 0.81, _: 1.0}</c> and
/// sibling spread was <c>{1: 26°, 2: 16°, 3: 9°, _: 0°}</c>, so ring 5 would have been drawn at the same
/// radius as ring 4 with every sibling at zero spread — every node of that ring stacked on a single
/// point. The tree was not broken, it was UNGROWABLE, and the failure was reserved for whoever added
/// the content. Both are formulas now, and both are checked by test at ring counts the catalogue does
/// not yet have.
/// </para>
/// </remarks>
public static class MasteryLayout
{
    /// <summary>The rim. The outermost ring sits here, whatever ring number that turns out to be.</summary>
    /// <remarks>
    /// 2000, from 1500 (2026-08-27): "open up the distance between the nodes". Every ring fraction below
    /// is unchanged, so the whole tree simply grew a third while the nodes grew a sixth
    /// (<see cref="NodeWorldRadius"/>) — neighbours breathe, and every clearance the overlap test
    /// measures got wider rather than narrower.
    /// </remarks>
    public const float WorldRadius = 2000f;

    /// <summary>
    /// How far out ring 1 sits, as a fraction of <see cref="WorldRadius"/>.
    /// </summary>
    /// <remarks>
    /// The first ring is the one number that cannot come from a curve. Even spacing put it at a quarter
    /// of the radius, which is inside the START node's own box — the minors of the left and right
    /// branches were drawn on top of "YOU". Ring 1 has to clear the centre before any spacing rule
    /// applies; everything past it is free to close up.
    /// </remarks>
    public const float InnerFraction = 0.37f;

    /// <summary>
    /// The world-unit gap between two siblings, held CONSTANT at every ring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what replaced the spread table, and it is the reason overlap is now impossible rather
    /// than merely absent. A fixed ANGLE per ring means the physical gap between siblings shrinks as
    /// the ring moves out; the old table hand-compensated for that (26° then 16° then 9°) and simply
    /// stopped having an answer past ring 3. Holding the ARC constant instead makes the funnel fall out
    /// for free — the angle narrows because the radius grew — and it can never run out of rings.
    /// </para>
    /// <para>
    /// 252 is where the old table already was at rings 1 and 2 (555 × 26° and 915 × 16° are both ≈252),
    /// so the shape the design settled on was preserved through the refactor. It is 330 now — the
    /// 2026-08-27 pass grew the rim by a third and the arc with it, so siblings sit as far apart in
    /// proportion as they did. Comfortably more than twice the widest node that can HAVE a sibling — a
    /// Greater at 103 world units — so two neighbours cannot touch.
    /// </para>
    /// </remarks>
    public const float SiblingArc = 330f;

    /// <summary>
    /// Bends the ring spacing so the gaps narrow going outward, matching the funnel the branches read as.
    /// </summary>
    /// <remarks>
    /// 0.9 is not a taste call — it is the exponent that reproduces the hand-picked table this replaced.
    /// Against the old 0.37 / 0.61 / 0.81 / 1.0 it lands 0.370 / 0.607 / 0.812 / 1.0, inside three
    /// thousandths at every ring, so the change is invisible on screen and the tree a player already
    /// knows does not move under them.
    /// </remarks>
    private const float RingCurve = 0.9f;

    /// <summary>A BRIDGE sits between the two branches it spans, close in.</summary>
    /// <remarks>
    /// Deliberately inside ring 2. A bridge is a shortcut between arms, and drawing it out at the rim
    /// would read as a destination.
    /// </remarks>
    public const float BridgeFraction = 0.46f;

    /// <summary>A SPECIALISATION hangs off the side of its branch rather than on the spine.</summary>
    public const float SpecialisationFraction = 0.86f;

    /// <summary>How far past its specialisation a style road's skill node sits, in world units.</summary>
    /// <remarks>
    /// A specialisation's drawn radius is 129 and a road's 114, so anything under 243 has them
    /// touching — the overlap gate caught 150 immediately. 260 clears it with a margin that reads as
    /// a deliberate step rather than a near miss.
    /// </remarks>
    public const float SkillRoadStep = 260f;

    /// <remarks>
    /// 30°, from 26°. Ring 3 holds four greaters since the 2026-08-27 pass and its fan reaches ±17°; at
    /// 26° the outer greater and the specialisation beside it cleared by 36 world units, which the
    /// clearance test rightly refuses. Four degrees buys 147.
    /// </remarks>
    private const float SpecialisationOffsetDegrees = 30f;

    /// <summary>
    /// How far a SPUR reaches out from ring 1, as a fraction of the gap to ring 2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A spur is a fifth minor that hangs off one of the four, and it must NOT join the ring-1 fan:
    /// the fan re-spaces when its count changes, which would move the four minors a player has
    /// memorised, and a spur is a twig off a node, not a fifth petal. So it sits between rings 1 and 2,
    /// off its parent's outer shoulder, where its wire visibly leaves the parent rather than START.
    /// </para>
    /// <para>
    /// The numbers are placed by clearance, not taste. At 0.52 of the way out and 30° off the spine the
    /// spur clears the bridge on the diagonal (0.46 of the radius at 45°) by ~105 world units, the
    /// outermost ring-2 notable by ~85, and its own parent by ~140 — every neighbour by more than a
    /// Minor's diameter. Push the reach past ~0.65 and it hits the notable; pull the angle past ~36° and
    /// it hits the bridge. <c>MasteryLayoutTests.test_no_two_nodes_in_the_catalogue_overlap</c> is what
    /// says so.
    /// </para>
    /// </remarks>
    public const float SpurReach = 0.52f;

    /// <summary>A spur's angle off its branch's spine, in degrees, on the side its parent sits.</summary>
    public const float SpurOffsetDegrees = 30f;

    /// <summary>The four branches are a CROSS: Weight up, Spread down, Tempo right, Endure left.</summary>
    /// <remarks>
    /// The geometry states the design. Weight sits opposite Spread and Tempo opposite Endure because
    /// those pairs are opposed — walking north is visibly walking away from south, before a single node
    /// has been read.
    /// </remarks>
    public static float AngleOf(Branch b) => b switch
    {
        Branch.Resonance => -90f,
        Branch.Tempo => 0f,
        Branch.Loot => 90f,
        _ => 180f,
    } * MathF.PI / 180f;

    /// <summary>The outermost ring the catalogue actually uses.</summary>
    public static int MaxRing => MasteryCatalog.Nodes.Max(n => n.Ring);

    /// <summary>
    /// How far out a ring sits, in world units, given how many rings the tree has.
    /// </summary>
    /// <remarks>
    /// Rings are distributed across <see cref="InnerFraction"/>..1 rather than growing past the rim,
    /// so a deeper tree PACKS rather than spills: the outermost ring is always at
    /// <see cref="WorldRadius"/> and the camera framing that depends on it keeps working. Adding a ring
    /// re-spaces the ones inside it, which is the correct behaviour — the rim is the horizon, and a
    /// horizon that moved every time content was added would make the whole tree jump.
    /// </remarks>
    public static float RingRadius(int ring, int maxRing)
    {
        if (ring <= 0) return 0f;
        if (maxRing <= 1) return WorldRadius;

        var t = (ring - 1) / (float)(maxRing - 1);
        return WorldRadius * (InnerFraction + (1f - InnerFraction) * MathF.Pow(t, RingCurve));
    }

    /// <summary>The angle between two branches. Four arms on a cross.</summary>
    public const float BranchSeparation = MathF.PI / 2f;

    /// <summary>
    /// How much of the gap to the next branch a fan may occupy.
    /// </summary>
    /// <remarks>
    /// The constant-arc rule separates SIBLINGS, and that is all it can do — it knows nothing about the
    /// branch next door. At ring 1 the four minors already spanned 87% of the 90° between arms, leaving
    /// the outermost minor of Weight and the outermost of Tempo 17 world units apart out of a 99-unit
    /// combined radius: not touching, and two nodes away from touching. A fifth minor on any ring-1 arm
    /// would have put them through each other, so the tree could grow OUTWARD and not SIDEWAYS, which
    /// is exactly the half a content author would reach for first.
    ///
    /// Capping the fan at four fifths of the gap costs ring 1 about five degrees of spread and buys
    /// roughly 75 units of clearance between arms — enough for six nodes on a ring rather than four,
    /// and it reads better besides: the four arms are what the cross is supposed to say, and they say
    /// it more clearly with a corridor between them.
    /// </remarks>
    public const float BranchFanFraction = 0.80f;

    /// <summary>
    /// The angle between two siblings on the same ring — a constant arc, capped by the branch's budget.
    /// </summary>
    public static float SiblingSpread(int ring, int maxRing, int count = 2)
    {
        var r = RingRadius(ring, maxRing);
        if (r <= 0f) return 0f;

        var byArc = SiblingArc / r;
        if (count <= 1) return byArc;

        // The whole fan is (count - 1) steps wide, and it has to fit inside its arm's share of the
        // circle. Past this, the ring is genuinely over-subscribed rather than badly spaced, and
        // MasteryLayoutTests.test_no_two_nodes_in_the_catalogue_overlap is what says so.
        var budget = BranchFanFraction * BranchSeparation / (count - 1);
        return MathF.Min(byArc, budget);
    }

    /// <summary>
    /// A node's drawn radius in world units. The overlap rule is stated against this.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Size states price: a node a player can see is expensive before they have read it. The 1.9 is the
    /// factor the screen already applied on top of these numbers when drawing and hit-testing, folded
    /// in here so world-space geometry has one honest answer for "how big is this".
    /// </para>
    /// <para>
    /// <b>Every kind grew a sixth on 2026-08-27</b> ("make the nodes a little bigger") — 26 → 31 for a
    /// Minor up to 64 → 76 for a capstone — while the rim grew a third, so the tree reads looser, not
    /// tighter. The capstone is the largest by a clear step: it is drawn as a round medallion of the
    /// same family as a Greater now, not a plaque, and SIZE is the only thing that says "this is the
    /// big one" — which is why it is the one kind that out-measures a Specialisation.
    /// </para>
    /// <para>
    /// <b>A SPECIALISATION IS THE SECOND-LARGEST NODE, and it used to share a line with BRIDGE at 42.</b>
    /// That was the tree's worst lie about itself. A Specialisation is the one node that decides WHO THE
    /// HUNTER IS — it sets the discipline, it can be taken exactly once, and every skill the player ever
    /// weaves is judged against it — while a Bridge is a 6-point hybrid convenience. Drawn at the same
    /// size, the six choices that name the character were indistinguishable from ordinary studs, and a
    /// playtester reported exactly that: "the Form specialist nodes are small, like any other node — but
    /// are not THOSE the real masteries?" Size is this tree's only pre-reading channel, so the node that
    /// matters most now measures 68 against the ring-4 capstone's 76, and Bridge keeps 49 alone.
    /// </para>
    /// <para>
    /// The change is safe by a wide margin, and the margin is checked rather than asserted: a
    /// Specialisation's nearest neighbours are its branch's outer ring-3 Greaters at ~380 world units
    /// and its capstone at ~1000, against a combined radius of 232 and 274. See
    /// <c>MasteryLayoutTests.test_no_two_nodes_in_the_catalogue_overlap</c>, which is what says so.
    /// </para>
    /// </remarks>
    public static float NodeWorldRadius(MasteryKind k) => 1.9f * k switch
    {
        MasteryKind.Start => 70f,
        MasteryKind.Mastery => 76f,
        MasteryKind.Specialisation => 68f,
        MasteryKind.SkillRoad => 60f,
        MasteryKind.Greater => 54f,
        MasteryKind.Bridge => 49f,
        MasteryKind.Notable => 45f,
        _ => 31f,
    };

    /// <summary>
    /// The world radius that encloses START and the whole of ring 1 — what the tree's FIRST OPEN frames.
    /// </summary>
    /// <remarks>
    /// "When the tree first opens it should come zoomed in on the centre node and the nodes one step
    /// around it." The screen turns this into a zoom by dividing the viewport's half-extent by it, so
    /// the framing is derived from the layout rather than pinned to a number that would be wrong the
    /// day a ring moved. Ring 1 plus a Minor's own radius: the outermost pixel of the outermost minor.
    /// The spurs sit past it on purpose — half a node at the edge of the view is the invitation to pan.
    /// </remarks>
    public static float FirstOpenRadius
        => RingRadius(1, MaxRing) + NodeWorldRadius(MasteryKind.Minor);

    /// <summary>
    /// Which of its ring's siblings this node is, and how many there are — what drives the fan.
    /// </summary>
    /// <remarks>
    /// Grouped by (Branch, RING), not by (Branch, Kind). Those agree today only because every Kind
    /// happens to occupy exactly one ring; the moment a branch gained a second ring of Minors, the two
    /// rings would have shared one sibling list, and the new nodes would have silently re-indexed and
    /// MOVED the existing ones. A layout key that is right by coincidence is a layout key that breaks
    /// on the first content change.
    ///
    /// Bridges, specialisations, spurs and SKILL ROADS are excluded because none of them sits on the
    /// spine — each has its own placement rule below and its own sibling list. A spur is ring 1 by
    /// price and would otherwise re-space the four minors it hangs off; a skill road is ring 3 by
    /// price and DID re-space the greaters when it arrived (2026-08-30), shoving RESONANCE's PURE
    /// onto its own specialisation — the overlap test is what caught it.
    /// </remarks>
    private static (int Index, int Count) SpineSibling(MasteryNode n)
    {
        var peers = MasteryCatalog.Nodes
            .Where(x => x.Branch == n.Branch && x.Ring == n.Ring
                        && x.Link is null && !x.Spur
                        && x.Kind is not MasteryKind.Specialisation and not MasteryKind.SkillRoad)
            .ToList();
        return (Math.Max(0, peers.FindIndex(x => x.Id == n.Id)), Math.Max(1, peers.Count));
    }

    private static (int Index, int Count) SpecialisationSibling(MasteryNode n)
    {
        var peers = MasteryCatalog.Nodes
            .Where(x => x.Branch == n.Branch && x.Kind == MasteryKind.Specialisation)
            .ToList();
        return (Math.Max(0, peers.FindIndex(x => x.Id == n.Id)), Math.Max(1, peers.Count));
    }

    /// <summary>Where a node lives, in world units. Independent of zoom, pan and the window.</summary>
    public static TreePoint PositionOf(MasteryNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return PositionOf(node, MaxRing);
    }

    /// <summary>The same, against an explicit ring count — so a test can pose a tree deeper than today's.</summary>
    public static TreePoint PositionOf(MasteryNode node, int maxRing)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Kind == MasteryKind.Start) return new TreePoint(0f, 0f);

        if (node.Link is { } link)
        {
            var mid = (AngleOf(node.Branch) + AngleOf(link)) / 2f;
            // Endure (180) and Weight (-90) average to 45, which points at Tempo. Rotate that one case.
            if (MathF.Abs(AngleOf(node.Branch) - AngleOf(link)) > MathF.PI) mid += MathF.PI;
            return Polar(WorldRadius * BridgeFraction, mid);
        }

        var baseAngle = AngleOf(node.Branch);

        if (node.Kind == MasteryKind.Specialisation)
        {
            var (sIdx, sCount) = SpecialisationSibling(node);
            var spread = SpecialisationOffsetDegrees * MathF.PI / 180f;
            var sOffset = sCount <= 1 ? -spread : (sIdx - (sCount - 1) / 2f) * 2f * spread;
            return Polar(WorldRadius * SpecialisationFraction, baseAngle + sOffset);
        }

        if (node.Kind == MasteryKind.SkillRoad)
        {
            // ON ITS SPECIALISATION'S OWN SPOKE, one step further out — the road IS that
            // specialisation continued, and any other placement would make the wire cross the rim.
            // FROM ITS PREREQUISITE'S OWN RADIUS, not from the specialisation's. A road is two nodes
            // deep — the style's active, then its passive off that — so the second must step out from
            // where the FIRST actually landed. Reading the fraction instead put both at one radius and
            // the overlap gate caught them stacked.
            var before = node.Prereqs.Select(MasteryCatalog.ById).FirstOrDefault(x => x is not null);
            if (before is not null)
            {
                var at = PositionOf(before, maxRing);
                var angle = MathF.Atan2(at.Y, at.X);
                var radius = MathF.Sqrt(at.X * at.X + at.Y * at.Y);
                return Polar(radius + SkillRoadStep, angle);
            }
        }

        if (node.Spur)
        {
            // Off the parent's OUTER shoulder: the side of the spine the parent already sits on, so
            // the wire reads as leaving that minor and not as crossing the fan. The side is read from
            // the parent's actual position rather than its index, so it stays right if the fan is
            // ever re-ordered.
            var parent = node.Prereqs.Select(MasteryCatalog.ById).FirstOrDefault(p => p is not null);
            var parentAt = parent is null
                ? Polar(RingRadius(1, maxRing), baseAngle)
                : PositionOf(parent, maxRing);
            var parentAngle = MathF.Atan2(parentAt.Y, parentAt.X);
            var side = MathF.IEEERemainder(parentAngle - baseAngle, 2f * MathF.PI) < 0f ? -1f : 1f;

            var r1 = RingRadius(1, maxRing);
            var r2 = RingRadius(2, maxRing);
            return Polar(r1 + SpurReach * (r2 - r1),
                         baseAngle + side * SpurOffsetDegrees * MathF.PI / 180f);
        }

        var (idx, count) = SpineSibling(node);
        var step = SiblingSpread(node.Ring, maxRing, count);
        var offset = count <= 1 ? 0f : (idx - (count - 1) / 2f) * step;
        return Polar(RingRadius(node.Ring, maxRing), baseAngle + offset);
    }

    private static TreePoint Polar(float radius, float angle)
        => new(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
}
