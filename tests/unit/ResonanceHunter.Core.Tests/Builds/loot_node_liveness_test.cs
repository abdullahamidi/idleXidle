using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// Every node of the re-axed LOOT branch must change WHAT THE RUN PAYS.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="MasteryNodeLivenessTests"/>, and it needs a different instrument: a LOOT
/// node is allowed to change no damage at all, and a damage probe would call the whole branch dormant.
/// What it must move is the haul — gleam carried out, or the quality of what drops.
/// </para>
/// <para>
/// TWO RUNS, one clean and one bloody. The branch is a FORK: UNTOUCHED and SPOTLESS pay for a wave
/// nothing bit you in, BLOODPRICE and GAMBLE pay for ending it nearly dead, and no single fixture can
/// be both. The clean run gives the champion a pool nothing can dent; the bloody one starts it at a
/// sliver so every wave ends under half health. A node has to move ONE of them.
/// </para>
/// </remarks>
public class LootNodeLivenessTests
{
    private readonly ITestOutputHelper _out;
    public LootNodeLivenessTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> EveryLootNode()
        => MasteryCatalog.Nodes
            .Where(n => n.Branch == Branch.Loot
                        && n.Kind is not MasteryKind.Start and not MasteryKind.Specialisation)
            .Select(n => new object[] { n.Id });

    private static MasteryTree TreeWith(params string[] ids)
    {
        var tree = new MasteryTree();
        tree.SetEarned(999);
        tree.RestoreTaken(ids);
        return tree;
    }

    private static Build Compose(MasteryTree mastery)
        => BuildComposer.Compose(
            new MemoryDustTree(), mastery, character: null,
            skills: new[]
            {
                new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a"),
                new BuildComposer.SkillPick(Source.Mind, Form.Projectile, null, "b"),
                new BuildComposer.SkillPick(Source.Nature, Form.Aura, null, "c"),
                new BuildComposer.SkillPick(Source.Shadow, Form.Trap, null, "d"),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

    private static (long Gleam, double Quality, int Reached) Run(MasteryTree mastery, bool bloody)
    {
        var build = Compose(mastery);
        var hunter = new Hunter();
        hunter.SetMasteryStats(mastery.Stats());
        var pool = SoloBattle.ChampionHealth(build, hunter);
        // CLEAN: a pool nothing in the region can dent, so the wave record stays quiet and the
        // untouched rules pay. BLOODY: a sliver, so every wave that clears ends under half health.
        var champ = bloody
            ? new Champion { MaxHealth = pool * 40, Health = pool }
            : new Champion { MaxHealth = pool * 4000, Health = pool * 4000 };
        var run = new SoloExpedition(build, champ, hunter, 260f, 26f,
                                     ExpeditionTuning.Default, Source.Machine, new Random(19));

        long gleam = 0;
        double quality = 0;
        // FORTY, because PROSPECT's floor is depth 20 and PROSPECTOR's is 30. A twelve-wave probe
        // reached neither and reported both as dormant — the branch's own numbers decide how long a
        // run has to be before it can measure them.
        for (var w = 0; w < 40; w++)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
            gleam += run.LastWaveHaul.Gleam;
            quality += run.LastWaveHaul.Quality;
        }
        return (gleam, Math.Round(quality, 4), run.Wave);
    }

    [Theory]
    [MemberData(nameof(EveryLootNode))]
    public void test_taking_a_loot_node_changes_what_the_run_pays(string nodeId)
    {
        var without = TreeWith();
        var with = TreeWith(nodeId);
        var moved = false;
        foreach (var bloody in new[] { false, true })
        {
            var a = Run(without, bloody);
            var b = Run(with, bloody);
            _out.WriteLine($"{MasteryCatalog.ById(nodeId)!.Label} [{(bloody ? "bloody" : "clean")}]: "
                           + $"gleam {a.Gleam} -> {b.Gleam}, quality {a.Quality} -> {b.Quality}, "
                           + $"wave {a.Reached} -> {b.Reached}");
            moved |= a != b;
        }

        Assert.True(moved,
            $"{nodeId} changed NOTHING — the same gleam, the same chest quality and the same depth in "
            + "both a clean run and a bloody one. A LOOT node that does not move the haul is a point "
            + "spent on the branch's own subject and not felt.");
    }

    [Fact]
    public void test_the_branch_is_a_fork_and_not_a_ladder()
    {
        // UNTOUCHED and BLOODPRICE must disagree about which run they want, or the "fork" the branch
        // is built around is decoration and both notables are simply bought together.
        var untouched = TreeWith("untouched");
        var bloodprice = TreeWith("bloodprice");

        var untouchedClean = Run(untouched, bloody: false).Gleam - Run(TreeWith(), bloody: false).Gleam;
        var bloodpriceClean = Run(bloodprice, bloody: false).Gleam - Run(TreeWith(), bloody: false).Gleam;
        var untouchedBloody = Run(untouched, bloody: true).Gleam - Run(TreeWith(), bloody: true).Gleam;
        var bloodpriceBloody = Run(bloodprice, bloody: true).Gleam - Run(TreeWith(), bloody: true).Gleam;

        _out.WriteLine($"UNTOUCHED   clean +{untouchedClean}  bloody +{untouchedBloody}");
        _out.WriteLine($"BLOODPRICE  clean +{bloodpriceClean}  bloody +{bloodpriceBloody}");

        Assert.True(bloodpriceBloody > bloodpriceClean,
            "BLOODPRICE pays no more for a bloody run than a clean one — it is not reading the health.");
        Assert.True(untouchedClean > bloodpriceClean,
            "UNTOUCHED does not beat BLOODPRICE on the run it is FOR, so the fork is not a fork.");
    }
}
