using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// What an IDLE champion conquers while the player does nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, verbatim: <i>"itemsiz bir şekilde 4. mapi tamamladım hiçbir şey yapmadan, kapatmasam oyunu
/// bitirecektim herhalde"</i> — cleared the fourth map with no items, doing nothing, and would have
/// finished the game had they not closed it.
/// </para>
/// <para>
/// <b>This flatly contradicts <see cref="PacingTest"/>, which measures a fresh champion dying at wave
/// 8 against a conquest bar of 7.</b> Both can be true, and the reason they are is the thing this file
/// exists to measure: PacingTest scores ONE run. The live game scores the BEST RUN EVER. The champion
/// auto-restarts on death at no cost, conquest fires on <c>Deepest &gt;= ConquerWaveDepth</c>, and so
/// the only question a region really asks is whether the dice can reach seven ONCE, given unlimited
/// attempts. That is not a difficulty curve. It is a waiting time.
/// </para>
/// <para>
/// So this probe models the loop the way it actually runs — repeated runs, best-ever kept, no gear
/// bought, no stats trained, no tree spent — and reports how many attempts each region on the real
/// ladder costs a player who is doing nothing.
/// </para>
/// </remarks>
public class AttritionTest
{
    private readonly ITestOutputHelper _out;

    public AttritionTest(ITestOutputHelper output) => _out = output;

    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    /// <summary>The bar Game1 conquers at.</summary>
    private const int ConquerWaveDepth = 7;

    /// <summary>The live ladder from Game1, with no region mastery on top.</summary>
    private static float LadderHealth(int regionIndex) => 110f * RegionLadder.Health(regionIndex);

    private static float LadderDamage(int regionIndex) => 9f * RegionLadder.Damage(regionIndex);

    /// <summary>
    /// A champion with <paramref name="ranks"/> points of training spread the way a player spreads them.
    /// </summary>
    /// <remarks>
    /// The do-nothing model was not enough to reproduce the playtest, so this is the other half of the
    /// question: how much TRAINING does the ladder actually ask for? Spread round-robin across the four
    /// stats a player can see the point of, because a min-maxed spread would measure the best case and
    /// the report is about the ordinary one.
    /// </remarks>
    private static Hunter TrainedHunter(int ranks)
    {
        var h = new Hunter();
        var order = new[] { HunterStat.AttackPower, HunterStat.Vitality,
                            HunterStat.CriticalChance, HunterStat.Defense };
        h.AddGleam(100_000_000);
        for (var i = 0; i < ranks; i++) h.Train(order[i % order.Length]);
        return h;
    }

    private static Build StarterBuild()
    {
        var b = new Build();
        for (var i = 0; i < 4; i++)
            b.Weave(TestBuilds.Skill("hammer_blow"));
        return b;
    }

    /// <summary>
    /// The champion a run actually starts with — minted the way the game mints it.
    /// </summary>
    /// <remarks>
    /// <c>hunter.MaxHealth</c> is not the pool. It is the first term of one, and since 2026-08-28 the
    /// pool also carries the wave-length transform (<see cref="ExpeditionTuning.WaveLengthScale"/>), so
    /// a hand-minted champion fights a wave from a different game — deeper or shallower than anything a
    /// player lives. Every ladder figure in this file is measured off that pool.
    /// </remarks>
    private static Champion FreshChampion(Hunter hunter)
    {
        var pool = SoloBattle.ChampionHealth(StarterBuild(), hunter);
        return new Champion { MaxHealth = pool, Health = pool };
    }

    /// <summary>One run, from full health to death or the cap. Returns the deepest wave it held.</summary>
    private static int OneRun(Random rng, int regionIndex, int ranks = 0)
    {
        var hunter = TrainedHunter(ranks);
        var champ = FreshChampion(hunter);
        var run = new SoloExpedition(StarterBuild(), champ, hunter,
                                     LadderHealth(regionIndex), LadderDamage(regionIndex),
                                     T, rng);

        while (!run.Over && run.Wave < 200)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
        }
        return run.Wave;
    }

    /// <summary>Attempts until best-ever reaches the conquest bar. Null if it never does.</summary>
    private static int? RunsToConquer(int regionIndex, int seed, int giveUpAfter = 3000, int ranks = 0)
    {
        var rng = new Random(seed);
        var best = 0;
        for (var attempt = 1; attempt <= giveUpAfter; attempt++)
        {
            best = Math.Max(best, OneRun(rng, regionIndex, ranks));
            if (best >= ConquerWaveDepth) return attempt;
        }
        return null;
    }

    [Fact]
    public void test_the_whole_ladder_is_reported_for_a_player_who_does_nothing()
    {
        // The headline measurement. No gear, no stats, no tree — only time.
        _out.WriteLine("A CHAMPION LEFT ALONE. No items, no spending, only restarts:");
        _out.WriteLine($"{"region",8} {"enemy hp",9} {"runs to conquer",17} {"median depth",13}");

        for (var region = 0; region < 6; region++)
        {
            var runs = Enumerable.Range(0, 12)
                                 .Select(s => RunsToConquer(region, 1000 + s))
                                 .ToList();

            var rng = new Random(77);
            var depths = Enumerable.Range(0, 40).Select(_ => OneRun(rng, region)).OrderBy(d => d).ToList();
            var median = depths[depths.Count / 2];

            var solved = runs.Where(r => r is not null).Select(r => r!.Value).ToList();
            var text = solved.Count == 0
                ? "never"
                : $"{solved.Average(),6:0.0} (worst {solved.Max()})"
                  + (solved.Count < runs.Count ? $"  [{runs.Count - solved.Count}/12 never]" : "");

            _out.WriteLine($"{region,8} {LadderHealth(region),9:0} {text,17} {median,13}");
        }
    }

    [Fact]
    public void test_doing_nothing_does_not_conquer_the_world()
    {
        // THE ASSERTION THE PLAYTEST ASKED FOR. A region deep in the ladder must not fall to a player who
        // never equips anything and never spends anything. If it does, every system in the game — gear,
        // stats, the trees, the Forge — is decoration, because the only input that mattered was time.
        //
        // The FIRST region is deliberately exempt: it is allowed to fall to persistence, because that is
        // how a new player learns the loop exists. It is the LATER ones that have to ask for something.
        var lateRegion = 3;      // the fourth map — the one the playtest cleared with nothing

        var attempts = Enumerable.Range(0, 8)
                                 .Select(s => RunsToConquer(lateRegion, 2000 + s, giveUpAfter: 400))
                                 .ToList();
        var fell = attempts.Count(a => a is not null);

        _out.WriteLine($"the fourth map fell to a do-nothing champion in {fell} of {attempts.Count} "
                       + "attempts of 400 restarts each");
        foreach (var a in attempts) _out.WriteLine($"   {(a is null ? "held" : $"fell after {a} runs")}");

        Assert.True(fell == 0,
            $"the fourth map fell to a champion with no gear, no stats and no tree in {fell} of "
            + $"{attempts.Count} trials. Conquest reads the BEST RUN EVER and death costs nothing, so "
            + "the region asks only for patience — exactly what the playtest reported.");
    }

    [Fact]
    public void test_how_much_training_each_region_on_the_ladder_asks_for()
    {
        // THE MEASUREMENT THAT MATTERS, and the one the do-nothing model could not give. The playtest
        // champion had no gear but had been played — so the real question is not "does nothing win", it
        // is "how steep is the ask". Read down a column: if a region falls at the same training as the
        // one before it, the ladder is flat there and travelling is free.
        _out.WriteLine("RANKS OF TRAINING NEEDED TO CONQUER (dash = held at 200 restarts):");
        _out.WriteLine($"{"ranks",6} {"gleam",10}  " + string.Join(" ", Enumerable.Range(0, 6).Select(r => $"R{r,-4}")));

        foreach (var ranks in new[] { 0, 40, 80, 200, 240 })
        {
            var cost = 0L;
            var h = new Hunter();
            h.AddGleam(100_000_000);
            var order = new[] { HunterStat.AttackPower, HunterStat.Vitality,
                                HunterStat.CriticalChance, HunterStat.Defense };
            var before = h.Gleam;
            for (var i = 0; i < ranks; i++) h.Train(order[i % order.Length]);
            cost = before - h.Gleam;

            var cells = Enumerable.Range(0, 6).Select(region =>
            {
                var hit = Enumerable.Range(0, 4)
                                    .Select(s => RunsToConquer(region, 3000 + s, giveUpAfter: 200, ranks: ranks))
                                    .Count(r => r is not null);
                return hit == 4 ? "yes  " : hit == 0 ? "-    " : $"{hit}/4  ";
            });

            _out.WriteLine($"{ranks,6} {cost,10:N0}  " + string.Join(" ", cells));
        }
    }

    [Fact]
    public void test_the_ladder_is_priced_against_what_a_region_pays_to_climb_it()
    {
        // THE ROOT OF IT. A region's difficulty step is only real if the power needed to take it costs
        // more than the previous region hands you on the way. If a region pays 10,000 Gleam and the next
        // one asks for 500 Gleam of training, the ladder is decorative — you are always already ready,
        // which is precisely the "I cleared the fourth map doing nothing" report.
        //
        // Reported as a ratio, because the absolute numbers are meaningless without each other.
        _out.WriteLine($"{"region",7} {"gleam earned conquering it",27} {"training to be ready for next",30} {"ratio",8}");

        var tuning = new ProgressionTuning();
        long CostOfRanks(int ranks)
        {
            long total = 0;
            var perStat = new int[4];
            for (var i = 0; i < ranks; i++) total += Hunter.CostOfRank(perStat[i % 4]++, tuning);
            return total;
        }

        // Ranks each region needs, read off the sweep above.
        var needed = new[] { 0, 20, 80, 80, 160, 160 };

        for (var region = 0; region < 5; region++)
        {
            var rng = new Random(500 + region);
            long earned = 0;
            // What conquering this region actually pays: the runs it takes, at the training it takes.
            var best = 0;
            for (var attempt = 1; attempt <= 200 && best < ConquerWaveDepth; attempt++)
            {
                var hunter = TrainedHunter(needed[region]);
                var champ = FreshChampion(hunter);
                var run = new SoloExpedition(StarterBuild(), champ, hunter,
                                             LadderHealth(region), LadderDamage(region),
                                             T, rng);
                while (!run.Over && run.Wave < 200)
                {
                    var before = run.Wave;
                    run.PushWave();
                    if (run.Wave == before) break;
                    earned += run.LastWaveHaul.Gleam;
                }
                best = Math.Max(best, run.Wave);
            }

            var step = CostOfRanks(needed[region + 1]) - CostOfRanks(needed[region]);
            var ratio = step == 0 ? float.PositiveInfinity : earned / (float)step;
            _out.WriteLine($"{region,7} {earned,27:N0} {step,30:N0} {ratio,8:0.0}x");
        }
    }

    [Fact]
    public void test_the_last_regions_are_a_gear_gate_and_not_a_wall()
    {
        // THE SAFETY CHECK ON THE NEW LADDER. Making the chain geometric put regions 4 and 5 beyond what
        // stat training alone can reach, even at the rank cap — which is the DESIGN ("strong items should
        // not be easy to access"), but only if gear actually closes the gap. A gate nobody can open is
        // not a gate, and this is the one way that change could be quietly catastrophic.
        var geared = new List<(string what, int region, bool took)>();

        foreach (var region in new[] { 4, 5 })
        {
            var hit = Enumerable.Range(0, 4).Count(seed =>
            {
                var rng = new Random(6000 + seed);
                var best = 0;
                for (var attempt = 1; attempt <= 200 && best < ConquerWaveDepth; attempt++)
                {
                    var hunter = TrainedHunter(200);
                    // A full set at a tier a player who has conquered four regions would be holding.
                    // Rolled repeatedly rather than minted directly, so the gear under test is gear the
                    // live loot table can actually produce — a hand-built item would prove nothing about
                    // whether the gate is openable with what the game drops.
                    for (var roll = 0; roll < 20; roll++)
                        foreach (var item in LootSystem.Roll(
                                     new KillContext { PowerTier = 30, LootTiltPercent = 40f },
                                     rng, LootTuning.Default))
                            hunter.Equip(item);

                    var champ = FreshChampion(hunter);
                    var run = new SoloExpedition(StarterBuild(), champ, hunter,
                                                 LadderHealth(region), LadderDamage(region),
                                                 T, rng);
                    while (!run.Over && run.Wave < 200)
                    {
                        var before = run.Wave;
                        run.PushWave();
                        if (run.Wave == before) break;
                    }
                    best = Math.Max(best, run.Wave);
                }
                return best >= ConquerWaveDepth;
            });

            _out.WriteLine($"   region {region}: a trained AND geared champion took it in {hit} of 4 trials");
            geared.Add(($"region {region}", region, hit > 0));
        }

        foreach (var (what, _, took) in geared)
            Assert.True(took,
                $"{what} did not fall to a champion at 200 ranks of training wearing a full Epic set. "
                + "The geometric ladder has made the back half of the world unreachable rather than "
                + "expensive — that is a wall, not a gear gate.");
    }

    [Fact]
    public void test_every_step_on_the_ladder_asks_for_more_than_the_last()
    {
        // The regression guard on the thing that actually broke. It is not enough that the world gets
        // harder — the STEPS have to stay the same size, because player power multiplies. A linear ladder
        // technically rises while its felt steps shrink (+35%, +26%, +21%, +17%), and that shrinking is
        // what handed the playtest four free regions.
        var steps = new List<float>();
        for (var i = 1; i < 6; i++)
            steps.Add(RegionLadder.Health(i) / RegionLadder.Health(i - 1));

        for (var i = 0; i < steps.Count; i++)
            _out.WriteLine($"   region {i} -> {i + 1}:  +{(steps[i] - 1f) * 100f:0}% enemy health");

        foreach (var step in steps)
            Assert.Equal(steps[0], step, 3);

        Assert.True(steps[0] > 1.2f,
            $"each region is only {(steps[0] - 1f) * 100f:0}% tougher than the last — the world barely "
            + "rises, and accumulated power will outrun it.");
    }
}
