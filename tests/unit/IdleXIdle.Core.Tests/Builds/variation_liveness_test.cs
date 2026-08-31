using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// Every variation must CHANGE THE FIGHT. Twenty-four of them, checked one at a time.
/// </summary>
/// <remarks>
/// <para>
/// A variation the player can buy and cannot feel is the failure this codebase produces more
/// reliably than any other: bought, persisted, resolved, displayed, and never once executed. The
/// catalogue can declare a delta and the sim can quietly not read the dial it turns, and nothing
/// downstream would say so — the build would compose, the screen would show the choice, the fight
/// would run exactly as before.
/// </para>
/// <para>
/// So each variation is run against the same wave as the same skill WITHOUT it, and something has to
/// move: damage dealt, or health kept. Which of the two is deliberately not specified — a defensive
/// variation like BANKED or IRON changes what the champion keeps rather than what it deals, and
/// demanding damage from those would only teach the next author to write damage variations.
/// </para>
/// </remarks>
public class VariationLivenessTests
{
    private readonly ITestOutputHelper _out;
    public VariationLivenessTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> EveryVariation()
        => SkillCatalogue.All.SelectMany(d => d.Variations.Select(v => new object[] { d.Id, v.Name }));

    /// <summary>
    /// A tree with every style road walked — the probe means to weave any of the twelve.
    /// </summary>
    /// <remarks>
    /// Six skills are taught by the mastery tree (design §5), so a fixture holding a bare tree can
    /// only ever weave the other six and a probe over all twelve would report half of them dormant
    /// for a reason that has nothing to do with what it is measuring.
    /// </remarks>
    internal static MasteryTree EveryRoadWalked()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        // THE ROAD NODES ONLY, without the specialisations that gate them. RestoreTaken does not
        // walk prerequisites, which is what makes that possible — and it has to be done, because a
        // specialisation also grants a trigger and sets an Affinity, and taking all six turned the
        // fixture into a champion with six enchantments. CLUSTER measured as dormant under it: the
        // VOLLEY specialisation's extra target had already covered the reach it gives up.
        tree.RestoreTaken(MasteryCatalog.Nodes
            .Where(x => x.Kind == MasteryKind.SkillRoad)
            .Select(x => x.Id));
        return tree;
    }

    private static Build Build(SkillDef def, string? variation)
    {
        var progress = new SkillProgress();
        if (variation is not null)
        {
            for (var i = 0; i < SkillProgress.UsesForLevel(1); i++) progress.RecordWave(def.Id);
            Assert.True(progress.ChooseVariation(def, variation), $"could not take {variation}");
        }

        // The skill under test, in the slot its kind demands, beside a plain Strike so the champion
        // can still fight when the skill itself deals nothing.
        var picks = new List<BuildComposer.SkillPick>
        {
            new(Source.Body, Form.Strike, null, "filler"),
        };
        if (def.LegacyForm is { } lf)
            picks.Add(new BuildComposer.SkillPick(Source.Body, lf, null, "subject", !def.TakesABeat));
        else
        {
            // Six of the twelve have no legacy Form: they are reached by putting their style's Form in
            // the other slot, which is exactly how a player reaches them.
            var sibling = def.TakesABeat
                ? SkillCatalogue.PassiveOf(def.Style)
                : SkillCatalogue.ActiveOf(def.Style);
            picks.Add(new BuildComposer.SkillPick(Source.Body, sibling.LegacyForm!.Value, null, "subject",
                                                  !def.TakesABeat));
        }

        return BuildComposer.Compose(new MemoryDustTree(), EveryRoadWalked(), character: null,
                                     skills: picks, keystoneIds: Array.Empty<string>(),
                                     slotCapacity: 4, progress: progress);
    }

    private static (long Dealt, int Kept) Fight(Build build)
    {
        // A SHORT RUN, not one wave. A single wave cannot exercise the whole set: an execute threshold
        // needs a cast that finds something already wounded, a bleed-on-kill needs a kill, and a
        // reach variation needs more creatures than the base skill already covers. Six waves of a
        // real expedition give all three, and it is also the shape a player meets.
        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool * 40, Health = pool * 40 };   // survives, so the run is the subject
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
        return (dealt, champ.Health);
    }

    [Theory]
    [MemberData(nameof(EveryVariation))]
    public void test_taking_a_variation_changes_the_fight(string skillId, string variationName)
    {
        var def = SkillCatalogue.ById(skillId);
        var plain = Fight(Build(def, null));
        var varied = Fight(Build(def, variationName));

        _out.WriteLine($"{def.Name} / {variationName}: dealt {plain.Dealt} -> {varied.Dealt}, kept {plain.Kept} -> {varied.Kept}");

        Assert.True(plain.Dealt != varied.Dealt || plain.Kept != varied.Kept,
            $"{def.Name} / {variationName} changed NOTHING — the same damage dealt and the same health " +
            "kept. The catalogue declares a delta the fight does not read, which is a choice the player " +
            "can buy and cannot feel.");
    }
}
