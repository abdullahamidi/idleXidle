using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Evolution;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using ResonanceHunter.Core.Prestige;

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
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);
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
    private int _wheel;    // mouse-wheel notches this frame, latched alongside the click

    // ── Game state ────────────────────────────────────────────────────────────────────────────
    private readonly Hunter _hunter = new();
    private readonly Random _rng = new();

    // The world of regions to conquer. The "active" region is what you hunt in and farm right now.
    private readonly World _world = new();
    private string _activeRegion = VerdantHollow.RegionId;
    private Region _region = null!;   // == _world.RegionFarm(_activeRegion); reassigned on region change
    private bool _showWorld;
    private int _worldCursor;
    private string _conquerMsg = "";

    private int _regionProgression;

    private RigSpikeScreen _rigSpike = null!;
    private bool _showSpike;
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
    private BuildScreen _buildScreen = null!;
    private bool _showBuild;

    // The character sheet — equipment + stat training + a live damage bench, apart from the fight it feeds.
    private CharacterScreen _character = null!;
    private bool _showCharacter;
    private StatsScreen _stats = null!;
    private bool _showStats;

    private AutomationScreen _automation = null!;
    private bool _showAutomation;

    // ── Memory Dust prestige (Full Vision). NOTHING RESETS — Dust accrues from mastery. ───────
    private PrestigeScreen _prestige = null!;
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
        if (_showSpike) { _ui.Background(_batch, "__none__"); return; } // void — it's a tech demo
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
        _highestMasteryAwarded = save.HighestMasteryAwarded;

        // The woven build. Only overwrite the Starter when the save actually carries one — a pre-solo
        // save has an empty list, and restoring THAT would strip a returning player down to no skills.
        if (save.WovenSkills.Count > 0)
            _loadout.Restore(
                save.WovenSkills.Select(s => (s.Source, s.Form, s.VowId)), save.SocketedKeystoneIds);

        _deepestEver = save.MasteryEarned;         // stored the deepest-ever; Earned re-derives from it
        _mastery.RestoreTaken(save.MasteryTaken);

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
                _bootMessage = $"WELCOME BACK — {span} AWAY\n+{Abbrev((long)champOffline + gleam)} GLEAM · {kills} KILLS · {cores} CORES";
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
    private Dictionary<GearSlot, string?> _pendingWorn = new();
    private int _pendingCores;
    private AutomationYield? _pendingOfflineYield;

    private void Save()
    {
        // A screenshot run seeds throwaway fixed-id items; it must NEVER write them into the player's real
        // save. Autosave and conquest both call this, so the guard lives here, at the one write site.
        if (Environment.GetEnvironmentVariable("RH_SHOT") is not null) return;

        var save = SaveSystem.Capture(
            _hunter, _region, _automation.Roster, _forge.Inventory, _automation.Cores, SaveFile.NowMs,
            _dust, _highestMasteryAwarded, _world, _activeRegion) with
        {
            // The build rides along via `with`, so Core's Capture stays unaware of the Game-layer loadout.
            WovenSkills = _loadout.SaveSkills()
                .Select(s => new SavedSkill { Source = s.Source, Form = s.Form, VowId = s.VowId }).ToList(),
            SocketedKeystoneIds = _loadout.KeystoneIds.ToList(),
            MasteryTaken = _mastery.Taken.ToList(),
            MasteryEarned = _deepestEver,          // stored as deepest-ever; Earned is re-derived on load
            ChampionGleamRate = _champGleamRate,
            // Unopened chests ride along too — a boss's drop must survive a reload, opened or not.
            UnopenedChests = _forge.UnopenedChests
                .Select(c => new SavedChest { Rarity = (int)c.Rarity, Tier = c.Tier, Element = c.Element?.ToString() })
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
        _rigSpike = new RigSpikeScreen(GraphicsDevice, _pixel);
        _automation = new AutomationScreen(_ui);
        _forge = new ForgeScreen(_ui);
        _prestige = new PrestigeScreen(_ui, _dust);
        _expedition = new SoloExpeditionScreen(_ui);
        _buildScreen = new BuildScreen(_ui);
        _character = new CharacterScreen(_ui, _forge);
        _stats = new StatsScreen(_ui);
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
        if (_pendingOfflineYield is not null) _automation.ReportOffline(_pendingOfflineYield);
        _automation.Cores += _pendingCores;
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
                or "banked" or "lootforge" or "settings" or "settingsfull" or "vow" or "hybrid" or "build" or "character" or "stats")
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
                    _forge.AddLoot(seed);
                    _forge.ActiveForms = _loadout.Skills.Select(s => s.Form).ToList();
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
                }

                if (sm == "build")
                {
                    _showBuild = true;
                    // Pose a partly-walked tree so the shot shows taken/takeable/bridge states + RESET.
                    _mastery.SetEarned(8);
                    _mastery.Take("strike_1");
                    _mastery.Take("strike_2");
                    _mastery.Take("bridge_strike_volley");
                }

                if (sm == "character")
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
                }

                if (sm == "stats")
                {
                    _showStats = true;
                    _stats.Loadout = _loadout;
                    _stats.Mastery = _mastery;
                    _stats.Tree = _dust;
                    _hunter.AddGleam(20000);
                    for (var i = 0; i < 12; i++) _hunter.Train(HunterStat.AttackPower);
                    for (var i = 0; i < 6; i++) _hunter.Train(HunterStat.CriticalChance);
                    for (var i = 0; i < 5; i++) _hunter.Train(HunterStat.Defense);
                }

                if (sm == "fight")
                {
                    _expedition.Loadout = _loadout;
                    _expedition.Tree = _dust;
                    _expedition.Mastery = _mastery;
                    // Pose the new fight-screen UX: the welcome-back toast, and the "you have things to do"
                    // attention cue (a waiting chest + unspent mastery points).
                    _bootMessage = "WELCOME BACK — 18 MIN AWAY\n+140 GLEAM · 12 KILLS · 0 CORES";
                    _bootColor = Gold; _bootTimer = 7f;
                    _forge.AddChest(new Chest { Rarity = Rarity.Epic, Tier = 8, Element = Source.Nature });
                    _mastery.SetEarned(3);
                    // A DELIBERATELY beefy enemy: a real wave-1 fight is over in ~1.5s, so a shot taken a
                    // second in only ever caught the aftermath — useless for verifying the fight itself.
                    // This one stays standing long enough to capture skills firing.
                    _expedition.DevStart(_hunter, 1400f, 9f);
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
                if (sm is "forge" or "hybrid")
                {
                    _showForge = true;
                    // Seed some loot so the Forge screen can be audited with content.
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
                    _forge.AddLoot(loot);

                    // Land the cursor on a LEGENDARY: Commons carry no enchantment by design, so a shot
                    // of the default selection cannot show the enchantment row at all.
                    _forge.DevSelect(4);

                    // Queue a cross-family trio so the HYBRID recipe row can be screenshotted.
                    if (sm == "hybrid") _forge.DevQueueHybrid();
                }
                if (sm == "farm")
                {
                    _showAutomation = true;
                    _automation.DevPopulate(_region);
                }
                if (sm == "dust")
                {
                    _showPrestige = true;
                    _dust.AwardFromMastery(400);
                    // Light a handful of stars so the screenshot shows lit/buyable/locked states.
                    foreach (var u in _dust.All.OrderBy(u => u.Requires.Count).ThenBy(u => u.Cost).Take(4))
                        _dust.Purchase(u.Id);
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
        if (Pressed(Keys.F9)) _showSpike = !_showSpike;
        if (Pressed(Keys.F6)) _expedition.DevForceBoss = !_expedition.DevForceBoss;   // dev: force the Crystal Lich boss render (Rev 4 §12)
        if (Pressed(Keys.F7)) { _expedition.DevBossDebug = !_expedition.DevBossDebug; _character.DevGearDebug = !_character.DevGearDebug; }   // dev layout overlays
        if (Pressed(Keys.F1)) _showHelp = !_showHelp;
        if (Pressed(Keys.F10)) _showSettings = !_showSettings;

        // A modal eats the frame's input, but NOT the frame. The farms above still tick and the
        // autosave above still fires — an idle game does not pause because you opened a menu. What it
        // must not do is let the hotkeys and buttons underneath the panel keep responding: without
        // this, clicking FULLSCREEN also presses whatever the panel happens to be covering.
        if (_showSettings) { Latch(gameTime); return; }

        HandleNavClick();   // a click on the shared hex nav works from any screen

        if (Pressed(Keys.A)) { _showAutomation = !_showAutomation; _showForge = false; _showPrestige = false; _showWorld = false; _showBuild = false; _showCharacter = false; _showStats = false; }
        if (Pressed(Keys.P)) { _showPrestige = !_showPrestige; _showAutomation = false; _showForge = false; _showWorld = false; _showBuild = false; _showCharacter = false; _showStats = false; }
        if (Pressed(Keys.W)) { _showWorld = !_showWorld; _showAutomation = false; _showForge = false; _showPrestige = false; _showBuild = false; _showCharacter = false; _showStats = false; }

        // B — WEAVE YOUR BUILD. The whole decision layer of the solo model: four skills, three sockets.
        if (Pressed(Keys.B)) { _showBuild = !_showBuild; _showAutomation = false; _showForge = false; _showPrestige = false; _showWorld = false; _showCharacter = false; _showStats = false; }

        // C — CHARACTER. Equipment and the bag — the gear half of the sheet.
        if (Pressed(Keys.C)) { _showCharacter = !_showCharacter; _showStats = false; _showBuild = false; _showAutomation = false; _showForge = false; _showPrestige = false; _showWorld = false; }

        // V — STATS. Commander training, spelled out, on its own page apart from the inventory.
        if (Pressed(Keys.V)) { _showStats = !_showStats; _showCharacter = false; _showBuild = false; _showAutomation = false; _showForge = false; _showPrestige = false; _showWorld = false; }

        // THE CHAMPION FIGHTS EVERYWHERE. Ticked here, before any overlay can early-return, so a run
        // keeps clearing waves and paying out while you're in the Forge, the tree, or another region's
        // menu — the whole point of an idle game. Only COMMAND clicks are gated to the combat view (the
        // `interactive` flag), so a click in the Forge never falls through into the fight.
        _regionProgression = Math.Clamp((int)_region.MasteryLevel + (_region.MasteryLevel > 0 ? 1 : 0), 0, 4);
        var watchingFight = !(_showWorld || _showPrestige || _showForge || _showAutomation || _showBuild
            || _showCharacter || _showStats || _showSpike || _showHelp || _showSettings);
        UpdateExpedition(gameTime, watchingFight);

        if (_showWorld) { UpdateWorld(); Latch(gameTime); return; }

        if (_showBuild)
        {
            _buildScreen.Loadout = _loadout;
            _buildScreen.Mastery = _mastery;
            _buildScreen.Update(_keys, _prevKeys, CanvasMouse, MouseClicked, _dust);
            if (_buildScreen.Dirty) { _buildScreen.ClearDirty(); Save(); }
            Latch(gameTime);
            return;
        }

        if (_showCharacter)
        {
            _character.Loadout = _loadout;
            _character.Mastery = _mastery;
            _character.Tree = _dust;
            _character.Update(_keys, _prevKeys, CanvasMouse, MouseClicked, MouseWheel, _hunter);
            if (_character.Dirty) { _character.ClearDirty(); Save(); }
            Latch(gameTime);
            return;
        }

        if (_showStats)
        {
            _stats.Loadout = _loadout;
            _stats.Mastery = _mastery;
            _stats.Tree = _dust;
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

        if (_showPrestige) { _prestige.Update(_keys, CanvasMouse, MouseClicked, _dust); Latch(gameTime); return; }

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

        if (_showSpike) { _rigSpike.Update(gameTime, _keys); Latch(gameTime); return; }

        if (_showForge)
        {
            // Keep the Forge told which Forms the build runs, so it can flag live combos here too (not
            // only when arrived at from the fight).
            _forge.ActiveForms = _loadout.Skills.Select(s => s.Form).ToList();
            _forge.Update(gameTime, _keys, CanvasMouse, MouseClicked, MouseWheel, _hunter);
            Latch(gameTime);
            return;
        }

        if (_showAutomation)
        {
            // The farm runs whether or not you are watching it — that is the point of an idle game.
            _automation.Update(gameTime, _keys, CanvasMouse, MouseClicked, MouseWheel, _region, _hunter);
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
    private static int RegionLadder(string regionId)
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
    /// <param name="interactive">
    /// True only when the combat screen is the one on top — so the champion fights on EVERY screen and
    /// while the game is minimised (that is what "idle" means), but the COMMAND clicks only register when
    /// you are actually looking at the fight and not clicking through it into the Forge.
    /// </param>
    private void UpdateExpedition(GameTime gameTime, bool interactive)
    {
        var def = Regions.Get(_activeRegion);
        _expedition.EnemySource = def.Theme;          // region element → the Source matchup
        _expedition.RegionId = def.Id;                // region id → the boss creature (boss_<region>) on boss waves
        _expedition.EnemyBias = def.CombatBias;       // region character → the enemy's bite tempo (feel + TRAP synergy)
        _expedition.Loadout = _loadout;               // the player's build, handed over live…
        _expedition.Tree = _dust;                     // …powered by the Dust tree's passive nodes
        _expedition.Mastery = _mastery;               // …and the mastery tree (affinity + node bonuses)
        _expedition.ChestCount = _forge.UnopenedChests.Count;   // drives the fight screen's "go open a chest" nudge
        _expedition.IdleGleamRate = _champGleamRate;            // gleam/sec the champion earns idle → HUNT idle panel

        _forge.FeedTarget = _automation.Selected;
        _forge.ActiveForms = _loadout.Skills.Select(s => s.Form).ToList();   // so the Forge can flag live combos
        _forge.Tuning = ForgeTuning.Default with
        {
            DismantleReturnRate = DustEffects.DismantleRate(_dust, ForgeTuning.Default.DismantleReturnRate),
        };
        // A chest's rolled loot honours the same Dust filters a boss drop did — auto-sell floor and the
        // tireless-forge auto-merge — now applied at OPEN, since that is where a chest's items land.
        _forge.AutoSellFloor = DustEffects.AutoSellAtOrBelow(_dust);
        _forge.AutoMergeOnOpen = DustEffects.AutoMergeAfterRuns(_dust);
        var scale = CorruptionScaling.HealthMultiplier(_world.CorruptionTier);

        _expedition.Update(
            gameTime, _keys, CanvasMouse, MouseClicked && interactive, MouseWheel, _hunter,
            // Every region is a rung up the ladder for the champion, not just a new element — so reaching
            // the sixth region is a real climb, and "more regions" is more progression, not more of the same.
            enemyBaseHealth: 110f * (1f + 0.35f * _regionProgression) * (1f + 0.35f * RegionLadder(_activeRegion)) * scale,
            enemyBaseDamage: 9f * (1f + 0.20f * _regionProgression) * (1f + 0.25f * RegionLadder(_activeRegion)) * scale);

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

            // MATERIALS FROM MONSTERS — a small trickle every wave, deeper waves paying a little more. The
            // steady drip; chests and dismantling are the bulk. (The farm is no longer a material source.)
            _hunter.AddMaterials(1 + r.Wave / 20);

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

        // MASTERY POINTS — the currency you spend walking the mastery tree, DERIVED so it can never
        // double-count across reloads: three to start (so a new character can take a node at once), plus
        // one per five waves of your deepest-ever run, plus five per region conquered. Pure function of
        // progress, recomputed every frame; only _deepestEver needs saving.
        _deepestEver = Math.Max(_deepestEver, _expedition.Deepest);
        _mastery.SetEarned(3 + _deepestEver / 5 + _world.ConqueredIds.Count * 5);

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
        var lootTier = 1 + RegionLadder(_activeRegion) + _regionProgression + depthTier
                       + CorruptionScaling.TierBonus(_world.CorruptionTier);
        if (_rng.NextDouble() >= Chests.DropChance(lootTier)) return false;         // most bosses give nothing
        _forge.AddChest(Chests.RollDrop(lootTier, def.Theme, _rng));
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
        var r = new Rectangle(590, 165, 740, 82);
        _ui.Fill(_batch, r, new Color(0x10, 0x0D, 0x16) * (0.9f * fade));
        _ui.Fill(_batch, new Rectangle(r.X, r.Y, r.Width, 4), _bootColor * (0.65f * fade));
        _ui.Fill(_batch, new Rectangle(r.X, r.Bottom - 4, r.Width, 4), _bootColor * (0.65f * fade));
        _ui.TextCenterBig(_batch, parts[0], r.Center.X, r.Y + 14, _bootColor * fade, UiTypography.OverlayTitle);
        if (parts.Length > 1) _ui.TextCenterBig(_batch, parts[1], r.Center.X, r.Y + 46, Bone * fade, UiTypography.OverlayBody);
    }

    private bool Pressed(Keys k) => _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

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
    private bool MouseClicked => _clicked && !_showSettings;


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

        // Batch B — the active screen. ScreenScale() is 4 today; converted screens will return 1.
        BeginCanvas(ScreenScale());
        if (_showSpike) _rigSpike.Draw(_batch);
        else if (_showForge) _forge.Draw(_batch, _hunter, CanvasMouse, MouseClicked);
        else if (_showWorld) DrawWorld();
        else if (_showPrestige) _prestige.Draw(_batch, _dust, CanvasMouse, MouseClicked);
        else if (_showAutomation) _automation.Draw(_batch, _region, _hunter, CanvasMouse, MouseClicked);
        else if (_showBuild) _buildScreen.Draw(_batch, CanvasMouse, _dust);
        else if (_showCharacter) _character.Draw(_batch, CanvasMouse, _hunter);
        else if (_showStats) _stats.Draw(_batch, CanvasMouse, _hunter);
        else _expedition.Draw(_batch, CanvasMouse, MouseClicked, Regions.Get(_activeRegion).Name, EnemyArtFor(_activeRegion), _bootTimer > 0f);
        _batch.End();

        // Batch C — the shared overlays (pills, nav, help/settings, boot toast), authored in true 1920 coords.
        BeginCanvas(1);
        DrawCurrencyPills();   // shared Gleam / Dust / Materials row, top-right of every screen
        DrawHexNav();   // the shared nav bar, over every screen

        if (_showHelp) DrawHelp();
        if (_showSettings) DrawSettings();

        DrawBootToast();

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
            _ui.Panel(_batch, r, gold: on);
            _ui.TextCenter(_batch, modeNames[i], r.Center.X, r.Center.Y - 12, on ? new Color(0x1E, 0x14, 0x04) : Bone);
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
            _ui.TextCenter(_batch, label, r.Center.X, r.Center.Y - 12, on ? new Color(0x1E, 0x14, 0x04) : windowed ? Bone : Slate);
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
        right = _ui.Pill(_batch, right, 16, null, new Color(0x9A, 0xC0, 0x88), Abbrev(_hunter.MaterialOf(Material.Scrap)), "", new Color(0x9A, 0xC0, 0x88)) - 20;
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
    private static readonly Rectangle[] RegionNodes =
    [
        // A serpentine path across the map: the lower row runs left-to-right, then the journey climbs and
        // the upper row runs right-to-left, so six regions fit and the route reads as a single winding climb.
        new(96, 736, 424, 144),    // region 0   (×4 of 480-space 24,184,106,36)
        new(748, 736, 424, 144),   // region 1
        new(1400, 736, 424, 144),  // region 2
        new(1400, 440, 424, 144),  // region 3
        new(748, 440, 424, 144),   // region 4
        new(96, 440, 424, 144),    // region 5
    ];

    private void UpdateWorld()
    {
        var n = Math.Min(Regions.All.Count, RegionNodes.Length);
        if (Pressed(Keys.Left)) _worldCursor = (_worldCursor - 1 + n) % n;
        if (Pressed(Keys.Right)) _worldCursor = (_worldCursor + 1) % n;
        if (Pressed(Keys.Enter)) SelectWorldNode(_worldCursor);
        if (Pressed(Keys.D)) DeepenCorruption(); // keyboard/gamepad path for the deepen button

        for (var i = 0; i < n; i++)
            if (UiKit.ClickedIn(RegionNodes[i], ChromeMouse, MouseClicked)) { _worldCursor = i; SelectWorldNode(i); }
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

    private void SelectWorldNode(int i)
    {
        var def = Regions.All[i];
        if (!_world.IsUnlocked(def.Id)) { _conquerMsg = $"{def.Name} IS LOCKED — CONQUER THE PREVIOUS REGION."; return; }
        SetActiveRegion(def.Id);
        _showWorld = false;
    }

    private void DrawWorld()
    {
        var title = _world.CorruptionTier > 0 ? $"THE WORLD — CORRUPTION {_world.CorruptionTier}" : "THE WORLD";
        _ui.Title(_batch, title);
        var n = Math.Min(Regions.All.Count, RegionNodes.Length);

        // The winding path between nodes. Drawn as an L (a horizontal run then a vertical one) so the
        // serpentine's ROW-CHANGE link — where two nodes share an X — is visible; the old single horizontal
        // segment collapsed to zero width there, leaving the two rows looking disconnected.
        for (var i = 0; i < n - 1; i++)
        {
            var a = RegionNodes[i].Center;
            var bpt = RegionNodes[i + 1].Center;
            var color = _world.IsConquered(Regions.All[i].Id) ? Gold : Dim;
            if (a.X != bpt.X)
                _batch.Draw(_pixel, new Rectangle(Math.Min(a.X, bpt.X), a.Y, Math.Abs(bpt.X - a.X), 8), color);
            if (a.Y != bpt.Y)
                _batch.Draw(_pixel, new Rectangle(bpt.X, Math.Min(a.Y, bpt.Y), 8, Math.Abs(bpt.Y - a.Y)), color);
        }

        for (var i = 0; i < n; i++)
        {
            var def = Regions.All[i];
            var node = RegionNodes[i];
            var unlocked = _world.IsUnlocked(def.Id);
            var conquered = _world.IsConquered(def.Id);
            var active = def.Id == _activeRegion;

            // Region emblem, a signpost above the node — dimmed while the region is still locked.
            var emblem = def.Id switch
            {
                "cinderworks" => "icon_region_cinderworks",
                "umbral_reach" => "icon_region_umbral",
                _ => "icon_region_verdant",
            };
            _ui.Icon(_batch, emblem, new Rectangle(node.Center.X - 36, node.Y - 84, 72, 72),
                unlocked ? Color.White : new Color(0x55, 0x55, 0x60));

            _ui.Panel(_batch, node, gold: active || conquered);
            // Gold plate (active/conquered) takes near-black; the dark plates take light text.
            _ui.TextCenter(_batch, def.Name, node.Center.X, node.Y + 24,
                active || conquered ? new Color(0x20, 0x16, 0x06) : unlocked ? Bone : Slate);

            var status = conquered ? "CONQUERED" : !unlocked ? "LOCKED" : active ? "HERE NOW" : "AVAILABLE";
            _ui.TextCenter(_batch, status, node.Center.X, node.Y + 72, conquered ? Gold : !unlocked ? Dim : active ? Gold : Bone);

            // A small mastery pip-track for conquered/active regions.
            if (unlocked)
            {
                var farm = _world.RegionFarm(def.Id);
                _ui.Bar(_batch, node.X + 32, node.Bottom - 32, node.Width - 64, 16, (int)farm.MasteryLevel / 3f, Gold);
            }

            if (i == _worldCursor) Reticle(new Rectangle(node.X - 12, node.Y - 12, node.Width + 24, node.Height + 24), Bone);
        }

        // In the clear band between the two node rows, so it never overlaps a region panel.
        if (_conquerMsg.Length > 0) _ui.TextCenter(_batch, _conquerMsg, 960, 632, Gold);

        // Endgame: once the whole world is conquered, offer to deepen the corruption — tougher fights,
        // richer Memory Dust, forever. A real button (works by mouse) with a keyboard shortcut (D).
        if (_world.CanDeepenCorruption)
        {
            var btn = new Rectangle(560, 672, 800, 56);   // in the inter-row band, clear of the node panels
            if (_ui.Button(_batch, btn, $"DEEPEN THE CORRUPTION  {_world.CorruptionTier}→{_world.CorruptionTier + 1}  [D]",
                    ChromeMouse, MouseClicked))
                DeepenCorruption();
        }

        // One footer line, clear of the nav shelf. Was two rows at y=250/260 — both buried under the nav.
        // Conquest is measured in waves held (ConquerWaveDepth); the nav bar already teaches W  BACK.
        _ui.TextCenter(_batch, $"CLICK A REGION TO TRAVEL  ·  HOLD {ConquerWaveDepth} WAVES TO CONQUER IT AND UNLOCK THE NEXT", 960, 912, Slate);
    }

    private void DrawHelp()
    {
        // 1920-space (scale-1 chrome): every position/size ×4 of its old 480-space value.
        Fill(new Rectangle(80, 80, 1760, 920), Bone);
        Fill(new Rectangle(84, 84, 1752, 912), VoidInk);

        Text("RESONANCE HUNTER — CONTROLS", 120, 112, Gold);

        // Two columns: the build on the left, the screens on the right.
        var build = new (string Key, string What)[]
        {
            ("4", "WOVEN SKILLS"),
            ("", "SOURCE x FORM x VOW"),
            ("3", "KEYSTONE SOCKETS"),
            ("", "EACH ONE A TRADE"),
            ("", "FORM IS YOUR"),
            ("", "  PLAYSTYLE"),
            ("", "SOURCE VS REGION"),
        };
        var world = new (string Key, string What)[]
        {
            ("C", "CHARACTER — GEAR & BAG"),
            ("V", "STATS — TRAIN"),
            ("B", "WEAVE YOUR BUILD"),
            ("W", "WORLD MAP"),
            ("F", "FORGE — CRAFT LOOT"),
            ("A", "WARREN — HATCH"),
            ("P", "MEMORY DUST"),
            ("F1", "CLOSE"),
        };

        Text("YOUR BUILD", 120, 176, Slate);
        for (var i = 0; i < build.Length; i++)
        {
            Text(build[i].Key, 120, 232 + i * 48, Gold);
            Text(build[i].What, 312, 232 + i * 48, Bone);
        }

        Text("SCREENS", 1200, 176, Slate);
        for (var i = 0; i < world.Length; i++)
        {
            Text(world[i].Key, 1200, 232 + i * 48, Gold);
            Text(world[i].What, 1320, 232 + i * 48, Bone);
        }

        Text("THE IDEA:", 120, 600, Gold);
        WrapText(
            "ONE CHAMPION, YOUR BUILD — IT FIGHTS ON ITS OWN, ON EVERY SCREEN, EVEN WHILE THE GAME IS " +
            "CLOSED. EACH WAVE IT CLEARS PAYS GLEAM AT ONCE. EVERY 5TH IS A BOSS, WITH A CHANCE AT A CHEST. " +
            "WHEN IT FALLS IT RECOVERS AND PUSHES ON — NOTHING IS BANKED, NOTHING IS LOST.",
            120, 648, 1640, Bone);
        WrapText(
            $"SPEND GLEAM ON COMMAND TO STRENGTHEN YOUR CHAMPION. REACH WAVE {ConquerWaveDepth} TO CONQUER A REGION AND UNLOCK THE NEXT.",
            120, 840, 1640, Slate);
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
        ("HUNT", 'H', "nav_hunt_128"), ("GEAR", 'C', "nav_inventory_128"),
        ("STATS", 'V', "state_resonance_128"), ("BUILD", 'B', "state_mastery_128"),
        ("FORGE", 'F', "nav_forge_128"), ("WARREN", 'A', "nav_warren_128"),
        ("MAP", 'W', "nav_relics_128"), ("DUST", 'P', "state_prestige_128"),
    };

    private static readonly Color NavGold = new(0xF0, 0xB2, 0x4A);
    private static readonly Color NavGem = new(0x5F, 0xE0, 0xC8);
    private static readonly Color NavIdle = new(0x1A, 0x14, 0x30);
    private static readonly Color NavHover = new(0x2C, 0x25, 0x44);
    private static readonly Color NavLabel = new(0x8A, 0x82, 0xA0);

    /// <summary>Which nav slot is lit: 0 HUNT (fight), else the open overlay.</summary>
    private int NavActive() =>
        _showCharacter ? 1 : _showStats ? 2 : _showBuild ? 3 : _showForge ? 4 :
        _showAutomation ? 5 : _showWorld ? 6 : _showPrestige ? 7 : 0;

    private void OpenNav(int i)
    {
        _showCharacter = _showStats = _showBuild = _showForge = _showAutomation = _showWorld = _showPrestige = false;
        switch (i)
        {
            case 1: _showCharacter = true; break;
            case 2: _showStats = true; break;
            case 3: _showBuild = true; break;
            case 4: _showForge = true; break;
            case 5: _showAutomation = true; break;
            case 6: _showWorld = true; break;
            case 7: _showPrestige = true; break;
            // case 0 HUNT: everything cleared above → back to the fight.
        }
    }

    private static Rectangle NavHexRect(int i)   // now a rectangular TILE (guide: package_01 nav tiles)
    {
        // 1920-space (scale-1 chrome): cell/tile/top ×4 of the old 480 values; the canvas centre is 1920/2.
        // Rev 4 §22.4: eight equal 240px sections across the 1920 rail.
        return new Rectangle(i * 240, 934, 240, 146);
    }

    private void DrawHexNav()
    {
        if (_showSettings || _showHelp || _showSpike) return;   // a modal owns the frame

        // A dark shelf so the bar seats cleanly over whatever screen sits behind it. Nearly opaque and
        // starting a hair above the hexes, so the scene behind can't show through and clip their tops.
        // 1920-space (scale-1 chrome): shelf top 234→936, full 1920 width, 36→144 tall.
        // Rev 4 §22: ONE shared QUIET rail — a dark background + thin dividers, NOT an ornate panel per item.
        // Only the active item gets ornate emphasis (ui_tab_active + purple tint, full-contrast icon/label);
        // inactive items are a quiet glyph + label at ~75% opacity.
        _ui.Fill(_batch, new Rectangle(0, 934, 1920, 146), new Color(0x0C, 0x09, 0x16) * 0.96f);
        _ui.Fill(_batch, new Rectangle(0, 934, 1920, 3), NavGem * 0.4f);   // a thin top seam

        var active = NavActive();
        for (var i = 0; i < Nav.Length; i++)
        {
            var r = NavHexRect(i);
            var on = i == active;
            var hover = r.Contains(ChromeMouse);
            if (i > 0) _ui.Fill(_batch, new Rectangle(r.X, r.Y + 26, 2, r.Height - 52), new Color(0x22, 0x1C, 0x30));

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

            var iconTint = on ? Color.White : Color.White * 0.75f;
            if (_assets.Get(Nav[i].Glyph) is { } g)
                _batch.Draw(g, new Rectangle(r.Center.X - 26, r.Y + 28, 52, 52), iconTint);
            else
                _ui.Diamond(_batch, new Rectangle(r.Center.X - 22, r.Y + 32, 44, 44), on ? NavGold : NavGem * 0.75f);
            _ui.TextCenterBig(_batch, Nav[i].Label, r.Center.X, r.Bottom - 44, on ? NavGold : NavLabel * 0.9f, UiTypography.NavigationLabel);
        }
    }

    private void HandleNavClick()
    {
        if (!MouseClicked || _showSettings || _showHelp || _showSpike) return;
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
