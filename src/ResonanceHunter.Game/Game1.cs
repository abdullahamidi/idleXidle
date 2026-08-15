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
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Evolution;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using ResonanceHunter.Core.Prestige;
using ResonanceHunter.Core.Progression;
using ResonanceHunter.Core.Quests;
using ResonanceHunter.Core.Warrens;

namespace ResonanceHunter.Client;

/// <summary>
/// The shell: it owns the screens, the save, and the idle tick — and routes between them.
/// </summary>
/// <remarks>
/// <para>
/// The game is a <b>single-champion idle auto-battler</b>: you build ONE character and it fights on its
/// own, wave after wave, on every screen and even while the game is closed. There is no squad and no
/// bank-or-push decision any more — every cleared wave pays out at once, and the depth the build can hold
/// is the only thing that gates the haul. <see cref="SoloExpeditionScreen"/> is the main screen; the
/// build/mastery tree, Warren, Forge, world map and Memory Dust hang off it. This class renders and
/// routes — all rules live in ResonanceHunter.Core (ADR-001), and it decides nothing.
/// </para>
/// <para>
/// This doc used to describe "a playable greybox of the hunt loop" whose job was to answer <i>"is the
/// precision-hunt loop actually fun?"</i> — a manual-combat game that playtested as "3 buttons, win,
/// press R" and was replaced. Its ~1100 lines of screens lived on here for several commits after
/// nothing could reach them, because nothing pointed out that they were unreachable.
/// </para>
/// </remarks>
public class Game1 : Game
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
    /// <summary>
    /// Current integer scale. No longer a const: display settings change it at runtime, and in
    /// borderless/fullscreen it is derived from the monitor rather than chosen.
    /// </summary>
    private int _scale = 3;

    /// <summary>Where the canvas lands on screen (scaled + centred; the rest is letterbox).</summary>
    private Rectangle _present = Display.Present(Display.CanvasWidth * 3, Display.CanvasHeight * 3, 3);

    private DisplayMode _displayMode = DisplayMode.Windowed;
    private int _windowedScale = 3;

    // art-bible §4.1. Hearth Gold marks EARNED states only — never decoration.
    private static readonly Color VoidInk = new(0x1B, 0x16, 0x20);
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
    private MouseState _mouse, _prevMouse;
    private bool _clicked; // the left-click EDGE for this frame, latched in Update so Draw can read it
    private bool _rightClicked; // the right-click EDGE, latched the same way — the item context menu
    private int _wheel;    // mouse-wheel notches this frame, latched alongside the click

    // ── Game state ────────────────────────────────────────────────────────────────────────────
    private readonly Hunter _hunter = new();
    private readonly Random _rng = new();

    // The world of regions to conquer. The "active" region is what you hunt in and farm right now.
    private readonly World _world = new();
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

    /// <summary>Activities whose explanation has not been shown yet, oldest first.</summary>
    /// <remarks>
    /// A QUEUE rather than a single slot, because two gates can open on the same frame (conquering the
    /// first region opens both the Map and the Warren). Showing one and dropping the other would leave a
    /// screen permanently unexplained — the silent kind of gap this codebase keeps finding.
    /// </remarks>
    private readonly Queue<(string Head, string Body)> _unlockQueue = new();

    /// <summary>The explanation currently on screen, or empty. Dismissed by any click or key.</summary>
    private string _unlockShowing = "";
    private string _unlockHeadline = "";

    /// <summary>
    /// False until the first roster Refresh has been absorbed silently.
    /// </summary>
    /// <remarks>
    /// The unlocked set is derived from conquest and held in memory only — the save carries just the
    /// active champion's id — so the first Refresh after every load reports EVERY champion the player
    /// already owns as newly gained. Without this the game would open with a stack of "X JOINS YOU"
    /// panels for champions earned hours ago.
    /// </remarks>
    private bool _rosterBaselined;

    /// <summary>Last frame's facts, so an unlock can be noticed exactly once, when it happens.</summary>
    private UnlockFacts _lastUnlockFacts;
    private int _lastSkillSlots = -1;

    private int _regionProgression;

    private bool _showHelp;
    private bool _showSettings;

    // ── The idle half. Automation is EARNED here, never assumed. ──────────────────────────────
    private ForgeScreen _forge = null!;
    private SoloExpeditionScreen _expedition = null!;   // solo build model — one champion, not a squad
    private bool _showForge;

    // The player's build (four woven skills + three keystones), the mastery tree it walks, and the editor.
    private readonly PlayerLoadout _loadout = PlayerLoadout.Starter();
    private readonly MasteryTree _mastery = new();
    private int _deepestEver;   // the deepest wave ever reached — drives mastery points, so it must persist

    // The champion's recent gleam/second, measured live so it keeps earning OFFLINE at the rate it was
    // actually managing — not a guessed number. Saved, and applied on the next load as time-away gleam.
    private double _champGleamAccrued, _champSecondsAccrued;
    private float _champGleamRate;
    private int _chestsCredited;   // chests already credited to CRAFTER evolution (delta vs _forge.ChestsOpened)
    private float? _pendingRevealPose;   // RH_SHOT_T for the chest reveal, applied once the fixture has opened one
    private BuildScreen _buildScreen = null!;
    private bool _showBuild;

    // The character sheet — equipment + stat training + a live damage bench, apart from the fight it feeds.
    private CharacterScreen _character = null!;
    private bool _showCharacter;
    private StatsScreen _stats = null!;
    private bool _showStats;

    private AutomationScreen _automation = null!;
    private bool _showAutomation;

    // The Warren is a facility-production dashboard (spec rev 1). The old creature den (_automation) is
    // retired from the UI; _automation is kept only to carry the saved roster/cores forward (dormant).
    private WarrenScreen _warrenScreen = null!;
    private readonly Warren _warren = new();
    private long _warrenMasteryPool;   // Mastery the facilities have produced — feeds SetEarned, and is the spendable pool

    // ── Memory Dust prestige (Full Vision). NOTHING RESETS — Dust accrues from mastery. ───────
    private PrestigeScreen _prestige = null!;

    /// <summary>Which characters are yours, and which one you are. Unlocks derive from conquest.</summary>
    private readonly CharacterState _characters = new();

    /// <summary>
    /// Descents finished with a Vow's demand still met — the one quest counter that cannot be derived.
    /// </summary>
    /// <remarks>
    /// Every other quest reads a fact still true when you look at it. A run's Vow is gone the moment the
    /// run ends, so if this is not latched at that instant it can never be proved afterwards.
    /// </remarks>
    private int _runsWithVowKept;
    private RosterScreen _roster = null!;
    private bool _showRoster;
    private WeaveScreen _weave = null!;
    private bool _showWeave;

    /// <summary>THE VAULT — unopened chests, read before they are cracked.</summary>
    private ChestScreen _chests = null!;
    private bool _showChests;
    private bool _showPrestige;
    private readonly MemoryDustTree _dust = new();
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
        if (_showPrestige) { _ui.Background(_batch, "bg_constellation"); return; }
        if (_showAutomation) { _ui.Background(_batch, "bg_warren"); return; }
        if (_showBuild) { _ui.Background(_batch, "bg_warren"); return; }   // the workshop, reused as the build bench

        // Combat and results share the arena; results dims it so the panels read. Prefer a
        // region-themed arena when its art exists (bg_arena_machine, bg_arena_shadow, …), and fall
        // back to the verdant arena otherwise — so travelling to a new region themes automatically
        // the moment its background is added, with no code change.
        var theme = Regions.Get(_activeRegion).Theme.ToString().ToLowerInvariant();
        var arena = _assets.Get($"bg_arena_{theme}") is not null ? $"bg_arena_{theme}" : "bg_arena_verdant";
        _ui.Background(_batch, arena, ArenaDim);
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
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Resonance Hunter — prototype";
    }

    // ── Persistence. For an idle game this is not plumbing — it IS the game. ──────────────────
    private float _sinceAutosave;
    private string _bootMessage = "";
    private Color _bootColor = Bone;
    private float _bootTimer;   // the "welcome back" toast — a few seconds after boot, then it fades
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
            (_displayMode, _windowedScale) = Display.Load();
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

        if (!result.Ok)
        {
            // A corrupt save is NEVER silently replaced with a new game — that looks exactly like the
            // game deleted your progress on purpose. Say so, and leave the old file alone.
            _bootMessage = SaveSystem.Explain(result.Failure);
            _bootColor = result.Failure == LoadFailure.Missing ? Dim : Ember;
            if (result.Failure == LoadFailure.Missing) SeedNewGame();
            return;
        }

        var save = result.Save!;
        _hasSave = true; // the title screen offers CONTINUE rather than NEW HUNT

        SaveSystem.RestoreHunter(save, _hunter);
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
                save.WovenSkills.Select(s => (s.Source, s.Form, s.VowId)), save.SocketedKeystoneIds);

        _deepestEver = save.MasteryEarned;         // stored the deepest-ever; Earned re-derives from it
        _mastery.RestoreTaken(save.MasteryTaken);

        // The Warren facility economy — levels/XP restored before the offline tick below so its production
        // is computed against the real facility levels, not a fresh level-1 base.
        SaveSystem.RestoreWarren(save, _warren);
        _warrenMasteryPool = save.WarrenMasteryPool;
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
        _characters.Restore(save.ActiveCharacterId, save.QuestsDone);
        _runsWithVowKept = save.RunsWithVowKept;
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
        _chestsCredited = save.ChestsCredited;

        var roster = SaveSystem.RestoreRoster(save);
        _automationRoster = roster;
        _pendingCores = save.UnhatchedCores;
        _pendingInventory = SaveSystem.RestoreInventory(save);
        _pendingChests = save.UnopenedChests
            .Select(s => new Chest
            {
                Rarity = (Rarity)s.Rarity,
                Tier = s.Tier,
                Element = Enum.TryParse<Source>(s.Element, out var e) ? e : null,   // unknown element → inert, never a throw
                Region = s.Region,                            // null on a pre-profile save → uniform loot, as before
                RunTilt = s.RunTilt <= 0f ? 1f : s.RunTilt,   // a pre-tilt save reads 0; neutral is 1
            })
            .ToList();
        _pendingWorn = new Dictionary<GearSlot, string?>
        {
            [GearSlot.Weapon] = save.WornWeaponId, [GearSlot.Charm] = save.WornCharmId, [GearSlot.Focus] = save.WornFocusId,
            [GearSlot.Helm] = save.WornHelmId, [GearSlot.Chest] = save.WornChestId, [GearSlot.Gloves] = save.WornGlovesId,
            [GearSlot.Boots] = save.WornBootsId, [GearSlot.Ring] = save.WornRingId,
        };

        // ── Restore the world. Conquest, corruption, and the active region are restored UNCONDITIONALLY,
        // so a save that carries them but (through an old or edited state) has no per-region farms keeps its
        // whole conquered world and endgame corruption instead of being silently reset to the home region.
        // Ordered before the IsUnlocked check so the active region resolves against the restored conquest.
        _world.RestoreConquered(save.ConqueredRegions);
        _world.RestoreCorruption(save.CorruptionTier);
        if (!string.IsNullOrEmpty(save.ActiveRegion) && _world.IsUnlocked(save.ActiveRegion))
            _activeRegion = save.ActiveRegion;

        // Per-region farms + team assignments — multi-region saves carry one farm each; older single-region
        // saves fold everything into the home region.
        if (save.RegionFarms.Count > 0)
        {
            foreach (var rf in save.RegionFarms)
            {
                var farm = _world.RegionFarm(rf.Id);
                farm.RestoreMasteryPoints(rf.MasteryPoints);
                farm.RestoreBestDepth(rf.BestDepth);
                farm.AutomationStage = rf.Stage;
                foreach (var c in roster.Where(c => rf.AssignedIds.Contains(c.Id))) farm.Assign(c);
            }
        }
        else
        {
            var home = _world.RegionFarm(VerdantHollow.RegionId);
            home.RestoreMasteryPoints(save.RegionMasteryPoints);
            home.AutomationStage = save.AutomationStage;
            foreach (var c in roster.Where(c => save.AssignedCreatureIds.Contains(c.Id))) home.Assign(c);
        }

        _region = _world.RegionFarm(_activeRegion);

        // ── Offline progression: the farm ran while you were away. ────────────────────────────
        // Always apply the elapsed time (even a few seconds), but only greet the player when the trip
        // actually produced something — a "0.0 HOURS, 0 kills" banner is noise, not a welcome.
        var credited = SaveSystem.CreditedOfflineSeconds(result.OfflineSeconds);

        // The Warren produced the whole time you were away — credit it into the real balances (Gleam and
        // Dust are shared accumulators; Mastery banks into the pool that feeds SetEarned).
        //
        // ConqueredRegions IS SET HERE, and was not before. Its only other assignments live in TickFarms
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
        _dust.AwardFromMastery((int)wOffline.Dust);
        _warrenMasteryPool += wOffline.Mastery;

        // THE CHAMPION earned while you were away too, at HALF the rate it was managing live (offline is
        // never as good as playing — that's what brings you back). Its gleam-rate was measured last
        // session, so this is grounded in what your build actually does, not a guess.
        var champOffline = (int)(credited * save.ChampionGleamRate * 0.5);
        if (champOffline > 0) _hunter.AddGleam(champOffline);

        // Seed the LIVE rate from the save, not just the offline calc above. Without this the field stays 0
        // until the champion fights for >10s and recomputes it — so a short or farm-only session would
        // autosave that 0 straight back, wiping offline champion income until the next sustained fight.
        _champGleamRate = save.ChampionGleamRate;

        if (credited > 1.0)
        {
            // Every staffed region farmed while you were away — sum them into one welcome-back tally.
            int kills = 0, cores = 0, gleam = 0;
            AutomationYield? activeYield = null;
            foreach (var def in Regions.All)
            {
                var farm = _world.RegionFarm(def.Id);
                if (farm.Team.Count == 0) continue;

                // Offline must apply the SAME Memory-Dust mastery-rate boost the live tick does, or a
                // purchased SHARPENED RECALL does nothing across the offline span — which is where an idle
                // game banks most of its mastery.
                var y = farm.Tick((float)credited, gleamPerKill: 8, masteryRate: DustEffects.MasteryRate(_dust));
                kills += y.Kills;
                cores += y.CoresProduced;
                gleam += y.GleamRealized;
                if (def.Id == _activeRegion) activeYield = y;
            }

            _hunter.AddGleam(gleam);
            _pendingCores += cores;
            _pendingOfflineYield = activeYield;

            if (kills > 0 || gleam > 0 || champOffline > 0 || credited >= 60)
            {
                var hours = credited / 3600.0;
                var span = hours >= 1.0 ? $"{hours:0.0} HOURS" : $"{credited / 60.0:0} MIN";
                // wOffline.Gleam WAS MISSING FROM THIS SUM. It is credited to the balance forty lines
                // above and was then left out of the only report of it, so the largest single payment
                // in the game was invisible to the player receiving it. Attribution matters more than
                // the total: it is the difference between "the game gave me money" and knowing WHICH of
                // your two economies is paying you.
                _bootMessage = $"WELCOME BACK — {span} AWAY\n"
                               + $"+{Abbrev((long)champOffline + gleam + wOffline.Gleam)} GLEAM "
                               + $"({Abbrev(wOffline.Gleam)} WARREN · {Abbrev((long)champOffline + gleam)} HUNT)"
                               + $" · {kills} KILLS · {cores} CORES";
                _bootColor = Gold;
            }
        }
        else if (champOffline > 0)
        {
            // A short trip that the farm ignores can still have earned the champion something.
            _bootMessage = $"WELCOME BACK\n+{Abbrev(champOffline)} GLEAM EARNED WHILE AWAY";
            _bootColor = Gold;
        }
    }

    /// <summary>
    /// A new hunter starts with a squad, because otherwise the game is a HARD SOFTLOCK.
    /// </summary>
    /// <remarks>
    /// Once the roster became the thing that fights, an empty roster meant: no squad → no expedition →
    /// no cores → no creatures → no squad. A fresh player could do literally nothing. These three also
    /// teach the composition in one glance: an Attacker to kill, a Defender to hold the front, and a
    /// Support to heal between waves.
    /// </remarks>
    private void SeedNewGame()
    {
        // Every other creation path calls BeginEvolution; the starters didn't, so their Evolution stayed null
        // and they were frozen out of the whole loop — no wave/work credit, unable to evolve — until the
        // player hatched replacements. Seed them the same way so a new roster is live from wave one.
        Creature Starter(string id, Source src, Role role, int tier)
        {
            var c = Creature.Hatch(id, src, role, tier);
            c.BeginEvolution(EvolutionTrees.For(src), new EvolutionProgress());
            return c;
        }

        _automationRoster = new List<Creature>
        {
            Starter("start_atk", Source.Nature, Role.Attacker, 3),
            Starter("start_def", Source.Nature, Role.Defender, 3),
            Starter("start_sup", Source.Nature, Role.Support, 2),
        };
        _pendingCores = 2;
        // The starter creatures staff the WARREN now (the idle farm), not a combat squad. Combat is your
        // one champion and its build — so the first nudge points at the build, not at a front line.
        _bootMessage = "PRESS B TO WEAVE YOUR BUILD, THEN SEND YOUR CHAMPION IN.";
        _bootColor = Gold;
    }

    private List<Creature>? _automationRoster;
    private List<ItemInstance>? _pendingInventory;
    private List<Chest>? _pendingChests;
    private List<RunReport>? _pendingRunLog;
    private Dictionary<GearSlot, string?> _pendingWorn = new();
    private int _pendingCores;
    private int _pendingChestsOpened;
    private AutomationYield? _pendingOfflineYield;

    private void Save()
    {
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
            _hunter, _region, _automation.Roster, _forge.Inventory, _automation.Cores, SaveFile.NowMs,
            _dust, _highestMasteryAwarded, _world, _activeRegion, _warren, _warrenMasteryPool) with
        {
            // The build rides along via `with`, so Core's Capture stays unaware of the Game-layer loadout.
            WovenSkills = _loadout.SaveSkills()
                .Select(s => new SavedSkill { Source = s.Source, Form = s.Form, VowId = s.VowId }).ToList(),
            SocketedKeystoneIds = _loadout.KeystoneIds.ToList(),
            // The expedition log rides along the same way. The report is the only place this game can
            // teach, and the player it teaches is by definition not watching — the lesson has to survive
            // being closed.
            RunLog = _expedition.Log.Entries.Select(RunLog.ToSave).ToList(),
            MasteryTaken = _mastery.Taken.ToList(),
            // Only WHICH character, never which are unlocked — that is derived from conquest every
            // frame, so there is nothing here to fall out of step with the world.
            ActiveCharacterId = _characters.ActiveId,
            QuestsDone = _characters.SaveQuests().ToList(),
            RunsWithVowKept = _runsWithVowKept,
            ChestsOpened = _forge.ChestsOpened,
            ChestsCredited = _chestsCredited,
            MasteryEarned = _deepestEver,          // stored as deepest-ever; Earned is re-derived on load
            ChampionGleamRate = _champGleamRate,
            // Unopened chests ride along too — a boss's drop must survive a reload, opened or not.
            UnopenedChests = _forge.UnopenedChests
                .Select(c => new SavedChest
                {
                    Rarity = (int)c.Rarity, Tier = c.Tier, Element = c.Element?.ToString(),
                    Region = c.Region, RunTilt = c.RunTilt,
                })
                .ToList(),
        };

        SaveFile.TryWrite(save, out _);
    }

    private float _farmAccum;

    /// <summary>
    /// Advance every staffed region farm in real time. Runs on every screen, every frame — a farm you
    /// conquered keeps producing while you fight elsewhere, which is what "idle" means.
    /// </summary>
    private void TickFarms(float dt)
    {
        _farmAccum += dt;
        if (_farmAccum < 1f) return; // the farm resolves in whole-second ticks
        var span = _farmAccum;
        _farmAccum = 0f;

        foreach (var def in Regions.All)
        {
            var farm = _world.RegionFarm(def.Id);
            if (farm.Team.Count == 0) continue; // an unstaffed region produces nothing

            var y = farm.Tick(span, gleamPerKill: 8, masteryRate: DustEffects.MasteryRate(_dust));
            _hunter.AddGleam(y.GleamRealized);
            _automation.Cores += y.CoresProduced;              // cores feed the one shared hatchery
            if (def.Id == _activeRegion) _automation.ReportOffline(y); // the farm screen shows the active region
        }

        // The Warren's facilities produce every second too, on every screen — Gleam/Dust into the real
        // balances, Mastery into the pool that feeds the build tree (see the SetEarned call below).
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
        if (w.Dust > 0) _dust.AwardFromMastery((int)w.Dust);
        _warrenMasteryPool += w.Mastery;
    }

    /// <summary>
    /// Draw the WARREN nav destination: the facility-production dashboard.
    /// </summary>
    /// <remarks>
    /// The dashboard performs its upgrade in the DRAW pass (the same place Forge does its Refine), so the
    /// spend + <see cref="Warren.Upgrade"/> happen here, right after Draw sets the request. Gleam and Dust
    /// spend from their real balances; Mastery spends from the produced pool.
    /// </remarks>
    private void DrawWarren()
    {
        _warren.Name = Regions.Find(_activeRegion)?.Name ?? "THE WARREN";
        _warren.ConqueredRegions = _world.ConqueredIds.Count;
        _warrenScreen.Warren = _warren;

        // One facility level per five waves of proven depth. Recomputed every frame it draws, so a
        // record set this session raises the ceiling without a restart. Floor of 1: a new player must
        // still be able to see what a facility does before their first descent ends.
        _warren.FacilityLevelCap = Math.Max(1, DeepestAnywhere() / Warren.DepthPerFacilityLevel);

        _warrenScreen.GleamOwned = _hunter.Gleam;
        _warrenScreen.MasteryOwned = _warrenMasteryPool;
        _warrenScreen.DustOwned = _dust.MemoryDust;
        _warrenScreen.Draw(_batch, CanvasMouse, MouseClicked);

        if (_warrenScreen.ConsumeUpgrade() is { } kind
            && _warren.CanUpgrade(kind, _hunter.Gleam, _warrenMasteryPool, _dust.MemoryDust))
        {
            var c = _warren.UpgradeCost(kind);
            _hunter.SpendGleam(c.Gleam);
            _warrenMasteryPool -= c.Mastery;
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
        _ui = new UiKit(GraphicsDevice, _font, _assets);
        _automation = new AutomationScreen(_ui);
        _forge = new ForgeScreen(_ui);
        _prestige = new PrestigeScreen(_ui, _dust);
        _roster = new RosterScreen(_ui);
        _chests = new ChestScreen(_ui);
        _weave = new WeaveScreen(_ui);
        _expedition = new SoloExpeditionScreen(_ui);
        _buildScreen = new BuildScreen(_ui);
        _character = new CharacterScreen(_ui, _forge);
        _stats = new StatsScreen(_ui);
        _warrenScreen = new WarrenScreen(_ui);
        _mapScreen = new MapScreen(_ui);
        // The canvas is now 1920x1080; screens still draw in 480x270 logical units (see ArtScale).
        _canvas = new RenderTarget2D(GraphicsDevice, CanvasWidth * ArtScale, CanvasHeight * ArtScale);

        // Apply anything the save restored. Screens are constructed after Initialize(), so the loaded
        // state is parked in _pending* fields until here.
        if (_automationRoster is not null) _automation.RestoreRoster(_automationRoster);
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
        if (_pendingRunLog is not null) _expedition.Log.Restore(_pendingRunLog);
        if (_pendingOfflineYield is not null) _automation.ReportOffline(_pendingOfflineYield);
        _automation.Cores += _pendingCores;
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
        ("gear", () => _showCharacter = true),
        ("stats", () => { _showCharacter = false; _showStats = true; }),
        ("build", () => { _showStats = false; _showBuild = true; }),
        ("weave", () => { _showBuild = false; _showWeave = true; }),
        ("forge", () => { _showWeave = false; _showForge = true; }),
        ("warren", () => { _showForge = false; _showAutomation = true; }),
        ("map", () => { _showAutomation = false; _showWorld = true; }),
        ("traits", () => { _showWorld = false; _showPrestige = true; }),
        ("roster", () => { _showPrestige = false; _showRoster = true; }),
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
        // NullReferenceException in StatsScreen that a player cannot produce. Beside the hotkeys it
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

        // ── Title screen — a real front-end instead of dropping straight into a fight. ──────────
        if (_showTitle)
        {
            // DEV: when capturing a gameplay screenshot, skip straight past the title (and open a screen).
            var sm = Environment.GetEnvironmentVariable("RH_SHOT_MODE");
            // `telegraph`, `combat` and `boss2` posed the manual-combat screen for screenshots. That
            // screen is gone, so they had nothing to pose.
            if (sm is "vfx" or "forge" or "farm" or "dust" or "world"
                or "region2" or "region3" or "conquered" or "help" or "expedition" or "fight" or "boss" or "bossdebug"
                or "banked" or "lootforge" or "settings" or "settingsfull" or "vow" or "hybrid" or "runlog" or "reforge" or "build" or "buildtree" or "buildzoom" or "character" or "itemmenu" or "stats" or "warren" or "map" or "rig"
                or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "traitlit" or "traitterm" or "traitterminal"
                or "roster" or "rosterlocked" or "weave" or "vault")
            {
                _showTitle = false;
                // Muster screen with a real roster to arrange.
                if (sm == "expedition") _automation.DevPopulate(_region);
                if (sm == "vow")
                {
                    _automation.DevPopulate(_region);
                    // Swear a Vow on the first creature so the populated row can be screenshotted.
                    if (_automation.Roster.FirstOrDefault() is { } first) first.VowId = "vow_bloodied";
                }
                // lootforge: seed the Forge with a spread of loot so it can be screenshotted with content
                // (the idle loop drops items only on boss waves, which a 1-second shot won't reach).
                // RH_SHOT_T poses the chest reveal at a chosen instant — the shake, the burst, the card.
                // Read BEFORE the fixture runs; applied after it opens a chest, or the open overwrites it.
                if (sm == "lootforge" && Environment.GetEnvironmentVariable("RH_SHOT_T") is { } rt
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
                    _forge.AddLoot(seed);
                    TellForgeTheBuild(_loadout.ToBuild(_dust, _mastery, _characters.Active));
                    _forge.DevManage();   // the loot-forge fixture poses the SALVAGE hub (grid, chests, merge)
                    _forge.DevSelect(_forge.Inventory.Count - 1);   // the SIPHON charm (added last) — poses a new combo
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

                if (sm is "build" or "buildtree" or "buildzoom")
                {
                    _showBuild = true;

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
                    _loadout.AddSkill();
                    // SIX passives, which is the most the panel will list (it Takes 6). Same reasoning as
                    // the fifth skill card above: the passives list is variable-length and the RESONANCE
                    // heading below it used to be pinned at a fixed offset, so a full list was drawn
                    // straight through the heading. A fixture stopping at three could never show that.
                    foreach (var id in new[] { "heavy_hand", "sharpened", "sunder", "crush", "breaker",
                                               "monolith", "opener", "hasten", "alpha", "mark_mastery",
                                               "interrupt" })
                        _mastery.Take(id);

                    if (sm == "buildtree") _buildScreen.DevOpenTree();
                    // The same tree at a working zoom. Node art is thirty pixels across in the
                    // overview, where a capture can only prove that something was drawn. RH_SHOT_ZOOM
                    // picks the scale, so the rim (masteries) and the hub (minors) are both reachable
                    // from the capture script without editing this file again.
                    if (sm == "buildzoom")
                        _buildScreen.DevOpenTree(
                            float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_ZOOM"),
                                           System.Globalization.CultureInfo.InvariantCulture, out var dz)
                                ? dz : 0.95f);

                }

                // ITEMMENU reuses the CHARACTER fixture wholesale and then opens the menu on it —
                // seeding a second, different bag would pose a screen the game never shows.
                if (sm is "character" or "itemmenu")
                {
                    _showCharacter = true;
                    _character.Loadout = _loadout;
                    _character.Mastery = _mastery;
                    _character.Tree = _dust;
                    // Seed gear so the bag and the three worn slots pose with content, gleam so the train
                    // buttons are live, and a few trained ranks so the values aren't all at base.
                    var rar = new[] { Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
                    var types = new[]
                    {
                        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Helm,
                        ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
                    };
                    var seed = new List<ItemInstance>();
                    for (var i = 0; i < types.Length; i++)
                        seed.Add(new ItemInstance
                        {
                            InstanceId = $"cd{i}", BaseType = types[i],
                            Rarity = rar[i % rar.Length], SellValue = 20 + i * 9, Element = Source.Nature,
                            ItemLevel = 8 + i * 6,
                        });
                    // Two superior UNEQUIPPED drops, added first so they sit at the top of the bag — they
                    // pose the green UP badge and the hover tooltip's "UPGRADE +N PWR" verdict (with
                    // RH_SHOT_MOUSE parked over the first card).
                    _forge.AddLoot(new List<ItemInstance>
                    {
                        new() { InstanceId = "up_wpn", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary, SellValue = 220, Element = Source.Machine, ItemLevel = 62 },
                        new() { InstanceId = "up_cht", BaseType = ItemBaseType.Chest, Rarity = Rarity.Epic, SellValue = 150, Element = Source.Nature, ItemLevel = 55 },
                    });
                    _forge.AddLoot(seed);
                    // Extra unequipped drops so the inventory grid fills and overflows into a scroll (fixture §17).
                    var srcs = new[] { Source.Shadow, Source.Body, Source.Mind, Source.Spirit, Source.Machine, Source.Nature };
                    var extra = new List<ItemInstance>();
                    for (var i = 0; i < 14; i++)
                        extra.Add(new ItemInstance
                        {
                            InstanceId = $"cx{i}", BaseType = types[i % types.Length],
                            Rarity = rar[i % rar.Length], SellValue = 15 + i * 5, Element = srcs[i % srcs.Length],
                            ItemLevel = 20 + i * 3,
                        });
                    _forge.AddLoot(extra);
                    foreach (var it in seed) _hunter.Equip(it);   // one item per slot — all eight filled
                    _hunter.AddGleam(5000);
                    for (var i = 0; i < 8; i++) _hunter.Train(HunterStat.AttackPower);
                    for (var i = 0; i < 4; i++) _hunter.Train(HunterStat.Vitality);

                    // LAST, and inside this block: the item menu poses ON this fixture, so it can only
                    // open after the bag it points into has actually been filled.
                    if (sm == "itemmenu") _character.DevOpenItemMenu();
                }

                if (sm == "stats")
                {
                    _showStats = true;
                    _stats.Loadout = _loadout;
                    _stats.Mastery = _mastery;
                    _stats.Tree = _dust;
                    _stats.Character = _characters.Active;
                    _hunter.AddGleam(20000);
                    for (var i = 0; i < 12; i++) _hunter.Train(HunterStat.AttackPower);
                    for (var i = 0; i < 6; i++) _hunter.Train(HunterStat.CriticalChance);
                    for (var i = 0; i < 5; i++) _hunter.Train(HunterStat.Defense);
                    for (var i = 0; i < 8; i++) _hunter.Train(HunterStat.Vitality);
                    _deepestEver = 23; _mastery.SetEarned(77400);   // fixture career values (Stats §13)
                }

                if (sm is "fight" or "fightgear" or "fightswing" or "fightreport" or "fightfall" or "runlog")
                {
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
                        _bootMessage = "WELCOME BACK — 18 MIN AWAY\n+140 GLEAM · 12 KILLS · 0 CORES";
                        _bootColor = Gold; _bootTimer = 7f;
                    }
                    _forge.AddChest(new Chest { Rarity = Rarity.Epic, Tier = 8, Element = Source.Nature });
                    _mastery.SetEarned(3);
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
                }
                if (sm is "boss" or "bossdebug")
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
                    _conquerMsg = "THE WORLD IS YOURS.  DEEPEN THE CORRUPTION FOR MORE.";
                    _showWorld = true;
                }
                if (sm == "help") _showHelp = true;
                if (sm == "settings") _showSettings = true;
                if (sm == "settingsfull") { _showSettings = true; _displayMode = DisplayMode.Fullscreen; }
                if (sm == "world")
                {
                    // Show a mid-progression map: home conquered, Cinderworks unlocked and staffed.
                    _world.Conquer(VerdantHollow.RegionId);
                    _world.RegionFarm("cinderworks").AutomationStage = 2;
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
                    _dust.AwardFromMastery(77_400);
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
                if (sm is "forge" or "hybrid" or "reforge")
                {
                    _showForge = true;
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
                    _dust.AwardFromMastery(77_400);               // the Memory-Dust pill, so it reads like the ref

                    // The HYBRID shot poses the merge recipe row, which lives in the SALVAGE hub.
                    if (sm == "hybrid") { _forge.DevQueueHybrid(); _forge.DevManage(); }
                    if (sm == "reforge") _forge.DevReforge();
                }
                if (sm == "warren")
                {
                    _showAutomation = true;
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
                    _warrenMasteryPool = 77_400;
                    _dust.AwardFromMastery(12_600);
                }
                if (sm == "farm")
                {
                    _showAutomation = true;   // deprecated: the creature den was removed; shows the Warren dashboard
                }
                // TRAITLIT / TRAITTERM pose the unlock flourish mid-animation. A celebration is over in
                // a second and the capture rig renders a fixed frame count then exits, so without a
                // fixture "the flourish works" could only ever be a claim.
                // ROSTER poses the whole cast unlocked so every card's art can be checked at once;
                // ROSTERLOCKED leaves it as a fresh save, which is the state a new player actually
                // sees and the one where a locked card still has to explain itself.
                // THE VAULT — a pile of chests of different grades, regions and depths, so the page poses
                // with the variety it exists to show rather than five copies of one card.
                if (sm == "vault")
                {
                    _showChests = true;
                    var grades = new[] { Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Rare,
                                         Rarity.Uncommon, Rarity.Common };
                    var regions = new[] { "cinderworks", "umbral_reach", VerdantHollow.RegionId,
                                          "pale_choir", "marrow_wastes", "still_archive" };
                    for (var i = 0; i < grades.Length; i++)
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
                    _showWeave = true;
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
                    ApplySkillCapacity();
                    _loadout.AddSkill();
                    _weave.DevPose(1, null);
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
                        // A fresh save posed part-way into one quest and nowhere on the other, so the
                        // capture shows a live count rather than two identical LOCKED cards.
                        _world.RegionFarm(VerdantHollow.RegionId).RestoreBestDepth(14);
                        _roster.DevSelect("quiver");
                    }
                }

                if (sm is "dust" or "traitlit" or "traitterm" or "traitterminal")
                {
                    _showPrestige = true;
                    _dust.AwardFromMastery(77_605);   // Dust still shows in the top pills; it no longer buys traits

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
                    _prestige.DevSelect("ks_bloodlust");   // AVAILABLE: its prerequisite (THE GLASS ROAD) is lit
                    _hunter.AddGleam(131_900_000);     // the top currency pills read like the reference
                    _hunter.AddMaterials(12_600);

                    // Frozen at 0.30s: past the flash, into the shockwaves, with the name plate risen
                    // and readable. RH_SHOT_T moves the freeze so the other beats can be checked too.
                    var poseT = float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_T"),
                                               System.Globalization.CultureInfo.InvariantCulture, out var pt)
                        ? pt : 0.30f;
                    if (sm == "traitlit") _prestige.DevPoseLit(_dust, "ks_bloodlust", poseT);
                    if (sm is "traitterm" or "traitterminal")
                    {
                        // The Ruin road walked to its end — the one purchase in the game that costs
                        // twelve points and closes off three other roads.
                        foreach (var id in new[] { "socket_2", "ks_glass_cannon", "ks_bloodlust", "ks_blood_magic" })
                            _dust.Purchase(id);
                        _prestige.DevPoseLit(_dust, "ks_reaper", poseT);
                    }
                }
            }
            else {
            if (Pressed(Keys.F10)) _showSettings = !_showSettings;

            if (_showSettings)
            {
                if (Pressed(Keys.Escape)) _showSettings = false;
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

        // Escape backs out of an open panel before it quits the game. Escape is the reflex for "get me
        // out of this menu" — and in fullscreen it is the reflex for "give me my desktop back". Wiring
        // it straight to Exit() meant a player poking at the display options quit to desktop instead of
        // closing them, which in an idle game is a genuinely expensive misfire.
        // Edge, not held-state: with IsKeyDown, holding Escape closes the panel on one frame and quits
        // on the very next one — the fix would have quietly kept the bug.
        if (Pressed(Keys.Escape))
        {
            if (_showSettings) _showSettings = false;
            else if (_showHelp) _showHelp = false;
            else Exit();
        }

        if (_bootTimer > 0f) _bootTimer = Math.Max(0f, _bootTimer - dt);
        if (_lockedTimer > 0f) _lockedTimer = Math.Max(0f, _lockedTimer - dt);

        // Autosave. An idle game that loses your farm to a crash has taken your hours, not your time.
        _sinceAutosave += dt;
        if (_sinceAutosave >= AutosaveIntervalSeconds)
        {
            _sinceAutosave = 0f;
            Save();
        }

        // Every staffed farm runs, every frame, on every screen — including mid-combat. This is the
        // whole point of an idle game, and once you have conquered regions, all of them farm at once.
        TickFarms(dt);

        // The dev rig-spike tech demo moved OFF Tab (F9) — Tab is the Forge's loot filter, and the global
        // binding here ran first every frame, hijacking the filter into a blank dev screen.
        if (Pressed(Keys.F6)) _expedition.DevForceBoss = !_expedition.DevForceBoss;   // dev: force the Crystal Lich boss render (Rev 4 §12)
        if (Pressed(Keys.F7)) { _expedition.DevBossDebug = !_expedition.DevBossDebug; _character.DevGearDebug = !_character.DevGearDebug; _stats.DevStatsDebug = !_stats.DevStatsDebug; _buildScreen.DevBuildDebug = !_buildScreen.DevBuildDebug; _forge.DevForgeDebug = !_forge.DevForgeDebug; _warrenScreen.DevWarrenDebug = !_warrenScreen.DevWarrenDebug; _mapScreen.DevMapDebug = !_mapScreen.DevMapDebug; _prestige.DevDustDebug = !_prestige.DevDustDebug; }   // dev layout overlays
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

        // AN UNLOCK EXPLANATION SWALLOWS INPUT while it is up: it is drawn over the nav rail, so a click
        // meant to dismiss it would otherwise also land on whatever tile is underneath and throw the
        // player onto a screen they did not ask for.
        //
        // IT DOES NOT RETURN EARLY, and that is not a style choice. The settings panel can, because it
        // is impossible to have open on the first frame. This one is not: a brand-new save queues an
        // explanation immediately, so an early return here skipped the per-frame block that feeds every
        // screen its dependencies, and the first Draw hit a null Loadout in StatsScreen. check_boot.sh
        // caught it, which is the second time this exact shape — a return placed ahead of the feeds —
        // has taken the game down. Swallow the input; never skip the frame.
        //
        // The champion keeps fighting behind it. An idle game does not pause to talk to you.
        // THE ORDER HERE IS THE WHOLE FIX. The first version cleared a local flag inside the dismissal
        // branch and then assigned _swallowInput from it — so on the very frame the dismissing click was
        // consumed, _swallowInput came out FALSE while _clicked was still latched true for the rest of
        // the frame. The click closed the panel AND went on to hit HandleNavClick, all nine hotkeys and
        // every button hit-tested during Draw. The guard was written and then defeated by its own
        // sequencing, which is why the swallow is now set BEFORE the branch and never cleared by it.
        _swallowInput = _unlockShowing.Length > 0;
        if (_swallowInput && (_clicked || AnyKeyPressed()))
        {
            _unlockShowing = "";
            _unlockHeadline = "";
            _sound.Play("sfx_click", 0.7f);
            // _swallowInput deliberately STAYS true: this frame's input was spent closing the panel.
        }

        HandleNavClick();   // a click on the shared hex nav works from any screen

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
        // _showRoster and _showWeave — so the player could see one screen while an invisible one
        // consumed their clicks. OpenNav clears all nine, every time.
        for (var navKey = 0; navKey < Nav.Length; navKey++)
        {
            // Keys.A..Keys.Z are the ASCII letter codes, so the table's char IS the key.
            if (!Pressed((Keys)Nav[navKey].Key)) continue;
            // Pressing the key of the screen you are already on returns you to the hunt, preserving the
            // toggle these handlers used to have.
            OpenNav(NavActive() == navKey ? 0 : navKey);
            break;
        }
        // L — THE EXPEDITION LOG. It closes every other overlay, because it is a full-screen read and
        // the one thing the player opens specifically to think, not to act.
        if (Pressed(Keys.L))
        {
            _expedition.ToggleLog();
            if (_expedition.LogOpen)
            {
                // ALL NINE, not seven. _showRoster and _showWeave were missing, so opening the log from
                // the roster or the weave left that screen live underneath it — and because the log is
                // drawn in the same batch, OverlayActive stayed true and the batch kept the OVERLAY
                // inset transform instead of the plain canvas one. DrawLog's own hit-tests assume the
                // plain one, so its page buttons landed in a third coordinate space.
                _showPrestige = _showAutomation = _showForge = _showWorld = false;
                _showBuild = _showCharacter = _showStats = _showRoster = _showWeave = _showChests = false;
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
        if (Pressed(Keys.T))
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
                var wasWeave = _showWeave;
                OpenNav(0);              // clears all nine flags in one place
                _showWeave = !wasWeave;
            }
        }




        // THE CHAMPION FIGHTS EVERYWHERE. Ticked here, before any overlay can early-return, so a run
        // keeps clearing waves and paying out while you're in the Forge, the tree, or another region's
        // menu — the whole point of an idle game. The fight screen's own clicks are handled in its Draw,
        // which only runs when that screen is the one on top, so a click in the Forge cannot fall
        // through into the fight without any flag being threaded down for it.
        _regionProgression = Math.Clamp((int)_region.MasteryLevel + (_region.MasteryLevel > 0 ? 1 : 0), 0, 4);
        UpdateExpedition(gameTime);

        // A modal eats the frame's INPUT, but not the frame, and not the fight. The autosave and the
        // farms tick above; the champion ticks on the line above this one. What must not happen is the
        // hotkeys and buttons underneath the panel continuing to respond.
        if (_showSettings) { Latch(gameTime); return; }

        if (_showWorld) { UpdateWorld(); Latch(gameTime); return; }

        if (_showBuild)
        {
            _buildScreen.Loadout = _loadout;
            _buildScreen.Mastery = _mastery;
            _buildScreen.Power = _hunter.PowerRating;   // the Build screen has no Hunter ref of its own
            _buildScreen.Level = _hunter.HunterLevel;
            _buildScreen.Update(_keys, _prevKeys, CanvasMouse, MouseClicked,
                                _mouse.LeftButton == ButtonState.Pressed, MouseWheel, _dust);
            if (_buildScreen.Dirty) { _buildScreen.ClearDirty(); Save(); }
            // The BUILD page asks for the weave editor; the host owns which screen is open.
            if (_buildScreen.WantsWeave) { _buildScreen.WantsWeave = false; _showBuild = false; _showWeave = true; }
            Latch(gameTime);
            return;
        }

        if (_showCharacter)
        {
            _character.Loadout = _loadout;
            _character.Mastery = _mastery;
            _character.Tree = _dust;
            _character.Update(_keys, _prevKeys, CanvasMouse, MouseClicked, MouseRightClicked, MouseWheel, _hunter);

            // ── THE ITEM MENU'S VERBS. Three of the four live in the Forge, so the gear screen names
            //    what it wants and the host carries the player there, already pointed at the item.
            //    EQUIP is the exception: it is the gear screen's own verb and never leaves. ────────
            if (_character.ConsumeItemAction() is { } request)
            {
                if (request.Action == ItemAction.Equip)
                {
                    if (_forge.Inventory.FirstOrDefault(i => i.InstanceId == request.InstanceId) is { } toWear)
                        _hunter.Equip(toWear);
                    else if (Enum.GetValues<GearSlot>().Select(_hunter.Worn).OfType<ItemInstance>()
                                 .FirstOrDefault(i => i.InstanceId == request.InstanceId) is { } toDoff
                             && Gear.SlotFor(toDoff.BaseType) is { } sl)
                        _hunter.Unequip(sl);
                    Save();
                }
                else
                {
                    _forge.FocusFor(request.InstanceId, request.Action switch
                    {
                        ItemAction.Upgrade => ForgeScreen.ForgeMode.Upgrade,
                        ItemAction.Reforge => ForgeScreen.ForgeMode.Reforge,
                        _ => ForgeScreen.ForgeMode.Salvage,
                    });
                    _showCharacter = false;
                    _showForge = true;
                    _sound.PlayFirst(1f, "sfx_forge", "sfx_click");
                }
            }
            if (_character.Dirty) { _character.ClearDirty(); Save(); }
            Latch(gameTime);
            return;
        }

        // THE VAULT. Placed here, in the SAME relative position as its Draw branch, because Update's and
        // Draw's screen chains are two independent statements of the same priority and nothing enforces
        // that they agree — a screen inserted at a different point in each is a screen the player sees
        // while an invisible one eats their clicks.
        if (_showChests)
        {
            _chests.Update(dt, _forge.UnopenedChests, CanvasMouse, MouseClicked, MouseWheel);

            switch (_chests.ConsumeOpen())
            {
                case ChestScreen.OpenRequest.Selected:
                {
                    // The screen sorts for display, so its index is into the SORTED pile, not the
                    // Forge's storage order. Resolve it here rather than exposing storage order to a view.
                    var sorted = ChestDossiers.BestFirst(_forge.UnopenedChests);
                    if (sorted.Count > 0)
                    {
                        _forge.OpenOneChest(sorted[Math.Clamp(_chests.SelectedIndex, 0, sorted.Count - 1)], _hunter);
                        _sound.Play("sfx_forge", 0.9f);
                        Save();
                    }
                    break;
                }
                case ChestScreen.OpenRequest.All:
                    _forge.OpenEveryChest(_hunter);
                    _sound.Play("sfx_forge", 0.9f);
                    Save();
                    break;
            }

            Latch(gameTime);
            return;
        }

        if (_showStats)
        {
            _stats.Loadout = _loadout;
            _stats.Mastery = _mastery;
            _stats.Tree = _dust;
            _stats.Character = _characters.Active;
            _stats.HighestWave = _deepestEver;          // real career counters (Stats spec §9.2)
            _stats.ChestsOpened = _forge.ChestsOpened;
            _stats.MasteryPoints = _mastery.Earned;
            _stats.Update(_keys, _prevKeys, CanvasMouse, MouseClicked, MouseWheel, _hunter);
            if (_stats.Dirty) { _stats.ClearDirty(); Save(); }
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
            var reward = CorruptionScaling.RewardMultiplier(_world.CorruptionTier);
            _dust.AwardFromMastery((int)((totalMasteryLevels - _highestMasteryAwarded) * 15 * reward));
            _highestMasteryAwarded = totalMasteryLevels;
        }

        if (_showWeave)
        {
            _weave.Loadout = _loadout;
            _weave.Mastery = _mastery;
            _weave.Tree = _dust;
            _weave.Hunter = _hunter;
            _weave.Character = _characters.Active;
            _weave.RegionId = _activeRegion;
            _weave.RegionName = Regions.Get(_activeRegion).Name;
            _weave.Update(CanvasMouse, MouseClicked, MouseWheel);
            if (_weave.Dirty) { _weave.ClearDirty(); Save(); }
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

        if (_showPrestige)
        {
            _prestige.Update(_keys, CanvasMouse, MouseClicked, MouseWheel, _dust, dt);
            // Taking a trait is permanent and there is no respec, so it is worth a sound and worth
            // writing to disk immediately. The screen owns neither: it hands back a cue the same way
            // StatsScreen hands back a trained stat.
            if (_prestige.ConsumeCue() is { } cue)
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
        if (Pressed(Keys.F) && !_showAutomation)
        {
            _showForge = !_showForge;
            _showAutomation = false;
            _showCharacter = false;
        }


        if (_showForge)
        {
            // Keep the Forge told which Forms the build runs, so it can flag live combos here too (not
            // only when arrived at from the fight).
            TellForgeTheBuild(_loadout.ToBuild(_dust, _mastery, _characters.Active));
            _forge.Update(gameTime, _keys, CanvasMouse, MouseClicked, MouseWheel, _hunter);
            Latch(gameTime);
            return;
        }

        if (_showAutomation)
        {
            // WARREN is the facility-production dashboard. Production runs every frame in TickFarms; the
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
        _forge.ActiveForms = _loadout.Skills.Select(s => s.Form).ToList();
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
        return skills[0].Source != Source.Body || skills[0].Form != Form.Strike || skills[0].VowId is not null;
    }

    /// <summary>Pick the looping music bed for the current screen. No-op until the music_* WAVs exist.</summary>
    private void UpdateMusic()
    {
        string track;
        if (_showTitle) track = "music_title";
        else if (_showForge) track = "music_forge";
        else if (_showAutomation) track = "music_warren";
        else if (_showPrestige) track = "music_constellation";
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
        ChestsHeld: _forge?.UnopenedChests.Count ?? 0,
        RegionsConquered: _world.ConqueredIds.Count,
        TraitPointsEarned: _dust.Earned);


    /// <summary>
    /// Notice anything that just opened, and queue its explanation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called every frame from Update, and cheap: it is two struct comparisons and an enum walk. The
    /// alternative — firing the announcement from each of the places that CAUSE an unlock (the conquest
    /// branch, the chest drop, the item pickup) — is how a gate ends up with three call sites and one of
    /// them missing, which is precisely the bug species this project keeps shipping.
    /// </para>
    /// <para>
    /// The FIRST frame seeds the baseline instead of announcing. Without that, every existing player
    /// would be handed nine explanation panels in a row on the launch after this shipped.
    /// </para>
    /// </remarks>
    private void NoticeUnlocks()
    {
        // NOT UNDER THE CAPTURE RIG. RH_SHOT deliberately never reads the save, so every shot looks like
        // a first launch — and since the fixtures also grant mastery, items and conquests to pose their
        // screens, the panel fires over the exact thing each fixture exists to photograph. The rig is
        // not a player. A fixture that wants to pose the panel can still enqueue one itself.
        if (Environment.GetEnvironmentVariable("RH_SHOT") is not null) return;

        var now = GuideUnlockFacts();
        var slots = Unlocks.SkillSlots(now);

        if (_lastSkillSlots < 0)
        {
            // THE HUNT'S OWN EXPLANATION HAD NO WAY TO BE SHOWN. Activity.Hunt is open unconditionally,
            // so it is open in both `before` and `now` on every frame and NewlyOpened never yields it —
            // meaning the single best onboarding paragraph in the codebase ("your champion fights on its
            // own, forever, without you… you never click an attack") ran for nobody. The seeding guard
            // below is correct and necessary; Hunt just needs to be announced outside it, once, to
            // someone who has never played.
            if (!_hasSave)
                _unlockQueue.Enqueue((Unlocks.Headline(Activity.Hunt), Unlocks.Explain(Activity.Hunt)));

            _lastUnlockFacts = now;
            _lastSkillSlots = slots;
            return;
        }

        foreach (var opened in Unlocks.NewlyOpened(_lastUnlockFacts, now))
            _unlockQueue.Enqueue((Unlocks.Headline(opened), Unlocks.Explain(opened)));

        for (var slot = _lastSkillSlots + 1; slot <= slots; slot++)
            if (Unlocks.SkillSlotNote(slot) is { Length: > 0 } note)
                _unlockQueue.Enqueue(("A NEW SKILL SLOT", note));

        _lastUnlockFacts = now;
        _lastSkillSlots = slots;

        // One at a time, and only while the player is not already reading one.
        if (_unlockShowing.Length == 0 && _unlockQueue.Count > 0)
        {
            (_unlockHeadline, _unlockShowing) = _unlockQueue.Dequeue();
            _sound.PlayFirst(0.9f, "sfx_levelup", "sfx_click");

            // SWALLOW THIS FRAME TOO. _swallowInput is computed near the top of Update, ~40 lines before
            // this method runs, so on the frame a panel first appears the flag was still false and the
            // panel drew over live, clickable chrome. Whatever the player happened to be clicking when
            // the unlock fired went through underneath it.
            _swallowInput = true;
        }
    }


    /// <summary>
    /// The first-run guide, drawn as shared chrome over every screen in the game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THIS USED TO LIVE INSIDE THE ARENA, and that was the reported bug.</b> DrawGuide was called
    /// from SoloExpeditionScreen.DrawArena, which is reached only through the terminal <c>else</c> of
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
    /// Drawn beneath the unlock panel and above everything else, and suppressed while that panel is up
    /// so two pieces of teaching never compete for the same attention.
    /// </para>
    /// </remarks>
    private void DrawGuideBanner()
    {
        if (_showTitle || _showHelp || _showSettings) return;
        if (_unlockShowing.Length > 0) return;
        if (_guideStep is not { } step || !Tutorial.HasGuidance(step)) return;

        const int width = 980;
        var x = (1920 - width) / 2;
        var body = _ui.WrapBig(Tutorial.Body(step), width - 44, UiTypography.Secondary);
        var height = 58 + body.Count * 22;
        var y = 1080 - height - 26;

        _ui.Fill(_batch, new Rectangle(x, y, width, height), new Color(0x10, 0x0D, 0x18, 0xEE));
        _ui.Fill(_batch, new Rectangle(x, y, 5, height), NavGold);

        _ui.TextBig(_batch, Tutorial.Title(step), x + 22, y + 14, NavGold, UiTypography.Body);
        var ty = y + 44;
        foreach (var line in body)
        {
            _ui.TextBig(_batch, line, x + 22, ty, UiKit.Vellum, UiTypography.Secondary);
            ty += 22;
        }
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

    /// <summary>
    /// The explanation panel for something that just opened. Modal, and dismissed by the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately NOT a timed toast. The brief was that every activity be explained in detail, and a
    /// paragraph that slides away on a timer is a paragraph nobody finishes — the player is watching a
    /// fight, the text is competing with it, and the one thing they needed to read is the thing that
    /// left. It waits to be dismissed.
    /// </para>
    /// <para>
    /// Sized from the WRAPPED line count rather than a fixed height, because these strings differ by a
    /// factor of two in length and a fixed box would either clip the Build explanation or leave the
    /// Roster one floating in a mostly-empty panel. Wrapping is what makes it possible to write these
    /// as prose at all.
    /// </para>
    /// </remarks>
    private void DrawUnlockPanel()
    {
        if (_unlockShowing.Length == 0) return;

        const int pad = 44;
        const int wrapWidth = 900;
        var lines = _ui.WrapBig(_unlockShowing, wrapWidth, UiTypography.Body);
        var height = pad * 2 + 58 + lines.Count * 30 + 44;
        var box = new Rectangle((1920 - (wrapWidth + pad * 2)) / 2, Math.Max(80, (1080 - height) / 2),
                                wrapWidth + pad * 2, height);

        _ui.Fill(_batch, new Rectangle(0, 0, 1920, 1080), new Color(0x05, 0x03, 0x0A) * 0.72f);
        _ui.Fill(_batch, box, new Color(0x15, 0x0E, 0x24));
        _ui.Fill(_batch, new Rectangle(box.X, box.Y, box.Width, 4), NavGold);
        _ui.Fill(_batch, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), NavGem * 0.5f);

        _ui.TextBig(_batch, "NEWLY OPEN", box.X + pad, box.Y + 20, NavGem, UiTypography.Secondary);
        _ui.TextBig(_batch, _ui.ShortenBig(_unlockHeadline, wrapWidth, UiTypography.PanelTitle),
                    box.X + pad, box.Y + 46, NavGold, UiTypography.PanelTitle);

        var y = box.Y + pad + 58;
        foreach (var line in lines)
        {
            _ui.TextBig(_batch, line, box.X + pad, y, new Color(0xD8, 0xD2, 0xE4), UiTypography.Body);
            y += 30;
        }

        _ui.TextCenterBig(_batch, "CLICK OR PRESS ANY KEY TO CONTINUE", box.Center.X, box.Bottom - 34,
                          NavLabel, UiTypography.Secondary);
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
    /// a timer inside <see cref="SoloExpeditionScreen"/> and hands each cleared wave to us as a reward we
    /// credit the instant it lands: gleam and cores every wave, ITEMS on boss waves only (so the Forge
    /// isn't flooded and depth is what earns loot). Conquest is measured by the deepest wave reached.
    /// </remarks>
    /// <remarks>
    /// THE `interactive` PARAMETER IS GONE. It carried "is the combat screen the one on top", threaded
    /// down to SoloExpeditionScreen.Update — which read none of its four input arguments, because click
    /// handling had migrated into Draw and the Update-side plumbing was left standing. A gate that gates
    /// nothing is worse than no gate: the next person to need one would have found this and believed it
    /// was already handled. The fight screen's clicks are gated where they are actually read, in Draw.
    /// </remarks>
    private void UpdateExpedition(GameTime gameTime)
    {
        var def = Regions.Get(_activeRegion);
        _expedition.EnemySource = def.Theme;          // region element → the Source matchup
        _expedition.RegionId = def.Id;                // region id → the boss creature (boss_<region>) on boss waves
        _expedition.EnemyBias = def.CombatBias;       // region character → the enemy's bite tempo (feel + TRAP synergy)
        _expedition.Loadout = _loadout;               // the player's build, handed over live…
        _expedition.Tree = _dust;                     // …powered by the Dust tree's passive nodes
        _expedition.Mastery = _mastery;               // …and the mastery tree (affinity + node bonuses)
        _expedition.BestDepthHere = _world.RegionFarm(def.Id).BestDepth;   // so NEW RECORD means it
        _expedition.ChestCount = _forge.UnopenedChests.Count;   // drives the fight screen's "go open a chest" nudge
        _expedition.IdleGleamRate = _champGleamRate;            // gleam/sec the champion earns idle → HUNT idle panel
        // The first-run guide, or null once outgrown. Held on the HOST, not on the fight screen: it is
        // drawn as chrome over every screen now (DrawGuideBanner), because a guide that vanishes the
        // moment you obey it reads as a guide that has stopped working.
        _guideStep = Tutorial.Showing(GuideFacts());

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
        var wornBuild = _loadout.ToBuild(_dust, _mastery, _characters.Active);
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
                _unlockQueue.Enqueue(("QUEST COMPLETE", $"{done.Name} — {done.Demand}"));
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
        // pass silently, the same way NoticeUnlocks seeds its own baseline.
        foreach (var got in _characters.Refresh(_world.ConqueredIds))
        {
            if (!_rosterBaselined) continue;
            _unlockQueue.Enqueue(($"{got.Name} JOINS YOU",
                                  $"{got.PassiveName} — {got.PassiveText}\n\n"
                                  + "Switch champion on the ROSTER (R). Nothing resets when you do: both "
                                  + "trees, your gear, your Gleam and the Warren are shared."));
        }
        _rosterBaselined = true;
        _expedition.Character = _characters.Active;
        _character.Character = _characters.Active;

        // The spine's capacity nodes reach the loadout. Without this the sockets and the fifth weave are
        // bought and never granted — the shape of the failure this codebase keeps repeating.
        _loadout.KeystoneCapacity = DustEffects.KeystoneSockets(_dust);
        ApplySkillCapacity();
        // Sits immediately after the capacity is applied, so a slot the player just earned is announced
        // on the same frame it becomes usable rather than the frame after.
        NoticeUnlocks();
        _forge.AutoMergeOnOpen = DustEffects.AutoMergeAfterRuns(_dust);
        var scale = CorruptionScaling.HealthMultiplier(_world.CorruptionTier);
        var mod = RegionModifiers.For(_activeRegion);   // the region's themed combat twist (Map variety)

        _expedition.Update(
            gameTime, _hunter,
            // Every region is a rung up the ladder for the champion, not just a new element — so reaching
            // the sixth region is a real climb, and "more regions" is more progression, not more of the same.
            // The region MODIFIER twists this region's health/damage on top of the ladder + corruption.
            // THE REGION STEP IS GEOMETRIC, not linear — see Core/Encounters/RegionLadder for why the
            // linear form let a player conquer the fourth map with no gear at all. Region MASTERY stays
            // linear on purpose: it is a difficulty a player opts into inside one place, and it should
            // stack in even increments rather than compound with the chain.
            enemyBaseHealth: 110f * (1f + 0.35f * _regionProgression) * RegionLadder.Health(LadderIndex(_activeRegion)) * scale * mod.EnemyHealthMult,
            enemyBaseDamage: 9f * (1f + 0.20f * _regionProgression) * RegionLadder.Damage(LadderIndex(_activeRegion)) * scale * mod.EnemyDamageMult);

        // Measure the champion's gleam/second over this session, for offline earnings later.
        _champSecondsAccrued += gameTime.ElapsedGameTime.TotalSeconds;

        // Credit every wave the champion cleared since last frame.
        while (_expedition.HasReward)
        {
            var r = _expedition.TakeReward();
            _hunter.AddGleam(r.Haul.Gleam);
            _champGleamAccrued += r.Haul.Gleam;
            _automation.Cores += r.Haul.Cores;
            _region.RecordActiveKill();

            // The champion's victories feed the WARREN's evolution. Every cleared wave is a WaveCleared
            // for each creature, a boss also a BossFelled — the exact inputs EvolutionTrees hints ("FELL
            // 4 BOSSES", "CLEAR 40 WAVES") and nothing produced after the squad was retired, leaving the
            // ATTACKER and SUPPORT branches unreachable for everyone. See EvolutionCredit. (CRAFTER still
            // asks for a "banked run", a mechanic the idle loop deleted — a re-theme left for design.)
            EvolutionCredit.CreditWave(_automation.Roster, r.IsBoss);

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

            if (r.IsBoss && DropBossChest(r, def)) _expedition.FlashChest();   // a chest is a LOW-rate drop now, not a given
        }
        if (_champSecondsAccrued > 10) _champGleamRate = (float)(_champGleamAccrued / _champSecondsAccrued);

        // CRAFTER evolution: credit the warren for every boss chest cracked at the Forge since last frame.
        var chestsOpenedNow = _forge.ChestsOpened - _chestsCredited;
        if (chestsOpenedNow > 0)
        {
            _chestsCredited = _forge.ChestsOpened;
            for (var i = 0; i < chestsOpenedNow; i++) EvolutionCredit.CreditChestOpened(_automation.Roster);
        }

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
            // demand tested against the same WeaveContext. That matters — a Vow SWORN and a Vow KEPT are
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
            _dust.AwardFromMastery((int)(40 * CorruptionScaling.RewardMultiplier(_world.CorruptionTier)));
            _sound.PlayFirst(1f, "sfx_conquer", "sfx_levelup");
            _conquerMsg = unlocked is not null
                ? $"{Regions.Get(_activeRegion).Name} CONQUERED!  {unlocked.Name} UNLOCKED — MAP (W)."
                : "THE WORLD IS YOURS.  DEEPEN THE CORRUPTION ON THE MAP (W).";
            Save();
        }
    }

    /// <summary>A boss dropped a CHEST — its grade rolled by depth now, its contents rolled when opened.</summary>
    /// <remarks>
    /// Depth buys RARITY, not quantity: every boss cleared deeper folds into the tier, so a wave-40 boss
    /// drops a chest likelier to be Legendary — a reason to push, not a firehose. The item roll, the loot
    /// filter, and any auto-merge now happen at OPEN (in the Forge), so nothing lands in the bag unbidden.
    /// </remarks>
    /// <summary>Roll whether this boss drops a chest (a LOW, depth-scaled chance). Returns true if one did.</summary>
    private bool DropBossChest(SoloExpeditionScreen.WaveReward r, RegionDefinition def)
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
        _forge.AddChest(Chests.RollDrop(lootTier, def.Theme, _rng, runTilt: _expedition.CarriedQuality,
                                        region: _activeRegion));
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
    private const int ConquerWaveDepth = 7;



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
        var fade = Math.Clamp(_bootTimer / 1.2f, 0f, 1f);   // fade over the last ~1.2s
        // Rev 4 §18.4: a FIXED two-line welcome-back toast at (590,165,740,82) — never a full-width band,
        // never ellipsized. Line 1 (duration) at OverlayTitle, line 2 (haul) at OverlayBody. The message is
        // authored as "line1\nline2" by the boot handlers.
        var parts = _bootMessage.Split('\n');
        // Framed panel art rather than a flat band with two gold rules. The flat version butted straight
        // against the stage banner above it and read as a seam in the chrome; a panel with its own border
        // sits clearly ON TOP of the scene, which is what a transient toast should do.
        var r = new Rectangle(660, 176, 600, 96);
        if (_assets.Get("ui_panel_modal_wide") is { } bg) _batch.Draw(bg, r, Color.White * fade);
        else _ui.Panel(_batch, r);
        _ui.TextCenterBig(_batch, parts[0], r.Center.X, r.Y + 22, _bootColor * fade, UiTypography.OverlayTitle);
        if (parts.Length > 1) _ui.TextCenterBig(_batch, parts[1], r.Center.X, r.Y + 56, Bone * fade, UiTypography.OverlayBody);
    }

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
    /// The mouse in TRUE 1920×1080 space — <see cref="CanvasMouse"/> scaled ×4.
    /// </summary>
    /// <remarks>
    /// The shared chrome (currency pills, hex nav, title, settings modal) now authors in 1920 coords at
    /// scale 1, so its hit-tests must compare against a 1920-space cursor. <see cref="CanvasMouse"/> stays
    /// 480-space for the unconverted screens (batch B, scale 4); this is its ×4 counterpart for chrome.
    /// </remarks>
    private Point ChromeMouse => new(CanvasMouse.X * 4, CanvasMouse.Y * 4);

    /// <summary>
    /// Push the chosen display mode to the device and recompute the letterbox.
    /// </summary>
    /// <remarks>
    /// Windowed picks its own integer scale; borderless and fullscreen take the monitor's size and
    /// derive the LARGEST WHOLE scale that fits, centring the rest. Pixel art is never stretched.
    /// </remarks>
    private void ApplyDisplay()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        switch (_displayMode)
        {
            case DisplayMode.Windowed:
                Window.IsBorderless = false;
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = CanvasWidth * _windowedScale;
                _graphics.PreferredBackBufferHeight = CanvasHeight * _windowedScale;
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

    private void RecomputePresent()
    {
        var vw = _graphics.PreferredBackBufferWidth;
        var vh = _graphics.PreferredBackBufferHeight;
        _scale = _displayMode == DisplayMode.Windowed
            ? _windowedScale
            : Display.LargestIntegerScale(vw, vh);
        _present = Display.Present(vw, vh, _scale);
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

    private bool MouseClicked => _clicked && !_showSettings && !_swallowInput;
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
        var kick = _showPrestige ? _prestige.Shake : Vector2.Zero;
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            null, null, null,
            Matrix.CreateScale(OverlayScale)
                * Matrix.CreateTranslation(OverlayLeft + kick.X, kick.Y, 0f));
    }

    /// <summary>True when a menu screen owns the frame — those draw inset; the fight screen does not.</summary>
    private bool OverlayActive =>
        _showForge || _showWorld || _showPrestige || _showAutomation || _showBuild || _showCharacter
        || _showStats || _showRoster || _showWeave || _showChests;

    /// <summary>The counter-scale the active screen draws at. Converted 1920-coord screens return 1; the
    /// HUNT screen is converted, so it returns 1 whenever no other screen flag is set (the else branch below).</summary>
    // Every screen now authors in true 1920×1080 at canvas scale 1 (the migration is complete). This shim
    // stays only so step 5 can flip the global constants and delete it in one place; it is now a constant 1.
    private int ScreenScale() => 1;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    protected override void Draw(GameTime gameTime)
    {
        // Render the 480x270 virtual canvas, then integer-upscale it (ADR-003).
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
            GraphicsDevice.Clear(VoidInk);
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
        if (_showForge) _forge.Draw(_batch, _hunter, CanvasMouse, MouseClicked);
        else if (_showWorld) DrawWorld();
        else if (_showPrestige) _prestige.Draw(_batch, _dust, CanvasMouse, MouseClicked);
        else if (_showRoster) { _roster.Progress = QuestSnapshot(); _roster.Draw(_batch, _characters, CanvasMouse, MouseClicked); }
        else if (_showChests) _chests.Draw(_batch, _forge.UnopenedChests, CanvasMouse, MouseClicked);
        else if (_showWeave) _weave.Draw(_batch, CanvasMouse, MouseClicked);
        else if (_showAutomation) DrawWarren();
        else if (_showBuild) _buildScreen.Draw(_batch, CanvasMouse, _dust);
        else if (_showCharacter) _character.Draw(_batch, CanvasMouse, _hunter);
        else if (_showStats)
        {
            _stats.Draw(_batch, CanvasMouse, _hunter, MouseClicked);

            // Gleam is one of the three payouts a descent makes, and this is the layer it buys. The model
            // (geometric cost, rank cap) has always been here; until now nothing in the game called it.
            if (_stats.ConsumeTrain() is { } stat && _hunter.Train(stat)) { _sound.Play("sfx_click", 0.8f); Save(); }
        }
        else _expedition.Draw(_batch, CanvasMouse, MouseClicked, Regions.Get(_activeRegion).Name, EnemyArtFor(_activeRegion), _bootTimer > 0f);

        // The LOG draws over everything, including the nav rail: it is a full-screen read, and the one
        // overlay a player opens to think rather than to act.
        _expedition.DrawLog(_batch, CanvasMouse, MouseClicked);
        _batch.End();

        // Batch C — the shared overlays (pills, nav, help/settings, boot toast), authored in true 1920 coords.
        BeginCanvas(1);
        DrawCurrencyPills();   // shared Gleam / Dust / Materials row, top-right of every screen
        DrawHexNav();   // the shared nav bar, over every screen

        if (_showHelp) DrawHelp();
        if (_showSettings) DrawSettings();

        DrawBootToast();
        DrawGuideBanner();
        DrawLockedToast();
        // LAST of the chrome, so the explanation of a thing that just opened sits over everything —
        // including the nav rail it is usually talking about.
        DrawUnlockPanel();

        _batch.End();

        // DEV SCREENSHOT HOOK: set RH_SHOT=<path> to dump one upscaled frame after ~1s, then exit.
        // Used to verify rendering headlessly; harmless and inert without the env var.
        _shotFrame++;
        var shotPath = Environment.GetEnvironmentVariable("RH_SHOT");
        if (shotPath is not null && _shotFrame == 60)
        {
            using var fs = System.IO.File.Create(shotPath);
            _canvas.SaveAsPng(fs, CanvasWidth * ArtScale, CanvasHeight * ArtScale);
            Exit();
        }

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(VoidInk);

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

    private void SaveDisplay() => Display.Save(_displayMode, _windowedScale);

    // ── Display settings ──────────────────────────────────────────────────────────────────────
    // 1920-space (scale-1 chrome): every literal is ×4 of its old 480-space value.
    private static readonly Rectangle SettingsPanel = new(480, 256, 960, 528);
    private static Rectangle ModeBtn(int i) => new(536, 392 + i * 88, 384, 72);
    private static Rectangle ScaleBtn(int i) => new(1000, 392 + i * 88, 384, 72);
    private static readonly Rectangle SettingsClose = new(800, 688, 320, 72);

    /// <summary>
    /// Display options, drawn as an overlay over whatever is behind it.
    /// </summary>
    /// <remarks>
    /// Drawn (not updated) because <see cref="UiKit.Button"/> hit-tests as it renders — the click edge
    /// is latched in Update precisely so Draw can read it. WINDOW SIZE is offered only in windowed mode:
    /// in borderless/fullscreen the scale is the monitor's to decide, and the buttons grey out rather
    /// than vanish so the panel doesn't reflow under the cursor.
    /// </remarks>
    private void DrawSettings()
    {
        _ui.Scrim(_batch, 0.75f);
        _ui.Panel(_batch, SettingsPanel);
        // The panel is DARK glass, so light text on it — gold heading, bone labels. (It used to use dark
        // parchment inks here, which were invisible on the dark panel.)
        TextCenter("DISPLAY", 960, 288, Gold);

        Text("MODE", 536, 344, Bone);

        // The right column's header doubles as its own explanation. A separate "fit to screen" line
        // had nowhere to live that wasn't already occupied — it landed on the greyed buttons. The
        // header is free space that is already describing exactly this, so it says it instead.
        var windowed = _displayMode == DisplayMode.Windowed;
        Text(windowed ? "WINDOW SIZE" : $"AUTO FIT — {_scale}x", 1000, 344, Bone);

        var modes = new[] { DisplayMode.Windowed, DisplayMode.Borderless, DisplayMode.Fullscreen };
        var modeNames = new[] { "WINDOWED", "BORDERLESS", "FULLSCREEN" };
        for (var i = 0; i < modes.Length; i++)
        {
            var on = _displayMode == modes[i];
            var r = ModeBtn(i);
            // The SELECTED row has to be the most readable one, not the least. `gold: true` only TINTS the
            // frame art — the interior of a row this short is still near-black — so the dark ink chosen for
            // "a gold surface" was drawn black on black, and the live display mode was the one thing on the
            // panel you could not read. Gold ink on the dark interior, which is the pairing that works here.
            _ui.Panel(_batch, r, gold: on);
            _ui.TextCenter(_batch, modeNames[i], r.Center.X, r.Center.Y - 12, on ? Gold : Bone);
            if (UiKit.ClickedIn(r, ChromeMouse, _clicked) && !on)
            {
                _displayMode = modes[i];
                ApplyDisplay();
                SaveDisplay();
            }
        }

        for (var i = 0; i < Display.WindowedScales.Length; i++)
        {
            var s = Display.WindowedScales[i];
            var on = windowed && _windowedScale == s;
            var r = ScaleBtn(i);
            _ui.Panel(_batch, r, gold: on);
            var label = $"{Display.CanvasWidth * s}x{Display.CanvasHeight * s}";
            _ui.TextCenter(_batch, label, r.Center.X, r.Center.Y - 12, on ? Gold : windowed ? Bone : Slate);
            if (windowed && UiKit.ClickedIn(r, ChromeMouse, _clicked) && !on)
            {
                _windowedScale = s;
                ApplyDisplay();
                SaveDisplay();
            }
        }

        if (_ui.Button(_batch, SettingsClose, "CLOSE", ChromeMouse, _clicked))
            _showSettings = false;
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
        var right = 1896;   // 1920-space right margin (was 474 in 480-space)
        right = _ui.Pill(_batch, right, 16, "mat_scrap", new Color(0x9A, 0xC0, 0x88), Abbrev(_hunter.MaterialOf(Material.Scrap)), "", new Color(0x9A, 0xC0, 0x88)) - 20;
        right = _ui.Pill(_batch, right, 16, "ui_memory_dust", default, Abbrev(_dust.MemoryDust), "", new Color(0x9E, 0x86, 0xFF)) - 20;
        var leftEdge = _ui.Pill(_batch, right, 16, "ui_gleam_coin", default, Abbrev(_hunter.Gleam), "", new Color(0xF0, 0xB2, 0x4A));
        if (leftEdge < 1210) System.Diagnostics.Debug.WriteLine($"Currency bar (left {leftEdge}) crowds the stage header.");
    }

    private void DrawTitle()
    {
        _ui.Background(_batch, "bg_title");

        // Logo, centred near the top. Drawn at a LAYOUT size with the asset's own aspect preserved — NOT
        // its native 1200×400, which the 4× canvas transform would blow up to 4800px (only "SONAN" showed).
        if (_assets.Get("logo_horizontal_full") is { } logo)   // package_08 branding (2048×512)
        {
            const int lw = 1280;   // 1920-space logo width (was 320 in 480-space)
            var lh = lw * logo.Height / logo.Width;   // aspect-preserved
            _batch.Draw(logo, new Rectangle((CanvasWidth * ArtScale - lw) / 2, 112, lw, lh), Color.White);
        }
        else
            TextCenter("RESONANCE HUNTER", 960, 240, Gold);

        var items = new[] { _hasSave ? "CONTINUE" : "BEGIN THE HUNT", "SETTINGS", "QUIT" };
        for (var i = 0; i < items.Length; i++)
        {
            var selected = i == _titleCursor;
            var box = new Rectangle(640, 608 + i * 112, 640, 96);
            _ui.Panel(_batch, box, gold: selected);
            // The ornate menu plates are DARK (even the gold/selected one), so both states take LIGHT text —
            // warm gold when selected, bone otherwise.
            _ui.TextCenter(_batch, items[i], box.Center.X, box.Center.Y - 12,
                selected ? new Color(0xF6, 0xD8, 0x88) : new Color(0xEC, 0xE6, 0xF2));

            // Clickable as well as keyed — every other menu in the game is.
            if (!_showSettings && UiKit.ClickedIn(box, ChromeMouse, _clicked)) ChooseTitleItem(i);
            else if (box.Contains(ChromeMouse)) _titleCursor = i;
        }

        TextCenter("UP / DOWN     ENTER", 960, 984, Slate);
    }

    // ── World map ─────────────────────────────────────────────────────────────────────────────

    private void UpdateWorld()
    {
        PushMapState();
        _mapScreen.Update(_keys, _prevKeys, CanvasMouse, MouseClicked);
        ConsumeMapRequests();
    }

    /// <summary>Feed the Map screen the live world state each frame (host owns the model, the screen renders it).</summary>
    private void PushMapState()
    {
        _mapScreen.World = _world;
        _mapScreen.ActiveRegion = _activeRegion;
        _mapScreen.DeepestWave = _deepestEver;
        _mapScreen.HunterPower = _hunter.PowerRating;
        _mapScreen.ConquerWaves = ConquerWaveDepth;
        _mapScreen.Message = _conquerMsg;
    }

    /// <summary>Act on the Map screen's ENTER / DEEPEN requests (set by keyboard in Update or buttons in Draw).</summary>
    private void ConsumeMapRequests()
    {
        if (_mapScreen.ConsumeEnter() is { } id)
        {
            if (_world.IsUnlocked(id)) { SetActiveRegion(id); _showWorld = false; }
            else _conquerMsg = $"{Regions.Get(id).Name} IS LOCKED — CONQUER THE PREVIOUS REGION.";
        }
        if (_mapScreen.ConsumeDeepen()) DeepenCorruption();
    }

    /// <summary>Push the fully-conquered world one corruption tier deeper: harder, richer, permanent.</summary>
    private void DeepenCorruption()
    {
        if (!_world.CanDeepenCorruption) return;
        var tier = _world.DeepenCorruption();
        _sound.PlayFirst(1f, "sfx_deepen", "sfx_conquer", "sfx_levelup");
        _dust.AwardFromMastery(CorruptionScaling.DeepeningDustAward(tier));
        _conquerMsg = $"THE CORRUPTION DEEPENS — TIER {tier}. STRONGER FOES, RICHER DUST.";
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

        _ui.TextCenterBig(_batch, "RESONANCE HUNTER — CONTROLS", panel.Center.X, panel.Y + 46, Gold, UiTypography.SectionTitle);

        // Two columns: the build on the left, the screens on the right.
        // DERIVED where it can be, and honest where it cannot. This panel is the only place a new player
        // is told how the game works, and it had drifted: it stated a flat "4 WOVEN SKILLS" when the
        // trait spine sells a fifth, and "3 KEYSTONE SOCKETS" when a player starts with ONE and buys the
        // others. Both numbers now come from the live loadout, so the help cannot be wrong about what
        // the player actually has.
        var build = new (string Key, string What)[]
        {
            ($"{_loadout.SkillCapacity}", "WOVEN SKILLS"),
            ("", "SOURCE x FORM x VOW"),
            ($"{_loadout.KeystoneCapacity}", "KEYSTONE SOCKETS"),
            ("", "EACH ONE A TRADE"),
            ("", "THE SPINE SELLS MORE OF BOTH"),
            ("", "FORM IS YOUR PLAYSTYLE"),
            ("", "SOURCE VS REGION"),
        };
        // Every tile on the nav rail, plus the two keys that open nothing on it. HUNT and ROSTER were
        // missing outright — ROSTER is ten characters and a quest chain, and nothing in the game told
        // anyone the key existed.
        var world = new (string Key, string What)[]
        {
            ("H", "HUNT — THE FIGHT"),
            ("C", "CHARACTER — GEAR & BAG"),
            ("V", "STATS — TRAIN"),
            ("B", "WEAVE YOUR BUILD"),
            ("W", "WORLD MAP"),
            ("F", "FORGE — CRAFT & CHESTS"),
            ("A", "WARREN — HATCH"),
            ("P", "TRAITS — THE SPINE"),
            ("R", "ROSTER — YOUR CHAMPIONS"),
            ("L", "THE EXPEDITION LOG"),
            ("F1", "CLOSE"),
        };

        Text("YOUR BUILD", 176, 200, Bone);
        for (var i = 0; i < build.Length; i++)
        {
            Text(build[i].Key, 176, 254 + i * 38, Gold);
            Text(build[i].What, 356, 254 + i * 38, Bone);
        }

        // 38px pitch, not 46: eleven rows at 46 would run to y=754 and through THE IDEA block below.
        Text("SCREENS", 1140, 200, Bone);
        for (var i = 0; i < world.Length; i++)
        {
            Text(world[i].Key, 1140, 254 + i * 38, Gold);
            Text(world[i].What, 1256, 254 + i * 38, Bone);
        }

        Text("THE IDEA:", 176, 620, Gold);
        WrapText(
            "ONE CHAMPION, YOUR BUILD — IT FIGHTS ON ITS OWN, ON EVERY SCREEN, EVEN WHILE THE GAME IS " +
            "CLOSED. EACH WAVE IT CLEARS PAYS GLEAM AT ONCE. EVERY 5TH IS A BOSS, WITH A CHANCE AT A CHEST. " +
            "WHEN IT FALLS IT RECOVERS AND PUSHES ON — NOTHING IS BANKED, NOTHING IS LOST.",
            176, 664, 1568, Bone);
        WrapText(
            $"SPEND GLEAM ON COMMAND TO STRENGTHEN YOUR CHAMPION. REACH WAVE {ConquerWaveDepth} TO CONQUER A REGION AND UNLOCK THE NEXT.",
            176, 832, 1568, Bone * 0.7f);
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
    /// Skill-tree points: one per five waves of first-time depth, summed across regions.
    /// </summary>
    /// <remarks>
    /// Three grants at the start so a new player has something to spend before their first descent
    /// ends. Nothing else feeds this — not the Warren, not conquest. See game-flow.md §3.5.
    /// </remarks>
    private int SkillPointsEarned()
    {
        var total = 3;
        foreach (var def in Regions.All) total += _world.RegionFarm(def.Id).BestDepth / 5;
        return total;
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
    private QuestProgress QuestSnapshot() => new(
        DepthByRegion: Regions.All.ToDictionary(d => d.Id, d => _world.RegionFarm(d.Id).BestDepth),
        RegionsConquered: _world.ConqueredIds.Count,
        ChestsOpened: _forge.ChestsOpened,
        RunsWithVowKept: _runsWithVowKept);

    /// <summary>
    /// Did the descent that just ended run under a Vow whose demand the build actually met?
    /// </summary>
    /// <remarks>
    /// Not "was a Vow sworn". A Vow pays nothing while its demand is unmet, and a quest that counted
    /// sworn-but-unmet Vows would hand THE OATHBOUND to a player who never engaged with the system the
    /// character exists to reward.
    /// </remarks>
    private bool VowWasKept()
    {
        var build = _loadout.ToBuild(_dust, _mastery, _characters.Active);
        var ctx = SoloBattle.DescribeBuild(build, _hunter);
        return build.Skills.Any(s => s.Vow is { } v && Weaving.IsActive(v, ctx));
    }

    private int TraitPointsEarned()
    {
        var total = _world.ConqueredIds.Count + _world.CorruptionTier;
        foreach (var def in Regions.All) total += (int)_world.RegionFarm(def.Id).MasteryLevel;
        return total;
    }

    /// <summary>The deepest wave held in any region — what the Warren's ceiling is derived from.</summary>
    private int DeepestAnywhere()
    {
        var best = _deepestEver;
        foreach (var def in Regions.All) best = Math.Max(best, _world.RegionFarm(def.Id).BestDepth);
        return best;
    }

    /// <summary>Which nav slot is lit: 0 HUNT (fight), else the open overlay.</summary>
    /// <summary>Which rail tile is lit. The Weave has no tile of its own, so it lights BUILD's.</summary>
    /// <remarks>
    /// <c>_showWeave</c> had no case here at all and fell through to 0, so the rail cheerfully reported
    /// HUNT while the player was standing on the Weave — the game's most distinctive screen, telling
    /// them they were somewhere else. It is reached from the Build overview and is part of the same
    /// activity, so it lights that tile rather than claiming one.
    /// </remarks>
    private int NavActive() =>
        _showCharacter ? 1 : _showStats ? 2 : _showBuild || _showWeave ? 3 : _showChests ? 4 :
        _showForge ? 5 : _showAutomation ? 6 : _showWorld ? 7 : _showPrestige ? 8 : _showRoster ? 9 : 0;

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
        Activity.Hunt, Activity.Gear, Activity.Stats, Activity.Build,
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

        _showCharacter = _showStats = _showBuild = _showForge = _showAutomation = _showWorld = _showPrestige = _showRoster = _showWeave = _showChests = false;
        switch (i)
        {
            case 1: _showCharacter = true; break;
            case 2: _showStats = true; break;
            case 3: _showBuild = true; break;
            case 4: _showChests = true; break;
            case 5: _showForge = true; break;
            case 6: _showAutomation = true; break;
            case 7: _showWorld = true; _mapScreen.ActiveRegion = _activeRegion; _mapScreen.SelectActive(); break;
            case 8: _showPrestige = true; break;
            case 9: _showRoster = true; break;
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
            if (_assets.Get(Nav[i].Glyph) is { } g)
                _batch.Draw(g, new Rectangle(r.Center.X - 24, r.Y + 24, 48, 48), iconTint);
            else
                _ui.Diamond(_batch, new Rectangle(r.Center.X - 20, r.Y + 28, 40, 40), on ? NavGold : NavGem * 0.75f);
            _ui.TextCenterBig(_batch, Nav[i].Label, r.Center.X, r.Bottom - 34,
                              !unlocked ? NavLabel * 0.35f : on ? NavGold : NavLabel * 0.9f, UiTypography.NavigationLabel);

            // The price, on the tile, so the rail teaches the progression without being clicked. Hover
            // only — nine requirement lines drawn permanently is the wall this pass exists to remove.
            if (!unlocked && hover)
                _ui.TextCenterBig(_batch, _ui.ShortenBig(Unlocks.Requirement(NavActivity[i]), NavRailWidth - 24, UiTypography.Secondary),
                                  r.Center.X, r.Bottom - 16, NavGem * 0.8f, UiTypography.Secondary);

            // UNOPENED CHESTS, as a count on the FORGE tile. A player told us the chest-opening feature
            // felt hidden in the game, and it was: chests live three clicks deep — FORGE, then the
            // SALVAGE mode, then a small toolbar row — with nothing anywhere else in the game saying one
            // is waiting. The most valuable thing the game gives you should not be the hardest to find.
            if (Nav[i].Label == "FORGE" && _forge is not null && _forge.UnopenedChests.Count > 0)
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
