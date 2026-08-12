using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Warrens;

namespace ResonanceHunter.Client;

/// <summary>
/// The WARREN screen (nav: WARREN): a facility-management dashboard. Overview + 8-facility grid + a bonuses
/// strip + a facility detail/upgrade panel, built to the Warren production spec (rev 1).
/// </summary>
/// <remarks>
/// Every value is real, from the <see cref="Core.Warrens.Warren"/> model the host owns: facility levels
/// and outputs, the Warren level/XP, the derived production bonuses, and the real upgrade costs in Gleam /
/// Mastery / Dust. The creature "den" (the old Warren) is preserved as a sub-view reached via the CREATURES
/// button. The host sets the model + owned balances each frame and consumes the upgrade / den requests.
/// </remarks>
public sealed class WarrenScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    private static readonly Color GleamC = new(0xF0, 0xA8, 0x30);   // gold coin track
    private static readonly Color MasteryC = new(0xC0, 0x6E, 0xE0); // purple gem track
    private static readonly Color DustC = new(0x5F, 0xE0, 0xC8);    // teal gem track

    private readonly UiKit _ui;
    public WarrenScreen(UiKit ui) => _ui = ui;

    // ── Host-set each frame ───────────────────────────────────────────────────────────────────────
    public Warren Warren { get; set; } = null!;
    public long GleamOwned { get; set; }
    public long MasteryOwned { get; set; }
    public long DustOwned { get; set; }
    public bool DevWarrenDebug { get; set; }

    // ── Host-consumed requests ──────────────────────────────────────────────────────────────────
    private FacilityKind? _upgradeRequest;
    public FacilityKind? ConsumeUpgrade() { var r = _upgradeRequest; _upgradeRequest = null; return r; }

    private FacilityKind _selected = FacilityKind.Nursery;

    // ── Spec §4 rectangles ──────────────────────────────────────────────────────────────────────
    private static readonly Rectangle OverviewPanel = new(28, 154, 388, 708);
    private static readonly Rectangle GridPanel = new(446, 154, 920, 500);
    private static readonly Rectangle BonusStrip = new(446, 676, 920, 186);
    private static readonly Rectangle DetailPanel = new(1400, 154, 480, 708);

    private static Rectangle Card(int i)
    {
        var col = i % 4;
        var row = i / 4;
        return new Rectangle(GridPanel.X + 24 + col * 222, GridPanel.Y + 58 + row * 212, 206, 200);
    }

    private Color ResColor(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => GleamC, WarrenResource.Mastery => MasteryC, _ => DustC,
    };

    private static string ResName(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => "GOLD", WarrenResource.Mastery => "MASTERY", _ => "NATURE",
    };

    private static string Ab(long v) => v >= 1_000_000
        ? $"{v / 1_000_000.0:0.#}M"
        : v >= 1000 ? $"{v / 1000.0:0.#}K" : v.ToString();

    public void Draw(SpriteBatch b) => Draw(b, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xC0));   // scrim so panels pop
        _ui.TextCenterBig(b, "WARREN", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "FACILITIES  ·  PRODUCTION  ·  UPGRADES", 960, 80, Slate, UiTypography.Secondary);

        DrawOverview(b, hit, clicked);
        DrawGrid(b, hit, clicked);
        DrawBonuses(b);
        DrawDetail(b, hit, clicked);
        if (DevWarrenDebug) DrawDebug(b);
    }

    private void DrawOverview(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.Panel(b, OverviewPanel);
        _ui.TextCenterBig(b, "WARREN OVERVIEW", OverviewPanel.Center.X, OverviewPanel.Y + 22, Gold, UiTypography.SectionTitle);

        // Crest + level + name.
        var crest = new Rectangle(OverviewPanel.X + 40, OverviewPanel.Y + 68, 72, 84);
        _ui.Diamond(b, crest, MasteryC);
        _ui.TextBig(b, $"LEVEL {Warren.Level}", crest.Right + 20, OverviewPanel.Y + 78, Bone, UiTypography.PanelTitle);
        _ui.TextBig(b, Warren.Name, crest.Right + 20, OverviewPanel.Y + 112, MasteryC, UiTypography.Body);

        // XP bar to the next Warren level.
        var bar = new Rectangle(OverviewPanel.X + 32, OverviewPanel.Y + 176, OverviewPanel.Width - 64, 26);
        var pct = Warren.XpToNext > 0 ? Warren.Xp / (float)Warren.XpToNext : 1f;
        _ui.Bar(b, bar.X, bar.Y, bar.Width, bar.Height, pct, Gold);
        // Ink on the gold fill, Bone off it. Cream on gold measures ~1.6:1 — the same gold-on-gold
        // failure the boss bar had, and the reason this readout was invisible for most of the bar.
        var xpFrac = Warren.XpToNext > 0 ? Warren.Xp / (float)Warren.XpToNext : 0f;
        _ui.TextCenterBig(b, $"{Warren.Xp:N0} / {Warren.XpToNext:N0}", bar.Center.X, bar.Y + 4,
            xpFrac > 0.55f ? UiKit.Ink : Bone, UiTypography.Secondary);
        _ui.TextBig(b, $"NEXT LEVEL {Warren.Level + 1}", OverviewPanel.X + 32, OverviewPanel.Y + 214, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"+{Warren.AllProductionBonus * 100f:0}% ALL PRODUCTION", OverviewPanel.Right - 32, OverviewPanel.Y + 214, Met, UiTypography.Secondary);

        _ui.Fill(b, new Rectangle(OverviewPanel.X + 24, OverviewPanel.Y + 254, OverviewPanel.Width - 48, 2), Dim);

        // Total production, per real currency (bonuses applied).
        _ui.TextBig(b, "TOTAL PRODUCTION", OverviewPanel.X + 32, OverviewPanel.Y + 272, Gold, UiTypography.Body);
        _ui.TextRightBig(b, "(IDLE RATE)", OverviewPanel.Right - 32, OverviewPanel.Y + 276, Slate, UiTypography.Secondary);
        var y = OverviewPanel.Y + 320;
        foreach (var r in new[] { WarrenResource.Gleam, WarrenResource.Mastery, WarrenResource.Dust })
        {
            _ui.Diamond(b, new Rectangle(OverviewPanel.X + 36, y, 34, 34), ResColor(r));
            _ui.TextBig(b, $"+{Ab(Warren.ProductionPerMinute(r))} /min", OverviewPanel.X + 88, y + 2, Bone, UiTypography.PanelTitle);
            y += 52;
        }

        // Production runs on every screen (see Game1.TickFarms) — a quiet reminder that the base earns idle.
        _ui.Fill(b, new Rectangle(OverviewPanel.X + 24, OverviewPanel.Bottom - 124, OverviewPanel.Width - 48, 2), Dim);
        DrawWrapped(b, "Facilities earn idle income even while away.",
            OverviewPanel.X + 30, OverviewPanel.Bottom - 104, OverviewPanel.Width - 60, Slate);
    }

    private void DrawGrid(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.Panel(b, GridPanel);
        var i = 0;
        foreach (var f in Warren.AllFacilities)
        {
            var card = Card(i++);
            var sel = f.Kind == _selected;
            if (UiKit.ClickedIn(card, hit, clicked)) _selected = f.Kind;

            _ui.Fill(b, card, new Color(0x16, 0x12, 0x20, 0xD0));
            var rc = ResColor(f.Info.Produces);
            _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 4), sel ? Gold : rc * 0.6f);
            _ui.Fill(b, new Rectangle(card.X, card.Bottom - 4, card.Width, 4), sel ? Gold : rc * 0.35f);
            if (sel) { _ui.Fill(b, new Rectangle(card.X, card.Y, 4, card.Height), Gold); _ui.Fill(b, new Rectangle(card.Right - 4, card.Y, 4, card.Height), Gold); }

            _ui.TextCenterBig(b, f.Info.Name, card.Center.X, card.Y + 14, sel ? Bone : Slate, UiTypography.Secondary);
            // Milestone pips (top-right) — one gold gem per milestone crossed, the at-a-glance long-term goal.
            for (var m = 0; m < f.MilestoneTier && m < 5; m++)
                _ui.Diamond(b, new Rectangle(card.Right - 18 - m * 30, card.Y + 30, 11, 11), Gold);
            // The facility's own picture, on a resource-tinted hex plate. Eight identical hexagons
            // distinguished only by colour told the player nothing about what each place does.
            var plate = new Rectangle(card.Center.X - 44, card.Y + 46, 88, 76);
            _ui.Hex(b, plate, rc * (sel ? 0.55f : 0.38f));
            var icon = $"icon_facility_{f.Kind.ToString().ToLowerInvariant()}";
            if (!_ui.Icon(b, icon, new Rectangle(plate.X + 2, plate.Y - 4, plate.Width - 4, plate.Height + 8)))
                _ui.Hex(b, plate, rc);
            _ui.TextCenterBig(b, $"LEVEL {f.Level}", card.Center.X, card.Bottom - 70, Gold, UiTypography.Body);
            _ui.Diamond(b, new Rectangle(card.Center.X - 62, card.Bottom - 38, 18, 18), rc);
            _ui.TextCenterBig(b, $"+{Ab(f.BaseOutputPerMin)} /min", card.Center.X + 8, card.Bottom - 38, Bone, UiTypography.Secondary);
        }
    }

    private void DrawBonuses(SpriteBatch b)
    {
        _ui.Panel(b, BonusStrip);
        _ui.TextCenterBig(b, "WARREN BONUSES", BonusStrip.Center.X, BonusStrip.Y + 16, Gold, UiTypography.PanelTitle);

        var entries = new (Color Gem, string Value, string Label)[]
        {
            (GleamC, $"+{Warren.ResourceBonus(WarrenResource.Gleam) * 100f:0}%", "GOLD"),
            (MasteryC, $"+{Warren.ResourceBonus(WarrenResource.Mastery) * 100f:0}%", "MASTERY"),
            (DustC, $"+{Warren.ResourceBonus(WarrenResource.Dust) * 100f:0}%", "NATURE"),
            (Met, $"+{Warren.AllProductionBonus * 100f:0}%", "ALL PROD"),
            (Ember, $"+{Warren.ConquestBonus * 100f:0}%", "CONQUEST"),
            (Gold, $"LV {Warren.Level}", "WARREN"),
        };
        var slot = (BonusStrip.Width - 48) / entries.Length;
        for (var i = 0; i < entries.Length; i++)
        {
            var (gem, value, label) = entries[i];
            var cx = BonusStrip.X + 24 + slot * i + slot / 2;
            _ui.Diamond(b, new Rectangle(cx - 74, BonusStrip.Y + 72, 40, 40), gem);
            _ui.TextBig(b, value, cx - 24, BonusStrip.Y + 68, Bone, UiTypography.PanelTitle);
            _ui.TextCenterBig(b, label, cx, BonusStrip.Y + 116, Slate, UiTypography.Secondary);
        }
    }

    private void DrawDetail(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.Panel(b, DetailPanel);
        var f = Warren.Facility(_selected);
        var rc = ResColor(f.Info.Produces);

        _ui.TextCenterBig(b, f.Info.Name, DetailPanel.Center.X, DetailPanel.Y + 22, Gold, UiTypography.SectionTitle);
        _ui.TextCenterBig(b, $"LEVEL {f.Level}", DetailPanel.Center.X, DetailPanel.Y + 58, Slate, UiTypography.Body);
        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 96, DetailPanel.Width - 56, 2), Dim);

        DrawWrapped(b, f.Info.Description, DetailPanel.X + 28, DetailPanel.Y + 112, DetailPanel.Width - 56, Bone);

        // Output: current -> next (the NEXT figure includes any milestone jump the upgrade crosses).
        _ui.TextBig(b, "OUTPUT", DetailPanel.X + 28, DetailPanel.Y + 190, Slate, UiTypography.Body);
        _ui.Diamond(b, new Rectangle(DetailPanel.X + 30, DetailPanel.Y + 226, 30, 30), rc);
        _ui.TextBig(b, $"+{Ab(f.BaseOutputPerMin)} /min", DetailPanel.X + 72, DetailPanel.Y + 226, Bone, UiTypography.PanelTitle);
        _ui.TextRightBig(b, $"NEXT  +{Ab(f.NextLevelOutput)} /min", DetailPanel.Right - 28, DetailPanel.Y + 230, Met, UiTypography.Secondary);

        // Milestones — the long-term goal. Show the crossed multiplier and where the next one lands.
        var mile = new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 274, DetailPanel.Width - 56, 44);
        _ui.Fill(b, mile, new Color(0x16, 0x12, 0x20, 0xC0));
        _ui.TextBig(b, f.MilestoneTier > 0 ? $"MILESTONES  ×{f.MilestoneMultiplier:0.00} OUTPUT" : "MILESTONES  —", mile.X + 12, mile.Y + 12, Gold, UiTypography.Secondary);
        _ui.TextRightBig(b, $"NEXT AT Lv{f.NextMilestoneLevel}", mile.Right - 12, mile.Y + 12, Slate, UiTypography.Secondary);

        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 336, DetailPanel.Width - 56, 2), Dim);
        _ui.TextBig(b, "UPGRADE REQUIREMENTS", DetailPanel.X + 28, DetailPanel.Y + 352, Slate, UiTypography.Body);

        var cost = f.UpgradeCost();
        var y = DetailPanel.Y + 388;
        DrawReq(b, y, GleamC, "GOLD", GleamOwned, cost.Gleam);
        DrawReq(b, y + 42, MasteryC, "MASTERY", MasteryOwned, cost.Mastery);
        DrawReq(b, y + 84, DustC, "NATURE", DustOwned, cost.Dust);

        var afford = GleamOwned >= cost.Gleam && MasteryOwned >= cost.Mastery && DustOwned >= cost.Dust;
        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 502, DetailPanel.Width - 56, 2), Dim);
        _ui.TextBig(b, "INSTANT UPGRADE  ·  NO WAIT", DetailPanel.X + 28, DetailPanel.Y + 518, Met, UiTypography.Secondary);

        if (_ui.Button(b, new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 96, DetailPanel.Width - 80, 72), "UPGRADE", hit, clicked, enabled: afford))
            _upgradeRequest = _selected;
    }

    private void DrawReq(SpriteBatch b, int y, Color gem, string label, long owned, int required)
    {
        var ok = owned >= required;
        _ui.Diamond(b, new Rectangle(DetailPanel.X + 30, y + 2, 26, 26), gem);
        _ui.TextBig(b, label, DetailPanel.X + 68, y, Bone, UiTypography.Body);
        // The verdict is a fixed 46px column; the ratio ends where that column starts. Previously both were
        // right-aligned 28px apart, so "131.9M / 7.8M" ran straight through the "OK" beside it.
        _ui.TextRightBig(b, $"{Ab(owned)} / {Ab(required)}", DetailPanel.Right - 90, y, ok ? Met : Ember, UiTypography.Secondary);
        _ui.TextRightBig(b, ok ? "OK" : "X", DetailPanel.Right - 44, y, ok ? Met : Ember, UiTypography.Secondary);
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
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.MeasureBig(probe, px) > width && line.Length > 0)
            {
                _ui.TextBig(b, line, x, y, c, px);
                y += px * 3 / 2;
                line = w;
            }
            else line = probe;
        }
        if (line.Length > 0) _ui.TextBig(b, line, x, y, c, px);
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { OverviewPanel, GridPanel, BonusStrip, DetailPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav WARREN  facilities  sel {_selected}", 446, 118, Gold, UiTypography.Secondary);
    }
}
