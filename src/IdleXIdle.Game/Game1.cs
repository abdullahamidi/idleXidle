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
public class Game1 : Microsoft.Xna.Framework.Game
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
    private static readonly Color VoidInk = new(0x1B, 0x16, 0x20);

    /// <summary>The letterbox bars — the backbuffer outside the canvas.</summary>
    /// <remarks>
    /// One step darker than <see cref="VoidInk"/> (art-bible §4.1's #1B1620) on purpose. Void Ink is a
    /// SURFACE colour: it is what the canvas itself clears to, so bars painted in it read as more game
    /// — a panel the content failed to fill. The bar is not game at all, and the darkest tone in the
    /// palette is the one that says so and stops the eye at the canvas edge.
    /// </remarks>
    private static readonly Color LetterboxInk = new(0x14, 0x10, 0x1A);
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;

    /// <summary>A soft, dark panel tone for HUD bands — lighter than the void so UI sits on a surface,
    /// not floating text on black. This is most of what "softer" means at this resolution.</summary>
    private static readonly Color PanelBg = new(0x24, 0x20, 0x2C);

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
    /// (Enter on the MAP) underneath the scrim (review 2026-08-26).
    /// </summary>
    private KeyboardState ScreenKeys => _tourActive ? default : _keys;
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
    private TutorialStep? _guideStep;

    /// <summary>Toast for clicking a rail tile that is not open yet — it names its own price.</summary>
    private string _lockedMsg = "";
    private float _lockedTimer;

    // ── The intro, and the on-demand explanations that replaced the modal panels ─────────────
    //
    // THERE USED TO BE A QUEUE OF MODAL PANELS HERE. Every screen that opened, every skill slot, every
    // finished quest and every champion that joined was pushed onto it, and the panels dripped out one
    // every eight seconds, each one swallowing input until clicked. Playtest: "while a player is focused
    // on solving something, the screen keeps throwing 'this opened, that arrived' notifications, and it
    // wore the testers out." The queue, the panel, its cooldown and everything that fed them are gone —
    // not disabled, gone — and replaced by three quiet channels:
    //
    //   * THE TOURS (Onboarding.TourFor): a click-through spotlight walk of a screen, one region lit
    //     at a time, while the screen keeps working underneath. The HUNT's runs once, on the first
    //     BEGIN THE HUNT (the intro); every other screen's runs the first time it is on top. A screen
    //     that opens gets a gold NEW mark on its rail tile, and its tour waits there until the player
    //     goes. (The other screens used to get a dense banner instead — playtest: "too crowded and
    //     too small. The Hunt screen's walkthrough was much clearer.")
    //   * THE SLOT NOTE: the one banner left — a new skill slot's line at the top of the BUILD
    //     screen, after its tour, closed with one click.
    //   * NOTICE TOASTS: a quest finishing or a champion joining is a line at the top that fades on its
    //     own. It never takes input.

    /// <summary>Is a tour on screen? While true, input belongs to it and nothing else teaches.</summary>
    private bool _tourActive;

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
    /// Once, deliberately: the decision reads "has a wave been cleared", and the first wave clears
    /// roughly fifteen seconds in — asking every frame would end the intro under the player mid-card.
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

    /// <summary>True while one of the host's own modals is up — the two that fade in.</summary>
    private bool ModalUpNow => _showSettings || _showHelp;

    // ── THE CURRENCY PILLS REACT (brief sec. 36-38) ─────────────────────────────────────────────
    //
    // Spending was silent: the number was one figure on one frame and a smaller figure on the next,
    // in the corner of the screen furthest from the button that spent it. sec. 37 asks the SPEND to
    // react at the pill rather than fly resources across the page, and sec. 36 asks the number to
    // move to its new value rather than jump. sec. 38 allows a "+N" for a substantial GAIN — with a
    // threshold, because Gleam arrives a few at a time all through a hunt and a badge on every kill
    // is not feedback, it is weather.

    /// <summary>The three pills' true values as of the last Update — the tick's target.</summary>
    private readonly long[] _pillTrue = new long[3];

    /// <summary>What each pill is currently PRINTING; eases to <see cref="_pillTrue"/>.</summary>
    private readonly double[] _pillShown = new double[3];

    /// <summary>Seconds left of a pill's reaction flash.</summary>
    private readonly float[] _pillFlash = new float[3];

    /// <summary>Gains banked but not yet worth announcing, per pill.</summary>
    private readonly long[] _pillGainAcc = new long[3];

    /// <summary>The gain each pill is announcing, and the seconds left of that announcement.</summary>
    private readonly long[] _pillGainShow = new long[3];
    private readonly float[] _pillGainT = new float[3];

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
    private static readonly long[] PillGainFloor = { 25, 25, 100 };   // scrap, dust, gleam

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
    private TraitsScreen _traits = null!;

    /// <summary>
    /// THE TRAITS SCREEN (P4): three worn characteristics, the collection, and the unknown.
    /// </summary>
    /// <remarks>
    /// The nav's TRAITS destination is this screen now. <see cref="_traits"/> — the old Memory tree —
    /// is still built and is still reachable through the capture rig (RH_SHOT_MODE=dusttree), because
    /// P5 is moving the keystone and vow producers off it in parallel and it is deleted whole when
    /// both land. Until then it is the only producer of those two catalogues, which is the one thing
    /// this phase deliberately leaves standing.
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
    /// DEV ONLY: draw the OLD Memory tree instead of the TRAITS screen (RH_SHOT_MODE=dusttree).
    /// </summary>
    /// <remarks>
    /// Never set at play. The tree is not the trait system any more (BRIEF §22), but it is still the
    /// only producer of the nineteen keystones and the thirteen vows until P5 moves both off it, so
    /// this phase leaves the class standing and photographable rather than deleting a screen whose
    /// replacement is still being written in another branch.
    /// </remarks>
    private bool _showDustTree;
    private MemoryDustTree _dust = new();

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
    private bool WelcomeUp => _showWelcome && !_tourActive;
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
        // developer's machine happens to be set to.
        if (Environment.GetEnvironmentVariable("RH_SHOT") is null)
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

        SaveSystem.RestoreHunter(save, _hunter);   // (the Merge→Salvage charter fold lives inside — Core, tested)
        _dust.Restore(save.MemoryDust, save.MemoryDustUnlocks);

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
        // save.LearnedSkills IS NO LONGER READ. It was the permanent-discovery latch (D7): a skill
        // taught by a road stayed usable after the points moved on. Access now follows the CURRENT
        // allocation (BRIEF sec.15-17), so the taken set above is the whole of it. The field is still
        // WRITTEN for one version, so a player who rolls back to the previous build does not lose
        // their skills on the way — see Save().
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
        // The guide rungs the player closed by hand — names, so a reordered enum can never
        // dismiss a different lesson. A plain field, so restoring here (Initialize) is safe.
        _dismissedGuide.Clear();
        foreach (var rung in save.DismissedGuideRungs) _dismissedGuide.Add(Tutorial.ModernRungName(rung));
        // The intro flag is a plain field; the explained list is PARKED, because seeding it asks the
        // unlock gates, and those read the Forge's inventory — a screen LoadContent has not built yet.
        // Seeded in SeedExplained, from ApplyRestoredState, once the bag is real.
        _introSeen = save.IntroSeen;
        _pendingExplained = save.ExplainedScreens.Select(Onboarding.ModernScreenKey).ToList();
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
        var wOffline = Unlocks.IsOpen(Activity.Warren, GuideUnlockFacts())
            ? _warren.Tick((float)credited)
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
                _hunter, credited, _activeRegion, Regions.Get(_activeRegion).CombatBias, obh, obd,
                seed: unchecked((int)Math.Round(result.OfflineSeconds)) ^ _deepestEver);
            champOffline = offline.Gleam;
            if (champOffline > 0) _hunter.AddGleam((int)Math.Min(int.MaxValue, champOffline));
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
        var summary = new WelcomeSummary(credited, offline, wOffline, Unlocks.IsOpen(Activity.Warren, GuideUnlockFacts()));
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
        // NAME WHAT IS OPEN, not what is locked. This said "PRESS B TO PICK SKILLS" while the BUILD
        // screen stays locked until wave 5 (Unlocks) — so the very first thing the game told a new
        // player was an instruction the game itself then refused. The one thing that IS open from
        // frame one is the fight, and watching it is genuinely the job.
        _bootMessage = "YOUR HUNTER IS ALREADY FIGHTING\nWATCH THE FIRST WAVES — SCREENS OPEN AS YOU PLAY";
        _bootColor = Gold;

        // THE WELCOME GIFT: one chest in the vault from the first frame (playtest 2026-08-26: "the
        // VAULT tutorial talks about chests but there are none"). Its contents are catalogue data, not
        // a roll — the starter's plainest weapon — so the vault's first visit, its tour and the guide's
        // "open a chest" rung are all true in the first minute. PARKED like every restored thing: the
        // Forge that holds the pile is built in LoadContent, and this runs from Initialize. Only here,
        // on a NEW game — an existing save's vault is whatever it saved, never a retroactive gift.
        _pendingChests = GiftChests.NewGameChests().ToList();
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
            // WRITTEN, NEVER READ — a courtesy to the previous build for one version. Access is
            // recomputed from MasteryTaken now; this list is what the OLD build would need to find
            // if a player rolled back, so it keeps being written and stops being believed.
            LearnedSkills = _mastery.AvailableSkills().OrderBy(s => s, StringComparer.Ordinal).ToList(),
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
            TraderWeekStamp = _traderWeek,
            TraderBoughtSlots = _traderBought.ToList(),
            ChestKeepSlots = _chestKeepSlots.Select(sl => sl.ToString()).ToList(),
            MasteryEarned = _deepestEver,          // stored as deepest-ever; Earned is re-derived on load
            ChampionGleamRate = _champGleamRate,
            DismissedGuideRungs = _dismissedGuide.OrderBy(s => s).ToList(),
            IntroSeen = _introSeen,
            ExplainedScreens = _explained.OrderBy(s => s).ToList(),
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
        _mastery = new MasteryTree();
        _dust = new MemoryDustTree();
        _skillProgress = new SkillProgress();
        _characters = new CharacterState();
        _warren = new Warren();
        _activeRegion = VerdantHollow.RegionId;

        _deepestEver = 0;
        _champGleamAccrued = 0;
        _champSecondsAccrued = 0;
        _champGleamRate = 0f;
        _chestKeepMinTier = 0;
        _chestKeepSlots.Clear();
        _traderWeek = 0;
        _traderBought.Clear();
        _traderStock = null;
        _traderStockLevel = 0;
        _runsWithVowKept = 0;
        _bossesFelled = 0;
        _highestMasteryAwarded = 0;
        _hasSave = false;

        // Nothing pending: SeedNewGame below parks only the first-boot message.
        _pendingInventory = null;
        _pendingChests = null;
        _pendingRunLog = null;
        _pendingTreeCamera = null;   // a fresh game opens the tree on its first-open framing
        _pendingWorn = new Dictionary<GearSlot, string?>();
        _pendingChestsOpened = 0;
        _pendingFreeSocketUsed = false;   // a fresh game's first gem is free again

        // The teaching layer starts over with the game: the intro is due again, nothing is explained,
        // no tile has been visited, and no notice is waiting.
        _rosterBaselined = false;
        _tourActive = false;
        _tourStep = 0;
        _introDecided = false;
        _introSeen = false;
        _pendingExplained = null;
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
        _guideStep = null;
        _dismissedGuide.Clear();
        _conquerMsg = "";
        _lockedMsg = "";
        _lockedTimer = 0f;

        SeedNewGame();               // the first-boot message
        _region = _world.RegionFarm(_activeRegion);
        BuildScreens();              // every screen re-made around the fresh state
        ApplyRestoredState();        // hands the (empty) pending state to the new screens
        _bootTimer = 7f;

        _showSettings = _showHelp = false;
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
    /// The dashboard performs its upgrade in the DRAW pass (the same place Forge does its Refine), so the
    /// spend + <see cref="Warren.Upgrade"/> happen here, right after Draw sets the request. Gleam and
    /// Dust spend from their real balances.
    /// </remarks>
    private void DrawWarren()
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
        _warrenScreen.Draw(_batch, PageCursor, MouseClicked);
        PlayCue(_warrenScreen.ConsumeCue());   // an upgrade that landed, or a refusal

        if (_warrenScreen.ConsumeUpgrade() is { } kind
            && _warren.CanUpgrade(kind, _hunter.Gleam, _dust.MemoryDust))
        {
            var c = _warren.UpgradeCost(kind);
            _hunter.SpendGleam(c.Gleam);
            _dust.Spend(c.Dust);
            _warren.Upgrade(kind);
            Save();
        }
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        Save();
        base.OnExiting(sender, args);
    }

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
    /// holds the Forge; the trait screen holds the Dust tree), so replacing the core objects without
    /// replacing the screens would leave half the game serving the abandoned state.
    /// </remarks>
    private void BuildScreens()
    {
        _forge = new ForgeScreen(_ui);
        _forge.Sound = _sound;   // the reveal's landing ticks, the gem set, the successful upgrade
        _forge.AskBeforeScrap = _askBeforeScrap;
        _traits = new TraitsScreen(_ui, _dust);
        _traitScreen = new TraitCollectionScreen(_ui);
        _roster = new RosterScreen(_ui);
        _vault = new VaultScreen(_ui);
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
        _vault.KeepSlots.Clear(); foreach (var sl in _chestKeepSlots) _vault.KeepSlots.Add(sl);
        if (_pendingRunLog is not null) _expedition.Log.Restore(_pendingRunLog);
        if (_pendingTreeCamera is { } cam) _masteryScreen.RestoreCamera(cam.Zoom, cam.PanX, cam.PanY);
        SeedExplained();
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
    /// A save from before the intro existed is recognised there (intro neither seen nor due) and every
    /// open screen is marked read. That save then carries IntroSeen forward as TRUE — the migration
    /// happens once, not on every load.
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
        foreach (var key in Onboarding.SeedExplained(facts, _introSeen, _pendingExplained ?? new List<string>(), _forge.FreeSocketUsed))
            _explained.Add(key);
        _pendingExplained = null;

        // The one-time migration: a player from before the intro is past it. Marked so the seeding
        // above does not repeat on every launch, quietly marking screens they opened but never read.
        if (!_introSeen && !Onboarding.IntroDue(GuideFacts(), false)) _introSeen = true;
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
        ("hunt again", () => _showSettings = false),
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
        Exit();
    }


    // ══════════════════════════════════════════════════════════════════════════════════════════
    protected override void Update(GameTime gameTime)
    {
        _keys = Keyboard.GetState();
        _mouse = Mouse.GetState();
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
        TickChromeMotion((float)gameTime.ElapsedGameTime.TotalSeconds);

        // Latch the click EDGE once per frame, here, before anything reads it. The edge lives for
        // exactly one Update, and _prevMouse is overwritten in Latch() at the end of Update — so a
        // property that recomputed the edge would read false during the following Draw(), which is
        // where every screen's action buttons (Forge Sell/Merge, Farm Hatch/Assign/Evolve, …) are
        // detected. Latching into a field keeps the click live through this frame's Draw.
        _clicked = _mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;

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
                or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "fightaura" or "fightflash" or "traitlit" or "traitterm" or "traitterminal" or "dusttree"
                or "fightstatus" or "fightfive" or "fightshieldbroken" or "fightmulti"
                or "roster" or "rosterlocked" or "rosterswitch" or "warrenready" or "warrenfresh" or "weave" or "weavefresh" or "vault" or "vaultfirst" or "vaultfilter" or "attune" or "attuned" or "trader"
                or "keystonenotice"
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
                if (Environment.GetEnvironmentVariable("RH_SHOT_HUNTER")?.Trim() is { Length: > 0 } shotHunter
                    && CharacterRoster.Find(shotHunter) is { } posed)
                    _characters.Restore(posed.Id, _characters.SaveQuests(),
                                        _characters.SaveUnlocked().Append(posed.Id));
                // (`expedition` and `vow` used to seed the retired creature den here; since its removal
                // they pose nothing beyond skipping the title.)
                // lootforge: seed the Forge with a spread of loot so it can be screenshotted with content
                // (the idle loop drops items only on boss waves, which a 1-second shot won't reach).
                // RH_SHOT_T poses the chest reveal at a chosen instant — the shake, the burst, the card.
                // Read BEFORE the fixture runs; applied after it opens a chest, or the open overwrites it.
                if (sm is "lootforge" or "vaultfirst" && Environment.GetEnvironmentVariable("RH_SHOT_T") is { } rt
                    && float.TryParse(rt, System.Globalization.CultureInfo.InvariantCulture, out var revealT))
                    _pendingRevealPose = revealT;

                if (sm == "lootforge")
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
                    _forge.DevOpenOneChest(_hunter);   // pose the chest-open REVEAL burst
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
                    // The world INPUTS, then the total: trait points are derived from conquests and
                    // corruption every frame, so a fixture that only calls SetEarned is stamped back to
                    // zero before it draws. (Conquest does not touch the mastery total above — that
                    // reads BestDepth/5 per region — so the 24 this fixture means still holds.)
                    _world.RestoreConquered(Regions.All.Select(r => r.Id));
                    _world.RestoreCorruption(16);
                    _dust.SetEarned(22);
                    _dust.Purchase("socket_2");   // weave_5 hangs off it on the capacity chain
                    _dust.Purchase("weave_5");
                    ApplySkillCapacity();
                    // SIX passives, which is the most the panel will list (it Takes 6). Same reasoning as
                    // the fifth skill card above: the passives list is variable-length and the RESONANCE
                    // heading below it used to be pinned at a fixed offset, so a full list was drawn
                    // straight through the heading. A fixture stopping at three could never show that.
                    // By BRANCH, not by a hard-coded id list: nine of the eleven ids the old list
                    // named retired at the 2026-08-30 re-axe, Take() no-ops on an unknown id, and the
                    // "six passives" pose had quietly become an empty list. Walking a branch's Minors
                    // and Notables in catalog order respects prereqs the same way the ATTUNE fixture does.
                    foreach (var wn in MasteryCatalog.Nodes.Where(
                                 x => x.Branch == Branch.Resonance
                                      && x.Kind is MasteryKind.Minor or MasteryKind.Notable))
                        _mastery.Take(wn.Id);

                    // BUILDTREE frames the whole tree. The TOUR of this screen is, by definition, a first
                    // visit, so it is posed on the first-open framing a real first visit gets — the
                    // card's light has to fall on what the player actually sees.
                    if (sm == "buildtree")
                    {
                        if (Environment.GetEnvironmentVariable("RH_SHOT_MODE") == "tour") _masteryScreen.DevOpenTreeFirstVisit();
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
                        _mastery.RestoreTaken(_mastery.Taken.Concat(new[] { "road_hammer", "road_snare" }).ToList());
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
                    _gear.Tree = _dust;
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
                        });
                    // Two superior UNEQUIPPED drops, added first so they sit at the top of the bag — they
                    // pose the green UP badge and the hover tooltip's "UPGRADE +N PWR" verdict (with
                    // RH_SHOT_MOUSE parked over the first card).
                    _forge.AddLoot(new List<ItemInstance>
                    {
                        // The weapon is the starter's class, so the UPGRADE verdict still poses on the
                        // first card; the chestplate is a WARDEN's, so the second card poses the dimmed
                        // lock and, under RH_SHOT_MOUSE, the "who can wear it" lines of the hover card.
                        new() { InstanceId = "up_wpn", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary, SellValue = 220, Element = Source.Machine, ItemLevel = 62, Class = ItemClass.Wanderer, Family = 1 },
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
                    _training.Tree = _dust;
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

                if (sm is "fight" or "welcome" or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "fightaura" or "fightflash" or "fightshield" or "runlog"
                    or "fightstatus" or "fightfive" or "fightshieldbroken" or "fightmulti" or "vfxdebug")
                {
                    // `vfxdebug` is `fightshield` PLUS the VFX contract's own overlay (brief §70): the
                    // standing barrier is the acceptance case, so the mode that photographs the contract
                    // has to be the mode that raises one.
                    if (sm == "vfxdebug") _expedition.DevVfxDebug = true;
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
                        or "fightstatus" or "fightfive" or "fightshieldbroken" or "fightmulti" or "vfxdebug")
                    {
                        _loadout.SkillCapacity = Math.Max(_loadout.SkillCapacity, 4);
                        // The roads, or every skill but the champion's own is refused and the fixture
                        // photographs an empty build.
                        _mastery.SetEarned(9999);
                        _mastery.RestoreTaken(_mastery.Taken
                            .Concat(MasteryCatalog.Nodes.Where(x => x.Kind == MasteryKind.SkillRoad).Select(x => x.Id))
                            .ToList());
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
                    // the keystones that grant them LEARNED (their trait nodes owned) and SOCKETED (two
                    // sockets: socket_2). Nothing else on the screen changes, so the chips are the
                    // difference between this and `fight`.
                    if (sm == "fightstatus")
                    {
                        _dust.Restore(_dust.MemoryDust, _dust.OwnedIds.Concat(new[]
                            { "socket_2", "ks_ironclad", "ks_juggernaut", "ks_undying", "ks_glass_cannon", "ks_rend" }));
                        _loadout.KeystoneCapacity = 2;
                        var learned = DustEffects.LearnedKeystones(_dust);
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
                    if (sm == "fightshieldbroken" && _loadout.IndexOfSkill("snare_jaws") is var jawsSlot && jawsSlot >= 0)
                    {
                        _loadout.SetSource(jawsSlot, Source.Nature);
                        _loadout.SetSkill(jawsSlot, "field_mire");
                    }
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
                    if (sm is "fightgear" or "fightswing" or "fightreport" or "fightfall" or "runlog" or "fightshield" or "fightshieldbroken" or "vfxdebug")
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
                    _expedition.Tree = _dust;
                    _expedition.Mastery = _mastery;
                    // The levels too — the run DevStart composes is the run the capture shows (nothing
                    // restarts it), so a variation chosen above (fightmulti's CLUSTER) has to be in the
                    // build before the fight starts, not pushed by the host on a later frame.
                    _expedition.Progress = _skillProgress;
                    // Pose the new fight-screen UX: the welcome-back toast, and the "you have things to do"
                    // attention cue (a waiting chest + unspent mastery points).
                    // Not for fightreport: the welcome-back toast outranks the HunterDown overlay in the
                    // arena's priority list, so it would hide the very screen that capture exists to show.
                    // No toast over `fightshield`: it would sit on the very strip the capture is for.
                    if (sm is not ("fightreport" or "fightfall" or "runlog" or "welcome" or "fightshield" or "fightshieldbroken" or "fightstatus"))
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
                        var offline = OfflineHunt.Simulate(ComposeBuild(),
                            _hunter, away, _activeRegion, Regions.Get(_activeRegion).CombatBias, obh, obd, seed: 42);
                        _warren.ConqueredRegions = _world.ConqueredIds.Count;
                        var warrenOpen = Unlocks.IsOpen(Activity.Warren, GuideUnlockFacts());
                        _welcome = new WelcomeSummary(away, offline, warrenOpen ? _warren.Tick((float)away) : default, warrenOpen);
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
                        // RH_SHOT_LIMIT=armour|reach|sustain poses the log's diagnostic for that limit: the
                        // real seeded death, with the one measurement that names the limit set past its
                        // threshold — so each of the three verdicts the log can give has a picture.
                        var limit = Environment.GetEnvironmentVariable("RH_SHOT_LIMIT")?.ToLowerInvariant() switch
                        {
                            "armour" or "armor" => RunLimit.Armour,
                            "reach" => RunLimit.Reach,
                            "sustain" => RunLimit.Sustain,
                            _ => (RunLimit?)null,
                        };
                        _expedition.DevRunToDeath(_hunter, poseLimit: limit);
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
                    if (sm is "fight" or "fightshield" or "fightshieldbroken" or "fightmulti" or "fightstatus" or "fightfive" or "vfxdebug"
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
                    _expedition.Tree = _dust;
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
                    // has to hold at UI SCALE 150.
                    var fell = Environment.GetEnvironmentVariable("RH_SHOT_CONQUEST")?.Trim() is { Length: > 0 } cq
                               && Regions.Find(cq) is not null ? cq : VerdantHollow.RegionId;
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
                    _dust.SetEarned(12);
                    _dust.Purchase("ledger");
                    _dust.Purchase("filter_common");
                    _dust.Purchase("forge_insight");
                    _dust.Purchase("auto_merge");
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
                    _mastery.RestoreTaken(new[] { "road_volley", "road_volley_2" });
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
                    _dust.SetEarned(22);
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
                        .ToList());
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

                if (sm is "dust" or "traitlit" or "traitterm" or "traitterminal" or "dusttree")
                {
                    _showTraits = true;
                    // THE OLD MEMORY TREE, only under its own modes. `dust` poses the TRAITS screen
                    // now; `dusttree` — and the two flourish poses, which are the TREE's animation and
                    // have no counterpart on the new screen — are what still photograph it. It stays
                    // reachable until P5 has moved the keystones and the vows off it.
                    _showDustTree = sm is "dusttree" or "traitlit" or "traitterm" or "traitterminal";
                    _dust.AddDust(77_605);   // Dust still shows in the top pills; it no longer buys traits

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
                    foreach (var traitId in posedTraits)
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
                    var pinned = Environment.GetEnvironmentVariable("RH_SHOT_TRAIT");
                    if (string.Equals(pinned, "unknown", StringComparison.OrdinalIgnoreCase))
                        _traitScreen.DevSelectUnknown(_traitLedger);
                    else
                        _traitScreen.DevSelect(pinned is { Length: > 0 } ? pinned : "t_last_word");

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

                    // TRAIT POINTS, and real ids. This fixture used to award Dust and then buy
                    // "might_1", "might_2", "grit_1", "grit_2", "tempo_1" and "blood_1" — none of which
                    // have existed since the trait tree was rebuilt around the spine and the four roads.
                    // Every Purchase silently returned false and DevSelect fell back to the cheapest
                    // node, so for the whole of that rebuild the capture showed an untouched tree with
                    // zero points: forty-four LOCKED cards and NOT ENOUGH TRAIT POINTS. The screen was
                    // being reviewed on a screenshot of its own empty state.
                    // Set the INPUTS, not the total. Both trees derive their points from world progress
                    // every frame inside UpdateExpedition, which runs before this screen's early return
                    // — so a fixture that calls SetEarned directly is overwritten one frame later and
                    // has been posing a career with ZERO points ever since the derivation landed. A
                    // fully conquered world at corruption 16 is a real, reachable 22.
                    _world.RestoreConquered(Regions.All.Select(r => r.Id));
                    _world.RestoreCorruption(16);
                    // And once directly, because the derivation does not run until later in this same
                    // frame — without it the fixture's own Purchase calls see zero available points and
                    // every one of them silently fails.
                    _dust.SetEarned(22);
                    foreach (var id in new[] { "socket_2", "ledger", "vow_study_1", "forge_insight",
                                               "filter_common", "recall_1", "ks_glass_cannon" })
                        _dust.Purchase(id);
                    _traits.DevSelect("ks_bloodlust");   // AVAILABLE: its prerequisite (KEYSTONE — GLASS CANNON) is lit
                    // RH_SHOT_NODE=<id> selects another trait, so a blocked node or a terminal can be photographed read.
                    if (Environment.GetEnvironmentVariable("RH_SHOT_NODE") is { } traitPin) _traits.DevSelect(traitPin);
                    _hunter.AddGleam(131_900_000);     // the top currency pills read like the reference
                    _hunter.AddMaterials(12_600);

                    // The tree is a free camera now. `capture.sh dust out.png 1.2` zooms it to 1.2 on
                    // the posed node, so "zooming in reveals detail" is a captured fact rather than a
                    // claim. Only the plain dust mode reads it — traitlit's third argument is a time.
                    if (sm == "dust"
                        && float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_ZOOM"),
                                          System.Globalization.NumberStyles.Float,
                                          System.Globalization.CultureInfo.InvariantCulture, out var devZoom))
                        _traits.DevCamera(devZoom, Environment.GetEnvironmentVariable("RH_SHOT_NODE") ?? "ks_bloodlust");

                    // Frozen at 0.30s: past the flash, into the shockwaves, with the name plate risen
                    // and readable. RH_SHOT_T moves the freeze so the other beats can be checked too.
                    var poseT = float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                                               System.Globalization.CultureInfo.InvariantCulture, out var pt)
                        ? pt : 0.30f;
                    if (sm == "traitlit") _traits.DevPoseLit(_dust, "ks_bloodlust", poseT);
                    if (sm is "traitterm" or "traitterminal")
                    {
                        // The Ruin road walked to its end — the one purchase in the game that costs
                        // twelve points and closes off three other roads.
                        foreach (var id in new[] { "socket_2", "ks_glass_cannon", "ks_bloodlust", "ks_blood_magic" })
                            _dust.Purchase(id);
                        _traits.DevPoseLit(_dust, "ks_reaper", poseT);
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
                Latch(gameTime);
                return;
            }

            const int titleItems = 3; // PLAY / SETTINGS / QUIT
            if (Pressed(Keys.Up)) _titleCursor = (_titleCursor + titleItems - 1) % titleItems;
            if (Pressed(Keys.Down)) _titleCursor = (_titleCursor + 1) % titleItems;
            if (Pressed(Keys.Enter) || Pressed(Keys.Space)) ChooseTitleItem(_titleCursor);
            if (_keys.IsKeyDown(Keys.Escape)) Exit();
            Latch(gameTime);
            return;
            }
        }

        // THE INTRO DECISION, once, on the first gameplay frame after the title closes — whichever way it
        // closed (BEGIN THE HUNT, a fixture, the boot check). The fixture `intro` poses a card by number.
        if (!_introDecided)
        {
            _introDecided = true;
            var wanted = CaptureRig ? ShotMode == "intro" : Onboarding.IntroDue(GuideFacts(), _introSeen);
            if (wanted) BeginTour(Activity.Hunt);
        }

        // EVERY OTHER SCREEN'S TOUR starts the first time that screen is on top while its tour is still
        // owed — asked of the explained list every frame, so no navigation path can forget to ask, and a
        // screen opened by a hotkey, the rail or an errand button is toured all the same. Never over the
        // help, the settings, a chest reveal or the expedition log: a tour is about the screen the player
        // is looking at, and none of those is a screen.
        if (!_tourActive && !_showHelp && !_showSettings && !WelcomeUp && !_forge.RevealActive && !_expedition.LogOpen
            && Onboarding.TourDue(ScreenActivity(), _explained) is not null)
            BeginTour(ScreenActivity());

        // THE FIRST GEM. Its lesson is a second, smaller tour of the FORGE, owed from the moment a gem
        // is held (derived — Onboarding.GemTourDue) and given the first time the Forge is on top after
        // that, once its own tour is done. It arrives with the SOCKET tab open, so the light falls on
        // the tab the card names and the bag beside it lists the gem.
        var gemsHeld = GemsHeld();
        if (!_tourActive && !_showHelp && !_showSettings && !WelcomeUp && !_forge.RevealActive && !_expedition.LogOpen
            && ScreenActivity() == Activity.Forge && _showForge
            && Onboarding.GemTourDue(gemsHeld, _explained) is { } gemKey)
        {
            _forge.RequestSocketTab();
            BeginTour(Activity.Forge, Onboarding.GemTourFor(_forge.FreeSocketUsed), gemKey);
        }

        // And the moment the first one DROPS, a line at the top says where it goes — never a panel —
        // and the FORGE tile earns its NEW mark back even if the Forge was visited earlier this session.
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
        }

        if (_bootTimer > 0f) _bootTimer = Math.Max(0f, _bootTimer - dt);
        if (_lockedTimer > 0f) _lockedTimer = Math.Max(0f, _lockedTimer - dt);
        // Notice toasts: one at a time, each for NoticeSeconds, the next one only once the last has
        // gone. Ticks here, past the title return, so only seconds of actual play count.
        if (_noticeTimer > 0f) _noticeTimer = Math.Max(0f, _noticeTimer - dt);
        if (_noticeTimer <= 0f && _noticeQueue.Count > 0)
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
        TickWarren(dt);

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
        if (DevKeysEnabled && Pressed(Keys.F7)) { _expedition.DevBossDebug = !_expedition.DevBossDebug; _gear.DevGearDebug = !_gear.DevGearDebug; _training.DevStatsDebug = !_training.DevStatsDebug; _masteryScreen.DevBuildDebug = !_masteryScreen.DevBuildDebug; _forge.DevForgeDebug = !_forge.DevForgeDebug; _warrenScreen.DevWarrenDebug = !_warrenScreen.DevWarrenDebug; _mapScreen.DevMapDebug = !_mapScreen.DevMapDebug; _traits.DevDustDebug = !_traits.DevDustDebug; }   // dev layout overlays
        if (DevKeysEnabled && Pressed(Keys.F8)) CycleUiScale();   // dev: UI SCALE 100 / 125 / 150 / AUTO, until the settings row lands (UX V2 P3.1)
        if (Pressed(Keys.F1)) _showHelp = !_showHelp;
        if (Pressed(Keys.F10)) _showSettings = !_showSettings;
        if (WelcomeUp && (Pressed(Keys.Enter) || Pressed(Keys.Space))) _showWelcome = false;   // CONTINUE by key
        // THE WELCOME IS A MODAL FOR INPUT, NOT FOR THE FRAME. It is up on the very first frame after a load,
        // and an early return here would skip the block below that feeds every screen its dependencies —
        // the first Draw would then hit a null (the boot check caught exactly that). So the frame runs on
        // and only the input is spent: Pressed() and MouseClicked both honour _swallowInput / WelcomeUp.
        if (WelcomeUp) _swallowInput = true;

        // A modal eats the frame's input, but NOT the frame. The farms above still tick and the
        // autosave above still fires — an idle game does not pause because you opened a menu. What it
        // must not do is let the hotkeys and buttons underneath the panel keep responding: without
        // this, clicking FULLSCREEN also presses whatever the panel happens to be covering.
        // THE SETTINGS RETURN MOVED BELOW UpdateExpedition — see the note beside it. It used to sit
        // here, which stopped the champion fighting for as long as the display-options panel was open:
        // the one overlay in the game that paused the idle loop, contradicting the comments on both
        // sides of it. Its INPUT gating is unaffected, because `watchingFight` already carries a
        // !_showSettings term.

        // THE INTRO SWALLOWS INPUT while it is up: it is drawn over the nav rail, so a click meant to
        // advance it would otherwise also land on whatever tile is underneath and throw the player onto
        // a screen they did not ask for.
        //
        // IT DOES NOT RETURN EARLY, and that is not a style choice. The settings panel can, because it
        // is impossible to have open on the first frame. This one is not: a brand-new save starts the
        // intro on its first gameplay frame, so an early return here would skip the per-frame block
        // that feeds every screen its dependencies, and the first Draw would hit a null Loadout in
        // TrainingScreen. check_boot.sh caught exactly that shape twice before, under the modal panel this
        // replaced. Swallow the input; never skip the frame.
        //
        // The champion keeps fighting behind it. An idle game does not pause to talk to you.
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
        _swallowInput = _tourActive;
        // NOT UNDER THE CAPTURE RIG. The game window takes focus while a shot renders, so a key the
        // developer happens to press in the sixty frames advances the card — a capture asked for card
        // five came back as card six. The rig poses a card by number; it never plays.
        if (_tourActive && !CaptureRig && (_clicked || AnyKeyPressed()))
        {
            // Raw edge, not Pressed(): Pressed reads !_swallowInput, which is already true.
            var escape = _keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape);
            if (escape || _tourStep + 1 >= _tour.Count) EndTour();
            else _tourStep++;
            _sound.Play("sfx_click", 0.7f);
            // _swallowInput deliberately STAYS true: this frame's input was spent on the tour.
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
            if (!onRevealButton && (revealClick || Pressed(Keys.Space) || Pressed(Keys.Enter)))
                _forge.AdvanceReveal();

            // The reveal's question carries the same "don't ask me again" box the bench's does, and the
            // bench's poll lives inside the _showForge branch below — which this reveal never reaches
            // (it plays on whatever screen the chest was opened from). Without this the box ticked, the
            // preference flipped for the session, and the next launch asked again.
            if (_forge.PrefsDirty) { _forge.PrefsDirty = false; _askBeforeScrap = _forge.AskBeforeScrap; SaveDisplay(); }

            _swallowInput = true;
        }

        // THE SETTINGS GEAR, top-right of every screen. Handled here, before the nav and the screens,
        // so its click never falls through to whatever sits underneath it.
        if (!_showSettings && !_showHelp && !_swallowInput && _clicked && SettingsGear.Contains(ChromeMouse))
        {
            _showSettings = true;
            _swallowInput = true;
        }

        // THE HINT SLOT at the top of a menu screen — a slot note, the lesson about this screen, or a hint
        // from real state — closes with its × (a note and a lesson are remembered in the save; a hint for
        // the session), and a click anywhere else on it is spent: it sits over the screen's own controls,
        // and a click that closed nothing must not press a TRAIN button underneath. THE HUNT'S LESSON
        // CARD closes the same way (playtest: "messages stay forever until I do the thing"). Display only:
        // no underlying fact is faked, so unlocks and gates are untouched. Handled here rather than in
        // Draw so the click is swallowed before any screen hit-tests it.
        if (!_showSettings && !_showHelp && !_swallowInput && _clicked)
        {
            if (SlotShowing() is { } slot)
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

        if (!ceremonyHolds) HandleNavClick();   // a click on the shared hex nav works from any screen

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
        for (var navKey = 0; navKey < Nav.Length && !ceremonyHolds; navKey++)
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
        if ((Pressed(Keys.L) || wantsLog) && !ceremonyHolds)
        {
            _expedition.ToggleLog();
            if (_expedition.LogOpen)
            {
                // ALL NINE, not seven. _showRoster and _showLoadout were missing, so opening the log from
                // the roster or the weave left that screen live underneath it — and because the log is
                // drawn in the same batch, OverlayActive stayed true and the batch kept the OVERLAY
                // inset transform instead of the plain canvas one. DrawLog's own hit-tests assume the
                // plain one, so its page buttons landed in a third coordinate space.
                _showTraits = _showWarren = _showForge = _showWorld = false;
                _showMastery = _showGear = _showTraining = _showRoster = _showLoadout = _showVault = false;
            }
        }
        if (_expedition.LogOpen)
        {
            if (Pressed(Keys.Left)) _expedition.StepLog(1);    // left = older
            if (Pressed(Keys.Right)) _expedition.StepLog(-1);
        }

        // T — THE WEAVE. The one screen with no rail tile of its own, so it cannot go through OpenNav;
        // it is gated on Activity.Build, which is the screen it is reached from and the thing it is
        // part of. Without this it was the last remaining way to walk past the unlock gate.
        if (Pressed(Keys.T) && !ceremonyHolds)
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
        _forge.TickReveal((float)gameTime.ElapsedGameTime.TotalSeconds);

        // THE CHAMPION FIGHTS EVERYWHERE. Ticked here, before any overlay can early-return, so a run
        // keeps clearing waves and paying out while you're in the Forge, the tree, or another region's
        // menu — the whole point of an idle game. The fight screen's own clicks are handled in its Draw,
        // which only runs when that screen is the one on top, so a click in the Forge cannot fall
        // through into the fight without any flag being threaded down for it.
        _regionProgression = RegionProgressionOf(_region);
        UpdateExpedition(gameTime);

        // A modal eats the frame's INPUT, but not the frame, and not the fight. The autosave and the
        // farms tick above; the champion ticks on the line above this one. What must not happen is the
        // hotkeys and buttons underneath the panel continuing to respond.
        if (_showSettings || _showHelp) { Latch(gameTime); return; }   // the help (F1) is a modal too

        if (_showWorld) { UpdateWorld(); Latch(gameTime); return; }

        if (_showMastery)
        {
            // One lock, read by both doors — the rail tile and the button on the page.
            _masteryScreen.Loadout = _loadout;
            _masteryScreen.Mastery = _mastery;
            _masteryScreen.Power = _hunter.PowerRating;   // the Build screen has no Hunter ref of its own
            _masteryScreen.Level = _hunter.HunterLevel;
            _masteryScreen.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked,
                                _mouse.LeftButton == ButtonState.Pressed, MouseWheel, _dust, MouseRightClicked);
            if (_masteryScreen.Dirty) { _masteryScreen.ClearDirty(); Save(); }
            PlayCue(_masteryScreen.ConsumeCue());   // a node taken, a style sealed, a point handed back, a refusal

            Latch(gameTime);
            return;
        }

        if (_showGear)
        {
            _gear.Loadout = _loadout;
            _gear.Mastery = _mastery;
            _gear.Tree = _dust;
            _gear.DiscoveredKeystones = _keystoneMenu;
            _gear.KnownVows = _vowMenu;
            _gear.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked, MouseRightClicked, MouseWheel, _hunter);
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

            _vault.Update(dt, _forge.UnopenedChests, PageCursor, MouseClicked, MouseWheel);

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
            _training.Loadout = _loadout;
            _training.Mastery = _mastery;
            _training.Tree = _dust;
            _training.DiscoveredKeystones = _keystoneMenu;
            _training.KnownVows = _vowMenu;
            _training.Character = _characters.Active;
            // (HighestWave / ChestsOpened / MasteryPoints are gone with the PROGRESS panel — they
            //  are the Map's, the Vault's and the Mastery tree's numbers, and none of them moves
            //  when you train.)
            _training.SkillLevels = _skillProgress;   // the live screen resolved a build without them
            _training.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked, MouseWheel, _hunter);
            if (_training.Dirty) { _training.ClearDirty(); Save(); }
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
            _loadoutScreen.Tree = _dust;
            // WHAT THE WORLD HAS TAUGHT AND WHAT THE HUNTER HAS PROVED. The workbench used to read the
            // trait tree for both; both are facts about the account now, and the host owns them.
            _loadoutScreen.DiscoveredKeystones = _keystoneMenu;
            _loadoutScreen.KnownVows = _vowMenu;
            _loadoutScreen.NextSocketNote = Unlocks.NextSocketNote(GuideUnlockFacts());
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
            // THE NEW TRAITS SCREEN DOES ITS WORK IN Draw, like the Vault and the Forge: it is a
            // board of tiles and one reading panel, with no camera to drive and no held drag. Its cue
            // and its Dirty flag are read at the foot of its Draw call. This block stays so the
            // screen still swallows the frame's hotkeys.
            //
            // The OLD Memory tree keeps its own update, and it only runs under the capture rig — see
            // _showDustTree. P5 deletes it whole.
            if (_showDustTree)
            {
                _traits.Update(ScreenKeys, PageCursor, MouseClicked, _mouse.LeftButton == ButtonState.Pressed,
                                 MouseWheel, _dust, dt);
                if (_traits.ConsumeCue() is { } cue)
                {
                    _sound.PlayFirst(1f, cue, "sfx_conquer", "sfx_levelup", "sfx_click");
                    if (cue is "sfx_trait_lit" or "sfx_trait_terminal") Save();
                }
            }
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
                          inputLocked: _swallowInput || _showSettings || WelcomeUp || _forge.RevealActive);
            // The dialog's "don't ask me again" writes through to the prefs file the moment it is used.
            if (_forge.PrefsDirty) { _forge.PrefsDirty = false; _askBeforeScrap = _forge.AskBeforeScrap; SaveDisplay(); }
            Latch(gameTime);
            return;
        }

        if (_showWarren)
        {
            // WARREN is the facility-production dashboard. Production runs every frame in TickWarren; the
            // dashboard handles its clicks in Draw (like Forge/Stats), so it needs no Update here.
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

    /// <summary>
    /// What the first-run guide knows about this player.
    /// </summary>
    /// <remarks>
    /// Every field is read from state the save ALREADY carries, so the guide needed no new persisted
    /// field and no migration — and a player who is already deep into the game arrives with every step
    /// satisfied rather than being taught the loop they have been playing for hours.
    ///
    /// Two are deliberately proxies. "Felled a boss" is read as having reached the first boss wave,
    /// because bosses are every fifth wave and depth is what the save keeps. "Touched the build" is read
    /// as having SPENT a mastery point, which cannot happen without opening the build screen — the
    /// woven skills themselves are seeded on a new game, so counting those would mark the lesson done
    /// before the player had seen it.
    /// </remarks>
    private TutorialFacts GuideFacts() => new(
        WavesCleared: _deepestEver,
        Gleam: _hunter.Gleam,
        StatsTrained: Enum.GetValues<HunterStat>().Sum(_hunter.RankOf),
        ItemsOwned: _forge?.Inventory.Count(Gear.IsWearable) ?? 0,
        ItemsWorn: Enum.GetValues<GearSlot>().Count(sl => _hunter.Worn(sl) is not null),
        // A CHEST WAITING is now its own fact, because the guide has a rung about opening one. The step
        // it feeds shows during the wait as well as after it — a 20% drop off a boss every fifth wave is
        // tens of waves, and the guide going quiet for all of them was the reported bug.
        ChestsHeld: _forge?.UnopenedChests.Count ?? 0,
        // WAS `_mastery.Spent > 0`, WHICH MEASURED THE WRONG SYSTEM. The step says "press B for BUILD,
        // then WEAVE", and weaving a skill touches _loadout — it never touches the mastery tree. So a
        // player who did exactly what they were told stayed on that prompt forever, while a player who
        // idly poked the tree in minute one completed a step they were never shown. It also went
        // BACKWARDS on a respec, which the tree offers free and unlimited.
        //
        // The real signal is "the build is no longer the one you were handed": a second skill woven, or
        // the first one changed away from the starter's Body Strike.
        SkillsWoven: BuildDiffersFromStarter() ? 1 : 0,
        DeepestWave: _deepestEver,
        RegionsConquered: _world.ConqueredIds.Count);

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
                    PostVowReveal(granted, offered: true);

        // KEYSTONES. TRANSITIONAL: the trait tree still stands this phase and its keystone nodes are
        // still buyable, so what it has taught is unioned in as well. That term goes when the tree does.
        var found = Keystones.DiscoveredBy(_world, DustEffects.LearnedKeystones(_dust).Select(k => k.Id));
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
    private void PostKeystoneReveal(Keystone k)
    {
        var where = Keystones.SourceOf(k.Id) is { } src ? RungReached(src) : "";
        PostNotice($"NEW KEYSTONE — {k.Name}", where.Length > 0 ? $"{where} {k.Blurb}" : k.Blurb);
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
        => _loadout.ToBuild(_dust, _mastery, _characters.Active, _skillProgress, _keystoneMenu, _vowMenu);

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
        TraitPointsEarned: _dust.Earned);


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

    /// <summary>The slot note owed at the top of the current screen, or null. Never during a tour.</summary>
    private ScreenBanner? ScreenBannerShowing()
    {
        if (_showTitle || _tourActive || _showHelp || _showSettings) return null;
        // The expedition LOG is a full-screen read over the hunt; a banner over it would be about a
        // screen the player is not looking at.
        if (_expedition.LogOpen) return null;
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
    /// That screen is marked explained, and only that one: the Hunt's intro says the other screens
    /// EXIST, and each one's own tour still waits on it. The Hunt's tour also sets the intro flag,
    /// which is what <see cref="Onboarding.IntroDue"/> reads on the next launch.
    /// </remarks>
    private void EndTour()
    {
        _tourActive = false;
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
    // a slot note (Onboarding.BannerFor), the guide rung that is ABOUT this screen (Tutorial.Sends), or a
    // hint from real state (Onboarding.HintFor) — in that order, one at a time. The strip that used to
    // cross the bottom of every screen with a lesson about some other screen is gone; where a rung is
    // about another screen, that screen's rail tile wears the NEW mark instead.

    /// <summary>What kind of thing the slot holds, which decides what closing it remembers.</summary>
    private enum SlotKind { Note, Lesson, Hint }

    /// <summary>The slot's content: the key that closes it, its title, its body (empty for a one-line hint).</summary>
    private readonly record struct SlotContent(SlotKind Kind, string Key, string Title, string Body);

    /// <summary>Hints closed this session. Not saved: a hint is about now, and returns when its fact changes.</summary>
    private readonly HashSet<string> _dismissedHints = new();

    /// <summary>The slot content owed on the current menu screen, or null. Never during a tour, never on the HUNT.</summary>
    private SlotContent? SlotShowing()
    {
        if (_showTitle || _tourActive || _showHelp || _showSettings || !OverlayActive) return null;
        if (ScreenBannerShowing() is { } note) return new SlotContent(SlotKind.Note, note.Key, note.Title, note.Body);
        var screen = ScreenActivity();
        var tf = GuideFacts();
        if (_guideStep is { } step && Tutorial.HasGuidance(step) && Tutorial.Sends(step, tf) == screen)
            return new SlotContent(SlotKind.Lesson, step.ToString(), Tutorial.Title(step), Tutorial.Body(step, tf));
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
            case SlotKind.Lesson: _dismissedGuide.Add(slot.Key); _guideStep = Tutorial.Showing(GuideFacts(), _dismissedGuide); Save(); break;
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

        return new HintFacts(
            NewRegionName: newRegion,
            TraitPointsFree: _dust.Available,
            MasteryPointsFree: _mastery.Available,
            EmptySkillSlots: Math.Max(0, Unlocks.SkillSlots(GuideUnlockFacts()) - _loadout.Skills.Count),
            NewChampionName: _rosterNews ? _rosterNewName : null,
            ChestsWaiting: _forge?.UnopenedChests.Count ?? 0,
            TrainableStat: stat,
            TrainableCost: cost,
            AffordableUpgradeName: upgrade);
    }

    // ── The HUNT's lesson card (UX V2 P0.7) ────────────────────────────────────────────────────
    //
    // The fight's own rungs — Watch, MeetABoss, Conquer, and OpenChest while no chest is held — are the
    // HUNT's onboarding. They render one at a time in the toast slot under the header stack, as a quiet
    // card with an ×, and yield to the boot and notice toasts (a queue, not a stack).

    /// <summary>The fight rung to show on the HUNT this frame, or null.</summary>
    private TutorialStep? HuntLessonShowing()
    {
        if (_showTitle || _tourActive || _showHelp || _showSettings || WelcomeUp || OverlayActive) return null;
        if (_expedition.LogOpen) return null;
        if (_bootTimer > 0f && _bootMessage.Length > 0) return null;       // the welcome toast has the slot
        if (_noticeTimer > 0f && _notice.Head.Length > 0) return null;     // so does a notice
        if (_guideStep is not { } step || !Tutorial.HasGuidance(step)) return null;
        // A rung about another screen is that screen's business (and its tile's NEW mark), not the fight's.
        return Tutorial.Sends(step, GuideFacts()) is null ? step : null;
    }

    /// <summary>Where the lesson card hangs: the toast slot, as tall as its wrapped body.</summary>
    private Rectangle HuntLessonRect(TutorialStep step)
    {
        var body = _ui.WrapBig(Tutorial.Body(step, GuideFacts()), ToastWidth - CardPadX - CardCloseLane, UiTypography.Secondary);
        return new Rectangle(ToastLeft, ToastTop, ToastWidth, CardHeight(body.Count));
    }

    private void DrawHuntLesson()
    {
        if (HuntLessonShowing() is { } step) DrawLessonCard(step, 1f);
    }

    /// <summary>One fight rung as a card — the live one, or the intro's preview of what a lesson looks like.</summary>
    private void DrawLessonCard(TutorialStep step, float alpha)
    {
        var r = HuntLessonRect(step);
        var body = _ui.WrapBig(Tutorial.Body(step, GuideFacts()), r.Width - CardPadX - CardCloseLane, UiTypography.Secondary);
        _ui.PanelQuiet(_batch, r, alpha);
        _ui.TextBig(_batch, Tutorial.Title(step), r.X + CardPadX, r.Y + CardTitleTop, UiInk.Accent * alpha, UiTypography.Body);
        var ty = r.Y + CardBodyTop;
        foreach (var line in body)
        {
            _ui.TextBig(_batch, line, r.X + CardPadX, ty, UiInk.Primary * alpha, UiTypography.Secondary);
            ty += UiTypography.Pitch(UiTypography.Secondary);
        }
        _ui.CloseButton(_batch, HintCloseRect(r), ChromeMouse, false);   // drawn here; the click is handled in Update
    }

    /// <summary>Close a fight lesson for good — remembered in the save, like closing any rung.</summary>
    private void CloseLesson(TutorialStep step)
    {
        _dismissedGuide.Add(step.ToString());
        _guideStep = Tutorial.Showing(GuideFacts(), _dismissedGuide);
        _sound.Play("sfx_click", 0.6f);
        Save();
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
        if (shed.Count == 0 && unwoven.Count == 0) return;

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
                ? $"{said} BELONGS TO ANOTHER HUNTER — ITS SLOT IS EMPTY, AND EVERY LEVEL ON IT IS KEPT"
                : $"{said} BELONG TO ANOTHER HUNTER — THEIR SLOTS ARE EMPTY, AND EVERY LEVEL ON THEM IS KEPT");
        }
        _lockedMsg = string.Join("  ·  ", lines);
        _lockedTimer = 4.5f;
        Save();
    }

    /// <summary>The toast for clicking a locked rail tile. Says the price, then fades.</summary>
    private void DrawLockedToast()
    {
        if (_lockedTimer <= 0f || _lockedMsg.Length == 0) return;

        var fade = MathF.Min(1f, _lockedTimer / 0.5f);
        var w = LockedToastWidth;
        // TWO LINES WHEN TWO THINGS HAPPENED. A switch can shed gear AND unweave a signature in the same
        // instant, and a box built for exactly one line answered that by cutting the second fact off
        // mid-word. It still draws as one line whenever one line is what there is.
        var wrapped = _ui.WrapBig(_lockedMsg, w - UiMetrics.Space(40), UiTypography.OverlayBody).Take(2).ToList();
        var h = wrapped.Count * UiTypography.Pitch(UiTypography.OverlayBody) + UiMetrics.Space(17) * 2 - (UiTypography.Pitch(UiTypography.OverlayBody) - UiTypography.OverlayBody);
        var box = new Rectangle(UiKit.PageCenterX - w / 2, LockedToastTop, w, h);
        _ui.Fill(_batch, box, new Color(0x18, 0x10, 0x24) * (0.92f * fade));
        _ui.Fill(_batch, new Rectangle(box.X, box.Y, box.Width, 3), NavGem * fade);
        var ly = box.Y + UiMetrics.Space(17);
        foreach (var line in wrapped)
        {
            _ui.TextCenterBig(_batch, line, box.Center.X, ly, Color.White * fade, UiTypography.OverlayBody);
            ly += UiTypography.Pitch(UiTypography.OverlayBody);
        }
    }

    /// <summary>The locked-tile toast's width and where it hangs — a readable line under the pills. Page geometry.</summary>
    private const int LockedToastWidth = 900, LockedToastTop = 96;

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
        if (_noticeTimer <= 0f || _notice.Head.Length == 0) return;
        if (_showTitle || _showHelp || _showSettings || WelcomeUp) return;
        // Never over the SPECIALISATION ceremony: it is the one modal the game stops for, and a quest
        // toast across it covered the panel's own title (seen at UI SCALE 125%).
        if (_showMastery && _masteryScreen.SpecialisationOpen) return;
        // Never over a tour: the first-gem notice sits exactly where the Forge's tab strip is, and a
        // toast across a spotlight is two lessons at once. The tour IS the notice's payload.
        if (_tourActive) return;

        var fade = Math.Clamp(_noticeTimer / 1.0f, 0f, 1f);
        var y = _bootTimer > 0f && _bootMessage.Length > 0 && !_tourActive ? ToastTop + ToastHeight + UiMetrics.Space(8) : ToastTop;
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
        var room = r0.Width - UiMetrics.Space(90);
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
        var r = new Rectangle(r0.X, r0.Y, r0.Width, h);
        _ui.PanelQuiet(_batch, r, fade);   // a toast is not a modal — the quiet frame (UiKit.PanelQuiet)

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

    /// <summary>How many wrapped body lines a notice may grow to before it is cut.</summary>
    /// <remarks>
    /// Four holds every string the game posts at every UI SCALE. The longest is CAPACITOR's keystone
    /// reveal at 164 characters — its blurb, plus the sentence naming the rung that taught it — which
    /// wraps to three lines at SCALE 100 and to four at 150, where the type is larger and the plate is
    /// not. This is a ceiling for a rare case rather than a target: a toast that needs more than four
    /// lines is a copy problem, not a layout one, and `keystonenotice` is the mode that shows it.
    /// </remarks>
    private const int NoticeBodyLines = 4;

    /// <summary>The notice toast's width — a readable two-line plate, centred. Page geometry.</summary>
    private const int NoticeToastWidth = 800;

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
        if (target == TourTarget.MasteryTile)
            return new[] { NavHexRect(Array.IndexOf(NavActivity, Activity.Mastery)) };

        var own = screen switch
        {
            Activity.Hunt => HuntScreen.Spotlights(target),
            Activity.Training => TrainingScreen.Spotlights(target),
            Activity.Gear => GearScreen.Spotlights(target),
            Activity.Build => _loadoutScreen.Spotlights(target),
            Activity.Mastery => MasteryScreen.Spotlights(target),
            Activity.Vault => VaultScreen.Spotlights(target),
            Activity.Forge => ForgeScreen.Spotlights(target),
            Activity.Warren => WarrenScreen.Spotlights(target),
            Activity.Map => _mapScreen.Spotlights(target),
            Activity.Traits => TraitCollectionScreen.Spotlights(target),
            Activity.Roster => RosterScreen.Spotlights(target),
            _ => Array.Empty<Rectangle>(),
        };
        if (own.Length == 0) return new[] { new Rectangle(0, 0, 1920, 1080) };
        if (screen == Activity.Hunt) return own;
        return own.Select(r =>
        {
            var lit = OverlayToCanvas(r, Vector2.Zero);
            lit.Inflate(10, 10);
            return lit;
        }).ToArray();
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
    private static Rectangle TourCardRect(IReadOnlyList<Rectangle> holes, int width, int height)
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
    /// The only full-screen modal left in the game, and it runs once per screen. Drawn last of the
    /// chrome so the scrim covers the rail too — the intro has a card about the rail, and the BUILD
    /// tour points at the MASTERY tile on it.
    /// </remarks>
    private void DrawTour()
    {
        if (!_tourActive || _tour.Count == 0) return;

        var stepNo = Math.Clamp(_tourStep, 0, _tour.Count - 1);
        var step = _tour[stepNo];
        var holes = TourSpotlights(_tourScreen, step.Target);

        // The last card points at where lessons appear — so a lesson appears there. It is the first
        // rung, the very card that will be standing in that light when the intro ends: the light
        // lifts and nothing has moved. (The live card itself is suppressed while the intro is up.)
        if (step.Target == TourTarget.LessonSlot) DrawLessonCard(TutorialStep.Watch, 1f);

        DrawScrimAround(holes, new Color(0x05, 0x03, 0x0A) * 0.74f);
        foreach (var h in holes) TourOutline(h, 3, NavGold);

        // The card's height is its lines: a title line, the wrapped body at the paragraph pitch, a
        // footer line — so a bigger profile makes a taller card, never a body that leaves its plate.
        var width = Math.Min(UiMetrics.Control(TourCardWidth), UiKit.Page.Width / 2);
        var pad = UiMetrics.Space(24);
        var lines = _ui.WrapBig(step.Body, width - pad * 2, UiTypography.Body);
        var bodyTop = pad + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
        var footerH = UiMetrics.Space(14) + UiTypography.Pitch(UiTypography.Secondary);
        var height = bodyTop + lines.Count * UiTypography.Pitch(UiTypography.Body) + footerH + pad;
        var card = TourCardRect(holes, width, height);

        _ui.Fill(_batch, card, new Color(0x15, 0x0E, 0x24, 0xF6));
        _ui.Fill(_batch, new Rectangle(card.X, card.Y, card.Width, 4), NavGold);
        _ui.Fill(_batch, new Rectangle(card.X, card.Bottom - 2, card.Width, 2), NavGem * 0.5f);

        _ui.TextBig(_batch, step.Title, card.X + pad, card.Y + pad, NavGold, UiTypography.Headline);
        _ui.TextRightBig(_batch, $"{stepNo + 1} / {_tour.Count}", card.Right - pad,
                         card.Y + pad + (UiTypography.Headline - UiTypography.Secondary) / 2, NavLabel, UiTypography.Secondary);

        var y = card.Y + bodyTop;
        foreach (var line in lines)
        {
            _ui.TextBig(_batch, line, card.X + pad, y, new Color(0xD8, 0xD2, 0xE4), UiTypography.Body);
            y += UiTypography.Pitch(UiTypography.Body);
        }

        // The last card's footer says what the click does next: the intro's hands over to the first
        // wave; every other tour's hands the screen back.
        var last = stepNo + 1 >= _tour.Count;
        var footer = !last ? "CLICK TO CONTINUE  ·  ESC SKIPS"
                   : _tourScreen == Activity.Hunt ? "CLICK TO BEGIN" : "CLICK TO FINISH";
        _ui.TextBig(_batch, footer, card.X + pad, card.Bottom - pad - UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(4),
                    NavGem, UiTypography.Secondary);
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
        _expedition.CorruptionTier = _world.CorruptionTier;   // → creature tint, the boss's epithet, the header line
        _expedition.ShowDamageNumbers = _showDamageNumbers;   // the settings' quality-of-life switches
        _expedition.ShowSkillCallouts = _showSkillCallouts;
        _expedition.ShowHitEffects = _showHitEffects;
        _expedition.ShowScreenFlash = _showScreenFlash;
        _expedition.Loadout = _loadout;               // the player's build, handed over live…
        _expedition.Tree = _dust;                     // …powered by the Dust tree's passive nodes
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
        _expedition.RegionConquered = _world.IsConquered(def.Id);
        _expedition.ChestCount = _forge.UnopenedChests.Count;   // drives the fight screen's "go open a chest" nudge
        _expedition.VaultOpen = Unlocks.IsOpen(Activity.Vault, GuideUnlockFacts());       // the rail hides a reward whose screen is locked
        _expedition.MasteryOpen = Unlocks.IsOpen(Activity.Mastery, GuideUnlockFacts());   // SPEND POINTS only shows once the tree is open
        _expedition.IdleGleamRate = _champGleamRate;            // gleam/sec the champion earns idle → HUNT idle panel
        // THE FIGHT RAIL'S REWARD BUTTONS NAVIGATE, and the host is what navigates. The screen only
        // records that they were pressed — a fight screen that opened chests itself would be a second
        // Forge, and the errand belongs to the screen that owns the verb.
        // Indices, not names — they moved when MASTERY was inserted at 4. VAULT is 5 now.
        if (_expedition.WantsVault) { _expedition.WantsVault = false; OpenNav(5); }
        // The empty vault's door out. Same shape as the HUNT's: the screen records the intent in Draw,
        // the host reads it one frame later — only the host may change screens.
        if (_vault.WantsHunt) { _vault.WantsHunt = false; OpenNav(0); }
        // TAKE ONLY edits (made in the HUNT screen's Draw) come back on the dirty flag only — never a
        // per-frame push of the saved value, which clobbered the vault's edit in playtest five.
        if (_vault.FilterDirty)
        {
            _vault.FilterDirty = false;
            _chestKeepMinTier = _vault.KeepMinTier;
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
        _guideStep = Tutorial.Showing(GuideFacts(), _dismissedGuide);

        _forge.Tuning = ForgeTuning.Default with
        {
            DismantleReturnRate = DustEffects.DismantleRate(_dust, ForgeTuning.Default.DismantleReturnRate),
        };
        // A chest's rolled loot honours the same Dust filters a boss drop did — auto-sell floor and the
        // tireless-forge auto-merge — now applied at OPEN, since that is where a chest's items land.
        // AUTO-SELL IS THE WARREN'S JOB NOW — SCAVENGER RUNS level 2 sells Commons, level 4 Uncommons.
        // (TRANSITIONAL: the trait tree still stands this phase and its two filter nodes are still
        // buyable, so the more permissive of the two answers wins. That term goes when the tree does.)
        _forge.AutoSellFloor = MorePermissive(
            WarrenAutomation.AutoSellAtOrBelow(_warren), DustEffects.AutoSellAtOrBelow(_dust));

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
        // AUTO-MERGE TOO — HOARD VAULTS level 2. (Same transitional OR as auto-sell above.)
        _forge.AutoMergeOnOpen = WarrenAutomation.AutoMergeOnChestOpen(_warren)
                                 || DustEffects.AutoMergeAfterRuns(_dust);
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
        while (_expedition.HasReward)
        {
            var r = _expedition.TakeReward();
            _hunter.AddGleam(r.Haul.Gleam);
            _champGleamAccrued += r.Haul.Gleam;
            // HAUL CORES ARE THE FORGE MATERIAL NOW. The hatchery currency they used to feed retired
            // 2026-08-24 with the creature subsystem, which left HARVEST ("ON KILL: n% CORE") and
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
            _region.RecordActiveKill(DustEffects.MasteryRate(_dust));

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
            if (r.IsBoss) _bossesFelled++;
            if (r.IsBoss && DropBossChest(r, def)) _expedition.FlashChest();   // a chest is a LOW-rate drop now, not a given
        }
        if (_champSecondsAccrued > 10) _champGleamRate = (float)(_champGleamAccrued / _champSecondsAccrued);

        _deepestEver = Math.Max(_deepestEver, _expedition.Deepest);

        // BOTH TREES' POINTS ARE DERIVED FROM PROGRESS, every frame, so they can never double-count
        // across a reload — nothing is persisted but which nodes were taken. See SkillPointsEarned and
        // TraitPointsEarned for what each one reads.
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
        _dust.SetEarned(TraitPointsEarned());

        // Conquest: the deepest the champion has held this region. Fires once, unlocks the next region.
        if (_expedition.Deepest >= ConquerWaveDepth && !_world.IsConquered(_activeRegion))
        {
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
        if (_bootTimer <= 0f || _bootMessage.Length == 0) return;
        // The first-session nudge ("your champion is already fighting") is what the intro now says in
        // eight cards; under the intro's scrim it would be a dim duplicate. Its clock still runs.
        if (_tourActive) return;
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
        // The quiet frame, like the stage header it sits under — gold is for modals (UiKit.PanelQuiet).
        _ui.PanelQuiet(_batch, r, fade);
        var pad = UiMetrics.Space(18);
        _ui.TextCenterBig(_batch, parts[0], r.Center.X, r.Y + pad, _bootColor * fade, UiTypography.OverlayTitle);
        if (parts.Length > 1)
            _ui.TextCenterBig(_batch, parts[1], r.Center.X, r.Y + pad + UiTypography.Pitch(UiTypography.OverlayTitle),
                              Bone * fade, UiTypography.OverlayBody);
    }

    /// <summary>The two-line toast's height: its pad, a title line, a body line, a breath — it follows the rungs.</summary>
    private static int ToastHeight
        => UiMetrics.Space(18) + UiTypography.Pitch(UiTypography.OverlayTitle) + UiTypography.Pitch(UiTypography.OverlayBody) + UiMetrics.Space(8);

    /// <summary>The WELCOME BACK panel's width at 100 % — wider with the profile so its lines keep their length.</summary>
    private const int WelcomeWidth = 720;

    /// <summary>
    /// WELCOME BACK: the held return panel — AWAY · HUNT · WARREN · CONTINUE. The one gold frame on screen
    /// while it is up; a modal, so nothing under it takes the click that closes it.
    /// </summary>
    /// <remarks>
    /// Every figure is <see cref="WelcomeSummary"/>'s, which is <see cref="OfflineHunt.Result"/>'s and
    /// <see cref="WarrenYield"/>'s: nothing here is estimated, and a zero is not printed. Under the intro
    /// tour it waits (<see cref="WelcomeUp"/>) rather than competing with the spotlight.
    /// </remarks>
    private void DrawWelcomePanel()
    {
        if (_welcome is not { } w) { _showWelcome = false; return; }
        var hunt = w.HuntParts();
        var warren = w.WarrenParts();
        // The panel's width follows the profile so the wrapped lines keep their length; its height is
        // its lines. The inset is the frame's own (PanelPadX): the corner ornament reaches that far.
        var width = Math.Min(UiMetrics.Control(WelcomeWidth), UiKit.Page.Width - UiMetrics.Space(40) * 2);
        const int pad = UiTypography.PanelPadX;
        var cw = width - pad * 2;
        var huntLines = hunt.Count > 0 ? _ui.WrapBig(string.Join("  ·  ", hunt), cw, UiTypography.Body) : Array.Empty<string>();
        var warrenLines = warren.Count > 0 ? _ui.WrapBig(string.Join("  ·  ", warren), cw, UiTypography.Body) : Array.Empty<string>();
        var rowGap = UiMetrics.Space(12);
        int RowH(IReadOnlyList<string> lines) => lines.Count == 0 ? 0
            : UiTypography.Pitch(UiTypography.Secondary) + lines.Count * UiTypography.Pitch(UiTypography.Body) + rowGap;
        var button = UiMetrics.Control(56);
        var h = UiTypography.ModalTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(14)
                + RowH(huntLines) + RowH(warrenLines) + UiMetrics.Space(16) + button + pad;
        var r = new Rectangle(UiKit.PageCenterX - width / 2, (UiKit.Page.Height - h) / 2 - UiMetrics.Space(40), width, h);

        _ui.Fill(_batch, UiKit.Page, new Color(0x0A, 0x08, 0x10) * 0.55f);
        _ui.Panel(_batch, r);   // gold: this is a modal, and the one thing on screen
        var x = r.X + pad;
        var y = r.Y + UiTypography.ModalTitleTop;
        _ui.TextBig(_batch, "WELCOME BACK", x, y, Gold, UiTypography.PanelTitle, TextFace.Display);
        y += UiTypography.Pitch(UiTypography.PanelTitle);
        _ui.TextBig(_batch, $"AWAY {WelcomeSummary.AwayText(w.AwaySeconds)}", x, y, Bone, UiTypography.Headline);
        y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(14);

        void Row(string label, IReadOnlyList<string> lines)
        {
            if (lines.Count == 0) return;
            _ui.TextBig(_batch, label, x, y, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.Secondary);
            foreach (var line in lines) { _ui.TextBig(_batch, line, x, y, Bone, UiTypography.Body); y += UiTypography.Pitch(UiTypography.Body); }
            y += rowGap;
        }
        Row("HUNT", huntLines);
        Row("WARREN", warrenLines);

        var btn = new Rectangle(x, r.Bottom - pad - button, cw, button);
        if (_ui.Button(_batch, btn, "CONTINUE", ChromeMouse, _clicked, true, ButtonStyle.Primary)) _showWelcome = false;
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
    private const int PageContentTop = 150;

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

    private bool Pressed(Keys k) => !_swallowInput && _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

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
    private static readonly (bool Page, float X, float Y)? PosedCursor = ParsePosedCursor();

    private static (bool Page, float X, float Y)? ParsePosedCursor()
    {
        if (Environment.GetEnvironmentVariable("RH_SHOT_PAGE_MOUSE") is { } pm && ParsePair(pm, out var px, out var py))
            return (true, px, py);
        if (Environment.GetEnvironmentVariable("RH_SHOT_MOUSE") is { } sm && ParsePair(sm, out var mx, out var my))
            return (false, mx * ArtScale, my * ArtScale);
        return null;

        static bool ParsePair(string text, out float x, out float y)
        {
            x = y = 0f;
            var parts = text.Split(',');
            return parts.Length == 2
                && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)
                && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
        }
    }

    /// <summary>Map the raw mouse through this frame's geometry, once. Called at the top of Update.</summary>
    private void ReadCursor()
    {
        (float X, float Y) chrome, page;
        if (PosedCursor is { } posed)
        {
            if (posed.Page)
            {
                page = (posed.X, posed.Y);
                chrome = (posed.X * OverlayScale + OverlayLeft, posed.Y * OverlayScale);
            }
            else
            {
                chrome = (posed.X, posed.Y);
                page = ((posed.X - OverlayLeft) / OverlayScale, posed.Y / OverlayScale);
            }
        }
        else
        {
            chrome = _frame.ScreenToCanvas(_mouse.X, _mouse.Y);
            page = _frame.ScreenToPage(_mouse.X, _mouse.Y);
        }
        ChromeMouseF = new Vector2(chrome.X, chrome.Y);
        PageMouseF = new Vector2(page.X, page.Y);
        var (cx, cy) = PageFrame.Floor(chrome);
        ChromeMouse = new Point(cx, cy);
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

    private bool MouseClicked => _clicked && !_showSettings && !_showHelp && !WelcomeUp && !_swallowInput;
    private bool MouseRightClicked => _rightClicked && !_showSettings;


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
        var kick = (_showTraits ? _traits.Shake : Vector2.Zero) + ScreenSettle;
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
            _forge.Draw(_batch, _hunter, PageCursor, MouseClicked, MouseRightClicked);
            // Each operation names itself — upgrade, re-roll, socket, salvage — instead of every one of
            // them borrowing the same hammer.
            PlayCue(_forge.ConsumeCue(), 0.9f);
        }
        else if (_showWorld) DrawWorld();
        else if (_showTraits)
        {
            if (_showDustTree) _traits.Draw(_batch, _dust, PageCursor, MouseClicked);
            else
            {
                _traitScreen.Draw(_batch, _traitLedger, _characters.ActiveId,
                                  _characters.Active?.Name ?? "YOUR HUNTER", PageCursor, MouseClicked);
                PlayCue(_traitScreen.ConsumeCue(), 0.9f);
                // WEARING A TRAIT IS A SAVE. It is per-character state and the only place it lives is
                // the file; the ten-second autosave would get there eventually, and "eventually" is
                // how a crash costs someone their build.
                if (_traitScreen.Dirty) Save();
            }
        }
        else if (_showRoster)
        {
            _roster.Progress = QuestSnapshot();
            _roster.Mastery = _mastery;
            _roster.Hunter = _hunter;
            _roster.Draw(_batch, _characters, PageCursor, MouseClicked);
            PlayCue(_roster.ConsumeCue(), 0.6f);   // SET ACTIVE is a navigation: the rail's own page tick
        }
        else if (_showVault) _vault.Draw(_batch, _forge.UnopenedChests, PageCursor, MouseClicked);
        else if (_showLoadout)
        {
            _loadoutScreen.Draw(_batch, PageCursor, MouseClicked);
            PlayCue(_loadoutScreen.ConsumeCue());   // the refusal LAW 13 already says in words
        }
        else if (_showWarren) DrawWarren();
        else if (_showMastery) _masteryScreen.Draw(_batch, PageCursor, _dust);
        else if (_showGear) _gear.Draw(_batch, PageCursor, _hunter);
        else if (_showTraining)
        {
            _training.Draw(_batch, PageCursor, _hunter, MouseClicked);

            // Gleam is one of the three payouts a descent makes, and this is the layer it buys. The model
            // (geometric cost, rank cap) has always been here; until now nothing in the game called it.
            // TRAIN has its own cue (two notes up) instead of the ordinary click, and the screen's
            // own cue carries the refusal when the gleam is not there.
            if (_training.ConsumeTrain() is { } stat && _hunter.Train(stat)) { _sound.Play("sfx_train", 0.8f); Save(); }
            PlayCue(_training.ConsumeCue());
            if (_training.ConsumeReset() && _hunter.ResetTraining()) { _sound.Play("sfx_forge", 0.8f); Save(); }
        }
        else _expedition.Draw(_batch, ChromeMouse, MouseClicked, Regions.Get(_activeRegion).Name, EnemyArtFor(_activeRegion), _bootTimer > 0f || WelcomeUp);

        // The LOG draws over everything, including the nav rail: it is a full-screen read, and the one
        // overlay a player opens to think rather than to act.
        _expedition.DrawLog(_batch, ChromeMouse, MouseClicked);
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
        DrawHexNav();   // the shared nav bar, over every screen

        // The chest burst, over the rail and over whatever screen is open — it is the one moment the
        // game asks the player to stop and look, so nothing on the page should sit on top of it.
        if (_forge.RevealActive) _forge.DrawRevealOverlay(_batch, _hunter);

        // A CLOSED MODAL IS BACK AT ITS TOP. Both scrolls only ever leave zero at 150 %, where the
        // rows outgrow the page — and the UI SCALE row is at the top of the settings panel, so a
        // scroll that survived the close would hide the one control that can undo 150 %.
        if (!_showHelp) _helpScroll = 0;
        if (!_showSettings) _settingsScroll = 0;

        if (_showHelp) DrawHelp();
        if (_showTypeSpec) DrawTypeSpec();
        if (_showSettings) DrawSettings();

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

        _batch.End();

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
                System.IO.File.WriteAllLines(shotPath + ".events.txt", _expedition.DevWaveEvents());

            using var fs = System.IO.File.Create(shotPath);
            _canvas.SaveAsPng(fs, CanvasWidth * ArtScale, CanvasHeight * ArtScale);
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

    // ── Title screen ──────────────────────────────────────────────────────────────────────────
    private void ChooseTitleItem(int i)
    {
        switch (i)
        {
            case 0: _showTitle = false; break; // PLAY / CONTINUE
            case 1: _showSettings = true; break;
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

    /// <summary>The panel's height at 100 % — the floor it grows from.</summary>
    private const int SetDesignHeight = 880;

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
        public int DisplayY, ScaleCaptionY, AudioY, ControlsY, ControlsTextY;
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
        var padBottom = UiMetrics.Space(44);
        var footerH = UiMetrics.Space(8) + button + padBottom;

        // The left column, flowing down from the header (y relative to the content top).
        var modeY = rule + ruleToRow;
        var sizeY = modeY + fieldPitch;
        var scaleY = sizeY + fieldPitch;
        var scaleCapY = scaleY + fieldH + UiMetrics.Space(6);
        var audioY = scaleCapY + captionPitch + sectionGap;
        var fxRowY = audioY + rule + ruleToRow;
        var controlsY = fxRowY + sliderPitch * 2 + sectionGap;
        var controlsTextY = controlsY + rule + ruleToRow + UiMetrics.Space(10);
        var leftRows = controlsTextY + bodyPitch * 2;
        var copyH = button + UiMetrics.Space(8) + captionPitch + UiMetrics.Space(10);
        var leftFlow = leftRows + sectionGap + copyH;

        // The right column.
        var askY = rule + UiMetrics.Space(12);
        var accessY = askY + button + UiMetrics.Space(22);
        var toggleY = accessY + rule + UiMetrics.Space(20);
        var togglePitch = UiMetrics.Control(58);
        var accessCapY = toggleY + togglePitch * 4 + UiMetrics.RowHeight + UiMetrics.Space(6);
        var rightRows = accessCapY + captionPitch;
        var dangerTextY = UiMetrics.Space(44);
        var dangerBtnY = dangerTextY + body + UiMetrics.Space(18);
        var dangerH = dangerBtnY + button + UiMetrics.Space(20);
        var rightFlow = rightRows + sectionGap + dangerH;

        // The panel: the design height at 100 %, taller when the profile asks, never past the page.
        var needed = headerH + Math.Max(leftFlow + footerH, rightFlow + padBottom);
        var maxH = page.Height - UiMetrics.Space(12) * 2;
        var h = Math.Clamp(needed, SetDesignHeight, maxH);
        var scrolls = needed > h;
        var f = new SettingsFrame
        {
            Panel = new Rectangle(UiKit.PageCenterX - width / 2, page.Y + (page.Height - h) / 2, width, h),
            Scrolls = scrolls, ColW = colW, Rule = rule, TogglePitch = togglePitch,
            DangerTitleY = UiMetrics.Space(12), DangerTextY = dangerTextY,
            ContentHeight = Math.Max(leftFlow, rightFlow),
        };
        f.Close = UiKit.CloseRect(f.Panel);
        f.Top = f.Panel.Y + headerH;
        f.LeftX = f.Panel.X + SetInset;
        f.RightX = f.LeftX + colW + colGap;
        // In scroll mode the right column gives up a lane for the scrollbar.
        f.RightW = scrolls ? colW - UiMetrics.ScrollbarWidth - UiMetrics.Space(8) : colW;
        f.Quit = new Rectangle(f.LeftX, f.Panel.Bottom - padBottom - button, wide, button);
        f.View = new Rectangle(f.Panel.X + SetInset / 2, f.Top, f.Panel.Width - SetInset, f.Quit.Y - UiMetrics.Space(8) - f.Top);

        var t = f.Top;
        f.DisplayY = t;
        f.ModeRow = new Rectangle(f.LeftX + labelW, t + modeY, colW - labelW, fieldH);
        f.SizeRow = new Rectangle(f.LeftX + labelW, t + sizeY, colW - labelW, fieldH);
        f.ScaleRow = new Rectangle(f.LeftX + labelW, t + scaleY, colW - labelW, fieldH);
        f.ScaleCaptionY = t + scaleCapY;
        f.AudioY = t + audioY;
        f.FxTrack = new Rectangle(f.LeftX + labelW, t + fxRowY + UiMetrics.Space(20), trackW, trackH);
        f.MusicTrack = new Rectangle(f.LeftX + labelW, f.FxTrack.Y + sliderPitch, trackW, trackH);
        f.ControlsY = t + controlsY;
        f.ControlsTextY = t + controlsTextY;

        f.GameplayY = t;
        var askW = UiMetrics.Control(260);
        f.AskBtn = new Rectangle(f.RightX + f.RightW - askW, t + askY, askW, button);
        f.AskLabelY = f.AskBtn.Y + (button - body) / 2;
        f.AccessY = t + accessY;
        f.ToggleY = t + toggleY;
        f.AccessCaptionY = t + accessCapY;

        // The bottom blocks: pinned to the panel's foot when the rows fit, flowing after them when they scroll.
        var copyY = scrolls ? t + leftRows + sectionGap
                            : f.Quit.Y - UiMetrics.Space(10) - captionPitch - UiMetrics.Space(8) - button;
        f.CopyFeedback = new Rectangle(f.LeftX, copyY, wide, button);
        // THE DANGER ZONE (brief §37): its own bordered region, overhanging the column by a breath, so
        // the button that deletes a save is not one of a pair of identical buttons.
        var overhang = UiMetrics.Space(24);
        var dangerY = scrolls ? t + rightRows + sectionGap : f.Panel.Bottom - padBottom - dangerH;
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

    /// <summary>Guide rungs the player closed by hand (TutorialStep names). Persisted in the save.</summary>
    private readonly HashSet<string> _dismissedGuide = new();

    /// <summary>How long the armed are-you-sure state stays live before disarming itself.</summary>
    private const float ResetArmSeconds = 4f;

    /// <summary>The persistent way back here — a gear in the corner, the convention every game teaches.</summary>
    // Level with the currency pills (their capsules run y 22..70, centre 46): the gear used to sit at
    // y 8..68, centre 38 — eight pixels above the row it belongs to (playtest 2026-08-26: "it sits a
    // little higher and catches the eye").
    // A page anchor: the pills' capsule is a fixed 60 (UiKit.Pill), so the gear's slot is too.
    private static Rectangle SettingsGear => new(UiKit.PageRight(78), 16, 60, 60);

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
    private void DrawSettings()
    {
        var f = SettingsFrameNow();
        var panel = f.Panel;
        _ui.Scrim(_batch, 0.75f);
        _ui.Panel(_batch, panel);
        _ui.TextCenterBig(_batch, "SETTINGS", panel.Center.X, panel.Y + UiTypography.ModalTitleTop,
                          Gold, UiTypography.PanelTitle);

        // A click reaches the ordinary rows only while no dropdown list is open. The open list is
        // drawn over them, so its clicks — and the click that closes it — must be swallowed here.
        var uiClick = _clicked && _settingsDropdown == 0;
        var mouse = ChromeMouse;

        // The COPIED confirmation, in the header row beside the close icon — the one line no row shares.
        if (_feedbackToastTimer > 0f)
            _ui.TextRightBig(_batch, _feedbackToast, f.Close.X - UiMetrics.Space(16),
                             panel.Y + UiTypography.ModalTitleTop + (UiTypography.PanelTitle - UiTypography.Secondary) / 2,
                             Gold, UiTypography.Secondary);

        // ── THE SCROLL. When the rows outgrow the room above the QUIT row (150 %), they scroll under
        //    the header: a wheel notch is one list row, the range is the overflow, and a click lands
        //    only where a row is actually visible. Nothing scrolls at 100 % or 125 %.
        var maxScroll = f.Scrolls ? Math.Max(0, f.ContentHeight - f.View.Height) : 0;
        if (maxScroll > 0 && _settingsDropdown == 0 && MouseWheel != 0 && f.View.Contains(mouse))
            _settingsScroll -= MouseWheel * UiMetrics.RowHeight;
        // DEV ONLY: pose the panel scrolled, so the state a 150 % player actually reaches can be
        // photographed. RH_SHOT_SCROLL=<rows>, and "end" for the bottom of the list — the two rows
        // that sit under the clip line at 150 % (COPY FEEDBACK CODE, START A NEW GAME) are otherwise
        // a state no capture mode can pose, which in this project is the same as a state nobody has
        // ever looked at. The same variable name the map and the warren already use for the same job.
        if (RigActive && maxScroll > 0
            && Environment.GetEnvironmentVariable("RH_SHOT_SCROLL") is { Length: > 0 } posed)
            _settingsScroll = posed.Equals("end", StringComparison.OrdinalIgnoreCase) ? maxScroll
                : int.TryParse(posed, out var rows) ? rows * UiMetrics.RowHeight
                : _settingsScroll;
        _settingsScroll = Math.Clamp(_settingsScroll, 0, maxScroll);
        var dy = -_settingsScroll;
        var rowsVisible = !f.Scrolls || f.View.Contains(mouse);
        var rowClick = uiClick && rowsVisible;
        Rectangle Row(Rectangle r) => new(r.X, r.Y + dy, r.Width, r.Height);

        // ── GROUPED BY PURPOSE, IN TWO COLUMNS (brief §36). Left: what the game looks and sounds
        //    like. Right: how it behaves, what it shows in a fight, and the one destructive door. ──
        void Group(string name, int x, int y, int w)
        {
            _ui.TextBig(_batch, name, x, y, Gold, UiTypography.Secondary);
            _ui.Fill(_batch, new Rectangle(x, y + f.Rule, w, 1), new Color(0x3A, 0x3A, 0x44));
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
        if (_ui.Button(_batch, scaleRow, Display.UiScaleLabel(_uiScalePercent), mouse, rowClick))
        {
            CycleUiScale();
            SaveDisplay();
        }
        _ui.TextBig(_batch, "AUTO PICKS 125% IN A SMALL WINDOW", f.LeftX, f.ScaleCaptionY + dy,
                    Slate, UiTypography.Secondary);

        // ── AUDIO ────────────────────────────────────────────────────────────────────────────────
        Group("AUDIO", f.LeftX, f.AudioY + dy, f.ColW);

        var fxTrack = Row(f.FxTrack);
        var fx = SliderRow("EFFECTS VOLUME", fxTrack, _sfxVolume, 1, rowClick, f.LeftX);
        if (fx >= 0 && fx != _sfxVolume)
        {
            _sfxVolume = fx;
            _sound.SfxVolume = _sfxVolume / 100f;   // live, so the release click previews the new level
        }

        var musicTrack = Row(f.MusicTrack);
        var mu = SliderRow("MUSIC VOLUME", musicTrack, _musicVolume, 2, rowClick, f.LeftX);
        if (mu >= 0 && mu != _musicVolume)
        {
            _musicVolume = mu;
            _sound.MusicVolume = _musicVolume / 100f;   // the playing bed follows the drag instantly
        }

        // The drag ends when the button does. Persist ONCE here, not on every dragged frame.
        if (_dragSlider != 0 && _mouse.LeftButton == ButtonState.Released)
        {
            if (_dragSlider == 1) _sound.PlayFirst(1f, "sfx_click", "sfx_forge");
            _dragSlider = 0;
            SaveDisplay();
        }

        // ── CONTROLS. Only what the game really has: the keys, and where they are listed. ────────
        Group("CONTROLS", f.LeftX, f.ControlsY + dy, f.ColW);
        Label("EVERY KEY IS LISTED IN HELP — PRESS F1", f.LeftX, f.ControlsTextY + dy, Slate);
        Label("KEYS CANNOT BE REBOUND YET", f.LeftX, f.ControlsTextY + dy + UiTypography.Pitch(UiTypography.Body), Slate);

        // ── GAMEPLAY ─────────────────────────────────────────────────────────────────────────────
        Group("GAMEPLAY", f.RightX, f.GameplayY + dy, f.RightW);
        var askBtn = Row(f.AskBtn);
        // The label takes what the button leaves — it can never print through it.
        Label(_ui.ShortenBig("ASK BEFORE SELL OR SALVAGE", askBtn.X - UiMetrics.Space(12) - f.RightX, UiTypography.Body),
              f.RightX, f.AskLabelY + dy, Bone);
        if (_ui.Button(_batch, askBtn, _askBeforeScrap ? "ON — IT ASKS" : "OFF", mouse, rowClick))
        {
            _askBeforeScrap = !_askBeforeScrap;
            _forge.AskBeforeScrap = _askBeforeScrap;
            SaveDisplay();
        }

        // ── ACCESSIBILITY. The fight's text and effects, and the motion switch. ──────────────────
        Group("ACCESSIBILITY", f.RightX, f.AccessY + dy, f.RightW);
        // Five switches, one per row, each the full column width. Two to a row they were 260 px
        // apart and the second label sat under the first row's button.
        var changed = false;
        var sy = f.ToggleY + dy;
        changed |= ToggleRow("DAMAGE NUMBERS", f.RightX, sy, f.RightW, ref _showDamageNumbers, rowClick);
        sy += f.TogglePitch;
        changed |= ToggleRow("SKILL NAMES", f.RightX, sy, f.RightW, ref _showSkillCallouts, rowClick);
        sy += f.TogglePitch;
        changed |= ToggleRow("FIGHT EFFECTS", f.RightX, sy, f.RightW, ref _showHitEffects, rowClick);
        sy += f.TogglePitch;
        changed |= ToggleRow("RED FLASH", f.RightX, sy, f.RightW, ref _showScreenFlash, rowClick);
        sy += f.TogglePitch;
        var reduced = ReducedMotion;
        changed |= ToggleRow("REDUCED MOTION", f.RightX, sy, f.RightW, ref reduced, rowClick);
        if (reduced != ReducedMotion) ReducedMotion = reduced;
        if (changed) SaveDisplay();
        _ui.TextBig(_batch, "REDUCED MOTION HOLDS IDLE ANIMATIONS STILL", f.RightX, f.AccessCaptionY + dy,
                    Slate, UiTypography.Secondary);

        // ── The feedback code, and the one door that destroys something. ─────────────────────────
        var copyBtn = Row(f.CopyFeedback);
        if (_resetArmTimer <= 0f && _ui.Button(_batch, copyBtn, "COPY FEEDBACK CODE", mouse, rowClick))
        {
            // The failure is NOT silent (same rule as the weave's copy button): no clipboard, no lie.
            _feedbackToast = ClipboardInterop.TrySet(FeedbackCode())
                ? "COPIED — PASTE IT TO THE DEVELOPER"
                : "COPY FAILED — TRY AGAIN";
            _feedbackToastTimer = 4f;
        }
        _ui.TextBig(_batch, "SENDS THE DEVELOPER YOUR BUILD AND PROGRESS", f.LeftX,
                    copyBtn.Bottom + UiMetrics.Space(8), Slate, UiTypography.Secondary);

        // DANGER ZONE — its own bordered region, so the button that deletes a save is not one of a
        // pair of identical buttons (brief §37).
        var danger = Row(f.Danger);
        var newGame = Row(f.NewGame);
        var armed = Row(f.NewGameArmed);
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
            if (UiKit.ClickedIn(armed, mouse, rowClick))
            {
                _resetArmTimer = 0f;
                _wantsNewGame = true;
            }
            else if (_clicked)
            {
                _resetArmTimer = 0f;   // any click that is not the confirmation disarms
            }
        }
        else
        {
            _ui.TextBig(_batch, _ui.ShortenBig("DELETES THIS SAVE AND STARTS OVER. NOTHING COMES BACK.", danger.Width - dangerPad * 2, UiTypography.Body),
                        danger.X + dangerPad, danger.Y + f.DangerTextY, Bone, UiTypography.Body);
            if (_ui.Button(_batch, newGame, "START A NEW GAME", mouse, rowClick))
                _resetArmTimer = ResetArmSeconds;
        }

        if (f.Scrolls)
        {
            EndChromeClip();
            var lane = UiMetrics.ScrollbarWidth;
            _ui.ScrollBar(_batch, new Rectangle(panel.Right - SetInset - lane, f.View.Y, lane, f.View.Height),
                          _settingsScroll, f.View.Height, f.ContentHeight);
        }

        if (_ui.CloseButton(_batch, f.Close, mouse, uiClick))
        {
            _showSettings = false;
            _settingsDropdown = 0;
        }

        // Esc no longer quits (it opens THIS panel), so the game needs a door that says what it does.
        // Hidden on the title screen, whose own menu already has QUIT — and whose Hunter may not exist
        // yet to save. It is the game's exit — the door Esc used to be — and it still saves on the way out.
        if (!_showTitle && _ui.Button(_batch, f.Quit, "QUIT TO DESKTOP", mouse, uiClick))
        {
            Save();
            Exit();
        }

        // Which build this is — the same stamp the feedback code carries, so "which version are you
        // on" is answerable from a screenshot. Under the panel while the page has room for it; when the
        // panel has grown to the page, in the header row's left end — the one line no row shares,
        // mirroring the close icon. (Its first home up here was the footer's right end, which at 125 %
        // is where the DANGER ZONE's button sits when the rows do not scroll.)
        var stamp = $"BUILD {BuildStamp.Short}";
        if (panel.Bottom + UiMetrics.Space(20) + UiTypography.Pitch(UiTypography.Body) <= UiKit.Page.Bottom)
            Text(stamp, panel.X + UiMetrics.Space(45), panel.Bottom + UiMetrics.Space(20), Slate);
        else
            _ui.TextBig(_batch, stamp, f.LeftX, panel.Y + UiTypography.ModalTitleTop + (UiTypography.PanelTitle - UiTypography.Secondary) / 2,
                        Slate, UiTypography.Secondary);

        // ── The OPEN dropdown list, drawn last so it sits over every row below it. A click on an
        //    option applies and closes; any other click just closes — and either way the rows under
        //    the list never see it (uiClick above). ──
        var openList = Rectangle.Empty;
        if (_settingsDropdown == 1)
        {
            openList = DropdownList(modeRow, modeNames, modeIdx, pick =>
            {
                var modes = new[] { DisplayMode.Windowed, DisplayMode.Borderless, DisplayMode.Fullscreen };
                if (_displayMode == modes[pick]) return;
                _displayMode = modes[pick];
                ApplyDisplay();
                SaveDisplay();
            });
        }
        else if (_settingsDropdown == 2)
        {
            var labels = new string[sizes.Length];
            for (var i = 0; i < labels.Length; i++)
                labels[i] = Display.WindowSizeLabel(sizes[i], desktop.Width, desktop.Height);
            openList = DropdownList(sizeRow, labels, Array.IndexOf(sizes, _windowSize), pick =>
            {
                if (_windowSize == sizes[pick]) return;
                _windowSize = sizes[pick];
                ApplyDisplay();
                SaveDisplay();
            });
        }
        else if (_clicked && rowsVisible)
        {
            // No list open: a click on a closed row opens its list, with the cursor unplaced so the
            // list puts it on the value the player already has.
            if (modeRow.Contains(mouse)) OpenDropdown(1);
            else if (windowed && sizeRow.Contains(mouse)) OpenDropdown(2);
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
    private Rectangle DropdownList(Rectangle field, string[] options, int currentIndex, Action<int> pick)
    {
        if (options.Length == 0) { CloseDropdown(); return Rectangle.Empty; }

        // The cursor lands on the current value the first frame the list is up.
        if (_dropCursor < 0 || _dropCursor >= options.Length) _dropCursor = Math.Max(0, currentIndex);
        if (_dropMove != 0)
        {
            _dropCursor = ((_dropCursor + _dropMove) % options.Length + options.Length) % options.Length;
            _dropMove = 0;
        }

        // How many rows fit between the field and the bottom of the settings panel.
        var top = field.Bottom + DropGap;
        var room = SettingsPanel.Bottom - UiKit.PanelCorner - top - DropListInset * 2;
        var rows = Math.Clamp(room / DropRowHeight, 1, options.Length);

        if (rows < options.Length && MouseWheel != 0) _dropScroll -= MouseWheel;
        _dropScroll = Math.Clamp(_dropScroll, 0, options.Length - rows);
        if (_dropCursor < _dropScroll) _dropScroll = _dropCursor;
        else if (_dropCursor >= _dropScroll + rows) _dropScroll = _dropCursor - rows + 1;

        var list = new Rectangle(field.X, top, field.Width, rows * DropRowHeight + DropListInset * 2);
        // The frame's own centre is opaque, so it IS the list's surface — no flat rectangle underneath,
        // which would square off the corners the ornament is shaped around.
        _ui.PanelQuiet(_batch, list);

        var mouse = ChromeMouse;
        var inner = new Rectangle(list.X + DropListInset, list.Y + DropListInset,
                                  list.Width - DropListInset * 2, rows * DropRowHeight);
        // A list that scrolls gives its rows up a lane on the right for the house scrollbar.
        var scrolling = rows < options.Length;
        var rowW = inner.Width - (scrolling ? UiMetrics.ScrollbarWidth + UiMetrics.Space(6) : 0);
        var mark = UiMetrics.Control(10);   // the diamond on the value you already have

        if (_dropCommit)
        {
            _dropCommit = false;
            var chosen = _dropCursor;
            CloseDropdown();
            pick(chosen);
            return list;
        }

        for (var k = 0; k < rows; k++)
        {
            var i = _dropScroll + k;
            var r = new Rectangle(inner.X, inner.Y + k * DropRowHeight, rowW, DropRowHeight);
            if (r.Contains(mouse)) _dropCursor = i;   // the mouse and the arrow keys share one cursor
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

            if (!_clicked || !r.Contains(mouse)) continue;
            CloseDropdown();
            pick(i);
            return list;
        }

        // The scrollbar, only when there is something to scroll — a bar that is always full is noise.
        if (scrolling)
            _ui.ScrollBar(_batch, new Rectangle(inner.Right - UiMetrics.ScrollbarWidth, inner.Y, UiMetrics.ScrollbarWidth, inner.Height),
                          _dropScroll, rows, options.Length);

        // A click anywhere else — the closed field included — just closes the list.
        if (_clicked) CloseDropdown();
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
    private bool ToggleRow(string label, int x, int y, int width, ref bool value, bool clicked)
    {
        var h = UiMetrics.RowHeight;
        var w = UiMetrics.Control(116);
        _ui.TextBig(_batch, _ui.ShortenBig(label, width - w - UiMetrics.Space(12), UiTypography.Body), x,
                    y + (h - UiTypography.Body) / 2, Bone, UiTypography.Body);
        var btn = new Rectangle(x + width - w, y, w, h);
        if (!_ui.Button(_batch, btn, value ? "ON" : "OFF", ChromeMouse, clicked)) return false;
        value = !value;
        return true;
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
    private int SliderRow(string label, Rectangle track, int current, int dragId, bool clickable, int labelX)
    {
        var textY = track.Y + (track.Height - UiTypography.Body) / 2;
        _ui.TextBig(_batch, label, labelX, textY, Bone, UiTypography.Body);

        var bedH = UiMetrics.Control(8);
        var bed = new Rectangle(track.X, track.Center.Y - bedH / 2, track.Width, bedH);
        _ui.Fill(_batch, bed, new Color(0x2A, 0x24, 0x38));
        var fillW = (int)(track.Width * (current / 100f));
        if (fillW > 0) _ui.Fill(_batch, new Rectangle(bed.X, bed.Y, fillW, bedH), new Color(0xC8, 0x9A, 0x3C));
        var handleW = UiMetrics.Control(14);
        _ui.Fill(_batch, new Rectangle(track.X + fillW - handleW / 2, track.Y, handleW, track.Height), Bone);
        _ui.TextBig(_batch, $"{current}%", track.Right + UiMetrics.Space(14), textY, Slate, UiTypography.Body);

        var mouse = ChromeMouse;
        var grabX = UiMetrics.Space(10);
        var grabY = UiMetrics.Space(6);
        var grab = new Rectangle(track.X - grabX, track.Y - grabY, track.Width + grabX * 2, track.Height + grabY * 2);
        if (clickable && _clicked && grab.Contains(mouse)) _dragSlider = dragId;
        if (_dragSlider == dragId && _mouse.LeftButton == ButtonState.Pressed)
            return Math.Clamp((int)MathF.Round((mouse.X - track.X) * 100f / track.Width), 0, 100);
        return -1;
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
                if (z.Zone.Contains(mouse)) { key = z.Key; tip = z.Tip; break; }

        if (key != _tipKey)
        {
            _tipKey = key;
            _tipTimer = 0f;
            return;
        }
        if (key.Length > 0 && _tipTimer >= TipDelaySeconds) _ui.HoverTip(_batch, tip, mouse);
    }

    /// <summary>The corner gear — the way back to settings from any screen, including mid-hunt.</summary>
    private void DrawSettingsGear()
    {
        if (_showSettings || _showHelp) return;   // a modal owns the frame
        var hover = SettingsGear.Contains(ChromeMouse);
        var c = new Vector2(SettingsGear.Center.X, SettingsGear.Center.Y);
        var ink = hover ? NavGold : NavLabel * 0.8f;

        if (_ui.Assets.Get("icon_settings") is { } gearArt)
        {
            // The drawn medallion (PixelLab, the close icon's sibling): 52 px at rest, 56 under the mouse.
            var side = hover ? 56 : 52;
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
        _ui.Fill(_batch, new Rectangle((int)c.X - 3, (int)c.Y - 3, 6, 6), new Color(0x0C, 0x09, 0x16));

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

        // THE MODAL. The rising edge starts it; a closed modal leaves nothing behind.
        var modal = ModalUpNow;
        if (modal && !_modalWasUp) _modalFade = UiMotion.Fast;
        if (!modal) _modalFade = 0f;
        _modalWasUp = modal;
        _modalFade = Math.Max(0f, _modalFade - dt);

        // THE PILLS. Order matches DrawCurrencyPills: scrap, dust, gleam.
        Span<long> now = stackalloc long[3];
        now[0] = (long)_hunter.MaterialOf(Material.Scrap);
        now[1] = (long)_dust.MemoryDust;
        now[2] = _hunter.Gleam;

        _pillWarm += dt;
        for (var i = 0; i < 3; i++)
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
        // Rev 4 §20: ONE currency per capsule — no packed "S.. E.. C.. X..". The three most Hunt-relevant
        // (Gleam, Dust, Scrap); the finer materials (Essence/Core/Crystal) live on the Forge. Three capsules
        // fit the bar's 686px without crossing into the stage header.
        // Rev 4 §20.4: icon + abbreviated value ONLY (no long text label). A labelled pill with an 8-digit
        // value (e.g. 130.6M GLEAM) balloons past the 686px bar and crosses into the stage header — the icon
        // carries the identity, the number is abbreviated. The check is a crash-SAFE dev warning, never a
        // Debug.Assert (a failed assert aborts the game's Debug build — the "Continue" crash).
        var scrapVal = (long)_hunter.MaterialOf(Material.Scrap);
        var dustVal = (long)_dust.MemoryDust;
        var gleamVal = _hunter.Gleam;

        // THE CHAIN IS MEASURED BEFORE IT IS DRAWN (UI polish P2). The value is drawn at the Label rung,
        // which follows the profile, so at 150 % three late-game "130.6M"s would run the row into the
        // stage header. When the chain would cross PillChainMinLeft every value drops its decimals —
        // "131M" — the icon carries the identity and the hover the exact figure, so nothing is lost.
        // UiKit.Pill itself is untouched: its capsule is a fixed 60 and the row's y stays with it.
        // WHAT THE PILL PRINTS is the walking number (see TickChromeMotion), not the true one — a spend
        // or a payout moves it to its new figure over a transition instead of swapping it between two
        // frames. The HOVER still reads the exact, true value: the animation is for the corner of the
        // eye, and the tooltip is for the question "how much exactly".
        var walk = new[] { (long)Math.Round(_pillShown[0]), (long)Math.Round(_pillShown[1]), (long)Math.Round(_pillShown[2]) };
        var values = new[] { Abbrev(walk[0]), Abbrev(walk[1]), Abbrev(walk[2]) };
        if (PillChainLeft(values) < PillChainMinLeft)
            values = new[] { Abbrev(walk[0], compact: true), Abbrev(walk[1], compact: true), Abbrev(walk[2], compact: true) };

        var e1 = PillRowRight;
        var l1 = _ui.Pill(_batch, e1, PillRowTop, "mat_scrap", new Color(0x9A, 0xC0, 0x88), values[0], "", new Color(0x9A, 0xC0, 0x88));
        var e2 = l1 - PillGap;
        var l2 = _ui.Pill(_batch, e2, PillRowTop, "ui_memory_dust", default, values[1], "", new Color(0x9E, 0x86, 0xFF));
        var e3 = l2 - PillGap;
        var leftEdge = _ui.Pill(_batch, e3, PillRowTop, "ui_gleam_coin", default, values[2], "", UiInk.Accent);
        if (leftEdge < PillChainMinLeft) System.Diagnostics.Debug.WriteLine($"Currency bar (left {leftEdge}) crowds the stage header.");

        // WHAT AM I LOOKING AT. The pills are icon + "4.2M", which names neither the resource nor the
        // real figure — hover does both, in plain words and exact digits (playtest: "3 kaynağın ne
        // olduğu anlaşılır değil... tam sayısı yazsın üstüne gelince"). Invariant grouping, so the
        // number reads the same on every machine. The SCRAP pill's hover lists ALL FOUR material
        // tiers — Essence, Core and Crystal have no pill of their own, and "Core sayısını
        // göremiyorum" is exactly the question this answers.
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var allMats = $"SCRAP {scrapVal.ToString("N0", inv)}  ·  ESSENCE {_hunter.MaterialOf(Material.Essence).ToString("N0", inv)}"
                      + $"  ·  CORE {_hunter.MaterialOf(Material.Core).ToString("N0", inv)}"
                      + $"  ·  CRYSTAL {_hunter.MaterialOf(Material.Crystal).ToString("N0", inv)}";
        var pillRows = new (Rectangle R, string Name, long V)[]
        {
            (new Rectangle(l1, PillRowTop, e1 - l1, PillHeight), allMats, -1),
            (new Rectangle(l2, PillRowTop, e2 - l2, PillHeight), "MEMORY DUST — STARTS A DESCENT FROM A WAVE YOU HAVE CLEARED (MAP) · BUILDS THE WARREN", dustVal),
            (new Rectangle(leftEdge, PillRowTop, e3 - leftEdge, PillHeight), "GLEAM — PAYS FOR TRAINING (V)", gleamVal),
        };
        // THE SPEND REACTS HERE (brief sec. 37), at the pill, rather than by flying a coin across the
        // page: the capsule takes a rim in its own colour that fades over a transition. A flash is a
        // state change rather than movement, so Reduced Motion keeps it — what Reduced drops is the
        // easing, and TickChromeMotion has already landed the number by the time it gets here.
        var pillInk = new[] { new Color(0x9A, 0xC0, 0x88), new Color(0x9E, 0x86, 0xFF), UiInk.Accent };
        for (var i = 0; i < 3; i++)
        {
            var rr = pillRows[i].R;
            if (_pillFlash[i] > 0f)
            {
                var a = _pillFlash[i] / UiMotion.Transition;
                var ink = pillInk[i] * (a * 0.75f);
                _ui.Fill(_batch, new Rectangle(rr.X, rr.Y, rr.Width, 2), ink);
                _ui.Fill(_batch, new Rectangle(rr.X, rr.Bottom - 2, rr.Width, 2), ink);
                _ui.Fill(_batch, new Rectangle(rr.X, rr.Y, 2, rr.Height), ink);
                _ui.Fill(_batch, new Rectangle(rr.Right - 2, rr.Y, 2, rr.Height), ink);
            }

            // AND A SUBSTANTIAL GAIN SAYS SO (brief sec. 38), under its own pill, once — the bank in
            // TickChromeMotion decides what counts as substantial. Never while the pill is hovered:
            // the tooltip lives on that line and two answers in one place is neither.
            if (_pillGainT[i] > 0f && _pillGainShow[i] > 0 && !rr.Contains(ChromeMouse))
            {
                var fade = Math.Min(1f, _pillGainT[i] / 0.3f);
                _ui.TextRight(_batch, "+" + Abbrev(_pillGainShow[i]), rr.Right, rr.Bottom + UiMetrics.Space(4),
                              pillInk[i] * fade);
            }
        }

        foreach (var (rr, name, v) in pillRows)
        {
            if (!rr.Contains(ChromeMouse)) continue;
            var text = v < 0 ? name : $"{name}  ·  {v.ToString("N0", inv)}";
            // The plate is its line plus a pad — 42 tall at 100 %, taller with the rung — hung under the capsules.
            var padX = UiMetrics.Space(14);
            var padY = UiMetrics.Space(10);
            var w = _ui.Measure(text) + padX * 2;
            var tip = new Rectangle(Math.Min(rr.Right, UiKit.PageRight(16)) - w, PillRowTop + PillHeight + UiMetrics.Space(8), w,
                                    UiTypography.Label + padY * 2);
            _ui.Fill(_batch, tip, new Color(0x0E, 0x0A, 0x14, 0xF0));
            _ui.Fill(_batch, new Rectangle(tip.X, tip.Y, tip.Width, 2), NavGold * 0.6f);
            _ui.TextRight(_batch, text, tip.Right - padX, tip.Y + padY, Bone);
            break;
        }
    }

    // ── The currency row's geometry. The capsules are UiKit.Pill's — a fixed 60 tall, icon 40, pads
    //    24 / 12 / 24 around the value — so the row is page geometry: it neither grows nor moves with
    //    the profile, only the value inside a capsule does.
    /// <summary>The capsules' top edge — level with the settings gear beside them.</summary>
    private const int PillRowTop = 16;
    /// <summary>A capsule's height (UiKit.Pill's).</summary>
    private const int PillHeight = 60;
    /// <summary>The gap between two capsules.</summary>
    private const int PillGap = 20;
    /// <summary>What UiKit.Pill wraps around a value: its left pad, the icon, the gap, the right pad.</summary>
    private const int PillCapsuleChrome = 24 + 40 + 12 + 24;
    /// <summary>The row's right end: a breath left of the settings gear, so the two can never share a pixel.</summary>
    private static int PillRowRight => SettingsGear.X - 16;
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
            var box = new Rectangle(640, 608 + i * 112, 640, 96);
            if (selected) _ui.Panel(_batch, box, gold: true); else _ui.PanelQuiet(_batch, box);
            // The ornate menu plates are DARK (even the gold/selected one), so both states take LIGHT text —
            // warm gold when selected, bone otherwise.
            // A 96 px menu plate carrying body-sized text read as a caption on a slab; these are the
            // three loudest choices in the game and they take the in-panel headline rung.
            // Centred on the plate by the rung's own optical middle (the house 27/40), so a 39 px label at
            // 150 % sits where the 26 px one sat at 100 %.
            _ui.TextCenterBig(_batch, items[i], box.Center.X, box.Center.Y - UiTypography.Headline * 27 / 40,
                selected ? new Color(0xF6, 0xD8, 0x88) : new Color(0xEC, 0xE6, 0xF2), UiTypography.Headline);

            // Clickable as well as keyed — every other menu in the game is.
            if (!_showSettings && UiKit.ClickedIn(box, ChromeMouse, _clicked)) ChooseTitleItem(i);
            else if (box.Contains(ChromeMouse)) _titleCursor = i;
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
        _mapScreen.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked);
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

    /// <summary>Act on the Map screen's ENTER / DEEPEN requests (set by keyboard in Update or buttons in Draw).</summary>
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
        // cycle (review, 2026-08-23). The trait point for the tier keys off the peak too (TraitPointsEarned).
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
        PushMapState();
        _mapScreen.Draw(_batch, PageCursor, MouseClicked);
        ConsumeMapRequests();
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
    private const int HelpLeft = 112, HelpTop = 92, HelpWidth = 1696, HelpDesignHeight = 840;

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
        var rowGap = UiMetrics.Space(10);                       // a row's breath under its last line: 38 px rows at 100 %
        var headerH = UiTypography.ModalTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(28);   // the title row → the columns
        var headToRows = pitch + UiMetrics.Space(26);           // a column's heading → its first row
        var keyLeft = UiMetrics.Control(180);                   // the key column: a count, or a key's name
        var keyRight = UiMetrics.Control(116);
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
        var leftH = ColumnHeight(buildLines) + UiMetrics.Space(48) + pitch + UiMetrics.Space(16)
                    + idea.Count * pitch + UiMetrics.Space(40) + next.Count * pitch;
        var worldLines = Wrap(world, rightW - keyRight);
        var contentH = Math.Max(leftH, ColumnHeight(worldLines));

        // THE PANEL: the design height at 100 %, taller when the profile asks, never past the page —
        // and past that the columns scroll under the title, the right one giving up a lane for the bar.
        var padBottom = UiTypography.PanelPadBottom;
        var needed = headerH + contentH + padBottom;
        var maxH = page.Height - UiMetrics.Space(12) * 2;
        var h = Math.Clamp(needed, HelpDesignHeight, maxH);
        var scrolls = needed > h;
        if (scrolls)
        {
            rightW -= UiMetrics.ScrollbarWidth + UiMetrics.Space(8);
            worldLines = Wrap(world, rightW - keyRight);
            contentH = Math.Max(leftH, ColumnHeight(worldLines));
        }
        var panel = new Rectangle(HelpLeft, h > HelpDesignHeight ? page.Y + (page.Height - h) / 2 : HelpTop, HelpWidth, h);
        var view = new Rectangle(panel.X + UiTypography.PanelPadX, panel.Y + headerH,
                                 panel.Width - UiTypography.PanelPadX * 2, panel.Bottom - padBottom - (panel.Y + headerH));
        var maxScroll = scrolls ? Math.Max(0, contentH - view.Height) : 0;
        if (maxScroll > 0 && MouseWheel != 0 && view.Contains(mouse)) _helpScroll -= MouseWheel * UiMetrics.RowHeight;
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
                if (rows[i].Key.Length > 0) Text(rows[i].Key, x, y, Gold);
                foreach (var line in lines[i]) { Text(line, x + keyW, y, Bone); y += pitch; }
                if (lines[i].Count == 0) y += pitch;
                y += rowGap;
            }
            return y;
        }

        var y = Column("YOUR BUILD", build, buildLines, leftX, keyLeft, top) + UiMetrics.Space(48);
        Text("THE IDEA:", leftX, y, Gold);
        y += pitch + UiMetrics.Space(16);
        foreach (var line in idea) { Text(line, leftX, y, Bone); y += pitch; }
        y += UiMetrics.Space(40);
        foreach (var line in next) { Text(line, leftX, y, Bone * 0.7f); y += pitch; }

        Column("SCREENS", world, worldLines, rightX, keyRight, top);

        if (scrolls)
        {
            EndChromeClip();
            var lane = UiMetrics.ScrollbarWidth;
            _ui.ScrollBar(_batch, new Rectangle(panel.Right - UiTypography.PanelPadX - lane, view.Y, lane, view.Height),
                          _helpScroll, view.Height, contentH);
        }

        // The house close icon, anchored in the corner above anything that scrolls. F1 and Esc close it too.
        if (_ui.CloseButton(_batch, UiKit.CloseRect(panel), mouse, _clicked)) _showHelp = false;
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
    private static readonly Color NavIdle = new(0x1A, 0x14, 0x30);
    private static readonly Color NavHover = new(0x2C, 0x25, 0x44);
    private static readonly Color NavLabel = new(0x8A, 0x82, 0xA0);

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
    /// Trait points: conquests, corruption tiers, and region-mastery goals.
    /// </summary>
    /// <remarks>
    /// NOT Memory Dust, which the Warren mints tick by tick while the game is closed. A permanent tree
    /// bought with an idle currency is a permanent tree bought by WAITING, and worse than the same
    /// failure in the skill tree, because these choices can never be taken back. Every term here is
    /// something that only happens because somebody descended.
    ///
    /// At full current content: 6 conquests + ~10 corruption tiers + 18 mastery goals = ~34 against a
    /// tree costing about 150 — roughly a quarter, and two of the four terminals out of reach.
    /// </remarks>
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
    private int TraitPointsEarned() => Career.TraitPointsEarned(_world);

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
    private bool NavUnlocked(int i)
        => i < 0 || i >= NavActivity.Length || Unlocks.IsOpen(NavActivity[i], GuideUnlockFacts());

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
            case 4: _showMastery = true; _masteryScreen.Disarm(); break;
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

    // ── THE TILE'S GRID (UI polish P2). The label sits on the tile's foot and the icon takes what is
    //    left above it, so the two cannot print through each other at any profile. Tile geometry — the
    //    tile is 1080/11 = 98 px and the rail 180 wide at every profile — so these are constants, not
    //    UiMetrics; what follows the profile is the label rung they are laid out around.
    /// <summary>How far the label's bottom clears the tile's foot.</summary>
    private const int NavLabelFoot = 10;
    /// <summary>Where the icon starts under the tile's top edge at 100 %. It is the first thing to give way when the label grows.</summary>
    private const int NavIconTop = 24;
    /// <summary>The breath between the icon and the label.</summary>
    private const int NavIconGap = 2;
    /// <summary>The icon's edge: never past 48 (its art), never under 30 (a glyph stops reading).</summary>
    private const int NavIconMax = 48, NavIconMin = 30;
    /// <summary>The room a label leaves at each side of the tile — the rail's 3 px seam and its mirror.</summary>
    private const int NavLabelInset = 3;

    private static Rectangle NavHexRect(int i)   // a rectangular TILE (guide: package_01 nav tiles)
    {
        // Vertical rail down the LEFT edge. It used to be eight 240px sections along the bottom
        // (0, 934, 1920, 146), which ate a full band of the stage and cut the actors off at the shins.
        // Stacked on the left it frees that height back to the arena.
        return new Rectangle(0, i * NavTileHeight, NavRailWidth, NavTileHeight);
    }

    private void DrawHexNav()
    {
        if (_showSettings || _showHelp) return;   // a modal owns the frame

        // A dark shelf so the bar seats cleanly over whatever screen sits behind it. Nearly opaque and
        // starting a hair above the hexes, so the scene behind can't show through and clip their tops.
        // 1920-space (scale-1 chrome): shelf top 234→936, full 1920 width, 36→144 tall.
        // Rev 4 §22: ONE shared QUIET rail — a dark background + thin dividers, NOT an ornate panel per item.
        // Only the active item gets ornate emphasis (ui_tab_active + purple tint, full-contrast icon/label);
        // inactive items are a quiet glyph + label at ~75% opacity.
        // Fully opaque. At 96% the rail let whatever a screen happened to draw underneath bleed through as
        // ghost shapes; chrome should never show the scene behind it.
        _ui.Fill(_batch, new Rectangle(0, 0, NavRailWidth, 1080), new Color(0x0C, 0x09, 0x16));
        // Seam runs down the rail's trailing edge now that the rail is vertical.
        _ui.Fill(_batch, new Rectangle(NavRailWidth - 3, 0, 3, 1080), NavGem * 0.4f);

        var active = NavActive();
        var navFacts = GuideUnlockFacts();
        var navGuide = GuideFacts();   // once per frame, not once per tile   // once per frame, not once per tile
        var navGems = GemsHeld();            // the first-gem lesson marks the FORGE tile the same way
        for (var i = 0; i < Nav.Length; i++)
        {
            var r = NavHexRect(i);
            var on = i == active;
            var hover = r.Contains(ChromeMouse);
            // A LOCKED TILE STILL DRAWS, dimmed. Hiding it would make the rail change length as the game
            // opens up, which moves every tile under the player's cursor and hides the shape of what is
            // still to come — the promise of the locked tile is half of why unlocking it lands.
            var unlocked = NavUnlocked(i);
            // Dividers are horizontal between stacked tiles, not vertical between side-by-side ones.
            if (i > 0) _ui.Fill(_batch, new Rectangle(r.X + 26, r.Y, r.Width - 52, 2), new Color(0x22, 0x1C, 0x30));

            if (on)
            {
                if (_assets.Get("ui_tab_active") is { } tab) _batch.Draw(tab, r, Color.White);
                else if (_assets.Get("ui_button_primary") is { } bp) _batch.Draw(bp, r, Color.White);
                else _ui.Fill(_batch, r, new Color(0x3A, 0x28, 0x54));
                _ui.Fill(_batch, r, new Color(0x8A, 0x5A, 0xC8) * 0.18f);   // purple interior tint
            }
            else if (hover)
            {
                _ui.Fill(_batch, r, new Color(0x8A, 0x5A, 0xC8) * 0.10f);
            }

            // Icon above, label below, both centred in the shorter tile.
            var iconTint = !unlocked ? Color.White * 0.22f : on ? Color.White : Color.White * 0.75f;
            // THE TILE'S LAYOUT IS DERIVED FROM THE LABEL RUNG, not written. The label (NavigationLabel:
            // 24 / 30 / 36 px) sits NavLabelFoot above the tile's foot and the icon takes the room between
            // NavIconTop and the label — 38 px at 100 % on the 98 px tile, pixel-identical to the old
            // Clamp(NavTileHeight - 60, 30, 48) at Y+24 — and it gives up its top pad before its floor,
            // so at 150 % a 36 px label gets a 30 px icon at Y+20 rather than an icon it prints through.
            // (Before this, the icon was sized from the tile alone and the label drawn at Bottom-34: at
            // 150 % the label ran two pixels past the tile's foot into the next tile's divider.)
            var labelH = UiTypography.NavigationLabel;
            var labelY = r.Bottom - NavLabelFoot - labelH;
            var iconPx = Math.Clamp(labelY - NavIconGap - (r.Y + NavIconTop), NavIconMin, NavIconMax);
            var iconY = labelY - NavIconGap - iconPx;
            if (_assets.Get(Nav[i].Glyph) is { } g)
                _batch.Draw(g, new Rectangle(r.Center.X - iconPx / 2, iconY, iconPx, iconPx), iconTint);
            else
                _ui.Diamond(_batch, new Rectangle(r.Center.X - iconPx / 2 + 4, iconY + 4, iconPx - 8, iconPx - 8), on ? NavGold : NavGem * 0.75f);
            // The label fits the tile or says so with an ellipsis — it is never shrunk; the rung is the rung.
            _ui.TextCenterBig(_batch, _ui.ShortenBig(Nav[i].Label, NavRailWidth - NavLabelInset * 2, labelH), r.Center.X, labelY,
                              !unlocked ? NavLabel * 0.35f : on ? NavGold : NavLabel * 0.9f, labelH);

            // The price, on the tile, so the rail teaches the progression without being clicked. Hover
            // only — nine requirement lines drawn permanently is the wall this pass exists to remove.
            // It hangs off the label's foot (Bottom-16 at 100 %), under the caps' ink, at every profile.
            if (!unlocked && hover)
                _ui.TextCenterBig(_batch, _ui.ShortenBig(Unlocks.Requirement(NavActivity[i]), NavRailWidth - UiMetrics.Space(24), UiTypography.Secondary),
                                  r.Center.X, labelY + labelH - UiMetrics.Space(6), NavGem * 0.8f, UiTypography.Secondary);


            // A GOLD "NEW" MARK on a tile that is open with something unread on it — the quiet
            // replacement for the modal panel that used to announce every opening. On the LEFT of
            // the tile, because the VAULT's red chest count owns the right, and both can be true of
            // the VAULT on the frame it opens. Never on the lit tile: the player is already there, and
            // the banner at the top of that screen is the mark's payload. A tile visited this session
            // drops its mark even with the banner still open — the mark means "you have not looked".
            var activity = NavActivity[i];
            // A rung ABOUT this tile's screen marks it for as long as the rung shows — visited or not; the
            // lesson is waiting on that screen, and the mark is how the rail says so (UX V2 P0.7).
            var rungSends = _guideStep is { } rung && Tutorial.HasGuidance(rung) && Tutorial.Sends(rung, navGuide) == activity;
            var isNew = !on && unlocked
                        && (((Onboarding.IsNew(activity, navFacts, _explained)
                              || (activity == Activity.Forge && Onboarding.GemTourDue(navGems, _explained) is not null))
                             && !_visited.Contains(activity))
                            || rungSends
                            || (activity == Activity.Roster && _rosterNews));
            if (isNew)
            {
                // A chip sized from its word: the caption rung plus a pad (50 × 26 at 100 %). It keeps to
                // the tile's top-left corner, clear of the icon beside it and the label under it — at
                // 150 % the corner is smaller than the chip would like, and the chip yields, not the label.
                var markH = UiTypography.Secondary + UiMetrics.Space(7);
                var markW = _ui.MeasureBig("NEW", UiTypography.Secondary) + UiMetrics.Space(12);
                var mark = new Rectangle(Math.Min(r.X + UiMetrics.Space(10), r.Center.X - iconPx / 2 - NavIconGap - markW),
                                         Math.Min(r.Y + UiMetrics.Space(14), labelY - NavIconGap - markH), markW, markH);
                _ui.Fill(_batch, mark, NavGold);
                _ui.Fill(_batch, new Rectangle(mark.X, mark.Y, mark.Width, 3), new Color(0xFF, 0xE0, 0xA0));
                _ui.TextCenterBig(_batch, "NEW", mark.Center.X, mark.Y + (markH - UiTypography.Secondary) / 2, new Color(0x2A, 0x1C, 0x08),
                                  UiTypography.Secondary);
            }

            // UNOPENED CHESTS, as a count on the VAULT tile — the page chests actually live on.
            //
            // It was on FORGE, correctly, when the Forge was the only place a chest existed: they sat
            // three clicks deep (FORGE, then SALVAGE mode, then a toolbar row) with nothing anywhere
            // saying one was waiting, and the most valuable thing the game gives you should not be the
            // hardest to find. The Vault took that job, and a badge pointing at the old address is a
            // signpost to the wrong door — worse than none, because the player follows it.
            if (Nav[i].Label == "VAULT" && _forge is not null && _forge.UnopenedChests.Count > 0)
            {
                var n = _forge.UnopenedChests.Count;
                var count = n > 9 ? "9+" : n.ToString();
                // A badge sized from its count: the caption rung plus a pad (38 × 30 at 100 %). Top-right,
                // clear of the icon and the label the same way the NEW mark is — at 150 % it sat across
                // the label's last letter.
                var badgeH = UiTypography.Secondary + UiMetrics.Space(11);
                var badgeW = Math.Max(UiMetrics.Control(38), _ui.MeasureBig(count, UiTypography.Secondary) + UiMetrics.Space(16));
                var badge = new Rectangle(Math.Max(r.Right - UiMetrics.Space(16) - badgeW, r.Center.X + iconPx / 2 + NavIconGap),
                                          Math.Min(r.Y + UiMetrics.Space(16), labelY - NavIconGap - badgeH), badgeW, badgeH);
                _ui.Fill(_batch, badge, new Color(0xC8, 0x3A, 0x3A));
                _ui.Fill(_batch, new Rectangle(badge.X, badge.Y, badge.Width, 3), new Color(0xF0, 0x8A, 0x6A));
                _ui.TextCenterBig(_batch, count, badge.Center.X, badge.Y + (badgeH - UiTypography.Secondary) / 2,
                                  Color.White, UiTypography.Secondary);
            }
        }
    }

    private void HandleNavClick()
    {
        if (!MouseClicked || _showSettings || _showHelp) return;
        // NavHexRect is 1920-space chrome now, so hit-test the 1920-space cursor.
        for (var i = 0; i < Nav.Length; i++)
            if (NavHexRect(i).Contains(ChromeMouse)) { OpenNav(i); return; }
    }
}
