using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// Every node of RESONANCE, TEMPO and ENDURE must CHANGE THE FIGHT.
/// </summary>
/// <remarks>
/// <para>
/// The same guarantee <see cref="ReinforcementLivenessTests"/> makes for the 72 reinforcements, applied
/// to the three branches whose subject is the fight. LOOT has its own file, because a loot node is
/// allowed to change no damage at all and this probe would call the whole branch dormant. It is the
/// cheapest insurance this codebase has: the
/// reinforcement pass found four dials that the catalogue declared and the fight never read, and a
/// mastery node is bought with the scarcer of the two currencies — a player reaches ring 4 once.
/// </para>
/// <para>
/// A node is run against the same build WITHOUT it, and something has to move. The champion is posed so
/// that every dial has somewhere to land: a discipline (or NARROW and BROAD read nothing), a Vow (or
/// PLEDGE and ZEALOT read nothing), and skills of more than one Source (or PURE could never be false).
/// </para>
/// </remarks>
public class MasteryNodeLivenessTests
{
    private readonly ITestOutputHelper _out;
    public MasteryNodeLivenessTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> EveryResonanceNode()
        => MasteryCatalog.Nodes
            // The specialisation is EXCLUDED because it is this test's own constant: a discipline has
            // to be posed on both sides or NARROW and BROAD read nothing, and a node held on both
            // sides can never be the thing that moved.
            // ALL THREE fight branches, not just the re-axed one. TEMPO and ENDURE gained twelve
            // plain-stat minors on 2026-08-30 and a stat node reaches the sim by a different route
            // from a shape node (Hunter.ValueOf rather than Build.Shape) — exactly the kind of second
            // channel BalanceSweepTests was blind to. Nothing was checking it end to end.
            .Where(n => n.Branch is Branch.Resonance or Branch.Tempo or Branch.Endure
                        && n.Kind is not MasteryKind.Start and not MasteryKind.Specialisation
                                     // A STYLE ROAD grants an ABILITY, and these fixtures weave a
                                     // fixed loadout — the skill it teaches is not in it, so the probe
                                     // could only ever report the node dormant. It is proven instead by
                                     // test_a_skill_the_tree_has_not_taught_cannot_be_chosen, which
                                     // asks the question that actually applies: is the skill weavable?
                                     and not MasteryKind.SkillRoad)
            .Select(n => new object[] { n.Id });

    /// <summary>
    /// A tree holding exactly the nodes asked for — prerequisites ignored on purpose.
    /// </summary>
    /// <remarks>
    /// Taking a node the honest way needs its whole road, and the road's own nodes would then be the
    /// thing that moved. <see cref="MasteryTree.RestoreTaken"/> sets the taken set directly, which is
    /// what isolates ONE node's contribution.
    /// </remarks>
    private static MasteryTree TreeWith(params string[] ids)
    {
        var tree = new MasteryTree();
        tree.SetEarned(999);
        // EVERY SKILL ROAD, ALWAYS, on top of whatever the case asks for. All twelve skills are
        // learned on the tree since 2026-08-30, so a probe holding only the node under test would
        // compose an empty build and report the whole branch dormant for a reason that has nothing to
        // do with the node. The roads are held identical on both sides, so they cannot be the thing
        // that moved.
        tree.RestoreTaken(ids.Concat(MasteryCatalog.Nodes
            .Where(n => n.Kind == MasteryKind.SkillRoad).Select(n => n.Id)));
        return tree;
    }

    /// <summary>
    /// A build posed so no dial is dead for want of a subject: four filled slots (so VOW OF COMPLETION
    /// holds and PLEDGE has something to pay on) — two actives and two passives, a skill in every slot.
    /// </summary>
    /// <param name="oneSource">
    /// PURE only pays while the whole weave shares a Source, and every other node wants the matchup
    /// variety a mixed weave gives. Neither build alone can measure all sixteen, so both are run and a
    /// node has to move ONE of them — the same two-contexts rule the reinforcement file uses for depth.
    /// </param>
    private static Build Compose(MasteryTree mastery, bool oneSource)
    {
        Source Src(Source mixed) => oneSource ? Source.Body : mixed;
        // A tree that has STUDIED the sworn vow — the composer refuses untaught ones (P7), and
        // PLEDGE/ZEALOT liveness depends on the vow actually paying.
        var studied = new MemoryDustTree();
        studied.Restore(0, new[] { "vow_study_1" });
        return BuildComposer.Compose(
            studied, mastery, character: null,
            skills: new[]
            {
                new BuildComposer.SkillPick(Src(Source.Body), "vow_complete", SkillId: "hammer_blow"),
                new BuildComposer.SkillPick(Src(Source.Mind), null, SkillId: "volley_spray"),
                new BuildComposer.SkillPick(Src(Source.Nature), null, SkillId: "field_mire"),
                new BuildComposer.SkillPick(Src(Source.Shadow), null, SkillId: "snare_jaws"),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);
    }

    private static (long Dealt, int Kept, int Reached) Fight(Build build, MasteryTree mastery)
    {
        var hunter = new Hunter();
        hunter.SetMasteryStats(mastery.Stats());   // the same push Game1 makes every frame
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool * 40, Health = pool * 20 };
        var run = new SoloExpedition(build, champ, hunter, 260f, 26f,
                                     ExpeditionTuning.Default, Source.Machine, new Random(19));

        long dealt = 0;
        for (var w = 0; w < 6; w++)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
            dealt += run.LastWaveEvents.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => (long)e.Amount);
        }
        return (dealt, champ.Health, run.Wave);
    }

    [Theory]
    [MemberData(nameof(EveryResonanceNode))]
    public void test_taking_a_resonance_node_changes_the_fight(string nodeId)
    {
        // A DISCIPLINE IS POSED, not bought: NARROW and BROAD read Build.Affinity, which only a
        // specialisation sets, and a specialisation costs its own road. Held constant on both sides.
        var spec = MasteryCatalog.Nodes.First(x => x.Kind == MasteryKind.Specialisation).Id;

        var withoutTree = TreeWith(spec);
        var withTree = TreeWith(spec, nodeId);
        var moved = false;
        foreach (var oneSource in new[] { false, true })
        {
            var without = Fight(Compose(withoutTree, oneSource), withoutTree);
            var with = Fight(Compose(withTree, oneSource), withTree);
            _out.WriteLine($"{MasteryCatalog.ById(nodeId)!.Label} [{(oneSource ? "one source" : "mixed")}]: "
                           + $"dealt {without.Dealt} -> {with.Dealt}, kept {without.Kept} -> {with.Kept}, "
                           + $"wave {without.Reached} -> {with.Reached}");
            moved |= without != with;
        }

        Assert.True(moved,
            $"{nodeId} changed NOTHING — the same damage dealt, the same health kept and the same depth "
            + "reached. A mastery node is bought with the scarcer currency, once, and this one is a "
            + "point the player can spend and cannot feel.");
    }

    [Fact]
    public void test_the_branch_pays_in_stats_that_reach_the_champion()
    {
        // The plumbing the minors need, asserted on its own: a node's Stats must survive the tree,
        // the push, and Hunter.ValueOf. If this breaks, six minors go quietly inert.
        var tree = TreeWith("chime");
        Assert.Equal(6f, tree.Stats()[HunterStat.ResonanceAffinity]);

        var plain = new Hunter();
        var lifted = new Hunter();
        lifted.SetMasteryStats(tree.Stats());
        Assert.Equal(plain.ValueOf(HunterStat.ResonanceAffinity) + 6f,
                     lifted.ValueOf(HunterStat.ResonanceAffinity));

        // And respec gives it back — the tree replaces the grant rather than accumulating it.
        lifted.SetMasteryStats(new MasteryTree().Stats());
        Assert.Equal(plain.ValueOf(HunterStat.ResonanceAffinity),
                     lifted.ValueOf(HunterStat.ResonanceAffinity));
    }
}
