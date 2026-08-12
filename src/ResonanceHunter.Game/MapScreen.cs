using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Progression;

namespace ResonanceHunter.Client;

/// <summary>
/// The MAP screen (nav: MAP): a region-selection dashboard built to the Map production spec (rev 1) —
/// a campaign-progress column, the region map canvas, and a selected-region detail panel.
/// </summary>
/// <remarks>
/// Every value is real, from the <see cref="World"/> and <see cref="Regions"/> model the host owns: the six
/// regions of the conquer-chain, their unlocked / conquered / active states, per-region mastery and idle
/// efficiency, the boss power tier, the combat bias, and the corruption tier. The host sets the model each
/// frame and consumes the ENTER / DEEPEN requests. Invented reference bits (Acts, 21 regions, per-region
/// modifier %s, a stage-depth slider, a notable-drops catalog) are replaced or dropped per the UX standard.
/// </remarks>
public sealed class MapScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    private static readonly Color Violet = new(0xC8, 0x8A, 0xE0);

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x8C, 0x74, 0xC8), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };

    private readonly UiKit _ui;
    public MapScreen(UiKit ui) => _ui = ui;

    // ── Host-set each frame ───────────────────────────────────────────────────────────────────────
    public World World { get; set; } = null!;
    public string ActiveRegion { get; set; } = "";
    public int DeepestWave { get; set; }
    public int HunterPower { get; set; }
    public int ConquerWaves { get; set; } = 7;
    public string Message { get; set; } = "";
    public bool DevMapDebug { get; set; }

    // ── Host-consumed requests ──────────────────────────────────────────────────────────────────
    private string? _enterRequest;
    private bool _deepenRequest;
    public string? ConsumeEnter() { var r = _enterRequest; _enterRequest = null; return r; }
    public bool ConsumeDeepen() { var r = _deepenRequest; _deepenRequest = false; return r; }

    private int _selected;

    // ── Spec §4 rectangles ──────────────────────────────────────────────────────────────────────
    private static readonly Rectangle ProgressPanel = new(34, 146, 356, 716);
    private static readonly Rectangle MapCanvas = new(420, 146, 964, 716);
    private static readonly Rectangle DetailPanel = new(1412, 146, 474, 716);

    // Six region nodes in a serpentine across the canvas: 0-1-2 along the top, 3-4-5 back along the bottom.
    private static readonly (float Fx, float Fy)[] NodeFrac =
    {
        (0.20f, 0.28f), (0.50f, 0.24f), (0.80f, 0.28f), (0.80f, 0.72f), (0.50f, 0.76f), (0.20f, 0.72f),
    };

    private static Rectangle Node(int i)
    {
        var cx = MapCanvas.X + (int)(MapCanvas.Width * NodeFrac[i].Fx);
        var cy = MapCanvas.Y + (int)(MapCanvas.Height * NodeFrac[i].Fy);
        return new Rectangle(cx - 78, cy - 58, 156, 116);
    }

    private static int RegionCount => Math.Min(Regions.All.Count, NodeFrac.Length);
    private static RegionDefinition Def(int i) => Regions.All[i];

    /// <summary>A real, monotonic "recommended power" derived from the region's boss power tier.</summary>
    private static int RegionPower(RegionDefinition def) => def.Boss.PowerTierBase * 350;

    private static string Description(Source theme) => theme switch
    {
        Source.Nature => "A verdant hollow of primal, creeping growth.",
        Source.Machine => "Grinding industry and slow, heavy iron.",
        Source.Shadow => "Realm of the forsaken — fast, creeping dark.",
        Source.Body => "A slaughterhouse of brutal, heavy blows.",
        Source.Mind => "A still archive of precise psychic lances.",
        _ => "The deepest reach — relentless and balanced.",
    };

    private static readonly string[] MasteryShort = { "NEWLY", "PARTIAL", "FULLY", "OPTIMIZED" };

    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked)
    {
        bool P(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);
        if (P(Keys.Left)) _selected = (_selected - 1 + RegionCount) % RegionCount;
        if (P(Keys.Right)) _selected = (_selected + 1) % RegionCount;
        if (P(Keys.Enter)) _enterRequest = Def(_selected).Id;
        if (P(Keys.D) && World.CanDeepenCorruption) _deepenRequest = true;
    }

    /// <summary>Point the selection at the active region when the screen opens (host calls once on entry).</summary>
    public void SelectActive()
    {
        var idx = Regions.All.ToList().FindIndex(r => r.Id == ActiveRegion);
        if (idx >= 0) _selected = idx;
    }

    public void Draw(SpriteBatch b) => Draw(b, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xB0));
        _ui.TextCenterBig(b, "MAP", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        _ui.TextCenterBig(b, "REGIONS  ·  DEPTH  ·  PROGRESSION", 960, 80, Slate, UiTypography.Secondary);

        DrawProgress(b, hit, clicked);
        DrawMap(b, hit, clicked);
        DrawDetail(b, hit, clicked);
        if (DevMapDebug) DrawDebug(b);
    }

    private void DrawProgress(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.Panel(b, ProgressPanel);
        _ui.TextCenterBig(b, "CAMPAIGN PROGRESS", ProgressPanel.Center.X, ProgressPanel.Y + 18, Gold, UiTypography.SectionTitle);

        var conquered = Regions.All.Count(r => World.IsConquered(r.Id));
        var total = Regions.All.Count;
        var stage = World.CorruptionTier > 0 ? $"CORRUPTION TIER {World.CorruptionTier}" : "THE CONQUEST";
        _ui.Diamond(b, new Rectangle(ProgressPanel.Center.X - 28, ProgressPanel.Y + 50, 56, 64), Violet);
        _ui.TextCenterBig(b, stage, ProgressPanel.Center.X, ProgressPanel.Y + 122, Bone, UiTypography.PanelTitle);

        var bar = new Rectangle(ProgressPanel.X + 32, ProgressPanel.Y + 158, ProgressPanel.Width - 64, 20);
        _ui.Bar(b, bar.X, bar.Y, bar.Width, bar.Height, total > 0 ? conquered / (float)total : 0f, Violet);
        _ui.TextCenterBig(b, $"{conquered} / {total} REGIONS CONQUERED", bar.Center.X, bar.Y + 2, Bone, UiTypography.Secondary);

        _ui.Fill(b, new Rectangle(ProgressPanel.X + 24, ProgressPanel.Y + 192, ProgressPanel.Width - 48, 2), Dim);
        _ui.TextBig(b, "DISCOVERED REGIONS", ProgressPanel.X + 30, ProgressPanel.Y + 206, Gold, UiTypography.Body);

        var y = ProgressPanel.Y + 240;
        for (var i = 0; i < RegionCount; i++)
        {
            var def = Def(i);
            var unlocked = World.IsUnlocked(def.Id);
            var conq = World.IsConquered(def.Id);
            var active = def.Id == ActiveRegion;
            var row = new Rectangle(ProgressPanel.X + 24, y, ProgressPanel.Width - 48, 50);
            if (UiKit.ClickedIn(row, hit, clicked)) _selected = i;

            _ui.Fill(b, row, i == _selected ? new Color(0x3A, 0x2E, 0x52) : new Color(0x16, 0x12, 0x20, 0xC0));
            _ui.Fill(b, new Rectangle(row.X, row.Y, 4, row.Height), unlocked ? SourceColor[def.Theme] : Dim);

            _ui.TextBig(b, $"{i + 1}. {def.Name}", row.X + 16, row.Y + 5, unlocked ? Bone : Slate, UiTypography.Secondary);
            var (label, col) = conq ? ("CONQUERED", Met) : active ? ("IN PROGRESS", Violet) : !unlocked ? ("LOCKED", Slate) : ("AVAILABLE", Bone);
            _ui.TextBig(b, label, row.X + 16, row.Y + 28, col, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{RegionPower(def):N0}", row.Right - 34, row.Y + 28, Slate, UiTypography.Secondary);
            if (!unlocked) DrawLock(b, new Rectangle(row.Right - 24, row.Y + 6, 18, 20), Slate);
            else if (conq) DrawCheck(b, new Rectangle(row.Right - 26, row.Y + 8, 20, 16), Met);
            else if (active) _ui.Diamond(b, new Rectangle(row.Right - 24, row.Y + 6, 18, 18), Violet);
            y += 56;
        }

        _ui.TextBig(b, "RECOMMENDED POWER", ProgressPanel.X + 30, ProgressPanel.Bottom - 62, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"{RegionPower(Def(_selected)):N0}", ProgressPanel.Right - 30, ProgressPanel.Bottom - 66, Violet, UiTypography.PanelTitle);
    }

    private void DrawMap(SpriteBatch b, Point hit, bool clicked)
    {
        // The illustrated map backdrop, cropped to fill the canvas (AspectFillCrop), then a dark scrim so nodes read.
        // bg_mapfield, not bg_regionmap: the latter is the cartographer's-chamber art already covering the
        // whole SCREEN behind these panels, so reusing it here under a scrim made the map panel a black
        // void with cards floating in it. This is a chart, which is what the nodes want to sit on.
        // FRAME FIRST, then the chart inside it. The panel art's centre slice is opaque near-black, so
        // drawing the backdrop first and the panel over it painted the map out entirely — which is why
        // this canvas has always been an empty black rectangle.
        _ui.Panel(b, MapCanvas);
        var field = UiKit.PanelInner(MapCanvas);
        if (_ui.Assets.Get("bg_mapfield") is { } bg)
        {
            // Aspect-fill by cropping the SOURCE, not by overdrawing the destination. Scaling the whole
            // texture up and centring it meant the overflow was painted outside the canvas: at 1280x720
            // the backdrop bled 154px to each side and covered half the campaign panel next to it.
            var srcW = Math.Min(bg.Width, (int)(bg.Height * (field.Width / (float)field.Height)));
            var srcH = Math.Min(bg.Height, (int)(bg.Width * (field.Height / (float)field.Width)));
            b.Draw(bg, field,
                new Rectangle((bg.Width - srcW) / 2, (bg.Height - srcH) / 2, srcW, srcH), Color.White);
        }
        else _ui.Fill(b, field, new Color(0x12, 0x0E, 0x1C));
        _ui.Fill(b, field, new Color(0x08, 0x06, 0x12, 0x66));   // scrim, so the node cards still read

        // Connection lines along the conquer-chain (gold once the source region is conquered).
        for (var i = 0; i < RegionCount - 1; i++)
        {
            var a = Node(i).Center;
            var c = Node(i + 1).Center;
            // Vellum, not Dim: these connectors sit on the parchment chart now, and Dim (1.6:1 on both
            // surfaces) was the colour that failed everywhere else on this screen too.
            var col = World.IsConquered(Def(i).Id) ? Gold * 0.9f : UiKit.Vellum * 0.5f;
            DrawDottedLine(b, a, c, col);
        }

        for (var i = 0; i < RegionCount; i++)
        {
            var def = Def(i);
            var node = Node(i);
            var unlocked = World.IsUnlocked(def.Id);
            var conq = World.IsConquered(def.Id);
            var active = def.Id == ActiveRegion;
            var sel = i == _selected;
            if (UiKit.ClickedIn(node, hit, clicked)) _selected = i;

            var sc = SourceColor[def.Theme];
            _ui.Fill(b, node, unlocked ? new Color(0x1A, 0x14, 0x28, 0xF0) : new Color(0x12, 0x10, 0x16, 0xE0));
            var edge = sel ? Gold : conq ? Met : active ? Violet : unlocked ? sc : Dim;
            foreach (var e in new[] { new Rectangle(node.X, node.Y, node.Width, 3), new Rectangle(node.X, node.Bottom - 3, node.Width, 3),
                                      new Rectangle(node.X, node.Y, 3, node.Height), new Rectangle(node.Right - 3, node.Y, 3, node.Height) })
                _ui.Fill(b, e, edge);

            // Emblem — one crest per region; unknown ids still fall back to a Source gem.
            var emblem = def.Id switch
            {
                "cinderworks" => "icon_region_cinderworks", "umbral_reach" => "icon_region_umbral",
                "verdant_hollow" => "icon_region_verdant", "marrow_wastes" => "icon_region_marrow_wastes",
                "still_archive" => "icon_region_still_archive", "pale_choir" => "icon_region_pale_choir",
                _ => (string?)null,
            };
            var eb = new Rectangle(node.Center.X - 26, node.Y + 12, 52, 52);
            if (emblem is not null && _ui.Assets.Get(emblem) is { } em) b.Draw(em, eb, unlocked ? Color.White : new Color(0x55, 0x55, 0x60));
            else _ui.Diamond(b, eb, unlocked ? sc : Dim);

            _ui.TextCenterBig(b, def.Name, node.Center.X, node.Y + 70, unlocked ? Bone : Slate, UiTypography.Secondary);
            // BELOW the card, not across its bottom border — the power was printed straight through the
            // node's frame edge on every region.
            // Vellum, not Slate/Dim: these labels now sit on the parchment chart rather than on black, and
            // Slate (1.6:1 either way) was invisible on both.
            _ui.TextCenterBig(b, $"PWR {RegionPower(def):N0}", node.Center.X, node.Bottom + 8,
                unlocked ? UiKit.Vellum : UiKit.Vellum * 0.55f, UiTypography.Secondary);

            // State badge, top-right of the node.
            if (!unlocked) DrawLock(b, new Rectangle(node.Right - 30, node.Y + 8, 20, 22), Slate);
            else if (conq) DrawCheck(b, new Rectangle(node.Right - 32, node.Y + 10, 22, 18), Met);
            else if (active) _ui.Diamond(b, new Rectangle(node.Right - 30, node.Y + 8, 20, 20), Violet);

            if (sel) _ui.Fill(b, new Rectangle(node.X - 4, node.Y - 4, node.Width + 8, 4), Gold);
        }

        if (Message.Length > 0) _ui.TextCenter(b, Message, MapCanvas.Center.X, MapCanvas.Bottom - 30, Gold);
    }

    private void DrawDetail(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.Panel(b, DetailPanel);
        var def = Def(_selected);
        var unlocked = World.IsUnlocked(def.Id);
        var conq = World.IsConquered(def.Id);
        var farm = World.RegionFarm(def.Id);
        var sc = SourceColor[def.Theme];

        _ui.TextCenterBig(b, def.Name, DetailPanel.Center.X, DetailPanel.Y + 20, sc, UiTypography.SectionTitle);
        _ui.TextCenterBig(b, Description(def.Theme), DetailPanel.Center.X, DetailPanel.Y + 56, Slate, UiTypography.Secondary);

        // Preview plate — a Source-tinted band with the emblem (no per-region illustration exists).
        var prev = new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 92, DetailPanel.Width - 56, 150);
        _ui.Fill(b, prev, sc * 0.28f);
        _ui.Fill(b, new Rectangle(prev.X, prev.Y, prev.Width, 3), sc);
        _ui.Fill(b, new Rectangle(prev.X, prev.Bottom - 3, prev.Width, 3), sc);
        if (_ui.Assets.Get($"source_{def.Theme.ToString().ToLowerInvariant()}") is { } gem)
            b.Draw(gem, new Rectangle(prev.Center.X - 40, prev.Center.Y - 40, 80, 80), unlocked ? Color.White : new Color(0x60, 0x60, 0x68));

        // Recommended power + boss tier.
        _ui.TextBig(b, "RECOMMENDED POWER", DetailPanel.X + 28, DetailPanel.Y + 262, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"{RegionPower(def):N0}", DetailPanel.Right - 28, DetailPanel.Y + 258, Violet, UiTypography.PanelTitle);
        _ui.TextBig(b, "BOSS POWER TIER", DetailPanel.X + 28, DetailPanel.Y + 300, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"T{def.Boss.PowerTierBase}", DetailPanel.Right - 28, DetailPanel.Y + 298, Bone, UiTypography.Body);

        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 338, DetailPanel.Width - 56, 2), Dim);

        // Enemy theme (the region's Source) + combat modifier (its bias).
        _ui.TextBig(b, "ENEMY THEME", DetailPanel.X + 28, DetailPanel.Y + 352, Gold, UiTypography.Secondary);
        if (_ui.Assets.Get($"source_{def.Theme.ToString().ToLowerInvariant()}") is { } tg)
            b.Draw(tg, new Rectangle(DetailPanel.X + 30, DetailPanel.Y + 384, 34, 34), Color.White);
        _ui.TextBig(b, $"{def.Theme.ToString().ToUpperInvariant()} AFFINITY", DetailPanel.X + 74, DetailPanel.Y + 388, Bone, UiTypography.Body);
        // Region modifier — the themed combat twist (replaces the old flavor-only bias line).
        var mod = RegionModifiers.For(def.Id);
        _ui.TextBig(b, mod.Name, DetailPanel.X + 30, DetailPanel.Y + 420, Ember, UiTypography.Body);
        _ui.TextBig(b, mod.Blurb, DetailPanel.X + 30, DetailPanel.Y + 444, Slate, UiTypography.Secondary);

        // Objective + idle-farm status (real).
        _ui.TextBig(b, "OBJECTIVE", DetailPanel.X + 28, DetailPanel.Y + 470, Gold, UiTypography.Secondary);
        if (conq) { DrawCheck(b, new Rectangle(DetailPanel.X + 30, DetailPanel.Y + 504, 22, 18), Met); _ui.TextBig(b, "REGION CONQUERED", DetailPanel.X + 64, DetailPanel.Y + 502, Met, UiTypography.Body); }
        else _ui.TextBig(b, $"HOLD {ConquerWaves} WAVES TO CONQUER", DetailPanel.X + 30, DetailPanel.Y + 502, unlocked ? Bone : Slate, UiTypography.Body);
        if (unlocked)
            _ui.TextBig(b, $"IDLE FARM: {MasteryShort[(int)farm.MasteryLevel]} · {farm.IdleEfficiencyPercent():0}%", DetailPanel.X + 30, DetailPanel.Y + 540, Slate, UiTypography.Secondary);

        // CTA. ENTER for an unlocked region; a locked one disables honestly. DEEPEN once the world is yours.
        var cta = new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 92, DetailPanel.Width - 80, 68);
        if (World.CanDeepenCorruption)
        {
            _ui.TextCenter(b, $"CORRUPTION {World.CorruptionTier} -> {World.CorruptionTier + 1}", DetailPanel.Center.X, DetailPanel.Bottom - 116, Violet);
            if (_ui.Button(b, cta, "DEEPEN", hit, clicked)) _deepenRequest = true;
        }
        else if (_ui.Button(b, cta, def.Id == ActiveRegion ? "RE-ENTER" : "ENTER REGION", hit, clicked, enabled: unlocked))
            _enterRequest = def.Id;
        if (!unlocked) _ui.TextCenter(b, "CONQUER THE PREVIOUS REGION TO UNLOCK", DetailPanel.Center.X, DetailPanel.Bottom - 110, Ember);
    }

    // ── Small drawn glyphs (no dedicated lock/check assets) ────────────────────────────────────
    private void DrawLock(SpriteBatch b, Rectangle r, Color c)
    {
        var body = new Rectangle(r.X, r.Y + r.Height / 2, r.Width, r.Height / 2);
        _ui.Fill(b, body, c);
        var shTop = r.Y + r.Height / 6;
        _ui.Fill(b, new Rectangle(r.X + 3, shTop, 3, r.Height / 2 - 2), c);
        _ui.Fill(b, new Rectangle(r.Right - 6, shTop, 3, r.Height / 2 - 2), c);
        _ui.Fill(b, new Rectangle(r.X + 3, shTop, r.Width - 6, 3), c);
    }

    private void DrawCheck(SpriteBatch b, Rectangle r, Color c)
    {
        for (var k = 0; k < 6; k++) _ui.Fill(b, new Rectangle(r.X + k, r.Bottom - 6 + k / 2 - 2, 3, 3), c);
        for (var k = 0; k < 11; k++) _ui.Fill(b, new Rectangle(r.X + 6 + k, r.Bottom - 2 - k, 3, 3), c);
    }

    private void DrawDottedLine(SpriteBatch b, Point a, Point c, Color col)
    {
        var dx = c.X - a.X;
        var dy = c.Y - a.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        var steps = Math.Max(1, (int)(len / 26f));
        for (var s = 0; s <= steps; s++)
        {
            var t = s / (float)steps;
            _ui.Fill(b, new Rectangle((int)(a.X + dx * t) - 3, (int)(a.Y + dy * t) - 3, 7, 7), col);
        }
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { ProgressPanel, MapCanvas, DetailPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav MAP  sel {Def(_selected).Id}  active {ActiveRegion}", 420, 112, Gold, UiTypography.Secondary);
    }
}
