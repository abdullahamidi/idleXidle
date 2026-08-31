using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The skill tree's rules — the ones that decide whether it is a set of decisions or a shopping list.
/// </summary>
/// <remarks>
/// Rewritten with the tree. The previous suite tested a six-Form-arm tree where every node cost one
/// point; almost none of its assertions have a meaning here, because the thing they described is gone.
/// </remarks>
public class MasteryTreeTests
{
    private static readonly Branch[] Branches =
        { Branch.Resonance, Branch.Loot, Branch.Tempo, Branch.Endure };

    private static MasteryTree TreeWith(int points)
    {
        var t = new MasteryTree();
        t.SetEarned(points);
        return t;
    }

    private static IEnumerable<MasteryNode> Of(Branch b, MasteryKind kind)
        => MasteryCatalog.Nodes.Where(n => n.Branch == b && n.Kind == kind && n.Link is null);

    /// <summary>Walk a branch outward, taking everything on the way. Returns what was taken.</summary>
    private static List<string> WalkBranch(MasteryTree t, Branch b)
    {
        var taken = new List<string>();
        foreach (var kind in new[] { MasteryKind.Minor, MasteryKind.Notable, MasteryKind.Greater, MasteryKind.Mastery })
            foreach (var node in Of(b, kind))
                if (t.Take(node.Id)) taken.Add(node.Id);
        return taken;
    }

    // ── The budget. These four are the design's arithmetic, and they are load-bearing. ────────────

    /// <summary>A full branch costs 49 — the unit everything else is priced against.</summary>
    /// <remarks>
    /// Was 31 (4 + 9 + 10 + 8), then 40 when every branch grew a side road after the post-pre-alpha
    /// playtest reached a specialisation inside fifteen minutes. 49 since 2026-08-27, when every ring
    /// gained one more node (1 + 3 + 5 = 9 again) — and the number the rest of the arithmetic hangs off
    /// moved with it.
    /// </remarks>
    [Fact]
    public void test_a_full_branch_costs_forty_nine()
    {
        foreach (var b in Branches)
            Assert.Equal(49, MasteryCatalog.BranchCost(b));
    }

    /// <summary>Every branch is exactly 6 minors, 5 notables, 4 greaters and 1 mastery.</summary>
    /// <remarks>
    /// The shape the design doc states and the layout is tuned for. A branch that quietly gained a
    /// seventh minor or lost a greater would still "work", and the ring fans would silently re-space to
    /// absorb it — which is precisely the kind of drift a count pins.
    /// </remarks>
    [Fact]
    public void test_every_branch_has_six_five_four_one()
    {
        foreach (var b in Branches)
        {
            Assert.Equal(6, Of(b, MasteryKind.Minor).Count());
            Assert.Equal(5, Of(b, MasteryKind.Notable).Count());
            Assert.Equal(4, Of(b, MasteryKind.Greater).Count());
            Assert.Single(Of(b, MasteryKind.Mastery));
            Assert.Single(Of(b, MasteryKind.Minor).Where(n => n.Spur));
        }
    }

    /// <summary>The whole tree is 256: four branches at 49, four bridges at 6, six specialisations at 6.</summary>
    [Fact]
    public void test_the_whole_tree_costs_two_hundred_and_fifty_six()
    {
        // Four branches at 49, four bridges at 6, six specialisations at 6 — and, since 2026-08-30,
        // six STYLE ROADS at 5, one per specialisation, each teaching a skill that has no Form of its
        // own (design §5). A whole road is 11 points: the specialisation, then the skill.
        Assert.Equal(4 * 49 + 4 * 6 + 6 * 6 + 12 * MasteryCatalog.SkillRoadCost, MasteryCatalog.TotalCost);
        Assert.Equal(316, MasteryCatalog.TotalCost);
    }

    /// <summary>No two nodes share an id — a duplicate would make ById, Take and the save ambiguous.</summary>
    [Fact]
    public void test_every_node_id_is_unique()
    {
        var dupes = MasteryCatalog.Nodes.GroupBy(n => n.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, $"duplicate ids: {string.Join(", ", dupes)}");
    }

    /// <summary>
    /// Every node can be reached from START by walking prerequisites — nothing is an island.
    /// </summary>
    /// <remarks>
    /// A node whose prerequisite is misspelled is unreachable and, because prerequisites are any-of
    /// lists resolved by id at CanTake time, nothing at construction would complain. It would sit on
    /// the screen forever locked, which a player reads as "I have not found the way yet".
    /// </remarks>
    [Fact]
    public void test_every_node_is_reachable_from_start()
    {
        var t = TreeWith(10_000);
        bool progress;
        do
        {
            progress = false;
            foreach (var node in MasteryCatalog.Nodes)
            {
                // The two one-per-hunter rules are not reachability; lift them by taking only what
                // CanTake allows and treating the refused capstones/specialisations as reached when
                // their prerequisites are.
                if (t.IsTaken(node.Id)) continue;
                // A SKILL ROAD hangs off a specialisation, and a specialisation is one-per-hunter —
                // so the road is reachable exactly when its specialisation is, and the walk has to
                // lift it the same way or the six roads read as unreachable.
                if (node.Kind is MasteryKind.Mastery or MasteryKind.Specialisation or MasteryKind.SkillRoad)
                {
                    if (node.Unlocked(t.IsTaken)) continue;
                    Assert.True(node.Prereqs.All(p => MasteryCatalog.ById(p) is not null),
                        $"{node.Id} names a prerequisite that does not exist.");
                    continue;
                }
                if (t.Take(node.Id)) progress = true;
            }
        } while (progress);

        // A SKILL ROAD hangs off a specialisation, and the walk above never TAKES a specialisation —
        // one per hunter — so a road can never be Unlocked either. It is reachable exactly when its
        // specialisation is, which is what this asks instead of pretending the chain was walked.
        bool Reached(MasteryNode n)
        {
            if (t.IsTaken(n.Id) || n.Unlocked(t.IsTaken)) return true;
            // A road is TWO nodes deep — the style's active, then its passive off that — so the
            // second one's prerequisite is the first road, not the specialisation. Walk the chain.
            return n.Kind == MasteryKind.SkillRoad
                   && n.Prereqs.Select(MasteryCatalog.ById)
                       .All(p => p is not null && (p.Unlocked(t.IsTaken) || Reached(p)));
        }

        var unreached = MasteryCatalog.Nodes.Where(n => !Reached(n)).Select(n => n.Id).ToList();
        Assert.True(unreached.Count == 0, $"unreachable from START: {string.Join(", ", unreached)}");
    }

    /// <summary>
    /// The side road is a ROAD: spur needs its parent, the side notable needs the spur, the side greater
    /// needs the side notable, and the capstone accepts the side greater.
    /// </summary>
    /// <remarks>
    /// This is the whole answer to "almost at a specialisation in the first fifteen minutes". Nine more
    /// points per branch only slow a player down if they are strung end to end; nine points spread
    /// across the existing any-of fans would be nine more ways to skip ahead.
    /// </remarks>
    [Fact]
    public void test_each_branch_has_one_side_road_from_spur_to_capstone()
    {
        foreach (var b in Branches)
        {
            var spur = Of(b, MasteryKind.Minor).Single(n => n.Spur);
            var parent = MasteryCatalog.ById(spur.Prereqs.Single())!;
            Assert.Equal(MasteryKind.Minor, parent.Kind);
            Assert.Equal(b, parent.Branch);
            Assert.False(parent.Spur, $"{spur.Id} hangs off another spur.");

            var notable = Of(b, MasteryKind.Notable).Single(n => n.Prereqs.Contains(spur.Id));
            Assert.Equal(new[] { spur.Id }, notable.Prereqs);

            var greater = Of(b, MasteryKind.Greater).Single(n => n.Prereqs.Contains(notable.Id));
            Assert.Equal(new[] { notable.Id }, greater.Prereqs);

            var mastery = Of(b, MasteryKind.Mastery).Single();
            Assert.Contains(greater.Id, mastery.Prereqs);

            // Walked as a road it costs 1 + 1 + 3 + 5 + 8 = 18 and needs every step in order.
            var t = TreeWith(18);
            Assert.False(t.CanTake(spur.Id), $"{spur.Id} is takeable before {parent.Id}.");
            Assert.True(t.Take(parent.Id));
            Assert.False(t.CanTake(notable.Id), $"{notable.Id} is takeable before {spur.Id}.");
            Assert.True(t.Take(spur.Id));
            Assert.False(t.CanTake(greater.Id), $"{greater.Id} is takeable before {notable.Id}.");
            Assert.True(t.Take(notable.Id));
            Assert.False(t.CanTake(mastery.Id), $"{mastery.Id} is takeable before any greater.");
            Assert.True(t.Take(greater.Id));
            Assert.True(t.Take(mastery.Id));
            Assert.Equal(0, t.Available);
        }
    }

    /// <summary>A full career at today's content, on the real curve: six regions walked to depth 225.</summary>
    /// <remarks>
    /// The budget the two tests below used to hard-code as 60 ("6 regions × depth 50 / 5") was the old
    /// linear faucet's number. Read from <see cref="MasteryPoints"/> instead, so the invariants are
    /// measured against the curve the game actually pays — and a curve change that broke them would
    /// break them here rather than in a playtest.
    /// </remarks>
    private static int FullCareerPoints => MasteryPoints.Total(new[] { 225, 225, 225, 225, 225, 225 });

    /// <summary>
    /// Two complete branches cost more than a full career earns.
    /// </summary>
    /// <remarks>
    /// THE INVARIANT THE WHOLE TREE RESTS ON. Six regions at depth 225 pay 78 points; two branches are
    /// 98. If this ever inverts, the opposed pairs stop being opposed — a player simply buys both Weight
    /// and Spread and the tree's central decision evaporates.
    /// </remarks>
    [Fact]
    public void test_two_complete_branches_are_out_of_reach()
    {
        var budgetAtFullContent = FullCareerPoints;
        var two = MasteryCatalog.BranchCost(Branch.Resonance) + MasteryCatalog.BranchCost(Branch.Loot);

        Assert.True(two > budgetAtFullContent,
            $"Two branches cost {two} against {budgetAtFullContent} available. Both halves of an opposed " +
            "pair are affordable, so the tree no longer asks anything.");
    }

    /// <summary>The whole tree is roughly three to four times what a career earns.</summary>
    /// <remarks>
    /// 60 / 184 was 33%; the side roads took the tree to 220 and 60 points to 27%; the 2026-08-27 pass
    /// took it to 256 and the curve to 0.9, so six regions at depth 150 buy 66 (26%) and at depth 225
    /// buy 78 (30%). What this test guards is that the tree never becomes mostly affordable (the
    /// ceiling), and never so large that a career buys less than a branch and a half (the floor:
    /// 0.25 × 256 = 64 > 49).
    ///
    /// <b>The floor moved on 2026-08-30</b>, twice in one day. Six style roads took the tree from 256
    /// to 286 and a career from 26% to 23%; then ALL TWELVE skills moved onto the tree — the designer
    /// retired composing a skill from a Source and a Form — which took it to 316 and 21%. The floor's REASON still holds — its point is that a
    /// career must buy more than one branch, and 0.22 × 286 = 63, still comfortably over 49. What
    /// changed is that the tree grew twice and the point income did not, which is a real signal and is
    /// deliberately NOT answered here by quietly raising the curve: whether a champion should earn
    /// more mastery now that mastery also buys skills is a playtest question, not a number to move
    /// because a test went red.
    /// </remarks>
    [Fact]
    public void test_about_a_third_of_the_tree_is_reachable()
    {
        var midCareer = MasteryPoints.Total(new[] { 150, 150, 150, 150, 150, 150 }) / (float)MasteryCatalog.TotalCost;
        var fullCareer = FullCareerPoints / (float)MasteryCatalog.TotalCost;

        Assert.InRange(midCareer, 0.20f, 0.38f);
        Assert.InRange(fullCareer, 0.20f, 0.38f);
        Assert.True(FullCareerPoints > MasteryCatalog.BranchCost(Branch.Resonance) * 1.5f,
            "a full career must buy a branch and a half, or the second start is fiction.");
    }

    /// <summary>Points are spent by COST, not by node count.</summary>
    /// <remarks>
    /// REGRESSION. Spent used to be "nodes taken minus the free start", which is correct only in a tree
    /// where everything costs one. With ring pricing that formula lets a player take a 8-point mastery
    /// for 1, and the whole budget invariant above becomes fiction.
    /// </remarks>
    [Fact]
    public void test_points_are_spent_by_cost_not_by_count()
    {
        var t = TreeWith(100);
        var minor = Of(Branch.Resonance, MasteryKind.Minor).First();
        var notable = Of(Branch.Resonance, MasteryKind.Notable).First();

        t.Take(minor.Id);
        Assert.Equal(1, t.Spent);

        t.Take(notable.Id);
        Assert.Equal(4, t.Spent);
        Assert.Equal(96, t.Available);
    }

    /// <summary>A node priced above the remaining points cannot be taken.</summary>
    [Fact]
    public void test_points_gate_allocation()
    {
        var t = TreeWith(2);
        var minor = Of(Branch.Tempo, MasteryKind.Minor).First();
        var notable = Of(Branch.Tempo, MasteryKind.Notable).First();

        Assert.True(t.Take(minor.Id));
        Assert.False(t.CanTake(notable.Id));   // 3 points, 1 left
        Assert.False(t.Take(notable.Id));
    }

    // ── The shape of the tree. ────────────────────────────────────────────────────────────────────

    /// <summary>Nothing is reachable without walking to it.</summary>
    [Fact]
    public void test_a_node_needs_its_prerequisite()
    {
        var t = TreeWith(100);
        var mastery = Of(Branch.Endure, MasteryKind.Mastery).Single();

        Assert.False(t.CanTake(mastery.Id));
        WalkBranch(t, Branch.Endure);
        Assert.True(t.IsTaken(mastery.Id));
    }

    /// <summary>
    /// A bridge needs ring 2 of BOTH branches it spans, not either.
    /// </summary>
    /// <remarks>
    /// With a single any-of prerequisite list, one notable on either side would unlock it — which turns a
    /// 6-point hybrid reward into a 3-point splash and deletes the price the design put on hybridising.
    /// </remarks>
    [Fact]
    public void test_a_bridge_needs_both_branches()
    {
        var bridge = MasteryCatalog.Nodes.First(n => n.Kind == MasteryKind.Bridge);
        var t = TreeWith(100);

        foreach (var node in Of(bridge.Branch, MasteryKind.Minor).Take(1)) t.Take(node.Id);
        foreach (var node in Of(bridge.Branch, MasteryKind.Notable).Take(1)) t.Take(node.Id);

        Assert.False(t.CanTake(bridge.Id),
            "One side of the bridge unlocked it. Hybridising is meant to cost two branches' worth of " +
            "commitment, not one.");

        var other = bridge.Link!.Value;
        foreach (var node in Of(other, MasteryKind.Minor).Take(1)) t.Take(node.Id);
        foreach (var node in Of(other, MasteryKind.Notable).Take(1)) t.Take(node.Id);

        Assert.True(t.CanTake(bridge.Id));
    }

    /// <summary>One mastery, ever. The branch you did not master is the shape of your build.</summary>
    [Fact]
    public void test_only_one_branch_can_be_mastered()
    {
        var t = TreeWith(400);
        WalkBranch(t, Branch.Resonance);
        Assert.Equal(Branch.Resonance, t.MasteredBranch());

        WalkBranch(t, Branch.Loot);
        Assert.False(t.IsTaken(Of(Branch.Loot, MasteryKind.Mastery).Single().Id));
        Assert.Equal(Branch.Resonance, t.MasteredBranch());
    }

    // ── The two rules the design says both trees obey. ────────────────────────────────────────────

    /// <summary>
    /// No node is a bare multiplier.
    /// </summary>
    /// <remarks>
    /// The audit found 31 of 43 nodes in the old tree were flat percentages, which is what made three
    /// separate trees feel like one screen with different colours. Every node here must move a SHAPE —
    /// hit size, target count, cooldown, threshold, trigger — and a node that changes nothing at all is
    /// the worse version of the same failure.
    /// </remarks>
    [Fact]
    public void test_every_node_changes_a_shape()
    {
        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Kind != MasteryKind.Start))
            Assert.True(
                node.Shape != SkillShape.None || node.Grant is not null || node.Stats is { Count: > 0 }
                || node.GrantsSkillId is not null,
                $"{node.Id} changes nothing. A node that is only a price is worse than a flat percentage.");
    }

    /// <summary>
    /// Every mastery is a trade — it must make at least one thing worse.
    /// </summary>
    /// <remarks>
    /// The same rule Keystones.cs already enforces for the trait tree, extended here. A mastery that is
    /// pure upside is not an identity, it is a reward for having played longer, and the branch it ends
    /// stops being a decision.
    /// </remarks>
    [Fact]
    public void test_every_mastery_is_a_trade()
    {
        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Mastery))
        {
            var s = node.Shape;
            var costs = s.HitSize < 1f || s.SkillRate < 1f || s.MaxHealth < 1f || s.DamageDealt < 1f
                        || s.LaterHitMultiplier < 1f || s.AfterOpeningPenalty > 0f
                        || s.VsOtherPenalty > 0f || s.OverwhelmFloor > 0f;

            Assert.True(costs, $"{node.Id} is pure upside. Every mastery must make something worse.");
        }
    }

    /// <summary>
    /// Every greater carries a visible price too.
    /// </summary>
    /// <remarks>
    /// Ring 3 is where a branch stops being free. BASTION and CASCADE are the two exceptions: both are
    /// conditional rather than costed — they pay only when a condition the player must build for holds,
    /// which is a price of a different kind. Damage TAKEN above 1 joined the list of recognised prices
    /// with RUSH: Tempo's side-road greater pays in the opposed branch's currency, which is the most
    /// honest price a Tempo node can carry. A LATER-CAST multiplier under 1 joined it with OPENING
    /// VOLLEY, whose price is every cast after each skill's first.
    /// </remarks>
    [Fact]
    public void test_greaters_are_costed_or_conditional()
    {
        // ABSORB joined the list when it moved down a ring in the 2026-08-30 pass: "mitigation rises
        // as health falls" pays nothing to a champion that is winning, which is a price of the same
        // kind BASTION carries.
        var conditional = new[] { "bastion", "cascade", "assassinate", "pure", "absorb" };

        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Greater))
        {
            if (conditional.Contains(node.Id)) continue;
            var s = node.Shape;
            Assert.True(
                s.HitSize < 1f || s.SkillRate < 1f || s.DamageDealt < 1f || s.VsOtherPenalty > 0f
                || s.DamageTaken > 1f || s.LaterCastMultiplier < 1f || s.AutoAttackDamage < 1f,
                $"{node.Id} is an unconditional upgrade at ring 3. Greaters cost something.");
        }
    }

    /// <summary>The four new greaters each carry a visible price, and no two pay in the same coin.</summary>
    [Fact]
    public void test_the_new_greaters_each_pay_a_different_price()
    {
        Assert.True(MasteryCatalog.ById("opening_volley")!.Shape.LaterCastMultiplier < 1f,
            "OPENING VOLLEY must pay on every later cast.");
        Assert.True(MasteryCatalog.ById("rebound")!.Shape.DamageDealt < 1f, "REBOUND must pay in damage dealt.");
    }

    /// <summary>
    /// The new node of each ring sits in the FAN: any spine node of the ring below unlocks it, and it
    /// in turn unlocks the fan above — a player who walks through it is never stranded short of the rim.
    /// </summary>
    [Fact]
    public void test_the_new_nodes_are_wired_into_their_fans()
    {
        // WEIGHT's trio (HEFT, HEADLONG, STAGGER) and SPREAD's (FAN, RALLY, TIDE) went with their
        // branches — the 2026-08-30 re-axe replaced both, with RESONANCE and LOOT. Their sixteen nodes
        // each are pinned by MasteryNodeLivenessTests and LootNodeLivenessTests instead, one at a time
        // and against the fight rather than against the wiring.
        var road = new (string Minor, string Notable, string Greater)[]
        {
            // TEMPO and ENDURE's ring-1 rules became plain stats on 2026-08-30, so the trio starts
            // at a STAT minor now and the rule it used to start at (BRISK) is the notable.
            ("bite", "brisk", "opening_volley"),
            ("hide", "payback", "rebound"),
        };

        foreach (var (minor, notable, greater) in road)
        {
            var t = TreeWith(100);
            Assert.True(t.Take(minor), $"{minor} should hang off START.");
            Assert.True(t.Take(notable), $"{notable} should accept {minor}.");
            Assert.True(t.Take(greater), $"{greater} should accept {notable}.");
            var capstone = Of(MasteryCatalog.ById(greater)!.Branch, MasteryKind.Mastery).Single();
            Assert.True(t.Take(capstone.Id), $"{capstone.Id} should accept {greater}.");
            Assert.Equal(1 + 3 + 5 + 8, t.Spent);
        }
    }

    /// <summary>The three side-road greaters each carry a visible price, and each pays differently.</summary>
    [Fact]
    public void test_the_side_road_greaters_each_pay_a_different_price()
    {
        Assert.True(MasteryCatalog.ById("zealot")!.Shape.DamageTaken > 1f, "ZEALOT must pay in bites taken.");
        Assert.True(MasteryCatalog.ById("lode")!.Shape.AutoAttackDamage < 1f, "LODE must pay in the swing.");
        Assert.True(MasteryCatalog.ById("rush")!.Shape.DamageTaken > 1f, "RUSH must pay in bites taken.");
        Assert.True(MasteryCatalog.ById("rebound")!.Shape.DamageDealt < 1f, "REBOUND must pay in damage dealt.");
    }

    /// <summary>
    /// This tree grants ONLY the six Form-combo triggers.
    /// </summary>
    /// <remarks>
    /// The old mastery tree handed out general triggers for free while the Dust tree charged a permanent
    /// price for the same ones, so one tree was quietly giving away what the other sold. A Form-combo
    /// trigger is dead weight without its Form, which makes it a specialisation rather than a gift.
    /// </remarks>
    [Fact]
    public void test_only_form_combo_triggers_are_granted()
    {
        var formCombo = new[]
        {
            BuildTrigger.Execute, BuildTrigger.Coiled, BuildTrigger.Overdraw,
            BuildTrigger.Radiance, BuildTrigger.Linger, BuildTrigger.Siphon,
        };

        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Grant is not null))
            Assert.Contains(node.Grant!.Value, formCombo);
    }

    /// <summary>Each of the six Forms has exactly one specialisation, and it is the only Form node.</summary>
    [Fact]
    public void test_every_form_has_one_specialisation()
    {
        var specs = MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Specialisation).ToList();

        Assert.Equal(6, specs.Count);
        Assert.Equal(6, specs.Select(n => n.Form).Distinct().Count());
        Assert.All(specs, n => Assert.Equal(MasteryCatalog.SpecialisationCost, n.Cost));
    }

    // ── Allocation, refund, respec. ───────────────────────────────────────────────────────────────

    /// <summary>Taking nodes composes their shapes and collects their triggers.</summary>
    [Fact]
    public void test_taken_nodes_compose_their_shapes()
    {
        var t = TreeWith(100);
        WalkBranch(t, Branch.Resonance);
        t.Take("spec_strike");

        var shape = t.Shape();

        // RESONANCE does not sell hit size — that was WEIGHT, and the skills own it now (design §9).
        // What a walked RESONANCE road must produce is a champion whose skills are worth more before
        // any one of them is chosen: a deeper matchup, a stronger lean, a heavier Vow.
        Assert.True(shape.StrongMatchupBonus > 0f, "KEYED's matchup bonus did not reach the shape.");
        Assert.True(shape.VowPowerMultiplier > 1f, "PLEDGE's Vow power did not reach the shape.");
        Assert.True(shape.AllMatchupsStrong, "CHORD did not reach the shape.");
        Assert.Contains(BuildTrigger.Execute, t.Triggers());
        Assert.Equal(Form.Strike, t.Affinity());
    }

    /// <summary>Respec returns every point, instantly and for free.</summary>
    /// <remarks>
    /// The asymmetry against the permanent trait tree IS the distinction between the two. If a respec
    /// ever costs anything, the skill tree stops being a workbench and the two collapse into one screen.
    /// </remarks>
    [Fact]
    public void test_respec_gives_every_point_back()
    {
        var t = TreeWith(60);
        WalkBranch(t, Branch.Loot);
        Assert.True(t.Spent > 0);

        t.Respec();

        Assert.Equal(0, t.Spent);
        Assert.Equal(60, t.Available);
        Assert.Null(t.MasteredBranch());
    }

    /// <summary>A single node can be given back — but never one something else stands on.</summary>
    /// <remarks>
    /// Refusing to strand is deliberate. A click that quietly removed six other nodes to stay consistent
    /// would be indistinguishable from a bug, and the player would have no idea what they had lost.
    /// </remarks>
    [Fact]
    public void test_a_refund_never_strands_a_node()
    {
        var t = TreeWith(100);
        var notable = Of(Branch.Tempo, MasteryKind.Notable).First();
        // The SPINE minors — the ones the notable lists. The spur is a different case, below.
        var minors = Of(Branch.Tempo, MasteryKind.Minor).Where(m => notable.Prereqs.Contains(m.Id)).ToList();
        Assert.Equal(5, minors.Count);

        foreach (var m in minors) t.Take(m.Id);
        t.Take(notable.Id);

        // Every spine minor is a valid prerequisite for the notable, so all but the last may go back.
        var refunded = minors.Count(m => t.Refund(m.Id));
        Assert.Equal(minors.Count - 1, refunded);
        Assert.True(t.IsTaken(notable.Id));

        Assert.True(t.Refund(notable.Id));
    }

    /// <summary>A spur's parent cannot be refunded while the spur stands on it.</summary>
    /// <remarks>
    /// The spur is the one minor with a SINGLE prerequisite, so it is the one place a minor-for-minor
    /// refund can strand something. Refusing is the same rule as everywhere else; this just proves the
    /// rule reaches the new shape.
    /// </remarks>
    [Fact]
    public void test_a_spur_holds_its_parent_until_it_is_refunded()
    {
        var t = TreeWith(100);
        var spur = Of(Branch.Tempo, MasteryKind.Minor).Single(n => n.Spur);
        var parent = spur.Prereqs.Single();

        Assert.True(t.Take(parent));
        Assert.True(t.Take(spur.Id));

        Assert.False(t.Refund(parent), $"{parent} was refunded out from under {spur.Id}.");
        Assert.True(t.IsTaken(parent));

        Assert.True(t.Refund(spur.Id));
        Assert.True(t.Refund(parent));
        Assert.Equal(0, t.Spent);
    }

    /// <summary>Restoring a save re-derives points rather than trusting them.</summary>
    [Fact]
    public void test_earned_points_survive_a_restore()
    {
        var t = TreeWith(40);
        WalkBranch(t, Branch.Endure);
        var taken = t.Taken.ToList();
        var spent = t.Spent;

        var restored = TreeWith(40);
        restored.RestoreTaken(taken);

        Assert.Equal(spent, restored.Spent);
        Assert.Equal(t.Available, restored.Available);
    }

    /// <summary>An unknown id from an older save is dropped, not crashed on.</summary>
    [Fact]
    public void test_an_unknown_node_id_is_ignored_on_restore()
    {
        var t = TreeWith(20);
        t.RestoreTaken(new[] { "strike_1", "volley_m", "chime" });

        Assert.True(t.IsTaken("chime"));
        Assert.False(t.IsTaken("strike_1"));
        Assert.Equal(1, t.Spent);
    }
}
