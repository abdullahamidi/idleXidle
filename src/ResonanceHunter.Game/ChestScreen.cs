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
/// The VAULT: every unopened chest you hold, and what each one already promises.
/// </summary>
/// <remarks>
/// <para>
/// Playtest: <i>"Ne chesti düştüğünü bilmiyorum, açınca anlıyorum sadece. Chest için ayrı bir sayfa
/// tasarla, ne chesti geldiğini açmadan görebileyim."</i> — I do not know which chest dropped, I only
/// find out when I open it; design a separate chest page so I can see what arrived before opening.
/// </para>
/// <para>
/// Before this, chests were one line of text — <c>"3 CHESTS"</c> — tucked into the corner of the
/// Forge's SALVAGE mode, tinted by the best grade in the pile. Everything a chest actually carries
/// (its grade, the tier its items roll at, its element, the region that flavours it, the tilt of the
/// run that won it, and above all its GUARANTEED rarity floor) was decided at drop and shown to
/// nobody. The reveal was doing all the work, and a reveal with no anticipation in front of it is just
/// a number appearing.
/// </para>
/// <para>
/// <b>This screen states facts, never outcomes.</b> Contents are rolled at OPEN, so everything here is
/// a floor or a range. Showing the actual roll early would delete the reason a chest exists; showing
/// nothing, as before, deletes the reason to care which one you open. The text comes from
/// <see cref="ChestDossier"/>, which derives every line from the same tuning the roll reads — so this
/// screen cannot drift into lying, and a test opens two thousand real chests to prove it.
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

    private int _cursor;
    private int _scroll;
    private float _anim;

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

    /// <summary>Which chest the OPEN button would open — the host needs it to open the right one.</summary>
    public int SelectedIndex => _cursor;

    // ── Layout ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>The anchor the cards are laid out from. Its HEIGHT is decided per-frame — see below.</summary>
    private static readonly Rectangle GridPanel = new(38, 144, 1180, 718);

    /// <summary>
    /// The grid panel, sized to the pile in it.
    /// </summary>
    /// <remarks>
    /// A three-row frame is drawn around one row of chests for almost the whole game — the pile is
    /// usually two or three, and this panel reserved space for twelve. Four hundred pixels of ornate
    /// frame around nothing reads as content that failed to load, not as headroom.
    ///
    /// The height is the LAST CARD'S BOTTOM plus the frame's own band, not the row pitch: a card is 190
    /// of its 208-pixel pitch, and UiKit.Panel's border reaches 40px inward. Sizing to the pitch put the
    /// bottom row 36px into the frame — which is what the fixed 718 was already doing to row three, so
    /// the clipped last row is older than this change and was simply never at the bottom of the screen.
    ///
    /// The three heights are 338 / 546 / 754, whose aspects against a 1180 width are 3.49 / 2.16 / 1.56
    /// — all above 1.30, so <see cref="UiKit.Panel"/> stays on the SAME frame texture at every size.
    /// It picks its art by aspect ratio, and a panel that changes its own frame as it resizes is worse
    /// than one that is too big.
    /// </remarks>
    private static Rectangle GridPanelFor(int count)
    {
        var rows = Math.Clamp((count + Cols - 1) / Cols, 1, Rows);
        return GridPanel with { Height = 338 + (rows - 1) * 208 };
    }
    private static readonly Rectangle DetailPanel = new(1250, 144, 630, 718);

    private const int Cols = 4;
    private const int Rows = 3;
    private const int PerPage = Cols * Rows;

    private static Rectangle Card(int visible) =>
        new(GridPanel.X + 46 + visible % Cols * 274, GridPanel.Y + 108 + visible / Cols * 208, 256, 190);

    /// <summary>Keep the cursor legal and the page following it.</summary>
    /// <remarks>
    /// Called from Draw as well as Update, because the pile shrinks underneath this screen every time
    /// the host opens a chest. A cursor left pointing past the end would index out of range on the very
    /// next frame — the list is not a static thing being browsed, it is being consumed while displayed.
    /// </remarks>
    /// <summary>Set when the cursor was CHOSEN, so the page follows it once and then leaves it alone.</summary>
    private bool _cursorFollow;

    private void Clamp(int count)
    {
        if (count <= 0) { _cursor = 0; _scroll = 0; return; }
        _cursor = Math.Clamp(_cursor, 0, count - 1);
        // THE PAGE FOLLOWS THE CURSOR ONLY WHEN THE CURSOR MOVED. Clamp runs from Update AND from Draw,
        // so with the cursor sitting on 0 these two lines dragged _scroll back to 0 twice a frame — the
        // wheel set a new page in Update and Draw undid it before anything was drawn, and every chest
        // past the first twelve was unreachable. Identical in shape to the Forge bag's dead wheel.
        if (_cursorFollow)
        {
            if (_cursor < _scroll) _scroll = _cursor;
            if (_cursor >= _scroll + PerPage) _scroll = _cursor - PerPage + 1;
            _cursorFollow = false;
        }
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, count - 1));
    }

    public void Update(float dt, IReadOnlyList<Chest> chests, Point mouse, bool clicked, int wheel)
    {
        ArgumentNullException.ThrowIfNull(chests);
        _anim += dt;

        // Every rect here is authored in 1920x1080 and this screen draws through the overlay inset, so
        // the 480-space cursor has to be lifted before any hit-test — the same one line ForgeScreen and
        // eight others carry. StatsScreen forgot it and its buttons were dead at every cursor position;
        // check_mouse_space.py caught this file the moment it was wired up.
        var hit = Game1.ToOverlay(mouse);

        var sorted = ChestDossiers.BestFirst(chests);
        Clamp(sorted.Count);

        if (wheel != 0 && sorted.Count > PerPage)
            _scroll = Math.Clamp(_scroll - Math.Sign(wheel) * Cols, 0, Math.Max(0, sorted.Count - 1));

        if (!clicked) return;

        for (var vis = 0; vis < PerPage && _scroll + vis < sorted.Count; vis++)
            if (Card(vis).Contains(hit)) { _cursor = _scroll + vis; _cursorFollow = true; return; }
    }

    public void Draw(SpriteBatch b, IReadOnlyList<Chest> chests, Point mouse, bool clicked)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(chests);

        // See the note in Update: authored 1920, cursor arrives 480.
        var hit = Game1.ToOverlay(mouse);

        var sorted = ChestDossiers.BestFirst(chests);
        Clamp(sorted.Count);

        DrawGrid(b, sorted, hit, clicked);
        DrawDetail(b, sorted, hit, clicked);
    }

    private void DrawGrid(SpriteBatch b, IReadOnlyList<Chest> sorted, Point mouse, bool clicked)
    {
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
            return;
        }

        if (sorted.Count > PerPage)
            _ui.TextRightBig(b, $"{_scroll / Cols + 1} / {(sorted.Count + Cols - 1) / Cols}",
                             GridPanel.Right - 46, GridPanel.Y + 72, Slate, UiTypography.Secondary);

        for (var vis = 0; vis < PerPage && _scroll + vis < sorted.Count; vis++)
        {
            var idx = _scroll + vis;
            var chest = sorted[idx];
            var d = ChestDossiers.For(chest);
            var card = Card(vis);
            var grade = RarityColors[(int)chest.Rarity];
            var active = idx == _cursor;

            _ui.Fill(b, card, active ? new Color(0x2A, 0x24, 0x14) : new Color(0x16, 0x14, 0x1C));
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 5), grade);
            if (active)
            {
                _ui.Fill(b, new Rectangle(card.X, card.Bottom - 3, card.Width, 3), grade);
                _ui.Fill(b, new Rectangle(card.X, card.Y, 3, card.Height), grade);
                _ui.Fill(b, new Rectangle(card.Right - 3, card.Y, 3, card.Height), grade);
            }

            // THE CHEST IS THE PICTURE NOW, and the grade is counted rather than spelled.
            //
            // Playtest: "yazılarla chestleri belirtmek yerine düzgün bir kategorizasyonla ve chest
            // ikonlarıyla gösterim sağlanabilir." The card led with the word LEGENDARY set in the
            // largest type on it, so six chests were six paragraphs to read rather than six objects to
            // recognise — and the one image the game has for a chest was not on the card at all.
            //
            // Only chest_loot.png ships, so grade rides on its TINT — and a tint alone would make grade
            // a hue-only channel, which the art bible forbids and a colourblind player cannot use. The
            // pip row is the second channel: one to five filled diamonds, countable at a glance and
            // readable in greyscale. Common is one pip, Legendary is five.
            var iconBox = new Rectangle(card.X + 16, card.Y + 14, 96, 96);
            // LIFTED TOWARD WHITE FOR THE ICON ONLY. SpriteBatch tint MULTIPLIES, so a Rare blue or an
            // Epic crimson at full saturation turns the chest into a dark silhouette while Legendary
            // gold stays bright — the grade would have decided how VISIBLE the chest is, not just what
            // colour it is. The strip and the pips keep the true rarity colour.
            if (_ui.Assets.Get("chest_loot") is { } chestArt)
                _ui.SpriteFit(b, chestArt, iconBox, Color.Lerp(grade, Color.White, 0.45f));
            else _ui.Diamond(b, iconBox, grade);

            for (var pip = 0; pip < 5; pip++)
            {
                var dot = new Rectangle(card.X + 18 + pip * 20, card.Y + 118, 14, 14);
                if (pip <= (int)chest.Rarity) _ui.Diamond(b, dot, grade);
                else _ui.Diamond(b, dot, new Color(0x2A, 0x26, 0x34));
            }

            _ui.TextBig(b, $"TIER {chest.Tier}", card.X + 128, card.Y + 26, Bone, UiTypography.PanelTitle);

            // The element as a coloured chip rather than a word — it is an identity, and at card size a
            // word competes with the grade for the same glance.
            if (chest.Element is { } e)
            {
                var chip = new Rectangle(card.Right - 46, card.Y + 22, 26, 26);
                _ui.Diamond(b, chip, SourceColor.GetValueOrDefault(e, Slate));
            }

            // THE PROMISE, on the card itself. This is the line the player was missing entirely, so it
            // belongs where they see it before clicking anything.
            _ui.TextBig(b, _ui.ShortenBig(d.FloorShort, card.Width - 32, UiTypography.Secondary),
                        card.X + 16, card.Y + 142,
                        // SLATE FOR THE GAMBLE CASE, not Dim: at ~1.6:1 the promise line vanished on
                        // precisely the two grades whose promise is "there isn't one", which is the
                        // fact a player most needs before spending the click.
                        d.GuaranteedFloor > Rarity.Common ? Gem : Slate, UiTypography.Secondary);

            _ui.TextBig(b, _ui.ShortenBig(d.RegionShort, card.Width - 32, UiTypography.Secondary),
                        card.X + 16, card.Y + 166, Slate, UiTypography.Secondary);

            if (card.Contains(mouse) && clicked) { _cursor = idx; _cursorFollow = true; }
        }
    }

    private void DrawDetail(SpriteBatch b, IReadOnlyList<Chest> sorted, Point mouse, bool clicked)
    {
        _ui.PanelQuiet(b, DetailPanel);

        if (sorted.Count == 0)
        {
            _ui.TextBig(b, "NOTHING TO OPEN", DetailPanel.X + 40, DetailPanel.Y + 32, Slate,
                        UiTypography.PanelTitle);
            foreach (var (line, i) in Wrapped(
                         "A chest is an event, not a paycheck — roughly one boss in five drops one. "
                         + "Its grade decides the rarity floor of what is inside; the depth you were at "
                         + "decides the tier.").Select((l, i) => (l, i)))
                _ui.TextBig(b, line, DetailPanel.X + 40, DetailPanel.Y + 96 + i * 30, Dim,
                            UiTypography.Body);
            return;
        }

        var chest = sorted[Math.Clamp(_cursor, 0, sorted.Count - 1)];
        var d = ChestDossiers.For(chest);
        var grade = RarityColors[(int)chest.Rarity];

        // ANCHORED TO THE PANEL'S INTERIOR, not to a hand-picked offset. UiKit.Panel chooses its plate by
        // aspect, so "clear of the border" is not a constant — the first pass guessed +28, the second
        // +52, and both were painted over the top-left corner filigree. PanelInner knows.
        var inner = UiKit.PanelInner(DetailPanel);
        _ui.TextBig(b, $"{chest.Rarity.ToString().ToUpperInvariant()} CHEST",
                    inner.X + 8, inner.Y + 6, grade, UiTypography.SectionTitle);
        _ui.Fill(b, new Rectangle(DetailPanel.X + 40, DetailPanel.Y + 92, DetailPanel.Width - 80, 2),
                 grade * 0.6f);

        var y = DetailPanel.Y + 116;
        foreach (var line in d.Lines)
        {
            // A bullet, so six statements of fact read as a list rather than as a paragraph that
            // happens to have line breaks in it.
            _ui.Fill(b, new Rectangle(DetailPanel.X + 40, y + 10, 8, 8), grade * 0.8f);
            foreach (var wrapped in Wrapped(line))
            {
                _ui.TextBig(b, wrapped, DetailPanel.X + 60, y, Bone, UiTypography.Body);
                y += 28;
            }
            y += 8;
        }

        if (!string.IsNullOrWhiteSpace(d.RegionBlurb))
        {
            // INSET TO MATCH THE BULLET TEXT ABOVE (+60, not +40). The panel art carries a large diamond
            // ornament on each side at its vertical midpoint, which is exactly where this block lands —
            // at +40 the label and its first line ran under it. The bullets above already clear it
            // because their text starts past the bullet mark; this had no bullet and no reason to.
            const int textX = 60;
            y += 12;
            _ui.TextBig(b, "WHERE IT WAS WON", DetailPanel.X + textX, y, Slate, UiTypography.Secondary);
            y += 30;
            foreach (var wrapped in Wrapped(d.RegionBlurb))
            {
                _ui.TextBig(b, wrapped, DetailPanel.X + textX, y, Slate, UiTypography.Secondary);
                y += 26;
            }
        }

        // Buttons pinned to the panel's foot rather than flowing after the text: the text length varies
        // by several lines between grades, and a button that moves is a button the player has to find.
        var openRect = new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 96, 260, 60);
        var allRect = new Rectangle(DetailPanel.X + 320, DetailPanel.Bottom - 96, 260, 60);

        if (_ui.Button(b, openRect, "OPEN THIS", mouse, clicked)) _pending = OpenRequest.Selected;
        if (_ui.Button(b, allRect, $"OPEN ALL ({sorted.Count})", mouse, clicked, sorted.Count > 1))
            _pending = OpenRequest.All;
    }

    /// <summary>Wrap into the detail panel's text column.</summary>
    private IReadOnlyList<string> Wrapped(string text)
        => _ui.WrapBig(text, DetailPanel.Width - 130, UiTypography.Body);
}
