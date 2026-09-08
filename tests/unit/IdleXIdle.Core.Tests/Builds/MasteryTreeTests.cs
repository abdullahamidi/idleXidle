using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The mastery tree's rules — the ones that decide whether it is a set of decisions or a shopping list.
/// </summary>
/// <remarks>
/// Rewritten with the path redesign (2026-09-06; design/gdd/mastery-tree-redesign-2026-09-06.md).
/// The previous suite pinned a ring tree — six minors a branch, any-of fans, a spur — and almost none
/// of its counts have a meaning here. What survives is the arithmetic the design rests on: a career
/// cannot buy two branches, a capstone is one per hunter, every node moves a shape, a refund never
/// strands, a restore re-derives its points.
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

    /// <summary>A branch's own nodes — no bridges, which belong to two.</summary>
    private static IEnumerable<MasteryNode> Of(Branch b, MasteryKind kind)
        => MasteryCatalog.Nodes.Where(n => n.Branch == b && n.Kind == kind && n.Link is null);

    private static IEnumerable<MasteryNode> Of(Branch b)
        => MasteryCatalog.Nodes.Where(n => n.Branch == b && n.Link is null && n.Kind != MasteryKind.Start);

    /// <summary>
    /// Walk a branch outward, taking everything the rule allows, until nothing more can be taken.
    /// Specialisations are left alone (one per hunter — a test that wants one takes it by name).
    /// </summary>
    private static List<string> WalkBranch(MasteryTree t, Branch b)
    {
        var taken = new List<string>();
        bool progress;
        do
        {
            progress = false;
            foreach (var node in Of(b).Where(n => n.Kind != MasteryKind.Specialisation))
                if (!t.IsTaken(node.Id) && t.Take(node.Id)) { taken.Add(node.Id); progress = true; }
        } while (progress);
        return taken;
    }

    /// <summary>The cheapest points from START to a node, walking one parent per side.</summary>
    private static int PathCost(string id)
    {
        var n = MasteryCatalog.ById(id)!;
        if (n.Kind == MasteryKind.Start) return 0;
        var first = n.Prereqs.Min(PathCost);
        var second = n.SecondPrereqs.Count > 0 ? n.SecondPrereqs.Min(PathCost) : 0;
        return n.Cost + first + second;
    }

    // ── The budget. ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Every kind has one price, and every node wears its kind's price.</summary>
    [Fact]
    public void test_every_node_costs_what_its_kind_costs()
    {
        foreach (var n in MasteryCatalog.Nodes)
            Assert.Equal(MasteryCatalog.CostOf(n.Kind), n.Cost);
        Assert.Equal(1, MasteryCatalog.CostOf(MasteryKind.Minor));
        Assert.Equal(3, MasteryCatalog.CostOf(MasteryKind.Notable));
        Assert.Equal(4, MasteryCatalog.CostOf(MasteryKind.SkillRoad));
        Assert.Equal(5, MasteryCatalog.CostOf(MasteryKind.Greater));
        Assert.Equal(6, MasteryCatalog.CostOf(MasteryKind.Specialisation));
        Assert.Equal(6, MasteryCatalog.CostOf(MasteryKind.Bridge));
        Assert.Equal(8, MasteryCatalog.CostOf(MasteryKind.Mastery));
    }

    /// <summary>A route from START to its capstone is about thirty points — the unit the pacing is tuned in.</summary>
    [Fact]
    public void test_a_route_to_its_capstone_costs_about_thirty()
    {
        foreach (var cap in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Mastery))
            Assert.InRange(PathCost(cap.Id), 24, 32);
    }

    /// <summary>The whole tree: the branches by their routes, and four bridges.</summary>
    [Fact]
    public void test_the_whole_tree_is_the_branches_and_the_bridges()
    {
        var branches = Branches.Sum(MasteryCatalog.BranchCost);
        Assert.Equal(branches + 4 * MasteryCatalog.BridgeCost, MasteryCatalog.TotalCost);
        Assert.Equal(244, MasteryCatalog.TotalCost);
    }

    /// <summary>No two nodes share an id — a duplicate would make ById, Take and the save ambiguous.</summary>
    [Fact]
    public void test_every_node_id_is_unique()
    {
        var dupes = MasteryCatalog.Nodes.GroupBy(n => n.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, $"duplicate ids: {string.Join(", ", dupes)}");
    }

    /// <summary>Every prerequisite names a node that exists, and every node can be reached from START.</summary>
    [Fact]
    public void test_every_node_is_reachable_from_start()
    {
        foreach (var n in MasteryCatalog.Nodes)
            foreach (var pre in n.Prereqs.Concat(n.SecondPrereqs))
                Assert.True(MasteryCatalog.ById(pre) is not null, $"{n.Id} names a prerequisite that does not exist: {pre}.");

        // Reachability with the two one-per-hunter rules lifted: walk the rule (Unlocked), not the purse.
        var reached = new HashSet<string> { MasteryCatalog.StartId };
        bool progress;
        do
        {
            progress = false;
            foreach (var n in MasteryCatalog.Nodes)
                if (!reached.Contains(n.Id) && n.Unlocked(reached.Contains)) { reached.Add(n.Id); progress = true; }
        } while (progress);

        var unreached = MasteryCatalog.Nodes.Where(n => !reached.Contains(n.Id)).Select(n => n.Id).ToList();
        Assert.True(unreached.Count == 0, $"unreachable from START: {string.Join(", ", unreached)}");
    }

    /// <summary>A full career at today's content, on the real curve: six regions walked to depth 225.</summary>
    private static int FullCareerPoints => MasteryPoints.Total(new[] { 225, 225, 225, 225, 225, 225 });

    /// <summary>Two complete branches cost more than a full career earns.</summary>
    /// <remarks>
    /// THE INVARIANT THE WHOLE TREE RESTS ON. If this ever inverts, the opposed pairs stop being
    /// opposed — a player simply buys both halves and the tree's central decision evaporates.
    /// </remarks>
    [Fact]
    public void test_two_complete_branches_are_out_of_reach()
    {
        var budget = FullCareerPoints;
        var cheapestTwo = Branches.Select(MasteryCatalog.BranchCost).OrderBy(c => c).Take(2).Sum();
        Assert.True(cheapestTwo > budget,
            $"Two branches cost {cheapestTwo} against {budget} available. Both halves of an opposed pair are affordable.");
    }

    /// <summary>
    /// A career buys a quarter to a third of the tree: more than one whole branch, never two, and
    /// never so little that the second start is fiction.
    /// </summary>
    [Fact]
    public void test_about_a_quarter_of_the_tree_is_reachable()
    {
        var midCareer = MasteryPoints.Total(new[] { 150, 150, 150, 150, 150, 150 }) / (float)MasteryCatalog.TotalCost;
        var fullCareer = FullCareerPoints / (float)MasteryCatalog.TotalCost;

        Assert.InRange(midCareer, 0.20f, 0.38f);
        Assert.InRange(fullCareer, 0.20f, 0.38f);
        Assert.True(FullCareerPoints > Branches.Max(MasteryCatalog.BranchCost),
            "a full career must buy more than one whole branch, or the second start is fiction.");
    }

    /// <summary>Points are spent by COST, not by node count.</summary>
    [Fact]
    public void test_points_are_spent_by_cost_not_by_count()
    {
        var t = TreeWith(100);
        Assert.True(t.Take("chime"));
        Assert.Equal(1, t.Spent);
        Assert.True(t.Take("tone"));
        Assert.True(t.Take("steep"));
        Assert.True(t.Take("road_hammer"));
        Assert.Equal(7, t.Spent);
        Assert.True(t.Take("keyed"));
        Assert.Equal(10, t.Spent);
        Assert.Equal(90, t.Available);
    }

    /// <summary>A node priced above the remaining points cannot be taken.</summary>
    [Fact]
    public void test_points_gate_allocation()
    {
        var t = TreeWith(3);
        Assert.True(t.Take("bite"));
        Assert.True(t.Take("swift"));
        Assert.True(t.Take("sharp"));
        Assert.False(t.CanTake("road_sign"));   // 4 points, 0 left
        Assert.False(t.Take("road_sign"));
    }

    // ── The shape of the tree. ────────────────────────────────────────────────────────────────────

    /// <summary>Every branch is a trunk of two minors on START, a fork, and two routes that each begin with a minor.</summary>
    [Fact]
    public void test_every_branch_is_a_trunk_a_fork_and_two_routes()
    {
        foreach (var b in Branches)
        {
            var trunk = Of(b).Where(n => n.Route == MasteryRoute.Trunk).OrderBy(n => n.Step).ToList();
            Assert.Equal(2, trunk.Count);
            Assert.All(trunk, n => Assert.Equal(MasteryKind.Minor, n.Kind));
            Assert.Equal(new[] { MasteryCatalog.StartId }, trunk[0].Prereqs);
            Assert.Equal(new[] { trunk[0].Id }, trunk[1].Prereqs);

            foreach (var route in new[] { MasteryRoute.Left, MasteryRoute.Right })
            {
                var road = Of(b).Where(n => n.Route == route && n.Kind != MasteryKind.Specialisation).OrderBy(n => n.Step).ToList();
                Assert.InRange(road.Count, 5, 6);
                Assert.Equal(MasteryKind.Minor, road[0].Kind);
                Assert.Equal(new[] { trunk[1].Id }, road[0].Prereqs);
                for (var i = 1; i < road.Count; i++)
                {
                    Assert.Equal(i, road[i].Step);
                    Assert.Equal(new[] { road[i - 1].Id }, road[i].Prereqs);
                }
                Assert.Equal(MasteryKind.Greater, road[^1].Kind);
                Assert.Equal(MasteryKind.SkillRoad, road[1].Kind);   // the first skill is the second step
            }

            var cap = Of(b, MasteryKind.Mastery).Single();
            Assert.Equal(MasteryRoute.Capstone, cap.Route);
            var greaters = Of(b, MasteryKind.Greater).Select(n => n.Id).OrderBy(x => x);
            Assert.Equal(greaters, cap.Prereqs.OrderBy(x => x));
        }
    }

    /// <summary>Every node but a capstone has ONE parent; a capstone has one per route; a bridge one per side.</summary>
    [Fact]
    public void test_every_node_has_one_explicit_parent()
    {
        foreach (var n in MasteryCatalog.Nodes.Where(n => n.Kind != MasteryKind.Start))
        {
            switch (n.Kind)
            {
                case MasteryKind.Mastery:
                    Assert.Equal(2, n.Prereqs.Count);
                    Assert.Empty(n.SecondPrereqs);
                    break;
                case MasteryKind.Bridge:
                    Assert.Single(n.Prereqs);
                    Assert.Single(n.SecondPrereqs);
                    break;
                default:
                    Assert.Single(n.Prereqs);
                    Assert.Empty(n.SecondPrereqs);
                    break;
            }
        }
    }

    /// <summary>A route is a road: each step needs the one before it, and nothing else opens it.</summary>
    [Fact]
    public void test_a_route_is_walked_in_order()
    {
        var t = TreeWith(100);
        Assert.False(t.CanTake("tone"));
        Assert.True(t.Take("chime"));
        Assert.False(t.CanTake("steep"));
        Assert.True(t.Take("tone"));
        Assert.False(t.CanTake("road_hammer"));
        Assert.True(t.Take("steep"));
        Assert.False(t.CanTake("keyed"));
        Assert.True(t.Take("road_hammer"));
        Assert.False(t.CanTake("deep"));
        Assert.True(t.Take("keyed"));
        Assert.False(t.CanTake("road_hammer_2"));
        Assert.True(t.Take("deep"));
        Assert.False(t.CanTake("pure"));
        Assert.True(t.Take("road_hammer_2"));
        Assert.True(t.CanTake("pure"));
    }

    /// <summary>The capstone accepts EITHER route's greater — a fork is a choice, not a checklist.</summary>
    [Fact]
    public void test_a_capstone_accepts_either_route()
    {
        foreach (var b in Branches)
        {
            var cap = Of(b, MasteryKind.Mastery).Single();
            foreach (var greaterId in cap.Prereqs)
            {
                var t = TreeWith(100);
                var chain = new List<string>();
                for (var n = MasteryCatalog.ById(greaterId); n is not null && n.Kind != MasteryKind.Start; n = MasteryCatalog.ById(n.Prereqs[0]))
                    chain.Add(n.Id);
                chain.Reverse();
                foreach (var id in chain) Assert.True(t.Take(id), $"{id} refused on the way to {greaterId}.");
                Assert.True(t.CanTake(cap.Id), $"{cap.Id} does not accept {greaterId} alone.");
            }
        }
    }

    /// <summary>Rewards stand on the road to a skill: the first skill of a route is never its first node.</summary>
    [Fact]
    public void test_a_skill_node_is_never_the_first_step_of_a_route()
    {
        foreach (var road in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.SkillRoad))
        {
            var parent = MasteryCatalog.ById(road.Prereqs.Single())!;
            Assert.NotEqual(MasteryKind.Start, parent.Kind);
            Assert.True(parent.Stats is { Count: > 0 } || parent.Shape != SkillShape.None,
                $"{road.Id} is not preceded by a reward — {parent.Id} gives nothing.");
        }
    }

    /// <summary>Every skill node teaches a real, different skill.</summary>
    [Fact]
    public void test_every_skill_node_teaches_a_different_skill()
    {
        var roads = MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.SkillRoad).ToList();
        Assert.Equal(12, roads.Count);
        Assert.All(roads, r => Assert.NotNull(SkillCatalogue.Find(r.GrantsSkillId!)));
        Assert.Equal(12, roads.Select(r => r.GrantsSkillId).Distinct().Count());
    }

    /// <summary>A bridge needs a named notable on BOTH sides, not either.</summary>
    [Fact]
    public void test_a_bridge_needs_both_branches()
    {
        foreach (var bridge in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Bridge))
        {
            var near = MasteryCatalog.ById(bridge.Prereqs.Single())!;
            var far = MasteryCatalog.ById(bridge.SecondPrereqs.Single())!;
            Assert.Equal(MasteryKind.Notable, near.Kind);
            Assert.Equal(MasteryKind.Notable, far.Kind);
            Assert.Equal(bridge.Branch, near.Branch);
            Assert.Equal(bridge.Link, far.Branch);

            var t = TreeWith(100);
            TakeTo(t, near.Id);
            Assert.False(t.CanTake(bridge.Id), $"{bridge.Id} opened from one side. Hybridising costs two branches' commitment.");
            TakeTo(t, far.Id);
            Assert.True(t.CanTake(bridge.Id));
        }
    }

    private static void TakeTo(MasteryTree t, string id)
    {
        var chain = new List<string>();
        for (var n = MasteryCatalog.ById(id); n is not null && n.Kind != MasteryKind.Start; n = MasteryCatalog.ById(n.Prereqs[0]))
            chain.Add(n.Id);
        chain.Reverse();
        foreach (var step in chain) if (!t.IsTaken(step)) Assert.True(t.Take(step), $"{step} refused.");
    }

    /// <summary>One capstone, ever. The branch you did not master is the shape of your build.</summary>
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

    // ── What the nodes say. ───────────────────────────────────────────────────────────────────────

    /// <summary>No node is only a price: every one moves a shape, a stat, a trigger or a skill.</summary>
    [Fact]
    public void test_every_node_changes_a_shape()
    {
        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Kind != MasteryKind.Start))
            Assert.True(
                node.Shape != SkillShape.None || node.Grant is not null || node.Stats is { Count: > 0 }
                || node.GrantsSkillId is not null,
                $"{node.Id} changes nothing. A node that is only a price is worse than a flat percentage.");
    }

    /// <summary>A minor is ONE idea: one stat, and only its branch's stat, with no shape riding along.</summary>
    [Fact]
    public void test_a_minor_is_one_stat_and_nothing_else()
    {
        var own = new Dictionary<Branch, HunterStat[]>
        {
            [Branch.Resonance] = new[] { HunterStat.ResonanceAffinity },
            [Branch.Tempo] = new[] { HunterStat.AttackPower, HunterStat.Engineering, HunterStat.CriticalChance },
            [Branch.Endure] = new[] { HunterStat.MaxHealth, HunterStat.Defense },
            [Branch.Loot] = new[] { HunterStat.Guile },
        };
        foreach (var n in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Minor))
        {
            Assert.NotNull(n.Stats);
            Assert.Single(n.Stats!);
            Assert.Equal(SkillShape.None, n.Shape);
            Assert.Null(n.Grant);
            Assert.Contains(n.Stats!.Keys.Single(), (IEnumerable<HunterStat>)own[n.Branch]);
        }
    }

    /// <summary>No two nodes above MINOR carry the same bundle under different names.</summary>
    [Fact]
    public void test_no_duplicate_bundles()
    {
        var bundles = MasteryCatalog.Nodes
            .Where(n => n.Kind is not (MasteryKind.Start or MasteryKind.Minor or MasteryKind.SkillRoad))
            .GroupBy(n => (n.Shape, n.Grant, Stats: n.Stats is null ? "" : string.Join(",", n.Stats.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))))
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" = ", g.Select(n => n.Id)))
            .ToList();
        Assert.True(bundles.Count == 0, $"the same bundle under two names: {string.Join("; ", bundles)}");
    }

    /// <summary>Every capstone is a trade — it must make at least one thing worse.</summary>
    [Fact]
    public void test_every_mastery_is_a_trade()
    {
        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Mastery))
        {
            var s = node.Shape;
            var costs = s.HitSize < 1f || s.SkillRate < 1f || s.MaxHealth < 1f || s.DamageDealt < 1f
                        || s.LaterHitMultiplier < 1f || s.AfterOpeningPenalty > 0f;
            Assert.True(costs, $"{node.Id} is pure upside. Every capstone must make something worse.");
        }
    }

    /// <summary>Every greater carries a visible price, or a condition the player must build for.</summary>
    [Fact]
    public void test_greaters_are_costed_or_conditional()
    {
        // PURE pays nothing to a hunter who mixes Sources — a price of a different kind.
        var conditional = new[] { "pure" };
        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Greater))
        {
            if (conditional.Contains(node.Id)) continue;
            var s = node.Shape;
            Assert.True(
                s.HitSize < 1f || s.SkillRate < 1f || s.DamageDealt < 1f
                || s.DamageTaken > 1f || s.LaterCastMultiplier < 1f || s.AutoAttackDamage < 1f,
                $"{node.Id} is an unconditional upgrade. Greaters cost something.");
        }
    }

    /// <summary>The two routes of a branch pay in different coins, so the fork is a real choice.</summary>
    [Fact]
    public void test_the_two_greaters_of_a_branch_pay_differently()
    {
        Assert.True(MasteryCatalog.ById("zealot")!.Shape.DamageTaken > 1f, "ZEALOT pays in bites taken.");
        Assert.True(MasteryCatalog.ById("pure")!.Shape.OneSourceBonus > 0f, "PURE pays in commitment.");
        Assert.True(MasteryCatalog.ById("opening_volley")!.Shape.LaterCastMultiplier < 1f, "OPENING VOLLEY pays on every later cast.");
        Assert.True(MasteryCatalog.ById("blitz")!.Shape.HitSize < 1f, "BLITZ pays in hit size.");
        Assert.True(MasteryCatalog.ById("bulwark")!.Shape.DamageDealt < 1f, "BULWARK pays in damage dealt.");
        Assert.True(MasteryCatalog.ById("rebound")!.Shape.DamageDealt < 1f, "REBOUND pays in damage dealt.");
        Assert.True(MasteryCatalog.ById("second_look")!.Shape.HitSize < 1f, "SECOND LOOK pays in hit size.");
        Assert.True(MasteryCatalog.ById("gamble")!.Shape.DamageTaken > 1f, "GAMBLE pays in bites taken.");
    }

    /// <summary>This tree grants ONLY the six Style-combo triggers.</summary>
    [Fact]
    public void test_only_style_combo_triggers_are_granted()
    {
        var styleCombo = new[]
        {
            BuildTrigger.Execute, BuildTrigger.Coiled, BuildTrigger.Overdraw,
            BuildTrigger.Radiance, BuildTrigger.Linger, BuildTrigger.Siphon,
        };
        foreach (var node in MasteryCatalog.Nodes.Where(n => n.Grant is not null))
            Assert.Contains(node.Grant!.Value, styleCombo);
    }

    /// <summary>Each of the six Styles has exactly one specialisation, hung as a leaf off a route's notable.</summary>
    [Fact]
    public void test_every_style_has_one_specialisation()
    {
        var specs = MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Specialisation).ToList();
        Assert.Equal(6, specs.Count);
        Assert.Equal(6, specs.Select(n => n.Style).Distinct().Count());
        Assert.All(specs, n => Assert.Equal(MasteryCatalog.SpecialisationCost, n.Cost));
        foreach (var s in specs)
        {
            var parent = MasteryCatalog.ById(s.Prereqs.Single())!;
            Assert.Equal(MasteryKind.Notable, parent.Kind);
            Assert.Equal(s.Route, parent.Route);
            Assert.Equal(s.Branch, parent.Branch);
        }
    }

    // ── Allocation, refund, respec, restore. ──────────────────────────────────────────────────────

    /// <summary>Taking nodes composes their shapes and collects their triggers.</summary>
    [Fact]
    public void test_taken_nodes_compose_their_shapes()
    {
        var t = TreeWith(100);
        WalkBranch(t, Branch.Resonance);
        Assert.True(t.Take("spec_strike"));

        var shape = t.Shape();
        Assert.True(shape.StrongMatchupBonus > 0f, "KEYED's matchup bonus did not reach the shape.");
        Assert.True(shape.VowPowerMultiplier > 1f, "PLEDGE's Vow power did not reach the shape.");
        Assert.True(shape.AllMatchupsStrong, "CHORD did not reach the shape.");
        Assert.Contains(BuildTrigger.Execute, t.Triggers());
        Assert.Equal(Style.Hammer, t.Affinity());
    }

    /// <summary>Respec returns every point, instantly and for free.</summary>
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
    [Fact]
    public void test_a_refund_never_strands_a_node()
    {
        var t = TreeWith(100);
        Assert.True(t.Take("bite"));
        Assert.True(t.Take("swift"));
        Assert.True(t.Take("sharp"));
        Assert.True(t.Take("quick"));

        Assert.False(t.Refund("swift"), "SWIFT was refunded out from under the fork.");
        Assert.False(t.Refund("bite"));
        Assert.True(t.Refund("sharp"));
        Assert.False(t.Refund("swift"), "QUICK still stands on SWIFT.");
        Assert.True(t.Refund("quick"));
        Assert.True(t.Refund("swift"));
        Assert.True(t.Refund("bite"));
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

    /// <summary>
    /// A restored node whose parent is not taken — the catalogue moved it, or its parent was retired —
    /// is dropped with everything that stood on it, and its points come back.
    /// </summary>
    /// <remarks>
    /// The path redesign re-parented most of the tree. An old save that held KEYED off a ring-1 fan
    /// would otherwise load as a node the rule says cannot be taken, sitting there forever taken.
    /// </remarks>
    [Fact]
    public void test_a_restore_drops_what_no_longer_stands_on_anything()
    {
        var t = TreeWith(30);
        // KEYED and DEEP without ROAD_HAMMER under them; CHIME and TONE stand.
        t.RestoreTaken(new[] { "chime", "tone", "steep", "keyed", "deep", "hum" });

        Assert.True(t.IsTaken("chime"));
        Assert.True(t.IsTaken("tone"));
        Assert.True(t.IsTaken("steep"));
        Assert.False(t.IsTaken("keyed"), "KEYED was kept without ROAD_HAMMER under it.");
        Assert.False(t.IsTaken("deep"), "DEEP was kept standing on a dropped KEYED.");
        Assert.Equal(3, t.Spent);
        Assert.Equal(27, t.Available);
    }
}
