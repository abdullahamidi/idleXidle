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
    /// <summary>
    /// The one speed the fight plays at. There is no control for it any more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The HUD used to offer x1/x2/x4/x8, defaulting to x2. It was removed on the designer's call, and
    /// the call is right for a reason worth writing down: <b>a wave is resolved instantly by the
    /// simulation and then REPLAYED, so the multiplier was not "how fast you watch" — it was how many
    /// waves per real second, and therefore a multiplier on every Gleam, material and chest.</b> A
    /// control that hands the player their own income rate is not a choice; it is a button everyone
    /// presses once and then the economy is whatever they picked. It was also leaking into the OFFLINE
    /// rate, which is how x8-then-quit paid four times as much.
    /// </para>
    /// <para>
    /// x1, not the old x2 default: the standing complaint is that the fight is too fast to read.
    /// </para>
    /// </remarks>
    private const float PlaybackSpeed = 1.0f;
    private const float _speedMul = PlaybackSpeed;

    /// <summary>
    /// How fast the replay is being watched. Read by the host to keep the OFFLINE rate honest.
    /// </summary>
    /// <remarks>
    /// A wave is resolved instantly by the simulation and then REPLAYED, so this multiplier is also a
    /// multiplier on waves-per-real-second — and therefore on everything a wave pays. While the player
    /// is watching, that is a legitimate fast-forward. It must NOT leak into the offline rate, which is
    /// measured live and then applied to hours when nobody is watching anything.
    /// </remarks>
    public float SpeedMultiplier => _speedMul;
    // ── THE WAVE TRANSITION, as named beats ─────────────────────────────────────────────────────
    //
    // It used to be one 0.8s pause followed by a 0.4s slide-in, and the designer's note was exactly
    // right: "ölüm animasyonları ve düşmanların sahneye gelişi çok hızlı oluyor, anlaşılmıyor." The
    // wave ended, a banner appeared, and the next wave was already walking in — the moment a player is
    // supposed to READ (what died, and what it paid) had no time of its own at all.
    //
    // Three beats now, each tunable on its own line, and each answering one question:
    //   FALLEN  — what just died. The death effect gets room to finish before anything else moves.
    //   SPOILS  — what it paid. The haul rises from where the creatures actually stood, so the reward
    //             is attached to the thing that dropped it rather than to a banner in the corner.
    //   BREATH  — an empty stage, so the next wave arrives as an arrival and not as a continuation.
    //
    // Derived from one countdown rather than a state machine: _breakTimer already existed, already
    // gated re-entry correctly, and a second source of truth for "where are we in the transition" is
    // how these things drift.
    private const float FallenBeat = 0.70f;
    private const float SpoilsBeat = 1.00f;
    private const float BreathBeat = 0.35f;
    private const float WaveBreakSeconds = FallenBeat + SpoilsBeat + BreathBeat;
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

    /// <summary>
    /// Does the champion need mirroring to face the enemies?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>DERIVED FROM THE LAYOUT, not asserted.</b> The champion stands at x=760 and the enemies at
    /// x=1320, so the champion faces RIGHT — and if either box ever moves, this follows rather than
    /// silently becoming wrong. The whole bug being fixed here is a direction that was decided in one
    /// place (the lunge offsets, the slide-in) and ignored in another (the draw).
    /// </para>
    /// <para>
    /// The generated character art faces LEFT: the roster's attack strips wind up and swing toward the
    /// left of the frame, which before this was away from everything they were hitting. Playtest:
    /// "Karakter animasyonları ters tarafa oynuyor gibi." The ENEMY art already faces left and is
    /// therefore already correct — enemies stand on the right and their target is on the left — so
    /// only the champion is mirrored. Flipping both would break the half that worked.
    /// </para>
    /// <para>
    /// The idle strip is close to front-on, so mirroring costs it nothing visible; the attack strip is
    /// where the direction actually reads.
    /// </para>
    /// </remarks>
    // 2026-08-22 ART CONTRACT (design/art/arena-art-contract.md): the champion strips are generated on
    // the SOUTH-EAST rotation and face RIGHT natively; enemies and bosses are generated SOUTH-WEST and
    // face LEFT. Nothing is mirrored at draw time any more — the flip below stays derived so that a
    // future layout change (or a wrong-facing asset) is one constant away from correct.
    private const bool ArtFacesLeft = false;

    private static bool ChampionFacesRight => ArtFacesLeft && ChampBox.Center.X < EnemyBox.Center.X;
    // 2026-08-22: the boss is an ordinary 8x512 strip on a clean canvas (design/art/arena-art-contract.md),
    // so the per-boss BODY-bounds table that used to live here (SrcTop / BodyX.. measured off the old
    // crystal_lich render, with a "bleed streak" to trim) is gone. The name is the only per-boss fact left.
    private static readonly Dictionary<string, string> BossNameFor = new()
    {
        ["thorn_regent"] = "THORN REGENT", ["forge_colossus"] = "FORGE COLOSSUS", ["void_reaper"] = "VOID REAPER",
        ["crystal_lich"] = "CRYSTAL LICH", ["lumen_angel"] = "LUMEN ANGEL", ["spirit_matron"] = "SPIRIT MATRON",
    };
    private const int BossTargetBodyHeight = 540;   // §6/§25: rendered figure height — the boss box is a 540 square
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

    /// <summary>Seconds of flash left on each Form's medallion, keyed by (int)Form. Set when it fires.</summary>
    /// <remarks>
    /// Playtest: "Skill kullanımlarını ve cooldownlarını da takip edemiyorum." The screen already knew
    /// the moment a skill went off — it plays a VFX and a callout for it — but nothing on the rail, the
    /// one place the skills are listed, moved at all. So the medallions were a legend, not an instrument.
    /// </remarks>
    private readonly Dictionary<int, float> _skillFlash = new();

    // CHARGE — latched off Charge events at the playhead, never re-derived (a replayed rule can
    // drift from the sim's). Live only when the build carries a keystone that reads the pool.
    private int _chargeNow;
    private bool _chargeLive;
    private int _chargeCap = SoloBattle.ChargeCap;
    private WaveReplay? _replay;
    private float _champLunge, _enemyLunge, _enemyWindup;
    /// <summary>Where DrawComposition last put the creature row — what its labels anchor to.</summary>
    /// <remarks>
    /// Published rather than recomputed because the row's position is the product of an archetype
    /// scale, a spacing compression and two clamps, and every place that re-derived it got a different
    /// answer from the one on screen.
    /// </remarks>
    /// <summary>Set when the rail's reward buttons are pressed — the host navigates, the screen does not.</summary>
    public bool WantsVault { get; set; }
    public bool WantsBuild { get; set; }

    private int _rowCentreX = EnemyBox.Center.X;
    private int _rowTopY = EnemyBox.Y;

    private int _nextEnemyStrikeMs;

    /// <summary>When the champion's next blow lands, so its swing can ANTICIPATE the hit.</summary>
    /// <remarks>
    /// The mirror of <see cref="_nextEnemyStrikeMs"/>. Before this the two actors ran in opposite
    /// animation phase: the enemy's clip was scrubbed across the 600ms BEFORE its blow (so it completed
    /// at impact) while the champion's was ARMED BY the impact and therefore played entirely afterwards.
    /// The champion struck and then wound up. Playtest: "animasyon geçişleri ve saldırma animasyonları
    /// garip görünüyor."
    /// </remarks>
    private int _nextChampStrikeMs;

    /// <summary>0 to 1 across the champion's swing, completing exactly as the blow lands.</summary>
    private float _champWindup;
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

    /// <summary>Where the enemy nameplate sits, relative to the row anchor, and how tall it is.</summary>
    private const int NameplateOffset = -52, NameplateHeight = 34;

    /// <summary>
    /// Where the enemy callout stack begins — clear above the nameplate.
    /// </summary>
    /// <remarks>
    /// <b>THE STACK USED TO CLIMB STRAIGHT THROUGH THE HEALTH BAR.</b> Damage numbers were spawned at
    /// <c>_rowTopY + 40</c> and each further one 54px higher, while the nameplate is drawn at
    /// <c>_rowTopY - 52</c> — so the first number was below the bar, the second landed on it, and the
    /// third cleared it. Nothing was wrong with either piece of code; they simply did not know about
    /// each other, and the collision only appears on the second hit of a burst, which is exactly the
    /// moment a player is looking at the number.
    ///
    /// Derived from the nameplate's own offset and the tallest glyph that stacks here, so moving the
    /// bar moves the numbers with it.
    /// </remarks>
    private int EnemyCalloutBase => _rowTopY + NameplateOffset - CritPx - 6;
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
    /// <summary>
    /// UNUSED — the guide moved to the host as shared chrome (Game1.DrawGuideBanner).
    /// </summary>
    /// <remarks>
    /// Kept as a deliberate tombstone rather than deleted silently, because the capture fixtures and
    /// the scene audits both reference "the guide on the hunt screen" and the next person to look for
    /// it here should find out where it went rather than conclude the feature was cut.
    /// </remarks>
    [Obsolete("The guide is drawn by Game1.DrawGuideBanner over every screen. Setting this does nothing.")]
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
    /// <summary>Advance the fight. Takes no input — this screen's clicks are handled in Draw.</summary>
    /// <remarks>
    /// <b>IT USED TO TAKE FOUR INPUT PARAMETERS AND READ NONE OF THEM</b> — keys, mouse, clicked and
    /// wheel, all dead. Click handling migrated into Draw (DrawBattleControls) and the Update-side
    /// plumbing was left standing, which meant the host computed a `watchingFight` flag every frame,
    /// threaded it down as `interactive`, and handed it to a method that ignored it. A gate that gates
    /// nothing is worse than no gate: the next person to need one would have found this and believed it
    /// was already handled.
    /// </remarks>
    public void Update(GameTime time, Hunter hunter, float enemyBaseHealth, float enemyBaseDamage)
    {
        _hunter = hunter;
        var dt = (float)time.ElapsedGameTime.TotalSeconds;
        _anim += dt;
        // _strikeTime WAS THE OLD SWING CLOCK and is gone. It was armed by the impact and counted down,
        // so the clip played entirely AFTER the blow it was meant to deliver. The champion now runs on
        // _champWindup, the same anticipation model the enemy already used. Leaving a decaying timer
        // here that nothing reads is exactly the kind of dead machinery this codebase keeps finding.
        _champLunge = Math.Max(0f, _champLunge - dt * 5f);
        _champSinceHit += dt;
        _enemySinceHit += dt;
        _skillSinceCast += dt;
        _enemyLunge = Math.Max(0f, _enemyLunge - dt * 5f);
        // 1.25, not 2.5: the slide-in was over in 0.4s, which is too quick to register as creatures
        // ARRIVING rather than simply appearing. Twice as long, and the beat before it is now empty
        // stage, so the entrance has something to be an entrance from.
        _enemyEnter = Math.Max(0f, _enemyEnter - dt * 1.25f);   // the new enemy slides in over ~0.8s
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

        // The CHARGE pill only exists when the pool does.
        var trig = build.Triggers(hunter);
        _chargeLive = trig.Contains(BuildTrigger.Rend) || trig.Contains(BuildTrigger.Capacitor)
                      || trig.Contains(BuildTrigger.Dynamo) || trig.Contains(BuildTrigger.Lodestone);
        _chargeCap = trig.Contains(BuildTrigger.Capacitor)
            ? SoloBattle.ChargeCapExtended : SoloBattle.ChargeCap;
        _chargeNow = 0;

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
        _chargeNow = 0;   // the sim's pool is per-wave; the pill must not carry one over
        _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(0f);
        _nextChampStrikeMs = _replay.NextChampionStrikeAfter(0f);
        _callouts.Clear();
    }

    /// <summary>The host rolled a chest for the boss just felled — upgrade the banner to the reward beat.</summary>
    /// <remarks>Called same-frame as the boss-down banner, so "CHEST DROPPED!" replaces "BOSS DOWN!" cleanly.</remarks>
    public void FlashChest()
    {
        _bannerText = "BOSS DOWN — CHEST DROPPED!  (F — FORGE)";
        _bannerTimer = 2.4f;
    }

    /// <summary>A charter dropped — say so on its own, louder than a material.</summary>
    /// <remarks>
    /// REPLACES the banner rather than appending, unlike FlashSpoil. A charter is one wave in forty and
    /// a whole Forge operation; sharing a line with "+1 ESSENCE" would bury the rarer of the two.
    /// </remarks>
    public void FlashCharter(string name)
    {
        _bannerText = $"{name} FOUND  —  SPEND IT IN THE FORGE (F)";
        _bannerTimer = 3.0f;
    }

    /// <summary>
    /// Show what the wave paid, rising from where the creatures actually stood.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The designer asked for this directly: <i>"ölen düşmandan düşen dropu da gösterip, sonra bir
    /// sonraki waveye geçerek daha anlaşılabilir bir sahne yapabiliriz."</i>
    /// </para>
    /// <para>
    /// Every reward the fight paid used to appear as text in a banner at the top of the arena, or as a
    /// number silently added to a pill in the corner — nowhere near the thing that dropped it, and gone
    /// before the next wave had finished walking in. A player could clear forty waves without ever
    /// seeing where their materials came from.
    /// </para>
    /// <para>
    /// Spawned at the ROW anchor, so the haul rises out of the creatures rather than out of the middle
    /// of the screen, and staggered so a wave that paid three things reads as three things.
    /// </para>
    /// </remarks>
    public void ShowSpoils(int gleam, string? material, string? charter)
    {
        var slot = 0;

        void Rise(string text, Color tint, int px)
        {
            _callouts.Add(new Callout
            {
                Text = text,
                Color = tint,
                X = _rowCentreX,
                // Stacked UPWARD from the row's shoulder, and started low so the whole beat is a rise.
                Y = EnemyCalloutBase - slot * CalloutLineHeight,
                Life = SpoilsBeat + 0.35f - slot * 0.06f,
                Px = px,
                Lane = CalloutLane.Enemy,
            });
            slot++;
        }

        if (gleam > 0) Rise($"+{gleam:N0}", Gold, DamagePx);
        if (material is not null) Rise($"+1 {material}", Verdant, DamagePx);
        if (charter is not null) Rise(charter, Bloom, CritPx);
    }

    /// <summary>The wave paid a material better than Scrap — say which, on the clear banner.</summary>
    /// <remarks>
    /// <para>
    /// APPENDS rather than replaces. The host pays the wave out on the same frame the clear banner is
    /// written, so a replacing call would delete "WAVE 12 CLEARED" and leave the player looking at a
    /// material with no idea what earned it. Appending keeps the cause and the reward in one line.
    /// </para>
    /// <para>
    /// Scrap deliberately never reaches here. It drops on nearly every wave, and a banner that fires
    /// every wave is wallpaper — the announcement has to stay rare enough to still read as an event.
    /// </para>
    /// </remarks>
    public void FlashSpoil(Material material)
    {
        var name = material.ToString().ToUpperInvariant();
        _bannerText = string.IsNullOrEmpty(_bannerText) ? $"+1 {name}" : $"{_bannerText}   +1 {name}";
        _bannerTimer = MathF.Max(_bannerTimer, 1.6f);
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
            X = _rowCentreX,
            Y = EnemyCalloutBase - StackSlot(CalloutLane.Enemy) * CalloutLineHeight,
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
        // BOTH EARLY RETURNS CLEAR THE WINDUPS FIRST. They sit above the lines that recompute them, so a
        // wave that ended mid-swing left _enemyWindup frozen at whatever it held — and the NEXT wave then
        // slid in holding that pose, a creature entering the arena already halfway through an attack it
        // was not making. The champion's clock has the same shape and the same hazard.
        if (_breakTimer > 0f)
        {
            _enemyWindup = _champWindup = 0f;
            _breakTimer -= dt;
            if (_breakTimer <= 0f) { BeginWave(); _enemyEnter = 1f; }
            return;
        }

        // Hold the fight until the new enemy has finished sliding in — otherwise the champion swings at empty
        // air while the enemy is still off to the right ("hunter hits before the enemy arrives").
        if (_enemyEnter > 0f) { _enemyWindup = _champWindup = 0f; return; }

        _playheadMs += dt * 1000f * _speedMul;
        if (_skillFlash.Count > 0)
            foreach (var key in _skillFlash.Keys.ToList())
            {
                var left = _skillFlash[key] - dt;
                if (left <= 0f) _skillFlash.Remove(key); else _skillFlash[key] = left;
            }

        // THE WINDOW, NOT THE FPS, IS WHAT MAKES THIS SWING LEGIBLE — and getting that wrong is easy.
        // EnemyClipSeconds maps the windup 0..1 onto the clip's FULL length, so the clip always completes
        // across this window whatever its fps: lowering frames-per-second changes the idle loop and
        // nothing at all about the attack. Widened from 600ms to 900ms, near the champion's own swing
        // (StrikeSeconds = 8 frames / 8fps = 1.0s), so the two actors wind up at comparable speeds and a
        // player can see a creature commit before it lands.
        const float windupMs = 900f;
        var lead = _nextEnemyStrikeMs - _playheadMs;
        _enemyWindup = lead > 0f && lead < windupMs ? 1f - lead / windupMs : 0f;

        // The champion's swing, on the same anticipation model. StrikeSeconds is the authored clip
        // length, so the arm is fully drawn back one clip-length out and connects on the frame the blow
        // is credited — instead of the blow landing on a figure still standing at rest.
        var champLead = _nextChampStrikeMs - _playheadMs;
        // The windup window is the clip UP TO the contact frame (0.625 s of the 1 s clip), so the swing
        // plays at one speed from first frame to touch; the follow-through after the hit is the rest.
        var champWindowMs = StrikeSeconds * ContactFraction * 1000f;
        _champWindup = champLead > 0f && champLead < champWindowMs
            ? 1f - champLead / champWindowMs
            : 0f;

        // The SKILL anticipation, same model: the cast clip runs up to the cast. Read straight off the
        // replay every frame (a few hundred events at most) rather than cached, so a new wave's replay
        // needs no reset and a dev fixture that rewinds the playhead stays honest.
        _skillWindup = 0f;
        if (_replay.NextSkillEventAfter(_playheadMs) is { } nextSkill && (Form)nextSkill.Amount != Form.Trap)
        {
            var skillLead = nextSkill.AtMs - _playheadMs;
            var castWindowMs = CastSeconds * ContactFraction * 1000f;
            if (skillLead > 0f && skillLead < castWindowMs)
            {
                _skillWindup = 1f - skillLead / castWindowMs;
                _skillForm = (Form)nextSkill.Amount;
            }
        }

        foreach (var e in _replay.Advance(_playheadMs))
        {
            // EVERY EFFECT USED THE SAME DEFAULT SIZE, so a glancing blow, a critical and a death all
            // burst at 208px — on a 430px champion and on swarm creatures barely 100px across. Playtest:
            // "HUNT ekranında efektlerin boyutları düzgün değil." Size is the loudest channel an effect
            // has, and spending all of it on "something happened" leaves nothing for "something big
            // happened". They are graded now: a weak hit is 1, an ordinary hit 1, an interrupt or a
            // level-up 2, a crit, a Ruin strike or a death 3. The champion's own hit also drops 40px to
            // the torso, because centred on ChampBox it burst over the head.
            switch (e.Kind)
            {
                case BattleEventKind.Strike:
                    // Just the lunge. A hit-spark on EVERY strike (skills AND auto-attacks fire these
                    // constantly) was the "too many red slashes" the playtest flagged — loudness has to be
                    // budgeted against importance, so the loud VFX are reserved for the SKILL casts below.
                    _champLunge = 1f;
                    _champSinceHit = 0f;   // the follow-through starts at the touch
                    _nextChampStrikeMs = _replay.NextChampionStrikeAfter(e.AtMs);
                    if ((_strikeCount++ & 1) == 0)   // every other auto-hit: a number and a small, quiet puff
                    {
                        SpawnDamage(HitDamage(1f), false);
                        _vfx.Play("fx_weakhit", _rowCentreX, _rowTopY + 110, scale: 1, fps: 16f, tint: Steel);
                    }
                    break;
                case BattleEventKind.EnemyStrike:
                    _enemyLunge = 1f;
                    _enemySinceHit = 0f;
                    _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(e.AtMs);
                    _vfx.Play("fx_hit", ChampBox.Center.X, ChampBox.Center.Y + 40, scale: 2, fps: 14f, tint: Ember);
                    break;
                case BattleEventKind.Skill:
                    var form = (Form)e.Amount;
                    _skillFlash[e.Amount] = 0.42f;
                    var (text, colour) = CalloutFor(form);
                    Say(text, colour);
                    // Slot carries the casting skill's Source (WaveModel.BattleEvent) — the effect is the
                    // Form's shape in the Source's colour, which is the whole hexagon in one flash.
                    PlayFormVfx(form, (Source)e.Slot);
                    if (form != Form.Trap) { _skillSinceCast = 0f; _skillFollowForm = form; }   // the release frames
                    SpawnDamage(HitDamage(form == Form.Trap ? 3f : 2f), form == Form.Trap);   // skills hit big
                    break;
                case BattleEventKind.Heal:
                    Say($"+{e.Amount}", Verdant);
                    _vfx.Play("fx_heal", ChampBox.Center.X, ChampBox.Center.Y, scale: 3, fps: 10f, tint: Verdant);
                    break;
                case BattleEventKind.Shield:
                    Say("UNDYING", Gold);
                    _vfx.Play("fx_shield", ChampBox.Center.X, ChampBox.Center.Y - 20, scale: 4, fps: 12f, tint: Gold);
                    break;
                case BattleEventKind.Down:
                    _vfx.Play("fx_death", ChampBox.Center.X, ChampBox.Center.Y, scale: 4, fps: 9f);
                    break;
                case BattleEventKind.EnemyDown:
                    // A boss falling is the loudest beat in the fight: the starburst AND the plume.
                    if (_isBossWave) _vfx.Play("fx_crit", _rowCentreX, _rowTopY + 110, scale: 4, fps: 10f, tint: Gold);
                    _vfx.Play("fx_death", _rowCentreX, _rowTopY + 110, scale: _isBossWave ? 4 : 2, fps: 9f);
                    break;
                case BattleEventKind.Charge:
                    _chargeNow = e.Amount;   // the pool AFTER the change; 0 is REND's dump
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

            // THE ANNOUNCEMENT COMES BEFORE THE BOSS — in the breath between waves, replacing the
            // ordinary clear banner (the overlay priority already ranks it higher). It used to fire at
            // the boss REPLAY's start, where a strong champion kills him inside the announcement's own
            // 1.6 seconds and INCOMING outlives the corpse. Playtest: "Boss öldükten sonra boss
            // incoming mesajı geliyor."
            if (WaveScaling.IsBossWave(_run.Wave + 1, ExpeditionTuning.Default))
                _bossIncomingTimer = _breakTimer + 0.9f;
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

    /// <summary>
    /// The skill layer of the fight: one effect strip per FORM, tinted by the casting skill's SOURCE.
    /// </summary>
    /// <remarks>
    /// The Form is the SHAPE of what happened (a slash, a bolt, a ring, a burst from the floor, a sigil, a
    /// surge) and the Source is its colour — so a Shadow Strike and a Nature Strike share a silhouette and
    /// differ in glow, which is exactly the two-axis reading the art bible asks of creatures (§3.1). The
    /// strips are authored white so the tint carries the whole hue; they are drawn additively, which is why
    /// the glow colours below are brighter than the bible's body colours (Umbra Indigo added to black is
    /// nothing at all).
    /// </remarks>
    private void PlayFormVfx(Form form, Source source)
    {
        var glow = SourceGlow(source);
        // Size is relative to the thing being hit: a boss is ~540 px tall against a normal creature's
        // ~300, so everything that lands on the enemy side grows one step on a boss wave.
        var big = _isBossWave ? 1 : 0;
        switch (form)
        {
            case Form.Strike:
                _vfx.Play("fx_strike", _rowCentreX, _rowTopY + 110, scale: 3 + big, fps: 12f, tint: glow);
                break;
            case Form.Projectile:
                // The bolt flies left-to-right inside its own frame, so it is centred between the two figures.
                _vfx.Play("fx_projectile", (ChampBox.Right + _rowCentreX) / 2, _rowTopY + 120, scale: 3 + big, fps: 14f, tint: glow);
                break;
            case Form.Aura:
                _vfx.Play("fx_aura", ChampBox.Center.X, ChampBox.Center.Y + 20, scale: 3, fps: 12f, tint: glow);
                break;
            case Form.Trap:
                _vfx.Play("fx_trap", _rowCentreX, _rowTopY + 150, scale: 3 + big, fps: 12f, tint: glow);
                break;
            case Form.Mark:
                _vfx.Play("fx_mark", _rowCentreX, _rowTopY + 110, scale: 2 + big, fps: 12f, tint: glow);
                break;
            case Form.Transformation:
                _vfx.Play("fx_transformation", ChampBox.Center.X, ChampBox.Center.Y, scale: 3, fps: 12f, tint: glow);
                break;
        }
    }

    /// <summary>The additive glow of each Source — see <see cref="PlayFormVfx"/> for why these are not the bible's body hues.</summary>
    private static Color SourceGlow(Source s) => s switch
    {
        Source.Body => new Color(0xE8, 0x4A, 0x5E),
        Source.Mind => new Color(0x5A, 0xC8, 0xE8),
        Source.Nature => new Color(0x7F, 0xCB, 0x4A),
        Source.Machine => new Color(0xE8, 0x8E, 0x3C),
        Source.Shadow => new Color(0x9B, 0x7B, 0xFF),
        _ => new Color(0xE6, 0xE0, 0xFF),   // Spirit
    };

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
        DrawRightColumn(b, hit, clicked);
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
        var attacking = EnemyAttacking;
        if (_isBossWave) DrawBoss(b, attacking);
        else DrawNormalEnemy(b, attacking);

        // Champion (arena left). Name/HP live in the top-left HUD.
        var push = (int)(_champLunge * 40f);
        var cbox = new Rectangle(ChampBox.X + push, ChampBox.Y, ChampBox.Width, ChampBox.Height);
        if (_replay!.IsShielded(0)) Outline(b, new Rectangle(cbox.X - 4, cbox.Y - 4, cbox.Width + 8, cbox.Height + 8), Steel, 4);
        DrawChampion(b, cbox, dead: _mode == Mode.Downed);

        _vfx.Draw(b);
        DrawCallouts(b);
        // The HUNT screen is NOT drawn through the overlay inset (see Game1.OverlayActive), so its own
        // full-page effects are authored 1:1 against the canvas and must NOT use the oversized scrim.
        if (_deathFlash > 0f) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Ember * (_deathFlash * 0.35f));


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
    /// Where in an eight-frame clip the blow CONNECTS: frame 5 of 8. The 2026-08-22 clips wind up over
    /// frames 0-2, commit over 3-4, touch at 5 and recover over 6-7 — so an anticipation clock that ran
    /// the WHOLE clip up to the hit (the previous model) put the impact on a figure already standing back
    /// up, and the filmstrip showed it: the slash effect flashing beside an upright champion, the
    /// creature's lunge finished a third of a second before its bite landed. The windup now runs the clip
    /// to this frame, the hit itself starts the follow-through, and the follow-through plays the rest.
    /// </summary>
    private const float ContactFraction = 5f / 8f;

    /// <summary>Seconds since the champion's last auto-hit / the enemy's last bite / the champion's last
    /// cast — the follow-through clocks. Large at rest so nothing plays before its first beat.</summary>
    private float _champSinceHit = 99f, _enemySinceHit = 99f, _skillSinceCast = 99f;
    private Form _skillFollowForm;
    /// <summary>How long an enemy's follow-through stays on its attack clip: three frames at ~10 fps.</summary>
    private const float EnemyFollowSeconds = 0.3f;

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
    {
        if (!attacking) return _anim + stagger;
        var clip = EnemyClipFrames / fps;
        // The windup runs the clip up to the CONTACT frame; the bite itself starts the follow-through,
        // which plays the remaining frames and then clamps on the last (see ContactFraction).
        return _enemyWindup > 0f
            ? _enemyWindup * clip * ContactFraction
            : clip * ContactFraction + _enemySinceHit;
    }

    /// <summary>The enemy is on its attack clip: winding up to a bite, or following one through.</summary>
    private bool EnemyAttacking => _enemyWindup > 0f || _enemySinceHit < EnemyFollowSeconds;

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
        // 0.54, not 0.62. Playtest: "düşmanlar çok yakında geliyor onları biraz daha sağa alabiliriz."
        // The row is already pinned as far right as the clamp allows, so the creatures were not too far
        // right — the ROW WAS TOO WIDE, and its left end reached back toward the champion. At 0.62 a
        // swarm of four spans 372px, putting its leftmost creature 102px from ChampBox's right edge;
        // at 0.54 it spans 324 and that gap becomes 150.
        var spacing = (int)(w * 0.54f);        // overlap slightly; a row of five must still fit
        if (comp.Count > 1 && room > 0) spacing = Math.Min(spacing, room / (comp.Count - 1));
        var wanted = Math.Max(0, spacing) * (comp.Count - 1);

        // AND THE CLAMP STILL CANNOT INVERT. Compression handles every wave that can be made to fit;
        // this handles the one that cannot — a single creature wider than the arena leaves no room at
        // all, and an overhanging row is a cosmetic problem where a thrown exception is a lost session.
        var lo = ArenaRect.X + half + 20 + wanted / 2;
        var hi = ArenaRect.Right - half - 20 - wanted / 2;

        // AND A FLOOR THAT KEEPS THE ROW OFF THE CHAMPION. Raised only as far as `hi` allows, so it can
        // never invert the clamp — a wave too wide to leave the gap simply gets whatever gap there is,
        // which is the same graceful degradation the fallback below already provides.
        lo = Math.Min(hi, Math.Max(lo, ChampBox.Right + 80 + wanted / 2));

        // THE MOTION IS ADDED AFTER THE CLAMP, and that is the whole fix. It used to be clamped WITH the
        // resting position — `Clamp(EnemyBox.Center.X + enter + lunge, lo, hi)` — and for essentially
        // every archetype and count the arena can produce, `hi` already sits below EnemyBox.Center.X
        // (a swarm of four lands hi ~1160 against a centre of 1320). So the clamp saturated and BOTH the
        // slide-in and the lunge were swallowed whole: the champion shoved right and the enemies never
        // shoved back, on 19 of 20 multi-creature waves. The animation existed and never moved a pixel.
        //
        // Clamping only the RESTING layout keeps the guarantee that matters — a row that fits, and a
        // clamp that can never invert — while letting the two 40-to-280px offsets do what they were
        // written to do. Overhang is handled by the arena scissor (see ArenaRasterizer), which is
        // already active for exactly this kind of transient overshoot.
        var resting = lo > hi ? ArenaRect.Center.X : Math.Clamp(EnemyBox.Center.X, lo, hi);
        var centre = resting + enter + lunge;
        var left = centre - wanted / 2;

        // WHERE THE ROW ACTUALLY IS, published for the labels.
        //
        // The nameplate, the health bar and the damage callouts all anchored to the raw EnemyBox — a
        // 440-tall box centred at 1320 — while the row is clamped into the arena and scaled by its
        // archetype. A swarm of four occupies the bottom 255px of that box and clamps to x≈1174, so the
        // bar hung 200px above the creatures and 146px to their right, and the damage numbers stacked
        // 340px up in bare rock. Every label described something that was not there.
        //
        // Deliberately NOT the lunge-shifted centre: a nameplate that slides 40px every time the row
        // shoves forward reads as jitter. The labels follow the row's RESTING position and its real
        // height, which is what actually moved.
        _rowCentreX = resting;
        _rowTopY = EnemyBox.Bottom - h;

        // THE HIT EFFECTS USE THIS TOO. Every enemy-side VFX played at EnemyBox.Center — 146px right of
        // where a swarm actually stands — so the spark, the crit burst and the death plume all went off
        // beside the creatures rather than on them. They are spawned at (_rowCentreX, _rowTopY + 110):
        // the row's centre, and roughly its upper body rather than its feet.

        string? stripKey = null, staticKey = null;
        if (EnemySource is { } es && EnemyForSource.TryGetValue(es, out var en))
        {
            var act = attacking ? "attack" : "idle";
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

            // MEASURED headroom, not a constant. The 2026-08-22 strips fill ~90% of their frame with the
            // figure sat 3.5% up from the floor, so a fixed 8% crop bit the top of the tallest creatures;
            // a negative topCrop makes AnimSprite trim exactly the empty rows and never the figure.
            const float crop = -1f;
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
            // With the headroom trimmed by measurement the figure's top IS box.Y, so the pip sits a
            // fixed 14px above it (the old 0.22 head fraction was measured on the previous art).
            var pip = new Rectangle(box.Center.X - 28, box.Y - 14, 56, 6);
            _ui.Fill(b, pip, new Color(0x12, 0x0C, 0x10));
            if (frac > 0f) _ui.Fill(b, new Rectangle(pip.X, pip.Y, (int)(pip.Width * frac), pip.Height), Ember);
        }

        // One wave-level nameplate: what this wave IS, which is the thing the player has to learn.
        var label = $"{_run?.LastWaveArchetype.ToString().ToUpperInvariant()}  x{comp.Count}";
        var affixes = _run?.LastWaveAffixes ?? Array.Empty<Affix>();
        if (affixes.Count > 0)
            label += "   ·   " + string.Join(" + ", affixes.Select(a => a.ToString().ToUpperInvariant()));

        var bar = new Rectangle(_rowCentreX - 92, _rowTopY + NameplateOffset, 184, NameplateHeight);
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
        const float crop = -1f;   // measured headroom — see DrawComposition
        var figTop = ebox.Bottom - ebox.Height;
        var ab = new Rectangle(ebox.X, figTop + bob, ebox.Width, ebox.Height);

        string? stripKey = null, staticKey = null;
        // 11/9, was 16/12. The whole complaint is legibility: an 8-frame swing at 16fps is over in half
        // a second, which is not long enough to see a creature wind up and commit. Held above ~9 so the
        // frames still read as motion rather than as a slideshow.
        var fps = attacking ? 11f : 9f;
        if (EnemySource is { } es && EnemyForSource.TryGetValue(es, out var en))
        {
            var act = attacking ? "attack" : "idle";
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
        // The strip is drawn with its measured headroom trimmed, so the visible top is the box top.
        var visTop = figTop;
        // Framed bar art, matching the Hunter's own HUD bar. A bare 130x12 red rectangle was the one
        // unstyled element left inside the arena, and at full health it read as a floating red streak
        // with nothing tying it to the creature underneath.
        var ebar = new Rectangle(ebox.Center.X - 92, visTop - 46, 184, 34);
        _ui.BarArt(b, ebar, Math.Clamp(_replay!.EnemyHealthFraction, 0f, 1f), "health");
        // The creature was never named on screen — the player fought an anonymous sprite for the whole run.
        if (stripKey is not null || staticKey is not null)
            _ui.TextCenterBig(b, PrettyName(EnemySource), ebar.Center.X, ebar.Y - 28, UiKit.Vellum, UiTypography.Secondary);
    }

    /// <summary>
    /// Draw the boss: a 540-px figure standing at <see cref="BossAnchor"/>, facing LEFT, from its own
    /// <c>&lt;boss&gt;_&lt;clip&gt;_strip8_512</c> clip — the same path every other figure takes.
    /// </summary>
    /// <remarks>
    /// This used to slice a 1024-px strip by a hand-measured body rectangle because the old render carried a
    /// bleed streak and wings that had to be excluded from the scale. The 2026-08-22 art contract generates
    /// every boss on a clean canvas at a shared density, so the generic grounded draw is now the correct one,
    /// and the boss gets the same lunge the ordinary enemies have when it lands a blow.
    /// </remarks>
    private void DrawBoss(SpriteBatch b, bool attacking)
    {
        var bossKey = DevForceBoss ? "crystal_lich" : BossForRegion.GetValueOrDefault(RegionId);
        _bossName = bossKey is not null ? BossNameFor.GetValueOrDefault(bossKey, "BOSS") : "BOSS";

        var lunge = (int)(_enemyLunge * -40f);
        var box = new Rectangle(BossAnchor.X - BossTargetBodyHeight / 2 + lunge, BossAnchor.Y - BossTargetBodyHeight,
                                BossTargetBodyHeight, BossTargetBodyHeight);
        _ui.GroundShadow(b, box.Center.X, BossAnchor.Y - 8, (int)(box.Width * 0.62f), 44, 0.6f);

        // The swing rides the same windup as everything else (see EnemyClipSeconds): the strike lands on the
        // frame the blow is credited, instead of the boss cycling its attack strip on the free clock.
        var fps = attacking ? 10f : 8f;
        var key = bossKey is null ? null : $"{bossKey}_{(attacking ? "attack" : "idle")}_strip8_512";
        var seconds = EnemyClipSeconds(attacking, fps);
        if (key is null || !_ui.AnimSprite(b, key, box, seconds, fps, !attacking, Color.White, -1f))
        {
            _bossBodyRect = new Rectangle(BossAnchor.X - 110, BossAnchor.Y - BossTargetBodyHeight, 220, BossTargetBodyHeight);
            _bossFullRect = _bossBodyRect;
            _ui.Fill(b, _bossBodyRect, Ember);
            return;
        }
        _bossFrame = attacking ? Math.Min(7, (int)(seconds * fps)) : (int)(seconds * fps) % 8;
        _bossBodyRect = box;
        _bossFullRect = box;
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
    public void DrawLog(SpriteBatch b, Point mouse, bool clicked)
    {
        if (!_logOpen) return;

        // THE PARAMETER USED TO BE CALLED `hit`, which promised the caller had already lifted it into
        // this screen's 1920 space. The caller had not — Game1 passes CanvasMouse, 480x270 — so the
        // log's page buttons were hit-tested against a cursor that could never reach them, and both
        // arrows were dead at every resolution. A parameter name is not a conversion.
        //
        // The same lift the sibling Draw does (:697). Safe unconditionally because opening the log now
        // closes all nine overlay screens, so this batch is never under the overlay inset transform.
        var hit = new Point(mouse.X * 4, mouse.Y * 4);

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0A, 0x08, 0x10, 0xE6));

        if (Log.Count == 0)
        {
            _ui.TextCenterBig(b, "NO EXPEDITIONS YET", 960, 480, Slate, UiTypography.RegionTitle, TextFace.Display);
            _ui.TextCenter(b, "L CLOSES THIS.", 960, 540, Dim);
            return;
        }

        _logIndex = Math.Clamp(_logIndex, 0, Log.Count - 1);
        var shown = Log.Entries[_logIndex];
        var older = Log.OlderThan(_logIndex);

        _ui.TextCenterBig(b, "EXPEDITION LOG", 960, 120, Gold, UiTypography.ScreenTitle, TextFace.Display);
        _ui.TextCenter(b, $"{_logIndex + 1} OF {Log.Count}   \u00b7   \u2039 \u203a TO STEP   \u00b7   L CLOSES", 960, 172, Slate);

        DrawReportPanel(b, shown, older);

        // THE PAGE BUTTONS BELONG TO THE PANEL THEY PAGE, and they were placed by absolute literals
        // that landed on top of other things entirely: "back" at x=300 sat in the middle of the
        // battle-control rail and covered the AUTO HUNT label, while "forward" at x=1500 overlapped the
        // report panel's own right ornament and then ate the leading characters of the REWARD ACTIVITY
        // rows behind it. The only two interactive things on this screen were drawn over two panels
        // that had nothing to do with them.
        //
        // Docked inside the report, in the band it already leaves empty at its foot, and labelled with
        // a verb \u2014 a bare chevron tells a new player nothing about what it steps through.
        var reportPanel = new Rectangle(ArenaRect.X + 40, 250, ArenaRect.Width - 80, 690);
        var prev = new Rectangle(reportPanel.X + 44, reportPanel.Bottom - 104, 200, 64);
        var next = new Rectangle(reportPanel.Right - 244, reportPanel.Bottom - 104, 200, 64);
        if (_ui.Button(b, prev, "\u2039  OLDER", hit, clicked, enabled: _logIndex < Log.Count - 1)
            && _logIndex < Log.Count - 1) _logIndex++;
        if (_ui.Button(b, next, "NEWER  \u203a", hit, clicked, enabled: _logIndex > 0)
            && _logIndex > 0) _logIndex--;
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

        _ui.TextBig(b, $"MEASURED OVER THE LAST {r.SampledWaves} WAVE{(r.SampledWaves == 1 ? "" : "S")}", x, y + 4, Slate, UiTypography.Secondary);

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
    // DrawGuide MOVED TO Game1.DrawGuideBanner — it is shared chrome now, not arena furniture.
    //
    // It lived here, inside DrawArena, which is reached only through the terminal `else` of Game1's
    // screen chain. So the guide drew on the HUNT screen and nowhere else — and every step from
    // SpendGleam onward names a key that navigates AWAY from the hunt. The player read "press V for
    // STATS", pressed V, and the instruction vanished with no confirmation it had been understood or
    // completed. Playtest: "Tutorial bozuk, düzgün ilerlemiyor." That is what a guide that disappears
    // the instant you obey it looks like from the outside.

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
                _ui.TextCenterBig(b, "BOSS INCOMING", 960, 224, Gold * fade, UiTypography.RegionTitle, TextFace.Display);
                _ui.TextCenterBig(b, "STEEL YOURSELF", 960, 274, Bone * fade, UiTypography.OverlayBody);
                break;
            }
            case HuntOverlay.WaveCleared:
            {
                var fade = Math.Clamp(_bannerTimer * 1.4f, 0f, 1f);
                _ui.TextCenterBig(b, _bannerText, 960, 268, Gold * fade, UiTypography.RegionTitle, TextFace.Display);
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
        _ui.PanelQuiet(b, panel);

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
        _ui.TextCenterBig(b, title, cx, 32 + (UiTypography.RegionTitle - titlePx) / 2, Gold, titlePx, TextFace.Display);
        _ui.TextCenterBig(b, Deepest >= ConquerAt ? "CONQUERED" : $"DEPTH {Deepest} / {ConquerAt}",
            cx, 73, Deepest >= ConquerAt ? Gold : Bone, UiTypography.StageLabel);
        _ui.BarArt(b, new Rectangle(710, 102, 400, 18),
            ConquerAt > 0 ? Math.Clamp(Deepest / (float)ConquerAt, 0f, 1f) : 0f, "progress");
        // THE WAVE LINE CARRIES THE RUN'S STATE NOW, which is what the deleted EXPEDITION plate was for.
        // Nothing is appended while the run is simply running: "ACTIVE" was true of every frame this
        // screen has ever drawn, so it distinguished nothing and only made the line longer.
        var wave = $"WAVE {_run!.Wave + 1}";
        if (RunState() is { } st)
        {
            var gap = _ui.MeasureBig("  —  ", UiTypography.OverlayTitle);
            var full = _ui.MeasureBig(wave, UiTypography.OverlayTitle) + gap + _ui.MeasureBig(st.Text, UiTypography.OverlayTitle);
            var wx = cx - full / 2;
            _ui.TextBig(b, wave, wx, 124, isBossWave ? Gold : Bone, UiTypography.OverlayTitle);
            _ui.TextBig(b, "  —  ", wx + _ui.MeasureBig(wave, UiTypography.OverlayTitle), 124, Slate, UiTypography.OverlayTitle);
            _ui.TextBig(b, st.Text, wx + _ui.MeasureBig(wave, UiTypography.OverlayTitle) + gap, 124, st.Tint, UiTypography.OverlayTitle);
        }
        else _ui.TextCenterBig(b, wave, cx, 124, isBossWave ? Gold : Bone, UiTypography.OverlayTitle);
    }

    private static readonly Color PlateEdge = new(0x74, 0x62, 0x3E);

    /// <summary>A titled panel using the ORNATE ui_panel_* frame art (package_01) — the reference's look.
    /// The title is left-aligned just inside the top edge so it clears the frame's top-centre gem. Returns
    /// the inner content rect (inset past the ornate corners).</summary>
    /// <summary>A plate in the right rail, returning the space its content may actually use.</summary>
    /// <remarks>
    /// THE BOTTOM INSET WAS 22px WHERE THE FRAME NEEDS 40. `Height - 92` reserved 70 at the top and only
    /// 22 at the bottom, so EVERY plate in this rail overhung its own border by 18px — the OBJECTIVE
    /// progress bar was drawn on the frame's bottom band, its ends colliding with the corner filigree so
    /// the two pieces of art merged, and REWARD ACTIVITY's third row clips the same way the moment
    /// Deepest rises above zero. UiKit.PanelCorner is the number the art actually uses.
    /// </remarks>
    private Rectangle CleanPanel(SpriteBatch b, Rectangle r, string title)
    {
        _ui.PanelQuiet(b, r);
        _ui.TextBig(b, title, r.X + 44, r.Y + 26, Gold, 20);
        return new Rectangle(r.X + 44, r.Y + 70, r.Width - 88, r.Height - 70 - UiKit.PanelCorner);
    }

    /// <summary>Right context column: two plates — the idle rate, and the errands worth doing now.</summary>
    /// <remarks>
    /// It was four. OBJECTIVE and EXPEDITION printed the depth, the progress bar and the wave that the
    /// banner over the arena already prints, so half the rail was a second copy of the header.
    /// </remarks>
    private void DrawRightColumn(SpriteBatch b, Point hit, bool clicked)
    {
        // Rev 3 §20: right context rail (1570,110,326,354) — secondary panels, all live data. The spec's
        // "no keyboard hints" rule is deliberately broken by the two REWARD ACTIVITY buttons, which name
        // their key: they are the one place on this screen that asks the player to go somewhere, and a
        // door worth opening should say how. Idle rate → reward activity.
        const int px = 1570, pw = 326;

        // Idle rate (1570,110,326,130). Auto-credited, so no Claim button (§20.2).
        var inner = CleanPanel(b, new Rectangle(px, 110, pw, 130), "IDLE RATE");
        if (_ui.Assets.Get("currency_gleam") is { } gi) b.Draw(gi, new Rectangle(inner.X, inner.Y + 2, 38, 38), Color.White);
        _ui.TextBig(b, $"+{Game1.Abbrev((long)(IdleGleamRate * 60f))}/min", inner.X + 48, inner.Y + 8, Gold, 24);

        // OBJECTIVE AND EXPEDITION ARE GONE. Between them they held four facts, and the banner over the
        // arena was already printing all four: "DEPTH 0 / 7", the same progress bar, and "WAVE 2". Two
        // ornate frames and a second copy of the same bar three hundred pixels from the first — and a
        // player scanning the rail for what to do next had to read past both to reach the one panel that
        // answers it. That is the "iç içe geçmiş" the playtest reported, and the cause was not density:
        // it was that most of the density said nothing new.
        //
        // The one thing they carried that the banner did NOT is the run's state — BOSS WAVE, or
        // RECOVERING after a fall — so that moved onto the banner's wave line, beside the wave it
        // describes. See RunState and DrawStageHeader.

        // Reward activity — real summary, no fake loot grid, no keyboard hints (§20.4/§21). It takes the
        // slot OBJECTIVE had, because it is now the first plate under the idle rate and the rail reads
        // downward.
        inner = CleanPanel(b, new Rectangle(px, 254, pw, 210), "REWARD ACTIVITY");
        // THE TWO THINGS A PLAYER SHOULD DO NOW WERE INERT GREY TEXT, drawn in exactly the same style as
        // the dead career stat below them — so "1 chest available" read as trivia rather than as an
        // errand, and the most valuable thing the game had given them sat unclaimed. They are buttons
        // with verbs now, naming their own key, so the rail tells you what to press as well as what you
        // have.
        //
        // They only navigate: the actual work still happens on the screen that owns it. A rail that
        // opened chests would be a second Forge.
        var ry = inner.Y;
        var anyReward = false;
        if (ChestCount > 0)
        {
            if (_ui.Button(b, new Rectangle(inner.X, ry, inner.Width, 46),
                           $"OPEN {ChestCount} CHEST{(ChestCount == 1 ? "" : "S")}  (K)", hit, clicked))
                WantsVault = true;
            ry += 54; anyReward = true;
        }
        if (Mastery.Available > 0)
        {
            if (_ui.Button(b, new Rectangle(inner.X, ry, inner.Width, 46),
                           $"SPEND {Mastery.Available} POINT{(Mastery.Available == 1 ? "" : "S")}  (B)", hit, clicked))
                WantsBuild = true;
            ry += 54; anyReward = true;
        }
        if (Deepest > 0) _ui.TextBig(b, $"DEEPEST WAVE REACHED  {Deepest}", inner.X, ry, Slate, 16);
        else if (!anyReward) _ui.TextBig(b, "NOTHING TO CLAIM YET", inner.X, inner.Y, Slate, 18);
    }

    /// <summary>The run's state, or null when it is simply running and there is nothing to say.</summary>
    /// <remarks>
    /// Was the right half of an EXPEDITION plate whose left half repeated the banner's wave number. The
    /// word it printed most often was "ACTIVE" — true of every frame the screen is drawn in, so it
    /// distinguished nothing and cost a whole plate to say. Only the two states that mean something now
    /// reach the player, and they reach them on the banner beside the wave they describe.
    /// </remarks>
    private (string Text, Color Tint)? RunState()
    {
        if (_mode == Mode.Downed) return ("RECOVERING", Ember);
        if (WaveScaling.IsBossWave(_run!.Wave + 1, ExpeditionTuning.Default)) return ("BOSS WAVE", Gold);
        return null;
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
        // PITCH IS DERIVED FROM THE SPACE, not fixed at 74. The dock was laid out against a four-slot
        // maximum, but the loop runs to Loadout.SkillCapacity, which reaches five once the trait tree's
        // weave_5 node is bought — and the fifth medallion then drew 60px BELOW the bottom of the panel
        // it lives in (ControlRail ends at y=936). The reward for the largest single upgrade in the game
        // was a slot hanging off the frame.
        // STARTS AT THE TOP OF THE RAIL NOW. It used to begin at y=632 because BATTLE SPEED and AUTO HUNT
        // occupied everything above it — so the one block a player actually reads sat in the bottom
        // third, squeezed, while two thirds of the panel reported a constant and offered a control that
        // should not have existed. With both gone the slots get the whole rail: bigger medallions, a
        // pitch that breathes, and the Form's rule beside each one with room to be read.
        // Each row is a medallion, a name beside it, and up to two wrapped lines under both — so the
        // pitch has to clear the medallion PLUS that text, and the medallion shrinks to pay for it.
        var (_, pitch, slot) = SkillRailMetrics();
        const int y0 = RailTop + RailHeadroom;
        _ui.TextBig(b, "SKILLS", RailContentX, RailTop + 26, Gold, 20);
        // THE POOL, beside the header it feeds. Gold at the brim — the REND moment worth waiting for.
        if (_chargeLive)
            _ui.TextRight(b, $"CHARGE {_chargeNow}/{_chargeCap}", RailContentX + RailContentW, RailTop + 30,
                          _chargeNow >= _chargeCap ? Gold : Slate);
        for (var i = 0; i < n; i++)
        {
            var box = new Rectangle(RailContentX, y0 + i * pitch, slot, slot);
            if (i < skills.Count)
            {
                var s = skills[i];
                var sc = SourceColor.GetValueOrDefault(s.Source, Bone);

                // THE RHYTHM OF THIS SKILL, read off the resolved wave rather than invented.
                //
                // WaveReplay carries a Skill event per cast, with the Form in Amount and the moment in
                // AtMs, so "when did this last fire" and "when does it fire next" are both answerable —
                // and a bar built on them is the fight's real cadence, not a decorative tick at a rate
                // nobody chose. Before this the rail listed the skills and then never moved again, so a
                // player could see WHAT their champion carries and never WHEN any of it happens.
                var formKey = (int)s.Form;
                var flash = _skillFlash.TryGetValue(formKey, out var fl) ? Math.Clamp(fl / 0.42f, 0f, 1f) : 0f;
                var ready = 0f;
                if (_replay is not null)
                {
                    var next = _replay.NextSkillAfter(_playheadMs, formKey);
                    var prev = _replay.LastSkillBefore(_playheadMs, formKey);
                    if (next != int.MaxValue)
                    {
                        // From the previous cast, or from the top of the wave for the very first one.
                        var from = prev >= 0 ? prev : 0;
                        var span = MathF.Max(1f, next - from);
                        ready = Math.Clamp((_playheadMs - from) / span, 0f, 1f);
                    }
                    else if (prev >= 0)
                    {
                        // It fired and will not fire again THIS WAVE — but a bar pinned at full read
                        // as "this skill never cools down" (playtest: "ilk skill hiç cooldown'a
                        // girmiyor"). Refill over the form's base cooldown instead, so the rhythm
                        // stays visible when a short wave outlives the last cast.
                        var cdMs = MathF.Max(1f, FormBehaviour.BaseCooldownMs(s.Form));
                        ready = Math.Clamp((_playheadMs - prev) / cdMs, 0f, 1f);
                    }
                }

                if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl)
                    b.Draw(sl, box, flash > 0f ? Color.Lerp(Color.White, Gold, flash) : Color.White);
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
                while (lpx > 12 && _ui.MeasureBig(label, lpx) > RailContentW - box.Width - 20) lpx--;
                // Beside the slot: the name is short and pairs with the medallion.
                _ui.TextBig(b, label, box.Right + 12, box.Y + 10, Bone, lpx);

                // WHAT THE SKILL ACTUALLY DOES, on the screen where the player first meets it.
                // This line used to read "AUTO" on every row — true of every skill in the game, so it
                // distinguished nothing and used the only spare line for it. Playtest: "Skillerin
                // açıklamaları yok, ne olduklarını anlamadım." The Weave screen explains a Form
                // properly, but a player who has not gone looking for it never sees a word.
                //
                // From BuildGlossary, so this cannot drift from the rule the simulation runs.
                // BENEATH BOTH, ACROSS THE FULL RAIL — not squeezed into the ~98px beside a medallion.
                // Removing the speed control gave this rail its height back, and the sensible use of it
                // is to let the one line that says what the skill DOES actually be read. Wrapped rather
                // than shortened: "ONE HEAVY BL…" is a fragment, and a fragment teaches nothing.
                // A four-pixel line under the medallion, filling toward the next cast. It sits in the gap
                // the label already leaves, so it costs no height — and a full bar plus a lit medallion
                // is the moment the callout in the arena fires, which is what ties the two together.
                var track = new Rectangle(box.X, box.Bottom + 2, RailContentW, 4);
                _ui.Fill(b, track, new Color(0x22, 0x1C, 0x30));
                if (ready > 0f)
                    _ui.Fill(b, new Rectangle(track.X, track.Y, (int)(track.Width * ready), track.Height),
                             flash > 0f ? Gold : sc * 0.85f);
                if (flash > 0f)
                {
                    var halo = new Rectangle(box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6);
                    _ui.Fill(b, new Rectangle(halo.X, halo.Y, halo.Width, 2), Gold * flash);
                    _ui.Fill(b, new Rectangle(halo.X, halo.Bottom - 2, halo.Width, 2), Gold * flash);
                    _ui.Fill(b, new Rectangle(halo.X, halo.Y, 2, halo.Height), Gold * flash);
                    _ui.Fill(b, new Rectangle(halo.Right - 2, halo.Y, 2, halo.Height), Gold * flash);
                }

                var head = BuildGlossary.FormHeadline(s.Form);
                var hy = box.Bottom + 12;
                foreach (var line in _ui.WrapBig(head, RailContentW, 13).Take(2))
                {
                    _ui.TextBig(b, line, RailContentX, hy, Gold, 13);
                    hy += 16;
                }
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
        _ui.PanelQuiet(b, SkillRailMetrics().Rail);

        // THE RAIL HELD TWO THINGS THAT ARE GONE, and the skills inherit the room.
        //
        // BATTLE SPEED (four buttons) was removed with the feature — see PlaybackSpeed. AUTO HUNT went
        // with it: a bordered chip whose only state was the word "ON", occupying a sixth of the rail to
        // report a constant. The tutorial's first line already says the champion fights on its own, and
        // a control that cannot be changed is not a control, it is furniture.
        //
        // What is left is the thing a player actually reads here — WHAT THEIR CHAMPION IS — and it now
        // starts at the top of the rail instead of two thirds of the way down it.
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
    private const int RailX = 190, RailTop = 236, RailW = 286, RailMaxH = 700;
    private const int RailHeadroom = 64;      // rail top to the first medallion
    private const int RailFootroom = 34;      // the ornate frame's bottom band
    private const int SkillSlotMax = 64;
    private const int SkillTextBlock = 40;    // two wrapped lines under each medallion

    /// <summary>The skills rail, its pitch and its medallion size — all derived from the slots you HAVE.</summary>
    /// <remarks>
    /// <b>THE RAIL WAS A FIXED 700px TALL AND ITS CONTENTS WERE NOT.</b> A player with two skills got two
    /// medallions in the top third and four hundred pixels of framed nothing under them — on the screen
    /// they spend nearly all their time on, in the most ornate frame in the game. An empty frame is not
    /// neutral: it is a heavy border drawn around a void, and the eye reads it as content it has somehow
    /// failed to find. Playtest, on this screen among others: "gizlenmiş gibi, iç içe geçmiş gibi".
    ///
    /// It is sized to its content now, so it grows as the trait tree buys slots instead of standing at
    /// its five-slot maximum from the first minute of the game. The pitch only compresses at the largest
    /// capacity, where the natural 128 would overrun the nav rail.
    /// </remarks>
    private (Rectangle Rail, int Pitch, int Slot) SkillRailMetrics()
    {
        var n = Math.Max(1, Loadout.SkillCapacity);
        var fixedH = RailHeadroom + SkillSlotMax + SkillTextBlock + RailFootroom;
        var pitch = 128;
        if (n > 1 && fixedH + (n - 1) * pitch > RailMaxH)
            pitch = Math.Max(96, (RailMaxH - fixedH) / (n - 1));
        var h = Math.Clamp(fixedH + (n - 1) * pitch, RailMinH, RailMaxH);
        return (new Rectangle(RailX, RailTop, RailW, h), pitch, Math.Min(SkillSlotMax, pitch - 56));
    }

    /// <summary>
    /// The shortest this rail may be — enough to keep <see cref="UiKit.Panel"/> on the same frame art.
    /// </summary>
    /// <remarks>
    /// <b>UiKit.Panel PICKS ITS TEXTURE BY ASPECT RATIO</b> — vertical below 0.82, square below 1.30,
    /// medium above — so sizing this rail to its contents silently SWAPPED ITS FRAME. At two skills the
    /// 286x330 rail crossed into the square art, whose corner ornament is proportionally heavier, and it
    /// covered the SKILLS heading that had sat clear of the vertical frame for the whole project. The
    /// panel would then have changed identity again at three slots and again at four, so the rail's
    /// appearance would have depended on how far through the trait tree the player was.
    ///
    /// 350 is 286 / 0.82 rounded up: every capacity from one to five now lands in the vertical bucket.
    /// It costs a little empty space at one skill and buys a frame that does not change under the player.
    /// </remarks>
    private const int RailMinH = 350;
    // Inset by the ornate frame's border (UiKit.Panel reserves 44px), not by the 20px a flat slab needed.
    private const int RailContentX = 234;
    private const int RailContentW = 198;


    /// <summary>
    /// DEV: hold the strike clip at a fixed point (0..1 of its duration) instead of letting combat drive it.
    /// </summary>
    /// <remarks>
    /// A one-second screenshot lands wherever the fight happens to be, which is almost never mid-swing,
    /// so the frame that most wants checking is the one no capture reliably catches.
    /// </remarks>
    public float? DevSwingPhase;

    /// <summary>
    /// The champion's SKILL anticipation: 0..1 across the clip length ENDING at the next Skill event, and
    /// the Form that event carries — so the cast (or, for a Strike, the attack) clip opens its hand on the
    /// exact frame the effect appears. Zero when no skill is due inside one clip length.
    /// </summary>
    /// <remarks>
    /// This replaced a reactive timer armed BY the Skill event, which played the whole cast after its own
    /// effect had flashed — the same backwards phase the auto-swing once had (see WaveReplay.
    /// NextChampionStrikeAfter). A Trap fires on being hit and gets no clip; every other Form is a cast,
    /// except Strike, which is the heavier swing and uses the attack clip.
    /// </remarks>
    private float _skillWindup;
    private Form _skillForm;
    /// <summary>How long a cast clip runs — eight frames at the champion's frame rate, like the strike.</summary>
    private const float CastSeconds = 8f / ChampionFps;

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

        // THE FALL, two ways. The 2026-08-22 art pass gives every character a real DEATH clip — eight frames
        // from a stagger to lying still — so when it is present the champion falls the way the artist drew it
        // and holds the last frame for the rest of the downed beat. The older treatment (freeze, sink, shrink,
        // fade) stays as the fallback for a character whose death strip has not survived the quality gate.
        var hasDeathClip = dead && _ui.Assets.Has(Character.StripKey("death"));
        if (dead && !hasDeathClip)
        {
            // The drop and the shrink are the SAME number on purpose. Sprites are bottom-anchored, so
            // moving the box down by d and shortening it by d leaves the feet exactly where they were
            // and brings the head down — a body folding onto the floor.
            var fold = (int)(box.Height * 0.42f * ease);
            box = new Rectangle(box.X, box.Y + fold, box.Width, Math.Max(8, box.Height - fold));
        }

        var shadowEase = hasDeathClip ? 0f : ease;
        _ui.GroundShadow(b, box.Center.X, box.Bottom - 10,
                         (int)(box.Width * (0.62f - 0.18f * shadowEase)), 46, 0.6f - 0.25f * shadowEase);

        var tint = dead && !hasDeathClip
            ? Color.Lerp(new Color(0x6A, 0x5A, 0x62), new Color(0x2A, 0x28, 0x34), ease) * (1f - 0.45f * ease)
            : dead ? Color.Lerp(Color.White, new Color(0x8A, 0x80, 0x88), ease * 0.6f)
            : Color.White;

        // ATTACK and CAST do not loop, and are driven by the combat beat rather than their own clock — a
        // swing that runs free drifts out of step with the hit it is delivering. _champWindup runs 0 to 1
        // across the clip length ENDING at the blow (the enemy's swing follows the same rule on the other
        // side of the arena); the cast timer counts DOWN from the Skill event.
        // DevSwingPhase forces the ATTACK clip as well as its phase (the fightswing fixture).
        // Each beat is a windup (0..contact) and then a follow-through (contact..end) started by the hit.
        // The SKILL beat outranks the auto-swing — a cast is the rarer, louder beat, and the swing's windup
        // runs most of every 1.2-second cycle, so it would otherwise hide every cast. A follow-through
        // outranks the NEXT windup so a finished swing is never snapped back to frame 0 mid-recovery.
        var fixture = DevSwingPhase is not null && !dead;
        var skilling = !dead && !fixture && _skillWindup > 0f;
        var skillFollow = !dead && !fixture && !skilling && _skillSinceCast < CastSeconds * (1f - ContactFraction);
        var strikeFollow = !dead && !fixture && !skilling && !skillFollow
                           && _champSinceHit < StrikeSeconds * (1f - ContactFraction);
        var swinging = !dead && !skilling && !skillFollow && !strikeFollow && (_champWindup > 0f || fixture);
        var skillForm = skilling ? _skillForm : _skillFollowForm;
        var clip = hasDeathClip ? "death"
            : skilling || skillFollow ? (skillForm == Form.Strike ? "attack" : "cast")
            : swinging || strikeFollow ? "attack" : "idle";
        var seconds = DevSwingPhase is { } ph && !dead
            ? ph * StrikeSeconds
            : skilling ? _skillWindup * CastSeconds * ContactFraction
            : skillFollow ? CastSeconds * ContactFraction + _skillSinceCast
            : strikeFollow ? StrikeSeconds * ContactFraction + _champSinceHit
            : swinging ? _champWindup * StrikeSeconds * ContactFraction
            : hasDeathClip ? (DownedSeconds - _downedTimer)   // plays through, then CLAMPS on the last frame
            : _anim;

        // A DEAD CHAMPION WITHOUT A DEATH CLIP HOLDS ITS LAST POSE — the fallback freezes the idle.
        if (dead && !hasDeathClip) seconds = 0f;

        // The generated strip, then the idle strip, then the base sprite, then a block. A missing or rejected
        // clip falls back to the still design, so a character whose strip did not survive the quality gate
        // stands there as themselves rather than vanishing. No mirroring: the art faces right (ArtFacesLeft).
        var loop = clip == "idle";
        if (_ui.AnimSprite(b, Character.StripKey(clip), box, seconds, ChampionFps, loop, tint, -1f,
                           flip: ChampionFacesRight)) return;
        if (_ui.AnimSprite(b, Character.StripKey("idle"), box, dead ? 0f : _anim, ChampionFps, loop: true, tint, -1f,
                           flip: ChampionFacesRight)) return;

        var breathe = (int)(MathF.Sin(seconds * 2.1f) * 4f);
        if (_ui.SpriteGrounded(b, Character.SpriteKey,
                               new Rectangle(box.X, box.Y + breathe, box.Width, box.Height), tint, 0.02f,
                               flip: ChampionFacesRight)) return;

        _ui.Fill(b, new Rectangle(box.Center.X - 32, box.Bottom - 80, 64, 72), dead ? Dim : Gold);
    }

    /// <summary>How long a strike clip runs. Eight frames at the champion's frame rate.</summary>
    private const float StrikeSeconds = 8f / ChampionFps;
    /// <summary>Frames per second for the champion's clips. Slower than it was, deliberately.</summary>
    /// <remarks>
    /// 8, was 10. The champion's swing is the single most-watched animation in the game and it was
    /// finishing in 0.8s. It now takes a full second, which is also the window its anticipation clock
    /// scrubs across — so the arm draws back visibly instead of snapping.
    /// </remarks>
    private const float ChampionFps = 8f;

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
