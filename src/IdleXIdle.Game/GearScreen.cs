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

/// <summary>The four verbs the game has for a single item, as one menu.</summary>
/// <remarks>
/// Named on the ITEM rather than on the screen that owns them. UPGRADE, REFORGE and SALVAGE all live in
/// the Forge, and a player holding a new drop in the gear screen had no way to reach any of them without
/// knowing that — which is knowledge about the software, not about the game.
/// </remarks>
public enum ItemAction { Equip, Upgrade, Reforge, Salvage }

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
public sealed class GearScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    /// <summary>
    /// A rung not yet reached. Between Slate and Dim: Dim measured under 2:1 on this panel and an
    /// unreached rung is still something to read — it is the reason to wear one more piece.
    /// </summary>
    private static readonly Color Faint = UiInk.Secondary;
    private static readonly Color Bg = new(0x0E, 0x0C, 0x12);
    private static readonly Color Quiet = UiInk.Plate;
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

    /// <summary>
    /// What the woven skills have earned. Set by the host so this screen's damage readout is measured
    /// against the same build the fight runs — a preview that ignores a bought variation lies.
    /// </summary>
    public SkillProgress? SkillLevels { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    /// <summary>F7 layout-debug (spec §18): panel rects, content-safe rects, selection, active nav.</summary>
    public bool DevGearDebug { get; set; }

    public GearScreen(UiKit ui, ForgeScreen forge) { _ui = ui; _forge = forge; }

    // ── Spec §4 layout: four content panels + a title band, all clearing the shared nav rail at y=934. ──
    private static readonly Rectangle TitleBand = new(0, 0, 1920, 106);
    // 890, not 790 — bottom 1010, which is canvas 905 and clear of everything. The four columns keep a
    // shared baseline and the page stops looking like a strip pinned to the top of the picture. Aspects
    // 0.337 / 0.708 / 0.494 / 0.510 all stay under 0.82, so every panel keeps ui_panel_vertical.
    private static readonly Rectangle LoadoutPanel = new(24, 120, 300, 890);
    private static readonly Rectangle EquippedPanel = new(340, 120, 630, 890);
    private static readonly Rectangle InventoryPanel = new(986, 120, 440, 890);
    private static readonly Rectangle DetailPanel = new(1442, 120, 454, 890);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.PaperDoll => new[] { EquippedPanel },
        TourTarget.Inventory => new[] { InventoryPanel },
        TourTarget.ItemDetail => new[] { DetailPanel },
        _ => Array.Empty<Rectangle>(),
    };

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
    /// <summary>The item under the pointer this frame, or null. Set during draw, used after it.</summary>
    /// <remarks>
    /// Established every frame rather than tracked on mouse-move, because the grid scrolls, filters and
    /// re-sorts under a stationary pointer — a remembered id would go on describing an item that is no
    /// longer in that cell.
    /// </remarks>
    private ItemInstance? _hovered;

    // THE GRID, THE TABS AND THE FOOTER NOW SHARE ONE LEFT EDGE — the panel's margin. They used to
    // sit at 1026, 1010 and 1026 respectively: three columns, three edges, on a panel 440 px wide.
    // The cell grew from 82 to 90 so four columns plus their gaps span the content width exactly, which
    // is what makes the right margin equal the left one instead of eight pixels wider.
    private const int InvCols = 4, InvRows = 5, InvGap = 8;
    private static readonly int InvCell =
        (InventoryPanel.Width - UiKit.PadX(InventoryPanel) * 2 - InvGap * (InvCols - 1)) / InvCols;
    private static readonly Point InvOrigin = new(UiKit.ContentLeft(InventoryPanel), 260);
    private static Rectangle InvCellRect(int i)
        => new(InvOrigin.X + i % InvCols * (InvCell + InvGap), InvOrigin.Y + i / InvCols * (InvCell + InvGap), InvCell, InvCell);

    private static readonly string[] Tabs = { "ALL", "WEAPONS", "ARMOR", "ACCESSORY" };
    private static Rectangle TabRect(int i)
        => new(InvOrigin.X + i * (InvCell + InvGap), 190, InvCell, 44);
    // DERIVED FROM THEIR PANELS, not written as absolutes. At fixed y they would have floated a hundred
    // pixels above the foot of a panel that grew underneath them — which is how a control stops looking
    // like it belongs to the thing it acts on.
    // Both ends of a button row sit on the panel's own margin — a pair written as X+20 / Right-220
    // was symmetric only for as long as nobody changed the margin, and the margin changed.
    private const int ButtonGap = 12;
    private static readonly int DetailButtonW =
        (DetailPanel.Width - UiKit.PadX(DetailPanel) * 2 - ButtonGap) / 2;
    private static readonly Rectangle EquipBtn =
        new(UiKit.ContentLeft(DetailPanel), DetailPanel.Bottom - 100, DetailButtonW, 48);
    private static readonly Rectangle LockBtn =
        new(UiKit.ContentRight(DetailPanel) - DetailButtonW, DetailPanel.Bottom - 100, DetailButtonW, 48);
    private static readonly int LoadoutButtonW = LoadoutPanel.Width - UiKit.PadX(LoadoutPanel) * 2;
    private static readonly Rectangle EquipBestBtn =
        new(UiKit.ContentLeft(LoadoutPanel), LoadoutPanel.Bottom - 162, LoadoutButtonW, 52);
    private static readonly Rectangle UnequipAllBtn =
        new(UiKit.ContentLeft(LoadoutPanel), LoadoutPanel.Bottom - 100, LoadoutButtonW, 52);

    private int _tab;
    private int _invScroll;
    private string? _selectedId;

    /// <summary>Everything in the bag that fits a slot — whether or not THIS champion may wear it.</summary>
    /// <remarks>
    /// Still the "do I own it at all" question. A piece of another class stays in this list and in the
    /// grid, dimmed and locked, because a bag that hides what you cannot wear teaches the player their
    /// drops vanished; a bag that shows it teaches them the roster.
    /// </remarks>
    private List<ItemInstance> Wearable()
        => _forge.Inventory.Where(i => Gear.SlotFor(i.BaseType) is not null).ToList();

    /// <summary>Can the champion on the doll wear this? The class rule, asked of the active character.</summary>
    private bool CanWearNow(ItemInstance item) => Gear.CanWear(Character, item);

    private static bool InTab(ItemBaseType t, int tab) => tab switch
    {
        1 => t == ItemBaseType.Weapon,
        2 => t is ItemBaseType.Helm or ItemBaseType.Chest or ItemBaseType.Gloves or ItemBaseType.Boots,
        3 => t is ItemBaseType.Charm or ItemBaseType.AbilityFocus or ItemBaseType.Ring,
        _ => true,
    };

    /// <summary>The grid's list: bag wearables on the active tab — WORN pieces excluded.</summary>
    /// <remarks>
    /// Playtest: "giyili eşya envanterden kaybolsun o çok kafa karıştırıyor, direkt üstüme giyilsin,
    /// çıkarınca envantere geri düşsün." Equip moves the item ONTO the doll visually; unequip drops it
    /// back into the grid. (The item still lives in the Forge's inventory model either way —
    /// <see cref="Wearable"/> stays the "do I own it at all" question the selection logic asks.)
    /// </remarks>
    private List<ItemInstance> Filtered(Hunter hunter)
        => Wearable().Where(i => InTab(i.BaseType, _tab) && !IsWorn(hunter, i))
            .OrderByDescending(i => (int)i.Rarity).ThenByDescending(i => i.ItemLevel).ToList();

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    /// <summary>What the item menu asked the host to do, taken once.</summary>
    /// <remarks>
    /// Request/consume, like every other cross-screen action here: this screen must not know how to open
    /// the Forge or how to play its animation. It names the verb and the item; Game1 routes it.
    /// </remarks>
    public (string InstanceId, ItemAction Action)? ConsumeItemAction()
    {
        var r = _itemAction;
        _itemAction = null;
        return r;
    }

    private (string InstanceId, ItemAction Action)? _itemAction;

    // The open context menu: which item, and where it was opened. Null = closed.
    private string? _menuItemId;
    private Point _menuAt;

    private static readonly (ItemAction Action, string Label)[] MenuEntries =
    {
        (ItemAction.Equip, "EQUIP"),
        (ItemAction.Upgrade, "UPGRADE"),
        (ItemAction.Reforge, "REFORGE"),
        (ItemAction.Salvage, "SALVAGE"),
    };

    private const int MenuW = 250, MenuRowH = 44, MenuHeaderH = 34;
    private Rectangle MenuRect => new(_menuAt.X, _menuAt.Y, MenuW, MenuEntries.Length * MenuRowH + MenuHeaderH + 12);

    /// <summary>The character being played — the doll shows them, not a fixed hunter.</summary>
    public Character Character { get; set; } = CharacterRoster.Get(CharacterRoster.StarterId);

    /// <summary>Seconds, so the doll's idle loop plays. Advanced by Draw, which is the only clock it needs.</summary>
    private float _anim;

    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, bool rightClicked, int wheel, Hunter hunter)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        var list = Filtered(hunter);

        // THE SELECTION IS ADOPTED ONLY WHEN THE ITEM IS GONE — not merely when it is off-tab.
        //
        // The first version tested against the FILTERED list, which made it steal a selection the player
        // had just made on the paper doll: a worn Charm selected while the WEAPONS tab is up is absent
        // from `list` and was instantly replaced, so clicking a doll slot on any tab but ALL could not
        // hold its selection and the click-twice-to-unequip gesture died with it. `Wearable()` is the
        // real question — is this item still in the bag at all — and a tab change re-selects explicitly
        // where the tab is switched, because that IS a deliberate act rather than an accident of filter.
        if (_selectedId is null || Wearable().All(i => i.InstanceId != _selectedId))
            _selectedId = list.FirstOrDefault()?.InstanceId;

        // CLAMPED EVERY FRAME, not only when the wheel moves. The list shrinks under this screen —
        // items are equipped, merged and salvaged from the menu it opens — so a scroll that was legal
        // last frame can point past the end of the grid, and the page then draws twenty empty cells
        // with no scrollbar while the footer underneath still reports a full bag.
        var maxScroll = Math.Max(0, (list.Count - 1) / InvCols - (InvRows - 1));
        if (wheel != 0 && InventoryPanel.Contains(hit)) _invScroll -= Math.Sign(wheel);
        _invScroll = Math.Clamp(_invScroll, 0, maxScroll);

        // ── THE ITEM MENU. Right-click anything wearable — worn or in the bag — and the four verbs the
        //    game has for an item are right there, instead of scattered across two screens. ────────
        if (rightClicked)
        {
            var list2 = Filtered(hunter);
            for (var vis = 0; vis < InvCols * InvRows; vis++)
            {
                var idx = _invScroll * InvCols + vis;
                if (idx >= list2.Count) break;
                if (!InvCellRect(vis).Contains(hit)) continue;
                _selectedId = list2[idx].InstanceId;
                OpenMenu(list2[idx].InstanceId, InvCellRect(vis));
                return;
            }

            foreach (var (slot, _, box) in SlotLayout)
                if (box.Contains(hit) && hunter.Worn(slot) is { } wornItem)
                {
                    _selectedId = wornItem.InstanceId;
                    OpenMenu(wornItem.InstanceId, box);
                    return;
                }

            _menuItemId = null;   // right-click on nothing closes it
            return;
        }

        if (!clicked) return;

        // A click anywhere resolves the open menu FIRST — otherwise the click also lands on whatever
        // sits under the menu, and choosing SALVAGE would silently re-select an item at the same time.
        if (_menuItemId is not null)
        {
            var chosen = MenuHit(hit);
            _menuItemId = null;
            if (chosen is null) return;
            return;
        }

        for (var i = 0; i < Tabs.Length; i++)
            if (TabRect(i).Contains(hit)) { _tab = i; _invScroll = 0; _selectedId = Filtered(hunter).FirstOrDefault()?.InstanceId; return; }

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

        // Detail actions. A piece of another class cannot be put on from here — the button is drawn
        // disabled and the detail panel says who can wear it — so the click does nothing.
        if (EquipBtn.Contains(hit) && Selected(hunter) is { } sel && !IsWorn(hunter, sel) && CanWearNow(sel))
        {
            hunter.Equip(sel); Dirty = true; return;
        }
        // LOCK is a disabled fallback (no lock system) — no action.

        if (EquipBestBtn.Contains(hit)) { EquipBest(hunter); return; }
        if (UnequipAllBtn.Contains(hit)) { foreach (var s in AllSlots) hunter.Unequip(s); Dirty = true; return; }
    }

    private static string TrimName(string n) => n.Length <= 20 ? n : n[..19] + "\u2026";

    /// <summary>Open the menu beside a cell, nudged so it never runs off the canvas.</summary>
    private void OpenMenu(string instanceId, Rectangle anchor)
    {
        _menuItemId = instanceId;
        var x = Math.Min(anchor.Right + 8, 1920 - MenuW - 16);
        var y = Math.Min(anchor.Y, 1080 - (MenuEntries.Length * MenuRowH + MenuHeaderH + 12) - 16);
        _menuAt = new Point(x, y);
    }

    /// <summary>Which entry a click landed on, raising the request. Null if it missed the menu.</summary>
    private ItemAction? MenuHit(Point hit)
    {
        if (_menuItemId is null || !MenuRect.Contains(hit)) return null;

        for (var i = 0; i < MenuEntries.Length; i++)
        {
            var row = new Rectangle(MenuRect.X + 6, MenuRect.Y + MenuHeaderH + 6 + i * MenuRowH, MenuW - 12, MenuRowH - 4);
            if (!row.Contains(hit)) continue;
            // A dimmed CANNOT WEAR row is not a verb: the click closes the menu and raises nothing.
            if (MenuEntries[i].Action == ItemAction.Equip
                && Wearable().FirstOrDefault(it => it.InstanceId == _menuItemId) is { } bagItem
                && !CanWearNow(bagItem))
                return null;
            _itemAction = (_menuItemId, MenuEntries[i].Action);
            return MenuEntries[i].Action;
        }
        return null;
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
            if (slot == GearSlot.Weapon)
            {
                // THE WEAPON IS RANKED BY THE FIGHT, not by the rating: DamageBench with THIS build, so an
                // archer's build is handed the bow (ItemFamilies.FavouredForms) and the number here is
                // the same one the WEAVE screen prints. Playtest 2026-08-23: "build'ime göre silah
                // önerileri ve DPS verileri uyuşmalı."
                // Only what THIS champion can wear — a WARDEN's best bow is not a candidate.
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

    // ── The weapon's real number: bench DPS with it worn. ─────────────────────────────────────────
    private readonly Dictionary<string, float> _dpsCache = new();

    /// <summary>
    /// Damage per second the current build does with <paramref name="weapon"/> worn (null = an empty
    /// weapon slot), everything else as it is — the sim's own number (DamageBench), swap-and-restore.
    /// </summary>
    /// <remarks>
    /// Cached by everything that feeds the bench (the candidate, the rest of the worn set, the woven
    /// skills, the hunter's level) so a hover costs one bench run, not one per frame. Falls back to the
    /// rating's contribution when the screen has not been handed a build yet.
    /// </remarks>
    private float WeaponDps(Hunter hunter, ItemInstance? weapon)
    {
        if (Loadout is null || Mastery is null || Tree is null)
            return weapon is null ? 0f : hunter.PowerContribution(weapon);
        var others = string.Join(",", AllSlots.Where(sl => sl != GearSlot.Weapon).Select(sl => hunter.Worn(sl)?.InstanceId ?? "-"));
        var skills = string.Join(",", Loadout.Skills.Select(sk => $"{sk.Source}:{sk.SkillId}:{sk.VowId}"));
        // Everything ToBuild and the bench read: the candidate, the rest of the worn set, the woven
        // skills, the socketed keystones, the two trees, the hunter's stats and the character. A key
        // that missed the trees served a stale number after a Dust or mastery buy (review, 2026-08-23).
        var keystones = string.Join(",", Loadout.KeystoneIds);
        var trees = $"{Tree.OwnedIds.Count}:{string.Join(",", Tree.OwnedIds)}|{Mastery.Taken.Count}:{string.Join(",", Mastery.Taken)}";
        var key = $"{weapon?.InstanceId ?? "-"}|{weapon?.ItemLevel}|{others}|{skills}|{keystones}|{trees}|{hunter.PowerRating}|{Character.Id}";
        if (_dpsCache.TryGetValue(key, out var hit)) return hit;

        var was = hunter.Worn(GearSlot.Weapon);
        if (weapon is null) hunter.Unequip(GearSlot.Weapon); else hunter.Equip(weapon);
        float dps;
        try { dps = DamageBench.Measure(Loadout.ToBuild(Tree, Mastery, Character, SkillLevels), hunter).Dps; }
        finally { if (was is null) hunter.Unequip(GearSlot.Weapon); else hunter.Equip(was); }
        if (_dpsCache.Count > 512) _dpsCache.Clear();
        _dpsCache[key] = dps;
        return dps;
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, Hunter hunter)
    {
        // A fixed step rather than a real delta: this screen has no GameTime and does not need one — the
        // doll only idles, and an idle that is a frame out of step with the arena's is not observable.
        _anim += 1f / 60f;
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        // The shared batch-A backdrop already draws the region scene behind us; a translucent scrim keeps it
        // subdued and the UI legible (§6), rather than a flat opaque fill.
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));

        // Title band (§5) — centred title + subtitle; currency is the shared top-right chrome (Game1).
        _ui.TextCenterBig(b, "CHAMPION GEAR", 960, 24, Gold, UiTypography.RegionTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);
        // THE SUBTITLE SAYS WHAT THIS SCREEN IS FOR, not what its panels are called.
        //
        // It used to recite the headers directly beneath it, which spends the largest 100px on the page
        // to tell the player something they can already read, and teaches them that big text in this
        // band is not worth reading. Same slot, same cost, real content.
        // WHO IS WEARING IT, AND WHAT THEY MAY WEAR, on the left; the screen's one instruction on the
        // right. "THE ANVIL · WARDEN — WEARS WARDEN GEAR AND ANY CHARM, RING OR FOCUS" is the sentence
        // that explains every dimmed cell in the grid below, so it sits where the eye starts.
        _ui.TextBig(b, $"{Character.Name}  ·  {ItemClasses.NameOf(Character.Class)} — {ItemClasses.WearsLine(Character.Class)}",
                    40, 80, UiKit.ClassColor(Character.Class), UiTypography.Secondary);
        _ui.TextRightBig(b, "ONLY WORN GEAR COUNTS IN A FIGHT — RIGHT-CLICK AN ITEM FOR OPTIONS",
                         1880, 80, Slate, UiTypography.Secondary);

        DrawLoadout(b, hit, hunter);
        _hovered = null;                // re-established by whichever draw finds the pointer over an item
        DrawEquipped(b, hit, hunter);
        DrawInventory(b, hit, hunter);
        DrawDetail(b, hit, hunter);
        DrawItemMenu(b, hit, hunter);   // LAST — it floats over everything it was opened from

        // THE HOVER CARD, after everything and before nothing. It answers "what is this" without a
        // click, which is the question twenty identical-looking icons in a grid otherwise force you to
        // ask one at a time. Suppressed while the right-click menu is open: two floating things fighting
        // over the same pointer is worse than either alone.
        if (_hovered is { } hov && _menuItemId is null)
            ItemTooltip.Draw(_ui, b, hov, hunter, hit, new Rectangle(0, 0, 1920, 1080), Character);
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

    /// <summary>
    /// The four verbs, on the item, where the player is holding it.
    /// </summary>
    /// <remarks>
    /// UPGRADE, REFORGE and SALVAGE all live in the Forge, and a player who had just picked up a drop had
    /// no way to reach any of them from here without already knowing which screen owned which verb —
    /// knowledge about the software rather than about the game. Choosing one carries the item across and
    /// opens the Forge already pointed at it.
    /// </remarks>
    private void DrawItemMenu(SpriteBatch b, Point hit, Hunter hunter)
    {
        if (_menuItemId is null) return;
        var item = Wearable().FirstOrDefault(i => i.InstanceId == _menuItemId)
                   ?? AllSlots.Select(hunter.Worn).OfType<ItemInstance>()
                              .FirstOrDefault(i => i.InstanceId == _menuItemId);
        if (item is null) { _menuItemId = null; return; }

        var worn = IsWorn(hunter, item);
        var rc = RarityColor(item.Rarity);
        var box = MenuRect;

        _ui.Fill(b, new Rectangle(box.X + 4, box.Y + 4, box.Width, box.Height), new Color(0, 0, 0, 0xA0));
        _ui.Fill(b, box, new Color(0x14, 0x11, 0x1E, 0xF6));
        _ui.Fill(b, new Rectangle(box.X, box.Y, box.Width, 3), rc);
        _ui.Fill(b, new Rectangle(box.X, box.Bottom - 3, box.Width, 3), rc);
        _ui.Fill(b, new Rectangle(box.X, box.Y, 3, box.Height), rc);
        _ui.Fill(b, new Rectangle(box.Right - 3, box.Y, 3, box.Height), rc);

        // The item's NAME heads the menu. Without it, "SALVAGE" is a verb with no object — the player is
        // about to destroy something and the only clue as to WHAT is which cell they happened to be over.
        // MEASURED AGAINST THE MENU, not trimmed to a character count. TrimName caps at 20 characters and
        // this drew them at the unsized 32px face, which measures ~367px against a 250px menu — the name
        // ran 116px past the box, across the inventory's scrollbar and the panel frame behind it. The
        // menu is a fixed width, so the type has to fit it.
        _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), box.Width - 28, UiTypography.Secondary),
                    box.X + 14, box.Y + 12, rc, UiTypography.Secondary);
        _ui.Fill(b, new Rectangle(box.X + 8, box.Y + MenuHeaderH - 4, box.Width - 16, 1), Dim);

        for (var i = 0; i < MenuEntries.Length; i++)
        {
            var (action, label) = MenuEntries[i];
            var row = new Rectangle(box.X + 6, box.Y + MenuHeaderH + 6 + i * MenuRowH, box.Width - 12, MenuRowH - 4);

            // WORN PIECES TAKE EVERY VERB NOW. The Forge upgrades and re-rolls worn gear in place
            // (ReplaceItem re-points the worn slot), and SALVAGE arrives there with the confirmation
            // already open — the dialog carries the worn warning, so this menu no longer has to refuse.
            // EQUIP on another class's piece reads CANNOT WEAR and stays dim: the verb is not offered,
            // and MenuHit refuses it, so the hover card's "who can wear it" line is the answer.
            var locked = action == ItemAction.Equip && !worn && !CanWearNow(item);
            var shown = action == ItemAction.Equip && worn ? "TAKE OFF" : locked ? "CANNOT WEAR" : label;

            var hover = row.Contains(hit) && !locked;
            if (hover) _ui.Fill(b, row, new Color(0x36, 0x2A, 0x4E));
            _ui.Text(b, shown, row.X + 16, row.Y + 12, locked ? Dim : hover ? Bone : Slate);
        }
    }

    private void DrawLoadout(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.PanelQuiet(b, LoadoutPanel);
        var x = UiKit.ContentLeft(LoadoutPanel);
        // Centred like EQUIPPED. Left-aligned at +24 the title sat on the frame's corner filigree.
        _ui.TextCenterBig(b, "LOADOUT", LoadoutPanel.Center.X, UiKit.TitleTop(LoadoutPanel), Gold, UiTypography.PanelTitle);

        var por = new Rectangle(LoadoutPanel.X + 98, LoadoutPanel.Y + 82, 105, 105);   // §7.6
        if (_ui.Assets.GetFirst(Character.PortraitKey, "hunter_portrait") is { } p) b.Draw(p, por, Color.White);

        var adept = Mastery?.Affinity() is { } mf ? $"{mf.ToString().ToUpperInvariant()} ADEPT" : "SEEKER";
        _ui.TextCenterBig(b, adept, LoadoutPanel.Center.X, LoadoutPanel.Y + 200, Bone, UiTypography.Headline);
        _ui.TextCenterBig(b, $"LEVEL {hunter.HunterLevel}", LoadoutPanel.Center.X, LoadoutPanel.Y + 234, Gold, UiTypography.Body);

        _ui.TextCenterBig(b, "GEAR POWER", LoadoutPanel.Center.X, LoadoutPanel.Y + 284, Slate, UiTypography.Secondary);
        _ui.TextCenterBig(b, $"{hunter.PowerRating:N0}", LoadoutPanel.Center.X, LoadoutPanel.Y + 312, Bone, UiTypography.PrimaryValue);

        // WHO YOU ARE PLAYING, and what that buys. The champion's name and portrait are on every screen
        // and the passive behind them was on exactly one — the Roster, which a player only opens in order
        // to SWITCH. So the thing that most changes how a build performs was invisible from the screen
        // where builds are assembled.
        //
        // Under the portrait rather than under the buttons, which is where it went first: below
        // UNEQUIP ALL there are 56 pixels before the frame, the name alone takes 22, and the clamp
        // correctly rendered zero lines of body text. The passive describes the champion, so it belongs
        // beside the champion — and there is room here.
        // A NAME AND ONE LINE, because that is the room there is: the gap between the power figure and
        // the summary rows is 42 pixels. Four lines of passive text would push the rows into the action
        // buttons, and this panel is already full. The name is the identity hook and the ROSTER carries
        // the sentence in full — this is the reminder, not the reference.
        _ui.TextCenterBig(b, Character.PassiveName, LoadoutPanel.Center.X, LoadoutPanel.Y + 346,
                          Gold, UiTypography.Secondary);
        // WRAPPED TO TWO LINES, not shortened to one. A single 240px line for a 69-character sentence cut
        // every champion's passive mid-clause — and these sentences end in their number ("Every skill
        // hits 8% harder"), so the truncation deleted the only mechanical fact in the line and left the
        // flavour. There is room below for the second line; there was never room for the first.
        var passive = _ui.WrapBig(Character.PassiveText, LoadoutPanel.Width - 60, UiTypography.Secondary);
        for (var pi = 0; pi < Math.Min(2, passive.Count); pi++)
            _ui.TextCenterBig(b, passive[pi], LoadoutPanel.Center.X, LoadoutPanel.Y + 366 + pi * 20,
                              Slate, UiTypography.Secondary);

        // Real summary rows (no fake armour set / vow-bound rows — those systems don't exist).
        var worn = AllSlots.Select(hunter.Worn).OfType<ItemInstance>().ToList();
        var legend = worn.Count(i => i.Rarity == Rarity.Legendary);

        // TWO ROWS, NOT FOUR, AND THEY START 28px LOWER. Both changes fix a collision that was on
        // screen: the passive's second wrapped line ends at Y+402 and the rows began at Y+384, so
        // "Every skill hits 8% harder" was drawn straight through "EQUIPPED  8 / 8" — and the fourth
        // row ran from Y+660 to Y+704, four pixels into EQUIP BEST at Y+700. A panel with no room for
        // its own contents.
        //
        // The room came from deleting, not from shuffling: EQUIPPED and GEAR LEVEL are both already
        // printed on the strip under the paper doll, which is where you are looking while you equip.
        // Two panels, same two numbers, eleven inches apart. What is left here is what is NOT said
        // anywhere else — how much of the loadout is Legendary, and which element it mostly shares.
        var ry = LoadoutPanel.Y + 412;
        SummaryRow(b, "LEGENDARY", $"{legend}", ref ry);
        // "SOURCE", not "SOURCE FOCUS". The row above will now shorten a value rather than collide with
        // it, but shortening THIS value loses the number, and the number is the whole row. The label is
        // the half that can afford to be shorter — it sits beside "NATURE 100%" and reads the same.
        // "FORGE ELEM", not "SOURCE" — see the long note at the matching line in ForgeScreen. This row
        // reports the element your WORN GEAR mostly shares, which decides nothing in a fight and
        // everything about whether you can merge a matched trio. Calling it SOURCE made it read as the
        // combat matchup and made ITEM POWER look broken for not accounting for it.
        // THE SET BONUSES THAT ARE ON, and nothing about the ones that are not. There was a SETS row
        // here — "NATURE 8 WORN" with the rungs behind a hover tip — and the player's verdict was that
        // the bonus was written in a bad place: a count is not a bonus, and a tip is not a place. So the
        // row is gone. What this panel says now is what the worn gear is actually doing for the fight,
        // one plain sentence per active rung, and it says nothing at all when no rung is on — the set
        // information proper (every rung, reached or not) lives under the item on the ITEM DETAIL
        // panel, which is where you decide what to wear.
        DrawActiveSets(b, hunter, ry + 8, EquipBestBtn.Y - 14);

        // Quick actions (§7.8) — both real. EQUIP BEST fills each slot with the highest-scoring item.
        Button(b, EquipBestBtn, "EQUIP BEST", hit, true);
        Button(b, UnequipAllBtn, "UNEQUIP ALL", hit, worn.Count > 0);

        // WHO YOU ARE PLAYING, and what that buys. The champion's name and portrait are on every screen
        // and the passive behind them was on exactly one — the Roster, which a player only opens to
        // SWITCH. So the thing that most changes how a build performs was invisible from the screen
        // where builds are assembled. It is one line and a sentence; it belongs beside the loadout it
        // modifies.

    }

    /// <summary>
    /// The ACTIVE SET BONUSES block of the loadout panel: a small header and one bright line per rung
    /// the worn gear has reached — "NATURE 3 — Regain 0.3% of maximum health every second." Draws
    /// nothing when no rung is on.
    /// </summary>
    /// <remarks>
    /// Between the LEGENDARY row and EQUIP BEST there are about 240 pixels. A full set is four rungs
    /// of one or two wrapped lines each, which fits; two half-sets (four and four) are six rungs and
    /// do not — so the lines are laid out against <paramref name="limit"/> by <see cref="DrawEntries"/>,
    /// which counts the rest ("AND 2 MORE") rather than running under the button.
    /// </remarks>
    private void DrawActiveSets(SpriteBatch b, Hunter hunter, int y, int limit)
    {
        var active = ElementSets.Active(hunter).ToList();
        if (active.Count == 0) return;

        var x = UiKit.ContentLeft(LoadoutPanel);
        var width = LoadoutPanel.Width - 76;
        _ui.TextBig(b, "SET BONUSES", x, y, Slate, UiTypography.Secondary);
        _ui.Fill(b, new Rectangle(x, y + 22, width, 1), Dim);
        y += 30;

        var entries = active.Select(a => ((string?)null,
                _ui.WrapBig($"{a.Element.ToString().ToUpperInvariant()} {a.Tier.Pieces} — {a.Tier.Line}", width, UiTypography.Secondary),
                UiKit.Vellum)).ToList();
        DrawEntries(b, entries, x, 0, y, limit, pitch: 20, gap: 6);
    }

    /// <summary>
    /// Wrapped entries drawn top-down and never past <paramref name="limit"/>: when they cannot all
    /// fit, the ones that do are drawn and one Slate line counts the rest — "AND 2 MORE" — so a line is
    /// never clipped and never drawn under a button. Each entry may lead with a short mark (a rung
    /// number) in its own column. Returns the y after the last line.
    /// </summary>
    private int DrawEntries(SpriteBatch b, IReadOnlyList<(string? Lead, IReadOnlyList<string> Lines, Color Colour)> entries,
                            int x, int leadWidth, int y, int limit, int pitch, int gap)
    {
        var total = entries.Sum(e => e.Lines.Count * pitch) + gap * (entries.Count - 1);
        var fitsAll = y + total <= limit;
        for (var i = 0; i < entries.Count; i++)
        {
            var (lead, lines, colour) = entries[i];
            var h = lines.Count * pitch;
            // Reserve the counting line before drawing an entry that would leave no room for it.
            if (!fitsAll && y + h + gap + pitch > limit)
            {
                _ui.TextBig(b, $"AND {entries.Count - i} MORE", x, y, Slate, UiTypography.Secondary);
                return y + pitch;
            }
            if (lead is not null) _ui.TextBig(b, lead, x, y, colour, UiTypography.Secondary);
            foreach (var line in lines) { _ui.TextBig(b, line, x + leadWidth, y, colour, UiTypography.Secondary); y += pitch; }
            y += gap;
        }
        return y - gap;
    }

    /// <summary>
    /// One label-and-value row in the loadout panel.
    /// </summary>
    /// <remarks>
    /// A COLUMN EACH, not a left label and a right-aligned value trusted to stay apart. This row is
    /// 224px wide between its margins and "SOURCE FOCUS" against "NATURE 100%" is wider than that, so
    /// the two ends met in the middle and drew as one word: "SOURCE FOCUSNATURE 100%". The Forge's
    /// item panel learned this exact lesson once already, where it read "SPLINTERON A KILL" — the
    /// comment there says a column each is the fix precisely because neither side can then grow into
    /// the other. Measuring the label and handing the value what is left does that generally, so the
    /// next long value shortens instead of colliding.
    /// </remarks>
    private void SummaryRow(SpriteBatch b, string label, string value, ref int y)
    {
        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(LoadoutPanel), y, LoadoutPanel.Width - UiKit.PadX(LoadoutPanel) * 2, 44), Quiet);
        _ui.TextBig(b, label, UiKit.ContentLeft(LoadoutPanel), y + 12, Slate, UiTypography.Secondary);

        var labelEnd = UiKit.ContentLeft(LoadoutPanel) + _ui.MeasureBig(label, UiTypography.Secondary);
        var valueRight = UiKit.ContentRight(LoadoutPanel);
        _ui.TextRightBig(b, _ui.ShortenBig(value, valueRight - labelEnd - 16, UiTypography.Body),
            valueRight, y + 10, Bone, UiTypography.Body);
        y += 52;
    }

    private void DrawEquipped(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.Panel(b, EquippedPanel);
        _ui.TextCenterBig(b, "EQUIPPED", EquippedPanel.Center.X, UiKit.TitleTop(EquippedPanel), Gold, UiTypography.PanelTitle);

        // The champion, dressed (base body + one overlay per worn slot), NativeScale (fit, not stretched).
        _ui.GroundShadow(b, HunterBox.Center.X, HunterBox.Bottom - 8, (int)(HunterBox.Width * 0.7f), 40, 0.55f);
        // The doll is the character you are playing, idling — the same strip the arena draws, so the two
        // screens can never show different people. It used to be the assembled cutout rig, whose whole
        // reason for being here was that gear rode its bones; the figure wears nothing now, so the rig
        // was carrying seventeen cut parts to display a person standing still.
        if (!_ui.AnimSprite(b, Character.StripKey("idle"), HunterBox, _anim, 10f, loop: true, Color.White, -1f))
            _ui.SpriteGrounded(b, Character.SpriteKey, HunterBox, Color.White, 0.02f);

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
                if (hot) _hovered = w2;      // the worn piece answers the same question the bag does
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
        var strip = new Rectangle(UiKit.ContentLeft(EquippedPanel), EquippedPanel.Bottom - 106, EquippedPanel.Width - UiKit.PadX(EquippedPanel) * 2, 52);
        _ui.Fill(b, strip, Quiet);
        _ui.Fill(b, new Rectangle(strip.X, strip.Y, 4, strip.Height), Purple);
        _ui.TextBig(b, $"{wornAll.Count} / 8 EQUIPPED", strip.X + 20, strip.Y + 8, Bone, UiTypography.Body);
        var il = wornAll.Count > 0 ? (int)Math.Round(wornAll.Average(i => i.ItemLevel)) : 0;
        _ui.TextRightBig(b, $"GEAR LEVEL {il}", strip.Right - 20, strip.Y + 10, Gold, UiTypography.Secondary);
    }

    private void DrawInventory(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.PanelQuiet(b, InventoryPanel);
        _ui.TextCenterBig(b, "INVENTORY", InventoryPanel.Center.X, UiKit.TitleTop(InventoryPanel), Gold, UiTypography.PanelTitle);

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

        var list = Filtered(hunter);
        for (var vis = 0; vis < InvCols * InvRows; vis++)
        {
            var idx = _invScroll * InvCols + vis;
            var cell = InvCellRect(vis);
            _ui.Fill(b, cell, CellBg);
            if (idx >= list.Count) continue;
            var item = list[idx];
            var hot = cell.Contains(hit);
            var sel = item.InstanceId == _selectedId;
            if (hot) _hovered = item;
            if (hot && !sel) _ui.Fill(b, cell, CellHot);
            _forge.DrawItemIcon(b, item, new Rectangle(cell.X + 6, cell.Y + 6, cell.Width - 12, cell.Height - 12));

            // EVERY MARK IN THIS CELL IS NOW DRAWN AFTER THE ICON, and each one owns a different part of
            // the cell. The item frame's centre is fully opaque, so anything drawn before it is painted
            // over — which is why the hover fill was invisible and why the rarity strip was the only
            // thing that survived (it sat in the 6px margin).
            //
            // Playtest: "envanter ekranında seçili ve kuşanılan item belirgin değil." Both were true and
            // both had the same cause: every state was competing for the same gold pixels at the top of
            // the cell. Selection was `Reticle(cell, Gold)` — and Legendary's rarity colour IS Gold, and
            // the list sorts rarity-descending with cell 0 selected by default, so THE FIRST THING THE
            // PLAYER SEES ON OPENING GEAR was a screen with no discernible selection. "Worn" was a
            // single 16px letter E on the frame's brightest corner ornament: the smallest, lowest
            // contrast mark in the cell, carrying the most important fact in the grid.
            //
            // So: rarity takes the LEFT EDGE, state takes the BOTTOM, selection takes the PERIMETER.
            // Different shapes in different places, none of them competing for a hue.
            _ui.Fill(b, new Rectangle(cell.X, cell.Y, 5, cell.Height), RarityColor(item.Rarity));

            // STATE IS A FRAME NOW, NOT A WORD.
            //
            // Playtest: "giyili olanların altına WORN yazmışsın ama o da güzel durmuyor. Bir çerçeve
            // ekleyerek giyili olanları gösterebiliriz, yazıya ihtiyaç duymayalım. Aynı şekilde daha iyi
            // olanlar için çerçeve içinde parlama efekti olabilir." Right on both counts: a 20px black
            // band across the foot of an 82px cell covers a quarter of the art to say one word, twenty
            // times over, in a grid the eye is meant to scan rather than read.
            //
            // THE THREE MARKS ARE SEPARATED BY TEXTURE, NOT BY HUE, so none of them needs colour to be
            // legible and none can be mistaken for a rarity (which owns the frame tint and the left bar):
            //
            //   BETTER    a soft halo — a glow, not a border.
            //   SELECTED  the outermost ring, brightest.
            //
            // (WORN left the grid entirely — playtest: worn gear lives on the doll now, so the grid
            // never needs a worn mark again.)
            // ANOTHER CLASS'S PIECE IS DIMMED AND LOCKED, not hidden. The icon stays so the player can
            // see what they found; the scrim says it is not for this champion; the lock says why, and
            // the hover card names who can wear it. It is never an upgrade candidate, so no halo.
            var wearable = CanWearNow(item);
            if (!wearable)
            {
                _ui.Fill(b, Shrink(cell, 5), new Color(0x0A, 0x08, 0x10, 0xB4));
                Lock(b, new Rectangle(cell.Right - 26, cell.Bottom - 28, 18, 20), UiKit.ClassColor(item.Class ?? Character.Class));
            }

            var better = wearable && Gear.SlotFor(item.BaseType) is { } bs && hunter.Worn(bs) is not null
                         && hunter.PowerContribution(item) > hunter.PowerContribution(hunter.Worn(bs));

            // WEIGHTED BY RARITY OF THE MARK, NOT BY IMPORTANCE OF THE FACT — which is the opposite of
            // the first attempt and the reason it failed. BETTER is the COMMON state: on a fresh bag most
            // of the grid beats what you are wearing, so a three-ring halo there flooded the page and the
            // two marks that are actually rare drowned in it. The most frequent mark has to be the
            // quietest one, or the grid has no figure and no ground.
            if (better)
            {
                // One hairline, inside the frame, at the edge of noticeable. It is a hint that this cell
                // is worth a second look — the ITEM DETAIL panel is where the case gets made.
                Ring(b, Shrink(cell, 3), new Color(0x6E, 0xC8, 0x7A, 0x9E), 1);
            }

            // BETTER no longer fires on an EMPTY slot. PowerContribution(null) returns 0, so the old test
            // was true for every item whose slot was bare — which on a fresh character, or one frame
            // after UNEQUIP ALL, meant every visible cell claimed to be an upgrade. A mark that is on
            // everything says nothing, and it teaches the player to stop reading that row.

            if (sel)
            {
                // 0x18, not 0x3C. Playtest: "seçili itemin üstüne mor bir image koymuşsun ama çok
                // belirgin, kötü görünüyor." It was a flat violet sheet over the art at 24% — it did not
                // read as "this one is chosen", it read as "this one is broken". The ring carries the
                // selection; the wash only has to warm the cell enough to separate it from its neighbours.
                // NO WASH AT ALL. Two were tried and both failed the same way: a violet sheet at 24% read
                // as damage on a violet item, and a cream sheet at 8% bleached a Legendary's own pale
                // frame until the cell looked blank. Any full-cell tint fights the art it sits on, and
                // the art is the thing the player is choosing between.
                //
                // The ring carries it alone — three pixels at the cell's edge, the brightest thing in the
                // grid, and outside every other mark so it can coexist with WORN. A selection needs to be
                // FOUND, not shouted; the ITEM DETAIL panel beside it confirms what is selected.
                Ring(b, cell, new Color(0xF6, 0xEA, 0xC6), 3);
                Ring(b, Shrink(cell, 3), new Color(0x14, 0x10, 0x1A, 0x88), 1);
            }
            else if (hot) Ring(b, cell, Slate, 2);
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
        // "/ 64 SLOTS" WAS FICTION. There is no bag cap anywhere in this game — _inv is an unbounded
        // List and no tuning names a limit — so the footer invented a ceiling, and a player watching it
        // approach 64 would have been hoarding against a wall that does not exist.
        _ui.TextBig(b, $"{total} ITEM{(total == 1 ? "" : "S")}", UiKit.ContentLeft(InventoryPanel), InventoryPanel.Bottom - 56, Slate, UiTypography.Secondary);

        // The menu is worth nothing if nobody finds it. Right-click is not a convention this game has
        // used anywhere else, so it has to be said out loud once.
        // Above the slots line, not below it: the panel's bottom edge is ornate frame art and anything
        // drawn on it is unreadable — the first attempt put this hint straight through the border.
        // SLATE, NOT DIM. This is the only place the game teaches that items have a context menu, and in
        // Dim it measured under 2:1 against the panel — an instruction nobody can read is not an
        // instruction. It matches the SORT hint beside it now, which was already legible.
        _ui.TextBig(b, "RIGHT-CLICK AN ITEM", UiKit.ContentLeft(InventoryPanel), InventoryPanel.Bottom - 84,
                    Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, "SORT: RARITY", UiKit.ContentRight(InventoryPanel), InventoryPanel.Bottom - 56, Slate, UiTypography.Secondary);
    }

    private void DrawDetail(SpriteBatch b, Point hit, Hunter hunter)
    {
        _ui.PanelQuiet(b, DetailPanel);
        _ui.TextCenterBig(b, "ITEM DETAIL", DetailPanel.Center.X, UiKit.TitleTop(DetailPanel), Gold, UiTypography.PanelTitle);

        var item = Selected(hunter);
        if (item is null)
        {
            _ui.TextCenterBig(b, "No item selected.", DetailPanel.Center.X, DetailPanel.Y + 380, Slate, UiTypography.Body);
            return;
        }

        // Hero block (§10.5): rarity frame + large icon + source + enchant glyphs.
        //
        // 200 tall, not 280. The panel's job changed: it has to say what the TRAIT and the ENCHANT DO,
        // not just name them, and two sentences need room that a 280px picture of a helmet was holding.
        // The old layout did not fit even without them — a Legendary with four affixes ran its stat list
        // to y=813 while the comparison strip is clamped to 796, so the two overlapped on exactly the
        // items a player most wants to read.
        var hero = new Rectangle(1510, 205, 318, 150);
        _ui.Fill(b, hero, new Color(0x12, 0x0F, 0x1C));
        _ui.Fill(b, new Rectangle(hero.X, hero.Y, hero.Width, 4), RarityColor(item.Rarity));
        _forge.DrawItemIcon(b, item, new Rectangle(hero.Center.X - 52, hero.Y + 10, 104, 104));
        if (item.Element is { } el && _ui.Assets.Get($"source_{el.ToString().ToLowerInvariant()}") is { } sg)
            b.Draw(sg, new Rectangle(hero.Right - 60, hero.Y + 16, 44, 44), Color.White);
        if (Enchantments.Of(item) is { } ench2) _ui.Diamond(b, new Rectangle(hero.Right - 58, hero.Y + 72, 40, 40), Purple);
        _ui.TextCenterBig(b, RarityShort(item.Rarity) + " " + SlotWord(item.BaseType), hero.Center.X, hero.Bottom - 40, RarityColor(item.Rarity), UiTypography.Body);

        // Identity (§10.6) — the rolled name: PREFIX (dominant affix) + ELEMENT + TYPE, e.g. "FURIOUS SHADOW BLADE".
        _ui.TextBig(b, ItemNaming.FullName(item), UiKit.ContentLeft(DetailPanel), hero.Bottom + 20, Bone, UiTypography.Headline);
        var ench = Enchantments.Of(item);
        // The enchant NAME moved down to sit with the sentence that explains it; repeating it here
        // spent a line on a word the reader still could not act on.
        _ui.TextBig(b, $"{(item.Element?.ToString().ToUpperInvariant() ?? "PLAIN")}  ·  LEVEL {item.ItemLevel}",
            UiKit.ContentLeft(DetailPanel), hero.Bottom + 54, Purple, UiTypography.Secondary);
        // The class, right-aligned on the same line, in its own colour — the third fact about an item
        // after its element and its level, and the one that decides whether EQUIP is even offered.
        _ui.TextRightBig(b, ItemClasses.ClassLine(item), UiKit.ContentRight(DetailPanel), hero.Bottom + 54,
            item.Class is { } dc && ItemClasses.IsClassLocked(item.BaseType) ? UiKit.ClassColor(dc) : Slate,
            UiTypography.Secondary);

        // Stat list (§10.7): ITEM POWER + the item's real affixes.
        var sy = hero.Bottom + 90;
        StatRow(b, "ITEM POWER", $"{hunter.PowerContribution(item):N0}", ref sy, Bone);
        foreach (var af in ItemAffixes.Of(item))
            StatRow(b, AffixLabel(af.Stat), AffixVal(af.Stat, af.Magnitude), ref sy, Green);

        // Comparison strip (§10.8) — the matching worn item + the net power delta. Concise, not a full sim.
        var slot = Gear.SlotFor(item.BaseType);
        var worn = slot is { } s ? hunter.Worn(s) : null;
        // ABOVE the trait and enchant sentences, not below them. It is the answer to "should I put this
        // on", which is the question the panel is open to settle, and it was previously last — under a
        // list whose length is DATA. With four affixes and two blurbs it landed at y=855, past the
        // buttons at 848, and rendered underneath them.
        var cmpY = sy + 8;
        var cmp = new Rectangle(UiKit.ContentLeft(DetailPanel), cmpY, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 44);
        _ui.Fill(b, cmp, Quiet);
        var whyNot = ItemClasses.WhyNot(Character, item);
        if (IsWorn(hunter, item))
            _ui.TextBig(b, "EQUIPPED", cmp.X + 16, cmp.Y + 12, Gold, UiTypography.Secondary);
        else if (whyNot is not null)
            // Another class's piece. The strip says so instead of a power delta the player cannot act
            // on; the sentence under it names the two champions who can — which is a reason to open
            // the roster, not a dead end.
            _ui.TextBig(b, $"{Character.Name} CANNOT WEAR THIS", cmp.X + 16, cmp.Y + 12, Ember, UiTypography.Secondary);
        else
        {
            var delta = hunter.PowerContribution(item) - hunter.PowerContribution(worn);
            _ui.TextBig(b, worn is null ? "SLOT EMPTY" : $"EQUIPPED: {(worn.Element?.ToString().ToUpperInvariant() ?? "PLAIN")} {SlotWord(worn.BaseType)}",
                cmp.X + 16, cmp.Y + 12, Slate, UiTypography.Secondary);
            if (item.BaseType == ItemBaseType.Weapon)
            {
                // A weapon answers in the fight's own unit: damage per second with THIS build, against
                // the weapon worn now — the bow/blade question the rating cannot see.
                var now = WeaponDps(hunter, worn);
                var with = WeaponDps(hunter, item);
                var pct = now > 0f ? with / now - 1f : 0f;
                _ui.TextRightBig(b, $"{pct:+0%;-0%;0%} DAMAGE PER SECOND", cmp.Right - 16, cmp.Y + 10,
                    pct > 0.005f ? Green : pct < -0.005f ? Ember : Slate, UiTypography.Body);
            }
            else
                _ui.TextRightBig(b, $"{(delta >= 0 ? "+" : "")}{delta:N0} POWER", cmp.Right - 16, cmp.Y + 10,
                    delta > 0 ? Green : delta < 0 ? Ember : Slate, UiTypography.Body);
        }

        // ── WHAT THEY DO, not what they are called. ───────────────────────────────────────────────
        // This panel printed "TRAIT — KEEN" and, above, "ENCHANT: HARVEST". Both are names. Nothing on
        // the screen said what KEEN does to a hit or what HARVEST does on a kill, though the sentences
        // have sat in GearTraits.BlurbOf and Enchantment.Blurb since they were written — which is what
        // the player meant by "I struggle to read what my items do". They were not reading badly; there
        // was nothing there to read.
        sy = cmp.Bottom + 14;
        if (whyNot is not null)
            sy = Wrapped(b, whyNot, UiKit.ContentLeft(DetailPanel), sy, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, Ember) + 10;
        if (GearTraits.TraitOf(item) is { } tr)
        {
            _ui.TextBig(b, GearTraits.NameOf(tr), UiKit.ContentLeft(DetailPanel), sy, Gold, UiTypography.Body);
            _ui.TextRightBig(b, "TRAIT", UiKit.ContentRight(DetailPanel), sy, Slate, UiTypography.Secondary);
            sy += 26;

            // THE NUMBERS, NOT THE ADJECTIVE. The blurb gives a direction — "far harder hits, skills come
            // slower" — and a direction alone is not a decision: "harder" is worth knowing only once you
            // know whether it means four percent or forty. It could never carry the figure either,
            // because the real one depends on this item's rarity AND its refine level, neither of which
            // a sentence written next to the enum knows.
            //
            // So the computed effect REPLACES the blurb rather than joining it. "+80% DMG  -20% SKILL" is
            // both shorter and stricter than "far harder hits — skills come slower", and the panel has
            // no room for a line that says less. The blurb stays the fallback for anything with no
            // measurable channel.
            var effect = GearTraits.EffectOf(item);
            sy = effect.Length > 0
                ? Wrapped(b, effect, UiKit.ContentLeft(DetailPanel), sy, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, Gold) + 10
                : Wrapped(b, GearTraits.BlurbOf(tr).ToUpperInvariant(), UiKit.ContentLeft(DetailPanel), sy,
                          DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, Bone) + 10;
        }
        if (ench is not null)
        {
            _ui.TextBig(b, ench.Name, UiKit.ContentLeft(DetailPanel), sy, Purple, UiTypography.Body);
            _ui.TextRightBig(b, "ENCHANT", UiKit.ContentRight(DetailPanel), sy, Slate, UiTypography.Secondary);
            sy += 26;
            sy = Wrapped(b, ench.Blurb.ToUpperInvariant(), UiKit.ContentLeft(DetailPanel), sy,
                         DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, Bone) + 10;
        }

        // ── THE SET, under the item. "NATURE SET — 3 OF 5 WORN", then the four rungs: the ones the
        //    worn gear has reached in Vellum, the rest in Faint. Playtest 2026-08-28: "set bonus
        //    information must be written under the item". This is the whole set, reached or not,
        //    because the question here is what one more piece would buy — the LOADOUT panel carries
        //    only what is already on.
        //
        //    NO SHIFT MODE, BY MEASUREMENT. The plan was a compact block that expands to the full lines
        //    while Shift is held. Measured at the Secondary size, the longest rung line in the catalogue
        //    is 273 pixels ("Your MACHINE skills hit 8% harder again — 16% in all.") and this column is
        //    338 wide beside the rung-number gutter, so every line already fits whole; a key that
        //    changes nothing would be a lie in a hint. WrapBig stays on the path so a longer line some
        //    day wraps rather than clips.
        //
        //    THE ROOM IS DATA. A Legendary with four affixes, a prefix and an enchant ends its text
        //    around y=805 and the buttons start at 910, so the rows tighten from a 22 to a 20 pitch
        //    when the block would not fit, and DrawEntries counts any rung that still would not. ──
        if (item.Element is { } setElement)
        {
            var tiers = ElementSets.TiersOf(setElement);
            var wornOf = ElementSets.WornCount(hunter, setElement);
            var limit = EquipBtn.Y - 8;
            var pitch = sy + 4 + 26 + tiers.Count * 22 <= limit ? 22 : 20;
            if (pitch == 22) sy += 4;
            _ui.TextBig(b, $"{ElementSets.Name(setElement)} — {ElementSets.Progress(wornOf)}", UiKit.ContentLeft(DetailPanel), sy, Bone, UiTypography.Body);
            _ui.TextRightBig(b, "SET", UiKit.ContentRight(DetailPanel), sy, Slate, UiTypography.Secondary);
            sy += 26;
            const int gutter = 28;
            var entries = tiers.Select(t => ((string?)t.Pieces.ToString(),
                    _ui.WrapBig(t.Line, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2 - gutter, UiTypography.Secondary),
                    wornOf >= t.Pieces ? UiKit.Vellum : Faint)).ToList();
            DrawEntries(b, entries, UiKit.ContentLeft(DetailPanel), gutter, sy, limit, pitch, gap: 0);
        }

        // Actions (§10.9): EQUIP (real). LOCK disabled — no lock system in the model.
        Button(b, EquipBtn, IsWorn(hunter, item) ? "EQUIPPED" : whyNot is null ? "EQUIP" : "CANNOT WEAR", hit,
               !IsWorn(hunter, item) && whyNot is null);
        Button(b, LockBtn, "LOCK", hit, false);
    }

    /// <summary>Wrap to a width and return the y AFTER the last line. Two sentences, never one clipped.</summary>
    private int Wrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        const int lineH = 26;
        foreach (var line in _ui.WrapBig(text, width, UiTypography.Label)) { _ui.Text(b, line, x, y, c); y += lineH; }
        return y;
    }

    private void StatRow(SpriteBatch b, string label, string value, ref int y, Color valColor)
    {
        // +44 inset and a 34px pitch. At +24 the labels sat on the frame's filigree; the pitch came down
        // from 38 when the panel took on two explanatory sentences it had never carried.
        _ui.TextBig(b, label, UiKit.ContentLeft(DetailPanel), y, Slate, UiTypography.Body);
        _ui.TextRightBig(b, value, UiKit.ContentRight(DetailPanel), y, valColor, UiTypography.Body);
        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(DetailPanel), y + 28, DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 2), Dim * 0.6f);
        y += 34;
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

    /// <summary>A continuous border inside a rect — the one mark in a cell that reads at a glance.</summary>
    /// <remarks>
    /// Replaces <see cref="Reticle"/> for selection. Eight corner ticks totalling 328px of a cell's
    /// perimeter lose to the item frame's own gold corner ornaments, which are in the same places; a
    /// closed ring has no corner to hide behind and reads as one shape rather than eight marks.
    /// </remarks>
    /// <summary>A rect inset equally on all four sides — for concentric marks inside one cell.</summary>
    private static Rectangle Shrink(Rectangle r, int by)
        => new(r.X + by, r.Y + by, Math.Max(1, r.Width - by * 2), Math.Max(1, r.Height - by * 2));

    private void Ring(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    /// <summary>
    /// A small padlock from rectangles — a shackle over a body — so the mark needs no glyph the
    /// font gate would have to allow. Drawn in the locking class's colour.
    /// </summary>
    private void Lock(SpriteBatch b, Rectangle r, Color c)
    {
        var bodyH = r.Height * 11 / 20;
        var body = new Rectangle(r.X, r.Bottom - bodyH, r.Width, bodyH);
        _ui.Fill(b, new Rectangle(body.X - 1, body.Y - 1, body.Width + 2, body.Height + 2), Shadow);
        _ui.Fill(b, body, c);
        // The shackle: a ring of two uprights and a bar, one pixel narrower each side than the body.
        var sw = Math.Max(2, r.Width / 5);
        var top = new Rectangle(r.X + 2, r.Y, r.Width - 4, sw);
        _ui.Fill(b, top, c);
        _ui.Fill(b, new Rectangle(top.X, r.Y, sw, body.Y - r.Y + 1), c);
        _ui.Fill(b, new Rectangle(top.Right - sw, r.Y, sw, body.Y - r.Y + 1), c);
        // The keyhole, so it reads as a lock at 18 pixels and not as a bucket.
        _ui.Fill(b, new Rectangle(body.Center.X - 1, body.Y + 3, 3, body.Height - 6), Shadow);
    }

    private void Reticle(SpriteBatch b, Rectangle r, Color c)
    {
        const int len = 20, t = 4;
        _ui.Fill(b, new Rectangle(r.X, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Bottom - len, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Bottom - len, t, len), c);
    }

    private static string SlotWord(ItemBaseType t) => t switch
    {
        ItemBaseType.Weapon => "WEAPON", ItemBaseType.Charm => "CHARM", ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.Helm => "HELM", ItemBaseType.Chest => "CHEST", ItemBaseType.Gloves => "GLOVES",
        ItemBaseType.Boots => "BOOTS", ItemBaseType.Ring => "RING", _ => "ITEM",
    };
    /// <summary>The stat's word. Core owns the list — see <see cref="ItemAffixes.StatWord"/> for why
    /// three screens must not each keep their own copy of it.</summary>
    private static string AffixLabel(AffixStat s) => ItemAffixes.StatWord(s);
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
