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
/// read one road at a time. Same camera as <c>MasteryScreen</c> — pan is the world point at the view's
/// centre, zoom is screen pixels per world unit, and the hit-testing goes through the same transform
/// as the drawing so the two can never disagree.
/// </para>
/// </remarks>
public sealed class TraitsScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    // 0x5E5A6E, up from 0x3A3A44. Dim is the LOCKED outline and the divider rule, and at the old value a
    // locked node on the 0xC0-alpha scrim was a square of near-black on near-black — the playtest said
    // the tree was hard to read, and forty of its fifty-one nodes are locked at any given career. A
    // locked trait should look unbought, not absent.
    private static readonly Color Dim = UiInk.Rule;
    /// <summary>The tint of a locked node's icon — lighter than Dim, so the glyph still reads as a glyph.</summary>
    private static readonly Color LockedInk = new(0x84, 0x7E, 0x96);
    /// <summary>The dark plate a name sits on, so a wire running behind it never runs through it.</summary>
    private static readonly Color Plate = new(0x0A, 0x08, 0x10);
    private static readonly Color Met = UiInk.Good;
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

    // ── The purchase pulse (UI polish §31, §73–§82: "connection lights, short pulse") ─────────────
    //
    // The flourish above is the celebration; this is the DIAGRAM's own answer. A purchase lights one
    // wire — from the prerequisite the node hangs off to the node itself — and a band of light runs
    // along it over UiMotion.Reward and lands on the node as a short glow. Once per purchase, keyed to
    // the node taken, fired from Buy and read by Draw; Reduced Motion drops the travel and simply
    // lights the wire with a fade (brief §32: static highlight, simple fade).
    private string? _pulseFrom;            // the prerequisite the lit wire runs from
    private string? _pulseTo;              // the node just taken
    private float? _pulsePosed;            // DEV: hold the pulse at this progress (0 fired → 1 done)

    /// <summary>The purchase pulse's key: one per node, so a second purchase never re-arms the first's.</summary>
    private static int PulseKey(string id) => HashCode.Combine("traits", "wire", id);

    /// <summary>
    /// A cue that must not talk over a trait being taken: the purchase cues are set by <see cref="BeginLit"/>
    /// and win the frame; a navigation tick or a refusal only fills an empty slot.
    /// </summary>
    private void Cue(string cue)
    {
        if (_cue is null) _cue = cue;
    }

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
    /// <remarks>Same shape as TrainingScreen.ConsumeTrain, so the host's Update reads one way everywhere.</remarks>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    public bool DevDustDebug { get; set; }

    public TraitsScreen(UiKit ui, MemoryDustTree tree)
    {
        _ui = ui;
        _selectedId = tree.All.OrderBy(u => u.Cost).First().Id;
        // Every node this tree holds must have a home in the diagram. The layout covers the catalogue by
        // construction; this is the loud version for a debug build, so a tree the layout has never seen
        // is noticed at the desk rather than in a capture — and Pos() still draws it either way.
        System.Diagnostics.Debug.Assert(TraitTreeLayout.Covers(tree.All.Select(u => u.Id)),
            "TraitTreeLayout does not cover every node of this tree — see TraitTreeLayout.Unauthored / TraitsScreen.Pos");
        // The drawing sizes below must fit the room the layout promises, or the layout's no-overlap
        // test is holding a promise the screen breaks.
        System.Diagnostics.Debug.Assert(NameWidth + 8 <= TraitTreeLayout.NodeWidth
                                        && RadiusOf(SocketKind.Terminal) * 2 + NameGap + 3 * NameLineH + 2 <= TraitTreeLayout.NodeHeight,
            "TraitsScreen draws nodes larger than TraitTreeLayout.NodeWidth/NodeHeight allow");

        (_worldMin, _worldMax) = WorldExtent();
        _homeZoom = FitZoom();
        _homePan = (_worldMin + _worldMax) / 2f;
        // FIRST OPEN FRAMES THE SPINE AND THE ROAD STARTS (UX V2 P1.6, brief §43): the whole tree at minimum
        // zoom is a shape, not a list — names were 9 px at 720p. Home still shows the whole tree.
        _zoom = Math.Clamp(_homeZoom * 1.55f, MinZoom, MaxZoom);
        var spineHead = tree.All.Where(u => u.Road == TraitRoad.Spine).OrderBy(u => u.Cost).FirstOrDefault();
        _pan = spineHead is not null ? Pos(spineHead.Id) : _homePan;
        ClampPan();
    }

    /// <summary>Frame one road: its nodes and its header, fitted to the view (brief §44) — the camera glides there.</summary>
    private void FrameRoad(MemoryDustTree tree, TraitRoad road)
    {
        var nodes = tree.All.Where(u => u.Road == road).Select(u => Pos(u.Id)).ToList();
        if (nodes.Count == 0) return;
        var min = new Vector2(nodes.Min(p => p.X) - TraitTreeLayout.NodeWidth, nodes.Min(p => p.Y) - HeaderRoom - 40f);
        var max = new Vector2(nodes.Max(p => p.X) + TraitTreeLayout.NodeWidth, nodes.Max(p => p.Y) + TraitTreeLayout.NodeHeight);
        var span = Vector2.Max(max - min, new Vector2(1f));
        GlideTo((min + max) / 2f, MathF.Min(View.Width / span.X, View.Height / span.Y) * 0.92f);
    }

    // ── The camera's glide (UI polish §31 transition, §73–§82 "smooth road focus") ─────────────
    //
    // A road's name, the keys 1–4, HOME and ALL used to CUT: one frame the spine, the next frame a road,
    // and the eye had to find where it was. The camera now eases pan and zoom together over
    // UiMotion.Transition — one smoothstep for both, so the picture swoops rather than sliding and then
    // zooming. The END STATE IS THE SAME as the cut was: the glide's target is exactly the framing
    // FrameRoad computed, and under Reduced Motion UiMotion.Ease answers with the target on its first
    // ask, which is the old jump. The first-open framing in the constructor is untouched. Advanced from
    // Update (the eased value is asked once a frame there), never from Draw; any hand on the camera —
    // a drag, the wheel, the arrows, +/- — cancels a glide in flight, because the player's hand wins.
    private Vector2 _glideFromPan, _glideToPan;
    private float _glideFromZoom, _glideToZoom;
    private bool _gliding;
    private float? _glidePosed;            // DEV: hold the glide at this eased progress (RH_SHOT_TRAITS_GLIDE)
    private bool _drawnOnce;               // the foot band is measured by the first Draw; a fit before it is wrong at 150 %
    private bool _devGlideApplied;
    private static readonly int GlideKey = HashCode.Combine("traits", "glide");

    /// <summary>The one door to a framing the player ASKED for: a road, HOME, ALL. Glides there, or jumps under Reduced Motion.</summary>
    private void GlideTo(Vector2 pan, float zoom)
    {
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        pan = new Vector2(Math.Clamp(pan.X, _worldMin.X, _worldMax.X), Math.Clamp(pan.Y, _worldMin.Y, _worldMax.Y));
        if (UiMotion.Reduced)
        {
            _pan = pan;
            _zoom = zoom;
            _gliding = false;
            return;
        }
        _glideFromPan = _pan;
        _glideFromZoom = _zoom;
        _glideToPan = pan;
        _glideToZoom = zoom;
        _gliding = true;
        // A zero-length ease to 0 forgets the key, so the next ask starts the fade from the beginning
        // rather than from a glide that settled at 1 last time.
        UiMotion.Ease(GlideKey, 0f, 0f);
    }

    /// <summary>One frame of the glide — the eased value asked once, both axes moved by it, the target landed on exactly.</summary>
    private void TickGlide()
    {
        if (!_gliding) return;
        var t = _glidePosed ?? UiMotion.Ease(GlideKey, 1f, UiMotion.Transition);
        (_pan, _zoom) = GlideAt(_glideFromPan, _glideFromZoom, _glideToPan, _glideToZoom, t);
        if (t >= 1f && _glidePosed is null) _gliding = false;
    }

    /// <summary>The player's hand on the camera: whatever glide was running stops where it is.</summary>
    private void CancelGlide() => _gliding = false;

    /// <summary>
    /// The camera part-way through a glide: pan and zoom interpolated by the same (already eased)
    /// progress, and the endpoints returned EXACTLY at 0 and 1, so a finished glide is the target
    /// framing to the bit rather than a lerp's rounding of it.
    /// </summary>
    public static (Vector2 Pan, float Zoom) GlideAt(Vector2 fromPan, float fromZoom, Vector2 toPan, float toZoom, float t)
    {
        if (t <= 0f) return (fromPan, fromZoom);
        if (t >= 1f) return (toPan, toZoom);
        return (Vector2.Lerp(fromPan, toPan, t), MathHelper.Lerp(fromZoom, toZoom, t));
    }

    /// <summary>
    /// DEV ONLY — the glide is the one camera state a still cannot catch by accident. When
    /// <c>RH_SHOT_TRAITS_GLIDE=&lt;road|all&gt;[@&lt;0..1&gt;]</c> is set (any TRAITS capture mode; the rig
    /// inherits it, like RH_SHOT_PAGE_MOUSE), the first Update after the first Draw starts a glide from
    /// the posed framing to that road (or to HOME for <c>all</c>) and HOLDS it at that eased progress
    /// (0.5 when unnamed) for the shutter — the same freeze the flourish uses. <c>@1</c> photographs
    /// the landing, which must be the exact picture a cut to the road used to give.
    /// </summary>
    private void ApplyDevGlide(MemoryDustTree tree)
    {
        if (_devGlideApplied || !_drawnOnce) return;
        _devGlideApplied = true;
        if (Environment.GetEnvironmentVariable("RH_SHOT_TRAITS_GLIDE") is not { Length: > 0 } spec) return;
        var at = spec.IndexOf('@');
        var target = at > 0 ? spec[..at] : spec;
        var posed = at > 0 && float.TryParse(spec[(at + 1)..], System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out var f)
            ? Math.Clamp(f, 0f, 1f) : 0.5f;
        // FrameRoad and GlideTo move the camera and nothing else — the navigation tick is set by the
        // hand that asked (a key, a header, the ALL button), so a posed camera is silent by construction
        // rather than by wiping a cue back out afterwards.
        if (Enum.TryParse<TraitRoad>(target, true, out var road)) FrameRoad(tree, road);
        else GlideTo(_homePan, _homeZoom);
        if (_gliding) _glidePosed = posed;
    }

    /// <summary>
    /// DEV ONLY — the two capture dials that belong to the whole page rather than to this screen's own
    /// state: <c>RH_SHOT_REDUCED=1</c> turns Reduced Motion on, and <c>RH_SHOT_HELD=1</c> holds the left
    /// mouse button down so a PRESSED control can be photographed.
    /// </summary>
    /// <remarks>
    /// Both settings live in the host — Reduced Motion in the player's preferences, the held button on the
    /// real mouse — and the host reapplies both every frame BEFORE any screen updates; this screen's Update
    /// runs after that, so writing them here (every frame, not once) is what makes two states a photograph
    /// rather than a claim: the accessibility end state (a camera that jumps, a wire that simply lights)
    /// and the pressed face of a custom-drawn button. Off unless the variable is set, so neither can reach
    /// a player's session. <c>RH_SHOT_HELD</c> touches only the DRAWN state — a drag reads the host's own
    /// held flag, and a click is still an edge, so nothing is bought or moved by posing it.
    /// </remarks>
    private static void ApplyDevCaptureDials()
    {
        if (Set("RH_SHOT_REDUCED")) UiMotion.Reduced = true;
        if (Set("RH_SHOT_HELD")) UiKit.MouseHeld = true;

        static bool Set(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } and not "0";
    }

    /// <summary>The road headers as last drawn, in screen space — click one to frame its road.</summary>
    private readonly Dictionary<TraitRoad, Rectangle> _headerRects = new();

    /// <summary>The terminal armed for a two-step COMMIT (brief §46), or null.</summary>
    private string? _armedId;
    private string? _tip;
    private Point _tipAt;

    // ── The inspector's sheet: a wheel-scrolled region of whole rows (UI polish §17–§18) ─────────
    /// <summary>The first row of the sheet on show. Reset whenever the selection changes.</summary>
    private int _sheetFirst;

    /// <summary>
    /// How many rows the sheet's LAST page holds, and how many rows there are: total − page is the
    /// furthest the sheet scrolls — what the wheel's clamp (<see cref="UiKit.Scrolled"/>) and the
    /// scrollbar read. Both are what the last Draw measured.
    /// </summary>
    private int _sheetPage, _sheetTotal;

    /// <summary>The sheet's row heights, measured every Draw — the scroll clamp's input. Reused, never reallocated.</summary>
    private readonly List<int> _sheetRows = new();

    /// <summary>The camera buttons' edge — a hit target, so it follows the profile (brief §107).</summary>
    private static int CamBtnSize => UiMetrics.Control(44);

    /// <summary>The camera's three buttons in the foot band, under the canvas: zoom out, the whole tree, zoom in.</summary>
    private static Rectangle CamBtn(int i)
        => new(View.Right - UiMetrics.Gap - (3 - i) * (CamBtnSize + UiMetrics.Space(6)),
               UiKit.PageBottom(PageBottomInset) - UiMetrics.Gap - CamBtnSize, CamBtnSize, CamBtnSize);

    /// <summary>
    /// DEV ONLY: pose the detail panel on a specific trait for the screenshot fixture. An "@N" suffix
    /// (RH_SHOT_NODE=ks_bloodlust@3) opens the sheet scrolled to its Nth row — the one state of this
    /// panel no other dial can pose, and the one the 125 % and 150 % profiles put in front of every player.
    /// </summary>
    public void DevSelect(string spec)
    {
        var (id, first) = DevSpec(spec);
        _selectedId = id;
        _sheetFirst = first;
    }

    /// <summary>The trait id and the sheet row a fixture spec names: "id", or "id@row".</summary>
    private static (string Id, int First) DevSpec(string spec)
    {
        var at = spec.IndexOf('@');
        if (at > 0 && int.TryParse(spec[(at + 1)..], out var first)) return (spec[..at], Math.Max(0, first));
        return (spec, 0);
    }

    /// <summary>DEV ONLY: park the camera at a zoom, centred on a node, so a capture can prove the zoomed view.</summary>
    public void DevCamera(float zoom, string centreId)
    {
        _gliding = false;
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _pan = Pos(DevSpec(centreId).Id);
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
        // THE MOMENT AFTER THE PURCHASE, not a flourish over an unbought node. The pose used to leave the
        // tree untouched, so the banner said TRAIT LEARNED over a node the inspector called LOCKED and a
        // wire that was not lit — and the purchase pulse runs along a LIT wire, which a still of an unlit
        // one could never show. Bought when the fixture can afford it; otherwise granted with what it
        // needs, the way a save restores a career (the terminal pose costs twelve the fixture never has).
        if (!tree.Owns(id) && !tree.Purchase(id))
            tree.Restore(tree.MemoryDust, tree.OwnedIds.Concat(u.Requires).Append(id));
        BeginLit(tree, u);
        _litT = t;
        // FROZEN, or the fixture is useless: the capture rig renders sixty frames before it saves, so
        // an un-frozen pose advances a full second and every mode would screenshot the same empty
        // moment after the flourish had already finished.
        _litFrozen = true;
        // THE PULSE IS HELD TOO, at its own instant. Its clock is the flourish's first UiMotion.Reward
        // seconds, so the flourish's own pose time answers for it by default; RH_SHOT_TRAITS_PULSE=<0..1>
        // moves it alone, because the beat that reads best for the flourish (0.30 s, past the flash) and
        // the beat that reads best for the band on the wire (about a third of the way across) are not the
        // same beat and one dial cannot hold both.
        _pulsePosed = Math.Clamp(t / UiMotion.Reward, 0f, 1f);
        if (float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_TRAITS_PULSE"),
                           System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var posedPulse))
            _pulsePosed = Math.Clamp(posedPulse, 0f, 1f);
        // AND THE CAMERA CAN BE PARKED ON THE WIRE — opt-in, so the signed-off `traitlit` framing is the
        // one this fixture has always given. RH_SHOT_TRAITS_WIRE=<zoom> centres the camera on the wire the
        // pulse runs along and zooms to it: at the home framing the whole tree is on screen and that wire
        // is forty pixels long, which photographs a band nobody can see. The flourish is drawn in screen
        // space and does not care where the tree is, so only the diagram behind it moves.
        if (_pulseFrom is { } from
            && float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_TRAITS_WIRE"),
                              System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out var wireZoom))
        {
            _gliding = false;
            _zoom = Math.Clamp(wireZoom, MinZoom, MaxZoom);
            _pan = (Pos(from) + Pos(id)) / 2f;
            ClampPan();
        }
        TickFlourish(0f);   // settle Shake for the posed instant, so the kick is in the picture too
    }

    // ── The page ────────────────────────────────────────────────────────────────────────────────
    // No frame around the tree any more: the whole canvas left of the detail panel and under the top
    // strip is the tree's, edge to edge, and the camera decides what part of the world it shows. The
    // detail panel stays docked on the right.

    // THE PAGE'S MARGINS are anchors, unscaled: the strip's top, the canvas's left edge, the right
    // margin the top pills leave and the bottom margin. Everything INSIDE them — the strip's rows, the
    // gap to the inspector, the inspector's width — follows the density profile through UiMetrics, so
    // the canvas and the panel begin where the bigger subtitle actually ends rather than at a 136 that
    // assumed 100 % type (brief §8–§9).
    private const int StripTop = 24;
    private const int PageLeft = 40;
    private const int PageRightInset = 52;
    private const int PageBottomInset = 10;

    /// <summary>The gold rule under the screen's title: one screen-title line below it. 74 at 100 %.</summary>
    private static int RuleY => StripTop + UiTypography.Pitch(UiTypography.ScreenTitle) + 1;

    /// <summary>The subtitle's row, just under the rule. 80 at 100 %.</summary>
    private static int SubtitleY => RuleY + UiMetrics.Space(6);

    /// <summary>
    /// Where the canvas and the inspector begin: one subtitle line and a breath under the subtitle
    /// (136 at 100 %) — or a breath under the point plate, whichever is lower. The plate grows with
    /// the profile faster than the header band does; at 150 % it reached into the canvas and sat on
    /// the top-left road's names (the C6 capture), so the canvas now starts where the plate ends.
    /// </summary>
    private static int CanvasTop
        => Math.Max(SubtitleY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(32),
                    PlateTop + PlateHeight + UiMetrics.Space(10));

    /// <summary>The point plate's top edge and height — shared by <see cref="PointPlate"/> and <see cref="CanvasTop"/>, so neither can drift from the other.</summary>
    private static int PlateTop => RuleY + UiMetrics.Space(2);
    private static int PlateHeight => UiMetrics.Space(22) * 2 + PointGlyph;

    /// <summary>The tree's canvas: everything left of the detail panel, below the top strip.</summary>
    // The canvas ends where the inspector begins and follows the page, so a smaller page (UI SCALE) does
    // not leave the tree drawing underneath the panel.
    private static Rectangle View
        => new(PageLeft, CanvasTop, DetailPanel.X - UiMetrics.Space(28) - PageLeft, FootTop - CanvasTop);

    // ── The foot band ────────────────────────────────────────────────────────────────────────────
    //
    // The camera hint, the tally and the three camera buttons used to be printed in screen space OVER
    // the clipped tree, in "the canvas's empty bottom-left corner" — empty at 100 % with the tree at
    // home, and not empty at all once a player pans, or at 150 %, where the wrapped hint and the tally's
    // own row printed straight through the deepest nodes' names (the 150 % capture of the merged
    // reflow). Brief §17 forbids text over text and a control under a footer, so the foot is now a BAND
    // BELOW THE VIEW: the tree's clip ends where the band begins, and the band's height follows how
    // many rows the foot needs at the current profile — measured every Draw, not guessed.

    /// <summary>
    /// Rows of text the foot holds at the current profile: 1 while the hint and the tally share the
    /// buttons' row; the hint's lines plus a tally row once they stack. Static because <see cref="View"/>
    /// is static (the tour reads it); the last Draw's measurement, 1 until the first.
    /// </summary>
    private static int _footRows = 1;

    /// <summary>The band's height: the buttons' row, plus whatever the stacked rows rise above it, plus a breath.</summary>
    private static int FootBand
        => UiMetrics.Gap + CamBtnSize
           + Math.Max(0, (_footRows - 1) * UiTypography.Pitch(UiTypography.Secondary) - UiMetrics.Space(16))
           + UiMetrics.Space(10);

    /// <summary>Where the band begins — the view's bottom edge, and the tree's clip.</summary>
    private static int FootTop => UiKit.PageBottom(PageBottomInset) - FootBand;

    /// <summary>
    /// The docked reading panel, on the page's right edge — the house inspector width
    /// (<see cref="UiMetrics.InspectorWidth"/>: 496 at 100 %, wider at the larger profiles so the bigger
    /// type keeps its line length, brief §9).
    /// </summary>
    private static Rectangle DetailPanel
    {
        get
        {
            var w = UiMetrics.InspectorWidth(UiKit.Page.Width);
            return new(UiKit.PageRight(PageRightInset) - w, CanvasTop, w, UiKit.PageBottom(PageBottomInset) - CanvasTop);
        }
    }

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
    /// <b>In the header band, above <see cref="View"/>.</b> The tree is drawn FIRST and clipped to
    /// its canvas, so anything drawn after it covers it; the plate therefore lives entirely in the header
    /// band — under the title rule, left of the centred subtitle, above the canvas — and never eats a
    /// road. Its width is FIXED rather than measured so the caption does not shuffle sideways as the
    /// number goes from one digit to two; the number is right-aligned into its own column instead.
    /// </para>
    /// </remarks>
    // Just the glyph and the number. The caption beside them ("TRAIT POINTS TO SPEND") made the plate a
    // 360-px ribbon around a one- or two-digit number (playtest 2026-08-30: "the frame is far too long
    // and thin — delete the text, dress the frame around the points and the icon only"). The size comes
    // from the house grid: the glyph's box plus a fixed shoulder each side, and a height that clears the
    // glyph with a pad above and below. A PROPERTY, not a static readonly: it follows the profile, and a
    // static readonly is frozen at class load.
    private static Rectangle PointPlate
        => new(View.X, PlateTop, UiMetrics.Space(58) * 2 + PointGlyph, PlateHeight);

    /// <summary>The plate's glyph box — shared by the draw and the tour. An icon beside a number: the house icon size.</summary>
    private static int PointGlyph => UiMetrics.IconSize;

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
    private float _homeZoom;               // the framing HOME returns to: the whole tree, fitted — refitted when the foot band changes
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
        ApplyDevCaptureDials();
        TickFlourish(dt);
        _time += dt;
        ApplyDevGlide(tree);
        TickGlide();

        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var over = mouse;

        // ── THE CAMERA — the mastery tree's, verbatim in spirit. ──────────────────────────────
        if (wheel != 0 && View.Contains(over))
        {
            CancelGlide();
            ZoomAt(over, wheel > 0 ? 1.16f : 1f / 1.16f);
        }
        // Over the inspector the wheel scrolls the sheet instead (brief §18) — a row a notch, clamped so
        // the last page stays full. The page and the total are what the last Draw measured.
        else if (wheel != 0 && DetailPanel.Contains(over))
            _sheetFirst = UiKit.Scrolled(_sheetFirst, wheel, _sheetPage, _sheetTotal);

        // DRAG TO PAN. Held, not clicked: a click pins a node for the panel, and a tree you can only
        // move with a scrollbar is a tree nobody moves. A drag that began inside the view keeps
        // panning even when the pointer leaves it.
        if (held && (_dragFrom is not null || View.Contains(over)))
        {
            if (_dragFrom is null) { _dragFrom = over; _dragPanFrom = _pan; }
            else
            {
                var d = _dragFrom.Value;
                CancelGlide();
                _pan = _dragPanFrom + new Vector2((d.X - over.X) / _zoom, (d.Y - over.Y) / _zoom);
                ClampPan();
            }
        }
        else _dragFrom = null;

        // Keyboard, for anyone who would rather not drag. Arrows pan (they used to step the selection
        // through the list, which a free canvas has no use for), +/- zoom about the centre, Home resets.
        var step = 260f / _zoom * 0.06f;
        if (keys.IsKeyDown(Keys.Left)) { CancelGlide(); _pan.X -= step; ClampPan(); }
        if (keys.IsKeyDown(Keys.Right)) { CancelGlide(); _pan.X += step; ClampPan(); }
        if (keys.IsKeyDown(Keys.Up)) { CancelGlide(); _pan.Y -= step; ClampPan(); }
        if (keys.IsKeyDown(Keys.Down)) { CancelGlide(); _pan.Y += step; ClampPan(); }
        if (Pressed(keys, Keys.OemPlus) || Pressed(keys, Keys.Add)) { CancelGlide(); ZoomAt(View.Center, 1.25f); }
        if (Pressed(keys, Keys.OemMinus) || Pressed(keys, Keys.Subtract)) { CancelGlide(); ZoomAt(View.Center, 1f / 1.25f); }
        if (Pressed(keys, Keys.Home)) { GlideTo(_homePan, _homeZoom); Cue("sfx_nav"); }

        // 1–4 frame a road; each is a navigation tick, the same sound the rail makes (brief §86).
        if (Pressed(keys, Keys.D1)) { FrameRoad(tree, TraitRoad.Ruin); Cue("sfx_nav"); }
        if (Pressed(keys, Keys.D2)) { FrameRoad(tree, TraitRoad.Aegis); Cue("sfx_nav"); }
        if (Pressed(keys, Keys.D3)) { FrameRoad(tree, TraitRoad.Artifice); Cue("sfx_nav"); }
        if (Pressed(keys, Keys.D4)) { FrameRoad(tree, TraitRoad.Avarice); Cue("sfx_nav"); }

        if (Pressed(keys, Keys.Enter)) TryBuy(tree, Selected(tree));

        _prevKeys = keys;
    }

    /// <summary>
    /// The one door to a purchase. A minor permanent node buys on one press — no modal spam; a TERMINAL
    /// (a road's crown, a permanent identity choice) arms on the first press and buys on the second (brief §46).
    /// </summary>
    private void TryBuy(MemoryDustTree tree, MemoryDustUnlock u)
    {
        var terminal = TerminalArt(u) is not null;
        if (terminal && !tree.Owns(u.Id) && tree.CanUnlock(u.Id) && _armedId != u.Id)
        {
            _armedId = u.Id;
            _msg = $"THIS IS A PERMANENT IDENTITY CHOICE. PRESS AGAIN TO COMMIT TO {RoadName(u.Road)}.";
            Cue("sfx_click");   // armed, not taken: the dry click, not the ritual
            return;
        }
        _armedId = null;
        Buy(tree, u);
    }

    private void Buy(MemoryDustTree tree, MemoryDustUnlock u)
    {
        if (tree.Owns(u.Id)) _msg = "ALREADY LEARNED — A TRAIT IS PERMANENT.";
        else if (tree.Purchase(u.Id)) { _msg = $"LEARNED: {u.Name}."; BeginLit(tree, u); return; }
        else if (tree.Available < u.Cost) _msg = "NOT ENOUGH TRAIT POINTS.";
        else _msg = "LOCKED — LEARN WHAT IT NEEDS FIRST.";
        // A refusal sounds like one (brief §86: locked / error, dull) — the reason beside the button says why.
        Cue("sfx_error");
    }

    /// <summary>Arm the flourish, and the pulse along the wire, for a trait that was just bought.</summary>
    private void BeginLit(MemoryDustTree tree, MemoryDustUnlock u)
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
        // THE WIRE THAT LIT: the same prerequisite the diagram draws the node's edge to, so the light
        // runs along a line that is there. A root with nothing before it has no wire to run.
        _pulseTo = u.Id;
        _pulseFrom = NearestPrerequisite(tree, u);
        _pulsePosed = null;
        UiMotion.Flash(PulseKey(u.Id), UiMotion.Reward);
    }

    /// <summary>
    /// Where the purchase pulse is at progress <paramref name="s"/> (0 just fired → 1 done): the band's head
    /// and tail along the wire (0 = the prerequisite's rim, 1 = the node's rim), and the arrival glow on
    /// the node. The band takes the first 70 % of the pulse to cross, eased with the house curve; the
    /// glow pops on as it lands and fades over the rest — all inside UiMotion.Reward.
    /// </summary>
    public static (float Head, float Tail, float Glow) WirePulseAt(float s)
    {
        const float travel = 0.70f;   // the band's share of the pulse; the rest is the landing
        const float band = 0.30f;     // the band's length, as a share of the wire
        s = Math.Clamp(s, 0f, 1f);
        var head = UiMotion.Smooth(Math.Clamp(s / travel, 0f, 1f));
        var tail = Math.Max(0f, head - band);
        var glow = s < travel ? 0f : 1f - Math.Clamp((s - travel) / (1f - travel), 0f, 1f);
        return (head, tail, glow);
    }

    /// <summary>The pulse's progress this frame (0 fired → 1 done), or null when no purchase is pulsing.</summary>
    private float? PulseProgress()
    {
        if (_pulseTo is null) return null;
        if (_pulsePosed is { } posed) return posed;
        var key = PulseKey(_pulseTo);
        return UiMotion.Pulsing(key) ? 1f - UiMotion.Pulse(key) : null;
    }

    /// <summary>
    /// The prerequisite a node's ONE drawn edge runs to — its nearest, with a taken prerequisite winning
    /// once the node itself is taken, so a walked road reads as one continuous gold run. The diagram and
    /// the purchase pulse ask the same question here, so the light can only run along a wire that is drawn.
    /// </summary>
    private string? NearestPrerequisite(MemoryDustTree tree, MemoryDustUnlock u)
    {
        if (u.Requires.Count == 0) return null;
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
        return nearest;
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
        var hit = mouse;

        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xC0));

        // THE HOVER TIP IS CLEARED BEFORE ANYTHING DRAWS, not half-way down the method. It used to be
        // reset just above the point plate — AFTER the tree — so every tip the tree set (a node's name
        // and cost, a road header's "click to frame this road") was written and thrown away one call
        // later, and no hover on the canvas had shown a tip since. The camera buttons' DISABLED reason
        // (brief §29: a disabled control says why, in one plain line) goes through the same one slot and
        // would have been born dead in exactly the same way.
        _tip = null;

        // THE TREE FIRST, clipped to its canvas, so the strip and the panel are drawn over it rather
        // than a zoomed-in node being drawn over them.
        _hoverId = null;
        DrawTree(b, tree, hit, clicked);
        _drawnOnce = true;

        // ── THE TOP STRIP ──
        // TRAITS, not DUST. The screen stopped spending Memory Dust when the tree stopped being buyable
        // by idling; a title naming a currency it does not charge is the kind of small lie that makes a
        // player mistrust every other number on the screen.
        _ui.TextCenterBig(b, "TRAITS", UiKit.PageCenterX, StripTop, UiInk.Accent, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 240, RuleY, 480, 3), Gold * 0.5f);
        // "NO TAKING BACK", not "NO RESPEC" — the reader plays in English as a second language, and
        // "respec" is a word only the genre knows.
        // THE QUIET MARK, finally visible (P7): attunement's whole promise is "a mark", and its
        // wire (TreeComplete) had no reader — four points bought for something the player could
        // never see. A standing fact about the account belongs on the screen's own subtitle.
        if (DustEffects.TreeComplete(tree))
            _ui.TextCenterBig(b, "ONE SPINE  ·  FOUR ROADS  ·  NO TAKING BACK  ·  ALL FOUR ROADS WALKED",
                              UiKit.PageCenterX, SubtitleY, UiInk.Accent, UiTypography.Secondary);
        else
            _ui.TextCenterBig(b, "ONE SPINE  ·  FOUR ROADS  ·  NO TAKING BACK", UiKit.PageCenterX, SubtitleY, Slate, UiTypography.Secondary);

        // How many points you have to spend, how much of the tree you have learned, and what all of it
        // would cost. A header rather than a panel: a player checks "can I afford this" constantly and
        // "what does the whole tree cost" once, and neither is worth a third of the page.
        DrawPointPlate(b, tree);
        DrawDetail(b, tree, hit, clicked);
        DrawFlourish(b);
        if (_tip is { } tip) _ui.HoverTip(b, tip, _tipAt);
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
        var ink = has ? Gold : Bone;   // never the disabled grey on the one figure the screen is about
        // THE HOUSE GRID, not a hand-measured centre: the glyph starts at ContentLeft, the number ends at
        // ContentRight, and both sit on the plate's vertical middle — the same rule every other panel uses.
        // The glyph and the number are ONE group, centred in the frame's interior: the plate is a fixed
        // width so the tour's spotlight and this drawing cannot disagree, and a one-digit number pinned
        // to ContentRight left the group hanging off the frame's right ornament.
        // AVAILABLE on the left, the tally on the right (UX V2 P1.6). The "226 FOR EVERYTHING" that used to
        // stand beside it is gone: a career earns about thirty-four points, and a total the player can
        // never reach is a number without a meaning (brief §92).
        var num = $"{tree.Available}";
        var numW = _ui.MeasureBig(num, UiTypography.PrimaryValue);
        var gap = UiMetrics.Space(14);
        var plate = PointPlate;
        var group = PointGlyph + gap + numW;
        var x = plate.X + (plate.Width - group) / 2;
        var glyph = new Rectangle(x, plate.Y + (plate.Height - PointGlyph) / 2, PointGlyph, PointGlyph);
        if (!_ui.Icon(b, "nav_prestige", glyph, has ? Color.White : Bone)) _ui.Diamond(b, glyph, ink);
        _ui.TextBig(b, num, glyph.Right + gap, plate.Y + (plate.Height - UiTypography.PrimaryValue) / 2 - UiMetrics.Space(2),
                    has ? Gold : Bone, UiTypography.PrimaryValue);
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
        // on it. Its height is STACKED from the three lines it holds — the caption, the name at the
        // screen-title rung, the road line — so at 150 % the bigger name pushes the road line down
        // instead of printing through it.
        var headY = UiMetrics.Space(16);
        var nameY = headY + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(8);
        var roadY = nameY + UiTypography.Pitch(UiTypography.ScreenTitle) + UiMetrics.Space(9);
        var plateH = _litTerminal ? roadY + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(20)
                                  : nameY + UiTypography.Pitch(UiTypography.ScreenTitle) + UiMetrics.Space(17);
        var plate = new Rectangle(0, y - UiMetrics.Space(26), 1920, plateH);
        _ui.Fill(b, plate, new Color(0x0A, 0x08, 0x10) * (alpha * 0.86f));
        _ui.Fill(b, new Rectangle(plate.X, plate.Y, plate.Width, 3), accent * alpha);
        _ui.Fill(b, new Rectangle(plate.X, plate.Bottom - 3, plate.Width, 3), accent * alpha);

        _ui.TextCenterBig(b, _litTerminal ? "A ROAD ENDS HERE" : "TRAIT LEARNED — AND IT IS PERMANENT",
                          centre.X, plate.Y + headY, accent * alpha, UiTypography.Secondary);
        _ui.TextCenterBig(b, _litName, centre.X, plate.Y + nameY, Bone * alpha, UiTypography.ScreenTitle, TextFace.Display);
        if (_litTerminal)
            _ui.TextCenterBig(b, RoadName(_litRoad) + "  ·  WALKED TO ITS END", centre.X, plate.Y + roadY,
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
        var spent = tree.All.Where(u => tree.Owns(u.Id)).Sum(u => u.Cost);
        var learned = tree.All.Count(u => tree.Owns(u.Id));
        // The tally — read once a session, not before every purchase. The "226 FOR EVERYTHING" that used
        // to follow it is gone: a career earns about thirty-four points.
        var tally = $"{spent} SPENT  ·  {learned} OF {tree.All.Count} LEARNED";
        var tallyW = _ui.MeasureBig(tally, UiTypography.Secondary);
        var foot = CamBtn(0);
        var hintX = View.X + UiMetrics.Gap;
        var hintY = foot.Y + UiMetrics.Space(16);
        var tallyRight = foot.X - UiMetrics.Space(20);
        var hintPitch = UiTypography.Pitch(UiTypography.Secondary);
        var hintLine = string.Join(HintSep, HintSegments);
        var oneLine = hintX + _ui.MeasureBig(hintLine, UiTypography.Secondary) + UiMetrics.Space(20) + tallyW <= tallyRight;
        // With the tally on its own row, the hint may run right up to the buttons.
        var hintRoom = foot.X - UiMetrics.Space(20) - hintX;
        var hintLines = oneLine ? new List<string> { hintLine } : PackSegments(HintSegments, HintSep, hintRoom, UiTypography.Secondary);
        // The row count sets the band, the band sets the view, and the view is the clip — measured
        // BEFORE the clip so the tree is cut where the foot begins on this very frame. A new count
        // (first Draw, or a profile change) refits HOME so ALL still shows the whole tree.
        var rows = oneLine ? 1 : hintLines.Count + 1;
        if (rows != _footRows)
        {
            _footRows = rows;
            _homeZoom = FitZoom();
            _zoom = Math.Clamp(_zoom, MinZoom, MaxZoom);
            ClampPan();
        }

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

        // THE FOOT, in its band under the view (see FootBand): how to move, in words, and the camera's
        // controls as controls (UX V2 P1.6): zoom out · the whole tree · zoom in — and one line that says
        // what a click on a road's name does. Readable Slate, never a dimmed Slate. THE FOOT FOLLOWS THE
        // ROOM IT HAS (brief §9, §17): the hint sits on the buttons' row and the tally right-aligns beside
        // them — the 100 % foot, unchanged — for as long as one line of each fits. When the profile's type
        // no longer fits (at 150 % the hint and the tally together are wider than the canvas leaves), the
        // tally takes its own row above the hint and the hint wraps at its own separators, stacked UPWARD
        // from the same baseline: never shortened to an ellipsis, never drawn smaller, never printed
        // through the tally — and never through a node, because the band is not part of the view.
        // A hairline where the clip ends, so a node cut at the band reads as an edge, not as a bug.
        _ui.Fill(b, new Rectangle(View.X, View.Bottom, View.Width, 1), Slate * 0.28f);
        var stackTop = hintY - (hintLines.Count - 1) * hintPitch;
        for (var i = 0; i < hintLines.Count; i++)
            _ui.TextBig(b, hintLines[i], hintX, stackTop + i * hintPitch, Slate, UiTypography.Secondary);
        if (oneLine) _ui.TextRightBig(b, tally, tallyRight, hintY, Slate, UiTypography.Secondary);
        else _ui.TextRightBig(b, tally, CamBtn(2).Right, stackTop - hintPitch, Slate, UiTypography.Secondary);
        // THE CAMERA BUTTONS wear the standard states (brief §25–§29), the way UiKit.Button does, on the
        // quiet plate they always had: HOVER is a luminance lift eased in over UiMotion.Fast with the
        // hairline the button showed before; PRESSED is the face dropped 2 px and darkened for as long as
        // the mouse is held on it; DISABLED — the camera at its limit, or ALL with the whole tree already
        // in view — keeps its label readable and says why in the hover tip.
        var camLabels = new[] { "–", "ALL", "+" };
        var atHome = !_gliding && Vector2.DistanceSquared(_pan, _homePan) < 0.25f && MathF.Abs(_zoom - _homeZoom) < 1e-3f;
        for (var i = 0; i < 3; i++)
        {
            var r = CamBtn(i);
            var enabled = i == 0 ? _zoom > MinZoom + 1e-4f
                        : i == 2 ? _zoom < MaxZoom - 1e-4f
                        : !atHome;
            var over = r.Contains(hit);
            var hover = enabled && over;
            var lift = UiMotion.Ease(UiMotion.KeyOf(r), hover ? 1f : 0f);
            var pressed = hover && UiKit.MouseHeld;
            var face = pressed ? new Rectangle(r.X, r.Y + 2, r.Width, r.Height) : r;
            _ui.Plate(b, face);
            var inner = new Rectangle(face.X + 1, face.Y + 1, face.Width - 2, face.Height - 2);
            if (lift > 0f) _ui.Fill(b, inner, Color.White * (0.07f * lift));
            if (pressed) _ui.Fill(b, inner, Color.Black * 0.18f);
            if (lift > 0f) Outline(b, face, Slate * lift, 1);
            var px = i == 1 ? UiTypography.Caption : UiTypography.Body;
            var ink = !enabled ? UiInk.Disabled : hover ? Bone : Slate;
            _ui.TextCenterBig(b, camLabels[i], face.Center.X, face.Y + (face.Height - px) / 2 - UiMetrics.Space(2), ink, px);
            if (over && !enabled)
            {
                _tip = i == 0 ? "AS FAR OUT AS THE CAMERA GOES."
                     : i == 2 ? "AS CLOSE AS THE CAMERA GOES."
                     : "THE WHOLE TREE IS ALREADY IN VIEW.";
                _tipAt = hit;
            }
            if (clicked && hover)
            {
                if (i == 0) { CancelGlide(); ZoomAt(View.Center, 1f / 1.25f); Cue("sfx_click"); }
                else if (i == 2) { CancelGlide(); ZoomAt(View.Center, 1.25f); Cue("sfx_click"); }
                else { GlideTo(_homePan, _homeZoom); Cue("sfx_nav"); }
            }
        }
    }

    /// <summary>
    /// The camera hint's three clauses. One line joined by <see cref="HintSep"/> while the foot has the
    /// room; packed into as few lines as fit when it does not — a clause is never split or shortened.
    /// </summary>
    private static readonly string[] HintSegments =
        { "DRAG TO MOVE", "WHEEL TO ZOOM", "CLICK A ROAD'S NAME OR PRESS 1–4 TO FRAME IT" };

    private const string HintSep = "  ·  ";

    /// <summary>
    /// Pack clauses into lines no wider than <paramref name="room"/> at <paramref name="px"/>: greedy and
    /// in order, a clause never split. A clause wider than the room stands alone on its line rather than
    /// being cut — the room is the canvas's width, and the longest clause fits it at every profile.
    /// </summary>
    private List<string> PackSegments(IReadOnlyList<string> segments, string sep, int room, int px)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var s in segments)
        {
            var joined = cur.Length == 0 ? s : cur + sep + s;
            if (cur.Length > 0 && _ui.MeasureBig(joined, px) > room) { lines.Add(cur); cur = s; }
            else cur = joined;
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
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
            if (NearestPrerequisite(tree, u) is not { } nearest) continue;
            var lit = tree.Owns(u.Id) && tree.Owns(nearest);
            Line(b, ToScreen(Pos(nearest)), ToScreen(Pos(u.Id)),
                 lit ? Gold : RoadColor(u.Road) * 0.42f, lit ? thick + 1 : thick);
        }

        var labels = _zoom > LabelZoom;
        if (labels) DrawRoadHeaders(b, tree, hit, clicked);
        if (labels) DrawSpineCaption(b, tree);

        // Names first, then nodes, then their cost tags: a plate never covers a node, and a tag sits
        // over whatever wire runs under it.
        if (labels) foreach (var u in tree.All) DrawName(b, tree, u);
        // THE PULSE RUNS OVER THE PLATES, not under them. Drawn with the wires it was hidden for its
        // whole second half: a name sits UNDER its node, so the plate of the very node the light is
        // travelling to lies across the wire's arrival end, and the band vanished into it about
        // half-way (the 0.82 capture showed a gold wire and no band at all). It is drawn after the
        // names and before the nodes instead, so the light crosses whatever it crosses and is covered
        // only by the node it reaches — which is the light going IN, and is the point.
        DrawWirePulse(b, tree, thick);
        foreach (var u in tree.All) DrawNode(b, tree, u, hit, clicked, labels);
    }

    /// <summary>
    /// THE PURCHASE PULSE on the wire: a band of light running from the prerequisite's rim to the taken
    /// node's rim, brightest at its head, over the gold the wire already turned. Drawn after the names
    /// and before the nodes, so it crosses whatever plate is in its way and is covered only by the node
    /// it reaches. Reduced Motion: no travel — the whole wire lights and fades (brief §32).
    /// </summary>
    private void DrawWirePulse(SpriteBatch b, MemoryDustTree tree, int thick)
    {
        if (PulseProgress() is not { } s || _pulseFrom is null || _pulseTo is null) return;
        if (tree.All.FirstOrDefault(u => u.Id == _pulseTo) is not { } node) return;
        var a = ToScreen(Pos(_pulseFrom));
        var c = ToScreen(Pos(_pulseTo));
        // A prerequisite the catalogue no longer names still gets a rim — the smallest one — so the band
        // starts beside it rather than on top of it.
        var rimA = Px(tree.All.FirstOrDefault(u => u.Id == _pulseFrom) is { } prereq ? RadiusOf(KindOf(tree, prereq)) : RadiusOf(SocketKind.Minor));
        var rimC = Px(RadiusOf(KindOf(tree, node)));
        var span = c - a;
        var len = span.Length();
        if (len <= rimA + rimC + 2f) return;   // touching nodes: no wire to run along
        var dir = span / len;
        var from = a + dir * rimA;
        var to = c - dir * rimC;

        if (UiMotion.Reduced)
        {
            // The connection simply lights: one pale wire, fading out as the pulse spends itself.
            Line(b, from, to, Bone * (0.85f * (1f - s)), thick + 2);
            return;
        }

        var (head, tail, _) = WirePulseAt(s);
        if (head <= tail) return;
        // A comet: six slices from the tail to the head, each brighter than the last, so the light has
        // a direction — it is going TO the node, which is the whole sentence the pulse speaks.
        const int slices = 6;
        for (var k = 0; k < slices; k++)
        {
            var t0 = tail + (head - tail) * k / slices;
            var t1 = tail + (head - tail) * (k + 1) / slices;
            var bright = (k + 1) / (float)slices;
            Line(b, Vector2.Lerp(from, to, t0), Vector2.Lerp(from, to, t1), Bone * (0.35f + 0.65f * bright), thick + 2);
        }
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
    private void DrawRoadHeaders(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        _headerRects.Clear();
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
            // THE HEADER IS A DOOR (UX V2 P1.6, brief §44): click the road's name and the camera frames the road.
            // The slop around the header is hit padding in SCREEN pixels — it follows the profile, not the zoom.
            var slopX = UiMetrics.Space(8);
            var slopY = UiMetrics.Space(6);
            var headRect = new Rectangle(x0 - slopX, nameY - slopY, nameW + glyphPx + Px(8) + slopX * 2, Px(34) + slopY * 2);
            _headerRects[road] = headRect;
            var overHead = headRect.Contains(hit) && View.Contains(hit);
            if (overHead) { _ui.Fill(b, headRect, Bone * 0.06f); _tip = $"{RoadName(road)} — click to frame this road."; _tipAt = hit; }
            if (clicked && overHead) { FrameRoad(tree, road); Cue("sfx_nav"); }
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
    private const int NamePx = 30;   // ui-size-ok: tree WORLD units, multiplied by the camera zoom

    /// <summary>A road's own name at the head of its branch, in world units.</summary>
    private const int RoadNameWorldPx = 36;   // ui-size-ok: tree WORLD units, multiplied by the zoom

    /// <summary>A road's sentence and tally under its name, in world units.</summary>
    private const int RoadLineWorldPx = 28;   // ui-size-ok: tree WORLD units, multiplied by the zoom

    /// <summary>The cost tag on an unlit node, in world units.</summary>
    private const int NodeCostWorldPx = 24;   // ui-size-ok: tree WORLD units, multiplied by the zoom
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

        if (clicked && hover) { _selectedId = u.Id; _msg = ""; _armedId = null; _sheetFirst = 0; }
        if (hover) { _hoverId = u.Id; _tip = $"{u.Name} — {u.Cost} TRAIT POINT{(u.Cost == 1 ? "" : "S")}"; _tipAt = hit; }

        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var road = RoadColor(u.Road);

        // AFFORDABLE INVITES: a slow breathing halo, the same gesture the mastery tree uses for its
        // unchosen specialisations. Quiet on purpose — an invitation, not an alarm.
        if (buyable && !isOwned)
        {
            // REDUCED MOTION STOPS THE BREATHING, not the halo (brief §32: drop idle motion, keep the
            // state). The ring is held at the middle of the breath, so an affordable node still wears the
            // one mark that says "you can take this" and nothing on the screen moves by itself.
            var pulse = UiMotion.Reduced ? 0.5f : 0.5f + 0.5f * MathF.Sin(_time * 2.6f);
            for (var ring = 2; ring >= 1; ring--)
            {
                var g = (int)(box.Width * 0.10f * ring + pulse * 4f * _zoom);
                _ui.Diamond(b, new Rectangle(box.X - g, box.Y - g, box.Width + g * 2, box.Height + g * 2),
                            road * (0.05f + 0.09f * pulse));
            }
        }

        // THE PULSE LANDS: as the band along the wire reaches this node, a pale glow pops on around it
        // and fades — the node "activates" (brief §73–§82) in the diagram, not only in the flourish
        // over it. Under Reduced Motion the same glow simply fades from the moment of purchase.
        // The BLOOM goes behind the face; the RING goes outside the gold selection outline further
        // down. Both used the same inflated box, and a trait is always SELECTED the instant it is
        // bought — so the gold outline was drawn over the pale one, pixel for pixel, and the arrival
        // never showed on the one node it is about.
        var landing = u.Id == _pulseTo && PulseProgress() is { } s
            ? (UiMotion.Reduced ? 1f - s : WirePulseAt(s).Glow) : 0f;
        if (landing > 0f)
        {
            var g = Math.Max(4, (int)(box.Width * 0.22f * landing));
            _ui.Diamond(b, new Rectangle(box.X - g, box.Y - g, box.Width + g * 2, box.Height + g * 2), Bone * (0.30f * landing));
        }

        DrawFace(b, tree, u, box, hover || u.Id == _selectedId);
        if (u.Id == _selectedId) Outline(b, new Rectangle(box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6), Gold, Math.Max(2, Px(3)));   // gold = selected
        if (landing > 0f)
        {
            var ring = Math.Max(6, Px(9));
            Outline(b, new Rectangle(box.X - ring, box.Y - ring, box.Width + ring * 2, box.Height + ring * 2),
                    Bone * landing, Math.Max(2, Px(3)));
        }

        // STATE IN A SHAPE, not only in a tint (brief §8): a lock on a locked node, a tick on a learned one.
        if (labels && box.Width >= 18)
        {
            var chip = Math.Max(10, Px(16));
            var cr = new Rectangle(box.Right - chip / 2, box.Y - chip / 3, chip, chip);
            // The tick keeps its 100 % size on the chip's foot, as it always did: the chip follows the camera
            // zoom, not the profile, and the tick's box is what DrawTick proportions itself to.
            if (isOwned) { _ui.Fill(b, cr, Plate); DrawTick(b, new Rectangle(cr.X, cr.Bottom - TickBoxH, TickBoxW, TickBoxH), true); }
            else if (!buyable) _ui.Icon(b, "ui_slot_locked", cr, Bone);
        }

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

    /// <summary>What kind of thing a node is, in the player's words — the inspector's CATEGORY.</summary>
    private static string KindWord(SocketKind k) => k switch
    {
        SocketKind.Terminal => "TERMINAL — THE ROAD'S END",
        SocketKind.Rung => "KEYSTONE GATE",
        SocketKind.Spur => "KEYSTONE SPUR",
        SocketKind.Gate => "SPINE GATE",
        SocketKind.Mark => "MARK",
        _ => "ATTRIBUTE",
    };

    /// <summary>
    /// THE INSPECTOR (D3, brief §45): the SELECTED trait, read properly — its road and what that road is for,
    /// its face, its name, its state as a pill, then the sheet in the order a player decides in: what it does,
    /// what it costs, what it needs first, and that it is permanent; the reason beside the button; one button.
    /// </summary>
    /// <remarks>
    /// Click pins; hover only highlights and tips (D5) — the panel used to swap to whatever the pointer crossed
    /// on the way to the button, and the button's enabled state flipped with it. The words come from
    /// <see cref="MemoryDustText.Describe"/>, so the sheet a test holds and the one the player reads agree.
    /// <para>
    /// THE SHEET SCROLLS (UI polish §17–§18). The head (road, face, name, state) and the foot (the
    /// permanence line, the reason, the one button) are ANCHORED; what lies between them — what it does,
    /// what it costs, what it needs first — is a wheel-scrolled region of whole rows with a
    /// <see cref="UiKit.ScrollBar"/> when they do not fit. At 100 % nearly every trait fits without it;
    /// at 150 % the bigger type does not, and the answer is a scrollbar — never a smaller font and never a
    /// button under the fold. Every size in here is the profile's, through <see cref="UiMetrics"/>.
    /// </para>
    /// </remarks>
    private void DrawDetail(SpriteBatch b, MemoryDustTree tree, Point hit, bool clicked)
    {
        var panel = DetailPanel;
        _ui.PanelQuiet(b, panel);
        var u = Selected(tree);
        var isOwned = tree.Owns(u.Id);
        var buyable = tree.CanUnlock(u.Id);
        var terminal = TerminalArt(u) is not null;
        var roadCol = RoadColor(u.Road);
        var left = UiKit.ContentLeft(panel);
        var right = UiKit.ContentRight(panel);
        var width = right - left;

        // THE FOOT, stacked UP from the panel's bottom pad: the button, two lines of reason above it, the
        // permanence line above that — each at the profile's size, so at 150 % the reason does not print
        // through the permanence line, which is what a fixed "Bottom - 204" did.
        var btn = new Rectangle(left, panel.Bottom - UiMetrics.PanelPadding - UiMetrics.ButtonHeightPrimary, width, UiMetrics.ButtonHeightPrimary);
        var reasonY = btn.Y - UiMetrics.Space(10) - 2 * UiTypography.Pitch(UiTypography.Secondary);
        var permY = reasonY - UiMetrics.Space(16) - UiTypography.Pitch(UiTypography.Body) - UiMetrics.Space(10);
        var floor = permY - UiMetrics.Space(12);   // the sheet ends here
        var y = panel.Y + UiTypography.PanelTitleTop;

        // CATEGORY: the road's glyph and name with the kind, then the road's own sentence.
        var badgePx = UiMetrics.Control(26);
        var badgeGap = UiMetrics.Space(10);
        var badge = new Rectangle(left, y, badgePx, badgePx);
        var hasGlyph = _ui.Icon(b, RoadGlyph(u.Road), badge, roadCol);
        _ui.TextBig(b, _ui.ShortenBig($"{RoadName(u.Road)}  —  {KindWord(KindOf(tree, u))}", width - badgePx - badgeGap, UiTypography.Secondary),
                    hasGlyph ? badge.Right + badgeGap : left, y + UiMetrics.Space(3), roadCol, UiTypography.Secondary);
        y += UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
        _ui.TextBig(b, _ui.ShortenBig(TraitRoads.Sentence(u.Road), width, UiTypography.Body), left, y, Slate, UiTypography.Body);
        y += UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(8);

        // THE FACE, NAME and STATE pill.
        var size = UiMetrics.Control(terminal ? 96 : 76);
        var icon = new Rectangle(panel.Center.X - size / 2, y, size, size);
        DrawFace(b, tree, u, icon, hot: false);
        y += size + UiMetrics.Space(6);
        _ui.TextCenterBig(b, _ui.ShortenBig(u.Name, width, UiTypography.PanelTitle), panel.Center.X, y, isOwned ? Gold : Bone, UiTypography.PanelTitle);
        y += UiTypography.Pitch(UiTypography.PanelTitle);
        var state = isOwned ? "LEARNED" : buyable ? "AVAILABLE NOW" : "LOCKED";
        var stateCol = isOwned ? Gold : buyable ? Met : Bone;
        // The pill is its parts added up — a pad, a glyph box, a gap, the word, a pad — so its width and its
        // glyph follow the profile together instead of a hand-measured 44.
        var pillPad = UiMetrics.Space(10);
        var pillGlyph = UiMetrics.Control(16);
        var pillTextX = pillPad + pillGlyph + UiMetrics.Space(6);
        var pillW = pillTextX + _ui.MeasureBig(state, UiTypography.Secondary) + UiMetrics.Space(12);
        var pill = new Rectangle(panel.Center.X - pillW / 2, y, pillW, UiTypography.Secondary + UiMetrics.Space(10));
        _ui.Fill(b, pill, stateCol * 0.14f);
        Outline(b, pill, stateCol, 1);
        if (isOwned) DrawTick(b, new Rectangle(pill.X + pillPad, pill.Y + UiMetrics.Space(3), pillGlyph, UiMetrics.Control(TickBoxH)), true);
        else if (buyable)
        {
            var d = UiMetrics.Control(12);
            _ui.Diamond(b, new Rectangle(pill.X + pillPad + (pillGlyph - d) / 2, pill.Y + UiMetrics.Space(7), d, d), Met);
        }
        else
        {
            var l = UiMetrics.Control(18);
            _ui.Icon(b, "ui_slot_locked", new Rectangle(pill.X + pillPad + (pillGlyph - l) / 2, pill.Y + UiMetrics.Space(3), l, l), Bone);
        }
        _ui.TextBig(b, state, pill.X + pillTextX, pill.Y + UiMetrics.Space(4), stateCol, UiTypography.Secondary);
        y += pill.Height + UiMetrics.Space(10);

        // ── THE SHEET: whole rows between the pill and the foot, scrolled by the wheel ──
        // Walked twice by one local function: once to MEASURE (does it all fit at the full width?), then
        // to DRAW — narrower by the scrollbar's lane when it did not. Row() is the whole scroll model: it
        // counts every row, skips the ones above _sheetFirst, and stops at the first one that would cross
        // the floor, so no row is ever half-drawn over the permanence line.
        var sheetTop = y;
        var sheetW = width;
        var lineH = UiTypography.Pitch(UiTypography.Body);
        var headH = UiTypography.Pitch(UiTypography.Secondary);
        var ruleGap = UiMetrics.Space(10);
        var desc = MemoryDustText.Describe(u);
        var lines = _ui.WrapBig(desc, sheetW, UiTypography.Body);
        var poor = !isOwned && tree.Available < u.Cost;
        var source = poor ? _ui.WrapBig(PointSource, sheetW, UiTypography.Secondary) : new List<string>();
        var haveLine = $"You have {tree.Available} trait {(tree.Available == 1 ? "point" : "points")}.";
        var costLine = isOwned ? "LEARNED" : $"{u.Cost} TRAIT {(u.Cost == 1 ? "POINT" : "POINTS")}";
        var statusW = Math.Max(_ui.MeasureBig("learned", UiTypography.Body), _ui.MeasureBig("not yet", UiTypography.Body));
        var tickW = UiMetrics.Control(TickBoxW);
        var tickH = UiMetrics.Control(TickBoxH);
        var nameX = UiMetrics.Space(2) + tickW + UiMetrics.Space(10);

        int idx = 0, contentH = 0, cursor = 0, rowY = 0;
        bool drawing = false, full = false;
        bool Row(int h)
        {
            var i = idx++;
            contentH += h;
            if (!drawing) { _sheetRows.Add(h); return false; }
            if (full || i < _sheetFirst) return false;
            if (cursor + h > floor) { full = true; return false; }
            rowY = cursor;
            cursor += h;
            return true;
        }
        void Walk()
        {
            idx = 0; contentH = 0; full = false; cursor = sheetTop;
            if (!drawing) _sheetRows.Clear();
            var rowRight = left + sheetW;
            void Rule(int atY) => _ui.Fill(b, new Rectangle(left, atY, sheetW, 1), Dim);

            // 1. WHAT IT DOES — at Body, wrapped, every line a row.
            if (Row(ruleGap + headH)) { Rule(rowY); _ui.TextBig(b, "WHAT IT DOES", left, rowY + ruleGap, Slate, UiTypography.Secondary); }
            foreach (var l in lines) if (Row(lineH)) _ui.TextBig(b, l, left, rowY, Bone, UiTypography.Body);

            // 2. WHAT IT COSTS — in TRAIT POINTS, beside how many you have; and, when short, where points come from.
            if (Row(UiMetrics.Space(6) + ruleGap + UiTypography.Pitch(UiTypography.Headline)))
            {
                var ry = rowY + UiMetrics.Space(6);
                Rule(ry);
                _ui.TextBig(b, "WHAT IT COSTS", left, ry + ruleGap, Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, costLine, rowRight, ry + ruleGap - UiMetrics.Space(4), isOwned ? Gold : buyable ? Bone : Ember, UiTypography.Headline);
            }
            if (!isOwned && Row(lineH)) _ui.TextBig(b, haveLine, left, rowY, poor ? Ember : Bone, UiTypography.Body);
            foreach (var l in source) if (Row(headH)) _ui.TextBig(b, l, left, rowY, Slate, UiTypography.Secondary);

            // 3. YOU NEED FIRST — the real Requires, each with a tick once you have it; at Body, in words too.
            if (Row(UiMetrics.Space(4) + ruleGap + headH))
            {
                var ry = rowY + UiMetrics.Space(4);
                Rule(ry);
                _ui.TextBig(b, "YOU NEED FIRST", left, ry + ruleGap, Slate, UiTypography.Secondary);
            }
            if (u.Requires.Count == 0) { if (Row(lineH)) _ui.TextBig(b, "Nothing. You can start here.", left, rowY, Bone, UiTypography.Body); }
            else
                foreach (var reqId in u.Requires)
                {
                    if (!Row(lineH)) continue;
                    var req = tree.All.FirstOrDefault(x => x.Id == reqId);
                    var got = tree.Owns(reqId);
                    DrawTick(b, new Rectangle(left + UiMetrics.Space(2), rowY + UiMetrics.Space(5), tickW, tickH), got);
                    _ui.TextBig(b, _ui.ShortenBig(req?.Name ?? reqId, rowRight - (left + nameX) - statusW - UiMetrics.Gap, UiTypography.Body),
                                left + nameX, rowY, got ? Bone : Slate, UiTypography.Body);
                    _ui.TextRightBig(b, got ? "learned" : "not yet", rowRight, rowY, got ? Met : Slate, UiTypography.Body);
                }
        }

        var room = floor - sheetTop;
        Walk();   // measure, at the full width
        var scrolls = contentH > room;
        if (scrolls)
        {
            sheetW = width - UiMetrics.ScrollbarWidth - UiMetrics.Gap;
            lines = _ui.WrapBig(desc, sheetW, UiTypography.Body);
            if (poor) source = _ui.WrapBig(PointSource, sheetW, UiTypography.Secondary);
            Walk();   // measure again, narrower: the wrap may have grown a line
        }
        // The furthest the sheet scrolls is the first row from which the TAIL fits, so the last page is
        // always full: a wheel cannot leave one row over a field of nothing, and neither can a first row
        // left over from a longer sheet, a bigger profile or another trait.
        var maxFirst = Math.Max(0, _sheetRows.Count - 1);
        for (int i = _sheetRows.Count - 1, tail = 0; i >= 0 && (tail += _sheetRows[i]) <= room; i--) maxFirst = i;
        _sheetTotal = _sheetRows.Count;
        _sheetPage = _sheetTotal - maxFirst;
        _sheetFirst = Math.Clamp(_sheetFirst, 0, maxFirst);
        drawing = true;
        Walk();   // draw
        if (scrolls)
            _ui.ScrollBar(b, new Rectangle(right - UiMetrics.ScrollbarWidth, sheetTop, UiMetrics.ScrollbarWidth, floor - sheetTop),
                          _sheetFirst, _sheetPage, _sheetTotal);

        // 4. PERMANENT. NEVER RESETS. — the last thing read before the button, in gold.
        _ui.Fill(b, new Rectangle(left, permY, width, 1), Dim);
        _ui.TextCenterBig(b, MemoryDustText.Permanence, panel.Center.X, permY + UiMetrics.Space(10), Gold * 0.85f, UiTypography.Body);

        // THE REASON, at Body, beside the button: success in Met, refusal in Ember, the standing reason otherwise.
        var reason = _msg.Length > 0 ? _msg
                   : isOwned || buyable ? ""
                   : tree.Available < u.Cost ? "NOT ENOUGH TRAIT POINTS" : "LEARN WHAT IT NEEDS FIRST";
        var good = _msg.StartsWith("LEARNED:", StringComparison.Ordinal);
        var armedHere = _armedId == u.Id;
        if (reason.Length > 0)
            foreach (var (l, k) in _ui.WrapBig(reason, width, UiTypography.Secondary).Take(2).Select((l, k) => (l, k)))
                _ui.TextCenterBig(b, l, panel.Center.X, reasonY + k * UiTypography.Pitch(UiTypography.Secondary), good ? Met : armedHere ? Gold : Ember, UiTypography.Secondary);

        var label = isOwned ? "LEARNED"
                  : armedHere ? "PRESS AGAIN TO COMMIT"
                  : terminal ? $"COMMIT TO {RoadName(u.Road)}"
                  : "LEARN THIS TRAIT";
        if (_ui.Button(b, btn, label, hit, clicked, enabled: buyable, buyable ? ButtonStyle.Primary : ButtonStyle.Secondary))
            TryBuy(tree, u);
    }

    /// <summary>Where trait points come from — under the cost, when the player is short of them.</summary>
    private const string PointSource = "Points come from conquering regions, going deeper into the corruption, and raising a region's mastery.";

    /// <summary>The tick's box at 100 %: <see cref="DrawTick"/> proportions its squares to a box this tall.</summary>
    private const int TickBoxW = 20;
    private const int TickBoxH = 16;

    /// <summary>
    /// A tick (or, off, a dash) built from small squares, proportioned to the box's height: a box
    /// <see cref="TickBoxH"/> tall draws 3-px squares — the 100 % tick, exactly — and a
    /// <see cref="UiMetrics.Control"/>-scaled box draws a bigger one, so the inspector's ticks grow with its type.
    /// </summary>
    private void DrawTick(SpriteBatch b, Rectangle r, bool on)
    {
        var f = r.Height / (float)TickBoxH;
        var s = Math.Max(2, (int)MathF.Round(3 * f));
        if (on)
        {
            for (var k = 0; k < 5; k++) _ui.Fill(b, new Rectangle(r.X + (int)(k * f), r.Bottom - (int)(8 * f) + (int)(k / 2 * f), s, s), Met);
            for (var k = 0; k < 10; k++) _ui.Fill(b, new Rectangle(r.X + (int)((5 + k) * f), r.Bottom - (int)(2 * f) - (int)(k * f), s, s), Met);
        }
        else _ui.Fill(b, new Rectangle(r.X + (int)(2 * f), r.Center.Y - (int)(2 * f), r.Width - (int)(4 * f), (int)(4 * f)), Slate);
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
