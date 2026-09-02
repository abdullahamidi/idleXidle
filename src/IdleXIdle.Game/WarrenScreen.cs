using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Warrens;

using IdleXIdle.Core.Progression;
namespace IdleXIdle.Game;

/// <summary>
/// WARREN: what each facility pays, where that is spent, and what the place made while you were away.
/// A summary strip, an eight-facility grid, and the shared inspector.
/// </summary>
/// <remarks>
/// <para>
/// Every value is real, from the <see cref="Core.Warrens.Warren"/> model the host owns: facility levels
/// and outputs, the Warren level and XP, the derived production multipliers, and the real upgrade costs
/// in Gleam and Dust. The host sets the model and the owned balances each frame and consumes the
/// upgrade request.
/// </para>
/// <para>
/// UX V2 P2.4 (brief §77–§81, as corrected by PLAN D11 — the Warren has no services, no targeting and
/// no automation in Core, so those sections are honoured as what each facility PAYS and where that is
/// spent). The screen used to answer a question nobody asked: its loudest panel was five derived
/// percentages and the formula that produced them, while the two facts a player comes here for were
/// missing — no card said whether it could be upgraded now, so the only way to find out was to click
/// all eight, and what the place earned while the game was closed was never shown at all, though the
/// host had already computed it.
/// </para>
/// <para>
/// UI POLISH P2 (brief §8–§9, §17–§18): UI SCALE is a density profile, so every row, chip, glyph box
/// and pad on this screen is a base size read through <see cref="UiMetrics"/>, and every vertical
/// rhythm is derived from the rungs it stacks rather than from an assumed 1080 px of height. The
/// strip's height is its tallest cell's stack; the grid gets what is under the strip and a card lays
/// its stack out from what it is given (the icon plate and the milestone caption yield, in that
/// order, when the height is not there); the inspector's rows scroll under a wheel when they no
/// longer fit above its refusal line and button, which stay anchored where they were.
/// </para>
/// </remarks>
public sealed class WarrenScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Met = UiInk.Good;
    private static readonly Color GleamC = new(0xF0, 0xA8, 0x30);   // gold coin track
    private static readonly Color DustC = new(0x5F, 0xE0, 0xC8);    // teal gem track
    private static readonly Color ScrapC = new(0x9A, 0xC0, 0x88);   // scrap green — the Forge wallet's tint
    private static readonly Color EssenceC = new(0x74, 0xC6, 0xE8); // essence blue — the Forge wallet's tint

    private readonly UiKit _ui;
    public WarrenScreen(UiKit ui) => _ui = ui;

    // ── Host-set each frame ───────────────────────────────────────────────────────────────────────
    public Warren Warren { get; set; } = null!;
    public long GleamOwned { get; set; }
    public long DustOwned { get; set; }
    public bool DevWarrenDebug { get; set; }

    /// <summary>The deepest wave held anywhere — the number the facility ceiling comes from. Host-set.</summary>
    public int DeepestWave { get; set; }

    /// <summary>What the last real absence paid, or null when there was none. Host-set.</summary>
    public WelcomeSummary? LastReturn { get; set; }

    // ── Host-consumed requests ──────────────────────────────────────────────────────────────────
    private FacilityKind? _upgradeRequest;
    public FacilityKind? ConsumeUpgrade() { var r = _upgradeRequest; _upgradeRequest = null; return r; }

    private FacilityKind _selected = FacilityKind.Nursery;

    // ── Layout: one summary strip, the grid under it, the inspector beside both. Every edge comes
    //    from UiKit.Page and every size from UiMetrics, so a profile reflows the screen instead of
    //    clipping it. The four page anchors below are the only literals: they are where the page's
    //    chrome (the hint slot, the margins) ends, and they do not grow with the type. ────────────
    private const int Top = 150;            // the hint slot owns canvas y 86-134
    private const int BottomMargin = 60;
    private const int LeftMargin = 38;
    private const int RightInset = 40;

    private static int PanelGap => UiMetrics.Space(20);      // the strip and the grid ↔ the inspector
    private static int StripGap => UiMetrics.Space(16);      // the strip ↔ the grid
    private static int GridPad => UiMetrics.PanelPadding;    // 24 — the plate has no ornament to clear
    private static int CardGap => UiMetrics.Space(18);
    private static int CardPadX => UiMetrics.Space(18);
    private static int ChipHeight => UiMetrics.Control(38);

    /// <summary>
    /// What a card must be given to say UPGRADE READY or NEEDS 12.4K GLEAM at the profile's Body: 200
    /// at 100 %, which is that chip's text plus the chip's and the card's own insets.
    /// </summary>
    private static int CardMinWidth => UiMetrics.Control(200);

    private static int GridMinWidth => CardMinWidth * 4 + CardGap * 3 + GridPad * 2;

    /// <summary>
    /// The inspector takes the house inspector width — but never so much that the eight cards beside
    /// it cannot say their one line. This inspector SCROLLS at the larger profiles (§18), so it can
    /// afford a shorter line; a card cannot scroll, so it wins the room.
    /// </summary>
    private static int InspectorW =>
        Math.Min(UiMetrics.InspectorWidth(UiKit.Page.Width),
                 UiKit.Page.Width - RightInset - LeftMargin - PanelGap - GridMinWidth);

    private static Rectangle InspectorPanel =>
        new(UiKit.PageRight(RightInset) - InspectorW, Top, InspectorW, UiKit.PageBottom(BottomMargin) - Top);

    private static Rectangle SummaryStrip =>
        new(LeftMargin, Top, InspectorPanel.X - PanelGap - LeftMargin, StripHeight);

    private static Rectangle GridPanel =>
        new(LeftMargin, Top + StripHeight + StripGap, SummaryStrip.Width,
            UiKit.PageBottom(BottomMargin) - (Top + StripHeight + StripGap));

    // ── THE STRIP'S RHYTHM. Cell C's stack (a caption, the four figures, their names, the multipliers,
    //    the regions line) is the tallest of the three cells, so it sets the strip's height, and every
    //    step in it is a rung's pitch plus a breath — 200 at 100 %, the height it always had. ─────
    private static int StripCaptionY => UiMetrics.Space(16);
    private static int StripGlyphRowY => StripCaptionY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
    private static int StripNamesY => StripGlyphRowY + UiTypography.Pitch(UiTypography.Headline) - 2;
    private static int StripMultiplierY => StripNamesY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
    private static int StripRegionsY => StripMultiplierY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
    private static int StripRuleY => StripRegionsY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);
    private static int StripAwayY => StripRuleY + UiMetrics.Space(8);
    private static int StripHeight => StripAwayY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.Facilities => new[] { GridPanel },
        TourTarget.FacilityDetail => new[] { InspectorPanel },
        TourTarget.WarrenOverview => new[] { SummaryStrip },
        _ => Array.Empty<Rectangle>(),
    };

    private static int CardW => (GridPanel.Width - GridPad * 2 - CardGap * 3) / 4;
    private static int CardH => (GridPanel.Height - GridPad * 2 - CardGap) / 2;

    private static Rectangle Card(int i) =>
        new(GridPanel.X + GridPad + i % 4 * (CardW + CardGap),
            GridPanel.Y + GridPad + i / 4 * (CardH + CardGap), CardW, CardH);

    /// <summary>
    /// WHERE A CARD'S ROWS SIT, from the height it was given. The name, the resource row, the figure and
    /// the chip are always there; the icon plate and the milestone caption take what is left between the
    /// resource row and the figure, in that order — a facility's own art is what tells eight cards
    /// apart at a glance, and the milestone is repeated in the inspector.
    /// </summary>
    private readonly record struct CardRows(int NameY, int RowY, int FigureY, int MilestoneY, Rectangle Plate, Rectangle Chip)
    {
        public bool HasMilestone => MilestoneY >= 0;
        public bool HasPlate => Plate.Height > 0;
    }

    private static CardRows LayoutCard(Rectangle card)
    {
        var nameY = card.Y + UiMetrics.Space(12);
        var rowY = nameY + UiTypography.Pitch(UiTypography.Headline) - 1;
        var rowEnd = rowY + UiTypography.Pitch(UiTypography.Body);
        var chip = new Rectangle(card.X + CardPadX, card.Bottom - UiMetrics.Space(16) - ChipHeight,
                                 card.Width - CardPadX * 2, ChipHeight);
        var plateTop = rowEnd + UiMetrics.Space(5);
        var plateMax = UiMetrics.Control(100);
        var plateMin = UiMetrics.IconSize;

        // The figure with the caption under it, or the figure alone against the chip.
        var milestoneY = chip.Y - 2 - UiTypography.Pitch(UiTypography.Secondary);
        var figureWith = milestoneY - UiTypography.Pitch(UiTypography.Headline) + 2;
        var figureAlone = chip.Y - UiTypography.Pitch(UiTypography.Headline);
        var roomWith = figureWith - UiMetrics.Space(4) - plateTop;
        var roomAlone = figureAlone - UiMetrics.Space(4) - plateTop;

        Rectangle PlateOf(int h)
        {
            // The hex keeps its shape as it shrinks: 88 wide for every 100 tall, as the art was drawn.
            var w = Math.Min(UiMetrics.Control(88), h * 88 / 100);
            return new Rectangle(card.Center.X - w / 2, plateTop, w, h);
        }

        if (roomWith >= plateMin)
            return new CardRows(nameY, rowY, figureWith, milestoneY, PlateOf(Math.Min(plateMax, roomWith)), chip);
        if (roomAlone >= plateMin)
            return new CardRows(nameY, rowY, figureAlone, -1, PlateOf(Math.Min(plateMax, roomAlone)), chip);
        if (roomWith >= 0)
            return new CardRows(nameY, rowY, figureWith, milestoneY, Rectangle.Empty, chip);
        return new CardRows(nameY, rowY, figureAlone, -1, Rectangle.Empty, chip);
    }

    private Color ResColor(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => GleamC, WarrenResource.Dust => DustC,
        WarrenResource.Scrap => ScrapC, _ => EssenceC,
    };

    private static string ResName(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => "GLEAM", WarrenResource.Dust => "DUST",
        WarrenResource.Scrap => "SCRAP", _ => "ESSENCE",
    };

    /// <summary>Where this currency is actually spent — the Warren's honest answer to "what for?".</summary>
    private static string SinkLine(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => "GLEAM PAYS FOR TRAINING AND THE FORGE",
        WarrenResource.Dust => "DUST PAYS TO START A HUNT DEEPER",
        WarrenResource.Scrap => "SCRAP PAYS FOR FORGE UPGRADES",
        _ => "ESSENCE PAYS TO SET GEMS IN THE FORGE",
    };

    /// <summary>What this facility actually pays per minute — Core's raw output through Core's own
    /// multiplier, which is what makes a card's figure reconcile with the strip's total.</summary>
    private int Boosted(Facility f) => (int)MathF.Round(f.BaseOutputPerMin * Warren.Multiplier(f.Info.Produces));

    private int BoostedNext(Facility f) => (int)MathF.Round(f.NextLevelOutput * Warren.Multiplier(f.Info.Produces));

    /// <summary>
    /// The currency's real icon (Gleam coin, Memory Dust, the Forge's material gems), the tinted
    /// diamond only when the art is missing.
    /// </summary>
    private void ResGlyph(SpriteBatch b, Rectangle box, WarrenResource? r, Color? fallback = null)
    {
        var key = r switch
        {
            WarrenResource.Gleam => "ui_gleam_coin",
            WarrenResource.Dust => "ui_memory_dust",
            WarrenResource.Scrap => "mat_scrap",
            WarrenResource.Essence => "mat_essence",
            _ => "",
        };
        if (key.Length > 0 && _ui.Assets.Get(key) is { } tex) { b.Draw(tex, box, Color.White); return; }
        _ui.Diamond(b, box, fallback ?? (r is { } rr ? ResColor(rr) : Dim));
    }

    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    private void Outline(SpriteBatch b, Rectangle r, Color c, int px)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, px), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - px, r.Width, px), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, px, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - px, r.Y, px, r.Height), c);
    }

    private string? _tip;
    private Point _tipAt;

    private void Tip(Rectangle r, Point hit, string text)
    {
        if (r.Contains(hit)) { _tip = text; _tipAt = hit; }
    }

    // The away line is built only when the summary it describes changes — this screen redraws sixty
    // times a second and the string is the same every one of them.
    private WelcomeSummary? _awayFrom;
    // Seeded with the never-been-away line: both fields start null, so a cache keyed on "did it
    // change?" would never build the first string and the row would draw empty.
    private string _awayLine = AwayNever;
    private bool _awayBuilt;

    private static readonly string AwayNever =
        $"THE WARREN KEEPS EARNING WHILE THE GAME IS CLOSED — UP TO {SaveSystem.MaxOfflineSeconds / 3600:0} HOURS AWAY";

    // ── THE WHEEL. The host latches the wheel once a frame and hands it to the screens whose Update
    //    takes it; this screen's entry point predates any of them and takes only the cursor and the
    //    click, so the notches are latched here from the same source the host reads. It is the wheel
    //    only — the cursor is the parameter, hit-tested as it is. (A `wheel` argument on Draw, passed
    //    from Game1.MouseWheel, is the shared shape this should take.) ─────────────────────────────
    private int _wheelPrev;
    private long _wheelSeenAt;

    /// <summary>
    /// A gap longer than this between two frames means the screen was not being drawn — the player was
    /// elsewhere — and the wheel they turned there must not arrive here as one leap through the list.
    /// </summary>
    private const int WheelResyncMs = 250;

    private int WheelNotches()
    {
        var v = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
        var now = Environment.TickCount64;
        var stale = now - _wheelSeenAt > WheelResyncMs;   // also the first frame ever, which latches only
        _wheelSeenAt = now;
        var notches = stale ? 0 : (v - _wheelPrev) / 120;   // one notch is 120, as Game1 reads it
        _wheelPrev = v;
        return notches;
    }

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // The cursor arrives in page space (Game1.PageCursor); it is hit-tested as it is.
        var hit = mouse;
        _tip = null;
        // Latched every frame so a notch turned over the grid is not applied later over the inspector.
        var wheel = WheelNotches();
        if (!InspectorPanel.Contains(hit)) wheel = 0;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));   // scrim so panels pop
        _ui.TextCenterBig(b, "WARREN", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);
        // (The subtitle is gone. What it promised — the place earns while you are away — is now the
        //  strip's own last line, said with the figures from the absence that actually happened.)

        DrawSummary(b);
        DrawGrid(b, hit, clicked);
        DrawInspector(b, hit, clicked, wheel);
        if (_tip is { } tip) _ui.HoverTip(b, tip, _tipAt);
        if (DevWarrenDebug) DrawDebug(b);
    }

    /// <summary>
    /// THE WARREN'S RANK, STRUCK ON ITS OWN MEDALLION — the shipping gold octagon with the number in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest 2026-08-29: "in the warren overview panel, there is no LEVEL icon". There WAS a crest,
    /// and that was the problem: it was <see cref="UiKit.Diamond"/> in the Insight purple, which is the
    /// shape this kit draws when a key is MISSING. So the one badge on the screen that names the base's
    /// own rank wore the placeholder for art that had not shipped — except it had.
    /// <c>ui_medallion_hex</c> is a hollow gold medallion frame drawn for exactly this, and a medallion
    /// with a numeral inside it is the one badge every player already reads as a level without being
    /// told. The words WARREN LEVEL stay beside it, spelled out, for the reader who does not.
    /// </para>
    /// <para>
    /// The numeral SHRINKS rather than spilling over the rim. The medallion's hollow is about 56% of its
    /// width; a three-figure warren is a long game away, but it is a game away rather than impossible,
    /// and a badge that breaks at level 100 is a bug with a date on it.
    /// </para>
    /// </remarks>
    /// <summary>
    /// THE WARREN'S CREST: its own drawn icon (a lit burrow mouth) with a GOLD PROGRESS ARC around it —
    /// the arc closes as the warren fills its level, so the badge shows how far along the place is
    /// before a number is read.
    /// </summary>
    /// <remarks>
    /// The level's numeral used to be struck inside a medallion. That is not what a crest is for
    /// (playtest 2026-08-30: "I did not say write the level on the icon — make a warren icon that
    /// conveys its progress, think of it as a medallion"), and the number is already beside it in words.
    /// The arc starts at twelve o'clock and runs clockwise, the same direction as the hunt's cooldowns.
    /// </remarks>
    private void LevelBadge(SpriteBatch b, Rectangle box, float progress)
    {
        if (!_ui.Icon(b, "icon_warren_crest", box, Color.White)) _ui.Diamond(b, box, UiInk.Empty);

        var centre = new Vector2(box.Center.X, box.Center.Y);
        var radius = box.Width * 0.54f;
        const int segments = 96;
        var filled = Math.Clamp(progress, 0f, 1f);
        for (var k = 0; k < segments; k++)
        {
            var t0 = k / (float)segments;
            var a0 = -MathF.PI / 2f + MathF.Tau * t0;
            var a1 = -MathF.PI / 2f + MathF.Tau * (k + 1) / segments;
            var lit = t0 < filled;
            var p0 = centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var p1 = centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            _ui.LineSeg(b, p0, p1, lit ? 5f : 3f, lit ? Gold : Dim);
        }
    }

    // ── THE SUMMARY STRIP. One region, five facts, and the away line. ────────────────────────
    //
    // It replaces a 388 px OVERVIEW column and a 920 px WARREN BONUSES panel that between them spent
    // half the screen on five derived percentages and the formula behind them — a question nobody
    // asked — while the ceiling that stops every upgrade was explained eight times, once per card,
    // and the away earnings were not shown at all.
    private void DrawSummary(SpriteBatch b)
    {
        var strip = SummaryStrip;
        _ui.Plate(b, strip);
        var ix = strip.X + GridPad;
        var iw = strip.Width - GridPad * 2;
        // Proportional cells: written as fixed widths the third one ran off the strip at 125 %.
        var aw = iw * 26 / 100;
        var bw = iw * 28 / 100;
        var cw = iw - aw - bw;
        var ax = ix;
        var bx = ax + aw;
        var cx = bx + bw;
        // Everything in the strip ends above the away line's rule.
        var stripFloor = strip.Y + StripRuleY - 2;

        // CELL A — the Warren's own rank.
        var pct = Warren.XpToNext > 0 ? Warren.Xp / (float)Warren.XpToNext : 1f;
        var badge = UiMetrics.Control(72);
        LevelBadge(b, new Rectangle(ax, strip.Y + UiMetrics.Space(20), badge, badge), pct);
        var labelX = ax + badge + UiMetrics.Space(14);
        var labelW = aw - badge - UiMetrics.Space(14) - UiMetrics.Space(8);
        // The WORDS stop a breath short of the hairline that closes the cell; only the bar may run to
        // the cell's edge. Measured against the bar's width, "0 OF 3,000 TO LEVEL 2" sat on the hairline
        // at 125 %.
        var wordsW = bx - UiMetrics.Space(14) - UiMetrics.Space(6) - labelX;
        var labelY = strip.Y + UiMetrics.Space(24);
        // A SHORTER SENTENCE, NEVER A SHORTER NUMBER. Shortened to fit, this read "WARREN LEV..."
        // at 125 % — the one thing the cell exists to say was the half that got cut. The word WARREN
        // is the screen's own title, so it is what goes.
        var levelLabel = $"WARREN LEVEL {Warren.Level}";
        if (_ui.MeasureBig(levelLabel, UiTypography.Headline) > wordsW) levelLabel = $"LEVEL {Warren.Level}";
        _ui.TextBig(b, levelLabel, labelX, labelY, Bone, UiTypography.Headline);
        var bar = new Rectangle(labelX, labelY + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(2),
                                labelW, UiMetrics.Control(26));
        _ui.BarArt(b, bar, pct, "xp");
        // UNDER the bar, not inside it: a number printed on a bar's own fill is a number read against
        // whatever colour happens to be behind it.
        var xpLabel = $"{Warren.Xp:N0} OF {Warren.XpToNext:N0} TO LEVEL {Warren.Level + 1}";
        if (_ui.MeasureBig(xpLabel, UiTypography.Secondary) > wordsW)
            xpLabel = $"{Warren.Xp:N0} OF {Warren.XpToNext:N0}";
        _ui.TextBig(b, xpLabel, labelX, bar.Bottom + UiMetrics.Space(8), Slate, UiTypography.Secondary);

        // TWO HAIRLINES between the three cells. Without them cell B's right-aligned figures sat
        // against cell C's first words and read as C's numbers.
        var hairTop = strip.Y + UiMetrics.Space(20);
        _ui.Fill(b, new Rectangle(bx - UiMetrics.Space(14), hairTop, 1, stripFloor - hairTop), Dim);
        _ui.Fill(b, new Rectangle(cx - UiMetrics.Space(14), hairTop, 1, stripFloor - hairTop), Dim);

        // CELL B — the ceiling, explained ONCE for all eight cards.
        // THE LABEL AND ITS NUMBER SHARE A ROW WHEN THEY FIT AND THE LABEL WRAPS BESIDE THE NUMBER
        // WHEN THEY DO NOT. Held on one row at every width, "FACILITIES CAN REACH LEVEL" was cut to
        // "FACILITIES CAN REACH L..." at 125 %, which says nothing at all. Stacked — the label over
        // the number — it fit at 125 % but at 150 % the stack pushed the sentence under it through
        // the strip's floor; two caption lines beside a headline figure are shorter than a caption
        // OVER a figure, and that difference is the sentence's second line.
        var factY = strip.Y + UiMetrics.Space(18);
        var factRoom = bw - UiMetrics.Space(20);
        var factGap = UiMetrics.Space(20);

        void Fact(string label, string value)
        {
            var vw = _ui.MeasureBig(value, UiTypography.Headline);
            if (_ui.MeasureBig(label, UiTypography.Secondary) + vw + factGap <= factRoom)
            {
                _ui.TextBig(b, label, bx, factY + UiMetrics.Space(8), Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, value, bx + bw - UiMetrics.Space(28), factY, Bone, UiTypography.Headline);
                factY += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
                return;
            }
            var beside = _ui.WrapBig(label, factRoom - vw - factGap, UiTypography.Secondary);
            if (beside.Count <= 2)
            {
                _ui.TextRightBig(b, value, bx + bw - UiMetrics.Space(28), factY, Bone, UiTypography.Headline);
                var ly = factY;
                foreach (var l in beside)
                {
                    _ui.TextBig(b, l, bx, ly, Slate, UiTypography.Secondary);
                    ly += UiTypography.Pitch(UiTypography.Secondary);
                }
                factY = Math.Max(ly, factY + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6));
                return;
            }
            // Only when even two lines cannot hold the label beside its number: the label over the number.
            _ui.TextBig(b, _ui.ShortenBig(label, factRoom, UiTypography.Secondary), bx, factY, Slate, UiTypography.Secondary);
            factY += UiTypography.Pitch(UiTypography.Secondary);
            _ui.TextBig(b, value, bx, factY, Bone, UiTypography.Headline);
            factY += UiTypography.Pitch(UiTypography.Headline);
        }

        Fact("YOUR DEEPEST WAVE", $"{DeepestWave}");
        Fact("FACILITIES CAN REACH LEVEL", $"{Warren.FacilityLevelCap}");
        var ruleY = factY + UiMetrics.Space(4);
        var ruleW = bw - UiMetrics.Space(8);
        var ruleLines = _ui.WrapBig($"EVERY {Warren.DepthPerFacilityLevel} WAVES DEEPER RAISES IT BY ONE LEVEL", ruleW, UiTypography.Secondary);
        // As many of its lines as the strip's floor allows, two at most — and a sentence that loses a
        // line SAYS SO: the last line it keeps ends in an ellipsis, never mid-thought.
        var ruleFit = Math.Min(Math.Min(ruleLines.Count, 2), Math.Max(0, (stripFloor - ruleY) / UiTypography.Pitch(UiTypography.Secondary)));
        for (var n = 0; n < ruleFit; n++)
        {
            var text = n == ruleFit - 1 && ruleFit < ruleLines.Count
                ? _ui.ShortenBig(string.Join(" ", ruleLines.Skip(n)), ruleW, UiTypography.Secondary)
                : ruleLines[n];
            _ui.TextBig(b, text, bx, ruleY, Slate, UiTypography.Secondary);
            ruleY += UiTypography.Pitch(UiTypography.Secondary);
        }

        // CELL C — what it all makes, per minute, with the bonuses already in it.
        _ui.TextBig(b, "EVERY MINUTE, WITH WARREN BONUSES", cx, strip.Y + StripCaptionY, Slate, UiTypography.Secondary);
        var chw = cw / 4;
        var glyph = UiMetrics.Control(28);
        var k = 0;
        foreach (var r in new[] { WarrenResource.Gleam, WarrenResource.Dust, WarrenResource.Scrap, WarrenResource.Essence })
        {
            var v = Warren.ProductionPerMinute(r);
            ResGlyph(b, new Rectangle(cx + k * chw, strip.Y + StripGlyphRowY, glyph, glyph), r);
            // A zero rate is drawn in the placeholder ink — the honest fresh-Warren state, where both
            // material facilities are still locked.
            _ui.TextBig(b, $"+{Ab(v)}", cx + k * chw + glyph + UiMetrics.Space(6), strip.Y + StripGlyphRowY - UiMetrics.Space(4),
                        v > 0 ? Bone : UiInk.Empty, UiTypography.Headline);
            _ui.TextBig(b, ResName(r), cx + k * chw, strip.Y + StripNamesY, v > 0 ? ResColor(r) : Slate, UiTypography.Secondary);
            k++;
        }
        // All that survives of WARREN BONUSES: the three multipliers themselves, and where they came from.
        _ui.TextBig(b, _ui.ShortenBig(
                        $"GLEAM ×{Warren.Multiplier(WarrenResource.Gleam):0.00}   ·   DUST ×{Warren.Multiplier(WarrenResource.Dust):0.00}   ·   SCRAP AND ESSENCE ×{Warren.Multiplier(WarrenResource.Scrap):0.00}",
                        cw, UiTypography.Secondary),
                    cx, strip.Y + StripMultiplierY, Slate, UiTypography.Secondary);
        _ui.TextBig(b, _ui.ShortenBig(
                        Warren.ConqueredRegions == 1
                            ? $"1 REGION TAKEN ADDS +{Warren.ConquestBonus * 100f:0}% TO ALL OF IT"
                            : $"{Warren.ConqueredRegions} REGIONS TAKEN ADD +{Warren.ConquestBonus * 100f:0}% TO ALL OF IT",
                        cw, UiTypography.Secondary),
                    cx, strip.Y + StripRegionsY, Slate, UiTypography.Secondary);

        // THE AWAY LINE — the screen's own question, answered with the figures from the absence that
        // actually happened. The host already had them; this screen never showed them.
        _ui.Fill(b, new Rectangle(ix, strip.Y + StripRuleY, iw, 1), Dim);
        if (!_awayBuilt || !Equals(_awayFrom, LastReturn))
        {
            _awayBuilt = true;
            _awayFrom = LastReturn;
            _awayLine = LastReturn is { } w && w.WarrenParts().Count > 0
                ? $"WHILE YOU WERE AWAY {WelcomeSummary.AwayText(w.AwaySeconds)} THE WARREN MADE {string.Join("  ·  ", w.WarrenParts())}"
                : AwayNever;
        }
        _ui.TextBig(b, _ui.ShortenBig(_awayLine, iw, UiTypography.Secondary), ix, strip.Y + StripAwayY, Bone, UiTypography.Secondary);
    }

    // ── THE GRID. Eight cards, each answering "can I upgrade this one?" without being clicked. ──
    private void DrawGrid(SpriteBatch b, Point hit, bool clicked)
    {
        // QUIET, not ornate: this is a grid of things to pick between, and it wore the gold frame
        // while the column that explains them wore the brown.
        _ui.Plate(b, GridPanel);
        var lockGlyph = UiMetrics.Control(20);
        // WHERE A LOCKED CARD WEARS ITS BADGE is decided once for the grid, not once per card: at 125 %
        // RITUAL NEST kept LOCKED beside its name while its three neighbours, whose names are longer,
        // had moved it down a row, and the four read as two kinds of card.
        var lockedRoom = CardPadX + _ui.MeasureBig("LOCKED", UiTypography.Caption) + UiMetrics.Space(22);
        var lockedBadgeBelow = false;
        foreach (var f in Warren.AllFacilities)
            if (!Warren.IsUnlocked(f.Kind) && _ui.MeasureBig(f.Info.Name, UiTypography.Headline) > CardW - lockedRoom - UiMetrics.Space(20))
                lockedBadgeBelow = true;
        var i = 0;
        foreach (var f in Warren.AllFacilities)
        {
            var index = i++;
            var card = Card(index);
            var rows = LayoutCard(card);
            var chip = rows.Chip;
            // The chip's icon and text sit centred in the chip's own height, whatever the profile made it.
            var chipIcon = new Rectangle(chip.X + UiMetrics.Space(10), chip.Y + (chip.Height - lockGlyph) / 2, lockGlyph, lockGlyph);
            var chipTextY = chip.Y + (chip.Height - UiTypography.Body) / 2;
            var chipTextX = chipIcon.Right + UiMetrics.Space(8);

            // Facilities unlock by conquest. A locked card is a promise, not a faucet — it says what
            // would open it, by NAME, and takes no clicks.
            if (!Warren.IsUnlocked(f.Kind))
            {
                _ui.Plate(b, card);
                // Centred in the space the LOCKED badge leaves, not in the whole card — BREEDING
                // CHAMBER at Headline reached the badge and the two words touched. And when that space
                // cannot hold a locked name (125 % and up: "SCAVENGE...", "HOARD VAU..."), the badge
                // moves down to the resource row — the row where an open card says its LEVEL, empty on
                // a locked one — and the name is centred in the whole card like every other name.
                if (!lockedBadgeBelow)
                {
                    _ui.TextCenterBig(b, f.Info.Name, card.X + (card.Width - lockedRoom) / 2, rows.NameY, Bone, UiTypography.Headline);
                    _ui.TextRightBig(b, "LOCKED", card.Right - CardPadX, card.Y + UiMetrics.Space(16), UiInk.Disabled, UiTypography.Caption);
                }
                else
                {
                    _ui.TextCenterBig(b, _ui.ShortenBig(f.Info.Name, card.Width - UiMetrics.Space(24), UiTypography.Headline),
                                      card.Center.X, rows.NameY, Bone, UiTypography.Headline);
                    _ui.TextRightBig(b, "LOCKED", card.Right - CardPadX, rows.RowY + (UiTypography.Body - UiTypography.Caption) / 2,
                                     UiInk.Disabled, UiTypography.Caption);
                }

                if (rows.HasPlate)
                {
                    var plateBox = rows.Plate;
                    _ui.Hex(b, plateBox, UiInk.Empty * 0.4f);
                    var lockBox = Math.Min(UiMetrics.IconSize, plateBox.Height);
                    _ui.Icon(b, "ui_slot_locked",
                             new Rectangle(plateBox.Center.X - lockBox / 2, plateBox.Center.Y - lockBox / 2, lockBox, lockBox), UiInk.Empty);
                }

                // WHICH conquest, not "another region": the chain is linear, so the next one is known.
                // Facility k opens at ConqueredRegions >= k - 1 (UnlockedFacilityCount = 2 + conquered),
                // so the shortfall is measured from the card's OWN index — not the loop counter, which
                // has already moved on and made every locked card ask for one region too many.
                var need = index - 1 - Warren.ConqueredRegions;
                var next = Warren.ConqueredRegions >= 0 && Warren.ConqueredRegions < Regions.All.Count
                    ? Regions.All[Warren.ConqueredRegions].Name : "";
                var line = need <= 1 && next.Length > 0 ? $"CONQUER {next}"
                         : need <= 1 ? "CONQUER ONE MORE REGION"
                         : $"CONQUER {need} MORE REGIONS";
                // A SHORTER SENTENCE BEFORE AN ELLIPSIS. At 125 % the chip read "CONQUER 2 MOR..." and
                // "CONQUER CINDE..." — the verb kept and the one word that mattered cut. The chip's lock
                // glyph already says "a requirement", so the verb is what goes: "2 MORE REGIONS",
                // "CINDERWORKS". The tooltip keeps the whole line.
                var chipRoom = chip.Right - UiMetrics.Space(10) - chipTextX;
                var chipLine = _ui.MeasureBig(line, UiTypography.Body) <= chipRoom ? line
                             : _ui.ShortenBig(need <= 1 && next.Length > 0 ? next
                                              : need <= 1 ? "ONE MORE REGION"
                                              : $"{need} MORE REGIONS", chipRoom, UiTypography.Body);
                _ui.Plate(b, chip);
                _ui.Icon(b, "ui_slot_locked", chipIcon, Bone);
                _ui.TextBig(b, chipLine, chipTextX, chipTextY, Bone, UiTypography.Body);
                Tip(card, hit, $"{f.Info.Name} — LOCKED. {line} TO OPEN IT.");
                continue;
            }

            var sel = f.Kind == _selected;
            // A new selection opens its inspector at the top, wherever the last one was scrolled to.
            if (UiKit.ClickedIn(card, hit, clicked) && !sel) { _selected = f.Kind; _inspectorFirst = 0; }
            var rc = ResColor(f.Info.Produces);

            _ui.Plate(b, card);
            // The name is ALWAYS legible: unselected cards drew it in Slate, so "not selected" read as
            // "unavailable" on seven of the eight.
            _ui.TextCenterBig(b, _ui.ShortenBig(f.Info.Name, card.Width - UiMetrics.Space(24), UiTypography.Headline),
                              card.Center.X, rows.NameY, Bone, UiTypography.Headline);

            ResGlyph(b, new Rectangle(card.X + CardPadX, rows.RowY + 2, UiMetrics.IconSmall, UiMetrics.IconSmall), f.Info.Produces, rc);
            _ui.TextBig(b, ResName(f.Info.Produces), card.X + CardPadX + UiMetrics.IconSmall + UiMetrics.Space(6), rows.RowY, rc, UiTypography.Body);
            // GOLD ONLY WHEN SELECTED. Every card printed its LEVEL in gold, so selection had nothing
            // left to win with.
            _ui.TextRightBig(b, $"LEVEL {f.Level}", card.Right - CardPadX, rows.RowY, sel ? Gold : Bone, UiTypography.Body);

            if (rows.HasPlate)
            {
                var plate = rows.Plate;
                _ui.Hex(b, plate, rc * (sel ? 0.55f : 0.38f));
                var icon = $"icon_facility_{f.Kind.ToString().ToLowerInvariant()}";
                if (!_ui.Icon(b, icon, new Rectangle(plate.X + 2, plate.Y - 4, plate.Width - 4, plate.Height + 8)))
                    _ui.Hex(b, plate, rc);
            }

            // THE FIGURE THE CARD PAYS, with the bonuses in it — the raw one never reconciled with the
            // strip's total, and nothing said which was which.
            _ui.TextBig(b, $"+{Ab(Boosted(f))} /min", card.X + CardPadX, rows.FigureY, Bone, UiTypography.Headline);
            if (f.MilestoneTier > 0)
                _ui.TextRightBig(b, $"×{f.MilestoneMultiplier:0.00}", card.Right - CardPadX, rows.FigureY + UiMetrics.Space(4), Gold, UiTypography.Secondary);
            if (rows.HasMilestone)
                _ui.TextBig(b, _ui.ShortenBig($"MILESTONE AT LEVEL {f.NextMilestoneLevel}", card.Width - CardPadX * 2, UiTypography.Secondary),
                            card.X + CardPadX, rows.MilestoneY, Slate, UiTypography.Secondary);

            // ── THE CHIP: the answer the player had to click eight cards to find. ──
            if (Warren.CanUpgrade(f.Kind, GleamOwned, DustOwned))
            {
                _ui.Plate(b, chip, Gold);
                _ui.TextBig(b, "UPGRADE READY", chip.X + UiMetrics.Space(14), chipTextY, Gold, UiTypography.Body);
            }
            else if (Warren.IsAtLevelCap(f.Kind))
            {
                _ui.Plate(b, chip);
                _ui.Icon(b, "ui_slot_locked", chipIcon, Bone);
                _ui.TextBig(b, _ui.ShortenBig($"REACH WAVE {Warren.DepthForNextLevel(f.Kind)}", chip.Right - UiMetrics.Space(10) - chipTextX, UiTypography.Body),
                            chipTextX, chipTextY, Bone, UiTypography.Body);
            }
            else
            {
                var cost = Warren.UpgradeCost(f.Kind);
                var shortG = cost.Gleam - GleamOwned;
                var shortD = cost.Dust - DustOwned;
                var parts = new List<string>();
                if (shortG > 0) parts.Add($"{Ab(shortG)} GLEAM");
                if (shortD > 0) parts.Add($"{Ab(shortD)} DUST");
                _ui.Plate(b, chip);
                _ui.TextBig(b, _ui.ShortenBig($"NEEDS {string.Join("  ·  ", parts)}", chip.Width - UiMetrics.Space(14) - UiMetrics.Space(10), UiTypography.Body),
                            chip.X + UiMetrics.Space(14), chipTextY, Ember, UiTypography.Body);
            }

            if (sel) Outline(b, card, Gold, 3);
            else if (card.Contains(hit)) Outline(b, card, Bone, 1);
            Tip(card, hit, $"{f.Info.Name} — {f.Info.Description}");
        }
    }

    // ── THE INSPECTOR — the house grammar (§6). ─────────────────────────────────────────────────
    //
    // Its rows are BUILT first and DRAWN second. Everything above the refusal line is a list of rows
    // with known heights; when the list is taller than the room (which it is at 150 %, and at 125 %
    // for a capped facility), the rows scroll under the wheel with a bar in the lane the text gives
    // up, and the refusal line and the one lit button stay anchored at the bottom where they can
    // always be reached (§17–§18). At 100 % every row fits and nothing moves.

    private enum RowKind { Head, Name, Line, Gap, Rule, Pair, Locked, Cost }

    private readonly record struct Row(RowKind Kind, int H, string A = "", string B = "", string C = "",
                                       Color Ink = default, int Px = 0, WarrenResource Res = WarrenResource.Gleam, bool Ok = true);

    private readonly List<Row> _rows = new();
    private int _inspectorFirst = PosedScroll;

    /// <summary>
    /// The capture rig can pose the inspector SCROLLED — RH_SHOT_SCROLL=&lt;first row&gt;, clamped to the
    /// list's end like a wheel would be — so the scrolled state is photographed rather than described.
    /// The rig cannot turn a wheel headlessly, and a state no fixture can pose has never been looked at.
    /// </summary>
    private static readonly int PosedScroll =
        int.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_SCROLL"), out var posed) ? Math.Max(0, posed) : 0;

    private void BuildInspectorRows(Facility f, int w, bool capped, WarrenCost cost)
    {
        _rows.Clear();
        void Head(string s) => _rows.Add(new Row(RowKind.Head, UiTypography.Pitch(UiTypography.Secondary), s, Ink: Slate, Px: UiTypography.Secondary));
        void Line(string s, Color c, int px, int max)
        {
            foreach (var l in _ui.WrapBig(s, w, px).Take(max))
                _rows.Add(new Row(RowKind.Line, UiTypography.Pitch(px), l, Ink: c, Px: px));
        }
        void Gap() => _rows.Add(new Row(RowKind.Gap, UiMetrics.Space(6)));
        void Rule() => _rows.Add(new Row(RowKind.Rule, UiMetrics.Space(14)));

        Head($"FACILITY · {ResName(f.Info.Produces)}");
        _rows.Add(new Row(RowKind.Name, UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4),
                          _ui.ShortenBig(f.Info.Name, w, UiTypography.Headline), Ink: Bone, Px: UiTypography.Headline));
        Line(f.Info.Description, Bone, UiTypography.Body, 3);
        Gap();
        Rule();

        Head("WHAT IT DOES");
        _rows.Add(new Row(RowKind.Pair, UiTypography.Pitch(UiTypography.Headline), $"+{Ab(Boosted(f))} /min", $"+{Ab(BoostedNext(f))} /min"));
        // The ONLY place the raw figure appears — and it is labelled, so the two can never be mistaken
        // for a disagreement.
        Line($"BEFORE WARREN BONUSES: +{Ab(f.BaseOutputPerMin)} → +{Ab(f.NextLevelOutput)}", Slate, UiTypography.Secondary, 2);
        Line(SinkLine(f.Info.Produces), Bone, UiTypography.Body, 2);
        Line($"MILESTONE AT LEVEL {f.NextMilestoneLevel} — A PERMANENT STEP UP IN OUTPUT", Bone, UiTypography.Body, 2);
        Gap();
        Rule();

        // YOU NEED FIRST is drawn ONLY when there is a requirement. It used to hold "INSTANT UPGRADE ·
        // NO WAIT" the rest of the time — filler in the slot reserved for the reason you cannot act.
        if (capped)
        {
            Head("YOU NEED FIRST");
            _rows.Add(new Row(RowKind.Locked, UiTypography.Pitch(UiTypography.Body),
                              $"REACH WAVE {Warren.DepthForNextLevel(_selected)} ON A HUNT", Ink: Bone, Px: UiTypography.Body));
            Gap();
            Rule();
        }

        Head("CURRENT STATE");
        Line($"LEVEL {f.Level}", Bone, UiTypography.Body, 1);
        Line(f.MilestoneTier == 0 ? "NO MILESTONE CROSSED YET"
             : f.MilestoneTier == 1 ? $"1 MILESTONE CROSSED — ×{f.MilestoneMultiplier:0.00} OUTPUT"
             : $"{f.MilestoneTier} MILESTONES CROSSED — ×{f.MilestoneMultiplier:0.00} OUTPUT",
             Slate, UiTypography.Secondary, 2);
        Gap();
        Rule();

        Head("WHAT IT COSTS");
        CostRow(WarrenResource.Gleam, "GLEAM", GleamOwned, cost.Gleam);
        CostRow(WarrenResource.Dust, "DUST", DustOwned, cost.Dust);
    }

    /// <summary>One cost row: what it takes, what you hold, and how much MORE you need — never an "X".</summary>
    private void CostRow(WarrenResource res, string label, long owned, int required)
    {
        var ok = owned >= required;
        _rows.Add(new Row(RowKind.Cost,
                          UiTypography.Pitch(UiTypography.Body) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4),
                          label,
                          required.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                          ok ? $"YOU HAVE {Ab(owned)}" : $"YOU NEED {required - owned:N0} MORE",
                          Res: res, Ok: ok));
    }

    private void DrawRow(SpriteBatch b, in Row r, int x, int y, int w)
    {
        switch (r.Kind)
        {
            case RowKind.Head:
            case RowKind.Name:
            case RowKind.Line:
                _ui.TextBig(b, r.A, x, y, r.Ink, r.Px);
                break;
            case RowKind.Rule:
                _ui.Fill(b, new Rectangle(x, y + UiMetrics.Space(4), w, 1), Dim);
                break;
            case RowKind.Pair:
                _ui.TextBig(b, r.A, x, y, Bone, UiTypography.Headline);
                _ui.TextRightBig(b, r.B, x + w, y, Met, UiTypography.Headline);
                _ui.TextCenterBig(b, "→", x + w / 2, y + 2, Slate, UiTypography.Headline);
                break;
            case RowKind.Locked:
            {
                var glyph = UiMetrics.Control(22);
                _ui.Icon(b, "ui_slot_locked", new Rectangle(x, y + 2, glyph, glyph), Bone);
                var tx = x + glyph + UiMetrics.Space(8);
                _ui.TextBig(b, _ui.ShortenBig(r.A, x + w - tx, UiTypography.Body), tx, y, Bone, UiTypography.Body);
                break;
            }
            case RowKind.Cost:
            {
                var glyph = UiMetrics.Control(26);
                ResGlyph(b, new Rectangle(x, y, glyph, glyph), r.Res, ResColor(r.Res));
                _ui.TextBig(b, r.A, x + glyph + UiMetrics.Space(10), y, Bone, UiTypography.Body);
                _ui.TextRightBig(b, r.B, x + w, y, r.Ok ? Bone : Ember, UiTypography.Body);
                _ui.TextRightBig(b, r.C, x + w, y + UiTypography.Pitch(UiTypography.Body), r.Ok ? Slate : Ember, UiTypography.Secondary);
                break;
            }
        }
    }

    private void DrawInspector(SpriteBatch b, Point hit, bool clicked, int wheel)
    {
        var panel = InspectorPanel;
        _ui.PanelQuiet(b, panel);
        // A locked selection can only arrive by state reset (the grid never selects a locked card);
        // fall back to the first open facility rather than posing a locked one.
        if (!Warren.IsUnlocked(_selected))
            _selected = Warren.AllFacilities.First(x => Warren.IsUnlocked(x.Kind)).Kind;
        var f = Warren.Facility(_selected);

        var x = UiKit.ContentLeft(panel);
        var w = UiKit.ContentRight(panel) - x;
        var cta = new Rectangle(x, panel.Bottom - UiMetrics.PanelPadding - UiMetrics.ButtonHeightPrimary, w, UiMetrics.ButtonHeightPrimary);
        var capped = Warren.IsAtLevelCap(_selected);
        var cost = f.UpgradeCost();
        var afford = GleamOwned >= cost.Gleam && DustOwned >= cost.Dust;
        // THE REFUSAL WRAPS, NEVER SHORTENS: it is the one line that says why the button is off, and at
        // 150 % the narrower inspector cut it to "THE WARREN CANNOT PASS YOUR DEE...". Its room is the
        // longer of the two sentences' wrapped height — one line at 100 and 125 %, two at 150 — and is
        // reserved whether or not a refusal is showing, so the rows above it never move between one
        // facility and the next.
        const string CappedRefusal = "THE WARREN CANNOT PASS YOUR DEEPEST WAVE.";
        const string PoorRefusal = "YOU CANNOT PAY FOR THIS LEVEL YET.";
        var refusalRows = Math.Clamp(Math.Max(_ui.WrapBig(CappedRefusal, w, UiTypography.Body).Count,
                                              _ui.WrapBig(PoorRefusal, w, UiTypography.Body).Count), 1, 2);
        var refusalY = cta.Y - UiMetrics.Space(8) - refusalRows * UiTypography.Pitch(UiTypography.Body);
        var floor = refusalY - UiMetrics.Space(8);
        var top = UiKit.TitleTop(panel);
        var room = floor - top;

        // Built at the full width first; only a list that overflows gives up the scrollbar's lane and
        // is built again for the narrower column it then has.
        BuildInspectorRows(f, w, capped, cost);
        var rowsW = w;
        if (_rows.Sum(r => r.H) > room)
        {
            rowsW = w - UiMetrics.ScrollbarWidth - UiMetrics.Gap;
            BuildInspectorRows(f, rowsW, capped, cost);
        }

        // The last first-row that still shows the list's end, so the wheel cannot run past it. The rows
        // from there to the end are the list's last page, which is what the bar's thumb stands for: it
        // reaches the track's foot exactly when the list is at its end.
        var total = _rows.Count;
        var maxFirst = total;
        for (int i = total - 1, h = 0; i >= 0; i--)
        {
            h += _rows[i].H;
            if (h > room) break;
            maxFirst = i;
        }
        var lastPage = total - maxFirst;
        _inspectorFirst = UiKit.Scrolled(_inspectorFirst, wheel, lastPage, total);

        var y = top;
        for (var i = _inspectorFirst; i < total; i++)
        {
            var r = _rows[i];
            if (y + r.H > floor) break;
            // A heading is never the last thing on the page with nothing under it: it waits for the
            // scroll that brings its first row along.
            if (r.Kind == RowKind.Head && i + 1 < total && y + r.H + _rows[i + 1].H > floor) break;
            DrawRow(b, in r, x, y, rowsW);
            y += r.H;
        }
        if (rowsW < w)
            _ui.ScrollBar(b, new Rectangle(x + w - UiMetrics.ScrollbarWidth, top, UiMetrics.ScrollbarWidth, room),
                          _inspectorFirst, lastPage, total);

        if (!afford || capped)
        {
            var ry = refusalY;
            foreach (var line in _ui.WrapBig(capped ? CappedRefusal : PoorRefusal, w, UiTypography.Body).Take(refusalRows))
            {
                _ui.TextBig(b, line, x, ry, Ember, UiTypography.Body);
                ry += UiTypography.Pitch(UiTypography.Body);
            }
        }

        // The label IS the reason when capped — a button reading DEPTH LOCKED told the player a state,
        // not a next step.
        if (_ui.Button(b, cta, capped ? $"REACH WAVE {Warren.DepthForNextLevel(_selected)}" : "UPGRADE",
                       hit, clicked, enabled: afford && !capped, ButtonStyle.Primary))
            _upgradeRequest = _selected;
    }

    /// <summary>
    /// Word-wrapped body text at the shared Body size.
    /// </summary>
    /// <remarks>
    /// It used to MEASURE with <c>_ui.Measure</c> (the default size) and DRAW with <c>_ui.Text</c>
    /// (also the default), while every other label on this screen goes through the sized calls. The
    /// default is much larger than <see cref="UiTypography.Body"/>, so the description paragraphs
    /// rendered roughly twice the size of their own headings and wrapped in the wrong places — the
    /// only text on the screen off the type scale.
    /// </remarks>
    private void DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        var px = UiTypography.Body;
        foreach (var line in _ui.WrapBig(text, width, px)) { _ui.TextBig(b, line, x, y, c, px); y += UiTypography.Pitch(px); }
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { SummaryStrip, GridPanel, InspectorPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav WARREN  facilities  sel {_selected}", GridPanel.X, 118, Gold, UiTypography.Secondary);
    }
}
