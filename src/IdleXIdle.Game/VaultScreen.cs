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

    private int _cursor;     // which SORTED chest a click chose — the host reads it via SelectedIndex
    private int _scroll;     // first visible card, in steps of a row
    private float _anim;

    // Hover state, tracked in Update (where dt lives) and read by Draw the same frame.
    private int _hoverIdx = -1;    // card under the pointer, or -1
    private float _hoverT;         // eased 0..1 — ~120ms in, ~80ms out

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
    private const int PanelTop = 150;

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
    /// pile behind it did not, and only a stacked chest in those positions showed it.
    /// </remarks>
    private const int PileDepth = 14;

    /// <summary>The toolbar's controls — OPEN ALL, CHEST FILTER, TRADER, PASTE A CODE — all one height.</summary>
    private const int ToolbarH = 56;

    /// <summary>The gap between the toolbar's buttons.</summary>
    private const int HeaderButtonGap = 16;

    // THE TOOLBAR. Rectangles derived from ONE anchor, so the empty vault and the full one put the
    // same button in the same place. They used to be hand-placed off GridPanel.Right at -770, -558
    // and -346 — three magic numbers kept in step by hand, which ignored the panel's own margin.
    private static Rectangle HeaderButton(int slotFromRight, int width)
    {
        var x = UiKit.ContentRight(Frame);
        for (var i = 0; i < slotFromRight; i++) x -= HeaderWidths[i] + HeaderButtonGap;
        return new Rectangle(x - width, UiKit.TitleTop(Frame), width, ToolbarH);
    }

    /// <summary>Right to left: OPEN ALL, CHEST FILTER, TRADER. Widths sized to their own labels.</summary>
    private static readonly int[] HeaderWidths = { 340, 240, 200 };

    private static Rectangle OpenAllBtn => HeaderButton(0, HeaderWidths[0]);

    /// <summary>The CHEST FILTER's door — the keep-filter moved here from the HUNT (UX V2 P1.1, D12).</summary>
    private static Rectangle FilterBtn => HeaderButton(1, HeaderWidths[1]);

    private static Rectangle TraderBtn => HeaderButton(2, HeaderWidths[2]);

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
        new(UiKit.ContentLeft(Frame), UiKit.TitleTop(Frame), 240, ToolbarH);

    // THE GRID. Two columns of very large cards, not six of very small ones.
    //
    // Measured before the change: 58 % of the canvas empty, the chest's PROMISE — the one fact worth
    // a click — set at the smallest rung on the screen and truncated to "EPIC OR BE...", and a first
    // visit showing one 242 px card marooned in a 1650 px panel. The pile is small (a vault holds
    // stacks, not hundreds), so the cards can be enormous: at 884x349 the whole dossier fits beside
    // the chest at Body, with room for the region's blurb underneath.
    private const int Cols = 2, CardGapX = 24, CardGapY = 20;
    private const int MinCardH = 300, MaxCardH = 380;

    /// <summary>The first card row — under the toolbar, its rule, and the consequence row.</summary>
    private static int CardTop => UiTypography.PanelTitleTop + ToolbarH + 14 + 2 + 10
                                  + UiTypography.Pitch(UiTypography.Secondary) + 4;

    private static int GridTop => PanelTop + CardTop;
    private static int GridBottom => UiKit.PageBottom(40) - UiKit.PanelCorner;

    /// <summary>Two rows when the page can hold two whole cards; one when it cannot (UI SCALE 150 %).</summary>
    private static int Rows => GridBottom - GridTop - PileDepth >= MinCardH * 2 + CardGapY ? 2 : 1;

    private static int CardH =>
        Math.Min(MaxCardH, (GridBottom - GridTop - CardGapY * (Rows - 1) - PileDepth) / Rows);
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
                return new[] { new Rectangle(Frame.X + 30, GridTop - 12, Frame.Width - 60, CardH + 24) };
            case TourTarget.VaultButtons:
                var strip = PasteRect;
                strip.Inflate(10, 10);
                return new[] { new Rectangle(strip.X, strip.Y, OpenAllBtn.Right + 10 - strip.X, ToolbarH + 20) };
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
    private static int MaxScroll(int count) => Math.Max(0, (count + Cols - 1) / Cols * Cols - PerPage);

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

        // Authored 1920, cursor arrives 480 — the same one line every inset screen carries.
        var hit = mouse;

        // One card per STACK of identical chests (ChestDossiers.Stacked); the grid, the hover and the
        // cursor all count stacks. `sorted` is the stacks' samples in best-first order.
        var stacks = ChestDossiers.Stacked(chests);
        var sorted = stacks.Select(st => st.Sample).ToList();
        Clamp(sorted.Count);

        // While the stall or an inspect card is open, the grid underneath is furniture: no hover,
        // no wheel, and — decisive — no chest-opening click. The modals' own buttons live in Draw.
        if (ModalOpen) { _hoverIdx = -1; return; }

        if (wheel != 0)
            _scroll = Math.Clamp(_scroll - Math.Sign(wheel) * Cols, 0, MaxScroll(sorted.Count));

        // Hover, resolved here where dt lives. There is nothing on the card to hover SEPARATELY any
        // more: the peek glass that used to win over the card it sat on is gone, and with it the rule
        // that the card's OPEN affordance stood down while the pointer was on it.
        var overCard = -1;
        var onPage = OnPage(sorted.Count);
        for (var vis = 0; vis < onPage; vis++)
        {
            if (!Card(vis, onPage).Contains(hit)) continue;
            overCard = _scroll + vis;
            break;
        }

        if (overCard != _hoverIdx) _hoverT = 0f;
        _hoverIdx = overCard;
        _hoverT = _hoverIdx >= 0 ? MathF.Min(1f, _hoverT + dt / 0.12f) : 0f;

        if (!clicked) return;

        // THE CHEST IS THE BUTTON. A click anywhere on the card opens THAT chest — including on the
        // OPEN chip, which is a label on the card and deliberately not a second click source.
        if (overCard >= 0)
        {
            _cursor = overCard;
            SelectedChest = sorted[overCard];
            _pending = OpenRequest.Selected;
        }
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
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));
        // VAULT, without the article — the rail says VAULT and D7 settled that the screen agrees.
        _ui.TextCenterBig(b, "VAULT", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);

        // The tally, so the pile reads at a glance without counting cards.
        var tally = ChestDossiers.Tally(chests);   // every chest, not every stack
        var summary = tally.Count == 0
            ? "nothing waiting"
            : string.Join("   ", tally.Select(t => $"{t.Count} {t.Grade.ToString().ToUpperInvariant()}"));
        _ui.TextCenterBig(b, summary.ToUpperInvariant(), UiKit.PageCenterX, 80, Slate, UiTypography.Secondary);

        // QUIET, NOT GOLD. The house frame rule (UiKit.PanelQuiet): the gold filigree is for MODALS —
        // the stall and the two share-code cards below still wear it — and every panel that lives IN a
        // screen wears the brown. This was the last in-screen panel in the game still shouting.
        var rows = Math.Clamp((sorted.Count + Cols - 1) / Cols, 1, Rows);
        var frame = PanelAt(rows);
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

        if (_ui.Button(b, TraderBtn, "TRADER", hit, uiClicked, TraderStock.Count > 0))
        {
            _traderOpen = true;
            _modalOpenedNow = true;
        }

        var pasteHot = PasteRect.Contains(hit) && !ModalOpen;
        _ui.Plate(b, PasteRect);
        Outline(b, PasteRect, pasteHot ? Slate : Dim, 1);
        _ui.TextCenterBig(b, "PASTE A CODE", PasteRect.Center.X,
                          PasteRect.Y + (ToolbarH - UiTypography.ButtonText) / 2 - 2,
                          pasteHot ? Bone : Slate, UiTypography.ButtonText);
        if (UiKit.ClickedIn(PasteRect, hit, uiClicked))
        {
            PasteCode();
            _modalOpenedNow = true;
        }
        if (pasteHot)
            _ui.HoverTip(b, "READ AN ITEM OR BUILD CODE SOMEONE SHARED WITH YOU. IT NEVER JOINS YOUR BAG.", hit);

        // The hairline the toolbar stands on, so the buttons read as a header and the cards below as
        // the panel's contents — one rule instead of a gap the eye has to guess at.
        var strip = new Rectangle(UiKit.ContentLeft(frame), UiKit.TitleTop(frame) + ToolbarH + 14,
                                  UiKit.ContentRight(frame) - UiKit.ContentLeft(frame), 2);
        _ui.Fill(b, strip, Dim);

        // ── THE CONSEQUENCE ROW. What OPEN ALL will do to the drops BEFORE you see them, said once,
        //    on a reserved row — not as a confirmation dialog on every click (brief §51, §85).
        //
        //    Both traits are Memory Dust purchases, and the row names them the way the tree does, so
        //    it reads as "the thing you bought is on" rather than as a warning from nowhere. "GEAR",
        //    not "items": LandChest sells only wearable pieces and deliberately spares gems. And "CAN
        //    GIVE", not "will": Chests.Open elevates a roll UP to the chest's floor and never down, so
        //    a chest whose floor is at or under the sell line can still produce a sellable piece. ────
        var conseqY = strip.Bottom + 10;
        var clauses = new List<string>();
        if (AutoSellFloor is { } floor)
        {
            var n = stacks.Where(s => ChestDossiers.For(s.Sample).GuaranteedFloor <= floor).Sum(s => s.Count);
            if (n > 0)
                clauses.Add($"AUTO-SELL {floor.ToString().ToUpperInvariant()} DROPS IS ON — {n} OF THESE "
                            + (n == 1 ? "CHESTS CAN GIVE GEAR THAT IS SOLD THE MOMENT IT DROPS."
                                      : "CHESTS CAN GIVE GEAR THAT IS SOLD THE MOMENT IT DROPS."));
        }
        if (AutoMergeOnOpen) clauses.Add("AUTO-MERGE SPARE ITEMS IS ON.");
        if (clauses.Count > 0)
            _ui.TextBig(b, _ui.ShortenBig(string.Join("   ·   ", clauses), ContentW - 460, UiTypography.Secondary),
                        UiKit.ContentLeft(frame), conseqY, Slate, UiTypography.Secondary);

        // The page position, on the same row's right end — and a scrollbar-less screen has to say in
        // words what moves it, because there is no bar to drag.
        if (sorted.Count > PerPage)
            _ui.TextRightBig(b, $"PAGE {_scroll / PerPage + 1} OF {(sorted.Count + PerPage - 1) / PerPage}"
                                + "  —  THE MOUSE WHEEL SCROLLS",
                             UiKit.ContentRight(frame), conseqY, Slate, UiTypography.Secondary);

        if (sorted.Count == 0)
        {
            DrawEmpty(b, frame, hit, uiClicked);
            DrawFilterIfOpen(b, hit, clicked && !_modalOpenedNow);
            DrawTrader(b, hit, clicked && !_modalOpenedNow);
            DrawInspect(b, hit, clicked && !_modalOpenedNow);
            _modalOpenedNow = false;
            return;
        }

        var onPage = OnPage(sorted.Count);
        for (var vis = 0; vis < onPage; vis++)
            DrawCard(b, sorted[_scroll + vis], stacks[_scroll + vis].Count, Card(vis, onPage),
                     _scroll + vis == _hoverIdx);

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
    private void DrawCard(SpriteBatch b, Chest chest, int stackCount, Rectangle card, bool hovered)
    {
        var d = ChestDossiers.For(chest);
        var grade = RarityColors[(int)chest.Rarity];
        // REDUCED MOTION keeps the value step and drops the movement: the card still brightens under
        // the pointer, it just does not grow or ease into it.
        var ease = hovered ? Game1.ReducedMotion ? 1f : _hoverT * _hoverT * (3f - 2f * _hoverT) : 0f;   // smoothstep

        // A STACK LOOKS LIKE A STACK. Two identical chests were one card with a small "x2" in the
        // corner, and testers did not see it (playtest 2026-08-25). Now the card sits on two offset
        // card backs, the way a pile of cards does, before anything else on it is drawn.
        for (var back = Math.Min(2, stackCount - 1); back >= 1; back--)
        {
            var off = new Rectangle(card.X + back * 7, card.Y + back * 7, card.Width, card.Height);
            _ui.Fill(b, off, new Color(0x0E, 0x0C, 0x14));
            _ui.Fill(b, new Rectangle(off.X, off.Y, off.Width, 5), grade * 0.45f);
            _ui.Fill(b, new Rectangle(off.Right - 2, off.Y, 2, off.Height), grade * 0.25f);
            _ui.Fill(b, new Rectangle(off.X, off.Bottom - 2, off.Width, 2), grade * 0.25f);
        }

        // The QUIET tier, which is what this is: a plate inside a panel. It used to hand-draw the
        // plate that UiKit.Plate exists to be. Hover brightens the EDGE one value step — never a new
        // hue (art bible chrome rules).
        _ui.Plate(b, card);
        _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 6), grade);
        if (hovered)
        {
            var edge = Bone * (0.35f + 0.45f * ease);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - 2, card.Width, 2), edge);
            _ui.Fill(b, new Rectangle(card.X, card.Y, 2, card.Height), edge);
            _ui.Fill(b, new Rectangle(card.Right - 2, card.Y, 2, card.Height), edge);
        }

        // THE CHEST IS THE PICTURE, lifted toward white so grade never decides how VISIBLE it is
        // (SpriteBatch tint multiplies). On hover it grows 6% about its own centre.
        var icon = Grow(new Rectangle(card.X + 24, card.Y + 40, ArtSize, ArtSize),
                        Game1.ReducedMotion ? 1f : 1f + 0.06f * ease);
        if (_ui.Assets.Get("chest_loot") is { } chestArt)
            _ui.SpriteFit(b, chestArt, icon, Color.Lerp(grade, Color.White, 0.45f + 0.1f * ease));
        else _ui.Diamond(b, icon, grade);

        // The stack count as a gold badge on the pile itself. A click opens ONE of them; OPEN ALL
        // still opens every chest. Playtest: "ayni chestler stacklensin."
        if (stackCount > 1)
        {
            var badge = new Rectangle(card.X + 16, card.Y + 24, 76, 36);
            _ui.Fill(b, badge, Gold);
            _ui.Fill(b, new Rectangle(badge.X + 2, badge.Y + 2, badge.Width - 4, badge.Height - 4),
                     new Color(0x3A, 0x2A, 0x10));
            _ui.TextCenterBig(b, $"×{stackCount}", badge.Center.X, badge.Y + 4, Gold, UiTypography.Headline);
        }

        // The pip row: grade as a COUNT, readable in greyscale. Common one pip, Legendary five.
        for (var pip = 0; pip < 5; pip++)
        {
            var dot = new Rectangle(card.X + 24 + pip * 30, card.Y + 214, 18, 18);
            _ui.Diamond(b, dot, pip <= (int)chest.Rarity ? grade : Dim);
        }

        // OPEN — the label the click fulfils, always visible rather than fading in on hover. It is
        // NOT a button: the whole card commits, and a second click source inside it would open two
        // chests on one click. It lights with the CARD, which is what you are actually clicking.
        var chip = new Rectangle(card.X + 24, card.Bottom - 24 - ChipH, 160, ChipH);
        _ui.Plate(b, chip);
        Outline(b, chip, hovered ? Gold : Dim, 2);
        _ui.TextCenterBig(b, stackCount > 1 ? "OPEN ONE" : "OPEN", chip.Center.X,
                          chip.Y + (ChipH - UiTypography.ButtonText) / 2 - 2,
                          hovered ? Gold : Gold * 0.75f, UiTypography.ButtonText);

        // ── The text column. ─────────────────────────────────────────────────────────────────────
        var tx = card.X + 24 + ArtSize + 20;
        var tw = card.Right - 24 - tx;

        // The kind of chest, in its grade's colour — "EPIC CHEST", or a gift's own title.
        _ui.TextBig(b, d.Title + (stackCount > 1 ? $" · {stackCount} WAITING" : ""),
                    tx, card.Y + 40, grade, UiTypography.Secondary);

        // The element as its SOURCE GLYPH — the same art the skills and the map use, right-aligned on
        // the title's row. The coloured diamond stays as the fallback for a glyph not on disk.
        if (chest.Element is { } e)
        {
            var glyph = new Rectangle(card.Right - 24 - 28, card.Y + 36, 28, 28);
            if (!_ui.Icon(b, SourceGlyphKey(e), glyph, Color.White))
                _ui.Diamond(b, new Rectangle(glyph.X + 4, glyph.Y + 4, glyph.Width - 8, glyph.Height - 8),
                            SourceColor.GetValueOrDefault(e, Slate));
        }

        _ui.TextBig(b, $"TIER {chest.Tier}", tx, card.Y + 66, Bone, UiTypography.PrimaryValue);

        // THE DOSSIER, on the card. The first line is the PROMISE — the fact worth the click — so it
        // takes Good when there is one and Secondary when the honest answer is "any rarity, a
        // gamble". Never Dim: "there isn't a promise" is a fact, not a disabled control.
        var y = card.Y + 120;
        var bottom = card.Bottom - 24;
        var step = UiTypography.Pitch(UiTypography.Body);
        var lines = d.Lines;
        for (var li = 0; li < lines.Count && y + step <= bottom; li++)
        {
            var ink = li == 0
                ? (d.IsGift || d.GuaranteedFloor > Rarity.Common ? UiInk.Good : Slate)
                : Bone;
            foreach (var wrapped in _ui.WrapBig(lines[li], tw, UiTypography.Body))
            {
                // The line budget is what keeps the card HONEST at UI SCALE 150 %, where the card is
                // 358 tall: it stops emitting rather than drawing a fact through the OPEN chip.
                if (y + step > bottom) break;
                _ui.TextBig(b, wrapped, tx, y, ink, UiTypography.Body);
                y += step;
            }
        }

        // The region's own voice — the last thing the deleted tooltip said that the card did not.
        var blurbStep = UiTypography.Pitch(UiTypography.Secondary);
        if (!string.IsNullOrWhiteSpace(d.RegionBlurb) && y + 18 + blurbStep <= bottom)
        {
            _ui.Fill(b, new Rectangle(tx, y + 8, tw, 1), Dim);
            y += 18;
            foreach (var wrapped in _ui.WrapBig(d.RegionBlurb, tw, UiTypography.Secondary))
            {
                if (y + blurbStep > bottom) break;
                _ui.TextBig(b, wrapped, tx, y, Slate, UiTypography.Secondary);
                y += blurbStep;
            }
        }
    }

    /// <summary>The chest art's box, and the OPEN chip's height — the card's two fixed shapes.</summary>
    private const int ArtSize = 160, ChipH = 56;   // ui-size-ok: an art box and a chip, not a text pitch

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
    private void DrawEmpty(SpriteBatch b, Rectangle frame, Point hit, bool clicked)
    {
        if (_ui.Assets.Get("chest_loot") is { } art)
            _ui.SpriteFit(b, art, new Rectangle(frame.Center.X - 90, GridTop, 180, 140), UiInk.Empty);

        _ui.TextCenterBig(b, "THE VAULT IS EMPTY", frame.Center.X, GridTop + 156, Bone, UiTypography.PanelTitle);

        var y = GridTop + 196;
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
        if (_ui.Button(b, new Rectangle(frame.Center.X - 160, UiKit.ContentBottom(frame) - 60, 320, 60),
                       "RETURN TO HUNT", hit, clicked, true, ButtonStyle.Primary))
            WantsHunt = true;
    }

    // ── THE WANDERING TRADER — the weekly stall. Identity from the week, level from the buyer,
    //    prices in materials. See Core's WanderingTrader for the design reasoning. ────────────────

    private void DrawTrader(SpriteBatch b, Point hit, bool clicked)
    {
        if (!_traderOpen) return;

        _ui.Scrim(b, 0.72f);
        var pw = Math.Min(1320, UiKit.Page.Width - 80);
        var ph = Math.Min(724, UiKit.PageBottom(40) - 170);
        var panel = new Rectangle(UiKit.PageCenterX - pw / 2, 170, pw, ph);
        _ui.Panel(b, panel, gold: true);

        _ui.TextCenterBig(b, "THE WANDERING TRADER", panel.Center.X, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);
        // The four currencies by name — all four appear in WanderingTrader.PriceOf, so naming them is
        // accurate rather than decorative, and "materials" was a category word for things the player
        // only ever sees called SCRAP, ESSENCE, CORE and CRYSTAL.
        _ui.TextCenterBig(b, "NEW GOODS EVERY WEEK, THE SAME FOR EVERY HUNTER. YOU PAY IN SCRAP, ESSENCE, CORE OR CRYSTAL.",
                          panel.Center.X, UiKit.CaptionTop(panel), Slate, UiTypography.Secondary);

        if (_ui.Button(b, new Rectangle(panel.Right - 170, UiKit.TitleTop(panel), 130, 44), "CLOSE", hit, clicked, true))
        {
            _traderOpen = false;
            return;
        }

        ItemInstance? hoverOffer = null;
        for (var i = 0; i < TraderStock.Count && i < 4; i++)
        {
            var offer = TraderStock[i];
            var cw = (panel.Width - 80 - 3 * 20) / 4;
            var card = new Rectangle(panel.X + 40 + i * (cw + 20), panel.Y + 120, cw, panel.Height - 254);
            var grade = RarityColors[(int)offer.Rarity];
            var over = card.Contains(hit);
            if (over) hoverOffer = offer;

            _ui.Fill(b, card, new Color(0x12, 0x0E, 0x18, 0xF0));
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 6), grade);
            Outline(b, card, over ? Bone : Dim, 2);

            _ui.TextCenterBig(b, offer.Rarity.ToString().ToUpperInvariant(), card.Center.X, card.Y + 22,
                              grade, UiTypography.Secondary);

            // THE STALL'S GEM HAD NO PICTURE. Every other offer at least reads as a shape from its
            // name; a gem is a stone whose whole identity is its stat, so a card with a price, a name
            // and no face was the one offer you could not recognise. The medallion is the same art the
            // Forge draws for it (AssetLibrary's gem_<stat> aliases), so the two screens agree.
            if (GemCraft.IsGem(offer) && _ui.Assets.Get(GemMedallion(offer)) is { } face)
                b.Draw(face, new Rectangle(card.Center.X - 34, card.Y + 108, 68, 68), Color.White);
            var name = ItemNaming.FullName(offer);
            var px = UiTypography.Body;
            // The floor is a NAMED RUNG: a loop the type gate cannot see was free to shrink a card's
            // name to 13 px, three under the Caption floor the standard sets for anything readable.
            while (px > UiTypography.Caption && _ui.MeasureBig(name, px) > card.Width - 24) px--;
            _ui.TextCenterBig(b, name, card.Center.X, card.Y + 52, Bone, px);
            _ui.TextCenterBig(b, $"LEVEL {offer.ItemLevel} — SAME AS YOURS", card.Center.X, card.Y + 88, Slate,
                              UiTypography.Secondary);

            // The price, line by line, each in the wallet's verdict colour. A gem card carries its
            // medallion at 108, so its price starts below the picture rather than through it.
            var y = card.Y + (GemCraft.IsGem(offer) ? 186 : 140);
            _ui.TextCenterBig(b, "PRICE", card.Center.X, y, Slate, UiTypography.SectionLabel);
            y += 28;
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

            var buyBtn = new Rectangle(card.X + 40, card.Bottom - 82, card.Width - 80, 54);
            if (TraderBought.Contains(i))
                // Disabled ink, correctly: it IS unavailable, and its second cue is that where every
                // other card has a BUY button this one has none.
                _ui.TextCenterBig(b, "ALREADY BOUGHT", buyBtn.Center.X, buyBtn.Y + 16, UiInk.Disabled,
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

    private void DrawInspect(SpriteBatch b, Point hit, bool clicked)
    {
        if (_inspectError.Length > 0)
        {
            _ui.Scrim(b, 0.6f);
            var panel = new Rectangle(UiKit.PageCenterX - 400, 420, 800, 240);
            _ui.Panel(b, panel);
            _ui.TextCenterBig(b, "THE CODE DIDN'T OPEN", panel.Center.X, UiKit.TitleTop(panel), Ember, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, _inspectError, panel.Center.X, UiKit.BodyTop(panel), Bone, UiTypography.Body);
            if (_ui.Button(b, new Rectangle(panel.Center.X - 80, panel.Bottom - 76, 160, 48), "OK", hit, clicked, true))
                _inspectError = "";
            return;
        }

        if (_inspectItem is { } item)
        {
            _ui.Scrim(b, 0.7f);
            var panel = new Rectangle(UiKit.PageCenterX - 340, 200, 680, 680);
            // Fill+Outline, not Panel: this rect is nearly square and the frame picker would grab
            // the square art meant for icons (UiKit.Panel picks by aspect ratio).
            _ui.Fill(b, panel, new Color(0x12, 0x0E, 0x18, 0xF4));
            Outline(b, panel, Gold, 2);
            _ui.TextCenterBig(b, "A FRIEND'S ITEM", panel.Center.X, panel.Y + UiTypography.PanelTitleTop, Gold, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, "YOU CAN ONLY LOOK. IT NEVER JOINS YOUR BAG.", panel.Center.X,
                              panel.Y + UiTypography.PanelCaptionTop, Slate, UiTypography.Secondary);
            ItemTooltip.Draw(_ui, b, item, Hunter,
                             new Point(panel.Center.X - ItemTooltip.Width / 2, panel.Y + 104), UiKit.Page);
            if (_ui.Button(b, new Rectangle(panel.Center.X - 80, panel.Bottom - 72, 160, 48), "CLOSE", hit, clicked, true))
                _inspectItem = null;
            return;
        }

        if (_inspectBuild is { } build)
        {
            _ui.Scrim(b, 0.7f);
            // 800x600, not 620: at 620 the ratio was 1.29 and UiKit.Panel's aspect picker handed
            // this card the SQUARE frame meant for icons — the exact trap the item card above
            // dodges with Fill+Outline.
            var panel = new Rectangle(UiKit.PageCenterX - 400, 230, 800, 600);
            _ui.Panel(b, panel, gold: true);
            _ui.TextCenterBig(b, "A FRIEND'S BUILD", panel.Center.X, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, "READ IT, COPY THE IDEA. YOUR OWN BUILD STAYS THE SAME.", panel.Center.X,
                              UiKit.CaptionTop(panel), Slate, UiTypography.Secondary);

            var y = panel.Y + 112;

            // The discipline first — the Nen identity is the headline of any build now.
            var spec = build.Mastery
                .Select(MasteryCatalog.ById)
                .FirstOrDefault(n => n is { Kind: MasteryKind.Specialisation });
            _ui.TextBig(b, spec?.Style is { } f
                            ? $"STYLE: {f.ToString().ToUpperInvariant()}"
                            : "NO STYLE CHOSEN",
                        panel.X + 60, y, spec is null ? Slate : Gold, UiTypography.Body);
            y += 40;

            _ui.TextBig(b, "SKILLS", panel.X + 60, y, Slate, UiTypography.SectionLabel);
            y += UiTypography.Pitch(UiTypography.SectionLabel);
            if (build.Skills.Count == 0)
            {
                _ui.TextBig(b, "NO SKILLS", panel.X + 80, y, UiInk.Empty, UiTypography.Secondary);
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
                _ui.TextBig(b, line, panel.X + 80, y, Bone, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }

            y += 14;
            _ui.TextBig(b, "KEYSTONES", panel.X + 60, y, Slate, UiTypography.SectionLabel);
            y += UiTypography.Pitch(UiTypography.SectionLabel);
            if (build.Keystones.Count == 0)
            {
                _ui.TextBig(b, "NONE CHOSEN", panel.X + 80, y, UiInk.Empty, UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }
            foreach (var k in build.Keystones.Take(3))
            {
                _ui.TextBig(b, Keystones.ById(k)?.Name ?? k.ToUpperInvariant(), panel.X + 80, y, Bone,
                            UiTypography.Secondary);
                y += UiTypography.Pitch(UiTypography.Secondary);
            }

            y += 14;
            _ui.TextBig(b, $"MASTERY: {build.Mastery.Count} NODES TAKEN", panel.X + 60, y, Slate,
                        UiTypography.SectionLabel);

            if (_ui.Button(b, new Rectangle(panel.Center.X - 80, panel.Bottom - 72, 160, 48), "CLOSE", hit, clicked, true))
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

    // 460, not 420: the closing sentence was set at Caption — a size the standard allows for a label
    // and forbids for a sentence — because at 420 that was the only way it fitted. It wraps at
    // Secondary now, and 326/460 keeps the same vertical frame art, so nothing else about it moves.
    private const int FilterPopoverW = 326, FilterPopoverH = 460;

    private Rectangle FilterPopover =>
        new(Math.Min(FilterBtn.X, UiKit.PageRight(24) - FilterPopoverW), FilterBtn.Bottom + 10,
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
        _ui.PanelQuiet(b, pop);
        var inner = new Rectangle(pop.X + inset, pop.Y + inset, pop.Width - inset * 2, pop.Height - inset * 2);
        var close = UiKit.CloseRect(pop, 36);
        _ui.TextBig(b, "CHEST FILTER", inner.X, close.Y + 4, Gold, UiTypography.PanelTitle);
        if (_ui.CloseButton(b, close, hit, clicked)) FilterOpen = false;
        var y = close.Bottom + 8;
        _ui.TextBig(b, "WHICH CHESTS TO KEEP", inner.X, y, Slate, UiTypography.Secondary);
        y += 30;

        _ui.TextBig(b, "LOWEST TIER", inner.X, y, Bone, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        var minus = new Rectangle(inner.X, y, 34, 34);
        var plus = new Rectangle(inner.Right - 34, y, 34, 34);
        MiniButton(b, minus, "-", hit);
        MiniButton(b, plus, "+", hit);
        _ui.TextCenter(b, KeepMinTier <= 0 ? "ANY TIER" : $"TIER {KeepMinTier} AND UP", inner.Center.X, y + 6, KeepMinTier > 0 ? Gold : Slate);
        if (UiKit.ClickedIn(minus, hit, clicked) && KeepMinTier > 0) { KeepMinTier -= 1; FilterDirty = true; }
        if (UiKit.ClickedIn(plus, hit, clicked) && KeepMinTier < 99) { KeepMinTier += 1; FilterDirty = true; }

        y += 48;
        _ui.TextBig(b, "GEAR SLOTS THE CHEST IS FOR", inner.X, y, Bone, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        string? tip = null;
        const int cellH = 60;
        var cellW = inner.Width / 4;
        for (var i = 0; i < SlotChips.Length; i++)
        {
            var slot = SlotChips[i];
            var cell = new Rectangle(inner.X + (i % 4) * cellW, y + (i / 4) * (cellH + 4), cellW, cellH);
            var lit = KeepSlots.Contains(slot);
            var hot = cell.Contains(hit);
            if (lit) _ui.Fill(b, cell, Gold * 0.16f);
            var box = hot ? new Rectangle(cell.Center.X - 27, cell.Center.Y - 27, 54, 54)
                          : new Rectangle(cell.Center.X - 25, cell.Center.Y - 25, 50, 50);
            var tint = lit || hot ? Color.White : new Color(0x8C, 0x86, 0x80);
            if (!_ui.Icon(b, SlotIconKey(slot), box, tint))
                _ui.TextCenterBig(b, SlotLabel(slot), cell.Center.X, cell.Center.Y - 8, tint, UiTypography.Caption);
            if (lit) Outline(b, cell, Gold * 0.8f, 2);
            if (hot) tip = SlotTip(slot) + (lit ? " Click to stop keeping its chests." : " Click to keep the chests made for it.");
            if (UiKit.ClickedIn(cell, hit, clicked))
            {
                if (!KeepSlots.Remove(slot)) KeepSlots.Add(slot);
                FilterDirty = true;
            }
        }
        y += 2 * (cellH + 4) + 6;
        var all = new Rectangle(inner.X, y, inner.Width, 30);
        MiniButton(b, all, "ALL SLOTS", hit, KeepSlots.Count == 0);
        if (UiKit.ClickedIn(all, hit, clicked) && KeepSlots.Count > 0) { KeepSlots.Clear(); FilterDirty = true; }
        y += 40;
        foreach (var l in _ui.WrapBig(KeepMinTier > 0 || KeepSlots.Count > 0
                                          ? "OTHER CHESTS TURN INTO A LITTLE SCRAP"
                                          : "EVERY CHEST IS KEPT",
                                      inner.Width, UiTypography.Secondary))
        {
            _ui.TextBig(b, l, inner.X, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
        }
        if (tip is not null) _ui.HoverTip(b, tip, hit);
    }

    private void MiniButton(SpriteBatch b, Rectangle r, string label, Point hit, bool lit = false)
    {
        var hot = r.Contains(hit);
        _ui.Fill(b, r, new Color(0x14, 0x10, 0x1A, 0xE0));
        Outline(b, r, lit ? Gold * 0.8f : hot ? Bone : Dim, 2);
        _ui.TextCenterBig(b, label, r.Center.X, r.Y + (r.Height - UiTypography.Secondary) / 2 - 1,
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
