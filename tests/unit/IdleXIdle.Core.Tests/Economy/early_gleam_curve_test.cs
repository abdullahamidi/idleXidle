using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// THE OPENING PAYS LESS AND THE DEEP GAME PAYS THE SAME — measured on the shipped descent.
/// </summary>
/// <remarks>
/// <para>
/// The 2026-09-08 playtest verdict was that the first hour hands out slightly too much Gleam while
/// everything deeper feels right. The reward line was re-pitched rather than scaled:
/// <see cref="ExpeditionTuning.HaulScaleBase"/> (an implicit 1 until then) drops to 0.60 and
/// <see cref="ExpeditionTuning.HaulScaleSlope"/> rises 0.35 -> 0.36, so ONE expression still pays every
/// wave and the opening starts lean and catches up. Nothing branches on a milestone.
/// </para>
/// <para>
/// The intercept is the only early-weighted term in the whole economy: the wave haul is the sole
/// depth-scaling faucet (the offline hunt is the same descent at a haircut, the Warren is a conquest-band
/// constant, and loot Gleam is flat sell value), and its depth term is affine, so the intercept's share
/// of a run decays on its own — about a third of waves 1-10 and a fortieth by wave 100.
/// </para>
/// <para>
/// Both curves run through the SAME production descent here, the old one being the same tuning with the
/// old two constants, so the table measures the game rather than the formula. The probe is trained the
/// way <c>gleam_economy_test</c>'s own income harness is, because an untrained one-skill champion dies
/// around wave 7 and a bench that cannot reach the checkpoint would report a fault in a curve that is
/// fine. Convergence past the depth the probe survives is stated where it is decided — on the curve.
/// </para>
/// </remarks>
public class early_gleam_curve_test
{
    private readonly ITestOutputHelper _out;
    public early_gleam_curve_test(ITestOutputHelper o) => _out = o;

    /// <summary>The shipped tuning — the curve as it now pays.</summary>
    private static readonly ExpeditionTuning Now = ExpeditionTuning.Default;

    /// <summary>The same descent under the curve as it paid before this pass.</summary>
    private static readonly ExpeditionTuning Was = ExpeditionTuning.Default with { HaulScaleBase = 1f, HaulScaleSlope = 0.35f };

    /// <summary>Wave checkpoints, in the order a first session meets them.</summary>
    private static readonly (string Name, int Wave)[] Checkpoints =
    {
        ("the opening loop", 5),
        ("first build decisions", 10),
        ("first conquest", IdleXIdle.Core.Encounters.Checkpoints.ConquestWave),   // 20
        ("early-mid", 40),
    };

    /// <summary>A full four-skill weave — a champion at the depth these checkpoints describe has one.</summary>
    private static Build StarterBuild()
    {
        var b = new Build();
        foreach (var id in new[] { "hammer_blow", "volley_spray", "snare_jaws", "field_mire" })
            b.Equip(TestBuilds.Skill(id));
        return b;
    }

    /// <summary>
    /// A champion that survives to the deepest checkpoint. Trained the way the income harness trains
    /// its own probe, and further: at 120 ranks and one skill the run ends at wave 12, which measures
    /// the fixture's reach rather than the curve.
    /// </summary>
    private static Hunter TrainedProbe()
    {
        var hunter = new Hunter();
        hunter.AddGleam(100_000_000);
        var order = new[] { HunterStat.AttackPower, HunterStat.Vitality, HunterStat.CriticalChance,
                            HunterStat.Defense, HunterStat.ResonanceAffinity, HunterStat.MaxHealth };
        for (var i = 0; i < 300; i++) hunter.Train(order[i % order.Length]);
        return hunter;
    }

    /// <summary>Cumulative Gleam the descent has PAID by the end of each checkpoint wave.</summary>
    private static IReadOnlyDictionary<int, long> Earned(ExpeditionTuning tuning)
    {
        var hunter = TrainedProbe();
        var build = StarterBuild();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 9f, tuning, new Random(17));

        var deepest = Checkpoints.Max(c => c.Wave);
        var at = new Dictionary<int, long>();
        long gleam = 0;
        while (!run.Over && run.Wave <= deepest)
        {
            var wave = run.Wave;
            run.PushWave();
            gleam += run.LastWaveHaul.Gleam;
            foreach (var (_, checkpoint) in Checkpoints)
                if (wave == checkpoint) at[checkpoint] = gleam;
        }
        foreach (var (_, checkpoint) in Checkpoints)
            Assert.True(at.ContainsKey(checkpoint),
                        $"the probe never reached wave {checkpoint} (it ended at {run.Wave}) — the bench cannot measure what it was built to measure");
        return at;
    }

    /// <summary>How many training ranks a wallet buys from rank zero up — the "can I spend it yet?" reading.</summary>
    private static int RanksAffordable(long wallet)
    {
        var tuning = new ProgressionTuning();
        long spent = 0;
        var ranks = 0;
        while (ranks < 400 && spent + Hunter.CostOfRank(ranks, tuning) <= wallet)
        {
            spent += Hunter.CostOfRank(ranks, tuning);
            ranks++;
        }
        return ranks;
    }

    [Fact]
    public void test_the_opening_is_leaner_and_the_curve_converges()
    {
        var was = Earned(Was);
        var now = Earned(Now);

        _out.WriteLine("CUMULATIVE GLEAM EARNED by the end of each checkpoint wave — one descent, the shipped");
        _out.WriteLine("simulation, the only difference being the two curve constants.");
        _out.WriteLine("");
        _out.WriteLine($"{"checkpoint",-24} {"wave",5} {"BEFORE",10} {"AFTER",10} {"DELTA",8}   {"ranks it buys",16}");
        _out.WriteLine(new string('-', 84));

        var deltas = new List<(int Wave, double Pct)>();
        foreach (var (name, wave) in Checkpoints)
        {
            var before = was[wave];
            var after = now[wave];
            var pct = 100.0 * (after / (double)before - 1.0);
            deltas.Add((wave, pct));
            _out.WriteLine($"{name,-24} {wave,5} {before,10:N0} {after,10:N0} {pct,7:+0.0;-0.0;0.0}%   "
                           + $"{RanksAffordable(before),6} -> {RanksAffordable(after),-6}");
        }

        double At(int wave) => deltas.Single(d => d.Wave == wave).Pct;

        // ── THE SHAPE THE PLAYTEST ASKED FOR ──
        Assert.InRange(At(5), -26.0, -13.0);      // the opening loop: about a fifth leaner
        Assert.InRange(At(10), -20.0, -8.0);      // still noticeably lower while the first build is chosen
        Assert.InRange(At(IdleXIdle.Core.Encounters.Checkpoints.ConquestWave), -12.0, -2.0);   // narrowing by the first conquest
        Assert.InRange(At(40), -8.0, 0.0);        // early-mid is nearly whole again

        // NARROWING, not a step: every checkpoint sits closer to parity than the one before it.
        for (var i = 1; i < deltas.Count; i++)
            Assert.True(deltas[i].Pct > deltas[i - 1].Pct - 0.001,
                        $"wave {deltas[i].Wave} ({deltas[i].Pct:+0.0;-0.0}%) is further from the old curve than wave {deltas[i - 1].Wave} ({deltas[i - 1].Pct:+0.0;-0.0}%) — it is not converging");
    }

    [Fact]
    public void test_the_deep_curve_is_the_curve_it_always_was()
    {
        // PAST THE PROBE'S REACH the question is the curve's, not the simulation's: a wave's payout is
        // linear in HaulScale, so the ratio of the two lines IS the ratio of the two payouts. Stated
        // where it is decided rather than by sending a champion somewhere it cannot survive.
        _out.WriteLine($"{"wave",6} {"per-wave payout vs the old curve",34}");
        foreach (var wave in new[] { 0, 5, 20, 40, 80, 160, 400 })
        {
            var ratio = WaveScaling.HaulScale(wave, Now) / WaveScaling.HaulScale(wave, Was);
            _out.WriteLine($"{wave,6} {ratio,33:P1}");
            if (wave >= 80) Assert.InRange(ratio, 0.97, 1.0001);   // converging on parity, never past it
        }
        // Cumulative, which is what a wallet holds, converges on the same place.
        static double Cumulative(ExpeditionTuning t, int through)
            => Enumerable.Range(0, through + 1).Sum(w => (double)WaveScaling.HaulScale(w, t));
        Assert.InRange(Cumulative(Now, 80) / Cumulative(Was, 80), 0.96, 1.0001);
        Assert.InRange(Cumulative(Now, 400) / Cumulative(Was, 400), 0.99, 1.0001);
    }

    [Fact]
    public void test_the_leaner_opening_is_not_an_early_wall()
    {
        // THE COUNTERWEIGHT. Leaner must mean "think about what you buy", never "you cannot interact".
        // Measured at the FIRST checkpoint, where the cut is deepest. gleam_economy_test holds the other
        // half of this promise: the first rank inside ninety seconds of a fresh champion's first fight.
        var now = Earned(Now);
        var wallet = now[5];
        var ranks = RanksAffordable(wallet);
        var first = Hunter.CostOfRank(0, new ProgressionTuning());

        _out.WriteLine($"by the end of wave 5 the descent has paid {wallet:N0} Gleam — {ranks} training rank(s), the first costing {first}");

        Assert.True(wallet >= first, $"wave 5 has paid {wallet}, and one training rank costs {first} — that is a wall");

        // THE REAL READING: the leaner curve must not cost the player a PURCHASE at any checkpoint. One
        // rank behind at a checkpoint is a decision arriving a wave later; two would be a wall.
        var was = Earned(Was);
        foreach (var (name, wave) in Checkpoints)
        {
            var before = RanksAffordable(was[wave]);
            var after = RanksAffordable(now[wave]);
            Assert.True(after >= before - 1,
                        $"{name} (wave {wave}): the wallet bought {before} rank(s) before and {after} now — the opening stopped being a decision");
        }
    }

    [Fact]
    public void test_the_curve_is_the_only_thing_that_moved()
    {
        // The two constants live on one expression and it is the wave payout's alone.
        Assert.Equal(0.52f, Now.HaulScaleBase);
        Assert.Equal(Was.HaulScaleSlope, Now.HaulScaleSlope);   // the slope did NOT move
        Assert.Equal(3f, SoloExpedition.GleamPerWaveCoefficient);
        Assert.Equal(Was.WaveHaulScale, Now.WaveHaulScale);
        Assert.Equal(Was.EnemyScaleBase, Now.EnemyScaleBase);
        Assert.Equal(Was.EnemyDamageScaleBase, Now.EnemyDamageScaleBase);
        Assert.Equal(Was.WaveLengthScale, Now.WaveLengthScale);
        Assert.Equal(Was.BossHealthScale, Now.BossHealthScale);
    }
}
