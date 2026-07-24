using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Loot;

public enum Rarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

public enum ItemBaseType { CreatureCore, Weapon, Charm, Material, AbilityFocus, Helm, Chest, Gloves, Boots, Ring }

/// <summary>
/// A minted item instance.
/// </summary>
/// <remarks>
/// <para>
/// This record had five fields and no room for anything the design asks of it: no element, no
/// modifiers, no enchantments. That was not an oversight to work around but the actual blocker — you
/// cannot author what the data cannot hold, and every "items should have X" idea died here quietly.
/// </para>
/// <para>
/// <b>Element reuses <see cref="Source"/> rather than declaring a parallel enum.</b> An item's fire
/// and a creature's fire must be the same fire, or the game has two elemental systems that players
/// will reasonably expect to interact and which cannot. In the solo model the item's element is a
/// CRAFTING axis, not a combat one: it is what the Forge's element-hoarding merge reads (a matched trio
/// carries its element into the fused item). The FIGHT's Source matchup keys off the woven SKILL's
/// Source vs the region — a worn item's element does not enter it.
/// </para>
/// </remarks>
public sealed record ItemInstance
{
    public required string InstanceId { get; init; }
    public required ItemBaseType BaseType { get; init; }
    public required Rarity Rarity { get; init; }
    public required int SellValue { get; init; }

    /// <summary>
    /// The item's LEVEL — the loot tier (depth) it dropped at, 1+.
    /// </summary>
    /// <remarks>
    /// It is the ARPG "item level": it widens the pool of affix tiers and lifts the stat budget an item
    /// can carry, so a deeper drop is a genuinely better piece even at the same rarity — a reason to push
    /// beyond "one more rarity tier". A HIGHER ilvl never forces a good roll, it only raises the ceiling.
    /// Defaulted to 1 so a pre-ilvl save's items read as the shallowest tier rather than crashing.
    /// </remarks>
    public int ItemLevel { get; init; } = 1;

    /// <summary>
    /// The item's element, or null for the elementally inert (materials, cores).
    /// </summary>
    /// <remarks>
    /// Nullable rather than defaulted to a Source: `?? 0` would silently make every pre-element save's
    /// items Body-attuned, which is the same class of bug as an empty gear slot paying out like a
    /// Common. Absent must mean absent.
    /// </remarks>
    public Source? Element { get; init; }

    /// <summary>Non-null only for an equipped charm. Makes the item ineligible for EVERY Forge operation.</summary>
    public string? EquippedToCreatureId { get; init; }

    /// <summary>
    /// A REFORGED trait, overriding the id-derived one. Null (the default) means "derive from the id".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A trait is normally a pure function of <see cref="InstanceId"/> — see <c>GearTraits.TraitOf</c> —
    /// which is what makes it free to store and impossible to desync. The cost of that purity is that
    /// the item is <b>frozen</b>: the player can never change what a drop rolled. The Reforge is the
    /// answer, and this is where the answer has to live: a single overriding value ON the item, because
    /// the change is per-item and must persist. When set, <c>GearTraits.TraitOf</c> returns it verbatim.
    /// </para>
    /// <para>
    /// Only ever holds a trait from the item's own slot pool — the Reforge rolls from that pool — so it
    /// cannot smuggle a HEAVY onto a charm. Absent stays absent (nullable, not defaulted) for the same
    /// reason <see cref="Element"/> is: <c>?? default</c> would silently give every un-reforged item the
    /// first trait in the enum, which is the same class of bug as an empty slot paying like a Common.
    /// </para>
    /// </remarks>
    public GearTrait? TraitOverride { get; init; }

    /// <summary>
    /// A REFORGED enchantment KIND, overriding the id-derived one. Null means "derive from the id".
    /// </summary>
    /// <remarks>
    /// Only the KIND is stored: the magnitude still follows rarity through <c>Enchantments.MagnitudeFor</c>,
    /// so a reforged LINGER on a Legendary is exactly as strong as a rolled one. And only Rare+ items
    /// carry an enchantment at all, so this is meaningless below <c>Enchantments.MinimumRarity</c> — the
    /// Reforge never sets it there, and <c>Enchantments.Of</c> still returns null for a sub-Rare item.
    /// </remarks>
    public EnchantKind? EnchantOverride { get; init; }
}

public sealed record LootTuning
{
    public float BonusDropChancePercent { get; init; } = 35f;
    public int BonusRollAttempts { get; init; } = 3;
    public int DropCountBaseStandard { get; init; } = 1;
    public int DropCountBaseBoss { get; init; } = 3;

    public float TiltQuantityScalar { get; init; } = 0.6f;
    public float EfficiencyQuantityScalar { get; init; } = 0.8f;

    /// <summary>Sums to 1000. Common is overwhelmingly likely; Legendary is 0.05%.</summary>
    public IReadOnlyList<float> BaseRarityWeight { get; init; } = new[] { 700f, 250f, 45f, 4.5f, 0.5f };

    public float PowerTierRarityScalar { get; init; } = 0.15f;
    public float TiltRarityScalar { get; init; } = 0.6f;

    /// <summary>Base sell value per rarity tier. Geometric, per item-data-schema's rarity_value_multiplier.</summary>
    public IReadOnlyList<int> RaritySellValue { get; init; } = new[] { 6, 14, 34, 82, 200 };

    public static LootTuning Default { get; } = new();

    /// <summary>
    /// How much of the rarity TILT an automated kill retains. Stage 1 keeps none; Stage 2 keeps half.
    /// </summary>
    /// <remarks>
    /// This only ever dampens the bonus tilt ABOVE the baseline weight — it never scales the baseline
    /// itself. That is what guarantees every rarity tier keeps a non-zero probability on an automated
    /// kill, which is the provable form of "idle play is never locked out of anything" (Pillar 3).
    /// </remarks>
    public float AutomationRarityParityPercent(int automationStage) => automationStage switch
    {
        <= 1 => 0f,
        2 => 0.5f,
        _ => 1.0f,
    };
}

/// <summary>The context a kill produces, feeding the drop roll.</summary>
public sealed record KillContext
{
    public required int PowerTier { get; init; }
    public bool IsBoss { get; init; }

    /// <summary>The region's element. Wearable drops are attuned to it; null leaves them inert.</summary>
    public Source? Element { get; init; }

    /// <summary>
    /// 0–40. How far above baseline this kill tilts loot — in BOTH quantity and rarity.
    /// </summary>
    /// <remarks>
    /// Was <c>PartBreakLootBonusPercent</c>, named for the manual-combat part-break the expedition pivot
    /// deleted. The lever is general and is now fed by an expedition's haul QUALITY (i.e. its Crafters),
    /// so the old name pointed the reader at a mechanic that no longer exists.
    /// </remarks>
    public float LootTiltPercent { get; init; }

    /// <summary>Formula 7's output. Exactly 100 for an automated kill — automation IS the reference.</summary>
    public float ActiveEfficiencyPercent { get; init; } = 100f;

    /// <summary>Null for an active kill. Set for an automated one.</summary>
    public int? AutomationStage { get; init; }

    public bool IsAutomated => AutomationStage is not null;
}

public static class LootSystem
{
    /// <summary>The eight wearable base types. Spelled out here so Loot keeps no dependency on Economy's Gear.</summary>
    private static readonly ItemBaseType[] Wearables =
    {
        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus,
        ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
    };

    /// <summary>
    /// Roll a kill's loot.
    /// </summary>
    /// <remarks>
    /// <b>Every kill mints exactly one creature_core, unconditionally, at 100%</b> — active or
    /// automated. Only its RARITY varies. This is non-tunable and is the structural guarantee that idle
    /// play can never be locked out of core progression (Pillar 3): cores are the only way to acquire a
    /// creature at MVP, since capture is Vertical-Slice-deferred.
    /// </remarks>
    public static IReadOnlyList<ItemInstance> Roll(KillContext ctx, Random rng, LootTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(tuning);

        var items = new List<ItemInstance>();

        // The guaranteed core. Never rolled, never skipped, never gated behind active play.
        items.Add(Mint(ItemBaseType.CreatureCore, RollRarity(ctx, rng, tuning), rng, tuning, ctx.Element, ctx.PowerTier));

        var count = DropCount(ctx, rng, tuning);
        for (var i = 0; i < count; i++)
        {
            // 45% material, the rest split evenly across the eight wearable slots.
            var baseType = rng.NextDouble() < 0.45 ? ItemBaseType.Material : Wearables[rng.Next(Wearables.Length)];
            items.Add(Mint(baseType, RollRarity(ctx, rng, tuning), rng, tuning, ctx.Element, ctx.PowerTier));
        }

        return items;
    }

    /// <summary>
    /// Mint one item. Wearables carry the REGION'S element; materials and cores are inert.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An element the loot table never set would be an element no player could ever have — the field
    /// would exist, the Forge would carry it through merges, the matchup would read it, and every
    /// single item in the game would be null. Exactly the orphan shape that made Charm, Focus, Weaving
    /// and the whole Dust tree do nothing.
    /// </para>
    /// <para>
    /// It comes from the REGION rather than a roll: Cinderworks is a Machine place, so its loot is
    /// Machine. That makes where you hunt the thing that decides what you can build, and gives the
    /// world map a reason to exist beyond unlocking the next number. It is also why hoarding by
    /// element is possible at all — a random element per drop would make a matched trio pure luck.
    /// </para>
    /// </remarks>
    private static ItemInstance Mint(ItemBaseType type, Rarity rarity, Random rng, LootTuning tuning, Source? element, int itemLevel) => new()
    {
        InstanceId = $"itm_{rng.Next(int.MaxValue):x8}",
        BaseType = type,
        Rarity = rarity,
        SellValue = tuning.RaritySellValue[(int)rarity],
        ItemLevel = Math.Max(1, itemLevel),

        // Only wearables are attuned. An elemental lump of scrap would be noise — and it would let a
        // trio of materials carry an element into a hybrid, which is the one thing mixing must cost.
        // Materials and cores are the only inert types; every wearable slot attunes.
        Element = type is ItemBaseType.Material or ItemBaseType.CreatureCore ? null : element,
    };

    /// <summary>Formula 1 — Drop Count. Hard-bounded; the clamp is what makes it un-exploitable.</summary>
    public static int DropCount(KillContext ctx, Random rng, LootTuning tuning)
    {
        var chance = EffectiveBonusDropChancePercent(ctx, tuning);
        var baseCount = ctx.IsBoss ? tuning.DropCountBaseBoss : tuning.DropCountBaseStandard;

        var bonus = 0;
        for (var i = 0; i < tuning.BonusRollAttempts; i++)
            if (rng.NextDouble() * 100.0 < chance) bonus++;

        return baseCount + bonus;
    }

    /// <summary>
    /// The clamped bonus chance.
    /// </summary>
    /// <remarks>
    /// <b>The clamp to 100 is load-bearing.</b> active_efficiency_percent feeds BOTH kills/hour (via
    /// Formula 7's clear-time term) and loot/kill (here), which looks like it should compound
    /// multiplicatively and blow past the design's "~3x" ceiling. It cannot, because this clamp binds
    /// first: peak skill already overshoots to ~110% and is cut back to 100. Remove the clamp and peak
    /// active throughput grows without a ceiling.
    /// </remarks>
    public static float EffectiveBonusDropChancePercent(KillContext ctx, LootTuning tuning)
    {
        var tilt = 1f + tuning.TiltQuantityScalar * (ctx.LootTiltPercent / 100f);
        var efficiency = 1f + tuning.EfficiencyQuantityScalar
            * MathF.Max(0f, (ctx.ActiveEfficiencyPercent - 100f) / 100f);

        return Math.Clamp(tuning.BonusDropChancePercent * tilt * efficiency, 0f, 100f);
    }

    /// <summary>Formula 2 — Rarity Distribution (Weighted Tilt).</summary>
    public static Rarity RollRarity(KillContext ctx, Random rng, LootTuning tuning)
    {
        var weights = RarityWeights(ctx, tuning);
        var total = weights.Sum();
        var roll = rng.NextDouble() * total;

        var cumulative = 0.0;
        for (var i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return (Rarity)i;
        }

        return Rarity.Common;
    }

    /// <summary>
    /// The tilted rarity weights.
    /// </summary>
    /// <remarks>
    /// Two properties are proven here rather than hoped for:
    /// <list type="number">
    ///   <item>At i=0 (Common) the <c>i</c> factor zeroes both bonus terms, so Common's weight is never
    ///         suppressed.</item>
    ///   <item>For every i&gt;0 the applied tilt is &gt;= 1, because automation parity only dampens the
    ///         bonus ABOVE the baseline — it never scales the baseline down. So <b>every rarity tier
    ///         keeps a non-zero probability under every combination of power_tier, part-break
    ///         performance, and automation stage.</b> Idle can never be locked out of a tier.</item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<float> RarityWeights(KillContext ctx, LootTuning tuning)
    {
        var parity = ctx.AutomationStage is { } stage
            ? tuning.AutomationRarityParityPercent(stage)
            : 1.0f; // an active kill retains the full tilt

        var weights = new float[5];

        for (var i = 0; i < 5; i++)
        {
            var tilt =
                (1f + i * tuning.PowerTierRarityScalar * (ctx.PowerTier - 1))
                * (1f + i * tuning.TiltRarityScalar * (ctx.LootTiltPercent / 100f));

            var applied = 1f + (tilt - 1f) * parity;

            weights[i] = tuning.BaseRarityWeight[i] * applied;
        }

        return weights;
    }
}
