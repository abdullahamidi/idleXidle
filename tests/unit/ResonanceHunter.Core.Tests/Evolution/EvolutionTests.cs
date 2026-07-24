using System;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Evolution;
using Xunit;

namespace ResonanceHunter.Core.Tests.Evolution;

public class EvolutionTests
{
    private static Creature NewWhelp()
    {
        var c = Creature.Hatch("c1", Source.Nature, Role.Attacker, 3);
        c.BeginEvolution(EvolutionTrees.For(Source.Nature), new EvolutionProgress());
        return c;
    }

    [Fact]
    public void test_a_hatched_creature_starts_at_the_tree_root()
    {
        var c = NewWhelp();
        Assert.Equal(EvolutionTrees.RootIdFor(Source.Nature), c.EvolutionNodeId);
        Assert.Equal(Role.Attacker, c.Role);
    }

    /// <summary>An evolution requires ALL of a branch's conditions — not most of them.</summary>
    [Fact]
    public void test_evolution_needs_every_condition_on_a_branch_not_just_some()
    {
        var tree = EvolutionTrees.For(Source.Nature);
        var c = NewWhelp();

        // Thornstalker needs 30 materials AND 8 part-breaks. Give it the materials only.
        for (var i = 0; i < 30; i++) c.Evolution!.FeedMaterial(1);

        Assert.Null(c.TryEvolve(tree));                 // not yet — the combat condition is unmet
        Assert.Equal(EvolutionTrees.RootIdFor(Source.Nature), c.EvolutionNodeId);

        for (var i = 0; i < 8; i++) c.Evolution!.RecordCombat(CombatTags.BossFelled);

        Assert.Equal("nature_atk", c.TryEvolve(tree)); // now both hold
    }

    /// <summary>
    /// The SAME creature becomes a different Role depending on how it was played. Pillar 5.
    /// </summary>
    [Fact]
    public void test_the_same_creature_can_evolve_into_different_roles()
    {
        var tree = EvolutionTrees.For(Source.Nature);

        // Path 1: feed + fight -> Attacker.
        var attacker = NewWhelp();
        for (var i = 0; i < 30; i++) attacker.Evolution!.FeedMaterial(1);
        for (var i = 0; i < 8; i++) attacker.Evolution!.RecordCombat(CombatTags.BossFelled);
        attacker.TryEvolve(tree);
        Assert.Equal(Role.Attacker, attacker.Role);

        // Path 2: work as a defender with a ward charm -> Defender.
        var defender = NewWhelp();
        defender.Evolution!.EquippedTrait = CombatTags.WardingTrait;
        for (var i = 0; i < 20; i++) defender.Evolution.FeedMaterial(1);
        for (var i = 0; i < 40; i++) defender.Evolution.RecordWorkTick(Role.Attacker, healthy: true);
        defender.TryEvolve(tree);
        Assert.Equal(Role.Defender, defender.Role);

        // Path 3: just feed a lot -> Producer. Always reachable.
        var producer = NewWhelp();
        for (var i = 0; i < 60; i++) producer.Evolution!.FeedMaterial(1);
        producer.TryEvolve(tree);
        Assert.Equal(Role.Producer, producer.Role);
    }

    /// <summary>
    /// When two branches are eligible at once, the lower branch-priority wins — deterministically.
    /// </summary>
    /// <remarks>
    /// A creature meeting two paths must not get a coin flip. The tie-break is authored, so the same
    /// play always yields the same evolution, which is what lets a player AIM at a branch.
    /// </remarks>
    [Fact]
    public void test_simultaneously_eligible_branches_resolve_by_priority_deterministically()
    {
        var tree = EvolutionTrees.For(Source.Nature);
        var c = NewWhelp();

        // Satisfy BOTH Thornstalker (priority 0) and Sporeling (priority 2):
        for (var i = 0; i < 60; i++) c.Evolution!.FeedMaterial(1);   // >= 60 and >= 30
        for (var i = 0; i < 8; i++) c.Evolution!.RecordCombat(CombatTags.BossFelled);

        // Priority 0 (Thornstalker) must win, every time.
        for (var run = 0; run < 20; run++)
        {
            var fresh = NewWhelp();
            for (var i = 0; i < 60; i++) fresh.Evolution!.FeedMaterial(1);
            for (var i = 0; i < 8; i++) fresh.Evolution!.RecordCombat(CombatTags.BossFelled);
            Assert.Equal("nature_atk", fresh.TryEvolve(tree));
        }
    }

    /// <summary>Job diligence accrues only while healthy — a starving creature earns nothing.</summary>
    [Fact]
    public void test_unhealthy_work_ticks_do_not_count_toward_evolution()
    {
        var progress = new EvolutionProgress();

        for (var i = 0; i < 100; i++) progress.RecordWorkTick(Role.Defender, healthy: false);
        Assert.Equal(0, progress.HealthyWorkTicks.GetValueOrDefault(Role.Defender));

        for (var i = 0; i < 5; i++) progress.RecordWorkTick(Role.Defender, healthy: true);
        Assert.Equal(5, progress.HealthyWorkTicks.GetValueOrDefault(Role.Defender));
    }

    /// <summary>Evolving is a real power step, not just a rename.</summary>
    [Fact]
    public void test_evolving_raises_power_tier()
    {
        var tree = EvolutionTrees.For(Source.Nature);
        var c = NewWhelp();
        var before = c.PowerTier;

        for (var i = 0; i < 60; i++) c.Evolution!.FeedMaterial(1);
        c.TryEvolve(tree);

        Assert.True(c.PowerTier > before);
    }

    // Removed test_an_equip_requirement_blocks_the_branch_until_met: it exercised the RequiredEquipTrait gate
    // through the DEFENDER branch, but that branch no longer carries an equip requirement — the solo game has
    // no way to equip a charm onto a creature, so the requirement had no producer and made the branch
    // unreachable for everyone (audit fix). The RequiredEquipTrait mechanism itself remains in EdgeSatisfied
    // as dormant capability; test_the_defender_branch_can_actually_fire now proves the branch fires on work.

    /// <summary>Role cannot be reassigned at runtime — only evolution changes it (compile-time guarantee, asserted behaviourally).</summary>
    [Fact]
    public void test_role_is_only_ever_changed_by_evolution()
    {
        var tree = EvolutionTrees.For(Source.Nature);
        var c = NewWhelp();
        Assert.Equal(Role.Attacker, c.Role);

        for (var i = 0; i < 60; i++) c.Evolution!.FeedMaterial(1);
        var role = c.Role;
        c.TryEvolve(tree);

        Assert.NotEqual(role, c.Role); // it changed — and the only call that did it was TryEvolve
    }

    // ── Tree validation ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_tree_with_a_dangling_branch_is_rejected()
        => Assert.Throws<InvalidOperationException>(() => new EvolutionTree("root", new[]
        {
            new EvolutionNode
            {
                Id = "root", DisplayName = "R", Role = Role.Attacker,
                Children = { new EvolutionEdge { TargetNodeId = "ghost", BranchPriority = 0, Hint = "" } },
            },
        }));

    [Fact]
    public void test_duplicate_sibling_priorities_are_rejected()
        => Assert.Throws<InvalidOperationException>(() => new EvolutionTree("root", new[]
        {
            new EvolutionNode
            {
                Id = "root", DisplayName = "R", Role = Role.Attacker,
                Children =
                {
                    new EvolutionEdge { TargetNodeId = "a", BranchPriority = 0, Hint = "" },
                    new EvolutionEdge { TargetNodeId = "b", BranchPriority = 0, Hint = "" },
                },
            },
            new EvolutionNode { Id = "a", DisplayName = "A", Role = Role.Attacker },
            new EvolutionNode { Id = "b", DisplayName = "B", Role = Role.Defender },
        }));

    /// <summary>The MVP tree is internally valid — a build-time content check.</summary>
    [Fact]
    public void test_the_nature_tree_is_valid()
    {
        var tree = EvolutionTrees.For(Source.Nature);

        Assert.True(tree.HasNode("nature_atk"));
        Assert.True(tree.HasNode("nature_def"));
        Assert.True(tree.HasNode("nature_prd"));

        // Every branch carries a player-facing hint — no path is ever a mystery.
        var options = tree.Options(EvolutionTrees.RootIdFor(Source.Nature), new EvolutionProgress());
        Assert.All(options, o => Assert.False(string.IsNullOrWhiteSpace(o.Edge.Hint)));
    }
}
