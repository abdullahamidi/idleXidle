using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// How many of the champion's own actions fit inside one wave.
/// </summary>
/// <remarks>
/// <para>
/// Playtest 2026-08-28: the fight was over before it started. A wave was 2.6-3.4 seconds — <b>1.9 to 2.4
/// champion actions</b> — beside a 2.05 s transition, so a player watching their own build saw a swing,
/// maybe a second one, and a banner. A Strike waits six actions, so it fired every second or third wave:
/// the loadout the whole game is about was mostly not happening on screen.
/// </para>
/// <para>
/// <see cref="ExpeditionTuning.WaveLengthScale"/> and <see cref="SoloBattle.ChampionPoolScale"/> are the
/// answer, and this file is the band that keeps it. It measures the two things the change is FOR (fight
/// seconds and actions per wave) and the one thing it must not move (income per real minute), and it
/// pins the knob's neutrality at 1 so a future retune cannot quietly leave the wave short again.
/// </para>
/// </remarks>
public class WaveLengthTest
{
    private readonly ITestOutputHelper _out;

    public WaveLengthTest(ITestOutputHelper output) => _out = output;

    /// <summary>The transition the screen holds between waves — SoloExpeditionScreen.WaveBreakSeconds.</summary>
    /// <remarks>
    /// 0.45 + 0.45 + 0.20. Counted because income is per REAL minute and the break is real time: a
    /// figure measured in fight seconds alone reads roughly double the wall clock on short waves, which
    /// is the mistake gleam_economy_test records at length.
    /// </remarks>
    private const long WaveBreakMs = 1_100;

    /// <summary>
    /// The transition BEFORE 2026-08-28: 0.70 + 1.00 + 0.35. The "before" side of any income comparison
    /// has to hold it, because the shorter transition is half of what made the loop faster — measuring
    /// the old game with the new break credits the wave-length change with an income rise it did not
    /// cause, and hides the one it did.
    /// </summary>
    private const long LegacyWaveBreakMs = 2_050;

    /// <summary>
    /// A real four-skill loadout: one of each shape a player actually weaves — the same skills the
    /// Form-era Strike/Projectile/Aura/Mark fixture resolved to, so the bands below still measure
    /// the fight they were calibrated against.
    /// </summary>
    private static Build FourSkill()
    {
        var b = new Build();
        var plan = new (Source Src, string Id)[]
        {
            (Source.Body, "hammer_blow"), (Source.Mind, "volley_spray"),
            (Source.Nature, "field_mire"), (Source.Spirit, "sign_call"),
        };
        foreach (var (src, id) in plan) b.Weave(TestBuilds.Skill(id, src));
        return b;
    }

    private static Hunter Trained(int ranks)
    {
        var h = new Hunter();
        h.AddGleam(1_000_000_000);
        var order = new[] { HunterStat.AttackPower, HunterStat.Vitality,
                            HunterStat.CriticalChance, HunterStat.Defense };
        for (var i = 0; i < ranks; i++) h.Train(order[i % order.Length]);
        return h;
    }

    private readonly record struct Run(int Depth, float FightSecondsPerWave, float ActionsPerWave, float GleamPerMinute,
                                       IReadOnlyList<int> WaveGleam, IReadOnlyList<int> WaveMs, long BreakMs)
    {
        /// <summary>
        /// Gleam per real minute over the FIRST <paramref name="waves"/> waves only.
        /// </summary>
        /// <remarks>
        /// Comparing two runs' lifetime income compares two different NUMBERS OF WAVES, and reward grows
        /// with depth — so a run that happened to get four waves further read as richer per minute when
        /// nothing about its pay rate had changed. That is depth leaking into an income measurement. Held
        /// to the waves both runs actually fought, the comparison is about the RATE and nothing else.
        /// </remarks>
        public float GleamPerMinuteOver(int waves)
        {
            var n = Math.Min(waves, Math.Min(WaveGleam.Count, WaveMs.Count));
            if (n == 0) return 0f;
            long gleam = 0, ms = 0;
            for (var i = 0; i < n; i++) { gleam += WaveGleam[i]; ms += WaveMs[i] + BreakMs; }
            return ms == 0 ? 0f : gleam / (ms / 60000f);
        }
    }

    /// <summary>Play a whole run at a given training level and report what one wave felt like.</summary>
    private static Run Play(int ranks, float waveScale, float poolScale, float haulScale, long breakMs)
    {
        var tuning = ExpeditionTuning.Default with
        {
            WaveLengthScale = waveScale,
            ChampionPoolScale = poolScale,
            WaveHaulScale = haulScale,
        };
        var hunter = Trained(ranks);
        var build = FourSkill();
        var pool = SoloBattle.ChampionHealth(build, hunter, poolScale);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 9f, tuning, new Random(17));
        var beat = SoloBattle.BeatFor(build.Resolve(hunter).SkillRate);

        long fightMs = 0, gleam = 0;
        var perWaveGleam = new List<int>();
        var perWaveMs = new List<int>();
        while (!run.Over && run.Wave < 200)
        {
            var before = run.Wave;
            run.RefreshPool();
            run.PushWave();
            if (run.Wave == before) break;         // stalled: no progress, do not spin
            var ms = run.LastWaveEvents.Count == 0 ? 0 : run.LastWaveEvents.Max(e => e.AtMs);
            perWaveGleam.Add(run.LastWaveHaul.Gleam);
            perWaveMs.Add(ms);
            gleam += run.LastWaveHaul.Gleam;
            fightMs += ms;
        }

        var waves = perWaveMs.Count;
        if (waves == 0) return new Run(run.Wave, 0f, 0f, 0f, perWaveGleam, perWaveMs, breakMs);
        var perWave = fightMs / (float)waves;
        var wallMs = fightMs + waves * breakMs;
        return new Run(run.Wave, perWave / 1000f, perWave / beat,
                       gleam / (wallMs / 60000f), perWaveGleam, perWaveMs, breakMs);
    }

    /// <summary>The game as it ships.</summary>
    private static Run Live(int ranks)
        => Play(ranks, ExpeditionTuning.Default.WaveLengthScale, SoloBattle.ChampionPoolScale,
                ExpeditionTuning.Default.WaveHaulScale, WaveBreakMs);

    /// <summary>The game before 2026-08-28: every scale at 1, and the long transition.</summary>
    private static Run Legacy(int ranks) => Play(ranks, 1f, 1f, 1f, LegacyWaveBreakMs);

    /// <summary>The training levels a run is worth measuring at, from the first session to deep play.</summary>
    private static readonly int[] Ranks = { 0, 20, 60, 120, 200, 300 };

    [Fact]
    public void test_a_wave_holds_enough_actions_for_a_rhythm_skill_to_be_seen()
    {
        // Arrange / Act — the live tuning, across the whole training range.
        var runs = Ranks.ToDictionary(r => r, r => Live(r));

        _out.WriteLine($"{"ranks",6} {"depth",6} {"fight s/wave",13} {"actions/wave",13} {"gleam/min",10}");
        foreach (var (ranks, r) in runs)
            _out.WriteLine($"{ranks,6} {r.Depth,6} {r.FightSecondsPerWave,13:0.00} "
                           + $"{r.ActionsPerWave,13:0.00} {r.GleamPerMinute,10:N0}");

        // Assert. THREE, not four: the target was 4-6 actions and the deep game reaches it (4.1-5.1),
        // but a fresh champion's own damage is what sets its wave and it sits at 3.1. Three is the
        // floor that matters — it is the first number at which a four-action Projectile can land inside
        // a single wave at all, which was the complaint. Measured 3.14 to 5.12 (was 1.85 to 2.42).
        foreach (var (ranks, r) in runs)
            Assert.True(r.ActionsPerWave >= 3f,
                $"at {ranks} ranks a wave is {r.ActionsPerWave:0.0} champion actions ({r.FightSecondsPerWave:0.0}s). "
                + "The fight ends before the loadout does anything — the playtest's whole complaint.");

        // And the other end: a wave that runs long stops being a wave and becomes a wall to watch.
        foreach (var (ranks, r) in runs)
            Assert.True(r.FightSecondsPerWave <= 15f,
                $"at {ranks} ranks a wave takes {r.FightSecondsPerWave:0.0}s of fighting.");
    }

    [Fact]
    public void test_stretching_the_wave_leaves_income_per_real_minute_alone()
    {
        // A wave that lasts 2.5x longer and pays the same would have cut income by 2.5x, so the haul
        // carries the stretch too (SoloExpedition.HaulForWave). Asserted as a RATIO against the
        // pre-stretch game rather than an absolute, because the absolute is gleam_economy_test's band
        // and duplicating it here would be a second copy of a number that drifts.
        // COLLECTED, PRINTED, THEN ASSERTED. Throwing on the first bad row hides every row after it,
        // and on an economy the shape across the range is the finding — one outlier at the shallow end
        // means something different from a drift at every level.
        var offenders = new List<string>();

        foreach (var ranks in Ranks)
        {
            var before = Legacy(ranks);
            var after = Live(ranks);

            // OVER THE WAVES BOTH RUNS FOUGHT. Lifetime income folds in how DEEP each run got, and
            // reward grows with depth, so two runs of different length cannot be compared on pay rate —
            // at 300 ranks that leak alone read as a 36% pay rise while the canonical economy band
            // (gleam_economy_test, which holds its wave count fixed) had not moved at all.
            var common = Math.Min(before.Depth, after.Depth);
            var beforeRate = before.GleamPerMinuteOver(common);
            var afterRate = after.GleamPerMinuteOver(common);
            var ratio = afterRate / MathF.Max(1f, beforeRate);

            // MEASURED, BUT ONLY JUDGED WHERE THERE IS ENOUGH TO JUDGE. A fresh champion dies on wave
            // four, and four waves of a linear-in-depth reward is not an income measurement — the two
            // runs do not even meet the same wave compositions there. Printed at every level so the
            // shallow end stays visible; asserted where the sample is one.
            const int Enough = 8;
            var judged = common >= Enough;
            _out.WriteLine($"{ranks,4} ranks: {beforeRate,7:N0} -> {afterRate,7:N0} "
                           + $"gleam/min over the first {common,3} waves  (x{ratio:0.00})"
                           + (judged ? "" : $"   — under {Enough} waves, reported only"));

            // A TIGHT BAND, because this one is solved for rather than tolerated: ExpeditionTuning
            // .WaveHaulScale is calibrated against exactly this ratio (see its table). Paying the
            // wave's own 2.5 instead landed 1.49 and cut the lifetime grind from 230 hours to 154.
            if (judged && ratio is <= 0.80f or >= 1.25f)
                offenders.Add($"at {ranks} ranks income moved to x{ratio:0.00} over {common} waves");
        }

        Assert.True(offenders.Count == 0,
            string.Join("; ", offenders)
            + ". Longer waves and a shorter transition are meant to change how the loop FEELS; "
            + "WaveHaulScale is the knob that keeps them from changing what it pays.");
    }

    [Fact]
    public void test_the_knob_set_to_one_is_the_game_before_the_change()
    {
        // THE NEUTRALITY TRIPWIRE, and it caught a real defect while it was being written:
        // SoloExpedition.RefreshPool re-minted the pool from the DEFAULT scale instead of the run's own
        // tuning, so a probe sweeping the knob got a champion from one game fighting a wave from
        // another — and the first measurement of this whole change came back wrong because of it.
        var before = Legacy(120);
        var after = Live(120);

        _out.WriteLine($"at x1: {before.FightSecondsPerWave:0.00}s and {before.ActionsPerWave:0.00} actions per wave");
        _out.WriteLine($"live : {after.FightSecondsPerWave:0.00}s and {after.ActionsPerWave:0.00} actions per wave");

        // At 1 the wave is the short one the playtest complained about — which is the proof that the
        // knob, and not some other edit of the same day, is what lengthened it.
        Assert.True(before.ActionsPerWave < 3f,
            $"with the knob at 1 a wave is already {before.ActionsPerWave:0.0} actions, so it is not the "
            + "knob that lengthened the wave and this band is guarding the wrong thing.");
        Assert.True(after.FightSecondsPerWave > before.FightSecondsPerWave * 1.5f,
            $"the knob moved a wave from {before.FightSecondsPerWave:0.0}s to only "
            + $"{after.FightSecondsPerWave:0.0}s.");
    }

    [Fact]
    public void test_the_champion_pool_carries_its_own_share_of_the_stretch()
    {
        // The pool is HALF the transform: stretch the wave alone and the champion eats 2.5x the bites
        // for the same pool, which pacing_test had already measured as depth falling from 8 to 4 under
        // the note "raising enemy health does not lengthen the run — it kills the champion sooner".
        // Guarded as an ordering, not a number, so retuning either scale cannot invert it.
        var hunter = Trained(0);
        var build = FourSkill();

        var stretched = SoloBattle.ChampionHealth(build, hunter);
        var flat = SoloBattle.ChampionHealth(build, hunter, 1f);

        _out.WriteLine($"a fresh champion's pool: {flat} before the stretch, {stretched} after");

        Assert.True(stretched > flat,
            "ChampionHealth ignores the wave-length stretch, so every wave is thicker and the champion "
            + "is not — the one case measured to make runs shallower rather than longer.");
        Assert.True(SoloBattle.ChampionPoolScale < ExpeditionTuning.Default.WaveLengthScale,
            "the pool carries the FULL wave stretch. Measured, that runs deeper than before the change: "
            + "a longer fight already favours the champion (the opening pause amortises, and healing "
            + "interleaves with damage instead of being wasted as overheal). See ChampionPoolScale.");
    }
}
