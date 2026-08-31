using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Economy;

namespace IdleXIdle.Core.Loot;

public enum Rarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

public enum ItemBaseType
{
    CreatureCore, Weapon, Charm, Material, AbilityFocus, Helm, Chest, Gloves, Boots, Ring,

    /// <summary>A socketable STAT GEM — not wearable itself; it lives inside a Rare+ item's socket.</summary>
    Gem,
}

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
    /// How many REFINES this item has taken, 0..15. The redesign's enhancement ladder: the first five
    /// never fail, then a rising slip chance, and +15 is the top. A failed refine steps BOTH this and
    /// the item level back by one, so the ladder is climbed, not bought.
    /// </summary>
    public int Upgrades { get; init; }

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
    /// The STAT GEMS socketed into this item — themselves items (<see cref="ItemBaseType.Gem"/>).
    /// </summary>
    /// <remarks>
    /// Playtest, item-system redesign: "rare eşyada 1, epicte 2 ve legendary'de 3 slot... stat taşları
    /// rastgele özellik verecek ve kendi seviyeleri olacak." Wearables only; a gem never nests gems.
    /// NOTE: a record `with`-copy SHARES this list — <c>GemCraft.Socket/Crush</c> therefore always
    /// build a NEW list for their product, so an old copy can never see a gem it should not.
    /// </remarks>
    public List<ItemInstance> Gems { get; init; } = new();

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

    /// <summary>
    /// The item's CLASS — who can wear it — or null for an item minted before classes existed.
    /// </summary>
    /// <remarks>
    /// Nullable for the same reason <see cref="Element"/> is: a `?? Warden` would silently hand every
    /// pre-class save's helms to two champions and take them off the other eight. Null reads as
    /// "anyone" — see <c>ItemClasses.CanWear</c> — so an update never strips a worn piece. Meaningless
    /// on a universal slot (charm, ring, focus) and on anything unwearable; every mint path sets it on
    /// the five class-locked slots and leaves it null elsewhere.
    /// </remarks>
    public ItemClass? Class { get; init; }

    /// <summary>
    /// A weapon's FAMILY (an index into <c>ItemNaming.WeaponFamilies</c>), or null to derive it from the id.
    /// </summary>
    /// <remarks>
    /// The family used to be a pure function of the id, which was fine while any weapon could be any
    /// shape. A class carries only two shapes, so a fresh weapon's family is chosen from its class's
    /// list and stored here; an old weapon keeps null and therefore keeps the exact family — the art,
    /// the name and the stat channel — it has always had. See <c>ItemNaming.WeaponFamilyIndex</c>.
    /// </remarks>
    public int? Family { get; init; }
}

public sealed record LootTuning
{
    /// <summary>How a class-locked drop picks its class. See <see cref="ClassRollTuning"/>.</summary>
    public ClassRollTuning ClassRoll { get; init; } = ClassRollTuning.Default;

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

    /// <summary>
    /// The slots the REGION is known for. Twice as likely to drop here; never exclusive.
    /// </summary>
    /// <remarks>
    /// Empty means "anywhere", which is what every region used to be: eight slots at uniform odds, so
    /// the only thing a place decided about its loot was the element. A tilt rather than a lock, because
    /// a region that could only drop two slots would force a tour of the map to finish a set.
    /// </remarks>
    public IReadOnlyList<ItemBaseType> FavouredTypes { get; init; } = Array.Empty<ItemBaseType>();

    /// <summary>
    /// The active champion's item class, so four in five class-locked drops are theirs. Null rolls
    /// uniformly across the five — a kill with no champion behind it, which only the tests make.
    /// </summary>
    public ItemClass? FavouredClass { get; init; }

    /// <summary>Null for an active kill. Set for an automated one.</summary>
    public int? AutomationStage { get; init; }

    public bool IsAutomated => AutomationStage is not null;
}

public static class LootSystem
{
    /// <summary>The eight wearable base types. Spelled out here so Loot keeps no dependency on Economy's Gear.</summary>
    /// <summary>How often a region's wearable drop comes from its favoured pool rather than the full set.</summary>
    public const double RegionFavourShare = 0.5;

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
            // 45% material; the rest across the wearable slots, tilted toward what this region is for.
            // Half of a region's wearables come from its favoured pool — so a forge really does hand you
            // weapons — and the other half stay uniform, so nothing is ever unobtainable in the wrong place.
            var baseType = rng.NextDouble() < 0.45
                ? ItemBaseType.Material
                : ctx.FavouredTypes.Count > 0 && rng.NextDouble() < RegionFavourShare
                    ? ctx.FavouredTypes[rng.Next(ctx.FavouredTypes.Count)]
                    : Wearables[rng.Next(Wearables.Length)];
            items.Add(Mint(baseType, RollRarity(ctx, rng, tuning), rng, tuning, ctx.Element, ctx.PowerTier, ctx.FavouredClass));
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
    private static ItemInstance Mint(ItemBaseType type, Rarity rarity, Random rng, LootTuning tuning, Source? element, int itemLevel,
                                     ItemClass? favouredClass = null)
    {
        // Draw order is id, prefix, class, family — the two new draws come LAST so every seeded
        // fixture that pinned an id or a prefix before classes existed still gets the same one.
        var id = $"itm_{rng.Next(int.MaxValue):x8}";
        // The PREFIX is rolled once, here at mint, and never changes — see GearTraits.RollPrefix.
        var prefix = Economy.GearTraits.RollPrefix(type, rng);
        // THE CLASS IS ROLLED HERE, ONCE, for the five class-locked slots and never for the rest — a
        // charm with a class would be a field the wear rule ignores, which is a lie waiting to be read.
        // A weapon's family comes from its class's own two shapes, so a WARDEN's bow cannot exist.
        var cls = ItemClasses.IsClassLocked(type) ? ItemClasses.Roll(favouredClass, rng, tuning.ClassRoll) : (ItemClass?)null;
        return new()
        {
            InstanceId = id,
            BaseType = type,
            Rarity = rarity,
            SellValue = tuning.RaritySellValue[(int)rarity],
            ItemLevel = Math.Max(1, itemLevel),
            TraitOverride = prefix,

            // Only wearables are attuned. An elemental lump of scrap would be noise — and it would let a
            // trio of materials carry an element into a hybrid, which is the one thing mixing must cost.
            // Materials and cores are the only inert types; every wearable slot attunes.
            Element = type is ItemBaseType.Material or ItemBaseType.CreatureCore ? null : element,
            Class = cls,
            Family = type == ItemBaseType.Weapon && cls is { } c ? ItemClasses.RollFamily(c, rng) : null,
        };
    }

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
