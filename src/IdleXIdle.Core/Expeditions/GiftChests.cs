using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Expeditions;

/// <summary>One item a gift chest always holds — spelled out in full, nothing rolled.</summary>
/// <param name="InstanceId">A FIXED id, so the item is the same one on every new game and every reload.</param>
/// <param name="Class">Who can wear it. Null on a universal slot.</param>
/// <param name="Family">A weapon's shape — an index into <see cref="ItemNaming.WeaponFamilies"/>. Null off a weapon.</param>
public sealed record GiftItemDef(
    string InstanceId, ItemBaseType BaseType, Rarity Rarity, ItemClass? Class, int? Family);

/// <summary>A chest whose contents are DECIDED, not rolled — the catalogue entry the chest points at.</summary>
public sealed record GiftChestDef
{
    /// <summary>The key a <see cref="Chest.Gift"/> carries — "welcome".</summary>
    public required string Key { get; init; }

    /// <summary>The headline on its dossier — "A WELCOME GIFT".</summary>
    public required string Title { get; init; }

    /// <summary>The card's first line, where a boss's chest prints its rarity floor — "a welcome gift".</summary>
    public required string CardPromise { get; init; }

    /// <summary>The card's second line, where a boss's chest prints its region's lean — "one plain blade".</summary>
    public required string CardContents { get; init; }

    /// <summary>The dossier's lines, in reading order. Every one is true of the contents below.</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>What it holds. Every open mints exactly these.</summary>
    public required IReadOnlyList<GiftItemDef> Items { get; init; }

    /// <summary>Materials it pays. Zero for a gift that is about the item, not the wallet.</summary>
    public int Materials { get; init; }
}

/// <summary>
/// GIFT CHESTS: the one kind of chest that does not gamble.
/// </summary>
/// <remarks>
/// <para>
/// Playtest 2026-08-26: "The VAULT tutorial talks about chests but there are none. Give a gift chest
/// at the start. The starter item should be the simplest weapon the starting character can wear."
/// A boss's chest rolls its contents at open (<see cref="Chests.Open"/>) — that is the whole point of
/// a chest. A gift is the opposite: the player is being HANDED something, and a hand-out that might be
/// a helm is not a hand-out, it is a smaller gamble. So a gift's contents are a catalogue entry, read
/// and minted the same way on every new game, and its dossier says exactly what it holds.
/// </para>
/// <para>
/// The welcome gift is derived from the roster, not written out: the starter's own gear class, and
/// the first weapon shape that class carries. If the starter ever changes, the gift follows. Common,
/// tier one, no prefix — the plainest weapon the game can mint — because the gift's job is to make
/// the VAULT's first visit true and to let the first lessons (open a chest, wear what dropped) be
/// performed in the first minute, not to skip the loot curve.
/// </para>
/// </remarks>
public static class GiftChests
{
    /// <summary>The key of the one gift a new game is seeded with.</summary>
    public const string WelcomeKey = "welcome";

    /// <summary>The new game's welcome gift: the starter champion's plainest weapon.</summary>
    public static GiftChestDef Welcome { get; } = BuildWelcome();

    private static GiftChestDef BuildWelcome()
    {
        var starter = CharacterRoster.Get(CharacterRoster.StarterId);
        var cls = starter.Class;
        // The FIRST shape the class carries — the class lists its shapes plainest first (a WANDERER's
        // is the blade), so "the simplest weapon the starter can wear" is a lookup, not a choice.
        var family = ItemClasses.Get(cls).WeaponFamilies[0];
        var shape = ItemNaming.WeaponFamilies[family].ToUpperInvariant();
        return new GiftChestDef
        {
            Key = WelcomeKey,
            Title = "A WELCOME GIFT",
            CardPromise = "a welcome gift",
            CardContents = $"one plain {shape.ToLowerInvariant()}",
            Lines = new[]
            {
                $"One plain {shape} for {starter.Name}.",
                "Common, tier 1, nothing hidden.",
                "Every other chest rolls its contents when opened.",
            },
            Items = new[]
            {
                new GiftItemDef("itm_gift_welcome_weapon", ItemBaseType.Weapon, Rarity.Common, cls, family),
            },
            Materials = 0,
        };
    }

    /// <summary>The catalogue, by key.</summary>
    public static IReadOnlyList<GiftChestDef> All { get; } = new[] { Welcome };

    /// <summary>The gift a key names, or null for a rolled chest (a null or unknown key).</summary>
    /// <remarks>
    /// Lenient on purpose, like every name-keyed field in the save: a chest whose gift key this build
    /// does not know opens as the ordinary Common chest its grade and tier describe, never as a crash.
    /// </remarks>
    public static GiftChestDef? Get(string? key)
        => key is null ? null : All.FirstOrDefault(g => string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The welcome gift as a chest — Common, tier 1, from the starting region.</summary>
    /// <param name="element">The region's element, so the weapon reads like a drop from there.</param>
    /// <param name="region">Where it "was won". Null is honest too — a gift comes from nowhere.</param>
    public static Chest WelcomeChest(Source? element, string? region) => new()
    {
        Rarity = Rarity.Common,
        Tier = 1,
        Element = element,
        Region = region,
        Gift = WelcomeKey,
    };

    /// <summary>
    /// Everything a NEW game starts with in its vault. One welcome chest, from the starting region.
    /// </summary>
    /// <remarks>
    /// Read by the new-game seed and by nothing else: an existing save is never handed a gift
    /// retroactively — its vault is whatever it saved.
    /// </remarks>
    public static IReadOnlyList<Chest> NewGameChests()
    {
        var home = Regions.All.First(r => r.Id == VerdantHollow.RegionId);
        return new[] { WelcomeChest(home.Theme, home.Id) };
    }

    /// <summary>
    /// Open a gift: mint exactly its catalogue items. Deterministic — no random source is even taken.
    /// </summary>
    public static ChestReward Open(Chest chest, GiftChestDef gift, LootTuning loot)
    {
        ArgumentNullException.ThrowIfNull(chest);
        ArgumentNullException.ThrowIfNull(gift);
        ArgumentNullException.ThrowIfNull(loot);

        var items = gift.Items.Select(def => new ItemInstance
        {
            InstanceId = def.InstanceId,
            BaseType = def.BaseType,
            Rarity = def.Rarity,
            SellValue = loot.RaritySellValue[(int)def.Rarity],
            ItemLevel = Math.Max(1, chest.Tier),
            // Wearables attune to the chest's element like any regional drop; a gift with no element is plain.
            Element = Gear.SlotFor(def.BaseType) is null ? null : chest.Element,
            // Class only on a class-locked slot, family only on a weapon — the same shape every mint keeps.
            Class = ItemClasses.IsClassLocked(def.BaseType) ? def.Class : null,
            Family = def.BaseType == ItemBaseType.Weapon ? def.Family : null,
            // No prefix: TraitOverride stays null, which the naming reads as "plain".
            TraitOverride = null,
        }).ToList();

        return new ChestReward(gift.Materials, items);
    }
}
