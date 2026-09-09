using System;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Core.Builds;

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

    /// <summary>How many seeded runs the reading averages. See the remarks: crit is a rolled event.</summary>
    /// <remarks>
    /// One run stopped being a stable number on 2026-09-09, when critical hits became rolled events
    /// rather than a folded expected-value multiplier. A single seed then reads one sample of a random
    /// sum: cheap for a fast multi-hit build (a few percent) and expensive for a slow heavy one, whose
    /// window holds only a handful of hits. Averaging <c>Runs</c> seeded runs puts the reading back
    /// under a percent of its own mean without inventing a second estimator — every run is still the
    /// real <see cref="SoloBattle.ResolveWave"/>, and the seeds are fixed, so the bench is reproducible
    /// to the byte. THE PLAYER READS THIS NUMBER on the character screen, so a jittering figure would
    /// be read as the build changing when nothing had.
    /// </remarks>
    public const int Runs = 24;

    /// <summary>Measure the build's damage against the reference dummy over one full tick window.</summary>
    public static DamageReadout Measure(Build build, Hunter hunter, ExpeditionTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);
        var t = tuning ?? ExpeditionTuning.Default;

        // The one mint helper, shared with the live screen so the bench cannot drift from the game.
        var hp = SoloBattle.ChampionHealth(build, hunter);

        long total = 0;
        for (var run = 0; run < Runs; run++)
        {
            var champ = new Champion { MaxHealth = hp, Health = hp };
            // enemyDamage 0 and an interval far past the window so the dummy's (zero) swing never interferes.
            var (_, events) = SoloBattle.ResolveWave(
                champ, build, hunter, DummyHealth, enemyDamage: 0f, enemyIntervalMs: 100_000,
                t, new Random(Seed + run * 7919));

            total += events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => (long)e.Amount);
        }

        total /= Runs;
        var seconds = t.TickCeilingMs / 1000f;
        return new DamageReadout(total, seconds, seconds > 0f ? total / seconds : 0f);
    }
}
