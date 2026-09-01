using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;

namespace IdleXIdle.Core.Expeditions;

/// <summary>
/// The champion's earnings while nobody watched — computed by RUNNING THE GAME, not by guessing.
/// </summary>
/// <remarks>
/// <para>
/// Until P5 (2026-08-31) offline credit was <c>ChampionGleamRate × seconds × 0.5</c>: a rate
/// measured LAST session, applied to a build, region and corruption tier that may all have changed
/// since, and zero for any player whose last session never fought for ten sustained seconds. This
/// runs the real <see cref="Descent"/> — the same waves, the same falls, the same between-wave
/// breaths — against the build as it stands at load.
/// </para>
/// <para>
/// CAPPED twice, deliberately. The credited seconds are already bounded by
/// <see cref="Persistence.SaveSystem.MaxOfflineSeconds"/> (24 h); on top of that, at most
/// <see cref="MaxSimulatedWaves"/> waves are actually resolved (a bounded, sub-second cost at
/// load), and any remaining credited time is extrapolated at the rate the SIMULATED portion just
/// measured. The estimate is grounded in what this build does today, not in what last session's
/// build did — the memory's rule that a probe must model the loop, applied to the loop itself.
/// </para>
/// <para>
/// What offline deliberately does NOT do: level skills (<see cref="Descent.Progress"/> stays
/// null — build depth is earned watching), advance career records or quests, drop chests or
/// materials, or touch the world. It pays GLEAM, at <see cref="Haircut"/> of what live play pays,
/// because coming back must always beat staying away.
/// </para>
/// </remarks>
public static class OfflineHunt
{
    /// <summary>Offline pays this share of what the same fight pays live. Half: away is never as good as playing.</summary>
    public const float Haircut = 0.5f;

    /// <summary>The most waves actually resolved per credit — the CPU bound on a load.</summary>
    public const int MaxSimulatedWaves = 1500;

    /// <summary>A wave never spends less than this — a degenerate event stream still takes time.</summary>
    public const float MinWaveSeconds = 1.0f;

    /// <summary>What the trip earned, and how the number was grounded.</summary>
    /// <param name="Gleam">The credit, haircut applied — what the host adds to the balance.</param>
    /// <param name="WavesCleared">Waves actually resolved and cleared in the simulated portion.</param>
    /// <param name="Falls">Times the champion fell and regrouped.</param>
    /// <param name="DeepestWave">The deepest simulated wave — informational; never a record.</param>
    /// <param name="SimulatedSeconds">The credited time that was genuinely simulated.</param>
    /// <param name="ExtrapolatedSeconds">The remainder covered at the measured rate (0 unless the wave cap hit).</param>
    /// <param name="GleamPerSecond">The rate the simulated portion measured, BEFORE the haircut.</param>
    public readonly record struct Result(
        long Gleam, int WavesCleared, int Falls, int DeepestWave,
        double SimulatedSeconds, double ExtrapolatedSeconds, double GleamPerSecond);

    /// <summary>
    /// Fight the credited seconds through, wave by wave, and say what they were worth.
    /// Deterministic for a given seed — the loot-relevant rolls inside a wave draw from it.
    /// </summary>
    public static Result Simulate(
        Build build, Hunter hunter, double seconds,
        string regionId, AttackBias bias, float enemyBaseHealth, float enemyBaseDamage,
        int seed, ExpeditionTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);

        var descent = new Descent
        {
            RegionId = regionId,
            EnemyBias = bias,
            Tuning = tuning ?? ExpeditionTuning.Default,
            Progress = null,   // unwatched waves pay gleam, not build depth
            Rng = new Random(seed),
        };

        var budget = Math.Max(0.0, seconds);
        var consumed = 0.0;
        long rawGleam = 0;
        var waves = 0;
        var falls = 0;
        var deepest = 0;
        var pushed = 0;

        while (consumed < budget && pushed < MaxSimulatedWaves)
        {
            if (descent.Run is null || descent.Run.Over)
                descent.StartRun(build, hunter, enemyBaseHealth, enemyBaseDamage);

            var outcome = descent.PushWave();
            pushed++;
            var run = descent.Run!;

            // The wave costs what it SIMULATED — its last event's timestamp — plus the loop's own
            // breath. Same rhythm the live screen plays at x1, so an hour away is an hour's play.
            var waveSeconds = (double)MinWaveSeconds;
            if (run.LastWaveEvents.Count > 0)
                waveSeconds = Math.Max(MinWaveSeconds, run.LastWaveEvents.Max(e => e.AtMs) / 1000.0);
            consumed += waveSeconds;

            if (outcome == WaveOutcome.Cleared)
            {
                consumed += Descent.WaveBreakSeconds;
                while (descent.HasReward)
                {
                    var r = descent.TakeReward();
                    rawGleam += r.Haul.Gleam;
                    waves++;
                }
                if (run.Wave > deepest) deepest = run.Wave;
            }
            else
            {
                falls++;
                consumed += DownedRestartSeconds;
            }
        }

        var simulated = Math.Min(budget, consumed);
        var leftover = Math.Max(0.0, budget - consumed);
        var rate = simulated > 0 ? rawGleam / simulated : 0.0;

        // The wave cap hit with time still on the clock: the rest is paid at the rate the simulated
        // portion measured — grounded in THIS build against THIS region, minutes ago.
        var total = rawGleam + rate * leftover;

        return new Result(
            Gleam: (long)(total * Haircut),
            WavesCleared: waves,
            Falls: falls,
            DeepestWave: deepest,
            SimulatedSeconds: simulated,
            ExtrapolatedSeconds: leftover,
            GleamPerSecond: rate);
    }

    /// <summary>The fall's cost in seconds: the recovery beat before the next descent begins.</summary>
    private const double DownedRestartSeconds = Descent.DownedSeconds;
}
