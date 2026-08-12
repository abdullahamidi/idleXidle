using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The GEAR screen (nav: GEAR): a four-panel Champion Gear sheet — a loadout summary, the equipped
/// presentation with the dressed champion + eight slots, the inventory grid, and the selected-item detail
/// with a live comparison against the worn piece.
/// </summary>
/// <remarks>
/// Built to the Gear production spec (rev 1), but every value is REAL: the spec's fixture named fictional
/// slots (Head/Shoulder/Cloak/Neck/Trinket), a "Resonance Shards" currency and a "SHADOW WARDEN" armour set
/// that the game model does not have. Per the UX standard's data-honesty rules those are dropped for the
/// game's real eight slots (Weapon/Charm/Focus/Helm/Chest/Gloves/Boots/Ring), the real currencies, and a
/// real loadout summary. Items have no authored name, so identity is composed from rarity + source + slot;
/// there is no lock system, so LOCK is a disabled fallback.
/// </remarks>
public sealed class CharacterScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Bg = new(0x0E, 0x0C, 0x12);
    private static readonly Color Quiet = new(0x16, 0x12, 0x20, 0xE0);
    private static readonly Color CellBg = new(0x1C, 0x18, 0x28);
    private static readonly Color CellHot = new(0x2C, 0x25, 0x44);
    private static readonly Color Purple = new(0x8A, 0x5A, 0xC8);
    private static readonly Color Green = new(0x6E, 0xC8, 0x7A);
    private static readonly Color Shadow = new(0x08, 0x07, 0x0B);

    private readonly UiKit _ui;
    private readonly ForgeScreen _forge;   // owns the item bag + the shared item-icon renderer

    public PlayerLoadout Loadout { get; set; } = null!;
    public MasteryTree Mastery { get; set; } = null!;
    public MemoryDustTree Tree { get; set; } = null!;
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    /// <summary>F7 layout-debug (spec §18): panel rects, content-safe rects, selection, active nav.</summary>
    public bool DevGearDebug { get; set; }

    public CharacterScreen(UiKit ui, ForgeScreen forge) { _ui = ui; _forge = forge; }

    // ── Spec §4 layout: four content panels + a title band, all clearing the shared nav rail at y=934. ──
    private static readonly Rectangle TitleBand = new(0, 0, 1920, 106);
    private static readonly Rectangle LoadoutPanel = new(24, 120, 300, 790);
    private static readonly Rectangle EquippedPanel = new(340, 120, 630, 790);
    private static readonly Rectangle InventoryPanel = new(986, 120, 440, 790);
    private static readonly Rectangle DetailPanel = new(1442, 120, 454, 790);

    // Real eight slots placed in the spec's slot rectangles (§8.7): armour left, weapon+jewellery right.
    private static readonly (GearSlot Slot, string Label, Rectangle Box)[] SlotLayout =
    {
        (GearSlot.Helm,   "HELM",   new(390, 225, 102, 102)),
        (GearSlot.Chest,  "CHEST",  new(390, 355, 102, 102)),
        (GearSlot.Gloves, "GLOVES", new(390, 485, 102, 102)),
        (GearSlot.Boots,  "BOOTS",  new(390, 615, 102, 102)),
        (GearSlot.Weapon, "WEAPON", new(818, 225, 102, 102)),
        (GearSlot.Focus,  "FOCUS",  new(818, 355, 102, 102)),
        (GearSlot.Charm,  "CHARM",  new(818, 485, 102, 102)),
        (GearSlot.Ring,   "RING",   new(818, 615, 102, 102)),
    };
    private static readonly GearSlot[] OverlayOrder =
    {
        GearSlot.Chest, GearSlot.Boots, GearSlot.Gloves, GearSlot.Helm,
        GearSlot.Weapon, GearSlot.Ring, GearSlot.Charm, GearSlot.Focus,
    };
    private static readonly GearSlot[] AllSlots =
    {
        GearSlot.Weapon, GearSlot.Charm, GearSlot.Focus, GearSlot.Helm,
        GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots, GearSlot.Ring,
    };
    private static readonly Rectangle HunterBox = new(516, 210, 278, 588);   // between the slot columns

    // Inventory grid (§9.6): 4 columns, 5 rows, 82×82, gap 8, origin (1026,260).
    private const int InvCols = 4, InvRows = 5, InvCell = 82, InvGap = 8;
    private static readonly Point InvOrigin = new(1026, 260);
    private static Rectangle InvCellRect(int i)
        => new(InvOrigin.X + i % InvCols * (InvCell + InvGap), InvOrigin.Y + i / InvCols * (InvCell + InvGap), InvCell, InvCell);

    private static readonly string[] Tabs = { "ALL", "WEAPONS", "ARMOR", "ACCESSORY" };
    private static Rectangle TabRect(int i) => new(1010 + i * 100, 190, 94, 44);
    private static readonly Rectangle EquipBtn = new(1462, 848, 200, 48);
    private static readonly Rectangle LockBtn = new(1676, 848, 200, 48);
    private static readonly Rectangle EquipBestBtn = new(44, 700, 260, 52);
    private static readonly Rectangle UnequipAllBtn = new(44, 762, 260, 52);

    private HunterRigRenderer? _rigRenderer;
    private int _tab;
    private int _invScroll;
    private string? _selectedId;

    private List<ItemInstance> Wearable()
        => _forge.Inventory.Where(i => Gear.SlotFor(i.BaseType) is not null).ToList();

    private static bool InTab(ItemBaseType t, int tab) => tab switch
    {
        1 => t == ItemBaseType.Weapon,
        2 => t is ItemBaseType.Helm or ItemBaseType.Chest or ItemBaseType.Gloves or ItemBaseType.Boots,
        3 => t is ItemBaseType.Charm or ItemBaseType.AbilityFocus or ItemBaseType.Ring,
        _ => true,
    };

    private List<ItemInstance> Filtered()
        => Wearable().Where(i => InTab(i.BaseType, _tab))
            .OrderByDescending(i => (int)i.Rarity).ThenByDescending(i => i.ItemLevel).ToList();

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel, Hunter hunter)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        var list = Filtered();
        _selectedId ??= list.FirstOrDefault()?.InstanceId;   // default selection = first item (spec §9.6)

        if (wheel != 0 && InventoryPanel.Contains(hit))
        {
            var maxScroll = Math.Max(0, (list.Count - 1) / InvCols - (InvRows - 1));
            _invScroll = Math.Clamp(_invScroll - Math.Sign(wheel), 0, maxScroll);
        }

        if (!clicked) return;

        for (var i = 0; i < Tabs.Length; i++)
            if (TabRect(i).Contains(hit)) { _tab = i; _invScroll = 0; _selectedId = Filtered().FirstOrDefault()?.InstanceId; return; }

        // Click a worn slot → select it (so its detail shows) OR take it off on a second click.
        foreach (var (slot, _, box) in SlotLayout)
            if (box.Contains(hit) && hunter.Worn(slot) is { } worn)
            {
                if (_selectedId == worn.InstanceId) { hunter.Unequip(slot); Dirty = true; }
                else _selectedId = worn.InstanceId;
                return;
            }

        // Inventory cell → select.
        for (var vis = 0; vis < InvCols * InvRows; vis++)
        {
            var idx = _invScroll * InvCols + vis;
            if (idx >= list.Count) break;
            if (InvCellRect(vis).Contains(hit)) { _selectedId = list[idx].InstanceId; return; }
        }

        // Detail actions.
        if (EquipBtn.Contains(hit) && Selected(hunter) is { } sel && !IsWorn(hunter, sel))
        {
            hunter.Equip(sel); Dirty = true; return;
        }
        // LOCK is a disabled fallback (no lock system) — no action.

        if (EquipBestBtn.Contains(hit)) { EquipBest(hunter); return; }
        if (UnequipAllBtn.Contains(hit)) { foreach (var s in AllSlots) hunter.Unequip(s); Dirty = true; return; }
    }

    private ItemInstance? Selected(Hunter hunter)
    {
        if (_selectedId is null) return null;
        return Wearable().FirstOrDefault(i => i.InstanceId == _selectedId)
            ?? AllSlots.Select(hunter.Worn).OfType<ItemInstance>().FirstOrDefault(i => i.InstanceId == _selectedId);
    }

    private static bool IsWorn(Hunter hunter, ItemInstance item)
        => Gear.SlotFor(item.BaseType) is { } s && hunter.Worn(s)?.InstanceId == item.InstanceId;

    private void EquipBest(Hunter hunter)
    {
        // Real behaviour: for each slot, equip the highest-scoring available item if it beats what is worn.
        foreach (var slot in AllSlots)
        {
            var best = Wearable().Where(i => Gear.SlotFor(i.BaseType) == slot)
                .OrderByDescending(hunter.PowerContribution).FirstOrDefault();
            if (best is not null && hunter.PowerContribution(best) > hunter.PowerContribution(hunter.Worn(slot))) hunter.Equip(best);
        }
        Dirty = true;
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, Hunter hunter)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        // The shared batch-A backdrop already draws the region scene behind us; a translucent scrim keeps it
        // subdued and the UI legible (§6), rather than a flat opaque fill.
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xD8));

        // Title band (§5) — centred title + subtitle; currency is the shared top-right chrome (Game1).
        _ui.TextCenterBig(b, "CHAMPION GEAR", 960, 24, Gold, UiTypography.RegionTitle);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "EQUIPMENT   ·   INVENTORY   ·   ITEM DETAIL", 960, 80, Slate, UiTypography.Secondary);

        DrawLoadout(b, hit, hunter);
        DrawEquipped(b, hit, hunter);
        DrawInventory(b, hit, hunter);
        DrawDetail(b, hit, hunter);
        if (DevGearDebug) DrawDebug(b, hunter);
    }

    private void DrawLoadout(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, LoadoutPanel);
        var x = LoadoutPanel.X + 24;
        // Centred like EQUIPPED. Left-aligned at +24 the title sat on the frame's corner filigree.
        _ui.TextCenterBig(b, "LOADOUT", LoadoutPanel.Center.X, LoadoutPanel.Y + 26, Gold, UiTypography.SectionTitle);

        var por = new Rectangle(LoadoutPanel.X + 98, LoadoutPanel.Y + 82, 105, 105);   // §7.6
        if (_ui.Assets.Get("hunter_portrait") is { } p) b.Draw(p, por, Color.White);

        var adept = Mastery?.MasteryForm() is { } mf ? $"{FormShort(mf)} ADEPT" : "SEEKER";
        _ui.TextCenterBig(b, adept, LoadoutPanel.Center.X, LoadoutPanel.Y + 200, Bone, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, $"LEVEL {hunter.HunterLevel}", LoadoutPanel.Center.X, LoadoutPanel.Y + 234, Gold, UiTypography.Body);

        _ui.TextCenterBig(b, "GEAR POWER", LoadoutPanel.Center.X, LoadoutPanel.Y + 284, Slate, UiTypography.Secondary);
        _ui.TextCenterBig(b, $"{hunter.PowerRating:N0}", LoadoutPanel.Center.X, LoadoutPanel.Y + 312, Bone, UiTypography.PrimaryValue);

        // Real summary rows (no fake armour set / vow-bound rows — those systems don't exist).
        var worn = AllSlots.Select(hunter.Worn).OfType<ItemInstance>().ToList();
        var gearIl = worn.Count > 0 ? (int)Math.Round(worn.Average(i => i.ItemLevel)) : 0;
        var legend = worn.Count(i => i.Rarity == Rarity.Legendary);
        var src = worn.Where(i => i.Element is not null).GroupBy(i => i.Element!.Value)
            .OrderByDescending(g => g.Count()).FirstOrDefault();
        var srcTxt = src is null ? "—" : src.Key.ToString().ToUpperInvariant();
        var srcPct = src is null || worn.Count == 0 ? 0 : (int)Math.Round(100f * src.Count() / worn.Count);

        var ry = LoadoutPanel.Y + 384;
        SummaryRow(b, "EQUIPPED", $"{worn.Count} / 8", ref ry);
        SummaryRow(b, "GEAR iLVL", $"{gearIl}", ref ry);
        SummaryRow(b, "LEGENDARY", $"{legend}", ref ry);
        SummaryRow(b, "SOURCE FOCUS", $"{srcTxt} {srcPct}%", ref ry);

        // Quick actions (§7.8) — both real. EQUIP BEST fills each slot with the highest-scoring item.
        Button(b, EquipBestBtn, "EQUIP BEST", hit, true);
        Button(b, UnequipAllBtn, "UNEQUIP ALL", hit, worn.Count > 0);
    }

    private void SummaryRow(SpriteBatch b, string label, string value, ref int y)
    {
        _ui.Fill(b, new Rectangle(LoadoutPanel.X + 24, y, LoadoutPanel.Width - 48, 44), Quiet);
        _ui.TextBig(b, label, LoadoutPanel.X + 38, y + 12, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, value, LoadoutPanel.Right - 38, y + 10, Bone, UiTypography.Body);
        y += 52;
    }

    private void DrawEquipped(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, EquippedPanel);
        _ui.TextCenterBig(b, "EQUIPPED", EquippedPanel.Center.X, EquippedPanel.Y + 26, Gold, UiTypography.SectionTitle);

        // The champion, dressed (base body + one overlay per worn slot), NativeScale (fit, not stretched).
        _ui.GroundShadow(b, HunterBox.Center.X, HunterBox.Bottom - 8, (int)(HunterBox.Width * 0.7f), 40, 0.55f);
        // The paper doll is the SAME cutout rig the arena draws, so equipment shown here is literally the
        // equipment worn in the fight — one binding table, no second set of overlay art to keep in sync.
        // The old path drew a bare hunter_idle plus "overlay_<slot>_idle" sprites that were never authored,
        // so the doll showed no gear at all while the panel beside it said 8/8 EQUIPPED.
        _rigRenderer ??= new HunterRigRenderer(_ui);
        if (!_rigRenderer.Draw(b, HunterBox, null, hunter, Color.White)
            && _ui.Assets.Get("hunter_rig_base") is { } baseTex)
        {
            var sc = MathF.Min(HunterBox.Width / (float)baseTex.Width, HunterBox.Height / (float)baseTex.Height);
            var w = Math.Max(1, (int)(baseTex.Width * sc));
            var h = Math.Max(1, (int)(baseTex.Height * sc));
            b.Draw(baseTex, new Rectangle(HunterBox.Center.X - w / 2, HunterBox.Bottom - h, w, h), Color.White);
        }

        foreach (var (slot, label, box) in SlotLayout)
        {
            var hot = box.Contains(hit);
            var worn = hunter.Worn(slot);
            var selected = worn is not null && worn.InstanceId == _selectedId;
            var round = slot is GearSlot.Charm or GearSlot.Focus or GearSlot.Ring;   // §8.8 jewellery = round slot
            var slotArt = round ? "ui_slot_trinket_round" : "ui_slot_empty";
            if (_ui.Assets.Get(slotArt) is { } sa) b.Draw(sa, box, worn is not null || hot ? Color.White : Color.White * 0.7f);
            else { _ui.Fill(b, box, CellBg); }

            if (worn is { } w2)
            {
                _forge.DrawItemIcon(b, w2, new Rectangle(box.X + 12, box.Y + 12, box.Width - 24, box.Height - 24));
                _ui.Fill(b, new Rectangle(box.X, box.Y, box.Width, 4), RarityColor(w2.Rarity));
            }
            else _ui.TextCenter(b, "—", box.Center.X, box.Center.Y - 16, Dim);
            if (selected || hot) Reticle(b, box, selected ? Gold : Purple);
            _ui.TextCenterBig(b, label, box.Center.X, box.Bottom + 6, worn is not null ? Bone : Slate, UiTypography.Secondary);
        }

        // Equipped summary strip (§8.11) — no armour sets, so a real substitute: coverage + gear iLvl.
        var wornAll = AllSlots.Select(hunter.Worn).OfType<ItemInstance>().ToList();
        // Inside the panel, clear of its bottom border (EquippedPanel is 340..970, y..910). At
        // (452,838,406,56) the strip ran through the frame's bottom ornament and out its right edge.
        var strip = new Rectangle(EquippedPanel.X + 44, EquippedPanel.Bottom - 106, EquippedPanel.Width - 88, 52);
        _ui.Fill(b, strip, Quiet);
        _ui.Fill(b, new Rectangle(strip.X, strip.Y, 4, strip.Height), Purple);
        _ui.TextBig(b, $"{wornAll.Count} / 8 EQUIPPED", strip.X + 20, strip.Y + 8, Bone, UiTypography.Body);
        var il = wornAll.Count > 0 ? (int)Math.Round(wornAll.Average(i => i.ItemLevel)) : 0;
        _ui.TextRightBig(b, $"GEAR iLVL {il}", strip.Right - 20, strip.Y + 10, Gold, UiTypography.Secondary);
    }

    private void DrawInventory(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, InventoryPanel);
        _ui.TextCenterBig(b, "INVENTORY", InventoryPanel.Center.X, InventoryPanel.Y + 26, Gold, UiTypography.SectionTitle);

        for (var i = 0; i < Tabs.Length; i++)
        {
            var r = TabRect(i);
            var on = i == _tab;
            var hot = r.Contains(hit);
            var art = on ? "ui_tab_active" : "ui_tab_inactive";
            if (_ui.Assets.Get(art) is { } ta) b.Draw(ta, r, on || hot ? Color.White : Color.White * 0.7f);
            else _ui.Fill(b, r, on ? Purple * 0.5f : Quiet);
            _ui.TextCenterBig(b, Tabs[i], r.Center.X, r.Y + 12, on ? Gold : Slate, UiTypography.Secondary);
        }

        var list = Filtered();
        for (var vis = 0; vis < InvCols * InvRows; vis++)
        {
            var idx = _invScroll * InvCols + vis;
            var cell = InvCellRect(vis);
            _ui.Fill(b, cell, CellBg);
            if (idx >= list.Count) continue;
            var item = list[idx];
            var hot = cell.Contains(hit);
            var sel = item.InstanceId == _selectedId;
            var worn = IsWorn(hunter, item);
            if (hot && !sel) _ui.Fill(b, cell, CellHot);
            _forge.DrawItemIcon(b, item, new Rectangle(cell.X + 6, cell.Y + 6, cell.Width - 12, cell.Height - 12));
            _ui.Fill(b, new Rectangle(cell.X, cell.Y, cell.Width, 4), RarityColor(item.Rarity));
            if (worn) _ui.TextRightBig(b, "E", cell.Right - 8, cell.Y + 6, Gold, UiTypography.Secondary);
            else if (Gear.SlotFor(item.BaseType) is { } sl && hunter.PowerContribution(item) > hunter.PowerContribution(hunter.Worn(sl)))
                _ui.TextRightBig(b, "▲", cell.Right - 8, cell.Y + 6, Green, UiTypography.Secondary);
            if (sel) Reticle(b, cell, Gold);
        }

        // Scrollbar (§9.8) — only when the list overflows the visible rows.
        var rows = (list.Count + InvCols - 1) / InvCols;
        if (rows > InvRows)
        {
            var track = new Rectangle(InvOrigin.X + InvCols * (InvCell + InvGap) + 6, InvOrigin.Y, 8, InvRows * (InvCell + InvGap) - InvGap);
            _ui.Fill(b, track, Quiet);
            var maxScroll = rows - InvRows;
            var th = Math.Max(28, track.Height * InvRows / rows);
            var ty = track.Y + (track.Height - th) * _invScroll / Math.Max(1, maxScroll);
            _ui.Fill(b, new Rectangle(track.X, ty, track.Width, th), Purple);
        }

        // Footer (§9.9) — real slot count + sort note.
        var total = Wearable().Count;
        // +44, clear of the ornate border; at +24 the count read "4 / 64 SLOTS" with its first glyph under the frame.
        _ui.TextBig(b, $"{total} / 64 SLOTS", InventoryPanel.X + 44, InventoryPanel.Bottom - 56, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, "SORT: RARITY", InventoryPanel.Right - 44, InventoryPanel.Bottom - 56, Slate, UiTypography.Secondary);
    }

    private void DrawDetail(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, DetailPanel);
        _ui.TextCenterBig(b, "ITEM DETAIL", DetailPanel.Center.X, DetailPanel.Y + 26, Gold, UiTypography.SectionTitle);

        var item = Selected(hunter);
        if (item is null)
        {
            _ui.TextCenterBig(b, "No item selected.", DetailPanel.Center.X, DetailPanel.Y + 380, Slate, UiTypography.Body);
            return;
        }

        // Hero block (§10.5): rarity frame + large icon + source + enchant glyphs.
        var hero = new Rectangle(1510, 205, 318, 280);
        _ui.Fill(b, hero, new Color(0x12, 0x0F, 0x1C));
        _ui.Fill(b, new Rectangle(hero.X, hero.Y, hero.Width, 4), RarityColor(item.Rarity));
        _forge.DrawItemIcon(b, item, new Rectangle(hero.Center.X - 96, hero.Y + 24, 192, 192));
        if (item.Element is { } el && _ui.Assets.Get($"source_{el.ToString().ToLowerInvariant()}") is { } sg)
            b.Draw(sg, new Rectangle(hero.Right - 60, hero.Y + 16, 44, 44), Color.White);
        if (Enchantments.Of(item) is { } ench2) _ui.Diamond(b, new Rectangle(hero.Right - 58, hero.Y + 72, 40, 40), Purple);
        _ui.TextCenterBig(b, RarityShort(item.Rarity) + " " + SlotWord(item.BaseType), hero.Center.X, hero.Bottom - 40, RarityColor(item.Rarity), UiTypography.Body);

        // Identity (§10.6) — the rolled name: PREFIX (dominant affix) + ELEMENT + TYPE, e.g. "FURIOUS SHADOW BLADE".
        _ui.TextBig(b, ItemNaming.FullName(item), DetailPanel.X + 24, hero.Bottom + 20, Bone, UiTypography.PanelTitle);
        var ench = Enchantments.Of(item);
        _ui.TextBig(b, $"{(item.Element?.ToString().ToUpperInvariant() ?? "PLAIN")}  ·  ENCHANT: {(ench?.Name ?? "NONE")}",
            DetailPanel.X + 24, hero.Bottom + 54, Purple, UiTypography.Secondary);

        // Stat list (§10.7): ITEM POWER + the item's real affixes.
        var sy = hero.Bottom + 100;
        StatRow(b, "ITEM POWER", $"{hunter.PowerContribution(item):N0}", ref sy, Bone);
        foreach (var af in ItemAffixes.Of(item))
            StatRow(b, AffixLabel(af.Stat), AffixVal(af.Stat, af.Magnitude), ref sy, Green);
        if (GearTraits.TraitOf(item) is { } tr) StatRow(b, "TRAIT", GearTraits.NameOf(tr), ref sy, Gold);

        // Comparison strip (§10.8) — the matching worn item + the net power delta. Concise, not a full sim.
        var slot = Gear.SlotFor(item.BaseType);
        var worn = slot is { } s ? hunter.Worn(s) : null;
        // Below whatever the stat list actually ended at, not at a fixed y. The rows are dynamic —
        // affix count and the optional TRAIT line move the bottom — so a constant put the comparison
        // strip through the last row on any item with a trait.
        // Clamped, not just floored: the buttons live at y=848, so the strip has a hard ceiling of
        // 796 no matter how long the stat list got.
        var cmpY = Math.Clamp(sy + 8, 760, 796);
        var cmp = new Rectangle(DetailPanel.X + 44, cmpY, DetailPanel.Width - 88, 44);
        _ui.Fill(b, cmp, Quiet);
        if (IsWorn(hunter, item))
            _ui.TextBig(b, "EQUIPPED", cmp.X + 16, cmp.Y + 12, Gold, UiTypography.Secondary);
        else
        {
            var delta = hunter.PowerContribution(item) - hunter.PowerContribution(worn);
            _ui.TextBig(b, worn is null ? "SLOT EMPTY" : $"EQUIPPED: {(worn.Element?.ToString().ToUpperInvariant() ?? "PLAIN")} {SlotWord(worn.BaseType)}",
                cmp.X + 16, cmp.Y + 12, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{(delta >= 0 ? "+" : "")}{delta} POWER", cmp.Right - 16, cmp.Y + 10,
                delta > 0 ? Green : delta < 0 ? Ember : Slate, UiTypography.Body);
        }

        // Actions (§10.9): EQUIP (real). LOCK disabled — no lock system in the model.
        Button(b, EquipBtn, IsWorn(hunter, item) ? "EQUIPPED" : "EQUIP", hit, !IsWorn(hunter, item));
        Button(b, LockBtn, "LOCK", hit, false);
    }

    private void StatRow(SpriteBatch b, string label, string value, ref int y, Color valColor)
    {
        // +44 inset and a 38px pitch: at +24 the labels sat on the frame's filigree, and at 44px a
        // Legendary with four affixes plus a TRAIT line ran the list into the comparison strip below.
        _ui.TextBig(b, label, DetailPanel.X + 44, y, Slate, UiTypography.Body);
        _ui.TextRightBig(b, value, DetailPanel.Right - 44, y, valColor, UiTypography.Body);
        _ui.Fill(b, new Rectangle(DetailPanel.X + 44, y + 28, DetailPanel.Width - 88, 2), Dim * 0.6f);
        y += 38;
    }

    private void Button(SpriteBatch b, Rectangle r, string label, Point hit, bool enabled)
    {
        var hot = enabled && r.Contains(hit);
        var key = !enabled ? "ui_button_disabled" : hot ? "ui_button_primary" : "ui_button_secondary";
        if (_ui.Assets.Get(key) is { } t) b.Draw(t, r, Color.White);
        else _ui.Fill(b, r, !enabled ? Dim : hot ? Purple * 0.6f : Quiet);
        _ui.TextCenterBig(b, label, r.Center.X, r.Center.Y - 12, !enabled ? Slate : hot ? Gold : Bone, UiTypography.Body);
    }

    private void DrawDebug(SpriteBatch b, Hunter hunter)
    {
        foreach (var r in new[] { LoadoutPanel, EquippedPanel, InventoryPanel, DetailPanel })
            Outline(b, r, Ember);
        foreach (var (_, _, box) in SlotLayout) Outline(b, box, Green);
        for (var v = 0; v < InvCols * InvRows; v++) Outline(b, InvCellRect(v), new Color(0x40, 0xE0, 0xE0));
        _ui.TextBig(b, $"tab {Tabs[_tab]}  sel {_selectedId ?? "none"}  nav GEAR  scroll {_invScroll}",
            40, 108, Gold, UiTypography.Secondary);
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), c);
    }

    private void Reticle(SpriteBatch b, Rectangle r, Color c)
    {
        const int len = 20, t = 4;
        _ui.Fill(b, new Rectangle(r.X, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Bottom - len, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Bottom - len, t, len), c);
    }

    private static string FormShort(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };
    private static string SlotWord(ItemBaseType t) => t switch
    {
        ItemBaseType.Weapon => "WEAPON", ItemBaseType.Charm => "CHARM", ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.Helm => "HELM", ItemBaseType.Chest => "CHEST", ItemBaseType.Gloves => "GLOVES",
        ItemBaseType.Boots => "BOOTS", ItemBaseType.Ring => "RING", _ => "ITEM",
    };
    private static string AffixLabel(AffixStat s) => s switch
    {
        AffixStat.Damage => "DAMAGE", AffixStat.Health => "HEALTH", AffixStat.SkillRate => "SKILL RATE",
        AffixStat.Haul => "HAUL", AffixStat.Crit => "CRITICAL", _ => "DEFENSE",
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
