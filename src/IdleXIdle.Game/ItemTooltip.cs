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
    /// <param name="wearer">The champion reading the card; when they cannot wear it, the card says why (two lines).</param>
    public static int HeightFor(ItemInstance item, Hunter? hunter, Character? wearer = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Walk(null, null, item, hunter, wearer, new Rectangle(0, 0, Width, 0));
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

        // THROUGH THE ONE PLACEMENT RULE (PopoverPlacement, 2026-09-06): the pointer is the anchor, the
        // card hangs to its right, flips left at the edge, and is clamped into the canvas — the same
        // rule the enemy inspector and the hover tip obey.
        var h = HeightFor(item, hunter, wearer);
        var anchor = new Rectangle(at.X, at.Y - UiMetrics.Space(20), 1, 1);
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
    private static int Walk(UiKit? ui, SpriteBatch? b, ItemInstance item, Hunter? hunter, Character? wearer, Rectangle card)
    {
        var draw = ui is not null && b is not null;
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
            if (wearer is not null && !Gear.CanWear(wearer, item) && item.Class is { } locked)
            {
                // Two lines: the slot, then the two champions who can.
                L($"A {ItemClasses.NameOf(locked)}'S {ItemNaming.TypeWord(item)} —", Bad);
                cy += LineH;
                if (draw) ui!.Text(b!, ui.Shorten($"{ItemClasses.ChampionNames(locked)} CAN WEAR IT", innerW), lx, cy, Bad);
                cy += LineH;
            }
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

        // ── ITEM POWER, and what swapping would do to yours ──
        if (hunter is not null && slot is { } s)
        {
            var mine = hunter.PowerContribution(item);
            L("ITEM POWER", Dim);
            R($"{mine:N0}", Ink);
            cy += LineH + UiMetrics.Space(6);

            var worn = hunter.Worn(s);
            if (wearer is not null && !Gear.CanWear(wearer, item))
            {
                // No UPGRADE verdict on a piece this champion cannot put on — a "+21" they cannot
                // collect is a promise the EQUIP button then breaks. The same block, so the height holds.
                if (draw) ui!.Text(b!, ui.Shorten($"NOT FOR {wearer.Name} — SWITCH HUNTER ON THE ROSTER", innerW), lx, cy, Bad);
            }
            else if (worn is not null && worn.InstanceId != item.InstanceId)
            {
                var delta = mine - hunter.PowerContribution(worn);
                var word = delta > 0 ? "UPGRADE" : delta < 0 ? "DOWNGRADE" : "SIDEGRADE";
                var tone = delta > 0 ? Good : delta < 0 ? Bad : Dim;
                var deltaText = $"{delta:+#,0;-#,0;0}";
                // The verdict clears the value beside it — a long worn name is shortened, never overprinted.
                if (draw) ui!.Text(b!, ui.Shorten($"{word}  ·  REPLACES {ItemNaming.FullName(worn)}", innerW - ui.Measure(deltaText) - UiMetrics.Space(12)), lx, cy, tone);
                R(deltaText, tone);
            }
            else if (worn is not null) L("WORN", Gold);
            else L("THE SLOT IS EMPTY", Good);
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

        // ── AFFIXES — the rolled numbers ──
        var affixes = ItemAffixes.Of(item);
        foreach (var a in affixes)
        {
            L(ItemAffixes.Describe(a), Good);
            cy += LineH;
        }
        if (affixes.Count > 0) cy += UiMetrics.Space(10);

        // ── THE GEMS SET INTO IT — the player's own investment, and the card was blind to it. ──
        if (item.Gems.Count > 0)
        {
            L($"SET GEMS  {item.Gems.Count} OF {GemCraft.SocketCount(item.Rarity)}", Dim);
            cy += LineH;
            foreach (var gem in item.Gems)
            {
                L(GemCraft.NameOf(gem), Violet);
                R(GemCraft.Grant(gem), Good);
                cy += LineH;
            }
            cy += UiMetrics.Space(10);
        }

        // ── FAMILY — what the item IS by birth (item-system redesign): the built-in channel every
        //    copy of this shape carries, in its stat's own unit. ──
        if (ItemFamilies.BonusOf(item) is { } fam)
        {
            L($"{ItemNaming.TypeWord(item)}  {ItemAffixes.GrantLabel(fam.Stat, fam.Magnitude)}", Gold);
            R("BUILT IN", Dim);
            cy += LineH;
            if (item.BaseType == ItemBaseType.Weapon)
            {
                L(ItemFamilies.Blurb(item).ToUpperInvariant(), Ink);
                cy += LineH;
            }
            cy += UiMetrics.Space(10);
        }

        // ── PREFIX and ENCHANT — the NAME AND WHAT IT DOES. The half that was missing. ──
        if (GearTraits.TraitOf(item) is { } trait)
        {
            L(GearTraits.NameOf(trait), Gold);
            R("PREFIX", Dim);
            cy += LineH;
            // The REAL numbers, not the blurb: the prefix already scales with item level
            // (GearTraits.ModsFor folds ItemLevelFactor in), but a fixed sentence made it read as
            // flat — playtest: "prefix statları sabit kalmasın." An upgrade now visibly deepens it.
            L(PrefixNumbers(item), Ink);
            cy += LineH + UiMetrics.Space(8);
        }

        if (Enchantments.Of(item) is { } ench)
        {
            L(ench.Name, Violet);
            R("ENCHANT", Dim);
            cy += LineH;
            L(ench.LongBlurb.ToUpperInvariant(), Ink);
            cy += LineH + UiMetrics.Space(8);
        }

        // ── THE SET — one line. "NATURE SET  ·  3 OF 5 WORN": the element decides something in a fight
        //    now, and this is the fact that says whether wearing the piece moves a rung. The rungs
        //    themselves are on the ITEM DETAIL panel, which has the room to list them. ──
        if (hunter is not null && item.Element is { } setElement)
        {
            var worn = ElementSets.WornCount(hunter, setElement);
            cy += UiMetrics.Space(10);
            L($"{ElementSets.Name(setElement)}  ·  {ElementSets.Progress(worn)}", worn >= ElementSets.Rungs[0] ? Ink : Dim);
            R("SET", Dim);
            cy += LineH;
        }

        return cy - card.Y + PadBottom;
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
