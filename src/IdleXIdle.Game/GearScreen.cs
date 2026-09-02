using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

/// <summary>The four verbs the game has for an item. Raised here; the host routes them to the FORGE.</summary>
public enum ItemAction { Equip, Upgrade, Reforge, Salvage }

/// <summary>
/// GEAR: what the hunter wears, what is in the bag, and whether a piece is worth putting on.
/// </summary>
/// <remarks>
/// <para>
/// UX V2 P1.7 (brief §60–§65, D3, D5, D9). Three panels. EQUIPPED is the one ornate surface: the hunter's
/// header (portrait · name · class · GEAR POWER · innate), the paper doll with its eight slots, a footer that
/// says how much is worn and which set rungs are on, and the two utility actions. INVENTORY is a quiet plate:
/// a filter as a pill row, a four-column grid (three at 150 %) whose empty cells are flat so the items emerge
/// (§63), a footer that counts. ITEM DETAIL is the inspector in the shared grammar — CATEGORY · NAME · the
/// DECISION first (the verdict, what it replaces) · WHAT IT DOES · the SET as a ladder with reached and
/// unreached rungs in three encodings (§64) · one primary EQUIP. The LOADOUT column that spent a sixth of the
/// width on duplicates is gone (§61). EQUIP BEST is EQUIP HIGHEST POWER, because that is what it compares (§65).
/// </para>
/// <para>
/// Every value is the model's: <see cref="Hunter.PowerContribution"/>, <see cref="DamageBench"/> for the
/// weapon's verdict, <see cref="ElementSets"/> for the ladder, <see cref="EnchantNeed"/> for whether an
/// enchant works with the build, <see cref="ItemClasses.WhyNot"/> for who can wear it. There is no lock
/// system, so there is no LOCK button.
/// </para>
/// <para>
/// THE DENSITY PROFILE (UI polish §8–§9, LAW 7). The page is 1920×1080 at every UI SCALE; what grows is the
/// type, the rows, the boxes and the pads, all read from <see cref="UiMetrics"/> and <see cref="UiTypography"/>.
/// Every vertical rhythm here is derived from the room that is actually there: the header stack from the
/// rungs it stacks, the slot pitch from the span between header and footer, the inventory's rows from the
/// span between its tabs and its footer, the inspector's floor from the verbs and the button under it. Where
/// the content no longer fits at 150 % it reflows — slot labels step beside their boxes, the grid drops to
/// three columns and scrolls, the tab row stacks, the inspector scrolls by whole items — and never by
/// shrinking a font (§17).
/// </para>
/// </remarks>
public sealed class GearScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color CellBg = new(0x14, 0x11, 0x1C);
    private static readonly Color CellHot = new(0x2C, 0x25, 0x44);
    private static readonly Color Green = UiInk.Good;
    private static readonly Color Shadow = new(0x08, 0x07, 0x0B);

    private readonly UiKit _ui;
    private readonly ForgeScreen _forge;   // owns the item bag + the shared item-icon renderer

    public PlayerLoadout Loadout { get; set; } = null!;
    public MasteryTree Mastery { get; set; } = null!;
    public MemoryDustTree Tree { get; set; } = null!;
    /// <summary>What the equipped skills have earned, so the weapon bench measures the build the fight runs.</summary>
    public SkillProgress? SkillLevels { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    /// <summary>F7 layout-debug: panel rects, cells, selection.</summary>
    public bool DevGearDebug { get; set; }

    public GearScreen(UiKit ui, ForgeScreen forge) { _ui = ui; _forge = forge; }

    // ── LAYOUT: three columns to the page — EQUIPPED · INVENTORY · DETAIL in the brief's 42 / 27 / 31 shares (§61). ──
    // THE PAGE ANCHORS. Top, Margin and BottomInset are where the columns sit on the 1920×1080 page and do not
    // follow the profile (§8, §11: the top-level layout keeps fitting; what grows is inside it). Everything
    // that is a row, a button, an icon, a pad or a gap below is read from UiMetrics — never a per-screen factor.
    private const int Top = 120;
    private const int Margin = 24, BottomInset = 40;
    private static int Gutter => UiMetrics.Space(16);
    // THE EQUIPPED COLUMN IS 42 %, NOT 776 PX, AND THE REASON IS THE FRAME ART: UiKit.Panel picks its texture
    // by aspect, and at 776/920 = 0.84 this panel wore the SQUARE frame, whose side ornament reaches 57 px in —
    // past the 40 px content margin, so the GEAR POWER label was drawn under it. At 0.80 and under it wears
    // the vertical frame, whose side rail is 24.
    //
    // BOTH EDGES OF EVERY COLUMN FOLLOW THE PAGE. A fixed left edge against a page-relative right one
    // collapsed the inspector to two hundred pixels at UI SCALE 125% — the columns are shares of the
    // width that is actually there.
    private static int ColumnsWidth => UiKit.PageRight(Margin) - Margin - Gutter * 2;
    // THE INSPECTOR TAKES THE LARGER of its 31 % share and the house InspectorWidth — §9: "wider inspectors" at
    // the accessibility profiles, so 33 px prose keeps a readable line. At 100 % the share wins (570) and
    // nothing moves; at 150 % it is 744, and the doll and the bag split what is left in the same 42 : 27.
    private static int DetailWidth => Math.Max(ColumnsWidth * 31 / 100, UiMetrics.InspectorWidth(UiKit.Page.Width));
    private static int EquippedWidth => (ColumnsWidth - DetailWidth) * 42 / 69;
    private static int PanelHeight => UiKit.PageBottom(BottomInset) - Top;
    private static Rectangle EquippedPanel => new(Margin, Top, EquippedWidth, PanelHeight);
    private static Rectangle InventoryPanel => new(EquippedPanel.Right + Gutter, Top, ColumnsWidth - DetailWidth - EquippedWidth, PanelHeight);
    private static Rectangle DetailPanel => new(InventoryPanel.Right + Gutter, Top, UiKit.PageRight(Margin) - (InventoryPanel.Right + Gutter), PanelHeight);

    /// <summary>The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates.</summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.PaperDoll => new[] { EquippedPanel },
        TourTarget.Inventory => new[] { InventoryPanel },
        TourTarget.ItemDetail => new[] { DetailPanel },
        _ => Array.Empty<Rectangle>(),
    };

    // ── THE HEADER: portrait · name · class · GEAR POWER, then two full-width lines. Every y is derived from
    // the rung above it, so at 150 % the stack grows instead of overprinting (the 2026-09-01 capture had
    // WANDERER printed through WEARS… and the innate through the first slot ring). ──
    /// <summary>
    /// Where the header row starts: one title line and a breath under the panel's top edge — 216 at 100 %.
    /// Measured from the EDGE, not from <see cref="UiKit.BodyTopBare"/>: this panel wears the square frame,
    /// whose crest drop the title takes but the header never did, and the doll needs the 28 px more than
    /// the header needs the air.
    /// </summary>
    private static int HeaderTop => EquippedPanel.Y + UiTypography.PanelBodyTopBare + UiMetrics.Space(34);
    private static int PortraitSize => UiMetrics.Control(72);
    /// <summary>The two full-width lines (what this hunter may wear, the innate) start under the portrait.</summary>
    private static int WearsTop => HeaderTop + PortraitSize + UiMetrics.Space(10);
    // The doll and its eight slots: armour down the left, weapon and jewellery down the right.
    // THE COLUMN FITS THE PANEL IT IS IN. Written as a fixed 102-box on a 130 pitch from y 356, the four rows
    // needed 508 px and asked for 520 — which the 1080 page has and a 125 % page did not. Every figure below
    // is derived from the room between the header and the footer.
    private static int SlotsTop => WearsTop + UiTypography.Pitch(UiTypography.Secondary) + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(6);

    private static int SlotInset => UiMetrics.Space(6);
    private static int SlotSpan => FooterStrip.Y - UiMetrics.Space(16) - SlotsTop;
    private static int SlotPitch => Math.Max(1, SlotSpan / 4);
    /// <summary>What a label UNDER its box costs the row: one Secondary line and a breath.</summary>
    private static int SlotLabelRoom => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
    /// <summary>The lane a label BESIDE its box takes — the widest label (WEAPON, GLOVES) at Secondary.</summary>
    private static int SlotLabelLane => UiMetrics.Control(64);
    /// <summary>A box that still holds an icon and its ring comfortably.</summary>
    private static int SlotBoxComfortable => UiMetrics.IconSize + UiMetrics.Space(24);
    /// <summary>
    /// LABELS UNDER THEIR BOXES while four rows of box-and-label stack in the column with a comfortable box
    /// (100 %); BESIDE them — toward the doll — once they would not (125 % and 150 %). The box keeps its size
    /// and the label keeps its rung: the profile exists to make text bigger, never smaller (§17).
    /// </summary>
    private static bool SlotLabelsUnder => SlotPitch - SlotLabelRoom >= SlotBoxComfortable;
    private static int SlotBox => Math.Clamp(SlotPitch - (SlotLabelsUnder ? SlotLabelRoom : UiMetrics.Space(8)),
                                             UiMetrics.IconSmall, UiMetrics.Control(102));
    private static readonly (GearSlot Slot, string Label, int Column, int Row)[] SlotLayout =
    {
        (GearSlot.Helm, "HELM", 0, 0), (GearSlot.Chest, "CHEST", 0, 1), (GearSlot.Gloves, "GLOVES", 0, 2), (GearSlot.Boots, "BOOTS", 0, 3),
        (GearSlot.Weapon, "WEAPON", 1, 0), (GearSlot.Focus, "FOCUS", 1, 1), (GearSlot.Charm, "CHARM", 1, 2), (GearSlot.Ring, "RING", 1, 3),
    };
    /// <summary>The slot columns' inset from the panel's EDGE — the house margin and a breath (46 at 100 %), the frame's crest drop not counted, as the doll has always been placed.</summary>
    private static int SlotColumnInset => UiTypography.PanelPadX + SlotInset;
    private static Rectangle SlotRect(int column, int row)
        => new(column == 0 ? EquippedPanel.X + SlotColumnInset : EquippedPanel.Right - SlotColumnInset - SlotBox,
               SlotsTop + row * SlotPitch, SlotBox, SlotBox);
    /// <summary>The doll's frame at 100 % — sprite geometry (the idle strip is fitted by height), not a control.</summary>
    private const int DollWidth = 360;
    private static int DollTop => SlotsTop - UiMetrics.Space(10);
    /// <summary>The width between the two slot columns (and their labels, when those sit beside the boxes).</summary>
    private static int DollRoom => EquippedPanel.Width - 2 * (SlotColumnInset + SlotBox
                                                              + (SlotLabelsUnder ? 0 : UiMetrics.Space(8) + SlotLabelLane));
    private static Rectangle HunterBox
    {
        get
        {
            var w = Math.Clamp(DollRoom, UiMetrics.Control(120), DollWidth);
            return new(EquippedPanel.Center.X - w / 2, DollTop, w, Math.Max(UiMetrics.Control(120), FooterStrip.Y - UiMetrics.Space(16) - DollTop));
        }
    }
    private static readonly GearSlot[] AllSlots =
    {
        GearSlot.Weapon, GearSlot.Charm, GearSlot.Focus, GearSlot.Helm,
        GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots, GearSlot.Ring,
    };

    // ── THE FOOTER: the count strip, then the two utility buttons, stacked up from the panel's content bottom. ──
    /// <summary>Where the two buttons end: the house bottom pad and a breath above the panel's EDGE (1000 at 100 %) — the crest drop, again, not counted.</summary>
    private static int FooterButtonsBottom => EquippedPanel.Bottom - UiTypography.PanelPadBottom - UiMetrics.Space(8);
    /// <summary>One Body line and its breath: 48 at 100 %.</summary>
    private static int FooterStripH => UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(20);
    /// <summary>A set chip's height: one Secondary line in a bordered plate. 30 at 100 %.</summary>
    private static int SetChipH => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
    private static Rectangle FooterStrip
        => new(UiKit.ContentLeft(EquippedPanel), FooterButtonsBottom - UiMetrics.ButtonHeight - UiMetrics.Space(10) - FooterStripH,
               UiKit.ContentRight(EquippedPanel) - UiKit.ContentLeft(EquippedPanel), FooterStripH);
    private static Rectangle EquipBestBtn
        => new(FooterStrip.X, FooterButtonsBottom - UiMetrics.ButtonHeight, (FooterStrip.Width - Gutter) / 2, UiMetrics.ButtonHeight);
    private static Rectangle UnequipAllBtn
        => new(EquipBestBtn.Right + Gutter, EquipBestBtn.Y, FooterStrip.Width - EquipBestBtn.Width - Gutter, UiMetrics.ButtonHeight);

    // ── THE INVENTORY: a pill row, then a grid filling the content width; flat empty cells (§63). ──
    private static int InvGap => UiMetrics.Space(8);
    private static int InvX => UiKit.ContentLeft(InventoryPanel);
    private static int InvW => UiKit.ContentRight(InventoryPanel) - InvX;
    private static readonly string[] Tabs = { "ALL", "WEAPONS", "ARMOR", "ACCESSORY" };
    /// <summary>A tab's height: a Secondary line with its pad, never under the hit-target minimum (§107).</summary>
    private static int TabH => Math.Max(UiMetrics.HitTargetMinimum, UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(14));
    private static int TabsTop => InventoryPanel.Y + UiMetrics.Space(18) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(12);
    /// <summary>
    /// ONE ROW OF FOUR while the widest label fits a quarter of the width; TWO ROWS OF TWO once it does not
    /// (125 % and up — "ACCESSORY" at 24 px is wider than a 98 px tab). A stacked control (§9), not a shorter
    /// word and not a smaller one.
    /// </summary>
    private int TabRows
    {
        get
        {
            var one = (InvW - InvGap * (Tabs.Length - 1)) / Tabs.Length;
            var widest = 0;
            foreach (var t in Tabs) widest = Math.Max(widest, _ui.MeasureBig(t, UiTypography.Secondary));
            return widest + UiTypography.ChipPadX * 2 <= one ? 1 : 2;
        }
    }
    private Rectangle TabRect(int i)
    {
        var per = Tabs.Length / TabRows;
        var w = (InvW - InvGap * (per - 1)) / per;
        return new(InvX + i % per * (w + InvGap), TabsTop + i / per * (TabH + InvGap), w, TabH);
    }
    private int InvTop => TabsTop + TabRows * (TabH + InvGap) - InvGap + UiMetrics.Space(12);
    /// <summary>FOUR COLUMNS while a cell keeps a comfortable icon; THREE once it would not (150 %, §9).</summary>
    private static int InvCols => (InvW - InvGap * 3) / 4 >= UiMetrics.Control(64) ? 4 : 3;
    private static int InvCell => (InvW - InvGap * (InvCols - 1)) / InvCols;
    private const string SortedLabel = "SORTED BY RARITY", MoreLabel = "RIGHT-CLICK FOR MORE";
    /// <summary>The two footer lines share a row while both fit it; they stack once they do not (150 %).</summary>
    private bool InvFooterStacked
        => _ui.MeasureBig(SortedLabel, UiTypography.Secondary) + _ui.MeasureBig(MoreLabel, UiTypography.Secondary) + UiMetrics.Space(24) > InvW;
    private int InvFooterTop
        => InventoryPanel.Bottom - UiMetrics.Space(16) - UiTypography.Pitch(UiTypography.Secondary) * (InvFooterStacked ? 2 : 1);
    /// <summary>ROWS FROM THE ROOM: as many as fit between the tabs and the footer, never printing over either.</summary>
    private int InvRows => Math.Max(1, (InvFooterTop - UiMetrics.Space(12) - InvTop + InvGap) / (InvCell + InvGap));
    private Rectangle InvCellRect(int i)
        => new(InvX + i % InvCols * (InvCell + InvGap), InvTop + i / InvCols * (InvCell + InvGap), InvCell, InvCell);

    // ── THE INSPECTOR'S ACTIONS: the verb row and the one primary button, anchored to the panel's bottom. ──
    private static int DetX => UiKit.ContentLeft(DetailPanel);
    private static int DetW => UiKit.ContentRight(DetailPanel) - DetX;
    /// <summary>The verb row's height: a Secondary line, never under the hit-target minimum (§107).</summary>
    private static int VerbRowH => Math.Max(UiMetrics.HitTargetMinimum, UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(8));
    private static Rectangle EquipBtn
        => new(DetX, UiKit.ContentBottom(DetailPanel) - UiMetrics.Space(4) - UiMetrics.ButtonHeightPrimary, DetW, UiMetrics.ButtonHeightPrimary);
    private static Rectangle VerbText(int i) => new(DetX + i * (DetW / 3), EquipBtn.Y - UiMetrics.Space(8) - VerbRowH, DetW / 3, VerbRowH);
    private static readonly (ItemAction Action, string Label)[] Verbs =
    {
        (ItemAction.Upgrade, "UPGRADE"), (ItemAction.Reforge, "REFORGE"), (ItemAction.Salvage, "SALVAGE"),
    };

    private int _tab;
    private int _invScroll;
    private string? _selectedId;
    private ItemInstance? _hovered;
    private string? _tip;
    private Point _tipAt;

    // THE INSPECTOR'S SCROLL, in whole items from the top (§18: long inspectors scroll; the verbs and EQUIP
    // never do). What the last Draw measured tells the next Update whether there is anything below to reach.
    private int _detScroll;
    private string? _detScrollFor;
    private bool _detOverflow;
    private const int DetailScrollStep = 2;   // items per wheel notch

    /// <summary>Everything in the bag that fits a slot — whether or not THIS hunter may wear it.</summary>
    private List<ItemInstance> Wearable()
        => _forge.Inventory.Where(i => Gear.SlotFor(i.BaseType) is not null).ToList();

    private bool CanWearNow(ItemInstance item) => Gear.CanWear(Character, item);

    private static bool InTab(ItemBaseType t, int tab) => tab switch
    {
        1 => t == ItemBaseType.Weapon,
        2 => t is ItemBaseType.Helm or ItemBaseType.Chest or ItemBaseType.Gloves or ItemBaseType.Boots,
        3 => t is ItemBaseType.Charm or ItemBaseType.AbilityFocus or ItemBaseType.Ring,
        _ => true,
    };

    /// <summary>The grid's list: bag wearables on the active tab, worn pieces excluded (they live on the doll).</summary>
    private List<ItemInstance> Filtered(Hunter hunter)
        => Wearable().Where(i => InTab(i.BaseType, _tab) && !IsWorn(hunter, i))
            .OrderByDescending(i => (int)i.Rarity).ThenByDescending(i => i.ItemLevel).ToList();

    // ── ITEM VERBS: request/consume — this screen names the verb and the item; Game1 routes it. ─────────
    public (string InstanceId, ItemAction Action)? ConsumeItemAction()
    {
        var r = _itemAction;
        _itemAction = null;
        return r;
    }
    private (string InstanceId, ItemAction Action)? _itemAction;

    private string? _menuItemId;
    private Point _menuAt;
    private static readonly (ItemAction Action, string Label)[] MenuEntries =
    {
        (ItemAction.Equip, "EQUIP"), (ItemAction.Upgrade, "UPGRADE"), (ItemAction.Reforge, "REFORGE"), (ItemAction.Salvage, "SALVAGE"),
    };
    // The right-click menu: a header naming the item, then one row per verb. The rows follow the Body rung
    // they hold (44 at 100 %), so they are not tight at 150 % and each is a hit target of its own height.
    private static int MenuW => UiMetrics.Control(250);
    private static int MenuRowH => UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(16);
    private static int MenuHeaderH => UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(10);
    private static int MenuPad => UiMetrics.Space(6);
    private static int MenuH => MenuEntries.Length * MenuRowH + MenuHeaderH + MenuPad * 2;
    private Rectangle MenuRect => new(_menuAt.X, _menuAt.Y, MenuW, MenuH);
    /// <summary>One menu row — drawn and hit-tested from the same rectangle (LAW 5).</summary>
    private Rectangle MenuRow(int i)
        => new(MenuRect.X + MenuPad, MenuRect.Y + MenuHeaderH + MenuPad + i * MenuRowH, MenuW - MenuPad * 2, MenuRowH - UiMetrics.Space(4));

    /// <summary>The character being played — the doll shows them.</summary>
    public Character Character { get; set; } = CharacterRoster.Get(CharacterRoster.StarterId);
    private float _anim;

    // ── UPDATE ──────────────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, bool rightClicked, int wheel, Hunter hunter)
    {
        var hit = mouse;
        var list = Filtered(hunter);

        // The selection is adopted only when the item is GONE from the bag, not merely off-tab.
        if (_selectedId is null || (Wearable().All(i => i.InstanceId != _selectedId) && AllSlots.Select(hunter.Worn).All(w => w?.InstanceId != _selectedId)))
            _selectedId = list.FirstOrDefault()?.InstanceId;

        var rows = (list.Count + InvCols - 1) / InvCols;
        _invScroll = UiKit.Scrolled(_invScroll, wheel != 0 && InventoryPanel.Contains(hit) ? Math.Sign(wheel) : 0, InvRows, rows);
        if (wheel != 0 && DetailPanel.Contains(hit))
        {
            // Down only while the last Draw left something under the floor; up to the top.
            if (wheel < 0 && _detOverflow) _detScroll += DetailScrollStep;
            else if (wheel > 0) _detScroll = Math.Max(0, _detScroll - DetailScrollStep);
        }

        if (rightClicked)
        {
            for (var vis = 0; vis < InvCols * InvRows; vis++)
            {
                var idx = _invScroll * InvCols + vis;
                if (idx >= list.Count) break;
                if (!InvCellRect(vis).Contains(hit)) continue;
                _selectedId = list[idx].InstanceId;
                OpenMenu(list[idx].InstanceId, InvCellRect(vis));
                return;
            }
            foreach (var (slot, _, col, row) in SlotLayout)
                if (SlotRect(col, row).Contains(hit) && hunter.Worn(slot) is { } wornItem)
                {
                    _selectedId = wornItem.InstanceId;
                    OpenMenu(wornItem.InstanceId, SlotRect(col, row));
                    return;
                }
            _menuItemId = null;
            return;
        }

        if (!clicked) return;

        if (_menuItemId is not null)
        {
            MenuHit(hit);
            _menuItemId = null;
            return;
        }

        for (var i = 0; i < Tabs.Length; i++)
            if (TabRect(i).Contains(hit)) { _tab = i; _invScroll = 0; _selectedId = Filtered(hunter).FirstOrDefault()?.InstanceId; return; }

        // A worn slot: select it. (Taking off is the inspector's TAKE OFF, or the menu — one gesture, said.)
        foreach (var (slot, _, col, row) in SlotLayout)
            if (SlotRect(col, row).Contains(hit) && hunter.Worn(slot) is { } worn) { _selectedId = worn.InstanceId; return; }

        for (var vis = 0; vis < InvCols * InvRows; vis++)
        {
            var idx = _invScroll * InvCols + vis;
            if (idx >= list.Count) break;
            if (InvCellRect(vis).Contains(hit)) { _selectedId = list[idx].InstanceId; return; }
        }

        if (Selected(hunter) is { } sel)
        {
            if (EquipBtn.Contains(hit))
            {
                if (IsWorn(hunter, sel) && Gear.SlotFor(sel.BaseType) is { } ws) { hunter.Unequip(ws); Dirty = true; }
                else if (!IsWorn(hunter, sel) && CanWearNow(sel)) { hunter.Equip(sel); Dirty = true; }
                return;
            }
            for (var v = 0; v < Verbs.Length; v++)
                if (VerbText(v).Contains(hit)) { _itemAction = (sel.InstanceId, Verbs[v].Action); return; }
        }

        if (EquipBestBtn.Contains(hit)) { EquipHighestPower(hunter); return; }
        if (UnequipAllBtn.Contains(hit)) { foreach (var s in AllSlots) hunter.Unequip(s); Dirty = true; return; }
    }

    private void OpenMenu(string instanceId, Rectangle anchor)
    {
        _menuItemId = instanceId;
        var x = Math.Min(anchor.Right + UiMetrics.Space(8), UiKit.PageRight(UiMetrics.Space(16)) - MenuW);
        var y = Math.Min(anchor.Y, UiKit.PageBottom(UiMetrics.Space(16)) - MenuH);
        _menuAt = new Point(x, y);
    }

    private void MenuHit(Point hit)
    {
        if (_menuItemId is null || !MenuRect.Contains(hit)) return;
        for (var i = 0; i < MenuEntries.Length; i++)
        {
            if (!MenuRow(i).Contains(hit)) continue;
            if (MenuEntries[i].Action == ItemAction.Equip
                && Wearable().FirstOrDefault(it => it.InstanceId == _menuItemId) is { } bagItem && !CanWearNow(bagItem))
                return;   // CANNOT WEAR is not a verb
            _itemAction = (_menuItemId, MenuEntries[i].Action);
            return;
        }
    }

    private ItemInstance? Selected(Hunter hunter)
    {
        if (_selectedId is null) return null;
        return Wearable().FirstOrDefault(i => i.InstanceId == _selectedId)
            ?? AllSlots.Select(hunter.Worn).OfType<ItemInstance>().FirstOrDefault(i => i.InstanceId == _selectedId);
    }

    private static bool IsWorn(Hunter hunter, ItemInstance item)
        => Gear.SlotFor(item.BaseType) is { } s && hunter.Worn(s)?.InstanceId == item.InstanceId;

    /// <summary>
    /// EQUIP HIGHEST POWER: per slot, the wearable bag item with the highest ITEM POWER if it beats what is worn;
    /// the weapon by bench damage with THIS build. Named for what it compares (§65) — it does not see set bonuses.
    /// </summary>
    private void EquipHighestPower(Hunter hunter)
    {
        foreach (var slot in AllSlots)
        {
            if (slot == GearSlot.Weapon)
            {
                var bestWeapon = Wearable().Where(i => Gear.SlotFor(i.BaseType) == GearSlot.Weapon && CanWearNow(i))
                    .OrderByDescending(i => WeaponDps(hunter, i)).FirstOrDefault();
                if (bestWeapon is not null && WeaponDps(hunter, bestWeapon) > WeaponDps(hunter, hunter.Worn(GearSlot.Weapon)) * 1.001f)
                    hunter.Equip(bestWeapon);
                continue;
            }
            var best = Wearable().Where(i => Gear.SlotFor(i.BaseType) == slot && CanWearNow(i))
                .OrderByDescending(hunter.PowerContribution).FirstOrDefault();
            if (best is not null && hunter.PowerContribution(best) > hunter.PowerContribution(hunter.Worn(slot))) hunter.Equip(best);
        }
        Dirty = true;
    }

    // ── The weapon's real number: bench DPS with it worn, swap-and-restore, cached by everything the bench reads. ──
    private readonly Dictionary<string, float> _dpsCache = new();
    private float WeaponDps(Hunter hunter, ItemInstance? weapon)
    {
        if (Loadout is null || Mastery is null || Tree is null)
            return weapon is null ? 0f : hunter.PowerContribution(weapon);
        var others = string.Join(",", AllSlots.Where(sl => sl != GearSlot.Weapon).Select(sl => hunter.Worn(sl)?.InstanceId ?? "-"));
        var skills = string.Join(",", Loadout.Skills.Select(sk => $"{sk.Source}:{sk.SkillId}:{sk.VowId}"));
        var keystones = string.Join(",", Loadout.KeystoneIds);
        var trees = $"{Tree.OwnedIds.Count}:{string.Join(",", Tree.OwnedIds)}|{Mastery.Taken.Count}:{string.Join(",", Mastery.Taken)}";
        var key = $"{weapon?.InstanceId ?? "-"}|{weapon?.ItemLevel}|{others}|{skills}|{keystones}|{trees}|{hunter.PowerRating}|{Character.Id}";
        if (_dpsCache.TryGetValue(key, out var cached)) return cached;

        var was = hunter.Worn(GearSlot.Weapon);
        if (weapon is null) hunter.Unequip(GearSlot.Weapon); else hunter.Equip(weapon);
        float dps;
        try { dps = DamageBench.Measure(Loadout.ToBuild(Tree, Mastery, Character, SkillLevels), hunter).Dps; }
        finally { if (was is null) hunter.Unequip(GearSlot.Weapon); else hunter.Equip(was); }
        if (_dpsCache.Count > 512) _dpsCache.Clear();
        _dpsCache[key] = dps;
        return dps;
    }

    // ── DRAW ────────────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, Hunter hunter)
    {
        _anim += 1f / 60f;
        var hit = mouse;
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));

        _ui.TextCenterBig(b, "GEAR", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        _hovered = null;
        _tip = null;
        DrawEquipped(b, hit, hunter);
        DrawInventory(b, hit, hunter);
        DrawDetail(b, hit, hunter);
        DrawItemMenu(b, hit, hunter);

        // The hover card answers "what is this" without a click — never to the right over the inspector.
        if (_hovered is { } hov && _menuItemId is null)
            ItemTooltip.Draw(_ui, b, hov, hunter, hit, new Rectangle(0, 0, DetailPanel.X - UiMetrics.Space(8), UiKit.Page.Height), Character);
        else if (_tip is { } tip && _menuItemId is null) _ui.HoverTip(b, tip, _tipAt);
        if (DevGearDebug) DrawDebug(b, hunter);
    }

    /// <summary>Open the item menu on a cell — used by the headless capture to pose it.</summary>
    public void DevOpenItemMenu()
    {
        var first = Wearable().FirstOrDefault();
        if (first is null) return;
        _selectedId = first.InstanceId;
        OpenMenu(first.InstanceId, InvCellRect(0));
    }

    private void DrawItemMenu(SpriteBatch b, Point hit, Hunter hunter)
    {
        if (_menuItemId is null) return;
        var item = Wearable().FirstOrDefault(i => i.InstanceId == _menuItemId)
                   ?? AllSlots.Select(hunter.Worn).OfType<ItemInstance>().FirstOrDefault(i => i.InstanceId == _menuItemId);
        if (item is null) { _menuItemId = null; return; }

        var worn = IsWorn(hunter, item);
        var rc = RarityColor(item.Rarity);
        var box = MenuRect;
        _ui.Fill(b, new Rectangle(box.X + 4, box.Y + 4, box.Width, box.Height), new Color(0, 0, 0, 0xA0));
        _ui.Fill(b, box, new Color(0x14, 0x11, 0x1E, 0xF6));
        Ring(b, box, rc, 3);
        var hx = box.X + UiMetrics.Space(14);
        _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), box.Width - UiMetrics.Space(14) * 2, UiTypography.Secondary),
                    hx, box.Y + (MenuHeaderH - UiTypography.Secondary) / 2, rc, UiTypography.Secondary);
        _ui.Fill(b, new Rectangle(box.X + UiMetrics.Space(8), box.Y + MenuHeaderH - UiMetrics.Space(4), box.Width - UiMetrics.Space(8) * 2, 1), Dim);
        for (var i = 0; i < MenuEntries.Length; i++)
        {
            var (action, label) = MenuEntries[i];
            var row = MenuRow(i);
            var locked = action == ItemAction.Equip && !worn && !CanWearNow(item);
            var shown = action == ItemAction.Equip && worn ? "TAKE OFF" : locked ? "CANNOT WEAR" : label;
            var hover = row.Contains(hit) && !locked;
            if (hover) _ui.Fill(b, row, new Color(0x36, 0x2A, 0x4E));
            _ui.TextBig(b, shown, row.X + UiMetrics.Space(16), row.Y + (row.Height - UiTypography.Body) / 2,
                        locked ? UiInk.Disabled : hover ? Bone : Slate, UiTypography.Body);
        }
    }

    // ── EQUIPPED: the one ornate surface — who, what they wear, what it adds up to, two actions. ──────────
    private void DrawEquipped(SpriteBatch b, Point hit, Hunter hunter)
    {
        var panel = EquippedPanel;
        _ui.Panel(b, panel);
        _ui.TextCenterBig(b, "EQUIPPED", panel.Center.X, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);

        // THE HEADER ROW (moved from the LOADOUT column, §61): portrait · name · class line · GEAR POWER · innate.
        var x0 = UiKit.ContentLeft(panel);
        var x1 = UiKit.ContentRight(panel);
        var hy = HeaderTop;
        var por = new Rectangle(x0, hy, PortraitSize, PortraitSize);
        if (_ui.Assets.GetFirst(Character.PortraitKey, "hunter_portrait") is { } p) b.Draw(p, por, Color.White);
        var powerVal = $"{hunter.PowerRating:N0}";
        var powerW = Math.Max(_ui.MeasureBig("GEAR POWER", UiTypography.Secondary), _ui.MeasureBig(powerVal, UiTypography.PrimaryValue));
        _ui.TextRightBig(b, "GEAR POWER", x1, hy + UiMetrics.Space(4), Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, powerVal, x1, hy + UiTypography.Pitch(UiTypography.Secondary), Bone, UiTypography.PrimaryValue);
        var tx = por.Right + UiMetrics.Space(16);
        var nameW = x1 - powerW - UiMetrics.Space(24) - tx;
        var ny = hy + UiMetrics.Space(6);
        _ui.TextBig(b, _ui.ShortenBig(Character.Name.ToUpperInvariant(), nameW, UiTypography.Headline), tx, ny, Bone, UiTypography.Headline);
        _ui.TextBig(b, _ui.ShortenBig(ItemClasses.NameOf(Character.Class), nameW, UiTypography.Body),
                    tx, ny + UiTypography.Pitch(UiTypography.Headline), UiKit.ClassColor(Character.Class), UiTypography.Body);
        // WHAT THIS HUNTER MAY WEAR — the sentence that explains every locked cell in the grid — and the
        // INNATE, the thing that most changes how a build performs, beside the gear it modifies. Each gets
        // the full content width; at the header's right column they were cut mid-clause.
        var wy = WearsTop;
        _ui.TextBig(b, _ui.ShortenBig(ItemClasses.WearsLine(Character.Class).ToUpperInvariant(), x1 - x0, UiTypography.Secondary), x0, wy, Slate, UiTypography.Secondary);
        wy += UiTypography.Pitch(UiTypography.Secondary);
        var innate = $"{Character.PassiveName.ToUpperInvariant()} — {Character.PassiveText}";
        _ui.TextBig(b, _ui.ShortenBig(innate, x1 - x0, UiTypography.Body), x0, wy, Gold, UiTypography.Body);
        Tip(new Rectangle(x0, wy, x1 - x0, UiTypography.Pitch(UiTypography.Body)), hit, $"INNATE — {Character.PassiveName}: {Character.PassiveText}");

        // THE DOLL, dressed: the same idle strip the arena draws.
        var doll = HunterBox;
        _ui.GroundShadow(b, doll.Center.X, doll.Bottom - 8, (int)(doll.Width * 0.7f), 40, 0.55f);
        if (!_ui.AnimSprite(b, Character.StripKey("idle"), doll, _anim, 10f, loop: true, Color.White, -1f))
            _ui.SpriteGrounded(b, Character.SpriteKey, doll, Color.White, 0.02f);

        var labelsUnder = SlotLabelsUnder;
        foreach (var (slot, label, col, row) in SlotLayout)
        {
            var box = SlotRect(col, row);
            var hot = box.Contains(hit);
            var worn = hunter.Worn(slot);
            var selected = worn is not null && worn.InstanceId == _selectedId;
            var round = slot is GearSlot.Charm or GearSlot.Focus or GearSlot.Ring;
            var slotArt = round ? "ui_slot_trinket_round" : "ui_slot_empty";
            if (_ui.Assets.Get(slotArt) is { } sa) b.Draw(sa, box, worn is not null || hot ? Color.White : Color.White * 0.7f);
            else _ui.Fill(b, box, CellBg);
            // The ring art's well and its rim, in the art's own proportion (12 and 14 of its 102 source px).
            var well = box.Width * 12 / 102;
            var rim = box.Width * 14 / 102;
            var emptyInRing = false;
            if (worn is { } w2)
            {
                if (hot) _hovered = w2;
                _forge.DrawItemIcon(b, w2, new Rectangle(box.X + well, box.Y + well, box.Width - well * 2, box.Height - well * 2));
                _ui.Fill(b, new Rectangle(box.X, box.Y, box.Width, 4), RarityColor(w2.Rarity));
            }
            else
            {
                // An empty slot says so in shape and in a word — not a dash. The word sits in the ring while it
                // fits there; beside the box otherwise (a 150 % ring is narrower than EMPTY at its rung).
                Ring(b, Shrink(box, rim), UiInk.Empty, 1);
                emptyInRing = _ui.MeasureBig("EMPTY", UiTypography.Caption) <= box.Width - rim * 2;
                if (emptyInRing) _ui.TextCenterBig(b, "EMPTY", box.Center.X, box.Center.Y - UiTypography.Caption / 2, UiInk.Empty, UiTypography.Caption);
            }
            if (selected) Ring(b, box, Gold, 3);   // gold = selected
            else if (hot) Ring(b, box, Slate, 2);

            var labelInk = worn is not null ? Bone : Slate;
            if (labelsUnder)
                _ui.TextCenterBig(b, label, box.Center.X, box.Bottom + UiMetrics.Space(6), labelInk, UiTypography.Secondary);
            else
            {
                // BESIDE THE BOX, toward the doll: the label at its own rung, and EMPTY under it when the ring
                // could not hold the word.
                var emptyBeside = worn is null && !emptyInRing;
                var lh = UiTypography.Pitch(UiTypography.Secondary) + (emptyBeside ? UiTypography.Pitch(UiTypography.Caption) : 0);
                var ly = box.Center.Y - lh / 2;
                var text = _ui.ShortenBig(label, SlotLabelLane, UiTypography.Secondary);
                if (col == 0)
                {
                    var lx = box.Right + UiMetrics.Space(8);
                    _ui.TextBig(b, text, lx, ly, labelInk, UiTypography.Secondary);
                    if (emptyBeside) _ui.TextBig(b, "EMPTY", lx, ly + UiTypography.Pitch(UiTypography.Secondary), UiInk.Empty, UiTypography.Caption);
                }
                else
                {
                    var rx = box.X - UiMetrics.Space(8);
                    _ui.TextRightBig(b, text, rx, ly, labelInk, UiTypography.Secondary);
                    if (emptyBeside) _ui.TextRightBig(b, "EMPTY", rx, ly + UiTypography.Pitch(UiTypography.Secondary), UiInk.Empty, UiTypography.Caption);
                }
            }
        }

        // THE FOOTER STRIP: how much is worn, the average item level, and the set rungs that are ON as chips.
        var wornAll = AllSlots.Select(hunter.Worn).OfType<ItemInstance>().ToList();
        var strip = FooterStrip;
        _ui.Plate(b, strip);
        var chipX = strip.Right - UiMetrics.Space(12);
        var gem = SetChipH - UiMetrics.Space(5) * 2;
        foreach (var a in ElementSets.Active(hunter).GroupBy(a => a.Element).Select(g => g.OrderByDescending(a => a.Tier.Pieces).First()))
        {
            var text = $"{a.Element.ToString().ToUpperInvariant()} {a.Tier.Pieces}";
            var textX = UiMetrics.Space(6) + gem + UiMetrics.Space(4);
            var cw = textX + _ui.MeasureBig(text, UiTypography.Secondary) + UiMetrics.Space(14);
            chipX -= cw;
            var chip = new Rectangle(chipX, strip.Y + (strip.Height - SetChipH) / 2, cw, SetChipH);
            _ui.Fill(b, chip, Gold * 0.14f);
            Ring(b, chip, Gold, 1);
            if (_ui.Assets.Get($"source_{a.Element.ToString().ToLowerInvariant()}") is { } g)
                b.Draw(g, new Rectangle(chip.X + UiMetrics.Space(6), chip.Y + (SetChipH - gem) / 2, gem, gem), Color.White);
            _ui.TextBig(b, text, chip.X + textX, chip.Y + (SetChipH - UiTypography.Secondary) / 2, Gold, UiTypography.Secondary);
            Tip(chip, hit, a.Tier.Pieces == ElementSets.Rungs[^1]
                ? $"{ElementSets.Name(a.Element)} · {ElementSets.CapstoneName(a.Element)} — {a.Tier.Line}"
                : $"{ElementSets.Name(a.Element)} — {a.Tier.Line}");
            chipX -= UiMetrics.Space(8);
        }
        var il = wornAll.Count > 0 ? (int)Math.Round(wornAll.Average(i => i.ItemLevel)) : 0;
        var countX = strip.X + UiMetrics.Space(16);
        _ui.TextBig(b, _ui.ShortenBig($"{wornAll.Count} / 8 EQUIPPED  ·  AVERAGE ITEM LEVEL {il}", chipX - UiMetrics.Space(12) - countX, UiTypography.Body),
                    countX, strip.Y + (strip.Height - UiTypography.Body) / 2, Bone, UiTypography.Body);

        // TWO UTILITY ACTIONS, secondary: named for what they do (§65).
        _ui.Button(b, EquipBestBtn, "EQUIP HIGHEST POWER", hit, false, true);
        Tip(EquipBestBtn, hit, "Puts on the highest ITEM POWER in each slot, and the weapon that benches the most damage with your build. It does not count set bonuses.");
        _ui.Button(b, UnequipAllBtn, "UNEQUIP ALL", hit, false, wornAll.Count > 0);
    }

    // ── INVENTORY: a quiet plate; the items emerge, the empties do not (§63). ─────────────────────────────
    private void DrawInventory(SpriteBatch b, Point hit, Hunter hunter)
    {
        var panel = InventoryPanel;
        _ui.Plate(b, panel);
        _ui.TextBig(b, "INVENTORY", InvX, panel.Y + UiMetrics.Space(18), Slate, UiTypography.Secondary);
        var total = Wearable().Count;
        _ui.TextRightBig(b, $"{total} ITEM{(total == 1 ? "" : "S")}", InvX + InvW, panel.Y + UiMetrics.Space(18), Slate, UiTypography.Secondary);

        for (var i = 0; i < Tabs.Length; i++)
        {
            var r = TabRect(i);
            var on = i == _tab;
            var hot = r.Contains(hit);
            _ui.Fill(b, r, on ? new Color(0x2C, 0x25, 0x44) : hot ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.8f);
            Ring(b, r, on ? Gold : hot ? Slate : Dim, on ? 2 : 1);
            _ui.TextCenterBig(b, Tabs[i], r.Center.X, r.Y + (r.Height - UiTypography.Secondary) / 2, on ? Gold : hot ? Bone : Slate, UiTypography.Secondary);
        }

        var list = Filtered(hunter);
        var cols = InvCols;
        var visibleRows = InvRows;
        var iconInset = UiMetrics.Space(6);
        var lockW = UiMetrics.Control(18);
        var lockH = UiMetrics.Control(20);
        for (var vis = 0; vis < cols * visibleRows; vis++)
        {
            var idx = _invScroll * cols + vis;
            var cell = InvCellRect(vis);
            _ui.Fill(b, cell, CellBg);   // flat, no border: an empty cell is nothing to look at
            if (idx >= list.Count) continue;
            var item = list[idx];
            var hot = cell.Contains(hit);
            var sel = item.InstanceId == _selectedId;
            if (hot) _hovered = item;
            if (hot && !sel) _ui.Fill(b, cell, CellHot);
            _forge.DrawItemIcon(b, item, new Rectangle(cell.X + iconInset, cell.Y + iconInset, cell.Width - iconInset * 2, cell.Height - iconInset * 2));
            _ui.Fill(b, new Rectangle(cell.X, cell.Y, 5, cell.Height), RarityColor(item.Rarity));   // rarity owns the left edge
            var wearable = CanWearNow(item);
            if (!wearable)
            {
                _ui.Fill(b, Shrink(cell, 5), new Color(0x0A, 0x08, 0x10, 0xB4));
                Lock(b, new Rectangle(cell.Right - UiMetrics.Space(8) - lockW, cell.Bottom - UiMetrics.Space(8) - lockH, lockW, lockH),
                     UiKit.ClassColor(item.Class ?? Character.Class));
            }
            // BETTER: one green hairline inside the frame — a hint; the inspector makes the case. Judged by the
            // same ranking the inspector's verdict uses: bench damage for a weapon, ITEM POWER for the rest.
            var better = wearable && Gear.SlotFor(item.BaseType) is { } bs && hunter.Worn(bs) is { } wornPiece
                         && (bs == GearSlot.Weapon ? WeaponDps(hunter, item) > WeaponDps(hunter, wornPiece) * 1.005f
                                                    : hunter.PowerContribution(item) > hunter.PowerContribution(wornPiece));
            if (better) Ring(b, Shrink(cell, 3), new Color(0x6E, 0xC8, 0x7A, 0x9E), 1);
            if (sel) { Ring(b, cell, Gold, 3); Ring(b, Shrink(cell, 3), new Color(0x14, 0x10, 0x1A, 0x88), 1); }   // gold = selected
            else if (hot) Ring(b, cell, Slate, 2);
        }

        // THE SCROLLBAR, in the plate's right pad — drawn only when there is a row beyond the last visible one.
        var rows = (list.Count + cols - 1) / cols;
        _ui.ScrollBar(b, new Rectangle(InvX + InvW + UiMetrics.Space(6), InvTop, UiMetrics.ScrollbarWidth, visibleRows * (InvCell + InvGap) - InvGap),
                      _invScroll, visibleRows, rows);

        if (list.Count == 0)
        {
            var ey = InvTop + UiMetrics.Space(40);
            _ui.TextBig(b, total == 0 ? "NOTHING IN THE BAG" : "NOTHING IN THIS FILTER", InvX, ey, UiInk.Empty, UiTypography.Headline);
            ey += UiTypography.Pitch(UiTypography.Headline);
            var why = total == 0 ? "CHESTS DROP GEAR — BOSSES DROP CHESTS, AND THE VAULT OPENS THEM." : "TRY ANOTHER TAB, OR THE ALL TAB.";
            foreach (var l in _ui.WrapBig(why, InvW, UiTypography.Body)) { _ui.TextBig(b, l, InvX, ey, Slate, UiTypography.Body); ey += UiTypography.Pitch(UiTypography.Body); }
        }

        // The footer. The four verbs are named in the inspector now, so this says only what the order is.
        var fy = InvFooterTop;
        _ui.TextBig(b, SortedLabel, InvX, fy, Slate, UiTypography.Secondary);
        if (InvFooterStacked) fy += UiTypography.Pitch(UiTypography.Secondary);
        _ui.TextRightBig(b, MoreLabel, InvX + InvW, fy, Slate, UiTypography.Secondary);
    }

    // ── ITEM DETAIL: the inspector (D3). Decision first. ────────────────────────────────────────────────
    private void DrawDetail(SpriteBatch b, Point hit, Hunter hunter)
    {
        var panel = DetailPanel;
        _ui.PanelQuiet(b, panel);
        var x = DetX; var w = DetW;
        var top = UiKit.TitleTop(panel);
        var y = top;
        var floor = VerbText(0).Y - UiMetrics.Space(12);

        // THE SHEET SCROLLS when it is taller than the room (§18: long inspectors) — by whole items from the
        // top, so the first visible line is always a complete one, and the verbs and EQUIP stay anchored under
        // the floor (never below a scroll region). A new selection starts at the top.
        if (_detScrollFor != _selectedId) { _detScroll = 0; _detScrollFor = _selectedId; }
        var scrollable = _detScroll > 0 || _detOverflow;
        if (scrollable) w -= UiMetrics.ScrollbarWidth + UiMetrics.Space(8);
        int index = 0, shown = 0;
        var overflow = false;
        // Reserve h px for the next item: false when it is scrolled off the top or does not fit above the floor
        // (and once one does not fit, nothing after it is drawn either, so the sheet keeps its order).
        bool Take(int h)
        {
            var i = index++;
            if (i < _detScroll) return false;
            if (overflow || y + h > floor) { overflow = true; return false; }
            shown++;
            return true;
        }

        void Head(string s)
        {
            var h = UiTypography.Pitch(UiTypography.Secondary);
            if (!Take(h)) return;
            _ui.TextBig(b, s, x, y, Slate, UiTypography.Secondary); y += h;
        }
        void Line(string s, Color c, int px = 0, int maxLines = 3)
        {
            if (px == 0) px = UiTypography.Body;   // a rung is a profile-scaled property, not a constant
            var h = UiTypography.Pitch(px);
            foreach (var l in _ui.WrapBig(s, w, px).Take(maxLines))
            {
                if (!Take(h)) continue;
                _ui.TextBig(b, l, x, y, c, px); y += h;
            }
        }
        void Rule()
        {
            var h = UiMetrics.Space(16);
            if (!Take(h)) return;
            _ui.Fill(b, new Rectangle(x, y + UiMetrics.Space(6), w, 1), Dim); y += h;
        }
        void Pair(string k, string v, Color vc)
        {
            var h = UiTypography.Pitch(UiTypography.Body);
            if (!Take(h)) return;
            _ui.TextBig(b, k, x, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, v, x + w, y, vc, UiTypography.Body);
            y += h;
        }

        var item = Selected(hunter);
        if (item is null)
        {
            _detOverflow = false;
            Head("ITEM DETAIL");
            _ui.TextBig(b, "NOTHING SELECTED", x, y, UiInk.Empty, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
            Line("Click an item in the bag or on the doll to read it here. EQUIP puts it on; TAKE OFF puts it back.", Slate);
            return;
        }

        var rc = RarityColor(item.Rarity);
        var worn = IsWorn(hunter, item);
        var slot = Gear.SlotFor(item.BaseType);
        var wornPiece = slot is { } s0 ? hunter.Worn(s0) : null;
        var whyNot = ItemClasses.WhyNot(Character, item);

        // CATEGORY, with the class line beside it while both fit the row — under it once they do not (150 %).
        var cat = $"{RarityShort(item.Rarity)} {SlotWord(item.BaseType)}  ·  {item.Element?.ToString().ToUpperInvariant() ?? "PLAIN"}  ·  LEVEL {item.ItemLevel}";
        var cls = ItemClasses.ClassLine(item);
        var clsInk = item.Class is { } dc && ItemClasses.IsClassLocked(item.BaseType) ? UiKit.ClassColor(dc) : Slate;
        var catH = UiTypography.Pitch(UiTypography.Secondary);
        if (_ui.MeasureBig(cat, UiTypography.Secondary) + UiMetrics.Space(16) + _ui.MeasureBig(cls, UiTypography.Secondary) <= w)
        {
            if (Take(catH + UiMetrics.Space(2)))
            {
                _ui.TextBig(b, cat, x, y, rc, UiTypography.Secondary);
                _ui.TextRightBig(b, cls, x + w, y, clsInk, UiTypography.Secondary);
                y += catH + UiMetrics.Space(2);
            }
        }
        else
        {
            if (Take(catH)) { _ui.TextBig(b, _ui.ShortenBig(cat, w, UiTypography.Secondary), x, y, rc, UiTypography.Secondary); y += catH; }
            if (Take(catH + UiMetrics.Space(2))) { _ui.TextRightBig(b, cls, x + w, y, clsInk, UiTypography.Secondary); y += catH + UiMetrics.Space(2); }
        }

        // NAME with the icon, then THE DECISION FIRST: the verdict, then what it replaces.
        string verdict; Color vcol;
        if (worn) { verdict = "EQUIPPED"; vcol = Gold; }
        else if (whyNot is not null) { verdict = $"{Character.Name.ToUpperInvariant()} CANNOT WEAR THIS"; vcol = Ember; }
        else if (item.BaseType == ItemBaseType.Weapon)
        {
            var now = WeaponDps(hunter, wornPiece);
            var with = WeaponDps(hunter, item);
            var pct = now > 0f ? with / now - 1f : 0f;
            verdict = wornPiece is null ? "SLOT EMPTY — WEARING IT ADDS ITS DAMAGE" : $"{pct:+0%;-0%;0%} DAMAGE PER SECOND";
            vcol = wornPiece is null ? Green : pct > 0.005f ? Green : pct < -0.005f ? Ember : Slate;
        }
        else
        {
            var delta = hunter.PowerContribution(item) - hunter.PowerContribution(wornPiece);
            verdict = wornPiece is null ? $"SLOT EMPTY — +{hunter.PowerContribution(item):N0} POWER" : $"{(delta >= 0 ? "+" : "")}{delta:N0} POWER";
            vcol = wornPiece is null || delta > 0 ? Green : delta < 0 ? Ember : Slate;
        }
        var icoSize = UiMetrics.Control(64);
        var nameTop = UiMetrics.Space(4);
        var blockH = Math.Max(icoSize, nameTop + UiTypography.Pitch(UiTypography.Headline) + UiTypography.Pitch(UiTypography.Body)) + UiMetrics.Space(4);
        if (Take(blockH))
        {
            var ico = new Rectangle(x, y, icoSize, icoSize);
            _forge.DrawItemIcon(b, item, ico);
            var nx = ico.Right + UiMetrics.Space(12);
            _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), x + w - nx, UiTypography.Headline), nx, y + nameTop, rc, UiTypography.Headline);
            _ui.TextBig(b, _ui.ShortenBig(verdict, x + w - nx, UiTypography.Body), nx, y + nameTop + UiTypography.Pitch(UiTypography.Headline), vcol, UiTypography.Body);
            y += blockH;
        }
        if (!worn && wornPiece is not null && whyNot is null)
            Line($"REPLACES {ItemNaming.FullName(wornPiece).ToUpperInvariant()}", Slate, UiTypography.Secondary, 1);
        if (whyNot is not null) Line(whyNot, Ember, UiTypography.Secondary, 2);
        Rule();

        // WHAT IT DOES: power, affixes, the prefix's numbers, the enchant and whether it works with the build.
        Head("WHAT IT DOES");
        Pair("ITEM POWER", $"{hunter.PowerContribution(item):N0}", Bone);
        foreach (var af in ItemAffixes.Of(item)) Pair(ItemAffixes.StatWord(af.Stat), AffixVal(af.Stat, af.Magnitude), Green);
        if (GearTraits.TraitOf(item) is { } tr)
        {
            var effect = GearTraits.EffectOf(item);
            Pair($"PREFIX · {GearTraits.NameOf(tr).ToUpperInvariant()}", effect.Length > 0 ? effect : "", Gold);
            if (effect.Length == 0) Line(GearTraits.BlurbOf(tr), Bone, UiTypography.Body, 2);
        }
        if (Enchantments.Of(item) is { } ench)
        {
            Pair($"ENCHANT · {ench.Name.ToUpperInvariant()}", "", Bone);
            Line(ench.Blurb, Bone, UiTypography.Body, 2);
            if (ench.Needs is { } need && Loadout is not null)
            {
                var met = need.Keystone is not null || need.AnyVow || need.MetBySkills(Loadout.EquippedDefs());
                Line(met ? "WORKS WITH YOUR BUILD" : $"NEEDS {need.Label.ToUpperInvariant()} IN YOUR BUILD — UNTIL THEN IT DOES NOTHING", met ? Green : Ember, UiTypography.Secondary, 2);
            }
        }

        // THE SET, as a ladder: every rung, reached or not, in three encodings — a mark, a colour, a word (§64).
        if (item.Element is { } el)
        {
            Rule();
            var tiers = ElementSets.TiersOf(el);
            var wornOf = ElementSets.WornCount(hunter, el);
            // What wearing THIS would make the count — the same element already in the slot changes nothing.
            var would = worn ? wornOf : wornOf + (wornPiece?.Element == el ? 0 : 1);
            Head($"{ElementSets.Name(el).ToUpperInvariant()}  ·  {ElementSets.Progress(wornOf).ToUpperInvariant()}");
            var rowH = UiTypography.Pitch(UiTypography.Body);
            var mark = UiMetrics.Control(12);
            // The number column and the sentence column hang from the Body rung: one and two rungs in.
            var numX = x + UiTypography.Body;
            var textX = x + UiTypography.Body * 2;
            foreach (var t in tiers)
            {
                var reached = wornOf >= t.Pieces;
                var reaches = !reached && !worn && whyNot is null && would >= t.Pieces;
                var ink = reached ? Bone : reaches ? Gold : Slate;
                // THE CAPSTONE IS NAMED, and the name is what the rung is remembered by. A player talks
                // about running MOMENTUM, not about their fifth Body piece — so the word is on the row,
                // ahead of the sentence, and it keeps its emphasis whether or not the rung is reached:
                // an unreached capstone is the thing the ladder is FOR.
                var cap = t.Pieces == ElementSets.Rungs[^1] ? ElementSets.CapstoneName(el) : null;
                var capW = cap is null ? 0 : _ui.MeasureBig(cap + "  ", UiTypography.Body);
                // THE RIGHT LABEL'S ROOM IS MEASURED, not guessed at 64px. A fixed reserve was wrong in
                // both directions: it truncated a rung's sentence when there was no label to make room
                // for, and it was far too small for "WEARING THIS REACHES IT" when there was.
                var right = reached ? "ON" : reaches ? "WEARING THIS REACHES IT" : "";
                var rightW = right.Length == 0 ? 0 : _ui.MeasureBig(right, UiTypography.Secondary) + UiMetrics.Space(12);

                // THE RULE IS WRAPPED, NOT CUT. This column is 320 px wide and MACHINE's third rung is
                // sixty-three characters, so a single line ended "…worth 12% of your maximum h…" — a
                // sentence the player cannot finish is a rung they cannot evaluate, which is the whole
                // job of this list. Continuation lines hang under the sentence, past the number and the
                // capstone name, so the column still reads as one row per rung.
                var first = true;
                foreach (var wrapped in _ui.WrapBig(t.Line, x + w - rightW - textX - capW, UiTypography.Body))
                {
                    var lx = first ? textX + capW : textX;
                    if (Take(rowH))
                    {
                        if (first)
                        {
                            var m = new Rectangle(x + 2, y + (UiTypography.Body - mark) / 2 + 1, mark, mark);
                            if (reached) _ui.Fill(b, m, Bone); else Ring(b, m, reaches ? Gold : Slate, 1);
                            _ui.TextBig(b, $"{t.Pieces}", numX, y, ink, UiTypography.Body);
                            if (cap is not null) _ui.TextBig(b, cap, textX, y, reached ? Gold : ink, UiTypography.Body);
                            if (right.Length > 0) _ui.TextRightBig(b, right, x + w, y, reached ? Green : Gold, UiTypography.Secondary);
                        }
                        _ui.TextBig(b, wrapped, lx, y, ink, UiTypography.Body);
                        y += rowH;
                    }
                    first = false;
                }
            }
        }

        _detOverflow = overflow;
        if (scrollable || overflow)
            _ui.ScrollBar(b, new Rectangle(x + w + UiMetrics.Space(8), top, UiMetrics.ScrollbarWidth, Math.Max(1, floor - top)), _detScroll, shown, index);

        // THE VERBS as text actions, then the ONE primary action.
        for (var v = 0; v < Verbs.Length; v++)
        {
            var r = VerbText(v);
            var over = r.Contains(hit);
            _ui.TextCenterBig(b, Verbs[v].Label, r.Center.X, r.Y + (r.Height - UiTypography.Secondary) / 2, over ? Bone : Slate, UiTypography.Secondary);
            Tip(r, hit, Verbs[v].Action switch
            {
                ItemAction.Upgrade => "Take it to the FORGE to raise its level.",
                ItemAction.Reforge => "Take it to the FORGE to re-roll its enchant.",
                _ => "Take it to the FORGE to break it into materials.",
            });
        }
        var label = worn ? "TAKE OFF" : whyNot is null ? "EQUIP" : "CANNOT WEAR";
        _ui.Button(b, EquipBtn, label, hit, false, worn || whyNot is null, !worn && whyNot is null ? ButtonStyle.Primary : ButtonStyle.Secondary);
    }

    private void Tip(Rectangle r, Point hit, string text) { if (r.Contains(hit)) { _tip = text; _tipAt = hit; } }

    private void DrawDebug(SpriteBatch b, Hunter hunter)
    {
        foreach (var r in new[] { EquippedPanel, InventoryPanel, DetailPanel }) Ring(b, r, Ember, 2);
        foreach (var (_, _, col, row) in SlotLayout) Ring(b, SlotRect(col, row), Green, 2);
        for (var v = 0; v < InvCols * InvRows; v++) Ring(b, InvCellRect(v), new Color(0x40, 0xE0, 0xE0), 2);
        _ui.TextBig(b, $"tab {Tabs[_tab]}  sel {_selectedId ?? "none"}  nav GEAR  scroll {_invScroll}", 40, 108, Gold, UiTypography.Secondary);
    }

    private static Rectangle Shrink(Rectangle r, int by)
        => new(r.X + by, r.Y + by, Math.Max(1, r.Width - by * 2), Math.Max(1, r.Height - by * 2));

    private void Ring(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    /// <summary>A small padlock from rectangles, in the locking class's colour.</summary>
    private void Lock(SpriteBatch b, Rectangle r, Color c)
    {
        var bodyH = r.Height * 11 / 20;
        var body = new Rectangle(r.X, r.Bottom - bodyH, r.Width, bodyH);
        _ui.Fill(b, new Rectangle(body.X - 1, body.Y - 1, body.Width + 2, body.Height + 2), Shadow);
        _ui.Fill(b, body, c);
        var sw = Math.Max(2, r.Width / 5);
        var top = new Rectangle(r.X + 2, r.Y, r.Width - 4, sw);
        _ui.Fill(b, top, c);
        _ui.Fill(b, new Rectangle(top.X, r.Y, sw, body.Y - r.Y + 1), c);
        _ui.Fill(b, new Rectangle(top.Right - sw, r.Y, sw, body.Y - r.Y + 1), c);
        _ui.Fill(b, new Rectangle(body.Center.X - 1, body.Y + 3, 3, body.Height - 6), Shadow);
    }

    private static string SlotWord(ItemBaseType t) => t switch
    {
        ItemBaseType.Weapon => "WEAPON", ItemBaseType.Charm => "CHARM", ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.Helm => "HELM", ItemBaseType.Chest => "CHEST", ItemBaseType.Gloves => "GLOVES",
        ItemBaseType.Boots => "BOOTS", ItemBaseType.Ring => "RING", _ => "ITEM",
    };
    private static string AffixVal(AffixStat s, float m) => s switch
    {
        AffixStat.Crit => $"+{m:0.0}%",
        AffixStat.Defense => $"+{m:0}",
        _ => $"+{m * 100f:0}%",
    };
    private static string RarityShort(Rarity r) => r switch
    {
        Rarity.Common => "COMMON", Rarity.Uncommon => "UNCOMMON", Rarity.Rare => "RARE",
        Rarity.Epic => "EPIC", _ => "LEGENDARY",
    };
    private static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Common => Bone, Rarity.Uncommon => new Color(0x6E, 0xC8, 0x7A), Rarity.Rare => new Color(0x4A, 0x90, 0xD9),
        Rarity.Epic => new Color(0x8B, 0x3F, 0x82), _ => Gold,
    };
}
