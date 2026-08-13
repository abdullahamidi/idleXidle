using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

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
        { Branch.Weight, Branch.Spread, Branch.Tempo, Branch.Endure };

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

    /// <summary>A full branch costs 31 — the unit everything else is priced against.</summary>
    [Fact]
    public void test_a_full_branch_costs_thirty_one()
    {
        foreach (var b in Branches)
            Assert.Equal(31, MasteryCatalog.BranchCost(b));
    }

    /// <summary>
    /// Two complete branches cost more than a full career earns.
    /// </summary>
    /// <remarks>
    /// THE INVARIANT THE WHOLE TREE RESTS ON. Six regions at depth 50, one point per five depth, is 60
    /// points; two branches are 62. If this ever inverts, the opposed pairs stop being opposed — a player
    /// simply buys both Weight and Spread and the tree's central decision evaporates.
    /// </remarks>
    [Fact]
    public void test_two_complete_branches_are_out_of_reach()
    {
        const int budgetAtFullContent = 60;   // 6 regions x depth 50 / 5
        var two = MasteryCatalog.BranchCost(Branch.Weight) + MasteryCatalog.BranchCost(Branch.Spread);

        Assert.True(two > budgetAtFullContent,
            $"Two branches cost {two} against {budgetAtFullContent} available. Both halves of an opposed " +
            "pair are affordable, so the tree no longer asks anything.");
    }

    /// <summary>The whole tree is roughly three times what a career earns.</summary>
    [Fact]
    public void test_about_a_third_of_the_tree_is_reachable()
    {
        var fraction = 60f / MasteryCatalog.TotalCost;

        Assert.InRange(fraction, 0.28f, 0.38f);
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
        var minor = Of(Branch.Weight, MasteryKind.Minor).First();
        var notable = Of(Branch.Weight, MasteryKind.Notable).First();

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
        WalkBranch(t, Branch.Weight);
        Assert.Equal(Branch.Weight, t.MasteredBranch());

        WalkBranch(t, Branch.Spread);
        Assert.False(t.IsTaken(Of(Branch.Spread, MasteryKind.Mastery).Single().Id));
        Assert.Equal(Branch.Weight, t.MasteredBranch());
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
                node.Shape != SkillShape.None || node.Grant is not null,
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
    /// which is a price of a different kind.
    /// </remarks>
    [Fact]
    public void test_greaters_are_costed_or_conditional()
    {
        var conditional = new[] { "bastion", "cascade", "assassinate" };

        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Greater))
        {
            if (conditional.Contains(node.Id)) continue;
            var s = node.Shape;
            Assert.True(
                s.HitSize < 1f || s.SkillRate < 1f || s.DamageDealt < 1f || s.VsOtherPenalty > 0f,
                $"{node.Id} is an unconditional upgrade at ring 3. Greaters cost something.");
        }
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
        WalkBranch(t, Branch.Weight);
        t.Take("spec_strike");

        var shape = t.Shape();

        Assert.True(shape.HitSize > 1f, "A walked Weight branch must hit harder.");
        Assert.True(shape.ArmourPenetration > 0f, "SHARPENED's armour cut did not reach the shape.");
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
        WalkBranch(t, Branch.Spread);
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
        var minors = Of(Branch.Tempo, MasteryKind.Minor).ToList();
        var notable = Of(Branch.Tempo, MasteryKind.Notable).First();

        foreach (var m in minors) t.Take(m.Id);
        t.Take(notable.Id);

        // Every minor is a valid prerequisite for the notable, so all but the last may go back.
        var refunded = minors.Count(m => t.Refund(m.Id));
        Assert.Equal(minors.Count - 1, refunded);
        Assert.True(t.IsTaken(notable.Id));

        Assert.True(t.Refund(notable.Id));
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
        t.RestoreTaken(new[] { "strike_1", "volley_m", "heavy_hand" });

        Assert.True(t.IsTaken("heavy_hand"));
        Assert.False(t.IsTaken("strike_1"));
        Assert.Equal(1, t.Spent);
    }
}
