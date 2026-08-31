using System;

namespace IdleXIdle.Core.Encounters;

/// <summary>
/// What a corruption tier does to the fight and the payout.
/// </summary>
/// <remarks>
/// BOUNDED since 2026-08-23: every function clamps its tier to <see cref="MaxTier"/>. DEEPEN used to
/// count up forever, and the playtest said exactly what that felt like — "bir sayı arttırıyormuşum
/// hissi veriyor, zorluk arttırdığımı hissedemiyorum." Five named tiers the world visibly answers to
/// (<see cref="CorruptionLook"/>) read as a climb; an unbounded counter reads as a counter.
/// </remarks>
public static class CorruptionScaling
{
    /// <summary>The deepest the corruption goes. Tiers are 0 (the base world) to this.</summary>
    public const int MaxTier = 5;

    /// <summary>A tier clamped into the ladder — what every other function and the presentation read.</summary>
    public static int Clamp(int corruptionTier) => Math.Clamp(corruptionTier, 0, MaxTier);

    public static float HealthMultiplier(int corruptionTier)
        => 1f + 0.6f * Clamp(corruptionTier);

    public static int TierBonus(int corruptionTier) => Clamp(corruptionTier);

    public static float RewardMultiplier(int corruptionTier)
        => 1f + Clamp(corruptionTier);

    public static int DeepeningDustAward(int newCorruptionTier)
        => 60 * Math.Max(1, Clamp(newCorruptionTier));

    /// <summary>Dust paid when region-mastery levels are crossed — per new level, scaled by the tier's reward.</summary>
    /// <remarks>P12: the host used to inline this as a bare <c>15 ×</c> literal; on the model, a test pins it.</remarks>
    public static int MasteryLevelDust(int newLevels, int corruptionTier)
        => (int)(Math.Max(0, newLevels) * 15 * RewardMultiplier(corruptionTier));

    /// <summary>Dust paid once when a region is conquered, scaled by the tier's reward.</summary>
    /// <remarks>P12: hoisted from the host's bare <c>40 ×</c> literal for the same reason.</remarks>
    public static int ConquestDust(int corruptionTier)
        => (int)(40 * RewardMultiplier(corruptionTier));
}
