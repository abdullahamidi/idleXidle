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
    /// The one IDLE motion on this page: the breath under an unchosen Specialisation.
    /// </summary>
    /// <remarks>
    /// A wall clock rather than an accumulated delta because this screen's Update takes no frame time
    /// and the host's call site is not this file's to change. It is SAMPLED in <see cref="Update"/>
    /// into <see cref="_breathPhase"/> — Draw reads a number, never the clock — and under Reduced
    /// Motion the sample is held at the breath's brightest, so the six still say "not ordinary" as a
    /// static highlight rather than as motion (brief §32: drop idle motion, keep the highlight).
    /// </remarks>
    private static readonly System.Diagnostics.Stopwatch _breath = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>The breath's brightness this frame, 0.10 → 1; 1 and still under Reduced Motion.</summary>
    private float _breathPhase = 1f;

    /// <summary>
    /// The breath's brightness at a moment — and the whole of its Reduced Motion answer: HELD, at the
    /// top of its own breath, so the six unchosen Specialisations keep the highlight and lose the motion
    /// (brief §32).
    /// </summary>
    /// <remarks>
    /// Public and pure because it is the one piece of this screen's motion a test can reach: the screen
    /// itself cannot be constructed without a GraphicsDevice, and the capture rig has no Reduced Motion
    /// dial, so this state can only be PROVEN here. See <c>mastery_motion_test.cs</c>.
    /// </remarks>
    public static float BreathPhase(double seconds)
        => UiMotion.Reduced ? 1f : 0.55f + 0.45f * MathF.Sin((float)seconds * 2.4f);

    /// <summary>
    /// How far the SPECIALISATION ceremony has faded in, from how much of its one-shot is left
    /// (<see cref="UiMotion.Pulse"/>): 0 as it fires, 1 once it is open — and 1 AT ONCE under Reduced
    /// Motion, the same end state with no fade (brief §32, §34).
    /// </summary>
    /// <inheritdoc cref="BreathPhase" path="/remarks"/>
    public static float CeremonyFade(float pulseLeft)
        => UiMotion.Reduced ? 1f : UiMotion.Smooth(1f - pulseLeft);

    // ══ FEEDBACK (UI polish P3–P6) ══════════════════════════════════════════════════════════════
    //
    // Every motion on this page is a UiMotion value: a one-shot pulse FIRED FROM UPDATE at the event
    // it marks (a take, a refund, the ceremony opening) and only READ by Draw; or a hover / selection
    // ease. Nothing here loops and nothing is re-armed by a draw.
    //
    // NOT ONE OF THEM MOVES ANYTHING. Every pulse is a fade at a fixed size and a fixed place — the
    // take's bloom is two rings at a fixed radius, the ceremony's entrance is a veil clearing, the
    // spend is a figure brightening — which is why Reduced Motion has nothing to strip from them
    // (brief §32 drops scale, movement, idle motion and slides, and keeps simple fades). The two
    // things that WOULD read as motion under that setting answer it themselves: the eases collapse to
    // their target (UiMotion.Ease), and the idle breath holds at its brightest (BreathPhase).
    //
    // The one deliberate DISPLACEMENT is the 2 px a pressed node drops — held only while the button
    // is, which is a state and not an animation, and the same two pixels UiKit.Button drops.

    /// <summary>The take pulse on a node — a gold bloom that plays once, keyed to the node taken.</summary>
    private static int NodeKey(string id) => HashCode.Combine("node", id);

    /// <summary>The wire into a node lighting up, keyed to the node whose take lit it.</summary>
    private static int WireKey(string id) => HashCode.Combine("wire", id);

    /// <summary>A node's hover lift.</summary>
    private static int HoverKey(string id) => HashCode.Combine("hover", id);

    /// <summary>A node's SELECTED ring — the pinned node, the one the inspector is reading (brief §28).</summary>
    private static int SelKey(string id) => HashCode.Combine("pin", id);

    /// <summary>The AVAILABLE figure reacting to a spend or a refund (brief §36–§37).</summary>
    private static readonly int PointsKey = HashCode.Combine("points");

    /// <summary>The ceremony's backdrop and panel fading in (brief §34).</summary>
    private static readonly int SpecKey = HashCode.Combine("ceremony");

    /// <summary>
    /// A hover-style ease that costs nothing at rest.
    /// </summary>
    /// <remarks>
    /// <see cref="UiMotion.Ease"/> starts a key it has never seen at the OPPOSITE of its target — right
    /// for a hover fading in, but a control at rest that asks for 0 every frame is handed a 1 that
    /// then fades, is forgotten at 0, and is handed a 1 again: a sawtooth on every idle control
    /// (measured: 0.93, 0.74, 0.50, 0.26, 0.07, 0, 0.93 …). So a key is only asked for 0 while it is
    /// known to be lit, and dropped the moment it reaches rest. The set holds the lit keys and nothing
    /// else; a page at rest asks UiMotion for nothing.
    /// </remarks>
    private float Lift(int key, bool on, float seconds = UiMotion.Fast)
    {
        if (on) { _lit.Add(key); return UiMotion.Ease(key, 1f, seconds); }
        if (!_lit.Contains(key)) return 0f;
        var v = UiMotion.Ease(key, 0f, seconds);
        if (v <= 0f) _lit.Remove(key);
        return v;
    }

    private readonly HashSet<int> _lit = new();

    /// <summary>How far a pressed node sinks, in page pixels — the same two the house button drops.</summary>
    private const int PressDepth = 2;

    /// <summary>The left button is down — the real one, or the one a capture dial is holding down.</summary>
    private static bool Held => UiKit.MouseHeld || DevHold;

    /// <summary>The sound the host should play for what just happened here, read once and cleared.</summary>
    /// <remarks>
    /// The host owns audio; this screen only names the moment (the shape of
    /// <c>TraitsScreen.ConsumeCue</c>). The vocabulary is the audio README's: <c>sfx_weave</c> for a
    /// node clicking into the tree, <c>sfx_bind</c> for the two commitments (a branch capstone, a style
    /// sealed), <c>sfx_click</c> for a point handed back or a two-step button armed, <c>sfx_error</c>
    /// for a click the rules refused.
    /// </remarks>
    public string? ConsumeCue()
    {
        var c = _cue;
        _cue = null;
        return c;
    }

    private string? _cue;

    // ── DEV DIALS for the capture rig (the rig shoots Game1.ShotAtFrame; a transient needs a pose). ──
    //
    //   RH_SHOT_TAKE=<nodeId>   take that node FOR REAL, through TakeNode, at the posed instant below —
    //       so the shutter catches its bloom, its wire lighting and the AVAILABLE figure reacting. A
    //       node the fixture cannot take poses the REFUSAL instead, which is the other half of §29.
    //   RH_SHOT_HOVER=<nodeId>  put the cursor on that node's centre; the screen's own hit test then
    //       hovers it, so what is photographed is the real state and not a painted-on one.
    //   RH_SHOT_HOLD=1          the button is held over it: the PRESSED state.
    //   RH_SHOT_POSE=<0..1>     FREEZE the posed transient partway through and photograph it there:
    //       0 is the instant it fires, 1 is settled, 0.5 is halfway. Unset, the shot catches the take
    //       on its own peak and the ceremony already open. It poses the take's bloom, the wire that
    //       take lights, and the ceremony's fade — each on its OWN clock, so a frozen frame is a true
    //       frame: the bloom runs over a Transition and the wire over Fast, which is 1.8× quicker, so
    //       at pose 0.5 the bloom is half gone and the wire is already lit.
    //
    // WHY A FREEZE AND NOT A DELAY. The first version aimed the event so many frames before the
    // shutter. It is not reproducible: this rig runs Update more often than Draw while it catches up
    // from the content load, and a pulse is advanced by UiMotion.Tick (an Update) while an ease is
    // advanced by the ask (a Draw) — so "five frames" meant five steps of one and fifteen of the other,
    // and the same command produced a different fraction on a different machine. A pose that names the
    // FRACTION drives the same drawing code with a known input, which is what TraitsScreen.DevPoseLit
    // does with its own flourish.
    //
    // Read once here; consumed in Update and read in Draw. Nothing in the host had to change — the
    // dials are this screen's own, the way HuntScreen.DevSeekBefore and TraitsScreen._litFrozen are.
    private static readonly string? DevTakeId = Env("RH_SHOT_TAKE");
    private static readonly string? DevHoverId = Env("RH_SHOT_HOVER");
    private static readonly bool DevHold = Env("RH_SHOT_HOLD") == "1";
    private static readonly float? DevPose = ParsePose();
    private bool _devFired;

    private static string? Env(string k)
    {
        var v = Environment.GetEnvironmentVariable(k);
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static float? ParsePose()
        => float.TryParse(Env("RH_SHOT_POSE"), System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out var p)
            ? Math.Clamp(p, 0f, 1f)
            : null;

    /// <summary>
    /// The frame the posed take fires on: one before <see cref="Game1.ShotAtFrame"/>, because Update N
    /// runs before Draw N and the host's frame counter is the last Draw's — so the take lands in the
    /// Update the photographed Draw follows.
    /// </summary>
    private static int DevFireFrame => Game1.ShotAtFrame - 1;

    /// <summary>
    /// Where the WIRE stands at a posed fraction of the take's bloom. The bloom runs over a Transition
    /// and the wire over Fast, so the wire is 1.8× ahead of it and is already lit when the bloom is
    /// half gone — the same relationship the two have when they run for real.
    /// </summary>
    private static float PosedWire(float pose) => MathF.Min(1f, pose * UiMotion.Transition / UiMotion.Fast);

    /// <summary>The cursor the screen works with: the real one, or the posed one a dev dial put on a node.</summary>
    private Point Cursor(Point mouse)
        => DevHoverId is { } id && MasteryCatalog.ById(id) is { } n ? Screen(NodePos(n)).ToPoint() : mouse;

    private string _msg = "";

    /// <summary>The node <see cref="_msg"/> was raised for, or null for a message about the whole tree.</summary>
    /// <remarks>
    /// A refusal is read beside the button that raised it — so it is shown while the inspector shows
    /// THAT node, and every other node keeps its own reason (brief §29: a disabled control says why,
    /// its own why). Before this the last refusal followed the pointer from node to node.
    /// </remarks>
    private string? _msgNodeId;
    private string? _hoverNodeId;          // the node under the pointer this frame
    private string? _pinnedNodeId;         // the last node clicked — what the detail panel shows when nothing is hovered
    private string? _specNodeId;         // non-null: the SPECIALISATION ceremony is open for this just-taken node
    private bool _specWasOpen;           // last frame's answer, so the ceremony's fade fires ONCE when it opens
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
    //
    // A PROPERTY, SIZED BY WHAT IT HOLDS (UI polish P2). It was `static readonly (40, 112, 300, 140)`,
    // which is frozen at class load and cannot follow the UI SCALE setting — and 140 px was exactly the
    // 100 % stack of its four lines. At 150 % the number is 48 px tall and the style line 33, so the
    // plate is the sum of its rows: the number's row, the SPENT line, a breath, the style line, and the
    // frame's clearance top and bottom. Its width is two insets plus a run of content that grows with
    // the type it carries (the medallion beside a PrimaryValue figure beside the word AVAILABLE).
    private static Rectangle PointsPanel => new(PointsPanelLeft, HeaderBottom, PointsPanelWidth, PointsPanelHeight);

    /// <summary>The plate's left edge — the page's own margin past the nav rail, a page anchor.</summary>
    private const int PointsPanelLeft = 40;

    /// <summary>
    /// The plate's width: two insets and a run of 256 at 100 %. The run grows at the SPACING rate, not
    /// the control rate: at 100 % it is mostly air (the widest row, STYLE · HAMMER x2.0, is 167 of it),
    /// and at 150 % the rows still fit with room (251 of 320) — while a plate grown at the full rate
    /// (440 wide) reached under the tree's north-west arm at the overview and covered the HAMMER
    /// specialisation's caption. The button under it fits its label at every profile (measured).
    /// </summary>
    private static int PointsPanelWidth => PointsPad * 2 + UiMetrics.Space(256);

    private static int PointsPanelHeight
        => PointsNumberTop + UiTypography.Pitch(UiTypography.PrimaryValue) + UiTypography.Pitch(UiTypography.Secondary)
           + UiMetrics.Space(16) + UiTypography.Body + PointsPad;

    /// <summary>The plate's inset from its frame — the medium frame's side rail, plus room.</summary>
    private static int PointsPad => UiMetrics.Space(22);

    /// <summary>Where the AVAILABLE figure's row starts, below the plate's top edge.</summary>
    private static int PointsNumberTop => UiMetrics.Space(14);

    /// <summary>The mastery medallion's edge on the plate — an icon box, so it grows with the controls.</summary>
    private static int PointsMedalPx => UiMetrics.Control(56);

    // ON THE TREE PAGE, NOT THE OVERVIEW. Playtest: "'Take all mastery points back' buranın butonu
    // değil, mastery tree'nin butonu." Right — it un-spends every point in a tree the overview does not
    // even draw, so pressing it there meant watching nothing happen to anything on screen. It sits
    // where BACK used to: MASTERY is a rail destination now, and a BACK button on a page with its own
    // door reads as "you are somewhere nested" when you are not (playtest asked what it even meant).
    //
    // INSIDE THE POINTS PLATE now, on its content width, rather than floating on the tree beneath it.
    private static Rectangle ResetBtn
        => new(PointsPanel.X, PointsPanel.Bottom + UiMetrics.Gap, PointsPanel.Width, UiMetrics.ButtonHeight);

    /// <summary>
    /// The band the respec's warning hangs in: under the button that raised it, on the plate's own width.
    /// </summary>
    /// <remarks>
    /// BESIDE THE BUTTON THAT WOULD DO IT, like every other refusal on this screen — not five hundred
    /// pixels away in the node inspector, which is empty until a node is pinned and would therefore have
    /// swallowed the warning entirely on the most common path. It is reserved at the FULL slot count so
    /// the band is provably inside the page at every profile whatever the build holds; the frame drawn
    /// into it is sized to the names actually listed.
    /// </remarks>
    private static Rectangle RespecWarning
        => new(PointsPanel.X, ResetBtn.Bottom + UiMetrics.Gap, PointsPanel.Width,
               RespecWarningPad * 2 + UiTypography.Pitch(UiTypography.Secondary)
               + PlayerLoadout.MaxSkills * UiTypography.Pitch(UiTypography.Body));

    /// <summary>
    /// The warning plate's inset — the same one the points plate keeps, so the two read as one column and
    /// the heading clears the frame's top rail rather than being drawn through it.
    /// </summary>
    private static int RespecWarningPad => PointsPad;

    // ── THE TOP STRIP'S GRID — derived from the rungs it stacks, so a bigger title pushes its rule and
    //    caption down rather than printing through them (UI polish P2). At 100 % these are the house
    //    pattern's own numbers: title at 24, rule at 74, caption at 80, content from 112. ─────────────

    /// <summary>Where the screen's title sits — the top strip's one page anchor, the same on every screen.</summary>
    private const int TitleTop = 24;

    /// <summary>The gold rule under the title: one title's height below it, plus a breath. 74 at 100 %.</summary>
    private static int RuleTop => TitleTop + UiTypography.ScreenTitle + UiMetrics.Space(12);

    /// <summary>The screen's caption, just under the rule. 80 at 100 %.</summary>
    private static int CaptionTop => RuleTop + UiMetrics.Space(6);

    /// <summary>Where the page's content begins: under the caption's line and a breath. 112 at 100 %.</summary>
    private static int HeaderBottom => CaptionTop + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(8);

    /// <summary>The page's bottom margin under every column — the same inset the other screens keep.</summary>
    private const int PageMarginBottom = 16;

    /// <summary>
    /// The slack a node's hit box keeps past its drawn radius — the same slack for the hover, the
    /// left click and the right click, so what lights up is what takes the click (LAW 5).
    /// </summary>
    private static int HitSlop => UiMetrics.Space(6);

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
    private float WholeTreeZoom => Overview.Zoom;

    /// <summary>
    /// THE OVERVIEW'S GEOMETRY — the header ring and the zoom that fits it — derived TOGETHER from the
    /// room the canvas has at this profile (UI polish P2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things stand between a capstone and the branch header past it, and only one of them scales
    /// with the tree: the world-unit gap (<see cref="HeaderRing"/> − 1, times the zoom) and the TEXT —
    /// the capstone's two caption lines and the header's promise, in screen pixels that follow the
    /// profile and not the camera. At 125 % the captions had grown into the promise (CAPSTONE printed
    /// through STRONGER SKILLS… on the north arm and through LOOT on the south); at 150 % the east and
    /// west promises ran off the sides of a canvas the wider inspector had narrowed. So the ring is
    /// SOLVED FOR, not pinned: the smallest ring at which, with the zoom that fits that ring in the
    /// view, the header's text still clears the capstone's by <see cref="CaptionClearance"/> — and never
    /// inside <see cref="HeaderRingBase"/>, the ring the tree was drawn at, so 100 % does not move.
    /// </para>
    /// <para>
    /// The zoom fits the ring in BOTH axes: the view's half-height less a header's half-height, and its
    /// half-width less half the widest side header — whose promise is WRAPPED to the room a
    /// height-fitted ring leaves between itself and the canvas edge when one line will not fit there
    /// (brief §9: wrap secondary text at 150 %; one line at 100 %, two at 150 %).
    /// </para>
    /// <para>
    /// Memoised per profile and view: <see cref="LabelZoom"/> reads this for every node on every frame,
    /// and the derivation measures text.
    /// </para>
    /// </remarks>
    private OverviewGeometry Overview
    {
        get
        {
            if (_overview is null || _overview.Percent != UiMetrics.Percent || _overview.View != TreeView)
                _overview = DeriveOverview();
            return _overview;
        }
    }

    private OverviewGeometry? _overview;

    /// <param name="KindZoom">The zoom from which a capstone wears its CAPSTONE line — see <see cref="KindLineZoom"/>.</param>
    private sealed record OverviewGeometry(int Percent, Rectangle View, float Ring, float Zoom, float KindZoom,
                                           IReadOnlyDictionary<Branch, IReadOnlyList<string>> Promises);

    private OverviewGeometry DeriveOverview()
    {
        var view = TreeView;
        var byHeight = view.Height / 2f - HeaderHalfHeightPx;
        var pitch = UiTypography.Pitch(UiTypography.Secondary);

        // Every header's promise, wrapped to its room: the whole width for the poles, and for the sides
        // what a height-fitted ring leaves beside it. A room too small for a word still gets one word
        // a line — the width bound below then takes that word's width into account.
        var promises = new Dictionary<Branch, IReadOnlyList<string>>();
        var sideHalfWidth = 0;      // the widest side header, title or promise line, halved
        var sidePromiseHalf = 0;    // the widest side promise LINE, halved
        var sideLines = 1;
        foreach (var br in Enum.GetValues<Branch>())
        {
            var side = IsSideHeader(br);
            var room = side ? Math.Max(1, (int)(view.Width - byHeight * 2f)) : view.Width;
            var lines = _ui.WrapBig(BranchPromise(br), room, UiTypography.Secondary);
            promises[br] = lines;
            if (!side) continue;
            var promise = lines.Count == 0 ? 0 : lines.Max(l => _ui.MeasureBig(l, UiTypography.Secondary));
            sidePromiseHalf = Math.Max(sidePromiseHalf, promise / 2);
            sideHalfWidth = Math.Max(sideHalfWidth, Math.Max(_ui.MeasureBig(Short(br), UiTypography.PanelTitle), promise) / 2);
            sideLines = Math.Max(sideLines, lines.Count);
        }
        var reach = MathF.Min(byHeight, view.Width / 2f - sideHalfWidth);

        // The poles' promise height, and the side capstones' widest row (their NAME, or CAPSTONE).
        var poleLines = Enum.GetValues<Branch>().Where(b => !IsSideHeader(b)).Max(b => Math.Max(1, promises[b].Count));
        var poleHeight = UiTypography.Secondary + (poleLines - 1) * pitch;
        var sideNameHalf = MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Mastery && IsSideHeader(n.Branch))
                                               .Select(n => _ui.MeasureBig(Head(n.Label), UiTypography.Body) / 2)
                                               .DefaultIfEmpty(0).Max();
        var sideKindHalf = _ui.MeasureBig("CAPSTONE", UiTypography.Secondary) / 2;
        var capR = MasteryLayout.NodeWorldRadius(MasteryKind.Mastery);
        var capstone = capR / MasteryLayout.WorldRadius;

        (float Ring, float Zoom) Solve(bool kindLine)
        {
            // THE POLES. The stack the ring must clear on whichever pole is tighter, in screen pixels —
            // the capstone's captions as DrawNode stacks them and the header's lines as
            // DrawBranchHeader places them. With z = reach / (R·ring):
            //     (ring − 1)·R·z ≥ capstone·R·z + stack   ⇒   ring ≥ reach·(1 + capstone) / (reach − stack).
            var kindUp = kindLine ? 1 + UiTypography.Secondary : 0;
            var kindDown = kindLine ? UiTypography.Secondary : 0;
            var north = UiMetrics.Space(2) + UiTypography.Body + kindUp + HeaderPromiseDrop + poleHeight + CaptionClearance;
            var south = UiMetrics.Space(4) + UiTypography.Body + kindDown + HeaderTitleRise + CaptionClearance;
            var stack = Math.Max(north, south);
            var ring = reach > stack ? reach * (1f + capstone) / (reach - stack) : HeaderRingBase;
            ring = MathF.Max(HeaderRingBase, ring);
            var zoom = reach / (MasteryLayout.WorldRadius * ring);

            // THE SIDES. The promise's lines run BESIDE the capstone's rows there. Where a line and a
            // row share more than a line's leading, they have to be apart in x — (ring − 1)·R·z ≥ apart,
            // i.e. ring ≥ reach / (reach − apart). At 100 % the promise ends above the name row and this
            // never fires; at 150 % a two-line promise runs level with the name.
            var promiseBottom = HeaderPromiseDrop + (sideLines - 1) * pitch + UiTypography.Secondary;
            var rowTop = capR * zoom + UiMetrics.Space(4);
            var rowBottom = rowTop + UiTypography.Body + kindDown;
            var overlap = MathF.Min(promiseBottom, rowBottom) - MathF.Max(HeaderPromiseDrop, rowTop);
            if (overlap > pitch - UiTypography.Secondary)
            {
                var apart = Math.Max(sideNameHalf, kindLine ? sideKindHalf : 0) + sidePromiseHalf + CaptionClearance;
                if (reach > apart && reach / (reach - apart) > ring)
                {
                    ring = reach / (reach - apart);
                    zoom = reach / (MasteryLayout.WorldRadius * ring);
                }
            }
            return (ring, zoom);
        }

        // Solved WITH the capstone's kind line; if that overview sits under the zoom the kind line needs
        // (KindLineZoom — it will not be drawn there), solved again without it, and the bare solution
        // kept if it is consistent with itself (still under that zoom). Either way the line is only ever
        // drawn at a zoom its ring was solved for.
        var kindZoom = KindLineZoom();
        var (ring, zoom) = Solve(kindLine: true);
        if (zoom < kindZoom)
        {
            var bare = Solve(kindLine: false);
            if (bare.Zoom < kindZoom) (ring, zoom) = bare;
        }
        return new OverviewGeometry(UiMetrics.Percent, view, ring, zoom, kindZoom, promises);
    }

    /// <summary>
    /// The zoom from which a capstone may wear its KIND line (CAPSTONE, under its name) without that
    /// line meeting a neighbouring specialisation's own caption — the two stacks face each other on
    /// the east and west arms, where the capstone's rows run down and the south-half specialisation's
    /// caption runs up (see <see cref="DrawNode"/>). Their world gap holds both stacks at 100 % and
    /// 125 %; at 150 % it does not, and the KIND line is the one that yields (brief §9: less secondary
    /// info at 150 % — the medallion's size, its halo and its name still say what it is, and one wheel
    /// notch in the line is back). Measured over every capstone–specialisation pair, so it follows the
    /// catalogue: for each, the zoom at which the boxes part in y, or the one at which they part in x,
    /// whichever comes first.
    /// </summary>
    private float KindLineZoom()
    {
        var specs = MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Specialisation).ToList();
        var specW = _ui.MeasureBig("SPECIALISATION", UiTypography.Secondary);
        var kindW = _ui.MeasureBig("CAPSTONE", UiTypography.Secondary);
        var capR = MasteryLayout.NodeWorldRadius(MasteryKind.Mastery);
        var specR = MasteryLayout.NodeWorldRadius(MasteryKind.Specialisation);
        var capStack = UiMetrics.Space(4) + UiTypography.Body + UiTypography.Secondary;   // name, then CAPSTONE, away from the medallion
        var specStack = UiMetrics.Space(6) + UiTypography.Secondary;                      // the caption, toward the centre
        var need = 0f;
        foreach (var c in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Mastery))
        {
            var cp = NodePos(c);
            var capDown = cp.Y >= -1f;   // a north capstone stacks its captions UP; every other, DOWN
            var capW = Math.Max(_ui.MeasureBig(Head(c.Label), UiTypography.Body), kindW);
            foreach (var s in specs)
            {
                var sp = NodePos(s);
                var specUp = sp.Y > 1f;  // a south-half specialisation's caption sits ABOVE it; a north-half's, BELOW
                if (capDown != specUp) continue;                    // the stacks run the same way — they never meet
                var dy = capDown ? sp.Y - cp.Y : cp.Y - sp.Y;
                var clear = dy - capR - specR;                      // world units between the two boxes' edges
                if (clear <= 0f) continue;                          // not a pair a zoom can part
                var partY = (capStack + specStack) / clear;
                var dx = MathF.Abs(sp.X - cp.X);
                var partX = dx > 0f ? (capW + specW) / 2f / dx : float.MaxValue;
                need = MathF.Max(need, MathF.Min(partX, partY));
            }
        }
        return need;
    }

    /// <summary>The east and west headers — the ones the canvas's WIDTH has to hold.</summary>
    private static bool IsSideHeader(Branch b) => MathF.Abs(MathF.Cos(MasteryLayout.AngleOf(b))) > 0.5f;

    /// <summary>The air a header's line keeps off the capstone caption it faces, at the overview.</summary>
    private static int CaptionClearance => UiMetrics.Space(8);

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

    /// <summary>
    /// Half the height a branch header occupies on screen: its title line above, promise below.
    /// Measured from the two rungs it is drawn in (see <see cref="DrawBranchHeader"/>), so the framing
    /// keeps the headers inside the view at every profile — 24 at 100 %, 37 at 150 %.
    /// </summary>
    private static float HeaderHalfHeightPx
        => (UiTypography.PanelTitle + UiTypography.Secondary + UiMetrics.Space(2)) / 2f;

    /// <summary>A branch header's title sits this far above its ring point — the title's own height, less a hair.</summary>
    private static int HeaderTitleRise => UiTypography.PanelTitle - 5;

    /// <summary>A branch header's promise sits this far below its ring point.</summary>
    private static int HeaderPromiseDrop => UiMetrics.Space(7);

    /// <summary>
    /// Below this, every caption on the tree drops out and only shapes remain.
    /// </summary>
    /// <remarks>
    /// Two hundredths under the whole-tree framing, so the one framing HOME returns to keeps its
    /// labels. It was once pinned at 0.28 against a default of 0.30 and silently hid every label when
    /// the default moved; tied to the framing instead of guessed at again.
    /// </remarks>
    private float LabelZoom => WholeTreeZoom - 0.02f;

    private float _zoom = FirstOpenZoom;  // screen pixels per world unit — a fresh game opens on ring 1
    private Point? _dragFrom;             // where a drag started, in screen space
    private bool _draggedThisPress;       // the drag moved far enough to swallow the click
    private Vector2 _dragPanFrom;

    /// <summary>
    /// How far out the wheel may go: the base, or the overview itself where the profile has pushed
    /// the overview under it — HOME must never land on a zoom the wheel then refuses to hold.
    /// </summary>
    private float MinZoom => MathF.Min(MinZoomBase, WholeTreeZoom);

    private const float MinZoomBase = 0.16f, MaxZoom = 1.40f;

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
    // Its TOP is the header's bottom, DERIVED (UI polish P2): at 150 % the title is 57 px tall and the
    // caption 29, so the strip ends at 146 rather than 112, and the canvas starts where it ends.
    private static Rectangle TreeView
        => new(TreeLeft, HeaderBottom, InspectorPanel.X - UiMetrics.Space(8) - TreeLeft, UiKit.PageBottom(PageMarginBottom) - HeaderBottom);

    /// <summary>The canvas's left edge — the nav rail's width, a page anchor the host owns.</summary>
    private const int TreeLeft = 180;

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
    /// <para>
    /// 1.30 is the FLOOR now (UI polish P2): the profile's type is what those "~44px" and "7px" are
    /// made of, so at 125 % and 150 % the ring is derived — see <see cref="Overview"/> — and stands
    /// further out, still in world units, still fixed to its arm.
    /// </para>
    /// </remarks>
    private float HeaderRing => Overview.Ring;

    /// <inheritdoc cref="HeaderRing"/>
    private const float HeaderRingBase = 1.30f;

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

    /// <summary>A node's DRAWN radius in screen pixels at the current zoom — what its frame is drawn at.</summary>
    private int NodeDrawRadius(MasteryKind k) => Math.Max(4, (int)(NodeRadius(k) * _zoom * 1.9f));

    /// <summary>
    /// Half the edge of a node's HIT BOX at the current zoom — the ONE square the hover, the left click
    /// and the right click all test (LAW 5; they used to carry three copies of it with two different
    /// floors). The drawn radius plus <see cref="HitSlop"/>, grown to the house minimum
    /// (<see cref="UiMetrics.HitTargetMinimum"/>, brief §107) wherever the tree's spacing allows it:
    /// never past half the closest pair's on-screen distance, so two boxes cannot claim one pixel.
    /// </summary>
    private int NodeHitHalf(MasteryKind k)
    {
        var room = (int)(ClosestPairWorld * _zoom / 2f) - 1;
        return Math.Max(NodeDrawRadius(k) + HitSlop, Math.Min(UiMetrics.HitTargetMinimum / 2, room));
    }

    /// <summary>The closest two nodes in the catalogue, in world units — measured once from the layout.</summary>
    private static readonly float ClosestPairWorld = MeasureClosestPair();

    private static float MeasureClosestPair()
    {
        var pts = MasteryCatalog.Nodes.Select(NodePos).ToArray();
        var best = float.MaxValue;
        for (var i = 0; i < pts.Length; i++)
            for (var j = i + 1; j < pts.Length; j++)
                best = MathF.Min(best, Vector2.Distance(pts[i], pts[j]));
        return best;
    }

    /// <summary>
    /// THE INSPECTOR (UX V2 P1.5, D3): the whole right column. It replaced two equal ornate frames — a
    /// standing YOUR STYLE chart that was an empty state for most of the game, and a node card with no
    /// control in it. What a node is, what it does, what it needs, what it costs, and the one button.
    /// </summary>
    // ITS WIDTH IS THE HOUSE INSPECTOR'S (UiMetrics.InspectorWidth): 496 at 100 %, wider at the larger
    // profiles so the bigger type keeps its line length — and the canvas beside it gives up the room.
    private static Rectangle InspectorPanel
        => new(UiKit.PageRight(PageMarginRight) - UiMetrics.InspectorWidth(UiKit.Page.Width), HeaderBottom,
               UiMetrics.InspectorWidth(UiKit.Page.Width), UiKit.PageBottom(PageMarginBottom) - HeaderBottom);

    /// <summary>The page's right margin beside the inspector — the same inset the other screens keep.</summary>
    private const int PageMarginRight = 16;

    /// <summary>The inspector's one button: anchored to the panel's foot, never inside the reading region.</summary>
    private static Rectangle TakeBtn => new(UiKit.ContentLeft(InspectorPanel), InspectorPanel.Bottom - TakeBtnClearance - TakeBtnHeight,
                                            UiKit.ContentRight(InspectorPanel) - UiKit.ContentLeft(InspectorPanel), TakeBtnHeight);

    private static int TakeBtnHeight => UiMetrics.Control(56);

    /// <summary>How far the button's foot clears the frame's bottom rail.</summary>
    private static int TakeBtnClearance => UiMetrics.Space(36);

    /// <summary>The refusal line sits this far above the button — its own height plus a breath.</summary>
    private static int RefusalTop => TakeBtn.Y - UiTypography.Secondary - UiMetrics.Space(11);

    /// <summary>
    /// THE READING REGION of the inspector: from the panel's title line down to just above the refusal
    /// line. What a node is, does, needs and costs is laid out into this; when the profile makes it
    /// taller than the room (150 % on a Specialisation with its hexagon), the region scrolls under the
    /// wheel and wears a scrollbar in its right lane, while the refusal and the button stay anchored
    /// below it (brief §17–§18: scroll long inspectors, never a primary action below the scroll).
    /// </summary>
    private static Rectangle InspectorBody
        => new(UiKit.ContentLeft(InspectorPanel), InspectorPanel.Y + UiTypography.PanelTitleTop,
               UiKit.ContentRight(InspectorPanel) - UiKit.ContentLeft(InspectorPanel),
               RefusalTop - UiMetrics.Space(10) - (InspectorPanel.Y + UiTypography.PanelTitleTop));

    // THE DOCKED HEXAGON scales as one drawing: its seals are glyph boxes and its radius keeps the
    // rails between them the same share of the seal, so a 150 % chart is the 100 % chart, larger.
    private static int InspectorHexRadius => UiMetrics.Control(70);
    private static int InspectorSealPx => UiMetrics.Control(30);   // ui-size-ok: the seal medallion's edge in pixels, not a text size
    // THE CEREMONY IS CENTRED ON THE PAGE, not on the canvas. It is drawn inside the overlay transform
    // like everything else on this screen, so at UI SCALE 125% a modal pinned to 960 sat off to the right.
    //
    // SIZED BY WHAT IT HOLDS (UI polish P2). It was a fixed 960×720, laid out to the pixel for 100 %:
    // the field at +86, the two lines at +586 and +610, the buttons at −84. At 150 % the caption ran
    // into the field and the lines ran under the buttons. Its height is now the stack — title, caption,
    // the field the hexagon needs, the two sentences (WRAPPED, since at 150 % the first is wider than
    // the modal), the buttons, the frame's clearance — and it is centred on the page at that height.
    // Its width grows at the spacing rate so the shoulder labels keep clear of the field's edge.
    private Rectangle SpecPanel
    {
        get
        {
            var w = SpecWidth;
            var h = SpecHeight;
            return new Rectangle(UiKit.PageCenterX - w / 2, (UiKit.Page.Height - h) / 2, w, h);
        }
    }

    /// <summary>
    /// The modal's width: 960 at 100 %, grown at the spacing rate — and never narrower than
    /// <see cref="MediumFrameAspect"/> times its height. The frame is chosen by aspect
    /// (<see cref="UiKit.PanelArtKey"/>), and at 125 % the stack had grown the modal taller than 1/1.30
    /// of its width, so it changed into the SQUARE frame: a crest 28 px deeper, side diamonds, and a
    /// caption that <see cref="UiKit.CaptionTop"/> dropped by the crest while the field did not — so
    /// SIX STYLES — ONE IS YOURS. was drawn under the field. One frame at every profile.
    /// </summary>
    private int SpecWidth => Math.Max(SpecWidthBase, (int)MathF.Ceiling(SpecHeight * MediumFrameAspect) + 1);

    private static int SpecWidthBase => UiMetrics.Space(960);   // ui-page-ok: 960 is this modal's own width at 100 %, and it fits the page

    /// <summary>The aspect from which <see cref="UiKit.PanelArtKey"/> hands a panel the medium frame — the one this modal wears.</summary>
    private const float MediumFrameAspect = 1.30f;

    /// <summary>The modal's height: its stack from the title to the buttons' foot. 730 at 100 %.</summary>
    private int SpecHeight
        => SpecFieldTop + SpecFieldHeight + UiMetrics.Space(8)
           + SpecLineCount * UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(4)
           + UiMetrics.ButtonHeight + UiTypography.PanelPadBottom;

    /// <summary>Where the ceremony's field starts, below the modal's top: under the caption's line and a breath.</summary>
    private static int SpecFieldTop => UiTypography.PanelCaptionTop + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(6);

    /// <summary>The field's inset from the modal's sides. 150 each side at 100 %.</summary>
    private static int SpecFieldInset => UiMetrics.Space(150);

    /// <summary>
    /// The field's height: the hexagon's reach to its farthest label, top and bottom, plus a margin —
    /// the 492 the ceremony has always drawn at 100 %, and what the same drawing needs at 150 %.
    /// </summary>
    private static int SpecFieldHeight
        => (CeremonyRadius + (int)(CeremonySealPx * StyleAffinityDiagram.RimFraction) + CeremonyLabelGap + UiTypography.Body) * 2
           + UiMetrics.Space(16) * 2;

    /// <summary>
    /// The two sentences under the field, wrapped to the modal's BASE content width: the modal can only
    /// be wider than that (<see cref="SpecWidth"/>), so lines that fit the base fit the modal — and the
    /// height they give it cannot depend on the width that depends on the height.
    /// </summary>
    private IReadOnlyList<string> SpecLines(string name)
    {
        var w = SpecWidthBase - UiTypography.PanelPadX * 2;
        return _ui.WrapBig($"YOUR {name} SKILLS HIT TWICE AS HARD. THE FAR STYLES HIT SOFTER —", w, UiTypography.Body)
                  .Concat(_ui.WrapBig("A VOW YOU ARE KEEPING PULLS EVERY FAR STYLE ONE RING CLOSER.", w, UiTypography.Body))
                  .ToList();
    }

    /// <summary>
    /// How many lines the sentences take at this profile — the longest style name is the one measured,
    /// so the modal's height does not change with the style being sealed. Memoised per profile: the
    /// modal's rectangle is read several times a frame and a wrap is not free.
    /// </summary>
    private int SpecLineCount
    {
        get
        {
            if (_specLinesFor != UiMetrics.Percent)
            {
                _specLineCount = SpecLines(SpecLongestName).Count;
                _specLinesFor = UiMetrics.Percent;
            }
            return _specLineCount;
        }
    }

    private int _specLinesFor = -1, _specLineCount;

    /// <summary>The widest style name, so the modal's height does not change with the style being sealed.</summary>
    private static readonly string SpecLongestName
        = Enum.GetValues<Style>().Select(Short).OrderByDescending(s => s.Length).First();

    /// <summary>Where the sentences start, below the modal's top.</summary>
    private static int SpecLinesTop => SpecFieldTop + SpecFieldHeight + UiMetrics.Space(8);

    /// <summary>Where the two buttons sit, below the modal's top.</summary>
    private int SpecButtonsTop => SpecLinesTop + SpecLineCount * UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(4);

    /// <summary>A ceremony button's width: 360 at 100 %, and never so wide that two cannot share the modal.</summary>
    private int SpecButtonWidth
        => Math.Min(UiMetrics.Control(360), (SpecWidth - UiTypography.PanelPadX * 3) / 2);

    /// <summary>The air at each side of the two buttons and between them — three equal shares of what they leave.</summary>
    private int SpecButtonGap => (SpecWidth - SpecButtonWidth * 2) / 3;

    private Rectangle SpecSealBtn => new(SpecPanel.X + SpecButtonGap, SpecPanel.Y + SpecButtonsTop, SpecButtonWidth, UiMetrics.ButtonHeight);
    private Rectangle SpecUndoBtn => new(SpecPanel.Right - SpecButtonGap - SpecButtonWidth, SpecPanel.Y + SpecButtonsTop, SpecButtonWidth, UiMetrics.ButtonHeight);
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
        mouse = Cursor(mouse);

        // THE BREATH is sampled here, so Draw reads a number and not a clock — and under Reduced
        // Motion the number is the breath's brightest, held.
        _breathPhase = BreathPhase(_breath.Elapsed.TotalSeconds);

        // THE CEREMONY'S FADE (brief §34), fired ONCE, from Update, never re-armed by a draw.
        //
        // THE EDGE IS WATCHED HERE, BUT A TAKE FIRES IT ITSELF (see OpenCeremony). This check runs at
        // the TOP of Update and the click that opens the ceremony is resolved at the BOTTOM of it, so
        // watching the edge here alone was one frame too late: Draw ran once with the pulse not yet
        // armed — a fully-lit modal — and only THEN blacked out and faded in. A pop, which is the one
        // thing an arrival must not be. This is now the safety net for the paths that set _specNodeId
        // without going through a take (DevSpecialise), and the record of the CLOSE edge, so the next
        // ceremony fades in again.
        if ((_specNodeId is not null) != _specWasOpen)
        {
            _specWasOpen = _specNodeId is not null;
            if (_specWasOpen) UiMotion.Flash(SpecKey, UiMotion.Transition);
        }

        // DEV: the posed take, landing in the Update the shutter's Draw follows (see the dial block
        // above). It goes through the REAL TakeNode, so what is photographed is the rule's own answer —
        // a node the fixture cannot afford poses the refusal instead of a staged one.
        if (!_devFired && Game1.RigActive && DevTakeId is { } devId
            && Game1.ShotFrameNow >= DevFireFrame && MasteryCatalog.ById(devId) is { } devNode)
        {
            _devFired = true;
            _pinnedNodeId = devNode.Id;
            TakeNode(devNode);
        }

        // ── THE CAMERA. Only while the tree page is open; the overview has nothing to pan. ────────
        if (_specNodeId is not null) { _dragFrom = null; _draggedThisPress = false; }

        if (_specNodeId is null)
        {
            var over = mouse;

            if (wheel != 0 && TreeView.Contains(over) && !OverDock(over))
                ZoomAt(over, wheel > 0 ? 1.16f : 1f / 1.16f);

            // THE INSPECTOR SCROLLS UNDER THE WHEEL when its reading is taller than its room (150 % on
            // a Specialisation). A notch is one Body line; the last page stays full (UiKit.Scrolled).
            if (wheel != 0 && InspectorPanel.Contains(over))
                _inspectorScroll = UiKit.Scrolled(_inspectorScroll, wheel * UiTypography.Pitch(UiTypography.Body),
                                                  _inspectorVisible, _inspectorTotal);

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
                var rr = NodeHitHalf(node.Kind);
                if (Math.Abs(rhit.X - rp.X) > rr || Math.Abs(rhit.Y - rp.Y) > rr) continue;
                _pinnedNodeId = node.Id;
                GiveBack(node, "ONE POINT RETURNED.");
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
                // THE ONE COMMITMENT THIS SCREEN MAKES, so it gets the cue with weight (audio README).
                Say($"{_specStyle.ToString().ToUpperInvariant()} IS YOUR STYLE — ITS SKILLS HIT TWICE AS HARD.", attId, "sfx_bind");
            }
            else if (SpecUndoBtn.Contains(hit))
            {
                Mastery.Refund(attId);
                _specNodeId = null;
                Dirty = true;
                Say("THE POINTS ARE BACK. CHOOSE A STYLE WHEN YOU ARE READY.", attId, "sfx_click");
                UiMotion.Flash(PointsKey, UiMotion.Transition);
            }
            return;
        }

        // ── Edit sub-view: tree + right column. (No BACK and no WEAVE door here any more: MASTERY is
        //    its own rail tile, and a BACK on a page with its own door claims a nesting that does not
        //    exist — the rail is how you leave, same as every other screen.) ──

        if (ResetBtn.Contains(hit) && Mastery.Spent > 0)
        {
            // WHAT IT WILL COST, BEFORE IT COSTS IT (BRIEF sec.19). Access follows the allocation now, so
            // taking every point back takes back every shared skill's road with them, and the composer
            // would simply stop carrying those slots. The first press names them; the second clears them.
            //
            // TWO CLICKS, because it un-spends every point in the tree and there is no undo prompt
            // anywhere else in this game. The respec itself stays free and instant — that is the
            // load-bearing difference between this tree and the permanent one — so the guard is
            // deliberation, not cost.
            //
            // AND IT STAYS CALM (the polish spec for this screen). Arming and confirming each get the
            // dry click every ordinary button gets, and the only motion is the one flash on the figure
            // that changed. No bloom on the twenty nodes it emptied: a respec is a correction, not a
            // reward, and twenty simultaneous celebrations would read as a fault.
            if (!_resetArmed)
            {
                _resetArmed = true;
                var losing = RespecWouldUnequip();
                Say(losing.Count == 0
                        ? "PRESS AGAIN TO TAKE EVERY POINT BACK."
                        : $"PRESS AGAIN TO TAKE EVERY POINT BACK — {Names(losing)} WILL BE UNEQUIPPED.",
                    null, "sfx_click");
                return;
            }
            _resetArmed = false;
            Mastery.Respec();
            // AND NOW THE SLOTS MATCH THE RULES AGAIN. A respec is never blocked by an equipped skill
            // (sec.19); it warns, commits, and then empties exactly the slots it made unusable. The
            // skills' LEVELS are not touched by any of this — SkillProgress is keyed by skill id and has
            // never heard of mastery (LAW 4).
            var cleared = LoadoutRepair.Repair(Loadout, Mastery.AvailableSkills(), Character);
            Say(cleared.Count == 0
                    ? "ALL MASTERY POINTS RETURNED."
                    : $"ALL MASTERY POINTS RETURNED. {Names(cleared)} LEFT YOUR SLOTS — EVERY LEVEL IS KEPT.",
                null, "sfx_click");
            UiMotion.Flash(PointsKey, UiMotion.Transition);
            Dirty = true;
            return;
        }
        // The warning list is a CARD, not glass: a click on it must not spend a point on whatever node
        // happens to sit under it. It disarms, like any other click, and stops there.
        if (_resetArmed && RespecWarning.Contains(hit)) { _resetArmed = false; return; }
        // Any other click on the tree disarms it — but must NOT return, or no node could be taken.
        _resetArmed = false;

        // The docked cards are CARDS, not glass: a click on them must not take, pin, or refund the
        // node that happens to sit underneath (at the default framing, Tempo's rim does).
        // THE INSPECTOR'S BUTTON (UX V2 P1.5): TAKE the pinned node, or GIVE BACK a taken one — the same
        // rules a click on the node itself runs, so the two doors cannot disagree.
        if (TakeBtn.Contains(hit) && _pinnedNodeId is { } pinId && MasteryCatalog.ById(pinId) is { } pinned)
        {
            if (Mastery.IsTaken(pinId)) GiveBack(pinned, "POINTS RETURNED.");
            else TakeNode(pinned);
            return;
        }

        if (OverDock(hit)) return;

        foreach (var node in MasteryCatalog.Nodes)
        {
            if (node.Kind == MasteryKind.Start) continue;
            // The SAME transform the drawing uses, or the click lands where the node used to be.
            var sp = Screen(NodePos(node));
            var half = NodeHitHalf(node.Kind);
            if (Math.Abs(hit.X - sp.X) > half || Math.Abs(hit.Y - sp.Y) > half) continue;
            // Clicking a node PINS it in the detail panel whether or not it could be taken — a node you
            // cannot afford is exactly the one you most want to read.
            _pinnedNodeId = node.Id;


            TakeNode(node);
            return;
        }

        // Skills, Vows and keystone sockets are all LoadoutScreen's now.
    }

    /// <summary>
    /// Which EQUIPPED skills a respec would leave without a mastery node, in slot order.
    /// </summary>
    /// <remarks>
    /// Derived, never assumed. It builds the tree a respec actually leaves behind — nothing taken but
    /// START — and asks that tree what it teaches, so this answer cannot drift from what
    /// <see cref="MasteryTree.Respec"/> does. The champion's own signature is exempt from mastery
    /// (BRIEF sec.18) and so is never in this list, which is exactly what
    /// <see cref="LoadoutRepair"/> checks first.
    /// </remarks>
    private IReadOnlyList<SkillDef> RespecWouldUnequip()
    {
        var after = new MasteryTree();
        after.RestoreTaken(Array.Empty<string>());
        return LoadoutRepair.UnusableSkills(Loadout, after.AvailableSkills(), Character);
    }

    /// <summary>A list of skill names as a sentence reads them: A, B AND C.</summary>
    private static string Names(IReadOnlyList<SkillDef> defs)
    {
        var words = defs.Select(d => d.Name.ToUpperInvariant()).ToList();
        return words.Count switch
        {
            0 => "",
            1 => words[0],
            _ => string.Join(", ", words.Take(words.Count - 1)) + " AND " + words[^1],
        };
    }

    /// <summary>DEV: arm the respec, so the capture rig can photograph the warning it raises.</summary>
    /// <remarks>
    /// A state no capture mode can pose is a state nobody has looked at. The warning only exists between
    /// two presses of one button and a capture never clicks, so without this dial the one screen in the
    /// game that tells the player what a decision will cost could never be checked against the reflow
    /// contract at 125 % or 150 %.
    /// </remarks>
    public void DevArmRespec()
    {
        if (Mastery.Spent <= 0) return;
        _resetArmed = true;
        var losing = RespecWouldUnequip();
        if (losing.Count > 0) Say($"PRESS AGAIN TO TAKE EVERY POINT BACK — {Names(losing)} WILL BE UNEQUIPPED.", null, null);
    }

    /// <summary>
    /// Say something in the inspector's one line, ABOUT one node — and name the sound the host should
    /// play for it.
    /// </summary>
    /// <remarks>
    /// The line and the node it belongs to are set together because they were drifting apart: the
    /// message was a single field with no owner, so a refusal raised by one node followed the pointer
    /// onto every other node's card and told the player the wrong reason (brief §29 wants a disabled
    /// control to say ITS OWN why). A null <paramref name="nodeId"/> is a line about the whole tree —
    /// the respec's two — and is shown whatever the inspector is reading.
    /// </remarks>
    private void Say(string msg, string? nodeId, string? cue)
    {
        _msg = msg;
        _msgNodeId = nodeId;
        if (cue is not null) _cue = cue;
    }

    /// <summary>Hand a node's points back, or say exactly why not — the right click and the inspector's button alike.</summary>
    /// <remarks>
    /// The two doors used to carry a copy each, and the copies had already drifted (one tested
    /// <c>IsTaken</c> and one did not). The words each door says are still its own; the RULE is shared.
    /// A refund is the calm inverse of a take: the AVAILABLE figure reacts, the wire this node lit
    /// eases back down on its own (<see cref="Lift"/> reads the tree, not an event), and nothing blooms.
    /// </remarks>
    private void GiveBack(MasteryNode node, string returned)
    {
        if (Mastery.Refund(node.Id))
        {
            Say(returned, node.Id, "sfx_click");
            UiMotion.Flash(PointsKey, UiMotion.Transition);
            Dirty = true;
            return;
        }
        Say(Mastery.IsTaken(node.Id) ? "ANOTHER NODE DEPENDS ON THIS ONE." : "NOTHING SPENT HERE.",
            node.Id, "sfx_error");
    }

    /// <summary>
    /// Open the SPECIALISATION ceremony, and start its arrival on the SAME frame the take opened it.
    /// </summary>
    /// <remarks>
    /// The fade is armed here rather than by <see cref="Update"/>'s edge watch because the watch runs
    /// before the click that opens this — so the first Draw would find no pulse armed, read the modal
    /// as already open, and paint it at full light for one frame before the fade blacked it out and
    /// brought it back. The ceremony's own length is untouched: this is only its arrival (brief §34).
    /// </remarks>
    private void OpenCeremony(string nodeId, Style style)
    {
        _specNodeId = nodeId;
        _specStyle = style;
        _specWasOpen = true;
        UiMotion.Flash(SpecKey, UiMotion.Transition);
    }

    /// <summary>Take a node, or say exactly why not — for a click on the node and for the inspector's button alike.</summary>
    private void TakeNode(MasteryNode node)
    {
        var firstSpec = node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is null;
        if (Mastery.Take(node.Id))
        {
            // THE TAKE, FELT (brief §30–§31, the polish spec for this screen): the node blooms ONCE over
            // a Transition, the AVAILABLE figure it just cost reacts (§36–§37), and the wire into it
            // lights over Fast — that last one is not fired here because it is not an event, it is a
            // state: DrawTreeNodes eases every wire toward "both ends taken", so the light arrives on
            // the take and leaves on the refund without a second copy of the rule.
            Say("", node.Id, node.Kind == MasteryKind.Mastery ? "sfx_bind" : "sfx_weave");
            UiMotion.Flash(NodeKey(node.Id), UiMotion.Transition);
            UiMotion.Flash(PointsKey, UiMotion.Transition);
            Dirty = true;
            // THE SPECIALISATION: your first is the moment this game is named for, so it gets a ceremony
            // instead of a click-sound — the hexagon shown whole, the choice sealed or taken back.
            if (firstSpec && node.Style is { } nf) OpenCeremony(node.Id, nf);
            return;
        }
        // WHY THE CLICK DID NOTHING, NAMED EXACTLY — from the rule the refusal came from.
        Say(Mastery.IsTaken(node.Id) ? "ALREADY TAKEN." :
            Mastery.Available < node.Cost
                ? $"NEEDS {node.Cost} POINTS — YOU HAVE {Mastery.Available}. GO DEEPER." :
            node.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() is { } heldBranch
                ? $"YOU ALREADY TOOK THE {Short(heldBranch)} CAPSTONE — ONE CAPSTONE PER HUNTER." :
            node.Kind == MasteryKind.Specialisation && Mastery.Affinity() is not null
                ? "YOU ALREADY CHOSE A STYLE — ONE STYLE PER HUNTER." :
            "TAKE A CONNECTED NODE FIRST.",
            node.Id, "sfx_error");
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch b, Point mouse, MemoryDustTree tree)
    {
        Tree = tree;
        _hoverNodeId = null;
        // Inverts the overlay inset this screen is drawn through (Game1.OverlayScale).
        // The SAME posed cursor Update works with, or a capture would hit-test one point and draw another.
        var hit = Cursor(mouse);
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
        => _ui.TextBig(b, $"nav MASTERY  tree  zoom {_zoom:0.00}  pan {_pan.X:0},{_pan.Y:0}", 60, HeaderBottom, Gold, UiTypography.Secondary);

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
            // THE CONNECTION LIGHTS OVER FAST when the take joins both ends, and eases back down when a
            // refund parts them (the polish spec for this screen; brief §31's 80–120 ms band). It is a
            // STATE, eased, not a fired pulse: the wire is gold exactly while the tree says both ends
            // are taken, so a save that loads a walked branch cannot get stuck half-lit, and Reduced
            // Motion lands on the same end colour with no fade (UiMotion.Ease does that itself).
            var walked = Mastery.IsTaken(node.Id) && Mastery.IsTaken(nearest.Id);
            var lit = Lift(WireKey(node.Id), walked, UiMotion.Fast);
            if (DevPose is { } wp && node.Id == DevTakeId) lit = PosedWire(wp);
            _ui.LineSeg(b, a, c, WireThickness, lit <= 0f ? Path * 0.55f : Color.Lerp(Path * 0.55f, Gold, lit));
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
        // ── THE TOP STRIP — the house pattern, to the pixel at 100 %; derived so it stacks at 150 %. ──
        _ui.TextCenterBig(b, "MASTERY TREE", UiKit.PageCenterX, TitleTop, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.Fill(b, new Rectangle(UiKit.PageCenterX - 260, RuleTop, 520, 3), Gold * 0.5f);

        // The caption is the screen's, and only the screen's. Refusals and results speak in the inspector,
        // beside the button that raised them (UX V2 P1.5) — not five hundred pixels away under the title.
        _ui.TextCenterBig(b, "FOUR DIRECTIONS  ·  ONE STYLE  ·  TWELVE SKILLS TO LEARN", UiKit.PageCenterX, CaptionTop, Slate, UiTypography.Secondary);

        // ── THE CORNER PLATE: the points you can spend, what you have spent, and your style. ──
        _ui.PanelQuiet(b, PointsPanel);
        var px0 = PointsPanel.X + PointsPad;
        var medal = new Rectangle(px0, PointsPanel.Y + UiMetrics.Space(20), PointsMedalPx, PointsMedalPx);
        if (_ui.Assets.Get("ui_medallion_round") is { } ring) b.Draw(ring, medal, Color.White);
        if (_ui.Assets.Get("state_mastery_128") is { } glyph)
        {
            // The glyph fills the medallion's hollow — 30 of the ring's 56 source px, at whatever size it draws.
            var g = medal.Width * 30 / 56;
            b.Draw(glyph, new Rectangle(medal.X + (medal.Width - g) / 2, medal.Y + (medal.Height - g) / 2, g, g), Gold);
        }
        // NEVER SLATE AT ZERO: the most important figure on the page must not read as switched off in
        // the exact state where the player most needs to read it. Gold when there is something to spend.
        var figureX = medal.Right + UiMetrics.Space(14);
        var figureY = PointsPanel.Y + PointsNumberTop;
        // IT REACTS WHERE THE POINTS LIVE (brief §36–§37). A take, a refund and a respec each fire one
        // Transition flash on THIS figure — the resource pill of this screen — so a spend is felt at
        // the number it changed instead of by flying something across the page. It is a brief emphasis
        // on a figure that is already there: no new label, no counter, no toast.
        var spend = UiMotion.Pulse(PointsKey);
        var figureCol = Mastery.Available > 0 ? Gold : Bone;
        if (spend > 0f) figureCol = Color.Lerp(figureCol, Color.White, 0.85f * spend);
        _ui.TextBig(b, $"{Mastery.Available}", figureX, figureY, figureCol, UiTypography.PrimaryValue);
        _ui.TextBig(b, "AVAILABLE", figureX + _ui.MeasureBig($"{Mastery.Available}", UiTypography.PrimaryValue) + UiMetrics.Space(10),
                    figureY + UiTypography.PrimaryValue - UiTypography.Secondary - 2, Slate, UiTypography.Secondary);
        _ui.TextBig(b, $"{Mastery.Spent} SPENT", figureX, figureY + UiTypography.Pitch(UiTypography.PrimaryValue), Slate, UiTypography.Secondary);
        var styleLine = Mastery.Affinity() is { } st ? $"STYLE · {Short(st)}  x2.0" : "NO STYLE YET";
        _ui.TextBig(b, _ui.ShortenBig(styleLine, PointsPanel.Width - PointsPad * 2, UiTypography.Body), px0, PointsPanel.Bottom - PointsPad - UiTypography.Body,
                    Mastery.Affinity() is not null ? Gold : Slate, UiTypography.Body);

        if (Mastery.Spent > 0)
            Button(b, ResetBtn, _resetArmed ? "PRESS AGAIN TO CONFIRM" : "TAKE EVERY POINT BACK", hit, true);

        // ── WHAT THE SECOND PRESS WILL COST (BRIEF sec.19). Armed, and only armed: the player has asked
        //    once, and this is the answer before they ask again. Nothing is blocked by it. ──
        if (!_resetArmed) return;
        var losing = RespecWouldUnequip();
        if (losing.Count == 0) return;
        var band = RespecWarning;
        var pad = RespecWarningPad;
        var box = new Rectangle(band.X, band.Y, band.Width,
                                pad * 2 + UiTypography.Pitch(UiTypography.Secondary) + losing.Count * UiTypography.Pitch(UiTypography.Body));
        _ui.PanelQuiet(b, box);
        var wy = box.Y + pad;
        _ui.TextBig(b, "THIS RESPEC WILL UNEQUIP", box.X + pad, wy, Ember, UiTypography.Secondary);
        wy += UiTypography.Pitch(UiTypography.Secondary);
        foreach (var d in losing)
        {
            _ui.TextBig(b, _ui.ShortenBig(d.Name.ToUpperInvariant(), box.Width - pad * 2, UiTypography.Body), box.X + pad, wy, Bone, UiTypography.Body);
            wy += UiTypography.Pitch(UiTypography.Body);
        }
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
        _ui.TextCenterBig(b, Short(br), (int)p.X, (int)p.Y - HeaderTitleRise, walked ? col : col * 0.72f,
                          UiTypography.PanelTitle);
        // The promise, on as many lines as its room gave it (see Overview) — one everywhere at 100 %.
        var py = (int)p.Y + HeaderPromiseDrop;
        foreach (var line in Overview.Promises[br])
        {
            _ui.TextCenterBig(b, line, (int)p.X, py, Slate, UiTypography.Secondary);
            py += UiTypography.Pitch(UiTypography.Secondary);
        }
    }

    /// <summary>
    /// The ceremony's own label clearance. Its hexagon is twice the docked one's, on a modal with room
    /// to spare, so its names stand further off their seals — but off the SEAL, like the chart's, so
    /// the two drawings of the same object are the same drawing.
    /// </summary>
    private static int CeremonyLabelGap => UiMetrics.Space(20);

    /// <summary>The ceremony's hexagon and seals — the same drawing, at the size a modal can afford.</summary>
    /// <remarks>
    /// 150 at 100 %: the ceremony's field is 492 tall there — 246 px per half, less a 16 px margin,
    /// less the seal's rim (38) + the gap (20) + one Body line (22) — and the field is now derived from
    /// this the other way round (<see cref="SpecFieldHeight"/>). The whole drawing — radius, seal and
    /// gap — grows at the SPACING rate: the modal has to keep fitting under the page with its two
    /// sentences and its buttons, and at the full rate it would not (1058 px tall at 150 %).
    /// </remarks>
    private static int CeremonyRadius => UiMetrics.Space(150);

    /// <inheritdoc cref="CeremonyRadius"/>
    private static int CeremonySealPx => UiMetrics.Space(62);   // ui-size-ok: the seal medallion's edge in pixels, not a text size

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
        // IT ARRIVES, IT DOES NOT APPEAR (brief §34: a quick backdrop fade and a short panel fade).
        // ONE Transition, fired from Update on the frame the ceremony opens — the CEREMONY ITSELF keeps
        // its length: it stays open until the player seals it or hands the points back, exactly as
        // before. A fade and nothing else: no scale, no slide, so Reduced Motion has nothing to strip —
        // and it takes the instant path there anyway, landing on the same fully-lit panel.
        var open = CeremonyFade(DevPose is { } cp ? 1f - cp : UiMotion.Pulse(SpecKey));
        _ui.Scrim(b, 0.75f * open);
        _ui.Panel(b, SpecPanel, gold: true);

        var name = _specStyle.ToString().ToUpperInvariant();
        var modal = SpecPanel;
        _ui.TextCenterBig(b, "YOUR SPECIALISATION", modal.Center.X, UiKit.TitleTop(modal), Gold, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, "SIX STYLES — ONE IS YOURS.", modal.Center.X, UiKit.CaptionTop(modal), Slate, UiTypography.Body);

        // THE CEREMONY STANDS ON THE SAME FIELD THE DOCKED CHART DOES. It used to have none — the
        // hexagon floated on the modal's black interior — so the game's founding moment was the one
        // place the chart was drawn without the surface that makes it a chart.
        var field = new Rectangle(modal.X + SpecFieldInset, modal.Y + SpecFieldTop, modal.Width - SpecFieldInset * 2, SpecFieldHeight);
        StyleAffinityDiagram.Field(_ui, b, field);
        StyleAffinityDiagram.Draw(_ui, b, field.Center, CeremonyRadius, _specStyle, showFactors: true,
                            labelGap: CeremonyLabelGap, labelPx: UiTypography.Body,
                            factorPx: UiTypography.Secondary, sealPx: CeremonySealPx);

        // WRAPPED, not pinned: at 150 % the first sentence is wider than the modal.
        var ly = modal.Y + SpecLinesTop;
        foreach (var line in SpecLines(name))
        {
            _ui.TextCenterBig(b, line, modal.Center.X, ly, Bone, UiTypography.Body);
            ly += UiTypography.Pitch(UiTypography.Body);
        }

        Button(b, SpecSealBtn, $"CHOOSE {name}", hit, true);
        Button(b, SpecUndoBtn, "NOT YET — TAKE THE POINTS BACK", hit, true);

        // The panel fading UP from the page's own black — one veil over the modal rather than an alpha
        // threaded through thirty draw calls, which is what a "fade the panel in" would otherwise cost.
        // Drawn last, so it covers the buttons too; gone entirely once the fade has run.
        if (open < 1f) _ui.Fill(b, modal, new Color(0x0A, 0x08, 0x10) * (1f - open));
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

        var id = _hoverNodeId ?? _pinnedNodeId;
        var n = id is null ? null : MasteryCatalog.ById(id);
        // A different reading starts at its top: a scroll position belongs to the text it was read in.
        if ((n?.Id ?? "") != _inspectorKey) { _inspectorKey = n?.Id ?? ""; _inspectorScroll = 0; }

        // ── THE READING REGION (UI polish P2). It used to lay out against a floor and silently DROP
        //    whatever did not fit — the hexagon, YOU NEED FIRST, the cost — which at 150 % was most of a
        //    Specialisation's card. It is measured first, then drawn; when it is taller than its room it
        //    scrolls under a scissor and wears a scrollbar, and its lines give up the bar's lane. ──
        var body = InspectorBody;
        var w = body.Width;
        var total = LayoutDetail(b, n, body.X, body.Y, w, draw: false);
        var scrolls = total > body.Height;
        if (scrolls)
        {
            w -= UiMetrics.ScrollbarWidth + UiMetrics.Gap;
            total = LayoutDetail(b, n, body.X, body.Y, w, draw: false);
        }
        _inspectorTotal = total;
        _inspectorVisible = body.Height;
        _inspectorScroll = Math.Clamp(_inspectorScroll, 0, Math.Max(0, total - body.Height));

        if (scrolls)
        {
            // The same batch dance the tree canvas does (DrawTreeWorld): close the host's batch, clip,
            // draw, reopen unclipped for the chrome that follows.
            b.End();
            _ui.Device.ScissorRectangle =
                Rectangle.Intersect(Game1.OverlayToCanvas(body, Vector2.Zero), new Rectangle(0, 0, 1920, 1080));
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, Clip, null, Game1.OverlayTransform(Vector2.Zero));
            try
            {
                LayoutDetail(b, n, body.X, body.Y - _inspectorScroll, w, draw: true);
            }
            finally
            {
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        null, null, null, Game1.OverlayTransform(Vector2.Zero));
            }
            _ui.ScrollBar(b, new Rectangle(body.Right - UiMetrics.ScrollbarWidth, body.Y, UiMetrics.ScrollbarWidth, body.Height),
                          _inspectorScroll, body.Height, total);
        }
        else LayoutDetail(b, n, body.X, body.Y, w, draw: true);

        if (n is null) return;

        // ── ANCHORED, never inside the scroll: the refusal beside the button that would have done it, then
        //    the button — the panel's one action, always where the hand expects it. ──
        var btn = TakeBtn;
        var taken = Mastery.IsTaken(n.Id);
        var can = Mastery.CanTake(n.Id);
        // EVERY DISABLED NODE SAYS ITS OWN WHY (brief §29). The spoken line — the one a click just
        // raised — belongs to the node it was raised for, so it is only read here while THAT node is
        // the one on the card; every other node falls through to the reason its own rules give. Before
        // this, one refusal followed the pointer across the tree and told the player the wrong thing
        // about every node they hovered next.
        var said = _msg.Length > 0 && (_msgNodeId is null || _msgNodeId == n.Id) ? _msg : "";
        var refusal = said.Length > 0 ? said
            : taken || can ? ""
            : Mastery.Available < n.Cost ? $"NEEDS {n.Cost} POINTS — YOU HAVE {Mastery.Available}. GO DEEPER."
            : n.Kind == MasteryKind.Specialisation && Mastery.Affinity() is not null ? "ONE STYLE PER HUNTER — ALREADY CHOSEN."
            : n.Kind == MasteryKind.Mastery && Mastery.MasteredBranch() is not null ? "ONE CAPSTONE PER HUNTER — ALREADY TAKEN."
            : "TAKE A CONNECTED NODE FIRST.";
        if (refusal.Length > 0)
            _ui.TextBig(b, _ui.ShortenBig(refusal, body.Width, UiTypography.Secondary), body.X, RefusalTop,
                        said.Length > 0 && (taken || can) ? UiInk.Good : Ember, UiTypography.Secondary);
        var label = taken ? "GIVE BACK" : $"TAKE  ·  {n.Cost} POINT{(n.Cost == 1 ? "" : "S")}";
        _ui.Button(b, btn, label, hit, false, taken || can, !taken && can ? ButtonStyle.Primary : ButtonStyle.Secondary);
    }

    /// <summary>What the inspector last laid out, so the wheel knows how far it may scroll.</summary>
    private int _inspectorScroll, _inspectorTotal, _inspectorVisible;
    private string _inspectorKey = "";

    /// <summary>
    /// The inspector's reading, laid out from <paramref name="y"/> into a column <paramref name="w"/>
    /// wide — CATEGORY · NAME · what it does · what you need first · what it costs — or, with no node,
    /// the tree's own primer. Returns the height it took. With <paramref name="draw"/> false it only
    /// measures, which is how <see cref="DrawNodeDetail"/> learns whether the region has to scroll.
    /// </summary>
    private int LayoutDetail(SpriteBatch b, MasteryNode? n, int x, int y, int w, bool draw)
    {
        var top = y;

        void Section(string s, Color? c = null)
        {
            if (draw) _ui.TextBig(b, s, x, y, c ?? Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
        }
        void Line(string s, Color c, int px = 0, int maxLines = 3)
        {
            if (px == 0) px = UiTypography.Body;   // a rung is a profile-scaled property, not a constant
            foreach (var l in _ui.WrapBig(s, w, px).Take(maxLines))
            {
                if (draw) _ui.TextBig(b, l, x, y, c, px);
                y += UiTypography.Pitch(px);
            }
        }
        void Rule()
        {
            if (draw) _ui.Fill(b, new Rectangle(x, y + UiMetrics.Space(6), w, 1), Dim);
            y += UiMetrics.Space(16);
        }

        if (n is null)
        {
            Section("MASTERY TREE");
            if (draw) _ui.TextBig(b, "PICK A NODE TO READ IT", x, y, UiInk.Empty, UiTypography.Headline);
            y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
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
            return y - top;
        }

        var col = BranchColor(n.Branch);
        var taken = Mastery.IsTaken(n.Id);
        var can = Mastery.CanTake(n.Id);
        var roadDef = n.Kind == MasteryKind.SkillRoad && n.GrantsSkillId is { } rs ? SkillCatalogue.Find(rs) : null;
        var learned = roadDef is not null && Mastery.AvailableSkills().Contains(roadDef.Id);

        // CATEGORY · BRANCH, in the branch's colour; NAME at Headline — gold once it is yours.
        if (draw) _ui.Fill(b, new Rectangle(x, y - UiMetrics.Space(6), 5, UiTypography.Pitch(UiTypography.Secondary) + UiTypography.Pitch(UiTypography.Headline)), col);
        var xs = x; var indent = UiMetrics.Space(16); x += indent; w -= indent;
        var kindHead = n.Kind switch { MasteryKind.Specialisation => "SPECIALISATION", MasteryKind.SkillRoad => "SKILL", MasteryKind.Mastery => "CAPSTONE", _ => KindWord(n.Kind) };
        Section(_ui.ShortenBig($"{kindHead}  ·  {Short(n.Branch)}{(n.Link is { } lk ? $" + {Short(lk)}" : "")}", w, UiTypography.Secondary), col);
        var name = roadDef is not null ? roadDef.Name
                 : n.Kind == MasteryKind.Specialisation && n.Style is { } ss ? $"{Short(ss)} SPECIALISATION"
                 : Head(n.Label);
        if (roadDef is not null)
        {
            // The skill's icon beside its name: a box one breath taller than the Headline it sits by.
            var icon = UiTypography.Headline + UiMetrics.Space(8);
            if (draw)
            {
                _ui.Icon(b, $"icon_skill_{roadDef.Id}", new Rectangle(x, y - 2, icon, icon), learned || taken ? Gold : Bone);
                _ui.TextBig(b, name, x + icon + UiMetrics.Space(10), y, taken ? Gold : Bone, UiTypography.Headline);
            }
        }
        else if (draw) _ui.TextBig(b, name, x, y, taken ? Gold : Bone, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
        x = xs; w += indent;

        // WHAT IT DOES: the label's tail, the stat lines, the kind's own sentence.
        Section("WHAT IT DOES");
        var tail = Tail(n.Label);
        if (tail.Length > 0) Line(tail, Bone, UiTypography.Body, 3);
        if (n.Stats is { Count: > 0 } stats)
            foreach (var kv in stats.Take(3)) Line($"+{kv.Value:0} {kv.Key.ToString().ToUpperInvariant()}", Bone, UiTypography.Body, 1);
        if (roadDef is not null)
        {
            Line($"TEACHES {roadDef.Name.ToUpperInvariant()} — {roadDef.Line}", Bone, UiTypography.Body, 3);
            // ACCESS FOLLOWS THE ALLOCATION (BRIEF sec.15-16, LAW 3). These two lines used to promise the
            // opposite — "LEARNED, FOR GOOD; RESPEC RETURNS THE POINTS, NEVER THE SKILL" — which made every
            // road node free to buy, refund and keep. Giving the node back now closes the skill again. What
            // it never closes is the WAVES spent on it: those are the skill's own and are always kept.
            Line(learned ? "UNLOCKED WHILE THIS NODE IS TAKEN. GIVE THE NODE BACK AND THE SKILL LOCKS AGAIN — ITS LEVELS ARE ALWAYS KEPT."
                         : "TAKING IT UNLOCKS THE SKILL FOR AS LONG AS YOU KEEP THIS NODE. EQUIP IT ON THE BUILD SCREEN.",
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
            // Always drawn now — the region scrolls rather than dropping it when the room runs out.
            var gap = UiMetrics.Space(8);
            var need = (InspectorHexRadius + InspectorSealPx + gap + UiTypography.Body) * 2 + UiMetrics.Space(24);
            if (draw)
            {
                var field = new Rectangle(x, y + gap, w, need - gap * 2);
                StyleAffinityDiagram.Field(_ui, b, field);
                StyleAffinityDiagram.Draw(_ui, b, field.Center, InspectorHexRadius, specStyle, showFactors: true,
                                          labelGap: gap, labelPx: UiTypography.Secondary, factorPx: UiTypography.Secondary, sealPx: InspectorSealPx);
            }
            y += need;
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
        if (draw)
        {
            _ui.TextBig(b, taken ? "PAID" : "COST", x, y + UiMetrics.Space(6), Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, $"{n.Cost} POINT{(n.Cost == 1 ? "" : "S")}", x + w, y, taken ? Gold : can ? Bone : Ember, UiTypography.Headline);
        }
        y += UiTypography.Pitch(UiTypography.Headline);
        if (!taken)
        {
            if (draw)
            {
                _ui.TextBig(b, "YOU HAVE", x, y + UiMetrics.Space(6), Slate, UiTypography.Secondary);
                _ui.TextRightBig(b, $"{Mastery.Available}", x + w, y, Mastery.Available >= n.Cost ? Bone : Ember, UiTypography.Headline);
            }
            y += UiTypography.Pitch(UiTypography.Headline);
        }
        return y - top;
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

    /// <summary>
    /// A node's own colour carrying its HOVER and PRESSED states — the one place the two are applied,
    /// so the art path and the greybox path cannot disagree about what a hovered node looks like.
    /// </summary>
    /// <remarks>
    /// Hover ARRIVES at exactly the Bone the screen has always used — the end state is not touched, only
    /// the way it is reached (§26: ~80–120 ms, restrained; LAW 1: this is polish, not a redesign).
    /// PRESSED takes the light back out with <see cref="Muted"/> rather than with an alpha, so a held
    /// node DIMS instead of turning translucent over the wallpaper.
    /// </remarks>
    private static Color StateTint(Color c, float hoverLift, bool pressed)
    {
        if (hoverLift > 0f) c = Color.Lerp(c, Bone, hoverLift);
        return pressed ? Muted(c, 0.74f) : c;
    }

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
        var rad = NodeDrawRadius(node.Kind);

        // Cull. A tree meant to grow will one day have far more nodes off screen than on it.
        if (sp.X < TreeView.X - 200 || sp.X > TreeView.Right + 200
            || sp.Y < TreeView.Y - 200 || sp.Y > TreeView.Bottom + 200) return;

        var cx = (int)sp.X;
        // The SAME square the click tests, so what lights up is what takes the click (LAW 5). Tested
        // against the node's TRUE centre, never the pressed one: a control that moved out from under
        // the pointer as you pressed it would be a bug, not feedback (UiKit.Button does the same).
        var cyHit = (int)sp.Y;
        var half = NodeHitHalf(node.Kind);
        var hover = !OverDock(mouse)
                    && Math.Abs(mouse.X - cx) <= half && Math.Abs(mouse.Y - cyHit) <= half;

        var taken = Mastery.IsTaken(node.Id);
        var canTake = Mastery.CanTake(node.Id);
        var branchCol = BranchColor(node.Branch);

        // ══ THE STANDARD STATES, ON A NODE (brief §25–§29). A node is a control, drawn by hand, so it
        //    has to answer all six itself — UiKit.Button cannot do it for a diamond on a camera.
        //
        //    NORMAL   colour says branch, brightness says taken / takeable / locked (below).
        //    HOVER    a luminance lift toward Bone, eased in over Fast. Restrained: no size change,
        //             nothing jumps, and it reads at the whole-tree framing where a node is 28 px.
        //    PRESSED  the same 2 px depression the house button drops, and the light banked down —
        //             immediate, and held for exactly as long as the button is.
        //    SELECTED a persistent gold ring OUTSIDE the node: the node the inspector is reading. It
        //             is a different channel from hover on purpose (§28) — hover brightens the node
        //             itself, selection draws a ring around it — so a hovered node and the pinned node
        //             are told apart at a glance, including when they are the same node.
        //    DISABLED the locked treatment, which stays readable rather than vanishing; the REASON is
        //             one plain line in the inspector, that node's own (see Say / DrawNodeDetail).
        //    FOCUSED  this screen has no keyboard focus ring — the arrows drive the camera, not a
        //             cursor between nodes — so there is no focused state to draw. See the report.
        var hoverLift = Lift(HoverKey(node.Id), hover && node.Kind != MasteryKind.Start);
        var pressed = hover && Held && node.Kind != MasteryKind.Start;
        var selLift = Lift(SelKey(node.Id), _pinnedNodeId == node.Id && node.Kind != MasteryKind.Start);
        var cy = pressed ? cyHit + PressDepth : cyHit;
        var box = new Rectangle(cx - rad, cy - rad, rad * 2, rad * 2);

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
        //
        // UNDER REDUCED MOTION IT DOES NOT BREATHE — it HOLDS, at the top of its own breath (brief §32:
        // drop idle motion, keep the static highlight). _breathPhase is sampled once per Update, so this
        // is the whole of that decision and Draw only reads a number.
        if (node.Kind == MasteryKind.Specialisation && aff is null && !taken)
            for (var ring = 3; ring >= 1; ring--)
            {
                var g = (int)(box.Width * 0.09f * ring);
                _ui.Diamond(b, new Rectangle(box.X - g, box.Y - g, box.Width + g * 2, box.Height + g * 2),
                            Gold * (0.14f * _breathPhase));
            }

        // THE TAKE — the node blooms ONCE. Fired from Update at the take (see TakeNode), keyed to this
        // node, read here and never re-armed by a draw. A pure fade at a FIXED radius: a bloom that
        // expanded would be movement, and movement is the thing Reduced Motion drops — this one plays
        // the same on both settings, which is what the brief's "keep simple fades" allows.
        var bloom = DevPose is { } bp && node.Id == DevTakeId ? 1f - bp : UiMotion.Pulse(NodeKey(node.Id));
        if (bloom > 0f)
        {
            DrawRing(b, new Vector2(cx, cy), rad * 1.22f, Math.Max(2f, rad * 0.14f), Gold * (0.90f * bloom));
            DrawRing(b, new Vector2(cx, cy), rad * 1.50f, Math.Max(1.5f, rad * 0.09f), Gold * (0.45f * bloom));
        }

        // SELECTED — the node the inspector is reading, ringed in gold outside its own frame. Eased,
        // so moving the pin from one node to the next is a hand-off rather than a jump cut; instant
        // under Reduced Motion, with the same ring at the end of it.
        if (selLift > 0f)
            DrawRing(b, new Vector2(cx, cy), rad + Math.Max(3f, rad * 0.34f), Math.Max(1.5f, rad * 0.10f),
                     Gold * (0.85f * selLift));

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

            // HOVER ARRIVES, IT NO LONGER SNAPS. It was `hover ? Bone : <state>` — the same bone white
            // this still lands on, but reached in one frame, so crossing a node was a flicker rather
            // than a response. It eases there over Fast now (§26: luminance, restrained, 80–120 ms),
            // and PRESSED banks the light back down (§27) — three degrees of the same node, and the
            // resting colour and the fully-hovered colour are both exactly what shipped (LAW 1).
            var frameCol = owned || taken ? Gold : canTake ? branchCol
                           : lockedSpec ? Muted(branchCol, 0.72f) : Path;
            b.Draw(frame, box, StateTint(frameCol, hoverLift, pressed));
        }
        else
        {
            // No art on disk: the flat shapes this screen shipped with. The game must run with an empty
            // assets/art, so every draw site keeps its greybox rather than borrowing someone else's art.
            _ui.Fill(b, box, node.Kind == MasteryKind.Start ? PanelBg : fill);
            Outline(b, box, node.Kind == MasteryKind.Start ? Slate : StateTint(edge, hoverLift, pressed),
                    owned ? thick * 2 : thick);
        }

        if (node.Kind == MasteryKind.Start)
        {
            // >= the default zoom, not >. The threshold was 0.3 and HOME resets to exactly 0.30, so the
            // one label naming the centre of the tree was absent from the default view of it.
            if (_zoom > LabelZoom) _ui.TextCenterBig(b, "YOU", cx, cy - UiTypography.Body / 2 - 1, Bone, UiTypography.Body);
        }
        else if (node.Kind == MasteryKind.Specialisation && _zoom > LabelZoom)
        {
            // THE SECOND-LARGEST NODE, AND THE ONLY OTHER ONE THAT SPEAKS. Its Form goes inside the
            // diamond because the Form IS the choice; the word underneath names the kind, so a player
            // can count the six of them without hovering one.
            // Bright while the discipline is still open, quiet once it is spent. The five a hunter did
            // NOT take are dead content — a bone-white Form name over an unlit frame read as a bug in
            // the capture, a caption floating in empty space.
            _ui.TextCenterBig(b, node.Style is { } sf ? Short(sf) : "STYLE", cx, cy - UiTypography.Secondary / 2,
                              taken ? new Color(0x14, 0x11, 0x1E) : aff is null ? Bone : Muted(Bone, 0.62f),
                              UiTypography.Secondary);
            // INWARD, toward the centre: on the south arms the road node stands just outside this diamond and
            // used to print through the caption ("S ECIALISATION").
            var specCapY = w.Y > 1f ? box.Top - UiMetrics.Space(6) - UiTypography.Secondary : box.Bottom + UiMetrics.Space(5);
            _ui.TextCenterBig(b, "SPECIALISATION", cx, specCapY,
                              taken ? Gold : aff is null ? Muted(Gold, 0.90f) : Muted(Slate, 0.72f),
                              UiTypography.Secondary);
        }
        // The branch glyph, over the frame's hollow centre. Below about twenty pixels it is a smudge
        // that only muddies the socket, and the frame alone still carries the kind — so it drops out
        // rather than degrading, the same way the labels do.
        // A ROAD NODE WEARS THE SKILL IT TEACHES (UX V2 P1.5) — the twelve most consequential nodes on the
        // tree used to carry the same branch glyph as a one-point minor. A small gold mark says the skill
        // is OPEN RIGHT NOW: it reads AvailableSkills, so it goes out the moment the node is given back.
        else if (node.Kind == MasteryKind.SkillRoad && node.GrantsSkillId is { } roadSkill && box.Width >= 20
                 && _ui.Assets.Get($"icon_skill_{roadSkill}") is { } skillGlyph)
        {
            var g = (int)(box.Width * 0.50f);
            var learnedRoad = Mastery.AvailableSkills().Contains(roadSkill);
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
            // Stacked by their own rungs, so at 150 % the name and the kind still clear each other and
            // the medallion: name one breath off the box, CAPSTONE one hair off the name. The KIND line
            // waits for the zoom its neighbours allow it (KindLineZoom) — the name never does.
            var kind = _zoom >= Overview.KindZoom;
            if (w.Y < -1f)
            {
                var nameY = box.Top - UiMetrics.Space(2) - UiTypography.Body;
                _ui.TextCenterBig(b, Head(node.Label), cx, nameY, nameCol, UiTypography.Body);
                if (kind) _ui.TextCenterBig(b, "CAPSTONE", cx, nameY - 1 - UiTypography.Secondary, kindCol, UiTypography.Secondary);
            }
            else
            {
                var nameY = box.Bottom + UiMetrics.Space(4);
                _ui.TextCenterBig(b, Head(node.Label), cx, nameY, nameCol, UiTypography.Body);
                if (kind) _ui.TextCenterBig(b, "CAPSTONE", cx, nameY + UiTypography.Body, kindCol, UiTypography.Secondary);
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
            var ny = w.Y < -1f ? box.Top - UiMetrics.Space(6) - npx : box.Bottom + UiMetrics.Space(4);
            _ui.TextCenterBig(b, nm, cx, ny, taken || canTake ? Bone : Slate, npx);
        }
        else if (node.Kind == MasteryKind.Minor && _zoom >= FirstOpenZoom * 0.8f && node.Stats is { Count: > 0 } st0)
        {
            var kv0 = st0.First();
            _ui.TextCenterBig(b, $"+{kv0.Value:0}", cx, box.Bottom + UiMetrics.Space(2), taken ? Bone : Slate, UiTypography.Secondary);
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
