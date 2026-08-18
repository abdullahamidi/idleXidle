using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>
/// Turns an item's rolled data into a name — a <b>prefix</b> from its dominant affix, its <b>element</b>,
/// and a <b>type word</b> (the weapon family, or the slot). This is where the deep, varied item pool
/// becomes legible: two same-base drops read as "FURIOUS SHADOW BLADE" vs "IMMORTAL SHADOW BLADE".
/// </summary>
/// <remarks>
/// Pure and deterministic — every part derives from the item's id/affixes, nothing is stored, and (unlike
/// the old art seed) the weapon family/variant derive from the InstanceId, NOT the item level, so refining
/// an item never silently changes what it looks like or what it's called. The prefixes are DATA over the
/// existing art: no new asset is needed to make the pool feel deep.
/// </remarks>
public static class ItemNaming
{
    /// <summary>The weapon art families (also the weapon "type" words). Art keys are `weapon_&lt;family&gt;_NN`.</summary>
    public static readonly string[] WeaponFamilies = { "blade", "bow", "spear", "scythe" };

    /// <summary>How many art variants exist per weapon family (`weapon_blade_01..05`).</summary>
    public const int WeaponVariants = 5;

    private static readonly Dictionary<AffixStat, string> Prefixes = new()
    {
        [AffixStat.Damage] = "FURIOUS",
        [AffixStat.Health] = "IMMORTAL",
        [AffixStat.SkillRate] = "NIMBLE",
        [AffixStat.Haul] = "GREEDY",
        [AffixStat.Crit] = "VICIOUS",
        [AffixStat.Defense] = "WARDED",
    };

    /// <summary>The item's dominant affix — the stat rolled strongest relative to a typical roll of that stat.</summary>
    /// <remarks>Normalising by each stat's base magnitude is what lets a "+9% damage" beat a "+8 defense"
    /// fairly, instead of the raw-magnitude stats (Crit, Defense) always winning.</remarks>
    public static AffixStat? DominantAffix(ItemInstance? item)
    {
        var affixes = ItemAffixes.Of(item);
        if (affixes.Count == 0) return null;
        return affixes
            .OrderByDescending(a => a.Magnitude / MathF.Max(0.0001f, ItemAffixes.BaseMagnitude(a.Stat)))
            .First().Stat;
    }

    /// <summary>The item's prefix (from its dominant affix), or null for an affix-less item (a Common).</summary>
    public static string? Prefix(ItemInstance? item)
        => DominantAffix(item) is { } stat ? Prefixes[stat] : null;

    /// <summary>Which weapon family this item is — stable across its life (InstanceId-derived, ilvl-independent).</summary>
    public static int WeaponFamilyIndex(ItemInstance item) => (int)(Fnv1a(item.InstanceId + "|fam") % (uint)WeaponFamilies.Length);

    /// <summary>Which art variant within the family (0-based) — also stable.</summary>
    public static int WeaponVariantIndex(ItemInstance item) => (int)(Fnv1a(item.InstanceId + "|var") % WeaponVariants);

    /// <summary>The runtime art key for this item's icon, if it's a weapon (`weapon_&lt;family&gt;_NN`).</summary>
    public static string WeaponArtKey(ItemInstance item)
        => $"weapon_{WeaponFamilies[WeaponFamilyIndex(item)]}_{WeaponVariantIndex(item) + 1:00}";

    /// <summary>A stable per-item seed for picking non-weapon thumbnail art — InstanceId-derived, so a
    /// refine (which changes the item level) never shuffles which thumbnail an item shows.</summary>
    public static uint ArtSeed(ItemInstance item) => Fnv1a(item.InstanceId + "|art");

    /// <summary>The "type" word — a weapon's family (BLADE/BOW/…), otherwise the slot word.</summary>
    public static string TypeWord(ItemInstance item) => item.BaseType == ItemBaseType.Weapon
        ? WeaponFamilies[WeaponFamilyIndex(item)].ToUpperInvariant()
        : SlotWord(item.BaseType);

    /// <summary>The plain slot word for any base type.</summary>
    public static string SlotWord(ItemBaseType type) => type switch
    {
        ItemBaseType.Weapon => "WEAPON",
        ItemBaseType.Charm => "CHARM",
        ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.Helm => "HELM",
        // "CHESTPLATE", not "CHEST": the armour slot shared its one-word name with the treasure
        // chest, and the chest-open reveal printed "EPIC CHEST" over a picture of a breastplate.
        ItemBaseType.Chest => "CHESTPLATE",
        ItemBaseType.Gloves => "GLOVES",
        ItemBaseType.Boots => "BOOTS",
        ItemBaseType.Ring => "RING",
        ItemBaseType.CreatureCore => "CORE",
        _ => "MATERIAL",
    };

    /// <summary>
    /// The full display name: `PREFIX ELEMENT TYPE` (e.g. "FURIOUS SHADOW BLADE"). Absent parts drop out, so
    /// a plain Common weapon reads "SHADOW BLADE" and an inert material just "MATERIAL".
    /// </summary>
    public static string FullName(ItemInstance? item)
    {
        if (item is null) return "";
        var parts = new[]
        {
            Prefix(item),
            item.Element?.ToString().ToUpperInvariant(),
            TypeWord(item),
        };
        return string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static uint Fnv1a(string s)
    {
        var hash = 2166136261u;
        foreach (var ch in s) { hash ^= ch; hash *= 16777619u; }
        return hash;
    }
}
