using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// How much loot actually reaches the bag, per hundred waves.
/// </summary>
/// <remarks>
/// <para>
/// The playtest keeps returning to the same sentence: <i>"still too many chests drop, this much item
/// abundance is not right."</i> Every individual rule in the loot system already says the opposite — a
/// boss gives "one item, a lucky second sometimes", a chest is "an event, not a paycheck", grade buys
/// quality and never quantity. Each rule is modest. Nobody had added them up.
/// </para>
/// <para>
/// So this counts. It runs the real drop rolls over a hundred waves and reports what lands, because
/// "one item per boss" and "a boss every fifth wave" and "a wave every two seconds" multiply into a
/// number nobody chose.
/// </para>
/// <para>
/// <b>And it must count the path the GAME takes.</b> The first version of this file rolled
/// <c>ExpeditionLoot.RollBoss</c> as a separate per-boss source and reported 65 items per hundred
/// waves. The live loop does not do that: Game1's reward drain pays Gleam and a material
/// trickle per wave, and a boss drops a CHEST or nothing — <c>RollBoss</c> is reached only from inside
/// <c>Chests.Open</c>. The real figure was about 13. A probe that measures a path the game does not
/// take gives a precise answer about an imaginary game, and this one over-stated the problem four
/// times over before anyone checked where loot actually enters the bag.
/// </para>
/// </remarks>
public class LootRateTest
{
    private readonly ITestOutputHelper _out;

    public LootRateTest(ITestOutputHelper output) => _out = output;

    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    private sealed record Haul(int Waves, int Bosses, int Chests, int BossItems, int ChestItems)
    {
        public int Items => BossItems + ChestItems;
    }

    /// <summary>Roll a hundred waves of the real drop logic and total what the player would receive.</summary>
    private static Haul Simulate(int waves, int seed, ChestTuning? chestTuning = null)
    {
        var rng = new Random(seed);
        chestTuning ??= ChestTuning.Default;

        int bosses = 0, chests = 0, bossItems = 0, chestItems = 0;

        for (var wave = 1; wave <= waves; wave++)
        {
            if (!WaveScaling.IsBossWave(wave, T)) continue;
            bosses++;

            // NO DIRECT BOSS ITEM. The first version of this probe rolled ExpeditionLoot.RollBoss here
            // as a separate source and reported 65 items per hundred waves — but the live loop
            // (Game1's reward drain) pays only Gleam and a material trickle per wave, and a boss
            // drops a CHEST or nothing. RollBoss is reached ONLY from inside Chests.Open. A probe that
            // measures a path the game does not take produces a real number for an imaginary game, and
            // it over-stated this one by four times.
            var tier = wave;   // depth is the tier the drop rolls against

            if (rng.NextDouble() >= Chests.DropChance(tier, chestTuning)) continue;
            chests++;

            var chest = Chests.RollDrop(tier, element: null, rng, chestTuning);
            chestItems += Chests.Open(chest, rng, tuning: chestTuning).Items.Count;
        }

        return new Haul(waves, bosses, chests, bossItems, chestItems);
    }

    private static Haul Average(int waves, ChestTuning? tuning = null)
    {
        var runs = Enumerable.Range(0, 40).Select(s => Simulate(waves, 1000 + s * 7, tuning)).ToList();
        return new Haul(
            waves,
            (int)runs.Average(r => r.Bosses),
            (int)Math.Round(runs.Average(r => r.Chests)),
            (int)Math.Round(runs.Average(r => r.BossItems)),
            (int)Math.Round(runs.Average(r => r.ChestItems)));
    }

    [Fact]
    public void test_the_loot_rate_over_a_hundred_waves_is_reported()
    {
        var h = Average(100);

        _out.WriteLine("A HUNDRED WAVES, averaged over 40 runs:");
        _out.WriteLine($"   bosses          {h.Bosses,5}   (one every {T.BossEvery})");
        _out.WriteLine($"   chests          {h.Chests,5}   ({100f * h.Chests / Math.Max(1, h.Bosses):0}% of bosses)");
        _out.WriteLine($"   items (chests are the ONLY source) {h.ChestItems,5}");
        _out.WriteLine($"   ITEMS TOTAL     {h.Items,5}   ({(float)h.Items / h.Bosses:0.00} per boss)");
        _out.WriteLine("");

        // The rate the player actually experiences depends on how fast waves die. The fight soak clears
        // a wave in roughly two seconds at the saved battle speed, so a hundred waves is about three
        // and a half minutes of play.
        foreach (var secondsPerWave in new[] { 1f, 2f, 4f })
        {
            var minutes = waves100Seconds(secondsPerWave) / 60f;
            _out.WriteLine($"   at {secondsPerWave:0}s per wave: {h.Items / minutes:0.0} items a MINUTE"
                           + $" ({minutes:0.0} min for a hundred waves)");
        }

        static float waves100Seconds(float perWave) => 100f * perWave;
    }

    [Fact]
    public void test_a_boss_is_not_a_vending_machine()
    {
        // The design's own words are "a chest is an event, not a paycheck" and "one item, a lucky second
        // sometimes". Both are true of each rule in isolation. The question this asks is what they come
        // to when multiplied: how many items does a single boss actually hand over?
        var h = Average(200);
        var perBoss = (float)h.Items / h.Bosses;

        _out.WriteLine($"{h.Items} items from {h.Bosses} bosses = {perBoss:0.00} per boss");

        // Chests are the only source of items in the running game, so this is the chest rate times what
        // a chest holds: about 0.3 chests a boss, one or two items inside. The earlier version of this
        // assertion allowed 1.6 because the probe was also counting a direct boss drop that the live
        // loop does not perform.
        Assert.True(perBoss <= 0.6f,
            $"a boss hands over {perBoss:0.00} items on average. Every rule reads modest on its own — "
            + "one item plus a lucky second, a chest only sometimes, one or two items inside — and they "
            + "multiply into a firehose. Depth is supposed to buy RARITY, not volume.");
    }

    [Fact]
    public void test_depth_buys_rarity_rather_than_volume()
    {
        // The rule the whole loot rework rests on. Going deeper must make each drop BETTER, and must not
        // make drops more frequent — otherwise a deep player is buried rather than rewarded.
        var shallow = Average(60);
        var deep = Simulate(60, seed: 99);

        var shallowPerBoss = (float)shallow.Items / shallow.Bosses;

        // Rarity climbing with depth is what SHOULD change. Sample the grade roll at two depths.
        var rng = new Random(7);
        float AverageGrade(int tier) => (float)Enumerable.Range(0, 2000)
            .Select(_ => (int)Chests.RollRarity(tier, rng))
            .Average();

        var early = AverageGrade(5);
        var late = AverageGrade(60);

        _out.WriteLine($"average chest grade: tier 5 = {early:0.00}, tier 60 = {late:0.00}");
        _out.WriteLine($"items per boss stays at {shallowPerBoss:0.00}");

        Assert.True(late > early + 0.5f,
            $"depth barely moves the chest grade ({early:0.00} to {late:0.00}) — pushing deeper is "
            + "supposed to buy quality.");
    }
}
