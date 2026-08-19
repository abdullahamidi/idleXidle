using System;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>
/// A weapon FAMILY is a mechanical identity now, not just an art bucket.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, item-system redesign: "itemlerin basit statları olacak. sword - atk power, bow -
/// projectile damage, staff - magic power/skill cooldown, rapier - crit chance/atk speed." Mapped onto
/// the four channels this game actually fights with: a BLADE hits harder, a BOW crits, a SPEAR speeds
/// the skills, a SCYTHE reaps richer loot. The family was already stable per item
/// (<see cref="ItemNaming.WeaponFamilyIndex"/>, id-derived) and already in the name and the art — this
/// gives the word the player reads a number the fight obeys.
/// </para>
/// <para>
/// The bonus scales with ITEM LEVEL only — rarity already buys affix count, and a family identity that
/// grew with rarity would just be another rarity stat. It feeds <c>HunterProgression.AffixTotals</c>,
/// the same channel every affix uses, so every downstream number (power, defense, crit, the squad
/// multipliers) sees it with no second wiring.
/// </para>
/// </remarks>
public static class ItemFamilies
{
    /// <summary>Family index (see <see cref="ItemNaming.WeaponFamilies"/>) → its inherent channel.</summary>
    public static readonly AffixStat[] WeaponChannel =
        { AffixStat.Damage, AffixStat.Crit, AffixStat.SkillRate, AffixStat.Haul };   // blade, bow, spear, scythe

    /// <summary>
    /// The built-in bonus this item carries by being what it is, or null for the unwearable.
    /// </summary>
    /// <remarks>
    /// Weapons differ BY FAMILY (the four channels above); every other slot has one fixed identity at
    /// half a weapon's weight — a helm guards, a chestplate sustains, gloves strike, boots quicken, a
    /// ring enriches. This is also what makes REFINE always mean something: a prefixless Common helm
    /// has no prefix mods and no affixes, and before this its item level fed nothing at all.
    /// </remarks>
    public static (AffixStat Stat, float Magnitude)? BonusOf(ItemInstance? item)
    {
        if (item is null) return null;
        // CAPPED at 4x (item level ~51), aligned with ItemAffixes.IlvlFactor's own saturation. REFINE
        // is an infinite sink; an unbounded linear identity would quietly become the one lever that
        // buys power forever, which is the exact failure those caps exist to close.
        var scale = MathF.Min(1.0f + 0.06f * (item.ItemLevel - 1), 4f);

        if (item.BaseType == ItemBaseType.Weapon)
        {
            var stat = WeaponChannel[ItemNaming.WeaponFamilyIndex(item)];
            return (stat, ItemAffixes.BaseMagnitude(stat) * scale);
        }

        AffixStat? channel = item.BaseType switch
        {
            ItemBaseType.Helm => AffixStat.Defense,
            ItemBaseType.Chest => AffixStat.Health,
            ItemBaseType.Gloves => AffixStat.Damage,
            ItemBaseType.Boots => AffixStat.SkillRate,
            ItemBaseType.Ring => AffixStat.Haul,
            ItemBaseType.Charm => AffixStat.Health,
            ItemBaseType.AbilityFocus => AffixStat.SkillRate,
            _ => null,
        };
        if (channel is not { } c) return null;
        return (c, ItemAffixes.BaseMagnitude(c) * 0.5f * scale);
    }

    /// <summary>One plain line per family — what picking this weapon shape means.</summary>
    public static string Blurb(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ItemNaming.WeaponFamilyIndex(item) switch
        {
            0 => "A BLADE hits harder.",
            1 => "A BOW lands critical hits more often.",
            2 => "A SPEAR brings skills back faster.",
            _ => "A SCYTHE reaps richer loot.",
        };
    }
}
