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

    // ── THE REVEAL (UI polish §73, §30): a region that has JUST become available pulses once. ─────
    //
    // The screen remembers which regions it has already shown as available this session. A region
    // that turns up unlocked and is not in that set was conquered open since the last visit, and gets
    // ONE Reward-length pulse (UiMotion.Flash) the first time it is seen — then never again. No save
    // change: a new session seeds the set silently from the world as it stands, because nothing "just"
    // happened on a game's first frame (§30: animate CHANGE). Armed from Update, keyed to the region,
    // never from Draw — so a redraw cannot re-fire it.
    private readonly HashSet<string> _shownAvailable = new();
    private bool _revealSeeded;
    private World? _memoOf;      // the world the set was gathered from — a NEW GAME builds a new one

    /// <summary>The pulse key for a region's reveal — public so a test can read <see cref="UiMotion.Pulse"/> of it.</summary>
    public static int RevealKey(string regionId) => HashCode.Combine("map.reveal", regionId);

    /// <summary>
    /// RIG: <c>RH_SHOT_REVEAL=&lt;regionId&gt;</c> treats that region as newly available on the screen's
    /// first frame and HOLDS its pulse at the peak for the shot — the rig shoots frame 60, a Reward pulse
    /// is over by frame 21, and a state no capture can pose has never been looked at. Read once, under the
    /// capture rig only, the way <c>RH_SHOT_SCROLL</c> is. The re-arm each frame is the freeze, not the
    /// feature: outside the rig a pulse is armed once, by the event.
    /// </summary>
    private readonly string? _devRevealHold =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null ? Environment.GetEnvironmentVariable("RH_SHOT_REVEAL") : null;

    /// <summary>
    /// RIG: <c>RH_SHOT_HOLD=1</c> reads the posed cursor as a HELD mouse button, so PRESSED can be
    /// photographed. <see cref="UiKit.MouseHeld"/> comes from the real device and the capture rig has no
    /// device, so pressed is otherwise the one state of §25 that no screenshot can pose. Pair it with
    /// <c>RH_SHOT_PAGE_MOUSE=x,y</c> over the card or chip whose held face is wanted.
    /// </summary>
    private static readonly bool DevHold =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
        && Environment.GetEnvironmentVariable("RH_SHOT_HOLD") is { Length: > 0 };

    /// <summary>The mouse is down over this screen: the host's real button, or the rig's posed hold.</summary>
    private static bool Held => UiKit.MouseHeld || DevHold;

    // ── THE CUE (UI polish §86–§87): the host owns audio; the screen names the moment. ───────────
    //
    // sfx_click      a region card picked (click or arrow key)
    // sfx_nav        leaving for the hunt — HUNT HERE / RESUME HERE, a second click on the card, Enter
    // sfx_error      asked to go somewhere locked — a second click on a locked card, Enter on one
    // sfx_reveal_tick a region's reveal pulse fired
    private string? _cue;

    /// <summary>The sound cue for what just happened here, returned once; null when nothing did.</summary>
    public string? ConsumeCue() { var c = _cue; _cue = null; return c; }

    /// <summary>The locked card's hover explanation, gathered in DrawMap and drawn last so it sits over everything.</summary>
    private (string Text, Point At)? _lockTip;

    // ── THE INSPECTOR'S SCROLL (UI polish §17–§18). ──────────────────────────────────────────────
    //
    // The inspector is a flow of blocks under a fixed header, and at 125 % and 150 % the flow is longer
    // than the column: the same eleven blocks that fit 1080 px at Body 22 do not at Body 33, and the
    // brief forbids the one fix that used to be reached for (a smaller font). The flow scrolls by WHOLE
    // ITEMS — a line, a plate, a rule, a chip row — so nothing is ever half-drawn and no clip is needed,
    // which is how every row list in the game already scrolls. The one lit button stays anchored under
    // the region (§18: never a primary action below a scroll).
    private int _first;                                  // the first flow item the region shows
    private int _shown;                                  // how many it held last frame — the page size
    private readonly List<int> _itemHeights = new();     // the measured flow, reused each frame

    /// <summary>
    /// RIG: <c>RH_SHOT_SCROLL=&lt;items&gt;</c> poses the flow scrolled by that many items on its first
    /// frame (a large number lands on the last page), so a state only the scroll reaches — the checkpoint
    /// chips at 150 %, YOU NEED FIRST under the fold — is photographed rather than described. Read once,
    /// under the capture rig only, the way FORGE and TRAINING read their own dials.
    /// </summary>
    private int _devScrollPending =
        Environment.GetEnvironmentVariable("RH_SHOT") is not null
        && Environment.GetEnvironmentVariable("RH_SHOT_SCROLL") is { } s && int.TryParse(s, out var n) ? n : 0;

    /// <summary>Point the inspector at a region. A new region's flow starts at its top.</summary>
    private void Select(int i)
    {
        if (i != _selected) _first = 0;
        _selected = i;
    }

    // ── THE TWO COLUMNS, BOTH EDGES FOLLOWING THE PAGE (UX V2 P2.1). ─────────────────────────────
    //
    // The campaign progress column is gone and the chart has its width: it was a second, worse copy of
    // the map — six rows naming the same six regions, in the same order, with the same states, beside a
    // chart that shows all of it in one look.
    //
    // Top 150 clears the hint slot's band (canvas y 86..134), which this screen uses: MAP is one of the
    // eight screens Onboarding.HintFor speaks on ("A NEW REGION IS AVAILABLE — CINDERWORKS"). Top and the
    // bottom margin are PAGE anchors — the chrome above and below does not grow with the profile.
    private const int Top = 150;
    private const int BottomMargin = 54;
    private const int TitleY = 24;
    private static int Gutter => UiMetrics.Space(20);
    private static int ColumnH => UiKit.PageBottom(BottomMargin) - Top;

    /// <summary>
    /// The inspector's width — the house inspector width for the profile, capped so the panel keeps its
    /// VERTICAL frame art at every one of them.
    /// </summary>
    /// <remarks>
    /// <see cref="UiKit.PanelArtKey"/> chooses by aspect, and a fixed 496 against a short page crosses the
    /// 0.82 line into the SQUARE frame, whose side rails reach 57 px in — the fault that ate GEAR's
    /// inspector label in P1.7. Tying the width to the height keeps the ratio on the right side of it;
    /// at 150 % it is this cap, not the profile, that decides (744 wanted, 700 allowed).
    /// </remarks>
    private static int InspectorW => Math.Min(UiMetrics.InspectorWidth(UiKit.Page.Width), ColumnH * 80 / 100);
    private static Rectangle DetailPanel => new(UiKit.PageRight(40) - InspectorW, Top, InspectorW, ColumnH);
    private static Rectangle MapCanvas => new(34, Top, DetailPanel.X - Gutter - 34, ColumnH);

    /// <summary>The world's own row, inside the chart's top edge: the corruption ladder, or the last news.</summary>
    /// <remarks>Tall enough for the small button it carries, with a breath above and below it.</remarks>
    private static int StripTop => UiKit.PanelCorner + UiMetrics.Space(16);
    private static int StripH => UiMetrics.Space(6) * 2 + UiMetrics.ButtonHeightSmall;
    private static Rectangle WorldStrip
    {
        get
        {
            var inner = UiKit.PanelInner(MapCanvas);
            var inset = UiMetrics.Space(16);
            return new Rectangle(inner.X + inset, MapCanvas.Y + StripTop, inner.Width - inset * 2, StripH);
        }
    }

    /// <summary>The panel's one lit button: anchored above the frame's foot, never inside the scroll.</summary>
    private static Rectangle CtaRect(Rectangle panel)
    {
        var x = UiKit.ContentLeft(panel);
        var h = UiMetrics.ButtonHeightPrimary;
        return new Rectangle(x, panel.Bottom - UiMetrics.Space(24) - h, UiKit.ContentRight(panel) - x, h);
    }

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
            var top = HasWorldStrip ? WorldStrip.Bottom + UiMetrics.Space(18) : inner.Y + UiMetrics.Space(12);
            return new Rectangle(MapCanvas.X, top, MapCanvas.Width, inner.Bottom - UiMetrics.Space(18) - top);
        }
    }

    // ── THE CARD'S OWN GRID. ─────────────────────────────────────────────────────────────────────
    //
    // A CARD BIG ENOUGH TO READ. At 196×146 the name, the element and the state band were all at
    // Secondary — a five-pixel cap at 720p. The card is sized from the field it sits in, so it grows
    // with the chart and its three lines can sit on Body — and, since the density profile, it is never
    // shorter than the stack it carries, so a bigger emblem and a bigger Body push the band down rather
    // than printing over it. Read top to bottom, at 100 %:
    //
    //      10  EmblemTop      the crest's drop from the card's top
    //      56  EmblemSize     the crest
    //       6                 … to the name
    //      28  Pitch(Body)    the name's line
    //       2                 … to the element row
    //      28  GemSize        the Source gem, the element's name beside it
    //      11                 … to the state band
    //      32  BandH          the band, its word centred
    //       3  FrameThick     the card's own edge
    //     ───
    //     176                 the card at 100 %, exactly what it was
    private static int EmblemTop => UiMetrics.Space(10);
    private static int EmblemSize => UiMetrics.Control(56);
    private static int NameTop => EmblemTop + EmblemSize + UiMetrics.Space(6);
    private static int GemSize => UiMetrics.Control(28);
    private static int GemTop => NameTop + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(2);
    private static int BandH => UiMetrics.Control(32);
    private const int FrameThick = 3;   // the card's edge — a rule, not a control; the active card wears one more
    private static int CardStack => GemTop + GemSize + UiMetrics.Space(11) + BandH + FrameThick;

    /// <summary>The check glyph's box beside a word — the same shape on the card and in the inspector.</summary>
    private static int CheckW => UiMetrics.Control(22);
    private static int CheckH => UiMetrics.Control(18);

    /// <summary>The row under a card: a breath, then POWER on one Body line.</summary>
    private static int LabelGap => UiMetrics.Space(8);
    private static int PowerRowH => LabelGap + UiTypography.Pitch(UiTypography.Body);

    private int NodeH => Math.Max(CardStack, Math.Clamp(NodeField.Height * 26 / 100, UiMetrics.Control(116), UiMetrics.Control(176)));
    // NEVER WIDER THAN ITS COLUMN. Three cards share the field's width at 0.20 / 0.50 / 0.80, so a card
    // past 28 % of it would touch its neighbour; at 100 % the aspect wins by a wide margin (240 of 372),
    // at 150 % the column does, and the card comes out a little squarer rather than overlapping.
    private int NodeW => Math.Min(NodeH * 240 / 176, NodeField.Width * 28 / 100);

    // Six region nodes in a serpentine across the canvas: 0-1-2 along the top, 3-4-5 back along the bottom.
    private static readonly (float Fx, float Fy)[] NodeFrac =
    {
        (0.20f, 0.24f), (0.50f, 0.20f), (0.80f, 0.24f), (0.80f, 0.72f), (0.50f, 0.76f), (0.20f, 0.72f),
    };

    private Rectangle Node(int i)
    {
        var f = NodeField;
        var half = NodeH / 2;
        var cx = f.X + (int)(f.Width * NodeFrac[i].Fx);
        int cy;
        if (NodeFrac[i].Fy < 0.5f) cy = TopRowY(f, i, half);
        else
        {
            // FROM THE HEIGHT THAT IS THERE. The fractions place the rows on a tall field; on a short one
            // (150 % cards, or a world strip over them) the bottom row is held up so its POWER line AND its
            // CONQUER … line clear the frame's foot — the two bottom cards land at one height then, which
            // is a reflow, not a fault (§9). But never so far up that it takes the top row's own POWER
            // line: when the field cannot hold both, the requirement is the line that goes (its guard in
            // DrawMap drops it), and the frame's foot stays the one edge a card never crosses.
            var cyRaw = f.Y + (int)(f.Height * NodeFrac[i].Fy);
            var withRequirement = f.Bottom - PowerRowH - UiTypography.Pitch(UiTypography.Secondary) - half;
            var underTopRow = TopRowBottom(f, half) + PowerRowH + UiMetrics.Space(4) + half;
            cy = Math.Max(Math.Min(cyRaw, withRequirement), underTopRow);
            cy = Math.Min(cy, f.Bottom - PowerRowH - half);
        }
        return new Rectangle(cx - NodeW / 2, cy - half, NodeW, NodeH);
    }

    /// <summary>A top-row card's centre: its fraction of the field, held under the field's top on a short one.</summary>
    private static int TopRowY(Rectangle f, int i, int half)
        => Math.Max(f.Y + (int)(f.Height * NodeFrac[i].Fy), f.Y + UiMetrics.Space(4) + half);

    /// <summary>The top row's lowest edge — what the bottom row must stay under, with the POWER line between.</summary>
    private static int TopRowBottom(Rectangle f, int half)
    {
        var bottom = f.Y;
        for (var j = 0; j < RegionCount; j++)
            if (NodeFrac[j].Fy < 0.5f) bottom = Math.Max(bottom, TopRowY(f, j, half) + half);
        return bottom;
    }

    /// <summary>The bottom-row card under a top-row one — the thing a label under it must not reach.</summary>
    private Rectangle? Below(int i)
    {
        if (NodeFrac[i].Fy >= 0.5f) return null;
        for (var j = 0; j < RegionCount; j++)
            if (NodeFrac[j].Fy >= 0.5f && MathF.Abs(NodeFrac[j].Fx - NodeFrac[i].Fx) < 0.01f) return Node(j);
        return null;
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
                chain.Inflate(UiMetrics.Space(12), UiMetrics.Space(12));
                chain.Height += PowerRowH + UiMetrics.Space(16);   // the POWER row under a node is part of it
                return new[] { chain };
            case TourTarget.RegionDetail:
                return new[] { DetailPanel };
            case TourTarget.EnterRegion:
                return new[] { CtaRect(DetailPanel) };
            default:
                return Array.Empty<Rectangle>();
        }
    }
    private static RegionDefinition Def(int i) => Regions.All[i];

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


    /// <param name="wheel">
    /// Mouse-wheel notches this frame (+ away, - toward, as <c>Game1.MouseWheel</c> latches them), for the
    /// inspector's flow. The host does not pass it yet — it passes GEAR's, FORGE's, BUILD's, VAULT's and
    /// TRAINING's, and this screen's needs the same one-line change in Game1; until then the arrow keys,
    /// PAGE UP / PAGE DOWN and a click on the scrollbar's track are the ways down the flow.
    /// </param>
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, int wheel = 0)
    {
        bool P(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);
        ScanReveals();
        if (P(Keys.Left)) { Select((_selected - 1 + RegionCount) % RegionCount); _cue = "sfx_click"; }
        if (P(Keys.Right)) { Select((_selected + 1) % RegionCount); _cue = "sfx_click"; }
        // A locked region has no CTA any more, so Enter must not be the one path that can still ask for it —
        // and the refusal is audible (§29), the same dull error the locked card's second click gives.
        if (P(Keys.Enter))
        {
            if (World.IsUnlocked(Def(_selected).Id)) { _enterRequest = Def(_selected).Id; _cue = "sfx_nav"; }
            else _cue = "sfx_error";
        }
        if (P(Keys.D) && World.CanDeepenCorruption) _deepenRequest = true;
        if (P(Keys.S) && World.CanEaseCorruption) _easeRequest = true;

        // THE INSPECTOR'S SCROLL: the wheel over the panel, or the arrow keys — the path that needs no
        // pointer. The upper clamp is the flow's own, applied once it has measured itself in Draw.
        var step = wheel != 0 && DetailPanel.Contains(mouse) ? -wheel : 0;
        if (P(Keys.Down)) step++;
        if (P(Keys.Up)) step--;
        if (P(Keys.PageDown)) step += Math.Max(1, _shown);
        if (P(Keys.PageUp)) step -= Math.Max(1, _shown);
        if (step != 0) _first = Math.Max(0, _first + step);
    }

    /// <summary>
    /// Which regions have become available since the screen last looked: each one it has not shown
    /// available before is pulsed once and remembered. The first look of a session only remembers.
    /// </summary>
    private void ScanReveals()
    {
        if (World is null) return;
        // A NEW GAME hands this screen a different World with the same region ids. Without this the set
        // still holds the old world's regions, and the first region the new career opens — the one reveal
        // a new player would ever see — would be remembered as already shown and pulse nothing.
        if (!ReferenceEquals(_memoOf, World)) { _memoOf = World; _shownAvailable.Clear(); _revealSeeded = false; }
        for (var i = 0; i < RegionCount; i++)
        {
            var id = Def(i).Id;
            if (!World.IsUnlocked(id) || !_shownAvailable.Add(id)) continue;
            // The first sight of the world this session seeds the set and pulses nothing — unless the rig
            // has asked to see this region's reveal.
            if (!_revealSeeded && id != _devRevealHold) continue;
            UiMotion.Flash(RevealKey(id), UiMotion.Reward);
            _cue = "sfx_reveal_tick";
        }
        _revealSeeded = true;
        if (_devRevealHold is { } hold) UiMotion.Flash(RevealKey(hold), UiMotion.Reward);   // the rig's freeze, at the peak
    }

    /// <summary>What a locked region needs, in one plain line — the inspector's plate and the card's hover tip share it.</summary>
    private static string LockedReason(RegionDefinition def)
        => def.PrereqId is { } p && Regions.Find(p) is { } pd ? $"CONQUER {pd.Name} FIRST" : "CONQUER THE REGION BEFORE THIS ONE FIRST";

    /// <summary>DEV: point the inspector at a region, so a capture can photograph it read (RH_SHOT fixtures).</summary>
    public void DevSelect(string regionId)
    {
        var i = Regions.All.ToList().FindIndex(r => r.Id == regionId);
        if (i >= 0 && i < RegionCount) Select(i);
    }

    /// <summary>Point the selection at the active region when the screen opens (host calls once on entry).</summary>
    public void SelectActive()
    {
        var idx = Regions.All.ToList().FindIndex(r => r.Id == ActiveRegion);
        if (idx >= 0) Select(idx);
    }

    public void Draw(SpriteBatch b, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = mouse;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xB0));
        _ui.TextCenterBig(b, "MAP", UiKit.PageCenterX, TitleY, UiInk.Accent, UiTypography.ScreenTitle, TextFace.Display);
        // The rule sits one title line under the title, so a 150 % title does not run through it.
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, TitleY + UiTypography.Pitch(UiTypography.ScreenTitle) + 1, 480, 3), Gold * 0.5f);
        // NO SUBTITLE. The band under the title is the hint slot's (D4): when a region opens, the screen
        // says so there, about this player's world, instead of reciting a balance figure on every visit.

        _lockTip = null;
        DrawMap(b, hit, clicked);
        DrawDetail(b, hit, clicked);
        // LAST, over both panels: a locked card's hover answer (§29) must not sit under the inspector.
        if (_lockTip is { } tip) _ui.HoverTip(b, tip.Text, tip.At);
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

        var body = UiTypography.Body;
        var bodyPitch = UiTypography.Pitch(body);
        var wordGap = UiMetrics.Space(8);
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
                // The click has a sound for what it meant (§27, §86): picking a card is a dry click, going
                // is the navigation tick, and asking to go somewhere locked is the dull error — with the
                // reason already on the card, under it and in the inspector.
                if (sel && unlocked) { _enterRequest = def.Id; _cue = "sfx_nav"; }
                else _cue = sel ? "sfx_error" : "sfx_click";
                Select(i);
            }

            var sc = SourceColor[def.Theme];
            // THE STANDARD STATES (§25–§28), the way UiKit.Button wears them. HOVER is a lift plus the
            // edge easing to bone over Fast (below). PRESSED is the mouse held on the card: the whole
            // face drops 2 px and darkens for exactly as long as it is held — the labels under the card,
            // the selected bar and the hit rectangle stay put (§15: one authoritative rect). SELECTED
            // keeps its gold bar and bone edge whether or not the pointer is on it, so hover and
            // selected never read as one state.
            var hot = node.Contains(hit);
            var pressed = hot && Held;
            var face = pressed ? new Rectangle(node.X, node.Y + 2, node.Width, node.Height) : node;
            // THE REGION'S OWN GROUND, not a flat swatch. The arena art the fight already draws for this
            // theme is cropped into the node and scrimmed back, so a place on the map looks like the
            // place you land in. A locked region gets a heavier, colder scrim: it reads as somewhere you
            // can SEE but have not been, which a uniform grey rectangle cannot say.
            if (_ui.Assets.Get(ArenaKey(def.Theme)) is { } ground)
                b.Draw(ground, face, CentreCrop(ground, face), Color.White);
            _ui.Fill(b, face, unlocked ? new Color(0x0A, 0x08, 0x14, 0xB4) : new Color(0x10, 0x10, 0x16, 0xE4));
            // THE FRAME SAYS WHERE YOU ARE. Gold, and a pixel thicker, on the region you are in; a pale
            // frame on the one you have selected; the conquest green and the Source colour otherwise.
            // The active region used to wear an unexplained purple diamond in its corner ("Verdant
            // Hollow has a purple icon") — the frame and the band below now say it in words.
            // Hover = highlight (D5): a card the pointer is on lifts and takes the pale edge, so the chart
            // answers the pointer before a click is spent.
            // The lift is a PREMULTIPLIED white at a few percent, eased in the way UiKit.Button eases its
            // own (§26). It was `new Color(0xE8, 0xDF, 0xC8, 0x14)` — a straight-alpha cream that the
            // premultiplied blend read as near-opaque, so a hovered card washed to bone and its name
            // vanished: hover and selected were not distinct, hover and unreadable were.
            var lift = UiMotion.Ease(UiMotion.KeyOf(node), hot ? 1f : 0f);
            if (lift > 0f) _ui.Fill(b, face, Color.White * (0.07f * lift));
            if (pressed) _ui.Fill(b, face, Color.Black * 0.18f);
            var restEdge = active ? Gold : sel ? Bone : conq ? Met : unlocked ? sc : Dim;
            var edge = active || sel ? restEdge : Color.Lerp(restEdge, Bone, lift);
            var thick = active ? FrameThick + 1 : FrameThick;
            foreach (var e in new[] { new Rectangle(face.X, face.Y, face.Width, thick), new Rectangle(face.X, face.Bottom - thick, face.Width, thick),
                                      new Rectangle(face.X, face.Y, thick, face.Height), new Rectangle(face.Right - thick, face.Y, thick, face.Height) })
                _ui.Fill(b, e, edge);
            if (active)
                foreach (var e in new[] { new Rectangle(node.X - 3, node.Y - 3, node.Width + 6, 2), new Rectangle(node.X - 3, node.Bottom + 1, node.Width + 6, 2),
                                          new Rectangle(node.X - 3, node.Y - 3, 2, node.Height + 6), new Rectangle(node.Right + 1, node.Y - 3, 2, node.Height + 6) })
                    _ui.Fill(b, e, Gold * 0.45f);

            // THE REVEAL (§73, §31 reward band): a region that has just become available wears a gold
            // halo that fades over one Reward pulse, and its face takes a wash of gold that drains back
            // to its own ground. Once, keyed to the region, armed in ScanReveals — never here. The halo
            // does not move, so Reduced Motion has nothing to drop: the same short fade, the same end.
            var reveal = UiMotion.Smooth(UiMotion.Pulse(RevealKey(def.Id)));
            if (reveal > 0f)
            {
                for (var ring = 1; ring <= 3; ring++)
                {
                    var o = ring * 3;
                    var a = reveal * (0.62f - 0.16f * ring);
                    foreach (var e in new[] { new Rectangle(face.X - o, face.Y - o, face.Width + o * 2, 3), new Rectangle(face.X - o, face.Bottom + o - 3, face.Width + o * 2, 3),
                                              new Rectangle(face.X - o, face.Y - o, 3, face.Height + o * 2), new Rectangle(face.Right + o - 3, face.Y - o, 3, face.Height + o * 2) })
                        _ui.Fill(b, e, Gold * a);
                }
                _ui.Fill(b, face, Gold * (0.18f * reveal));
            }

            // Emblem — one crest per region; unknown ids still fall back to a Source gem.
            var emblem = EmblemKey(def.Id);
            var eb = new Rectangle(face.Center.X - EmblemSize / 2, face.Y + EmblemTop, EmblemSize, EmblemSize);
            if (emblem is not null && _ui.Assets.Get(emblem) is { } em) b.Draw(em, eb, unlocked ? Color.White : new Color(0x55, 0x55, 0x60));
            else _ui.Diamond(b, eb, unlocked ? sc : Dim);

            // A LOCKED REGION KEEPS ITS WORDS. Its name, its element and its power used to be greyed, so
            // the one card a player most needs to read about — the place they cannot go yet — was the
            // hardest to read. The padlock and the band say "locked"; the letters do not have to.
            _ui.TextCenterBig(b, _ui.ShortenBig(def.Name, node.Width - wordGap * 2, body),
                              face.Center.X, face.Y + NameTop, Bone, body);

            // THE ELEMENT, READABLE: the Source gem with the element's name beside it, in the Source's
            // colour, centred as one group. What the creatures there are made of is the first thing a
            // build cares about, and it was only ever implied by the card's ground art.
            var element = def.Theme.ToString().ToUpperInvariant();
            var ew = _ui.MeasureBig(element, body);
            var ex = face.Center.X - (GemSize + wordGap + ew) / 2;
            var gemBox = new Rectangle(ex, face.Y + GemTop, GemSize, GemSize);
            if (_ui.Assets.Get(SourceGemKey(def.Theme)) is { } gem) b.Draw(gem, gemBox, Color.White);
            else _ui.Diamond(b, gemBox, sc);
            _ui.TextBig(b, element, gemBox.Right + wordGap, gemBox.Y + (GemSize - body) / 2, sc, body);

            // BELOW the card, not across its bottom border. Vellum, because these labels sit on the
            // parchment chart rather than on black.
            _ui.TextCenterBig(b, $"POWER {RegionPower(def):N0}", node.Center.X, node.Bottom + LabelGap,
                              UiKit.Vellum, body);

            // AND, UNDER A LOCKED ONE, WHAT OPENS IT. The prerequisite's name was one lookup away and
            // appeared nowhere; "locked" without "locked by what" is a dead end on the screen whose whole
            // job is deciding where to go next. Guarded so it never lands on the chart's bottom frame —
            // or, on the top row, on the card below it (150 % under a world strip is where that bites).
            var reqBottom = node.Bottom + PowerRowH + UiTypography.Pitch(UiTypography.Secondary);
            var reqLimit = Below(i) is { } under ? under.Y - UiMetrics.Space(4) : UiKit.PanelInner(MapCanvas).Bottom;
            var reqPrinted = false;
            if (!unlocked && def.PrereqId is { } pid && Regions.Find(pid) is { } pdef && reqBottom <= reqLimit)
            {
                _ui.TextCenterBig(b, $"CONQUER {pdef.Name}", node.Center.X, node.Bottom + PowerRowH,
                                  Bone, UiTypography.Secondary);
                reqPrinted = true;
            }

            // THE STATE, IN WORDS, on a band along the card's foot — with the fourth state the chart never
            // had: a region that is open, not conquered and not where you are said nothing at all.
            var band = new Rectangle(face.X + thick, face.Bottom - BandH - thick, face.Width - thick * 2, BandH);
            var bandText = band.Y + (BandH - body) / 2;
            if (active)
            {
                _ui.Fill(b, band, new Color(0x2A, 0x1E, 0x08, 0xE6));
                _ui.TextCenterBig(b, "YOU ARE HERE", face.Center.X, bandText, Gold, body);
                if (conq) DrawCheck(b, new Rectangle(face.Right - wordGap - CheckW, face.Y + EmblemTop, CheckW, CheckH), Met);
            }
            else if (conq)
            {
                _ui.Fill(b, band, new Color(0x08, 0x14, 0x0C, 0xE6));
                var cw = _ui.MeasureBig("CONQUERED", body);
                var cx = face.Center.X - (CheckW + wordGap + cw) / 2;
                DrawCheck(b, new Rectangle(cx, band.Y + (BandH - CheckH) / 2, CheckW, CheckH), Met);
                _ui.TextBig(b, "CONQUERED", cx + CheckW + wordGap, bandText, Met, body);
            }
            else if (unlocked)
            {
                // AVAILABLE, not gold: it is neither earned nor where you are (D9) — except for the one
                // pulse of its reveal, when the word is lit gold and settles back to bone (§24: gold is
                // "just became available", for exactly as long as that is news).
                _ui.Fill(b, band, Color.Lerp(new Color(0x14, 0x11, 0x1E, 0xE6), new Color(0x2A, 0x1E, 0x08, 0xE6), reveal));
                _ui.TextCenterBig(b, "AVAILABLE", face.Center.X, bandText, Color.Lerp(Bone, Gold, reveal), body);
            }
            else
            {
                _ui.Fill(b, band, new Color(0x10, 0x10, 0x16, 0xE6));
                var lockS = UiMetrics.Control(18);
                var lw = _ui.MeasureBig("LOCKED", body);
                var lx = face.Center.X - (lockS + wordGap + lw) / 2;
                DrawLockArt(b, new Rectangle(lx, band.Y + (BandH - lockS) / 2, lockS, lockS));
                _ui.TextBig(b, "LOCKED", lx + lockS + wordGap, bandText, Bone, body);
                // HOVER SAYS WHY (§29) — WHEN NOTHING ELSE DOES. The requirement is normally printed
                // under the card, and a tooltip repeating a sentence the player is already reading is the
                // "second, worse copy" this screen spent a pass deleting (§22: polish adds feel, not
                // information). So the tip is the FALLBACK for the poses where the guard above dropped
                // that line for want of room — a short field, a world strip over the chain, 150 % — where
                // the card would otherwise say LOCKED and refuse to say by what. Gathered here, drawn
                // last, over both panels.
                if (hot && !reqPrinted) _lockTip = (LockedReason(def), hit);
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
        var body = UiTypography.Body;
        var pad = UiMetrics.Space(20);
        if (World.AllConquered)
        {
            _ui.Plate(b, r, Gold);
            var label = CorruptionLook.Label(World.CorruptionTier);
            // The two small buttons hang off the strip's right end; the blurb takes whatever is left
            // between the label and them, and says nothing rather than an ellipsis alone.
            var btnW = UiMetrics.Control(168);
            var btnY = r.Y + UiMetrics.Space(6);
            var deep = new Rectangle(r.Right - UiMetrics.Space(8) - btnW, btnY, btnW, UiMetrics.ButtonHeightSmall);
            var ease = new Rectangle(deep.X - UiMetrics.Gap - btnW, btnY, btnW, UiMetrics.ButtonHeightSmall);
            // The label stops short of the buttons too: at 150 % the longest tier name ends seventeen
            // pixels before SHALLOWER (so the margin here is the small one — the bigger one cut it to an
            // ellipsis), and a longer one would otherwise run under the button.
            var labelX = r.X + pad;
            var shownLabel = _ui.ShortenBig(label, ease.X - UiMetrics.Space(8) - labelX, body);
            _ui.TextBig(b, shownLabel, labelX, r.Y + (r.Height - body) / 2, World.CorruptionTier > 0 ? Gold : Bone, body);
            var blurbX = labelX + _ui.MeasureBig(shownLabel, body) + UiMetrics.Space(24);
            var blurb = _ui.ShortenBig(CorruptionLook.For(World.CorruptionTier).Blurb, ease.X - UiMetrics.Space(16) - blurbX, UiTypography.Secondary);
            if (blurb.Length > 1)
                _ui.TextBig(b, blurb, blurbX, r.Y + (r.Height - UiTypography.Secondary) / 2, Slate, UiTypography.Secondary);
            if (_ui.Button(b, ease, "SHALLOWER", hit, clicked, World.CanEaseCorruption)) _easeRequest = true;
            if (_ui.Button(b, deep, "DEEPER", hit, clicked, World.CanDeepenCorruption)) _deepenRequest = true;
        }
        else if (Message.Length > 0)
        {
            _ui.Plate(b, r, Gold);
            _ui.TextCenterBig(b, _ui.ShortenBig(Message, r.Width - UiMetrics.Space(24) * 2, body), r.Center.X, r.Y + (r.Height - body) / 2, Gold, body);
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
    /// ones that do not apply cost nothing. The header (category, name) stays put and the button stays
    /// anchored; the flow between them scrolls when the profile makes it longer than the column.
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
        var cta = CtaRect(panel);

        // ── CATEGORY · NAME — the header stays put; everything under it is the flow. ──
        _ui.TextBig(b, $"REGION {_selected + 1} OF {RegionCount}", x, y, Slate, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary);
        _ui.TextBig(b, _ui.ShortenBig(def.Name, w, UiTypography.Headline), x, y,
                    def.Id == ActiveRegion ? Gold : Bone, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);

        // 16, not 40. The flow ran five pixels short of the START AT WAVE block on a conquered region held
        // deep — so the one control the panel offers besides the button was correctly suppressed, for want
        // of air nobody had asked for. The button keeps a clear line above it either way.
        var region = new Rectangle(x, y, w, cta.Y - UiMetrics.Space(16) - y);

        // MEASURED, THEN DRAWN. The flow records every item's height first; if the whole of it is taller
        // than the region, a scrollbar lane comes off the right and the flow is measured again at the
        // narrower width (a wrapped line can only get longer). Then the first item is clamped so the last
        // page is as full as it can be, and the flow is drawn from there.
        _itemHeights.Clear();
        Flow(b, def, unlocked, conq, farm, sc, region, hit, clicked, draw: false);
        var lane = 0;
        if (_itemHeights.Sum() > region.Height)
        {
            lane = UiMetrics.ScrollbarWidth + UiMetrics.Gap;
            region.Width -= lane;
            _itemHeights.Clear();
            Flow(b, def, unlocked, conq, farm, sc, region, hit, clicked, draw: false);
        }
        var total = _itemHeights.Count;
        var maxFirst = 0;
        for (int i = total - 1, acc = 0; i >= 0; i--)
        {
            acc += _itemHeights[i];
            if (acc > region.Height) { maxFirst = i + 1; break; }
        }
        if (_devScrollPending != 0) { _first = _devScrollPending; _devScrollPending = 0; }
        _first = Math.Clamp(_first, 0, maxFirst);
        _shown = Flow(b, def, unlocked, conq, farm, sc, region, hit, clicked, draw: true);

        if (lane > 0)
        {
            var track = new Rectangle(region.Right + UiMetrics.Gap, region.Y, UiMetrics.ScrollbarWidth, region.Height);
            _ui.ScrollBar(b, track, _first, _shown, total);
            // A click on the track pages toward the click — the way down the flow for a pointer whose
            // wheel does not reach this screen yet. The track maps to the flow in proportion, which is
            // where the thumb is drawn.
            if (UiKit.ClickedIn(track, hit, clicked))
            {
                var at = (hit.Y - track.Y) * total / Math.Max(1, track.Height);
                var page = Math.Max(1, _shown);
                if (at < _first) _first = Math.Max(0, _first - page);
                else if (at >= _first + _shown) _first = Math.Min(maxFirst, _first + page);
            }
        }

        // ── THE ONE THING YOU CAN DO. A locked region gets the requirement on the quiet tier rather than
        //    a disabled mystery button (§29). ──
        if (unlocked)
        {
            if (_ui.Button(b, cta, def.Id == ActiveRegion ? "RESUME HERE" : "HUNT HERE", hit, clicked, true, ButtonStyle.Primary))
            {
                _enterRequest = def.Id;
                _cue = "sfx_nav";
            }
        }
        else
        {
            _ui.Plate(b, cta);
            var say = LockedReason(def);
            var lockS = UiMetrics.Control(22);
            var label = UiTypography.NavigationLabel;
            DrawLockArt(b, new Rectangle(cta.X + UiMetrics.Space(20), cta.Center.Y - lockS / 2, lockS, lockS));
            _ui.TextCenterBig(b, _ui.ShortenBig(say, cta.Width - UiMetrics.Space(40) * 2, label),
                              cta.Center.X + lockS / 2, cta.Center.Y - label / 2 - 1, Bone, label);
        }
    }

    /// <summary>
    /// The inspector's flow, from the identity plate to the region's state. Each thing it draws is an
    /// ITEM of a known height. With <paramref name="draw"/> false every item's height is recorded into
    /// <see cref="_itemHeights"/> and nothing is drawn; with it true, items before <see cref="_first"/>
    /// are skipped and the rest are drawn while the region holds them. Returns how many it drew.
    /// </summary>
    private int Flow(SpriteBatch b, RegionDefinition def, bool unlocked, bool conq, Region farm, Color sc,
                     Rectangle region, Point hit, bool clicked, bool draw)
    {
        var x = region.X;
        var w = region.Width;
        var y = region.Y;
        var idx = 0;
        var shown = 0;
        var full = false;
        var body = UiTypography.Body;
        var bodyPitch = UiTypography.Pitch(body);
        var wordGap = UiMetrics.Space(10);

        // One item, h tall: records it when measuring; when drawing, places it at `top` if it is past the
        // scroll and the region still has room, and says whether to draw it. Once one item does not fit,
        // none after it is drawn — the flow is in order, and a gap would read as a missing block.
        // A HEADING KEEPS ITS FIRST LINE: "WHAT IT DROPS" over nothing, at the foot of a page, says
        // nothing — so a heading that would be the page's last item is held for the next page (unless it
        // is the page's first item, when holding it would leave the page blank).
        bool Item(int h, out int top, bool keepWithNext = false)
        {
            top = y;
            var i = idx++;
            if (!draw) { _itemHeights.Add(h); return false; }
            if (i < _first || full) return false;
            var need = keepWithNext && i > _first && i + 1 < _itemHeights.Count ? h + _itemHeights[i + 1] : h;
            if (y + need > region.Bottom) { full = true; return false; }
            y += h;
            shown++;
            return true;
        }
        void Section(string s)
        {
            if (Item(UiTypography.Pitch(UiTypography.Secondary), out var t, keepWithNext: true))
                _ui.TextBig(b, s, x, t, Slate, UiTypography.Secondary);
        }
        void Line(string s, Color c, int px, int maxLines = 3)
        {
            foreach (var l in _ui.WrapBig(s, w, px).Take(maxLines))
                if (Item(UiTypography.Pitch(px), out var t)) _ui.TextBig(b, l, x, t, c, px);
        }
        void Rule()
        {
            if (Item(UiMetrics.Space(16), out var t)) _ui.Fill(b, new Rectangle(x, t + UiMetrics.Space(6), w, 1), Dim);
        }
        void Pair(string label, string figure, Color figureInk)
        {
            if (!Item(UiTypography.Pitch(UiTypography.Headline), out var t)) return;
            _ui.TextBig(b, label, x, t + UiMetrics.Space(6), Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, figure, x + w, t, figureInk, UiTypography.Headline);
        }

        // ── IDENTITY: the region's own ground, its gem, its element. The Source gem used to be drawn
        //    four times per region on this screen; it appears twice now — once on the node, once here. ──
        var gemS = UiMetrics.Control(56);
        var plateH = UiMetrics.Space(14) * 2 + gemS;
        if (Item(plateH + UiMetrics.Space(12), out var pt))
        {
            var plate = new Rectangle(x, pt, w, plateH);
            _ui.Plate(b, plate, sc);
            if (_ui.Assets.Get(ArenaKey(def.Theme)) is { } ground) b.Draw(ground, plate, CentreCrop(ground, plate), Color.White);
            _ui.Fill(b, plate, new Color(0x0A, 0x08, 0x14, 0x9E));
            _ui.Fill(b, plate, sc * 0.16f);
            var gemBox = new Rectangle(plate.X + UiMetrics.Space(18), plate.Y + UiMetrics.Space(14), gemS, gemS);
            if (_ui.Assets.Get(SourceGemKey(def.Theme)) is { } pgem) b.Draw(pgem, gemBox, Color.White);
            _ui.TextBig(b, def.Theme.ToString().ToUpperInvariant(), gemBox.Right + UiMetrics.Space(16),
                        plate.Y + (plateH - UiTypography.Headline) / 2, sc, UiTypography.Headline);
        }
        Line(Description(def.Theme), Bone, body, 2);
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
             HunterPower >= need ? Met : Ember, body, 2);
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
        }, Bone, body, 1);
        var mod = RegionModifiers.For(def.Id);
        Line(mod.Name, sc, body, 1);
        Line(mod.Blurb, Bone, body, 2);
        Rule();

        // ── WHAT IT DROPS. The region's theme becomes the chest's element (Chests.RollDrop), and each
        //    region leans toward its own slots — which is the answer to "where should I farm". ──
        var drops = RegionDrops.For(def.Id);
        if (drops.Favoured.Count > 0)
        {
            Section("WHAT IT DROPS");
            Line($"{def.Theme.ToString().ToUpperInvariant()} ITEMS", sc, body, 1);
            var glyph = UiMetrics.IconSmall;
            foreach (var slot in drops.Favoured)
            {
                if (!Item(bodyPitch, out var t)) continue;
                if (SlotGlyph(slot) is { } key && _ui.Assets.Get(key) is { } gi)
                    b.Draw(gi, new Rectangle(x, t + (bodyPitch - glyph) / 2, glyph, glyph), Bone);
                _ui.TextBig(b, RegionDrops.PlainName(slot), x + glyph + wordGap, t, Bone, body);
            }
            Rule();
        }

        // ── YOU NEED FIRST (a locked region), or CURRENT STATE. ──
        if (!unlocked)
        {
            Section("YOU NEED FIRST");
            if (def.PrereqId is { } pid && Regions.Find(pid) is { } pdef)
            {
                var lockS = UiMetrics.Control(20);
                if (Item(bodyPitch, out var t))
                {
                    DrawLockArt(b, new Rectangle(x, t + (bodyPitch - lockS) / 2, lockS, lockS));
                    _ui.TextBig(b, $"CONQUER {pdef.Name}", x + lockS + wordGap, t, Bone, body);
                }
                Line($"{pdef.Name} — BEST WAVE {World.RegionFarm(pdef.Id).BestDepth} OF {ConquerWaves}", Slate, body, 1);
            }
            else Line("CONQUER THE REGION BEFORE THIS ONE.", Bone, body, 1);
        }
        else
        {
            Section("CURRENT STATE");
            if (conq)
            {
                if (Item(bodyPitch, out var t))
                {
                    DrawCheck(b, new Rectangle(x, t + UiMetrics.Space(2), CheckW, CheckH), Met);
                    _ui.TextBig(b, "CONQUERED", x + CheckW + wordGap, t, Met, body);
                }
                Line($"BEST WAVE {farm.BestDepth}", Bone, body, 1);
                StartAtWave();
            }
            else
            {
                Line($"BEST WAVE {farm.BestDepth} OF {ConquerWaves}", Bone, body, 1);
                // The rule is Game1's `Deepest >= ConquerWaveDepth`, and the guide already says "REACH
                // WAVE 20" — "HOLD 20 WAVES" was the same rule in a second voice.
                Line($"REACH WAVE {ConquerWaves} TO CONQUER THIS REGION", Slate, body, 2);
            }
        }
        return shown;

        // START AT WAVE — the checkpoint chips of a conquered region (Checkpoints): 0 and every ten waves
        // the champion has held here. A chip is the wave the descent starts AFTER, priced in Memory Dust
        // per descent; the chosen one is gold, an unaffordable one is dim and says so.
        void StartAtWave()
        {
            var options = new List<int>(Checkpoints.Options(farm.BestDepth, conquered: true));
            var gap = UiMetrics.Space(4);
            // The row holds so many chips. Past that the SHALLOW middle goes (TOP and the deepest ones stay —
            // a player at wave 110 wants 100 and 110, not 10), never the deepest (review 2026-08-26).
            int ChipW(int o) => Math.Max(UiMetrics.Control(44), _ui.MeasureBig(o == 0 ? "TOP" : o.ToString(), UiTypography.Secondary) + UiTypography.ChipPadX * 2);
            int RowWidth() { var t = 0; foreach (var o in options) t += ChipW(o) + gap; return t; }
            while (options.Count > 2 && RowWidth() > w) options.RemoveAt(1);

            // ONE OPTION IS NOT A CHOICE. A region conquered at wave 1 offers only the top, and a header over a
            // single inert chip is furniture — the block appears when there is somewhere else to start.
            if (options.Count < 2) return;
            // 30 px chips at 100 %: Secondary plus the chip padding is 27, and the six pixels saved are what
            // let the consequence line under them fit on a deep region. They follow the profile.
            var chipH = UiMetrics.Control(30);

            // THE COST RIDES THE HEADER'S OWN ROW. On its own line it was the first thing the flow dropped on a
            // deep region — and it is the half that says what pressing a chip will charge you.
            var chosen = Checkpoints.Clamp(farm.StartWave, farm.BestDepth, true);
            var cost = Checkpoints.DustCost(chosen);
            var affordChosen = DustOwned >= cost;
            if (Item(UiTypography.Pitch(UiTypography.Secondary), out var ht, keepWithNext: true))
            {
                _ui.TextBig(b, "START AT WAVE", x, ht, Slate, UiTypography.Secondary);
                // WHEN IT CANNOT BE PAID, THE ROW SAYS WHAT WILL ACTUALLY HAPPEN. The host quietly starts the run at
                // the top when the chosen checkpoint is unaffordable, and the map never said so — the player watched
                // a gold chip and landed somewhere else. The price gives up its row to the consequence, because a
                // player who cannot pay needs to know where they will wake up more than what it would have cost.
                _ui.TextRightBig(b, chosen == 0 ? "FREE"
                                    : affordChosen ? $"{cost:N0} MEMORY DUST EACH TIME"
                                    : $"NEED {cost:N0} DUST — STARTS AT THE TOP",
                                 x + w, ht, affordChosen ? Slate : Ember, UiTypography.Secondary);
            }
            if (!Item(chipH + gap, out var ct)) return;
            var cx = x;
            foreach (var o in options)
            {
                var label = o == 0 ? "TOP" : o.ToString();
                var chip = new Rectangle(cx, ct, ChipW(o), chipH);
                var afford = DustOwned >= Checkpoints.DustCost(o);
                var lit = o == chosen;
                // The same states as the cards (§25–§27): the edge eases to bone under the pointer, and
                // a held chip drops a pixel and darkens until it is let go. Hit-tested on `chip`, drawn
                // on `cf`.
                var chipHot = chip.Contains(hit);
                var chipPressed = chipHot && Held;
                var chipLift = UiMotion.Ease(UiMotion.KeyOf(chip), chipHot ? 1f : 0f);
                var cf = chipPressed ? new Rectangle(chip.X, chip.Y + 1, chip.Width, chip.Height) : chip;
                _ui.Fill(b, cf, lit ? new Color(0x3A, 0x2C, 0x14, 0xE0) : new Color(0x14, 0x10, 0x1A, 0xE0));
                if (chipLift > 0f) _ui.Fill(b, cf, Color.White * (0.07f * chipLift));
                if (chipPressed) _ui.Fill(b, cf, Color.Black * 0.18f);
                var edge = lit ? Gold : Color.Lerp(Dim, Bone, chipLift);
                _ui.Fill(b, new Rectangle(cf.X, cf.Y, cf.Width, 2), edge);
                _ui.Fill(b, new Rectangle(cf.X, cf.Bottom - 2, cf.Width, 2), edge);
                _ui.Fill(b, new Rectangle(cf.X, cf.Y, 2, cf.Height), edge);
                _ui.Fill(b, new Rectangle(cf.Right - 2, cf.Y, 2, cf.Height), edge);
                _ui.TextCenterBig(b, label, cf.Center.X, cf.Y + (chipH - UiTypography.Secondary) / 2,
                                  lit ? Gold : afford ? Bone : UiInk.Disabled, UiTypography.Secondary);
                if (UiKit.ClickedIn(chip, hit, clicked)) _startRequest = (def.Id, o);
                cx += chip.Width + gap;
            }
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

    /// <summary>A check mark drawn to fill its box — the 22×18 glyph at 100 %, and the same shape scaled with it.</summary>
    private void DrawCheck(SpriteBatch b, Rectangle r, Color c)
    {
        var f = r.Height / 18f;
        int S(int v) => (int)MathF.Round(v * f, MidpointRounding.AwayFromZero);
        var dot = Math.Max(2, S(3));
        for (var k = 0; k < S(6); k++) _ui.Fill(b, new Rectangle(r.X + k, r.Bottom - S(8) + k / 2, dot, dot), c);
        for (var k = 0; k < S(11); k++) _ui.Fill(b, new Rectangle(r.X + S(6) + k, r.Bottom - S(2) - k, dot, dot), c);
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
