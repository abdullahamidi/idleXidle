using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Client;

/// <summary>
/// Everything an item IS, in one card, drawn under the pointer.
/// </summary>
/// <remarks>
/// <para>
/// Written because a player said they could not read what their items did — and they were right, but
/// not because the text was small. The text was ABSENT. The detail panel printed "TRAIT — KEEN" and
/// "ENCHANT: HARVEST", which are names, not effects: nothing on screen said what KEEN does to a hit or
/// what HARVEST does on a kill, though both sentences have existed in <c>GearTraits.BlurbOf</c> and
/// <c>Enchantment.Blurb</c> the whole time. And the inventory was twenty icons with a coloured edge, so
/// telling two items apart meant clicking each one.
/// </para>
/// <para>
/// One card, shared by every screen that shows an item, for the reason every other shared thing in this
/// codebase exists: four screens drawing four subsets of an item's facts is four chances to leave one
/// out, and the one left out is always the one that mattered.
/// </para>
/// <para>
/// It compares against what is WORN whenever a Hunter is passed, because "is this better than mine" is
/// the only question anyone opens an item to ask. The comparison uses
/// <see cref="Hunter.PowerContribution"/> — the marginal PowerRating of the piece — never
/// <c>Gear.ItemScore</c>, so what the card claims and what equipping actually does can never disagree.
/// </para>
/// </remarks>
public static class ItemTooltip
{
    public const int Width = 460;

    private static readonly Color Ink = new(0xE8, 0xE2, 0xD4);
    private static readonly Color Dim = new(0x8A, 0x82, 0x74);
    private static readonly Color Gold = new(0xE8, 0xC8, 0x7A);
    private static readonly Color Good = new(0x6E, 0xC8, 0x7A);
    private static readonly Color Bad = new(0xC8, 0x5A, 0x5A);
    private static readonly Color Violet = new(0xB0, 0x8A, 0xE0);

    private static readonly Color[] RarityInk =
        [new(0xC8, 0xC2, 0xB4), new(0x6E, 0xC8, 0x7A), new(0x4A, 0x90, 0xD9), new(0xB0, 0x6A, 0xC8), new(0xE8, 0xC8, 0x7A)];

    /// <summary>How tall the card will be for this item — so a caller can place it before drawing.</summary>
    /// <param name="wearer">The champion reading the card; when they cannot wear it, the card says why (two lines).</param>
    public static int HeightFor(ItemInstance item, Hunter? hunter, Character? wearer = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        var h = 96;                                             // name + the rarity/slot line
        // THE CLASS LINE, measured. Every line the card draws is counted here — the family block once
        // was not, and every weapon card ran past its own border for it.
        if (Gear.SlotFor(item.BaseType) is not null) h += 26;   // "WARDEN GEAR" / "ANY CLASS"
        if (wearer is not null && Gear.IsWearable(item) && !Gear.CanWear(wearer, item)) h += 52;   // the reason, two lines
        h += 34;                                                // item power
        if (GemCraft.IsGem(item)) h += 56;                      // what it gives + where it goes
        h += ItemAffixes.Of(item).Count * 28;
        if (ItemAffixes.Of(item).Count > 0) h += 10;
        // The gems SET INTO this item: a header line plus one line each. They are a real slice of the
        // item's numbers and the card said nothing about them at all.
        if (item.Gems.Count > 0) h += 26 + item.Gems.Count * 26 + 10;
        // THE FAMILY BLOCK WAS NEVER MEASURED. Draw prints it (one line, two on a weapon, plus a gap)
        // and this did not count it, so every weapon card ran ~62px past its own bottom border and the
        // ENCHANT blurb — the last and most build-relevant line — was drawn outside the card entirely.
        if (ItemFamilies.BonusOf(item) is not null)
            h += 26 + (item.BaseType == ItemBaseType.Weapon ? 26 : 0) + 10;
        if (GearTraits.TraitOf(item) is not null) h += 62;
        if (Enchantments.Of(item) is not null) h += 62;
        if (hunter is not null && Gear.SlotFor(item.BaseType) is not null) h += 46;
        return h + 28;
    }

    /// <summary>
    /// Draw the card with its top-left at <paramref name="at"/>, nudged to stay on the canvas.
    /// </summary>
    /// <param name="canvas">The drawable area, so a card raised near the right edge flips to the left.</param>
    /// <param name="wearer">
    /// The champion reading the card. When set and they cannot wear the item, the card names who can.
    /// </param>
    public static void Draw(UiKit ui, SpriteBatch b, ItemInstance item, Hunter? hunter, Point at, Rectangle canvas,
                            Character? wearer = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(item);

        var h = HeightFor(item, hunter, wearer);

        // FLIP, don't clamp. A card pinned to the edge sits ON the thing the pointer is over, which is
        // the one thing it must never cover — you are hovering an item to see it, not to have it hidden.
        var x = at.X + Width + 24 <= canvas.Right ? at.X + 24 : at.X - Width - 24;
        var y = Math.Clamp(at.Y - 20, canvas.Top + 8, Math.Max(canvas.Top + 8, canvas.Bottom - h - 8));
        var card = new Rectangle(x, y, Width, h);

        // Its own dark ground rather than UiKit.Panel: the panel art's 40px inset would eat a third of a
        // card this size, and a tooltip wants to read as a floating label, not as another window.
        ui.Fill(b, new Rectangle(card.X + 4, card.Y + 4, card.Width, card.Height), new Color(0, 0, 0, 140));
        ui.Fill(b, card, new Color(0x0E, 0x0C, 0x14, 0xF2));
        var edge = RarityInk[(int)item.Rarity];
        ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 3), edge);
        ui.Fill(b, new Rectangle(card.X, card.Bottom - 3, card.Width, 3), edge * 0.5f);
        ui.Fill(b, new Rectangle(card.X, card.Y, 3, card.Height), edge * 0.5f);
        ui.Fill(b, new Rectangle(card.Right - 3, card.Y, 3, card.Height), edge * 0.5f);

        var lx = card.X + 20;
        var rx = card.Right - 20;
        var cy = card.Y + 18;

        ui.TextBig(b, ItemNaming.FullName(item), lx, cy, edge, UiTypography.PanelTitle);
        cy += 34;

        // ── WHO CAN WEAR IT — the class, under the name, before anything else. ──
        // "WARDEN GEAR" in the class's colour; "ANY CLASS" for a charm, ring or focus; "ANY CLASS ·
        // OLD MAKE" for a helm from before classes existed, which anyone may still wear.
        var slot = Gear.SlotFor(item.BaseType);
        if (slot is not null)
        {
            ui.Text(b, ItemClasses.ClassLine(item), lx, cy,
                    item.Class is { } ic && ItemClasses.IsClassLocked(item.BaseType) ? UiKit.ClassColor(ic) : Dim);
            cy += 26;
            if (wearer is not null && !Gear.CanWear(wearer, item) && item.Class is { } locked)
            {
                // Two lines, both measured in HeightFor: the slot, then the two champions who can.
                ui.Text(b, $"A {ItemClasses.NameOf(locked)}'S {ItemNaming.TypeWord(item)} —", lx, cy, Bad);
                cy += 26;
                ui.Text(b, ui.Shorten($"{ItemClasses.ChampionNames(locked)} CAN WEAR IT", card.Width - 40), lx, cy, Bad);
                cy += 26;
            }
        }

        // ONE LINE, not a left run and a right-aligned element. Right-aligning the source put "NATURE"
        // on top of "iL2" the moment a name was long enough — a card that exists to make things readable
        // must not be the thing overlapping itself.
        var line = $"{item.Rarity.ToString().ToUpperInvariant()}  ·  "
                   + $"{(slot?.ToString() ?? item.BaseType.ToString()).ToUpperInvariant()}  ·  LEVEL {item.ItemLevel}";
        if (item.Element is { } el) line += $"  ·  {el.ToString().ToUpperInvariant()}";
        ui.Text(b, line, lx, cy, Dim);
        cy += 32;

        ui.Fill(b, new Rectangle(lx, cy, card.Width - 40, 1), new Color(0x2A, 0x26, 0x34));
        cy += 14;

        // ── ITEM POWER, and what swapping would do to yours ──
        if (hunter is not null && slot is { } s)
        {
            var mine = hunter.PowerContribution(item);
            ui.Text(b, "ITEM POWER", lx, cy, Dim);
            ui.TextRight(b, $"{mine:N0}", rx, cy, Ink);
            cy += 34;

            var worn = hunter.Worn(s);
            if (wearer is not null && !Gear.CanWear(wearer, item))
            {
                // No UPGRADE verdict on a piece this champion cannot put on — a "+21" they cannot
                // collect is a promise the EQUIP button then breaks. Same 46px slot, so the height holds.
                ui.Text(b, $"NOT FOR {wearer.Name} — SWITCH CHAMPION ON THE ROSTER", lx, cy, Bad);
                cy += 46;
            }
            else if (worn is not null && worn.InstanceId != item.InstanceId)
            {
                var delta = mine - hunter.PowerContribution(worn);
                var word = delta > 0 ? "UPGRADE" : delta < 0 ? "DOWNGRADE" : "SIDEGRADE";
                var tone = delta > 0 ? Good : delta < 0 ? Bad : Dim;
                ui.Text(b, $"{word}  ·  REPLACES {ItemNaming.FullName(worn)}", lx, cy, tone);
                ui.TextRight(b, $"{delta:+#,0;-#,0;0}", rx, cy, tone);
                cy += 46;
            }
            else if (worn is not null)
            {
                ui.Text(b, "WORN", lx, cy, Gold);
                cy += 46;
            }
            else
            {
                ui.Text(b, "THE SLOT IS EMPTY", lx, cy, Good);
                cy += 46;
            }
        }
        else if (GemCraft.IsGem(item))
        {
            // A GEM IS NOT "MATERIAL". This card printed that literal word for every gem — the one
            // fact a player needs from a stone (what it adds, and that it goes into gear) was on no
            // screen in the game. Same formatter as an affix line, so the units cannot drift.
            ui.Text(b, GemCraft.Grant(item), lx, cy, Good);
            cy += 30;
            ui.Text(b, "SET IT INTO RARE OR BETTER GEAR", lx, cy, Dim);
            cy += 60;
        }
        else
        {
            ui.Text(b, "MATERIAL", lx, cy, Dim);
            cy += 34;
        }

        // ── AFFIXES — the rolled numbers ──
        var affixes = ItemAffixes.Of(item);
        foreach (var a in affixes)
        {
            ui.Text(b, ItemAffixes.Describe(a), lx, cy, Good);
            cy += 28;
        }
        if (affixes.Count > 0) cy += 10;

        // ── THE GEMS SET INTO IT — the player's own investment, and the card was blind to it. ──
        if (item.Gems.Count > 0)
        {
            ui.Text(b, $"SET GEMS  {item.Gems.Count} OF {GemCraft.SocketCount(item.Rarity)}", lx, cy, Dim);
            cy += 26;
            foreach (var gem in item.Gems)
            {
                ui.Text(b, GemCraft.NameOf(gem), lx, cy, Violet);
                ui.TextRight(b, GemCraft.Grant(gem), rx, cy, Good);
                cy += 26;
            }
            cy += 10;
        }

        // ── FAMILY — what the item IS by birth (item-system redesign): the built-in channel every
        //    copy of this shape carries, in its stat's own unit. ──
        if (ItemFamilies.BonusOf(item) is { } fam)
        {
            ui.Text(b, $"{ItemNaming.TypeWord(item)}  {ItemAffixes.GrantLabel(fam.Stat, fam.Magnitude)}", lx, cy, Gold);
            ui.TextRight(b, "BUILT IN", rx, cy, Dim);
            cy += 26;
            if (item.BaseType == ItemBaseType.Weapon)
            {
                ui.Text(b, ItemFamilies.Blurb(item).ToUpperInvariant(), lx, cy, Ink);
                cy += 26;
            }
            cy += 10;
        }

        // ── PREFIX and ENCHANT — the NAME AND WHAT IT DOES. The half that was missing. ──
        if (GearTraits.TraitOf(item) is { } trait)
        {
            ui.Text(b, GearTraits.NameOf(trait), lx, cy, Gold);
            ui.TextRight(b, "PREFIX", rx, cy, Dim);
            cy += 26;
            // The REAL numbers, not the blurb: the prefix already scales with item level
            // (GearTraits.ModsFor folds ItemLevelFactor in), but a fixed sentence made it read as
            // flat — playtest: "prefix statları sabit kalmasın." An upgrade now visibly deepens it.
            ui.Text(b, PrefixNumbers(item), lx, cy, Ink);
            cy += 36;
        }

        if (Enchantments.Of(item) is { } ench)
        {
            ui.Text(b, ench.Name, lx, cy, Violet);
            ui.TextRight(b, "ENCHANT", rx, cy, Dim);
            cy += 26;
            ui.Text(b, ench.Blurb.ToUpperInvariant(), lx, cy, Ink);
        }
    }

    /// <summary>The prefix's real trade at THIS item's level — "DAMAGE +38% · LOOT -20%".</summary>
    private static string PrefixNumbers(ItemInstance item)
    {
        var m = GearTraits.ModsOf(item);
        var parts = new System.Collections.Generic.List<string>();
        void Add(string label, float v)
        {
            if (MathF.Abs(v - 1f) <= 0.005f) return;
            var pct = (int)MathF.Round((v - 1f) * 100f);
            parts.Add($"{label} {(pct >= 0 ? "+" : "")}{pct}%");
        }
        Add("DAMAGE", m.Damage);
        Add("HEALTH", m.Health);
        Add("SKILL RATE", m.SkillRate);
        Add("LOOT", m.Haul);
        return parts.Count == 0 ? "NO TRADE" : string.Join("  ·  ", parts);
    }
}
