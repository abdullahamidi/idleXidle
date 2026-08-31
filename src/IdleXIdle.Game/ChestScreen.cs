using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Abilities;
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
/// brightened edge, an OPEN label over the lid — value-step changes, never a new hue, per the art
/// bible's chrome rules), and the click IS the open. The dossier that used to fill a 630px column now
/// lives behind the small "?" in each card's corner, on a 0.4s hover delay so crossing the grid never
/// fires a wall of tooltips.
/// </para>
/// <para>
/// <b>This screen states facts, never outcomes.</b> Contents are rolled at OPEN, so everything here is
/// a floor or a range — the text comes from <see cref="ChestDossier"/>, which derives every line from
/// the same tuning the roll reads, so this screen cannot drift into lying.
/// </para>
/// </remarks>
public sealed class ChestScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Gem = new(0x5F, 0xE0, 0xC8);

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

    public ChestScreen(UiKit ui) => _ui = ui;

    private int _cursor;     // which SORTED chest a click chose — the host reads it via SelectedIndex
    private int _scroll;     // first visible card, in steps of a row
    private float _anim;

    // Hover state, tracked in Update (where dt lives) and read by Draw the same frame.
    private int _hoverIdx = -1;    // card under the pointer, or -1
    private float _hoverT;         // eased 0..1 — ~120ms in, ~80ms out
    private int _qIdx = -1;        // card whose "?" is under the pointer, or -1
    private float _qT;             // how long the pointer has rested on that "?" (tooltip delay 0.4s)

    // ── THE WANDERING TRADER + SHARE CODES — the two future-content directions the designer kept
    //    (2026-08-20). Both live in the vault: the room where things arrive from outside. ─────────
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);

    /// <summary>Host-fed. The stall needs a wallet to price against and a bag to tooltip with.</summary>
    public Hunter? Hunter { get; set; }

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
    /// The house pattern (see <c>StatsScreen.ConsumeTrain</c>): the screen records intent, the host
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
    // lives on the HUNT screen now (SoloExpeditionScreen.DrawKeepFilter); the host still persists it.

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
    private static readonly Rectangle GridPanel = new(38, 150, 1842, 712);

    /// <summary>
    /// The grid panel, sized to the pile in it.
    /// </summary>
    /// <remarks>
    /// Height is the last card row plus the frame's own 40px band. The aspects at 1842 wide are
    /// 5.15 / 3.13 / 2.25 for one / two / three rows — all far above 1.30, so <see cref="UiKit.Panel"/>
    /// keeps the SAME frame texture at every size (it picks its art by aspect, and a panel that swaps
    /// its own frame as it resizes is worse than one that is too big).
    /// </remarks>
    private static Rectangle GridPanelFor(int count)
    {
        var rows = Math.Clamp((count + Cols - 1) / Cols, 1, Rows);
        // The filter row that sat under the header moved to HUNT (2026-08-23), so the cards climbed 34px
        // and the panel shed the same. Aspects (1842 wide): 5.0 / 3.1 / 2.2 — all medium.
        return GridPanel with { Height = CardTop + CardH + UiKit.PanelCorner + (rows - 1) * CardPitchY };
    }

    /// <summary>The header strip's buttons — PASTE A CODE, TRADER, OPEN ALL — all one height.</summary>
    private const int HeaderButtonH = 56;

    /// <summary>The gap between the three header buttons.</summary>
    private const int HeaderButtonGap = 16;

    // ── THE ACTION STRIP. Three rectangles, derived once, so the empty vault and the full one put the
    //    same button in the same place. They used to be hand-placed off GridPanel.Right at −770, −558
    //    and −346 — three magic numbers that had to be kept in step by hand and that ignored the
    //    panel's own content margin, so the strip sat 8 px outside it. ──────────────────────────────
    private static Rectangle HeaderButton(int slotFromRight, int width)
    {
        var x = UiKit.ContentRight(GridPanel);
        for (var i = 0; i < slotFromRight; i++) x -= HeaderWidths[i] + HeaderButtonGap;
        return new Rectangle(x - width, UiKit.TitleTop(GridPanel), width, HeaderButtonH);
    }

    /// <summary>Right to left: OPEN ALL, TRADER, PASTE A CODE. Widths sized to their own labels.</summary>
    private static readonly int[] HeaderWidths = { 300, 200, 240 };

    private static Rectangle OpenAllBtn => HeaderButton(0, HeaderWidths[0]);
    private static Rectangle TraderBtn => HeaderButton(1, HeaderWidths[1]);
    private static Rectangle PasteBtn => HeaderButton(2, HeaderWidths[2]);

    private const int Cols = 6;
    private const int Rows = 3;
    private const int PerPage = Cols * Rows;

    private const int CardW = 270, CardH = 210, CardPitchY = 230;

    /// <summary>The card grid's first row — under the header's button strip and the rule beneath it.</summary>
    private static int CardTop => UiTypography.PanelTitleTop + HeaderButtonH + 28;

    /// <summary>Six columns spanning the panel's content width: the pitch follows the margin.</summary>
    private static int CardPitchX =>
        (GridPanel.Width - UiKit.PadX(GridPanel) * 2 - CardW) / (Cols - 1);

    private static Rectangle Card(int visible) =>
        new(UiKit.ContentLeft(GridPanel) + visible % Cols * CardPitchX,
            GridPanel.Y + CardTop + visible / Cols * CardPitchY, CardW, CardH);

    /// <summary>
    /// The PEEK icon in the card's corner — a glass over a chest (icon_peek), the button that reads the
    /// dossier. Drawn 32px at rest, a touch larger under the mouse; hit-tested 46px (Fitts's Law padding,
    /// house rule). Inset 16px from the card's right edge and 14px from its top, so it sits IN the corner
    /// rather than on it (playtest 2026-08-26: the old "?" hugged the edge and had no icon).
    /// </summary>
    private const int PeekSize = 32;
    private static Rectangle PeekIcon(Rectangle card) => new(card.Right - 16 - PeekSize, card.Y + 14, PeekSize, PeekSize);
    private static Rectangle QHit(Rectangle card)
    {
        var p = PeekIcon(card);
        return new Rectangle(p.Center.X - 23, p.Center.Y - 23, 46, 46);
    }

    /// <summary>
    /// The chest's ELEMENT, as the source glyph (source_body … source_spirit) under the peek icon. The
    /// name appears beside it while the pointer rests on it — the card carries no element label.
    /// </summary>
    private static Rectangle ElementGlyph(Rectangle card) => new(card.Right - 16 - 32, card.Y + 52, 32, 32);

    /// <summary>The asset key of an element's glyph — <c>source_nature</c>. Files under ItemsLoot/glyphs/source.</summary>
    private static string SourceGlyphKey(Source e) => $"source_{e.ToString().ToLowerInvariant()}";

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>
    /// The cards' light is the first row, which is the only row a first visit can have — the Vault opens
    /// on the first chest held, and a new game holds the welcome gift from its first frame. The second
    /// card lights the first card's peek icon, grown a little so the ring reads.
    /// </remarks>
    internal static Rectangle[] Spotlights(TourTarget target)
    {
        switch (target)
        {
            case TourTarget.ChestCards:
                // DERIVED FROM THE ROW IT LIGHTS, not from a remembered offset: the header gained a
                // rule under its buttons and the first row moved down with it, and a hand-written +100
                // would have gone on lighting where the cards used to be.
                var row = Card(0);
                return new[] { new Rectangle(GridPanel.X + 30, row.Y - 12, GridPanel.Width - 60, row.Height + 24) };
            case TourTarget.ChestQuestion:
                // The icon itself, not its padded hit box: the hit box reaches down over the element
                // glyph, and a ring around both would point the card at two things.
                var q = PeekIcon(Card(0));
                q.Inflate(8, 8);
                return new[] { q };
            case TourTarget.VaultButtons:
                var strip = PasteBtn;
                strip.Inflate(10, 10);
                return new[] { new Rectangle(strip.X, strip.Y, OpenAllBtn.Right + 10 - strip.X, strip.Height) };
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
        var hit = Game1.ToOverlay(mouse);

        // One card per STACK of identical chests (ChestDossiers.Stacked); the grid, the hover and the
        // cursor all count stacks. `sorted` is the stacks' samples in best-first order.
        var stacks = ChestDossiers.Stacked(chests);
        var sorted = stacks.Select(st => st.Sample).ToList();
        Clamp(sorted.Count);

        // While the stall or an inspect card is open, the grid underneath is furniture: no hover,
        // no wheel, and — decisive — no chest-opening click. The modals' own buttons live in Draw.
        if (ModalOpen) { _hoverIdx = -1; _qIdx = -1; return; }

        if (wheel != 0)
            _scroll = Math.Clamp(_scroll - Math.Sign(wheel) * Cols, 0, MaxScroll(sorted.Count));

        // ── Hover, resolved here where dt lives. The "?" wins over the card it sits on, and while the
        //    pointer is on it the card's own OPEN affordance stands down — inspecting and opening must
        //    never look like the same gesture. ──
        var overCard = -1;
        var overQ = -1;
        for (var vis = 0; vis < PerPage && _scroll + vis < sorted.Count; vis++)
        {
            var card = Card(vis);
            if (!card.Contains(hit) && !QHit(card).Contains(hit)) continue;
            if (QHit(card).Contains(hit)) overQ = _scroll + vis;
            else overCard = _scroll + vis;
            break;
        }

        if (overQ != _qIdx) _qT = 0f;
        _qIdx = overQ;
        if (_qIdx >= 0) _qT += dt;

        if (overCard != _hoverIdx) _hoverT = 0f;
        _hoverIdx = overCard;
        _hoverT = _hoverIdx >= 0 ? MathF.Min(1f, _hoverT + dt / 0.12f) : 0f;

        if (!clicked) return;

        // THE CHEST IS THE BUTTON. A click on the card opens THAT chest; a click on its "?" does
        // nothing (the tooltip is a hover, not a toggle — a click there must not open the chest).
        if (overQ >= 0) return;
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

        var hit = Game1.ToOverlay(mouse);

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
        _ui.TextCenterBig(b, "THE VAULT", 960, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);

        // The tally, so the pile reads at a glance without counting cards.
        var tally = ChestDossiers.Tally(chests);   // every chest, not every stack
        var summary = tally.Count == 0
            ? "nothing waiting"
            : string.Join("   ", tally.Select(t => $"{t.Count} {t.Grade.ToString().ToUpperInvariant()}"));
        _ui.TextCenterBig(b, summary.ToUpperInvariant(), 960, 80, Slate, UiTypography.Secondary);

        // QUIET, NOT GOLD. The house frame rule (UiKit.PanelQuiet): the gold filigree is for MODALS —
        // the stall and the two share-code cards below still wear it — and every panel that lives IN a
        // screen wears the brown. This was the last in-screen panel in the game still shouting.
        var frame = GridPanelFor(sorted.Count);
        _ui.PanelQuiet(b, frame);

        var uiClicked = clicked && !ModalOpen;

        // THE STALL AND THE CODES, in the header — and above the empty-vault return, because the
        // trader must be reachable with nothing waiting in the pile.
        //
        // _modalOpenedNow: the TRADER button's rect overlaps the stall's CLOSE (measured: a 72x28
        // region), and the click edge stays latched through the whole Draw — without this flag a
        // click in the overlap opened the stall and closed it IN THE SAME FRAME, an invisible dead
        // zone across a fifth of the button.
        if (_ui.Button(b, TraderBtn, "TRADER", hit, uiClicked, TraderStock.Count > 0))
        {
            _traderOpen = true;
            _modalOpenedNow = true;
        }
        if (_ui.Button(b, PasteBtn, "PASTE A CODE", hit, uiClicked, true))
        {
            PasteCode();
            _modalOpenedNow = true;
        }

        // The hairline the action strip stands on, so the buttons read as a header and the cards below
        // as the panel's contents — one rule instead of a gap the eye has to guess at.
        var strip = new Rectangle(UiKit.ContentLeft(GridPanel), TraderBtn.Bottom + 14,
                                  UiKit.ContentRight(GridPanel) - UiKit.ContentLeft(GridPanel), 2);
        _ui.Fill(b, strip, Dim);

        if (sorted.Count == 0)
        {
            var y = GridPanel.Y + CardTop + 8;
            _ui.TextBig(b, "No chests. Bosses drop them — about one boss in five.",
                        UiKit.ContentLeft(GridPanel), y, Dim, UiTypography.Body);
            _ui.TextBig(b, "When one arrives: click the chest to open it. Rest the pointer on the small glass to see what is inside.",
                        UiKit.ContentLeft(GridPanel), y + 34, Dim, UiTypography.Body);
            DrawTrader(b, hit, clicked && !_modalOpenedNow);
            DrawInspect(b, hit, clicked && !_modalOpenedNow);
            _modalOpenedNow = false;
            return;
        }

        // OPEN ALL, in the header — with one chest the card itself is the button, so this needs two.
        if (_ui.Button(b, OpenAllBtn, $"OPEN ALL ({chests.Count})", hit, uiClicked, chests.Count > 1))
            _pending = OpenRequest.All;

        // On the strip's own line, at its left end — the buttons hold its right.
        if (sorted.Count > PerPage)
            _ui.TextBig(b, $"ROWS {_scroll / Cols + 1} / {(sorted.Count + Cols - 1) / Cols}  ·  WHEEL SCROLLS",
                        UiKit.ContentLeft(GridPanel), TraderBtn.Y + 18, Slate, UiTypography.Secondary);

        for (var vis = 0; vis < PerPage && _scroll + vis < sorted.Count; vis++)
        {
            var idx = _scroll + vis;
            var chest = sorted[idx];
            var d = ChestDossiers.For(chest);
            var card = Card(vis);
            var grade = RarityColors[(int)chest.Rarity];
            var hovered = idx == _hoverIdx;
            var ease = hovered ? _hoverT * _hoverT * (3f - 2f * _hoverT) : 0f;   // smoothstep

            // A STACK LOOKS LIKE A STACK. Two identical chests were one card with a small "×2" in the
            // corner, and testers did not see it (playtest 2026-08-25). Now the card sits on two offset
            // card backs, the way a pile of cards does, before anything else on it is drawn.
            var stackCount = stacks[idx].Count;
            for (var back = Math.Min(2, stackCount - 1); back >= 1; back--)
            {
                var off = new Rectangle(card.X + back * 7, card.Y + back * 7, card.Width, card.Height);
                _ui.Fill(b, off, new Color(0x0E, 0x0C, 0x14));
                _ui.Fill(b, new Rectangle(off.X, off.Y, off.Width, 5), grade * 0.45f);
                _ui.Fill(b, new Rectangle(off.Right - 2, off.Y, 2, off.Height), grade * 0.25f);
                _ui.Fill(b, new Rectangle(off.X, off.Bottom - 2, off.Width, 2), grade * 0.25f);
            }

            // Card body: a dark cell, its grade on the top strip. Hover brightens the EDGE one value
            // step and lifts the chest — never a new hue (art bible chrome rules).
            _ui.Fill(b, card, hovered ? new Color(0x20, 0x1C, 0x2A) : new Color(0x16, 0x14, 0x1C));
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 5), grade);
            if (hovered)
            {
                var edge = Bone * (0.35f + 0.45f * ease);
                _ui.Fill(b, new Rectangle(card.X, card.Bottom - 2, card.Width, 2), edge);
                _ui.Fill(b, new Rectangle(card.X, card.Y, 2, card.Height), edge);
                _ui.Fill(b, new Rectangle(card.Right - 2, card.Y, 2, card.Height), edge);
            }

            // THE CHEST IS THE PICTURE, lifted toward white so grade never decides how VISIBLE it is
            // (SpriteBatch tint multiplies). On hover it grows 6% about its own centre — enough to say
            // "this responded" without clipping the neighbouring card.
            var iconBase = new Rectangle(card.X + 16, card.Y + 16, 110, 110);
            var icon = Grow(iconBase, 1f + 0.06f * ease);
            if (_ui.Assets.Get("chest_loot") is { } chestArt)
                _ui.SpriteFit(b, chestArt, icon, Color.Lerp(grade, Color.White, 0.45f + 0.1f * ease));
            else _ui.Diamond(b, icon, grade);

            // The pip row: grade as a COUNT, readable in greyscale. Common one pip, Legendary five.
            for (var pip = 0; pip < 5; pip++)
            {
                var dot = new Rectangle(card.X + 18 + pip * 22, card.Y + 134, 14, 14);
                if (pip <= (int)chest.Rarity) _ui.Diamond(b, dot, grade);
                else _ui.Diamond(b, dot, new Color(0x2A, 0x26, 0x34));
            }

            _ui.TextBig(b, $"TIER {chest.Tier}", card.X + 140, card.Y + 30, Bone, UiTypography.Headline);
            // The stack count. Four identical Rare tier-6 chests are one card that says ×4 — a click
            // opens one of them, OPEN ALL still opens every chest. Playtest: "aynı chestler stacklensin."
            // The count is a gold BADGE on the chest itself now, and a line under the tier says it in
            // words, because the corner number alone went unread.
            if (stackCount > 1)
            {
                var badge = new Rectangle(card.X + 10, card.Y + 12, 54, 30);
                _ui.Fill(b, badge, Gold);
                _ui.Fill(b, new Rectangle(badge.X + 2, badge.Y + 2, badge.Width - 4, badge.Height - 4), new Color(0x3A, 0x2A, 0x10));
                _ui.TextCenterBig(b, $"×{stackCount}", badge.Center.X, badge.Y + 4, Gold, UiTypography.Headline);
                // Under the element chip's row, in the 84 px column right of the chest art — a longer
                // line ran under the chip.
                _ui.TextBig(b, $"{stackCount} THE SAME", card.X + 140, card.Y + 84, Gold, UiTypography.Secondary);
            }

            // The element as its SOURCE GLYPH — the same art the skills and the map use for it, so the
            // card says "Nature" the way the rest of the game does (playtest 2026-08-26: "the chests have
            // colour icons on them; they should be element icons"). The coloured diamond stays only as
            // the fallback for a glyph that is not on disk. The word appears on hover, since the card
            // has no element label of its own.
            if (chest.Element is { } e)
            {
                var glyph = ElementGlyph(card);
                if (!_ui.Icon(b, SourceGlyphKey(e), glyph, Color.White))
                    _ui.Diamond(b, new Rectangle(glyph.X + 4, glyph.Y + 4, glyph.Width - 8, glyph.Height - 8),
                                SourceColor.GetValueOrDefault(e, Slate));
                if (glyph.Contains(hit) && !ModalOpen)
                    _ui.TextRightBig(b, e.ToString().ToUpperInvariant(), glyph.X - 8, glyph.Y + 7,
                                     SourceColor.GetValueOrDefault(e, Bone), UiTypography.Secondary);
            }

            // THE PROMISE, on the card. Slate for the gamble case, not Dim — "there isn't one" is the
            // fact a player most needs before spending the click.
            //
            // SET IN THE HOUSE VOICE. ChestDossier writes these for prose ("EPIC or better", "any
            // rarity", "a welcome gift", "one plain blade") and the rest of the game — this card's own
            // TIER line eighty pixels above them included — is uniformly capitals. Two casings on one
            // card is most of what made the VAULT read as a screen from another game. The words are
            // untouched; Core still owns them, and the tooltip behind the "?" still prints them as
            // written, because a sentence in a paragraph is not a label on a card.
            _ui.TextBig(b, _ui.ShortenBig(d.FloorShort.ToUpperInvariant(), card.Width - 32, UiTypography.Secondary),
                        card.X + 16, card.Y + 158,
                        d.GuaranteedFloor > Rarity.Common ? Gem : Slate, UiTypography.Secondary);
            _ui.TextBig(b, _ui.ShortenBig(d.RegionShort.ToUpperInvariant(), card.Width - 32, UiTypography.Secondary),
                        card.X + 16, card.Y + 182, Slate, UiTypography.Secondary);

            // The PEEK icon in the corner — the whole dossier lives behind it, on a hover delay. The same
            // hover pattern as UiKit.CloseButton: the medallion grows two pixels a side and goes to full
            // white under the mouse. The old bordered "?" square is the fallback if the art is missing.
            var q = PeekIcon(card);
            var qHot = idx == _qIdx;
            if (_ui.Assets.Get("icon_peek") is { } peek)
            {
                var box = qHot ? new Rectangle(q.X - 2, q.Y - 2, q.Width + 4, q.Height + 4) : q;
                _ui.SpriteFit(b, peek, box, qHot ? Color.White : new Color(0xE0, 0xD8, 0xC8));
            }
            else
            {
                _ui.Fill(b, q, new Color(0x14, 0x10, 0x1A, 0xE0));
                _ui.Fill(b, new Rectangle(q.X, q.Y, q.Width, 2), qHot ? Bone : Dim);
                _ui.Fill(b, new Rectangle(q.X, q.Bottom - 2, q.Width, 2), qHot ? Bone : Dim);
                _ui.Fill(b, new Rectangle(q.X, q.Y, 2, q.Height), qHot ? Bone : Dim);
                _ui.Fill(b, new Rectangle(q.Right - 2, q.Y, 2, q.Height), qHot ? Bone : Dim);
                _ui.TextCenterBig(b, "?", q.Center.X, q.Y + 5, qHot ? Bone : Slate, UiTypography.Body);
            }

            // OPEN, fading in over the lid while hovered — the label the click fulfils. Gold is the
            // reserved commit colour and this card IS the commit trigger, the one place it belongs.
            if (hovered && ease > 0.05f)
            {
                var pulse = 0.85f + 0.15f * MathF.Sin(_anim * 4f);
                var chip = new Rectangle(icon.Center.X - 52, icon.Center.Y - 18, 104, 36);
                _ui.Fill(b, chip, new Color(0x0E, 0x0A, 0x14) * (0.85f * ease));
                // A THING YOU CLICK, at the rung things you click are set in — the same size the three
                // buttons above it wear, rather than the paragraph size it had.
                _ui.TextCenterBig(b, "OPEN", chip.Center.X, chip.Y + 6, Gold * (ease * pulse),
                                  UiTypography.NavigationLabel);
            }
        }

        DrawDossierTip(b, sorted);

        DrawTrader(b, hit, clicked && !_modalOpenedNow);
        DrawInspect(b, hit, clicked && !_modalOpenedNow);
        _modalOpenedNow = false;
    }

    // ── THE WANDERING TRADER — the weekly stall. Identity from the week, level from the buyer,
    //    prices in materials. See Core's WanderingTrader for the design reasoning. ────────────────

    private void DrawTrader(SpriteBatch b, Point hit, bool clicked)
    {
        if (!_traderOpen) return;

        _ui.Scrim(b, 0.72f);
        var panel = new Rectangle(300, 170, 1320, 724);
        _ui.Panel(b, panel, gold: true);

        _ui.TextCenterBig(b, "THE WANDERING TRADER", 960, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, "NEW GOODS EVERY WEEK, THE SAME FOR EVERY HUNTER. YOU PAY IN MATERIALS.",
                          960, UiKit.CaptionTop(panel), Slate, UiTypography.Secondary);

        if (_ui.Button(b, new Rectangle(panel.Right - 170, UiKit.TitleTop(panel), 130, 44), "CLOSE", hit, clicked, true))
        {
            _traderOpen = false;
            return;
        }

        ItemInstance? hoverOffer = null;
        for (var i = 0; i < TraderStock.Count && i < 4; i++)
        {
            var offer = TraderStock[i];
            var card = new Rectangle(panel.X + 40 + i * 316, panel.Y + 120, 296, 470);
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
            while (px > 13 && _ui.MeasureBig(name, px) > card.Width - 24) px--;
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
                y += 26;
            }

            var buyBtn = new Rectangle(card.X + 40, card.Bottom - 82, card.Width - 80, 54);
            if (TraderBought.Contains(i))
                _ui.TextCenterBig(b, "ALREADY BOUGHT", buyBtn.Center.X, buyBtn.Y + 16, Dim,
                                  UiTypography.ButtonText);
            else if (_ui.Button(b, buyBtn, "BUY", hit, clicked, affordable && Hunter is not null))
                _traderBuy = i;
        }

        // The full tooltip beside the pointer — the same card the forge would show for it.
        if (hoverOffer is not null)
            ItemTooltip.Draw(_ui, b, hoverOffer, Hunter, new Point(hit.X + 26, hit.Y + 18),
                             new Rectangle(0, 0, 1920, 1080));
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
            var panel = new Rectangle(560, 420, 800, 240);
            _ui.Panel(b, panel);
            _ui.TextCenterBig(b, "THE CODE DIDN'T OPEN", 960, UiKit.TitleTop(panel), Ember, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, _inspectError, 960, UiKit.BodyTop(panel), Bone, UiTypography.Body);
            if (_ui.Button(b, new Rectangle(880, panel.Bottom - 76, 160, 48), "OK", hit, clicked, true))
                _inspectError = "";
            return;
        }

        if (_inspectItem is { } item)
        {
            _ui.Scrim(b, 0.7f);
            var panel = new Rectangle(620, 200, 680, 680);
            // Fill+Outline, not Panel: this rect is nearly square and the frame picker would grab
            // the square art meant for icons (UiKit.Panel picks by aspect ratio).
            _ui.Fill(b, panel, new Color(0x12, 0x0E, 0x18, 0xF4));
            Outline(b, panel, Gold, 2);
            _ui.TextCenterBig(b, "A FRIEND'S ITEM", 960, panel.Y + UiTypography.PanelTitleTop, Gold, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, "YOU CAN ONLY LOOK. IT NEVER JOINS YOUR BAG.", 960,
                              panel.Y + UiTypography.PanelCaptionTop, Slate, UiTypography.Secondary);
            ItemTooltip.Draw(_ui, b, item, Hunter, new Point(960 - ItemTooltip.Width / 2, panel.Y + 104),
                             new Rectangle(0, 0, 1920, 1080));
            if (_ui.Button(b, new Rectangle(880, panel.Bottom - 72, 160, 48), "CLOSE", hit, clicked, true))
                _inspectItem = null;
            return;
        }

        if (_inspectBuild is { } build)
        {
            _ui.Scrim(b, 0.7f);
            // 800x600, not 620: at 620 the ratio was 1.29 and UiKit.Panel's aspect picker handed
            // this card the SQUARE frame meant for icons — the exact trap the item card above
            // dodges with Fill+Outline.
            var panel = new Rectangle(560, 230, 800, 600);
            _ui.Panel(b, panel, gold: true);
            _ui.TextCenterBig(b, "A FRIEND'S BUILD", 960, UiKit.TitleTop(panel), Gold, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, "READ IT, COPY THE IDEA. YOUR OWN BUILD STAYS THE SAME.", 960,
                              UiKit.CaptionTop(panel), Slate, UiTypography.Secondary);

            var y = panel.Y + 112;

            // The discipline first — the Nen identity is the headline of any build now.
            var spec = build.Mastery
                .Select(MasteryCatalog.ById)
                .FirstOrDefault(n => n is { Kind: MasteryKind.Specialisation });
            _ui.TextBig(b, spec?.Style is { } f
                            ? $"DISCIPLINE: {f.ToString().ToUpperInvariant()}"
                            : "NO DISCIPLINE CHOSEN",
                        panel.X + 60, y, spec is null ? Slate : Gold, UiTypography.Body);
            y += 40;

            _ui.TextBig(b, "SKILLS", panel.X + 60, y, Slate, UiTypography.SectionLabel);
            y += 26;
            if (build.Skills.Count == 0) { _ui.TextBig(b, "NONE WOVEN", panel.X + 80, y, Dim, UiTypography.Secondary); y += 26; }
            foreach (var s in build.Skills.Take(5))
            {
                var vowName = s.VowId is null ? null
                    : Vows.Catalog.FirstOrDefault(v => v.Id == s.VowId)?.Name ?? s.VowId;
                var line = $"{s.Source.ToUpperInvariant()} {s.Form.ToUpperInvariant()}"
                           + (vowName is null ? "" : $"  —  {vowName.ToUpperInvariant()}");
                _ui.TextBig(b, line, panel.X + 80, y, Bone, UiTypography.Secondary);
                y += 26;
            }

            y += 14;
            _ui.TextBig(b, "KEYSTONES", panel.X + 60, y, Slate, UiTypography.SectionLabel);
            y += 26;
            if (build.Keystones.Count == 0) { _ui.TextBig(b, "NONE CHOSEN", panel.X + 80, y, Dim, UiTypography.Secondary); y += 26; }
            foreach (var k in build.Keystones.Take(3))
            {
                _ui.TextBig(b, Keystones.ById(k)?.Name ?? k.ToUpperInvariant(), panel.X + 80, y, Bone,
                            UiTypography.Secondary);
                y += 26;
            }

            y += 14;
            _ui.TextBig(b, $"MASTERY: {build.Mastery.Count} NODES TAKEN", panel.X + 60, y, Slate,
                        UiTypography.SectionLabel);

            if (_ui.Button(b, new Rectangle(880, panel.Bottom - 72, 160, 48), "CLOSE", hit, clicked, true))
                _inspectBuild = null;
        }
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }


    /// <summary>
    /// The "?" tooltip: the retired detail column's dossier, beside the card that asked for it.
    /// </summary>
    private void DrawDossierTip(SpriteBatch b, IReadOnlyList<Chest> sorted)
    {
        if (_qIdx < 0 || _qIdx >= sorted.Count || _qT < 0.4f) return;
        if (_qIdx < _scroll || _qIdx >= _scroll + PerPage) return;

        var chest = sorted[_qIdx];
        var d = ChestDossiers.For(chest);
        var grade = RarityColors[(int)chest.Rarity];
        var card = Card(_qIdx - _scroll);

        const int W = 470;
        const int TextW = W - 96;

        // Measure first, then place: wrap every line so the panel is exactly as tall as its facts.
        var blocks = new List<(string Line, bool Bullet)>();
        foreach (var line in d.Lines)
            foreach (var (wrapped, i) in _ui.WrapBig(line, TextW, UiTypography.Body).Select((l, i) => (l, i)))
                blocks.Add((wrapped, i == 0));
        var blurb = string.IsNullOrWhiteSpace(d.RegionBlurb)
            ? new List<string>()
            : _ui.WrapBig(d.RegionBlurb, TextW, UiTypography.Secondary).ToList();

        var h = 96 + blocks.Count * 28 + (blurb.Count > 0 ? 40 + blurb.Count * 26 : 0) + 40;

        // Edge-aware: beside the "?" when it fits, flipped left near the right edge, clamped vertically.
        var x = card.Right + 12 + W <= 1900 ? card.Right + 12 : card.X - W - 12;
        x = Math.Clamp(x, 24, 1920 - W - 24);
        var y = Math.Clamp(card.Y, 100, 1080 - h - 24);

        var panel = new Rectangle(x, y, W, h);
        _ui.Fill(b, panel, new Color(0x0E, 0x0A, 0x14, 0xF2));
        _ui.Fill(b, new Rectangle(panel.X, panel.Y, panel.Width, 3), grade);
        _ui.Fill(b, new Rectangle(panel.X, panel.Bottom - 2, panel.Width, 2), grade * 0.5f);

        // Just the grade and the word — "RARE CHEST" — or a gift's own title. The old " — WHAT MIGHT
        // BE INSIDE" tail said what the panel under it already shows (playtest 2026-08-26).
        _ui.TextBig(b, d.Title, panel.X + 28, panel.Y + 22, grade, UiTypography.Body);

        var ty = panel.Y + 64;
        foreach (var (line, bullet) in blocks)
        {
            if (bullet) _ui.Fill(b, new Rectangle(panel.X + 28, ty + 10, 8, 8), grade * 0.8f);
            _ui.TextBig(b, line, panel.X + 48, ty, Bone, UiTypography.Body);
            ty += 28;
        }

        if (blurb.Count > 0)
        {
            ty += 12;
            _ui.TextBig(b, "WHERE IT WAS WON", panel.X + 28, ty, Slate, UiTypography.Secondary);
            ty += 28;
            foreach (var line in blurb)
            {
                _ui.TextBig(b, line, panel.X + 28, ty, Slate, UiTypography.Secondary);
                ty += 26;
            }
        }
    }

    /// <summary>A rectangle scaled about its own centre.</summary>
    private static Rectangle Grow(Rectangle r, float scale)
    {
        var w = (int)(r.Width * scale);
        var h = (int)(r.Height * scale);
        return new Rectangle(r.Center.X - w / 2, r.Center.Y - h / 2, w, h);
    }
}
