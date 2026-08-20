using System;
using System.Collections.Generic;
using System.Globalization;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>
/// The GEZGİN TÜCCAR — a weekly stall whose goods are the same for every player, priced as a
/// material sink.
/// </summary>
/// <remarks>
/// <para>
/// The one future-content direction the designer kept (2026-08-20): shared-world FEEL with zero
/// networking. Stock IDENTITY — slot, rarity, element, prefix, even the gem's stat — derives from
/// the ISO week stamp alone, so two players comparing stalls are talking about the same goods.
/// Only the ITEM LEVEL is minted at the buyer's own progression, so the stall is never junk to a
/// veteran and never a power spike for a novice.
/// </para>
/// <para>
/// Prices are steep and paid in MATERIALS, deliberately: the trader is a SINK. It strengthens the
/// forge economy the game already runs on, where the player market it replaced would have bypassed
/// it (the Diablo 3 auction-house lesson, recorded in the panel review of 2026-08-20).
/// </para>
/// <para>
/// Determinism note: identity fields derive from a per-field FNV hash with an avalanche finisher,
/// NOT from <c>System.Random</c>. Measured: certain week seeds gave the legacy Knuth generator a
/// degenerate opening (draws 2,2,3,3,2,2,2,2 — every armour piece the same prefix), and a hash is
/// also immune to runtime changes, which <c>Random(seed)</c> is not guaranteed to be. Nothing here
/// reads the clock; the HOST derives the week stamp and passes it in.
/// </para>
/// </remarks>
public static class WanderingTrader
{
    public const int StallSize = 4;

    /// <summary>The wearable slots the stall draws from — everything a hunter can put on.</summary>
    private static readonly ItemBaseType[] Wearables =
    {
        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Helm,
        ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
    };

    /// <summary>ISO-week stamp, year*100 + week (e.g. 202634). The host owns the clock, not Core.</summary>
    public static int WeekStamp(DateTime utcNow)
        => ISOWeek.GetYear(utcNow) * 100 + ISOWeek.GetWeekOfYear(utcNow);

    /// <summary>
    /// This week's stall: three wearables climbing Rare → Epic → Legendary, and one Epic stat gem.
    /// Identity comes from the week; <paramref name="itemLevel"/> comes from the buyer.
    /// </summary>
    public static List<ItemInstance> Stock(int weekStamp, int itemLevel, LootTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var level = Math.Max(1, itemLevel);

        var stock = new List<ItemInstance>(StallSize);
        var rarities = new[] { Rarity.Rare, Rarity.Epic, Rarity.Legendary };
        for (var slot = 0; slot < rarities.Length; slot++)
        {
            var type = Wearables[(int)(Hash(weekStamp, slot, "type") % (uint)Wearables.Length)];
            var pool = Gear.SlotFor(type) is { } gearSlot ? GearTraits.PoolFor(gearSlot) : null;
            stock.Add(new ItemInstance
            {
                // Deterministic id: the same stall for everyone, and buying is idempotent — a
                // re-added week can never mint a second copy under a fresh id.
                InstanceId = $"trader_{weekStamp}_{slot}",
                BaseType = type,
                Rarity = rarities[slot],
                // A TENTH of the drop-loot value: the trader knows what his goods are worth.
                // At full value the Legendary was a CRYSTAL PUMP — 60 Core + 18 Crystal bought a
                // salvage of 80 Crystal (160 with a charter), a guaranteed weekly mint of the
                // premium material that inverted this stall's whole sink design. At /10 the best
                // charter salvage returns less than the Crystal line of the price alone.
                SellValue = Math.Max(1, tuning.RaritySellValue[(int)rarities[slot]] / 10),
                ItemLevel = level,
                // Same mint SHAPE as LootSystem.Mint (prefix once, immutable; element on every
                // wearable) — but hash-picked, never rolled: the stall never sells plain.
                TraitOverride = pool is null ? null
                    : pool[(int)(Hash(weekStamp, slot, "prefix") % (uint)pool.Length)],
                Element = (Source)(Hash(weekStamp, slot, "element") % 6),
            });
        }

        // The gem: its STAT derives from the deterministic id (GemCraft.StatOf reads the id hash),
        // so "this week's stall has a TEMPO gem" is the same sentence on every machine.
        stock.Add(new ItemInstance
        {
            InstanceId = $"trader_{weekStamp}_3",
            BaseType = ItemBaseType.Gem,
            Rarity = Rarity.Epic,
            SellValue = Math.Max(1, tuning.RaritySellValue[(int)Rarity.Epic] / 10),   // same rule
            ItemLevel = level,
        });

        return stock;
    }

    /// <summary>FNV-1a over (week, slot, field) with a murmur-style finisher — the low bits of raw
    /// FNV are weak, and a modulo reads exactly those.</summary>
    private static uint Hash(int weekStamp, int slot, string field)
    {
        var h = 2166136261u;
        h = (h ^ (uint)weekStamp) * 16777619u;
        h = (h ^ (uint)slot) * 16777619u;
        foreach (var c in field) h = (h ^ c) * 16777619u;
        h ^= h >> 15; h *= 2246822519u;
        h ^= h >> 13; h *= 3266489917u;
        h ^= h >> 16;
        return h;
    }

    /// <summary>What one offer costs, in materials. Steep on purpose — the stall is a sink.</summary>
    public static IReadOnlyList<(Material Material, int Amount)> PriceOf(
        ItemInstance offer, TraderTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(tuning);

        if (offer.BaseType == ItemBaseType.Gem)
            return new[] { (Material.Essence, tuning.GemEssence) };

        return offer.Rarity switch
        {
            Rarity.Legendary => new[]
            {
                (Material.Core, tuning.LegendaryCore), (Material.Crystal, tuning.LegendaryCrystal),
            },
            Rarity.Epic => new[]
            {
                (Material.Essence, tuning.EpicEssence), (Material.Core, tuning.EpicCore),
            },
            _ => new[]
            {
                (Material.Scrap, tuning.RareScrap), (Material.Essence, tuning.RareEssence),
            },
        };
    }

    /// <summary>Can this hunter pay the whole price? All-or-nothing — no partial spends.</summary>
    public static bool CanAfford(Hunter hunter, ItemInstance offer, TraderTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        foreach (var (m, amount) in PriceOf(offer, tuning))
            if (hunter.MaterialOf(m) < amount)
                return false;
        return true;
    }

    /// <summary>
    /// Pay for the offer. True only when every line was paid; a failed line refunds nothing because
    /// nothing is spent before <see cref="CanAfford"/> passes.
    /// </summary>
    public static bool TryBuy(Hunter hunter, ItemInstance offer, TraderTuning tuning)
    {
        if (!CanAfford(hunter, offer, tuning)) return false;
        foreach (var (m, amount) in PriceOf(offer, tuning))
            hunter.SpendMaterial(m, amount);
        return true;
    }
}

/// <summary>The stall's price card. Data-driven per the coding standard; numbers are a first cut
/// and carry the same playtest debt as the rest of the economy.</summary>
public sealed record TraderTuning
{
    public static readonly TraderTuning Default = new();

    public int RareScrap { get; init; } = 350;
    public int RareEssence { get; init; } = 40;
    public int EpicEssence { get; init; } = 120;
    public int EpicCore { get; init; } = 30;
    public int LegendaryCore { get; init; } = 60;
    public int LegendaryCrystal { get; init; } = 18;
    public int GemEssence { get; init; } = 90;
}
