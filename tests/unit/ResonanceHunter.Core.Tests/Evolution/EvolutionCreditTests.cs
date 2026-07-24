using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Evolution;
using Xunit;

namespace ResonanceHunter.Core.Tests.Evolution;

/// <summary>
/// The champion's combat must REACH the warren's evolution trees.
/// </summary>
/// <remarks>
/// This is the regression that would have caught the bug it was written for: after the squad became a
/// single champion, nothing produced <see cref="CombatTags.BossFelled"/> or <see cref="CombatTags.WaveCleared"/>,
/// so the ATTACKER and SUPPORT branches — which the tree openly hints "FELL 4 BOSSES" / "CLEAR 40 WAVES" —
/// could not fire for any creature in any save. These tests prove the produced tag reaches the consumer
/// (<see cref="EvolutionTree.EligibleBranch"/>), not merely that a counter went up.
/// </remarks>
public class EvolutionCreditTests
{
    private static Creature Begun(string id, Source source, Role role)
    {
        var c = Creature.Hatch(id, source, role, 1);
        c.BeginEvolution(EvolutionTrees.For(source), new EvolutionProgress());
        return c;
    }

    private static string Node(Source source, string suffix)
        => $"{source.ToString().ToLowerInvariant()}_{suffix}";

    [Fact]
    public void test_a_cleared_wave_tallies_for_every_creature_and_a_boss_tallies_both()
    {
        var roster = new[]
        {
            Begun("a", Source.Nature, Role.Attacker),
            Begun("b", Source.Machine, Role.Support),
        };

        EvolutionCredit.CreditWave(roster, wasBoss: false);
        EvolutionCredit.CreditWave(roster, wasBoss: true);

        foreach (var c in roster)
        {
            Assert.Equal(2, c.Evolution!.CombatTally.GetValueOrDefault(CombatTags.WaveCleared));
            Assert.Equal(1, c.Evolution!.CombatTally.GetValueOrDefault(CombatTags.BossFelled));
        }
    }

    [Fact]
    public void test_a_creature_that_has_not_begun_evolving_is_skipped_not_thrown()
    {
        // HatchRandom without BeginEvolution leaves Evolution null; crediting must no-op, not NRE.
        var raw = Creature.Hatch("x", Source.Body, Role.Producer, 1);
        Assert.Null(raw.Evolution);
        EvolutionCredit.CreditWave(new[] { raw }, wasBoss: true);   // must not throw
    }

    [Fact]
    public void test_felling_bosses_makes_the_ATTACKER_branch_reachable()
    {
        var c = Begun("a", Source.Nature, Role.Attacker);
        var tree = EvolutionTrees.For(Source.Nature);
        c.Evolution!.FeedMaterial(30);   // ATTACKER wants 30 materials AND 4 bosses felled

        // The bug, stated: fully fed, but with no combat produced, the boss branch cannot fire.
        Assert.Null(tree.EligibleBranch(c.EvolutionNodeId, c.Evolution));

        for (var i = 0; i < 4; i++) EvolutionCredit.CreditWave(new[] { c }, wasBoss: true);

        var branch = tree.EligibleBranch(c.EvolutionNodeId, c.Evolution);
        Assert.NotNull(branch);
        Assert.Equal(Node(Source.Nature, "atk"), branch!.TargetNodeId);
    }

    [Fact]
    public void test_clearing_waves_makes_the_SUPPORT_branch_reachable()
    {
        var c = Begun("a", Source.Nature, Role.Attacker);
        var tree = EvolutionTrees.For(Source.Nature);
        c.Evolution!.FeedMaterial(25);   // SUPPORT wants 25 materials AND 40 waves cleared

        for (var i = 0; i < 40; i++) EvolutionCredit.CreditWave(new[] { c }, wasBoss: false);

        var branch = tree.EligibleBranch(c.EvolutionNodeId, c.Evolution);
        Assert.NotNull(branch);
        Assert.Equal(Node(Source.Nature, "sup"), branch!.TargetNodeId);
    }

    [Fact]
    public void test_opening_chests_makes_the_CRAFTER_branch_reachable()
    {
        // CRAFTER used to want "banked runs" — a deleted mechanic, so the branch was dead. It is
        // chests-opened now; prove the new input reaches the consumer, like ATTACKER and SUPPORT.
        var c = Begun("a", Source.Nature, Role.Attacker);
        var tree = EvolutionTrees.For(Source.Nature);
        c.Evolution!.FeedMaterial(45);   // CRAFTER wants 45 materials AND 5 chests opened

        // Fully fed but no chests cracked → the branch cannot fire yet.
        Assert.NotEqual(Node(Source.Nature, "crf"),
            tree.EligibleBranch(c.EvolutionNodeId, c.Evolution)?.TargetNodeId);

        for (var i = 0; i < 5; i++) EvolutionCredit.CreditChestOpened(new[] { c });

        var branch = tree.EligibleBranch(c.EvolutionNodeId, c.Evolution);
        Assert.NotNull(branch);
        Assert.Equal(Node(Source.Nature, "crf"), branch!.TargetNodeId);
    }
}
