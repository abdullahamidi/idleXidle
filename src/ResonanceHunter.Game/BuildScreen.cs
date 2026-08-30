using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Prestige;
using ResonanceHunter.Core.Persistence;

using ResonanceHunter.Core.Progression;
namespace ResonanceHunter.Client;

/// <summary>
/// The BUILD screen: a four-panel overview of the woven loadout — an identity card, the Source/Form/Vow
/// composition, the skill cards with their two actions docked beneath them, and the passives / resonance
/// column. The mastery tree is the SAME class in its other view (see ShowTree) and has its own rail tile.
/// </summary>
/// <remarks>
/// Built to the Build production spec (rev 1). All data is REAL: the game has ONE live loadout (not the
/// reference's named "saved loadouts" presets, which the spec's own layout drops), so the screen shows the
/// current build. Resonance is the real source composition of the equipped skills; passives are the taken
/// mastery nodes; there is no numeric "trait bonus %" table, so that is shown as the real passive list.
/// </remarks>
public sealed class BuildScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x2C, 0x2C, 0x36);
    private static readonly Color PanelBg = new(0x14, 0x11, 0x1A, 0xC8);
    private static readonly Color Quiet = new(0x16, 0x12, 0x20, 0xE0);
    private static readonly Color SbBg = new(0x0F, 0x0D, 0x15);
    private static readonly Color Hi = new(0x2A, 0x24, 0x14);
    private static readonly Color Path = new(0x39, 0x33, 0x44);
    private static readonly Color Verd = new(0x5A, 0x9A, 0x4A);
    private static readonly Color Purple = new(0x8A, 0x5A, 0xC8);

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x8A, 0x5A, 0xC8), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };
    private static string Short(Branch b) => b.ToString().ToUpperInvariant();

    private static string Short(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };

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
    private string? _attuneNodeId;         // non-null: THE ATTUNEMENT ceremony is open for this just-taken node
    private Form _attuneForm;

    /// <summary>The host reads this to hold the rail, its hotkeys, L and T while the ceremony is open.</summary>
    public bool CeremonyOpen => _attuneNodeId is not null;

    /// <summary>The docked plates — input over them must never reach the tree behind.</summary>
    /// <remarks>
    /// The corner plate joined the two right-hand cards when it became a solid plate (2026-08-29).
    /// While it was loose text on the wallpaper there was nothing for a click to land on; a frame you
    /// can see is a frame the pointer must respect, or dragging the tree by its own furniture pans the
    /// canvas under it. The reset button is hit-tested BEFORE this gate in <see cref="Update"/>, so it
    /// still works. (The top strip needs no entry: the canvas starts below it.)
    /// </remarks>
    private static bool OverDock(Point p) => HexPanel.Contains(p) || NodePanel.Contains(p) || PointsPanel.Contains(p);
    /// <summary>Set when the player asked for the weave editor. The host opens it and clears this.</summary>
    public bool WantsWeave { get; set; }
    private bool _resetArmed;   // the reset button has been pressed once and is waiting for the second
    private bool _editMode;   // false = the overview; true = the mastery-tree + skill editor sub-view

    /// <summary>Which of this screen's two views is open. The rail's BUILD and MASTERY tiles both set it.</summary>
    /// <remarks>
    /// The tree used to be reachable only from inside this screen, so the flag was private. It has its
    /// own rail tile now, and a tile that opened "whichever view you happened to leave open" would read
    /// as a broken button rather than as stale state.
    /// </remarks>
    public bool ShowTree
    {
        get => _editMode;
        // Arming survives no navigation. The two-click reset used to stay armed when the player left
        // BUILD and came back, so a single click on a screen they had just opened wiped the whole tree.
        set { _editMode = value; _resetArmed = false; }
    }

    /// <summary>
    /// Whether <see cref="Activity.Mastery"/> is open yet. Host-set, like every other fact here.
    /// </summary>
    /// <remarks>
    /// The rail refuses the MASTERY tile until wave 8, and OPEN THE MASTERY TREE on this screen walked
    /// straight past that — so the tree was reachable at wave 5 from a button drawn beside a tile that
    /// was dimmed and saying "REACH WAVE 8". Two doors into one room have to agree about the lock.
    /// </remarks>
    public bool TreeUnlocked { get; set; } = true;

    /// <summary>Open straight onto the tree — used by the headless capture so the shot shows the tree.</summary>
    /// <summary>DEV ONLY: open the tree sub-view, optionally framed on the centre at a given zoom.</summary>
    /// <remarks>
    /// The zoom argument exists because node ART cannot be verified from the default overview —
    /// at that scale a Notable is thirty pixels across and a capture proves only that something was
    /// drawn there. The whole point of a camera is that the same layout has a near view.
    /// </remarks>
    public void DevOpenTree(float zoom = 0f)
    {
        _editMode = true;
        _pan = Vector2.Zero;
        _zoom = zoom > 0f ? zoom : WholeTreeZoom;
    }

    /// <summary>DEV ONLY: open the tree the way a player's FIRST visit opens it — centre and ring 1.</summary>
    public void DevOpenTreeFirstVisit()
    {
        _editMode = true;
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

    public BuildScreen(UiKit ui) => _ui = ui;

    /// <summary>DEV: pose THE ATTUNEMENT ceremony for a capture, without touching the tree's state.</summary>
    public void DevAttune(Form form)
    {
        _attuneNodeId = MasteryCatalog.Nodes
            .First(n => n.Kind == MasteryKind.Specialisation && n.Form == form).Id;
        _attuneForm = form;
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

    // ── Spec §4 overview layout. ──
    private static readonly Rectangle SummaryPanel = new(40, 138, 360, 460);
    private static readonly Rectangle CorePanel = new(426, 138, 774, 450);
    // 330, NOT 278 — the buttons live inside it now. They sat at y=902 while this panel ended at 884,
    // so the two controls that act on the build floated on the dungeon wall below the frame holding it,
    // reading as chrome that belonged to no panel at all.
    private static readonly Rectangle AuraPanel = new(426, 606, 774, 330);
    // 800, not 746. This column is the only one on the screen whose height is DATA — six passives, up
    // to five resonance rows, then the keystone block — and at 746 the worst case ran off the bottom.
    // The extra 54 puts its foot level with the middle column's button row rather than short of it.
    private static readonly Rectangle PassivePanel = new(1228, 138, 652, 800);
    // Docked in AuraPanel's foot, sharing its interior width. EditBtn is gone: it ran the same line as
    // VIEW TREE, and the tree has its own rail tile now.
    private static readonly Rectangle WeaveBtn = new(466, 842, 337, 44);
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
    private static readonly Rectangle PointsPanel = new(40, 112, 200, 96);

    // ON THE TREE PAGE, NOT THE OVERVIEW. Playtest: "'Take all mastery points back' buranın butonu
    // değil, mastery tree'nin butonu." Right — it un-spends every point in a tree the overview does not
    // even draw, so pressing it there meant watching nothing happen to anything on screen. It sits
    // where BACK used to: MASTERY is a rail destination now, and a BACK button on a page with its own
    // door reads as "you are somewhere nested" when you are not (playtest asked what it even meant).
    //
    // INSIDE THE POINTS PLATE now, on its content width, rather than floating on the tree beneath it.
    private static readonly Rectangle ResetBtn =
        new(PointsPanel.X, PointsPanel.Bottom + 12, 300, 52);
    /// <summary>The i-th of <paramref name="n"/> auto-skill cards, sharing the panel's width between them.</summary>
    /// <remarks>
    /// Divided rather than fixed at a pitch of 182, which fitted exactly four and put a fifth at
    /// x=1180..1348 — off the end of a panel that stops at 1200 and across the passives column beside
    /// it. FIFTH WEAVE is a node the player can buy, so "four" was never a safe constant to lay out
    /// against; the cards now narrow to make room instead of walking off the edge.
    /// </remarks>
    private static Rectangle AuraCard(int i, int n)
    {
        const int gap = 14;
        var left = AuraPanel.X + UiKit.PanelCorner;
        var usable = AuraPanel.Width - UiKit.PanelCorner * 2;
        var w = (usable - (Math.Max(1, n) - 1) * gap) / Math.Max(1, n);
        return new Rectangle(left + i * (w + gap), 648, w, 178);
    }
    // Under the panel's top crest, which the title now clears too — at y=196 the button was drawn
    // straight through it.
    // Left-aligned to the panel's text column (PassivePanel.X + 52) and on its OWN row, below the
    // points line rather than beside it. See DrawPassives — sharing that row clipped the line.

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
    /// <see cref="DrawTreeChrome"/>) — and at the whole-tree framing this canvas plants the WEIGHT
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
    private static readonly Rectangle TreeView = new(180, 112, 1260, 952);

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
    /// Each line is a compression of that branch's own catalogue text, not a new claim: RESONANCE's
    /// nodes read "YOUR STRONG SOURCE MATCHUP PAYS 25% MORE" and "EVERY SOURCE MATCHUP COUNTS AS
    /// STRONG"; SPREAD's read "EVERY HIT
    /// STRIKES ONE MORE CREATURE" and "ALL FORMS +1 TARGET"; TEMPO's read "+35% ON THE FIRST HIT" and
    /// "COOLDOWNS -40%"; ENDURE's read "+20% MAXIMUM HEALTH" and "REGAIN 20% OF HEALTH BETWEEN WAVES".
    /// </remarks>
    private static string BranchPromise(Branch b) => b switch
    {
        Branch.Resonance => "STRONGER SKILLS, BEFORE YOU PICK ONE",
        Branch.Spread => "HIT MANY AT ONCE",
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

    // A DOCKED CARD, not a column. The tree is the page now; a 700px panel permanently taking a third
    // of the canvas is exactly the "sıkışmış" the layout was accused of.
    private static readonly Rectangle NodePanel = new(1408, 588, 496, 460);
    // Level with the corner plate opposite it (both start at 112, where the header strip ends), so the
    // three plates on this page read as one row hung under one header rather than as three arrivals.
    private static readonly Rectangle HexPanel = new(1408, 112, 496, 460);
    private static readonly Rectangle AttunePanel = new(480, 180, 960, 720);
    private static readonly Rectangle AttuneSealBtn = new(560, 816, 360, 52);
    private static readonly Rectangle AttuneUndoBtn = new(1000, 816, 360, 52);
    // Sized for FIVE skill cards, not four.
    //
    // The trait tree's spine sells a fifth weave, and the old geometry (four cards of 132 at a pitch of
    // 144, sidebar ending at 928) had no room for it — the fifth card would have run over the keystone
    // chips, and the keystone header already sat on top of slot four's Vow row in a capture. Everything
    // below is derived from fitting five cards plus a header plus three chips inside 1080.
    private static readonly Rectangle Sidebar = new(SbX, 200, 1920 - SbX - 16, 860);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>These are the TREE view's regions; the tour of MASTERY is the only tour this screen gets.</remarks>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.MasteryTree => new[] { TreeView },
        TourTarget.Specialisations => new[] { HexPanel },
        TourTarget.NodeCard => new[] { NodePanel, ResetBtn },
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
        if (_attuneNodeId is not null) { _dragFrom = null; _draggedThisPress = false; }

        if (_editMode && _attuneNodeId is null)
        {
            var over = Game1.ToOverlay(mouse);

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
        if (rightClicked && _editMode && !_draggedThisPress && _attuneNodeId is null)
        {
            var rhit = Game1.ToOverlay(mouse);
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
        var hit = Game1.ToOverlay(mouse);

        // ── THE ATTUNEMENT swallows the click while it is open. Seal keeps the node; undo is a
        //    real Refund, so backing out costs nothing — deliberation, not punishment. ──────────
        if (_attuneNodeId is { } attId)
        {
            if (AttuneSealBtn.Contains(hit))
            {
                _attuneNodeId = null;
                _msg = $"ATTUNED. {_attuneForm.ToString().ToUpperInvariant()} IS YOURS — ITS SKILLS HIT TWICE AS HARD.";
            }
            else if (AttuneUndoBtn.Contains(hit))
            {
                Mastery.Refund(attId);
                _attuneNodeId = null;
                Dirty = true;
                _msg = "THE POINTS ARE BACK. ATTUNE WHEN YOU ARE READY.";
            }
            return;
        }

        if (!_editMode)
        {
            if (WeaveBtn.Contains(hit)) { WantsWeave = true; return; }
            // AN EMPTY SKILL CARD IS A DOOR. It drew a "+B" hint and did nothing when clicked, which is
            // the one place on this screen a player is most likely to press: the hole where a skill
            // should be.
            for (var i = Loadout.Skills.Count; i < Loadout.SkillCapacity; i++)
                if (AuraCard(i, Loadout.SkillCapacity).Contains(hit)) { WantsWeave = true; return; }
            _resetArmed = false;
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


            var firstSpec = node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is null;
            if (Mastery.Take(node.Id))
            {
                _msg = "";
                Dirty = true;
                // THE ATTUNEMENT: your first Specialisation is the moment this game is named for,
                // so it gets a ceremony instead of a click-sound — the hexagon shown whole, the
                // choice sealed or taken back, nothing else clickable until you decide.
                if (firstSpec && node.Form is { } nf) { _attuneNodeId = node.Id; _attuneForm = nf; }
            }
            // WHY THE CLICK DID NOTHING, NAMED EXACTLY.
            //
            // The capstone line used to read "YOU'VE ALREADY MASTERED A FORM." and it was wrong twice
            // over. It tested Affinity() — the FORM DISCIPLINE — while the rule CanTake actually
            // enforces for a ring-4 node is MasteredBranch(): one CAPSTONE per hunter, nothing to do
            // with Forms. So a player refused a capstone was told about their Form, and a player who
            // had taken a capstone but no Specialisation was refused with no message at all. It now
            // reads the same rule the refusal came from and says which branch already holds it.
            else _msg = Mastery.IsTaken(node.Id) ? "ALREADY TAKEN." :
                Mastery.Available < node.Cost
                    ? $"NEEDS {node.Cost} POINTS — YOU HAVE {Mastery.Available}. GO DEEPER." :
                node.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() is { } heldBranch
                    ? $"YOU ALREADY TOOK THE {Short(heldBranch)} CAPSTONE — ONE CAPSTONE PER HUNTER." :
                node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is not null
                    ? "YOU ARE ALREADY ATTUNED — ONE DISCIPLINE PER HUNTER." :
                "TAKE A CONNECTED NODE FIRST.";
            return;
        }

        // Skills, Vows and keystone sockets are all WeaveScreen's now.
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        Tree = tree;
        _hoverNodeId = null;
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        var hit = Game1.ToOverlay(mouse);
        _ui.Fill(b, UiKit.OverlayScrim, new Color(0x0A, 0x08, 0x10, 0xD8));

        if (_editMode) { DrawEditor(b, hit, tree); return; }

        _ui.TextCenterBig(b, "BUILD", 960, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);
        // The screen's PROMISE, not a recital of the panel headings under it. It read
        // "SOURCE · FORM · VOW · AURAS" — four words already printed larger a few hundred pixels below,
        // spending the biggest line on the page to say nothing the eye had not already reached.
        _ui.TextCenterBig(b, "EVERY SKILL IS A SOURCE AND A FORM — YOU CHOOSE BOTH", 960, 80, Slate, UiTypography.Secondary);

        DrawSummary(b);
        DrawCore(b);
        DrawAuraCards(b, hit);
        DrawPassives(b, hit, tree);

        // TWO BUTTONS, NOT THREE. "EDIT BUILD" and "VIEW TREE" ran the same line of code — both set
        // _editMode — so the screen offered two differently-named doors into one room while the room
        // itself is now a rail tile. What is left names what it opens and what it costs.
        Button(b, WeaveBtn, "CHOOSE YOUR SKILLS", hit, true);
        if (DevBuildDebug) DrawDebug(b);
    }

    private void DrawSummary(SpriteBatch b)
    {
        _ui.PanelQuiet(b, SummaryPanel);
        _ui.TextCenterBig(b, "BUILD OVERVIEW", SummaryPanel.Center.X, UiKit.TitleTop(SummaryPanel), Gold, UiTypography.PanelTitle);

        var por = new Rectangle(SummaryPanel.Center.X - 66, SummaryPanel.Y + 72, 132, 132);
        if (_ui.Assets.GetFirst(Character?.PortraitKey ?? "hunter_portrait", "hunter_portrait") is { } p)
            b.Draw(p, por, Color.White);

        var adept = Mastery.Affinity() is { } mf ? $"{Short(mf)} ADEPT" : "SEEKER";
        _ui.TextCenterBig(b, adept, SummaryPanel.Center.X, SummaryPanel.Y + 220, Bone, UiTypography.Headline);
        _ui.TextCenterBig(b, $"LEVEL {Level}", SummaryPanel.Center.X, SummaryPanel.Y + 254, Gold, UiTypography.Body);
        // "BUILD POWER" IS DELETED, AND THE REASON IS THAT IT WAS NOT A BUILD NUMBER.
        //
        // Playtest: "Build power denen şey nedir? Neye bağlı? Ben skillerimi neye göre seçiyorum da
        // artıyor azalıyor?" Traced it: this was Hunter.PowerRating (Game1 assigns it), which is
        //
        //     damage x skillRate x critFactor x 120  +  defence x 2  +  healthMultiplier x maxHealth x 0.5
        //
        // and every one of those three multipliers is built from GEAR AND STATS ONLY —
        // SquadDamageMultiplier is gear x AttackPower x worn item mods, SquadSkillRate is focus
        // attunement x worn mods x Engineering, SquadHealthMultiplier is Vitality x charm x worn mods.
        // Nothing in it reads a woven skill, a Source, a Form, a Vow, a keystone or the mastery tree.
        //
        // So the answer to "why does it move when I choose skills" is that IT DOES NOT. It is the gear
        // number, printed in the largest type on the one screen where gear is not the subject — and the
        // same figure is already the headline of the GEAR screen (GEAR POWER) and of the STATS card.
        // Detailing it, which was the other option offered, would have meant explaining at length why a
        // number on this page cannot respond to anything on this page.
        //
        // What belongs here instead is the readout the Weave already computes and this screen does not
        // show: damage per second for the build as woven. That arrives with the Weave merge; until then
        // an honest gap beats a confident wrong number.

        // THE FOUR ROWS THAT USED TO SIT HERE ARE GONE, and the panel is a pure identity card.
        //
        // SOURCE FOCUS / ACTIVE FORMS / VOW-BOUND / MASTERY restated, one panel to the right and in
        // larger type, exactly what CORE COMPOSITION already says: SOURCE, FORMS, VOWS. Two panels, the
        // same three facts, four hundred pixels apart — and they had already contradicted each other
        // once (ACTIVE FORMS counted skills while CORE counted distinct Forms, so a build running two
        // BODY STRIKEs reported five against four on one screen).
        //
        // CORE keeps them because it is the panel the screen is named for and it has the room to say
        // them properly; what is left here is who you are and what that is worth.
    }

    private void DrawCore(SpriteBatch b)
    {
        _ui.PanelQuiet(b, CorePanel);
        _ui.TextCenterBig(b, "CORE COMPOSITION", CorePanel.Center.X, UiKit.TitleTop(CorePanel), Gold, UiTypography.PanelTitle);
        var skills = Loadout.Skills;
        var focus = skills.GroupBy(s => s.Source).OrderByDescending(g => g.Count()).FirstOrDefault();
        var forms = string.Join("  ·  ", skills.Select(s => Short(s.Form)).Distinct());
        var vowList = skills.Select(s => Weaving.ById(s.VowId)?.Short.ToUpperInvariant()).Where(v => v is not null).Distinct();
        var vows = vowList.Any() ? string.Join("  ·  ", vowList) : "NONE";

        var x = CorePanel.X + 44;
        var y = CorePanel.Y + 90;
        Big(b, "SOURCE", focus is null ? "—" : focus.Key.ToString().ToUpperInvariant(), focus is null ? Slate : SourceColor.GetValueOrDefault(focus.Key, Bone), x, ref y);
        Big(b, "FORMS", forms.Length == 0 ? "—" : forms, Bone, x, ref y);
        Big(b, "VOWS", vows, vows == "NONE" ? Slate : Gold, x, ref y);

        // EVERY CLAUSE IS NOW CONDITIONAL ON THE FACT IT ASSERTS, and the panel no longer contradicts
        // itself. The old line was "{SOURCE}-led auto-skills with {affinity} mastery and layered vow
        // uptime" — and its last four words were a CONSTANT. A build with no vows at all was told it
        // had "layered vow uptime" while the row two lines above it read VOWS: NONE. The mastery
        // clause was wrong in the other direction: Affinity() returns the tree's DOMINANT FORM, so a
        // tree with 24 points spent but no single leading Form printed "no mastery" beside a row in
        // the next panel reading MASTERY 24 POINTS.
        //
        // A note that LOOKS derived and is partly hardcoded is worse than no note: the player cannot
        // tell which half to trust, and here both halves were readable as false from the same screen.
        // The comment above it said "no invented copy" — the invented copy was already there.
        var vowCount = skills.Count(s => Weaving.ById(s.VowId) is not null);
        var note = focus is null
            ? "Add a skill to begin. Every skill is a SOURCE and a FORM."
            : Sentences(focus.Key.ToString().ToUpperInvariant(), vowCount);

        _ui.Fill(b, new Rectangle(CorePanel.X + 44, CorePanel.Bottom - 140, CorePanel.Width - 88, 2), Dim);
        _ui.TextBig(b, "IN SHORT", CorePanel.X + 44, CorePanel.Bottom - 124, Slate, UiTypography.Secondary);
        // WRAPPED, because the sentences are now written to be read rather than to fit. Two lines is
        // what the space below the rows allows once the divider moves up into the void that was there.
        var lines = _ui.WrapBig(note, CorePanel.Width - 88, UiTypography.Body);
        for (var i = 0; i < Math.Min(3, lines.Count); i++)
            _ui.TextBig(b, lines[i], CorePanel.X + 44, CorePanel.Bottom - 96 + i * 28, Bone, UiTypography.Body);
    }

    /// <summary>The plain-words reading of a build: what it leads on, what it has bound, where it leans.</summary>
    private string Sentences(string lead, int vowCount)
    {
        var said = new List<string> { $"Most of your skills use {lead}." };
        said.Add(vowCount switch
        {
            0 => "None of them has a vow yet. A vow makes a skill stronger, but it costs you something.",
            1 => "One of them carries a vow.",
            _ => $"{vowCount} of them carry a vow.",
        });
        if (Mastery.Affinity() is { } lean) said.Add($"Your mastery leans {Short(lean)}.");
        else if (Mastery.Spent > 0) said.Add("Your mastery is spread evenly across forms.");
        return string.Join(" ", said);
    }

    private void DrawAuraCards(SpriteBatch b, Point hit)
    {
        _ui.Panel(b, AuraPanel);
        _ui.TextCenterBig(b, "YOUR SKILLS", AuraPanel.Center.X, UiKit.TitleTop(AuraPanel), Gold, UiTypography.PanelTitle);
        var skills = Loadout.Skills;
        // The slots this player HAS. Against the const, a fifth woven skill was invisible on the one
        // screen whose whole job is to show the build.
        for (var i = 0; i < Loadout.SkillCapacity; i++)
        {
            var card = AuraCard(i, Loadout.SkillCapacity);
            if (i < skills.Count)
            {
                var s = skills[i];
                var sc = SourceColor.GetValueOrDefault(s.Source, Bone);
                _ui.Fill(b, card, Quiet);
                _ui.Fill(b, new Rectangle(card.X, card.Y, card.Width, 4), sc);
                Outline(b, card, sc * 0.7f, 2);
                if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g)
                    b.Draw(g, new Rectangle(card.Center.X - 38, card.Y + 24, 76, 76), Color.White);
                else _ui.Diamond(b, new Rectangle(card.Center.X - 32, card.Y + 28, 64, 64), sc);
                // Title = the real skill identity (Source + Form); state line = the Vow (§8 concise state line).
                _ui.TextCenterBig(b, $"{s.Source.ToString().ToUpperInvariant()} {Short(s.Form)}", card.Center.X, card.Y + 118, Bone, UiTypography.Body);
                var vow = Weaving.ById(s.VowId);
                _ui.TextCenterBig(b, vow is null ? "NO VOW" : $"VOW · {vow.Short.ToUpperInvariant()}", card.Center.X, card.Y + 152, vow is null ? Slate : Gold, UiTypography.Secondary);
            }
            else
            {
                _ui.Fill(b, card, Quiet * 0.6f);
                _ui.TextCenter(b, i == skills.Count ? "+ EDIT" : "—", card.Center.X, card.Center.Y - 8, Dim);
            }
        }
    }

    private void DrawPassives(SpriteBatch b, Point hit, MemoryDustTree tree)
    {
        _ui.PanelQuiet(b, PassivePanel);
        // +44 clears the panel art's centred top crest, which a centred title at +24 ran into.
        _ui.TextCenterBig(b, "PASSIVES & RESONANCE", PassivePanel.Center.X, UiKit.TitleTop(PassivePanel), Gold, UiTypography.PanelTitle);

        // 52px, the width of the ornate border. At 32 the left column sat ON the frame and the
        // right-aligned resonance percentages were clipped by the opposite edge.
        var x = PassivePanel.X + 52;

        // THE POINTS LINE GETS ITS OWN ROW. It shared one with the VIEW TREE button, whose left edge is
        // at 1544 — 264px from the text's start, and the string needs about 300. It rendered as
        // "MASTERY POINTS   18 SPENT  ·  6 FI" with the rest under the button. Shortening the label would
        // have hidden it for now and brought it back the moment a player earned a third digit: mastery
        // points are 3 + deepestEver/5 + conquered*5, so three digits is a real endgame value, not a
        // hypothetical. The row is free instead.
        // ONE NAME FOR ONE POOL. This line called them SKILL POINTS while STATS calls the same number
        // MASTERY POINTS and the rail tile that spends them is now labelled MASTERY. Three names for one
        // currency is three currencies as far as a player is concerned.
        _ui.TextBig(b, $"MASTERY POINTS   {Mastery.Spent} SPENT  ·  {Mastery.Available} FREE", x, PassivePanel.Y + 112, Bone, UiTypography.Body);

        // PASSIVES — the real taken mastery nodes (notables + mastery). No invented "trait bonus %" table.
        _ui.TextBig(b, "PASSIVES", x, PassivePanel.Y + 206, Gold, UiTypography.Secondary);
        var taken = MasteryCatalog.Nodes.Where(n => n.Kind is MasteryKind.Notable or MasteryKind.Mastery && Mastery.IsTaken(n.Id)).ToList();
        var py = PassivePanel.Y + 240;
        if (taken.Count == 0) _ui.TextBig(b, "None yet — walk the tree.", x, py, Slate, UiTypography.Body);
        // SHORTENED TO THE COLUMN. Node labels are authored freely and nothing caps their length —
        // MARK MASTERY's runs off the panel and was drawn through the frame art, ending mid-word on
        // "…HITS 40% HA". That reads as a rendering glitch rather than as a label that is too long,
        // which is the worst way for it to fail: nobody files it, and the sentence a player needs in
        // order to evaluate the node is the part that went missing.
        // To the FRAME, not to the 52px text margin. That margin exists so the right-aligned resonance
        // percentages clear the ornate border; these labels are left-aligned and run the other way, so
        // holding them to it truncated three that fit with pixels to spare. The frame's own inset is
        // the real edge.
        var labelWidth = PassivePanel.Right - UiKit.PanelCorner - 4 - (x + 30);
        foreach (var n in taken.Take(6))
        {
            _ui.Diamond(b, new Rectangle(x, py + 2, 18, 18), n.Kind == MasteryKind.Mastery ? Gold : Purple);
            // WRAPPED, NOT SHORTENED. These labels are sentences — cutting them mid-clause left four of
            // five reading as fragments while 150px of the same panel sat empty underneath. Bounded to
            // two lines so six notables still cannot push the KEYSTONES block off the bottom.
            var wrapped = _ui.WrapBig(n.Label, labelWidth, UiTypography.Body);
            for (var li = 0; li < Math.Min(2, wrapped.Count); li++)
            {
                _ui.TextBig(b, wrapped[li], x + 30, py, Bone, UiTypography.Body);
                py += 26;
            }
            py += 12;
        }

        // RESONANCE — the real source composition of the equipped skills (share per source).
        //
        // ANCHORED BELOW THE PASSIVES LIST, not pinned at a fixed +430, for the same reason KEYSTONES
        // below is anchored below the resonance list: the block above it is variable-length. The list
        // takes up to six nodes at 36px from +240, so a player who has walked six notables ends at
        // +456 and this heading at +430 was drawn THROUGH their last passive. Six is not an unusual
        // build; it is what the tree is for. The old fixed position stays as a floor, so a short list
        // still puts the heading exactly where it has always been.
        _ui.TextBig(b, "RESONANCE", x, Math.Max(PassivePanel.Y + 430, py + 16), Gold, UiTypography.Secondary);
        var skills = Loadout.Skills;
        var ry = Math.Max(PassivePanel.Y + 464, py + 50);
        var groups = skills.GroupBy(s => s.Source).OrderByDescending(g => g.Count()).ToList();
        if (groups.Count == 0) _ui.TextBig(b, "No skills equipped.", x, ry, Slate, UiTypography.Body);
        foreach (var g in groups)
        {
            var pct = skills.Count == 0 ? 0 : 100 * g.Count() / skills.Count;
            _ui.Diamond(b, new Rectangle(x, ry + 2, 18, 18), SourceColor.GetValueOrDefault(g.Key, Bone));
            _ui.TextBig(b, $"{g.Key.ToString().ToUpperInvariant()} RESONANCE", x + 30, ry, Bone, UiTypography.Body);
            _ui.TextRightBig(b, $"{pct}%", PassivePanel.Right - 52, ry, SourceColor.GetValueOrDefault(g.Key, Bone), UiTypography.Body);
            ry += 40;
            // A FLOOR, which the keystone block below already has and this loop did not. One row per
            // Source in the loadout, so a six-Source build walks this list straight through the panel's
            // bottom frame and out the other side — and the list is data, so "six" is not hypothetical.
            if (ry > PassivePanel.Bottom - UiKit.PanelCorner - 58) break;
        }

        // KEYSTONES — worn sockets (real). Anchored BELOW the resonance list rather than at a fixed
        // +596: the list is one row per Source in the loadout, so a four-Source build ran its last row
        // (SPIRIT RESONANCE, at +584) straight through this heading. Four is the common case.
        var worn = DustEffects.LearnedKeystones(tree).Where(k => Loadout.HasKeystone(k.Id)).ToList();

        // AND CLAMPED, because pushing it down only moved the collision. Anchoring to the resonance
        // list fixed the overlap and handed the block to the panel's bottom ornament instead — the
        // heading and its line were being drawn on the frame art. The floor here is the last position
        // at which BOTH rows still clear the frame, so the block gets pushed down by a long resonance
        // list exactly as far as there is room for and no further.
        const int keystoneBlockH = 34 + 24;                       // heading, then its line
        var floor = PassivePanel.Bottom - UiKit.PanelCorner - keystoneBlockH;
        var keyY = Math.Min(Math.Max(PassivePanel.Y + 596, ry + 16), floor);
        _ui.TextBig(b, "KEYSTONES", x, keyY, Gold, UiTypography.Secondary);
        if (worn.Count == 0) _ui.TextBig(b, "NONE YET — PICK ONE IN YOUR SKILLS", x, keyY + 34, Slate, UiTypography.Body);
        else _ui.TextBig(b, string.Join("  \u00b7  ", worn.Select(k => k.Name.ToUpperInvariant())), x, keyY + 34, Bone, UiTypography.Body);
    }

    private void Big(SpriteBatch b, string label, string value, Color color, int x, ref int y)
    {
        _ui.TextBig(b, label, x, y, Slate, UiTypography.Secondary);
        _ui.TextBig(b, value, x + 200, y - 4, color, UiTypography.Headline);
        y += 74;
    }

    private void Row(SpriteBatch b, Rectangle panel, string label, string value, ref int y)
    {
        _ui.Fill(b, new Rectangle(panel.X + 24, y, panel.Width - 48, 44), Quiet);
        _ui.TextBig(b, label, panel.X + 38, y + 12, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, value, panel.Right - 38, y + 10, Bone, UiTypography.Body);
        y += 52;
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
    {
        foreach (var r in new[] { SummaryPanel, CorePanel, AuraPanel, PassivePanel })
            Outline(b, r, Ember, 2);
        for (var i = 0; i < Loadout.SkillCapacity; i++) Outline(b, AuraCard(i, Loadout.SkillCapacity), Verd, 2);
        _ui.TextBig(b, $"nav BUILD  overview  {Loadout.SkillCapacity} aura cards", 60, 112, Gold, UiTypography.Secondary);
    }

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
        DrawHexPanel(b, aff);

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
        if (_attuneNodeId is not null) DrawAttunement(b, hit);
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
    private void DrawTreeWorld(SpriteBatch b, Point hit, Form? aff)
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

    private void DrawTreeNodes(SpriteBatch b, Point hit, Form? aff)
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
        foreach (var br in new[] { Branch.Resonance, Branch.Spread, Branch.Tempo, Branch.Endure })
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
        _ui.TextCenterBig(b, "MASTERY TREE", 960, 24, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(700, 74, 520, 3), Gold * 0.5f);

        // ONE ROW, TWO JOBS. It is the screen's caption — what the tree IS — until the tree has
        // something to say back, and then it is the message. The refusals ("NEEDS 3 POINTS — YOU HAVE
        // 0") used to print at the bottom-left corner of the screen, as far from the click that raised
        // them as the canvas allows; the screen's own voice row is where a screen speaks.
        var say = _msg.Length > 0 ? _msg : "FOUR DIRECTIONS  ·  SIX SPECIALISATIONS  ·  ONE DISCIPLINE";
        _ui.TextCenterBig(b, say, 960, 80, _msg.Length > 0 ? Ember : Slate, UiTypography.Secondary);

        // ── THE CORNER PLATE: the mastery glyph and the points you can spend, and nothing else. ──
        _ui.PanelQuiet(b, PointsPanel);

        var num = $"{Mastery.Available}";
        var numW = _ui.MeasureBig(num, UiTypography.PrimaryValue);
        const int glyphBox = 56, gap = 14;
        var group = glyphBox + gap + numW;
        var gx = PointsPanel.X + (PointsPanel.Width - group) / 2;
        var medal = new Rectangle(gx, PointsPanel.Y + (PointsPanel.Height - glyphBox) / 2, glyphBox, glyphBox);
        if (_ui.Assets.Get("ui_medallion_round") is { } ring) b.Draw(ring, medal, Color.White);
        if (_ui.Assets.Get("state_mastery_128") is { } glyph)
            b.Draw(glyph, new Rectangle(medal.X + 13, medal.Y + 13, 30, 30), Gold);
        _ui.TextBig(b, num, medal.Right + gap,
                    PointsPanel.Y + (PointsPanel.Height - UiTypography.PrimaryValue) / 2 - 2,
                    Mastery.Available > 0 ? Gold : Slate, UiTypography.PrimaryValue);

        if (Mastery.Spent > 0)
            Button(b, ResetBtn, _resetArmed ? "PRESS AGAIN TO CONFIRM" : "TAKE EVERY POINT BACK", hit, true);
    }

    // The plate's rows, as offsets from its own top. Named rather than inlined because four of them
    // have to agree with each other and with ResetBtn, which is a static field and cannot read them
    // from a draw call. The last row must end above UiKit.ContentBottom(PointsPanel), which is 448.
    private const int MedallionTop = 14;     // 126 .. 182, straddling the title line at TitleTop (134)
    private const int PointsRowTop = 78;     // 190 .. 252
    private const int BranchRowTop = 152;    // 264 .. 376
    private const int BranchRowH = 28;
    private const int ResetRowTop = 276;     // 388 .. 440

    /// <summary>
    /// WHAT THE TREE HAS COST YOU — the two numbers, as a two-celled framed strip.
    /// </summary>
    /// <remarks>
    /// It was one sentence at the default label size: "POINTS 0 · 24 SPENT". Two different facts joined
    /// by a middle dot, both at the size of a caption, on a page whose entire economy they describe.
    /// A number the player is deciding against is a headline (<see cref="UiTypography.PrimaryValue"/>,
    /// the rung GEAR POWER and MASTERY POINTS use on the STATS card), and two facts in one row want a
    /// rule between them.
    /// </remarks>
    private void DrawPointsStrip(SpriteBatch b, Rectangle r)
    {
        _ui.Fill(b, r, Quiet);
        Outline(b, r, Path, 2);
        _ui.Fill(b, new Rectangle(r.Center.X - 1, r.Y + 10, 2, r.Height - 20), Path);

        var cells = new[]
        {
            (Label: "POINTS", Value: $"{Mastery.Available}", Tint: Mastery.Available > 0 ? Gold : Slate,
             X: r.X + r.Width / 4),
            (Label: "SPENT", Value: $"{Mastery.Spent}", Tint: Mastery.Spent > 0 ? Bone : Slate,
             X: r.X + r.Width * 3 / 4),
        };
        foreach (var (label, value, tint, x) in cells)
        {
            _ui.TextCenterBig(b, label, x, r.Y + 8, Slate, UiTypography.Secondary);
            _ui.TextCenterBig(b, value, x, r.Y + 26, tint, UiTypography.PrimaryValue);
        }
    }

    /// <summary>
    /// WHERE YOU SPENT IT — one row per direction, as a small framed table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These four rows used to live in the node card on the right, where they were visible only while
    /// nothing was hovered — so the readout that answers "have I committed to a branch yet" vanished
    /// the moment the player reached for a node to check. It is a standing fact about the build, not a
    /// thing you read instead of a node, so it stands in the header and never moves.
    /// </para>
    /// </remarks>
    private void DrawBranchTable(SpriteBatch b, Rectangle r)
    {
        _ui.Fill(b, r, Quiet);
        Outline(b, r, Path, 2);

        var y = r.Y + 4;
        foreach (var br in new[] { Branch.Resonance, Branch.Spread, Branch.Tempo, Branch.Endure })
        {
            var taken = MasteryCatalog.Nodes.Count(x => x.Branch == br && Mastery.IsTaken(x.Id));
            var spent = MasteryCatalog.Nodes.Where(x => x.Branch == br && Mastery.IsTaken(x.Id)).Sum(x => x.Cost);
            _ui.Fill(b, new Rectangle(r.X + 10, y + 3, 6, 16), BranchColor(br));
            _ui.TextBig(b, Short(br), r.X + 28, y, Bone, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{taken} · {spent} POINTS", r.Right - 12, y,
                             spent > 0 ? Gold : Dim, UiTypography.Secondary);
            y += BranchRowH;
        }
    }

    /// <summary>
    /// One branch's name and promise, planted outside the rim at the end of its arm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FOUR DIRECTIONS HAD NO LABEL OF THEIR OWN. Their names were printed on the ring-4 capstones
    /// instead — the four biggest objects on the page read WEIGHT / SPREAD / TEMPO / ENDURE while their
    /// own names (OVERWHELM, EVERYWHERE, FIRST STRIKE, ENDLESS) appeared nowhere — so a player read the
    /// tree as four masteries called after the directions and could not find the directions at all. A
    /// playtester said exactly that. A direction is not a node: it is a region of the page, so it is
    /// labelled the way a region is, outside the thing it contains.
    /// </para>
    /// <para>
    /// Hidden with the node captions below <see cref="LabelZoom"/> for the same reason they are: zoomed
    /// out far enough, four headings around a thumbnail is furniture, not information.
    /// </para>
    /// </remarks>
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
        // START is filed under Weight (it has to be filed somewhere) and is always taken, so without
        // the Start test WEIGHT burned as "walked" on a brand-new hunter who has spent nothing.
        var walked = MasteryCatalog.Nodes.Any(n => n.Branch == br && n.Kind != MasteryKind.Bridge
                                                   && n.Kind != MasteryKind.Start && Mastery.IsTaken(n.Id));
        _ui.TextCenterBig(b, Short(br), (int)p.X, (int)p.Y - 23, walked ? col : col * 0.72f,
                          UiTypography.PanelTitle);
        _ui.TextCenterBig(b, BranchPromise(br), (int)p.X, (int)p.Y + 7, Slate, UiTypography.Secondary);
    }

    /// <summary>
    /// THE SPECIALISATION CHART — the tree page's standing answer to "what does my discipline reach".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sits above the node-detail panel so the right side reads as one column: WHO YOU ARE on top,
    /// WHAT YOU ARE READING below. Unattuned it draws quiet and says where attunement lives.
    /// </para>
    /// <para>
    /// <b>IT WAS THE ONE PANEL IN THE GAME WITH NO FRAME.</b> Playtest 2026-08-29: <i>"the
    /// specialisation chart on the right of the MASTERY screen is undecorated."</i> It was a flat
    /// <c>Fill</c> with a two-pixel outline, sitting directly above the ornate node card — two panels
    /// in one column, one of them looking like a debug rectangle. It wears
    /// <see cref="UiKit.PanelQuiet"/> now, the same frame as the card under it, so the column reads as
    /// one thing.
    /// </para>
    /// <para>
    /// The three pieces are laid out to the house grid rather than by eye: the title at
    /// <see cref="UiKit.TitleTop"/>, the STATE as a centred plate under it — the attuned Form's name,
    /// or the two lines that say how to get one — and the diagram in a FIELD, so the hexagon reads as
    /// a chart on a surface rather than as six icons floating in a box. The field is what lets the six
    /// Form names sit close to their own points: it is the edge they are measured against, so the
    /// label gap can drop from the ceremony's 58 px to 8 without any of them looking unmoored.
    /// </para>
    /// <para>
    /// <b>THE FIELD IS THE CHART'S OWN NOW</b> (<see cref="FormHexDiagram.Field"/>) rather than a flat
    /// fill with a two-pixel outline — playtest 2026-08-30: <i>"the chart picture is still careless."</i>
    /// The same inlaid well is drawn under the ceremony's hexagon, which had none at all, so the two
    /// sizes of one drawing stand on one surface.
    /// </para>
    /// </remarks>
    private void DrawHexPanel(SpriteBatch b, Form? aff)
    {
        _ui.PanelQuiet(b, HexPanel);
        _ui.TextCenterBig(b, "YOUR ATTUNEMENT", HexPanel.Center.X, UiKit.TitleTop(HexPanel), Gold,
                          UiTypography.PanelTitle);

        // THE STATE, AS A PLATE. "UNATTUNED / TAKE A SPECIALISATION NODE" used to be two bare centred
        // lines hanging under a title with nothing to sit on — the emptiest-looking state on a screen
        // whose whole job is to make you want to fill it. A plate says "this is a slot, and it is not
        // filled yet"; bare words say "nothing here".
        var plate = new Rectangle(HexPanel.Center.X - 170, HexPanel.Y + StatePlateTop, 340, StatePlateH);
        _ui.Fill(b, plate, aff is null ? Quiet : Hi);
        Outline(b, plate, aff is null ? Path : Gold * 0.55f, 2);
        if (aff is { } a)
            _ui.TextCenterBig(b, a.ToString().ToUpperInvariant(), plate.Center.X, plate.Y + 7, Gold,
                              UiTypography.Headline);
        else
        {
            _ui.TextCenterBig(b, "UNATTUNED", plate.Center.X, plate.Y + 1, Slate, UiTypography.Body);
            _ui.TextCenterBig(b, "TAKE A SPECIALISATION NODE", plate.Center.X, plate.Y + 22, Slate,
                              UiTypography.Secondary);
        }

        // THE FIELD. Inset to the frame's own interior (UiKit.PanelInner), not to the content margin:
        // the six labels reach further out than any text column would, and the field is the thing that
        // has to hold them.
        var inner = UiKit.PanelInner(HexPanel);
        var field = inner with { Y = plate.Bottom + 10, Height = UiKit.ContentBottom(HexPanel) - plate.Bottom - 10 };
        FormHexDiagram.Field(_ui, b, field);

        FormHexDiagram.Draw(_ui, b, field.Center, HexRadius, aff, showFactors: aff is not null,
                            labelGap: HexLabelGap, labelPx: UiTypography.Secondary,
                            factorPx: UiTypography.Caption, sealPx: HexSealPx);
    }

    /// <summary>Where the state plate sits below the chart's own top edge — one title line down.</summary>
    /// <remarks>The panel wears the SQUARE frame, so <see cref="UiKit.FrameDrop"/>'s 28 is in this.</remarks>
    private const int StatePlateTop = 84;

    /// <summary>The state plate's depth: two short lines, or one name set at the in-panel headline.</summary>
    private const int StatePlateH = 40;

    /// <summary>
    /// The docked chart's hexagon, sized to its field rather than to the ceremony's.
    /// </summary>
    /// <remarks>
    /// The field is 266 px deep, so each half has 133 px for <c>radius + the seal's rim (0.62 of its
    /// edge) + the gap + one Secondary line</c>: 78 + 21 + 8 + 16 = 123, leaving 10 px of air at both
    /// ends. (The pole labels carry their factor beside the name rather than under it — see
    /// FormHexDiagram — which is what buys the last twenty pixels of radius.) It was 105 with the
    /// ceremony's 58 px gap: 326 px of diagram in a box that never framed it, so the top label ran into
    /// the caption above and the bottom one had nothing under it.
    /// </remarks>
    private const int HexRadius = 78;

    /// <inheritdoc cref="HexRadius"/>
    private const int HexLabelGap = 8;

    /// <summary>The Form seals — the MEDALLION's edge, scaled to this hexagon rather than to the ceremony's.</summary>
    private const int HexSealPx = 34;   // ui-size-ok: the seal medallion's edge in pixels, not a text size

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
    private void DrawAttunement(SpriteBatch b, Point hit)
    {
        _ui.Scrim(b, 0.75f);
        _ui.Panel(b, AttunePanel, gold: true);

        var name = _attuneForm.ToString().ToUpperInvariant();
        _ui.TextCenterBig(b, "THE ATTUNEMENT", 960, UiKit.TitleTop(AttunePanel), Gold, UiTypography.PanelTitle);
        _ui.TextCenter(b, "SIX FORMS ON THE LOOM — ONE IS YOURS.", 960, UiKit.CaptionTop(AttunePanel), Slate);

        // THE CEREMONY STANDS ON THE SAME FIELD THE DOCKED CHART DOES. It used to have none — the
        // hexagon floated on the modal's black interior — so the game's founding moment was the one
        // place the chart was drawn without the surface that makes it a chart.
        var field = new Rectangle(630, AttunePanel.Y + 86, 660, 492);
        FormHexDiagram.Field(_ui, b, field);
        FormHexDiagram.Draw(_ui, b, field.Center, CeremonyRadius, _attuneForm, showFactors: true,
                            labelGap: CeremonyLabelGap, labelPx: UiTypography.Body,
                            factorPx: UiTypography.Secondary, sealPx: CeremonySealPx);

        _ui.TextCenter(b, $"YOUR {name} SKILLS HIT TWICE AS HARD. THE FAR FORMS HIT SOFTER —",
                       960, AttunePanel.Y + 604, Bone);
        _ui.TextCenter(b, "A VOW ON A FAR-FORM SKILL PULLS IT ONE RING CLOSER.",
                       960, AttunePanel.Y + 626, Bone);

        Button(b, AttuneSealBtn, $"SEAL IT — {name} IS MINE", hit, true);
        Button(b, AttuneUndoBtn, "NOT YET — TAKE THE POINTS BACK", hit, true);
    }

    /// <summary>
    /// What the node under the pointer — or the last one clicked — actually does.
    /// </summary>
    /// <remarks>
    /// The one thing the tree page never had. A node was a coloured rectangle whose entire description
    /// was a single line of text at the bottom of the screen, printed only while the pointer was exactly
    /// on it; a player could not read a node and then look at the tree, which is what reading a tree IS.
    /// Hover shows, click PINS — including a node you cannot afford, which is the one you most want to
    /// read before deciding what to walk toward.
    /// </remarks>
    private void DrawNodeDetail(SpriteBatch b, Point hit)
    {
        _ui.PanelQuiet(b, NodePanel);

        var id = _hoverNodeId ?? _pinnedNodeId;
        if (id is null || MasteryCatalog.ById(id) is not { } n)
        {
            // No panel title: the frame's centred top medallion sits exactly where one would go, and the
            // two lines below already say what this panel is.
            _ui.TextCenterBig(b, "HOVER A NODE TO READ IT.", NodePanel.Center.X, UiKit.BodyTop(NodePanel), Slate,
                              UiTypography.Body);
            // Three words, not a sentence: the card is 496 wide and the long form ran out of both sides
            // of its own frame.
            _ui.TextCenterBig(b, "DRAG  \u00b7  WHEEL  \u00b7  HOME", NodePanel.Center.X,
                              UiKit.BodyTop(NodePanel) + 36, Dim, UiTypography.Secondary);

            // THE LADDER. Size is this tree's price tag, and until now nothing on the page said what
            // the sizes MEANT \u2014 a player could see that a Specialisation is bigger than a Notable and
            // had to click both to learn it costs twice as much. Five kinds, cheapest first, so the
            // order on the line is the order of the rings on the screen.
            //
            // THE FOUR BRANCH ROWS THAT USED TO SIT ABOVE IT ARE IN THE HEADER NOW (DrawBranchTable).
            // They answer "have I committed to a branch yet", which is a standing fact about the build,
            // and they were readable only while nothing was hovered \u2014 so they blanked at the exact
            // moment a player reached for a node to check them against.
            var y0 = UiKit.BodyTop(NodePanel) + 92;
            var rule = new Rectangle(UiKit.ContentLeft(NodePanel) + 6, y0,
                                     NodePanel.Width - UiKit.PadX(NodePanel) * 2 - 12, 2);
            _ui.Fill(b, rule, Dim);
            _ui.TextCenterBig(b, "WHAT EACH KIND COSTS IN POINTS", NodePanel.Center.X, y0 + 14, Slate,
                              UiTypography.Secondary);
            _ui.TextCenterBig(b, "MINOR 1  \u00b7  NOTABLE 3  \u00b7  GREATER 5", NodePanel.Center.X, y0 + 40,
                              Bone, UiTypography.Body);
            _ui.TextCenterBig(b, "SPECIALISATION 6  \u00b7  CAPSTONE 8", NodePanel.Center.X, y0 + 66,
                              Bone, UiTypography.Body);

            // THE TREE'S ONE RULE, in the panel that explains the tree. It used to print at the
            // bottom-left corner of the screen, in the same undecorated strip that also repeated \u2014
            // word for word \u2014 the label of whichever node this card was already showing. The
            // duplication is gone (playtest 2026-08-29, item 4); this line was the only part of that
            // strip saying something nothing else said, so it moved here rather than being deleted.
            _ui.Fill(b, rule with { Y = y0 + 104 }, Dim);
            var hintY = y0 + 118;
            foreach (var line in _ui.WrapBig("RESONANCE OR SPREAD.  TEMPO OR ENDURE.  YOU HAVE POINTS FOR ONE BRANCH, NOT TWO.",
                                             UiKit.ContentRight(NodePanel) - UiKit.ContentLeft(NodePanel),
                                             UiTypography.Secondary))
            {
                _ui.TextCenterBig(b, line, NodePanel.Center.X, hintY, Slate, UiTypography.Secondary);
                hintY += 24;
            }
            return;
        }

        var col = BranchColor(n.Branch);
        var taken2 = Mastery.IsTaken(n.Id);
        var can = Mastery.CanTake(n.Id);

        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(NodePanel) - 4, NodePanel.Y + 40, NodePanel.Width - UiKit.PadX(NodePanel) * 2 - 8, 4), col);
        _ui.TextBig(b, KindWord(n.Kind), UiKit.ContentLeft(NodePanel), NodePanel.Y + 56, col, UiTypography.Secondary);
        _ui.TextRightBig(b, Short(n.Branch), UiKit.ContentRight(NodePanel), NodePanel.Y + 56, Slate, UiTypography.Secondary);

        var afterLabel = DrawWrapped(b, n.Label, UiKit.ContentLeft(NodePanel), NodePanel.Y + 100,
                                     NodePanel.Width - UiKit.PadX(NodePanel) * 2, Bone);

        // WHAT A SPECIALISATION ACTUALLY DOES, IN ONE SENTENCE.
        //
        // Its own catalogue label says "STRIKE SPECIALIST — EXECUTE WEAKENED FOES", which names the
        // trigger and not the thing the node is FOR. Every one of the six also sets the hunter's
        // DISCIPLINE, which is the largest single multiplier in the game and can be chosen exactly
        // once — and that was written down nowhere the player could reach before committing six
        // points. The x2 is not a rounded boast: FormBehaviour.FactorAtDistance(0) is 2.00, and with
        // no discipline at all SoloBattle applies no factor, so taking this really does double the
        // Form named on it. The far Forms fall to 0.75 and the opposite to 0.45 by the same table.
        var y = NodePanel.Y + 240;
        if (n.Kind == MasteryKind.Specialisation && n.Form is { } specForm)
        {
            var f = Short(specForm);
            var mine = Mastery.Affinity();
            // Three states, three sentences. Reading a Specialisation you cannot have and being told
            // what TAKING it would do is the same lie as a button that does nothing.
            var says =
                taken2
                    ? $"{f} IS YOUR DISCIPLINE. YOUR {f} SKILLS HIT TWICE AS HARD, AND SKILLS FAR "
                      + "FROM IT HIT SOFTER."
                : mine is null
                    ? $"TAKING IT MAKES {f} YOUR DISCIPLINE. YOUR {f} SKILLS THEN HIT TWICE AS HARD, "
                      + "AND SKILLS FAR FROM IT HIT SOFTER. ONE DISCIPLINE PER HUNTER."
                    : $"YOUR DISCIPLINE IS ALREADY {Short(mine.Value)}. ONE DISCIPLINE PER HUNTER — "
                      + "TAKE EVERY POINT BACK IF YOU WANT TO CHOOSE AGAIN.";
            var after = DrawWrapped(b, says, UiKit.ContentLeft(NodePanel), afterLabel + 14,
                                    NodePanel.Width - UiKit.PadX(NodePanel) * 2, taken2 || mine is null ? Gold : Slate);
            y = Math.Max(y, after + 20);
        }

        _ui.Fill(b, new Rectangle(UiKit.ContentLeft(NodePanel) - 4, y - 16, NodePanel.Width - UiKit.PadX(NodePanel) * 2 - 8, 2), Dim);
        _ui.TextBig(b, "COST", UiKit.ContentLeft(NodePanel), y, Slate, UiTypography.Body);
        _ui.TextRightBig(b, $"{n.Cost} POINT{(n.Cost == 1 ? "" : "S")}", UiKit.ContentRight(NodePanel), y,
                         taken2 ? Gold : can ? Bone : Ember, UiTypography.Headline);

        y += 56;
        _ui.TextBig(b, "YOU HAVE", UiKit.ContentLeft(NodePanel), y, Slate, UiTypography.Body);
        _ui.TextRightBig(b, $"{Mastery.Available}", UiKit.ContentRight(NodePanel), y,
                         Mastery.Available >= n.Cost ? Bone : Ember, UiTypography.Headline);

        y += 72;
        // THE TWO ONCE-PER-HUNTER RULES GET THEIR OWN LINE. Without them a second Specialisation, or a
        // second capstone, fell through to "LOCKED — WALK TO IT FIRST" — which tells a player to do
        // something that cannot help, since the path is already walked and the rule is what refused.
        var state = taken2 ? "TAKEN" : can ? "AVAILABLE — CLICK THE NODE"
                    : Mastery.Available < n.Cost ? "NOT ENOUGH POINTS"
                    : n.Kind == MasteryKind.Specialisation && Mastery.Affinity() is not null
                        ? "CLOSED — YOU ALREADY HAVE A DISCIPLINE"
                    : n.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() is not null
                        ? "CLOSED — YOU ALREADY TOOK A CAPSTONE"
                    : "LOCKED — WALK TO IT FIRST";
        _ui.TextCenter(b, state, NodePanel.Center.X, y, taken2 ? Gold : can ? Verd : Ember);
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
        var line = "";
        foreach (var w in text.Split(' '))
        {
            var probe = line.Length == 0 ? w : line + " " + w;
            if (_ui.Measure(probe) > width && line.Length > 0) { _ui.Text(b, line, x, y, c); y += 30; line = w; }
            else line = probe;
        }
        if (line.Length > 0) { _ui.Text(b, line, x, y, c); y += 30; }
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
        MasteryKind.Specialisation => "SPECIALISATION — YOUR DISCIPLINE",
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
        Branch.Spread => new Color(0x48, 0xB8, 0x88),
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
        _ => "ui_node_spec",
    };

    /// <summary>What is set in the socket. The glyph says BRANCH — which of the four roads this is.</summary>
    private static string BranchGlyph(Branch b) => b switch
    {
        Branch.Resonance => "icon_branch_resonance",
        Branch.Spread => "icon_branch_spread",
        Branch.Tempo => "icon_branch_tempo",
        _ => "icon_branch_endure",
    };

    // THE SKILL EDITOR MOVED OUT. It was a column of `< VALUE >` cycling cells wedged into this page's
    // right margin, and it lived here only because the tree happened to have spare width. Cycling is
    // the wrong verb for a list of six — picking SPIRIT from BODY was five clicks and five reads, with
    // nothing on screen naming the other five — and a Vow's whole bargain (does my BUILD meet its
    // demand?) could not be answered from a cell that showed one word at a time.
    //
    // WeaveScreen owns it now, at full width, with every Source, every Form and every studied Vow
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
    private void DrawNode(SpriteBatch b, MasteryNode node, Point mouse, Form? aff)
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
            _ui.TextCenterBig(b, node.Form is { } sf ? Short(sf) : "FORM", cx, cy - 9,
                              taken ? new Color(0x14, 0x11, 0x1E) : aff is null ? Bone : Muted(Bone, 0.62f),
                              UiTypography.Secondary);
            _ui.TextCenterBig(b, "SPECIALISATION", cx, box.Bottom + 5,
                              taken ? Gold : aff is null ? Muted(Gold, 0.90f) : Muted(Slate, 0.72f),
                              UiTypography.Secondary);
        }
        // The branch glyph, over the frame's hollow centre. Below about twenty pixels it is a smudge
        // that only muddies the socket, and the frame alone still carries the kind — so it drops out
        // rather than degrading, the same way the labels do.
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
        // room is ABOVE the medallion, between it and the WEIGHT header, so the two lines stack away
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
    /// 5.7 is not a taste call. PrestigeScreen — the tree the same playtester has NOT complained about —
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
