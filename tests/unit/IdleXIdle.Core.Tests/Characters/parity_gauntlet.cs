using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Core.Tests.Characters;

/// <summary>
/// The one gauntlet every roster measurement runs — the parity table and the Anvil diagnostic alike.
/// </summary>
/// <remarks>
/// <para>
/// Three wave shapes, because the roster specialises against wave shape and a single one would
/// flatter whoever it happens to suit: THE CHORUS scales with head-count and evaporates in a boss
/// room; THE ANVIL only refunds overkill when hits overshoot small creatures. A gauntlet of one shape
/// would rank those two by the shape chosen rather than by their worth.
/// </para>
/// <para>
/// Extracted from <see cref="RosterParityTest"/> (2026-09-07) so the diagnostic that investigates a
/// parity finding measures the SAME fixture the finding was measured on — a second copy of these
/// numbers would drift, and a diagnostic on a different gauntlet answers a different question.
/// </para>
/// </remarks>
internal static class ParityGauntlet
{
    public static List<WaveCreature> Swarm() => Wave(10, health: 320f, damage: 14f, defense: 0f);
    public static List<WaveCreature> Pack() => Wave(4, health: 1_600f, damage: 34f, defense: 45f);
    public static List<WaveCreature> Boss() => Wave(1, health: 14_000f, damage: 90f, defense: 70f);

    /// <summary>The three waves, in the order the gauntlet fights them.</summary>
    public static readonly (string Name, Func<List<WaveCreature>> Make)[] Waves =
    {
        ("swarm", Swarm), ("pack", Pack), ("boss", Boss),
    };

    /// <summary>
    /// The game's tuning with the anti-hang ceiling lifted.
    /// </summary>
    /// <remarks>
    /// <see cref="ExpeditionTuning.TickCeilingMs"/> is 120 seconds in the real game, where its job is to
    /// stop a hopeless wave hanging the loop. As a MEASUREMENT boundary it is poison: a wave that hits
    /// it reports 120s whatever the truth was, so every build slower than the ceiling records the same
    /// time and the ranking silently becomes a list of who saturated.
    /// </remarks>
    public static readonly ExpeditionTuning Tuning = ExpeditionTuning.Default with { TickCeilingMs = 1_800_000 };

    /// <summary>
    /// Health large enough that the gauntlet does not kill anyone: a fixture whose champion dies pins
    /// HealthLost at the pool size, and every defensive passive then measures identical.
    /// </summary>
    public const int ChampionHealth = 400_000;

    public const int EnemyIntervalMs = 900;

    /// <summary>How many seeds each measurement averages over — see <see cref="RosterParityTest"/> for why.</summary>
    public const int Seeds = 40;

    public static int Seed(int index) => 20260814 + index * 7919;

    public static Champion FreshChampion() => new() { MaxHealth = ChampionHealth, Health = ChampionHealth };

    private static List<WaveCreature> Wave(int count, float health, float damage, float defense)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = health,
            Health = health,
            Damage = damage,
            Defense = defense,
        }).ToList();
}
