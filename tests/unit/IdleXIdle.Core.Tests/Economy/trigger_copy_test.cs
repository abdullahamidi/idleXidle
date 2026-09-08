using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Sources;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// THE CARD NAMES THE TRIGGER THE SIM FIRES ON — measured, not asserted from a string.
/// </summary>
/// <remarks>
/// <para>
/// SPLINTER and HARVEST both said "ON KILL", which a player reads as "each enemy that dies". Both are
/// paid in <c>SoloBattle</c>'s wave-CLEAR handler: one payment when the last creature falls, whatever
/// the head-count. The mechanics are not moving — the sentence was wrong, and a sentence pinned only to
/// itself would let it come back the moment someone edits it.
/// </para>
/// <para>
/// So each test here MEASURES the trigger first — how many payments a wave of many creatures produces —
/// and only then holds the presented row to naming that trigger. If a future pass genuinely moves either
/// payout to the per-creature branch (where SHADE, the bleed-on-kill variations, HARDFACE and LOOSE
/// AGAIN live), the measurement changes and this test demands the copy change with it.
/// </para>
/// </remarks>
public class trigger_copy_test
{
    private readonly ITestOutputHelper _out;
    public trigger_copy_test(ITestOutputHelper o) => _out = o;

    private const string WaveClear = "ON WAVE CLEAR:";
    private const string PerEnemy = "ON KILL";

    private static ItemInstance Weapon(EnchantKind kind, Rarity rarity = Rarity.Legendary) => new()
    {
        InstanceId = $"trigger_{kind}", BaseType = ItemBaseType.Weapon, Rarity = rarity,
        SellValue = 100, ItemLevel = 10, EnchantOverride = kind,
    };

    private static Build Woven()
    {
        var build = new Build { PassiveMods = BuildMods.None, Shape = SkillShape.None };
        build.Equip(TestBuilds.Skill("hammer_blow", Source.Nature));
        return build;
    }

    /// <summary>Fight one wave of <paramref name="creatures"/> one-health enemies and report what it paid.</summary>
    private static (WaveBonus Bonus, int Killed) OneWave(Hunter hunter, int creatures, int seed)
    {
        var bonus = new WaveBonus();
        var metrics = new WaveMetrics();
        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var wave = Enumerable.Range(0, creatures)
                             .Select(_ => new WaveCreature { MaxHealth = 1f, Health = 1f, Damage = 0f })
                             .ToList();
        var (outcome, _) = SoloBattle.ResolveWave(champ, Woven(), hunter, wave, enemyIntervalMs: 5_000,
                                                  ExpeditionTuning.Default, new Random(seed), bonus, metrics: metrics);
        Assert.Equal(WaveOutcome.Cleared, outcome);
        Assert.Equal(creatures, metrics.CreaturesKilled);
        return (bonus, metrics.CreaturesKilled);
    }

    /// <summary>The sentence a screen actually draws for this enchant — through the presentation seam, not the enum.</summary>
    private static string ShownSentence(ItemInstance item)
    {
        var row = Assert.Single(ItemPresentation.Rows(item).Where(r => r.Kind == ItemRowKind.Enchant));
        Assert.Equal(Enchantments.Of(item)!.Blurb, row.Note);   // the row carries the card's own words
        return row.Note;
    }

    [Fact]
    public void test_splinter_pays_once_a_wave_and_the_card_says_wave_clear()
    {
        var item = Weapon(EnchantKind.Splinter);
        var hunter = new Hunter();
        hunter.Equip(item);
        var magnitude = Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Legendary);

        // MEASURE THE TRIGGER: nine creatures die in one wave and the quality moves exactly once.
        // (Nine payments would be a per-enemy trigger, and then "ON KILL" would have been right.)
        var (bonus, killed) = OneWave(hunter, creatures: 9, seed: 3);
        _out.WriteLine($"SPLINTER — {killed} creatures killed, quality paid {bonus.Quality:0.000}, one payment is {magnitude:0.000}");
        Assert.Equal(magnitude, bonus.Quality, 5);
        Assert.True(killed > 1, "the fixture killed one creature — it cannot tell a per-kill trigger from a per-wave one");

        // ...and one wave is one payment however many creatures stand in it.
        Assert.Equal(bonus.Quality, OneWave(hunter, creatures: 1, seed: 3).Bonus.Quality, 5);

        // SO THE CARD MUST SAY SO.
        var shown = ShownSentence(item);
        Assert.StartsWith(WaveClear, shown, StringComparison.Ordinal);
        Assert.DoesNotContain(PerEnemy, shown, StringComparison.Ordinal);
    }

    [Fact]
    public void test_harvest_rolls_once_a_wave_and_the_card_says_wave_clear()
    {
        var item = Weapon(EnchantKind.Harvest);
        var hunter = new Hunter();
        hunter.Equip(item);

        // HARVEST is a CHANCE, so the trigger is measured over many seeds: a wave of nine deaths may
        // pay a spare core or none, but never TWO — one roll per cleared wave. A per-enemy trigger
        // would hand out several cores in a nine-creature wave almost every time.
        var paid = 0;
        for (var seed = 0; seed < 60; seed++)
        {
            var cores = OneWave(hunter, creatures: 9, seed: seed).Bonus.Cores;
            Assert.InRange(cores, 0, 1);
            if (cores == 1) paid++;
        }
        _out.WriteLine($"HARVEST — {paid} of 60 nine-creature waves paid a spare core, never more than one");
        Assert.True(paid > 0, "no wave in sixty paid — the fixture is not reaching the roll at all");
        Assert.True(paid < 60, "every wave paid — the roll is not a chance any more");

        var shown = ShownSentence(item);
        Assert.StartsWith(WaveClear, shown, StringComparison.Ordinal);
        Assert.DoesNotContain(PerEnemy, shown, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_keystone_that_grants_splinter_makes_the_same_promise_as_the_item()
    {
        // REAPER charges -25% skill rate for BuildTrigger.Splinter, and its card said "EVERY KILL".
        // Same trigger, same payout site, so it owes the player the same words.
        var reaper = Keystones.ById("reaper")!;
        Assert.Contains(BuildTrigger.Splinter, reaper.Grants);
        _out.WriteLine($"REAPER — \"{reaper.Blurb}\"");

        // A WORD, not a substring: "SKILLS" carries the letters and says nothing about a trigger.
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex(@"KILLS?"), reaper.Blurb);
        Assert.Contains("CLEAR", reaper.Blurb, StringComparison.Ordinal);
    }

    [Fact]
    public void test_a_per_enemy_trigger_still_says_on_kill()
    {
        // THE OTHER SIDE OF THE RULE, so this is a vocabulary that distinguishes rather than a
        // find-and-replace: the mechanics that really do fire on each creature's death keep saying so.
        // SHADOW's fourth rung leaves a SHADE per death; THE QUIVER's LOOSE AGAIN readies the rotation
        // on each death — both read in SoloBattle's per-creature branch, beside CreaturesKilled++.
        var quiver = IdleXIdle.Core.Characters.CharacterRoster.All.Single(c => c.Grants.Contains(BuildTrigger.LooseAgain));
        Assert.Contains("kill", quiver.PassiveText, StringComparison.OrdinalIgnoreCase);

        var shade = ElementSets.TiersOf(Source.Shadow).Single(r => r.Line.Contains("SHADE", StringComparison.Ordinal));
        Assert.Contains("kill", shade.Line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_no_enchant_sentence_claims_a_per_enemy_trigger_it_does_not_have()
    {
        // The sweep: every enchant, every live rarity. A sentence may say ON KILL only if its payout is
        // in the per-creature branch — none of the eleven is today, so none may say it. A future
        // per-enemy enchant fails here and the failure names what to go and prove.
        foreach (var kind in Enum.GetValues<EnchantKind>())
        foreach (var rarity in new[] { Rarity.Rare, Rarity.Epic, Rarity.Legendary })
        {
            var blurb = new Enchantment(kind, Enchantments.MagnitudeFor(kind, rarity)).Blurb;
            Assert.DoesNotContain(PerEnemy, blurb, StringComparison.OrdinalIgnoreCase);
        }
    }
}
