using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Prestige;

using IdleXIdle.Core.Progression;
namespace IdleXIdle.Game;

/// <summary>
/// The TRAITS screen (nav: TRAITS): the permanent trait tree, drawn as a free canvas under a camera,
/// with the selected trait read in full on a docked panel to the right.
/// </summary>
/// <remarks>
/// <para>
/// Every value is real, from <see cref="MemoryDustTree"/>: every unlock with its name, cost, description,
/// prerequisites and its <see cref="UnlockEffect"/> category, explained in plain sentences by
/// <see cref="MemoryDustText"/>. The real tree is a graph of BINARY unlocks (owned / available / locked),
/// so the panel says LEARNED / AVAILABLE NOW / LOCKED. The class keeps its name so the host wiring is
/// unchanged.
/// </para>
/// <para>
/// <b>A FREE SCREEN, LIKE THE MASTERY TREE.</b> Playtest, 2026-08-25: "the trait screen's UI should not be
/// squeezed into a canvas". The diagram used to be fitted, at whatever scale made it fit, into a framed
/// panel — fifty-one nodes and their names at fifteen pixels, because that is what fitted. Now the tree
/// lives in WORLD units (<see cref="TraitTreeLayout"/>) and a camera decides what is on screen: the
/// default framing shows the whole tree, and the wheel, a held drag, the arrow keys and +/- move in to
/// read one road at a time. Same camera as <c>BuildScreen</c> — pan is the world point at the view's
/// centre, zoom is screen pixels per world unit, and the hit-testing goes through the same transform
/// as the drawing so the two can never disagree.
/// </para>
/// </remarks>
public sealed class PrestigeScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    // 0x5E5A6E, up from 0x3A3A44. Dim is the LOCKED outline and the divider rule, and at the old value a
    // locked node on the 0xC0-alpha scrim was a square of near-black on near-black — the playtest said
    // the tree was hard to read, and forty of its fifty-one nodes are locked at any given career. A
    // locked trait should look unbought, not absent.
    private static readonly Color Dim = new(0x5E, 0x5A, 0x6E);
    /// <summary>The tint of a locked node's icon — lighter than Dim, so the glyph still reads as a glyph.</summary>
    private static readonly Color LockedInk = new(0x84, 0x7E, 0x96);
    /// <summary>The dark plate a name sits on, so a wire running behind it never runs through it.</summary>
    private static readonly Color Plate = new(0x0A, 0x08, 0x10);
    private static readonly Color Met = new(0x6E, 0xC8, 0x7A);
    private static readonly Color Violet = new(0xC0, 0x6E, 0xE0);
    private static readonly Color Teal = new(0x5F, 0xE0, 0xC8);

    private readonly UiKit _ui;
    private string _selectedId = "";
    /// <summary>The node under the pointer this frame — the detail panel prefers it to the pinned one.</summary>
    private string? _hoverId;
    private string _msg = "";
    private KeyboardState _prevKeys;

    /// <summary>Running seconds, fed by Update's dt — drives the affordable nodes' slow pulse.</summary>
    private float _time;

    // ── The unlock flourish ──────────────────────────────────────────────────────────────────────
    //
    // A trait is PERMANENT and there is no respec. That is the most consequential button in the game,
    // and pressing it used to change one word in a status line from AVAILABLE to LIT. The screen was
    // accused of being "çok basit ve kalitesiz" and this was the heart of it: nothing about taking a
    // trait felt like it had happened.
    //
    // Time-driven, not frame-driven, so the shape of the celebration is the same on any machine.
    private float _litT = -1f;             // seconds into the flourish; negative means idle
    private string _litName = "";
    private TraitRoad _litRoad;
    private string? _litArt;               // the terminal's own emblem, when a terminal was taken
    private bool _litTerminal;
    private bool _litFrozen;               // DEV: hold one frame of the flourish for a capture
    private string? _cue;                  // sound cue waiting for the host to play

    /// <summary>How long the flourish runs. A terminal ends a road; it is allowed to take twice as long.</summary>
    private float LitDuration => _litTerminal ? 2.2f : 1.0f;

    /// <summary>
    /// The camera kick, in 1920-space pixels. The host adds it to the overlay transform.
    /// </summary>
    /// <remarks>
    /// Owned here rather than in the host's present blit, which is where a global shake used to live and
    /// where it sat at a permanent zero for the whole of development because nothing drove it. A screen
    /// that wants a kick should own one; a shared one nobody owns decays into dead code.
    ///
    /// Deliberately NOT applied to the mouse mapping: a pointer that slides out from under the cursor
    /// for a fifth of a second is a bug, not game feel.
    /// </remarks>
    public Vector2 Shake { get; private set; }

    /// <summary>
    /// The sound cue for a trait just taken, cleared by reading — the host owns audio, this screen does not.
    /// </summary>
    /// <remarks>Same shape as StatsScreen.ConsumeTrain, so the host's Update reads one way everywhere.</remarks>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    public bool DevDustDebug { get; set; }

    public PrestigeScreen(UiKit ui, MemoryDustTree tree)
    {
        _ui = ui;
        _selectedId = tree.All.OrderBy(u => u.Cost).First().Id;
        // Every node this tree holds must have a home in the diagram. The layout covers the catalogue by
        // construction; this is the loud version for a debug build, so a tree the layout has never seen
        // is noticed at the desk rather than in a capture — and Pos() still draws it either way.
        System.Diagnostics.Debug.Assert(TraitTreeLayout.Covers(tree.All.Select(u => u.Id)),
            "TraitTreeLayout does not cover every node of this tree — see TraitTreeLayout.Unauthored / PrestigeScreen.Pos");
        // The drawing sizes below must fit the room the layout promises, or the layout's no-overlap
        // test is holding a promise the screen breaks.
        System.Diagnostics.Debug.Assert(NameWidth + 8 <= TraitTreeLayout.NodeWidth
                                        && RadiusOf(SocketKind.Terminal) * 2 + NameGap + 3 * NameLineH + 2 <= TraitTreeLayout.NodeHeight,
            "PrestigeScreen draws nodes larger than TraitTreeLayout.NodeWidth/NodeHeight allow");

        (_worldMin, _worldMax) = WorldExtent();
        _homeZoom = FitZoom();
        _homePan = (_worldMin + _worldMax) / 2f;
        _zoom = _homeZoom;
        _pan = _homePan;
    }

    /// <summary>DEV ONLY: pose the detail panel on a specific blessing for the screenshot fixture.</summary>
    public void DevSelect(string id) => _selectedId = id;

    /// <summary>DEV ONLY: park the camera at a zoom, centred on a node, so a capture can prove the zoomed view.</summary>
    public void DevCamera(float zoom, string centreId)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _pan = Pos(centreId);
        ClampPan();
    }

    /// <summary>DEV ONLY: freeze the unlock flourish part-way through so a capture can prove it draws.</summary>
    /// <remarks>
    /// A celebration is the one thing a still screenshot cannot catch by accident — it is over in a
    /// second, and the capture rig renders a fixed frame count and exits. Without this, "the flourish
    /// works" would be a claim rather than a verified fact, which is exactly what this project's
    /// verification rule exists to prevent.
    /// </remarks>
    public void DevPoseLit(MemoryDustTree tree, string id, float t)
    {
        if (tree.All.FirstOrDefault(u => u.Id == id) is not { } u) return;
        _selectedId = id;
        BeginLit(u);
        _litT = t;
        // FROZEN, or the fixture is useless: the capture rig renders sixty frames before it saves, so
        // an un-frozen pose advances a full second and every mode would screenshot the same empty
        // moment after the flourish had already finished.
        _litFrozen = true;
        TickFlourish(0f);   // settle Shake for the posed instant, so the kick is in the picture too
    }

    // ── The page ────────────────────────────────────────────────────────────────────────────────
    // No frame around the tree any more: the whole canvas left of the detail panel and under the top
    // strip is the tree's, edge to edge, and the camera decides what part of the world it shows. The
    // detail panel stays docked on the right.

    /// <summary>The tree's canvas: everything left of the detail panel, below the top strip.</summary>
    private static readonly Rectangle View = new(40, 136, 1300, 934);

    /// <summary>The docked reading panel, at 1368..1868.</summary>
    private static readonly Rectangle DetailPanel = new(1368, 144, 500, 790);

    /// <summary>
    /// THE POINT COUNTER'S PLATE — a framed readout in the band between the subtitle and the canvas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest 2026-08-29: "the indicator at the top left that shows my points is left as undecorated
    /// plain text. Let's polish this." It was exactly that — two bare strings laid straight over the
    /// tree's starfield, in the one corner of the screen a player checks before every single purchase,
    /// and the number that decides whether they can buy anything was the same size as a road's tally.
    /// It now wears the house's quiet frame with its own glyph, and the number is at the headline rung
    /// the GEAR POWER and MASTERY POINTS readouts use for the same job.
    /// </para>
    /// <para>
    /// <b>52 px tall, ending 4 px above <see cref="View"/>.</b> The tree is drawn FIRST and clipped to
    /// its canvas, so anything drawn after it covers it; the plate therefore lives entirely in the header
    /// band — under the title rule, left of the centred subtitle, above the canvas — and never eats a
    /// road. Its width is FIXED rather than measured so the caption does not shuffle sideways as the
    /// number goes from one digit to two; the number is right-aligned into its own column instead.
    /// </para>
    /// </remarks>
    // Just the glyph and the number. The caption beside them ("TRAIT POINTS TO SPEND") made the plate a
    // 360-px ribbon around a one- or two-digit number (playtest 2026-08-30: "the frame is far too long
    // and thin — delete the text, dress the frame around the points and the icon only"). The size comes
    // from the house grid: the frame's own corner plus the standard padding on each side, and a height
    // that clears PrimaryValue with PanelBodyTop above it and PanelPadBottom below.
    private static readonly Rectangle PointPlate = new(View.X, 76, 156, 84);

    /// <summary>The plate's glyph box — shared by the draw and the tour.</summary>
    private const int PointGlyph = 40;

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>
    /// The points readout is <see cref="PointPlate"/> itself — the light now has a frame to trace rather
    /// than a hand-boxed guess at where two bare strings happened to sit.
    /// </remarks>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.TraitTree => new[] { View },
        TourTarget.TraitPoints => new[] { PointPlate },
        TourTarget.TraitDetail => new[] { DetailPanel },
        _ => Array.Empty<Rectangle>(),
    };

    // ── The camera ──────────────────────────────────────────────────────────────────────────────
    private Vector2 _pan;                  // world point at the centre of the view
    private float _zoom;                   // screen pixels per world unit
    private readonly float _homeZoom;      // the framing HOME returns to: the whole tree, fitted
    private readonly Vector2 _homePan;
    private readonly Vector2 _worldMin, _worldMax;   // the world the pan may not leave
    private Point? _dragFrom;              // where a drag started, in screen space
    private Vector2 _dragPanFrom;

    /// <summary>Below this every caption drops out and only shapes remain — a zoomed-out tree is a shape, not a list.</summary>
    private float LabelZoom => _homeZoom * 0.72f;
    private float MinZoom => _homeZoom * 0.5f;
    private const float MaxZoom = 1.8f;

    /// <summary>Room reserved above a crown's centre for the road header (glyph, name, identity, tally), in world units.</summary>
    private const float HeaderRoom = 150f;

    /// <summary>Room reserved below the deepest root's centre for its name and the spine caption, in world units.</summary>
    private const float CaptionRoom = 176f;

    private Vector2 ToScreen(Vector2 world)
        => new(View.Center.X + (world.X - _pan.X) * _zoom, View.Center.Y + (world.Y - _pan.Y) * _zoom);

    private Vector2 ToWorld(Point screen)
        => new((screen.X - View.Center.X) / _zoom + _pan.X, (screen.Y - View.Center.Y) / _zoom + _pan.Y);

    /// <summary>A world length in screen pixels, rounded, never below one.</summary>
    private int Px(float world) => Math.Max(1, (int)MathF.Round(world * _zoom));

    /// <summary>The world rectangle the whole diagram occupies, names and headers included.</summary>
    private (Vector2 Min, Vector2 Max) WorldExtent()
    {
        var (nmin, nmax) = TraitTreeLayout.Bounds();
        var min = Xna(nmin);
        var max = Xna(nmax);
        foreach (var s in _strays.Values) max = Vector2.Max(max, s);
        var side = TraitTreeLayout.NodeWidth / 2f + 16f;
        return (new Vector2(min.X - side, min.Y - HeaderRoom), new Vector2(max.X + side, max.Y + CaptionRoom));
    }

    /// <summary>The zoom that fits the whole extent into the view, with a little air.</summary>
    private float FitZoom()
    {
        var span = Vector2.Max(_worldMax - _worldMin, new Vector2(1f));
        return MathF.Min(View.Width / span.X, View.Height / span.Y) * 0.985f;
    }

    /// <summary>
    /// Zoom about the POINTER, not about the centre.
    /// </summary>
    /// <remarks>
    /// Zooming about the centre makes the thing under the cursor slide away, so a player who scrolls to
    /// look closer at a node has to chase it. Anchoring on the pointer is what makes a map feel like a
    /// map rather than a slider.
    /// </remarks>
    private void ZoomAt(Point anchor, float factor)
    {
        var before = ToWorld(anchor);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        var after = ToWorld(anchor);
        _pan += before - after;
        ClampPan();
    }

    /// <summary>Keep the tree reachable: the view's centre may wander, but never off the diagram.</summary>
    private void ClampPan()
        => _pan = new Vector2(Math.Clamp(_pan.X, _worldMin.X, _worldMax.X), Math.Clamp(_pan.Y, _worldMin.Y, _worldMax.Y));

    /// <summary>
    /// The tree is grouped by ROAD, not by effect category.
    /// </summary>
    /// <remarks>
    /// Amplifier / Expansion / Convenience describes what a node DOES to the engine, which is an
    /// implementation fact and tells a player nothing about the decision in front of them. The decision
    /// is which of four roads to walk, and it is the one thing the screen has to communicate: two roads
    /// cost sixty against roughly thirty-four earnable, so the road you do not take is permanent. The
    /// names and the one-line identities come from <see cref="TraitRoads"/>, so the header, the detail
    /// panel and the tests print the same sentence.
    /// </remarks>
    private static string RoadName(TraitRoad r) => TraitRoads.Name(r);

    private static Color RoadColor(TraitRoad r) => r switch
    {
        TraitRoad.Spine => Teal,
        TraitRoad.Ruin => new Color(0xD6, 0x48, 0x5C),
        TraitRoad.Aegis => new Color(0x74, 0x9A, 0xE8),
        TraitRoad.Avarice => Gold,
        _ => Violet,
    };

    /// <summary>The road's emblem. Tinted at the draw site, so one asset serves every state.</summary>
    private static string RoadGlyph(TraitRoad r) => r switch
    {
        TraitRoad.Spine => "icon_road_spine",
        TraitRoad.Ruin => "icon_road_ruin",
        TraitRoad.Aegis => "icon_road_aegis",
        TraitRoad.Avarice => "icon_road_avarice",
        _ => "icon_road_artifice",
    };

    /// <summary>
    /// A TERMINAL's own emblem — the four 12-point nodes each road ends on, and nothing else.
    /// </summary>
    /// <remarks>
    /// Keyed off the granted keystone rather than the cost, because "costs 12" is a balance value that
    /// may move and "is the end of the Ruin road" is not. Everything else on the screen shares three
    /// category icons; these four are the only traits expensive enough to deserve a picture of their own,
    /// and two of them cost more than a career earns — a player will mostly meet them by reading them.
    /// </remarks>
    private static string? TerminalArt(MemoryDustUnlock u) => u.GrantsKeystone switch
    {
        "reaper" => "art_terminal_reaper",
        "titan" => "art_terminal_titan",
        "hoarder" => "art_terminal_hoarder",
        "weaver" => "art_terminal_weaver",
        _ => null,
    };

    // ── The node's dress: the mastery tree's three-channel language, worn by this tree ──────────
    // The FRAME's shape says what KIND of thing a node is, the GLYPH inside says which ROAD it
    // serves, and the tint on both says its state.

    /// <summary>What kind of thing a node is — decides its frame and its size, and nothing else.</summary>
    private enum SocketKind
    {
        /// <summary>A road's crown: its terminal, wearing its own emblem in the largest frame.</summary>
        Terminal,

        /// <summary>A rung of a road's keystone strand — the steps a road is walked in.</summary>
        Rung,

        /// <summary>A CHARGE spur: a keystone beside the road, not on it. A side step.</summary>
        Spur,

        /// <summary>A structural spine node: sockets, slots, vows, auto-selling, the forge.</summary>
        Gate,

        /// <summary>A small attribute node — a modest number, hung off a walked rung.</summary>
        Minor,

        /// <summary>A MARK — NO EFFECT: the quiet mark for standing at the head of all four roads.</summary>
        Mark,
    }

    /// <summary>
    /// Classify a node from its DATA, not from an id list: what grants a keystone and what requires
    /// it, which road it serves and what effect it has are all facts the catalogue already states,
    /// so a new node arrives correctly dressed without this file hearing about it.
    /// </summary>
    private static SocketKind KindOf(MemoryDustTree tree, MemoryDustUnlock u)
    {
        if (TerminalArt(u) is not null) return SocketKind.Terminal;
        if (u.Requires.Count >= 4) return SocketKind.Mark;   // the one five-way node in the tree
        if (u.GrantsKeystone is not null)
            // A spur is a keystone gate nothing builds on; every on-road gate has a next step.
            return tree.All.Any(o => o.Requires.Contains(u.Id)) ? SocketKind.Rung : SocketKind.Spur;
        if (u.Effect == UnlockEffect.Amplifier) return SocketKind.Minor;
        return u.Road == TraitRoad.Spine ? SocketKind.Gate : SocketKind.Rung;
    }

    /// <summary>The frame a kind is set in — the mastery tree's socket art, worn by rank.</summary>
    private static string FrameKey(SocketKind k) => k switch
    {
        SocketKind.Terminal => "ui_node_greater",
        SocketKind.Rung => "ui_node_notable",
        SocketKind.Spur => "ui_node_spec",
        SocketKind.Gate => "ui_node_bridge",
        SocketKind.Mark => "ui_node_start",
        _ => "ui_node_minor",
    };

    /// <summary>
    /// Node radius in WORLD units, by kind. Size is the hierarchy: crowns largest, minors smallest.
    /// Drawn larger than the old fitted diagram could afford, so zooming in reveals the art.
    /// </summary>
    private static int RadiusOf(SocketKind k) => k switch
    {
        SocketKind.Terminal => 44,
        SocketKind.Rung => 34,
        SocketKind.Spur => 34,
        SocketKind.Gate => 30,
        SocketKind.Mark => 32,
        _ => 26,
    };

    /// <summary>The same hue, darker — opaque, so a locked node dims instead of turning translucent.</summary>
    private static Color Muted(Color c, float f) => new((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));

    // Spine first (everyone walks it), then the four roads in enum order. WITHIN a road, by cost —
    // which is the order it is walked, so a road reads left to right as the chain it actually is.
    private List<MemoryDustUnlock> Ordered(MemoryDustTree tree) =>
        tree.All.OrderBy(u => (int)u.Road).ThenBy(u => u.Cost).ThenBy(u => u.Name).ToList();

    private MemoryDustUnlock Selected(MemoryDustTree tree) =>
        tree.All.FirstOrDefault(u => u.Id == _selectedId) ?? Ordered(tree).First();

    private bool Pressed(KeyboardState now, Keys k) => now.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    /// <summary>
    /// One frame of input: the camera (wheel, drag, arrows, +/-, Home) and Enter to learn the selected trait.
    /// </summary>
    /// <param name="held">The left button is DOWN this frame — a held button drags the tree.</param>
    public void Update(KeyboardState keys, Point mouse, bool clicked, bool held, int wheel, MemoryDustTree tree, float dt)
    {
        TickFlourish(dt);
        _time += dt;

        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var over = Game1.ToOverlay(mouse);

        // ── THE CAMERA — the mastery tree's, verbatim in spirit. ──────────────────────────────
        if (wheel != 0 && View.Contains(over))
            ZoomAt(over, wheel > 0 ? 1.16f : 1f / 1.16f);

        // DRAG TO PAN. Held, not clicked: a click pins a node for the panel, and a tree you can only
        // move with a scrollbar is a tree nobody moves. A drag that began inside the view keeps
        // panning even when the pointer leaves it.
        if (held && (_dragFrom is not null || View.Contains(over)))
        {
            if (_dragFrom is null) { _dragFrom = over; _dragPanFrom = _pan; }
            else
            {
                var d = _dragFrom.Value;
                _pan = _dragPanFrom + new Vector2((d.X - over.X) / _zoom, (d.Y - over.Y) / _zoom);
                ClampPan();
            }
        }
        else _dragFrom = null;

        // Keyboard, for anyone who would rather not drag. Arrows pan (they used to step the selection
        // through the list, which a free canvas has no use for), +/- zoom about the centre, Home resets.
        var step = 260f / _zoom * 0.06f;
        if (keys.IsKeyDown(Keys.Left)) { _pan.X -= step; ClampPan(); }
        if (keys.IsKeyDown(Keys.Right)) { _pan.X += step; ClampPan(); }
        if (keys.IsKeyDown(Keys.Up)) { _pan.Y -= step; ClampPan(); }
        if (keys.IsKeyDown(Keys.Down)) { _pan.Y += step; ClampPan(); }
        if (Pressed(keys, Keys.OemPlus) || Pressed(keys, Keys.Add)) ZoomAt(View.Center, 1.25f);
        if (Pressed(keys, Keys.OemMinus) || Pressed(keys, Keys.Subtract)) ZoomAt(View.Center, 1f / 1.25f);
        if (Pressed(keys, Keys.Home)) { _pan = _homePan; _zoom = _homeZoom; }

        if (Pressed(keys, Keys.Enter)) Buy(tree, Selected(tree));

        _prevKeys = keys;
    }

    private void Buy(MemoryDustTree tree, MemoryDustUnlock u)
    {
        if (tree.Owns(u.Id)) _msg = "ALREADY LEARNED — A TRAIT IS PERMANENT.";
        else if (tree.Purchase(u.Id)) { _msg = $"LEARNED: {u.Name}."; BeginLit(u); }
        else if (tree.Available < u.Cost) _msg = "NOT ENOUGH TRAIT POINTS. EARN MORE: CONQUER A REGION, GO DEEPER INTO THE CORRUPTION, OR RAISE A REGION'S MASTERY.";
        else _msg = "LOCKED — LEARN WHAT IT NEEDS FIRST.";
    }

    /// <summary>Arm the flourish for a trait that was just bought.</summary>
    private void BeginLit(MemoryDustUnlock u)
    {
        _litT = 0f;
        _litFrozen = false;
        _litName = u.Name;
        _litRoad = u.Road;
        _litArt = TerminalArt(u);
        _litTerminal = _litArt is not null;
        // Specific first, then generic: a terminal gets its own cue if one is authored, and falls back
        // to the ordinary one rather than to silence.
        _cue = _litTerminal ? "sfx_trait_terminal" : "sfx_trait_lit";
    }

    /// <summary>
    /// Advance the flourish and the camera kick it drives.
    /// </summary>
    /// <remarks>
    /// The kick is a decaying oscillation, NOT a random jitter: a capture has to be able to reproduce
    /// the same frame twice, and a shake sampled from an RNG makes every screenshot of it a different
    /// picture. Two incommensurate frequencies read as a knock rather than a wobble.
    /// </remarks>
    private void TickFlourish(float dt)
    {
        if (_litT < 0f) { Shake = Vector2.Zero; return; }

        if (!_litFrozen) _litT += dt;
        if (_litT >= LitDuration) { _litT = -1f; Shake = Vector2.Zero; return; }

        // Shake belongs to the first third and then gets out of the way — a screen that is still
        // moving while the player reads the name is a screen they cannot read.
        var window = LitDuration * 0.34f;
        if (_litT > window) { Shake = Vector2.Zero; return; }

        var energy = 1f - _litT / window;
        var amp = (_litTerminal ? 22f : 8f) * energy * energy;
        Shake = new Vector2(MathF.Sin(_litT * 71f) * amp, MathF.Cos(_litT * 53f) * amp * 0.7f);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, MemoryDustTree tree) => Draw(b, tree, new Point(-1, -1), false);

    public void Draw(SpriteBatch b, MemoryDustTree tree, Point mouse, bool clicked)
    {
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));

        // THE TREE FIRST, clipped to its canvas, so the strip and the panel are drawn over it rather
        // than a zoomed-in node being drawn over them.
        _hoverId = null;
        DrawTree(b, tree, hit, clicked);

        // ── THE TOP STRIP ──
        // TRAITS, not DUST. The screen stopped spending Memory Dust when the tree stopped being buyable
        // by idling; a title naming a currency it does not charge is the kind of small lie that makes a
        // player mistrust every other number on the screen.
        _ui.TextCenterBig(b, "TRAITS", 960, 24, new Color(0xF0, 0xB2, 0x4A), UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(720, 74, 480, 3), Gold * 0.5f);
        // "NO TAKING BACK", not "NO RESPEC" — the reader plays in English as a second language, and
        // "respec" is a word only the genre knows.
        _ui.TextCenterBig(b, "ONE SPINE  ·  FOUR ROADS  ·  NO TAKING BACK", 960, 80, Slate, UiTypography.Secondary);

        // How many points you have to spend, how much of the tree you have learned, and what all of it
        // would cost. A header rather than a panel: a player checks "can I afford this" constantly and
        // "what does the whole tree cost" once, and neither is worth a third of the page.
        var spent = tree.All.Where(u => tree.Owns(u.Id)).Sum(u => u.Cost);
        var learned = tree.All.Count(u => tree.Owns(u.Id));
        DrawPointPlate(b, tree);
        // The long tally is the OTHER half of the same line and stays a quiet caption — it is read once
        // a session, not before every purchase — but it now sits on the plate's own optical centre
        // instead of ten pixels below it.
        _ui.TextRightBig(b, $"{learned} OF {tree.All.Count} TRAITS LEARNED  ·  {spent} POINTS SPENT  ·  {tree.TotalTreeCost} FOR EVERYTHING",
                         View.Right, PointPlate.Y + (PointPlate.Height - UiTypography.Secondary) / 2, Slate, UiTypography.Secondary);

        DrawDetail(b, tree, hit, clicked);
        DrawFlourish(b);
        if (DevDustDebug) DrawDebug(b);
    }

    /// <summary>
    /// THE POINT COUNTER: the spine's glyph, the number you can spend, and what the number is — on a
    /// frame, in the house's quiet brown. See <see cref="PointPlate"/> for why it is this shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The GLYPH is the screen's own — <c>nav_prestige</c>, the star the TRAITS tile in the rail wears —
    /// so the readout is visibly part of this screen rather than a stray badge. The four road emblems
    /// were the obvious first pick and are all wrong: a point buys on ANY road, so wearing one road's
    /// mark takes a side. The spine's was tried and is wrong for a different reason — it is a tall thin
    /// vertebra, which at 32 px is a gold splinter nobody can name. It greys out with the number when
    /// there is nothing to spend, so the plate answers "can I buy anything" before a word of it is read.
    /// </para>
    /// <para>
    /// The caption is the line the screen already carried, unchanged: TRAIT POINTS TO SPEND. It says
    /// what the number is and what to do with it in five plain words, which is the whole job.
    /// </para>
    /// </remarks>
    private void DrawPointPlate(SpriteBatch b, MemoryDustTree tree)
    {
        _ui.PanelQuiet(b, PointPlate);

        var has = tree.Available > 0;
        var ink = has ? Gold : Slate;
        // THE HOUSE GRID, not a hand-measured centre: the glyph starts at ContentLeft, the number ends at
        // ContentRight, and both sit on the plate's vertical middle — the same rule every other panel uses.
        // The glyph and the number are ONE group, centred in the frame's interior: the plate is a fixed
        // width so the tour's spotlight and this drawing cannot disagree, and a one-digit number pinned
        // to ContentRight left the group hanging off the frame's right ornament.
        var num = $"{tree.Available}";
        var numW = _ui.MeasureBig(num, UiTypography.PrimaryValue);
        const int gap = 14;
        var group = PointGlyph + gap + numW;
        var x = PointPlate.X + (PointPlate.Width - group) / 2;
        var glyph = new Rectangle(x, PointPlate.Y + (PointPlate.Height - PointGlyph) / 2, PointGlyph, PointGlyph);
        if (!_ui.Icon(b, "nav_prestige", glyph, has ? Color.White : Slate)) _ui.Diamond(b, glyph, ink);
        _ui.TextBig(b, num, glyph.Right + gap,
                    PointPlate.Y + (PointPlate.Height - UiTypography.PrimaryValue) / 2 - 2,
                    ink, UiTypography.PrimaryValue);
    }

    /// <summary>
    /// What taking a trait looks like: a flash, a shockwave, a burst, and the name of what you just
    /// became — over the whole screen, because the whole screen is what changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drawn LAST and over everything. The decision it is celebrating is irreversible, so it is allowed
    /// to interrupt; it also never blocks input, because a player who wants to keep spending should not
    /// be made to wait out an animation they have already seen.
    /// </para>
    /// <para>
    /// Every beat is a slice of one normalised clock, so retiming the whole thing is one constant.
    /// A terminal ends a road and gets the long version: its own emblem, a third ring, and a second
    /// line naming the road that just finished.
    /// </para>
    /// </remarks>
    private void DrawFlourish(SpriteBatch b)
    {
        if (_litT < 0f) return;

        var p = Math.Clamp(_litT / LitDuration, 0f, 1f);
        var accent = _litTerminal ? Gold : RoadColor(_litRoad);
        var centre = new Point(960, 486);

        // EVERY fade below is `colour * float`, never `new Color(r, g, b, someByte)`.
        //
        // This batch blends PREMULTIPLIED, so a colour's RGB must already be scaled by its own alpha.
        // Building one component-wise leaves the RGB at full strength, and the blend then reads it as
        // "add all of this gold, keep most of what was underneath" — the first capture of this flourish
        // came out as a solid amber sheet with the entire screen visible faintly through it. MonoGame's
        // Color*float scales all four components, which is exactly the premultiplied form.
        //
        // 1. THE PAGE STANDS BACK. Ramped in over a tenth of a second and out over the last quarter, so
        // the celebration owns the screen while it runs instead of competing with fifty-one nodes for
        // the player's eye.
        var hold = Math.Min(1f, p / 0.10f) * Math.Min(1f, (1f - p) / 0.25f);
        _ui.Fill(b, UiKit.OverlayScrim,
                 new Color(0x06, 0x04, 0x0A) * (hold * (_litTerminal ? 0.66f : 0.55f)));

        // 2. THE FLASH. Short and bright: it is the frame in which the decision landed.
        if (p < 0.14f)
        {
            var f = 1f - p / 0.14f;
            _ui.Fill(b, UiKit.OverlayScrim, accent * (f * f * 0.55f));
        }

        // 3. THE SHOCKWAVES. Plotted, not scaled from a sprite — see build_spec.py, where the generated
        // ring was abandoned. A 256px texture blown up to 900 across is softest exactly when it is
        // biggest; a plotted circumference is one crisp band at any radius.
        var waves = _litTerminal ? 3 : 2;
        for (var i = 0; i < waves; i++)
        {
            var lead = p - i * 0.10f;
            if (lead <= 0f || lead >= 1f) continue;
            var eased = 1f - (1f - lead) * (1f - lead);            // fast out, settling
            var radius = (int)(60 + eased * (_litTerminal ? 940 : 620));
            var fade = (1f - lead) * (1f - lead) * 0.9f;
            Ring(b, centre, radius, Math.Max(2, (int)(9 * (1f - lead))), accent * fade);
        }

        // 4. THE BURST, twice: a wide slow one for the glare and a tight fast one for the spark.
        if (_ui.Assets.Get("vfx_trait_burst") is { } burst)
            for (var i = 0; i < 2; i++)
            {
                var lead = p / (i == 0 ? 1f : 0.55f);
                if (lead >= 1f) continue;
                var size = (int)((_litTerminal ? 560 : 380) * (i == 0 ? 0.5f + lead * 1.9f : 0.3f + lead * 0.9f));
                var fade = (1f - lead) * (1f - lead) * (i == 0 ? 0.75f : 1f);
                b.Draw(burst, new Rectangle(centre.X - size / 2, centre.Y - size / 2, size, size), accent * fade);
            }

        // 5. THE FACE. A terminal shows its own emblem; every other trait shows the ROAD it just moved
        // you along — which is the thing that actually changed, and the reason a burst alone was not
        // enough. Either way the picture swells out of the flare and settles behind the name, so the
        // celebration has a subject rather than being light with a caption under it.
        var faceKey = _litTerminal ? _litArt : RoadGlyph(_litRoad);
        if (faceKey is { } key && _ui.Assets.Get(key) is { } art)
        {
            var grow = Math.Min(1f, p / 0.30f);
            var eased = 1f - (1f - grow) * (1f - grow) * (1f - grow);
            var size = (int)(_litTerminal ? 150 + eased * 250 : 90 + eased * 130);
            var fade = Math.Min(1f, (1f - p) * 3.4f);
            _ui.SpriteFit(b, art, new Rectangle(centre.X - size / 2, centre.Y - size / 2 - 40, size, size),
                          (_litTerminal ? Color.White : accent) * fade);
        }

        // 6. THE NAME. It arrives after the flash rather than under it, rises as it settles, and holds
        // legible for most of the run — this is the only part a player actually has to READ.
        if (p < 0.08f) return;
        var t = (p - 0.08f) / 0.92f;
        var rise = (int)((1f - Math.Min(1f, t * 4f)) * 46f);
        var alpha = Math.Min(1f, t * 6f) * Math.Min(1f, (1f - t) * 5f);
        if (alpha <= 0.01f) return;

        var y = (_litTerminal ? 706 : 626) + rise;
        // Edge to edge, so its two rules read as a banner the page is showing rather than a box dropped
        // on it.
        var plate = new Rectangle(0, y - 26, 1920, _litTerminal ? 154 : 114);
        _ui.Fill(b, plate, new Color(0x0A, 0x08, 0x10) * (alpha * 0.86f));
        _ui.Fill(b, new Rectangle(plate.X, plate.Y, plate.Width, 3), accent * alpha);
        _ui.Fill(b, new Rectangle(plate.X, plate.Bottom - 3, plate.Width, 3), accent * alpha);

        _ui.TextCenterBig(b, _litTerminal ? "A ROAD ENDS HERE" : "TRAIT LEARNED — AND IT IS PERMANENT",
                          centre.X, plate.Y + 16, accent * alpha, UiTypography.Secondary);
        _ui.TextCenterBig(b, _litName, centre.X, plate.Y + 48, Bone * alpha, UiTypography.ScreenTitle, TextFace.Display);
        if (_litTerminal)
            _ui.TextCenterBig(b, RoadName(_litRoad) + "  ·  WALKED TO ITS END", centre.X, plate.Y + 106,
                              Gold * alpha, UiTypography.Body);
    }

    /// <summary>
    /// A hollow circle of small squares. The shockwave the generator would not draw.
    /// </summary>
    /// <remarks>
    /// Step count follows the radius so the band stays continuous as it expands — a fixed step count
    /// draws a solid ring when it is small and a dotted one when it is large, which is precisely
    /// backwards from what an expanding shockwave should do.
    /// </remarks>
    private void Ring(SpriteBatch b, Point c, int radius, int thick, Color col)
    {
        var steps = Math.Clamp(radius * 4, 48, 1600);
        for (var i = 0; i < steps; i++)
        {
            var a = i / (float)steps * MathF.Tau;
            var x = c.X + (int)(MathF.Cos(a) * radius);
            var y = c.Y + (int)(MathF.Sin(a) * radius);
            if (x < -thick || x > 1920 + thick || y < -thick || y > 1080 + thick) continue;
            _ui.Fill(b, new Rectangle(x - thick / 2, y - thick / 2, thick, thick), col);
        }
    }

    /// <summary>The scissor state the tree's batch runs under. One instance; the device keeps it.</summary>
    private RasterizerState? _clip;
    private RasterizerState Clip => _clip ??= new RasterizerState { ScissorTestEnable = true };

    /// <summary>
    /// THE TREE, as a diagram in a free world: a spine below a ground line and four roads climbing out
    /// of it, seen through the camera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything in here is CLIPPED to <see cref="View"/>. The host opened the overlay batch for us; we
    /// close it, run the tree under a scissor rectangle in the same transform, and reopen an unclipped
    /// batch for the strip and the panel. Without the clip a zoomed-in crown would be drawn straight
    /// across the TRAIT POINTS line, and the panel — drawn later — would cover a node's right half and
    /// leave its left half showing, which reads as a bug rather than as an edge.
    /// </para>
    /// <para>
    /// Edges are the REAL prerequisites, drawn gold once both ends are lit, so a walked road reads as
    /// one continuous line. The spine hangs downward and the roads climb upward from the exact node
    /// that gates them, which is how the diagram shows that the spine is not optional.
    /// </para>
    /// <para>
    /// EVERY NODE PRINTS ITS NAME, and the name now SAYS WHAT THE NODE DOES — "HARDER HITS I", not
    /// "SHARP EDGE" (playtest, 2026-08-25). Names sit under their node on a dark plate, in up to three
    /// short lines, sized in world units so they grow with the zoom and drop out below
    /// <see cref="LabelZoom"/>.
    /// </para>
    /// </remarks>
    private void DrawTree(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        b.End();
        var canvas = Game1.OverlayToCanvas(View, Shake);
        _ui.Device.ScissorRectangle = Rectangle.Intersect(canvas, new Rectangle(0, 0, 1920, 1080));
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Clip, null, Game1.OverlayTransform(Shake));
        try
        {
            DrawWorld(b, tree, hit, clicked);
        }
        finally
        {
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Game1.OverlayTransform(Shake));
        }

        // How to move, in words, in the canvas's empty bottom-left corner — the one thing a free canvas
        // has to say that a framed one did not. Screen space, over the clipped tree. Two SHORT lines:
        // one long line ran into the spine's caption at the foot, and at the head it ran into the RUIN
        // header — both measured on a capture, not guessed.
        _ui.TextBig(b, "DRAG TO MOVE  ·  SCROLL TO ZOOM", View.X + 12, View.Bottom - 46, Slate * 0.9f, UiTypography.Secondary);
        _ui.TextBig(b, "HOME KEY SHOWS THE WHOLE TREE", View.X + 12, View.Bottom - 24, Slate * 0.9f, UiTypography.Secondary);
    }

    private void DrawWorld(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        // THE GROUND LINE. The diagram is drawn as the tree it is named for: four boughs above this
        // line, roots below it. Drawn first, so every wire and node sits on top — soil, not structure.
        var groundY = (int)ToScreen(new Vector2(0, -TraitTreeLayout.ChainStep / 2f)).Y;
        _ui.Fill(b, new Rectangle(View.X, groundY - 1, View.Width, 2), Teal * 0.30f);

        // ONE EDGE PER NODE, to its NEAREST prerequisite — the rule the mastery tree arrived at, and
        // for the identical reason. A MARK — NO EFFECT requires one node from the head of all four
        // roads plus a socket; drawing all five made it a spider whose legs crossed every other chain.
        //
        // A prerequisite list is an "all of these" rule, and the DETAIL PANEL states it exactly. The
        // drawing's job is the SHAPE — which chain a node hangs off — and one line says that better
        // than five. A taken prerequisite wins ties, so a walked road reads as one continuous gold run.
        var thick = Math.Clamp((int)MathF.Round(5f * _zoom), 2, 9);
        foreach (var u in tree.All)
        {
            if (u.Requires.Count == 0) continue;
            var to = Pos(u.Id);

            string? nearest = null;
            var best = float.MaxValue;
            foreach (var reqId in u.Requires)
            {
                // The owned-prerequisite bias exists so a WALKED road picks its gold continuation.
                // It only applies once this node is owned too: on an unowned node the wire can never
                // be gold, and the bias just dragged the wire across the diagram to whichever distant
                // node happened to be lit.
                var d = Vector2.DistanceSquared(Pos(reqId), to)
                        - (tree.Owns(u.Id) && tree.Owns(reqId) ? 1e9f : 0f);
                if (d >= best) continue;
                best = d;
                nearest = reqId;
            }
            if (nearest is null) continue;

            var lit = tree.Owns(u.Id) && tree.Owns(nearest);
            Line(b, ToScreen(Pos(nearest)), ToScreen(to),
                 lit ? Gold : RoadColor(u.Road) * 0.42f, lit ? thick + 1 : thick);
        }

        var labels = _zoom > LabelZoom;
        if (labels) DrawRoadHeaders(b, tree);
        if (labels) DrawSpineCaption(b, tree);

        // Names first, then nodes, then their cost tags: a plate never covers a node, and a tag sits
        // over whatever wire runs under it.
        if (labels) foreach (var u in tree.All) DrawName(b, tree, u);
        foreach (var u in tree.All) DrawNode(b, tree, u, hit, clicked, labels);
    }

    /// <summary>
    /// The four road HEADERS, above their crowns, in world space — glyph, name, the road's one-line
    /// identity, and its tally.
    /// </summary>
    /// <remarks>
    /// The identity line is the playtest's ask: "make the split between the roads much sharper". A
    /// road used to be a name and a colour; now RUIN says "Hit harder. Live closer to death." under its
    /// name, on the diagram, before a single node is read. The sentences are <see cref="TraitRoads"/>'s.
    /// </remarks>
    private void DrawRoadHeaders(SpriteBatch b, MemoryDustTree tree)
    {
        var heads = new[]
            {
                (TraitRoad.Ruin, "ks_reaper"), (TraitRoad.Aegis, "ks_titan"),
                (TraitRoad.Artifice, "ks_weaver"), (TraitRoad.Avarice, "ks_hoarder"),
            }
            .Where(h => tree.All.Any(u => u.Id == h.Item2))
            .Select(h => (h.Item1, At: ToScreen(Pos(h.Item2))))
            .OrderBy(h => h.At.X)
            .ToList();

        // THE COLUMN, MEASURED. The four summaries are centred on their crowns, and each label is
        // shortened to the actual gap between neighbouring roads, so a longer sentence shortens instead
        // of colliding with the next road's.
        var column = heads.Count < 2
            ? Px(3 * TraitTreeLayout.LaneStep)
            : (int)Enumerable.Range(1, heads.Count - 1).Min(i => heads[i].At.X - heads[i - 1].At.X) - Px(24);

        var namePx = Px(RoadNameWorldPx);
        var linePx = Px(RoadLineWorldPx);
        var glyphPx = Px(32);
        foreach (var (road, p) in heads)
        {
            if (p.X < View.X - column || p.X > View.Right + column) continue;
            var nodes = tree.All.Where(u => u.Road == road).ToList();
            var lit = nodes.Count(u => tree.Owns(u.Id));
            var col = lit > 0 ? RoadColor(road) : Muted(RoadColor(road), 0.72f);
            var top = (int)p.Y - Px(RadiusOf(SocketKind.Terminal));

            // Bottom-up from the crown: tally, identity, then name and glyph.
            var tallyY = top - Px(34);
            var identityY = top - Px(64);
            var nameY = top - Px(108);

            var name = _ui.ShortenBig(RoadName(road), column - glyphPx - Px(8), namePx);
            var nameW = _ui.MeasureBig(name, namePx);
            var x0 = (int)p.X - (nameW + glyphPx + Px(8)) / 2;
            _ui.Icon(b, RoadGlyph(road), new Rectangle(x0, nameY + (Px(34) - glyphPx) / 2, glyphPx, glyphPx), col);
            _ui.TextBig(b, name, x0 + glyphPx + Px(8), nameY, col, namePx);

            _ui.TextCenterBig(b, _ui.ShortenBig(TraitRoads.Sentence(road), column, linePx),
                              (int)p.X, identityY, Bone * 0.92f, linePx);
            _ui.TextCenterBig(b,
                _ui.ShortenBig($"{lit} OF {nodes.Count}  ·  {nodes.Sum(u => u.Cost)} POINTS", column, linePx),
                (int)p.X, tallyY, lit > 0 ? Gold : Slate, linePx);
        }
    }

    /// <summary>The roots' caption, under the deepest root — the spine named the way the roads are named above.</summary>
    private void DrawSpineCaption(SpriteBatch b, MemoryDustTree tree)
    {
        var spine = tree.All.Where(u => u.Road == TraitRoad.Spine).ToList();
        if (spine.Count == 0) return;
        var spineLit = spine.Count(u => tree.Owns(u.Id));
        var (min, max) = TraitTreeLayout.Bounds();
        var at = ToScreen(new Vector2((min.X + max.X) / 2f, max.Y + CaptionRoom - 34f));
        var px = Px(26);
        _ui.TextCenterBig(b,
            _ui.ShortenBig($"{TraitRoads.Name(TraitRoad.Spine)} — {TraitRoads.Sentence(TraitRoad.Spine)}  ·  {spineLit} OF {spine.Count} LEARNED",
                           Px(max.X - min.X + TraitTreeLayout.NodeWidth), px),
            (int)at.X, (int)at.Y, spineLit > 0 ? Teal : Muted(Teal, 0.72f), px);
    }

    // ── TYPE INSIDE THE TREE CANVAS ───────────────────────────────────────────────────────────────
    //
    // These are WORLD sizes: everything on the tree is multiplied by the camera zoom before it is
    // drawn, so a rung from UiTypography — which is measured in final screen pixels — would be the
    // wrong unit here and would change size as the player scrolls. They are named all the same, so a
    // change to the tree's type is a decision somebody made rather than a literal somebody typed, and
    // so tools/check_ui_type.py can tell the two apart.

    /// <summary>The size the names under the nodes are drawn at, in world units.</summary>
    private const int NamePx = 28;   // ui-size-ok: tree WORLD units, multiplied by the camera zoom

    /// <summary>A road's own name at the head of its branch, in world units.</summary>
    private const int RoadNameWorldPx = 32;   // ui-size-ok: tree WORLD units, multiplied by the zoom

    /// <summary>A road's sentence and tally under its name, in world units.</summary>
    private const int RoadLineWorldPx = 24;   // ui-size-ok: tree WORLD units, multiplied by the zoom

    /// <summary>The cost tag on an unlit node, in world units.</summary>
    private const int NodeCostWorldPx = 19;   // ui-size-ok: tree WORLD units, multiplied by the zoom
    private const int NameLineH = 30;
    private const int NameGap = 4;

    /// <summary>The widest a name line may be, in world units — the plate has to fit the layout's lane.</summary>
    private const int NameWidth = (int)TraitTreeLayout.NodeWidth - 12;

    /// <summary>The name under a node: up to three short lines on a dark plate, sized to the lane.</summary>
    private void DrawName(SpriteBatch b, MemoryDustTree tree, MemoryDustUnlock u)
    {
        var p = ToScreen(Pos(u.Id));
        if (!Near(p)) return;
        var r = Px(RadiusOf(KindOf(tree, u)));
        var lines = NameLines(u.Name);
        if (lines.Count == 0) return;

        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var hot = u.Id == _selectedId || u.Id == _hoverId;
        var ink = isOwned ? Gold : buyable || hot ? Bone : Slate;

        var px = Px(NamePx);
        var lineH = Px(NameLineH);
        var w = lines.Max(l => _ui.MeasureBig(l, px));
        var top = (int)p.Y + r + Px(NameGap);
        _ui.Fill(b, new Rectangle((int)p.X - w / 2 - 4, top - 1, w + 8, lines.Count * lineH + 2),
                 Plate * (hot ? 0.95f : 0.82f));
        for (var i = 0; i < lines.Count; i++)
            _ui.TextCenterBig(b, lines[i], (int)p.X, top + i * lineH, ink, px);
    }

    /// <summary>
    /// A name as at most three lines that fit a lane.
    /// </summary>
    /// <remarks>
    /// Balanced, not greedy: "MORE AND RARER LOOT" greedy-packed is "MORE AND RARER" over "LOOT", and
    /// the first line then has to be cut. Of the splits where every line fits, the one with the fewest
    /// lines wins, then the one whose longest line is shortest; if no split fits, the least-bad two-line
    /// one is taken and each line is shortened with an ellipsis — the detail panel always carries the
    /// full name. Breaks after a hyphen as well as at a space, for AUTO-SELL. Measured in WORLD units at
    /// the name's world size, so the split is the same at every zoom.
    /// </remarks>
    private IReadOnlyList<string> NameLines(string name)
    {
        if (_nameLines.TryGetValue(name, out var cached)) return cached;

        var tokens = new List<string>();
        foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var cut = word.IndexOf('-');
            if (cut > 0 && cut < word.Length - 1) { tokens.Add(word[..(cut + 1)]); tokens.Add(word[(cut + 1)..]); }
            else tokens.Add(word);
        }

        string Join(IEnumerable<string> ts)
        {
            var s = "";
            foreach (var t in ts) s = s.Length == 0 || s.EndsWith('-') ? s + t : s + " " + t;
            return s;
        }
        int Width(string s) => _ui.MeasureBig(s, NamePx);

        List<string> result;
        var whole = Join(tokens);
        if (tokens.Count <= 1 || Width(whole) <= NameWidth) result = new() { whole };
        else
        {
            // Two lines, then three: the fewest lines that fit.
            (List<string> Lines, int Over, int Max)? best = null;
            void Consider(params IEnumerable<string>[] parts)
            {
                var lines = parts.Select(Join).ToList();
                var widths = lines.Select(Width).ToList();
                var over = widths.Sum(w => Math.Max(0, w - NameWidth));
                var max = widths.Max();
                if (best is null || over < best.Value.Over || (over == best.Value.Over && max < best.Value.Max))
                    best = (lines, over, max);
            }
            for (var i = 1; i < tokens.Count; i++) Consider(tokens.Take(i), tokens.Skip(i));
            if (best!.Value.Over > 0 && tokens.Count >= 3)
            {
                var two = best;
                best = null;
                for (var i = 1; i < tokens.Count - 1; i++)
                    for (var j = i + 1; j < tokens.Count; j++)
                        Consider(tokens.Take(i), tokens.Skip(i).Take(j - i), tokens.Skip(j));
                if (best!.Value.Over > 0) best = two;   // three lines did not fit either: prefer two, shortened
            }
            result = best!.Value.Lines.Select(l => _ui.ShortenBig(l, NameWidth, NamePx)).ToList();
        }

        _nameLines[name] = result;
        return result;
    }

    private readonly Dictionary<string, IReadOnlyList<string>> _nameLines = new();

    /// <summary>Is a screen point close enough to the view to be worth drawing? Cheap culling before the scissor.</summary>
    private bool Near(Vector2 p)
        => p.X > View.X - 400 && p.X < View.Right + 400 && p.Y > View.Y - 400 && p.Y < View.Bottom + 400;

    /// <summary>One node: a framed socket whose glyph is its road and whose brightness is its state.</summary>
    /// <remarks>
    /// The mastery tree's three channels, one fact each: the FRAME's shape is the kind (crown, rung,
    /// spur, gate, minor, mark), the GLYPH inside is the road it serves, and the tint on both is the
    /// state. Taken burns gold over a lit field; affordable wears its road's colour and breathes a
    /// slow halo; locked is dimmed with <see cref="Muted"/> — darker, never translucent, because a
    /// locked trait should look unbought, not absent.
    /// </remarks>
    private void DrawNode(SpriteBatch b, MemoryDustTree tree, MemoryDustUnlock u, Point hit, bool clicked, bool labels)
    {
        var p = ToScreen(Pos(u.Id));
        if (!Near(p)) return;
        var kind = KindOf(tree, u);
        var r = Px(RadiusOf(kind));
        var box = new Rectangle((int)p.X - r, (int)p.Y - r, r * 2, r * 2);

        // The name plate is part of the node for the pointer: a player aims at the word as readily as
        // at the frame, and a hit box that stops at the frame makes the plate feel broken — so the
        // hit box is the UNION of the frame and the plate, the plate measured the way DrawName draws it.
        // The hit box is in SCREEN space, built from the same camera the drawing used, so a zoomed or
        // panned tree is hit exactly where it is drawn.
        var hitBox = box;
        if (labels)
        {
            var lines = NameLines(u.Name);
            var px = Px(NamePx);
            var plateW = lines.Count == 0 ? 0 : lines.Max(l => _ui.MeasureBig(l, px)) + 8;
            var left = Math.Min(box.X - 4, (int)p.X - plateW / 2);
            var right = Math.Max(box.Right + 4, (int)p.X - plateW / 2 + plateW);
            hitBox = new Rectangle(left, box.Y, right - left, box.Height + Px(NameGap) + lines.Count * Px(NameLineH) + 2);
        }
        var hover = View.Contains(hit) && hitBox.Contains(hit);

        if (clicked && hover) { _selectedId = u.Id; _msg = ""; }
        if (hover) _hoverId = u.Id;

        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var road = RoadColor(u.Road);

        // AFFORDABLE INVITES: a slow breathing halo, the same gesture the mastery tree uses for its
        // unchosen specialisations. Quiet on purpose — an invitation, not an alarm.
        if (buyable && !isOwned)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(_time * 2.6f);
            for (var ring = 2; ring >= 1; ring--)
            {
                var g = (int)(box.Width * 0.10f * ring + pulse * 4f * _zoom);
                _ui.Diamond(b, new Rectangle(box.X - g, box.Y - g, box.Width + g * 2, box.Height + g * 2),
                            road * (0.05f + 0.09f * pulse));
            }
        }

        DrawFace(b, tree, u, box, hover || u.Id == _selectedId);

        // The cost, as a small tag on the node's right edge — over whatever wire runs under it.
        // Unlit only: a bought trait's price is history.
        if (!isOwned && labels)
        {
            var tagW = Px(26);
            var tagH = Px(22);
            var tag = new Rectangle(box.Right - tagW / 4, box.Center.Y - tagH / 2, tagW, tagH);
            _ui.Fill(b, tag, Plate * 0.92f);
            Outline(b, tag, buyable ? road : Dim, 1);
            _ui.TextCenterBig(b, $"{u.Cost}", tag.Center.X, tag.Y + 1, buyable ? Bone : Slate, Px(NodeCostWorldPx));
        }
    }

    /// <summary>
    /// The face a node shows anywhere it is drawn — field, frame and glyph, in the mastery tree's
    /// dress. Shared by the diagram and the detail panel, so a trait looks like ITSELF in both.
    /// </summary>
    private void DrawFace(SpriteBatch b, MemoryDustTree tree, MemoryDustUnlock u, Rectangle box, bool hot)
    {
        var kind = KindOf(tree, u);
        var road = RoadColor(u.Road);
        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);

        var frame = _ui.Assets.Get(FrameKey(kind));
        var frameTint = hot ? Bone : isOwned ? Gold : buyable ? road : Muted(road, 0.55f);
        var fieldCol = isOwned ? Muted(road, 0.92f)
                       : buyable ? Muted(road, 0.36f)
                       : new Color(0x16, 0x12, 0x20);

        if (frame is null)
        {
            // No art on disk: the flat shapes this screen shipped with. The game must run with an
            // empty assets/art, so the greybox path stays alive rather than borrowing other art.
            _ui.Fill(b, box, fieldCol);
            Outline(b, box, frameTint, hot || isOwned ? 3 : 2);
        }
        else
        {
            // THE FIELD FOLLOWS THE FRAME'S SHAPE — the lesson the mastery tree learned: a square
            // patch inside a ring reads as a square wearing a ring. Hex behind the octagon, diamond
            // behind the diamond, a plain fill behind the chain-square, and a grown diamond whose
            // points reach the rim behind every ring.
            var pad = (int)(box.Width * 0.24f);
            var field = new Rectangle(box.X + pad, box.Y + pad, box.Width - pad * 2, box.Height - pad * 2);
            if (kind == SocketKind.Rung) _ui.Hex(b, field, fieldCol);
            else if (kind == SocketKind.Spur) _ui.Diamond(b, field, fieldCol);
            else if (kind == SocketKind.Gate) _ui.Fill(b, field, fieldCol);
            else _ui.Diamond(b, Circleish(field), fieldCol);
            b.Draw(frame, box, frameTint);
        }

        if (TerminalArt(u) is { } art)
        {
            // A crown wears its own emblem, inside the ornate ring, untinted while it can matter.
            var inset = (int)(box.Width * 0.15f);
            _ui.Icon(b, art, new Rectangle(box.X + inset, box.Y + inset,
                                           box.Width - inset * 2, box.Height - inset * 2),
                     isOwned || buyable ? Color.White : Muted(Color.White, 0.5f));
        }
        else
        {
            // Every other node wears its ROAD's glyph: dark on a lit field once taken, road-coloured
            // while affordable, and legible grey — not faded — while locked.
            var g = (int)(box.Width * (kind == SocketKind.Spur ? 0.40f
                                       : kind == SocketKind.Minor ? 0.56f : 0.50f));
            _ui.Icon(b, RoadGlyph(u.Road),
                     new Rectangle(box.Center.X - g / 2, box.Center.Y - g / 2, g, g),
                     isOwned ? new Color(0x14, 0x11, 0x1E) : buyable ? road : LockedInk);
        }
    }

    /// <summary>A circle-filling diamond: grown past its box so its four points reach the ring.</summary>
    private static Rectangle Circleish(Rectangle r)
    {
        var grow = (int)MathF.Round(r.Width * 0.14f);
        return new Rectangle(r.X - grow, r.Y - grow, r.Width + grow * 2, r.Height + grow * 2);
    }

    /// <summary>
    /// Where a node is, in world units — for EVERY id, including one the layout never heard of.
    /// </summary>
    /// <remarks>
    /// The layout covers the whole catalogue by construction (see <see cref="TraitTreeLayout"/>), so
    /// this is the fallback for a custom tree — a fixture, a future catalogue the layout predates. It
    /// parks the stranger along the bottom row, visibly, instead of returning early: the CHARGE spur's
    /// four keystones were invisible for as long as they existed because the old DrawNode did exactly
    /// that, and the header counted them all the while.
    /// </remarks>
    private Vector2 Pos(string id)
    {
        if (TraitTreeLayout.Positions.TryGetValue(id, out var v)) return Xna(v);
        if (_strays.TryGetValue(id, out var s)) return s;
        var (_, max) = TraitTreeLayout.Bounds();
        var stray = new Vector2(max.X + TraitTreeLayout.LaneStep * (_strays.Count + 1), max.Y);
        _strays[id] = stray;
        return stray;
    }

    private readonly Dictionary<string, Vector2> _strays = new();

    /// <summary>The layout speaks System.Numerics (it lives in Core, engine-free); the screen speaks XNA.</summary>
    private static Vector2 Xna(System.Numerics.Vector2 v) => new(v.X, v.Y);

    /// <summary>A wire between two nodes. Long ones are DASHED — see remarks.</summary>
    /// <remarks>
    /// A road segment joins neighbours; A MARK — NO EFFECT wants one node from the head of every road, so
    /// its edge crosses a third of the diagram whichever prerequisite is nearest. Drawn solid, at any
    /// alpha, a line that long reads as structure — as though the two roads it cuts between were joined —
    /// and the eye follows it instead of the roads. Dashed, it reads as what it is: a dependency that
    /// lives somewhere else. The detail panel still states the full prerequisite list exactly.
    /// </remarks>
    private void Line(SpriteBatch b, Vector2 a, Vector2 c, Color col, int thick)
    {
        // Wholly outside the view on one side: nothing to draw, and no loop to run at a high zoom.
        if ((a.X < View.X && c.X < View.X) || (a.X > View.Right && c.X > View.Right)
            || (a.Y < View.Y && c.Y < View.Y) || (a.Y > View.Bottom && c.Y > View.Bottom)) return;

        var dx = c.X - a.X;
        var dy = c.Y - a.Y;
        var steps = (int)MathF.Max(MathF.Abs(dx), MathF.Abs(dy));
        if (steps <= 0) return;

        // Measured against the diagram's own rung, so it follows the layout rather than a screen size:
        // anything more than twice a normal step is a wire that has left its neighbourhood.
        var longRun = steps > (int)(TraitTreeLayout.ChainStep * _zoom * 2.2f);
        var dash = Math.Max(4, Px(16));
        for (var i = 0; i <= steps; i++)
        {
            if (longRun && (i / dash) % 2 == 1) continue;
            _ui.Fill(b, new Rectangle((int)(a.X + dx * i / steps) - thick / 2,
                                      (int)(a.Y + dy * i / steps) - thick / 2, thick, thick), col);
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
    /// The detail panel: one trait, read properly — its road and what that road is for, its picture,
    /// its name, and then the sheet in the order a player decides in: what it does (with the real
    /// numbers), what it costs, what it needs first, and that it is permanent.
    /// </summary>
    /// <remarks>
    /// The text comes from <see cref="MemoryDustText.Describe"/>, which composes the hand-written
    /// description with the keystone's own blurb; the cost, prerequisite and permanence lines are the
    /// same facts <see cref="MemoryDustText.Sheet"/> prints, in the same order, so the sheet a test
    /// holds and the one the player reads are the same words. Nothing is printed that the player
    /// cannot act on: the old "kind" line ("A small permanent boost", "Opens more of the game") is
    /// gone, and the reminder that learning is not wearing became the description's own words.
    /// </remarks>
    private void DrawDetail(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, DetailPanel);
        // Hover reads, click PINS — the lesson the mastery tree's node panel learned. A player cannot
        // read a node and then look at the tree if reading requires keeping the pointer still.
        var u = (_hoverId is not null ? tree.All.FirstOrDefault(x => x.Id == _hoverId) : null)
                ?? Selected(tree);
        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);

        var terminal = TerminalArt(u);
        var roadCol = RoadColor(u.Road);
        var left = UiKit.ContentLeft(DetailPanel);
        var right = UiKit.ContentRight(DetailPanel);
        var width = DetailPanel.Width - UiKit.PadX(DetailPanel) * 2;

        _ui.TextCenterBig(b, "TRAIT DETAIL", DetailPanel.Center.X, UiKit.TitleTop(DetailPanel), Gold, UiTypography.PanelTitle);

        // Everything below runs off ONE CURSOR, so adding a row moves the rows under it rather than
        // running one through the next.
        var y = UiKit.CaptionTop(DetailPanel);

        // THE ROAD, named and drawn, and under it what walking it means — the same sentence the header
        // on the diagram prints, so the panel and the picture agree.
        var badge = new Rectangle(left, y, 26, 26);
        var hasGlyph = _ui.Icon(b, RoadGlyph(u.Road), badge, roadCol);
        _ui.TextBig(b, RoadName(u.Road), hasGlyph ? badge.Right + 10 : left, y + 4, roadCol, UiTypography.Body);
        y += 30;
        _ui.TextBig(b, _ui.ShortenBig(TraitRoads.Sentence(u.Road), width, UiTypography.Secondary),
                    left, y, Slate, UiTypography.Secondary);
        y += 28;

        // The node's own face — the same frame, field and glyph it wears on the diagram, drawn large.
        var size = terminal is not null ? 100 : 80;
        var icon = new Rectangle(DetailPanel.Center.X - size / 2, y, size, size);
        DrawFace(b, tree, u, icon, hot: false);
        y += size + 4;

        _ui.TextCenterBig(b, _ui.ShortenBig(u.Name, width, UiTypography.Headline), DetailPanel.Center.X, y,
                          isOwned ? Gold : buyable ? Bone : Slate, UiTypography.Headline);
        y += 32;
        // The state in a word — and only the state. The "kind" that used to sit beside it was a label
        // about the engine's categories, not a fact a player could do anything with.
        var state = isOwned ? "LEARNED" : buyable ? "AVAILABLE NOW" : "LOCKED";
        _ui.TextCenterBig(b, state, DetailPanel.Center.X, y, isOwned ? Gold : buyable ? Met : Slate, UiTypography.Secondary);
        y += 28;

        // ── 1. WHAT IT DOES — at Body size, wrapped to the measured width, and never allowed to run
        //    into the blocks under it: the text gets as many lines as leave the cost and the
        //    prerequisites their room, and ends with an ellipsis past that (a long keystone blurb is
        //    the only thing that could get there).
        const int lineH = 23;
        var floor = DetailPanel.Bottom - 176;   // the permanence line and the button own the panel below this
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim * 0.7f);
        y += 12;
        _ui.TextBig(b, "WHAT IT DOES", left, y, Slate, UiTypography.Secondary);
        y += 26;
        var reserve = (isOwned ? 44 : 66)                                    // the cost block
                      + 40 + 32 * Math.Max(1, Math.Min(u.Requires.Count, 3));  // and the prerequisite block's minimum
        var lines = _ui.WrapBig(MemoryDustText.Describe(u), width, UiTypography.Body);
        var room = Math.Max(1, (floor - reserve - y) / lineH);
        for (var i = 0; i < Math.Min(lines.Count, room); i++)
        {
            var text = i == room - 1 && lines.Count > room ? _ui.ShortenBig(lines[i] + " …", width, UiTypography.Body) : lines[i];
            _ui.TextBig(b, text, left, y, Bone, UiTypography.Body);
            y += lineH;
        }
        y += 6;

        // ── 2. WHAT IT COSTS — priced in TRAIT POINTS and it says so, beside how many you have. The
        //    whole word, not "PTS"; and here, under what it does, rather than parked at the foot of
        //    the panel a screen away from the decision it belongs to.
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim * 0.7f);
        y += 12;
        _ui.TextBig(b, "WHAT IT COSTS", left, y, Slate, UiTypography.Secondary);
        if (isOwned) _ui.TextRightBig(b, "LEARNED", right, y - 4, Gold, UiTypography.Headline);
        else
            _ui.TextRightBig(b, $"{u.Cost} TRAIT {(u.Cost == 1 ? "POINT" : "POINTS")}", right, y - 4,
                             buyable ? Bone : Ember, UiTypography.Headline);
        y += 26;
        if (!isOwned)
        {
            _ui.TextBig(b, $"You have {tree.Available} trait {(tree.Available == 1 ? "point" : "points")}.", left, y,
                        tree.Available >= u.Cost ? Slate : Ember, UiTypography.Secondary);
            y += 22;
        }
        y += 6;

        // ── 3. YOU NEED FIRST — the real Requires, each with a tick once you have it.
        _ui.Fill(b, new Rectangle(left, y, width, 2), Dim * 0.7f);
        y += 12;
        _ui.TextBig(b, "YOU NEED FIRST", left, y, Gold, UiTypography.Secondary);
        y += 28;
        // The permanence line below is anchored to the panel's bottom, so the list has a hard floor.
        // Say what was dropped rather than drawing a row through the divider — a silently truncated
        // list reads as "these are all the prerequisites", which is the one thing it must never say.
        if (u.Requires.Count == 0) _ui.TextBig(b, "Nothing. You can start here.", left + 2, y, Slate, UiTypography.Body);
        else
            for (var i = 0; i < u.Requires.Count; i++)
            {
                if (y > floor)
                {
                    _ui.TextBig(b, $"and {u.Requires.Count - i} more — hover them on the tree", left + 2, y, Slate, UiTypography.Secondary);
                    break;
                }
                var reqId = u.Requires[i];
                var req = tree.All.FirstOrDefault(x => x.Id == reqId);
                var got = tree.Owns(reqId);
                DrawTick(b, new Rectangle(left + 2, y + 2, 20, 18), got);
                _ui.TextBig(b, _ui.ShortenBig(req?.Name ?? reqId, width - 110, UiTypography.Body), left + 32, y, got ? Bone : Slate, UiTypography.Body);
                _ui.TextRightBig(b, got ? "learned" : "not yet", right, y + 3, got ? Met : Slate, UiTypography.Secondary);
                y += 32;
            }

        // ── 4. PERMANENT. NEVER RESETS. — on every node, in gold, the last thing read before the
        //    button. The one fact the whole tree rests on, and the one a new player most needs.
        _ui.Fill(b, new Rectangle(left, DetailPanel.Bottom - 166, width, 2), Dim * 0.7f);
        _ui.TextCenterBig(b, MemoryDustText.Permanence, DetailPanel.Center.X, DetailPanel.Bottom - 152, Gold * 0.85f, UiTypography.Body);

        // Every refusal this screen has — already taken, not enough points, prerequisites unlit — is
        // said on the page that produced it, above the button that produced it. A fresh refusal from
        // the button takes the line; otherwise the standing reason the node cannot be bought.
        var reason = _msg.Length > 0 ? _msg
                   : isOwned || buyable ? ""
                   : tree.Available < u.Cost ? "NOT ENOUGH TRAIT POINTS" : "LEARN WHAT IT NEEDS FIRST";
        if (reason.Length > 0)
            _ui.TextCenterBig(b, _ui.ShortenBig(reason, width, UiTypography.Secondary), DetailPanel.Center.X, DetailPanel.Bottom - 120, Ember, UiTypography.Secondary);

        var label = isOwned ? "LEARNED" : "LEARN THIS TRAIT";
        if (_ui.Button(b, new Rectangle(left, DetailPanel.Bottom - 92, width, 68), label, hit, clicked, enabled: buyable))
            Buy(tree, u);
    }

    private void DrawTick(SpriteBatch b, Rectangle r, bool on)
    {
        if (on) { for (var k = 0; k < 5; k++) _ui.Fill(b, new Rectangle(r.X + k, r.Bottom - 6 + k / 2 - 2, 3, 3), Met); for (var k = 0; k < 10; k++) _ui.Fill(b, new Rectangle(r.X + 5 + k, r.Bottom - 2 - k, 3, 3), Met); }
        else _ui.Fill(b, new Rectangle(r.X + 2, r.Center.Y - 2, r.Width - 4, 4), Slate);
    }

    private void DrawDebug(SpriteBatch b)
    {
        foreach (var r in new[] { View, DetailPanel })
        {
            _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), Ember);
            _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), Ember);
            _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), Ember);
        }
        _ui.TextBig(b, $"nav TRAITS  sel {_selectedId}  zoom {_zoom:0.000} (home {_homeZoom:0.000})  pan {_pan.X:0},{_pan.Y:0}",
                    434, 112, Gold, UiTypography.Secondary);
        // Which nodes the layout had to place by itself — a to-do list, drawn where a developer looks.
        if (TraitTreeLayout.Unauthored.Count > 0 || _strays.Count > 0)
            _ui.TextBig(b, $"auto-placed: {string.Join(", ", TraitTreeLayout.Unauthored.Concat(_strays.Keys))}",
                        View.X + 20, View.Bottom - 48, Ember, UiTypography.Secondary);
    }
}
