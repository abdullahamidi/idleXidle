using System.Collections.Generic;

namespace ResonanceHunter.Core.Encounters;

/// <summary>
/// A region's standing MODIFIER — the themed twist that gives each region its own feel and reward
/// identity, on top of the global corruption ladder (<see cref="CorruptionScaling"/>).
/// </summary>
/// <remarks>
/// Pure data, applied at the same combat/loot hooks the corruption ratchet uses: enemy health and damage
/// multipliers change how the region <i>fights</i>, and a loot-tier bonus makes the harder, deeper regions
/// visibly worth farming — reinforcing "push deeper for better loot". Surfaced on the Map's region detail.
/// </remarks>
public sealed record RegionModifier(string Name, string Blurb, float EnemyHealthMult, float EnemyDamageMult, int LootTierBonus)
{
    public static readonly RegionModifier None = new("STABLE GROUND", "No special rule here.", 1f, 1f, 0);
}

public static class RegionModifiers
{
    // One themed modifier per region — matched to its element/bias. Difficulty rises with depth, and so
    // does the loot-tier bonus, so a deeper region is both harder AND richer.
    private static readonly Dictionary<string, RegionModifier> ByRegion = new()
    {
        ["verdant_hollow"] = new("VERDANT OVERGROWTH", "Enemies +10% health.", 1.10f, 1f, 0),
        ["cinderworks"]    = new("MOLTEN PLATING", "Enemies +20% health · better loot.", 1.20f, 1f, 1),
        ["umbral_reach"]   = new("UMBRAL VEIL", "Enemies hit +20% harder · better loot.", 1f, 1.20f, 1),
        ["marrow_wastes"]  = new("BLOODHUNGER", "Enemies have +25% health and +10% damage.", 1.25f, 1.10f, 1),
        ["still_archive"]  = new("PSYCHIC STATIC", "Enemies hit +25% harder · better loot.", 1f, 1.25f, 2),
        ["pale_choir"]     = new("PALE REQUIEM", "Enemies have +20% health and damage · better loot.", 1.20f, 1.20f, 2),
    };

    /// <summary>The modifier for a region, or <see cref="RegionModifier.None"/> for an unmodified one.</summary>
    public static RegionModifier For(string regionId) => ByRegion.GetValueOrDefault(regionId, RegionModifier.None);
}
