using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Expeditions;

/// <summary>
/// A reward CONTAINER a boss drops, opened later for its contents.
/// </summary>
/// <remarks>
/// <para>
/// The player asked for chests: the farm is not a material source, materials come from monsters, from the
/// chests monsters drop, and from dismantling — and opening a chest is a mechanic players enjoy for its
/// own sake ("kutu açma… dopamin sağladığı için güzel"). It also answers a real problem the loot rework
/// created: as item count grows, a firehose of items straight into the bag is noise. A chest gates the
/// haul behind one satisfying click — the anticipation of the roll, not the roll itself.
/// </para>
/// <para>
/// A chest carries its <see cref="Rarity"/> (the grade you see BEFORE opening — the hook) and the
/// <see cref="Tier"/> + <see cref="Element"/> that decide what is inside. Contents are rolled at OPEN,
/// never at drop, so the reveal is a genuine uncertainty rather than a delayed reading of a known result.
/// </para>
/// </remarks>
public sealed record Chest
{
    /// <summary>The chest's grade — a richer grade means RARER items (never more of them). Rolled at drop.</summary>
    public required Rarity Rarity { get; init; }

    /// <summary>The loot tier its items roll at — depth folded in, exactly as a boss drop's power tier.</summary>
    public required int Tier { get; init; }

    /// <summary>The region's element, so the items inside are attuned like any other regional drop.</summary>
    public Source? Element { get; init; }
}

/// <summary>What a chest paid out when opened: materials, and the items to land in the bag.</summary>
public sealed record ChestReward(int Materials, IReadOnlyList<ItemInstance> Items);

/// <summary>Knobs for what chests drop and what they pay. Data-driven so the numbers live in one place.</summary>
/// <remarks>
/// <b>Grade buys QUALITY, not QUANTITY.</b> Player direction: a better chest must mean VALUABLE items, not
/// MORE of them — "çok fazla çıkarsa oyuncu eşyalarla ve materyallerle boğulur". So the item count and the
/// material payout stay in a tight, roughly-constant band whatever the grade; only the item RARITY climbs
/// with the grade. The exact counts are a deliberate polish-pass knob, which is why they live here.
/// </remarks>
public sealed record ChestTuning
{
    /// <summary>Materials a chest pays before the small spread and depth nudge. GRADE never changes this.</summary>
    public int BaseMaterials { get; init; } = 12;

    /// <summary>Random spread on the payout: base + [0, this]. Keeps opens "mostly the same" (çoğunlukla aynı).</summary>
    public int MaterialsVariance { get; init; } = 6;

    /// <summary>One extra material per this many tiers of depth — a mild nudge for pushing, never a flood.</summary>
    public int TiersPerBonusMaterial { get; init; } = 4;

    /// <summary>
    /// How much the chest's grade tilts the RARITY of its items. This is the whole payoff of a good chest —
    /// valuable loot, not a bigger pile.
    /// </summary>
    public float QualityPerRarityPower { get; init; } = 0.14f;

    /// <summary>Items a chest always carries — the floor of the tight count.</summary>
    public int MinItems { get; init; } = 1;

    /// <summary>Chance of ONE extra item on top. Keeps it "mostly 1, sometimes 2" — never a pile, any grade.</summary>
    public float ExtraItemChance { get; init; } = 0.35f;

    /// <summary>
    /// The MINIMUM item rarity each grade GUARANTEES (indexed Common..Legendary). A good chest cannot
    /// disappoint — a Legendary always yields at least an Epic, an Epic at least a Rare.
    /// </summary>
    /// <remarks>
    /// This is the "değerli eşyalar" promise made reliable, and it is what loot-box design calls BOUNDED
    /// unpredictability: you know a Legendary chest gives a great item, the only question is HOW great. An
    /// unbounded roll — where a Legendary chest can still cough up a Common — reads as a betrayal and
    /// breaks the loop. The quality tilt still pushes ABOVE this floor; the floor just removes the feel-bad.
    /// </remarks>
    public IReadOnlyList<Rarity> ItemFloorByGrade { get; init; } =
        new[] { Rarity.Common, Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic };

    /// <summary>Chest-grade weights at tier 0 (Common..Legendary). Common dominates a shallow drop.</summary>
    public IReadOnlyList<double> BaseRarityWeight { get; init; } = new[] { 100.0, 40.0, 15.0, 4.0, 0.5 };

    /// <summary>Per-tier shift added to each grade's weight — depth pushes the GRADE roll upward.</summary>
    public IReadOnlyList<double> TierRarityShift { get; init; } = new[] { -6.0, 1.0, 2.0, 1.2, 0.5 };

    /// <summary>
    /// Chance a felled boss actually drops a chest. LOW on purpose — a chest is an event, not a paycheck.
    /// </summary>
    /// <remarks>
    /// Every boss used to drop one, guaranteed, which buried the player in chests (playtest: "too many
    /// chests drop"). A drop RATE makes the drop mean something: most bosses give nothing, and the one that
    /// does is a moment. The chance rises a little with depth — farming a deeper region pays out somewhat
    /// more often, "seviyeye göre" — but is capped so it never becomes a guarantee again.
    /// </remarks>
    public float BaseDropChance { get; init; } = 0.30f;
    public float DropChancePerTier { get; init; } = 0.015f;
    public float MaxDropChance { get; init; } = 0.60f;

    public static ChestTuning Default { get; } = new();
}

/// <summary>Dropping chests, and cracking them open.</summary>
public static class Chests
{
    /// <summary>
    /// Roll a chest's GRADE for a drop at a given tier. Deeper tiers push the odds toward the top.
    /// </summary>
    /// <remarks>
    /// Same philosophy as the item drop it replaces: depth buys RARITY, not quantity. A wave-40 boss
    /// drops a chest that is likelier to be Legendary, not a bigger pile of chests.
    /// </remarks>
    public static Rarity RollRarity(int tier, Random rng, ChestTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        tuning ??= ChestTuning.Default;

        var t = Math.Max(0, tier);
        var w = new double[5];
        for (var i = 0; i < 5; i++)
            w[i] = Math.Max(0.0, tuning.BaseRarityWeight[i] + t * tuning.TierRarityShift[i]);

        var total = w.Sum();
        if (total <= 0) return Rarity.Common;   // deep enough that Common fell to zero and rounding bit

        var roll = rng.NextDouble() * total;
        var acc = 0.0;
        for (var i = 0; i < 5; i++)
        {
            acc += w[i];
            if (roll < acc) return (Rarity)i;
        }
        return Rarity.Legendary;
    }

    /// <summary>How likely a felled boss at this tier is to drop a chest at all. Low, rising with depth, capped.</summary>
    public static float DropChance(int tier, ChestTuning? tuning = null)
    {
        tuning ??= ChestTuning.Default;
        return Math.Min(tuning.MaxDropChance, tuning.BaseDropChance + Math.Max(0, tier) * tuning.DropChancePerTier);
    }

    /// <summary>Mint a chest a boss drops at this tier — grade rolled now, contents rolled at open.</summary>
    public static Chest RollDrop(int tier, Source? element, Random rng, ChestTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return new Chest { Rarity = RollRarity(tier, rng, tuning), Tier = Math.Max(1, tier), Element = element };
    }

    /// <summary>
    /// Crack a chest open: a TIGHT, roughly-constant amount of materials + items, rolled fresh.
    /// </summary>
    /// <remarks>
    /// <b>Grade buys rarity, not quantity.</b> The item count is a tight 1–2 (whatever
    /// <see cref="ExpeditionLoot.RollBoss"/> gives, the same for every grade) and materials sit in a small
    /// band nudged only by DEPTH — so a Legendary chest is exciting because its item is RARER, never
    /// because it buries you in loot. The grade feeds ONE thing: the quality tilt on that item.
    /// <para>
    /// Pure — it does not touch a stash or apply loot filters. The caller lands the items and spends the
    /// materials, exactly as the Forge's merge and reforge are pure and the screen owns the mutation.
    /// </para>
    /// </remarks>
    public static ChestReward Open(Chest chest, Random rng, LootTuning? loot = null, ChestTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(chest);
        ArgumentNullException.ThrowIfNull(rng);
        tuning ??= ChestTuning.Default;
        loot ??= LootTuning.Default;

        // Materials: a tight band, nudged only by depth. GRADE is deliberately absent — it must not pay
        // MORE, only rarer items below.
        var materials = tuning.BaseMaterials
                        + rng.Next(tuning.MaterialsVariance + 1)
                        + chest.Tier / Math.Max(1, tuning.TiersPerBonusMaterial);

        // Items: a tight, grade-independent count. RollBoss mints a variable pile (base + bonus rolls), so
        // the count is CAPPED here with Take — the grade must not leak into quantity. The grade feeds the
        // quality tilt (rarer on average) AND a guaranteed FLOOR (never below the grade's minimum), so a
        // good chest is reliably valuable — bounded reward, the loot-box rule for "generous, not exploitative".
        var quality = 1f + Gear.RarityPower(chest.Rarity) * tuning.QualityPerRarityPower;
        var itemCount = tuning.MinItems + (rng.NextDouble() < tuning.ExtraItemChance ? 1 : 0);
        var floor = tuning.ItemFloorByGrade[(int)chest.Rarity];

        // A chest's ITEMS are always GEAR. The chest already pays material CURRENCY above, so a Material-type
        // item would be redundant clutter and an anticlimactic reveal ("a Legendary chest gave me… a
        // material"). Material rolls are discarded; if the whole roll happened to be materials, one gear
        // piece is minted at the grade's floor so a chest is never "just materials".
        var gear = ExpeditionLoot.RollBoss(chest.Tier, quality, rng, loot, element: chest.Element)
            .Where(i => i.BaseType is not ItemBaseType.Material)
            .ToList();
        if (gear.Count == 0)
            gear.Add(MintGear(floor, chest.Element, rng, loot, chest.Tier));

        var items = gear.Take(itemCount).Select(i => Elevate(i, floor, loot)).ToList();

        return new ChestReward(materials, items);
    }

    private static readonly ItemBaseType[] GearTypes =
    {
        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus,
        ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
    };

    /// <summary>Mint one wearable at a set rarity — the guaranteed-gear fallback when a roll was all materials.</summary>
    private static ItemInstance MintGear(Rarity rarity, Source? element, Random rng, LootTuning loot, int tier) => new()
    {
        InstanceId = $"itm_{rng.Next(int.MaxValue):x8}",
        BaseType = GearTypes[rng.Next(GearTypes.Length)],
        Rarity = rarity,
        SellValue = loot.RaritySellValue[(int)rarity],
        Element = element,
        ItemLevel = Math.Max(1, tier),
    };

    /// <summary>Lift an item to the grade's rarity FLOOR if the roll came in below it — never lower it.</summary>
    /// <remarks>
    /// Rarity and sell value move together (sell value is a pure function of rarity), so both are bumped.
    /// Lifting rarity also grants the item its rarity-gated enchantment and stronger trait mods — a
    /// floor-elevated item is genuinely better, not just a recoloured one.
    /// </remarks>
    private static ItemInstance Elevate(ItemInstance item, Rarity floor, LootTuning loot)
        => item.Rarity >= floor
            ? item
            : item with { Rarity = floor, SellValue = loot.RaritySellValue[(int)floor] };
}
