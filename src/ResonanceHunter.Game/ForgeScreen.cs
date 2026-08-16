using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Client;

/// <summary>
/// The Forge — click an item, then choose what to do with it. Sell, feed a merge, or dismantle.
/// </summary>
/// <remarks>
/// Redesigned to be point-and-click and panel-driven: an item is picked by clicking it, and every
/// action is a labelled button, not a remembered key. Keyboard still works as a fallback.
/// </remarks>
public sealed class ForgeScreen
{
    // ── THIS SCREEN HAS TWO SURFACES, and they want opposite ink. ─────────────────────────────────
    //
    // My first attempt at this pass flipped every colour on the screen to ink, and the header, the
    // footer hint and every item price went invisible — they are drawn on the DARK background, not on
    // a panel. A screen-wide colour is the wrong unit. The surface is the unit.
    //
    //   PARCHMENT (the loot panel, the item panel, the merge tray)  -> Ink*    (dark text)
    //   DARK      (the header band, the footer hint, the item cards) -> Scene* (light text)

    // On the DARK background and the dark item cards — unchanged, and still correct there.
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);

    // The reference art pack's panels are DARK glass, not parchment — so text on them is LIGHT, the same
    // as on the scene. The old dark "ink" ramp was invisible on the dark loot/item/merge panels.
    private static readonly Color Ink = Bone;                        // resting text — light on dark glass
    private static readonly Color InkFaint = new(0x9A, 0x92, 0xA8);  // labels, units — dim light
    private static readonly Color InkGold = Gold;                    // "earned" — gold reads on dark glass
    private static readonly Color InkEmber = Ember;                  // danger — bright ember on dark
    private static readonly Color Bloom = new(0xC8, 0x8A, 0xE0);     // Tyrian violet, light enough to read

    private static readonly string[] RarityNames = ["COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY"];

    /// <summary>Rarity on the DARK item cards and frames. Light.</summary>
    private static readonly Color[] RarityColors =
        [Bone, new Color(0x6E, 0xC8, 0x7A), new Color(0x4A, 0x90, 0xD9), new Color(0x8B, 0x3F, 0x82), Gold];

    /// <summary>Rarity for the item NAME. The panels are dark glass now, so it is just the bright ramp —
    /// the old dark-ink version was there for a parchment panel that no longer exists.</summary>
    private static readonly Color[] RarityInk = RarityColors;
    private static readonly Dictionary<ItemBaseType, string> ItemNames = new()
    {
        [ItemBaseType.CreatureCore] = "CORE", [ItemBaseType.Weapon] = "WEAPON", [ItemBaseType.Charm] = "CHARM",
        [ItemBaseType.Material] = "MATERIAL", [ItemBaseType.AbilityFocus] = "FOCUS",
        [ItemBaseType.Helm] = "HELM", [ItemBaseType.Chest] = "CHEST", [ItemBaseType.Gloves] = "GLOVES",
        [ItemBaseType.Boots] = "BOOTS", [ItemBaseType.Ring] = "RING",
    };
    // package_05 item thumbnails (128px). Weapons rotate through four families; chest uses the armour sets;
    // the rest use the generic accessory/armour thumbnail runs. A deterministic per-item seed gives variety.
    private static readonly string[] WeaponFams = ["weapon_blade", "weapon_bow", "weapon_spear", "weapon_scythe"];
    private static readonly string[] ChestSets = ["iron_sentinel_chest", "shadow_warden_chest", "verdant_guard_chest"];
    private static readonly Dictionary<ItemBaseType, (string Prefix, int Count)> ThumbFor = new()
    {
        [ItemBaseType.Charm] = ("accessory", 7), [ItemBaseType.AbilityFocus] = ("accessory", 7),
        [ItemBaseType.Ring] = ("accessory", 7), [ItemBaseType.CreatureCore] = ("accessory", 7),
        [ItemBaseType.Material] = ("accessory", 7),
        [ItemBaseType.Helm] = ("helmet", 6), [ItemBaseType.Gloves] = ("gloves", 6), [ItemBaseType.Boots] = ("boots", 6),
    };
    // package_05 rarity frames (also present in package_01 under the same names).
    private static readonly string[] FrameKey =
        ["ui_frame_rarity_common", "ui_frame_rarity_uncommon", "ui_frame_rarity_rare", "ui_frame_rarity_epic", "ui_frame_rarity_legendary"];

    private Texture2D? ItemThumb(ItemInstance item)
    {
        // Weapons use the shared, InstanceId-stable family/variant that also drives the item's NAME, so the
        // art and the "…BLADE/BOW…" name always agree. Other slots pick a stable thumbnail from the same seed.
        if (item.BaseType == ItemBaseType.Weapon) return _ui.Assets.Get(ItemNaming.WeaponArtKey(item));
        var seed = (int)(ItemNaming.ArtSeed(item) % 1000);
        var key = item.BaseType switch
        {
            ItemBaseType.Chest => ChestSets[seed % ChestSets.Length],
            _ => ThumbFor.TryGetValue(item.BaseType, out var m) ? $"{m.Prefix}_{seed % m.Count + 1:00}" : "",
        };
        return _ui.Assets.Get(key);
    }

    private readonly UiKit _ui;
    private readonly Random _rng = new();

    private readonly List<ItemInstance> _inv = new();
    private readonly List<Chest> _chests = new();    // unopened chests, waiting for the click
    private readonly List<string> _merge = new();   // up to 3 ids queued to merge
    /// <summary>The item under the pointer this frame. Re-established every draw; see CharacterScreen.</summary>
    private ItemInstance? _hovered;

    private int _cursor;                             // the active item (click or arrows)
    private string _msg = "";
    private Color _msgColor = Bone;

    // The chest-open reveal: a centred burst that holds, then fades — the payoff beat.
    private float _revealTimer;

    /// <summary>Hold the reveal at one instant so a capture can inspect a beat that lasts 0.2 seconds.</summary>
    /// <remarks>
    /// The capture rig renders sixty frames and saves the last, so an un-frozen animation is always shot
    /// at the same ~1 second in — which is beat three, and says nothing about whether beats one and two
    /// look right. Same lesson the Trait screen's flourish taught: a posed animation needs a brake.
    /// </remarks>
    private bool _revealFrozen;
    private Rarity _revealGrade;
    private int _revealMaterials;
    private readonly List<ItemInstance> _revealItems = new();
    // 2.9s, and it is a SEQUENCE now rather than a card that appears. See DrawReveal for the beats.
    private const float RevealHold = 2.9f;

    // The beats, in seconds from the moment the chest cracks. Named because the arithmetic below reads
    // as nonsense otherwise, and because a designer retiming this should not have to count decimals.
    private const float ShakeEnds = 0.55f;    // the chest rattles, harder and harder
    private const float BurstEnds = 0.78f;    // the ring goes out, the chest is gone
    private const float CardIn = 0.20f;       // how long the card takes to spring open
    private const float ItemStagger = 0.13f;  // one item lands, then the next
    private KeyboardState _prevKeys;
    private int _scroll;                             // index of the first visible card
    private int _bagScroll;                          // first visible row of the left-column bag list

    private const int Cols = 4;
    private const int VisRows = 3;                    // 4x3 = 12, matching uiref_forge; the freed strip below holds the footer
    private const int Visible = Cols * VisRows;      // loot cards shown at once

    /// <summary>Grid rectangle for the visible card at slot <paramref name="vis"/> (0..Visible-1).</summary>
    // Inside the loot panel (32,160,1000,592), whose ornate border reaches 40px in. The old geometry
    // (origin 64/224, pitch 232x172) put the left column on the frame and ran the bottom row 16px
    // through it, so the third row of loot was always sliced off.
    private static Rectangle Card(int vis) => new(72 + vis % Cols * 230, 216 + vis / Cols * 166, 222, 152);

    // ── Loot FILTER. A bag of ninety across eight slots plus four material tiers is a haystack; the filter
    //    lets the player narrow it to what they came for. The grid, cursor and scroll all walk the FILTERED
    //    view; mutations (merge/dismantle/salvage/sell) still act on the item objects, which live in _inv. ──
    private enum LootFilter { All, Weapons, Armour, Trinkets, Materials, RarePlus }
    private LootFilter _filter = LootFilter.All;

    private static string FilterName(LootFilter f) => f switch
    {
        LootFilter.All => "ALL", LootFilter.Weapons => "WEAPONS", LootFilter.Armour => "ARMOUR",
        LootFilter.Trinkets => "TRINKETS", LootFilter.Materials => "MATERIALS", _ => "RARE+",
    };

    private static bool Matches(LootFilter f, ItemInstance i) => f switch
    {
        LootFilter.All => true,
        LootFilter.Weapons => i.BaseType == ItemBaseType.Weapon,
        LootFilter.Armour => i.BaseType is ItemBaseType.Helm or ItemBaseType.Chest or ItemBaseType.Gloves or ItemBaseType.Boots,
        LootFilter.Trinkets => i.BaseType is ItemBaseType.Charm or ItemBaseType.AbilityFocus or ItemBaseType.Ring,
        LootFilter.Materials => i.BaseType is ItemBaseType.Material or ItemBaseType.CreatureCore,
        _ => i.Rarity >= Rarity.Rare,
    };

    /// <summary>The inventory the grid actually shows — <see cref="_inv"/> narrowed by the active filter.</summary>
    private List<ItemInstance> View() => _inv.Where(i => Matches(_filter, i)).ToList();

    private void CycleFilter(int dir)
    {
        var n = Enum.GetValues<LootFilter>().Length;
        _filter = (LootFilter)(((int)_filter + dir % n + n) % n);
        _cursor = 0;
        _scroll = 0;
    }

    public ForgeScreen(UiKit ui) => _ui = ui;

    // ── FORGE MODES (Forge production spec rev 1). The reference is a left-rail workshop: UPGRADE, then a
    //    set of other verbs. The real forge has exactly three that are BACKED BY THE MODEL, so the rail
    //    lists those and no invented ones (data-honesty §11): UPGRADE = Refine (+item level & affixes),
    //    REFORGE = re-roll the trait / enchant, SALVAGE = the bag + chests + merge + sell + dismantle hub.
    //    The reference's IMBUE and SET CRAFT have no backing system, so they are dropped, not faked. ──
    public enum ForgeMode { Upgrade, Reforge, Salvage }
    private ForgeMode _mode = ForgeMode.Upgrade;

    /// <summary>The item the focused (UPGRADE / REFORGE) modes act on — resolved by id so a re-forge that
    /// replaces the object keeps the selection. Defaults to the first wearable in the bag.</summary>
    private string? _focusId;

    /// <summary>F7 layout-debug overlay (UX standard §16 — component bounds).</summary>
    public bool DevForgeDebug { get; set; }

    // Spec §4 rectangles, adapted to the reference's rail + three content panels (all clear the nav at y≥934).
    // The left column is TWO panels now: the verbs on top, a real BAG under them.
    //
    // It used to be one rail whose only way to change the item you were working on was "< >  CYCLE ITEM"
    // — a keyboard-only, invisible, one-at-a-time walk through a bag that can hold ninety things. There
    // was no way to see what you owned, no way to jump to a piece, and no way to tell where you were.
    //
    // 366 wide, not 276: a row carries an icon, a name and a level, and at the old width the names ran
    // off the panel and the level column was clipped away entirely. The preview column gives back the 90.
    private static readonly Rectangle Rail = new(24, 140, 366, 330);
    private static readonly Rectangle BagPanel = new(24, 486, 366, 384);

    private const int BagRowH = 40;
    private static int BagRows => (BagPanel.Height - 124) / BagRowH;

    // +30 inset: UiKit.Panel's frame art eats the outer edge, and rows drawn flush to it sit ON the
    // ornament rather than inside the panel.
    private static Rectangle BagRow(int vis)
        => new(BagPanel.X + 30, BagPanel.Y + 84 + vis * BagRowH, BagPanel.Width - 60, BagRowH - 4);
    private static readonly Rectangle ItemPanel = new(406, 140, 384, 730);   // "ITEM PREVIEW"
    private static readonly Rectangle CostPanel = new(814, 140, 512, 730);   // "REQUIRED MATERIALS"
    private static readonly Rectangle ResultPanel = new(1350, 140, 546, 730);// "RESULT PREVIEW"
    private static readonly Rectangle ReforgePanel = new(814, 140, 1082, 730);// REFORGE action column

    // ── The SALVAGE hub's three panels. Named, because they were open-coded at their use sites and the
    //    three things that sat OUTSIDE them — the chest toolbar, the two mode buttons, the footer hint —
    //    each looked correct on its own line. A panel you cannot see the bounds of is a panel nothing
    //    gets checked against. Both columns now end level, at y=956. ──
    private static readonly Rectangle LootPanel = new(32, 72, 1000, 680);
    private static readonly Rectangle ActionPanel = new(1056, 72, 832, 624);
    private static readonly Rectangle TrayPanel = new(1056, 712, 832, 344);
    private static readonly Rectangle FooterPanel = new(32, 768, 1000, 288);

    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    // Scrap / Essence / Core / Crystal — no dedicated icons exist, so a tinted gem stands in (matches the
    // Scrap currency pill's own fallback). One colour per tier so the four read apart at a glance.
    private static readonly Color[] MatColor =
        { new(0x9A, 0xC0, 0x88), new(0x74, 0xC6, 0xE8), new(0xC0, 0x6E, 0xE0), new(0xF0, 0xC0, 0x48) };

    /// <summary>The wearable the focused modes upgrade — the id-matched item, or the first wearable.</summary>
    private ItemInstance? Target()
    {
        var wear = _inv.Where(Gear.IsWearable).ToList();
        if (wear.Count == 0) return null;
        return (_focusId is not null ? wear.FirstOrDefault(i => i.InstanceId == _focusId) : null) ?? wear[0];
    }

    /// <summary>Step the focused selection to the next / previous wearable in the bag.</summary>
    private void CycleTarget(int dir)
    {
        var wear = _inv.Where(Gear.IsWearable).ToList();
        if (wear.Count == 0) { _focusId = null; return; }
        var idx = Math.Max(0, wear.FindIndex(i => i.InstanceId == _focusId));
        _focusId = wear[(idx + dir % wear.Count + wear.Count) % wear.Count].InstanceId;
    }

    /// <summary>DEV ONLY: pose the UPGRADE view on a specific bag item, for the screenshot fixture.</summary>
    public void DevFocus(string instanceId) { _focusId = instanceId; _mode = ForgeMode.Upgrade; }

    /// <summary>
    /// Arrive here from somewhere else already pointed at an item, and say so.
    /// </summary>
    /// <remarks>
    /// The gear screen's item menu routes through this. It sets the mode AND the focus AND flashes the
    /// item's name, because a screen that changes under you without saying why is indistinguishable from
    /// a misclick — the player needs to see that the thing they right-clicked is the thing now in the
    /// preview.
    /// </remarks>
    public void FocusFor(string instanceId, ForgeMode mode)
    {
        _focusId = instanceId;
        _mode = mode;
        _bagScroll = 0;

        var it = _inv.FirstOrDefault(i => i.InstanceId == instanceId);
        if (it is not null) Say($"{ItemNaming.FullName(it)} — READY.", Met);
    }

    /// <summary>DEV ONLY: pose the SALVAGE hub (chests / merge / grid) for the loot-forge fixture.</summary>
    public void DevManage() => _mode = ForgeMode.Salvage;

    /// <summary>DEV ONLY: pose the REFORGE view, for auditing that mode.</summary>
    public void DevReforge() => _mode = ForgeMode.Reforge;

    public IReadOnlyList<ItemInstance> Inventory => _inv;

    /// <summary>Cumulative chests cracked open — the host polls the delta to credit CRAFTER evolution.</summary>
    public int ChestsOpened => _chestsOpened;
    private int _chestsOpened;

    /// <summary>Put a saved career's chest count back. Called on load, before anything reads it.</summary>
    public void RestoreChestsOpened(int count) => _chestsOpened = Math.Max(0, count);

    /// <summary>The chests waiting to be opened. Persisted, so a boss's drop survives a reload.</summary>
    public IReadOnlyList<Chest> UnopenedChests => _chests;

    /// <summary>A boss dropped a chest — it waits here until the player cracks it open.</summary>
    public void AddChest(Chest chest) => _chests.Add(chest);

    /// <summary>Restore unopened chests from a save.</summary>
    public void RestoreChests(IEnumerable<Chest> chests)
    {
        _chests.Clear();
        _chests.AddRange(chests);
    }

    /// <summary>
    /// Forge tuning, with whatever Memory Dust has bought folded in (EFFICIENT FORGE).
    /// </summary>
    /// <remarks>
    /// Set by the host each frame. It flows in as a TUNING VALUE rather than a special case at each
    /// call site: DismantleReturnRate was a hardcoded 0.4f that the unlock claimed to raise and never
    /// did, and a special case bolted onto one of the four call sites would have left the other three
    /// quietly disagreeing about what a dismantle is worth.
    /// </remarks>
    public ForgeTuning Tuning { get; set; } = ForgeTuning.Default;

    /// <summary>
    /// The Forms the player's current build runs. Set by the host so the Forge can tell you whether an
    /// item's Form-combo enchantment is LIVE for your build or dead weight — the whole loot philosophy in
    /// one line: not "is this a bigger number", but "does this fit what I'm building".
    /// </summary>
    public IReadOnlyCollection<Form> ActiveForms { get; set; } = Array.Empty<Form>();

    /// <summary>
    /// The triggers the worn build carries, so a KEYSTONE combo can say whether it is live.
    /// </summary>
    /// <remarks>
    /// Set by the host beside <see cref="ActiveForms"/>. An empty set is not "no keystones" so much as
    /// "nobody told this screen", and the failure is quiet either way — every keystone combo simply
    /// reads as dead. That is the safe direction: an item wrongly greyed is a player who looks again,
    /// an item wrongly gilded is a player who equips it and wonders why nothing changed.
    /// </remarks>
    public IReadOnlyCollection<BuildTrigger> ActiveTriggers { get; set; } = Array.Empty<BuildTrigger>();

    /// <summary>How many DISTINCT Vows the worn build has sworn — what TITHE is paid on.</summary>
    public int SwornVows { get; set; }

    /// <summary>
    /// Does this item's enchantment combo with the build the player is actually running?
    /// </summary>
    /// <remarks>
    /// The rule itself is <see cref="EnchantNeed.MetBy"/>, in Core, where it can be unit-tested — this
    /// only supplies the three facts this screen happens to hold. Both the grid badge and the detail
    /// panel's combo line come through here, because two copies of a three-clause predicate is how a
    /// card ends up saying COMBOS YOUR BUILD while the grid it sits in says nothing, and the player
    /// believes whichever one they read first.
    /// </remarks>
    private bool CombosWithBuild(ItemInstance? item)
        => Enchantments.Of(item)?.Needs?.MetBy(ActiveForms, ActiveTriggers, SwornVows) == true;

    /// <summary>
    /// The Memory Dust auto-sell floor. Set by the host so a chest's rolled loot honours the same filter a
    /// boss drop does — filtered items are sold for gleam, not dumped in the bag. Null keeps everything.
    /// </summary>
    public Rarity? AutoSellFloor { get; set; }

    /// <summary>
    /// The build's loot-quality tilt, set by the host each frame. 1 is neutral.
    /// </summary>
    /// <remarks>
    /// Host-set like AutoSellFloor rather than resolved here, because the screen has no Build and the
    /// tilt is a build property (keystones + gear + the trait tree's FORTUNE road), not a Forge one.
    /// </remarks>
    public float RarityBonus { get; set; } = 1f;

    /// <summary>TIRELESS FORGE (Memory Dust): auto-merge the bag after OPEN ALL, so a bulk crack tidies itself.</summary>
    public bool AutoMergeOnOpen { get; set; }

    /// <summary>DEV ONLY: put the cursor on a given item, so screenshots can pose a specific one.</summary>
    public void DevSelect(int index) => _cursor = Math.Clamp(index, 0, Math.Max(0, _inv.Count - 1));

    /// <summary>DEV ONLY: open the best chest, so a screenshot can pose the reveal burst.</summary>
    public void DevOpenOneChest(Hunter hunter) => OpenBestChest(hunter);

    /// <summary>
    /// DEV ONLY: mint and queue a cross-family trio, to pose the hybrid recipe row.
    /// </summary>
    /// <remarks>
    /// It MINTS rather than picking from the dev seed: that seed walks type and rarity on different
    /// cycles (i%4 and i%5), so across nine items no rarity ever holds three distinct types — the trio
    /// this needs cannot be found there.
    /// </remarks>
    public void DevQueueHybrid()
    {
        _merge.Clear();
        var types = new[] { ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus };
        for (var i = 0; i < types.Length; i++)
        {
            var item = new ItemInstance
            {
                InstanceId = $"hyb{i}", BaseType = types[i], Rarity = Rarity.Rare,
                SellValue = 34, Element = Source.Shadow,
            };
            _inv.Add(item);
            _merge.Add(item.InstanceId);
        }
        _cursor = _inv.Count - 1;
    }
    public void AddLoot(IEnumerable<ItemInstance> items) => _inv.AddRange(items);

    private ItemInstance? Active { get { var v = View(); return v.Count == 0 ? null : v[Math.Clamp(_cursor, 0, v.Count - 1)]; } }

    public void Update(GameTime time, KeyboardState keys, Point mouse, bool clicked, int wheel, Hunter hunter)
    {
        // The chest-open REVEAL fades on its own clock — the anticipation beat that loot design lives on.
        if (_revealTimer > 0f && !_revealFrozen) _revealTimer -= (float)time.ElapsedGameTime.TotalSeconds;

        // In the focused modes the grid is not shown — arrows cycle which wearable you are upgrading, and
        // the mode-switch and action clicks are handled in Draw. The bag/grid interactions below belong to
        // SALVAGE mode only, so a key never sells the item you are previewing on another screen.
        if (_mode != ForgeMode.Salvage)
        {
            if (Pressed(keys, Keys.Left)) CycleTarget(-1);
            if (Pressed(keys, Keys.Right)) CycleTarget(1);
            _prevKeys = keys;
            return;
        }

        var view = View();

        // The wheel serves whichever list the pointer is over — the bag on the left, the loot grid on
        // the right. Routing it only to the grid would leave the new list keyboard-bound, which is the
        // problem it exists to replace.
        if (wheel != 0 && BagPanel.Contains(mouse))
        {
            var wearCount = _inv.Count(Gear.IsWearable);
            _bagScroll = Math.Clamp(_bagScroll - wheel, 0, Math.Max(0, wearCount - BagRows));
            wheel = 0;
        }

        // Mouse wheel scrolls the loot grid a row at a time.
        if (wheel != 0)
        {
            var maxScroll = Math.Max(0, (view.Count - 1) / Cols * Cols - (VisRows - 1) * Cols);
            _scroll = Math.Clamp(_scroll - wheel * Cols, 0, maxScroll);
        }

        // Keyboard nav still works — arrows walk the grid.
        if (Pressed(keys, Keys.Left)) MoveCursor(-1);
        if (Pressed(keys, Keys.Right)) MoveCursor(1);
        if (Pressed(keys, Keys.Up)) MoveCursor(-Cols);
        if (Pressed(keys, Keys.Down)) MoveCursor(Cols);
        // TAB cycles the filter forward, SHIFT+TAB back — so reaching MATERIALS from ALL isn't four presses.
        if (Pressed(keys, Keys.Tab))
            CycleFilter(keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift) ? -1 : 1);
        if (Pressed(keys, Keys.Space)) ToggleMerge(hunter, Active);
        if (Pressed(keys, Keys.S)) Sell(hunter, Active);
        if (Pressed(keys, Keys.D)) Dismantle(hunter, Active);
        if (Pressed(keys, Keys.M)) DoMerge(hunter);
        if (Pressed(keys, Keys.J)) SalvageJunk(hunter);   // one press clears the Common/Uncommon clutter to Scrap

        // ── Mouse: click a loot card to make it active. ──
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        for (var vis = 0; vis < Visible && _scroll + vis < view.Count; vis++)
            if (UiKit.ClickedIn(Card(vis), hit, clicked)) { _cursor = _scroll + vis; _msg = ""; }

        // Action-button clicks are handled in Draw (where the SpriteBatch and button rects exist);
        // a click on a button rect and a click on a card never overlap, so this is unambiguous.
        _prevKeys = keys;
    }

    private void MoveCursor(int d)
    {
        var count = View().Count;
        if (count == 0) return;
        _cursor = Math.Clamp(_cursor + d, 0, count - 1);
        // Keep the cursor's row on screen (scroll moves a whole row at a time).
        if (_cursor < _scroll) _scroll = _cursor / Cols * Cols;
        if (_cursor >= _scroll + Visible) _scroll = (_cursor / Cols - VisRows + 1) * Cols;
    }

    private void ToggleMerge(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        if (_merge.Remove(item.InstanceId)) return;   // un-queueing is always allowed
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE MERGING IT.", Ember); return; }
        if (_merge.Count >= 3) { Say("MERGE HOLDS 3 ITEMS. REMOVE ONE FIRST.", Ember); return; }
        _merge.Add(item.InstanceId);
    }

    private bool MergeReady()
    {
        if (_merge.Count != 3) return false;
        var items = _inv.Where(i => _merge.Contains(i.InstanceId)).ToList();
        return items.Count == 3 && items.All(i => i.Rarity == items[0].Rarity) && items[0].Rarity != Rarity.Legendary;
    }

    /// <summary>
    /// Merge everything mergeable, repeatedly, until nothing is left to fuse.
    /// </summary>
    /// <remarks>
    /// Hand-picking three Commons out of a bag of ninety is busywork, not a decision — and busywork is
    /// what an idle game is supposed to delete. Worn gear is never consumed.
    /// </remarks>
    public void AutoMergeAll(Hunter hunter)
    {
        var rounds = 0;
        var merged = 0;

        bool IsWorn(ItemInstance i) =>
            Gear.SlotFor(i.BaseType) is { } s && hunter.Worn(s)?.InstanceId == i.InstanceId;

        for (var again = true; again && rounds < 200; rounds++)
        {
            again = false;
            foreach (var rarity in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic })
            {
                // Distinct-by-id WEARABLES only: a trio must be three DIFFERENT items (Merge rejects an
                // item merged with itself) and gear, not currency — so a duplicate-id copy or a stack of
                // Scrap can never grab the slots and stall the whole auto-merge.
                var trio = _inv.Where(i => i.Rarity == rarity && !IsWorn(i) && Gear.IsWearable(i))
                               .DistinctBy(i => i.InstanceId).Take(3).ToList();
                if (trio.Count < 3) continue;

                var result = Forge.Merge(trio, _rng, Tuning, LootTuning.Default);
                if (!result.Success) continue;

                foreach (var i in trio) { _inv.Remove(i); _merge.Remove(i.InstanceId); }
                _inv.Add(result.Product!);
                merged++;
                again = true;
            }
        }

        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say(merged > 0 ? $"AUTO-MERGED {merged}x — BAG IS NOW {_inv.Count} ITEMS." : "NOTHING TO MERGE (NEEDS 3 OF A RARITY).",
            merged > 0 ? Gold : Slate);
    }

    /// <summary>
    /// SALVAGE JUNK: dismantle every non-worn Common and Uncommon piece into materials at once.
    /// </summary>
    /// <remarks>
    /// The idle twin of a single DISMANTLE. A bag of ninety low drops is busywork one at a time, and in an
    /// AFK loop the player wants the Scrap for REFINE, not the clutter — so this clears the two junk tiers in
    /// one press and leaves Rare-and-up untouched, since those are the keepers you actually choose between.
    /// </remarks>
    public void SalvageJunk(Hunter hunter)
    {
        bool IsWornItem(ItemInstance i) =>
            Gear.SlotFor(i.BaseType) is { } s && hunter.Worn(s)?.InstanceId == i.InstanceId;

        var junk = _inv.Where(i => Gear.IsWearable(i) && i.Rarity <= Rarity.Uncommon && !IsWornItem(i)).ToList();
        if (junk.Count == 0) { Say("NO JUNK TO SALVAGE (COMMON / UNCOMMON GEAR).", Slate); return; }

        // A SALVAGE CHART DOUBLES THE YIELD. Salvage is the one Forge operation that costs nothing, so
        // its charter cannot waive a price — it raises the return instead. Spent here, after the junk
        // check above has already proven there is something to salvage.
        var chart = hunter.SpendCharter(Charter.Salvage);

        var gained = 0;
        foreach (var it in junk)
        {
            var m = Forge.Dismantle(it, Tuning) * (chart ? 2 : 1);
            hunter.AddMaterial(MaterialTiers.ForRarity(it.Rarity), m);
            gained += m;
            _inv.Remove(it);
            _merge.Remove(it.InstanceId);
        }

        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say($"SALVAGED {junk.Count} JUNK — +{gained} {MaterialTiers.Name(Material.Scrap)}"
            + (chart ? "  (SALVAGE CHART — DOUBLED)." : "."), Gold);
    }

    private void DoMerge(Hunter hunter)
    {
        var inputs = _inv.Where(i => _merge.Contains(i.InstanceId)).ToList();
        // Defence in depth: ToggleMerge already refuses to queue worn gear, but an item can be queued and
        // THEN equipped. Consuming it would delete equipped gear (and orphan its worn-slot pointer).
        if (inputs.Any(i => IsWorn(hunter, i))) { Say("TAKE WORN GEAR OFF BEFORE MERGING IT.", Ember); return; }
        // A MERGE CHART lifts the same-rarity rule. Tried WITHOUT it first, so a perfectly ordinary
        // matched trio never silently burns a paper the player was saving for a mixed one.
        var result = Forge.Merge(inputs, _rng, Tuning, LootTuning.Default);
        var chart = false;
        if (!result.Success && hunter.CharterCount(Charter.Merge) > 0)
        {
            var withChart = Forge.Merge(inputs, _rng, Tuning, LootTuning.Default, ignoreRarity: true);
            if (withChart.Success) { result = withChart; chart = true; }
        }
        if (!result.Success) { Say(result.Rejection!, Ember); return; }
        if (chart) hunter.SpendCharter(Charter.Merge);

        foreach (var i in inputs) _inv.Remove(i);
        _merge.Clear();
        _inv.Add(result.Product!);
        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say($"MERGED INTO A {RarityNames[(int)result.Product!.Rarity]} {ItemNames[result.Product.BaseType]}"
            + (chart ? "  (MERGE CHART — MIXED RARITIES)." : "."), Gold);
    }

    /// <summary>
    /// One short line saying what wearing this actually buys you — <b>and what it costs</b>.
    /// </summary>
    /// <remarks>
    /// This used to print "+34 DEF  +85 HP" for a charm and "+18 RES" for a focus. Both were lies: the
    /// pivot to auto-battle left those stats reaching no fight, so the screen was quoting numbers with
    /// nowhere to land while the player wondered why charms did nothing. It now reads the same
    /// <see cref="GearMods"/> the squad reads, so the label cannot drift from the effect again — and it
    /// leads with the cost, because a trade you can't see before you equip it isn't a decision.
    /// </remarks>
    /// <summary>
    /// What the TRAIT alone does — no weapon multiplier folded in.
    /// </summary>
    /// <remarks>
    /// <see cref="GearBlurb"/> deliberately folds a weapon's own damage multiplier into its damage figure,
    /// because on an inventory card the question is "what does this item give me". On the REFORGE row the
    /// question is different: re-rolling changes the TRAIT and nothing else. Sharing the helper printed
    /// "DMG+1637%" beside the word HEAVY on a Legendary bow — true about the item, and a lie about the
    /// thing the button next to it would change.
    /// </remarks>
    private static string TraitOnlyBlurb(ItemInstance item)
    {
        if (GearTraits.TraitOf(item) is null) return "";

        var m = GearTraits.ModsOf(item);
        var parts = new List<string>();
        void Add(string label, float v) { if (MathF.Abs(v - 1f) > 0.005f) parts.Add($"{label}{Pct(v)}"); }
        Add("DMG", m.Damage);
        Add("HP", m.Health);
        Add("SKL", m.SkillRate);
        Add("HAUL", m.Haul);
        return string.Join("  ", parts);
    }

    private static string GearBlurb(ItemInstance item)
    {
        if (GearTraits.TraitOf(item) is not { } trait) return "";

        var m = GearTraits.ModsOf(item);
        if (Gear.SlotFor(item.BaseType) == GearSlot.Weapon)
            m = m with { Damage = m.Damage * Gear.WeaponDamageMultiplier(item) };

        var parts = new List<string>();
        void Add(string label, float v) { if (MathF.Abs(v - 1f) > 0.005f) parts.Add($"{label}{Pct(v)}"); }
        Add("DMG", m.Damage);
        Add("HP", m.Health);
        Add("SKL", m.SkillRate);
        Add("HAUL", m.Haul);

        // Effects only — the trait's NAME is drawn on the title row, where there is room for it.
        _ = trait;
        return string.Join("  ", parts);
    }

    /// <summary>A multiplier as a signed percentage: 1.35 → "+35%", 0.8 → "-20%".</summary>
    private static string Pct(float mult)
    {
        var pct = (int)MathF.Round((mult - 1f) * 100f);
        return pct >= 0 ? $"+{pct}%" : $"{pct}%";
    }

    /// <summary>Wear it, or take it off. Anything displaced falls back into the bag.</summary>
    private void ToggleEquip(Hunter hunter, ItemInstance item)
    {
        if (Gear.SlotFor(item.BaseType) is not { } slot) return;

        if (hunter.Worn(slot)?.InstanceId == item.InstanceId)
        {
            hunter.Unequip(slot);
            Say($"TOOK OFF THE {ItemNames[item.BaseType]}.", Slate);
            return;
        }

        hunter.Equip(item);   // whatever it displaced is still in _inv — nothing is destroyed
        Say($"EQUIPPED. POWER IS NOW {hunter.PowerRating}.", Gold);
    }

    private void Sell(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        // The mouse SELL button greys out for worn gear, but the S key reaches here directly — so the
        // guard has to live in the handler, or you could sell what you are wearing (free Gleam, and the
        // worn slot keeps pointing at the sold item). Same reason Reforge/Refine self-guard below.
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE SELLING IT.", Ember); return; }
        var reason = Forge.CheckEligible(item);
        if (reason != IneligibleReason.Eligible) { Say(Forge.Explain(reason), Ember); return; }
        _inv.Remove(item); _merge.Remove(item.InstanceId);
        hunter.AddGleam(item.SellValue);
        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say($"SOLD FOR {item.SellValue} GLEAM.", Gold);
    }


    private void Dismantle(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE DISMANTLING IT.", Ember); return; }
        var reason = Forge.CheckEligible(item);
        if (reason != IneligibleReason.Eligible) { Say(Forge.Explain(reason), Ember); return; }
        var m = Forge.Dismantle(item, Tuning);
        _inv.Remove(item); _merge.Remove(item.InstanceId);
        var tier = MaterialTiers.ForRarity(item.Rarity);   // salvage sorts by rarity into the right tier
        hunter.AddMaterial(tier, m);
        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say($"DISMANTLED INTO {m} {MaterialTiers.Name(tier)}.", Slate);
    }

    /// <summary>
    /// Re-roll the selected item's TRAIT for materials. The item keeps its id — only what it IS changes.
    /// </summary>
    /// <remarks>
    /// This is the materials sink that makes items customisable. It is a real change every time (the roll
    /// excludes the current trait), so you can churn a weapon toward the trade your build wants. Worn gear
    /// is off-limits — reforging what you are wearing would mutate a live build mid-fight — so take it off
    /// first, exactly as SELL/DISMANTLE/MERGE already require.
    /// </remarks>
    private void DoReforgeTrait(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE REFORGING.", Ember); return; }

        var cost = ReforgeTuning.Default.TraitCostFor(item.Rarity);
        var chart = hunter.CharterCount(Charter.Reforge) > 0;
        if (!chart && hunter.MaterialOf(Material.Essence) < cost)
        {
            Say($"NEED {cost} ESSENCE TO REFORGE THE TRAIT.", Ember);
            return;
        }

        var result = Reforge.ReforgeTrait(item, _rng, ReforgeTuning.Default);
        if (!result.Success) { Say(result.Rejection!, Ember); return; }

        // Spent only now that the re-roll has actually succeeded.
        if (chart) hunter.SpendCharter(Charter.Reforge);
        else hunter.SpendMaterial(Material.Essence, result.Cost);
        ReplaceItem(item, result.Product!);
        var name = GearTraits.NameOf(GearTraits.TraitOf(result.Product!)!.Value);
        Say($"REFORGED — TRAIT IS NOW {name}.", Gold);
    }

    /// <summary>
    /// Re-roll the selected item's ENCHANTMENT — the Form-combo, the build-defining roll. Rare+ only.
    /// </summary>
    private void DoReforgeEnchant(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE REFORGING.", Ember); return; }
        if (item.Rarity < Enchantments.MinimumRarity) { Say("ONLY RARE+ ITEMS CARRY AN ENCHANTMENT.", Ember); return; }

        // A Legendary's enchant reforge is the premium roll, so it spends the premium material (CRYSTAL);
        // Rare/Epic spend CORE. That is what gives Crystal — the Legendary-salvage tier — its own sink.
        var tier = EnchantReforgeTier(item.Rarity);
        var cost = ReforgeTuning.Default.EnchantCostFor(item.Rarity);
        var chart = hunter.CharterCount(Charter.Reforge) > 0;
        if (!chart && hunter.MaterialOf(tier) < cost)
        {
            Say($"NEED {cost} {MaterialTiers.Name(tier)} TO REFORGE THE ENCHANTMENT.", Ember);
            return;
        }

        var result = Reforge.ReforgeEnchant(item, _rng, ReforgeTuning.Default);
        if (!result.Success) { Say(result.Rejection!, Ember); return; }

        if (chart) hunter.SpendCharter(Charter.Reforge);
        else hunter.SpendMaterial(tier, result.Cost);
        ReplaceItem(item, result.Product!);
        var ench = Enchantments.Of(result.Product!);
        Say(ench is not null ? $"REFORGED — ENCHANT IS NOW {ench.Name}." : "REFORGED THE ENCHANTMENT.", Gold);
    }

    private static Material EnchantReforgeTier(Rarity r) => r == Rarity.Legendary ? Material.Crystal : Material.Core;

    /// <summary>
    /// REFINE: spend SCRAP + Gold to raise the item's level by 1, which raises its affixes. The bulk,
    /// infinite sink — the cost climbs with the item's level, so it never stops draining.
    /// </summary>
    private void DoRefine(Hunter hunter, ItemInstance? item)
    {
        if (item is null || !Gear.IsWearable(item)) { Say("ONLY WEARABLES CAN BE REFINED.", Ember); return; }
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE REFINING.", Ember); return; }

        var r = Forge.Refine(item, Tuning);

        // A CHART PAYS FOR IT. Validated first and spent last, per Hunter.SpendCharter's contract: a
        // charter consumed before a rejection is a paper the player spent on a refusal, and these drop
        // one wave in forty.
        var chart = hunter.CharterCount(Charter.Refine) > 0;
        if (!chart)
        {
            if (hunter.MaterialOf(Material.Scrap) < r.Scrap) { Say($"NEED {r.Scrap} SCRAP TO REFINE.", Ember); return; }
            if (hunter.Gleam < r.Gold) { Say($"NEED {r.Gold}g TO REFINE.", Ember); return; }
        }

        if (chart) hunter.SpendCharter(Charter.Refine);
        else { hunter.SpendMaterial(Material.Scrap, r.Scrap); hunter.SpendGleam(r.Gold); }

        ReplaceItem(item, r.Product);
        Say(chart
                ? $"REFINED TO iL{r.Product.ItemLevel}  (REFINE CHART — FREE)."
                : $"REFINED TO iL{r.Product.ItemLevel}  ({r.Scrap} SCRAP + {r.Gold}g).", Gold);
    }

    /// <summary>
    /// GREATER REFINE: spend one CRYSTAL (+ Gold) to raise the item's level by FIVE at once — the premium,
    /// always-available sink for the rarest salvage tier. Reached by Shift-clicking REFINE, so Crystal has
    /// a broad drain of its own (not just the narrow "reroll a Legendary's enchant" path).
    /// </summary>
    private void DoGreaterRefine(Hunter hunter, ItemInstance? item)
    {
        if (item is null || !Gear.IsWearable(item)) { Say("ONLY WEARABLES CAN BE REFINED.", Ember); return; }
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE REFINING.", Ember); return; }

        var r = Forge.GreaterRefine(item, Tuning);
        if (hunter.MaterialOf(Material.Crystal) < r.Crystal) { Say($"NEED {r.Crystal} CRYSTAL TO GREATER-REFINE.", Ember); return; }
        if (hunter.Gleam < r.Gold) { Say($"NEED {r.Gold}g TO GREATER-REFINE.", Ember); return; }

        hunter.SpendMaterial(Material.Crystal, r.Crystal);
        hunter.SpendGleam(r.Gold);
        ReplaceItem(item, r.Product);
        Say($"GREATER-REFINED TO iL{r.Product.ItemLevel}  ({r.Crystal} CRYSTAL + {r.Gold}g).", Gold);
    }

    private static bool IsWorn(Hunter hunter, ItemInstance item)
        => Gear.SlotFor(item.BaseType) is { } s && hunter.Worn(s)?.InstanceId == item.InstanceId;

    /// <summary>Swap an item in the bag for its reforged self. Same id, same grid slot — nothing else moves.</summary>
    private void ReplaceItem(ItemInstance old, ItemInstance neu)
    {
        var idx = _inv.IndexOf(old);
        if (idx >= 0) _inv[idx] = neu;
        // The id is unchanged, so the merge tray and worn tracking keep resolving to the new object, and
        // the cursor sits on the same grid index — which now holds the reforged item. Nothing to re-point.
    }

    // ── CHESTS — the dopamine beat. Boss drops land here; the player cracks them for materials + items. ──

    /// <summary>Open the BEST unopened chest — the grade you actually want to see revealed.</summary>
    /// <summary>
    /// Open ONE named chest — what the Vault's OPEN THIS button drives.
    /// </summary>
    /// <remarks>
    /// The Forge's own button opens whichever chest is best, because the Forge shows a count and nothing
    /// else. The Vault shows the pile, so the player can point at one — and a page that lets you read
    /// six chests and then opens a seventh of its own choosing would be worse than no page at all.
    ///
    /// Routed through the Forge rather than done in the Vault because everything downstream of a chest
    /// lives here: the loot filter, the reveal burst, auto-merge, the footer line. A second opener would
    /// be a second copy of all of it.
    /// </remarks>
    public void OpenOneChest(Chest chest, Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(chest);
        var idx = _chests.IndexOf(chest);
        if (idx < 0) { Say("THAT CHEST IS ALREADY OPEN.", Slate); return; }

        _chests.RemoveAt(idx);
        Reveal(chest, LandChest(chest, hunter));
    }

    /// <summary>Crack every chest at once — the Vault's OPEN ALL, and the Forge's.</summary>
    public void OpenEveryChest(Hunter hunter) => OpenAllChests(hunter);

    private void OpenBestChest(Hunter hunter)
    {
        if (_chests.Count == 0) { Say("NO CHESTS YET — CLEAR A BOSS WAVE.", Slate); return; }

        var idx = 0;
        for (var i = 1; i < _chests.Count; i++)
            if (_chests[i].Rarity > _chests[idx].Rarity) idx = i;

        var chest = _chests[idx];
        _chests.RemoveAt(idx);
        Reveal(chest, LandChest(chest, hunter));
    }

    /// <summary>The reveal burst and the footer line for one opened chest. Shared by both openers.</summary>
    private void Reveal(Chest chest, (int Materials, List<ItemInstance> Items) landed)
    {
        var (mat, items) = landed;

        // The REVEAL: a centred burst you can't miss — the anticipation payoff loot design is built on.
        _revealGrade = chest.Rarity;
        _revealMaterials = mat;
        _revealItems.Clear();
        _revealItems.AddRange(items);
        _revealTimer = RevealHold;

        // Keep the footer line too (for OPEN ALL and as a fallback once the burst fades).
        var names = items.Count == 0
            ? "SOLD ON SIGHT (FILTER)"
            : string.Join(", ", items.Select(i => $"{RarityNames[(int)i.Rarity]} {ItemNames[i.BaseType]}"));
        Say($"{names}   +{mat} MAT",
            items.Count > 0 ? RarityColors[items.Max(i => (int)i.Rarity)] : Slate);
    }

    /// <summary>Crack every chest at once — the idle-convenience twin of AUTO-MERGE ALL.</summary>
    private void OpenAllChests(Hunter hunter)
    {
        if (_chests.Count == 0) { Say("NO CHESTS YET — CLEAR A BOSS WAVE.", Slate); return; }

        var count = _chests.Count;
        var mat = 0;
        var items = 0;
        foreach (var chest in _chests.ToList())
        {
            var (m, landed) = LandChest(chest, hunter);
            mat += m;
            items += landed.Count;
        }
        _chests.Clear();
        if (AutoMergeOnOpen && items > 0) AutoMergeAll(hunter);   // TIRELESS FORGE tidies the bulk haul
        Say($"OPENED {count} CHESTS — +{mat} MAT, {items} ITEMS.", Gold);
    }

    /// <summary>Roll a chest's contents, honour the loot filter, land the items + materials. Returns what landed.</summary>
    private (int Materials, List<ItemInstance> Items) LandChest(Chest chest, Hunter hunter)
    {
        var reward = Chests.Open(chest, _rng, LootTuning.Default, rarityBonus: RarityBonus);
        _chestsOpened++;   // the CRAFTER evolution path's earn — Game1 polls this and credits the warren
        hunter.AddMaterials(reward.Materials);

        var items = reward.Items.ToList();
        // Same loot filter a boss drop honours: filtered items are SOLD for gleam, never dumped in the bag.
        if (AutoSellFloor is { } floor)
        {
            foreach (var it in items.Where(i => i.Rarity <= floor)) hunter.AddGleam(it.SellValue);
            items = items.Where(i => i.Rarity > floor).ToList();
        }

        _inv.AddRange(items);
        return (reward.Materials, items);
    }

    private void Say(string m, Color c) { _msg = m; _msgColor = c; }
    private bool Pressed(KeyboardState now, Keys k) => now.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, Hunter hunter) => Draw(b, hunter, new Point(-1, -1), false);

    /// <summary>
    /// The Forge, spec rev 1: a centred title, a mode rail, and one of three mode surfaces. UPGRADE and
    /// REFORGE are the reference's focused item flows; SALVAGE is the full bag/chest hub (unchanged).
    /// </summary>
    public void Draw(SpriteBatch b, Hunter hunter, Point mouse, bool clicked)
    {
        // Every rect is authored ×4 (1920×1080) and rendered at scale 1, so hit-tests take the mouse ×4.
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        _hovered = null;                 // re-established by whichever surface finds the pointer over an item

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xC0));   // scrim so panels pop
        _ui.TextCenterBig(b, "THE FORGE", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        // The subtitle rides under the title in the focused modes; SALVAGE has its own dense toolbar there.
        if (_mode != ForgeMode.Salvage) _ui.TextCenterBig(b, ModeSubtitle(), 960, 80, Slate, UiTypography.Secondary);

        DrawCharters(b, hunter);

        switch (_mode)
        {
            case ForgeMode.Upgrade: DrawUpgradeMode(b, hunter, hit, clicked); break;
            case ForgeMode.Reforge: DrawReforgeMode(b, hunter, hit, clicked); break;
            default: DrawSalvageMode(b, hunter, hit, clicked); break;
        }

        DrawReveal(b);   // the chest-open burst rides on top of every mode

        // The hover card, above the surfaces and below nothing but the reveal — which is modal, and
        // whose whole job is to be the only thing you are looking at.
        if (_hovered is { } hov && _revealTimer <= 0f)
            ItemTooltip.Draw(_ui, b, hov, hunter, hit, new Rectangle(0, 0, 1920, 1080));

        if (DevForgeDebug) DrawDebug(b);
    }

    private string ModeSubtitle() => _mode switch
    {
        // Says what the mode DOES rather than naming the three panels under it.
        ForgeMode.Upgrade => "SPEND MATERIALS TO RAISE THIS ITEM'S LEVEL",
        ForgeMode.Reforge => "RE-ROLL WHAT AN ITEM ROLLED — ITS LEVEL AND SOURCE STAY",
        _ => "BREAK DOWN WHAT YOU WILL NOT WEAR, OR FUSE THREE INTO ONE",
    };

    // ── The mode rail — the reference's left column, listing only the verbs the model actually has. ──
    private void DrawRail(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        _ui.Panel(b, Rail);
        // +40, not +16.
        //
        // UiKit.Panel SCALES its frame art to the rectangle, so a short panel gets a proportionally
        // TALLER ornament band than a tall one — the title that reads cleanly at +18 on the 730px item
        // panel is drawn underneath the ornament on a 330px rail. Left-aligning it to dodge the centre
        // medallion only moved it into the corner piece, which was worse: it vanished outright.
        _ui.TextCenterBig(b, "FORGE", Rail.Center.X, Rail.Y + 40, Gold, UiTypography.PanelTitle);

        var modes = new[] { (ForgeMode.Upgrade, "UPGRADE"), (ForgeMode.Reforge, "REFORGE"), (ForgeMode.Salvage, "SALVAGE") };
        // TIGHTENED TO FIT THE RAIL. At +86 with a 66px pitch the loop ended at y = 470, which IS
        // Rail.Bottom — so the divider landed on the frame's bottom ornament and the hint line was drawn
        // below the panel entirely, floating on the bare dungeon wall with nothing behind it. The frame
        // art eats ~30px inward, so everything has to finish by Rail.Bottom - 30.
        var y = Rail.Y + 74;
        foreach (var (m, label) in modes)
        {
            var r = new Rectangle(Rail.X + 30, y, Rail.Width - 60, 58);
            if (_mode == m)
            {
                _ui.Fill(b, r, new Color(0x3A, 0x2E, 0x52));
                _ui.Fill(b, new Rectangle(r.X, r.Y, 5, r.Height), Gold);
                _ui.TextCenterBig(b, label, r.Center.X, r.Center.Y - 11, new Color(0xF6, 0xEA, 0xC6), UiTypography.Body);
            }
            else if (_ui.Button(b, r, label, hit, clicked)) { _mode = m; }
            y += 58;
        }

        _ui.Fill(b, new Rectangle(Rail.X + 30, y + 2, Rail.Width - 60, 2), Dim);
        _ui.TextCenterBig(b, ModeHint(), Rail.Center.X, y + 14, Slate, UiTypography.Secondary);

        DrawBag(b, hunter, hit, clicked);
    }

    private string ModeHint() => _mode switch
    {
        ForgeMode.Upgrade => "+LEVEL, +AFFIXES",
        ForgeMode.Reforge => "RE-ROLL ITS TRADE",
        _ => "BREAK DOWN, MERGE UP",
    };

    /// <summary>
    /// The bag, as a list you can see and click.
    /// </summary>
    /// <remarks>
    /// Every row carries the one thing that decides whether you care about it — its rarity, as a colour
    /// bar and as the name's ink — plus its LEVEL, because level is what UPGRADE moves and a player
    /// choosing what to refine is choosing between levels. Worn pieces are marked, because the Forge
    /// refuses to work on them and a greyed button with no reason reads as a bug.
    /// </remarks>
    private void DrawBag(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        _ui.Panel(b, BagPanel);

        var bag = _inv.Where(Gear.IsWearable).ToList();
        _ui.TextCenterBig(b, "YOUR BAG", BagPanel.Center.X, BagPanel.Y + 40, Gold, UiTypography.PanelTitle);

        // THE SCROLL COUNTER LIVES IN THE HEADER. It was a centred line in the strip between the last
        // row and the frame — a band only ~26px tall, whose centre carries the panel art's bottom
        // diamond, which ate the separator whichever y it was given. The header has clear space, and a
        // count belongs beside the thing it counts anyway.
        if (bag.Count > BagRows)
            _ui.TextRightBig(b, $"{_bagScroll + 1}-{Math.Min(bag.Count, _bagScroll + BagRows)} / {bag.Count}",
                             BagPanel.Right - 52, BagPanel.Y + 48, Slate, UiTypography.Secondary);

        if (bag.Count == 0)
        {
            _ui.TextCenter(b, "EMPTY — GO AND HUNT.", BagPanel.Center.X, BagPanel.Y + 150, Dim);
            return;
        }

        // Keep the focused item on screen without stealing the wheel from the player.
        var focus = Math.Max(0, bag.FindIndex(i => i.InstanceId == _focusId));
        if (focus < _bagScroll) _bagScroll = focus;
        if (focus >= _bagScroll + BagRows) _bagScroll = focus - BagRows + 1;
        _bagScroll = Math.Clamp(_bagScroll, 0, Math.Max(0, bag.Count - BagRows));

        for (var vis = 0; vis < BagRows; vis++)
        {
            var idx = _bagScroll + vis;
            if (idx >= bag.Count) break;

            var it = bag[idx];
            var row = BagRow(vis);
            var rc = RarityColors[(int)it.Rarity];
            var sel = it.InstanceId == _focusId;
            var hover = row.Contains(hit);
            if (hover) _hovered = it;
            var isWorn = Gear.SlotFor(it.BaseType) is { } sl && hunter.Worn(sl)?.InstanceId == it.InstanceId;

            if (UiKit.ClickedIn(row, hit, clicked)) _focusId = it.InstanceId;

            _ui.Fill(b, row, sel ? new Color(0x3A, 0x2E, 0x52)
                           : hover ? new Color(0x22, 0x1C, 0x30)
                           : new Color(0x14, 0x11, 0x1C, 0xC0));
            _ui.Fill(b, new Rectangle(row.X, row.Y, 5, row.Height), rc);

            DrawItemIcon(b, it, new Rectangle(row.X + 10, row.Y + 3, 30, 30));

            // TRUNCATED BY PIXELS, NOT BY CHARACTER COUNT. The old comment said "13 characters,
            // measured" — but a character count cannot be measured against a proportional font, and it
            // was not: thirteen wide glyphs overran the right-aligned level column and every row in the
            // bag printed its name straight through its own item level. "GREEDY MACHIiL1". The one
            // number the UPGRADE mode exists to move was illegible on every row of every mode.
            //
            // The level is measured FIRST and the name is given exactly what is left.
            var lvl = isWorn ? "WORN" : $"iL{it.ItemLevel}";
            var nameRoom = row.Right - 8 - _ui.Measure(lvl) - 16 - (row.X + 50);
            _ui.Text(b, _ui.Shorten(ItemNaming.FullName(it), nameRoom), row.X + 50, row.Y + 10,
                     sel ? Bone : rc);
            _ui.TextRight(b, lvl, row.Right - 8, row.Y + 10, isWorn ? Gold : Slate);
        }

        // The chest beat survives, sitting IN the list where the items are rather than in a header strip
        // that stole a row from them whether or not a chest existed.
        if (_chests.Count > 0)
        {
            var best = _chests.Max(c => (int)c.Rarity);
            var cr = new Rectangle(BagPanel.X + 30, BagPanel.Bottom - 92, BagPanel.Width - 60, 40);
            _ui.Fill(b, new Rectangle(cr.X, cr.Y, 5, cr.Height), RarityColors[best]);
            if (_ui.Button(b, cr, $"OPEN {_chests.Count} CHEST{(_chests.Count == 1 ? "" : "S")}", hit, clicked))
                _mode = ForgeMode.Salvage;
        }

        // (The scroll counter that used to live here moved into the header — see the note beside it.
        // The strip between the last row and the frame is ~26px and its centre carries the panel art's
        // bottom diamond, so nothing legible fits in it.)
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "\u2026";

    // ── UPGRADE = the real REFINE: +1 item level, which raises every affix. Deterministic, so the "after"
    //    column is a truthful preview, not a gamble — there is no success rate or downgrade to display. ──
    /// <summary>
    /// The charters you are holding, across the top of every Forge mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A permission you do not know you hold is not a permission. These are spent AUTOMATICALLY by the
    /// operation they name — there is no "use charter" button, because a button would be a second
    /// decision on top of a decision the player has already made ("refine this"). So the only thing the
    /// screen owes them is a visible count and a clear statement that the next one is free.
    /// </para>
    /// <para>
    /// Drawn on every mode rather than only the relevant one: a REFORGE CHART is a reason to switch to
    /// the REFORGE tab, and it cannot be that if it is only visible once you get there.
    /// </para>
    /// </remarks>
    private void DrawCharters(SpriteBatch b, Hunter hunter)
    {
        var held = Enum.GetValues<Charter>().Where(c => hunter.CharterCount(c) > 0).ToList();
        if (held.Count == 0) return;

        const int y = 108;
        var x = 1000;

        _ui.TextRight(b, "CHARTS", 980, y, Slate);
        foreach (var c in held)
        {
            var n = hunter.CharterCount(c);
            var label = n > 1 ? $"{Charters.Short(c)} x{n}" : Charters.Short(c);
            var w = _ui.Measure(label) + 26;

            _ui.Fill(b, new Rectangle(x, y - 6, w, 30), new Color(0x1C, 0x2A, 0x24));
            _ui.Fill(b, new Rectangle(x, y - 6, 4, 30), Met);
            _ui.Text(b, label, x + 14, y, Met);
            x += w + 12;
        }
    }

    private void DrawUpgradeMode(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        DrawRail(b, hunter, hit, clicked);
        var item = Target();
        var refined = item is null ? null : Forge.Refine(item, Tuning).Product;
        DrawItemPanel(b, item, refined, hunter);

        _ui.Panel(b, CostPanel);
        _ui.TextCenterBig(b, "REQUIRED MATERIALS", CostPanel.Center.X, CostPanel.Y + 22, Gold, UiTypography.SectionTitle);

        if (item is null)
        {
            _ui.TextCenter(b, "NOTHING TO UPGRADE.", CostPanel.Center.X, CostPanel.Y + 320, Slate);
            _ui.Panel(b, ResultPanel);
            _ui.TextCenterBig(b, "RESULT PREVIEW", ResultPanel.Center.X, ResultPanel.Y + 22, Gold, UiTypography.SectionTitle);
            return;
        }

        // Your four salvage tiers, at a glance — Refine spends SCRAP + Gold, the others fund REFORGE.
        _ui.Text(b, "YOUR MATERIALS", CostPanel.X + 40, CostPanel.Y + 68, Slate);
        var mats = new[] { Material.Scrap, Material.Essence, Material.Core, Material.Crystal };
        for (var i = 0; i < 4; i++)
        {
            var cell = new Rectangle(CostPanel.X + 40 + i * 108, CostPanel.Y + 100, 98, 94);
            _ui.Fill(b, cell, new Color(0x16, 0x12, 0x20, 0xC0));
            _ui.Diamond(b, new Rectangle(cell.Center.X - 20, cell.Y + 12, 40, 40), MatColor[i]);
            // AT THE DEFAULT SIZE "ESSENCE" AND "CRYSTAL" ARE WIDER THAN THEIR 98px CELL, so the four
            // labels ran together into "SCRAPESSENCECORECRYSTAL" and each sat across its own tile's
            // lower border. CostPanel is 512 wide, so the cells cannot grow — the type has to shrink,
            // and it is shortened to the cell as a backstop for a future longer name.
            _ui.TextCenterBig(b, _ui.ShortenBig(MaterialTiers.Name(mats[i]), cell.Width - 10, UiTypography.Secondary),
                              cell.Center.X, cell.Bottom - 42, Slate, UiTypography.Secondary);
            _ui.TextCenterBig(b, $"{hunter.MaterialOf(mats[i]):N0}", cell.Center.X, cell.Bottom - 22,
                              Bone, UiTypography.Secondary);
        }

        var r = Forge.Refine(item, Tuning);
        var worn = IsWorn(hunter, item);
        _ui.Fill(b, new Rectangle(CostPanel.X + 40, CostPanel.Y + 216, CostPanel.Width - 80, 2), Dim);
        _ui.Text(b, "UPGRADE COST", CostPanel.X + 40, CostPanel.Y + 236, Slate);
        DrawCostRow(b, CostPanel.Y + 274, "SCRAP", hunter.MaterialOf(Material.Scrap), r.Scrap, MatColor[0], false);
        DrawCostRow(b, CostPanel.Y + 326, "GLEAM", hunter.Gleam, r.Gold, Gold, true);

        _ui.Fill(b, new Rectangle(CostPanel.X + 40, CostPanel.Y + 392, CostPanel.Width - 80, 2), Dim);
        // Shortened: the long form ran 76px past the panel interior.
        _ui.Text(b, "GUARANTEED  ·  NO DOWNGRADE", CostPanel.X + 40, CostPanel.Y + 412, Met);
        DrawWrapped(b, "No cap — the cost climbs each level.", CostPanel.X + 40, CostPanel.Y + 442, CostPanel.Width - 80, Slate);

        var can = !worn && hunter.MaterialOf(Material.Scrap) >= r.Scrap && hunter.Gleam >= r.Gold;
        var greater = Forge.GreaterRefine(item, Tuning);
        var hasCrystal = hunter.MaterialOf(Material.Crystal) >= greater.Crystal;
        var kb = Keyboard.GetState();
        var doGreat = (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift)) && hasCrystal;
        var canGreat = !worn && hunter.Gleam >= greater.Gold;

        if (worn) _ui.TextCenter(b, "EQUIPPED — UNEQUIP ON GEAR TO UPGRADE", CostPanel.Center.X, CostPanel.Bottom - 120, Ember);
        else if (hasCrystal) _ui.TextCenter(b, "HOLD SHIFT: +5 FOR 1 CRYSTAL", CostPanel.Center.X, CostPanel.Bottom - 152, Slate);

        var btn = new Rectangle(CostPanel.X + 40, CostPanel.Bottom - 96, CostPanel.Width - 80, 72);
        if (_ui.Button(b, btn, doGreat ? "GREATER UPGRADE  +5 iL" : "UPGRADE  +1 iL", hit, clicked, enabled: doGreat ? canGreat : can))
        {
            if (doGreat) DoGreaterRefine(hunter, item); else DoRefine(hunter, item);
        }

        DrawResultPanel(b, item, refined!, hunter, hit, clicked, worn, can);
    }

    private void DrawResultPanel(SpriteBatch b, ItemInstance item, ItemInstance refined, Hunter hunter, Point hit, bool clicked, bool worn, bool can)
    {
        _ui.Panel(b, ResultPanel);
        _ui.TextCenterBig(b, "RESULT PREVIEW", ResultPanel.Center.X, ResultPanel.Y + 22, Gold, UiTypography.SectionTitle);

        var rc = RarityColors[(int)item.Rarity];
        var el = item.Element is { } e ? e.ToString().ToUpperInvariant() + " " : "";
        _ui.TextBig(b, $"{el}{ItemNames[item.BaseType]}", ResultPanel.X + 40, ResultPanel.Y + 66, rc, UiTypography.PanelTitle);
        _ui.TextRightBig(b, $"iL{refined.ItemLevel}", ResultPanel.Right - 40, ResultPanel.Y + 66, Met, UiTypography.PanelTitle);

        _ui.Text(b, "AFFIX PREVIEW", ResultPanel.X + 40, ResultPanel.Y + 114, Slate);
        var nxt = ItemAffixes.Of(refined);
        var y = ResultPanel.Y + 148;
        foreach (var a in nxt)
        {
            _ui.Diamond(b, new Rectangle(ResultPanel.X + 42, y + 4, 16, 16), Bloom);
            _ui.Text(b, AffixName(a.Stat), ResultPanel.X + 58, y, Bone);
            _ui.TextRight(b, AffixVal(a), ResultPanel.Right - 40, y, Met);
            y += 34;
        }
        if (nxt.Count == 0) _ui.Text(b, "This rarity carries no explicit affixes.", ResultPanel.X + 40, y, Dim);

        // The reference's "ADDED BENEFIT" panel, made honest: an item's standing benefit is its real enchant
        // (Rare+) or trait — refine does not unlock a new passive, so none is invented.
        y = ResultPanel.Y + 360;
        _ui.Fill(b, new Rectangle(ResultPanel.X + 40, y - 14, ResultPanel.Width - 80, 2), Dim);
        if (Enchantments.Of(item) is { } ench)
        {
            _ui.Text(b, "STANDING BENEFIT", ResultPanel.X + 40, y, Slate);
            _ui.TextBig(b, ench.Name, ResultPanel.X + 40, y + 28, Bloom, UiTypography.Body);
            DrawWrapped(b, ench.Blurb, ResultPanel.X + 40, y + 60, ResultPanel.Width - 80, Bone);
        }
        else if (GearTraits.TraitOf(item) is { } tr)
        {
            _ui.Text(b, "ITEM TRAIT", ResultPanel.X + 40, y, Slate);
            _ui.TextBig(b, GearTraits.NameOf(tr), ResultPanel.X + 40, y + 28, InkGold, UiTypography.Body);
        }

        _ui.Text(b, "FORGE NOTES", ResultPanel.X + 40, ResultPanel.Bottom - 200, Slate);
        DrawWrapped(b, "Level and affixes rise. Trait, enchant and source stay.",
            ResultPanel.X + 40, ResultPanel.Bottom - 170, ResultPanel.Width - 80, Slate);

        // "CONFIRM UPGRADE" USED TO SIT HERE AND IT COST PLAYERS MATERIALS.
        //
        // It called DoRefine — the same commit as the UPGRADE +1 iL button 140px away on the same
        // baseline. Two buttons, two panels, one purchase. Read as a two-step flow ("preview, then
        // confirm") it is not merely redundant: the first click already refined the item and took the
        // Scrap and the Gleam, so the confirm bought a SECOND level nobody asked for, silently, at the
        // higher price the new level costs.
        //
        // A panel titled RESULT PREVIEW must never commit. The commit lives with the price, in
        // REQUIRED MATERIALS, where a player can see what they are spending as they press it.
    }

    // ── REFORGE = re-roll the TRAIT (Essence) or the ENCHANT (Core / Crystal). Same item preview at left. ──
    private void DrawReforgeMode(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        DrawRail(b, hunter, hit, clicked);
        var item = Target();
        DrawItemPanel(b, item, null, hunter);

        _ui.Panel(b, ReforgePanel);
        _ui.TextCenterBig(b, "REFORGE", ReforgePanel.Center.X, ReforgePanel.Y + 22, Gold, UiTypography.SectionTitle);
        if (item is null)
        {
            _ui.TextCenter(b, "NO GEAR TO REFORGE.", ReforgePanel.Center.X, ReforgePanel.Y + 320, Slate);
            return;
        }

        var worn = IsWorn(hunter, item);

        // TRAIT — the item's trade, re-rolled with ESSENCE.
        var tRow = ReforgePanel.Y + 96;
        var tCost = ReforgeTuning.Default.TraitCostFor(item.Rarity);
        var canTrait = !worn && hunter.MaterialOf(Material.Essence) >= tCost;
        _ui.Text(b, "TRAIT  —  THE ITEM'S TRADE", ReforgePanel.X + 32, tRow, Slate);
        _ui.TextBig(b, GearTraits.TraitOf(item) is { } t ? GearTraits.NameOf(t) : "—", ReforgePanel.X + 32, tRow + 28, InkGold, UiTypography.PanelTitle);

        // WHAT THE TRAIT DOES. The row calls it "the item's trade" and then printed only its NAME, while
        // the enchant row beneath it printed a full sentence — so the one thing on this screen that both
        // gives and takes was the one thing the player could not read. GearBlurb already existed and was
        // drawn on the inventory card; it simply was not drawn here.
        var traitBlurb = TraitOnlyBlurb(item);
        if (traitBlurb.Length > 0)
            _ui.Text(b, traitBlurb, ReforgePanel.X + 32, tRow + 68, Bone);

        _ui.TextRight(b, $"{hunter.MaterialOf(Material.Essence):N0} / {tCost} ESSENCE", ReforgePanel.Right - 360, tRow + 34, canTrait ? Met : Ember);
        if (_ui.Button(b, new Rectangle(ReforgePanel.Right - 340, tRow + 12, 300, 60), "RE-ROLL", hit, clicked, enabled: canTrait))
            DoReforgeTrait(hunter, item);

        _ui.Fill(b, new Rectangle(ReforgePanel.X + 32, tRow + 108, ReforgePanel.Width - 64, 2), Dim);

        // ENCHANT — the build-defining trigger, Rare+ only, re-rolled with CORE (Legendary spends CRYSTAL).
        var eRow = tRow + 140;
        var hasEnch = item.Rarity >= Enchantments.MinimumRarity;
        var eTier = EnchantReforgeTier(item.Rarity);
        var eCost = ReforgeTuning.Default.EnchantCostFor(item.Rarity);
        var canEnch = !worn && hasEnch && hunter.MaterialOf(eTier) >= eCost;
        var ench = Enchantments.Of(item);
        // CLAMPED TO THE COLUMN THE BUTTON LEAVES. The heading had no width bound and ran under the
        // RE-ROLL button's ornate left frame at ReforgePanel.Right - 340, eating "RE+)".
        _ui.Text(b, _ui.Shorten("ENCHANT  —  THE BUILD-DEFINING TRIGGER (RARE+)",
                                (ReforgePanel.Right - 360) - (ReforgePanel.X + 32) - 24),
                 ReforgePanel.X + 32, eRow, Slate);
        _ui.TextBig(b, hasEnch ? ench?.Name ?? "—" : "LOCKED — RARE+ ONLY", ReforgePanel.X + 32, eRow + 28, hasEnch ? Bloom : Dim, UiTypography.PanelTitle);
        if (ench is not null) DrawWrapped(b, ench.Blurb, ReforgePanel.X + 32, eRow + 64, ReforgePanel.Width - 400, Bone);
        _ui.TextRight(b, $"{hunter.MaterialOf(eTier):N0} / {eCost} {MaterialTiers.Name(eTier)}", ReforgePanel.Right - 360, eRow + 34, canEnch ? Met : Ember);
        if (_ui.Button(b, new Rectangle(ReforgePanel.Right - 340, eRow + 12, 300, 60), "RE-ROLL", hit, clicked, enabled: canEnch))
            DoReforgeEnchant(hunter, item);

        _ui.Fill(b, new Rectangle(ReforgePanel.X + 32, eRow + 130, ReforgePanel.Width - 64, 2), Dim);
        if (worn) _ui.Text(b, "EQUIPPED — UNEQUIP ON GEAR TO REFORGE.", ReforgePanel.X + 32, eRow + 150, Ember);
        // WRAPPED. As a single _ui.Text line this measured ~1100px against 1018px of interior, so it ran
        // through the panel's right rail and was chopped at the screen edge mid-word — the sentence
        // ended on "ke". The enchant blurb six lines above already uses this helper.
        else DrawWrapped(b, "Re-forging changes only the trait or enchant; level, source and affixes are kept.",
                         ReforgePanel.X + 32, eRow + 150, ReforgePanel.Width - 64, Slate);
        if (_msg.Length > 0) _ui.Text(b, _msg, ReforgePanel.X + 32, ReforgePanel.Bottom - 40, _msgColor);
    }

    // ── The shared "ITEM PREVIEW" column: the item, its source, and (in UPGRADE) the before→after readout. ──
    private void DrawItemPanel(SpriteBatch b, ItemInstance? item, ItemInstance? refined, Hunter hunter)
    {
        _ui.Panel(b, ItemPanel);
        _ui.TextCenterBig(b, "ITEM PREVIEW", ItemPanel.Center.X, ItemPanel.Y + 22, Gold, UiTypography.SectionTitle);
        if (item is null)
        {
            _ui.TextCenter(b, "NO GEAR IN THE BAG.", ItemPanel.Center.X, ItemPanel.Y + 300, Slate);
            _ui.TextCenter(b, "GO HUNT SOMETHING.", ItemPanel.Center.X, ItemPanel.Y + 330, Dim);
            return;
        }

        var rc = RarityColors[(int)item.Rarity];
        _ui.TextCenterBig(b, ItemNaming.FullName(item), ItemPanel.Center.X, ItemPanel.Y + 56, rc, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, $"{RarityNames[(int)item.Rarity]}  ·  ITEM LEVEL {item.ItemLevel}", ItemPanel.Center.X, ItemPanel.Y + 90, Slate, UiTypography.Secondary);

        DrawItemIcon(b, item, new Rectangle(ItemPanel.Center.X - 100, ItemPanel.Y + 120, 200, 200));

        if (item.Element is { } src)
        {
            if (_ui.Assets.Get($"source_{src.ToString().ToLowerInvariant()}") is { } sg)
                b.Draw(sg, new Rectangle(ItemPanel.Center.X - 24, ItemPanel.Y + 330, 48, 48), Color.White);
            // CALLED "FORGE ELEMENT", NOT "SOURCE", and the difference is the whole point.
            //
            // Playtest: "gear power hesaplamasında bir hata olabilir. Nature item tamamen spirit skill
            // setupunda daha yüksekmiş gibi gösteriliyor." The maths is fine — every ITEM POWER in the
            // game comes from one function, Gear.PowerRating, and an item's Element is not one of its
            // five inputs at any depth. What was wrong was this label.
            //
            // The game has two unrelated things called "Source". The FIGHT's Source matchup keys off
            // the woven SKILL's source against the CREATURE's; an item's Element is a CRAFTING axis
            // that the Forge's element-hoarding merge reads and nothing else. Labelling the second one
            // "SOURCE" and drawing it with the same source_* gem art as the first told the player they
            // were the same system — so a Nature item in a Spirit build looked like a mismatch that
            // should be costing them power, and the number that ignored it looked like a bug.
            _ui.TextCenterBig(b, $"FORGE ELEMENT  ·  {src.ToString().ToUpperInvariant()}",
                              ItemPanel.Center.X, ItemPanel.Y + 386, Bloom, UiTypography.Secondary);
            // SHORTENED TO THE COLUMN. The full sentence measured ~435px against a 384-wide panel, so it
            // painted "for me" on the bare stone left of the frame and "t this" past the right rail, and
            // its descenders touched the ITEM LEVEL row twelve pixels below. The panel cannot grow —
            // this is the narrow preview column — so the copy fits it, and the rows below move down.
            _ui.TextCenterBig(b, _ui.ShortenBig("for merging — not the fight's Source",
                                                ItemPanel.Width - 40, UiTypography.Secondary),
                              ItemPanel.Center.X, ItemPanel.Y + 408, Slate, UiTypography.Secondary);
        }

        DrawTransition(b, "ITEM LEVEL", $"iL{item.ItemLevel}", refined is null ? null : $"iL{refined.ItemLevel}", ItemPanel.Y + 436);
        DrawTransition(b, "ITEM POWER", $"{hunter.PowerContribution(item):N0}", refined is null ? null : $"{hunter.PowerContribution(refined):N0}", ItemPanel.Y + 480);

        var cur = ItemAffixes.Of(item);
        var nxt = refined is null ? null : ItemAffixes.Of(refined);
        var ay = ItemPanel.Y + 524;
        _ui.Text(b, cur.Count > 0 ? "AFFIXES" : "NO AFFIXES (RARITY GATES COUNT)", ItemPanel.X + 28, ay, Slate);
        ay += 32;
        // BUILT RIGHT TO LEFT, so the name gets whatever the numbers leave and never one pixel more.
        // Drawn left-first, the row rendered "CRIT CHANCE2.7%" — the label ran under the before-value
        // with no gap at all, which is the single most-reported unreadable thing on this screen. The
        // values are the part you cannot abbreviate; the name is.
        var nameX = ItemPanel.X + 28;
        for (var i = 0; i < cur.Count; i++)
        {
            int valuesStart;
            if (nxt is not null && i < nxt.Count)
            {
                _ui.TextRight(b, AffixVal(nxt[i]), ItemPanel.Right - 28, ay, Met);
                var aw = _ui.Measure(AffixVal(nxt[i]));
                Arrow(b, ItemPanel.Right - 28 - aw - 26, ay + 3, Bloom);
                var beforeRight = ItemPanel.Right - 28 - aw - 52;
                _ui.TextRight(b, AffixVal(cur[i]), beforeRight, ay, Slate);
                valuesStart = beforeRight - _ui.Measure(AffixVal(cur[i]));
            }
            else
            {
                _ui.TextRight(b, AffixVal(cur[i]), ItemPanel.Right - 28, ay, InkGold);
                valuesStart = ItemPanel.Right - 28 - _ui.Measure(AffixVal(cur[i]));
            }

            _ui.Text(b, Shorten(AffixName(cur[i].Stat), valuesStart - nameX - 16), nameX, ay, Bone);
            ay += 34;
        }
    }

    /// <summary>Trim to a pixel width with an ellipsis. A short label beats a label drawn through a number.</summary>
    /// <remarks>Moved to <see cref="UiKit.Shorten"/> \u2014 the Build screen's passives list needed it too.</remarks>
    private string Shorten(string text, int width) => _ui.Shorten(text, width);

    /// <summary>One before/after row in the item preview.</summary>
    /// <remarks>
    /// THE LABEL AND THE VALUE ARE DRAWN AT THE SAME SIZE, and that is the fix rather than a style
    /// preference. SmoothFont draws from the TOP-LEFT of the em box, so two different type sizes sharing
    /// a y are top-aligned rather than baseline-aligned — the label went through the default ~32px
    /// raster and the values through the 19px Body role, leaving a ~13px baseline gap that made every
    /// value read as a superscript hanging off its label's shoulder.
    /// </remarks>
    private void DrawTransition(SpriteBatch b, string label, string cur, string? after, int y)
    {
        _ui.TextBig(b, label, ItemPanel.X + 28, y, Slate, UiTypography.Body);
        if (after is null) { _ui.TextRightBig(b, cur, ItemPanel.Right - 28, y, Bone, UiTypography.Body); return; }
        _ui.TextRightBig(b, after, ItemPanel.Right - 28, y, Met, UiTypography.Body);
        var aw = _ui.MeasureBig(after, UiTypography.Body);
        Arrow(b, ItemPanel.Right - 28 - aw - 28, y + 5, Bloom);
        _ui.TextRightBig(b, cur, ItemPanel.Right - 28 - aw - 54, y, Slate, UiTypography.Body);
    }

    private void DrawCostRow(SpriteBatch b, int y, string label, long owned, int required, Color gem, bool gleam)
    {
        var ok = owned >= required;
        if (gleam && _ui.Assets.Get("currency_gleam") is { } gi) b.Draw(gi, new Rectangle(CostPanel.X + 42, y, 40, 40), Color.White);
        else _ui.Diamond(b, new Rectangle(CostPanel.X + 32, y + 4, 36, 36), gem);
        _ui.TextBig(b, label, CostPanel.X + 84, y + 6, Bone, UiTypography.Body);
        // ABBREVIATED, like the currency pill directly above it on the same screen. The raw form printed
        // "131,900,000 / 79" beside a pill reading "131.9M" — the same number, twice, in two notations —
        // and it dwarfed the SCRAP row so the two costs stopped reading as a pair. It also crowds the
        // panel's right ornament as balances grow.
        _ui.TextRightBig(b, $"{Ab(owned)} / {Ab(required)}", CostPanel.Right - 30, y + 6,
                         ok ? Met : Ember, UiTypography.Body);
    }

    /// <summary>The house abbreviation, shared with the currency pills and the Warren.</summary>
    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    /// <summary>A small solid right-pointing triangle — the before→after arrow, sized ~20px tall.</summary>
    private void Arrow(SpriteBatch b, int x, int cy, Color c)
    {
        for (var i = 0; i < 10; i++) _ui.Fill(b, new Rectangle(x + i, cy - (10 - i), 2, (10 - i) * 2), c);
    }

    private void DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.Measure(probe) > width && line.Length > 0) { _ui.Text(b, line, x, y, c); y += 26; line = w; }
            else line = probe;
        }
        if (line.Length > 0) _ui.Text(b, line, x, y, c);
    }

    private static string AffixName(AffixStat s) => s switch
    {
        AffixStat.Damage => "DAMAGE", AffixStat.Health => "HEALTH", AffixStat.SkillRate => "SKILL RATE",
        AffixStat.Haul => "HAUL", AffixStat.Crit => "CRIT CHANCE", _ => "DEFENSE",
    };

    private static string AffixVal(ItemAffix a) => a.Stat switch
    {
        AffixStat.Crit => $"+{a.Magnitude:0.0}%",
        AffixStat.Defense => $"+{a.Magnitude:0}",
        _ => $"+{a.Magnitude * 100f:0}%",
    };

    private void DrawDebug(SpriteBatch b)
    {
        var rects = _mode switch
        {
            ForgeMode.Upgrade => new[] { Rail, ItemPanel, CostPanel, ResultPanel },
            ForgeMode.Reforge => new[] { Rail, ItemPanel, ReforgePanel },
            _ => new[] { new Rectangle(32, 160, 1000, 592), new Rectangle(1056, 88, 832, 552) },
        };
        foreach (var r in rects)
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav FORGE  mode {_mode}  focus {_focusId ?? "—"}", 320, 112, Gold, UiTypography.Secondary);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    private void DrawSalvageMode(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        // ── Inventory — a loot-card grid, not a text list. The panel starts ABOVE the chest toolbar and
        //    swallows it: the toolbar, the filter and the grid are one thing (what you have and what you
        //    can do to the pile of it), and a row of buttons floating on the bare scene above a panel
        //    read as leftovers from a different screen. ──────────────────────────────────────────────
        _ui.Panel(b, LootPanel);
        var view = View();

        // ── CHESTS — the toolbar row inside the panel's head. A boss's drop lands here as an unopened
        //    chest; OPEN reveals the best (grade coloured), OPEN ALL clears the lot. ──
        if (_chests.Count > 0)
        {
            var best = _chests.Max(c => (int)c.Rarity);
            _ui.Text(b, $"{_chests.Count} CHEST{(_chests.Count == 1 ? "" : "S")}", 112, 132, RarityColors[best]);
            if (_ui.Button(b, new Rectangle(284, 112, 156, 56), "OPEN", hit, clicked)) OpenBestChest(hunter);
            if (_ui.Button(b, new Rectangle(452, 112, 204, 56), "OPEN ALL", hit, clicked)) OpenAllChests(hunter);
        }
        else
            _ui.Text(b, "NO CHESTS", 112, 132, Slate);

        // FILTER cycle — narrows a haystack of ninety to one slot or rarity. TAB cycles too. Rides the
        // toolbar row, right of the chest controls, and stops short of the panel's side ornament.
        if (_ui.Button(b, new Rectangle(668, 112, 292, 56), FilterName(_filter), hit, clicked))
            CycleFilter(1);

        _ui.Text(b, $"LOOT ({view.Count})", 112, 178, InkFaint);
        if (view.Count > Visible)
            // ROWS, NOT PAGES — and it now says so. The denominator counted ROWS (ceil(13/4) = 4) while
            // the grid shows three rows at a time, so a 13-item bag in a 12-slot grid read "1/4" for
            // what is two screenfuls. Either number is defensible; printing one and meaning the other
            // is not.
            _ui.TextRight(b, $"ROW {_scroll / Cols + 1} OF {(view.Count + Cols - 1) / Cols}", 960, 178, InkFaint);

        if (_inv.Count == 0)
            _ui.Text(b, "EMPTY — GO HUNT SOMETHING.", 64, 264, Dim);
        else if (view.Count == 0)
            _ui.Text(b, $"NOTHING MATCHES {FilterName(_filter)} — TAB TO CHANGE.", 64, 264, Dim);

        for (var vis = 0; vis < Visible && _scroll + vis < view.Count; vis++)
        {
            var idx = _scroll + vis;
            var item = view[idx];
            var card = Card(vis);
            var active = idx == _cursor;
            var queued = _merge.Contains(item.InstanceId);
            var rarity = RarityColors[(int)item.Rarity];

            if (card.Contains(hit)) _hovered = item;

            // Card body: a dark cell edged in the item's rarity colour.
            _ui.Fill(b, card, active ? new Color(0x2A, 0x24, 0x14) : new Color(0x16, 0x14, 0x1C));
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 4), rarity);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - 4, card.Width, 4), active ? Gold : rarity * 0.5f);

            // The item icon, big and centred.
            DrawItemIcon(b, item, new Rectangle(card.Center.X - 48, card.Y + 12, 96, 96));
            _ui.TextCenter(b, $"{item.SellValue}g", card.Center.X, card.Bottom - 34, Gold);

            if (queued) _ui.TextCenter(b, "*", card.Right - 24, card.Y + 8, Gold);   // in the merge tray

            // The upgrade-at-a-glance signal — the same green UP the Character bag shows, now on the loot
            // grid where the whole haul actually lives, so you no longer click every card to learn its worth.
            if (Gear.SlotFor(item.BaseType) is { } sl && !IsWorn(hunter, item)
                && hunter.PowerContribution(item) > hunter.PowerContribution(hunter.Worn(sl)))
                _ui.Text(b, "UP", card.X + 12, card.Y + 8, new Color(0x6E, 0xC8, 0x7A));

            // FITS — this item's enchantment combos with the build you are actually running.
            //
            // The combo line on the detail panel answers this one item at a time, and a chest drops
            // fourteen. Reading them meant clicking every card, which is exactly the friction that makes
            // a player stop reading and sort by rarity instead — and rarity is the one axis the loot
            // rework exists to stop being the answer. UP already says "bigger"; this says "yours", and
            // the two are deliberately different words in different colours, because an item is often
            // one and not the other and that tension IS the decision.
            if (CombosWithBuild(item))
                _ui.Text(b, "FITS", card.X + 12, card.Bottom - 34, Gold);

            if (active) Reticle(b, card, Bone);
        }

        // ── Action panel ───────────────────────────────────────────────────────────────────────
        _ui.Panel(b, ActionPanel);
        var act = Active;
        if (act is null)
            _ui.Text(b, "PICK AN ITEM ON THE LEFT.", 1088, 120, InkFaint);
        else
        {
            DrawItemIcon(b, act, new Rectangle(1096, 130, 64, 64));

            // The ELEMENT is part of the item's name, because it is part of what the item IS — and it
            // is what the Source matchup and the Forge's carry-through rule both read. An element the
            // player cannot see is an element they cannot plan around.
            // Full rolled name — "FURIOUS SHADOW BLADE" — rarity carried by the colour, not the words.
            var title = ItemNaming.FullName(act);
            _ui.Text(b, title, 1168, 142, RarityInk[(int)act.Rarity]);

            // The trait rides the same row — but only if it FITS. Measured, not estimated: at 480x270
            // "LEGENDARY MACHINE WEAPON" and "WARDING" want the same pixels, and I have already shipped
            // two collisions this session by doing this arithmetic in my head. When it doesn't fit the
            // trait is dropped, not overlapped: the EQUIP button below still names its effects.
            if (GearTraits.TraitOf(act) is { } tr)
            {
                var traitName = GearTraits.NameOf(tr);
                var titleEnd = 1168 + _ui.Measure(title);
                var traitStart = 1840 - _ui.Measure(traitName);
                if (traitStart > titleEnd + 16) _ui.TextRight(b, traitName, 1840, 142, InkGold);
            }

            // The ENCHANTMENT gets its own line: it is a sentence about WHEN, not a number, and it is
            // the only thing on this panel that changes what the fight does rather than by how much.
            // It sits BELOW the affix rows with real clearance — at 564 it landed 12px into the second
            // affix row, and an item with four affixes rendered "+6% HP" through "TRANSFORM LEECHES 2X".
            if (Enchantments.Of(act) is { } ench)
            {
                // FIXED COLUMNS. Right-aligning the blurb was not enough: with a long name the two ends
                // met in the middle and rendered as one word ("SPLINTERON A KILL"). A column each means
                // neither can grow into the other, whatever the name.
                _ui.Text(b, ench.Name, 1088, 596, Bloom);
                _ui.Text(b, ench.Blurb, 1376, 596, Ink);

                // The loot philosophy, made visible: a Form-combo enchantment is LIVE only if your build
                // runs its Form. Gold "COMBOS YOUR BUILD" when it fits; a grey "NEEDS … IN YOUR BUILD"
                // when it is dead weight for you — so the choice is "does this fit my build", not "bigger".
                // A combo can now ask about any of the build's three axes — an equipped Form, a socketed
                // keystone, or a sworn Vow — so the question is asked of whichever one the enchantment
                // names rather than of Forms alone. A keystone combo shown as live because the player
                // happened to run the right Form would be the same lie in the other direction.
                if (ench.Needs is { } need)
                {
                    var live = CombosWithBuild(act);
                    _ui.Text(b, live ? "COMBOS YOUR BUILD" : $"NEEDS {need.Label} IN YOUR BUILD",
                        1088, 634, live ? InkGold : InkFaint);
                }
            }

            var wearable = Gear.IsWearable(act);
            var worn = wearable && Gear.SlotFor(act.BaseType) is { } s0 && hunter.Worn(s0)?.InstanceId == act.InstanceId;

            // EQUIP is the headline action now — gear is the Hunter's power curve.
            if (wearable)
            {
                var eq = new Rectangle(1088, 200, 768, 72);
                if (_ui.Button(b, eq, worn ? "TAKE OFF" : $"EQUIP   {GearBlurb(act)}", hit, clicked))
                    ToggleEquip(hunter, act);
            }

            var row = wearable ? 288 : 200;
            var sell = new Rectangle(1088, row, 368, 72);
            var dis = new Rectangle(1488, row, 368, 72);
            var merge = new Rectangle(1088, row + 88, 368, 72);

            // A worn item must be taken off before it can be sold or scrapped. (FEED was retired with the
            // creature system — the fates of unwanted loot are now SELL / DISMANTLE / MERGE / EQUIP.)
            if (_ui.Button(b, sell, $"SELL  +{act.SellValue}g", hit, clicked, enabled: !worn)) Sell(hunter, act);
            if (_ui.Button(b, dis, $"DISMANTLE +{Forge.Dismantle(act, Tuning)} MAT", hit, clicked, enabled: !worn))
                Dismantle(hunter, act);

            var queued = _merge.Contains(act.InstanceId);
            if (_ui.Button(b, merge, queued ? "UNQUEUE" : "MERGE (3→1)", hit, clicked, enabled: !worn))
                ToggleMerge(hunter, act);
        }

        // ── The selected item's rolled AFFIXES and its item LEVEL — the multi-stat bonuses that make
        //    one drop beat another of the same base. (Full worn gear lives on the CHARACTER screen now.) ──
        if (Active is { } sel)
        {
            var affixes = ItemAffixes.Of(sel);
            _ui.Text(b, $"iL{sel.ItemLevel}   {(affixes.Count > 0 ? "AFFIXES" : "NO AFFIXES")}", 1088, 472, InkFaint);
            for (var i = 0; i < affixes.Count; i++)
                _ui.Text(b, ItemAffixes.Describe(affixes[i]), 1088 + i % 2 * 392, 512 + i / 2 * 40, InkGold);
        }
        _ui.TextRight(b, $"POWER {hunter.PowerRating}", 1856, 480, InkGold);

        // ── Merge tray ─────────────────────────────────────────────────────────────────────────
        _ui.Panel(b, TrayPanel);
        _ui.Text(b, "MERGE TRAY", 1128, 756, InkFaint);

        var picked = _inv.Where(i => _merge.Contains(i.InstanceId)).ToList();
        for (var i = 0; i < 3; i++)
        {
            var slot = new Rectangle(1128 + i * 80, 796, 68, 68);
            _ui.Panel(b, slot);
            if (i < picked.Count) DrawItemIcon(b, picked[i], new Rectangle(slot.X + 8, slot.Y + 8, 52, 52));
        }

        var ready = MergeReady();
        var mergeBtn = new Rectangle(1388, 800, 430, 60);

        if (picked.Count == 3 && ready)
        {
            // THE RECIPE, stated before you commit. The result is decided, not rolled, so the preview
            // cannot lie — and a preview that could lie would be worse than none. This is what makes
            // the tray a recipe book rather than a slot machine: you are told what you are making.
            //
            // It rides the label row (right-aligned) because every other row in this panel is already
            // occupied by a button — below the slots it was clipped by AUTO-MERGE ALL. Violet means
            // HYBRID; colour carries it, as it does for every other state on these screens.
            var (type, element, isHybrid) = Forge.Preview(picked);
            var attune = element is { } e ? $"{e.ToString().ToUpperInvariant()} " : "";
            _ui.TextRight(b, $"-> {attune}{ItemNames[type]}", 1818, 756,
                isHybrid ? Bloom : RarityInk[(int)(picked[0].Rarity + 1)]);
        }
        else if (picked.Count == 3)
        {
            _ui.TextRight(b, "SAME RARITY ONLY", 1818, 756, InkFaint);
        }

        if (_ui.Button(b, mergeBtn, "MERGE", hit, clicked, enabled: ready)) DoMerge(hunter);

        // Fusing three-at-a-time by hand out of a bag of ninety is busywork. One button does the lot.
        var autoBtn = new Rectangle(1128, 880, 690, 60);
        // The button is live only when a REAL trio exists: three distinct-id, non-worn wearables of one
        // sub-Legendary rarity. Counting raw items (materials, duplicate copies) lit it up when nothing
        // could actually merge — the "it won't merge though there's stuff to merge" the player hit.
        var anyTrio = _inv.Where(i => Gear.IsWearable(i) && !IsWorn(hunter, i))
                          .DistinctBy(i => i.InstanceId)
                          .GroupBy(i => i.Rarity).Any(g => g.Key != Rarity.Legendary && g.Count() >= 3);
        if (_ui.Button(b, autoBtn, "AUTO-MERGE ALL", hit, clicked, enabled: anyTrio)) AutoMergeAll(hunter);

        // REFINE and REFORGE now live in their own polished modes. From the bag these two buttons carry the
        // selected item straight there — de-cluttering the hub and linking the modes (the reference's rail).
        if (act is not null && Gear.IsWearable(act))
        {
            if (_ui.Button(b, new Rectangle(1128, 952, 336, 60), "UPGRADE  >", hit, clicked))
            { _focusId = act.InstanceId; _mode = ForgeMode.Upgrade; }
            if (_ui.Button(b, new Rectangle(1482, 952, 336, 60), "REFORGE  >", hit, clicked))
            { _focusId = act.InstanceId; _mode = ForgeMode.Reforge; }
        }

        // ── Message + footer ── a panel of its own, under the loot column and level with the tray. The
        //    message is this hub's only feedback channel ("OPENED 4 CHESTS — +80 MAT, 3 ITEMS"), and it
        //    was being drawn on bare scene where it read as a caption for the floor tiles. The hint is
        //    two lines because one line of it is wider than the column it belongs to. ──
        _ui.Panel(b, FooterPanel);
        if (_msg.Length > 0) _ui.Text(b, _msg, 112, 820, _msgColor);
        _ui.Text(b, "CLICK AN ITEM, THEN A BUTTON ON THE RIGHT", 112, 884, Slate);
        _ui.Text(b, "TAB FILTERS THE PILE   ·   WHEEL SCROLLS IT   ·   J SALVAGES THE JUNK", 112, 928, Slate);
        _ui.Text(b, "UPGRADE AND REFORGE OPEN IN THEIR OWN VIEWS", 112, 972, Slate);
        // The chest-open reveal is drawn by the mode dispatcher, so it rides on top of every mode.
    }

    /// <summary>
    /// The chest-open reveal: a centred card that dims the screen behind it, shows the grade and the exact
    /// items that popped out (framed by rarity), then fades.
    /// </summary>
    /// <remarks>
    /// Anticipation is the whole engine of a chest (loot-box design 101), and a one-line footer message is
    /// not a payoff. This is the burst — dark card so the rarity colours pop, grade-coloured edges, item
    /// icons big in the middle. It holds for a beat and fades; opening another chest just refreshes it.
    /// </remarks>
    private void DrawReveal(SpriteBatch b)
    {
        if (_revealTimer <= 0f) return;

        var t = RevealHold - _revealTimer;                      // seconds SINCE the chest cracked
        var fade = Math.Clamp(_revealTimer / 0.45f, 0f, 1f);    // fade out over the last ~0.45s
        var grade = RarityColors[(int)_revealGrade];

        // Dim the Forge behind, deepening as the chest works itself up. The scrim arriving at full
        // strength on frame one is what made the old reveal read as a dialog rather than an event.
        var dim = Math.Clamp(t / (ShakeEnds * 0.6f), 0f, 1f);
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0, 0, 0, (int)(215 * dim * fade)));

        // ── BEAT 1 · THE CHEST RATTLES ────────────────────────────────────────────────────────────
        if (t < ShakeEnds)
        {
            var p = Math.Clamp(t / ShakeEnds, 0f, 1f);

            // Shake amplitude climbs with the square of progress, so the first half is a twitch and the
            // last moment is violent — the anticipation curve every loot box in the genre is built on.
            var amp = 26f * p * p;
            var jitterX = MathF.Sin(t * 61f) * amp;
            var jitterY = MathF.Cos(t * 47f) * amp * 0.5f;

            // A grade-coloured glow swelling out of the seams — four nested squares with falling alpha
            // rather than one, because a single Fill is a hard-edged RECTANGLE OF LIGHT sitting behind
            // the chest, and it reads as a bug. Four steps is enough to pass for a falloff at this size.
            for (var ring = 0; ring < 4; ring++)
            {
                var halo = (int)(130 + 150 * p) + ring * 70;
                _ui.Fill(b, new Rectangle(960 - halo / 2, 520 - halo / 2, halo, halo),
                         grade * (0.16f * p / (ring + 1)));
            }

            var box = new Rectangle((int)(960 - 110 + jitterX), (int)(520 - 110 + jitterY), 220, 220);
            if (_ui.Assets.Get("chest_loot") is { } art) b.Draw(art, box, Color.White);
            else { _ui.Fill(b, box, grade * 0.8f); }

            // ABOVE the chest. At 700 it sat behind the loot panel's lower half and read as a smudge.
            _ui.TextCenterBig(b, $"{RarityNames[(int)_revealGrade]} CHEST", 960, 340,
                grade * (0.4f + 0.6f * p), UiTypography.SectionTitle);
        }

        // ── BEAT 2 · THE BURST ────────────────────────────────────────────────────────────────────
        // A ring, PLOTTED rather than drawn from an asset: a circle of short segments whose radius
        // sweeps out and whose alpha falls away. Nothing to author, nothing to load, and retiming it is
        // editing a number — the same reasoning as the Trait screen's unlock flourish.
        if (t >= ShakeEnds && t < ShakeEnds + 0.5f)
        {
            var p = (t - ShakeEnds) / 0.5f;
            // Starts at the chest's own edge, not at 60. A ring that opens INSIDE the sprite it is
            // supposed to be bursting out of is a ring nobody sees.
            var radius = (int)(115 + 405 * p);
            var alpha = (1f - p) * (1f - p);
            // Segment count follows the RADIUS. At a fixed 64 the ring starts solid and ends as a dotted
            // circle, because the same number of dots is being spread around a circumference four times
            // longer. Density is the thing to hold constant, not count.
            var segments = Math.Clamp(radius / 6, 48, 220);
            for (var i = 0; i < segments; i++)
            {
                var a = MathF.PI * 2f * i / segments;
                var px = 960 + (int)(MathF.Cos(a) * radius);
                var py = 520 + (int)(MathF.Sin(a) * radius);
                var w = (int)(3 + 9 * (1f - p));
                _ui.Fill(b, new Rectangle(px - w / 2, py - w / 2, w, w), grade * alpha);
            }
            // The flash, brief and white-hot at the centre.
            if (p < 0.25f)
                _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Color.White * (0.35f * (1f - p / 0.25f)));
        }

        // ── BEAT 3 · THE CARD ─────────────────────────────────────────────────────────────────────
        if (t < BurstEnds) return;

        // Springs open with an overshoot, then settles. A card that simply appears is information; a
        // card that arrives is a reward.
        var cp = Math.Clamp((t - BurstEnds) / CardIn, 0f, 1f);
        var scale = cp >= 1f ? 1f : 1f + 0.18f * MathF.Sin(cp * MathF.PI) - 0.35f * (1f - cp);
        var full = new Rectangle(600, 336, 720, 416);
        var card = Grow(full, scale);

        _ui.Panel(b, card);
        _ui.Fill(b, UiKit.PanelInner(card), grade * (0.10f * fade));

        if (cp < 0.6f) return;      // the contents wait for the frame to stop moving

        _ui.TextCenterBig(b, $"{RarityNames[(int)_revealGrade]} CHEST", card.Center.X, card.Y + 52,
            grade * fade, UiTypography.SectionTitle);

        // The items that popped, big and framed by their own rarity — one at a time, each dropping the
        // last few pixels into place so the eye is led along the row instead of at all of it at once.
        var n = _revealItems.Count;
        for (var i = 0; i < n; i++)
        {
            var ip = Math.Clamp((t - BurstEnds - CardIn - i * ItemStagger) / 0.18f, 0f, 1f);
            if (ip <= 0f) continue;
            var drop = (int)(-40f * (1f - ip) * (1f - ip));
            DrawItemIcon(b, _revealItems[i], new Rectangle(960 - n * 68 + i * 136, 448 + drop, 120, 120));
        }

        var names = n == 0
            ? "SOLD ON SIGHT (FILTER)"
            : string.Join("   ", _revealItems.Select(it => $"{RarityNames[(int)it.Rarity]} {ItemNames[it.BaseType]}"));
        var nameColor = n > 0 ? RarityColors[_revealItems.Max(it => (int)it.Rarity)] : Slate;
        _ui.TextCenter(b, names, 960, 604, nameColor * fade);

        // Materials COUNT UP rather than landing finished. The number is the same; watching it arrive is
        // the difference between being told what you got and seeing it paid out.
        var mp = Math.Clamp((t - BurstEnds - CardIn) / 0.5f, 0f, 1f);
        _ui.TextCenterBig(b, $"+{(int)MathF.Round(_revealMaterials * mp)} MATERIALS", 960, 652,
            Gold * fade, UiTypography.Body);
    }

    /// <summary>Pose the chest reveal at <paramref name="t"/> seconds in, and hold it there.</summary>
    public void DevPoseReveal(float t)
    {
        _revealTimer = Math.Max(0.01f, RevealHold - t);
        _revealFrozen = true;
    }

    /// <summary>A rectangle scaled about its own centre.</summary>
    private static Rectangle Grow(Rectangle r, float scale)
    {
        var w = (int)(r.Width * scale);
        var h = (int)(r.Height * scale);
        return new Rectangle(r.Center.X - w / 2, r.Center.Y - h / 2, w, h);
    }

    private void Reticle(SpriteBatch b, Rectangle r, Color c)
    {
        const int len = 20, t = 4;
        _ui.Fill(b, new Rectangle(r.X, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Y, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, len), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.X, r.Bottom - len, t, len), c);
        _ui.Fill(b, new Rectangle(r.Right - len, r.Bottom - t, len, t), c); _ui.Fill(b, new Rectangle(r.Right - t, r.Bottom - len, t, len), c);
    }

    /// <summary>Draw an item's frame+glyph (or a rarity-coloured fallback). Shared with the Character screen.</summary>
    public void DrawItemIcon(SpriteBatch b, ItemInstance item, Rectangle box)
    {
        var frame = _ui.Assets.Get(FrameKey[(int)item.Rarity]);
        if (frame is not null) b.Draw(frame, box, Color.White);

        // The icon: per-trait art if it exists, else a package_05 item thumbnail, drawn INSET so the ornate
        // rarity frame stays visible around it. Then the Source gem (top-left) and enchant glyph (bottom-right)
        // as small modular overlays — the package_05 composition order.
        var glyph = TraitGlyph(item) ?? ItemThumb(item);
        if (glyph is not null)
        {
            var pad = frame is null ? 0 : Math.Max(4, box.Width * 15 / 100);
            b.Draw(glyph, new Rectangle(box.X + pad, box.Y + pad, box.Width - 2 * pad, box.Height - 2 * pad), Color.White);
            DrawSourceGem(b, item, box);
            DrawEnchantAccent(b, item, box);
            return;
        }

        // No glyph art at all: a rarity-coloured pip so the slot still reads as filled and its grade shows —
        // inset when a frame drew, so the border stays visible.
        var pip = frame is null
            ? box
            : new Rectangle(box.X + box.Width / 4, box.Y + box.Height / 4, box.Width / 2, box.Height / 2);
        _ui.Fill(b, pip, RarityColors[(int)item.Rarity]);
    }

    /// <summary>The item's trait-specific icon, or null if that slot/trait pair hasn't been drawn yet.</summary>
    private Texture2D? TraitGlyph(ItemInstance item)
        => Gear.SlotFor(item.BaseType) is { } slot && GearTraits.TraitOf(item) is { } trait
            // Prefer the WORN art: it is drawn front-on as the piece actually looks on the
            // character, which reads better in a grid than a display-angle icon, and it means
            // one asset serves both the inventory and the paperdoll.
            ? _ui.Assets.Get($"gear_{slot.ToString().ToLowerInvariant()}_{trait.ToString().ToLowerInvariant()}")
              ?? _ui.Assets.Get($"item_{slot.ToString().ToLowerInvariant()}_{trait.ToString().ToLowerInvariant()}")
            : null;

    /// <summary>The item's Source gem, top-left (package_05 source_&lt;element&gt;). Skipped on tiny boxes.</summary>
    private void DrawSourceGem(SpriteBatch b, ItemInstance item, Rectangle box)
    {
        if (box.Width < 80 || item.Element is not { } el) return;
        if (_ui.Assets.Get($"source_{el.ToString().ToLowerInvariant()}") is not { } sg) return;
        var s = box.Width * 2 / 5;
        b.Draw(sg, new Rectangle(box.X, box.Y, s, s), Color.White);
    }

    /// <summary>A small corner glyph for the item's enchantment (Rare+ only). Skipped on tiny boxes.</summary>
    private void DrawEnchantAccent(SpriteBatch b, ItemInstance item, Rectangle box)
    {
        if (box.Width < 88 || Enchantments.Of(item) is not { } ench) return;
        if (_ui.Assets.Get($"ench_{ench.Kind.ToString().ToLowerInvariant()}") is not { } g) return;
        var s = box.Width * 2 / 5;
        b.Draw(g, new Rectangle(box.Right - s, box.Bottom - s, s, s), Color.White);
    }

    /// <summary>The six element palette, for tinting neutral item art. Matches the creatures' Source colours.</summary>
    private static Color SourceTint(Source s) => s switch
    {
        Source.Body => new Color(0xD6, 0x48, 0x5C),
        Source.Mind => new Color(0x74, 0xC6, 0xE8),
        Source.Nature => new Color(0x48, 0xB8, 0x88),
        Source.Machine => new Color(0xBC, 0x78, 0x40),
        Source.Shadow => new Color(0x52, 0x45, 0x7E),
        _ => new Color(0xDC, 0xD4, 0xEC),
    };
}
