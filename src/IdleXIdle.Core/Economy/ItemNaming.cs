using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Economy;

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

    /// <summary>
    /// The item's name prefix — its PREFIX trait's name, or null for a plain drop.
    /// </summary>
    /// <remarks>
    /// It used to come from the dominant AFFIX, while the trait was a separate word shown elsewhere —
    /// two vocabularies for "what this item is like". The prefix trait is the item's character now
    /// (rolled at mint, immutable), so the NAME carries it: "KEEN SHADOW BOW" is keen forever, and a
    /// reforge can never rename a weapon into a different-sounding one again.
    /// </remarks>
    public static string? Prefix(ItemInstance? item)
        => GearTraits.TraitOf(item) is { } t ? GearTraits.NameOf(t) : null;

    /// <summary>
    /// Which weapon family this item is — stable across its life. A weapon minted inside a class
    /// carries its family on the item (<see cref="ItemInstance.Family"/>); anything older derives it
    /// from the id, exactly as it always did, so an old bow stays a bow.
    /// </summary>
    public static int WeaponFamilyIndex(ItemInstance item)
        => item.Family is { } f && f >= 0 && f < WeaponFamilies.Length
            ? f
            : (int)(Fnv1a(item.InstanceId + "|fam") % (uint)WeaponFamilies.Length);

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
        ItemBaseType.Gem => "GEM",
        _ => "MATERIAL",
    };

    /// <summary>
    /// The full display name: `PREFIX ELEMENT TYPE` (e.g. "FURIOUS SHADOW BLADE"). Absent parts drop out, so
    /// a plain Common weapon reads "SHADOW BLADE" and an inert material just "MATERIAL".
    /// </summary>
    public static string FullName(ItemInstance? item)
    {
        if (item is null) return "";
        // A gem names itself by its stat and level — "TEMPO GEM 4" — never by element or slot word.
        if (item.BaseType == ItemBaseType.Gem) return $"{GemCraft.NameOf(item)} {item.ItemLevel}";
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
