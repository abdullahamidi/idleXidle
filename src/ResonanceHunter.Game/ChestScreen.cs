using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;

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

    /// <summary>Which chest a click chose — the host needs it to open the right one.</summary>
    public int SelectedIndex => _cursor;

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
        return GridPanel with { Height = 358 + (rows - 1) * 230 };
    }

    private const int Cols = 6;
    private const int Rows = 3;
    private const int PerPage = Cols * Rows;

    private static Rectangle Card(int visible) =>
        new(GridPanel.X + 46 + visible % Cols * 290, GridPanel.Y + 108 + visible / Cols * 230, 270, 210);

    /// <summary>The "?" corner chip — drawn 30px, hit-tested 46px (Fitts's Law padding, house rule).</summary>
    private static Rectangle QChip(Rectangle card) => new(card.Right - 44, card.Y + 12, 30, 30);
    private static Rectangle QHit(Rectangle card) => new(card.Right - 52, card.Y + 4, 46, 46);

    private void Clamp(int count)
    {
        if (count <= 0) { _cursor = 0; _scroll = 0; return; }
        _cursor = Math.Clamp(_cursor, 0, count - 1);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, count - 1));
    }

    public void Update(float dt, IReadOnlyList<Chest> chests, Point mouse, bool clicked, int wheel)
    {
        ArgumentNullException.ThrowIfNull(chests);
        _anim += dt;

        // Authored 1920, cursor arrives 480 — the same one line every inset screen carries.
        var hit = Game1.ToOverlay(mouse);

        var sorted = ChestDossiers.BestFirst(chests);
        Clamp(sorted.Count);

        if (wheel != 0 && sorted.Count > PerPage)
            _scroll = Math.Clamp(_scroll - Math.Sign(wheel) * Cols, 0, Math.Max(0, sorted.Count - 1));

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
            _pending = OpenRequest.Selected;
        }
    }

    public void Draw(SpriteBatch b, IReadOnlyList<Chest> chests, Point mouse, bool clicked)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(chests);

        var hit = Game1.ToOverlay(mouse);

        var sorted = ChestDossiers.BestFirst(chests);
        Clamp(sorted.Count);

        _ui.Panel(b, GridPanelFor(sorted.Count));
        _ui.TextBig(b, "THE VAULT", GridPanel.X + 46, GridPanel.Y + 26, Gold, UiTypography.ScreenTitle, TextFace.Display);

        // The tally, so the pile reads at a glance without counting cards.
        var tally = ChestDossiers.Tally(sorted);
        var summary = tally.Count == 0
            ? "nothing waiting"
            : string.Join("   ", tally.Select(t => $"{t.Count} {t.Grade.ToString().ToUpperInvariant()}"));
        _ui.TextBig(b, summary, GridPanel.X + 46, GridPanel.Y + 72, Slate, UiTypography.Secondary);

        if (sorted.Count == 0)
        {
            _ui.TextBig(b, "No chests. Bosses drop them — about one boss in five.",
                        GridPanel.X + 46, GridPanel.Y + 150, Dim, UiTypography.Body);
            _ui.TextBig(b, "When one arrives: click the chest to open it, hover its ? to read what it holds.",
                        GridPanel.X + 46, GridPanel.Y + 184, Dim, UiTypography.Body);
            return;
        }

        // OPEN ALL, in the header — with one chest the card itself is the button, so this needs two.
        var allBtn = new Rectangle(GridPanel.Right - 346, GridPanel.Y + 24, 300, 56);
        if (_ui.Button(b, allBtn, $"OPEN ALL ({sorted.Count})", hit, clicked, sorted.Count > 1))
            _pending = OpenRequest.All;

        if (sorted.Count > PerPage)
            _ui.TextRightBig(b, $"ROWS {_scroll / Cols + 1} / {(sorted.Count + Cols - 1) / Cols}  ·  WHEEL SCROLLS",
                             allBtn.X - 24, GridPanel.Y + 40, Slate, UiTypography.Secondary);

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
