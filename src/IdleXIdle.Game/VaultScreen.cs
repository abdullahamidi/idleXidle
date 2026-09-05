using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;

using IdleXIdle.Core.Progression;
namespace IdleXIdle.Game;

/// <summary>
/// The VAULT: every unopened chest you hold. The chest itself is the button now.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, pass four: <i>"VAULT ekranını sadeleştirelim sağ taraftaki paneli sil sadece kutular
/// kalsın. Kutuların sağ üstüne bir soru işareti koy dropları o butona hover edince görelim. Chestin
/// üstüne tıklayınca da açılsın."</i> — delete the right panel, keep only the boxes; a "?" in each
/// box's corner shows the drops on hover; clicking the chest opens it.
/// </para>
/// <para>
/// So the screen is one grid. Each card is a clickable chest: hovering lifts it a step (a 6% scale, a
/// brightened edge, a lit OPEN chip — value-step changes, never a new hue, per the art bible's chrome
/// rules), and the click IS the open.
/// </para>
/// <para>
/// <b>UX V2 P1.8: the dossier is ON the card.</b> It used to live behind a 32 px glass in the card's
/// corner, on a 0.4 s delay, in a 470 px tooltip — which is exactly the "huge hover tooltip containing
/// full documentation" the brief rejects, and it meant the screen answered "what is waiting for me?"
/// only for a player who knew to hover. Four cards of 884x349 instead of eighteen of 270x210: the same
/// pile, the same facts, none of them hidden. Everything the tooltip said, the region's blurb
/// included, is on the card now, so nothing was lost by deleting it.
/// </para>
/// <para>
/// <b>This screen states facts, never outcomes.</b> Contents are rolled at OPEN, so everything here is
/// a floor or a range — the text comes from <see cref="ChestDossier"/>, which derives every line from
/// the same tuning the roll reads, so this screen cannot drift into lying.
/// </para>
/// <para>
/// <b>UI polish P2: the density profile.</b> Every row pitch, chip, icon box, pad and gap on this
/// screen is read from <see cref="UiMetrics"/> (a control at the full factor, a spacing at half), and
/// every vertical rhythm is DERIVED from the height that is there: the grid holds as many whole rows
/// of cards as fit above the page's bottom margin — two at 100 %, one at 125 and 150 %, where a card
/// that has to hold Body 33 dossier lines beside a chest is taller than half the page — and the pile
/// scrolls by rows with a scrollbar in the frame's own margin, so no card moves to make room for it.
/// At 100 % nothing here moved: the numbers below are the 100 % values the captures were checked
/// against, and the profile's arithmetic reproduces them exactly.
/// </para>
/// </remarks>
public sealed class VaultScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;

    /// <summary>The same ramp the Forge uses. A grade must read as one colour everywhere in the game.</summary>
    private static readonly Color[] RarityColors =
        [Bone, new Color(0x6E, 0xC8, 0x7A), new Color(0x4A, 0x90, 0xD9), new Color(0x8B, 0x3F, 0x82), Gold];

    /// <summary>Also shared with the fight screen, for the same reason.</summary>
    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x9A, 0x7A, 0xD8), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };

    private readonly UiKit _ui;

    public VaultScreen(UiKit ui) => _ui = ui;

    /// <summary>
    /// The game's ONE item renderer (ForgeScreen.DrawItemIcon), lent by the host so a trader's offer
    /// wears the same art, frame and gem it wears in GEAR and the FORGE. Null draws a rarity swatch.
    /// </summary>
    public Action<SpriteBatch, ItemInstance, Rectangle>? DrawItem { get; set; }

    private int _cursor;     // which SORTED chest a click chose — the host reads it via SelectedIndex
    private int _scroll;     // first visible card, in steps of a row
    private float _anim;

    // Hover state, resolved in Update and read by Draw the same frame. The EASE of it lives in
    // UiMotion (keyed per card, asked in Draw the way UiKit.Button asks for its own), so Reduced
    // Motion collapses it to the same end state without a second switch here.
    private int _hoverIdx = -1;    // card under the pointer, or -1

    // ── UI POLISH P3–P6: the states, the open burst, the cues (brief §22–§38, §56–§60, §86). ──
    //
    // THE CHEST IS THE BUTTON, so the card has to do what UiKit.Button does for itself: HOVER eases
    // in, PRESSED drops the plate 2 px and darkens it for as long as the button is held, and the
    // click — the host's click is the PRESS edge (Game1._clicked), the same edge every button in
    // the game fires on — starts the open BURST on that frame. The host removes the chest in the
    // same Update, so the burst is drawn from rectangles remembered at the click, not from a card
    // that may no longer be there: a grade-coloured glow on the card's edge that fades, and a ring
    // that sweeps out from where the chest was. Epic and up add ONE echo ring inside the same
    // window — never a longer wait (§56–§60). Nothing here loops; a pulse is armed by an event in
    // Update and only read in Draw.

    /// <summary>One card of the burst: where it was, where its chest was, and its grade.</summary>
    private readonly List<(Rectangle Card, Rectangle Art, Rarity Grade)> _burst = new(8);

    private static readonly int BurstKey = HashCode.Combine("vault", "open");
    private static readonly int TallyKey = HashCode.Combine("vault", "tally");
    private static readonly int CardKeyBase = HashCode.Combine("vault", "card");
    private static readonly int PasteKey = HashCode.Combine("vault", "paste");

    /// <summary>How long the open burst runs — the brief's REWARD band, through the one vocabulary.</summary>
    public const float OpenBurstSeconds = UiMotion.Reward;

    /// <summary>Where, in the burst, the Epic-and-up echo ring starts (a fraction of the window).</summary>
    public const float EchoStart = 0.4f;

    /// <summary>The pile's size last frame, so a pile that just SHRANK can light the tally once.</summary>
    private int _lastCount = -1;

    private string? _cue;

    /// <summary>
    /// The sound cue for an open, cleared by reading — the host owns audio (the same shape as
    /// <c>TraitsScreen.ConsumeCue</c>).
    /// </summary>
    /// <remarks>
    /// <c>sfx_chest_open</c> for every open; a chest of Epic or better grade — or an OPEN ALL whose
    /// best chest is — adds <c>sfx_chest_rare</c> in the SAME frame, so the two names come back
    /// joined by a comma (<c>"sfx_chest_open,sfx_chest_rare"</c>) and the host plays each. OPEN
    /// ALL sets the pair once for the whole pile, not once per chest. Set at the semantic moment
    /// (the click that opens), never from Draw.
    /// </remarks>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    /// <summary>
    /// The cue, or the pair, for a chest of this grade — the grade is known before the roll, so the
    /// sound is chosen at the click rather than after the contents come back.
    /// </summary>
    /// <remarks>
    /// Public because it is the contract the host wires to, and the one a test can hold: the names
    /// are the audio vocabulary's own (<c>assets/audio/ui</c>), not strings invented here.
    /// </remarks>
    public static string CueFor(Rarity grade) =>
        grade >= Rarity.Epic ? "sfx_chest_open,sfx_chest_rare" : "sfx_chest_open";

    // DEV: PRESSED lasts as long as a thumb and the open burst is over in a third of a second, while
    // the rig shoots frame 60 with no mouse button and no clock of its own — so a capture can only
    // prove either of them if the screen HOLDS them. RH_SHOT_VAULT_POSE, read once and only under
    // RH_SHOT (the house rule for a screen's own dial):
    //
    //   hold             the left button is treated as held ALL frame, so whatever the rig's posed
    //                    cursor (RH_SHOT_PAGE_MOUSE=x,y) is over draws in its PRESSED state — a card,
    //                    PASTE A CODE, and inside the chest filter its mini buttons and slot cells
    //   open:<n>[@t]     the burst of the n-th visible card, frozen t seconds in (0.08 when unsaid)
    //   openall[@t]      the OPEN ALL burst over every visible card, frozen the same way
    //   ,reduced         appended to any of the above, turns Reduced Motion on for the capture
    //
    //   RH_SHOT_VAULT_POSE=open:1@0.22 bash tools/asset-pipeline/capture.sh vault build/shots/x.png
    //   RH_SHOT_VAULT_POSE=hold RH_SHOT_PAGE_MOUSE=1440,410 bash tools/asset-pipeline/capture.sh vault …
    //   RH_SHOT_VAULT_POSE=open:0@0.14,reduced RH_SHOT_UISCALE=150 …
    //
    // `hold` poses WHERE the cursor already is rather than naming a control, so one dial photographs
    // every pressed state on the screen and no control needs a dial of its own.
    //
    // The pose does not open anything — the host is not told — so the burst is photographed over
    // the card it came from, which is exactly what a STACK looks like at play (the pile stays, one
    // shorter); a single chest's card is gone under its burst. It freezes the whole opened FRAME,
    // the tally's answer included, because at play those are one instant.
    //
    // REDUCED MOTION is the one state the rig cannot reach on its own: the accessibility settings are
    // read from the player's prefs file, which a shot run deliberately skips, so UiMotion.Reduced is
    // false in every capture ever taken. §106 asks for Reduced Motion AT 150 % specifically, and this
    // screen's reward beat is exactly what that setting changes — so `,reduced` re-asserts the flag
    // each Update (the host rewrites it from the real setting every frame, before any screen runs).
    // Guarded by RH_SHOT, and it is the only global this screen ever writes.
    private bool _devPosePending = Environment.GetEnvironmentVariable("RH_SHOT") is not null;
    private float? _devBurstT;
    private bool _devHeld;
    private bool _devReduced;

    /// <summary>The left button is down — the real one, or the rig's posed <c>hold</c>.</summary>
    private bool Held => UiKit.MouseHeld || _devHeld;

    /// <summary>Keys this screen is still easing — asked while hot, and while cooling back down.</summary>
    private readonly HashSet<int> _cooling = new(16);

    /// <summary>
    /// A hover ease that does not restart itself: 0 at rest, easing in while hot and out again after.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UiMotion.Ease"/> DROPS a key the moment it settles at 0 (a rested control is meant to
    /// cost nothing), and an absent key starts at the target's opposite — so asking it about something
    /// already at rest hands back 0.93 and fades to 0 again, forever. Six frames on, six frames off: a
    /// card with no pointer near it brightened its edge, lit its OPEN chip gold and grew its chest 6 %,
    /// ten times a second. Continuous idle motion is what the brief forbids outright (§30 animate
    /// CHANGE, §64 no constant flashing) and it drowns the hover it is supposed to be.
    /// </para>
    /// <para>
    /// So ask only while there is something to ask about: a hot control eases in, and it keeps being
    /// asked until the fade out reaches 0 — after which the key is at rest and is not asked again. The
    /// root cause is in the shared vocabulary (every <see cref="UiKit.Button"/> in the game does the
    /// same, this screen's toolbar included); this keeps the VAULT's own controls honest until that is
    /// fixed, and costs nothing once it is.
    /// </para>
    /// </remarks>
    private float Lift(int key, bool hot)
    {
        if (hot)
        {
            _cooling.Add(key);
            return UiMotion.Ease(key, 1f);
        }
        if (!_cooling.Contains(key)) return 0f;
        var v = UiMotion.Ease(key, 0f);
        if (v <= 0f) _cooling.Remove(key);
        return v;
    }

    // ── THE WANDERING TRADER + SHARE CODES — the two future-content directions the designer kept
    //    (2026-08-20). Both live in the vault: the room where things arrive from outside. ─────────
    private static readonly Color Ember = UiInk.Danger;

    /// <summary>Host-fed. The stall needs a wallet to price against and a bag to tooltip with.</summary>
    public Hunter? Hunter { get; set; }

    /// <summary>
    /// Host-fed from <c>ForgeScreen.AutoSellFloor</c>: gear at or below this grade is sold the moment
    /// it drops. Null when the player has not bought that trait.
    /// </summary>
    /// <remarks>
    /// The vault cannot know this on its own — the trait is a Memory Dust purchase the Forge reads —
    /// and OPEN ALL is exactly the moment it matters, because it is the one click that can sell a
    /// dozen pieces before the player sees them. Stated before the click, never as a confirmation.
    /// </remarks>
    public Rarity? AutoSellFloor { get; set; }

    /// <summary>Host-fed from <c>ForgeScreen.AutoMergeOnOpen</c>: spare items merge themselves.</summary>
    public bool AutoMergeOnOpen { get; set; }

    /// <summary>The empty vault's one door. The host reads it once and clears it.</summary>
    public bool WantsHunt { get; set; }

    /// <summary>This week's stall, minted by the host (identity from the week, level from the buyer).</summary>
    public List<ItemInstance> TraderStock { get; set; } = new();

    /// <summary>Stall slots already bought this week. Host-owned; the screen only reads it.</summary>
    public HashSet<int> TraderBought { get; set; } = new();

    private int? _traderBuy;
    /// <summary>The house pattern: the screen records intent, the host spends and mints.</summary>
    public int? ConsumeTraderBuy() { var r = _traderBuy; _traderBuy = null; return r; }

    private bool _traderOpen;
    private bool _modalOpenedNow;   // the click that OPENED a modal must not also click inside it
    /// <summary>DEV: pose the stall for a capture.</summary>
    public void DevOpenTrader() => _traderOpen = true;

    /// <summary>The host reads this to hold the rail and its hotkeys while a modal is up.</summary>
    public bool ModalUp => ModalOpen;

    /// <summary>Esc, and the host's door-holding, both land here.</summary>
    public void CloseModals()
    {
        _traderOpen = false;
        _inspectItem = null;
        _inspectBuild = null;
        _inspectError = "";
    }
    private ItemInstance? _inspectItem;
    private ShareCodes.SharedBuild? _inspectBuild;
    private string _inspectError = "";
    private bool ModalOpen => _traderOpen || _inspectItem is not null || _inspectBuild is not null
                              || _inspectError.Length > 0;

    /// <summary>
    /// What the player asked to open, taken by the host exactly once.
    /// </summary>
    /// <remarks>
    /// The house pattern (see <c>TrainingScreen.ConsumeTrain</c>): the screen records intent, the host
    /// performs the mutation. A screen that opened chests itself would need the Hunter, the loot
    /// tuning and the build's rarity bonus, which is most of the game reaching into a view.
    /// </remarks>
    public enum OpenRequest { None, Selected, All }

    private OpenRequest _pending = OpenRequest.None;

    /// <summary>Take the pending request, clearing it.</summary>
    public OpenRequest ConsumeOpen()
    {
        var r = _pending;
        _pending = OpenRequest.None;
        return r;
    }

    /// <summary>Which STACK a click chose (an index into the stacked, best-first pile).</summary>
    public int SelectedIndex => _cursor;

    /// <summary>
    /// The chest a click chose — a real member of the clicked stack, so the host opens exactly one
    /// chest equal to it (identical chests stack on the vault since 2026-08-23; contents are rolled at
    /// open, so any member is the right one).
    /// </summary>
    public Chest? SelectedChest { get; private set; }

    // The TAKE ONLY keep-filter used to be edited here. It concerns what the HUNT lets through, so it
    // lives on the HUNT screen now (HuntScreen.DrawKeepFilter); the host still persists it.

    // ── Layout ──────────────────────────────────────────────────────────────────────────────────
    //
    // NOTHING IN THIS BLOCK IS A `static readonly`. Every rectangle here is a property computed when it
    // is asked for, because every one of them depends on UiMetrics, and UiMetrics follows a SETTING —
    // a value frozen at class load would be the 100 % layout at every profile.

    /// <summary>
    /// Full width now — the detail column is gone. Height decided per-frame; see below.
    /// </summary>
    /// <remarks>
    /// Y=150, which is where STATS, GEAR, FORGE and MAP all start their first panel. It was 144, six
    /// pixels adrift for no reason anybody could name, and it had to move anyway: the screen's name is
    /// stated ABOVE the panel now, in the strip every other screen in the game keeps for it, rather
    /// than printed inside the panel's own top-left corner.
    /// </remarks>
    private static Rectangle Frame => PanelAt(Rows);

    /// <summary>Where the panel starts — under the screen's name and its tally, as on every screen.</summary>
    private static int PanelTop => UiKit.PageTop;   // the first row under the chrome band, at this profile

    /// <summary>Where the screen's name sits in the strip above the panel — the same 24 every screen uses.</summary>
    private const int ScreenTitleTop = 24;

    /// <summary>
    /// The one panel, sized to the pile in it.
    /// </summary>
    /// <remarks>
    /// It was a <c>static readonly Rectangle</c>, frozen at class load, which cannot follow
    /// <see cref="UiKit.Page"/> — and the page is exactly what UI SCALE rewrites. At 125 % the panel
    /// stayed 1842 wide inside a 1536-wide page and hung 330 px off the right edge. A property, not a
    /// tidy-up: the screen is wrong at two of the three scales without it.
    /// </remarks>
    private static Rectangle PanelAt(int rows) =>
        new(24, PanelTop, UiKit.PageRight(24) - 24, PanelH(rows));

    private static int PanelH(int rows) =>
        CardTop + rows * CardH + (rows - 1) * CardGapY + PileDepth + UiKit.PanelCorner;

    /// <summary>
    /// How far a stack's offset card backs reach past the card itself — reserved by the grid.
    /// </summary>
    /// <remarks>
    /// Two backs at 7 px each. Without the reservation a stacked chest in the right column or the
    /// bottom row drew its pile straight through the panel's ornate border: the card fitted, the
    /// pile behind it did not, and only a stacked chest in those positions showed it. The look of a
    /// pile, not a control — it does not grow with the profile.
    /// </remarks>
    private const int PileDepth = 14;

    /// <summary>One card back's offset — half the pile's depth.</summary>
    private const int PileStep = PileDepth / 2;

    /// <summary>The toolbar's controls — OPEN ALL, CHEST FILTER, TRADER, PASTE A CODE — all one height.</summary>
    private static int ToolbarH => UiMetrics.Control(56);

    /// <summary>The gap between the toolbar's buttons.</summary>
    private static int HeaderButtonGap => UiMetrics.Space(16);

    /// <summary>The hairline the toolbar stands on.</summary>
    private const int StripH = 2;

    // THE TOOLBAR. Rectangles derived from ONE anchor, so the empty vault and the full one put the
    // same button in the same place. They used to be hand-placed off GridPanel.Right at -770, -558
    // and -346 — three magic numbers kept in step by hand, which ignored the panel's own margin.
    private static Rectangle HeaderButton(int slotFromRight, int width)
    {
        var x = UiKit.ContentRight(Frame);
        for (var i = 0; i < slotFromRight; i++) x -= HeaderWidth(i) + HeaderButtonGap;
        return new Rectangle(x - width, UiKit.TitleTop(Frame), width, ToolbarH);
    }

    /// <summary>Right to left: OPEN ALL, CHEST FILTER, TRADER. Widths sized to their own labels at 100 %.</summary>
    private static readonly int[] HeaderBaseWidths = { 340, 240, 200 };

    /// <summary>A toolbar button's width at this profile — a label's room grows with the label.</summary>
    private static int HeaderWidth(int slotFromRight) => UiMetrics.Control(HeaderBaseWidths[slotFromRight]);

    private static Rectangle OpenAllBtn => HeaderButton(0, HeaderWidth(0));

    /// <summary>The CHEST FILTER's door — the keep-filter moved here from the HUNT (UX V2 P1.1, D12).</summary>
    private static Rectangle FilterBtn => HeaderButton(1, HeaderWidth(1));

    private static Rectangle TraderBtn => HeaderButton(2, HeaderWidth(2));

    /// <summary>
    /// PASTE A CODE, at the far LEFT of the toolbar and drawn as a plate rather than a button.
    /// </summary>
    /// <remarks>
    /// It is the one control here that does not act on your chests — it reads a stranger's code and
    /// shows it. Four identical ornate buttons in a row is what the audit measured as "no primary";
    /// the tiers now say what each one is: OPEN ALL is the decision, CHEST FILTER and TRADER are
    /// ordinary actions, and this is a side door.
    /// </remarks>
    private static Rectangle PasteRect =>
        new(UiKit.ContentLeft(Frame), UiKit.TitleTop(Frame), UiMetrics.Control(240), ToolbarH);

    // THE GRID. Two columns of very large cards, not six of very small ones.
    //
    // Measured before the change: 58 % of the canvas empty, the chest's PROMISE — the one fact worth
    // a click — set at the smallest rung on the screen and truncated to "EPIC OR BE...", and a first
    // visit showing one 242 px card marooned in a 1650 px panel. The pile is small (a vault holds
    // stacks, not hundreds), so the cards can be enormous: at 884x349 the whole dossier fits beside
    // the chest at Body, with room for the region's blurb underneath.
    private const int Cols = 2;
    private static int CardGapX => UiMetrics.Space(24);
    private static int CardGapY => UiMetrics.Space(20);

    /// <summary>A card never grows past this, however much room the page has under it.</summary>
    private static int MaxCardH => UiMetrics.Control(380);

    /// <summary>The card's inner inset on every side.</summary>
    private static int CardPad => UiMetrics.Space(24);

    /// <summary>How far under the card's top edge its first row (the chest, the kind line) sits.</summary>
    private static int CardHeadTop => UiMetrics.Space(40);

    /// <summary>The chest art's box: the picture the click is about, so it grows with the card.</summary>
    private static int ArtSize => UiMetrics.Control(160);

    /// <summary>The OPEN chip — a label shaped like a button, so it is a button's height.</summary>
    private static int ChipH => UiMetrics.Control(56);
    private static int ChipW => UiMetrics.Control(160);

    /// <summary>The grade pips: five small diamonds, readable in greyscale.</summary>
    private static int PipSize => UiMetrics.Control(18);
    private static int PipPitch => UiMetrics.Control(30);

    /// <summary>
    /// The shortest card that holds its own left column — chest, pips, OPEN chip — without one
    /// printing on the next. The text column budgets itself to whatever height the card gets.
    /// </summary>
    private static int CardMinH =>
        CardHeadTop + ArtSize + UiMetrics.Space(14) + PipSize + UiMetrics.Space(12) + ChipH + CardPad;

    /// <summary>The first card row — under the toolbar, its rule, and the consequence row.</summary>
    private static int CardTop => UiTypography.PanelTitleTop + ToolbarH + UiMetrics.Space(14) + StripH
                                  + UiMetrics.Space(10) + UiTypography.Pitch(UiTypography.Secondary)
                                  + UiMetrics.Space(4);

    private static int GridTop => PanelTop + CardTop;
    private static int GridBottom => UiKit.PageBottom(40) - UiKit.PanelCorner;

    /// <summary>The height the grid has for whole cards, once the pile's backs are reserved.</summary>
    private static int GridRoom => GridBottom - GridTop - PileDepth;

    /// <summary>
    /// As many rows of whole cards as the page holds — two at 100 %, one at 125 and 150 %, where the
    /// card that holds the bigger dossier is taller than half the room. Never a row that would print
    /// its OPEN chip through the panel's bottom frame.
    /// </summary>
    private static int Rows => Math.Max(1, (GridRoom + CardGapY) / (CardMinH + CardGapY));

    private static int CardH => Math.Min(MaxCardH, (GridRoom - CardGapY * (Rows - 1)) / Rows);
    private static int PerPage => Cols * Rows;

    private static int ContentW => UiKit.ContentRight(Frame) - UiKit.ContentLeft(Frame);
    private static int CardW => (ContentW - CardGapX * (Cols - 1) - PileDepth) / Cols;

    /// <summary>
    /// One visible card. A short last row is CENTRED, so three chests read as three rather than as
    /// four with a hole in it — the short-row centring the Forge's reveal summary already uses.
    /// </summary>
    private static Rectangle Card(int visible, int onPage)
    {
        var row = visible / Cols;
        var inRow = Math.Clamp(onPage - row * Cols, 1, Cols);
        var x0 = UiKit.ContentLeft(Frame)
                 + (ContentW - PileDepth - inRow * CardW - (inRow - 1) * CardGapX) / 2;
        return new Rectangle(x0 + visible % Cols * (CardW + CardGapX),
                             GridTop + row * (CardH + CardGapY), CardW, CardH);
    }

    /// <summary>How many cards this page actually shows — the last page is usually short.</summary>
    private int OnPage(int count) => Math.Clamp(count - _scroll, 0, PerPage);

    /// <summary>How many rows the whole pile takes.</summary>
    private static int TotalRows(int count) => (count + Cols - 1) / Cols;

    /// <summary>
    /// How far the medium frame's SIDE rail reaches in from the panel's edge, away from the corners.
    /// Measured off the art (the corner ornament reaches <see cref="UiKit.PanelCorner"/>; the plain
    /// rail between the corners is a fraction of that). Art geometry — unscaled.
    /// </summary>
    private const int FrameRailReach = 14;

    /// <summary>
    /// The scrollbar's lane: the plain strip of frame between the content's right edge and the side
    /// rail, so the bar marks the pile's depth without taking a pixel from the cards beside it. Only
    /// drawn when there is more pile than page.
    /// </summary>
    private static Rectangle ScrollTrack(Rectangle frame, int rows)
    {
        var band = UiTypography.PanelPadX - FrameRailReach;
        var w = UiMetrics.ScrollbarWidth;
        return new Rectangle(UiKit.ContentRight(frame) + (band - w) / 2, GridTop, w,
                             rows * CardH + (rows - 1) * CardGapY + PileDepth);
    }

    /// <summary>The asset key of an element's glyph — <c>source_nature</c>. Files under ItemsLoot/glyphs/source.</summary>
    private static string SourceGlyphKey(Source e) => $"source_{e.ToString().ToLowerInvariant()}";

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>
    /// The cards' light is the first row, which is the only row a first visit can have — the Vault opens
    /// on the first chest held, and a new game holds the welcome gift from its first frame. The second
    /// lights the whole toolbar, whose three names that card reads out.
    /// </remarks>
    internal static Rectangle[] Spotlights(TourTarget target)
    {
        switch (target)
        {
            case TourTarget.ChestCards:
                // DERIVED FROM THE GRID IT LIGHTS, not from a remembered offset — and no longer from
                // Card(), which now has to be told how many cards share its row.
                var halo = UiMetrics.Space(12);
                return new[]
                {
                    new Rectangle(Frame.X + UiMetrics.Space(30), GridTop - halo,
                                  Frame.Width - UiMetrics.Space(30) * 2, CardH + halo * 2),
                };
            case TourTarget.VaultButtons:
                var strip = PasteRect;
                var breath = UiMetrics.Space(10);
                strip.Inflate(breath, breath);
                return new[] { new Rectangle(strip.X, strip.Y, OpenAllBtn.Right + breath - strip.X, ToolbarH + breath * 2) };
            default:
                return Array.Empty<Rectangle>();
        }
    }

    /// <summary>The furthest first-visible index that still fills the page — row-aligned.</summary>
    /// <remarks>
    /// Clamping to count-1 stranded the view: open chests until the pile fits one page and a non-zero
    /// scroll kept the BEST cards (BestFirst sorts them first) hidden with the wheel gate dead. The
    /// pile is consumed while displayed, so the clamp must pull the window back as it shrinks.
    /// </remarks>
    private static int MaxScroll(int count) => Math.Max(0, TotalRows(count) - Rows) * Cols;

    private void Clamp(int count)
    {
        if (count <= 0) { _cursor = 0; _scroll = 0; return; }
        _cursor = Math.Clamp(_cursor, 0, count - 1);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll(count));
    }

    public void Update(float dt, IReadOnlyList<Chest> chests, Point mouse, bool clicked, int wheel)
    {
        ArgumentNullException.ThrowIfNull(chests);
        _anim += dt;

        // The host hands the cursor in PAGE space already; it is hit-tested as it arrives.
        var hit = mouse;

        // One card per STACK of identical chests (ChestDossiers.Stacked); the grid, the hover and the
        // cursor all count stacks. `sorted` is the stacks' samples in best-first order.
        var stacks = ChestDossiers.Stacked(chests);
        var sorted = stacks.Select(st => st.Sample).ToList();
        Clamp(sorted.Count);
        var onPage = OnPage(sorted.Count);

        if (_devPosePending) ApplyDevPose(sorted, onPage);
        if (_devReduced) UiMotion.Reduced = true;   // RH_SHOT only — see the pose dial's note

        // THE TALLY ANSWERS THE OPEN (brief §36). The pile only ever SHRINKS because the player
        // opened something — a boss drop grows it — so the count line brightens once and settles.
        if (_lastCount >= 0 && chests.Count < _lastCount) UiMotion.Flash(TallyKey, UiMotion.Transition);
        _lastCount = chests.Count;

        // A burst that has finished is forgotten HERE, in Update — Draw only reads it.
        if (_devBurstT is null && !UiMotion.Pulsing(BurstKey)) _burst.Clear();

        // OPEN ALL was clicked in the last Draw (UiKit.Button answers there, like every button in the
        // game) and the host takes the request the moment this call returns — so this is the one
        // frame the pile is still here to be remembered: every visible card's burst, and the cue for
        // the pile's best chest, once.
        if (_pending == OpenRequest.All && onPage > 0) BeginBurst(sorted, onPage, 0, all: true);

        // While the stall, an inspect card OR THE CHEST FILTER'S POPOVER is open, the grid underneath
        // is furniture: no hover, no wheel, and — decisive — no chest-opening click. The overlays' own
        // buttons live in Draw.
        //
        // FilterOpen was missing from this gate, and the polish pass is what made it visible. The
        // popover sits ON the top-right card, so its `+`, its `-` and its slot medallions all lie
        // inside that card's rectangle: a click on `+` raised the tier AND opened the chest under it,
        // and the card lit and pressed under a pointer that was inside a different control. Nothing
        // announced either, which is why it survived — a burst on a chest nobody meant to open is
        // what finally said it out loud. (Closing on a click outside is unaffected: that lives in
        // DrawFilterIfOpen, and Draw still sees every click.)
        if (ModalOpen || FilterOpen) { _hoverIdx = -1; return; }

        // The wheel moves the window one ROW at a time, never past a page that is still full.
        if (wheel != 0)
            _scroll = UiKit.Scrolled(_scroll / Cols, Math.Sign(wheel), Rows, TotalRows(sorted.Count)) * Cols;

        // Hover, resolved here where dt lives. There is nothing on the card to hover SEPARATELY any
        // more: the peek glass that used to win over the card it sat on is gone, and with it the rule
        // that the card's OPEN affordance stood down while the pointer was on it.
        var overCard = -1;
        for (var vis = 0; vis < onPage; vis++)
        {
            if (!Card(vis, onPage).Contains(hit)) continue;
            overCard = _scroll + vis;
            break;
        }
        _hoverIdx = overCard;

        if (!clicked) return;

        // THE CHEST IS THE BUTTON. A click anywhere on the card opens THAT chest — including on the
        // OPEN chip, which is a label on the card and deliberately not a second click source. The
        // burst and the cue are armed on this same edge: the card answers the click at once, and
        // the host removes the chest before the next Draw.
        if (overCard >= 0)
        {
            _cursor = overCard;
            SelectedChest = sorted[overCard];
            _pending = OpenRequest.Selected;
            BeginBurst(sorted, onPage, overCard - _scroll, all: false);
        }
    }

    /// <summary>
    /// Remember where the burst plays and arm it: one visible card, or under OPEN ALL every visible
    /// card (the ring goes on the first — the best, the pile is sorted best-first). Sets the cue.
    /// </summary>
    private void BeginBurst(List<Chest> sorted, int onPage, int vis, bool all)
    {
        _burst.Clear();
        var best = Rarity.Common;
        if (all)
        {
            for (var v = 0; v < onPage; v++)
                _burst.Add((Card(v, onPage), ArtBox(Card(v, onPage)), sorted[_scroll + v].Rarity));
            // The cue is for the PILE, which can be deeper than the page.
            for (var i = 0; i < sorted.Count; i++)
                if (sorted[i].Rarity > best) best = sorted[i].Rarity;
        }
        else
        {
            var card = Card(vis, onPage);
            best = sorted[_scroll + vis].Rarity;
            _burst.Add((card, ArtBox(card), best));
        }
        _cue = CueFor(best);
        UiMotion.Flash(BurstKey, OpenBurstSeconds);
    }

    /// <summary>RH_SHOT_VAULT_POSE, applied once (see the field's note). A pose opens nothing and cues nothing.</summary>
    private void ApplyDevPose(List<Chest> sorted, int onPage)
    {
        _devPosePending = false;
        if (Environment.GetEnvironmentVariable("RH_SHOT_VAULT_POSE") is not { Length: > 0 } pose) return;
        // "open:1@0.22,reduced" — the verb, its dials, then any flags.
        var parts = pose.Split(',');
        foreach (var flag in parts)
            if (flag.Trim().Equals("reduced", StringComparison.OrdinalIgnoreCase)) _devReduced = true;
        var at = parts[0].Split('@');
        var t = at.Length > 1
                && float.TryParse(at[1], System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out var s)
            ? s : 0.08f;
        var verb = at[0].Split(':');
        var n = verb.Length > 1 && int.TryParse(verb[1], out var i) ? i : 0;
        switch (verb[0].ToLowerInvariant())
        {
            case "hold": _devHeld = true; break;
            case "open" when n >= 0 && n < onPage: BeginBurst(sorted, onPage, n, all: false); _devBurstT = t; break;
            case "openall" when onPage > 0: BeginBurst(sorted, onPage, 0, all: true); _devBurstT = t; break;
        }
        _cue = null;
    }

    public void Draw(SpriteBatch b, IReadOnlyList<Chest> chests, Point mouse, bool clicked)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(chests);

        var hit = mouse;

        var stacks = ChestDossiers.Stacked(chests);
        var sorted = stacks.Select(st => st.Sample).ToList();
        Clamp(sorted.Count);

        // ── THE SCREEN'S OWN FURNITURE. ──────────────────────────────────────────────────────────
        //
        // Playtest 2026-08-29: "the VAULT screen is of a different design from the other screens."
        // It was, in three ways at once, and all three are in these four lines. It drew NO SCRIM, so
        // the swamp backdrop ran the full height of a screen that is a room indoors. It printed its
        // name INSIDE the panel's top-left corner at the screen-title size, where every other screen
        // in the game states its name centred above the panels, under a gold rule. And the tally that
        // says what is waiting was a caption hanging off that corner rather than the screen's own
        // subtitle. The words are unchanged; only where they stand is.
        //
        // The rule sits one title line under the title and the tally one breath under the rule, so a
        // 150 % title (57 px) pushes them down rather than printing through them.
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));
        // VAULT, without the article — the rail says VAULT and D7 settled that the screen agrees.
        _ui.TextCenterBig(b, "VAULT", UiKit.PageCenterX, ScreenTitleTop, Gold, UiTypography.ScreenTitle, TextFace.Display);
        var ruleY = ScreenTitleTop + UiTypography.Pitch(UiTypography.ScreenTitle) + 1;
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, ruleY, 480, 3), Gold * 0.5f);

        // The tally, so the pile reads at a glance without counting cards.
        var tally = ChestDossiers.Tally(chests);   // every chest, not every stack
        var summary = tally.Count == 0
            ? "nothing waiting"
            : string.Join("   ", tally.Select(t => $"{t.Count} {t.Grade.ToString().ToUpperInvariant()}"));
        // Lit once when an open shrank the pile (armed in Update), settling back to its own grey.
        // A posed burst freezes the tally at the SAME instant: at play the pile shrinks on the very
        // frame the burst starts, so the shot has to hold one moment, not two.
        var tallyPulse = _devBurstT is { } tallyHeld
            ? Math.Clamp(1f - tallyHeld / UiMotion.Transition, 0f, 1f)
            : UiMotion.Pulse(TallyKey);
        var tallyInk = Color.Lerp(Slate, Bone, UiMotion.Smooth(tallyPulse));
        _ui.TextCenterBig(b, summary.ToUpperInvariant(), UiKit.PageCenterX, ruleY + UiMetrics.Space(6), tallyInk,
                          UiTypography.Secondary);

        // QUIET, NOT GOLD. The house frame rule (UiKit.PanelQuiet): the gold filigree is for MODALS —
        // the stall and the two share-code cards below still wear it — and every panel that lives IN a
        // screen wears the brown. This was the last in-screen panel in the game still shouting.
        var rows = Math.Clamp(TotalRows(sorted.Count), 1, Rows);
        var frame = sorted.Count == 0 ? EmptyFrame : PanelAt(rows);
        _ui.PanelQuiet(b, frame);

        var uiClicked = clicked && !ModalOpen;

        // ── THE TOOLBAR, in three tiers. It was four ornate buttons of identical weight in a row, and
        //    the audit's finding was blunt: no primary at all, and on a first visit — one chest — the
        //    loudest thing on the screen was a greyed-out "OPEN ALL (1)" that refused to be clicked.
        //
        //    OPEN ALL is now the screen's one Primary, and it is never disabled: with a single chest
        //    it says OPEN THE CHEST, and ForgeScreen.OpenAllChests plays the identical single-chest
        //    ceremony for a pile of one. TRADER and CHEST FILTER are ordinary Secondary buttons.
        //    PASTE A CODE is a plate: the one control that does not touch your chests.
        //
        //    _modalOpenedNow: the TRADER button's rect overlaps the stall's CLOSE (measured: a 72x28
        //    region), and the click edge stays latched through the whole Draw — without this flag a
        //    click in the overlap opened the stall and closed it IN THE SAME FRAME. ─────────────────
        if (sorted.Count > 0)
        {
            var label = chests.Count == 1 ? "OPEN THE CHEST" : $"OPEN ALL ({chests.Count})";
            if (_ui.Button(b, OpenAllBtn, label, hit, uiClicked, true, ButtonStyle.Primary))
                _pending = OpenRequest.All;
        }

        if (_ui.Button(b, FilterBtn, "CHEST FILTER", hit, uiClicked, true)) FilterOpen = !FilterOpen;
        // SELECTED IS NOT HOVER (§28). While its popover is up the button wears a gold ring —
        // persistent structure that does not leave with the pointer, at the SELECTED tier's weight
        // (§23) rather than the primary's ornate art, and it is the very mark this screen already
        // puts on a lit filter cell and a lit MiniButton, so "on" looks the same everywhere on it.
        //
        // It was a thin rule along the button's foot first, and the 150 % capture killed that: the
        // button art's own gold ornament is thicker at every step of the density profile, and a
        // 2 px line inside it disappeared into the frame it was meant to be distinguished from.
        if (FilterOpen) Outline(b, FilterBtn, Gold * 0.8f, 2);

        if (_ui.Button(b, TraderBtn, "TRADER", hit, uiClicked, TraderStock.Count > 0))
        {
            _traderOpen = true;
            _modalOpenedNow = true;
        }

        // THE SIDE DOOR keeps to the QUIET tier: a plate, a value-step hover, the same 2 px press
        // every button makes — and never gold, never a pulse, so it cannot outrank OPEN ALL (§60).
        var pasteHot = PasteRect.Contains(hit) && !ModalOpen;
        var pasteLift = Lift(PasteKey, pasteHot);
        var pastePressed = pasteHot && Held;
        var pastePlate = pastePressed
            ? new Rectangle(PasteRect.X, PasteRect.Y + 2, PasteRect.Width, PasteRect.Height)
            : PasteRect;
        _ui.Plate(b, pastePlate);
        var pasteWell = new Rectangle(pastePlate.X + 1, pastePlate.Y + 1, pastePlate.Width - 2, pastePlate.Height - 2);
        if (pasteLift > 0f) _ui.Fill(b, pasteWell, Color.White * (0.05f * pasteLift));
        if (pastePressed) _ui.Fill(b, pasteWell, Color.Black * 0.18f);
        Outline(b, pastePlate, Color.Lerp(Dim, Slate, pasteLift), 1);
        _ui.TextCenterBig(b, "PASTE A CODE", pastePlate.Center.X,
                          pastePlate.Y + (ToolbarH - UiTypography.ButtonText) / 2 - 2,
                          Color.Lerp(Slate, Bone, pasteLift), UiTypography.ButtonText);
        if (UiKit.ClickedIn(PasteRect, hit, uiClicked))
        {
            PasteCode();
            _modalOpenedNow = true;
        }
        if (pasteHot)
            _ui.HoverTip(b, "READ AN ITEM OR BUILD CODE SOMEONE SHARED WITH YOU. IT NEVER JOINS YOUR BAG.", hit);

        // The hairline the toolbar stands on, so the buttons read as a header and the cards below as
        // the panel's contents — one rule instead of a gap the eye has to guess at.
        var strip = new Rectangle(UiKit.ContentLeft(frame), UiKit.TitleTop(frame) + ToolbarH + UiMetrics.Space(14),
                                  UiKit.ContentRight(frame) - UiKit.ContentLeft(frame), StripH);
        _ui.Fill(b, strip, Dim);

        // ── THE CONSEQUENCE ROW. What OPEN ALL will do to the drops BEFORE you see them, said once,
        //    on a reserved row — not as a confirmation dialog on every click (brief §51, §85).
        //
        //    Both traits are Memory Dust purchases, and the row names them the way the tree does, so
        //    it reads as "the thing you bought is on" rather than as a warning from nowhere. "GEAR",
        //    not "items": LandChest sells only wearable pieces and deliberately spares gems. And "CAN
        //    GIVE", not "will": Chests.Open elevates a roll UP to the chest's floor and never down, so
        //    a chest whose floor is at or under the sell line can still produce a sellable piece. ────
        var conseqY = strip.Bottom + UiMetrics.Space(10);
        var clauses = new List<string>();
        if (AutoSellFloor is { } floor)
        {
            var n = stacks.Where(s => ChestDossiers.For(s.Sample).GuaranteedFloor <= floor).Sum(s => s.Count);
            if (n > 0)
                // NAMED BY ITS OWNER. Auto-sell is a Warren facility level now, not a trait purchase,
                // and it fires when a chest is OPENED — which is what the sentence says.
                clauses.Add($"YOUR SCAVENGER RUNS SELL {floor.ToString().ToUpperInvariant()} ITEMS — {n} OF THESE "
                            + "CHESTS CAN GIVE GEAR THAT IS SOLD FOR YOU WHEN THE CHEST IS OPENED.");
        }
        if (AutoMergeOnOpen) clauses.Add("YOUR HOARD VAULTS MERGE YOUR SPARE ITEMS WHEN A CHEST IS OPENED.");
        if (clauses.Count > 0)
            _ui.TextBig(b, _ui.ShortenBig(string.Join("   ·   ", clauses), ContentW - UiMetrics.Control(460),
                                          UiTypography.Secondary),
                        UiKit.ContentLeft(frame), conseqY, Slate, UiTypography.Secondary);

        // The page position, on the same row's right end — the words still say what moves the pile,
        // and since the P2 reflow the bar in the frame's margin shows how deep it is.
        if (sorted.Count > PerPage)
            _ui.TextRightBig(b, $"PAGE {_scroll / PerPage + 1} OF {(sorted.Count + PerPage - 1) / PerPage}"
                                + "  —  THE MOUSE WHEEL SCROLLS",
                             UiKit.ContentRight(frame), conseqY, Slate, UiTypography.Secondary);

        if (sorted.Count == 0)
        {
            DrawEmpty(b, frame, hit, uiClicked);
            // OPEN ALL empties the room in the same frame it is clicked; its burst still plays here,
            // over where the pile was, while the Forge's cascade takes over above it.
            DrawBurst(b);
            DrawFilterIfOpen(b, hit, clicked && !_modalOpenedNow);
            DrawTrader(b, hit, clicked && !_modalOpenedNow);
            DrawInspect(b, hit, clicked && !_modalOpenedNow);
            _modalOpenedNow = false;
            return;
        }

        var onPage = OnPage(sorted.Count);
        for (var vis = 0; vis < onPage; vis++)
        {
            var idx = _scroll + vis;
            var hovered = idx == _hoverIdx && !ModalOpen;
            // PRESSED is the button held over the card (the rig poses it with `hold`).
            var pressed = hovered && Held;
            DrawCard(b, sorted[idx], stacks[idx].Count, Card(vis, onPage), idx, hovered, pressed);
        }

        // The pile's depth, in the frame's own margin: drawn only when a wheel step would show more.
        _ui.ScrollBar(b, ScrollTrack(frame, rows), _scroll / Cols, Rows, TotalRows(sorted.Count));

        // Over the cards, so the ring is not under the neighbour that slid into the opened slot.
        DrawBurst(b);

        DrawFilterIfOpen(b, hit, clicked && !_modalOpenedNow);
        DrawTrader(b, hit, clicked && !_modalOpenedNow);
        DrawInspect(b, hit, clicked && !_modalOpenedNow);
        _modalOpenedNow = false;
    }


    // ── THE CHEST CARD ───────────────────────────────────────────────────────────────────────────
    //
    // One card, one kind of chest, and the whole of what can honestly be said about it before it is
    // opened. ChestDossier.Lines is already the reading order and already gift-aware, so the card
    // prints it rather than choosing for itself which two facts fit.
    //
    // THE FACTS ARE IN SENTENCE CASE, and that reverses this card's own older note, which capitalised
    // them so the two fragments would match the TIER line above. Two fragments in capitals is a label;
    // six sentences in capitals is shouting, and it is the readability fault the whole pass exists to
    // remove. The header, the tier and the chip stay capitals — they are labels, and they still are.
    private void DrawCard(SpriteBatch b, Chest chest, int stackCount, Rectangle card, int idx, bool hovered, bool pressed)
    {
        var d = ChestDossiers.For(chest);
        var grade = RarityColors[(int)chest.Rarity];
        // HOVER through the one vocabulary (§26): ~100 ms in, and out again when the pointer leaves.
        // REDUCED MOTION keeps the value step and drops the movement: the card still brightens under
        // the pointer at once, it just does not grow or ease into it.
        var ease = Lift(HashCode.Combine(CardKeyBase, idx), hovered);
        var pad = CardPad;

        // A STACK LOOKS LIKE A STACK. Two identical chests were one card with a small "x2" in the
        // corner, and testers did not see it (playtest 2026-08-25). Now the card sits on two offset
        // card backs, the way a pile of cards does, before anything else on it is drawn.
        for (var back = Math.Min(2, stackCount - 1); back >= 1; back--)
        {
            var off = new Rectangle(card.X + back * PileStep, card.Y + back * PileStep, card.Width, card.Height);
            _ui.Fill(b, off, new Color(0x0E, 0x0C, 0x14));
            _ui.Fill(b, new Rectangle(off.X, off.Y, off.Width, 5), grade * 0.45f);
            _ui.Fill(b, new Rectangle(off.Right - 2, off.Y, 2, off.Height), grade * 0.25f);
            _ui.Fill(b, new Rectangle(off.X, off.Bottom - 2, off.Width, 2), grade * 0.25f);
        }

        // PRESSED (§27): the plate drops 2 px onto its own pile and darkens, for exactly as long as
        // the button is held — the depression UiKit.Button makes, so a card and a button under the
        // same thumb are the same material. Immediate; nothing eases. Everything on the card rides
        // the dropped rectangle, so the chip, the pips and the facts press together.
        if (pressed) card = new Rectangle(card.X, card.Y + 2, card.Width, card.Height);

        // The QUIET tier, which is what this is: a plate inside a panel. It used to hand-draw the
        // plate that UiKit.Plate exists to be. Hover brightens the EDGE one value step — never a new
        // hue (art bible chrome rules).
        _ui.Plate(b, card);
        _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 6), grade);
        if (pressed) _ui.Fill(b, new Rectangle(card.X + 1, card.Y + 6, card.Width - 2, card.Height - 7), Color.Black * 0.18f);
        if (ease > 0f)
        {
            var edge = Bone * (0.35f + 0.45f * ease);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - 2, card.Width, 2), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Y, 2, card.Height), edge);
            _ui.Fill(b, new Rectangle(card.Right - 2, card.Y, 2, card.Height), edge);
        }

        // THE CHEST IS THE PICTURE, lifted toward white so grade never decides how VISIBLE it is
        // (SpriteBatch tint multiplies). On hover it grows 6% about its own centre.
        var artBox = ArtBox(card);
        var icon = Grow(artBox, UiMotion.Reduced ? 1f : 1f + 0.06f * ease);
        if (_ui.Assets.Get("chest_loot") is { } chestArt)
            _ui.SpriteFit(b, chestArt, icon, Color.Lerp(grade, Color.White, 0.45f + 0.1f * ease));
        else _ui.Diamond(b, icon, grade);

        // The stack count as a gold badge on the pile itself. A click opens ONE of them; OPEN ALL
        // still opens every chest. Playtest: "ayni chestler stacklensin."
        if (stackCount > 1)
        {
            var badge = new Rectangle(card.X + UiMetrics.Space(16), card.Y + UiMetrics.Space(24),
                                      UiMetrics.Control(76), UiMetrics.Control(36));
            _ui.Fill(b, badge, Gold);
            _ui.Fill(b, new Rectangle(badge.X + 2, badge.Y + 2, badge.Width - 4, badge.Height - 4),
                     new Color(0x3A, 0x2A, 0x10));
            _ui.TextCenterBig(b, $"×{stackCount}", badge.Center.X,
                              badge.Y + (badge.Height - UiTypography.Headline) / 2 - 1, Gold, UiTypography.Headline);
        }

        // The pip row: grade as a COUNT, readable in greyscale. Common one pip, Legendary five.
        var pipY = artBox.Bottom + UiMetrics.Space(14);
        for (var pip = 0; pip < 5; pip++)
        {
            var dot = new Rectangle(card.X + pad + pip * PipPitch, pipY, PipSize, PipSize);
            _ui.Diamond(b, dot, pip <= (int)chest.Rarity ? grade : Dim);
        }

        // OPEN — the label the click fulfils, always visible rather than fading in on hover. It is
        // NOT a button: the whole card commits, and a second click source inside it would open two
        // chests on one click. It lights with the CARD, which is what you are actually clicking.
        var chip = new Rectangle(card.X + pad, card.Bottom - pad - ChipH, ChipW, ChipH);
        _ui.Plate(b, chip);
        Outline(b, chip, Color.Lerp(Dim, Gold, ease), 2);
        _ui.TextCenterBig(b, stackCount > 1 ? "OPEN ONE" : "OPEN", chip.Center.X,
                          chip.Y + (ChipH - UiTypography.ButtonText) / 2 - 2,
                          Color.Lerp(Gold * 0.75f, Gold, ease), UiTypography.ButtonText);

        // ── The text column. ─────────────────────────────────────────────────────────────────────
        var tx = card.X + pad + ArtSize + UiMetrics.Space(20);
        var tw = card.Right - pad - tx;

        // The kind of chest, in its grade's colour — "EPIC CHEST", or a gift's own title.
        var titleY = card.Y + CardHeadTop;
        _ui.TextBig(b, d.Title + (stackCount > 1 ? $" · {stackCount} WAITING" : ""),
                    tx, titleY, grade, UiTypography.Secondary);

        // The element as its SOURCE GLYPH — the same art the skills and the map use, right-aligned on
        // the title's row. The coloured diamond stays as the fallback for a glyph not on disk.
        if (chest.Element is { } e)
        {
            var g = UiMetrics.Control(28);
            var glyph = new Rectangle(card.Right - pad - g, titleY - UiMetrics.Space(4), g, g);
            if (!_ui.Icon(b, SourceGlyphKey(e), glyph, Color.White))
                _ui.Diamond(b, new Rectangle(glyph.X + 4, glyph.Y + 4, glyph.Width - 8, glyph.Height - 8),
                            SourceColor.GetValueOrDefault(e, Slate));
        }

        // The tier one kind-line under the kind, and the dossier one headline under that: derived, so
        // a 150 % TIER (48 px) pushes the facts down instead of printing over their first line.
        var tierY = titleY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(2);
        _ui.TextBig(b, $"TIER {chest.Tier}", tx, tierY, Bone, UiTypography.PrimaryValue);

        // THE DOSSIER, on the card. The first line is the PROMISE — the fact worth the click — so it
        // takes Good when there is one and Secondary when the honest answer is "any rarity, a
        // gamble". Never Dim: "there isn't a promise" is a fact, not a disabled control.
        var y = tierY + UiTypography.Pitch(UiTypography.PrimaryValue) + UiMetrics.Space(12);
        var bottom = card.Bottom - pad;
        var step = UiTypography.Pitch(UiTypography.Body);
        var lines = d.Lines;
        for (var li = 0; li < lines.Count && y + step <= bottom; li++)
        {
            var ink = li == 0
                ? (d.IsGift || d.GuaranteedFloor > Rarity.Common ? UiInk.Good : Slate)
                : Bone;
            foreach (var wrapped in _ui.WrapBig(lines[li], tw, UiTypography.Body))
            {
                // The line budget is what keeps the card HONEST when the card is short: it stops
                // emitting rather than drawing a fact through the OPEN chip.
                if (y + step > bottom) break;
                _ui.TextBig(b, wrapped, tx, y, ink, UiTypography.Body);
                y += step;
            }
        }

        // The region's own voice — the last thing the deleted tooltip said that the card did not.
        var blurbStep = UiTypography.Pitch(UiTypography.Secondary);
        var blurbDrop = UiMetrics.Space(18);
        if (!string.IsNullOrWhiteSpace(d.RegionBlurb) && y + blurbDrop + blurbStep <= bottom)
        {
            _ui.Fill(b, new Rectangle(tx, y + UiMetrics.Space(8), tw, 1), Dim);
            y += blurbDrop;
            foreach (var wrapped in _ui.WrapBig(d.RegionBlurb, tw, UiTypography.Secondary))
            {
                if (y + blurbStep > bottom) break;
                _ui.TextBig(b, wrapped, tx, y, Slate, UiTypography.Secondary);
                y += blurbStep;
            }
        }
    }

    /// <summary>The chest art's box on a card — the picture the click is about, and the burst's centre.</summary>
    private static Rectangle ArtBox(Rectangle card) =>
        new(card.X + CardPad, card.Y + CardHeadTop, ArtSize, ArtSize);

    // ── THE OPEN BURST ───────────────────────────────────────────────────────────────────────────
    //
    // The card's answer to the click, in the REWARD band and no longer: the card's edge lights in the
    // chest's grade colour and fades, and a ring sweeps out from where the chest was — plotted the way
    // the trait tree plots its shockwave, so the game's two celebrations are one family. Rarity scales
    // the EMPHASIS, not the wait: the ring reaches further for a richer grade (see Reach), and Epic and
    // up add ONE echo ring inside the same window — a second pulse, never a second's more waiting
    // (§56–§60: "no gacha fireworks"). Under Reduced Motion the ring — movement — is dropped and the
    // edge's fade stays, and both end on the same empty frame.

    /// <summary>
    /// The burst at one instant: the edge glow's strength, how far the ring has swept (0..1) and
    /// its alpha, and the same for the Epic-and-up echo ring (0 when there is none).
    /// </summary>
    public readonly record struct BurstFrame(float Edge, float Ring, float RingAlpha, float Echo, float EchoAlpha);

    /// <summary>
    /// The burst at one instant, from the pulse <see cref="UiMotion.Pulse"/> reports (1 just fired,
    /// 0 done). Pure, so a test can hold it at 0, the middle and the end — the rig cannot.
    /// </summary>
    public static BurstFrame BurstAt(float pulse, Rarity grade, bool reduced)
    {
        pulse = Math.Clamp(pulse, 0f, 1f);
        var t = 1f - pulse;
        var edge = pulse;   // a plain fade — the one motion Reduced Motion keeps
        if (reduced) return new BurstFrame(edge, 0f, 0f, 0f, 0f);
        var ring = UiMotion.Smooth(t);
        var ringAlpha = (1f - t) * (1f - t);
        var echo = 0f;
        var echoAlpha = 0f;
        if (grade >= Rarity.Epic && t >= EchoStart)
        {
            echo = UiMotion.Smooth((t - EchoStart) / (1f - EchoStart));
            echoAlpha = (1f - echo) * (1f - echo);
        }
        return new BurstFrame(edge, ring, ringAlpha, echo, echoAlpha);
    }

    /// <summary>The burst over the grid. Reads the pulse (or the rig's frozen instant); arms nothing.</summary>
    private void DrawBurst(SpriteBatch b)
    {
        if (_burst.Count == 0) return;
        var pulse = _devBurstT is { } held
            ? Math.Clamp(1f - held / OpenBurstSeconds, 0f, 1f)
            : UiMotion.Pulse(BurstKey);
        if (pulse <= 0f) return;

        for (var i = 0; i < _burst.Count; i++)
        {
            var (card, art, grade) = _burst[i];
            var f = BurstAt(pulse, grade, UiMotion.Reduced);
            var ink = RarityColors[(int)grade];

            // The edge: the card's own hover edge, in the grade's colour, with a softer halo a step out.
            Outline(b, card, ink * (0.9f * f.Edge), 3);
            Outline(b, new Rectangle(card.X - 3, card.Y - 3, card.Width + 6, card.Height + 6), ink * (0.35f * f.Edge), 3);

            // The ring — on the FIRST card only under OPEN ALL (the best; the pile is sorted best-first):
            // one burst for the pile, not one per card.
            if (i > 0 || f.RingAlpha <= 0f) continue;
            Ring(b, art.Center, ArtSize / 2f + Reach(grade) * f.Ring, ink * f.RingAlpha, f.Ring);
            if (f.EchoAlpha > 0f)
                Ring(b, art.Center, ArtSize / 2f + Reach(grade) * 0.55f * f.Echo, ink * f.EchoAlpha, f.Echo);
        }
    }

    /// <summary>
    /// How far past the chest a grade's wave travels — the EMPHASIS dial, measured in chest-widths so
    /// it follows the density profile with everything else.
    /// </summary>
    /// <remarks>
    /// §58's three tiers: common small, uncommon and rare stronger, epic and legendary richer. It stays
    /// a CARD-sized flourish on purpose — at 100 % the largest wave finishes at about 220 px, roughly
    /// the card's own half-height, so the vault answers a click rather than staging a lottery draw
    /// across the page (§57, "no gacha fireworks"). The reach grows, the WAIT never does.
    /// </remarks>
    private static float Reach(Rarity grade) => ArtSize * (0.35f + 0.13f * (int)grade);

    /// <summary>
    /// A hollow circle of small squares, thinning as it sweeps out.
    /// </summary>
    /// <remarks>
    /// The step count follows the RADIUS, not a constant — the lesson the trait tree's shockwave
    /// already learned: a fixed step count draws a solid band when the ring is small and a dotted one
    /// when it is large, which is exactly backwards for an expanding wave. Anything off the page is
    /// skipped rather than drawn and clipped, so the biggest grade on the left-hand column costs
    /// nothing for the arc nobody can see.
    /// </remarks>
    private void Ring(SpriteBatch b, Point centre, float radius, Color ink, float sweep)
    {
        var steps = Math.Clamp((int)(radius * 4f), 48, 1600);
        var w = Math.Max(2, (int)(7 * (1f - sweep)));
        for (var i = 0; i < steps; i++)
        {
            var a = i / (float)steps * MathF.Tau;
            var px = centre.X + (int)(MathF.Cos(a) * radius);
            var py = centre.Y + (int)(MathF.Sin(a) * radius);
            if (px < UiKit.Page.Left - w || px > UiKit.Page.Right + w
                || py < UiKit.Page.Top - w || py > UiKit.Page.Bottom + w) continue;
            _ui.Fill(b, new Rectangle(px - w / 2, py - w / 2, w, w), ink);
        }
    }

    // ── THE EMPTY VAULT ──────────────────────────────────────────────────────────────────────────
    //
    // It said, in UiInk.Rule — the HAIRLINE colour, which the standard forbids as text outright —
    // "No chests. Bosses drop them — about one boss in five", with the five typed by hand, and then
    // explained a peek glass that no longer exists. It has also never been photographed: no fixture
    // could empty the pile. Both are fixed here.
    //
    // The two acquisition lines are exhaustive and checked: every AddChest call site in the game is
    // the boss drop, the new-game gift seed, the save restore, or a capture fixture. The Warren pays
    // Gleam, Dust, Scrap and Essence and the trader sells items — neither ever mints a chest — so
    // "and through progression" would have been a lie.

    /// <summary>The empty room's chest picture — the card's chest, a little wider and lower.</summary>
    private static int EmptyArtW => UiMetrics.Control(180);
    private static int EmptyArtH => UiMetrics.Control(140);

    /// <summary>The empty room's door out: the screen's one primary while there is nothing to open.</summary>
    private static int EmptyButtonW => UiMetrics.Control(320);
    private static int EmptyButtonH => UiMetrics.Control(60);

    /// <summary>Where the empty room's title and its first line sit, under the picture.</summary>
    private static int EmptyTitleTop => GridTop + EmptyArtH + UiMetrics.Space(16);
    private static int EmptyLinesTop => EmptyTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(4);

    /// <summary>How many lines the empty room says: where chests come from, plus your filter if it is eating them.</summary>
    private int EmptyLineCount => KeepMinTier > 0 || KeepSlots.Count > 0 ? 3 : 2;

    /// <summary>
    /// The empty vault's panel: one card row deep, as it always was — or deeper, when the lines and
    /// the door under them need more than a row, so the door never prints through the last line.
    /// </summary>
    private Rectangle EmptyFrame
    {
        get
        {
            var need = EmptyLinesTop - PanelTop + EmptyLineCount * UiTypography.Pitch(UiTypography.Body)
                       + UiMetrics.Space(12) + EmptyButtonH + UiTypography.PanelPadBottom + UiKit.PanelCorner;
            var panel = PanelAt(1);
            return new Rectangle(panel.X, panel.Y, panel.Width, Math.Max(panel.Height, need));
        }
    }

    private void DrawEmpty(SpriteBatch b, Rectangle frame, Point hit, bool clicked)
    {
        if (_ui.Assets.Get("chest_loot") is { } art)
            _ui.SpriteFit(b, art, new Rectangle(frame.Center.X - EmptyArtW / 2, GridTop, EmptyArtW, EmptyArtH), UiInk.Empty);

        _ui.TextCenterBig(b, "THE VAULT IS EMPTY", frame.Center.X, EmptyTitleTop, Bone, UiTypography.PanelTitle);

        var y = EmptyLinesTop;
        var oneIn = (int)MathF.Round(1f / MathF.Max(0.01f, Chests.DropChance(0)));
        _ui.TextCenterBig(b, $"CHESTS COME FROM BOSSES — ABOUT ONE BOSS IN {oneIn} DROPS ONE.",
                          frame.Center.X, y, Slate, UiTypography.Body);
        y += UiTypography.Pitch(UiTypography.Body);
        _ui.TextCenterBig(b, "AND FROM GIFTS — A NEW GAME IS GIVEN ONE.",
                          frame.Center.X, y, Slate, UiTypography.Body);

        // Your own filter, if it is throwing chests away, said in the room where they would have been.
        if (KeepMinTier > 0 || KeepSlots.Count > 0)
        {
            var parts = new List<string>();
            if (KeepMinTier > 0) parts.Add($"TIER {KeepMinTier} AND UP");
            if (KeepSlots.Count > 0)
                parts.Add("CHESTS MADE FOR " + string.Join(" OR ", KeepSlots.Select(SlotLabel)));
            y += UiTypography.Pitch(UiTypography.Body);
            _ui.TextCenterBig(b, "YOUR CHEST FILTER KEEPS ONLY " + string.Join(" AND ", parts)
                                 + ". THE REST ARRIVE AS A LITTLE SCRAP.",
                              frame.Center.X, y, Slate, UiTypography.Body);
        }

        // With no chests there is no OPEN ALL, so THIS is the screen's one primary action — a door
        // out, rather than a room with nothing in it and no way on.
        var door = new Rectangle(frame.Center.X - EmptyButtonW / 2, UiKit.ContentBottom(frame) - EmptyButtonH,
                                 EmptyButtonW, EmptyButtonH);
        if (_ui.Button(b, door, "RETURN TO HUNT", hit, clicked, true, ButtonStyle.Primary))
            WantsHunt = true;
    }

    // ── THE WANDERING TRADER — the weekly stall. Identity from the week, level from the buyer,
    //    prices in materials. See Core's WanderingTrader for the design reasoning. ────────────────

    /// <summary>Where the stall stands at 100 %; at a larger profile it rises to keep its four cards whole.</summary>
    private const int TraderTop = 170;

    /// <summary>An offer card's height at 100 %, and the stall's room under the cards.</summary>
    private static int TraderCardH => UiMetrics.Control(470);
    private static int TraderFoot => TraderFootMin;   // the cards are the panel; nothing lived in a 134 px foot

    /// <summary>
    /// The least room the stall keeps under its cards: the frame's own bottom clearance. The 100 %
    /// foot is 134 px of air; when the page cannot hold the stall at its natural height the air goes
    /// first and the cards keep their height, so BUY never rises to meet the price lines above it.
    /// </summary>
    private static int TraderFootMin => UiTypography.PanelPadBottom + UiKit.PanelCorner;

    /// <summary>
    /// The highest a modal's top may sit, in page space. The host's currency pills and settings gear
    /// are live chrome drawn over everything (their capsules end at canvas y 70, the gear at 76 — see
    /// <c>Game1.DrawCurrencyPills</c> / <c>SettingsGear</c>), and the page is presented under them at
    /// <c>Game1.BaseOverlayScale</c>. At 150 % the stall clamps to the page's height and, anchored at
    /// the 40 px margin, put its CLOSE against the materials pill — a button under a button. 96 lands
    /// at canvas 86, where the host's own hint banner already sits "just under the capsules".
    /// </summary>
    private const int ModalCeiling = 96;

    private void DrawTrader(SpriteBatch b, Point hit, bool clicked)
    {
        if (!_traderOpen) return;

        _ui.Scrim(b, 0.72f);
        var pw = Math.Min(UiMetrics.Control(1320), UiKit.Page.Width - 80);
        // The stall's height is what its four cards need: the header, a card at this profile, and the
        // foot — clamped to the room under the chrome, and raised off its 100 % anchor only when it
        // would run past the bottom margin otherwise.
        var probe = new Rectangle(0, 0, pw, UiMetrics.Control(724));
        var captionW = UiKit.ContentRight(probe) - UiKit.ContentLeft(probe);
        var caption = _ui.WrapBig(
            "NEW GOODS EVERY WEEK, THE SAME FOR EVERY HUNTER. YOU PAY IN SCRAP, ESSENCE, CORE OR CRYSTAL.",
            captionW, UiTypography.Secondary);
        var cardsTopOff = UiTypography.PanelCaptionTop + caption.Count * UiTypography.Pitch(UiTypography.Secondary)
                          + UiMetrics.Space(40);
        var ph = Math.Min(cardsTopOff + TraderCardH + TraderFoot, UiKit.PageBottom(40) - ModalCeiling);
        var py = Math.Max(ModalCeiling, Math.Min(TraderTop, (UiKit.Page.Height - ph) / 2));
        var panel = new Rectangle(UiKit.PageCenterX - pw / 2, py, pw, ph);
        _ui.Panel(b, panel, gold: true);

        _ui.TextCenterBig(b, "THE WANDERING TRADER", panel.Center.X, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);
        // The four currencies by name — all four appear in WanderingTrader.PriceOf, so naming them is
        // accurate rather than decorative, and "materials" was a category word for things the player
        // only ever sees called SCRAP, ESSENCE, CORE and CRYSTAL.
        for (var i = 0; i < caption.Count; i++)
            _ui.TextCenterBig(b, caption[i], panel.Center.X,
                              UiKit.CaptionTop(panel) + i * UiTypography.Pitch(UiTypography.Secondary), Slate,
                              UiTypography.Secondary);

        var closeW = UiMetrics.Control(130);
        var closeBtn = new Rectangle(UiKit.ContentRight(panel) - closeW, UiKit.TitleTop(panel), closeW,
                                     UiMetrics.Control(44));
        if (_ui.Button(b, closeBtn, "CLOSE", hit, clicked, true))
        {
            _traderOpen = false;
            return;
        }

        ItemInstance? hoverOffer = null;
        var cardGap = UiMetrics.Space(20);
        var cardsTop = panel.Y + cardsTopOff;
        // The air under the cards gives way before the cards do: at 100 % the foot is the full 134,
        // at 150 % (the stall clamped to the page) it is the frame's clearance and the cards are whole.
        var foot = Math.Max(TraderFootMin, panel.Height - cardsTopOff - TraderCardH);
        var cardH = panel.Bottom - foot - cardsTop;
        var cw = (UiKit.ContentRight(panel) - UiKit.ContentLeft(panel) - 3 * cardGap) / 4;
        var buyH = UiMetrics.Control(54);
        var buyInset = UiMetrics.Space(40);
        for (var i = 0; i < TraderStock.Count && i < 4; i++)
        {
            var offer = TraderStock[i];
            var card = new Rectangle(UiKit.ContentLeft(panel) + i * (cw + cardGap), cardsTop, cw, cardH);
            var grade = RarityColors[(int)offer.Rarity];
            var over = card.Contains(hit);
            if (over) hoverOffer = offer;

            // THE HOUSE PLATE with the rarity as its accent rule, lifting under the pointer — not a
            // hand-filled rectangle with a bar on top (2026-09-06).
            _ui.Plate(b, card, grade);
            if (over) { _ui.Fill(b, card, Color.White * 0.04f); Outline(b, card, Bone, 1); }

            // The card's rows, one under the other: the grade, the name, the level, (a gem's face,)
            // the price. Each step is the line above's pitch plus a breath, so the rows keep their
            // order at every profile instead of meeting in the middle.
            // THE ITEM'S OWN ART, the same renderer GEAR, the FORGE and the VAULT's chest reveal use:
            // an offer used to be a column of words, and a gem the only card with a picture. The same
            // item looks the same everywhere, so the trader cannot sell a stranger.
            var y = card.Y + UiMetrics.Space(18);
            var art = UiMetrics.Control(96);
            var artBox = new Rectangle(card.Center.X - art / 2, y, art, art);
            if (DrawItem is { } draw) draw(b, offer, artBox);
            else _ui.Fill(b, artBox, grade * 0.5f);
            y += art + UiMetrics.Space(10);
            _ui.TextCenterBig(b, offer.Rarity.ToString().ToUpperInvariant(), card.Center.X, y,
                              grade, UiTypography.Caption);
            y += UiTypography.Pitch(UiTypography.Caption) + UiMetrics.Space(2);

            // THE NAME WRAPS RATHER THAN SHRINKS. A loop used to take a long name down toward the
            // Caption floor to fit the column; the profile exists to make text bigger, so a name that
            // does not fit on one line takes a second (brief §17).
            var name = ItemNaming.FullName(offer);
            var nameW = card.Width - UiMetrics.Space(24);
            foreach (var part in _ui.WrapBig(name, nameW, UiTypography.Body))
            {
                _ui.TextCenterBig(b, _ui.ShortenBig(part, nameW, UiTypography.Body), card.Center.X, y, Bone,
                                  UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
            }
            y += UiMetrics.Space(8);
            // Level, and the element where the item has one — a Source colour as an accent, never as prose.
            var levelLine = offer.Element is { } el
                ? $"LEVEL {offer.ItemLevel}  ·  {el.ToString().ToUpperInvariant()}"
                : $"LEVEL {offer.ItemLevel}";
            _ui.TextCenterBig(b, levelLine, card.Center.X, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(12);
            _ui.Fill(b, new Rectangle(card.X + UiMetrics.Space(16), y, card.Width - UiMetrics.Space(32), 1), Dim);
            y += UiMetrics.Space(10);

            // The price, line by line, each in the wallet's verdict colour.
            _ui.TextCenterBig(b, "PRICE", card.Center.X, y, Slate, UiTypography.SectionLabel);
            y += UiTypography.Pitch(UiTypography.SectionLabel) + UiMetrics.Space(4);
            var affordable = true;
            foreach (var (m, amount) in WanderingTrader.PriceOf(offer, TraderTuning.Default))
            {
                var held = Hunter?.MaterialOf(m) ?? 0;
                var enough = held >= amount;
                affordable &= enough;
                _ui.TextCenterBig(b, $"{amount} {m.ToString().ToUpperInvariant()}  (YOU HOLD {held})",
                                  card.Center.X, y, enough ? Bone : Ember, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
            }

            var buyBtn = new Rectangle(card.X + buyInset, card.Bottom - UiMetrics.Space(28) - buyH,
                                       card.Width - buyInset * 2, buyH);
            if (TraderBought.Contains(i))
                // Disabled ink, correctly: it IS unavailable, and its second cue is that where every
                // other card has a BUY button this one has none.
                _ui.TextCenterBig(b, "ALREADY BOUGHT", buyBtn.Center.X,
                                  buyBtn.Y + (buyH - UiTypography.ButtonText) / 2, UiInk.Disabled,
                                  UiTypography.ButtonText);
            else if (_ui.Button(b, buyBtn, "BUY", hit, clicked, affordable && Hunter is not null))
                _traderBuy = i;
        }

        // The full tooltip beside the pointer — the same card the forge would show for it.
        if (hoverOffer is not null)
            ItemTooltip.Draw(_ui, b, hoverOffer, Hunter, new Point(hit.X + 26, hit.Y + 18), UiKit.Page);
    }

    /// <summary>The stat medallion key for a gem — the alias table's gem_&lt;stat&gt; names.</summary>
    private static string GemMedallion(ItemInstance gem) => GemCraft.StatOf(gem) switch
    {
        AffixStat.Damage => "gem_damage", AffixStat.Health => "gem_health",
        AffixStat.SkillRate => "gem_skillrate", AffixStat.Haul => "gem_haul",
        AffixStat.Crit => "gem_crit", _ => "gem_defense",
    };

    // ── SHARE CODES — paste to LOOK. Nothing here can enter the bag; that is the whole deal. ─────

    private void PasteCode()
    {
        var text = ClipboardInterop.Get().Trim();
        _inspectItem = null;
        _inspectBuild = null;
        _inspectError = "";

        if (ShareCodes.LooksLikeItem(text))
        {
            if (ShareCodes.TryDecodeItem(text, out var item, out var err)) _inspectItem = item;
            else _inspectError = err;
        }
        else if (ShareCodes.LooksLikeBuild(text))
        {
            if (ShareCodes.TryDecodeBuild(text, out var build, out var err)) _inspectBuild = build;
            else _inspectError = err;
        }
        else
        {
            _inspectError = text.Length == 0
                ? "YOUR CLIPBOARD IS EMPTY — COPY A CODE FIRST."
                : "THIS IS NOT A SHARE CODE.";
        }
    }

    /// <summary>The share-code cards' one button — OK, CLOSE — and the room it keeps under itself.</summary>
    private static int InspectButtonW => UiMetrics.Control(160);
    private static int InspectButtonH => UiMetrics.Control(48);
    private static int InspectButtonPad => UiMetrics.Space(24);

    /// <summary>The item card's floor: the tooltip inside it does not follow the profile, so a short item still gets a card this deep.</summary>
    private const int ItemCardMinH = 680;

    /// <summary>The build card at 100 %: 800x600, a ratio the frame picker reads as the medium frame (see below).</summary>
    private const int BuildCardW = 800, BuildCardH = 600, BuildCardTop = 230;

    private void DrawInspect(SpriteBatch b, Point hit, bool clicked)
    {
        if (_inspectError.Length > 0)
        {
            _ui.Scrim(b, 0.6f);
            // Sized to the sentence: the message wraps to the card's width and the card is as tall as
            // the wrapped message, its title and its OK need — 800x240 at 100 %, with one line.
            var pw = Math.Min(UiMetrics.Control(800), UiKit.Page.Width - 80);
            var probe = new Rectangle(0, 0, pw, UiMetrics.Control(240));
            var lines = _ui.WrapBig(_inspectError, UiKit.ContentRight(probe) - UiKit.ContentLeft(probe), UiTypography.Body);
            var ph = UiTypography.PanelBodyTop + lines.Count * UiTypography.Pitch(UiTypography.Body)
                     + UiMetrics.Space(44) + InspectButtonH + UiMetrics.Space(28);
            var panel = new Rectangle(UiKit.PageCenterX - pw / 2, (UiKit.Page.Height - ph) / 2, pw, ph);
            _ui.Panel(b, panel);
            _ui.TextCenterBig(b, "THE CODE DIDN'T OPEN", panel.Center.X, UiKit.TitleTop(panel), Ember, UiTypography.PanelTitle);
            for (var i = 0; i < lines.Count; i++)
                _ui.TextCenterBig(b, lines[i], panel.Center.X,
                                  UiKit.BodyTop(panel) + i * UiTypography.Pitch(UiTypography.Body), Bone, UiTypography.Body);
            var ok = new Rectangle(panel.Center.X - InspectButtonW / 2, panel.Bottom - UiMetrics.Space(28) - InspectButtonH,
                                   InspectButtonW, InspectButtonH);
            if (_ui.Button(b, ok, "OK", hit, clicked, true))
                _inspectError = "";
            return;
        }

        if (_inspectItem is { } item)
        {
            _ui.Scrim(b, 0.7f);
            // The tooltip sits one caption line and a breath under the caption; the card is as tall
            // as the tooltip needs, never shorter than its 100 % height, never past the page.
            var tipTop = UiTypography.PanelCaptionTop + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(24);
            var need = tipTop + ItemTooltip.HeightFor(item, Hunter) + InspectButtonPad + InspectButtonH + InspectButtonPad;
            var pw = Math.Min(UiMetrics.Control(680), UiKit.Page.Width - 80);
            var ph = Math.Min(Math.Max(need, ItemCardMinH), UiKit.PageBottom(40) - ModalCeiling);
            var panel = new Rectangle(UiKit.PageCenterX - pw / 2, Math.Max(ModalCeiling, (UiKit.Page.Height - ph) / 2),
                                      pw, ph);
            // Fill+Outline, not Panel: this rect is nearly square and the frame picker would grab
            // the square art meant for icons (UiKit.Panel picks by aspect ratio).
            _ui.Fill(b, panel, new Color(0x12, 0x0E, 0x18, 0xF4));
            Outline(b, panel, Gold, 2);
            _ui.TextCenterBig(b, "A FRIEND'S ITEM", panel.Center.X, panel.Y + UiTypography.PanelTitleTop, Gold, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, "YOU CAN ONLY LOOK. IT NEVER JOINS YOUR BAG.", panel.Center.X,
                              panel.Y + UiTypography.PanelCaptionTop, Slate, UiTypography.Secondary);
            ItemTooltip.Draw(_ui, b, item, Hunter,
                             new Point(panel.Center.X - ItemTooltip.Width / 2, panel.Y + tipTop), UiKit.Page);
            var close = new Rectangle(panel.Center.X - InspectButtonW / 2, panel.Bottom - InspectButtonPad - InspectButtonH,
                                      InspectButtonW, InspectButtonH);
            if (_ui.Button(b, close, "CLOSE", hit, clicked, true))
                _inspectItem = null;
            return;
        }

        if (_inspectBuild is { } build)
        {
            _ui.Scrim(b, 0.7f);
            // 800x600, not 620: at 620 the ratio was 1.29 and UiKit.Panel's aspect picker handed
            // this card the SQUARE frame meant for icons — the exact trap the item card above
            // dodges with Fill+Outline. Both edges grow at the same rate, so the ratio holds.
            var pw = Math.Min(UiMetrics.Control(BuildCardW), UiKit.Page.Width - 80);
            var ph = Math.Min(UiMetrics.Control(BuildCardH), UiKit.PageBottom(40) - ModalCeiling);
            var py = Math.Max(ModalCeiling, Math.Min(BuildCardTop, (UiKit.Page.Height - ph) / 2));
            var panel = new Rectangle(UiKit.PageCenterX - pw / 2, py, pw, ph);
            _ui.Panel(b, panel, gold: true);
            _ui.TextCenterBig(b, "A FRIEND'S BUILD", panel.Center.X, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, "READ IT, COPY THE IDEA. YOUR OWN BUILD STAYS THE SAME.", panel.Center.X,
                              UiKit.CaptionTop(panel), Slate, UiTypography.Secondary);

            var y = UiKit.CaptionTop(panel) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(32);
            var x1 = UiKit.ContentLeft(panel) + UiMetrics.Space(20);   // a heading
            var x2 = UiKit.ContentLeft(panel) + UiMetrics.Space(40);   // the rows under it
            var sectionGap = UiMetrics.Space(14);

            // The discipline first — the Nen identity is the headline of any build now.
            var spec = build.Mastery
                .Select(MasteryCatalog.ById)
                .FirstOrDefault(n => n is { Kind: MasteryKind.Specialisation });
            _ui.TextBig(b, spec?.Style is { } f
                            ? $"STYLE: {f.ToString().ToUpperInvariant()}"
                            : "NO STYLE CHOSEN",
                        x1, y, spec is null ? Slate : Gold, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(12);

            _ui.TextBig(b, "SKILLS", x1, y, Slate, UiTypography.SectionLabel);
            y += UiTypography.Pitch(UiTypography.SectionLabel);
            if (build.Skills.Count == 0)
            {
                _ui.TextBig(b, "NO SKILLS", x2, y, UiInk.Empty, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }
            foreach (var s in build.Skills.Take(5))
            {
                var vowName = s.VowId is null ? null
                    : Vows.Catalog.FirstOrDefault(v => v.Id == s.VowId)?.Name ?? s.VowId;
                // The id names the skill outright (code v2); a v1 code still carries the legacy
                // Form pair, which the frozen migration map turns into the skill it always meant.
                var word = SkillCatalogue.Find(s.SkillId)?.Name
                           ?? SkillCatalogue.Find(LegacySkillForm.Resolve(
                                  s.Form, s.Passive ?? LegacySkillForm.IsNaturallyPassive(s.Form)))?.Name
                           ?? "UNKNOWN SKILL";
                var line = word
                           + (vowName is null ? "" : $"  —  {vowName.ToUpperInvariant()}");
                _ui.TextBig(b, line, x2, y, Bone, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }

            y += sectionGap;
            _ui.TextBig(b, "KEYSTONES", x1, y, Slate, UiTypography.SectionLabel);
            y += UiTypography.Pitch(UiTypography.SectionLabel);
            if (build.Keystones.Count == 0)
            {
                _ui.TextBig(b, "NONE CHOSEN", x2, y, UiInk.Empty, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }
            foreach (var k in build.Keystones.Take(3))
            {
                _ui.TextBig(b, Keystones.ById(k)?.Name ?? k.ToUpperInvariant(), x2, y, Bone,
                            UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }

            y += sectionGap;
            _ui.TextBig(b, $"MASTERY: {build.Mastery.Count} NODES TAKEN", x1, y, Slate,
                        UiTypography.SectionLabel);

            var close = new Rectangle(panel.Center.X - InspectButtonW / 2, panel.Bottom - InspectButtonPad - InspectButtonH,
                                      InspectButtonW, InspectButtonH);
            if (_ui.Button(b, close, "CLOSE", hit, clicked, true))
                _inspectBuild = null;
        }
    }

    // ── CHEST FILTER — which chests to keep (UX V2 P1.1: moved here from the HUNT; D12). ────────────────────────
    //    Host-fed and host-persisted; this screen only edits it and raises FilterDirty. The edit happens in
    //    DRAW, so the host reads it back on the dirty flag and never pushes the saved value every frame.
    /// <summary>Chests below this tier never land — they arrive as a little Scrap instead. 0 = all.</summary>
    public int KeepMinTier { get; set; }
    /// <summary>Keep only chests whose region favours ANY of these slots (no-lean chests always pass). Empty = any.</summary>
    public HashSet<ItemBaseType> KeepSlots { get; } = new();
    /// <summary>Set when the player edited the filter — the host copies it back and saves.</summary>
    public bool FilterDirty { get; set; }
    /// <summary>The filter's popover is up. The toolbar button toggles it; its ×, a click outside it, and Escape close it.</summary>
    public bool FilterOpen { get; set; }

    // 326, not 286: the closing sentence was set at Caption — a size the standard allows for a label
    // and forbids for a sentence — because at 286 that was the only way it fitted. It wraps at
    // Secondary now. The height is no longer a number: it is the sum of the rows the popover draws,
    // so a profile that makes the medallions and the tier buttons taller makes the popover taller.
    private static int FilterPopoverW => UiMetrics.Control(326);

    /// <summary>The slot medallions' cell, and the icon inside it at rest and under the pointer.</summary>
    private static int FilterCellH => UiMetrics.Control(60);
    private static int FilterCellGap => UiMetrics.Space(4);
    private static int FilterIconRest => UiMetrics.Control(50);
    private static int FilterIconHot => UiMetrics.Control(54);

    /// <summary>The -/+ tier steppers and ALL SLOTS: small buttons, never under the hit-target floor (§107).</summary>
    private static int FilterStepper => Math.Max(UiMetrics.Control(34), UiMetrics.HitTargetMinimum);
    private static int FilterAllH => Math.Max(UiMetrics.Control(30), UiMetrics.HitTargetMinimum);

    /// <summary>
    /// The popover's height, row by row — the same rows <see cref="DrawFilterPopover"/> walks, with
    /// two lines budgeted for the closing sentence (its longer reading wraps at 100 %).
    /// </summary>
    private static int FilterPopoverH
    {
        get
        {
            var inset = UiTypography.PanelPadNarrow;
            var closeSize = UiMetrics.HitTargetMinimum;
            var pitch = UiTypography.Pitch(UiTypography.Secondary);
            var h = UiKit.PanelCorner - 4 + closeSize + UiMetrics.Space(8);   // the title row, off CloseRect
            h += pitch + UiMetrics.Space(6);                                   // WHICH CHESTS TO KEEP
            h += pitch;                                                        // LOWEST TIER
            h += FilterStepper + UiMetrics.Space(14);                          // - ANY TIER +
            h += pitch;                                                        // GEAR SLOTS THE CHEST IS FOR
            h += 2 * (FilterCellH + FilterCellGap) + UiMetrics.Space(6);       // the medallions
            h += FilterAllH + UiMetrics.Space(10);                             // ALL SLOTS
            h += 2 * pitch;                                                    // the closing sentence
            return h + inset;
        }
    }

    private Rectangle FilterPopover =>
        new(Math.Min(FilterBtn.X, UiKit.PageRight(24) - FilterPopoverW), FilterBtn.Bottom + UiMetrics.Space(10),
            FilterPopoverW, FilterPopoverH);

    private static readonly ItemBaseType[] SlotChips =
    {
        ItemBaseType.Weapon, ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves,
        ItemBaseType.Boots, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Ring,
    };

    private static string SlotLabel(ItemBaseType t) => t switch
    {
        ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.Chest => "ARMOUR",
        _ => t.ToString().ToUpperInvariant(),
    };

    private static string SlotIconKey(ItemBaseType t) => t switch
    {
        ItemBaseType.AbilityFocus => "item_slot_focus",
        _ => "item_slot_" + t.ToString().ToLowerInvariant(),
    };

    private static string SlotTip(ItemBaseType t) => t switch
    {
        ItemBaseType.Weapon => "WEAPON — what your hunter strikes with.",
        ItemBaseType.Helm => "HELM — head armour.",
        ItemBaseType.Chest => "ARMOUR — body armour, worn on the chest.",
        ItemBaseType.Gloves => "GLOVES — hand armour.",
        ItemBaseType.Boots => "BOOTS — foot armour.",
        ItemBaseType.Charm => "CHARM — a trinket worn for its power.",
        ItemBaseType.AbilityFocus => "FOCUS — the piece that channels your skills.",
        ItemBaseType.Ring => "RING — a ring worn for its power.",
        _ => SlotLabel(t),
    };

    /// <summary>The popover, when it is up; a click anywhere outside it (and off its button) closes it.</summary>
    private void DrawFilterIfOpen(SpriteBatch b, Point hit, bool clicked)
    {
        if (!FilterOpen) return;
        var pop = FilterPopover;
        if (clicked && !pop.Contains(hit) && !FilterBtn.Contains(hit)) { FilterOpen = false; return; }
        DrawFilterPopover(b, pop, hit, clicked && pop.Contains(hit));
    }

    /// <summary>The filter's controls: the lowest tier to keep, and the gear slots — as medallions.</summary>
    private void DrawFilterPopover(SpriteBatch b, Rectangle pop, Point hit, bool clicked)
    {
        var inset = UiTypography.PanelPadNarrow;
        var pitch = UiTypography.Pitch(UiTypography.Secondary);
        _ui.PanelQuiet(b, pop);
        var inner = new Rectangle(pop.X + inset, pop.Y + inset, pop.Width - inset * 2, pop.Height - inset * 2);
        var close = UiKit.CloseRect(pop, UiMetrics.HitTargetMinimum);
        _ui.TextBig(b, "CHEST FILTER", inner.X, close.Y + (close.Height - UiTypography.PanelTitle) / 2, Gold, UiTypography.PanelTitle);
        if (_ui.CloseButton(b, close, hit, clicked)) FilterOpen = false;
        var y = close.Bottom + UiMetrics.Space(8);
        _ui.TextBig(b, "WHICH CHESTS TO KEEP", inner.X, y, Slate, UiTypography.Secondary);
        y += pitch + UiMetrics.Space(6);

        _ui.TextBig(b, "LOWEST TIER", inner.X, y, Bone, UiTypography.Secondary);
        y += pitch;
        var stepper = FilterStepper;
        var minus = new Rectangle(inner.X, y, stepper, stepper);
        var plus = new Rectangle(inner.Right - stepper, y, stepper, stepper);
        MiniButton(b, minus, "-", hit);
        MiniButton(b, plus, "+", hit);
        _ui.TextCenter(b, KeepMinTier <= 0 ? "ANY TIER" : $"TIER {KeepMinTier} AND UP", inner.Center.X,
                       y + (stepper - UiTypography.Label) / 2, KeepMinTier > 0 ? Gold : Slate);
        if (UiKit.ClickedIn(minus, hit, clicked) && KeepMinTier > 0) { KeepMinTier -= 1; FilterDirty = true; }
        if (UiKit.ClickedIn(plus, hit, clicked) && KeepMinTier < 99) { KeepMinTier += 1; FilterDirty = true; }

        y += stepper + UiMetrics.Space(14);
        _ui.TextBig(b, "GEAR SLOTS THE CHEST IS FOR", inner.X, y, Bone, UiTypography.Secondary);
        y += pitch;
        string? tip = null;
        var cellH = FilterCellH;
        var cellW = inner.Width / 4;
        for (var i = 0; i < SlotChips.Length; i++)
        {
            var slot = SlotChips[i];
            var cell = new Rectangle(inner.X + (i % 4) * cellW, y + (i / 4) * (cellH + FilterCellGap), cellW, cellH);
            var lit = KeepSlots.Contains(slot);
            var hot = cell.Contains(hit);
            if (lit) _ui.Fill(b, cell, Gold * 0.16f);
            // PRESSED: the cell darkens under the held button; the medallion keeps its hover size.
            if (hot && Held) _ui.Fill(b, cell, Color.Black * 0.18f);
            var iconEdge = hot ? FilterIconHot : FilterIconRest;
            var box = new Rectangle(cell.Center.X - iconEdge / 2, cell.Center.Y - iconEdge / 2, iconEdge, iconEdge);
            var tint = lit || hot ? Color.White : new Color(0x8C, 0x86, 0x80);
            if (!_ui.Icon(b, SlotIconKey(slot), box, tint))
                _ui.TextCenterBig(b, SlotLabel(slot), cell.Center.X, cell.Center.Y - UiTypography.Caption / 2, tint, UiTypography.Caption);
            if (lit) Outline(b, cell, Gold * 0.8f, 2);
            if (hot) tip = SlotTip(slot) + (lit ? " Click to stop keeping its chests." : " Click to keep the chests made for it.");
            if (UiKit.ClickedIn(cell, hit, clicked))
            {
                if (!KeepSlots.Remove(slot)) KeepSlots.Add(slot);
                FilterDirty = true;
            }
        }
        y += 2 * (cellH + FilterCellGap) + UiMetrics.Space(6);
        var all = new Rectangle(inner.X, y, inner.Width, FilterAllH);
        MiniButton(b, all, "ALL SLOTS", hit, KeepSlots.Count == 0);
        if (UiKit.ClickedIn(all, hit, clicked) && KeepSlots.Count > 0) { KeepSlots.Clear(); FilterDirty = true; }
        y += FilterAllH + UiMetrics.Space(10);
        foreach (var l in _ui.WrapBig(KeepMinTier > 0 || KeepSlots.Count > 0
                                          ? "OTHER CHESTS TURN INTO A LITTLE SCRAP"
                                          : "EVERY CHEST IS KEPT",
                                      inner.Width, UiTypography.Secondary))
        {
            _ui.TextBig(b, l, inner.X, y, Slate, UiTypography.Secondary);
            y += pitch;
        }
        if (tip is not null) _ui.HoverTip(b, tip, hit);
    }

    private void MiniButton(SpriteBatch b, Rectangle r, string label, Point hit, bool lit = false)
    {
        var hot = r.Contains(hit);
        // The same states as every control (§25): HOVER lifts the edge and the ink a value step,
        // PRESSED darkens the well while the button is held, LIT (selected) is gold and stays.
        var pressed = hot && Held;
        _ui.Fill(b, r, new Color(0x14, 0x10, 0x1A, 0xE0));
        if (pressed) _ui.Fill(b, r, Color.Black * 0.18f);
        Outline(b, r, lit ? Gold * 0.8f : hot ? Bone : Dim, 2);
        _ui.TextCenterBig(b, label, r.Center.X, r.Y + (r.Height - UiTypography.Secondary) / 2 - 1 + (pressed ? 1 : 0),
                          lit ? Gold : hot ? Bone : Slate, UiTypography.Secondary);
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }


    // DrawDossierTip is gone with UX V2 P1.8. It was a 470 px panel holding the whole dossier
    // behind a 0.4 s hover on a 32 px glass — the documentation-in-a-tooltip pattern the brief
    // rejects. Every line it drew, the region blurb included, is on the card now.

    /// <summary>A rectangle scaled about its own centre.</summary>
    private static Rectangle Grow(Rectangle r, float scale)
    {
        var w = (int)(r.Width * scale);
        var h = (int)(r.Height * scale);
        return new Rectangle(r.Center.X - w / 2, r.Center.Y - h / 2, w, h);
    }
}
