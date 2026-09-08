using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Economy;

/// <summary>What kind of fact an item row states — the screen chooses a layout by it, never a word.</summary>
public enum ItemRowKind
{
    /// <summary>ITEM POWER — the marginal power rating of the piece. Value is the figure.</summary>
    Power,

    /// <summary>The comparison against what is worn: UPGRADE / DOWNGRADE / SIDEGRADE / WORN / THE SLOT IS EMPTY, or who can wear it.</summary>
    Verdict,

    /// <summary>The built-in bonus the piece carries by being what it is — a modifier row.</summary>
    BuiltIn,

    /// <summary>One rolled affix — a modifier row. <see cref="ItemDisplayRow.Index"/> is its roll index.</summary>
    Affix,

    /// <summary>The prefix's NAME and its plain-words direction. Followed by its <see cref="PrefixSide"/> rows.</summary>
    Prefix,

    /// <summary>One side of the prefix's trade — a modifier row; the cost side is <see cref="ItemDisplayRow.IsDrawback"/>.</summary>
    PrefixSide,

    /// <summary>The set-gem count: Label "SET GEMS", Value "2 OF 3".</summary>
    GemCount,

    /// <summary>One set gem — a modifier row; Label is the gem's name, the modifier its grant.</summary>
    Gem,

    /// <summary>The enchant: Label its name, Note its one canonical sentence.</summary>
    Enchant,

    /// <summary>What the enchant needs in the build, and whether the build has it. Tone says which.</summary>
    EnchantNeed,

    /// <summary>The weapon family's plain line — "A BOW LANDS CRITICAL HITS MORE OFTEN."</summary>
    Family,

    /// <summary>The element set: Label "NATURE SET", Value "3 OF 5 WORN".</summary>
    Set,
}

/// <summary>The row's meaning as a colour class — the screen maps it to an ink, never the other way round.</summary>
public enum ItemRowTone { Neutral, Bonus, Cost, Accent, Muted }

/// <summary>
/// One player-facing row of an item's card or inspector, with its meaning already decided.
/// </summary>
/// <param name="Kind">What the row states.</param>
/// <param name="Label">The left column: a stat word for a modifier row (a set gem's row carries the gem's name; its <c>Line</c> names the stat), a name or caption otherwise. Never a slot or shape word beside a number.</param>
/// <param name="Value">The right column: the signed figure in its unit for a modifier row, a verdict or count otherwise; empty when the row is a sentence.</param>
/// <param name="Tone">The row's meaning as a colour class.</param>
/// <param name="Note">A second line of plain words — the enchant's sentence, the prefix's direction, the family's line.</param>
/// <param name="Modifier">The typed modifier a modifier row states, or null.</param>
/// <param name="IsDrawback">A cost side of a trade.</param>
/// <param name="Index">A rolled affix's index, so a screen can animate one roll; -1 otherwise.</param>
public sealed record ItemDisplayRow(ItemRowKind Kind, string Label, string Value = "", ItemRowTone Tone = ItemRowTone.Neutral,
                                    string Note = "", ItemModifier? Modifier = null, bool IsDrawback = false, int Index = -1)
{
    /// <summary>The row as ONE line — "+4% DAMAGE" for a modifier, "ITEM POWER  581" for a pair, the label alone otherwise.</summary>
    public string Line => Modifier is { } m ? ItemModifiers.Line(m) : Value.Length == 0 ? Label : $"{Label}  {Value}";

    /// <summary>A row that states a number.</summary>
    public bool IsNumeric => Modifier is not null || Value.Any(char.IsDigit);
}

/// <summary>
/// THE ONE READING OF AN ITEM — every row a card or an inspector prints, with its words decided here.
/// </summary>
/// <remarks>
/// <para>
/// Pass 17 (2026-09-07) gave every item NUMBER one formatter (<see cref="ItemModifiers"/>), and its
/// guard could hold that formatter to naming a stat — but not a screen, which could still compose a
/// label of its own beside the bare figure, exactly as the item card once composed the slot word
/// beside the built-in's number ("GLOVES  +2%"). So the ROWS themselves live here: the Gear and
/// Forge inspectors, the item card and the trader's compact card walk this list and lay it out, and
/// a test walks the same list and refuses any numeric row whose label is not the stat it moves. A
/// screen owns layout, clipping, hover, input and drawing; it never rebuilds a mechanic's words from
/// a slot, a prefix, an enum or a number.
/// </para>
/// <para>
/// The rows are in card order: power and verdict, the rolled affixes, the set gems, the built-in
/// (with the family's line for a weapon), the prefix and both sides of its trade, the enchant and its
/// need, the set. A caller that wants only the rows a Hunter cannot influence passes no Hunter.
/// </para>
/// </remarks>
public static class ItemPresentation
{
    /// <summary>The caption a built-in row wears after its stat word, in one spelling for every screen.</summary>
    public const string BuiltInCaption = "BUILT IN";
    
    /// <summary>The verdict for a slot nothing is worn in — one spelling for the card and its compact form.</summary>
    public const string SlotEmptyCaption = "THE SLOT IS EMPTY";

    /// <summary>
    /// Every row of the item. <paramref name="hunter"/> adds the power and the comparison against
    /// what is worn (and the set's worn count); <paramref name="wearer"/> the champion reading it, so
    /// a piece they cannot wear says who can instead of promising an upgrade; the build facts decide
    /// the enchant's need verdict.
    /// </summary>
    /// <remarks>
    /// The need verdict is <see cref="EnchantNeed.MetBy"/> — the one predicate the Forge's band and
    /// its RE-ROLL list already use — over what the caller passes: a screen that passes no weave gets
    /// no need row; one that passes the weave but no sockets or vows is judged as socketing none.
    /// The tolerant reading (unknown = met) is deliberately NOT taken: an item wrongly greyed is a
    /// player who looks again, an item wrongly gilded is a player who equips it and wonders why
    /// nothing changed — and both live screens can see their sockets.
    /// </remarks>
    public static IReadOnlyList<ItemDisplayRow> Rows(ItemInstance item, Hunter? hunter = null, Character? wearer = null,
                                                     IReadOnlyCollection<SkillDef>? woven = null,
                                                     IReadOnlyCollection<BuildTrigger>? triggers = null, int? swornVows = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        var rows = new List<ItemDisplayRow>();
        var slot = Gear.SlotFor(item.BaseType);

        // ── POWER, and the decision ──
        if (hunter is not null && slot is { } s)
        {
            var mine = hunter.PowerContribution(item);
            rows.Add(new ItemDisplayRow(ItemRowKind.Power, "ITEM POWER", mine.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
            var worn = hunter.Worn(s);
            if (wearer is not null && !Gear.CanWear(wearer, item) && item.Class is { } locked)
                // The refusal on one line, who can wear it on the next (Note) — one place for both.
                rows.Add(new ItemDisplayRow(ItemRowKind.Verdict, $"NOT FOR {wearer.Name}", Tone: ItemRowTone.Cost,
                                            Note: $"{ItemClasses.ChampionNames(locked)} CAN WEAR IT"));
            else if (worn is not null && worn.InstanceId != item.InstanceId)
            {
                var delta = mine - hunter.PowerContribution(worn);
                var word = delta > 0 ? "UPGRADE" : delta < 0 ? "DOWNGRADE" : "SIDEGRADE";
                var tone = delta > 0 ? ItemRowTone.Bonus : delta < 0 ? ItemRowTone.Cost : ItemRowTone.Muted;
                rows.Add(new ItemDisplayRow(ItemRowKind.Verdict, word, delta.ToString("+#,0;-#,0;0", System.Globalization.CultureInfo.InvariantCulture), tone,
                                            Note: $"REPLACES {ItemNaming.FullName(worn)}"));
            }
            else if (worn is not null) rows.Add(new ItemDisplayRow(ItemRowKind.Verdict, "WORN", Tone: ItemRowTone.Accent));
            else rows.Add(new ItemDisplayRow(ItemRowKind.Verdict, SlotEmptyCaption, Tone: ItemRowTone.Bonus));
        }

        // ── THE ROLLED NUMBERS ──
        var affixes = ItemModifiers.Affixes(item);
        for (var i = 0; i < affixes.Count; i++)
            rows.Add(Modifier(ItemRowKind.Affix, affixes[i], ItemRowTone.Bonus, index: i));

        // ── THE GEMS SET INTO IT ──
        if (item.Gems.Count > 0)
        {
            rows.Add(new ItemDisplayRow(ItemRowKind.GemCount, "SET GEMS", $"{item.Gems.Count} OF {GemCraft.SocketCount(item.Rarity)}", ItemRowTone.Muted));
            foreach (var gem in item.Gems)
                rows.Add(new ItemDisplayRow(ItemRowKind.Gem, GemCraft.NameOf(gem), ItemModifiers.Value(ItemModifiers.Gem(gem)), ItemRowTone.Bonus,
                                            Modifier: ItemModifiers.Gem(gem)));
        }

        // ── BUILT IN — what the item is by birth, NAMED by its stat. This is the row that read
        //    "GLOVES  +2%": the slot is not the effect, and the stat is data on the item. ──
        foreach (var m in ItemModifiers.BuiltIn(item))
        {
            rows.Add(Modifier(ItemRowKind.BuiltIn, m, ItemRowTone.Accent));
            if (item.BaseType == ItemBaseType.Weapon)
                rows.Add(new ItemDisplayRow(ItemRowKind.Family, ItemFamilies.Blurb(item).ToUpperInvariant(), Tone: ItemRowTone.Neutral));
        }

        // ── THE PREFIX, and BOTH SIDES of its trade ──
        if (GearTraits.TraitOf(item) is { } trait)
        {
            rows.Add(new ItemDisplayRow(ItemRowKind.Prefix, GearTraits.NameOf(trait), Tone: ItemRowTone.Accent, Note: GearTraits.BlurbOf(trait)));
            foreach (var m in ItemModifiers.Prefix(item))
                rows.Add(Modifier(ItemRowKind.PrefixSide, m, m.IsDrawback ? ItemRowTone.Cost : ItemRowTone.Accent));
        }

        // ── THE ENCHANT, in its one sentence, and what it needs ──
        if (Enchantments.Of(item) is { } ench)
        {
            rows.Add(new ItemDisplayRow(ItemRowKind.Enchant, ench.Name, Tone: ItemRowTone.Accent, Note: ench.Blurb));
            if (ench.Needs is { } need && woven is not null)
            {
                var met = need.MetBy(woven, triggers ?? Array.Empty<BuildTrigger>(), swornVows ?? 0);
                rows.Add(new ItemDisplayRow(ItemRowKind.EnchantNeed,
                                            met ? "WORKS WITH YOUR BUILD" : $"NEEDS {need.Label.ToUpperInvariant()} IN YOUR BUILD — UNTIL THEN IT DOES NOTHING",
                                            Tone: met ? ItemRowTone.Bonus : ItemRowTone.Cost));
            }
        }

        // ── THE SET ──
        if (hunter is not null && item.Element is { } element)
        {
            var count = ElementSets.WornCount(hunter, element);
            rows.Add(new ItemDisplayRow(ItemRowKind.Set, ElementSets.Name(element), ElementSets.Progress(count),
                                        count >= ElementSets.Rungs[0] ? ItemRowTone.Neutral : ItemRowTone.Muted));
        }

        return rows;
    }

    /// <summary>A modifier row: the stat word in the label, the signed figure in the value, the typed modifier attached.</summary>
    private static ItemDisplayRow Modifier(ItemRowKind kind, ItemModifier m, ItemRowTone tone, int index = -1)
        => new(kind, ItemModifiers.Word(m.Stat), ItemModifiers.Value(m), tone, Modifier: m, IsDrawback: m.IsDrawback, Index: index);

    /// <summary>
    /// The enchants a RE-ROLL could give this item, each with its name (Label), the build's verdict on
    /// it (Value — "WORKS WITH YOUR BUILD" / "NEEDS …" / nothing, Tone Bonus when it works) and its one
    /// canonical sentence (Note). The current enchant is not among them: a re-roll never returns it.
    /// </summary>
    public static IReadOnlyList<ItemDisplayRow> EnchantCandidates(ItemInstance item, IReadOnlyCollection<SkillDef> woven,
                                                                  IReadOnlyCollection<BuildTrigger> triggers, int swornVows)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Gear.SlotFor(item.BaseType) is not { } slot) return Array.Empty<ItemDisplayRow>();
        var current = Enchantments.Of(item);
        var rows = new List<ItemDisplayRow>();
        foreach (var kind in Enchantments.PoolFor(slot).Where(k => current is null || k != current.Kind))
        {
            var cand = new Enchantment(kind, Enchantments.MagnitudeFor(kind, item.Rarity));
            var verdict = "";
            var tone = ItemRowTone.Neutral;
            if (cand.Needs is { } need)
            {
                var met = need.MetBy(woven, triggers, swornVows);
                verdict = met ? "WORKS WITH YOUR BUILD" : $"NEEDS {need.Label.ToUpperInvariant()}";
                tone = met ? ItemRowTone.Bonus : ItemRowTone.Muted;
            }
            rows.Add(new ItemDisplayRow(ItemRowKind.Enchant, cand.Name, verdict, tone, Note: cand.Blurb));
        }
        return rows;
    }
}
