using System;

namespace ResonanceHunter.Core.Progression;

/// <summary>How far a region has been mastered. Drives the idle-efficiency band.</summary>
public enum MasteryLevel
{
    NewlyConquered = 0,
    PartiallyMastered = 1,
    FullyMastered = 2,
    OptimizedTeam = 3,
}

/// <summary>
/// The game's central balance promise, in one place: <b>active is always better, idle is never
/// worthless, and active is never more than ~3x better.</b>
///
/// Both halves are percentages of the SAME fixed anchor — <c>parClearTimeSeconds</c>, a hand-authored
/// constant on each encounter template, defined as *the clear time of a competent, fully-mastered
/// automated team*.
/// </summary>
/// <remarks>
/// <para>
/// <b>The anchor must never move.</b> Active efficiency was originally measured against automation's
/// LIVE clear time — which improves with mastery. The denominator therefore moved as the player's own
/// automation got better, so identical play scored 166% -> 92.5% -> 63.2% -> 50% across the mastery
/// ladder while automation did ~3.1x the kills/hour. Pillar 3 was arithmetically INVERTED, and the
/// active loot bonus (which keys off <c>activeEfficiency - 100</c>) silently clamped to zero after
/// about 26 minutes of team-building. That was Blocker B1.
/// </para>
/// <para>
/// If a future change makes the anchor vary with the player's mastery, team, or automation stage,
/// the bug has regressed. <see cref="ResonanceHunter.Core.Tests"/> asserts this directly.
/// </para>
/// </remarks>
public static class EfficiencyContract
{
    /// <summary>
    /// The locked idle-efficiency bands, as percentages of par. Specified by the design brief.
    /// Note that FullyMastered's ceiling converges on par (100%) and OptimizedTeam modestly exceeds
    /// it — exactly what you would expect if par is *defined* as a fully-mastered automated team.
    /// </summary>
    public static (float Min, float Max) IdleBand(MasteryLevel level) => level switch
    {
        MasteryLevel.NewlyConquered => (25f, 40f),
        MasteryLevel.PartiallyMastered => (50f, 70f),
        MasteryLevel.FullyMastered => (80f, 100f),
        MasteryLevel.OptimizedTeam => (100f, 120f),
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    /// <summary>The strongest idle efficiency reachable in the game, across every mastery level.</summary>
    public const float MaxIdleEfficiencyPercent = 120f;

    /// <summary>
    /// Formula 4 — idle efficiency, as a percentage of par.
    /// </summary>
    /// <param name="teamQualityScore">0..1. Composition-driven; a team of maxed Producers scores below a modest complete team.</param>
    public static float IdleEfficiencyPercent(MasteryLevel level, float teamQualityScore)
    {
        if (teamQualityScore is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(teamQualityScore), teamQualityScore, "Must be in [0, 1].");

        var (min, max) = IdleBand(level);
        return min + (max - min) * teamQualityScore;
    }

    /// <summary>
    /// Formula 6 — automation's actual clear time. DERIVED, an output only.
    /// </summary>
    /// <remarks>
    /// Causation runs one way: par is the fixed anchor, and automation's clear time is derived from
    /// it. This value feeds display surfaces and throughput simulation. It must NEVER be fed back
    /// into <see cref="ActiveEfficiencyPercent"/> — that is precisely the B1 feedback loop.
    /// </remarks>
    public static float AutomationClearTimeSeconds(float parClearTimeSeconds, float idleEfficiencyPercent)
    {
        const float epsilon = 0.01f;
        return parClearTimeSeconds / Math.Max(epsilon, idleEfficiencyPercent / 100f);
    }

    // ActiveEfficiencyPercent + ActiveLootBonus were removed here: they were Formula 7 of the retired
    // manual-combat "active play beats the farm" contract, read only by the deleted Encounter and CombatTuning.
    // The solo auto-battler has no active play, so there is no active efficiency to measure — only the IDLE
    // half above is live (RegionAutomation reads it to run the farm).

}
