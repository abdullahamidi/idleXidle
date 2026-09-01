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
using IdleXIdle.Core.Prestige;
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
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);

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

    /// <summary>Notices waiting their turn — "QUEST COMPLETE", "X JOINS YOU" — shown one at a time.</summary>
    private readonly Queue<string> _noticeQueue = new();
    private string _notice = "";
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
    private PlayerLoadout _loadout = PlayerLoadout.Starter();
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
    private MemoryDustTree _dust = new();

    /// <summary>
    /// What each skill has earned by being used. Owned by the game, not the expedition.
    /// </summary>
    /// <remarks>
    /// An expedition is minted per run and a skill's levels outlive every run, so this lives here and
    /// is handed to each expedition. That is the rule that makes unequipping a skill cost nothing.
    /// </remarks>
    private SkillProgress _skillProgress = new();
    private int _highestMasteryAwarded;





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

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = CanvasWidth * 3,
            PreferredBackBufferHeight = CanvasHeight * 3,
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
            ApplyDisplay();
        }
        else
        {
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
        _loadout.SkillCapacity = Math.Max(save.WovenSkills.Count, DustEffects.SkillSlots(_dust));
        _loadout.KeystoneCapacity = DustEffects.KeystoneSockets(_dust);
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
        for (var i = 0; i < _loadout.Skills.Count; i++)
            if (_loadout.Skills[i].VowId is { } vid && !DustEffects.KnowsVow(_dust, vid))
                _loadout.SetVow(i, null, DustEffects.KnownVows(_dust));
        _skillProgress.Restore(save.SkillProgress.Select(
            r => (r.SkillId, r.Uses, r.Variation, (IReadOnlyList<string>)r.Reinforcements)));

        _deepestEver = save.MasteryEarned;         // stored the deepest-ever; Earned re-derives from it
        _mastery.RestoreTaken(save.MasteryTaken);
        _mastery.RestoreLearned(save.LearnedSkills);   // D7 — discoveries survive every respec
        // The tree's camera. PARKED like the run log below: _masteryScreen is built in LoadContent. A
        // save from before the camera existed carries zoom 0, which the screen answers with its
        // first-open framing — the same first sight of the tree a new game gets.
        _pendingTreeCamera = (save.MasteryZoom, save.MasteryPanX, save.MasteryPanY);

        // The Warren facility economy — levels/XP restored before the offline tick below so its production
        // is computed against the real facility levels, not a fresh level-1 base.
        SaveSystem.RestoreWarren(save, _warren);
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
        if (credited > 0)
        {
            var (obh, obd) = EnemyBaselineFor(_activeRegion);
            var offline = OfflineHunt.Simulate(
                _loadout.ToBuild(_dust, _mastery, _characters.Active, _skillProgress),
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
        if (credited > 1.0 && (champOffline > 0 || credited >= 60))
        {
            var hours = credited / 3600.0;
            var span = hours >= 1.0 ? $"{hours:0.0} HOURS" : $"{credited / 60.0:0} MIN";
            // wOffline.Gleam WAS MISSING FROM THIS SUM. It is credited to the balance forty lines
            // above and was then left out of the only report of it, so the largest single payment
            // in the game was invisible to the player receiving it. Attribution matters more than
            // the total: it is the difference between "the game gave me money" and knowing WHICH of
            // your two economies is paying you.
            _bootMessage = $"WELCOME BACK — {span} AWAY\n"
                           + $"+{Abbrev(champOffline + (long)wOffline.Gleam)} GLEAM "
                           + $"({Abbrev(wOffline.Gleam)} WARREN · {Abbrev(champOffline)} HUNT)";
            _bootColor = Gold;
        }
        else if (champOffline > 0)
        {
            // A short trip that the farm ignores can still have earned the champion something.
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
        _bootMessage = "YOUR CHAMPION IS ALREADY FIGHTING\nWATCH THE FIRST WAVES — SCREENS OPEN AS YOU PLAY";
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
            // D7: the UNION (latch + currently-taken roads), so a pre-D7 save latches everything
            // its roads ever taught the first time it saves under this build.
            LearnedSkills = _mastery.LearnedSkills().OrderBy(s => s, StringComparer.Ordinal).ToList(),
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
        _loadout = PlayerLoadout.Starter();
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
        _notice = "";
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
        _warren.Name = Regions.Find(_activeRegion)?.Name ?? "THE WARREN";
        _warren.ConqueredRegions = _world.ConqueredIds.Count;
        _warrenScreen.Warren = _warren;

        // One facility level per five waves of proven depth. Recomputed every frame it draws, so a
        // record set this session raises the ceiling without a restart. The arithmetic (and its floor
        // of 1) lives on the model, where a test pins it.
        _warren.FacilityLevelCap = Warren.CapForDepth(DeepestAnywhere());

        _warrenScreen.GleamOwned = _hunter.Gleam;
        _warrenScreen.DustOwned = _dust.MemoryDust;
        _warrenScreen.Draw(_batch, CanvasMouse, MouseClicked);

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
        _roster = new RosterScreen(_ui);
        _vault = new VaultScreen(_ui);
        _loadoutScreen = new LoadoutScreen(_ui);
        _loadoutScreen.Sound = _sound;   // the weave's pick, and the seal a bound Vow presses
        _expedition = new HuntScreen(_ui);
        _expedition.Sound = _sound;   // the fight's hits, casts, deaths and the boss horn
        _masteryScreen = new MasteryScreen(_ui);
        _gear = new GearScreen(_ui, _forge);
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
        _expedition.KeepMinTier = _chestKeepMinTier;
        _expedition.KeepSlots.Clear(); foreach (var sl in _chestKeepSlots) _expedition.KeepSlots.Add(sl);
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
                or "region2" or "region3" or "conquered" or "help" or "expedition" or "fight" or "boss" or "bossdebug"
                or "banked" or "lootforge" or "settings" or "settingsfull" or "settingsopen" or "vow" or "runlog" or "reforge" or "build" or "buildtree" or "buildzoom" or "character" or "itemmenu" or "stats" or "warren" or "map" or "rig" or "corrupted" or "corruptedboss"
                or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "fightfilter" or "fightaura" or "fightflash" or "traitlit" or "traitterm" or "traitterminal"
                or "roster" or "rosterlocked" or "weave" or "vault" or "vaultfirst" or "attune" or "attuned" or "trader"
                or "gemtour" or "intro")
            {
                _showTitle = false;
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
                    TellForgeTheBuild(_loadout.ToBuild(_dust, _mastery, _characters.Active, _skillProgress));
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
                    // Depth per region, not a point total: SkillPointsEarned reads BestDepth/5 per
                    // region every frame and would otherwise stamp this back down to the base 3. Three
                    // regions at depth 35 is 3 + 21 = 24, the number this fixture always meant.
                    foreach (var def in Regions.All.Take(3)) _world.RegionFarm(def.Id).RestoreBestDepth(35);
                    _mastery.SetEarned(24);

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

                if (sm == "stats")
                {
                    _showTraining = true;
                    _training.Loadout = _loadout;
                    _training.Mastery = _mastery;
                    _training.Tree = _dust;
                    _training.Character = _characters.Active;
                    _training.SkillLevels = _skillProgress;
                    _hunter.AddGleam(20000);
                    for (var i = 0; i < 12; i++) _hunter.Train(HunterStat.AttackPower);
                    for (var i = 0; i < 6; i++) _hunter.Train(HunterStat.CriticalChance);
                    for (var i = 0; i < 5; i++) _hunter.Train(HunterStat.Defense);
                    for (var i = 0; i < 8; i++) _hunter.Train(HunterStat.Vitality);
                    _deepestEver = 23; _mastery.SetEarned(77400);   // fixture career values (Stats §13)
                }

                if (sm is "fight" or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "fightfilter" or "fightaura" or "fightflash" or "runlog")
                {
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
                    if (sm is "fight" or "fightswing" or "fightflash")
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
                    if (sm is "fightgear" or "fightswing" or "fightreport" or "fightfall" or "runlog")
                    {
                        var worn = new[]
                        {
                            ItemBaseType.Weapon, ItemBaseType.Helm, ItemBaseType.Chest,
                            ItemBaseType.Gloves, ItemBaseType.Boots,
                        };
                        var wrar = new[] { Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Epic, Rarity.Rare };
                        for (var i = 0; i < worn.Length; i++)
                            _hunter.Equip(new ItemInstance
                            {
                                InstanceId = $"fg{i}", BaseType = worn[i], Rarity = wrar[i],
                                SellValue = 40 + i * 20, Element = Source.Nature, ItemLevel = 30 + i * 5,
                            });
                    }
                    _expedition.Loadout = _loadout;
                    _expedition.Tree = _dust;
                    _expedition.Mastery = _mastery;
                    // Pose the new fight-screen UX: the welcome-back toast, and the "you have things to do"
                    // attention cue (a waiting chest + unspent mastery points).
                    // Not for fightreport: the welcome-back toast outranks the HunterDown overlay in the
                    // arena's priority list, so it would hide the very screen that capture exists to show.
                    if (sm is not ("fightreport" or "fightfall" or "runlog"))
                    {
                        _bootMessage = "WELCOME BACK — 18 MIN AWAY\n+140 GLEAM (90 WARREN · 50 HUNT)";
                        _bootColor = Gold; _bootTimer = 7f;
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
                        _expedition.DevRunToDeath(_hunter);
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
                    else _expedition.DevStart(_hunter, 1400f, 9f);
                    // `fight` with RH_SHOT_T: seconds into the wave to pose (capture.sh's third argument),
                    // so the bars can be photographed after the fight has moved them.
                    if (sm == "fight" && float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var seekS))
                        _expedition.DevSeek(seekS);
                    // fightfilter: the CHEST FILTER popover open, with a setting in it — so the row's
                    // summary, the lit medallions and the tier line can all be checked in one frame.
                    if (sm == "fightfilter")
                    {
                        _chestKeepMinTier = 3; _chestKeepSlots.Add(ItemBaseType.Helm); _chestKeepSlots.Add(ItemBaseType.Boots);
                        _expedition.KeepMinTier = 3; _expedition.KeepSlots.Add(ItemBaseType.Helm); _expedition.KeepSlots.Add(ItemBaseType.Boots);
                        _expedition.FilterOpen = true;
                    }
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
                    foreach (var r in Regions.All) _world.Conquer(r.Id);
                    _conquerMsg = "THE WORLD IS YOURS.  GO DEEPER INTO THE CORRUPTION ON THE MAP FOR MORE.";
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
                    // Show a mid-progression map: home conquered, Cinderworks unlocked.
                    _world.Conquer(VerdantHollow.RegionId);
                    _conquerMsg = "VERDANT HOLLOW CONQUERED!  CINDERWORKS UNLOCKED — OPEN THE MAP (W).";
                    _showWorld = true;
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
                if (sm is "forge" or "reforge" or "gemtour")
                {
                    _showForge = true;
                    if (sm == "gemtour")
                    {
                        var gemRng = new Random(11);
                        _forge.AddLoot(new[] { GemCraft.MintGem(4, gemRng), GemCraft.MintGem(7, gemRng) });
                    }
                    // Seed a spread of loot so SALVAGE has content, and a hero item the UPGRADE view poses.
                    var rar = new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
                    var types = new[] { ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.Material, ItemBaseType.AbilityFocus };
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
                    _forge.AddLoot(loot);
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
                if (sm == "warren")
                {
                    _showWarren = true;
                    _activeRegion = "pale_choir";   // so the Warren name reads "THE PALE CHOIR", per the reference
                    foreach (var id in new[] { "verdant_hollow", "cinderworks", "umbral_reach", "marrow_wastes" })
                        _world.Conquer(id);   // 4 conquered → the CONQUEST production bonus reads +40%
                    // Seed the reference's exact facility state (spec §6-9): level 23, the listed facility levels.
                    _warren.Restore(23, 18_540, new Dictionary<FacilityKind, int>
                    {
                        [FacilityKind.Nursery] = 18, [FacilityKind.Tunnels] = 17, [FacilityKind.ForagingPits] = 16,
                        [FacilityKind.ScavengerRuns] = 15, [FacilityKind.BreedingChamber] = 16, [FacilityKind.RitualNest] = 14,
                        [FacilityKind.HoardVaults] = 13, [FacilityKind.SentryBurrows] = 12,
                    });
                    // Owned balances at the reference's scale so the upgrade requirements read as MET.
                    _hunter.AddGleam(131_900_000);
                    _dust.AddDust(12_600);
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

                if (sm == "vault")
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

                if (sm == "weave")
                {
                    _showLoadout = true;
                    _dust.SetEarned(22);
                    // weave_5 is in the fixture ON PURPOSE: the fifth slot is the one that was bought
                    // and silently discarded for the whole of development, and a capture that poses four
                    // slots is a capture that would not have caught it.
                    // Four keystones LEARNED against three visible chips, so the shot poses the case the
                    // list was silently unable to show: a player who walked a road past its third node
                    // could never see the fourth, let alone choose it over the first three.
                    _world.RestoreConquered(Regions.All.Select(r => r.Id));
                    _world.RestoreCorruption(16);
                    // The head of all FOUR roads, which is what a spread-out 22-point career looks like —
                    // and four learned keystones against three visible chips, which is the case the list
                    // was silently unable to show.
                    foreach (var id in new[] { "socket_2", "weave_5", "vow_study_1", "ledger",
                                               "ks_glass_cannon", "ks_ironclad", "ks_echo", "ks_greed" })
                        _dust.Purchase(id);
                    // THE ROADS THIS FIXTURE'S BUILD NEEDS. Every skill is learned on the mastery tree
                    // now, so a fixture that walks none of them photographs four empty slots — true,
                    // and useless as a picture of the screen.
                    // RestoreTaken, not Take: a road needs its specialisation and a specialisation is
                    // one per hunter, so walking them honestly would take a career. The fixture is
                    // posing a champion who HAS walked them.
                    _mastery.SetEarned(9999);
                    _mastery.RestoreTaken(_mastery.Taken
                        .Concat(MasteryCatalog.Nodes.Where(x => x.Kind == MasteryKind.SkillRoad).Select(x => x.Id))
                        .ToList());
                    ApplySkillCapacity();
                    // The gradual-unlock gate would hold a fresh fixture at two slots, and two slots
                    // is one active and one passive — not enough to show a build.
                    _loadout.SkillCapacity = Math.Max(_loadout.SkillCapacity, 4);

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
                        _roster.DevSelect("quiver");
                    }
                }

                if (sm is "dust" or "traitlit" or "traitterm" or "traitterminal")
                {
                    _showTraits = true;
                    _dust.AddDust(77_605);   // Dust still shows in the top pills; it no longer buys traits

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
                    _hunter.AddGleam(131_900_000);     // the top currency pills read like the reference
                    _hunter.AddMaterials(12_600);

                    // The tree is a free camera now. `capture.sh dust out.png 1.2` zooms it to 1.2 on
                    // the posed node, so "zooming in reveals detail" is a captured fact rather than a
                    // claim. Only the plain dust mode reads it — traitlit's third argument is a time.
                    if (sm == "dust"
                        && float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_ZOOM"),
                                          System.Globalization.NumberStyles.Float,
                                          System.Globalization.CultureInfo.InvariantCulture, out var devZoom))
                        _traits.DevCamera(devZoom, "ks_bloodlust");

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
        if (!_tourActive && !_showHelp && !_showSettings && !_forge.RevealActive && !_expedition.LogOpen
            && Onboarding.TourDue(ScreenActivity(), _explained) is not null)
            BeginTour(ScreenActivity());

        // THE FIRST GEM. Its lesson is a second, smaller tour of the FORGE, owed from the moment a gem
        // is held (derived — Onboarding.GemTourDue) and given the first time the Forge is on top after
        // that, once its own tour is done. It arrives with the SOCKET tab open, so the light falls on
        // the tab the card names and the bag beside it lists the gem.
        var gemsHeld = GemsHeld();
        if (!_tourActive && !_showHelp && !_showSettings && !_forge.RevealActive && !_expedition.LogOpen
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
            else if (_showVault && _vault.ModalUp) _vault.CloseModals();
            // The hunt's CHEST FILTER popover is the same kind of thing: Esc on it means "close it".
            else if (!OverlayActive && !_expedition.LogOpen && _expedition.FilterOpen) _expedition.FilterOpen = false;
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
            _noticeTimer = NoticeSeconds;
            _sound.PlayFirst(0.9f, "sfx_levelup", "sfx_click");
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
        if (DevKeysEnabled && Pressed(Keys.F7)) { _expedition.DevBossDebug = !_expedition.DevBossDebug; _gear.DevGearDebug = !_gear.DevGearDebug; _training.DevStatsDebug = !_training.DevStatsDebug; _masteryScreen.DevBuildDebug = !_masteryScreen.DevBuildDebug; _forge.DevForgeDebug = !_forge.DevForgeDebug; _warrenScreen.DevWarrenDebug = !_warrenScreen.DevWarrenDebug; _mapScreen.DevMapDebug = !_mapScreen.DevMapDebug; _traits.DevDustDebug = !_traits.DevDustDebug; }   // dev layout overlays
        if (Pressed(Keys.F1)) _showHelp = !_showHelp;
        if (Pressed(Keys.F10)) _showSettings = !_showSettings;

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
        // only spends the one click that lands on it (below, beside the guide strip's close).
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
            // ChromeMouse, not CanvasMouse: the reveal is drawn in BeginCanvas(1), true 1920x1080.
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

        // THE GUIDE STRIP CAN BE CLOSED (playtest: "messages stay forever until I do the thing").
        // Its small x dismisses THAT rung for good — remembered in the save — and the guide shows
        // the next lesson as if this one were completed. Display only: no underlying fact is
        // faked, so unlocks and gates are untouched. Handled here rather than in Draw so the
        // click is swallowed before any screen hit-tests it.
        if (!_showSettings && !_showHelp && !_swallowInput && _clicked
            && _guideStep is { } closable && Tutorial.HasGuidance(closable)
            && GuideCloseRect(GuideBannerRect(closable)).Contains(ChromeMouse))
        {
            _dismissedGuide.Add(closable.ToString());
            _guideStep = Tutorial.Showing(GuideFacts(), _dismissedGuide);
            _sound.Play("sfx_click", 0.6f);
            Save();
            _swallowInput = true;
        }

        // THE SLOT-NOTE BANNER at the top of the BUILD screen closes the same way, and remembers itself
        // in the save. A click anywhere else on the banner is spent too — it sits over the screen's own
        // controls, and a click that closed nothing must not press a TRAIN button underneath.
        if (!_showSettings && !_showHelp && !_swallowInput && _clicked
            && ScreenBannerShowing() is { } banner)
        {
            var rect = ScreenBannerRect(banner);
            if (GuideCloseRect(rect).Contains(ChromeMouse))
            {
                _explained.Add(banner.Key);
                _sound.Play("sfx_click", 0.6f);
                Save();
                _swallowInput = true;
            }
            else if (rect.Contains(ChromeMouse)) _swallowInput = true;
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
                _sound.Play("sfx_click", 0.45f);
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
            _masteryScreen.Update(ScreenKeys, _prevKeys, CanvasMouse, MouseClicked,
                                _mouse.LeftButton == ButtonState.Pressed, MouseWheel, _dust, MouseRightClicked);
            if (_masteryScreen.Dirty) { _masteryScreen.ClearDirty(); Save(); }

            Latch(gameTime);
            return;
        }

        if (_showGear)
        {
            _gear.Loadout = _loadout;
            _gear.Mastery = _mastery;
            _gear.Tree = _dust;
            _gear.Update(ScreenKeys, _prevKeys, CanvasMouse, MouseClicked, MouseRightClicked, MouseWheel, _hunter);

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
                    _sound.PlayFirst(1f, "sfx_forge", "sfx_click");
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
            _vault.TraderStock = _traderStock;
            _vault.TraderBought = _traderBought;

            _vault.Update(dt, _forge.UnopenedChests, CanvasMouse, MouseClicked, MouseWheel);

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
                        _sound.Play("sfx_forge", 0.9f);
                        Save();
                    }
                    break;
                }
                case VaultScreen.OpenRequest.All:
                    _forge.OpenEveryChest(_hunter);
                    _sound.Play("sfx_forge", 0.9f);
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
            _training.Character = _characters.Active;
            _training.HighestWave = _deepestEver;          // real career counters (Stats spec §9.2)
            _training.ChestsOpened = _forge.ChestsOpened;
            _training.MasteryPoints = _mastery.Earned;
            _training.Update(ScreenKeys, _prevKeys, CanvasMouse, MouseClicked, MouseWheel, _hunter);
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
            _loadoutScreen.SkillLevels = _skillProgress;
            _loadoutScreen.Hunter = _hunter;
            _loadoutScreen.Character = _characters.Active;
            _loadoutScreen.RegionId = _activeRegion;
            _loadoutScreen.RegionName = Regions.Get(_activeRegion).Name;
            _loadoutScreen.ChosenStyle = _mastery.Affinity();
            _loadoutScreen.MasteryTaken = _mastery.Taken;
            _loadoutScreen.Update(CanvasMouse, MouseClicked, _mouse.LeftButton == ButtonState.Pressed, MouseWheel);
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
            _roster.Update(CanvasMouse, MouseClicked, _characters);
            Latch(gameTime);
            return;
        }

        if (_showTraits)
        {
            // HELD, as well as clicked: the tree is a free canvas now, and a held button drags it.
            _traits.Update(ScreenKeys, CanvasMouse, MouseClicked, _mouse.LeftButton == ButtonState.Pressed,
                             MouseWheel, _dust, dt);
            // Taking a trait is permanent and there is no respec, so it is worth a sound and worth
            // writing to disk immediately. The screen owns neither: it hands back a cue the same way
            // TrainingScreen hands back a trained stat.
            if (_traits.ConsumeCue() is { } cue)
            {
                _sound.PlayFirst(1f, cue, "sfx_conquer", "sfx_levelup", "sfx_click");
                Save();
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
            TellForgeTheBuild(_loadout.ToBuild(_dust, _mastery, _characters.Active, _skillProgress));
            // The keyboard is LOCKED while any host modal owns the frame — without this, the S that
            // dismissed an unlock panel also SOLD the focused (rarest-first!) bag item behind it, and
            // S/D/J kept working under the settings panel and the reveal. MouseClicked already carries
            // these gates; the keys did not. (Adversarial review, pass four.)
            _forge.Update(gameTime, ScreenKeys, CanvasMouse, MouseClicked, MouseWheel, _hunter,
                          inputLocked: _swallowInput || _showSettings || _forge.RevealActive);
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
    /// Compared against <see cref="PlayerLoadout.Starter"/>'s single Body Strike rather than a stored
    /// flag, so it needs no save migration and is honest on every existing save.
    /// </remarks>
    private bool BuildDiffersFromStarter()
    {
        var skills = _loadout.Skills;
        if (skills.Count != 1) return true;
        return skills[0].SkillId != "hammer_blow" || skills[0].Source != Source.Body || skills[0].VowId is not null;
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
        var gate = Unlocks.SkillSlots(GuideUnlockFacts());
        var fromTree = DustEffects.SkillSlots(_dust);
        var capacity = gate >= Build.SkillSlots ? fromTree : gate;
        _loadout.SkillCapacity = Math.Max(capacity, _loadout.Skills.Count);
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
    private void PostNotice(string head, string detail) => _noticeQueue.Enqueue(head + "\n" + detail);

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


    /// <summary>
    /// The first-run guide, drawn as shared chrome over every screen in the game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS USED TO LIVE INSIDE THE ARENA, and that was the reported bug.</b> DrawGuide was called
    /// from HuntScreen.DrawArena, which is reached only through the terminal <c>else</c> of
    /// the screen chain — so the guide appeared on HUNT and nowhere else. Every step from SpendGleam
    /// onward names a key that navigates away from the hunt, so the player read "press V for STATS",
    /// pressed V, and the instruction vanished. Nothing confirmed the step had completed; nothing
    /// carried the lesson onto the screen it had just sent them to. Playtest: "Tutorial bozuk, düzgün
    /// ilerlemiyor."
    /// </para>
    /// <para>
    /// As chrome it follows the player. Press V and the guide is still there on the STATS screen saying
    /// what to do; train a stat and it visibly advances to the next rung. That transition IS the sense
    /// of progress the guide was missing.
    /// </para>
    /// <para>
    /// Suppressed while a tour is up, so two pieces of teaching never compete for the same
    /// attention — the intro's last card points at the place this strip will appear.
    /// </para>
    /// </remarks>
    private void DrawGuideBanner()
    {
        if (_showTitle || _showHelp || _showSettings) return;
        if (_tourActive) return;
        if (_guideStep is not { } step || !Tutorial.HasGuidance(step)) return;
        DrawGuideStrip(step);
    }

    /// <summary>One guide rung as a strip — the live one, or the intro's preview of what a lesson looks like.</summary>
    private void DrawGuideStrip(TutorialStep step)
    {
        var r = GuideBannerRect(step);
        // The facts-aware body: the chest rung reads differently while a chest is actually waiting.
        var body = _ui.WrapBig(Tutorial.Body(step, GuideFacts()), r.Width - 44, UiTypography.Secondary);

        _ui.Fill(_batch, r, new Color(0x10, 0x0D, 0x18, 0xEE));
        _ui.Fill(_batch, new Rectangle(r.X, r.Y, 5, r.Height), NavGold);

        _ui.TextBig(_batch, Tutorial.Title(step), r.X + 22, r.Y + 14, NavGold, UiTypography.Body);
        var ty = r.Y + 44;
        foreach (var line in body)
        {
            _ui.TextBig(_batch, line, r.X + 22, ty, UiKit.Vellum, UiTypography.Secondary);
            ty += 22;
        }

        // The x that closes THIS lesson for good — see the dismissal handler in Update.
        var close = GuideCloseRect(r);
        var hover = close.Contains(ChromeMouse);
        _ui.CloseButton(_batch, close, ChromeMouse, false);   // drawn here; the click is handled in Update
    }

    /// <summary>Where the guide strip sits this frame — its height follows the wrapped body.</summary>
    private Rectangle GuideBannerRect(TutorialStep step)
    {
        const int width = 980;
        var body = _ui.WrapBig(Tutorial.Body(step, GuideFacts()), width - 44, UiTypography.Secondary);
        var height = 58 + body.Count * 22;
        return new Rectangle((1920 - width) / 2, 1080 - height - 26, width, height);
    }

    /// <summary>The guide strip's small close button, top-right of the banner.</summary>
    private static Rectangle GuideCloseRect(Rectangle banner) => new(banner.Right - 42, banner.Y + 10, 32, 32);

    /// <summary>
    /// Take off every worn piece the new champion's class cannot wear, tell the player where it went,
    /// and save — the switch and the shed must land in the same file.
    /// </summary>
    /// <remarks>
    /// The pieces are never removed from the bag: a worn item is a bag item the doll points at, so
    /// <c>Unequip</c> alone puts it back in the grid, dimmed and locked, where the hover card names
    /// who can wear it. The toast rides the same channel as a locked rail tile — one line, top of the
    /// screen, gone in a few seconds — because it is a notice, not a lesson.
    /// </remarks>
    private void ShedUnwearable()
    {
        var who = _characters.Active;
        var shed = new List<string>();
        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            if (_hunter.Worn(slot) is not { } worn || Gear.CanWear(who, worn)) continue;
            _hunter.Unequip(slot);
            shed.Add(ItemNaming.TypeWord(worn));
        }
        if (shed.Count == 0) return;

        var words = shed.Count == 1 ? shed[0] : string.Join(", ", shed.Take(shed.Count - 1)) + " AND " + shed[^1];
        _lockedMsg = shed.Count == 1
            ? $"{who.Name} CANNOT WEAR YOUR {words} — IT IS BACK IN YOUR BAG"
            : $"{who.Name} CANNOT WEAR YOUR {words} — THEY ARE BACK IN YOUR BAG";
        _lockedTimer = 4.5f;
        Save();
    }

    /// <summary>The toast for clicking a locked rail tile. Says the price, then fades.</summary>
    private void DrawLockedToast()
    {
        if (_lockedTimer <= 0f || _lockedMsg.Length == 0) return;

        var fade = MathF.Min(1f, _lockedTimer / 0.5f);
        var w = 900;
        var box = new Rectangle((1920 - w) / 2, 96, w, 62);
        _ui.Fill(_batch, box, new Color(0x18, 0x10, 0x24) * (0.92f * fade));
        _ui.Fill(_batch, new Rectangle(box.X, box.Y, box.Width, 3), NavGem * fade);
        _ui.TextCenterBig(_batch, _ui.ShortenBig(_lockedMsg, w - 40, UiTypography.OverlayBody), box.Center.X, box.Y + 18,
                          Color.White * fade, UiTypography.OverlayBody);
    }

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
    /// the length of the guide strip's and centres the banner under the screen's title.
    /// </remarks>
    private const int ScreenBannerWidth = 1200;

    /// <summary>The width the banner wraps its body to — inside the left rule and clear of the ×.</summary>
    private const int ScreenBannerWrap = ScreenBannerWidth - 44 - 40;

    /// <summary>
    /// Where the banner sits: under the screen's title band, centred over the content.
    /// </summary>
    /// <remarks>
    /// y = 86 is just under the currency capsules, so the banner covers neither them nor the settings
    /// gear — both are live chrome, and a banner over a button is a button the player cannot press.
    /// It does cover the top of the screen's own content, which is the point: it is that screen's
    /// explanation, and it is closed with one click.
    /// </remarks>
    private Rectangle ScreenBannerRect(ScreenBanner banner)
    {
        var body = _ui.WrapBig(banner.Body, ScreenBannerWrap, UiTypography.Secondary);
        var x = NavRailWidth + (1920 - NavRailWidth - ScreenBannerWidth) / 2;
        return new Rectangle(x, 86, ScreenBannerWidth, 58 + body.Count * 22);
    }

    /// <summary>
    /// The note owed at the top of the screen on top — the BUILD screen's new-slot line — in the guide
    /// strip's visual language, and closed the same way.
    /// </summary>
    /// <remarks>
    /// Not a timed toast, for the reason the old panel was not one: these are paragraphs, and a
    /// paragraph on a timer is a paragraph nobody finishes. Not modal either, for the reason the old
    /// panel was removed: it is on the screen it is about, and the player came here on purpose.
    /// </remarks>
    private void DrawScreenBanner()
    {
        if (ScreenBannerShowing() is not { } banner) return;

        var r = ScreenBannerRect(banner);
        var body = _ui.WrapBig(banner.Body, ScreenBannerWrap, UiTypography.Secondary);

        _ui.Fill(_batch, r, new Color(0x10, 0x0D, 0x18, 0xEE));
        _ui.Fill(_batch, new Rectangle(r.X, r.Y, 5, r.Height), NavGold);

        _ui.TextBig(_batch, banner.Title, r.X + 22, r.Y + 14, NavGold, UiTypography.Body);
        var ty = r.Y + 44;
        foreach (var line in body)
        {
            _ui.TextBig(_batch, line, r.X + 22, ty, UiKit.Vellum, UiTypography.Secondary);
            ty += 22;
        }

        var close = GuideCloseRect(r);
        var hover = close.Contains(ChromeMouse);
        _ui.CloseButton(_batch, close, ChromeMouse, false);   // drawn here; the click is handled in Update
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
        if (_noticeTimer <= 0f || _notice.Length == 0) return;
        if (_showTitle || _showHelp || _showSettings) return;
        // Never over a tour: the first-gem notice sits exactly where the Forge's tab strip is, and a
        // toast across a spotlight is two lessons at once. The tour IS the notice's payload.
        if (_tourActive) return;

        var fade = Math.Clamp(_noticeTimer / 1.0f, 0f, 1f);
        var parts = _notice.Split('\n');
        var y = _bootTimer > 0f && _bootMessage.Length > 0 && !_tourActive ? ToastTop + ToastHeight + 8 : ToastTop;
        var r = new Rectangle(560, y, 800, 96);
        _ui.PanelQuiet(_batch, r, fade);   // a toast is not a modal — the quiet frame (UiKit.PanelQuiet)
        _ui.TextCenterBig(_batch, _ui.ShortenBig(parts[0], r.Width - 90, UiTypography.OverlayTitle),
                          r.Center.X, r.Y + 22, NavGold * fade, UiTypography.OverlayTitle);
        if (parts.Length > 1)
            _ui.TextCenterBig(_batch, _ui.ShortenBig(parts[1], r.Width - 90, UiTypography.OverlayBody),
                              r.Center.X, r.Y + 56, Bone * fade, UiTypography.OverlayBody);
    }

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
            Activity.Map => MapScreen.Spotlights(target),
            Activity.Traits => TraitsScreen.Spotlights(target),
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
        const int gap = 28;
        var a = holes[0];
        var candidates = new[]
        {
            new Point(a.Center.X - width / 2, a.Bottom + gap),
            new Point(a.Right + gap, a.Center.Y - height / 2),
            new Point(a.Left - gap - width, a.Center.Y - height / 2),
            new Point(a.Center.X - width / 2, a.Top - gap - height),
        };
        foreach (var p in candidates)
        {
            if (p.Y < 8 || p.Y + height > 1072) continue;
            var r = new Rectangle(Math.Clamp(p.X, NavRailWidth + 20, 1900 - width), p.Y, width, height);
            if (holes.Any(h => h.Intersects(r))) continue;
            return r;
        }
        return new Rectangle(1900 - width, 1072 - height, width, height);
    }

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
        // rung, the very strip that will be standing in that light when the intro ends: the light
        // lifts and nothing has moved. (The live strip itself is suppressed while the intro is up.)
        if (step.Target == TourTarget.GuideStrip) DrawGuideStrip(TutorialStep.Watch);

        DrawScrimAround(holes, new Color(0x05, 0x03, 0x0A) * 0.74f);
        foreach (var h in holes) TourOutline(h, 3, NavGold);

        const int width = 560, pad = 24;
        var lines = _ui.WrapBig(step.Body, width - pad * 2, UiTypography.Body);
        var height = pad + 32 + 8 + lines.Count * 28 + 14 + 24 + pad;
        var card = TourCardRect(holes, width, height);

        _ui.Fill(_batch, card, new Color(0x15, 0x0E, 0x24, 0xF6));
        _ui.Fill(_batch, new Rectangle(card.X, card.Y, card.Width, 4), NavGold);
        _ui.Fill(_batch, new Rectangle(card.X, card.Bottom - 2, card.Width, 2), NavGem * 0.5f);

        _ui.TextBig(_batch, step.Title, card.X + pad, card.Y + pad, NavGold, UiTypography.Headline);
        _ui.TextRightBig(_batch, $"{stepNo + 1} / {_tour.Count}", card.Right - pad, card.Y + pad + 5,
                         NavLabel, UiTypography.Secondary);

        var y = card.Y + pad + 32 + 8;
        foreach (var line in lines)
        {
            _ui.TextBig(_batch, line, card.X + pad, y, new Color(0xD8, 0xD2, 0xE4), UiTypography.Body);
            y += 28;
        }

        // The last card's footer says what the click does next: the intro's hands over to the first
        // wave; every other tour's hands the screen back.
        var last = stepNo + 1 >= _tour.Count;
        var footer = !last ? "CLICK TO CONTINUE  ·  ESC SKIPS"
                   : _tourScreen == Activity.Hunt ? "CLICK TO BEGIN" : "CLICK TO FINISH";
        _ui.TextBig(_batch, footer, card.X + pad, card.Bottom - pad - 20, NavGem, UiTypography.Secondary);
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
    private void UpdateExpedition(GameTime gameTime)
    {
        var def = Regions.Get(_activeRegion);
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
        // TAKE ONLY edits (made in the HUNT screen's Draw) come back on the dirty flag only — never a
        // per-frame push of the saved value, which clobbered the vault's edit in playtest five.
        if (_expedition.FilterDirty)
        {
            _expedition.FilterDirty = false;
            _chestKeepMinTier = _expedition.KeepMinTier;
            _chestKeepSlots.Clear(); foreach (var sl in _expedition.KeepSlots) _chestKeepSlots.Add(sl);
            Save();
        }
        if (_expedition.WantsMastery) { _expedition.WantsMastery = false; OpenNav(4); }   // MASTERY (E) — the tree where the points are spent

        // The first-run guide, or null once outgrown. Held on the HOST, not on the fight screen: it is
        // drawn as chrome over every screen now (DrawGuideBanner), because a guide that vanishes the
        // moment you obey it reads as a guide that has stopped working.
        _guideStep = Tutorial.Showing(GuideFacts(), _dismissedGuide);

        _forge.Tuning = ForgeTuning.Default with
        {
            DismantleReturnRate = DustEffects.DismantleRate(_dust, ForgeTuning.Default.DismantleReturnRate),
        };
        // A chest's rolled loot honours the same Dust filters a boss drop did — auto-sell floor and the
        // tireless-forge auto-merge — now applied at OPEN, since that is where a chest's items land.
        _forge.AutoSellFloor = DustEffects.AutoSellAtOrBelow(_dust);

        // The loot-quality tilt reaches the roll that opens a chest. Until this line, Rarity was resolved
        // from keystones, gear and the trait tree, carried as Haul.Quality, and read by nothing at all.
        // The build is made ONCE and used twice — the Forge's combo line needs the same object.
        var wornBuild = _loadout.ToBuild(_dust, _mastery, _characters.Active, _skillProgress);
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
            PostNotice($"{got.Name.ToUpperInvariant()} JOINS YOU", "SWITCH CHAMPION ON THE ROSTER SCREEN");
            _rosterNews = true;
        }
        _rosterBaselined = true;

        // A CHAMPION SWITCH SHEDS WHAT THE NEW ONE CANNOT WEAR. The roster's promise is that switching
        // costs nothing, and it still costs nothing — the pieces go back to the bag, not away — but a
        // WARDEN's helm on a RANGER would be a class rule the fight quietly ignored, and the wear
        // rule has to hold on the doll as well as at the bag.
        if (_lastActiveCharacterId is { } wasId && wasId != _characters.ActiveId) ShedUnwearable();
        _lastActiveCharacterId = _characters.ActiveId;
        // Four in five class-locked pieces a chest pays are the active champion's.
        _forge.FavouredClass = _characters.Active.Class;
        // BIRTH SKILLS ARE LEARNED BY BEING SOMEONE (P9). Becoming a champion latches theirs into
        // the permanent set (D7), so the roster's YOU KEEP SKILLS holds for the one skill a
        // champion brings, not only for the tree's. Idempotent, so per-frame is free.
        _mastery.LearnSkill(_characters.Active.StartingSkillId);

        _expedition.Character = _characters.Active;
        _gear.Character = _characters.Active;
        _masteryScreen.Character = _characters.Active;

        // The spine's capacity nodes reach the loadout. Without this the sockets and the fifth weave are
        // bought and never granted — the shape of the failure this codebase keeps repeating.
        _loadout.KeystoneCapacity = DustEffects.KeystoneSockets(_dust);
        ApplySkillCapacity();
        // (A slot the player just earned is not announced here, or anywhere: the BUILD tile derives its
        // NEW mark from the slot count and the explained list every frame — see Onboarding.IsNew.)
        _forge.AutoMergeOnOpen = DustEffects.AutoMergeAfterRuns(_dust);
        // Every region is a rung up the ladder for the champion, not just a new element — see
        // EnemyBaselineFor, which is also what the offline simulation fights against.
        var (ebh, ebd) = EnemyBaselineFor(_activeRegion);
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
            _conquerMsg = unlocked is not null
                ? $"{Regions.Get(_activeRegion).Name} CONQUERED!  {unlocked.Name} UNLOCKED — MAP (W)."
                : "THE WORLD IS YOURS.  GO DEEPER INTO THE CORRUPTION ON THE MAP (W).";
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
        var r = new Rectangle(630, ToastTop, 560, ToastHeight);
        // The quiet frame, like the stage header it sits under — gold is for modals (UiKit.PanelQuiet).
        _ui.PanelQuiet(_batch, r, fade);
        _ui.TextCenterBig(_batch, parts[0], r.Center.X, r.Y + 18, _bootColor * fade, UiTypography.OverlayTitle);
        if (parts.Length > 1) _ui.TextCenterBig(_batch, parts[1], r.Center.X, r.Y + 50, Bone * fade, UiTypography.OverlayBody);
    }

    private const int ToastHeight = 88;

    /// <summary>Where a toast hangs: under the hunt's header stack, or under a menu screen's title.</summary>
    private int ToastTop => OverlayActive ? 176 : _expedition.HeaderStackBottom + 8;

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

    /// <summary>The mouse position in 480×270 canvas space (the backbuffer is upscaled 3×).</summary>
    /// <summary>Screen → canvas, through the letterbox. Never divide by a bare scale again.</summary>
    /// <remarks>
    /// RH_SHOT_MOUSE=x,y forces the canvas cursor to a fixed point, so a screenshot run can pose a
    /// hover-only affordance (a tooltip, a hot row) that a real cursor would otherwise have to be over.
    /// </remarks>
    private Point CanvasMouse =>
        Environment.GetEnvironmentVariable("RH_SHOT_MOUSE") is { } sm
        && sm.Split(',') is [var sx, var sy]
        && int.TryParse(sx, out var mx) && int.TryParse(sy, out var my)
            ? new Point(mx, my)
            : Display.ToCanvas(new Point(_mouse.X, _mouse.Y), _present);

    /// <summary>
    /// The mouse in TRUE 1920×1080 space — the chrome's own coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shared chrome (currency pills, hex nav, title, settings modal) authors in 1920 coords at
    /// scale 1, so its hit-tests must compare against a 1920-space cursor. <see cref="CanvasMouse"/>
    /// stays 480-space for the screens that still author there.
    /// </para>
    /// <para>
    /// MAPPED AT FULL RESOLUTION rather than as <c>CanvasMouse × 4</c>, which is what it was: floor to
    /// 480-space and multiply back and the chrome cursor can only ever land on a multiple of 4, so
    /// every hit-test in the chrome was quantised to a 4-px grid it never asked for. Same letterbox
    /// arithmetic as Core's ToCanvas, at the scale this space actually uses — and the floor is toward
    /// negative infinity for the same reason it is there: a point one pixel LEFT of the canvas must map
    /// outside it, not onto its leftmost column.
    /// </para>
    /// <para>
    /// NO CLAMP ON THE SCALE. Core clamps at 1 because a 480-wide canvas is never presented smaller
    /// than 480; a 1920-wide one is routinely presented smaller — a 1280×720 window shows it at 0.667.
    /// </para>
    /// </remarks>
    private Point ChromeMouse
    {
        get
        {
            // The posed cursor is authored in canvas coords, so it keeps the ×4 route it was written for.
            if (Environment.GetEnvironmentVariable("RH_SHOT_MOUSE") is not null)
                return new Point(CanvasMouse.X * 4, CanvasMouse.Y * 4);
            var scale = _present.Width / (float)(CanvasWidth * ArtScale);
            if (scale <= 0f) return new Point(CanvasMouse.X * 4, CanvasMouse.Y * 4);
            return new Point((int)MathF.Floor((_mouse.X - _present.X) / scale),
                             (int)MathF.Floor((_mouse.Y - _present.Y) / scale));
        }
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
        => _present = Display.PresentFit(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

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

    private bool MouseClicked => _clicked && !_showSettings && !_showHelp && !_swallowInput;
    private bool MouseRightClicked => _rightClicked && !_showSettings;


    /// <summary>
    /// Open a SpriteBatch for the virtual canvas at the given counter-scale, syncing the drawing helpers so
    /// native-resolution assets and text land 1:1. <paramref name="scale"/> is 4 for 480×270 logical screens
    /// (the default) or 1 for screens authored directly in 1920×1080.
    /// </summary>
    private void BeginCanvas(int scale)
    {
        _ui.Scale = scale;
        _ui.Text2.Scale = scale;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null, Matrix.CreateScale(scale));
    }

    /// <summary>Begin the batch for a menu screen: authored 1920-wide, drawn inset right of the nav rail.</summary>
    private void BeginOverlayCanvas()
    {
        _ui.Scale = 1;
        _ui.Text2.Scale = 1;
        // The camera kick belongs to the screen that wants one. A global shake used to live in the
        // present blit, driven by manual combat, and sat at a permanent zero from the pivot onward
        // because nothing owned it; this asks the active screen instead, so the kick exists only while
        // something is actually asking for it.
        var kick = _showTraits ? _traits.Shake : Vector2.Zero;
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
        if (_showForge) _forge.Draw(_batch, _hunter, CanvasMouse, MouseClicked, MouseRightClicked);
        else if (_showWorld) DrawWorld();
        else if (_showTraits) _traits.Draw(_batch, _dust, CanvasMouse, MouseClicked);
        else if (_showRoster) { _roster.Progress = QuestSnapshot(); _roster.Draw(_batch, _characters, CanvasMouse, MouseClicked); }
        else if (_showVault) _vault.Draw(_batch, _forge.UnopenedChests, CanvasMouse, MouseClicked);
        else if (_showLoadout) _loadoutScreen.Draw(_batch, CanvasMouse, MouseClicked);
        else if (_showWarren) DrawWarren();
        else if (_showMastery) _masteryScreen.Draw(_batch, CanvasMouse, _dust);
        else if (_showGear) _gear.Draw(_batch, CanvasMouse, _hunter);
        else if (_showTraining)
        {
            _training.Draw(_batch, CanvasMouse, _hunter, MouseClicked);

            // Gleam is one of the three payouts a descent makes, and this is the layer it buys. The model
            // (geometric cost, rank cap) has always been here; until now nothing in the game called it.
            if (_training.ConsumeTrain() is { } stat && _hunter.Train(stat)) { _sound.Play("sfx_click", 0.8f); Save(); }
            if (_training.ConsumeReset() && _hunter.ResetTraining()) { _sound.Play("sfx_forge", 0.8f); Save(); }
        }
        else _expedition.Draw(_batch, CanvasMouse, MouseClicked, Regions.Get(_activeRegion).Name, EnemyArtFor(_activeRegion), _bootTimer > 0f);

        // The LOG draws over everything, including the nav rail: it is a full-screen read, and the one
        // overlay a player opens to think rather than to act.
        _expedition.DrawLog(_batch, CanvasMouse, MouseClicked);
        _batch.End();

        // Batch C — the shared overlays (pills, nav, help/settings, boot toast), authored in true 1920 coords.
        BeginCanvas(1);
        DrawCurrencyPills();   // shared Gleam / Dust / Materials row, top-right of every screen
        DrawSettingsGear();    // the corner gear — settings from any screen, including mid-hunt
        DrawHexNav();   // the shared nav bar, over every screen

        // The chest burst, over the rail and over whatever screen is open — it is the one moment the
        // game asks the player to stop and look, so nothing on the page should sit on top of it.
        if (_forge.RevealActive) _forge.DrawRevealOverlay(_batch, _hunter);

        if (_showHelp) DrawHelp();
        if (_showSettings) DrawSettings();

        DrawBootToast();
        DrawGuideBanner();
        DrawScreenBanner();
        DrawLockedToast();
        DrawNoticeToast();
        // LAST of the chrome, so a tour's scrim and spotlight sit over everything — including the
        // nav rail the intro has a card about.
        DrawTour();

        _batch.End();

        // DEV SCREENSHOT HOOK: set RH_SHOT=<path> to dump one upscaled frame after ~1s, then exit.
        // Used to verify rendering headlessly; harmless and inert without the env var.
        _shotFrame++;
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
        else if (shotPath is not null && _shotFrame == 60)
        {
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

        base.Draw(gameTime);
    }

    private int _shotFrame;

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
        _showDamageNumbers, _showSkillCallouts, _showHitEffects, _showScreenFlash));

    // ── Settings ──────────────────────────────────────────────────────────────────────────────
    // 1920-space (scale-1 chrome). 2026-08-24, playtest nine: MODE and WINDOW SIZE became dropdowns
    // and the volume rows became draggable sliders, which freed two button rows of height — the panel
    // shrank from 1250x960 to 1250x880 and moved down under the title. Aspect 1250/880 = 1.42, still
    // comfortably above UiKit.Panel's 1.30 medium-frame line, so the frame art is unchanged. The
    // content column stays at x 536..1384, and CLOSE / QUIT now end 44px above the panel's bottom
    // edge instead of sitting on its frame.
    private static readonly Rectangle SettingsPanel = new(335, 100, 1250, 880);

    /// <summary>The closed MODE dropdown row — shows the current mode, opens the list on click.</summary>
    private static readonly Rectangle SettingsModeRow = new(860, 202, 524, 58);

    /// <summary>The closed WINDOW SIZE dropdown row. Dim and inert outside WINDOWED mode.</summary>
    private static readonly Rectangle SettingsSizeRow = new(860, 278, 524, 58);

    /// <summary>The effects-volume slider track (the grab area is padded around it).</summary>
    private static readonly Rectangle SettingsFxTrack = new(900, 416, 400, 30);

    /// <summary>The music-volume slider track.</summary>
    private static readonly Rectangle SettingsMusicTrack = new(900, 472, 400, 30);

    // CLOSE sits on the RIGHT, where every other panel in the game puts its way out (playtest
    // 2026-08-25: "close is left behind on the left"); the exit to the desktop sits left of it.
    // The CLOSE button is gone (playtest 2026-08-26: "remove the CLOSE text; the corner icon closes");
    // QUIT TO DESKTOP sits centred in the row it had shared.
    private static readonly Rectangle SettingsQuit = new(810, 880, 300, 56);
    /// <summary>The panel's corner close icon — UiKit.CloseButton at UiKit.CloseRect, inside the ornament.</summary>
    private static readonly Rectangle SettingsCornerClose = UiKit.CloseRect(SettingsPanel);

    /// <summary>Copies the feedback code (build stamp + progress + run log) to the clipboard.</summary>
    private static readonly Rectangle SettingsCopyFeedback = new(536, 800, 400, 52);

    /// <summary>START A NEW GAME at rest, and its wider red armed state spanning the whole row.</summary>
    private static readonly Rectangle SettingsNewGame = new(984, 800, 400, 52);
    private static readonly Rectangle SettingsNewGameArmed = new(536, 800, 848, 52);

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

    /// <summary>Height of one option row in an open settings dropdown.</summary>
    private const int DropRowHeight = 44;

    /// <summary>The gap between the closed field and the list that drops under it.</summary>
    private const int DropGap = 4;

    /// <summary>How far the list's ornate frame reaches in. Rows live inside this margin on every side.</summary>
    private const int DropListInset = UiKit.PanelCorner;

    /// <summary>Text inset from the edge of a row, and from the closed field's end ornament.</summary>
    private const int DropPadX = UiTypography.ButtonPadX;

    /// <summary>The size an option row and a closed field's value are set at — a control's own rung.</summary>
    private const int DropTextPx = UiTypography.ButtonText;

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
    private static readonly Rectangle SettingsGear = new(1842, 16, 60, 60);

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
        _ui.Scrim(_batch, 0.75f);
        _ui.Panel(_batch, SettingsPanel);
        // The panel is DARK glass, so light text on it — gold heading, bone labels. THE HEADING IS A
        // PANEL TITLE: it used to be an unsized call, so the one word naming the modal was drawn at the
        // body size — smaller than the MODE and WINDOW SIZE labels underneath it. It sits on the close
        // icon's row, which is what UiTypography.ModalTitleTop is.
        _ui.TextCenterBig(_batch, "SETTINGS", 960, SettingsPanel.Y + UiTypography.ModalTitleTop,
                          Gold, UiTypography.PanelTitle);

        // A click reaches the ordinary rows only while no dropdown list is open. The open list is
        // drawn over them, so its clicks — and the click that closes it — must be swallowed here.
        var uiClick = _clicked && _settingsDropdown == 0;
        var mouse = ChromeMouse;

        // The COPIED confirmation, up under the title where no row lives.
        if (_feedbackToastTimer > 0f) TextCenter(_feedbackToast, 960, SettingsPanel.Y + 82, Gold);

        // ── DISPLAY: two dropdowns. Closed rows draw here in layout order; the open list waits for
        //    the end of the method. ──
        Text("MODE", 536, 216, Bone);
        var modeNames = new[] { "WINDOWED", "BORDERLESS", "FULLSCREEN" };
        var modeIdx = _displayMode == DisplayMode.Windowed ? 0 : _displayMode == DisplayMode.Borderless ? 1 : 2;
        DropdownClosed(SettingsModeRow, modeNames[modeIdx], enabled: true, open: _settingsDropdown == 1);

        var windowed = _displayMode == DisplayMode.Windowed;
        var desktop = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var sizes = Display.OfferedWindowSizes(desktop.Width, desktop.Height);
        Text("WINDOW SIZE", 536, 292, windowed ? Bone : Slate);
        // Outside WINDOWED the size is the monitor's to decide, so the row says which one rather than
        // going blank — "AUTO" was a word about the setting; this is the answer the player wanted.
        var sizeValue = windowed
            ? Display.WindowSizeLabel(_windowSize, desktop.Width, desktop.Height)
            : $"YOUR SCREEN — {desktop.Width} × {desktop.Height}";
        DropdownClosed(SettingsSizeRow, sizeValue, enabled: windowed, open: _settingsDropdown == 2);

        // ── SOUND. Draggable 0-100 sliders: a click jumps there, holding drags, releasing saves —
        //    and the effects row clicks once on release so the new level is heard at the new level. ──
        _ui.Fill(_batch, new Rectangle(536, 366, 848, 2), new Color(0x3A, 0x3A, 0x44));
        Text("SOUND", 536, 380, Gold);

        var fx = SliderRow("EFFECTS VOLUME", SettingsFxTrack, _sfxVolume, 1, uiClick);
        if (fx >= 0 && fx != _sfxVolume)
        {
            _sfxVolume = fx;
            _sound.SfxVolume = _sfxVolume / 100f;   // live, so the release click previews the new level
        }

        var mu = SliderRow("MUSIC VOLUME", SettingsMusicTrack, _musicVolume, 2, uiClick);
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

        // ── FORGE. The way BACK for "don't ask me again" — a preference the player can suppress from
        //    a dialog must be reversible from settings, or one hasty click is permanent. ──
        _ui.Fill(_batch, new Rectangle(536, 542, 848, 2), new Color(0x3A, 0x3A, 0x44));
        Text("ASK BEFORE SELL OR SALVAGE", 536, 566, Bone);
        var askBtn = new Rectangle(1160, 554, 224, 52);
        if (_ui.Button(_batch, askBtn, _askBeforeScrap ? "ON — IT ASKS" : "OFF", mouse, uiClick))
        {
            _askBeforeScrap = !_askBeforeScrap;
            _forge.AskBeforeScrap = _askBeforeScrap;
            SaveDisplay();
        }

        // ── FIGHT TEXT AND EFFECTS. Quality-of-life switches (playtest 2026-08-23). Each is a plain
        //    ON/OFF; the hunt reads them on its next frame (fed in Update with the other flags). ──
        _ui.Fill(_batch, new Rectangle(536, 632, 848, 2), new Color(0x3A, 0x3A, 0x44));
        Text("FIGHT TEXT AND EFFECTS", 536, 646, Gold);
        var changed = false;
        changed |= ToggleRow("DAMAGE NUMBERS", 536, 684, ref _showDamageNumbers, uiClick);
        changed |= ToggleRow("SKILL NAMES", 1000, 684, ref _showSkillCallouts, uiClick);
        changed |= ToggleRow("FIGHT EFFECTS", 536, 732, ref _showHitEffects, uiClick);
        changed |= ToggleRow("RED FLASH", 1000, 732, ref _showScreenFlash, uiClick);
        if (changed) SaveDisplay();

        // ── FEEDBACK + RESET, side by side above the exit row. One click copies a code carrying the
        //    build stamp and progress; two deliberate clicks delete the save (see StartNewGame). ──
        _ui.Fill(_batch, new Rectangle(536, 786, 848, 2), new Color(0x3A, 0x3A, 0x44));
        // Hidden while the red SURE? bar covers this exact area — the confirm click must not also
        // copy a code over the player's clipboard (review 2026-08-24).
        if (_resetArmTimer <= 0f && _ui.Button(_batch, SettingsCopyFeedback, "COPY FEEDBACK CODE", mouse, uiClick))
        {
            // The failure is NOT silent (same rule as the weave's copy button): no clipboard, no lie.
            _feedbackToast = ClipboardInterop.TrySet(FeedbackCode())
                ? "COPIED — PASTE IT TO THE DEVELOPER"
                : "COPY FAILED — TRY AGAIN";
            _feedbackToastTimer = 4f;
        }

        if (_resetArmTimer > 0f)
        {
            _ui.Fill(_batch, SettingsNewGameArmed, new Color(0x8C, 0x1E, 0x1E));
            _ui.Fill(_batch, new Rectangle(SettingsNewGameArmed.X, SettingsNewGameArmed.Y, SettingsNewGameArmed.Width, 3), Ember);
            _ui.TextCenter(_batch, "SURE? THIS DELETES YOUR SAVE — CLICK AGAIN",
                           SettingsNewGameArmed.Center.X, SettingsNewGameArmed.Center.Y - 12, Color.White);
            if (UiKit.ClickedIn(SettingsNewGameArmed, mouse, uiClick))
            {
                _resetArmTimer = 0f;
                _wantsNewGame = true;
            }
            else if (_clicked)
            {
                _resetArmTimer = 0f;   // any click that is not the confirmation disarms
            }
        }
        else if (_ui.Button(_batch, SettingsNewGame, "START A NEW GAME", mouse, uiClick))
        {
            _resetArmTimer = ResetArmSeconds;
        }

        if (_ui.CloseButton(_batch, SettingsCornerClose, mouse, uiClick))
        {
            _showSettings = false;
            _settingsDropdown = 0;
        }

        // Esc no longer quits (it opens THIS panel), so the game needs a door that says what it does.
        // Hidden on the title screen, whose own menu already has QUIT — and whose Hunter may not exist
        // yet to save. It is the game's exit — the door Esc used to be — and it still saves on the way out.
        if (!_showTitle && _ui.Button(_batch, SettingsQuit, "QUIT TO DESKTOP", mouse, uiClick))
        {
            Save();
            Exit();
        }

        // Which build this is — the same stamp the feedback code carries, so "which version are you
        // on" is answerable from a screenshot.
        Text($"BUILD {BuildStamp.Short}", 380, 1000, Slate);

        // ── The OPEN dropdown list, drawn last so it sits over every row below it. A click on an
        //    option applies and closes; any other click just closes — and either way the rows under
        //    the list never see it (uiClick above). ──
        var openList = Rectangle.Empty;
        if (_settingsDropdown == 1)
        {
            openList = DropdownList(SettingsModeRow, modeNames, modeIdx, pick =>
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
            openList = DropdownList(SettingsSizeRow, labels, Array.IndexOf(sizes, _windowSize), pick =>
            {
                if (_windowSize == sizes[pick]) return;
                _windowSize = sizes[pick];
                ApplyDisplay();
                SaveDisplay();
            });
        }
        else if (_clicked)
        {
            // No list open: a click on a closed row opens its list, with the cursor unplaced so the
            // list puts it on the value the player already has.
            if (SettingsModeRow.Contains(mouse)) OpenDropdown(1);
            else if (windowed && SettingsSizeRow.Contains(mouse)) OpenDropdown(2);
        }

        // ── HOVER EXPLANATIONS — one plain sentence per row, near the cursor, after a short rest. ──
        DrawSettingsTips(mouse, windowed, openList);
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

        var ink = !enabled ? Slate : open ? Gold : hover ? Color.White : Bone;
        var room = chevronRight - 22 - textX;
        _ui.TextBig(_batch, _ui.ShortenBig(value, room, DropTextPx), textX,
                    r.Center.Y - DropTextPx * 27 / 40, ink, DropTextPx);

        // A chevron: two strokes meeting at a point, 16 wide and 6 deep. A solid stroke reads heavier
        // than type of the same colour, so an inert one is dimmed past the label it sits beside.
        var caret = !enabled ? new Color(0x5C, 0x64, 0x70) : ink;
        var cx = chevronRight - 8f;
        var cy = r.Center.Y + (open ? 3f : -3f);
        var dy = open ? -6f : 6f;
        _ui.LineSeg(_batch, new Vector2(cx - 8f, cy), new Vector2(cx, cy + dy), 3f, caret);
        _ui.LineSeg(_batch, new Vector2(cx, cy + dy), new Vector2(cx + 8f, cy), 3f, caret);
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
            var r = new Rectangle(inner.X, inner.Y + k * DropRowHeight, inner.Width, DropRowHeight);
            if (r.Contains(mouse)) _dropCursor = i;   // the mouse and the arrow keys share one cursor
            var lit = i == _dropCursor;

            if (lit)
            {
                _ui.Fill(_batch, r, DropRowHover);
                _ui.Fill(_batch, new Rectangle(r.X, r.Y, 3, r.Height), DropRowEdge);
            }
            if (k > 0) _ui.Fill(_batch, new Rectangle(r.X, r.Y, r.Width, 1), DropRowRule);
            if (i == currentIndex)
                _ui.Diamond(_batch, new Rectangle(r.X + DropPadX, r.Center.Y - 5, 10, 10), Gold);
            _ui.TextBig(_batch, options[i], r.X + DropPadX + 22, r.Center.Y - DropTextPx * 27 / 40,
                        lit ? Gold : Bone, DropTextPx);

            if (!_clicked || !r.Contains(mouse)) continue;
            CloseDropdown();
            pick(i);
            return list;
        }

        // The scroll thumb, only when there is something to scroll — a bar that is always full is noise.
        if (rows < options.Length)
        {
            var track = new Rectangle(inner.Right - 6, inner.Y + 2, 3, inner.Height - 4);
            _ui.Fill(_batch, track, DropRowRule);
            var h = Math.Max(18, track.Height * rows / options.Length);
            var y = track.Y + (track.Height - h) * _dropScroll / Math.Max(1, options.Length - rows);
            _ui.Fill(_batch, new Rectangle(track.X, y, track.Width, h), Gold);
        }

        // A click anywhere else — the closed field included — just closes the list.
        if (_clicked) CloseDropdown();
        return list;
    }

    /// <summary>A label and an ON/OFF button. Returns true when the click flipped it.</summary>
    private bool ToggleRow(string label, int x, int y, ref bool value, bool clicked)
    {
        Text(label, x, y + 10, Bone);
        var btn = new Rectangle(x + 276, y, 108, 44);
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
    private int SliderRow(string label, Rectangle track, int current, int dragId, bool clickable)
    {
        Text(label, 536, track.Y + 8, Bone);

        var bed = new Rectangle(track.X, track.Center.Y - 4, track.Width, 8);
        _ui.Fill(_batch, bed, new Color(0x2A, 0x24, 0x38));
        var fillW = (int)(track.Width * (current / 100f));
        if (fillW > 0) _ui.Fill(_batch, new Rectangle(bed.X, bed.Y, fillW, 8), new Color(0xC8, 0x9A, 0x3C));
        _ui.Fill(_batch, new Rectangle(track.X + fillW - 7, track.Y, 14, track.Height), Bone);
        Text($"{current}%", 1322, track.Y + 8, Slate);

        var mouse = ChromeMouse;
        var grab = new Rectangle(track.X - 10, track.Y - 6, track.Width + 20, track.Height + 12);
        if (clickable && _clicked && grab.Contains(mouse)) _dragSlider = dragId;
        if (_dragSlider == dragId && _mouse.LeftButton == ButtonState.Pressed)
            return Math.Clamp((int)MathF.Round((mouse.X - track.X) * 100f / track.Width), 0, 100);
        return -1;
    }

    /// <summary>Hover explanations for every settings row — one plain sentence each.</summary>
    /// <remarks>
    /// The zones cover the whole row (label and control), because a player hovers the words as often
    /// as the widget. Suppressed while a slider is mid-drag, while the cursor is inside an open
    /// dropdown list, and while START A NEW GAME is armed — an are-you-sure moment is not the moment
    /// for furniture. The tip appears after <see cref="TipDelaySeconds"/> of rest on one row; the
    /// clock ticks in Update and resets here whenever the hovered row changes.
    /// </remarks>
    private void DrawSettingsTips(Point mouse, bool windowed, Rectangle openList)
    {
        var zones = new (Rectangle Zone, string Key, string Tip)[]
        {
            (new Rectangle(536, 198, 848, 64), "mode",
                "How the game sits on your screen: in a window you can move, or filling the whole screen."),
            (new Rectangle(536, 274, 848, 64), "size",
                windowed ? "How big the game window is. The picture is the same at every size — a bigger window just draws it bigger. Nothing larger than your screen is offered; (native) is your screen's own size."
                         : "How big the game window is. You can change this only when MODE is WINDOWED — the other two modes always fill your screen."),
            (new Rectangle(536, 408, 848, 46), "fx",
                "How loud the hits, clicks and other short sounds are. Drag the handle, or click a spot on the line."),
            (new Rectangle(536, 464, 848, 46), "music",
                "How loud the background music is. Drag the handle, or click a spot on the line."),
            (new Rectangle(536, 552, 848, 56), "ask",
                "When this is on, the game asks you to confirm before an item is sold or broken down."),
            (new Rectangle(536, 680, 420, 50), "dmg",
                "Shows the damage of every hit as a small number in the fight."),
            (new Rectangle(1000, 680, 420, 50), "skills",
                "Shows the name of each skill as your champion uses it."),
            (new Rectangle(536, 728, 420, 50), "hitfx",
                "Shows the flashes and sparks when hits land in the fight."),
            (new Rectangle(1000, 728, 420, 50), "flash",
                "When your champion falls, the screen glows red for a moment. Turn this off if you do not want it."),
            (SettingsCopyFeedback, "feedback",
                "Copies a short code that describes your game. Paste it to the developer with your feedback."),
            (SettingsNewGame, "newgame",
                "Deletes your save and starts over from the beginning. It asks you to confirm first."),
        };

        var key = "";
        var tip = "";
        var suppressed = _dragSlider != 0 || _resetArmTimer > 0f
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
    internal static string Abbrev(long n)
    {
        if (n < 0) return "-" + Abbrev(-n);
        return n >= 1_000_000_000 ? $"{n / 1e9:0.##}B"
             : n >= 1_000_000 ? $"{n / 1e6:0.##}M"
             : n >= 10_000 ? $"{n / 1e3:0.#}K"
             : n.ToString();
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

        var e1 = 1826;   // right margin, left of the settings gear in the corner
        var l1 = _ui.Pill(_batch, e1, 16, "mat_scrap", new Color(0x9A, 0xC0, 0x88), Abbrev(scrapVal), "", new Color(0x9A, 0xC0, 0x88));
        var e2 = l1 - 20;
        var l2 = _ui.Pill(_batch, e2, 16, "ui_memory_dust", default, Abbrev(dustVal), "", new Color(0x9E, 0x86, 0xFF));
        var e3 = l2 - 20;
        var leftEdge = _ui.Pill(_batch, e3, 16, "ui_gleam_coin", default, Abbrev(gleamVal), "", new Color(0xF0, 0xB2, 0x4A));
        if (leftEdge < 1210) System.Diagnostics.Debug.WriteLine($"Currency bar (left {leftEdge}) crowds the stage header.");

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
            (new Rectangle(l1, 16, e1 - l1, 60), allMats, -1),
            (new Rectangle(l2, 16, e2 - l2, 60), "MEMORY DUST — STARTS A DESCENT FROM A WAVE YOU HAVE CLEARED (MAP) · BUILDS THE WARREN", dustVal),
            (new Rectangle(leftEdge, 16, e3 - leftEdge, 60), "GLEAM — BUYS UPGRADES ON STATS (V)", gleamVal),
        };
        foreach (var (rr, name, v) in pillRows)
        {
            if (!rr.Contains(ChromeMouse)) continue;
            var text = v < 0 ? name : $"{name}  ·  {v.ToString("N0", inv)}";
            var w = _ui.Measure(text) + 28;
            var tip = new Rectangle(Math.Min(rr.Right, 1904) - w, 84, w, 42);
            _ui.Fill(_batch, tip, new Color(0x0E, 0x0A, 0x14, 0xF0));
            _ui.Fill(_batch, new Rectangle(tip.X, tip.Y, tip.Width, 2), NavGold * 0.6f);
            _ui.TextRight(_batch, text, tip.Right - 14, tip.Y + 10, Bone);
            break;
        }
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
            _ui.TextCenterBig(_batch, items[i], box.Center.X, box.Center.Y - 16,
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
            TextCenter("SAVING IS OFF — " + _saveLockReason + ".", 960, 24, Ember);
            TextCenter("YOUR OLD FILE IS KEPT ON DISK, UNTOUCHED. START A NEW GAME IN SETTINGS TO PLAY FRESH.", 960, 60, Bone);
        }
    }

    // ── World map ─────────────────────────────────────────────────────────────────────────────

    private void UpdateWorld()
    {
        PushMapState();
        _mapScreen.Update(ScreenKeys, _prevKeys, CanvasMouse, MouseClicked);
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
            else _conquerMsg = $"{Regions.Get(id).Name} IS LOCKED — CONQUER THE PREVIOUS REGION.";
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
        _conquerMsg = firstTime
            ? $"THE CORRUPTION DEEPENS — {CorruptionLook.Label(tier)}. HARDER ENEMIES, MORE DUST."
            : $"THE CORRUPTION DEEPENS AGAIN — {CorruptionLook.Label(tier)}. (YOU ALREADY GOT THE DUST FOR THIS TIER.)";
        Save();
    }

    private void DrawWorld()
    {
        PushMapState();
        _mapScreen.Draw(_batch, CanvasMouse, MouseClicked);
        ConsumeMapRequests();
    }

    private void DrawHelp()
    {
        // Framed like every other surface. This was a bone rectangle with a black rectangle inside it —
        // the last flat two-rect panel in the game, on the screen a new player is most likely to open.
        _ui.Scrim(_batch, 0.72f);
        var panel = new Rectangle(112, 92, 1696, 840);
        _ui.Panel(_batch, panel);

        _ui.TextCenterBig(_batch, "IDLExIDLE — CONTROLS", panel.Center.X, panel.Y + UiTypography.ModalTitleTop, Gold, UiTypography.PanelTitle);

        // Two columns: the build on the left, the screens on the right.
        // DERIVED where it can be, and honest where it cannot. This panel is the only place a new player
        // is told how the game works, and it had drifted: it stated a flat "4 WOVEN SKILLS" when the
        // trait spine sells a fifth, and "3 KEYSTONE SOCKETS" when a player starts with ONE and buys the
        // others. Both numbers now come from the live loadout, so the help cannot be wrong about what
        // the player actually has.
        var build = new (string Key, string What)[]
        {
            ($"{_loadout.SkillCapacity}", "SKILL SLOTS"),
            ("", "STYLE  ·  SKILL  ·  VARIATION  ·  REINFORCEMENTS"),
            ($"{_loadout.KeystoneCapacity}", "KEYSTONE SOCKETS"),
            ("", "EACH HAS A COST"),
            ("", "TRAITS (P) SELL MORE OF BOTH"),
            ("", "A SKILL IS HOW YOU FIGHT"),
            ("", "SOURCE VS REGION"),
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
            ("V", "STATS — SPEND GLEAM"),
            ("B", "BUILD — CHOOSE YOUR SKILLS"),
            ("E", "MASTERY — HOW YOUR SKILLS WORK"),
            ("K", "VAULT — CHESTS YOU HAVE NOT OPENED"),
            ("F", "FORGE — CRAFT & CHESTS"),
            ("A", "WARREN — WORK WHILE YOU ARE AWAY"),
            ("W", "MAP — WHERE TO HUNT"),
            ("P", "TRAITS — PERMANENT BONUSES"),
            ("R", "ROSTER — YOUR CHAMPIONS"),
            ("L", "THE EXPEDITION LOG"),
            ("ESC", "SETTINGS — DISPLAY, SOUND, QUIT"),
            ("F1", "CLOSE"),
        };

        Text("YOUR BUILD", 176, 200, Bone);
        for (var i = 0; i < build.Length; i++)
        {
            Text(build[i].Key, 176, 254 + i * 38, Gold);
            Text(build[i].What, 356, 254 + i * 38, Bone);
        }

        // 38px pitch, not 46: eleven rows at 46 would run to y=754 and through THE IDEA block below.
        // The list is FOURTEEN rows now, so it reaches y=748 — and the two prose blocks below were
        // wrapped to 1568px, the full width of the panel, so they ran straight through it. They are
        // wrapped to the left column's width instead: two columns that share a page rather than two
        // blocks that share pixels.
        Text("SCREENS", 1140, 200, Bone);
        for (var i = 0; i < world.Length; i++)
        {
            Text(world[i].Key, 1140, 254 + i * 38, Gold);
            Text(world[i].What, 1256, 254 + i * 38, Bone);
        }

        Text("THE IDEA:", 176, 620, Gold);
        WrapText(
            "ONE CHAMPION, YOUR BUILD. IT FIGHTS ON ITS OWN, ON EVERY SCREEN, EVEN WHILE THE GAME IS " +
            "CLOSED. EACH WAVE IT CLEARS PAYS GLEAM AT ONCE. EVERY 5TH WAVE IS A BOSS. IT MAY DROP A CHEST. " +
            "WHEN IT FALLS, IT GETS UP AND GOES AGAIN. YOU LOSE NOTHING.",
            176, 664, 900, Bone);
        WrapText(
            $"SPEND GLEAM ON STATS (V) TO GROW STRONGER. REACH WAVE {ConquerWaveDepth} TO CONQUER A REGION AND UNLOCK THE NEXT.",
            176, 788, 900, Bone * 0.7f);
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
        ("STATS", 'V', "state_resonance_128"), ("BUILD", 'B', "state_mastery_128"),
        // THE MASTERY TREE GETS ITS OWN DOOR. It was reachable only through a button labelled EDIT BUILD
        // on the Build overview — neither what it edits nor what anyone would go looking for — and it is
        // the second-largest system in the game. nav_build is the only unused nav emblem with a real
        // centre (bright-pixel fraction 0.31; nav_evolve and nav_codex are empty rings at 0.00 / 0.01).
        ("MASTERY", 'E', "nav_build"),
        ("VAULT", 'K', "chest_loot"), ("FORGE", 'F', "icon_nav_forge"), ("WARREN", 'A', "icon_nav_warren"),
        ("MAP", 'W', "nav_relics_128"), ("TRAITS", 'P', "nav_prestige"),
        ("ROSTER", 'R', "icon_class_hunter"),
    };

    private static readonly Color NavGold = new(0xF0, 0xB2, 0x4A);
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
        => Career.VowWasKept(_loadout.ToBuild(_dust, _mastery, _characters.Active, _skillProgress), _hunter);

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
            // No dedicated refusal cue exists; the click at low volume reads as "heard you,
            // nothing happened", which is exactly what a locked tile means.
            _sound.Play("sfx_click", 0.45f);
            return;
        }

        _showGear = _showTraining = _showMastery = _showForge = _showWarren = _showWorld = _showTraits = _showRoster = _showLoadout = _showVault = false;
        // Navigating away abandons a pending SELL/SALVAGE question. Without this it sat armed and
        // invisible, and the player's first click on returning answered a dialog they had forgotten.
        _forge.CancelConfirm();
        _training.CancelConfirm();   // an armed RESET ALL TRAINING must not survive leaving the screen
        _expedition.FilterOpen = false;   // the hunt's CHEST FILTER popover folds when the player walks away
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

    /// <summary>Free width right of the nav rail, as a fraction of the authored 1920.</summary>
    internal const float OverlayScale = (1920f - NavRailWidth - 20f) / 1920f;

    /// <summary>Canvas x the inset content starts at.</summary>
    internal const float OverlayLeft = NavRailWidth;

    /// <summary>
    /// The 480-space cursor, mapped into an inset screen's own 1920-space coordinates.
    /// </summary>
    /// <remarks>
    /// Menu screens hit-test with <c>mouse * 4</c>. Under the inset that lands 180px right of where the
    /// player is actually pointing, so every one of them must invert the same transform its drawing goes
    /// through — hence one shared helper rather than the multiplication repeated per screen.
    /// </remarks>
    internal static Point ToOverlay(Point canvasMouse)
        => new((int)((canvasMouse.X * 4 - OverlayLeft) / OverlayScale), (int)(canvasMouse.Y * 4 / OverlayScale));
    // DERIVED from the table, not a literal. It was 1080/8 with a comment saying "eight items fill the
    // full height exactly", which was true right up until the roster added a ninth and the last tile
    // hung 135px off the bottom of the screen.
    private static readonly int NavTileHeight = 1080 / Nav.Length;

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
        var navFacts = GuideUnlockFacts();   // once per frame, not once per tile
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
            // THE ICON IS SIZED FROM THE TILE, not from a literal 48. NavTileHeight is 1080/Nav.Length,
            // so adding the eleventh tile took every tile from 108px to 98 — and at a fixed 48 starting
            // at Y+24 the icon ran to Y+72 while the label is drawn at Bottom-34, i.e. Y+64. The rail
            // would have rendered every glyph through its own caption. Pixel-identical at ten tiles.
            var iconPx = Math.Clamp(NavTileHeight - 60, 30, 48);
            if (_assets.Get(Nav[i].Glyph) is { } g)
                _batch.Draw(g, new Rectangle(r.Center.X - iconPx / 2, r.Y + 24, iconPx, iconPx), iconTint);
            else
                _ui.Diamond(_batch, new Rectangle(r.Center.X - iconPx / 2 + 4, r.Y + 28, iconPx - 8, iconPx - 8), on ? NavGold : NavGem * 0.75f);
            _ui.TextCenterBig(_batch, Nav[i].Label, r.Center.X, r.Bottom - 34,
                              !unlocked ? NavLabel * 0.35f : on ? NavGold : NavLabel * 0.9f, UiTypography.NavigationLabel);

            // The price, on the tile, so the rail teaches the progression without being clicked. Hover
            // only — nine requirement lines drawn permanently is the wall this pass exists to remove.
            if (!unlocked && hover)
                _ui.TextCenterBig(_batch, _ui.ShortenBig(Unlocks.Requirement(NavActivity[i]), NavRailWidth - 24, UiTypography.Secondary),
                                  r.Center.X, r.Bottom - 16, NavGem * 0.8f, UiTypography.Secondary);


            // A GOLD "NEW" MARK on a tile that is open with something unread on it — the quiet
            // replacement for the modal panel that used to announce every opening. On the LEFT of
            // the tile, because the VAULT's red chest count owns the right, and both can be true of
            // the VAULT on the frame it opens. Never on the lit tile: the player is already there, and
            // the banner at the top of that screen is the mark's payload. A tile visited this session
            // drops its mark even with the banner still open — the mark means "you have not looked".
            var activity = NavActivity[i];
            var isNew = !on && unlocked
                        && (((Onboarding.IsNew(activity, navFacts, _explained)
                              || (activity == Activity.Forge && Onboarding.GemTourDue(navGems, _explained) is not null))
                             && !_visited.Contains(activity))
                            || (activity == Activity.Roster && _rosterNews));
            if (isNew)
            {
                var mark = new Rectangle(r.X + 10, r.Y + 14, 50, 26);
                _ui.Fill(_batch, mark, NavGold);
                _ui.Fill(_batch, new Rectangle(mark.X, mark.Y, mark.Width, 3), new Color(0xFF, 0xE0, 0xA0));
                _ui.TextCenterBig(_batch, "NEW", mark.Center.X, mark.Y + 4, new Color(0x2A, 0x1C, 0x08),
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
                var badge = new Rectangle(r.Right - 54, r.Y + 16, 38, 30);
                _ui.Fill(_batch, badge, new Color(0xC8, 0x3A, 0x3A));
                _ui.Fill(_batch, new Rectangle(badge.X, badge.Y, badge.Width, 3), new Color(0xF0, 0x8A, 0x6A));
                _ui.TextCenterBig(_batch, n > 9 ? "9+" : n.ToString(), badge.Center.X, badge.Y + 4,
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

    /// <summary>Word-wrap, so explanatory copy does not run off the canvas.</summary>
    private void WrapText(string text, int x, int y, int maxWidth, Color c)
    {
        var words = text.Split(' ');
        var line = "";
        var row = 0;

        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : line + " " + word;

            if (_ui.Measure(candidate) > maxWidth)
            {
                Text(line, x, y + row * 36, c);   // 1920-space line height (was 9 in 480-space); WrapText is DrawHelp-only
                line = word;
                row++;
            }
            else line = candidate;
        }

        if (line.Length > 0) Text(line, x, y + row * 36, c);
    }
}
