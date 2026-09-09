using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Game;

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
/// <para>
/// ONE WALK FOR MEASURING AND DRAWING. <see cref="HeightFor"/> and <see cref="Draw"/> used to be two
/// hand-kept lists of line advances (34, 26, 32 + 14 …), and every time one gained a line the other did
/// not: the family block went unmeasured and every weapon card ran 62 px past its own border. Now a
/// single <see cref="Walk"/> lays the card out — with a batch it draws, without one it only advances —
/// and every advance is the rung's <see cref="UiTypography.Pitch"/>, so the card follows the UI SCALE
/// profile with the text it holds.
/// </para>
/// </remarks>
public static class ItemTooltip
{
    /// <summary>The card's width. It follows the profile with the text it holds, so a line that fits at 100 % fits at 150 %.</summary>
    public static int Width => UiMetrics.Control(460);

    // THE HOUSE INKS (UiInk), under the old local names. The card spoke its own warm palette beside an
    // inspector in the blue-grey Secondary, so the same word read in two colours 300 px apart
    // (release polish 2026-09-05, gear-03). An enchant's name is a name, not a state: Primary.
    private static readonly Color Ink = UiInk.Primary;
    private static readonly Color Dim = UiInk.Secondary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Good = UiInk.Good;
    private static readonly Color Bad = UiInk.Danger;
    private static readonly Color Violet = UiInk.Primary;

    private static readonly Color[] RarityInk =
        [new(0xC8, 0xC2, 0xB4), new(0x6E, 0xC8, 0x7A), new(0x4A, 0x90, 0xD9), new(0xB0, 0x6A, 0xC8), new(0xE8, 0xC8, 0x7A)];

    // The card's own grid: its side inset, its top and bottom pads, and the pitch of a plain line —
    // <c>ui.Text</c> draws at the Label rung, which is Body, so a plain line advances by Body's pitch.
    private static int PadX => UiMetrics.Space(20);
    private static int PadTop => UiMetrics.Space(18);
    private static int PadBottom => PadTop;   // symmetric, now that the card is a plate with no bottom rule (gear-03)
    private static int LineH => UiTypography.Pitch(UiTypography.Label);

    /// <summary>How tall the card will be for this item — so a caller can place it before drawing.</summary>
    /// <param name="ui">The kit that measures text: a wrapped line counts in the height pass exactly as it draws.</param>
    /// <param name="wearer">The champion reading the card; when they cannot wear it, the card says why (two lines).</param>
    public static int HeightFor(UiKit ui, ItemInstance item, Hunter? hunter, Character? wearer = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(item);
        return Walk(ui, null, item, hunter, wearer, new Rectangle(0, 0, Width, 0));
    }

    /// <summary>
    /// Draw the card with its top-left at <paramref name="at"/>, nudged to stay on the canvas.
    /// </summary>
    /// <param name="canvas">The drawable area, so a card raised near the right edge flips to the left.</param>
    /// <param name="wearer">
    /// The champion reading the card. When set and they cannot wear the item, the card names who can.
    /// </param>
    public static void Draw(UiKit ui, SpriteBatch b, ItemInstance item, Hunter? hunter, Rectangle at, Rectangle canvas,
                            Character? wearer = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(item);

        // THROUGH THE ONE PLACEMENT RULE (PopoverPlacement, 2026-09-06): the card hangs to the right of
        // THE CELL, flips left at the edge, and is clamped into the canvas — the same rule the enemy
        // inspector and the hover tip obey.
        //
        // The anchor was a 1x1 rectangle built from the POINTER until 2026-09-09, and this is the
        // biggest thing on the screen that followed it — 460 px wide at 100 %, 690 at 150 %, and up to
        // 700 tall. Playtest: "the informational text that appears on hover shouldn't move along with
        // the mouse cursor." Anchored to the cell, it is still while the pointer moves inside it and
        // lands in the same place every time the reader comes back to that item.
        var h = HeightFor(ui, item, hunter, wearer);
        var anchor = at;
        var card = PopoverPlacement.Place(anchor, new Point(Width, h), canvas, null,
                                          new[] { PopoverSide.Right, PopoverSide.Left, PopoverSide.Below, PopoverSide.Above },
                                          UiMetrics.Space(24));
        DrawAt(ui, b, item, hunter, card, wearer);
    }

    /// <summary>
    /// Draw the card in a rectangle a caller has already PLACED — the trader places its offer's card
    /// with the BUY buttons as avoided rectangles, through the shared rule, and hands the result here.
    /// </summary>
    public static void DrawAt(UiKit ui, SpriteBatch b, ItemInstance item, Hunter? hunter, Rectangle card, Character? wearer = null)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(item);

        // Its own dark ground rather than UiKit.Panel: the panel art's 40px inset would eat a third of a
        // card this size, and a tooltip wants to read as a floating label, not as another window.
        // The house QUIET plate with the rarity as its accent, over a soft shadow — not four hand-drawn
        // edges in the rarity colour (gear-03). It still reads as a floating label, on a house surface.
        ui.Fill(b, new Rectangle(card.X + 4, card.Y + 4, card.Width, card.Height), new Color(0, 0, 0, 140));
        ui.Plate(b, card, RarityInk[(int)item.Rarity]);

        Walk(ui, b, item, hunter, wearer, card);
    }

    /// <summary>
    /// The card's lines, top to bottom. With a batch it draws them into <paramref name="card"/>; without
    /// one it only walks, and returns the height the same lines take. Both callers share it, so the height
    /// a caller places by and the height the card draws to are one number.
    /// </summary>
    // ── THE COMPACT READING (2026-09-06): what a buyer needs BEFORE the purchase — the art, the grade,
    //    the name, the level and Source, the power verdict, the rolled numbers, the price — on the
    //    same canonical data the full card walks (the same Core calls; nothing recomputed here). The
    //    full card is the item's page once it is owned; at the stall it stood 700 px tall at 150 % and
    //    could only be placed over the title or a BUY.

    /// <summary>The compact card's width.</summary>
    public static int CompactWidth => UiMetrics.Control(340);

    /// <summary>A price line the compact card prints: the material, the amount, what the hunter holds.</summary>
    public readonly record struct PriceLine(string Material, int Amount, long Held);

    /// <summary>How tall the compact card will be — so a caller can place it before drawing.</summary>
    public static int CompactHeightFor(UiKit ui, ItemInstance item, Hunter? hunter, IReadOnlyList<PriceLine> prices)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(item);
        return WalkCompact(ui, null, item, hunter, prices, null, new Rectangle(0, 0, CompactWidth, 0));
    }

    /// <summary>Draw the compact card in a rectangle a caller has PLACED (through PopoverPlacement).</summary>
    public static void DrawCompact(UiKit ui, SpriteBatch b, ItemInstance item, Hunter? hunter, IReadOnlyList<PriceLine> prices,
                                   Action<SpriteBatch, ItemInstance, Rectangle>? drawArt, Rectangle card)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(item);
        ui.Fill(b, new Rectangle(card.X + 4, card.Y + 4, card.Width, card.Height), new Color(0, 0, 0, 140));
        ui.Plate(b, card, RarityInk[(int)item.Rarity]);
        WalkCompact(ui, b, item, hunter, prices, drawArt, card);
    }

    // The kit is REQUIRED in both passes (2026-09-07): the pair packing and the enchant sentence are
    // measured, and a height pass without a kit would count a card the draw pass then draws taller.
    private static int WalkCompact(UiKit ui, SpriteBatch? b, ItemInstance item, Hunter? hunter, IReadOnlyList<PriceLine> prices,
                                   Action<SpriteBatch, ItemInstance, Rectangle>? drawArt, Rectangle card)
    {
        var draw = b is not null;
        var lx = card.X + PadX;
        var rx = card.Right - PadX;
        var innerW = card.Width - PadX * 2;
        var cy = card.Y + PadTop;
        var edge = RarityInk[(int)item.Rarity];
        // A 48 thumbnail and tight rules (2026-09-06): the tallest offer — four rolls, a built-in, a
        // prefix, an enchant, two materials and the verdict on its own row — must still end above the
        // BUY under the next offer at 150 %, or the card cannot stand beside the offer at all.
        var art = UiMetrics.Control(48);
        var slot = Gear.SlotFor(item.BaseType);

        void L(string s, Color c) { if (draw) ui!.Text(b!, s, lx, cy, c); }
        void R(string s, Color c) { if (draw) ui!.TextRight(b!, s, rx, cy, c); }

        // The art beside the name and the grade line, so the card reads as the offer it hangs off.
        if (draw)
        {
            var artBox = new Rectangle(lx, cy, art, art);
            if (drawArt is not null) drawArt(b!, item, artBox); else ui!.Fill(b!, artBox, edge * 0.5f);
        }
        var textX = lx + art + UiMetrics.Space(12);
        var textW = innerW - art - UiMetrics.Space(12);
        if (draw) ui!.TextBig(b!, ui.ShortenBig(ItemNaming.FullName(item), textW, UiTypography.Body), textX, cy + (art - UiTypography.Body) / 2, edge, UiTypography.Body);
        cy += art + UiMetrics.Space(6);
        // The grade line takes the card's whole width under the art row: beside the art it was cut to
        // "LEVEL 18 · SPI…" at 150 %, and the Source is one of the reasons a buyer looks.
        var grade = $"{item.Rarity.ToString().ToUpperInvariant()}  ·  {(slot?.ToString() ?? item.BaseType.ToString()).ToUpperInvariant()}  ·  LEVEL {item.ItemLevel}";
        if (item.Element is { } el) grade += $"  ·  {el.ToString().ToUpperInvariant()}";
        // Shortened at the card's edge: "LEGENDARY · GLOVES · LEVEL 18 · MACHINE" ran a few pixels past
        // the plate at every profile (2026-09-07). A grade line is a caption, not a modifier — the
        // one kind of line an ellipsis may end.
        L(ui.Shorten(grade, innerW), Dim);
        cy += LineH + UiMetrics.Space(4);
        if (draw) ui!.Fill(b!, new Rectangle(lx, cy, innerW, 1), UiInk.Rule);
        cy += UiMetrics.Space(6);

        // THE ROWS ARE CORE'S (ItemPresentation, 2026-09-07); this card lays out the buyer's subset —
        // the power verdict on one row, the rolls, the built-in, the prefix and enchant names, both
        // sides of the trade, the enchant's sentence — and composes no words of its own.
        var rows = ItemPresentation.Rows(item, hunter, null);
        if (rows.FirstOrDefault(r => r.Kind == ItemRowKind.Power) is { } power)
        {
            // ITEM POWER and the verdict against what is worn, ON ONE ROW (2026-09-06: the compact
            // card is composed for the room beside an offer at 150 %).
            L($"{power.Label}  {power.Value}", Ink);
            var verdict = rows.First(r => r.Kind == ItemRowKind.Verdict);
            var tone = verdict.Tone switch { ItemRowTone.Bonus => Good, ItemRowTone.Cost => Bad, ItemRowTone.Accent => Gold, _ => Dim };
            R(verdict.Value.Length > 0 ? $"{verdict.Label}  {verdict.Value}" : verdict.Label == ItemPresentation.SlotEmptyCaption ? "SLOT EMPTY" : verdict.Label, tone);
            cy += LineH + UiMetrics.Space(6);
        }
        else if (GemCraft.IsGem(item))
        {
            L(GemCraft.Grant(item), Good);
            cy += LineH;
            L("SET IT INTO RARE OR BETTER GEAR", Dim);
            cy += LineH + UiMetrics.Space(6);
        }

        // THE ROLLED NUMBERS (up to four), TWO TO A ROW WHERE TWO FIT (2026-09-06): the first at the
        // margin, the second right-aligned on the same rung — the two columns a stat sheet has, so a
        // legendary's four rolls take two rows, not four. A roll too long to share keeps its own row;
        // the same measure decides in the height pass, so the placed card is the drawn card.
        void Packed(IReadOnlyList<(string Text, Color Ink)> lines)
        {
            for (var k = 0; k < lines.Count;)
            {
                var pair = k + 1 < lines.Count
                           && ui.Measure(lines[k].Text) + UiMetrics.Space(24) + ui.Measure(lines[k + 1].Text) <= innerW;
                L(lines[k].Text, lines[k].Ink);
                if (pair) R(lines[k + 1].Text, lines[k + 1].Ink);
                cy += LineH;
                k += pair ? 2 : 1;
            }
        }
        var affixes = rows.Where(r => r.Kind == ItemRowKind.Affix).ToList();
        Packed(affixes.Take(4).Select(r => (r.Line, Good)).ToList());
        if (affixes.Count > 4) { L($"AND {affixes.Count - 4} MORE — READ IT ON THE GEAR SCREEN ONCE IT IS YOURS", Dim); cy += LineH; }
        // THE BUILT-IN — the stat the item carries by birth, NAMED. It read "GLOVES  +2%" (the slot
        // word where the stat belongs) until 2026-09-07; the stat is data on the item and is what prints.
        foreach (var row in rows.Where(r => r.Kind == ItemRowKind.BuiltIn))
        {
            L(row.Line, Gold);
            R(ItemPresentation.BuiltInCaption, Dim);
            cy += LineH;
        }
        // THE PREFIX AND THE ENCHANT SHARE A ROW: "GREEDY · COILED", labelled with the item system's
        // own words. Two rows said the same two names with a label each.
        var prefix = rows.FirstOrDefault(r => r.Kind == ItemRowKind.Prefix);
        var enchant = rows.FirstOrDefault(r => r.Kind == ItemRowKind.Enchant);
        if (prefix is not null || enchant is not null)
        {
            L(string.Join("  ·  ", new[] { prefix?.Label, enchant?.Label }.Where(n => n is not null)), Gold);
            R(prefix is not null && enchant is not null ? "PREFIX · ENCHANT" : prefix is not null ? "PREFIX" : "ENCHANT", Dim);
            cy += LineH;
        }
        // THE PREFIX'S TRADE, BOTH SIDES, before the price: a buyer reading "HEAVY" is owed the slower
        // skill clock as plainly as the harder hit (2026-09-07). Packed like the rolls; the cost side
        // in the cost colour, so the trade reads as one at a glance.
        Packed(rows.Where(r => r.Kind == ItemRowKind.PrefixSide).Select(r => (r.Line, r.IsDrawback ? Bad : Ink)).ToList());
        // AND WHAT THE ENCHANT DOES, in its one sentence, wrapped: "LINGER" beside a price is a name,
        // not a thing a buyer can want. Measured in the height pass too (the kit is present in both).
        if (enchant is not null)
            foreach (var wrapped in ui.WrapBig(enchant.Note.ToUpperInvariant(), innerW, UiTypography.Label))
            {
                L(wrapped, Ink);
                cy += LineH;
            }
        cy += UiMetrics.Space(6);

        // THE PRICE ON ONE ROW, each material in the wallet's verdict colour, the verdict itself at the
        // right — the one thing the full card never says.
        if (prices.Count > 0)
        {
            if (draw) ui!.Fill(b!, new Rectangle(lx, cy, innerW, 1), UiInk.Rule);
            cy += UiMetrics.Space(6);
            var x = lx;
            var short0 = prices.FirstOrDefault(p => p.Held < p.Amount);
            var allEnough = prices.All(p => p.Held >= p.Amount);
            var verdict = allEnough ? "YOU CAN AFFORD IT" : $"YOU HOLD {short0.Held} {short0.Material}";
            // The verdict shares the price's row only when both fit with a gap between them (measured
            // in the height pass too, so the placed card is the drawn card); otherwise it takes the next row.
            var run = string.Join("  ·  ", prices.Select(p => $"{p.Amount} {p.Material}"));
            var sameRow = ui.Measure(run) + UiMetrics.Space(16) + ui.Measure(verdict) <= innerW;
            for (var i = 0; i < prices.Count; i++)
            {
                var p = prices[i];
                if (draw)
                {
                    if (i > 0) { ui!.Text(b!, "  ·  ", x, cy, Dim); x += ui.Measure("  ·  "); }
                    ui!.Text(b!, $"{p.Amount} {p.Material}", x, cy, p.Held >= p.Amount ? Ink : Bad);
                    x += ui.Measure($"{p.Amount} {p.Material}");
                }
            }
            if (!sameRow) cy += LineH;
            R(verdict, allEnough ? Good : Bad);
            cy += LineH;
        }

        return cy - card.Y + PadBottom;
    }

    private static int Walk(UiKit ui, SpriteBatch? b, ItemInstance item, Hunter? hunter, Character? wearer, Rectangle card)
    {
        var draw = b is not null;
        var lx = card.X + PadX;
        var rx = card.Right - PadX;
        var innerW = card.Width - PadX * 2;
        var cy = card.Y + PadTop;
        var edge = RarityInk[(int)item.Rarity];

        // A left-run line and a right-aligned value on the same line — drawn only when drawing.
        void L(string s, Color c) { if (draw) ui!.Text(b!, s, lx, cy, c); }
        void R(string s, Color c) { if (draw) ui!.TextRight(b!, s, rx, cy, c); }

        if (draw) ui!.TextBig(b!, ui.ShortenBig(ItemNaming.FullName(item), innerW, UiTypography.Headline), lx, cy, edge, UiTypography.Headline);
        cy += UiTypography.Pitch(UiTypography.Headline);

        // ── WHO CAN WEAR IT — the class, under the name, before anything else. ──
        // "WARDEN GEAR" in the class's colour; "ANY CLASS" for a charm, ring or focus; "ANY CLASS ·
        // OLD MAKE" for a helm from before classes existed, which anyone may still wear.
        var slot = Gear.SlotFor(item.BaseType);
        if (slot is not null)
        {
            L(ItemClasses.ClassLine(item), item.Class is { } ic && ItemClasses.IsClassLocked(item.BaseType) ? UiKit.ClassColor(ic) : Dim);
            cy += LineH;
            // Who can wear a piece this champion cannot is the verdict row's business below (NOT FOR
            // THE SEEKER / … CAN WEAR IT) — the card said it twice until 2026-09-07.
        }

        // ONE LINE, not a left run and a right-aligned element. Right-aligning the source put "NATURE"
        // on top of "iL2" the moment a name was long enough — a card that exists to make things readable
        // must not be the thing overlapping itself.
        var line = $"{item.Rarity.ToString().ToUpperInvariant()}  ·  "
                   + $"{(slot?.ToString() ?? item.BaseType.ToString()).ToUpperInvariant()}  ·  LEVEL {item.ItemLevel}";
        if (item.Element is { } el) line += $"  ·  {el.ToString().ToUpperInvariant()}";
        L(line, Dim);
        cy += LineH + UiMetrics.Space(4);

        if (draw) ui!.Fill(b!, new Rectangle(lx, cy, innerW, 1), new Color(0x2A, 0x26, 0x34));
        cy += UiMetrics.Space(14);

        // ── THE ROWS ARE CORE'S (ItemPresentation, 2026-09-07): power and the verdict, the rolls, the
        //    gems, the built-in and the family's line, the prefix and both sides of its trade, the
        //    enchant's sentence, the set. This card lays them out — a left run, a right-aligned value,
        //    a wrapped sentence — and composes no words of its own. It once composed the slot word
        //    beside the built-in's figure ("GLOVES  +2%"), a line that cannot be written from a list
        //    whose labels are stats. Group gaps follow the card's old grid, so nothing moved. ──
        var rows = ItemPresentation.Rows(item, hunter, wearer);
        Color ToneInk(ItemRowTone t) => t switch
        {
            ItemRowTone.Bonus => Good, ItemRowTone.Cost => Bad, ItemRowTone.Accent => Gold, ItemRowTone.Muted => Dim, _ => Ink,
        };

        if (rows.Any(r => r.Kind == ItemRowKind.Power))
        {
            var power = rows.First(r => r.Kind == ItemRowKind.Power);
            L(power.Label, Dim);
            R(power.Value, Ink);
            cy += LineH + UiMetrics.Space(6);
            var verdict = rows.First(r => r.Kind == ItemRowKind.Verdict);
            if (verdict.Value.Length > 0)
            {
                // The verdict clears the value beside it — a long worn name is shortened, never overprinted.
                if (draw) ui.Text(b!, ui.Shorten($"{verdict.Label}  ·  {verdict.Note}", innerW - ui.Measure(verdict.Value) - UiMetrics.Space(12)), lx, cy, ToneInk(verdict.Tone));
                R(verdict.Value, ToneInk(verdict.Tone));
            }
            else
            {
                // A refusal says who can wear it on the line beneath — Core's words, said once.
                if (draw) ui.Text(b!, ui.Shorten(verdict.Label, innerW), lx, cy, ToneInk(verdict.Tone));
                if (verdict.Note.Length > 0)
                {
                    cy += LineH;
                    if (draw) ui.Text(b!, ui.Shorten(verdict.Note, innerW), lx, cy, ToneInk(verdict.Tone));
                }
            }
            cy += LineH + UiMetrics.Space(18);
        }
        else if (GemCraft.IsGem(item))
        {
            // A GEM IS NOT "MATERIAL". This card printed that literal word for every gem — the one
            // fact a player needs from a stone (what it adds, and that it goes into gear) was on no
            // screen in the game. Same formatter as an affix line, so the units cannot drift.
            L(GemCraft.Grant(item), Good);
            cy += LineH + UiMetrics.Space(2);
            L("SET IT INTO RARE OR BETTER GEAR", Dim);
            cy += LineH + UiMetrics.Space(32);
        }
        else
        {
            L("MATERIAL", Dim);
            cy += LineH + UiMetrics.Space(6);
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var next = i + 1 < rows.Count ? rows[i + 1].Kind : (ItemRowKind?)null;
            switch (row.Kind)
            {
                case ItemRowKind.Affix:
                    L(row.Line, Good);
                    cy += LineH;
                    if (next != ItemRowKind.Affix) cy += UiMetrics.Space(10);
                    break;
                case ItemRowKind.GemCount:
                    L($"{row.Label}  {row.Value}", Dim);
                    cy += LineH;
                    break;
                case ItemRowKind.Gem:
                    L(row.Label, Violet);
                    R(row.Line, Good);
                    cy += LineH;
                    if (next != ItemRowKind.Gem) cy += UiMetrics.Space(10);
                    break;
                case ItemRowKind.BuiltIn:
                    // NAMED by its stat, never by its slot or shape — the line that read "GLOVES  +2%".
                    L(row.Line, Gold);
                    R(ItemPresentation.BuiltInCaption, Dim);
                    cy += LineH;
                    if (next != ItemRowKind.Family) cy += UiMetrics.Space(10);
                    break;
                case ItemRowKind.Family:
                    L(row.Label, Ink);
                    cy += LineH + UiMetrics.Space(10);
                    break;
                case ItemRowKind.Prefix:
                    L(row.Label, Gold);
                    R("PREFIX", Dim);
                    cy += LineH;
                    if (next != ItemRowKind.PrefixSide) { L(row.Note.ToUpperInvariant(), Ink); cy += LineH + UiMetrics.Space(8); }
                    break;
                case ItemRowKind.PrefixSide:
                    // BOTH SIDES OF THE TRADE, one to a line: the cost in the cost colour.
                    L(row.Line, row.IsDrawback ? Bad : Ink);
                    cy += LineH;
                    if (next != ItemRowKind.PrefixSide) cy += UiMetrics.Space(8);
                    break;
                case ItemRowKind.Enchant:
                    L(row.Label, Violet);
                    R("ENCHANT", Dim);
                    cy += LineH;
                    // WRAPPED, never cut: the sentence says what its number is of, and the height pass
                    // measures the same lines the draw does (the kit is present in both).
                    foreach (var wrapped in ui.WrapBig(row.Note.ToUpperInvariant(), innerW, UiTypography.Label))
                    {
                        L(wrapped, Ink);
                        cy += LineH;
                    }
                    cy += UiMetrics.Space(8);
                    break;
                case ItemRowKind.Set:
                    cy += UiMetrics.Space(10);
                    L($"{row.Label}  ·  {row.Value}", row.Tone == ItemRowTone.Muted ? Dim : Ink);
                    R("SET", Dim);
                    cy += LineH;
                    break;
            }
        }

        return cy - card.Y + PadBottom;
    }
}
