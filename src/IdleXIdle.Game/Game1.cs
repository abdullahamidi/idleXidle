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
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Presentation;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Traits;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Quests;
using IdleXIdle.Core.Warrens;

namespace IdleXIdle.Game;

/// <summary>
/// The shell: it owns the screens, the save, and the idle tick — and routes between them.
/// </summary>
/// <remarks>
/// <para>
/// The game is a <b>single-champion idle auto-battler</b>: you build ONE character and it fights on its
/// own, wave after wave, on every screen and even while the game is closed. There is no squad and no
/// bank-or-push decision any more — every cleared wave pays out at once, and the depth the build can hold
/// is the only thing that gates the haul. <see cref="HuntScreen"/> is the main screen; the
/// build/mastery tree, Warren, Forge, world map and Memory Dust hang off it. This class renders and
/// routes — all rules live in IdleXIdle.Core (ADR-001), and it decides nothing.
/// </para>
/// <para>
/// This doc used to describe "a playable greybox of the hunt loop" whose job was to answer <i>"is the
/// precision-hunt loop actually fun?"</i> — a manual-combat game that playtested as "3 buttons, win,
/// press R" and was replaced. Its ~1100 lines of screens lived on here for several commits after
/// nothing could reach them, because nothing pointed out that they were unreachable.
/// </para>
/// </remarks>
public partial class Game1 : Microsoft.Xna.Framework.Game
{
    private const int CanvasWidth = 480;
    private const int CanvasHeight = 270;

    /// <summary>
    /// Pixel density. The LAYOUT stays 480x270; only the density changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every screen positions itself in a 480x270 logical grid, and that grid is not moving —
    /// rewriting it would mean re-laying-out the whole game. Instead the canvas is rendered at
    /// 480x270 <b>times this</b>, and every draw is scaled by it. Existing code keeps its coordinates
    /// and knows nothing about it; a 128x184 asset drawn into a 32x46 logical rect lands at exactly
    /// 1:1 native density.
    /// </para>
    /// <para>
    /// 4 because hand-drawn art cannot survive at 480x270 — a 2px ink contour has nowhere to go on a
    /// 32px creature, and point-sampled linework at that size just IS pixel art. 4x makes the canvas
    /// 1920x1080, which is where most players are. Slay the Spire 2 authors its card portraits at
    /// exactly 4x what it displays, for the same reason.
    /// </para>
    /// </remarks>
    private const int ArtScale = 4;

    /// <summary>Where the canvas lands on screen (scaled + centred; the rest is letterbox).</summary>
    private Rectangle _present = Display.PresentFit(1280, 720);

    private DisplayMode _displayMode = DisplayMode.Windowed;

    /// <summary>
    /// The window size the player chose. Only WINDOWED reads it; the other two modes take the desktop.
    /// </summary>
    /// <remarks>
    /// A REQUEST, not a fact: <see cref="Display.NearestOffered"/> clamps it to something this desktop
    /// can actually hold every time the mode is applied, so a prefs file carried between machines never
    /// opens a window bigger than the screen it is on.
    /// </remarks>
    private WindowSize _windowSize = new(1280, 720);
    // Sound + dialog prefs ride the same prefs file as the display mode (Display.GamePrefs).
    private int _sfxVolume = 80;     // 0..100 percent (the settings sliders)
    private int _musicVolume = 50;   // 0..100 percent
    private bool _askBeforeScrap = true;
    // The fight's text and effects — quality-of-life switches (playtest 2026-08-23), same prefs file.
    private bool _showDamageNumbers = true, _showSkillCallouts = true, _showHitEffects = true, _showScreenFlash = true;

    /// <summary>
    /// REDUCED MOTION: the accessibility switch that stops the interface from moving on its own.
    /// </summary>
    /// <remarks>
    /// Static so the two places that actually animate — <see cref="UiKit.AnimSprite"/>, which loops
    /// every idle sprite in the game, and the vault card's hover grow — can read it without every
    /// screen having to be handed a flag. It is shipped WITH those consumers on purpose: a setting
    /// that changes nothing is worse than no setting, because it teaches the player that the options
    /// screen lies.
    /// </remarks>
    internal static bool ReducedMotion { get; private set; }

    // art-bible §4.1. Hearth Gold marks EARNED states only — never decoration.
    private static readonly Color VoidInk = UiInk.Void;

    /// <summary>The letterbox bars — the backbuffer outside the canvas.</summary>
    /// <remarks>
    /// One step darker than <see cref="VoidInk"/> (art-bible §4.1's #1B1620) on purpose. Void Ink is a
    /// SURFACE colour: it is what the canvas itself clears to, so bars painted in it read as more game
    /// — a panel the content failed to fill. The bar is not game at all, and the darkest tone in the
    /// palette is the one that says so and stops the eye at the canvas edge.
    /// </remarks>
    private static readonly Color LetterboxInk = new(0x16, 0x11, 0x10);
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;

    /// <summary>A soft, dark panel tone for HUD bands — lighter than the void so UI sits on a surface,
    /// not floating text on black. This is most of what "softer" means at this resolution.</summary>
    private static readonly Color PanelBg = UiInk.Raised;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _batch = null!;
    private Texture2D _pixel = null!;
    private PixelFont _font = null!;
    private RenderTarget2D _canvas = null!;

    private KeyboardState _keys, _prevKeys;

    /// <summary>
    /// The keyboard as the SCREENS see it: empty while a tour is up. The tour swallowed clicks from the
    /// first day, but the screens read raw <see cref="_keys"/>, so "any key advances the card" also
    /// bought a trait (Enter on TRAITS), sold the bench item (S on the FORGE) or walked into a region
    /// (Enter on the MAP) underneath the scrim (review 2026-08-26). Empty under the two host panels and
    /// the open Expedition Log too, which hold the host's own hotkeys the same way (see panelHolds).
    /// </summary>
    private KeyboardState ScreenKeys => _tourActive || _opening.OwnsInput || WelcomeUp || HostModalUp || _expedition.LogOpen ? default : _keys;
    private MouseState _mouse, _prevMouse;
    private bool _clicked; // the left-click EDGE for this frame, latched in Update so Draw can read it
    private bool _rightClicked; // the right-click EDGE, latched the same way — the item context menu
    private int _wheel;    // mouse-wheel notches this frame, latched alongside the click

    // ── Game state ────────────────────────────────────────────────────────────────────────────
    // The core-state fields (_hunter, _world, _loadout, _mastery, _dust, _characters, _warren) are
    // deliberately NOT readonly: START A NEW GAME replaces them all with fresh instances and rebuilds
    // the screens around them — see StartNewGame.
    private Hunter _hunter = new();
    private readonly Random _rng = new();

    // The world of regions to conquer. The "active" region is what you hunt in and farm right now.
    private World _world = new();
    private string _activeRegion = VerdantHollow.RegionId;
    private Region _region = null!;   // == _world.RegionFarm(_activeRegion); reassigned on region change
    private bool _showWorld;
    private MapScreen _mapScreen = null!;   // MAP nav: the region-selection dashboard (spec rev 1)
    private string _conquerMsg = "";

    // ── Gradual unlocking ───────────────────────────────────────────────────────────────────────
    // "Oyundaki etkinlikler yavaş yavaş açılmalı. Hepsi detaylı şekilde anlatılmalı." The gates live in
    // Core/Progression/Unlocks; these three fields are the whole of the presentation.

    /// <summary>The first-run guide's current rung, recomputed every frame. Null once outgrown.</summary>
    // ── ONBOARDING. Core holds the catalogue of what is TRUE; the director holds what is SAID. ───
    private readonly OnboardingDirector _coach = new();

    /// <summary>
    /// THE AUTHORED OPENING. It owns the player until it is finished; the coach is silent for all of it.
    /// </summary>
    /// <remarks>
    /// Two layers, deliberately. This one is an ordered script that may stop the fight, take every
    /// control but one and open a screen the rail has not unlocked yet — the first five minutes are
    /// authored and the order IS the teaching. <see cref="_coach"/> is the long tail: contextual,
    /// fact-driven, never modal, and it does not speak while this is running.
    /// </remarks>
    private readonly OpeningDirector _opening = new();

    /// <summary>SKIP GUIDANCE — presentation only. It fabricates no fact and moves no gate.</summary>
    private bool _guidanceOff;

    /// <summary>The three fall-loop facts the catalogue cannot derive, and the save therefore keeps.</summary>
    private bool _reportOpenedEver;
    private bool _changedAfterFall;
    private bool _retriedAfterChange;

    /// <summary>
    /// DISPATCHES — the account's inbox: the news it has been told, and which of it has been read.
    /// </summary>
    /// <remarks>
    /// A plain model with no device, restored in Initialize like the trait ledger. The inbox IS the
    /// dedupe: a producer posts by semantic key and needs no first-frame baseline of its own. Copy is
    /// never stored — <see cref="DispatchCopy"/> renders it from the catalogues at display time.
    /// </remarks>
    private readonly Inbox _inbox = new();

    /// <summary>
    /// Has DISPATCHES ever been opened? The completion latch of the inbox's lesson — a sibling of
    /// <see cref="_reportOpenedEver"/>, kept by the save because opening the inbox changes nothing
    /// in the world and so cannot be reconstructed.
    /// </summary>
    private bool _dispatchesOpenedEver;

    /// <summary>
    /// A dispatch arrived since the chrome last looked — the edge the envelope's pulse is made of.
    /// Set by <see cref="PostDispatch"/>, taken by <see cref="TakeDispatchArrival"/>. Session-only.
    /// </summary>
    private bool _dispatchArrivalOwed;

    /// <summary>
    /// The loaded file, parked between <see cref="LoadOrStartFresh"/> (Initialize) and
    /// <see cref="SeedExplained"/> for the inbox seed, whose facts include the rail's revealed set —
    /// real only once the Forge exists. Null once used, and on every path that read no file.
    /// </summary>
    private SaveGame? _pendingInboxSeed;

    /// <summary>Did this session's load seed the inbox — was it a file from before the inbox? Telemetry only.</summary>
    private bool _inboxSeeded;

    /// <summary>
    /// The Version of the file this session LOADED. Migration input, and nothing else.
    /// </summary>
    /// <remarks>
    /// It defaults to the current version, which is the right answer for every path that never reads a
    /// file — the capture rig, a missing save, START A NEW GAME — because none of those is a save that
    /// predates anything. Only <see cref="SeedExplained"/> reads it; see
    /// <see cref="OnboardingLessons.SeedFallLoopAsLived"/> for why a version and not a guess.
    /// </remarks>
    private int _saveVersionSeen = SaveGame.CurrentVersion;

    /// <summary>The opening cursor as the file wrote it, parked until the screens exist to check it.</summary>
    /// <remarks>
    /// Parked for the same reason the explained list is: resolving it asks what the player has already
    /// DONE — chests opened, items worn — and those live on screens Initialize has not built yet.
    /// Spent in <see cref="SeedOpening"/>, from ApplyRestoredState.
    /// </remarks>
    private int _pendingOpeningStage;

    /// <summary>
    /// What the build looked like at the moment of the first fall, so a real CHANGE can be told from
    /// a player poking around.
    /// </summary>
    /// <remarks>
    /// MAKE ONE CHANGE completes on a state delta, not on a screen being opened: ranks trained, worn
    /// pieces, mastery spent, or the woven build's revision. Any of the four moving is a change, which
    /// is what lets the lesson say "make ONE change" without naming which subsystem — the player picks
    /// the lever, exactly as the report refuses to prescribe one.
    /// </remarks>
    private (int Ranks, int Worn, int Mastery, int BuildRev)? _fallSnapshot;

    /// <summary>Falls this account has taken — the run log is the record, and it is monotone.</summary>
    private int _fallsSeen;

    /// <summary>Toast for clicking a rail tile that is not open yet — it names its own price.</summary>
    private string _lockedMsg = "";
    private float _lockedTimer;

    // ── The tours, and the on-demand explanations that replaced the modal panels ─────────────
    //
    // THERE USED TO BE A QUEUE OF MODAL PANELS HERE. Every screen that opened, every skill slot, every
    // finished quest and every champion that joined was pushed onto it, and the panels dripped out one
    // every eight seconds, each one swallowing input until clicked. Playtest: "while a player is focused
    // on solving something, the screen keeps throwing 'this opened, that arrived' notifications, and it
    // wore the testers out." The queue, the panel, its cooldown and everything that fed them are gone —
    // not disabled, gone — and replaced by three quiet channels:
    //
    //   * THE TOURS (Onboarding.TourFor): a click-through spotlight walk of a screen, one region lit
    //     at a time, while the screen keeps working underneath. NONE OF THEM STARTS ITSELF: the
    //     mandatory eight-card HUNT intro and every screen's first-open tour were both cut, and the
    //     one game path in is LEARN THIS SCREEN — the ? beside the settings gear — which the player
    //     may press as often as they like (TakeLearnClick). A gold NEW mark on a rail tile means
    //     "you have not looked at this screen", not "a tour is waiting there". (The other screens
    //     used to get a dense banner instead — playtest: "too crowded and too small. The Hunt
    //     screen's walkthrough was much clearer.")
    //   * THE SLOT NOTE: the one banner left — a new skill slot's line at the top of the BUILD
    //     screen, after its tour, closed with one click.
    //   * NOTICE TOASTS: a quest finishing or a champion joining is a line at the top that fades on its
    //     own. It never takes input.

    /// <summary>Is a tour on screen? While true, input belongs to it and nothing else teaches.</summary>
    /// <summary>The unread dot's slow breath, in seconds of a two-second cycle.</summary>
    private float _navDotClock;

    private bool _tourActive;

    /// <summary>
    /// Where the tour's card was drawn — the ONE rectangle a click may land on while a tour is up.
    /// </summary>
    /// <remarks>
    /// Latched by <see cref="DrawTour"/> and read by the input pass on the next frame, the way every
    /// screen in this game hit-tests what it drew. It is empty until the first card has been drawn,
    /// which is correct: there is nothing to click before there is a card.
    /// </remarks>
    private Rectangle _tourCard;

    /// <summary>The screen the running tour is about. The Hunt's tour is the intro.</summary>
    private Activity _tourScreen;

    /// <summary>The running tour's cards — <see cref="Onboarding.TourFor"/> of <see cref="_tourScreen"/>.</summary>
    private IReadOnlyList<TourStep> _tour = Array.Empty<TourStep>();

    /// <summary>Which card of <see cref="_tour"/> is showing.</summary>
    private int _tourStep;

    /// <summary>
    /// Has this session decided whether to run the intro? Asked once, the frame the title closes.
    /// </summary>
    /// <remarks>
    /// It used to read "has a wave been cleared", asked once because the first wave clears roughly
    /// fifteen seconds in and asking every frame would have ended the intro under the player
    /// mid-card. THE GAME DECIDES NOTHING NOW — the mandatory intro is gone and every tour is asked
    /// for by hand through LEARN THIS SCREEN — so the single frame this still guards is the capture
    /// rig's RH_SHOT_MODE=intro pose.
    /// </remarks>
    private bool _introDecided;

    /// <summary>The save's record of the intro having been finished or skipped.</summary>
    private bool _introSeen;

    /// <summary>The save's explained list, parked from load until the screens exist to seed it against.</summary>
    private List<string>? _pendingExplained;

    /// <summary>
    /// Screens (Activity names) whose tour the player has finished or skipped, and skill-slot notes
    /// (SkillSlotN) whose banner the player has closed.
    /// </summary>
    /// <remarks>
    /// Persisted. Whether a tile is NEW is derived from this and the unlock gates every frame
    /// (<see cref="Onboarding.IsNew"/>), never set at the moment of opening — so nothing can forget to
    /// mark it, and a screen that opened while the game was closed is marked too.
    /// </remarks>
    private readonly HashSet<string> _explained = new();

    /// <summary>Screens opened this session. A visited tile drops its NEW mark even while its tour is still running.</summary>
    private readonly HashSet<Activity> _visited = new();

    /// <summary>
    /// The rail's REVEALED set — the screens the player has been shown, ever (the journey, 2026-09-06).
    /// Persisted as names; seeded in <see cref="SeedExplained"/> from the save and the gates
    /// (<see cref="Reveal.Restore"/>), grown once per frame by <see cref="Reveal.Newly"/>, which is
    /// where the notice fires. THE HUNT is always in it. A revealed tile whose gate has since closed
    /// (GEAR after the bag is salvaged empty) stays on the rail, dimmed, with its price — the
    /// restrained locked state; a tile that was never revealed is not drawn at all.
    /// </summary>
    private HashSet<Activity> _revealed = new() { Activity.Hunt };

    /// <summary>The save's revealed names, parked until the bag is real (see <see cref="_pendingExplained"/>).</summary>
    private List<string>? _pendingRevealed;

    /// <summary>Set once the revealed set has been seeded, so no frame announces a screen the save already had.</summary>
    private bool _revealSeeded;

    /// <summary>A champion joined and the roster has not been looked at since. Session-only.</summary>
    private bool _rosterNews;

    /// <summary>Who joined most recently — the name the ROSTER hint says while <see cref="_rosterNews"/> holds.</summary>
    private string _rosterNewName = "";

    /// <summary>
    /// One queued toast. Two lines for the ordinary kind; three, styled differently, for an awakening.
    /// </summary>
    /// <remarks>
    /// It was a string with a newline in it, which was enough while every notice had exactly a title
    /// and a body. A TRAIT AWAKENING is three rungs and the middle one is the loud one — the trait's
    /// NAME, not the kicker above it — so the payload says which kind it is rather than the drawing
    /// code guessing from a line count.
    /// </remarks>
    private readonly record struct Notice(string Head, string Detail, string? Third = null,
                                          bool Awakening = false);

    /// <summary>Notices waiting their turn — "QUEST COMPLETE", "X JOINS YOU" — shown one at a time.</summary>
    private readonly Queue<Notice> _noticeQueue = new();
    private Notice _notice;

    // ── SCREEN AND MODAL MOTION (brief sec. 33, 34) ─────────────────────────────────────────────
    //
    // A screen used to APPEAR: one frame the page was BUILD, the next it was GEAR, with nothing in
    // between to say a change had happened. The brief asks for a quick content crossfade and a short
    // settle, 100-150 ms, with the nav rail staying put — the rail is the one thing that did not
    // change, and moving it would say it had.
    //
    // The fade is drawn as a scrim over the page (never over the rail) that lifts from the background
    // colour, so it costs no render target and reads as the content arriving rather than as a flash.
    // The settle rides the kick vector the overlay batch already takes for the traits camera.

    /// <summary>Which screen owned the last frame — a change starts the switch motion.</summary>
    private string _screenKey = "";

    /// <summary>Seconds left of the screen-switch fade; zero when nothing is switching.</summary>
    private float _screenFade;

    /// <summary>Seconds left of a modal's fade-in.</summary>
    private float _modalFade;

    /// <summary>Whether a modal owned the last frame — the edge starts the modal fade.</summary>
    private bool _modalWasUp;

    /// <summary>How long a screen switch takes. 130 ms: inside the brief's 100-150 ms band for a screen.</summary>
    private const float ScreenFadeSeconds = 0.13f;

    /// <summary>How far the page rises into place, in canvas pixels. Dropped entirely under Reduced Motion.</summary>
    private const float ScreenSettlePx = 8f;

    /// <summary>The switch scrim's colour — the darkest ground in the palette, so the page clears out of the dark it sits on.</summary>
    private static readonly Color ScreenFadeInk = new(0x08, 0x06, 0x0E);

    /// <summary>How dark the scrim ever gets. A full blackout between screens is a cut, not a transition.</summary>
    private const float ScreenFadeMax = 0.55f;

    /// <summary>The screen-switch progress, 1 at the instant of the switch falling to 0 — eased, so it lands softly.</summary>
    private float ScreenFadeT => _screenFade <= 0f ? 0f : UiMotion.Smooth(_screenFade / ScreenFadeSeconds);

    /// <summary>The page's settle offset for this frame: a short rise, and nothing at all under Reduced Motion.</summary>
    private Vector2 ScreenSettle => UiMotion.Reduced || _screenFade <= 0f
        ? Vector2.Zero
        : new Vector2(0f, ScreenSettlePx * ScreenFadeT);

    /// <summary>The key that names the screen on the page right now — any change is a switch.</summary>
    private string ScreenKeyNow =>
        _showForge ? "forge" : _showWorld ? "world" : _showTraits ? "traits" : _showRoster ? "roster"
        : _showVault ? "vault" : _showLoadout ? "loadout" : _showWarren ? "warren"
        : _showMastery ? "mastery" : _showGear ? "gear" : _showTraining ? "training" : "hunt";

    /// <summary>
    /// True while one of the HOST's own panels is up: SETTINGS, HELP or DISPATCHES.
    /// </summary>
    /// <remarks>
    /// ONE PREDICATE, THREE PANELS. Fourteen sites need this answer — the gear's click, the chrome's
    /// closes, the modal block, the rail's paint and its click, the two cursor gates, the screens'
    /// keyboard, the attention owner's Modal tier — and a disjunction spelt out at each of them is
    /// fourteen places a fourth panel can be forgotten, which is exactly how a gear ends up painting
    /// live over a modal that will refuse its click. They all ask this instead.
    /// </remarks>
    private bool HostModalUp => _showSettings || _showHelp || _showDispatches;

    /// <summary>True while one of the host's own modals is up — the ones that fade in.</summary>
    private bool ModalUpNow => HostModalUp;

    // ── THE CURRENCY PILLS REACT (brief sec. 36-38) ─────────────────────────────────────────────
    //
    // Spending was silent: the number was one figure on one frame and a smaller figure on the next,
    // in the corner of the screen furthest from the button that spent it. sec. 37 asks the SPEND to
    // react at the pill rather than fly resources across the page, and sec. 36 asks the number to
    // move to its new value rather than jump. sec. 38 allows a "+N" for a substantial GAIN — with a
    // threshold, because Gleam arrives a few at a time all through a hunt and a badge on every kill
    // is not feedback, it is weather.

    /// <summary>Each purse slot's true value as of the last Update — the tick's target.</summary>
    /// <remarks>Sized by <see cref="PurseCount"/> and indexed by the frozen Purse* constants, so a
    /// slot's motion state means the same thing whether or not its capsule is on the row today.</remarks>
    private readonly long[] _pillTrue = new long[PurseCount];

    /// <summary>What each pill is currently PRINTING; eases to <see cref="_pillTrue"/>.</summary>
    private readonly double[] _pillShown = new double[PurseCount];

    /// <summary>Seconds left of a pill's reaction flash.</summary>
    private readonly float[] _pillFlash = new float[PurseCount];

    /// <summary>Gains banked but not yet worth announcing, per pill.</summary>
    private readonly long[] _pillGainAcc = new long[PurseCount];

    /// <summary>The gain each pill is announcing, and the seconds left of that announcement.</summary>
    private readonly long[] _pillGainShow = new long[PurseCount];
    private readonly float[] _pillGainT = new float[PurseCount];

    /// <summary>
    /// Seconds the pills have been watching. Until <see cref="PillWarmSeconds"/> they only TRACK: they
    /// follow the true values without ticking, flashing or banking anything.
    /// </summary>
    /// <remarks>
    /// A world arrives in pieces. A save's offline credit is applied in Initialize, before the first
    /// Update, but the capture rig installs a whole fixture — gear, chests, a five-figure Gleam balance —
    /// over the first frames of Update, and a hunt's own first payout can land in the same breath. With
    /// no warm-up the pills read that installation as EARNINGS and the GEAR capture announced "+4560"
    /// for a balance the player had simply loaded (build/shots/r2_character_150.png). Half a second is
    /// longer than any of that and far shorter than any moment a player would call a gain.
    /// </remarks>
    private float _pillWarm;

    /// <summary>How long the pills track in silence before they start reporting.</summary>
    private const float PillWarmSeconds = 0.5f;

    /// <summary>How long a "+N" stays up.</summary>
    private const float PillGainSeconds = 1.4f;

    /// <summary>The smallest gain each pill will ever announce, before the proportional rule.</summary>
    /// <remarks>
    /// <para>
    /// ONE ENTRY PER PILL, and the length is asserted against <see cref="PurseCount"/> below rather than
    /// trusted. <b>It was three entries long against a six-pill row</b>, and it crashed the game:
    /// <c>TickChromeMotion</c> indexes this by the pill, so the first time an account's ESSENCE, CORE or
    /// CRYSTAL rose while the row was warm, <c>PillGainFloor[3]</c> threw
    /// <c>IndexOutOfRangeException</c> out of Update and the process died.
    /// </para>
    /// <para>
    /// DORMANT SINCE THE ROW GREW TO SIX. Gleam, Dust and Scrap are paid from wave one, so indices 0-2
    /// were exercised constantly and the other three only by a salvage — which the boot soak never
    /// reached while a fresh champion died on wave 5, and which no capture can reach because a fixture
    /// installs its materials during the warm-up. Making the first boss winnable is what finally walked
    /// a soak far enough to gain one, and the boot check failed on the next run.
    /// </para>
    /// <para>
    /// The three new floors are 1: Essence, Core and Crystal drop in ones and twos from a salvage, so
    /// every gain is worth the glance — a floor of 25 on Crystal would mean the pill never once said
    /// anything. The proportional rule (a twentieth of what is held) still takes over as an account
    /// accumulates them.
    /// </para>
    /// </remarks>
    private static readonly long[] PillGainFloor =
        { 25, 25, 100, 1, 1, 1 };   // scrap, dust, gleam, essence, core, crystal

    private float _noticeTimer;

    /// <summary>How long a notice toast stays. Long enough to read twice; it fades over the last second.</summary>
    private const float NoticeSeconds = 6f;

    /// <summary>
    /// False until the first roster Refresh has been absorbed silently.
    /// </summary>
    /// <remarks>
    /// The unlocked set is derived from conquest and held in memory only — the save carries just the
    /// active champion's id — so the first Refresh after every load reports EVERY champion the player
    /// already owns as newly gained. Without this the game would open with a stack of "X JOINS YOU"
    /// toasts for champions earned hours ago.
    /// </remarks>
    private bool _rosterBaselined;

    private int _regionProgression;

    private bool _showHelp;
    private bool _showSettings;

    /// <summary>The DISPATCHES reading surface — the third host panel, opened by the envelope or M.</summary>
    private bool _showDispatches;

    /// <summary>
    /// The KEY of the letter the reading pane has OPEN, or null when nothing is open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Session-only, and deliberately: what the pane is showing is presentation, and what has been READ
    /// is the fact — that one lives in the inbox and in the save.
    /// </para>
    /// <para>
    /// IT STARTS AT NOTHING, and that is the whole reason it exists apart from the cursor. Showing the
    /// newest letter the instant the panel opens would mark it read, and on the common case — one
    /// letter waiting — merely glancing at the surface would clear the mark without anybody having
    /// read a word. A letter is opened by a click or by ENTER, and opening it is what reads it.
    /// </para>
    /// <para>
    /// A KEY AND NOT A ROW NUMBER. The fight ticks above the modal block, so a conquest, an awakening
    /// or a champion joining can land while the panel is open — and a posted letter goes to the FRONT
    /// of the newest-first list, shifting every row under the player. Held as an index, the pane would
    /// silently swap to the letter that just arrived and read it on the next frame, destroying the one
    /// unread mark this whole surface exists to protect. A key names one letter for as long as the
    /// inbox holds it.
    /// </para>
    /// </remarks>
    private string? _dispatchOpenKey;

    /// <summary>The highlighted letter's KEY — the keyboard's cursor, anchored for the same reason.</summary>
    private string? _dispatchCursorKey;

    /// <summary>The first letter visible in the list, when there are more than the column holds.</summary>
    private int _dispatchScroll;

    /// <summary>
    /// A dispatch has arrived and its one pulse has not been spent yet.
    /// </summary>
    /// <remarks>
    /// <see cref="TakeDispatchArrival"/> gives the edge once; this HOLDS it while somebody else has the
    /// player's eyes, so news that lands under a chest reveal or a fall is announced when the frame is
    /// free rather than swallowed. A pulse cannot be paused (UiMotion decrements every one), so what
    /// waits is the Flash itself.
    /// </remarks>
    private bool _dispatchArrivalHeld;

    // ── The idle half. Automation is EARNED here, never assumed. ──────────────────────────────
    private ForgeScreen _forge = null!;
    private HuntScreen _expedition = null!;   // solo build model — one champion, not a squad
    private bool _showForge;

    // The player's build (four woven skills + three keystones), the mastery tree it walks, and the editor.
    private PlayerLoadout _loadout = PlayerLoadout.Starter();   // the roster's starter champion; a load or a new game replaces it
    private MasteryTree _mastery = new();
    private int _deepestEver;   // the deepest wave ever reached — drives mastery points, so it must persist

    // The champion's recent gleam/second, measured live so it keeps earning OFFLINE at the rate it was
    // actually managing — not a guessed number. Saved, and applied on the next load as time-away gleam.
    private double _champGleamAccrued, _champSecondsAccrued;
    private float _champGleamRate;
    // The VAULT's keep-filter — playthrough state, saved with the run (see VaultScreen.KeepMinTier).
    private int _chestKeepMinTier;

    /// <summary>Where the absence left the champion standing — the wave the FIRST live descent opens at.</summary>
    /// <remarks>
    /// Not persisted: it describes one launch. Cleared the moment a second descent starts, and only
    /// honoured in the region the absence was simulated in.
    /// </remarks>
    private int _offlineResumeWave;
    private string _offlineResumeRegion = "";
    private int _offlineResumeRuns;

    /// <summary>
    /// Has the player ALLOWED the Warren's runners to sell low-grade gear out of an opened chest?
    /// </summary>
    /// <remarks>
    /// The capability is unlocked by a Warren facility level; this is the consent. Off by default and
    /// off for every existing save, because the rider used to arrive unannounced with an upgrade
    /// bought for its output (playtest 2026-09-09: "it sold an item obtained from a chest even though
    /// the item filter wasn't active").
    /// </remarks>
    private bool _autoSellOn;

    // ── THE WANDERING TRADER. Week + purchases persist; the stock is re-minted on demand (identity
    //    is week-seeded, level follows the deepest wave, so a cache key of (week, level) suffices). ──
    private int _traderWeek;
    private readonly HashSet<int> _traderBought = new();
    private List<ItemInstance>? _traderStock;
    private int _traderStockLevel;
    /// <summary>The class the stall was last stocked for — a champion switch restocks it for the new one.</summary>
    private ItemClass? _traderStockClass;

    /// <summary>
    /// Who was active last frame, so a champion SWITCH is seen the frame it happens. Null until the
    /// first sync, so a loaded save is never treated as a switch away from nobody.
    /// </summary>
    private string? _lastActiveCharacterId;
    /// <summary>The keep-filter's wanted slots — several at once since 2026-08-23 (empty = any).</summary>
    private readonly HashSet<ItemBaseType> _chestKeepSlots = new();
    private float? _pendingRevealPose;   // RH_SHOT_T for the chest reveal, applied once the fixture has opened one
    private bool _rigNoticePosted;       // RH_SHOT_NOTICE: the sample notice is posted once, never every frame
    private MasteryScreen _masteryScreen = null!;
    private bool _showMastery;

    // The character sheet — equipment + stat training + a live damage bench, apart from the fight it feeds.
    private GearScreen _gear = null!;

    /// <summary>
    /// The sets whose one five-piece announcement the save says has already been made, held between
    /// <see cref="LoadOrStartFresh"/> (Initialize) and the GearScreen's construction (LoadContent).
    /// </summary>
    private List<string> _completedSetsLoaded = new();
    private bool _showGear;
    private TrainingScreen _training = null!;
    private bool _showTraining;

    private bool _showWarren;

    // The Warren is a facility-production dashboard (spec rev 1). The old creature den
    // (AutomationScreen, its roster, and the CORES currency) was fully retired 2026-08-24 — it was
    // built but unreachable, so no player ever had a creature or spent a core.
    private WarrenScreen _warrenScreen = null!;
    private Warren _warren = new();
    // (The INSIGHT pool — produced by two facilities, spendable only on facility upgrades — was cut
    // 2026-08-31 (P12): a closed loop that never decided anything the Gleam cost had not. The Warren
    // pays the Forge's Scrap/Essence instead; an old save's WarrenMasteryPool key is skipped on load.)

    // ── Memory Dust prestige (Full Vision). NOTHING RESETS — Dust accrues from mastery. ───────

    /// <summary>
    /// THE TRAITS SCREEN (P4): three worn characteristics, the collection, and the unknown.
    /// </summary>
    /// <remarks>
    /// The nav's TRAITS destination, and the only one: the old Memory tree screen that used to sit
    /// behind RH_SHOT_MODE=dusttree is deleted, along with the tree it drew.
    /// </remarks>
    private TraitCollectionScreen _traitScreen = null!;

    /// <summary>Which characters are yours, and which one you are. Unlocks derive from conquest.</summary>
    private CharacterState _characters = new();

    /// <summary>
    /// Descents finished with a Vow's demand still met — the one quest counter that cannot be derived.
    /// </summary>
    /// <remarks>
    /// Every other quest reads a fact still true when you look at it. A run's Vow is gone the moment the
    /// run ends, so if this is not latched at that instant it can never be proved afterwards.
    /// </remarks>
    private int _runsWithVowKept;

    /// <summary>Bosses felled across the career — latched at the reward, saved, quest-read (P10).</summary>
    private int _bossesFelled;
    private RosterScreen _roster = null!;
    private bool _showRoster;
    private LoadoutScreen _loadoutScreen = null!;
    private bool _showLoadout;

    /// <summary>THE VAULT — unopened chests, read before they are cracked.</summary>
    private VaultScreen _vault = null!;
    private bool _showVault;
    private bool _showTraits;

    /// <summary>
    /// MEMORY DUST — the wallet, and only the wallet.
    /// </summary>
    /// <remarks>
    /// The Memory tree this used to be is deleted: it sold twelve passive stat modifiers plus three
    /// rate multipliers that no live game could buy, so only old saves carried them and no screen could
    /// explain why two accounts differed. Dust itself stays because the economy spends it — Warren
    /// facility upgrades and expedition checkpoints — and it keeps its name because that name is still
    /// good in front of a player.
    /// </remarks>
    private MemoryDustWallet _dust = new();

    // ── TRAITS (P4). The account's characteristics: what has awakened, what fed it, and which three
    //    each champion wears. Account-wide discovery, per-character loadout (§25, §26). ────────────

    /// <summary>The account's trait ledger. Restored on load, written on save, never rebuilt.</summary>
    private readonly TraitLedger _traitLedger = new();

    /// <summary>
    /// What a running descent feeds, and what the reveal is drained from. One instance for the whole
    /// session — the ledger outlives every run, and so does what it has learned.
    /// </summary>
    private readonly TraitWatch _traitWatch;

    /// <summary>
    /// True until the first load-time trait check has run — which is what makes the six retroactive
    /// awakenings arrive as ONE plate rather than six ceremonies in one second (PLAN, §32).
    /// </summary>
    private bool _traitsFirstCheckOwed = true;

    /// <summary>
    /// What each skill has earned by being used. Owned by the game, not the expedition.
    /// </summary>
    /// <remarks>
    /// An expedition is minted per run and a skill's levels outlive every run, so this lives here and
    /// is handed to each expedition. That is the rule that makes unequipping a skill cost nothing.
    /// </remarks>
    private SkillProgress _skillProgress = new();
    private int _highestMasteryAwarded;

    // ── WHAT THE WORLD HAS TAUGHT AND WHAT THE HUNTER HAS PROVED ─────────────────────────────────
    //
    // Both of these used to come out of the trait tree, and only out of the trait tree: nineteen
    // keystone nodes and five study nodes were the sole producers of the whole keystone and Vow
    // catalogues. Keystones come from the world now (conquest, region mastery, the corruption) and
    // Vows come from keeping a rule once without them. Both sets are accumulated here, never reduced,
    // and both are written to the save so a legacy grant survives.

    /// <summary>Keystone ids this account knows. Unioned each frame with what the world derives.</summary>
    private readonly HashSet<string> _discoveredKeystones = new(StringComparer.Ordinal);

    /// <summary>Vow ids this account has found. Grows at the end of a descent, and never shrinks.</summary>
    private readonly HashSet<string> _discoveredVows = new(StringComparer.Ordinal);

    /// <summary>
    /// Every SHARED skill the account has ever had access to — the discovery latch behind the BUILD
    /// library's <c>???</c> tiles (release polish 2026-09-05).
    /// </summary>
    /// <remarks>
    /// Access follows the CURRENT mastery allocation (BRIEF sec.15-17) and a skill can go back to LOCKED;
    /// this set never shrinks, so a skill the player has seen once keeps its name while it is locked, and
    /// a skill they have never reached shows as a question. Persisted in <c>SaveGame.LearnedSkills</c>,
    /// which was written-never-read since the access rework and is exactly this latch by another name.
    /// </remarks>
    private readonly HashSet<string> _discoveredSkills = new(StringComparer.Ordinal);

    /// <summary>The discovery latch, brought up to date with what the tree reaches right now.</summary>
    private IReadOnlySet<string> DiscoveredSkillsNow()
    {
        foreach (var id in _mastery.AvailableSkills()) _discoveredSkills.Add(id);
        return _discoveredSkills;
    }

    /// <summary>The socket-capacity high-water mark: derived, legacy grant and worn count, maxed.</summary>
    private int _keystoneSocketsEarned;

    /// <summary>The keystone list handed to the composer and the BUILD screen, rebuilt when the set moves.</summary>
    private IReadOnlyList<Keystone> _keystoneMenu = Array.Empty<Keystone>();

    /// <summary>The Vow list handed to the composer and the workbench, rebuilt when the set moves.</summary>
    private IReadOnlyList<Vow> _vowMenu = Array.Empty<Vow>();

    /// <summary>True until the first grant pass has run, so a deep save reveals in one plate, not eleven.</summary>
    private bool _grantsBaselined;

    /// <summary>
    /// The skill a five-slot save lost on the way in, PARKED until the screens exist to say so.
    /// </summary>
    /// <remarks>
    /// The load runs from Initialize and every screen is built in LoadContent, so a toast posted here
    /// would be posted into a queue nothing is draining yet. Same reason as the run log and the tree
    /// camera two dozen lines below.
    /// </remarks>
    private string? _fifthSkillDropped;





    private AssetLibrary _assets = null!;
    private UiKit _ui = null!;
    private SoundBank _sound = null!;

    // ── Front-end ─────────────────────────────────────────────────────────────────────────────
    private bool _showTitle = true;
    private int _titleCursor;
    private bool _hasSave;

    /// <summary>Pick the scene background for whatever screen is currently showing.</summary>
    private void DrawSceneBackground()
    {
        if (_showForge) { _ui.Background(_batch, "bg_forge"); return; }
        if (_showWorld) { _ui.Background(_batch, "bg_regionmap"); return; }
        if (_showTraits) { _ui.Background(_batch, "bg_constellation"); return; }
        if (_showWarren) { _ui.Background(_batch, "bg_warren"); return; }
        if (_showMastery) { _ui.Background(_batch, "bg_warren"); return; }   // the workshop, reused as the build bench

        // Combat and results share the arena; results dims it so the panels read. Prefer a
        // region-themed arena when its art exists (bg_arena_machine, bg_arena_shadow, …), and fall
        // back to the verdant arena otherwise — so travelling to a new region themes automatically
        // the moment its background is added, with no code change.
        var theme = Regions.Get(_activeRegion).Theme.ToString().ToLowerInvariant();
        var arena = _assets.Get($"bg_arena_{theme}") is not null ? $"bg_arena_{theme}" : "bg_arena_verdant";
        // The arena darkens and cools with the corruption tier (CorruptionLook) — the world the player
        // chose to deepen should look deeper; ArenaDim is tier 0.
        var look = CorruptionLook.For(_world.CorruptionTier).Arena;
        _ui.Background(_batch, arena, new Color(look.R, look.G, look.B));
    }

    /// <summary>
    /// How far the ARENA is dimmed at draw time so the squad reads against it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The engine dims the background; the artist does not.</b> The asset spec used to demand
    /// backgrounds be authored at a mean luminance of 33 or less, which the first hand-drawn arena
    /// obeyed exactly — and the result was a painting compressed into <b>28 of 255 luminance values</b>,
    /// 11% of the range. There was a real forest in there: trees, canopy, stones. None of it survived.
    /// Nobody can make something beautiful in 28 values.
    /// </para>
    /// <para>
    /// The rule was right about the goal (a creature must read against its arena) and wrong about
    /// where to enforce it. Asking the artist to pre-bake the compromise destroys the art permanently
    /// and cannot be tuned; doing it here costs one multiply, keeps the source at full range, and is a
    /// number we can move. So the arena is authored bright, beautiful and deep — and dimmed HERE.
    /// </para>
    /// <para>
    /// <b>0.59x, and the number was found by LOOKING, not by the formula.</b> The spec says a creature
    /// (floor L=76) needs 35+ separation from its arena, which demanded 0.38x here and crushed the
    /// painting all over again. At 0.59x the arithmetic says FAIL — separation is 21 — and the
    /// creatures read perfectly anyway, because <b>the heavy iron-gall contour is what separates
    /// figure from ground, not luminance</b>. That is what the art bible meant by "Void Ink — the
    /// leading line" all along; the luminance rule was modelling the wrong mechanism. Trust the ink.
    /// </para>
    /// </remarks>
    // A LIGHT readability tint only (~15% darken), per the hunt spec — the old (150,150,162) multiply cut
    // the background to ~59% brightness and read as muddy/blurred.
    private static readonly Color ArenaDim = new(216, 216, 224);






    public Game1()
    {
        // Every number on screen is formatted with the current culture, so on a machine set to a
        // comma-decimal locale the HUD read "TEMPO 4,94x" and costs printed with the wrong separators.
        // The UI is authored in English with '.' decimals; pin the culture so it renders identically
        // everywhere (and so save files round-trip regardless of the player's regional settings).
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        // The trait watch wraps the session's one ledger. Built here rather than in LoadContent
        // because LoadOrStartFresh runs from Initialize and restores into it — the same rule the
        // _pending* fields exist for, taken the other way: this is a plain object with no device.
        _traitWatch = new TraitWatch(_traitLedger);

        _graphics = new GraphicsDeviceManager(this)
        {
            // RH_SHOT_WINDOW=WxH poses the rig at a REAL window size, so a capture can show what a 1280×720
            // player sees — the canvas bilinearly shrunk — rather than the 1920 render target.
            PreferredBackBufferWidth = ShotWindow?.X ?? CanvasWidth * 3,
            PreferredBackBufferHeight = ShotWindow?.Y ?? CanvasHeight * 3,
            // FULLSCREEN IS A BORDERLESS WINDOW THE SIZE OF THE MONITOR, not an exclusive mode switch.
            // Playtest 2026-08-25: "Fullscreen yayın yapılmıyor" — an exclusive-mode surface is invisible
            // to Discord screen share, OBS window capture and the Windows Game Bar, and it flickers the
            // desktop on every Alt-Tab. MonoGame's soft fullscreen keeps the desktop resolution, so the
            // aspect-fit letterbox (CanvasFit.PresentFit) is exactly what the player sees on stream.
            HardwareModeSwitch = false,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "IDLExIDLE — pre-alpha";
    }

    // ── Persistence. For an idle game this is not plumbing — it IS the game. ──────────────────
    private float _sinceAutosave;
    private string _bootMessage = "";
    private Color _bootColor = Bone;

    /// <summary>The return summary a real absence earned (UX V2 P1.3), held until CONTINUE. Null when there is none.</summary>
    private WelcomeSummary? _welcome;
    private bool _showWelcome;

    /// <summary>The welcome panel is on screen: pending, and no tour has the screen. Under a tour it waits its turn.</summary>
    private bool WelcomeUp => _showWelcome && !_tourActive && !_opening.Running;
    private float _bootTimer;   // the "welcome back" toast — a few seconds after boot, then it fades

    /// <summary>
    /// True when this session must never write the save file.
    /// </summary>
    /// <remarks>
    /// Latched when the load failed on a DAMAGED file or one from a NEWER build. The boot message
    /// promises the old file is safe — and before this latch existed, the ten-second autosave broke
    /// that promise on its first interval by writing a blank game over the file it could not read.
    /// START A NEW GAME (settings) is the one door out: deleting the file is the player's explicit
    /// choice, and the latch lifts with it.
    /// </remarks>
    private bool _saveLocked;
    private string _saveLockReason = "";
    private LoadFailure _saveLockFailure;   // WHY it locked — START A NEW GAME treats corrupt and newer differently

    /// <summary>Developer hotkeys (F6 force boss, F7 layout overlays) are live only under RH_DEV=1.</summary>
    private static readonly bool DevKeysEnabled = Environment.GetEnvironmentVariable("RH_DEV") == "1";

    /// <summary>True under the screenshot rig (RH_SHOT). The rig poses; it does not play.</summary>
    private static readonly bool CaptureRig = Environment.GetEnvironmentVariable("RH_SHOT") is not null;

    /// <summary>
    /// RH_SHOT_WINDOW=WxH: the rig's window size, when a capture should be the PRESENTED frame (the
    /// backbuffer after the letterbox blit) rather than the 1920×1080 render target. Null when unset.
    /// </summary>
    /// <remarks>
    /// The UX audit's 720p reviews were bilinear downscales of the 1080 capture done in Python — a good
    /// approximation of what the present blit does, but an approximation. This photographs the real thing,
    /// so "readable at 720p" is a claim about pixels a player actually receives.
    /// </remarks>
    private static readonly Point? ShotWindow =
        Environment.GetEnvironmentVariable("RH_SHOT_WINDOW") is { } w
        && w.Split('x', 'X') is [var sw, var sh]
        && int.TryParse(sw, out var ww) && int.TryParse(sh, out var wh) && ww > 0 && wh > 0
            ? new Point(ww, wh) : null;

    /// <summary>The `typespec` fixture: draw the type ladder itself instead of a screen.</summary>
    private bool _showTypeSpec;

    /// <summary>The screen a `tour` capture is about — RH_SHOT_TAB, an Activity name.</summary>
    private static Activity? ShotTab
        => Enum.TryParse<Activity>(Environment.GetEnvironmentVariable("RH_SHOT_TAB"), true, out var a) ? a : null;

    /// <summary>
    /// The capture mode, with `tour` resolved to the fixture that dresses the screen it tours.
    /// </summary>
    /// <remarks>
    /// A tour photographed over an empty screen proves only that a scrim was drawn; the light has to
    /// fall on the real furniture the card names. So `tour` borrows each screen's own fixture — the one
    /// its plain capture uses — and owes that screen's tour (see <see cref="SeedExplained"/>). The
    /// Hunt's fixture is `intro`, whose card is posed the same way.
    /// </remarks>
    private static string? ShotMode => Environment.GetEnvironmentVariable("RH_SHOT_MODE") switch
    {
        "tour" => ShotTab switch
        {
            Activity.Hunt => "intro",
            Activity.Training => "stats",
            Activity.Gear => "character",
            Activity.Build => "weave",
            Activity.Mastery => "buildtree",
            Activity.Vault => "vault",
            Activity.Forge => "forge",
            Activity.Warren => "warren",
            Activity.Map => "map",
            Activity.Traits => "dust",
            Activity.Roster => "roster",
            _ => "fight",
        },
        var other => other,
    };

    // ── START A NEW GAME (settings): the armed-confirm state — and the feedback-code toast. ────
    private float _resetArmTimer;      // >0 while the red are-you-sure state is armed
    private bool _wantsNewGame;        // set by the settings panel, acted on between frames in Update
    private string _feedbackToast = "";
    private float _feedbackToastTimer;

    /// <summary>
    /// Autosave cadence. Short on purpose.
    /// </summary>
    /// <remarks>
    /// A clean exit saves anyway, so this only matters when the game is killed or crashes. In an idle
    /// game that is precisely when losing progress hurts most — the player may have left it running for
    /// hours. Ten seconds is a cheap write (a few KB) against a very expensive loss. There is also an
    /// explicit checkpoint on every kill, which is the moment the player actually gains something.
    /// </remarks>
    private const float AutosaveIntervalSeconds = 10f;

    protected override void Initialize()
    {
        // Prefs before anything draws — the player should never see the default window flash past.
        // Skipped under RH_SHOT: a screenshot run must render at a known size regardless of what the
        // developer's machine happens to be set to. So is an AUTOPLAYED opening (Game1.OpeningRig.cs):
        // it films at the UI scale it was asked for, and it must neither read nor write the player's
        // display preferences.
        if (Environment.GetEnvironmentVariable("RH_SHOT") is null && !Autoplay)
        {
            var prefs = Display.Load();
            (_displayMode, _windowSize) = (prefs.Mode, prefs.Window);
            (_sfxVolume, _musicVolume, _askBeforeScrap) = (prefs.SfxVolume, prefs.MusicVolume, prefs.AskBeforeScrap);
            (_showDamageNumbers, _showSkillCallouts, _showHitEffects, _showScreenFlash)
                = (prefs.ShowDamageNumbers, prefs.ShowSkillCallouts, prefs.ShowHitEffects, prefs.ShowScreenFlash);
            _uiScalePercent = prefs.UiScalePercent;
            ReducedMotion = prefs.ReducedMotion;
            ApplyDisplay();
        }
        else
        {
            // The rig poses the UI SCALE too: RH_SHOT_UISCALE=100|125|150|auto, 100 when unset — never AUTO
            // by default, or every fixture would silently capture at 125% in the rig's 1280-wide window.
            var shotScale = Environment.GetEnvironmentVariable("RH_SHOT_UISCALE");
            _uiScalePercent = shotScale == "auto" ? 0 : int.TryParse(shotScale, out var sp) && sp is 125 or 150 ? sp : 100;
            // RH_SHOT_REDUCED=1 poses REDUCED MOTION (the accessibility matrix, brief §102–§107): set here,
            // before the first Update, so UiMotion.Reduced follows it on the very first frame and every
            // idle animation holds still on the photographed one. Off when unset, like the real setting.
            if (Environment.GetEnvironmentVariable("RH_SHOT_REDUCED") == "1") ReducedMotion = true;
            RecomputePresent();
        }

        _region = _world.RegionFarm(_activeRegion);
        LoadOrStartFresh();
        _region = _world.RegionFarm(_activeRegion); // a save may have changed the active region
        // Show the "welcome back" summary (offline earnings, or the first-session nudge) it just built —
        // an idle game's best-earned dopamine beat, and until now it was computed and silently dropped.
        if (_bootMessage.Length > 0) _bootTimer = 7f;
        base.Initialize();
    }

    /// <summary>Switch the region you're hunting in and farming. Only unlocked regions are selectable.</summary>
    private void SetActiveRegion(string id)
    {
        if (!_world.IsUnlocked(id)) return;
        _activeRegion = id;
        _region = _world.RegionFarm(id);
        _conquerMsg = "";
    }

    private void LoadOrStartFresh()
    {
        // A screenshot run starts from a CLEAN slate and never reads the player's save — so a shot is
        // deterministic (only its own seed shows) and can never entangle with, or write back, real progress.
        if (Environment.GetEnvironmentVariable("RH_SHOT") is not null) return;

        var result = SaveFile.Read();

        // A damaged live file gets ONE chance at self-repair before the session locks: quarantine the
        // corpse, then read the PREVIOUS good generation (save.bak — written by every save, and until
        // the 2026-08-23 review read by nothing). If it parses, promote it back to the live path and
        // carry on as a normal load — the player loses one save interval, not their file.
        if (!result.Ok && result.Failure == LoadFailure.Corrupt)
        {
            SaveFile.QuarantineCorrupt();
            var backup = SaveFile.ReadBackup();
            if (backup.Ok)
            {
                SaveFile.TryWrite(backup.Save!, out _);
                result = backup;
                _bootMessage = "THE SAVE FILE WAS DAMAGED — RESTORED FROM THE BACKUP. A LITTLE PROGRESS MAY BE MISSING.";
                _bootColor = Gold;
            }
        }

        if (!result.Ok)
        {
            // A corrupt save is NEVER silently replaced with a new game — that looks exactly like the
            // game deleted your progress on purpose. Say so, and leave the old file alone.
            _bootMessage = SaveSystem.Explain(result.Failure);
            _bootColor = result.Failure == LoadFailure.Missing ? Dim : Ember;


            // THE PROMISE ABOVE USED TO BE BROKEN WITHIN TEN SECONDS: the message said "your old file
            // has not been overwritten" while the autosave — running on the blank game this session
            // now holds — overwrote it on its first interval. Two guards close it for good: saving is
            // LATCHED OFF for the whole session (Save() refuses at its top), and a damaged file is
            // renamed aside (save.corrupt-<time>.json) so even a rogue later write cannot reach it.
            // The rename also means the NEXT boot finds no file and starts a fresh, saveable game
            // instead of hitting the same unreadable corpse forever. A newer-version file is locked
            // but NOT renamed — it is perfectly good, and the newer build it belongs to can read it.
            if (SaveFile.LocksSaving(result.Failure))
            {
                _saveLocked = true;
                _saveLockFailure = result.Failure;
                _saveLockReason = result.Failure == LoadFailure.FromNewerVersion
                    ? "THIS SAVE WAS MADE BY A NEWER VERSION OF THE GAME"
                    : "THE OLD SAVE FILE IS DAMAGED";
                // A corrupt file was already quarantined above (before the backup attempt); a
                // newer-version file stays exactly where it is — the build it belongs to reads it.
            }

            if (result.Failure == LoadFailure.Missing) SeedNewGame();
            return;
        }

        var save = result.Save!;
        _hasSave = true; // the title screen offers CONTINUE rather than NEW HUNT

        // An OLDER-format file is snapshotted before anything can write over it. The rolling backup
        // is one generation deep and rotates out ~20 seconds after boot, which was the whole window
        // in which a bad format migration stayed recoverable. Once per format version; a no-op on a
        // current-format save.
        SaveFile.SnapshotBeforeUpgrade(save.Version);

        // ...AND THE SAME NUMBER IS KEPT, because it is the only honest way to tell a file written
        // before the onboarding facts existed from one whose player simply has not lived that loop yet.
        _saveVersionSeen = save.Version;

        SaveSystem.RestoreHunter(save, _hunter);   // (the Merge→Salvage charter fold lives inside — Core, tested)
        _dust.Restore(save.MemoryDust);

        // BEFORE the loadout is restored, because Restore truncates to the capacity. Restoring first and
        // deriving after would drop a saved fifth skill on every load and then look correct forever
        // after, since the player would simply never see it again.
        //
        // THE UNLOCK GATE DELIBERATELY DOES NOT APPLY HERE, and getting that wrong would have been
        // catastrophic and silent. Skill slots are now gated by progress (Unlocks.SkillSlots), but the
        // facts that gate reads — deepest wave, regions conquered — are restored BELOW this line and on
        // line 407. Asking the gate here would see an all-zero player, return a capacity of one, and
        // Restore would truncate every existing player's four-skill build down to one. They would then
        // never see the other three again, because the save it wrote back would agree.
        //
        // So the restore floor is the save's OWN skill count: whatever a player had, they keep. The
        // gate is applied per-frame afterwards, by which point the facts are real.
        // WHAT THE OLD TRAIT TREE HAD ALREADY BOUGHT, read ONCE, before anything can be written back.
        // Frozen table, floors and unions only, so running it on every load forever grants nothing a
        // first pass did not (that idempotence is what makes it testable rather than a one-shot).
        var legacy = LegacyTraitTree.Read(save.MemoryDustUnlocks);
        foreach (var id in save.DiscoveredKeystoneIds) _discoveredKeystones.Add(id);
        foreach (var id in legacy.Keystones) _discoveredKeystones.Add(id);
        foreach (var id in save.DiscoveredVowIds) _discoveredVows.Add(id);
        foreach (var id in legacy.Vows) _discoveredVows.Add(id);

        // THE FIFTH SKILL SLOT IS GONE, and this is where a five-row build loses its last row. Capacity
        // is clamped to four by the loadout itself; the floor is still the save's own count so a
        // one-slot or two-slot player is not handed four (the gate below cannot be asked yet — see the
        // note above). The dropped skill keeps every level it earned: SkillProgress is keyed by skill
        // id, not by slot, so it can be woven again in place of another at any time.
        if (save.WovenSkills.Count > PlayerLoadout.MaxSkills)
            _fifthSkillDropped = SkillCatalogue.Find(save.WovenSkills[^1].SkillId ?? "")?.Name ?? "A SKILL";
        _loadout.SkillCapacity = Math.Max(1, save.WovenSkills.Count);
        // SOCKETS, floored the same way and for the same reason. The unlock facts are all zero at this
        // point in the load — conquest is not restored until RestoreWorld, far below — so asking the
        // derived rule HERE would see a player with no conquests, answer zero, and PlayerLoadout.Restore
        // would truncate every existing player's worn keystones away. They would never see them again,
        // because the save written back would agree. So: the save's own latch, its legacy grant, and
        // what it was actually WEARING, whichever is largest.
        _keystoneSocketsEarned = Math.Max(
            Math.Max(save.KeystoneSocketsEarned, legacy.KeystoneSockets),
            save.SocketedKeystoneIds.Count);
        _loadout.KeystoneCapacity = _keystoneSocketsEarned;
        _highestMasteryAwarded = save.HighestMasteryAwarded;

        // The woven build. Only overwrite the Starter when the save actually carries one — a pre-solo
        // save has an empty list, and restoring THAT would strip a returning player down to no skills.
        if (save.WovenSkills.Count > 0)
            _loadout.Restore(
                save.WovenSkills.Select(s => (s.SkillId, (string?)s.Source, (string?)s.Form, s.VowId, s.Passive)),
                save.SocketedKeystoneIds);

        // AN UNTAUGHT VOW IS NOT WORN (P8). The composer refuses to pay one (P7's gate) — but a
        // restored slot silently wearing it would be invisible too: the weave screen lists only
        // KNOWN vows, so nothing on any screen could explain the missing pay. Dropped on the way
        // in, like every other id the account does not know.
        RebuildBuildMenus();
        for (var i = 0; i < _loadout.Skills.Count; i++)
            if (_loadout.Skills[i].VowId is { } vid && _vowMenu.All(v => v.Id != vid))
                _loadout.SetVow(i, null, _vowMenu);
        _skillProgress.Restore(save.SkillProgress.Select(
            r => (r.SkillId, r.Uses, r.Variation, (IReadOnlyList<string>)r.Reinforcements)));

        _deepestEver = save.MasteryEarned;         // stored the deepest-ever; Earned re-derives from it
        _mastery.RestoreTaken(save.MasteryTaken);
        // save.LearnedSkills IS THE DISCOVERY LATCH, read again (release polish 2026-09-05). It stopped
        // granting ACCESS when access started following the current allocation (BRIEF sec.15-17) and was
        // written-never-read for a version; the BUILD library's ??? tiles need exactly what it records —
        // which shared skills this account has ever reached. Seeded from the current tree and from any
        // skill with waves on it too, so a save from before the latch was kept shows nothing as unknown
        // that the player has plainly used.
        _discoveredSkills.Clear();
        foreach (var id in save.LearnedSkills) _discoveredSkills.Add(id);
        foreach (var row in save.SkillProgress) if (row.Uses > 0) _discoveredSkills.Add(row.SkillId);
        DiscoveredSkillsNow();
        // A SET ANNOUNCED IS ANNOUNCED FOR GOOD — but the screen that remembers it does not exist yet.
        // This runs from Initialize and GearScreen is built in LoadContent, which needs a GraphicsDevice,
        // so the list is PARKED here and handed over the moment the screen is there, exactly like the
        // tree camera two lines below. Calling the screen from here was a null reference that killed
        // the boot outright.
        _completedSetsLoaded = save.CompletedSets;
        // The tree's camera. PARKED like the run log below: _masteryScreen is built in LoadContent. A
        // save from before the camera existed carries zoom 0, which the screen answers with its
        // first-open framing — the same first sight of the tree a new game gets.
        _pendingTreeCamera = (save.MasteryZoom, save.MasteryPanX, save.MasteryPanY);

        // The Warren facility economy — levels/XP restored before the offline tick below so its production
        // is computed against the real facility levels, not a fresh level-1 base.
        SaveSystem.RestoreWarren(save, _warren, legacy);
        // PARKED, not applied. This method runs from Initialize(); every screen — _expedition included —
        // is constructed in LoadContent(), which has not run yet, so reaching through _expedition here
        // dereferences null and takes the whole game down before the window opens.
        //
        // It did exactly that, for every player who had ever saved: a first launch has no save file and
        // returns above, so the crash began on the SECOND launch and never once appeared in a capture,
        // because LoadOrStartFresh returns at the top when RH_SHOT is set. The screenshot rig cannot see
        // this code path at all. Anything restored here belongs in a _pending* field.
        _pendingRunLog = save.RunLog.Select(RunLog.FromSave).ToList();
        // Safe here, unlike the run log: CharacterState is a plain field constructed with this class,
        // not a screen built in LoadContent. That distinction is exactly what crashed the game once.
        // The banked unlocked set rides in too; a save from before it was banked is seeded from the
        // gates that were true when it was written (LegacyUnlocks), so no champion is taken back.
        SaveSystem.RestoreCharacters(save, _characters);
        // THE SIGNATURE IS EQUIPPED FOR THE PLAYER (release polish 2026-09-05): a save whose build has
        // room but not the active champion's own skill gets it here, into an empty slot only. The
        // switch repair does the same on every switch (RepairForSwitch); the fresh-game path already
        // starts with it (PlayerLoadout.Starter).
        // NAMED HERE, AND NOT ONE LINE EARLIER. The pin has to know which skill is the signature, and
        // only the RestoreCharacters above makes _characters.Active the champion the save was written
        // with — before it, Active is whoever CharacterState was constructed with, so naming it any
        // sooner pinned the STARTER's skill for every player who had switched champions, and the lock
        // silently guarded the wrong slot until ApplyBuildCapacities corrected it on the first Update.
        _loadout.SignatureSkillId = _characters.Active?.SignatureSkillId;
        LoadoutRepair.EnsureSignature(_loadout, _characters.Active);
        _runsWithVowKept = save.RunsWithVowKept;
        // THE TRAIT ACCOUNT. A plain object with no device, like CharacterState above and unlike the
        // screens below, so it is safe to restore here in Initialize. Unknown ids and unknown counter
        // names are dropped inside Restore, and every loadout is re-clamped to three discovered
        // traits on the way in — a save is an input like any other.
        _traitLedger.Restore(
            save.DiscoveredTraits,
            save.TraitTally,
            save.TraitLoadouts.Select(r => (r.CharacterId, (IReadOnlyList<string>)r.TraitIds)),
            save.TraitProvenance.Select(r =>
                (r.TraitId, new TraitFirst(r.CharacterId, r.RegionId, r.Wave))));
        // Seeded from chests on a pre-P10 save: every opened chest was a felled boss, so the floor
        // is honest — and a MAGPIE chase already underway keeps most of its steps.
        _bossesFelled = save.BossesFelled > 0 ? save.BossesFelled : save.ChestsOpened;
        // THE RETIRED LADDER'S DISMISSED RUNGS, read and written back untouched. The names mean
        // nothing to the lesson catalogue — completion is a fact now, and a closed card completes
        // nothing — but they are carried across a save/load cycle so that rolling this build back does
        // not lose what an old player had closed. Nothing reads them.
        _dismissedGuide.Clear();
        foreach (var rung in save.DismissedGuideRungs) _dismissedGuide.Add(rung);
        // The intro flag is a plain field; the explained list is PARKED, because seeding it asks the
        // unlock gates, and those read the Forge's inventory — a screen LoadContent has not built yet.
        // Seeded in SeedExplained, from ApplyRestoredState, once the bag is real.
        _introSeen = save.IntroSeen;
        // THE AUTHORED OPENING, parked the same way and for the same reason. The cursor is not the
        // truth — the deeds are — so it is walked forward over anything already done (SeedOpening).
        _prologueSeen = save.PrologueSeen;
        _pendingOpeningStage = save.OpeningStage;
        // ...AND THE VERSION IS WHAT SAYS "ALREADY GRANTED" FOR EVERY OLDER FILE. Before v6 the gift
        // was seeded into the vault of every new game at frame one, so a v5 file has had it — whether
        // the chest is still sitting there unopened, was opened long ago, or was sold. Answering this
        // from progression instead ("no welcome chest in the bag") would hand a second gift to every
        // returning player who had already opened theirs. See OpeningScript.FirstVersionWithOpeningState.
        _welcomeGiftGranted = save.WelcomeGiftGranted
                              || save.Version < OpeningScript.FirstVersionWithOpeningState;
        _tutorialsDone.Clear();
        foreach (var done in save.TutorialsDone) _tutorialsDone.Add(done);
        _guidanceOff = save.GuidanceOff;
        _reportOpenedEver = save.ReportOpenedEver;
        _changedAfterFall = save.ChangedAfterFall;
        _retriedAfterChange = save.RetriedAfterChange;
        _dispatchesOpenedEver = save.DispatchesOpenedEver;
        // THE INBOX — a plain model with no device, like the trait ledger above, so it is safe to
        // restore here in Initialize. Rows are deduped by key on the way in and a Kind this build does
        // not know is dropped (Core, tested) — never a boot crash.
        _inbox.Restore(SaveSystem.RestoreDispatches(save), save.KnownDispatchKeys);
        // PARKED for the inbox seed: a file from before the inbox is told what it already knows in
        // SeedExplained, once the rail's revealed set is real (Reveal.Restore reads the Forge's bag,
        // and the Forge is built in LoadContent).
        _pendingInboxSeed = save;
        _pendingExplained = save.ExplainedScreens.Select(Onboarding.ModernScreenKey).ToList();
        _pendingRevealed = save.RevealedScreens.ToList();
        // PARKED, exactly like the run log above and for exactly the reason the comment above gives.
        // This line was `_forge.RestoreChestsOpened(...)`, and _forge is a ForgeScreen built in
        // LoadContent — which has not run yet. It threw a NullReferenceException and took the game down
        // before the window opened, on EVERY launch that had a save to load.
        //
        // The comment two lines above describes this precise failure, names _expedition as the example,
        // and says "anything restored here belongs in a _pending* field". The very next screen touched
        // did not follow it. That is worth stating plainly rather than quietly fixing: the warning was
        // correct, present, and not enough — which is why the guard for it is now a test rather than a
        // paragraph.
        _pendingChestsOpened = save.ChestsOpened;
        // Parked for the same reason: the Forge owns the flag. Core decides what an old save means.
        _pendingFreeSocketUsed = SaveSystem.RestoreFreeSocketUsed(save);
        _chestKeepMinTier = save.ChestKeepMinTier;
        _autoSellOn = save.AutoSellOn;
        _traderWeek = save.TraderWeekStamp;
        _traderBought.Clear();
        foreach (var slot in save.TraderBoughtSlots) _traderBought.Add(slot);
        _chestKeepSlots.Clear();
        foreach (var name in save.ChestKeepSlots)
            if (Enum.TryParse<ItemBaseType>(name, out var slotWanted)) _chestKeepSlots.Add(slotWanted);
        // An older save carried one slot; it becomes the one wanted slot.
        if (_chestKeepSlots.Count == 0 && Enum.TryParse<ItemBaseType>(save.ChestKeepSlot ?? "", out var legacySlot))
            _chestKeepSlots.Add(legacySlot);
        _pendingInventory = SaveSystem.RestoreInventory(save);
        _pendingChests = save.UnopenedChests
            .Select(s => new Chest
            {
                Rarity = (Rarity)s.Rarity,
                Tier = s.Tier,
                Element = Enum.TryParse<Source>(s.Element, out var e) ? e : null,   // unknown element → inert, never a throw
                Region = s.Region,                            // null on a pre-profile save → uniform loot, as before
                RunTilt = s.RunTilt <= 0f ? 1f : s.RunTilt,   // a pre-tilt save reads 0; neutral is 1
                Gift = s.Gift,                                // null on every save from before gifts
            })
            .ToList();
        _pendingWorn = new Dictionary<GearSlot, string?>
        {
            [GearSlot.Weapon] = save.WornWeaponId, [GearSlot.Charm] = save.WornCharmId, [GearSlot.Focus] = save.WornFocusId,
            [GearSlot.Helm] = save.WornHelmId, [GearSlot.Chest] = save.WornChestId, [GearSlot.Gloves] = save.WornGlovesId,
            [GearSlot.Boots] = save.WornBootsId, [GearSlot.Ring] = save.WornRingId,
        };

        // ── Restore the world: conquest, corruption, per-region farms. In Core now
        // (SaveSystem.RestoreWorld) because the farm loop used to index the region table directly —
        // a renamed region id in an old save was a KeyNotFoundException before the window opened,
        // unreachable by any capture. Core drops unknown ids, and a test holds it to that. The
        // active region resolves against the restored conquest; unknown or locked falls back home.
        SaveSystem.RestoreWorld(save, _world);
        if (!string.IsNullOrEmpty(save.ActiveRegion) && _world.IsUnlocked(save.ActiveRegion))
            _activeRegion = save.ActiveRegion;

        _region = _world.RegionFarm(_activeRegion);

        // ── Offline progression: the Warren and the champion earned while you were away. ──────
        // Always apply the elapsed time (even a few seconds), but only greet the player when the trip
        // actually produced something — a "0.0 HOURS, 0 GLEAM" banner is noise, not a welcome.
        var credited = SaveSystem.CreditedOfflineSeconds(result.OfflineSeconds);

        // The Warren produced the whole time you were away — credit it into the real balances (Gleam,
        // Dust, and the Forge's Scrap/Essence — the same wallets the live tick feeds).
        //
        // ConqueredRegions IS SET HERE, and was not before. Its only other assignments live in TickWarren
        // and DrawWarren, both of which run from Update/Draw — while this runs from Initialize, before
        // either has executed. So every offline credit in the game was computed with the conquest bonus
        // at zero: the one payout that most needed it, silently missing it.
        //
        // Gated like the live tick — a player who has not taken a region has no Warren, awake or asleep.
        _warren.ConqueredRegions = _world.ConqueredIds.Count;
        // THE CAMP HOLDS SO MANY HOURS (OfflineCamp, 2026-09-06): the absence is paid for the hours the
        // camp holds — two on a fresh account, more for every facility level bought — at the camp's
        // efficiency; the Warren produces for the same held hours. A day away used to pay a full day
        // at half rate from minute one, which is what made an unbuilt Warren pointless.
        var warrenOpen = Unlocks.IsOpen(Activity.Warren, GuideUnlockFacts());
        var camp = warrenOpen ? _warren : null;
        var held = OfflineCamp.CreditedSeconds(credited, camp);
        var wOffline = warrenOpen
            ? _warren.Tick((float)held)
            : default;
        _hunter.AddGleam((int)wOffline.Gleam);
        _dust.AddDust((int)wOffline.Dust);
        _hunter.AddMaterial(Material.Scrap, (int)wOffline.Scrap);
        _hunter.AddMaterial(Material.Essence, (int)wOffline.Essence);

        // THE CHAMPION fought while you were away — the SAME simulation, headless (P5). The old
        // credit was a rate measured LAST session × seconds × 0.5: zero for anyone whose final
        // session never fought ten sustained seconds, and wrong for anyone whose build, region or
        // corruption changed since. OfflineHunt runs the real descent against the build as it
        // stands NOW — capped at a bounded number of waves, the remainder extrapolated at the rate
        // the simulated portion just measured — with the 50% haircut inside it (away is never as
        // good as playing; that's what brings you back).
        var champOffline = 0L;
        OfflineHunt.Result offline = default;
        if (credited > 0)
        {
            var (obh, obd) = EnemyBaselineFor(_activeRegion);
            offline = OfflineHunt.Simulate(
                ComposeBuild(),
                _hunter, held, _activeRegion, Regions.Get(_activeRegion).CombatBias, obh, obd,
                seed: unchecked((int)Math.Round(result.OfflineSeconds)) ^ _deepestEver,
                tuning: TuningNow());   // the same wave model the live descent will fight — see TuningNow
            champOffline = OfflineCamp.HuntCredit(offline, camp, credited);
            if (champOffline > 0) _hunter.AddGleam((int)Math.Min(int.MaxValue, champOffline));

            // WHERE IT WAS STANDING. The first live descent opens there instead of at wave one — the
            // whole of "the game shouldn't start at Wave 1 every time it's launched". It changes
            // nothing else: not BestDepth, not _deepestEver, not a quest, not a mastery point. Offline
            // still earns no RECORDS (OfflineHunt's own contract); this is a starting position, and one
            // descent's worth of it.
            // CAPPED AT WHAT THE PLAYER HAS ALREADY PROVED HERE. The live run RECORDS depth (RecordDepth
            // / _deepestEver, further down this file), so a resume deeper than the region's best depth
            // would write a permanent record — and, past wave 20, a CONQUEST — for waves nobody fought.
            // That is not a discount on the checkpoint, it is the checkpoint's whole product for free,
            // and it also breaks OfflineHunt's own standing contract that offline earns no records.
            // Bounded this way the resume can only ever save the player from re-walking ground they
            // have already held, which is exactly what it is for. Found by the session's own review.
            var provedHere = _world.RegionFarm(_activeRegion).BestDepth;
            _offlineResumeWave = Math.Min(offline.DeepestWave, provedHere) > 1
                ? Math.Min(offline.DeepestWave, provedHere)
                : 0;
            _offlineResumeRegion = _activeRegion;
            _offlineResumeRuns = 0;
        }

        // Seed the LIVE rate from the save, not just the offline calc above. Without this the field stays 0
        // until the champion fights for >10s and recomputes it — so a short or farm-only session would
        // autosave that 0 straight back, wiping offline champion income until the next sustained fight.
        _champGleamRate = save.ChampionGleamRate;

        // (The creature-farm offline catch-up loop lived here until 2026-08-24. Its screen was never
        // reachable, so no farm was ever staffed and the loop always summed zero — the "0 KILLS ·
        // 0 CORES" every welcome-back toast showed. Retired with the subsystem.)
        // THE RETURN SUMMARY (UX V2 P1.3, brief §25). A real absence that paid gets a held PANEL — away time,
        // what the hunt did (Gleam, waves, falls, deepest), what the Warren produced (all four outputs) —
        // with CONTINUE, because the largest single payment in the game deserves more than a seven-second
        // toast that named one number and dropped the rest. A short trip keeps the toast.
        var summary = new WelcomeSummary(credited, offline, wOffline, warrenOpen, CampSeconds: held, HuntGleamPaid: champOffline);
        if (summary.ShowsPanel)
        {
            _welcome = summary;
            _showWelcome = true;
        }
        else if (champOffline > 0)
        {
            _bootMessage = $"WELCOME BACK\n+{Abbrev(champOffline)} GLEAM EARNED WHILE AWAY";
            _bootColor = Gold;
        }
    }

    /// <summary>
    /// Seed a brand-new game. There is nothing to park but the first-boot message.
    /// </summary>
    /// <remarks>
    /// This used to seed three starter creatures and two unhatched cores for the squad auto-battler.
    /// The creature subsystem retired 2026-08-24: the CHAMPION is what fights, and it needs no seed.
    /// </remarks>
    private void SeedNewGame()
    {
        // NO FIRST-BOOT TOAST. It said "YOUR HUNTER IS ALREADY FIGHTING / WATCH THE FIRST WAVES —
        // SCREENS OPEN AS YOU PLAY", and both halves of it are now false as well as duplicated: a
        // fresh career does not land in a fight at all, it lands in the prologue, and the fight is
        // held still until the authored opening has introduced the thing on the stage. What the toast
        // was for is said by the opening's own first beats, on the frame each one is true.
        //
        // The second half — that screens open as you play — is the rail's job: a locked tile names what
        // opens it when it is pressed, and a newly opened one wears an unread dot until it is visited.
        // (Before this, the message named what was LOCKED: "PRESS B TO PICK SKILLS", while BUILD stays
        // shut until wave 5. The first thing the game said was an instruction it then refused.)

        // THE WELCOME GIFT NO LONGER EXISTS AT FRAME ONE — IT COMES FROM THE FIRST BOSS.
        //
        // It used to be seeded here, so a career opened with an unopened chest in a vault the player
        // had never been told about, earned by nothing. That reversed the only order this game has:
        // the boss is the game's own rhythm, the chest is what a boss leaves, and the VAULT is where a
        // chest waits. Handing all three over before the first wave meant the tutorial's own sentence
        // — "it left a chest" — described something that had been sitting there since launch, and it
        // opened the VAULT's chain at a moment when nothing had happened.
        //
        // Granted instead on the frame the tutorial boss falls (see the reward loop's welcome-gift
        // latch), through the real vault path, exactly once per career. A brand-new game seeds
        // NOTHING now.
    }

    private List<ItemInstance>? _pendingInventory;
    private List<Chest>? _pendingChests;
    private List<RunReport>? _pendingRunLog;
    private (float Zoom, float PanX, float PanY)? _pendingTreeCamera;
    private Dictionary<GearSlot, string?> _pendingWorn = new();
    private int _pendingChestsOpened;
    private bool _pendingFreeSocketUsed;

    /// <summary>Gems held at the last frame — the edge that announces the FIRST one. Seeded at load, so a
    /// returning player's gems are not "news".</summary>
    private int _gemsHeldLast;

    /// <summary>Every gem the player holds: loose in the bag, or set into an item.</summary>
    private int GemsHeld() => _forge?.Inventory.Sum(i => GemCraft.IsGem(i) ? 1 : i.Gems.Count) ?? 0;

    private void Save()
    {
        // THE LOAD FAILED AND SAID SO. A session that could not read the player's file holds nothing
        // but a blank game, and the boot message just promised the old file was safe — this latch is
        // what keeps the ten-second autosave from breaking that promise on its first interval.
        // START A NEW GAME (StartNewGame) is the one way out: it deletes the file deliberately.
        if (_saveLocked) return;

        // A screenshot run seeds throwaway fixed-id items; it must NEVER write them into the player's real
        // save. Autosave and conquest both call this, so the guard lives here, at the one write site.
        //
        // RH_BOOTCHECK sits on the SAME guard, and that is what makes it safe to point at the real save.
        // It is the one mode that deliberately reads a player's progress, so it is also the one that most
        // needs to be unable to write it — sharing this line means no second write site can appear
        // without inheriting the protection.
        if (Environment.GetEnvironmentVariable("RH_SHOT") is not null) return;

        // A BOOT CHECK MAY WRITE, BUT ONLY SOMEWHERE ELSE. The guard exists to protect the player's
        // save, and RH_SAVE_DIR already protects it by pointing the whole save system at another
        // directory — so blocking the write there would only prevent the check from testing the half
        // of persistence that matters most. Saving is the thing that, if broken, costs an hour of
        // play and shows no symptom until it is too late to fix.
        //
        // The condition is exact: with RH_SAVE_DIR unset, a boot check still cannot write at all, so
        // the real file is untouchable by the same rule as before.
        if (Environment.GetEnvironmentVariable("RH_BOOTCHECK") is not null
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RH_SAVE_DIR"))) return;

        var save = SaveSystem.Capture(
            _hunter, _forge.Inventory, SaveFile.NowMs,
            _dust, _highestMasteryAwarded, _world, _activeRegion, _warren) with
        {
            // The build rides along via `with`, so Core's Capture stays unaware of the Game-layer loadout.
            WovenSkills = _loadout.SaveSkills()
                .Select(s => new SavedSkill
                {
                    SkillId = s.SkillId, Source = s.Source, VowId = s.VowId, Passive = s.Passive,
                }).ToList(),
            SocketedKeystoneIds = _loadout.KeystoneIds.ToList(),
            // WHAT THE WORLD HAS TAUGHT, AND WHAT THE HUNTER HAS PROVED. Keystone knowledge is
            // re-derived from conquest and region mastery on every load and UNIONED with this, so the
            // field can only ever carry something forward — a legacy trait-tree grant, most of all.
            // Vow knowledge has nowhere else to live: a discovery is an event, so it must be latched.
            DiscoveredKeystoneIds = _discoveredKeystones.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            DiscoveredVowIds = _discoveredVows.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            KeystoneSocketsEarned = _keystoneSocketsEarned,
            SkillProgress = _skillProgress.ToSave()
                .Select(r => new SavedSkillProgress
                {
                    SkillId = r.SkillId, Uses = r.Uses, Variation = r.Variation,
                    Reinforcements = r.Taken.ToList(),
                }).ToList(),
            // The expedition log rides along the same way. The report is the only place this game can
            // teach, and the player it teaches is by definition not watching — the lesson has to survive
            // being closed.
            RunLog = _expedition.Log.Entries.Select(RunLog.ToSave).ToList(),
            MasteryTaken = _mastery.Taken.ToList(),
            // THE DISCOVERY LATCH: every shared skill this account has ever reached (see
            // _discoveredSkills). Access itself is recomputed from MasteryTaken; this only decides which
            // tiles the BUILD library may name.
            LearnedSkills = DiscoveredSkillsNow().OrderBy(s => s, StringComparer.Ordinal).ToList(),
            // Which five-piece sets have already had their one announcement (see the GEAR block).
            CompletedSets = _gear.CompletedSets.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            // The tree's camera, so the zoom a player settled on is the zoom they come back to. The
            // ten-second autosave carries it; nothing marks the screen dirty per wheel-tick.
            MasteryZoom = _masteryScreen.CameraZoom,
            MasteryPanX = _masteryScreen.CameraPanX,
            MasteryPanY = _masteryScreen.CameraPanY,
            // WHICH character, and WHICH ARE EARNED. The earned set used to be derived every frame
            // and never written, on the argument that it could not fall out of step with the world.
            // It can fall out of step with the RULES: the tiered roster tightened every second
            // champion's gate, and a derived set would have taken those champions back.
            ActiveCharacterId = _characters.ActiveId,
            UnlockedCharacters = _characters.SaveUnlocked().ToList(),
            QuestsDone = _characters.SaveQuests().ToList(),
            RunsWithVowKept = _runsWithVowKept,
            BossesFelled = _bossesFelled,
            // THE TRAIT ACCOUNT — all four fields, in the same commit as the four on SaveGame. A
            // field added there and forgotten here serialises at its default for ever, and autosave
            // fires every ten seconds: the ledger would be wiped within one interval of being earned.
            DiscoveredTraits = _traitLedger.Discovered.ToList(),
            TraitTally = _traitLedger.SaveTally().ToDictionary(kv => kv.Key, kv => kv.Value),
            TraitLoadouts = _traitLedger.SaveLoadouts()
                .Select(r => new SavedTraitLoadout { CharacterId = r.CharacterId, TraitIds = r.TraitIds.ToList() })
                .ToList(),
            TraitProvenance = _traitLedger.SaveProvenance()
                .Select(r => new SavedTraitFirst
                {
                    TraitId = r.TraitId, CharacterId = r.First.CharacterId,
                    RegionId = r.First.RegionId, Wave = r.First.Wave,
                })
                .ToList(),
            ChestsOpened = _forge.ChestsOpened,
            FreeSocketUsed = _forge.FreeSocketUsed,
            ChestKeepMinTier = _chestKeepMinTier,
            AutoSellOn = _autoSellOn,
            TraderWeekStamp = _traderWeek,
            TraderBoughtSlots = _traderBought.ToList(),
            ChestKeepSlots = _chestKeepSlots.Select(sl => sl.ToString()).ToList(),
            MasteryEarned = _deepestEver,          // stored as deepest-ever; Earned is re-derived on load
            ChampionGleamRate = _champGleamRate,
            DismissedGuideRungs = _dismissedGuide.OrderBy(s => s).ToList(),
            IntroSeen = _introSeen,
            PrologueSeen = _prologueSeen,
            OpeningStage = (int)_opening.Stage,
            WelcomeGiftGranted = _welcomeGiftGranted,
            TutorialsDone = _tutorialsDone.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            GuidanceOff = _guidanceOff,
            ReportOpenedEver = _reportOpenedEver,
            ChangedAfterFall = _changedAfterFall,
            RetriedAfterChange = _retriedAfterChange,
            // THE INBOX — rows in created order (the order IS meaning), the known-key set, and the
            // opened latch. All three, in the same commit as the three on SaveGame: a field added there
            // and forgotten here serialises empty for ever, and autosave fires every ten seconds.
            Dispatches = _inbox.ToSave(),
            KnownDispatchKeys = _inbox.KnownToSave(),
            DispatchesOpenedEver = _dispatchesOpenedEver,
            ExplainedScreens = _explained.OrderBy(s => s).ToList(),
            RevealedScreens = Reveal.Names(_revealed),
            // Unopened chests ride along too — a boss's drop must survive a reload, opened or not.
            UnopenedChests = _forge.UnopenedChests
                .Select(c => new SavedChest
                {
                    Rarity = (int)c.Rarity, Tier = c.Tier, Element = c.Element?.ToString(),
                    Region = c.Region, RunTilt = c.RunTilt, Gift = c.Gift,
                })
                .ToList(),
        };

        SaveFile.TryWrite(save, out _);
    }

    /// <summary>
    /// Everything a bug report needs, as one pasteable line — see <see cref="ShareCodes.SharedFeedback"/>.
    /// </summary>
    private string FeedbackCode() => ShareCodes.EncodeFeedback(new ShareCodes.SharedFeedback
    {
        Build = BuildStamp.Full,
        SaveVersion = SaveGame.CurrentVersion,
        DeepestWave = _deepestEver,
        RegionsConquered = _world.ConqueredIds.Count,
        CorruptionTier = _world.CorruptionTier,
        Gleam = _hunter.Gleam,
        TrainingRanks = Enum.GetValues<HunterStat>().Sum(_hunter.RankOf),
        Loadout = new ShareCodes.SharedBuild
        {
            Skills = _loadout.SaveSkills()
                .Select(s => new SavedSkill
                {
                    SkillId = s.SkillId, Source = s.Source, VowId = s.VowId, Passive = s.Passive,
                }).ToList(),
            Keystones = _loadout.KeystoneIds.ToList(),
            Mastery = _mastery.Taken.ToList(),
        },
        Worn = Enum.GetValues<GearSlot>()
            .Select(sl => (Slot: sl, Item: _hunter.Worn(sl)))
            .Where(x => x.Item is not null)
            .Select(x => new ShareCodes.WornItemSummary
            {
                Slot = x.Slot.ToString(), Rarity = (int)x.Item!.Rarity, Level = x.Item.ItemLevel,
            })
            .ToList(),
        RunLog = _expedition.Log.Entries.Select(RunLog.ToSave).ToList(),
    });

    /// <summary>
    /// Delete the save and begin again — START A NEW GAME, after its armed second click.
    /// </summary>
    /// <remarks>
    /// Runs between frames (see the flag in Update): it replaces the core state AND rebuilds every
    /// screen, because several screens capture references at construction and would otherwise keep
    /// serving the abandoned game. It is also the recovery path for a locked session (a damaged file,
    /// or one from a newer build): deleting the file is the player's explicit choice, so the write
    /// latch lifts here and nowhere else.
    /// </remarks>
    private void StartNewGame()
    {
        // What this reset removes may be the only copy of something good (review 2026-08-23): a save
        // from a NEWER build is perfectly readable by the build it belongs to, and the backup is the
        // last real generation. Neither is deleted — both are set aside — and only then does the live
        // path clear for the fresh game.
        if (_saveLockFailure == LoadFailure.FromNewerVersion) SaveFile.QuarantineNewer();
        SaveFile.PreserveBackupAside();
        SaveFile.TryDelete(out _);   // the Persistence API owns the disk — no raw File calls in a screen
        _saveLocked = false;
        _saveLockReason = "";
        _saveLockFailure = LoadFailure.None;

        // Fresh core state. These fields are deliberately not readonly so this method can exist.
        _hunter = new Hunter();
        _world = new World();
        _loadout = PlayerLoadout.Starter(_characters.Active);
        _discoveredSkills.Clear();   // a new game has reached nothing yet
        _mastery = new MasteryTree();
        _dust = new MemoryDustWallet();
        _skillProgress = new SkillProgress();
        _characters = new CharacterState();
        _warren = new Warren();
        _activeRegion = VerdantHollow.RegionId;

        _deepestEver = 0;
        _champGleamAccrued = 0;
        _champSecondsAccrued = 0;
        _champGleamRate = 0f;
        _chestKeepMinTier = 0;
        _autoSellOn = false;
        _chestKeepSlots.Clear();
        _traderWeek = 0;
        _traderBought.Clear();
        _traderStock = null;
        _traderStockLevel = 0;
        _runsWithVowKept = 0;
        _bossesFelled = 0;
        _highestMasteryAwarded = 0;
        // FOUND BY tools/check_reset_clears.py ON ITS FIRST RUN, and it is the same fault as the
        // absence below: a latch the loader restores and the reset never put back. Sockets are earned
        // by conquest and depth, both of which a new game has none of — but the latch is floored at
        // what the account was WEARING, so a reset handed a starter three keystone sockets.
        _keystoneSocketsEarned = 0;
        _hasSave = false;

        // Nothing pending: SeedNewGame below parks only the first-boot message.
        _pendingInventory = null;
        _pendingChests = null;
        _pendingRunLog = null;
        _pendingTreeCamera = null;   // a fresh game opens the tree on its first-open framing
        _pendingWorn = new Dictionary<GearSlot, string?>();
        _pendingChestsOpened = 0;
        _pendingFreeSocketUsed = false;   // a fresh game's first gem is free again

        // ── THE ABSENCE GOES WITH THE GAME. ──────────────────────────────────────────────────────
        //
        // Playtest 2026-09-09: "I reset the game, but I died at around wave 17 — normally I have no
        // way of getting there. The Welcome Back screen probably was not reset. Starting the game over
        // should lose all my AFK progress."
        //
        // Exactly right, and worse than it looks. The free offline resume is a HOST field, not a saved
        // one, and it was never cleared here: the reset deletes the file, mints a fresh world with no
        // conquests and no depth, rebuilds every screen — and then the very next descent still opened
        // at the wave the DELETED account's absence had reached. The depth cap that keeps the resume
        // honest is applied once, when a save loads, so a reset never re-asked it; and the spend
        // counter compares against the new screen's run count, which the rebuild had just put back to
        // zero, so the resume was not even spent after the first descent. A new game inherited an old
        // account's night of hunting and could not give it back.
        //
        // Everything an absence produced is cleared together: the panel, the flag that shows it, and
        // the three fields that carry the resume. The Warren screen's "while you were away" line reads
        // _welcome every frame, so it goes silent with it.
        _welcome = null;
        _showWelcome = false;
        _offlineResumeWave = 0;
        _offlineResumeRegion = "";
        _offlineResumeRuns = 0;

        // The teaching layer starts over with the game: nothing is explained, so every open tile
        // wears its NEW mark again, no tile has been visited, and no notice is waiting. Nothing
        // becomes DUE, because the mandatory intro is gone — the two intro flags reset only as the
        // rig's pose latch and the migration's record of a save that predates the intro.
        _rosterBaselined = false;
        _tourActive = false;
        _tourCard = Rectangle.Empty;
        _tourStep = 0;
        _introDecided = false;
        _introSeen = false;
        _pendingExplained = null;
        _pendingRevealed = null;
        _revealed = new HashSet<Activity> { Activity.Hunt };
        _revealSeeded = false;
        _explained.Clear();
        _visited.Clear();
        _rosterNews = false;
        _noticeQueue.Clear();
        _notice = default;
        _noticeTimer = 0f;
        // The click that confirmed the reset may have closed a banner in the same frame, which
        // leaves _swallowInput latched TRUE — and the title screen's keys all read through it. The
        // recompute lives below the title branch, so it never runs there (review 2026-08-23).
        _swallowInput = false;
        _coach.Reset();
        _deathWasUp = false;   // a new career's first fall is its own; the old one's edge must not hush it
        // ...AND THE OPENING RUNS AGAIN, WHOLE. A new career is a new beginning in every sense: the
        // story is unread, the cursor is at nothing, the gift is ungranted, and the title's PLAY item
        // starts it over (ChooseTitleItem reads _hasSave, which this reset has just cleared).
        _opening.Reset();
        _openingWas = OpeningStage.NotStarted;
        _openingGrants.Clear();
        _pendingOpeningStage = 0;
        _prologueSeen = false;
        _prologueBeat = 0;
        _prologueClock = 0f;
        _welcomeGiftGranted = false;
        _tutorialsDone.Clear();
        _rewardsCredited = 0;
        _arrivalDwell = 0f;
        _openingBlaze = 0f;
        _openingCard = _openingButton = _prologueNext = _prologueSkip = Rectangle.Empty;
        _guidanceOff = false;
        _reportOpenedEver = false;
        _changedAfterFall = false;
        _retriedAfterChange = false;
        // THE INBOX: a new career has been told nothing, has opened nothing, and owes the chrome nothing.
        _inbox.Clear();
        _dispatchesOpenedEver = false;
        _dispatchArrivalOwed = false;
        _pendingInboxSeed = null;
        _inboxSeeded = false;
        // A NEW GAME HAS READ NO FILE, so it predates nothing and is never seeded as a veteran.
        _saveVersionSeen = SaveGame.CurrentVersion;
        _fallSnapshot = null;
        _retryFrom = null;
        _fallsSeen = 0;
        _dismissedGuide.Clear();
        _conquerMsg = "";
        _lockedMsg = "";
        _lockedTimer = 0f;

        SeedNewGame();               // the first-boot message
        _region = _world.RegionFarm(_activeRegion);
        BuildScreens();              // every screen re-made around the fresh state
        ApplyRestoredState();        // hands the (empty) pending state to the new screens
        _bootTimer = 7f;

        _showSettings = _showHelp = _showDispatches = false;
        _dispatchScroll = 0;
        _dispatchCursorKey = _dispatchOpenKey = null;
        _dispatchArrivalHeld = false;
        _showGear = _showTraining = _showMastery = _showForge = _showWarren = false;
        _showWorld = _showTraits = _showRoster = _showLoadout = _showVault = false;
        _showTitle = true;           // back to the title, which now offers BEGIN THE HUNT
        _titleCursor = 0;
        _sinceAutosave = 0f;
        _warrenAccum = 0f;
    }

    private float _warrenAccum;

    /// <summary>
    /// Advance the Warren's idle production in real time. Runs on every screen, every frame — the
    /// facilities keep producing while you fight elsewhere, which is what "idle" means.
    /// </summary>
    /// <remarks>
    /// This method also ticked the creature region-farms until 2026-08-24. Their screen was never
    /// reachable, so no farm was ever staffed and that loop skipped every region on every frame —
    /// retired with the creature subsystem. The Warren below is the whole live idle economy.
    /// </remarks>
    private void TickWarren(float dt)
    {
        _warrenAccum += dt;
        if (_warrenAccum < 1f) return; // production resolves in whole-second ticks
        var span = _warrenAccum;
        _warrenAccum = 0f;

        // The Warren's facilities produce every second, on every screen — Gleam and Dust into the
        // shared balances, Scrap and Essence into the Forge's material wallet.
        //
        // GATED ON THE WARREN BEING OPEN, which it was not. The gradual-unlock pass gated the Warren's
        // nav TILE on the first conquest but nothing gated its PRODUCTION, so a brand-new player was
        // paid by a building they had never been told existed and could not visit. That is the inverse
        // of this codebase's usual bug — not a feature that never runs, but one that runs before the
        // player has met it — and it made the Warren 87% of all Gleam income in the game from minute
        // zero. Now it is what it reads as: the reward for taking a region.
        if (!Unlocks.IsOpen(Activity.Warren, GuideUnlockFacts())) return;

        _warren.ConqueredRegions = _world.ConqueredIds.Count;   // conquest → a standing production bonus
        var w = _warren.Tick(span);
        if (w.Gleam > 0) _hunter.AddGleam((int)w.Gleam);
        if (w.Dust > 0) _dust.AddDust((int)w.Dust);
        if (w.Scrap > 0) _hunter.AddMaterial(Material.Scrap, (int)w.Scrap);
        if (w.Essence > 0) _hunter.AddMaterial(Material.Essence, (int)w.Essence);
    }

    /// <summary>
    /// Draw the WARREN nav destination: the facility-production dashboard.
    /// </summary>
    /// <remarks>
    /// The dashboard's own clicks — and with them the spend + <see cref="Warren.Upgrade"/> — are resolved
    /// in UPDATE now (2026-09-12, ADR-006: draw must not consume input), so this only feeds the model and
    /// draws. Gleam and Dust still spend from their real balances, in UpdateWarren.
    /// </remarks>
    private void DrawWarren()
    {
        FeedWarren();
        _warrenScreen.Draw(_batch, PageCursor);
    }

    /// <summary>
    /// Hand the dashboard the state it renders and decides against.
    /// </summary>
    /// <remarks>
    /// Called from BOTH halves of the frame. Game1's Update returns early while a host modal is up
    /// (<c>if (_showSettings || _showHelp)</c>), so on those frames DrawWarren runs and the Warren's
    /// Update does not — a feed only in Update would paint the dashboard against a stale model.
    /// </remarks>
    private void FeedWarren()
    {
        _warren.ConqueredRegions = _world.ConqueredIds.Count;
        _warrenScreen.Warren = _warren;

        // One facility level per five waves of proven depth. Recomputed every frame it draws, so a
        // record set this session raises the ceiling without a restart. The arithmetic (and its floor
        // of 1) lives on the model, where a test pins it.
        var deepest = DeepestAnywhere();
        _warren.FacilityLevelCap = Warren.CapForDepth(deepest);
        _warrenScreen.DeepestWave = deepest;      // the strip explains the ceiling ONCE, not per card
        _warrenScreen.LastReturn = _welcome;      // what the last real absence actually paid

        _warrenScreen.GleamOwned = _hunter.Gleam;
        _warrenScreen.DustOwned = _dust.MemoryDust;
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        Save();
        DumpLessonLedger();
        base.OnExiting(sender, args);
    }

    /// <summary>
    /// RH_LESSON_LEDGER=1 — what onboarding actually did this session, on the way out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One line per lesson the session touched (eligible, shown, completed, muted, skipped) and one
    /// milestone line. It is printed rather than posted: this project has no analytics sink, and adding
    /// one to answer a playtest question would be a larger commitment than the question is worth. A
    /// tester runs with the dial set and reads the console; that is the whole mechanism.
    /// </para>
    /// <para>
    /// THE FIGURE WORTH READING IS THE LAST LINE. Not "did they finish the tutorial" — there is no
    /// tutorial to finish — but <c>ftue_loop_lived</c>: did this player open their first failure
    /// report, change something because of it, and go back down. That is the loop the game is, and a
    /// session that never reaches it is the failure onboarding exists to prevent.
    /// </para>
    /// </remarks>
    private void DumpLessonLedger()
    {
        // ONCE. The rig's exit path calls this and then calls Exit(), which calls OnExiting, which
        // calls it again — printing the whole ledger twice and making every count in it look doubled
        // to anybody reading the console rather than counting the rows.
        if (_ledgerDumped) return;
        if (Environment.GetEnvironmentVariable("RH_LESSON_LEDGER") is not { Length: > 0 }) return;
        _ledgerDumped = true;
        foreach (var row in _coach.Telemetry(LessonFactsNow())) Console.WriteLine(row);
        // THE INBOX, in one line: what waits unread, what the account knows, and whether this load
        // seeded it (a file from before the inbox). tools/check_boot.sh reads it on three hand-written
        // files — a migration lane that cannot fail is not a test.
        Console.WriteLine($"inbox\tunread={_inbox.Unread}\tknown={_inbox.Known.Count}\tseeded={_inboxSeeded}");
    }

    /// <summary>Has the ledger been printed? Both exit paths run on the way out of a rig shot.</summary>
    private bool _ledgerDumped;

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = new PixelFont(GraphicsDevice);
        _assets = new AssetLibrary(GraphicsDevice);
        // Audio is disabled during headless screenshot/CI runs (RH_SHOT set) — those machines may have
        // no sound device, and a screenshot never needs sound. Real runs get the full audio bank.
        _sound = new SoundBank(disable: Environment.GetEnvironmentVariable("RH_SHOT") is not null);
        _sound.SfxVolume = _sfxVolume / 100f;
        _sound.MusicVolume = _musicVolume / 100f;
        _ui = new UiKit(GraphicsDevice, _font, _assets);
        BuildScreens();
        // The canvas is now 1920x1080; screens still draw in 480x270 logical units (see ArtScale).
        _canvas = new RenderTarget2D(GraphicsDevice, CanvasWidth * ArtScale, CanvasHeight * ArtScale);
        ApplyRestoredState();
    }

    /// <summary>Construct every screen against the CURRENT core state.</summary>
    /// <remarks>
    /// Split out of <see cref="LoadContent"/> so START A NEW GAME can rebuild the whole screen layer
    /// around a fresh world: several screens capture references at construction (the character sheet
    /// holds the Forge), so replacing the core objects without
    /// replacing the screens would leave half the game serving the abandoned state.
    /// </remarks>
    private void BuildScreens()
    {
        _forge = new ForgeScreen(_ui);
        _forge.Sound = _sound;   // the reveal's landing ticks, the gem set, the successful upgrade
        _forge.AskBeforeScrap = _askBeforeScrap;
        _traitScreen = new TraitCollectionScreen(_ui);
        _roster = new RosterScreen(_ui);
        _vault = new VaultScreen(_ui);
        _vault.DrawItem = (sb, item, box) => _forge.DrawItemIcon(sb, item, box);   // one item renderer for every screen
        _loadoutScreen = new LoadoutScreen(_ui);
        _loadoutScreen.Sound = _sound;   // the weave's pick, and the seal a bound Vow presses
        _expedition = new HuntScreen(_ui);
        _expedition.Sound = _sound;   // the fight's hits, casts, deaths and the boss horn
        _masteryScreen = new MasteryScreen(_ui);
        _gear = new GearScreen(_ui, _forge);
        _gear.RestoreCompletedSets(_completedSetsLoaded);   // parked by LoadOrStartFresh, before this existed
        _training = new TrainingScreen(_ui);
        _warrenScreen = new WarrenScreen(_ui);
        _mapScreen = new MapScreen(_ui);
    }

    /// <summary>Apply whatever the load parked in _pending* fields — see <see cref="LoadOrStartFresh"/>.</summary>
    /// <remarks>
    /// Screens are constructed after Initialize(), so the loaded state waits in the _pending* fields
    /// until here. Also called by START A NEW GAME, whose pending state is simply empty.
    /// </remarks>
    private void ApplyRestoredState()
    {
        if (_pendingInventory is not null)
        {
            _forge.AddLoot(_pendingInventory);

            // Re-wear whatever was worn. Gear IS the power curve — losing it on reload would be a
            // silent, unexplained nerf the moment the player restarts the game.
            ItemInstance? Find(string? id) =>
                id is null ? null : _pendingInventory.FirstOrDefault(i => i.InstanceId == id);

            foreach (var (slot, id) in _pendingWorn)
                _hunter.RestoreWorn(slot, Find(id));
        }
        if (_pendingChests is not null) _forge.RestoreChests(_pendingChests);
        _forge.RestoreChestsOpened(_pendingChestsOpened);
        _forge.FreeSocketUsed = _pendingFreeSocketUsed;
        // The gems a returning player already holds are not news; only the NEXT one is announced.
        _gemsHeldLast = GemsHeld();
        // The keep-filter (TAKE ONLY, on the HUNT screen since 2026-08-23), seeded ONCE — the screen
        // owns it from here; the host reads it back on FilterDirty (UpdateExpedition).
        _vault.KeepMinTier = _chestKeepMinTier;
        _vault.AutoSellOn = _autoSellOn;
        _vault.KeepSlots.Clear(); foreach (var sl in _chestKeepSlots) _vault.KeepSlots.Add(sl);
        if (_pendingRunLog is not null) _expedition.Log.Restore(_pendingRunLog);
        if (_pendingTreeCamera is { } cam) _masteryScreen.RestoreCamera(cam.Zoom, cam.PanX, cam.PanY);
        SeedExplained();
        SeedOpening();
    }

    /// <summary>
    /// Decide what this player already knows, now that the screens exist to ask.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs from <see cref="ApplyRestoredState"/>, AFTER the bag is restored, because the unlock facts
    /// count wearable items and chests — read here at Initialize time, GEAR, VAULT and FORGE would look
    /// shut on a save that has all three open, and a returning player would find NEW marks on screens
    /// they have used for hours. The rule itself is <see cref="Onboarding.SeedExplained"/>.
    /// </para>
    /// <para>
    /// A save written before the explained list could be believed is recognised there BY ITS VERSION
    /// (<c>Onboarding.FirstVersionWithExplainedList</c>), and every open screen is marked read. It then
    /// saves at the current version, so the migration happens once and not on every load.
    /// </para>
    /// <para>
    /// UNDER THE CAPTURE RIG everything is explained unless a fixture says otherwise: every fixture
    /// grants the facts that open its screen, and a banner over the thing being photographed is the
    /// old modal panel's mistake in a new coat. <c>RH_SHOT_EXPLAIN=Stats,Map</c> leaves those two owed,
    /// which is how a tour is captured over a screen a fixture has dressed; the <c>tour</c> fixture
    /// owes its own screen without being asked.
    /// </para>
    /// </remarks>
    private void SeedExplained()
    {
        // THE RAIL'S REVEALED SET, seeded from the save and from the gates — never announced: a
        // returning player has used these screens, and an older save (no list) keeps every screen its
        // facts open. From here on Update grows it through Reveal.Newly, which is where the notice is.
        _revealed = Reveal.Restore(_pendingRevealed ?? new List<string>(), GuideUnlockFacts());
        // RH_SHOT_RAIL=all: the whole rail, for the fixture that proves an advanced account's eleven
        // tiles still fit at 150 % in a small window — a rig affordance, never a game path.
        if (Environment.GetEnvironmentVariable("RH_SHOT_RAIL") == "all")
            foreach (var a in Enum.GetValues<Activity>()) _revealed.Add(a);
        _pendingRevealed = null;
        _revealSeeded = true;

        // ── AND THE INBOX, FOR A SAVE THAT PREDATES IT. ─────────────────────────────────────────
        //
        // A file from before the inbox has been TOLD nothing, and it has lived everything its facts
        // carry — conquests, traits, keystones, Vows, quests, hunters, sets, the screens it has open.
        // Loaded literally, the first producers would post twenty letters about a life already led.
        // So its accomplishments are marked KNOWN — no rows, no timestamps, nothing to read — and only
        // what happens from here on is news. Here and not in LoadOrStartFresh because the revealed set
        // is one of the facts, and it is only real once the Forge exists.
        //
        // THE FILE'S VERSION DECIDES (Dispatches.FirstVersionWithInbox), never the inbox's emptiness:
        // a file at 8 or above with an empty inbox is a player who has honestly been told nothing yet.
        if (_pendingInboxSeed is { } seedFrom && _saveVersionSeen < Dispatches.FirstVersionWithInbox)
            _inboxSeeded = Dispatches.SeedKnown(seedFrom, _revealed, _inbox);
        _pendingInboxSeed = null;

        _explained.Clear();
        if (Environment.GetEnvironmentVariable("RH_SHOT") is not null)
        {
            var owed = (Environment.GetEnvironmentVariable("RH_SHOT_EXPLAIN") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (Environment.GetEnvironmentVariable("RH_SHOT_MODE") == "tour" && ShotTab is { } tab)
                owed.Add(Onboarding.ScreenKey(tab));
            // `gemtour` dresses the Forge with gems and owes the first-gem lesson, the way `tour` owes
            // a screen's own.
            if (Environment.GetEnvironmentVariable("RH_SHOT_MODE") == "gemtour") owed.Add(Onboarding.GemTourKey);
            foreach (var a in Enum.GetValues<Activity>())
                if (!owed.Contains(Onboarding.ScreenKey(a))) _explained.Add(Onboarding.ScreenKey(a));
            for (var slot = 2; slot <= Build.SkillSlots; slot++)
                if (!owed.Contains(Onboarding.SlotKey(slot))) _explained.Add(Onboarding.SlotKey(slot));
            if (!owed.Contains(Onboarding.GemTourKey)) _explained.Add(Onboarding.GemTourKey);
            return;
        }

        var facts = GuideUnlockFacts();
        foreach (var key in Onboarding.SeedExplained(facts, _introSeen, _pendingExplained ?? new List<string>(),
                                                     _forge.FreeSocketUsed, _saveVersionSeen))
            _explained.Add(key);
        _pendingExplained = null;

        // ── AND THE FALL LOOP, FOR A SAVE THAT PREDATES IT. ─────────────────────────────────────
        //
        // The three fall-loop facts are the only onboarding state the save keeps, and a file written
        // before they existed carries none of them — so a player with three regions conquered would be
        // met by READ THE LOG, which is the game announcing it has not been watching.
        //
        // THE FILE'S VERSION DECIDES, never the progression on its own. "Conquered a region and has
        // not opened a report" is also a brand-new player on this build who reloaded before their
        // first fall, and seeding them cost them the lesson the whole first session is for.
        if (!_reportOpenedEver && OnboardingLessons.SeedFallLoopAsLived(LessonFactsNow(), _saveVersionSeen))
        {
            _reportOpenedEver = _changedAfterFall = _retriedAfterChange = true;
            _fallsSeen = _expedition?.Log.Entries.Count ?? 0;
        }

        // ── AND THE RETRY, FOR A CAREER THAT CHANGED ITS BUILD AND THEN RELOADED. ────────────────
        //
        // _retryFrom is session-only — it is never written to the save — so a career that fell, changed
        // its build, saved, and reloaded before its next descent carries ChangedAfterFall forward but
        // loses the number WatchTheFallLoop compares RunsStarted against, and RetriedAfterChange could
        // never latch. RunsStarted always reopens at zero on the fresh HuntScreen BuildScreens has
        // already built by the time this runs, so seeding _retryFrom with that same zero makes the very
        // next StartRun — the descent the game is about to begin on its own — the retry the milestone
        // is waiting for.
        if (_changedAfterFall && !_retriedAfterChange) _retryFrom = _expedition?.RunsStarted ?? 0;

        // The one-time migration: a player from before the intro is past it. Marked so the seeding
        // above does not repeat on every launch, quietly marking screens they opened but never read.
        if (!_introSeen && !Onboarding.IntroDue(_deepestEver, false)) _introSeen = true;
    }

    /// <summary>Frames survived under RH_BOOTCHECK. See <see cref="BootCheck"/>.</summary>
    private int _bootCheckFrames;

    /// <summary>
    /// RH_BOOTCHECK: boot against the REAL save, prove it, and exit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The save-load path is the one path nothing could verify. LoadOrStartFresh returns immediately
    /// when RH_SHOT is set — deliberately, so a capture can never entangle with real progress — which
    /// means every screenshot and every gate in this repo was green while the game crashed on startup
    /// for anyone with a save. That is not a gap in the rig, it is a hole the rig's own safety created.
    /// </para>
    /// <para>
    /// So this mode does what a capture must not: it loads the real save. It is safe to do because it
    /// can never write one — RH_BOOTCHECK is on the same guard in <c>Save()</c> that RH_SHOT is, at the
    /// single write site. It reports whether a save was actually restored rather than only that nothing
    /// threw, because "no crash" and "no save file" look identical from outside and the second one
    /// would pass a check meant to prove the first.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Every screen, in the order the boot check walks them.
    /// </summary>
    /// <remarks>
    /// Starting is not the same claim as WORKING. A crash that needs real save data to reach — an
    /// empty roster, a worn item whose id no longer resolves, a run log written by an older format —
    /// hides behind a title screen that comes up perfectly, and the screenshot fixtures cannot find it
    /// because each one seeds its own data instead of loading yours. Walking every screen with the real
    /// save loaded is the difference between "it launched" and "you can open the things you were about
    /// to open".
    /// </remarks>
    private (string Name, Action Open)[] BootCheckScreens() => new (string, Action)[]
    {
        ("title", () => _showTitle = true),
        ("hunt", () => _showTitle = false),
        ("gear", () => _showGear = true),
        ("stats", () => { _showGear = false; _showTraining = true; }),
        ("build", () => { _showTraining = false; _showMastery = true; }),
        ("weave", () => { _showMastery = false; _showLoadout = true; }),
        ("forge", () => { _showLoadout = false; _showForge = true; }),
        ("warren", () => { _showForge = false; _showWarren = true; }),
        ("map", () => { _showWarren = false; _showWorld = true; }),
        ("traits", () => { _showWorld = false; _showTraits = true; }),
        ("roster", () => { _showTraits = false; _showRoster = true; }),
        ("help", () => { _showRoster = false; _showHelp = true; }),
        ("settings", () => { _showHelp = false; _showSettings = true; }),
        ("dispatches", () => OpenDispatches()),
        ("hunt again", () => _showDispatches = false),
    };

    private int _bootCheckScreensSeen;

    private void BootCheck()
    {
        if (Environment.GetEnvironmentVariable("RH_BOOTCHECK") is null) return;

        _bootCheckFrames++;

        // How long each lap leaves the fight running before touring the screens again.
        const int FightFramesPerLap = 450;

        // RH_BOOTCHECK=<frames> lengthens the soak; anything non-numeric (including "1") keeps the
        // default. A gate wants ten seconds; hunting a crash that only shows after minutes of real
        // ticking wants thousands of frames, and that is a different job for the same machinery.
        var soakFrames = int.TryParse(
            Environment.GetEnvironmentVariable("RH_BOOTCHECK"), out var want) && want > 600 ? want : 600;

        // A CEILING, so a stuck walk fails instead of hanging. When this method sat lower in Update it
        // stopped being reached at all while the title was up, and the run simply never ended — a
        // ten-minute timeout killed it and the failure looked like a slow machine rather than a broken
        // harness. A check that can hang has a failure mode nobody reads.
        var soakCeiling = soakFrames + 1800;
        if (_bootCheckFrames > soakCeiling)
        {
            Console.WriteLine($"BOOT STUCK — {_bootCheckScreensSeen} screens reached, then no progress.");
            Environment.Exit(2);
        }

        // Settle first, then walk. The opening frames restore the save and run the offline catch-up;
        // switching screens under that would be testing the transition rather than the screen.
        const int Settle = 45, PerScreen = 12;
        if (_bootCheckFrames < Settle) return;

        var screens = BootCheckScreens();
        var elapsed = _bootCheckFrames - Settle;

        // A CYCLE, not a single tour. Touring the screens once at the start only ever draws them
        // against the state the save arrived in — an empty merge tray, no chests, a bag that has not
        // grown. The interesting failures are the ones that need the game to have HAPPENED first: the
        // Forge after a chest drops, the log after a run ends, the map after a conquest. So each lap
        // walks every screen and then leaves the fight running for a while, and a long soak is many
        // laps, redrawing everything against state that keeps moving.
        var lapLength = screens.Length * PerScreen + FightFramesPerLap;
        var inLap = elapsed % lapLength;
        var step = inLap / PerScreen;

        if (step < screens.Length)
        {
            if (inLap % PerScreen == 0)
            {
                _bootCheckScreensSeen++;
                screens[step].Open();
            }
            return;
        }

        // Make sure the lap ends back in the fight, whatever the last screen visited was.
        if (inLap == screens.Length * PerScreen) screens[1].Open();

        // THEN SIT IN THE FIGHT AND LET IT RUN. Drawing the hunt screen for twelve frames proves it
        // renders; it does not prove a wave can resolve. Clearing waves is where the loot roll, the
        // chest drop, the level-up and the conquest counter all fire, and those run against the
        // player's real numbers — which is the combination no fixture reproduces and no capture holds
        // still long enough to reach. This is the part of the check that touches the actual game.
        if (elapsed < soakFrames) return;

        Console.WriteLine(_hasSave
            ? $"BOOT OK — save loaded, hunter level {_hunter.HunterLevel}, "
              + $"{_bootCheckScreensSeen} screens drawn, deepest wave {_deepestEver}"
            : $"BOOT OK — no save present, started fresh, {_bootCheckScreensSeen} screens drawn");
        // RH_UI_TEXT=1: every label the boot's own sweep of all fourteen screens could not fit. This is
        // where the truncation ledger is worth the most — the capture path photographs one screen, and
        // this one draws every screen the game has.
        UiTextLedger.Dump();
        DumpLessonLedger();   // RH_LESSON_LEDGER=1: what onboarding said during the boot soak
        Exit();
    }


    // ══════════════════════════════════════════════════════════════════════════════════════════
    protected override void Update(GameTime gameTime)
    {
        _keys = Keyboard.GetState();
        // THE OPENING RIG (Game1.OpeningRig.cs, dev only): the trace samples what the last frame did,
        // and the autoplayed hand — when it is on — IS the mouse this frame, fed to the same field the
        // real one is. Inert without RH_OPENING_*.
        if (OpeningRigOn) OpeningRigFrame();
        _mouse = Autoplay ? _autoMouse : Mouse.GetState();
        ReadCursor();
        // PRESSED IS A STATE NO CAPTURE COULD POSE. The mouse button comes from the real device, so
        // every screen's pressed face — UiKit.Button's own, and the seven screens that draw their own
        // to match it — was invisible to the rig, and five of the eleven polish agents reported it as
        // unphotographable. RH_SHOT_HELD=1 holds the button down for the whole capture, so the state
        // can be looked at on any screen with the cursor parked on any control (RH_SHOT_MOUSE /
        // RH_SHOT_PAGE_MOUSE). Rig-only, and it feeds the same field the real mouse does rather than
        // opening a second path.
        UiKit.MouseHeld = _mouse.LeftButton == ButtonState.Pressed || (RigActive && ShotHeld);
        UiMotion.Reduced = ReducedMotion;
        UiMotion.Tick((float)gameTime.ElapsedGameTime.TotalSeconds);
        ReserveNoticeLane();
        TickChromeMotion((float)gameTime.ElapsedGameTime.TotalSeconds);

        // Latch the click EDGE once per frame, here, before anything reads it. The edge lives for
        // exactly one Update, and _prevMouse is overwritten in Latch() at the end of Update — so a
        // property that recomputed the edge would read false during the following Draw(), which is
        // where every screen's action buttons (Forge Sell/Merge, Farm Hatch/Assign/Evolve, …) are
        // detected. Latching into a field keeps the click live through this frame's Draw.
        _clicked = _mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        // ...and with it the "this edge opened a modal" mark, which lives exactly as long as the edge.
        _modalOpenedNow = false;

        // THE BOOT CHECK SWITCHES SCREENS HERE, at the top, and the position took two tries to get
        // right. At the END of Update it flipped a flag after the `if (_showX)` blocks that hand each
        // screen its data, so the next Draw ran against null fields and it reported a
        // NullReferenceException in TrainingScreen that a player cannot produce. Beside the hotkeys it
        // was better but still wrong: while the title is up, Update returns long before reaching them,
        // so the walk never left the first screen and the run hung until it was killed. Ahead of every
        // early return, a flag set here is fed by the same blocks that feed a keypress, on the same
        // frame. A smoke test that does not take the player's path only generates false alarms.
        BootCheck();

        // The RIGHT click edge, latched identically. It opens the inventory's item menu — the one place
        // a player should be able to say "wear this / upgrade this / break this up" without first
        // finding the screen that owns the verb.
        _rightClicked = _mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;

        // Mouse wheel, latched the same way. One notch is 120; screens scroll by ROWS, not pixels.
        // Nothing in the game read the wheel at all before this, which left long lists unreachable.
        _wheel = (_mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue) / 120;

        var dtMs = (int)gameTime.ElapsedGameTime.TotalMilliseconds;
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Settings-chrome timers, ticked on every screen INCLUDING the title (the panel opens there
        // too): the armed state of START A NEW GAME, and the COPIED confirmation line.
        if (_resetArmTimer > 0f) _resetArmTimer = Math.Max(0f, _resetArmTimer - dt);
        if (_feedbackToastTimer > 0f) _feedbackToastTimer = Math.Max(0f, _feedbackToastTimer - dt);
        // The hover-tip clock only ever counts UP here; DrawSettings resets it whenever the
        // hovered row changes, so the delay is measured on whichever screen the panel is over.
        _tipTimer = Math.Min(_tipTimer + dt, 10f);   // capped: an uncapped float stalls after days
        // Escape is claimed per frame, not per handler — see _settingsEscSpent.
        _settingsEscSpent = false;
        // A closed panel holds no dropdown open and no slider mid-drag. The drag latch is the
        // quiet failure: Esc mid-drag must still write the prefs the drag was setting.
        if (!_showSettings)
        {
            CloseDropdown();
            _tipKey = "";
            if (_dragSlider != 0) { _dragSlider = 0; SaveDisplay(); }
        }
        else if (_settingsDropdown != 0)
        {
            // ── AN OPEN LIST OWNS THE KEYBOARD. Up/Down walk it, Enter takes the row under the
            //    cursor, Escape shuts the LIST — one layer, not the whole panel (see _settingsEscSpent).
            //    Raw edges rather than Pressed(): this runs before _swallowInput is recomputed for the
            //    frame, and the arrow keys must not also reach the title menu's own cursor below.
            bool Edge(Keys k) => _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);
            if (Edge(Keys.Up)) _dropMove--;
            if (Edge(Keys.Down)) _dropMove++;
            if (Edge(Keys.Enter)) _dropCommit = true;
            if (Edge(Keys.Escape))
            {
                CloseDropdown();
                _settingsEscSpent = true;
                _sound.Play("sfx_click", 0.6f);
            }
        }

        // START A NEW GAME, confirmed on the settings panel last frame. Acted on HERE, between
        // frames, because it replaces every screen object the frame that requested it was still
        // drawing with.
        if (_wantsNewGame) { _wantsNewGame = false; StartNewGame(); }

        // ── Title screen — a real front-end instead of dropping straight into a fight. ──────────
        if (_showTitle)
        {
            // DEV: when capturing a gameplay screenshot, skip straight past the title (and open a screen).
            // ShotMode, not the raw variable: `tour` arrives here as the fixture of the screen it tours.
            var sm = ShotMode switch
            {
                // Screen-named aliases (UX V2 P0.2). The handlers below keep their older names so
                // capture.sh's mode list and every baseline capture still work.
                "loadout" => "weave", "mastery" => "buildtree", "traits" => "dust", "gear" => "character",
                "training" => "stats", "hunt" => "fight", "build" => "buildtree",
                "specialise" => "attune", "specialised" => "attuned",
                var other => other,
            };
            // `telegraph`, `combat` and `boss2` posed the manual-combat screen for screenshots. That
            // screen is gone, so they had nothing to pose.
            if (sm is "vfx" or "forge" or "farm" or "dust" or "world"
                or "region2" or "region3" or "conquered" or "mapdeep" or "maplocked" or "help" or "expedition" or "fight" or "fightshield" or "welcome" or "boss" or "bossdebug"
                or "banked" or "lootforge" or "settings" or "settingsfull" or "settingsopen" or "vow" or "runlog" or "reforge" or "build" or "buildtree" or "buildzoom" or "character" or "itemmenu" or "stats" or "trainingpoor" or "trainingreset" or "warren" or "map" or "rig" or "corrupted" or "corruptedboss"
                or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "fightfade" or "fightarrive" or "fightcooldown" or "fightaura" or "fightflash"
                or "fightstatus" or "fightfive" or "fightshieldbroken" or "fightmulti" or "fightinspect"
                or "roster" or "rosterlocked" or "rosterswitch" or "warrenready" or "warrenfresh" or "warrenlocked" or "weave" or "weavefresh" or "vault" or "vaultfirst" or "vaultfilter" or "attune" or "attuned" or "trader"
                or "keystonenotice"
                or "dispatches" or "dispatchesempty" or "dispatchesunread" or "dispatcheshover" or "chestdispatch"
                or "vaultempty" or "vaultemptyfilter" or "vaultsell" or "vaultmany" or "forgeempty"
                or "gemtour" or "intro" or "typespec" or "vfxdebug")
            {
                _showTitle = false;
                // RH_SHOT_HUNTER=<character id> poses the fixture on a DIFFERENT champion.
                //
                // The VFX contract measures every effect against the hunter's VISIBLE silhouette, and
                // the silhouettes differ enormously: THE OATHBOUND draws 162 px wide and QUIVER 373 px,
                // a 2.3x spread, while all ten draw within 4 px of the same HEIGHT. So a capture of one
                // hunter proves nothing about the placement of anything measured against a width —
                // which is exactly why the brief's §71 asks for two silhouettes and why the audit found
                // its named pair (THE SEEKER 267, THE MAGPIE 302) differ by only 13 %.
                //
                // Restore is the roster's own entry point; it unlocks the champion being restored, so
                // the fixture can pose one the save has not earned without a second unlock path.
                //
                // An id the roster does not know ABORTS. It used to fall through the `&&` and leave the
                // default champion in the chair, so a typo produced a perfectly plausible screenshot of
                // the wrong hunter — and a cross-silhouette proof that had photographed one silhouette
                // twice. A fixture that cannot pose what it was asked for must not return an image.
                if (Environment.GetEnvironmentVariable("RH_SHOT_HUNTER")?.Trim() is { Length: > 0 } shotHunter)
                {
                    var posed = CharacterRoster.Find(shotHunter)
                        ?? throw new InvalidOperationException(
                            $"RH_SHOT_HUNTER='{shotHunter}' is not a character id. Known: " +
                            string.Join(", ", CharacterRoster.All.Select(c => c.Id)) + ".");
                    _characters.Restore(posed.Id, _characters.SaveQuests(),
                                        _characters.SaveUnlocked().Append(posed.Id));
                }
                // (`expedition` and `vow` used to seed the retired creature den here; since its removal
                // they pose nothing beyond skipping the title.)
                // lootforge: seed the Forge with a spread of loot so it can be screenshotted with content
                // (the idle loop drops items only on boss waves, which a 1-second shot won't reach).
                // RH_SHOT_T poses the chest reveal at a chosen instant — the shake, the burst, the card.
                // Read BEFORE the fixture runs; applied after it opens a chest, or the open overwrites it.
                if (sm is "lootforge" or "chestdispatch" or "vaultfirst" && Environment.GetEnvironmentVariable("RH_SHOT_T") is { } rt
                    && float.TryParse(rt, System.Globalization.CultureInfo.InvariantCulture, out var revealT))
                    _pendingRevealPose = revealT;

                if (sm is "lootforge" or "chestdispatch")
                {
                    _showForge = true;
                    var rar = new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
                    var types = new[] { ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.Material, ItemBaseType.AbilityFocus };
                    var seed = new List<ItemInstance>();
                    for (var i = 0; i < 10; i++)
                        seed.Add(new ItemInstance
                        {
                            InstanceId = $"dev{i}", BaseType = types[i % types.Length],
                            Rarity = rar[i % rar.Length], SellValue = 12 + i * 7,
                        });
                    // A reforged charm carrying SIPHON — poses the longest combo line, "NEEDS
                    // TRANSFORMATION IN YOUR BUILD", the widest string on this panel.
                    seed.Add(new ItemInstance
                    {
                        InstanceId = "dev_siphon", BaseType = ItemBaseType.Charm, Rarity = Rarity.Legendary,
                        SellValue = 200, Element = Source.Nature, EnchantOverride = EnchantKind.Siphon,
                    });
                    // And a KEYSTONE combo, because the combo line no longer only asks about Forms. This
                    // one poses the other branch of that check — a requirement the build cannot satisfy
                    // by equipping a skill, only by socketing a keystone.
                    seed.Add(new ItemInstance
                    {
                        InstanceId = "dev_fervour", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary,
                        SellValue = 240, Element = Source.Body, EnchantOverride = EnchantKind.Fervour,
                    });
                    // Two loose gems, so the SOCKETS strip poses with supply on the bench.
                    seed.Add(new ItemInstance
                    {
                        InstanceId = "dev_gem1", BaseType = ItemBaseType.Gem, Rarity = Rarity.Rare,
                        SellValue = 50, ItemLevel = 5,
                    });
                    seed.Add(new ItemInstance
                    {
                        InstanceId = "dev_gem2", BaseType = ItemBaseType.Gem, Rarity = Rarity.Epic,
                        SellValue = 80, ItemLevel = 8,
                    });
                    _forge.AddLoot(seed);
                    TellForgeTheBuild(ComposeBuild());
                    _forge.DevFocus("dev_siphon");   // the SIPHON charm — poses the longest combo line on the bench
                    // Stock every tier so the reforge/refine buttons pose live, not greyed.
                    _hunter.AddMaterials(500);   // SCRAP
                    _hunter.AddMaterial(Material.Essence, 200);
                    _hunter.AddMaterial(Material.Core, 120);
                    _hunter.AddMaterial(Material.Crystal, 40);
                    // A pile of chests of varying grades, so the chest bar + OPEN buttons pose with content.
                    foreach (var cr in new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary, Rarity.Rare })
                        _forge.AddChest(new Chest { Rarity = cr, Tier = 8, Element = Source.Nature });
                    // WHO IS WEARING IT, before the chest is opened. The reveal's EQUIP button asks the
                    // champion's class whether the drop is wearable, and the per-frame push in
                    // UpdateExpedition has not run yet at fixture time — so without this the button
                    // could only ever photograph its refused state.
                    _forge.Wearer = _characters.Active;
                    _forge.FavouredClass = _characters.Active.Class;
                    // RH_SHOT_REVEAL_WEARABLE poses the reveal over a piece this champion CAN wear, so
                    // the EQUIP button's live readings (UPGRADE / SIDEGRADE, and the empty slot) have a
                    // fixture. Without it the roll decides, and four in five class-locked rolls refuse.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_REVEAL_WEARABLE") is { Length: > 0 })
                        _forge.DevRevealItem(new Chest { Rarity = Rarity.Epic, Tier = 8, Element = Source.Nature },
                                             new ItemInstance
                                             {
                                                 InstanceId = "dev_reveal_wear", BaseType = ItemBaseType.Boots,
                                                 Rarity = Rarity.Epic, SellValue = 180, Element = Source.Nature,
                                                 ItemLevel = 8,
                                             });
                    else _forge.DevOpenOneChest(_hunter);   // pose the chest-open REVEAL burst
                    // AFTER the open, never before: opening a chest resets the reveal clock, so a pose
                    // applied earlier is silently overwritten and every RH_SHOT_T lands on t=0.
                    if (_pendingRevealPose is { } rp) _forge.DevPoseReveal(rp);
                }

                if (sm is "build" or "buildtree" or "buildzoom" or "attune" or "attuned")
                {
                    _showMastery = true;

                    // Pose a partly-walked tree so the shot shows taken / takeable / locked states, a
                    // bridge with only one side satisfied, and a mastery still out of reach.
                    // Depth per region, not a point total: the host re-derives earned points from
                    // MasteryPoints.Total every frame. Three regions at depth 100 pay 3 × ⌊√100 × 0.9⌋ = 27;
                    // the Resonance minors and notables below cost 21, so SIX points stay AVAILABLE and a
                    // takeable node is actually photographed (UX V2 P1.5 — the old depth-35 pose paid 15
                    // against 21 spent, a state the game cannot produce, and no capture ever showed a node
                    // in its takeable light).
                    foreach (var def in Regions.All.Take(3)) _world.RegionFarm(def.Id).RestoreBestDepth(100);
                    _mastery.SetEarned(27);

                    // FIVE auto-skill cards, not four. The fifth slot is the thing this screen was
                    // silently unable to draw, and a fixture that poses the easy case would not have
                    // caught it — nor would it catch the card row walking off the panel again, which is
                    // what a fixed pitch of 182 did the moment a fifth card existed.
                    //
                    // The world INPUTS. (Conquest does not touch the mastery total above — that reads
                    // BestDepth/5 per region — so the 24 this fixture means still holds.)
                    _world.RestoreConquered(Regions.All.Select(r => r.Id));
                    _world.RestoreCorruption(16);
                    // (This used to buy socket_2 and weave_5 on the Memory tree. Sockets are derived
                    // from the conquests just restored above, and the fifth skill slot is removed.)
                    ApplySkillCapacity();
                    // SIX passives, which is the most the panel will list (it Takes 6). Same reasoning as
                    // the fifth skill card above: the passives list is variable-length and the RESONANCE
                    // heading below it used to be pinned at a fixed offset, so a full list was drawn
                    // straight through the heading. A fixture stopping at three could never show that.
                    // By BRANCH, not by a hard-coded id list: nine of the eleven ids the old list
                    // named retired at the 2026-08-30 re-axe, Take() no-ops on an unknown id, and the
                    // "six passives" pose had quietly become an empty list. Walking a branch's Minors
                    // and Notables in catalog order respects prereqs the same way the ATTUNE fixture does.
                    // THE PATH TREE (2026-09-06): a route is walked in order, skill nodes included, so
                    // the walk repeats until nothing more in RESONANCE below its greaters can be taken.
                    bool walked;
                    do
                    {
                        walked = false;
                        foreach (var wn in MasteryCatalog.Nodes.Where(
                                     x => x.Branch == Branch.Resonance && x.Link is null
                                          && x.Kind is MasteryKind.Minor or MasteryKind.Notable or MasteryKind.SkillRoad))
                            if (!_mastery.IsTaken(wn.Id) && _mastery.Take(wn.Id)) walked = true;
                    } while (walked);

                    // BUILDTREE frames the whole tree. The TOUR of this screen is, by definition, a first
                    // visit, so it is posed on the first-open framing a real first visit gets — the
                    // card's light has to fall on what the player actually sees.
                    if (sm == "buildtree")
                    {
                        if (Environment.GetEnvironmentVariable("RH_SHOT_MODE") == "tour") _masteryScreen.DevOpenTreeFirstVisit();
                        else if (Environment.GetEnvironmentVariable("RH_SHOT_FRONTIER") == "1") _masteryScreen.FrameFrontier();   // the normal reopen
                        else _masteryScreen.DevOpenTree();
                    }

                    // RH_SHOT_RESPEC=1 poses the ARMED respec — the one state on this screen that says
                    // what a decision will cost before it costs it (BRIEF sec.19). It exists only
                    // between two presses of one button and a capture never clicks, so without this
                    // dial nobody could ever look at the warning, at any profile. Two roads walked and
                    // both their skills woven, so the list it raises has real names in it.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_RESPEC") == "1")
                    {
                        _mastery.SetEarned(9999);
                        _mastery.RestoreTaken(_mastery.Taken.Concat(new[] { "road_hammer", "road_snare" }).ToList(), repair: false);
                        if (_loadout.IndexOfSkill("snare_jaws") < 0)
                        {
                            var extra = _loadout.AddSkill();
                            if (extra >= 0) _loadout.SetSkill(extra, "snare_jaws");
                        }
                        // The three the arming READS. The host wires these in Update, which has not run
                        // yet — without them DevArmRespec asks a fresh empty tree what it would cost and
                        // is told "nothing", which is how this pose came back unarmed the first time.
                        _masteryScreen.Mastery = _mastery;
                        _masteryScreen.Loadout = _loadout;
                        _masteryScreen.Character = _characters.Active;
                        _masteryScreen.DevArmRespec();
                    }
                    // RH_SHOT_NODE=<id> pins a node in the inspector, so a Notable, a Specialisation, a road
                    // node and a locked node can each be photographed read (UX V2 P1.5).
                    if (Environment.GetEnvironmentVariable("RH_SHOT_NODE") is { } pinNode) _masteryScreen.DevPin(pinNode);

                    // ATTUNE poses THE ATTUNEMENT ceremony; ATTUNED poses the tree after sealing
                    // (the hex panel gold, the discipline live). Both walk the REAL path — Weight's
                    // lower rings then the Specialisation — so the pose can't drift from the rules.
                    if (sm is "attune" or "attuned")
                    {
                        _mastery.SetEarned(80);
                        foreach (var k in new[] { MasteryKind.Minor, MasteryKind.Notable })
                            foreach (var wn in MasteryCatalog.Nodes.Where(x => x.Branch == Branch.Resonance && x.Kind == k))
                                _mastery.Take(wn.Id);
                        _mastery.Take("spec_strike");
                        // ONE STYLE ROAD WALKED, so a capture shows both sides of the skill gate:
                        // HAMMER's PRESS is learned and the other five roads are not.
                        _mastery.Take("road_hammer");
                        _masteryScreen.DevOpenTree();
                        if (sm == "attune") _masteryScreen.DevSpecialise(Style.Hammer);
                    }
                    // The same tree at a working zoom. Node art is thirty pixels across in the
                    // overview, where a capture can only prove that something was drawn. RH_SHOT_ZOOM
                    // picks the scale, so the rim (capstones) and the hub (minors) are both reachable
                    // from the capture script without editing this file again. With NO dial it poses
                    // the FIRST-OPEN framing — the centre and ring 1 — which is the one a player sees.
                    if (sm == "buildzoom")
                    {
                        if (float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_ZOOM"),
                                           System.Globalization.CultureInfo.InvariantCulture, out var dz))
                            _masteryScreen.DevOpenTree(dz);
                        else _masteryScreen.DevOpenTreeFirstVisit();
                    }

                }

                // ITEMMENU reuses the CHARACTER fixture wholesale and then opens the menu on it —
                // seeding a second, different bag would pose a screen the game never shows.
                if (sm is "character" or "itemmenu")
                {
                    _showGear = true;
                    _gear.Loadout = _loadout;
                    _gear.Mastery = _mastery;
                    _gear.SkillLevels = _skillProgress;
                    _gear.DiscoveredKeystones = _keystoneMenu;
                    _gear.KnownVows = _vowMenu;
                    // Seed gear so the bag and the three worn slots pose with content, gleam so the train
                    // buttons are live, and a few trained ranks so the values aren't all at base.
                    var rar = new[] { Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
                    var types = new[]
                    {
                        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Helm,
                        ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
                    };
                    var seed = new List<ItemInstance>();
                    // The worn set is the starter's own class (WANDERER), so all eight sit on the doll.
                    for (var i = 0; i < types.Length; i++)
                        seed.Add(new ItemInstance
                        {
                            InstanceId = $"cd{i}", BaseType = types[i],
                            Rarity = rar[i % rar.Length], SellValue = 20 + i * 9, Element = Source.Nature,
                            ItemLevel = 8 + i * 6,
                            Class = ItemClasses.IsClassLocked(types[i]) ? ItemClass.Wanderer : null,
                            // A TRADE on the worn gloves and boots (2026-09-07): a fixture item is minted
                            // without a prefix, so no capture had ever shown a prefix's cost line on the
                            // doll's own pieces — FOCUSED (skills faster, hits softer) and SWIFT.
                            TraitOverride = types[i] switch
                            {
                                ItemBaseType.Gloves => GearTrait.Focused,
                                ItemBaseType.Boots => GearTrait.Swift,
                                _ => null,
                            },
                        });
                    // Two superior UNEQUIPPED drops, added first so they sit at the top of the bag — they
                    // pose the green UP badge and the hover tooltip's "UPGRADE +N PWR" verdict (with
                    // RH_SHOT_MOUSE parked over the first card).
                    // The weapon is the starter's class, so the UPGRADE verdict still poses on the
                    // first card; the chestplate is a WARDEN's, so the second card poses the dimmed
                    // lock and, under RH_SHOT_MOUSE, the "who can wear it" lines of the hover card.
                    // HEAVY, so the first card — the one RH_SHOT_MOUSE hovers and the detail panel
                    // opens on — carries a trade with both sides on it (2026-09-07). A bow, so its
                    // built-in is the longest stat word, CRITICAL CHANCE.
                    var upWeapon = new ItemInstance { InstanceId = "up_wpn", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary, SellValue = 220, Element = Source.Machine, ItemLevel = 62, Class = ItemClass.Wanderer, Family = 1, TraitOverride = GearTrait.Heavy };
                    // ...AND ITS SOCKETS ARE FULL (2026-09-08). The widest row the detail column can
                    // draw is a GEM: the stone's name on the left and its WHOLE modifier line on the
                    // right ("EDGE GEM" · "+3.5% CRITICAL CHANCE"), where every other row pairs a stat
                    // word with a bare figure. No fixture had ever set a stone, so the pair the row
                    // layout exists for had never been photographed at any scale. A gem's stat is
                    // derived from its id (GemCraft.StatOf), so these three ids deterministically give
                    // the widest pair, a second percentage and a flat value — through the real socket
                    // path, at the level the wandering trader's own stones carry.
                    foreach (var gemId in new[] { "cd_gem1", "cd_gem_b", "cd_gem2" })
                    {
                        var gem = new ItemInstance
                        {
                            InstanceId = gemId, BaseType = ItemBaseType.Gem,
                            Rarity = Rarity.Legendary, SellValue = 200, ItemLevel = 18,
                        };
                        if (GemCraft.Socket(upWeapon, gem).Product is { } set) upWeapon = set;
                    }
                    _forge.AddLoot(new List<ItemInstance>
                    {
                        upWeapon,
                        new() { InstanceId = "up_cht", BaseType = ItemBaseType.Chest, Rarity = Rarity.Epic, SellValue = 150, Element = Source.Nature, ItemLevel = 55, Class = ItemClass.Warden },
                    });
                    _forge.AddLoot(seed);
                    // Extra unequipped drops so the inventory grid fills and overflows into a scroll (fixture §17).
                    var srcs = new[] { Source.Shadow, Source.Body, Source.Mind, Source.Spirit, Source.Machine, Source.Nature };
                    var extra = new List<ItemInstance>();
                    // Class-locked drops rotate through all five classes, so the grid poses both the
                    // wearable and the dimmed-and-locked cell states; jewellery stays classless.
                    for (var i = 0; i < 14; i++)
                        extra.Add(new ItemInstance
                        {
                            InstanceId = $"cx{i}", BaseType = types[i % types.Length],
                            Rarity = rar[i % rar.Length], SellValue = 15 + i * 5, Element = srcs[i % srcs.Length],
                            ItemLevel = 20 + i * 3,
                            Class = ItemClasses.IsClassLocked(types[i % types.Length]) ? (ItemClass)(i % 5) : null,
                        });
                    _forge.AddLoot(extra);
                    foreach (var it in seed) _hunter.Equip(it);   // one item per slot — all eight filled
                    _hunter.AddGleam(5000);
                    for (var i = 0; i < 8; i++) _hunter.Train(HunterStat.AttackPower);
                    for (var i = 0; i < 4; i++) _hunter.Train(HunterStat.Vitality);

                    // LAST, and inside this block: the item menu poses ON this fixture, so it can only
                    // open after the bag it points into has actually been filled.
                    if (sm == "itemmenu") _gear.DevOpenItemMenu();
                }

                if (sm is "stats" or "trainingpoor" or "trainingreset")
                {
                    _showTraining = true;
                    _training.Loadout = _loadout;
                    _training.Mastery = _mastery;
                    _training.DiscoveredKeystones = _keystoneMenu;
                    _training.KnownVows = _vowMenu;
                    _training.Character = _characters.Active;
                    _training.SkillLevels = _skillProgress;
                    // RH_SHOT_SELECT poses the inspector against a chosen row.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_SELECT") is { Length: > 0 } pick)
                        _training.DevSelect(pick);

                    _hunter.AddGleam(20000);
                    for (var i = 0; i < 12; i++) _hunter.Train(HunterStat.AttackPower);
                    for (var i = 0; i < 6; i++) _hunter.Train(HunterStat.CriticalChance);
                    for (var i = 0; i < 5; i++) _hunter.Train(HunterStat.Defense);
                    for (var i = 0; i < 8; i++) _hunter.Train(HunterStat.Vitality);
                    _deepestEver = 23; _mastery.SetEarned(77400);   // fixture career values (Stats §13)

                    // AFTER the common seeding, or the purse it leaves would undo the state posed here.
                    // trainingpoor: a MAXED row, a row you cannot afford, and one still untrained.
                    if (sm == "trainingpoor")
                    {
                        _hunter.AddGleam(400_000);
                        for (var i = 0; i < 60; i++) _hunter.Train(HunterStat.Guile);
                        for (var i = 0; i < 20; i++) _hunter.Train(HunterStat.AttackPower);
                        _hunter.SpendGleam(_hunter.Gleam - 120);
                    }
                    // trainingreset: one Crystal, so the ENABLED reset button is in a capture at all —
                    // only the dev-armed state had ever been photographed.
                    if (sm == "trainingreset") _hunter.AddMaterial(Material.Crystal, 1);
                }

                if (sm is "fight" or "welcome" or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "fightfade" or "fightarrive" or "fightcooldown" or "fightaura" or "fightflash" or "fightshield" or "runlog"
                    or "fightstatus" or "fightfive" or "fightshieldbroken" or "fightmulti" or "fightinspect" or "vfxdebug"
                    or "dispatches" or "dispatchesempty" or "dispatchesunread" or "dispatcheshover")
                {
                    // `vfxdebug` is `fightshield` PLUS the VFX contract's own overlay (brief §70): the
                    // standing barrier is the acceptance case, so the mode that photographs the contract
                    // has to be the mode that raises one.
                    if (sm == "vfxdebug") _expedition.DevVfxDebug = true;
                    // RH_SHOT_GEOMETRY=1 draws the same overlay on ANY fight fixture — the actor bodies,
                    // their envelopes and the hunter's keep-out — so a hover pose (`fightinspect`) can
                    // be photographed with the geometry it was hit-tested against.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_GEOMETRY")?.Trim() == "1") _expedition.DevVfxDebug = true;
                    // `fightflash` pins the hit flash so a still capture can prove the white silhouette draws.
                    if (sm == "fightflash") _expedition.DevHoldFlash = true;
                    // `fightaura` slots a NATURE AURA beside the starter Strike, so the always-on pulse
                    // (and the champion NOT flinching to it) is checkable in a capture.
                    if (sm == "fightaura")
                    {
                        var auraSlot = _loadout.AddSkill();
                        if (auraSlot >= 0) { _loadout.SetSource(auraSlot, Source.Nature); _loadout.SetSkill(auraSlot, "field_mire"); }
                    }
                    // A FULL LOADOUT, BOTH KINDS. The plain `fight` fixture carried a single skill, so
                    // no capture of the hunt rail could ever show a PASSIVE row — and its cadence line
                    // (ALWAYS ON / WHEN BITTEN, rather than a beat count) went unphotographed for
                    // exactly that reason. One skill also cannot show the thing the rework is for: two
                    // actives leaving the plain swing most of its beats.
                    if (sm is "fight" or "fightswing" or "fightflash" or "fightshield"
                        or "fightstatus" or "fightfive" or "fightshieldbroken" or "fightmulti" or "fightinspect" or "vfxdebug")
                    {
                        _loadout.SkillCapacity = Math.Max(_loadout.SkillCapacity, 4);
                        // The roads, or every skill but the champion's own is refused and the fixture
                        // photographs an empty build.
                        _mastery.SetEarned(9999);
                        _mastery.RestoreTaken(_mastery.Taken
                            .Concat(MasteryCatalog.Nodes.Where(x => x.Kind == MasteryKind.SkillRoad).Select(x => x.Id))
                            .ToList(), repair: false);
                        // PRESS IS IN THE FIXTURE ON PURPOSE: it is the skill whose whole effect had no
                        // picture, so a capture of the fight that does not carry it cannot show the
                        // badge that was added for it.
                        foreach (var (src, skillId) in new[]
                                 {
                                     (Source.Mind, "volley_spray"),
                                     (Source.Body, "hammer_press"),
                                     (Source.Shadow, "snare_jaws"),
                                 })
                        {
                            var slot = _loadout.AddSkill();
                            if (slot < 0) break;
                            _loadout.SetSource(slot, src);
                            _loadout.SetSkill(slot, skillId);
                        }
                    }
                    // THE THREE OWED FIGHT FIXTURES (UX V2 REPORT §4, UI polish §21 / §108), plus the
                    // shield pose combat-v2 §36 owed. Each poses a state no capture could show before.
                    //
                    // `fightfive` USED TO LIVE HERE — the five-slot strip, posed by buying FIFTH WEAVE
                    // on the trait tree. The fifth slot is removed (a build is two skills that take an
                    // action and two that do not), so the state it photographed no longer exists and the
                    // fixture went with it rather than posing something the game cannot produce.
                    // `fightstatus` — the hunter card's status chips: UNDYING READY and CHARGE n/10 need
                    // the keystones that grant them DISCOVERED and SOCKETED (two sockets). Nothing else
                    // on the screen changes, so the chips are the difference between this and `fight`.
                    //
                    // It used to buy the matching nodes on the Memory tree; the world is the only
                    // teacher now, so the fixture seeds the account's discovered set directly — which is
                    // what a save that had conquered for them actually looks like.
                    if (sm == "fightstatus")
                    {
                        foreach (var id in new[] { "ironclad", "juggernaut", "undying", "glass_cannon", "rend" })
                            _discoveredKeystones.Add(id);
                        _loadout.KeystoneCapacity = 2;
                        var learned = _keystoneMenu;
                        _loadout.ToggleKeystone("undying", learned);
                        _loadout.ToggleKeystone("rend", learned);
                    }
                    // `fightmulti` — VOLLEY as a MULTI-HIT: SPRAY's CLUSTER variation puts all five arrows
                    // on one creature at the same instant, which the screen folds into one "-N ×5". The
                    // level that unlocks the choice is earned the way the game earns it (waves recorded).
                    if (sm == "fightmulti" && SkillCatalogue.Find("volley_spray") is { } sprayDef)
                    {
                        for (var u = 0; u < SkillProgress.UsesForLevel(1); u++) _skillProgress.RecordWave(sprayDef.Id);
                        _skillProgress.ChooseVariation(sprayDef, "CLUSTER");
                    }
                    // `fightshieldbroken` cannot carry JAWS: it answers every bite by STOPPING it, so the
                    // shield only ever met one leaked point. MIRE (a field) takes its passive slot.
                    // `fightinspect` — the ENEMY INSPECTOR's fixture: a Mark (CALL), a slow (MIRE) and an
                    // attack break (WILT) on the wave, so a hover over a creature has statuses to read.
                    // RH_SHOT_PAGE_MOUSE puts the pointer on a creature; RH_SHOT_T seeks into the wave.
                    if (sm == "fightinspect")
                    {
                        if (_loadout.IndexOfSkill("volley_spray") is var sp && sp >= 0) { _loadout.SetSource(sp, Source.Mind); _loadout.SetSkill(sp, "sign_call"); }
                        if (_loadout.IndexOfSkill("hammer_press") is var pr && pr >= 0) { _loadout.SetSource(pr, Source.Nature); _loadout.SetSkill(pr, "field_mire"); }
                        if (_loadout.IndexOfSkill("snare_jaws") is var jw && jw >= 0) { _loadout.SetSource(jw, Source.Shadow); _loadout.SetSkill(jw, "drain_wilt"); }
                    }
                    if (sm == "fightshieldbroken" && _loadout.IndexOfSkill("snare_jaws") is var jawsSlot && jawsSlot >= 0)
                    {
                        _loadout.SetSource(jawsSlot, Source.Nature);
                        _loadout.SetSkill(jawsSlot, "field_mire");
                    }
                    // THE CAPPED CREATURE'S POSE. Which figure the arena draws is the region's THEME, and
                    // how big its box is is the wave's ARCHETYPE — a seeded roll. Only a Bruiser box asks
                    // the renderer for more magnification than it will give, and only of a deeply
                    // letterboxed idle (the rift guardian's, in the Spirit region), so the one case where
                    // the drawn figure and its geometry could disagree could not be photographed at all.
                    // Both dials move presentation and nothing else: the art key and a layout scale.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_SOURCE")?.Trim() is { Length: > 0 } shotSource)
                        _expedition.DevEnemySource = Enum.TryParse<Source>(shotSource, ignoreCase: true, out var src)
                            ? src
                            : throw new InvalidOperationException(
                                $"RH_SHOT_SOURCE='{shotSource}' is not a Source. Known: " + string.Join(", ", Enum.GetNames<Source>()) + ".");
                    // RH_SHOT_NOSTRIP=1 takes every animation strip away from the arena, so the STATIC
                    // FALLBACK draws — the path no shipped asset can reach, and so the one nobody had
                    // ever seen. The geometry falls back with it, which is the point of posing it.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_NOSTRIP")?.Trim() == "1")
                        _expedition.DevNoStrips = true;
                    // RH_SHOT_CREATURES=<n> draws only the first n of the wave's creatures, so a LONE
                    // creature and its pack can be photographed at one camera and their bodies compared
                    // — the proof that the archetype scale is population-independent (both go through
                    // HuntScreen.CreatureRow). The seeded roll cannot pose that pair on its own: a
                    // Bruiser always rolls exactly one.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_CREATURES")?.Trim() is { Length: > 0 } shotCount)
                        _expedition.DevCreatureCount = int.TryParse(shotCount, out var n) && n > 0
                            ? n
                            : throw new InvalidOperationException(
                                $"RH_SHOT_CREATURES='{shotCount}' is not a positive creature count.");
                    if (Environment.GetEnvironmentVariable("RH_SHOT_ARCHETYPE")?.Trim() is { Length: > 0 } shotArch)
                        _expedition.DevArchetype = Enum.TryParse<Archetype>(shotArch, ignoreCase: true, out var arch)
                            ? arch
                            : throw new InvalidOperationException(
                                $"RH_SHOT_ARCHETYPE='{shotArch}' is not an Archetype. Known: " + string.Join(", ", Enum.GetNames<Archetype>()) + ".");
                    // fightswing holds the strike clip at its apex, so the one pose a timed capture can
                    // never catch — the blade at full extension — is checkable.
                    if (sm == "fightswing")
                        _expedition.DevSwingPhase =
                            float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_SWING"),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var swp) ? swp : 0.55f;
                    // `fightgear` dresses the Hunter before the fight opens. A fresh save wears nothing, so
                    // a plain `fight` capture can never show worn equipment — and worn equipment is exactly
                    // what the rig bindings need verifying against.
                    if (sm is "fightgear" or "fightswing" or "fightreport" or "fightfall" or "fightfade" or "runlog" or "fightshield" or "fightshieldbroken" or "vfxdebug"
                        or "dispatches" or "dispatchesempty" or "dispatchesunread" or "dispatcheshover")
                    {
                        var worn = new[]
                        {
                            ItemBaseType.Weapon, ItemBaseType.Helm, ItemBaseType.Chest,
                            ItemBaseType.Gloves, ItemBaseType.Boots,
                        };
                        var wrar = new[] { Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Epic, Rarity.Rare };
                        // `fightshield` wears the FULL MACHINE SET, because SHIELD has no other way onto
                        // the screen: the strip, the standing barrier and the SHIELDED chip are all states
                        // no existing capture mode could pose, and a state nothing can pose is a state
                        // nobody has ever looked at. Five MACHINE pieces open the 3-piece wave-start
                        // shield, so the bar is filled from the wave's first frame rather than depending
                        // on a grant landing in the instant the shutter opens.
                        var setEl = sm is "fightshield" or "fightshieldbroken" or "vfxdebug" ? Source.Machine : Source.Nature;
                        for (var i = 0; i < worn.Length; i++)
                            _hunter.Equip(new ItemInstance
                            {
                                InstanceId = $"fg{i}", BaseType = worn[i], Rarity = wrar[i],
                                // `fightshieldbroken`: FOUR machine pieces, not five — the 5-piece
                                // PLATING turns the first damaging bite into shield, and a shield
                                // that refills at every bite is a shield no capture can show broken.
                                SellValue = 40 + i * 20, ItemLevel = 30 + i * 5,
                                Element = sm == "fightshieldbroken" && i == worn.Length - 1 ? Source.Nature : setEl,
                            });
                    }
                    _expedition.Loadout = _loadout;
                    _expedition.Mastery = _mastery;
                    // The levels too — the run DevStart composes is the run the capture shows (nothing
                    // restarts it), so a variation chosen above (fightmulti's CLUSTER) has to be in the
                    // build before the fight starts, not pushed by the host on a later frame.
                    _expedition.Progress = _skillProgress;
                    // Pose the new fight-screen UX: the welcome-back toast, and the "you have things to do"
                    // attention cue (a waiting chest + unspent mastery points).
                    // Not for the fall fixtures: the welcome-back toast outranks every arena overlay, so
                    // it would stand over the very state those captures exist to show.
                    // No toast over `fightshield`: it would sit on the very strip the capture is for.
                    if (sm is not ("fightreport" or "fightfall" or "fightfade" or "fightarrive" or "fightcooldown" or "runlog" or "welcome" or "fightshield" or "fightshieldbroken" or "fightstatus"
                                   or "dispatches" or "dispatchesempty" or "dispatchesunread" or "dispatcheshover"))
                    {
                        _bootMessage = "WELCOME BACK\n+140 GLEAM EARNED WHILE AWAY";   // the short-trip toast
                        _bootColor = Gold; _bootTimer = 7f;
                    }
                    // welcome: a REAL six-hour-forty-two-minute absence, run through the same simulation
                    // LoadOrStartFresh uses, so the panel shows figures the model produced (UX V2 P1.3).
                    if (sm == "welcome")
                    {
                        const double away = 6 * 3600 + 42 * 60;
                        var (obh, obd) = EnemyBaselineFor(_activeRegion);
                        _warren.ConqueredRegions = _world.ConqueredIds.Count;
                        var warrenOpen = Unlocks.IsOpen(Activity.Warren, GuideUnlockFacts());
                        var camp = warrenOpen ? _warren : null;
                        var held = OfflineCamp.CreditedSeconds(away, camp);
                        var offline = OfflineHunt.Simulate(ComposeBuild(),
                            _hunter, held, _activeRegion, Regions.Get(_activeRegion).CombatBias, obh, obd, seed: 42);
                        _welcome = new WelcomeSummary(away, offline, warrenOpen ? _warren.Tick((float)held) : default, warrenOpen,
                                                      CampSeconds: held, HuntGleamPaid: OfflineCamp.HuntCredit(offline, camp, away));
                        _showWelcome = true;
                    }
                    _forge.AddChest(new Chest { Rarity = Rarity.Epic, Tier = 8, Element = Source.Nature });
                    // Points are DERIVED from depth every frame (SkillPointsEarned → MasteryPoints), so a
                    // SetEarned here lasted one frame; a real best depth of 40 pays five points.
                    _world.RegionFarm(_activeRegion).RestoreBestDepth(40);
                    _deepestEver = 25;   // MasteryOpen gate (Unlocks: DeepestWave >= 25) — without it the SPEND POINTS row this fixture poses never renders
                    // A DELIBERATELY beefy enemy: a real wave-1 fight is over in ~1.5s, so a shot taken a
                    // second in only ever caught the aftermath — useless for verifying the fight itself.
                    // This one stays standing long enough to capture skills firing.
                    // Umbral Reach, the swarm region, so the capture can show whether compositions
                    // render at all. Set through SetActiveRegion — Game1 rewrites _expedition.RegionId
                    // from the active region every frame, so assigning the screen directly is overwritten
                    // before the first draw.
                    _world.Conquer(VerdantHollow.RegionId);
                    _world.Conquer("cinderworks");
                    SetActiveRegion("umbral_reach");
                    if (sm == "fightreport")
                    {
                        // THE LOG, OPEN ON A SEEDED DEATH'S REPORT — the report's one surface. RH_SHOT_LIMIT=
                        // armour|reach|sustain poses its diagnostic for that limit: the real seeded death,
                        // with the one measurement that names the limit set past its threshold — so each of
                        // the three verdicts the log can give has a picture. (`runlog` is the same door
                        // after three descents.)
                        var limit = Environment.GetEnvironmentVariable("RH_SHOT_LIMIT")?.ToLowerInvariant() switch
                        {
                            "armour" or "armor" => RunLimit.Armour,
                            "reach" => RunLimit.Reach,
                            "sustain" => RunLimit.Sustain,
                            _ => (RunLimit?)null,
                        };
                        _expedition.DevRunToDeath(_hunter, poseLimit: limit);
                        _expedition.ToggleLog();
                    }
                    else if (sm == "fightfall")
                    {
                        // The COLLAPSE, uncovered. The report is a large centred panel that lands on top
                        // of the champion, so the death could never be photographed from "fightreport" —
                        // and a death nobody can photograph is a death nobody reviewed. RH_SHOT_T picks
                        // the moment: 0 is the instant of the fall, 1 the settled body.
                        var t = float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                                               System.Globalization.NumberStyles.Float,
                                               System.Globalization.CultureInfo.InvariantCulture, out var v)
                            ? v : 0.6f;
                        _expedition.DevRunToDeath(_hunter, fallProgress: t);
                    }
                    else if (sm == "fightcooldown")
                    {
                        // THE RAIL'S COOLDOWN, at a chosen point in its cycle. RH_SHOT_T is the sweep:
                        // 0 just cast, 0.5 halfway, 1 the instant it comes up.
                        _expedition.DevStart(_hunter, 1400f, 14f);
                        _expedition.DevSeek(1.2f);
                        var sweep = float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                                                   System.Globalization.NumberStyles.Float,
                                                   System.Globalization.CultureInfo.InvariantCulture, out var sv)
                            ? sv : 0.45f;
                        _expedition.DevPoseCooldown(sweep);
                    }
                    else if (sm == "fightarrive")
                    {
                        // THE ENTRANCE. RH_SHOT_T is how far off-stage the wave still is: 1 fully off,
                        // 0.35 most of the way in, 0 landed. DevStart clears the gate so a fight pose is
                        // not stuck behind it, so this puts it back deliberately.
                        _expedition.DevStart(_hunter, 1400f, 14f);
                        var at = float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                                                System.Globalization.NumberStyles.Float,
                                                System.Globalization.CultureInfo.InvariantCulture, out var av)
                            ? av : 0.55f;
                        _expedition.DevPoseArrival(at);
                    }
                    else if (sm == "fightfade")
                    {
                        // THE DEATH TRANSITION. RH_SHOT_T sweeps it: 0 the last readable instant of the
                        // fall, 0.5 the black with the next descent already begun beneath it, 1 the stage
                        // back (DeathTransition.At). RH_SHOT_REDUCED=1 poses its cuts. From the restart on,
                        // what stands under the black is a real wave one — StartRun ran — not a posed corpse.
                        var t = float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                                               System.Globalization.NumberStyles.Float,
                                               System.Globalization.CultureInfo.InvariantCulture, out var ft)
                            ? ft : 0.5f;
                        _expedition.DevRunToDeath(_hunter);
                        _expedition.DevPoseDeathTransition(t);
                    }
                    else if (sm == "runlog")
                    {
                        // Three descents, so the log has something to step BACK through — a one-entry
                        // log proves nothing about the thing it exists for.
                        for (var i = 0; i < 3; i++) _expedition.DevRunToDeath(_hunter);
                        _expedition.ToggleLog();
                    }
                    // `fightshieldbroken`: a bite heavy enough that, after armour, it exceeds the 3-piece
                    // wave-start shield — one bite absorbs the whole shield, BREAKS it, and the rest
                    // reaches health: the split bite and the break, in one deterministic instant.
                    // Measured in Core against this exact gear and build: the pool is 243, the wave-start
                    // shield 29, and at 70 the swarm's first bite (1.5 s in) absorbs all 29, breaks the
                    // shield and lands 51 on health. The creatures are thick (6000) so the wave outlasts
                    // that bite. PINNED THROUGH THE HOST (_shotEnemyBaseline): the host pushes the
                    // region's baseline every frame and the run restarts on its first live frame, so a
                    // number handed to DevStart alone never survives to the shutter.
                    else if (sm == "fightshieldbroken")
                    {
                        // 180, not 70: this region's FAST bias lands lighter bites, and at 70 only three
                        // points leaked past the shield — a break with no visible split. (Measured with
                        // RH_SHOT_DUMP, which is what it exists for.)
                        _shotEnemyBaseline = (6000f, 180f);
                        _expedition.DevStart(_hunter, 6000f, 180f);
                        // The shutter opens on the break itself, not on a guessed second.
                        _expedition.DevSeekBefore(e => e.Kind == BattleEventKind.ShieldBroken, ShotLead());
                    }
                    else
                    {
                        // PINNED THROUGH THE HOST, like the broken-shield pose: every fight fixture said
                        // "a deliberately beefy enemy, 1400" and photographed the region's real baseline,
                        // because the host pushes that baseline every frame and restarts the run once.
                        _shotEnemyBaseline = (1400f, 9f);
                        _expedition.DevStart(_hunter, 1400f, 9f);
                    }
                    // `fightmulti`: the shutter opens on SPRAY's cast, when its five CLUSTER hits print.
                    if (sm == "fightmulti")
                        _expedition.DevSeekBefore(e => e.Kind == BattleEventKind.Skill && e.Slot == _expedition.DevSlotOf("volley_spray"), ShotLead());
                    // `fight` (and the shield / multi-hit poses) with RH_SHOT_T: seconds into the wave to
                    // pose (capture.sh's third argument), so the bars can be photographed after the fight
                    // has moved them — and a bite or a burst caught at its instant.
                    if (sm is "fight" or "fightshield" or "fightshieldbroken" or "fightmulti" or "fightinspect" or "fightstatus" or "fightfive" or "vfxdebug"
                        && float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var seekS))
                        _expedition.DevSeek(seekS);
                }
                if (sm is "boss" or "bossdebug" or "corruptedboss")
                {
                    // Rev 4 §12 / Rev 5 boss fixture: force the current wave to render as the Crystal Lich boss
                    // (crystal_lich_idle), no ordinary enemies, no welcome/wave overlay. `bossdebug` also shows
                    // the §17 bounds overlay.
                    _expedition.Loadout = _loadout;
                    _expedition.Mastery = _mastery;
                    _expedition.EnemySource = Source.Mind;
                    _expedition.DevForceBoss = true;
                    if (sm == "bossdebug") _expedition.DevBossDebug = true;
                    _expedition.DevStart(_hunter, 6000f, 6f);
                }
                if (sm == "conquered")
                {
                    // Whole world conquered — show the map with the DEEPEN THE CORRUPTION button live.
                    // No message is set: a conquered world gives the strip to the corruption LADDER, so
                    // the line this fixture used to write was never drawn. (Nor was the live one — the
                    // sixth conquest's own news lands on the same frame the ladder takes the strip.
                    // That is why the keystone reveal is a notice now and not a line in here.)
                    foreach (var r in Regions.All) _world.Conquer(r.Id);
                    _showWorld = true;
                }
                // RH_SHOT_BOSSCALL=1 holds the BOSS INCOMING announcement up on any fight fixture. It
                // lives a second and a half on a timer, in the break before a boss wave, so nobody had
                // ever photographed it against the header above it — see HuntScreen.DevBossCall.
                if (Environment.GetEnvironmentVariable("RH_SHOT_BOSSCALL") is { Length: > 0 })
                    _expedition.DevBossCall = true;
                if (sm is "corrupted" or "corruptedboss")
                {
                    // The corruption ladder at tier 3 (FEVERED): the map's SHALLOWER / DEEPER row, and the
                    // hunt's tinted creatures + the boss's epithet. Verification fixtures for 2026-08-23.
                    foreach (var r in Regions.All) _world.Conquer(r.Id);
                    _world.RestoreCorruption(3);
                    if (sm == "corrupted") _showWorld = true;
                    else { _expedition.DevForceBoss = true; _expedition.CorruptionTier = 3; }
                }
                if (sm == "help") _showHelp = true;
                if (sm == "typespec") _showTypeSpec = true;
                if (sm == "settings") _showSettings = true;
                if (sm == "settingsfull") { _showSettings = true; _displayMode = DisplayMode.Fullscreen; }
                // Poses the MODE dropdown OPEN, so the option list's z-order over the rows under
                // it is photographable (pair with RH_SHOT_MOUSE to park the cursor on a row and
                // pose its hover explanation in the same shot).
                // RH_SHOT_DROPDOWN picks WHICH list is posed: "mode" (the default) or "size".
                if (sm == "settingsopen")
                {
                    _showSettings = true;
                    OpenDropdown(Environment.GetEnvironmentVariable("RH_SHOT_DROPDOWN") == "size" ? 2 : 1);
                }
                if (sm == "world")
                {
                    // A mid-progression map — home conquered, Cinderworks unlocked — with the strip
                    // carrying the line a REAL conquest writes: the news and the keystone's compact
                    // headline, built by the same helper the live conquest calls, so what is
                    // photographed is the real string and not a stand-in for it. Until this fixture
                    // the reveal had no capture mode at all, and the four lines it used to write into
                    // a one-line plate spilled across the region cards unphotographed.
                    //
                    // RH_SHOT_CONQUEST=<region id> poses a LATER conquest's line. `still_archive` is
                    // the longest headline the world can produce, and is what the one-line contract
                    // has to hold at UI SCALE 150. Every region but the LAST: posing a conquest
                    // conquers every region up to it, so `pale_choir` leaves the world whole and the
                    // corruption ladder takes the strip — the very fact that moved the reveal to a
                    // notice. That capture shows the ladder, which is the real screen for it.
                    // An unknown region id ABORTS rather than quietly posing VERDANT HOLLOW: this dial
                    // exists to photograph the LONGEST headline, and a typo silently posing the shortest
                    // would return a passing picture of the case nobody asked about.
                    var fell = VerdantHollow.RegionId;
                    if (Environment.GetEnvironmentVariable("RH_SHOT_CONQUEST")?.Trim() is { Length: > 0 } cq)
                        fell = Regions.Find(cq) is not null ? cq
                             : throw new InvalidOperationException(
                                 $"RH_SHOT_CONQUEST='{cq}' is not a region id. Known: " +
                                 string.Join(", ", Regions.All.Select(r => r.Id)) + ".");
                    foreach (var r in Regions.All)
                    {
                        _world.Conquer(r.Id);
                        if (r.Id == fell) break;
                    }
                    var gift = Keystones.Sources.FirstOrDefault(
                                   s => s.RegionId == fell && s.Rung == WorldRung.Conquest) is { } taught
                               ? Keystones.ById(taught.KeystoneId) : null;
                    _conquerMsg = MapScreen.ConquestHeadline(fell, Regions.Next(fell), gift);
                    _showWorld = true;
                }
                // mapdeep: the same world, but the region you are in was conquered and held deep — the only
                // pose in which the START AT WAVE chips exist (they need more than one option), and the one
                // that photographs a checkpoint the player cannot afford (UX V2 P2.1).
                if (sm == "mapdeep")
                {
                    _world.Conquer(VerdantHollow.RegionId);
                    _world.Conquer("cinderworks");
                    _world.Conquer("umbral_reach");
                    _world.Conquer("marrow_wastes");
                    SetActiveRegion("marrow_wastes");
                    _world.RegionFarm("marrow_wastes").RestoreBestDepth(43);
                    _world.RegionFarm("marrow_wastes").SetStartWave(40);
                    _deepestEver = 43;
                    _showWorld = true;
                    _mapScreen.ActiveRegion = _activeRegion;
                    _mapScreen.DevSelect("marrow_wastes");
                    _hunter.AddGleam(131_900_000);
                    _dust.AddDust(400);   // short of the 1,000 a wave-40 start costs, so the refusal line shows
                }
                // maplocked: the inspector on a region you cannot reach yet — YOU NEED FIRST and the
                // requirement in place of a button.
                if (sm == "maplocked")
                {
                    _world.Conquer(VerdantHollow.RegionId);
                    _world.Conquer("cinderworks");
                    SetActiveRegion("umbral_reach");
                    _showWorld = true;
                    _mapScreen.ActiveRegion = _activeRegion;
                    _mapScreen.DevSelect("pale_choir");
                    _hunter.AddGleam(131_900_000);
                    _dust.AddDust(77_400);
                }
                if (sm == "map")
                {
                    // The reference's pattern: three regions cleared, one in progress (selected), the rest locked.
                    _world.Conquer(VerdantHollow.RegionId);
                    _world.Conquer("cinderworks");
                    _world.Conquer("umbral_reach");
                    SetActiveRegion("marrow_wastes");   // unlocked by conquering umbral_reach; the in-progress region
                    _world.RegionFarm("marrow_wastes").RestoreMasteryPoints(650);   // a real Partially-Mastered farm
                    _deepestEver = 24;
                    _showWorld = true;
                    _mapScreen.ActiveRegion = _activeRegion;
                    _mapScreen.SelectActive();
                    _hunter.AddGleam(131_900_000);      // top currency pills read like the reference
                    _dust.AddDust(77_400);
                }
                if (sm == "region2")
                {
                    // Travel to the second region and fight one of its standard creatures.
                    _world.Conquer(VerdantHollow.RegionId);
                    SetActiveRegion("cinderworks");
                }
                if (sm == "region3")
                {
                    // Travel to the third region (Shadow) and fight there.
                    _world.Conquer(VerdantHollow.RegionId);
                    _world.Conquer("cinderworks");
                    SetActiveRegion("umbral_reach");
                }
                // GEMTOUR is the forge fixture plus loose gems, with the first-gem lesson owed (see
                // SeedExplained) — so the lesson's two cards can be posed over the furniture they name.
                if (sm is "forge" or "reforge" or "gemtour" or "forgeempty")
                {
                    _showForge = true;
                    if (sm == "gemtour")
                    {
                        var gemRng = new Random(11);
                        _forge.AddLoot(new[] { GemCraft.MintGem(4, gemRng), GemCraft.MintGem(7, gemRng) });
                    }
                    // Seed a spread of loot so SALVAGE has content, and a hero item the UPGRADE view poses.
                    var rar = new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
                    // NOT Material: Bag() filters to Gear.IsWearable, so three of the nine seeded rows
                    // never rendered — and the bag never showed an armour slot at all.
                    var types = new[] { ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.Helm,
                                        ItemBaseType.AbilityFocus, ItemBaseType.Boots, ItemBaseType.Ring };
                    var loot = new List<ItemInstance>();
                    for (var i = 0; i < 9; i++)
                        loot.Add(new ItemInstance
                        {
                            InstanceId = $"dev{i}", BaseType = types[i % types.Length],
                            Rarity = rar[i % rar.Length], SellValue = 12 + i * 7,
                            Element = (Source)(i % 6),
                        });
                    // The hero: a deep Legendary Shadow weapon — four affixes, a real enchant, a mid item level
                    // so a refine reads (current→next power, before→after affixes). Mirrors the spec fixture.
                    // Its enchant is PINNED to a keystone combo, so this shot also poses the branch of the
                    // combo line that asks about something other than a Form. The id-derived roll was a
                    // Form combo, which the SIPHON charm in the lootforge fixture already covers.
                    loot.Add(new ItemInstance
                    {
                        InstanceId = "dev_hero", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary,
                        SellValue = 200, Element = Source.Shadow, ItemLevel = 8,
                        EnchantOverride = EnchantKind.Fervour,
                        // HEAVY (2026-09-07): the bench's hero carries a trade, so the WHAT IT DOES column
                        // and the upgrade preview pose a prefix's cost row beside its bonus.
                        TraitOverride = GearTrait.Heavy,
                    });
                    // A pair of armour pieces with a trade, for RH_SHOT_ITEM (2026-09-07): the two slots
                    // whose built-in line read "GLOVES +2%" / "BOOTS +4%" — a Legendary FOCUSED glove
                    // (skill rate up, damage down) and an Epic GREEDY boot (loot up, health down).
                    loot.Add(new ItemInstance
                    {
                        InstanceId = "dev_glove", BaseType = ItemBaseType.Gloves, Rarity = Rarity.Legendary,
                        SellValue = 180, Element = Source.Body, ItemLevel = 40, Class = ItemClass.Wanderer,
                        TraitOverride = GearTrait.Focused,
                    });
                    loot.Add(new ItemInstance
                    {
                        InstanceId = "dev_boot", BaseType = ItemBaseType.Boots, Rarity = Rarity.Epic,
                        SellValue = 120, Element = Source.Spirit, ItemLevel = 24, Class = ItemClass.Wanderer,
                        TraitOverride = GearTrait.Greedy,
                    });
                    // THE THREE STATES §95 ASKS FOR AND NO FIXTURE COULD POSE. dev_hero has Upgrades = 0,
                    // so the slip branch, the at-cap state and every below-Rare locked state were
                    // unphotographable — the copy for all three shipped unchecked.
                    loot.Add(new ItemInstance
                    {
                        InstanceId = "dev_rung", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare,
                        SellValue = 90, Element = Source.Machine, ItemLevel = 6, Upgrades = 7,
                    });
                    loot.Add(new ItemInstance
                    {
                        InstanceId = "dev_cap", BaseType = ItemBaseType.Charm, Rarity = Rarity.Epic,
                        SellValue = 140, Element = Source.Mind, ItemLevel = 12, Upgrades = 15,
                    });
                    loot.Add(new ItemInstance
                    {
                        InstanceId = "dev_low", BaseType = ItemBaseType.Boots, Rarity = Rarity.Common,
                        SellValue = 8, ItemLevel = 1,
                    });
                    // forgeempty poses the OTHER unphotographable state: a bench with nothing on it.
                    if (sm != "forgeempty")
                    {
                        _forge.AddLoot(loot);
                        // One piece WORN, so the row tag, the category tag and the worn-salvage variant
                        // (which always asks, and has no "don't ask again" box) are all in the default shot.
                        if (loot.FirstOrDefault(i => i.InstanceId == "dev1") is { } wear)
                            _hunter.Equip(wear);
                    }
                    _forge.DevFocus("dev_hero");   // pose the UPGRADE screen on the hero item

                    // Stock the tiers + Gleam so the cost rows read as MET and the top pills show real values.
                    _hunter.AddGleam(131_900_000);
                    _hunter.AddMaterials(12_600);                 // SCRAP
                    _hunter.AddMaterial(Material.Essence, 3_400);
                    _hunter.AddMaterial(Material.Core, 820);
                    _hunter.AddMaterial(Material.Crystal, 400);
                    _dust.AddDust(77_400);               // the Memory-Dust pill, so it reads like the ref

                    // ("hybrid" and the DevReforge pose retired with the pile screen — one workbench now.)
                }
                if (sm is "warren" or "warrenready")
                {
                    _showWarren = true;
                    foreach (var id in new[] { "verdant_hollow", "cinderworks", "umbral_reach", "marrow_wastes" })
                        _world.Conquer(id);   // 4 conquered → the CONQUEST production bonus reads +40%
                    // Seed the reference's exact facility state (spec §6-9): level 23, the listed facility levels.
                    _warren.Restore(23, 18_540, new Dictionary<FacilityKind, int>
                    {
                        [FacilityKind.Nursery] = 18, [FacilityKind.Tunnels] = 17, [FacilityKind.ForagingPits] = 16,
                        [FacilityKind.ScavengerRuns] = 15, [FacilityKind.BreedingChamber] = 16, [FacilityKind.RitualNest] = 14,
                        [FacilityKind.HoardVaults] = 13, [FacilityKind.SentryBurrows] = 12,
                    });
                    // BALANCES THAT POSE ALL THREE CARD STATES. At 131.9M Gleam every cost was
                    // trivially met, so neither UPGRADE READY nor a shortfall could be photographed —
                    // and with no depth at all the cap was 1, so all eight cards read as capped.
                    _deepestEver = sm == "warrenready" ? 200 : 80;   // cap 40 (none capped) / 16 (four capped)
                    _hunter.AddGleam(sm == "warrenready" ? 300_000 : 40_000);
                    _dust.AddDust(12_600);
                    _hunter.AddMaterials(2_400);                      // SCRAP, so the pill agrees with the strip
                    _hunter.AddMaterial(Material.Essence, 900);
                }

                // warrenfresh: the Warren a player meets on their FIRST conquest — three facilities
                // open, five locked and naming the conquest that opens them, and two of the four
                // resource chips honestly reading +0. Nobody had ever looked at that state.
                if (sm == "warrenfresh")
                {
                    _showWarren = true;
                    _world.Conquer(VerdantHollow.RegionId);
                    _deepestEver = 20;
                    _hunter.AddGleam(500);
                    _dust.AddDust(60);
                }
                // warrenlocked: THE RAIL'S LOCKED LINE (2026-09-06). Two conquests, so four facilities
                // are open and four are locked with REAL reasons from the unlock model
                // (UnlockedFacilityCount = 2 + conquered): the next region by name, then "2 MORE
                // REGIONS" and on. One facility upgraded and capped by the depth, one with an upgrade
                // it can afford, two at level 1 — every card state the rail has to explain.
                if (sm == "warrenlocked")
                {
                    _showWarren = true;
                    foreach (var id in new[] { "verdant_hollow", "cinderworks" }) _world.Conquer(id);
                    _warren.Restore(6, 1_200, new Dictionary<FacilityKind, int>
                    {
                        [FacilityKind.Nursery] = 6, [FacilityKind.Tunnels] = 3,
                    });
                    _deepestEver = 30;                  // cap 6: the Nursery is capped, the Tunnels are not
                    _hunter.AddGleam(9_000);
                    _dust.AddDust(300);
                    _hunter.AddMaterials(120);
                }
                if (sm == "farm")
                {
                    _showWarren = true;   // deprecated: the creature den was removed; shows the Warren dashboard
                }
                // TRAITLIT / TRAITTERM pose the unlock flourish mid-animation. A celebration is over in
                // a second and the capture rig renders a fixed frame count then exits, so without a
                // fixture "the flourish works" could only ever be a claim.
                // ROSTER poses the whole cast unlocked so every card's art can be checked at once;
                // ROSTERLOCKED leaves it as a fresh save, which is the state a new player actually
                // sees and the one where a locked card still has to explain itself.
                // THE VAULT — a pile of chests of different grades, regions and depths, so the page poses
                // with the variety it exists to show rather than five copies of one card.
                if (sm == "trader")
                {
                    _showVault = true;
                    // A wallet that can afford the Rare and the gem but NOT the Legendary, so the
                    // capture shows both the payable and the refused price colours.
                    _hunter.AddMaterials(2_000);
                    _hunter.AddMaterial(Material.Essence, 400);
                    _hunter.AddMaterial(Material.Core, 20);
                    _deepestEver = 18;
                    _vault.DevOpenTrader();
                }

                // VAULTFIRST is a NEW GAME's vault: exactly what SeedNewGame parks — the one welcome
                // gift — so the first visit the Vault's tour describes can be looked at, not assumed.
                if (sm == "vaultfirst")
                {
                    _showVault = true;
                    foreach (var gift in GiftChests.NewGameChests()) _forge.AddChest(gift);
                    // RH_SHOT_OPEN poses the gift's REVEAL — the one reveal whose contents are known
                    // in advance, so the card can be checked against the catalogue.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_OPEN") is not null)
                    {
                        _forge.DevOpenOneChest(_hunter);
                        if (_pendingRevealPose is { } gp) _forge.DevPoseReveal(gp);
                    }
                }

                // vaultfilter: the vault with the CHEST FILTER popover open and a setting in it (UX V2 P1.1: the
                // filter moved here from the HUNT), so the lit medallions and the tier line can be checked.
                if (sm == "vaultfilter")
                {
                    _chestKeepMinTier = 3; _chestKeepSlots.Add(ItemBaseType.Helm); _chestKeepSlots.Add(ItemBaseType.Boots);
                    _vault.KeepMinTier = 3; _vault.KeepSlots.Add(ItemBaseType.Helm); _vault.KeepSlots.Add(ItemBaseType.Boots);
                    _vault.FilterOpen = true;
                }

                // vaultempty: the state no fixture could pose until UX V2 P1.8 — a vault with nothing
                // in it. The empty state had been written, rewritten and shipped for months without
                // ever being photographed, and it was wrong in three ways when it finally was.
                // RestoreChests runs after LoadContent's own restore, so it wins.
                if (sm is "vaultempty" or "vaultemptyfilter")
                {
                    _showVault = true;
                    _forge.RestoreChests(Array.Empty<Chest>());
                }
                // ...and the same room with a keep-filter set, which is the only state in which the
                // empty vault's third line — the one that says where your chests went — is drawn.
                if (sm == "vaultemptyfilter")
                {
                    _chestKeepMinTier = 3; _chestKeepSlots.Add(ItemBaseType.Helm); _chestKeepSlots.Add(ItemBaseType.Boots);
                    _vault.KeepMinTier = 3; _vault.KeepSlots.Add(ItemBaseType.Helm); _vault.KeepSlots.Add(ItemBaseType.Boots);
                }

                // vaultsell: the pile with both OPEN-ALL consequence traits bought, so the reserved
                // row under the toolbar has something true to say. Nothing is special-cased — the
                // traits are really purchased, UpdateExpedition sets the Forge's two flags from them,
                // and the host feed hands them to the vault the way it does in a real save.
                if (sm == "vaultsell")
                {
                    // AUTO-SELL AND AUTO-MERGE ARE THE WARREN'S NOW, so the fixture raises the two
                    // facilities that carry them rather than buying the retired tree's filter nodes:
                    // SCAVENGER RUNS 2 sells Commons, HOARD VAULTS 2 merges on open.
                    _warren.Restore(_warren.Level, _warren.Xp, new Dictionary<FacilityKind, int>
                    {
                        [FacilityKind.ScavengerRuns] = 2,
                        [FacilityKind.HoardVaults] = 2,
                    });
                }
                if (sm is "vault" or "vaultfilter" or "vaultsell" or "vaultmany")
                {
                    _showVault = true;
                    var grades = new[] { Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Rare,
                                         Rarity.Uncommon, Rarity.Common };
                    var regions = new[] { "cinderworks", "umbral_reach", VerdantHollow.RegionId,
                                          "pale_choir", "marrow_wastes", "still_archive" };
                    for (var i = 0; i < grades.Length; i++)
                        // The Epic card is posed as a STACK of three (identical chests stack; the pile
                        // and the ×3 badge are what the capture proves) — a loop of `copies` per grade.
                        for (var copy = 0; copy < (i == 1 ? 3 : 1); copy++)
                            _forge.AddChest(new Chest
                            {
                                Rarity = grades[i],
                                Tier = 6 + i * 7,
                                Element = (Source)(i % 6),
                                Region = regions[i],
                                RunTilt = i == 0 ? 1.35f : 1f,
                            });
                }

                // vaultmany: three pages of chests, so the page indicator, the wheel and a SHORT last
                // row are all in one capture — plus the two honest edge cases the six-chest pile has
                // never held: a chest with no element and no region ("Plain — no element.", "No
                // favoured gear.") and one won on a BAD hunt, whose tilt line reads the other way.
                if (sm == "vaultmany")
                {
                    foreach (var gift in GiftChests.NewGameChests()) _forge.AddChest(gift);
                    _forge.AddChest(new Chest { Rarity = Rarity.Rare, Tier = 18, Element = null, Region = null });
                    _forge.AddChest(new Chest
                    {
                        Rarity = Rarity.Epic, Tier = 24, Element = Source.Spirit,
                        Region = "pale_choir", RunTilt = 0.8f,
                    });
                }

                // `weavefresh` — the BUILD screen of an account the world has taught NOTHING: no
                // keystone, no socket, one Vow (the one that is simply given), one skill slot. Every
                // line of "you have not found any of this yet" copy lives only in this state, and until
                // this fixture existed no capture could reach it — the `weave` pose conquers the whole
                // world on its first frame. A state no dial can pose has never been looked at.
                if (sm == "weavefresh")
                {
                    _showLoadout = true;
                    _deepestEver = 6;                       // the BUILD screen's own gate, and no further
                    _mastery.SetEarned(3);
                    _mastery.RestoreTaken(new[] { "road_volley", "road_volley_2" }, repair: false);
                    if (_characters.Active.SignatureSkillId is { } freshSig)
                    {
                        if (_loadout.Skills.Count == 0) _loadout.AddSkill();
                        _loadout.SetSkill(0, freshSig);
                    }
                }
                // `keystonenotice` — the LONGEST reveal the game can post. A keystone is announced with
                // what it DOES, not just its name, and CAPACITOR's sentence is 164 characters: its blurb
                // plus the line naming the rung that taught it. One body line silently cut that in half
                // for as long as the toast has existed, and nothing could photograph it, because no
                // capture mode had ever posted a long one. It is built here through the SAME helper the
                // live reveal calls, so what is photographed is the real string at its real length.
                //
                // MEASURED, NOT NAMED: the pose asks the catalogue which reveal is longest, so it stays
                // the worst case as keystones are added or their copy is rewritten. RH_SHOT_KEYSTONE=<id>
                // poses a specific one instead — `echo` is the shortest, the first conquest's reveal.
                if (sm == "keystonenotice")
                {
                    _showLoadout = true;
                    var shown = Environment.GetEnvironmentVariable("RH_SHOT_KEYSTONE")?.Trim() is { Length: > 0 } kid
                                ? Keystones.ById(kid) : LongestKeystoneReveal();
                    if (shown is not null) PostKeystoneReveal(shown);
                }
                if (sm == "weave")
                {
                    _showLoadout = true;
                    // DEEP AS WELL AS WIDE. The third keystone socket is bought with depth (wave 80) and
                    // vow capacity with conquests, so a fixture that conquers the world without ever
                    // saying how deep it went poses a hunter the game cannot produce.
                    _deepestEver = Math.Max(_deepestEver, Unlocks.ThirdSocketWave + 20);
                    // A WHOLE WORLD CONQUERED, which is what teaches the keystones now: six conquest
                    // doctrines plus WEAVER from the corruption, against three sockets. That is the case
                    // the chip list was silently unable to show — more keystones than visible chips —
                    // and it is also the ordinary late-career state rather than a bought fixture.
                    _world.RestoreConquered(Regions.All.Select(r => r.Id));
                    _world.RestoreCorruption(16);
                    // ...AND THE DEPTH THAT PAID FOR IT, per region. The comment above says a fixture
                    // that conquers the world without saying how deep it went poses a hunter the game
                    // cannot produce — and this one did exactly that, one level down: MASTERY POINTS
                    // are derived from each region's BEST DEPTH (SkillPointsEarned), never from
                    // _deepestEver, so the pose carried six conquests, a four-skill loadout, and an
                    // unlock ladder still saying "earn 7 mastery points for a SECOND skill slot".
                    // Nothing that reads Unlocks could be photographed truthfully here until now.
                    foreach (var def in Regions.All) _world.RegionFarm(def.Id).RecordDepth(_deepestEver);
                    // AND THE VOWS THE ACCOUNT HAS PROVED. A Vow is found by keeping its rule once
                    // without it, so the fixture states the account's knowledge directly rather than
                    // buying it: three found, which is a real mid-career collection and enough rows to
                    // photograph the capacity bound greying the third.
                    foreach (var id in new[] { "vow_complete", "vow_singular", "vow_unguarded" })
                        _discoveredVows.Add(id);
                    RebuildBuildMenus();
                    // THE ROADS THIS FIXTURE'S BUILD NEEDS — AND, DELIBERATELY, THE ONES IT DOES NOT.
                    // Every shared skill is behind a road now, so a fixture that walks none photographs
                    // four empty slots. But one that walks ALL TWELVE photographs a library with no
                    // LOCKED tile in it, and LOCKED is the state BRIEF sec.20 is about: a skill with
                    // waves spent on it and no node must read LOCKED · LEVEL n, never unlearned. So this
                    // pose walks four roads and leaves HAMMER's and DRAIN's shut, and spends waves on a
                    // shut one below — a state the screen must be photographed in, at every profile.
                    // RestoreTaken, not Take: walking them honestly would take a career.
                    _mastery.SetEarned(9999);
                    _mastery.RestoreTaken(_mastery.Taken
                        .Concat(new[] { "road_volley", "road_volley_2", "road_field", "road_field_2",
                                        "road_snare", "road_snare_2", "road_sign" })
                        .ToList(), repair: false);
                    ApplySkillCapacity();
                    // The gradual-unlock gate would hold a fresh fixture at two slots, and two slots
                    // is one active and one passive — not enough to show a build.
                    _loadout.SkillCapacity = Math.Max(_loadout.SkillCapacity, 4);

                    // SLOT 1 IS THE SIGNATURE. It is the one skill on this screen that no road opens and
                    // no other champion may hold, it now has its own pinned row at the top of the
                    // library, and a capture that never equips it cannot show either fact (sec.12).
                    if (_characters.Active.SignatureSkillId is { } sigId) _loadout.SetSkill(0, sigId);

                    // FOUR WOVEN SLOTS, AND BOTH KINDS. The fixture used to add ONE skill, which meant
                    // no capture of this screen could ever show a passive row — and three UI changes
                    // in a row went unphotographed because of it (the ACTIVE/PASSIVE switch, the
                    // resolved skill name, the slot badge). A fixture that cannot show the thing under
                    // test is a fixture that certifies nothing.
                    foreach (var (src, skillId) in new[]
                             {
                                 (Source.Mind, "volley_spray"),
                                 (Source.Nature, "field_mire"),
                                 (Source.Shadow, "snare_jaws"),
                             })
                    {
                        var slot = _loadout.AddSkill();
                        if (slot < 0) break;
                        _loadout.SetSource(slot, src);
                        _loadout.SetSkill(slot, skillId);
                    }
                    // A SHUT ROAD WITH WAVES BEHIND IT. hammer_blow's road is not walked in this pose, so
                    // its tile must read LOCKED and still show the level the player keeps (sec.20, LAW 4).
                    for (var u = 0; u < SkillProgress.UsesForLevel(4); u++) _skillProgress.RecordWave("hammer_blow");
                    _loadoutScreen.DevOpenSkillTree();
                    // ALL FOUR STATES OF A SKILL'S OWN LEVELS, one per slot, so a single shot certifies
                    // the whole ladder. Posing one state at a time is how the reinforcement strip could
                    // have shipped unphotographed the way the variation strip nearly did: a fixture that
                    // cannot show the thing under test certifies nothing.
                    //   slot 1 — a level waiting, nothing chosen      -> the two variation buttons
                    //   slot 2 — chosen, nothing spare                -> the name line, 0/3
                    //   slot 3 — chosen and a level spare             -> the three reinforcement buttons
                    //   slot 4 — chosen and fully reinforced          -> the name line, 3/3
                    for (var si = 0; si < _loadout.Skills.Count; si++)
                    {
                        var sk = _loadout.Skills[si];
                        if (SkillCatalogue.Find(sk.SkillId) is not { } rd) continue;
                        var level = si switch { 0 => 1, 1 => 1, 2 => 2, _ => SkillProgress.MaxLevel };
                        for (var u = 0; u < SkillProgress.UsesForLevel(level); u++) _skillProgress.RecordWave(rd.Id);
                        if (si == 0) continue;                       // left unchosen on purpose
                        var v = rd.Variations[0];
                        _skillProgress.ChooseVariation(rd, v.Name);
                        if (si < 3) continue;                        // slot 3 keeps its spare level
                        foreach (var r in v.Reinforcements) _skillProgress.TakeReinforcement(rd, r.Name);
                    }

                    // RH_SHOT_SWORN=<vowId>[,<vowId>...] SWEARS vows and leaves the list CLOSED, which
                    // is the only way to photograph the BUILD screen's vow READING. RH_SHOT_POSE below
                    // opens the swear list, and with no dial at all nothing is sworn — so the block that
                    // reports what is holding this hunter, the one surface where the build-level vow
                    // model is stated to the player, could not be posed by any mode. That is the state
                    // this project has been wrong about every time it could not photograph one.
                    //
                    // Vows are stored on slots because that is where a player swears them; the READING
                    // is of the whole build, so more than one id here is the case worth looking at.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_SWORN")?.Trim() is { Length: > 0 } swornSpec)
                    {
                        // The world grants vow capacity, and this fixture conquers regions above; without
                        // this the dial would be refused by a capacity of 1 that the pose does not have.
                        ApplyWorldGrants();
                        var known = Vows.Catalog.ToList();
                        var want = swornSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        for (var i = 0; i < want.Length; i++)
                        {
                            if (Vows.ById(want[i]) is null)
                                throw new InvalidOperationException(
                                    $"RH_SHOT_SWORN='{want[i]}' is not a vow id. Known: "
                                    + string.Join(", ", known.Select(v => v.Id)) + ".");
                            if (i >= _loadout.Skills.Count)
                                throw new InvalidOperationException(
                                    $"RH_SHOT_SWORN names {want.Length} vows and this pose has only "
                                    + $"{_loadout.Skills.Count} slots to record them on.");
                            if (!_loadout.SetVow(i, want[i], known))
                                throw new InvalidOperationException(
                                    $"RH_SHOT_SWORN='{want[i]}' was refused — vow capacity is "
                                    + $"{_loadout.VowCapacity}, and {i + 1} were asked for.");
                        }
                    }

                    // RH_SHOT_POSE=<vowId>[,<0..1>] opens a seal's BIND row and, with the second
                    // number, holds the bind chain part-played so the flourish can be photographed.
                    var wp = Environment.GetEnvironmentVariable("RH_SHOT_POSE");
                    if (!string.IsNullOrEmpty(wp))
                    {
                        var parts = wp.Split(',');
                        var chain = parts.Length > 1 && float.TryParse(parts[1],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var ct) ? ct : 0f;
                        _loadoutScreen.DevPose(1, parts[0], chain);
                    }
                    else _loadoutScreen.DevPose(1, null);

                    // RH_SHOT_PICK=<skillId> reads that skill in the INSPECTOR, which is the only way to
                    // photograph a LOCKED skill's reading and its LOCKED button (sec.20). Last, so it wins
                    // over the slot DevPose above.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_PICK") is { Length: > 0 } pickSkill)
                        _loadoutScreen.DevPickLibrary(pickSkill);
                }

                if (sm is "roster" or "rosterlocked")
                {
                    _showRoster = true;
                    if (sm == "roster")
                    {
                        _world.RestoreConquered(Regions.All.Select(r => r.Id));
                        foreach (var c in CharacterRoster.All)
                            if (c.Unlock.QuestId is { } q) _characters.CompleteQuest(q);
                        _characters.Refresh(_world.ConqueredIds);
                        _characters.Select("anvil");
                        _roster.DevSelect("oathbound");
                    }
                    else
                    {
                        // A save posed part-way into every second champion's quest, so the capture
                        // shows five live counts rather than five identical LOCKED cards — and two
                        // conquests, so the first Warden is READY beside the second Warden's count.
                        _world.RestoreConquered(new[] { VerdantHollow.RegionId, "cinderworks" });
                        _world.RegionFarm(VerdantHollow.RegionId).RestoreBestDepth(34);
                        _world.RegionFarm("cinderworks").RestoreBestDepth(23);
                        _forge.RestoreChestsOpened(12);
                        _runsWithVowKept = 1;
                        // The three counters no fixture posed, so the capture shows live progress on
                        // every gate rather than three zeroes: bosses felled, waves cleared with a
                        // VOLLEY skill, and a region held part-way to conquest (11 of 20 — under the
                        // threshold, so THE CHORUS is still locked and still counting).
                        _bossesFelled = 17;
                        for (var u = 0; u < 88; u++) _skillProgress.RecordWave("volley_spray");
                        _world.RegionFarm("umbral_reach").RestoreBestDepth(11);
                        _roster.DevSelect("quiver");
                    }
                }

                // rosterswitch: the frame after a switch — the toast, the moved gold edge and the
                // inspector's new state line, which is the feedback §86 asks for and which no fixture
                // could photograph.
                if (sm == "rosterswitch")
                {
                    _showRoster = true;
                    _world.RestoreConquered(Regions.All.Select(r => r.Id));
                    foreach (var c in CharacterRoster.All)
                        if (c.Unlock.QuestId is { } q) _characters.CompleteQuest(q);
                    _characters.Refresh(_world.ConqueredIds);
                    _characters.Select("seeker");
                    _roster.DevSelect("seeker");

                    // RH_SHOT_SHED=1 poses the REPAIR a switch performs, rather than a stand-in for it:
                    // weave the SEEKER's own skill, baseline the switch detector on the SEEKER, then
                    // become the ANVIL — so the first gameplay frame runs the real RepairForSwitch and
                    // the notice on screen is the one the game actually posts (BRIEF sec.13).
                    if (Environment.GetEnvironmentVariable("RH_SHOT_SHED") == "1")
                    {
                        if (_characters.Active.SignatureSkillId is { } leaving)
                        {
                            var sl = _loadout.AddSkill();
                            if (sl >= 0) _loadout.SetSkill(sl, leaving);
                        }
                        _lastActiveCharacterId = _characters.ActiveId;
                        _characters.Select("anvil");
                        _roster.DevSelect("anvil");
                    }
                    else
                        PostNotice("YOU ARE THE SEEKER",
                                   "SWITCHING IS FREE — YOUR SKILLS, TRAITS, GEAR AND THE WARREN STAY");
                }

                if (sm == "dust")
                {
                    _showTraits = true;
                    // `dusttree`, `traitlit` and the two `traitterm` spellings went with the Memory
                    // tree screen: they posed its constellation and its light-up flourish, and there is
                    // no counterpart on the new screen because there is nothing to buy.
                    _dust.AddDust(77_605);   // Dust still shows in the top pills — it is a material now

                    // ── THE TRAITS FIXTURE (BRIEF §109). Eleven awakened out of twenty-six, three of
                    //    them worn, and the rest reading `???` — which is the ONLY state that shows
                    //    all four things the screen is for at once: whose three these are, what has
                    //    awakened, what has not, and one of them read in the inspector.
                    //
                    //    An empty account was the alternative, and it is the trap the old tree
                    //    fixture fell into for a whole rebuild: the screen was reviewed on a
                    //    screenshot of its own empty state. Discovery is account-wide, so seeding the
                    //    ledger directly is what a mid-career save actually looks like — nothing here
                    //    is a state the game cannot reach.
                    // `posedTraits`, not `posed`: the VFX slice's RH_SHOT_HUNTER dial introduced a
                    // `posed` champion in an enclosing scope, and two fixtures naming one thing is how
                    // a fixture quietly poses the wrong state.
                    var posedTraits = new[]
                    {
                        "t_scar_tissue", "t_last_word", "t_deep_cut", "t_spill", "t_last_breath",
                        "t_certain_hand", "t_settling_weight", "t_homeground", "t_single_note",
                        "t_answering_wall", "t_lingering_mark",
                    };
                    // A PINNED TRAIT MUST BE ONE THE FIXTURE HAS AWAKENED, or the inspector reads
                    // nothing and the capture returns the screen's idle "PICK A CHARACTERISTIC" — a
                    // photograph of the default state wearing the filename of the state that was
                    // asked for. That is how both re-authored vow traits went unlooked-at: they are
                    // not among the eleven above, so RH_SHOT_TRAIT named them and posed nothing.
                    // Now the pinned one joins the awakened set, and an id no trait answers to aborts.
                    var pinnedId = Environment.GetEnvironmentVariable("RH_SHOT_TRAIT")?.Trim();
                    var toDiscover = posedTraits.ToList();
                    // HOW FAR ALONG THE ACCOUNT IS. The eleven above are one point on the discovery
                    // curve; RH_SHOT_AWAKENED=<n> poses the others, so the screen can be reviewed at
                    // the pace the catalogue is actually tuned to — a first sitting's one, an early
                    // account's three, a developed one's whole shelf — instead of only at the middle.
                    // The order is the fixture's own, so the same n always poses the same set.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_AWAKENED")?.Trim() is { Length: > 0 } awoke)
                    {
                        if (!int.TryParse(awoke, out var keep) || keep < 0)
                            throw new InvalidOperationException($"RH_SHOT_AWAKENED='{awoke}' is not a trait count.");
                        toDiscover = keep <= posedTraits.Length
                            ? posedTraits.Take(keep).ToList()
                            : posedTraits.Concat(TraitCatalogue.All.Select(t => t.Id).Where(id => !posedTraits.Contains(id)))
                                         .Take(keep).ToList();
                    }
                    if (pinnedId is { Length: > 0 } && !string.Equals(pinnedId, "unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TraitCatalogue.Find(pinnedId) is null)
                            throw new InvalidOperationException(
                                $"RH_SHOT_TRAIT='{pinnedId}' is not a trait id. Known: "
                                + string.Join(", ", TraitCatalogue.All.Select(t => t.Id)) + ".");
                        if (!toDiscover.Contains(pinnedId)) toDiscover.Add(pinnedId);
                    }
                    foreach (var traitId in toDiscover)
                        _traitLedger.Discover(traitId, new TraitFirst(_characters.ActiveId, _activeRegion, 41));
                    foreach (var traitId in new[] { "t_scar_tissue", "t_last_word", "t_last_breath" })
                        _traitLedger.Equip(_characters.ActiveId, traitId);
                    // The reveal has already happened for these — the rig must not fire eleven
                    // awakening toasts across the shot it is about to take.
                    _traitsFirstCheckOwed = false;
                    // WHAT THE INSPECTOR IS READING. Worn by default, because that is the reading with
                    // the most in it: the sentence, where it first awakened, and TAKE IT OFF rather
                    // than WEAR IT. RH_SHOT_TRAIT=<id> reads another; RH_SHOT_TRAIT=unknown poses the
                    // `???` reading, which no click in a capture could ever reach.
                    if (string.Equals(pinnedId, "unknown", StringComparison.OrdinalIgnoreCase))
                        _traitScreen.DevSelectUnknown(_traitLedger);
                    else
                        _traitScreen.DevSelect(pinnedId is { Length: > 0 } ? pinnedId : "t_last_word");

                    // THE AWAKENING PLATE (§32). RH_SHOT_WAKE=one poses one trait's reveal;
                    // RH_SHOT_WAKE=many poses the combined plate an established save gets on its
                    // first load. It fires once, in a moment nobody can schedule, so without this
                    // dial the reveal's own layout — three rungs at UI SCALE 150 — would never have
                    // been looked at, which is how every wrong state in this project has been found.
                    switch (Environment.GetEnvironmentVariable("RH_SHOT_WAKE"))
                    {
                        case "one" when TraitCatalogue.Find("t_scar_tissue") is { } woken:
                            PostAwakening("A TRAIT HAS AWAKENED", woken.Name, woken.Flavour.ToUpperInvariant());
                            break;
                        case "many":
                            PostAwakening("6 TRAITS HAVE AWAKENED",
                                          "WHAT YOU HAVE LIVED THROUGH CHANGED YOU",
                                          "READ THEM ON THE TRAITS SCREEN");
                            break;
                    }

                    // A CONQUERED WORLD, which is what teaches the keystones and opens the sockets
                    // now. The fixture used to buy tree nodes here; the world is the producer, so it
                    // sets the world and lets the derivation answer.
                    _world.RestoreConquered(Regions.All.Select(r => r.Id));
                    _world.RestoreCorruption(16);
                    _hunter.AddGleam(131_900_000);     // the top currency pills read like the reference
                    _hunter.AddMaterials(12_600);
                }

                // ── DISPATCHES. THE INBOX IS WRITTEN UNDER THE RIG, and only its SURFACING is posed.
                //    It is state, not a flourish: a fixture that suppressed production the way the
                //    trait awakenings are suppressed would photograph an empty panel forever. So the
                //    rows go in through the host's own PostDispatch — the same dedupe, the same
                //    arrival edge, the same copy the game renders.
                if (sm is "dispatches" or "dispatchesempty" or "dispatchesunread" or "dispatcheshover" or "chestdispatch")
                {
                    // RH_SHOT_DISPATCH=unread|read|many|<key> — which letter the pane is reading, and
                    // whether the list still has unread rows in it. An unparseable value ABORTS: a
                    // fixture that cannot pose what it was asked for must not return an image.
                    var posedMail = Environment.GetEnvironmentVariable("RH_SHOT_DISPATCH")?.Trim();
                    var manyLetters = string.Equals(posedMail, "many", StringComparison.OrdinalIgnoreCase);

                    // `dispatchesempty` is the one fixture with nothing in it: the panel's own empty
                    // state is a real reading and had no picture otherwise.
                    if (sm != "dispatchesempty")
                    {
                        PostDispatch(Dispatches.Unlock(Activity.Warren, 1_000));
                        PostDispatch(Dispatches.Trait("t_scar_tissue", 2_000));
                        PostDispatch(Dispatches.Region(VerdantHollow.RegionId, 3_000));
                        // ...AND A COLUMN THAT OVERFLOWS. Three letters fit in every profile's list,
                        // so the scrollbar, the wheel and the keyboard's follow-the-cursor had no
                        // fixture at all — which is exactly why a scroll that could not scroll went
                        // unseen. `many` fills past the shortest column (nine rows, at 150 %).
                        if (manyLetters)
                        {
                            var at = 3_000L;
                            foreach (var region in Regions.All.Where(r => r.Id != VerdantHollow.RegionId))
                                PostDispatch(Dispatches.Region(region.Id, at += 1_000));
                            foreach (var trait in TraitCatalogue.All.Take(7).Where(t => t.Id != "t_scar_tissue"))
                                PostDispatch(Dispatches.Trait(trait.Id, at += 1_000));
                            foreach (var screen in new[] { Activity.Vault, Activity.Forge, Activity.Map, Activity.Mastery })
                                PostDispatch(Dispatches.Unlock(screen, at += 1_000));
                            PostDispatch(Dispatches.Gem(at += 1_000));
                            PostDispatch(Dispatches.Socket(at += 1_000));
                        }
                    }
                    // The two panel fixtures open it through the same door the envelope and M use.
                    if (sm is "dispatches" or "dispatchesempty") OpenDispatches();

                    switch (posedMail?.ToLowerInvariant())
                    {
                        case null or "" or "unread" or "many":
                            // The newest open, the rest waiting — a MIXED list.
                            _dispatchOpenKey = _inbox.Rows.Count > 0 ? _inbox.Rows[0].Key : null;
                            break;
                        case "read":
                            _inbox.MarkAllRead();        // nothing waiting: no dots, MARK ALL READ dead
                            _dispatchOpenKey = _inbox.Rows.Count > 0 ? _inbox.Rows[0].Key : null;
                            break;
                        default:
                            if (_inbox.Rows.FirstOrDefault(d => string.Equals(d.Key, posedMail, StringComparison.OrdinalIgnoreCase)) is not { } posedLetter)
                                throw new InvalidOperationException(
                                    $"RH_SHOT_DISPATCH='{posedMail}' is not a seeded dispatch key. Seeded: "
                                    + string.Join(", ", _inbox.Rows.Select(d => d.Key)) + ".");
                            _dispatchCursorKey = _dispatchOpenKey = posedLetter.Key;
                            break;
                    }
                }
            }
            else {
            if (Pressed(Keys.F10)) _showSettings = !_showSettings;

            if (_showSettings)
            {
                // Escape peels one layer: an open dropdown list first (spent in the block above),
                // then the panel.
                if (!_settingsEscSpent && Pressed(Keys.Escape)) _showSettings = false;
                // The panel's controls are decided in UpdateSettings (ADR-006), and this branch
                // returns before the modal block at the end of Update that runs it for the game --
                // so it runs here, or a panel opened from the title paints and takes no click.
                else UpdateSettings();
                Latch(gameTime);
                return;
            }

            if (Pressed(Keys.Up)) _titleCursor = (_titleCursor + TitleItems - 1) % TitleItems;
            if (Pressed(Keys.Down)) _titleCursor = (_titleCursor + 1) % TitleItems;
            if (Pressed(Keys.Enter) || Pressed(Keys.Space)) ChooseTitleItem(_titleCursor);

            // ── AND THE MOUSE, HERE, IN UPDATE. ─────────────────────────────────────────────────
            //
            // This hit test lived inside DrawTitle, against the same latched `_clicked`, and it could
            // DROP A CLICK. MonoGame's default IsFixedTimeStep is never overridden in this project, and
            // a fixed-timestep tick makes AT LEAST ONE call to Update and exactly one to Draw — so on a
            // catch-up tick (a frame over 16.6 ms: the profile change that re-keys the glyph atlas at
            // 150 % is a real one) Update runs twice and Draw once. The first Update latches the edge;
            // Latch() then copies _mouse into _prevMouse; the second Update recomputes the edge as
            // false; and the single Draw that follows hit-tests an edge that is already gone. A human
            // holds the button for three to six frames, so it never re-arms — the press is simply lost.
            //
            // Consumed in the Update that latched it, the edge cannot be lost and cannot be spent
            // twice: exactly one press edge exists per Update, and only the Update that sees it acts.
            // The plates are laid out by TitlePlate(), which is what Draw draws — one rule, so the
            // thing hit-tested and the thing painted can never disagree.
            //
            // The same shape as the prologue's own buttons (Game1.Opening.cs TakeOpeningInput), the
            // settings gear, and every screen Mastery and Gear already converted.
            for (var i = 0; i < TitleItems; i++)
            {
                var plate = TitlePlate(i);
                if (!plate.Contains(ChromeMouse)) continue;
                _titleCursor = i;
                if (_clicked) { ChooseTitleItem(i); break; }
            }

            // An EDGE, not the held key: the Escape that just closed the settings panel is still down on
            // this frame, and a held read here quit the game on it.
            if (Pressed(Keys.Escape)) Exit();
            Latch(gameTime);
            return;
            }
        }

        // ── NO TOUR STARTS ITSELF ANY MORE. ─────────────────────────────────────────────────────
        //
        // A fresh save used to open onto a fight and, eight seconds later, an eight-card modal about
        // the hunter, the enemies, the health bar, Gleam, the skills, the rewards, the rail and the
        // lesson slot — before the player had watched a single wave. Every other screen then did the
        // same on its first open: four cards before you could equip anything, four before you could
        // upgrade anything.
        //
        // The first experience is now the fight itself, and the coach says one short thing at a time
        // (OnboardingDirector). THE TOURS SURVIVE WHOLE — their copy is good and recently corrected —
        // as LEARN THIS SCREEN, the ? beside the settings gear, which is where somebody who wants the
        // whole screen explained can ask for it. `_introDecided` remains so the rig can still pose a
        // card by number.
        if (!_introDecided)
        {
            _introDecided = true;
            if (CaptureRig && ShotMode == "intro") BeginTour(Activity.Hunt);
        }

        // THE CAPTURE RIG still starts a screen's tour on demand — RH_SHOT_MODE=tour owes exactly one
        // screen through SeedExplained, and nothing else in the game does.
        if (CaptureRig && !_tourActive && ShotMode == "tour"
            && Onboarding.TourDue(ScreenActivity(), _explained) is not null)
            BeginTour(ScreenActivity());
        if (CaptureRig && !_tourActive && ShotMode == "gemtour"
            && ScreenActivity() == Activity.Forge && _showForge
            && Onboarding.GemTourDue(GemsHeld(), _explained) is { } gemKey)
        {
            _forge.RequestSocketTab();
            BeginTour(Activity.Forge, Onboarding.GemTourFor(_forge.FreeSocketUsed), gemKey);
        }

        // And the moment the first one DROPS, a line at the top says where it goes — never a panel —
        // and the FORGE tile earns its unread dot back even if the Forge was visited earlier this
        // session. The DEED itself is the coach's FirstGemSocket lesson, which waits on a gem being
        // set and not on this notice being read.
        var gemsHeld = GemsHeld();
        if (gemsHeld > _gemsHeldLast && Onboarding.GemTourDue(gemsHeld, _explained) is not null)
        {
            PostNotice("A GEM DROPPED",
                       GemCraft.IsFirstGemFree(_forge.FreeSocketUsed)
                           ? "THE FORGE'S SOCKET TAB SETS IT INTO AN ITEM — YOUR FIRST GEM IS FREE"
                           : "THE FORGE'S SOCKET TAB SETS IT INTO AN ITEM");
            _visited.Remove(Activity.Forge);
        }
        _gemsHeldLast = gemsHeld;

        // Escape backs out of an open panel before it quits the game. Escape is the reflex for "get me
        // out of this menu" — and in fullscreen it is the reflex for "give me my desktop back". Wiring
        // it straight to Exit() meant a player poking at the display options quit to desktop instead of
        // closing them, which in an idle game is a genuinely expensive misfire.
        // Edge, not held-state: with IsKeyDown, holding Escape closes the panel on one frame and quits
        // on the very next one — the fix would have quietly kept the bug.
        // ESC OPENS SETTINGS NOW, closing whatever modal is up first. It used to quit the game — the
        // standard PC reflex "Esc = menu" was wired to the most destructive thing the program can do.
        // Quitting lives ON the settings panel (SAVE AND QUIT), where it says what it does.
        if (!_settingsEscSpent && Pressed(Keys.Escape))
        {
            if (_showSettings) _showSettings = false;
            else if (_showHelp) _showHelp = false;
            // DISPATCHES is a host panel like the two above it, and Esc means "close the letters".
            else if (_showDispatches) _showDispatches = false;
            // THE OPEN EXPEDITION LOG is the next layer down: Esc on it means "close the log" — the one door
            // out of a full-screen read that needs no aim — never "open settings over it".
            else if (_expedition.LogOpen) _expedition.ToggleLog();
            // A pending SELL/SALVAGE question outranks the settings reflex: Esc on it means "keep it",
            // not "open another panel over the question". (Adversarial review, pass four.)
            else if (_showForge && _forge.ConfirmOpen) _forge.CancelConfirm();
            // The vault's modals outrank the settings reflex too — Esc on the stall means "close
            // the stall", not "stack the settings panel on top of it".
            else if (WelcomeUp) _showWelcome = false;   // Esc reads the welcome as read
            else if (_showVault && _vault.ModalUp) _vault.CloseModals();
            // The vault's CHEST FILTER popover is the same kind of thing: Esc on it means "close it".
            else if (_showVault && _vault.FilterOpen) _vault.FilterOpen = false;
            else _showSettings = true;
            _settingsEscSpent = true;   // one Escape, one layer: the opening's own Escape below must not also act on it
        }

        // THE ONE LINE THAT SAYS THE GAME HAS STARTED MUST SURVIVE THE INTRO. _bootMessage on a fresh
        // save is "YOUR HUNTER IS ALREADY FIGHTING / WATCH THE FIRST WAVES", and DrawBootToast refuses
        // to draw it under the intro's scrim — correctly, it would be a dim duplicate. But its clock
        // ran anyway, so all seven seconds burned behind eight cards and the toast was gone before the
        // player ever saw the arena. Playtest 2026-09-09: "the game starts running in the background
        // while the tutorial is active, and the player misses this; we need a way to indicate that the
        // game has started." Holding the clock is the whole fix: the nudge now lands the instant the
        // scrim lifts, which is the first moment it could be read.
        // ── THE AUTHORED OPENING RUNS FIRST, because everything below reads its decision. ───────
        //
        // The fight advance, the Warren tick, the input authority and the rail's gate all ask this
        // object what the current beat allows, and a beat that means to freeze the game has to have
        // said so before the game moves.
        UpdateOpening(dt);

        // THE TOASTS' CLOCKS RUN ONLY WHILE THEIR TOASTS CAN BE READ. Feedback tier: under a lit lesson,
        // the open log, a fall, a reveal, a modal or the opening the boot line waits with its clock held,
        // and lands the instant the frame is handed back rather than expiring behind whatever owned it.
        // The owner is last frame's — it is decided further down, once this frame's flags have settled —
        // and one frame is a frame nobody sees. The locked-tile refusal may simply expire: a refusal is
        // over the moment it was seen, or not.
        if (_bootTimer > 0f && !AttentionOwnedAbove(AttentionOwner.Feedback)) _bootTimer = Math.Max(0f, _bootTimer - dt);
        if (_lockedTimer > 0f) _lockedTimer = Math.Max(0f, _lockedTimer - dt);
        // Notice toasts: one at a time, each for NoticeSeconds, the next one only once the last has
        // gone. Ticks here, past the title return, so only seconds of actual play count.
        // THE RAIL GROWS (the journey, 2026-09-06): a screen whose need has just become real is
        // revealed once — its tile appears, this notice says what opened and why, and the gold NEW
        // waits on the tile until the player looks (Onboarding.IsNew). Read every frame from the
        // same facts the gates read; Reveal.Newly returns nothing on nearly every one of them.
        // UNDER THE CAPTURE RIG a fixture dresses its facts after the seed, so every screen it opens
        // would toast over the thing being photographed; the rail still grows, silently, and
        // RH_SHOT_REVEAL=<Activity> names the one reveal a capture wants to see announced.
        if (_revealSeeded)
            foreach (var opened in Reveal.Newly(_revealed, GuideUnlockFacts()))
            {
                // THE CHAINS COME OFF THE TILE ON THE SAME FRAME the notice says why. The tile has been
                // standing there bound since the first launch, so the reveal is a thing the player can
                // SEE happen on the rail rather than a row quietly appearing under the last one.
                UiMotion.Flash(NavBreakKey(opened), NavChainSeconds);
                OpeningRigMark($"NAV_BREAK {opened}", $"break_{opened}", 60, 2);   // dev: the opening rig's trace and film
                if ((!CaptureRig || string.Equals(Environment.GetEnvironmentVariable("RH_SHOT_REVEAL"), opened.ToString(), StringComparison.OrdinalIgnoreCase))
                    && !OpeningWalksInto(opened))
                    PostNotice($"NEW — {Unlocks.Headline(opened)}", Unlocks.OpenedLine(opened));
            }

        // A QUEUE, NOT A STACK — the rule this codebase already keeps for the toast slot, applied to
        // the band above a menu screen's page. A slot REVEAL and a notice both stand there, and drawing
        // both put QUEST COMPLETE through "You can equip a second skill." Stacking them instead cost
        // the page two lanes and squeezed four labels into ellipses at 150 %. So the reveal, which
        // waits for a click, holds the band; the notice, which is a few seconds, waits — AND ITS CLOCK
        // IS HELD, so it lands the moment the card closes rather than expiring unseen behind it.
        // ...AND THE SAME UNDER ANYTHING THAT OUTRANKS IT. The toast is Feedback tier: a lit lesson, the
        // open log, a fall, a reveal, a modal and the opening all own the frame over it, so its clock
        // holds while any of them is up, and the NEXT one is not dequeued either — a toast dequeued
        // under an owner played its cue for a plate nobody could see.
        if (_noticeTimer > 0f && !NoticeHeld && !AttentionOwnedAbove(AttentionOwner.Feedback)) _noticeTimer = Math.Max(0f, _noticeTimer - dt);
        if (_noticeTimer <= 0f && _noticeQueue.Count > 0 && !AttentionOwnedAbove(AttentionOwner.Feedback))
        {
            _notice = _noticeQueue.Dequeue();
            // AN AWAKENING HOLDS LONGER, because it has a third line to read and because it is the
            // rarest thing this toast slot ever says. Still a toast and not a modal: §32 asks for a
            // meaningful reveal and warns in the same breath against a blocking ceremony.
            _noticeTimer = _notice.Awakening ? NoticeSeconds * 1.6f : NoticeSeconds;
            _sound.PlayFirst(0.9f, _notice.Awakening ? "sfx_trait_lit" : "sfx_levelup", "sfx_levelup", "sfx_click");
        }

        // Autosave. An idle game that loses your farm to a crash has taken your hours, not your time.
        _sinceAutosave += dt;
        if (_sinceAutosave >= AutosaveIntervalSeconds)
        {
            _sinceAutosave = 0f;
            Save();
        }

        // Every staffed farm runs, every frame, on every screen — including mid-combat. This is the
        // whole point of an idle game, and once you have conquered regions, all of them farm at once.
        // ...AND THE CAMP STANDS STILL WITH IT. A paused tutorial that let the Warren keep paying
        // would be visible progression during a frozen frame, which is exactly what "the player must
        // see a frozen game" forbids. (Offline production is untouched — that is a different clock,
        // and the opening suppresses it separately by not releasing the career into idle time yet.)
        if (!_opening.HoldsFight) TickWarren(dt);

        // The dev rig-spike tech demo moved OFF Tab (F9) — Tab is the Forge's loot filter, and the global
        // binding here ran first every frame, hijacking the filter into a blank dev screen.
        // BEHIND RH_DEV=1, both of them. F6 forced a boss and F7 covered every screen with layout
        // overlays, and they sat one key away from F1 (help) and F10 (settings), live in normal play —
        // a playtester could trip either and reasonably conclude the game was broken.
        if (DevKeysEnabled && Pressed(Keys.F6)) _expedition.DevForceBoss = !_expedition.DevForceBoss;   // dev: force the Crystal Lich boss render (Rev 4 §12)
        // F9, NOT F8. F8 already cycles the UI SCALE two lines below — the design for this phase said
        // "F8 is free" and the code says otherwise, so one press would have toggled the overlay AND
        // stepped the density profile, which reads as the overlay breaking the layout it is measuring.
        if (DevKeysEnabled && Pressed(Keys.F9)) _expedition.DevVfxDebug = !_expedition.DevVfxDebug;   // dev: the VFX placement contract's bounds/anchor/ratio overlay (brief §70)
        if (DevKeysEnabled && Pressed(Keys.F7)) { _expedition.DevBossDebug = !_expedition.DevBossDebug; _gear.DevGearDebug = !_gear.DevGearDebug; _training.DevStatsDebug = !_training.DevStatsDebug; _masteryScreen.DevBuildDebug = !_masteryScreen.DevBuildDebug; _forge.DevForgeDebug = !_forge.DevForgeDebug; _warrenScreen.DevWarrenDebug = !_warrenScreen.DevWarrenDebug; _mapScreen.DevMapDebug = !_mapScreen.DevMapDebug; }   // dev layout overlays
        if (DevKeysEnabled && Pressed(Keys.F8)) CycleUiScale();   // dev: UI SCALE 100 / 125 / 150 / AUTO, until the settings row lands (UX V2 P3.1)
        // ONE HOST PANEL AT A TIME, as the nav's own entries already insist: the modal block below runs
        // exactly one of the two, so a HELP stacked under SETTINGS painted its close icon dead.
        if (Pressed(Keys.F1)) { _showHelp = !_showHelp; if (_showHelp) _showSettings = false; }
        if (Pressed(Keys.F10)) { _showSettings = !_showSettings; if (_showSettings) _showHelp = false; }
        // ...AND M IS THE THIRD PANEL'S KEY. DISPATCHES swaps with the other two exactly as they swap
        // with each other, and the two lines above are untouched: read AFTER them, M wins its own press
        // and the else below closes the letters behind an F1 or F10 pressed over them. M is free — the
        // rail's nine tiles are H C V B E K F A W P R, and L and T are the log and the weave.
        if (Pressed(Keys.M)) { if (_showDispatches) _showDispatches = false; else { OpenDispatches(); _modalOpenedNow = true; } }
        else if (_showDispatches && (_showSettings || _showHelp)) _showDispatches = false;
        // A HOST PANEL HOLDS THE HOST'S OWN KEYS. The panels eat the frame's clicks (MouseClicked) but the
        // hotkeys below read Pressed(), which only the tour, the opening and the welcome silence — so B
        // under SETTINGS opened BUILD beneath the panel, and L opened the log under it. Esc, F1 and F10
        // above stay raw: they are how the panels open and close. Read once, here, for every key below.
        var panelHolds = HostModalUp;
        // CONTINUE by key -- RAW edges, because the welcome joins the frame's swallow below and Pressed()
        // honours it. Escape reads the welcome too, one layer per press.
        if (WelcomeUp && (KeyEdge(Keys.Enter) || KeyEdge(Keys.Space) || (!_settingsEscSpent && KeyEdge(Keys.Escape)))) _showWelcome = false;
        // ...AND BY MOUSE, HERE. It used to be decided inside DrawWelcomePanel, so on a catch-up tick
        // (Update, Update, Draw) the edge was gone before the button was tested and the panel ignored
        // the press. Raw `_clicked`, as the drawn button used: this panel IS the modal.
        if (WelcomeUp && _clicked && _welcomeContinue.Contains(ChromeMouse)) _showWelcome = false;
        // THE WELCOME IS A MODAL FOR INPUT, NOT FOR THE FRAME. It is up on the very first frame after a load,
        // and an early return here would skip the block below that feeds every screen its dependencies —
        // the first Draw would then hit a null (the boot check caught exactly that). So the frame runs on
        // and only the input is spent: the welcome is a term of the frame's swallow below, beside the tour
        // and the opening. (A `_swallowInput = true` here was overwritten by that line and never held.)

        // A modal eats the frame's input, but NOT the frame. The farms above still tick and the
        // autosave above still fires — an idle game does not pause because you opened a menu. What it
        // must not do is let the hotkeys and buttons underneath the panel keep responding: without
        // this, clicking FULLSCREEN also presses whatever the panel happens to be covering.
        // THE SETTINGS RETURN MOVED BELOW UpdateExpedition — see the note beside it. It used to sit
        // here, which stopped the champion fighting for as long as the display-options panel was open:
        // the one overlay in the game that paused the idle loop, contradicting the comments on both
        // sides of it. Its INPUT gating is unaffected, because `watchingFight` already carries a
        // !_showSettings term.

        // A TOUR SWALLOWS INPUT while it is up: it is drawn over the nav rail, so a click meant to
        // advance it would otherwise also land on whatever tile is underneath and throw the player onto
        // a screen they did not ask for.
        //
        // IT DOES NOT RETURN EARLY, and that is not a style choice. The settings panel can, because it
        // is impossible to have open on the first frame. This one is not: the capture rig's
        // RH_SHOT_MODE=intro begins the Hunt's tour on the first gameplay frame, so an early return
        // here would skip the per-frame block that feeds every screen its dependencies, and the
        // first Draw would hit a null Loadout in TrainingScreen. check_boot.sh caught exactly that
        // shape twice before, under the modal panel this replaced. Swallow the input; never skip
        // the frame.
        //
        // The champion keeps fighting behind it: the return summary is news, and news does not stop
        // the world. (The AUTHORED OPENING is the one thing in this game that does stop it — see
        // OpeningDirector.HoldsFight — and it is not this.)
        // THE ORDER HERE IS THE WHOLE FIX. The first version of the panel this replaced cleared a local
        // flag inside the dismissal branch and then assigned _swallowInput from it — so on the very
        // frame the dismissing click was consumed, _swallowInput came out FALSE while _clicked was
        // still latched true for the rest of the frame. The click closed the panel AND went on to hit
        // HandleNavClick, all nine hotkeys and every button hit-tested during Draw. The swallow is set
        // BEFORE the branch and never cleared by it.
        //
        // A TOUR IS THE ONLY THING THAT SETS THIS TRUE FOR A WHOLE FRAME. The notice toasts and the
        // slot-note banner are deliberately not modal: a toast never touches this flag, and the banner
        // only spends the one click that lands on it (below, beside the hint slot's close).
        // A TOUR OR THE AUTHORED OPENING. The opening is the second thing in this game allowed to
        // own a frame, and it is allowed for the reason the tour is: while it holds the player it is
        // the only thing being asked of them. A LiveExplain step owns nothing — see OwnsInput.
        _swallowInput = _tourActive || _opening.OwnsInput || WelcomeUp;
        // NOT UNDER THE CAPTURE RIG. The game window takes focus while a shot renders, so a key the
        // developer happens to press in the sixty frames advances the card — a capture asked for card
        // five came back as card six. The rig poses a card by number; it never plays.
        if (_tourActive && !CaptureRig)
        {
            // ── THE TOUR ADVANCES FROM ITS OWN CARD, AND FROM NOWHERE ELSE. ─────────────────────
            //
            // It used to advance on ANY click and ANY key, anywhere on the screen. Playtest
            // 2026-09-09: "while in the tutorial, clicking outside the screen also fast-forwards it."
            // Worse than an accident — it made the tour's own spotlight into a lie. The ring goes
            // round a real control, the card beside it says what that control does, and pressing the
            // control skipped the explanation instead of using it. Every gold ring in the intro was
            // a button that did the wrong thing.
            //
            // Now: the CARD is the control. A click on it (or its NEXT button) continues, SPACE and
            // ENTER continue, ESC skips, and a click anywhere else is swallowed and does nothing —
            // which is what a modal means. The ring is a pointer now, not a button; see DrawTour.
            //
            // Raw edges, not Pressed(): Pressed reads !_swallowInput, which is already true.
            var escape = _keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape);
            var keyGo = (_keys.IsKeyDown(Keys.Space) && _prevKeys.IsKeyUp(Keys.Space))
                        || (_keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter));
            var clickGo = _clicked && _tourCard.Contains(ChromeMouse);
            if (escape) EndTour();
            else if (keyGo || clickGo)
            {
                if (_tourStep + 1 >= _tour.Count) EndTour();
                else _tourStep++;
                _sound.Play("sfx_click", 0.7f);
            }
            // _swallowInput deliberately STAYS true whatever happened: a click that missed the card
            // is spent here rather than reaching the frozen screen underneath.
        }

        // A CHEST REVEAL IS MODAL TOO: while it is up, a click (or Space / Enter) advances or skips IT —
        // never the screen underneath. Without this, skipping the cascade in the VAULT would also press
        // whatever card happened to sit under the cursor, opening a chest the player never chose.
        // (_forge is unconditionally built in LoadContent, which MonoGame runs before the first Update
        //  — an `is not null` here would teach flow analysis it can be null and warn at every later use.)
        if (_forge.RevealActive)
        {
            // NOT raw _clicked: on the frame a click dismisses the unlock panel above this overlay,
            // _swallowInput is already true and that click is SPENT — feeding it here too skipped the
            // cascade the player was trying to uncover. (Adversarial review, pass four.)
            var revealClick = _clicked && !_swallowInput;

            // THE REVEAL HAS BUTTONS NOW — hover to read an item, SELL or SALVAGE it where it lies,
            // KEEP ALL to close. So "a click while the reveal is up" is no longer one thing. The screen
            // is asked first whether the pointer is on something of its own; if it is, the click is
            // HANDED TO IT (spent in its Draw, which is where every rect on it is authored) and must
            // NOT also skip — a click that sells an item and dismisses the card that said so is the
            // worst of both. Everything else still skips, for everyone who does not care.
            //
            // ChromeMouse, not PageCursor: the reveal is drawn in BeginCanvas(1), true 1920x1080.
            var onRevealButton = revealClick && _forge.RevealWantsClick(ChromeMouse);
            _forge.RevealInput(ChromeMouse, onRevealButton);
            // ...and not from under a host panel: Space with SETTINGS over the reveal advanced the card the
            // panel was covering.
            if (!onRevealButton && (revealClick || (!panelHolds && (Pressed(Keys.Space) || Pressed(Keys.Enter)))))
                _forge.AdvanceReveal();

            // The reveal's question carries the same "don't ask me again" box the bench's does, and the
            // bench's poll lives inside the _showForge branch below — which this reveal never reaches
            // (it plays on whatever screen the chest was opened from). Without this the box ticked, the
            // preference flipped for the session, and the next launch asked again.
            if (_forge.PrefsDirty) { _forge.PrefsDirty = false; _askBeforeScrap = _forge.AskBeforeScrap; SaveDisplay(); }

            _swallowInput = true;
        }

        // THE NOTICE TOAST'S ×, now that this frame's click edge is latched and its swallow is settled
        // (it used to run at the top of Update, against LAST frame's edge, and its swallow was then
        // overwritten). Read here, it spends the click before the gear, the hint slot, the nav and the
        // screens can.
        DismissNoticeIfClosed();

        // THE SETTINGS GEAR, top-right of every screen. Handled here, before the nav and the screens,
        // so its click never falls through to whatever sits underneath it.
        if (!HostModalUp && !_swallowInput && _clicked && SettingsGear.Contains(ChromeMouse))
        {
            _showSettings = true;
            _swallowInput = true;
            _modalOpenedNow = true;   // the panel's own input runs later this Update — see the field
        }

        // ...AND THE ? BESIDE IT, on the same terms and for the same reason: it sits over the screen,
        // so its click is spent here rather than falling through to whatever it covers.
        if (!_swallowInput && TakeLearnClick()) _swallowInput = true;

        // ...AND THE ENVELOPE BESIDE THAT. Same terms again: it sits over the screen, so its click is
        // spent here rather than falling through to whatever it covers. DispatchesOffered is the one
        // question DrawDispatchButton asks too — what is painted is what is hit-tested.
        // _modalOpenedNow for the gear's reason: the panel's own input runs later this Update, and
        // without the mark the opening click would also land on the list row under the cursor.
        if (!_swallowInput && _clicked && DispatchesOffered() && DispatchButton.Contains(ChromeMouse))
        {
            OpenDispatches();
            _swallowInput = true;
            _modalOpenedNow = true;
        }

        // THE HINT SLOT at the top of a menu screen — a slot note, the lesson about this screen, or a hint
        // from real state — closes with its × (a note and a lesson are remembered in the save; a hint for
        // the session), and a click anywhere else on it is spent: it sits over the screen's own controls,
        // and a click that closed nothing must not press a TRAIN button underneath. THE HUNT'S LESSON
        // CARD closes the same way (playtest: "messages stay forever until I do the thing"). Display only:
        // no underlying fact is faked, so unlocks and gates are untouched. Handled here rather than in
        // Draw so the click is swallowed before any screen hit-tests it.
        if (!HostModalUp && !_swallowInput && _clicked)
        {
            // THE COACH'S CARD FIRST, because it is drawn over everything else and its close is the
            // only click it owns. The card is NOT modal: a click anywhere else — including the lit
            // control it is pointing at — falls straight through to the screen, which is the whole
            // difference between this and a tour.
            if (_coach.Showing is { } lit && CoachLightsIt(lit) && _coachCard != Rectangle.Empty
                && HintCloseRect(_coachCard).Contains(ChromeMouse))
            {
                CloseLesson(lit);
                _swallowInput = true;
            }
            else if (SlotShowing() is { } slot)
            {
                var rect = HintSlotRect(slot);
                if (HintCloseRect(rect).Contains(ChromeMouse)) { CloseSlot(slot); _swallowInput = true; }
                else if (rect.Contains(ChromeMouse)) _swallowInput = true;
            }
            else if (HuntLessonShowing() is { } lesson && HintCloseRect(HuntLessonRect(lesson)).Contains(ChromeMouse))
            {
                CloseLesson(lesson);
                _swallowInput = true;
            }
        }

        // THE ATTUNEMENT holds the door — and so do the vault's modals (the trader stall and the
        // code-inspect cards). A modal can only swallow its own screen's input; the rail, the nine
        // hotkeys, L and T are the host's, so the host holds them while any modal is open.
        var ceremonyHolds = (_showMastery && _masteryScreen.SpecialisationOpen)
                              || (_showVault && _vault.ModalUp);
        // ...AND SO DOES THE OPEN EXPEDITION LOG, a full-screen read: a tile or a hotkey that switched the
        // screen beneath it left the log painting through the overlay's inset transform while its
        // hit-tests stayed in plain 1920 space. Its own doors (BUILD, GEAR, the ×, L, Esc) are the way
        // out; the rail is furniture until it closes, and is not painted (DrawHexNav).
        var navHolds = ceremonyHolds || panelHolds || _expedition.LogOpen;

        if (!ceremonyHolds) HandleNavClick();   // a click on the shared hex nav works from any screen; it refuses the panels and the log itself

        // EVERY NAV HOTKEY GOES THROUGH OpenNav — the unlock gate, the refusal toast and the
        // flag-clearing all live in exactly one place now.
        //
        // THEY DID NOT BEFORE, AND THAT HOLE SWALLOWED THE WHOLE GRADUAL-UNLOCK FEATURE. NavUnlocked was
        // consulted from OpenNav (mouse) and DrawHexNav (dimming) and nowhere else, so the nine keyboard
        // bindings — written long before the gating pass — set the _showX flags directly. A first-run
        // player who pressed B landed on the full Build screen while the rail tile beside it read "THE
        // BUILD IS NOT OPEN YET — REACH WAVE 5". Nine destinations at once, the exact wall the feature
        // exists to remove, was one keypress away. Worse, the tutorial's own bodies are a list of these
        // keys: the guide was teaching the bypass.
        //
        // Each handler also cleared its own idiosyncratic subset of the flags, and three of them forgot
        // _showRoster and _showLoadout — so the player could see one screen while an invisible one
        // consumed their clicks. OpenNav clears all nine, every time.
        for (var navKey = 0; navKey < Nav.Length && !navHolds; navKey++)
        {
            // Keys.A..Keys.Z are the ASCII letter codes, so the table's char IS the key.
            if (!Pressed((Keys)Nav[navKey].Key)) continue;
            // Pressing the key of the screen you are already on returns you to the hunt, preserving the
            // toggle these handlers used to have — EXCEPT from the Weave. The Weave lights the BUILD
            // tile (it is a sub-screen of it), so NavActive() reports 3 there, and pressing B to "go to
            // BUILD" read as "you are already on BUILD" and dropped the player onto the fight instead.
            OpenNav(NavActive() == navKey && !_showLoadout ? 0 : navKey);
            break;
        }
        // L — THE EXPEDITION LOG. It closes every other overlay, because it is a full-screen read and
        // the one thing the player opens specifically to think, not to act.
        // The hunt screen's LOG button raises WantsLog (drawn last frame); it is the same door as L.
        var wantsLog = _expedition.WantsLog;
        _expedition.WantsLog = false;
        if ((Pressed(Keys.L) || wantsLog) && !ceremonyHolds && !panelHolds)
        {
            _expedition.ToggleLog();
            if (_expedition.LogOpen)
            {
                // READ THE LOG, ANSWERED. Opening a report changes nothing in the world, so this is one
                // of the three onboarding facts the save has to keep — and it is the first half of the
                // only FTUE milestone worth measuring: opened the report, changed something, went back
                // down. It latches once and is never unlatched.
                if (!_reportOpenedEver) { _reportOpenedEver = true; Save(); }
                // ALL NINE, not seven. _showRoster and _showLoadout were missing, so opening the log from
                // the roster or the weave left that screen live underneath it — and because the log is
                // drawn in the same batch, OverlayActive stayed true and the batch kept the OVERLAY
                // inset transform instead of the plain canvas one. DrawLog's own hit-tests assume the
                // plain one, so its page buttons landed in a third coordinate space.
                _showTraits = _showWarren = _showForge = _showWorld = false;
                _showMastery = _showGear = _showTraining = _showRoster = _showLoadout = _showVault = false;
            }
        }
        if (_expedition.LogOpen && !panelHolds)
        {
            if (Pressed(Keys.Left)) _expedition.StepLog(1);    // left = older
            if (Pressed(Keys.Right)) _expedition.StepLog(-1);
        }

        // T — THE WEAVE. The one screen with no rail tile of its own, so it cannot go through OpenNav;
        // it is gated on Activity.Build, which is the screen it is reached from and the thing it is
        // part of. Without this it was the last remaining way to walk past the unlock gate.
        if (Pressed(Keys.T) && !navHolds)
        {
            if (!Unlocks.IsOpen(Activity.Build, GuideUnlockFacts()))
            {
                _lockedMsg = $"{Unlocks.Headline(Activity.Build)} IS NOT OPEN YET — "
                             + $"{Unlocks.Requirement(Activity.Build).ToUpperInvariant()}.";
                _lockedTimer = 3.2f;
                _sound.Play("sfx_error", 0.55f);   // the same refusal as a locked tile — see OpenNav
            }
            else
            {
                var wasWeave = _showLoadout;
                OpenNav(0);              // clears all nine flags in one place
                _showLoadout = !wasWeave;
            }
        }




        // THE CHEST REVEAL RUNS EVERYWHERE TOO, and for the same reason as the champion below it: a
        // beat that only advances on one screen is a beat the player watches somewhere they did not
        // start it. Ticked before any overlay branch can return.
        _forge.TickReveal((float)gameTime.ElapsedGameTime.TotalSeconds, _hunter);

        // THE CHAMPION FIGHTS EVERYWHERE. Ticked here, before any overlay can early-return, so a run
        // keeps clearing waves and paying out while you're in the Forge, the tree, or another region's
        // menu — the whole point of an idle game. The fight screen's own clicks are handled in its Draw,
        // which only runs when that screen is the one on top, so a click in the Forge cannot fall
        // through into the fight without any flag being threaded down for it.
        _regionProgression = RegionProgressionOf(_region);

        // ── WHO HAS THE PLAYER'S EYES THIS FRAME. ───────────────────────────────────────────────
        //
        // Decided here, once, after every flag it reads has settled for the frame — the panels and the
        // welcome above, the log's L and medallion, the reveal's tick — and before the fight and the
        // coach run, so it stands on a frame the authored opening holds the fight. The death and the
        // coach's choice are read as the last frame left them: both tick inside UpdateExpedition below,
        // one frame behind is a frame nobody sees, and a second reading would be a second owner. The
        // death owns the frame only while it is ON the page: the fight ticks on every screen, and a fall
        // behind the Forge is not something the player is looking at — so the edge below is the on-page
        // death's too, and a fall nobody watched hushes nothing. Highest wins.
        var deathWasUp = _deathWasUp;
        _deathWasUp = !OverlayActive && _expedition.DeathTransitionUp;
        // THE FALL HAS FINISHED: a beat of quiet, so READ THE LOG lands once the new descent is legible
        // rather than on the first lit frame of the stage coming back. No such beat when the log closes —
        // the lesson it leads to is simply chosen fresh.
        if (deathWasUp && !_deathWasUp) _coach.Hush(OnboardingDirector.QuietAfterReward);
        _attention = _opening.Running ? AttentionOwner.Opening
            : ProductionModalUp || WelcomeUp ? AttentionOwner.Modal
            : _forge.RevealActive ? AttentionOwner.Reveal
            : !OverlayActive && _expedition.DeathTransitionUp ? AttentionOwner.Death
            : _expedition.LogOpen ? AttentionOwner.Report
            // THE COACH OWNS THE FRAME ONLY WHEN SOMETHING IS LIT. A raised beat about the fight (its
            // Target is a fight control) dwells while the player is on the Forge; it aims, but there is
            // no hole for it there, nothing is painted, and a light that paints nothing must not hold
            // the news back. The holes are the geometry DrawCoachSpotlight paints — the lane is already
            // reserved for this frame, so resolving them here is safe where CoachLightsIt could not.
            : _coach.Showing is { } aimed && CoachHoles(aimed).Length > 0 ? AttentionOwner.Coach
            : _noticeTimer > 0f || _lockedTimer > 0f || _feedbackToastTimer > 0f ? AttentionOwner.Feedback
            : AttentionOwner.None;

        // ── A LETTER ARRIVED. One pulse, one cue, and only when nobody else has the player's eyes.
        //
        // The mark on the envelope is STATE and needs no permission — it simply holds until the letters
        // are read. The flourish is the part that competes: a halo and a soft cue over a chest reveal,
        // a fall or an open log is the game asking for two things at once. So the edge is HELD (a pulse
        // cannot be paused — UiMotion decrements every one) and spent on the first frame the owner is
        // the coach or nothing. No toast, ever: this is background news.
        if (TakeDispatchArrival()) _dispatchArrivalHeld = true;
        if (_dispatchArrivalHeld && !AttentionOwnedAbove(AttentionOwner.Coach))
        {
            _dispatchArrivalHeld = false;
            UiMotion.Flash(DispatchPulseKey, UiMotion.Transition);
            // The background tier's cue, quieter than the notice's reward tier. PlayFirst so the game
            // plays before the wav lands, and so a build without it falls back to the rail's page tick.
            _sound.PlayFirst(0.5f, "sfx_dispatch", "sfx_nav");
        }

        // ── THE AUTHORED HOLD. ──────────────────────────────────────────────────────────────────
        //
        // The player must SEE a frozen game, not a still frame with a simulation running behind it.
        // This one call is the whole fight: HuntScreen begins the descent lazily inside it
        // (`if (_run is null) StartRun(hunter)`), so skipping it before the first frame means the run
        // has never begun — RunsStarted stays 0 and there is nothing to unwind when the hold lifts.
        // Mid-fight it means no wave starts, no reward is credited and no income accrues, so nothing
        // piles up to be applied in a burst afterwards.
        //
        // CORE IS UNTOUCHED. The wave was resolved deterministically before any of this; holding
        // changes what is on screen and never what happened. Headless and offline simulation cannot
        // tell the difference because they never call this method.
        // ...AND EXACTLY ONE FRAME OF IT GETS THROUGH BEFORE THE FIRST HOLD. The descent starts
        // lazily inside the fight screen, so a hold applied before it began would freeze an empty
        // stage; one frame puts the champion there in its idle stance with the wave's pack still off
        // to the right, which IS the arrival tableau. Every frame after it is held.
        if (!_opening.HoldsFight || !_expedition.RunStarted) UpdateExpedition(gameTime);

        // ── THE FIGHT SCREEN'S OWN UI INPUT: the EXPEDITION LOG medallion and the utility rail's
        //    door. Deliberately OUTSIDE the hold above — the log must stay
        //    openable on a frame the authored opening is holding the fight — and deliberately ABOVE the
        //    menu branches' `Latch(); return;`, so it runs on every frame whatever screen is up. A modal
        //    already makes MouseClicked false, so nothing fires under one.
        //
        //    `huntOnTop` is the screen gate, and it is load-bearing: _expedition.Draw and DrawLog are
        //    reached only from the draw chain's terminal `else`, so without it a click in the FORGE
        //    would fall through into the fight's HUD. !OverlayActive IS that terminal branch's
        //    condition. The LOG is deliberately not gated by it — DrawLog is drawn over every screen,
        //    and HuntScreen returns early while the log is open.
        _expedition.TakeInput(ChromeMouse, MouseClicked, MouseWheel, huntOnTop: !OverlayActive);

        // A modal eats the frame's INPUT, but not the frame, and not the fight. The autosave and the
        // farms tick above; the champion ticks on the line above this one. What must not happen is the
        // hotkeys and buttons underneath the panel continuing to respond.
        // THE TWO HOST MODALS DECIDE HERE, not in their Draw. The panel eats the frame's input and
        // not the frame (the farms and the champion tick above), so this is the last thing that runs.
        if (HostModalUp)
        {
            if (_showSettings) UpdateSettings();
            else if (_showDispatches) TakeDispatchesInput();
            else if (_showHelp)
            {
                // The help panel's two inputs: its close icon (F1 and Esc close it too, from the block
                // above) and its wheel, against the view the paint measured.
                if (_clicked && _helpClose.Contains(ChromeMouse)) _showHelp = false;
                if (_helpMaxScroll > 0 && MouseWheel != 0 && _helpView.Contains(ChromeMouse))
                    _helpScroll = Math.Clamp(_helpScroll - MouseWheel * UiMetrics.RowHeight, 0, _helpMaxScroll);
            }
            Latch(gameTime);
            return;   // the help (F1) is a modal too
        }

        if (_showWorld) { UpdateWorld(); Latch(gameTime); return; }

        if (_showMastery)
        {
            // One lock, read by both doors — the rail tile and the button on the page.
            _masteryScreen.Loadout = _loadout;
            _masteryScreen.Mastery = _mastery;
            _masteryScreen.Power = _hunter.PowerRating;   // the Build screen has no Hunter ref of its own
            _masteryScreen.Level = _hunter.HunterLevel;
            _masteryScreen.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked,
                                _mouse.LeftButton == ButtonState.Pressed, MouseWheel, MouseRightClicked);
            if (_masteryScreen.Dirty) { _masteryScreen.ClearDirty(); Save(); }
            PlayCue(_masteryScreen.ConsumeCue());   // a node taken, a style sealed, a point handed back, a refusal

            Latch(gameTime);
            return;
        }

        if (_showGear)
        {
            _gear.Loadout = _loadout;
            _gear.Mastery = _mastery;
            _gear.DiscoveredKeystones = _keystoneMenu;
            _gear.KnownVows = _vowMenu;
            _gear.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked || ForcedScreenClick(), MouseRightClicked, MouseWheel, _hunter);
            PlayCue(_gear.ConsumeCue());

            // A SET FINISHED IS NEWS, ONCE. The screen decides when a five-piece set is first complete
            // and hands back two lines; the host toasts them, sounds the reward, and writes the set's
            // name to the save so the next session does not announce it again (SaveGame.CompletedSets).
            if (_gear.ConsumeNotice() is { } setNews)
            {
                var lines = setNews.Split('\n');
                PostNotice(lines[0], lines.Length > 1 ? lines[1] : "");
                _sound.Play("sfx_levelup", 0.8f);
                Save();
            }

            // ── THE ITEM MENU'S VERBS. Three of the four live in the Forge, so the gear screen names
            //    what it wants and the host carries the player there, already pointed at the item.
            //    EQUIP is the exception: it is the gear screen's own verb and never leaves. ────────
            if (_gear.ConsumeItemAction() is { } request)
            {
                if (request.Action == ItemAction.Equip)
                {
                    // WORN FIRST. Equipping does NOT remove the item from the bag — worn pieces live in
                    // both — so `Inventory.FirstOrDefault` matches a worn item too, and TAKE OFF re-equipped
                    // the thing it was asked to remove while the unequip branch below could never run.
                    if (Enum.GetValues<GearSlot>().Select(_hunter.Worn).OfType<ItemInstance>()
                            .FirstOrDefault(i => i.InstanceId == request.InstanceId) is { } toDoff
                        && Gear.SlotFor(toDoff.BaseType) is { } sl)
                    {
                        _hunter.Unequip(sl);
                        // The same cue as equipping, pitched down — the taking-not-giving convention.
                        _sound.Play("sfx_equip", 0.7f, pitch: -0.25f);
                    }
                    else if (_forge.Inventory.FirstOrDefault(i => i.InstanceId == request.InstanceId) is { } toWear)
                    {
                        // THE CLASS RULE HOLDS HERE TOO. The menu greys its EQUIP row for another
                        // class's piece, but this is the one route that actually puts gear on, so it
                        // asks for itself and answers with the reason rather than a silent no.
                        if (ItemClasses.WhyNot(_characters.Active, toWear) is { } whyNot)
                        {
                            _lockedMsg = whyNot;
                            _lockedTimer = 3.2f;
                        }
                        else
                        {
                            _hunter.Equip(toWear);
                            _sound.Play("sfx_equip", 0.7f);
                        }
                    }
                    Save();
                }
                else
                {
                    // The VERB picks the tab (review 2026-08-23: the forge opened on whatever tab was last
                    // used, so GEAR's REFORGE could land on SALVAGE with the RE-ROLL button off screen).
                    // GEAR's SALVAGE arrives with the question already asked — never a silent scrap
                    // ordered from another screen.
                    switch (request.Action)
                    {
                        case ItemAction.Reforge: _forge.RequestReroll(request.InstanceId); break;
                        case ItemAction.Salvage: _forge.FocusFor(request.InstanceId); _forge.RequestSalvage(request.InstanceId); break;
                        default: _forge.RequestUpgrade(request.InstanceId); break;
                    }
                    _showGear = false;
                    _showForge = true;
                    // The click that picked the menu row is spent HERE. Without this the forge's first Draw
                    // ran with MouseClicked still true and pressed whatever button sat under the cursor —
                    // UPGRADE / GREATER UPGRADE / RE-ROLL / SELL — spending materials or a chart with no
                    // forge press (review 2026-08-23, high). Same one-frame latch the unlock panel uses.
                    _swallowInput = true;
                    // A NAVIGATION, not an operation: this only carries the player to the Forge with the
                    // item already pointed at. The hammer belongs to the press that follows.
                    _sound.Play("sfx_nav", 0.6f);
                }
            }
            if (_gear.Dirty) { _gear.ClearDirty(); Save(); }
            Latch(gameTime);
            return;
        }

        // THE VAULT. Placed here, in the SAME relative position as its Draw branch, because Update's and
        // Draw's screen chains are two independent statements of the same priority and nothing enforces
        // that they agree — a screen inserted at a different point in each is a screen the player sees
        // while an invisible one eats their clicks.
        if (_showVault)
        {
            // NO per-frame push. The filter is edited during VaultScreen.DRAW; pushing the stored
            // value here every Update clobbered the edit one frame later and then copied the clobber
            // back — the whole TAKE ONLY row was inoperative while its Core tests stayed green.
            // (Adversarial review, pass five, HIGH.) The screen is authoritative; LoadContent seeds it
            // once from the save, and this branch only reads the edits back.
            // ── THE WANDERING TRADER. Rollover first: a new ISO week clears the purchases and the
            //    stall re-mints. Zero on old saves lands here too — the migration IS the rollover. ──
            var week = WanderingTrader.WeekStamp(DateTime.UtcNow);
            // FORWARD-ONLY: a clock set backwards must not re-arm the weekly purchases (an offline
            // game cannot stop a determined self-cheater, but it must not hand out a re-arm loop by
            // accident). The second clause self-heals a save stamped in the absurd future — a
            // wrong-then-corrected clock would otherwise freeze the stall until reality caught up.
            if (week > _traderWeek || _traderWeek > week + 100)
            {
                _traderWeek = week;
                _traderBought.Clear();
                _traderStock = null;
                // A BUY clicked in the old week's dying frame must not buy the NEW week's slot.
                _vault.ConsumeTraderBuy();
                Save();
            }
            var traderLevel = Math.Max(1, _deepestEver);
            // Restocked for the CLASS as well as the level: the stall leans four in five toward the
            // champion you are playing, so a switch on the roster re-leans it.
            var traderClass = _characters.Active.Class;
            if (_traderStock is null || _traderStockLevel != traderLevel || _traderStockClass != traderClass)
            {
                _traderStock = WanderingTrader.Stock(week, traderLevel, new LootTuning(), traderClass);
                _traderStockLevel = traderLevel;
                _traderStockClass = traderClass;
            }
            _vault.Hunter = _hunter;
            // What OPEN ALL will do to the drops before the player sees them. Both are Memory Dust
            // traits the FORGE owns and UpdateExpedition (called above, this same frame) refreshes,
            // so the vault's consequence row is never a frame stale.
            _vault.AutoSellFloor = _forge.AutoSellFloor;
            _vault.AutoMergeOnOpen = _forge.AutoMergeOnOpen;
            _vault.TraderStock = _traderStock;
            _vault.TraderBought = _traderBought;

            _vault.Update(dt, _forge.UnopenedChests, PageCursor, MouseClicked || ForcedScreenClick(), MouseWheel);

            // A stall purchase: pay in materials, and the good goes to the FORGE bench like any
            // other loot — the vault shows chests, not items.
            if (_vault.ConsumeTraderBuy() is { } stallSlot
                && _traderStock is not null && stallSlot >= 0 && stallSlot < _traderStock.Count
                && !_traderBought.Contains(stallSlot)
                && WanderingTrader.TryBuy(_hunter, _traderStock[stallSlot], TraderTuning.Default))
            {
                _forge.AddLoot(new List<ItemInstance> { _traderStock[stallSlot] });
                _traderBought.Add(stallSlot);
                _sound.Play("sfx_forge", 0.9f);
                Save();
            }

            switch (_vault.ConsumeOpen())
            {
                case VaultScreen.OpenRequest.Selected:
                {
                    // The vault shows STACKS of identical chests; the click names a real member of the
                    // stack (SelectedChest) and record equality finds it in storage — any member of the
                    // stack is the right one, contents are rolled at open. The sorted index is the
                    // fallback for a stale click.
                    var sorted = ChestDossiers.BestFirst(_forge.UnopenedChests);
                    if (sorted.Count > 0)
                    {
                        var pick = _vault.SelectedChest is { } sample && _forge.UnopenedChests.Contains(sample)
                            ? sample
                            : sorted[Math.Clamp(_vault.SelectedIndex, 0, sorted.Count - 1)];
                        _forge.OpenOneChest(pick, _hunter);
                        // The chest has its own voice now (sfx_chest_open, plus sfx_chest_rare for Epic
                        // and better) — it used to borrow the FORGE's hammer, which is a different act.
                        PlayCue(_vault.ConsumeCue(), 0.9f);
                        Save();
                    }
                    break;
                }
                case VaultScreen.OpenRequest.All:
                    _forge.OpenEveryChest(_hunter);
                    PlayCue(_vault.ConsumeCue(), 0.9f);   // one pair for the whole pile, graded by its best chest
                    Save();
                    break;
            }

            Latch(gameTime);
            return;
        }

        if (_showTraining)
        {
            // (HighestWave / ChestsOpened / MasteryPoints are gone with the PROGRESS panel — they
            //  are the Map's, the Vault's and the Mastery tree's numbers, and none of them moves
            //  when you train.)
            PushTrainingState();
            _training.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked, MouseWheel, _hunter);
            if (_training.Dirty) { _training.ClearDirty(); Save(); }

            // Gleam is one of the three payouts a descent makes, and this is the layer it buys. The
            // model (geometric cost, rank cap) has always been here; until now nothing in the game
            // called it. TRAIN has its own cue (two notes up) instead of the ordinary click, and the
            // screen's own cue carries the refusal when the gleam is not there.
            //
            // DRAINED HERE, not in Draw. It spent Gleam and wrote the save from the paint pass, which
            // is the mutation half of ADR-006: a Draw must be safe to run with no Update before it.
            if (_training.ConsumeTrain() is { } stat && _hunter.Train(stat)) { _sound.Play("sfx_train", 0.8f); Save(); }
            PlayCue(_training.ConsumeCue());
            if (_training.ConsumeReset() && _hunter.ResetTraining()) { _sound.Play("sfx_forge", 0.8f); Save(); }
            Latch(gameTime);
            return;
        }

        // Award Memory Dust for mastery levels earned across ALL regions, not just the active one. NOTHING
        // resets — the high-water mark only ratchets up, so a milestone is never paid twice.
        //
        // It used to read only the ACTIVE region's level against a single global mark, so once one region
        // was mastered, every LATER region's levels below that peak paid no Dust — six regions farming
        // independently made that undervaluing real. Summing every region's levels into the same int keeps
        // the save format unchanged (one older save's peak simply pays its one-time catch-up on first load).
        var totalMasteryLevels = Regions.All.Sum(r => (int)_world.RegionFarm(r.Id).MasteryLevel);
        if (totalMasteryLevels > _highestMasteryAwarded)
        {
            _dust.AddDust(CorruptionScaling.MasteryLevelDust(
                totalMasteryLevels - _highestMasteryAwarded, _world.CorruptionTier));
            _highestMasteryAwarded = totalMasteryLevels;
        }

        // THE MASTERY TREE'S PLAIN STATS, pushed into the champion every frame. Unconditional on
        // purpose: the tree is mutated from the build screen, from the dev fixtures and by respec, and
        // refreshing at any one of those sites is how a stat node ends up live on the screen that
        // changed it and dead everywhere else. Respec is free, so this REPLACES rather than adds.
        _hunter.SetMasteryStats(_mastery.Stats());

        if (_showLoadout)
        {
            _loadoutScreen.Loadout = _loadout;
            _loadoutScreen.Mastery = _mastery;
            // WHAT THE WORLD HAS TAUGHT AND WHAT THE HUNTER HAS PROVED. The workbench used to read the
            // trait tree for both; both are facts about the account now, and the host owns them.
            _loadoutScreen.DiscoveredKeystones = _keystoneMenu;
            _loadoutScreen.DiscoveredSkills = DiscoveredSkillsNow();
            _loadoutScreen.KnownVows = _vowMenu;
            _loadoutScreen.NextSocketNote = Unlocks.NextSocketNote(GuideUnlockFacts());
            _loadoutScreen.NextSkillSlotNote = Unlocks.NextSkillSlotNote(GuideUnlockFacts());
            // ...AND WHETHER THE SLOT REVEAL IS UP, so the screen can mark the row the banner is about.
            // One question, asked once: the same ScreenBannerShowing the slot itself draws from.
            _loadoutScreen.RevealingNewSlot = ScreenBannerShowing() is not null;
            _loadoutScreen.NextVowNote = Unlocks.NextVowNote(GuideUnlockFacts());
            _loadoutScreen.SkillLevels = _skillProgress;
            _loadoutScreen.Hunter = _hunter;
            _loadoutScreen.Character = _characters.Active;
            _loadoutScreen.RegionId = _activeRegion;
            _loadoutScreen.RegionName = Regions.Get(_activeRegion).Name;
            _loadoutScreen.ChosenStyle = _mastery.Affinity();
            _loadoutScreen.MasteryTaken = _mastery.Taken;
            _loadoutScreen.Update(PageCursor, MouseClicked, _mouse.LeftButton == ButtonState.Pressed, MouseWheel);
            if (_loadoutScreen.Dirty) { _loadoutScreen.ClearDirty(); Save(); }
            PlayCue(_loadoutScreen.ConsumeCue());   // the refusal LAW 13 already says in words
            // CONSUMED HERE, where the Weave actually runs. It was read inside `if (_showMastery)`, and
            // _showMastery and _showLoadout are mutually exclusive on every path that opens this screen —
            // so the flag was set and never read, the BACK button did nothing, and the stale flag then
            // fired on the next visit to BUILD and switched OFF the tree the MASTERY tile had just
            // switched on. A request consumed in a branch its producer cannot reach is not wiring.
            Latch(gameTime);
            return;
        }

        if (_showRoster)
        {
            _roster.Progress = QuestSnapshot();
            _roster.Mastery = _mastery;   // so the inspector can say a starting skill is ALREADY KNOWN
            _roster.Hunter = _hunter;     // so it can count the worn pieces a switch would shed
            _roster.Update(PageCursor, MouseClicked, _characters);
            PlayCue(_roster.ConsumeCue(), 0.6f);   // SET ACTIVE is a navigation: the rail's own page tick
            // THE SWITCH ANNOUNCES ITSELF on the channel every other arrival uses, instead of a stray
            // line printed inside the panel. Polled in Update, not Draw, so nothing fires from a draw
            // pass; the one-frame delay is invisible.
            if (_roster.TakeNotice() is { } who)
                PostNotice($"YOU ARE {who.ToUpperInvariant()}",
                           "SWITCHING IS FREE — YOUR SKILLS, TRAITS, GEAR AND THE WARREN STAY");
            Latch(gameTime);
            return;
        }

        if (_showTraits)
        {
            // THE TRAITS SCREEN ANSWERS ITS CLICKS HERE, not in its Draw. A catch-up tick runs Update
            // twice and Draw once, so an edge hit-tested from a draw pass is silently dropped — and on
            // this screen the DRAG was the worse half, because a release resolved from a paint pass
            // EQUIPPED a trait. The block still swallows the frame's hotkeys, as it always did.
            _traitScreen.Update(_traitLedger, _characters.ActiveId, PageCursor, MouseClicked, MouseWheel);
            PlayCue(_traitScreen.ConsumeCue(), 0.9f);
            // WEARING A TRAIT IS A SAVE. It is per-character state and the only place it lives is
            // the file; the ten-second autosave would get there eventually, and "eventually" is
            // how a crash costs someone their build.
            //
            // READ HERE AND NOT AT THE FOOT OF Draw: the screen clears Dirty at the TOP of its Update,
            // so a catch-up tick's second Update would wipe the flag before a draw-side read saw it —
            // and on a tick where this block does not run, a stale true would re-fire Save() from
            // every Draw. Read straight after the Update that set it, this is exactly-once, which is
            // the pattern MASTERY and GEAR already use.
            if (_traitScreen.Dirty) Save();
            Latch(gameTime);
            return;
        }

        // F opens the Forge — but NOT while the automation screen is up, where F feeds materials to the
        // selected creature.
        //
        // This used to also require `_encounter.Phase == EncounterPhase.Complete`, i.e. "you just
        // finished a fight, so open the results-screen Forge". The pivot stopped calling UpdateCombat,
        // so Phase never left Engaging and that condition became PERMANENTLY FALSE: the Forge — every
        // item, merge, and equip in the game — was simply unreachable, while the help screen went right
        // on telling players to press F for it. The Forge is a place now, not a post-fight reward.
        // THE SECOND F HANDLER IS GONE, AND WITH IT THE REASON F DID NOTHING.
        //
        // The nav-rail hotkey loop above already owns every rail key including F: it calls OpenNav, which
        // sets _showForge. This handler then ran in the same frame and toggled it straight back off, so
        // the two correct handlers cancelled and the Forge could not be opened by its own key at all —
        // while the help screen went on teaching it. Neither piece of code was wrong alone; the bug was
        // owning one key in two places.


        if (_showForge)
        {
            // Keep the Forge told which Forms the build runs, so it can flag live combos here too (not
            // only when arrived at from the fight).
            TellForgeTheBuild(ComposeBuild());
            // The keyboard is LOCKED while any host modal owns the frame — without this, the S that
            // dismissed an unlock panel also SOLD the focused (rarest-first!) bag item behind it, and
            // S/D/J kept working under the settings panel and the reveal. MouseClicked already carries
            // these gates; the keys did not. (Adversarial review, pass four.)
            _forge.Update(gameTime, ScreenKeys, PageCursor, MouseClicked, MouseWheel, _hunter,
                          inputLocked: _swallowInput || _showSettings || WelcomeUp || _forge.RevealActive,
                          rightClicked: MouseRightClicked);
            // Each operation names itself — upgrade, re-roll, socket, salvage — instead of every one of
            // them borrowing the same hammer.
            PlayCue(_forge.ConsumeCue(), 0.9f);
            // The dialog's "don't ask me again" writes through to the prefs file the moment it is used.
            if (_forge.PrefsDirty) { _forge.PrefsDirty = false; _askBeforeScrap = _forge.AskBeforeScrap; SaveDisplay(); }
            Latch(gameTime);
            return;
        }

        if (_showWarren)
        {
            // WARREN is the facility-production dashboard. Production runs every frame in TickWarren; its
            // own input — a card picked, a refusal, the UPGRADE that spends — is resolved HERE, because
            // draw must not consume an input edge: a catch-up tick runs Update twice and Draw once, and
            // the second Update erases the edge the single Draw would have hit-tested (ADR-006).
            FeedWarren();
            _warrenScreen.Update(PageCursor, MouseClicked, MouseWheel);
            PlayCue(_warrenScreen.ConsumeCue());   // an upgrade that landed, or a refusal

            // The spend happens in the same half of the frame as the ask, so the next Update's Settle()
            // sees the level it asked for actually land and lights what changed.
            if (_warrenScreen.ConsumeUpgrade() is { } kind
                && _warren.CanUpgrade(kind, _hunter.Gleam, _dust.MemoryDust))
            {
                var c = _warren.UpgradeCost(kind);
                _hunter.SpendGleam(c.Gleam);
                _dust.Spend(c.Dust);
                _warren.Upgrade(kind);
                Save();
                // ARM THE FEEDBACK IN THE SAME HALF-FRAME AS THE SPEND. Settle() already ran at the top
                // of the Update that asked, so without this the frame the upgrade lands paints the new
                // output, cost and wall with nothing animating, and the rise then plays from the new
                // figures a frame later — a visible step backwards on exactly the numbers the feedback
                // exists to show moving.
                _warrenScreen.SettleNow();
            }
            Latch(gameTime);
            return;
        }

        Latch(gameTime);
    }

    private void Latch(GameTime gameTime)
    {
        // The `vfx` / `telegraph` shot modes posed manual combat for screenshots. That screen is gone,
        // and the effects they fired were never Update()d or Draw()n anyway — VfxPlayer's only caller
        // was this. VfxPlayer.cs is KEPT and left unwired on purpose: the auto-battle has no impact
        // effects at all (a hit on REND, a sparkle on MEND), which is a feature gap worth filling, not
        // rot worth deleting.
        UpdateMusic();

        _prevKeys = _keys;
        _prevMouse = _mouse;
        base.Update(gameTime);
    }

    /// <summary>
    /// Tell the Forge what the worn build actually is, so it can mark a combo live or dead.
    /// </summary>
    /// <remarks>
    /// All three axes travel together, in one call, on purpose. They used to be one line setting
    /// <c>ActiveForms</c>, repeated at three call sites — and the moment a combo could ask about a
    /// keystone or a Vow instead, three separate places each had to remember to set three things. Two
    /// of them would have kept answering for Forms alone, and a keystone combo would have read as dead
    /// on a build that ran it. One method means a fourth axis is one edit, not four.
    /// </remarks>
    private void TellForgeTheBuild(Build build)
    {
        _forge.ActiveDefs = build.Skills.Select(s => s.Def).ToList();
        _forge.ActiveTriggers = build.Triggers(_hunter).ToList();
        _forge.SwornVows = _loadout.Skills
                                   .Select(s => s.VowId)
                                   .Where(id => !string.IsNullOrEmpty(id))
                                   .Distinct()
                                   .Count();
    }


    /// <summary>Has the player actually changed their weave, rather than keeping what they were given?</summary>
    /// <remarks>
    /// Compared against <see cref="PlayerLoadout.Starter"/>'s single slot rather than a stored flag,
    /// so it needs no save migration and is honest on every existing save.
    /// </remarks>
    private bool BuildDiffersFromStarter()
    {
        var skills = _loadout.Skills;
        if (skills.Count != 1) return true;
        // The starter is the champion's OWN signature now, not a fixed shared skill, so the comparison
        // has to ask the active champion what it was given rather than name one skill.
        return skills[0].SkillId != _characters.Active.SignatureSkillId
               || skills[0].Source != Source.Body || skills[0].VowId is not null;
    }

    /// <summary>Pick the looping music bed for the current screen. No-op until the music_* WAVs exist.</summary>
    private void UpdateMusic()
    {
        string track;
        if (_showTitle) track = "music_title";
        else if (_showForge) track = "music_forge";
        else if (_showWarren) track = "music_warren";
        else if (_showTraits) track = "music_constellation";
        else if (_showWorld) track = "music_map";
        else
        {
            // Combat/results: a region-themed bed if one exists, else the generic combat track.
            var theme = Regions.Get(_activeRegion).Theme.ToString().ToLowerInvariant();
            track = _sound.Has($"music_arena_{theme}") ? $"music_arena_{theme}" : "music_combat";
        }

        _sound.PlayMusic(track);
    }



    /// <summary>The creature a region throws at your squad, so the enemy is never a coloured box.</summary>
    private static string EnemyArtFor(string regionId) => regionId switch
    {
        "cinderworks" => "crea_machine_atk_warden",
        "umbral_reach" => "crea_shadow_atk_stalker",
        "marrow_wastes" => "crea_body_atk_sinew",
        "still_archive" => "crea_mind_atk_lance",
        "pale_choir" => "crea_spirit_atk_echofang",
        _ => "crea_nature_atk_whelp",
    };

    /// <summary>A region's rung on the world ladder (0 = home). Deeper regions are innately tougher.</summary>

    /// <summary>
    /// How many skills the player may weave right now: the onboarding gate first, the trait tree after.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two sources, and they hand over cleanly rather than competing. While the onboarding gate is still
    /// below the built-in four slots it RULES, because that is the whole point of it — slots arriving
    /// one at a time with something said about each. Once it reaches four it steps aside entirely and
    /// the trait tree decides, which is the pre-existing rule and the only way the fifth-slot node stays
    /// a real reward.
    /// </para>
    /// <para>
    /// Never reduces below what is already woven. A capacity that shrinks silently deletes a skill the
    /// player chose, and the only way to find out would be to notice it missing.
    /// </para>
    /// </remarks>
    private void ApplySkillCapacity()
    {
        // PROGRESSION ALONE, capped at four. This used to hand over to the trait tree at the top of the
        // ladder, which is where the fifth slot came from; the fifth slot is removed (it bought a THIRD
        // action-taking skill, the number the slot rework existed to reduce) and nothing sells slots but
        // depth and conquest now.
        _loadout.SkillCapacity = Math.Max(Unlocks.SkillSlots(GuideUnlockFacts()), _loadout.Skills.Count);
    }

    /// <summary>
    /// Everything the WORLD has taught the account: keystones, keystone sockets, Vow capacity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every fact behind these only ever grows — a region conquered is never un-conquered, region
    /// mastery points are only added to, the corruption's PEAK never falls, and deepest-ever wave is a
    /// running maximum. That is what stops a reveal from firing twice, which is the invariant the
    /// unlock layer carries a scar from. The sets here are unioned and the capacity is maxed, so this
    /// method cannot take anything away even if a fact somehow moved backwards.
    /// </para>
    /// <para>
    /// It runs every frame rather than on the conquest event alone, because there are four producers
    /// (conquest, two mastery rungs and the corruption) and three of them happen quietly mid-farm.
    /// </para>
    /// </remarks>
    private void ApplyWorldGrants()
    {
        var facts = GuideUnlockFacts();

        // THE ONE VOW THAT IS GIVEN. Without it a player could reach the end of the game without ever
        // learning that Vows exist, since every other one has to be proved by keeping its rule first.
        // It arrives with the BUILD screen, on the gate that already opens the workbench it is sworn on.
        if (Unlocks.IsOpen(Activity.Build, facts))
            foreach (var granted in Vows.Granted)
                if (_discoveredVows.Add(granted.Id) && _grantsBaselined)
                    // SILENT UNDER THE RIG unless a capture asked for this one, exactly as a rail
                    // reveal is (RH_SHOT_REVEAL below). A fixture that opens the BUILD screen offers
                    // this Vow on its first frame, and the toast stands in the notice lane, over
                    // whatever a fight fixture is posing there. RH_SHOT_REVEAL=Vow announces it deliberately.
                    if (!CaptureRig || string.Equals(Environment.GetEnvironmentVariable("RH_SHOT_REVEAL"), "Vow", StringComparison.OrdinalIgnoreCase))
                        PostVowReveal(granted, offered: true);

        // KEYSTONES. TRANSITIONAL: the trait tree still stands this phase and its keystone nodes are
        // still buyable, so what it has taught is unioned in as well. That term goes when the tree does.
        // THE WORLD IS THE ONLY TEACHER NOW. This used to union in what the Memory tree had taught;
        // an old save's keystone nodes are converted to discoveries once, at load, by LegacyTraitTree.
        var found = Keystones.DiscoveredBy(_world, Array.Empty<string>());
        var fresh = new List<Keystone>();
        foreach (var k in found)
            if (_discoveredKeystones.Add(k.Id)) fresh.Add(k);

        // SOCKETS. Derived from the world, floored by what was already earned or already worn, so a
        // returning player can never be handed fewer sockets than the keystones they are wearing.
        _keystoneSocketsEarned = Math.Max(
            Math.Max(_keystoneSocketsEarned, Unlocks.KeystoneSockets(facts)),
            _loadout.KeystoneIds.Count);
        _loadout.KeystoneCapacity = _keystoneSocketsEarned;
        // ASSIGNED, NOT RAISED. The facts behind it only ever grow, so this only ever grows in play —
        // and a Math.Max here would let the type's own default stand in for the world's answer, which
        // is exactly how this milestone came to grant nothing at all.
        _loadout.VowCapacity = Unlocks.VowCapacity(facts);
        // WHOSE SKILL IS UNTOUCHABLE. Published from the active champion the same way, so the loadout's
        // own verbs can refuse to clear, overwrite or reorder it (playtest 2026-09-09).
        _loadout.SignatureSkillId = _characters.Active?.SignatureSkillId;

        if (fresh.Count > 0) RebuildBuildMenus();

        // THE FIRST PASS ON A DEEP SAVE derives eleven keystones at once, and eleven ceremonies in one
        // second is the failure mode of a ceremony. One line instead, and the ordinary one-at-a-time
        // reveal from there.
        if (!_grantsBaselined)
        {
            _grantsBaselined = true;
            // A RETURNING PLAYER IS TOLD, ONCE. Their keystones moved house — off the trait tree and
            // onto the world — and arriving in silence would give them no reason to go and look. A
            // new game finds nothing on this frame, so the line is only ever seen by a save that
            // already had some.
            if (fresh.Count > 0 && (!CaptureRig || ShotMode == "conquered"))
                PostNotice(
                    fresh.Count == 1
                        ? "THE WORLD HAS TAUGHT YOU A KEYSTONE"
                        : $"THE WORLD HAS TAUGHT YOU {fresh.Count} KEYSTONES",
                    (fresh.Count == 1 ? "IT IS " : "THEY ARE ")
                    + "ON THE BUILD SCREEN, READY TO WEAR. YOU FIND MORE BY CONQUERING "
                    + "AND MASTERING REGIONS.");
            if (_fifthSkillDropped is { } lost)
            {
                _fifthSkillDropped = null;
                PostNotice("THE FIFTH SKILL SLOT IS GONE",
                    $"{lost.ToUpperInvariant()} WAS TAKEN OUT OF YOUR BUILD. IT KEEPS ITS LEVEL — "
                    + "PUT IT BACK ANY TIME IN PLACE OF ANOTHER SKILL.");
            }
            return;
        }
        if (fresh.Count == 0) return;

        // A MASTERY RUNG HAPPENS MID-FARM, with the player very possibly not watching, so it is never
        // modal: the same wrapping notice every keystone reveal uses. A CONQUEST posts its own, at the
        // conquest site, so the socket line can follow it in the right order — so it is skipped here.
        foreach (var k in fresh)
        {
            if (Keystones.SourceOf(k.Id) is { Rung: WorldRung.Conquest }) continue;
            PostKeystoneReveal(k);
        }
    }

    /// <summary>The reveal a found keystone gets: its name, then the rung that taught it and what it does.</summary>
    /// <remarks>
    /// ONE HELPER FOR ALL FOUR PRODUCERS — conquest, the two mastery rungs and the corruption — so the
    /// sentence a keystone arrives with is written once and every one of them is the same shape.
    /// <para>
    /// It is a NOTICE and not the map's strip, and that is the whole point. A keystone has to say what it
    /// DOES — the word CAPACITOR alone teaches nobody anything — and the longest of those sentences is 164
    /// characters. The notice is the one presentation in the game that can hold it: its body WRAPS and its
    /// plate grows to the rungs it draws. The map strip is a fixed-height single line, and after the sixth
    /// conquest it is not drawn at all (the corruption ladder takes it), so a reveal written into it was
    /// four lines across the region cards for five conquests and invisible on the sixth.
    /// </para>
    /// </remarks>
    private void PostKeystoneReveal(Keystone k) => PostNotice($"NEW KEYSTONE — {k.Name}", KeystoneRevealDetail(k));

    /// <summary>
    /// The reveal's body: where it came from, then the FIRST sentence of what it does, and a pointer to
    /// the BUILD screen when there is more. Four lines of uppercase prose was not a toast (chrome-08);
    /// the BUILD inspector carries the whole blurb. Static and pure so the notice test reads the same
    /// sentence the host posts.
    /// </summary>
    internal static string KeystoneRevealDetail(Keystone k)
    {
        var where = Keystones.SourceOf(k.Id) is { } src ? RungReached(src) : "";
        var blurb = k.Blurb.Trim();
        var cut = blurb.IndexOf(". ", StringComparison.Ordinal);
        var first = cut > 0 ? blurb[..(cut + 1)] : blurb;
        var detail = where.Length > 0 ? $"{where} {first}" : first;
        return first.Length < blurb.Length ? $"{detail} THE BUILD SCREEN SAYS THE REST." : detail;
    }

    /// <summary>Whichever keystone's reveal is the longest sentence in the catalogue. Capture rig only.</summary>
    /// <remarks>
    /// The toast's wrap is a ceiling, and a ceiling is only ever proved by the worst case. Asked of the
    /// catalogue rather than named in the fixture, so it follows the copy: today it is CAPACITOR at 164
    /// characters, and it stays the right answer when a keystone is added or a blurb is rewritten.
    /// </remarks>
    private static Keystone? LongestKeystoneReveal()
        => Keystones.Catalog
            .OrderByDescending(k => (Keystones.SourceOf(k.Id) is { } s ? RungReached(s).Length + 1 : 0) + k.Blurb.Length)
            .ThenBy(k => k.Id, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>"VERDANT HOLLOW IS PARTLY MASTERED." — the sentence a mastery-rung reveal opens with.</summary>
    private static string RungReached(KeystoneSource src)
    {
        if (src.Rung == WorldRung.Corruption) return "THE CORRUPTION HAS DEEPENED.";
        if (src.RegionId is not { } id || Regions.Find(id) is not { } def) return "";
        return $"{def.Name} IS {Keystones.RungName(src.Rung)}.";
    }

    /// <summary>The reveal a found Vow gets: what you did, then one line about it.</summary>
    private void PostVowReveal(Vow vow, bool offered = false)
        => PostNotice(offered ? $"A VOW IS OFFERED TO YOU — {vow.Name}"
                              : $"A VOW HAS REVEALED ITSELF — {vow.Name}",
                      vow.ProofLine);

    /// <summary>
    /// Rebuild the keystone and Vow lists every consumer reads — the composer, the workbench, the sim.
    /// </summary>
    /// <remarks>
    /// Catalogue order, not discovery order, so neither list ever reshuffles itself under the player.
    /// </remarks>
    private void RebuildBuildMenus()
    {
        _keystoneMenu = Keystones.Catalog.Where(k => _discoveredKeystones.Contains(k.Id)).ToList();
        _vowMenu = Vows.Catalog.Where(v => _discoveredVows.Contains(v.Id)).ToList();
    }

    /// <summary>The build the fight runs, composed against what the world has actually taught.</summary>
    private Build ComposeBuild()
        => _loadout.ToBuild(_mastery, _characters.Active, _skillProgress, _keystoneMenu, _vowMenu);

    /// <summary>Whichever auto-sell floor keeps the least. Null means "keep everything".</summary>
    private static Rarity? MorePermissive(Rarity? a, Rarity? b)
        => a is null ? b : b is null ? a : (Rarity)Math.Max((int)a.Value, (int)b.Value);

    /// <summary>
    /// What the account OWNED on the descent just finished — the temptation half of a Vow's proof.
    /// </summary>
    /// <remarks>
    /// Everything an item could tempt you with is counted in the BAG and on the BODY together. A player
    /// told they own nothing while a critical-chance ring sits in their inventory would rightly read
    /// that as a bug, and "you had it in your hand" is the sentence the proof is making.
    /// </remarks>
    private VowTemptationFacts VowTemptations()
    {
        var owned = _forge.Inventory
            .Concat(Enum.GetValues<GearSlot>().Select(_hunter.Worn).OfType<ItemInstance>())
            .ToList();

        bool Carries(AffixStat stat) => owned.Any(i => ItemAffixes.Of(i).Any(a => a.Stat == stat));
        bool Has(GearSlot slot) => owned.Any(i => Gear.SlotFor(i.BaseType) == slot);

        // EVERY WOVEN SOURCE CHOSEN — proof that the single Source was a decision, not a default. A
        // skill with a chosen variation is a skill the player opened and picked something on.
        var woven = _loadout.Skills.Where(s => s.SkillId is not null).ToList();
        var chosen = woven.Count > 0 && woven.All(s =>
            SkillCatalogue.Find(s.SkillId!) is { } def && _skillProgress.VariationOf(def) is not null);

        // STYLES IN REACH — how many styles this champion could weave RIGHT NOW. Carrying one style
        // when only one is reachable is not a restriction, it is the catalogue.
        var reach = _mastery.AvailableSkills().ToHashSet(StringComparer.Ordinal);
        if (_characters.Active.SignatureSkillId is { } own) reach.Add(own);
        var styles = reach.Select(SkillCatalogue.Find).OfType<SkillDef>()
                          .Select(d => d.Style).Distinct().Count();

        return new VowTemptationFacts(
            OwnsCritItem: Carries(AffixStat.Crit),
            OwnsSkillRateItem: Carries(AffixStat.SkillRate),
            OwnsDefenceItem: Carries(AffixStat.Defense),
            OwnsDamageItem: Carries(AffixStat.Damage),
            OwnsHealthItem: Carries(AffixStat.Health),
            OwnsHaulItem: Carries(AffixStat.Haul),
            OwnsBoots: Has(GearSlot.Boots),
            OwnsGloves: Has(GearSlot.Gloves),
            OwnsHelm: Has(GearSlot.Helm),
            OwnsRing: Has(GearSlot.Ring),
            OwnsCharm: Has(GearSlot.Charm),
            EveryWovenSourceChosen: chosen,
            StylesInReach: styles,
            KeystonesKnown: _discoveredKeystones.Count);
    }

    /// <summary>
    /// Reveal every Vow this descent proved: its rule held for enough cleared waves, and it was unsworn.
    /// </summary>
    /// <remarks>
    /// Nothing here is a roll, a drop or a missable event, and there is no one-time window: a Vow that
    /// was not proved this run can be proved on any later one, in any region, for as long as the game
    /// runs. That is the whole difference between a discovery and a lottery.
    /// </remarks>
    private void RevealProvedVows()
    {
        var found = Vows.Revealed(
            _expedition.LastRunVowProof, _loadout.SwornVows, _discoveredVows, VowTemptations());
        if (found.Count == 0) return;

        foreach (var vow in found)
        {
            _discoveredVows.Add(vow.Id);
            PostVowReveal(vow);
        }
        RebuildBuildMenus();
    }

    /// <summary>The facts the unlock gates read, all of them already carried by the save.</summary>
    private UnlockFacts GuideUnlockFacts() => new(
        WavesCleared: _deepestEver,
        DeepestWave: _deepestEver,
        // Null-guarded because ApplySkillCapacity is reachable from paths that run before LoadContent
        // has built the Forge. A hard dereference here is the exact shape of the boot crash this file
        // has already shipped once.
        ItemsOwned: _forge?.Inventory.Count(Gear.IsWearable) ?? 0,
        // EVER, not "right now": opened + still waiting. Both counters persist, so the sum is monotone
        // — which is what stops the Vault re-announcing itself every time the pile refills from empty.
        ChestsEverHeld: (_forge?.ChestsOpened ?? 0) + (_forge?.UnopenedChests.Count ?? 0),
        RegionsConquered: _world.ConqueredIds.Count,
        TraitsDiscovered: _traitLedger.DiscoveredCount,
        // THE JOURNEY'S FACTS (2026-09-06): the tree opens on points, the Build on a real choice, the
        // Roster on a second hunter. Every one is derived from state the save already carries.
        MasteryPointsEarned: SkillPointsEarned(),
        SkillsKnown: 1 + (_mastery?.AvailableSkills().Count ?? 0),
        KeystonesDiscovered: _discoveredKeystones.Count,
        VowsKnown: _discoveredVows.Count,
        CharactersUnlocked: _characters.SaveUnlocked().Count);

    /// <summary>What the BUILD screen has to choose between beyond the signature — the guide's CHOOSE BUILD rung waits on it.</summary>
    private int BuildChoicesNow()
        => (_mastery?.AvailableSkills().Count ?? 0) + _discoveredKeystones.Count + _discoveredVows.Count;


    /// <summary>
    /// Queue a notice toast: two lines, "HEAD\nDETAIL", shown at the top for a few seconds, never modal.
    /// </summary>
    /// <remarks>
    /// Quest completions and champion joins used to be modal panels in the same queue as the unlock
    /// explanations, and they were the worst of it: a panel about a champion, over the Forge, while the
    /// player was deciding what to salvage. They are a line at the top now. Queued rather than
    /// replaced, so two events on one frame (a quest finishing is what frees a quest-gated champion)
    /// are both read.
    /// </remarks>
    private void PostNotice(string head, string detail) => _noticeQueue.Enqueue(new Notice(head, detail));

    /// <summary>
    /// Post a dispatch to the inbox. True if it was news; false — and nothing posted — if the account
    /// already knew the key. The inbox is the dedupe: a producer needs no baseline of its own.
    /// </summary>
    /// <remarks>
    /// Posting does not save. A producer that latches a fact already calls <see cref="Save"/> on the
    /// same frame, so the row and its fact land in one write; the ten-second autosave carries the rest.
    /// </remarks>
    private bool PostDispatch(Dispatch dispatch)
    {
        if (!_inbox.Post(dispatch)) return false;
        _dispatchArrivalOwed = true;   // the edge the envelope's pulse is made of
        return true;
    }

    /// <summary>The arrival edge, taken once: true on the first ask after a dispatch was posted.</summary>
    private bool TakeDispatchArrival()
    {
        var owed = _dispatchArrivalOwed;
        _dispatchArrivalOwed = false;
        return owed;
    }

    /// <summary>
    /// Mark one dispatch read, and save at once — the pattern of ReportOpenedEver: a thing the player
    /// just did survives a crash before the ten-second autosave. Reading changes presentation only.
    /// </summary>
    private void MarkDispatchRead(string key)
    {
        if (_inbox.MarkRead(key)) Save();
    }

    /// <summary>Mark every dispatch read, and save at once.</summary>
    private void MarkAllDispatchesRead()
    {
        if (_inbox.MarkAllRead() > 0) Save();
    }

    /// <summary>
    /// A TRAIT HAS AWAKENED — the reveal §32 asks for: meaningful, rare, and never blocking.
    /// </summary>
    /// <remarks>
    /// Queued like any other notice, so two awakenings in one moment are shown one after the other
    /// rather than on top of each other. An established save's first load can satisfy six rules at
    /// once, and six ceremonies in one second is the failure mode §32 warns about as loudly as it
    /// asks for the ceremony — so that case posts ONE combined plate instead (see RefreshTraits).
    /// </remarks>
    /// <remarks>
    /// The flavour arrives UPPERCASED by its callers, because the inspector uppercases the same
    /// string and two surfaces showing one line in two cases is a defect you only see side by side —
    /// which is what the RH_SHOT_WAKE capture is for.
    /// </remarks>
    private void PostAwakening(string kicker, string name, string flavour)
        => _noticeQueue.Enqueue(new Notice(kicker, name, flavour, Awakening: true));

    /// <summary>The activity the screen on top belongs to — what a tour or a slot note would be about.</summary>
    private Activity ScreenActivity() => NavActivity[NavActive()];

    /// <summary>The slot note owed at the top of the current screen, or null. Never under anything above the coach's tier.</summary>
    private ScreenBanner? ScreenBannerShowing()
    {
        // THE COACH'S TIER: a note is teaching. Under the open log, a fall, a reveal, a modal or the
        // opening it would be about a screen the player is not looking at, so it waits.
        if (AttentionOwnedAbove(AttentionOwner.Coach)) return null;
        return Onboarding.BannerFor(ScreenActivity(), GuideUnlockFacts(), _explained);
    }

    /// <summary>Start the tour of a screen. The Hunt's is the intro. Under the rig, posed at the asked-for card.</summary>
    /// <remarks>
    /// RH_SHOT_STEP=n (capture.sh's card argument; RH_SHOT_T is read too, for the `intro` mode's third
    /// argument) poses card n, counted from 1. The rig poses a card by number; it never plays.
    /// </remarks>
    private void BeginTour(Activity screen) => BeginTour(screen, Onboarding.TourFor(screen), Onboarding.ScreenKey(screen));

    /// <summary>The explained-list key the running tour writes when it ends — the screen's, or a lesson's own.</summary>
    private string _tourKey = "";

    /// <summary>Start any tour over a screen: its own cards, or a lesson's (the first-gem tour) under its own key.</summary>
    private void BeginTour(Activity screen, IReadOnlyList<TourStep> cards, string key)
    {
        _tourActive = true;
        _tourScreen = screen;
        _tour = cards;
        _tourKey = key;
        _tourStep = 0;
        var posed = Environment.GetEnvironmentVariable("RH_SHOT_STEP")
                    ?? Environment.GetEnvironmentVariable("RH_SHOT_T");
        if (CaptureRig && int.TryParse(posed, out var stepNo))
            _tourStep = Math.Clamp(stepNo - 1, 0, _tour.Count - 1);
    }

    /// <summary>End the running tour — finished or skipped — and remember that in the save.</summary>
    /// <remarks>
    /// That screen is marked explained, and only that one: the Hunt's tour says the other screens
    /// EXIST, and each one has a tour of its own to be asked for on it. Marking it takes the gold
    /// NEW mark off that tile; it locks nothing away, because LEARN THIS SCREEN never reads the
    /// explained list and the same tour can be asked for again. The Hunt's tour also sets the intro
    /// flag, which only <see cref="Onboarding.SeedExplained"/> still reads — to tell a save written
    /// before the intro existed from one that has been past it.
    /// </remarks>
    private void EndTour()
    {
        _tourActive = false;
        _tourCard = Rectangle.Empty;
        _explained.Add(_tourKey.Length > 0 ? _tourKey : Onboarding.ScreenKey(_tourScreen));
        _visited.Add(_tourScreen);
        if (_tourScreen == Activity.Hunt) _introSeen = true;
        Save();
    }


    /// <summary>The small close button top-right of a hint slot or lesson card — a hit target, so it follows the profile.</summary>
    /// <remarks>
    /// It sits at the inset on any card tall enough to hold it, which is every card with a body. A
    /// ONE-LINE hint is shorter than the icon at 150 % (43 px of plate, 48 px of icon), and pinned at
    /// the inset the icon then hung 18 px past the plate onto the VAULT's own OPEN ALL button. So the
    /// drop is the inset OR what the plate can give, whichever is less — and the icon rides the plate's
    /// top edge rather than the screen's first control row. Draw rect and hit rect are this one rect.
    /// </remarks>
    private static Rectangle HintCloseRect(Rectangle slot)
    {
        var size = UiMetrics.Control(32);
        var inset = UiMetrics.Space(10);
        var band = Math.Min(slot.Height, inset * 2 + size);
        return new Rectangle(slot.Right - inset - size, slot.Y + Math.Max(0, (band - size) / 2), size, size);
    }

    // ── THE CARD GRID — one shape for the hint slot, the fight's lesson card and the intro's preview
    //    of it. A card is a title line over a wrapped body, closed with the × in its top-right corner;
    //    its height is DERIVED from the lines it holds, so a bigger profile makes a taller card rather
    //    than a card whose body runs out of its plate.

    /// <summary>A card's text inset from its left edge.</summary>
    private static int CardPadX => UiMetrics.Space(22);

    /// <summary>The lane a card's text leaves for the close icon on its right: the icon, its inset, a breath.</summary>
    private static int CardCloseLane => UiMetrics.Space(10) + UiMetrics.Control(32) + UiMetrics.Space(20);

    /// <summary>Where a card's title sits below its top edge.</summary>
    private static int CardTitleTop => UiMetrics.Space(14);

    /// <summary>Where a card's body starts: one title line under the title, plus a breath.</summary>
    private static int CardBodyTop => CardTitleTop + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(2);

    /// <summary>A card's height for a wrapped body of <paramref name="lines"/> lines — title, body, pads.</summary>
    private static int CardHeight(int lines) => CardBodyTop + lines * UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(14);

    /// <summary>A one-line card's height: the line, with the same pad above and below.</summary>
    private static int CardLineHeight => UiTypography.Body + UiMetrics.Space(13) * 2;

    /// <summary>The toast column: the stage header's own width (630..1190). Page geometry, not a control.</summary>
    private const int ToastLeft = 630, ToastWidth = 560;

    // ── The hint slot (UX V2 P0.7) ─────────────────────────────────────────────────────────────
    //
    // One place at the top of a menu screen that says the one thing worth saying about it right now:
    // a slot note (Onboarding.BannerFor), the lesson that is ABOUT this screen — the Tutorial
    // ladder that used to name them is deleted, and the live call is OnboardingLessons.Sends — or
    // a hint from real state (Onboarding.HintFor), in that order, one at a time. The strip that
    // used to cross the bottom of every screen with a lesson about another screen is gone; where a
    // lesson is about another screen, that screen's rail tile wears the NEW mark instead.

    /// <summary>What kind of thing the slot holds, which decides what closing it remembers.</summary>
    private enum SlotKind { Note, Lesson, Hint }

    /// <summary>The slot's content: the key that closes it, its title, its body (empty for a one-line hint).</summary>
    private readonly record struct SlotContent(SlotKind Kind, string Key, string Title, string Body);

    /// <summary>Hints closed this session. Not saved: a hint is about now, and returns when its fact changes.</summary>
    private readonly HashSet<string> _dismissedHints = new();

    /// <summary>The slot content owed on the current menu screen, or null. Never on the HUNT, never under anything above the coach's tier.</summary>
    private SlotContent? SlotShowing()
    {
        // THE COACH'S TIER, on a menu screen only. The slot paints OVER a reveal, the welcome, a tour and
        // the panels, whose swallow would leave its × dead — so under any of them it does not paint at all.
        if (!OverlayActive || AttentionOwnedAbove(AttentionOwner.Coach)) return null;
        if (ScreenBannerShowing() is { } note) return new SlotContent(SlotKind.Note, note.Key, note.Title, note.Body);
        var screen = ScreenActivity();
        // THE ONE LESSON THE DIRECTOR CHOSE, if the deed it asks for happens on this screen. The
        // director has already decided there is exactly one; this only asks whether it belongs here.
        if (_coach.Showing is { } lesson && OnboardingLessons.Sends(lesson) == screen && !CoachLightsIt(lesson))
            return new SlotContent(SlotKind.Lesson, lesson.ToString(),
                                   OnboardingLessons.Title(lesson), LessonLine(lesson));
        if (Onboarding.HintFor(screen, HintFactsNow()) is { } hint && !_dismissedHints.Contains(hint.Key))
            return new SlotContent(SlotKind.Hint, hint.Key, hint.Text, "");
        return null;
    }

    /// <summary>Close the slot: a note is remembered in the save, a lesson is dismissed for good, a hint for this session.</summary>
    private void CloseSlot(SlotContent slot)
    {
        switch (slot.Kind)
        {
            case SlotKind.Note: _explained.Add(slot.Key); Save(); break;
            // A CLOSED CARD IS SILENCE, NEVER COMPLETION. The fact stays false and the deed stays
            // undone; the player who wants no coaching at all has SKIP GUIDANCE in Settings, which is
            // one honest switch rather than a dozen little lies about what they have learned.
            case SlotKind.Lesson:
                if (Enum.TryParse<OnboardingLessonId>(slot.Key, out var muted)) _coach.Mute(muted);
                break;
            default: _dismissedHints.Add(slot.Key); break;
        }
        _sound.Play("sfx_click", 0.6f);
    }

    /// <summary>The stats in ladder order, once — <see cref="HintFactsNow"/> runs every frame and must not allocate.</summary>
    private static readonly HunterStat[] s_stats = Enum.GetValues<HunterStat>();

    /// <summary>
    /// What is true right now that a screen could point at — gathered from the models the host already
    /// holds. Every value here is one a screen already displays somewhere; nothing is invented.
    /// </summary>
    private HintFacts HintFactsNow()
    {
        string? newRegion = null;
        foreach (var def in Regions.All)
        {
            if (def.Id == _activeRegion || !_world.IsUnlocked(def.Id) || _world.IsConquered(def.Id)) continue;
            if (_world.RegionFarm(def.Id).BestDepth > 0) continue;   // hunted there before: not new
            newRegion = def.Name;
            break;
        }
        string? stat = null;
        long cost = 0;
        foreach (var s in s_stats)
        {
            if (!_hunter.CanTrain(s)) continue;
            var c = _hunter.NextRankCost(s);
            // THE PLAYER'S WORD, not the enum's: this line rendered "YOU CAN TRAIN ATTACKPOWER
            // FOR 25 GLEAM" — and RESONANCEAFFINITY, and CRITICALCHANCE — because the words lived
            // inside the training screen where the host could not reach them.
            if (stat is null || c < cost) { stat = TrainingScreen.WordFor(s); cost = c; }
        }
        string? upgrade = null;
        foreach (var f in Facilities.All)
            if (_warren.CanUpgrade(f.Kind, _hunter.Gleam, _dust.MemoryDust)) { upgrade = f.Name; break; }

        // AN UNSPENT SKILL LEVEL, and the first skill holding one. Read off the woven slots, so it can
        // only ever name a skill the player is actually fighting with.
        string? levelled = null;
        var levelsFree = 0;
        foreach (var woven in _loadout.Skills)
        {
            if (woven.SkillId is not { } id || SkillCatalogue.Find(id) is not { } wd) continue;
            var free = _skillProgress.FreeOn(id);
            if (free <= 0) continue;
            levelsFree += free;
            levelled ??= wd.Name;
        }

        return new HintFacts(
            NewRegionName: newRegion,
            MasteryPointsFree: _mastery.Available,
            // AN EMPTY SLOT IS ONLY WORTH NAGGING ABOUT IF SOMETHING CAN FILL IT. The count is bounded
            // by how many skills the world has actually taught, so a respec that gives a road back
            // stops the line rather than sending the player to a library of locked tiles (2026-09-09).
            EmptySkillSlots: Math.Max(0, Math.Min(Unlocks.SkillSlots(GuideUnlockFacts()),
                                                  1 + (_mastery?.AvailableSkills().Count ?? 0))
                                         - _loadout.Skills.Count),
            NewChampionName: _rosterNews ? _rosterNewName : null,
            ChestsWaiting: _forge?.UnopenedChests.Count ?? 0,
            TrainableStat: stat,
            TrainableCost: cost,
            AffordableUpgradeName: upgrade,
            // WHICH SKILL IS WAITING, and how many levels in total. Named rather than counted alone,
            // because "SPRAY HAS A LEVEL TO SPEND" sends the player to a row while "1 SKILL LEVEL"
            // sends them hunting down a column they have never scrolled to.
            SkillWithLevelToSpend: levelled,
            SkillLevelsToSpend: levelsFree);
    }

    /// <summary>
    /// Everything the lesson catalogue reads, gathered from the live game.
    /// </summary>
    /// <remarks>
    /// Every field but the four persisted ones is derived here and now, which is why onboarding needs
    /// no state machine: a save that loads in the middle of a lesson reconstructs its own eligibility
    /// from the world, and a deed done before the prompt appeared is simply already complete.
    /// </remarks>
    private LessonFacts LessonFactsNow()
    {
        var known = _mastery?.AvailableSkills().Count ?? 0;
        var signature = _characters.Active?.SignatureSkillId;
        var shared = _loadout.Skills.Count(k => k.SkillId is { } id && id != signature);
        var slots = Math.Max(0, Math.Min(Unlocks.SkillSlots(GuideUnlockFacts()), 1 + known) - _loadout.Skills.Count);

        // A VARIATION OFFERED IS A LEVEL WAITING ON A WOVEN SKILL — the one decision in this game the
        // player is never told they can make, and the reason it has a lesson of its own.
        var offered = 0;
        var chosen = 0;
        foreach (var woven in _loadout.Skills)
        {
            if (woven.SkillId is not { } id || SkillCatalogue.Find(id) is not { } def) continue;
            if (_skillProgress?.VariationOf(def) is not null) chosen++;
            else if ((_skillProgress?.FreeOn(id) ?? 0) >= 1) offered++;
        }

        return new LessonFacts(
            WavesCleared: _deepestEver,
            DeepestWave: _deepestEver,
            Gleam: _hunter.Gleam,
            StatsTrained: Enum.GetValues<HunterStat>().Sum(_hunter.RankOf),
            BossesFelled: _bossesFelled,
            ChestsHeld: _forge?.UnopenedChests.Count ?? 0,
            // THE REAL COUNTER, not a souvenir of one. This inferred an opened chest from "the bag has
            // anything in it", which is a different fact with different causes — a trader purchase, a
            // share code, a fixture, a seeded save — and it made the lesson complete for players who
            // had never touched the VAULT. ForgeScreen keeps a monotone tally of chests actually
            // opened and the save carries it (SaveGame.ChestsOpened), so ask that.
            ChestsOpened: _forge?.ChestsOpened ?? 0,
            ItemsOwned: _forge?.Inventory.Count(Gear.IsWearable) ?? 0,
            ItemsWorn: Enum.GetValues<GearSlot>().Count(sl => _hunter.Worn(sl) is not null),
            GemsHeld: _forge?.Inventory.Count(GemCraft.IsGem) ?? 0,
            // ...AND WHETHER ONE COULD ACTUALLY GO ANYWHERE. The Forge's own bag is the whole holding —
            // a worn piece lives in the inventory as well as on the doll — so one sweep covers both,
            // and GemCraft answers with the rule its own SET button obeys.
            CanSocketNow: _forge is not null
                          && GemCraft.CanSocketNow(_forge.Inventory, _hunter.MaterialOf(Material.Essence),
                                                   _forge.FreeSocketUsed),
            GemsSet: (_forge?.Inventory.Sum(i => i.Gems.Count) ?? 0)
                     + Enum.GetValues<GearSlot>().Sum(sl => _hunter.Worn(sl)?.Gems.Count ?? 0),
            MasterySpent: _mastery?.Spent ?? 0,
            MasteryPointsFree: _mastery?.Available ?? 0,
            SharedSkillsEquipped: shared,
            // LIVE ACCESS, not a latch: a respec that gives a road back takes its skill with it, so the
            // lesson stops asking for something the tree no longer reaches.
            SharedSkillsAvailable: known,
            EmptySkillSlots: slots,
            VariationsChosen: chosen,
            VariationsOffered: offered,
            KeystonesDiscovered: _discoveredKeystones.Count,
            KeystonesWorn: _loadout.KeystoneIds.Count,
            RegionsConquered: _world.ConqueredIds.Count,
            RegionsOpen: Regions.All.Count(r => _world.IsUnlocked(r.Id)),
            // ENTERED is where a wave was actually fought, which is what TRAVEL asks for — a region
            // that is merely unlocked has taught nothing.
            RegionsEntered: Regions.All.Count(r => _world.RegionFarm(r.Id).BestDepth > 0),
            TraitsDiscovered: _traitLedger?.DiscoveredCount ?? 0,
            TraitsEquipped: _traitLedger?.LoadoutOf(_characters.ActiveId).Count ?? 0,
            HuntersOwned: _characters.Unlocked.Count,
            HunterSwitches: _characters.ActiveId == CharacterRoster.StarterId ? 0 : 1,
            WarrenOpen: Unlocks.IsOpen(Activity.Warren, GuideUnlockFacts()),
            // THE CAMP IS TAUGHT AFTER IT HAS PAID, never before — a Warren tour given to somebody it
            // has done nothing for is a theory; after a payout it answers a question they already have.
            WarrenPaidOffline: _welcome is { WarrenOpen: true } w
                               && (w.Warren.Gleam > 0 || w.Warren.Dust > 0 || w.Warren.Scrap > 0 || w.Warren.Essence > 0),
            WarrenVisited: _visited.Contains(Activity.Warren),
            Falls: _fallsSeen,
            ReportOpenedEver: _reportOpenedEver,
            ChangedAfterFall: _changedAfterFall,
            RetriedAfterChange: _retriedAfterChange,
            GuidanceOff: _guidanceOff,

            // ── THE BACK HALF (coverage pass): six systems that had no voice at all. ────────────
            //
            // Every one of these is asked of the object that owns the answer rather than remembered
            // in a field of its own — the Forge's gate, the camp's facility level, the woven build's
            // sworn list, the tree's own CanTake, the stall's stock, the world's valve. A lesson that
            // read a copy would be a lesson that could disagree with the game.
            AutoSellUnlocked: WarrenAutomation.AutoSellAtOrBelow(_warren) is not null,
            AutoSellOn: _autoSellOn,
            VowsKnown: _vowMenu.Count,
            VowsSworn: _loadout.SwornVows.Count,
            // THE TREE'S OWN RULE, not a threshold. CanTake already answers "points, road and the
            // one-discipline law", so the card cannot arrive a single point early or a ring too soon.
            SpecialisationReachable: _mastery is not null
                                     && MasteryCatalog.Nodes.Any(n => n.Kind == MasteryKind.Specialisation
                                                                      && _mastery.CanTake(n.Id)),
            SpecialisationTaken: _mastery?.Affinity() is not null,
            TraderStocked: _vault?.TraderStock.Count > 0,
            TraderBought: _traderBought.Count,
            CanDeepenWorld: _world.CanDeepenCorruption,
            CorruptionTier: _world.CorruptionTier,

            // THE ACCOUNT'S OWN NEWS. The inbox answers "is anything waiting"; the persisted latch
            // answers "has anybody ever looked" — written by the surface itself, never by a card.
            DispatchesUnread: _inbox.Unread,
            DispatchesOpenedEver: _dispatchesOpenedEver);
    }

    /// <summary>
    /// Watch the loop the whole game is about: fall, read, change, retry.
    /// </summary>
    /// <remarks>
    /// Three facts nothing else in the game records, each latched once and never unlatched. The change
    /// is a DELTA against a snapshot taken at the fall, so opening a screen is not a change and closing
    /// a card is not a change — only moving a rank, a worn piece, a node or the woven build is.
    /// </remarks>
    private void WatchTheFallLoop()
    {
        var falls = _expedition?.Log.Entries.Count ?? 0;
        if (falls > _fallsSeen)
        {
            _fallsSeen = falls;
            // The first fall is the one that matters: snapshot what the build was, so the change that
            // answers the report can be recognised whatever subsystem it happens in.
            _fallSnapshot ??= BuildSnapshot();
        }

        if (_fallsSeen == 0 || _retriedAfterChange) return;

        if (!_changedAfterFall && _fallSnapshot is { } was && BuildSnapshot() != was)
        {
            _changedAfterFall = true;
            _retryFrom = _expedition?.RunsStarted ?? 0;
            Save();
        }
        else if (_changedAfterFall && _retryFrom is { } started && (_expedition?.RunsStarted ?? 0) > started)
        {
            _retriedAfterChange = true;
            Save();
        }
    }

    /// <summary>
    /// SIGNATURE — said the first time the hunter's own skill actually fires, and never again.
    /// </summary>
    /// <remarks>
    /// The old ladder announced the signature skill when the fight began, which is a claim rather than
    /// a demonstration: the player is told they have a unique skill before they have seen one go off.
    /// The fact this reads is <see cref="SkillProgress.UsesOf"/>, which the simulation writes when the
    /// skill is cast — so the sentence lands on a thing that has just happened on screen.
    /// </remarks>
    /// <remarks>
    /// AFTER THE FIRST WAVE, NOT DURING IT. The signature usually fires within seconds of the opening,
    /// and raising it there would take the slot off YOUR HUNTER FIGHTS FOR YOU three seconds in — the
    /// first thing said, and the only one about the whole game rather than one skill. The window is
    /// then the first few waves of a first descent, which is also what keeps it away from a returning
    /// save: a player who has fallen, felled a boss, or gone deeper than one is not told about their
    /// own skill.
    /// </remarks>
    private void RaiseSignatureBeat()
    {
        // NOT WHILE THE OPENING RUNS: it has its own SIGNATURE SKILL card for this cast. Raised under it,
        // the observe beat waited out the opening and arrived the moment it ended, saying the same thing
        // over BACK TO THE HUNT (seen on the autoplayed opening, 2026-09-11) — FirstBoss's rule, again.
        //
        // ...AND NOT ONCE A BOSS HAS FALLEN, which is what actually closes the window. `Running` was
        // doing half the job and a DEATH was accidentally doing the other half: the window also wanted
        // `_deepestEver > BossEvery`, and the opening HOLDS the wave after the tutorial boss for the
        // chest/item/equip beats — so at the frame the opening ends the deepest wave is exactly 5, the
        // `>` misses by one, and the beat fires over BACK TO THE HUNT after all. It never showed while
        // the fresh Hunter lost that boss (a fall spent the `_fallsSeen` guard first); the instant the
        // boss became winnable, the duplicate appeared. `_bossesFelled` is the fact the window meant all
        // along — persisted, monotone, and true the moment the boss the opening is about goes down.
        if (_guidanceOff || _fallsSeen > 0 || _bossesFelled > 0 || _opening.Running) return;
        if (_deepestEver < 1 || _deepestEver > OnboardingLessons.BossEvery) return;
        if (_characters.Active?.SignatureSkillId is not { } sig) return;
        if ((_skillProgress?.UsesOf(sig) ?? 0) < 1) return;
        _coach.Raise(OnboardingLessonId.SignatureSeen);
    }

    /// <summary>The four numbers a real change moves. Any one of them differing is a change.</summary>
    private (int Ranks, int Worn, int Mastery, int BuildRev) BuildSnapshot()
        => (Enum.GetValues<HunterStat>().Sum(_hunter.RankOf),
            Enum.GetValues<GearSlot>().Count(sl => _hunter.Worn(sl) is not null),
            _mastery?.Spent ?? 0,
            _loadout.Skills.Count * 97 + _loadout.KeystoneIds.Count * 31
            + _loadout.Skills.Sum(k => k.SkillId?.GetHashCode() ?? 0));

    /// <summary>The descent count at the moment the change landed — the retry is the next one after it.</summary>
    private int? _retryFrom;

    // ── The HUNT's lesson card (UX V2 P0.7) ────────────────────────────────────────────────────
    //
    // The fight's own rungs — Watch, MeetABoss, Conquer, and OpenChest while no chest is held — are the
    // HUNT's onboarding. They render one at a time in the toast slot under the header stack, as a quiet
    // card with an ×, and yield to the boot and notice toasts (a queue, not a stack).

    /// <summary>The fight rung to show on the HUNT this frame, or null.</summary>
    private OnboardingLessonId? HuntLessonShowing()
    {
        // THE COACH'S TIER, on the fight only. Nothing is said over the open log, over a fall — the Hunter
        // collapsing, the black and the stage coming back are the one thing on screen — over a modal or
        // the opening; the card waits for the owner to hand the frame back.
        if (OverlayActive || AttentionOwnedAbove(AttentionOwner.Coach)) return null;
        if (BootToastShowing) return null;                                  // the welcome toast has the slot
        if (_noticeTimer > 0f && _notice.Head.Length > 0) return null;     // so does a notice
        if (_coach.Showing is not { } step) return null;
        // THE SPOTLIGHT SAYS IT INSTEAD, wherever it can. The toast slot is the place the playtest
        // said is not read; a lesson that has something to light carries its own words beside the
        // light. This card is what is left for the few that light nothing.
        if (CoachLightsIt(step)) return null;
        // A lesson about another screen is that screen's business (and its tile's dot), not the fight's.
        return OnboardingLessons.Sends(step) is null ? step : null;
    }

    /// <summary>Where the lesson card hangs: the toast slot, as tall as its wrapped body.</summary>
    private Rectangle HuntLessonRect(OnboardingLessonId step)
    {
        var body = _ui.WrapBig(LessonLine(step), ToastWidth - CardPadX - CardCloseLane, UiTypography.Secondary);
        return new Rectangle(ToastLeft, ToastTop, ToastWidth, CardHeight(body.Count));
    }

    private void DrawHuntLesson()
    {
        if (HuntLessonShowing() is { } step) DrawLessonCard(step, 1f);
    }

    /// <summary>One fight rung as a card — the live one, or the intro's preview of what a lesson looks like.</summary>
    private void DrawLessonCard(OnboardingLessonId step, float alpha)
    {
        var r = HuntLessonRect(step);
        var body = _ui.WrapBig(LessonLine(step), r.Width - CardPadX - CardCloseLane, UiTypography.Secondary);
        // The QUIET plate with the gold rule, the same surface the boot toast wears in this slot — one
        // surface per toast slot (release polish 2026-09-05, hunt-07).
        _ui.Plate(_batch, r, UiInk.Accent * alpha, alpha);
        _ui.TextBig(_batch, OnboardingLessons.Title(step), r.X + CardPadX, r.Y + CardTitleTop, UiInk.Accent * alpha, UiTypography.Body);
        var ty = r.Y + CardBodyTop;
        foreach (var line in body)
        {
            _ui.TextBig(_batch, line, r.X + CardPadX, ty, UiInk.Primary * alpha, UiTypography.Secondary);
            ty += UiTypography.Pitch(UiTypography.Secondary);
        }
        _ui.CloseButton(_batch, HintCloseRect(r), ChromeMouse, false);   // drawn here; the click is handled in Update
    }

    /// <summary>
    /// A lesson's body: the deed, then the one line that answers "why should I care?".
    /// </summary>
    /// <remarks>
    /// Both halves are usually empty — an Observe lesson asks for nothing and most guided ones need no
    /// justification — so this is a short line far more often than it is two. That is the point: the
    /// prompt it replaced spent three sentences and a keyboard shortcut telling somebody to train a
    /// stat, and a prompt nobody finishes reading teaches nothing.
    /// </remarks>
    private static string LessonLine(OnboardingLessonId id)
    {
        var action = OnboardingLessons.Action(id);
        var why = OnboardingLessons.Why(id);
        if (action.Length == 0) return why;
        return why.Length == 0 ? action : $"{action}  ·  {why}";
    }

    /// <summary>Close a fight lesson for good — remembered in the save, like closing any rung.</summary>
    private void CloseLesson(OnboardingLessonId step)
    {
        // Silences this one lesson's presentation for the session. It completes nothing — see the note
        // on the slot's close, and OnboardingDirector.Mute.
        _coach.Mute(step);
        _sound.Play("sfx_click", 0.6f);
    }

    /// <summary>
    /// Everything a champion switch owes the player: take off every worn piece the new champion's class
    /// cannot wear, empty every slot holding the old champion's own skill, say what happened, and save —
    /// the switch and the repair must land in the same file.
    /// </summary>
    /// <remarks>
    /// Nothing is destroyed by either half. The pieces are never removed from the bag: a worn item is a
    /// bag item the doll points at, so <c>Unequip</c> alone puts it back in the grid, dimmed and locked,
    /// where the hover card names who can wear it. The unwoven skill keeps every wave spent on it —
    /// <c>SkillProgress</c> is keyed by skill id and is not touched here (LAW 4) — and the slot itself
    /// stays, empty and ready. The toast rides the same channel as a locked rail tile — top of the
    /// screen, gone in a few seconds — because it is a notice, not a lesson.
    /// </remarks>
    private void RepairForSwitch()
    {
        var who = _characters.Active;
        // THE NEW CHAMPION'S SKILL BECOMES THE UNTOUCHABLE ONE before anything is shed or placed: the
        // loadout's verbs refuse to move whatever is named here, and naming the OUTGOING champion's
        // skill would make the switch unable to take it out.
        _loadout.SignatureSkillId = who?.SignatureSkillId;
        var shed = new List<string>();
        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            if (_hunter.Worn(slot) is not { } worn || Gear.CanWear(who, worn)) continue;
            _hunter.Unequip(slot);
            shed.Add(ItemNaming.TypeWord(worn));
        }

        // AND THE BUILD IS REPAIRED IN THE SAME BREATH. One loadout follows the player across every
        // switch, so the champion just left behind leaves their own SIGNATURE in a slot — a slot the
        // composer refuses (BuildComposer: "a signature that belongs to somebody else is refused here")
        // and which would therefore sit filled and dead. Only foreign signatures go: a shared skill the
        // current mastery allocation has locked keeps its slot and reads LOCKED on the BUILD screen,
        // because a switch must not quietly finish what a respec started (BRIEF sec.13, sec.20, LAW 1).
        var unwoven = LoadoutRepair.ShedForeignSignatures(_loadout, who);
        // AND THE NEW CHAMPION'S OWN SIGNATURE GOES IN (release polish 2026-09-05) — into the slot the
        // old one just left, or any empty slot; never over a shared skill the player chose. The toast
        // says so, so the switch reads as an exchange rather than a loss.
        var placed = LoadoutRepair.EnsureSignature(_loadout, who);
        var placedName = placed >= 0 && who.SignatureSkillId is { } ps && SkillCatalogue.Find(ps) is { } pd ? pd.Name.ToUpperInvariant() : null;
        if (shed.Count == 0 && unwoven.Count == 0 && placedName is null) return;

        var lines = new List<string>();
        if (shed.Count > 0)
        {
            var words = shed.Count == 1 ? shed[0] : string.Join(", ", shed.Take(shed.Count - 1)) + " AND " + shed[^1];
            lines.Add(shed.Count == 1
                ? $"{who.Name} CANNOT WEAR YOUR {words} — IT IS BACK IN YOUR BAG"
                : $"{who.Name} CANNOT WEAR YOUR {words} — THEY ARE BACK IN YOUR BAG");
        }
        if (unwoven.Count > 0)
        {
            var names = unwoven.Select(d => d.Name.ToUpperInvariant()).ToList();
            var said = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " AND " + names[^1];
            lines.Add(names.Count == 1
                ? (placedName is not null
                    ? $"{said} STAYS WITH ITS OWN HUNTER — {placedName} TAKES ITS SLOT, AND EVERY LEVEL IS KEPT"
                    : $"{said} BELONGS TO ANOTHER HUNTER — ITS SLOT IS EMPTY, AND EVERY LEVEL ON IT IS KEPT")
                : $"{said} BELONG TO ANOTHER HUNTER — THEIR SLOTS ARE EMPTY, AND EVERY LEVEL ON THEM IS KEPT");
        }
        else if (placedName is not null)
            lines.Add($"{placedName} — {who.Name.ToUpperInvariant()}'S OWN SKILL — IS EQUIPPED");
        _lockedMsg = string.Join("  ·  ", lines);
        _lockedTimer = 4.5f;
        Save();
    }

    /// <summary>The toast for clicking a locked rail tile. Says the price, then fades.</summary>
    private void DrawLockedToast()
    {
        // FEEDBACK TIER: a refusal never paints over anything the player is meant to be reading.
        if (_lockedTimer <= 0f || _lockedMsg.Length == 0 || AttentionOwnedAbove(AttentionOwner.Feedback)) return;

        var fade = MathF.Min(1f, _lockedTimer / 0.5f);
        var w = LockedToastWidth;
        // TWO LINES WHEN TWO THINGS HAPPENED. A switch can shed gear AND unweave a signature in the same
        // instant, and a box built for exactly one line answered that by cutting the second fact off
        // mid-word. It still draws as one line whenever one line is what there is.
        var wrapped = _ui.WrapBig(_lockedMsg, w - UiMetrics.Space(40), UiTypography.OverlayBody).Take(2).ToList();
        var h = wrapped.Count * UiTypography.Pitch(UiTypography.OverlayBody) + UiMetrics.Space(17) * 2 - (UiTypography.Pitch(UiTypography.OverlayBody) - UiTypography.OverlayBody);
        var box = new Rectangle(UiKit.PageCenterX - w / 2, LockedToastTop, w, h);
        _ui.Fill(_batch, box, new Color(0x1A, 0x13, 0x11) * (0.92f * fade));
        _ui.Fill(_batch, new Rectangle(box.X, box.Y, box.Width, 3), UiInk.Accent * fade);
        var ly = box.Y + UiMetrics.Space(17);
        foreach (var line in wrapped)
        {
            _ui.TextCenterBig(_batch, line, box.Center.X, ly, Color.White * fade, UiTypography.OverlayBody);
            ly += UiTypography.Pitch(UiTypography.OverlayBody);
        }
    }

    /// <summary>The locked-tile toast's width and where it hangs — a readable line under the pills. Page geometry.</summary>
    private const int LockedToastWidth = 900;

    /// <summary>
    /// The locked-tile toast's right edge — and the wall the HUNT's log medallion stands clear of.
    /// </summary>
    /// <remarks>
    /// Published for the same reason <see cref="ChromeRowBottom"/> is: two surfaces that must never
    /// share a pixel should read one number, not two literals that happen to agree at 100 %. This one
    /// is what decides where the medallion can live at all — see <c>HuntScreen.LogButtonRect</c>.
    /// </remarks>
    internal static int LockedToastRight => UiKit.PageCenterX + LockedToastWidth / 2;

    /// <summary>
    /// Where the locked-tile refusal hangs — the band under the chrome row, at every profile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS WAS BRIEFLY MOVED DOWN, to get out from under the log medallion after the medallion itself
    /// had to move to escape the currency capsules at 150 %. Photographing it (RH_SHOT_LOCKED) showed
    /// what the arithmetic had missed: the band below the medallion is the TOAST SLOT — where the
    /// welcome toast, the notice and the fight's lesson card live — so the refusal simply traded a
    /// collision with a button for a collision with onboarding copy, printed through it.
    /// </para>
    /// <para>
    /// There is no third band. At 150 % the capsules own y 16..106 out to x 1191 and cannot be made
    /// narrower (three ornate capsules of one digit each), so the medallion must sit below them; and a
    /// 900-wide line centred on the page crosses the medallion's column wherever it is put. So the
    /// toast keeps the band it has always had, and for its three seconds it covers the medallion's
    /// upper half — a permanent control the player can press a moment later, rather than a lesson that
    /// does not come back.
    /// </para>
    /// </remarks>
    private const int LockedToastTop = 96;

    // ── The slot-note banner ───────────────────────────────────────────────────────────────────
    //
    // The one banner left. Every screen's first explanation used to be one of these — a paragraph in
    // small type under the title — and the playtest found it "too crowded and too small" next to the
    // Hunt's spotlight tour. The screens have tours now (DrawTour); this is kept for the thing that is
    // genuinely a one-line note: a skill slot that opened after the BUILD screen did.

    /// <summary>The banner's width — a readable line, not the whole content width.</summary>
    /// <remarks>
    /// The first cut spanned the content (1700px) and the Stats explanation came out as two lines of
    /// a hundred and sixty characters each, which nobody reads to the end. 1200 keeps a line near
    /// a comfortable reading line and centres the slot under the screen's title.
    /// </remarks>
    private const int ScreenBannerWidth = 1200;

    /// <summary>The width the banner wraps its body to — inside the left inset and clear of the ×.</summary>
    private static int ScreenBannerWrap => ScreenBannerWidth - CardPadX - CardCloseLane;

    /// <summary>
    /// Where the banner hangs: under the screen's title strip, and under its SUBTITLE when it has one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only three screens draw a subtitle line — VAULT's rarity tally, TRAITS' "ONE SPINE · FOUR ROADS
    /// · NO TAKING BACK" and MASTERY's "FOUR DIRECTIONS · ONE STYLE · TWELVE SKILLS TO LEARN" — and on
    /// those the slot must clear it: at 125 and 150 % a slot pinned at the 100 % literal let all three
    /// read straight through the plate as ghost text.
    /// </para>
    /// <para>
    /// On the eight screens that draw NO subtitle, that line's room is the slot's, and taking it is not
    /// a nicety: the band between the title strip and the first panel is only a few pixels at 150 %, so
    /// a slot dropped by a subtitle that was never drawn lands on the panel instead and cuts its first
    /// caption in half (TRAINING's "DAMAGE · TRAINED STAT", build/shots/r2_stats_150.png). Ask the
    /// screen, rather than assuming every screen looks like the three that do.
    /// </para>
    /// </remarks>
    private static int ScreenBannerTop(bool hasSubtitle) => CanvasY(hasSubtitle ? PageSubtitleBottom : PageSubtitleTop);

    /// <summary>Whether the screen on the page prints a subtitle under its gold rule — see <see cref="ScreenBannerTop"/>.</summary>
    private bool ScreenDrawsSubtitle => _showVault || _showTraits || _showMastery;

    /// <summary>The least a one-line hint can be: its line, with the tightest pad above and below.</summary>
    private static int HintLineMin => UiTypography.Body + UiMetrics.Space(4) * 2;

    /// <summary>
    /// A one-line hint's height: the card's comfortable line (48) when the band above the first panel
    /// has room for it, else what the band has left — never under <see cref="HintLineMin"/>, so the
    /// line itself is never squeezed. The band is 41 px at 100 % and gone by 150 %, where the hint
    /// overruns the panel's top rail rather than the subtitle above it.
    /// </summary>
    private static int HintLineHeight(bool hasSubtitle) => Math.Min(CardLineHeight, Math.Max(HintLineMin, CanvasY(PageContentTop) - ScreenBannerTop(hasSubtitle)));

    /// <summary>
    /// Where the banner sits: under the screen's title band, centred over the content.
    /// </summary>
    /// <remarks>
    /// Just under the currency capsules, so the banner covers neither them nor the settings gear — both
    /// are live chrome, and a banner over a button is a button the player cannot press. A one-line hint
    /// stays in the band the screens leave free; a hint with a body covers the top of the screen's own
    /// content, which is the point: it is that screen's explanation, and it is closed with one click.
    /// </remarks>
    private Rectangle HintSlotRect(SlotContent slot)
    {
        var x = NavRailWidth + (UiKit.Page.Width - NavRailWidth - ScreenBannerWidth) / 2;
        var sub = ScreenDrawsSubtitle;
        if (slot.Body.Length == 0) return new Rectangle(x, ScreenBannerTop(sub), ScreenBannerWidth, HintLineHeight(sub));   // a one-line hint
        var body = _ui.WrapBig(slot.Body, ScreenBannerWrap, UiTypography.Secondary);
        return new Rectangle(x, ScreenBannerTop(sub), ScreenBannerWidth, CardHeight(body.Count));
    }

    /// <summary>
    /// The slot at the top of the screen on top: a slot note, the lesson about this screen, or a one-line
    /// hint from its state — the quiet plate with a gold rule, closed with the ×.
    /// </summary>
    /// <remarks>
    /// Not a timed toast, for the reason the old panel was not one: a note is a paragraph, and a
    /// paragraph on a timer is a paragraph nobody finishes. Not modal either, for the reason the old
    /// panel was removed: it is on the screen it is about, and the player came here on purpose. A hint
    /// is one line and needs no timer at all — it leaves when its fact does.
    /// </remarks>
    /// <summary>
    /// Play what a screen just asked for, if anything. Every screen hands the host a cue the same way
    /// (a private field set at the semantic moment, returned once and cleared), because the host owns
    /// audio and a screen that played its own sound could not be muted, throttled or reordered.
    /// </summary>
    /// <remarks>
    /// A screen may name more than one cue for one moment — the vault's rare chest is an open AND a
    /// shimmer — so a comma-separated list is a list. SoundBank's per-cue minimum gap does the rest.
    /// </remarks>
    private void PlayCue(string? cue, float volume = 0.7f)
    {
        if (cue is null) return;
        foreach (var name in cue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            _sound.Play(name, volume);
    }

    /// <summary>The hint slot's opaque ground: <see cref="UiInk.Plate"/>'s own colour at full alpha.</summary>
    private static readonly Color SlotGround = new(UiInk.Plate.R, UiInk.Plate.G, UiInk.Plate.B);

    private void DrawHintSlot()
    {
        if (SlotShowing() is not { } slot) return;

        var r = HintSlotRect(slot);
        // OPAQUE, unlike every other plate in the game. UiInk.Plate is 0xE0 alpha, which is right for a
        // plate that sits on a screen's own background and wrong for the ONE plate that sits on a
        // screen's own CONTENT: the slot deliberately covers the top of what is behind it (that is the
        // design — it is this screen's explanation, closed with one click), and at 12 % see-through the
        // thing it covers reads straight through it. At 150 % that put MASTERY's road name and its
        // "STRONGER SKILLS, BEFORE YOU PICK ONE" caption inside the sentence about mastery points
        // (build/shots/c6b_buildtree_150.png, before this fill). A flat ground under the plate makes
        // the slot say one thing at a time, at every profile.
        _ui.Fill(_batch, r, SlotGround);
        _ui.Plate(_batch, r, UiInk.Accent);
        if (slot.Body.Length == 0)
        {
            _ui.TextBig(_batch, _ui.ShortenBig(slot.Title, ScreenBannerWrap, UiTypography.Body), r.X + CardPadX,
                        r.Y + (r.Height - UiTypography.Body) / 2, UiInk.Primary, UiTypography.Body);
        }
        else
        {
            _ui.TextBig(_batch, _ui.ShortenBig(slot.Title, ScreenBannerWrap, UiTypography.Body), r.X + CardPadX,
                        r.Y + CardTitleTop, UiInk.Accent, UiTypography.Body);
            var ty = r.Y + CardBodyTop;
            foreach (var line in _ui.WrapBig(slot.Body, ScreenBannerWrap, UiTypography.Secondary))
            {
                _ui.TextBig(_batch, line, r.X + CardPadX, ty, UiInk.Primary, UiTypography.Secondary);
                ty += UiTypography.Pitch(UiTypography.Secondary);
            }
        }
        _ui.CloseButton(_batch, HintCloseRect(r), ChromeMouse, false);   // drawn here; the click is handled in Update
    }

    // ── Notice toasts ──────────────────────────────────────────────────────────────────────────

    /// <summary>A quest finished, a champion joined: two lines at the top, fading on their own clock.</summary>
    /// <remarks>
    /// The boot toast's shape and place, because it IS the same kind of thing — news, not a lesson.
    /// It steps down under the boot toast on the one occasion both are up (a quest that was already
    /// satisfied when the save loaded). Never modal, never reads input.
    /// </remarks>
    private void DrawNoticeToast()
    {
        // NOTHING CLICKABLE PAINTS WHEN ITS INPUT IS BLOCKED, and this plate has a ×: on every frame it is
        // not painted the rect the × is read from is cleared first, so a hidden close cannot take a click.
        _noticeCloseRect = Rectangle.Empty;
        if (_noticeTimer <= 0f || _notice.Head.Length == 0) return;
        // FEEDBACK TIER. News waits under anything the player is meant to be reading — a lit lesson, the
        // open log, a fall, a reveal, a modal (the SPECIALISATION ceremony had its own title covered by a
        // quest toast at 125 %; a toast across a tour's spotlight is two lessons at once) and the authored
        // opening — and its clock waits with it (Update), so it lands when the frame is handed back.
        if (AttentionOwnedAbove(AttentionOwner.Feedback)) return;
        if (NoticeHeld) return;   // the band belongs to a slot reveal; the clock is held with it
        if (OpeningRigOn) _rigNoticeDrawn = _notice.Head;   // dev: the opening rig's trace

        var fade = Math.Clamp(_noticeTimer / 1.0f, 0f, 1f);
        // IN THE LANE on a menu screen (ReserveNoticeLane): the toast stands where the body's first row
        // used to start, and the body has moved down to make room; it arrives with the lane. On the
        // hunt it stays an overlay under the header stack, stepping down under the boot toast.
        var y = BootToastShowing ? ToastTop + ToastHeight + UiMetrics.Space(8) : ToastTop;
        if (OverlayActive)
        {
            y = CanvasY(UiKit.PageTopBase);
            fade *= _noticeLaneOpen;
        }
        // THE HEIGHT FOLLOWS THE TEXT, at both shapes, so nothing can fall out of the plate.
        //
        // Two notices meet here and each fixed half of the same fault. The trait AWAKENING is three
        // rungs — a quiet kicker, the trait's own NAME in gold at the title size, then its flavour —
        // and its height is summed from those rungs rather than guessed. Every OTHER notice is a
        // headline and a body, and its body WRAPS: it used to be one ellipsised line, which was fine
        // while every notice was a short sentence and wrong the moment keystone reveals arrived,
        // because a keystone has to say what it DOES — the word IRONCLAD alone teaches nobody
        // anything — and the longest of those is 164 characters against a line that fits about 80.
        var pad = UiMetrics.Space(22);
        var r0 = new Rectangle(UiKit.PageCenterX - NoticeToastWidth / 2, y, NoticeToastWidth, 0);
        // MEASURED AGAINST THE CLOSE BUTTON, on BOTH sides, because this text is CENTRED: a reserve
        // on the right alone moves the middle, and the line still reaches the corner the x sits in.
        // At 150 % the second line of a keystone reveal ran straight under it.
        var room = r0.Width - 2 * (UiKit.PanelCorner + 8 + UiMetrics.Control(UiKit.CloseSize) + UiMetrics.Space(8));
        var body = !_notice.Awakening && _notice.Detail.Length > 0
            ? _ui.WrapBig(_notice.Detail, room, UiTypography.OverlayBody).Take(NoticeBodyLines).ToList()
            : new List<string>();
        var rungs = _notice.Awakening
            ? UiTypography.Pitch(UiTypography.OverlayBody)      // the kicker
              + UiTypography.Pitch(UiTypography.OverlayTitle)   // the NAME
              + UiTypography.Pitch(UiTypography.OverlayBody)    // the flavour
            : UiTypography.Pitch(UiTypography.OverlayTitle)
              + UiTypography.Pitch(UiTypography.OverlayBody) * Math.Max(1, body.Count);
        var h = pad + rungs + UiMetrics.Space(12);
        // ── IT ARRIVES, rather than being there. ─────────────────────────────────────────────────
        //
        // Playtest 2026-09-09: "nobody pays any attention to the notification messages at the top."
        // The largest part of that is answered by the rail's unread dot, which holds until the screen
        // is looked at — but a toast that simply exists for six seconds and fades is also easy to miss
        // on a screen where a fight is moving. It drops in over the first fifth of a second and takes
        // a bar down its leading edge, which is what a notification looks like everywhere else.
        var entry = Math.Clamp((NoticeSeconds - _noticeTimer) / 0.2f, 0f, 1f);
        var drop = UiMotion.Reduced ? 0 : (int)MathF.Round((1f - UiMotion.Smooth(entry)) * UiMetrics.Space(18));
        var r = new Rectangle(r0.X, r0.Y - drop, r0.Width, h);
        // A toast weighs less than the panels it hangs over: the house plate with the accent rule, as the
        // boot toast and the hint slot wear — not a framed panel (chrome-08). The backdrop is heavier
        // than it was: this thing has to win against a wave of creatures moving behind it.
        _ui.Fill(_batch, r, Color.Black * (0.55f * fade));
        _ui.Plate(_batch, r, UiInk.Accent, fade);
        // THE BAR IS THE SIGNAL. One thick accent edge down the left, the shape a notification wears in
        // every interface a player has used, and the one element here that is not shared with the four
        // other plates this game hangs at the top of a screen.
        _ui.Fill(_batch, new Rectangle(r.X, r.Y, Math.Max(3, UiMetrics.Control(6)), r.Height), UiInk.Accent * fade);
        // A DISMISS, at the house close position: the toast leaves on its timer, and on a click too.
        // Drawn here; the click is read in Update (DismissNoticeIfClosed) like the hint slot's.
        _noticeCloseRect = UiKit.CloseRect(r);
        _ui.CloseButton(_batch, _noticeCloseRect, ChromeMouse, false);

        if (_notice.Awakening)
        {
            // THE MIDDLE RUNG IS THE LOUD ONE. "A TRAIT HAS AWAKENED" is only the kicker; the trait's
            // own NAME is what the player carries away, so it takes the title size and the gold, and
            // the flavour sits under it in the body size.
            var ty = r.Y + pad;
            _ui.TextCenterBig(_batch, _ui.ShortenBig(_notice.Head, room, UiTypography.OverlayBody),
                              r.Center.X, ty, Slate * fade, UiTypography.OverlayBody);
            ty += UiTypography.Pitch(UiTypography.OverlayBody);
            _ui.TextCenterBig(_batch, _ui.ShortenBig(_notice.Detail, room, UiTypography.OverlayTitle),
                              r.Center.X, ty, NavGold * fade, UiTypography.OverlayTitle);
            ty += UiTypography.Pitch(UiTypography.OverlayTitle);
            if (_notice.Third is { } flavour)
                _ui.TextCenterBig(_batch, _ui.ShortenBig(flavour, room, UiTypography.OverlayBody),
                                  r.Center.X, ty, Bone * fade, UiTypography.OverlayBody);
            return;
        }

        _ui.TextCenterBig(_batch, _ui.ShortenBig(_notice.Head, room, UiTypography.OverlayTitle),
                          r.Center.X, r.Y + pad, NavGold * fade, UiTypography.OverlayTitle);
        var bodyY = r.Y + pad + UiTypography.Pitch(UiTypography.OverlayTitle);
        foreach (var line in body)
        {
            _ui.TextCenterBig(_batch, _ui.ShortenBig(line, room, UiTypography.OverlayBody),
                              r.Center.X, bodyY, Bone * fade, UiTypography.OverlayBody);
            bodyY += UiTypography.Pitch(UiTypography.OverlayBody);
        }
    }

    /// <summary>Where the notice's close control was drawn last frame — empty while no notice shows.</summary>
    private Rectangle _noticeCloseRect;

    /// <summary>The notice's × — read where the hint slot's is, before the screens take the click.</summary>
    private void DismissNoticeIfClosed()
    {
        if (_noticeTimer <= 0f || _noticeCloseRect.IsEmpty) return;
        if (MouseClicked && _noticeCloseRect.Contains(ChromeMouse)) { _noticeTimer = 0f; _noticeCloseRect = Rectangle.Empty; _swallowInput = true; }
    }

    /// <summary>How many wrapped body lines a notice may grow to before it is cut.</summary>
    /// <remarks>
    /// Four holds every string the game posts at every UI SCALE. The longest is CAPACITOR's keystone
    /// reveal at 164 characters — its blurb, plus the sentence naming the rung that taught it — which
    /// wraps to three lines at SCALE 100 and to four at 150, where the type is larger and the plate is
    /// not. This is a ceiling for a rare case rather than a target: a toast that needs more than four
    /// lines is a copy problem, not a layout one, and `keystonenotice` is the mode that shows it.
    /// </remarks>
    private const int NoticeBodyLines = 2;

    /// <summary>The notice toast's width — a readable two-line plate, centred, wider with the profile so the line count does not climb at 150 %.</summary>
    private static int NoticeToastWidth => Math.Min(UiMetrics.Control(800), UiKit.Page.Width * 2 / 3);

    // ── The tours ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The spotlight cut-outs for a tour card, in 1920×1080 chrome space. The first is the one the
    /// caption card is placed beside.
    /// </summary>
    /// <remarks>
    /// Each screen measures its own regions in its own coordinates (<c>Spotlights</c> on every screen
    /// class, next to the rectangles it draws with), and this is the one place they are brought into
    /// the chrome's: the menu screens draw inset through <see cref="OverlayTransform"/>, so their
    /// rectangles go through the same matrix and then grow by ten pixels, so the frame art is inside
    /// the light rather than cut by it. The fight screen is not inset and measures its own margin. The
    /// MASTERY tile is the rail's, so it is answered here. A target no screen claims lights the whole
    /// canvas — visibly wrong, which is the point: a missing rectangle must never pass as a card whose
    /// light happens to be off.
    /// </remarks>
    private Rectangle[] TourSpotlights(Activity screen, TourTarget target)
    {
        // THE CHEST'S REVEAL CARD is host chrome, drawn in canvas space over whichever screen opened the
        // chest — so it never goes through a screen's overlay transform. Nothing lit when no reveal is up.
        if (target == TourTarget.RevealedItem)
        {
            if (!_forge.RevealActive || _forge.RevealCardRect.IsEmpty) return new[] { new Rectangle(0, 0, 1920, 1080) };
            var reveal = _forge.RevealCardRect;
            reveal.Inflate(SpotlightHalo, SpotlightHalo);
            return new[] { reveal };
        }

        // THE ENVELOPE IS HOST CHROME: canvas space on every screen, so it never goes through a
        // screen's overlay transform. Answered here rather than by whichever screen happens to be up.
        if (target == TourTarget.DispatchIcon)
        {
            var mail = DispatchButton;
            mail.Inflate(SpotlightHalo, SpotlightHalo);
            return new[] { mail };
        }

        if (target == TourTarget.MasteryTile)
        {
            // The tile, where it stands on the rail — or, while the tree is not revealed yet, the
            // rail's next empty slot: the card says when the tile appears, and that is where.
            var slots = NavSlots();
            var slot = slots.IndexOf(Array.IndexOf(NavActivity, Activity.Mastery));
            return new[] { NavHexRect(slot >= 0 ? slot : slots.Count) };
        }

        // THE AUTHORED OPENING'S OWN CONTROLS: one item's cell, and one chest's card. The host knows
        // WHICH item and WHICH chest a beat is about; the screen knows where it drew them. Both come
        // back in the screen's page space and go through the same transform as every other light, so
        // the lit rectangle and the clicked rectangle are still one rectangle.
        Rectangle[]? hostOwned = (screen, target) switch
        {
            (Activity.Gear, TourTarget.InventoryItem)
                => OpeningLitItem() is { } item && _gear.CellOf(item, _hunter) is { } cell ? new[] { cell } : Array.Empty<Rectangle>(),
            (Activity.Vault, TourTarget.ChestCard)
                => OpeningLitChest() is { } chest && _vault.CardOf(chest, _forge.UnopenedChests) is { } card ? new[] { card } : Array.Empty<Rectangle>(),
            _ => null,
        };
        var own = hostOwned ?? screen switch
        {
            Activity.Hunt => HuntScreen.Spotlights(target),
            Activity.Training => TrainingScreen.Spotlights(target),
            Activity.Gear => GearScreen.Spotlights(target),
            Activity.Build => _loadoutScreen.Spotlights(target),
            Activity.Mastery => MasteryScreen.Spotlights(target),
            Activity.Vault => VaultScreen.Spotlights(target),
            Activity.Forge => ForgeScreen.Spotlights(target),
            Activity.Warren => _warrenScreen.Spotlights(target),
            Activity.Map => _mapScreen.Spotlights(target),
            Activity.Traits => TraitCollectionScreen.Spotlights(target),
            Activity.Roster => RosterScreen.Spotlights(target),
            _ => Array.Empty<Rectangle>(),
        };
        if (own.Length == 0) return new[] { new Rectangle(0, 0, 1920, 1080) };
        if (screen == Activity.Hunt) return own;
        // NEVER A HOLE PAST THE CANVAS: every lit rect is cut to the page, and one that has nothing
        // left worth lighting is dropped rather than framing the panel's empty band and the stage
        // floor (chrome-01 — the BUILD tour's KEYSTONES step at 150 %, with the block scrolled away).
        var canvas = new Rectangle(0, 0, 1920, 1080);
        var lit = own.Select(r =>
        {
            var c = OverlayToCanvas(r, Vector2.Zero);
            c.Inflate(SpotlightHalo, SpotlightHalo);   // for the eye only — a forced click drops it (ClickableOf)
            return Rectangle.Intersect(c, canvas);
        }).Where(c => c.Width > 0 && c.Height >= UiMetrics.Control(60)).ToArray();
        return lit.Length > 0 ? lit : new[] { Rectangle.Intersect(OverlayToCanvas(own[0], Vector2.Zero), canvas) };
    }

    /// <summary>
    /// A guided lesson's marker on the REAL control it is asking about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same corner brackets a tour draws, on the same semantic rectangle, resolved by the same
    /// screen — so the thing that is lit is the thing that takes the click, at every UI density, and
    /// no coordinate is written down anywhere in this file. A screen that reflows at 150 % moves its
    /// own rectangle and the light moves with it.
    /// </para>
    /// <para>
    /// <b>And it does not scrim.</b> A HardGuide is not a modal: the fight goes on, the rail works, the
    /// player can walk away and come back, and Settings — which is where SKIP GUIDANCE lives — is never
    /// unreachable. What the guide does is point at a real control and wait for a real deed; what it
    /// must never do is build a room the player cannot leave.
    /// </para>
    /// </remarks>
    private void DrawCoachSpotlight()
    {
        // THE CARD'S RECT IS CLEARED ON EVERY FRAME THE CARD IS NOT PAINTED, so the × Update hit-tests is
        // always the one that was drawn — never last frame's, on a frame the light was withheld.
        if (_coach.Showing is not { } id || !CoachLightsIt(id)) { _coachCard = Rectangle.Empty; return; }
        var holes = CoachHoles(id);
        if (holes.Length == 0) { _coachCard = Rectangle.Empty; return; }   // nothing to light: the card path says it instead
        // ONE SURFACE PER SLOT — A QUEUE, NOT A STACK. On the fight the notice toast and a card beside the
        // run-log button share one band, and the light outranks the news: a toast waits for a lit lesson
        // (DrawNoticeToast asks the owner, and the next one is not dequeued under it) rather than being
        // drawn over the thing the player is being shown.

        // ── DARKEN EVERYTHING, LIGHT THE ONE THING. ─────────────────────────────────────────────
        //
        // Playtest 2026-09-10: "there are places you mark with a yellow box but they get lost in the
        // picture — the method where we darken the screen and light only the region we want is more
        // effective." It is the tour's own method, and it was withheld here because a lesson can wait
        // indefinitely and a permanently dark screen is intolerable in a game you leave running. So it
        // BLAZES rather than holds: full scrim on arrival, fading out after CoachBlazeSeconds and
        // leaving the brackets and the card. Re-armed whenever the lesson changes or the player walks
        // onto a different screen — which is exactly when there is something new to look at.
        //
        // THE AUTHORED OPENING DOES THE OPPOSITE, and the difference is not a disagreement. A beat of
        // the opening is WAITING FOR ONE PRESS and will get it in seconds, so its scrim holds until it
        // does; a coaching lesson is waiting for a deed the player may not perform for an hour, and a
        // scrim that held for that would be a broken game. Same method, two clocks. (The two never run
        // at once: the coach is silent for the whole of the opening.)
        var blaze = Math.Clamp(_coachBlaze / CoachBlazeFade, 0f, 1f);
        if (blaze > 0f)
            DrawScrimAround(holes, CoachScrim * (UiMotion.Reduced ? 1f : UiMotion.Smooth(blaze)));

        // The brackets stay, and against the scrim they finally read. A viewfinder, never an outline:
        // an outline is what this game puts round a SELECTED control.
        foreach (var hole in holes) TourBrackets(hole);

        DrawCoachCard(id, holes);
    }

    /// <summary>How long the scrim holds at full before it fades away, in seconds.</summary>
    private const float CoachBlazeSeconds = 6f;

    /// <summary>The fade's own length — the tail of the blaze, eased out.</summary>
    private const float CoachBlazeFade = 0.8f;

    /// <summary>The coach's scrim. Lighter than the tour's, because nothing here is modal.</summary>
    private static readonly Color CoachScrim = new Color(0x05, 0x03, 0x0A) * 0.66f;

    /// <summary>Seconds of scrim left on the lesson being lit.</summary>
    private float _coachBlaze;

    /// <summary>What the blaze is armed for — the lesson, and the screen it was armed on.</summary>
    private OnboardingLessonId? _blazeFor;
    private Activity _blazeOn = Activity.Hunt;

    /// <summary>Where the coach's copy card is this frame, for the click that closes it.</summary>
    private Rectangle _coachCard = Rectangle.Empty;

    /// <summary>
    /// Re-arm the blaze when there is something new to look at. Called once a frame.
    /// </summary>
    /// <remarks>
    /// Two things count as new: a different lesson, and the same lesson seen from a different screen.
    /// Walking onto TRAINING while TRAIN ANY STAT ONCE is up moves the light off a rail tile and onto
    /// the rows themselves, which is worth blazing for a second time.
    /// </remarks>
    private void TickCoachBlaze(float dt)
    {
        var here = ScreenActivity();
        if (_coach.Showing != _blazeFor || here != _blazeOn)
        {
            _blazeFor = _coach.Showing;
            _blazeOn = here;
            _coachBlaze = _coach.Showing is null ? 0f : CoachBlazeSeconds;
            return;
        }
        // ...AND IT BURNS ONLY WHILE THE LIGHT IS PAINTED. Under an owner the spotlight is withheld and the
        // blaze holds with it, so the scrim is at full when the light is first seen, not half gone.
        if (_coachBlaze > 0f && CoachLit) _coachBlaze = MathF.Max(0f, _coachBlaze - dt);
    }

    /// <summary>Is the coach lighting something this frame — the spotlight, its brackets and its card?</summary>
    private bool CoachLit => _coach.Showing is { } lit && CoachLightsIt(lit);

    // ── WHO HAS THE PLAYER'S EYES ────────────────────────────────────────────────────────────────
    //
    // One owner per frame, decided in Update once the frame's flags have settled (the assignment beside
    // the fight's hold) and read by every gate that paints something the player is meant to read and by
    // every clock under it. A gate names its own tier and asks one question; the ranking lives in
    // AttentionOwner and nowhere else, so no surface keeps a private list of what outranks it.

    /// <summary>The surface that owns the player's attention this frame. Assigned in exactly one place.</summary>
    private AttentionOwner _attention;

    /// <summary>Was the death transition on the page last frame? Its falling edge is the fall's end, as the player saw it.</summary>
    private bool _deathWasUp;

    /// <summary>Is anything ABOVE this tier up? The one question every reader asks.</summary>
    private bool AttentionOwnedAbove(AttentionOwner tier) => _attention > tier;

    /// <summary>
    /// A production surface that owns the frame: the title, the two host panels, the type spec, a tour,
    /// and the screens' own confirmations — the attunement, the vault's stall and cards, the Forge's
    /// SELL / SALVAGE question. The welcome is not here: it is a reward to the director (the camp's
    /// lesson may speak over it), and the owner adds it to this tier itself.
    /// </summary>
    private bool ProductionModalUp
        => _showTitle || HostModalUp || _showTypeSpec || _tourActive
           || (_showMastery && _masteryScreen.SpecialisationOpen)
           || (_showVault && _vault.ModalUp)
           || (_showForge && _forge.ConfirmOpen);

    /// <summary>Is the boot toast on screen? Feedback tier: it waits under anything the player is meant to be reading, and its clock waits with it (Update).</summary>
    private bool BootToastShowing => _bootTimer > 0f && _bootMessage.Length > 0 && !AttentionOwnedAbove(AttentionOwner.Feedback);

    /// <summary>
    /// Is this lesson's copy carried by the SPOTLIGHT rather than by a card in the toast slot?
    /// </summary>
    /// <remarks>
    /// The coach's tier: it lights only when nothing above it — the log, a fall on the page, a reveal, a
    /// modal, the opening — has the frame, and <see cref="CoachAims"/> says whether there is anything
    /// to light. Deliberately geometry-free. <see cref="SlotShowing"/> and <see cref="HuntLessonShowing"/>
    /// ask this to decide whether to draw the lesson themselves, and they are called from the page's own
    /// lane arithmetic — resolving a rectangle here would make the lane depend on a rectangle that
    /// depends on the lane. It asks only what the catalogue says and which screen is on top.
    /// </remarks>
    private bool CoachLightsIt(OnboardingLessonId id) => !AttentionOwnedAbove(AttentionOwner.Coach) && CoachAims(id);

    /// <summary>
    /// Would the coach carry this lesson by a spotlight, given the frame — geometry-free, so the lane
    /// arithmetic may ask. (The owner's own coach term asks the geometry, <see cref="CoachHoles"/>.)
    /// </summary>
    private bool CoachAims(OnboardingLessonId id)
    {
        if (OnboardingLessons.Sends(id) is { } sends && sends != ScreenActivity())
            return NavSlots().Contains(Array.IndexOf(NavActivity, sends));   // the tile must be on the rail
        return OnboardingLessons.Target(id) is not null;
    }

    /// <summary>What the lesson lights on the screen the player is actually looking at.</summary>
    /// <remarks>
    /// A lesson about ANOTHER screen lights that screen's rail tile — the only part of it on this page,
    /// and the thing that has to be pressed to get there. A lesson about this one lights its own
    /// control. The whole-canvas answer means the screen does not know the target: better to light
    /// nothing than to darken the page and cut a hole the size of the page.
    /// </remarks>
    private Rectangle[] CoachHoles(OnboardingLessonId id)
    {
        if (OnboardingLessons.Sends(id) is { } sends && sends != ScreenActivity())
        {
            var slot = NavSlots().IndexOf(Array.IndexOf(NavActivity, sends));
            return slot >= 0 ? new[] { NavHexRect(slot) } : Array.Empty<Rectangle>();
        }
        if (OnboardingLessons.Target(id) is not { } target) return Array.Empty<Rectangle>();
        var own = TourSpotlights(ScreenActivity(), target);
        return own.Any(h => h.Width >= 1900 && h.Height >= 1060) ? Array.Empty<Rectangle>() : own;
    }

    /// <summary>
    /// The lesson's words, beside the light rather than in the slot nobody reads.
    /// </summary>
    /// <remarks>
    /// The tour's plate and the tour's placement (<see cref="TourCardRect"/> — below the hole, then
    /// right, then left, then above), because a card under the thing it names reads as a label. It
    /// carries the same close button the slot card did, and closing it means the same thing: this one
    /// lesson goes quiet for the session, and no fact is touched.
    /// </remarks>
    private void DrawCoachCard(OnboardingLessonId id, Rectangle[] holes)
    {
        var pad = UiMetrics.Space(20);
        var width = Math.Min(UiMetrics.Control(TourCardWidth), UiKit.Page.Width / 2);
        var body = _ui.WrapBig(LessonLine(id), width - pad * 2 - CardCloseLane, UiTypography.Body);
        var height = pad + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4)
                     + Math.Max(1, body.Count) * UiTypography.Pitch(UiTypography.Body) + pad;

        // Off the chrome row it is not pointing at, and off the fight's skill dock.
        var avoid = new List<Rectangle> { new(NavRailWidth, 0, 1920 - NavRailWidth, ChromeRowBottom + UiMetrics.Space(8)) };
        if (ScreenActivity() == Activity.Hunt) avoid.Add(HuntScreen.DockRect);
        _coachCard = TourCardRect(holes, avoid, width, height);

        _ui.Fill(_batch, _coachCard, SlotGround);
        _ui.Plate(_batch, _coachCard, UiInk.Accent);
        _ui.TextBig(_batch, OnboardingLessons.Title(id), _coachCard.X + pad, _coachCard.Y + pad,
                    UiInk.Accent, UiTypography.Headline);
        var y = _coachCard.Y + pad + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(4);
        foreach (var line in body)
        {
            _ui.TextBig(_batch, line, _coachCard.X + pad, y, UiInk.Primary, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }
        _ui.CloseButton(_batch, HintCloseRect(_coachCard), ChromeMouse, false);
    }

    /// <summary>Darken the whole canvas except the given holes.</summary>
    /// <remarks>
    /// Cut into horizontal bands at every hole edge; inside each band, fill the x-runs no hole covers.
    /// One hole gives the four rectangles around it; two holes give a few more. Nothing is drawn twice,
    /// so the scrim's alpha is uniform — a second layer over a corner would read as a darker patch.
    /// </remarks>
    private void DrawScrimAround(IReadOnlyList<Rectangle> holes, Color scrim)
    {
        var edges = new SortedSet<int> { 0, 1080 };
        foreach (var h in holes)
        {
            edges.Add(Math.Clamp(h.Top, 0, 1080));
            edges.Add(Math.Clamp(h.Bottom, 0, 1080));
        }
        var ys = edges.ToList();
        for (var i = 0; i + 1 < ys.Count; i++)
        {
            int y0 = ys[i], y1 = ys[i + 1];
            var x = 0;
            foreach (var h in holes.Where(h => h.Top <= y0 && h.Bottom >= y1).OrderBy(h => h.Left))
            {
                if (h.Left > x) _ui.Fill(_batch, new Rectangle(x, y0, h.Left - x, y1 - y0), scrim);
                x = Math.Max(x, h.Right);
            }
            if (x < 1920) _ui.Fill(_batch, new Rectangle(x, y0, 1920 - x, y1 - y0), scrim);
        }
    }

    /// <summary>A thin frame just outside a rectangle.</summary>
    private void TourOutline(Rectangle r, int t, Color c)
    {
        _ui.Fill(_batch, new Rectangle(r.X - t, r.Y - t, r.Width + 2 * t, t), c);
        _ui.Fill(_batch, new Rectangle(r.X - t, r.Bottom, r.Width + 2 * t, t), c);
        _ui.Fill(_batch, new Rectangle(r.X - t, r.Y, t, r.Height), c);
        _ui.Fill(_batch, new Rectangle(r.Right, r.Y, t, r.Height), c);
    }

    /// <summary>
    /// Where the caption card goes: beside the spotlight, on the first side it fits without covering it.
    /// </summary>
    /// <remarks>
    /// Below, then right, then left, then above. Below first because a card under the thing it names
    /// reads as a label; the others are for holes at an edge. A candidate that would leave the canvas
    /// vertically is rejected outright; one that runs off the sides is pulled in (clear of the nav rail)
    /// and then checked against every hole, so the card can never sit on the thing it is pointing at.
    /// </remarks>
    private static Rectangle TourCardRect(IReadOnlyList<Rectangle> holes, IReadOnlyList<Rectangle> avoid, int width, int height)
    {
        var gap = UiMetrics.Space(28);
        var edge = UiMetrics.Space(8);
        var a = holes[0];
        var candidates = new[]
        {
            new Point(a.Center.X - width / 2, a.Bottom + gap),
            new Point(a.Right + gap, a.Center.Y - height / 2),
            new Point(a.Left - gap - width, a.Center.Y - height / 2),
            new Point(a.Center.X - width / 2, a.Top - gap - height),
        };
        var right = UiKit.PageRight(UiMetrics.Space(20));
        var bottom = UiKit.PageBottom(edge);
        foreach (var p in candidates)
        {
            if (p.Y < edge || p.Y + height > bottom) continue;
            var r = new Rectangle(Math.Clamp(p.X, NavRailWidth + UiMetrics.Space(20), right - width), p.Y, width, height);
            if (holes.Any(h => h.Intersects(r))) continue;
            if (avoid.Any(h => h.Intersects(r))) continue;
            return r;
        }
        return new Rectangle(right - width, bottom - height, width, height);
    }

    /// <summary>A tour card's width at 100 % — a comfortable reading line; wider with the profile, never past half the page.</summary>
    private const int TourCardWidth = 560;

    /// <summary>
    /// The click-through tour: the screen keeps working underneath, one region at a time is lit, and a
    /// card beside it says what that region is. A click or any key advances; Escape skips the rest.
    /// </summary>
    /// <remarks>
    /// The only full-screen modal left in the game, and it runs when the player asks for it — LEARN
    /// THIS SCREEN, as many times as they like — never on its own. Drawn last of the chrome so the
    /// scrim covers the rail too: the Hunt's tour has a card about the rail, and the BUILD tour
    /// points at the MASTERY tile on it.
    /// </remarks>
    private void DrawTour()
    {
        if (!_tourActive || _tour.Count == 0) return;

        var stepNo = Math.Clamp(_tourStep, 0, _tour.Count - 1);
        var step = _tour[stepNo];
        var holes = TourSpotlights(_tourScreen, step.Target);

        // The last card points at where lessons appear — so a lesson appears there. It is a SAMPLE
        // of the shape, not the live card: the tour can be asked for at any depth now, so the first
        // rung is rarely what the coach is saying at the time. (The live card is suppressed under a
        // tour, so without this the light would fall on an empty slot.)
        if (step.Target == TourTarget.LessonSlot) DrawLessonCard(OnboardingLessonId.FirstFight, 1f);

        DrawScrimAround(holes, new Color(0x05, 0x03, 0x0A) * 0.74f);
        // THE RING IS A POINTER, NOT A BUTTON. It used to be a solid gold outline — the same shape
        // this game puts round a SELECTED control — so the intro spent forty cards ringing things that
        // could not be pressed. Drawn as corner brackets instead: a viewfinder marks what is being
        // talked about and has never in any interface meant "press me".
        foreach (var h in holes) TourBrackets(h);

        // The card's height is its lines: a title line, the wrapped body at the paragraph pitch, a
        // footer line — so a bigger profile makes a taller card, never a body that leaves its plate.
        var width = Math.Min(UiMetrics.Control(TourCardWidth), UiKit.Page.Width / 2);
        var pad = UiMetrics.Space(24);
        var lines = _ui.WrapBig(step.Body, width - pad * 2, UiTypography.Body);
        var bodyTop = pad + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
        var footerH = UiMetrics.Space(14) + UiTypography.Pitch(UiTypography.Secondary);
        var height = bodyTop + lines.Count * UiTypography.Pitch(UiTypography.Body) + footerH + pad;
        // The card keeps off the HUD it is not pointing at: on the HUNT the skill dock and, on every
        // screen, the currency row (chrome-04 — the intro's first card sat on HARD HANDS at 100 %).
        var avoid = new List<Rectangle> { new(NavRailWidth, 0, 1920 - NavRailWidth, PillRowTop + PillHeight + UiMetrics.Space(8)) };
        if (_tourScreen == Activity.Hunt) avoid.Add(HuntScreen.DockRect);
        var card = TourCardRect(holes, avoid, width, height);

        // THE HOUSE TEACHING PLATE — the hint slot's own: a flat ground, the plate with the accent rule,
        // the title in the accent, the body in Primary, the counter and footer in Secondary. It was a
        // hand-drawn purple box with a gold bar, a teal line and lavender text (chrome-03).
        _ui.Fill(_batch, card, SlotGround);
        _ui.Plate(_batch, card, UiInk.Accent);

        _ui.TextBig(_batch, step.Title, card.X + pad, card.Y + pad, UiInk.Accent, UiTypography.Headline);
        _ui.TextRightBig(_batch, $"{stepNo + 1} / {_tour.Count}", card.Right - pad,
                         card.Y + pad + (UiTypography.Headline - UiTypography.Secondary) / 2, UiInk.Secondary, UiTypography.Secondary);

        var y = card.Y + bodyTop;
        foreach (var line in lines)
        {
            _ui.TextBig(_batch, line, card.X + pad, y, UiInk.Primary, UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        // ── A REAL CONTROL, WHERE THE INSTRUCTION USED TO BE. ────────────────────────────────────
        //
        // The footer said CLICK TO CONTINUE and the whole screen obeyed it, which is how a ringed
        // nav tile came to advance the tour instead of opening the screen it was ringing. The verb
        // now belongs to a button that is visibly the only thing lit, and ESC still skips.
        var last = stepNo + 1 >= _tour.Count;
        var verb = !last ? "NEXT" : _tourScreen == Activity.Hunt ? "BEGIN THE HUNT" : "DONE";
        var btnH = UiMetrics.Control(44);
        var btnW = Math.Min(card.Width - pad * 2, UiMetrics.Control(240));
        var btn = new Rectangle(card.Right - pad - btnW, card.Bottom - pad - btnH + UiMetrics.Space(6), btnW, btnH);
        // Drawn only — the press is spent in the input pass, which owns the whole frame while a tour
        // is up. A Button that took the click here would fire on a frame the tour had already spent.
        _ui.Button(_batch, btn, verb, ChromeMouse, false, true, ButtonStyle.Primary);
        _ui.TextBig(_batch, "ESC SKIPS", card.X + pad,
                    btn.Y + (btnH - UiTypography.Pitch(UiTypography.Secondary)) / 2 + UiMetrics.Space(2),
                    UiInk.Secondary, UiTypography.Secondary);
        _tourCard = card;
    }

    /// <summary>
    /// The tour's spotlight marker: four corner brackets, not a closed ring.
    /// </summary>
    /// <remarks>
    /// A closed gold outline is this game's SELECTED state — the mastery tree, the bag, the roster all
    /// use it — so ringing a control during a tour said "this is chosen, press it" about something the
    /// tour had frozen. Brackets are a viewfinder: they mark, they do not offer.
    /// </remarks>
    private void TourBrackets(Rectangle r)
    {
        var t = Math.Max(2, UiMetrics.Control(3));
        var len = Math.Clamp(Math.Min(r.Width, r.Height) / 4, UiMetrics.Control(10), UiMetrics.Control(28));
        var g = UiMetrics.Space(3);
        var x0 = r.X - g; var y0 = r.Y - g; var x1 = r.Right + g; var y1 = r.Bottom + g;
        void Corner(int cx, int cy, int dx, int dy)
        {
            _ui.Fill(_batch, new Rectangle(Math.Min(cx, cx + dx * len), cy - (dy < 0 ? t : 0), len, t), NavGold);
            _ui.Fill(_batch, new Rectangle(cx - (dx < 0 ? t : 0), Math.Min(cy, cy + dy * len), t, len), NavGold);
        }
        Corner(x0, y0, 1, 1);
        Corner(x1, y0, -1, 1);
        Corner(x0, y1, 1, -1);
        Corner(x1, y1, -1, -1);
    }

    /// <summary>Where a region sits on the world chain. The curve itself lives in Core/RegionLadder.</summary>
    private static int LadderIndex(string regionId)
    {
        for (var i = 0; i < Regions.All.Count; i++)
            if (Regions.All[i].Id == regionId) return i;
        return 0;
    }

    /// <summary>
    /// The idle heart: the champion auto-clears the region and pays out per wave; the player never clicks
    /// a fight.
    /// </summary>
    /// <remarks>
    /// Bank-or-push is gone (playtest: "I don't want to click after every fight"). The champion fights on
    /// a timer inside <see cref="HuntScreen"/> and hands each cleared wave to us as a reward we
    /// credit the instant it lands: gleam and cores every wave, ITEMS on boss waves only (so the Forge
    /// isn't flooded and depth is what earns loot). Conquest is measured by the deepest wave reached.
    /// </remarks>
    /// <remarks>
    /// THE `interactive` PARAMETER IS GONE. It carried "is the combat screen the one on top", threaded
    /// down to HuntScreen.Update — which read none of its four input arguments, because click
    /// handling had migrated into Draw and the Update-side plumbing was left standing. A gate that gates
    /// nothing is worse than no gate: the next person to need one would have found this and believed it
    /// was already handled. The fight screen's clicks are gated where they are actually read, in Draw.
    /// </remarks>
    /// <summary>
    /// THE ONE SEAM THE TRAITS REACH THE GAME THROUGH: compose what the worn three contribute, keep
    /// the account facts current, and hand the reveals to the toast queue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called once a frame from <see cref="UpdateExpedition"/>, which runs on every screen — the same
    /// place both trees derive their points. Recomputed rather than cached because four of the
    /// twenty-six traits read the WORLD (the region being walked, the set being worn, the regions
    /// mastered, the last three descents) and every one of those can change without the Traits screen
    /// ever being opened. Composing three small records a frame is the same order of work
    /// <c>ToBuild</c> already does on this line.
    /// </para>
    /// <para>
    /// <b>The first pass is different, once.</b> Six traits read facts an established save already
    /// carries, so they awaken the moment this build first loads. §32 asks for a meaningful awakening
    /// and warns against a ceremony every few minutes; six in one second is the failure mode of both,
    /// so the first pass posts ONE combined plate and every pass after it reveals one at a time.
    /// </para>
    /// </remarks>
    private void RefreshTraits(RegionDefinition region)
    {
        // WHAT THE ACCOUNT ALREADY KEEPS. Six rules read these and nothing copies them into the
        // ledger — they have one home apiece in the save, and a second copy is a second truth.
        var mastered = 0;
        foreach (var r in Regions.All)
            if (_world.RegionFarm(r.Id).MasteryLevel >= MasteryLevel.FullyMastered) mastered++;

        var (streak, wall) = TraitDiscovery.WallStreakOf(_expedition.Log.Entries);

        _traitWatch.Account = new TraitAccount(
            BossesFelled: _bossesFelled,
            RunsWithVowKept: _runsWithVowKept,
            SetsCompleted: _gear?.CompletedSets.Count ?? 0,
            RegionsConquered: _world.ConqueredIds.Count,
            RegionsMastered: mastered,
            WallStreak: streak);
        _traitWatch.CharacterId = _characters.ActiveId;
        _traitWatch.RegionId = region.Id;

        // THE FIVE-PIECE SET BEING WORN RIGHT NOW, read live off the hunter rather than off the
        // "already celebrated" list: THE MATCHED SUIT pays for wearing the set, and the celebration
        // list only records that a set was once completed.
        Source? suit = null;
        foreach (var (element, count) in ElementSets.WornCounts(_hunter))
            if (count >= ElementSets.Rungs[^1]) { suit = element; break; }

        // WHAT THE FIGHT MUST NEVER LEARN, resolved here into plain dials.
        var context = new TraitContext(
            RegionConquered: _world.IsConquered(region.Id),
            RegionsMastered: mastered,
            SetElement: suit,
            LastWallArchetype: streak >= 3 ? wall : null);

        // THE ONE PLACE THE WORN THREE REACH THE BUILD. PlayerLoadout.TraitShape rides into every
        // ToBuild call there is, so the fight, the damage bench and every readout see one build.
        _loadout.TraitShape = TraitEffects.Compose(
            _traitLedger.LoadoutOf(_characters.ActiveId), context);

        // A LOAD IS A CHECK. Every rule is re-checked here as well as after a cleared wave, which is
        // what makes a threshold crossed while the player was not looking impossible to miss.
        //
        // THE DISCOVERY ALWAYS HAPPENS; ONLY THE REVEAL IS HELD BACK UNDER THE CAPTURE RIG. Every
        // fixture in the rig is an established save, so the six account-fed rules fire on frame one
        // and the plate landed across the middle of EVERY fight, build and gear capture in the
        // project. A pose is something a capture asks for (RH_SHOT_WAKE), not something that leaks
        // into other people's baselines.
        // RH_SHOT_WAKE poses the plate itself, so the rig never needs the organic one.
        var revealing = ShotMode is null;
        if (_traitsFirstCheckOwed)
        {
            _traitsFirstCheckOwed = false;
            _traitWatch.Recheck();
            var woke = revealing ? _traitWatch.TakeAwakened() : Array.Empty<string>();
            if (woke.Count == 1 && TraitCatalogue.Find(woke[0]) is { } only)
                PostAwakening("A TRAIT HAS AWAKENED", only.Name, only.Flavour.ToUpperInvariant());
            else if (woke.Count > 1)
                // ONE PLATE, NOT SIX. The names are in the collection the plate points at; saying six
                // of them here would be the achievement list §90 forbids, in a toast.
                PostAwakening($"{woke.Count} TRAITS HAVE AWAKENED",
                              "WHAT YOU HAVE LIVED THROUGH CHANGED YOU",
                              "READ THEM ON THE TRAITS SCREEN");
        }

        foreach (var id in _traitWatch.TakeAwakened())
            if (revealing && TraitCatalogue.Find(id) is { } def0)
                PostAwakening("A TRAIT HAS AWAKENED", def0.Name, def0.Flavour.ToUpperInvariant());
    }

    private void UpdateExpedition(GameTime gameTime)
    {
        var def = Regions.Get(_activeRegion);
        // The traits, before anything composes a build off the loadout this frame.
        RefreshTraits(def);
        _expedition.TraitWatch = _traitWatch;
        _expedition.EnemySource = def.Theme;          // the screen keys enemy art + name off the theme
        // The skills bank their levels in the game's own progress, not the screen's or the run's:
        // a run ends and a skill's levels do not.
        _expedition.Progress = _skillProgress;
        _expedition.RegionId = def.Id;                // region id → the boss creature (boss_<region>) on boss waves
        _expedition.EnemyBias = def.CombatBias;       // region character → the enemy's bite tempo (feel + TRAP synergy)
        _expedition.Tuning = TuningNow();             // → the wave model; taught waves until the first boss falls
        _expedition.CorruptionTier = _world.CorruptionTier;   // → creature tint, the boss's epithet, the header line
        _expedition.ShowDamageNumbers = _showDamageNumbers;   // the settings' quality-of-life switches
        _expedition.ShowSkillCallouts = _showSkillCallouts;
        _expedition.ShowHitEffects = _showHitEffects;
        _expedition.ShowScreenFlash = _showScreenFlash;
        _expedition.Loadout = _loadout;               // the player's build, handed over live…
        // …and by what the WORLD has taught: the fight must socket exactly what the workbench
        // shows, so both read the same two lists.
        _expedition.DiscoveredKeystones = _keystoneMenu;
        _expedition.KnownVows = _vowMenu;
        _expedition.Mastery = _mastery;               // …and the mastery tree (affinity + node bonuses)
        _expedition.BestDepthHere = _world.RegionFarm(def.Id).BestDepth;   // so NEW RECORD means it
        // THE CHECKPOINT, if it is still valid and the Dust is there. Validated every frame rather
        // than at the click, because the record, the conquest and the Dust all move without the map.
        if (_expedition.CheckpointCharge > 0)
        {
            // The descent already began at the checkpoint; the Dust it cost comes out now — BEFORE the
            // next affordability read below, so the next start is judged against the Dust that is
            // actually left (review 2026-08-26). Spend() refusing (a race with the Warren tick, one
            // frame wide) simply makes that descent free.
            _dust.Spend(_expedition.CheckpointCharge);
            _expedition.CheckpointCharge = 0;
        }
        var farmHere = _world.RegionFarm(def.Id);
        var wish = Checkpoints.Clamp(farmHere.StartWave, farmHere.BestDepth, _world.IsConquered(def.Id));
        _expedition.StartWave = _dust.MemoryDust >= Checkpoints.DustCost(wish) ? wish : 0;
        // AND THE FREE RESUME, for ONE descent. The absence's own simulation left the champion on a
        // wave; that depth was earned in real elapsed time and already paid out at the camp's rate, so
        // opening there costs no Dust. It is spent the moment a SECOND descent begins — the first fall
        // drops back to the paid checkpoint, which is where the Dust economy's revenue actually lives
        // (HuntScreen re-charges DustCost on every StartRun, including the one after a death).
        if (_offlineResumeWave > 0 && _expedition.RunsStarted > _offlineResumeRuns) _offlineResumeWave = 0;
        _expedition.FreeStartWave = _expedition.RegionId == _offlineResumeRegion ? _offlineResumeWave : 0;
        _expedition.RegionConquered = _world.IsConquered(def.Id);
        _expedition.ChestCount = _forge.UnopenedChests.Count;   // drives the fight screen's "go open a chest" nudge
        _expedition.VaultOpen = Unlocks.IsOpen(Activity.Vault, GuideUnlockFacts());       // the rail hides a reward whose screen is locked
        _expedition.MasteryOpen = Unlocks.IsOpen(Activity.Mastery, GuideUnlockFacts());   // SPEND POINTS only shows once the tree is open
        _expedition.IdleGleamRate = _champGleamRate;            // gleam/sec the champion earns idle → HUNT idle panel
        // THE FIGHT RAIL'S REWARD BUTTONS NAVIGATE, and the host is what navigates. The screen only
        // records that they were pressed — a fight screen that opened chests itself would be a second
        // Forge, and the errand belongs to the screen that owns the verb.
        // Indices, not names — they moved when MASTERY was inserted at 4. VAULT is 5 now.
        // The empty vault's door out. Same shape as the HUNT's: the screen records the intent in Draw,
        // the host reads it one frame later — only the host may change screens.
        if (_vault.WantsHunt) { _vault.WantsHunt = false; OpenNav(0); }
        // TAKE ONLY edits (made in the HUNT screen's Draw) come back on the dirty flag only — never a
        // per-frame push of the saved value, which clobbered the vault's edit in playtest five.
        if (_vault.FilterDirty)
        {
            _vault.FilterDirty = false;
            _chestKeepMinTier = _vault.KeepMinTier;
            _autoSellOn = _vault.AutoSellOn;
            _chestKeepSlots.Clear(); foreach (var sl in _vault.KeepSlots) _chestKeepSlots.Add(sl);
            Save();
        }
        if (_expedition.WantsMastery) { _expedition.WantsMastery = false; OpenNav(4); }   // MASTERY (E) — the tree where the points are spent
        // The EXPEDITION LOG's doors (UX V2 P1.2): the screen closed its own log before raising these.
        if (_expedition.WantsBuild) { _expedition.WantsBuild = false; OpenNav(3); }   // BUILD
        if (_expedition.WantsGear) { _expedition.WantsGear = false; OpenNav(1); }     // GEAR

        // The first-run guide, or null once outgrown. Held on the HOST, not on the fight screen: it is
        // drawn as chrome by the host now (the HUNT's lesson card, a menu screen's hint slot, the rail's NEW mark), because a guide that vanishes the
        // moment you obey it reads as a guide that has stopped working.
        // ── THE COACH RUNS ON THE CLOCK, AND NOTHING WAITS FOR IT. ──────────────────────────────
        //
        // One lesson chosen per frame from live facts, and the simulation neither knows nor cares: no
        // wave is delayed, no animation is awaited, no gate is moved. Deleting this line would make the
        // game silent and leave every other behaviour identical, which is the test of the split.
        // RH_SHOT_LESSON=<OnboardingLessonId> poses one lesson for the shutter — see the note on
        // OnboardingDirector.Forced. Read every frame so the fixture's own dressing cannot clear it.
        if (CaptureRig && Environment.GetEnvironmentVariable("RH_SHOT_LESSON") is { Length: > 0 } posed
            && Enum.TryParse<OnboardingLessonId>(posed, true, out var poseId))
        {
            _coach.Forced = poseId;
            // ...AND THE SLOT IS CLEARED FOR IT. The lesson card yields to the boot toast and to a
            // notice — one surface per slot, a queue rather than a stack — and every fight fixture
            // opens with a welcome toast in exactly that slot. A pose that photographed the toast
            // instead of the lesson would prove nothing about the lesson.
            _bootTimer = 0f;
            _noticeTimer = 0f;
        }

        // ...AND THE SAME FOR THE BOSS ANNOUNCEMENT, for the same reason and a different slot: the
        // hunt draws exactly one major overlay, and the host suppresses them all while a boot toast is
        // up (see the Draw call's suppressBanner argument). Every fight fixture opens with one, so a
        // posed BOSS INCOMING would be suppressed by a toast rather than photographed.
        if (CaptureRig && _expedition.DevBossCall) { _bootTimer = 0f; _noticeTimer = 0f; }

        // RH_SHOT_LOCKED=<Activity> poses the LOCKED-TILE REFUSAL — the toast a player gets for
        // pressing a rail tile that is not open yet. It is three seconds long and lives behind a click
        // no capture can make, so it had never been photographed; the chrome fix that moved the log
        // medallion moved this toast with it, and a band nobody has looked at is a band nobody has
        // checked. Written every frame, so the fixture's own dressing cannot clear it.
        // RH_SHOT_BREAK=<Activity>[:<0..1>] holds the NAV CHAIN'S BREAK part-played. It is nine tenths
        // of a second on a frame that happens once per screen per career, so it had no way of being
        // looked at; re-posed every frame here, so the fixture's own dressing cannot outrun it.
        if (CaptureRig && Environment.GetEnvironmentVariable("RH_SHOT_BREAK") is { Length: > 0 } breakSpec)
        {
            var bits = breakSpec.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var who = Enum.TryParse<Activity>(bits[0], true, out var ba)
                ? ba
                : throw new InvalidOperationException(
                    $"RH_SHOT_BREAK='{bits[0]}' is not an Activity. Known: " + string.Join(", ", Enum.GetNames<Activity>()) + ".");
            var part = bits.Length > 1 && float.TryParse(bits[1], System.Globalization.NumberStyles.Float,
                                                         System.Globalization.CultureInfo.InvariantCulture, out var bp) ? bp : 0.5f;
            UiMotion.PoseFlash(NavBreakKey(who), part, NavChainSeconds);
        }

        if (CaptureRig && Environment.GetEnvironmentVariable("RH_SHOT_LOCKED") is { Length: > 0 } lockedName)
        {
            var locked = Enum.TryParse<Activity>(lockedName, true, out var la)
                ? la
                : throw new InvalidOperationException(
                    $"RH_SHOT_LOCKED='{lockedName}' is not an Activity. Known: " + string.Join(", ", Enum.GetNames<Activity>()) + ".");
            _lockedMsg = $"{Unlocks.Headline(locked)} IS NOT OPEN YET — {Unlocks.Requirement(locked).ToUpperInvariant()}.";
            _lockedTimer = 3.2f;
        }

        // RH_SHOT_NOTICE=1 posts ONE sample notice on any fixture, once, so what news does under an owner
        // can be photographed: queued under a chest reveal it must not paint and its clock must not burn,
        // and it lands when the reveal is dismissed (a late RH_SHOT_T on `lootforge` poses that frame).
        if (CaptureRig && !_rigNoticePosted && Environment.GetEnvironmentVariable("RH_SHOT_NOTICE") is { Length: > 0 })
        {
            _rigNoticePosted = true;
            PostNotice("QUEST COMPLETE — FIRST STEPS", "You can equip a second skill.");
        }

        WatchTheFallLoop();
        RaiseSignatureBeat();
        TickCoachBlaze((float)gameTime.ElapsedGameTime.TotalSeconds);
        _coach.Update((float)gameTime.ElapsedGameTime.TotalSeconds, LessonFactsNow(),
                      new OnboardingDirector.Busy(
                          RewardUp: _forge.RevealActive || WelcomeUp,
                          // THE OWNER'S MODAL TIER, less the welcome on its own: the welcome is a reward
                          // to the director (the camp's lesson is raised by it and may speak over it).
                          // The authored opening outranks it entirely — only one guided sequence owns
                          // the player at a time, and while the first minutes are written the
                          // contextual catalogue has nothing to add that is not either already being
                          // said or being said too early.
                          ModalUp: _attention >= AttentionOwner.Modal && !(WelcomeUp && !ProductionModalUp),
                          ReportUp: _expedition.LogOpen,
                          // The death on the page, as the owner read it this frame: the transition ticks
                          // further down, so this is last frame's — and the frame it ends on is hushed.
                          DeathUp: _attention == AttentionOwner.Death));

        _forge.Tuning = ForgeTuning.Default with
        {
            // BASE RATE, FOR EVERYONE. The retired tree's `efficient_forge` multiplied this by 1.15 for
            // whoever had bought it — a per-account economy multiplier nobody else could earn. If salvage
            // reads low it is tuned here, in the base, rather than reintroduced as hidden legacy power.
            DismantleReturnRate = ForgeTuning.Default.DismantleReturnRate,
        };
        // A chest's rolled loot honours the same Dust filters a boss drop did — auto-sell floor and the
        // tireless-forge auto-merge — now applied at OPEN, since that is where a chest's items land.
        // AUTO-SELL IS THE WARREN'S JOB — SCAVENGER RUNS level 2 sells Commons, level 4 Uncommons. An
        // old save's filter nodes are converted to that facility's level once, at load, by
        // LegacyTraitTree, so a returning player keeps the automation without the tree existing.
        // THE FACILITY UNLOCKS IT; THE PLAYER TURNS IT ON. The level alone used to be the whole gate,
        // so an upgrade bought for its production quietly began selling gear out of every chest opened
        // from then on, with no switch anywhere and no line at the moment of sale.
        var sellFloor = WarrenAutomation.AutoSellAtOrBelow(_warren);
        _forge.AutoSellFloor = _autoSellOn ? sellFloor : null;
        _vault.AutoSellUnlocked = sellFloor is not null;
        _vault.AutoSellOn = _autoSellOn;

        // The loot-quality tilt reaches the roll that opens a chest. Until this line, Rarity was resolved
        // from keystones, gear and the trait tree, carried as Haul.Quality, and read by nothing at all.
        // The build is made ONCE and used twice — the Forge's combo line needs the same object.
        var wornBuild = ComposeBuild();
        _forge.RarityBonus = wornBuild.Resolve(_hunter).Rarity;
        TellForgeTheBuild(wornBuild);

        // The roster derives from conquest, every frame, exactly like both trees' points. A character
        // unlocked by a conquest the player made three regions ago should not depend on having been
        // logged in when it happened.
        // Quests, then characters — in that order, because a quest finishing is what makes a
        // quest-gated character available on the SAME frame rather than the next one.
        foreach (var done in QuestCatalogue.Satisfied(QuestSnapshot()))
            if (!_characters.QuestDone(done.Id))
            {
                _characters.CompleteQuest(done.Id);
                // WAS _bootMessage, WHICH IS A BOOT-ONLY CHANNEL. See the champion block below.
                PostNotice("QUEST COMPLETE", $"{done.Name} — {done.Demand}");
                Save();
            }

        // NEW CHAMPIONS. Playtest: "Karakterler hemen açılıyor, açılma geri bildirimi de gelmiyor,
        // haberim olmadı."
        //
        // THE FEEDBACK WAS BEING WRITTEN AND THROWN AWAY. This loop set _bootMessage — but that field is
        // half of a two-part mechanism, and the other half is _bootTimer, which gates the draw and is
        // armed in exactly one live place: Initialize(). A conquest happens minutes into a session, by
        // which time the timer has been zero for a long time, so DrawBootToast returned on its first
        // line. There was no sound, no banner, no panel and no badge for a champion anywhere in the
        // game. The player was right that nothing told them.
        //
        // It also had a second failure in the opposite direction. CharacterState._unlocked is in-memory
        // only — the save carries just the active id — so on the FIRST gameplay frame of every launch
        // Refresh re-reports every champion the player already owns, and the last one clobbered the
        // WELCOME BACK offline summary with a false "X JOINS YOU". _rosterBaselined absorbs that first
        // pass silently, the same way SeedExplained settles what a returning player already knows.
        foreach (var got in _characters.Refresh(_world.ConqueredIds))
        {
            if (!_rosterBaselined) continue;
            // A toast, and a NEW mark on the ROSTER tile that stays until the roster is opened. The
            // champion's power and the "nothing resets" reassurance live on the roster card itself,
            // which is where a player who follows the mark will read them.
            PostNotice($"{got.Name.ToUpperInvariant()} JOINS YOU", "SWITCH HUNTER ON THE ROSTER SCREEN");
            _rosterNews = true;
            _rosterNewName = got.Name;
        }
        _rosterBaselined = true;

        // A CHAMPION SWITCH SHEDS WHAT THE NEW ONE CANNOT WEAR — AND WHAT THEY CANNOT USE. The roster's
        // promise is that switching costs nothing, and it still costs nothing: the pieces go back to the
        // bag, not away, and an unwoven signature keeps every level it earned. But a WARDEN's helm on a
        // RANGER would be a class rule the fight quietly ignored, and the SEEKER's own skill left in a
        // slot after you became the ANVIL is a slot the composer refuses and the player cannot use.
        if (_lastActiveCharacterId is { } wasId && wasId != _characters.ActiveId) RepairForSwitch();
        _lastActiveCharacterId = _characters.ActiveId;
        // Four in five class-locked pieces a chest pays are the active champion's.
        _forge.FavouredClass = _characters.Active.Class;
        // ...and WHO is wearing it, so the reveal's EQUIP button can judge the drop against the slot.
        _forge.Wearer = _characters.Active;
        // THE ACCOUNT-WIDE LEAK IS GONE. This line used to latch the active champion's birth skill
        // into the permanent learned set, every frame, for ever: play THE QUIVER once and every
        // other champion could weave WEEP from then on. BRIEF sec.9 forbids exactly that, and it was
        // already in every existing save. A champion's own skill reaches the build through its
        // OWNER now, not by being banked account-wide the moment you meet them.

        _expedition.Character = _characters.Active;
        _gear.Character = _characters.Active;
        _masteryScreen.Character = _characters.Active;

        // WHAT THE WORLD HAS TAUGHT reaches the loadout — keystones, sockets, Vow capacity. Without
        // this the sockets are earned and never granted, which is the shape of the failure this
        // codebase keeps repeating.
        ApplyWorldGrants();
        ApplySkillCapacity();
        // (A slot the player just earned is not announced here, or anywhere: the BUILD tile derives its
        // NEW mark from the slot count and the explained list every frame — see Onboarding.IsNew.)
        // AND WHAT THE WORLD HAS TAUGHT, so an unpaired combo enchantment can name the region that
        // teaches its partner instead of naming a keystone the player has no route to.
        _forge.DiscoveredKeystones = _keystoneMenu;
        // AUTO-MERGE — HOARD VAULTS level 2, and only that. An old save's `auto_merge` node is converted
        // to that facility level once, at load, by LegacyTraitTree.
        _forge.AutoMergeOnOpen = WarrenAutomation.AutoMergeOnChestOpen(_warren);
        // Every region is a rung up the ladder for the champion, not just a new element — see
        // EnemyBaselineFor, which is also what the offline simulation fights against.
        var (ebh, ebd) = _shotEnemyBaseline ?? EnemyBaselineFor(_activeRegion);
        _expedition.Update(gameTime, _hunter, enemyBaseHealth: ebh, enemyBaseDamage: ebd);

        // Measure the champion's gleam/second over this session, for offline earnings later.
        //
        // ACCRUED IN SIMULATED SECONDS, NOT REAL ONES — divided by the playback speed, which is the whole
        // point. A wave is resolved instantly by the sim and then replayed at x1..x8, so the speed the
        // player is WATCHING at is also a multiplier on waves-per-real-second and therefore on every
        // Gleam a wave pays. Live, that is a fair fast-forward: they are sitting there.
        //
        // The offline rate is a different animal. It is measured here and then applied to hours during
        // which nobody is watching anything — so leaving the speed in it meant a player could click x8,
        // close the game, and come back with four times the offline earnings (the 50% offline haircut
        // against an 8x measurement). Free, silent, and available from the first minute. Normalising to
        // x1 here makes the stored rate mean "what this champion earns per second of fight", which is
        // the only reading that survives being applied to time the player was not present for.
        _champSecondsAccrued += gameTime.ElapsedGameTime.TotalSeconds
                                * Math.Max(0.01f, _expedition.SpeedMultiplier);

        // Credit every wave the champion cleared since last frame.
        // THE BITE, ONCE. _enemySinceHit counts UP from zero and is reset on the sim's own EnemyStrike,
        // so a fall in its value is the edge — read here, in Update, because a Draw that armed a pulse
        // would re-arm it every frame and the tile would sit red for as long as the player looked.
        var sinceHit = _expedition.SinceChampionHit;
        if (sinceHit < _navHuntSinceHit && _showScreenFlash && !UiMotion.Reduced)
            UiMotion.Flash(NavHuntHurtKey, UiMotion.Fast);
        _navHuntSinceHit = sinceHit;

        while (_expedition.HasReward)
        {
            var r = _expedition.TakeReward();
            // AND SO DOES THE OPENING. "WAVES PAY GLEAM" is a beat about being PAID FOR A WAVE, which
            // is a narrower fact than the purse going up — the Warren, offline time and a chest all do
            // that, and none of them is what the card is pointing at.
            _rewardsCredited++;
            // THE RAIL LEARNS EVERY WAVE THE FIGHT CLEARS. No new plumbing: this loop already runs on
            // every screen and already carries the wave and whether it was a boss — the HUNT tile just
            // had no way to know. A boss's tick lives longer and lands brighter.
            UiMotion.Flash(NavHuntClearKey, r.IsBoss ? UiMotion.Reward : UiMotion.Transition);
            _navHuntBoss = r.IsBoss;
            _hunter.AddGleam(r.Haul.Gleam);
            _champGleamAccrued += r.Haul.Gleam;
            // HAUL CORES ARE THE FORGE MATERIAL NOW. The hatchery currency they used to feed retired
            // 2026-08-24 with the creature subsystem, which left HARVEST ("a chance of a spare core") and
            // LODESTONE ("a spare core") paying into a channel nobody read — an enchant whose stat
            // was a lie. But both texts already say CORE, and the forge's re-roll material IS called
            // CORE — the words become true by pointing the payout there. Only those two produce into
            // this channel (the boss's flat 2-per-wave went with the hatchery), so a build without
            // them sees no change.
            if (r.Haul.Cores > 0)
            {
                _hunter.AddMaterial(Material.Core, r.Haul.Cores);
                _expedition.FlashSpoil(Material.Core);
            }
            // The tree's FASTER REGION MASTERY nodes ride in here — the one place mastery grows.
            // RATE 1, FOR EVERYONE. `recall_1..4` gave +5 % each to whoever bought them; mastery already
            // EARNED is progression and survives, but a permanent hidden speed multiplier is not.
            _region.RecordActiveKill(1f);

            // MATERIALS FROM MONSTERS — a small trickle every wave, and deeper waves pay in BETTER STUFF
            // rather than more of the same. The steady drip; chests and dismantling are still the bulk.
            // (The farm is no longer a material source.) Rule and tiers live in WaveSpoils.
            var spoil = WaveSpoils.Roll(r.Wave, _rng);
            _hunter.AddMaterial(spoil.Material, spoil.Amount);
            if (spoil.Material != Material.Scrap) _expedition.FlashSpoil(spoil.Material);

            // A CHARTER — one wave in forty. Announced loudly and by NAME, because it is a permission
            // rather than a number: "+1 SCRAP" needs no explanation and "REFINE CHART" does.
            if (spoil.Charter is { } chart)
            {
                _hunter.AddCharter(chart);
                _expedition.FlashCharter(Charters.Name(chart));
            }

            // AND SHOW IT WHERE IT CAME FROM. The banners above say what happened; this makes the haul
            // rise out of the creatures that just died, during the transition's SPOILS beat. Until now
            // every reward the fight paid was either a line of text at the top of the arena or a number
            // that silently changed in a corner pill — a player could clear forty waves without ever
            // seeing where their materials came from.
            _expedition.ShowSpoils(
                r.Haul.Gleam,
                spoil.Material.ToString().ToUpperInvariant(),
                spoil.Charter is { } c2 ? Charters.Name(c2) : null);

            // THE CAREER'S BOSS TALLY (P10) — deterministic, unlike the chest roll beneath it.
            // AND THE FIRST ONE IS AN OBSERVE BEAT: the boss is the game's own rhythm made visible, so
            // it is named the once, on the frame it is felled, and never mentioned again. Raised BEFORE
            // the tally moves, because "is this the first?" is a question about the tally as it was.
            // ...UNLESS THE AUTHORED OPENING HAS ALREADY SAID IT. Its own BOSS WAVE card is the first-boss
            // beat; the coach's observe card, held back while the opening ran, arrived the moment it ended
            // and said the same thing again (seen on the autoplayed opening, 2026-09-11). A player who
            // skipped the tutorial before the boss still gets it.
            if (r.IsBoss && _bossesFelled == 0 && !_opening.Running) _coach.Raise(OnboardingLessonId.FirstBoss);
            // ── THE WELCOME GIFT, FROM THE BOSS THAT EARNED IT. ─────────────────────────────────
            //
            // A LATCH, not a count: exactly one gift per career however many bosses fall, however many
            // descents are made, and however often the game is reloaded between them (the latch is
            // saved on the frame it flips, and every pre-v6 file is seeded latched — its gift was
            // handed out at frame one under the old rule and must never be handed out twice).
            //
            // Through the ordinary vault path, and it is deliberately NOT conditional on the boss's
            // own rolled drop: that roll is a chance and this is a promise. The chest that arrives is
            // the catalogue's welcome gift, whose contents are decided rather than rolled, which is
            // what lets the beats that follow — open the chest, read the item, wear it — be authored
            // at all.
            if (r.IsBoss && !_welcomeGiftGranted)
            {
                _welcomeGiftGranted = true;
                _forge.AddChest(GiftChests.WelcomeChest(def.Theme, def.Id));
                _expedition.FlashChest();
                Save();
            }
            if (r.IsBoss) _bossesFelled++;
            if (r.IsBoss && DropBossChest(r, def)) _expedition.FlashChest();   // a chest is a LOW-rate drop now, not a given
        }
        if (_champSecondsAccrued > 10) _champGleamRate = (float)(_champGleamAccrued / _champSecondsAccrued);

        _deepestEver = Math.Max(_deepestEver, _expedition.Deepest);

        // MASTERY POINTS ARE DERIVED FROM PROGRESS, every frame, so they can never double-count across
        // a reload — nothing is persisted but which nodes were taken. See SkillPointsEarned. (The
        // Memory tree's trait points were the other half of this and are deleted with it.)
        //
        // SKILL POINTS COME FROM DEPTH, AND ONLY FROM DEPTH.
        //
        // The old formula added the Warren's mastery pool, so idle time bought build identity: eight
        // facilities minting 240 mastery a minute at level 1 meant the whole tree was affordable before
        // a player had descended once, and no point in it was ever a choice. It also paid five per
        // conquest, which belongs to the permanent tree, and read one GLOBAL deepest-ever, which let a
        // player bank the entire game's tree progress in a single region.
        //
        // One point per five waves of FIRST-TIME depth, per region. Farming a depth already reached pays
        // haul but no points, so the only way to earn one is to push somewhere new.
        _world.RegionFarm(_activeRegion).RecordDepth(_expedition.Deepest);
        if (_expedition.LogDirty)
        {
            _expedition.LogDirty = false;
            // A RUN JUST ENDED. This is the only frame on which "was a Vow kept for that descent?" can
            // be answered — the run's build is still assembled and its context still describes it. One
            // frame later the expedition has reset and there is nothing left to ask.
            //
            // "Kept" is judged the way the simulation judged it while the run was paying out: the Vow's
            // demand tested against the same BuildContext. That matters — a Vow SWORN and a Vow KEPT are
            // different things, and the quest is about the second.
            if (VowWasKept()) _runsWithVowKept++;
            // AND THE SAME FRAME IS WHERE A VOW REVEALS ITSELF. Four lines below the one that already
            // asks the same question of the same context — because "did this build keep that rule?" is
            // one question, and whether the Vow was SWORN is what separates the two answers.
            RevealProvedVows();
            Save();
        }

        _mastery.SetEarned(SkillPointsEarned());

        // Conquest: the deepest the champion has held this region. Fires once, unlocks the next region.
        if (_expedition.Deepest >= ConquerWaveDepth && !_world.IsConquered(_activeRegion))
        {
            // THE FIRST CONQUEST IS OBSERVED, NOT TAUGHT. It opens the map, the camp, the roster and
            // usually the first keystone in one frame, and the old ladder answered that by explaining
            // all of them — which is the moment the game stopped being playable and became a manual.
            // The beat says one sentence about what just happened; the only LESSON that follows is
            // TRAVEL TO THE NEXT REGION, and the rest wait on the rail with an unread dot until the
            // player walks into them. Raised before Conquer, because "is this the first?" is a
            // question about the count as it stands.
            if (_world.ConqueredIds.Count == 0) _coach.Raise(OnboardingLessonId.FirstRegionConquest);
            var unlocked = _world.Conquer(_activeRegion);
            _dust.AddDust(CorruptionScaling.ConquestDust(_world.CorruptionTier));
            _sound.PlayFirst(1f, "sfx_conquer", "sfx_levelup");
            // AND THE REGION TEACHES YOU ITS DOCTRINE. A conquest is the fast axis of the world and the
            // spine every player walks, so it carries the six keystones the rest of the game depends on
            // — two weapon enchantments are dead without ECHO or BLOODLUST, and both are conquest
            // rewards. The BLURB is not optional: a player who has never owned a keystone learns
            // nothing from the word BLOODLUST on its own — but it is 164 characters at its longest and
            // it goes where a long sentence can be READ, which is the notice toast. See
            // PostKeystoneReveal. The strip gets the compact headline and stays the one line it is.
            var gift = Keystones.Sources.FirstOrDefault(s =>
                           s.RegionId == _activeRegion && s.Rung == WorldRung.Conquest) is { } taught
                       ? Keystones.ById(taught.KeystoneId) : null;
            _conquerMsg = MapScreen.ConquestHeadline(_activeRegion, unlocked, gift);
            if (gift is not null)
            {
                _discoveredKeystones.Add(gift.Id);
                RebuildBuildMenus();
                PostKeystoneReveal(gift);
                // On the FIRST conquest only, because the socket arrives on the same event — the build
                // screen is never showing a keystone with nowhere to put it. Its own toast, queued
                // behind the reveal, so the two are read in the order they happened.
                if (_world.ConqueredIds.Count == 1)
                    PostNotice("A KEYSTONE SOCKET OPENS",
                               "GO TO THE BUILD SCREEN TO WEAR YOUR NEW KEYSTONE.");
            }
            Save();
        }
    }

    /// <summary>The region's mastery-driven progression step, 0..4 — shared by difficulty and the loot tier.</summary>
    private static int RegionProgressionOf(Region region)
        => Math.Clamp((int)region.MasteryLevel + (region.MasteryLevel > 0 ? 1 : 0), 0, 4);

    /// <summary>
    /// The enemy baseline for a region AS IT STANDS NOW — ladder rung, region mastery, corruption
    /// and the region modifier folded into one formula, used by the live fight and the offline
    /// simulation alike, so the champion earns away against the same enemies it fights here.
    /// </summary>
    /// <remarks>
    /// THE REGION STEP IS GEOMETRIC, not linear — see Core/Encounters/RegionLadder for why the
    /// linear form let a player conquer the fourth map with no gear at all. Region MASTERY stays
    /// linear on purpose: a difficulty a player opts into inside one place should stack in even
    /// increments rather than compound with the chain.
    /// </remarks>
    /// <summary>
    /// RIG ONLY: a fight fixture's enemy baseline, pinned so the per-frame push above cannot replace it.
    /// The run restarts on its first live frame (the host's Source push), so a baseline handed to
    /// DevStart alone is gone before the shutter opens; this is what `fightshieldbroken` fights.
    /// </summary>
    private (float Health, float Damage)? _shotEnemyBaseline;

    /// <summary>
    /// RIG ONLY: how far before its target event a posed seek lands (RH_SHOT_LEAD seconds). 0.02 when
    /// unset: the seek applies two frames before the shutter, so anything under two frames (33 ms)
    /// puts the event on the photographed frame itself — callout fresh, effect on its first frame.
    /// </summary>
    private static float ShotLead()
        => float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_LEAD"), System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out var lead) ? lead : 0.02f;

    /// <summary>
    /// THE WAVE MODEL THIS ACCOUNT IS FIGHTING UNDER: the game, unless it has never felled a boss.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fresh validation, 2026-09-12: the first descent reached THORN REGENT on 72 of 180 and died there
    /// in 500 of 500 sweeps — and it was the SAME 500, because a wave's composition is seeded from
    /// (region, wave, RunIndex) and every career's first descent is RunIndex 1. So the first boss did
    /// not merely tend to kill a new player; it was the same authored, unwinnable fight for all of them,
    /// standing between the player and the chest that boss drops. The opening teaches
    /// "boss → meaningful reward"; it was teaching "boss → guaranteed death" instead, before the player
    /// had been handed a single thing to change. See <see cref="ExpeditionTuning.TutorialWaves"/> for
    /// the bill, the sweep and why it is the BITE rather than the boss that is softened.
    /// </para>
    /// <para>
    /// <b>The condition is <see cref="_bossesFelled"/>, not the authored opening.</b> It is a persisted,
    /// monotone save fact, so the window survives a reload mid-opening, survives SKIP TUTORIAL (which
    /// fabricates nothing and would otherwise hand a skipping player the same certain death), and holds
    /// if the player somehow loses the protected boss anyway. The instant any boss falls it is closed
    /// for the life of the career — wave 10's boss, and every later one, is the game as it shipped.
    /// </para>
    /// <para>
    /// Read by the live descent and by the offline simulation both, so an absence fights the same game
    /// the session does. (In practice an account with no boss felled has no absence worth simulating —
    /// but a rate measured against a softer wave and then spent on a harder one is exactly the kind of
    /// quiet divergence this codebase keeps finding, so the two paths read one function.)
    /// </para>
    /// </remarks>
    private ExpeditionTuning TuningNow()
        => _bossesFelled == 0 ? ExpeditionTuning.UntilTheFirstBossFalls : ExpeditionTuning.Default;

    private (float Health, float Damage) EnemyBaselineFor(string regionId)
    {
        var scale = CorruptionScaling.HealthMultiplier(_world.CorruptionTier);
        var mod = RegionModifiers.For(regionId);
        var prog = RegionProgressionOf(_world.RegionFarm(regionId));
        return (110f * (1f + 0.35f * prog) * RegionLadder.Health(LadderIndex(regionId)) * scale * mod.EnemyHealthMult,
                9f * (1f + 0.20f * prog) * RegionLadder.Damage(LadderIndex(regionId)) * scale * mod.EnemyDamageMult);
    }

    /// <summary>A boss dropped a CHEST — its grade rolled by depth now, its contents rolled when opened.</summary>
    /// <remarks>
    /// Depth buys RARITY, not quantity: every boss cleared deeper folds into the tier, so a wave-40 boss
    /// drops a chest likelier to be Legendary — a reason to push, not a firehose. The item roll, the loot
    /// filter, and any auto-merge now happen at OPEN (in the Forge), so nothing lands in the bag unbidden.
    /// </remarks>
    /// <summary>Roll whether this boss drops a chest (a LOW, depth-scaled chance). Returns true if one did.</summary>
    private bool DropBossChest(WaveReward r, RegionDefinition def)
    {
        var depthTier = r.Wave / Math.Max(1, ExpeditionTuning.Default.BossEvery);   // one tier per boss cleared
        // Loot quality climbs with PROGRESSION: how far up the region ladder you are, how deeply you have
        // farmed this region, how deep this run reached, and the world's corruption. A deeper region's
        // chests are innately better — "lootların kalitesi ilerlemeye göre artmalı".
        var lootTier = 1 + LadderIndex(_activeRegion) + _regionProgression + depthTier
                       + CorruptionScaling.TierBonus(_world.CorruptionTier)
                       + RegionModifiers.For(_activeRegion).LootTierBonus;   // the region modifier's loot bump
        if (_rng.NextDouble() >= Chests.DropChance(lootTier)) return false;         // most bosses give nothing

        // The run's carried QUALITY rides along on the chest. This is where SPLINTER — "on kill, richer
        // loot" — finally lands: the trigger adds quality per kill, Haul carries the best wave's worth to
        // the end of the descent, and the chest that descent earned remembers it. Without this the whole
        // chain terminated in a field nothing read, which made REAPER a keystone you pay for and get
        // nothing from.
        var rolled = Chests.RollDrop(lootTier, def.Theme, _rng, runTilt: _expedition.CarriedQuality,
                                     region: _activeRegion);

        // THE KEEP-FILTER. A chest the player said no to never lands — it arrives as a little Scrap,
        // and the banner must not sing about a chest that is not there (hence false, not true).
        if (!Chests.PassesKeepFilter(rolled, _chestKeepMinTier, _chestKeepSlots))
        {
            _hunter.AddMaterials(Chests.FilterCompensation(rolled));
            return false;
        }

        _forge.AddChest(rolled);
        return true;
    }

    /// <summary>
    /// Waves the champion must reach in a region before it is conquered and the next unlocks.
    /// </summary>
    /// <remarks>
    /// Was 10. Lowered so the world is SPREAD across more, shorter regions — you reach a new place sooner
    /// and more often, rather than grinding one region deep. Conquest only UNLOCKS the next region; you can
    /// still farm a conquered one as deep as you like for loot.
    /// </remarks>
    /// <summary>
    /// Waves held to conquer a region. 20, was 7 (playtest 2026-08-26: "seven waves is far too short to
    /// clear a map"): the fourth boss, past the mastery door. Mirrored by HuntScreen.ConquerAt
    /// and Checkpoints.ConquestWave — the pinned test keeps the three in step.
    /// </summary>
    private const int ConquerWaveDepth = Checkpoints.ConquestWave;



    /// <summary>
    /// The "welcome back" toast — offline earnings, or the first-session nudge — for a few seconds, fading.
    /// </summary>
    /// <remarks>
    /// The return-summary is an idle game's best-earned payoff (Cookie Clicker, Melvor Idle, Idle Slayer all
    /// lead with it). This game built the string and never drew it; now it lands on boot, a dark banner
    /// under the header that fades on its own clock.
    /// </remarks>
    private void DrawBootToast()
    {
        // FEEDBACK TIER, like the notice. The first-session nudge ("your champion is already fighting")
        // would be a dim duplicate under a tour's scrim, and it stood frozen under every card of the
        // authored opening and across the lit GEAR panel at 150 % (autoplayed opening, 2026-09-11); it
        // waits under those, a lit lesson, the open log, a fall, a reveal and a modal alike — and its
        // clock is HELD while it waits (the tick in Update reads the same owner), so it lands the instant
        // the frame is handed back rather than expiring behind whatever owned it.
        if (!BootToastShowing) return;
        var fade = Math.Clamp(_bootTimer / 1.2f, 0f, 1f);   // fade over the last ~1.2s
        // Rev 4 §18.4: a FIXED two-line welcome-back toast at (590,165,740,82) — never a full-width band,
        // never ellipsized. Line 1 (duration) at OverlayTitle, line 2 (haul) at OverlayBody. The message is
        // authored as "line1\nline2" by the boot handlers.
        var parts = _bootMessage.Split('\n');
        // Framed panel art rather than a flat band with two gold rules. The flat version butted straight
        // against the stage banner above it and read as a seam in the chrome; a panel with its own border
        // sits clearly ON TOP of the scene, which is what a transient toast should do.
        // The stage header's own width (630..1190), hung under whatever the header stack ends with — the
        // enemy strip, or the boss bar — so the three read as one column rather than a toast across a
        // bar. On a menu screen there is no stack; it takes the old place under the title.
        var r = new Rectangle(ToastLeft, ToastTop, ToastWidth, ToastHeight);
        // THE QUIET PLATE, with an accent in the toast's own colour (UX guide §2: toasts are the QUIET
        // tier). It wore the bronze frame of the persistent stage header above it, so a message that
        // fades in seven seconds outweighed the fight's own enemy strip between them (release polish
        // 2026-09-05, hunt-07).
        _ui.Plate(_batch, r, _bootColor * fade, fade);
        var pad = UiMetrics.Space(12);
        _ui.TextCenterBig(_batch, parts[0], r.Center.X, r.Y + pad, _bootColor * fade, UiTypography.OverlayTitle);
        if (parts.Length > 1)
            _ui.TextCenterBig(_batch, parts[1], r.Center.X, r.Y + pad + UiTypography.Pitch(UiTypography.OverlayTitle),
                              Bone * fade, UiTypography.OverlayBody);
    }

    /// <summary>The two-line toast's height: its pad, a title line, a body line, a breath — it follows the rungs.</summary>
    private static int ToastHeight
        => UiMetrics.Space(12) + UiTypography.Pitch(UiTypography.OverlayTitle) + UiTypography.Pitch(UiTypography.OverlayBody) + UiMetrics.Space(6);

    /// <summary>The WELCOME BACK panel's width at 100 % — wider with the profile so its lines keep their length.</summary>
    private const int WelcomeWidth = 720;

    /// <summary>
    /// WELCOME BACK: the held return panel — AWAY · HUNT · WARREN · CONTINUE. The one gold frame on screen
    /// while it is up; a modal, so nothing under it takes the click that closes it.
    /// </summary>
    /// <remarks>
    /// Every figure is <see cref="WelcomeSummary"/>'s, which is <see cref="OfflineHunt.Result"/>'s and
    /// <see cref="WarrenYield"/>'s: nothing here is estimated, and a zero is not printed. Under any
    /// tour it waits (<see cref="WelcomeUp"/>) rather than competing with the spotlight.
    /// </remarks>
    private void DrawWelcomePanel()
    {
        if (_welcome is not { } w) { _showWelcome = false; return; }

        // ── A RESULTS SCREEN, NOT A REPORT. ──────────────────────────────────────────────────────
        //
        // Playtest 2026-09-09: "you have written the resources and statistics on the Welcome Back panel
        // as flat text — make them iconed and like a results screen; we are not telling a story." The
        // panel had tried prose twice: a run of joined fragments, and then six narrated sentences over
        // the top of them. Both make the reader parse a sentence to find a number.
        //
        // Now every figure is a TILE — picture, figure, word — laid on a grid, which is the shape every
        // end-of-run screen in the genre uses because it can be read at a glance and in any order. The
        // figures and their words are Core's (WelcomeSummary.HuntTiles / WarrenTiles, which omit a zero
        // rather than printing one); the only thing decided here is which picture goes with which.
        var hunt = w.HuntTiles();
        var warren = w.WarrenTiles();
        var resume = w.ResumeLine();

        var width = Math.Min(UiMetrics.Control(WelcomeWidth), UiKit.Page.Width - UiMetrics.Space(40) * 2);
        const int pad = UiTypography.PanelPadX;
        var cw = width - pad * 2;

        // THE GRID. Three across is the widest that keeps a tile square-ish at every profile; a row that
        // does not fill centres, so two tiles never read as one tile and a hole.
        const int cols = 3;
        var gap = UiMetrics.Space(10);
        var tileW = (cw - gap * (cols - 1)) / cols;
        var tileH = WelcomeTileH;
        int GridH(int count) => count == 0 ? 0 : ((count + cols - 1) / cols) * (tileH + gap) - gap;

        var campLines = w.CampLine() is { } campLine ? _ui.WrapBig(campLine, cw, UiTypography.Secondary) : Array.Empty<string>();
        var campH = campLines.Count == 0 ? 0 : campLines.Count * UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(10);
        var headRow = UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4);
        var button = UiMetrics.Control(56);
        var resumeH = resume is null ? 0 : UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(10);

        // Measured, never assumed: this panel has no scrollbar and its frame is cut from the height, so
        // a block that did not fit would print straight through the ornament.
        var h = UiTypography.ModalTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(10)
                + (hunt.Count > 0 ? headRow + GridH(hunt.Count) + UiMetrics.Space(16) : 0)
                + (warren.Count > 0 ? headRow + GridH(warren.Count) + UiMetrics.Space(16) : 0)
                + campH + resumeH + button + pad;
        var r = new Rectangle(UiKit.PageCenterX - width / 2, (UiKit.Page.Height - h) / 2 - UiMetrics.Space(40), width, h);

        _ui.Fill(_batch, UiKit.Page, new Color(0x0B, 0x09, 0x08) * 0.55f);
        _ui.Panel(_batch, r);   // gold: this is a modal, and the one thing on screen
        var x = r.X + pad;
        var y = r.Y + UiTypography.ModalTitleTop;

        // THE TITLE CARRIES THE ABSENCE. It used to be its own Headline line underneath, spending a
        // whole row on two words and a duration; joined, the panel answers "how long was I gone" in the
        // place a reader already looks first.
        _ui.TextCenterBig(_batch, $"WELCOME BACK  ·  AWAY {WelcomeSummary.AwayText(w.AwaySeconds)}",
                          r.Center.X, y, Gold, UiTypography.PanelTitle);
        y += UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(10);

        void Grid(string head, IReadOnlyList<WelcomeTile> tiles)
        {
            if (tiles.Count == 0) return;
            _ui.TextBig(_batch, head, x, y, Slate, UiTypography.Secondary);
            y += headRow;
            for (var i = 0; i < tiles.Count; i++)
            {
                var row = i / cols;
                var inRow = Math.Min(cols, tiles.Count - row * cols);
                // A SHORT LAST ROW CENTRES rather than hugging the left edge, so the block reads as one
                // shape. Two tiles pinned left under three read as a third tile that failed to draw.
                var rowW = inRow * tileW + (inRow - 1) * gap;
                var x0 = x + (cw - rowW) / 2;
                DrawWelcomeTile(tiles[i], new Rectangle(x0 + i % cols * (tileW + gap), y + row * (tileH + gap), tileW, tileH));
            }
            y += GridH(tiles.Count) + UiMetrics.Space(16);
        }

        Grid("THE HUNT", hunt);
        Grid("THE WARREN", warren);

        foreach (var line in campLines) { _ui.TextBig(_batch, line, x, y, Slate, UiTypography.Secondary); y += UiTypography.Pitch(UiTypography.Secondary); }
        if (campLines.Count > 0) y += UiMetrics.Space(10);

        // WHERE IT PICKS UP — the one line about what happens NEXT rather than about what happened, so
        // it sits directly over the button that does it.
        if (resume is not null)
            _ui.TextCenterBig(_batch, resume, r.Center.X, y, Gold, UiTypography.Headline);

        // DRAWN ONLY: the press is taken in Update against this same rectangle — see _welcomeContinue.
        _welcomeContinue = new Rectangle(x, r.Bottom - pad - button, cw, button);
        _ui.Button(_batch, _welcomeContinue, "CONTINUE", ChromeMouse, clicked: false, true, ButtonStyle.Primary);
    }

    /// <summary>A return tile's height: an icon box, the figure, and the word under it, at the profile.</summary>
    private static int WelcomeTileH
        => UiMetrics.Space(12) + UiMetrics.Control(40) + UiMetrics.Space(6)
           + UiTypography.Pitch(UiTypography.PrimaryValue) + UiTypography.Pitch(UiTypography.Caption) + UiMetrics.Space(8);

    /// <summary>
    /// One tile on the return screen: the picture, the figure, the word.
    /// </summary>
    /// <remarks>
    /// The picture is chosen HERE and nowhere else — Core hands over a <see cref="WelcomeFigure"/> and
    /// knows nothing about textures. Every key below is an asset that carries a real symbol, checked by
    /// eye rather than assumed: the crossed blades for waves held, the coin for Gleam, a skull for a
    /// boss, the broken disc for a fall, the map's star for depth, and the three material glyphs the
    /// currency pills already use — so a resource looks the same here as it does everywhere else.
    /// </remarks>
    private void DrawWelcomeTile(WelcomeTile tile, Rectangle box)
    {
        var (key, tint) = tile.Figure switch
        {
            WelcomeFigure.Waves => ("nav_hunt_128", Bone),
            WelcomeFigure.Gleam => ("ui_gleam_coin", Gold),
            WelcomeFigure.Bosses => ("icon_trait_t_carrion_weight", Bone),
            WelcomeFigure.Deepest => ("nav_relics_128", Bone),
            WelcomeFigure.Falls => ("icon_effect_break", UiInk.Danger),
            WelcomeFigure.Dust => ("ui_memory_dust", new Color(0x9E, 0x86, 0xFF)),
            WelcomeFigure.Scrap => ("mat_scrap", new Color(0x9A, 0xC0, 0x88)),
            WelcomeFigure.Essence => ("mat_essence", UiInk.Good),
            _ => ("", Bone),
        };

        _ui.Plate(_batch, box);
        var edge = UiMetrics.Control(40);
        var icon = new Rectangle(box.Center.X - edge / 2, box.Y + UiMetrics.Space(12), edge, edge);
        // A MISSING TEXTURE DOES NOT LEAVE A HOLE. Icon returns false when the key resolves to nothing,
        // and a tile with no picture is still a figure and a word — the house rule for every glyph draw.
        if (!_ui.Icon(_batch, key, icon, Color.White)) _ui.Diamond(_batch, icon, tint * 0.7f);

        var vy = icon.Bottom + UiMetrics.Space(6);
        // THE FIGURE IS THE LOUDEST THING ON THE TILE, at the rung the game reserves for a headline
        // number: this panel exists to be read at a glance after a night away.
        _ui.TextCenterBig(_batch, tile.Value, box.Center.X, vy, tint, UiTypography.PrimaryValue);
        _ui.TextCenterBig(_batch, _ui.ShortenBig(tile.Label, box.Width - UiMetrics.Space(8), UiTypography.Caption),
                          box.Center.X, vy + UiTypography.Pitch(UiTypography.PrimaryValue), Slate, UiTypography.Caption);
    }

    // ── THE BAND UNDER A MENU SCREEN'S TITLE STRIP ─────────────────────────────────────────────
    //
    // Every inset screen draws its name at page y 24, a gold rule at page y 74, and — on TRAITS,
    // MASTERY, VAULT — one Secondary subtitle line at page y 80; its first panel starts at page y 150
    // (TrainingScreen.Top and its siblings). The band between the subtitle and the panel is the host's
    // — the hint slot, and a toast — and it is PAGE geometry mapped to the canvas, because the chrome
    // draws at scale 1 in true 1920×1080 while the screens draw through the overlay matrix: the same
    // numbers through the same transform, so a card sits where the screen actually left room at every
    // profile. (Before UI polish P2 the toast hung at a literal canvas 176 and the slot at 86: right at
    // 100 %, and at 125 and 150 % a slot plate that sat across the subtitle, which showed through it as
    // ghost text — build/shots/m150_dust.png, m150_buildtree.png, m150_vault.png.)
    //
    // THE THREE Y'S ARE THE SCREENS' OWN, AND THE SCREENS DERIVE THEM. Every converted screen writes the
    // same three lines — the name at 24, the rule one title line under it, the subtitle a breath under
    // the rule — and each of those steps is the PROFILE'S line, not a literal:
    //
    //     var ruleY = ScreenTitleTop + UiTypography.Pitch(UiTypography.ScreenTitle) + 1;   (VaultScreen)
    //     _ui.TextCenterBig(..., ruleY + UiMetrics.Space(6), ..., UiTypography.Secondary);
    //
    // so the band ends at page 104 at 100 % and page 144 at 150 %. Pinning the subtitle's own top at the
    // 100 % literal 80 is right at 100 % and 27 px too high at 150 %, which is exactly the ghost text
    // this anchoring was written to remove: the plate landed across the subtitle's lower half and the
    // words read straight through it (build/shots/c6_vault_150.png, before this fix). The same three
    // steps, at the same rungs, put the slot where the screen really left it at every profile.

    /// <summary>The page y a menu screen prints its own name at — the first of the three lines above.</summary>
    private const int PageScreenTitleTop = 24;

    /// <summary>The page y of the gold rule under that name: one screen-title line below it. 74 at 100 %, 99 at 150 %.</summary>
    private static int PageRuleY => PageScreenTitleTop + UiTypography.Pitch(UiTypography.ScreenTitle) + 1;

    /// <summary>The page y a menu screen draws its one subtitle line at, a breath under the rule. 80 at 100 %, 107 at 150 %.</summary>
    private static int PageSubtitleTop => PageRuleY + UiMetrics.Space(6);

    /// <summary>The page y under that subtitle line — one <see cref="UiTypography.Secondary"/> line at the profile's own rung (104 at 100 %, 144 at 150 %).</summary>
    private static int PageSubtitleBottom => PageSubtitleTop + UiTypography.Pitch(UiTypography.Secondary);

    /// <summary>The page y the screens' first panel starts at — the band above it is the host's.</summary>
    private const int PageContentTopBase = 150;

    /// <summary>The page y the screens' first panel starts at — <see cref="UiKit.PageTop"/>, set in <see cref="ApplyUiScale"/>.</summary>
    private static int PageContentTop => UiKit.PageTop;

    /// <summary>A page y in canvas pixels, through the overlay matrix the screens are drawn with.</summary>
    private static int CanvasY(int pageY) => (int)MathF.Round(pageY * OverlayScale);

    /// <summary>
    /// Where a toast hangs: under the hunt's header stack; on a menu screen, under the hint slot when one
    /// is up, else a breath under the subtitle line — so it lands under the subtitle at every profile,
    /// and never on top of the slot.
    /// </summary>
    private int ToastTop => !OverlayActive ? _expedition.HeaderStackBottom + 8
        : SlotShowing() is { } slot ? HintSlotRect(slot).Bottom + UiMetrics.Space(8)
        : CanvasY(PageSubtitleBottom + UiMetrics.Space(8));

    private bool Pressed(Keys k) => !_swallowInput && KeyEdge(k);

    /// <summary>A key going down this frame, before the frame's swallow — for a modal's own keys.</summary>
    private bool KeyEdge(Keys k) => _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    /// <summary>Any key going down this frame — for "press anything to continue" panels.</summary>
    /// <remarks>
    /// Edge-triggered against the previous frame, so a key still held from whatever the player was doing
    /// when the panel appeared does not dismiss it before they have seen it.
    /// </remarks>
    private bool AnyKeyPressed()
    {
        foreach (var k in _keys.GetPressedKeys())
            if (_prevKeys.IsKeyUp(k)) return true;
        return false;
    }

    // ── THE CURSOR: one transform, read once a frame ───────────────────────────────────────────
    //
    // Core.Presentation.PageFrame is the exact inverse of the matrices the frame is drawn through, in
    // floating point; the ONLY rounding is the final floor to a page (or canvas) pixel, which is where
    // a drawn rectangle's edge is. Until 2026-09-01 the menu screens received a cursor floored to a
    // 480×270 canvas, multiplied by four and divided by the page scale — a ~4.5 page-pixel grid on every
    // hover and click ("mouse quantisation across eleven screens", UX V2 REPORT §4). The chrome had
    // been mapped at full resolution since the day the bug was named; the screens had not. Now every
    // consumer reads one of the properties below, computed in ReadCursor() at the top of Update, and
    // no screen converts anything (tools/check_mouse_space.py refuses one that tries).

    /// <summary>This frame's canvas ↔ screen ↔ page geometry. Rebuilt by <see cref="RecomputePresent"/>.</summary>
    private PageFrame _frame = new(new FitRect(0, 0, CanvasWidth * ArtScale, CanvasHeight * ArtScale),
                                   CanvasWidth * ArtScale, CanvasHeight * ArtScale, BaseOverlayScale, OverlayLeft);

    /// <summary>The cursor in PAGE space, floored once to the page pixel — what every inset menu screen hit-tests with.</summary>
    internal static Point PageCursor { get; private set; } = new(-1, -1);

    /// <summary>The cursor in PAGE space as a float — the exact inverse of the overlay matrix, for drags and anything sub-pixel.</summary>
    internal static Vector2 PageMouseF { get; private set; }

    /// <summary>
    /// The cursor in TRUE 1920×1080 canvas space — the chrome's own coordinates, and the fight's.
    /// </summary>
    /// <remarks>
    /// The shared chrome (currency pills, hex nav, title, settings modal) and the fight author in canvas
    /// coords at scale 1, so their hit-tests compare against this. Floored toward negative infinity for
    /// the same reason the page cursor is: a point one pixel LEFT of the canvas must map outside it, not
    /// onto its leftmost column. No clamp on the scale — a 1920-wide canvas is routinely presented smaller.
    /// </remarks>
    internal static Point ChromeMouse { get; private set; } = new(-1, -1);

    /// <summary>The chrome cursor as a float.</summary>
    internal static Vector2 ChromeMouseF { get; private set; }

    /// <summary>
    /// The rig can POSE the cursor so a screenshot run can photograph a hover-only affordance:
    /// RH_SHOT_PAGE_MOUSE=x,y in page space (exact), or RH_SHOT_MOUSE=x,y in the rig's historical
    /// 480×270 unit (×4 = canvas). Parsed once.
    /// </summary>
    // ── THE POSED CURSOR (the capture rig). Two spaces, named, one transform (PageFrame):
    //      RH_SHOT_PAGE_MOUSE=x,y     PAGE space — an inset menu screen's own coordinates, what every
    //                                 menu screen hit-tests (raw; for low-level poses)
    //      RH_SHOT_CANVAS_MOUSE=x,y   CANVAS space — the logical 1920×1080 game UI, what a capture
    //                                 shows; converted through PageFrame.CanvasToPage, never by a
    //                                 second formula
    //      RH_SHOT_MOUSE=x,y          the rig's historical canvas dial, in the 480×270 art grid
    //    A value that does not parse, lies outside its space, or names two spaces at once THROWS at
    //    the first read — a silently defaulted pose photographs the wrong thing and passes.
    //    NO DIAL UNDER THE RIG IS A POSE TOO (2026-09-06): the cursor is PARKED outside the canvas, so a
    //    plain capture hovers nothing. It used to read the desk's mouse, and a "plain" Warren shot
    //    photographed whichever card the mouse happened to rest on. A hover is always posed by a dial.
    private static (bool Page, float X, float Y)? _posedCursor;
    private static bool _posedCursorRead;

    /// <summary>The rig's neutral pose: a canvas point far outside every rectangle a screen could hit-test.</summary>
    private static readonly (bool Page, float X, float Y) ParkedCursor = (false, -4096f, -4096f);

    private static (bool Page, float X, float Y)? PosedCursor
    {
        get
        {
            if (!_posedCursorRead) { _posedCursor = ParsePosedCursor(); _posedCursorRead = true; }
            return _posedCursor;
        }
    }

    private static (bool Page, float X, float Y)? ParsePosedCursor()
    {
        var page = Environment.GetEnvironmentVariable("RH_SHOT_PAGE_MOUSE");
        var canvas = Environment.GetEnvironmentVariable("RH_SHOT_CANVAS_MOUSE");
        var legacy = Environment.GetEnvironmentVariable("RH_SHOT_MOUSE");
        var set = new[] { page, canvas, legacy }.Count(v => !string.IsNullOrEmpty(v));
        if (set == 0) return Environment.GetEnvironmentVariable("RH_SHOT") is not null ? ParkedCursor : null;
        if (set > 1)
            throw new InvalidOperationException("RH_SHOT_PAGE_MOUSE, RH_SHOT_CANVAS_MOUSE and RH_SHOT_MOUSE name one cursor in three spaces — set exactly one.");

        if (!string.IsNullOrEmpty(page))
        {
            var (x, y) = Pair("RH_SHOT_PAGE_MOUSE", page);
            // Page space runs a rail's width past the canvas on the left and a little past it elsewhere.
            if (x < -OverlayLeft / BaseOverlayScale - 1f || x > 1920f / BaseOverlayScale + 1f || y < -1f || y > 1080f / BaseOverlayScale + 1f)
                throw new InvalidOperationException($"RH_SHOT_PAGE_MOUSE={page} is outside page space (about -200..2143 × 0..1205).");
            return (true, x, y);
        }
        if (!string.IsNullOrEmpty(canvas))
        {
            var (x, y) = Pair("RH_SHOT_CANVAS_MOUSE", canvas);
            if (x < 0f || x > CanvasWidth * ArtScale || y < 0f || y > CanvasHeight * ArtScale)
                throw new InvalidOperationException($"RH_SHOT_CANVAS_MOUSE={canvas} is outside the {CanvasWidth * ArtScale}×{CanvasHeight * ArtScale} canvas.");
            return (false, x, y);
        }
        {
            var (x, y) = Pair("RH_SHOT_MOUSE", legacy!);
            if (x < 0f || x > CanvasWidth || y < 0f || y > CanvasHeight)
                throw new InvalidOperationException($"RH_SHOT_MOUSE={legacy} is outside the {CanvasWidth}×{CanvasHeight} art grid.");
            return (false, x * ArtScale, y * ArtScale);
        }

        static (float X, float Y) Pair(string name, string text)
        {
            var parts = text.Split(',');
            if (parts.Length == 2
                && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                return (x, y);
            throw new InvalidOperationException($"{name}={text} is not x,y.");
        }
    }

    /// <summary>Map the raw mouse through this frame's geometry, once. Called at the top of Update.</summary>
    private void ReadCursor()
    {
        (float X, float Y) chrome, page;
        if (Autoplay && _autoCursor is { } hand)
        {
            // THE AUTOPLAYED HAND (Game1.OpeningRig.cs): a canvas point, through the one frame transform
            // every screen hit-tests with — the hand cannot click anything a mouse could not.
            chrome = (hand.X, hand.Y);
            page = _frame.CanvasToPage(hand.X, hand.Y);
        }
        else if (PosedCursor is { } posed)
        {
            // Through the frame — the one transform the renderer and the real mouse use — so the two
            // dials can never disagree with the screen by a formula of their own.
            if (posed.Page)
            {
                page = (posed.X, posed.Y);
                chrome = _frame.PageToCanvas(posed.X, posed.Y);
            }
            else
            {
                chrome = (posed.X, posed.Y);
                page = _frame.CanvasToPage(posed.X, posed.Y);
            }
        }
        else
        {
            chrome = _frame.ScreenToCanvas(_mouse.X, _mouse.Y);
            page = _frame.ScreenToPage(_mouse.X, _mouse.Y);
        }
        ChromeMouseF = new Vector2(chrome.X, chrome.Y);
        var (cx, cy) = PageFrame.Floor(chrome);
        ChromeMouse = new Point(cx, cy);
        // UNDER A TOUR THE SCREEN GETS NO POINTER — the mirror of the keyboard mute (ScreenKeys): with
        // the GEAR tour up, the cursor over a slot raised the item's hover card half inside and half
        // under the scrim (chrome-05). The tour's own click reads ChromeMouse, which stays live.
        if (_tourActive)
        {
            PageMouseF = new Vector2(-1f, -1f);
            PageCursor = new Point(-1, -1);
            return;
        }
        PageMouseF = new Vector2(page.X, page.Y);
        var (px, py) = PageFrame.Floor(page);
        PageCursor = new Point(px, py);
    }

    /// <summary>
    /// Push the chosen display mode to the device and recompute the letterbox.
    /// </summary>
    /// <remarks>
    /// Windowed takes the size the player chose, clamped to something this desktop can hold; borderless
    /// and fullscreen take the monitor's own size. All three then fit the canvas uniformly and letterbox
    /// the remainder — see <see cref="RecomputePresent"/>.
    /// </remarks>
    private void ApplyDisplay()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        switch (_displayMode)
        {
            case DisplayMode.Windowed:
                // CLAMPED HERE, on the way to the device, so there is exactly one place a size can be
                // wrong. A prefs file written on a 4K desktop must not open a 3840-wide window on a
                // laptop — the title bar would be off the screen and the window unmovable.
                _windowSize = Display.NearestOffered(_windowSize, display.Width, display.Height);
                Window.IsBorderless = false;
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = _windowSize.Width;
                _graphics.PreferredBackBufferHeight = _windowSize.Height;
                break;
            case DisplayMode.Borderless:
                Window.IsBorderless = true;
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = display.Width;
                _graphics.PreferredBackBufferHeight = display.Height;
                break;
            case DisplayMode.Fullscreen:
                Window.IsBorderless = false;
                _graphics.IsFullScreen = true;
                _graphics.PreferredBackBufferWidth = display.Width;
                _graphics.PreferredBackBufferHeight = display.Height;
                break;
        }

        _graphics.ApplyChanges();
        RecomputePresent();
    }

    /// <summary>
    /// Where the 1920×1080 canvas lands in the backbuffer: one uniform scale, centred, rest letterbox.
    /// </summary>
    /// <remarks>
    /// ONE RULE FOR ALL THREE MODES since 2026-08-27. Windowed used to have its own branch, because its
    /// sizes were whole multiples of the canvas and could be presented exactly; now that a window is a
    /// real resolution (2560×1440 is 1.333× the canvas) it fits the same way everything else does. The
    /// layout never changes — the canvas is 1920×1080 whatever the window is — so this is the only code
    /// a resolution touches.
    /// </remarks>
    private void RecomputePresent()
    {
        _present = Display.PresentFit(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        ApplyUiScale();
        _frame = new PageFrame(new FitRect(_present.X, _present.Y, _present.Width, _present.Height),
                               CanvasWidth * ArtScale, CanvasHeight * ArtScale, OverlayScale, OverlayLeft);
        ReadCursor();
    }

    /// <summary>Wheel notches this frame: + = scroll up/away, - = down/toward. Latched in Update.</summary>
    private int MouseWheel => _wheel;

    /// <summary>True on the frame the left button goes down.</summary>
    /// <summary>
    /// The click edge, as the game screens see it — suppressed while a modal is up.
    /// </summary>
    /// <remarks>
    /// The screens hit-test during Draw, so an overlay drawn last cannot stop the click reaching them
    /// by drawing over it: both would fire. Gating here is the single choke point every screen reads.
    /// The settings panel deliberately reads the raw <c>_clicked</c> instead, since it IS the modal.
    /// </remarks>
    /// <summary>True while a modal explanation is up — every input path below reads it.</summary>
    private bool _swallowInput;

    /// <summary>
    /// A MODAL WAS OPENED BY THIS VERY CLICK EDGE, so the panel it opened must not also act on it.
    /// </summary>
    /// <remarks>
    /// One edge, at most one action. The settings gear sets it: without it, the click that opens the
    /// panel is still live when the panel's own input runs later in the SAME Update, and lands on
    /// whatever row happens to sit under the gear's corner. It was latent while the panel decided its
    /// clicks in Draw — Draw runs after the whole of Update, but the edge was still the same one — and
    /// moving that decision into Update is what made it worth closing rather than describing.
    /// Cleared at the top of every Update; <c>_swallowInput</c> cannot do this job, because it is also
    /// true while the authored opening owns the frame and SETTINGS is the opening's one way out.
    /// </remarks>
    private bool _modalOpenedNow;

    /// <summary>
    /// The HELP panel's close icon and the WELCOME panel's CONTINUE, as Draw last laid them out —
    /// hit-tested in Update on the next frame.
    /// </summary>
    /// <remarks>
    /// THE ONE GEOMETRY, CACHED RATHER THAN RE-DERIVED. Both panels size themselves from MEASURED text
    /// (the help columns wrap against the panel's width; the welcome panel's height is its tiles, its
    /// camp lines and its resume line), so a pure layout function for either would have to re-run the
    /// measurement — a second geometry system, which is the one thing this pass must not build. Storing
    /// the rectangle the paint actually used makes "what is drawn" and "what is hit-tested" the same
    /// object by construction. The same idiom, for the same reason, as the opening's own
    /// <c>_openingButton</c> / <c>_prologueNext</c> (Game1.Opening.cs) — and safe here because neither
    /// rectangle can move without the content moving, and both controls have a keyboard twin handled in
    /// Update (F1 / Esc, and ENTER / SPACE) that never depended on Draw at all.
    /// </remarks>
    private Rectangle _helpClose, _welcomeContinue;

    /// <summary>The HELP panel's scrolling view and its overflow, as Draw last measured them.</summary>
    private Rectangle _helpView;
    private int _helpMaxScroll;

    private bool MouseClicked => _clicked && !HostModalUp && !WelcomeUp && !_swallowInput;

    private bool MouseRightClicked => _rightClicked && !HostModalUp && !WelcomeUp && !_swallowInput;


    /// <summary>
    /// Open a SpriteBatch for the virtual canvas at the given counter-scale, syncing the drawing helpers so
    /// native-resolution assets and text land 1:1. <paramref name="scale"/> is 4 for 480×270 logical screens
    /// (the default) or 1 for screens authored directly in 1920×1080.
    /// </summary>
    private void BeginCanvas(int scale)
    {
        _ui.Scale = scale;
        _ui.Text2.Density = scale;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale));
    }

    /// <summary>Begin the batch for a menu screen: authored 1920-wide, drawn inset right of the nav rail.</summary>
    private void BeginOverlayCanvas()
    {
        _ui.Scale = 1;
        // Glyphs rasterised at the page scale land 1:1 on the canvas after the matrix — crisp at 100% (where
        // they were being shrunk to 0.9 from a 1080 raster) and at 125%/150% (where they would be blown up).
        _ui.Text2.Density = OverlayScale;
        // The camera kick belongs to the screen that wants one. A global shake used to live in the
        // present blit, driven by manual combat, and sat at a permanent zero from the pivot onward
        // because nothing owned it; this asks the active screen instead, so the kick exists only while
        // something is actually asking for it.
        var kick = ScreenSettle;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, OverlayTransform(kick));
    }

    /// <summary>
    /// The one transform every inset menu screen is drawn through: scaled to the free width and pushed
    /// right of the rail, plus the screen's own camera kick.
    /// </summary>
    /// <remarks>
    /// Shared with the screens themselves, because the TRAITS screen clips its free tree canvas with a
    /// scissor and has to reopen the batch with the SAME matrix the host opened it with. Two copies of
    /// this arithmetic would drift the next time the rail changed width; one method cannot.
    /// </remarks>
    internal static Matrix OverlayTransform(Vector2 kick)
        => Matrix.CreateScale(OverlayScale) * Matrix.CreateTranslation(OverlayLeft + kick.X, kick.Y, 0f);

    /// <summary>An overlay-space rectangle in canvas pixels — what a scissor rectangle has to be given.</summary>
    internal static Rectangle OverlayToCanvas(Rectangle r, Vector2 kick)
        => new((int)MathF.Round(r.X * OverlayScale + OverlayLeft + kick.X),
               (int)MathF.Round(r.Y * OverlayScale + kick.Y),
               (int)MathF.Round(r.Width * OverlayScale),
               (int)MathF.Round(r.Height * OverlayScale));

    /// <summary>True when a menu screen owns the frame — those draw inset; the fight screen does not.</summary>
    private bool OverlayActive =>
        _showForge || _showWorld || _showTraits || _showWarren || _showMastery || _showGear
        || _showTraining || _showRoster || _showLoadout || _showVault;

    /// <summary>The counter-scale the active screen draws at. Converted 1920-coord screens return 1; the
    /// HUNT screen is converted, so it returns 1 whenever no other screen flag is set (the else branch below).</summary>
    // Every screen now authors in true 1920×1080 at canvas scale 1 (the migration is complete). This shim
    // stays only so step 5 can flip the global constants and delete it in one place; it is now a constant 1.
    private int ScreenScale() => 1;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    protected override void Draw(GameTime gameTime)
    {
        // Render the 1920x1080 virtual canvas, then fit it to the backbuffer (see RecomputePresent).
        GraphicsDevice.SetRenderTarget(_canvas);
        GraphicsDevice.Clear(VoidInk);
        // LinearClamp, not PointClamp: this is hand-drawn art now, and the rule forbidding filtering
        // existed to protect pixel art. The canvas transform (see BeginCanvas) is what lets every screen
        // keep its 480x270 coordinates while the canvas underneath is 4x denser.

        if (_showTitle)
        {
            BeginCanvas(1);   // the title chrome authors in true 1920×1080 coords
            DrawTitle();
            if (_showSettings) DrawSettings();
            _batch.End();
            _shotFrame++;
            if (Environment.GetEnvironmentVariable("RH_SHOT") is { } sp && _shotFrame == 60)
            {
                using var fs0 = System.IO.File.Create(sp);
                _canvas.SaveAsPng(fs0, CanvasWidth * ArtScale, CanvasHeight * ArtScale);
                Exit();
            }
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(LetterboxInk);
            // The present blit. LinearClamp and a NON-integer scale are both fine now — the rule against
            // them protected pixel art, and there is none left to protect.
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            _batch.Draw(_canvas, _present, Color.White);
            _batch.End();
            base.Draw(gameTime);
            return;
        }

        // Three canvas regions, each in its own batch so the active screen can later run at a different
        // scale (1920 coords) while the shared background and HUD stay at the 480-logical scale.
        // Batch A — the scene background, authored in true 1920×1080 coords at scale 1.
        BeginCanvas(1);
        DrawSceneBackground();
        _batch.End();

        // Batch B — the active screen. Menu screens draw through the overlay inset (see OverlayScale).
        if (OverlayActive) BeginOverlayCanvas(); else BeginCanvas(ScreenScale());
        if (_showForge)
        {
            _forge.Draw(_batch, _hunter, PageCursor);
        }
        else if (_showWorld) DrawWorld();
        else if (_showTraits)
        {
            // PAINT ONLY. Every click, the wheel, the drag, the cue and the save are resolved in the
            // `if (_showTraits)` block of Update.
            _traitScreen.Draw(_batch, _traitLedger, _characters.ActiveId,
                              _characters.Active?.Name ?? "YOUR HUNTER", PageCursor);
        }
        else if (_showRoster)
        {
            _roster.Progress = QuestSnapshot();
            _roster.Mastery = _mastery;
            _roster.Hunter = _hunter;
            _roster.Draw(_batch, _characters, PageCursor);
        }
        else if (_showVault) _vault.Draw(_batch, _forge.UnopenedChests, PageCursor);
        else if (_showLoadout)
        {
            _loadoutScreen.Draw(_batch, PageCursor);
        }
        else if (_showWarren) DrawWarren();
        else if (_showMastery) _masteryScreen.Draw(_batch, PageCursor);
        else if (_showGear) _gear.Draw(_batch, PageCursor, _hunter);
        else if (_showTraining)
        {
            PushTrainingState();   // never painted with nothing pushed — see PushTrainingState
            _training.Draw(_batch, PageCursor, _hunter);
        }
        else
        {
            // The overlays' foot, for the fight's floating words (HuntScreen.OverlayFloor, hunt-01): this
            // frame's boot toast or lesson card hangs under the header stack, and a callout must not rise
            // behind it.
            _expedition.OverlayFloor = BootToastShowing
                ? ToastTop + ToastHeight + UiMetrics.Space(8)
                : HuntLessonShowing() is { } lessonUp ? HuntLessonRect(lessonUp).Bottom + UiMetrics.Space(8) : 0;
            // The fight's own banners yield to a boot toast that is ON SCREEN, to the opening (whose held boot
            // line is what the fresh save's seven seconds are for) and to the welcome — never to a toast that
            // is merely waiting behind a lit lesson, which could stand for as long as the lesson does.
            _expedition.Draw(_batch, ChromeMouse, Regions.Get(_activeRegion).Name, EnemyArtFor(_activeRegion), BootToastShowing || _opening.Running || WelcomeUp);
        }

        // The LOG draws over everything, including the nav rail: it is a full-screen read, and the one
        // overlay a player opens to think rather than to act.
        _expedition.DrawLog(_batch, ChromeMouse);
        _batch.End();

        // Batch C — the shared overlays (pills, nav, help/settings, boot toast), authored in true 1920 coords.
        BeginCanvas(1);

        // THE SWITCH READS AS AN ARRIVAL (brief sec. 33). The page lifts the last few pixels into place
        // (the kick above) under a scrim that clears over 130 ms, so a new screen arrives rather than
        // replaces. Drawn BEFORE the pills, the gear and the rail, because sec. 33 says the navigation
        // stays: the rail did not change, and dimming it would say it had. Reduced Motion keeps the
        // scrim — a fade is the one kind of motion sec. 32 allows — and drops the lift.
        if (_screenFade > 0f)
            _ui.Fill(_batch, new Rectangle(NavRailWidth, 0, UiKit.Page.Width - NavRailWidth, UiKit.Page.Height),
                     ScreenFadeInk * (ScreenFadeT * ScreenFadeMax));

        DrawCurrencyPills();   // shared Gleam / Dust / Materials row, top-right of every screen
        DrawSettingsGear();    // the corner gear — settings from any screen, including mid-hunt
        DrawLearnButton();     // ...and the ? beside it, which is where the screen tours live now
        DrawDispatchButton();  // ...and the envelope beside that, where account news waits
        DrawCoachSpotlight();  // the guided lesson's brackets, on the screen's own control
        DrawHexNav();   // the shared nav bar, over every screen

        // The chest burst, over the rail and over whatever screen is open — it is the one moment the
        // game asks the player to stop and look, so nothing on the page should sit on top of it.
        if (_forge.RevealActive) _forge.DrawRevealOverlay(_batch, _hunter);

        // A CLOSED MODAL IS BACK AT ITS TOP. Both scrolls only ever leave zero at 150 %, where the
        // rows outgrow the page — and the UI SCALE row is at the top of the settings panel, so a
        // scroll that survived the close would hide the one control that can undo 150 %.
        if (!_showHelp) _helpScroll = 0;
        if (!_showSettings) _settingsScroll = 0;
        if (!_showDispatches) _dispatchScroll = 0;

        if (_showHelp) DrawHelp();
        if (_showTypeSpec) DrawTypeSpec();
        if (_showSettings) DrawSettings();
        if (_showDispatches) DrawDispatches();

        // A MODAL FADES UP (brief sec. 34): backdrop and panel together over one fast beat, so the
        // settings panel stops appearing between two frames. Over the modal, under everything the
        // modal must not hide — the toasts and the tour below draw after it.
        if (_modalFade > 0f)
            _ui.Fill(_batch, new Rectangle(0, 0, UiKit.Page.Width, UiKit.Page.Height),
                     ScreenFadeInk * (UiMotion.Smooth(_modalFade / UiMotion.Fast) * ScreenFadeMax));
        if (WelcomeUp) DrawWelcomePanel();

        DrawBootToast();
        DrawHuntLesson();
        DrawHintSlot();
        DrawLockedToast();
        DrawNoticeToast();
        // LAST of the chrome, so a tour's scrim and spotlight sit over everything — including the
        // nav rail the intro has a card about.
        DrawTour();
        // ...AND THE AUTHORED OPENING OVER EVEN THAT. Only one of the two can be running — a tour is
        // asked for from a screen the opening has not released yet — but the order is stated rather
        // than assumed, because the opening is the one that can freeze the game underneath it.
        DrawOpening();

        _batch.End();

        // THE OPENING RIG'S FILM (Game1.OpeningRig.cs): the finished canvas, when a moment asked for it.
        if (OpeningRigOn) OpeningFilmShot();

        // DEV SCREENSHOT HOOK: set RH_SHOT=<path> to dump one upscaled frame after ~1s, then exit.
        // Used to verify rendering headlessly; harmless and inert without the env var.
        _shotFrame++;
        ShotFrameNow = _shotFrame;
        var shotPath = Environment.GetEnvironmentVariable("RH_SHOT");
        // RH_SHOT_SEQ="count,stride" turns the one posed frame into a FILMSTRIP: from frame 60 on, every
        // `stride` frames a full canvas is saved as <path>_NN.png, `count` times, then exit. A single
        // capture can prove a layout; it cannot prove a rhythm — whether the swing lands on the hit, how
        // long a burst lingers, whether the cast opens as the effect appears. Assemble the strip with
        // tools/asset-pipeline/v2/filmstrip.py and LOOK at it. Fixed-step updates make it deterministic.
        if (shotPath is not null && Environment.GetEnvironmentVariable("RH_SHOT_SEQ") is { } seqSpec
            && seqSpec.Split(',') is { Length: 2 } seqParts
            && int.TryParse(seqParts[0], out var seqCount) && int.TryParse(seqParts[1], out var seqStride)
            && seqStride > 0)
        {
            if (_shotFrame >= 60 && (_shotFrame - 60) % seqStride == 0)
            {
                var idx = (_shotFrame - 60) / seqStride;
                var seqPath = System.IO.Path.ChangeExtension(shotPath, null) + $"_{idx:00}.png";
                using var fsq = System.IO.File.Create(seqPath);
                _canvas.SaveAsPng(fsq, CanvasWidth * ArtScale, CanvasHeight * ArtScale);
                if (idx + 1 >= seqCount) Exit();
            }
        }
        else if (shotPath is not null && _shotFrame == 60 && ShotWindow is null)
        {
            // RH_SHOT_DUMP: the wave's events beside the shot, so a fight pose can be checked against
            // what the wave actually contained rather than against a guess about the timeline.
            if (Environment.GetEnvironmentVariable("RH_SHOT_DUMP") is not null)
            {
                System.IO.File.WriteAllLines(shotPath + ".events.txt", _expedition.DevWaveEvents());
                // ...and the actor geometry the photographed frame hit-tested with: each figure's body,
                // its envelope and the hunter's keep-out, in canvas pixels — the numbers a hover pose
                // is aimed from, and the proof that a rectangle did not move between two frames.
                System.IO.File.WriteAllLines(shotPath + ".actors.txt", _expedition.DevActorGeometry());
            }

            using var fs = System.IO.File.Create(shotPath);
            _canvas.SaveAsPng(fs, CanvasWidth * ArtScale, CanvasHeight * ArtScale);
            // RH_UI_BUDGET=1: the raster ledger for whatever this shot drew (BRIEF sec.74). Printed
            // after the frame so it reports the screen that was actually photographed.
            UiRasterLedger.Dump();
            // RH_UI_TEXT=1: every label this shot could not fit. The truncation ledger — see UiTextLedger.
            UiTextLedger.Dump();
            // RH_LESSON_LEDGER=1: the onboarding counters for the state this shot posed.
            DumpLessonLedger();
            Exit();
        }

        GraphicsDevice.SetRenderTarget(null);
        // THE LETTERBOX. This clear IS the bars — everything the canvas does not cover on this
        // backbuffer. A window whose aspect is not 16:9 (and every fullscreen on a 16:10 or ultrawide
        // panel) shows them, so they get the palette's darkest ink rather than the canvas's own.
        GraphicsDevice.Clear(LetterboxInk);

        // The present blit. LinearClamp and a NON-integer window scale are both fine now — that rule
        // existed to protect pixel art, and there is none left to protect.
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // Screen shake lived here, driven by manual combat's impacts. Nothing has driven it since the
        // pivot — the expedition does its own game-feel inside ExpeditionScreen — so it was a permanent
        // zero. If the auto-battle ever wants a camera kick, it should own one, not inherit this.
        _batch.Draw(_canvas, _present, Color.White);
        _batch.End();

        // The posed-window capture: the backbuffer AFTER the blit — letterbox, bilinear shrink and all.
        if (shotPath is not null && ShotWindow is not null && _shotFrame == 60)
        {
            SaveBackBuffer(shotPath);
            Exit();
        }

        base.Draw(gameTime);
    }

    /// <summary>Write the current backbuffer — the frame the player would receive — to a PNG.</summary>
    private void SaveBackBuffer(string path)
    {
        var pp = GraphicsDevice.PresentationParameters;
        var data = new Color[pp.BackBufferWidth * pp.BackBufferHeight];
        GraphicsDevice.GetBackBufferData(data);
        using var tex = new Texture2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);
        tex.SetData(data);
        using var fs = System.IO.File.Create(path);
        tex.SaveAsPng(fs, pp.BackBufferWidth, pp.BackBufferHeight);
    }

    /// <summary>
    /// THE TYPE SPECIMEN (fixture <c>typespec</c>): every rung of the ladder with its name, size, pitch and
    /// weight on the plain page, and the combat callouts beside it — the one picture that shows whether the
    /// ladder reads as a hierarchy, at 1080 and, through RH_SHOT_WINDOW, at 720.
    /// </summary>
    private void DrawTypeSpec()
    {
        _ui.Fill(_batch, new Rectangle(0, 0, CanvasWidth * ArtScale, CanvasHeight * ArtScale), new Color(0x10, 0x0D, 0x18));
        var rungs = new (string Name, int Px)[]
        {
            ("SCREEN TITLE", UiTypography.ScreenTitle), ("PRIMARY VALUE", UiTypography.PrimaryValue),
            ("PANEL TITLE", UiTypography.PanelTitle), ("HEADLINE", UiTypography.Headline),
            ("NAVIGATION LABEL", UiTypography.NavigationLabel), ("BODY", UiTypography.Body),
            ("SECONDARY", UiTypography.Secondary), ("CAPTION", UiTypography.Caption),
        };
        const int x = 120;
        var y = 60;
        _ui.TextBig(_batch, "IDLE X IDLE — TYPE SPECIMEN", x, y, UiInk.Accent, UiTypography.PanelTitle);
        y += UiTypography.Pitch(UiTypography.PanelTitle) + 16;
        foreach (var (name, px) in rungs)
        {
            var pitch = UiTypography.Pitch(px);
            _ui.TextBig(_batch, $"{name}  {px} PX · PITCH {pitch}", x, y, UiInk.Secondary, UiTypography.Caption);
            y += UiTypography.Pitch(UiTypography.Caption);
            _ui.TextBig(_batch, "The hunter fell at WAVE 13 — 12 waves cleared · 0123456789", x, y, UiInk.Primary, px);
            y += pitch + 14;
        }
        _ui.TextBig(_batch, $"PAGE {UiKit.Page.Width} × {UiKit.Page.Height} · UI SCALE {UiMetrics.Percent}%"
                            + (ShotWindow is { } sw ? $" · WINDOW {sw.X} × {sw.Y}" : ""),
                    x, 1010, UiInk.Secondary, UiTypography.Secondary);

        const int cx = 1360;
        var cy = 120;
        _ui.TextBig(_batch, "COMBAT CALLOUTS — A FAMILY BESIDE THE LADDER", cx, cy, UiInk.Secondary, UiTypography.Caption);
        cy += UiTypography.Pitch(UiTypography.Caption) + 8;
        _ui.TextBig(_batch, "1 240", cx, cy, UiInk.Primary, UiTypography.DamageNormal);
        cy += UiTypography.Pitch(UiTypography.DamageNormal);
        _ui.TextBig(_batch, "3 249", cx, cy, UiInk.Accent, UiTypography.DamageSkill);
        cy += UiTypography.Pitch(UiTypography.DamageSkill);
        _ui.TextBig(_batch, "9 870!", cx, cy, UiInk.Danger, UiTypography.DamageCritical);
        cy += UiTypography.Pitch(UiTypography.DamageCritical) + 24;
        _ui.TextBig(_batch, "INKS ON THE PANEL", cx, cy, UiInk.Secondary, UiTypography.Caption);
        cy += UiTypography.Pitch(UiTypography.Caption) + 4;
        foreach (var (label, ink) in new[] { ("PRIMARY", UiInk.Primary), ("SECONDARY", UiInk.Secondary), ("ACCENT", UiInk.Accent),
                                             ("DISABLED", UiInk.Disabled), ("GOOD", UiInk.Good), ("DANGER", UiInk.Danger) })
        {
            _ui.TextBig(_batch, label, cx, cy, ink, UiTypography.Body);
            cy += UiTypography.Pitch(UiTypography.Body);
        }
    }

    private int _shotFrame;

    /// <summary>RIG ONLY: true when RH_SHOT is set — the process exists to take one screenshot and exit.</summary>
    internal static readonly bool RigActive = Environment.GetEnvironmentVariable("RH_SHOT") is not null;

    /// <summary>RIG: RH_SHOT_HELD=1 poses the mouse button held down, so a PRESSED face can be photographed.</summary>
    private static readonly bool ShotHeld = Environment.GetEnvironmentVariable("RH_SHOT_HELD") == "1";

    /// <summary>RIG ONLY: the frame the shutter opens on. Every capture mode saves at this frame.</summary>
    internal const int ShotAtFrame = 60;

    /// <summary>RIG ONLY: the frame being drawn, so a screen can apply a posed seek just before the shutter.</summary>
    internal static int ShotFrameNow { get; private set; }

    /// <summary>Hand the TRAINING screen everything it resolves a build from.</summary>
    /// <remarks>
    /// Called by the Update that runs the screen AND by the Draw that paints it. A frame that opened
    /// TRAINING and then left Update before reaching the screen's block painted it with nothing pushed:
    /// a NullReferenceException on a fresh career, caught by the boot check's screen walk (2026-09-11).
    /// The same references every frame, so pushing twice costs nothing.
    /// </remarks>
    private void PushTrainingState()
    {
        _training.Loadout = _loadout;
        _training.Mastery = _mastery;
        _training.DiscoveredKeystones = _keystoneMenu;
        _training.KnownVows = _vowMenu;
        _training.Character = _characters.Active;
        _training.SkillLevels = _skillProgress;   // the live screen resolved a build without them
    }

    // ── Title screen ──────────────────────────────────────────────────────────────────────────

    /// <summary>How many plates the title offers: PLAY / CONTINUE, SETTINGS, QUIT.</summary>
    internal const int TitleItems = 3;

    /// <summary>
    /// One title plate's rectangle, 1920-space chrome — the ONE layout, read by the hit test in Update
    /// and by the paint in <see cref="DrawTitle"/>.
    /// </summary>
    /// <remarks>
    /// Published (and static) so a test can drive the menu without a GraphicsDevice, the way
    /// <c>SettingsFrameNow</c> and <c>ClickableOf</c> already are. Deliberately NOT scaled by
    /// <see cref="UiMetrics"/>: a 640x96 plate is far above the hit-target floor at every profile and
    /// the title is authored in true canvas coordinates — only its LABEL follows the density rung.
    /// </remarks>
    internal static Rectangle TitlePlate(int i) => new(640, 608 + i * 112, 640, 96);

    private void ChooseTitleItem(int i)
    {
        switch (i)
        {
            // PLAY / CONTINUE. A FRESH CAREER DOES NOT LAND IN A FIGHT: it lands in the prologue, and
            // BEGIN THE HUNT is the deliberate press that follows it. `_hasSave` is already the exact
            // "this is a brand-new career" fact and is already false on the path with no CONTINUE.
            case 0:
                _showTitle = false;
                if (!_hasSave && _opening.Stage == OpeningStage.NotStarted) _opening.Begin();
                break;
            case 1: _showSettings = true; _modalOpenedNow = true; break;   // see _modalOpenedNow
            default: Exit(); break;
        }
    }

    private void SaveDisplay() => Display.Save(new Display.GamePrefs(
        _displayMode, _windowSize, _sfxVolume, _musicVolume, _askBeforeScrap,
        _showDamageNumbers, _showSkillCallouts, _showHitEffects, _showScreenFlash,
        UiScalePercent: _uiScalePercent, ReducedMotion: ReducedMotion));

    // ── Settings ──────────────────────────────────────────────────────────────────────────────
    // 1920-space (scale-1 chrome). 2026-08-24, playtest nine: MODE and WINDOW SIZE became dropdowns
    // and the volume rows became draggable sliders, which freed two button rows of height — the panel
    // shrank from 1250x960 to 1250x880 and moved down under the title. Aspect 1250/880 = 1.42, still
    // comfortably above UiKit.Panel's 1.30 medium-frame line, so the frame art is unchanged.
    //
    // LAID OUT, NOT WRITTEN (UI polish P2, brief §83–§85). The rectangles here used to be static
    // readonly fields — frozen at class load, so UI SCALE could not move them, and at 150 % the title
    // printed through the column headers, ASK BEFORE SELL OR SALVAGE through its own button and the
    // UI SCALE caption through the AUDIO header. SettingsFrameNow lays the whole panel out from
    // UiMetrics in one pass: the 100 % numbers above come out unchanged, the columns and rows grow
    // with the profile, and when the rows outgrow the page they SCROLL under a fixed title and QUIT
    // row — the close icon and the UI SCALE row stay reachable, so 150 % can always be switched off.
    //
    // ── TWO COLUMNS, GROUPED BY PURPOSE (brief §36). One column of nine unrelated rows made the
    //    player read the whole list to find one switch, and it left the panel's right half empty
    //    while its controls stayed small enough to be a chore to hit (§35).

    /// <summary>A column's width at 100 %. Two of them, a gap and the insets make the 1250 panel.</summary>
    private const int SetColWBase = 560;

    /// <summary>The content inset from the panel's edge — 3 px inside the frame's corner reach. Art geometry.</summary>
    private const int SetInset = 37;

    /// <summary>The least the panel will be — it is its content above this (chrome-14: at a fixed 880 the footer floated 140 px under the columns at 100 %).</summary>
    private const int SetDesignHeight = 560;

    /// <summary>
    /// The settings modal's geometry at the current profile — page space, rows UNSCROLLED. Pure
    /// arithmetic over UiMetrics (no text is measured), so the static rects below hold at every
    /// profile without a font. Every y is absolute; the draw adds the scroll.
    /// </summary>
    private struct SettingsFrame
    {
        public Rectangle Panel, Close, View, Quit;
        /// <summary>True when the rows outgrow the room above the QUIT row and scroll under the header.</summary>
        public bool Scrolls;
        /// <summary>How tall the rows are from <see cref="Top"/> — what the scroll range is measured against.</summary>
        public int ContentHeight;
        public int LeftX, RightX, ColW, RightW, Top, Rule;
        public int DisplayY, ScaleCaptionY, AudioY, KeysNoteY;
        public Rectangle ModeRow, SizeRow, ScaleRow, FxTrack, MusicTrack;
        public int GameplayY, AskLabelY, AccessY, ToggleY, TogglePitch, AccessCaptionY;
        public Rectangle AskBtn, CopyFeedback, Danger, NewGame, NewGameArmed;
        public int DangerTitleY, DangerTextY;
    }

    /// <summary>Lay the settings panel out for the current profile. See <see cref="SettingsFrame"/>.</summary>
    private static SettingsFrame SettingsFrameNow()
    {
        var page = UiKit.Page;
        var colW = UiMetrics.Control(SetColWBase);
        var colGap = UiMetrics.Space(44);
        var width = colW * 2 + colGap + SetInset * 2;
        var labelW = UiMetrics.Control(230);      // the label column a field sits after
        var fieldH = UiMetrics.Control(56);       // a dropdown field, and the UI SCALE button
        var fieldPitch = UiMetrics.Control(64);
        var sliderPitch = UiMetrics.Control(56);
        var trackH = UiMetrics.Control(30);
        var trackW = colW - UiMetrics.Control(300);
        var button = UiMetrics.ButtonHeight;
        var wide = UiMetrics.Control(360);        // COPY FEEDBACK CODE, QUIT TO DESKTOP, START A NEW GAME
        var body = UiTypography.Body;
        var bodyPitch = UiTypography.Pitch(body);
        var captionPitch = UiTypography.Pitch(UiTypography.Secondary);
        var rule = captionPitch + 2;              // a group's header text top → its hairline
        var ruleToRow = UiMetrics.Space(8);
        var sectionGap = UiMetrics.Space(16);
        var headerH = UiTypography.ModalTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) - 4;
        var padBottom = UiMetrics.Space(36);

        // The left column, flowing down from the header (y relative to the content top).
        var modeY = rule + ruleToRow;
        var sizeY = modeY + fieldPitch;
        var scaleY = sizeY + fieldPitch;
        var scaleCapY = scaleY + fieldH + UiMetrics.Space(6);
        var audioY = scaleCapY + captionPitch + sectionGap;
        var fxRowY = audioY + rule + ruleToRow;
        // No CONTROLS group: it was a head over two lines of boilerplate, and at 150 % those were the
        // rows that pushed the column into scrolling. The one sentence it carried lives in the footer.
        var leftRows = fxRowY + sliderPitch * 2;

        // The right column.
        var askY = rule + UiMetrics.Space(8);
        var accessY = askY + button + UiMetrics.Space(10);
        var toggleY = accessY + rule + UiMetrics.Space(8);
        // The toggle pitch is a ROW and a breath — at Control(58) the five switches alone were 435 px
        // at 150 %, which is what pushed the whole column into scrolling (chrome-02).
        var togglePitch = UiMetrics.RowHeight + UiMetrics.Space(6);
        var accessCapY = toggleY + togglePitch * 4 + UiMetrics.RowHeight + UiMetrics.Space(6);
        var rightRows = accessCapY + captionPitch;
        var dangerTextY = UiMetrics.Space(40);
        var dangerBtnY = dangerTextY + body + UiMetrics.Space(14);
        var dangerH = dangerBtnY + button + UiMetrics.Space(20);

        // The panel: the design height at 100 %, taller when the profile asks, never past the page.
        //
        // THE FOOTER IS PINNED AT EVERY PROFILE — QUIT, COPY FEEDBACK CODE and the DANGER ZONE — and
        // only the four groups of rows scroll, above it. They used to flow after the rows when the
        // rows scrolled, which at 150 % put START A NEW GAME and COPY FEEDBACK CODE under the clip and
        // sliced the DANGER plate in half: a 150 player had no new-game door and no feedback button
        // (release polish 2026-09-05, chrome-02). The footer's height is what the panel needs under
        // the rows; the rows take the rest and scroll when it is not enough.
        var leftFooter = UiMetrics.Space(10) + captionPitch + UiMetrics.Space(8)          // the keys note
                       + button + UiMetrics.Space(8) + captionPitch + UiMetrics.Space(10)  // COPY and its caption
                       + button;                                                            // QUIT
        var footer = Math.Max(leftFooter, dangerH) + padBottom;
        var needed = headerH + Math.Max(leftRows, rightRows) + sectionGap + footer;
        var maxH = page.Height - UiMetrics.Space(12) * 2;
        var h = Math.Clamp(needed, SetDesignHeight, maxH);
        var f = new SettingsFrame
        {
            Panel = new Rectangle(UiKit.PageCenterX - width / 2, page.Y + (page.Height - h) / 2, width, h),
            ColW = colW, Rule = rule, TogglePitch = togglePitch,
            DangerTitleY = UiMetrics.Space(12), DangerTextY = dangerTextY,
            ContentHeight = Math.Max(leftRows, rightRows),
        };
        f.Close = UiKit.CloseRect(f.Panel);
        f.Top = f.Panel.Y + headerH;
        f.LeftX = f.Panel.X + SetInset;
        f.RightX = f.LeftX + colW + colGap;
        f.Quit = new Rectangle(f.LeftX, f.Panel.Bottom - padBottom - button, wide, button);
        var copyY = f.Quit.Y - UiMetrics.Space(10) - captionPitch - UiMetrics.Space(8) - button;
        f.KeysNoteY = copyY - UiMetrics.Space(8) - captionPitch;
        var dangerY = f.Panel.Bottom - padBottom - dangerH;
        var footerTop = Math.Min(f.KeysNoteY, dangerY);
        f.View = new Rectangle(f.Panel.X + SetInset / 2, f.Top, f.Panel.Width - SetInset, footerTop - UiMetrics.Space(8) - f.Top);
        var scrolls = f.ContentHeight > f.View.Height;
        f.Scrolls = scrolls;
        // In scroll mode the right column gives up a lane for the scrollbar.
        f.RightW = scrolls ? colW - UiMetrics.ScrollbarWidth - UiMetrics.Space(8) : colW;

        var t = f.Top;
        f.DisplayY = t;
        f.ModeRow = new Rectangle(f.LeftX + labelW, t + modeY, colW - labelW, fieldH);
        f.SizeRow = new Rectangle(f.LeftX + labelW, t + sizeY, colW - labelW, fieldH);
        f.ScaleRow = new Rectangle(f.LeftX + labelW, t + scaleY, colW - labelW, fieldH);
        f.ScaleCaptionY = t + scaleCapY;
        f.AudioY = t + audioY;
        f.FxTrack = new Rectangle(f.LeftX + labelW, t + fxRowY + UiMetrics.Space(20), trackW, trackH);
        f.MusicTrack = new Rectangle(f.LeftX + labelW, f.FxTrack.Y + sliderPitch, trackW, trackH);

        f.GameplayY = t;
        var askW = UiMetrics.Control(260);
        f.AskBtn = new Rectangle(f.RightX + f.RightW - askW, t + askY, askW, button);
        f.AskLabelY = f.AskBtn.Y + (button - body) / 2;
        f.AccessY = t + accessY;
        f.ToggleY = t + toggleY;
        f.AccessCaptionY = t + accessCapY;

        // The bottom blocks: pinned to the panel's foot at every profile (see above).
        f.CopyFeedback = new Rectangle(f.LeftX, copyY, wide, button);
        // THE DANGER ZONE (brief §37): its own bordered region, overhanging the column by a breath, so
        // the button that deletes a save is not one of a pair of identical buttons.
        var overhang = UiMetrics.Space(24);
        f.Danger = new Rectangle(f.RightX - overhang, dangerY, f.RightW + overhang, dangerH);
        f.NewGame = new Rectangle(f.Danger.Right - overhang - wide, f.Danger.Y + dangerBtnY, wide, button);
        var armedPad = UiMetrics.Space(16);
        f.NewGameArmed = new Rectangle(f.Danger.X + armedPad, f.Danger.Y + dangerBtnY, f.Danger.Width - armedPad * 2, button);
        return f;
    }

    // The frame's rectangles, as static properties — unscrolled — so the page-layout test can check
    // every one of them at every profile. The draw reads the frame once per frame instead.
    private static Rectangle SettingsPanel => SettingsFrameNow().Panel;
    /// <summary>The panel's corner close icon — UiKit.CloseButton at UiKit.CloseRect, inside the ornament.</summary>
    private static Rectangle SettingsCornerClose => SettingsFrameNow().Close;
    /// <summary>The closed MODE dropdown row — shows the current mode, opens the list on click.</summary>
    private static Rectangle SettingsModeRow => SettingsFrameNow().ModeRow;
    /// <summary>The closed WINDOW SIZE dropdown row. Dim and inert outside WINDOWED mode.</summary>
    private static Rectangle SettingsSizeRow => SettingsFrameNow().SizeRow;
    /// <summary>The UI SCALE row — a button that steps through the offered scales. Applies at once.</summary>
    private static Rectangle SettingsScaleRow => SettingsFrameNow().ScaleRow;
    /// <summary>The effects-volume slider track (the grab area is padded around it).</summary>
    private static Rectangle SettingsFxTrack => SettingsFrameNow().FxTrack;
    /// <summary>The music-volume slider track.</summary>
    private static Rectangle SettingsMusicTrack => SettingsFrameNow().MusicTrack;
    // CLOSE sits on the RIGHT, where every other panel in the game puts its way out (playtest
    // 2026-08-25: "close is left behind on the left"); the exit to the desktop sits left of it.
    // The CLOSE button is gone (playtest 2026-08-26: "remove the CLOSE text; the corner icon closes");
    // QUIT TO DESKTOP is the panel's footer — anchored, never under the scroll.
    private static Rectangle SettingsQuit => SettingsFrameNow().Quit;
    /// <summary>Copies the feedback code (build stamp + progress + run log) to the clipboard.</summary>
    private static Rectangle SettingsCopyFeedback => SettingsFrameNow().CopyFeedback;
    private static Rectangle SettingsDanger => SettingsFrameNow().Danger;
    /// <summary>START A NEW GAME at rest, and its wider red armed state spanning the zone.</summary>
    private static Rectangle SettingsNewGame => SettingsFrameNow().NewGame;
    private static Rectangle SettingsNewGameArmed => SettingsFrameNow().NewGameArmed;

    /// <summary>How far the settings rows are scrolled, in page pixels. Only ever non-zero when they outgrow the page.</summary>
    private int _settingsScroll;

    /// <summary>How far the help sheet is scrolled, in page pixels. Only ever non-zero when it outgrows the page.</summary>
    private int _helpScroll;

    /// <summary>The rasterizer that honours the scissor rectangle — one instance, made on first use.</summary>
    private RasterizerState? _clipState;
    private RasterizerState ClipState => _clipState ??= new RasterizerState { ScissorTestEnable = true };

    /// <summary>
    /// Reopen the chrome batch clipped to <paramref name="view"/> — canvas pixels, which in the chrome
    /// batch are page pixels. Pair with <see cref="EndChromeClip"/>. For the modals whose rows can
    /// outgrow the page at 150 % (settings, help): what scrolls under a fixed header must stop at it.
    /// </summary>
    private void BeginChromeClip(Rectangle view)
    {
        _batch.End();
        GraphicsDevice.ScissorRectangle = Rectangle.Intersect(view, new Rectangle(0, 0, CanvasWidth * ArtScale, CanvasHeight * ArtScale));
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, ClipState, null, Matrix.Identity);
    }

    /// <summary>Close the clipped batch and reopen the plain chrome batch.</summary>
    private void EndChromeClip()
    {
        _batch.End();
        BeginCanvas(1);
    }

    /// <summary>Which settings dropdown is open: 0 none, 1 MODE, 2 WINDOW SIZE. One at a time.</summary>
    private int _settingsDropdown;

    /// <summary>The highlighted option in the open list — the keyboard's cursor and the mouse's hover.</summary>
    /// <remarks>
    /// One field for both, deliberately: a hovered row SETS it, so a player who reaches for the arrow
    /// keys after moving the mouse carries on from where they were looking rather than from somewhere
    /// else. -1 means "not placed yet"; the list places it on the current value the first frame it draws.
    /// </remarks>
    private int _dropCursor = -1;

    /// <summary>Arrow-key movement banked in Update for the list to apply in Draw.</summary>
    /// <remarks>
    /// Keys are edge-detected against the previous frame, and <c>_prevKeys</c> is latched at the END of
    /// Update — so a key read in Draw is never down. The list's OPTIONS, on the other hand, only exist
    /// in Draw. So Update banks a delta and Draw, which knows how many rows there are, wraps it.
    /// </remarks>
    private int _dropMove;

    /// <summary>Enter, pressed on an open list: take whatever the cursor is on.</summary>
    private bool _dropCommit;

    /// <summary>First visible row when a list is taller than the room under it.</summary>
    private int _dropScroll;

    /// <summary>
    /// True on a frame where Escape already closed a dropdown list, so it must not ALSO close the panel.
    /// </summary>
    /// <remarks>
    /// Escape peels one layer at a time — the list, then the panel — which is the reflex every player
    /// has. Without this flag the single edge fires at both handlers and one press does both.
    /// </remarks>
    private bool _settingsEscSpent;

    /// <summary>Which volume slider is mid-drag: 0 none, 1 effects, 2 music.</summary>
    private int _dragSlider;

    /// <summary>The settings row the cursor is resting on — the hover-tip target and its clock.</summary>
    private string _tipKey = "";
    private float _tipTimer;

    /// <summary>The settings row the pending tip explains — the plate hangs off it, never the cursor.</summary>
    private Rectangle _tipZone;

    /// <summary>Seconds a row must be hovered before its explanation appears.</summary>
    private const float TipDelaySeconds = 0.35f;

    // ── DROPDOWN GEOMETRY ─────────────────────────────────────────────────────────────────────
    // One set of numbers, so MODE and WINDOW SIZE are the same control and a third dropdown added
    // later is too. The 2026-08-28 note — "the dropdown menus look very low quality" — was about the
    // OPEN list: a flat #161022 rectangle with a gold line on top, rows of centred text, no frame, no
    // separators, no mark on the value you already had. It read as a debug box on a hand-drawn panel.

    /// <summary>Height of one option row in an open settings dropdown — the house list row.</summary>
    private static int DropRowHeight => UiMetrics.RowHeight;

    /// <summary>The gap between the closed field and the list that drops under it.</summary>
    private static int DropGap => UiMetrics.Space(4);

    /// <summary>How far the list's ornate frame reaches in. Rows live inside this margin on every side.</summary>
    private const int DropListInset = UiKit.PanelCorner;

    /// <summary>Text inset from the edge of a row, and from the closed field's end ornament.</summary>
    private static int DropPadX => UiTypography.ButtonPadX;

    /// <summary>The size an option row and a closed field's value are set at — a control's own rung.</summary>
    private static int DropTextPx => UiTypography.ButtonText;

    /// <summary>The cursor row's fill. Warm and QUIET — the gold text on it is the signal, not this.</summary>
    /// <remarks>
    /// The panel behind is nearly black, so a mid-tone here reads far hotter on screen than it does as
    /// a hex triple: the first pass used #4A3318 and it was the loudest thing on the settings panel —
    /// a highlight shouting over the value it was meant to point at.
    /// </remarks>
    private static readonly Color DropRowHover = new(0x2E, 0x22, 0x12);

    /// <summary>The gold edge down the left of the cursor row — the unambiguous "you are here".</summary>
    private static readonly Color DropRowEdge = new(0xC8, 0x9A, 0x3C);

    /// <summary>The hairline between rows. One pixel, warm, just above the frame's inner shadow.</summary>
    private static readonly Color DropRowRule = new(0x46, 0x3C, 0x30);

    /// <summary>
    /// The well painted inside a field before its frame art.
    /// </summary>
    /// <remarks>
    /// Near-black ON PURPOSE, matching the interior of <c>ui_button_secondary</c>. The well exists only
    /// because the lit art (<c>ui_button_primary</c>) is transparent inside; any tone brighter than the
    /// resting art makes the field visibly change colour the moment it opens, which reads as two
    /// different controls rather than one control in two states.
    /// </remarks>
    private static readonly Color DropFieldWell = new(0x0B, 0x09, 0x0E);

    /// <summary>The retired ladder's closed rungs, by its step names. Persisted, read by nothing.</summary>
    /// <remarks>
    /// The <c>TutorialStep</c> enum that named them is deleted: lessons are
    /// <c>OnboardingLessonId</c> values now, and a lesson is completed by a fact the player
    /// produced, never by a card they closed. The names are loaded and written back untouched so
    /// that rolling this build back does not lose what an old player had already closed.
    /// </remarks>
    private readonly HashSet<string> _dismissedGuide = new();

    /// <summary>How long the armed are-you-sure state stays live before disarming itself.</summary>
    private const float ResetArmSeconds = 4f;

    /// <summary>The persistent way back here — a gear in the corner, the convention every game teaches.</summary>
    // Level with the currency pills (their capsules run y 22..70, centre 46): the gear used to sit at
    // y 8..68, centre 38 — eight pixels above the row it belongs to (playtest 2026-08-26: "it sits a
    // little higher and catches the eye").
    // A page anchor: the pills' capsule is a fixed 60 (UiKit.Pill), so the gear's slot is too.
    private static Rectangle SettingsGear => new(UiKit.PageRight(78 - 60) - UiMetrics.Control(60), 16, UiMetrics.Control(60), UiMetrics.Control(60));

    /// <summary>
    /// LEARN THIS SCREEN — the ? beside the gear, and the only way a screen tour starts now.
    /// </summary>
    /// <remarks>
    /// The tours used to fire themselves the first time a screen opened: four cards before the player
    /// could equip anything, four more before they could upgrade anything, eight on the Hunt before
    /// the first wave. The copy in them is good and was corrected recently, so none of it was thrown
    /// away — it moved BEHIND this button, where somebody who wants a screen explained can ask, and
    /// nobody else is stopped. One control rather than a per-screen affordance, because it has to sit
    /// in the same place on all eleven screens for anybody to learn it is there.
    /// </remarks>
    private static Rectangle LearnButton
        => new(LearnButtonLeft, PillRowTop + (PillHeight - LearnButtonSize) / 2, LearnButtonSize, LearnButtonSize);

    /// <summary>
    /// The ?'s edge — a small button, deliberately.
    /// </summary>
    /// <remarks>
    /// Every pixel it takes comes off the currency row, which at 150 % is already three ornate capsules
    /// wide and ends a hand's breadth from MASTERY TREE, the longest screen title in the game. At the
    /// standard control height the row crossed into it. It is exactly
    /// <see cref="UiMetrics.HitTargetMinimum"/> — the smallest edge an interactive target may have, at
    /// every profile — and not one pixel less.
    /// </remarks>
    private static int LearnButtonSize => UiMetrics.HitTargetMinimum;

    /// <summary>
    /// The ?'s left edge, and the wall the currency row stops at.
    /// </summary>
    /// <remarks>
    /// THE CHROME ROW IS A CHAIN, and a control added to it has to take its room rather than sit on
    /// top of the neighbour: the first cut of this button was hung off the gear at the same y as the
    /// capsules, which put a ? through the MATERIALS pill on every screen. Both the button and
    /// <see cref="PillRowRight"/> derive from this one number, so the two cannot drift apart, and the
    /// room is reserved even on a screen with no tour behind the button — a row of capsules that
    /// shifted sideways when the ? appeared would be worse than the gap.
    /// </remarks>
    private static int LearnButtonLeft => SettingsGear.X - UiMetrics.Space(8) - LearnButtonSize;

    /// <summary>
    /// DISPATCHES — the sealed envelope beside the ?, and the third link in the chrome's chain.
    /// </summary>
    /// <remarks>
    /// Not a rail tile: dispatches are not a place to go, they are news that has already happened, and
    /// the rail's eleven tiles are the eleven screens. The chrome is where the things that belong to no
    /// screen live — the gear, the ?, and now the letters — and the chrome is on every screen, which is
    /// the only way a mark meaning "something is waiting" can be seen wherever the player is standing.
    /// <para>
    /// <c>internal</c> so the coach and the tour can light it (the pattern <see cref="GleamPillRect"/>
    /// set), and so <c>page_layout_test</c> walks it at every profile.
    /// </para>
    /// </remarks>
    internal static Rectangle DispatchButton
        => new(DispatchButtonLeft, PillRowTop + (PillHeight - DispatchButtonSize) / 2, DispatchButtonSize, DispatchButtonSize);

    /// <summary>The envelope's edge — the ?'s size, for the ?'s reason: every pixel comes off the currency row.</summary>
    private static int DispatchButtonSize => UiMetrics.HitTargetMinimum;

    /// <summary>
    /// The envelope's left edge, and the wall the currency row stops at now.
    /// </summary>
    /// <remarks>
    /// THE CHAIN AGAIN (see <see cref="LearnButtonLeft"/>): a third control in this row has to take its
    /// room rather than sit on top of the ?, so it hangs off the ? exactly as the ? hangs off the gear,
    /// and <see cref="PillRowRight"/> derives from THIS number instead. Right to left the row now reads
    /// gear, ?, envelope, capsules, and the room is reserved on every screen — a row of capsules that
    /// shifted sideways when the letters arrived would be worse than the gap.
    /// </remarks>
    private static int DispatchButtonLeft => LearnButtonLeft - UiMetrics.Space(8) - DispatchButtonSize;

    /// <summary>
    /// Display options, drawn as an overlay over whatever is behind it.
    /// </summary>
    /// <remarks>
    /// Drawn (not updated) because <see cref="UiKit.Button"/> hit-tests as it renders — the click edge
    /// is latched in Update precisely so Draw can read it. MODE and WINDOW SIZE are dropdowns: the
    /// closed row shows the current value, the open option list is drawn LAST in this method so it
    /// wins the z-order, and while it is open every other row's click is disabled (see uiClick), so a
    /// click meant for the list can never press what sits underneath it. WINDOW SIZE is offered only
    /// in windowed mode: in borderless/fullscreen the scale is the monitor's to decide, and the row
    /// dims rather than vanishing so the panel doesn't reflow under the cursor. Every row grows a
    /// hover explanation after a short delay — see <see cref="DrawSettingsTips"/>.
    /// </remarks>
    // ── THE SETTINGS PANEL'S INPUT, IN UPDATE ────────────────────────────────────────────────────
    //
    // Every control on this panel used to be decided inside DrawSettings, on the raw click edge, and
    // its own doc comment said why: "Drawn (not updated) because UiKit.Button hit-tests as it renders
    // — the click edge is latched in Update precisely so Draw can read it." That reasoning holds only
    // for a tick that updates once. MonoGame's fixed timestep makes AT LEAST ONE Update and exactly
    // one Draw, so a frame over budget runs Update twice: the first latches the edge, Latch() copies
    // _mouse into _prevMouse, the second recomputes the edge as FALSE, and the single Draw that
    // follows tests an edge that no longer exists. A press held three to six frames never re-arms, so
    // the press is simply lost. The title screen shipped with exactly that bug.
    //
    // WHAT KEEPS THE TWO HALVES HONEST is that they read the SAME geometry: SettingsFrameNow() was
    // already a pure static layout of every rectangle on the panel, and the only thing added to it
    // here is the scroll offset (SettingsRow) and the three control rects that used to be computed
    // inside the draw-and-decide helpers (SettingsToggleButton, SettingsSliderGrab,
    // SettingsDropdownRow). Nothing is a literal, and nothing is measured twice.

    /// <summary>A panel row at the current scroll — the one offset both halves apply.</summary>
    private Rectangle SettingsRow(Rectangle r) => new(r.X, r.Y - _settingsScroll, r.Width, r.Height);

    /// <summary>An ON/OFF row's button: the right end of the row's width. Shared with <see cref="DrawToggleRow"/>.</summary>
    private static Rectangle SettingsToggleButton(int x, int y, int width)
    {
        var w = UiMetrics.Control(116);
        return new Rectangle(x + width - w, y, w, UiMetrics.RowHeight);
    }

    /// <summary>A slider's grab zone — the track, generously padded. Shared with <see cref="DrawSliderRow"/>.</summary>
    private static Rectangle SettingsSliderGrab(Rectangle track)
    {
        var gx = UiMetrics.Space(10);
        var gy = UiMetrics.Space(6);
        return new Rectangle(track.X - gx, track.Y - gy, track.Width + gx * 2, track.Height + gy * 2);
    }

    /// <summary>The k-th accessibility switch's row — the hover zone and the row the button sits in.</summary>
    private Rectangle SettingsToggleZone(SettingsFrame f, int k)
        => SettingsRow(new Rectangle(f.RightX, f.ToggleY + k * f.TogglePitch, f.RightW, UiMetrics.RowHeight));

    /// <summary>
    /// ONE SWITCH IN THE ACCESSIBILITY COLUMN: what it says, what it reads, and what it writes.
    /// </summary>
    /// <remarks>
    /// A table rather than six hand-written pairs, because Update and Draw both walk it and six
    /// duplicated label/field pairings is six chances for the two halves to disagree about which row
    /// is which. <c>Pref</c> marks the four that are DISPLAY preferences and are persisted together by
    /// one <c>SaveDisplay()</c> after the walk, exactly as the old <c>changed |= …</c> did; GUIDANCE
    /// writes the SAVE instead and does its own work.
    /// </remarks>
    private readonly record struct SettingsSwitch(string Label, Func<bool> On, Action<bool> Set, bool Pref);

    /// <summary>The six switches, in the order they are stacked.</summary>
    private SettingsSwitch[] SettingsSwitches() => new[]
    {
        new SettingsSwitch("DAMAGE NUMBERS", () => _showDamageNumbers, v => _showDamageNumbers = v, true),
        new SettingsSwitch("SKILL NAMES", () => _showSkillCallouts, v => _showSkillCallouts = v, true),
        new SettingsSwitch("FIGHT EFFECTS", () => _showHitEffects, v => _showHitEffects = v, true),
        new SettingsSwitch("RED FLASH", () => _showScreenFlash, v => _showScreenFlash = v, true),
        // EVERY SWITCH IN THIS COLUMN POINTS THE SAME WAY: ON grants the thing. The stored preference
        // is still REDUCED MOTION (DisplaySettings persists motion=0|1 and every screen reads
        // ReducedMotion) — inverting a saved value would silently flip the preference of every player
        // who has already set one. Only the label and its sense are inverted, here, once.
        new SettingsSwitch("INTERFACE MOTION", () => !ReducedMotion, v => ReducedMotion = !v, true),
        // GUIDANCE is one switch on purpose, and while the authored opening runs it is also SKIP
        // TUTORIAL. Turning it off ends the opening where the player stands and does nothing else: no
        // item granted, no unlock moved, no boss credited.
        new SettingsSwitch(_opening.Running ? "GUIDANCE — SKIP TUTORIAL" : "GUIDANCE",
                           () => !_guidanceOff,
                           v =>
                           {
                               _guidanceOff = !v;
                               if (_guidanceOff && _opening.Running) { _opening.SkipToEnd(); _openingWas = _opening.Stage; }
                               Save();
                           },
                           false),
    };

    /// <summary>The three display modes, in the order the MODE list offers them.</summary>
    private static readonly DisplayMode[] SettingsModes =
        { DisplayMode.Windowed, DisplayMode.Borderless, DisplayMode.Fullscreen };

    /// <summary>MODE's list, and which row is current.</summary>
    private (string[] Names, int Index) SettingsModeList()
        => (new[] { "WINDOWED", "BORDERLESS", "FULLSCREEN" }, Array.IndexOf(SettingsModes, _displayMode));

    /// <summary>WINDOW SIZE's offered sizes, their labels, and which is current.</summary>
    private (WindowSize[] Sizes, string[] Labels, int Index) SettingsSizeList()
    {
        var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var sizes = Display.OfferedWindowSizes(desktop.Width, desktop.Height);
        var labels = new string[sizes.Length];
        for (var i = 0; i < labels.Length; i++)
            labels[i] = Display.WindowSizeLabel(sizes[i], desktop.Width, desktop.Height);
        return (sizes, labels, Array.IndexOf(sizes, _windowSize));
    }

    /// <summary>
    /// An open dropdown's box and how many of its options fit — the geometry, with nothing decided.
    /// </summary>
    /// <remarks>
    /// Pure: <see cref="DropdownList"/> used to compute this while painting AND pick from it. Both
    /// halves read it now, so the row the player clicks is the row they saw.
    /// </remarks>
    private static (Rectangle List, int Rows) SettingsDropdownBox(Rectangle field, int options)
    {
        var top = field.Bottom + DropGap;
        var room = SettingsPanel.Bottom - UiKit.PanelCorner - top - DropListInset * 2;
        var rows = Math.Clamp(room / DropRowHeight, 1, Math.Max(1, options));
        return (new Rectangle(field.X, top, field.Width, rows * DropRowHeight + DropListInset * 2), rows);
    }

    /// <summary>The k-th visible row of an open dropdown list.</summary>
    private static Rectangle SettingsDropdownRow(Rectangle list, int rows, int k, bool scrolling)
    {
        var inner = new Rectangle(list.X + DropListInset, list.Y + DropListInset,
                                  list.Width - DropListInset * 2, rows * DropRowHeight);
        var rowW = inner.Width - (scrolling ? UiMetrics.ScrollbarWidth + UiMetrics.Space(6) : 0);
        return new Rectangle(inner.X, inner.Y + k * DropRowHeight, rowW, DropRowHeight);
    }

    /// <summary>
    /// THE SETTINGS PANEL'S WHOLE INPUT PASS. Runs from Update while the panel is up; Draw paints the
    /// result and decides nothing.
    /// </summary>
    /// <remarks>
    /// Deliberately reads the RAW <c>_clicked</c> rather than <see cref="MouseClicked"/>, as the draw
    /// half did, because this panel IS the modal that gate exists for. The one thing it must not act on
    /// is the edge that OPENED it — see <see cref="_modalOpenedNow"/>.
    /// </remarks>
    private void UpdateSettings()
    {
        var f = SettingsFrameNow();
        var mouse = ChromeMouse;
        // A click reaches the ordinary rows only while no dropdown list is open: the open list is drawn
        // over them, so its clicks — and the click that closes it — are swallowed here. And never the
        // edge that opened the panel.
        var uiClick = _clicked && _settingsDropdown == 0 && !_modalOpenedNow;

        // ── THE SCROLL, before anything is hit-tested against it. ───────────────────────────────
        var maxScroll = f.Scrolls ? Math.Max(0, f.ContentHeight - f.View.Height) : 0;
        if (maxScroll > 0 && _settingsDropdown == 0 && MouseWheel != 0 && f.View.Contains(mouse))
            _settingsScroll -= MouseWheel * UiMetrics.RowHeight;
        // DEV: pose the panel scrolled (RH_SHOT_SCROLL=<rows>|end) so the two rows that sit under the
        // clip line at 150 % can be photographed at all.
        if (RigActive && maxScroll > 0
            && Environment.GetEnvironmentVariable("RH_SHOT_SCROLL") is { Length: > 0 } posed)
            _settingsScroll = posed.Equals("end", StringComparison.OrdinalIgnoreCase) ? maxScroll
                : int.TryParse(posed, out var rows) ? rows * UiMetrics.RowHeight
                : _settingsScroll;
        _settingsScroll = Math.Clamp(_settingsScroll, 0, maxScroll);

        var rowsVisible = !f.Scrolls || f.View.Contains(mouse);
        var rowClick = uiClick && rowsVisible;

        // ── DISPLAY ────────────────────────────────────────────────────────────────────────────
        var modeRow = SettingsRow(f.ModeRow);
        var sizeRow = SettingsRow(f.SizeRow);
        var windowed = _displayMode == DisplayMode.Windowed;

        // UI SCALE applies AT ONCE, so the next frame is laid out at the new profile. The edge is spent
        // here and the rows below are tested against the geometry it just replaced — which is why this
        // returns rather than falling on through: at the new profile every rectangle under the cursor
        // is a different control, and one press must never press two.
        if (UiKit.ClickedIn(SettingsRow(f.ScaleRow), mouse, rowClick))
        {
            CycleUiScale();
            SaveDisplay();
            return;
        }

        // ── AUDIO. Click the track to jump the handle, hold to drag it; persist once, on release. ──
        var fxTrack = SettingsRow(f.FxTrack);
        var musicTrack = SettingsRow(f.MusicTrack);
        if (rowClick && SettingsSliderGrab(fxTrack).Contains(mouse)) _dragSlider = 1;
        if (rowClick && SettingsSliderGrab(musicTrack).Contains(mouse)) _dragSlider = 2;
        if (_mouse.LeftButton == ButtonState.Pressed)
        {
            if (_dragSlider == 1 && SliderValueAt(fxTrack, mouse) is { } fx && fx != _sfxVolume)
            {
                _sfxVolume = fx;
                _sound.SfxVolume = _sfxVolume / 100f;   // live, so the drag previews the new level
            }
            else if (_dragSlider == 2 && SliderValueAt(musicTrack, mouse) is { } mu && mu != _musicVolume)
            {
                _musicVolume = mu;
                _sound.MusicVolume = _musicVolume / 100f;   // the playing bed follows the drag instantly
            }
        }
        // The drag ends when the button does. One write per drag, not sixty.
        if (_dragSlider != 0 && _mouse.LeftButton == ButtonState.Released)
        {
            if (_dragSlider == 1) _sound.PlayFirst(1f, "sfx_click", "sfx_forge");
            _dragSlider = 0;
            SaveDisplay();
        }

        // ── GAMEPLAY ───────────────────────────────────────────────────────────────────────────
        if (UiKit.ClickedIn(SettingsRow(f.AskBtn), mouse, rowClick))
        {
            _askBeforeScrap = !_askBeforeScrap;
            _forge.AskBeforeScrap = _askBeforeScrap;
            SaveDisplay();
        }

        // ── ACCESSIBILITY, and GUIDANCE under it: one walk of the same table Draw walks. ────────
        var switches = SettingsSwitches();
        var changed = false;
        for (var k = 0; k < switches.Length; k++)
        {
            var zone = SettingsToggleZone(f, k);
            if (!UiKit.ClickedIn(SettingsToggleButton(zone.X, zone.Y, zone.Width), mouse, rowClick)) continue;
            switches[k].Set(!switches[k].On());
            changed |= switches[k].Pref;
            break;   // the switches do not overlap; one edge, one switch
        }
        if (changed) SaveDisplay();

        // ── THE FOOTER: outside the clip, so it is always on the panel. ─────────────────────────
        if (_resetArmTimer <= 0f && UiKit.ClickedIn(f.CopyFeedback, mouse, uiClick))
        {
            // The failure is NOT silent (the weave's copy button's rule): no clipboard, no lie.
            _feedbackToast = ClipboardInterop.TrySet(FeedbackCode())
                ? "COPIED — PASTE IT TO THE DEVELOPER"
                : "COPY FAILED — TRY AGAIN";
            _feedbackToastTimer = 4f;
        }

        // DANGER ZONE. Armed, the confirmation is the only click that goes through; any other click
        // disarms it.
        if (_resetArmTimer > 0f)
        {
            if (UiKit.ClickedIn(f.NewGameArmed, mouse, uiClick))
            {
                _resetArmTimer = 0f;
                _wantsNewGame = true;
            }
            else if (_clicked && !_modalOpenedNow)
            {
                _resetArmTimer = 0f;
            }
        }
        else if (UiKit.ClickedIn(f.NewGame, mouse, uiClick))
        {
            _resetArmTimer = ResetArmSeconds;
        }

        if (UiKit.ClickedIn(f.Close, mouse, uiClick))
        {
            _showSettings = false;
            _settingsDropdown = 0;
            return;
        }

        // Esc opens this panel rather than quitting, so the game needs a door that says what it does.
        // Hidden on the title, whose own menu has QUIT and whose Hunter may not exist yet to save.
        if (!_showTitle && UiKit.ClickedIn(f.Quit, mouse, uiClick))
        {
            Save();
            Exit();
            return;
        }

        // ── THE OPEN DROPDOWN, last: it is drawn over every row below it, and `uiClick` above has
        //    already kept those rows from seeing this edge. ───────────────────────────────────────
        if (_settingsDropdown == 1)
        {
            var (names, index) = SettingsModeList();
            if (TakeDropdownInput(modeRow, names.Length, index) is { } pick && pick != index)
            {
                _displayMode = SettingsModes[pick];
                ApplyDisplay();
                SaveDisplay();
            }
        }
        else if (_settingsDropdown == 2)
        {
            var (sizes, labels, index) = SettingsSizeList();
            if (TakeDropdownInput(sizeRow, labels.Length, index) is { } pick && pick != index
                && pick >= 0 && pick < sizes.Length)
            {
                _windowSize = sizes[pick];
                ApplyDisplay();
                SaveDisplay();
            }
        }
        else if (_clicked && rowsVisible && !_modalOpenedNow)
        {
            // No list open: a click on a closed row opens its list, with the cursor unplaced so the
            // list puts it on the value the player already has.
            if (modeRow.Contains(mouse)) OpenDropdown(1);
            else if (windowed && sizeRow.Contains(mouse)) OpenDropdown(2);
        }
    }

    /// <summary>Where on a track a cursor sits, 0..100 — the slider's one reading, shared by both halves.</summary>
    private static int? SliderValueAt(Rectangle track, Point mouse)
        => track.Width <= 0 ? null : Math.Clamp((int)MathF.Round((mouse.X - track.X) * 100f / track.Width), 0, 100);

    /// <summary>
    /// AN OPEN DROPDOWN'S INPUT: the arrow cursor, the wheel, and the pick. Returns the chosen index
    /// when the list committed this frame, and closes the list; null otherwise.
    /// </summary>
    /// <remarks>
    /// The cursor and the scroll are state the LIST owns, so they are settled here before Draw reads
    /// them — which is also why the list's own scroll clamp moved out of the paint.
    /// </remarks>
    private int? TakeDropdownInput(Rectangle field, int options, int currentIndex)
    {
        if (options <= 0) { CloseDropdown(); return null; }

        // The cursor lands on the current value the first frame the list is up.
        if (_dropCursor < 0 || _dropCursor >= options) _dropCursor = Math.Max(0, currentIndex);
        if (_dropMove != 0)
        {
            _dropCursor = ((_dropCursor + _dropMove) % options + options) % options;
            _dropMove = 0;
        }

        var (list, rows) = SettingsDropdownBox(field, options);
        if (rows < options && MouseWheel != 0) _dropScroll -= MouseWheel;
        _dropScroll = Math.Clamp(_dropScroll, 0, Math.Max(0, options - rows));
        if (_dropCursor < _dropScroll) _dropScroll = _dropCursor;
        else if (_dropCursor >= _dropScroll + rows) _dropScroll = _dropCursor - rows + 1;

        // ENTER commits whatever the arrow keys have lit (the edge is taken in Update, above).
        if (_dropCommit)
        {
            _dropCommit = false;
            var chosen = _dropCursor;
            CloseDropdown();
            return chosen;
        }

        var scrolling = rows < options;
        var mouse = ChromeMouse;
        for (var k = 0; k < rows; k++)
        {
            var r = SettingsDropdownRow(list, rows, k, scrolling);
            if (r.Contains(mouse)) _dropCursor = _dropScroll + k;   // the mouse and the keys share one cursor
            if (!_clicked || _modalOpenedNow || !r.Contains(mouse)) continue;
            var picked = _dropScroll + k;
            CloseDropdown();
            return picked;
        }

        // A click anywhere else — the closed field included — just closes the list.
        if (_clicked && !_modalOpenedNow) CloseDropdown();
        return null;
    }

    private void DrawSettings()
    {
        var f = SettingsFrameNow();
        var panel = f.Panel;
        _ui.Scrim(_batch, 0.75f);
        _ui.Panel(_batch, panel);
        _ui.TextCenterBig(_batch, "SETTINGS", panel.Center.X, panel.Y + UiTypography.ModalTitleTop,
                          Gold, UiTypography.PanelTitle);

        // NOTHING ON THIS PANEL IS DECIDED HERE. Every control's press is taken in UpdateSettings,
        // against the same SettingsFrameNow() rectangles this paints — see that method for why a click
        // read from Draw is lost on a catch-up tick. These two constants keep the drawn widgets in
        // their hover-and-press-face-only form, which is what `clicked: false` means to UiKit.Button.
        var mouse = ChromeMouse;

        // The COPIED confirmation, in the header row beside the close icon — the one line no row shares.
        if (_feedbackToastTimer > 0f)
            _ui.TextRightBig(_batch, _feedbackToast, f.Close.X - UiMetrics.Space(16),
                             panel.Y + UiTypography.ModalTitleTop + (UiTypography.PanelTitle - UiTypography.Secondary) / 2,
                             Gold, UiTypography.Secondary);

        // ── THE SCROLL. When the rows outgrow the room above the QUIT row (150 %), they scroll under
        //    the header. The wheel is taken in UpdateSettings, which also clamps and poses it
        //    (RH_SHOT_SCROLL); this reads the settled offset through the one shared helper.
        var dy = -_settingsScroll;
        Rectangle Row(Rectangle r) => SettingsRow(r);
        // Presentation only: the hover tips are suppressed while the cursor is outside the scrolled
        // view, so a tip never points at a row the clip has hidden.
        var rowsVisible = !f.Scrolls || f.View.Contains(mouse);

        // ── GROUPED BY PURPOSE, IN TWO COLUMNS (brief §36). Left: what the game looks and sounds
        //    like. Right: how it behaves, what it shows in a fight, and the one destructive door. ──
        void Group(string name, int x, int y, int w)
        {
            // A group head is a CATEGORY line — Secondary, as every inspector's is. Five gold heads
            // competed with the one gold title (chrome-10).
            _ui.TextBig(_batch, name, x, y, Slate, UiTypography.Secondary);
            _ui.Fill(_batch, new Rectangle(x, y + f.Rule, w, 1), UiInk.Rule);
        }

        void Label(string s, int x, int y, Color c) =>
            _ui.TextBig(_batch, s, x, y, c, UiTypography.Body);

        if (f.Scrolls) BeginChromeClip(f.View);

        // ── DISPLAY ──────────────────────────────────────────────────────────────────────────────
        Group("DISPLAY", f.LeftX, f.DisplayY + dy, f.ColW);

        var modeRow = Row(f.ModeRow);
        Label("MODE", f.LeftX, modeRow.Y + (modeRow.Height - UiTypography.Body) / 2, Bone);
        var modeNames = new[] { "WINDOWED", "BORDERLESS", "FULLSCREEN" };
        var modeIdx = _displayMode == DisplayMode.Windowed ? 0 : _displayMode == DisplayMode.Borderless ? 1 : 2;
        DropdownClosed(modeRow, modeNames[modeIdx], enabled: true, open: _settingsDropdown == 1);

        var windowed = _displayMode == DisplayMode.Windowed;
        var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var sizes = Display.OfferedWindowSizes(desktop.Width, desktop.Height);
        var sizeRow = Row(f.SizeRow);
        Label("WINDOW SIZE", f.LeftX, sizeRow.Y + (sizeRow.Height - UiTypography.Body) / 2, windowed ? Bone : Slate);
        // Outside WINDOWED the size is the monitor's to decide, so the row says which one rather than
        // going blank — "AUTO" was a word about the setting; this is the answer the player wanted.
        var sizeValue = windowed
            ? Display.WindowSizeLabel(_windowSize, desktop.Width, desktop.Height)
            : $"YOUR SCREEN — {desktop.Width} × {desktop.Height}";
        DropdownClosed(sizeRow, sizeValue, enabled: windowed, open: _settingsDropdown == 2);

        // UI SCALE, out of the dev keys and onto the panel (P0.5 built it; F8 was its only door). It
        // applies AT ONCE: the next frame is laid out at the new profile, this row included.
        var scaleRow = Row(f.ScaleRow);
        Label("UI SCALE", f.LeftX, scaleRow.Y + (scaleRow.Height - UiTypography.Body) / 2, Bone);
        _ui.Button(_batch, scaleRow, Display.UiScaleLabel(_uiScalePercent), mouse, clicked: false);
        _ui.TextBig(_batch, "AUTO PICKS 125% IN A SMALL WINDOW", f.LeftX, f.ScaleCaptionY + dy,
                    Slate, UiTypography.Secondary);

        // ── AUDIO ────────────────────────────────────────────────────────────────────────────────
        Group("AUDIO", f.LeftX, f.AudioY + dy, f.ColW);

        var fxTrack = Row(f.FxTrack);
        DrawSliderRow("EFFECTS VOLUME", fxTrack, _sfxVolume, f.LeftX);
        var musicTrack = Row(f.MusicTrack);
        DrawSliderRow("MUSIC VOLUME", musicTrack, _musicVolume, f.LeftX);

        // ── GAMEPLAY ─────────────────────────────────────────────────────────────────────────────
        Group("GAMEPLAY", f.RightX, f.GameplayY + dy, f.RightW);
        var askBtn = Row(f.AskBtn);
        // The label takes what the button leaves — it can never print through it.
        Label(_ui.ShortenBig("ASK BEFORE SELL OR SALVAGE", askBtn.X - UiMetrics.Space(12) - f.RightX, UiTypography.Body),
              f.RightX, f.AskLabelY + dy, Bone);
        _ui.Button(_batch, askBtn, _askBeforeScrap ? "ON — IT ASKS" : "OFF", mouse, clicked: false);

        // ── ACCESSIBILITY, and GUIDANCE under it. ────────────────────────────────────────────────
        //
        // Five switches then GUIDANCE, one per row, each the full column width — two to a row they
        // were 260 px apart and the second label sat under the first row's button. Walked from
        // SettingsSwitches(), the one table UpdateSettings also walks, so the row the player presses
        // and the row that is painted are the same row by construction. Each label's sense — why ON
        // means ON down the whole column, and why GUIDANCE is also SKIP TUTORIAL while the opening
        // runs — is explained on that table.
        Group("ACCESSIBILITY", f.RightX, f.AccessY + dy, f.RightW);
        var switches = SettingsSwitches();
        for (var k = 0; k < switches.Length; k++)
            DrawToggleRow(switches[k].Label, SettingsToggleZone(f, k), switches[k].On());
        _ui.TextBig(_batch,
                    _opening.Running
                        ? "GUIDANCE OFF ENDS THE TUTORIAL WHERE YOU STAND — IT UNLOCKS AND GRANTS NOTHING"
                        : "GUIDANCE OFF STOPS EVERY PROMPT — IT UNLOCKS AND GRANTS NOTHING",
                    f.RightX, f.AccessCaptionY + dy,
                    Slate, UiTypography.Secondary);

        if (f.Scrolls)
        {
            EndChromeClip();
            var lane = UiMetrics.ScrollbarWidth;
            _ui.ScrollBar(_batch, new Rectangle(panel.Right - SetInset - lane, f.View.Y, lane, f.View.Height),
                          _settingsScroll, f.View.Height, f.ContentHeight);
        }

        // ── THE FOOTER: the feedback code, and the one door that destroys something — pinned under
        //    the rows at every profile, outside the clip, so they are always on the panel (chrome-02). ──
        // The keys, in one line: only what the game really has, and where they are listed.
        _ui.TextBig(_batch, _ui.ShortenBig("EVERY KEY IS LISTED IN HELP (F1) · KEYS CANNOT BE REBOUND YET", f.ColW, UiTypography.Secondary),
                    f.LeftX, f.KeysNoteY, Slate, UiTypography.Secondary);
        var copyBtn = f.CopyFeedback;
        if (_resetArmTimer <= 0f) _ui.Button(_batch, copyBtn, "COPY FEEDBACK CODE", mouse, clicked: false);
        _ui.TextBig(_batch, "SENDS THE DEVELOPER YOUR BUILD AND PROGRESS", f.LeftX,
                    copyBtn.Bottom + UiMetrics.Space(8), Slate, UiTypography.Secondary);

        // DANGER ZONE — its own bordered region, so the button that deletes a save is not one of a
        // pair of identical buttons (brief §37).
        var danger = f.Danger;
        var newGame = f.NewGame;
        var armed = f.NewGameArmed;
        var dangerPad = UiMetrics.Space(16);
        _ui.Plate(_batch, danger, Ember);
        _ui.TextBig(_batch, "DANGER ZONE", danger.X + dangerPad, danger.Y + f.DangerTitleY, Ember, UiTypography.Secondary);
        if (_resetArmTimer > 0f)
        {
            _ui.Fill(_batch, armed, new Color(0x8C, 0x1E, 0x1E));
            _ui.Fill(_batch, new Rectangle(armed.X, armed.Y, armed.Width, 3), Ember);
            _ui.TextCenterBig(_batch, _ui.ShortenBig("SURE? THIS DELETES YOUR SAVE — CLICK AGAIN", armed.Width - dangerPad * 2, UiTypography.Body),
                              armed.Center.X, armed.Center.Y - UiTypography.Body * 27 / 40,
                              Color.White, UiTypography.Body);
        }
        else
        {
            _ui.TextBig(_batch, _ui.ShortenBig("DELETES THIS SAVE AND STARTS OVER. NOTHING COMES BACK.", danger.Width - dangerPad * 2, UiTypography.Body),
                        danger.X + dangerPad, danger.Y + f.DangerTextY, Bone, UiTypography.Body);
            _ui.Button(_batch, newGame, "START A NEW GAME", mouse, clicked: false);
        }

        _ui.CloseButton(_batch, f.Close, mouse, clicked: false);

        // Esc no longer quits (it opens THIS panel), so the game needs a door that says what it does.
        // Hidden on the title screen, whose own menu already has QUIT — and whose Hunter may not exist
        // yet to save. It is the game's exit — the door Esc used to be — and it still saves on the way out.
        if (!_showTitle) _ui.Button(_batch, f.Quit, "QUIT TO DESKTOP", mouse, clicked: false);

        // Which build this is — the same stamp the feedback code carries, so "which version are you
        // on" is answerable from a screenshot. Under the panel while the page has room for it; when the
        // panel has grown to the page, in the header row's left end — the one line no row shares,
        // mirroring the close icon. (Its first home up here was the footer's right end, which at 125 %
        // is where the DANGER ZONE's button sits when the rows do not scroll.)
        // In the header row's left end at EVERY profile, mirroring the close icon: under the panel it
        // was a line of text on the scrim, pasted on at 100 % and inside the frame at 150 % (chrome-13).
        var stamp = $"BUILD {BuildStamp.Short}";
        _ui.TextBig(_batch, stamp, f.LeftX, panel.Y + UiTypography.ModalTitleTop + (UiTypography.PanelTitle - UiTypography.Secondary) / 2,
                    Slate, UiTypography.Secondary);

        // ── The OPEN dropdown list, drawn last so it sits over every row below it. A click on an
        //    option applies and closes; any other click just closes — and either way the rows under
        //    the list never see it (uiClick above). ──
        var openList = Rectangle.Empty;
        if (_settingsDropdown == 1) openList = DrawDropdownList(modeRow, modeNames, modeIdx);
        else if (_settingsDropdown == 2)
        {
            var (_, labels, index) = SettingsSizeList();
            openList = DrawDropdownList(sizeRow, labels, index);
        }

        // ── HOVER EXPLANATIONS — one plain sentence per row, near the cursor, after a short rest.
        //    The zones ARE the rows just laid out (label and control), scrolled with them.
        Rectangle Zone(int x, Rectangle r, int w) => new(x, r.Y, w, r.Height);
        Rectangle SliderZone(Rectangle track) =>
            new(f.LeftX, track.Y - UiMetrics.Space(8), f.ColW, track.Height + UiMetrics.Space(16));
        Rectangle ToggleZone(int k) => new(f.RightX, f.ToggleY + dy + k * f.TogglePitch, f.RightW, UiMetrics.RowHeight);
        var zones = new (Rectangle Zone, string Key, string Tip)[]
        {
            (Zone(f.LeftX, modeRow, f.ColW), "mode",
                "How the game sits on your screen: in a window you can move, or filling the whole screen."),
            (Zone(f.LeftX, sizeRow, f.ColW), "size",
                windowed ? "How big the game window is. The picture is the same at every size — a bigger window just draws it bigger. Nothing larger than your screen is offered; (native) is your screen's own size."
                         : "How big the game window is. You can change this only when MODE is WINDOWED — the other two modes always fill your screen."),
            (SliderZone(fxTrack), "fx",
                "How loud the hits, clicks and other short sounds are. Drag the handle, or click a spot on the line."),
            (SliderZone(musicTrack), "music",
                "How loud the background music is. Drag the handle, or click a spot on the line."),
            (Zone(f.RightX, askBtn, f.RightW), "ask",
                "When this is on, the game asks you to confirm before an item is sold or broken down."),
            (ToggleZone(0), "dmg",
                "Shows the damage of every hit as a small number in the fight."),
            (ToggleZone(1), "skills",
                "Shows the name of each skill as your hunter uses it."),
            (ToggleZone(2), "hitfx",
                "Shows the flashes and sparks when hits land in the fight."),
            (ToggleZone(3), "flash",
                "When your hunter falls, the screen glows red for a moment. Turn this off if you do not want it."),
            (copyBtn, "feedback",
                "Copies a short code that describes your game. Paste it to the developer with your feedback."),
            (newGame, "newgame",
                "Deletes your save and starts over from the beginning. It asks you to confirm first."),
        };
        DrawSettingsTips(mouse, openList, rowsVisible, zones);
    }

    /// <summary>Open one of the settings dropdowns, with its cursor unplaced and its list at the top.</summary>
    private void OpenDropdown(int which)
    {
        _settingsDropdown = which;
        _dropCursor = -1;
        _dropScroll = 0;
        _dropMove = 0;
        _dropCommit = false;
    }

    /// <summary>Close whatever settings dropdown is open, and forget everything about it.</summary>
    private void CloseDropdown()
    {
        _settingsDropdown = 0;
        _dropCursor = -1;
        _dropScroll = 0;
        _dropMove = 0;
        _dropCommit = false;
    }

    /// <summary>
    /// The CLOSED dropdown box: a framed field with its value on the left and a chevron on the right.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to be an ornate PANEL frame with the value centred in it, which is the wrong two signals:
    /// a panel is a surface you read, and centred text is a title. A dropdown is a CONTROL holding a
    /// value, so it wears the button family's frame (<see cref="UiKit.Field"/>) and reads left to right
    /// like the field it is — value where the eye starts, the affordance parked on the right.
    /// </para>
    /// <para>
    /// The chevron is two drawn strokes, not a glyph: the font gate carries no caret, and a stack of
    /// 1-px fills — the old arrow — is a staircase at this size. It points DOWN when the list is shut
    /// and UP when it is open, so the box says what a click will do.
    /// </para>
    /// </remarks>
    private void DropdownClosed(Rectangle r, string value, bool enabled, bool open)
    {
        var hover = enabled && r.Contains(ChromeMouse);
        _ui.Field(_batch, r, enabled, hover || open, DropFieldWell);

        // The art's end ornaments are off limits to content — clear them, then pad inside that.
        var cap = UiKit.FieldCapWidth(r.Height);
        var textX = r.X + cap + DropPadX;
        var chevronRight = r.Right - cap - DropPadX;

        // A chevron: two strokes meeting at a point, 16 wide and 6 deep at 100 % — a glyph box, so it
        // grows with the controls. A solid stroke reads heavier than type of the same colour, so an
        // inert one is dimmed past the label it sits beside.
        var half = UiMetrics.Control(8);
        var depth = UiMetrics.Control(6);
        var ink = !enabled ? Slate : open ? Gold : hover ? Color.White : Bone;
        var room = chevronRight - half * 2 - UiMetrics.Space(6) - textX;
        _ui.TextBig(_batch, _ui.ShortenBig(value, room, DropTextPx), textX,
                    r.Center.Y - DropTextPx * 27 / 40, ink, DropTextPx);

        var caret = !enabled ? new Color(0x5C, 0x64, 0x70) : ink;
        var cx = chevronRight - half;
        var cy = r.Center.Y + (open ? depth / 2f : -depth / 2f);
        var dy = open ? -depth : depth;
        var stroke = UiMetrics.Control(3);
        _ui.LineSeg(_batch, new Vector2(cx - half, cy), new Vector2(cx, cy + dy), stroke, caret);
        _ui.LineSeg(_batch, new Vector2(cx, cy + dy), new Vector2(cx + half, cy), stroke, caret);
    }

    /// <summary>
    /// Draw the OPEN option list under a closed dropdown field, handle its input, return its rect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from the END of DrawSettings so the list wins the z-order over the rows beneath it; those
    /// rows' clicks are already disabled while it is open, so the frame's click belongs to the list
    /// alone. A click on an option applies it through <paramref name="pick"/>; any other click closes
    /// the list and does nothing else — swallowed, exactly as a modal should.
    /// </para>
    /// <para>
    /// THE SHAPE: a framed panel from the quiet family, dropped <see cref="DropGap"/> px below the
    /// field and aligned to its left edge; rows a flat <see cref="DropRowHeight"/> px tall inside the
    /// frame's margin, text <see cref="DropPadX"/> px in from the row, a 1-px hairline between rows,
    /// the row under the cursor on a warm fill with gold text, and a gold diamond on the left of the
    /// value you already have. The diamond is drawn rather than typed for the same reason the chevron
    /// is: the font gate is a whitelist, and a mark that carries meaning must not depend on the
    /// player's system font having a glyph for it.
    /// </para>
    /// <para>
    /// IT SCROLLS rather than overflowing. Nothing offered today comes close — the tallest list is six
    /// window sizes on a 4K desktop, and eleven rows fit under WINDOW SIZE — but a list that runs off
    /// the bottom of the panel would put options where they cannot be clicked, and that is a silent
    /// failure. The window is capped to the room under the field; the wheel and the arrow keys move it.
    /// </para>
    /// </remarks>
    private Rectangle DrawDropdownList(Rectangle field, string[] options, int currentIndex)
    {
        if (options.Length == 0) return Rectangle.Empty;

        // THE SAME BOX AND THE SAME ROWS UpdateSettings hit-tests (SettingsDropdownBox /
        // SettingsDropdownRow). The cursor and the scroll were settled there, before this paint.
        var (list, rows) = SettingsDropdownBox(field, options.Length);
        // The frame's own centre is opaque, so it IS the list's surface — no flat rectangle underneath,
        // which would square off the corners the ornament is shaped around.
        _ui.PanelQuiet(_batch, list);

        var scrolling = rows < options.Length;
        var mark = UiMetrics.Control(10);   // the diamond on the value you already have
        for (var k = 0; k < rows; k++)
        {
            var i = _dropScroll + k;
            if (i >= options.Length) break;
            var r = SettingsDropdownRow(list, rows, k, scrolling);
            var lit = i == _dropCursor;

            if (lit)
            {
                _ui.Fill(_batch, r, DropRowHover);
                _ui.Fill(_batch, new Rectangle(r.X, r.Y, 3, r.Height), DropRowEdge);
            }
            if (k > 0) _ui.Fill(_batch, new Rectangle(r.X, r.Y, r.Width, 1), DropRowRule);
            if (i == currentIndex)
                _ui.Diamond(_batch, new Rectangle(r.X + DropPadX, r.Center.Y - mark / 2, mark, mark), Gold);
            _ui.TextBig(_batch, _ui.ShortenBig(options[i], r.Right - DropPadX - (r.X + DropPadX + mark + UiMetrics.Space(12)), DropTextPx),
                        r.X + DropPadX + mark + UiMetrics.Space(12), r.Center.Y - DropTextPx * 27 / 40,
                        lit ? Gold : Bone, DropTextPx);
        }

        // The scrollbar, only when there is something to scroll — a bar that is always full is noise.
        if (scrolling)
        {
            var inner = new Rectangle(list.X + DropListInset, list.Y + DropListInset,
                                      list.Width - DropListInset * 2, rows * DropRowHeight);
            _ui.ScrollBar(_batch, new Rectangle(inner.Right - UiMetrics.ScrollbarWidth, inner.Y, UiMetrics.ScrollbarWidth, inner.Height),
                          _dropScroll, rows, options.Length);
        }
        return list;
    }

    /// <summary>A label and an ON/OFF button. Returns true when the click flipped it.</summary>
    /// <summary>
    /// One ON/OFF row: the label at the left of the given width, the button at its right end.
    /// </summary>
    /// <remarks>
    /// The button's x used to be the label's plus a fixed 276, which is a layout the caller cannot
    /// see and cannot fit around: regrouped into two columns, the settings panel put two of these
    /// buttons outside the panel and one on top of the next label. The row owns its width now.
    /// </remarks>
    private void DrawToggleRow(string label, Rectangle row, bool on)
    {
        var w = UiMetrics.Control(116);
        _ui.TextBig(_batch, _ui.ShortenBig(label, row.Width - w - UiMetrics.Space(12), UiTypography.Body), row.X,
                    row.Y + (row.Height - UiTypography.Body) / 2, Bone, UiTypography.Body);
        // THE SAME RECTANGLE UpdateSettings PRESSES — one helper, asked twice (SettingsToggleButton).
        _ui.Button(_batch, SettingsToggleButton(row.X, row.Y, row.Width), on ? "ON" : "OFF", ChromeMouse, clicked: false);
    }

    /// <summary>
    /// A draggable 0..100 volume slider row. Returns the value being set this frame, or -1.
    /// </summary>
    /// <remarks>
    /// Clicking anywhere on the track jumps the handle there; while the button stays held the handle
    /// follows the mouse — the raw held state, which the settings panel may read because it IS the
    /// modal. Persisting happens on RELEASE, back in DrawSettings, so a drag is one write, not sixty.
    /// The percent label updates live as the handle moves.
    /// </remarks>
    private void DrawSliderRow(string label, Rectangle track, int current, int labelX)
    {
        var textY = track.Y + (track.Height - UiTypography.Body) / 2;
        _ui.TextBig(_batch, label, labelX, textY, Bone, UiTypography.Body);

        // The house slim bar for the track and a diamond for the knob — two flat rectangles and a bone
        // slab looked unfinished beside the ornate fields (chrome-11).
        var bedH = UiMetrics.Control(10);
        var bed = new Rectangle(track.X, track.Center.Y - bedH / 2, track.Width, bedH);
        _ui.Bar(_batch, bed.X, bed.Y, bed.Width, bedH, current / 100f, UiInk.Accent);
        var fillW = (int)(track.Width * (current / 100f));
        var knob = UiMetrics.Control(16);
        _ui.Diamond(_batch, new Rectangle(track.X + fillW - knob / 2, track.Center.Y - knob / 2, knob, knob), Bone);
        _ui.TextBig(_batch, $"{current}%", track.Right + UiMetrics.Space(14), textY, Slate, UiTypography.Body);
    }

    /// <summary>Hover explanations for every settings row — one plain sentence each.</summary>
    /// <remarks>
    /// The zones are the rows DrawSettings just laid out (label and control, scrolled with them),
    /// because a player hovers the words as often as the widget — and because a zone written as a
    /// literal goes stale the first time the rows move, which is how four of them came to point at
    /// a two-column toggle grid that no longer existed. Suppressed while a slider is mid-drag, while
    /// the cursor is inside an open dropdown list, while it is off the visible rows, and while START
    /// A NEW GAME is armed — an are-you-sure moment is not the moment for furniture. The tip appears
    /// after <see cref="TipDelaySeconds"/> of rest on one row; the clock ticks in Update and resets
    /// here whenever the hovered row changes.
    /// </remarks>
    private void DrawSettingsTips(Point mouse, Rectangle openList, bool rowsVisible, (Rectangle Zone, string Key, string Tip)[] zones)
    {
        var key = "";
        var tip = "";
        var suppressed = _dragSlider != 0 || _resetArmTimer > 0f || !rowsVisible
                         || (openList != Rectangle.Empty && openList.Contains(mouse));
        if (!suppressed)
            foreach (var z in zones)
                if (z.Zone.Contains(mouse)) { key = z.Key; tip = z.Tip; _tipZone = z.Zone; break; }

        if (key != _tipKey)
        {
            _tipKey = key;
            _tipTimer = 0f;
            return;
        }
        // BESIDE THE ROW, not the pointer: a settings row is wide, and a plate that slid along it
        // as the reader's eye moved was the loudest instance of the cursor-following complaint.
        if (key.Length > 0 && _tipTimer >= TipDelaySeconds) _ui.HoverTip(_batch, tip, _tipZone);
    }

    /// <summary>The corner gear — the way back to settings from any screen, including mid-hunt.</summary>
    /// <summary>The ? that opens this screen's own tour, when it has one and nothing modal is up.</summary>
    private void DrawLearnButton()
    {
        if (!LearnOffered()) return;
        var r = LearnButton;
        var hover = r.Contains(ChromeMouse);
        _ui.Plate(_batch, r, hover ? UiInk.Accent : null);
        _ui.TextCenterBig(_batch, "?", r.Center.X, r.Y + (r.Height - UiTypography.Headline) / 2,
                          hover ? UiInk.Accent : UiInk.Secondary, UiTypography.Headline);
        if (hover) _ui.TextRight(_batch, "LEARN THIS SCREEN", r.Right, r.Bottom + 8, NavGold);
    }

    /// <summary>Is the ? live right now? Drawing and hit-testing ask the same question.</summary>
    /// <remarks>
    /// The coach's tier: a tour is teaching, so the ? stays live over a lit lesson and is gone under
    /// anything the player is reading — the open log — and everything above it, the fall included.
    /// </remarks>
    private bool LearnOffered()
        => !AttentionOwnedAbove(AttentionOwner.Coach)
           && Onboarding.TourFor(ScreenActivity()).Count > 0;

    /// <summary>Take the ? if it was pressed. Returns true when the click was spent.</summary>
    private bool TakeLearnClick()
    {
        if (!MouseClicked || !LearnOffered() || !LearnButton.Contains(ChromeMouse)) return false;

        // THE GEM TOUR IS STILL REACHABLE. It is a second, smaller FORGE tour — three cards about
        // sockets and the free first one — and it used to fire itself the first time the Forge was
        // opened with a gem in the bag. Nothing fires itself now, and without this clause its copy
        // would be alive, tested, and unreachable: the exact species of dead feature this project
        // keeps producing. When a gem is held and that tour is still owed, the ? gives that one; the
        // SOCKET tab is opened with it, so the light falls on the tab the card names.
        if (ScreenActivity() == Activity.Forge && _showForge
            && Onboarding.GemTourDue(GemsHeld(), _explained) is { } gemKey)
        {
            _forge.RequestSocketTab();
            BeginTour(Activity.Forge, Onboarding.GemTourFor(_forge.FreeSocketUsed), gemKey);
            _sound.Play("sfx_click", 0.7f);
            return true;
        }

        // ...otherwise the screen's own tour, started by hand. It is marked explained on the way out
        // exactly as before, so asking twice is allowed and the rail's dot still means "you have not
        // looked".
        BeginTour(ScreenActivity());
        _sound.Play("sfx_click", 0.7f);
        return true;
    }

    /// <summary>
    /// THE ENVELOPE — the door to the letters, and the mark that says one is waiting.
    /// </summary>
    /// <remarks>
    /// The LOG medallion's recipe (HuntScreen.DrawLogButton): bone at rest, white under the mouse, a
    /// 4 px lift eased in, and the 2 px pressed drop every button in the game wears. What is added here
    /// is the UNREAD MARK — the rail's own red dot on its dark seat — and the ARRIVAL's one halo.
    /// </remarks>
    private void DrawDispatchButton()
    {
        if (!DispatchesOffered()) return;
        var r = DispatchButton;
        var hot = r.Contains(ChromeMouse);
        var pressed = hot && UiKit.MouseHeld;
        var lift = UiMotion.Ease(UiMotion.KeyOf(r), hot ? 1f : 0f);
        var side = UiMetrics.Control(36) + (int)MathF.Round(UiMetrics.Control(4) * lift);
        var box = new Rectangle(r.X + (r.Width - side) / 2, r.Y + (r.Height - side) / 2 + (pressed ? 2 : 0), side, side);
        var tint = hot ? Color.White : new Color(0xE0, 0xD8, 0xC8);
        if (pressed) tint = new Color((int)(tint.R * 0.78f), (int)(tint.G * 0.78f), (int)(tint.B * 0.78f), (int)tint.A);

        // THE ARRIVAL'S ONE HALO, behind the medallion: it fades once and is gone. Armed in Update on
        // the frame the news was allowed to speak, never here — a pulse re-armed in a paint never ends.
        // Under Reduced Motion it is skipped and the dot carries the whole message, exactly as the
        // rail's unread dot drops its breath.
        var pulse = UiMotion.Pulse(DispatchPulseKey);
        if (pulse > 0f && !UiMotion.Reduced)
        {
            var halo = (int)MathF.Round(side * 0.5f * UiMotion.Smooth(pulse));
            _ui.Disc(_batch, new Rectangle(box.X - halo / 2, box.Y - halo / 2, box.Width + halo, box.Height + halo),
                     NavGold * (0.34f * UiMotion.Smooth(pulse)));
        }

        if (!_ui.Icon(_batch, "icon_dispatch", box, tint))
        {
            // No medallion on disk: an envelope from primitives — a plate, a rim and its two folds, the
            // way the gear keeps a drawn cog behind its own art.
            _ui.Fill(_batch, box, new Color(0x16, 0x11, 0x10, 0xE0));
            var flap = new Vector2(box.Center.X, box.Y + box.Height * 0.62f);
            _ui.LineSeg(_batch, new Vector2(box.Left + 3, box.Y + 3), flap, 3f, tint);
            _ui.LineSeg(_batch, new Vector2(box.Right - 3, box.Y + 3), flap, 3f, tint);
            _ui.Fill(_batch, new Rectangle(box.X, box.Y, box.Width, 2), tint);
            _ui.Fill(_batch, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), tint);
            _ui.Fill(_batch, new Rectangle(box.X, box.Y, 2, box.Height), tint);
            _ui.Fill(_batch, new Rectangle(box.Right - 2, box.Y, 2, box.Height), tint);
        }

        // ── THE UNREAD MARK. The rail's idiom verbatim, and a STATE rather than an animation: it holds
        //    until the letters are read, so it says what a six-second toast could not. NO BREATH here —
        //    the rail already has one breathing dot, and two on one screen is noise. It overhangs the
        //    corner by a third of itself, into the eight-pixel gap the chain leaves before the ?.
        //    Control(12), the size the panel's own rows wear -- one dot for the whole feature -- and
        //    small enough that the medallion under it still reads as an envelope (at 14 it sat across
        //    the rope rim, which the first captures showed).
        if (_inbox.Unread > 0)
        {
            var dot = Math.Max(8, UiMetrics.Control(12));
            var at = new Rectangle(r.Right - dot + dot / 3, r.Y - dot / 3, dot, dot);
            _ui.Disc(_batch, new Rectangle(at.X - 2, at.Y - 2, at.Width + 4, at.Height + 4), UiInk.Ground * 0.85f);
            _ui.Disc(_batch, at, UiInk.Danger);
        }

        if (hot) _ui.TextRight(_batch, "DISPATCHES — M", r.Right, r.Bottom + 8, NavGold);
    }

    /// <summary>Is the envelope live right now? Drawing and hit-testing ask the same question.</summary>
    /// <remarks>
    /// The ?'s tier, for the ?'s reason: the letters are background news, so they wait under anything
    /// the player is being asked to read — the open log, a chest's reveal, a fall, a panel, the authored
    /// opening — and they are gone on the title, which paints no chrome at all. The gear is the one
    /// control that outranks this, because the gear is the way out of everything.
    /// </remarks>
    private bool DispatchesOffered() => !AttentionOwnedAbove(AttentionOwner.Coach);

    /// <summary>The envelope's own pulse key — one key, so a second letter re-arms the same halo.</summary>
    private const int DispatchPulseKey = 0x0D15A7C4;

    /// <summary>
    /// Open DISPATCHES — from the envelope, or from M. One host panel at a time, newest letter first.
    /// </summary>
    private void OpenDispatches()
    {
        _showSettings = _showHelp = false;   // ONE HOST PANEL AT A TIME, as F1 and F10 already insist
        _showDispatches = true;
        _dispatchCursorKey = null;   // resolved to the newest letter on the panel's first frame
        _dispatchOpenKey = null;     // ...and nothing is READ until a letter is actually opened
        _dispatchScroll = 0;
        _sound.Play("sfx_click", 0.7f);
    }

    /// <summary>
    /// Everything the DISPATCHES panel decides: which letter is open, MARK ALL READ, the way out, and
    /// the keyboard path through all three.
    /// </summary>
    /// <remarks>
    /// Run from the modal block at the foot of Update, never from <c>DrawDispatches</c> — the panel
    /// paints the rectangles this hit-tests and reads the same <c>DispatchesFrameNow()</c> to find them
    /// (ADR-006). Raw edges rather than <c>Pressed</c>/<c>MouseClicked</c>: this panel IS the modal that
    /// makes those false, so it has to read the frame's own edge, the way UpdateSettings does.
    /// </remarks>
    private void TakeDispatchesInput()
    {
        // THE DEED, LATCHED. Opening the letters is the fact that completes the one lesson about them,
        // and it is a REAL open — not a card the player closed. Saved at once, as READ THE LOG is.
        if (!_dispatchesOpenedEver) { _dispatchesOpenedEver = true; Save(); }

        var f = DispatchesFrameNow();
        var rows = _inbox.Rows;
        var count = rows.Count;
        // Never the edge that opened the panel: without this the opening click also lands on the row
        // under the cursor (see _modalOpenedNow).
        var uiClick = _clicked && !_modalOpenedNow;

        // ── WHICH LETTERS THESE ARE, BY NAME. The cursor and the open letter are KEYS, resolved to a
        //    row here and written back at the foot. The fight ticks above this block, so a conquest,
        //    an awakening or a champion joining can land mid-read — and a posted letter goes to the
        //    FRONT of the newest-first list, shifting every row under the player. Held as indices the
        //    pane would swap to the newcomer and read it on the next frame. A cursor whose letter has
        //    been pruned away falls back to the newest rather than to nothing.
        var cursor = DispatchIndexOf(rows, _dispatchCursorKey);
        var open = DispatchIndexOf(rows, _dispatchOpenKey);
        if (cursor < 0 && count > 0) cursor = 0;

        // ── THE SCROLL, before anything is hit-tested against it. ───────────────────────────────
        var maxScroll = Math.Max(0, count - f.Rows);
        if (maxScroll > 0 && MouseWheel != 0 && f.List.Contains(ChromeMouse))
            _dispatchScroll = UiKit.Scrolled(_dispatchScroll, MouseWheel, f.Rows, count);
        // DEV: pose the list scrolled (RH_SHOT_SCROLL=<rows>|end), as the settings panel does.
        if (RigActive && maxScroll > 0
            && Environment.GetEnvironmentVariable("RH_SHOT_SCROLL") is { Length: > 0 } posedScroll)
            _dispatchScroll = posedScroll.Equals("end", StringComparison.OrdinalIgnoreCase) ? maxScroll
                : int.TryParse(posedScroll, out var posedRows) ? posedRows : _dispatchScroll;

        // ── THE KEYBOARD PATH, and it is not a convenience: every interaction in this game has to be
        //    completable without the mouse (technical-preferences). Up and Down walk the letters,
        //    ENTER opens the one under the cursor, R marks the lot read, and Escape peels the panel
        //    from the block above.
        var walked = false;
        if (KeyEdge(Keys.Down)) { cursor++; walked = true; }
        if (KeyEdge(Keys.Up)) { cursor--; walked = true; }
        cursor = count == 0 ? -1 : Math.Clamp(cursor, 0, count - 1);
        if (KeyEdge(Keys.Enter) && cursor >= 0) open = cursor;

        // ── A CLICK ON A ROW both moves the cursor and OPENS the letter: a mouse asks for one thing.
        if (uiClick && DispatchRowAt(ChromeMouse, _dispatchScroll, count) is var row && row >= 0)
        {
            cursor = row;
            open = row;
            _sound.Play("sfx_nav", 0.6f);
        }

        // THE COLUMN FOLLOWS THE CURSOR ONLY ON THE FRAME A KEY MOVED IT, so the keyboard can always
        // see what it is pointing at. Run unconditionally it fought the wheel and won: from a fresh
        // open the cursor is on the newest letter, so a wheel-down scrolled and was dragged straight
        // back in the same frame, and the list could not be scrolled by mouse at all.
        if (walked && cursor >= 0)
        {
            if (cursor < _dispatchScroll) _dispatchScroll = cursor;
            if (cursor >= _dispatchScroll + f.Rows) _dispatchScroll = cursor - f.Rows + 1;
        }
        _dispatchScroll = Math.Clamp(_dispatchScroll, 0, maxScroll);

        _dispatchCursorKey = cursor >= 0 ? rows[cursor].Key : null;
        _dispatchOpenKey = open >= 0 ? rows[open].Key : null;

        // AN OPEN LETTER HAS BEEN READ. It is on the page in front of the player; there is no second
        // act of reading to wait for. MarkDispatchRead writes only on the frame the flag actually
        // turns, so this costs one call and one save per letter however many frames it stands open.
        if (_dispatchOpenKey is { } reading) MarkDispatchRead(reading);

        // MARK ALL READ — the button at the foot, and R, which the button prints on itself the way
        // the gear prints ESC. A visible, enabled control in a modal with a keyboard map has to be
        // reachable without the mouse; opening each letter in turn is a different act, not this one.
        if (_inbox.Unread > 0 && (UiKit.ClickedIn(f.MarkAll, ChromeMouse, uiClick) || KeyEdge(Keys.R)))
        {
            MarkAllDispatchesRead();
            _sound.Play("sfx_click", 0.7f);
        }

        // THE WAY OUT. The corner icon; Escape and M close it from the block above.
        if (UiKit.ClickedIn(f.Close, ChromeMouse, uiClick))
        {
            _showDispatches = false;
            _sound.Play("sfx_click", 0.7f);
        }
    }

    private void DrawSettingsGear()
    {
        if (HostModalUp) return;   // a modal owns the frame
        var hover = SettingsGear.Contains(ChromeMouse);
        var c = new Vector2(SettingsGear.Center.X, SettingsGear.Center.Y);
        var ink = hover ? NavGold : UiInk.Secondary;

        if (_ui.Assets.Get("icon_settings") is { } gearArt)
        {
            // The drawn medallion (PixelLab, the close icon's sibling): 52 px at rest, 56 under the mouse —
            // at the control rate, so the gear grows with the pills beside it (chrome-17).
            var side = UiMetrics.Control(hover ? 56 : 52);
            var box = new Rectangle((int)c.X - side / 2, (int)c.Y - side / 2, side, side);
            _ui.SpriteFit(_batch, gearArt, box, hover ? Color.White : new Color(0xE0, 0xD8, 0xC8));
            if (hover) _ui.TextRight(_batch, "SETTINGS — ESC", SettingsGear.Right, SettingsGear.Bottom + 8, NavGold);
            return;
        }

        // Fallback when the art is missing: a cog from primitives — eight teeth, a rim, a dark hub.
        for (var k = 0; k < 8; k++)
        {
            var a = MathF.PI / 4f * k;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            _ui.LineSeg(_batch, c + dir * 9f, c + dir * 17f, 7f, ink);
        }
        const int rim = 24;
        for (var k = 0; k < rim; k++)
        {
            var a = MathF.PI * 2f * k / rim;
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 12f;
            _ui.Fill(_batch, new Rectangle((int)p.X - 2, (int)p.Y - 2, 4, 4), ink);
        }
        _ui.Fill(_batch, new Rectangle((int)c.X - 3, (int)c.Y - 3, 6, 6), new Color(0x0F, 0x0B, 0x0B));

        if (hover)
            _ui.TextRight(_batch, "SETTINGS — ESC", SettingsGear.Right, SettingsGear.Bottom + 8, NavGold);
    }

    /// <summary>The reference's top-right currency row — Gleam, Dust, Materials — on every gameplay screen.</summary>
    /// <summary>Compact number: 125802745 -> "125.8M". Keeps the top resource bar tight (late-game values
    /// hit the hundreds of millions and were blowing out the bar into the region title).</summary>
    internal static string Abbrev(long n, bool compact = false)
    {
        if (n < 0) return "-" + Abbrev(-n, compact);
        // COMPACT drops the decimals — "131M" for "130.6M" — for a row that has run out of width
        // (the currency pills at 150 %); the hover still carries every digit.
        if (compact)
            return n >= 1_000_000_000 ? $"{n / 1e9:0}B"
                 : n >= 1_000_000 ? $"{n / 1e6:0}M"
                 : n >= 10_000 ? $"{n / 1e3:0}K"
                 : n.ToString();
        return n >= 1_000_000_000 ? $"{n / 1e9:0.##}B"
             : n >= 1_000_000 ? $"{n / 1e6:0.##}M"
             : n >= 10_000 ? $"{n / 1e3:0.#}K"
             : n.ToString();
    }

    /// <summary>
    /// Advance the chrome's own motion: the screen switch, the modal fade, and the three currency
    /// pills' numbers, flashes and banked gains.
    /// </summary>
    /// <remarks>
    /// Every one of these is driven from Update by dt, never from Draw — a value advanced in Draw
    /// moves at the frame rate rather than at the clock, and a pulse re-armed in Draw never ends.
    /// Under Reduced Motion nothing eases: the numbers land, the flashes still happen (a flash is a
    /// state change, not movement) and the page does not slide.
    /// </remarks>
    private void TickChromeMotion(float dt)
    {
        // THE SCREEN SWITCH. The key names what is on the page; a change starts the fade.
        var key = ScreenKeyNow;
        if (key != _screenKey)
        {
            _screenKey = key;
            _screenFade = ScreenFadeSeconds;
        }
        _screenFade = Math.Max(0f, _screenFade - dt);

        // THE UNREAD DOT'S BREATH — one cycle every two seconds, on the clock rather than on the frame
        // rate. Wrapped so it never grows without bound in a session left running overnight.
        _navDotClock = (_navDotClock + dt) % 2f;

        // THE MODAL. The rising edge starts it; a closed modal leaves nothing behind.
        var modal = ModalUpNow;
        if (modal && !_modalWasUp) _modalFade = UiMotion.Fast;
        if (!modal) _modalFade = 0f;
        _modalWasUp = modal;
        _modalFade = Math.Max(0f, _modalFade - dt);

        // THE PILLS. Indexed by the frozen Purse* constants, so a pill's motion state cannot change
        // meaning when the row grows. A pill with no capsule still ticks: it costs nothing, and it
        // means the first Crystal ever salvaged arrives with its flash rather than appearing settled.
        Span<long> now = stackalloc long[PurseCount];
        for (var k = 0; k < PurseCount; k++) now[k] = PurseValue(k);

        _pillWarm += dt;
        for (var i = 0; i < PurseCount; i++)
        {
            if (_pillWarm < PillWarmSeconds)
            {
                _pillTrue[i] = now[i];
                _pillShown[i] = now[i];
                continue;
            }

            var delta = now[i] - _pillTrue[i];
            if (delta != 0)
            {
                _pillTrue[i] = now[i];
                _pillFlash[i] = UiMotion.Transition;
                if (delta > 0)
                {
                    // BANKED, NOT ANNOUNCED. A hunt pays Gleam a few at a time; a badge per kill is
                    // noise. The bank empties into one "+N" once it is worth a glance — a twentieth
                    // of what is already held, and never under the pill's own floor.
                    _pillGainAcc[i] += delta;
                    var bar = Math.Max(PillGainFloor[i], (long)(_pillShown[i] / 20));
                    if (_pillGainAcc[i] >= bar)
                    {
                        _pillGainShow[i] = _pillGainAcc[i];
                        _pillGainT[i] = PillGainSeconds;
                        _pillGainAcc[i] = 0;
                    }
                }
                else
                {
                    // A SPEND CANCELS THE BANK. Money that arrived and left is not a gain to report.
                    _pillGainAcc[i] = 0;
                }
            }

            // The printed number walks to the true one over a transition; Reduced Motion lands it.
            if (UiMotion.Reduced) _pillShown[i] = _pillTrue[i];
            else if (Math.Abs(_pillShown[i] - _pillTrue[i]) < 1.0) _pillShown[i] = _pillTrue[i];
            else
            {
                var step = dt / UiMotion.Transition;
                _pillShown[i] += (_pillTrue[i] - _pillShown[i]) * Math.Min(1.0, step);
            }

            _pillFlash[i] = Math.Max(0f, _pillFlash[i] - dt);
            _pillGainT[i] = Math.Max(0f, _pillGainT[i] - dt);
        }

        PoseChromeMotion();
    }

    /// <summary>
    /// DEV ONLY: hold the chrome's transients at a chosen point so a capture can photograph them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every effect above lasts between 100 and 350 ms and the rig's shutter opens on frame 60, so
    /// none of them would ever appear in a screenshot — and this project's rule is that a state no
    /// capture mode can pose has never been looked at. <c>RH_SHOT_MOTION=&lt;0..1&gt;</c> pins the
    /// screen switch, the modal fade, the pill flash and a banked gain at that fraction of their run
    /// (1 = the first instant, 0.5 = halfway, 0 = finished), so each can be judged at real size.
    /// </para>
    /// <para>
    /// It runs only under the rig and only after the ordinary tick, so it never changes what the game
    /// does for a player — it re-poses the same fields the real motion drives, rather than adding a
    /// second path through them.
    /// </para>
    /// </remarks>
    private void PoseChromeMotion()
    {
        if (!RigActive) return;
        if (Environment.GetEnvironmentVariable("RH_SHOT_MOTION") is not { } spec
            || !float.TryParse(spec, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var t)) return;

        t = Math.Clamp(t, 0f, 1f);
        _screenFade = ScreenFadeSeconds * t;
        _modalFade = UiMotion.Fast * t;
        for (var i = 0; i < 3; i++)
        {
            _pillFlash[i] = UiMotion.Transition * t;
            _pillGainShow[i] = Math.Max(PillGainFloor[i], (long)(_pillShown[i] / 20));
            _pillGainT[i] = PillGainSeconds * t;
        }
    }

    private void DrawCurrencyPills()
    {
        // ONE CURRENCY PER CAPSULE — never a packed "S.. E.. C.. X..". Gleam, Dust and Scrap are
        // always here; Essence, Core and Crystal join the row once the account holds any (see the
        // Purse table). Icon and abbreviated value only: a labelled pill with an eight-digit value
        // balloons past the bar and crosses into the stage header.
        //
        // WHAT A PILL PRINTS is the walking number (TickChromeMotion), not the true one — a spend or a
        // payout moves it over a transition instead of swapping it between two frames. The HOVER reads
        // the exact figure: the animation is for the corner of the eye, the tooltip for "how much".
        var shown = PurseOrder.Where(PurseShown).ToArray();

        // THE CHAIN IS MEASURED BEFORE IT IS DRAWN. The value is set at the Label rung, which follows
        // the profile, so at 150 % a row of six late-game figures would run into the stage header.
        // When the chain would cross PillChainMinLeft every value drops its decimals — "131M" — and
        // the icon carries the identity.
        var walk = shown.Select(i => (long)Math.Round(_pillShown[i])).ToArray();
        var values = walk.Select(v => Abbrev(v)).ToArray();
        if (PillChainLeft(values) < PillChainMinLeft)
            values = walk.Select(v => Abbrev(v, compact: true)).ToArray();

        // Right to left, each capsule starting where the last one ended.
        var rects = new Rectangle[shown.Length];
        var edge = PillRowRight;
        for (var k = 0; k < shown.Length; k++)
        {
            var left = _ui.Pill(_batch, edge, PillRowTop, PurseIcon[shown[k]], default, values[k], "",
                                PurseInk[shown[k]], PillHeight);
            rects[k] = new Rectangle(left, PillRowTop, edge - left, PillHeight);
            edge = left - PillGap;
        }
        if (edge < PillChainMinLeft) System.Diagnostics.Debug.WriteLine($"Currency bar (left {edge}) crowds the stage header.");

        // WHERE THE GLEAM PILL ACTUALLY IS, published for the intro's spotlight — read from the row
        // this draw just laid out, so the card that says GLEAM and the light that shows it cannot
        // drift apart. (Card four used to light a hand-written rectangle framing all three pills.)
        var gleamAt = Array.IndexOf(shown, PurseGleam);
        if (gleamAt >= 0) s_gleamPillRect = rects[gleamAt];

        // THE SPEND REACTS AT THE PILL (brief sec. 37) rather than by flying a coin across the page:
        // the capsule takes a rim in its own colour that fades over a transition. A flash is a state
        // change rather than movement, so Reduced Motion keeps it.
        for (var k = 0; k < shown.Length; k++)
        {
            var i = shown[k];
            var rr = rects[k];
            if (_pillFlash[i] > 0f)
            {
                var a = _pillFlash[i] / UiMotion.Transition;
                var ink = PurseInk[i] * (a * 0.75f);
                _ui.Fill(_batch, new Rectangle(rr.X, rr.Y, rr.Width, 2), ink);
                _ui.Fill(_batch, new Rectangle(rr.X, rr.Bottom - 2, rr.Width, 2), ink);
                _ui.Fill(_batch, new Rectangle(rr.X, rr.Y, 2, rr.Height), ink);
                _ui.Fill(_batch, new Rectangle(rr.Right - 2, rr.Y, 2, rr.Height), ink);
            }

            // AND A SUBSTANTIAL GAIN SAYS SO (brief sec. 38), under its own pill, once — the bank in
            // TickChromeMotion decides what counts as substantial. Never while the pill is hovered:
            // the tooltip lives on that line, and two answers in one place is neither.
            if (_pillGainT[i] > 0f && _pillGainShow[i] > 0 && !rr.Contains(ChromeMouse))
            {
                var fade = Math.Min(1f, _pillGainT[i] / 0.3f);
                _ui.TextRight(_batch, "+" + Abbrev(_pillGainShow[i]), rr.Right, rr.Bottom + UiMetrics.Space(4),
                              PurseInk[i] * fade);
            }
        }

        // WHAT AM I LOOKING AT, AND WHERE DOES MORE COME FROM. The pill is an icon and "4.2M", which
        // names neither the resource nor the real figure; the hover does both — the name, where it is
        // earned, and every digit. It used to say what the resource was FOR, which is the question the
        // player is not asking: they are looking at a number that moved, or at a price they cannot
        // meet, and both are answered by the faucet (playtest 2026-09-09).
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (var k = 0; k < shown.Length; k++)
        {
            var rr = rects[k];
            if (!rr.Contains(ChromeMouse)) continue;
            var text = $"{PurseSource[shown[k]]}  ·  {PurseValue(shown[k]).ToString("N0", inv)}";
            // The plate is its line plus a pad — 42 tall at 100 %, taller with the rung — hung under
            // the capsules, and it never leaves the page's right edge.
            var padX = UiMetrics.Space(14);
            var padY = UiMetrics.Space(10);
            var w = Math.Min(_ui.Measure(text) + padX * 2, UiKit.Page.Width - UiMetrics.Space(32));
            var tip = new Rectangle(Math.Min(rr.Right, UiKit.PageRight(16)) - w, PillRowTop + PillHeight + UiMetrics.Space(8), w,
                                    UiTypography.Label + padY * 2);
            _ui.Fill(_batch, tip, new Color(0x0E, 0x0A, 0x14, 0xF0));
            _ui.Fill(_batch, new Rectangle(tip.X, tip.Y, tip.Width, 2), NavGold * 0.6f);
            _ui.TextRight(_batch, _ui.Shorten(text, w - padX * 2), tip.Right - padX, tip.Y + padY, Bone);
            break;
        }
    }

    // ── The currency row's geometry. The capsules are UiKit.Pill's — a fixed 60 tall, icon 40, pads
    //    24 / 12 / 24 around the value — so the row is page geometry: it neither grows nor moves with
    //    the profile, only the value inside a capsule does.
    // ── THE PURSE. Six things the account can hold, in the order the row draws them RIGHT to left.
    //
    // Playtest 2026-09-09: "Crystal and Essence are only visible once you enter the Forge screen. If
    // they have uses elsewhere they need to stand globally."
    //
    // They do: a Crystal is what RESET ALL TRAINING costs, on a screen with no Crystal anywhere on it
    // except inside the sentence asking for one. The three finer tiers had lived in a hover on the
    // Scrap pill, which is not a place anybody looks for a number they are about to spend.
    //
    // A PILL APPEARS WHEN THE ACCOUNT HOLDS ANY. Gleam, Dust and Scrap are always there — the game
    // pays them from the first wave — and Essence, Core and Crystal arrive with the first one
    // salvaged, so the row grows with the account instead of showing three empty capsules to a player
    // who has never seen a Rare. The row is right-anchored and already measures its own chain, so the
    // extra capsules push leftward into space the early game was not using.
    //
    // The indices are FROZEN: 0/1/2 are the original three and the per-pill motion arrays are keyed by
    // them, so the animation state of a pill cannot change meaning when the row grows.
    private const int PurseCount = 6;
    private const int PurseScrap = 0, PurseDust = 1, PurseGleam = 2, PurseEssence = 3, PurseCore = 4, PurseCrystal = 5;

    /// <summary>Right to left: the materials cluster at the edge, then Dust, then Gleam.</summary>
    private static readonly int[] PurseOrder = { PurseCrystal, PurseCore, PurseEssence, PurseScrap, PurseDust, PurseGleam };

    private static readonly string[] PurseIcon =
        { "mat_scrap", "ui_memory_dust", "ui_gleam_coin", "mat_essence", "mat_core", "mat_crystal" };

    private static readonly Color[] PurseInk =
    {
        new(0x9A, 0xC0, 0x88), new(0x9E, 0x86, 0xFF), UiInk.Accent,
        new(0x74, 0xC6, 0xE8), new(0xC0, 0x6E, 0xE0), new(0xF0, 0xC0, 0x48),
    };

    /// <summary>
    /// WHERE EACH ONE COMES FROM — the hover's sentence.
    /// </summary>
    /// <remarks>
    /// Playtest 2026-09-09: <i>"instead of saying what the resources are used for, let us write how
    /// they are obtained."</i> The hovers said "GLEAM — PAYS FOR TRAINING (V)", which answers a
    /// question the player is not asking: they are looking at a number that went up, or at a price
    /// they cannot meet, and either way the useful answer is where MORE comes from. What it buys is
    /// written on the thing it buys, at the moment of buying, in every screen that spends it.
    /// </remarks>
    private static readonly string[] PurseSource =
    {
        "SCRAP — SALVAGE COMMON AND UNCOMMON GEAR · THE WARREN'S SCAVENGER RUNS",
        "MEMORY DUST — CONQUER A REGION · EACH MASTERY LEVEL · THE WARREN'S FORAGING PITS AND SENTRY BURROWS",
        "GLEAM — EVERY WAVE YOU CLEAR PAYS IT · THE WARREN'S NURSERY AND TUNNELS",
        "ESSENCE — SALVAGE RARE GEAR · THE WARREN'S RITUAL NEST AND SOUNDING CHAMBER",
        "CORE — SALVAGE EPIC GEAR",
        "CRYSTAL — SALVAGE LEGENDARY GEAR",
    };

    private long PurseValue(int i) => i switch
    {
        PurseScrap => (long)_hunter.MaterialOf(Material.Scrap),
        PurseDust => _dust.MemoryDust,
        PurseGleam => _hunter.Gleam,
        PurseEssence => (long)_hunter.MaterialOf(Material.Essence),
        PurseCore => (long)_hunter.MaterialOf(Material.Core),
        _ => (long)_hunter.MaterialOf(Material.Crystal),
    };

    /// <summary>Does this one have a capsule right now? The first three always; the rest once held.</summary>
    private bool PurseShown(int i) => i <= PurseGleam || PurseValue(i) > 0;

    /// <summary>The capsules' top edge — level with the settings gear beside them.</summary>
    private const int PillRowTop = 16;
    /// <summary>A capsule's height (UiKit.Pill's) — at the control rate, so the capsule grows with the digits inside it (chrome-17).</summary>
    private static int PillHeight => UiMetrics.Control(60);
    /// <summary>The gap between two capsules.</summary>
    private const int PillGap = 20;

    /// <summary>The GLEAM capsule's own rectangle, as the last DrawCurrencyPills laid it out.</summary>
    /// <remarks>
    /// Static because the tour asks HuntScreen for its spotlights and the pills are the host's chrome.
    /// Published rather than recomputed: a second copy of this arithmetic is how the card and the
    /// light came apart in the first place.
    /// </remarks>
    internal static Rectangle GleamPillRect => s_gleamPillRect;

    private static Rectangle s_gleamPillRect = new(1640, 4, 200, 84);
    /// <summary>What UiKit.Pill wraps around a value: its left pad, the icon, the gap, the right pad — the capsule's own proportions.</summary>
    private static int PillCapsuleChrome => UiKit.PillChrome(PillHeight);
    /// <summary>
    /// The row's right end: a breath left of the ? and the gear, so none of the three shares a pixel.
    /// </summary>
    /// <remarks>
    /// It was <c>SettingsGear.X - 16</c>, which was right while the gear was the only thing in the
    /// corner. LEARN THIS SCREEN and then DISPATCHES took their room out of it — see
    /// <see cref="DispatchButtonLeft"/>, the chain's leftmost link, which this
    /// reads rather than re-deriving, so a change to either lands on both.
    /// </remarks>
    private static int PillRowRight => DispatchButtonLeft - 16;

    /// <summary>
    /// The chrome row's foot: the bottom of the capsules, the ? and the gear, which share one line.
    /// </summary>
    /// <remarks>
    /// Published because the HUNT hangs its EXPEDITION LOG medallion under it. That medallion sat at a
    /// literal y of 40 while the capsules sat at 16 — fine at 100 %, where a capsule is 60 tall and the
    /// chain ends around x 1776, and a collision at 150 %, where the capsule grows to 90 and the chain
    /// reaches back past the medallion's left edge at 1206. Half the medallion was drawn UNDER the
    /// GLEAM pill — and that medallion is the control READ THE LOG points at, so the game's most
    /// important prompt was lighting something the player could not see. One derived anchor, the same
    /// rule at every profile, rather than two literals that only agree at one of them.
    /// </remarks>
    internal static int ChromeRowBottom => PillRowTop + PillHeight;
    /// <summary>The least the row's left end may be: clear of the stage header (x 630..1190) with a breath.</summary>
    private const int PillChainMinLeft = 1210;

    /// <summary>Where the row's left end would land for these three values — the capsules' arithmetic, before any is drawn.</summary>
    private int PillChainLeft(string[] values)
    {
        var x = PillRowRight;
        for (var i = 0; i < values.Length; i++)
            x -= PillCapsuleChrome + _ui.Measure(values[i]) + (i > 0 ? PillGap : 0);
        return x;
    }

    private void DrawTitle()
    {
        _ui.Background(_batch, "bg_title");

        // Logo, centred near the top. Drawn at a LAYOUT size with the asset's own aspect preserved — NOT
        // its native 1200×400, which the 4× canvas transform would blow up to 4800px (only "SONAN" showed).
        // The IDLExIDLE lockup (2026-08-24 rename): IDLE x IDLE, the X a winged pair of crossed
        // hourglasses — composed from the generated emblem + the shipped Cinzel face by
        // tools' make_logo pass. The old package_08 wordmark said the retired name.
        if (_assets.Get("logo_idlexidle_full") is { } logo)
        {
            const int lw = 1180;   // 1920-space logo width; the lockup is ~4.3:1
            var lh = lw * logo.Height / logo.Width;   // aspect-preserved
            _batch.Draw(logo, new Rectangle((CanvasWidth * ArtScale - lw) / 2, 150, lw, lh), Color.White);
        }
        else
            _ui.TextCenterBig(_batch, "IDLExIDLE", 960, 240, Gold, UiTypography.ScreenTitle, TextFace.Display);

        var items = new[] { _hasSave ? "CONTINUE" : "BEGIN THE HUNT", "SETTINGS", "QUIT" };
        for (var i = 0; i < items.Length; i++)
        {
            var selected = i == _titleCursor;
            var box = TitlePlate(i);
            if (selected) _ui.Panel(_batch, box, gold: true); else _ui.PanelQuiet(_batch, box);
            // The ornate menu plates are DARK (even the gold/selected one), so both states take LIGHT text —
            // warm gold when selected, bone otherwise.
            // A 96 px menu plate carrying body-sized text read as a caption on a slab; these are the
            // three loudest choices in the game and they take the in-panel headline rung.
            // Centred on the plate by the rung's own optical middle (the house 27/40), so a 39 px label at
            // 150 % sits where the 26 px one sat at 100 %.
            _ui.TextCenterBig(_batch, items[i], box.Center.X, box.Center.Y - UiTypography.Headline * 27 / 40,
                selected ? new Color(0xF6, 0xD8, 0x88) : new Color(0xEC, 0xE6, 0xF2), UiTypography.Headline);

            // Clickable as well as keyed — every other menu in the game is — but the hit test is NOT
            // here. It is in Update, beside the arrow keys, because a click edge tested in Draw is lost
            // on a catch-up tick: see the block at the title's Update branch for the whole of why.
            // Nothing on this screen reads the mouse for anything but hover, which the cursor already
            // carries; Draw only paints.
        }

        TextCenter("UP / DOWN     ENTER", 960, 984, Slate);

        // Which build this is — the same stamp the feedback code carries.
        TextRight($"BUILD {BuildStamp.Short}", 1908, 1044, Slate);

        // A locked session says so HERE, where the player decides whether to continue — not only in
        // a toast that fades. Saving being off changes what playing is worth.
        if (_saveLocked)
        {
            // Two lines at the Label rung, one pitch and a breath apart (24 → 60 at 100 %), so the
            // second cannot print through the first when the rung grows.
            TextCenter("SAVING IS OFF — " + _saveLockReason + ".", 960, 24, Ember);
            TextCenter("YOUR OLD FILE IS KEPT ON DISK, UNTOUCHED. START A NEW GAME IN SETTINGS TO PLAY FRESH.", 960,
                       24 + UiTypography.Pitch(UiTypography.Label) + UiMetrics.Space(8), Bone);
        }
    }

    // ── World map ─────────────────────────────────────────────────────────────────────────────

    private void UpdateWorld()
    {
        PushMapState();
        _mapScreen.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked, MouseWheel);
        ConsumeMapRequests();
    }

    /// <summary>Feed the Map screen the live world state each frame (host owns the model, the screen renders it).</summary>
    private void PushMapState()
    {
        _mapScreen.World = _world;
        _mapScreen.ActiveRegion = _activeRegion;
        // (_mapScreen.DeepestWave was pushed here every frame and read by nothing in that file —
        //  the third instance of this exact shape found in one week. Deleted rather than wired:
        //  the screen already shows per-region power and YOUR POWER, which is what it needed it for.)
        _mapScreen.HunterPower = _hunter.PowerRating;
        _mapScreen.ConquerWaves = ConquerWaveDepth;
        _mapScreen.DustOwned = _dust.MemoryDust;
        _mapScreen.Message = _conquerMsg;
    }

    /// <summary>Act on the Map screen's ENTER / DEEPEN requests — all raised in its Update, keyboard and pointer alike.</summary>
    private void ConsumeMapRequests()
    {
        // Selecting a region, leaving for the hunt, being refused by a locked one, and a region that
        // has just opened all sound different now; the screen decides which, the host plays it.
        PlayCue(_mapScreen.ConsumeCue(), 0.6f);
        if (_mapScreen.ConsumeStart() is { } start)
        {
            var farm = _world.RegionFarm(start.RegionId);
            farm.SetStartWave(Checkpoints.Clamp(start.Wave, farm.BestDepth, _world.IsConquered(start.RegionId)));
            _sound.Play("sfx_click", 0.6f);
            Save();
        }
        if (_mapScreen.ConsumeEnter() is { } id)
        {
            if (_world.IsUnlocked(id)) { SetActiveRegion(id); _showWorld = false; }
        }
        if (_mapScreen.ConsumeDeepen()) DeepenCorruption();
        if (_mapScreen.ConsumeEase()) EaseCorruption();
    }

    /// <summary>Pull the corruption one tier back — the choice DEEPEN never offered (playtest 2026-08-23).</summary>
    private void EaseCorruption()
    {
        if (!_world.CanEaseCorruption) return;
        var tier = _world.EaseCorruption();
        _sound.PlayFirst(0.8f, "sfx_click");
        // FOUND, NOT FIXED — a line that cannot be read. Easing needs a corruption to ease and
        // deepening needs the whole world conquered, and once the world is conquered the strip belongs
        // to the corruption LADDER for good, so this message and DeepenCorruption's two are written to a
        // row that is already showing something else. Left as they are, deliberately: the toast is the
        // other place they could go, and on this screen it hangs directly over the strip and the
        // SHALLOWER / DEEPER buttons the player is pressing. The ladder row does say the new tier and
        // its blurb, so what is actually lost is the note about the dust. Owner: the corruption.
        _conquerMsg = $"THE CORRUPTION EASES — {CorruptionLook.Label(tier)}.";
        Save();
    }

    /// <summary>Push the fully-conquered world one corruption tier deeper: harder, richer, permanent.</summary>
    private void DeepenCorruption()
    {
        if (!_world.CanDeepenCorruption) return;
        var peakBefore = _world.PeakCorruptionTier;
        var tier = _world.DeepenCorruption();
        _sound.PlayFirst(1f, "sfx_deepen", "sfx_conquer", "sfx_levelup");
        // The award is for REACHING a tier, paid once: SHALLOWER then DEEPER used to mint it again on every
        // cycle (review, 2026-08-23).
        var firstTime = tier > peakBefore;
        if (firstTime) _dust.AddDust(CorruptionScaling.DeepeningDustAward(tier));
        // Also never read — see EaseCorruption. The strip is the corruption ladder itself by here.
        _conquerMsg = firstTime
            ? $"THE CORRUPTION DEEPENS — {CorruptionLook.Label(tier)}. HARDER ENEMIES, MORE DUST."
            : $"THE CORRUPTION DEEPENS AGAIN — {CorruptionLook.Label(tier)}. (YOU ALREADY GOT THE DUST FOR THIS TIER.)";
        Save();
    }

    private void DrawWorld()
    {
        // PAINT ONLY. PushMapState stays — Draw reads World / ActiveRegion / HunterPower / DustOwned /
        // Message from it — but the screen raises nothing here any more, so there is nothing to consume:
        // every request, keyboard and pointer alike, is raised in UpdateWorld below.
        PushMapState();
        _mapScreen.Draw(_batch, PageCursor);
    }

    // ── THE HELP SHEET (F1) ────────────────────────────────────────────────────────────────────
    //
    // Page geometry at 100 %: the panel it grows from, and the two columns' split. The rows are FLOWED,
    // not placed (UI polish P2): each sentence wraps to its column and the next row starts under it, so
    // a bigger profile makes a longer column rather than rows that print through each other — and when
    // the columns outgrow the page they scroll under the title, with the close icon anchored above the
    // scroll (brief §17–§18). Before this the rows sat at literal 38 px pitches and the two prose
    // blocks at literal y's; at 150 % the second block ran straight through the first.

    /// <summary>The sheet at 100 %: where it stands, and the height it grows from.</summary>
    private const int HelpLeft = 112, HelpWidth = 1696, HelpMinHeight = 520;

    /// <summary>The left column's share of the sheet — the build's sentences are the longer ones.</summary>
    private const int HelpLeftColumn = 964;

    /// <summary>The columns' inset from the panel's edge: the frame's corner reach and a breath (64 at 100 %).</summary>
    private static int HelpInset => UiTypography.PanelPadX + UiMetrics.Space(24);

    private void DrawHelp()
    {
        // Framed like every other surface. This was a bone rectangle with a black rectangle inside it —
        // the last flat two-rect panel in the game, on the screen a new player is most likely to open.
        _ui.Scrim(_batch, 0.72f);
        var page = UiKit.Page;
        var mouse = ChromeMouse;
        var body = UiTypography.Body;
        var pitch = UiTypography.Pitch(body);
        var rowGap = UiMetrics.Space(8);                        // a row's breath under its last line
        var headerH = UiTypography.ModalTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(28);   // the title row → the columns
        var headToRows = pitch + UiMetrics.Space(26);           // a column's heading → its first row
        // The key columns are MEASURED, not scaled from a literal: Control(116) was 174 px for "ESC" at
        // 150 %, which wrapped five rows and pushed ESC and F1 under the clip (chrome-09).
        var keyLeft = _ui.MeasureBig("00", body) + UiMetrics.Space(24);    // the key column: a count, or a key's name
        var keyRight = _ui.MeasureBig("ESC", body) + UiMetrics.Space(24);
        var leftX = HelpLeft + HelpInset;
        var leftW = HelpLeftColumn;
        var rightX = leftX + leftW;
        var rightW = HelpWidth - HelpInset * 2 - leftW;

        // Two columns: the build on the left, the screens on the right.
        // DERIVED where it can be, and honest where it cannot. This panel is the only place a new player
        // is told how the game works, and it had drifted: it stated a flat "4 WOVEN SKILLS" when the
        // trait spine sells a fifth, and "3 KEYSTONE SOCKETS" when a player starts with ONE and buys the
        // others. Both numbers now come from the live loadout, so the help cannot be wrong about what
        // the player actually has.
        // SENTENCES, NOT FRAGMENTS. This column used to be seven half-lines — "SOURCE VS REGION",
        // "EACH HAS A COST" — which name a subject without saying anything about it. A player opening
        // the only page that explains the game deserves a sentence per row.
        var build = new (string Key, string What)[]
        {
            ($"{_loadout.SkillCapacity}", "SKILL SLOTS — EACH HOLDS ONE SKILL"),
            ("", "A SKILL IS A STYLE, A VARIATION AND ITS REINFORCEMENTS"),
            ($"{_loadout.KeystoneCapacity}", "KEYSTONE SOCKETS — EACH KEYSTONE COSTS SOMETHING"),
            ("", "SLOTS AND SOCKETS OPEN AS YOU GO DEEPER AND CONQUER"),
            ("", "ONE ACTION AT A TIME — TEMPO DECIDES HOW OFTEN"),
            ("", "MIGHT IS YOUR BASIC ATTACK, RESONANCE IS YOUR SKILLS"),
            ("", "A SKILL'S SOURCE IS ITS ELEMENT, NOT THE REGION'S"),
        };
        // Every tile on the nav rail, plus the two keys that open nothing on it. HUNT and ROSTER were
        // missing outright — ROSTER is ten characters and a quest chain, and nothing in the game told
        // anyone the key existed.
        var world = new (string Key, string What)[]
        {
            // IN RAIL ORDER, and complete. K and T were missing entirely — the VAULT is where chests are
            // read and the WEAVE is where skills are chosen, so the two keys a new player most needs were
            // the two this list did not have. B said "WEAVE YOUR BUILD", which is the OTHER screen.
            ("H", "HUNT — THE FIGHT"),
            ("C", "GEAR — WHAT YOU WEAR"),
            ("V", "TRAINING — SPEND GLEAM TO GET STRONGER"),
            ("B", "BUILD — CHOOSE YOUR SKILLS"),
            ("E", "MASTERY — HOW YOUR SKILLS WORK"),
            ("K", "VAULT — CHESTS YOU HAVE NOT OPENED"),
            ("F", "FORGE — UPGRADE, RE-ROLL, SOCKET, SALVAGE"),
            ("A", "WARREN — WORK WHILE YOU ARE AWAY"),
            ("W", "MAP — WHERE TO HUNT"),
            ("P", "TRAITS — PERMANENT BONUSES"),
            ("R", "ROSTER — YOUR HUNTERS"),
            ("L", "THE EXPEDITION LOG"),
            ("M", "DISPATCHES — WHAT HAS HAPPENED TO YOUR ACCOUNT"),
            ("ESC", "SETTINGS — DISPLAY, SOUND, QUIT"),
            ("F1", "CLOSE"),
        };

        // EVERY ROW IS WRAPPED TO ITS COLUMN BEFORE ANYTHING IS DRAWN, so the sheet's height is known:
        // two columns that share a page rather than two blocks that share pixels. (The prose used to be
        // wrapped to 1568 px, the whole panel, and ran straight through the SCREENS list.)
        IReadOnlyList<string>[] Wrap((string Key, string What)[] rows, int width)
        {
            var lines = new IReadOnlyList<string>[rows.Length];
            for (var i = 0; i < rows.Length; i++) lines[i] = _ui.WrapBig(rows[i].What, width, body);
            return lines;
        }
        int ColumnHeight(IReadOnlyList<string>[] lines)
        {
            var h = headToRows;
            foreach (var l in lines) h += Math.Max(1, l.Count) * pitch + rowGap;
            return h;
        }
        var proseW = leftW - UiMetrics.Space(64);   // a reading line: 900 at 100 %
        var idea = _ui.WrapBig(
            "ONE HUNTER, YOUR BUILD. IT FIGHTS ON ITS OWN, ON EVERY SCREEN, EVEN WHILE THE GAME IS " +
            "CLOSED. EACH WAVE IT CLEARS PAYS GLEAM AT ONCE. EVERY 5TH WAVE IS A BOSS, AND ONLY A BOSS " +
            "DROPS A CHEST. WHEN IT FALLS, IT GETS UP AND GOES AGAIN. YOU LOSE NOTHING.", proseW, body);
        var next = _ui.WrapBig(
            $"SPEND GLEAM ON TRAINING (V) TO GROW STRONGER. REACH WAVE {ConquerWaveDepth} TO CONQUER A REGION AND UNLOCK THE NEXT.",
            proseW, body);
        var buildLines = Wrap(build, leftW - keyLeft);
        // Tighter breaths between the column and the two paragraphs (48 → 24, 40 → 20): at 150 % the
        // closing sentence was clipped on the frame's inset for want of exactly that room (chrome-09).
        var leftH = ColumnHeight(buildLines) + UiMetrics.Space(24) + pitch + UiMetrics.Space(12)
                    + idea.Count * pitch + UiMetrics.Space(20) + next.Count * pitch;
        var worldLines = Wrap(world, rightW - keyRight);
        var contentH = Math.Max(leftH, ColumnHeight(worldLines));

        // THE PANEL: the design height at 100 %, taller when the profile asks, never past the page —
        // and past that the columns scroll under the title, the right one giving up a lane for the bar.
        var padBottom = UiTypography.PanelPadBottom;
        var needed = headerH + contentH + padBottom;
        var maxH = page.Height - UiMetrics.Space(12) * 2;
        // The sheet is its content, centred — a fixed 840 left the lower fifth of the frame bare at 100 % (chrome-15).
        var h = Math.Clamp(needed, UiMetrics.Control(HelpMinHeight), maxH);
        var scrolls = needed > h;
        if (scrolls)
        {
            rightW -= UiMetrics.ScrollbarWidth + UiMetrics.Space(8);
            worldLines = Wrap(world, rightW - keyRight);
            contentH = Math.Max(leftH, ColumnHeight(worldLines));
        }
        var panel = new Rectangle(HelpLeft, page.Y + (page.Height - h) / 2, HelpWidth, h);
        var view = new Rectangle(panel.X + UiTypography.PanelPadX, panel.Y + headerH,
                                 panel.Width - UiTypography.PanelPadX * 2, panel.Bottom - padBottom - (panel.Y + headerH));
        var maxScroll = scrolls ? Math.Max(0, contentH - view.Height) : 0;
        // THE WHEEL IS TAKEN IN UPDATE, against the view and the overflow this paint measured (see
        // _helpView / _helpMaxScroll). Reading a wheel delta here would lose a notch on a catch-up
        // tick exactly as a click was lost, and scrolling is the only way to reach the bottom columns.
        _helpView = view;
        _helpMaxScroll = maxScroll;
        _helpScroll = Math.Clamp(_helpScroll, 0, maxScroll);

        _ui.Panel(_batch, panel);
        _ui.TextCenterBig(_batch, "IDLExIDLE — CONTROLS", panel.Center.X, panel.Y + UiTypography.ModalTitleTop, Gold, UiTypography.PanelTitle);

        if (scrolls) BeginChromeClip(view);
        var top = view.Y - _helpScroll;

        // A column: its heading, then each row's key beside its wrapped sentence. Returns the y under it.
        int Column(string heading, (string Key, string What)[] rows, IReadOnlyList<string>[] lines, int x, int keyW, int y)
        {
            Text(heading, x, y, Bone);
            y += headToRows;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Key.Length > 0) Text(rows[i].Key, x, y, Bone);   // the key is what the player reads — Primary, not fourteen gold accents (chrome-10)
                foreach (var line in lines[i]) { Text(line, x + keyW, y, Bone); y += pitch; }
                if (lines[i].Count == 0) y += pitch;
                y += rowGap;
            }
            return y;
        }

        var y = Column("YOUR BUILD", build, buildLines, leftX, keyLeft, top) + UiMetrics.Space(24);
        Text("THE IDEA:", leftX, y, Slate);
        y += pitch + UiMetrics.Space(12);
        foreach (var line in idea) { Text(line, leftX, y, Bone); y += pitch; }
        y += UiMetrics.Space(20);
        foreach (var line in next) { Text(line, leftX, y, Slate); y += pitch; }

        Column("SCREENS", world, worldLines, rightX, keyRight, top);

        if (scrolls)
        {
            EndChromeClip();
            var lane = UiMetrics.ScrollbarWidth;
            _ui.ScrollBar(_batch, new Rectangle(panel.Right - UiTypography.PanelPadX - lane, view.Y, lane, view.Height),
                          _helpScroll, view.Height, contentH);
        }

        // The house close icon, anchored in the corner above anything that scrolls. F1 and Esc close it
        // too. DRAWN ONLY: the press is taken in Update against the rectangle stored on the next line,
        // which is this same rectangle — see _helpClose.
        _helpClose = UiKit.CloseRect(panel);
        _ui.CloseButton(_batch, _helpClose, mouse, clicked: false);
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────────────────────
    private void Fill(Rectangle r, Color c) => _batch.Draw(_pixel, r, c);



    /// <summary>Corner brackets around a rectangle — a targeting reticle, not a full outline.</summary>
    private void Reticle(Rectangle r, Color c)
    {
        const int len = 5, t = 2;
        // four L-shaped corners
        Fill(new Rectangle(r.X, r.Y, len, t), c); Fill(new Rectangle(r.X, r.Y, t, len), c);
        Fill(new Rectangle(r.Right - len, r.Y, len, t), c); Fill(new Rectangle(r.Right - t, r.Y, t, len), c);
        Fill(new Rectangle(r.X, r.Bottom - t, len, t), c); Fill(new Rectangle(r.X, r.Bottom - len, t, len), c);
        Fill(new Rectangle(r.Right - len, r.Bottom - t, len, t), c); Fill(new Rectangle(r.Right - t, r.Bottom - len, t, len), c);
    }


    /// <summary>The art-bible Source colour, used to tint an ability icon so it reads without a name.</summary>
    private static Color SourceColor(Source s) => s switch
    {
        Source.Body => new Color(0x8C, 0x2E, 0x42),
        Source.Mind => new Color(0x3F, 0xA9, 0xC9),
        Source.Nature => new Color(0x5C, 0x8A, 0x3A),
        Source.Machine => new Color(0x9C, 0x5A, 0x32),
        Source.Shadow => new Color(0x6E, 0x5A, 0x86), // lightened Umbra so it reads on the dark panel
        Source.Spirit => new Color(0xC7, 0xBF, 0xE0),
        _ => Slate,
    };





    private void Text(string s, int x, int y, Color c) => _ui.Text(_batch, s, x, y, c);
    private void TextRight(string s, int right, int y, Color c) => _ui.TextRight(_batch, s, right, y, c);
    private void TextCenter(string s, int cx, int y, Color c) => _ui.TextCenter(_batch, s, cx, y, c);

    // ── The shared hexagonal navigation bar — drawn over every screen, driven by click OR the hotkeys. ──
    private static readonly (string Label, char Key, string Glyph)[] Nav =
    {
        // Each entry is the asset that actually carries a SYMBOL, measured rather than assumed:
        // scoring every candidate by bright-pixel fraction inside its centre disc shows the six here
        // score 0.26-0.69 while nav_forge, nav_forge_128, nav_warren and nav_warren_128 all score
        // 0.00 — ornate rings around an empty centre. FORGE and WARREN had no usable emblem in any
        // shipped set, which is why every scene audit reported the rail as half-blank; theirs are
        // generated (icon_nav_*).
        ("HUNT", 'H', "nav_hunt_128"), ("GEAR", 'C', "nav_inventory_128"),
        ("TRAINING", 'V', "state_resonance_128"), ("BUILD", 'B', "state_mastery_128"),
        // THE MASTERY TREE GETS ITS OWN DOOR. It was reachable only through a button labelled EDIT BUILD
        // on the Build overview — neither what it edits nor what anyone would go looking for — and it is
        // the second-largest system in the game. nav_build is the only unused nav emblem with a real
        // centre (bright-pixel fraction 0.31; nav_evolve and nav_codex are empty rings at 0.00 / 0.01).
        ("MASTERY", 'E', "nav_build"),
        ("VAULT", 'K', "chest_loot"), ("FORGE", 'F', "icon_nav_forge"), ("WARREN", 'A', "icon_nav_warren"),
        ("MAP", 'W', "nav_relics_128"), ("TRAITS", 'P', "nav_prestige"),
        ("ROSTER", 'R', "icon_class_hunter"),
    };

    private static readonly Color NavGold = UiInk.Accent;
    private static readonly Color NavGem = new(0x5F, 0xE0, 0xC8);
    private static readonly Color NavIdle = new(0x1E, 0x18, 0x16);
    private static readonly Color NavHover = UiInk.Hover;

    /// <summary>
    /// Mastery points: floor(sqrt(first-time depth) × 0.9) per region, summed — see MasteryPoints.
    /// </summary>
    /// <remarks>
    /// Nothing else feeds this — not the Warren, not conquest, and there are no free starting
    /// points: the tree opens at wave 25, which already pays four.
    /// </remarks>
    private int SkillPointsEarned()
    {
        // The curve lives in Core (MasteryPoints) so a test can pin it; this only feeds it the depths.
        var depths = new List<int>(Regions.All.Count);
        foreach (var def in Regions.All) depths.Add(_world.RegionFarm(def.Id).BestDepth);
        return MasteryPoints.Total(depths);
    }

    /// <summary>
    /// The world as the quest layer is allowed to see it: flat, pure, rebuilt every frame.
    /// </summary>
    /// <remarks>
    /// Derived rather than banked, exactly as both trees' points and the character unlocks are. The one
    /// exception is <c>_runsWithVowKept</c>, which is an event and has to be latched — see the run-end
    /// branch in UpdateExpedition.
    /// </remarks>
    private QuestProgress QuestSnapshot()
        => Career.QuestSnapshot(_world, _runsWithVowKept, _bossesFelled, _skillProgress.UsesBySkill());

    /// <summary>
    /// Did the descent that just ended run under a Vow whose demand the build actually met?
    /// </summary>
    /// <remarks>
    /// Not "was a Vow sworn". A Vow pays nothing while its demand is unmet, and a quest that counted
    /// sworn-but-unmet Vows would hand THE OATHBOUND to a player who never engaged with the system the
    /// character exists to reward.
    /// </remarks>
    private bool VowWasKept()
        => Career.VowWasKept(ComposeBuild(), _hunter);

    // The arithmetic (and its tuning history) is Career's, in Core, since P5.

    /// <summary>The deepest wave held in any region — what the Warren's ceiling is derived from.</summary>
    private int DeepestAnywhere() => Career.DeepestAnywhere(_world, _deepestEver);

    /// <summary>Which nav slot is lit: 0 HUNT (fight), else the open overlay.</summary>
    /// <summary>Which rail tile is lit. The Weave has no tile of its own, so it lights BUILD's.</summary>
    /// <remarks>
    /// <c>_showLoadout</c> had no case here at all and fell through to 0, so the rail cheerfully reported
    /// HUNT while the player was standing on the Weave — the game's most distinctive screen, telling
    /// them they were somewhere else. It is reached from the Build overview and is part of the same
    /// activity, so it lights that tile rather than claiming one.
    /// </remarks>
    private int NavActive() =>
        _showGear ? 1 : _showTraining ? 2 :
        // BUILD is the weave; MASTERY is the tree. Two tiles, two screens, no shared flag.
        _showMastery ? 4 : _showLoadout ? 3 :
        _showVault ? 5 : _showForge ? 6 : _showWarren ? 7 : _showWorld ? 8 :
        _showTraits ? 9 : _showRoster ? 10 : 0;

    /// <summary>
    /// Which activity each rail tile is, so one table decides both what a tile opens and whether it may.
    /// </summary>
    /// <remarks>
    /// Parallel to <see cref="Nav"/> by index, and asserted to stay that length by check_nav_gates.py —
    /// a tenth nav entry added without a tenth activity here would silently be ungated, which is the
    /// failure mode this whole gating pass exists to end.
    /// </remarks>
    private static readonly Activity[] NavActivity =
    {
        Activity.Hunt, Activity.Gear, Activity.Training, Activity.Build, Activity.Mastery,
        Activity.Vault, Activity.Forge, Activity.Warren, Activity.Map, Activity.Traits, Activity.Roster,
    };

    /// <summary>Is the rail tile at this index open to the player yet?</summary>
    /// <summary>
    /// May this tile be pressed? The ONE predicate the whole rail reads — the dimming, the chain, the
    /// requirement toast, the click and the eleven hotkeys.
    /// </summary>
    /// <remarks>
    /// The authored opening adds one term and takes one away. It ADDS a grant, because it walks the
    /// player into the VAULT and the GEAR screen at the exact moment each becomes the thing being
    /// taught — the tile has to be pressable a frame before <see cref="Unlocks"/> would say so, and
    /// the chains break on the tutorial's beat rather than on a fact's. It TAKES the rest away, because
    /// a forced step means one tile is live and the other ten are not.
    /// </remarks>
    private bool NavUnlocked(int i)
        => _opening.ForcedNav is { } only
            ? i >= 0 && i < NavActivity.Length && NavActivity[i] == only
            : NavOpen(i);

    /// <summary>
    /// Is this tile OPEN — by its own fact, or because the opening opened it early? The ART reads this.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="NavUnlocked"/> because a forced beat narrows what is PRESSABLE without
    /// changing what is OPEN. Drawing the narrowing would hang a padlock on TRAINING — a screen this
    /// player unlocked an hour ago — for the twenty seconds it takes to open a chest, which is a lie
    /// about the game's own state. The scrim is what says "not now"; the padlock says "not yet".
    /// </remarks>
    private bool NavOpen(int i)
    {
        if (i < 0 || i >= NavActivity.Length) return true;
        var activity = NavActivity[i];
        return _openingGrants.Contains(activity) || Unlocks.IsOpen(activity, GuideUnlockFacts());
    }

    /// <summary>
    /// Screens the authored opening has opened ahead of their own unlock fact.
    /// </summary>
    /// <remarks>
    /// Never a substitute for the fact — the VAULT's real gate is a chest that really exists and the
    /// GEAR screen's is an item that really exists, and both become true within a beat of the grant.
    /// This exists so the chain can break ON THE TUTORIAL'S WORD rather than a frame later, which is
    /// the difference between "the boss left you a chest, here is where it went" and two unrelated
    /// animations in the same second.
    /// </remarks>
    private readonly HashSet<Activity> _openingGrants = new();

    private void OpenNav(int i)
    {
        // A LOCKED TILE SAYS WHY, rather than doing nothing. A dead click reads as a broken button, and
        // the player learns to distrust the rail instead of learning what opens the door.
        if (!NavUnlocked(i))
        {
            var activity = NavActivity[i];
            _lockedMsg = $"{Unlocks.Headline(activity)} IS NOT OPEN YET — {Unlocks.Requirement(activity).ToUpperInvariant()}.";
            _lockedTimer = 3.2f;
            // A REFUSAL HAS ITS OWN SOUND NOW (brief sec. 86-87). It used to borrow the ordinary click
            // at low volume — "heard you, nothing happened" — which is the same cue as a button that
            // WORKED, only quieter, and a quiet yes is not a no. sfx_error is a dull, short refusal,
            // and it is the one cue every refusal in the game shares, so the sound means the thing
            // rather than the place.
            _sound.Play("sfx_error", 0.55f);
            // AND NOTHING ELSE. Deliberately no _visited.Add and no screen flag: a refused tile must
            // not read as "that screen was on top", or the tour owed to a screen the player has never
            // reached would fire against it and burn its key — which is PERSISTED (SaveGame's
            // ExplainedScreens), so the tour would be lost for good rather than for the session. Ten
            // tiles are clickable-and-refused now, where before only a re-locked one ever could be.
            return;
        }

        _showGear = _showTraining = _showMastery = _showForge = _showWarren = _showWorld = _showTraits = _showRoster = _showLoadout = _showVault = false;
        // Navigating away abandons a pending SELL/SALVAGE question. Without this it sat armed and
        // invisible, and the player's first click on returning answered a dialog they had forgotten.
        _forge.CancelConfirm();
        _training.CancelConfirm();   // an armed RESET ALL TRAINING must not survive leaving the screen
        _vault.FilterOpen = false;   // the vault's CHEST FILTER popover folds when the player walks away
        // Looked at: the tile's NEW mark goes (its banner, if any, waits on the screen until closed),
        // and the roster's "someone joined" mark is satisfied by a visit.
        _visited.Add(NavActivity[i]);
        if (NavActivity[i] == Activity.Roster) _rosterNews = false;
        // BUILD and MASTERY are two doors into one screen, so the tile also sets which VIEW it opens on.
        // Without that line the rail would be lying: pressing MASTERY while the overview was last open
        // would show the overview, and the tile would look broken rather than the state being stale.
        switch (i)
        {
            case 1: _showGear = true; break;
            case 2: _showTraining = true; break;
            // BUILD opens the weave itself. The old overview is retired: it listed the same skills
            // without letting you change one, so it was a page you looked at in front of the page
            // you used. What was unique to it — the taken mastery nodes — is the MASTERY tile's
            // diagram, drawn rather than listed.
            case 3: _showLoadout = true; break;
            // THE FRONTIER, on every open (2026-09-06): the camera lands on the path walked and the next
            // decisions, not on a poster of the whole tree; the player's own moves during the visit stand.
            case 4: _showMastery = true; _masteryScreen.Disarm(); _masteryScreen.FrameFrontier(); break;
            case 5: _showVault = true; break;
            case 6: _showForge = true; break;
            case 7: _showWarren = true; break;
            case 8: _showWorld = true; _mapScreen.ActiveRegion = _activeRegion; _mapScreen.SelectActive(); break;
            case 9: _showTraits = true; break;
            case 10: _showRoster = true; break;
            // case 0 HUNT: everything cleared above → back to the fight.
        }

        // THE RAIL HAS A VOICE (brief sec. 86-87): one soft page tick for every arrival, so moving
        // through the game sounds like turning pages rather than like pressing the same button that
        // buys things. Played here rather than in the click handler, so the hotkeys sound like the
        // tiles do — they are the same act.
        _sound.Play("sfx_nav", 0.6f);
    }

    /// <summary>Width of the vertical navigation rail down the left edge.</summary>
    internal const int NavRailWidth = 180;

    // ── Overlay inset ───────────────────────────────────────────────────────────────────────────
    //
    // Every menu screen (GEAR, STATS, BUILD, FORGE, WARREN, MAP, DUST) was laid out against a full
    // 1920-wide canvas, back when navigation was a bar across the bottom. The nav rail is vertical now
    // and owns x 0..180, so each of those screens had its leftmost panel sliced in half — the Hunter
    // summary on STATS read "KER / EL 3 / OWER".
    //
    // Rather than re-derive several hundred hand-placed literals per screen (and re-derive them again
    // the next time the rail changes width), the whole screen is drawn through one transform: uniformly
    // scaled to the free width and pushed right of the rail. Uniform, so no medallion turns into an
    // ellipse; one transform, so layout and hit-testing cannot drift apart.
    //
    // The fight screen is NOT inset — its layout was rebuilt around the rail directly, and its arena
    // wants every pixel of the free space.

    /// <summary>Free width right of the nav rail, as a fraction of the authored 1920 — the page scale at UI SCALE 100%.</summary>
    internal const float BaseOverlayScale = (1920f - NavRailWidth - 20f) / 1920f;

    /// <summary>
    /// THE PAGE SCALE: what one logical page pixel is in canvas pixels. The overlay matrix, the cursor's
    /// inverse (Core's PageFrame) and the font density all read it. It no longer multiplies by a UI SCALE
    /// factor: UI SCALE is a density profile (<see cref="UiMetrics"/>) and the page keeps its size.
    /// </summary>
    internal static float OverlayScale => BaseOverlayScale;

    /// <summary>The UI SCALE preference: 100, 125, 150, or 0 for AUTO. Resolved in <see cref="ApplyUiScale"/>.</summary>
    private int _uiScalePercent = 100;

    /// <summary>
    /// Resolve the UI SCALE preference against the present rect and push it to everything that reads it.
    /// </summary>
    /// <remarks>
    /// Called from <see cref="RecomputePresent"/>, the one place the present rect changes, so AUTO follows a
    /// window resize without anyone remembering to ask. Nothing else moves: the page and the overlay matrix
    /// are the same at every profile, and the screens reflow their rows to the bigger type.
    /// </remarks>
    private void ApplyUiScale()
    {
        // A DENSITY profile (UiMetrics), not a page zoom: the page stays 1920×1080 and the overlay
        // matrix stays BaseOverlayScale at every step. The first UI SCALE shrank UiKit.Page to 1920/f
        // and every screen laid out for 1080 px overflowed at 150 % (UX V2 REPORT §4).
        UiMetrics.Apply(Display.ResolveUiScale(_uiScalePercent, _present.Width));
        // WHERE THE PAGE STARTS, at this profile: under the subtitle line and the hint slot's least
        // line, plus a breath — never above the 150 every screen was laid out for at 100 %. At 100 %
        // this is 150 exactly (the band holds a 41 px slot); at 150 % it is about 200, which is what
        // stops the slot landing on the VAULT's toolbar (see UiKit.PageTop).
        UiKit.PageTopBase = Math.Max(PageContentTopBase,
            PageSubtitleBottom + (int)MathF.Ceiling(HintLineMin / OverlayScale) + UiMetrics.Space(6));
        UiKit.PageTop = UiKit.PageTopBase + UiKit.NoticeLane;
    }

    /// <summary>
    /// Is the band above the page owed to a slot reveal rather than to a notice?
    /// </summary>
    /// <remarks>
    /// Only a slot card WITH A BODY holds it. A one-line hint fits inside the band the page already
    /// reserves and leaves the notice its own room, so it queues nothing.
    /// </remarks>
    private bool NoticeHeld => OverlayActive && SlotShowing() is { Body.Length: > 0 };

    /// <summary>The notice lane's motion key — its height eases open and shut.</summary>
    private static readonly int NoticeLaneKey = HashCode.Combine("notice", "lane");

    /// <summary>How open the lane is, 0..1 — the toast's own alpha follows it, so the two arrive together.</summary>
    private float _noticeLaneOpen;

    /// <summary>The lane's full height in canvas pixels while it opens and shuts — the last notice's, so the ease has a number to close from.</summary>
    private int _noticeLaneFull;

    /// <summary>
    /// Reserve the page's NOTICE LANE for this frame: the notice toast's height while one is showing on
    /// a menu screen, eased over a Transition, and nothing on the hunt (the hunt has its own overlay).
    /// </summary>
    /// <remarks>
    /// Called every frame before the screens lay out. The body follows <see cref="UiKit.PageTop"/>, so
    /// a notice pushes the first row of panels down instead of covering it (the follow-up brief §6); the
    /// ease is what stops that from being a jump, and Reduced Motion collapses it to a fade in place.
    /// </remarks>
    private void ReserveNoticeLane()
    {
        // Only while the toast itself may paint (DrawNoticeToast asks the owner the same way): a lane
        // opened for a toast nobody may read pushed every page down under the opening's cards — at 150 %
        // it left the GEAR doll that BACK TO THE HUNT lights no height at all (autoplayed opening,
        // 2026-09-11). The owner is last frame's here, at the top of Update; the lane eases anyway.
        var showing = OverlayActive && !AttentionOwnedAbove(AttentionOwner.Feedback)
                      && _noticeTimer > 0f && _notice.Head.Length > 0 && !NoticeHeld;
        if (showing) _noticeLaneFull = NoticeToastHeight() + UiMetrics.Space(8);
        _noticeLaneOpen = UiMotion.Ease(NoticeLaneKey, showing ? 1f : 0f, UiMotion.Transition);
        if (!showing && _noticeLaneOpen <= 0f) _noticeLaneFull = 0;

        // ...AND THE SLOT HAS A LANE OF ITS OWN, for the same reason. The band above the page reserves
        // room for a ONE-LINE slot (UiKit.PageTopBase), which is all the slot ever held while it carried
        // hints; a card with a BODY is a title plus a wrapped line and reaches past it. The old answer
        // was that it may cover the top of the screen's content — fine for a caption, wrong for a
        // CONTROL, and the VAULT puts OPEN ALL, CHEST FILTER and TRADER in exactly that band. A lesson
        // that half-covers the button it is telling you to press is worse than no lesson.
        var wantSlot = SlotLanePage();
        if (wantSlot > 0) _slotLaneFull = wantSlot;
        _slotLaneOpen = UiMotion.Ease(SlotLaneKey, wantSlot > 0 ? 1f : 0f, UiMotion.Transition);
        if (wantSlot == 0 && _slotLaneOpen <= 0f) _slotLaneFull = 0;

        UiKit.NoticeLane = Math.Max((int)MathF.Ceiling(_noticeLaneFull * _noticeLaneOpen / OverlayScale),
                                    (int)MathF.Ceiling(_slotLaneFull * _slotLaneOpen));
        UiKit.PageTop = UiKit.PageTopBase + UiKit.NoticeLane;
    }

    /// <summary>The slot's motion key — its lane eases open and shut like the notice's.</summary>
    private static readonly int SlotLaneKey = HashCode.Combine("slot", "lane");

    /// <summary>How open the slot's lane is, 0..1.</summary>
    private float _slotLaneOpen;

    /// <summary>The slot lane's full height in PAGE pixels while it opens and shuts.</summary>
    private int _slotLaneFull;

    /// <summary>
    /// How far past the band a slot WITH A BODY reaches, in page pixels — the room the page owes it.
    /// </summary>
    /// <remarks>
    /// Measured against <see cref="UiKit.PageTopBase"/> and never against <see cref="UiKit.PageTop"/>:
    /// the lane is added to PageTop, so reading PageTop here would make the lane feed itself. A one-line
    /// hint returns zero by construction — the band is sized for exactly that — so only a note or a
    /// lesson card moves the page, and only while it is up.
    /// </remarks>
    private int SlotLanePage()
    {
        if (SlotShowing() is not { } slot || slot.Body.Length == 0) return 0;
        var bottom = ScreenBannerTop(ScreenDrawsSubtitle)
                     + CardHeight(_ui.WrapBig(slot.Body, ScreenBannerWrap, UiTypography.Secondary).Count);
        return Math.Max(0, (int)MathF.Ceiling(bottom / OverlayScale) + UiMetrics.Space(6) - UiKit.PageTopBase);
    }

    /// <summary>The notice toast's height for the notice showing now, from its own lines.</summary>
    private int NoticeToastHeight()
    {
        var pad = UiMetrics.Space(22);
        var room = NoticeToastWidth - UiMetrics.Space(60);
        var body = !_notice.Awakening && _notice.Detail.Length > 0
            ? _ui.WrapBig(_notice.Detail, room, UiTypography.OverlayBody).Take(NoticeBodyLines).Count()
            : 0;
        var rungs = _notice.Awakening
            ? UiTypography.Pitch(UiTypography.OverlayBody) + UiTypography.Pitch(UiTypography.OverlayTitle) + UiTypography.Pitch(UiTypography.OverlayBody)
            : UiTypography.Pitch(UiTypography.OverlayTitle) + UiTypography.Pitch(UiTypography.OverlayBody) * Math.Max(1, body);
        return pad + rungs + UiMetrics.Space(12);
    }

    /// <summary>Dev (F8 under RH_DEV): step the UI SCALE 100 → 125 → 150 → AUTO → 100 and keep it.</summary>
    private void CycleUiScale()
    {
        var steps = Display.UiScaleSteps;
        _uiScalePercent = steps[(Array.IndexOf(steps, _uiScalePercent) + 1) % steps.Length];
        ApplyUiScale();
        SaveDisplay();
    }

    /// <summary>Canvas x the inset content starts at.</summary>
    internal const float OverlayLeft = NavRailWidth;

    // DERIVED from the table, not a literal. It was 1080/8 with a comment saying "eight items fill the
    // full height exactly", which was true right up until the roster added a ninth and the last tile
    // hung 135px off the bottom of the screen.
    private static readonly int NavTileHeight = 1080 / Nav.Length;

    // ── THE TILE'S GRID moved to NavTileGrid (2026-09-12). The label sits on the tile's foot and the
    //    icon takes what is left above it, so the two cannot print through each other at any profile —
    //    but the CHAIN on a locked tile laid itself out from the raw tile rectangle instead, and at
    //    150 % its padlock came to rest on the first letter of a short label. Two things laying out one
    //    tile from two rules is the fault; NavTileGrid.Of is now the only rule, and NavChain reads it.
    /// <summary>The room a label leaves at each side of the tile — the rail's 3 px seam and its mirror.</summary>
    private const int NavLabelInset = NavTileGrid.LabelInset;

    private static Rectangle NavHexRect(int i)   // a rectangular TILE (guide: package_01 nav tiles)
    {
        // Vertical rail down the LEFT edge. It used to be eight 240px sections along the bottom
        // (0, 934, 1920, 146), which ate a full band of the stage and cut the actors off at the shins.
        // Stacked on the left it frees that height back to the arena.
        return new Rectangle(0, i * NavTileHeight, NavRailWidth, NavTileHeight);
    }

    private void DrawHexNav()
    {
        // A modal owns the frame — and so does the open EXPEDITION LOG, a full-screen read the rail cannot
        // answer under (HandleNavClick and the hotkeys refuse), so it is not painted as if it could.
        if (HostModalUp || _expedition.LogOpen) return;

        // A dark shelf so the bar seats cleanly over whatever screen sits behind it. Nearly opaque and
        // starting a hair above the hexes, so the scene behind can't show through and clip their tops.
        // 1920-space (scale-1 chrome): shelf top 234→936, full 1920 width, 36→144 tall.
        // Rev 4 §22: ONE shared QUIET rail — a dark background + thin dividers, NOT an ornate panel per item.
        // Only the active item gets ornate emphasis (ui_tab_active + purple tint, full-contrast icon/label);
        // inactive items are a quiet glyph + label at ~75% opacity.
        // Fully opaque. At 96% the rail let whatever a screen happened to draw underneath bleed through as
        // ghost shapes; chrome should never show the scene behind it.
        _ui.Fill(_batch, new Rectangle(0, 0, NavRailWidth, 1080), new Color(0x0F, 0x0B, 0x0B));
        // Seam runs down the rail's trailing edge now that the rail is vertical.
        _ui.Fill(_batch, new Rectangle(NavRailWidth - 3, 0, 3, 1080), NavGem * 0.4f);

        var active = NavActive();
        var navFacts = GuideUnlockFacts();
        var navGems = GemsHeld();            // the first-gem lesson marks the FORGE tile the same way
        // ONLY THE REVEALED TILES, stacked from the top in the rail's own order (the journey,
        // 2026-09-06). A tile the player has never had a reason to use is not drawn: a fresh save's
        // rail is THE HUNT and nothing else, and each screen arrives — with its notice and its NEW —
        // when the need that opens it is real. Tiles keep their size (NavTileHeight is the full
        // rail's), so the rail fills downward as the game opens rather than re-spacing. A revealed
        // tile whose gate has since closed still draws, dimmed, with its price on hover.
        var slots = NavSlots();
        for (var slot = 0; slot < slots.Count; slot++)
        {
            var i = slots[slot];
            var r = NavHexRect(slot);
            var on = i == active;
            var hover = r.Contains(ChromeMouse);
            var unlocked = NavOpen(i);
            var activity = NavActivity[i];
            // Dividers are horizontal between stacked tiles, not vertical between side-by-side ones.
            if (slot > 0) _ui.Fill(_batch, new Rectangle(r.X + 26, r.Y, r.Width - 52, 2), new Color(0x22, 0x1C, 0x30));

            if (on)
            {
                // Sliced, not stretched: the tab art is 256×96 and the tile is 180×98, so a whole-image
                // draw squashed its end scrollwork 0.7× one way and 1.02× the other (release polish).
                if (_assets.Get("ui_tab_active") is { } tab) _ui.SliceFrame(_batch, tab, r, Color.White);
                else if (_assets.Get("ui_button_primary") is { } bp) _ui.SliceFrame(_batch, bp, r, Color.White);
                else _ui.Fill(_batch, r, UiInk.Raised);
                // The lit tile's interior is a GOLD wash now (2026-09-06) — selected is gold everywhere
                // else in the game; the purple wash was the last of the old violet chrome on the rail.
                _ui.Fill(_batch, r, UiInk.Accent * 0.12f);
            }
            else if (hover)
            {
                _ui.Fill(_batch, r, UiInk.Accent * 0.06f);
            }

            // Icon above, label below, both centred in the shorter tile. ONE GRID — NavTileGrid.Of is
            // the whole rule, and the locked tile's chain reads the same one (see NavChain).
            var iconTint = !unlocked ? Color.White * 0.22f : on ? Color.White : Color.White * 0.75f;
            var grid = NavTileGrid.Of(r);
            var labelH = grid.LabelHeight;
            var labelY = grid.LabelTop;
            var iconBox = grid.Icon;
            if (_assets.Get(Nav[i].Glyph) is { } g)
                _batch.Draw(g, iconBox, iconTint);
            else
                _ui.Diamond(_batch, new Rectangle(iconBox.X + 4, iconBox.Y + 4, iconBox.Width - 8, iconBox.Height - 8), on ? NavGold : NavGem * 0.75f);

            // The label fits the tile or says so with an ellipsis — it is never shrunk; the rung is the rung.
            // The house Secondary ink for an inactive label — the private lilac at 90 % sat under the
            // 6:1 floor and in a hue no token has (chrome-12).
            _ui.TextCenterBig(_batch, _ui.ShortenBig(Nav[i].Label, NavRailWidth - NavLabelInset * 2, labelH), r.Center.X, labelY,
                              !unlocked ? UiInk.Secondary * 0.4f : on ? NavGold : UiInk.Secondary, labelH);

            // ── A LOCKED TILE IS CHAINED SHUT, and the chains SPRING OFF when the screen opens. ─────
            //
            // Two designs were rejected before this one: a RING around the icon (a decoration ON the
            // icon, not a restraint on the TILE), then ONE STRAIGHT RUN across the tile at the icon's
            // height (a belt, whose break slid two halves sideways). The tile is bound the way a door
            // is now — two runs on different diagonals across the whole button (NavChain owns the
            // geometry, and the Game tests pin it) — and AFTER THE LABEL, because a chain on a button
            // lies over what is printed on it.
            //
            // THE BREAK IS DRAWN ON AN UNLOCKED TILE, which is the only kind of tile it can happen to.
            // Armed by Reveal.Newly, and Reveal.Newly fires on the frame the gate OPENS — so a break
            // guarded by `if (!unlocked)` was a flourish that could never once play: by the time it was
            // armed the branch that reads it was already closed.
            var breaking = UiMotion.Pulse(NavBreakKey(activity));
            if (!unlocked || breaking > 0f)
                DrawNavChain(r, breaking > 0f ? 1f - breaking : 0f);   // 0 while bound, 1 once the tile is clear

            // ── THE HUNT TILE IS ALIVE, because the fight is (playtest 2026-09-09: "it wasn't clear
            //    that the HUNT screen is the main combat screen while navigating other menus"). Three
            //    signals, none of them a play-by-play: a thin health bar in the tile's own foot band,
            //    a red wash when the champion is bitten, and a gold one when a wave falls. Only while
            //    the player is somewhere ELSE — on the hunt itself all three would be a second, worse
            //    copy of the arena's own readouts.
            if (activity == Activity.Hunt && !on)
            {
                var hurt = UiMotion.Pulse(NavHuntHurtKey);
                if (hurt > 0f) _ui.Fill(_batch, r, UiInk.Danger * (0.30f * UiMotion.Smooth(hurt)));
                var cleared = UiMotion.Pulse(NavHuntClearKey);
                if (cleared > 0f)
                    _ui.Fill(_batch, r, UiInk.Accent * ((_navHuntBoss ? 0.30f : 0.18f) * UiMotion.Smooth(cleared)));

                // THE BAR SITS IN THE FOOT BAND the label already leaves (NavLabelFoot is unscaled, so
                // this room exists at every density profile and nothing else has to move).
                var life = Math.Clamp(_expedition.ChampionHealthFraction, 0f, 1f);
                var barW = NavRailWidth - 52;
                _ui.Bar(_batch, r.X + 26, r.Bottom - 7, barW, 4, life,
                        _expedition.ChampionDowned ? UiInk.Danger : UiInk.Good);
            }

            // The price, on the tile, so the rail teaches the progression without being clicked. Hover
            // only — nine requirement lines drawn permanently is the wall this pass exists to remove.
            // It hangs off the label's foot (Bottom-16 at 100 %), under the caps' ink, at every profile.
            if (!unlocked && hover)
                _ui.TextCenterBig(_batch, _ui.ShortenBig(Unlocks.Requirement(NavActivity[i]), NavRailWidth - UiMetrics.Space(24), UiTypography.Secondary),
                                  r.Center.X, labelY + labelH - UiMetrics.Space(6), UiInk.Secondary, UiTypography.Secondary);


            // A GOLD "NEW" MARK on a tile that is open with something unread on it — the quiet
            // replacement for the modal panel that used to announce every opening. On the LEFT of
            // the tile, because the VAULT's red chest count owns the right, and both can be true of
            // the VAULT on the frame it opens. Never on the lit tile: the player is already there, and
            // the banner at the top of that screen is the mark's payload. A tile visited this session
            // drops its mark even with the banner still open — the mark means "you have not looked".
            // A rung ABOUT this tile's screen marks it for as long as the rung shows — visited or not; the
            // lesson is waiting on that screen, and the mark is how the rail says so (UX V2 P0.7).
            var rungSends = _coach.Showing is { } rung && OnboardingLessons.Sends(rung) == activity;
            var isNew = !on && unlocked
                        && (((Onboarding.IsNew(activity, navFacts, _explained)
                              || (activity == Activity.Forge && Onboarding.GemTourDue(navGems, _explained) is not null))
                             && !_visited.Contains(activity))
                            || rungSends
                            || (activity == Activity.Roster && _rosterNews));
            if (isNew)
            {
                // ── AN UNREAD DOT, NOT A GOLD CHIP. ─────────────────────────────────────────────
                //
                // Playtest 2026-09-09: "nobody pays any attention to the notification messages at the
                // top — we cannot put important things there. It needs to be more noticeable. It could
                // even be given as a notification on the relevant screen's left-panel button: a red dot
                // over the newly opened thing."
                //
                // The mark existed and was a plate with the word NEW in the ACCENT — the same gold as
                // the rail's own lit tile, its labels, its frames and half the game's chrome. A gold
                // mark on a gold rail is camouflage. Red appears nowhere else on the rail, and a dot on
                // a menu button is the one notification idiom every player already owns.
                //
                // It is a state, not an animation: it holds until the screen is visited, so it says
                // what a six-second toast could not. Under Reduced Motion it simply does not breathe.
                var dot = Math.Max(8, UiMetrics.Control(14));
                var at = new Rectangle(Math.Min(r.X + UiMetrics.Space(12), grid.Icon.Left - NavTileGrid.IconGap - dot),
                                       Math.Min(r.Y + UiMetrics.Space(14), labelY - NavTileGrid.IconGap - dot), dot, dot);
                // A dark seat first, so the dot reads on the tile's own art rather than merging with it.
                _ui.Disc(_batch, new Rectangle(at.X - 2, at.Y - 2, at.Width + 4, at.Height + 4), UiInk.Ground * 0.85f);
                _ui.Disc(_batch, at, UiInk.Danger);
                if (!UiMotion.Reduced)
                {
                    // One slow breath — a halo that grows and fades on a two-second cycle. It draws the
                    // eye on a still screen without ever moving the dot itself.
                    var beat = (MathF.Sin(_navDotClock * MathF.PI) + 1f) * 0.5f;
                    var halo = (int)MathF.Round(dot * (0.35f + 0.45f * beat));
                    _ui.Disc(_batch, new Rectangle(at.X - halo / 2, at.Y - halo / 2, at.Width + halo, at.Height + halo),
                             UiInk.Danger * (0.30f * (1f - beat)));
                }
            }

            // UNOPENED CHESTS, as a count on the VAULT tile — the page chests actually live on.
            //
            // It was on FORGE, correctly, when the Forge was the only place a chest existed: they sat
            // three clicks deep (FORGE, then SALVAGE mode, then a toolbar row) with nothing anywhere
            // saying one was waiting, and the most valuable thing the game gives you should not be the
            // hardest to find. The Vault took that job, and a badge pointing at the old address is a
            // signpost to the wrong door — worse than none, because the player follows it.
            // ...AND UNSPENT SKILL LEVELS, as the same count on the BUILD tile. The one decision the
            // game never told anyone they could make: a skill's variation and its reinforcements sit at
            // the foot of a scrolling column, behind a click on a slot, with no mark anywhere saying
            // one is waiting (playtest 2026-09-09: "no one would have even looked at it if I hadn't
            // pointed it out"). This is the established idiom for "something valuable is behind this
            // door" and it already had exactly one user.
            var pending = activity == Activity.Vault ? _forge?.UnopenedChests.Count ?? 0
                        : activity == Activity.Build && unlocked ? UnspentSkillLevels()
                        : 0;
            if (pending > 0)
            {
                var n = pending;
                var count = n > 9 ? "9+" : n.ToString();
                // A badge sized from its count: the caption rung plus a pad (38 × 30 at 100 %). Top-right,
                // clear of the icon and the label the same way the NEW mark is — at 150 % it sat across
                // the label's last letter.
                // A chip, not a red square: a reward waiting was coloured like a failure, and at 150 % the
                // badge out-sized the chest it counted (chrome-07). The plate with the accent rule, the
                // count in Primary, sized from the count alone, and never nearer the icon than a breath.
                var badgeH = UiTypography.Secondary + UiMetrics.Space(7);
                var badgeW = _ui.MeasureBig(count, UiTypography.Secondary) + UiMetrics.Space(16);
                var badge = new Rectangle(Math.Max(r.Right - UiMetrics.Space(12) - badgeW, grid.Icon.Right + UiMetrics.Space(6)),
                                          Math.Min(r.Y + UiMetrics.Space(14), labelY - NavTileGrid.IconGap - badgeH), badgeW, badgeH);
                _ui.Plate(_batch, badge, UiInk.Accent);
                _ui.TextCenterBig(_batch, count, badge.Center.X + 2, badge.Y + (badgeH - UiTypography.Secondary) / 2,
                                  UiInk.Primary, UiTypography.Secondary);
            }
        }
    }

    /// <summary>Levels earned on woven skills and not yet spent — the BUILD tile's badge.</summary>
    /// <remarks>
    /// Woven skills only, so it can never point at a decision the player is not currently fighting
    /// with, and derived every frame from the one ledger that owns it.
    /// </remarks>
    private int UnspentSkillLevels()
    {
        if (_skillProgress is null) return 0;
        var total = 0;
        foreach (var woven in _loadout.Skills)
            if (woven.SkillId is { } id) total += _skillProgress.FreeOn(id);
        return total;
    }

    private void HandleNavClick()
    {
        if (HostModalUp || _expedition.LogOpen) return;   // the panels and the open log hold the rail (see navHolds)
        // A FORCED NAVIGATION LETS EXACTLY ONE TILE THROUGH the authority that killed the rest. The
        // tile is the game's own, in its own place, taking its own click — there is no tutorial-only
        // button in this design — and a press on any OTHER tile is swallowed in silence rather than
        // answered with "GEAR IS NOT OPEN YET" while the card says OPEN THE VAULT.
        var forced = _opening.ForcedNav;
        if (!MouseClicked && !(forced is not null && _clicked && !WelcomeUp)) return;
        // NavHexRect is 1920-space chrome now, so hit-test the 1920-space cursor — slot by slot, the
        // same mapping the drawing used, so what lights up is what takes the click.
        var slots = NavSlots();
        for (var slot = 0; slot < slots.Count; slot++)
            if (NavHexRect(slot).Contains(ChromeMouse))
            {
                if (forced is { } only && NavActivity[slots[slot]] != only) return;
                OpenNav(slots[slot]);
                return;
            }
    }

    /// <summary>The rail's tiles, top to bottom: the indices into <see cref="Nav"/> of every revealed activity.</summary>
    /// <summary>
    /// Which tiles the rail draws — ALL of them, always, in the rail's own order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to filter by <c>_revealed</c>, so a fresh save's rail was THE HUNT and nothing else
    /// and every later tile shifted the ones under it up a slot as it arrived. Playtest 2026-09-09:
    /// <i>"We removed the screen buttons from the left panel, having them appear only later, but
    /// that's a bad approach because the player doesn't know something will be added there. All
    /// buttons should remain visible, perhaps with a chain overlay indicating they are locked."</i>
    /// </para>
    /// <para>
    /// <b>This reverses a decision made three days earlier, and the two complaints are not actually in
    /// conflict.</b> The earlier one (<c>Unlocks</c>: <i>"Hemen oyunun içine atıldım ve bilmeme rağmen
    /// kafam karıştı"</i> — nine live destinations at once is not a menu, it is a wall) was about
    /// COGNITIVE LOAD AT THE POINT OF USE. This one is about the ABSENT MAP: a rail that grows gives no
    /// sense of the shape of the game. So: the SHAPE is revealed from the first frame, the CAPABILITY
    /// is still gated. Eleven tiles always drawn, the unearned ones visibly bound, each saying its own
    /// price on hover and refusing out loud when clicked.
    /// </para>
    /// <para>
    /// <b><c>_revealed</c> is NOT deleted</b> — it stops being a draw filter and becomes what it was
    /// always half doing: the ledger behind <c>Reveal.Newly</c>'s notice and the NEW mark's "you have
    /// not looked" reading. Removing it would silence every reveal in the game.
    /// </para>
    /// </remarks>
    /// <summary>The gold tick on the HUNT tile whenever a wave is cleared, from any screen.</summary>
    private static readonly int NavHuntClearKey = HashCode.Combine("nav.hunt.clear");

    /// <summary>The red flash on the HUNT tile when the champion is bitten, from any screen.</summary>
    private static readonly int NavHuntHurtKey = HashCode.Combine("nav.hunt.hurt");

    /// <summary>Was the wave that just cleared a boss? Its tick is brighter and lasts longer.</summary>
    private bool _navHuntBoss;

    /// <summary>Last frame's "seconds since bitten", so the flash fires on the bite rather than every frame.</summary>
    private float _navHuntSinceHit = 99f;

    /// <summary>The one-shot armed when a screen opens — the tile's chain springs off over its life.</summary>
    private static int NavBreakKey(Activity a) => HashCode.Combine("nav.break", (int)a);

    /// <summary>
    /// The chains on a rail tile that is not open yet, and the one-shot that springs them off.
    /// </summary>
    /// <param name="tile">The whole 180x98 tile — the chains span it, because the tile is what is bound.</param>
    /// <param name="progress">0 while bound, rising to 1 over the break; at 1 the tile is clear.</param>
    /// <remarks>
    /// Only the host's half: which tile, how far along, and the batch. Where every link lies, which way
    /// each half recoils and what Reduced Motion keeps is <see cref="NavChain"/>'s, a pure function the
    /// Game tests pin (nav_chain_test.cs) — a one-shot that plays once per screen per career is exactly
    /// the state a screenshot alone has let this project ship wrong.
    /// </remarks>
    private void DrawNavChain(Rectangle tile, float progress)
    {
        // AT REST EVERY LINK LIES WHOLLY ON THE TILE (NavChain lays them so), and the batch is left alone.
        // A break throws its halves out across the tile's edges, so a break alone pays for a clipped
        // batch — two boundaries, for nine tenths of a second, once per screen per career — and nothing
        // it throws lands on the neighbouring tile or out on the page.
        if (progress > 0f) BeginChromeClip(tile);
        NavChain.Draw(_batch, _ui, tile, NavChain.LinkLength, progress, UiMotion.Reduced);
        if (progress > 0f) EndChromeClip();
    }

    /// <summary>
    /// How long the chain's break runs, in seconds.
    /// </summary>
    /// <remarks>
    /// It was 0.55, which was the eight-frame strip's length — at eight frames a second that reached
    /// frame four, so the strip's last three frames had never once been drawn. There is no strip now
    /// and this is simply how long the two halves take to leave: long enough to be seen as an event
    /// beside the notice that says what opened, short enough not to hold up the rail.
    /// </remarks>
    private const float NavChainSeconds = 0.9f;

    private List<int> NavSlots()
    {
        var slots = new List<int>(Nav.Length);
        for (var i = 0; i < Nav.Length; i++) slots.Add(i);
        return slots;
    }
}
