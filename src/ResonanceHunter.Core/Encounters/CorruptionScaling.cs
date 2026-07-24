using System;

namespace ResonanceHunter.Core.Encounters;

/// <summary>
/// The endgame ratchet. Once the whole world is conquered, the player may "deepen the corruption":
/// every region's creatures get tougher, and every reward gets richer, forever.
/// </summary>
/// <remarks>
/// This is what an idle game needs past its finish line — a reason to keep hunting after the last boss
/// falls. It is deliberately a ONE-WAY ratchet that resets NOTHING (the game's prestige pillar): higher
/// corruption is pure "more", scaling both threat and payoff so the permanent Memory Dust economy keeps
/// moving. Kept as pure functions of the tier so the balance lives in tests, not in the UI layer.
/// </remarks>
public static class CorruptionScaling
{
    /// <summary>Corrupted creatures are tankier: +60% max health per tier.</summary>
    public static float HealthMultiplier(int corruptionTier)
        => 1f + 0.6f * Math.Max(0, corruptionTier);

    /// <summary>Each corruption tier adds one effective power tier, so attacks hit harder and wind faster.</summary>
    public static int TierBonus(int corruptionTier) => Math.Max(0, corruptionTier);

    /// <summary>Rewards (Memory Dust from mastery and boss kills) scale up: ×(1 + tier).</summary>
    public static float RewardMultiplier(int corruptionTier)
        => 1f + Math.Max(0, corruptionTier);

    /// <summary>
    /// The Memory Dust milestone paid the moment a new corruption tier is reached — a concrete, one-off
    /// payoff for choosing to make the world harder.
    /// </summary>
    public static int DeepeningDustAward(int newCorruptionTier)
        => 60 * Math.Max(1, newCorruptionTier);
}
