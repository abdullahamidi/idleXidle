using System;
using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;

namespace ResonanceHunter.Core.Builds;

/// <summary>What a bench test found: the damage the current build does to a reference dummy.</summary>
public readonly record struct DamageReadout(long TotalDamage, float Seconds, float Dps);

/// <summary>
/// A bench test for a build's damage. Runs the REAL <see cref="SoloBattle"/> against a fixed, unkillable
/// dummy, so the same build before and after a change — or two different builds — can be compared by one
/// honest number.
/// </summary>
/// <remarks>
/// <para>
/// This is a test instrument, not a game system. It exists so a player tuning gear, stats, or the mastery
/// tree can see whether a change actually moved the damage — and so a stat that quietly reaches no formula
/// (the recurring bug in this project) shows up plainly as "trained it, the number did not budge".
/// </para>
/// <para>
/// It drives the same <see cref="SoloBattle.ResolveWave"/> the real fight does, on purpose: a separate
/// estimator would be a second source of truth that could drift from the game and lie to the player. The
/// dummy never swings, so the champion stays at full health and the reading is the fresh-fight DPS,
/// deterministic and repeatable. (A build that scales with MISSING health — BLOODLUST — therefore reads
/// its floor here, and one that scales with health PRESENT — ZEAL — reads its ceiling; that is the
/// honest "at full health" number, and the caller can say so.)
/// </para>
/// </remarks>
public static class DamageBench
{
    /// <summary>Large enough that the dummy cannot die inside the window, so the whole window is measured.</summary>
    public const float DummyHealth = 1_000_000_000f;

    private const int Seed = 1234;

    /// <summary>Measure the build's damage against the reference dummy over one full tick window.</summary>
    public static DamageReadout Measure(Build build, Hunter hunter, ExpeditionTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);
        var t = tuning ?? ExpeditionTuning.Default;

        // Mirrors SoloExpeditionScreen's champion construction, RECKLESS OFFERING's health price included.
        var hp = Math.Max(1, (int)MathF.Round(Math.Max(60, hunter.MaxHealth) * SoloBattle.VowHealthMultiplier(build)));
        var champ = new Champion { MaxHealth = hp, Health = hp };

        // enemyDamage 0 and an interval far past the window so the dummy's (zero) swing never interferes.
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, hunter, DummyHealth, enemyDamage: 0f, enemyIntervalMs: 100_000,
            t, new Random(Seed));

        var total = events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => (long)e.Amount);
        var seconds = t.TickCeilingMs / 1000f;
        return new DamageReadout(total, seconds, seconds > 0f ? total / seconds : 0f);
    }
}
