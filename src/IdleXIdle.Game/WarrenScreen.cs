using System;
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
    //    from UiKit.Page, so 125 % shrinks the screen instead of clipping it. ─────────────────────
    private const int Top = 150;            // the hint slot owns canvas y 86-134
    private const int BottomMargin = 60;
    private const int StripHeight = 200;
    private const int GridPad = 24, CardGap = 18;

    private static Rectangle InspectorPanel =>
        new(UiKit.PageRight(40) - 496, Top, 496, UiKit.PageBottom(BottomMargin) - Top);

    private static Rectangle SummaryStrip =>
        new(38, Top, InspectorPanel.X - 20 - 38, StripHeight);

    private static Rectangle GridPanel =>
        new(38, Top + StripHeight + 16, SummaryStrip.Width,
            UiKit.PageBottom(BottomMargin) - (Top + StripHeight + 16));

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

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        _tip = null;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));   // scrim so panels pop
        _ui.TextCenterBig(b, "WARREN", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);
        // (The subtitle is gone. What it promised — the place earns while you are away — is now the
        //  strip's own last line, said with the figures from the absence that actually happened.)

        DrawSummary(b);
        DrawGrid(b, hit, clicked);
        DrawInspector(b, hit, clicked);
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
        _ui.Plate(b, SummaryStrip);
        var ix = SummaryStrip.X + GridPad;
        var iw = SummaryStrip.Width - GridPad * 2;
        // Proportional cells: written as fixed widths the third one ran off the strip at 125 %.
        var aw = iw * 26 / 100;
        var bw = iw * 28 / 100;
        var cw = iw - aw - bw;
        var ax = ix;
        var bx = ax + aw;
        var cx = bx + bw;

        // CELL A — the Warren's own rank.
        var pct = Warren.XpToNext > 0 ? Warren.Xp / (float)Warren.XpToNext : 1f;
        LevelBadge(b, new Rectangle(ax, Top + 20, 72, 72), pct);
        // A SHORTER SENTENCE, NEVER A SHORTER NUMBER. Shortened to fit, this read "WARREN LEV..."
        // at 125 % — the one thing the cell exists to say was the half that got cut. The word WARREN
        // is the screen's own title, so it is what goes.
        var levelLabel = $"WARREN LEVEL {Warren.Level}";
        if (_ui.MeasureBig(levelLabel, UiTypography.Headline) > aw - 100) levelLabel = $"LEVEL {Warren.Level}";
        _ui.TextBig(b, levelLabel, ax + 86, Top + 24, Bone, UiTypography.Headline);
        _ui.BarArt(b, new Rectangle(ax + 86, Top + 60, aw - 94, 26), pct, "xp");
        // UNDER the bar, not inside it: a number printed on a bar's own fill is a number read against
        // whatever colour happens to be behind it.
        var xpLabel = $"{Warren.Xp:N0} OF {Warren.XpToNext:N0} TO LEVEL {Warren.Level + 1}";
        if (_ui.MeasureBig(xpLabel, UiTypography.Secondary) > aw - 100)
            xpLabel = $"{Warren.Xp:N0} OF {Warren.XpToNext:N0}";
        _ui.TextBig(b, xpLabel, ax + 86, Top + 94, Slate, UiTypography.Secondary);

        // TWO HAIRLINES between the three cells. Without them cell B's right-aligned figures sat
        // against cell C's first words and read as C's numbers.
        _ui.Fill(b, new Rectangle(bx - 14, Top + 20, 1, StripHeight - 60), Dim);
        _ui.Fill(b, new Rectangle(cx - 14, Top + 20, 1, StripHeight - 60), Dim);

        // CELL B — the ceiling, explained ONCE for all eight cards.
        // THE LABEL AND ITS NUMBER SHARE A ROW WHEN THEY FIT AND STACK WHEN THEY DO NOT. Held on one
        // row at every width, "FACILITIES CAN REACH LEVEL" was cut to "FACILITIES CAN REACH L..." at
        // 125 %, which says nothing at all.
        var factY = Top + 18;

        void Fact(string label, string value)
        {
            var vw = _ui.MeasureBig(value, UiTypography.Headline);
            if (_ui.MeasureBig(label, UiTypography.Secondary) + vw + 20 <= bw - 20)
            {
                _ui.TextBig(b, label, bx, factY + 8, Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, value, bx + bw - 28, factY, Bone, UiTypography.Headline);
                factY += UiTypography.Pitch(UiTypography.Headline) + 6;
                return;
            }
            _ui.TextBig(b, _ui.ShortenBig(label, bw - 20, UiTypography.Secondary), bx, factY, Slate, UiTypography.Secondary);
            factY += UiTypography.Pitch(UiTypography.Secondary);
            _ui.TextBig(b, value, bx, factY, Bone, UiTypography.Headline);
            factY += UiTypography.Pitch(UiTypography.Headline);
        }

        Fact("YOUR DEEPEST WAVE", $"{DeepestWave}");
        Fact("FACILITIES CAN REACH LEVEL", $"{Warren.FacilityLevelCap}");
        var ruleY = factY + 4;
        foreach (var line in _ui.WrapBig($"EVERY {Warren.DepthPerFacilityLevel} WAVES DEEPER RAISES IT BY ONE LEVEL",
                                         bw - 8, UiTypography.Secondary).Take(2))
        {
            _ui.TextBig(b, line, bx, ruleY, Slate, UiTypography.Secondary);
            ruleY += UiTypography.Pitch(UiTypography.Secondary);
        }

        // CELL C — what it all makes, per minute, with the bonuses already in it.
        _ui.TextBig(b, "EVERY MINUTE, WITH WARREN BONUSES", cx, Top + 16, Slate, UiTypography.Secondary);
        var chw = cw / 4;
        var k = 0;
        foreach (var r in new[] { WarrenResource.Gleam, WarrenResource.Dust, WarrenResource.Scrap, WarrenResource.Essence })
        {
            var v = Warren.ProductionPerMinute(r);
            ResGlyph(b, new Rectangle(cx + k * chw, Top + 44, 28, 28), r);
            // A zero rate is drawn in the placeholder ink — the honest fresh-Warren state, where both
            // material facilities are still locked.
            _ui.TextBig(b, $"+{Ab(v)}", cx + k * chw + 34, Top + 40, v > 0 ? Bone : UiInk.Empty, UiTypography.Headline);
            _ui.TextBig(b, ResName(r), cx + k * chw, Top + 76, v > 0 ? ResColor(r) : Slate, UiTypography.Secondary);
            k++;
        }
        // All that survives of WARREN BONUSES: the three multipliers themselves, and where they came from.
        _ui.TextBig(b, _ui.ShortenBig(
                        $"GLEAM ×{Warren.Multiplier(WarrenResource.Gleam):0.00}   ·   DUST ×{Warren.Multiplier(WarrenResource.Dust):0.00}   ·   SCRAP AND ESSENCE ×{Warren.Multiplier(WarrenResource.Scrap):0.00}",
                        cw, UiTypography.Secondary),
                    cx, Top + 104, Slate, UiTypography.Secondary);
        _ui.TextBig(b, _ui.ShortenBig(
                        Warren.ConqueredRegions == 1
                            ? $"1 REGION TAKEN ADDS +{Warren.ConquestBonus * 100f:0}% TO ALL OF IT"
                            : $"{Warren.ConqueredRegions} REGIONS TAKEN ADD +{Warren.ConquestBonus * 100f:0}% TO ALL OF IT",
                        cw, UiTypography.Secondary),
                    cx, Top + 132, Slate, UiTypography.Secondary);

        // THE AWAY LINE — the screen's own question, answered with the figures from the absence that
        // actually happened. The host already had them; this screen never showed them.
        _ui.Fill(b, new Rectangle(ix, Top + 162, iw, 1), Dim);
        if (!_awayBuilt || !Equals(_awayFrom, LastReturn))
        {
            _awayBuilt = true;
            _awayFrom = LastReturn;
            _awayLine = LastReturn is { } w && w.WarrenParts().Count > 0
                ? $"WHILE YOU WERE AWAY {WelcomeSummary.AwayText(w.AwaySeconds)} THE WARREN MADE {string.Join("  ·  ", w.WarrenParts())}"
                : AwayNever;
        }
        _ui.TextBig(b, _ui.ShortenBig(_awayLine, iw, UiTypography.Secondary), ix, Top + 170, Bone, UiTypography.Secondary);
    }

    // ── THE GRID. Eight cards, each answering "can I upgrade this one?" without being clicked. ──
    private void DrawGrid(SpriteBatch b, Point hit, bool clicked)
    {
        // QUIET, not ornate: this is a grid of things to pick between, and it wore the gold frame
        // while the column that explains them wore the brown.
        _ui.Plate(b, GridPanel);
        var i = 0;
        foreach (var f in Warren.AllFacilities)
        {
            var index = i++;
            var card = Card(index);
            var chip = new Rectangle(card.X + 18, card.Bottom - 54, card.Width - 36, 38);

            // Facilities unlock by conquest. A locked card is a promise, not a faucet — it says what
            // would open it, by NAME, and takes no clicks.
            if (!Warren.IsUnlocked(f.Kind))
            {
                _ui.Plate(b, card);
                // Centred in the space the LOCKED badge leaves, not in the whole card — BREEDING
                // CHAMBER at Headline reached the badge and the two words touched.
                _ui.TextCenterBig(b, _ui.ShortenBig(f.Info.Name, card.Width - 96, UiTypography.Headline),
                                  card.X + (card.Width - 76) / 2, card.Y + 12, Bone, UiTypography.Headline);
                _ui.TextRightBig(b, "LOCKED", card.Right - 18, card.Y + 16, UiInk.Disabled, UiTypography.Caption);

                var plateBox = new Rectangle(card.Center.X - 44, card.Y + 78, 88, 76);
                if (plateBox.Bottom < chip.Y - 8)
                {
                    _ui.Hex(b, plateBox, UiInk.Empty * 0.4f);
                    _ui.Icon(b, "ui_slot_locked",
                             new Rectangle(plateBox.Center.X - 20, plateBox.Center.Y - 20, 40, 40), UiInk.Empty);
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
                _ui.Plate(b, chip);
                _ui.Icon(b, "ui_slot_locked", new Rectangle(chip.X + 10, chip.Y + 9, 20, 20), Bone);
                _ui.TextBig(b, _ui.ShortenBig(line, chip.Width - 48, UiTypography.Body),
                            chip.X + 38, chip.Y + 7, Bone, UiTypography.Body);
                Tip(card, hit, $"{f.Info.Name} — LOCKED. {line} TO OPEN IT.");
                continue;
            }

            var sel = f.Kind == _selected;
            if (UiKit.ClickedIn(card, hit, clicked)) _selected = f.Kind;
            var rc = ResColor(f.Info.Produces);

            _ui.Plate(b, card);
            // The name is ALWAYS legible: unselected cards drew it in Slate, so "not selected" read as
            // "unavailable" on seven of the eight.
            _ui.TextCenterBig(b, _ui.ShortenBig(f.Info.Name, card.Width - 24, UiTypography.Headline),
                              card.Center.X, card.Y + 12, Bone, UiTypography.Headline);

            ResGlyph(b, new Rectangle(card.X + 18, card.Y + 47, 24, 24), f.Info.Produces, rc);
            _ui.TextBig(b, ResName(f.Info.Produces), card.X + 48, card.Y + 45, rc, UiTypography.Body);
            // GOLD ONLY WHEN SELECTED. Every card printed its LEVEL in gold, so selection had nothing
            // left to win with.
            _ui.TextRightBig(b, $"LEVEL {f.Level}", card.Right - 18, card.Y + 45, sel ? Gold : Bone, UiTypography.Body);

            var plateTop = card.Y + 78;
            var plateH = Math.Min(100, chip.Y - 62 - plateTop);
            if (plateH >= 40)
            {
                var plate = new Rectangle(card.Center.X - 44, plateTop, 88, plateH);
                _ui.Hex(b, plate, rc * (sel ? 0.55f : 0.38f));
                var icon = $"icon_facility_{f.Kind.ToString().ToLowerInvariant()}";
                if (!_ui.Icon(b, icon, new Rectangle(plate.X + 2, plate.Y - 4, plate.Width - 4, plate.Height + 8)))
                    _ui.Hex(b, plate, rc);
            }

            // THE FIGURE THE CARD PAYS, with the bonuses in it — the raw one never reconciled with the
            // strip's total, and nothing said which was which.
            _ui.TextBig(b, $"+{Ab(Boosted(f))} /min", card.X + 18, chip.Y - 58, Bone, UiTypography.Headline);
            if (f.MilestoneTier > 0)
                _ui.TextRightBig(b, $"×{f.MilestoneMultiplier:0.00}", card.Right - 18, chip.Y - 54, Gold, UiTypography.Secondary);
            _ui.TextBig(b, _ui.ShortenBig($"MILESTONE AT LEVEL {f.NextMilestoneLevel}", card.Width - 36, UiTypography.Secondary),
                        card.X + 18, chip.Y - 26, Slate, UiTypography.Secondary);

            // ── THE CHIP: the answer the player had to click eight cards to find. ──
            if (Warren.CanUpgrade(f.Kind, GleamOwned, DustOwned))
            {
                _ui.Plate(b, chip, Gold);
                _ui.TextBig(b, "UPGRADE READY", chip.X + 14, chip.Y + 7, Gold, UiTypography.Body);
            }
            else if (Warren.IsAtLevelCap(f.Kind))
            {
                _ui.Plate(b, chip);
                _ui.Icon(b, "ui_slot_locked", new Rectangle(chip.X + 10, chip.Y + 9, 20, 20), Bone);
                _ui.TextBig(b, _ui.ShortenBig($"REACH WAVE {Warren.DepthForNextLevel(f.Kind)}", chip.Width - 48, UiTypography.Body),
                            chip.X + 38, chip.Y + 7, Bone, UiTypography.Body);
            }
            else
            {
                var cost = Warren.UpgradeCost(f.Kind);
                var shortG = cost.Gleam - GleamOwned;
                var shortD = cost.Dust - DustOwned;
                var parts = new System.Collections.Generic.List<string>();
                if (shortG > 0) parts.Add($"{Ab(shortG)} GLEAM");
                if (shortD > 0) parts.Add($"{Ab(shortD)} DUST");
                _ui.Plate(b, chip);
                _ui.TextBig(b, _ui.ShortenBig($"NEEDS {string.Join("  ·  ", parts)}", chip.Width - 24, UiTypography.Body),
                            chip.X + 14, chip.Y + 7, Ember, UiTypography.Body);
            }

            if (sel) Outline(b, card, Gold, 3);
            else if (card.Contains(hit)) Outline(b, card, Bone, 1);
            Tip(card, hit, $"{f.Info.Name} — {f.Info.Description}");
        }
    }

    // ── THE INSPECTOR — the house grammar (§6). ─────────────────────────────────────────────────
    private void DrawInspector(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, InspectorPanel);
        // A locked selection can only arrive by state reset (the grid never selects a locked card);
        // fall back to the first open facility rather than posing a locked one.
        if (!Warren.IsUnlocked(_selected))
            _selected = Warren.AllFacilities.First(x => Warren.IsUnlocked(x.Kind)).Kind;
        var f = Warren.Facility(_selected);

        var x = UiKit.ContentLeft(InspectorPanel);
        var w = UiKit.ContentRight(InspectorPanel) - x;
        var cta = new Rectangle(x, InspectorPanel.Bottom - 92, w, 68);
        var capped = Warren.IsAtLevelCap(_selected);
        var cost = f.UpgradeCost();
        var afford = GleamOwned >= cost.Gleam && DustOwned >= cost.Dust;
        var refusalY = cta.Y - 8 - UiTypography.Pitch(UiTypography.Body);
        var floor = refusalY - 8;
        var y = UiKit.TitleTop(InspectorPanel);

        void Head(string s)
        {
            if (y + UiTypography.Pitch(UiTypography.Secondary) > floor) return;
            _ui.TextBig(b, s, x, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
        }

        void Line(string s, Color c, int px = UiTypography.Body, int max = 3)
        {
            foreach (var l in _ui.WrapBig(s, w, px).Take(max))
            {
                if (y + UiTypography.Pitch(px) > floor) return;
                _ui.TextBig(b, l, x, y, c, px);
                y += UiTypography.Pitch(px);
            }
        }

        void Rule()
        {
            if (y + 14 > floor) return;
            _ui.Fill(b, new Rectangle(x, y + 4, w, 1), Dim);
            y += 14;
        }

        Head($"FACILITY · {ResName(f.Info.Produces)}");
        if (y + UiTypography.Pitch(UiTypography.Headline) <= floor)
        {
            _ui.TextBig(b, _ui.ShortenBig(f.Info.Name, w, UiTypography.Headline), x, y, Bone, UiTypography.Headline);
            y += UiTypography.Pitch(UiTypography.Headline) + 4;
        }
        Line(f.Info.Description, Bone, UiTypography.Body, 3);
        y += 6;
        Rule();

        Head("WHAT IT DOES");
        if (y + UiTypography.Pitch(UiTypography.Headline) <= floor)
        {
            _ui.TextBig(b, $"+{Ab(Boosted(f))} /min", x, y, Bone, UiTypography.Headline);
            _ui.TextRightBig(b, $"+{Ab(BoostedNext(f))} /min", x + w, y, Met, UiTypography.Headline);
            _ui.TextCenterBig(b, "→", x + w / 2, y + 2, Slate, UiTypography.Headline);
            y += UiTypography.Pitch(UiTypography.Headline);
        }
        // The ONLY place the raw figure appears — and it is labelled, so the two can never be mistaken
        // for a disagreement.
        Line($"BEFORE WARREN BONUSES: +{Ab(f.BaseOutputPerMin)} → +{Ab(f.NextLevelOutput)}", Slate, UiTypography.Secondary, 2);
        Line(SinkLine(f.Info.Produces), Bone, UiTypography.Body, 2);
        Line($"MILESTONE AT LEVEL {f.NextMilestoneLevel} — A PERMANENT STEP UP IN OUTPUT", Bone, UiTypography.Body, 2);
        y += 6;
        Rule();

        // YOU NEED FIRST is drawn ONLY when there is a requirement. It used to hold "INSTANT UPGRADE ·
        // NO WAIT" the rest of the time — filler in the slot reserved for the reason you cannot act.
        if (capped)
        {
            Head("YOU NEED FIRST");
            if (y + UiTypography.Pitch(UiTypography.Body) <= floor)
            {
                _ui.Icon(b, "ui_slot_locked", new Rectangle(x, y + 2, 22, 22), Bone);
                _ui.TextBig(b, _ui.ShortenBig($"REACH WAVE {Warren.DepthForNextLevel(_selected)} ON A HUNT",
                                              w - 30, UiTypography.Body),
                            x + 30, y, Bone, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
            }
            y += 6;
            Rule();
        }

        Head("CURRENT STATE");
        Line($"LEVEL {f.Level}", Bone, UiTypography.Body, 1);
        Line(f.MilestoneTier == 0 ? "NO MILESTONE CROSSED YET"
             : f.MilestoneTier == 1 ? $"1 MILESTONE CROSSED — ×{f.MilestoneMultiplier:0.00} OUTPUT"
             : $"{f.MilestoneTier} MILESTONES CROSSED — ×{f.MilestoneMultiplier:0.00} OUTPUT",
             Slate, UiTypography.Secondary, 2);
        y += 6;
        Rule();

        Head("WHAT IT COSTS");
        DrawCost(b, ref y, floor, x, w, WarrenResource.Gleam, "GLEAM", GleamOwned, cost.Gleam);
        DrawCost(b, ref y, floor, x, w, WarrenResource.Dust, "DUST", DustOwned, cost.Dust);

        if (!afford || capped)
            _ui.TextBig(b, _ui.ShortenBig(capped ? "THE WARREN CANNOT PASS YOUR DEEPEST WAVE."
                                                 : "YOU CANNOT PAY FOR THIS LEVEL YET.", w, UiTypography.Body),
                        x, refusalY, Ember, UiTypography.Body);

        // The label IS the reason when capped — a button reading DEPTH LOCKED told the player a state,
        // not a next step.
        if (_ui.Button(b, cta, capped ? $"REACH WAVE {Warren.DepthForNextLevel(_selected)}" : "UPGRADE",
                       hit, clicked, enabled: afford && !capped, ButtonStyle.Primary))
            _upgradeRequest = _selected;
    }

    /// <summary>One cost row: what it takes, what you hold, and how much MORE you need — never an "X".</summary>
    private void DrawCost(SpriteBatch b, ref int y, int floor, int x, int w,
                          WarrenResource res, string label, long owned, int required)
    {
        if (y + UiTypography.Pitch(UiTypography.Body) + UiTypography.Pitch(UiTypography.Secondary) > floor) return;
        var ok = owned >= required;
        ResGlyph(b, new Rectangle(x, y, 26, 26), res, ResColor(res));
        _ui.TextBig(b, label, x + 36, y, Bone, UiTypography.Body);
        _ui.TextRightBig(b, required.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                         x + w, y, ok ? Bone : Ember, UiTypography.Body);
        y += UiTypography.Pitch(UiTypography.Body);
        _ui.TextRightBig(b, ok ? $"YOU HAVE {Ab(owned)}" : $"YOU NEED {required - owned:N0} MORE",
                         x + w, y, ok ? Slate : Ember, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary) + 4;
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
        const int px = UiTypography.Body;
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
