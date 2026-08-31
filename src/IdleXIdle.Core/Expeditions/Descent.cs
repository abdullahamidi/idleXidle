using System;
using System.Collections.Generic;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;

namespace IdleXIdle.Core.Expeditions;

/// <summary>One cleared wave's payout, queued for the host to credit.</summary>
/// <remarks>
/// Lived as a nested type on the hunt SCREEN until P5 — which meant the only queue of earned
/// rewards in the game belonged to a MonoGame class and nothing headless could ever earn one.
/// </remarks>
public readonly record struct WaveReward(Haul Haul, int Wave, bool IsBoss);

/// <summary>
/// THE DESCENT: the headless run driver — mints a champion and a <see cref="SoloExpedition"/> per
/// life, pushes waves, queues each cleared wave's reward, and tracks the deepest wave of this
/// visit. The hunt screen DRIVES this and draws it; it no longer owns it.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <c>SoloExpeditionScreen.StartRun/BeginWave</c> (P5, 2026-08-31 — audit critique
/// G2: "Game1 and the hunt screen are the second Core"). The descent state machine living in a
/// MonoGame class meant there was NO headless fast-forward: offline progress was a rate estimate
/// (<c>ChampionGleamRate × seconds × 0.5</c>), loop probes could not model the loop, and the
/// migration tests had nothing to drive. <see cref="OfflineHunt"/> runs this same machine with
/// nobody watching.
/// </para>
/// <para>
/// The split of duties: the DESCENT owns what the run IS (champion pool, run index, wave pushes,
/// rewards, deepest). The SCREEN owns what the run LOOKS like (replay pacing, banners, the downed
/// beat) and WHEN the next wave is pushed live. The HOST owns what the rewards BUY (gleam,
/// materials, chests) and everything charged or latched across runs (checkpoint Dust, quest
/// counters) — crediting touches the whole economy and belongs beside it.
/// </para>
/// </remarks>
public sealed class Descent
{
    // ── The loop's rhythm, in seconds. Shared constants, not screen constants, because the
    //    ECONOMY runs on them: waves-per-hour is sim time plus these breaths, and an offline
    //    simulation that skipped them would quietly out-earn the live game it stands in for. The
    //    screen reads these for its transition beats; OfflineHunt spends them as time. ────────────
    /// <summary>The pause on the cleared frame — the fallen creature's beat (it has art behind it).</summary>
    public const float FallenBeat = 0.45f;

    /// <summary>The spoils beat — the haul rises out of the corpses.</summary>
    public const float SpoilsBeat = 0.45f;

    /// <summary>The breath before the next wave slides in.</summary>
    public const float BreathBeat = 0.20f;

    /// <summary>The full between-wave break.</summary>
    public const float WaveBreakSeconds = FallenBeat + SpoilsBeat + BreathBeat;

    /// <summary>The recovery beat after a fall, before the champion tries again.</summary>
    public const float DownedSeconds = 1.6f;

    // ── Configuration the host sets before (or between) runs. ────────────────────────────────────
    /// <summary>Which region's band cycle the runs walk — see <see cref="SoloExpedition.RegionId"/>.</summary>
    public string RegionId { get; set; } = "";

    /// <summary>The region's combat character — bends the enemy's bite tempo.</summary>
    public AttackBias EnemyBias { get; set; } = AttackBias.Balanced;

    public ExpeditionTuning Tuning { get; set; } = ExpeditionTuning.Default;

    /// <summary>
    /// Where the woven skills bank the levels they earn, or null when nothing should track them —
    /// which is also the offline setting: an unwatched descent pays gleam, not build depth.
    /// </summary>
    public SkillProgress? Progress { get; set; }

    /// <summary>The stream Harvest/Ricochet rolls draw from. Seed it and the descent is replayable.</summary>
    public Random Rng { get; set; } = new();

    // ── The live state. ──────────────────────────────────────────────────────────────────────────
    /// <summary>The current life's expedition, or null before the first run (and after <see cref="Reset"/>).</summary>
    public SoloExpedition? Run { get; private set; }

    /// <summary>The current life's champion — the screen's health bars read this.</summary>
    public Champion? Champion { get; private set; }

    /// <summary>Counts descents, so each one seeds its own compositions (see <see cref="SoloExpedition.RunIndex"/>).</summary>
    public int RunIndex { get; private set; }

    /// <summary>The deepest wave reached THIS VISIT — the host folds it into the career's records.</summary>
    public int Deepest { get; private set; }

    private readonly Queue<WaveReward> _rewards = new();

    public bool HasReward => _rewards.Count > 0;

    public WaveReward TakeReward() => _rewards.Dequeue();

    /// <summary>
    /// Travelling: drop the run and this visit's record. The run index stays monotonic — a replayed
    /// composition must never collide with one from before the trip.
    /// </summary>
    public void Reset()
    {
        Run = null;
        Champion = null;
        Deepest = 0;
        _rewards.Clear();
    }

    /// <summary>
    /// A NEW DESCENT IS A NEW CHAMPION: mint the pool from the build and hunter as they are now,
    /// and start a fresh expedition — optionally at a checkpoint (whose Dust the HOST charges;
    /// this only skips the waves).
    /// </summary>
    public SoloExpedition StartRun(Build build, Hunter hunter,
                                   float enemyBaseHealth, float enemyBaseDamage, int startWave = 0)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);

        var hp = SoloBattle.ChampionHealth(build, hunter);
        Champion = new Champion { MaxHealth = hp, Health = hp };
        Run = new SoloExpedition(build, Champion, hunter, enemyBaseHealth, enemyBaseDamage,
                                 Tuning, Rng)
        {
            EnemyBias = EnemyBias,
            RegionId = RegionId,
            RunIndex = ++RunIndex,
            // The skills bank their levels here rather than in the run, because a run ends and a
            // skill's levels do not.
            Progress = Progress,
        };
        if (startWave > 0)
        {
            Run.StartAtWave(startWave);
            Deepest = Math.Max(Deepest, startWave);
        }
        return Run;
    }

    /// <summary>
    /// Push one wave: re-mint the pool at the boundary, resolve the wave, and on a clear queue its
    /// reward and advance the record.
    /// </summary>
    /// <remarks>
    /// A live driver that re-composed the player's build hands it to
    /// <see cref="SoloExpedition.ReplaceBuild"/> BEFORE this call (the screen also needs the pool
    /// refreshed before it captures the replay's health table, so it may call
    /// <see cref="SoloExpedition.RefreshPool"/> itself — the refresh is idempotent and this call's
    /// own refresh then changes nothing). A headless driver needs neither: the build it started
    /// with is the build it fights with.
    /// </remarks>
    public WaveOutcome PushWave()
    {
        if (Run is not { } run)
            throw new InvalidOperationException("No run to push — StartRun first.");

        run.RefreshPool();
        var outcome = run.PushWave();
        if (outcome == WaveOutcome.Cleared)
        {
            // Pay this wave out NOW — the idle loop pays per wave, the instant it clears.
            _rewards.Enqueue(new WaveReward(run.LastWaveHaul, run.Wave, run.LastWaveWasBoss));
            if (run.Wave > Deepest) Deepest = run.Wave;
        }
        return outcome;
    }
}
