using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Animation;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Prestige;
using ResonanceHunter.Core.Progression;

namespace ResonanceHunter.Client;

/// <summary>
/// The idle heart of the game: your champion clears the region ON ITS OWN, forever, and you watch.
/// </summary>
/// <remarks>
/// <para>
/// There is no bank-or-push and no muster gate any more. The champion fights wave after wave
/// automatically; every wave it clears pays out on the spot; when it finally falls it picks itself up
/// and starts the region again. The player never clicks a fight. All the decisions live elsewhere — the
/// BUILD (press B), the Forge, the tree — and this screen is where you see whether they were good ones.
/// </para>
/// <para>
/// It drives <see cref="SoloExpedition"/> on a timer and hands each cleared wave's haul to the host via a
/// small reward queue, so the host credits gleam, cores and loot as they are earned rather than at a
/// run's end that no longer exists.
/// </para>
/// </remarks>
public sealed class SoloExpeditionScreen
{
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Ember = new(0xD8, 0x48, 0x3A);
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);
    private static readonly Color Dim = new(0x2C, 0x2C, 0x36);
    private static readonly Color Bloom = new(0x8A, 0x6A, 0xB0);
    private static readonly Color Steel = new(0x7A, 0x9A, 0xC0);
    private static readonly Color Verdant = new(0x5A, 0x9A, 0x4A);
    private static readonly Color Shadow = new(0x10, 0x0E, 0x14);
    private static readonly Color PanelBg = new(0x14, 0x11, 0x1A, 0xC8);
    private static readonly Color GroundShade = new(0x00, 0x00, 0x00, 0x64);   // soft translucent contact shadow
    private static readonly Color OnScene = UiKit.Vellum;

    /// <summary>The ground line every actor stands on. All actor boxes derive from it.</summary>
    /// <remarks>
    /// Was 735 and, despite the comment, referenced nowhere — the boxes below hardcoded "735 - h"
    /// instead, so the two could drift apart. It is now the single source of truth.
    /// Moved to 890: the regenerated arena backdrops put their walkable floor in the lower band, and
    /// the host nav rail is an OPAQUE bar at (0, 934, 1920, 146) (Game1.cs:1852), so anything below
    /// 934 was hidden outright. The nav bar has since moved to a VERTICAL rail down the left edge
    /// (Game1.NavHexRect), which frees that whole 146 px band back to the stage — so the ground line
    /// drops to 1000, giving the actors the full height of the arena to stand in.
    /// </remarks>
    private const int GroundY = 1000;
    // A wave is resolved instantly then REPLAYED at this speed. It was 3.5x with no gap between waves, so
    // the whole run blurred past — playtest: "waves flow too fast". Slowed to a watchable pace, and a short
    // BREATH now sits between waves so each clear reads as its own beat. The champion's own skill rate
    // (TEMPO, the Focus slot) still speeds combat back up on top of this, so the upgrade is now visible.
    private const float PlaybackSpeed = 2.0f;   // default battle speed
    private float _speedMul = PlaybackSpeed;     // player-adjustable via the HUD's BATTLE SPEED buttons (x1/x2/x4/x8)
    private static readonly float[] SpeedSteps = { 1f, 2f, 4f, 8f };
    private const float WaveBreakSeconds = 0.8f;   // the pause after a clear, before the next enemy is fought
    private const float DownedSeconds = 1.6f;   // the recovery beat before the champion tries again

    /// <summary>How long the fall is left uncovered before the run report slides over it.</summary>
    /// <remarks>
    /// Long enough to read as a death, short enough that a player who has seen a hundred of them is not
    /// waiting. It is taken OUT of the existing downed beat rather than added to it, so the loop keeps
    /// exactly the rhythm it had.
    /// </remarks>
    private const float DeathBeatSeconds = 0.55f;

    // Spec §12: the hunter is the DOMINANT figure, bottom-centred at (560,735), ~390px tall; the enemy grounds
    // at the front-melee anchor (1110,750), smaller (~250px) so the hunter reads as the focal point. The old
    // layout over-sized the enemy/boss ("boss too big, masked in a box") — the spec's ranges fix that.
    // Shifted right (centre 560 -> 700) to clear the vertical control rail down the left edge, and
    // enlarged so the figure holds the larger stage.
    // 430, not 500. The rig fitted its ASSEMBLED bounds into this box and those bounds carried the
    // rig's own layout spacing, so the figure came out well short of the box it was given. A sprite
    // strip has no such slack — the generated frames are trimmed to their content, so the character
    // renders at exactly the box height and the champion suddenly stood a head taller than before,
    // dominating a stage it shares with enemies less than half that size.
    private static readonly Rectangle ChampBox = new(760 - 200, GroundY - 430, 400, 430);
    // Rev 3 §16.1: one normal enemy bottom-centred at (1160,735), visible ~320px (range 280–360). A boss is
    // drawn far larger from its own anchor (see the draw), so this box is the NORMAL-enemy size only.
    private static readonly Rectangle EnemyBox = new(1320 - 218, GroundY - 440, 436, 440);
    // Rev 5 boss presentation metadata — measured from the crystal_lich_idle strip's frame 0 (1024²), shared
    // across frames (Option A). Body = the central figure (torso/head/robe), EXCLUDING the wings, staff, and a
    // top-of-frame BLEED-STREAK defect (rows 0..~305) that is trimmed via SrcTop and REPORTED as an asset
    // issue (§19.3) — never hidden by shifting the body. Grounding, scale, centring, and the bar all key off
    // BodyBounds, so the wings/staff may extend beyond the 540px body and are clipped by the arena.
    private readonly record struct BossMeta(int SrcTop, int BodyX, int BodyY, int BodyW, int BodyH,
        int FullX, int FullY, int FullW, int FullH, string Name);
    private static readonly Dictionary<string, BossMeta> BossMetaFor = new()
    {
        // BodyX corrected LEFT of the frame-0 estimate (388): the dense body (robe/torso) sat consistently
        // left of anchor across frames, so the frame-0 measurement was biased right. Centres the figure.
        ["crystal_lich"] = new(SrcTop: 305, BodyX: 296, BodyY: 430, BodyW: 214, BodyH: 500,
            FullX: 240, FullY: 350, FullW: 648, FullH: 580, Name: "CRYSTAL LICH"),
    };
    private static readonly BossMeta DefaultBossMeta = new(0, 300, 200, 424, 640, 200, 120, 624, 780, "BOSS");
    private const int BossTargetBodyHeight = 540;   // §6/§25: rendered BODY height (wings extend beyond)
    // Pulled in from x=1360: the boss is far wider than an ordinary creature, and anchored that far right
    // its wing ran into the arena's scissor edge at 1554 and read as sliced off behind the side panels.
    private static readonly Point BossAnchor = new(1230, GroundY + 10);
    private Rectangle _bossBodyRect, _bossFullRect;   // rendered screen rects, set by DrawBoss for the bar/overlay
    private int _bossFrame;
    private string _bossName = "BOSS";

    // package_03: one representative common enemy per Source (no Nature enemy shipped — a wisp stands in).
    private static readonly Dictionary<Source, string> EnemyForSource = new()
    {
        [Source.Body] = "bonecrawler", [Source.Mind] = "soul_leech", [Source.Nature] = "wisp",
        [Source.Machine] = "stone_sentinel", [Source.Shadow] = "shadeling", [Source.Spirit] = "rift_guardian",
    };

    /// <summary>Display name for the wave's creature ("STONE SENTINEL"), from its art key.</summary>
    private static string PrettyName(Source? src)
        => src is { } s && EnemyForSource.TryGetValue(s, out var k)
            ? k.Replace('_', ' ').ToUpperInvariant()
            : "CORRUPTED";
    // package_04: the six region bosses, matched to region theme.
    private static readonly Dictionary<string, string> BossForRegion = new()
    {
        ["verdant_hollow"] = "thorn_regent", ["cinderworks"] = "forge_colossus", ["umbral_reach"] = "void_reaper",
        ["still_archive"] = "crystal_lich", ["pale_choir"] = "lumen_angel", ["marrow_wastes"] = "spirit_matron",
    };

    private readonly UiKit _ui;
    private readonly VfxPlayer _vfx;
    private readonly Random _rng = new();

    /// <summary>The report for the run just ended, and the one before it — the diff is the whole point.</summary>
    private RunReport? _lastReport;

    /// <summary>
    /// Every run's report, kept and readable — set by the host so it can be saved.
    /// </summary>
    /// <remarks>
    /// The report used to live for four seconds inside the CHAMPION DOWN overlay and then be gone. In a
    /// game whose whole premise is that you are not watching, a lesson delivered only to someone looking
    /// at the exact second their champion fell is a lesson delivered to nobody.
    /// </remarks>
    public RunLog Log { get; set; } = new();

    private bool _logOpen;
    private int _logIndex;   // 0 = newest
    private RunReport? _previousReport;

    /// <summary>Deepest wave reached in this region, so a report can say whether it was a record.</summary>
    /// <summary>The deepest this region has ever been taken — set by the host from persisted world state.</summary>
    /// <remarks>
    /// A HOST-FED value, not a screen-local counter, and the difference is the whole bug. It used to be
    /// a private int starting at 0 every session and restored by nothing, so `Wave >= _bestDepth` was
    /// true on the FIRST run of every launch: the report announced NEW RECORD for a wave-3 death after a
    /// career of forty, and `Log.Add` then saved it that way, so the lie outlived the session that told
    /// it. It was also per-SCREEN rather than per-REGION, so switching regions carried the wrong figure
    /// across even within one session.
    ///
    /// The right number was already persisted and already per-region — `RegionFarm.BestDepth`, which
    /// funds skill points and gates conquest. There was never a second source of truth to keep; there
    /// was a second COPY, kept badly.
    /// </remarks>
    public int BestDepthHere { get; set; }

    /// <summary>The quality this descent accumulated — the tilt a chest it drops should remember.</summary>
    /// <remarks>
    /// Neutral 1.0 when there is no run, so a chest dropped outside one (a fixture, a test) rolls exactly
    /// as it always did rather than being silently zeroed.
    /// </remarks>
    public float CarriedQuality => _run?.Carried.Quality ?? 1f;

    /// <summary>The figure this descent has to beat, latched when it STARTS.</summary>
    /// <remarks>
    /// Latched, because <see cref="BestDepthHere"/> is live: the host calls
    /// <c>RegionFarm.RecordDepth(_expedition.Deepest)</c> every frame, so a player pushing past their
    /// own record moves the target while they are still standing on it. Compared live, a run that beat
    /// forty and died at forty-five would find the record already reading forty-five and report nothing
    /// — the exact opposite failure to the one this replaced, and just as invisible.
    /// </remarks>
    private int _recordToBeat;

    private enum Mode { Fighting, Downed }
    private Mode _mode = Mode.Fighting;

    private SoloExpedition? _run;
    private Champion? _champ;
    private float _anim;
    private float _downedTimer;
    private float _breakTimer;   // the between-wave breath; while >0 the cleared frame holds, then the next wave begins

    // Replay state.
    private float _playheadMs;
    private WaveReplay? _replay;
    private float _champLunge, _enemyLunge, _enemyWindup;
    private int _nextEnemyStrikeMs;
    private float _enemyBaseHealth = 120f, _enemyBaseDamage = 9f;
    private WaveOutcome _outcome = WaveOutcome.Cleared;

    // Feedback so a WATCHED fight actually reads: a wave-cleared banner, the next enemy sliding IN from
    // the right, and a red flash when the champion falls. Without these, waves passed silently and the
    // playtest note was exactly that — "I can't tell what's happening".
    private float _enemyEnter;    // 1 → 0: how far off-screen-right the new enemy still is
    private float _bannerTimer;   // 1.2 → 0: the "WAVE N CLEARED" flash
    private string _bannerText = "";
    private float _deathFlash;    // 1 → 0: red vignette on a fall
    private string _enemyArt = "";

    /// <summary>Deepest wave reached in this region, across restarts — what conquest is measured against.</summary>
    public int Deepest { get; private set; }

    private readonly List<Callout> _callouts = new();
    /// <summary>Which column a callout stacks in. Two columns that never collide must not push each other.</summary>
    private enum CalloutLane { Champion, Enemy }

    private struct Callout
    {
        public string Text; public Color Color; public int X, Y; public float Life; public int Px;
        public CalloutLane Lane;
    }

    /// <summary>
    /// Vertical gap between stacked callouts. MUST exceed <see cref="CritPx"/>, the tallest thing stacked.
    /// </summary>
    /// <remarks>
    /// This was 44 while damage numbers drew at 52px and crits at 72px, so consecutive numbers were
    /// guaranteed to overlap by 8 to 28 pixels — the stack existed, did its arithmetic correctly, and
    /// still produced a smear, because the spacing was smaller than the glyphs it was spacing.
    /// </remarks>
    private const int CalloutLineHeight = 54;

    /// <summary>
    /// How many lines the stack climbs before wrapping back to the bottom.
    /// </summary>
    /// <remarks>
    /// Counting every live callout without a wrap sends a sustained flurry marching off the top of the
    /// arena. Wrapping reuses the lowest slot, by which time the number that was there has faded.
    /// </remarks>
    private const int CalloutLanesDeep = 5;

    private const int DamagePx = 34;
    private const int CritPx = 46;
    private int _strikeCount;   // throttles per-strike damage numbers so they don't flood

    // ── Arena clipping + overlay state (Rev 4 §1/§2/§11). ──
    // Widened and shifted right: left edge clears the control rail (ends x=280), right edge stops
    // short of the existing right rail (starts x=1570), bottom stops short of the nav rail (y=934).
    private static readonly Rectangle ArenaRect = new(492, 100, 1062, 940);
    private RasterizerState? _arenaRasterizer;
    private RasterizerState ArenaRasterizer => _arenaRasterizer ??= new RasterizerState { ScissorTestEnable = true };
    private float _bossIncomingTimer;
    private bool _isBossWave;
    /// <summary>Dev fixture (F6 / RH_SHOT_MODE=boss): render the current wave as the Crystal Lich boss.</summary>
    public bool DevForceBoss { get; set; }
    /// <summary>Dev boss-bounds overlay (F7): draws ground pivot / body / full / arena rects (Rev 5 §17).</summary>
    public bool DevBossDebug { get; set; }

    // Exactly ONE major overlay may show. Priority (high→low): Modal/WelcomeBack (host) > HunterDown >
    // BossIncoming > WaveCleared. The host draws WelcomeBack; when it does, the screen draws none of its own.
    private enum HuntOverlay { None, HunterDown, BossIncoming, WaveCleared }
    private HuntOverlay ResolveOverlay(bool welcome)
    {
        if (welcome) return HuntOverlay.None;

        // LET THE FALL LAND BEFORE THE PAPERWORK. The report used to appear on the same frame the
        // champion went down, and it is a large centred panel — so the death was covered by a table of
        // numbers before anyone could see it happen. That is most of why the death "looked strange":
        // there was nothing to look at, only a red flash and then a summary.
        //
        // Holding the report back for the first stretch of the downed beat costs nothing (the wait was
        // already there) and gives the collapse a moment to read as a collapse.
        //
        // DevHoldReport is exempt. That fixture freezes the downed timer at its full value so the
        // report can be photographed, and the beat is measured from that same value — so without this
        // the fixture would sit forever in the uncovered moment and capture a report that never
        // appeared. Found by running it: the shot came back with no panel at all.
        if (_mode == Mode.Downed)
            return DevShowFall || (!DevHoldReport && _downedTimer > DownedSeconds - DeathBeatSeconds)
                ? HuntOverlay.None
                : HuntOverlay.HunterDown;
        if (_bossIncomingTimer > 0f) return HuntOverlay.BossIncoming;
        if (DevForceBoss) return HuntOverlay.None;   // boss verification fixture: active combat, no wave banner
        if (_bannerTimer > 0f) return HuntOverlay.WaveCleared;
        return HuntOverlay.None;
    }

    /// <summary>
    /// The first-run lesson to show, or null once the player has outgrown the guide.
    /// </summary>
    /// <remarks>
    /// Drawn on the HUNT screen because that is where a new player is actually looking — the guide's
    /// first and most important sentence is that the fight needs nothing from them, and a prompt about
    /// that belongs over the fight, not behind a menu they have no reason to open.
    /// </remarks>
    public TutorialStep? Guide { get; set; }

    /// <summary>The player's build choices and the tree that powers them. Set by the host each frame.</summary>
    public PlayerLoadout Loadout { get; set; } = PlayerLoadout.Starter();
    public MemoryDustTree Tree { get; set; } = new();
    public MasteryTree Mastery { get; set; } = new();
    public Source? EnemySource { get; set; }
    /// <summary>The active region id — picks the boss creature (boss_&lt;region&gt;) on boss waves. Set by the host.</summary>
    public string RegionId { get; set; } = "";
    /// <summary>The active region's combat character — handed to the run so the enemy's bite tempo matches it.</summary>
    public AttackBias EnemyBias { get; set; } = AttackBias.Balanced;
    /// <summary>Unopened chests waiting in the Forge — drives the "you have things to do" nudge. Set by the host.</summary>
    public int ChestCount { get; set; }
    /// <summary>Gleam per second the idle champion earns — shown in the HUNT idle-rewards panel. Set by the host.</summary>
    public float IdleGleamRate { get; set; }
    private Source? _lastSource;
    private Hunter? _hunter;

    public SoloExpeditionScreen(UiKit ui)
    {
        _ui = ui;
        _vfx = new VfxPlayer(ui.Assets);
    }

    // ── Reward channel: one entry per cleared wave, drained by the host ─────────────────────────────
    public readonly record struct WaveReward(Haul Haul, int Wave, bool IsBoss);
    private readonly Queue<WaveReward> _rewards = new();
    public bool HasReward => _rewards.Count > 0;
    public WaveReward TakeReward() => _rewards.Dequeue();

    /// <summary>What each Form looks like when it fires — the fight is watched, so the effect is the read.</summary>
    private static (string Text, Color Color) CalloutFor(Form form) => form switch
    {
        Form.Strike => ("STRIKE", Ember),
        Form.Projectile => ("VOLLEY", Steel),
        Form.Aura => ("AURA", Bloom),
        Form.Trap => ("TRAP", Gold),
        Form.Mark => ("MARK", Bone),
        _ => ("MORPH", Verdant),
    };

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Update(GameTime time, KeyboardState keys, Point mouse, bool clicked, int wheel,
        Hunter hunter, float enemyBaseHealth, float enemyBaseDamage)
    {
        _hunter = hunter;
        var dt = (float)time.ElapsedGameTime.TotalSeconds;
        _anim += dt;
        // The swing clock is SEPARATE from _champLunge. The lunge is a 0.2s positional shove
        // (it decays at dt*5), and driving a 1.1s animation off it played the whole swing in
        // 0.2s — the arm blurred. This advances at real time so the clip reads at the pace it
        // was authored, and re-arms when a new strike lands.
        if (_champLunge > 0.3f && _strikeTime <= 0f) _strikeTime = StrikeSeconds;
        if (_strikeTime > 0f) _strikeTime = Math.Max(0f, _strikeTime - dt);
        _champLunge = Math.Max(0f, _champLunge - dt * 5f);
        _enemyLunge = Math.Max(0f, _enemyLunge - dt * 5f);
        _enemyEnter = Math.Max(0f, _enemyEnter - dt * 2.5f);   // the new enemy slides in over ~0.4s
        _bannerTimer = Math.Max(0f, _bannerTimer - dt);
        _bossIncomingTimer = Math.Max(0f, _bossIncomingTimer - dt);
        _deathFlash = Math.Max(0f, _deathFlash - dt * 1.5f);
        _vfx.Update(dt);
        for (var i = 0; i < _callouts.Count; i++) { var c = _callouts[i]; c.Life -= dt * 1.6f; _callouts[i] = c; }
        _callouts.RemoveAll(c => c.Life <= 0f);

        // Travelling to a new region (its element changes) restarts the champion there, fresh.
        // A source change restarts the run — travelling to a new region should not continue the old
        // descent. DevHoldReport is exempt: the capture fixture builds its report before the host has
        // pushed the region's Source down, so the very next frame would discard the run and the report
        // with it. That is what made the report screenshot show a fresh descent every time.
        if (EnemySource != _lastSource && !DevHoldReport)
        {
            _lastSource = EnemySource;
            _run = null;
            Deepest = 0;
        }

        // Keep the region's difficulty current; it is read the next time a run (re)starts.
        _enemyBaseHealth = enemyBaseHealth;
        _enemyBaseDamage = enemyBaseDamage;

        if (_run is null) StartRun(hunter);

        // Stat training moved to the CHARACTER screen (press C); this is a pure idle-watch view now.

        switch (_mode)
        {
            case Mode.Fighting: UpdateFight(dt); break;
            case Mode.Downed:
                if (DevHoldReport) break;   // capture fixture: keep the report up instead of restarting
                _downedTimer -= dt;
                if (_downedTimer <= 0f) StartRun(hunter);
                break;
        }
    }

    /// <summary>Counts descents, so each one seeds its own compositions. See SoloExpedition.RunIndex.</summary>
    private int _runIndex;

    private void StartRun(Hunter hunter)
    {
        var build = Loadout.ToBuild(Tree, Mastery, Character);
        _recordToBeat = BestDepthHere;   // before a wave is pushed, or the run competes with itself

        // Charged once here at mint: RECKLESS OFFERING's health price and the build's health multipliers.
        var hp = SoloBattle.ChampionHealth(build, hunter);
        _champ = new Champion { MaxHealth = hp, Health = hp };
        // RegionId reaches the sim, not just the boss art: it selects the band cycle and the creature
        // roster, which is what makes one region a different PLACE rather than the same place with
        // bigger numbers. RunIndex seeds each descent's compositions so a replay is identical.
        _run = new SoloExpedition(build, _champ, hunter, _enemyBaseHealth, _enemyBaseDamage,
            ExpeditionTuning.Default, EnemySource, _rng)
        {
            EnemyBias = EnemyBias,
            RegionId = RegionId,
            RunIndex = ++_runIndex,
        };
        _mode = Mode.Fighting;
        BeginWave();
    }

    private void BeginWave()
    {
        if (_run is null || _champ is null) return;

        var startHealth = new Dictionary<int, int> { [0] = _champ.Health };
        var maxHealth = new Dictionary<int, int> { [0] = _champ.MaxHealth };

        _outcome = _run.PushWave();

        // THE WAVE'S ACTUAL TOTAL, summed from the creatures the sim just built — not
        // `_enemyBaseHealth * EnemyScale(wave + 1)`, which is what this used to be. That was a
        // PRE-COMPOSITION estimate: computed before PushWave, it could not know the archetype's health
        // multiplier, could not know PLATED had thickened anything, and could not know NUMBERS had added
        // a creature. The bar the player watches is the only readout of how a wave is going, and on
        // exactly the waves that differ from the average it was measuring against a number the
        // simulation never used — draining to empty with creatures still standing, or stalling above
        // zero on a wave already won.
        var enemyHp = _run.LastWaveCreatures.Sum(c => c.MaxHealth);

        _replay = new WaveReplay(_run.LastWaveEvents, startHealth, maxHealth, enemyHp);
        // Hand the replay the composition so each creature drains its own bar and vanishes on its own
        // beat. Without this a wave of five reads as one bar going down, which hides the single most
        // useful fact in a Swarm band: how many of them you actually got through.
        _replay.SetComposition(_run.LastWaveCreatures.Select(c => c.MaxHealth).ToList());
        _playheadMs = 0f;
        _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(0f);
        _callouts.Clear();
        if (_run.LastWaveWasBoss) _bossIncomingTimer = 1.6f;   // a BossIncoming announcement opens the boss wave
    }

    /// <summary>The host rolled a chest for the boss just felled — upgrade the banner to the reward beat.</summary>
    /// <remarks>Called same-frame as the boss-down banner, so "CHEST DROPPED!" replaces "BOSS DOWN!" cleanly.</remarks>
    public void FlashChest()
    {
        _bannerText = "BOSS DOWN — CHEST DROPPED!  (F — FORGE)";
        _bannerTimer = 2.4f;
    }

    /// <summary>
    /// Float a line over the champion — a skill name, a heal, a triggered keystone.
    /// </summary>
    /// <remarks>
    /// There is deliberately no per-call vertical offset. There was one, and it defeated the stack: heals
    /// spawned 32px lower than skill names, so the two kinds interleaved instead of queueing, and a heal
    /// landing on the same frame as a cast drew straight through it. A column can only have one origin —
    /// once the callers disagree about where line zero is, the slot arithmetic is spacing them from
    /// different places and the overlap it exists to prevent comes back.
    /// </remarks>
    private void Say(string text, Color color)
    {
        _callouts.Add(new Callout
        {
            Text = text,
            Color = color,
            X = ChampBox.Center.X,
            Y = ChampBox.Y - 40 - StackSlot(CalloutLane.Champion) * CalloutLineHeight,
            Life = 1f,
            Px = 36,
            Lane = CalloutLane.Champion,
        });
    }

    /// <summary>
    /// Which line of its column the next callout takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two fixes over the counter this replaces, both of which showed up together as an unreadable
    /// smear over the enemy.
    /// </para>
    /// <para>
    /// It counts only callouts in the SAME lane. The skill names over the champion and the damage
    /// numbers over the creature share one list but are drawn in columns hundreds of pixels apart, so
    /// each was shoving the other up a line to avoid a collision that could not happen — three skills
    /// firing pushed the next damage number three lines into empty sky, and vice versa.
    /// </para>
    /// <para>
    /// And it counts everything still VISIBLE, not everything still fresh. The old threshold was
    /// Life &gt; 0.55, but a callout is drawn until Life reaches 0 — nearly twice as long. So the counter
    /// returned to zero while the earlier numbers were still on screen, and the next number spawned
    /// underneath one it could not see.
    /// </para>
    /// </remarks>
    private int StackSlot(CalloutLane lane)
        => _callouts.Count(c => c.Lane == lane && c.Life > 0f) % CalloutLanesDeep;

    /// <summary>A floating combat number over the enemy — the fight's "action" read (package_10). Scaled off
    /// the hunter's real PowerRating, jittered so numbers don't stack; crits are gold and linger.</summary>
    private void SpawnDamage(int amount, bool crit)
    {
        // ABOVE the creature's health bar, and STACKED. Numbers used to spawn at EnemyBox.Y + 8..40,
        // which is exactly where the wave's health bar is drawn — so a hit printed "-203" through the
        // bar and the next one printed "-344" through the first. Two unreadable numbers and an
        // unreadable bar, at the one moment the player is watching to see how the fight is going.
        //
        // The stack counter is the same trick the wave callouts already use: each number still on
        // screen pushes the next one up a line, so a flurry reads as a column instead of a smear.
        //
        // The X jitter is GONE, and its removal is half the fix. It predates the stack — it was the
        // original anti-collision trick, scattering numbers up to 72px sideways so two of them rarely
        // landed on the same spot. Once the stack arrived the two devices fought: the stack builds a
        // column and the jitter immediately kicked every entry out of it, so a tidy vertical list read
        // as a scatter and numbers on neighbouring lines still crossed. A column only reads as a column
        // if it is one.
        _callouts.Add(new Callout
        {
            Text = crit ? $"-{amount:N0} CRIT" : $"-{amount:N0}",
            Color = crit ? Gold : Bone,
            X = EnemyBox.Center.X,
            Y = EnemyBox.Y - 96 - StackSlot(CalloutLane.Enemy) * CalloutLineHeight,
            Life = crit ? 1.3f : 1f,
            // Was 52 and 72. The fight "reads loud" was the standing playtest note, and a damage number
            // two-thirds the height of the creature it is describing is most of why.
            Px = crit ? CritPx : DamagePx,
            Lane = CalloutLane.Enemy,
        });
    }

    private int HitDamage(float mult)
    {
        var pwr = Math.Max(1, _hunter?.PowerRating ?? 100);
        var j = 0.85f + (Jitter((int)_playheadMs, 30) + 30) / 200f;   // ~0.85..1.15 spread
        return Math.Max(1, (int)(pwr * mult * j));
    }

    private void UpdateFight(float dt)
    {
        if (_run is null || _replay is null) return;

        // The between-wave breath: hold on the cleared frame for a beat, THEN walk into the next wave. This
        // also gates re-entry — the just-finished replay stays "finished", so the clear payout below fires
        // exactly once.
        if (_breakTimer > 0f)
        {
            _breakTimer -= dt;
            if (_breakTimer <= 0f) { BeginWave(); _enemyEnter = 1f; }
            return;
        }

        // Hold the fight until the new enemy has finished sliding in — otherwise the champion swings at empty
        // air while the enemy is still off to the right ("hunter hits before the enemy arrives").
        if (_enemyEnter > 0f) return;

        _playheadMs += dt * 1000f * _speedMul;

        var lead = _nextEnemyStrikeMs - _playheadMs;
        _enemyWindup = lead is > 0 and < 600 ? 1f - lead / 600f : 0f;

        foreach (var e in _replay.Advance(_playheadMs))
        {
            switch (e.Kind)
            {
                case BattleEventKind.Strike:
                    // Just the lunge. A hit-spark on EVERY strike (skills AND auto-attacks fire these
                    // constantly) was the "too many red slashes" the playtest flagged — loudness has to be
                    // budgeted against importance, so the loud VFX are reserved for the SKILL casts below.
                    _champLunge = 1f;
                    if ((_strikeCount++ & 1) == 0) SpawnDamage(HitDamage(1f), false);   // every other auto-hit
                    break;
                case BattleEventKind.EnemyStrike:
                    _enemyLunge = 1f;
                    _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(e.AtMs);
                    _vfx.Play("vfx_hit", ChampBox.Center.X, ChampBox.Center.Y, tint: Ember);
                    break;
                case BattleEventKind.Skill:
                    var form = (Form)e.Amount;
                    var (text, colour) = CalloutFor(form);
                    Say(text, colour);
                    PlayFormVfx(form);
                    SpawnDamage(HitDamage(form == Form.Trap ? 3f : 2f), form == Form.Trap);   // skills hit big
                    break;
                case BattleEventKind.Heal:
                    Say($"+{e.Amount}", Verdant);
                    _vfx.Play("vfx_levelup", ChampBox.Center.X, ChampBox.Y + 32, tint: Verdant);
                    break;
                case BattleEventKind.Shield:
                    Say("UNDYING", Gold);
                    _vfx.Play("vfx_interrupt", ChampBox.Center.X, ChampBox.Center.Y, tint: Gold);
                    break;
                case BattleEventKind.Down:
                    _vfx.Play("vfx_death", ChampBox.Center.X, ChampBox.Center.Y, fps: 14f);
                    break;
                case BattleEventKind.EnemyDown:
                    _vfx.Play("vfx_death", EnemyBox.Center.X, EnemyBox.Center.Y, scale: 2, fps: 14f);
                    break;
            }
        }

        if (!_replay.Finished) return;

        if (_outcome == WaveOutcome.Cleared)
        {
            // Pay this wave out NOW, then walk straight into the next one — no boundary, no button.
            _rewards.Enqueue(new WaveReward(_run.LastWaveHaul, _run.Wave, _run.LastWaveWasBoss));
            if (_run.Wave > Deepest) Deepest = _run.Wave;

            // Announce the clear and slide the NEXT enemy in, so the wave boundary is something you SEE.
            // A boss falling is the reward beat — it dropped a chest — so it gets its own louder banner
            // that lingers a touch longer. You FEEL the earn at the kill, then go crack it in the Forge.
            // A chest is no longer a given, so the boss banner no longer promises one — the host calls
            // FlashChest() and upgrades this banner only when a chest actually drops.
            _bannerText = _run.LastWaveWasBoss ? "BOSS DOWN!" : $"WAVE {_run.Wave} CLEARED";
            _bannerTimer = _run.LastWaveWasBoss ? 1.6f : 1.2f;
            // Take a breath on the clear, THEN begin the next wave (see the break gate atop UpdateFight). A
            // boss's fall lingers a touch longer — it dropped a chest, and that beat should land.
            _breakTimer = _run.LastWaveWasBoss ? WaveBreakSeconds + 0.5f : WaveBreakSeconds;
        }
        else
        {
            // Fell (or stalled). A red flash, a short breath, then the champion regroups.
            //
            // The REPORT is taken here, at the exact moment the run ended. With no in-run decisions this
            // is the only thing the player can learn from, so it is captured before anything resets.
            _previousReport = Log.PreviousIn(RegionId);
            _lastReport = _run!.Report(isRecord: _run.Wave > _recordToBeat);
            Log.Add(_lastReport);   // kept and saved — see the Log property
            LogDirty = true;

            _deathFlash = 1f;
            _mode = Mode.Downed;
            _downedTimer = DownedSeconds;
        }
    }

    private void PlayFormVfx(Form form)
    {
        switch (form)
        {
            case Form.Strike:
                _vfx.Play("vfx_ability_ruinstrike", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 22f, tint: Ember);
                break;
            case Form.Trap:
                _vfx.Play("vfx_crit", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 20f, tint: Gold);
                break;
            case Form.Mark:
                _vfx.Play("vfx_interrupt", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 20f, tint: Bone);
                break;
            case Form.Transformation:
                _vfx.Play("vfx_levelup", ChampBox.Center.X, ChampBox.Y + 24, fps: 18f, tint: Verdant);
                break;
            default:
                _vfx.Play("vfx_weakhit", EnemyBox.Center.X, EnemyBox.Center.Y, fps: 18f, tint: Steel);
                break;
        }
    }

    private static int Jitter(int seed, int spread) => (int)(seed * 2654435761L % (spread * 2 + 1)) - spread;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, Point mouse, bool clicked, string regionName, string enemyArt = "", bool suppressBanner = false)
    {
        _enemyArt = enemyArt;
        _vfx.Scale = 1;   // this screen authors at canvas scale 1, so the VFX overlay draws at scale 1 too
        // The host hands us the mouse in 480-logical space; lift it into this screen's 1920 space so every
        // hit-test (the only clickables are the BATTLE SPEED buttons) lands on the drawn rects.
        var hit = new Point(mouse.X * 4, mouse.Y * 4);
        if (_run is null || _replay is null || _champ is null) return;

        // The dev boss fixture is a STATIC verification shot — clear transient combat churn (death smoke,
        // callouts, flash, wave banner) so only the boss and its bar read.
        if (DevForceBoss) { _vfx.Clear(); _callouts.Clear(); _deathFlash = 0f; _bannerTimer = 0f; }

        _isBossWave = DevForceBoss || WaveScaling.IsBossWave(_run.Wave + 1, ExpeditionTuning.Default);
        var overlay = ResolveOverlay(suppressBanner);
        // Rev 4 §18.3: exactly one major overlay. The host draws WelcomeBack; when it does, the screen draws
        // none. Dev warning only — never a Debug.Assert (a failed assert aborts the game's Debug build).
        if (suppressBanner && overlay != HuntOverlay.None)
            System.Diagnostics.Debug.WriteLine("Only one major Hunt overlay may be active.");

        // ── ARENA — every world-space element is scissor-clipped to the arena rect (Rev 4 §1/§11). The host
        // began a canvas batch for us; end it, run the clipped arena pass, then reopen an UNCLIPPED batch for
        // the HUD (which the host closes). The VFX sub-pass inherits the same scissor via _vfx.Rasterizer. ──
        b.End();
        _ui.Device.ScissorRectangle = ArenaRect;
        _vfx.Rasterizer = ArenaRasterizer;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, ArenaRasterizer);
        DrawArena(b, overlay);
        b.End();
        _vfx.Rasterizer = null;

        // ── HUD — unclipped chrome over the arena. ──
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawHunterHud(b);
        DrawStageHeader(b, regionName, _isBossWave);
        DrawRightColumn(b);
        // Rail frame first, then its contents. The old order relied on the rail being TRANSLUCENT — the
        // skill dock was drawn under it and read through as a washed-out ghost. With a real opaque panel
        // that hid the dock outright.
        DrawBattleControls(b, hit, clicked);
        DrawSkillDock(b);
        if (_isBossWave) DrawBossBar(b);                          // §10/§12: screen-space, NOT arena-clipped
        if (_isBossWave && DevBossDebug) DrawBossDebugOverlay(b); // §17: fixture-only bounds visualization (F7)
        // (the host closes this batch with b.End(); the shared hex nav is drawn by the host over every screen.)
    }

    /// <summary>Arena figures + effects, drawn inside the scissor clip so no actor/VFX/bar/number escapes it.</summary>
    private void DrawArena(SpriteBatch b, HuntOverlay overlay)
    {
        var attacking = _enemyWindup > 0f;
        if (_isBossWave) DrawBoss(b, attacking);
        else DrawNormalEnemy(b, attacking);

        // Champion (arena left). Name/HP live in the top-left HUD.
        var push = (int)(_champLunge * 40f);
        var cbox = new Rectangle(ChampBox.X + push, ChampBox.Y, ChampBox.Width, ChampBox.Height);
        if (_replay!.IsShielded(0)) Outline(b, new Rectangle(cbox.X - 4, cbox.Y - 4, cbox.Width + 8, cbox.Height + 8), Steel, 4);
        DrawChampion(b, cbox, dead: _mode == Mode.Downed);

        _vfx.Draw(b);
        DrawCallouts(b);
        if (_deathFlash > 0f) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Ember * (_deathFlash * 0.35f));

        DrawGuide(b);

        DrawArenaOverlay(b, overlay);
    }

    /// <summary>
    /// How big each archetype draws, relative to the enemy box.
    /// </summary>
    /// <remarks>
    /// Scale is the fastest read in the arena. A Swarm is small and there are several; a Bruiser fills
    /// the box alone. Together with the archetype name above the wave's bar, this is how a player learns
    /// what beat them without being told.
    /// </remarks>
    private static float ArchetypeScale(Archetype a) => a switch
    {
        Archetype.Swarm => 0.58f,
        Archetype.Caster => 0.78f,
        Archetype.Armoured => 0.92f,
        _ => 1.12f,   // Bruiser
    };

    /// <summary>
    /// Draw a wave of several creatures: laid out across the arena's right half, scaled by archetype,
    /// each with its own health pip, each vanishing as it dies.
    /// </summary>
    /// <remarks>
    /// The arena drew exactly one enemy for the whole life of the project. A player looking at a Swarm
    /// band could not see that there were five of them, which meant the one thing the band was trying to
    /// teach — that a single-target build spends its cooldown on one of five while the other four keep
    /// biting — was invisible.
    /// </remarks>
    /// <summary>Frames in a generated enemy strip. Every one of them is eight.</summary>
    private const float EnemyClipFrames = 8f;

    /// <summary>
    /// The clock an enemy's clip runs on — the SWING for an attack, the free clock for an idle.
    /// </summary>
    /// <remarks>
    /// This is the fix for "the attack animations look strange". An attack clip is drawn with
    /// <c>loop: false</c>, and it was handed <c>_anim</c> — the free-running screen clock, which by the
    /// second wave is a large number. A non-looping clip at a large time is CLAMPED to its last frame,
    /// so every enemy attack in the game was a single frozen pose: the creature snapped to the end of
    /// its swing and held it, then snapped back to idle. Eight frames of animation existed and one of
    /// them was ever drawn.
    ///
    /// <c>_enemyWindup</c> already runs 0 to 1 across the 600ms before the blow lands, so it is exactly
    /// the phase the clip wants. Driving the clip from it means the swing plays THROUGH, and plays in
    /// step with the hit it is delivering rather than beside it — the same rule the champion's strike
    /// already followed, and the reason the champion's swing looked right while the enemy's did not.
    /// </remarks>
    private float EnemyClipSeconds(bool attacking, float fps, float stagger = 0f)
        => attacking ? _enemyWindup * (EnemyClipFrames / fps) : _anim + stagger;

    private void DrawComposition(SpriteBatch b, bool attacking, IReadOnlyList<WaveCreature> comp)
    {
        var scale = ArchetypeScale(_run?.LastWaveArchetype ?? Archetype.Bruiser);
        var enter = (int)(_enemyEnter * 280f);
        var lunge = (int)(_enemyLunge * -40f);

        var w = (int)(EnemyBox.Width * scale);
        var h = (int)(EnemyBox.Height * scale);

        // Lay the row out INSIDE the arena, clamped by each creature's own half-width. The first version
        // spread from the enemy box's centre by a fixed span and pushed the last creature past the
        // arena's scissor edge, so a wave of five showed four and a sliver — which is exactly the fact
        // the player most needs to read.
        var half = w / 2;

        // THE ROW COMPRESSES BEFORE IT OVERFLOWS. Spacing was a fixed fraction of the creature width,
        // so a wide enough wave made the row wider than the arena — and then the clamp below was handed
        // a minimum greater than its maximum, which is not a layout mistake but an ArgumentException:
        // "'1028' cannot be greater than 1018", thrown out of Draw, killing the game mid-fight. It only
        // appears on a big composition, which is why it survived every screenshot and every short run
        // and was found by soaking the fight for five minutes.
        var room = ArenaRect.Width - w - 40;   // the span a row may occupy inside the arena
        var spacing = (int)(w * 0.62f);        // overlap slightly; a row of five must still fit
        if (comp.Count > 1 && room > 0) spacing = Math.Min(spacing, room / (comp.Count - 1));
        var wanted = Math.Max(0, spacing) * (comp.Count - 1);

        // AND THE CLAMP STILL CANNOT INVERT. Compression handles every wave that can be made to fit;
        // this handles the one that cannot — a single creature wider than the arena leaves no room at
        // all, and an overhanging row is a cosmetic problem where a thrown exception is a lost session.
        var lo = ArenaRect.X + half + 20 + wanted / 2;
        var hi = ArenaRect.Right - half - 20 - wanted / 2;
        var centre = lo > hi
            ? ArenaRect.Center.X
            : Math.Clamp(EnemyBox.Center.X + enter + lunge, lo, hi);
        var left = centre - wanted / 2;

        string? stripKey = null, staticKey = null;
        if (EnemySource is { } es && EnemyForSource.TryGetValue(es, out var en))
        {
            var act = attacking ? en == "stone_sentinel" ? "slam" : "attack" : "idle";
            stripKey = $"{en}_{act}_strip8_512";
            staticKey = attacking ? $"{en}_attack_01" : $"{en}_idle_01";
        }

        for (var i = 0; i < comp.Count; i++)
        {
            if (_replay is not null && !_replay.CreatureAlive(i)) continue;

            // Back-to-front by index so the row overlaps consistently, and each creature bobs on its own
            // phase — five sprites bobbing in unison read as one animated object, not as five creatures.
            var cx = left + spacing * i;
            var bob = (int)(MathF.Sin(_anim * 2f + i * 1.7f) * 7f);
            var box = new Rectangle(cx - w / 2, EnemyBox.Bottom - h + bob, w, h);

            _ui.GroundShadow(b, box.Center.X, EnemyBox.Bottom - 10, (int)(w * 0.55f), (int)(38 * scale), 0.55f);

            const float crop = 0.08f;
            var compFps = attacking ? 16f : 12f;
            if (stripKey is null || !_ui.AnimSprite(b, stripKey, box,
                    EnemyClipSeconds(attacking, compFps, i * 0.31f), compFps,
                    !attacking, Color.White, crop))
                if (staticKey is null || !_ui.SpriteGrounded(b, staticKey, box, Color.White, crop))
                    _ui.Fill(b, new Rectangle(box.X + 20, box.Y + 20, box.Width - 40, box.Height - 40), Ember);

            // A pip per creature rather than a framed bar — at five across, ornate frames become noise.
            if (_replay is null) continue;
            var frac = _replay.CreatureHealthFraction(i);
            // Sits on the creature's own visible top, not on its padded canvas — otherwise the pip
            // floats a sprite's worth of empty pixels above its head.
            // A MEASURED constant rather than the sprite's alpha bounds. TopPadFraction reports the
            // minimum padding across the whole strip, and these creatures reach the top of the frame in
            // at least one animation frame, so it returns ~0 and the pip floated 55px above every head.
            // The creature art fills roughly the lower four-fifths of its frame; captured and checked.
            const float headFraction = 0.22f;
            var pip = new Rectangle(box.Center.X - 28, box.Y + (int)(h * headFraction) - 10, 56, 6);
            _ui.Fill(b, pip, new Color(0x12, 0x0C, 0x10));
            if (frac > 0f) _ui.Fill(b, new Rectangle(pip.X, pip.Y, (int)(pip.Width * frac), pip.Height), Ember);
        }

        // One wave-level nameplate: what this wave IS, which is the thing the player has to learn.
        var label = $"{_run?.LastWaveArchetype.ToString().ToUpperInvariant()}  x{comp.Count}";
        var affixes = _run?.LastWaveAffixes ?? Array.Empty<Affix>();
        if (affixes.Count > 0)
            label += "   ·   " + string.Join(" + ", affixes.Select(a => a.ToString().ToUpperInvariant()));

        var bar = new Rectangle(EnemyBox.Center.X - 92, EnemyBox.Y + 10, 184, 34);
        _ui.BarArt(b, bar, Math.Clamp(_replay?.EnemyHealthFraction ?? 1f, 0f, 1f), "health");
        _ui.TextCenterBig(b, label, bar.Center.X, bar.Y - 28, UiKit.Vellum, UiTypography.Secondary);
    }

    private void DrawNormalEnemy(SpriteBatch b, bool attacking)
    {
        var comp = _run?.LastWaveCreatures ?? Array.Empty<WaveCreature>();
        if (comp.Count > 1)
        {
            DrawComposition(b, attacking, comp);
            return;
        }

        var elunge = (int)(_enemyLunge * -40f);
        var enter = (int)(_enemyEnter * 280f);
        var ebox = new Rectangle(EnemyBox.X + elunge + enter, EnemyBox.Y, EnemyBox.Width, EnemyBox.Height);
        _ui.GroundShadow(b, ebox.Center.X, ebox.Bottom - 10, (int)(ebox.Width * 0.60f), 42, 0.6f);

        var bob = (int)(MathF.Sin(_anim * 2f) * 8f);
        const float crop = 0.08f;
        var figTop = ebox.Bottom - ebox.Height;
        var ab = new Rectangle(ebox.X, figTop + bob, ebox.Width, ebox.Height);

        string? stripKey = null, staticKey = null;
        var fps = attacking ? 16f : 12f;
        if (EnemySource is { } es && EnemyForSource.TryGetValue(es, out var en))
        {
            var act = attacking ? en == "stone_sentinel" ? "slam" : "attack" : "idle";
            stripKey = $"{en}_{act}_strip8_512";
            staticKey = attacking ? $"{en}_attack_01" : $"{en}_idle_01";
        }

        if (stripKey is null || !_ui.AnimSprite(b, stripKey, ab, EnemyClipSeconds(attacking, fps), fps,
                                                !attacking, Color.White, crop))
        {
            // Grounded so the static fallback stands where the animated strip does — otherwise the enemy
            // visibly hopped whenever the strip was missing and this path took over.
            if (staticKey is null || !_ui.SpriteGrounded(b, staticKey, ab, Color.White, crop))
                _ui.Fill(b, new Rectangle(ebox.X + 40, ebox.Y + 40, ebox.Width - 80, ebox.Height - 80), Ember);
        }

        if (_enemyWindup > 0f)
        {
            var r = (int)(16 + _enemyWindup * 32);
            Outline(b, new Rectangle(ab.X - r, figTop - r, ab.Width + r * 2, ebox.Height + r * 2), Ember, 4);
        }

        // Quiet 130×12 bar ~18px above the VISIBLE top of the figure (§24.1), via the sprite's alpha bounds.
        var boundsKey = staticKey ?? stripKey ?? "";
        var topPad = boundsKey.Length > 0 ? _ui.TopPadFraction(boundsKey) : 0f;
        var visTop = figTop + (int)(Math.Max(0f, (topPad - crop) / (1f - crop)) * ebox.Height);
        // Framed bar art, matching the Hunter's own HUD bar. A bare 130x12 red rectangle was the one
        // unstyled element left inside the arena, and at full health it read as a floating red streak
        // with nothing tying it to the creature underneath.
        var ebar = new Rectangle(ebox.Center.X - 92, visTop - 46, 184, 34);
        _ui.BarArt(b, ebar, Math.Clamp(_replay!.EnemyHealthFraction, 0f, 1f), "health");
        // The creature was never named on screen — the player fought an anonymous sprite for the whole run.
        if (stripKey is not null || staticKey is not null)
            _ui.TextCenterBig(b, PrettyName(EnemySource), ebar.Center.X, ebar.Y - 28, UiKit.Vellum, UiTypography.Secondary);
    }

    /// <summary>Rev 5: draw the boss by its BODY bounds — the ground pivot lands at the anchor, the body scales
    /// to ~540px, and the wings/staff extend beyond (clipped by the arena). NOT box-fit on the full texture.</summary>
    private void DrawBoss(SpriteBatch b, bool attacking)
    {
        var bossKey = DevForceBoss ? "crystal_lich" : BossForRegion.GetValueOrDefault(RegionId);
        var m = bossKey is not null ? BossMetaFor.GetValueOrDefault(bossKey, DefaultBossMeta) : DefaultBossMeta;
        _bossName = m.Name;
        var tex = bossKey is not null ? _ui.Assets.Get($"{bossKey}_{(attacking ? "attack" : "idle")}_strip8_1024") : null;
        if (tex is null || tex.Height <= 0)
        {
            _bossBodyRect = new Rectangle(BossAnchor.X - 110, BossAnchor.Y - BossTargetBodyHeight, 220, BossTargetBodyHeight);
            _bossFullRect = _bossBodyRect;
            _ui.Fill(b, _bossBodyRect, Ember);
            return;
        }

        var frameW = tex.Height;                          // square frames
        var frames = Math.Max(1, tex.Width / frameW);
        // The boss swing rides the same windup as everything else. It used to loop its attack strip on
        // the free clock, so a boss "attacking" was really a boss cycling frames at a faster rate — the
        // swing never lined up with the blow, which reads as flailing rather than striking.
        _bossFrame = attacking
            ? Math.Min(frames - 1, (int)(_enemyWindup * frames))
            : (int)(_anim * 10f) % frames;   // EXACTLY one frame (§8)
        if (tex.Width % frameW != 0)
            System.Diagnostics.Debug.WriteLine($"Boss strip width {tex.Width} not a whole multiple of {frameW}.");

        var scale = BossTargetBodyHeight / (float)m.BodyH;
        // Source excludes the top bleed-streak defect (SrcTop). The ground pivot = body bottom-centre; place it
        // at the anchor by choosing the destination so (BodyCenterX, BodyBottom) maps to BossAnchor.
        var src = new Rectangle(_bossFrame * frameW, m.SrcTop, frameW, tex.Height - m.SrcTop);
        var destLeft = (int)(BossAnchor.X - (m.BodyX + m.BodyW / 2f) * scale);
        var destTop = (int)(BossAnchor.Y - (m.BodyY + m.BodyH - m.SrcTop) * scale);
        var dest = new Rectangle(destLeft, destTop, (int)(frameW * scale), (int)(src.Height * scale));

        // Boss ground shadow at the pivot (§13), sized off the rendered BODY width, not the full silhouette.
        var bodyWpx = (int)(m.BodyW * scale);
        _ui.Fill(b, new Rectangle(BossAnchor.X - (int)(bodyWpx * 0.34f), BossAnchor.Y - 9, (int)(bodyWpx * 0.68f), 16), GroundShade);

        b.Draw(tex, dest, src, Color.White);

        _bossBodyRect = new Rectangle(destLeft + (int)(m.BodyX * scale), destTop + (int)((m.BodyY - m.SrcTop) * scale),
            (int)(m.BodyW * scale), (int)(m.BodyH * scale));
        _bossFullRect = new Rectangle(destLeft + (int)(m.FullX * scale), destTop + (int)((m.FullY - m.SrcTop) * scale),
            (int)(m.FullW * scale), (int)(m.FullH * scale));
        if (_bossBodyRect.Height is < 500 or > 580)
            System.Diagnostics.Debug.WriteLine($"Boss body height {_bossBodyRect.Height}px outside 500..580.");
    }

    /// <summary>The dedicated boss health bar — SCREEN-SPACE UI (§10/§12), drawn in the HUD pass, not clipped.</summary>
    private void DrawBossBar(SpriteBatch b)
    {
        // Below the stage header (which ends at y=153) and right of the Hunter HUD (which ends at x=596).
        // At (510,145,900,46) it drove straight through both of them.
        var bar = new Rectangle(660, 200, 600, 54);
        // The dev fixture's underlying wave-2 enemy is already dead (0%), so pose a representative fill.
        var frac = DevForceBoss ? 0.78f : Math.Clamp(_replay!.EnemyHealthFraction, 0f, 1f);
        // Name ABOVE the bar, not on it. Gold on the molten fill was gold-on-gold — the boss's name, the
        // one label that has to land, was the least readable thing on screen. Same rule as the parchment
        // panels: when the surface is already gold, the word moves off it.
        _ui.BarArt(b, bar, frac, "boss");
        _ui.TextCenterBig(b, _bossName, bar.Center.X, bar.Y - 34, UiKit.Vellum, UiTypography.PanelTitle);
        _ui.TextCenterBig(b, $"{(int)(frac * 100)}%", bar.Center.X, bar.Y + 17, UiKit.Ink, UiTypography.OverlayBody);
    }

    /// <summary>Rev 5 §17: fixture-only bounds visualization (ground pivot, body, full silhouette, arena).</summary>
    private void DrawBossDebugOverlay(SpriteBatch b)
    {
        DebugRect(b, ArenaRect, Ember, 3);                                         // arena — red
        DebugRect(b, _bossFullRect, new Color(0x40, 0xE0, 0xE0), 2);               // full visible — cyan
        DebugRect(b, _bossBodyRect, new Color(0x48, 0xD0, 0x48), 3);               // body — green
        _ui.Fill(b, new Rectangle(BossAnchor.X - 22, BossAnchor.Y - 2, 44, 4), Gold);   // ground pivot — yellow cross
        _ui.Fill(b, new Rectangle(BossAnchor.X - 2, BossAnchor.Y - 22, 4, 44), Gold);
        _ui.TextBig(b, $"body {_bossBodyRect.Width}x{_bossBodyRect.Height}  frame {_bossFrame}  anchor {BossAnchor.X},{BossAnchor.Y}",
            190, 206, Gold, UiTypography.Secondary);
    }

    private void DebugRect(SpriteBatch b, Rectangle r, Color c, int t)
    {
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        _ui.Fill(b, new Rectangle(r.X, r.Y, t, r.Height), c);
        _ui.Fill(b, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    /// <summary>
    /// The post-run report — the most important screen in the game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Combat is automatic and a run holds no decisions, so this is the only moment a player can learn
    /// anything. It replaced a two-line "CHAMPION DOWN — REGROUPING" banner that told them the wave
    /// number and nothing else, which meant a player who died at 23 four times in a row had no way to
    /// tell whether their build was too small or the wrong shape.
    /// </para>
    /// <para>
    /// It gives a DIAGNOSIS, never a prescription. Every figure shown maps to something the player can
    /// change; a number that points at no lever is decoration and does not belong here.
    /// </para>
    /// </remarks>
    /// <summary>Set when a report was added, so the host knows to save. Cleared by the host.</summary>
    public bool LogDirty { get; set; }

    /// <summary>Open or close the log, and step through it. Called by the host from its key handling.</summary>
    public void ToggleLog()
    {
        _logOpen = !_logOpen;
        _logIndex = 0;
    }

    public bool LogOpen => _logOpen;

    public void StepLog(int dir)
    {
        if (Log.Count == 0) return;
        _logIndex = Math.Clamp(_logIndex + dir, 0, Log.Count - 1);
    }

    /// <summary>
    /// The log, as the same report panel with a way to walk backwards through it.
    /// </summary>
    /// <remarks>
    /// Deliberately the SAME drawing as the death overlay rather than a second, summarised view: the
    /// player has learned to read one layout, and a history that presented the same facts differently
    /// would make comparing two runs — the entire reason to keep them — harder, not easier.
    /// </remarks>
    public void DrawLog(SpriteBatch b, Point hit, bool clicked)
    {
        if (!_logOpen) return;

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xE6));

        if (Log.Count == 0)
        {
            _ui.TextCenterBig(b, "NO EXPEDITIONS YET", 960, 480, Slate, UiTypography.RegionTitle);
            _ui.TextCenter(b, "L CLOSES THIS.", 960, 540, Dim);
            return;
        }

        _logIndex = Math.Clamp(_logIndex, 0, Log.Count - 1);
        var shown = Log.Entries[_logIndex];
        var older = Log.OlderThan(_logIndex);

        _ui.TextCenterBig(b, "EXPEDITION LOG", 960, 120, Gold, UiTypography.ScreenTitle);
        _ui.TextCenter(b, $"{_logIndex + 1} OF {Log.Count}   \u00b7   \u2039 \u203a TO STEP   \u00b7   L CLOSES", 960, 172, Slate);

        DrawReportPanel(b, shown, older);

        var prev = new Rectangle(300, 480, 120, 64);
        var next = new Rectangle(1500, 480, 120, 64);
        if (_ui.Button(b, prev, "\u2039", hit, clicked) && _logIndex < Log.Count - 1) _logIndex++;
        if (_ui.Button(b, next, "\u203a", hit, clicked) && _logIndex > 0) _logIndex--;
    }

    private void DrawRunReport(SpriteBatch b)
    {
        if (_lastReport is not { } r)
        {
            _ui.TextCenterBig(b, "CHAMPION DOWN", ArenaRect.Center.X, 500, Ember, UiTypography.StageLabel);
            return;
        }

        DrawReportPanel(b, r, _previousReport);
    }

    /// <summary>
    /// The report itself. Shared by the death overlay and the log, deliberately.
    /// </summary>
    /// <remarks>
    /// One layout, two contexts. A history screen that summarised the same facts differently would make
    /// comparing two runs — the entire reason to keep them — harder rather than easier.
    /// </remarks>
    private void DrawReportPanel(SpriteBatch b, RunReport r, RunReport? previous)
    {
        // 690, not 620. The diff block below draws five lines and only three of them cleared the
        // bottom frame — the last two were painted over the panel's own ornament. The clipped lines
        // are the WALL and the ABSORBED delta: the two that answer "what stopped me" and "did my
        // change help", which is the entire reason the report exists.
        var panel = new Rectangle(ArenaRect.X + 40, 250, ArenaRect.Width - 80, 690);
        _ui.Panel(b, panel);
        var x = panel.X + 44;
        var right = panel.Right - 44;

        _ui.TextCenterBig(b, r.IsRecord ? $"NEW RECORD — DEPTH {r.Depth}" : $"DEPTH {r.Depth}",
            panel.Center.X, panel.Y + 34, r.IsRecord ? Gold : Ember, UiTypography.RegionTitle);

        // THE WALL — named plainly, because the player has to be able to go and look at it.
        var affixes = r.WallAffixes.Count > 0
            ? string.Join(" + ", r.WallAffixes.Select(a => a.ToString().ToUpperInvariant()))
            : "NO AFFIX";
        _ui.TextCenterBig(b, $"WAVE {r.WallWave}  ·  {r.WallArchetype.ToString().ToUpperInvariant()} x{r.WallCreatures}  ·  {affixes}",
            panel.Center.X, panel.Y + 88, UiKit.Vellum, UiTypography.OverlayBody);

        _ui.TextCenterBig(b, r.Verdict(), panel.Center.X, panel.Y + 126, Gold, UiTypography.Body);
        _ui.Fill(b, new Rectangle(x, panel.Y + 166, panel.Width - 88, 2), Slate * 0.4f);

        // THE MEASUREMENTS. Each line is a lever.
        var y = panel.Y + 186;
        void Row(string label, string value, string points)
        {
            _ui.TextBig(b, label, x, y, Slate, UiTypography.Secondary);
            _ui.TextBig(b, value, x + 300, y, UiKit.Vellum, UiTypography.Body);
            _ui.TextRightBig(b, points, right, y + 2, Slate, UiTypography.Secondary);
            y += 44;
        }

        Row("ARMOUR ABSORBED", $"{r.AbsorbedFraction:P0}", "hit size");
        Row("AVERAGE HIT", $"{r.AverageHitSize:F0}", "hit size");
        Row("REACH", $"{r.TargetsPerActivation:F1} of {r.CreaturesPerWave:F1} per cast", "action economy");
        Row("HEALTH LOST / WAVE", $"{r.HealthLostPerWaveFraction:P0}", "sustain");
        Row("SECONDS / WAVE", $"{r.SecondsPerWave:F1}s", "throughput");

        _ui.TextBig(b, $"measured over the last {r.SampledWaves} wave(s)", x, y + 4, Slate, UiTypography.Secondary);

        // THE DIFF — what changed since the last attempt here. This is what makes iteration legible.
        var diff = r.DiffAgainst(previous).ToList();
        if (diff.Count > 0)
        {
            var dy = y + 44;
            _ui.TextBig(b, "SINCE YOUR LAST RUN HERE", x, dy, Gold, UiTypography.Secondary);
            dy += 32;

            // AND CLAMPED TO THE ROOM THERE ACTUALLY IS. Growing the panel fixes today's five lines;
            // this is what stops the next line added to RunReport.DiffAgainst from silently going
            // under the frame again. A report that quietly drops its last row is better than one that
            // draws it where it cannot be read — and the count is derived, so neither happens.
            const int lineHeight = 28;
            var lastBaseline = panel.Bottom - UiKit.PanelCorner - 22;
            var room = Math.Max(0, (lastBaseline - dy) / lineHeight + 1);

            foreach (var line in diff.Take(Math.Min(5, room)))
            {
                _ui.TextBig(b, line, x, dy, UiKit.Vellum, UiTypography.Secondary);
                dy += lineHeight;
            }
        }
    }

    /// <summary>
    /// The first-run guide: one lesson, low on the arena, out of the way of the fight.
    /// </summary>
    /// <remarks>
    /// Deliberately not a modal and not a pointer. An idle game's first minute is spent WATCHING, so the
    /// guide has to be readable without stopping anything and ignorable without dismissing anything — a
    /// step completes when its lesson is performed, so a player who already knows the loop never has to
    /// acknowledge a single one of these.
    /// </remarks>
    private void DrawGuide(SpriteBatch b)
    {
        if (Guide is not { } step || !Tutorial.HasGuidance(step)) return;

        var width = ArenaRect.Width - 160;
        var x = ArenaRect.X + 80;
        var body = _ui.WrapBig(Tutorial.Body(step), width - 40, UiTypography.Secondary);
        var height = 58 + body.Count * 22;
        var y = ArenaRect.Bottom - height - 26;

        _ui.Fill(b, new Rectangle(x, y, width, height), new Color(0x10, 0x0D, 0x18, 0xE6));
        _ui.Fill(b, new Rectangle(x, y, 5, height), Gold);

        _ui.TextBig(b, Tutorial.Title(step), x + 22, y + 14, Gold, UiTypography.Body);
        var ty = y + 44;
        foreach (var line in body)
        {
            _ui.TextBig(b, line, x + 22, ty, UiKit.Vellum, UiTypography.Secondary);
            ty += 22;
        }
    }

    /// <summary>The single active arena announcement (Rev 4 §2/§18) — never more than one at a time.</summary>
    private void DrawArenaOverlay(SpriteBatch b, HuntOverlay overlay)
    {
        switch (overlay)
        {
            case HuntOverlay.HunterDown:
                DrawRunReport(b);
                break;
            case HuntOverlay.BossIncoming:
            {
                var fade = Math.Clamp(_bossIncomingTimer * 1.4f, 0f, 1f);
                _ui.Fill(b, new Rectangle(610, 200, 700, 110), PanelBg * fade);
                _ui.TextCenterBig(b, "BOSS INCOMING", 960, 224, Gold * fade, UiTypography.RegionTitle);
                _ui.TextCenterBig(b, "STEEL YOURSELF", 960, 274, Bone * fade, UiTypography.OverlayBody);
                break;
            }
            case HuntOverlay.WaveCleared:
            {
                var fade = Math.Clamp(_bannerTimer * 1.4f, 0f, 1f);
                _ui.TextCenterBig(b, _bannerText, 960, 268, Gold * fade, UiTypography.RegionTitle);
                break;
            }
        }
    }

    private const int ConquerAt = 7;   // mirrors Game1.ConquerWaveDepth — shown so the goal is visible

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x9A, 0x7A, 0xD8), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };
    private static string FormShort(Form f) => f switch
    {
        Form.Projectile => "VOLLEY", Form.Transformation => "MORPH", _ => f.ToString().ToUpperInvariant(),
    };

    /// <summary>Top-left hunter HUD (package_10 region A): portrait medallion, name/level, HP, power, and a
    /// row of Source icons for the build's elements.</summary>
    private void DrawHunterHud(SpriteBatch b)
    {
        // Spec §8: player summary, exact rectangles. Ornate primary frame; portrait AspectFit (it already
        // carries its own frame — no second medallion); name/level/power/HP/tempo/source-icons per §8.3.
        var panel = new Rectangle(196, 20, 420, 205);
        _ui.Panel(b, panel);

        var por = new Rectangle(214, 39, 104, 104);
        if (_ui.Assets.Get("hunter_portrait") is { } p) b.Draw(p, por, Color.White);
        else if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, por, Color.White);

        var adept = Mastery.Affinity() is { } mf ? $"{FormShort(mf)} ADEPT" : "SEEKER";
        _ui.TextBig(b, adept, 334, 43, Bone, 26);                                   // name
        _ui.TextBig(b, $"LV {_hunter?.HunterLevel ?? 1}", 334, 80, Gold, 20);        // level
        // Combat power — an icon + value (spec: an icon, not a "PWR" label).
        if (_ui.Assets.Get("state_resonance_128") is { } pi) b.Draw(pi, new Rectangle(440, 74, 30, 30), Ember);
        _ui.TextBig(b, Game1.Abbrev(_hunter?.PowerRating ?? 0), 476, 76, Ember, 24);
        // HP bar (162,112,220,24), value centred on it.
        var hp = Math.Max(0, _replay?.HealthOf(0) ?? 0);
        var hpBar = new Rectangle(334, 112, 220, 24);
        _ui.BarArt(b, hpBar, _replay?.HealthFractionOf(0) ?? 1f, "health");
        _ui.TextCenterBig(b, $"{hp}/{_champ?.MaxHealth ?? 0}", hpBar.Center.X, hpBar.Y + 3, Bone, 16);
        // Secondary stat line — TEMPO × skill rate (spec §8.6: no fake mana bar; real SquadSkillRate).
        _ui.TextBig(b, $"TEMPO {(_hunter?.SquadSkillRate ?? 1f):0.00}x SKILL RATE", 334, 145, Slate, 15);

        // Source icons — the build's real elements. These sat at x=42, coordinates from the layout where
        // the HUD panel started near the left edge. Once the panel moved to x=196 to clear the vertical nav
        // rail, the row stayed behind and painted four medallions onto the NAV RAIL, where they showed
        // through its 96%-opaque shelf as ghosts beside the GEAR tile. Aligned to the panel's text column.
        var sx = 334;
        foreach (var s in Loadout.Skills)
        {
            var box = new Rectangle(sx, 172, 32, 32);
            if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g) b.Draw(g, box, Color.White);
            else _ui.Diamond(b, box, SourceColor.GetValueOrDefault(s.Source, Slate));
            sx += 40;
        }
    }

    /// <summary>Top-center stage header (region B): region name, current wave, and the conquest progress bar.</summary>
    private void DrawStageHeader(SpriteBatch b, string regionName, bool isBossWave)
    {
        // Rev 3 §12: stage header (630,18,560,135) — narrower, so it clears the currency bar (≥20px gap). One
        // clean hierarchy region → depth → progress → wave, all centred at x=910 (the banner centre).
        var bar = new Rectangle(630, 18, 560, 135);
        if (_ui.Assets.Get("ui_panel_modal_wide") is { } bg) b.Draw(bg, bar, Color.White);
        else _ui.Panel(b, bar);
        const int cx = 910;
        // §19.2: render the region title at 36, shrinking to a floor of 28 to fit 490px — never ellipsize the
        // ACTIVE region title. (Two-line fallback below 28 is a noted follow-up; region names fit at 28.)
        var title = regionName.ToUpperInvariant();
        var titlePx = UiTypography.RegionTitle;
        while (titlePx > 28 && _ui.MeasureBig(title, titlePx) > 490) titlePx--;
        _ui.TextCenterBig(b, title, cx, 32 + (UiTypography.RegionTitle - titlePx) / 2, Gold, titlePx);
        _ui.TextCenterBig(b, Deepest >= ConquerAt ? "CONQUERED" : $"DEPTH {Deepest} / {ConquerAt}",
            cx, 73, Deepest >= ConquerAt ? Gold : Bone, UiTypography.StageLabel);
        _ui.BarArt(b, new Rectangle(710, 102, 400, 18),
            ConquerAt > 0 ? Math.Clamp(Deepest / (float)ConquerAt, 0f, 1f) : 0f, "progress");
        _ui.TextCenterBig(b, $"WAVE {_run!.Wave + 1}", cx, 124, isBossWave ? Gold : Bone, UiTypography.OverlayTitle);
    }

    private static readonly Color PlateEdge = new(0x74, 0x62, 0x3E);

    /// <summary>A titled panel using the ORNATE ui_panel_* frame art (package_01) — the reference's look.
    /// The title is left-aligned just inside the top edge so it clears the frame's top-centre gem. Returns
    /// the inner content rect (inset past the ornate corners).</summary>
    private Rectangle CleanPanel(SpriteBatch b, Rectangle r, string title)
    {
        _ui.Panel(b, r);
        _ui.TextBig(b, title, r.X + 44, r.Y + 26, Gold, 20);
        return new Rectangle(r.X + 44, r.Y + 70, r.Width - 88, r.Height - 92);
    }

    /// <summary>Right context column (reference regions): a stack of four plates — Idle Rewards, Objective,
    /// Loot, Expedition — all fed from live run data.</summary>
    private void DrawRightColumn(SpriteBatch b)
    {
        // Rev 3 §20: right context rail (1570,110,326,690) — secondary panels, all live data, NO keyboard
        // hints (§21). Idle-rate → objective → reward activity → expedition.
        const int px = 1570, pw = 326;

        // Idle rate (1570,110,326,130). Auto-credited, so no Claim button (§20.2).
        var inner = CleanPanel(b, new Rectangle(px, 110, pw, 130), "IDLE RATE");
        if (_ui.Assets.Get("currency_gleam") is { } gi) b.Draw(gi, new Rectangle(inner.X, inner.Y + 2, 38, 38), Color.White);
        _ui.TextBig(b, $"+{Game1.Abbrev((long)(IdleGleamRate * 60f))}/min", inner.X + 48, inner.Y + 8, Gold, 24);

        // Objective (1570,254,326,150).
        inner = CleanPanel(b, new Rectangle(px, 254, pw, 150), "OBJECTIVE");
        _ui.TextBig(b, Deepest >= ConquerAt ? "Conquered" : $"Reach depth {ConquerAt}", inner.X, inner.Y, Bone, 19);
        _ui.TextRightBig(b, $"{Math.Min(Deepest, ConquerAt)} / {ConquerAt}", inner.Right, inner.Y, Gold, 18);
        _ui.BarArt(b, new Rectangle(inner.X, inner.Y + 40, inner.Width, 26), ConquerAt > 0 ? Math.Clamp(Deepest / (float)ConquerAt, 0f, 1f) : 0f, "progress");

        // Reward activity (1570,418,326,210) — real summary, no fake loot grid, no keyboard hints (§20.4/§21).
        inner = CleanPanel(b, new Rectangle(px, 418, pw, 210), "REWARD ACTIVITY");
        var ry = inner.Y;
        var anyReward = false;
        if (ChestCount > 0)
        {
            _ui.TextBig(b, $"{ChestCount} chest{(ChestCount == 1 ? "" : "s")} available", inner.X, ry, Bone, 18); ry += 40; anyReward = true;
        }
        if (Mastery.Available > 0)
        {
            _ui.TextBig(b, $"{Mastery.Available} Mastery Point{(Mastery.Available == 1 ? "" : "s")}", inner.X, ry, Bone, 18); ry += 40; anyReward = true;
        }
        if (Deepest > 0) _ui.TextBig(b, $"Deepest wave reached: {Deepest}", inner.X, ry, Slate, 16);
        else if (!anyReward) _ui.TextBig(b, "No rewards pending", inner.X, inner.Y, Slate, 18);

        // Expedition (1570,642,326,145) — region wave + live status.
        inner = CleanPanel(b, new Rectangle(px, 642, pw, 145), "EXPEDITION");
        _ui.TextBig(b, $"Wave {_run!.Wave + 1}", inner.X, inner.Y, Bone, 19);
        var state = _mode == Mode.Downed ? "RECOVERING"
            : WaveScaling.IsBossWave(_run.Wave + 1, ExpeditionTuning.Default) ? "BOSS WAVE" : "ACTIVE";
        _ui.TextRightBig(b, state, inner.Right, inner.Y + 2, _mode == Mode.Downed ? Ember : state == "BOSS WAVE" ? Gold : Verdant, 16);
    }

    /// <summary>Auto-skill dock (region F): the build as circular auto-cast medallions, centered under the
    /// arena. Each shows its Source glyph, its Form, and the AUTO state the reference calls for.</summary>
    private void DrawSkillDock(SpriteBatch b)
    {
        // Spec §14: auto-skill dock centred under the arena (500,770,920,145). Hex slots (ui_slot_skill_hex),
        // Source glyph inside, Form + AUTO beneath. Presentation Model C (§14.4): AUTO/READY, no fake cooldowns.
        var skills = Loadout.Skills;
        var n = Loadout.SkillCapacity;     // the dock shows the slots you have, not the four everyone starts with
        // Stacked down the control rail instead of a horizontal dock across the bottom centre, which
        // sat over the stage and collided with the nav rail at y=934.
        const int slot = 68, pitch = 74, y0 = 632;
        _ui.TextBig(b, "SKILLS", RailContentX, 608, Slate, 15);
        _ui.Fill(b, new Rectangle(RailContentX, 588, RailContentW, 2), Slate * 0.35f);
        for (var i = 0; i < n; i++)
        {
            var box = new Rectangle(RailContentX, y0 + i * pitch, slot, slot);
            if (i < skills.Count)
            {
                var s = skills[i];
                var sc = SourceColor.GetValueOrDefault(s.Source, Bone);
                if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl) b.Draw(sl, box, Color.White);
                // §18.3 Layer 1: source-coloured inner glow (no Form-glyph asset ships, so the Source glyph is
                // the central identity and the Form name labels it — a quieter composition per §36).
                _ui.Diamond(b, new Rectangle(box.Center.X - 22, box.Center.Y - 22, 44, 44), sc * 0.28f);
                if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g)
                    b.Draw(g, new Rectangle(box.X + 16, box.Y + 14, box.Width - 32, box.Height - 28), Color.White);
                else _ui.Diamond(b, new Rectangle(box.Center.X - 24, box.Center.Y - 24, 48, 48), sc);
                // (§16 Vow glyph omitted: the loadout SkillChoice doesn't carry the Vow — it lives on the built
                // ability. Wiring the built skills through would add it; deferred, logged once below.)
                // §16.2: a DESCRIPTIVE Source+Form label ("SHADOW STRIKE"), never a bare form name — shrunk to
                // fit the slot pitch rather than clipped.
                var label = $"{s.Source.ToString().ToUpperInvariant()} {FormShort(s.Form)}";
                var lpx = UiTypography.Secondary;
                while (lpx > 12 && _ui.MeasureBig(label, lpx) > 128) lpx--;
                // Beside the slot, not beneath it: the rail is narrow and tall, so the label reads
                // left-aligned in the space to the slot's right.
                _ui.TextBig(b, label, box.Right + 12, box.Y + 6, Bone, lpx);
                _ui.TextBig(b, "AUTO", box.Right + 12, box.Y + 32, Gold, 13);
            }
            else
            {
                if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl) b.Draw(sl, box, Color.White * 0.5f);
                _ui.TextCenterBig(b, i == skills.Count ? "+B" : "-", box.Center.X, box.Center.Y - 10, Dim, 16);
            }
        }
    }

    /// <summary>Bottom-left battle controls (reference region G): adjustable BATTLE SPEED (x1/x2/x4/x8) driving
    /// the real replay multiplier, plus the AUTO HUNT state.</summary>
    private void DrawBattleControls(SpriteBatch b, Point mouse, bool clicked)
    {
        // Spec §15 + §4.4: speed / auto-hunt controls, bottom-left (24,785,250,140). A QUIET surface (not a
        // heavy ornate frame) — these are minor controls; the arena stays dominant. SPEED drives the real
        // replay multiplier; AUTO HUNT is always ON (this is a pure idle-watch screen).
        // Ornate frame art, like every other panel on this screen. A flat translucent slab was the only
        // unstyled surface left in the scene, and because its alpha was constant it changed apparent
        // brightness with whatever background it happened to be over — dark against the trunk, washed out
        // against the foliage. A real panel is opaque, so it reads the same everywhere.
        _ui.Panel(b, ControlRail);

        _ui.TextBig(b, "BATTLE SPEED", RailContentX, 268, Slate, 15);
        for (var i = 0; i < SpeedSteps.Length; i++)
        {
            var r = new Rectangle(RailContentX, 292 + i * 52, RailContentW, 44);
            var on = Math.Abs(_speedMul - SpeedSteps[i]) < 0.01f;
            var hover = r.Contains(mouse);
            _ui.Fill(b, r, on ? Gold : hover ? new Color(0x2C, 0x25, 0x44) : new Color(0x1A, 0x14, 0x28));
            // Vertical centring: the old "r.Y + 8" was tuned for a 34px-tall button and sits too high
            // in a 44px one. Glyph box is ~1.35x the point size.
            _ui.TextCenterBig(b, $"x{(int)SpeedSteps[i]}", r.Center.X, r.Y + (r.Height - 16 * 27 / 20) / 2,
                on ? Shadow : Bone, 16);
            if (clicked && hover) _speedMul = SpeedSteps[i];
        }

        _ui.TextBig(b, "AUTO HUNT", RailContentX, 508, Slate, 15);
        var auto = new Rectangle(RailContentX, 532, RailContentW, 36);
        _ui.Fill(b, auto, new Color(0x1A, 0x30, 0x22));
        _ui.Fill(b, new Rectangle(auto.X, auto.Y, auto.Width, 3), Verdant * 0.7f);
        _ui.TextCenterBig(b, "ON", auto.Center.X, auto.Y + (auto.Height - 16 * 27 / 20) / 2, Verdant, 16);
    }

    // THE GEAR OVERLAY IS GONE, by both routes it ever had.
    //
    // First a socket table: fixed fractions of the champion box, one per slot. That cannot track an
    // articulated figure — a hand moves every frame, so any constant is wrong for all but one pose —
    // and in play the weapon floated across the torso and the glove sat on the hip.
    //
    // Then bone-bound gear on the rig, which fixed the tracking and did not fix the look: an item icon
    // carries its own perspective, light and palette, so it reads as a sticker wherever it is pinned,
    // and four independently-chosen pieces never agree with each other. See HunterRigRenderer.
    //
    // The champion's appearance is FIXED. Gear is still worn, still carries every stat, and still shows
    // in GEAR and the inventory — it just no longer decides what the character looks like.

    /// <summary>
    /// The vertical control rail down the left edge, replacing the old horizontal bottom strip.
    /// </summary>
    /// <remarks>
    /// Bounded above by the hunter HUD panel (bottom 225) and below by the host nav rail (top 934),
    /// so it occupies the full clear height between them. Moving the controls here is what frees the
    /// stage to shift right.
    /// </remarks>
    // Sits just right of the vertical nav rail (width 180).
    // Below the hunter HUD (bottom 225) and right of the nav rail (width 180).
    private static readonly Rectangle ControlRail = new(190, 236, 286, 700);
    // Inset by the ornate frame's border (UiKit.Panel reserves 44px), not by the 20px a flat slab needed.
    private const int RailContentX = 234;
    private const int RailContentW = 198;

    private float _strikeTime;

    /// <summary>
    /// DEV: hold the strike clip at a fixed point (0..1 of its duration) instead of letting combat drive it.
    /// </summary>
    /// <remarks>
    /// A one-second screenshot lands wherever the fight happens to be, which is almost never mid-swing,
    /// so the frame that most wants checking is the one no capture reliably catches.
    /// </remarks>
    public float? DevSwingPhase;

    /// <summary>The character being played. The host sets it; it decides which sprite strip is drawn.</summary>
    public Character Character { get; set; } = CharacterRoster.Get(CharacterRoster.StarterId);

    /// <summary>
    /// Draw the champion from the active character's animation strip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaced an assembled cutout RIG. The rig articulated real limbs and charged for it in
    /// constraints that reached all the way back into the art: every source body had to stand in a wide
    /// A-pose with a measurable gap under each armpit and between the legs, hold nothing, and wear
    /// nothing that hung, because a rectangle cannot separate an arm from a cloak behind it. With one
    /// character that was a curiosity. With ten it was the whole art direction, decided by the renderer.
    /// </para>
    /// <para>
    /// A strip is also FEWER assets, not more — one sprite and two eight-frame clips per character,
    /// against seventeen cut parts, a drawn pauldron and a pose table — and the motion is authored
    /// rather than assembled from rotating rectangles, which the rig could never get past: its own
    /// death pose is capped at 0.4 rad with a comment explaining that flat cutouts visibly come apart
    /// beyond it.
    /// </para>
    /// <para>
    /// Falls back to the still base sprite when a clip is missing, so a character whose strips have not
    /// been generated yet still shows the right person standing still rather than nothing at all.
    /// </para>
    /// </remarks>
    private void DrawChampion(SpriteBatch b, Rectangle box, bool dead)
    {
        // Soft contact shadow, not a hard bar: the flat rectangle that shipped here read as a painted
        // slab under the feet rather than as the figure touching the floor.
        // THE FALL. There is no death CLIP — every character ships an idle and an attack strip and
        // nothing else — so "dead" was drawn as the idle loop in grey, which is a corpse standing up
        // and breathing. That is the whole of "the death animation looks strange".
        //
        // Without new art the honest reading is a COLLAPSE: freeze the pose (a dead thing does not
        // animate), sink it into the floor, shrink it as it goes, and fade. Three cheap channels moving
        // together read as falling far better than one grey loop reads as dying, and the shadow
        // contracts with it so the figure stays planted rather than sliding down a wall.
        var fallen = dead ? 1f - Math.Clamp(_downedTimer / DownedSeconds, 0f, 1f) : 0f;
        var ease = fallen * fallen * (3f - 2f * fallen);   // smoothstep — a body accelerates, then settles

        if (dead)
        {
            // The drop and the shrink are the SAME number on purpose. Sprites are bottom-anchored, so
            // moving the box down by d and shortening it by d leaves the feet exactly where they were
            // and brings the head down — a body folding onto the floor. When the two differed the feet
            // travelled too, and the figure read as sinking THROUGH the ground rather than falling onto
            // it, which is a different and much worse animation.
            var fold = (int)(box.Height * 0.42f * ease);
            box = new Rectangle(box.X, box.Y + fold, box.Width, Math.Max(8, box.Height - fold));
        }

        _ui.GroundShadow(b, box.Center.X, box.Bottom - 10,
                         (int)(box.Width * (0.62f - 0.18f * ease)), 46, 0.6f - 0.25f * ease);

        var tint = dead
            ? Color.Lerp(new Color(0x6A, 0x5A, 0x62), new Color(0x2A, 0x28, 0x34), ease) * (1f - 0.45f * ease)
            : Color.White;

        // ATTACK does not loop, and is driven by the combat beat rather than its own clock — the same
        // rule the rig's strike clip followed, for the same reason: a swing that runs free drifts out of
        // step with the hit it is supposed to be delivering.
        var swinging = _strikeTime > 0f && !dead;
        var clip = swinging ? "attack" : "idle";
        var seconds = DevSwingPhase is { } ph && !dead
            ? ph * StrikeSeconds
            : swinging ? StrikeSeconds - _strikeTime : _anim;

        // A DEAD CHAMPION HOLDS ITS LAST POSE. Passing the free clock here would keep the idle strip
        // cycling under the tint and the sink — the figure would sag into the floor while still walking
        // on the spot, which is worse than either treatment alone.
        if (dead) seconds = 0f;

        // The generated strip, then the base sprite, then a block.
        //
        // The strips were briefly abandoned on a wrong diagnosis worth recording: the first clip came
        // back as a waist-up bust and the animator was blamed for re-framing its input. It was not.
        // The BASE SPRITE was a bust at that moment — the hero style had returned one and "full body"
        // in the prompt had not been enough to stop it — so the clip was a faithful animation of a
        // cropped design. Padding the source (animate.py:pad_to_fraction) is what keeps the framing,
        // and it works.
        //
        // A missing or rejected clip falls back to the still design, so a character whose strip did
        // not survive the quality gate stands there as themselves rather than vanishing.
        if (_ui.AnimSprite(b, Character.StripKey(clip), box, seconds, ChampionFps, loop: !swinging, tint, -1f)) return;
        if (_ui.AnimSprite(b, Character.StripKey("idle"), box, _anim, ChampionFps, loop: true, tint, -1f)) return;

        var breathe = (int)(MathF.Sin(seconds * 2.1f) * 4f);
        if (_ui.SpriteGrounded(b, Character.SpriteKey,
                               new Rectangle(box.X, box.Y + breathe, box.Width, box.Height), tint, 0.02f)) return;

        _ui.Fill(b, new Rectangle(box.Center.X - 32, box.Bottom - 80, 64, 72), dead ? Dim : Gold);
    }

    /// <summary>How long a strike clip runs. Eight frames at the champion's frame rate.</summary>
    private const float StrikeSeconds = 8f / ChampionFps;
    private const float ChampionFps = 10f;

    /// <summary>
    /// Draw the champion's base body, bottom-anchored inside the box.
    /// </summary>
    /// <remarks>
    /// It used to layer one <c>overlay_&lt;slot&gt;_&lt;pose&gt;</c> per equipped slot on top. That path is gone
    /// with the rest of the wardrobe — the appearance is fixed.
    /// </remarks>
    private void DrawLayeredChampion(SpriteBatch b, Rectangle box, Texture2D baseTex, string pose, Color tint)
    {
        var draw = new Rectangle(box.X + 8, box.Y + 8, box.Width - 16, box.Height - 40);
        var sc = MathF.Min(draw.Width / (float)baseTex.Width, draw.Height / (float)baseTex.Height);
        var w = Math.Max(1, (int)(baseTex.Width * sc));
        var h = Math.Max(1, (int)(baseTex.Height * sc));
        var rect = new Rectangle(draw.Center.X - w / 2, draw.Bottom - h, w, h);

        b.Draw(baseTex, rect, tint);
    }

    private void DrawCallouts(SpriteBatch b)
    {
        foreach (var c in _callouts)
        {
            var rise = (int)((1f - c.Life) * 40f);
            var fade = Math.Clamp(c.Life * 1.8f, 0f, 1f);
            _ui.TextCenterBig(b, c.Text, c.X, c.Y - rise, c.Color * fade, c.Px <= 0 ? 36 : c.Px);
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
    /// DEV: run a whole descent to its end immediately, so the post-run report can be captured.
    /// </summary>
    /// <remarks>
    /// The report only exists after a run ends, and when a run ends is a property of the build — so a
    /// timed screenshot can never reliably catch it. This is the same reasoning as RH_SHOT_SWING: the
    /// states that most need checking are the ones a clock cannot be aimed at.
    /// </remarks>
    /// <summary>
    /// DEV: hold the post-run report open instead of restarting after the recovery beat.
    /// </summary>
    /// <remarks>
    /// The recovery beat is 1.6s and a headless capture lands at frame 60, which ought to be inside it —
    /// but the first frames of a run carry loading time, so the timer had already expired and the shot
    /// caught a fresh descent every time. Holding is the honest fix for a fixture: it changes when the
    /// screen leaves, not what it shows.
    /// </remarks>
    public bool DevHoldReport { get; set; }

    /// <summary>DEV: freeze in the downed beat but keep the report OFF, to photograph the fall.</summary>
    /// <remarks>
    /// Separate from <see cref="DevHoldReport"/> because the two jobs are different and conflating them
    /// broke the capture: holding is what stops the timer running out and restarting the run, and
    /// suppressing is what keeps the panel off the body. The first attempt cleared the hold in order to
    /// suppress, so the beat simply expired mid-capture and photographed a fresh wave instead.
    /// </remarks>
    public bool DevShowFall { get; set; }

    /// <param name="fallProgress">
    /// 0 poses the instant of the fall, 1 the settled body. Anything other than null ALSO suppresses the
    /// report, so the collapse can be photographed uncovered — the report is a large centred panel and
    /// sits directly on top of the thing this poses.
    /// </param>
    public void DevRunToDeath(Hunter hunter, float? fallProgress = null)
    {
        DevHoldReport = true;
        DevStart(hunter, 900f, 14f);

        // DevStart already plays wave 1, and at these numbers that wave can be the one that kills. The
        // first version checked _run.Over at the TOP of the loop and so broke out without ever building
        // the report — the capture caught a fresh descent every time.
        var guard = 0;
        while (_run is { Over: false } && guard++ < 400) _outcome = _run.PushWave();

        if (_run is null) return;
        _previousReport = null;
        _previousReport = Log.PreviousIn(RegionId);

        // THE REAL COMPARISON, not a hard true. Forcing the flag made the capture print
        // "NEW RECORD — DEPTH 52" directly above its own "DEPTH 53.0 -> 52.0 (-1.0)" line, which is a
        // report contradicting itself in the same panel. A fixture that lies cannot catch the bug it is
        // posing for — and the live path had exactly this bug until yesterday, announcing a record on
        // the first run of every session because nothing restored the figure to beat.
        _lastReport = _run.Report(isRecord: _run.Wave > (_previousReport?.Depth ?? 0));
        Log.Add(_lastReport);   // the fixture must exercise the same path the game does
        _mode = Mode.Downed;
        _downedTimer = DownedSeconds;
        _bannerTimer = 0f;   // the wave-cleared banner would otherwise sit over the report

        if (fallProgress is { } fp)
        {
            // Wind the downed clock to a chosen point in the fall and FREEZE it there. The collapse is
            // driven by how much of the beat has elapsed, so this is the only way to photograph a moment
            // of it — a capture takes one frame, and the fall is over in half a second.
            _downedTimer = DownedSeconds * (1f - Math.Clamp(fp, 0f, 1f));
            DevShowFall = true;   // hold stays ON so the beat cannot expire mid-capture
        }
    }

    /// <summary>DEV: start a run and play partway into a wave, for screenshots.</summary>
    public void DevStart(Hunter hunter, float ehp, float edmg)
    {
        _hunter = hunter;
        _enemyBaseHealth = ehp;
        _enemyBaseDamage = edmg;
        _lastSource = EnemySource;
        StartRun(hunter);
        _playheadMs = 900f;
    }
}
