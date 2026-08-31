using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Economy;

/// <summary>
/// The five ITEM CLASSES — who can wear what.
/// </summary>
/// <remarks>
/// <para>
/// Playtest (post-pre-alpha round two): "Items must have classes. Not every character should be able
/// to wear every item." Before this, every drop fit every champion, so a bag was one undifferentiated
/// pile and switching champion changed nothing about what you reached for. A class is a small
/// partition of that pile: five groups, each with two champions, so a class-locked drop is still
/// wearable by two different builds and a player who prefers one champion is never stuck with the
/// other one's passive to use their gear.
/// </para>
/// <para>
/// Each class names the MASTERY road it was built for, so the roster, the mastery tree and the bag
/// all point the same way: a WARDEN's gear is for heavy blows, and both WARDEN champions lean WEIGHT.
/// The WANDERER class has no road, and its two champions are the two with no lean.
/// </para>
/// </remarks>
public enum ItemClass
{
    /// <summary>Heavy blows. Built for the WEIGHT road.</summary>
    Warden,

    /// <summary>Many targets. Built for the SPREAD road.</summary>
    Ranger,

    /// <summary>Front-loaded skills. Built for the TEMPO road.</summary>
    Mystic,

    /// <summary>Lasting. Built for the ENDURE road.</summary>
    Bulwark,

    /// <summary>Any road. The starter's class, and the one that wears every weapon shape.</summary>
    Wanderer,
}

/// <summary>One class's catalogue entry: its name, its sentence, its two champions and its weapon shapes.</summary>
public sealed record ItemClassDef
{
    public required ItemClass Class { get; init; }

    /// <summary>The word on the card — "WARDEN".</summary>
    public required string Name { get; init; }

    /// <summary>One plain sentence: "Skills that carry. Built for the RESONANCE road."</summary>
    public required string Description { get; init; }

    /// <summary>The mastery road this class was built for, or null for a class that favours none.</summary>
    /// <summary>The two champions who wear this class. Exactly two, always.</summary>
    public required IReadOnlyList<string> ChampionIds { get; init; }

    /// <summary>Indices into <see cref="ItemNaming.WeaponFamilies"/> — the weapon shapes this class can carry.</summary>
    public required IReadOnlyList<int> WeaponFamilies { get; init; }
}

/// <summary>
/// How a fresh drop picks its class. Data-driven per the coding standard; the number is a first cut.
/// </summary>
/// <remarks>
/// EIGHTY PERCENT YOUR OWN CLASS, not a hundred. A drop that was always yours would make the class a
/// label rather than a choice; a drop that was uniformly random across five classes would make four
/// in five drops junk and turn the bag into a salvage queue. Four in five drops fit the champion you
/// are playing, and the fifth is a reason to open the roster.
/// </remarks>
public sealed record ClassRollTuning
{
    public static readonly ClassRollTuning Default = new();

    /// <summary>The chance a class-locked drop belongs to the active champion's own class.</summary>
    public float OwnClassChance { get; init; } = 0.80f;
}

/// <summary>The class catalogue, and the one rule the whole system reduces to: <see cref="CanWear(ItemClass, ItemInstance)"/>.</summary>
public static class ItemClasses
{
    private const int Blade = 0, Bow = 1, Spear = 2, Scythe = 3;

    public static IReadOnlyList<ItemClassDef> All { get; } = new List<ItemClassDef>
    {
        new()
        {
            // WARDEN kept its NAME through the 2026-08-30 re-axe and changed what it is for. The class
            // named the WEIGHT road, and WEIGHT is gone — its hit-size content went to the skills
            // (design §9). A warden is still the one who holds a line, so the name survives the road:
            // it now points at RESONANCE, where a champion's skills are made to carry.
            Class = ItemClass.Warden, Name = "WARDEN",
            Description = "Skills that carry. Built for the RESONANCE road.",
            ChampionIds = new[] { "anvil", "tower" },
            WeaponFamilies = new[] { Blade, Spear },
        },
        new()
        {
            Class = ItemClass.Ranger, Name = "RANGER",
            Description = "Many targets. Built for the LOOT road.",
            // THE OATHBOUND, not THE QUIVER. The brief put THE QUIVER here, but THE QUIVER leans TEMPO
            // in the roster and a class whose champion walks a different road than the class is named
            // for would be the first lie on the card. THE OATHBOUND has no lean, so it fits any class,
            // and its Mark aptitude — a skill that makes every other skill hit — is the nearest thing
            // the off-tree pair has to "many targets".
            ChampionIds = new[] { "chorus", "oathbound" },
            WeaponFamilies = new[] { Bow, Spear },
        },
        new()
        {
            Class = ItemClass.Mystic, Name = "MYSTIC",
            Description = "Front-loaded skills. Built for the TEMPO road.",
            ChampionIds = new[] { "metronome", "quiver" },
            WeaponFamilies = new[] { Scythe, Bow },
        },
        new()
        {
            Class = ItemClass.Bulwark, Name = "BULWARK",
            Description = "Lasting. Built for the ENDURE road.",
            ChampionIds = new[] { "unbroken", "thornwall" },
            WeaponFamilies = new[] { Blade, Scythe },
        },
        new()
        {
            Class = ItemClass.Wanderer, Name = "WANDERER",
            Description = "Any road. Wears every weapon shape.",
            ChampionIds = new[] { CharacterRoster.StarterId, "magpie" },
            WeaponFamilies = new[] { Blade, Bow, Spear, Scythe },
        },
    };

    public static ItemClassDef Get(ItemClass cls) => All.First(d => d.Class == cls);

    /// <summary>The class's card word — "WARDEN".</summary>
    public static string NameOf(ItemClass cls) => Get(cls).Name;

    /// <summary>The class's people — "WARDENS", for "FIRST OF THE WARDENS".</summary>
    public static string PluralOf(ItemClass cls) => Get(cls).Name + "S";

    /// <summary>The asset key of the class's icon — <c>icon_class_warden</c>. Drawn with a coloured diamond behind it when the art is absent.</summary>
    public static string IconKey(ItemClass cls) => $"icon_class_{cls.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Is this slot locked to a class? Weapon and the four armour pieces are; charm, ring and focus
    /// are worn by anyone.
    /// </summary>
    /// <remarks>
    /// Jewellery stays universal so a good ring is never junk, and so the three slots the old
    /// three-slot loadout was built on keep the promise they made before classes existed.
    /// </remarks>
    public static bool IsClassLocked(ItemBaseType type) => type is
        ItemBaseType.Weapon or ItemBaseType.Helm or ItemBaseType.Chest or ItemBaseType.Gloves or ItemBaseType.Boots;

    /// <summary>A wearable slot anyone can wear — charm, ring, focus.</summary>
    public static bool IsUniversal(ItemBaseType type) => Gear.SlotFor(type) is not null && !IsClassLocked(type);

    /// <summary>
    /// The rule. True when the item is universal, or was made before classes (null), or is the
    /// wearer's own class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NEVER REJECT BY WEAPON FAMILY. A weapon minted inside a class carries a family from that class's
    /// own list by construction, so the check would be redundant on new weapons — and on a legacy
    /// weapon it would be wrong: a bow that dropped before classes existed keeps its bow art, its bow
    /// stat and its null class, and null means "anyone", including a WARDEN.
    /// </para>
    /// <para>
    /// A null class on a class-locked slot is LEGACY, never "unassigned". Every mint path sets the
    /// field; a null can only come from a save or a share code written before the field existed, and
    /// those items stay wearable by everyone so an update never takes a worn helm off a player.
    /// </para>
    /// </remarks>
    public static bool CanWear(ItemClass wearer, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Gear.SlotFor(item.BaseType) is null) return false;
        if (!IsClassLocked(item.BaseType)) return true;
        return item.Class is null || item.Class == wearer;
    }

    /// <summary>The same rule, asked of a champion.</summary>
    public static bool CanWear(Character wearer, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(wearer);
        return CanWear(wearer.Class, item);
    }

    /// <summary>The class a champion wears, from the roster.</summary>
    public static ItemClass ClassOf(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return character.Class;
    }

    /// <summary>Is this weapon family one the class can carry?</summary>
    public static bool FamilyAllowed(ItemClass cls, int family) => Get(cls).WeaponFamilies.Contains(family);

    /// <summary>
    /// Pick a fresh drop's class: the favoured one at <see cref="ClassRollTuning.OwnClassChance"/>,
    /// otherwise one of the other four uniformly. No favourite means uniform across all five.
    /// </summary>
    public static ItemClass Roll(ItemClass? favoured, Random rng, ClassRollTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(tuning);
        var all = All.Select(d => d.Class).ToList();
        if (favoured is not { } own) return all[rng.Next(all.Count)];
        if (rng.NextDouble() < tuning.OwnClassChance) return own;
        var others = all.Where(c => c != own).ToList();
        return others[rng.Next(others.Count)];
    }

    /// <summary>The same pick, from a hash rather than a roll — for the trader's stall, which never rolls.</summary>
    public static ItemClass Pick(ItemClass? favoured, uint hash, ClassRollTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var all = All.Select(d => d.Class).ToList();
        if (favoured is not { } own) return all[(int)(hash % (uint)all.Count)];
        // The low bits decide "own or other", the high bits decide which other — two questions off
        // one hash, not one bit read twice.
        if (hash % 10_000u < (uint)MathF.Round(tuning.OwnClassChance * 10_000f)) return own;
        var others = all.Where(c => c != own).ToList();
        return others[(int)((hash >> 16) % (uint)others.Count)];
    }

    /// <summary>A weapon family from the class's own list.</summary>
    public static int RollFamily(ItemClass cls, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var fams = Get(cls).WeaponFamilies;
        return fams[rng.Next(fams.Count)];
    }

    /// <summary>A weapon family from the class's own list, by hash — the trader's version.</summary>
    public static int PickFamily(ItemClass cls, uint hash)
    {
        var fams = Get(cls).WeaponFamilies;
        return fams[(int)(hash % (uint)fams.Count)];
    }

    /// <summary>The two champions' names joined — "THE ANVIL OR THE FALLING TOWER".</summary>
    public static string ChampionNames(ItemClass cls)
        => string.Join(" OR ", Get(cls).ChampionIds.Select(id => CharacterRoster.Get(id).Name));

    /// <summary>
    /// The one line under an item's name: "WARDEN GEAR", "ANY CLASS" for a universal slot, or
    /// "ANY CLASS · OLD MAKE" for a class-locked item from before classes existed.
    /// </summary>
    public static string ClassLine(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!IsClassLocked(item.BaseType)) return "ANY CLASS";
        return item.Class is { } c ? $"{NameOf(c)} GEAR" : "ANY CLASS · OLD MAKE";
    }

    /// <summary>
    /// Why a champion cannot wear an item, in the player's terms — "A WARDEN'S HELM — THE ANVIL OR
    /// THE FALLING TOWER CAN WEAR IT". Null when they can.
    /// </summary>
    public static string? WhyNot(Character wearer, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(wearer);
        ArgumentNullException.ThrowIfNull(item);
        if (CanWear(wearer, item) || item.Class is not { } c) return null;
        return $"A {NameOf(c)}'S {ItemNaming.TypeWord(item)} — {ChampionNames(c)} CAN WEAR IT";
    }

    /// <summary>
    /// The champion's own gear sentence — "WEARS WARDEN GEAR AND ANY CHARM, RING OR FOCUS".
    /// </summary>
    public static string WearsLine(ItemClass cls) => $"WEARS {NameOf(cls)} GEAR AND ANY CHARM, RING OR FOCUS";
}
