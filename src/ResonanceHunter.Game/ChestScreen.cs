using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;

namespace ResonanceHunter.Client;

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
    /// <summary>Full width now — the detail column is gone. Height decided per-frame; see below.</summary>
    private static readonly Rectangle GridPanel = new(38, 144, 1842, 718);

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
        return GridPanel with { Height = 366 + (rows - 1) * 230 };
    }

    private const int Cols = 6;
    private const int Rows = 3;
    private const int PerPage = Cols * Rows;

    private static Rectangle Card(int visible) =>
        new(GridPanel.X + 46 + visible % Cols * 290, GridPanel.Y + 116 + visible / Cols * 230, 270, 210);

    /// <summary>The "?" corner chip — drawn 30px, hit-tested 46px (Fitts's Law padding, house rule).</summary>
    private static Rectangle QChip(Rectangle card) => new(card.Right - 44, card.Y + 12, 30, 30);
    private static Rectangle QHit(Rectangle card) => new(card.Right - 52, card.Y + 4, 46, 46);

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

        _ui.Panel(b, GridPanelFor(sorted.Count));
        _ui.TextBig(b, "THE VAULT", GridPanel.X + 46, GridPanel.Y + 26, Gold, UiTypography.ScreenTitle, TextFace.Display);

        // The tally, so the pile reads at a glance without counting cards.
        var tally = ChestDossiers.Tally(chests);   // every chest, not every stack
        var summary = tally.Count == 0
            ? "nothing waiting"
            : string.Join("   ", tally.Select(t => $"{t.Count} {t.Grade.ToString().ToUpperInvariant()}"));
        _ui.TextBig(b, summary, GridPanel.X + 46, GridPanel.Y + 72, Slate, UiTypography.Secondary);

        var uiClicked = clicked && !ModalOpen;

        // THE STALL AND THE CODES, in the header — and above the empty-vault return, because the
        // trader must be reachable with nothing waiting in the pile.
        var traderBtn = new Rectangle(GridPanel.Right - 558, GridPanel.Y + 24, 200, 56);
        var pasteBtn = new Rectangle(GridPanel.Right - 770, GridPanel.Y + 24, 200, 56);
        // _modalOpenedNow: the TRADER button's rect overlaps the stall's CLOSE (measured: a 72x28
        // region), and the click edge stays latched through the whole Draw — without this flag a
        // click in the overlap opened the stall and closed it IN THE SAME FRAME, an invisible dead
        // zone across a fifth of the button.
        if (_ui.Button(b, traderBtn, "TRADER", hit, uiClicked, TraderStock.Count > 0))
        {
            _traderOpen = true;
            _modalOpenedNow = true;
        }
        if (_ui.Button(b, pasteBtn, "PASTE A CODE", hit, uiClicked, true))
        {
            PasteCode();
            _modalOpenedNow = true;
        }

        if (sorted.Count == 0)
        {
            _ui.TextBig(b, "No chests. Bosses drop them — about one boss in five.",
                        GridPanel.X + 46, GridPanel.Y + 156, Dim, UiTypography.Body);
            _ui.TextBig(b, "When one arrives: click the chest to open it, hover its ? to read what it holds.",
                        GridPanel.X + 46, GridPanel.Y + 190, Dim, UiTypography.Body);
            DrawTrader(b, hit, clicked && !_modalOpenedNow);
            DrawInspect(b, hit, clicked && !_modalOpenedNow);
            _modalOpenedNow = false;
            return;
        }

        // OPEN ALL, in the header — with one chest the card itself is the button, so this needs two.
        var allBtn = new Rectangle(GridPanel.Right - 346, GridPanel.Y + 24, 300, 56);
        if (_ui.Button(b, allBtn, $"OPEN ALL ({chests.Count})", hit, uiClicked, chests.Count > 1))
            _pending = OpenRequest.All;

        // Right-aligned under the header row now — the TRADER and PASTE buttons live where it sat.
        if (sorted.Count > PerPage)
            _ui.TextRightBig(b, $"ROWS {_scroll / Cols + 1} / {(sorted.Count + Cols - 1) / Cols}  ·  WHEEL SCROLLS",
                             GridPanel.Right - 46, GridPanel.Y + 92, Slate, UiTypography.Secondary);

        for (var vis = 0; vis < PerPage && _scroll + vis < sorted.Count; vis++)
        {
            var idx = _scroll + vis;
            var chest = sorted[idx];
            var d = ChestDossiers.For(chest);
            var card = Card(vis);
            var grade = RarityColors[(int)chest.Rarity];
            var hovered = idx == _hoverIdx;
            var ease = hovered ? _hoverT * _hoverT * (3f - 2f * _hoverT) : 0f;   // smoothstep

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

            _ui.TextBig(b, $"TIER {chest.Tier}", card.X + 140, card.Y + 30, Bone, UiTypography.PanelTitle);
            // The stack count. Four identical Rare tier-6 chests are one card that says ×4 — a click
            // opens one of them, OPEN ALL still opens every chest. Playtest: "aynı chestler stacklensin."
            if (stacks[idx].Count > 1)
                _ui.TextRightBig(b, $"×{stacks[idx].Count}", card.Right - 16, card.Y + 30, Gold, UiTypography.PanelTitle);

            // The element as a coloured chip — an identity, not a word competing with the grade.
            if (chest.Element is { } e)
                _ui.Diamond(b, new Rectangle(card.Right - 44, card.Y + 52, 24, 24),
                            SourceColor.GetValueOrDefault(e, Slate));

            // THE PROMISE, on the card. Slate for the gamble case, not Dim — "there isn't one" is the
            // fact a player most needs before spending the click.
            _ui.TextBig(b, _ui.ShortenBig(d.FloorShort, card.Width - 32, UiTypography.Secondary),
                        card.X + 16, card.Y + 158,
                        d.GuaranteedFloor > Rarity.Common ? Gem : Slate, UiTypography.Secondary);
            _ui.TextBig(b, _ui.ShortenBig(d.RegionShort, card.Width - 32, UiTypography.Secondary),
                        card.X + 16, card.Y + 182, Slate, UiTypography.Secondary);

            // The "?" corner chip — the whole dossier lives behind it, on a hover delay.
            var q = QChip(card);
            var qHot = idx == _qIdx;
            _ui.Fill(b, q, new Color(0x14, 0x10, 0x1A, 0xE0));
            _ui.Fill(b, new Rectangle(q.X, q.Y, q.Width, 2), qHot ? Bone : Dim);
            _ui.Fill(b, new Rectangle(q.X, q.Bottom - 2, q.Width, 2), qHot ? Bone : Dim);
            _ui.Fill(b, new Rectangle(q.X, q.Y, 2, q.Height), qHot ? Bone : Dim);
            _ui.Fill(b, new Rectangle(q.Right - 2, q.Y, 2, q.Height), qHot ? Bone : Dim);
            _ui.TextCenter(b, "?", q.Center.X, q.Y + 4, qHot ? Bone : Slate);

            // OPEN, fading in over the lid while hovered — the label the click fulfils. Gold is the
            // reserved commit colour and this card IS the commit trigger, the one place it belongs.
            if (hovered && ease > 0.05f)
            {
                var pulse = 0.85f + 0.15f * MathF.Sin(_anim * 4f);
                var chip = new Rectangle(icon.Center.X - 52, icon.Center.Y - 18, 104, 36);
                _ui.Fill(b, chip, new Color(0x0E, 0x0A, 0x14) * (0.85f * ease));
                _ui.TextCenter(b, "OPEN", chip.Center.X, chip.Y + 8, Gold * (ease * pulse));
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

        _ui.TextCenterBig(b, "THE WANDERING TRADER", 960, panel.Y + 34, Gold, UiTypography.SectionTitle);
        _ui.TextCenter(b, "THE SAME STALL FOR EVERY HUNTER THIS WEEK — NEW GOODS EACH WEEK, PAID IN MATERIALS.",
                       960, panel.Y + 74, Slate);

        if (_ui.Button(b, new Rectangle(panel.Right - 170, panel.Y + 26, 130, 44), "CLOSE", hit, clicked, true))
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
            var name = ItemNaming.FullName(offer);
            var px = UiTypography.Body;
            while (px > 13 && _ui.MeasureBig(name, px) > card.Width - 24) px--;
            _ui.TextCenterBig(b, name, card.Center.X, card.Y + 52, Bone, px);
            _ui.TextCenter(b, $"LEVEL {offer.ItemLevel} — YOUR OWN MEASURE", card.Center.X, card.Y + 88, Slate);

            // The price, line by line, each in the wallet's verdict colour.
            var y = card.Y + 140;
            _ui.TextCenter(b, "PRICE", card.Center.X, y, Slate);
            y += 28;
            var affordable = true;
            foreach (var (m, amount) in WanderingTrader.PriceOf(offer, TraderTuning.Default))
            {
                var held = Hunter?.MaterialOf(m) ?? 0;
                var enough = held >= amount;
                affordable &= enough;
                _ui.TextCenter(b, $"{amount} {m.ToString().ToUpperInvariant()}  (YOU HOLD {held})",
                               card.Center.X, y, enough ? Bone : Ember);
                y += 26;
            }

            var buyBtn = new Rectangle(card.X + 40, card.Bottom - 82, card.Width - 80, 54);
            if (TraderBought.Contains(i))
                _ui.TextCenter(b, "TAKEN THIS WEEK", buyBtn.Center.X, buyBtn.Y + 16, Dim);
            else if (_ui.Button(b, buyBtn, "BUY", hit, clicked, affordable && Hunter is not null))
                _traderBuy = i;
        }

        // The full tooltip beside the pointer — the same card the forge would show for it.
        if (hoverOffer is not null)
            ItemTooltip.Draw(_ui, b, hoverOffer, Hunter, new Point(hit.X + 26, hit.Y + 18),
                             new Rectangle(0, 0, 1920, 1080));
    }

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
            _ui.TextCenterBig(b, "THE CODE DIDN'T OPEN", 960, panel.Y + 32, Ember, UiTypography.PanelTitle);
            _ui.TextCenter(b, _inspectError, 960, panel.Y + 88, Bone);
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
            _ui.TextCenterBig(b, "A FRIEND'S ITEM", 960, panel.Y + 26, Gold, UiTypography.PanelTitle);
            _ui.TextCenter(b, "YOURS TO LOOK AT — IT NEVER JOINS YOUR BAG.", 960, panel.Y + 64, Slate);
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
            _ui.TextCenterBig(b, "A FRIEND'S BUILD", 960, panel.Y + 30, Gold, UiTypography.PanelTitle);
            _ui.TextCenter(b, "READ IT, STEAL THE IDEA — YOUR OWN BUILD IS UNTOUCHED.", 960, panel.Y + 66, Slate);

            var y = panel.Y + 112;

            // The discipline first — the Nen identity is the headline of any build now.
            var spec = build.Mastery
                .Select(MasteryCatalog.ById)
                .FirstOrDefault(n => n is { Kind: MasteryKind.Specialisation });
            _ui.TextBig(b, spec?.Form is { } f
                            ? $"DISCIPLINE: {f.ToString().ToUpperInvariant()}"
                            : "NO DISCIPLINE CHOSEN",
                        panel.X + 60, y, spec is null ? Slate : Gold, UiTypography.Body);
            y += 40;

            _ui.Text(b, "SKILLS", panel.X + 60, y, Slate);
            y += 26;
            if (build.Skills.Count == 0) { _ui.TextBig(b, "NONE WOVEN", panel.X + 80, y, Dim, UiTypography.Secondary); y += 26; }
            foreach (var s in build.Skills.Take(5))
            {
                var vowName = s.VowId is null ? null
                    : Weaving.Catalog.FirstOrDefault(v => v.Id == s.VowId)?.Name ?? s.VowId;
                var line = $"{s.Source.ToUpperInvariant()} {s.Form.ToUpperInvariant()}"
                           + (vowName is null ? "" : $"  —  {vowName.ToUpperInvariant()}");
                _ui.TextBig(b, line, panel.X + 80, y, Bone, UiTypography.Secondary);
                y += 26;
            }

            y += 14;
            _ui.Text(b, "KEYSTONES", panel.X + 60, y, Slate);
            y += 26;
            if (build.Keystones.Count == 0) { _ui.TextBig(b, "NONE SOCKETED", panel.X + 80, y, Dim, UiTypography.Secondary); y += 26; }
            foreach (var k in build.Keystones.Take(3))
            {
                _ui.TextBig(b, Keystones.ById(k)?.Name ?? k.ToUpperInvariant(), panel.X + 80, y, Bone,
                            UiTypography.Secondary);
                y += 26;
            }

            y += 14;
            _ui.Text(b, $"MASTERY: {build.Mastery.Count} NODES TAKEN", panel.X + 60, y, Slate);

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

        _ui.TextBig(b, $"{chest.Rarity.ToString().ToUpperInvariant()} CHEST — WHAT MIGHT BE INSIDE",
                    panel.X + 28, panel.Y + 22, grade, UiTypography.Body);

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
