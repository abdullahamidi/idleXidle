using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Loot;
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
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
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
    public int HunterPower { get; set; }
    public int ConquerWaves { get; set; } = 7;
    public string Message { get; set; } = "";
    public bool DevMapDebug { get; set; }

    // ── Host-consumed requests ──────────────────────────────────────────────────────────────────
    private string? _enterRequest;
    private bool _deepenRequest;
    private bool _easeRequest;
    public string? ConsumeEnter() { var r = _enterRequest; _enterRequest = null; return r; }
    public bool ConsumeDeepen() { var r = _deepenRequest; _deepenRequest = false; return r; }
    public bool ConsumeEase() { var r = _easeRequest; _easeRequest = false; return r; }

    private int _selected;

    // ── Spec §4 rectangles ──────────────────────────────────────────────────────────────────────
    // THE CAMPAIGN PROGRESS COLUMN IS GONE and the chart takes its width.
    //
    // Playtest: "hem solda mapler var hem ekranın ortasında resimli gösterimi var. Soldaki kısım
    // gereksiz." Correct — it was a second, worse copy of the map: six rows naming the same six regions,
    // in the same order, with the same states, beside a chart that shows all of it in one look. Its two
    // unique facts (YOUR POWER, and the conquered count) move to where they are actually compared.
    //
    // 34..1376 matches the detail column's 34px right margin, and 880 tall reaches authored 1026 —
    // aspects 1.525 and 0.539, so both panels stay on the frame art they already wear.
    private static readonly Rectangle MapCanvas = new(34, 146, 1342, 880);
    private static readonly Rectangle DetailPanel = new(1412, 146, 474, 880);

    // Six region nodes in a serpentine across the canvas: 0-1-2 along the top, 3-4-5 back along the bottom.
    private static readonly (float Fx, float Fy)[] NodeFrac =
    {
        (0.20f, 0.28f), (0.50f, 0.24f), (0.80f, 0.28f), (0.80f, 0.72f), (0.50f, 0.76f), (0.20f, 0.72f),
    };

    private static Rectangle Node(int i)
    {
        var cx = MapCanvas.X + (int)(MapCanvas.Width * NodeFrac[i].Fx);
        var cy = MapCanvas.Y + (int)(MapCanvas.Height * NodeFrac[i].Fy);
        // 196x146, up from 156x116 — the chart is 39% wider now and a node that did not grow with it
        // would read as a small tile floating in a large frame.
        return new Rectangle(cx - 98, cy - 73, 196, 146);
    }

    private static int RegionCount => Math.Min(Regions.All.Count, NodeFrac.Length);
    private static RegionDefinition Def(int i) => Regions.All[i];

    /// <summary>A real, monotonic "recommended power" derived from the region's boss power tier.</summary>
    private static int RegionPower(RegionDefinition def) => Regions.RecommendedPower(def);


    /// <summary>The shipped padlock, falling back to the hand-drawn one if the texture is missing.</summary>
    /// <remarks>
    /// <see cref="DrawLock"/> builds a padlock out of two rectangles and an arc because no art was known
    /// to exist; ui_slot_locked ships and is used by the skill dock. Two padlocks in one game is one
    /// padlock too many, and the drawn one is the poorer of the two at this size.
    /// </remarks>
    private void DrawLockArt(SpriteBatch b, Rectangle box)
    {
        if (_ui.Assets.Get("ui_slot_locked") is { } art) b.Draw(art, box, new Color(0xB0, 0xA8, 0xC0));
        else DrawLock(b, box, Slate);
    }

    /// <summary>The glyph for a gear slot. Literal arms, so check_asset_keys can see every key.</summary>
    private static string? SlotGlyph(ItemBaseType t) => t switch
    {
        ItemBaseType.Weapon => "item_slot_weapon",
        ItemBaseType.Helm => "item_slot_helm",
        ItemBaseType.Chest => "item_slot_chest",
        ItemBaseType.Gloves => "item_slot_gloves",
        ItemBaseType.Boots => "item_slot_boots",
        ItemBaseType.Charm => "item_slot_charm",
        ItemBaseType.AbilityFocus => "item_slot_focus",
        ItemBaseType.Ring => "item_slot_ring",
        _ => null,
    };

    /// <summary>The arena texture for a Source. Literal arms, so check_asset_keys can see every key.</summary>
    /// <remarks>
    /// Interpolating the key (<c>$"bg_arena_{theme}"</c>, which the host does) is invisible to the asset
    /// gate: it scans string literals, so a themed key that stops existing would fail silently as a
    /// missing background rather than loudly as a broken build.
    /// </remarks>
    private static string ArenaKey(Source theme) => theme switch
    {
        Source.Body => "bg_arena_body",
        Source.Machine => "bg_arena_machine",
        Source.Mind => "bg_arena_mind",
        Source.Nature => "bg_arena_nature",
        Source.Shadow => "bg_arena_shadow",
        _ => "bg_arena_spirit",
    };

    /// <summary>One crest per region, in one place — the node, the detail panel and the progress list share it.</summary>
    private static string? EmblemKey(string regionId) => regionId switch
    {
        "cinderworks" => "icon_region_cinderworks", "umbral_reach" => "icon_region_umbral",
        "verdant_hollow" => "icon_region_verdant", "marrow_wastes" => "icon_region_marrow_wastes",
        "still_archive" => "icon_region_still_archive", "pale_choir" => "icon_region_pale_choir",
        _ => null,
    };

    /// <summary>The largest centred part of a texture that has the destination's shape — a crop, not a squash.</summary>
    private static Rectangle CentreCrop(Texture2D t, Rectangle dst)
    {
        var want = dst.Width / MathF.Max(1f, dst.Height);
        var have = t.Width / MathF.Max(1f, t.Height);
        if (have > want)
        {
            var w = (int)(t.Height * want);
            return new Rectangle((t.Width - w) / 2, 0, Math.Max(1, w), t.Height);
        }
        var h = (int)(t.Width / want);
        return new Rectangle(0, (t.Height - h) / 2, t.Width, Math.Max(1, h));
    }

    private static string Description(Source theme) => theme switch
    {
        Source.Nature => "A verdant hollow of primal, creeping growth.",
        Source.Machine => "Grinding industry and slow, heavy iron.",
        Source.Shadow => "Realm of the forsaken — fast, creeping dark.",
        Source.Body => "A slaughterhouse of brutal, heavy blows.",
        Source.Mind => "A still archive of precise psychic lances.",
        _ => "The deepest reach — relentless and balanced.",
    };


    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked)
    {
        bool P(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);
        if (P(Keys.Left)) _selected = (_selected - 1 + RegionCount) % RegionCount;
        if (P(Keys.Right)) _selected = (_selected + 1) % RegionCount;
        if (P(Keys.Enter)) _enterRequest = Def(_selected).Id;
        if (P(Keys.D) && World.CanDeepenCorruption) _deepenRequest = true;
        if (P(Keys.S) && World.CanEaseCorruption) _easeRequest = true;
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

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xB0));
        _ui.TextCenterBig(b, "MAP", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        // THE SUBTITLE SAYS WHAT THIS SCREEN IS FOR, not what its panels are called.
        //
        // It used to recite the headers directly beneath it, which spends the largest 100px on the page
        // to tell the player something they can already read, and teaches them that big text in this
        // band is not worth reading. Same slot, same cost, real content.
        // THE NUMBER COMES FROM THE LADDER THAT SETS IT. "About half again" was written when the step was
        // 50% and the tuning has moved since; a subtitle that states a balance figure has to read that
        // figure, or it becomes a confident lie the first time someone retunes the curve.
        _ui.TextCenterBig(b, $"EACH REGION IS ABOUT {RegionLadder.StepPercent:0}% TOUGHER THAN THE LAST, AND DROPS ITS OWN THINGS",
                          960, 80, Slate, UiTypography.Secondary);

        DrawMap(b, hit, clicked);
        DrawDetail(b, hit, clicked);
        if (DevMapDebug) DrawDebug(b);
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
            // First click selects, a second click on the selected region TRAVELS — "haritayı
            // değiştiremiyorum": a tile that only ever selected, with the travel verb parked in a
            // button that disappeared at endgame, read as a map you could not use.
            if (UiKit.ClickedIn(node, hit, clicked))
            {
                if (sel && unlocked) _enterRequest = def.Id;
                _selected = i;
            }

            var sc = SourceColor[def.Theme];
            // THE REGION'S OWN GROUND, not a flat swatch. The arena art the fight already draws for this
            // theme is cropped into the node and scrimmed back, so a place on the map looks like the
            // place you land in. A locked region gets a heavier, colder scrim: it reads as somewhere you
            // can SEE but have not been, which a uniform grey rectangle cannot say.
            if (_ui.Assets.Get(ArenaKey(def.Theme)) is { } ground)
                b.Draw(ground, node, CentreCrop(ground, node), Color.White);
            _ui.Fill(b, node, unlocked ? new Color(0x0A, 0x08, 0x14, 0xB4) : new Color(0x10, 0x10, 0x16, 0xE4));
            var edge = sel ? Gold : conq ? Met : active ? Violet : unlocked ? sc : Dim;
            foreach (var e in new[] { new Rectangle(node.X, node.Y, node.Width, 3), new Rectangle(node.X, node.Bottom - 3, node.Width, 3),
                                      new Rectangle(node.X, node.Y, 3, node.Height), new Rectangle(node.Right - 3, node.Y, 3, node.Height) })
                _ui.Fill(b, e, edge);

            // Emblem — one crest per region; unknown ids still fall back to a Source gem.
            var emblem = EmblemKey(def.Id);
            var eb = new Rectangle(node.Center.X - 26, node.Y + 12, 52, 52);
            if (emblem is not null && _ui.Assets.Get(emblem) is { } em) b.Draw(em, eb, unlocked ? Color.White : new Color(0x55, 0x55, 0x60));
            else _ui.Diamond(b, eb, unlocked ? sc : Dim);

            _ui.TextCenterBig(b, def.Name, node.Center.X, node.Y + 70, unlocked ? Bone : Slate, UiTypography.Secondary);
            // BELOW the card, not across its bottom border — the power was printed straight through the
            // node's frame edge on every region.
            // Vellum, not Slate/Dim: these labels now sit on the parchment chart rather than on black, and
            // Slate (1.6:1 either way) was invisible on both.
            _ui.TextCenterBig(b, $"POWER {RegionPower(def):N0}", node.Center.X, node.Bottom + 8,
                unlocked ? UiKit.Vellum : UiKit.Vellum * 0.55f, UiTypography.Secondary);

            // State badge, top-right of the node.
            if (!unlocked) DrawLockArt(b, new Rectangle(node.Right - 32, node.Y + 8, 24, 24));
            else if (conq) DrawCheck(b, new Rectangle(node.Right - 32, node.Y + 10, 22, 18), Met);
            else if (active) _ui.Diamond(b, new Rectangle(node.Right - 30, node.Y + 8, 20, 20), Violet);

            if (sel) _ui.Fill(b, new Rectangle(node.X - 4, node.Y - 4, node.Width + 8, 4), Gold);
        }

        // BELOW the panels, on a ground of its own. At MapCanvas.Bottom - 30 it sat on the map panel's
        // bottom ornament, and the string is wider than the map — "VERDANT HOLLOW CONQUERED! CINDERWORKS
        // UNLOCKED — OPEN THE MAP (W)." ran out through the frames of the two panels either side of it,
        // with no background behind any of it. This is how the game announces that a region opened, which
        // makes it the last line in the game that should be hard to read.
        if (Message.Length > 0)
        {
            var w = Math.Min(1780, _ui.Measure(Message) + 96);
            var band = new Rectangle(960 - w / 2, MapCanvas.Bottom + 22, w, 60);
            _ui.Fill(b, new Rectangle(band.X + 3, band.Y + 3, band.Width, band.Height), new Color(0, 0, 0, 120));
            _ui.Fill(b, band, new Color(0x14, 0x10, 0x1E, 0xF0));
            _ui.Fill(b, new Rectangle(band.X, band.Y, band.Width, 3), Gold);
            _ui.Fill(b, new Rectangle(band.X, band.Bottom - 3, band.Width, 3), Gold * 0.4f);
            _ui.TextCenter(b, Message, band.Center.X, band.Y + 20, Gold);
        }
    }

    private void DrawDetail(SpriteBatch b, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, DetailPanel);
        var def = Def(_selected);
        var unlocked = World.IsUnlocked(def.Id);
        var conq = World.IsConquered(def.Id);
        var farm = World.RegionFarm(def.Id);
        var sc = SourceColor[def.Theme];

        _ui.TextCenterBig(b, def.Name, DetailPanel.Center.X, DetailPanel.Y + 20, sc, UiTypography.SectionTitle);
        _ui.TextCenterBig(b, Description(def.Theme), DetailPanel.Center.X, DetailPanel.Y + 56, Slate, UiTypography.Secondary);

        // Preview plate — a Source-tinted band with the emblem (no per-region illustration exists).
        var prev = new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 92, DetailPanel.Width - 56, 100);
        // The same ground as the node, so the panel and the map agree about where you are looking.
        if (_ui.Assets.Get(ArenaKey(def.Theme)) is { } plate)
            b.Draw(plate, prev, CentreCrop(plate, prev), Color.White);
        _ui.Fill(b, prev, new Color(0x0A, 0x08, 0x14, 0x9E));
        _ui.Fill(b, prev, sc * 0.16f);
        _ui.Fill(b, new Rectangle(prev.X, prev.Y, prev.Width, 3), sc);
        _ui.Fill(b, new Rectangle(prev.X, prev.Bottom - 3, prev.Width, 3), sc);
        if (_ui.Assets.Get($"source_{def.Theme.ToString().ToLowerInvariant()}") is { } gem)
            b.Draw(gem, new Rectangle(prev.Center.X - 40, prev.Center.Y - 40, 80, 80), unlocked ? Color.White : new Color(0x60, 0x60, 0x68));

        // BOSS POWER TIER IS GONE and everything below it moved up 38px, which is what makes the DROPS
        // section physically possible — see the note there. The row printed "T17": a raw index into an
        // internal scale, against nothing, on the one screen a player uses to pick where to go. It said
        // less than the RECOMMENDED POWER figure directly above it already does, and cost the panel the
        // room its last real section needed.
        // YOUR POWER SITS ON THE ROW ABOVE THE ONE IT IS MEASURED AGAINST. It used to live at the foot
        // of the deleted column, a thousand pixels from the number it exists to be compared with.
        _ui.TextBig(b, "YOUR POWER", DetailPanel.X + 28, DetailPanel.Y + 214, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"{HunterPower:N0}", DetailPanel.Right - 28, DetailPanel.Y + 210,
                         HunterPower >= RegionPower(def) ? Met : Ember, UiTypography.PanelTitle);
        _ui.TextBig(b, "RECOMMENDED POWER", DetailPanel.X + 28, DetailPanel.Y + 252, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"{RegionPower(def):N0}", DetailPanel.Right - 28, DetailPanel.Y + 248, Violet, UiTypography.PanelTitle);

        _ui.Fill(b, new Rectangle(DetailPanel.X + 28, DetailPanel.Y + 292, DetailPanel.Width - 56, 2), Dim);

        // Enemy theme (the region's Source) + combat modifier (its bias).
        _ui.TextBig(b, "ENEMY THEME", DetailPanel.X + 28, DetailPanel.Y + 308, Gold, UiTypography.Secondary);
        if (_ui.Assets.Get($"source_{def.Theme.ToString().ToLowerInvariant()}") is { } tg)
            b.Draw(tg, new Rectangle(DetailPanel.X + 30, DetailPanel.Y + 340, 34, 34), Color.White);
        _ui.TextBig(b, $"{def.Theme.ToString().ToUpperInvariant()} AFFINITY", DetailPanel.X + 74, DetailPanel.Y + 344, Bone, UiTypography.Body);
        // HOW THE PLACE FIGHTS, which the region model has carried since it was written and no screen has
        // ever shown. It decides whether you are hit by a few heavy blows or a fast flurry — a real
        // difference to a build, and the kind of thing you want to know BEFORE walking in.
        //
        // RIGHT-ALIGNED ON THE AFFINITY'S OWN LINE, not on a new one. Added below, it landed on the
        // region modifier's name six pixels lower — inserting a row without moving what follows it is
        // the exact fault this panel's DROPS section was already suffering from, and this panel has no
        // spare vertical space at all. The affinity label is short and the line is 418px wide.
        _ui.TextRightBig(b, def.CombatBias switch
        {
            AttackBias.Heavy => "IT HITS SLOWLY AND HARD",
            AttackBias.Fast => "IT HITS FAST AND OFTEN",
            _ => "IT HITS AT AN EVEN PACE",
        }, DetailPanel.Right - 28, DetailPanel.Y + 346, Slate, UiTypography.Secondary);
        // Region modifier — the themed combat twist (replaces the old flavor-only bias line).
        var mod = RegionModifiers.For(def.Id);
        _ui.TextBig(b, mod.Name, DetailPanel.X + 30, DetailPanel.Y + 382, Ember, UiTypography.Body);
        // WRAPPED. The blurb is a sentence now rather than "+25% HP & +10% damage", and a single
        // TextBig would have run it off the panel and through the frame.
        var modY = DetailPanel.Y + 408;
        foreach (var line in _ui.WrapBig(mod.Blurb, DetailPanel.Width - 60, UiTypography.Secondary))
        {
            _ui.TextBig(b, line, DetailPanel.X + 30, modY, Slate, UiTypography.Secondary);
            modY += 22;
        }

        // Objective + what it earns while you are away (real).
        _ui.TextBig(b, "OBJECTIVE", DetailPanel.X + 28, DetailPanel.Y + 470, Gold, UiTypography.Secondary);
        if (conq) { DrawCheck(b, new Rectangle(DetailPanel.X + 30, DetailPanel.Y + 506, 22, 18), Met); _ui.TextBig(b, "REGION CONQUERED", DetailPanel.X + 64, DetailPanel.Y + 504, Met, UiTypography.Body); }
        else _ui.TextBig(b, $"HOLD {ConquerWaves} WAVES TO CONQUER", DetailPanel.X + 30, DetailPanel.Y + 504, unlocked ? Bone : Slate, UiTypography.Body);
        if (unlocked)
            // "IDLE FARM: PARTIAL · 50%" was two pieces of jargon and a number with no unit. The percent
            // is the only part a player can act on, and it needed a sentence to say what it is a percent
            // OF. The mastery word went with the label: it named a tier nothing on screen explains, and
            // the number it produces is already right there.
            _ui.TextBig(b, $"EARNS {farm.IdleEfficiencyPercent():0}% OF ITS RATE WHILE YOU ARE AWAY",
                        DetailPanel.X + 30, DetailPanel.Y + 546, Slate, UiTypography.Secondary);

        // WHAT THIS PLACE DROPS. The map decided a difficulty and an element and said nothing about
        // reward, so "where should I farm" had no answer on the screen built to answer it. Each region
        // now leans toward its own slots, and this is where a player finds that out — before walking in,
        // not after twenty chests.
        // THIS SECTION HAD NEVER DRAWN A SINGLE LINE. Its body started at Y+606 and its floor was
        // Bottom-150 = Y+566 — forty pixels ABOVE its own first row — so the very first iteration broke
        // and every region in the game showed a "DROPS" heading with nothing under it. The heading was
        // outside the loop, so the failure looked like missing DATA rather than a layout that could not
        // fit its own content, and the comment below explains a clamp that was doing far more than it
        // claimed. The floor was right; there was simply no room, and nothing said so.
        //
        // The room came from deleting BOSS POWER TIER above. Now the heading is only drawn if a line
        // will actually follow it, so this can never silently regress to a bare label again.
        var drops = RegionDrops.For(def.Id);
        var dropLines = drops.Favoured.Count > 0
            ? _ui.WrapBig(drops.Blurb, DetailPanel.Width - 60, UiTypography.Secondary)
            : System.Array.Empty<string>();
        var dropsTop = DetailPanel.Y + 620;
        // Clamped to the row above the CTA: on a deepenable world the CORRUPTION line lands in this
        // band, and a wrap with no floor eventually meets whatever is below it.
        // Once the world is conquered the corruption ladder row starts at Bottom-196; the blurb stops above it.
        var dropsFloor = DetailPanel.Bottom - (World.AllConquered ? 204 : 108);
        if (dropLines.Count > 0 && dropsTop + 22 <= dropsFloor)
        {
            _ui.TextBig(b, "DROPS", DetailPanel.X + 28, DetailPanel.Y + 590, Gold, UiTypography.Secondary);

            // WHAT IT FAVOURS, AS PICTURES, on the heading's own line — which costs no vertical space
            // at all. The blurb below already says it in words; a player comparing two regions is
            // comparing slots, and three glyphs are read in one glance where two sentences are not.
            var gx = DetailPanel.Right - 28;
            foreach (var slot in drops.Favoured.Take(3).Reverse())
            {
                if (SlotGlyph(slot) is not { } key || _ui.Assets.Get(key) is not { } gi) continue;
                gx -= 30;
                b.Draw(gi, new Rectangle(gx, DetailPanel.Y + 584, 26, 26), Bone);
            }
            var y = dropsTop;
            foreach (var line in dropLines)
            {
                if (y + 22 > dropsFloor) break;
                _ui.TextBig(b, line, DetailPanel.X + 30, y, Bone, UiTypography.Secondary);
                y += 22;
            }
        }

        // CTA. ENTER / RESUME for an unlocked region, ALWAYS — it used to give way to DEEPEN once the
        // world was conquered, which took travel away from a mouse player for the rest of the game.
        // A locked region disables honestly.
        var cta = new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 92, DetailPanel.Width - 80, 68);
        if (_ui.Button(b, cta, def.Id == ActiveRegion ? "RESUME HERE" : "ENTER THIS REGION", hit, clicked, enabled: unlocked))
            _enterRequest = def.Id;
        if (!unlocked) _ui.TextCenter(b, "CONQUER THE PREVIOUS REGION TO UNLOCK", DetailPanel.Center.X, DetailPanel.Bottom - 110, Ember);

        // THE CORRUPTION LADDER, its own row above the CTA once the world is yours: the tier by name
        // (CorruptionLook), SHALLOWER / DEEPER either side, both honest about their ends. Five tiers,
        // not a counter — and the hunt answers each one (tinted creatures, an epithet on the boss, a
        // darker arena), which is the difference between raising a number and raising the stakes.
        if (World.AllConquered)
        {
            var look = CorruptionLook.For(World.CorruptionTier);
            _ui.TextCenterBig(b, CorruptionLook.Label(World.CorruptionTier), DetailPanel.Center.X, DetailPanel.Bottom - 196,
                              World.CorruptionTier > 0 ? Violet : Slate, UiTypography.Secondary);
            _ui.TextCenter(b, look.Blurb, DetailPanel.Center.X, DetailPanel.Bottom - 170, Slate);
            var half = (DetailPanel.Width - 80 - 12) / 2;
            var easeBtn = new Rectangle(DetailPanel.X + 40, DetailPanel.Bottom - 148, half, 44);
            var deepBtn = new Rectangle(easeBtn.Right + 12, DetailPanel.Bottom - 148, half, 44);
            if (_ui.Button(b, easeBtn, "SHALLOWER", hit, clicked, enabled: World.CanEaseCorruption)) _easeRequest = true;
            if (_ui.Button(b, deepBtn, "DEEPER", hit, clicked, enabled: World.CanDeepenCorruption)) _deepenRequest = true;
        }
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
        foreach (var r in new[] { MapCanvas, DetailPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav MAP  sel {Def(_selected).Id}  active {ActiveRegion}", 420, 112, Gold, UiTypography.Secondary);
    }
}
