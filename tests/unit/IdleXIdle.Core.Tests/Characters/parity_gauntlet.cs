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

    /// <summary>
    /// The state a wave STARTS from — what the waves before it left on the champion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A descent is one continuous fight: cooldowns are counted in BEATS against
    /// <see cref="Champion.BeatCount"/>, which never resets, so wave two opens with whatever readiness
    /// wave one happened to end on. That is production behaviour and the whole-gauntlet number is the
    /// authoritative one — but it makes a SINGLE WAVE's clear time a phase-sensitive reading, and a
    /// per-wave table that shows the percentage without the phase invites the reader to call a local
    /// negative a regression (2026-09-08: one pack wave read -12.8% inside a run that was +15.8%).
    /// </para>
    /// <para>
    /// So the small set of values that actually control the opening are read straight off the canonical
    /// champion — the beat clock, the pool, and how ready each woven skill is — and printed beside the
    /// row they explain. Nothing here is a snapshot of the fight; it is the entry state and no more.
    /// </para>
    /// </remarks>
    internal readonly record struct WavePhase(int Beat, int Health, IReadOnlyList<int> Waits)
    {
        /// <summary>Skills that could fire on the wave's first beat.</summary>
        public int Ready => Waits.Count(w => w <= 0);

        /// <summary>Beats the slowest woven skill still owes before it may fire.</summary>
        public int LongestWait => Waits.Count == 0 ? 0 : Waits.Max();

        public string Line =>
            $"entry beat {Beat,4} · health {Health,8:N0} · skills ready {Ready}/{Waits.Count} · longest wait {LongestWait,2} beat(s)";

        /// <summary>
        /// Do two arms open this wave on the same ACTION CLOCK — the same beat, with the same skills
        /// owing the same beats?
        /// </summary>
        /// <remarks>
        /// The clock is compared and the pool is not, deliberately. What decides a wave's opening
        /// tempo is when the next beat lands and which skills may fire on it; health is reported
        /// beside it because a reader wants to see it, but on this fixture the two arms differ by a
        /// few points in four hundred thousand and calling that "a different phase" would mark every
        /// row and mean nothing. (A record's own equality would also compare the list by reference.)
        /// </remarks>
        public bool SamePlaceAs(WavePhase o) => Beat == o.Beat && Waits.SequenceEqual(o.Waits);
    }

    /// <summary>Read the entry state off the champion the next wave is about to be handed.</summary>
    public static WavePhase PhaseOf(Champion champ, Build build)
    {
        ArgumentNullException.ThrowIfNull(champ);
        ArgumentNullException.ThrowIfNull(build);
        var waits = Enumerable.Range(0, build.Skills.Count)
                              .Select(i => champ.ReadyAtBeat.TryGetValue(i, out var ready) ? Math.Max(0, ready - champ.BeatCount) : 0)
                              .ToList();
        return new WavePhase(champ.BeatCount, champ.Health, waits);
    }

    private static List<WaveCreature> Wave(int count, float health, float damage, float defense)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = health,
            Health = health,
            Damage = damage,
            Defense = defense,
        }).ToList();
}
