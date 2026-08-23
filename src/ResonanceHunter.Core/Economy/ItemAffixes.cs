using System;
using System.Collections.Generic;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>The stat an explicit affix grants. Each maps to a channel the fight already reads.</summary>
public enum AffixStat { Damage, Health, SkillRate, Haul, Crit, Defense }

/// <summary>One rolled property on an item: a stat and how much of it.</summary>
public readonly record struct ItemAffix(AffixStat Stat, float Magnitude);

/// <summary>Formats a raw magnitude for its stat's own unit — see <see cref="ItemAffixes.GrantLabel"/>.</summary>

/// <summary>
/// An item's EXPLICIT affixes — the rolled bonuses on top of its base type's implicit trait + enchant.
/// </summary>
/// <remarks>
/// <para>
/// The item model had one trait (a trade) and one enchant (a trigger). An item was two facts, so two items
/// of the same type and rarity differed only in a coin-flip trait and a coin-flip enchant — not the varied,
/// "which of these is the upgrade?" loot an ARPG runs on. Affixes are the missing axis: a LIST of stat
/// bonuses whose COUNT is set by rarity (a Legendary carries four, a Common none) and whose MAGNITUDE grows
/// with <see cref="ItemInstance.ItemLevel"/> — so a deep drop is a better piece even at the same rarity, the
/// reason to push past "one more rarity tier".
/// </para>
/// <para>
/// Derived from the InstanceId like the trait and enchant, and for the same reasons: nothing to store, no
/// save migration, impossible to desync. A DIFFERENT hash salt again, so the affix roll does not correlate
/// with the trait or the enchant — three independent axes, not one wearing three hats. The trait stays the
/// item's identity (a trade you cannot avoid); these are pure bonuses that scale, which is what makes chasing
/// a higher-ilvl copy of the same base worthwhile.
/// </para>
/// </remarks>
public static class ItemAffixes
{
    /// <summary>
    /// A magnitude in its stat's OWN unit: Crit is percentage POINTS, Defense is FLAT, the four
    /// multiplicative channels are percentages.
    /// </summary>
    /// <remarks>
    /// The Forge's gem and family lines multiplied everything by 100 with a "%" — so a WARD gem
    /// granting +5.6 flat defense advertised "+560% DEFENCE" (adversarial review, pass five). One
    /// formatter, beside the magnitudes it formats, so a new display site cannot re-invent the bug.
    /// </remarks>
    public static string GrantLabel(AffixStat stat, float magnitude) => stat switch
    {
        AffixStat.Crit => $"+{magnitude:0.0}%",
        AffixStat.Defense => $"+{magnitude:0}",
        _ => $"+{magnitude * 100f:0}%",
    };

    /// <summary>How many explicit affixes a rarity carries. Common is implicit-only; a Legendary is loaded.</summary>
    public static int CountFor(Rarity rarity) => rarity switch
    {
        Rarity.Common => 0,
        Rarity.Uncommon => 1,
        Rarity.Rare => 2,
        Rarity.Epic => 3,
        Rarity.Legendary => 4,
        _ => 0,
    };

    /// <summary>Each stat's base per-affix magnitude at ilvl 0, before ilvl and rarity scaling.</summary>
    private static readonly Dictionary<AffixStat, float> Base = new()
    {
        [AffixStat.Damage] = 0.04f,     // +4% damage
        [AffixStat.Health] = 0.05f,     // +5% squad health
        [AffixStat.SkillRate] = 0.03f,  // +3% skill speed
        [AffixStat.Haul] = 0.06f,       // +6% haul
        [AffixStat.Crit] = 1.5f,        // +1.5% crit chance
        [AffixStat.Defense] = 8f,       // +8 flat defense
    };

    /// <summary>Per-ilvl growth of an affix's magnitude — the "a deeper drop is better" curve.</summary>
    /// <remarks>
    /// Read together with <see cref="IlvlHalf"/>: this is the SLOPE near iL0, not a rate that continues
    /// forever. See <see cref="IlvlFactor"/>.
    /// </remarks>
    public const float IlvlScale = 0.04f;

    /// <summary>Where the affix curve has spent half of its total growth.</summary>
    private const float IlvlHalf = 45f;

    /// <summary>
    /// How much item level multiplies an affix — saturating, not linear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was <c>1 + iL × 0.04</c>, unbounded, which meant a +4% damage affix reached +164% at iL1000
    /// and four of them turned one item into the entire build. It is half of why the playtest saw
    /// "meaningless big numbers": capping the weapon multiplier alone still left a deeply refined
    /// Legendary at 43k damage a second, because the affixes kept climbing after the weapon stopped.
    /// </para>
    /// <para>
    /// The shape mirrors <c>Gear.ItemLevelFactor</c> deliberately — the two levers item level pulls
    /// should saturate together, or the one that does not becomes the only one that matters. Tuned to
    /// keep the early curve the design was built around: iL20 lands within a whisker of the old linear
    /// value (1.80 against 1.80) and iL45 at 2.5, then flattens toward 4.
    /// </para>
    /// </remarks>
    public static float IlvlFactor(int itemLevel)
    {
        var levels = MathF.Max(0f, itemLevel);

        // 1 + G·x/(x+H) with G chosen so the slope at x=0 is IlvlScale: G = IlvlScale × H.
        const float growth = IlvlScale * IlvlHalf;
        return 1f + growth * levels / (levels + IlvlHalf);
    }

    /// <summary>The base per-affix magnitude of a stat (before ilvl/rarity/variance). Lets callers compare
    /// affixes of different stats on a common "how many multiples of a typical roll" scale — e.g. to pick an
    /// item's dominant affix for naming.</summary>
    public static float BaseMagnitude(AffixStat stat) => Base[stat];

    /// <summary>
    /// The affixes an item carries. Empty for non-wearables and Commons.
    /// </summary>
    public static IReadOnlyList<ItemAffix> Of(ItemInstance? item)
    {
        if (item is null || Gear.SlotFor(item.BaseType) is null) return Array.Empty<ItemAffix>();

        var count = CountFor(item.Rarity);
        if (count == 0) return Array.Empty<ItemAffix>();

        var stats = Enum.GetValues<AffixStat>();
        var ilvlFactor = IlvlFactor(item.ItemLevel);
        var rarityFactor = 0.6f + Gear.RarityPower(item.Rarity) * 0.10f;   // modest — rarity mostly buys COUNT

        var affixes = new List<ItemAffix>(count);
        for (var i = 0; i < count; i++)
        {
            // A fresh salted hash per affix slot, so the stat AND the roll vary but stay deterministic.
            var h = Fnv1a(item.InstanceId + "|affix" + i);
            var stat = stats[(int)(h % (uint)stats.Length)];
            var variance = 0.75f + (h >> 8) % 51u / 100f;   // 0.75 .. 1.25 — a wider spread so same-base items differ more
            var mag = Base[stat] * ilvlFactor * rarityFactor * variance;
            affixes.Add(new ItemAffix(stat, mag));
        }
        return affixes;
    }

    /// <summary>A one-line, player-facing description of an affix (its sign and unit).</summary>
    public static string Describe(ItemAffix a) => a.Stat switch
    {
        AffixStat.Damage => $"+{a.Magnitude * 100f:0}% DAMAGE",
        AffixStat.Health => $"+{a.Magnitude * 100f:0}% HEALTH",
        AffixStat.SkillRate => $"+{a.Magnitude * 100f:0}% SKILL RATE",
        AffixStat.Haul => $"+{a.Magnitude * 100f:0}% LOOT",
        AffixStat.Crit => $"+{a.Magnitude:0.0}% CRITICAL CHANCE",
        _ => $"+{a.Magnitude:0} DEFENCE",
    };

    private static uint Fnv1a(string s)
    {
        var hash = 2166136261u;
        foreach (var ch in s) { hash ^= ch; hash *= 16777619u; }
        return hash;
    }
}
