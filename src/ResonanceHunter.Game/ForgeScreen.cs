using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
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
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
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
        var seed = Math.Abs(item.ItemLevel * 13 + (int)item.BaseType * 7 + (int)item.Rarity * 3);
        var key = item.BaseType switch
        {
            ItemBaseType.Weapon => $"{WeaponFams[seed % WeaponFams.Length]}_{seed / 4 % 5 + 1:00}",
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
    private int _cursor;                             // the active item (click or arrows)
    private string _msg = "";
    private Color _msgColor = Bone;

    // The chest-open reveal: a centred burst that holds, then fades — the payoff beat.
    private float _revealTimer;
    private Rarity _revealGrade;
    private int _revealMaterials;
    private readonly List<ItemInstance> _revealItems = new();
    private const float RevealHold = 1.9f;
    private KeyboardState _prevKeys;
    private int _scroll;                             // index of the first visible card

    private const int Cols = 4;
    private const int VisRows = 3;                    // 4x3 = 12, matching uiref_forge; the freed strip below holds the footer
    private const int Visible = Cols * VisRows;      // loot cards shown at once

    /// <summary>Grid rectangle for the visible card at slot <paramref name="vis"/> (0..Visible-1).</summary>
    private static Rectangle Card(int vis) => new(16 + vis % Cols * 58, 56 + vis / Cols * 43, 56, 40);

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

    public IReadOnlyList<ItemInstance> Inventory => _inv;

    /// <summary>Cumulative chests cracked open — the host polls the delta to credit CRAFTER evolution.</summary>
    public int ChestsOpened => _chestsOpened;
    private int _chestsOpened;

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
    /// The creature a FEED would go to — whichever one the Warren has selected.
    /// </summary>
    /// <remarks>
    /// Feeding needs a mouth. Set by the host from the Warren's selection rather than duplicating a
    /// roster picker into the Forge: the player already chose a creature over there, and asking twice
    /// would be the kind of ceremony that turns a decision back into a chore.
    /// </remarks>
    public Creature? FeedTarget { get; set; }

    /// <summary>
    /// The Forms the player's current build runs. Set by the host so the Forge can tell you whether an
    /// item's Form-combo enchantment is LIVE for your build or dead weight — the whole loot philosophy in
    /// one line: not "is this a bigger number", but "does this fit what I'm building".
    /// </summary>
    public IReadOnlyCollection<Form> ActiveForms { get; set; } = Array.Empty<Form>();

    /// <summary>
    /// The Memory Dust auto-sell floor. Set by the host so a chest's rolled loot honours the same filter a
    /// boss drop does — filtered items are sold for gleam, not dumped in the bag. Null keeps everything.
    /// </summary>
    public Rarity? AutoSellFloor { get; set; }

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
        if (_revealTimer > 0f) _revealTimer -= (float)time.ElapsedGameTime.TotalSeconds;

        var view = View();

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
        for (var vis = 0; vis < Visible && _scroll + vis < view.Count; vis++)
            if (UiKit.ClickedIn(Card(vis), mouse, clicked)) { _cursor = _scroll + vis; _msg = ""; }

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

        var gained = 0;
        foreach (var it in junk)
        {
            var m = Forge.Dismantle(it, Tuning);
            hunter.AddMaterial(MaterialTiers.ForRarity(it.Rarity), m);
            gained += m;
            _inv.Remove(it);
            _merge.Remove(it.InstanceId);
        }

        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say($"SALVAGED {junk.Count} JUNK — +{gained} {MaterialTiers.Name(Material.Scrap)}.", Gold);
    }

    private void DoMerge(Hunter hunter)
    {
        var inputs = _inv.Where(i => _merge.Contains(i.InstanceId)).ToList();
        // Defence in depth: ToggleMerge already refuses to queue worn gear, but an item can be queued and
        // THEN equipped. Consuming it would delete equipped gear (and orphan its worn-slot pointer).
        if (inputs.Any(i => IsWorn(hunter, i))) { Say("TAKE WORN GEAR OFF BEFORE MERGING IT.", Ember); return; }
        var result = Forge.Merge(inputs, _rng, Tuning, LootTuning.Default);
        if (!result.Success) { Say(result.Rejection!, Ember); return; }

        foreach (var i in inputs) _inv.Remove(i);
        _merge.Clear();
        _inv.Add(result.Product!);
        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say($"MERGED INTO A {RarityNames[(int)result.Product!.Rarity]} {ItemNames[result.Product.BaseType]}.", Gold);
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

    /// <summary>Feed an item straight into the selected creature's evolution.</summary>
    private void Feed(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        if (IsWorn(hunter, item)) { Say("TAKE IT OFF BEFORE FEEDING IT.", Ember); return; }
        if (FeedTarget?.Evolution is not { } progress)
        {
            Say("SELECT A CREATURE IN THE WARREN [A] FIRST.", Ember);
            return;
        }

        var reason = Forge.CheckEligible(item);
        if (reason != IneligibleReason.Eligible) { Say(Forge.Explain(reason), Ember); return; }

        var m = Forge.Feed(item, Tuning);
        _inv.Remove(item); _merge.Remove(item.InstanceId);
        progress.FeedMaterial(m);
        _cursor = Math.Clamp(_cursor, 0, Math.Max(0, _inv.Count - 1));
        Say($"FED {m} TO {FeedTarget.Source.ToString().ToUpperInvariant()} {FeedTarget.Role.ToString().ToUpperInvariant()}.", Gold);
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
        if (hunter.MaterialOf(Material.Essence) < cost) { Say($"NEED {cost} ESSENCE TO REFORGE THE TRAIT.", Ember); return; }

        var result = Reforge.ReforgeTrait(item, _rng, ReforgeTuning.Default);
        if (!result.Success) { Say(result.Rejection!, Ember); return; }

        hunter.SpendMaterial(Material.Essence, result.Cost);
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
        if (hunter.MaterialOf(tier) < cost) { Say($"NEED {cost} {MaterialTiers.Name(tier)} TO REFORGE THE ENCHANTMENT.", Ember); return; }

        var result = Reforge.ReforgeEnchant(item, _rng, ReforgeTuning.Default);
        if (!result.Success) { Say(result.Rejection!, Ember); return; }

        hunter.SpendMaterial(tier, result.Cost);
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
        if (hunter.MaterialOf(Material.Scrap) < r.Scrap) { Say($"NEED {r.Scrap} SCRAP TO REFINE.", Ember); return; }
        if (hunter.Gleam < r.Gold) { Say($"NEED {r.Gold}g TO REFINE.", Ember); return; }

        hunter.SpendMaterial(Material.Scrap, r.Scrap);
        hunter.SpendGleam(r.Gold);
        ReplaceItem(item, r.Product);
        Say($"REFINED TO iL{r.Product.ItemLevel}  ({r.Scrap} SCRAP + {r.Gold}g).", Gold);
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
    private void OpenBestChest(Hunter hunter)
    {
        if (_chests.Count == 0) { Say("NO CHESTS YET — CLEAR A BOSS WAVE.", Slate); return; }

        var idx = 0;
        for (var i = 1; i < _chests.Count; i++)
            if (_chests[i].Rarity > _chests[idx].Rarity) idx = i;

        var chest = _chests[idx];
        _chests.RemoveAt(idx);
        var (mat, items) = LandChest(chest, hunter);

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
        var reward = Chests.Open(chest, _rng, LootTuning.Default);
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

    public void Draw(SpriteBatch b, Hunter hunter, Point mouse, bool clicked)
    {
        // ── Header — a gem-title on the left, the chest pile in the centre. Gleam + Materials are the
        //    shared currency pills top-right now (Game1.DrawCurrencyPills). ─────────────────────────────
        _ui.Title(b, "THE FORGE", "SELL · MERGE · REFORGE");

        // ── CHESTS — a toolbar row under the title (the currency pills own the top-right now). A boss's
        // drop lands here as an unopened chest; OPEN reveals the best (grade coloured), OPEN ALL clears it. ──
        if (_chests.Count > 0)
        {
            var best = _chests.Max(c => (int)c.Rarity);
            _ui.Text(b, $"{_chests.Count} CHEST{(_chests.Count == 1 ? "" : "S")}", 10, 25, RarityColors[best]);
            if (_ui.Button(b, new Rectangle(72, 22, 42, 15), "OPEN", mouse, clicked)) OpenBestChest(hunter);
            if (_ui.Button(b, new Rectangle(118, 22, 60, 15), "OPEN ALL", mouse, clicked)) OpenAllChests(hunter);
        }
        else
            _ui.Text(b, "NO CHESTS", 10, 25, Slate);

        // ── Inventory — a loot-card grid, not a text list ────────────────────────────────────────
        _ui.Panel(b, new Rectangle(8, 40, 250, 148));
        var view = View();
        _ui.Text(b, $"LOOT ({view.Count})", 14, 44, InkFaint);

        // FILTER cycle — narrows a haystack of ninety to one slot or rarity. TAB cycles too. Rides the
        // toolbar row, right of the chest controls.
        if (_ui.Button(b, new Rectangle(184, 22, 72, 15), FilterName(_filter), mouse, clicked))
            CycleFilter(1);
        if (view.Count > Visible)
            _ui.TextRight(b, $"{_scroll / Cols + 1}/{(view.Count + Cols - 1) / Cols}", 250, 44, InkFaint);

        if (_inv.Count == 0)
            _ui.Text(b, "EMPTY — GO HUNT SOMETHING.", 16, 66, Dim);
        else if (view.Count == 0)
            _ui.Text(b, $"NOTHING MATCHES {FilterName(_filter)} — TAB TO CHANGE.", 16, 66, Dim);

        for (var vis = 0; vis < Visible && _scroll + vis < view.Count; vis++)
        {
            var idx = _scroll + vis;
            var item = view[idx];
            var card = Card(vis);
            var active = idx == _cursor;
            var queued = _merge.Contains(item.InstanceId);
            var rarity = RarityColors[(int)item.Rarity];

            // Card body: a dark cell edged in the item's rarity colour.
            _ui.Fill(b, card, active ? new Color(0x2A, 0x24, 0x14) : new Color(0x16, 0x14, 0x1C));
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 1), rarity);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - 1, card.Width, 1), active ? Gold : rarity * 0.5f);

            // The item icon, big and centred.
            DrawItemIcon(b, item, new Rectangle(card.Center.X - 13, card.Y + 4, 26, 26));
            _ui.TextCenter(b, $"{item.SellValue}g", card.Center.X, card.Bottom - 10, Gold);

            if (queued) _ui.TextCenter(b, "*", card.Right - 6, card.Y + 2, Gold);   // in the merge tray

            // The upgrade-at-a-glance signal — the same green UP the Character bag shows, now on the loot
            // grid where the whole haul actually lives, so you no longer click every card to learn its worth.
            if (Gear.SlotFor(item.BaseType) is { } sl && !IsWorn(hunter, item)
                && Gear.ItemScore(item) > Gear.ItemScore(hunter.Worn(sl)))
                _ui.Text(b, "UP", card.X + 3, card.Y + 2, new Color(0x6E, 0xC8, 0x7A));

            if (active) Reticle(b, card, Bone);
        }

        // ── Action panel ───────────────────────────────────────────────────────────────────────
        _ui.Panel(b, new Rectangle(264, 22, 208, 138));
        var act = Active;
        if (act is null)
            _ui.Text(b, "PICK AN ITEM ON THE LEFT.", 272, 30, InkFaint);
        else
        {
            DrawItemIcon(b, act, new Rectangle(272, 30, 16, 16));

            // The ELEMENT is part of the item's name, because it is part of what the item IS — and it
            // is what the Source matchup and the Forge's carry-through rule both read. An element the
            // player cannot see is an element they cannot plan around.
            var element = act.Element is { } el ? $"{el.ToString().ToUpperInvariant()} " : "";
            var title = $"{RarityNames[(int)act.Rarity]} {element}{ItemNames[act.BaseType]}";
            _ui.Text(b, title, 292, 33, RarityInk[(int)act.Rarity]);

            // The trait rides the same row — but only if it FITS. Measured, not estimated: at 480x270
            // "LEGENDARY MACHINE WEAPON" and "WARDING" want the same pixels, and I have already shipped
            // two collisions this session by doing this arithmetic in my head. When it doesn't fit the
            // trait is dropped, not overlapped: the EQUIP button below still names its effects.
            if (GearTraits.TraitOf(act) is { } tr)
            {
                var traitName = GearTraits.NameOf(tr);
                var titleEnd = 292 + _ui.Measure(title);
                var traitStart = 464 - _ui.Measure(traitName);
                if (traitStart > titleEnd + 4) _ui.TextRight(b, traitName, 464, 33, InkGold);
            }

            // The ENCHANTMENT gets its own line: it is a sentence about WHEN, not a number, and it is
            // the only thing on this panel that changes what the fight does rather than by how much.
            if (Enchantments.Of(act) is { } ench)
            {
                // FIXED COLUMNS. Right-aligning the blurb was not enough: with a long name the two ends
                // met in the middle and rendered as one word ("SPLINTERON A KILL"). A column each means
                // neither can grow into the other, whatever the name.
                _ui.Text(b, ench.Name, 272, 141, Bloom);
                _ui.Text(b, ench.Blurb, 344, 141, Ink);

                // The loot philosophy, made visible: a Form-combo enchantment is LIVE only if your build
                // runs its Form. Gold "COMBOS YOUR BUILD" when it fits; a grey "NEEDS … IN YOUR BUILD"
                // when it is dead weight for you — so the choice is "does this fit my build", not "bigger".
                if (ench.NeedsForm is { } need)
                {
                    var live = ActiveForms.Contains(need);
                    _ui.Text(b, live ? "COMBOS YOUR BUILD" : $"NEEDS {need.ToString().ToUpperInvariant()} IN YOUR BUILD",
                        272, 152, live ? InkGold : InkFaint);
                }
            }

            var wearable = Gear.IsWearable(act);
            var worn = wearable && Gear.SlotFor(act.BaseType) is { } s0 && hunter.Worn(s0)?.InstanceId == act.InstanceId;

            // EQUIP is the headline action now — gear is the Hunter's power curve.
            if (wearable)
            {
                var eq = new Rectangle(272, 50, 192, 18);
                if (_ui.Button(b, eq, worn ? "TAKE OFF" : $"EQUIP   {GearBlurb(act)}", mouse, clicked))
                    ToggleEquip(hunter, act);
            }

            var row = wearable ? 72 : 50;
            var sell = new Rectangle(272, row, 92, 18);
            var dis = new Rectangle(372, row, 92, 18);
            var feed = new Rectangle(272, row + 22, 92, 18);
            var add = new Rectangle(372, row + 22, 92, 18);

            // A worn item must be taken off before it can be sold or scrapped.
            if (_ui.Button(b, sell, $"SELL  +{act.SellValue}g", mouse, clicked, enabled: !worn)) Sell(hunter, act);
            if (_ui.Button(b, dis, $"DISMANTLE +{Forge.Dismantle(act, Tuning)} MAT", mouse, clicked, enabled: !worn))
                Dismantle(hunter, act);

            // FEED — the third fate of unwanted loot, and the one that was never built. Efficient
            // (full rate) but COMMITTED: it all goes into one creature, right now. Dismantle is the
            // reverse trade — lossy, but liquid. Neither dominates, which is the whole point.
            var canFeed = !worn && FeedTarget?.Evolution is not null;
            if (_ui.Button(b, feed, canFeed ? $"FEED +{Forge.Feed(act, Tuning)}" : "FEED", mouse, clicked, enabled: canFeed))
                Feed(hunter, act);

            var queued = _merge.Contains(act.InstanceId);
            if (_ui.Button(b, add, queued ? "UNQUEUE" : "MERGE (3→1)", mouse, clicked, enabled: !worn))
                ToggleMerge(hunter, act);
        }

        // ── The selected item's rolled AFFIXES and its item LEVEL — the multi-stat bonuses that make
        //    one drop beat another of the same base. (Full worn gear lives on the CHARACTER screen now.) ──
        if (Active is { } sel)
        {
            var affixes = ItemAffixes.Of(sel);
            _ui.Text(b, $"iL{sel.ItemLevel}   {(affixes.Count > 0 ? "AFFIXES" : "NO AFFIXES")}", 272, 118, InkFaint);
            for (var i = 0; i < affixes.Count; i++)
                _ui.Text(b, ItemAffixes.Describe(affixes[i]), 272 + i % 2 * 98, 128 + i / 2 * 10, InkGold);
        }
        _ui.TextRight(b, $"POWER {hunter.PowerRating}", 464, 120, InkGold);

        // ── Merge tray ─────────────────────────────────────────────────────────────────────────
        _ui.Panel(b, new Rectangle(264, 164, 208, 46));
        _ui.Text(b, "MERGE TRAY", 272, 167, InkFaint);

        var picked = _inv.Where(i => _merge.Contains(i.InstanceId)).ToList();
        for (var i = 0; i < 3; i++)
        {
            var slot = new Rectangle(272 + i * 22, 178, 18, 18);
            _ui.Panel(b, slot);
            if (i < picked.Count) DrawItemIcon(b, picked[i], new Rectangle(slot.X + 2, slot.Y + 2, 14, 14));
        }

        var ready = MergeReady();
        var mergeBtn = new Rectangle(344, 176, 120, 15);

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
            _ui.TextRight(b, $"-> {attune}{ItemNames[type]}", 464, 167,
                isHybrid ? Bloom : RarityInk[(int)(picked[0].Rarity + 1)]);
        }
        else if (picked.Count == 3)
        {
            _ui.TextRight(b, "SAME RARITY ONLY", 464, 167, InkFaint);
        }

        if (_ui.Button(b, mergeBtn, "MERGE", mouse, clicked, enabled: ready)) DoMerge(hunter);

        // Fusing three-at-a-time by hand out of a bag of ninety is busywork. One button does the lot.
        var autoBtn = new Rectangle(344, 193, 120, 15);
        // The button is live only when a REAL trio exists: three distinct-id, non-worn wearables of one
        // sub-Legendary rarity. Counting raw items (materials, duplicate copies) lit it up when nothing
        // could actually merge — the "it won't merge though there's stuff to merge" the player hit.
        var anyTrio = _inv.Where(i => Gear.IsWearable(i) && !IsWorn(hunter, i))
                          .DistinctBy(i => i.InstanceId)
                          .GroupBy(i => i.Rarity).Any(g => g.Key != Rarity.Legendary && g.Count() >= 3);
        if (_ui.Button(b, autoBtn, "AUTO-MERGE ALL", mouse, clicked, enabled: anyTrio)) AutoMergeAll(hunter);

        // ── REFORGE — spend MATERIALS to re-roll the selected item's passives (RNG). This is what makes
        // an item CUSTOMISABLE: churn the trait toward the trade your build wants, or commit to re-rolling
        // the Form-combo enchant until it fits your Forms. Acts on the selected item, like SELL/DISMANTLE.
        // A worn item greys out here for the same reason it does there — take it off before reworking it.
        if (act is not null && Gear.IsWearable(act))
        {
            var isWorn = IsWorn(hunter, act);

            // TRAIT re-roll spends ESSENCE, ENCH re-roll spends CORE (or CRYSTAL on a Legendary), and
            // REFINE spends SCRAP + Gold to raise the level. Each tier drains through its own verb.
            var tCost = ReforgeTuning.Default.TraitCostFor(act.Rarity);
            var canTrait = !isWorn && hunter.MaterialOf(Material.Essence) >= tCost;
            if (_ui.Button(b, new Rectangle(264, 214, 66, 15), $"TRAIT {tCost}E", mouse, clicked, enabled: canTrait))
                DoReforgeTrait(hunter, act);

            var hasEnch = act.Rarity >= Enchantments.MinimumRarity;
            var eTier = EnchantReforgeTier(act.Rarity);
            var eCost = ReforgeTuning.Default.EnchantCostFor(act.Rarity);
            var canEnch = !isWorn && hasEnch && hunter.MaterialOf(eTier) >= eCost;
            var eLabel = hasEnch ? $"ENCH {eCost}{(eTier == Material.Crystal ? "X" : "C")}" : "ENCH --";
            if (_ui.Button(b, new Rectangle(334, 214, 66, 15), eLabel, mouse, clicked, enabled: canEnch))
                DoReforgeEnchant(hunter, act);

            // REFINE spends SCRAP + Gold for +1 level. Holding SHIFT turns it into GREATER REFINE — one
            // CRYSTAL (+ Gold) for +5 at once — but only when a Crystal is actually on hand, so the button
            // never silently changes verb on a player who has none. The footer teaches this when it applies.
            var refine = Forge.Refine(act, Tuning);
            var greater = Forge.GreaterRefine(act, Tuning);
            var hasCrystal = hunter.MaterialOf(Material.Crystal) >= greater.Crystal;
            var kb = Keyboard.GetState();
            var doGreat = (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift)) && hasCrystal;
            var canRefine = !isWorn && (doGreat
                ? hunter.Gleam >= greater.Gold
                : hunter.MaterialOf(Material.Scrap) >= refine.Scrap && hunter.Gleam >= refine.Gold);
            if (_ui.Button(b, new Rectangle(404, 214, 66, 15), doGreat ? "REFINE +5" : "REFINE +1", mouse, clicked, enabled: canRefine))
            {
                if (doGreat) DoGreaterRefine(hunter, act); else DoRefine(hunter, act);
            }
        }

        // ── Message + footer ── in the strip under the loot panel (the nav owns the very bottom now) ──
        if (_msg.Length > 0) _ui.Text(b, _msg, 12, 194, _msgColor);
        // The footer teaches Shift+Refine exactly when it becomes usable — the moment a Crystal is on hand.
        // A hint for an action you cannot take yet is noise; one that appears the instant you can is a tutor.
        // BACK is dropped — the nav bar teaches it now.
        _ui.Text(b, hunter.MaterialOf(Material.Crystal) >= 1
            ? "SHIFT+REFINE = +5 FOR A CRYSTAL   ·   J  SALVAGE JUNK"
            : "CLICK AN ITEM, THEN A BUTTON   ·   J  SALVAGE JUNK", 12, 208, Slate);

        DrawReveal(b);   // the chest-open burst, on top of everything
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

        var fade = Math.Clamp(_revealTimer / 0.45f, 0f, 1f);   // fade out over the last ~0.45s
        var grade = RarityColors[(int)_revealGrade];

        // Dim the Forge behind, so the eye goes to the burst.
        _ui.Fill(b, new Rectangle(0, 0, 480, 270), new Color(0, 0, 0, (int)(150 * fade)));

        // A dark reward card edged in the chest's grade colour.
        var card = new Rectangle(150, 84, 180, 104);
        _ui.Fill(b, card, new Color(0x16, 0x14, 0x1C) * fade);
        _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 2), grade * fade);
        _ui.Fill(b, new Rectangle(card.X, card.Bottom - 2, card.Width, 2), grade * fade);

        _ui.TextCenter(b, $"{RarityNames[(int)_revealGrade]} CHEST", 240, 92, grade * fade);

        // The items that popped, big and framed by their own rarity.
        var n = _revealItems.Count;
        for (var i = 0; i < n; i++)
            DrawItemIcon(b, _revealItems[i], new Rectangle(240 - n * 17 + i * 34, 108, 30, 30));

        var names = n == 0
            ? "SOLD ON SIGHT (FILTER)"
            : string.Join("   ", _revealItems.Select(it => $"{RarityNames[(int)it.Rarity]} {ItemNames[it.BaseType]}"));
        var nameColor = n > 0 ? RarityColors[_revealItems.Max(it => (int)it.Rarity)] : Slate;
        _ui.TextCenter(b, names, 240, 146, nameColor * fade);
        _ui.TextCenter(b, $"+{_revealMaterials} MATERIALS", 240, 160, Gold * fade);
    }

    private void Reticle(SpriteBatch b, Rectangle r, Color c)
    {
        const int len = 5, t = 1;
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
            var pad = frame is null ? 0 : Math.Max(1, box.Width * 15 / 100);
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
            ? _ui.Assets.Get($"item_{slot.ToString().ToLowerInvariant()}_{trait.ToString().ToLowerInvariant()}")
            : null;

    /// <summary>The item's Source gem, top-left (package_05 source_&lt;element&gt;). Skipped on tiny boxes.</summary>
    private void DrawSourceGem(SpriteBatch b, ItemInstance item, Rectangle box)
    {
        if (box.Width < 20 || item.Element is not { } el) return;
        if (_ui.Assets.Get($"source_{el.ToString().ToLowerInvariant()}") is not { } sg) return;
        var s = box.Width * 2 / 5;
        b.Draw(sg, new Rectangle(box.X, box.Y, s, s), Color.White);
    }

    /// <summary>A small corner glyph for the item's enchantment (Rare+ only). Skipped on tiny boxes.</summary>
    private void DrawEnchantAccent(SpriteBatch b, ItemInstance item, Rectangle box)
    {
        if (box.Width < 22 || Enchantments.Of(item) is not { } ench) return;
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
