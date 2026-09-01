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
/// a filter as a pill row, a 4×6 grid whose empty cells are flat so the items emerge (§63), a footer that
/// counts. ITEM DETAIL is the inspector in the shared grammar — CATEGORY · NAME · the DECISION first (the
/// verdict, what it replaces) · WHAT IT DOES · the SET as a ladder with reached and unreached rungs in three
/// encodings (§64) · one primary EQUIP. The LOADOUT column that spent a sixth of the width on duplicates is
/// gone (§61). EQUIP BEST is EQUIP HIGHEST POWER, because that is what it compares (§65).
/// </para>
/// <para>
/// Every value is the model's: <see cref="Hunter.PowerContribution"/>, <see cref="DamageBench"/> for the
/// weapon's verdict, <see cref="ElementSets"/> for the ladder, <see cref="EnchantNeed"/> for whether an
/// enchant works with the build, <see cref="ItemClasses.WhyNot"/> for who can wear it. There is no lock
/// system, so there is no LOCK button.
/// </para>
/// </remarks>
public sealed class GearScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Quiet = UiInk.Plate;
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

    // ── LAYOUT: three columns to the page — EQUIPPED 776 · INVENTORY 496 · DETAIL 568, 16 px gutters (§61). ──
    private const int Top = 120;
    // 740 WIDE, NOT 776, AND THE REASON IS THE FRAME ART: UiKit.Panel picks its texture by aspect, and at
    // 776/920 = 0.84 this panel wore the SQUARE frame, whose side ornament reaches 57 px in — past the
    // 40 px content margin, so the GEAR POWER label was drawn under it. At 740/920 = 0.80 it wears the
    // vertical frame, whose side rail is 24. The 36 px go to the inventory.
    //
    // BOTH EDGES OF EVERY COLUMN FOLLOW THE PAGE. A fixed left edge against a page-relative right one
    // collapsed the inspector to two hundred pixels at UI SCALE 125% — the columns are shares of the
    // width that is actually there, in the brief's 42 / 27 / 31 proportion (§61), with 16 px gutters.
    private const int Gutter = 16, Margin = 24;
    private static int ColumnsWidth => UiKit.PageRight(Margin) - Margin - Gutter * 2;
    private static Rectangle EquippedPanel => new(Margin, Top, ColumnsWidth * 42 / 100, UiKit.PageBottom(40) - Top);
    private static Rectangle InventoryPanel => new(EquippedPanel.Right + Gutter, Top, ColumnsWidth * 27 / 100, UiKit.PageBottom(40) - Top);
    private static Rectangle DetailPanel => new(InventoryPanel.Right + Gutter, Top, UiKit.PageRight(Margin) - (InventoryPanel.Right + Gutter), UiKit.PageBottom(40) - Top);

    /// <summary>The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates.</summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.PaperDoll => new[] { EquippedPanel },
        TourTarget.Inventory => new[] { InventoryPanel },
        TourTarget.ItemDetail => new[] { DetailPanel },
        _ => Array.Empty<Rectangle>(),
    };

    // The doll and its eight slots: armour down the left, weapon and jewellery down the right.
    // 356, not 300: the header's two full-width lines (what this hunter may wear, and the innate) end at
    // about 346, and the slot columns used to be drawn straight through them.
    private const int SlotBox = 102, SlotPitch = 130, SlotsTop = 356;
    private static readonly (GearSlot Slot, string Label, int Column, int Row)[] SlotLayout =
    {
        (GearSlot.Helm, "HELM", 0, 0), (GearSlot.Chest, "CHEST", 0, 1), (GearSlot.Gloves, "GLOVES", 0, 2), (GearSlot.Boots, "BOOTS", 0, 3),
        (GearSlot.Weapon, "WEAPON", 1, 0), (GearSlot.Focus, "FOCUS", 1, 1), (GearSlot.Charm, "CHARM", 1, 2), (GearSlot.Ring, "RING", 1, 3),
    };
    private static Rectangle SlotRect(int column, int row)
        => new(column == 0 ? EquippedPanel.X + 46 : EquippedPanel.Right - 46 - SlotBox, SlotsTop + row * SlotPitch, SlotBox, SlotBox);
    private static Rectangle HunterBox => new(EquippedPanel.Center.X - 180, 346, 360, 534);
    private static readonly GearSlot[] AllSlots =
    {
        GearSlot.Weapon, GearSlot.Charm, GearSlot.Focus, GearSlot.Helm,
        GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots, GearSlot.Ring,
    };
    private static Rectangle FooterStrip => new(UiKit.ContentLeft(EquippedPanel), EquippedPanel.Bottom - 150, UiKit.ContentRight(EquippedPanel) - UiKit.ContentLeft(EquippedPanel), 48);
    private static Rectangle EquipBestBtn => new(UiKit.ContentLeft(EquippedPanel), EquippedPanel.Bottom - 92, (FooterStrip.Width - 16) / 2, 52);
    private static Rectangle UnequipAllBtn => new(EquipBestBtn.Right + 16, EquippedPanel.Bottom - 92, (FooterStrip.Width - 16) / 2, 52);

    // The inventory: a pill row, then 4 × 6 cells filling the content width; flat empty cells (§63).
    private const int InvCols = 4, InvRows = 6, InvGap = 8;
    private static int InvX => UiKit.ContentLeft(InventoryPanel);
    private static int InvW => UiKit.ContentRight(InventoryPanel) - InvX;
    private static int InvCell => (InvW - InvGap * (InvCols - 1)) / InvCols;
    private static int InvTop => InventoryPanel.Y + 104;
    private static Rectangle InvCellRect(int i)
        => new(InvX + i % InvCols * (InvCell + InvGap), InvTop + i / InvCols * (InvCell + InvGap), InvCell, InvCell);
    private static readonly string[] Tabs = { "ALL", "WEAPONS", "ARMOR", "ACCESSORY" };
    private static Rectangle TabRect(int i) => new(InvX + i * (InvCell + InvGap), InventoryPanel.Y + 54, InvCell, 38);

    // The inspector's actions.
    private static int DetX => UiKit.ContentLeft(DetailPanel);
    private static int DetW => UiKit.ContentRight(DetailPanel) - DetX;
    private static Rectangle EquipBtn => new(DetX, DetailPanel.Bottom - 92, DetW, 56);
    private static Rectangle VerbText(int i) => new(DetX + i * (DetW / 3), EquipBtn.Y - 36, DetW / 3, 28);
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
    private const int MenuW = 250, MenuRowH = 44, MenuHeaderH = 34;
    private Rectangle MenuRect => new(_menuAt.X, _menuAt.Y, MenuW, MenuEntries.Length * MenuRowH + MenuHeaderH + 12);

    /// <summary>The character being played — the doll shows them.</summary>
    public Character Character { get; set; } = CharacterRoster.Get(CharacterRoster.StarterId);
    private float _anim;

    // ── UPDATE ──────────────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, bool rightClicked, int wheel, Hunter hunter)
    {
        var hit = Game1.ToOverlay(mouse);
        var list = Filtered(hunter);

        // The selection is adopted only when the item is GONE from the bag, not merely off-tab.
        if (_selectedId is null || (Wearable().All(i => i.InstanceId != _selectedId) && AllSlots.Select(hunter.Worn).All(w => w?.InstanceId != _selectedId)))
            _selectedId = list.FirstOrDefault()?.InstanceId;

        var maxScroll = Math.Max(0, (list.Count - 1) / InvCols - (InvRows - 1));
        if (wheel != 0 && InventoryPanel.Contains(hit)) _invScroll -= Math.Sign(wheel);
        _invScroll = Math.Clamp(_invScroll, 0, maxScroll);

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
        var x = Math.Min(anchor.Right + 8, UiKit.PageRight(16) - MenuW);
        var y = Math.Min(anchor.Y, UiKit.PageBottom(16) - (MenuEntries.Length * MenuRowH + MenuHeaderH + 12));
        _menuAt = new Point(x, y);
    }

    private void MenuHit(Point hit)
    {
        if (_menuItemId is null || !MenuRect.Contains(hit)) return;
        for (var i = 0; i < MenuEntries.Length; i++)
        {
            var row = new Rectangle(MenuRect.X + 6, MenuRect.Y + MenuHeaderH + 6 + i * MenuRowH, MenuW - 12, MenuRowH - 4);
            if (!row.Contains(hit)) continue;
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
        var hit = Game1.ToOverlay(mouse);
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
            ItemTooltip.Draw(_ui, b, hov, hunter, hit, new Rectangle(0, 0, DetailPanel.X - 8, UiKit.Page.Height), Character);
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
        _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), box.Width - 28, UiTypography.Secondary), box.X + 14, box.Y + 10, rc, UiTypography.Secondary);
        _ui.Fill(b, new Rectangle(box.X + 8, box.Y + MenuHeaderH - 4, box.Width - 16, 1), Dim);
        for (var i = 0; i < MenuEntries.Length; i++)
        {
            var (action, label) = MenuEntries[i];
            var row = new Rectangle(box.X + 6, box.Y + MenuHeaderH + 6 + i * MenuRowH, box.Width - 12, MenuRowH - 4);
            var locked = action == ItemAction.Equip && !worn && !CanWearNow(item);
            var shown = action == ItemAction.Equip && worn ? "TAKE OFF" : locked ? "CANNOT WEAR" : label;
            var hover = row.Contains(hit) && !locked;
            if (hover) _ui.Fill(b, row, new Color(0x36, 0x2A, 0x4E));
            _ui.TextBig(b, shown, row.X + 16, row.Y + 9, locked ? UiInk.Disabled : hover ? Bone : Slate, UiTypography.Body);
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
        // THREE LINES, each with the whole width it needs: the panel's title band ends above them (the
        // square frame's crest reaches 28 px further in than the others), so the header starts clear of it.
        var hy = panel.Y + 96;
        var por = new Rectangle(x0, hy, 72, 72);
        if (_ui.Assets.GetFirst(Character.PortraitKey, "hunter_portrait") is { } p) b.Draw(p, por, Color.White);
        var powerVal = $"{hunter.PowerRating:N0}";
        var powerW = Math.Max(_ui.MeasureBig("GEAR POWER", UiTypography.Secondary), _ui.MeasureBig(powerVal, UiTypography.PrimaryValue));
        _ui.TextRightBig(b, "GEAR POWER", x1, hy + 4, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, powerVal, x1, hy + 4 + UiTypography.Pitch(UiTypography.Secondary) - 4, Bone, UiTypography.PrimaryValue);
        var tx = por.Right + 16;
        _ui.TextBig(b, _ui.ShortenBig(Character.Name.ToUpperInvariant(), x1 - powerW - 24 - tx, UiTypography.Headline), tx, hy + 6, Bone, UiTypography.Headline);
        _ui.TextBig(b, _ui.ShortenBig(ItemClasses.NameOf(Character.Class), x1 - powerW - 24 - tx, UiTypography.Body),
                    tx, hy + 6 + UiTypography.Pitch(UiTypography.Headline) - 2, UiKit.ClassColor(Character.Class), UiTypography.Body);
        // WHAT THIS HUNTER MAY WEAR — the sentence that explains every locked cell in the grid — and the
        // INNATE, the thing that most changes how a build performs, beside the gear it modifies. Each gets
        // the full content width; at the header's right column they were cut mid-clause.
        var wy = por.Bottom + 10;
        _ui.TextBig(b, _ui.ShortenBig(ItemClasses.WearsLine(Character.Class).ToUpperInvariant(), x1 - x0, UiTypography.Secondary), x0, wy, Slate, UiTypography.Secondary);
        wy += UiTypography.Pitch(UiTypography.Secondary);
        var innate = $"{Character.PassiveName.ToUpperInvariant()} — {Character.PassiveText}";
        _ui.TextBig(b, _ui.ShortenBig(innate, x1 - x0, UiTypography.Body), x0, wy, Gold, UiTypography.Body);
        Tip(new Rectangle(x0, wy, x1 - x0, UiTypography.Body + 4), hit, $"INNATE — {Character.PassiveName}: {Character.PassiveText}");

        // THE DOLL, dressed: the same idle strip the arena draws.
        var doll = HunterBox;
        _ui.GroundShadow(b, doll.Center.X, doll.Bottom - 8, (int)(doll.Width * 0.7f), 40, 0.55f);
        if (!_ui.AnimSprite(b, Character.StripKey("idle"), doll, _anim, 10f, loop: true, Color.White, -1f))
            _ui.SpriteGrounded(b, Character.SpriteKey, doll, Color.White, 0.02f);

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
            if (worn is { } w2)
            {
                if (hot) _hovered = w2;
                _forge.DrawItemIcon(b, w2, new Rectangle(box.X + 12, box.Y + 12, box.Width - 24, box.Height - 24));
                _ui.Fill(b, new Rectangle(box.X, box.Y, box.Width, 4), RarityColor(w2.Rarity));
            }
            else
            {
                // An empty slot says so in shape and in a word — not a dash.
                Ring(b, Shrink(box, 14), UiInk.Empty, 1);
                _ui.TextCenterBig(b, "EMPTY", box.Center.X, box.Center.Y - 8, UiInk.Empty, UiTypography.Caption);
            }
            if (selected) Ring(b, box, Gold, 3);   // gold = selected
            else if (hot) Ring(b, box, Slate, 2);
            _ui.TextCenterBig(b, label, box.Center.X, box.Bottom + 6, worn is not null ? Bone : Slate, UiTypography.Secondary);
        }

        // THE FOOTER STRIP: how much is worn, the average item level, and the set rungs that are ON as chips.
        var wornAll = AllSlots.Select(hunter.Worn).OfType<ItemInstance>().ToList();
        var strip = FooterStrip;
        _ui.Plate(b, strip);
        var il = wornAll.Count > 0 ? (int)Math.Round(wornAll.Average(i => i.ItemLevel)) : 0;
        _ui.TextBig(b, $"{wornAll.Count} / 8 EQUIPPED  ·  AVERAGE ITEM LEVEL {il}", strip.X + 16, strip.Y + 12, Bone, UiTypography.Body);
        var chipX = strip.Right - 12;
        foreach (var a in ElementSets.Active(hunter).GroupBy(a => a.Element).Select(g => g.OrderByDescending(a => a.Tier.Pieces).First()))
        {
            var text = $"{a.Element.ToString().ToUpperInvariant()} {a.Tier.Pieces}";
            var cw = _ui.MeasureBig(text, UiTypography.Secondary) + 24 + 20;
            chipX -= cw;
            var chip = new Rectangle(chipX, strip.Y + 9, cw, 30);
            _ui.Fill(b, chip, Gold * 0.14f);
            Ring(b, chip, Gold, 1);
            if (_ui.Assets.Get($"source_{a.Element.ToString().ToLowerInvariant()}") is { } g) b.Draw(g, new Rectangle(chip.X + 6, chip.Y + 5, 20, 20), Color.White);
            _ui.TextBig(b, text, chip.X + 30, chip.Y + 5, Gold, UiTypography.Secondary);
            Tip(chip, hit, $"{ElementSets.Name(a.Element)} — {a.Tier.Line}");
            chipX -= 8;
        }

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
        _ui.TextBig(b, "INVENTORY", InvX, panel.Y + 18, Slate, UiTypography.Secondary);
        var total = Wearable().Count;
        _ui.TextRightBig(b, $"{total} ITEM{(total == 1 ? "" : "S")}", InvX + InvW, panel.Y + 18, Slate, UiTypography.Secondary);

        for (var i = 0; i < Tabs.Length; i++)
        {
            var r = TabRect(i);
            var on = i == _tab;
            var hot = r.Contains(hit);
            _ui.Fill(b, r, on ? new Color(0x2C, 0x25, 0x44) : hot ? new Color(0x1E, 0x18, 0x2C) : new Color(0x0E, 0x0B, 0x16) * 0.8f);
            Ring(b, r, on ? Gold : hot ? Slate : Dim, on ? 2 : 1);
            _ui.TextCenterBig(b, Tabs[i], r.Center.X, r.Y + 8, on ? Gold : hot ? Bone : Slate, UiTypography.Secondary);
        }

        var list = Filtered(hunter);
        for (var vis = 0; vis < InvCols * InvRows; vis++)
        {
            var idx = _invScroll * InvCols + vis;
            var cell = InvCellRect(vis);
            _ui.Fill(b, cell, CellBg);   // flat, no border: an empty cell is nothing to look at
            if (idx >= list.Count) continue;
            var item = list[idx];
            var hot = cell.Contains(hit);
            var sel = item.InstanceId == _selectedId;
            if (hot) _hovered = item;
            if (hot && !sel) _ui.Fill(b, cell, CellHot);
            _forge.DrawItemIcon(b, item, new Rectangle(cell.X + 6, cell.Y + 6, cell.Width - 12, cell.Height - 12));
            _ui.Fill(b, new Rectangle(cell.X, cell.Y, 5, cell.Height), RarityColor(item.Rarity));   // rarity owns the left edge
            var wearable = CanWearNow(item);
            if (!wearable)
            {
                _ui.Fill(b, Shrink(cell, 5), new Color(0x0A, 0x08, 0x10, 0xB4));
                Lock(b, new Rectangle(cell.Right - 26, cell.Bottom - 28, 18, 20), UiKit.ClassColor(item.Class ?? Character.Class));
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

        var rows = (list.Count + InvCols - 1) / InvCols;
        if (rows > InvRows)
        {
            var track = new Rectangle(InvX + InvW + 6, InvTop, 6, InvRows * (InvCell + InvGap) - InvGap);
            _ui.Fill(b, track, Quiet);
            var maxScroll = rows - InvRows;
            var th = Math.Max(28, track.Height * InvRows / rows);
            var ty = track.Y + (track.Height - th) * _invScroll / Math.Max(1, maxScroll);
            _ui.Fill(b, new Rectangle(track.X, ty, track.Width, th), Slate);
        }

        if (list.Count == 0)
        {
            var ey = InvTop + 40;
            _ui.TextBig(b, total == 0 ? "NOTHING IN THE BAG" : "NOTHING IN THIS FILTER", InvX, ey, UiInk.Empty, UiTypography.Headline);
            _ui.TextBig(b, total == 0 ? "CHESTS DROP GEAR — BOSSES DROP CHESTS, AND THE VAULT OPENS THEM." : "TRY ANOTHER TAB, OR THE ALL TAB.", InvX, ey + UiTypography.Pitch(UiTypography.Headline), Slate, UiTypography.Body);
        }

        // The footer. The four verbs are named in the inspector now, so this says only what the order is.
        _ui.TextBig(b, "SORTED BY RARITY", InvX, panel.Bottom - 40, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, "RIGHT-CLICK FOR MORE", InvX + InvW, panel.Bottom - 40, Slate, UiTypography.Secondary);
    }

    // ── ITEM DETAIL: the inspector (D3). Decision first. ────────────────────────────────────────────────
    private void DrawDetail(SpriteBatch b, Point hit, Hunter hunter)
    {
        var panel = DetailPanel;
        _ui.PanelQuiet(b, panel);
        var x = DetX; var w = DetW;
        var y = panel.Y + UiTypography.PanelTitleTop;
        var floor = VerbText(0).Y - 12;

        void Head(string s) { if (y + UiTypography.Pitch(UiTypography.Secondary) > floor) return; _ui.TextBig(b, s, x, y, Slate, UiTypography.Secondary); y += UiTypography.Pitch(UiTypography.Secondary); }
        void Line(string s, Color c, int px = UiTypography.Body, int maxLines = 3)
        {
            foreach (var l in _ui.WrapBig(s, w, px).Take(maxLines))
            {
                if (y + UiTypography.Pitch(px) > floor) return;
                _ui.TextBig(b, l, x, y, c, px); y += UiTypography.Pitch(px);
            }
        }
        void Rule() { if (y + 14 < floor) { _ui.Fill(b, new Rectangle(x, y + 6, w, 1), Dim); y += 16; } }
        void Pair(string k, string v, Color vc)
        {
            if (y + UiTypography.Pitch(UiTypography.Body) > floor) return;
            _ui.TextBig(b, k, x, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, v, x + w, y, vc, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        var item = Selected(hunter);
        if (item is null)
        {
            Head("ITEM DETAIL");
            _ui.TextBig(b, "NOTHING SELECTED", x, y, UiInk.Empty, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + 4;
            Line("Click an item in the bag or on the doll to read it here. EQUIP puts it on; TAKE OFF puts it back.", Slate);
            return;
        }

        var rc = RarityColor(item.Rarity);
        var worn = IsWorn(hunter, item);
        var slot = Gear.SlotFor(item.BaseType);
        var wornPiece = slot is { } s0 ? hunter.Worn(s0) : null;
        var whyNot = ItemClasses.WhyNot(Character, item);

        // CATEGORY, then NAME with the icon.
        var cat = $"{RarityShort(item.Rarity)} {SlotWord(item.BaseType)}  ·  {item.Element?.ToString().ToUpperInvariant() ?? "PLAIN"}  ·  LEVEL {item.ItemLevel}";
        _ui.TextBig(b, _ui.ShortenBig(cat, w - 150, UiTypography.Secondary), x, y, rc, UiTypography.Secondary);
        _ui.TextRightBig(b, ItemClasses.ClassLine(item), x + w, y,
            item.Class is { } dc && ItemClasses.IsClassLocked(item.BaseType) ? UiKit.ClassColor(dc) : Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary) + 2;
        var ico = new Rectangle(x, y, 64, 64);
        _forge.DrawItemIcon(b, item, ico);
        _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), w - 76, UiTypography.Headline), ico.Right + 12, y + 4, rc, UiTypography.Headline);

        // THE DECISION FIRST: the verdict, then what it replaces.
        var vy = y + 4 + UiTypography.Pitch(UiTypography.Headline);
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
        _ui.TextBig(b, _ui.ShortenBig(verdict, w - 76, UiTypography.Body), ico.Right + 12, vy, vcol, UiTypography.Body);
        y = Math.Max(ico.Bottom, vy + UiTypography.Pitch(UiTypography.Body)) + 4;
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
            foreach (var t in tiers)
            {
                if (y + UiTypography.Pitch(UiTypography.Body) > floor) break;
                var reached = wornOf >= t.Pieces;
                var reaches = !reached && !worn && whyNot is null && would >= t.Pieces;
                var mark = new Rectangle(x + 2, y + 6, 12, 12);
                if (reached) _ui.Fill(b, mark, Bone); else Ring(b, mark, reaches ? Gold : Slate, 1);
                _ui.TextBig(b, $"{t.Pieces}", x + 22, y, reached ? Bone : reaches ? Gold : Slate, UiTypography.Body);
                var lineText = _ui.ShortenBig(t.Line, w - 44 - 64, UiTypography.Body);
                _ui.TextBig(b, lineText, x + 44, y, reached ? Bone : reaches ? Gold : Slate, UiTypography.Body);
                _ui.TextRightBig(b, reached ? "ON" : reaches ? "WEARING THIS REACHES IT" : "", x + w, y, reached ? Green : Gold, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Body);
            }
        }

        // THE VERBS as text actions, then the ONE primary action.
        for (var v = 0; v < Verbs.Length; v++)
        {
            var r = VerbText(v);
            var over = r.Contains(hit);
            _ui.TextCenterBig(b, Verbs[v].Label, r.Center.X, r.Y + 4, over ? Bone : Slate, UiTypography.Secondary);
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
