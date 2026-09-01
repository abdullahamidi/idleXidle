using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;

using IdleXIdle.Core.Progression;
namespace IdleXIdle.Game;

/// <summary>
/// The Forge — click an item in the bag, then pick a tab: UPGRADE, RE-ROLL, SOCKET or SALVAGE.
/// </summary>
/// <remarks>
/// Point-and-click and panel-driven: an item is picked by clicking it, each tab carries ONE big
/// button with its price printed under it, and the wallet on the right shows what you hold with the
/// real material icons. Keyboard still works as a fallback (arrows, S, D, J).
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
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;

    // The reference art pack's panels are DARK glass, not parchment — so text on them is LIGHT, the same
    // as on the scene. The old dark "ink" ramp was invisible on the dark loot/item/merge panels.
    private static readonly Color Ink = Bone;                        // resting text — light on dark glass
    private static readonly Color InkFaint = UiInk.Secondary;  // labels, units — dim light
    private static readonly Color InkGold = Gold;                    // "earned" — gold reads on dark glass
    private static readonly Color InkEmber = Ember;                  // danger — bright ember on dark

    private static readonly string[] RarityNames = ["COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY"];

    /// <summary>Rarity on the DARK item cards and frames. Light.</summary>
    private static readonly Color[] RarityColors =
        [Bone, new Color(0x6E, 0xC8, 0x7A), new Color(0x4A, 0x90, 0xD9), new Color(0x8B, 0x3F, 0x82), Gold];

    /// <summary>Rarity for the item NAME. The panels are dark glass now, so it is just the bright ramp —
    /// the old dark-ink version was there for a parchment panel that no longer exists.</summary>

    private static readonly Dictionary<ItemBaseType, string> ItemNames = new()
    {
        // No CreatureCore and no Material row: neither type can reach a bag (Bag() filters to
        // Gear.IsWearable and MergeRecipe cannot produce them), and "CORE" as an item word collided
        // with CORE the material.
        [ItemBaseType.Weapon] = "WEAPON", [ItemBaseType.Charm] = "CHARM", [ItemBaseType.AbilityFocus] = "FOCUS",
        [ItemBaseType.Helm] = "HELM", [ItemBaseType.Chest] = "CHESTPLATE", [ItemBaseType.Gloves] = "GLOVES",
        [ItemBaseType.Boots] = "BOOTS", [ItemBaseType.Ring] = "RING", [ItemBaseType.Gem] = "GEM",
    };
    // package_05 rarity frames (also present in package_01 under the same names).
    private static readonly string[] FrameKey =
        ["ui_frame_rarity_common", "ui_frame_rarity_uncommon", "ui_frame_rarity_rare", "ui_frame_rarity_epic", "ui_frame_rarity_legendary"];

    /// <summary>
    /// The item's face — an id-stable pick from its slot's art set, independent of everything mutable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The art files are named <c>item_&lt;slot&gt;_&lt;trait&gt;.png</c> from the era when the trait
    /// WAS the identity</b> — the true root of the playtest bug "trait'i re-rollüyorum ama silahın tipi
    /// de değişiyor": re-rolling swapped the whole picture. The picture is picked by an id-derived
    /// index now — pure LOOK, no meaning — so an item keeps one face for life whatever its prefix (or
    /// none) says. The scan walks forward from the seed so a slot set missing a file still lands on a
    /// neighbour deterministically.
    /// </para>
    /// <para>
    /// (The old ItemThumb fallback — weapon_blade_01 / accessory_01 thumbnails — referenced art that
    /// has never existed on disk: a dormant path from birth. Deleted, not kept. Matching BOW/SPEAR
    /// family ART does not exist yet either — logged in production/qa/art-consistency-audit.md.)
    /// </para>
    /// </remarks>
    private Texture2D? ItemArt(ItemInstance item)
    {
        if (Gear.SlotFor(item.BaseType) is not { } slot) return null;
        var pool = GearTraits.PoolFor(slot);
        var start = (int)(ItemNaming.ArtSeed(item) % (uint)pool.Length);
        var slotWord = slot.ToString().ToLowerInvariant();
        // A WEAPON'S FACE IS ITS FAMILY (2026-08-23). The name says BLADE / BOW / SPEAR / SCYTHE and the
        // stat obeys it (ItemFamilies), but every weapon picture on disk was a sword, so a "SHADOW BOW"
        // rendered as a sword — logged in the art audit since the item-system redesign. The bow, spear
        // and scythe sets are item_weapon_<family>_<trait>; the blade keeps the original item_weapon_<trait>
        // files, which is why the family prefix is tried first and the plain key stays as the fallback.
        var family = item.BaseType == ItemBaseType.Weapon
            ? ItemNaming.WeaponFamilies[ItemNaming.WeaponFamilyIndex(item)]
            : null;
        for (var k = 0; k < pool.Length; k++)
        {
            var pick = pool[(start + k) % pool.Length].ToString().ToLowerInvariant();
            if (family is not null && family != "blade"
                && _ui.Assets.Get($"item_{slotWord}_{family}_{pick}") is { } famTex)
                return famTex;
            if (_ui.Assets.Get($"item_{slotWord}_{pick}") is { } tex)
                return tex;
        }
        return null;
    }

    private readonly UiKit _ui;
    private readonly Random _rng = new();

    private readonly List<ItemInstance> _inv = new();
    private readonly List<Chest> _chests = new();    // unopened chests, waiting for the click
    /// <summary>The item under the pointer this frame. Re-established every draw; see GearScreen.</summary>
    private ItemInstance? _hovered;

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
    /// <summary>The headline over the reveal — "RARE CHEST", or a gift's own title (ChestDossier.Title).</summary>
    private string _revealTitle = "";
    private readonly List<ItemInstance> _revealItems = new();
    // 3.8s, and it is a SEQUENCE now rather than a card that appears. See DrawReveal for the beats.
    // It was 2.9 until the reveal grew per-item SELL / SALVAGE buttons: those land about a second in
    // and the fade starts 0.45s before the end, which left barely a second to reach one. The pointer
    // freeze makes a long hold free -- the card stops the moment you touch it, and a click still skips.
    private const float RevealHold = 3.8f;

    // The beats, in seconds from the moment the chest cracks. Named because the arithmetic below reads
    // as nonsense otherwise, and because a designer retiming this should not have to count decimals.
    private const float ShakeEnds = 0.55f;    // the chest rattles, harder and harder
    private const float BurstEnds = 0.78f;    // the ring goes out, the chest is gone
    private const float CardIn = 0.20f;       // how long the card takes to spring open
    private const float ItemStagger = 0.13f;  // one item lands, then the next

    // ── Bulk OPEN ALL: a cascade of brief per-chest reveals (worst grade first, best last), then a
    //    summary that holds until a click. Research pass (Hearthstone mass opening, Genshin 10-pull,
    //    AFK Arena): a skip must jump to the END, never to the next item, and the summary is mandatory
    //    — skipping the show must never skip the review of the haul. ──
    private readonly Queue<(Rarity Grade, int Materials, List<ItemInstance> Items)> _revealQueue = new();
    private readonly List<ItemInstance> _revealAll = new();   // everything the bulk open landed
    private int _revealAllMats;
    private int _revealChestCount;     // chests this reveal covers (1 = the classic single ceremony)
    private int _revealIndex;          // 1-based position of the cascade's current chest
    private bool _revealBrief;         // compressed beats for cascade entries
    private bool _revealSummary;       // the closing card is up, pinned until a click
    private float _revealHold = RevealHold;   // total hold of the CURRENT beat (fade math reads this)
    private int _revealMergedCount;           // TIRELESS FORGE fusions during this bulk open, for the summary
    private const float BriefHold = 1.05f;    // per-chest hold inside a cascade
    private KeyboardState _prevKeys;
    private int _bagScroll;                          // first visible row of the left-column bag list

    public ForgeScreen(UiKit ui) => _ui = ui;

    /// <summary>The shared audio bank — set by the host, like AskBeforeScrap. Null runs silent.</summary>
    /// <remarks>Every cue rides the bank's master effects volume; nothing here touches a raw SoundEffect.</remarks>
    public SoundBank? Sound { get; set; }

    /// <summary>The item the workbench acts on — resolved by id so a re-forge that
    /// replaces the object keeps the selection. Defaults to the first wearable in the bag.</summary>
    private string? _focusId;
    /// <summary>Set when the focus was CHOSEN, so the list scrolls to it once and then leaves it alone.</summary>
    private bool _focusFollow;

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
    // THE MODE RAIL IS GONE. Playtest: "o butonları kaldır" — three buttons in their own framed column,
    // whose selected state was a coloured slab, standing between the player and a screen that never
    // needed to be three screens. UPGRADE and SALVAGE are actions on the item now, and REFORGE is a
    // column, so nothing is left to switch between.

    // ── THE WORKBENCH (UX V2 P1.9) ────────────────────────────────────────────────────────────────
    //
    // Four near-equal ornate columns became THREE plus a strip. The fourth — a 514 px WALLET carrying
    // five balances, a YOUR CHARTS essay and a THE FOUR TABS legend — was a quarter of the screen
    // spent explaining the screen, and the width it held is exactly what the before → after compare
    // needed. The balances are a compact strip along the top now; the legend is the tour's job.
    //
    // Every rectangle is a PROPERTY, not a `static readonly`. A frozen rectangle cannot follow
    // UiKit.Page, and the page is what UI SCALE rewrites — this screen was the last big one still
    // authored against raw 1920x1080 literals.
    private const int Top = 150;            // the hint slot owns canvas y 86-134; 150 is the first clear row
    private const int StripH = 72;
    private const int Gutter = 22;
    private const int ColTop = Top + StripH + Gutter;
    private const int SideMargin = 38;
    private const int BottomMargin = 40;

    /// <summary>Every balance you hold, along the top — the QUIET tier, shared by all three columns.</summary>
    private static Rectangle MaterialStrip =>
        new(SideMargin, Top, UiKit.PageRight(BottomMargin) - SideMargin, StripH);

    private static int ColBottom => UiKit.PageBottom(BottomMargin);
    private static int ColSpan => UiKit.PageRight(BottomMargin) - SideMargin - Gutter * 2;

    /// <summary>WHAT YOU HAVE. The bag, its filter, and the two bulk verbs.</summary>
    private static Rectangle BagPanel => new(SideMargin, ColTop, ColSpan * 25 / 100, ColBottom - ColTop);

    /// <summary>WHAT IT IS. The identity of the piece on the bench — SECONDARY, never the subject.</summary>
    private static Rectangle ItemPanel => new(BagPanel.Right + Gutter, ColTop, ColSpan * 30 / 100, ColBottom - ColTop);

    /// <summary>
    /// WHAT YOU CAN DO TO IT — the screen's one PRIMARY surface.
    /// </summary>
    /// <remarks>
    /// The ornate frame was on the ITEM column, which is the one column that asks for no decision. The
    /// gold goes where the choice is. Its aspect is about 1.0 at every scale, so it wears the SQUARE
    /// frame — correct, and safe since UiKit.FrameDrop/PadX started clearing that art's finial and its
    /// side diamonds automatically.
    /// </remarks>
    private static Rectangle ForgePanel =>
        new(ItemPanel.Right + Gutter, ColTop,
            UiKit.PageRight(BottomMargin) - (ItemPanel.Right + Gutter), ColBottom - ColTop);

    /// <summary>
    /// The right end of the strip, kept for the charts — a chip lane, not a column.
    /// </summary>
    /// <remarks>
    /// Wide enough for all three charts at once (measured, with their counts), because a lane that is
    /// one chip too narrow does not wrap — it draws the third chip straight through the CRYSTAL
    /// balance. Anything that still will not fit is not drawn at all.
    /// </remarks>
    private const int ChartsLane = 560;

    private static int StripChipW => (MaterialStrip.Width - 24 - ChartsLane) / 5;

    private static Rectangle MatChip(int i) =>
        new(MaterialStrip.X + 12 + i * StripChipW, MaterialStrip.Y + 8, StripChipW - 12, 56);

    private const int BagRowH = 40;

    private static int BagContentW => UiKit.ContentRight(BagPanel) - UiKit.ContentLeft(BagPanel) - 34;

    /// <summary>The bag's two bulk verbs, anchored to the panel's own foot.</summary>
    private static Rectangle SalvageJunkBtn =>
        new(UiKit.ContentLeft(BagPanel), UiKit.ContentBottom(BagPanel) - 48, BagContentW, 48);

    private static Rectangle MergeBtn =>
        new(UiKit.ContentLeft(BagPanel), SalvageJunkBtn.Y - 12 - 48, BagContentW, 48);

    /// <summary>Where the merge preview line sits — and the floor the list stops at.</summary>
    private static int MergePreviewY => MergeBtn.Y - 26;

    /// <summary>
    /// How many rows fit between the header and the foot.
    /// </summary>
    /// <remarks>
    /// Derived from the foot's real position rather than from a hand-kept reserve constant. The reserve
    /// was 124 when the furniture needed 176, so the list claimed a row it did not have and drew it
    /// underneath a button — a row that was visible, clickable, and reporting another item's name.
    /// </remarks>
    private static int BagRows => (MergePreviewY - 12 - UiKit.BodyTop(BagPanel)) / BagRowH;

    // The panel's own margin: rows drawn flush to the rectangle sit ON the frame art's ornament.
    // The extra 34 on the right is the scroll track's lane, not a margin.
    private static Rectangle BagRow(int vis)
        => new(UiKit.ContentLeft(BagPanel), UiKit.BodyTop(BagPanel) + vis * BagRowH, BagContentW, BagRowH - 4);

    /// <summary>What the bag is showing. Session state — a filter that outlived a session would be a
    /// bag that looks empty for a reason the player cannot see.</summary>
    private enum BagFilter { All, Gear, Gems }

    private BagFilter _filter = BagFilter.All;

    /// <summary>The three filter chips, under the bag's title.</summary>
    private static Rectangle FilterChip(int i)
    {
        var w = (BagContentW + 34 - 16) / 3;
        return new Rectangle(UiKit.ContentLeft(BagPanel) + i * (w + 8), UiKit.CaptionTop(BagPanel), w, 34);
    }

    // ── THE FORGE COLUMN'S SKELETON. Every tab fills the same shape, so switching tabs moves nothing
    //    but the words: tabs · intent · what will change · what is uncertain · ONE button · price ·
    //    the second option, if the tab has one · feedback. ──────────────────────────────────────────
    /// <summary>
    /// The extra inset this column pays for wearing the SQUARE frame.
    /// </summary>
    /// <remarks>
    /// UiKit.PadX already clears that art's 64 px side diamond — but the diamond is at the panel's
    /// vertical CENTRE, which is exactly where this column's primary button lands, and an ornate
    /// button's own end scroll met the panel's ornament there. Measured from the capture, not guessed.
    /// </remarks>
    private const int FrameInset = 30;

    private static int FX => UiKit.ContentLeft(ForgePanel) + FrameInset;
    private static int FW => UiKit.ContentRight(ForgePanel) - FrameInset - FX;

    /// <summary>
    /// A short column is a REAL state, not an edge case: at UI SCALE 125 % this panel is 580 px tall
    /// instead of 796.
    /// </summary>
    /// <remarks>
    /// The stack under the compare — button, price, second verb, feedback — was fixed at its 1080
    /// sizes, so at 125 % it consumed the whole column and <see cref="TabBody"/> came out NEGATIVE:
    /// the before → after table, the entire point of the column, silently drew nothing. Every band
    /// steps down together, and what the table cannot fit it drops a row at a time from the bottom.
    /// </remarks>
    private static bool Tight => ForgePanel.Height < 700;

    private static int TabStripH => Tight ? 44 : 52;
    private static int PrimaryH => Tight ? 64 : 84;
    private static int OptionH => Tight ? 44 : 52;
    private static int FeedbackH => Tight ? 56 : 72;

    /// <summary>One tab of the strip — the same rectangle the strip draws and hit-tests.</summary>
    private static Rectangle TabRect(int i) =>
        new(FX + i * (FW / 4), UiKit.TitleTop(ForgePanel), FW / 4 - 6, TabStripH);

    private static int IntentY => TabRect(0).Bottom + (Tight ? 12 : 22);
    private static int BodyTopY => IntentY + UiTypography.Pitch(UiTypography.Body) + (Tight ? 8 : 14);

    /// <summary>The one-line verdict of the last act, directly under the button that caused it.</summary>
    private static Rectangle Feedback =>
        new(FX, UiKit.ContentBottom(ForgePanel) - FeedbackH, FW, FeedbackH);

    /// <summary>The second, quieter verb — GREATER UPGRADE, SELL. Only two tabs have one.</summary>
    private static Rectangle OptionBtn =>
        new(FX, Feedback.Y - 12 - UiTypography.Pitch(UiTypography.Secondary) - OptionH, FW, OptionH);

    private static int OptionPriceY => OptionBtn.Bottom + 4;

    private static Rectangle PrimaryBtn(bool hasOption) =>
        new(FX, (hasOption ? OptionBtn.Y : Feedback.Y) - 14
                - UiTypography.Pitch(UiTypography.Body) - UiTypography.Pitch(UiTypography.Secondary)
                - PrimaryH,
            FW, PrimaryH);

    private static int PriceY(bool hasOption) => PrimaryBtn(hasOption).Bottom + 12;

    /// <summary>
    /// Where the "what is uncertain" block starts — the last thing read before the commit.
    /// </summary>
    /// <remarks>
    /// A tab with no second verb has room for TWO lines here and uses it (RE-ROLL says what is random
    /// AND what is kept; SOCKET says nothing is random AND that a set gem never comes out), so its
    /// start is pulled up by one line. Anchored one line too low, the second sentence drew straight
    /// through the primary button.
    /// </remarks>
    private static int UncertainY(bool hasOption) =>
        PrimaryBtn(hasOption).Y - 28 - (hasOption ? 0 : UiTypography.Pitch(UiTypography.Body));

    private static Rectangle TabBody(bool hasOption) =>
        new(FX, BodyTopY, FW, UncertainY(hasOption) - 12 - BodyTopY);

    /// <summary>The four things you can do to an item — one tab each, one big button each.</summary>
    private enum Tab { Upgrade, Reroll, Socket, BreakDown }
    // The fourth tab was BREAK DOWN while the chest reveal's per-item button said SALVAGE for the same
    // act (playtest 2026-08-25: "it creates an inconsistency") — one word now, the reveal's.
    private static readonly string[] TabNames = ["UPGRADE", "RE-ROLL", "SOCKET", "SALVAGE"];
    private Tab _tab = Tab.Upgrade;

    /// <summary>The gem a click in the bag SELECTED for the socket tab — chosen, not yet committed.</summary>
    private string? _gemId;

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.Bag => new[] { BagPanel },
        TourTarget.ForgeItem => new[] { ItemPanel },
        TourTarget.ForgeTabs => new[] { Grow(new Rectangle(TabRect(0).X, TabRect(0).Y, FW, TabStripH), 6) },
        TourTarget.Materials => new[] { MaterialStrip },
        // The first-gem lesson's last card: the SOCKET tab alone, grown so the ring clears its text.
        TourTarget.SocketTab => new[] { Grow(TabRect((int)Tab.Socket), 6) },
        _ => Array.Empty<Rectangle>(),
    };

    private static Rectangle Grow(Rectangle r, int by) => new(r.X - by, r.Y - by, r.Width + 2 * by, r.Height + 2 * by);

    /// <summary>
    /// Has this player set a gem before? Their FIRST is free (<see cref="GemCraft.SocketCost(Rarity, bool, SocketTuning?)"/>);
    /// this is the memory of having spent it. Host-restored from the save, host-saved from here.
    /// </summary>
    public bool FreeSocketUsed { get; set; }

    /// <summary>What THIS player pays to set a gem into a host of this grade — nothing for their first.</summary>
    private int SocketPrice(Rarity host) => GemCraft.SocketCost(host, FreeSocketUsed);

    /// <summary>Open the SOCKET tab — the first-gem lesson arrives with it open, so the bag lists the gems.</summary>
    public void RequestSocketTab() { _tab = Tab.Socket; _confirm = null; _socketAsk = null; }

    private static readonly Color Met = UiInk.Good;
    // Scrap / Essence / Core / Crystal. The REAL icons — assets/art/ItemsLoot/loot/materials — indexed
    // by (int)Material. The tint is the fallback gem if a file ever goes missing, one colour per tier.
    private static readonly string[] MatIcon = ["mat_scrap", "mat_essence", "mat_core", "mat_crystal"];
    private static readonly Color[] MatColor =
        { new(0x9A, 0xC0, 0x88), new(0x74, 0xC6, 0xE8), new(0xC0, 0x6E, 0xE0), new(0xF0, 0xC0, 0x48) };

    /// <summary>
    /// The bag, in one order, for everything that reads it.
    /// </summary>
    /// <remarks>
    /// Three places built this list independently — the drawn rows, <see cref="Target"/> and
    /// <see cref="CycleTarget"/> — so an ordering change in one silently disagreed with the other two,
    /// and "the row I clicked" and "the item the preview shows" were only the same thing by luck.
    ///
    /// SORTED RAREST FIRST, then by level, which is what the GEAR screen already does with the identical
    /// item set. In insertion order the bag was DROP order: a Legendary found forty items ago sits below
    /// forty Commons, and with six visible rows and no filter, finding it is scrolling past everything.
    /// </remarks>
    private List<ItemInstance> Bag()
        => _inv.Where(Gear.IsWearable)
               .OrderByDescending(i => (int)i.Rarity)
               .ThenByDescending(i => i.ItemLevel)
               .ToList();

    /// <summary>
    /// The loose gems, best first. A gem IS an inventory item — <see cref="Bag"/> simply filters them out.
    /// </summary>
    /// <remarks>
    /// Playtest: "gems are items too, so they should be in the inventory; pressing the SOCKET tab should
    /// not reveal something out of nowhere." It was literally true — the loose gems existed only as an
    /// icon strip inside the SOCKET tab, so until you opened that one tab the game never showed you that
    /// you owned any. They live in the same list as everything else now; the LEFT PANEL just shows that
    /// half of it while SOCKET is the open tab.
    /// </remarks>
    private List<ItemInstance> Gems()
        => _inv.Where(GemCraft.IsGem)
               .OrderByDescending(g => g.ItemLevel)
               .ThenByDescending(g => (int)g.Rarity)
               .ToList();

    /// <summary>
    /// What the left panel lists right now: the GEMS under the SOCKET tab, the wearables everywhere else.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately NOT a change to <see cref="Bag"/>.</b> <see cref="Target"/> and
    /// <see cref="CycleTarget"/> both mean "the wearable on the bench", and a gem can never be that —
    /// letting one into that list would point the UPGRADE and SALVAGE tabs at a stone.
    /// </remarks>
    private List<ItemInstance> BagList() => _filter switch
    {
        BagFilter.Gems => Gems(),
        BagFilter.Gear => Bag(),
        _ => Bag().Concat(Gems()).ToList(),
    };

    /// <summary>The wearable the focused modes upgrade — the id-matched item, or the first wearable.</summary>
    private ItemInstance? Target()
    {
        var wear = Bag();
        if (wear.Count == 0) return null;
        return (_focusId is not null ? wear.FirstOrDefault(i => i.InstanceId == _focusId) : null) ?? wear[0];
    }

    /// <summary>Step the focused selection to the next / previous wearable in the bag.</summary>
    private void CycleTarget(int dir)
    {
        var wear = Bag();
        if (wear.Count == 0) { _focusId = null; return; }
        var idx = Math.Max(0, wear.FindIndex(i => i.InstanceId == _focusId));
        _focusId = wear[(idx + dir % wear.Count + wear.Count) % wear.Count].InstanceId;
        _focusFollow = true;
    }

    /// <summary>DEV ONLY: point the workbench at a specific bag item, for the screenshot fixture.</summary>
    public void DevFocus(string instanceId) { _focusId = instanceId; _focusFollow = true; }

    /// <summary>
    /// DEV ONLY: pose a tab, a question or a chart stock for the screenshot rig.
    /// </summary>
    /// <remarks>
    /// Read from the environment the capture script sets — <c>RH_SHOT_TAB</c> (upgrade / reroll /
    /// socket / breakdown), <c>RH_SHOT_ASK</c> (sell / salvage / junk / gems / crush / haul, comma-separated)
    /// and <c>RH_SHOT_CHARTS</c> (a count of every chart) — and only while <c>RH_SHOT</c> itself is
    /// set, so a normal run never reads any of it. The tabs and the in-place questions have no other
    /// way to be captured: the host's fixtures pick the screen and the item, not the tab.
    /// </remarks>
    private bool _devPosePending = Environment.GetEnvironmentVariable("RH_SHOT") is not null;

    private void ApplyDevPose(Hunter hunter)
    {
        _devPosePending = false;
        if (Environment.GetEnvironmentVariable("RH_SHOT_CHARTS") is { } cs && int.TryParse(cs, out var n) && n > 0)
        {
            hunter.AddCharter(Charter.Reforge, n);
            hunter.AddCharter(Charter.Refine, n);
            hunter.AddCharter(Charter.Salvage, n);
        }
        // WHICH ITEM is on the bench, and WHAT the bag is filtered to — two dials instead of a new
        // capture mode per state.
        if (Environment.GetEnvironmentVariable("RH_SHOT_ITEM") is { Length: > 0 } id) DevFocus(id);
        if (Environment.GetEnvironmentVariable("RH_SHOT_FILTER") is { } f)
            _filter = f.ToLowerInvariant() switch
            {
                "gems" => BagFilter.Gems,
                "gear" => BagFilter.Gear,
                _ => BagFilter.All,
            };

        if (Environment.GetEnvironmentVariable("RH_SHOT_TAB") is { } t)
            _tab = t.ToLowerInvariant() switch
            {
                "reroll" => Tab.Reroll, "socket" => Tab.Socket, "breakdown" => Tab.BreakDown, _ => Tab.Upgrade,
            };
        if (Environment.GetEnvironmentVariable("RH_SHOT_ASK") is not { } asks || Target() is not { } item) return;
        foreach (var ask in asks.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (ask)
            {
                case "sell": OpenConfirm(ScrapKind.Sell, item.InstanceId); break;
                case "salvage": OpenConfirm(ScrapKind.Salvage, item.InstanceId); break;
                case "junk": OpenConfirm(ScrapKind.JunkAll, null); break;
                case "gems":
                    // Three loose gems, and one set into the item (no Essence asked — it is a pose).
                    for (var i = 0; i < 3; i++) _inv.Add(GemCraft.MintGem(4 + i, _rng));
                    if (GemCraft.SocketCount(item.Rarity) > 0 && GemCraft.Socket(item, GemCraft.MintGem(6, _rng)).Product is { } set)
                    {
                        ReplaceItem(hunter, item, set);
                        item = set;
                    }
                    break;
                case "crush" when item.Gems.Count > 0: RequestCrush(item, 0); break;
                case "revealsell" when _revealItems.Count > 0:
                    // Pose the reveal's OWN sell question. It is two clicks deep behind an animation
                    // that has to be timed, so a capture cannot reach it by waiting either.
                    _revealAsk = (_revealItems[0].InstanceId, ScrapKind.Sell);
                    _revealAskSuppress = false;
                    break;
                case "revealsold" when _revealItems.Count > 0:
                    // Pose the AFTERMATH: one drop already sold from the reveal, so the capture can
                    // check that its cell dims and stamps instead of offering to sell it twice.
                    DoRevealScrap(hunter, _revealItems[0], ScrapKind.Sell);
                    break;
                case "haul":
                    // Open the whole pile and jump to the SUMMARY. It is the cascade's review card —
                    // eight beats and a click away — and a capture renders sixty frames, so waiting for
                    // it is not an option. Nothing here shortcuts the LOGIC: the chests really open and
                    // the loot really lands; only the eight beats in front of the review are skipped.
                    OpenEveryChest(hunter);
                    _revealQueue.Clear();
                    EnterRevealSummary();
                    break;
                case "say": Say("RE-ROLLED — THE ENCHANT IS NOW FERVOUR  (A REFORGE CHART PAID FOR IT).", Gold); break;
            }
        }
    }

    /// <summary>
    /// Arrive here from somewhere else already pointed at an item, and say so.
    /// </summary>
    /// <remarks>
    /// The gear screen's item menu routes through this. It sets the focus AND flashes the item's name,
    /// because a screen that changes under you without saying why is indistinguishable from a misclick —
    /// the player needs to see that the thing they right-clicked is the thing now in the preview.
    /// </remarks>
    public void FocusFor(string instanceId)
    {
        _focusId = instanceId;
        _focusFollow = true;
        _bagScroll = 0;

        var it = _inv.FirstOrDefault(i => i.InstanceId == instanceId);
        if (it is not null) Say($"{ItemNaming.FullName(it)} — READY.", Met);
    }

    /// <summary>The GEAR screen's SALVAGE verb: arrive focused, with the question already open.</summary>
    /// <summary>GEAR's UPGRADE verb: focus the item AND open the UPGRADE tab — the tab is a persistent field.</summary>
    public void RequestUpgrade(string instanceId) { FocusFor(instanceId); _tab = Tab.Upgrade; _confirm = null; }

    /// <summary>GEAR's REFORGE verb: focus the item AND open the RE-ROLL tab, so its button and price line are on screen.</summary>
    public void RequestReroll(string instanceId) { FocusFor(instanceId); _tab = Tab.Reroll; _confirm = null; }

    public void RequestSalvage(string instanceId)
    {
        if (_inv.All(i => i.InstanceId != instanceId)) return;
        // The question is asked IN the SALVAGE tab of the focused item, so the item must be the
        // focus and the tab must be open — arriving with the question open anywhere else would be
        // a question the player cannot see.
        _focusId = instanceId;
        _focusFollow = true;
        _bagScroll = 0;
        OpenConfirm(ScrapKind.Salvage, instanceId);
    }

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
    /// The RESOLVED defs of the build the fight runs (P11 — variations included, so a variation
    /// that opens an amplify window lights LINGER). Set by the host so the Forge can tell you
    /// whether an item's combo enchantment is LIVE for your build or dead weight — the loot
    /// philosophy in one line: not "is this a bigger number", but "does this fit my build".
    /// </summary>
    public IReadOnlyCollection<SkillDef> ActiveDefs { get; set; } = Array.Empty<SkillDef>();

    /// <summary>
    /// The triggers the worn build carries, so a KEYSTONE combo can say whether it is live.
    /// </summary>
    /// <remarks>
    /// Set by the host beside <see cref="ActiveDefs"/>. An empty set is not "no keystones" so much as
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
        => Enchantments.Of(item)?.Needs?.MetBy(ActiveDefs, ActiveTriggers, SwornVows) == true;

    /// <summary>
    /// The Memory Dust auto-sell floor. Set by the host so a chest's rolled loot honours the same filter a
    /// boss drop does — filtered items are sold for gleam, not dumped in the bag. Null keeps everything.
    /// </summary>
    public Rarity? AutoSellFloor { get; set; }

    // ── The SELL / SALVAGE confirmation ──────────────────────────────────────────────────────────
    // Research pass (Diablo 3/4, Last Epoch, WoW): routine disposal gets ONE dialog with a suppression
    // checkbox committed by the confirming click; the EQUIPPED case is categorically different and can
    // never be suppressed — so that variant simply has no checkbox at all, rather than a checkbox the
    // code secretly ignores (which a future refactor would silently honour).

    /// <summary>Ask before SELL / SALVAGE? Host-set from the prefs file; the dialog can end the asking.</summary>
    public bool AskBeforeScrap { get; set; } = true;

    /// <summary>Set when the player flipped <see cref="AskBeforeScrap"/> in here — the host persists it.</summary>
    public bool PrefsDirty { get; set; }

    private enum ScrapKind { Sell, Salvage, JunkAll, CrushGem }

    /// <summary>The destructive action waiting on an answer, or null. A null id means the whole junk pile.</summary>
    private (ScrapKind Kind, string? ItemId)? _confirm;

    /// <summary>The SET-A-GEM question waiting on an answer, or null. Socketing is one-way (crushing
    /// destroys the gem) and costs Essence, so it always asks — this is not gated on AskBeforeScrap.</summary>
    private (string HostId, string GemId)? _socketAsk;
    private bool _confirmSuppress;    // the dialog checkbox — reset on every open
    private int _confirmGemIndex;     // CrushGem only: which socket the question is about
    private bool _confirmOpenedNow;   // swallows the click that OPENED the dialog so it cannot also answer it

    /// <summary>
    /// The build's loot-quality tilt, set by the host each frame. 1 is neutral.
    /// </summary>
    /// <remarks>
    /// Host-set like AutoSellFloor rather than resolved here, because the screen has no Build and the
    /// tilt is a build property (keystones + gear + the trait tree's FORTUNE road), not a Forge one.
    /// </remarks>
    public float RarityBonus { get; set; } = 1f;

    /// <summary>
    /// The active champion's ITEM CLASS, so four in five class-locked pieces a chest pays are theirs.
    /// Host-set every frame like <see cref="RarityBonus"/>: the screen has no roster of its own.
    /// </summary>
    public ItemClass? FavouredClass { get; set; }

    /// <summary>TIRELESS FORGE (Memory Dust): auto-merge the bag after OPEN ALL, so a bulk crack tidies itself.</summary>
    public bool AutoMergeOnOpen { get; set; }

    /// <summary>DEV ONLY: open the best chest, so a screenshot can pose the reveal burst.</summary>
    public void DevOpenOneChest(Hunter hunter) => OpenBestChest(hunter);

    public void AddLoot(IEnumerable<ItemInstance> items) => _inv.AddRange(items);


    /// <summary>
    /// Advance the chest-open reveal. Called by the HOST every frame, from every screen.
    /// </summary>
    /// <remarks>
    /// <b>THE REVEAL USED TO TICK INSIDE THIS SCREEN'S Update, AND THAT UPDATE ONLY RUNS ON THE FORGE.</b>
    /// Playtest: "Open Chest diyorum chest açılma animasyonu gelmiyor, FORGE ekranına geçince geliyor."
    /// Exactly that: the Vault's OPEN routes to ForgeScreen.OpenOneChest, which sets a 2.9-second timer —
    /// and then nothing decrements it, because Game1's _showChests branch returns above the _showForge
    /// branch that owns the clock. The animation waited, frozen, until the player happened to walk into
    /// the Forge, and then played for a chest they had opened minutes earlier.
    ///
    /// The state stays here (this class is the chest model as well as a view); what moved is who calls
    /// the clock and who calls the draw. Both are the host's job now, so the reveal plays WHERE THE
    /// PLAYER PRESSED — which is the only place it means anything.
    /// </remarks>
    public void TickReveal(float dt)
    {
        if (_revealTimer <= 0f || _revealFrozen) return;
        // THE CLOCK STOPS UNDER THE POINTER. A card that dissolves while the player is reading it — or
        // reaching for the SELL button on it — is the exact bug the reveal's new buttons would otherwise
        // become a trap for. Set by the draw (see DrawReveal); cleared the frame the pointer leaves.
        if (_revealPointerHold) return;
        var tBefore = _revealHold - _revealTimer;
        _revealTimer -= dt;
        PlayRevealTicks(tBefore, _revealHold - _revealTimer);
        if (_revealTimer > 0f) return;

        // This beat ended — the cascade decides what plays next.
        if (_revealQueue.Count > 0) NextRevealEntry();
        else if (_revealChestCount > 1 && !_revealSummary) EnterRevealSummary();
    }

    /// <summary>
    /// A click (or Space/Enter) while the reveal is up. The HOST routes input here, because the reveal
    /// is modal — skipping the cascade must never also press whatever sits under the cursor.
    /// </summary>
    public void AdvanceReveal()
    {
        if (_revealTimer <= 0f) return;
        _revealClosing = true;   // and the pointer-hold lets go, or the card the player just dismissed would stay
        if (_revealFrozen)
        {
            // The summary holds frozen until this click. A POSED capture (DevPoseReveal) stays put.
            if (_revealSummary) { _revealFrozen = false; _revealTimer = 0.35f; }
            return;
        }
        if (_revealChestCount > 1 && !_revealSummary) { _revealQueue.Clear(); EnterRevealSummary(); return; }
        _revealTimer = Math.Min(_revealTimer, 0.30f);   // single chest: hurry the fade
    }

    /// <summary>
    /// One bright tick per item as it lands on the reveal card — the sound of the stagger DrawReveal
    /// draws. Computed HERE because this is where the reveal's clock advances: item i starts its drop
    /// at burst + card-in + i·stagger (the same arithmetic DrawReveal uses, brief-aware), and a tick
    /// fires on the frame that instant is crossed. Each tick steps slightly up in pitch, so a
    /// three-drop chest reads as a little rising run. A skip that jumps the clock crosses several
    /// instants at once; the SoundBank's per-cue rate limit collapses those to one tick.
    /// </summary>
    private void PlayRevealTicks(float tBefore, float tAfter)
    {
        if (_revealSummary || _revealItems.Count == 0) return;
        var landBase = (_revealBrief ? 0.46f : BurstEnds) + (_revealBrief ? 0.14f : CardIn);
        var stagger = _revealBrief ? 0.08f : ItemStagger;
        // Only the LAST instant crossed this frame plays — a skip that jumps the clock is one tick,
        // not a burst — and the tick bypasses the throttle: the cascade's 80 ms stagger sat under the
        // 90 ms repeat gate, which silently dropped every second landing (review 2026-08-23).
        var lastCrossed = -1;
        for (var i = 0; i < _revealItems.Count; i++)
        {
            var lands = landBase + i * stagger;
            if (tBefore < lands && tAfter >= lands) lastCrossed = i;
        }
        if (lastCrossed >= 0)
            Sound?.Play("sfx_reveal_tick", 0.6f, pitch: Math.Min(0.5f, lastCrossed * 0.07f), throttle: false);
    }

    private void NextRevealEntry()
    {
        if (_revealQueue.Count == 0) { EnterRevealSummary(); return; }
        var (grade, mats, items) = _revealQueue.Dequeue();
        _revealClosing = false;
        _revealPointerHold = false;
        _revealIndex++;
        _revealGrade = grade;
        _revealTitle = $"{RarityNames[(int)grade]} CHEST";
        _revealMaterials = mats;
        _revealItems.Clear();
        _revealItems.AddRange(items);
        _revealBrief = true;
        // Rarity buys TIME, not just a colour — an Epic or Legendary in the cascade gets a fuller beat.
        _revealHold = BriefHold + (grade >= Rarity.Epic ? 0.6f : 0f);
        _revealTimer = _revealHold;
        _revealSummary = false;
    }

    private void EnterRevealSummary()
    {
        _revealClosing = false;
        _revealPointerHold = false;
        _revealSummary = true;
        _revealBrief = false;
        _revealTimer = 1f;      // any positive value — the freeze pins it until the closing click
        _revealFrozen = true;
    }

    /// <summary>Is a reveal on screen right now? The host asks, because the host draws it.</summary>
    public bool RevealActive => _revealTimer > 0f;

    /// <summary>
    /// The chest-open burst. Drawn by the host as chrome, over whatever screen is open.
    /// </summary>
    /// <remarks>
    /// Takes the Hunter because the reveal is a place you can ACT now — the hover card compares against
    /// what the champion is wearing, and SELL / SALVAGE pay into the same wallet the bench does.
    /// </remarks>
    public void DrawRevealOverlay(SpriteBatch b, Hunter hunter) => DrawReveal(b, hunter);

    public void Update(GameTime time, KeyboardState keys, Point mouse, bool clicked, int wheel, Hunter hunter,
                       bool inputLocked = false)
    {
        // The HOST's modals (unlock panel, settings, the reveal) own the frame: no key may reach the
        // bench's verbs through them. The keys are still LATCHED, or the key that closed the modal
        // would edge-fire here the moment it lifted.
        if (inputLocked) { _prevKeys = keys; return; }


        // THE CURSOR, IN THE SPACE THE RECTANGLES ARE AUTHORED IN. Every rect on this screen is written
        // in 1920x1080 and drawn through Game1's overlay inset; the incoming cursor is in 480x270 canvas
        // space. Draw already converts (see the twin below); Update did not, which is half of why the bag
        // was unusable — see the note on the wheel.
        var overlay = Game1.ToOverlay(mouse);

        // THE BAG'S WHEEL. It once sat below a mode return and tested the raw cursor against a
        // 1920-space rect — the full pathology is in 98ca957's message. The rule it settled on stands:
        // the wheel belongs to the bag, and nothing else on this screen scrolls.
        if (wheel != 0 && BagPanel.Contains(overlay))
        {
            // BagList, not the wearables: under the SOCKET tab the panel lists GEMS, and a wheel clamped
            // to a different list's length is the same dead-scroll bug this line already carries a note about.
            var rowCount = BagList().Count;
            _bagScroll = Math.Clamp(_bagScroll - wheel, 0, Math.Max(0, rowCount - BagRows));
        }

        // While a SELL / SALVAGE question is up it owns the KEYS — no key may scrap a second item
        // behind the question about the first. (The question is drawn in place now, and a click
        // anywhere else simply withdraws it — see NormaliseConfirm — so only the keys need the gate.)
        if (_confirm is not null || _socketAsk is not null) { _prevKeys = keys; return; }

        // Arrows cycle which wearable the bench is pointed at. S / D / J are the bench's verbs, and
        // they land in the same confirmation flow as the buttons — a key must never skip a question.
        if (Pressed(keys, Keys.Left)) CycleTarget(-1);
        if (Pressed(keys, Keys.Right)) CycleTarget(1);
        if (Pressed(keys, Keys.S)) Sell(hunter, Target());
        if (Pressed(keys, Keys.D)) Dismantle(hunter, Target());
        if (Pressed(keys, Keys.J)) SalvageJunk(hunter);
        _prevKeys = keys;
    }

    /// <summary>
    /// Merge everything mergeable, repeatedly, until nothing is left to fuse.
    /// </summary>
    /// <remarks>
    /// Hand-picking three Commons out of a bag of ninety is busywork, not a decision — and busywork is
    /// what an idle game is supposed to delete. Worn gear is never consumed.
    /// </remarks>
    /// <summary>
    /// The next trio auto-merge would take, or null.
    /// </summary>
    /// <remarks>
    /// ONE helper, two callers: the bag's preview line and the press itself. They used to hold two
    /// copies of this predicate — the preview asked only "are there three of one grade?", which is
    /// true in bags the press then refuses because the three are of different classes.
    ///
    /// Distinct-by-id WEARABLES only: a trio must be three DIFFERENT items (Merge rejects an item
    /// merged with itself) and gear, not currency — so a duplicate-id copy or a stack of Scrap can
    /// never grab the slots and stall the whole auto-merge. GEMMED PIECES ARE NEVER FED TO IT: the
    /// product is a brand-new item, so the socketed gems — the player's own investment — would
    /// silently cease to exist. ONE CLASS PER TRIO: the product inherits its inputs' class, so a trio
    /// drawn in bag order across classes fused two of the hunter's pieces into one they cannot wear.
    /// </remarks>
    private (Rarity Rarity, List<ItemInstance> Trio)? NextTrio(Hunter hunter)
    {
        bool Worn(ItemInstance i) =>
            Gear.SlotFor(i.BaseType) is { } s && hunter.Worn(s)?.InstanceId == i.InstanceId;

        foreach (var rarity in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic })
        {
            var trio = _inv.Where(i => i.Rarity == rarity && !Worn(i) && Gear.IsWearable(i) && i.Gems.Count == 0)
                           .DistinctBy(i => i.InstanceId)
                           .GroupBy(i => i.Class)
                           .OrderByDescending(g => g.Key is null || g.Key == FavouredClass)
                           .ThenByDescending(g => g.Count())
                           .Select(g => g.Take(3).ToList())
                           .FirstOrDefault(g => g.Count == 3);
            if (trio is not null) return (rarity, trio);
        }
        return null;
    }

    public int AutoMergeAll(Hunter hunter)
    {
        var merged = 0;
        // 200 is a runaway guard, not a budget: every round consumes three items and adds one, so a
        // real bag converges long before it.
        for (var rounds = 0; rounds < 200; rounds++)
        {
            if (NextTrio(hunter) is not { } pick) break;
            var result = Forge.Merge(pick.Trio, _rng, Tuning, LootTuning.Default);
            if (!result.Success) break;
            foreach (var i in pick.Trio) _inv.Remove(i);
            _inv.Add(result.Product!);
            merged++;
        }

        if (merged > 0) Sound?.Play("sfx_forge", 0.7f);
        Say(merged > 0 ? $"MERGED {merged} TIMES — THE BAG HOLDS {_inv.Count} ITEMS NOW." : "NOTHING TO MERGE — YOU NEED THREE OF THE SAME GRADE AND CLASS.",
            merged > 0 ? Gold : Slate);
        return merged;
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
        if (JunkOf(hunter).Count == 0) { Say("NO JUNK TO SALVAGE (COMMON / UNCOMMON GEAR).", Slate); return; }
        if (AskBeforeScrap) { OpenConfirm(ScrapKind.JunkAll, null); return; }
        SalvageJunkNow(hunter);
    }

    /// <summary>Every non-worn Common/Uncommon wearable — exactly what SALVAGE JUNK would take.</summary>
    private List<ItemInstance> JunkOf(Hunter hunter)
        => _inv.Where(i => Gear.IsWearable(i) && i.Rarity <= Rarity.Uncommon && !IsWorn(hunter, i)).ToList();

    private void SalvageJunkNow(Hunter hunter)
    {
        var junk = JunkOf(hunter);
        if (junk.Count == 0) return;

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
        }

        Sound?.Play("sfx_forge", 0.7f);
        Say($"SALVAGED {junk.Count} JUNK ITEMS — +{gained} {MaterialTiers.Name(Material.Scrap)}"
            + (chart ? "  (YOUR SALVAGE CHART DOUBLED IT)." : "."), Gold);
    }


    /// <summary>A multiplier as a signed percentage: 1.35 → "+35%", 0.8 → "-20%".</summary>
    private static string Pct(float mult)
    {
        var pct = (int)MathF.Round((mult - 1f) * 100f);
        return pct >= 0 ? $"+{pct}%" : $"{pct}%";
    }

    /// <summary>SELL, via the confirmation. Worn gear is allowed now — it comes off first, and it ALWAYS asks.</summary>
    private void Sell(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        var reason = Forge.CheckEligible(item);
        if (reason != IneligibleReason.Eligible) { Say(Forge.Explain(reason), Ember); return; }
        if (AskBeforeScrap || IsWorn(hunter, item)) { OpenConfirm(ScrapKind.Sell, item.InstanceId); return; }
        SellNow(hunter, item);
    }

    private void SellNow(Hunter hunter, ItemInstance item)
    {
        TakeOffFirst(hunter, item);
        ReleaseGems(item);
        _inv.Remove(item);
        hunter.AddGleam(item.SellValue);
        Sound?.Play("sfx_forge", 0.55f);
        Say(item.Gems.Count > 0
                ? $"SOLD FOR {item.SellValue} GLEAM — ITS {item.Gems.Count} GEMS CAME BACK TO YOU."
                : $"SOLD FOR {item.SellValue} GLEAM.", Gold);
    }

    /// <summary>
    /// The gems come OUT before the host dies. AUTO-MERGE already refuses gemmed pieces for exactly
    /// this reason; sell and salvage were quietly deciding it anyway (adversarial review, pass five).
    /// Returning them free keeps the Essence spent on SETTING as the only sunk cost.
    /// </summary>
    private void ReleaseGems(ItemInstance item) => _inv.AddRange(item.Gems);

    private void Dismantle(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;
        var reason = Forge.CheckEligible(item);
        if (reason != IneligibleReason.Eligible) { Say(Forge.Explain(reason), Ember); return; }
        if (AskBeforeScrap || IsWorn(hunter, item)) { OpenConfirm(ScrapKind.Salvage, item.InstanceId); return; }
        DismantleNow(hunter, item);
    }

    private void DismantleNow(Hunter hunter, ItemInstance item)
    {
        TakeOffFirst(hunter, item);
        ReleaseGems(item);
        _inv.Remove(item);
        var m = Forge.Dismantle(item, Tuning);
        var tier = MaterialTiers.ForRarity(item.Rarity);   // salvage sorts by rarity into the right tier
        hunter.AddMaterial(tier, m);
        Sound?.Play("sfx_forge", 0.55f);
        Say(item.Gems.Count > 0
                ? $"SALVAGED INTO {m} {MaterialTiers.Name(tier)} — ITS {item.Gems.Count} GEMS CAME BACK TO YOU."
                : $"SALVAGED INTO {m} {MaterialTiers.Name(tier)}.", Slate);
    }

    /// <summary>
    /// If the piece is on the champion, take it off first — selling or scrapping worn gear is allowed
    /// now (playtest asked for it), and the worn slot must never point at an item that no longer exists.
    /// </summary>
    private void TakeOffFirst(Hunter hunter, ItemInstance item)
    {
        if (Gear.SlotFor(item.BaseType) is { } s && hunter.Worn(s)?.InstanceId == item.InstanceId)
            hunter.Unequip(s);
    }

    /// <summary>Is the SELL/SALVAGE question on screen? The host reads it to give Esc the right job.</summary>
    public bool ConfirmOpen => _confirm is not null || _socketAsk is not null;

    /// <summary>Withdraw the question — Esc, or navigating away. Withdrawing never scraps anything.</summary>
    public void CancelConfirm() { _confirm = null; _socketAsk = null; }

    private void OpenConfirm(ScrapKind kind, string? itemId)
    {
        _confirm = (kind, itemId);
        _socketAsk = null;   // one question at a time — a stacked hidden question resurfaces uninvited
        _confirmSuppress = false;
        _confirmOpenedNow = true;
        // The question is drawn in the tab that owns the verb; open that tab so it is on screen.
        var subject = itemId is null ? null : _inv.FirstOrDefault(i => i.InstanceId == itemId);
        _tab = kind switch
        {
            ScrapKind.CrushGem => Tab.Socket,
            ScrapKind.Sell or ScrapKind.Salvage when GemCraft.IsGem(subject) => Tab.Socket,
            ScrapKind.Sell or ScrapKind.Salvage => Tab.BreakDown,
            _ => _tab,
        };
    }

    /// <summary>
    /// Re-roll the selected item's ENCHANTMENT — the build-defining roll. Rare and better only.
    /// </summary>
    /// <remarks>
    /// Who pays is Core's call — <see cref="Reforge.CanPay"/> and <see cref="Reforge.PayWith"/> — and
    /// the SAME call the button's enable check and its price line read, so the three cannot disagree.
    /// They did: the price line said 110 CORE while a REFORGE CHART paid, silently, press after press
    /// (playtest 2026-08-23). The message now names what paid. Validated first, spent last.
    /// </remarks>
    private void DoReforgeEnchant(Hunter hunter, ItemInstance? item)
    {
        if (item is null) return;

        if (item.Rarity < Enchantments.MinimumRarity) { Say("ONLY RARE AND BETTER ITEMS CARRY AN ENCHANT.", Ember); return; }

        var tier = Reforge.EnchantMaterial(item.Rarity);
        var cost = ReforgeTuning.Default.EnchantCostFor(item.Rarity);
        if (!Reforge.CanPay(hunter, tier, cost))
        {
            Say($"NEED {cost} {MaterialTiers.Name(tier)} TO RE-ROLL — YOU HOLD {hunter.MaterialOf(tier):N0}.", Ember);
            return;
        }

        var result = Reforge.ReforgeEnchant(item, _rng, ReforgeTuning.Default);
        if (!result.Success) { Say(result.Rejection!, Ember); return; }

        var paid = Reforge.PayWith(hunter, tier, result.Cost);
        if (!paid.Paid) { Say($"NEED {cost} {MaterialTiers.Name(tier)} TO RE-ROLL.", Ember); return; }
        ReplaceItem(hunter, item, result.Product!);
        Sound?.Play("sfx_forge", 0.7f);   // RE-ROLL is an anvil verb — the house forge cue
        var ench = Enchantments.Of(result.Product!);
        var how = paid.UsedChart ? "A REFORGE CHART PAID FOR IT" : $"{paid.MaterialSpent} {MaterialTiers.Name(tier)} SPENT";
        Say(ench is not null ? $"RE-ROLLED — THE ENCHANT IS NOW {ench.Name}  ({how})." : $"RE-ROLLED THE ENCHANT  ({how}).", Gold);
    }

    /// <summary>
    /// REFINE: spend SCRAP + Gold for one rung of the ladder — 15 rungs, the first five safe, then a
    /// rising slip chance. A slip still costs the materials and steps the item back one level and one
    /// rung; the ladder is climbed, not bought.
    /// </summary>
    private void DoRefine(Hunter hunter, ItemInstance? item)
    {
        if (item is null || !Gear.IsWearable(item)) { Say("ONLY GEAR CAN BE UPGRADED.", Ember); return; }
        if (Forge.AtRefineCap(item, Tuning)) { Say($"FULLY UPGRADED — {Tuning.MaxUpgrades} IS THE TOP OF THE LADDER.", Slate); return; }

        var r = Forge.Refine(item, Tuning);

        // A CHART PAYS FOR IT. Validated first and spent last, per Hunter.SpendCharter's contract: a
        // charter consumed before a rejection is a paper the player spent on a refusal, and these drop
        // one wave in forty.
        var chart = hunter.CharterCount(Charter.Refine) > 0;
        if (!chart)
        {
            if (hunter.MaterialOf(Material.Scrap) < r.Scrap) { Say($"NEED {r.Scrap} SCRAP TO UPGRADE — YOU HOLD {hunter.MaterialOf(Material.Scrap):N0}.", Ember); return; }
            if (hunter.Gleam < r.Gold) { Say($"NEED {r.Gold} GLEAM TO UPGRADE — YOU HOLD {Ab(hunter.Gleam)}.", Ember); return; }
        }

        if (chart) hunter.SpendCharter(Charter.Refine);
        else { hunter.SpendMaterial(Material.Scrap, r.Scrap); hunter.SpendGleam(r.Gold); }

        var outcome = Forge.TryRefine(item, Tuning, _rng);
        ReplaceItem(hunter, item, outcome.Product);
        if (!outcome.Failed) Sound?.Play("sfx_upgrade", 0.85f);   // the rung takes; a slip stays silent on purpose
        var how = chart ? "A REFINE CHART PAID FOR IT" : $"{r.Scrap} SCRAP + {r.Gold} GLEAM SPENT";
        if (outcome.Failed)
            Say($"THE UPGRADE SLIPPED — BACK TO LEVEL {outcome.Product.ItemLevel}, UPGRADE {outcome.Product.Upgrades} OF {Tuning.MaxUpgrades}  ({how}).", Ember);
        else
            Say($"UPGRADED TO LEVEL {outcome.Product.ItemLevel} — UPGRADE {outcome.Product.Upgrades} OF {Tuning.MaxUpgrades}  ({how}).", Gold);
    }

    /// <summary>
    /// GREATER REFINE: spend one CRYSTAL (+ Gold) to raise the item's level by FIVE at once — the premium,
    /// always-available sink for the rarest salvage tier. Reached by Shift-clicking REFINE, so Crystal has
    /// a broad drain of its own (not just the narrow "reroll a Legendary's enchant" path).
    /// </summary>
    private void DoGreaterRefine(Hunter hunter, ItemInstance? item)
    {
        if (item is null || !Gear.IsWearable(item)) { Say("ONLY GEAR CAN BE UPGRADED.", Ember); return; }
        if (Forge.AtRefineCap(item, Tuning)) { Say($"FULLY UPGRADED — {Tuning.MaxUpgrades} IS THE TOP OF THE LADDER.", Slate); return; }

        var r = Forge.GreaterRefine(item, Tuning);
        if (hunter.MaterialOf(Material.Crystal) < r.Crystal) { Say($"NEED {r.Crystal} CRYSTAL FOR A GREATER UPGRADE — YOU HOLD {hunter.MaterialOf(Material.Crystal):N0}.", Ember); return; }
        if (hunter.Gleam < r.Gold) { Say($"NEED {r.Gold} GLEAM FOR A GREATER UPGRADE — YOU HOLD {Ab(hunter.Gleam)}.", Ember); return; }

        hunter.SpendMaterial(Material.Crystal, r.Crystal);
        hunter.SpendGleam(r.Gold);
        ReplaceItem(hunter, item, r.Product);
        Sound?.Play("sfx_upgrade", 0.9f);
        Say($"GREATER UPGRADE — LEVEL {r.Product.ItemLevel}, UPGRADE {r.Product.Upgrades} OF {Tuning.MaxUpgrades}  ({r.Crystal} CRYSTAL + {r.Gold} GLEAM SPENT — IT NEVER SLIPS).", Gold);
    }

    private static bool IsWorn(Hunter hunter, ItemInstance item)
        => Gear.SlotFor(item.BaseType) is { } s && hunter.Worn(s)?.InstanceId == item.InstanceId;

    /// <summary>Swap an item in the bag for its reforged self. Same id, same slot — and the worn slot follows.</summary>
    private void ReplaceItem(Hunter hunter, ItemInstance old, ItemInstance neu)
    {
        var idx = _inv.IndexOf(old);
        if (idx >= 0) _inv[idx] = neu;
        // The id is unchanged, so id-based lookups keep resolving. The WORN slot does not: it stores the
        // OBJECT, not the id — so upgrading or reforging what you are wearing must hand the fight the new
        // object too, or the fight keeps reading pre-upgrade numbers while the bag shows the new ones.
        if (Gear.SlotFor(neu.BaseType) is { } s && hunter.Worn(s)?.InstanceId == neu.InstanceId)
            hunter.RestoreWorn(s, neu);
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
        _revealTitle = ChestDossiers.For(chest).Title;   // a gift is announced as the gift it is
        _revealMaterials = mat;
        _revealItems.Clear();
        _revealItems.AddRange(items);
        _revealTimer = RevealHold;
        _revealHold = RevealHold;
        _revealBrief = false;
        _revealSummary = false;
        _revealChestCount = 1;
        _revealMergedCount = 0;
        _revealQueue.Clear();
        ResetRevealActions();

        // Keep the footer line too (for OPEN ALL and as a fallback once the burst fades).
        var names = items.Count == 0
            ? "SOLD ON SIGHT (FILTER)"
            : string.Join(", ", items.Select(i => $"{RarityNames[(int)i.Rarity]} {ItemNames[i.BaseType]}"));
        Say($"{names}   +{mat} MATERIALS",
            items.Count > 0 ? RarityColors[items.Max(i => (int)i.Rarity)] : Slate);
    }

    /// <summary>Crack every chest at once — the idle-convenience twin of AUTO-MERGE ALL.</summary>
    private void OpenAllChests(Hunter hunter)
    {
        if (_chests.Count == 0) { Say("NO CHESTS YET — CLEAR A BOSS WAVE.", Slate); return; }

        // Worst grade first, best last — a cascade that ends on the chest the player cared about.
        // Playtest: "Chestlerde open all diyorum ne çıktığını görmüyorum" — the items just appeared
        // in the bag, and the one screen built around anticipation showed nothing at all.
        var opened = _chests.OrderBy(c => (int)c.Rarity).ToList();
        _chests.Clear();

        var entries = new List<(Rarity Grade, int Materials, List<ItemInstance> Items)>();
        _revealAll.Clear();
        _revealAllMats = 0;
        ResetRevealActions();
        foreach (var chest in opened)
        {
            var (m, landed) = LandChest(chest, hunter);
            _revealAllMats += m;
            _revealAll.AddRange(landed);
            entries.Add((chest.Rarity, m, landed));
        }

        // TIRELESS FORGE tidies the bulk haul — and the summary must SAY so, because it shows what
        // DROPPED, some of which the merge has already fused into better pieces.
        _revealMergedCount = AutoMergeOnOpen && _revealAll.Count > 0 ? AutoMergeAll(hunter) : 0;
        Say($"OPENED {opened.Count} CHESTS — +{_revealAllMats} MATERIALS, {_revealAll.Count} ITEMS.", Gold);

        if (entries.Count == 1)
        {
            // One chest gets the full single ceremony, not a one-entry cascade.
            Reveal(opened[0], (entries[0].Materials, entries[0].Items));
            return;
        }

        // The cascade plays at most the BEST eight — at thirty chests a full run outstays its welcome.
        // Nothing is silently dropped: the summary at the end shows everything that landed.
        const int CascadeCap = 8;
        _revealQueue.Clear();
        foreach (var e in entries.Skip(Math.Max(0, entries.Count - CascadeCap)))
            _revealQueue.Enqueue(e);
        _revealChestCount = entries.Count;
        _revealIndex = entries.Count - Math.Min(entries.Count, CascadeCap);
        _revealSummary = false;
        NextRevealEntry();
    }

    /// <summary>Roll a chest's contents, honour the loot filter, land the items + materials. Returns what landed.</summary>
    private (int Materials, List<ItemInstance> Items) LandChest(Chest chest, Hunter hunter)
    {
        var reward = Chests.Open(chest, _rng, LootTuning.Default, rarityBonus: RarityBonus, favouredClass: FavouredClass);
        _chestsOpened++;   // the CRAFTER evolution path's earn — Game1 polls this and credits the warren
        hunter.AddMaterials(reward.Materials);

        // Gems land beside the gear — same bag, same reveal, their own reward channel in Core.
        var items = reward.Items.Concat(reward.Gems).ToList();
        // Same loot filter a boss drop honours: filtered items are SOLD for gleam, never dumped in the bag.
        if (AutoSellFloor is { } floor)
        {
            // WEARABLES ONLY: a gem's frame grade tracks its LEVEL, not its worth — the filter selling
            // "Common" gems would quietly eat the socket system's whole supply line.
            foreach (var it in items.Where(i => Gear.IsWearable(i) && i.Rarity <= floor)) hunter.AddGleam(it.SellValue);
            items = items.Where(i => !Gear.IsWearable(i) || i.Rarity > floor).ToList();
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
    public void Draw(SpriteBatch b, Hunter hunter, Point mouse, bool clicked, bool rightClicked = false)
    {
        // Every rect is authored ×4 (1920×1080) and rendered at scale 1, so hit-tests take the mouse ×4.
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        _hovered = null;                 // re-established by whichever surface finds the pointer over an item
        if (_devPosePending) ApplyDevPose(hunter);

        // A SELL / SALVAGE question is drawn IN PLACE (in the tab that raised it), not as a modal, so
        // the rest of the screen stays live: clicking another tab or item simply withdraws it.
        NormaliseConfirm();
        var uiClicked = clicked;
        var uiRight = rightClicked;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));   // scrim so panels pop
        // FORGE, without the article (D7), page-centred — and WITHOUT the sentence that used to sit
        // under it telling the player to pick an item and choose a tab. That sentence named the four
        // tabs a third time (the tab strip and the deleted THE FOUR TABS legend named them twice), and
        // it was drawn inside the band the hint slot owns. The tour's "FOUR JOBS" card teaches it.
        _ui.TextCenterBig(b, "FORGE", UiKit.PageCenterX, 24, UiInk.Accent, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        // ONE WORKBENCH, full stop. The pile screen (grid + merge tray + its own chest bar) is
        // retired — playtest: "O ekrana gerek yok bence." Its two verbs that were real, auto-merge and
        // salvage-the-junk, live at the foot of the bag now; chests are opened where they are read, in
        // the VAULT, whose rail tile wears the pile count.
        DrawWorkbench(b, hunter, hit, uiClicked, uiRight);

        // (The reveal is drawn by the HOST now, as chrome — see Game1 and TickReveal. Drawing it here
        //  too would double-draw it on the one screen that used to be its only home.)

        // The hover card, above the surfaces and below nothing but the reveal — which is modal, and
        // whose whole job is to be the only thing you are looking at.
        // Clipped to the LEFT of the forge column, so a hover card can never cover the operation the
        // player is reading (the GEAR screen's rule).
        if (_hovered is { } hov && _revealTimer <= 0f)
            ItemTooltip.Draw(_ui, b, hov, hunter, hit, new Rectangle(0, 0, ForgePanel.X - 8, UiKit.Page.Height));

        if (DevForgeDebug) DrawDebug(b);
    }

    /// <summary>
    /// The bag, as a list you can see and click.
    /// </summary>
    /// <remarks>
    /// Every row carries the one thing that decides whether you care about it — its rarity, as a colour
    /// bar and as the name's ink — plus its LEVEL, because level is what UPGRADE moves and a player
    /// choosing what to refine is choosing between levels. Worn pieces are marked, because scrapping
    /// one is a bigger decision — and the confirmation dialog will say so.
    /// </remarks>
    private void DrawBag(SpriteBatch b, Hunter hunter, Point hit, bool clicked, bool rightClicked)
    {
        _ui.PanelQuiet(b, BagPanel);
        _ui.TextCenterBig(b, "BAG", BagPanel.Center.X, UiKit.TitleTop(BagPanel), Gold, UiTypography.PanelTitle);

        // ── THE FILTER, VISIBLE. ─────────────────────────────────────────────────────────────────
        //
        // The bag used to swap itself for a GEM DRAWER whenever the SOCKET tab was open — same panel,
        // different contents, no control — and it paid for that with a four-line block of help text
        // explaining the state it had entered on its own. Three chips say the state and let the player
        // change it, and the help block goes with them.
        var names = new[] { "ALL", "GEAR", "GEMS" };
        for (var i = 0; i < 3; i++)
        {
            var chip = FilterChip(i);
            var on = (int)_filter == i;
            var hot = chip.Contains(hit);
            _ui.Plate(b, chip, on ? UiInk.Accent : null);
            _ui.TextCenterBig(b, names[i], chip.Center.X, chip.Y + (34 - UiTypography.Caption) / 2 - 1,
                              on ? UiInk.Accent : hot ? Bone : Slate, UiTypography.Caption);
            if (UiKit.ClickedIn(chip, hit, clicked)) _filter = (BagFilter)i;
        }

        var bag = BagList();
        if (bag.Count == 0)
        {
            _ui.TextCenterBig(b, _filter == BagFilter.Gems ? "NO GEMS YET" : "THE BAG IS EMPTY",
                              BagPanel.Center.X, UiKit.BodyTop(BagPanel) + 40, UiInk.Empty, UiTypography.Body);
            _ui.TextCenterBig(b, _filter == BagFilter.Gems ? "CHESTS CARRY THEM." : "BOSSES DROP CHESTS.",
                              BagPanel.Center.X, UiKit.BodyTop(BagPanel) + 40 + UiTypography.Pitch(UiTypography.Body),
                              Slate, UiTypography.Secondary);
            return;
        }

        // Keep the focused item on screen without stealing the wheel from the player.
        // THE FOCUS IS ADOPTED BEFORE IT IS USED. With no focus set, FindIndex returns -1, Math.Max
        // floors it to 0, and the keep-the-selection-visible clamp below then reads "row 0 must be on
        // screen" — dragging _bagScroll back to zero on EVERY FRAME. Fixing the wheel alone changed
        // nothing: the scroll was undone a frame later by a line whose job is to be helpful.
        // NEVER FROM A GEM. The focus is "the wearable on the bench" — adopting a gem into it would
        // point UPGRADE, RE-ROLL and SALVAGE at a stone.
        var wearables = bag.Where(Gear.IsWearable).ToList();
        if (wearables.Count > 0 && (_focusId is null || wearables.All(i => i.InstanceId != _focusId)))
        {
            _focusId = wearables[0].InstanceId;
            _focusFollow = false;   // an adopted default is not a choice, so nothing should scroll to it
        }

        // THE CLAMP ONLY RUNS WHEN THE FOCUS WAS JUST CHOSEN. Adopting row 0 made the focus REAL, and
        // then this clamp, seeing focus 0, went on dragging _bagScroll to 0 every frame exactly as
        // before: a "keep the selection visible" rule running on frames where the selection had not moved.
        if (_focusFollow)
        {
            var focus = Math.Max(0, bag.FindIndex(i => i.InstanceId == _focusId));
            if (focus < _bagScroll) _bagScroll = focus;
            if (focus >= _bagScroll + BagRows) _bagScroll = focus - BagRows + 1;
            _focusFollow = false;
        }
        _bagScroll = Math.Clamp(_bagScroll, 0, Math.Max(0, bag.Count - BagRows));

        for (var vis = 0; vis < BagRows; vis++)
        {
            var idx = _bagScroll + vis;
            if (idx >= bag.Count) break;

            var it = bag[idx];
            var row = BagRow(vis);
            var rc = RarityColors[(int)it.Rarity];
            var gem = GemCraft.IsGem(it);
            var sel = gem ? it.InstanceId == _gemId : it.InstanceId == _focusId;
            var hover = row.Contains(hit);
            if (hover) _hovered = it;
            var isWorn = Gear.SlotFor(it.BaseType) is { } sl && hunter.Worn(sl)?.InstanceId == it.InstanceId;

            // A CLICK SELECTS. A gem row picks the gem the SOCKET tab will set — the refusals and the
            // question that used to fire from this click now live on that tab's button, where the
            // player can read them before committing. A gear row picks the item on the bench.
            if (UiKit.ClickedIn(row, hit, clicked) && !ConfirmOpen)
            {
                if (gem) { _gemId = it.InstanceId; _tab = Tab.Socket; }
                else { _focusId = it.InstanceId; _focusFollow = true; }
            }
            if (gem && !ConfirmOpen && UiKit.ClickedIn(row, hit, rightClicked)) Sell(hunter, it);

            _ui.Plate(b, row, sel ? rc : null);
            _ui.Fill(b, new Rectangle(row.X, row.Y, 5, row.Height), rc);

            DrawItemIcon(b, it, new Rectangle(row.X + 10, row.Y + 3, 30, 30));

            // ONE LINE PER ROW. A gem row used to draw two — a name at 19 px and its grant under it —
            // which needed 52 px inside a 40 px row once the type ladder settled. The gem's level moved
            // to the hover card, which every row already feeds.
            var right = gem ? GemCraft.Grant(it) : isWorn ? "WORN" : $"LEVEL {it.ItemLevel}";
            var rightInk = gem ? Met : isWorn ? Gold : Slate;
            var nameRoom = row.Right - 8 - _ui.MeasureBig(right, UiTypography.Body) - 16 - (row.X + 50);
            _ui.TextBig(b, _ui.ShortenBig(gem ? GemCraft.NameOf(it) : ItemNaming.FullName(it), nameRoom, UiTypography.Body),
                        row.X + 50, row.Y + 6, sel ? Bone : rc, UiTypography.Body);
            _ui.TextRightBig(b, right, row.Right - 8, row.Y + 6, rightInk, UiTypography.Body);
        }

        // A SCROLLBAR, so "there is more below" is something you can SEE rather than something you find
        // out by spinning the wheel. (The scroll counter that used to sit under the title is gone: the
        // bar says the same thing, and at 720p the counter was four physical pixels tall.)
        if (bag.Count > BagRows)
        {
            var first = BagRow(0);
            var track = new Rectangle(first.Right + 8, first.Y, 6, BagRows * BagRowH - 4);
            _ui.Fill(b, track, UiInk.Plate);
            var th = Math.Max(24, track.Height * BagRows / bag.Count);
            var ty = track.Y + (track.Height - th) * _bagScroll / Math.Max(1, bag.Count - BagRows);
            _ui.Fill(b, new Rectangle(track.X, ty, track.Width, th), Slate);
        }

        // ── THE FOOT: two bulk verbs that say what they will act on, and why they cannot. ─────────
        //
        // The JUNK question comes FIRST, whatever the tab: J is not gated on the tab, and OpenConfirm
        // does not move the tab for JunkAll, so the question must have a drawing surface here or the
        // forge's keys die and the next Escape is spent cancelling something invisible.
        if (_confirm is { Kind: ScrapKind.JunkAll })
        {
            DrawJunkQuestion(b, hunter, hit, clicked);
            return;
        }

        var trio = NextTrio(hunter);
        var junk = JunkOf(hunter).Count;

        if (_ui.Button(b, MergeBtn, $"MERGE 3 INTO 1 BETTER  ({(trio is null ? 0 : 1)})", hit, clicked, enabled: trio is not null))
            AutoMergeAll(hunter);
        if (trio is { } tr)
        {
            // WHAT THE PRESS WILL MAKE, from the same helper the press itself uses — so the preview
            // cannot describe a merge the button would not perform.
            var type = MergeRecipe.TypeOf(tr.Trio);
            var el = MergeRecipe.ElementOf(tr.Trio);
            var kind = ItemNames.TryGetValue(type, out var kn) ? kn : type.ToString().ToUpperInvariant();
            var line = $"NEXT: 3 {RarityNames[(int)tr.Rarity]} INTO 1 {RarityNames[(int)tr.Rarity + 1]} {kind}"
                       + (el is { } e ? $" · {e.ToString().ToUpperInvariant()}" : "")
                       + $" · LEVEL {tr.Trio.Max(i => i.ItemLevel)} · A NEW RANDOM PREFIX";
            _ui.TextBig(b, _ui.ShortenBig(line, BagContentW, UiTypography.Secondary),
                        UiKit.ContentLeft(BagPanel), MergePreviewY, Slate, UiTypography.Secondary);
        }
        else
            _ui.TextBig(b, _ui.ShortenBig("YOU NEED THREE OF THE SAME GRADE.", BagContentW, UiTypography.Secondary),
                        UiKit.ContentLeft(BagPanel), MergePreviewY, Slate, UiTypography.Secondary);

        if (_ui.Button(b, SalvageJunkBtn, $"SALVAGE COMMON AND UNCOMMON  ({junk})", hit, clicked, enabled: junk > 0))
            SalvageJunk(hunter);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // THE WORKBENCH — one item, four things you can do to it, and the wallet beside them.
    //
    // Playtest (2026-08-23), three complaints in one: "the Forge still feels complex and confusing";
    // "Scrap, Core and Essence sit in the middle with no icons — I didn't notice them"; and a RE-ROLL
    // that "said 110 Core, I had 139, and only the 5th or 6th press deducted it" — a REFORGE CHART was
    // paying the first presses, silently, while the price line kept saying 110 CORE.
    //
    // What changed, and why it is one change rather than three:
    //   - Three side-by-side panels of equal-weight buttons (eleven of them) became ONE card with a
    //     tab strip. A tab is an intent — UPGRADE, RE-ROLL, SOCKET, SALVAGE — and carries ONE big
    //     button with its price printed directly under it. Nothing is hidden behind a modifier key.
    //   - The materials moved to a WALLET with the real icons (mat_scrap / mat_essence / mat_core /
    //     mat_crystal — they were on disk all along; the "no dedicated icons exist" comment was wrong),
    //     at the top of the right column, under the currency pills, where money already lives.
    //   - Every price says who pays it: the chart you hold (FREE — and how many you hold), or the
    //     material — and what you hold of it. The enable check, the label and the payment now read the
    //     same Core predicate (Reforge.CanPay / PayWith), so they cannot disagree again.
    //   - The "are you sure?" question is asked IN PLACE — in the tab whose button raised it — instead
    //     of a modal over the whole screen. Switching tab or item withdraws it; it is never answered
    //     by a click meant for something else.
    // ══════════════════════════════════════════════════════════════════════════════════════════

    private void DrawWorkbench(SpriteBatch b, Hunter hunter, Point hit, bool clicked, bool rightClicked)
    {
        var item = Target();
        DrawMaterialStrip(b, hunter, item, hit);
        DrawBag(b, hunter, hit, clicked, rightClicked);
        DrawWorkPanel(b, hunter, item, hit, clicked);
        if (_stripTip is { } tip) _ui.HoverTip(b, tip, hit);
        _stripTip = null;
    }

    /// <summary>What a hovered chip in the materials strip is for — drawn last, over everything.</summary>
    private string? _stripTip;

    // ── THE MATERIALS STRIP ──────────────────────────────────────────────────────────────────────
    //
    // This was a 514 px ornate COLUMN — a quarter of the screen — holding five balances, an essay
    // about charts and a legend naming the four tabs. The balances are the only part of it a player
    // ever needs at a glance, and they need one row, not a column: five chips along the top, each
    // saying what it is, what you hold, and what the OPEN TAB will do to it. The chart essay and the
    // legend are the tour's job; what they said is in the tour cards word for word.
    private void DrawMaterialStrip(SpriteBatch b, Hunter hunter, ItemInstance? item, Point hit)
    {
        _ui.Plate(b, MaterialStrip);

        // The five balances, and — for the tab that is open — the mark that says what it needs or
        // gives. The marks are verb-first ("UPGRADE NEEDS 12", "SALVAGE GIVES +32") and every one of
        // them comes from the same Core call the button's enable check and its payment make.
        var marks = StripMarks(hunter, item);
        var rows = new (string Key, string Icon, Color Tint, string Name, long Held, string Use)[]
        {
            ("gleam", "currency_gleam", Gold, "GLEAM", hunter.Gleam, GleamUse),
            ("scrap", MatIcon[0], MatColor[0], "SCRAP", hunter.MaterialOf(Material.Scrap), MatUse(Material.Scrap)),
            ("essence", MatIcon[1], MatColor[1], "ESSENCE", hunter.MaterialOf(Material.Essence), MatUse(Material.Essence)),
            ("core", MatIcon[2], MatColor[2], "CORE", hunter.MaterialOf(Material.Core), MatUse(Material.Core)),
            ("crystal", MatIcon[3], MatColor[3], "CRYSTAL", hunter.MaterialOf(Material.Crystal), MatUse(Material.Crystal)),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var chip = MatChip(i);
            var r = rows[i];
            DrawMatIcon(b, r.Icon, r.Tint, new Rectangle(chip.X, chip.Y + 2, 32, 32));
            _ui.TextBig(b, r.Name, chip.X + 40, chip.Y + 6, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, Ab(r.Held), chip.Right, chip.Y, Bone, UiTypography.Headline);
            if (marks.TryGetValue(r.Key, out var mark))
                _ui.TextRightBig(b, mark.Text, chip.Right, chip.Y + UiTypography.Pitch(UiTypography.Headline),
                                 mark.Colour, UiTypography.Secondary);
            if (chip.Contains(hit)) _stripTip = r.Use;
        }

        // THE CHARTS, as chips at the right end — one per kind held, and nothing at all when you hold
        // none. The panel used to spend four lines saying "NONE RIGHT NOW — WAVES DROP ONE NOW AND
        // THEN"; an empty lane says the same thing and costs nothing.
        // Laid out RIGHT TO LEFT from the strip's own edge, each chip measured: laid out left to right
        // from a fixed lane, a third chart ran off the end of the screen.
        var cx = MaterialStrip.Right - 12;
        foreach (var c in new[] { Charter.Salvage, Charter.Reforge, Charter.Refine })
        {
            var n = hunter.CharterCount(c);
            if (n <= 0) continue;
            var pays = ChartPaysOpenTab(c);
            var label = $"{Charters.Name(c)} ×{n}";
            var cw = _ui.MeasureBig(label, UiTypography.Secondary) + 24;
            var chip = new Rectangle(cx - cw, MaterialStrip.Y + 6, cw, 38);
            // The lane is the promise the balances were laid out against; a chip that would cross it
            // is dropped rather than drawn over a number.
            if (chip.X < MaterialStrip.Right - ChartsLane) break;
            _ui.Plate(b, chip, pays ? UiInk.Accent : UiInk.Good);
            _ui.TextBig(b, label, chip.X + 12, chip.Y + 8, pays ? UiInk.Accent : UiInk.Good, UiTypography.Secondary);
            // A chart that will pay the NEXT press says so under its own chip — a badge, not a sentence.
            if (pays)
                _ui.TextRightBig(b, _tab == Tab.Upgrade ? "PAYS THE NEXT UPGRADE" : "PAYS THE NEXT RE-ROLL",
                                 chip.Right, chip.Bottom + 4, UiInk.Accent, UiTypography.Caption);
            if (chip.Contains(hit)) _stripTip = Charters.Blurb(c);
            cx = chip.X - 12;
        }
    }

    /// <summary>Does a chart of this kind pay the open tab's next press?</summary>
    private bool ChartPaysOpenTab(Charter c) =>
        (_tab == Tab.Upgrade && c == Charter.Refine && Target() is not null)
        || (_tab == Tab.Reroll && c == Charter.Reforge && Target() is { } it && it.Rarity >= Enchantments.MinimumRarity);

    /// <summary>
    /// What the OPEN tab's next press takes — or gives — per balance.
    /// </summary>
    /// <remarks>
    /// Verb first, so the mark reads as a sentence ("UPGRADE NEEDS 12") rather than as a second,
    /// unexplained number beside the balance. Every figure here comes from the same Core call the
    /// button's enable check and its payment make, which is why the price and the press cannot drift
    /// apart. Moved out of the deleted wallet column unchanged.
    /// </remarks>
    private Dictionary<string, (string Text, Color Colour)> StripMarks(Hunter hunter, ItemInstance? item)
    {
        var marks = new Dictionary<string, (string Text, Color Colour)>();
        void Need(string key, string verb, long need, long have) =>
            marks[key] = ($"{verb} NEEDS {need:N0}", have >= need ? Met : Ember);
        void Gives(string key, string verb, long amount) => marks[key] = ($"{verb} GIVES +{amount:N0}", Gold);
        if (item is null) return marks;
        switch (_tab)
        {
            case Tab.Upgrade when !Forge.AtRefineCap(item, Tuning):
                if (hunter.CharterCount(Charter.Refine) <= 0)
                {
                    var r = Forge.Refine(item, Tuning);
                    Need("scrap", "UPGRADE", r.Scrap, hunter.MaterialOf(Material.Scrap));
                    Need("gleam", "UPGRADE", r.Gold, hunter.Gleam);
                }
                Need("crystal", "GREATER", Forge.GreaterRefine(item, Tuning).Crystal, hunter.MaterialOf(Material.Crystal));
                break;
            case Tab.Reroll when item.Rarity >= Enchantments.MinimumRarity:
                if (hunter.CharterCount(Charter.Reforge) <= 0)
                {
                    var tier = Reforge.EnchantMaterial(item.Rarity);
                    Need(MatKey(tier), "RE-ROLL", ReforgeTuning.Default.EnchantCostFor(item.Rarity), hunter.MaterialOf(tier));
                }
                break;
            case Tab.Socket when GemCraft.SocketCount(item.Rarity) > 0:
                if (SocketPrice(item.Rarity) == 0) marks["essence"] = ("YOUR FIRST GEM IS FREE", Met);
                else Need("essence", "A GEM", SocketPrice(item.Rarity), hunter.MaterialOf(Material.Essence));
                break;
            case Tab.BreakDown:
                Gives("gleam", "SELL", item.SellValue);
                Gives(MatKey(MaterialTiers.ForRarity(item.Rarity)), "SALVAGE", Forge.Dismantle(item, Tuning));
                break;
        }
        return marks;
    }

    /// <summary>
    /// The two right-hand columns: WHAT IT IS, and WHAT YOU CAN DO TO IT.
    /// </summary>
    /// <remarks>
    /// The ornate frame moved. It was on the ITEM column — the one column of the four that asks for no
    /// decision — while the column holding every button, every price and every risk wore the quiet
    /// brown. The gold goes where the choice is.
    /// </remarks>
    private void DrawWorkPanel(SpriteBatch b, Hunter hunter, ItemInstance? item, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, ItemPanel);
        _ui.Panel(b, ForgePanel);

        if (item is null)
        {
            // An empty state in the disabled ink reads as a screen that is switched off. This one is a
            // fact, so it is written in the ink facts are written in.
            _ui.TextCenterBig(b, "NOTHING ON THE BENCH", ItemPanel.Center.X, ItemPanel.Y + 300,
                              UiInk.Empty, UiTypography.Headline);
            _ui.TextCenterBig(b, "YOUR BAG IS EMPTY.", ItemPanel.Center.X, ItemPanel.Y + 348, Slate, UiTypography.Body);
            _ui.TextCenterBig(b, "BOSSES DROP CHESTS, AND CHESTS DROP GEAR.", ItemPanel.Center.X,
                              ItemPanel.Y + 348 + UiTypography.Pitch(UiTypography.Body), Slate, UiTypography.Body);
            DrawTabStrip(b, hit, clicked);
            _ui.TextCenterBig(b, "PICK AN ITEM FIRST.", ForgePanel.Center.X, BodyTopY + 40, UiInk.Empty, UiTypography.Body);
            DrawFeedback(b);
            return;
        }

        DrawItemColumn(b, hunter, item, hit, clicked);

        DrawTabStrip(b, hit, clicked);
        switch (_tab)
        {
            case Tab.Upgrade: DrawUpgradeTab(b, hunter, item, TabBody(true), hit, clicked); break;
            case Tab.Reroll: DrawRerollTab(b, hunter, item, TabBody(false), hit, clicked); break;
            case Tab.Socket: DrawSocketTab(b, hunter, item, TabBody(false), hit, clicked); break;
            default: DrawBreakDownTab(b, hunter, item, TabBody(true), hit, clicked); break;
        }
        DrawFeedback(b);
    }

    /// <summary>The four intents, as tabs — the forge column's own header.</summary>
    private void DrawTabStrip(SpriteBatch b, Point hit, bool clicked)
    {
        for (var i = 0; i < TabNames.Length; i++)
        {
            var r = TabRect(i);
            var on = (int)_tab == i;
            var hover = r.Contains(hit);
            _ui.Plate(b, r, on ? UiInk.Accent : null);
            if (on) _ui.Fill(b, new Rectangle(r.X, r.Bottom - 3, r.Width, 3), Gold);
            // A THING YOU CLICK, at the rung things you click are set in.
            _ui.TextCenterBig(b, _ui.ShortenBig(TabNames[i], r.Width - 12, UiTypography.NavigationLabel),
                              r.Center.X, r.Y + 14, on ? Gold : hover ? Bone : Slate,
                              UiTypography.NavigationLabel, TextFace.Strong);
            // Switching tabs withdraws any question the old tab was asking — see NormaliseConfirm.
            if (UiKit.ClickedIn(r, hit, clicked) && !on) { _tab = (Tab)i; _confirm = null; _socketAsk = null; }
        }
    }

    // ── THE ITEM COLUMN — identity, and nothing that belongs beside a button. ────────────────────
    //
    // This column used to carry the before → after compare, four hundred pixels from the UPGRADE
    // button that produced it, at the smallest values on the screen. The compare moved to the forge
    // column (§56: the numbers belong with the verb). What is left here is what the piece IS: what it
    // is called, what it looks like, what it does right now, its prefix, its enchant and its sockets.
    private void DrawItemColumn(SpriteBatch b, Hunter hunter, ItemInstance item, Point hit, bool clicked)
    {
        var rc = RarityColors[(int)item.Rarity];
        var worn = IsWorn(hunter, item);
        var x = UiKit.ContentLeft(ItemPanel);
        var right = UiKit.ContentRight(ItemPanel);
        var w = right - x;

        // The sockets block, the enchant and the copy link are anchored to the FOOT. WHAT AN ITEM IS
        // must not be the thing a long affix list pushes off the bottom — at UI SCALE 125 % the flow
        // ran out of column exactly at the enchant, so the one line that says whether the piece does
        // anything in this build vanished while a fifth stat number stayed.
        var slots = GemCraft.SocketCount(item.Rarity);
        var socketTop = UiKit.ContentBottom(ItemPanel) - 40 - (slots > 0 ? 48 + 30 : UiTypography.Pitch(UiTypography.Body) + 30);
        var ench = item.Rarity < Enchantments.MinimumRarity ? null : Enchantments.Of(item);
        var enchLines = ench is null ? 1 : 2 + Math.Min(2, _ui.WrapBig(ench.Blurb, w, UiTypography.Secondary).Count);
        var enchTop = socketTop - 12 - enchLines * UiTypography.Pitch(UiTypography.Secondary) - 16;
        var flowFloor = enchTop - 12;

        var y = UiKit.TitleTop(ItemPanel);

        // A HEAD IS HELD UNTIL SOMETHING LANDS UNDER IT. On a short column the flow used to draw
        // "WHAT IT DOES" and then run out of room for every row it introduced — a heading over
        // nothing, which reads as a bug rather than as a section that did not fit.
        string? pendingHead = null;
        void Head(string s) => pendingHead = s;

        bool Room(int size)
        {
            var need = UiTypography.Pitch(size)
                       + (pendingHead is null ? 0 : UiTypography.Pitch(UiTypography.Secondary));
            if (y + need > flowFloor) return false;
            if (pendingHead is { } h)
            {
                _ui.TextBig(b, h, x, y, Slate, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
                pendingHead = null;
            }
            return true;
        }

        void Line(string s, Color ink, int size)
        {
            if (!Room(size)) return;
            _ui.TextBig(b, _ui.ShortenBig(s, w, size), x, y, ink, size);
            y += UiTypography.Pitch(size);
        }

        void Pair(string label, string value)
        {
            if (!Room(UiTypography.Body)) return;
            var vw = _ui.MeasureBig(value, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig(label, w - vw - 16, UiTypography.Body), x, y, Slate, UiTypography.Body);
            _ui.TextRightBig(b, value, right, y, Bone, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        void Rule()
        {
            if (y + 14 > flowFloor) return;
            _ui.Fill(b, new Rectangle(x, y + 6, w, 1), Dim);
            y += 16;
        }

        // CATEGORY · NAME — the inspector's own header, so the panel needs no title of its own.
        var kind = ItemNames.TryGetValue(item.BaseType, out var kn) ? kn : item.BaseType.ToString().ToUpperInvariant();
        Line($"{RarityNames[(int)item.Rarity]}  ·  {kind}  ·  LEVEL {item.ItemLevel}" + (worn ? "  ·  WORN" : ""),
             worn ? Gold : rc, UiTypography.Secondary);
        Line(ItemNaming.FullName(item), rc, UiTypography.Headline);

        // THE ELEMENT, as one chip. It used to take two lines, the second of which defined the element
        // by what it is NOT ("not the fight's Source") — a definition by negation, in a retired word.
        if (item.Element is { } src)
        {
            if (_ui.Assets.Get($"source_{src.ToString().ToLowerInvariant()}") is { } sg)
                b.Draw(sg, new Rectangle(x, y - 2, 28, 28), Color.White);
            _ui.TextBig(b, $"{src.ToString().ToUpperInvariant()}  ·  MERGE ELEMENT", x + 38, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
        }

        // The picture gives way first on a short column: it is the one element here that says nothing
        // the CATEGORY line above it does not.
        var art = ItemPanel.Height < 700 ? 110 : 150;
        var iconBox = new Rectangle(ItemPanel.Center.X - art / 2, y + 6, art, art);
        DrawItemIcon(b, item, iconBox);
        if (iconBox.Contains(hit)) _hovered = item;    // the big picture carries the full tooltip
        y = iconBox.Bottom + 10;

        Rule();
        Head("WHAT IT DOES");
        Pair("ITEM POWER", $"{hunter.PowerContribution(item):N0}");
        var affixes = ItemAffixes.Of(item);
        if (affixes.Count == 0) Line("NO STATS — RARER ITEMS CARRY MORE.", UiInk.Empty, UiTypography.Secondary);
        foreach (var a in affixes) Pair(ItemAffixes.StatWord(a.Stat), ItemAffixes.GrantLabel(a.Stat, a.Magnitude));

        if (GearTraits.TraitOf(item) is { } tr)
        {
            Rule();
            var effect = GearTraits.EffectOf(item);
            Pair($"PREFIX · {GearTraits.NameOf(tr)}", effect);
            if (effect.Length == 0) Line(GearTraits.BlurbOf(tr), Slate, UiTypography.Secondary);
        }

        // THE ENCHANT, in its reserved band: it is what the item IS, and the tab that re-rolls it now
        // has room to list what it could become instead.
        // The three closures stop at flowFloor; the enchant band lives BELOW it, so the floor is
        // lifted to the socket block before the band is drawn.
        y = enchTop;
        flowFloor = socketTop - 12;
        _ui.Fill(b, new Rectangle(x, y - 10, w, 1), Dim);
        if (ench is null)
            Line("NO ENCHANT — ONLY RARE AND BETTER ITEMS CARRY ONE.", UiInk.Empty, UiTypography.Secondary);
        else
        {
            Line($"ENCHANT · {ench.Name}", Bone, UiTypography.Secondary);
            foreach (var l in _ui.WrapBig(ench.Blurb, w, UiTypography.Secondary).Take(2))
                Line(l, Slate, UiTypography.Secondary);
            if (ench.Needs is { } need)
            {
                var met = need.MetBy(ActiveDefs, ActiveTriggers, SwornVows);
                Line(met ? "WORKS WITH YOUR BUILD RIGHT NOW."
                         : $"NEEDS {need.Label.ToUpperInvariant()} IN YOUR BUILD — UNTIL THEN IT DOES NOTHING.",
                     met ? Met : Slate, UiTypography.Secondary);
            }
        }

        // ── SOCKETS, anchored to the foot. ──
        var sy = socketTop;
        if (slots == 0)
            _ui.TextBig(b, _ui.ShortenBig("NO SOCKETS — RARE 1 · EPIC 2 · LEGENDARY 3.", w, UiTypography.Secondary),
                        x, sy + 6, Slate, UiTypography.Secondary);
        else
        {
            _ui.TextBig(b, $"SOCKETS  ·  {item.Gems.Count} OF {slots}", x, sy, Slate, UiTypography.Secondary);
            for (var i = 0; i < slots; i++)
            {
                var box = new Rectangle(x + i * 56, sy + 26, 48, 48);
                _ui.Plate(b, box);
                if (i < item.Gems.Count)
                {
                    DrawItemIcon(b, item.Gems[i], new Rectangle(box.X + 6, box.Y + 6, 36, 36));
                    // A set gem can be CRUSHED — the existing question owns the act.
                    if (UiKit.ClickedIn(box, hit, clicked)) { _tab = Tab.Socket; RequestCrush(item, i); }
                    if (box.Contains(hit)) _hovered = item.Gems[i];
                }
                else
                    _ui.TextCenterBig(b, "+", box.Center.X, box.Y + 10, UiInk.Empty, UiTypography.Body);
            }
        }

        // SHARE CODES: the item as one pasteable line. A quiet verb, drawn as a link, not a button —
        // it competes with nothing and it is not what this screen is for.
        var copyY = UiKit.ContentBottom(ItemPanel) - 28;
        var copyHot = new Rectangle(x, copyY - 4, w, 30).Contains(hit);
        _ui.TextCenterBig(b, "COPY ITEM CODE", ItemPanel.Center.X, copyY, copyHot ? Bone : Slate, UiTypography.Secondary);
        if (copyHot && clicked)
        {
            if (ClipboardInterop.TrySet(Core.Persistence.ShareCodes.EncodeItem(item)))
                Say("CODE COPIED — PASTE THE LINE TO SHOW A FRIEND THIS ITEM.", Gold);
            else
                Say("THE CLIPBOARD REFUSED — TRY AGAIN.", Ember);
        }
    }

    // ── THE COMPARE — the centre of the screen (§56). ────────────────────────────────────────────
    //
    // One row: what it is called, what it is now, what it becomes. The AFTER value is set at Headline
    // and the arrow is 16 px, because this is the thing the player came to read; it used to be Body
    // values behind a 20 px arrow on a panel four hundred pixels from the button.
    private int ChangeRow(string label, string? before, string after, Color afterInk, int y,
                          SpriteBatch b, int floor)
    {
        var step = UiTypography.Pitch(UiTypography.Headline);
        if (y + step > floor) return y;

        _ui.TextRightBig(b, after, FX + FW, y, afterInk, UiTypography.Headline);
        var used = _ui.MeasureBig(after, UiTypography.Headline);
        if (before is not null)
        {
            Arrow(b, FX + FW - used - 30, y + 12, Slate, 16);
            var beforeRight = FX + FW - used - 52;
            _ui.TextRightBig(b, before, beforeRight, y, Slate, UiTypography.Headline);
            used += 52 + _ui.MeasureBig(before, UiTypography.Headline);
        }
        _ui.TextBig(b, _ui.ShortenBig(label, FW - used - 16, UiTypography.Body), FX, y + 4, Slate, UiTypography.Body);
        return y + step;
    }

    /// <summary>The one-line verdict of the last act, on a quiet plate right under the button that caused it.</summary>
    private void DrawFeedback(SpriteBatch b)
    {
        if (_msg.Length == 0) return;
        _ui.Plate(b, Feedback, _msgColor);
        var room = Feedback.Width - 30;
        if (_ui.MeasureBig(_msg, UiTypography.Body) <= room)
        {
            _ui.TextBig(b, _msg, Feedback.X + 18, Feedback.Y + 24, _msgColor, UiTypography.Body);
            return;
        }
        // Two lines at Secondary rather than one chopped line at Body — "(A REFORGE CHART PAID F…" is
        // exactly the part of this message that must not be the part cut.
        var lines = _ui.WrapBig(_msg, room, UiTypography.Secondary);
        for (var i = 0; i < Math.Min(2, lines.Count); i++)
        {
            var line = i == 1 && lines.Count > 2
                ? _ui.ShortenBig(string.Join(" ", lines.Skip(1)), room, UiTypography.Secondary)
                : lines[i];
            _ui.TextBig(b, line, Feedback.X + 18, Feedback.Y + 14 + i * UiTypography.Pitch(UiTypography.Secondary),
                        _msgColor, UiTypography.Secondary);
        }
    }

    // ── UPGRADE ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// UPGRADE = the real REFINE: +1 item level, which raises every stat; 15 steps, the first five safe,
    /// then a rising slip chance. GREATER UPGRADE is a visible second button now, not a Shift modifier
    /// nobody could discover.
    /// </summary>
    private void DrawUpgradeTab(SpriteBatch b, Hunter hunter, ItemInstance item, Rectangle body, Point hit, bool clicked)
    {
        _ui.TextBig(b, "RAISE THE ITEM ONE LEVEL. EVERY STAT ON IT GROWS.", FX, IntentY, Bone, UiTypography.Body);

        var r = Forge.Refine(item, Tuning);
        var atCap = Forge.AtRefineCap(item, Tuning);
        var charts = hunter.CharterCount(Charter.Refine);
        var canPay = charts > 0 || (hunter.MaterialOf(Material.Scrap) >= r.Scrap && hunter.Gleam >= r.Gold);

        if (atCap)
        {
            // AT THE CAP THE BUTTON IS REMOVED, not greyed: a disabled control invites a click that
            // can never work. The state says itself.
            _ui.TextBig(b, $"FULLY UPGRADED  ·  {Tuning.MaxUpgrades} OF {Tuning.MaxUpgrades}",
                        FX, body.Y, Gold, UiTypography.Headline);
            _ui.TextBig(b, "THE TOP OF THE LADDER — IT CLIMBS NO FURTHER.", FX,
                        body.Y + UiTypography.Pitch(UiTypography.Headline), Slate, UiTypography.Body);
        }
        else
        {
            // ── WHAT WILL CHANGE. The whole reason this column is the wide one. ──
            var y = body.Y;
            y = ChangeRow("ITEM LEVEL", $"{item.ItemLevel}", $"{r.Product.ItemLevel}", Met, y, b, body.Bottom);
            y = ChangeRow("ITEM POWER", $"{hunter.PowerContribution(item):N0}",
                          $"{hunter.PowerContribution(r.Product):N0}", Met, y, b, body.Bottom);
            var cur = ItemAffixes.Of(item);
            var nxt = ItemAffixes.Of(r.Product);
            for (var i = 0; i < cur.Count && i < nxt.Count; i++)
                y = ChangeRow(ItemAffixes.StatWord(cur[i].Stat), AffixValPrecise(cur[i]), AffixValPrecise(nxt[i]),
                              Met, y, b, body.Bottom);

            // ── WHAT IS UNCERTAIN — the step count and the risk, in one line. (The progress bar that
            //    used to carry the step count is gone: it said in a shape what this says in words.) ──
            _ui.TextBig(b, _ui.ShortenBig(
                            r.FailChance <= 0
                                ? $"STEP {item.Upgrades + 1} OF {Tuning.MaxUpgrades}  ·  SAFE — THE FIRST {Tuning.RefineSafeUpgrades} NEVER FAIL."
                                : $"STEP {item.Upgrades + 1} OF {Tuning.MaxUpgrades}  ·  {(1 - r.FailChance) * 100:0}% SUCCESS. A SLIP DROPS ONE LEVEL AND ONE STEP.",
                            FW, UiTypography.Body),
                        FX, UncertainY(true), r.FailChance <= 0 ? Met : Ember, UiTypography.Body);

            var label = r.FailChance <= 0 ? "UPGRADE  +1 LEVEL"
                                          : $"UPGRADE  +1 LEVEL  ({(1 - r.FailChance) * 100:0}% SUCCESS)";
            if (_ui.Button(b, PrimaryBtn(true), label, hit, clicked, enabled: canPay, style: ButtonStyle.Primary))
                DoRefine(hunter, item);
            DrawPrice(b, FX, PriceY(true), FW, "COSTS",
                      [MatPrice(hunter, Material.Scrap, r.Scrap), GleamPrice(hunter, r.Gold)], Charter.Refine, charts);
        }

        // GREATER UPGRADE — the second, quieter verb. Two co-equal ornate buttons is a screen that has
        // not decided which one you came for. At the cap it is REMOVED, like the primary above it: a
        // disabled button that can never become enabled is an invitation with no answer.
        if (atCap) return;
        var g = Forge.GreaterRefine(item, Tuning);
        var canGreat = hunter.MaterialOf(Material.Crystal) >= g.Crystal && hunter.Gleam >= g.Gold;
        if (_ui.Button(b, OptionBtn, $"GREATER UPGRADE  +{g.Steps} LEVELS — NEVER SLIPS", hit, clicked, enabled: canGreat))
            DoGreaterRefine(hunter, item);
        _ui.TextBig(b, _ui.ShortenBig(
                        $"COSTS {g.Crystal} CRYSTAL AND {Ab(g.Gold)} GLEAM — YOU HOLD {hunter.MaterialOf(Material.Crystal):N0} CRYSTAL",
                        FW, UiTypography.Secondary),
                    FX, OptionPriceY, canGreat ? Slate : Ember, UiTypography.Secondary);
    }

    // ── RE-ROLL ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// RE-ROLL THE ENCHANT — the build-defining trigger, Rare+ only. The one tab where the chart bug
    /// lived: the price line, the enable check and the payment all go through Core now.
    /// </summary>
    private void DrawRerollTab(SpriteBatch b, Hunter hunter, ItemInstance item, Rectangle body, Point hit, bool clicked)
    {
        var hasEnch = item.Rarity >= Enchantments.MinimumRarity;
        var ench = Enchantments.Of(item);

        if (!hasEnch)
        {
            // A LOCKED TAB SAYS WHAT UNLOCKS IT. It used to say "NONE — THIS ITEM IS BELOW RARE" in
            // the hairline ink and stop there, which is a dead end wearing the colour of a disabled
            // control.
            _ui.TextBig(b, "THIS ITEM CANNOT BE RE-ROLLED.", FX, IntentY, Bone, UiTypography.Body);
            _ui.TextBig(b, "COMMON AND UNCOMMON ITEMS CARRY NO ENCHANT.", FX, body.Y, Bone, UiTypography.Body);
            _ui.TextBig(b, "MERGE THREE OF THEM TO REACH RARE.", FX,
                        body.Y + UiTypography.Pitch(UiTypography.Body), Slate, UiTypography.Body);
            return;
        }

        _ui.TextBig(b, "A NEW ENCHANT REPLACES THIS ONE. NEVER THE SAME ONE AGAIN.", FX, IntentY, Bone, UiTypography.Body);

        // ── WHAT WILL CHANGE: the exact set Reforge.Roll draws from. ─────────────────────────────
        //
        // The tab used to say "a new random enchant" and nothing else — the player pressed a button
        // that spent Core to pick blind from a list the game already knew. Listing the candidates is
        // the honest form of §58, and each one carries the only verdict Core can give about it: does
        // your build satisfy what it needs? There is deliberately NO better/worse word and no per-
        // candidate percentage: Core has no ordering over enchants, and printing one would invent it.
        var y = ChangeRow("ENCHANT", ench?.Name ?? "NONE", "ONE OF THESE", Gold, body.Y, b, body.Bottom);
        var slot = Gear.SlotFor(item.BaseType);
        if (slot is { } sl)
            foreach (var kind in Enchantments.PoolFor(sl).Where(k => ench is null || k != ench.Kind))
            {
                if (y + UiTypography.Pitch(UiTypography.Body) > body.Bottom) break;
                var cand = new Enchantment(kind, Enchantments.MagnitudeFor(kind, item.Rarity));
                var verdict = cand.Needs is { } nd
                    ? nd.MetBy(ActiveDefs, ActiveTriggers, SwornVows) ? "WORKS WITH YOUR BUILD" : $"NEEDS {nd.Label.ToUpperInvariant()}"
                    : "";
                var vw = verdict.Length == 0 ? 0 : _ui.MeasureBig(verdict, UiTypography.Secondary);
                _ui.TextBig(b, _ui.ShortenBig(cand.Name, FW - vw - 16, UiTypography.Body), FX, y, Bone, UiTypography.Body);
                if (verdict.Length > 0)
                    _ui.TextRightBig(b, verdict, FX + FW, y + 2,
                                     verdict[0] == 'W' ? Met : Slate, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Body);
            }

        _ui.TextBig(b, "WHICH OF THESE YOU GET IS RANDOM.", FX, UncertainY(false), Slate, UiTypography.Body);
        _ui.TextBig(b, "LEVEL, STATS, GEMS AND THE PREFIX DO NOT CHANGE.", FX,
                    UncertainY(false) + UiTypography.Pitch(UiTypography.Body), Slate, UiTypography.Body);

        var tier = Reforge.EnchantMaterial(item.Rarity);
        var cost = ReforgeTuning.Default.EnchantCostFor(item.Rarity);
        var charts = hunter.CharterCount(Charter.Reforge);
        // Enabled by the SAME predicate that pays: a chart holder with no Core sees a live button.
        if (_ui.Button(b, PrimaryBtn(false), "RE-ROLL THE ENCHANT", hit, clicked,
                       enabled: Reforge.CanPay(hunter, tier, cost), style: ButtonStyle.Primary))
            DoReforgeEnchant(hunter, item);
        DrawPrice(b, FX, PriceY(false), FW, "COSTS", [MatPrice(hunter, tier, cost)], Charter.Reforge, charts);
    }

    // ── SOCKET ───────────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// The item's sockets and the player's loose gems, side by side.
    /// </summary>
    /// <remarks>
    /// Rare 1 / Epic 2 / Legendary 3 sockets. Click a gem in the strip to SET it — it fills the first
    /// open socket (sockets have no positions; an "aim" marker shipped here briefly and was dead
    /// state the setter never read). Right-click a gem to SELL it — the strip is the only place loose
    /// gems live, so it must offer the way out too. Click a SET gem to CRUSH it: the gem dies, the
    /// slot opens, the item is never at risk. Both questions are asked here, in the strip's place.
    /// </remarks>
    private void DrawSocketTab(SpriteBatch b, Hunter hunter, ItemInstance item, Rectangle body, Point hit, bool clicked)
    {
        var slots = GemCraft.SocketCount(item.Rarity);
        var question = new Rectangle(FX, body.Y, FW, Feedback.Y - 12 - body.Y);

        // A question about a gem — set a loose one, crush a set one, sell a loose one — takes the whole
        // body, including the button block, so no primary is drawn behind it.
        if (_socketAsk is { } sa)
        {
            // Re-resolved every frame: the bench item may have changed, the gem may have been sold.
            var saHost = Target();
            var saGem = _inv.FirstOrDefault(i => i.InstanceId == sa.GemId);
            if (saHost is null || saHost.InstanceId != sa.HostId || saGem is null || !GemCraft.IsGem(saGem))
                _socketAsk = null;
            else
            {
                DrawSocketQuestion(b, hunter, saHost, saGem, question, hit, clicked);
                return;
            }
        }
        if (_confirm is { } ask && ask.ItemId is { } qid && ask.Kind is ScrapKind.CrushGem or ScrapKind.Sell)
        {
            if (ask.Kind == ScrapKind.CrushGem && qid == item.InstanceId && _confirmGemIndex < item.Gems.Count)
            {
                DrawCrushQuestion(b, hunter, item, question, hit, clicked);
                return;
            }
            if (ask.Kind == ScrapKind.Sell && _inv.FirstOrDefault(i => i.InstanceId == qid) is { } sold && GemCraft.IsGem(sold))
            {
                DrawScrapQuestion(b, hunter, sold, ScrapKind.Sell, question, hit, clicked);
                return;
            }
        }

        _ui.TextBig(b, "SET A GEM INTO THIS ITEM TO ADD ITS STAT.", FX, IntentY, Bone, UiTypography.Body);

        if (slots == 0)
        {
            _ui.TextBig(b, "THIS ITEM HAS NO SOCKETS.", FX, body.Y, Bone, UiTypography.Body);
            _ui.TextBig(b, "RARE 1 · EPIC 2 · LEGENDARY 3.", FX, body.Y + UiTypography.Pitch(UiTypography.Body),
                        Slate, UiTypography.Body);
            return;
        }

        // THE GEM IS SELECTED IN THE BAG, NOT COMMITTED THERE. Clicking a gem used to run every refusal
        // check and then raise the question straight from the bag row — so the reasons it might refuse
        // were toasts fired after the click. They are the button's enable and its refusal line now.
        var gem = _gemId is null ? null : _inv.FirstOrDefault(i => i.InstanceId == _gemId && GemCraft.IsGem(i));
        var cost = SocketPrice(item.Rarity);
        var full = item.Gems.Count >= slots;
        var poor = hunter.MaterialOf(Material.Essence) < cost;

        if (gem is null)
        {
            _ui.TextBig(b, "PICK A GEM IN THE BAG ON THE LEFT.", FX, body.Y, UiInk.Empty, UiTypography.Body);
            if (Gems().Count == 0)
                _ui.TextBig(b, "YOU HOLD NO GEMS YET — CHESTS CARRY THEM.", FX,
                            body.Y + UiTypography.Pitch(UiTypography.Body), Slate, UiTypography.Body);
        }
        else
        {
            var product = GemCraft.Socket(item, gem).Product;
            var y = body.Y;
            y = ChangeRow("ITEM POWER", $"{hunter.PowerContribution(item):N0}",
                          product is null ? "—" : $"{hunter.PowerContribution(product):N0}", Met, y, b, body.Bottom);
            y = ChangeRow($"ADDS {ItemAffixes.StatWord(GemCraft.StatOf(gem))}", null,
                          ItemAffixes.GrantLabel(GemCraft.StatOf(gem), GemCraft.Magnitude(gem)), Met, y, b, body.Bottom);
            ChangeRow("SOCKETS USED", $"{item.Gems.Count} OF {slots}", $"{item.Gems.Count + 1} OF {slots}",
                      Bone, y, b, body.Bottom);
        }

        _ui.TextBig(b, "NOTHING HERE IS RANDOM.", FX, UncertainY(false), Slate, UiTypography.Body);
        _ui.TextBig(b, "A SET GEM CANNOT COME BACK OUT — CRUSHING IT LATER DESTROYS IT.", FX,
                    UncertainY(false) + UiTypography.Pitch(UiTypography.Body), Ember, UiTypography.Body);

        var can = gem is not null && !full && !poor;
        if (_ui.Button(b, PrimaryBtn(false), gem is null ? "SET A GEM" : $"SET {GemCraft.NameOf(gem)}",
                       hit, clicked, enabled: can, style: ButtonStyle.Primary) && gem is not null)
        {
            // The one-way act still asks — the existing question, unchanged.
            _confirm = null;
            _confirmSuppress = false;
            _socketAsk = (item.InstanceId, gem.InstanceId);
            _confirmOpenedNow = true;
        }

        // THE REFUSAL, WHERE THE BUTTON IS. These two sentences used to be toasts fired by a bag click.
        if (full)
            _ui.TextBig(b, "EVERY SOCKET IS FULL — CRUSH A GEM TO FREE ONE.", FX, PriceY(false), Ember, UiTypography.Secondary);
        else if (gem is not null && poor)
            _ui.TextBig(b, $"YOU NEED {cost} ESSENCE — YOU HOLD {hunter.MaterialOf(Material.Essence):N0}.",
                        FX, PriceY(false), Ember, UiTypography.Secondary);
        else if (cost == 0)
        {
            _ui.TextBig(b, "YOUR FIRST GEM IS FREE", FX, PriceY(false), Gold, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig($"LATER ONES COST {GemCraft.SocketCost(item.Rarity)} ESSENCE FOR AN ITEM OF THIS GRADE.",
                                          FW, UiTypography.Secondary),
                        FX, PriceY(false) + UiTypography.Pitch(UiTypography.Body), Slate, UiTypography.Secondary);
        }
        else
            DrawPrice(b, FX, PriceY(false), FW, "SETTING A GEM COSTS",
                      [MatPrice(hunter, Material.Essence, cost)], null, 0);
    }

    /// <summary>SET a gem into the first open socket, spending Essence — none for the player's first gem.</summary>
    private void TrySocket(Hunter hunter, ItemInstance host, ItemInstance gem)
    {
        var slots = GemCraft.SocketCount(host.Rarity);
        if (slots == 0) { Say("ONLY RARE AND BETTER GEAR HAS SOCKETS.", Ember); return; }

        var cost = SocketPrice(host.Rarity);
        if (hunter.MaterialOf(Material.Essence) < cost)
        {
            Say($"NEED {cost} ESSENCE TO SET A GEM — YOU HOLD {hunter.MaterialOf(Material.Essence):N0}.", Ember);
            return;
        }

        var (product, rejection) = GemCraft.Socket(host, gem);
        if (product is null) { Say(rejection!, Ember); return; }

        // A free first gem spends nothing (SpendMaterial refuses a zero anyway); either way the free
        // one is now USED, and every later socket is the Essence sink it always was.
        if (cost > 0) hunter.SpendMaterial(Material.Essence, cost);
        var wasFree = cost == 0;
        FreeSocketUsed = true;
        _inv.Remove(gem);
        ReplaceItem(hunter, host, product);
        Sound?.Play("sfx_gem", 0.8f);   // the crystalline ping — a gem set for good
        var paid = wasFree ? "YOUR FIRST GEM WAS FREE" : $"{cost} ESSENCE SPENT";
        Say($"{GemCraft.NameOf(gem)} {gem.ItemLevel} SET — {ItemAffixes.GrantLabel(GemCraft.StatOf(gem), GemCraft.Magnitude(gem))} {AffixName(GemCraft.StatOf(gem))}  ({paid}).", Gold);
    }

    /// <summary>
    /// CRUSH the gem in a socket — through the question, and it ALWAYS asks: the "don't ask me again"
    /// box is labelled for selling and salvaging, and a click on a filled socket is one pixel from the
    /// hover-to-inspect gesture (adversarial review, pass five).
    /// </summary>
    private void RequestCrush(ItemInstance host, int index)
    {
        _confirmGemIndex = index;
        OpenConfirm(ScrapKind.CrushGem, host.InstanceId);
    }

    private void CrushNow(Hunter hunter, ItemInstance host, int index)
    {
        if (GemCraft.Crush(host, index) is not { } result) return;
        ReplaceItem(hunter, host, result.Product);
        Say($"{GemCraft.NameOf(result.Crushed)} CRUSHED — THE SOCKET IS OPEN.", Slate);
    }

    /// <summary>The SET question — socketing is one-way, so the forge asks before it commits.</summary>
    private void DrawSocketQuestion(SpriteBatch b, Hunter hunter, ItemInstance host, ItemInstance gem,
                                    Rectangle area, Point hit, bool clicked)
    {
        // The click that OPENED the question is still latched this frame; it must not also answer it.
        if (_confirmOpenedNow) { clicked = false; _confirmOpenedNow = false; }

        var x = area.X; var y = area.Y; var w = area.Width;
        _ui.Fill(b, new Rectangle(x - 12, y - 10, w + 24, 262), new Color(0x1C, 0x1A, 0x2A, 0xC0));
        _ui.TextBig(b, "SET THIS GEM?", x, y, Gold, UiTypography.PanelTitle);
        DrawItemIcon(b, gem, new Rectangle(x, y + 44, 56, 56));
        _ui.TextBig(b, _ui.ShortenBig($"{GemCraft.NameOf(gem)} {gem.ItemLevel}  —  {ItemAffixes.GrantLabel(GemCraft.StatOf(gem), GemCraft.Magnitude(gem))} {AffixName(GemCraft.StatOf(gem))}", w - 70, UiTypography.Body),
                    x + 70, y + 58, RarityColors[(int)gem.Rarity], UiTypography.Body);
        var price = SocketPrice(host.Rarity) == 0 ? "YOUR FIRST GEM IS FREE" : $"COSTS {SocketPrice(host.Rarity)} ESSENCE";
        _ui.TextBig(b, _ui.ShortenBig($"INTO {ItemNaming.FullName(host)} — {price}.", w, UiTypography.Secondary),
                    x, y + 116, SocketPrice(host.Rarity) == 0 ? Gold : Bone, UiTypography.Secondary);
        _ui.TextBig(b, "A SET GEM CANNOT COME BACK OUT — CRUSHING IT LATER DESTROYS IT.",
                    x, y + 144, Slate, UiTypography.Secondary);

        var bw = (w - 16) / 2;
        var keep = new Rectangle(x, y + 186, bw, 56);
        var doIt = new Rectangle(x + bw + 16, y + 186, bw, 56);
        // KEEP sits first, so a reflex click lands on the safe answer.
        if (_ui.Button(b, keep, "NO — KEEP IT", hit, clicked)) { _socketAsk = null; return; }
        if (_ui.Button(b, doIt, "YES — SET IT", hit, clicked))
        {
            _socketAsk = null;
            TrySocket(hunter, host, gem);
        }
    }

    /// <summary>The CRUSH question, in the socket tab, in the gem strip's place.</summary>
    private void DrawCrushQuestion(SpriteBatch b, Hunter hunter, ItemInstance host, Rectangle area, Point hit, bool clicked)
    {
        // The click that OPENED the question is still latched this frame; it must not also answer it.
        if (_confirmOpenedNow) { clicked = false; _confirmOpenedNow = false; }

        var gem = host.Gems[_confirmGemIndex];
        var x = area.X; var y = area.Y; var w = area.Width;
        _ui.Fill(b, new Rectangle(x - 12, y - 10, w + 24, 226), new Color(0x2A, 0x16, 0x1C, 0xC0));
        _ui.TextBig(b, "CRUSH THIS GEM?", x, y, Gold, UiTypography.PanelTitle);
        DrawItemIcon(b, gem, new Rectangle(x, y + 44, 56, 56));
        _ui.TextBig(b, _ui.ShortenBig($"{GemCraft.NameOf(gem)} {gem.ItemLevel}  —  {ItemAffixes.GrantLabel(GemCraft.StatOf(gem), GemCraft.Magnitude(gem))} {AffixName(GemCraft.StatOf(gem))}", w - 70, UiTypography.Body),
                    x + 70, y + 58, RarityColors[(int)gem.Rarity], UiTypography.Body);
        _ui.TextBig(b, "THE GEM IS DESTROYED AND THE SOCKET OPENS. THIS CANNOT BE UNDONE.", x, y + 116, Bone, UiTypography.Secondary);

        var bw = (w - 16) / 2;
        var keep = new Rectangle(x, y + 150, bw, 56);
        var doIt = new Rectangle(x + bw + 16, y + 150, bw, 56);
        // KEEP sits first, so a reflex click lands on the safe answer.
        if (_ui.Button(b, keep, "NO — KEEP IT", hit, clicked)) { _confirm = null; return; }
        if (_ui.Button(b, doIt, "YES — CRUSH IT", hit, clicked))
        {
            _confirm = null;
            CrushNow(hunter, host, _confirmGemIndex);
        }
    }

    // ── SALVAGE ───────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// SELL and SALVAGE, and the question they raise, all in one place under the item they destroy.
    /// </summary>
    private void DrawBreakDownTab(SpriteBatch b, Hunter hunter, ItemInstance item, Rectangle body, Point hit, bool clicked)
    {
        if (_confirm is { } ask && ask.ItemId == item.InstanceId && ask.Kind is ScrapKind.Sell or ScrapKind.Salvage)
        {
            DrawScrapQuestion(b, hunter, item, ask.Kind, new Rectangle(FX, body.Y, FW, Feedback.Y - 12 - body.Y),
                              hit, clicked);
            return;
        }

        _ui.TextBig(b, "THE ITEM IS GONE EITHER WAY.", FX, IntentY, Bone, UiTypography.Body);

        var tier = MaterialTiers.ForRarity(item.Rarity);
        var mats = Forge.Dismantle(item, Tuning);
        var y = body.Y;
        y = ChangeRow("SALVAGE GIVES", null, $"+{mats} {MaterialTiers.Name(tier)}", Met, y, b, body.Bottom);
        y = ChangeRow("SELL GIVES", null, $"+{item.SellValue:N0} GLEAM", Met, y, b, body.Bottom);
        if (item.Gems.Count > 0)
            ChangeRow("GEMS RETURNED", null, $"{item.Gems.Count} TO YOUR BAG", Met, y, b, body.Bottom);

        var worn = IsWorn(hunter, item);
        _ui.TextBig(b, _ui.ShortenBig(worn ? "THIS IS ON YOUR HUNTER RIGHT NOW — IT COMES OFF FIRST."
                                           : "THIS CANNOT BE UNDONE.", FW, UiTypography.Body),
                    FX, UncertainY(true), Ember, UiTypography.Body);

        if (_ui.Button(b, PrimaryBtn(true), $"SALVAGE FOR {mats} {MaterialTiers.Name(tier)}", hit, clicked,
                       enabled: true, style: ButtonStyle.Primary))
            Dismantle(hunter, item);
        _ui.TextBig(b, _ui.ShortenBig($"{MaterialTiers.Name(tier)} — {MatUse(tier)}", FW, UiTypography.Secondary),
                    FX, PriceY(true), Slate, UiTypography.Secondary);

        if (_ui.Button(b, OptionBtn, $"SELL FOR {item.SellValue:N0} GLEAM", hit, clicked)) Sell(hunter, item);
        _ui.TextBig(b, _ui.ShortenBig($"GLEAM — {GleamUse}", FW, UiTypography.Secondary),
                    FX, OptionPriceY, Slate, UiTypography.Secondary);

        if (!AskBeforeScrap)
            _ui.TextBig(b, _ui.ShortenBig("YOU TURNED OFF THE ARE-YOU-SURE QUESTION. WORN GEAR STILL ASKS.",
                                          FW, UiTypography.Secondary),
                        FX, body.Bottom - UiTypography.Pitch(UiTypography.Secondary), Slate, UiTypography.Secondary);
    }

    /// <summary>
    /// The SELL / SALVAGE question, asked in place — in the tab whose button raised it.
    /// </summary>
    /// <remarks>
    /// One question, two variants (never two stacked — stacking trains blind-clicking): the ROUTINE
    /// variant carries a "don't ask me again" box whose state commits together with the confirming
    /// click; the WORN variant has no box at all and asks regardless of the preference, because
    /// destroying what you are wearing is the one mistake that genuinely hurts. KEEP sits first so a
    /// reflex click lands on the safe answer; the destructive verb is named, never a generic CONFIRM.
    /// </remarks>
    private void DrawScrapQuestion(SpriteBatch b, Hunter hunter, ItemInstance item, ScrapKind kind, Rectangle area, Point hit, bool clicked)
    {
        // The click that OPENED the question is still latched this frame; it must not also answer it.
        if (_confirmOpenedNow) { clicked = false; _confirmOpenedNow = false; }

        var gem = GemCraft.IsGem(item);
        var worn = !gem && IsWorn(hunter, item);
        var x = area.X; var y = area.Y; var w = area.Width;
        _ui.Fill(b, new Rectangle(x - 12, y - 10, w + 24, 300), new Color(0x2A, 0x16, 0x1C, 0xC0));

        _ui.TextBig(b, kind == ScrapKind.Sell ? (gem ? "SELL THIS GEM?" : "SELL THIS ITEM?") : "SALVAGE THIS ITEM?",
                    x, y, Gold, UiTypography.PanelTitle);
        DrawItemIcon(b, item, new Rectangle(x, y + 44, 56, 56));
        var name = gem ? $"{GemCraft.NameOf(item)} {item.ItemLevel}" : ItemNaming.FullName(item);
        _ui.TextBig(b, _ui.ShortenBig(name, w - 70, UiTypography.Body), x + 70, y + 58, RarityColors[(int)item.Rarity], UiTypography.Body);

        var outcome = kind == ScrapKind.Sell
            ? $"IT SELLS FOR {item.SellValue:N0} GLEAM. THIS CANNOT BE UNDONE."
            : $"IT BREAKS DOWN INTO {Forge.Dismantle(item, Tuning)} {MaterialTiers.Name(MaterialTiers.ForRarity(item.Rarity))}. THIS CANNOT BE UNDONE.";
        var ly = DrawWrappedBig(b, outcome, x, y + 116, w, Bone, UiTypography.Secondary) + 6;
        if (item.Gems.Count > 0)
        {
            _ui.TextBig(b, $"ITS {item.Gems.Count} GEM{(item.Gems.Count == 1 ? "" : "S")} COME BACK TO YOU FIRST.", x, ly, Met, UiTypography.Secondary);
            ly += UiTypography.Pitch(UiTypography.Secondary);
        }

        if (worn)
        {
            // The warning that never goes away. No box on this variant — see the remarks.
            _ui.Fill(b, new Rectangle(x, ly, 5, 50), Ember);
            _ui.TextBig(b, "THIS IS ON YOUR HUNTER RIGHT NOW.", x + 18, ly, Ember, UiTypography.Body);
            _ui.TextBig(b, "IT COMES OFF FIRST — THE FIGHT LOSES ITS NUMBERS.", x + 18, ly + 28, Slate, UiTypography.Secondary);
            ly += 62;
        }
        else
        {
            // The suppression box — a wide row, so the label is as clickable as the box.
            var box = new Rectangle(x, ly + 2, 24, 24);
            var row = new Rectangle(x, ly, w, 30);
            _ui.Fill(b, new Rectangle(box.X - 2, box.Y - 2, box.Width + 4, box.Height + 4), Slate * 0.7f);
            _ui.Fill(b, box, new Color(0x14, 0x10, 0x1A));
            if (_confirmSuppress) _ui.Fill(b, new Rectangle(box.X + 5, box.Y + 5, 14, 14), Gold);
            _ui.TextBig(b, "DON'T ASK ME AGAIN BEFORE SELLING OR SALVAGING", box.Right + 12, ly + 4,
                        row.Contains(hit) ? Bone : Slate, UiTypography.Secondary);
            if (UiKit.ClickedIn(row, hit, clicked)) _confirmSuppress = !_confirmSuppress;
            ly += 42;
        }

        var bw = (w - 16) / 2;
        var keep = new Rectangle(x, ly + 8, bw, 60);
        var doIt = new Rectangle(x + bw + 16, ly + 8, bw, 60);
        if (_ui.Button(b, keep, "NO — KEEP IT", hit, clicked)) { _confirm = null; return; }
        if (_ui.Button(b, doIt, kind == ScrapKind.Sell ? "YES — SELL IT" : "YES — SALVAGE IT", hit, clicked))
        {
            // The box commits WITH the confirming click — ticking it and cancelling changes nothing.
            if (_confirmSuppress && !worn) { AskBeforeScrap = false; PrefsDirty = true; }
            _confirm = null;
            if (kind == ScrapKind.Sell) SellNow(hunter, item); else DismantleNow(hunter, item);
        }
    }

    /// <summary>The SALVAGE ALL THE JUNK question, in the bag's foot where its button was.</summary>
    private void DrawJunkQuestion(SpriteBatch b, Hunter hunter, Point hit, bool clicked)
    {
        if (_confirmOpenedNow) { clicked = false; _confirmOpenedNow = false; }

        // The question quotes the REAL payout: a held SALVAGE CHART doubles the yield and will be
        // spent — a question that understates the outcome by half is not a question, it is a trap.
        var junk = JunkOf(hunter);
        var chart = hunter.CharterCount(Charter.Salvage) > 0;
        var mats = junk.Sum(i => Forge.Dismantle(i, Tuning)) * (chart ? 2 : 1);
        var x = UiKit.ContentLeft(BagPanel); var w = BagPanel.Width - UiKit.PadX(BagPanel) * 2;
        _ui.TextBig(b, _ui.ShortenBig($"SALVAGE {junk.Count} JUNK ITEMS FOR {mats} SCRAP?", w, UiTypography.Body),
                    x, BagPanel.Bottom - 152, Gold, UiTypography.Body);
        _ui.TextBig(b, _ui.ShortenBig(chart ? "YOUR SALVAGE CHART DOUBLES IT AND IS USED UP." : "WORN GEAR IS NEVER TOUCHED. NO UNDO.", w, UiTypography.Secondary),
                    x, BagPanel.Bottom - 126, chart ? Gold : Slate, UiTypography.Secondary);
        var bw = (w - 12) / 2;
        if (_ui.Button(b, new Rectangle(x, BagPanel.Bottom - 100, bw, 56), "NO — KEEP", hit, clicked)) { _confirm = null; return; }
        if (_ui.Button(b, new Rectangle(x + bw + 12, BagPanel.Bottom - 100, bw, 56), "YES — SALVAGE", hit, clicked))
        {
            _confirm = null;
            SalvageJunkNow(hunter);
        }
    }

    /// <summary>
    /// Withdraw a question that no longer fits what is on the bench — so it can never be answered by a
    /// click meant for something else, and never lingers invisibly behind another tab.
    /// </summary>
    /// <remarks>
    /// The questions are drawn IN PLACE: SELL / SALVAGE in the SALVAGE tab of the focused item,
    /// CRUSH (and a gem's SELL) in the SOCKET tab, the junk question in the bag's foot. A question whose
    /// place is no longer on screen is simply dropped. Withdrawing never scraps anything.
    /// </remarks>
    private void NormaliseConfirm()
    {
        if (_confirm is not { } ask) return;
        var focus = Target()?.InstanceId;
        var subject = ask.ItemId is null ? null : _inv.FirstOrDefault(i => i.InstanceId == ask.ItemId);
        switch (ask.Kind)
        {
            case ScrapKind.JunkAll:
                break;
            case ScrapKind.CrushGem:
                if (_tab != Tab.Socket || subject is null || ask.ItemId != focus) _confirm = null;
                break;
            default:
                if (subject is null) { _confirm = null; break; }
                if (GemCraft.IsGem(subject)) { if (_tab != Tab.Socket) _confirm = null; }
                else if (_tab != Tab.BreakDown || ask.ItemId != focus) _confirm = null;
                break;
        }
    }

    // ── THE WALLET ───────────────────────────────────────────────────────────────────────────────
    // ── Price lines and icons ────────────────────────────────────────────────────────────────────

    /// <summary>One part of a price: the icon to draw, the name to print, what it costs, what you hold.</summary>
    private readonly record struct Price(string Icon, Color Tint, string Name, long Cost, long Have, bool IsGleam);

    private Price MatPrice(Hunter h, Material m, int cost)
        => new(MatIcon[(int)m], MatColor[(int)m], MaterialTiers.Name(m), cost, h.MaterialOf(m), false);

    private static Price GleamPrice(Hunter h, int cost) => new("currency_gleam", Gold, "GLEAM", cost, h.Gleam, true);

    /// <summary>The wallet's row key for a material — the marks dictionary is keyed by these.</summary>
    private static string MatKey(Material m) => m.ToString().ToLowerInvariant();

    /// <summary>Gleam is abbreviated like the pill (131.9M); materials print whole (12,600), like the wallet.</summary>
    private static string Amount(Price p, long v) => p.IsGleam ? Ab(v) : $"{v:N0}";

    /// <summary>
    /// A price, printed UNDER the button it prices: who pays, and whether you can.
    /// </summary>
    /// <remarks>
    /// Two lines. When a chart will pay: "FREE — YOU HOLD 4 REFORGE CHARTS" and, under it, what it
    /// would cost without one, because the charts run out and the player should not meet the price as
    /// a surprise. Otherwise "COSTS [icon] 110 CORE" with each part coloured by whether you hold it,
    /// and "YOU HOLD 139 CORE" under it — or "— NOT ENOUGH CORE" in ember. The old single line said
    /// "COSTS 110 CORE" whether or not the chart was about to pay, and never said what you held.
    /// </remarks>
    private void DrawPrice(SpriteBatch b, int x, int y, int width, string verb, IReadOnlyList<Price> parts, Charter? chart, int charts)
    {
        var priceText = string.Join(" + ", parts.Select(p => $"{Amount(p, p.Cost)} {p.Name}"));
        if (chart is { } c && charts > 0)
        {
            _ui.TextBig(b, $"FREE — YOU HOLD {charts} {Charters.Name(c)}{(charts > 1 ? "S" : "")}", x, y, Gold, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig($"ONE IS USED UP. WITHOUT A CHART IT COSTS {priceText}.", width, UiTypography.Secondary),
                        x, y + 28, Slate, UiTypography.Secondary);
            return;
        }

        var cx = x;
        _ui.TextBig(b, verb, cx, y, Bone, UiTypography.Body);
        cx += _ui.MeasureBig(verb, UiTypography.Body) + 12;
        for (var i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (i > 0) { _ui.TextBig(b, "+", cx, y, Bone, UiTypography.Body); cx += _ui.MeasureBig("+", UiTypography.Body) + 12; }
            DrawMatIcon(b, p.Icon, p.Tint, new Rectangle(cx, y - 4, 28, 28));
            cx += 34;
            var t = $"{Amount(p, p.Cost)} {p.Name}";
            _ui.TextBig(b, t, cx, y, p.Have >= p.Cost ? Met : Ember, UiTypography.Body);
            cx += _ui.MeasureBig(t, UiTypography.Body) + 12;
        }

        var missing = parts.Where(p => p.Have < p.Cost).Select(p => p.Name).ToList();
        var held = "YOU HOLD " + string.Join(" · ", parts.Select(p => $"{Amount(p, p.Have)} {p.Name}"));
        if (missing.Count > 0) held += " — NOT ENOUGH " + string.Join(" OR ", missing);
        _ui.TextBig(b, _ui.ShortenBig(held, width, UiTypography.Secondary), x, y + 28, missing.Count > 0 ? Ember : Slate, UiTypography.Secondary);
    }

    /// <summary>A material's icon — the real art, or a tinted gem if the file is ever missing.</summary>
    private void DrawMatIcon(SpriteBatch b, string key, Color tint, Rectangle box)
    {
        if (!_ui.Icon(b, key, box)) _ui.Diamond(b, box, tint);
    }

    /// <summary>What each material is FOR, in the wallet's words — the question "what is Core?" answered where Core is.</summary>
    private static string MatUse(Material m) => m switch
    {
        Material.Scrap => "pays for upgrades",
        Material.Essence => "sets gems into sockets",
        Material.Core => "re-rolls rare and epic enchants",
        _ => "legendary re-rolls · greater upgrades",
    };

    private const string GleamUse = "upgrades · stats · the shop";

    /// <summary>Wrap sized text to a width; returns the y just under the last line.</summary>
    private int DrawWrappedBig(SpriteBatch b, string text, int x, int y, int width, Color c, int px)
    {
        foreach (var line in _ui.WrapBig(text, width, px))
        {
            _ui.TextBig(b, line, x, y, c, px);
            y += UiTypography.Pitch(px);
        }
        return y;
    }

    /// <summary>The house abbreviation, shared with the currency pills and the Warren.</summary>
    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    /// <summary>A small solid right-pointing triangle — the before→after arrow, sized ~20px tall.</summary>
    /// <summary>The before → after arrow, at a stated size — the compare draws it larger than a label does.</summary>
    private void Arrow(SpriteBatch b, int x, int cy, Color c, int size = 20)
    {
        var h = size / 2;
        for (var i = 0; i < h; i++) _ui.Fill(b, new Rectangle(x + i, cy - (h - i), 2, (h - i) * 2), c);
    }

    /// <summary>The stat's word. Core owns it — see <see cref="ItemAffixes.StatWord"/> for why.</summary>
    private static string AffixName(AffixStat s) => ItemAffixes.StatWord(s);

    /// <summary>The magnitude in the stat's own unit. Core owns it — see <see cref="ItemAffixes.GrantLabel"/>.</summary>
    private static string AffixVal(ItemAffix a) => ItemAffixes.GrantLabel(a.Stat, a.Magnitude);

    /// <summary>One decimal, for the before → after rows only. Playtest 2026-08-23: "why does UPGRADE
    /// not raise the stats?" — it does, ~2% relative a rung, but "+11.2% → +11.4%" both rounded to
    /// "+11%", so the card showed the same number on both sides of the arrow and the growth looked
    /// like a lie. Everywhere else keeps the round figure; the comparison is where precision earns
    /// its clutter.</summary>
    private static string AffixValPrecise(ItemAffix a) => ItemAffixes.GrantLabelPrecise(a.Stat, a.Magnitude);

    private void DrawDebug(SpriteBatch b)
    {
        var rects = new[] { MaterialStrip, BagPanel, ItemPanel, ForgePanel };
        foreach (var r in rects)
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav FORGE  focus {_focusId ?? "—"}  tab {_tab}", 320, 112, Gold, UiTypography.Secondary);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // THE REVEAL IS A PLACE YOU CAN ACT — not a card you watch go by.
    //
    // Playtest: "I need to hover the items that came out of a chest and READ what they are; and I
    // should be able to sell or salvage them right there." Both halves were true. The card printed one
    // JOINED line of rarity-plus-type words for the whole haul ("EPIC RING"), which is not a name and
    // says nothing about what the piece does; and the only way to act on a drop was to sit out the
    // fade, walk to the Forge, and work out which of the four Rare rings in a rarest-first bag was the
    // new one.
    //
    // What the reveal carries now:
    //   · THE POINTER, so hovering an item raises the SAME ItemTooltip every other screen shows.
    //   · EACH ITEM'S NAME under its icon in its rarity colour, so the common case needs no hover.
    //   · SELL and SALVAGE under each item with the payout printed on the button.
    //   · The "are you sure?" question asked INSIDE the card — the bench's OpenConfirm answers it by
    //     switching to the SALVAGE tab, which is a screen behind this overlay: a question the
    //     player cannot see is worse than no question.
    //   · A real KEEP ALL button. "CLICK TO CLOSE" is an instruction, not a choice.
    //
    // AND THE CLOCK STOPS WHILE THE POINTER IS INSIDE THE CARD. Without that every button here is a
    // trap: you reach for SELL and the card dissolves under the cursor, and the click lands on
    // whatever screen was behind it. See TickReveal.
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The reveal's pointer, in TRUE 1920x1080 — the space the host draws the reveal in.</summary>
    private Point _revealMouse = new(-1, -1);

    /// <summary>A click the host has decided belongs to the reveal's own buttons. Spent by the next draw.</summary>
    private bool _revealClick;

    /// <summary>Every rect on the reveal that answers to a click — rebuilt each draw, read by the host.</summary>
    private readonly List<Rectangle> _revealHots = new();

    /// <summary>The pointer is resting on the settled card, so the fade waits. Read by <see cref="TickReveal"/>.</summary>
    private bool _revealPointerHold;

    /// <summary>The player asked to close — the hold must not keep the card they just dismissed.</summary>
    private bool _revealClosing;

    /// <summary>What this reveal has already disposed of: instance id to the stamp word (SOLD / SALVAGED).</summary>
    private readonly Dictionary<string, string> _revealStamp = new(StringComparer.Ordinal);

    /// <summary>The reveal's OWN sell/salvage question — asked in the card, never by switching tab.</summary>
    private (string ItemId, ScrapKind Kind)? _revealAsk;
    private bool _revealAskSuppress;

    /// <summary>
    /// DEV ONLY: <c>RH_SHOT_HOVER=reveal</c> raises the first item's hover card on the reveal.
    /// </summary>
    /// <remarks>
    /// A hover is the one affordance a screenshot cannot pose by itself, and here it cannot be posed by
    /// coordinates either: WHERE an item sits depends on how many the chest rolled, which is random per
    /// run. Read once, and only while <c>RH_SHOT</c> is set, so a normal run never looks at it. The card
    /// is still placed at <c>RH_SHOT_MOUSE</c>, exactly where a real pointer would put it.
    /// </remarks>
    private static readonly bool DevRevealHover =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
        && Environment.GetEnvironmentVariable("RH_SHOT_HOVER") == "reveal";

    /// <summary>One item's column on the reveal: icon, name, and the two verbs under it.</summary>
    private const int RevealCol = 210;
    private const int RevealIcon = 120;
    private const int RevealCellH = RevealIcon + 116;

    /// <summary>
    /// Does the pointer rest on something the reveal itself would answer?
    /// </summary>
    /// <remarks>
    /// The host asks BEFORE it treats a click as "skip the reveal". A click aimed at SELL that also
    /// skipped the card would be the worst of both: the item is gone and so is the card that said so.
    /// The rects come from the previous frame's draw, which is exact — the layout only moves while the
    /// card is springing open, and no button is drawn until it has stopped.
    /// </remarks>
    public bool RevealWantsClick(Point mouse1920)
    {
        foreach (var r in _revealHots)
            if (r.Contains(mouse1920)) return true;
        return false;
    }

    /// <summary>The host's pointer, and a click it has routed here, in true 1920x1080 space.</summary>
    public void RevealInput(Point mouse1920, bool clicked)
    {
        _revealMouse = mouse1920;
        if (clicked) _revealClick = true;
    }

    /// <summary>Forget everything the LAST reveal was in the middle of. Called wherever one begins.</summary>
    private void ResetRevealActions()
    {
        // THE CLICK GOES TOO. The host routes input at Game1:1677 and ticks the clock further down the
        // same Update, so a click can be handed here on the very frame the reveal's timer runs out —
        // and then no draw ever spends it. Left latched, it would press whatever button happened to sit
        // under the cursor on the NEXT chest opened. (The house bug species, in miniature.)
        _revealClick = false;
        _revealStamp.Clear();
        _revealHots.Clear();
        _revealAsk = null;
        _revealAskSuppress = false;
        _revealPointerHold = false;
        _revealClosing = false;
    }

    /// <summary>
    /// The chest-open reveal: a centred card that dims the screen behind it, shows the grade and the exact
    /// items that popped out (framed by rarity, named, and sellable), then fades.
    /// </summary>
    /// <remarks>
    /// Anticipation is the whole engine of a chest (loot-box design 101), and a one-line footer message is
    /// not a payoff. This is the burst — dark card so the rarity colours pop, grade-coloured edges, item
    /// icons big in the middle. It holds for a beat and fades; opening another chest just refreshes it.
    /// </remarks>
    private void DrawReveal(SpriteBatch b, Hunter hunter)
    {
        if (_revealTimer <= 0f) return;

        var mouse = _revealMouse;
        var click = _revealClick;
        _revealClick = false;               // one click, one answer
        _revealHots.Clear();
        _revealPointerHold = false;

        if (_revealSummary) { DrawRevealSummary(b, hunter, mouse, click); return; }

        // Cascade entries play the same beats, compressed — rarity already bought its extra hold.
        var shakeEnds = _revealBrief ? 0.28f : ShakeEnds;
        var burstEnds = _revealBrief ? 0.46f : BurstEnds;
        var cardIn = _revealBrief ? 0.14f : CardIn;
        var stagger = _revealBrief ? 0.08f : ItemStagger;
        var ringWin = _revealBrief ? 0.30f : 0.5f;

        var t = _revealHold - _revealTimer;                     // seconds SINCE the chest cracked
        // The fade window compresses WITH the beats: at 0.45s a brief entry's card was fully readable
        // for ~0.06s and the materials count-up died mid-number. (Adversarial review, pass four.)
        var fadeWin = _revealBrief ? 0.22f : 0.45f;

        // The card's FINAL rectangle, known before a pixel is drawn, because the pointer test needs it
        // and the fade needs the pointer test. It grows with the haul so three drops never crowd.
        var n = _revealItems.Count;
        var cardW = Math.Max(720, n * RevealCol + 140);
        var full = new Rectangle(960 - cardW / 2, 300, cardW, 470);

        // THE HOLD. Only once the card has FINISHED arriving — the plate landing is not enough. The
        // first cut latched at burstEnds + cardIn, which is the moment the plate lands and the moment
        // the drops START falling in: park the cursor there and the clock stopped on a card with no
        // items, no material count and no buttons, forever (review 2026-08-23, high). The latch now
        // waits for the last drop to settle and for the material count-up to finish, and a CASCADE
        // entry never latches at all — it carries no buttons (`acts` is false while _revealBrief), so
        // a pointer parked mid-screen would stall the whole OPEN ALL for nothing.
        var settled = burstEnds + cardIn
                      + MathF.Max(Math.Max(0, n - 1) * stagger + 0.18f, _revealBrief ? 0.25f : 0.5f);
        var arrived = !_revealBrief && t >= settled;
        var pointerIn = arrived && !_revealClosing && full.Contains(mouse);
        _revealPointerHold = pointerIn;

        // Held, the card is FULLY OPAQUE — not frozen half-faded at whatever alpha the pointer arrived
        // at. The clock is stopped, not rewound, so letting go resumes exactly where it stopped.
        var fade = pointerIn ? 1f : Math.Clamp(_revealTimer / fadeWin, 0f, 1f);
        var grade = RarityColors[(int)_revealGrade];

        // Dim the Forge behind, deepening as the chest works itself up. The scrim arriving at full
        // strength on frame one is what made the old reveal read as a dialog rather than an event.
        var dim = Math.Clamp(t / (shakeEnds * 0.6f), 0f, 1f);
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0, 0, 0, (int)(215 * dim * fade)));

        // A cascade says where it is and how to leave — the skip must be visible from the first frame.
        if (_revealChestCount > 1)
        {
            _ui.TextCenterBig(b, $"CHEST {_revealIndex} OF {_revealChestCount}", 960, 214,   // ui-page-ok: host chrome, canvas space
                              Slate * MathF.Max(fade, 0.6f), UiTypography.Secondary);
            _ui.TextCenterBig(b, "CLICK TO SKIP TO THE HAUL", 960, 984, Slate * 0.8f, UiTypography.Secondary);   // ui-page-ok: host chrome, canvas space
        }

        // ── BEAT 1 · THE CHEST RATTLES ────────────────────────────────────────────────────────────
        if (t < shakeEnds)
        {
            var p = Math.Clamp(t / shakeEnds, 0f, 1f);

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
            _ui.TextCenterBig(b, _revealTitle, 960, 340,   // ui-page-ok: host chrome, canvas space
                grade * (0.4f + 0.6f * p), UiTypography.PanelTitle);
        }

        // ── BEAT 2 · THE BURST ────────────────────────────────────────────────────────────────────
        // A ring, PLOTTED rather than drawn from an asset: a circle of short segments whose radius
        // sweeps out and whose alpha falls away. Nothing to author, nothing to load, and retiming it is
        // editing a number — the same reasoning as the Trait screen's unlock flourish.
        if (t >= shakeEnds && t < shakeEnds + ringWin)
        {
            var p = (t - shakeEnds) / ringWin;
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
                _ui.Fill(b, UiKit.OverlayScrim, Color.White * (0.35f * (1f - p / 0.25f)));
        }

        // ── BEAT 3 · THE CARD ─────────────────────────────────────────────────────────────────────
        if (t < burstEnds) return;

        // Springs open with an overshoot, then settles. A card that simply appears is information; a
        // card that arrives is a reward.
        var cp = Math.Clamp((t - burstEnds) / cardIn, 0f, 1f);
        var scale = cp >= 1f ? 1f : 1f + 0.18f * MathF.Sin(cp * MathF.PI) - 0.35f * (1f - cp);
        var card = Grow(full, scale);

        _ui.PanelQuiet(b, card);
        _ui.Fill(b, UiKit.PanelInner(card), grade * (0.10f * fade));

        if (cp < 0.6f) return;      // the contents wait for the frame to stop moving

        _ui.TextCenterBig(b, _revealTitle, card.Center.X, card.Y + 34,
            grade * fade, UiTypography.PanelTitle);

        // A question takes the card's body. Its subject is re-resolved by id every frame: if the item
        // has left the bag under it, the question simply has nothing left to ask.
        if (_revealAsk is { } ask && _inv.FirstOrDefault(i => i.InstanceId == ask.ItemId) is { } subject)
        {
            DrawRevealQuestion(b, hunter, subject, ask.Kind,
                               new Rectangle(full.X + 40, full.Y + 78, full.Width - 80, full.Height - 118),
                               mouse, click);
            return;
        }
        _revealAsk = null;

        // The items that popped, big and framed by their own rarity — one at a time, each dropping the
        // last few pixels into place so the eye is led along the row instead of at all of it at once.
        // A CASCADE ENTRY GETS NO BUTTONS: it is on screen for about a second, and a button that brief
        // is a misclick waiting to happen. The summary at the end of the cascade carries them instead.
        var acts = !_revealBrief && cp >= 1f;
        var cellsTop = full.Y + 78;
        ItemInstance? hover = null;
        for (var i = 0; i < n; i++)
        {
            var ip = Math.Clamp((t - burstEnds - cardIn - i * stagger) / 0.18f, 0f, 1f);
            if (ip <= 0f) continue;
            var drop = (int)(-40f * (1f - ip) * (1f - ip));
            var cell = new Rectangle(960 - n * RevealCol / 2 + i * RevealCol, cellsTop, RevealCol - 10, RevealCellH);
            if (DrawRevealCell(b, hunter, _revealItems[i], cell, drop, fade, acts && ip >= 1f, mouse, click) is { } h)
                hover = h;
        }

        if (DevRevealHover && hover is null && n > 0)
            hover = _inv.FirstOrDefault(i => i.InstanceId == _revealItems[0].InstanceId);

        if (n == 0)
            _ui.TextCenter(b, "SOLD ON SIGHT — YOUR LOOT FILTER TOOK IT.", card.Center.X, cellsTop + 90, Slate * fade);

        // Materials COUNT UP rather than landing finished. The number is the same; watching it arrive is
        // the difference between being told what you got and seeing it paid out.
        var mp = Math.Clamp((t - burstEnds - cardIn) / (_revealBrief ? 0.25f : 0.5f), 0f, 1f);
        // A gift pays no materials; "+0 MATERIALS" under a gift reads as a shortfall, so the line is
        // simply absent when there is nothing to count up to.
        if (_revealMaterials > 0)
            _ui.TextCenterBig(b, $"+{(int)MathF.Round(_revealMaterials * mp)} MATERIALS", card.Center.X, full.Y + 330,
                Gold * fade, UiTypography.Body);

        if (acts && _revealChestCount <= 1)
        {
            var keep = new Rectangle(full.Center.X - 180, full.Y + 372, 360, 56);
            _revealHots.Add(keep);
            if (_ui.Button(b, keep, "CLOSE", mouse, click)) AdvanceReveal();
        }

        // LAST, so nothing is drawn over it — and BESIDE THE CARD rather than under the pointer. At the
        // pointer it lands squarely on the item's own SELL and SALVAGE buttons: you hover a drop to
        // decide, and the thing helping you decide covers the two things you decided between.
        if (hover is not null)
            ItemTooltip.Draw(_ui, b, hover, hunter, new Point(full.Right - 16, mouse.Y - 60),
                             new Rectangle(0, 0, 1920, 1080));
    }

    /// <summary>
    /// One item on the reveal: its picture, its NAME, and the two verbs with their payout on them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by the single-chest card and the OPEN ALL summary deliberately — two grids drawing two
    /// subsets of "what you can do with this drop" is how one of them ends up without a SELL button.
    /// </para>
    /// <para>
    /// <b>EVERY ITEM IS RE-RESOLVED BY ID against the live bag before anything is drawn.</b> The reveal
    /// holds a SNAPSHOT of what dropped, and TIRELESS FORGE (AutoMergeAll) may have fused some of it
    /// into better pieces before the card was ever on screen — so a button here could otherwise sell an
    /// object that no longer exists. What is gone is dimmed and stamped instead of being actionable.
    /// </para>
    /// </remarks>
    /// <returns>The item under the pointer, so the caller can raise its tooltip above everything else.</returns>
    private ItemInstance? DrawRevealCell(SpriteBatch b, Hunter hunter, ItemInstance snapshot, Rectangle cell,
                                         int drop, float fade, bool acts, Point mouse, bool click)
    {
        var live = _inv.FirstOrDefault(i => i.InstanceId == snapshot.InstanceId);
        var shown = live ?? snapshot;
        var rc = RarityColors[(int)shown.Rarity];
        var icon = new Rectangle(cell.X + (cell.Width - RevealIcon) / 2, cell.Y + drop, RevealIcon, RevealIcon);

        DrawItemIcon(b, shown, icon);
        if (live is null) _ui.Fill(b, icon, new Color(0x0A, 0x08, 0x10, 0xB4));   // gone: dim the picture

        // THE NAME, under the picture, in its rarity colour. The card used to join every item into one
        // "EPIC RING   RARE BLADE" line, which names a rarity and a shape and no item.
        _ui.TextCenterBig(b, _ui.ShortenBig(ItemNaming.FullName(shown), cell.Width, UiTypography.Secondary),
                          cell.Center.X, cell.Y + 128, (live is null ? Dim : rc) * fade, UiTypography.Secondary);

        if (live is null)
        {
            // SOLD / SALVAGED by the buttons below; MERGED by TIRELESS FORGE before the card opened.
            var stamp = _revealStamp.TryGetValue(snapshot.InstanceId, out var w) ? w : "MERGED";
            _ui.TextCenterBig(b, stamp, cell.Center.X, cell.Y + 160, Slate * fade, UiTypography.Body, TextFace.Strong);
            return null;
        }

        var hover = icon.Contains(mouse) ? live : null;
        if (!acts) return hover;

        var sell = new Rectangle(cell.X, cell.Y + 154, cell.Width, 38);
        _revealHots.Add(sell);
        if (_ui.Button(b, sell, $"SELL FOR {live.SellValue:N0} GLEAM", mouse, click))
            AskOrScrap(hunter, live, ScrapKind.Sell);

        if (Gear.IsWearable(live))
        {
            var tier = MaterialTiers.ForRarity(live.Rarity);
            var salvage = new Rectangle(cell.X, cell.Y + 196, cell.Width, 38);
            _revealHots.Add(salvage);
            if (_ui.Button(b, salvage, $"SALVAGE FOR {Forge.Dismantle(live, Tuning)} {MaterialTiers.Name(tier)}", mouse, click))
                AskOrScrap(hunter, live, ScrapKind.Salvage);
        }
        else
        {
            // A GEM has no salvage — it is set into gear or sold, and saying so is better than a greyed
            // button whose reason lives in someone's head.
            _ui.TextCenterBig(b, _ui.ShortenBig("SET IT INTO GEAR AT THE FORGE", cell.Width, UiTypography.Secondary),
                              cell.Center.X, cell.Y + 202, Slate * fade, UiTypography.Secondary);
        }
        return hover;
    }

    /// <summary>
    /// SELL or SALVAGE from the reveal — through the question when the preference asks, straight through
    /// when the player has turned it off.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT <see cref="Sell"/> / <see cref="Dismantle"/>: those raise the question by
    /// switching the bench to the SALVAGE tab, which is a screen behind this overlay — the player
    /// would be answering a question they cannot see. A revealed item is never worn, so the always-ask
    /// worn variant cannot apply here and the preference is the only gate.
    /// </remarks>
    private void AskOrScrap(Hunter hunter, ItemInstance item, ScrapKind kind)
    {
        var reason = Forge.CheckEligible(item);
        if (reason != IneligibleReason.Eligible) { Say(Forge.Explain(reason), Ember); return; }
        if (AskBeforeScrap) { _revealAsk = (item.InstanceId, kind); _revealAskSuppress = false; return; }
        DoRevealScrap(hunter, item, kind);
    }

    /// <summary>Do it, and remember the word to stamp on the cell it came out of.</summary>
    private void DoRevealScrap(Hunter hunter, ItemInstance item, ScrapKind kind)
    {
        _revealStamp[item.InstanceId] = kind == ScrapKind.Sell ? "SOLD" : "SALVAGED";
        if (kind == ScrapKind.Sell) SellNow(hunter, item);
        else DismantleNow(hunter, item);
    }

    /// <summary>
    /// The reveal's own "are you sure?", drawn inside the card in the haul's place.
    /// </summary>
    /// <remarks>
    /// The same shape as the bench's <see cref="DrawScrapQuestion"/> — KEEP first so a reflex click is
    /// safe, the destructive verb named rather than a generic CONFIRM, and a "don't ask me again" box
    /// that commits WITH the confirming click (ticking it and cancelling changes nothing). The whole
    /// card counts as a hot rect while it is up, so a click that misses both buttons withdraws the
    /// question instead of skipping the reveal out from under it.
    /// </remarks>
    private void DrawRevealQuestion(SpriteBatch b, Hunter hunter, ItemInstance item, ScrapKind kind,
                                    Rectangle area, Point mouse, bool click)
    {
        _revealHots.Add(area);
        _ui.Fill(b, area, new Color(0x2A, 0x16, 0x1C, 0xEE));

        var x = area.X + 26; var w = area.Width - 52; var y = area.Y + 18;
        _ui.TextBig(b, kind == ScrapKind.Sell ? "SELL THIS?" : "SALVAGE THIS?", x, y, Gold, UiTypography.PanelTitle);
        DrawItemIcon(b, item, new Rectangle(x, y + 42, 56, 56));
        _ui.TextBig(b, _ui.ShortenBig(ItemNaming.FullName(item), w - 70, UiTypography.Body), x + 70, y + 56,
                    RarityColors[(int)item.Rarity], UiTypography.Body);

        var outcome = kind == ScrapKind.Sell
            ? $"IT SELLS FOR {item.SellValue:N0} GLEAM. THIS CANNOT BE UNDONE."
            : $"IT BREAKS DOWN INTO {Forge.Dismantle(item, Tuning)} {MaterialTiers.Name(MaterialTiers.ForRarity(item.Rarity))}. THIS CANNOT BE UNDONE.";
        _ui.TextBig(b, _ui.ShortenBig(outcome, w, UiTypography.Secondary), x, y + 112, Bone, UiTypography.Secondary);

        var answered = false;
        var box = new Rectangle(x, y + 144, 24, 24);
        var row = new Rectangle(x, y + 140, w, 32);
        _ui.Fill(b, new Rectangle(box.X - 2, box.Y - 2, box.Width + 4, box.Height + 4), Slate * 0.7f);
        _ui.Fill(b, box, new Color(0x14, 0x10, 0x1A));
        if (_revealAskSuppress) _ui.Fill(b, new Rectangle(box.X + 5, box.Y + 5, 14, 14), Gold);
        _ui.TextBig(b, "DON'T ASK ME AGAIN BEFORE SELLING OR SALVAGING", box.Right + 12, y + 146,
                    row.Contains(mouse) ? Bone : Slate, UiTypography.Secondary);
        if (UiKit.ClickedIn(row, mouse, click)) { _revealAskSuppress = !_revealAskSuppress; answered = true; }

        var bw = (w - 20) / 2;
        var keep = new Rectangle(x, y + 186, bw, 56);
        var doIt = new Rectangle(x + bw + 20, y + 186, bw, 56);
        // Registered as their own hot rects rather than trusting the panel to contain them — the host
        // routes a click to the reveal only when it lands on a registered rect.
        _revealHots.Add(keep); _revealHots.Add(doIt);
        if (_ui.Button(b, keep, "NO — KEEP IT", mouse, click)) { _revealAsk = null; return; }
        if (_ui.Button(b, doIt, kind == ScrapKind.Sell ? "YES — SELL IT" : "YES — SALVAGE IT", mouse, click))
        {
            if (_revealAskSuppress) { AskBeforeScrap = false; PrefsDirty = true; }
            _revealAsk = null;
            DoRevealScrap(hunter, item, kind);
            return;
        }
        if (click && !answered && area.Contains(mouse)) _revealAsk = null;   // a miss withdraws, never scraps
    }

    /// <summary>The haul, all of it, holding until a click — the review the cascade must never skip.</summary>
    private void DrawRevealSummary(SpriteBatch b, Hunter hunter, Point mouse, bool click)
    {
        var fade = _revealFrozen ? 1f : Math.Clamp(_revealTimer / 0.35f, 0f, 1f);
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0, 0, 0, (int)(215 * fade)));

        var n = _revealAll.Count;
        const int PerRow = 5, MaxShown = 10;
        var shown = Math.Min(n, MaxShown);
        var rows = Math.Max(1, (shown + PerRow - 1) / PerRow);
        // 1180 wide keeps every row count above the 1.30 aspect line, so UiKit.Panel never swaps the
        // frame art as the haul grows (the trap the VAULT and the settings panel both hit before).
        var height = rows * RevealCellH + 286;
        var panel = new Rectangle(960 - 590, 540 - height / 2, 1180, height);
        _ui.PanelQuiet(b, panel);

        var best = n > 0 ? RarityColors[_revealAll.Max(i => (int)i.Rarity)] : Bone;
        _ui.TextCenterBig(b, $"{_revealChestCount} CHESTS OPENED", panel.Center.X, panel.Y + 34,
                          best * fade, UiTypography.PanelTitle);

        // The tally, rarest first — countable without reading ten icons.
        var tally = _revealAll.GroupBy(i => i.Rarity).OrderByDescending(g => (int)g.Key)
                              .Select(g => $"{g.Count()} {RarityNames[(int)g.Key]}");
        _ui.TextCenterBig(b, n == 0 ? "EVERYTHING WAS SOLD ON SIGHT (YOUR LOOT FILTER)." : string.Join("  ·  ", tally),
                          panel.Center.X, panel.Y + 78, (n == 0 ? Slate : Bone) * fade, UiTypography.Secondary);

        var cellsTop = panel.Y + 116;
        var cellsBottom = cellsTop + rows * RevealCellH;
        ItemInstance? hover = null;
        var hoverCell = Rectangle.Empty;   // anchors the hover card BESIDE the item, never over its buttons

        if (_revealAsk is { } ask && _inv.FirstOrDefault(i => i.InstanceId == ask.ItemId) is { } subject)
        {
            // The question's own layout needs 260 px (18 + 186 + 56). With one row of drops the cells
            // are only 236 px tall, so the two answer buttons hung BELOW the rect — and the rect is
            // what RevealWantsClick tests, so the lower half of YES/NO dismissed the summary instead
            // of answering it (review 2026-08-23, high). RevealCellH + 64 = 300 covers the layout and
            // still stops short of KEEP ALL at cellsTop + 308.
            DrawRevealQuestion(b, hunter, subject, ask.Kind,
                               new Rectangle(panel.Center.X - 340, cellsTop, 680,
                                             Math.Max(RevealCellH + 64, cellsBottom - cellsTop)),
                               mouse, click);
        }
        else
        {
            _revealAsk = null;
            // Rarest first — each in its own rarity frame, named, and sellable where it lies.
            var order = _revealAll.OrderByDescending(i => (int)i.Rarity).Take(shown).ToList();
            for (var i = 0; i < order.Count; i++)
            {
                var row = i / PerRow;
                var inRow = Math.Min(PerRow, order.Count - row * PerRow);
                var x0 = panel.Center.X - inRow * RevealCol / 2;
                var cell = new Rectangle(x0 + i % PerRow * RevealCol, cellsTop + row * RevealCellH,
                                         RevealCol - 10, RevealCellH);
                if (i == 0) hoverCell = cell;
                if (DrawRevealCell(b, hunter, order[i], cell, 0, fade, _revealFrozen, mouse, click) is { } h)
                { hover = h; hoverCell = cell; }
            }
            if (DevRevealHover && hover is null && order.Count > 0)
                hover = _inv.FirstOrDefault(i => i.InstanceId == order[0].InstanceId);
            if (n > shown)
                _ui.TextCenterBig(b, $"+{n - shown} MORE ITEMS — THEY ARE ALL IN YOUR BAG", panel.Center.X,
                                  cellsBottom + 4, Slate * fade, UiTypography.Secondary);
        }

        _ui.TextCenterBig(b, $"+{_revealAllMats} MATERIALS"
                             + (_revealMergedCount > 0 ? $"   ·   AUTO-MERGE FUSED {_revealMergedCount}x" : ""),
                          panel.Center.X, cellsBottom + 36, Gold * fade, UiTypography.Body);

        // A REAL BUTTON, replacing "CLICK TO CLOSE". Clicking anywhere else still closes it, for everyone
        // who does not care — but the way out is now a thing you can see and press.
        var keepAll = new Rectangle(panel.Center.X - 190, cellsBottom + 72, 380, 56);
        _revealHots.Add(keepAll);
        if (_ui.Button(b, keepAll, "CLOSE", mouse, click)) AdvanceReveal();

        // Anchored to the hovered CELL, not to the pointer. At the pointer the card lands on the item's
        // own SELL and SALVAGE buttons — the two things it is helping you choose between.
        if (hover is not null)
            ItemTooltip.Draw(_ui, b, hover, hunter, new Point(hoverCell.Right, hoverCell.Y),
                             new Rectangle(0, 0, 1920, 1080));
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

    /// <summary>
    /// The tint each rarity's frame is drawn with, so the ring itself carries rarity.
    /// </summary>
    /// <remarks>
    /// <b>THE FIVE RARITY FRAME TEXTURES ARE THE SAME PICTURE.</b> ui_frame_rarity_common through
    /// _legendary are five files with one warm gold-and-bone ring between them, drawn untinted — so
    /// "rarity frame" was a name, not a signal, and rarity's only real mark on an item was a few pixels
    /// of coloured bar beside it. The largest shape in every cell said nothing.
    ///
    /// Tinting works here only because the art's brightest pixels are near-white (sampled 239,221,199),
    /// not saturated gold: a multiply against a near-white ring can reach any hue, while a multiply
    /// against actual gold could only ever darken toward brown. Legendary keeps White, which is what
    /// makes it the one that still reads as metal.
    ///
    /// Indexed by <c>(int)Rarity</c>, in the same order as FrameKey.
    /// </remarks>
    private static readonly Color[] FrameTint =
    {
        new(0xD2, 0xCE, 0xC2),   // Common — neutral bone, so it reads as "no claim"
        new(0x92, 0xDE, 0x9E),   // Uncommon
        new(0x82, 0xB8, 0xFF),   // Rare
        new(0xD2, 0x8C, 0xE2),   // Epic
        Color.White,             // Legendary — the art as authored
    };

    /// <summary>Draw an item's frame+glyph (or a rarity-coloured fallback). Shared with the Character screen.</summary>
    public void DrawItemIcon(SpriteBatch b, ItemInstance item, Rectangle box)
    {
        var frame = _ui.Assets.Get(FrameKey[(int)item.Rarity]);
        if (frame is not null) b.Draw(frame, box, FrameTint[(int)item.Rarity]);

        // A STAT GEM wears the MEDALLION OF ITS STAT — a fist for damage, a star for critical chance.
        // Six of them shipped in assets/art/ItemsLoot/glyphs/affix and nothing in the game ever asked
        // for one, so every gem everywhere was the same coloured diamond and told the player nothing.
        // Fixing it HERE fixes it on every gem surface at once — bag row, socket box, reveal card,
        // question, tooltip — because they all draw their icon through this one method.
        if (GemCraft.IsGem(item))
        {
            var inset = frame is null ? 4 : Math.Max(6, box.Width * 18 / 100);
            var inner = new Rectangle(box.X + inset, box.Y + inset, box.Width - 2 * inset, box.Height - 2 * inset);
            var stat = GemCraft.StatOf(item);
            if (_ui.Assets.Get(GemIconKey(stat)) is { } medallion) b.Draw(medallion, inner, Color.White);
            else _ui.Diamond(b, inner, GemColor(stat));   // the pre-art fallback, kept: a missing PNG must not blank the slot
            return;
        }

        // The icon: the item's id-stable face (see ItemArt), drawn INSET so the ornate rarity frame
        // stays visible around it. Then the Source gem (top-left) and enchant glyph (bottom-right)
        // as small modular overlays — the package_05 composition order.
        var glyph = ItemArt(item);
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

    /// <summary>The medallion asset key for a gem's stat — aliased in <see cref="AssetLibrary"/>.</summary>
    private static string GemIconKey(AffixStat s) => s switch
    {
        AffixStat.Damage => "gem_damage", AffixStat.Health => "gem_health",
        AffixStat.SkillRate => "gem_skillrate", AffixStat.Haul => "gem_haul",
        AffixStat.Crit => "gem_crit", _ => "gem_defense",
    };

    /// <summary>Each gem stat's stone colour — hue plus the NAME carried elsewhere, never hue alone.</summary>
    private static Color GemColor(AffixStat s) => s switch
    {
        AffixStat.Damage => new Color(0xD6, 0x48, 0x5C),
        AffixStat.Health => new Color(0x6E, 0xC8, 0x7A),
        AffixStat.SkillRate => new Color(0x74, 0xC6, 0xE8),
        AffixStat.Haul => UiInk.Accent,
        AffixStat.Crit => new Color(0xC8, 0x8A, 0xE0),
        _ => new Color(0x8A, 0x96, 0xA8),
    };
}
