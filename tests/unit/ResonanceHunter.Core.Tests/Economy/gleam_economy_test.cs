using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Warrens;
using ResonanceHunter.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// What Gleam is worth: the faucet measured against the sink, in minutes of play.
/// </summary>
/// <remarks>
/// <para>
/// Playtest: <i>"Çok fazla gold geliyor."</i> — far too much gold is coming in. It was correct by a
/// wide margin, and the reason it shipped is instructive: a test already existed asserting the sink was
/// "substantial", and it asserted <c>total &gt; 50_000</c> with no reference to income at all. 79,578
/// passed green while the faucet ran at 252,974 an hour. <b>A one-sided assertion on an economy proves
/// nothing</b>, because an economy is a ratio.
/// </para>
/// <para>
/// So everything here is stated as PLAY TIME. Not "the sink is big" and not "the rate is small" — how
/// many minutes of play the whole progression takes, which is the only form in which either number
/// means anything.
/// </para>
/// </remarks>
public class GleamEconomyTest
{
    private readonly ITestOutputHelper _out;

    public GleamEconomyTest(ITestOutputHelper output) => _out = output;

    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    /// <summary>Every Gleam a player can ever spend on training, from rank 0 to the cap on all stats.</summary>
    private static long LifetimeTrainingSink()
    {
        var tuning = new ProgressionTuning();
        long total = 0;
        foreach (var _ in Enum.GetValues<HunterStat>())
            for (var rank = 0; rank < tuning.StatRankCap; rank++)
                total += Hunter.CostOfRank(rank, tuning);
        return total;
    }

    /// <summary>The Warren's Gleam, at the level a player has it on the day it unlocks.</summary>
    private static long WarrenGleamPerMinute(int conquered = 1)
    {
        var w = new Warren { ConqueredRegions = conquered };
        return w.ProductionPerMinute(WarrenResource.Gleam);
    }

    private static Build StarterBuild()
    {
        var b = new Build();
        b.Weave(new EquippedSkill(
            new WovenAbility { Name = "S", Source = Source.Body, Form = Form.Strike },
            FormBehaviour.BaseCooldownMs(Form.Strike)));
        return b;
    }

    /// <summary>
    /// The champion's Gleam per minute of FIGHT TIME at a given depth, measured from the real run.
    /// </summary>
    /// <remarks>
    /// Measured, not derived from the formula: the rate that matters is Gleam per SECOND, and that
    /// depends on how fast waves actually die, which only the simulation knows.
    /// </remarks>
    private static float ChampionGleamPerMinute(int stopAtWave, int ranks = 0)
    {
        var hunter = new Hunter();
        hunter.AddGleam(100_000_000);
        var order = new[] { HunterStat.AttackPower, HunterStat.Vitality,
                            HunterStat.CriticalChance, HunterStat.Defense };
        for (var i = 0; i < ranks; i++) hunter.Train(order[i % order.Length]);

        var champ = new Champion { MaxHealth = hunter.MaxHealth, Health = hunter.MaxHealth };
        var run = new SoloExpedition(StarterBuild(), champ, hunter, 110f, 9f, T, Source.Nature,
                                     new Random(17));

        long gleam = 0;
        long ms = 0;
        while (!run.Over && run.Wave < stopAtWave)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
            gleam += run.LastWaveHaul.Gleam;
            ms += run.LastWaveEvents.Count == 0 ? 0 : run.LastWaveEvents.Max(e => e.AtMs);
        }

        return ms == 0 ? 0f : gleam / (ms / 60000f);
    }

    [Fact]
    public void test_the_whole_gleam_economy_is_reported_as_play_time()
    {
        var sink = LifetimeTrainingSink();

        _out.WriteLine($"LIFETIME TRAINING SINK: {sink:N0} Gleam "
                       + $"({Enum.GetValues<HunterStat>().Length} stats x {new ProgressionTuning().StatRankCap} ranks)");
        _out.WriteLine("");
        _out.WriteLine($"{"faucet",-34} {"gleam/min",12} {"hours to fund the whole sink",30}");

        var warren0 = WarrenGleamPerMinute(conquered: 0);
        var warren1 = WarrenGleamPerMinute(conquered: 1);
        var champShallow = ChampionGleamPerMinute(stopAtWave: 8);
        var champDeep = ChampionGleamPerMinute(stopAtWave: 40, ranks: 120);

        void Row(string name, double perMin)
            => _out.WriteLine($"{name,-34} {perMin,12:N0} {(perMin <= 0 ? 0 : sink / perMin / 60),30:N1}");

        Row("Warren, level 1, 0 conquests", warren0);
        Row("Warren, level 1, 1 conquest", warren1);
        Row("champion, fresh, waves 1-8", champShallow);
        Row("champion, trained, waves 1-40", champDeep);
        Row("TOTAL (warren@1 + trained champion)", warren1 + champDeep);

        _out.WriteLine("");
        _out.WriteLine("The ladder measured in attrition_test, priced in Gleam:");
        foreach (var (region, ranks) in new[] { (1, 40), (2, 80), (3, 200) })
        {
            var tuning = new ProgressionTuning();
            long cost = 0;
            var per = new int[4];
            for (var i = 0; i < ranks; i++) cost += Hunter.CostOfRank(per[i % 4]++, tuning);
            var mins = cost / Math.Max(1.0, warren1 + champDeep);
            _out.WriteLine($"   region {region}: {ranks,3} ranks = {cost,8:N0} Gleam = {mins,7:N1} minutes");
        }
    }

    [Fact]
    public void test_a_new_player_earns_nothing_from_a_screen_they_cannot_open()
    {
        // The Warren's nav tile is gated on the first conquest, but its PRODUCTION was not — so a brand
        // new player was paid ~1,800 Gleam a minute by a building they had never been told existed.
        // That is the inverse of this codebase's usual bug: not a feature that never runs, but one that
        // runs before the player has met it.
        //
        // Asserted against the gate itself rather than a number, so retuning the Warren cannot quietly
        // reopen the hole.
        var facts = new UnlockFacts();       // a brand-new save
        Assert.False(Unlocks.IsOpen(Activity.Warren, facts),
            "the Warren is open to a new player, so gating its production would change nothing.");
    }

    [Fact]
    public void test_the_whole_progression_is_not_over_inside_an_hour()
    {
        // THE ASSERTION THE OLD TEST SHOULD HAVE MADE. It checked `sink > 50_000` and passed green while
        // income ran at 252,974/hour — the entire nine-stat progression affordable in 18.9 minutes.
        //
        // Stated as play time, both sides named, so it can never pass by growing the sink alone.
        var sink = LifetimeTrainingSink();
        var income = WarrenGleamPerMinute(conquered: 1) + ChampionGleamPerMinute(stopAtWave: 40, ranks: 120);
        var hours = sink / income / 60.0;

        _out.WriteLine($"the full stat progression funds itself in {hours:N1} hours of play "
                       + $"({income:N0} Gleam/min against a {sink:N0} sink)");

        Assert.True(hours >= 5,
            $"the entire nine-stat progression is affordable in {hours * 60:N0} minutes. Gleam is not a "
            + "currency, it is a formality — every training decision is 'yes' and the stat screen is a "
            + "list of buttons to press in any order.");

        // And the other end: a progression nobody can finish is not a progression either.
        Assert.True(hours <= 120,
            $"the full progression needs {hours:N0} hours. That is a grind, not a curve.");
    }

    [Fact]
    public void test_the_first_rank_is_affordable_almost_immediately()
    {
        // The counterweight to every cut above. A new player must be able to spend something within the
        // first minute, or the first thing the game teaches is that its currency does not do anything
        // yet — and the tutorial's SPEND GLEAM step would sit unsatisfiable while it waited.
        var firstRank = Hunter.CostOfRank(0, new ProgressionTuning());
        var champShallow = ChampionGleamPerMinute(stopAtWave: 8);

        var seconds = firstRank / Math.Max(0.001, champShallow / 60.0);
        _out.WriteLine($"a fresh champion affords its first rank ({firstRank} Gleam) after {seconds:N0}s");

        Assert.True(seconds <= 90,
            $"a new player waits {seconds:N0}s before they can buy anything at all.");
    }
}
