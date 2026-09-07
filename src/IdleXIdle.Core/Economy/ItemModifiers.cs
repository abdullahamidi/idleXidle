using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Economy;

/// <summary>Which layer of an item a numeric modifier comes from.</summary>
public enum ModifierLayer
{
    /// <summary>What the item is by birth — a blade hits harder, gloves strike, boots quicken (<see cref="ItemFamilies.BonusOf"/>).</summary>
    BuiltIn,

    /// <summary>A rolled bonus, counted by rarity (<see cref="ItemAffixes.Of"/>).</summary>
    Affix,

    /// <summary>One side of the prefix's trade — HEAVY's harder hit, or its slower skill clock (<see cref="GearTraits.ModsOf"/>).</summary>
    Prefix,

    /// <summary>A stat gem set into the item (<see cref="GemCraft"/>).</summary>
    Gem,
}

/// <summary>The unit a stat's magnitude is written in — the difference between +12 HEALTH and +12% HEALTH.</summary>
public enum ModifierUnit
{
    /// <summary>A share of a multiplicative channel: a magnitude of 0.04 reads +4%.</summary>
    Percent,

    /// <summary>Percentage POINTS of a chance: a magnitude of 1.5 reads +1.5%.</summary>
    Points,

    /// <summary>A flat amount: a magnitude of 8 reads +8.</summary>
    Flat,
}

/// <summary>
/// One typed numeric modifier on an item: what it changes, by how much, from which layer.
/// </summary>
/// <remarks>
/// The magnitude is in the stat's OWN unit — the same number the fight reads (see
/// <see cref="ItemModifiers.UnitOf"/>) — and it is SIGNED: a prefix's drawback is a negative modifier
/// on the channel it costs, not a separate kind of fact.
/// </remarks>
public readonly record struct ItemModifier(ModifierLayer Layer, AffixStat Stat, float Magnitude)
{
    /// <summary>How the magnitude is written.</summary>
    public ModifierUnit Unit => ItemModifiers.UnitOf(Stat);

    /// <summary>A cost rather than a bonus — the second half of a trade.</summary>
    public bool IsDrawback => Magnitude < 0f;
}

/// <summary>
/// THE ONE conversion from a typed item modifier to the line a player reads.
/// </summary>
/// <remarks>
/// <para>
/// Every item screen — the item card, the trader's compact card, the Gear and Forge inspectors, the
/// upgrade preview, the socket rows — prints its numbers through <see cref="Line"/> and
/// <see cref="Value"/>, or through a Core helper that does (<see cref="ItemAffixes.Describe"/>,
/// <see cref="GemCraft.Grant"/>, <see cref="GearTraits.EffectOf"/>). A screen never formats a
/// magnitude itself, so a new stat cannot ship with a number and no word, and a new screen cannot
/// re-invent "+560% DEFENCE" for a flat 5.6.
/// </para>
/// <para>
/// <b>A modifier line names the STAT the number moves, never the slot or the shape of the item.</b>
/// The built-in bonus used to print as <c>"GLOVES  +2%"</c> — the slot word beside a bare number —
/// which is a line the player cannot finish ("+2% what?") without knowing that gloves are the slot
/// whose identity is damage. The equipment slot is not the effect; the built-in's stat is real data
/// on the item (<see cref="ItemFamilies.BonusOf"/>) and that is the word printed. Nothing here infers
/// a stat from a slot name.
/// </para>
/// <para>
/// <b>The unit is the stat's own.</b> Damage, health, skill rate and loot are multiplicative channels
/// and read as percentages; critical chance is percentage points and also reads with a % sign;
/// defence is flat. <c>+12 HEALTH</c> and <c>+12% HEALTH</c> are two different facts and the formatter
/// keeps them apart by the stat, not by the caller.
/// </para>
/// </remarks>
public static class ItemModifiers
{
    /// <summary>The unit a stat's magnitude is written in.</summary>
    public static ModifierUnit UnitOf(AffixStat stat) => stat switch
    {
        AffixStat.Crit => ModifierUnit.Points,
        AffixStat.Defense => ModifierUnit.Flat,
        _ => ModifierUnit.Percent,
    };

    /// <summary>The stat's player-facing word — "DAMAGE", "SKILL RATE", "CRITICAL CHANCE". One owner: <see cref="ItemAffixes.StatWord"/>.</summary>
    public static string Word(AffixStat stat) => ItemAffixes.StatWord(stat);

    /// <summary>
    /// The signed number in its unit — "+4%", "-20%", "+1.5%", "+8". The half of a line that goes in a
    /// value column; a screen that prints it alone MUST print <see cref="Word"/> in the label column.
    /// </summary>
    /// <param name="precise">One more decimal, for a before → after comparison a single upgrade rung would otherwise not move.</param>
    /// <remarks>
    /// Invariant culture, explicitly: the copy is English and a decimal comma in "+1,5%" is a typo to
    /// the player who reads it, whatever the machine's locale. The game pins the invariant culture at
    /// start-up; this pins it at the one site that prints an item number, so a test host or a tool
    /// thread on another culture prints the same string the game does.
    /// </remarks>
    public static string Value(AffixStat stat, float magnitude, bool precise = false)
    {
        var c = CultureInfo.InvariantCulture;
        return UnitOf(stat) switch
        {
            ModifierUnit.Points => precise ? magnitude.ToString("+0.00;-0.00;+0.00", c) + "%" : magnitude.ToString("+0.0;-0.0;+0.0", c) + "%",
            ModifierUnit.Flat => precise ? magnitude.ToString("+0.0;-0.0;+0.0", c) : magnitude.ToString("+0;-0;+0", c),
            _ => precise ? (magnitude * 100f).ToString("+0.0;-0.0;+0.0", c) + "%" : (magnitude * 100f).ToString("+0;-0;+0", c) + "%",
        };
    }

    /// <summary>The signed number of one modifier — see <see cref="Value(AffixStat, float, bool)"/>.</summary>
    public static string Value(ItemModifier m, bool precise = false) => Value(m.Stat, m.Magnitude, precise);

    /// <summary>The whole line — "+4% DAMAGE", "-20% SKILL RATE", "+8 DEFENCE". Number first, then the stat it moves.</summary>
    public static string Line(AffixStat stat, float magnitude, bool precise = false)
        => $"{Value(stat, magnitude, precise)} {Word(stat)}";

    /// <summary>The whole line of one modifier — see <see cref="Line(AffixStat, float, bool)"/>.</summary>
    public static string Line(ItemModifier m, bool precise = false) => Line(m.Stat, m.Magnitude, precise);

    /// <summary>Several modifiers on one row — "+12% DAMAGE  ·  -20% SKILL RATE". Empty for none.</summary>
    public static string Join(IEnumerable<ItemModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        return string.Join("  ·  ", modifiers.Select(m => Line(m)));
    }

    /// <summary>The built-in bonus, as a modifier — none for an unwearable.</summary>
    public static IReadOnlyList<ItemModifier> BuiltIn(ItemInstance? item)
        => ItemFamilies.BonusOf(item) is { } fam
            ? new[] { new ItemModifier(ModifierLayer.BuiltIn, fam.Stat, fam.Magnitude) }
            : Array.Empty<ItemModifier>();

    /// <summary>The rolled affixes, as modifiers, in roll order.</summary>
    public static IReadOnlyList<ItemModifier> Affixes(ItemInstance? item)
        => ItemAffixes.Of(item).Select(a => new ItemModifier(ModifierLayer.Affix, a.Stat, a.Magnitude)).ToList();

    /// <summary>
    /// The prefix's trade, BOTH sides, in <see cref="GearTraits.Channels"/> order: HEAVY is a positive
    /// DAMAGE modifier and a negative SKILL RATE one. Empty for a plain drop, or a channel that did not move.
    /// </summary>
    /// <remarks>
    /// A prefix's numbers are the item's real <see cref="GearTraits.ModsOf"/> — rarity and item level
    /// folded in — so the line deepens as the piece is refined, exactly as the fight does.
    /// </remarks>
    public static IReadOnlyList<ItemModifier> Prefix(ItemInstance? item)
    {
        if (GearTraits.TraitOf(item) is null) return Array.Empty<ItemModifier>();
        var m = GearTraits.ModsOf(item);
        var parts = new List<ItemModifier>(4);
        void Channel(AffixStat stat, float multiplier)
        {
            var share = multiplier - 1f;
            if (MathF.Abs(share * 100f) < 0.5f) return;   // a rounding artefact is not an effect
            parts.Add(new ItemModifier(ModifierLayer.Prefix, stat, share));
        }
        Channel(AffixStat.Damage, m.Damage);
        Channel(AffixStat.Health, m.Health);
        Channel(AffixStat.SkillRate, m.SkillRate);
        Channel(AffixStat.Haul, m.Haul);
        return parts;
    }

    /// <summary>The gems set into the item, as modifiers, in socket order.</summary>
    public static IReadOnlyList<ItemModifier> Gems(ItemInstance? item)
        => item is null
            ? Array.Empty<ItemModifier>()
            : item.Gems.Select(Gem).ToList();

    /// <summary>One stat gem as the modifier it grants.</summary>
    public static ItemModifier Gem(ItemInstance gem)
    {
        ArgumentNullException.ThrowIfNull(gem);
        return new ItemModifier(ModifierLayer.Gem, GemCraft.StatOf(gem), GemCraft.Magnitude(gem));
    }

    /// <summary>
    /// Every numeric modifier the item carries, in the order the card reads them: the built-in, the
    /// rolled affixes, the prefix's trade, the set gems. Enchantments are not here — an enchant is a
    /// trigger with a magnitude, not a stat, and it says what it does in its own sentence
    /// (<see cref="Enchantment.Blurb"/>).
    /// </summary>
    public static IReadOnlyList<ItemModifier> Of(ItemInstance? item)
    {
        var all = new List<ItemModifier>();
        all.AddRange(BuiltIn(item));
        all.AddRange(Affixes(item));
        all.AddRange(Prefix(item));
        all.AddRange(Gems(item));
        return all;
    }
}
