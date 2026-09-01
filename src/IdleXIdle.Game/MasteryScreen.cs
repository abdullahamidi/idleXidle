using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Persistence;

using IdleXIdle.Core.Progression;
namespace IdleXIdle.Game;

/// <summary>
/// The BUILD screen: a four-panel overview of the woven loadout — an identity card, the Source/Form/Vow
/// composition, the skill cards with their two actions docked beneath them, and the passives / resonance
/// column. It is the MASTERY rail tile's one and only view.
/// </summary>
/// <remarks>
/// Built to the Build production spec (rev 1). All data is REAL: the game has ONE live loadout (not the
/// reference's named "saved loadouts" presets, which the spec's own layout drops), so the screen shows the
/// current build. Resonance is the real source composition of the equipped skills; passives are the taken
/// mastery nodes; there is no numeric "trait bonus %" table, so that is shown as the real passive list.
/// </remarks>
public sealed class MasteryScreen
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color PanelBg = new(0x14, 0x11, 0x1A, 0xC8);
    private static readonly Color Quiet = UiInk.Plate;
    private static readonly Color SbBg = new(0x0F, 0x0D, 0x15);
    private static readonly Color Hi = new(0x2A, 0x24, 0x14);
    private static readonly Color Path = new(0x39, 0x33, 0x44);
    private static readonly Color Verd = new(0x5A, 0x9A, 0x4A);
    private static readonly Color Purple = new(0x8A, 0x5A, 0xC8);

    private static string Short(Branch b) => b.ToString().ToUpperInvariant();

    private static string Short(Style s) => s.ToString().ToUpperInvariant();

    /// <summary>The word for a woven slot: the skill's NAME, or EMPTY for a slot holding nothing.</summary>
    private static string SkillWord(PlayerLoadout.SkillChoice s)
        => SkillCatalogue.Find(s.SkillId)?.Name ?? "EMPTY";

    /// <summary>A node label's NAME — everything before the em dash that introduces what it does.</summary>
    /// <remarks>
    /// Catalogue labels are one string doing two jobs: "OVERWHELM — HITS UNDER 60 DO NOTHING". The
    /// detail card wants the whole sentence; a plaque 114 pixels wide wants the first two words.
    /// </remarks>
    private static string Head(string label)
    {
        var cut = label.IndexOf('—');
        return (cut < 0 ? label : label[..cut]).Trim();
    }

    private readonly UiKit _ui;

    /// <summary>
    /// The one animated thing on this page: the breath under an unchosen Specialisation.
    /// </summary>
    /// <remarks>
    /// A wall clock rather than an accumulated delta because this screen's Update takes no frame time
    /// and the host's call site is not this file's to change. Nothing reads it but the halo, so a
    /// dropped frame costs a hair of phase and nothing else.
    /// </remarks>
    private static readonly System.Diagnostics.Stopwatch _breath = System.Diagnostics.Stopwatch.StartNew();

    private string _msg = "";
    private string? _hoverNodeId;          // the node under the pointer this frame
    private string? _pinnedNodeId;         // the last node clicked — what the detail panel shows when nothing is hovered
    private string? _specNodeId;         // non-null: the SPECIALISATION ceremony is open for this just-taken node
    private Style _specStyle;

    /// <summary>The host reads this to hold the rail, its hotkeys, L and T while the ceremony is open.</summary>
    public bool SpecialisationOpen => _specNodeId is not null;

    /// <summary>The docked plates — input over them must never reach the tree behind.</summary>
    /// <remarks>
    /// The corner plate joined the two right-hand cards when it became a solid plate (2026-08-29).
    /// While it was loose text on the wallpaper there was nothing for a click to land on; a frame you
    /// can see is a frame the pointer must respect, or dragging the tree by its own furniture pans the
    /// canvas under it. The reset button is hit-tested BEFORE this gate in <see cref="Update"/>, so it
    /// still works. (The top strip needs no entry: the canvas starts below it.)
    /// </remarks>
    private static bool OverDock(Point p) => InspectorPanel.Contains(p) || PointsPanel.Contains(p) || ResetBtn.Contains(p);
    private bool _resetArmed;   // the reset button has been pressed once and is waiting for the second

    // (This class carried a second, unreachable view until 2026-09-01 — a "BUILD OVERVIEW" of four
    // panels whose copy still said "EVERY SKILL IS A SOURCE AND A FORM". The rail had opened the tree
    // directly for weeks; the overview, its door, its flags and its colour table are gone: UX V2 P0.2.)

    /// <summary>DEV: pin a node in the inspector, so a capture can photograph it read (RH_SHOT_NODE).</summary>
    public void DevPin(string nodeId) => _pinnedNodeId = nodeId;

    /// <summary>Arming survives no navigation: the two-click reset disarms whenever the screen is (re)opened.</summary>
    public void Disarm() => _resetArmed = false;

    /// <summary>DEV ONLY: frame the tree on the centre at a given zoom (the whole-tree framing by default).</summary>
    /// <remarks>
    /// The zoom argument exists because node ART cannot be verified from the default overview —
    /// at that scale a Notable is thirty pixels across and a capture proves only that something was
    /// drawn there. The whole point of a camera is that the same layout has a near view.
    /// </remarks>
    public void DevOpenTree(float zoom = 0f)
    {
        _pan = Vector2.Zero;
        _zoom = zoom > 0f ? zoom : WholeTreeZoom;
    }

    /// <summary>DEV ONLY: open the tree the way a player's FIRST visit opens it — centre and ring 1.</summary>
    public void DevOpenTreeFirstVisit()
    {
        FrameFirstOpen();
    }

    // ── THE CAMERA THE SAVE CARRIES. "On later visits the player's zoom should be saved." ────────

    /// <summary>The tree camera's zoom, for the save. 0 is never written: a zoom is always framed.</summary>
    public float CameraZoom => _zoom;
    public float CameraPanX => _pan.X;
    public float CameraPanY => _pan.Y;

    /// <summary>
    /// Put the camera where the save left it — or, for a save that never opened the tree (zoom 0,
    /// which is what every older save carries), on the FIRST-OPEN framing.
    /// </summary>
    public void RestoreCamera(float zoom, float panX, float panY)
    {
        if (zoom <= 0f || float.IsNaN(zoom) || float.IsNaN(panX) || float.IsNaN(panY))
        {
            FrameFirstOpen();
            return;
        }
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _pan = new Vector2(panX, panY);
        ClampPan();
    }

    public MasteryScreen(UiKit ui) => _ui = ui;

    /// <summary>DEV: pose THE ATTUNEMENT ceremony for a capture, without touching the tree's state.</summary>
    public void DevSpecialise(Style form)
    {
        _specNodeId = MasteryCatalog.Nodes
            .First(n => n.Kind == MasteryKind.Specialisation && n.Style == form).Id;
        _specStyle = form;
    }

    public PlayerLoadout Loadout { get; set; } = new();
    public MasteryTree Mastery { get; set; } = new();

    /// <summary>The character being played — host-fed, for the summary portrait.</summary>
    public Character? Character { get; set; }
    public MemoryDustTree Tree { get; set; } = new();
    public int Power { get; set; }   // host-set: PowerRating (the Build screen has no Hunter reference)
    public int Level { get; set; }
    public bool Dirty { get; private set; }
    public void ClearDirty() => Dirty = false;
    public bool DevBuildDebug { get; set; }

    /// <summary>
    /// YOUR POINTS — the mastery glyph, what the tree has cost you, where it went, and the one button
    /// that undoes it. A compact plate in the corner, the way the TRAITS screen carries its own counter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest 2026-08-29 asked for the tree page's furniture to be on a frame instead of printed on
    /// the wallpaper, and it was — but the fix put the SCREEN'S TITLE on that frame too, in the top-left
    /// corner, which is a shape no other screen in the game has. Playtest 2026-08-30: <i>"do you think
    /// the game's other screens look like this? Look at the MASTERY screen again."</i> Right. The title
    /// is at the top with its rule and its caption now (<see cref="DrawTreeChrome"/>); what stayed
    /// behind is this plate, which is furniture and belongs in a corner.
    /// </para>
    /// <para>
    /// It is a DOCK, the same as the two cards on the right — chrome that sits over the canvas rather
    /// than beside it — so it is in <see cref="OverDock"/> and a drag started on it moves nothing. The
    /// aspect (1.30) is deliberately exactly <see cref="UiKit.PanelArtKey"/>'s threshold: it takes the
    /// MEDIUM frame, whose top rail is 20 source px deep, rather than the square frame's 51 px crest,
    /// which would push every row 28 px down a plate that has none to spare.
    /// </para>
    /// </remarks>
    // JUST THE GLYPH AND THE NUMBER, like the TRAITS screen's plate (playtest 2026-08-30: "the panel at
    // the top left should be only the symbol and the number, as on the traits screen — the four tree
    // rows there are unnecessary"). The branch tally and the SPENT cell are gone; TAKE EVERY POINT BACK
    // is its own button under the plate, because it is an action and not a readout.
    private static readonly Rectangle PointsPanel = new(40, 112, 300, 140);

    // ON THE TREE PAGE, NOT THE OVERVIEW. Playtest: "'Take all mastery points back' buranın butonu
    // değil, mastery tree'nin butonu." Right — it un-spends every point in a tree the overview does not
    // even draw, so pressing it there meant watching nothing happen to anything on screen. It sits
    // where BACK used to: MASTERY is a rail destination now, and a BACK button on a page with its own
    // door reads as "you are somewhere nested" when you are not (playtest asked what it even meant).
    //
    // INSIDE THE POINTS PLATE now, on its content width, rather than floating on the tree beneath it.
    private static readonly Rectangle ResetBtn =
        new(PointsPanel.X, PointsPanel.Bottom + 12, 300, 52);

    // ══ THE TREE'S OWN SPACE ═══════════════════════════════════════════════════════════════════
    //
    // Node positions are computed in WORLD units around (0,0) and a camera decides what is on screen.
    // The tree used to be laid out directly in pixels around (600,520) with a radius of 464 — which
    // meant the LAYOUT was the size of the window, so every node added made the tree tighter and there
    // was no way to add a second cross, a deeper ring, or a whole new branch without the thing becoming
    // unreadable. A world plus a camera has no such ceiling: growing the tree moves the far edge, not
    // the spacing. The rim itself is MasteryLayout.WorldRadius — it was a second copy of the number
    // here, which is one copy more than a rim can have.

    private Vector2 _pan;                 // world point at the centre of the view

    /// <summary>
    /// The framing HOME returns to, and the one the whole-tree capture is judged at: every capstone,
    /// its name, and the four branch headers inside the view.
    /// </summary>
    /// <remarks>
    /// DERIVED, not pinned. It was 0.30, then 0.24, each chosen by hand against the rim of the day and
    /// each wrong the day the rim moved. The rule it always encoded is "the header ring fits the view's
    /// half-height with room for the header's own two lines", so that is what is written: the view's
    /// half-height less a header's half-height, over the header ring's world radius. Move the rim, the
    /// ring or the view and this follows.
    /// </remarks>
    // PROPERTIES, NOT STATIC READONLY FIELDS: a static field initialiser runs in textual order, and
    // TreeView is declared below — so a field here read a zero-height rectangle and framed the whole
    // tree at a negative zoom (the first capture after this change was a single dot). A property reads
    // the rectangle when it is asked, which is always after the type is initialised.
    private static float WholeTreeZoom
        => (TreeView.Height / 2f - HeaderHalfHeightPx) / (MasteryLayout.WorldRadius * HeaderRing);

    /// <summary>
    /// The framing the tree opens with the FIRST time: the centre node and the ring of minors around it.
    /// </summary>
    /// <remarks>
    /// "When the tree first opens it should come zoomed in on the centre node and the nodes one step
    /// around it." <see cref="MasteryLayout.FirstOpenRadius"/> is the world radius that encloses exactly
    /// that; the zoom is the view's smaller half-extent over it, less a margin so the outermost minor
    /// sits inside the frame rather than on its edge. The spurs beyond ring 1 show at the corners on
    /// purpose — half a node at the edge is the invitation to pan.
    /// </remarks>
    private static float FirstOpenZoom
        => (Math.Min(TreeView.Width, TreeView.Height) / 2f - FirstOpenMarginPx) / MasteryLayout.FirstOpenRadius;

    private const float FirstOpenMarginPx = 48f;

    /// <summary>Half the height a branch header occupies on screen: its title line above, promise below.</summary>
    private const float HeaderHalfHeightPx = 24f;

    /// <summary>
    /// Below this, every caption on the tree drops out and only shapes remain.
    /// </summary>
    /// <remarks>
    /// Two hundredths under the whole-tree framing, so the one framing HOME returns to keeps its
    /// labels. It was once pinned at 0.28 against a default of 0.30 and silently hid every label when
    /// the default moved; tied to the framing instead of guessed at again.
    /// </remarks>
    private static float LabelZoom => WholeTreeZoom - 0.02f;

    private float _zoom = FirstOpenZoom;  // screen pixels per world unit — a fresh game opens on ring 1
    private Point? _dragFrom;             // where a drag started, in screen space
    private bool _draggedThisPress;       // the drag moved far enough to swallow the click
    private Vector2 _dragPanFrom;

    private const float MinZoom = 0.16f, MaxZoom = 1.40f;

    /// <summary>Centre node and ring 1, dead centre.</summary>
    private void FrameFirstOpen()
    {
        _pan = Vector2.Zero;
        _zoom = Math.Clamp(FirstOpenZoom, MinZoom, MaxZoom);
    }

    /// <summary>
    /// The tree's canvas — and, through its centre, where the world origin is projected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It was (180, 96, 1740, 900), i.e. everything right of the nav rail, so the origin projected to
    /// x=1050 and the whole EAST arm — the Tempo capstone included — was drawn UNDER the two docked
    /// cards at x&gt;=1408 and covered by them. One of the four things the layout exists to show was
    /// invisible at the default framing, which is the same species of bug as a label nobody reads.
    /// The rectangle is now the space the tree actually HAS: right of the rail, left of the dock, and
    /// the full height of the canvas rather than an inset band. Its centre (810, 588) is what every
    /// number in <see cref="WholeTreeZoom"/> and <see cref="HeaderRing"/> is measured from.
    /// </para>
    /// <para>
    /// <b>IT STARTS UNDER THE SCREEN'S HEADER NOW (y=112, was 40).</b> Playtest 2026-08-30: <i>"on the
    /// other screens the title is at the top, a rule under it, and then the screen's content begins."</i>
    /// The tree page's title is at the top like the rest of the game now (see
    /// <see cref="DrawTreeChrome"/>) — and at the whole-tree framing this canvas plants the north
    /// branch header 24 px inside its own top edge, i.e. straight through that title, the rule and the
    /// caption. A canvas that begins where the header ends is what every other screen does and what the
    /// TRAITS tree next door already did (its view starts at 136).
    /// </para>
    /// <para>
    /// Nothing about the CAMERA changed: <see cref="WholeTreeZoom"/>, <see cref="FirstOpenZoom"/> and
    /// <see cref="LabelZoom"/> are the same three formulas, the saved zoom and pan still load as
    /// written, and no node moved. The framings are 5% tighter because the view is 5% shorter, which is
    /// the arithmetic doing its job rather than a retune.
    /// </para>
    /// </remarks>
    // THE CANVAS ENDS WHERE THE INSPECTOR BEGINS (UX V2 P1.5): the east ring used to be cut by a dock that
    // started 32 px inside the view. Both hang off UiKit.Page, so UI SCALE moves them together.
    private static Rectangle TreeView => new(180, 112, InspectorPanel.X - 8 - 180, UiKit.PageBottom(16) - 112);

    private Vector2 Screen(Vector2 world)
        => new(TreeView.Center.X + (world.X - _pan.X) * _zoom,
               TreeView.Center.Y + (world.Y - _pan.Y) * _zoom);

    private Vector2 World(Point screen)
        => new((screen.X - TreeView.Center.X) / _zoom + _pan.X,
               (screen.Y - TreeView.Center.Y) / _zoom + _pan.Y);

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
        var before = World(anchor);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        var after = World(anchor);
        _pan += before - after;
        ClampPan();
    }

    /// <summary>Keep the tree reachable: the centre may leave the view, but never by more than a screen.</summary>
    private void ClampPan()
    {
        var limit = MasteryLayout.WorldRadius * 1.25f;
        _pan = new Vector2(Math.Clamp(_pan.X, -limit, limit), Math.Clamp(_pan.Y, -limit, limit));
    }

    /// <summary>The rim point of a branch: where its capstone sits, in world units.</summary>
    private static Vector2 Corner(Branch b)
    {
        var a = MasteryLayout.AngleOf(b);
        return new Vector2(MasteryLayout.WorldRadius * MathF.Cos(a), MasteryLayout.WorldRadius * MathF.Sin(a));
    }

    /// <summary>
    /// How far past the rim a branch HEADER is planted, as a multiple of <see cref="MasteryLayout.WorldRadius"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 1.30. The capstone is a round medallion now (it was a plaque 456 world units wide, which pushed
    /// the ring out to 1.40 on the east and west arms), so the header only has to clear the capstone's
    /// radius plus the two caption lines stacked OUTWARD from it — its NAME and the word CAPSTONE — on
    /// the two arms where those lines point at the header, north and south. At the whole-tree framing
    /// the medallion's outer edge is 2144 world units out, the two lines are ~44px, and the header's
    /// promise line ends 7px past its own ring: 1.26 left the north CAPSTONE one pixel into "FEWER,
    /// BIGGER HITS"; 1.30 leaves about twenty. The buildtree capture is what says so.
    /// </para>
    /// <para>
    /// Held in WORLD units rather than screen ones so a header is fixed to its arm: pan and the header
    /// travels with its branch, zoom and it stays exactly as far outside the rim as it was.
    /// </para>
    /// </remarks>
    private const float HeaderRing = 1.30f;

    /// <summary>What a branch IS, in one plain line — the promise its nodes then keep.</summary>
    /// <remarks>
    /// Each line is a compression of that branch's own catalogue text, not a new claim — one summary
    /// per branch (RESONANCE / LOOT / TEMPO / ENDURE), drawn from that branch's own node lines.
    /// </remarks>
    private static string BranchPromise(Branch b) => b switch
    {
        Branch.Resonance => "STRONGER SKILLS, BEFORE YOU PICK ONE",
        Branch.Loot => "CARRY MORE OUT OF EVERY RUN",
        Branch.Tempo => "HIT FIRST AND OFTEN",
        _ => "OUTLAST THE ENEMY",
    };

    /// <summary>Where a node lives in WORLD units. Independent of zoom, pan, and the window.</summary>
    /// <remarks>
    /// The geometry itself is <see cref="MasteryLayout"/>, in Core. It was here, which meant the one
    /// claim worth making about a tree layout — that no two nodes land on top of each other — could not
    /// be tested at all. This is now the conversion to the drawing library's vector type and nothing
    /// else.
    /// </remarks>
    private static Vector2 NodePos(MasteryNode n)
    {
        var p = MasteryLayout.PositionOf(n);
        return new Vector2(p.X, p.Y);
    }


    /// <summary>Size states price: a node you can see is expensive before you read it.</summary>
    /// <remarks>
    /// The sizes live in <see cref="MasteryLayout.NodeWorldRadius"/> with the 1.9 already folded in,
    /// because the overlap test has to reason about how big a node actually is. Divided back out here
    /// so the two call sites below keep the multiply they have always had.
    /// </remarks>
    private static int NodeRadius(MasteryKind k) => (int)(MasteryLayout.NodeWorldRadius(k) / 1.9f);
    private const int SbX = 1200;

    /// <summary>
    /// THE INSPECTOR (UX V2 P1.5, D3): the whole right column. It replaced two equal ornate frames — a
    /// standing YOUR STYLE chart that was an empty state for most of the game, and a node card with no
    /// control in it. What a node is, what it does, what it needs, what it costs, and the one button.
    /// </summary>
    private static Rectangle InspectorPanel => new(UiKit.PageRight(16) - 496, 112, 496, UiKit.PageBottom(16) - 112);
    private static Rectangle TakeBtn => new(UiKit.ContentLeft(InspectorPanel), InspectorPanel.Bottom - 92,
                                            UiKit.ContentRight(InspectorPanel) - UiKit.ContentLeft(InspectorPanel), 56);
    private const int InspectorHexRadius = 70;
    private const int InspectorSealPx = 30;   // ui-size-ok: the seal medallion's edge in pixels, not a text size
    // THE CEREMONY IS CENTRED ON THE PAGE, not on the canvas. It is drawn inside the overlay transform
    // like everything else on this screen, so at UI SCALE 125% a modal pinned to 960 sat off to the right.
    private static Rectangle SpecPanel => new(UiKit.PageCenterX - 480, 180, 960, 720);   // ui-page-ok: 960 is this modal's own width, and it fits the smallest page
    private static Rectangle SpecSealBtn => new(SpecPanel.X + 80, SpecPanel.Bottom - 84, 360, 52);
    private static Rectangle SpecUndoBtn => new(SpecPanel.Right - 80 - 360, SpecPanel.Bottom - 84, 360, 52);
    // Sized for FIVE skill cards, not four.
    //
    // The trait tree's spine sells a fifth weave, and the old geometry (four cards of 132 at a pitch of
    // 144, sidebar ending at 928) had no room for it — the fifth card would have run over the keystone
    // chips, and the keystone header already sat on top of slot four's Vow row in a capture. Everything
    // below is derived from fitting five cards plus a header plus three chips inside 1080.

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>These are the TREE view's regions; the tour of MASTERY is the only tour this screen gets.</remarks>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.MasteryTree => new[] { TreeView },
        TourTarget.Specialisations => new[] { InspectorPanel },
        TourTarget.NodeCard => new[] { InspectorPanel, ResetBtn },
        _ => Array.Empty<Rectangle>(),
    };

    // ── Update ───────────────────────────────────────────────────────────────────────────────────
    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked, MemoryDustTree tree)
        => Update(keys, prev, mouse, clicked, false, 0, tree);

    public void Update(KeyboardState keys, KeyboardState prev, Point mouse, bool clicked,
                       bool held, int wheel, MemoryDustTree tree, bool rightClicked = false)
    {
        Tree = tree;

        // ── THE CAMERA. Only while the tree page is open; the overview has nothing to pan. ────────
        if (_specNodeId is not null) { _dragFrom = null; _draggedThisPress = false; }

        if (_specNodeId is null)
        {
            var over = mouse;

            if (wheel != 0 && TreeView.Contains(over) && !OverDock(over))
                ZoomAt(over, wheel > 0 ? 1.16f : 1f / 1.16f);

            // DRAG TO PAN. Held, not clicked: a click is a node take, and a tree you can only move with
            // a scrollbar is a tree nobody moves.
            if (held && TreeView.Contains(over) && (_dragFrom is not null || !OverDock(over)))
            {
                if (_dragFrom is null) { _dragFrom = over; _dragPanFrom = _pan; }
                else
                {
                    var d = _dragFrom.Value;
                    _pan = _dragPanFrom + new Vector2((d.X - over.X) / _zoom, (d.Y - over.Y) / _zoom);
                    ClampPan();
                    if (Math.Abs(d.X - over.X) + Math.Abs(d.Y - over.Y) > 6) _draggedThisPress = true;
                }
            }
            else _dragFrom = null;

            // Keyboard, for anyone who would rather not drag.
            //
            // LEFT/RIGHT, NOT A/D. A and D are nav-rail hotkeys — A opens the WARREN, D is free but sits
            // beside it — and the rail's hotkey loop runs on every frame the tree is open, so holding A
            // to pan west left the tree and opened another screen. The arrows are already the pan keys
            // everywhere else in this game and collide with nothing here.
            var step = 260f / _zoom * 0.06f;
            if (keys.IsKeyDown(Keys.Left)) { _pan.X -= step; ClampPan(); }
            if (keys.IsKeyDown(Keys.Right)) { _pan.X += step; ClampPan(); }
            if (keys.IsKeyDown(Keys.Up)) { _pan.Y -= step; ClampPan(); }
            if (keys.IsKeyDown(Keys.Down)) { _pan.Y += step; ClampPan(); }
            bool Tapped(Keys k) => keys.IsKeyDown(k) && prev.IsKeyUp(k);
            if (Tapped(Keys.OemPlus) || Tapped(Keys.Add)) ZoomAt(TreeView.Center, 1.25f);
            if (Tapped(Keys.OemMinus) || Tapped(Keys.Subtract)) ZoomAt(TreeView.Center, 1f / 1.25f);
            if (Tapped(Keys.Home)) { _pan = Vector2.Zero; _zoom = WholeTreeZoom; }
        }

        // THE RIGHT-CLICK REFUND, ABOVE THE LEFT-CLICK GATE. It was written below `if (!clicked)
        // return;` — and `clicked` is the LEFT button's edge while `rightClicked` is the right's, two
        // independent latches — so the refund could only fire on a frame where both buttons were pressed
        // at once. Shipped dead, in the same commit whose comment celebrated wiring up a dead method.
        // Everything under the gate assumes a left click, so this runs first rather than widening it.
        if (rightClicked && !_draggedThisPress && _specNodeId is null)
        {
            var rhit = mouse;
            if (OverDock(rhit)) return;
            foreach (var node in MasteryCatalog.Nodes)
            {
                if (node.Kind == MasteryKind.Start) continue;
                var rp = Screen(NodePos(node));
                var rr = Math.Max(6, (int)(NodeRadius(node.Kind) * _zoom * 1.9f)) + 6;
                if (Math.Abs(rhit.X - rp.X) > rr || Math.Abs(rhit.Y - rp.Y) > rr) continue;
                _pinnedNodeId = node.Id;
                if (Mastery.Refund(node.Id)) { _msg = "ONE POINT RETURNED."; Dirty = true; }
                else _msg = Mastery.IsTaken(node.Id)
                    ? "ANOTHER NODE DEPENDS ON THIS ONE."
                    : "NOTHING SPENT HERE.";
                return;
            }
        }

        if (!clicked) return;

        // A click that ENDED a drag is a pan, not a take. Without this every attempt to move the tree
        // also spent a point on whatever node the pointer happened to stop over.
        if (_draggedThisPress) { _draggedThisPress = false; return; }
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = mouse;

        // ── THE ATTUNEMENT swallows the click while it is open. Seal keeps the node; undo is a
        //    real Refund, so backing out costs nothing — deliberation, not punishment. ──────────
        if (_specNodeId is { } attId)
        {
            if (SpecSealBtn.Contains(hit))
            {
                _specNodeId = null;
                _msg = $"{_specStyle.ToString().ToUpperInvariant()} IS YOUR STYLE — ITS SKILLS HIT TWICE AS HARD.";
            }
            else if (SpecUndoBtn.Contains(hit))
            {
                Mastery.Refund(attId);
                _specNodeId = null;
                Dirty = true;
                _msg = "THE POINTS ARE BACK. CHOOSE A STYLE WHEN YOU ARE READY.";
            }
            return;
        }

        // ── Edit sub-view: tree + right column. (No BACK and no WEAVE door here any more: MASTERY is
        //    its own rail tile, and a BACK on a page with its own door claims a nesting that does not
        //    exist — the rail is how you leave, same as every other screen.) ──

        if (ResetBtn.Contains(hit) && Mastery.Spent > 0)
        {
            // TWO CLICKS, because it un-spends every point in the tree and there is no undo prompt
            // anywhere else in this game. The respec itself stays free and instant — that is the
            // load-bearing difference between this tree and the permanent one — so the guard is
            // deliberation, not cost.
            if (!_resetArmed) { _resetArmed = true; _msg = "PRESS AGAIN TO TAKE EVERY POINT BACK."; return; }
            _resetArmed = false;
            Mastery.Respec();
            _msg = "ALL MASTERY POINTS RETURNED.";
            Dirty = true;
            return;
        }
        // Any other click on the tree disarms it — but must NOT return, or no node could be taken.
        _resetArmed = false;

        // The docked cards are CARDS, not glass: a click on them must not take, pin, or refund the
        // node that happens to sit underneath (at the default framing, Tempo's rim does).
        // THE INSPECTOR'S BUTTON (UX V2 P1.5): TAKE the pinned node, or GIVE BACK a taken one — the same
        // rules a click on the node itself runs, so the two doors cannot disagree.
        if (TakeBtn.Contains(hit) && _pinnedNodeId is { } pinId && MasteryCatalog.ById(pinId) is { } pinned)
        {
            if (Mastery.IsTaken(pinId))
            {
                if (Mastery.Refund(pinId)) { _msg = "POINTS RETURNED."; Dirty = true; }
                else _msg = "ANOTHER NODE DEPENDS ON THIS ONE.";
            }
            else TakeNode(pinned);
            return;
        }

        if (OverDock(hit)) return;

        foreach (var node in MasteryCatalog.Nodes)
        {
            if (node.Kind == MasteryKind.Start) continue;
            // The SAME transform the drawing uses, or the click lands where the node used to be.
            var sp = Screen(NodePos(node));
            var rad = Math.Max(6, (int)(NodeRadius(node.Kind) * _zoom * 1.9f)) + 6;
            var halfW = rad;
            if (Math.Abs(hit.X - sp.X) > halfW || Math.Abs(hit.Y - sp.Y) > rad) continue;
            // Clicking a node PINS it in the detail panel whether or not it could be taken — a node you
            // cannot afford is exactly the one you most want to read.
            _pinnedNodeId = node.Id;


            TakeNode(node);
            return;
        }

        // Skills, Vows and keystone sockets are all LoadoutScreen's now.
    }

    /// <summary>Take a node, or say exactly why not — for a click on the node and for the inspector's button alike.</summary>
    private void TakeNode(MasteryNode node)
    {
        var firstSpec = node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is null;
        if (Mastery.Take(node.Id))
        {
            _msg = "";
            Dirty = true;
            // THE SPECIALISATION: your first is the moment this game is named for, so it gets a ceremony
            // instead of a click-sound — the hexagon shown whole, the choice sealed or taken back.
            if (firstSpec && node.Style is { } nf) { _specNodeId = node.Id; _specStyle = nf; }
            return;
        }
        // WHY THE CLICK DID NOTHING, NAMED EXACTLY — from the rule the refusal came from.
        _msg = Mastery.IsTaken(node.Id) ? "ALREADY TAKEN." :
            Mastery.Available < node.Cost
                ? $"NEEDS {node.Cost} POINTS — YOU HAVE {Mastery.Available}. GO DEEPER." :
            node.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() is { } heldBranch
                ? $"YOU ALREADY TOOK THE {Short(heldBranch)} CAPSTONE — ONE CAPSTONE PER HUNTER." :
            node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is not null
                ? "YOU ALREADY CHOSE A STYLE — ONE STYLE PER HUNTER." :
            "TAKE A CONNECTED NODE FIRST.";
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        Tree = tree;
        _hoverNodeId = null;
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = mouse;
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));

        DrawEditor(b, hit, tree);
        if (DevBuildDebug) DrawDebug(b);
    }

    /// <summary>
    /// A button on this screen — drawn here, but decided in <see cref="Update"/>.
    /// </summary>
    /// <remarks>
    /// It used to be a private re-implementation of <see cref="UiKit.Button"/>, and the two had drifted
    /// apart in both of the ways a copy does: it stretched the whole 256×96 button art across a 320 px
    /// control (so the end scrollwork smeared — the "the frame looks cheap" of the 2026-08-26 playtest,
    /// which <c>UiKit.Button</c> fixed with a 3-slice) and it set its label at <c>Body</c> rather than
    /// <c>ButtonText</c>, so every button on the tree page spoke two rungs quieter than every button in
    /// the rest of the game. It delegates now; the input path is untouched, because the click is still
    /// resolved in <see cref="Update"/> and this always passes <c>clicked: false</c>.
    /// </remarks>
    private void Button(SpriteBatch b, Rectangle r, string label, Point hit, bool enabled)
        => _ui.Button(b, r, label, hit, false, enabled);

    private void DrawDebug(SpriteBatch b)
        => _ui.TextBig(b, $"nav MASTERY  tree  zoom {_zoom:0.00}  pan {_pan.X:0},{_pan.Y:0}", 60, 112, Gold, UiTypography.Secondary);

    // ── Edit sub-view (the existing tree + skill sidebar). ──
    private void DrawEditor(SpriteBatch b, Point hit, MemoryDustTree tree)
    {
        _ui.Scrim(b, 0.6f);
        var aff = Mastery.Affinity();

        DrawTreeWorld(b, hit, aff);
        DrawTreeChrome(b, hit);

        // The right column answers "what am I looking at", not "what are my skill slots".
        //
        // It used to be the SKILL EDITOR — four slots and a keystone strip — sitting beside a tree it has
        // nothing to do with, so the screen asked the player to hold two unrelated jobs at once and
        // answered neither. The tree's own question ("what does this node do, can I afford it, what does
        // it cost me") had no home at all except a one-line strip at the very bottom of the screen.
        DrawNodeDetail(b, hit);

        // THE BOTTOM-LEFT STRIP IS GONE (playtest 2026-08-29, item 4: "the explanation written on the
        // right of the MASTERY screen is also written at the bottom left — remove the undecorated text
        // at the bottom left"). It printed three different things in one place at the foot of the
        // canvas, and it was right about all three:
        //
        //   the hovered node's label + cost  — WORD FOR WORD what DrawNodeDetail was already showing,
        //                                      in a card four times the size, on the same frame.
        //   the last message                 — a refusal raised by a click at the OTHER end of the
        //                                      screen; it speaks in the screen's own caption row now,
        //                                      under the title, where a screen speaks.
        //   the one-branch rule              — the only line that was not printed anywhere else; it
        //                                      moved into the node card, under the cost ladder.
        //
        // Nothing was deleted except the copy, and no line lost a home.
        if (_specNodeId is not null) DrawSpecialisation(b, hit);
    }

    /// <summary>The scissor state the tree's batch runs under. One instance; the device keeps it.</summary>
    private RasterizerState? _clip;
    private RasterizerState Clip => _clip ??= new RasterizerState { ScissorTestEnable = true };

    /// <summary>
    /// THE TREE ITSELF — wires, branch headers and nodes — clipped to <see cref="TreeView"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE CANVAS HAS AN EDGE NOW.</b> Nodes are culled at a generous margin so a node half over the
    /// boundary still draws, which is right — but nothing then stopped it, so at any zoom past the
    /// overview the tree painted straight through the screen's own header: the first capture of the new
    /// top strip has a lit wire running through the middle of the caption and a node sitting on the
    /// title. The TRAITS tree next door hit exactly this and answered it exactly this way, for the
    /// reason written on its own <c>DrawTree</c>: a panel drawn later covers a node's right half and
    /// leaves its left half showing, "which reads as a bug rather than as an edge".
    /// </para>
    /// <para>
    /// The host opened the overlay batch for us; we close it, run the tree under a scissor rectangle in
    /// the SAME transform, and reopen an unclipped batch for the chrome. <see cref="Game1.OverlayTransform"/>
    /// is shared rather than copied so the two cannot drift.
    /// </para>
    /// </remarks>
    private void DrawTreeWorld(SpriteBatch b, Point hit, Style? aff)
    {
        b.End();
        _ui.Device.ScissorRectangle =
            Rectangle.Intersect(Game1.OverlayToCanvas(TreeView, Vector2.Zero), new Rectangle(0, 0, 1920, 1080));
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Clip, null, Game1.OverlayTransform(Vector2.Zero));
        try
        {
            DrawTreeNodes(b, hit, aff);
        }
        finally
        {
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    null, null, null, Game1.OverlayTransform(Vector2.Zero));
        }
    }

    private void DrawTreeNodes(SpriteBatch b, Point hit, Style? aff)
    {
        // ONE EDGE PER NODE, to its NEAREST prerequisite.
        //
        // Drawing every prerequisite drew a spider's web: each notable lists all four of its branch's
        // minors and each minor lists START, so a single branch produced sixteen crossing lines and the
        // whole screen read as scribble. A prerequisite list is an "any of these" rule, not a diagram —
        // so the drawing shows the SPINE (what the funnel looks like) and the tooltip carries the rule.
        // Gold only when both ends are taken, so a walked branch reads as one continuous path.
        foreach (var node in MasteryCatalog.Nodes)
        {
            if (node.Prereqs.Count == 0) continue;
            var to = NodePos(node);

            MasteryNode? nearest = null;
            var best = float.MaxValue;
            foreach (var pre in node.Prereqs.Concat(node.SecondPrereqs))
            {
                if (MasteryCatalog.ById(pre) is not { } p) continue;
                var from = NodePos(p);
                // Prefer a TAKEN prerequisite when there is one, so the gold path follows the route the
                // player actually walked rather than whichever node happens to sit closest.
                var d = (from.X - to.X) * (from.X - to.X) + (from.Y - to.Y) * (from.Y - to.Y)
                        - (Mastery.IsTaken(pre) ? 1_000_000 : 0);
                if (d >= best) continue;
                best = d;
                nearest = p;
            }

            if (nearest is null) continue;
            var a = Screen(NodePos(nearest));
            var c = Screen(to);
            // AN UNWALKED WIRE IS DIMMER THAN THE NODE IT TOUCHES, which it was not before: Path is also
            // the locked-node frame tint, so a node and the fatter wire running into it were the same
            // colour and the node disappeared into the web. Half-alpha puts the wire behind the studs.
            var walked = Mastery.IsTaken(node.Id) && Mastery.IsTaken(nearest.Id);
            _ui.LineSeg(b, a, c, WireThickness, walked ? Gold : Path * 0.55f);
        }
        // THE FOUR DIRECTIONS, NAMED OUTSIDE THE RIM. Between the wires and the nodes, so a header is
        // never drawn over a plaque even if the arithmetic in HeaderRing is one day wrong.
        foreach (var br in new[] { Branch.Resonance, Branch.Loot, Branch.Tempo, Branch.Endure })
            DrawBranchHeader(b, br);

        foreach (var node in MasteryCatalog.Nodes) DrawNode(b, node, hit, aff);
    }

    /// <summary>
    /// The page's own top strip and its corner plate — drawn OVER the tree, not under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to be drawn first, so at any zoom past the overview the tree was painted straight
    /// through the screen's title: a capture at 0.95 reads "SIX SPECIALIS|TIONS · ONE DISCI|LINE"
    /// with node art in the gaps. Chrome belongs on top, and the input path already agrees — the reset
    /// button is hit-tested before any node, so drawing it underneath them was the one place where
    /// what the player saw and what the click did disagreed.
    /// </para>
    /// <para>
    /// <b>THE TITLE IS AT THE TOP, LIKE EVERY OTHER SCREEN.</b> Playtest 2026-08-30: <i>"do you think
    /// the game's other screens look like this? On the other screens the title is at the top, a rule
    /// under it, and then the screen's content begins."</i> STATS, THE FORGE, THE VAULT, WARREN, MAP,
    /// TRAITS, ROSTER and BUILD all draw exactly three things at exactly three coordinates — the name
    /// centred at (960, 24) in <see cref="UiTypography.ScreenTitle"/> and the ceremony face, a gold
    /// rule at y=74, one caption at (960, 80) — and this screen instead grew a title PLATE in its
    /// top-left corner. Those three lines are now copied here to the pixel.
    /// </para>
    /// <para>
    /// What the plate carried that is not a title — the mastery glyph, POINTS / SPENT, the four branch
    /// rows and TAKE EVERY POINT BACK — stayed on a frame, in the corner, as
    /// <see cref="PointsPanel"/>: the same arrangement TRAITS uses, where the screen names itself at
    /// the top and its point counter sits on a small plate at the left.
    /// </para>
    /// </remarks>
    private void DrawTreeChrome(SpriteBatch b, Point hit)
    {
        // ── THE TOP STRIP — the house pattern, to the pixel. ──
        _ui.TextCenterBig(b, "MASTERY TREE", UiKit.PageCenterX, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 260, 74, 520, 3), Gold * 0.5f);

        // The caption is the screen's, and only the screen's. Refusals and results speak in the inspector,
        // beside the button that raised them (UX V2 P1.5) — not five hundred pixels away under the title.
        _ui.TextCenterBig(b, "FOUR DIRECTIONS  ·  ONE STYLE  ·  TWELVE SKILLS TO LEARN", UiKit.PageCenterX, 80, Slate, UiTypography.Secondary);

        // ── THE CORNER PLATE: the points you can spend, what you have spent, and your style. ──
        _ui.PanelQuiet(b, PointsPanel);
        var px0 = PointsPanel.X + 22;
        var medal = new Rectangle(px0, PointsPanel.Y + 20, 56, 56);
        if (_ui.Assets.Get("ui_medallion_round") is { } ring) b.Draw(ring, medal, Color.White);
        if (_ui.Assets.Get("state_mastery_128") is { } glyph)
            b.Draw(glyph, new Rectangle(medal.X + 13, medal.Y + 13, 30, 30), Gold);
        // NEVER SLATE AT ZERO: the most important figure on the page must not read as switched off in
        // the exact state where the player most needs to read it. Gold when there is something to spend.
        _ui.TextBig(b, $"{Mastery.Available}", medal.Right + 14, PointsPanel.Y + 14,
                    Mastery.Available > 0 ? Gold : Bone, UiTypography.PrimaryValue);
        _ui.TextBig(b, "AVAILABLE", medal.Right + 14 + _ui.MeasureBig($"{Mastery.Available}", UiTypography.PrimaryValue) + 10,
                    PointsPanel.Y + 14 + UiTypography.PrimaryValue - UiTypography.Secondary - 2, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{Mastery.Spent} SPENT", medal.Right + 14, PointsPanel.Y + 14 + UiTypography.Pitch(UiTypography.PrimaryValue), Slate, UiTypography.Secondary);
        var styleLine = Mastery.Affinity() is { } st ? $"STYLE · {Short(st)}  x2.0" : "NO STYLE YET";
        _ui.TextBig(b, _ui.ShortenBig(styleLine, PointsPanel.Width - 44, UiTypography.Body), px0, PointsPanel.Bottom - 22 - UiTypography.Body,
                    Mastery.Affinity() is not null ? Gold : Slate, UiTypography.Body);

        if (Mastery.Spent > 0)
            Button(b, ResetBtn, _resetArmed ? "PRESS AGAIN TO CONFIRM" : "TAKE EVERY POINT BACK", hit, true);
    }

    private void DrawBranchHeader(SpriteBatch b, Branch br)
    {
        if (_zoom <= LabelZoom) return;

        var p = Screen(Corner(br) * HeaderRing);
        // Same cull as the nodes, widened for the header's own text box.
        if (p.X < TreeView.X - 320 || p.X > TreeView.Right + 320
            || p.Y < TreeView.Y - 320 || p.Y > TreeView.Bottom + 320) return;

        var col = BranchColor(br);
        // A direction the player has actually walked burns; one they have not is still legible but
        // quiet. Bridges are excluded: a bridge is filed under one branch but belongs to neither.
        // START is filed under the first branch (it has to be filed somewhere) and is always taken, so without
        // the Start test WEIGHT burned as "walked" on a brand-new hunter who has spent nothing.
        var walked = MasteryCatalog.Nodes.Any(n => n.Branch == br && n.Kind != MasteryKind.Bridge
                                                   && n.Kind != MasteryKind.Start && Mastery.IsTaken(n.Id));
        _ui.TextCenterBig(b, Short(br), (int)p.X, (int)p.Y - 23, walked ? col : col * 0.72f,
                          UiTypography.PanelTitle);
        _ui.TextCenterBig(b, BranchPromise(br), (int)p.X, (int)p.Y + 7, Slate, UiTypography.Secondary);
    }

    /// <summary>
    /// The ceremony's own label clearance. Its hexagon is twice the docked one's, on a modal with room
    /// to spare, so its names stand further off their seals — but off the SEAL, like the chart's, so
    /// the two drawings of the same object are the same drawing.
    /// </summary>
    private const int CeremonyLabelGap = 20;

    /// <summary>The ceremony's hexagon and seals — the same drawing, at the size a modal can afford.</summary>
    /// <remarks>
    /// Derived the same way as <see cref="HexRadius"/>, against the ceremony's own field: 246 px per
    /// half, less an 18 px margin, less the seal's rim (38) + the gap (20) + one Body line (19) —
    /// which lands back on the 150 the ceremony has always drawn at.
    /// </remarks>
    private const int CeremonyRadius = 150;

    /// <inheritdoc cref="CeremonyRadius"/>
    private const int CeremonySealPx = 62;   // ui-size-ok: the seal medallion's edge in pixels, not a text size

    /// <summary>
    /// THE ATTUNEMENT — the ceremony for the first Specialisation.
    /// </summary>
    /// <remarks>
    /// The game's founding system deserves a moment, not a click-sound. Deliberately NOT the
    /// inspiration's divination scene: this game's language is the weave, so the ceremony is the
    /// loom shown whole — six seals, your thread bound in gold — and a choice you may still hand
    /// back. Sealing changes nothing the Take didn't already do; the ceremony IS the information.
    /// </remarks>
    private void DrawSpecialisation(SpriteBatch b, Point hit)
    {
        _ui.Scrim(b, 0.75f);
        _ui.Panel(b, SpecPanel, gold: true);

        var name = _specStyle.ToString().ToUpperInvariant();
        _ui.TextCenterBig(b, "YOUR SPECIALISATION", SpecPanel.Center.X, UiKit.TitleTop(SpecPanel), Gold, UiTypography.PanelTitle);
        _ui.TextCenter(b, "SIX STYLES — ONE IS YOURS.", SpecPanel.Center.X, UiKit.CaptionTop(SpecPanel), Slate);

        // THE CEREMONY STANDS ON THE SAME FIELD THE DOCKED CHART DOES. It used to have none — the
        // hexagon floated on the modal's black interior — so the game's founding moment was the one
        // place the chart was drawn without the surface that makes it a chart.
        var field = new Rectangle(SpecPanel.Center.X - 330, SpecPanel.Y + 86, 660, 492);
        StyleAffinityDiagram.Field(_ui, b, field);
        StyleAffinityDiagram.Draw(_ui, b, field.Center, CeremonyRadius, _specStyle, showFactors: true,
                            labelGap: CeremonyLabelGap, labelPx: UiTypography.Body,
                            factorPx: UiTypography.Secondary, sealPx: CeremonySealPx);

        _ui.TextCenter(b, $"YOUR {name} SKILLS HIT TWICE AS HARD. THE FAR STYLES HIT SOFTER —",
                       SpecPanel.Center.X, SpecPanel.Y + 586, Bone);
        _ui.TextCenter(b, "A VOW ON A FAR-STYLE SKILL PULLS IT ONE RING CLOSER.",
                       SpecPanel.Center.X, SpecPanel.Y + 610, Bone);

        Button(b, SpecSealBtn, $"CHOOSE {name}", hit, true);
        Button(b, SpecUndoBtn, "NOT YET — TAKE THE POINTS BACK", hit, true);
    }

    /// <summary>The label's tail — what the node DOES, after the em dash that follows its name.</summary>
    private static string Tail(string label)
    {
        var cut = label.IndexOf('—');
        return cut < 0 ? "" : label[(cut + 1)..].Trim();
    }

    /// <summary>
    /// THE INSPECTOR (D3): the node under the pointer, or the last one clicked — CATEGORY · NAME · what it
    /// does · what you need first · what it costs · a refusal line · the one button.
    /// </summary>
    /// <remarks>
    /// Hover shows, click PINS — including a node you cannot afford, which is the one you most want to read
    /// before deciding what to walk toward. A Specialisation draws the style hexagon here, where the
    /// decision is made, instead of as a standing panel that was an empty state for most of the game.
    /// Nothing here re-derives a rule: the refusal is what <see cref="MasteryTree.Take"/> would say.
    /// </remarks>
    private void DrawNodeDetail(SpriteBatch b, Point hit)
    {
        var panel = InspectorPanel;
        _ui.PanelQuiet(b, panel);
        var x = UiKit.ContentLeft(panel);
        var w = UiKit.ContentRight(panel) - x;
        var y = panel.Y + UiTypography.PanelTitleTop;
        var btn = TakeBtn;
        var floor = btn.Y - 40;

        void Section(string s, Color? c = null)
        {
            if (y + UiTypography.Pitch(UiTypography.Secondary) > floor) return;
            _ui.TextBig(b, s, x, y, c ?? Slate, UiTypography.Secondary); y += UiTypography.Pitch(UiTypography.Secondary);
        }
        void Line(string s, Color c, int px = 0, int maxLines = 3)
        {
            if (px == 0) px = UiTypography.Body;   // a rung is a profile-scaled property, not a constant
            foreach (var l in _ui.WrapBig(s, w, px).Take(maxLines))
            {
                if (y + UiTypography.Pitch(px) > floor) return;
                _ui.TextBig(b, l, x, y, c, px); y += UiTypography.Pitch(px);
            }
        }
        void Rule() { if (y + 14 < floor) { _ui.Fill(b, new Rectangle(x, y + 6, w, 1), Dim); y += 16; } }

        var id = _hoverNodeId ?? _pinnedNodeId;
        if (id is null || MasteryCatalog.ById(id) is not { } n)
        {
            Section("MASTERY TREE");
            _ui.TextBig(b, "PICK A NODE TO READ IT", x, y, UiInk.Empty, UiTypography.Headline); y += UiTypography.Pitch(UiTypography.Headline) + 4;
            Line("Hover shows a node here; click pins it. Take it with the button below.", Slate);
            Rule();
            Section("WHAT EACH KIND COSTS IN POINTS");
            Line("MINOR 1  ·  NOTABLE 3  ·  GREATER 5", Bone);
            Line("SKILL 5  ·  SPECIALISATION 6  ·  CAPSTONE 8", Bone);
            Rule();
            Section("HOW POINTS ARE EARNED");
            Line("The first time you reach a new depth in a region. Deeper pays more; every region pays.", Bone, UiTypography.Body, 3);
            Line("Points buy about one full branch — choose a direction.", Slate, UiTypography.Secondary, 2);
            Rule();
            Section("CONTROLS");
            Line("DRAG TO MOVE  ·  WHEEL TO ZOOM  ·  HOME SHOWS THE WHOLE TREE", Slate, UiTypography.Secondary, 2);
            Line("RIGHT-CLICK A TAKEN NODE TO GIVE IT BACK  ·  RESPEC IS FREE", Slate, UiTypography.Secondary, 2);
            return;
        }

        var col = BranchColor(n.Branch);
        var taken = Mastery.IsTaken(n.Id);
        var can = Mastery.CanTake(n.Id);
        var roadDef = n.Kind == MasteryKind.SkillRoad && n.GrantsSkillId is { } rs ? SkillCatalogue.Find(rs) : null;
        var learned = roadDef is not null && Mastery.LearnedSkills().Contains(roadDef.Id);

        // CATEGORY · BRANCH, in the branch's colour; NAME at Headline — gold once it is yours.
        _ui.Fill(b, new Rectangle(x, y - 6, 5, UiTypography.Pitch(UiTypography.Secondary) + UiTypography.Pitch(UiTypography.Headline)), col);
        var xs = x; x += 16; w -= 16;
        var kindHead = n.Kind switch { MasteryKind.Specialisation => "SPECIALISATION", MasteryKind.SkillRoad => "SKILL", MasteryKind.Mastery => "CAPSTONE", _ => KindWord(n.Kind) };
        Section(_ui.ShortenBig($"{kindHead}  ·  {Short(n.Branch)}{(n.Link is { } lk ? $" + {Short(lk)}" : "")}", w, UiTypography.Secondary), col);
        var name = roadDef is not null ? roadDef.Name
                 : n.Kind == MasteryKind.Specialisation && n.Style is { } ss ? $"{Short(ss)} SPECIALISATION"
                 : Head(n.Label);
        if (roadDef is not null) { _ui.Icon(b, $"icon_skill_{roadDef.Id}", new Rectangle(x, y - 2, 34, 34), learned || taken ? Gold : Bone); _ui.TextBig(b, name, x + 44, y, taken ? Gold : Bone, UiTypography.Headline); }
        else _ui.TextBig(b, name, x, y, taken ? Gold : Bone, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + 6;
        x = xs; w += 16;

        // WHAT IT DOES: the label's tail, the stat lines, the kind's own sentence.
        Section("WHAT IT DOES");
        var tail = Tail(n.Label);
        if (tail.Length > 0) Line(tail, Bone, UiTypography.Body, 3);
        if (n.Stats is { Count: > 0 } stats)
            foreach (var kv in stats.Take(3)) Line($"+{kv.Value:0} {kv.Key.ToString().ToUpperInvariant()}", Bone, UiTypography.Body, 1);
        if (roadDef is not null)
        {
            Line($"TEACHES {roadDef.Name.ToUpperInvariant()} — {roadDef.Line}", Bone, UiTypography.Body, 3);
            Line(learned ? "LEARNED — FOR GOOD. RESPEC RETURNS THE POINTS, NEVER THE SKILL." : "LEARNING IS PERMANENT: RESPEC RETURNS THE POINTS, NEVER THE SKILL. EQUIP IT ON THE BUILD SCREEN.",
                 learned ? Gold : Slate, UiTypography.Secondary, 3);
        }
        if (n.Kind == MasteryKind.Specialisation && n.Style is { } specStyle)
        {
            var mine = Mastery.Affinity();
            var f = Short(specStyle);
            Line(taken ? $"{f} IS YOUR STYLE. YOUR {f} SKILLS HIT TWICE AS HARD; SKILLS FAR FROM IT HIT SOFTER."
                 : mine is null ? $"TAKING IT MAKES {f} YOUR STYLE: {f} SKILLS HIT TWICE AS HARD, FAR STYLES SOFTER. ONE STYLE PER HUNTER."
                 : $"YOUR STYLE IS ALREADY {Short(mine.Value)}. ONE STYLE PER HUNTER — TAKE EVERY POINT BACK TO CHOOSE AGAIN.",
                 taken || mine is null ? Bone : Slate, UiTypography.Body, 4);
            // THE HEXAGON, where the decision is made: this style at the centre, the six factors around it.
            var need = (InspectorHexRadius + InspectorSealPx + 8 + UiTypography.Body) * 2 + 24;
            if (y + need < floor)
            {
                var field = new Rectangle(x, y + 8, w, need - 16);
                StyleAffinityDiagram.Field(_ui, b, field);
                StyleAffinityDiagram.Draw(_ui, b, field.Center, InspectorHexRadius, specStyle, showFactors: true,
                                          labelGap: 8, labelPx: UiTypography.Secondary, factorPx: UiTypography.Secondary, sealPx: InspectorSealPx);
                y += need;
            }
        }
        if (n.Kind == MasteryKind.Mastery)
            Line(Mastery.MasteredBranch() is { } mb && mb != n.Branch ? $"YOU ALREADY TOOK THE {Short(mb)} CAPSTONE — ONE CAPSTONE PER HUNTER." : "THE BRANCH'S CAPSTONE — ONE PER HUNTER.",
                 Slate, UiTypography.Secondary, 2);
        Rule();

        // YOU NEED FIRST: the prerequisites BY NAME, with their state in words.
        if (n.Prereqs.Count > 0 && !taken)
        {
            Section("YOU NEED FIRST");
            var names = n.Prereqs.Select(MasteryCatalog.ById).Where(p => p is not null).Select(p => (Name: p!.Kind == MasteryKind.Start ? "START" : Head(p.Label), Taken: Mastery.IsTaken(p.Id))).ToList();
            var anyTaken = names.Any(p => p.Taken);
            var shown = names.Where(p => p.Taken).Concat(names.Where(p => !p.Taken)).Take(3).ToList();
            Line((names.Count > 1 ? "ANY OF  " : "") + string.Join("  ·  ", shown.Select(p => $"{p.Name.ToUpperInvariant()} {(p.Taken ? "(TAKEN)" : "(NOT YET)")}")) + (names.Count > 3 ? " …" : ""),
                 anyTaken ? UiInk.Good : Bone, UiTypography.Body, 3);
            if (n.SecondPrereqs.Count > 0)
            {
                var second = n.SecondPrereqs.Select(MasteryCatalog.ById).Where(p => p is not null).Select(p => (Name: Head(p!.Label), Taken: Mastery.IsTaken(p.Id))).ToList();
                Line("AND ONE OF  " + string.Join("  ·  ", second.Take(3).Select(p => $"{p.Name.ToUpperInvariant()} {(p.Taken ? "(TAKEN)" : "(NOT YET)")}")),
                     second.Any(p => p.Taken) ? UiInk.Good : Bone, UiTypography.Body, 3);
            }
            Rule();
        }

        // WHAT IT COSTS · YOU HAVE.
        if (y + UiTypography.Pitch(UiTypography.Headline) < floor)
        {
            _ui.TextBig(b, taken ? "PAID" : "COST", x, y + 6, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{n.Cost} POINT{(n.Cost == 1 ? "" : "S")}", x + w, y, taken ? Gold : can ? Bone : Ember, UiTypography.Headline);
            y += UiTypography.Pitch(UiTypography.Headline);
        }
        if (!taken && y + UiTypography.Pitch(UiTypography.Headline) < floor)
        {
            _ui.TextBig(b, "YOU HAVE", x, y + 6, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{Mastery.Available}", x + w, y, Mastery.Available >= n.Cost ? Bone : Ember, UiTypography.Headline);
            y += UiTypography.Pitch(UiTypography.Headline);
        }

        // THE REFUSAL, beside the button that would have done it; then the button.
        var refusal = _msg.Length > 0 ? _msg
            : taken || can ? ""
            : Mastery.Available < n.Cost ? $"NEEDS {n.Cost} POINTS — YOU HAVE {Mastery.Available}. GO DEEPER."
            : n.Kind == MasteryKind.Specialisation && Mastery.Affinity() is not null ? "ONE STYLE PER HUNTER — ALREADY CHOSEN."
            : n.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() is not null ? "ONE CAPSTONE PER HUNTER — ALREADY TAKEN."
            : "TAKE A CONNECTED NODE FIRST.";
        if (refusal.Length > 0)
            _ui.TextBig(b, _ui.ShortenBig(refusal, w, UiTypography.Secondary), x, btn.Y - 30, _msg.Length > 0 && (taken || can) ? UiInk.Good : Ember, UiTypography.Secondary);
        var label = taken ? "GIVE BACK" : $"TAKE  ·  {n.Cost} POINT{(n.Cost == 1 ? "" : "S")}";
        _ui.Button(b, btn, label, hit, false, taken || can, !taken && can ? ButtonStyle.Primary : ButtonStyle.Secondary);
    }

    /// <summary>
    /// Word-wrap a sentence into the panel, and return the y the NEXT block may start at.
    /// </summary>
    /// <remarks>
    /// It used to return nothing, so every block under it was pinned to a hand-picked constant and the
    /// panel could only ever hold one paragraph. The Specialisation card holds two.
    /// </remarks>
    private int DrawWrapped(SpriteBatch b, string text, int x, int y, int width, Color c)
    {
        foreach (var line in _ui.WrapBig(text, width, UiTypography.Label)) { _ui.Text(b, line, x, y, c); y += UiTypography.Pitch(UiTypography.Label); }
        return y;
    }

    /// <summary>
    /// What a node IS, in the words the rest of the screen uses for the same thing.
    /// </summary>
    /// <remarks>
    /// MASTERY became BRANCH CAPSTONE and FORM SPECIALIST became SPECIALISATION because the tree was
    /// using two words for each of two things and the player had to guess which was which. "MASTERY"
    /// was the screen's own name AND the ring-4 kind AND, in a message, the Form discipline; "FORM
    /// SPECIALIST" appeared here while the hexagon panel, the weave screen and the ceremony all said
    /// discipline or attunement. One word for one thing: a ring-4 node is the CAPSTONE of its branch,
    /// and the six Form nodes are SPECIALISATIONS, of which your one is your DISCIPLINE.
    /// </remarks>
    private static string KindWord(MasteryKind k) => k switch
    {
        MasteryKind.Minor => "MINOR",
        MasteryKind.Notable => "NOTABLE",
        MasteryKind.Greater => "GREATER",
        MasteryKind.Mastery => "BRANCH CAPSTONE",
        MasteryKind.Bridge => "BRIDGE",
        MasteryKind.Specialisation => "SPECIALISATION — CHOOSES YOUR STYLE",
        // A road node used to fall through to "START" — the tree's most consequential kind wearing
        // the label of its most trivial one.
        MasteryKind.SkillRoad => "SKILL — LEARNED FOR GOOD",
        _ => "START",
    };

    /// <summary>The same hue, darker — opaque, so it dims rather than turning translucent.</summary>
    /// <remarks>
    /// <c>colour * f</c> scales ALPHA with the channels, which over a dark page reads as "disappearing"
    /// rather than "quiet". This keeps the node solid and only takes the light out of it.
    /// </remarks>
    private static Color Muted(Color c, float f) => new((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));

    private static Color BranchColor(Branch b) => b switch
    {
        Branch.Resonance => new Color(0xD6, 0x48, 0x5C),
        Branch.Loot => new Color(0x48, 0xB8, 0x88),
        Branch.Tempo => new Color(0x74, 0xC6, 0xE8),
        _ => new Color(0xC0, 0x6E, 0xE0),
    };

    /// <summary>The socket a node is set in. Shape says KIND — how big a commitment this is.</summary>
    /// <remarks>
    /// THE CAPSTONE WEARS THE GREATER'S MEDALLION, LARGER. It wore a wide plaque (ui_node_mastery), and
    /// a playtester read the four plaques as "the masteries in general" — a different KIND of thing
    /// from the round studs, rather than the biggest of them. Playtest 2026-08-27: "if those nodes are
    /// big power-ups, we can convey that with SIZE." So it is the same spiked ring as a Greater, drawn
    /// a size up with a thicker halo in the branch colour and a larger glyph, and nothing on the tree
    /// is a rectangle any more — the BRIDGE's chain-link square went the same way and wears the plain
    /// ring around its hexagonal field, a shape no other kind has. The plaque and chain art stay on
    /// disk, unused.
    /// </remarks>
    private static string KindFrame(MasteryKind k) => k switch
    {
        MasteryKind.Start => "ui_node_start",
        MasteryKind.Minor => "ui_node_minor",
        MasteryKind.Notable => "ui_node_notable",
        MasteryKind.Greater => "ui_node_greater",
        MasteryKind.Mastery => "ui_node_greater",
        MasteryKind.Bridge => "ui_node_minor",
        MasteryKind.SkillRoad => "ui_node_greater",   // priced like one, framed like one
        _ => "ui_node_spec",
    };

    /// <summary>What is set in the socket. The glyph says BRANCH — which of the four roads this is.</summary>
    private static string BranchGlyph(Branch b) => b switch
    {
        Branch.Resonance => "icon_branch_resonance",
        Branch.Loot => "icon_branch_loot",
        Branch.Tempo => "icon_branch_tempo",
        _ => "icon_branch_endure",
    };

    // THE SKILL EDITOR MOVED OUT. It was a column of `< VALUE >` cycling cells wedged into this page's
    // right margin, and it lived here only because the tree happened to have spare width. Cycling is
    // the wrong verb for a list of six — picking SPIRIT from BODY was five clicks and five reads, with
    // nothing on screen naming the other five — and a Vow's whole bargain (does my BUILD meet its
    // demand?) could not be answered from a cell that showed one word at a time.
    //
    // LoadoutScreen owns it now, at full width, with every Source, every Form and every studied Vow
    // visible at once and each Vow's demand checked live against the build. Deleted rather than left
    // behind a flag: two editors for one loadout is the parallel-systems failure this codebase has
    // been bitten by twice already.

    /// <summary>
    /// One node, drawn through the camera.
    /// </summary>
    /// <remarks>
    /// Radius, outline and label all scale with zoom, so zooming out is a real overview rather than the
    /// same glyphs at the same size on a shrinking layout — which is what makes a big tree navigable at
    /// all. Labels vanish below a threshold: forty overlapping words is less readable than none, and the
    /// detail panel is where reading happens anyway.
    /// </remarks>
    private void DrawNode(SpriteBatch b, MasteryNode node, Point mouse, Style? aff)
    {
        var w = NodePos(node);
        var sp = Screen(w);
        var rad = Math.Max(4, (int)(NodeRadius(node.Kind) * _zoom * 1.9f));

        // Cull. A tree meant to grow will one day have far more nodes off screen than on it.
        if (sp.X < TreeView.X - 200 || sp.X > TreeView.Right + 200
            || sp.Y < TreeView.Y - 200 || sp.Y > TreeView.Bottom + 200) return;

        var cx = (int)sp.X;
        var cy = (int)sp.Y;
        var box = new Rectangle(cx - rad, cy - rad, rad * 2, rad * 2);
        var hover = !OverDock(mouse)
                    && Math.Abs(mouse.X - cx) <= rad + 6 && Math.Abs(mouse.Y - cy) <= rad + 6;

        var taken = Mastery.IsTaken(node.Id);
        var canTake = Mastery.CanTake(node.Id);
        var branchCol = BranchColor(node.Branch);

        // Colour says BRANCH; brightness says state. A field of identical grey boxes told the player
        // neither, and the tree's whole shape — four opposed roads — was invisible until they read
        // labels one at a time.
        var fill = taken ? branchCol : canTake ? branchCol * 0.34f : new Color(0x14, 0x11, 0x1E, 0xE0);
        var edge = taken ? Gold : canTake ? branchCol : Path;
        var thick = Math.Max(2, (int)(4 * _zoom * 1.6f));

        // THE ART. Three channels, one fact each: the FRAME's shape is the kind, the GLYPH inside it is
        // the branch, and the tint on both is the state. Before this the three facts shared one channel
        // — a coloured square, sized by kind, with a smaller square in the middle of the expensive ones
        // — so "which road is this" and "how much does it cost me" were both answered in area and hue,
        // the two things a player reads last.
        //
        // Stretched, not fitted: these are frames, and a frame that letterboxes stops framing what is
        // inside it. Every frame is square art in a square box now that the capstone is round too.
        var frame = _ui.Assets.Get(KindFrame(node.Kind));
        var owned = node.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() == node.Branch;

        // THE CAPSTONE'S HALO. Size is what says "the big one", and a halo in the branch colour a step
        // outside the medallion is how the size is made legible at the whole-tree framing, where the
        // medallion itself is under thirty pixels across. Drawn before the frame so the frame's spikes
        // sit on top of it. Gold once the branch is mastered, like the frame.
        if (node.Kind == MasteryKind.Mastery)
        {
            var haloR = rad * 1.16f;
            var haloT = Math.Max(2f, rad * 0.11f);
            DrawRing(b, new Vector2(cx, cy), haloR, haloT,
                     owned || taken ? Gold : canTake ? branchCol : Muted(branchCol, 0.55f));
        }

        // THE SIX UNCHOSEN SPECIALISATIONS BREATHE. While the hunter has no discipline, the one
        // decision on this page that cannot be undone by a respec is also the one the eye has no
        // reason to land on — six nodes among sixty-three. A slow gold halo is the cheapest honest way
        // to say "these are not ordinary nodes"; the moment a discipline exists it stops, because then
        // the announcement would be nagging about a choice already made.
        if (node.Kind == MasteryKind.Specialisation && aff is null && !taken)
        {
            var pulse = 0.55f + 0.45f * MathF.Sin((float)_breath.Elapsed.TotalSeconds * 2.4f);
            for (var ring = 3; ring >= 1; ring--)
            {
                var g = (int)(box.Width * 0.09f * ring);
                _ui.Diamond(b, new Rectangle(box.X - g, box.Y - g, box.Width + g * 2, box.Height + g * 2),
                            Gold * (0.14f * pulse));
            }
        }

        if (frame is not null)
        {
            // The socket's field, inset well inside the rim so it fits the DIAMOND and the octagon too,
            // not just the round frames. This is the only surface that goes bright when a node is taken.
            //
            // START is exempt. It is permanently allocated, so it takes the "taken" branch of `fill`
            // and came out as a bright WEIGHT-red square at the dead centre of the tree — reading as
            // the most emphatic Weight node on the page when it belongs to no branch at all.
            // THE FIELD FOLLOWS THE FRAME'S SHAPE. It was an axis-aligned Rectangle inset 30% into every
            // frame — a square patch inside a circle, an octagon and a diamond, so a taken Minor read as
            // a coloured square wearing a ring and the DIAMOND kind wore a square that poked out of its
            // own points. The three procedural shapes UiKit already owns cover all four frame outlines.
            //
            // START is exempt. It is permanently allocated, so it takes the "taken" branch of `fill` and
            // came out as a bright WEIGHT-red field at the dead centre of the tree — reading as the most
            // emphatic Weight node on the page when it belongs to no branch at all.
            // A SPECIALISATION IS NEVER A GHOST. Every other node carries a branch GLYPH, which is what
            // gives a locked one its mass; a Specialisation carries the name of its Form instead. Under
            // the generic locked treatment — a near-black field behind a Path-tinted frame — the five
            // a hunter did not choose became captions floating in empty space the moment a discipline
            // existed. They keep a dim branch-tinted body, so "closed" reads as closed and not as a
            // rendering fault.
            var lockedSpec = node.Kind == MasteryKind.Specialisation && !taken && !canTake;
            var pad = (int)(box.Width * 0.30f);
            var field = new Rectangle(box.X + pad, box.Y + pad, box.Width - pad * 2, box.Height - pad * 2);
            var fieldCol = node.Kind == MasteryKind.Start ? PanelBg
                           : lockedSpec ? Muted(branchCol, 0.30f) : fill;
            if (node.Kind == MasteryKind.Specialisation) _ui.Diamond(b, field, fieldCol);
            else if (node.Kind == MasteryKind.Notable || node.Kind == MasteryKind.Bridge) _ui.Hex(b, field, fieldCol);
            else _ui.Diamond(b, Circleish(field), fieldCol);

            b.Draw(frame, box, hover ? Bone : owned || taken ? Gold : canTake ? branchCol
                               : lockedSpec ? Muted(branchCol, 0.72f) : Path);
        }
        else
        {
            // No art on disk: the flat shapes this screen shipped with. The game must run with an empty
            // assets/art, so every draw site keeps its greybox rather than borrowing someone else's art.
            _ui.Fill(b, box, node.Kind == MasteryKind.Start ? PanelBg : fill);
            Outline(b, box, node.Kind == MasteryKind.Start ? Slate : hover ? Bone : edge,
                    owned ? thick * 2 : thick);
        }

        if (node.Kind == MasteryKind.Start)
        {
            // >= the default zoom, not >. The threshold was 0.3 and HOME resets to exactly 0.30, so the
            // one label naming the centre of the tree was absent from the default view of it.
            if (_zoom > LabelZoom) _ui.TextCenter(b, "YOU", cx, cy - 12, Bone);
        }
        else if (node.Kind == MasteryKind.Specialisation && _zoom > LabelZoom)
        {
            // THE SECOND-LARGEST NODE, AND THE ONLY OTHER ONE THAT SPEAKS. Its Form goes inside the
            // diamond because the Form IS the choice; the word underneath names the kind, so a player
            // can count the six of them without hovering one.
            // Bright while the discipline is still open, quiet once it is spent. The five a hunter did
            // NOT take are dead content — a bone-white Form name over an unlit frame read as a bug in
            // the capture, a caption floating in empty space.
            _ui.TextCenterBig(b, node.Style is { } sf ? Short(sf) : "STYLE", cx, cy - 9,
                              taken ? new Color(0x14, 0x11, 0x1E) : aff is null ? Bone : Muted(Bone, 0.62f),
                              UiTypography.Secondary);
            // INWARD, toward the centre: on the south arms the road node stands just outside this diamond and
            // used to print through the caption ("S ECIALISATION").
            var specCapY = w.Y > 1f ? box.Top - 6 - UiTypography.Secondary : box.Bottom + 5;
            _ui.TextCenterBig(b, "SPECIALISATION", cx, specCapY,
                              taken ? Gold : aff is null ? Muted(Gold, 0.90f) : Muted(Slate, 0.72f),
                              UiTypography.Secondary);
        }
        // The branch glyph, over the frame's hollow centre. Below about twenty pixels it is a smudge
        // that only muddies the socket, and the frame alone still carries the kind — so it drops out
        // rather than degrading, the same way the labels do.
        // A ROAD NODE WEARS THE SKILL IT TEACHES (UX V2 P1.5) — the twelve most consequential nodes on the
        // tree used to carry the same branch glyph as a one-point minor. A small gold mark says DISCOVERED:
        // the skill is learned for good, and the mark survives a respec that takes the points back.
        else if (node.Kind == MasteryKind.SkillRoad && node.GrantsSkillId is { } roadSkill && box.Width >= 20
                 && _ui.Assets.Get($"icon_skill_{roadSkill}") is { } skillGlyph)
        {
            var g = (int)(box.Width * 0.50f);
            var learnedRoad = Mastery.LearnedSkills().Contains(roadSkill);
            _ui.SpriteFit(b, skillGlyph, new Rectangle(cx - g / 2, cy - g / 2, g, g),
                          taken ? new Color(0x14, 0x11, 0x1E) : learnedRoad ? Gold : canTake ? branchCol : new Color(0x4A, 0x46, 0x58));
            if (learnedRoad && !taken)
            {
                var bd = Math.Max(8, rad / 3);
                _ui.Diamond(b, new Rectangle(box.Right - bd, box.Y - bd / 4, bd, bd), Gold);
            }
        }
        else if (box.Width >= 20
                 && _ui.Assets.Get(BranchGlyph(node.Branch)) is { } glyph)
        {
            // A capstone's glyph fills more of its medallion: the third size cue after the frame and the
            // halo, and the one that survives the whole-tree framing best.
            var g = (int)(box.Width * (node.Kind == MasteryKind.Mastery ? 0.54f : 0.46f));
            // Dark on a lit field once taken, lit on a dark field before: whichever way round, the
            // glyph is the thing with contrast against what is behind it.
            var tint = taken ? new Color(0x14, 0x11, 0x1E) : canTake ? branchCol : new Color(0x4A, 0x46, 0x58);
            _ui.SpriteFit(b, glyph, new Rectangle(cx - g / 2, cy - g / 2, g, g), tint);
        }
        else if (frame is null && _zoom > 0.34f
                 && node.Kind is MasteryKind.Notable or MasteryKind.Greater
                                 or MasteryKind.Bridge or MasteryKind.Specialisation)
        {
            // The greybox kind mark, kept for the no-art path only.
            var pip = Math.Max(3, rad / 4);
            _ui.Fill(b, new Rectangle(cx - pip, cy - pip, pip * 2, pip * 2),
                     taken ? new Color(0x14, 0x11, 0x1E) : branchCol);
        }

        // A CAPSTONE WEARS ITS OWN NAME, under the medallion. It used to print Short(node.Branch) inside
        // a plaque — so the four biggest objects on the page read WEIGHT / SPREAD / TEMPO / ENDURE, the
        // names of the four DIRECTIONS and not of these nodes at all, and a playtester concluded the
        // plaques WERE the directions. The directions are labelled outside the rim now
        // (DrawBranchHeader); the medallion carries the branch glyph like every other node and says
        // what it is underneath. HeaderRing is measured against these two lines.
        //
        // OUTWARD, not below. The north capstone's captions drawn under it were printed straight
        // through the ring-3 greaters inside it (the first capture said so); on the north arm the
        // room is ABOVE the medallion, between it and the branch header, so the two lines stack away
        // from the centre there and below the node everywhere else. The name is always the line
        // nearer the medallion.
        if (node.Kind == MasteryKind.Mastery && _zoom > LabelZoom)
        {
            var nameCol = owned || taken ? Bone : branchCol;
            var kindCol = owned || taken ? Gold : Slate;
            if (w.Y < -1f)
            {
                _ui.TextCenterBig(b, Head(node.Label), cx, box.Top - 24, nameCol, UiTypography.Body);
                _ui.TextCenterBig(b, "CAPSTONE", cx, box.Top - 44, kindCol, UiTypography.Secondary);
            }
            else
            {
                _ui.TextCenterBig(b, Head(node.Label), cx, box.Bottom + 4, nameCol, UiTypography.Body);
                _ui.TextCenterBig(b, "CAPSTONE", cx, box.Bottom + 26, kindCol, UiTypography.Secondary);
            }
        }

        // NAMES ON THE CANVAS (UX V2 P1.5): every node that is a decision says what it is without a hover.
        // Body once the camera is near, Secondary at the whole-tree framing where a longer word would cross
        // its neighbour; a minor shows the stat it gives instead of a name.
        // Not at the whole-tree framing: ring-2 names cross their neighbours there, and the capstones and
        // specialisations already name the far view. From about half the first-open zoom the words fit.
        if (_zoom >= FirstOpenZoom * 0.55f && node.Kind is MasteryKind.Notable or MasteryKind.Greater or MasteryKind.Bridge or MasteryKind.SkillRoad)
        {
            var nm = node.Kind == MasteryKind.SkillRoad && node.GrantsSkillId is { } rs2 && SkillCatalogue.Find(rs2) is { } rd2 ? rd2.Name : Head(node.Label);
            var npx = _zoom >= FirstOpenZoom * 0.8f ? UiTypography.Body : UiTypography.Secondary;
            var ny = w.Y < -1f ? box.Top - 6 - npx : box.Bottom + 4;
            _ui.TextCenterBig(b, nm, cx, ny, taken || canTake ? Bone : Slate, npx);
        }
        else if (node.Kind == MasteryKind.Minor && _zoom >= FirstOpenZoom * 0.8f && node.Stats is { Count: > 0 } st0)
        {
            var kv0 = st0.First();
            _ui.TextCenterBig(b, $"+{kv0.Value:0}", cx, box.Bottom + 2, taken ? Bone : Slate, UiTypography.Secondary);
        }

        if (hover && node.Kind != MasteryKind.Start)
        {
            _hoverNodeId = node.Id;
        }
    }

    /// <summary>
    /// A ring of the given radius and stroke, as a polygon of short segments.
    /// </summary>
    /// <remarks>
    /// UiKit has no disc or ring primitive and this file must not add art. Forty-eight segments of
    /// LineSeg from the one pixel texture read as a circle at every zoom the tree reaches, and batch
    /// with everything else on the page.
    /// </remarks>
    private void DrawRing(SpriteBatch b, Vector2 centre, float radius, float thickness, Color col)
    {
        const int segments = 48;
        var prev = centre + new Vector2(radius, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var a = MathF.Tau * i / segments;
            var next = centre + new Vector2(MathF.Cos(a) * radius, MathF.Sin(a) * radius);
            _ui.LineSeg(b, prev, next, thickness, col);
            prev = next;
        }
    }

    /// <summary>
    /// One cycling row of a skill card. <paramref name="label"/> names what is being cycled.
    /// </summary>
    /// <remarks>
    /// The label is not decoration. All three rows of a card were arrow-flanked values with nothing
    /// saying which was which, so the Vow row — the only one whose value can be NONE — read as a status
    /// message rather than a control, and a player looking for "where do I set a Vow" had no reason to
    /// think the row that said NO VOW was the answer.
    /// </remarks>
    private void DrawCell(SpriteBatch b, Rectangle r, string text, Color color, Point mouse, string label = "")
    {
        _ui.Fill(b, r, r.Contains(mouse) ? Hi : PanelBg);
        _ui.Text(b, "<", r.X + 4, r.Y + 12, Slate);
        _ui.Text(b, ">", r.Right - 28, r.Y + 12, Slate);
        if (label.Length > 0) _ui.Text(b, label, r.X + 30, r.Y + 12, Dim);
        _ui.TextCenter(b, text, r.Center.X, r.Y + 12, color);
    }

    /// <summary>
    /// A wire's thickness in pixels at the current zoom.
    /// </summary>
    /// <remarks>
    /// 5.7 is not a taste call. TraitsScreen — the tree the same playtester has NOT complained about —
    /// draws a 3px wire against a 52px node, a ratio of 1:17.3. A Minor node here is 98.8 * _zoom pixels
    /// across, and 98.8 / 17.3 = 5.7. So these wires land at exactly the weight the trait tree already
    /// ships, at every zoom, instead of a constant 8 that is 29% of a node at the default camera and 57%
    /// of one when the player zooms out to see the whole tree.
    /// </remarks>
    private float WireThickness => Math.Clamp(5.7f * _zoom, 1.2f, 9f);

    /// <summary>
    /// A field for a ROUND frame, drawn as a diamond grown to touch the circle it sits in.
    /// </summary>
    /// <remarks>
    /// UiKit has no disc primitive and this file must not add art. A diamond inscribed in the circle
    /// leaves visible gaps at the diagonals, so it is grown by sqrt(2)/2 of the inset instead: the
    /// points now reach the rim and the flats sit just inside it, which at a Minor's 28px default size
    /// reads as a filled socket rather than as a rotated square.
    /// </remarks>
    private static Rectangle Circleish(Rectangle r)
    {
        var grow = (int)MathF.Round(r.Width * 0.14f);
        return new Rectangle(r.X - grow, r.Y - grow, r.Width + grow * 2, r.Height + grow * 2);
    }

    private void Outline(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }
}
