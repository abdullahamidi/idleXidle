using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

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
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Met = UiInk.Good;

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
    public int ConquerWaves { get; set; } = Checkpoints.ConquestWave;
    /// <summary>The player's Memory Dust, for the checkpoint chips' affordability.</summary>
    public int DustOwned { get; set; }
    public string Message { get; set; } = "";
    public bool DevMapDebug { get; set; }

    // ── Host-consumed requests ──────────────────────────────────────────────────────────────────
    private string? _enterRequest;
    private bool _deepenRequest;
    private bool _easeRequest;
    public string? ConsumeEnter() { var r = _enterRequest; _enterRequest = null; return r; }
    private (string RegionId, int Wave)? _startRequest;
    /// <summary>A checkpoint chip was clicked: the region and the wave to start after.</summary>
    public (string RegionId, int Wave)? ConsumeStart() { var r = _startRequest; _startRequest = null; return r; }
    public bool ConsumeDeepen() { var r = _deepenRequest; _deepenRequest = false; return r; }
    public bool ConsumeEase() { var r = _easeRequest; _easeRequest = false; return r; }

    private int _selected;

    // ── THE TWO COLUMNS, BOTH EDGES FOLLOWING THE PAGE (UX V2 P2.1). ─────────────────────────────
    //
    // The campaign progress column is gone and the chart has its width: it was a second, worse copy of
    // the map — six rows naming the same six regions, in the same order, with the same states, beside a
    // chart that shows all of it in one look.
    //
    // Top 150 clears the hint slot's band (canvas y 86..134), which this screen uses: MAP is one of the
    // eight screens Onboarding.HintFor speaks on ("A NEW REGION IS AVAILABLE — CINDERWORKS").
    private const int Top = 150;
    private const int BottomMargin = 54;
    private const int Gutter = 20;
    private const int InspectorMaxW = 496;
    private static int ColumnH => UiKit.PageBottom(BottomMargin) - Top;

    /// <summary>
    /// The inspector's width — capped so the panel keeps its VERTICAL frame art at every UI SCALE.
    /// </summary>
    /// <remarks>
    /// <see cref="UiKit.PanelArtKey"/> chooses by aspect, and a fixed 496 against a short page crosses the
    /// 0.82 line into the SQUARE frame, whose side rails reach 57 px in — the fault that ate GEAR's
    /// inspector label in P1.7. Tying the width to the height keeps the ratio on the right side of it.
    /// </remarks>
    private static int InspectorW => Math.Min(InspectorMaxW, ColumnH * 80 / 100);
    private static Rectangle DetailPanel => new(UiKit.PageRight(40) - InspectorW, Top, InspectorW, ColumnH);
    private static Rectangle MapCanvas => new(34, Top, DetailPanel.X - Gutter - 34, ColumnH);

    /// <summary>The world's own row, inside the chart's top edge: the corruption ladder, or the last news.</summary>
    private const int StripTop = 56, StripH = 52;
    private static Rectangle WorldStrip => new(UiKit.PanelInner(MapCanvas).X + 16, MapCanvas.Y + StripTop,
                                               UiKit.PanelInner(MapCanvas).Width - 32, StripH);

    /// <summary>Has the world anything to say right now? The strip costs nothing when it has not.</summary>
    /// <remarks>
    /// A conditional block must not reserve a hole — that is the fault this pass took out of the inspector,
    /// and reserving one here would put it straight back at the top of the chart.
    /// </remarks>
    private bool HasWorldStrip => World is not null && (World.AllConquered || Message.Length > 0);

    /// <summary>The band the six nodes and their label rows live in — under the world strip, above the frame.</summary>
    private Rectangle NodeField
    {
        get
        {
            var inner = UiKit.PanelInner(MapCanvas);
            var top = HasWorldStrip ? WorldStrip.Bottom + 18 : inner.Y + 12;
            return new Rectangle(MapCanvas.X, top, MapCanvas.Width, inner.Bottom - 18 - top);
        }
    }

    // A CARD BIG ENOUGH TO READ. At 196×146 the name, the element and the state band were all at
    // Secondary — a five-pixel cap at 720p. The card is sized from the field it sits in, so it grows
    // with the chart and its three lines can sit on Body.
    private int NodeH => Math.Clamp(NodeField.Height * 26 / 100, 116, 176);
    private int NodeW => NodeH * 240 / 176;

    // Six region nodes in a serpentine across the canvas: 0-1-2 along the top, 3-4-5 back along the bottom.
    private static readonly (float Fx, float Fy)[] NodeFrac =
    {
        (0.20f, 0.24f), (0.50f, 0.20f), (0.80f, 0.24f), (0.80f, 0.72f), (0.50f, 0.76f), (0.20f, 0.72f),
    };

    private Rectangle Node(int i)
    {
        var f = NodeField;
        var cx = f.X + (int)(f.Width * NodeFrac[i].Fx);
        var cy = f.Y + (int)(f.Height * NodeFrac[i].Fy);
        return new Rectangle(cx - NodeW / 2, cy - NodeH / 2, NodeW, NodeH);
    }

    private static int RegionCount => Math.Min(Regions.All.Count, NodeFrac.Length);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>The chain's light is the union of every region node, so the serpentine reads as one thing.</remarks>
    internal Rectangle[] Spotlights(TourTarget target)
    {
        switch (target)
        {
            case TourTarget.RegionChain:
                var chain = Node(0);
                for (var i = 1; i < RegionCount; i++) chain = Rectangle.Union(chain, Node(i));
                chain.Inflate(12, 12);
                chain.Height += 24 + UiTypography.Pitch(UiTypography.Body);   // the POWER and requirement rows under a node are part of it
                return new[] { chain };
            case TourTarget.RegionDetail:
                return new[] { DetailPanel };
            case TourTarget.EnterRegion:
                return new[] { new Rectangle(UiKit.ContentLeft(DetailPanel), DetailPanel.Bottom - 92,
                                 DetailPanel.Width - UiKit.PadX(DetailPanel) * 2, 68) };
            default:
                return Array.Empty<Rectangle>();
        }
    }
    private static RegionDefinition Def(int i) => Regions.All[i];

    /// <summary>A real, monotonic "recommended power" derived from the region's boss power tier.</summary>
    /// <summary>
    /// START AT WAVE — the checkpoint chips of a conquered region (Checkpoints): 0 and every ten waves
    /// the champion has held here. A chip is the wave the descent starts AFTER, priced in Memory Dust
    /// per descent; the chosen one is gold, an unaffordable one is dim and says so.
    /// </summary>
    private void DrawCheckpoints(SpriteBatch b, RegionDefinition def, Region farm, Point hit, bool clicked,
                                 ref int y, int x, int width, int floor)
    {
        var options = new List<int>(Checkpoints.Options(farm.BestDepth, conquered: true));
        // The row holds so many chips. Past that the SHALLOW middle goes (TOP and the deepest ones stay —
        // a player at wave 110 wants 100 and 110, not 10), never the deepest (review 2026-08-26).
        int ChipW(int o) => Math.Max(44, _ui.MeasureBig(o == 0 ? "TOP" : o.ToString(), UiTypography.Secondary) + UiTypography.ChipPadX * 2);
        int RowWidth() { var t = 0; foreach (var o in options) t += ChipW(o) + 4; return t; }
        while (options.Count > 2 && RowWidth() > width) options.RemoveAt(1);

        // ONE OPTION IS NOT A CHOICE. A region conquered at wave 1 offers only the top, and a header over a
        // single inert chip is furniture — the block appears when there is somewhere else to start.
        if (options.Count < 2) return;
        // 30 px chips: Secondary plus the chip padding is 27, and the six pixels saved are what let the
        // consequence line under them fit on a deep region.
        const int ChipH = 30;
        if (y + UiTypography.Pitch(UiTypography.Secondary) + ChipH > floor) return;

        // THE COST RIDES THE HEADER'S OWN ROW. On its own line it was the first thing the flow dropped on a
        // deep region — and it is the half that says what pressing a chip will charge you.
        var chosen = Checkpoints.Clamp(farm.StartWave, farm.BestDepth, true);
        var cost = Checkpoints.DustCost(chosen);
        var affordChosen = DustOwned >= cost;
        _ui.TextBig(b, "START AT WAVE", x, y, Slate, UiTypography.Secondary);
        // WHEN IT CANNOT BE PAID, THE ROW SAYS WHAT WILL ACTUALLY HAPPEN. The host quietly starts the run at
        // the top when the chosen checkpoint is unaffordable, and the map never said so — the player watched
        // a gold chip and landed somewhere else. The price gives up its row to the consequence, because a
        // player who cannot pay needs to know where they will wake up more than what it would have cost.
        _ui.TextRightBig(b, chosen == 0 ? "FREE"
                            : affordChosen ? $"{cost:N0} MEMORY DUST EACH TIME"
                            : $"NEED {cost:N0} DUST — STARTS AT THE TOP",
                         x + width, y, affordChosen ? Slate : Ember, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        var cx = x;
        foreach (var o in options)
        {
            var label = o == 0 ? "TOP" : o.ToString();
            var chip = new Rectangle(cx, y, ChipW(o), ChipH);
            var afford = DustOwned >= Checkpoints.DustCost(o);
            var lit = o == chosen;
            _ui.Fill(b, chip, lit ? new Color(0x3A, 0x2C, 0x14, 0xE0) : new Color(0x14, 0x10, 0x1A, 0xE0));
            var edge = lit ? Gold : chip.Contains(hit) ? Bone : Dim;
            _ui.Fill(b, new Rectangle(chip.X, chip.Y, chip.Width, 2), edge);
            _ui.Fill(b, new Rectangle(chip.X, chip.Bottom - 2, chip.Width, 2), edge);
            _ui.Fill(b, new Rectangle(chip.X, chip.Y, 2, chip.Height), edge);
            _ui.Fill(b, new Rectangle(chip.Right - 2, chip.Y, 2, chip.Height), edge);
            _ui.TextCenterBig(b, label, chip.Center.X, chip.Y + UiTypography.ChipPadY + 2,
                              lit ? Gold : afford ? Bone : UiInk.Disabled, UiTypography.Secondary);
            if (UiKit.ClickedIn(chip, hit, clicked)) _startRequest = (def.Id, o);
            cx += chip.Width + 4;
        }
        y += ChipH + 4;
    }

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

    /// <summary>The Source gem for an element. Literal arms, so check_asset_keys can see every key.</summary>
    private static string SourceGemKey(Source theme) => theme switch
    {
        Source.Body => "source_body",
        Source.Machine => "source_machine",
        Source.Mind => "source_mind",
        Source.Nature => "source_nature",
        Source.Shadow => "source_shadow",
        _ => "source_spirit",
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
        Source.Nature => "A wild green hollow of creeping growth.",
        Source.Machine => "Grinding industry and slow, heavy iron.",
        Source.Shadow => "Home of the forgotten — fast, creeping dark.",
        Source.Body => "A slaughterhouse of brutal, heavy blows.",
        Source.Mind => "A still archive of precise mind-strikes.",
        _ => "The deepest reach — steady, never resting.",
    };


    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked)
    {
        bool P(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);
        if (P(Keys.Left)) _selected = (_selected - 1 + RegionCount) % RegionCount;
        if (P(Keys.Right)) _selected = (_selected + 1) % RegionCount;
        // A locked region has no CTA any more, so Enter must not be the one path that can still ask for it.
        if (P(Keys.Enter) && World.IsUnlocked(Def(_selected).Id)) _enterRequest = Def(_selected).Id;
        if (P(Keys.D) && World.CanDeepenCorruption) _deepenRequest = true;
        if (P(Keys.S) && World.CanEaseCorruption) _easeRequest = true;
    }

    /// <summary>DEV: point the inspector at a region, so a capture can photograph it read (RH_SHOT fixtures).</summary>
    public void DevSelect(string regionId)
    {
        var i = Regions.All.ToList().FindIndex(r => r.Id == regionId);
        if (i >= 0 && i < RegionCount) _selected = i;
    }

    /// <summary>Point the selection at the active region when the screen opens (host calls once on entry).</summary>
    public void SelectActive()
    {
        var idx = Regions.All.ToList().FindIndex(r => r.Id == ActiveRegion);
        if (idx >= 0) _selected = idx;
    }

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = mouse;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xB0));
        _ui.TextCenterBig(b, "MAP", UiKit.PageCenterX, 24, UiInk.Accent, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, 74, 480, 3), Gold * 0.5f);
        // NO SUBTITLE. The band under the title is the hint slot's (D4): when a region opens, the screen
        // says so there, about this player's world, instead of reciting a balance figure on every visit.

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
            // THE FRAME SAYS WHERE YOU ARE. Gold, and a pixel thicker, on the region you are in; a pale
            // frame on the one you have selected; the conquest green and the Source colour otherwise.
            // The active region used to wear an unexplained purple diamond in its corner ("Verdant
            // Hollow has a purple icon") — the frame and the band below now say it in words.
            // Hover = highlight (D5): a card the pointer is on lifts and takes the pale edge, so the chart
            // answers the pointer before a click is spent.
            var hot = node.Contains(hit);
            if (hot) _ui.Fill(b, node, new Color(0xE8, 0xDF, 0xC8, 0x14));
            var edge = active ? Gold : sel || hot ? Bone : conq ? Met : unlocked ? sc : Dim;
            var thick = active ? 4 : 3;
            foreach (var e in new[] { new Rectangle(node.X, node.Y, node.Width, thick), new Rectangle(node.X, node.Bottom - thick, node.Width, thick),
                                      new Rectangle(node.X, node.Y, thick, node.Height), new Rectangle(node.Right - thick, node.Y, thick, node.Height) })
                _ui.Fill(b, e, edge);
            if (active)
                foreach (var e in new[] { new Rectangle(node.X - 3, node.Y - 3, node.Width + 6, 2), new Rectangle(node.X - 3, node.Bottom + 1, node.Width + 6, 2),
                                          new Rectangle(node.X - 3, node.Y - 3, 2, node.Height + 6), new Rectangle(node.Right + 1, node.Y - 3, 2, node.Height + 6) })
                    _ui.Fill(b, e, Gold * 0.45f);

            // Emblem — one crest per region; unknown ids still fall back to a Source gem.
            var emblem = EmblemKey(def.Id);
            var eb = new Rectangle(node.Center.X - 28, node.Y + 10, 56, 56);
            if (emblem is not null && _ui.Assets.Get(emblem) is { } em) b.Draw(em, eb, unlocked ? Color.White : new Color(0x55, 0x55, 0x60));
            else _ui.Diamond(b, eb, unlocked ? sc : Dim);

            // A LOCKED REGION KEEPS ITS WORDS. Its name, its element and its power used to be greyed, so
            // the one card a player most needs to read about — the place they cannot go yet — was the
            // hardest to read. The padlock and the band say "locked"; the letters do not have to.
            _ui.TextCenterBig(b, _ui.ShortenBig(def.Name, node.Width - 16, UiTypography.Body),
                              node.Center.X, node.Y + 72, Bone, UiTypography.Body);

            // THE ELEMENT, READABLE: the Source gem with the element's name beside it, in the Source's
            // colour, centred as one group. What the creatures there are made of is the first thing a
            // build cares about, and it was only ever implied by the card's ground art.
            var element = def.Theme.ToString().ToUpperInvariant();
            var ew = _ui.MeasureBig(element, UiTypography.Body);
            var ex = node.Center.X - (28 + 8 + ew) / 2;
            var gemBox = new Rectangle(ex, node.Y + 102, 28, 28);
            if (_ui.Assets.Get(SourceGemKey(def.Theme)) is { } gem) b.Draw(gem, gemBox, Color.White);
            else _ui.Diamond(b, gemBox, sc);
            _ui.TextBig(b, element, gemBox.Right + 8, node.Y + 105, sc, UiTypography.Body);

            // BELOW the card, not across its bottom border. Vellum, because these labels sit on the
            // parchment chart rather than on black.
            _ui.TextCenterBig(b, $"POWER {RegionPower(def):N0}", node.Center.X, node.Bottom + 8,
                              UiKit.Vellum, UiTypography.Body);

            // AND, UNDER A LOCKED ONE, WHAT OPENS IT. The prerequisite's name was one lookup away and
            // appeared nowhere; "locked" without "locked by what" is a dead end on the screen whose whole
            // job is deciding where to go next. Guarded so it never lands on the chart's bottom frame.
            if (!unlocked && def.PrereqId is { } pid && Regions.Find(pid) is { } pdef
                && node.Bottom + 8 + UiTypography.Pitch(UiTypography.Body) + UiTypography.Pitch(UiTypography.Secondary)
                   <= UiKit.PanelInner(MapCanvas).Bottom)
                _ui.TextCenterBig(b, $"CONQUER {pdef.Name}", node.Center.X,
                                  node.Bottom + 8 + UiTypography.Pitch(UiTypography.Body),
                                  Bone, UiTypography.Secondary);

            // THE STATE, IN WORDS, on a band along the card's foot — with the fourth state the chart never
            // had: a region that is open, not conquered and not where you are said nothing at all.
            var band = new Rectangle(node.X + thick, node.Bottom - 32 - thick, node.Width - thick * 2, 32);
            if (active)
            {
                _ui.Fill(b, band, new Color(0x2A, 0x1E, 0x08, 0xE6));
                _ui.TextCenterBig(b, "YOU ARE HERE", node.Center.X, band.Y + 5, Gold, UiTypography.Body);
                if (conq) DrawCheck(b, new Rectangle(node.Right - 30, node.Y + 10, 22, 18), Met);
            }
            else if (conq)
            {
                _ui.Fill(b, band, new Color(0x08, 0x14, 0x0C, 0xE6));
                var cw = _ui.MeasureBig("CONQUERED", UiTypography.Body);
                var cx = node.Center.X - (22 + 8 + cw) / 2;
                DrawCheck(b, new Rectangle(cx, band.Y + 6, 22, 18), Met);
                _ui.TextBig(b, "CONQUERED", cx + 30, band.Y + 5, Met, UiTypography.Body);
            }
            else if (unlocked)
            {
                // AVAILABLE, not gold: it is neither earned nor where you are (D9).
                _ui.Fill(b, band, new Color(0x14, 0x11, 0x1E, 0xE6));
                _ui.TextCenterBig(b, "AVAILABLE", node.Center.X, band.Y + 5, Bone, UiTypography.Body);
            }
            else
            {
                _ui.Fill(b, band, new Color(0x10, 0x10, 0x16, 0xE6));
                var lw = _ui.MeasureBig("LOCKED", UiTypography.Body);
                var lx = node.Center.X - (18 + 8 + lw) / 2;
                DrawLockArt(b, new Rectangle(lx, band.Y + 6, 18, 18));
                _ui.TextBig(b, "LOCKED", lx + 26, band.Y + 5, Bone, UiTypography.Body);
            }

            if (sel) _ui.Fill(b, new Rectangle(node.X - 4, node.Y - 4, node.Width + 8, 4), Gold);
        }

        DrawWorldStrip(b, hit, clicked);
    }

    /// <summary>
    /// THE WORLD'S OWN ROW, inside the chart's top edge: the corruption ladder once every region is
    /// yours, and otherwise the last thing the world did.
    /// </summary>
    /// <remarks>
    /// Both used to live in the inspector, whose CATEGORY line says REGION — but corruption is a setting
    /// on the WORLD, not a fact about the region you have selected, and it put a second and third button
    /// on a panel that is allowed one (standard law 2). The conquest message was worse off still: it was
    /// drawn under the panels, in the canvas's dead band, wider than the chart.
    /// </remarks>
    private void DrawWorldStrip(SpriteBatch b, Point hit, bool clicked)
    {
        var r = WorldStrip;
        if (World.AllConquered)
        {
            _ui.Plate(b, r, Gold);
            var label = CorruptionLook.Label(World.CorruptionTier);
            _ui.TextBig(b, label, r.X + 20, r.Y + 14, World.CorruptionTier > 0 ? Gold : Bone, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig(CorruptionLook.For(World.CorruptionTier).Blurb, r.Width - 460, UiTypography.Secondary),
                        r.X + 20 + _ui.MeasureBig(label, UiTypography.Body) + 24, r.Y + 15, Slate, UiTypography.Secondary);
            var deep = new Rectangle(r.Right - 8 - 168, r.Y + 6, 168, 40);
            var ease = new Rectangle(deep.X - 12 - 168, r.Y + 6, 168, 40);
            if (_ui.Button(b, ease, "SHALLOWER", hit, clicked, World.CanEaseCorruption)) _easeRequest = true;
            if (_ui.Button(b, deep, "DEEPER", hit, clicked, World.CanDeepenCorruption)) _deepenRequest = true;
        }
        else if (Message.Length > 0)
        {
            _ui.Plate(b, r, Gold);
            _ui.TextCenterBig(b, _ui.ShortenBig(Message, r.Width - 48, UiTypography.Body), r.Center.X, r.Y + 14, Gold, UiTypography.Body);
        }
    }

    // (the old message band drew here, under the panels, in the canvas's dead band)
    /// <summary>
    /// THE INSPECTOR (D3): what this region is, what you will fight, what it drops, where you stand, and
    /// the one thing you can do about it.
    /// </summary>
    /// <remarks>
    /// A FLOW, not a table of fixed offsets. Every block used to be positioned by an absolute
    /// <c>DetailPanel.Y + n</c> with two conditional sections reserved as holes — about a hundred and sixty
    /// pixels held empty for a checkpoint row and a corruption ladder that are usually not there, while
    /// the text above them ran a rung too small for want of room. Blocks are drawn in order now and the
    /// ones that do not apply cost nothing.
    /// </remarks>
    private void DrawDetail(SpriteBatch b, Point hit, bool clicked)
    {
        var panel = DetailPanel;
        _ui.PanelQuiet(b, panel);
        var def = Def(_selected);
        var unlocked = World.IsUnlocked(def.Id);
        var conq = World.IsConquered(def.Id);
        var farm = World.RegionFarm(def.Id);
        var sc = SourceColor[def.Theme];

        var x = UiKit.ContentLeft(panel);
        var w = UiKit.ContentRight(panel) - x;
        var y = panel.Y + UiTypography.PanelTitleTop;
        var cta = new Rectangle(x, panel.Bottom - 92, w, 68);
        // 16, not 40. The flow ran five pixels short of the START AT WAVE block on a conquered region held
        // deep — so the one control the panel offers besides the button was correctly suppressed, for want
        // of air nobody had asked for. The button keeps a clear line above it either way.
        var floor = cta.Y - 16;

        void Section(string s)
        {
            if (y + UiTypography.Pitch(UiTypography.Secondary) > floor) return;
            _ui.TextBig(b, s, x, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
        }
        void Line(string s, Color c, int px = 0, int maxLines = 3)
        {
            if (px == 0) px = UiTypography.Body;   // a rung is a profile-scaled property, not a constant
            foreach (var l in _ui.WrapBig(s, w, px).Take(maxLines))
            {
                if (y + UiTypography.Pitch(px) > floor) return;
                _ui.TextBig(b, l, x, y, c, px);
                y += UiTypography.Pitch(px);
            }
        }
        void Rule() { if (y + 14 < floor) { _ui.Fill(b, new Rectangle(x, y + 6, w, 1), Dim); y += 16; } }
        void Pair(string label, string figure, Color figureInk)
        {
            if (y + UiTypography.Pitch(UiTypography.Headline) > floor) return;
            _ui.TextBig(b, label, x, y + 6, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, figure, x + w, y, figureInk, UiTypography.Headline);
            y += UiTypography.Pitch(UiTypography.Headline);
        }

        // ── CATEGORY · NAME · IDENTITY ──
        Section($"REGION {_selected + 1} OF {RegionCount}");
        _ui.TextBig(b, _ui.ShortenBig(def.Name, w, UiTypography.Headline), x, y,
                    def.Id == ActiveRegion ? Gold : Bone, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + 6;
        // The identity plate: the region's own ground, its gem, its element. The Source gem used to be
        // drawn four times per region on this screen; it appears twice now — once on the node, once here.
        var plate = new Rectangle(x, y, w, 84);
        _ui.Plate(b, plate, sc);
        if (_ui.Assets.Get(ArenaKey(def.Theme)) is { } ground) b.Draw(ground, plate, CentreCrop(ground, plate), Color.White);
        _ui.Fill(b, plate, new Color(0x0A, 0x08, 0x14, 0x9E));
        _ui.Fill(b, plate, sc * 0.16f);
        if (_ui.Assets.Get(SourceGemKey(def.Theme)) is { } pgem)
            b.Draw(pgem, new Rectangle(plate.X + 18, plate.Y + 14, 56, 56), Color.White);
        _ui.TextBig(b, def.Theme.ToString().ToUpperInvariant(), plate.X + 90, plate.Y + 28, sc, UiTypography.Headline);
        y += 84 + 12;
        Line(Description(def.Theme), Bone, UiTypography.Body, 2);
        Rule();

        // ── CAN I SURVIVE IT — the two figures, and the gap between them IN WORDS. ──
        //
        // The verdict used to be carried by the colour of one number alone (green or red), which §8
        // forbids. There is no difficulty word here on purpose: nothing in Core maps a power gap to
        // EASY/FAIR/DEADLY, and inventing one would be the screen telling the player something the game
        // does not know. Subtracting two figures that are already on screen is the honest version.
        var need = RegionPower(def);
        Pair("YOUR POWER", $"{HunterPower:N0}", Bone);
        Pair("RECOMMENDED", $"{need:N0}", Bone);
        Line(HunterPower > need ? $"{HunterPower - need:N0} ABOVE THE RECOMMENDED POWER"
             : HunterPower == need ? "AT THE RECOMMENDED POWER"
             : $"{need - HunterPower:N0} BELOW THE RECOMMENDED POWER",
             HunterPower >= need ? Met : Ember, UiTypography.Body, 2);
        Rule();

        // ── WHAT YOU WILL FIGHT. How the place fights has been in the region model since it was written
        //    and no screen ever showed it; the modifier's sentence is the most decision-relevant line on
        //    the panel and was set at footnote size. ──
        Section("WHAT YOU WILL FIGHT");
        Line(def.CombatBias switch
        {
            AttackBias.Heavy => "HEAVY — SLOW, HARD HITS",
            AttackBias.Fast => "FAST — QUICK, LIGHT HITS",
            _ => "EVEN — A STEADY PACE",
        }, Bone, UiTypography.Body, 1);
        var mod = RegionModifiers.For(def.Id);
        Line(mod.Name, sc, UiTypography.Body, 1);
        Line(mod.Blurb, Bone, UiTypography.Body, 2);
        Rule();

        // ── WHAT IT DROPS. The region's theme becomes the chest's element (Chests.RollDrop), and each
        //    region leans toward its own slots — which is the answer to "where should I farm". ──
        var drops = RegionDrops.For(def.Id);
        if (drops.Favoured.Count > 0)
        {
            Section("WHAT IT DROPS");
            Line($"{def.Theme.ToString().ToUpperInvariant()} ITEMS", sc, UiTypography.Body, 1);
            foreach (var slot in drops.Favoured)
            {
                if (y + UiTypography.Pitch(UiTypography.Body) > floor) break;
                if (SlotGlyph(slot) is { } key && _ui.Assets.Get(key) is { } gi)
                    b.Draw(gi, new Rectangle(x, y + 1, 24, 24), Bone);
                _ui.TextBig(b, RegionDrops.PlainName(slot), x + 34, y, Bone, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
            }
            Rule();
        }

        // ── YOU NEED FIRST (a locked region), or CURRENT STATE. ──
        if (!unlocked)
        {
            Section("YOU NEED FIRST");
            if (def.PrereqId is { } pid && Regions.Find(pid) is { } pdef)
            {
                DrawLockArt(b, new Rectangle(x, y + 2, 20, 20));
                _ui.TextBig(b, $"CONQUER {pdef.Name}", x + 30, y, Bone, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
                Line($"{pdef.Name} — BEST WAVE {World.RegionFarm(pdef.Id).BestDepth} OF {ConquerWaves}", Slate, UiTypography.Body, 1);
            }
            else Line("CONQUER THE REGION BEFORE THIS ONE.", Bone, UiTypography.Body, 1);
        }
        else
        {
            Section("CURRENT STATE");
            if (conq)
            {
                DrawCheck(b, new Rectangle(x, y + 2, 22, 18), Met);
                _ui.TextBig(b, "CONQUERED", x + 32, y, Met, UiTypography.Body);
                y += UiTypography.Pitch(UiTypography.Body);
                Line($"BEST WAVE {farm.BestDepth}", Bone, UiTypography.Body, 1);
                DrawCheckpoints(b, def, farm, hit, clicked, ref y, x, w, floor);
            }
            else
            {
                Line($"BEST WAVE {farm.BestDepth} OF {ConquerWaves}", Bone, UiTypography.Body, 1);
                // The rule is Game1's `Deepest >= ConquerWaveDepth`, and the guide already says "REACH
                // WAVE 20" — "HOLD 20 WAVES" was the same rule in a second voice.
                Line($"REACH WAVE {ConquerWaves} TO CONQUER THIS REGION", Slate, UiTypography.Body, 2);
            }
        }

        // ── THE ONE THING YOU CAN DO. A locked region gets the requirement on the quiet tier rather than
        //    a disabled mystery button (§29). ──
        if (unlocked)
        {
            if (_ui.Button(b, cta, def.Id == ActiveRegion ? "RESUME HERE" : "HUNT HERE", hit, clicked, true, ButtonStyle.Primary))
                _enterRequest = def.Id;
        }
        else
        {
            _ui.Plate(b, cta);
            var pname = def.PrereqId is { } p2 && Regions.Find(p2) is { } pd2 ? pd2.Name : "";
            var say = pname.Length > 0 ? $"CONQUER {pname} FIRST" : "CONQUER THE REGION BEFORE THIS ONE FIRST";
            DrawLockArt(b, new Rectangle(cta.X + 20, cta.Center.Y - 11, 22, 22));
            _ui.TextCenterBig(b, _ui.ShortenBig(say, cta.Width - 80, UiTypography.NavigationLabel),
                              cta.Center.X + 11, cta.Center.Y - 13, Bone, UiTypography.NavigationLabel);
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
