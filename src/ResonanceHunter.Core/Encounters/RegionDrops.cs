using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Encounters;

/// <summary>
/// What a particular place drops, and how often.
/// </summary>
/// <remarks>
/// <para>
/// A region already decided the ELEMENT of its loot and nothing else, so every region dropped the same
/// eight slots at the same eight-way uniform odds. That makes "where do I farm" a question with no
/// answer — the map is a difficulty ladder and never a destination, and the playtest asked for exactly
/// this: <i>the place you clear should have its own drops and its own rates.</i>
/// </para>
/// <para>
/// The bias is a TILT, never a lock. A region that could only drop two slots would force a player to
/// tour the map to finish a set, which is the opposite of letting them choose where to be — so a
/// favoured slot is roughly twice as likely here as anywhere else, and everything else still drops.
/// The theme decides which: a forge drops what a forge makes, a place of bodies drops what covers one.
/// </para>
/// </remarks>
public sealed record RegionDropProfile
{
    /// <summary>The slots this place is known for. Twice as likely here; never exclusive.</summary>
    public required IReadOnlyList<ItemBaseType> Favoured { get; init; }

    /// <summary>One line about the place and what it drops, as the chest dossier reads it.</summary>
    /// <remarks>
    /// The MAP no longer prints this — it lists <see cref="FavouredNames"/> one per line instead,
    /// which is what a player comparing two regions actually reads. The chest screen still wants a
    /// sentence under a chest's region, so the sentence lives on.
    /// </remarks>
    public required string Blurb { get; init; }

    /// <summary>
    /// Extra rarity tilt from the region's place in the chain, in the same units as
    /// <see cref="KillContext.LootTiltPercent"/>.
    /// </summary>
    /// <remarks>
    /// The later a region sits on the conquest chain the better its floor, so pushing to a new place is
    /// a loot decision as well as a difficulty one. It is deliberately small next to what DEPTH buys —
    /// going deeper in a region you can survive should still beat visiting one you cannot.
    /// </remarks>
    public required float RarityTilt { get; init; }

    /// <summary>
    /// The favoured slots as the map lists them, one plain name per slot, in the profile's order.
    /// </summary>
    /// <remarks>
    /// The map used to print <see cref="Blurb"/> here and the owner asked for the drops listed
    /// instead — a player comparing two regions is comparing slots, and a column is read in one glance
    /// where a sentence is not. The names live in Core so a test can hold them plain and inside the
    /// font gate.
    /// </remarks>
    public IReadOnlyList<string> FavouredNames => Favoured.Select(RegionDrops.PlainName).ToList();
}

/// <summary>Each region's drop profile, keyed by id.</summary>
public static class RegionDrops
{
    private static readonly ItemBaseType[] Weapons = { ItemBaseType.Weapon, ItemBaseType.AbilityFocus };
    private static readonly ItemBaseType[] Armour = { ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Boots };
    private static readonly ItemBaseType[] Trinkets = { ItemBaseType.Charm, ItemBaseType.Ring };
    private static readonly ItemBaseType[] Hands = { ItemBaseType.Gloves, ItemBaseType.AbilityFocus };

    private static readonly Dictionary<string, RegionDropProfile> Profiles = new()
    {
        // The order of the chain is the order of the tilts: 0, 4, 8, 12, 16, 20.
        ["verdant_hollow"] = new()
        {
            Favoured = Armour, RarityTilt = 0f,
            Blurb = "A living, growing place. More armour drops here.",
        },
        ["cinderworks"] = new()
        {
            Favoured = Weapons, RarityTilt = 4f,
            Blurb = "A burning forge. More weapons and focuses drop here.",
        },
        ["umbral_reach"] = new()
        {
            Favoured = Trinkets, RarityTilt = 8f,
            Blurb = "Nothing here is solid. More charms and rings drop here.",
        },
        ["marrow_wastes"] = new()
        {
            Favoured = Armour, RarityTilt = 12f,
            Blurb = "A place of bones. More armour drops here.",
        },
        ["still_archive"] = new()
        {
            Favoured = Hands, RarityTilt = 16f,
            Blurb = "A silent library. More gloves and focuses drop here.",
        },
        ["pale_choir"] = new()
        {
            Favoured = Trinkets, RarityTilt = 20f,
            Blurb = "Voices without bodies. More charms and rings drop here.",
        },
    };

    /// <summary>The profile for a region, or a neutral one for anywhere unlisted.</summary>
    /// <remarks>
    /// Falls back rather than throwing, because a region added to the catalogue and not to this table
    /// should drop ordinary loot rather than crash a fight. <see cref="ProfileTest"/>'s coverage check
    /// is what stops that fallback from being where a real region quietly ends up.
    /// </remarks>
    public static RegionDropProfile For(string? regionId)
        => regionId is not null && Profiles.TryGetValue(regionId, out var p)
            ? p
            : new RegionDropProfile { Favoured = Array.Empty<ItemBaseType>(), RarityTilt = 0f, Blurb = "" };

    /// <summary>Every region id this table covers — for the test that keeps it in step with the world.</summary>
    public static IReadOnlyCollection<string> CoveredIds => Profiles.Keys;

    /// <summary>
    /// A gear slot as the map's DROPS list names it: the plural, in capitals, in the word the rest of
    /// the game uses for that slot.
    /// </summary>
    public static string PlainName(ItemBaseType slot) => slot switch
    {
        ItemBaseType.Weapon => "WEAPONS",
        ItemBaseType.AbilityFocus => "FOCUSES",
        ItemBaseType.Helm => "HELMS",
        ItemBaseType.Chest => "CHEST ARMOUR",
        ItemBaseType.Gloves => "GLOVES",
        ItemBaseType.Boots => "BOOTS",
        ItemBaseType.Charm => "CHARMS",
        ItemBaseType.Ring => "RINGS",
        _ => slot.ToString().ToUpperInvariant(),
    };
}
