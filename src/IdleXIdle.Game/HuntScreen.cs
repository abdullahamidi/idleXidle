using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
// NO Microsoft.Xna.Framework.Input HERE, ON PURPOSE. This screen reads no input device of its own any
// more: the host hands it a cursor, a click and a wheel delta in TakeInput, and nothing else. Adding
// this using back is the first half of putting a device read into a paint — which is the bug this file
// was combed for on 2026-09-12 (see TakeInput, and tools/check_draw_purity.py).
using IdleXIdle.Core.Animation;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Traits;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game.Presentation;
using IdleXIdle.Game.Vfx;

namespace IdleXIdle.Game;

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
public sealed class HuntScreen : IFocusActors, IActionStage
{
    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Ember = UiInk.Danger;
    private static readonly Color Slate = UiInk.Secondary;
    private static readonly Color Dim = UiInk.Rule;
    private static readonly Color Bloom = new(0x8A, 0x6A, 0xB0);
    private static readonly Color Steel = new(0x7A, 0x9A, 0xC0);
    private static readonly Color Verdant = new(0x5A, 0x9A, 0x4A);
    private static readonly Color Shadow = new(0x11, 0x0E, 0x0D);
    private static readonly Color PanelBg = new(0x15, 0x10, 0x0F, 0xC8);
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
    // 1000 → 880 (UX V2 P1.1): the skill strip owns y 900–1064 now, so the actors stand above it.
    // DERIVED FROM THE STRIP since UI SCALE became a density profile (UI polish P2): the strip grows
    // with its type at 125 / 150 % and the arena gives up that room — the ground line rises with the
    // strip's top edge rather than letting the actors' feet run under it. 880 at 100 %, as before.
    private static int GroundY => SkillStrip.Y - UiMetrics.Space(20);
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
    //
    // HALVED 2026-08-28, with the wave-length change and as one decision with it. The three beats were
    // measured against a wave that was 2.6 seconds of fighting: 2.05 s of transition against 2.6 s of
    // fight meant 44% of the loop was the pause between fights, and the designer's complaint was that
    // the fight never got going. The wave is now 5-7.5 s (ExpeditionTuning.WaveLengthScale), so the
    // transition can afford to be brisk without taking the reading time back — 1.10 s against 6 s is
    // 15% of the loop, and each beat still has its own moment.
    //
    // FallenBeat stays the longest of the three because it is the one with art behind it:
    // DrawCreatureDeath runs on DownedSeconds and is not cut here — the death effect plays across the
    // beats that follow it.
    // THE VALUES ARE CORE'S NOW (P5). The loop's rhythm is economy, not decoration — waves per
    // hour is sim time plus these breaths, and OfflineHunt spends the same beats as time. A second
    // copy here would let live and offline drift apart, which is a silent earnings bug either way.
    private const float FallenBeat = Descent.FallenBeat;
    private const float SpoilsBeat = Descent.SpoilsBeat;
    private const float BreathBeat = Descent.BreathBeat;
    private const float WaveBreakSeconds = Descent.WaveBreakSeconds;
    private const float DownedSeconds = Descent.DownedSeconds;

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
    // 620. The limit that used to stop this was the arena SCISSOR, not the stage: the champion is drawn
    // from a square frame scaled to the box's HEIGHT, so the roster's widest pose covers 518 px, and with
    // the clip starting at x=492 any centre below 751 had its cloak sliced by a vertical edge. The
    // designer's answer was the right one — "gerekirse rectangle ile birlikte" — so ArenaClip moved
    // instead and the figure is free to stand where the composition wants it. The floor is now
    // ArenaClip.X + 259 = 445.
    // A PROPERTY, not a static readonly: the ground line follows the profile, and a static readonly is
    // frozen at class load — it would stand on the 100 % floor whatever the setting said.
    /// <summary>
    /// THE ARENA'S GROUND PLANE: the one y an actor's base pose stands on.
    /// </summary>
    /// <remarks>
    /// <see cref="ChampBox"/> and <see cref="EnemyBox"/> are both laid out to END here, so every draw
    /// path plants its visible sole on this line — a strip, a still, a capped figure alike. It is
    /// named so that nothing has to spell it as "some box's bottom, less a few pixels" again.
    /// </remarks>
    private static int ArenaGround => GroundY;

    /// <summary>
    /// The creatures the arena LAYS OUT and DRAWS this frame — the wave's, or the first
    /// <see cref="DevCreatureCount"/> of them.
    /// </summary>
    /// <remarks>
    /// FIXTURE ONLY (<c>RH_SHOT_CREATURES</c>), and PRESENTATION ONLY: the wave has already been
    /// fought, so trimming the list changes what is drawn and nothing that was decided. It exists for
    /// one comparison the seeded rolls cannot pose — a LONE creature beside a wave of several. That
    /// comparison is what showed a lone creature laid out at the unscaled enemy box while its pack
    /// wore the archetype's scale; both go through <see cref="CreatureRow"/> now, and the fixture
    /// stays so the invariant can be photographed. It composes nothing: the creatures are the wave's
    /// own, in the wave's own order.
    /// </remarks>
    private IReadOnlyList<WaveCreature> LaidOutCreatures
    {
        get
        {
            var comp = _run?.LastWaveCreatures ?? Array.Empty<WaveCreature>();
            return DevCreatureCount is { } n && n > 0 && n < comp.Count
                ? comp.Take(n).ToList()
                : comp;
        }
    }

    /// <summary>FIXTURE ONLY: draw only the first N of the wave's creatures (RH_SHOT_CREATURES).</summary>
    public int? DevCreatureCount { get; set; }

    /// <summary>The plane the PACK stands on — the enemy box's floor, which is the arena's.</summary>
    /// <remarks>
    /// Read instead of the live creature box on purpose: a creature BOBS, and a shadow that bobs with
    /// it is not a shadow. The figure rises off its shade and settles back onto it, which is what the
    /// bob is for; the shade stays where the floor is.
    /// </remarks>
    private static int CreatureGround => EnemyBox.Bottom;

    /// <summary>
    /// An actor's contact shadow, centred ON the plane its feet land on.
    /// </summary>
    /// <remarks>
    /// Every call site used to lift this by a literal — ten pixels for the pack and the hunter, eight
    /// for the boss — so the darkest part of the blob sat at the ankles and each figure read as
    /// hovering a few pixels over its own shade. There is one plane, the actors stand on it, and the
    /// shade is centred on it: the blob's own falloff is what makes the contact soft, not an offset.
    /// </remarks>
    private void ActorShadow(SpriteBatch b, int centreX, int groundY, int width, int height, float strength)
        => _ui.GroundShadow(b, centreX, groundY, width, height, strength);

    /// <summary>Where the hunter stands. PUBLIC because the VFX contract's tests measure against it.</summary>
    public static Rectangle ChampBox => new(620 - 200, GroundY - 430, 400, 430);
    // Rev 3 §16.1: one normal enemy bottom-centred at (1160,735), visible ~320px (range 280–360). A boss is
    // drawn far larger from its own anchor (see the draw), so this box is the NORMAL-enemy size only.
    // 1320 -> 1380 -> 1430. The pack carries the separation the designer asked for, because the champion
    // could not: see the measured left limit above.
    private static Rectangle EnemyBox => new(1500 - 218, GroundY - 440, 436, 440);

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
    // crystal_lich render, with a "bleed streak" to trim) is gone. Its name and art are EnemyPresentation's.
    private const int BossTargetBodyHeight = 540;   // §6/§25: rendered figure height — the boss box is a 540 square
    // Pulled in from x=1360: the boss is far wider than an ordinary creature, and anchored that far right
    // its wing ran into the arena's scissor edge at 1554 and read as sliced off behind the side panels.
    private static Point BossAnchor => new(1330, GroundY + 10);   // travels with the pack — and with the ground line
    private Rectangle _bossBodyRect, _bossFullRect;   // rendered screen rects, set by DrawBoss for the bar/overlay
    private int _bossFrame;
    private string _bossName = "BOSS";

    // THE CAST IS EnemyPresentation's (2026-09-16): a body per REGION and ARCHETYPE, not one per Source
    // scaled to four sizes. Nothing here keys art off a Source any more.

    /// <summary>The enemy key back out of its strip key ("verdant_swarm_idle_strip8_512" → "verdant_swarm").</summary>
    private static string EnemyKeyOf(string stripKey)
    {
        var cut = stripKey.IndexOf("_idle_", StringComparison.Ordinal);
        if (cut < 0) cut = stripKey.IndexOf("_attack_", StringComparison.Ordinal);
        return cut < 0 ? stripKey : stripKey[..cut];
    }


    private readonly UiKit _ui;
    private readonly VfxPlayer _vfx;

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

    /// <summary>
    /// The wave the next descent starts AFTER (0 = from the top). Host-fed each frame from the region's
    /// chosen checkpoint, already reduced to 0 when the Memory Dust it costs is not there.
    /// </summary>
    public int StartWave { get; set; }

    /// <summary>
    /// A wave the NEXT descent may open at for FREE — where an absence left the champion standing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playtest 2026-09-09: <i>"The game shouldn't start at Wave 1 every time it's launched. We need
    /// to convince the player that the simulation continues even while the game is closed, picking up
    /// from the middle of a run."</i> It always did simulate — <c>OfflineHunt</c> runs the real descent
    /// against the real build — and then threw the depth away and opened the live run at wave 1.
    /// </para>
    /// <para>
    /// <b>Separate from <see cref="StartWave"/> because one is PAID and this is not.</b> A checkpoint
    /// costs 25 Memory Dust per wave, every descent, and StartRun charges it below; charging for a
    /// resume would bill the player for time they already spent. The host consumes this after ONE
    /// descent, so the checkpoint's real revenue — the second, third and twentieth run of a session —
    /// is untouched, and a player cannot farm depth by closing and reopening the game.
    /// </para>
    /// </remarks>
    public int FreeStartWave { get; set; }

    /// <summary>How many descents this screen has begun. The host watches it to spend the free resume.</summary>
    public int RunsStarted { get; private set; }

    /// <summary>Set by StartRun when a descent began at a checkpoint: the Dust the host must now take.</summary>
    public int CheckpointCharge { get; set; }

    /// <summary>Host-fed: the region is already conquered, so the banner says CONQUERED from wave one (review 2026-08-26).</summary>
    public bool RegionConquered { get; set; }

    /// <summary>
    /// The wave model the next descent is fought under. <see cref="ExpeditionTuning.Default"/> is the
    /// game; the host hands in <see cref="ExpeditionTuning.UntilTheFirstBossFalls"/> while the career
    /// has never felled a boss.
    /// </summary>
    /// <remarks>
    /// READ AT <c>StartRun</c>, like the enemy baseline beside it — the tuning is captured readonly by
    /// the expedition, so it can only change between descents. That is exactly the grain the FTUE needs:
    /// the window it opens is measured in WAVES (<see cref="ExpeditionTuning.TutorialWaves"/>) and closes
    /// on its own at the tutorial boss, so a descent that runs on past it is at full strength from wave
    /// six without anything having to be revoked mid-run.
    /// </remarks>
    public ExpeditionTuning Tuning { get; set; } = ExpeditionTuning.Default;

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

    // THE DESCENT IS CORE'S NOW (P5). This screen configures and drives it — and draws it — but
    // the run state machine itself is headless: OfflineHunt runs the same machine with nobody
    // watching, which is what makes offline credit real simulation instead of a rate guess.
    private readonly Descent _descent = new();
    private SoloExpedition? _run => _descent.Run;
    private Champion? _champ => _descent.Champion;
    private float _anim;
    private float _downedTimer;
    private float _breakTimer;   // the between-wave breath; while >0 the cleared frame holds, then the next wave begins

    // Replay state.
    private float _playheadMs;

    /// <summary>
    /// Where each skill stood in its cycle when the LAST wave ended — BEATS taken since its last cast,
    /// and milliseconds since it, carried into the wave now being replayed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE DIALS USED TO RESTART EVERY WAVE, and the fight did not (playtest 2026-08-28: "wave başlayınca
    /// skiller resetlenmesin, aynı akışında devam etsin"). Cooldowns are expedition-cumulative in the sim
    /// — <c>Champion.BeatCount</c> never resets and <c>ReadyAtBeat</c> persists, which is what makes a
    /// descent one continuous fight — but <see cref="WaveReplay"/> is built per wave and knows only the
    /// events of the wave it holds. So <c>LastSkillBefore</c> answered "never" at every wave's opening and
    /// the ring wound back to empty on a skill that was four beats into a six-beat cycle.
    /// </para>
    /// <para>
    /// The lie was visible and expensive: a four-action Projectile that had cast late in one wave could
    /// go a whole short wave without firing, its ring crawling up from zero, and then cast on the FIRST
    /// action of the wave after — from a ring the player had watched sit near empty. The rhythm the rail
    /// exists to teach looked arbitrary, and the readout was the only part that was wrong.
    /// </para>
    /// <para>
    /// Folded at each boundary in <see cref="FoldSkillCarry"/> and added back in the rail's own arithmetic
    /// whenever this wave holds no cast of its own to measure from. Cleared by <see cref="StartRun"/>: a
    /// new descent is a new champion, and its cooldowns really do start empty.
    /// </para>
    /// </remarks>
    private readonly Dictionary<int, int> _carryBeats = new();
    private readonly Dictionary<int, float> _carryMs = new();

    /// <summary>The last event's timestamp in the wave currently replayed — the fold's "end of wave".</summary>
    private float _replayEndMs;

    /// <summary>The wave being replayed, skill by slot — the build THE FIGHT ran (see SoloExpedition.Skills).</summary>
    private IReadOnlyList<EquippedSkill> _waveSkills = Array.Empty<EquippedSkill>();

    /// <summary>
    /// Roll the outgoing wave into <see cref="_carryBeats"/> / <see cref="_carryMs"/>, so the next
    /// wave's rail opens where this one left off instead of at zero.
    /// </summary>
    /// <remarks>
    /// Two cases, and the second is the one that matters. If the skill CAST in the wave that is ending,
    /// the carry is simply what has happened since that cast. If it did NOT cast at all — a short wave,
    /// or a long cooldown — the wave's whole length is ADDED to the carry it already had, because the
    /// skill has been waiting across both. Overwriting instead of adding would quietly re-zero a skill
    /// every time a wave passed without it, which is the same bug one level up.
    /// </remarks>
    private void FoldSkillCarry()
    {
        if (_replay is null) return;
        // Past the last event, so a cast landing exactly on it is still counted as having happened.
        var end = _replayEndMs + 1f;
        var lastBeat = _replay.LastBeat;
        if (lastBeat < 0) return;               // a wave with no action at all changes nothing
        for (var key = 0; key < _waveSkills.Count; key++)
        {
            var last = _replay.LastSkillBefore(end, key);
            if (last >= 0)
            {
                _carryBeats[key] = lastBeat - _replay.BeatAt(last);
                _carryMs[key] = _replayEndMs - last;
            }
            else
            {
                // The skill sat this wave out: ADD the beats it passed. The first beat is counted too,
                // hence FirstBeat - 1 rather than FirstBeat — the wave's opening action is one the
                // skill waited through like any other.
                _carryBeats[key] = _carryBeats.GetValueOrDefault(key) + (lastBeat - _replay.FirstBeat + 1);
                _carryMs[key] = _carryMs.GetValueOrDefault(key) + _replayEndMs;
            }
        }
    }

    // CHARGE — latched off Charge events at the playhead, never re-derived (a replayed rule can
    // drift from the sim's). Live only when the build carries a keystone that reads the pool.
    private int _chargeNow;
    private bool _chargeLive;
    private bool _undyingLive;
    private int _chargeCap = SoloBattle.ChargeCap;
    private WaveReplay? _replay;
    private float _champLunge, _enemyLunge, _enemyWindup;

    /// <summary>How far the champion's swing carries his draw box to the right, at full lunge.</summary>
    /// <remarks>
    /// Named because two things read it and they must not drift: the draw box, which follows the
    /// lunge, and the published envelope, which spans the whole travel so the inspector's keep-out
    /// does not slide with it (see <see cref="LayoutActors"/>).
    /// </remarks>
    private const int ChampLungePx = 40;   // ui-size-ok: arena travel in canvas pixels, not type
    /// <summary>Where DrawComposition last put the creature row — what its labels anchor to.</summary>
    /// <remarks>
    /// Published rather than recomputed because the row's position is the product of an archetype
    /// scale, a spacing compression and two clamps, and every place that re-derived it got a different
    /// answer from the one on screen.
    /// </remarks>
    /// <summary>Set by the SPEND POINTS button — the host opens the MASTERY tree (the E screen).</summary>
    /// <remarks>
    /// The button used to set <see cref="WantsBuild"/> and the host sent the player to the BUILD
    /// screen, where mastery points cannot be spent at all. Playtest: "points are not spent there —
    /// it should send me to MASTERY."
    /// </remarks>
    public bool WantsMastery { get; set; }


    /// <summary>Is the VAULT open to the player yet? Host-fed from the unlock gates.</summary>
    /// <remarks>
    /// A reward button must never advertise a locked door — pressing "OPEN 1 CHEST" only to be told
    /// the screen is not open yet teaches distrust of the whole rail. A reward whose screen is locked
    /// is HIDDEN outright, and the default is false so a fixture with no host offers no errand it
    /// cannot honour.
    /// </remarks>
    public bool VaultOpen { get; set; }

    /// <summary>Is the MASTERY tree open to the player yet? Host-fed, same rule as <see cref="VaultOpen"/>.</summary>
    public bool MasteryOpen { get; set; }

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
    /// <summary>How late a Trap's clip may still start after the trap bit, in replay ms.</summary>
    /// <remarks>
    /// A quarter of a beat. The trap fires on the enemy's swing, which the replay reaches on its own
    /// clock, and a clip allowed to start a whole beat late would play over the NEXT action.
    /// </remarks>
    private const float TrapClipGraceMs = 380f;

    // ── The champion's committed clip (2026-08-25). See UpdateChampionClip. ──
    private string? _clipName;        // "attack" / "cast" while a clip is committed; null = idle
    private float _clipStartMs;       // replay-clock ms the clip began
    private float _clipSpeed = 1f;    // >1 when the beat came sooner than one clip can play
    /// <summary>The authored clip length in replay ms at speed 1 — eight frames at the champion's rate.</summary>
    private const float ClipMs = 1000f * 8f / ChampionFps;
    /// <summary>The fastest a clip may be run to catch a beat. Past this the swing lands a beat late rather than blurring.</summary>
    // 1.5, from 2.5. This is the ceiling on catching up when the champion came free INSIDE the window
    // before a beat: at 2.5 the eight frames of a clip played in under half a second and the designer read
    // it as dropped frames rather than as haste. Past this the contact lands a little late instead, which
    // reads as a heavy blow — the trade the comment on UpdateChampionClip always intended.
    private const float MaxClipSpeed = 1.5f;
    private float _enemyBaseHealth = 120f, _enemyBaseDamage = 9f;
    private WaveOutcome _outcome = WaveOutcome.Cleared;

    // Feedback so a WATCHED fight actually reads: a wave-cleared banner, the next enemy sliding IN from
    // the right, and a red flash when the champion falls. Without these, waves passed silently and the
    // playtest note was exactly that — "I can't tell what's happening".
    private float _enemyEnter;    // 1 → 0: how far off-screen-right the new enemy still is
    private const float EnemyEnterPerSecond = 1.25f;   // the slide takes ~0.8 s: long enough to read as an ARRIVAL, not an appearance
    private float _bannerTimer;   // 1.2 → 0: the "WAVE N CLEARED" flash
    private string _bannerText = "";
    private float _deathFlash;    // 1 → 0: red flash on a fall — drawn over the WHOLE canvas (see Draw)
    private int _fellWave = 1;    // the wave the champion fell at — stamped on a trait the fall awakens

    /// <summary>Deepest wave reached in this region, across restarts — what conquest is measured against.</summary>
    public int Deepest => _descent.Deepest;

    /// <summary>The keystones the world has taught this account — set by the host.</summary>
    public IReadOnlyList<Keystone> DiscoveredKeystones { get; set; } = Array.Empty<Keystone>();

    /// <summary>The Vows the account has found — set by the host.</summary>
    public IReadOnlyList<Vow> KnownVows { get; set; } = Array.Empty<Vow>();

    /// <summary>
    /// What the descent that just ended proved, per Vow: cleared waves with the rule held and unsworn.
    /// </summary>
    /// <remarks>
    /// Snapshotted on the frame the run ends, beside the report, because the champion regroups a few
    /// seconds later and mints a fresh expedition with an empty counter. The host reads it on the same
    /// frame it reads <see cref="LogDirty"/> — the one frame a finished descent can still be asked
    /// anything at all.
    /// </remarks>
    public IReadOnlyDictionary<string, int> LastRunVowProof { get; private set; }
        = new Dictionary<string, int>();

    private readonly List<Callout> _callouts = new();

    /// <summary>Batch indices whose damage has been summed into an earlier blow's number (§21).</summary>
    private readonly HashSet<int> _summed = new();
    /// <summary>Which column a callout stacks in. Two columns that never collide must not push each other.</summary>
    private enum CalloutLane { Champion, Enemy }

    private struct Callout
    {
        public string Text; public Color Color; public int X, Y; public float Life; public int Px;
        public CalloutLane Lane;
        /// <summary>A tag for a callout that may be ADDED TO after it is posted — the hunter's own damage number.</summary>
        public int Id;
        /// <summary>Part of a cleared wave's haul (ShowSpoils): the clear is not SHOWN while one is still rising.</summary>
        public bool Spoil;
    }

    // ── THE HUNTER'S OWN DAMAGE NUMBER (2026-09-06). ─────────────────────────────────────────────────
    //    Enemy damage has read as a number since the first build; Health the HUNTER lost never did — a
    //    bite was a lunge, a thud and a red flash. The number is the FIRST reading a player has of "how
    //    hard does this wave hit me", and its rule is strict: a number only for Health actually lost.
    //    Shield absorption keeps its own barrier feedback (ShieldAbsorbed), and a bite the build stopped
    //    outright shows nothing — the sim reports those as a strike with Amount 0.
    //
    //    COALESCED, not spammed: five creatures bite as one wave clock, and a pack of eight at 150 ms
    //    would be eight numbers a second. Hits inside HunterHitMergeSeconds add into the number already
    //    rising, which then reads "-118" for the pack's bite rather than "-14 -16 -15 -13 …".
    private int _nextCalloutId = 1;
    private int _hunterHitId;
    private int _hunterHitTotal;
    private float _hunterHitAt = -10f;
    private const float HunterHitMergeSeconds = 0.35f;

    /// <summary>
    /// What the hunter's floating number says for an enemy strike, or null for no number: only Health
    /// actually lost prints — never Shield absorption, never a bite the build prevented.
    /// </summary>
    internal static string? HunterHitText(BattleEvent e)
        => e.Kind == BattleEventKind.EnemyStrike && e.Amount > 0 ? $"-{e.Amount}" : null;

    private void HunterHit(BattleEvent e)
    {
        if (!ShowDamageNumbers || HunterHitText(e) is null) return;
        if (_anim - _hunterHitAt <= HunterHitMergeSeconds)
        {
            for (var i = 0; i < _callouts.Count; i++)
            {
                if (_callouts[i].Id != _hunterHitId) continue;
                var c = _callouts[i];
                _hunterHitTotal += e.Amount;
                c.Text = $"-{_hunterHitTotal}";
                c.Life = MathF.Max(c.Life, 0.9f);
                _callouts[i] = c;
                _hunterHitAt = _anim;
                return;
            }
        }
        _hunterHitTotal = e.Amount;
        _hunterHitId = _nextCalloutId++;
        _hunterHitAt = _anim;
        _callouts.Add(new Callout
        {
            Text = $"-{e.Amount}",
            Color = Ember,
            X = ChampBox.Center.X + ChampBox.Width / 5,   // off the centre line, so it never sits on a SAY
            Y = ChampBox.Y + ChampBox.Height / 3 - StackSlot(CalloutLane.Champion) * CalloutLineHeight,
            Life = 1f,
            Px = DamagePx,
            Lane = CalloutLane.Champion,
            Id = _hunterHitId,
        });
    }

    /// <summary>
    /// Vertical gap between stacked callouts. MUST exceed <see cref="CritPx"/>, the tallest thing stacked.
    /// </summary>
    /// <remarks>
    /// This was 44 while damage numbers drew at 52px and crits at 72px, so consecutive numbers were
    /// guaranteed to overlap by 8 to 28 pixels — the stack existed, did its arithmetic correctly, and
    /// still produced a smear, because the spacing was smaller than the glyphs it was spacing.
    /// Derived from the crit rung it has to clear, so the profile that grows the numbers grows the lane.
    /// </remarks>
    private static int CalloutLineHeight => CritPx + UiMetrics.Space(8);

    /// <summary>
    /// How many lines the stack climbs before wrapping back to the bottom.
    /// </summary>
    /// <remarks>
    /// Counting every live callout without a wrap sends a sustained flurry marching off the top of the
    /// arena. Wrapping reuses the lowest slot, by which time the number that was there has faded.
    /// </remarks>
    private const int CalloutLanesDeep = 5;

    // The three combat-callout sizes, from the one place the game keeps its type. They were three
    // private constants here while UiTypography carried a DamageNormal and a DamageCritical that
    // nothing drew with — the same three sizes, written down twice, disagreeing.
    private static int DamagePx => UiTypography.DamageNormal;
    private static int SkillHitPx => UiTypography.DamageSkill;
    private static int CritPx => UiTypography.DamageCritical;
    /// <summary>The champion's own callout — a skill's name as it is cast. Between a hit and a skill hit, and it follows the profile like them.</summary>
    private static int SayPx => UiMetrics.Text(36);

    /// <summary>Where the enemy wave label sits, relative to the row anchor.</summary>
    /// <remarks>
    /// The wave-total health bar that anchored here is gone (see DrawComposition); the offset stays
    /// because the damage-callout stack derives from it, and moving it would move every number.
    /// </remarks>
    private const int NameplateOffset = -52;

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
    private int _strikeCount;   // every other Strike gets a hit-puff; the NUMBER prints on every one (it is the sim's)

    // ── Arena clipping + overlay state (Rev 4 §1/§2/§11). ──
    // Widened and shifted right: left edge clears the control rail (ends x=280), right edge stops
    // short of the existing right rail (starts x=1570), bottom stops short of the nav rail (y=934).
    // THE STAGE THE ACTORS ARE LAID OUT ON. Widened right (1554 -> 1722) so the pack can actually stand
    // further right: the row's resting x is clamped to this rect, and a swarm of four already clamped to
    // ~1160 against a centre of 1320, so moving EnemyBox alone moved nothing at all.
    // Its foot is the skill strip's top: the strip grows with the profile and the stage yields to it.
    private static Rectangle ArenaRect => new(300, 150, 1500, SkillStrip.Y - 150);

    /// <summary>
    /// Where arena pixels may LAND — wider than the stage the actors are placed on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These were one rectangle and should never have been: "where an actor may stand" and "where a
    /// pixel may land" are different questions. Sharing an answer meant a figure standing legally at the
    /// stage's edge had its overhang sliced by a straight vertical line — the champion's cloak on the
    /// left, and every creature SLIDING IN from the right, which the row code even documents as expected
    /// ("Overhang is handled by the arena scissor... transient overshoot"). The designer saw the slice,
    /// twice: "spriteyi kesiyor", then "hala düşman spritesi kesiliyor".
    /// </para>
    /// <para>
    /// It runs from the nav rail's edge to the screen's. That is safe because the region art covers the
    /// FULL screen width — sampled at y=900 and y=1020, x=200 and x=1850 are the same floor as x=1000 —
    /// so an overhanging figure stands on stage rather than on background, and the side panels are drawn
    /// after the arena and cover whatever strays under them.
    /// </para>
    /// </remarks>
    private static Rectangle ArenaClip => new(186, 150, 1734, SkillStrip.Y - 150);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>
    /// Hand-measured against this screen's real layout (the hunter panel at (196,20,420,205), the stage
    /// header at (630,18,560,135), the SKILLS rail at (190,236,286,350) for one skill, the right column
    /// from x 1570 down to the CHEST FILTER row, the champion in ChampBox, the pack right of it)
    /// with a margin of about ten pixels so the frame art is inside the light, not cut by it. A
    /// FRESH SAVE IS NO LONGER THE ONLY STATE THIS DRAWS OVER — LEARN THIS SCREEN offers the tour
    /// at any depth, to a player with four skills docked and a full errand rail — so the figures
    /// above survive only as the 100 % baseline, and every light whose extent moves with play is
    /// read from what the screen LAST DREW. The fight screen is not inset, so these are already
    /// chrome coordinates and the host adds no margin of its own.
    /// </remarks>
    // DERIVED from the rects the screen draws (UI polish P2): the card, the strip and the stage move
    // with the profile, and a light measured for the 100 % layout would fall beside them at 150 %.
    // The numbers are the same as the hand-measured ones at 100 %.
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.Champion => new[] { new Rectangle(ChampBox.X + 50, ChampBox.Y - 10, ChampBox.Width + 60, ChampBox.Height + 20) },
        // Lit from the TALLEST normal box, not the neutral one: a lone creature wears its archetype's
        // scale now (CreatureRow), so a first wave that rolls a Bruiser stands 52 px taller than the
        // enemy box, and a light sized to the neutral box would cut the top off the creature the
        // card is pointing at. The Bruiser box is the table's ceiling, so it is the honest static bound.
        TourTarget.Enemies => new[]
        {
            new Rectangle(940, EnemyBox.Bottom - ArchetypeBox(Archetype.Bruiser).Y - 10, 680, ArchetypeBox(Archetype.Bruiser).Y + 20),
            Inflated(StageHeader, 10),
        },
        TourTarget.HunterHud => new[] { new Rectangle(HunterCard.X - 10, HunterCard.Y - 10, HunterCard.Width + 20, s_hunterCardBottom - HunterCard.Y + 20) },
        // THE HEADER ALONE — where the region, the wave and the conquest bar are. Enemies above lights
        // the creatures AND this, because a card about what is being fought wants both; a card about
        // DEPTH wants only the panel the number is in.
        TourTarget.StageHeader => new[] { Inflated(StageHeader, 10) },
        // THE GLEAM CAPSULE ALONE — the card is titled GLEAM and says Gleam pays for training, and
        // the light used to frame all three pills beside it (playtest 2026-09-09). Read from the row
        // the host just drew, so the card's claim and the lit rectangle cannot drift apart again.
        TourTarget.CurrencyPills => new[] { Inflated(Game1.GleamPillRect, 6) },
        TourTarget.Skills => new[] { Inflated(s_dockRect, 10) },
        // Idle rate, errands, the filter row (closed) — down to wherever the filter row LAST drew. The
        // rail's height depends on how many errands are up, and a new game now holds the welcome chest,
        // so the fixed 268 px measured for an empty rail sliced the filter row in half on every first
        // intro (review 2026-08-26). The screen draws before the tour asks, so the measure is fresh.
        TourTarget.RightColumn => new[] { new Rectangle(1560, 100, 350, Math.Max(150, s_railBottom - 100)) },
        TourTarget.NavRail => new[] { new Rectangle(0, 0, 184, 1080) },
        // The lesson card hangs under the header stack (UX V2 P0.7) — the same slot the toasts use.
        TourTarget.LessonSlot => new[] { new Rectangle(630, s_headerStackBottom + 8, 560, 130) },
        // THE MEDALLION ITSELF — the same rectangle DrawLogButton draws from and UiKit.ClickedIn
        // hit-tests, so the light and the click cannot drift apart. The first failure lesson (IT FELL) is
        // the most important prompt in the game and it pointed at nothing until this arm existed.
        TourTarget.LogButton => new[] { Inflated(LogButtonRect, 10) },
        // THE ENVELOPE IN THE HOST'S OWN CHROME, read from the one rectangle Game1 paints and
        // hit-tests — the pattern CurrencyPills set. The lesson about it sends the player nowhere, so
        // a null Sends reads as "the HUNT" and this is where that resolution lands.
        TourTarget.DispatchIcon => new[] { Inflated(Game1.DispatchButton, 10) },
        _ => Array.Empty<Rectangle>(),
    };

    /// <summary>A rectangle grown by <paramref name="by"/> on every side — a spotlight's margin around the thing it lights.</summary>
    private static Rectangle Inflated(Rectangle r, int by) => new(r.X - by, r.Y - by, r.Width + by * 2, r.Height + by * 2);
    private RasterizerState? _arenaRasterizer;
    private RasterizerState ArenaRasterizer => _arenaRasterizer ??= new RasterizerState { ScissorTestEnable = true };
    private float _bossIncomingTimer;
    private bool _isBossWave;
    /// <summary>Dev fixture (F6 / RH_SHOT_MODE=boss): render the current wave as the Crystal Lich boss.</summary>
    public bool DevForceBoss { get; set; }
    /// <summary>Dev boss-bounds overlay (F7): draws ground pivot / body / full / arena rects (Rev 5 §17).</summary>
    public bool DevBossDebug { get; set; }

    /// <summary>
    /// DEV ONLY — the VFX placement contract's debug view (brief §70). Never true in a shipped build.
    /// </summary>
    /// <remarks>
    /// Three doors, none of them open to a player: F9 under <c>RH_DEV=1</c> (beside F6's boss and F7's
    /// layout overlays), <c>RH_SHOT_MODE=vfxdebug</c> for the capture rig, and <c>RH_VFX_DUMP=1</c> for
    /// the text version, which is the one that actually gets read. F9 rather than F8 because F8 already
    /// steps the UI SCALE.
    /// </remarks>
    public bool DevVfxDebug { get; set; }

    /// <summary><c>RH_VFX_BUDGET=1</c>: print the asset-scale ledger once, from the real textures.</summary>
    private static readonly bool WriteBudgetLedger =
        Environment.GetEnvironmentVariable("RH_VFX_BUDGET") is "1" or "true";

    /// <summary>
    /// FIXTURE DIAL — <c>RH_SHOT_SHIELDFX=gain|absorb|break</c>: hold one shield transient at its peak
    /// so the shutter can photograph it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three shield feedbacks are 100–350 ms long and are fired by the SIMULATION, not by a click,
    /// so a shutter at frame 60 lands on them only by luck — and a state no capture can pose has never
    /// been looked at. This is the same shape as <see cref="DevSwingPhase"/> and
    /// <c>RH_SHOT_SWING</c>: an environment dial the SCREEN reads (the host is not involved), which
    /// freezes the effect rather than moving the shutter.
    /// </para>
    /// <para>
    /// <c>gain</c> and <c>absorb</c> re-arm their one-shot from <see cref="UpdateFight"/> every frame, so
    /// the bar's rim / notch stands at full when the frame is saved. They also fire the matching
    /// BARRIER effect once, slowed to <see cref="PosedFxFps"/> so it is still on its first, brightest
    /// frame at the shutter — the standing barrier breathes between 0.40 and 0.60 alpha (raised with
    /// the art, 2026-09-04) and the flare is still its brightest moment, which the brief's §106 asks to
    /// be shown on two different hunters. Posing only the bar left that to the replay: the same fixture
    /// caught the flare on THE MAGPIE and missed it on THE SEEKER. <c>break</c> lets the real
    /// <see cref="BattleEventKind.ShieldBroken"/> fire and then jumps the burst to the middle of its own
    /// eight frames (see <see cref="PlayShieldBreak"/>), which is the widest moment of the shatter.
    /// It also forces the bar on, so the pose works on any fight mode. Nothing here runs without the
    /// variable, and it is read once at class load.
    /// </para>
    /// <para>
    /// It travels as ordinary process environment — capture.sh forwards only the RH_SHOT_* names it
    /// knows about through <c>RH_ENV</c>, but <c>dn</c> inherits the caller's environment, so:
    /// <code>
    /// RH_SHOT_SHIELDFX=absorb RH_SHOT_UISCALE=100 \
    ///   bash tools/asset-pipeline/capture.sh fightshield build/shots/p2_hunt_absorb_100.png
    /// </code>
    /// </para>
    /// </remarks>
    private static readonly string? ShotShieldFx = ReadShieldFxDial();

    /// <summary>
    /// A FIXTURE DIAL THAT CANNOT HONOUR ITS VALUE MUST SAY SO.
    /// </summary>
    /// <remarks>
    /// This used to accept any string and quietly match none of them, so a typo produced a capture
    /// that looked entirely plausible and posed nothing — the shot was of the DEFAULT moment, and
    /// whoever read it believed they had photographed the state they asked for. That is the failure
    /// this project has already paid for twice: a bench that cannot pose a condition manufactures a
    /// verdict. A misconfigured fixture aborts instead.
    /// </remarks>
    private static string? ReadShieldFxDial()
    {
        if (Environment.GetEnvironmentVariable("RH_SHOT_SHIELDFX")?.Trim().ToLowerInvariant() is not { Length: > 0 } v)
            return null;
        string[] known = { "gain", "absorb", "break" };
        if (Array.IndexOf(known, v) < 0)
            throw new InvalidOperationException(
                $"RH_SHOT_SHIELDFX='{v}' is not a shield moment this fixture can pose. " +
                $"Use one of: {string.Join(", ", known)}. (The held barrier needs no dial — it is drawn " +
                "whenever the hunter has shield.)");
        return v;
    }

    // Exactly ONE major overlay may show. Priority (high→low): Modal/WelcomeBack (host) > HunterDown >
    // BossIncoming > WaveCleared. The host draws WelcomeBack; when it does, the screen draws none of its own.
    private enum HuntOverlay { None, HunterDown, BossIncoming, WaveCleared }

    private HuntOverlay ResolveOverlay(bool welcome)
    {
        if (welcome) return HuntOverlay.None;

        // THE FALL PAINTS NOTHING HERE, AND OUTRANKS EVERYTHING. Its presentation is the collapse, the
        // flash and the black, all read from the clocks at the foot of Draw — never from this, which the
        // host's toast can silence. Resolving it keeps BOSS INCOMING and WAVE CLEARED off the stage until
        // the next descent is visible; the report itself waits in the EXPEDITION LOG (L).
        if (DeathTransitionUp) return HuntOverlay.HunterDown;
        if (DevBossCall || _bossIncomingTimer > 0f) return HuntOverlay.BossIncoming;
        if (DevForceBoss) return HuntOverlay.None;   // boss verification fixture: active combat, no wave banner
        if (_bannerTimer > 0f) return HuntOverlay.WaveCleared;
        return HuntOverlay.None;
    }

    // THE GUIDE IS NOT HERE. It moved to the host as shared chrome — Game1.DrawHuntLesson over the
    // fight, Game1.DrawHintSlot on a menu screen — and the settable property that stood here as a
    // tombstone went with the tutorial ladder it was typed on. A capture fixture or an audit looking
    // for "the guide on the hunt screen" wants OnboardingDirector and those two draws.

    /// <summary>The player's build choices and the tree that powers them. Set by the host each frame.</summary>
    /// <summary>
    /// The account's trait ledger, or null. Set by the host, handed to each descent it starts.
    /// </summary>
    /// <remarks>
    /// The screen never writes it — <c>TraitDiscovery</c> is the only writer of a discovered set —
    /// and never reads a trait's rule. All it does is pass it to the run and re-check the rules at
    /// the one moment failure means something.
    /// </remarks>
    public TraitWatch? TraitWatch { get; set; }

    public PlayerLoadout Loadout { get; set; } = PlayerLoadout.Starter();
    public MasteryTree Mastery { get; set; } = new();
    public Source? EnemySource { get; set; }

    /// <summary>
    /// FIXTURE ONLY (<c>RH_SHOT_SOURCE</c>): draw another region's creatures without conquering to it.
    /// </summary>
    /// <remarks>
    /// Since the arena's cast is chosen by region and archetype (<see cref="EnemyPresentation"/>), a
    /// Source pose picks the FAMILY of the region whose theme it is — Spirit draws the Pale Choir's —
    /// and the wave glyph. <see cref="EnemySource"/> itself cannot be posed: the host rewrites it from
    /// the active region every frame. This overrides the ART, nothing else — no baseline, no roll, no
    /// reward.
    /// </remarks>
    public Source? DevEnemySource { get; set; }

    /// <summary>The Source the wave strip falls back to when the wave carries none: the rig's, else the region's.</summary>
    private Source? ArtSource => DevEnemySource ?? EnemySource;

    /// <summary>The region whose family the arena DRAWS: the rig's Source pose's, else the active one.</summary>
    private string ArtRegion
        => DevEnemySource is { } posed && EnemyPresentation.RegionOfTheme(posed) is { } posedRegion ? posedRegion : RegionId;

    /// <summary>The look this wave's creatures wear — the region's body for the archetype the run rolled.</summary>
    private EnemyLook CreatureLook => EnemyPresentation.For(ArtRegion, WaveArchetype);

    private string? _warmedChampion;
    private string? _warmedRegion;
    private bool _warmedForcedBoss;

    /// <summary>
    /// Load, from Update, the art families this arena is about to draw: the champion's clips, the four
    /// creatures of the region it draws, and that region's boss. The library defers every animation strip
    /// to first use (<see cref="AssetLibrary.IsDeferred"/>); asking here keeps the decode out of Draw. Costs
    /// one comparison a frame until the champion or the region changes.
    /// </summary>
    public void WarmArt()
    {
        var champion = Character.Id;
        var region = ArtRegion;
        if (champion == _warmedChampion && region == _warmedRegion && DevForceBoss == _warmedForcedBoss) return;
        _warmedChampion = champion;
        _warmedRegion = region;
        _warmedForcedBoss = DevForceBoss;
        var assets = _ui.Assets;
        assets.Warm("char_" + champion + "_");   // every clip of this champion (Character.StripKey's family)
        assets.Warm("fx_" + champion + "_");     // and its own effect strips (the forms it casts in)
        var family = EnemyPresentation.For(region, Archetype.Swarm).RegionId;
        foreach (var look in EnemyPresentation.Normals)
            if (look.RegionId == family) assets.Warm(look.ArtKey + "_");
        if (BossArt is { } boss) assets.Warm(boss.ArtKey + "_");
    }

    /// <summary>
    /// The Source a creature in <paramref name="slot"/> really carries (Core's roll), else the wave's fallback.
    /// </summary>
    private Source? CreatureSource(int slot)
    {
        if (DevEnemySource is { } posed) return posed;
        var comp = _run?.LastWaveCreatures;
        return comp is not null && slot >= 0 && slot < comp.Count && comp[slot].Source is { } s ? s : ArtSource;
    }
    /// <summary>The active region id — picks the boss creature (boss_&lt;region&gt;) on boss waves. Set by the host.</summary>
    public string RegionId { get; set; } = "";
    /// <summary>The active region's combat character — handed to the run so the enemy's bite tempo matches it.</summary>
    public AttackBias EnemyBias { get; set; } = AttackBias.Balanced;
    /// <summary>Unopened chests waiting in the Forge — drives the "you have things to do" nudge. Set by the host.</summary>
    public int ChestCount { get; set; }
    /// <summary>Gleam per second the idle champion earns — shown in the HUNT idle-rewards panel. Set by the host.</summary>
    public float IdleGleamRate { get; set; }
    /// <summary>The shared audio bank — set by the host like every other service here. Null runs silent.</summary>
    /// <remarks>
    /// Every cue goes THROUGH the bank (never a raw SoundEffect), so the settings EFFECTS slider
    /// governs all of it and the bank's per-cue rate limit keeps a swarm wave from stacking five
    /// copies of one sample. Deliberately independent of the fight's text/effects toggles: those
    /// govern what is DRAWN; only the volume sliders govern what is heard.
    /// </remarks>
    public SoundBank? Sound { get; set; }
    private Source? _lastSource;
    private Hunter? _hunter;

    public HuntScreen(UiKit ui)
    {
        _ui = ui;
        _vfx = new VfxPlayer(ui) { Bounds = _actors };
    }

    /// <summary>
    /// Where every figure VISIBLY is this frame — the one thing the effects pass resolves against.
    /// </summary>
    /// <remarks>
    /// Filled by <see cref="LayoutActors"/> as the first statement of <see cref="DrawArena"/>, before a
    /// single figure is drawn, so an effect can never be aimed at last frame's rectangle and can never
    /// miss a lunge the draw applied after it.
    /// </remarks>
    private readonly VfxBoundsRegistry _actors = new();

    // ── Reward channel: one entry per cleared wave, drained by the host. The record is Core's
    //    (Expeditions.WaveReward) since P5, and the DESCENT banks a wave the moment the sim clears
    //    it; this queue holds the ANNOUNCED rewards — forwarded when the replay catches up, so the
    //    player is paid at the moment they SEE the clear rather than a whole replay early. ─────────
    private readonly Queue<WaveReward> _rewards = new();
    public bool HasReward => _rewards.Count > 0;
    public WaveReward TakeReward() => _rewards.Dequeue();

    // ── WHAT THE REST OF THE GAME MAY ASK ABOUT THE FIGHT ─────────────────────────────────────────
    //
    // The champion fights on EVERY screen (Game1 ticks the expedition above every overlay), and until
    // 2026-09-09 nothing outside this file could tell. Playtest: "It wasn't clear that the HUNT screen
    // is the main combat screen while navigating other menus. We need a live feedback mechanism to
    // show that combat is happening there." Three read-onlys is the whole of the plumbing that was
    // missing — the facts were already being recomputed sixty times a second, behind private fields.

    /// <summary>The champion's health, 0..1 — what the arena's own bar draws.</summary>
    public float ChampionHealthFraction => _replay?.HealthFractionOf(0) ?? 1f;

    /// <summary>Seconds since the champion was last bitten. Zero on the frame it happens.</summary>
    public float SinceChampionHit => _enemySinceHit;

    /// <summary>Is the champion down and regrouping right now?</summary>
    public bool ChampionDowned => _mode == Mode.Downed;

    // ── WHAT AN AUTHORED OPENING WAITS FOR. ─────────────────────────────────────────────────────
    //
    // Three readings and one dial, all of them about PRESENTATION — where the animation has got to,
    // and where the playhead is. The simulation is untouched: the wave was resolved before any of
    // this ran, and holding the replay changes what is on screen, never what happened.

    /// <summary>
    /// Park the playhead one millisecond short of the next beat of this kind. Null to run freely.
    /// </summary>
    /// <remarks>
    /// Set by the host from the authored script, cleared by it the moment the player continues. The
    /// screen does not know what a tutorial is; it knows how to wait.
    /// </remarks>
    public BattleEventKind? HoldBeforeKind { get; set; }

    /// <summary>Is the playhead actually parked against <see cref="HoldBeforeKind"/> this frame?</summary>
    public bool ReplayHeld { get; private set; }

    /// <summary>
    /// Has the wave's pack finished walking on and settled into its combat position?
    /// </summary>
    /// <remarks>
    /// The fight already waits for this before anyone swings (the _enemyEnter hold above), so an
    /// authored step that waits for it is watching the same moment the game does: the entrance plays
    /// whole, and the explanation lands after it and before the first blow.
    /// </remarks>
    public bool EnemySettled => _run is not null && _replay is not null && _enemyEnter <= 0f && _breakTimer <= 0f;

    /// <summary>Is a BOSS the thing standing there?</summary>
    /// <remarks>
    /// Computed from the wave Update keeps current (BeginWave sets <c>_replayWave</c>) — never from the
    /// copy Draw caches, which stops moving the moment the player is on another screen. The fight runs on
    /// every screen, and a reading of it that is stale off the HUNT held the wrong break (review 2026-09-11).
    /// </remarks>
    public bool BossOnStage => DevForceBoss || WaveScaling.IsBossWave(Math.Max(1, _replayWave), ExpeditionTuning.Default);

    /// <summary>
    /// Is a hold in play in THIS session — a beat held now, or one let go and not yet played?
    /// </summary>
    /// <remarks>
    /// The hold and its release live on this instance and die with the process. A reader that waits on
    /// <see cref="ReleasedBeatPlayed"/> can ask this too, so it never waits on a release nobody made.
    /// </remarks>
    public bool HoldInPlay => _heldAtMs is not null || (_releasedAtMs is not null && !ReleasedBeatPlayed);

    /// <summary>
    /// Has a descent actually begun? False until the first Update, because the run starts lazily.
    /// </summary>
    /// <remarks>
    /// The arrival tableau needs exactly one frame of the fight and then stillness: one frame puts a
    /// champion on the stage in its idle stance with the wave's pack still off to the right, which IS
    /// the arrival. The host reads this so it can let that single frame through and hold every one
    /// after it.
    /// </remarks>
    public bool RunStarted => _run is not null && _replay is not null;

    /// <summary>The timestamp of the beat the barrier is holding the replay short of, or null.</summary>
    /// <remarks>
    /// Set while <see cref="ReplayHeld"/>: the playhead is strictly before this millisecond AND before
    /// the beat's own wind-up (see <see cref="ReplayBarrier"/>), so nothing of it is on screen yet.
    /// </remarks>
    public int? HeldEventAtMs { get; private set; }

    /// <summary>
    /// Has the beat the barrier last held been let go, crossed, and PLAYED — the Hunter's clip for it done?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host waits on this before it draws anything else over the fight: a card that arrives the
    /// frame the cast crosses covers the cast it was promising (playtest 2026-09-11, HEALTH over HARD
    /// HANDS). PLAYED means all of the cast the player can see: the Hunter's clip aimed at it has run
    /// out, the effect it launched has finished, and — when the blow ended the wave, as the first cast
    /// of a fresh career does — the fall it caused has played through and faded.
    /// </para>
    /// <para>A beat that no clip was aimed at, and that launched nothing, has played the moment it crosses.</para>
    /// </remarks>
    public bool ReleasedBeatPlayed
    {
        get
        {
            // LATCHED. Once the released beat has played it stays played until the next hold: the
            // conditions below read live state (a clip, the break), and a later clip that happened to
            // aim at the same millisecond of another wave would otherwise un-play it.
            if (!_releasedPlayed && _releasedAtMs is { } released && _releasedCrossed
                && !(_clipName is not null && _clipBeatMs == released)
                && !_vfx.AnyPlaying(_releasedFxFrom, _releasedFxTo)
                && (_breakTimer <= 0f || FallsPlayed))
                _releasedPlayed = true;
            return _releasedPlayed;
        }
    }
    private bool _releasedPlayed;

    /// <summary>
    /// Keep a cleared wave's stage empty until the host lets the next wave in. False in play.
    /// </summary>
    /// <remarks>
    /// The break between waves still plays every beat of its own — the fall, the haul, the empty stage
    /// (<see cref="ClearBeat.Tick"/>); this only stops it handing over. The screen does not know what a
    /// tutorial is; it knows how to wait.
    /// </remarks>
    public bool HoldNextWave { get; set; }

    /// <summary>Has every creature that fell in this wave played its fall through, lain there and faded?</summary>
    public bool FallsPlayed => ClearBeat.FallsPlayed(_anim, _diedAt.Values, FallSeconds);

    /// <summary>
    /// Has the wave the fight just cleared finished being SHOWN? Every fall played and faded, the haul
    /// risen and gone, the stage empty — false during a fight and false on a fall.
    /// </summary>
    /// <remarks>
    /// The PRESENTATION boundary. The purse is paid on the kill's own frame (the reward is queued the
    /// moment the replay runs out); this is the later moment at which the player has actually SEEN the
    /// clear, and it is what a sentence about the clear waits for.
    /// </remarks>
    public bool ClearShown
        => _mode == Mode.Fighting && _replay is { Finished: true } && _outcome == WaveOutcome.Cleared
           && _breakTimer > 0f && FallsPlayed && !_callouts.Any(c => c.Spoil);

    /// <summary>Waves this screen has begun replaying, ever. A monotone count the host can watch.</summary>
    public int WavesBegun { get; private set; }

    /// <summary>EnemyDown beats the replay has crossed, ever.</summary>
    public int EnemyDownsSeen { get; private set; }

    /// <summary>Skill beats the replay has crossed, ever — each one a callout, an effect and a cue.</summary>
    public int SkillCastsSeen { get; private set; }

    /// <summary>The in-wave timestamp of the last Skill beat crossed, or -1.</summary>
    public int LastSkillCastAtMs { get; private set; } = -1;

    /// <summary>The wave number being shown.</summary>
    public int WaveShown => _replayWave;

    /// <summary>Where the replay's playhead stands in the wave, in milliseconds.</summary>
    public float PlayheadMs => _playheadMs;

    /// <summary>
    /// Is the DEATH TRANSITION up — the downed beat, or the black and the lift that follow it?
    /// </summary>
    /// <remarks>
    /// Published for the host: nothing is said over a fall. The lesson card and the coach's spotlight
    /// yield while this is true — a prompt beside a collapsing Hunter, or brackets over a black stage,
    /// is a second thing to look at. Input is the narrower question (<see cref="BlackCovers"/>): the
    /// medallion beside a fallen Hunter is painted, so it is live; only the black blocks it.
    /// </remarks>
    public bool DeathTransitionUp => DeathTransition.Up(_mode == Mode.Downed, _deathFadeIn);

    /// <summary>How black the stage is this frame, 0..1 — what the foot of <see cref="Draw"/> paints.</summary>
    /// <remarks>
    /// ONE READING FOR BOTH HALVES (ADR-006): the fill paints this and <see cref="TakeInput"/> refuses
    /// under it, so the paint and the refusal cannot disagree about whether the HUD is covered. Read from
    /// the two clocks and the Reduced Motion switch alone; DevShowFall holds it at zero so `fightfall`
    /// can photograph the collapse at any point of the beat.
    /// </remarks>
    private float BlackAlpha => DevShowFall ? 0f : DeathTransition.Alpha(_mode == Mode.Downed, _downedTimer, _deathFadeIn, UiMotion.Reduced);

    /// <summary>Is the black covering this screen's own HUD — the medallion, the rail's doors, the inspector?</summary>
    private bool BlackCovers => DeathTransition.Covers(BlackAlpha);

    /// <summary>What each STYLE announces when it fires — the fight is watched, so the effect is the read.</summary>
    private static (string Text, Color Color) CalloutFor(Style style) => style switch
    {
        Style.Hammer => ("HAMMER", Ember),
        Style.Volley => ("VOLLEY", Steel),
        Style.Field => ("FIELD", Bloom),
        Style.Snare => ("SNARE", SourceGlow(Source.Machine)),   // not the crit's gold: a style firing and a graded blow must not share one ink (huntstates-13)
        Style.Sign => ("SIGN", Bone),
        _ => ("DRAIN", Verdant),
    };

    // ══════════════════════════════════════════════════════════════════════════════════════════
    /// <summary>Advance the fight. Takes no input — this screen's clicks are resolved in <see cref="TakeInput"/>.</summary>
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
        PresentTrace.Tick(dt);
        // THE WHEEL IS NOT SAMPLED HERE ANY MORE. It used to be read off Mouse.GetState() in this
        // method, which the host calls only while the fight is allowed to run — so the log's scroll
        // columns went dead for the whole of the authored hold, and any stretch without an Update
        // turned the notches accumulated in the meantime into one enormous jump the next time it ran.
        // The host hands the frame's delta to <see cref="TakeInput"/> now, beside the click.
        // _strikeTime WAS THE OLD SWING CLOCK and is gone. It was armed by the impact and counted down,
        // so the clip played entirely AFTER the blow it was meant to deliver. The champion now runs on
        // _champWindup, the same anticipation model the enemy already used. Leaving a decaying timer
        // here that nothing reads is exactly the kind of dead machinery this codebase keeps finding.
        _champLunge = Math.Max(0f, _champLunge - dt * 5f);
        _enemySinceHit += dt;
        _enemyLunge = Math.Max(0f, _enemyLunge - dt * 5f);
        // 1.25, not 2.5: the slide-in was over in 0.4s, which is too quick to register as creatures
        // ARRIVING rather than simply appearing. Twice as long, and the beat before it is now empty
        // stage, so the entrance has something to be an entrance from.
        if (_devArrivalPose is { } posed) _enemyEnter = posed;   // held for the shutter (DevPoseArrival)
        else _enemyEnter = Math.Max(0f, _enemyEnter - dt * EnemyEnterPerSecond);   // the new enemy slides in over ~0.8s
        _bannerTimer = Math.Max(0f, _bannerTimer - dt);
        if (!DevBossCall) _bossIncomingTimer = Math.Max(0f, _bossIncomingTimer - dt);
        // The flash holds while the fall fixture is posing it — a capture must be able to photograph
        // the one frame it exists to check (does the flash cover the WHOLE screen, corners included).
        if (!DevShowFall) _deathFlash = Math.Max(0f, _deathFlash - dt * 1.5f);
        _deathFadeIn = Math.Max(0f, _deathFadeIn - dt);   // the black after a restart lifts; see DeathTransition
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
            _descent.Reset();
            // The fall's presentation belongs to the region it happened in: carried across travel, a
            // fall's black would land on a region it did not fall in (review 2026-08-24 caught the fall
            // banner of the day doing exactly that). The flash clear is defensive; it decays in under a second.
            _deathFadeIn = 0f;
            _deathFlash = 0f;
        }

        // Keep the region's difficulty current; it is read the next time a run (re)starts.
        _enemyBaseHealth = enemyBaseHealth;
        _enemyBaseDamage = enemyBaseDamage;

        if (_run is null) StartRun(hunter);
        HoldDeathPose();   // the posed transition is re-asserted every frame, like the arrival (`fightfade`)

        // Stat training moved to the CHARACTER screen (press C); this is a pure idle-watch view now.

        switch (_mode)
        {
            case Mode.Fighting: UpdateFight(dt); break;
            case Mode.Downed:
                if (DevHoldReport) break;   // capture fixture: keep the fallen beat up instead of restarting
                _downedTimer -= dt;
                if (_downedTimer <= 0f)
                {
                    // THE RESTART LANDS AT FULL BLACK, on the frame the downed beat runs out — the frame it
                    // has always been: OfflineHunt spends DownedSeconds per fall, and the Dust for a
                    // checkpoint is charged here and spent by the host next frame. The black then holds
                    // through the reset and lifts over the new wave's entrance (DeathTransition).
                    _deathFadeIn = DeathTransition.FadeInSeconds(UiMotion.Reduced);
                    StartRun(hunter);
                }
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Take the frame's UI input. THE ONLY PLACE THIS SCREEN CONSUMES AN INPUT EDGE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>DRAW MUST NOT CONSUME INPUT.</b> MonoGame's fixed timestep calls Update at least once and
    /// Draw exactly once per tick, so a frame over budget runs Update twice and Draw once — and the
    /// host latches the click edge at the top of Update and overwrites the previous mouse state at the
    /// bottom of it. A hit test inside a Draw therefore tests an edge the second Update has already
    /// recomputed as false, and because a press is held for three to six frames it never re-arms: the
    /// click is silently dropped. The title screen shipped with exactly that bug (fixed 2026-09-12).
    /// Every control on this screen is decided here now; its Draw methods paint the same rectangles
    /// and still hover and depress, but they cannot fire.
    /// </para>
    /// <para>
    /// Called from the host's Update on EVERY frame, and deliberately NOT from <see cref="Update"/>:
    /// that one is skipped while the authored opening holds the fight, and the EXPEDITION LOG's
    /// medallion has to answer a click on a held frame.
    /// </para>
    /// <para>
    /// <paramref name="huntOnTop"/> is the host's <c>!OverlayActive</c> — the same condition its draw
    /// chain uses to reach <see cref="Draw"/>. The screen's own HUD (the medallion, the right rail's
    /// door) answers only when the HUNT is the screen on top, so a click in the Forge cannot fall
    /// through into the fight. The LOG does not read it: the log is drawn over every
    /// screen and stays live wherever it was opened from.
    /// </para>
    /// </remarks>
    public void TakeInput(Point mouse, bool clicked, int wheel, bool huntOnTop)
    {
        // THE LOG FIRST, AND ALONE. Under its full-screen scrim the rail and the medallion are furniture —
        // the same `!_logOpen` gate the paint applies, kept as an early return.
        if (_logOpen) { TakeLogInput(mouse, clicked, wheel); return; }

        // AND THE TRANSITION IS SNAPPED TO ITS END THE MOMENT THE SCREEN LEAVES. Update ticks this screen
        // on every other screen too (the restart happens on its own clock wherever the player is), but
        // nothing here paints while another screen is on top — so a lift that ran out unseen must not
        // resume as a black frame when the player comes back.
        if (!huntOnTop) { _deathFadeIn = 0f; return; }
        // UNDER THE BLACK, NOTHING ON THIS SCREEN ANSWERS — and only under the black. The medallion, the
        // rail's doors and the inspector are painted beside a fallen Hunter for the readable beat, so
        // they are live then, exactly as on any other frame; from the fade's first frame to the lift's
        // last they are covered, and refuse. The host's own chrome stays live above the black, and L
        // still opens the log.
        if (BlackCovers) return;
        // The paint's own guard: before the first descent there is no HUD to hit.
        if (_run is null || _replay is null || _champ is null) return;

        // IN THE ORDER THE HUD PASS PAINTS THEM (see the foot of Draw): the medallion, then the right rail.
        if (UiKit.ClickedIn(LogButtonRect, mouse, clicked)) WantsLog = true;
        TakeRailInput(mouse, clicked);
    }

    /// <summary>What the run's build was composed from — compared at every wave boundary (see BeginWave).</summary>
    private string _buildStamp = "";
    private string BuildStamp()
        => $"{Loadout.Signature}|{Mastery.Taken.Count}:{string.Join(",", Mastery.Taken)}|{Character.Id}|{Progress?.Signature}"
           // A keystone discovered mid-descent (a conquest happens at the end of one, but region
           // mastery lands mid-farm) must reach the fight at the next wave boundary like everything
           // else. Without these two the build would keep composing against the old menu.
           + $"|{DiscoveredKeystones.Count}|{KnownVows.Count}";

    /// <summary>Compose the build and refresh what the screen caches off it (the CHARGE pill).</summary>
    private Build ComposeBuild(Hunter hunter)
    {
        var build = Loadout.ToBuild(Mastery, Character, Progress, DiscoveredKeystones, KnownVows);
        _buildStamp = BuildStamp();
        // The CHARGE pill only exists when the pool does.
        var trig = build.Triggers(hunter);
        _chargeLive = trig.Contains(BuildTrigger.Rend) || trig.Contains(BuildTrigger.Capacitor)
                      || trig.Contains(BuildTrigger.Dynamo) || trig.Contains(BuildTrigger.Lodestone);
        _chargeCap = trig.Contains(BuildTrigger.Capacitor)
            ? SoloBattle.ChargeCapExtended : SoloBattle.ChargeCap;
        _undyingLive = trig.Contains(BuildTrigger.Undying);   // the hunter card shows UNDYING only when the build has it
        return build;
    }

    /// <summary>Where the woven skills bank the levels they earn. Set by the host.</summary>
    public SkillProgress? Progress { get; set; }

    private void StartRun(Hunter hunter)
    {
        RunsStarted++;
        var build = ComposeBuild(hunter);
        _recordToBeat = BestDepthHere;   // before a wave is pushed, or the run competes with itself
        _chargeNow = 0;
        // A NEW RUN HAS NOT SEEN A SHIELD YET. The flag latched for the life of the process, so the
        // second descent opened with an empty steel strip on the card before anything had granted one —
        // and the first SHIELD BROKEN of that run, which is the moment that teaches what the strip is,
        // arrived to a bar the player had been staring at since the last champion died. Reset with the
        // run and the introduction happens once per run, where it belongs (§65).
        _shieldSeen = false;

        // A NEW DESCENT IS A NEW CHAMPION. The rail carries a skill's place in its cycle across wave
        // boundaries (see _carryBeats), and carrying it across a DEATH would open the next run with
        // rings inherited from the corpse — the descent mints a fresh Champion, cooldowns and all.
        _carryBeats.Clear();
        _carryMs.Clear();
        _replayEndMs = 0f;

        // THE DESCENT IS CORE'S (P5). RegionId reaches the sim, not just the boss art: it selects
        // the band cycle and the creature roster, which is what makes one region a different PLACE
        // rather than the same place with bigger numbers; the descent's own RunIndex seeds each
        // run's compositions so a replay is identical.
        _descent.RegionId = RegionId;
        _descent.EnemyBias = EnemyBias;
        _descent.Progress = Progress;
        // AND THE WAVE MODEL ITSELF. Default for everybody who has ever felled a boss; the taught-waves
        // tuning until then. Assigned HERE with the other four because the expedition captures it at
        // construction — set it after StartRun and the descent fights the game the last one fought.
        _descent.Tuning = Tuning;
        // The ledger the run's cleared waves feed. Refreshed with WHERE and WHO before the first
        // push, so a first awakening is stamped with the region and champion it happened to.
        _descent.TraitWatch = TraitWatch;
        if (TraitWatch is { } watch) { watch.RegionId = RegionId; watch.CharacterId = Character.Id; }
        // THE DEEPER OF THE TWO: the checkpoint the player paid for, or the wave the absence left the
        // champion standing on. Never their sum — they are two answers to the same question.
        _descent.StartRun(build, hunter, _enemyBaseHealth, _enemyBaseDamage, Math.Max(StartWave, FreeStartWave));
        // A CHECKPOINT START. The region's chosen start wave (Map screen) skips the waves already
        // cleared, and the Memory Dust it costs is charged through the host (CheckpointCharge) — the
        // host fed StartWave = 0 when the Dust was not there, so a start here is always affordable.
        // THE DUST IS CHARGED FOR THE CHECKPOINT ALONE. A free resume (FreeStartWave) opens the run
        // deeper and costs nothing: the waves under it were fought, in real elapsed absence, already
        // paid out at the camp's 30-60% rate. The player has been charged once; the depth is the
        // receipt, not a second purchase.
        if (StartWave > 0) CheckpointCharge += Checkpoints.DustCost(StartWave);
        _mode = Mode.Fighting;
        BeginWave();
    }

    private void BeginWave()
    {
        if (_run is null || _champ is null) return;

        // A committed clip belongs to the wave it was swung in. StartRun (a death, a region change)
        // reaches here with the playhead back at zero, and a clip left standing from the last wave
        // held the figure on its first frame for the whole opening wave (review 2026-08-25).
        _clipName = null;
        _clipTiming = null;
        _performance = null;
        _outgoing = null;

        // THE WAVE BEING SHOWN, captured BEFORE the push: PushWave resolves wave+1 and, on a clear, counts
        // it — so after it `_run.Wave` is already the replayed wave, and `_run.Wave + 1` (which the boss
        // flag and the header used) named the NEXT wave. The boss drew one wave early and the boss wave
        // itself drew ordinary creatures; the header said WAVE 2 over the WAVE 1 CLEARED banner.
        // THE BUILD YOU HAVE NOW. A weave, a socket, a mastery or dust buy, a character switch — any of
        // them re-composes the build for the wave about to be fought (SoloExpedition.ReplaceBuild keeps
        // the run itself). The UI drew the live loadout all along; the sim fought the wave-one snapshot.
        if (_hunter is { } h && BuildStamp() != _buildStamp)
            _run.ReplaceBuild(ComposeBuild(h));
        // Gear is not in the build stamp (the sim reads worn mods live), so the pool is refreshed every
        // wave regardless: a ring put on mid-descent changes the health number at the next wave, not at
        // the next death (playtest 2026-08-25).
        _run.RefreshPool();
        if (_hunter is { } rh)
        {
            var cb = ComposeBuild(rh);
            _castRate = cb.Resolve(rh).SkillRate * cb.Shape.SkillRate;
            _beatMs = SoloBattle.BeatFor(_castRate);
            // WHICHEVER PASSIVE FIELD IS WOVEN, not always the Aura. After the slot rework a passive
            // can be any style's — a spilled STRIKE is PRESS, a spilled TRANSFORMATION is WILT — and
            // hunting for Form.Aura meant every one of them drew nothing at all.
            FlushAuraTotal();   // the wave's last tick still owes its number
            _auraTotal = 0; _auraTotalMs = -1;
            _auraSincePulse = 999f;
            // NOT _vfx.Clear(). Every effect fades out inside a second on its own, and clearing them at
            // the boundary erased the aura's ring mid-flight twice a cycle (review 2026-08-30). The
            // frozen ring that made me add this was the drawn-ring bug, which is gone.
        }
        // The replay's health table is read AFTER the pool refresh, or the HUD prints last wave's pool
        // over this wave's bar for a whole wave (review 2026-08-25: "300/360 with a full bar").
        var startHealth = new Dictionary<int, int> { [0] = _champ.Health };
        var maxHealth = new Dictionary<int, int> { [0] = _champ.MaxHealth };
        _replayWave = _run.Wave + 1;
        _outcome = _descent.PushWave();

        // The boss's arrival horn. Played when the boss wave actually BEGINS rather than at the
        // INCOMING banner, so it lands as the creature walks in — and so a reload straight into a
        // boss wave still announces it.
        if (WaveScaling.IsBossWave(_replayWave, ExpeditionTuning.Default)) Sound?.Play("sfx_boss", 0.58f);

        // THE WAVE'S ACTUAL TOTAL, summed from the creatures the sim just built — not
        // `_enemyBaseHealth * EnemyScale(wave + 1)`, which is what this used to be. That was a
        // PRE-COMPOSITION estimate: computed before PushWave, it could not know the archetype's health
        // multiplier, could not know PLATED had thickened anything, and could not know NUMBERS had added
        // a creature. The bar the player watches is the only readout of how a wave is going, and on
        // exactly the waves that differ from the average it was measuring against a number the
        // simulation never used — draining to empty with creatures still standing, or stalling above
        // zero on a wave already won.
        var enemyHp = _run.LastWaveCreatures.Sum(c => c.MaxHealth);

        // WHERE EACH SKILL STOOD WHEN THAT WAVE ENDED, banked before the replay carrying it is dropped.
        // The sim's cooldowns cross this boundary; the rail's readout only does because of this line.
        FoldSkillCarry();

        // THE WAVE'S OWN SKILLS, slot for slot — what every event's Slot indexes into. From the run,
        // not the loadout: the composer may have skipped an unlearned pick, and after a mid-descent
        // edit the loadout and the fight differ for a whole wave.
        _waveSkills = _run.Skills;
        // WHICHEVER PASSIVE FIELD THE FIGHT ACTUALLY RUNS decides the held-field art and colour.
        var fieldSk = _waveSkills.FirstOrDefault(k => k.Def.Kind == SkillKind.Field);
        _auraFxKey = fieldSk?.Def.FxKey;
        _auraColour = fieldSk is null ? null : SourceColor.GetValueOrDefault(fieldSk.Source, Bone);

        WavesBegun++;
        _replay = new WaveReplay(_run.LastWaveEvents, startHealth, maxHealth, enemyHp);
        _waveStartHealth = startHealth[0];   // for a posed seek's rebuild (DevSeek) — see UpdateFight
        _waveEnemyHp = enemyHp;
        _replayEndMs = _run.LastWaveEvents.Count == 0 ? 0f : _run.LastWaveEvents.Max(e => e.AtMs);
        _diedAt.Clear();          // the previous wave's fallen are gone with its replay
        _actors.Clear();
        // Hand the replay the composition so each creature drains its own bar and vanishes on its own
        // beat. Without this a wave of five reads as one bar going down, which hides the single most
        // useful fact in a Swarm band: how many of them you actually got through.
        _replay.SetComposition(_run.LastWaveCreatures, _run.LastWaveIntervalMs);   // with base damage and defence, for the enemy inspector
        _playheadMs = 0f;
        _chargeNow = 0;   // the sim's pool is per-wave; the pill must not carry one over
        _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(0f);
        _nextChampStrikeMs = _replay.NextChampionStrikeAfter(0f);
        _callouts.Clear();

        // ── THE WAVE ARRIVES. Enemies slide in from off-stage and fade up over ~0.8 s, and the replay
        //    is held until they have landed (UpdateFight's gate on _enemyEnter) so the champion never
        //    swings at empty air.
        //
        //    THIS USED TO LIVE AT ONE CALL SITE — the between-waves break, and nowhere else. StartRun
        //    reaches BeginWave too, on first launch, on a region change, and after EVERY death, and
        //    none of those got an arrival: the creatures were simply THERE, at rest, at full opacity,
        //    on frame one. Playtest 2026-09-09: "enemies spawn instantly when we start; they don't
        //    walk in." The mechanism was built, correct and tested-adjacent, and never reached the two
        //    moments the player actually named. Setting it here means every wave open arrives the same
        //    way, whatever opened it.
        //
        //    A capture must not inherit it: a fixture seeks to an instant inside a fight, and a 0.8 s
        //    gate in front of the playhead would freeze every fight* shot on frame zero of an entrance.
        //    DevStart clears it (see below).
        _enemyEnter = 1f;

        // ── THE READINESS EDGES ARE RE-SEEDED, NOT WIPED. The edge detector in Update fires on a
        //    FALSE -> TRUE crossing and reads the stored value with GetValueOrDefault, so an empty map
        //    reads as "every slot was waiting" — and a wave that opens with three skills already ready
        //    (which is most of them, since the carry rolls a short wave's wait forward) then popped all
        //    three medallions and chimed for each on its first frame. That is a ready-pop for a cooldown
        //    that did not run, at every single wave boundary: exactly what clearing the map here was
        //    written to prevent, caused by the clear itself. Seeded from what is true at the wave's
        //    opening instant instead, so only a slot that actually comes up during the wave pops.
        for (var slot = 0; slot < _waveSkills.Count; slot++)
        {
            var def = _waveSkills[slot].Def;
            var t0 = Timing(slot, def);
            _railReady[slot] = def.TakesABeat && t0.Swept >= 1f;
            // ...and the notch it opens on, for the same reason: an unseeded map reads as notch zero,
            // and every ring would tick on the wave's first frame.
            _railStep[slot] = t0.RingSteps > 0 ? (int)MathF.Floor(t0.Swept * t0.RingSteps + 0.001f) : 0;
        }
    }

    /// <summary>The host rolled a chest for the boss just felled — upgrade the banner to the reward beat.</summary>
    /// <remarks>Called same-frame as the boss-down banner, so "CHEST DROPPED!" replaces "BOSS DOWN!" cleanly.</remarks>
    public void FlashChest()
    {
        // THE VAULT, not the Forge. Chests moved to the VAULT screen when it was carved off the Forge, and
        // this line kept sending players to the FORGE tab to open one (release polish 2026-09-05). No key
        // hint either: the doors name no keys in their labels — the tour and the help sheet teach the
        // keys (UX guide §14) — and the VAULT TILE on the rail, which is wearing a fresh count badge as
        // this banner is posted, is the click this line points at. (The right column had a second door
        // labelled OPEN VAULT; it was removed with the authored opening — one room, one door.)
        _bannerText = "BOSS DOWN — A CHEST IS WAITING IN THE VAULT";
        _bannerTimer = 2.4f;
    }

    /// <summary>A charter dropped — say so on its own, louder than a material.</summary>
    /// <remarks>
    /// REPLACES the banner rather than appending, unlike FlashSpoil. A charter is one wave in forty and
    /// a whole Forge operation; sharing a line with "+1 ESSENCE" would bury the rarer of the two.
    /// </remarks>
    public void FlashCharter(string name)
    {
        _bannerText = $"{name} FOUND  —  SPEND IT IN THE FORGE";
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
                Spoil = true,   // the clear is not SHOWN while this is still rising (ClearShown)
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
    /// <param name="px">
    /// Its size, or 0 for the lane's own <see cref="SayPx"/>. §63 ranks the fight's moments — normal
    /// damage &lt; critical &lt; major skill impact &lt; Break / Shield Break / major state — and size is
    /// the loudest channel a callout has, so the top of that ladder is allowed off the default rung.
    /// </param>
    /// <param name="life">How long it holds before it starts to go; a crit lingers 1.3.</param>
    private void Say(string text, Color color, int px = 0, float life = 1f, int lift = 0)
    {
        if (PresentTrace.Enabled) PresentTrace.Log("callout", text);
        _callouts.Add(new Callout
        {
            Text = text,
            Color = color,
            X = ChampBox.Center.X,
            Y = ChampBox.Y - 40 - lift - StackSlot(CalloutLane.Champion) * CalloutLineHeight,
            Life = life,
            Px = px > 0 ? px : SayPx,
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

    /// <summary>
    /// The WORDS of a damage callout: the number, what its grade is called, and the multi-hit fold.
    /// </summary>
    /// <remarks>
    /// Public and pure so the wording is testable without a GraphicsDevice — the Game test project can
    /// only reach statics, and "what does a Reaction's blow say" is a rule worth pinning rather than
    /// re-reading off a screenshot. Three rules in one line:
    /// <list type="bullet">
    /// <item>a plain blow is its number and nothing else;</item>
    /// <item>a REACTION's blow is captioned with the skill's own name ("-4 JAWS") — a Reaction answers
    /// every bite, so it is drawn a size up on every one of them, and calling that CRITICAL taught the
    /// player that a critical is the ordinary case while leaving the skill that actually fired
    /// unnamed;</item>
    /// <item>a CRITICAL hit says CRITICAL. Until 2026-09-09 this word was unreachable: the screen's
    /// <c>crit</c> flag meant "a Reaction produced it" and its true branch always replaced the word
    /// with the Reaction's name, so the one caption the game had for a critical hit could never be
    /// drawn — and the sim rolled none to draw it for. Both halves are fixed; the grade now comes off
    /// <see cref="IdleXIdle.Core.Expeditions.BattleEvent.Crit"/>, and a Reaction that also crits says
    /// both;</item>
    /// <item>a cast that landed more than once folds into one number with its count ("-635 ×5", §21).
    /// Criticals fold with criticals only, so a mixed instant prints two honest numbers rather than
    /// one that grades the whole sum by its luckiest hit.</item>
    /// </list>
    /// </remarks>
    public static string DamageCalloutText(int total, bool crit, int hits, string? critWord = null)
    {
        var text = $"-{total:N0}";
        if (critWord is { Length: > 0 }) text += " " + critWord;
        if (crit) text += " CRITICAL";
        return text + (hits > 1 ? $" ×{hits}" : "");
    }

    /// <summary>
    /// A floating combat number over the creature a Strike event hit — THE EVENT'S OWN AMOUNT, which is
    /// what its bar just lost.
    /// </summary>
    /// <remarks>
    /// <b>THE NUMBER WAS INVENTED.</b> Until 2026-08-26 this was fed by <c>HitDamage()</c>: the hunter's
    /// PowerRating times a multiplier (1 for a swing, 2 for a cast, 3 for a Trap) times a jitter — a
    /// figure that no part of the simulation ever produced. The bars, meanwhile, follow the replay, which
    /// follows the sim to the point (WaveReplayTests: each creature replays to exactly where the sim left
    /// it). So a wave-one auto-attack of 6 printed "-185" over four full pips, and the playtest read it
    /// as "the enemy bars don't drop correctly — maybe a bug in how damage is applied". There was no bug in
    /// the damage; the number was lying about it. It now prints <see cref="BattleEvent.Amount"/>, and it
    /// prints it over the creature in <see cref="BattleEvent.Slot"/>, so a number and the bar under it
    /// always describe the same blow.
    /// </remarks>
    /// <param name="slot">The creature struck — the column the number rises from.</param>
    /// <param name="crit">The crit GRADE: gold, larger, and it lingers.</param>
    /// <param name="skill">A cast's hit, drawn a size up from the auto-swing's.</param>
    /// <param name="critWord">
    /// What the graded blow is CALLED — see <see cref="DamageCalloutText"/>. Null means CRITICAL; a
    /// Reaction passes its own skill's name instead ("-4 JAWS").
    /// </param>
    private void SpawnDamage(int amount, int slot, bool crit, bool skill, int hits = 1, string? critWord = null)
    {
        if (!ShowDamageNumbers) return;   // settings: DAMAGE NUMBERS off
        if (PresentTrace.Enabled) PresentTrace.Log("number", $"slot={slot}\tamount={amount}\thits={hits}\tcrit={crit}\tskill={skill}");
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
            // The count rides on the number rather than replacing it: "-635 ×5" says both what the
            // creature lost and that one cast did it.
            Text = DamageCalloutText(total: amount, crit: crit, hits: hits, critWord: critWord),
            Color = crit ? Gold : skill ? UiKit.Vellum : Bone,
            // Over the creature it struck, not the row's centre: in a swarm the row centre is the gap
            // between two creatures, and a number there names neither of them.
            X = CreatureCentreX(slot),
            Y = EnemyCalloutBase - StackSlot(CalloutLane.Enemy) * CalloutLineHeight,
            Life = crit ? 1.3f : 1f,
            // Was 52 and 72. The fight "reads loud" was the standing playtest note, and a damage number
            // two-thirds the height of the creature it is describing is most of why.
            Px = crit ? CritPx : skill ? SkillHitPx : DamagePx,
            Lane = CalloutLane.Enemy,
        });
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
        // THE FIELD DOES NOT STOP BETWEEN WAVES. The pulse used to be raised only inside the fighting
        // window, and with a 2.2-second wave and a 2.05-second transition (measured 2026-08-30) that left
        // the aura off screen for nearly half the cycle — which is why it read as absent.
        PulseAuraOnClock(dt);
        HoldAura();   // re-asked every frame; VfxPlayer drops it the moment we stop
        HoldShieldBarrier();

        if (_breakTimer > 0f)
        {
            _enemyWindup = 0f;
            // THE LAST BLOW PLAYS OUT (ADR-011). A clip or a performance still running when the wave is cleared
            // finishes on the break's own clock: the replay is over, so no fight event reads the playhead any
            // more. It used to be dropped on the spot — harmless for a throw, but a melee lunge that landed the
            // wave's last kill snapped 800 px home in one frame. Nothing survives the next wave (BeginWave).
            if (ActionStillPlaying())
            {
                _playheadMs += dt * 1000f * _speedMul;
                PresentTrace.PlayheadMs = _playheadMs;
                UpdatePerformance(dt);
                if (_clipName is not null && _clipTiming is { } playing && _playheadMs >= _clipStartMs + playing.TotalMs)
                {
                    if (PresentTrace.Enabled) PresentTrace.Log("clip-end", _clipName);
                    _clipName = null;
                    _clipTiming = null;
                    _idleFrom = _anim;
                }
            }
            else
            {
                _clipName = null;   // a clip never survives the wave it was swung in
                _clipTiming = null;
                _performance = null;
                _outgoing = null;
            }
            // ...AND THE HOST MAY KEEP THE NEXT PACK OFF THE STAGE (HoldNextWave): the break plays every
            // beat of its own, then rests on the empty stage until it is let go.
            if (ClearBeat.Tick(ref _breakTimer, dt, HoldNextWave)) BeginWave();   // ...which arms the arrival itself now
            return;
        }

        // Hold the fight until the new enemy has finished sliding in — otherwise the champion swings at empty
        // air while the enemy is still off to the right ("hunter hits before the enemy arrives").
        if (_enemyEnter > 0f) { _enemyWindup = 0f; _clipName = null; return; }

        // DEV: a seek aimed at an EVENT (DevSeekBefore) resolves against this run's wave — and under
        // the rig it waits for the frame before the shutter, so the event is crossed LIVE on the frame
        // that is photographed: callout fresh, effect on its first frame. Applied any earlier, the
        // wall-clock fades had already taken the callout by the time the shot was saved.
        if (_devSeekPick is { } aim && _run is not null
            && (!Game1.RigActive || Game1.ShotFrameNow >= Game1.ShotAtFrame - 2))
        {
            _devSeekPick = null;
            foreach (var e in _run.LastWaveEvents)
                if (aim.Pick(e)) { _devSeekMs = Math.Max(0f, e.AtMs - aim.Lead * 1000f); break; }
        }
        // DEV: the fixture's seek (DevSeek / DevSeekBefore) — the beats before it land silently, health
        // only. THE REPLAY IS REBUILT from the wave's events first: a WaveReplay cannot rewind, and under
        // the rig the playhead has usually run past the target by the time the seek applies (the run
        // restarts once on the host's Source push, and a posed seek waits for the shutter). Seeking a
        // replay whose cursor was already past the target crossed nothing — the shot showed callouts
        // that had faded a second earlier and a dump that swore the playhead was at the target.
        if (_devSeekMs is { } seek && _run is not null && _champ is not null)
        {
            _devSeekMs = null;
            _replay = new WaveReplay(_run.LastWaveEvents,
                new Dictionary<int, int> { [0] = _waveStartHealth },
                new Dictionary<int, int> { [0] = _champ.MaxHealth }, _waveEnemyHp);
            _replay.SetComposition(_run.LastWaveCreatures, _run.LastWaveIntervalMs);   // with base damage and defence, for the enemy inspector
            _diedAt.Clear();
            _actors.Clear();
            _callouts.Clear();      // the pose shows THIS instant, not the second before it
            _vfx.Clear();
            _shieldFxPosed = false; // ...so a posed shield flare is re-fired after the rebuild wiped it
            _hitFlash.Clear();
            _playheadMs = seek;
            foreach (var crossed in _replay.Advance(seek))
                if (crossed.Kind is BattleEventKind.ShieldGained or BattleEventKind.ShieldAbsorbed)
                    _shieldSeen = true;   // the strip must show a shield the seek granted silently
            _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(seek);
            _nextChampStrikeMs = _replay.NextChampionStrikeAfter(seek);
        }

        // ── THE BARRIER. ────────────────────────────────────────────────────────────────────────
        //
        // An authored step can ask the replay to stop short of the next beat of a given kind, so the
        // player is told what is about to happen BEFORE it happens — and short of that beat's own
        // WIND-UP, not only of its millisecond (ReplayBarrier). A cast's clip starts one contact-length
        // ahead of its event, so the old park one millisecond short froze the Hunter mid-cast under
        // the very card that was meant to come first (playtest 2026-09-11). Lifting the barrier lets
        // the wind-up begin, and the SAME event crosses on its contact frame with callout and effect.
        //
        // A CLAMP, NOT AN EARLY RETURN. The two holds above this line return, and each has to
        // hand-clear _enemyWindup and _clipName because of it. Clamping leaves the windup, the
        // champion's clip and every rail readout frozen CONSISTENTLY at the held instant: a creature
        // caught mid-swing stays mid-swing instead of snapping back to idle.
        var free = _playheadMs + dt * 1000f * _speedMul;
        var hold = ReplayBarrier.Advance(_replay, _playheadMs, free, HoldBeforeKind, _leadMs ??= PresentationLeadMs);
        // THE HOLD LET GO. The host clears the kind on the frame the card is answered; the beat that was
        // being held is remembered, so the host can wait for it to PLAY and not merely to cross.
        if (HoldBeforeKind is null && _heldAtMs is { } letGo)
        {
            _releasedAtMs = letGo;
            _releasedCrossed = false;
            _releasedPlayed = false;
            _releasedFxFrom = _releasedFxTo = 0;
            _heldAtMs = null;
        }
        if (hold.HeldAtMs is { } heldAt) { _heldAtMs = heldAt; _releasedAtMs = null; _releasedPlayed = false; }
        ReplayHeld = hold.Held;
        HeldEventAtMs = hold.HeldAtMs;
        _playheadMs = hold.PlayheadMs;
        PresentTrace.PlayheadMs = _playheadMs;

        // ── THE READY CROSSING, ARMED HERE AND NOWHERE ELSE. A pulse belongs to Update: a Draw that
        //    armed one would re-arm it every frame and the medallion would swell for as long as you
        //    looked at it (the rail's own standing rule, and the reason the cast pulse lives in the
        //    event pump). Each slot's readiness is read once per frame and only a FALSE -> TRUE edge
        //    fires — so a skill that stays ready between waves pops once, at the moment it came up.
        for (var slot = 0; slot < _waveSkills.Count; slot++)
        {
            var def = _waveSkills[slot].Def;
            if (!def.TakesABeat) continue;             // a passive is always ready; it never crosses
            var timing = Timing(slot, def);
            var nowReady = timing.Swept >= 1f;

            // ── EVERY NOTCH ANNOUNCES ITSELF. ────────────────────────────────────────────────────
            //
            // A beat-counted ring is quantised to one notch per action, so between notches it shows
            // nothing at all; a six-beat skill was three silent jumps across four and a half seconds.
            // Crossing a notch now arms a tick, and the ring draws it as a bright spoke that fades —
            // so the wait reads as a countdown rather than as a stalled dial. Armed HERE, in Update,
            // for the rail's standing rule: a Draw that armed a pulse would re-arm it every frame.
            var step = timing.RingSteps > 0
                ? (int)MathF.Floor(timing.Swept * timing.RingSteps + 0.001f)
                : 0;
            if (timing.RingSteps > 0 && step > _railStep.GetValueOrDefault(slot) && step < timing.RingSteps)
                UiMotion.Flash(SkillStepKey(slot), UiMotion.Transition);
            _railStep[slot] = nowReady ? 0 : step;

            if (nowReady && !_railReady.GetValueOrDefault(slot))
            {
                UiMotion.Flash(SkillReadyKey(slot), UiMotion.Transition);
                // AND THE MOMENT ITSELF GETS A SHAPE. The pop was a tenth of a swell over 180 ms, which
                // is the smallest reading of "the cooldown animation is not satisfying" (playtest
                // 2026-09-09) and not the one that was asked for. A ring now leaves the medallion and
                // fades over the REWARD beat — the same length the game uses for a haul landing —
                // because a skill coming up IS the fight's reward beat.
                UiMotion.Flash(SkillBurstKey(slot), UiMotion.Reward);
                // A CUE, but a quiet and a rare one. The screen already plays a thud every beat and a
                // cast every action; four Actives coming up every few beats would be the disco-ball
                // note again, one modality over. Only a skill with a real wait (two notches or more)
                // is worth announcing, and it is announced under the cast's own volume.
                if (def.Beats > 2) Sound?.Play("sfx_trait_lit", 0.16f, vary: 0.05f);
            }
            _railReady[slot] = nowReady;
        }

        // THE AURA'S PULSE, ON THE WALL CLOCK. It used to be raised by an aura's damage event, which
        // meant a tick landing on a cast's own millisecond was read as that cast's blow and raised
        // nothing — and in a real four-skill build that happened often enough that the field looked
        // absent (playtest 2026-08-30: "there is no aura effect on screen"). An always-on field is
        // always on: while a wave is running and the build carries an Aura, it pulses on its own beat.

        // The aura's tick prints ONE number, and it prints on time: it used to wait for the NEXT tick's
        // events to arrive, which put it 500 ms late over whatever creature was alive by then.
        if (_auraTotal > 0 && _playheadMs > _auraTotalMs + 1_000 * 0.5f) FlushAuraTotal();
        if (_hitFlash.Count > 0)
            foreach (var key in _hitFlash.Keys.ToList())
            {
                var left = _hitFlash[key] - dt * FlashLook(key).Rate;   // ~200 ms of life (a recipe's own); FlashAt shapes it
                if (left <= 0f) _hitFlash.Remove(key); else _hitFlash[key] = left;
            }
        // The skill tiles' cast pulses used to decay here, on a private 0.42 s clock. They are
        // UiMotion one-shots now (see SkillCastKey): the host ticks them, they collapse with the rest
        // of the game's motion, and there is one fewer timer in this file that a Draw could re-arm.

        // THE WINDOW, NOT THE FPS, IS WHAT MAKES THIS SWING LEGIBLE — and getting that wrong is easy.
        // EnemyClipSeconds maps the windup 0..1 onto the clip's FULL length, so the clip always completes
        // across this window whatever its fps: lowering frames-per-second changes the idle loop and
        // nothing at all about the attack. Widened from 600ms to 900ms, near the champion's own swing
        // (StrikeSeconds = 8 frames / 8fps = 1.0s), so the two actors wind up at comparable speeds and a
        // player can see a creature commit before it lands.
        const float windupMs = 900f;
        var lead = _nextEnemyStrikeMs - _playheadMs;
        var windupWas = _enemyWindup;
        _enemyWindup = lead > 0f && lead < windupMs ? 1f - lead / windupMs : 0f;
        // the row's ANTICIPATION, for the reaction timeline (tools/asset-pipeline/reaction_timeline.py): the moment
        // the creatures start winding up toward the next bite, whose contact frame is the bite itself
        if (PresentTrace.Enabled && windupWas <= 0f && _enemyWindup > 0f)
            PresentTrace.Log("enemy-windup", $"bite={_nextEnemyStrikeMs:0}\tfollowing={_enemySinceHit < EnemyFollowSeconds}");

        // The champion's clip: one committed swing at a time, aimed at the next beat.
        UpdateChampionClip();

        UpdatePerformance(dt);
        var batch = _replay.Advance(_playheadMs);
        // Which blows in THIS batch have already been folded into another's number. A field, not a
        // local: this runs every frame and §93 forbids a per-frame allocation.
        _summed.Clear();
        // The beat a cast lands on. A Skill event precedes the Strike events its hit produces, at the
        // same timestamp, so a Strike stamped with the cast's beat is that cast's blow — graded a size
        // up, and gold when the cast was a Trap. Anything else at another beat is the auto-swing.
        var skillAtMs = -1;
        var trapAtMs = -1;
        var auraAtMs = -1;
        // ...and WHAT the reaction at that beat is called, so its blow can print its own name instead of
        // the word CRITICAL (§63: a critical is the expected value there; the skill is the news).
        string? trapName = null;
        for (var bi = 0; bi < batch.Count; bi++)
        {
            var e = batch[bi];
            if (PresentTrace.Enabled) PresentTrace.Log("event", $"{e.Kind}\tslot={e.Slot}\tamount={e.Amount}\tat={e.AtMs}\tcrit={e.Crit}\tskill={e.FromSkill}");
            // EVERY EFFECT USED THE SAME DEFAULT SIZE, so a glancing blow, a critical and a death all
            // burst at 208px — on a 430px champion and on swarm creatures barely 100px across. Playtest:
            // "HUNT ekranında efektlerin boyutları düzgün değil." Size is the loudest channel an effect
            // has, and spending all of it on "something happened" leaves nothing for "something big
            // happened". They are graded now: a weak hit is 1, an ordinary hit 1, an interrupt or a
            // level-up 2, a crit, a Ruin strike or a death 3. The champion's own hit also drops 40px to
            // the torso, because centred on ChampBox it burst over the head.
            switch (e.Kind)
            {
                case BattleEventKind.Aura:
                    auraAtMs = e.AtMs;   // the blows at this instant are the field's, not a cast's
                    break;

                case BattleEventKind.Strike:
                {
                    // AN AURA TICK is a skill's blow with no cast behind it (Aura is passive — it emits no
                    // Skill event) and no Trap bite either. It gets its own picture: a pulse of the
                    // Source's colour from the champion, once per tick, and NO lunge, sound or puff —
                    // the champion "kept trying to start an animation" every half second (playtest
                    // 2026-08-28: "an always-on effect that never touches the character's animation").
                    // The PICTURE is on its own clock (see PulseAura, driven from Update): a field that is
                    // always on must not depend on an event surviving a classification. This flag only
                    // decides whether the blow lunges, sounds, sparks and flashes.
                    // AN AURA TICK SAYS SO. The sim emits a BattleEventKind.Aura before the tick's blows
                    // (2026-08-30); before that the screen had to guess from the timestamp, and at some
                    // action speeds a fifth of the ticks shared a cast's millisecond and were reported as
                    // that cast's — the whole pack flashed, four damage numbers printed as skill hits and
                    // four hit sounds fired at once.
                    var auraTick = e.FromSkill && e.AtMs == auraAtMs;
                    // IMPACT PRIORITY (ADR-011): a blow the champion PERFORMED has its own contact — the blade's
                    // directional impact and its contact sound — so the generic puff and thud, which describe
                    // the same physical event, give way. The flash and the number stay: they are the enemy's.
                    var performedHit = !auraTick && e.FromSkill && _performance is { } performer
                                       && performer.IsBeat(e.AtMs) && performer.Recipe.ReplacesGenericHit;
                    // The swing lunges; a cast already has its clip (UpdateChampionClip aims it at the beat).
                    if (!e.FromSkill) _champLunge = 1f;
                    _nextChampStrikeMs = _replay.NextChampionStrikeAfter(e.AtMs);
                    // The swing's thud at full weight; a skill's landing blows quieter — the cast's breath
                    // already announced them, and four projectile impacts on top of it were "two sounds at
                    // once" (playtest 2026-08-26). An aura tick is silent: it hums, it does not strike.
                    if (!auraTick && !performedHit) Sound?.Play("sfx_hit", e.FromSkill ? 0.22f : 0.38f, vary: 0.06f);
                    // The number is the blow: the event's amount, over the creature that took it.
                    // Graded by PROVENANCE (the event says whether a skill dealt it) and only then by beat:
                    // an auto-swing on a cast's own millisecond stays plain.
                    // An aura tick's blows are ONE number — the tick's total over the pack — not four
                    // "-9"s drifting up every half second; the previous tick's total is flushed when the
                    // next tick starts (or at wave end, in BeginWave).
                    if (auraTick)
                    {
                        if (e.AtMs != _auraTotalMs) { FlushAuraTotal(); _auraTotalMs = e.AtMs; }
                        _auraTotal += e.Amount;
                    }
                    else if (e.Amount > 0)
                    {
                        // ONE CREATURE, ONE INSTANT, ONE NUMBER (brief §21). A multi-hit cast emits a
                        // Strike per hit at the SAME millisecond, so five hits on one creature printed
                        // five numbers up one column and the player read a flurry instead of a total.
                        // They are summed here and drawn as "-635 ×5" — the total is what the fight did,
                        // and the count is what makes it legible as one cast rather than one hit.
                        // THE GRADE AND THE CAPTION ARE TWO QUESTIONS. `crit` is the fight's own answer
                        // — the roll, carried on the event — and `reaction` is which skill produced the
                        // blow. They used to be one boolean whose true branch deleted the word CRITICAL,
                        // which is why the game could not draw a critical hit even once it had one.
                        var crit = e.Crit;
                        var reaction = e.FromSkill && e.AtMs == trapAtMs;
                        var skill = e.FromSkill && e.AtMs == skillAtMs;
                        var hits = 1;
                        var total = e.Amount;
                        for (var kj = bi + 1; kj < batch.Count; kj++)
                        {
                            var o = batch[kj];
                            if (o.AtMs != e.AtMs) break;              // the batch is in time order
                            if (o.Kind != BattleEventKind.Strike || o.Slot != e.Slot || o.Amount <= 0) continue;
                            if (o.FromSkill != e.FromSkill) continue; // a swing and a cast stay separate
                            if (o.Crit != e.Crit) continue;           // ...and so do a critical and a plain hit
                            total += o.Amount;
                            hits++;
                            _summed.Add(kj);        // its own turn still flashes and sounds; it draws no number
                        }
                        if (!_summed.Contains(bi))
                        {
                            SpawnDamage(total, e.Slot, crit, skill, hits, reaction ? trapName : null);
                            // THE CRITICAL'S OWN CUE. sfx_crit existed and was spent on Reactions, so the
                            // sound the file is named for had never once accompanied a critical hit.
                            if (crit) Sound?.Play("sfx_crit", 0.46f, vary: 0.06f);
                        }
                    }
                    // The creature that took it FLASHES — but ONLY for a real blow, and only once its last
                    // flash has finished. An aura ticks twice a second and the swing lands every beat, and
                    // together they strobed the pack ("the enemy blinks like a disco ball", playtest
                    // 2026-08-30); an always-on field has its own picture (the pulse) and needs no flash.
                    if (!auraTick && _hitFlash.GetValueOrDefault(e.Slot) <= 0f)
                    {
                        _hitFlash[e.Slot] = 1f;
                        _hitFlashLook[e.Slot] = performedHit
                            ? (_performance!.Recipe.TargetFlash, _performance.Recipe.TargetFlashRise, 1000f / Math.Max(1f, _performance.Recipe.TargetFlashMs))
                            : UsualFlash;
                        if (PresentTrace.Enabled) PresentTrace.Log("flash", $"slot={e.Slot}");
                    }
                    if (!auraTick && !performedHit && (_strikeCount++ & 1) == 0)   // every other blow: a small, quiet puff
                    {
                        PlayFx(VfxProfiles.ImpactWeak, VfxSubject.Creature(e.Slot), Steel);
                    }
                    break;
                }
                case BattleEventKind.EnemyStrike:
                    _enemyLunge = 1f;
                    _enemySinceHit = 0f;
                    _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(e.AtMs);
                    Sound?.Play("sfx_hit", 0.30f, pitch: -0.25f, vary: 0.06f);   // same thud pitched down: taking, not giving
                    // ONE FOCAL POINT (ADR-011): while the champion is performing an action, an ordinary bite on
                    // him is drawn quieter — it still lands and still shows its number, but it does not put a red
                    // burst on the throwing arm at the moment the eye should be on the hand and the blades.
                    PlayFx(VfxProfiles.ImpactBite, VfxSubject.Champion,
                           _performance is { } acting && !acting.ClipOver(_playheadMs) ? Ember * 0.55f : Ember);
                    HunterHit(e);   // the Health the hunter lost, as a number — nothing for a shielded or prevented bite
                    break;
                case BattleEventKind.Skill:
                {
                    SkillCastsSeen++;
                    LastSkillCastAtMs = e.AtMs;
                    // THE SLOT IS THE IDENTITY. The event names which equipped skill acted; name,
                    // art, colour and kind all come from the skill itself — the old payload was a
                    // (Source, Form) ordinal pair, and two skills of one style collided on it.
                    if (e.Slot < 0 || e.Slot >= _waveSkills.Count) break;
                    // THE RELEASED CAST'S OWN EFFECTS are the ones launched from here to the end of this
                    // case — remembered so the host can wait for exactly those to finish (ReleasedBeatPlayed).
                    var fxBefore = _vfx.Launched;
                    var released = _releasedAtMs == e.AtMs;
                    var castSk = _waveSkills[e.Slot];
                    var castDef = castSk.Def;
                    // ONE PULSE, ON THE CAST (§30, and the strip's own rule: nothing on it may flash on
                    // its own). Keyed to the event's slot, armed here and nowhere else — a Draw that
                    // re-armed it would be a tile that blinks for as long as you look at it.
                    UiMotion.Flash(SkillCastKey(e.Slot), UiMotion.Transition);
                    var (text, colour) = CalloutFor(castDef.Style);
                    // A PERFORMED cast (ADR-011) was announced at its release, launched from the hand and voiced
                    // there; at the beat — its contact — the fight's own hits carry it.
                    var performed = _performance is { } perf && perf.SkillSlot == e.Slot && perf.IsBeat(e.AtMs);
                    if (ShowSkillCallouts && !(performed && _performance!.Recipe.CalloutAtRelease)) Say(text, colour);   // settings: SKILL NAMES hides exactly this
                    // The creature this cast HITS is the one its own Strike in the same batch names — the
                    // batch has already applied the kill, so "first alive" would point past a creature the
                    // cast just killed and the flash would land on its neighbour.
                    int? castTarget = null;
                    for (var k = bi + 1; k < batch.Count && batch[k].AtMs <= e.AtMs + 1; k++)
                        if (batch[k].Kind == BattleEventKind.Strike) { castTarget = batch[k].Slot; break; }
                    if (!performed) PlaySkillVfx(castDef, castSk.Source, castTarget);
                    if (released) _releasedFxFrom = fxBefore;   // the rest of the range closes with the batch
                    if (!performed) Sound?.Play("sfx_cast", 0.42f, vary: 0.06f);
                    var isReaction = castDef.Kind == SkillKind.Reaction;
                    // A Reaction's answer is louder than a cast but is NOT a critical: sfx_crit belongs
                    // to the roll now (see the Strike case), and the answer keeps the cast's own thud
                    // pitched up, so the two events stay tellable apart by ear.
                    if (isReaction) Sound?.Play("sfx_hit", 0.40f, pitch: 0.25f, vary: 0.06f);
                    skillAtMs = e.AtMs;                          // the Strikes at this beat are this cast's
                    if (isReaction) { trapAtMs = e.AtMs; trapName = castDef.Name; }   // ...and a reaction's are graded up, under its OWN name
                    break;
                }
                case BattleEventKind.Heal:
                    if (ShowDamageNumbers) Say($"+{e.Amount}", Verdant);   // a number — follows DAMAGE NUMBERS; UNDYING below always shows
                    // The effect's FOOT sits on the ground line. That used to be a hand-tuned "- 156",
                    // which only held at one size and one strip: the constant is now the STANDING anchor,
                    // which puts the content's bottom edge on the champion's own visible sole whatever
                    // the art's padding is (playtest 2026-08-28: "the heal effect's ground part appears
                    // at the character's middle").
                    PlayFx(VfxProfiles.HealColumn, VfxSubject.Champion, Verdant);
                    break;
                case BattleEventKind.Undying:
                    Say("UNDYING", Gold);
                    PlayFx(VfxProfiles.ShieldUndying, VfxSubject.Champion, Gold);
                    break;
                case BattleEventKind.Down:
                    Sound?.Play("sfx_champ_down", 0.62f, vary: 0.03f);
                    PlayFx(VfxProfiles.DeathChampion, VfxSubject.Champion, Color.White);
                    break;
                case BattleEventKind.EnemyDown:
                {
                    // The creature FALLS (its death clip, from this moment), and the plume rises over the
                    // body half a second later — after the fall, not instead of it. Playtest: "düşman
                    // ölüyor ama önünde bir duman animasyonu çıkıyor, herkesin ölme animasyonu olması lazım."
                    _diedAt[e.Slot] = _anim;
                    EnemyDownsSeen++;
                    // A boss has its own fall (sfx_boss_down: deeper, longer, a second thump when the mass lands)
                    // rather than the creature's death pitched down — a pitched-down crumble is a slower crumble.
                    if (_isBossWave) Sound?.Play("sfx_boss_down", 0.46f, vary: 0.03f);
                    else Sound?.Play("sfx_enemy_down", 0.36f, vary: 0.06f);
                    // A boss falling is the loudest beat in the fight: the starburst AND the plume.
                    // The plume's half-second wait is the profile's DelaySeconds now, not the caller's.
                    if (_isBossWave) PlayFx(VfxProfiles.DeathBossBurst, VfxSubject.Creature(e.Slot), Gold);
                    PlayFx(VfxProfiles.DeathCreature, VfxSubject.Creature(e.Slot), Color.White);
                    break;
                }
                case BattleEventKind.Charge:
                    _chargeNow = e.Amount;   // the pool AFTER the change; 0 is REND's dump
                    break;

                // ── SHIELD. Three events, three different weights of feedback (§68–§70). ──────────
                //    Each has its own cue in the §86 vocabulary — the cold shimmer, its tick, its crack —
                //    and each is ONE SHOT, armed here on the event and never re-armed by a draw.
                case BattleEventKind.ShieldGained:
                    // GAIN IS THE ONE WORTH A NUMBER (§24). It is a thing the build DID, it is rare
                    // enough not to be spam, and the amount is the whole point of the rungs and skills
                    // that grant it. The wave-start grant arrives at 0 ms with the bar already drawn,
                    // so it is not said — nothing happened on screen for it to explain.
                    _shieldSeen = true;
                    if (e.AtMs > 0 && ShowDamageNumbers) Say($"+{e.Amount} SHIELD", Steel);
                    // The rim builds on the BAR, which is where the gain actually landed; the dome on
                    // the champion says the same thing in the arena. A transition, not a reward — a
                    // grant is a state change, and the run has many of them.
                    UiMotion.Flash(ShieldGainKey, UiMotion.Transition);
                    Sound?.Play("sfx_shield_gain", 0.40f, vary: 0.05f);
                    PlayFx(VfxProfiles.ShieldGain, VfxSubject.Champion, Steel);
                    break;

                case BattleEventKind.ShieldAbsorbed:
                    // ABSORPTION IS NOT A NUMBER. It happens on every bite a shielded champion takes,
                    // and a figure on each one would bury the health damage beside it — which is the
                    // number that actually matters. The bar falls, the barrier takes a small hit, and
                    // that is the whole of it. FAST, because it is the micro-feedback of a single bite;
                    // the cue is throttled at 60 ms by SoundBank so a swarm reads as busy, not as a wall.
                    _shieldSeen = true;
                    UiMotion.Flash(ShieldAbsorbKey, UiMotion.Fast);
                    Sound?.Play("sfx_shield_hit", 0.26f, vary: 0.07f);
                    PlayFx(VfxProfiles.ShieldAbsorb, VfxSubject.Champion, Steel);
                    break;

                case BattleEventKind.ShieldBroken:
                    // BREAKING IS LOUD, because from the next bite the player is paying in health — the
                    // loudest beat in the fight that is not a boss falling (§63's priority ladder puts
                    // Shield Break at the top with Break / Stun / Execute). It gets the REWARD length
                    // and its own art: fx_shield_break, a cracked shell bursting into shards, generated
                    // for exactly this moment because fx_shield is the barrier STANDING — a shell that
                    // is asked for every frame it exists. A held loop played backwards is not a break.
                    //
                    // The cue was sfx_champ_down at 0.30 — the champion's DEATH sample, quietened, for
                    // an event that is not a death. sfx_shield_break is the crack the §86 vocabulary
                    // shipped for it, and SoundBank holds it 300 ms clear of itself.
                    // AND IT OUTRANKS A CRITICAL, which is what §63's ladder asks for and what the
                    // callout could not say: it printed at the champion lane's 36 while a crit beside it
                    // printed at 46, so the loudest event on the screen was the smallest word on it. At
                    // the top of the damage ladder, and holding longer than a crit does.
                    // In Primary, LIFTED clear of the burst: in steel, on the burst's pale ring, the word
                    // that announces the state change could not be read at the instant it fired (huntstates-06).
                    Say("SHIELD BROKEN", Bone, UiTypography.DamageCritical, life: 1.6f, lift: ChampBox.Height * 15 / 100);
                    Sound?.Play("sfx_shield_break", 0.58f, vary: 0.03f);
                    PlayShieldBreak();
                    break;
            }
        }

        // FIXTURE DIAL ONLY (see ShotShieldFx) — inert without RH_SHOT_SHIELDFX. Inline rather than a
        // method call because a call on `this` would reset the nullable flow state the rest of this
        // method depends on; the break arm lives in PlayShieldBreak, where the burst is spawned.
        if (ShotShieldFx is not null)
        {
            _shieldSeen = true;   // the bar has to exist for the pose to sit on it
            if (ShotShieldFx == "gain") UiMotion.Flash(ShieldGainKey, UiMotion.Transition);
            else if (ShotShieldFx == "absorb") UiMotion.Flash(ShieldAbsorbKey, UiMotion.Fast);
            // ...AND THE EFFECT, not only the bar. §106 asks to see the barrier surrounding two
            // different hunters, and the standing barrier breathes between 0.40 and 0.60 alpha — its
            // brightest moment is still the flare. Posing only the bar left that to luck: the same
            // fixture caught the flare on THE MAGPIE and missed it on THE SEEKER, because the replay
            // fires the grant wherever it fires it. A state no dial can pose has never been looked at.
            if (!_shieldFxPosed && ShotShieldFx is "gain" or "absorb")
            {
                _shieldFxPosed = true;
                _vfx.Play(ShotShieldFx == "gain" ? VfxProfiles.ShieldGain : VfxProfiles.ShieldAbsorb,
                          FxFor("shield"), VfxSubject.Champion, Steel, fps: PosedFxFps);
            }
        }

        // THE RELEASED BEAT HAS CROSSED — latched here, after the batch, so the range of effects it
        // launched closes over everything its beat set off: the cast's own strip, the blow's impact and,
        // when it killed, the plume over the fall. ReleasedBeatPlayed waits on exactly that range.
        if (_releasedAtMs is { } releasedAt && !_releasedCrossed && _playheadMs >= releasedAt)
        {
            _releasedCrossed = true;
            _releasedFxTo = _vfx.Launched;
        }

        if (!_replay.Finished) return;

        if (_outcome == WaveOutcome.Cleared)
        {
            // Pay this wave out NOW, then walk straight into the next one — no boundary, no
            // button. The descent banked the reward (and the depth) the moment the sim cleared the
            // wave; the screen forwards it here, when the player SEES the clear.
            while (_descent.HasReward) _rewards.Enqueue(_descent.TakeReward());

            // Announce the clear and slide the NEXT enemy in, so the wave boundary is something you SEE.
            // A boss falling is the reward beat — it dropped a chest — so it gets its own louder banner
            // that lingers a touch longer. You FEEL the earn at the kill, then go crack it in the Forge.
            // A chest is no longer a given, so the boss banner no longer promises one — the host calls
            // FlashChest() and upgrades this banner only when a chest actually drops.
            // `!`: UpdateFight returns on a null run at its first line and nothing between here and there
            // clears it — the flow analysis simply loses the fact across the calls in the event loop.
            _bannerText = _run!.LastWaveWasBoss ? "BOSS DOWN!" : $"WAVE {_run.Wave} CLEARED";
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
            // Fell (or stalled). A red flash, the collapse, then the stage fades to black and the next
            // descent begins under it (DeathTransition).
            //
            // The REPORT is taken here, at the exact moment the run ended, and goes STRAIGHT into the
            // EXPEDITION LOG (L). Nothing over the arena announces it: a fall is part of the idle loop,
            // and the log keeps the report for whenever it is wanted.
            Log.Add(_run!.Report(isRecord: _run.Wave > _recordToBeat));   // kept and saved — see the Log property
            // WHAT THIS DESCENT PROVED. Taken here for the same reason the report is: one frame later
            // the champion regroups onto a fresh expedition and the counter is empty.
            LastRunVowProof = _run.VowProofWaves.ToDictionary(kv => kv.Key, kv => kv.Value);
            LogDirty = true;

            // THE WAVE THAT FELL, BEFORE ANYTHING IS STAMPED WITH IT. TraitFirst.Wave is provenance
            // the SAVE keeps for every awakening (TraitLedger.SaveProvenance) beside the champion and
            // the region — those two are what the TRAITS screen prints today; the wave is written,
            // read back and available to whatever prints it next. The re-check below stamps whatever
            // this holds, so it is assigned for THIS fall first: a number persisted wrong for the life
            // of a career is wrong whether or not a surface has been built to show it yet.
            _fellWave = Math.Max(1, _replayWave);

            // AND THE RUN'S END IS THE ONE MOMENT FAILURE CAN TEACH. WHAT KILLED YOU reads the log
            // that was just written — three consecutive deaths in one region against one kind of
            // creature — so the account facts are refreshed by the host and every rule re-checked
            // here, after Log.Add and never before it. Nothing is added to a counter: a lost run
            // teaches nothing on its way out, which is the rule skill experience already follows.
            if (TraitWatch is { } watchFell)
            {
                watchFell.Account = watchFell.Account with
                {
                    WallStreak = TraitDiscovery.WallStreakOf(Log.Entries).Streak,
                };
                watchFell.Recheck(_fellWave);
            }

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
    private void PlaySkillVfx(SkillDef def, Source source, int? hitSlot = null)
    {
        // ONE STATEMENT, where there was a six-case switch of hand-placed pixels. The profile says
        // which figure the cast belongs to and how big it is against that figure; the skill says which
        // ART it wears (its own per-character strip where one exists, the shared one otherwise).
        //
        // The sim lands single-target skills on the first living creature, so that is the creature the
        // flash goes on; a trap bursts under the whole ROW, and the champion's own shapes stay on him.
        var profile = VfxProfiles.ForSkill(def);
        var target = hitSlot ?? TargetSlot();
        var subject = profile.Subject switch
        {
            VfxSubjectKind.Champion => VfxSubject.Champion,
            VfxSubjectKind.EnemyRow => VfxSubject.EnemyRow,
            _ => VfxSubject.Creature(target),
        };
        // IT CROSSES THE GAP. A bolt used to be played at the midpoint between the two figures and
        // simply appear there — "projectile efektlerinin gitme animasyonu yok, direkt düşmanın üstünde
        // çıkıyor" (2026-08-28). The strip is authored as an in-place spin, which is right and stays:
        // only the renderer knows where the two figures are this frame, so the renderer flies it.
        var travel = profile.Travel == VfxTravel.ToTarget ? VfxSubject.Creature(target) : (VfxSubject?)null;
        PlayFx(profile, subject, SourceGlow(source), FxFor(def), travel);
    }

    /// <summary>
    /// Fire one effect: a profile, a subject, and the colour of the moment. No pixels, ever.
    /// </summary>
    /// <param name="assetKey">
    /// Overrides the profile's shared art. A skill wears its own character-specific strip where one was
    /// generated, and the held field wears whatever art the Field skill names.
    /// </param>
    private void PlayFx(VfxProfile p, VfxSubject subject, Color tint, string? assetKey = null,
                        VfxSubject? travelTo = null)
        => _vfx.Play(p, assetKey ?? p.AssetKey, subject, tint, travelTo);

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


    // ══════════════════════════════════════════════════════════════════════════════════════════
    public void Draw(SpriteBatch b, Point mouse, string regionName, bool suppressBanner = false)
    {
        if (WriteBudgetLedger) WriteBudgetLedgerOnce();
        // The host hands us the cursor in this screen's own 1920 space (Game1.ChromeMouse, mapped once at
        // full resolution), so the log button and the utility doors hit-test it as is. PARKED under the
        // black, under the SAME predicate TakeInput refuses under: a control that will not answer must
        // not lift, whiten or offer its tip through a half-lifted fade (ADR-006 — what is drawn is what
        // is hit-tested). The readable beat is not covered, so it hovers and answers like any other frame.
        var hit = BlackCovers ? OffCanvas : mouse;
        if (_run is null || _replay is null || _champ is null) return;

        // The dev boss fixture is a STATIC verification shot — clear transient combat churn (death smoke,
        // callouts, flash, wave banner) so only the boss and its bar read.
        if (DevForceBoss) { _vfx.Clear(); _callouts.Clear(); _deathFlash = 0f; _bannerTimer = 0f; }

        _isBossWave = DevForceBoss || WaveScaling.IsBossWave(Math.Max(1, _replayWave), ExpeditionTuning.Default);
        var overlay = ResolveOverlay(suppressBanner);
        // Rev 4 §18.3: exactly one major overlay. The host draws WelcomeBack; when it does, the screen draws
        // none. Dev warning only — never a Debug.Assert (a failed assert aborts the game's Debug build).
        if (suppressBanner && overlay != HuntOverlay.None)
            System.Diagnostics.Debug.WriteLine("Only one major Hunt overlay may be active.");

        // ── ARENA — every world-space element is scissor-clipped to the arena rect (Rev 4 §1/§11). The host
        // began a canvas batch for us; end it, run the clipped arena pass, then reopen an UNCLIPPED batch for
        // the HUD (which the host closes). The VFX sub-pass inherits the same scissor via _vfx.Rasterizer. ──
        b.End();
        _ui.Device.ScissorRectangle = ArenaClip;
        // Effects are NOT scissored (2026-08-23): the clip edge cut bursts flat against an invisible
        // rectangle and gave the arena away ("bir karenin içinde"). The rail panels and the banner are
        // drawn after the arena and cover anything that strays under them.
        // Effects alone are unclipped; the arena batch gets its scissor BACK after them (RestoreRasterizer),
        // so callouts, the red flash and the overlay keep the arena's edge whether or not a burst is live.
        _vfx.Rasterizer = null;
        _vfx.RestoreRasterizer = ArenaRasterizer;
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, ArenaRasterizer);
        DrawArena(b, overlay);
        b.End();
        _vfx.Rasterizer = null;
        _vfx.RestoreRasterizer = null;

        // ── HUD — unclipped chrome over the arena. ──
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawHunterHud(b);
        DrawStageHeader(b, regionName, _isBossWave);
        DrawLogButton(b, hit);
        // Under the EXPEDITION LOG's full-screen scrim the rail is furniture — no TAKE ONLY edits, no
        // errands. TakeInput keeps that gate: with the log open it returns before the rail is reached.
        DrawRightColumn(b, hit);
        DrawSkillDock(b);
        HeaderStackBottom = StageHeaderBottomY;                   // the strip or the boss bar lowers it
        if (_isBossWave) DrawBossBar(b);                          // §10/§12: screen-space, NOT arena-clipped
        DrawEnemyLine(b);                                          // the wave's live strip under the header
        if (!_logOpen) DrawEnemyInspector(b, hit);                 // the hovered creature's live numbers and statuses
        // The red flash on a fall covers the whole 1920x1080 canvas, so it draws in this UNCLIPPED
        // pass, over the rails and panels too — inside the arena batch the scissor cut it down to the
        // arena rectangle. The settings' SCREEN FLASH switch still governs it.
        if (_deathFlash > 0f && ShowScreenFlash) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Ember * (_deathFlash * 0.35f));
        // THE FALL'S BLACK, after the flash and under the host's chrome. One fill over the whole canvas —
        // the arena, the header stack, the rails, the medallion, the scene behind — from BlackAlpha, the
        // reading TakeInput refuses under, so the host's toast gate cannot silence it and the HUD is
        // never refused while visible nor answered while covered. Batch C paints the pills, the gear and
        // the nav over it: their input is not blocked, so they stay live.
        var black = BlackAlpha;
        if (black > 0f) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Color.Black * black);   // ui-page-ok: the canvas, not the page
        if (_isBossWave && DevBossDebug) DrawBossDebugOverlay(b); // §17: fixture-only bounds visualization (F7)
        if (DevVfxDebug) DrawVfxDebugOverlay(b);                  // §70: the VFX contract's own arithmetic (F9)
        // LAST, over every panel on the screen: a hover tip is an answer to the mouse, and nothing drawn
        // for a cursor that is somewhere else may cover it (see DrawLogButton).
        if (_logTipAt is { } tipAt)
            _ui.HoverTip(b, "EXPEDITION LOG — every descent's report. The L key opens it too.", tipAt);
        // (the host closes this batch with b.End(); the shared hex nav is drawn by the host over every screen.)
    }

    /// <summary>Arena figures + effects, drawn inside the scissor clip so no actor/VFX/bar/number escapes it.</summary>
    // ── THE ENEMY INSPECTOR (2026-09-06). Rest the pointer on a creature and its live numbers appear:
    //    health, its bite and its defence as BASE → CURRENT, the wave's bite clock, and every status
    //    standing on it. Every figure is READ from WaveReplay, which replays the sim's own events —
    //    nothing here re-derives a combat rule. Colour says the direction FOR THE HUNTER (green when
    //    the creature is worse off, red when it is better off), and the arrow, the label and the
    //    status line say it in words, so the reading never depends on colour alone. ──

    /// <summary>The creature under the pointer, topmost last — or -1.</summary>
    private int HoveredCreature(Point hit)
    {
        if (_replay is null || _run is null) return -1;
        var found = -1;
        // The stable ENVELOPE takes the pointer (ActorPresentation) — a creature that is still standing.
        foreach (var slot in _creatureBoxes.Keys)
            if (Presentation(slot).Hovers(hit) && _replay.CreatureAlive(slot)) found = slot;
        _lastHover = (hit, found);
        return found;
    }

    /// <summary>
    /// The last pointer the arena hit-tested and what it found — for the geometry dump alone.
    /// </summary>
    /// <remarks>
    /// A hover proof used to be read off the pixels, which cannot tell a plate from the debug
    /// overlay's own text; this makes the answer a number beside the rectangles it was tested
    /// against. Written by <see cref="HoveredCreature"/>, which is the one hit-test there is.
    /// </remarks>
    private (Point At, int Slot) _lastHover = (Point.Zero, -1);

    /// <summary>One line of the inspector: a label, and the base → current pair for it.</summary>
    private readonly record struct InspectorRow(string Label, string Base, string Now, int Direction);

    private static string Whole(float v) => ((int)MathF.Round(v)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    private static string Secs(int ms) => (ms / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";

    private void DrawEnemyInspector(SpriteBatch b, Point hit)
    {
        var slot = HoveredCreature(hit);
        if (slot < 0 || _replay is null || _run is null) return;
        var r = _replay;

        // ── THE READINGS ──
        var health = r.CreatureHealth(slot);
        var maxHealth = r.CreatureMaxHealth(slot);
        var baseBite = r.CreatureBaseDamage(slot);
        var biteNow = r.CreatureDamageNow(slot);
        var breakPct = r.CreatureAttackBreakPercent(slot);
        var baseDef = r.CreatureBaseDefence(slot);
        var defNow = r.CreatureDefenceNow(slot);
        var baseEvery = r.EnemyIntervalMs;
        var everyNow = r.BiteEveryMsNow;
        var kind = _isBossWave ? "BOSS" : WaveArchetype.ToString().ToUpperInvariant();
        var name = _isBossWave ? _bossName : CreatureLook.Name;
        var source = CreatureSource(slot);
        var affixes = _run.LastWaveAffixes ?? Array.Empty<Affix>();

        // Direction: +1 = favourable to the hunter (green), -1 = unfavourable (red), 0 = unchanged.
        var rows = new List<InspectorRow>
        {
            new("ATTACK", Whole(baseBite), Whole(biteNow), biteNow < baseBite - 0.5f ? 1 : biteNow > baseBite + 0.5f ? -1 : 0),
            new("DEFENCE", Whole(baseDef), Whole(defNow), defNow < baseDef - 0.5f ? 1 : defNow > baseDef + 0.5f ? -1 : 0),
            new("BITES EVERY", Secs(baseEvery), Secs(everyNow), everyNow > baseEvery ? 1 : everyNow < baseEvery ? -1 : 0),
        };
        var statuses = new List<(string Text, int Direction)>();
        if (defNow < baseDef - 0.5f) statuses.Add(($"DEFENCE BREAK   {Whole(defNow - baseDef)}", 1));
        if (breakPct < 0) statuses.Add(($"ATTACK BREAK   {breakPct}%", 1));
        if (r.SlowPercent > 0) statuses.Add(($"SLOWED   +{r.SlowPercent}% BETWEEN BITES", 1));
        if (r.StaggerLeftMs > 0) statuses.Add(($"STAGGERED   {Secs(r.StaggerLeftMs)} LEFT", 1));
        if (r.MarkLeftMs > 0) statuses.Add(($"MARKED   YOUR SKILLS +{r.MarkPercent}% FOR {Secs(r.MarkLeftMs)}", 1));

        // ── THE PLATE: sized from its lines, beside the creature, on the page and off the dock. ──
        var pad = UiMetrics.Space(12);
        var lineS = UiTypography.Pitch(UiTypography.Caption);
        // The name line carries the creature's name, its role and its Source glyph; the plate widens
        // for a long name at a large profile rather than clipping it.
        var glyphEdge = UiTypography.Caption + UiMetrics.Space(4);
        var nameLineW = _ui.MeasureBig(name, UiTypography.Secondary) + UiMetrics.Space(8)
                        + glyphEdge + UiMetrics.Space(6) + _ui.MeasureBig(kind, UiTypography.Caption);
        var w = Math.Max(UiMetrics.Control(284), nameLineW + 2 * pad + 5);
        var lineB = UiTypography.Pitch(UiTypography.Secondary);
        var barH = UiMetrics.Control(8);
        var h = pad + lineB + lineS + UiMetrics.Space(8)                       // name, kind line, rule
                + lineB + UiMetrics.Space(4) + barH + UiMetrics.Space(8)       // the HEALTH block: figure, then its bar
                + rows.Count * lineS + UiMetrics.Space(6)                      // the three pairs
                + (statuses.Count > 0 ? UiMetrics.Space(2) + lineS + statuses.Count * lineS : lineS)
                + pad;
        // THROUGH THE ONE PLACEMENT RULE (PopoverPlacement, 2026-09-06), as EnemyInspectorPlacement
        // asks it: the side AWAY FROM THE HUNTER first, then the other sides; the hunter's envelope
        // and the IDLE panel are avoided, the header stack and the skill dock bound the viewport. At
        // 150 % a creature near the centre used to put this plate over the hunter, and at 125 % one
        // near the right edge put it over OPEN VAULT. The anchor is the creature's stable envelope.
        //
        // THE HUNTER IS HIS ENVELOPE, NOT HIS BOX. The keep-out used to be ChampBox — 400 x 430 of
        // layout that the hunter fills only where his clips reach — so the plate refused margin the
        // art never used. Now it is the same published geometry the pointer reads, plus one small
        // clearance the placement owns (EnemyInspectorPlacement.HunterClearance).
        var box = Presentation(slot).InspectorAnchor;
        var ceiling = HeaderStackBottom + UiMetrics.Space(8);
        var floor = Math.Min(s_dockRect.Height > 0 ? s_dockRect.Y : UiKit.PageBottom(UiMetrics.Space(12)), UiKit.PageBottom(UiMetrics.Space(12)));
        var viewport = new Rectangle(ArenaClip.X, ceiling, UiKit.Page.Width - ArenaClip.X, Math.Max(h + 2 * UiMetrics.Space(12), floor - ceiling));
        var plate = EnemyInspectorPlacement.Place(box, new Point(w, h), viewport, ChampionPresentation.Envelope, _utilityPanel);
        _ui.Fill(b, plate, Color.Black * 0.45f);
        _ui.Plate(b, plate, source is { } src ? SourceGlow(src) : null);

        var left = plate.X + pad + 5;
        var right = plate.Right - pad;
        var ty = plate.Y + pad;
        // THE NAME LINE IS THE NAME — the creature's own (2026-09-16), with its ROLE and its SOURCE in
        // the corner. The Source is THIS creature's, rolled by Core from the region's roster, which is
        // why the plate's accent and the glyph read it rather than the region's theme: a Verdant pack
        // can carry Body and Mind creatures, and the body a creature wears no longer says which.
        // (The health figure used to hang in this corner, three rows above the bar that quantifies it.)
        var cornerW = _ui.MeasureBig(kind, UiTypography.Caption);
        var cornerX = right - cornerW;
        _ui.TextBig(b, kind, cornerX, ty + (lineB - UiTypography.Caption) / 2, Slate, UiTypography.Caption);
        if (source is { } own && _ui.Assets.Get(SourceGlyphKey(own)) is { } ownGlyph)
        {
            cornerX -= glyphEdge + UiMetrics.Space(6);
            b.Draw(ownGlyph, new Rectangle(cornerX, ty + (lineB - glyphEdge) / 2, glyphEdge, glyphEdge), Color.White);
        }
        _ui.TextBig(b, _ui.ShortenBig(name, cornerX - left - UiMetrics.Space(8), UiTypography.Secondary), left, ty, Bone, UiTypography.Secondary);
        ty += lineB;
        var kindLine = affixes.Count > 0 ? string.Join("  ·  ", affixes.Take(2).Select(AffixWords)) : "NO AFFIX";
        _ui.TextBig(b, _ui.ShortenBig(kindLine, right - left, UiTypography.Caption), left, ty, Slate, UiTypography.Caption);
        ty += lineS + UiMetrics.Space(4);
        _ui.Fill(b, new Rectangle(left, ty, right - left, 1), UiInk.Rule);
        ty += UiMetrics.Space(4);

        // ── THE HEALTH BLOCK: one label, one figure, one bar, in that order and touching. ──
        // The bar spans the whole block so it reads as the picture OF the figure above it rather than
        // as a third column, and it is what makes a boss's four-digit pool legible — the number can
        // take whatever width it needs without squeezing the bar into a stub.
        _ui.TextBig(b, "HEALTH", left, ty + (lineB - UiTypography.Caption) / 2, Slate, UiTypography.Caption);
        var hpFigure = $"{Whole(health)} / {Whole(maxHealth)}";
        var hpRung = UiTypography.Secondary;
        var hpRoom = right - left - _ui.MeasureBig("HEALTH", UiTypography.Caption) - UiMetrics.Space(10);
        while (hpRung > UiTypography.Caption && _ui.MeasureBig(hpFigure, hpRung) > hpRoom) hpRung--;
        _ui.TextRightBig(b, hpFigure, right, ty + (lineB - hpRung) / 2, Bone, hpRung);
        ty += lineB + UiMetrics.Space(4);
        _ui.Bar(b, left, ty, right - left, barH,
                maxHealth <= 0f ? 0f : Math.Clamp(health / maxHealth, 0f, 1f), Ember);
        ty += barH + UiMetrics.Space(8);

        // BASE → CURRENT, one pair a row: the base muted, the arrow only when something moved, the
        // current in the direction's ink. A pair that has not moved prints one figure in Primary.
        var arrowW = _ui.MeasureBig("→", UiTypography.Caption);
        foreach (var row in rows)
        {
            _ui.TextBig(b, row.Label, left, ty, Slate, UiTypography.Caption);
            if (row.Direction == 0)
            {
                _ui.TextRightBig(b, row.Now, right, ty, Bone, UiTypography.Caption);
            }
            else
            {
                var ink = row.Direction > 0 ? UiInk.Good : Ember;
                var nowW = _ui.MeasureBig(row.Now, UiTypography.Caption);
                _ui.TextRightBig(b, row.Now, right, ty, ink, UiTypography.Caption);
                var ax = right - nowW - UiMetrics.Space(8) - arrowW;
                _ui.TextBig(b, "→", ax, ty, Slate, UiTypography.Caption);
                _ui.TextRightBig(b, row.Base, ax - UiMetrics.Space(8), ty, UiInk.Disabled, UiTypography.Caption);
            }
            ty += lineS;
        }
        ty += UiMetrics.Space(6);

        if (statuses.Count == 0)
        {
            _ui.TextBig(b, "NO STATUS ON IT", left, ty, UiInk.Disabled, UiTypography.Caption);
            return;
        }
        _ui.TextBig(b, "STATUS", left, ty, Slate, UiTypography.Caption);
        ty += lineS + UiMetrics.Space(2);
        var pip = UiMetrics.Control(8);
        foreach (var (text, dir) in statuses)
        {
            // A diamond in the direction's ink beside the words — the icon the reading has, at every profile.
            _ui.Diamond(b, new Rectangle(left, ty + (UiTypography.Caption - pip) / 2 + 1, pip, pip), dir > 0 ? UiInk.Good : Ember);
            _ui.TextBig(b, _ui.ShortenBig(text, right - left - pip - UiMetrics.Space(8), UiTypography.Caption),
                        left + pip + UiMetrics.Space(8), ty, dir > 0 ? UiInk.Good : Ember, UiTypography.Caption);
            ty += lineS;
        }
    }

    /// <summary>
    /// RH_SHOT_SOCKETS: every live hand socket as a crosshair, and each blade's first position — the review of
    /// "does the knife leave the hand, or appear near it".
    /// </summary>
    private void DrawSocketOverlay(SpriteBatch b)
    {
        if (_performance is not { } p) return;
        var frame = p.FrameAt(_playheadMs);
        if (p.Timing.Socket(frame, p.Recipe.HandSocket) is { } socket
            && ((IActionStage)this).TryActorFrame(p.Recipe.ClipKey, frame, out var drawn, out var size))
        {
            var at = ActorSocketMap.ToArena(drawn, size, frame, socket).ToPoint();
            _ui.Fill(b, new Rectangle(at.X - 14, at.Y - 1, 29, 3), new Color(80, 255, 140));
            _ui.Fill(b, new Rectangle(at.X - 1, at.Y - 14, 3, 29), new Color(80, 255, 140));
        }
        foreach (var v in p.LaunchPoints)
        {
            var at = v.ToPoint();
            _ui.Fill(b, new Rectangle(at.X - 5, at.Y - 5, 11, 11), new Color(255, 220, 60) * 0.8f);
        }
    }

    private void DrawArena(SpriteBatch b, HuntOverlay overlay)
    {
        // EVERY FIGURE IS PLACED BEFORE ANY OF THEM IS DRAWN. The effects pass resolves against what
        // this publishes, so an effect can never aim at last frame's rectangle (the old creature table
        // was written here and read from Update) nor miss the lunge the draw applies (every
        // champion-side effect used the un-pushed box while the champion stood up to 40 px right of it).
        LayoutActors();
        var attacking = EnemyAttacking;

        // UNDER the figures: the ground ring the pack stands in, and the field behind the body. This
        // tier did not exist before — one flat effects pass ran after both figures, so §68's layer
        // vocabulary had nowhere to land and a field could only ever haze the champion it wrapped.
        if (!ShotNoVfx) _vfx.DrawUnder(b);

        if (_isBossWave) DrawBoss(b, attacking);
        else DrawNormalEnemy(b, attacking);

        // Champion (arena left). Name/HP live in the top-left HUD.
        if (!ShotNoChamp)
        {
            DrawChampion(b, _champDrawBox, dead: _mode == Mode.Downed);
            // THE BUNDLE IN THE HAND (ADR-011): the same knives that will fly, drawn at his hand socket.
            _performance?.DrawProp(b, this, _playheadMs);
        }
        // The flying blades' STEEL, over every figure, in the normal batch: material, not light.
        var perfDraws = PresentTrace.Enabled ? b.GraphicsDevice.Metrics.DrawCount : 0;
        if (!ShotNoVfx) _outgoing?.DrawMaterial(b);
        if (!ShotNoVfx) _performance?.DrawMaterial(b);

        if (!ShotNoVfx) _vfx.DrawOver(b);
        // ...and everything about them that IS light, in one additive pass of its own.
        if (!ShotNoVfx && _performance is { } performer)
        {
            _vfx.BeginLight(b);
            _outgoing?.DrawLight(b, _playheadMs, _ui.Assets.Get("fxp_trail_soft"));
            performer.DrawLight(b, _playheadMs, _ui.Assets.Get("fxp_trail_soft"));
            // a lunge's speed lines trail the body, on its way in only
            if (performer is MeleePerformance lunge && _ui.Assets.Get("fxp_trail_soft") is { } streak)
                lunge.DrawSpeedLines(b, streak, _champDrawBox, _playheadMs);
            _vfx.EndLight(b);
            // the performance's own cost: its blades' sprites and the draw calls from its material to its light
            // (the effects pass between them is counted too, so this is an upper bound)
            if (PresentTrace.Enabled)
                PresentTrace.Log("perf-draw", $"sprites={performer.SpriteCount}\tdraws={b.GraphicsDevice.Metrics.DrawCount - perfDraws}\treleased={performer.Released}");
        }
        if (ShotSockets) DrawSocketOverlay(b);
        DrawCallouts(b);
        // The death flash used to be drawn HERE, inside the arena pass — whose rasterizer scissors
        // everything to ArenaRect, so the "full screen" flash was silently cropped to the arena
        // rectangle (playtest ten: "it only glows in a limited area"). It draws in the unclipped HUD
        // pass now (see Draw), where full screen actually means full screen.

        DrawArenaOverlay(b, overlay);
    }

    /// <summary>
    /// How big each archetype draws, relative to the enemy box — WHATEVER THE POPULATION.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scale is the fastest read in the arena. A Swarm is small and there are several; a Bruiser fills
    /// the box alone. Together with the archetype name above the wave's bar, this is how a player learns
    /// what beat them without being told.
    /// </para>
    /// <para>
    /// <b>Population-independent (2026-09-07).</b> A lone creature used to be laid out at the unscaled
    /// enemy box whatever its archetype, so the one archetype that ALWAYS rolls alone — the Bruiser —
    /// never once wore its own size, and a lone Swarm stood taller than its pack. Measured on the same
    /// art: 360 x 421 alone against 403 x 471 in a Bruiser pair. The multiplier now reaches both
    /// layouts through <see cref="CreatureRow"/>; population decides room, placement and spacing, and
    /// never whether the archetype's size language applies. The table itself is unchanged.
    /// </para>
    /// </remarks>
    public static float ArchetypeScale(Archetype a) => a switch
    {
        Archetype.Swarm => 0.58f,
        Archetype.Caster => 0.78f,
        Archetype.Armoured => 0.92f,
        _ => 1.12f,   // Bruiser
    };

    /// <summary>The box one creature of this archetype is laid out in — the enemy box at the archetype's scale.</summary>
    public static Point ArchetypeBox(Archetype a)
        => new((int)(EnemyBox.Width * ArchetypeScale(a)), (int)(EnemyBox.Height * ArchetypeScale(a)));

    /// <summary>The boxes of one row of creatures, and where the row stands — the ONE creature layout.</summary>
    /// <param name="Boxes">One rectangle per creature, in slot order, bob included.</param>
    /// <param name="CentreX">The row's resting centre, for the labels — deliberately not lunge-shifted.</param>
    /// <param name="TopY">The row's resting top: the plane minus the box height.</param>
    public readonly record struct CreatureRowLayout(Rectangle[] Boxes, int CentreX, int TopY);

    /// <summary>
    /// Lay out <paramref name="count"/> creatures of one archetype across the arena's right half:
    /// the same box for a lone creature as for each of a pack, the row compressed to fit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PURE ARITHMETIC over the arena's static geometry, so the population invariant can be tested
    /// rather than photographed: a creature's box is <see cref="ArchetypeBox"/> whether it stands alone
    /// or in a row of five. Population reaches only the spacing, the room the row may occupy and the
    /// bob phase of each figure.
    /// </para>
    /// <para>
    /// The row's resting centre is the enemy box's, clamped so the row stays inside the arena and off
    /// the champion; the slide-in and the lunge are added AFTER the clamp (see the note on the old
    /// LayoutComposition: a clamp applied to the moved position saturated and swallowed both). Each
    /// creature bobs on its own phase — five sprites bobbing in unison read as one animated object.
    /// </para>
    /// </remarks>
    /// <param name="archetype">The archetype every creature in the row is laid out as.</param>
    /// <param name="count">How many creatures. At least one.</param>
    /// <param name="enter01">The slide-in, 1 at the start of the wave and 0 at rest.</param>
    /// <param name="lunge01">The lunge, 1 at full extension.</param>
    /// <param name="anim">The bob clock, in seconds.</param>
    public static CreatureRowLayout CreatureRow(Archetype archetype, int count, float enter01, float lunge01, float anim)
    {
        count = Math.Max(1, count);
        var (w, h) = (ArchetypeBox(archetype).X, ArchetypeBox(archetype).Y);
        var half = w / 2;
        // A shorter slide that FADES in: the old 280-px entry began past the scissor edge, so a wave
        // appeared as a hard-cut slice growing out of nothing — the box the playtest could see.
        var enter = (int)(enter01 * 150f);
        var lunge = (int)(lunge01 * -40f);

        var room = ArenaRect.Width - w - 40;   // the span a row may occupy inside the arena
        var spacing = (int)(w * 0.54f);        // overlap slightly; a row of five must still fit
        if (count > 1 && room > 0) spacing = Math.Min(spacing, room / (count - 1));
        var wanted = Math.Max(0, spacing) * (count - 1);

        var lo = ArenaRect.X + half + 20 + wanted / 2;
        var hi = ArenaRect.Right - half - 20 - wanted / 2;
        lo = Math.Min(hi, Math.Max(lo, ChampBox.Right + 80 + wanted / 2));

        var resting = lo > hi ? ArenaRect.Center.X : Math.Clamp(EnemyBox.Center.X, lo, hi);
        var centre = resting + enter + lunge;
        var left = centre - wanted / 2;

        var boxes = new Rectangle[count];
        for (var i = 0; i < count; i++)
        {
            var cx = left + spacing * i;
            var bob = (int)(MathF.Sin(anim * 2f + i * 1.7f) * 7f);
            boxes[i] = new Rectangle(cx - w / 2, EnemyBox.Bottom - h + bob, w, h);
        }
        return new CreatureRowLayout(boxes, resting, EnemyBox.Bottom - h);
    }

    /// <summary>
    /// The archetype the arena LAYS OUT for — the wave's, or the rig's override.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FIXTURE ONLY (<c>RH_SHOT_ARCHETYPE</c>). It reaches every normal wave — a lone creature and a
    /// pack are laid out by the same <see cref="CreatureRow"/> at the same archetype scale — and only a
    /// BOSS ignores it, because a boss has its own presentation law (<see cref="LayoutBoss"/>). The box
    /// that asks the renderer for more magnification than it will give is the 488 x 492 Bruiser box,
    /// which a Bruiser now wears alone as well as in a NUMBERS band's pair.
    /// </para>
    /// <para>
    /// It moves a layout number and nothing else: no gameplay value, no Core call, no roll.
    /// </para>
    /// </remarks>
    private Archetype WaveArchetype => DevArchetype ?? _run?.LastWaveArchetype ?? Archetype.Bruiser;

    /// <summary>FIXTURE ONLY: force the laid-out archetype (RH_SHOT_ARCHETYPE). Null = the wave's own.</summary>
    public Archetype? DevArchetype { get; set; }

    /// <summary>Is this run under the capture rig? A fixture that cannot pose its state must fail, not photograph.</summary>
    private static readonly bool UnderRig = Environment.GetEnvironmentVariable("RH_SHOT") is not null;

    /// <summary>
    /// Under the rig, refuse to photograph a wave <see cref="DevArchetype"/> could not reach.
    /// </summary>
    /// <remarks>
    /// The dial reaches every normal wave — lone or packed, both go through <see cref="CreatureRow"/>
    /// at the archetype's scale — and only a BOSS ignores it, because a boss has its own presentation
    /// law (<see cref="LayoutBoss"/>). On a boss the dial is inert, and an inert dial produces a
    /// perfectly ordinary picture of the WRONG state, which is the failure this project keeps paying
    /// for. Whether a wave is a boss is the seeded roll's to decide, so this is checked at the moment
    /// of use rather than assumed.
    /// </remarks>
    private void RequireArchetypeReached()
    {
        if (DevArchetype is not { } forced || !UnderRig) return;
        if (!_isBossWave) return;   // every normal wave, lone or packed, lays out at the archetype's scale
        throw new InvalidOperationException(
            $"RH_SHOT_ARCHETYPE={forced} changed nothing: this wave lays out a BOSS, which has its own "
            + "presentation law and never reads the archetype scale. Seek to a normal wave.");
    }

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
    private float _enemySinceHit = 99f;
    /// <summary>How long an enemy's follow-through stays on its attack clip: three frames at ~10 fps.</summary>
    private const float EnemyFollowSeconds = 0.3f;

    /// <summary>The wave whose replay is on screen (1-based) — see BeginWave; the header and the boss flag read it.</summary>
    private int _replayWave;

    // ── WHERE EACH FIGURE IS, laid out ONCE per frame before anything is drawn. ──────────────────
    //
    //    This replaces a dictionary the DRAW wrote and the UPDATE read, which had two faults the VFX
    //    contract could not live with. Positions were one frame STALE, because PublishCreature ran
    //    inside Draw while every effect spawned inside UpdateFight; and there was no entry at all on a
    //    wave's first frame, where EnemyPoint fell back to an imaginary point 110 px below the row.
    //    On the champion's side it was worse: he is DRAWN at ChampBox.X + push (up to 40 px right
    //    during a swing) and every one of his ten effects used the un-pushed box.
    //
    //    LayoutActors computes each figure's rectangle as the first statement of DrawArena; the draw
    //    methods consume what it computed, and the effects pass resolves against the VISUAL bounds it
    //    published. One source, one frame, no fallback.
    private readonly Dictionary<int, Rectangle> _creatureBoxes = new();
    private Rectangle _champDrawBox;

    /// <summary>Screen time at which each creature died — the death clip plays from it.</summary>
    private readonly Dictionary<int, float> _diedAt = new();
    private const float DeathFps = 10f;              // 8 frames in 0.8 s
    private const float DeathHoldSeconds = 0.6f;     // the body lies there
    private const float DeathFadeSeconds = 0.45f;    // then fades out

    /// <summary>A fall's whole length — its clip, the body lying there, the fade. What a SHOWN clear waits on.</summary>
    /// <remarks>
    /// 1.85 s against a 1.1 s break: in ordinary play the next wave arrives over the tail of the last
    /// fall, which is fine for the fortieth wave and wrong for the one a card is about to name. The
    /// authored opening keeps that wave's stage empty until this has run (HoldNextWave).
    /// </remarks>
    internal const float FallSeconds = 8f / DeathFps + DeathHoldSeconds + DeathFadeSeconds;

    // ── THE HELD BEAT, AND THE ONE THAT WAS LET GO (the authored opening's barrier; see UpdateFight). ──
    private int? _heldAtMs;                        // the beat the barrier held, until the host lets it go
    private int? _releasedAtMs;                    // ...then which beat that was
    private bool _releasedCrossed;                 // has the playhead crossed it since the release?
    private long _releasedFxFrom, _releasedFxTo;   // the effects its crossing launched (VfxPlayer.Launched)
    private int? _clipBeatMs;                      // the beat the committed champion clip is aimed at
    private Func<int, float>? _leadMs;             // PresentationLeadMs, cached — no delegate per frame

    /// <summary>The layout box laid out for creature <paramref name="slot"/> this frame.</summary>
    private Rectangle CreatureBox(int slot)
        => _creatureBoxes.TryGetValue(slot, out var r) ? r : EnemyBox;

    /// <summary>
    /// Creature <paramref name="slot"/>'s geometry this frame: its draw box, the visible body the
    /// arena published for it and the stable envelope its clips reach (falling back to the box only
    /// where no art resolved, which is what the draw itself falls back to). The hover and the
    /// inspector's anchor read this, nothing else.
    /// </summary>
    private ActorPresentation Presentation(int slot) => Presentation(VfxSubject.Creature(slot), CreatureBox(slot));

    /// <summary>The champion's geometry this frame — what the inspector keeps clear of.</summary>
    private ActorPresentation ChampionPresentation => Presentation(VfxSubject.Champion, _champDrawBox);

    /// <summary>One actor's geometry, as LayoutActors published it into the registry this frame.</summary>
    private ActorPresentation Presentation(VfxSubject subject, Rectangle box)
        => _actors.TryBounds(subject, out var vb)
            ? new ActorPresentation(box, vb.Rect, vb.Envelope)
            : new ActorPresentation(box, box, box);

    /// <summary>The right column's IDLE panel as the column laid it out this frame — the inspector keeps clear of it.</summary>
    private Rectangle _utilityPanel;

    /// <summary>Where a creature's VISIBLE middle is — the anchor a damage number hangs from.</summary>
    private int CreatureCentreX(int slot)
        => _actors.TryBounds(VfxSubject.Creature(slot), out var vb) ? vb.CenterX : _rowCentreX;

    /// <summary>
    /// The VISIBLE rectangle a figure occupies when its strip is drawn into <paramref name="box"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>UiKit.AnimSprite</c> crops the strip's transparent margin and fills the box HEIGHT with what
    /// is left, planting the visible sole on <c>box.Bottom</c>. So the drawn figure is shorter than the
    /// box (by the bottom pad's share) and almost always much narrower — <c>ChampBox</c> claims 400 px
    /// of width for a hunter who draws 162 px (THE OATHBOUND) to 373 px (QUIVER). Placing an effect
    /// against the box is how a barrier ended up a third the size of the body it was enclosing.
    /// </para>
    /// <para>
    /// Measured from ONE REFERENCE STRIP — the idle — never from the live clip, and that is
    /// load-bearing. <c>char_seeker_idle</c> has 132 px of side padding and <c>char_seeker_attack</c>
    /// has 30, so the same hunter draws 267 px wide standing and far wider swinging: a width-derived
    /// offset read off the live clip would make every effect jump on every cast. Height is nearly
    /// clip-invariant (412 against 411), so taking it from the reference too costs nothing.
    /// </para>
    /// <para>
    /// RESOLVED, not authored: the rectangle is landed at the scale <c>UiKit.AnimSprite</c> will
    /// actually draw at, which is the box's ask THROUGH the renderer's magnification ceiling
    /// (<c>UiKit.DrawScale</c>, reached here as <c>UiKit.FrameCeiling</c>). A box may ask for more
    /// than the ceiling allows — a Bruiser wave, lone or packed, gives a 488 x 492 box, which
    /// asked 1.27 of the retired rift guardian's deeply letterboxed idle (today the Flayed Brute's and the Dusk Ape's, 120 px of sky, ask 1.255) — and the renderer answers 1.25, so
    /// an authored rectangle would size every
    /// effect on that creature against a figure 6 px wider and 9 px taller than the one on screen.
    /// See <see cref="AuthoredRect"/> for the pre-ceiling rectangle, which is a diagnostic and not a
    /// consumer's geometry.
    /// </para>
    /// <para>
    /// Missing art falls back to the layout box, which is what the draw itself falls back to.
    /// </para>
    /// </remarks>
    private Rectangle VisualRect(Rectangle box, string? referenceStrip)
    {
        var (key, crop) = BodySource(referenceStrip);
        return key is null ? box : VfxFigure.Land(box, _ui.Content(key), _ui.DrawnFrame(key, crop, box.Height));
    }

    /// <summary>
    /// The figure's rectangle AS AUTHORED — what the box asked for, before the renderer's crop ceiling
    /// and magnification ceiling. Nothing consumes it; the geometry dump prints it beside the resolved
    /// body so a capped actor says so.
    /// </summary>
    private Rectangle AuthoredRect(Rectangle box, string? referenceStrip)
    {
        var (key, _) = BodySource(referenceStrip);
        return key is null ? box : VfxFigure.VisualRect(box, _ui.Content(key));
    }

    /// <summary>
    /// WHICH TEXTURE THE FIGURE IS ACTUALLY DRAWN FROM, and with which crop: the animation strip, else
    /// the single-pose fallback the draw falls back to, else nothing.
    /// </summary>
    /// <remarks>
    /// THE GEOMETRY MUST FOLLOW THE DRAW DOWN ITS FALLBACK. When a strip is missing the arena draws a
    /// static pose (<c>UiKit.SpriteGrounded</c>) — a real figure, at a real size — while the published
    /// geometry used to give up and return the LAYOUT BOX, so every effect on that creature was sized
    /// against a rectangle a third too wide and the pointer could hover its empty margin. Both paths
    /// now name the same texture and the same crop, and both resolve through <c>UiKit.DrawnFrame</c>.
    /// The crop is <see cref="MeasuredCrop"/> for both, which is what the draw sites pass.
    /// </remarks>
    private (string? Key, float Crop) BodySource(string? referenceStrip)
    {
        if (referenceStrip is null) return (null, MeasuredCrop);
        if (StripAvailable(referenceStrip)) return (referenceStrip, MeasuredCrop);
        var (still, crop) = StaticPoseFor(referenceStrip);
        return still is not null && _ui.Assets.Has(still) ? (still, crop) : (null, MeasuredCrop);
    }

    /// <summary>
    /// The single pose a strip falls back to, and the crop the draw gives it: the champion's base
    /// design (<c>char_&lt;id&gt;_base</c>, cropped by <see cref="ChampionStillCrop"/> as
    /// <see cref="DrawChampion"/> does), or a creature's <c>&lt;actor&gt;_idle_01</c>, measured as the
    /// arena's other draws are. Always the IDLE pose, never the live one — the body is stable across
    /// clips whether it comes from a strip or from a still.
    /// </summary>
    private static (string? Key, float Crop) StaticPoseFor(string referenceStrip)
        => referenceStrip.StartsWith("char_", StringComparison.Ordinal)
            ? (ChampionStillKey(referenceStrip), ChampionStillCrop)
            : (EnemyKeyOf(referenceStrip) + "_idle_01", MeasuredCrop);

    /// <summary>
    /// EVERY still the figure can be drawn from on the fallback path, with the crop the draw gives it.
    /// </summary>
    /// <remarks>
    /// The envelope must union these the way it unions a strip's clips, because the fallback draw
    /// picks its pose the same way the animated one does: <c>DrawComposition</c> and
    /// <c>DrawSingleEnemy</c> reach for <c>&lt;actor&gt;_attack_01</c> while the creature is biting and
    /// <c>&lt;actor&gt;_idle_01</c> otherwise. An envelope built from the idle alone would let the
    /// attack pose escape the rectangle it is hovered by — the very defect the strip path was
    /// repaired for. The champion has ONE still (his base design), so his list is one long.
    /// </remarks>
    private static IEnumerable<(string Key, float Crop)> StaticClipsFor(string referenceStrip)
    {
        if (referenceStrip.StartsWith("char_", StringComparison.Ordinal))
        {
            yield return (ChampionStillKey(referenceStrip), ChampionStillCrop);
            yield break;
        }
        var actor = EnemyKeyOf(referenceStrip);
        yield return (actor + "_idle_01", MeasuredCrop);
        yield return (actor + "_attack_01", MeasuredCrop);
    }

    /// <summary>The champion's base design key, from any of his strip keys.</summary>
    private static string ChampionStillKey(string referenceStrip)
        => referenceStrip[..referenceStrip.IndexOf("_idle_", StringComparison.Ordinal)] + "_base";

    /// <summary>
    /// THE ARENA'S ONE ANIMATED FIGURE DRAW. Every actor clip goes through here so the no-strip
    /// fixture (<see cref="DevNoStrips"/>) can take the strips away from the DRAW and the published
    /// GEOMETRY in the same breath — a fallback whose picture and whose rectangle disagree is the
    /// thing this exists to make impossible.
    /// </summary>
    /// <returns>false when the strip did not draw, so the caller falls back exactly as before.</returns>
    /// <param name="record">
    /// The figure this draw IS, so the frame it drew from is kept for the focus light's silhouette
    /// (<see cref="IFocusActors.TryDrawnFrame"/>); null for a pass that is not the figure — the hit
    /// flash draws the white mask through here and must not overwrite the figure's own frame.
    /// </param>
    private bool ActorSprite(SpriteBatch b, string stripKey, Rectangle box, float seconds, float fps,
                             bool loop, Color tint, float topCrop = 0f, bool flip = false, VfxSubject? record = null,
                             string? placeAs = null)
    {
        if (DevNoStrips || !_ui.AnimSprite(b, stripKey, box, seconds, fps, loop, tint, topCrop, flip, out var frame, placeAs)) return false;
        if (record is { } subject) _drawnFrames[subject] = frame;
        return true;
    }

    /// <summary>
    /// The frame each figure was drawn from THIS frame — cleared by <see cref="LayoutActors"/> before
    /// a figure is drawn, written by <see cref="ActorSprite"/> as each one is. Preallocated; a slot's
    /// entry is overwritten in place, never re-added.
    /// </summary>
    private readonly Dictionary<VfxSubject, SpriteFrame> _drawnFrames = new();

    /// <summary>The frame this figure was drawn from this frame, if a strip drew it.</summary>
    internal bool TryDrawnFrame(VfxSubject subject, out SpriteFrame frame) => _drawnFrames.TryGetValue(subject, out frame);

    /// <summary>The visible body <see cref="LayoutActors"/> published for this figure this frame.</summary>
    internal bool TryBody(VfxSubject subject, out Rectangle body)
    {
        if (_actors.TryBounds(subject, out var vb)) { body = vb.Rect; return true; }
        body = Rectangle.Empty;
        return false;
    }

    /// <summary>How many creature slots <see cref="LayoutActors"/> laid out this frame — a boss is one, in slot 0.</summary>
    internal int LaidOutCreatureCount => _creatureBoxes.Count;

    bool IFocusActors.TryDrawnFrame(VfxSubject subject, out SpriteFrame frame) => TryDrawnFrame(subject, out frame);
    bool IFocusActors.TryBody(VfxSubject subject, out Rectangle body) => TryBody(subject, out body);
    int IFocusActors.LaidOutCreatureCount => LaidOutCreatureCount;
    // THE MASK IS THE FLASH'S (AssetLibrary.WhiteMask): built once per strip, looked up by the strip the
    // frame came from. A texture the library did not load has no key and no mask, and the light falls
    // back to the body's rectangle rather than to a silhouette of nothing. Remembered per texture,
    // because the library's lookup builds the mask's key by concatenation and the light asks every frame.
    Texture2D? IFocusActors.MaskOf(Texture2D texture)
    {
        if (_maskOf.TryGetValue(texture, out var known)) return known;
        var mask = _ui.Assets.KeyOf(texture) is { } key ? _ui.Assets.WhiteMask(key) : null;
        _maskOf[texture] = mask;
        return mask;
    }

    /// <summary>Each strip's white mask (or its absence), remembered after the first ask.</summary>
    private readonly Dictionary<Texture2D, Texture2D?> _maskOf = new();

    /// <summary>Will the arena DRAW from this strip? The no-strip fixture takes it from geometry and draw alike.</summary>
    private bool StripAvailable(string key) => !DevNoStrips && _ui.Assets.Has(key);

    /// <summary>A negative crop means MEASURE the art's own headroom — the arena's rule, for every actor path.</summary>
    private const float MeasuredCrop = -1f;

    /// <summary>The crop <see cref="DrawChampion"/> gives the base design when no strip resolves.</summary>
    private const float ChampionStillCrop = 0.02f;

    /// <summary>
    /// FIXTURE ONLY (<c>RH_SHOT_NOSTRIP=1</c>): pretend every animation strip is missing, so the arena
    /// takes its STATIC FALLBACK — the path no shipped asset can reach, and therefore the one nobody
    /// had ever looked at. Read by the geometry and by the draw alike, so the two fall back together.
    /// </summary>
    public bool DevNoStrips { get; set; }

    /// <summary>
    /// The STABLE ENVELOPE a figure reaches when its clips <paramref name="strips"/> are drawn into
    /// <paramref name="box"/>: the union of every clip's opaque silhouette, landed the way AnimSprite
    /// lands it (<see cref="VfxFigure.Envelope"/>). Strips that do not resolve are skipped; none at
    /// all is the box, the draw's own fallback.
    /// </summary>
    /// <remarks>
    /// This is the rectangle the pointer and the inspector read, and the one the inspector keeps
    /// clear of on the hunter's side. It is NOT the body the effects measure (<see cref="VisualRect"/>,
    /// the idle reference) — the asset orders and the LAW 16 ratios were written from that, and a
    /// wider envelope would resize every effect. Both are constant across the clips: measured from
    /// what the figure CAN play, never from what it is playing, so an idle frame, an attack frame and
    /// an idle frame again leave the hover, the anchor and the keep-out exactly where they were.
    /// The attack animates inside it. The death clip is deliberately not in it: a dying figure lies
    /// flat and is not hoverable, so its silhouette is not a body anyone points at.
    /// </remarks>
    private Rectangle Envelope(Rectangle box, IReadOnlyList<string> strips, string? referenceStrip)
    {
        _envelopeClips.Clear();
        // EACH CLIP AT THE SIZE IT IS DRAWN. The crop and the ceiling are resolved once, by UiKit, per
        // clip — they differ between clips of one figure, since each is trimmed by its own headroom
        // and only some reach the ceiling — so the envelope holds no pixel the renderer never drew.
        foreach (var key in strips)
            if (StripAvailable(key))
                _envelopeClips.Add(new ResolvedClip(_ui.Silhouette(key), _ui.DrawnFrame(key, MeasuredCrop, box.Height)));
        // Nothing resolved: the figure is drawn from its static pose, whose one silhouette is its
        // whole reach — the same fallback the body takes, never the layout box while art exists.
        if (_envelopeClips.Count == 0 && referenceStrip is not null)
            foreach (var (still, crop) in StaticClipsFor(referenceStrip))
                if (_ui.Assets.Has(still))
                    _envelopeClips.Add(new ResolvedClip(_ui.Silhouette(still), _ui.DrawnFrame(still, crop, box.Height)));
        return VfxFigure.Envelope(box, _envelopeClips);
    }

    /// <summary>Scratch list for <see cref="Envelope"/> — one allocation for the screen's life, not one per frame.</summary>
    private readonly List<ResolvedClip> _envelopeClips = new();

    /// <summary>The champion's idle strip — the reference silhouette every champion-side effect measures.</summary>
    private string ChampionReferenceStrip => Character.StripKey("idle");

    /// <summary>
    /// The strip a champion strip is PLACED as (ADR-011): an authored action clip (one with a timing file) is keyed
    /// pixel-exact onto his idle, so it is drawn at the idle's scale and on its ground line; every other clip is
    /// placed by its own measurements, as it always was (see <see cref="UiKit.ResolveFrame"/>).
    /// </summary>
    private string? PlacedAs(string stripKey)
        => ActionClipLibrary.For(stripKey) is not null && stripKey != ChampionReferenceStrip ? ChampionReferenceStrip : null;

    /// <summary>
    /// Every clip the champion can be drawn in while standing THIS WAVE: the idle, the basic swing,
    /// and the clip each equipped skill commits — resolved the way <see cref="DrawChampion"/> resolves
    /// it (the character's own strip for the Form, else the generic one it stands in for).
    /// </summary>
    /// <remarks>
    /// THE BUILD, NOT THE ROSTER'S WHOLE VOCABULARY. <see cref="UpdateChampionClip"/> can only ever
    /// commit a clip a skill in <c>_waveSkills</c> names (plus the basic attack, and a trap for a
    /// Reaction), so unioning every Form's clip would keep the inspector off room this hunter cannot
    /// occupy — and the widest clip is a Form clip for eight of the ten: the thornwall's projectile
    /// reaches 515 px where his idle and swing reach 372. A build without a Projectile never throws
    /// it, and the plate should have that space.
    /// </remarks>
    private IReadOnlyList<string> ChampionClipStrips
    {
        get
        {
            if (_champClipStripsFor == Character.Id && ReferenceEquals(_champClipStripsSkills, _waveSkills) && _champClipStripsNoStrips == DevNoStrips)
                return _champClipStrips;
            // Most specific first, exactly as the draw resolves it — the decision itself is
            // ActorClips.ChampionStrips, which needs no GraphicsDevice and is tested without one. The
            // only thing this screen still owns here is the answer to "is that strip loaded?".
            ActorClips.ChampionStrips(Character, _waveSkills, StripAvailable, _champClipStrips);
            _champClipStripsFor = Character.Id;
            _champClipStripsSkills = _waveSkills;
            _champClipStripsNoStrips = DevNoStrips;
            return _champClipStrips;
        }
    }

    private readonly List<string> _champClipStrips = new();
    private string? _champClipStripsFor;
    private IReadOnlyList<EquippedSkill>? _champClipStripsSkills;
    private bool _champClipStripsNoStrips;

    /// <summary>The idle strip this wave's creatures are measured from (the region's body for the rolled archetype).</summary>
    private string? CreatureReferenceStrip => CreatureLook.IdleStrip;

    /// <summary>
    /// The clips a standing creature plays — its idle and its attack — for the wave's art key, or
    /// nothing. Rebuilt only when the key changes (a wave's creatures share one key).
    /// </summary>
    private IReadOnlyList<string> CreatureClipStrips(string? key)
    {
        if (key == _creatureClipStripsFor) return _creatureClipStrips;
        _creatureClipStrips.Clear();
        if (key is not null) { _creatureClipStrips.Add($"{key}_idle_strip8_512"); _creatureClipStrips.Add($"{key}_attack_strip8_512"); }
        _creatureClipStripsFor = key;
        return _creatureClipStrips;
    }

    private readonly List<string> _creatureClipStrips = new();
    private string? _creatureClipStripsFor;

    /// <summary>
    /// Lay every figure out and publish its visible bounds, before one pixel of the arena is drawn.
    /// </summary>
    /// <remarks>
    /// The rule this enforces: <b>a spawn site names a subject and a profile and never computes a
    /// pixel.</b> Everything positional lives here, once, and both the figures and their effects read it.
    /// </remarks>
    private void LayoutActors()
    {
        _creatureBoxes.Clear();
        _drawnFrames.Clear();   // this frame's figures record their frames below; last frame's are gone

        // The champion, WITH his lunge. The draw used to apply this push and the effects never saw it.
        // The BODY rides the lunge, because the effects are supposed to: a hit lands where the figure
        // is. The ENVELOPE does not, because the inspector is supposed not to — it is the union of
        // where the figure stands and where the swing carries it, so the plate's keep-out is the same
        // rectangle at rest and at full extension instead of sliding forty pixels twice a second.
        // VisualRect is purely additive in the box's x, so the lunged envelope is the resting one
        // shifted by the push; no second silhouette pass is needed.
        var push = (int)(_champLunge * ChampLungePx) + (int)MathF.Round(PerformedRootOffset());
        var lift = (int)MathF.Round(PerformedRootLift());
        _champDrawBox = new Rectangle(ChampBox.X + push, ChampBox.Y + lift, ChampBox.Width, ChampBox.Height);
        if (PresentTrace.Enabled && (_performance is MeleePerformance || _outgoing is MeleePerformance) && (push != _tracedRoot.X || lift != _tracedRoot.Y))
        {
            _tracedRoot = new Point(push, lift);
            PresentTrace.Log("root", $"x={push}\ty={lift}");
        }
        var standing = Envelope(ChampBox, ChampionClipStrips, ChampionReferenceStrip);
        var lunged = standing;
        lunged.Offset(ChampLungePx, 0);
        _actors.Publish(VfxSubject.Champion, VisualRect(_champDrawBox, ChampionReferenceStrip),
                        Rectangle.Union(standing, lunged), facing: 1);

        var comp = LaidOutCreatures;
        RequireArchetypeReached();
        if (_isBossWave) LayoutBoss();
        else LayoutComposition(comp.Count);   // one or many: the same row, the same archetype scale

        // The row: the union of the bodies standing in it. A trap ring is a statement about the PACK,
        // and the pack is nine hundred pixels wide and one creature tall — which is why it is the one
        // profile measured against a width.
        var strip = _isBossWave ? BossReferenceStrip : CreatureReferenceStrip;
        var clips = CreatureClipStrips(strip is null ? null : EnemyKeyOf(strip));
        Rectangle? row = null, reach = null;
        foreach (var (slot, box) in _creatureBoxes)
        {
            var vis = VisualRect(box, strip);
            var env = Envelope(box, clips, strip);
            _actors.Publish(VfxSubject.Creature(slot), vis, env, facing: -1);
            row = row is { } r ? Rectangle.Union(r, vis) : vis;
            reach = reach is { } e ? Rectangle.Union(e, env) : env;
        }
        if (row is { } union && reach is { } envelope) _actors.Publish(VfxSubject.EnemyRow, union, envelope, facing: -1);
    }

    /// <summary>
    /// The row's geometry for a wave of one or more creatures — compression, clamp, motion, bob.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE ROW COMPRESSES BEFORE IT OVERFLOWS. Spacing was a fixed fraction of the creature width, so a
    /// wide enough wave made the row wider than the arena — and then the clamp below was handed a
    /// minimum greater than its maximum, which is not a layout mistake but an ArgumentException:
    /// "'1028' cannot be greater than 1018", thrown out of Draw, killing the game mid-fight. It only
    /// appears on a big composition, which is why it survived every screenshot and every short run and
    /// was found by soaking the fight for five minutes.
    /// </para>
    /// <para>
    /// 0.54, not 0.62. Playtest: "düşmanlar çok yakında geliyor onları biraz daha sağa alabiliriz." The row
    /// is already pinned as far right as the clamp allows, so the creatures were not too far right —
    /// the ROW WAS TOO WIDE, and its left end reached back toward the champion.
    /// </para>
    /// <para>
    /// AND THE CLAMP STILL CANNOT INVERT. Compression handles every wave that can be made to fit; the
    /// floor that keeps the row off the champion is raised only as far as <c>hi</c> allows, so a wave
    /// too wide to leave the gap simply gets whatever gap there is.
    /// </para>
    /// <para>
    /// THE MOTION IS ADDED AFTER THE CLAMP, and that is the whole fix for a bug that swallowed both the
    /// slide-in and the lunge on 19 of 20 multi-creature waves: <c>hi</c> already sits below
    /// <c>EnemyBox.Center.X</c> for essentially every archetype and count, so a clamp applied to the
    /// moved position saturated and the animation never moved a pixel. Overhang is handled by the arena
    /// scissor, which is already active for exactly this kind of transient overshoot.
    /// </para>
    /// </remarks>
    private void LayoutComposition(int count)
    {
        // ONE LAYOUT FOR ONE OR MANY. The lone creature used to have its own method at the unscaled
        // enemy box, which is how a Bruiser — the archetype that always rolls alone — never wore its
        // own size. The row function is the single owner of the box; this only publishes its answer.
        var row = CreatureRow(WaveArchetype, count, _enemyEnter, _enemyLunge, _anim);
        // WHERE THE ROW ACTUALLY IS, published for the labels. Deliberately NOT the lunge-shifted
        // centre: a nameplate that slides 40 px every time the row shoves forward reads as jitter.
        _rowCentreX = row.CentreX;
        _rowTopY = row.TopY;
        for (var i = 0; i < row.Boxes.Length; i++) _creatureBoxes[i] = row.Boxes[i];
    }

    /// <summary>The boss: a square figure standing on its own anchor, with the same lunge as any creature.</summary>
    private void LayoutBoss()
    {
        // THE BOSS ARRIVES TOO. This read only the lunge, so a boss appeared at rest on its own mark
        // while every ordinary wave slid in — the one wave in five with a horn and a banner was also
        // the only one that popped. The entrance is the same rectangle offset the row uses, eased the
        // same way, so the two presentations cannot drift.
        var lunge = (int)(_enemyLunge * -40f) + (int)(_enemyEnter * _enemyEnter * BossEnterOffset);
        _creatureBoxes[0] = new Rectangle(BossAnchor.X - BossTargetBodyHeight / 2 + lunge,
                                          BossAnchor.Y - BossTargetBodyHeight,
                                          BossTargetBodyHeight, BossTargetBodyHeight);
        _rowCentreX = BossAnchor.X;
        _rowTopY = _creatureBoxes[0].Y;
    }

    /// <summary>How far off-stage-right a boss starts its arrival. Wider than the row's, because it is one figure.</summary>
    private const float BossEnterOffset = 260f;

    /// <summary>The boss's idle strip, or null when the region has no boss art.</summary>
    private string? BossReferenceStrip
        => BossArt is { } k && _ui.Assets.Has(k.IdleStrip) ? k.IdleStrip : null;

    /// <summary>Which boss this wave draws — the dev fixture's, or the region's.</summary>
    private BossLook? BossArt => DevForceBoss ? EnemyPresentation.BossByArtKey("crystal_lich") : EnemyPresentation.BossFor(RegionId);

    /// <summary>The creature the sim is hitting: the first alive one, else the one that died last.</summary>
    private int TargetSlot()
    {
        if (_replay is not null)
        {
            var n = _run?.LastWaveCreatures.Count ?? 1;
            for (var i = 0; i < Math.Max(1, n); i++) if (_replay.CreatureAlive(i)) return i;
        }
        var last = 0; var lastAt = float.MinValue;
        foreach (var (slot, at) in _diedAt) if (at > lastAt) { lastAt = at; last = slot; }
        return last;
    }

    /// <summary>
    /// Draw a dead creature's fall: its <c>&lt;key&gt;_death_strip8_512</c> clip from the moment it died,
    /// held on the last frame, then faded out. Without a death clip the creature vanishes as before.
    /// </summary>
    private void DrawCreatureDeath(SpriteBatch b, int slot, Rectangle box, string? enemyKey)
    {
        if (enemyKey is null || !_diedAt.TryGetValue(slot, out var at)) return;
        var t = _anim - at;
        var life = 8f / DeathFps + DeathHoldSeconds;
        var fade = t <= life ? 1f : 1f - (t - life) / DeathFadeSeconds;
        if (fade <= 0f) return;
        ActorShadow(b, box.Center.X, CreatureGround, (int)(box.Width * 0.55f), 30, 0.5f * fade);
        ActorSprite(b, $"{enemyKey}_death_strip8_512", box, t, DeathFps, loop: false, EnemyTint * fade, -1f,
                    record: VfxSubject.Creature(slot));
    }

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
        // THE FOLLOW-THROUGH FINISHES FIRST. The wind-up is recomputed every frame from the NEXT bite,
        // and in a fast region the next bite is inside the 900 ms wind-up window before the last one has
        // recovered — so the clip snapped back to frame 0 about 100 ms after every impact and frame 7
        // was never drawn at all (review 2026-08-30, measured on a 1000 ms bite interval). This is the
        // "an action is constantly interrupted by another action" the playtest reported, on every
        // creature, twice a second.
        return _enemyWindup > 0f && _enemySinceHit >= EnemyFollowSeconds
            ? _enemyWindup * clip * ContactFraction
            : clip * ContactFraction + _enemySinceHit;
    }

    /// <summary>The enemy is on its attack clip: winding up to a bite, or following one through.</summary>
    private bool EnemyAttacking => _enemyWindup > 0f || _enemySinceHit < EnemyFollowSeconds;

    private void DrawComposition(SpriteBatch b, bool attacking, IReadOnlyList<WaveCreature> comp)
    {
        // The geometry is LayoutComposition's — computed before anything drew, so the effects landed on
        // these exact rectangles rather than on the ones this method used to work out for itself.
        var scale = ArchetypeScale(WaveArchetype);
        var enterTint = Color.Lerp(EnemyTint * (1f - _enemyEnter * _enemyEnter), Ember, _enemyWindup * 0.38f);   // fade-in, then the ember wind-up flush
        var (w, h) = (ArchetypeBox(WaveArchetype).X, ArchetypeBox(WaveArchetype).Y);

        var look = CreatureLook;
        string? stripKey = attacking ? look.AttackStrip : look.IdleStrip;
        string? staticKey = attacking ? look.AttackStill : look.IdleStill;

        for (var i = 0; i < comp.Count; i++)
        {
            var box = CreatureBox(i);
            // THE HIT LANDS ON SOMEONE: for ~120 ms after a blow the creature is drawn a second time,
            // ADDITIVE and white, over itself — a SpriteBatch tint can only darken a sprite, so the warm
            // tint of the first attempt was invisible on a dark creature (playtest 2026-08-28: "the enemy
            // flash does not work"). No knock-back: the user asked for the flash alone.
            var hitFl = FlashAt(i);
            var creatureTint = enterTint;
            if (_replay is not null && !_replay.CreatureAlive(i))
            {
                DrawCreatureDeath(b, i, new Rectangle(box.X, EnemyBox.Bottom - h, w, h), stripKey is null ? null : EnemyKeyOf(stripKey));
                continue;
            }

            ActorShadow(b, box.Center.X, CreatureGround, (int)(w * 0.55f), (int)(38 * scale), 0.55f);

            // ── WHAT IS ON THIS CREATURE. A STATE, not a flourish (designer, 2026-08-30: "düşmanın
            //    üstünde olan efektin ikonu ve kaç X stack olduğu kalıcı olarak görünse de olur").
            //
            //    A rise-and-fade would have been the other option and it is the wrong one HERE: this is
            //    an idle game whose whole premise is that the champion fights while you are not looking,
            //    so a half-second flourish is a flourish nobody sees. A badge that stands for as long as
            //    the effect does is still there when the player comes back — which is the only moment
            //    that reliably exists.
            //
            //    Defence break is the first of these because it was the one the playtest could not feel;
            //    slow and attack break belong here too, and the layout leaves room to the right.
            DrawBreakBadge(b, i, box);

            // MEASURED headroom, not a constant. The 2026-08-22 strips fill ~90% of their frame with the
            // figure sat 3.5% up from the floor, so a fixed 8% crop bit the top of the tallest creatures;
            // a negative topCrop makes AnimSprite trim exactly the empty rows and never the figure.
            const float crop = -1f;
            var compFps = attacking ? 16f : 12f;
            if (stripKey is null || !ActorSprite(b, stripKey, box,
                    EnemyClipSeconds(attacking, compFps, i * 0.31f), compFps,
                    !attacking, creatureTint, crop, record: VfxSubject.Creature(i)))
                if (staticKey is null || !_ui.SpriteGrounded(b, staticKey, box, creatureTint, crop))
                    _ui.Fill(b, new Rectangle(box.X + 20, box.Y + 20, box.Width - 40, box.Height - 40), Ember);
            // The flash: the creature's WHITE SILHOUETTE (AssetLibrary.WhiteMask) over it at the same
            // frame — the only way a dark sprite turns white in a SpriteBatch.
            FlashOver(b, stripKey, box, EnemyClipSeconds(attacking, compFps, i * 0.31f), compFps, !attacking, hitFl, crop);

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
            // The house SLIM bar (UiKit.Bar) rather than two bare fills: a 56×6 red dash with no edge read as
            // a debug marker over the sprites, and at full life the well was invisible so nothing said
            // "a bar" until damage was taken (release polish 2026-09-05, hunt-05). Still world-anchored.
            // ANCHORED TO THE MEASURED FIGURE the VFX already use, not the box's top: a wide, crouched
            // creature fits its box's width, its head sits well under box.Y, and the pip hung sixty
            // pixels over empty wall (huntstates-07). Sized at the control rate, like every other bar.
            var pipY = _actors.TryBounds(VfxSubject.Creature(i), out var vb) ? vb.Rect.Top - UiMetrics.Space(10) : box.Y - UiMetrics.Space(14);
            var pipW = UiMetrics.Control(56);
            _ui.Bar(b, box.Center.X - pipW / 2, pipY, pipW, UiMetrics.Control(8), frac, Ember);
        }

        // One wave-level nameplate: what this wave IS, which is the thing the player has to learn.
        //
        // THE WAVE-TOTAL HEALTH BAR THAT SAT UNDER IT IS GONE (playtest ten: "it is pointless, and
        // buggy"). It summed every creature into one fill, so one big creature dying emptied most of
        // the bar while the rest of the wave stood — it read as a bar that resets — and it answered no
        // question the per-creature pips above each head do not answer honestly. The label keeps the
        // bar's exact anchor so the callout stack above it does not drift.
        // No "x4" after it (playtest 2026-08-25): the creatures are counted by being drawn, and each
        // wears its own life pip — the number said what the eye already had.
        // NOTHING IS WRITTEN OVER THE CREATURES ANY MORE (playtest 2026-08-26: "no text over the
        // creatures; give the live enemy information under the top panel"). The wave's label moved to
        // DrawEnemyLine, under the stage header, and reads live: what they are, how many still stand,
        // how much life the wave has left.
    }

    private void DrawNormalEnemy(SpriteBatch b, bool attacking)
    {
        var comp = LaidOutCreatures;
        if (comp.Count > 1)
        {
            DrawComposition(b, attacking, comp);
            return;
        }

        // The geometry is the row's (LayoutComposition, count 1), computed before anything drew: the
        // lone creature's box is its archetype's, bottom-anchored on the plane, bob included.
        var enterTint = Color.Lerp(EnemyTint * (1f - _enemyEnter * _enemyEnter), Ember, _enemyWindup * 0.38f);   // fade-in, then the ember wind-up flush
        var ab = CreatureBox(0);
        // The resting box — the same rectangle without the bob — for what must not bob: the shade,
        // the fallback fill, the health bar. Its top is the plane minus the box height, which is
        // where the row's TopY already is.
        var ebox = new Rectangle(ab.X, CreatureGround - ab.Height, ab.Width, ab.Height);
        ActorShadow(b, ebox.Center.X, CreatureGround, (int)(ebox.Width * 0.60f), (int)(42 * ArchetypeScale(WaveArchetype)), 0.6f);

        const float crop = -1f;   // measured headroom — see DrawComposition
        var figTop = _rowTopY;
        if (_replay is not null && !_replay.CreatureAlive(0))
        {
            DrawCreatureDeath(b, 0, new Rectangle(ebox.X, figTop, ebox.Width, ebox.Height), CreatureLook.ArtKey);
            return;
        }

        var look = CreatureLook;
        // 11/9, was 16/12. The whole complaint is legibility: an 8-frame swing at 16fps is over in half
        // a second, which is not long enough to see a creature wind up and commit. Held above ~9 so the
        // frames still read as motion rather than as a slideshow.
        var fps = attacking ? 11f : 9f;
        string? stripKey = attacking ? look.AttackStrip : look.IdleStrip;
        string? staticKey = attacking ? look.AttackStill : look.IdleStill;

        if (stripKey is null || !ActorSprite(b, stripKey, ab, EnemyClipSeconds(attacking, fps), fps,
                                                !attacking, enterTint, crop, record: VfxSubject.Creature(0)))
        {
            // Grounded so the static fallback stands where the animated strip does — otherwise the enemy
            // visibly hopped whenever the strip was missing and this path took over.
            if (staticKey is null || !_ui.SpriteGrounded(b, staticKey, ab, enterTint, crop))
                _ui.Fill(b, new Rectangle(ebox.X + 40, ebox.Y + 40, ebox.Width - 80, ebox.Height - 80), Ember);
        }
        FlashOver(b, stripKey, ab, EnemyClipSeconds(attacking, fps), fps, !attacking, FlashAt(0), crop);
        DrawBreakBadge(b, 0, ab);

        // The wind-up telegraph is the attack CLIP itself plus the ember tint blended into the sprite above
        // (playtest 2026-08-23: the old growing red rectangle around the enemy read as "a red box — what is
        // it?" and looked like debug chrome; a pixel frame has no place in a hand-drawn arena).

        // Quiet 130×12 bar ~18px above the VISIBLE top of the figure (§24.1), via the sprite's alpha bounds.
        // The strip is drawn with its measured headroom trimmed, so the visible top is the box top.
        var visTop = figTop;
        // Framed bar art, matching the Hunter's own HUD bar. A bare 130x12 red rectangle was the one
        // unstyled element left inside the arena, and at full health it read as a floating red streak
        // with nothing tying it to the creature underneath.
        var ebar = new Rectangle(ebox.Center.X - 92, visTop - 46, 184, 34);
        var nameY = ebar.Y - 28;
        // THE PLATE KEEPS CLEAR OF THE RIGHT RAIL (2026-09-16). A lone Bruiser stands at the arena's right
        // and tall, so its name and bar rose into the band the IDLE panel covers — and the rail is drawn
        // AFTER the arena, so the bar's right end and half the name were painted over at every profile
        // (enemies/*_lone_*). The plate slides left until it clears the panel it would sit under; the
        // panel is the one the rail recorded (last frame's, drawn later in this Draw).
        var rail = _utilityPanel;
        if (rail.Width > 0 && nameY < rail.Bottom + UiMetrics.Space(8) && ebar.Bottom > rail.Y)
        {
            var plateHalf = Math.Max(ebar.Width, _ui.MeasureBig(look.Name, UiTypography.Secondary)) / 2;
            var over = ebar.Center.X + plateHalf - (rail.X - UiMetrics.Space(12));
            if (over > 0) ebar.X -= over;
        }
        _ui.BarArt(b, ebar, Math.Clamp(_replay!.EnemyHealthFraction, 0f, 1f), "health");
        // The creature was never named on screen — the player fought an anonymous sprite for the whole run.
        // Its name is its look's: what this region calls this role (EnemyPresentation).
        _ui.TextCenterBig(b, look.Name, ebar.Center.X, nameY, UiKit.Vellum, UiTypography.Secondary);
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
        var bossArt = BossArt;
        _bossName = bossArt?.Name ?? "BOSS";
        // The corruption's epithet on the boss — "FEVERED CRYSTAL LICH" — so the tier is a thing with a
        // name that looks back at you, not a number on another screen.
        var epithet = CorruptionLook.For(CorruptionTier).Epithet;
        if (epithet.Length > 0) _bossName = epithet + " " + _bossName;

        var box = CreatureBox(0);   // LayoutBoss's, so the effects and the figure share one rectangle
        ActorShadow(b, box.Center.X, BossAnchor.Y, (int)(box.Width * 0.62f), 44, 0.6f);

        // The swing rides the same windup as everything else (see EnemyClipSeconds): the strike lands on the
        // frame the blow is credited, instead of the boss cycling its attack strip on the free clock.
        if (bossArt is not null && _replay is not null && !_replay.CreatureAlive(0) && _diedAt.TryGetValue(0, out var bossDiedAt)
            && _ui.Assets.Has(bossArt.DeathStrip))
        {
            // The boss falls and lies there for the whole break — no fade; the next wave clears it.
            ActorSprite(b, bossArt.DeathStrip, box, _anim - bossDiedAt, DeathFps, loop: false, EnemyTint, -1f,
                        record: VfxSubject.Creature(0));
            _bossBodyRect = box; _bossFullRect = box;
            return;
        }
        var fps = attacking ? 10f : 8f;
        var key = bossArt is null ? null : attacking ? bossArt.AttackStrip : bossArt.IdleStrip;
        var seconds = EnemyClipSeconds(attacking, fps);
        if (key is null || !ActorSprite(b, key, box, seconds, fps, !attacking, EnemyTint, -1f, record: VfxSubject.Creature(0)))
        {
            // THE FILL IS THE LAID-OUT BOX (2026-09-08). It used to be a hardcoded 220-wide rectangle
            // while LayoutActors published the full box as this boss's body and envelope — 2.45x wider
            // than the bar on screen, so 160 px of empty ground on each side hit-tested as the boss and
            // every relative-scale effect sized itself against a body nobody could see. Bosses ship
            // strips and no stills, so the boss is the one actor whose ladder can fall this far; the
            // geometry follows the draw down its fallback everywhere else, and now here too.
            _bossBodyRect = box;
            _bossFullRect = box;
            _ui.Fill(b, box, Ember);
            return;
        }
        // The boss flashes white for a blow like every other creature (playtest 2026-08-30: "the bosses
        // do not flash"). Same silhouette pass, same shaped life — the boss is always slot 0.
        FlashOver(b, key, box, seconds, fps, !attacking, FlashAt(0), -1f);
        DrawBreakBadge(b, 0, box);
        _bossFrame = attacking ? Math.Min(7, (int)(seconds * fps)) : (int)(seconds * fps) % 8;
        _bossBodyRect = box;
        _bossFullRect = box;
    }

    /// <summary>The dedicated boss health bar — SCREEN-SPACE UI (§10/§12), drawn in the HUD pass, not clipped.</summary>
    private void DrawBossBar(SpriteBatch b)
    {
        // Below the stage header (which ends at y=153) and right of the Hunter HUD (which ends at x=596).
        // At (510,145,900,46) it drove straight through both of them.
        // HUNG FROM THE HEADER'S FOOT, one name line and a breath under it, so a header that grew with the
        // profile pushes the bar down rather than the name up into it. (660, 200, 600, 54) at 100 %.
        var barY = StageHeaderBottomY + UiMetrics.Space(14) + UiTypography.Pitch(UiTypography.Headline);
        // ON THE HEADER'S OWN COLUMN (630..1190, centred on 910): at (660, 600) the bar jutted 70 px past
        // the header's right rail and its name was centred 50 px off the WAVE line above it, so the
        // stack read as two misaligned pieces (release polish 2026-09-05, hunt-04).
        var bar = new Rectangle(StageHeader.X, barY, StageHeader.Width, UiMetrics.Control(54));
        HeaderStackBottom = bar.Bottom;   // the host's toasts hang under the boss bar, not across it
        // The dev fixture's underlying wave-2 enemy is already dead (0%), so pose a representative fill.
        var frac = DevForceBoss ? 0.78f : Math.Clamp(_replay!.EnemyHealthFraction, 0f, 1f);
        // Name ABOVE the bar, not on it. Gold on the molten fill was gold-on-gold — the boss's name, the
        // one label that has to land, was the least readable thing on screen. Same rule as the parchment
        // panels: when the surface is already gold, the word moves off it.
        _ui.BarArt(b, bar, frac, "boss");
        _ui.TextCenterBig(b, _bossName, bar.Center.X, bar.Y - UiTypography.Pitch(UiTypography.Headline) - 1, UiKit.Vellum, UiTypography.Headline);
        // BONE OVER A SHADOW, like the dock's words: the figure was the near-black plate ink, legible only
        // while the molten fill happened to be under it, and dark-on-dark once the fill fell past the
        // middle (release polish 2026-09-05, hunt-12).
        var pct = $"{(int)(frac * 100)}%";
        ShadowText(b, pct, bar.Center.X - _ui.MeasureBig(pct, UiTypography.OverlayBody) / 2,
                   bar.Y + (bar.Height - UiTypography.OverlayBody) / 2 + 1, Bone, UiTypography.OverlayBody);
    }

    /// <summary>Rev 5 §17: fixture-only bounds visualization (ground pivot, body, full silhouette, arena).</summary>
    private void DrawBossDebugOverlay(SpriteBatch b)
    {
        DebugRect(b, ArenaRect, Ember, 3);                                         // arena stage — red
        DebugRect(b, ArenaClip, Steel, 2);                                         // arena clip — steel
        DebugRect(b, _bossFullRect, new Color(0x40, 0xE0, 0xE0), 2);               // full visible — cyan
        DebugRect(b, _bossBodyRect, new Color(0x48, 0xD0, 0x48), 3);               // body — green
        _ui.Fill(b, new Rectangle(BossAnchor.X - 22, BossAnchor.Y - 2, 44, 4), Gold);   // ground pivot — yellow cross
        _ui.Fill(b, new Rectangle(BossAnchor.X - 2, BossAnchor.Y - 22, 4, 44), Gold);
        _ui.TextBig(b, $"body {_bossBodyRect.Width}x{_bossBodyRect.Height}  frame {_bossFrame}  anchor {BossAnchor.X},{BossAnchor.Y}",
            190, 206, Gold, UiTypography.Secondary);
    }

    /// <summary>
    /// WHAT IS ON THIS CREATURE — a STATE, drawn for as long as the state lasts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The designer asked for exactly this, 2026-08-30: "düşmanın üstünde olan efektin ikonu ve kaç X
    /// stack olduğu kalıcı olarak görünse de olur". A rise-and-fade would have been the other option and
    /// it is the wrong one here: this is an idle game whose whole premise is that the champion fights
    /// while you are not looking, so a half-second flourish is a flourish nobody sees. A badge that
    /// stands is still there when the player comes back, which is the only moment that reliably exists.
    /// </para>
    /// <para>
    /// It used to be drawn INSIDE the composition loop, so a lone creature and a boss never got one —
    /// defence break was invisible on exactly the waves where a single stack matters most. It is
    /// reconstructed, not remembered: <c>WaveReplay.CreatureBreaks</c> is accumulated state, so a frame
    /// in which no break event replays still knows the stack is there, and a dev seek rebuilds it
    /// exactly. That is the shape the brief's §69 asks persistent states to have.
    /// </para>
    /// </remarks>
    private void DrawBreakBadge(SpriteBatch b, int slot, Rectangle box)
    {
        if (_replay?.CreatureBreaks(slot) is not ( > 0 and var stacks)) return;
        var badge = new Rectangle(box.Center.X - 40, box.Y - 34, 26, 26);
        _ui.Icon(b, "icon_effect_break", badge, Ember);
        _ui.TextBig(b, $"x{stacks}", badge.Right + 2, badge.Y + 5, Ember, UiTypography.Caption);
    }

    /// <summary>
    /// The actor geometry of the last laid-out frame as text — one line per subject: the body the
    /// effects measure, the envelope the pointer reads, and for the hunter the inspector's keep-out
    /// beside the layout box it replaced. Canvas pixels. The capture rig writes it beside a shot
    /// under <c>RH_SHOT_DUMP</c>, so a hover can be aimed from numbers rather than from a guess.
    /// </summary>
    public IEnumerable<string> DevActorGeometry()
    {
        static string R(Rectangle r) => $"{r.X},{r.Y},{r.Width},{r.Height}";
        var creatureStrip = _isBossWave ? BossReferenceStrip : CreatureReferenceStrip;
        foreach (var (subject, vb) in _actors.All.OrderBy(kv => kv.Key.Kind).ThenBy(kv => kv.Key.Slot))
        {
            var line = $"{subject}\tbody {R(vb.Rect)}\tenvelope {R(vb.Envelope)}";
            if (subject.Kind == VfxSubjectKind.Champion)
                line += $"\tkeepout {R(EnemyInspectorPlacement.HunterKeepOut(vb.Envelope))}\tbox {R(_champDrawBox)}";
            else if (subject.Kind == VfxSubjectKind.Creature)
                line += $"\tbox {R(CreatureBox(subject.Slot))}";
            // AUTHORED beside RESOLVED, and a VERDICT EITHER WAY naming WHICH limit bound. Silence
            // used to mean "the box's ask was honoured", which reads exactly like a fixture that
            // failed to pose its case at all; and one word for two different ceilings hid which.
            var (box, strip) = subject.Kind switch
            {
                VfxSubjectKind.Champion => (_champDrawBox, ChampionReferenceStrip),
                VfxSubjectKind.Creature => (CreatureBox(subject.Slot), creatureStrip),
                _ => (Rectangle.Empty, null),
            };
            if (strip is not null)
            {
                var authored = AuthoredRect(box, strip);
                if (authored != vb.Rect) line += $"\tauthored {R(authored)}";
                line += "\t" + Verdict(box, strip);
            }
            yield return line;
        }
        yield return $"Hover\tat {_lastHover.At.X},{_lastHover.At.Y}\tcreature "
                     + (_lastHover.Slot < 0 ? "none" : _lastHover.Slot.ToString());
    }

    /// <summary>
    /// Which presentation limit decided this figure's size — for the geometry dump alone.
    /// </summary>
    /// <remarks>
    /// GRANTED: the box got the size it asked for. MAGNIFIED: <c>UiKit.RasterCeiling</c> bound, so the
    /// figure is drawn smaller than the box wanted. CROPPED: the draw trimmed a different headroom
    /// than the art's own — the static fallback's fixed crop, or <c>UiKit.CropCeiling</c> holding a
    /// mis-measured one. STILL: the strip did not resolve and the figure is a single pose. The three
    /// are reported apart because one word for all of them hid which ceiling a fixture had reached.
    /// </remarks>
    private string Verdict(Rectangle box, string referenceStrip)
    {
        var (key, crop) = BodySource(referenceStrip);
        if (key is null) return "NOART";
        var resolved = _ui.ResolveCrop(key, crop);
        var uncapped = box.Height / MathF.Max(0.01f, 1f - resolved);
        var words = new List<string>();
        if (_ui.DrawnFrame(key, crop, box.Height) < uncapped - 0.5f) words.Add("MAGNIFIED");
        if (MathF.Abs(resolved - _ui.Content(key).Top) > 0.001f) words.Add("CROPPED");
        if (key != referenceStrip) words.Add("STILL");
        return words.Count == 0 ? "GRANTED" : string.Join("+", words);
    }

    /// <summary>
    /// FIXTURE / DEV ONLY (F9, or <c>RH_SHOT_MODE=vfxdebug</c>): draw the VFX contract's own arithmetic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The brief's §70. Off in normal play — nothing writes <see cref="DevVfxDebug"/> but the F9 key
    /// under <c>RH_DEV=1</c> and the capture rig's own mode — and drawn in the UNCLIPPED HUD pass, after
    /// the arena and before the hover tip, so it is never scissored and never covers a tooltip.
    /// </para>
    /// <para>
    /// What it shows, and why each one: the SUBJECT rectangle, because "the box is not the figure" is the
    /// fact the whole contract turns on; the FRAME rectangle dim beside the CONTENT rectangle bright,
    /// because seeing those two apart is what makes a padded strip obvious at a glance; the ANCHOR
    /// point, so <c>Center</c>, <c>Head</c> and <c>Standing</c> can be told apart without reading
    /// code; and the native RATIO, coloured green inside the §73 budget, amber under it
    /// and red over it — LAW 16 as a colour.
    /// </para>
    /// <para>
    /// <c>RH_VFX_DUMP=1</c> prints the same numbers as text, which is the artifact that actually gets
    /// checked. The picture is for judging placement; the dump is for judging size.
    /// </para>
    /// </remarks>
    private void DrawVfxDebugOverlay(SpriteBatch b)
    {
        // THE SUBJECTS, in a colour no layer uses, so a figure's bounds can never be mistaken for an
        // effect's. Their labels are STACKED in the reading block rather than floated over each box:
        // a swarm's creature rectangles overlap by more than half, so per-box labels wrote over each
        // other and the row's label over the first creature's.
        // The BODY bright and the ENVELOPE dim around it: the body is what the effects measure, the
        // envelope is what the pointer and the inspector read, and seeing the two apart is what makes
        // "the attack reaches past the idle" a picture. The hunter's KEEP-OUT (his envelope plus the
        // inspector's clearance) is dashed in the inspector's own colour, so a capture can show that
        // the plate stops at the art and not at the layout box.
        var sizes = new List<string>();
        foreach (var (subject, vb) in _actors.All.OrderBy(kv => kv.Key.Kind).ThenBy(kv => kv.Key.Slot))
        {
            DebugRect(b, vb.Envelope, UiKit.Vellum * 0.45f, 1);
            DebugRect(b, vb.Rect, UiKit.Vellum, 1);
            if (subject.Kind == VfxSubjectKind.Champion)
                DebugRect(b, EnemyInspectorPlacement.HunterKeepOut(vb.Envelope), Ember * 0.8f, 1);
            if (subject.Kind != VfxSubjectKind.Creature || subject.Slot == 0)
                sizes.Add($"{(subject.Kind == VfxSubjectKind.Creature ? "creature" : subject.ToString().ToLowerInvariant())}"
                          + $" {vb.Rect.Width}x{vb.Rect.Height} reach {vb.Envelope.Width}x{vb.Envelope.Height}");
        }

        // EVERY EFFECT'S MARKS, and its reading line built at the same time. The line carries the
        // AUTHORED numbers beside the pixels they produced — §70 asks the view to show the normalized
        // offset, and the gold line from the anchor to the content centre only shows that there IS one;
        // it cannot say it is 0.42 of a width. The profile is looked up by id rather than carried on
        // the item, because the table IS the source and a second copy of a number can disagree with itself.
        var green = new Color(0x48, 0xD0, 0x48);
        var read = new List<(string Text, Color Tint)> { (string.Join("  ·  ", sizes), UiKit.Vellum) };
        var over = 0;
        foreach (var fx in _vfx.DebugItems)
        {
            var tint = LayerColour(fx.Layer);
            DebugRect(b, fx.Frame, tint * 0.35f, 1);       // the padded frame — where the strip lands
            DebugRect(b, fx.Content, tint, 2);             // the picture — what the player sees
            _ui.Fill(b, new Rectangle(fx.Anchor.X - 9, fx.Anchor.Y - 1, 18, 2), Gold);
            _ui.Fill(b, new Rectangle(fx.Anchor.X - 1, fx.Anchor.Y - 9, 2, 18), Gold);
            _ui.Fill(b, LineRect(fx.Anchor, fx.Content.Center), Gold * 0.6f);
            _ui.TextBig(b, $"{fx.Id} · {fx.Layer}{(fx.Held ? " · held" : "")}",
                        fx.Content.X, fx.Content.Y - UiTypography.Pitch(UiTypography.Caption),
                        tint, UiTypography.Caption);
            if (fx.OverBudget) over++;
            var authored = VfxProfiles.Get(fx.Id);
            var basis = authored.Basis == VfxBasis.SubjectHeight ? "h" : "w";
            read.Add(($"{fx.Id}  {fx.Key}  {fx.Subject}  {fx.Content.Width}x{fx.Content.Height}"
                      + $"  {authored.Anchor}  x{authored.RelativeScale:0.00}{basis}"
                      + $"  off {authored.OffsetX:+0.00;-0.00;0.00}w {authored.OffsetY:+0.00;-0.00;0.00}h"
                      + $"  ratio {fx.NativeRatio:0.00}{(fx.OverBudget ? "  OVER BUDGET" : "")}",
                      fx.NativeRatio > VfxBudget.Max ? Ember : fx.NativeRatio < VfxBudget.Min ? Gold : green));
        }
        read.Add((over > 0
                     ? $"{over} EFFECT{(over == 1 ? "" : "S")} OVER THE ASSET SCALE BUDGET — REGENERATE, DO NOT MAGNIFY"
                     : "EVERY LIVE EFFECT IS INSIDE THE ASSET SCALE BUDGET",
                  over > 0 ? Ember : green));

        // THE READING BLOCK sits on the empty floor above the skill strip, on its own plate. It was
        // under the stage header first, where the host's own WELCOME BACK toast — drawn after every
        // screen — landed straight across it. A dev overlay obeys the same rule as any other panel:
        // nothing may cover it and it may cover nothing.
        //
        // The plate is MEASURED, not a constant width. Its longest line grew when the authored anchor,
        // scale and offset joined it, and a fixed width is a width that is wrong at one density profile
        // out of three — the same mistake the whole contract is about, one layer up. It is still capped
        // at the arena's right edge, so it can never reach the idle panel.
        var pitch = UiTypography.Pitch(UiTypography.Caption);
        var pad = UiMetrics.Space(6);
        var widest = read.Max(l => _ui.MeasureBig(l.Text, UiTypography.Caption));
        var plate = new Rectangle(ArenaRect.X + pad, 0,
                                  Math.Min(widest + pad * 2, ArenaRect.Width - pad * 2),
                                  pitch * read.Count + UiMetrics.Space(10));
        plate.Y = SkillStrip.Y - UiMetrics.Space(8) - plate.Height;
        _ui.Fill(b, plate, UiInk.Ground * 0.86f);
        var y = plate.Y + UiMetrics.Space(5);
        foreach (var (text, tint) in read)
        {
            _ui.TextBig(b, text, plate.X + pad, y, tint, UiTypography.Caption);
            y += pitch;
        }
    }

    /// <summary>
    /// <c>RH_VFX_BUDGET=1</c>: print every profile against every asset it can wear, once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The §73 / LAW 16 diagnostic, run where the textures actually are. A unit test cannot do this
    /// honestly — it would have to carry every strip's measured content box as a hand-typed constant,
    /// and a fixture that restates the art is a fixture that can drift away from it. This reads the
    /// real PNGs through the same cached alpha scan the draw path uses, so it cannot disagree with what
    /// the player sees.
    /// </para>
    /// <para>
    /// Three representative subjects, because a ratio is a property of a profile AND the figure it is
    /// measured against: the hunter, the smallest creature the arena makes (a swarm body) and the
    /// largest (a boss). OVER is the verdict that matters — it means the strip would have to be
    /// magnified past its authored size, which the renderer refuses to do.
    /// </para>
    /// </remarks>
    private void WriteBudgetLedgerOnce()
    {
        if (_budgetLedgerWritten) return;
        _budgetLedgerWritten = true;

        var champ = VisualRect(ChampBox, ChampionReferenceStrip);
        var swarmBox = new Rectangle(Point.Zero, ArchetypeBox(Archetype.Swarm));
        var swarm = VisualRect(swarmBox, EnemyPresentation.For(ArtRegion, Archetype.Swarm).IdleStrip);
        var boss = VisualRect(new Rectangle(0, 0, BossTargetBodyHeight, BossTargetBodyHeight), BossReferenceStrip);
        var row = new Rectangle(0, 0, swarm.Width * 3, swarm.Height);   // a four-creature row, compressed

        Console.WriteLine($"vfx-budget\tsubjects\tchampion {champ.Width}x{champ.Height}"
                          + $"\tswarm {swarm.Width}x{swarm.Height}\tboss {boss.Width}x{boss.Height}"
                          + $"\trow {row.Width}x{row.Height}");
        foreach (var profile in VfxProfiles.All)
            foreach (var key in AssetKeysFor(profile))
            {
                if (_ui.Assets.Get(key) is not { } tex) { Console.WriteLine($"vfx-budget\t{profile.Id}\t{key}\tMISSING"); continue; }
                var content = _ui.Content(key, profile.Frames);
                var frameW = Math.Max(1, tex.Width / Math.Max(1, profile.Frames));
                foreach (var (name, rect) in new (string, Rectangle)[]
                         {
                             ("champion", champ), ("swarm", swarm), ("boss", boss), ("row", row),
                         })
                {
                    if (!SubjectApplies(profile, name)) continue;
                    var placed = VfxResolver.Resolve(profile, new VisualBounds(rect, 1), content, frameW, tex.Height);
                    Console.WriteLine($"vfx-budget\t{profile.Id}\t{key}\t{name}"
                                      + $"\tvisible {placed.Content.Width}x{placed.Content.Height}"
                                      + $"\tframe {placed.Frame.Height}\tnative {tex.Height}"
                                      + $"\tratio {placed.NativeRatio:0.000}\t{VfxBudget.Of(placed.NativeRatio)}");
                }
            }
    }

    private static bool SubjectApplies(VfxProfile p, string subjectName) => p.Subject switch
    {
        VfxSubjectKind.Champion => subjectName == "champion",
        VfxSubjectKind.EnemyRow => subjectName == "row",
        _ => subjectName is "swarm" or "boss",
    };

    /// <summary>Every strip a profile can wear: its shared art, plus each character's own variant.</summary>
    private IEnumerable<string> AssetKeysFor(VfxProfile p)
    {
        yield return p.AssetKey;
        var bare = p.AssetKey.StartsWith("fx_", StringComparison.Ordinal) ? p.AssetKey[3..] : p.AssetKey;
        foreach (var c in CharacterRoster.All)
        {
            var own = $"fx_{c.Id}_{bare}_strip8_512";
            if (_ui.Assets.Has(own)) yield return own;
        }
        // The held field wears whatever art a Field skill names, which is not the profile's own key.
        if (p.Id != VfxProfiles.FieldAura.Id) yield break;
        foreach (var def in SkillCatalogue.All)
            if (def.Kind == SkillKind.Field) yield return $"fx_{def.FxKey}";
    }

    private static bool _budgetLedgerWritten;

    /// <summary>The debug view's colour per layer, so the z-model is legible without reading code.</summary>
    private static Color LayerColour(VfxLayer l) => l switch
    {
        VfxLayer.GroundUnder => new Color(0x7F, 0xCB, 0x4A),     // verdant — on the floor
        VfxLayer.BehindSubject => new Color(0x7C, 0x9A, 0xB0),   // steel — behind the body
        VfxLayer.Overhead => new Color(0x9B, 0x7B, 0xFF),        // violet — above everything
        _ => new Color(0xD8, 0xB4, 0x5C),                        // gold — on the body
    };

    /// <summary>A 2-px rectangle spanning two points, for the anchor-to-content line.</summary>
    private static Rectangle LineRect(Point a, Point c)
        => new(Math.Min(a.X, c.X), Math.Min(a.Y, c.Y),
               Math.Max(2, Math.Abs(c.X - a.X)), Math.Max(2, Math.Abs(c.Y - a.Y)));

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
        _logNumbersFirst = _logDiffFirst = 0;
    }

    public bool LogOpen => _logOpen;

    public void StepLog(int dir)
    {
        if (Log.Count == 0) return;
        _logIndex = Math.Clamp(_logIndex + dir, 0, Log.Count - 1);
        _logNumbersFirst = _logDiffFirst = 0;   // a new entry reads from its top
    }

    /// <summary>
    /// The first visible row of the log's two columns when they have more rows than the profile leaves
    /// room for (150 % on a report with a shield row). Zero — everything shown — at 100 %.
    /// </summary>
    private int _logNumbersFirst, _logDiffFirst;

    // THE WHEEL IS A PARAMETER NOW, NOT A FIELD. `_wheel` was sampled off Mouse.GetState() in Update
    // and read by DrawReportPanel — a field whose only job was to smuggle an input edge into the paint,
    // which is the shape this pass removes. The host's own per-frame delta reaches the two scroll
    // columns through TakeInput → TakeLogInput → ScrollReport, and nothing else ever sees it.

    /// <summary>
    /// The EXPEDITION LOG: a full-screen read of one run's report, with a way to walk back through the others.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UX V2 P1.2 (brief §23/§24, D6). The structure the playtest learned stays — the outcome band, the
    /// measures, SINCE YOUR LAST RUN HERE, OLDER / NEWER — and three things change. A DIAGNOSIS layer sits
    /// between the band and the numbers: <c>MAIN LIMIT — REACH</c>, the verdict sentence, and what to look
    /// at, from <see cref="RunReport.Limit"/> — the numbers below it explain magnitude, this explains meaning.
    /// The numbers and the diff stand side by side at readable rungs (nothing under Secondary, and Secondary
    /// only for chips), instead of the diff hanging under the table at Caption. And the DOORS live here —
    /// ADJUST BUILD and GEAR — because this is where a player decides what to change; the medallion over the
    /// arena is only the door to this screen.
    /// </para>
    /// <para>
    /// Deliberately one drawing for every entry rather than a summarised history view: the player has learned
    /// to read one layout, and a history that presented the same facts differently would make comparing two
    /// runs — the entire reason to keep them — harder, not easier.
    /// </para>
    /// </remarks>
    public void DrawLog(SpriteBatch b, Point mouse)
    {
        if (!_logOpen) return;

        // The host hands us the cursor in true 1920 space (Game1.ChromeMouse). Correct unconditionally
        // because opening the log closes every overlay screen, so this batch is never under the overlay
        // inset transform.
        var hit = mouse;

        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), new Color(0x0B, 0x09, 0x08, 0xE6));

        var panel = LogPanel;
        _ui.Panel(b, panel);   // the gold nine-slice: this IS a modal, the one PRIMARY surface on screen
        var x0 = UiKit.ContentLeft(panel);
        var x1 = UiKit.ContentRight(panel);
        var close = UiKit.CloseRect(panel);

        // ZONE A — the header row: what this screen is; which run, of how many; the close icon.
        _ui.TextBig(b, "EXPEDITION LOG", x0, panel.Y + UiTypography.ModalTitleTop, Gold, UiTypography.PanelTitle, TextFace.Display);
        _ui.CloseButton(b, close, hit, false);   // painted here; TakeLogInput decides it, and walks the same host path the L key does

        if (Log.Count == 0)
        {
            var emptyY = panel.Center.Y - UiMetrics.Space(70);
            _ui.TextCenterBig(b, "NO EXPEDITIONS YET", panel.Center.X, emptyY, Slate, UiTypography.RegionTitle, TextFace.Display);
            var lines = _ui.WrapBig("Your first report is written the moment your hunter falls or stalls. Every report stays here.", x1 - x0 - UiMetrics.Space(200), UiTypography.Body);
            var ey = emptyY + UiTypography.Pitch(UiTypography.RegionTitle) + UiMetrics.Space(8);
            foreach (var line in lines) { _ui.TextCenterBig(b, line, panel.Center.X, ey, Bone, UiTypography.Body); ey += UiTypography.Pitch(UiTypography.Body); }
            return;
        }

        // A LOCAL, NOT THE FIELD. The clamp is semantic state and it is written in TakeLogInput, which
        // runs before this every frame the log is open; reading it through a local keeps the paint from
        // writing anything at all.
        var index = Math.Clamp(_logIndex, 0, Log.Count - 1);
        var shown = Log.Entries[index];
        var older = Log.OlderThan(index);
        var region = Regions.Find(shown.RegionId)?.Name ?? shown.RegionId;
        var entry = $"ENTRY {index + 1} OF {Log.Count}";
        _ui.TextRightBig(b, region.Length > 0 ? $"{region.ToUpperInvariant()}  ·  {entry}" : entry,
                         close.X - UiMetrics.Space(20), panel.Y + UiTypography.ModalTitleTop + UiMetrics.Space(6), Slate, UiTypography.Body);

        DrawReportPanel(b, panel, shown, older);

        // ZONE E — the footer: paging on the left as ordinary buttons; the doors on the right, ADJUST BUILD
        // the one primary decision this screen offers. No RETRY — the hunter already regroups. ANCHORED to
        // the panel's foot at every profile — the doors are the screen's primary actions and never sit
        // under a scroll region (brief §18).
        var fy = LogFooterY(panel);
        var bh = LogButtonHeight;
        var foot = LogFooter(panel);   // THE SAME FOUR RECTS TakeLogInput HIT-TESTS
        _ui.Button(b, foot.Older, "‹  OLDER", hit, false, enabled: index < Log.Count - 1);
        _ui.Button(b, foot.Newer, "NEWER  ›", hit, false, enabled: index > 0);
        // A disabled arrow says why, beside itself (huntstates-10).
        var edge = Log.Count == 1 ? "THE ONLY ENTRY" : index == 0 ? "THIS IS THE NEWEST" : index == Log.Count - 1 ? "THIS IS THE OLDEST" : "";
        if (edge.Length > 0)
            _ui.TextBig(b, edge, foot.Newer.Right + UiMetrics.Space(16), fy + (bh - UiTypography.Secondary) / 2, Slate, UiTypography.Secondary);
        _ui.Button(b, foot.Build, "ADJUST BUILD", hit, false, true, ButtonStyle.Primary);
        _ui.Button(b, foot.Gear, "GEAR", hit, false);
    }

    /// <summary>The LOG's footer controls — paging on the left, the two doors on the right.</summary>
    /// <remarks>
    /// ONE GEOMETRY, READ BY BOTH HALVES: <see cref="DrawLog"/> paints these rectangles and
    /// <see cref="TakeLogInput"/> hit-tests them. Anchored to the panel's foot at every profile, so the
    /// doors never sit under a scroll region (brief §18).
    /// </remarks>
    private readonly record struct LogFooterRects(Rectangle Older, Rectangle Newer, Rectangle Build, Rectangle Gear);

    private static LogFooterRects LogFooter(Rectangle panel)
    {
        var x0 = UiKit.ContentLeft(panel);
        var x1 = UiKit.ContentRight(panel);
        var fy = LogFooterY(panel);
        var bh = LogButtonHeight;
        var build = new Rectangle(x1 - UiMetrics.Space(280), fy, UiMetrics.Space(280), bh);
        return new LogFooterRects(
            new Rectangle(x0, fy, UiMetrics.Space(180), bh),
            new Rectangle(x0 + UiMetrics.Space(196), fy, UiMetrics.Space(180), bh),
            build,
            new Rectangle(build.X - UiMetrics.Space(16) - UiMetrics.Space(200), fy, UiMetrics.Space(200), bh));
    }

    /// <summary>
    /// The LOG's own input — the close icon, the two scroll columns, the paging arrows and the doors, in
    /// the order <see cref="DrawLog"/> paints them.
    /// </summary>
    /// <remarks>
    /// Reached from <see cref="TakeInput"/> without the <c>huntOnTop</c> gate, because the log is drawn
    /// over whatever screen it was opened from (the host's L handler closes the menu screens, but its
    /// nav keys do not close the log) and its controls have to keep answering there.
    /// </remarks>
    private void TakeLogInput(Point mouse, bool clicked, int wheel)
    {
        var panel = LogPanel;
        if (UiKit.ClickedIn(UiKit.CloseRect(panel), mouse, clicked)) WantsLog = true;   // the same host path the L key walks
        if (Log.Count == 0) return;

        // THE CLAMP LIVES HERE. An index a trimmed log has put out of range is semantic state, and a
        // paint that writes state is what this pass removes; the paint reads a local clamp instead.
        _logIndex = Math.Clamp(_logIndex, 0, Log.Count - 1);
        ScrollReport(panel, Log.Entries[_logIndex], Log.OlderThan(_logIndex), mouse, wheel);

        var foot = LogFooter(panel);
        if (UiKit.ClickedIn(foot.Older, mouse, clicked) && _logIndex < Log.Count - 1) { _logIndex++; _logNumbersFirst = _logDiffFirst = 0; }
        if (UiKit.ClickedIn(foot.Newer, mouse, clicked) && _logIndex > 0) { _logIndex--; _logNumbersFirst = _logDiffFirst = 0; }
        if (UiKit.ClickedIn(foot.Build, mouse, clicked)) { _logOpen = false; WantsBuild = true; }
        if (UiKit.ClickedIn(foot.Gear, mouse, clicked)) { _logOpen = false; WantsGear = true; }
    }

    /// <summary>
    /// The log's panel: 1200×900 at 100 %, aspect 1.33 — held at or above 1.30 on purpose, because
    /// <see cref="UiKit.Panel"/> picks its frame art by aspect and the squarer frame's edge diamonds reach
    /// thirty pixels into the panel. At the larger profiles it grows at the spacing rate and is capped by
    /// the page's height, so it stays a modal on the page rather than a page of its own; centred on the
    /// page either way. A property: its size follows the setting.
    /// </summary>
    private static Rectangle LogPanel
    {
        get
        {
            // The cap keeps the panel's top under the host's currency pills' foot, so the close icon
            // (PanelCorner - 4 below the top) and the title row are never under them.
            // 800, not 900: at 100 % the rows ended 160 px above the footer and the buttons stood
            // stranded on bare panel (huntstates-04). Aspect 1.5 keeps the medium frame.
            var w = UiMetrics.Space(1200);
            var h = Math.Min(UiMetrics.Space(800), UiKit.Page.Height - UiMetrics.Space(60));
            return new((UiKit.Page.Width - w) / 2, (UiKit.Page.Height - h) / 2, w, h);
        }
    }

    /// <summary>The footer buttons' height — the log's own 56, at the profile.</summary>
    private static int LogButtonHeight => UiMetrics.Control(56);

    /// <summary>Where the footer's buttons sit: one button and a pad above the panel's foot.</summary>
    private static int LogFooterY(Rectangle panel) => panel.Bottom - UiMetrics.Space(48) - LogButtonHeight;

    /// <summary>Where the outcome band starts under the title row: the modal title, its line, and a breath. 100 at 100 %.</summary>
    private static int LogBandTop => UiTypography.ModalTitleTop + UiTypography.Pitch(UiTypography.PanelTitle) + UiMetrics.Space(20);

    /// <summary>What a diff row is called on screen — the table's own names, so the two blocks agree.</summary>
    private static string DiffLabel(string coreLabel) => coreLabel switch
    {
        "DEPTH" => "WAVES CLEARED",
        "HIT SIZE" => "AVERAGE HIT",
        "ABSORBED" => "ARMOUR ABSORBED",
        "WALL" => "ENDED BY",   // the band's own name for the same fact (huntstates-11)
        _ => coreLabel,
    };

    /// <summary>A diff value in plain words: no slash abbreviations, and a count of waves is a whole number.</summary>
    private static string DiffValue(string label, float v, string unit) => unit.Trim() switch
    {
        "%" => $"{v:F1}%",
        "targets/cast" => $"{v:F1} per cast",
        _ when label == "DEPTH" => $"{v:F0}",
        _ => $"{v:F1}",
    };

    /// <summary>An affix in the words its own doc comment uses — what it DOES, not its enum name.</summary>
    private static string AffixWords(Affix a) => a switch
    {
        Affix.Numbers => "MORE CREATURES",
        Affix.Plated => "HEAVIER ARMOUR",
        Affix.Ritual => "DAMAGE CLIMBS EACH WAVE",
        Affix.Endless => "HALF LEECH AND REGEN",
        Affix.Brittle => "EVERYTHING DIES FAST",
        Affix.Entrenched => "FIRST HITS DEAL A QUARTER",
        Affix.Swift => "BITES MORE OFTEN",
        _ => a.ToString().ToUpperInvariant(),
    };

    /// <summary>What to look at for a limit — the lever the numbers under it measure.</summary>
    private static string LookAt(RunLimit limit) => limit switch
    {
        RunLimit.Armour => "hit size — bigger hits get through armour.",
        RunLimit.Reach => "hits per cast — skills that strike more of the pack.",
        RunLimit.Sustain => "staying alive — health, defence, leech.",
        RunLimit.Stalled => "damage and speed — the wave outlived the clock.",
        _ => "power — nothing specific lost; the numbers did.",
    };

    /// <summary>Which measure row a limit names, so that row wears the gold rule. -1 for none.</summary>
    private static int LimitRow(RunLimit limit) => limit switch
    {
        RunLimit.Armour => 0, RunLimit.Reach => 2, RunLimit.Sustain => 3, RunLimit.Stalled => 4, _ => -1,
    };

    // ── THE REPORT'S VERTICAL GEOMETRY, as pure arithmetic off the panel. ─────────────────────────
    //    Both halves read it: the wheel is applied to the two columns in ScrollReport and the rows are
    //    laid out from the very same numbers in DrawReportPanel, so the column that scrolls is always
    //    the column the pointer was over and the rows that move are the rows that were drawn.

    /// <summary>ZONE B — the outcome band, at the top of the report's content.</summary>
    private static Rectangle ReportBand(Rectangle panel)
    {
        var x0 = UiKit.ContentLeft(panel);
        return new Rectangle(x0, panel.Y + LogBandTop, UiKit.ContentRight(panel) - x0,
                             UiMetrics.Space(24) + UiTypography.RegionTitle);
    }

    /// <summary>The ENDED BY line, under the band.</summary>
    private static int ReportEndedByY(Rectangle panel) => ReportBand(panel).Bottom + UiMetrics.Space(14);

    /// <summary>The diagnosis plate's own inset.</summary>
    private static int ReportDiagPad => UiMetrics.Space(14);

    /// <summary>ZONE C — the diagnosis plate: the limit's name, the verdict, and what to look at.</summary>
    private static Rectangle ReportDiag(Rectangle panel)
    {
        var x0 = UiKit.ContentLeft(panel);
        return new Rectangle(x0, ReportEndedByY(panel) + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(14),
                             UiKit.ContentRight(panel) - x0,
                             ReportDiagPad + UiTypography.Pitch(UiTypography.Headline) + UiTypography.Pitch(UiTypography.Body)
                             + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(10));
    }

    /// <summary>ZONE D — where the two columns and their heads begin.</summary>
    private static int ReportColumnsTop(Rectangle panel) => ReportDiag(panel).Bottom + UiMetrics.Space(20);

    /// <summary>The first row of either column: under the column's head, its hairline and a breath.</summary>
    private static int ReportRowsTop(Rectangle panel)
        => ReportColumnsTop(panel) + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(6) + UiMetrics.Space(8);

    /// <summary>The floor both columns stop at, clear of the footer's doors.</summary>
    private static int ReportFooterTop(Rectangle panel) => LogFooterY(panel) - UiMetrics.Space(16);

    /// <summary>The numbers column's width — the 100 % split (620 of 1120) kept as a share.</summary>
    private static int ReportLeftW(Rectangle panel) => (UiKit.ContentRight(panel) - UiKit.ContentLeft(panel)) * 620 / 1120;

    /// <summary>The diff column's left edge.</summary>
    private static int ReportRightX(Rectangle panel) => UiKit.ContentLeft(panel) + ReportLeftW(panel) + UiMetrics.Space(40);

    /// <summary>What the pointer has to be inside for the wheel to move THE NUMBERS.</summary>
    private static Rectangle NumbersHot(Rectangle panel)
    {
        var top = ReportColumnsTop(panel);
        return new Rectangle(UiKit.ContentLeft(panel), top, ReportLeftW(panel), ReportFooterTop(panel) - top);
    }

    /// <summary>What the pointer has to be inside for the wheel to move SINCE YOUR LAST RUN HERE.</summary>
    private static Rectangle DiffHot(Rectangle panel)
    {
        var top = ReportColumnsTop(panel);
        var rightX = ReportRightX(panel);
        return new Rectangle(rightX, top, UiKit.ContentRight(panel) - rightX, ReportFooterTop(panel) - top);
    }

    /// <summary>A column's measured rhythm: the row pitch, and how many rows there is room for.</summary>
    private readonly record struct ScrollRhythm(int Pitch, int Visible);

    /// <summary>
    /// THE RHYTHM COMES FROM THE ROOM THERE IS (brief §17): the house pitch when the rows fit, tighter
    /// down to a floor when they do not, and past the floor the column scrolls under the wheel.
    /// </summary>
    private static ScrollRhythm NumbersScroll(Rectangle panel, int count)
    {
        var pitchMax = UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(18);
        var pitchMin = UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
        var room = Math.Max(0, ReportFooterTop(panel) - ReportRowsTop(panel));
        var pitch = Math.Clamp(room / Math.Max(1, count), pitchMin, pitchMax);
        return new ScrollRhythm(pitch, Math.Min(count, room / pitch));
    }

    /// <summary>The diff column's rhythm — two lines to an entry, and the same clamp-to-the-room rule.</summary>
    private static ScrollRhythm DiffScroll(Rectangle panel, int count)
    {
        var diffMax = UiTypography.Pitch(UiTypography.Body) * 2 + UiMetrics.Space(8);
        var diffMin = UiTypography.Pitch(UiTypography.Body) * 2 + UiMetrics.Space(2);
        var room = Math.Max(0, ReportFooterTop(panel) - ReportRowsTop(panel));
        var pitch = Math.Clamp(room / Math.Max(1, count), diffMin, diffMax);
        return new ScrollRhythm(pitch, Math.Min(count, room / pitch));
    }

    /// <summary>
    /// THE NUMBERS' rows. Gathered here rather than inside the paint so the count the scroll measure
    /// uses and the rows the paint lays out can never disagree — a row added to this list scrolls.
    /// </summary>
    /// <remarks>
    /// SHIELD ABSORBED sits beside HEALTH LOST because they are the two halves of one question — what
    /// the wave landed, and what it landed ON. Never shown at zero: a build with no shield would
    /// otherwise read a row of nothing every run and learn to skip past the rows.
    /// </remarks>
    private static List<(string Label, string Figure, string Unit)> ReportRows(RunReport r)
    {
        var rows = new List<(string Label, string Figure, string Unit)>
        {
            ("ARMOUR ABSORBED", $"{r.AbsorbedFraction * 100f:F0}", "% of your damage"),
            ("AVERAGE HIT", $"{r.AverageHitSize:F0}", ""),
            ("REACH", $"{r.TargetsPerActivation:F1}", $"of {r.CreaturesPerWave:F1} creatures per cast"),
            ("HEALTH LOST PER WAVE", $"{r.HealthLostPerWaveFraction * 100f:F0}", "% of your health"),
            ("TIME PER WAVE", $"{r.SecondsPerWave:F1}", "seconds"),
        };
        if (r.ShieldAbsorbedFraction > 0f)
            rows.Insert(4, ("SHIELD ABSORBED", $"{r.ShieldAbsorbedFraction * 100f:F0}", "% of what the wave landed"));
        return rows;
    }

    /// <summary>
    /// The wheel, over the report's two scroll columns — the one place either first-row index is moved.
    /// </summary>
    /// <remarks>
    /// It used to live in <see cref="DrawReportPanel"/>, reading a <c>_wheel</c> field the screen
    /// sampled for itself: a paint that consumed an input edge, which on a catch-up tick is a notch the
    /// player never gets back. The clamp moved with it, because a paint that writes a scroll position is
    /// a paint that mutates state.
    /// </remarks>
    private void ScrollReport(Rectangle panel, RunReport r, RunReport? previous, Point mouse, int wheel)
    {
        var rows = ReportRows(r).Count;
        _logNumbersFirst = UiKit.Scrolled(_logNumbersFirst, NumbersHot(panel).Contains(mouse) ? wheel : 0,
                                          NumbersScroll(panel, rows).Visible, rows);

        var entries = r.DiffEntries(previous).Count();
        _logDiffFirst = UiKit.Scrolled(_logDiffFirst, DiffHot(panel).Contains(mouse) ? wheel : 0,
                                       DiffScroll(panel, entries).Visible, entries);
    }

    /// <summary>
    /// The report, as the log shows it: the outcome band, the diagnosis, the numbers and the diff side by side.
    /// Nothing here is measured; every number is <see cref="RunReport"/>'s. The footer is the caller's.
    /// </summary>
    /// <remarks>
    /// It takes no cursor and no edge any more: the wheel over its two columns is applied in
    /// <see cref="ScrollReport"/>, from Update, and every vertical anchor here comes from the same
    /// Report* helpers that measure it — so the rows that move are the rows that were drawn.
    /// </remarks>
    private void DrawReportPanel(SpriteBatch b, Rectangle panel, RunReport r, RunReport? previous)
    {
        var x0 = UiKit.ContentLeft(panel);
        var x1 = UiKit.ContentRight(panel);
        var pad = UiMetrics.Space(24);   // the inset of text inside the band and the diagnosis plate

        // ZONE B — the outcome band. FELL, in the title: every entry is the end of a run and must say so.
        // Ember for a fall, gold for a stall (the clock ran out, the hunter stood); a record is a chip.
        var stalled = r.Outcome == WaveOutcome.Stalled;
        var tint = stalled ? Gold : Ember;
        var band = ReportBand(panel);
        // The house plate with the outcome's accent, and its wash inside — the same component as the
        // diagnosis plate fourteen pixels under it, not a second treatment (huntstates-09).
        _ui.Plate(b, band, tint);
        _ui.Fill(b, new Rectangle(band.X + 5, band.Y + 1, band.Width - 6, band.Height - 2), tint * 0.16f);
        _ui.TextBig(b, stalled ? $"STALLED AT WAVE {r.WallWave}" : $"FELL AT WAVE {r.WallWave}",
            band.X + pad, band.Y + UiMetrics.Space(10), tint, UiTypography.RegionTitle, TextFace.Display);
        var cleared = $"{r.Depth} WAVE{(r.Depth == 1 ? "" : "S")} CLEARED";
        var clearedRight = band.Right - pad;
        _ui.TextRightBig(b, cleared, clearedRight, band.Y + (band.Height - UiTypography.Body) / 2, Bone, UiTypography.Body);
        if (r.IsRecord)
        {
            var chipX = clearedRight - _ui.MeasureBig(cleared, UiTypography.Body) - UiMetrics.Space(16) - ChipWidth("NEW RECORD", UiTypography.Secondary);
            var chipH = UiTypography.Secondary + UiTypography.ChipPadY * 2;
            Chip(b, chipX, band.Y + (band.Height - chipH) / 2, "NEW RECORD", Gold, Gold * 0.7f, UiTypography.Secondary);
        }

        // ENDED BY — the wave, named plainly, with its affixes in the words that say what they do. The affix
        // list takes whatever the line has left after the wave's name, and is shortened to it.
        var affixes = r.WallAffixes.Count > 0
            ? string.Join(", ", r.WallAffixes.Select(AffixWords))
            : "NO AFFIX";
        var y = ReportEndedByY(panel);
        var ax = Runs(b, x0, y, UiTypography.Body,
            ("ENDED BY   ", Slate),
            ($"{r.WallArchetype.ToString().ToUpperInvariant()} × {r.WallCreatures}", UiKit.Vellum),
            ("   ·   ", Slate));
        _ui.TextBig(b, _ui.ShortenBig(affixes, x1 - ax, UiTypography.Body), ax, y, Bone, UiTypography.Body);

        // ZONE C — the diagnosis: the limit's NAME, the verdict SENTENCE, and what to LOOK AT. A quiet plate
        // with the gold rule — this gold means "the thing that matters", and it is the only gold below the band.
        var diagPad = ReportDiagPad;
        var diag = ReportDiag(panel);
        _ui.Plate(b, diag, tint);   // the outcome's own colour: it names the failure (huntstates-05); gold stays on the title and ADJUST BUILD
        var dx = diag.X + pad;
        var dy = diag.Y + diagPad;
        _ui.TextBig(b, $"MAIN LIMIT — {r.LimitLabel()}", dx, dy, Bone, UiTypography.Headline);
        dy += UiTypography.Pitch(UiTypography.Headline);
        _ui.TextBig(b, _ui.ShortenBig(r.Verdict(), diag.Width - pad * 2, UiTypography.Body), dx, dy, Bone, UiTypography.Body);
        dy += UiTypography.Pitch(UiTypography.Body);
        _ui.TextBig(b, _ui.ShortenBig($"WHAT TO LOOK AT — {LookAt(r.Limit)}", diag.Width - pad * 2, UiTypography.Secondary), dx, dy, Slate, UiTypography.Secondary);
        y = ReportColumnsTop(panel);

        // ZONE D — two columns. LEFT: the numbers, each a lever; the row the diagnosis names wears the rule.
        // The split is the 100 % one (620 of 1120) kept as a share, so a wider panel widens both columns.
        var leftW = ReportLeftW(panel);
        var rightX = ReportRightX(panel);
        var rightW = x1 - rightX;

        _ui.TextBig(b, $"THE NUMBERS  ·  LAST {r.SampledWaves} WAVE{(r.SampledWaves == 1 ? "" : "S")}", x0, y, Slate, UiTypography.Body);
        var ty = ReportRowsTop(panel);
        Hairline(b, x0, ty - UiMetrics.Space(8), leftW, Slate * 0.45f);

        // THE ROWS, gathered by ReportRows so the scroll measure counts exactly what is drawn.
        var rows = ReportRows(r);
        var litRow = LimitRow(r.Limit);   // indexes the five base rows; the shield row is inserted AFTER it is resolved
        if (r.ShieldAbsorbedFraction > 0f && litRow >= 4) litRow++;
        // The figures' right edge: the house 330 at 100 %, or further right when the profile's labels need it.
        var labelW = rows.Max(row => _ui.MeasureBig(row.Label, UiTypography.Body));
        var figureW = rows.Max(row => _ui.MeasureBig(row.Figure, UiTypography.Headline));
        var xv = x0 + Math.Max(UiMetrics.Space(330), labelW + UiMetrics.Space(16) + figureW);
        // THE RHYTHM AND THE SCROLL ARE ONE MEASURE (NumbersScroll) — the wheel is applied to it in
        // ScrollReport, from Update, and the rows are laid out from it here. `first` is a CLAMP, not a
        // wheel read: ScrollReport has already moved the index this frame.
        var rhythm = NumbersScroll(panel, rows.Count);
        var rowPitch = rhythm.Pitch;
        var visible = rhythm.Visible;
        var first = UiKit.Scrolled(_logNumbersFirst, 0, visible, rows.Count);
        var scrolls = rows.Count > visible;
        var rowRight = x0 + leftW - (scrolls ? UiMetrics.ScrollbarWidth + UiMetrics.Gap : 0);
        var drop = UiMetrics.Space(5);   // a Body label's baseline nudge beside a Headline figure
        var rowsTop = ty;
        for (var i = first; i < Math.Min(rows.Count, first + visible); i++)
        {
            var (label, figure, unit) = rows[i];
            if (i == litRow) _ui.Fill(b, new Rectangle(x0 - UiMetrics.Space(14), ty - UiMetrics.Space(4), 4, rowPitch - UiMetrics.Space(8)), tint);
            _ui.TextBig(b, label, x0, ty + drop, Bone, UiTypography.Body);
            _ui.TextRightBig(b, figure, xv, ty, UiKit.Vellum, UiTypography.Headline);
            if (unit.Length > 0) _ui.TextBig(b, _ui.ShortenBig(unit, rowRight - xv - UiMetrics.Space(12), UiTypography.Body), xv + UiMetrics.Space(12), ty + drop, Slate, UiTypography.Body);
            ty += rowPitch;
            Hairline(b, x0, ty - UiMetrics.Space(10), rowRight - x0, Slate * 0.18f);
        }
        if (scrolls)
            _ui.ScrollBar(b, new Rectangle(x0 + leftW - UiMetrics.ScrollbarWidth, rowsTop, UiMetrics.ScrollbarWidth, visible * rowPitch), first, visible, rows.Count);

        // RIGHT: what changed since the last run here — the core concept, ranked as such.
        // The same head as THE NUMBERS beside it — two peer columns, one rung, one ink (huntstates-05).
        _ui.TextBig(b, "SINCE YOUR LAST RUN HERE", rightX, y, Slate, UiTypography.Body);
        var ry = ReportRowsTop(panel);
        Hairline(b, rightX, ry - UiMetrics.Space(8) - UiMetrics.Space(4), rightW, Slate * 0.45f);
        var entries = r.DiffEntries(previous).ToList();
        if (entries.Count == 0)
        {
            var region = Regions.Find(r.RegionId)?.Name.ToUpperInvariant() ?? r.RegionId.ToUpperInvariant();
            foreach (var line in _ui.WrapBig($"FIRST RUN IN {region} — nothing to compare yet.", rightW, UiTypography.Body))
            {
                _ui.TextBig(b, line, rightX, ry, Slate, UiTypography.Body);
                ry += UiTypography.Pitch(UiTypography.Body);
            }
        }
        // CLAMPED TO THE ROOM THERE ACTUALLY IS, above the footer: a row added to RunReport's diff must never
        // go under the frame. The same rhythm as the numbers: the house pitch when the entries fit, tighter
        // down to a floor when they do not, and past the floor they scroll under the wheel.
        var diffRhythm = DiffScroll(panel, entries.Count);
        var diffPitch = diffRhythm.Pitch;
        var diffVisible = diffRhythm.Visible;
        var diffFirst = UiKit.Scrolled(_logDiffFirst, 0, diffVisible, entries.Count);   // a clamp, not a wheel read
        var diffScrolls = entries.Count > diffVisible;
        var entryRight = rightX + rightW - (diffScrolls ? UiMetrics.ScrollbarWidth + UiMetrics.Gap : 0);
        var diffTop = ry;
        for (var i = diffFirst; i < Math.Min(entries.Count, diffFirst + diffVisible); i++)
        {
            var e = entries[i];
            _ui.TextBig(b, DiffLabel(e.Label), rightX, ry, Slate, UiTypography.Body);
            var vy = ry + UiTypography.Pitch(UiTypography.Body);
            var was = e.Numeric ? DiffValue(e.Label, e.Was, e.Unit) : e.WasText.ToUpperInvariant();
            var now = e.Numeric ? DiffValue(e.Label, e.Now, e.Unit) : e.NowText.ToUpperInvariant();
            var vx = Runs(b, rightX, vy, UiTypography.Body, (was, Bone), ("  →  ", Slate), (now, UiKit.Vellum));
            if (e.Numeric)
            {
                var sign = e.Delta >= 0f ? "+" : "";
                var change = e.Improved switch { true => UiInk.Good, false => Ember, null => Slate };
                var text = e.Improved is null ? "NO CHANGE" : $"{sign}{DiffValue(e.Label, e.Delta, e.Unit)}";
                Chip(b, Math.Max(vx + UiMetrics.Space(16), entryRight - ChipWidth(text, UiTypography.Secondary)), vy, text, change, change * 0.6f, UiTypography.Secondary);
            }
            ry += diffPitch;
        }
        if (diffScrolls)
            _ui.ScrollBar(b, new Rectangle(rightX + rightW - UiMetrics.ScrollbarWidth, diffTop, UiMetrics.ScrollbarWidth, diffVisible * diffPitch), diffFirst, diffVisible, entries.Count);
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
    // DrawGuide MOVED TO Game1.DrawHuntLesson — it is shared chrome now, not arena furniture.
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
                // Nothing: the fall is the collapse, the flash and the black, painted at the foot of Draw
                // from the clocks. Resolved here only so the wave's own banners stay off under it.
                break;
            case HuntOverlay.BossIncoming:
            {
                var fade = DevBossCall ? 1f : Math.Clamp(_bossIncomingTimer * 1.4f, 0f, 1f);
                // UNDER THE HEADER, NEVER BEHIND IT. The plate sat at a literal y of 200, which cleared
                // a header that ended at 153 and stopped clearing one that can now end at 262 — and the
                // state that makes the wave lane take a second row is BOSS WAVE, which is precisely the
                // state this announcement plays under. The panel's frame is opaque, so the top half of
                // the game's boss warning was simply painted over. Floored at the old 200 so nothing
                // moves at the profile and row count it was measured for.
                var plateTop = Math.Max(200, StageHeaderBottomY + UiMetrics.Space(24));
                // The plate is as tall as its two lines: (610, 200, 700, 110) at 100 %.
                var titleY = plateTop + UiMetrics.Space(24);
                var subY = titleY + UiTypography.Pitch(UiTypography.RegionTitle) + 1;
                var plateH = subY + UiTypography.OverlayBody + UiMetrics.Space(14) - plateTop;
                _ui.Fill(b, new Rectangle(610, plateTop, 700, plateH), PanelBg * fade);
                _ui.TextCenterBig(b, "BOSS INCOMING", 960, titleY, Gold * fade, UiTypography.RegionTitle, TextFace.Display);
                _ui.TextCenterBig(b, "GET READY", 960, subY, Bone * fade, UiTypography.OverlayBody);
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

    private const int ConquerAt = Checkpoints.ConquestWave;   // mirrors Game1.ConquerWaveDepth — shown so the goal is visible

    private static readonly Dictionary<Source, Color> SourceColor = new()
    {
        [Source.Shadow] = new(0x9A, 0x7A, 0xD8), [Source.Body] = new(0xD6, 0x48, 0x5C),
        [Source.Machine] = new(0xBC, 0x78, 0x40), [Source.Nature] = new(0x48, 0xB8, 0x88),
        [Source.Mind] = new(0x74, 0xC6, 0xE8), [Source.Spirit] = new(0xDC, 0xD4, 0xEC),
    };
    /// <summary>"FOURTH", "SIXTH" — the rhythm line's counting word.</summary>
    /// <remarks>
    /// A WORD, not "4TH", because the two shorter rhythms beside it are already words ("EVERY OTHER
    /// ACTION", "EVERY THIRD ACTION") and because the game is read in English as a second language: a
    /// spelled-out ordinal is one less thing to decode. Only 4 and 6 ship today (Projectile and Strike);
    /// the rest of the table is here so a new Form cannot silently print a digit.
    /// </remarks>
    private static string OrdinalWord(int n) => n switch
    {
        4 => "FOURTH", 5 => "FIFTH", 6 => "SIXTH", 7 => "SEVENTH", 8 => "EIGHTH",
        9 => "NINTH", 10 => "TENTH", 11 => "ELEVENTH", 12 => "TWELFTH",
        _ => $"{n}TH",
    };


    /// <summary>
    /// The hunter card's frame — level with the stage header beside it. Its height is the three lines it
    /// holds at this profile; the draw grows it further when a line has to reflow (see DrawHunterHud), and
    /// <see cref="s_hunterCardBottom"/> is where it actually ended. A property: the lines follow the setting.
    /// </summary>
    private static Rectangle HunterCard => new(196, 14, 420, HunterCardBaseHeight);

    /// <summary>The card's height when nothing reflows: the name line, the life line, the status row. 140 at 100 %.</summary>
    // NO RESERVED CHIP ROW: the card is sized by what it draws. It always added a status-chip line, so a
    // build with no UNDYING / CHARGE chip — most builds — fought under a card with an orphaned black band
    // 50–70 px deep at 125/150 % (release polish 2026-09-05, hunt-03). The chip block is added in
    // DrawHunterHud only when there are chips.
    private static int HunterCardBaseHeight
        => UiMetrics.Space(20) + UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6)
         + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(18);

    /// <summary>A Caption chip's height — the status row's, and the enemy strip's affix chips'.</summary>
    private static int ChipHeight => UiTypography.Caption + UiTypography.ChipPadY * 2;

    /// <summary>
    /// THE PORTRAIT IS A PICTURE, NOT AN ICON BESIDE A ROW. The card's width is fixed by the header beside
    /// it, so the picture keeps its 96 px wherever the card is tall enough to hold it, and the room goes to
    /// the text the profile grew: at 150 % a portrait at the control rate would leave the name 80 px to live in.
    /// </summary>
    /// <remarks>
    /// A CAP, NOT A SIZE — see <see cref="PortraitBox"/>. It was a flat 96 with a flat top offset, and when
    /// the reserved chip row came off the card (2026-09-05) the card lost about a third of its height while
    /// the picture kept all of its own: at 100 % a 96-px portrait hung 13 px BELOW a 105-px card. That is
    /// the "portrait has drifted downward" regression — the picture did not move, the frame shrank out from
    /// under it.
    /// </remarks>
    private const int PortraitEdge = 96;

    /// <summary>
    /// Where the portrait sits inside a card <paramref name="height"/> tall: as large as the card can hold
    /// up to <see cref="PortraitEdge"/>, and OPTICALLY CENTRED on the card's own axis.
    /// </summary>
    /// <remarks>
    /// Both numbers come from the card, so the picture can never outlive its frame again — a card that
    /// reflows taller re-centres it, and one that loses a row shrinks it instead of pushing it out of the
    /// bottom. No character-specific offset: every hunter's portrait is the same square in the same place,
    /// which is what makes silhouettes of different visual mass read as one row.
    /// </remarks>
    private static Rectangle PortraitBox(Rectangle frame, int height)
    {
        var edge = Math.Min(PortraitEdge, Math.Max(1, height - PortraitBreath * 2));
        return new Rectangle(frame.X + UiMetrics.Space(18), frame.Y + (height - edge) / 2, edge, edge);
    }

    /// <summary>The breath between the portrait and the card's border, at the card's own rate.</summary>
    private static int PortraitBreath => UiMetrics.Space(10);

    /// <summary>The card's bottom as last drawn — the tour's spotlight follows the reflowed card, not the base one.</summary>
    private static int s_hunterCardBottom = HunterCard.Bottom;

    /// <summary>
    /// Top-left HUNTER CARD: who, level, POWER, life, and the statuses that change during a fight.
    /// </summary>
    /// <remarks>
    /// UX V2 P1.1 (brief §19). It was a build sheet — a TEMPO multiplier, a mastery title, a row of the
    /// build's Source gems — none of which moves during a wave. The card carries only what the fight changes
    /// or is measured against: name · class · level; POWER, labelled; the life bar with its figure BESIDE it at
    /// Body (the most combat-critical number on the screen used to be its smallest text, centred on a red
    /// bar); and the live statuses Core already tracks — SHIELDED, UNDYING ready/spent, CHARGE n/cap — as chips
    /// that exist only while they say something.
    /// </remarks>
    private void DrawHunterHud(SpriteBatch b)
    {
        var frame = HunterCard;
        // The picture is laid out against the card's BASE height so the text column's width is known
        // before the card can reflow taller; its final Y comes from the card that is actually drawn,
        // below. Sizing it against the base is what guarantees it fits every taller variant too.
        var por = PortraitBox(frame, HunterCardBaseHeight);
        var x = por.Right + UiMetrics.Space(16);
        var right = frame.Right - UiMetrics.Space(24);
        var full = right - x;   // the text column's width
        var y = frame.Y + UiMetrics.Space(20);

        // ── EVERYTHING ON THE CARD IS MEASURED BEFORE ANYTHING IS DRAWN: the frame is as tall as the sum. ──
        // Line 1 — SEEKER · WANDERER  LV 12                         POWER / 222 (labelled; it was a bare ember number)
        var name = Character.ShortName;
        var cls = ItemClasses.NameOf(Character.Class);
        var lv = $"LV {_hunter?.HunterLevel ?? 1}";
        var powerVal = Game1.Abbrev(_hunter?.PowerRating ?? 0);
        var powerW = Math.Max(_ui.MeasureBig("POWER", UiTypography.Secondary), _ui.MeasureBig(powerVal, UiTypography.PrimaryValue));
        var powerCol = powerW + UiMetrics.Space(16);   // the POWER column, when it shares a line
        var lvGap = UiMetrics.Space(10);
        var lvW = lvGap + _ui.MeasureBig(lv, UiTypography.Secondary);
        const string sep = " · ";
        var line = name + sep + cls;
        // THE LINE REFLOWS BEFORE IT SHRINKS PAST THE FLOOR (brief §9, §17). In order: the whole line beside
        // POWER (the 100 % layout, where it always fits); the whole line with POWER moved to a row of its
        // own under the life bar; the name and class alone, LV moved down beside POWER; and last the class
        // on a line of its own under the name. The name steps from Headline down to Secondary at each stage
        // and never below it (the profile exists to make the text bigger), and is never cut while it fits.
        int Fits(string s, int extra, int room)
        {
            for (var p = UiTypography.Headline; p >= UiTypography.Secondary; p--)
                if (_ui.MeasureBig(s, p) + extra <= room) return p;
            return -1;
        }
        var px = Fits(line, lvW, full - powerCol);
        var powerBeside = px > 0;
        var lvOnLine1 = true;
        var clsOnLine2 = false;
        if (!powerBeside)
        {
            if ((px = Fits(line, lvW, full)) > 0) { }
            else if ((px = Fits(line, 0, full)) > 0) lvOnLine1 = false;
            else { clsOnLine2 = true; px = Math.Max(UiTypography.Secondary, Fits(name, 0, full)); }
        }

        // Line 2 — the life bar, its figure beside it. The POWER figure hangs into this line when it sits
        // beside the name, so the bar leaves that column free; the bar keeps the house 120 px where the
        // column can afford it and yields to the figure — which must always land — where it cannot.
        var hp = Math.Max(0, _replay?.HealthOf(0) ?? 0);
        var hpText = $"{hp} / {_champ?.MaxHealth ?? 0}";
        var hpTextW = _ui.MeasureBig(hpText, UiTypography.Body);
        var barGap = UiMetrics.Space(12);
        // THE SHIELD'S LABEL IS IN THE SAME COLUMN AS THE POOL'S FIGURE, so it has to be in the same
        // budget. It was not, and at 150 % the glyph pushed "SHIELD 30" past the card's edge and the
        // figure was ellipsed away to "SHIELD…" — the bar kept its width by taking the room from the
        // number the bar exists to quantify. Both readouts now bid for one column and the wider wins.
        var shieldGlyph = UiMetrics.Control(16);
        var shieldText = !ShieldStripShown ? ""
            : _replay!.CurrentShield > 0 ? $"SHIELD {_replay.CurrentShield}" : "SHIELD";
        var shieldTextW = shieldText.Length == 0 ? 0
            : shieldGlyph + UiMetrics.Space(5) + _ui.MeasureBig(shieldText, UiTypography.Secondary);
        var readoutW = Math.Max(hpTextW, shieldTextW);
        var barW = Math.Max(Math.Min(UiMetrics.Control(120), full - readoutW - barGap),
                            full - (powerBeside ? powerCol : 0) - readoutW - barGap);

        // Line 3 — the statuses, as chips: a chip that does not fit its line starts the next one.
        var chips = new List<(string Text, Color Ink)>();
        if (_undyingLive)
        {
            var spent = _champ?.UndyingSpent == true;
            chips.Add((spent ? "UNDYING SPENT" : "UNDYING READY", spent ? UiInk.Disabled : Gold));
        }
        if (_chargeLive) chips.Add(($"CHARGE {_chargeNow}/{_chargeCap}", _chargeNow >= _chargeCap ? Gold : Slate));
        var chipGap = UiMetrics.Space(8);
        var chipRows = 1;
        for (int i = 0, cx = 0; i < chips.Count; i++)
        {
            var cw = ChipWidth(chips[i].Text);
            if (cx > 0 && cx + cw > full) { chipRows++; cx = 0; }
            cx += cw + chipGap;
        }

        var height = HunterCardBaseHeight
                   + (clsOnLine2 ? UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(2) : 0)
                   + (powerBeside ? 0 : UiTypography.Pitch(UiTypography.PrimaryValue) + UiMetrics.Space(2))
                   + (chips.Count == 0 ? 0 : chipRows * (ChipHeight + UiMetrics.Space(6)) + UiMetrics.Space(4));
        var panel = new Rectangle(frame.X, frame.Y, frame.Width, height);
        _ui.PanelQuiet(b, panel);
        s_hunterCardBottom = panel.Bottom;

        // Re-centred on the card that was actually drawn: a card that reflowed taller carries the
        // portrait down its own axis rather than leaving it clinging to the top edge. The SIZE stays
        // the one the text column was measured against, so nothing beside it moves.
        por.Y = panel.Y + (height - por.Height) / 2;
        if (_ui.Assets.GetFirst(Character.PortraitKey, "hunter_portrait") is { } p) b.Draw(p, por, Color.White);
        else if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, por, Color.White);

        // ── Line 1. ──
        var nx = x;
        _ui.TextBig(b, _ui.ShortenBig(name, full, px), nx, y, Bone, px);   nx += _ui.MeasureBig(name, px);
        if (!clsOnLine2)
        {
            _ui.TextBig(b, sep, nx, y, Slate, px);                       nx += _ui.MeasureBig(sep, px);
            _ui.TextBig(b, cls, nx, y, UiKit.ClassColor(Character.Class), px);   nx += _ui.MeasureBig(cls, px);
        }
        if (lvOnLine1) _ui.TextBig(b, lv, nx + lvGap, y + (px - UiTypography.Secondary) / 2 + 1, Slate, UiTypography.Secondary);
        if (powerBeside)
        {
            _ui.TextRightBig(b, "POWER", right, y - 2, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, powerVal, right, y + UiTypography.Pitch(UiTypography.Secondary) - 6, Bone, UiTypography.PrimaryValue);
        }
        y += UiTypography.Pitch(UiTypography.Headline) + UiMetrics.Space(6);
        if (clsOnLine2)
        {
            var cx2 = x;
            _ui.TextBig(b, cls, cx2, y, UiKit.ClassColor(Character.Class), UiTypography.Secondary);
            cx2 += _ui.MeasureBig(cls, UiTypography.Secondary) + lvGap;
            _ui.TextBig(b, lv, cx2, y, Slate, UiTypography.Secondary);
            lvOnLine1 = true;   // said here; the POWER row below need not repeat it
            y += UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(2);
        }

        // ── Line 2. ──

        // ── SHIELD sits ABOVE the pool, never over it. ────────────────────────────────────────────
        // The brief's own test of this line is that "400 health and no shield" must be one glance away
        // from "400 health and 200 shield", and an overlay cannot do that: it hides the number the
        // player is actually watching. So the shield gets its own strip, thinner than the pool because
        // it is the smaller promise, and drawn only for a build that has one — a permanently empty
        // strip on every other build would be noise that means nothing.
        //
        // NOT COLOUR ALONE (§22, §102): steel rather than the pool's red, well under its height, the
        // shield glyph, and the word SHIELD with its figure beside it. A player who cannot separate
        // steel from red still has the geometry, the picture and the label — and all four survive
        // FIGHT EFFECTS being switched off, because none of them is a VFX.
        //
        // THE SAME BAR FAMILY AS HEALTH, in the cold colour (§66). It was hand-drawn fills — a flat
        // steel block with hairline scoring — beside a health bar wearing the game's ornate frame art,
        // which is exactly the "two different voices for one control" this pass exists to remove.
        // ui_bar_mana_* shipped with that family and no screen ever drew it (the game has no mana), so
        // AssetLibrary aliases ui_bar_shield_* onto it and this is one BarArt call like every other bar.
        if (ShieldStripShown)
        {
            var sBar = new Rectangle(x, y - ShieldStripH - UiMetrics.Space(3), barW, ShieldStripH);
            var frac = _replay!.MaxShield <= 0 ? 0f
                     : Math.Clamp(_replay.CurrentShield / (float)_replay.MaxShield, 0f, 1f);
            _ui.BarArt(b, sBar, frac, "shield");

            // GAINED — a quick rim build around the bar (§68), one shot per grant. The bar has just
            // grown; the rim says the growth was a thing the build DID rather than a wave starting.
            // COLD AND TIGHT. Two pixels out in white it was a selection box in a level editor — the
            // same mistake the standing barrier made before its capture settled it — and its top edge
            // ran into the name above. Hugging the frame, in steel, it reads as the bar's own edge
            // lighting up, which is what a barrier thickening looks like.
            var gain = UiMotion.Pulse(ShieldGainKey);
            if (gain > 0f)
                Outline(b, new Rectangle(sBar.X - 1, sBar.Y - 1, sBar.Width + 2, sBar.Height + 2),
                        Color.Lerp(Steel, Color.White, 0.45f) * (0.9f * gain), 2);

            // ABSORBED — a small impact AT THE BARRIER (§69): a bright notch where the fill now ends,
            // so the bar's fall reads as a bite rather than as a decay. Never a screen shake — the
            // champion is not the one taking the blow while this bar has anything in it.
            var absorbed = UiMotion.Pulse(ShieldAbsorbKey);
            if (absorbed > 0f)
            {
                var notchW = Math.Max(2, UiMetrics.Control(3));
                var edge = Math.Clamp(sBar.X + 2 + (int)((sBar.Width - 4) * frac) - notchW / 2,
                                      sBar.X + 1, sBar.Right - 1 - notchW);
                _ui.Fill(b, new Rectangle(edge, sBar.Y + 2, notchW, Math.Max(1, sBar.Height - 4)),
                         Color.Lerp(Steel, Color.White, 0.7f) * absorbed);
            }

            // The GLYPH, then the word AND the figure on one line. Split across the strip and a chip
            // below they read as a column — SHIELD, then 243 / 243 under it — and a player scanning that
            // column has every reason to think the pool's numbers belong to the shield.
            // At Secondary in the house inks — the label was a Caption in an off-palette steel beside
            // a Body-sized health readout, under the floor (huntstates-12). The glyph carries the hue.
            var ink = _replay.CurrentShield > 0 ? Bone : UiInk.Disabled;
            var lx = sBar.Right + barGap;
            var gBox = new Rectangle(lx, sBar.Y + (sBar.Height - shieldGlyph) / 2, shieldGlyph, shieldGlyph);
            if (_ui.Icon(b, "icon_shield", gBox, Color.White * (_replay.CurrentShield > 0 ? 1f : 0.4f)))
                lx = gBox.Right + UiMetrics.Space(5);
            _ui.TextBig(b, _ui.ShortenBig(shieldText, Math.Max(1, right - lx), UiTypography.Secondary), lx,
                        sBar.Y + (sBar.Height - UiTypography.Secondary) / 2 - 1, ink, UiTypography.Secondary);
        }

        var hpBar = new Rectangle(x, y + 2, barW, UiMetrics.Control(26));
        _ui.BarArt(b, hpBar, _replay?.HealthFractionOf(0) ?? 1f, "health");
        _ui.TextBig(b, hpText, hpBar.Right + barGap, y, Bone, UiTypography.Body);
        y += UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(6);

        // ── The POWER row, only when line 1 had no room beside the name: label and figure on one baseline,
        // right-aligned where the figure was; LV on the left if line 1 could not carry it either. ──
        if (!powerBeside)
        {
            var rowH = UiTypography.PrimaryValue;
            var smallY = y + (rowH - UiTypography.Secondary) / 2 + 1;
            if (!lvOnLine1) _ui.TextBig(b, lv, x, smallY, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, powerVal, right, y, Bone, UiTypography.PrimaryValue);
            _ui.TextRightBig(b, "POWER", right - _ui.MeasureBig(powerVal, UiTypography.PrimaryValue) - lvGap, smallY, Slate, UiTypography.Secondary);
            y += UiTypography.Pitch(UiTypography.PrimaryValue) + UiMetrics.Space(2);
        }

        // ── Line 3 — statuses: only the ones this build has, only while they say something. ──
        // NO SHIELD CHIP. The strip above says the word and the figure together; a chip repeating it
        // is a second readout of one fact, and the status row exists for the states that have no bar.
        var sx = x;
        foreach (var (text, ink) in chips)
        {
            var cw = ChipWidth(text);
            if (sx > x && sx + cw > right) { sx = x; y += ChipHeight + UiMetrics.Space(6); }
            sx += Chip(b, sx, y, text, ink, PlateEdge) + chipGap;
        }
    }

    /// <summary>Top-center stage header (region B): region name, current wave, and the conquest progress bar.</summary>
    /// <summary>
    /// Where the EXPEDITION LOG button sits: right of the stage header, UNDER the chrome row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BOTH OF ITS NUMBERS ARE ANCHORS, and each was a literal that failed at 150 %. The y hangs off
    /// <see cref="Game1.ChromeRowBottom"/>: a fixed 40 put the medallion half underneath the GLEAM
    /// capsule, where the capsules are half again as tall and their chain reaches back past 1206.
    /// </para>
    /// <para>
    /// The x then had to leave the page's centred column entirely. Below the capsules is where the
    /// locked-tile refusal lives — 900 px wide, centred, and taller at every profile because its type
    /// scales while its top does not — so a medallion at 1206 was simply covered by it for three
    /// seconds, which is the control the first failure lesson points at. There is no band that holds all three, so
    /// the medallion takes the gap between that toast's right edge and the right-hand column. Its own
    /// edge follows the profile, as a hit target must.
    /// </para>
    /// </remarks>
    internal static Rectangle LogButtonRect
        => new(Game1.LockedToastRight + UiMetrics.Space(16), Game1.ChromeRowBottom + UiMetrics.Space(8),
               UiMetrics.Control(64), UiMetrics.Control(64));

    /// <summary>
    /// The EXPEDITION LOG's own button — the log was reachable only by the L key, which a player who has
    /// not read the help screen does not know exists (playtest 2026-08-25: "put an icon button for it").
    /// </summary>
    /// <remarks>
    /// The <c>icon_log</c> medallion — a sealed scroll in the same round frame the settings gear and the
    /// nav tiles wear — brighter under the mouse and gold while the log is open, with a hover tip that
    /// names the screen and the key. It replaced a page glyph drawn from fills ("the LOG icon looks bad",
    /// playtest 2026-08-26); the medallion reads on its own, so it carries no caption. It calls the same
    /// <see cref="ToggleLog"/> the key does; the host's L handler closes the other overlays first, so the
    /// button is offered only while none of them is up — which the host guarantees by not routing clicks
    /// here under a modal.
    /// </remarks>
    private void DrawLogButton(SpriteBatch b, Point hit)
    {
        var r = LogButtonRect;
        var hot = r.Contains(hit);
        // THE STANDARD STATES, on a custom-drawn control (§25–§29). NORMAL is the bone tint at 52 px;
        // HOVER eases up to 56 over UiMotion.Fast (it was a hard 52→56 step, which reads as a twitch
        // rather than as a lift, and ignored Reduced Motion); PRESSED drops the medallion 2 px and
        // darkens it, exactly as UiKit.Button does, for as long as the mouse is held; SELECTED is gold,
        // because the log is OPEN. There is no disabled state — the log is always reachable.
        var lift = UiMotion.Ease(UiMotion.KeyOf(r), hot ? 1f : 0f);
        var pressed = hot && UiKit.MouseHeld;
        var edge = UiMetrics.Control(52) + (int)MathF.Round(UiMetrics.Control(4) * lift);
        var box = new Rectangle(r.X + (r.Width - edge) / 2,
                                r.Y + (r.Height - edge) / 2 + (pressed ? 2 : 0), edge, edge);
        var tint = _logOpen ? Gold : hot ? Color.White : new Color(0xE0, 0xD8, 0xC8);
        if (pressed) tint = new Color((int)(tint.R * 0.78f), (int)(tint.G * 0.78f), (int)(tint.B * 0.78f), (int)tint.A);
        if (!_ui.Icon(b, "icon_log", box, tint))
        {
            // No medallion on disk: a plain page so the door still shows.
            _ui.Fill(b, box, new Color(0x16, 0x11, 0x10, 0xE0));
            _ui.TextCenterBig(b, "LOG", box.Center.X, box.Center.Y - UiTypography.Caption / 2, tint, UiTypography.Caption);
        }
        // THE TIP IS DEFERRED TO THE TOP OF THE HUD PASS. Drawn here it went under the idle/rewards
        // panel two calls later, and the sentence was cut mid-word — "…every descent's report. The L k".
        // A hover tip that a panel eats is worse than no tip: it says there is more to read and then
        // hides it. It is remembered here and drawn last (see the foot of Draw).
        _logTipAt = hot ? r : null;   // the BUTTON, not the pointer: the tip stands still while you read it
        // NO HIT TEST HERE. LogButtonRect is the one geometry; TakeInput tests this very rectangle.
    }

    /// <summary>Where the LOG button's hover tip is owed this frame, or null — drawn at the top of the HUD pass.</summary>
    private Rectangle? _logTipAt;

    /// <summary>Set by the log button; the host routes it through its own L handling and clears it.</summary>
    public bool WantsLog { get; set; }

    /// <summary>The LOG's ADJUST BUILD door (UX V2 P1.2). The screen closes its log before raising it; the host opens BUILD.</summary>
    public bool WantsBuild { get; set; }

    /// <summary>The LOG's GEAR door. As above; the host opens GEAR.</summary>
    public bool WantsGear { get; set; }

    /// <summary>
    /// The live enemy strip under the stage header: the wave's Source glyph, its kind, its affixes as
    /// chips, how many creatures still stand, and the pack's remaining life as a thin bar. Skipped on a
    /// boss wave — the boss bar and its name are that wave's line.
    /// </summary>
    /// <remarks>
    /// It was one small-caps sentence floating under the header ("SWARM · 4 OF 4 STANDING · 86% LIFE
    /// LEFT"), and it read as a caption that had lost its picture. Now it is a plate of the header's own
    /// width in the rail's brown row style (the CHEST FILTER row's), so the header and the strip read as
    /// one stack: where, when, how far — and, underneath, what. The life is a bar, not a percentage in
    /// words; the count keeps its words because "4 OF 4" alone does not say what is being counted.
    /// </remarks>
    /// <summary>The distinct Sources this wave's creatures carry, in slot order — refilled by <see cref="WaveSources"/>.</summary>
    private readonly List<Source> _waveSources = new(6);

    /// <summary>
    /// Fill <see cref="_waveSources"/> with the Sources the wave's creatures really carry (Core's roll),
    /// or the fallback Source when none of them carries one. No allocation per frame.
    /// </summary>
    private void WaveSources()
    {
        _waveSources.Clear();
        if (DevEnemySource is { } posed) { _waveSources.Add(posed); return; }
        var comp = _run?.LastWaveCreatures;
        if (comp is not null)
            for (var i = 0; i < comp.Count; i++)
                if (comp[i].Source is { } s && !_waveSources.Contains(s)) _waveSources.Add(s);
        if (_waveSources.Count == 0 && ArtSource is { } fallback) _waveSources.Add(fallback);
    }

    private static readonly Dictionary<Source, string> SourceGlyphKeys = new()
    {
        [Source.Body] = "source_body", [Source.Mind] = "source_mind", [Source.Nature] = "source_nature",
        [Source.Machine] = "source_machine", [Source.Shadow] = "source_shadow", [Source.Spirit] = "source_spirit",
    };

    /// <summary>The Source medallion's asset key, without building a string per frame.</summary>
    private static string SourceGlyphKey(Source s) => SourceGlyphKeys[s];

    private void DrawEnemyLine(SpriteBatch b)
    {
        if (_replay is null || _run is null || _isBossWave) return;
        var strip = EnemyStrip;
        // THE HOUSE PLATE with the wave's Source as its accent — it was a hand-drawn fill with a 2 px
        // bronze outline no other surface wears, so the one QUIET surface in the header stack competed
        // with the frame above it instead of sitting under it (release polish 2026-09-05, hunt-06).
        WaveSources();
        _ui.Plate(b, strip, _waveSources.Count > 0 ? SourceGlow(_waveSources[0]) : null);
        HeaderStackBottom = strip.Bottom;

        // LEFT: the Sources the wave really carries, then its kind. Core rolls each creature's Source
        // from the region's roster, so a pack can hold two or three; the strip used to show the
        // region's theme alone, which was true of the body (one per Source) and of nothing else — and
        // since 2026-09-16 the body does not say it either. Everything on the strip is centred on its
        // height, which is the Body line plus its pads — so a taller profile's strip keeps its middle line.
        var glyphEdge = UiMetrics.Control(30);
        var x = strip.X + 5 + UiMetrics.Space(12);
        WaveSources();
        foreach (var ws in _waveSources)
        {
            var glyph = new Rectangle(x, strip.Y + (strip.Height - glyphEdge) / 2, glyphEdge, glyphEdge);
            if (_ui.Assets.Get(SourceGlyphKey(ws)) is { } g) b.Draw(g, glyph, Color.White);
            else
            {
                var inset = glyphEdge * 6 / 30;   // the diamond's proportion of its box
                _ui.Diamond(b, new Rectangle(glyph.X + inset, glyph.Y + inset, glyphEdge - inset * 2, glyphEdge - inset * 2), SourceGlow(ws));
            }
            x = glyph.Right + UiMetrics.Space(4);
        }
        x += UiMetrics.Space(6);
        // The laid-out role, which is the run's own roll outside the capture rig (WaveArchetype), so the
        // label always names the bodies drawn beside it.
        var kind = WaveArchetype.ToString().ToUpperInvariant();
        _ui.TextBig(b, kind, x, strip.Y + (strip.Height - UiTypography.Body) / 2 + 1, UiKit.Vellum, UiTypography.Body);
        x += _ui.MeasureBig(kind, UiTypography.Body) + UiMetrics.Space(10);

        // RIGHT: how many still stand. (The pack's life bar that stood here was "unnecessary" — every
        // creature wears its own pip, playtest 2026-08-28.)
        var total = _replay.CreatureCount;
        var alive = 0;
        for (var i = 0; i < total; i++) if (_replay.CreatureAlive(i)) alive++;
        var countLeft = strip.Right - UiMetrics.Space(16);
        if (total > 0)
            countLeft = RunsRight(b, countLeft, strip.Y + (strip.Height - UiTypography.Secondary) / 2 + 1, UiTypography.Secondary, ($"{alive} OF {total}", Bone), ("  STANDING", Slate));

        // BETWEEN: the affixes as chips, as many as fit; the rest fold into a "+N" chip.
        var affixes = _run.LastWaveAffixes ?? Array.Empty<Affix>();
        var limit = countLeft - UiMetrics.Space(16);
        var chipY = strip.Y + (strip.Height - ChipHeight) / 2 + 1;
        var chipGap = UiMetrics.Space(6);
        for (var i = 0; i < affixes.Count; i++)
        {
            var text = affixes[i].ToString().ToUpperInvariant();
            var rest = affixes.Count - i;
            var more = rest > 1 ? ChipWidth($"+{rest - 1}") + chipGap : 0;
            if (x + ChipWidth(text) + more > limit)
            {
                if (x + ChipWidth($"+{rest}") <= limit) Chip(b, x, chipY, $"+{rest}", Bone, PlateEdge);
                break;
            }
            x += Chip(b, x, chipY, text, Bone, PlateEdge) + chipGap;
        }
    }

    // ── The stage header's geometry (630, 18, 560, 135) at 100 %: derived line by line from the rungs it
    //    stacks — the region title, the wave line, the conquest bar — so a bigger profile lengthens the
    //    header rather than printing the wave over the title. The enemy strip and the boss bar hang from
    //    its foot. The x and width are page anchors and stay. ──
    private const int StageHeaderCentreX = 910;
    /// <summary>What joins the wave to the run's state on ONE row. Measured and drawn from here alone.</summary>
    private const string WaveJoin = "  —  ";
    private const int StageHeaderTop = 18;
    /// <summary>Where the region title sits: a breath under the frame's top.</summary>
    private static int StageHeaderTitleY => StageHeaderTop + UiMetrics.Space(10);
    /// <summary>The wave line, one title under the title. 70 at 100 %.</summary>
    private static int StageHeaderWaveY => StageHeaderTitleY + UiTypography.RegionTitle + UiMetrics.Space(4);
    /// <summary>
    /// How many rows the wave line is spending this frame — 1 normally, 2 while the run's state is
    /// beside it and the pair will not fit the header's own content width.
    /// </summary>
    /// <remarks>
    /// A per-frame mirror in the shape of <c>s_headerStackBottom</c> and <c>s_hunterCardBottom</c>:
    /// <see cref="DrawStageHeader"/> decides it before it reads <see cref="StageHeader"/>, and
    /// everything hung off the header's foot follows for free. The frame's padding does not change
    /// with the extra row — 560 wide stays past <c>UiTypography.WidePanelFrom</c> and every height it
    /// can take keeps the same panel art key — so <see cref="StageHeaderRoom"/> cannot feed back into
    /// the decision that sets this.
    /// </remarks>
    private static int s_waveRows = 1;

    /// <summary>The wave lane's height: one rung, plus a full line for each extra row.</summary>
    private static int StageHeaderWaveH
        => UiTypography.StageLabel + (s_waveRows - 1) * UiTypography.Pitch(UiTypography.StageLabel);

    /// <summary>The header's usable line width — its own content rails, 480 at every profile.</summary>
    private static int StageHeaderRoom => UiKit.ContentRight(StageHeader) - UiKit.ContentLeft(StageHeader);

    /// <summary>
    /// Does a wave line of this width need a second row?
    /// </summary>
    /// <remarks>
    /// Pure, so the rule can be tested without a font. The wave line is the ONE line in this header
    /// with no fit ladder and no clamp: the region title above it steps its rung down until it fits,
    /// and the conquest row below it reserves its bar around a measured label, but the wave line was
    /// simply centred on 910 and drawn. At 150 % its longest form — the wave, a corruption tier's name
    /// and BOSS WAVE — is measured at the full text rate while 910, 630, 560 and the panel's padding are
    /// all literals, so it grew past both of the header's rails and printed itself over the hunter's
    /// health readout on the left and under the log medallion on the right.
    /// </remarks>
    internal static int WaveRowsFor(int lineWidth, int room) => lineWidth > room ? 2 : 1;

    /// <summary>The conquest bar's line, under the wave lane. 106 at 100 %.</summary>
    private static int StageHeaderBarY => StageHeaderWaveY + StageHeaderWaveH + UiMetrics.Space(8);
    /// <summary>The conquest bar's height.</summary>
    private static int StageHeaderBarH => UiMetrics.Control(16);
    /// <summary>The header's bottom edge — the enemy strip hangs from it. 153 at 100 %.</summary>
    private static int StageHeaderBottomY => StageHeaderBarY + StageHeaderBarH + UiMetrics.Space(31);
    /// <summary>The header's frame.</summary>
    private static Rectangle StageHeader => new(630, StageHeaderTop, 560, StageHeaderBottomY - StageHeaderTop);

    /// <summary>The enemy strip's height: the Body line it carries and its pads. 44 at 100 %.</summary>
    private static int EnemyStripHeight => UiMetrics.Space(12) + UiTypography.Body + UiMetrics.Space(10);

    /// <summary>The enemy strip: the header's width, a few pixels under its frame.</summary>
    private static Rectangle EnemyStrip => new(630, StageHeaderBottomY + UiMetrics.Space(7), 560, EnemyStripHeight);

    /// <summary>
    /// Where the header stack ends this frame — the enemy strip's foot, the boss bar's, or the header's
    /// own — so the host can hang its transient toasts under it rather than across it.
    /// </summary>
    public int HeaderStackBottom { get => _headerStackBottom; private set => s_headerStackBottom = _headerStackBottom = value; }
    private int _headerStackBottom = StageHeaderBottomY;
    /// <summary>A static mirror for the static <see cref="Spotlights"/>, like <c>s_railBottom</c>.</summary>
    private static int s_headerStackBottom = StageHeaderBottomY;

    private void DrawStageHeader(SpriteBatch b, string regionName, bool isBossWave)
    {
        // Rev 3 §12: stage header (630,18,560,135) — narrower, so it clears the currency bar (≥20px gap),
        // and inside the tour's header spotlight (620,8,580,152). ONE HIERARCHY, top to bottom: WHERE (the
        // region, in the display face), WHEN (the wave, and the run's state beside it), HOW FAR (the
        // conquest, a small label and a thin bar on one line). WHAT is being fought hangs under the panel
        // in DrawEnemyLine at the same width, so the two read as one stack (playtest 2026-08-28: "the wave
        // information texts look quite bad" — three lines of three sizes with a bar between two of them).
        // ── THE WAVE LANE IS MEASURED BEFORE THE FRAME IS DRAWN, because the frame's height depends
        //    on the answer. One row when the line fits the header's rails; two when the run's state
        //    makes it too long, which is what a fall does at the larger profiles.
        var wave = $"WAVE {Math.Max(1, _replayWave)}";
        if (CorruptionTier > 0) wave += $"  ·  {CorruptionLook.For(CorruptionTier).Name}";
        var waveTint = isBossWave ? Gold : Bone;
        var state = RunState();
        var room = StageHeaderRoom;
        s_waveRows = state is { } probe
            ? WaveRowsFor(RunsWidth(UiTypography.StageLabel, (wave, waveTint), (WaveJoin, Slate), (probe.Text, probe.Tint)), room)
            : WaveRowsFor(_ui.MeasureBig(wave, UiTypography.StageLabel), room);

        var bar = StageHeader;
        // The quiet frame, like every other panel on this screen (UiKit.PanelQuiet: gold is for modals).
        _ui.PanelQuiet(b, bar);
        const int cx = StageHeaderCentreX;
        // §19.2: render the region title at its rung, shrinking to the panel-title rung to fit 490px — never
        // ellipsize the ACTIVE region title. (A two-line fallback below that is a noted follow-up; region
        // names fit.) The floor is a rung, so it follows the profile with the title.
        var title = regionName.ToUpperInvariant();
        var titlePx = UiTypography.RegionTitle;
        while (titlePx > UiTypography.PanelTitle && _ui.MeasureBig(title, titlePx) > 490) titlePx--;
        _ui.TextCenterBig(b, title, cx, StageHeaderTitleY + (UiTypography.RegionTitle - titlePx) / 2, Gold, titlePx, TextFace.Display);

        // THE WAVE LINE CARRIES THE RUN'S STATE, which is what the deleted EXPEDITION plate was for.
        // Nothing is appended while the run is simply running: "ACTIVE" was true of every frame this
        // screen has ever drawn, so it distinguished nothing and only made the line longer.
        //
        // TWO ROWS WHEN ONE WILL NOT HOLD IT, and the line break becomes the separator — the dash is
        // what joins two halves of one row, so a second row does not need it. Nothing shrinks and
        // nothing is hidden: the header's own stack grows and everything under it moves down with it,
        // which is exactly what the geometry block above this method promises.
        var waveY = StageHeaderWaveY;
        if (state is { } st && s_waveRows == 1)
            RunsCenter(b, cx, waveY, UiTypography.StageLabel, (wave, waveTint), (WaveJoin, Slate), (st.Text, st.Tint));
        else if (state is { } st2)
        {
            _ui.TextCenterBig(b, wave, cx, waveY, waveTint, UiTypography.StageLabel);
            // THE LAST RESORT, on the second row only: a state that still overruns a whole row of its
            // own steps DOWN THE RUNG LADDER rather than losing a word.
            var statePx = _ui.FitRung(st2.Text, room, UiTypography.StageLabel);
            _ui.TextCenterBig(b, st2.Text, cx, waveY + UiTypography.Pitch(UiTypography.StageLabel)
                                              + (UiTypography.StageLabel - statePx) / 2, st2.Tint, statePx);
        }
        else _ui.TextCenterBig(b, wave, cx, waveY, waveTint, UiTypography.StageLabel);

        // CONQUEST, not DEPTH: "depth" was three different things across the UI (this count of waves
        // toward the conquest, a region's best depth, and the corruption tier). This is the conquest.
        // Past the conquest bar the banner counts OVERWAVE — how far beyond the bar this descent has gone
        // (playtest 2026-08-26: "after the map is conquered, mark it with something like Overwave").
        // The label and the bar share one line, centred as a group: the goal is the quietest fact here.
        var over = Math.Max(0, _replayWave - ConquerAt);
        var conquered = RegionConquered || Deepest >= ConquerAt;
        var label = conquered
            ? new[] { (over > 0 ? $"CONQUERED  ·  OVERWAVE +{over}" : "CONQUERED", Gold) }
            : new[] { ("CONQUEST  ", Slate), ($"{Deepest} / {ConquerAt}", Bone) };
        // The bar keeps the house 240 where the label leaves it room, and yields to a longer label at a
        // larger profile rather than pushing the group past the frame's side rails.
        var gap = UiMetrics.Space(16);
        var barH = StageHeaderBarH;
        var barY = StageHeaderBarY;
        var labelW = RunsWidth(UiTypography.Secondary, label);
        var barW = Math.Max(UiMetrics.Space(80), Math.Min(UiMetrics.Space(240), bar.Width - UiTypography.PanelPadNarrow * 2 - labelW - gap));
        var groupX = cx - (labelW + gap + barW) / 2;
        Runs(b, groupX, barY - 1, UiTypography.Secondary, label);
        _ui.BarArt(b, new Rectangle(groupX + labelW + gap, barY, barW, barH),
            conquered ? 1f : ConquerAt > 0 ? Math.Clamp(Deepest / (float)ConquerAt, 0f, 1f) : 0f, "progress");
    }

    // ── Small typographic helpers for the header stack and the log. ─────────────────────────────

    /// <summary>Draws runs of text in different colours on one line; returns the x after the last one.</summary>
    private int Runs(SpriteBatch b, int x, int y, int px, params (string Text, Color Tint)[] runs)
    {
        foreach (var (t, c) in runs)
        {
            _ui.TextBig(b, t, x, y, c, px);
            x += _ui.MeasureBig(t, px);
        }
        return x;
    }

    private int RunsWidth(int px, params (string Text, Color Tint)[] runs) => runs.Sum(r => _ui.MeasureBig(r.Text, px));

    private void RunsCenter(SpriteBatch b, int cx, int y, int px, params (string Text, Color Tint)[] runs)
        => Runs(b, cx - RunsWidth(px, runs) / 2, y, px, runs);

    /// <summary>Runs ending at a right edge; returns the x they start at.</summary>
    private int RunsRight(SpriteBatch b, int right, int y, int px, params (string Text, Color Tint)[] runs)
    {
        var x = right - RunsWidth(px, runs);
        Runs(b, x, y, px, runs);
        return x;
    }

    /// <summary>A small labelled chip — a dark plate, a one-pixel edge, the word inside. Returns its width.</summary>
    private int Chip(SpriteBatch b, int x, int y, string text, Color ink, Color edge,
                     int px = 0)
    {
        if (px == 0) px = UiTypography.Caption;   // a rung is a profile-scaled property, not a constant
        var w = ChipWidth(text, px);
        var r = new Rectangle(x, y, w, px + UiTypography.ChipPadY * 2);
        // A chip is the house plate; only a chip with its OWN edge colour (NEW RECORD's gold) draws a
        // rule over it — the bronze PlateEdge was the plate's imitation and is the plate now
        // (release polish 2026-09-05, hunt-06).
        _ui.Plate(b, r);
        if (edge != PlateEdge) Outline(b, r, edge, 1);
        _ui.TextBig(b, text, x + UiTypography.ChipPadX, y + UiTypography.ChipPadY, ink, px);
        return w;
    }

    private int ChipWidth(string text, int px = 0)   // 0 = Caption: a rung is a profile-scaled property, not a constant
        => _ui.MeasureBig(text, px == 0 ? UiTypography.Caption : px) + UiTypography.ChipPadX * 2;

    private void Hairline(SpriteBatch b, int x, int y, int w, Color c) => _ui.Fill(b, new Rectangle(x, y, w, 1), c);

    private static readonly Color PlateEdge = new(0x74, 0x62, 0x3E);

    /// <summary>How tall the shield strip is — well under the life bar's 26, because it is the smaller promise.</summary>
    /// <remarks>
    /// 16, not the 12 it was drawn at while it was a flat fill. The strip is <see cref="UiKit.BarArt"/>
    /// now — the same ornate frame the health bar wears, in the cold colour — and that frame is 256×64
    /// art whose scrollwork simply disappears under 14 px. 16 against the pool's 26 still reads as
    /// "thinner" at a glance, which is the cue the colour-blind path depends on.
    /// </remarks>
    private static int ShieldStripH => UiMetrics.Control(16);

    /// <summary>
    /// Whether this build has any shield at all, and so whether the strip is drawn.
    /// </summary>
    /// <remarks>
    /// True from the moment a wave grants shield and for the rest of THIS run, rather than only while
    /// some is standing: a strip that appears and vanishes as bites land is a flicker, and a player
    /// cannot learn the shape of a bar they only see in the instants it is full. A build with no shield
    /// mechanic never sees it at all.
    /// <para>
    /// <b>It resets when a run does</b> (see <c>StartRun</c>). It used to be a one-way latch for the life
    /// of the process, so a champion that fell holding a shield came back for its next descent with an
    /// empty steel strip already on the card — the bar arrived before the mechanic that fills it, and the
    /// SHIELD BROKEN moment that teaches what the bar is for had nothing left to introduce.
    /// </para>
    /// </remarks>
    private bool ShieldStripShown => _shieldSeen && _replay is not null;

    private bool _shieldSeen;

    /// <summary>FIXTURE ONLY: the posed shield flare has been fired, so it is not re-fired every tick.</summary>
    private bool _shieldFxPosed;

    /// <summary>
    /// FIXTURE ONLY: the frame rate a posed shield flare is played at.
    /// </summary>
    /// <remarks>
    /// Eight frames at half a frame a second is sixteen seconds of clip, so the shutter — which opens
    /// about a second into the wave — always finds the burst on its first frame at full brightness,
    /// whatever the replay was doing. It is a shutter speed, not a gameplay value; nothing reads it
    /// without <c>RH_SHOT_SHIELDFX</c>.
    /// </remarks>
    private const float PosedFxFps = 0.5f;

    /// <summary>
    /// The motion keys for the shield bar's two one-shots: a rim on a grant, a notch on an absorb.
    /// </summary>
    /// <remarks>
    /// Constants rather than <see cref="UiMotion.KeyOf"/> of the bar's rectangle, because that rectangle
    /// MOVES — the hunter card reflows (a wrapped name row, a second chip row, any of the three density
    /// profiles) and the bar slides with it. A pulse keyed to a rect that moves mid-flight is a pulse
    /// that is silently dropped halfway through.
    /// </remarks>
    private static readonly int ShieldGainKey = HashCode.Combine("hunt.shield.gain");
    private static readonly int ShieldAbsorbKey = HashCode.Combine("hunt.shield.absorb");

    /// <summary>One skill tile's cast pulse, keyed by the slot the Skill event names.</summary>
    private static int SkillCastKey(int slot) => HashCode.Combine("hunt.skill.cast", slot);

    /// <summary>The one-shot armed the instant a slot crosses into READY — the pop and the unwinding band.</summary>
    private static int SkillReadyKey(int slot) => HashCode.Combine("hunt.skill.ready", slot);

    /// <summary>Was each slot ready last frame? The edge, not the state, is what arms the pop.</summary>
    private readonly Dictionary<int, bool> _railReady = new();

    /// <summary>Which notch each slot's ring last stood on, so a crossing can be told from a hold.</summary>
    private readonly Dictionary<int, int> _railStep = new();

    /// <summary>The eased ring position, so a beat-counted notch travels instead of teleporting.</summary>
    private static int SkillRingKey(int slot) => HashCode.Combine("hunt.skill.ring", slot);

    /// <summary>
    /// One notch crossed — the tick that makes a long wait feel like it is counting down.
    /// </summary>
    /// <remarks>
    /// A six-beat skill spends four and a half seconds getting ready and used to report that with three
    /// silent jumps. Each notch it passes now says so, which turns a dead band into a countdown the eye
    /// can follow without reading the word beside it.
    /// </remarks>
    private static int SkillStepKey(int slot) => HashCode.Combine("hunt.skill.step", slot);

    /// <summary>The ready burst's own key — a ring that leaves the medallion, longer-lived than the pop.</summary>
    private static int SkillBurstKey(int slot) => HashCode.Combine("hunt.skill.burst", slot);

    // ── The right UTILITY (UX V2 P1.1, brief §20): idle rate · rewards · doors. Lightweight. ───────────
    //    The CHEST FILTER row and its popover moved to the VAULT toolbar (D12): chest filtering is inventory
    //    management, not combat state. DEEPEST WAVE REACHED went too — the header's CONQUEST n / 20 is the same
    //    number. The doors name no keys in their labels; the tour and the help sheet teach the keys.
    private const int ColumnX = 1570, ColumnW = 326;   // page anchors: the column's place beside the arena
    private const int UtilityTop = 110;
    /// <summary>The column's inset, at the profile — it was the bare 28, so at 150 % the door's foot sat in the frame's bottom band (release polish 2026-09-05, hunt-02).</summary>
    private static int UtilityPad => UiMetrics.Space(UiTypography.PanelPadNarrow);
    /// <summary>A door button's height and the step to the next — controls, at the profile. 44 / 52 at 100 %.</summary>
    private static int DoorHeight => UiMetrics.Control(44);
    private static int DoorPitch => DoorHeight + UiMetrics.Space(8);

    /// <summary>The utility's bottom edge as last drawn (+ margin) — the right column's true extent, for the tour.</summary>
    private static int s_railBottom = 368;

    /// <summary>
    /// The right rail's measured layout — the panel, its text anchors, and its one door.
    /// </summary>
    /// <remarks>
    /// ONE GEOMETRY, READ BY BOTH HALVES: <see cref="DrawRightColumn"/> paints from it and
    /// <see cref="TakeRailInput"/> hit-tests the same <c>PointsDoor</c>. The whole vertical walk lives
    /// in <see cref="MeasureRail"/> so the paint does no layout arithmetic of its own and the door
    /// cannot drift away from the rows above it.
    /// </remarks>
    private readonly record struct RailPlan(
        Rectangle Panel, int X, int W,
        bool ChestRow, bool PointsRow, bool TwoLines,
        string Chests, string Points, string Both,
        int IdleLabelY, int IdleValueY, int RewardsLabelY, int RewardLineY,
        Rectangle PointsDoor);

    private RailPlan MeasureRail()
    {
        var chestRow = ChestCount > 0 && VaultOpen;
        var pointsRow = Mastery.Available > 0 && MasteryOpen;
        var doors = pointsRow ? 1 : 0;   // the chest's door is the VAULT tile now — see DrawRightColumn

        var top = UiTypography.PanelTitleTop;
        var idleBlock = UiTypography.Pitch(UiTypography.Secondary) + UiTypography.Pitch(UiTypography.PrimaryValue);
        // THE REWARDS LINE SPLITS IN TWO when the two facts do not share a line at this profile — at
        // 150 % "1 CHEST READY · 7 POI…" was ellipsised over the two doors that say the same facts
        // (huntstates-03). The panel is summed from its blocks, so the door moves down with it.
        var chests = $"{ChestCount} CHEST{(ChestCount == 1 ? "" : "S")} READY";
        var points = $"{Mastery.Available} MASTERY POINT{(Mastery.Available == 1 ? "" : "S")}";
        var both = chestRow && pointsRow ? $"{chests} · {Mastery.Available} POINT{(Mastery.Available == 1 ? "" : "S")}" : "";
        var lineW = ColumnW - UtilityPad * 2;
        var twoLines = both.Length > 0 && _ui.MeasureBig(both, UiTypography.Body) > lineW;
        var rewardBlock = UiTypography.Pitch(UiTypography.Secondary) + UiTypography.Pitch(UiTypography.Body) * (twoLines ? 2 : 1);
        var doorBlock = doors > 0 ? UiMetrics.Space(8) + doors * DoorPitch - (DoorPitch - DoorHeight) : 0;
        var panel = new Rectangle(ColumnX, UtilityTop, ColumnW, top + idleBlock + UiMetrics.Space(10) + rewardBlock + doorBlock + UtilityPad);

        var x = panel.X + UtilityPad;
        var w = panel.Width - UtilityPad * 2;
        var idleLabelY = panel.Y + top;
        var idleValueY = idleLabelY + UiTypography.Pitch(UiTypography.Secondary);
        var rewardsLabelY = idleValueY + UiTypography.Pitch(UiTypography.PrimaryValue) + UiMetrics.Space(10);
        var rewardLineY = rewardsLabelY + UiTypography.Pitch(UiTypography.Secondary);
        var doorY = rewardLineY + UiTypography.Pitch(UiTypography.Body) * (twoLines ? 2 : 1) + UiMetrics.Space(8);

        return new RailPlan(panel, x, w, chestRow, pointsRow, twoLines, chests, points, both,
                            idleLabelY, idleValueY, rewardsLabelY, rewardLineY,
                            pointsRow ? new Rectangle(x, doorY, w, DoorHeight) : Rectangle.Empty);
    }

    /// <summary>The rail's one errand: SPEND n POINTS. Decided here, painted in <see cref="DrawRightColumn"/>.</summary>
    private void TakeRailInput(Point mouse, bool clicked)
    {
        var p = MeasureRail();
        if (p.PointsRow && UiKit.ClickedIn(p.PointsDoor, mouse, clicked)) WantsMastery = true;
    }

    /// <summary>Right utility: the idle rate, what is waiting, and the door to it.</summary>
    /// <remarks>
    /// A reward whose screen is locked is hidden — not greyed, not clickable-into-a-refusal. A door this
    /// column advertises must open; until the host says the screen is unlocked, the errand is not offered.
    /// It lays nothing out itself: every anchor and the door's rectangle come from
    /// <see cref="MeasureRail"/>, which <see cref="TakeRailInput"/> reads too.
    /// </remarks>
    private void DrawRightColumn(SpriteBatch b, Point hit)
    {
        var p = MeasureRail();
        // PINNED to the medium frame: this panel's height follows the profile while its width is a page
        // anchor, so its aspect crossed 1.30 at 125 % and it alone switched to the crested square frame
        // beside three siblings that did not (release polish 2026-09-05, hunt-02).
        _ui.PanelQuiet(b, p.Panel, artKey: "ui_panel_medium");
        s_railBottom = p.Panel.Bottom + 10;
        _utilityPanel = p.Panel;   // the inspector, drawn after this column, keeps clear of it

        _ui.TextBig(b, "IDLE", p.X, p.IdleLabelY, Slate, UiTypography.Secondary);
        var gem = UiMetrics.Control(30);   // the gleam icon beside the rate — an icon box, at the profile
        if (_ui.Assets.Get("currency_gleam") is { } gi) b.Draw(gi, new Rectangle(p.X, p.IdleValueY + UiMetrics.Space(5), gem, gem), Color.White);
        _ui.TextBig(b, $"+{Game1.Abbrev((long)(IdleGleamRate * 60f))}/min", p.X + gem + UiMetrics.Space(10), p.IdleValueY, Bone, UiTypography.PrimaryValue);

        _ui.TextBig(b, "REWARDS", p.X, p.RewardsLabelY, Slate, UiTypography.Secondary);
        // NOTHING WAITING in Slate, not the Empty ink: a sentence under the contrast floor with no
        // shape beside it read as disabled text (release polish 2026-09-05, hunt-11).
        if (p.TwoLines)
        {
            _ui.TextBig(b, _ui.ShortenBig(p.Chests, p.W, UiTypography.Body), p.X, p.RewardLineY, Bone, UiTypography.Body);
            _ui.TextBig(b, _ui.ShortenBig(p.Points, p.W, UiTypography.Body), p.X, p.RewardLineY + UiTypography.Pitch(UiTypography.Body), Bone, UiTypography.Body);
        }
        else
        {
            var line = p.Both.Length > 0 ? p.Both : p.ChestRow ? p.Chests : p.PointsRow ? p.Points : "NOTHING WAITING";
            _ui.TextBig(b, _ui.ShortenBig(line, p.W, UiTypography.Body), p.X, p.RewardLineY, p.PointsRow ? Bone : Slate, UiTypography.Body);
        }

        // THE VAULT DOOR IS GONE FROM HERE, and it is the rail tile now.
        //
        // A chest waiting had TWO doors in this game — a button in this column and the VAULT tile on
        // the rail, which carries its own count badge — and the authored opening has to point at one
        // of them. The tile is the one that is always there, on every screen, at every depth, in the
        // place the player will look for it for the rest of the game; a button that exists only on
        // this screen and only while a chest happens to be waiting teaches nothing they can reuse.
        // Two doors to one room also meant the teaching had to name a place ("the right column") that
        // stops existing the moment the chest is opened. The line above still SAYS a chest is ready.
        //
        // PAINTED WITH clicked: false — the pressed face comes from the static UiKit.MouseHeld, so this
        // is visually identical to the old form and can no longer fire. TakeRailInput decides it.
        // ON ONE LINE on purpose: the draw-purity gate judges a widget by its edge ARGUMENT, and a call
        // split across lines cannot be read from one line, so it is reported rather than cleared.
        var spend = $"SPEND {Mastery.Available} POINT{(Mastery.Available == 1 ? "" : "S")}";
        if (p.PointsRow) _ui.Button(b, p.PointsDoor, spend, hit, false, true, ButtonStyle.Primary);
    }

    // ── The death transition (attention ownership pass, 2026-09-15). ─────────────────────────────────────
    //    A fall is part of the idle loop, so it is not announced. The Hunter is seen to fall (the clip, the
    //    flash); the stage fades to black over the last DeathTransition.FadeOut of the downed beat; the next
    //    descent begins at full black on the frame the beat runs out; the black holds for DeathTransition.Hold
    //    and lifts over DeathTransition.FadeIn while the new wave walks in. The arithmetic is DeathTransition
    //    (PresentationBeats.cs); this screen owns the two clocks — _downedTimer and the lift below — and
    //    paints ONE full-canvas fill from them at the foot of Draw, under the host's chrome; its own HUD
    //    refuses input, and parks the cursor for its own hover faces and tips, exactly while that fill is
    //    visible (BlackCovers), and is live at every other frame.
    //    Under Reduced Motion both fades are cuts. No plate, no door, no header line: the wave lane shows
    //    the wave that fell until the restart and the new wave after it, and the report waits in the LOG.

    /// <summary>Seconds left of the lift after a restart — the hold, then the fade. Zero while the stage is visible.</summary>
    /// <remarks>
    /// Armed on the restart frame (Update's Downed case) and decayed there, zeroed by travel and the moment
    /// another screen is on top (<see cref="TakeInput"/>), held by <see cref="DevPoseDeathTransition"/>.
    /// </remarks>
    private float _deathFadeIn;

    /// <summary>Where this screen's HUD is told the cursor is while the black covers it: nowhere, so nothing hovers or tips under the black.</summary>
    private static readonly Point OffCanvas = new(-4096, -4096);

    /// <summary>The run's state, or null when it is simply running and there is nothing to say.</summary>
    /// <remarks>
    /// Was the right half of an EXPEDITION plate whose left half repeated the banner's wave number. The
    /// word it printed most often was "ACTIVE" — true of every frame the screen is drawn in, so it
    /// distinguished nothing and cost a whole plate to say. Only the one state that means something now
    /// reaches the player, and it reaches them on the banner beside the wave it describes. A fall says
    /// nothing here: the lane is state, not an announcement (see the death transition above).
    /// </remarks>
    private (string Text, Color Tint)? RunState()
    {
        if (DevBossCall || WaveScaling.IsBossWave(_run!.Wave + 1, ExpeditionTuning.Default)) return ("BOSS WAVE", Gold);
        return null;
    }

    // ── The SKILL STRIP (UX V2 P1.1, brief §17/§18) ─────────────────────────────────────────────────────
    //    ACTIVE and PASSIVE groups across the foot of the stage, on a QUIET plate. The vertical SKILLS rail
    //    it replaces was a 286×592 ornate frame down the left of the arena that printed each skill's rule
    //    text (BUILD's job) at 9 px, and boxed the stage on a third side. A slot now says: glyph · name ·
    //    readiness word · Source — what a player reads at a glance in a fight, nothing they read once.
    //    (486, 900, 1000, 164) at 100 %. DERIVED since the density profile (UI polish P2): the strip is
    //    anchored to the page's foot and grows UPWARD by the type it holds — the group caption, then a slot
    //    that is a Headline name over a Body line — and the arena above it yields the room (GroundY). It
    //    widens at the control rate too, centred where it was, so the slot text keeps its length at 150 %
    //    rather than shortening every skill's name; the arena clip is wider still, so it never leaves it.
    //    HUNT never scrolls (brief §18): the strip is as tall as its content, and the stage gives way.
    private static Rectangle SkillStrip => new(SkillStripCentreX - SkillStripWidth / 2, UiKit.PageBottom(16) - SkillStripHeight, SkillStripWidth, SkillStripHeight);
    private const int SkillStripCentreX = 986;
    private static int SkillStripWidth => UiMetrics.Control(1000);
    private static int SkillStripHeight => UiMetrics.Space(8) + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(2) + SlotH + UiMetrics.Space(16);
    private static int StripPad => UiMetrics.Space(12);
    private static int StripGroupGap => UiMetrics.Space(28);
    private static int SlotGap => UiMetrics.Space(10);
    /// <summary>The skill medallion's edge — an icon box, at the profile.</summary>
    private static int MedallionPx => UiMetrics.Control(72);
    /// <summary>A slot's height: the name over the readiness line with their pads, and never less than the medallion needs. 114 at 100 %.</summary>
    private static int SlotH => Math.Max(MedallionPx + UiMetrics.Space(20),
        UiMetrics.Space(16) + UiTypography.Pitch(UiTypography.Headline) + UiTypography.Pitch(UiTypography.Body) + UiMetrics.Space(36));

    /// <summary>What the strip knows about one skill's timing this frame, read off the resolved wave.</summary>
    private readonly record struct SkillTiming(float Ready, float Swept, int RingSteps, float Telegraph, float Flash, int NextMs);

    /// <summary>The dock as it was last drawn — its content's own extent — for the tour's light.</summary>
    private static Rectangle s_dockRect = SkillStrip;

    /// <summary>The skill dock as last drawn — what a tour card must keep off (canvas space).</summary>
    internal static Rectangle DockRect => s_dockRect;

    /// <summary>A slot's width in the dock: room for the medallion and a Headline name beside it. 236 at 100 %.</summary>
    private static int DockSlotW => UiMetrics.Control(236);

    /// <summary>
    /// The ACTIVE / PASSIVE dock — the fight's own skills, grouped by whether they take a beat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NO PLATE, AND NO EMPTIES (release polish 2026-09-05).</b> The dock used to be a 1000×164 dark
    /// plate across the foot of the arena with a slot drawn for every unlocked capacity, so a new hunter
    /// with one skill fought under a huge dark rectangle holding one medallion and three EMPTY SLOT
    /// notes — "sonradan yapıştırılmış koyu panel". The skills are drawn straight on the scene now, as
    /// wide as they are and centred where the strip was, and an empty slot is not drawn at all: the
    /// BUILD screen's hint and the rail's NEW mark already say a slot is waiting, and the fight is not
    /// where a slot is filled.
    /// </para>
    /// <para>
    /// What keeps the words legible over a lit floor is a SOFT SHADOW under the group — a stack of
    /// faint fills feathering out over a few pixels — and a one-pixel dark shadow under every glyph
    /// (<see cref="ShadowText"/>). Neither has an edge to read as a panel.
    /// </para>
    /// </remarks>
    private void DrawSkillDock(SpriteBatch b)
    {
        // THE FIGHT'S OWN SKILLS — the same list every event's Slot indexes; the slot index is kept through
        // the grouping because the replay is asked by it.
        var skills = _waveSkills;
        var actives = new List<int>();
        var passives = new List<int>();
        for (var i = 0; i < skills.Count; i++) (skills[i].Def.TakesABeat ? actives : passives).Add(i);
        if (actives.Count + passives.Count == 0) { s_dockRect = new Rectangle(SkillStrip.X, SkillStrip.Y, 0, 0); return; }

        var strip = SkillStrip;
        // SIZED BY THE WIDEST WORDS IT HOLDS, never under the house width: a slot that could not fit
        // "ON BITE · SHADOW" at 100 % dropped the Source word and told the player less than the same
        // slot at 150 % — the density profile changing information, not size (release polish 2026-09-05,
        // hunt-09). The dock centres on its measured width and the arena clip is wider still.
        var widest = 0;
        foreach (var i in actives.Concat(passives))
        {
            var d = skills[i].Def;
            var line2 = "2 ACTIONS · " + skills[i].Source.ToString().ToUpperInvariant();
            widest = Math.Max(widest, Math.Max(_ui.MeasureBig(d.Name, UiTypography.Headline) + UiMetrics.Space(8) + UiMetrics.Control(22),
                                               _ui.MeasureBig(line2, UiTypography.Body)));
        }
        var slotW = Math.Max(DockSlotW, UiMetrics.Space(10) + MedallionPx + UiMetrics.Space(14) + widest + UiMetrics.Space(8));
        var groupGap = actives.Count > 0 && passives.Count > 0 ? StripGroupGap : 0;
        var gaps = Math.Max(0, actives.Count - 1) + Math.Max(0, passives.Count - 1);
        var width = (actives.Count + passives.Count) * slotW + gaps * SlotGap + groupGap;
        var x0 = SkillStripCentreX - width / 2;
        var y = strip.Y + UiMetrics.Space(8);
        var slotY = y + UiTypography.Pitch(UiTypography.Secondary) + UiMetrics.Space(2);
        var dock = new Rectangle(x0, strip.Y, width, strip.Height);
        s_dockRect = dock;

        // The soft shadow: six fills, each a few pixels wider than the last and each faint, so the centre
        // is dimmed by about a third and the edge fades to nothing over 24 px.
        var feather = UiMetrics.Space(4);
        for (var i = 6; i >= 1; i--)
        {
            var band = new Rectangle(dock.X - i * feather, dock.Y - i * feather / 2, dock.Width + 2 * i * feather, dock.Height + i * feather);
            _ui.Fill(b, band, UiKit.Ink * 0.07f);
        }

        var x = x0;
        if (actives.Count > 0)
        {
            x = DrawSkillGroup(b, "ACTIVE", x, y, slotY, slotW, actives);
            if (passives.Count > 0)
            {
                var ruleInset = UiMetrics.Space(12);
                _ui.Fill(b, new Rectangle(x + groupGap / 2, strip.Y + ruleInset, 1, strip.Height - ruleInset * 2), UiInk.Rule * 0.8f);
                x += groupGap;
            }
        }
        if (passives.Count > 0) DrawSkillGroup(b, "PASSIVE", x, y, slotY, slotW, passives);
    }

    /// <summary>One group of the dock: its caption and its skills. Returns the x after the last slot.</summary>
    private int DrawSkillGroup(SpriteBatch b, string caption, int x, int captionY, int slotY, int slotW, List<int> members)
    {
        ShadowText(b, caption, x + UiMetrics.Space(4), captionY, Slate, UiTypography.Secondary);
        foreach (var i in members)
        {
            DrawSkillSlot(b, new Rectangle(x, slotY, slotW, SlotH), i);
            x += slotW + SlotGap;
        }
        return x - SlotGap;
    }

    /// <summary>Text over the scene: a one-pixel dark shadow under the glyphs, then the glyphs. Legible on any floor, no plate.</summary>
    private void ShadowText(SpriteBatch b, string text, int x, int y, Color ink, int px)
    {
        _ui.TextBig(b, text, x + 1, y + 2, UiKit.Ink * 0.85f, px);
        _ui.TextBig(b, text, x, y, ink, px);
    }

    /// <summary>Where a slot's medallion sits: inset from the slot's left, centred on its height.</summary>
    private static Rectangle MedallionBox(Rectangle slot)
        => new(slot.X + UiMetrics.Space(10), slot.Y + (SlotH - MedallionPx) / 2, MedallionPx, MedallionPx);

    /// <summary>One equipped skill: medallion with its cooldown ring and telegraph, name, readiness word, Source.</summary>
    private void DrawSkillSlot(SpriteBatch b, Rectangle slot, int i)
    {
        var s = _waveSkills[i];
        var def = s.Def;
        var sc = SourceColor.GetValueOrDefault(s.Source, Bone);
        var t = Timing(i, def);

        // READY OR NOT, IN THE ICON'S OWN BRIGHTNESS: a step, not a fade — the moment worth seeing is the one
        // where the skill becomes available. The ring around the rim answers HOW MUCH LONGER; this answers
        // WHETHER. A passive is always ready and never dims.
        //
        // THE TELEGRAPH BRIGHTENS THE WHOLE MEDALLION (2026-09-09). `waiting` tested t.Flash alone while
        // `lit` — which the hex frame uses — is max(Flash, Telegraph), so for the 300 ms before a cast
        // the FRAME went full gold around a diamond and a glyph still sitting at 0.45. A gold ring
        // around a dark icon, three times a second, on every Active in the rail. Reading `lit` here
        // instead makes those 300 ms a charge-up: the medallion brightens INTO its own cast, which is
        // the tell the rail was missing.
        var lit = Math.Max(t.Flash, t.Telegraph);
        var waiting = t.Swept < 1f && lit <= 0f;
        var wake = waiting ? MathHelper.Lerp(WaitingSkillDim, 1f, Math.Clamp(lit, 0f, 1f)) : 1f;
        // THE READY POP. The one moment the rail is for had no presentation whatsoever: the word
        // swapped, the dim stepped up, and nothing moved. The medallion swells a tenth over a
        // Transition on the crossing, armed in the event pump (Draw must never arm a pulse).
        var box = MedallionBox(slot);
        // THE SWELL OVERSHOOTS AND SETTLES. A flat tenth over 180 ms reads as a nudge; the same motion
        // with an overshoot reads as a thing arriving. The curve is 1 - (1-p)^2 shaped past its target
        // and pulled back, which is the ordinary back-ease and costs nothing — and it is skipped whole
        // under Reduced Motion, like every other one-shot on this screen.
        if (UiMotion.Pulse(SkillReadyKey(i)) is var pp && pp > 0f && !UiMotion.Reduced)
        {
            var k = 1f - UiMotion.Smooth(pp);              // 0 at the crossing, 1 when the pop is spent
            var swell = MathF.Sin(k * MathF.PI) * 0.17f;   // out and back, peaking a sixth over
            var grow = (int)MathF.Round(box.Width * swell);
            box = new Rectangle(box.X - grow / 2, box.Y - grow / 2, box.Width + grow, box.Height + grow);
        }
        if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl)
            b.Draw(sl, box, lit > 0f ? Color.Lerp(Color.White, Gold, lit) : Color.White * wake);
        // The diamond and the glyph are PROPORTIONS of the medallion art (44 and 40×44 of 72), so they
        // sit in the hex's window at any size.
        var half = box.Width * 22 / 72;
        _ui.Diamond(b, new Rectangle(box.Center.X - half, box.Center.Y - half, half * 2, half * 2), sc * (0.34f * wake));
        int gx = box.Width * 16 / 72, gy = box.Height * 14 / 72;
        var glyphBox = new Rectangle(box.X + gx, box.Y + gy, box.Width - gx * 2, box.Height - gy * 2);
        if (_ui.Assets.Get($"icon_skill_{def.Id}") is { } sg) b.Draw(sg, glyphBox, Color.Lerp(sc, Color.White, 0.65f) * wake);
        else if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g) b.Draw(g, glyphBox, Color.White * wake);
        else _ui.Diamond(b, glyphBox, sc * wake);
        // ── THE COOLDOWN, AND THE MOMENT IT ENDS ─────────────────────────────────────────────────
        //
        // For a beat-counted skill — which is most of them — this ring used to be a STAIRCASE: the
        // dial is quantised to one notch per action, so it held perfectly still for a whole beat
        // (1500 ms), jumped in a single frame, and held again. Three instantaneous jumps in a 4.5 s
        // cycle and nothing at all in between, which is the whole of "the skill cooldown animation
        // isn't satisfying" (playtest 2026-09-09). The notch still MEANS one action — that is what
        // the steps are for, and the smooth dial would lie about when the skill fires — but it now
        // TRAVELS to its new position over a Transition instead of teleporting. Under Reduced Motion
        // Ease returns the target outright, which is exactly the old behaviour.
        var ringCentre = new Vector2(box.Center.X, box.Center.Y);
        var ringR = box.Width * 0.50f;
        var shownSweep = _devCooldownPose
                         ?? (t.RingSteps > 0 ? UiMotion.Ease(SkillRingKey(i), t.Swept, UiMotion.Transition) : t.Swept);
        var pop = UiMotion.Pulse(SkillReadyKey(i));

        if (t.Flash <= 0f)
        {
            // THE EDGE WARMS AS IT APPROACHES. The moving line was full gold from the first frame of a
            // cooldown to the last, so the ring said HOW MUCH LONGER and never HOW CLOSE. Ramped, the
            // last quarter of every wait glows, and a rail of four skills tells you at a glance which
            // one is about to go off.
            var heat = Math.Clamp((shownSweep - 0.55f) / 0.45f, 0f, 1f);
            var edge = Color.Lerp(new Color(0x8A, 0x74, 0x52), Gold, heat);
            _ui.CooldownSweep(b, ringCentre, ringR, shownSweep, new Color(0x0F, 0x0B, 0x0B, 0xE0), edge);

            // ...AND IT CARRIES A HEAD. A one-pixel line on a band that moves a few degrees a second is
            // invisible; a short bright arc trailing back from it is not, and it makes the ring read as
            // something travelling rather than as a shape being erased.
            if (shownSweep is > 0f and < 1f)
            {
                const float step = MathF.PI / 72f;
                var lead = -MathF.PI / 2f + shownSweep * MathF.Tau;
                for (var k = 0; k < 7; k++)
                {
                    var a = lead + k * step;
                    var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    var fadeK = (1f - k / 7f) * (1f - k / 7f);
                    _ui.LineSeg(b, ringCentre + dir * (ringR * 0.84f), ringCentre + dir * ringR,
                                MathF.Max(2f, ringR * 0.10f), edge * (0.55f * fadeK));
                }
            }

            // THE NOTCHES THEMSELVES, so "2 ACTIONS" is a shape and not only a word: the band reads as
            // a segmented track and each step visibly arrives somewhere. The one just crossed FLARES —
            // a spoke that brightens and reaches past the rim, then settles back into the track.
            if (t.RingSteps > 1 && shownSweep < 1f)
            {
                var tick = UiMotion.Pulse(SkillStepKey(i));
                var crossed = t.RingSteps > 0 ? (int)MathF.Floor(shownSweep * t.RingSteps + 0.001f) : 0;
                for (var n = 1; n < t.RingSteps; n++)
                {
                    var a = -MathF.PI / 2f + n / (float)t.RingSteps * MathF.Tau;
                    var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    var hot = n == crossed && tick > 0f ? UiMotion.Smooth(tick) : 0f;
                    var reach = ringR * (1f + 0.14f * hot);
                    _ui.LineSeg(b, ringCentre + dir * (ringR * 0.84f), ringCentre + dir * reach,
                                2f + 2f * hot, Color.Lerp(Slate * 0.55f, Gold, hot));
                }
            }

            // AND THE BAND COMPLETES RATHER THAN VANISHING. CooldownSweep returns on its first line at
            // ready >= 1, so the dark band simply disappeared on the frame a skill came up — the single
            // moment the rail exists to teach had no event at all. On the ready crossing the band is
            // drawn back in GOLD and unwinds off over a Transition.
            if (pop > 0f)
                _ui.CooldownSweep(b, ringCentre, ringR, 1f - UiMotion.Smooth(pop), Gold * 0.40f, Gold);
        }
        else
        {
            var halo = new Rectangle(box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6);
            Outline(b, halo, Gold * t.Flash, 2);
        }

        // ── READY: A RING LEAVES THE MEDALLION. ──────────────────────────────────────────────────
        //
        // Drawn after the ring and the art so it crosses both, and after the branch above so it fires
        // whether the skill came up quietly or came up mid-cast. Two rings a beat apart, the second
        // wider and fainter, so the burst has a front and a wake instead of being one expanding circle
        // — the cheapest thing that reads as ENERGY rather than as a growing outline.
        if (!UiMotion.Reduced && UiMotion.Pulse(SkillBurstKey(i)) is var burst && burst > 0f)
        {
            var age = 1f - burst;                       // 0 at the crossing, 1 when spent
            SkillBurstRing(b, ringCentre, ringR, age, Gold);
            if (age > 0.18f) SkillBurstRing(b, ringCentre, ringR, age - 0.18f, sc);
        }

        // The words: NAME (Headline), then the readiness word and the Source on one Body line — colour AND text.
        // Shadowed, because they stand on the scene now rather than on a plate.
        var tx = box.Right + UiMetrics.Space(14);
        var room = slot.Right - UiMetrics.Space(8) - tx;
        var nameY = slot.Y + UiMetrics.Space(16);
        ShadowText(b, _ui.ShortenBig(def.Name, room, UiTypography.Headline), tx, nameY, Bone, UiTypography.Headline);
        var word = ReadinessWord(def, t);
        var ty = nameY + UiTypography.Pitch(UiTypography.Headline);
        var source = s.Source.ToString().ToUpperInvariant();
        var wordW = _ui.MeasureBig(word, UiTypography.Body);
        ShadowText(b, word, tx, ty, t.Swept >= 1f || !def.TakesABeat ? Bone : Slate, UiTypography.Body);
        // The Source word ALWAYS prints — the slot is sized for it (DrawSkillDock, hunt-09).
        ShadowText(b, " · ", tx + wordW, ty, Slate, UiTypography.Body);
        ShadowText(b, source, tx + wordW + _ui.MeasureBig(" · ", UiTypography.Body), ty, sc, UiTypography.Body);
        // The Source glyph BESIDE THE NAME — the third cue beside the colour and the word. Pinned to the
        // slot's far corner it hung in empty space for a short name (release polish 2026-09-05, hunt-09).
        if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } sg2)
        {
            var gem = UiMetrics.Control(22);   // a small glyph beside the name — an icon box, at the profile
            var nameW = _ui.MeasureBig(_ui.ShortenBig(def.Name, room, UiTypography.Headline), UiTypography.Headline);
            b.Draw(sg2, new Rectangle(tx + nameW + UiMetrics.Space(8), nameY + (UiTypography.Headline - gem) / 2 + 1, gem, gem), Color.White * 0.9f);
        }
    }

    /// <summary>The readiness in a word: READY · 2 ACTIONS · 1.4s · ACTIVE (a Field) · ON BITE (a Reaction).</summary>
    private string ReadinessWord(SkillDef def, SkillTiming t)
    {
        if (def.Kind == SkillKind.Reaction) return "ON BITE";
        if (def.Kind == SkillKind.Field || !def.TakesABeat) return "ACTIVE";
        if (t.Swept >= 1f) return "READY";
        if (t.RingSteps > 0)
        {
            var left = Math.Max(1, t.RingSteps - (int)MathF.Floor(t.Ready * t.RingSteps + 0.001f));
            return left == 1 ? "1 ACTION" : $"{left} ACTIONS";
        }
        if (t.NextMs != int.MaxValue) return $"{MathF.Max(0.1f, (t.NextMs - _playheadMs) / 1000f):0.0}s";
        return "WAITING";
    }

    /// <summary>
    /// A skill's timing this frame, read off the resolved wave — the fight's real cadence, not a decorative tick.
    /// </summary>
    /// <remarks>
    /// A beat-counted skill STEPS: one notch of the ring per plain action in its cycle, so "two more swings"
    /// reads at a glance; a timed skill sweeps. Between waves the carry (<c>_carryBeats</c> / <c>_carryMs</c>)
    /// says how long a skill has really been waiting, so a ring never sits dead through a short wave. A passive
    /// is always ready. The playtest notes that shaped each branch are in the history of the rail this replaced.
    /// </remarks>
    private SkillTiming Timing(int i, SkillDef def)
    {
        var flash = UiMotion.Pulse(SkillCastKey(i));   // 1 → 0 over a Transition, armed by the cast event
        var isPassiveSlot = !def.TakesABeat;
        var ready = isPassiveSlot ? 1f : 0f;
        var ringSteps = 0;
        var telegraph = 0f;
        var next = int.MaxValue;
        if (_replay is not null && !isPassiveSlot)
        {
            next = _replay.NextSkillAfter(_playheadMs, i);
            var prev = _replay.LastSkillBefore(_playheadMs, i);
            var carryBeats = prev >= 0 ? 0 : _carryBeats.GetValueOrDefault(i);
            var carryMs = prev >= 0 ? 0f : _carryMs.GetValueOrDefault(i);
            if (next != int.MaxValue && next - _playheadMs is > 0f and <= 300f) telegraph = 1f - (next - _playheadMs) / 300f;
            if (def.Beats is var bts and > 0)
            {
                var beatNow = _replay.BeatAt(_playheadMs);
                var since = prev >= 0
                    ? Math.Max(0, beatNow - _replay.BeatAt(prev))
                    : Math.Max(0, beatNow - _replay.FirstBeat + 1) + carryBeats;
                ringSteps = Math.Max(1, bts - 1);
                ready = Math.Clamp(since / (float)ringSteps, 0f, 1f);
            }
            else if (next != int.MaxValue)
            {
                var from = prev >= 0 ? prev : -carryMs;
                var span = MathF.Max(1f, next - from);
                ready = Math.Clamp((_playheadMs - from) / span, 0f, 1f);
            }
            else if (prev >= 0)
            {
                var cdMs = MathF.Max(1f, RailCooldownMs(def));
                ready = Math.Clamp((_playheadMs - prev) / cdMs, 0f, 1f);
            }
            else
            {
                var cdMs = MathF.Max(1f, RailCooldownMs(def));
                ready = Math.Clamp((_playheadMs + carryMs) / cdMs, 0f, 1f);
            }
        }
        var swept = ringSteps > 0
            ? Math.Clamp((int)MathF.Floor(ready * ringSteps + 0.001f), 0, ringSteps) / (float)ringSteps
            : ready;
        return new SkillTiming(ready, swept, ringSteps, telegraph, flash, next);
    }

    /// <summary>How bright a skill's medallion is while it is still waiting to come ready.</summary>
    /// <remarks>
    /// 0.45, which is dark enough to sort the rail into lit and unlit at a glance and light enough that
    /// the Source glyph stays legible — a waiting skill still has to say WHICH skill it is. The empty
    /// slots further down the rail sit at 0.5, so a waiting skill reads as dimmer than a real row but
    /// not as absent.
    /// </remarks>
    /// <summary>
    /// One expanding ring of the ready burst: wider, thinner and fainter as it ages.
    /// </summary>
    /// <param name="age">0 the instant it fires, 1 when it is gone.</param>
    /// <remarks>
    /// Twenty-four segments, which is smooth at the medallion's radius and is twenty-four draws — this
    /// runs at most four times a wave, once per Active, and only on the frames a skill comes up.
    /// </remarks>
    private void SkillBurstRing(SpriteBatch b, Vector2 centre, float radius, float age, Color tint)
    {
        age = Math.Clamp(age, 0f, 1f);
        var r = radius * (1f + 0.60f * UiMotion.Smooth(1f - age));
        var thick = MathF.Max(1.5f, radius * 0.13f * (1f - age));
        var alpha = (1f - age) * (1f - age);
        const int seg = 24;
        for (var k = 0; k < seg; k++)
        {
            var a0 = k / (float)seg * MathF.Tau;
            var a1 = (k + 1) / (float)seg * MathF.Tau;
            _ui.LineSeg(b, centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r,
                        centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r, thick, tint * alpha);
        }
    }

    private const float WaitingSkillDim = 0.45f;


    /// <summary>
    /// DEV: hold the strike clip at a fixed point (0..1 of its duration) instead of letting combat drive it.
    /// </summary>
    /// <remarks>
    /// A one-second screenshot lands wherever the fight happens to be, which is almost never mid-swing,
    /// so the frame that most wants checking is the one no capture reliably catches.
    /// </remarks>
    public float? DevSwingPhase;

    /// <summary>
    /// Pick, start and finish the champion's clip. ONE clip at a time, played through to its last frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE OLD MODEL CHOSE THE CLIP EVERY FRAME from four overlapping timers</b> — a skill windup, a
    /// skill follow-through, a strike follow-through and a strike windup — with a priority order between
    /// them. It looked right at one cast per second. At a real build's tempo (skills at 1.06× and rising,
    /// four slots, the auto-swing every 1.2 s) the next cast's windup began before the last cast's release
    /// had finished, outranked it, and snapped the figure from frame 7 back to frame 0; a strike windup
    /// already half-run when a follow-through ended made the arm APPEAR mid-swing. Playtest 2026-08-25:
    /// "the animations cannot keep up with the skill speed; it goes into strange animations."
    /// </para>
    /// <para>
    /// Now a clip is a COMMITMENT. When the champion is free, the next beat — the earlier of the next
    /// Skill event and the next auto-strike — is read off the replay, and its clip starts exactly one
    /// contact-length ahead of it so the blow connects on the beat (ContactFraction). If the champion
    /// only became free INSIDE that window, the clip runs faster (up to MaxClipSpeed) so the contact still
    /// lands on time rather than starting late; beyond that it lands a little late, which reads as a
    /// heavy swing, not a broken one. While a clip runs, later beats do not touch it — their damage
    /// numbers and effects still fire, because those are the fight, and the figure catches the next beat
    /// it can. Nothing snaps to frame 0 mid-recovery any more.
    /// </para>
    /// <para>
    /// The clock is the REPLAY clock (<c>_playheadMs</c>), so a raised battle speed plays the clips faster
    /// in step with the beats they are aimed at, and a paused wave holds the pose.
    /// </para>
    /// </remarks>
    private void UpdateChampionClip()
    {
        // Expired — after its own length PLUS a settle on the last frame (the clip does not loop, so the
        // held frame is the pose the action ends in; without it the cut to idle was the "did it finish?"
        // the playtest could not read) — or the playhead is BEHIND the clip's start (a rewound fixture),
        // which would run it backwards; either way the commitment is over.
        var clipEnds = _clipStartMs + (_clipTiming?.TotalMs ?? ClipMs / _clipSpeed + SettleMs);
        if (_clipName is not null && (_playheadMs >= clipEnds || _playheadMs < _clipStartMs))
        {
            if (PresentTrace.Enabled) PresentTrace.Log("clip-end", _clipName);
            _clipName = null;
            _clipTiming = null;
            _idleFrom = _anim;   // the idle picks up from ITS first frame, not from a random loop phase
        }
        if (_clipName is not null)
        {
            // committed — plays through, and once its blow has landed it plans how it hands the figure over
            PlanHandoff();
            return;
        }
        if (_replay is null) return;

        // EACH FORM THROWS ITS OWN SHAPE. The clip is named after the Form, and Character.StripKeys
        // falls back to the old attack/cast pair for any character whose strip is not generated yet —
        // so a Strike is still a swing and a Mark is still a cast until the art lands.
        var reactionSlots = ReactionSlots();
        var next = NextAnimatedAction(reactionSlots);
        float? beatMs = next?.BeatMs;
        var clip = next?.Clip;
        var skillEvent = next?.Cast;
        // THE TRAP, WHICH IS NOT AN ACTION. It answers the enemy's bite, off the beat, so it can never
        // be aimed at one — and for the whole life of the fight it therefore had no champion animation
        // at all: the trap bit, the enemy took damage, and the figure stood still through it.
        //
        // Committed opportunistically, and only when nothing else is due: a Trap consumes no beat, so
        // it must never take the clip a real action was about to use. That it fires on the enemy's
        // swing — off the champion's own metronome — is what makes the opening usually there.
        float? lastTrap = null;
        foreach (var ri in reactionSlots)
            if (_replay.LastTrapBefore(_playheadMs, ri) is { } tms && (lastTrap is null || tms > lastTrap))
                lastTrap = tms;
        if (beatMs is null && lastTrap is { } trapMs
            && _playheadMs - trapMs < TrapClipGraceMs)
        {
            CommitPlainClip("trap", trapMs, Math.Max(0.6f, ClipMs / (_beatMs * SkillClipShareOfBeat)), (int)trapMs);
            if (PresentTrace.Enabled) PresentTrace.Log("clip-start", $"trap\tbeat={trapMs:0}\tspeed={_clipSpeed:0.000}");
            return;
        }

        if (beatMs is null) return;

        // NOTHING OF A HELD BEAT IS SHOWN. While the barrier holds the replay short of a beat, the clip
        // aimed at it — or at anything after it — does not begin. The park already sits ahead of the
        // wind-up; this is for the playhead that was inside that window when the hold arrived, which
        // stands in idle rather than half-way through a cast the card has not introduced yet.
        if (ReplayHeld && HeldEventAtMs is { } heldAt && beatMs.Value >= heldAt) return;

        // THE AUTHORED ACTION (ADR-011). A cast whose Form has a recipe for this champion is PERFORMED: its
        // clip starts so that its release lands one flight before the beat, and the beat is the contact.
        // Too early for it is "not yet" — never a fall-through into the old 5/8 clip.
        if (skillEvent is { } cast && clip is not null)
            switch (TryCommitPerformance(cast, clip))
            {
                case PerformCommit.Committed:
                case PerformCommit.NotYet:
                    return;
            }

        // EVERY action fills its share of the BEAT (ClipShareOfBeat): the sim acts on the
        // beat and only on it, so a clip sized to 0.65 of a beat — plus its settle — is always over
        // before the next action's clip may start, and a fast build visibly fights fast.
        // A CAST IS NOT A SWING. Both used to take the same share of the beat, so TEMPO hurried them
        // equally and a fast build ran eight frames of a Transformation past the eye — "frame atlıyormuş
        // gibi". The swing keeps the beat-scaled share (so Tempo is felt ON THE SWING, which is what the
        // designer asked for) and now takes LESS of it, leaving more beat standing after it; a cast takes
        // more, so it plays close to its authored second.
        var share = clip == "attack" ? ClipShareOfBeat : SkillClipShareOfBeat;
        var baseSpeed = Math.Max(0.6f, ClipMs / (_beatMs * share));
        var contactMs = ContactMs(cast: clip != "attack");
        var lead = beatMs.Value - _playheadMs;
        if (lead > contactMs) return;   // not yet: the clip starts one contact-length before the beat

        CommitPlainClip(clip!, _playheadMs,
                        Math.Clamp(baseSpeed * contactMs / Math.Max(1f, lead), baseSpeed, Math.Max(baseSpeed, MaxClipSpeed)),
                        (int)beatMs.Value);
        if (PresentTrace.Enabled) PresentTrace.Log("clip-start", $"{clip}\tbeat={beatMs.Value:0}\tspeed={_clipSpeed:0.000}\tcontactMs={contactMs:0}\tframeMs={1000f / ChampionFps / _clipSpeed:0}");
    }

    /// <summary>
    /// Commit a PLAIN clip (a strip without a timing file): its eight equal frames at <paramref name="speed"/>, and its
    /// settle, as a timing whose phases a handoff can read (<see cref="ActionClipTiming.Plain"/>).
    /// </summary>
    private void CommitPlainClip(string clip, float startMs, float speed, int beatMs)
    {
        _clipSpeed = speed;
        _clipStartMs = startMs;
        _clipName = clip;
        _clipBeatMs = beatMs;
        _clipTiming = ActionClipTiming.Plain(ClipMs / 8f / speed, SettleMs);
        _handoffPlanned = false;
    }

    /// <summary>
    /// The next beat the figure plays and the clip it plays there: the earlier of the next skill cast (a Reaction
    /// excepted: it fires on a bite, not on a beat, and gets its own commitment) and the next basic swing.
    /// </summary>
    /// <remarks>
    /// The ONE pick both the commitment and the handoff read, so the action a recovery makes room for is the
    /// action that then takes the figure. The swing is a real action (MIGHT's hit, at TEMPO's cadence) and the sim
    /// holds one lock for swings and casts alike, so a swing clip never starts inside a cast nor a cast inside it.
    /// </remarks>
    private (float BeatMs, string Clip, BattleEvent? Cast)? NextFigureBeat(HashSet<int> reactionSlots, float? afterMs = null)
    {
        if (_replay is null) return null;
        var from = afterMs ?? _playheadMs;
        (float BeatMs, string Clip, BattleEvent? Cast)? next = null;
        if (_replay.NextSkillEventAfter(from, reactionSlots) is { } nextSkill
            && nextSkill.Slot >= 0 && nextSkill.Slot < _waveSkills.Count)
            next = (nextSkill.AtMs, _waveSkills[nextSkill.Slot].Def.ClipKey, nextSkill);
        // the cached next swing is refreshed when a Strike is crossed, AFTER this runs on that frame: on it, ask
        // the replay, or the swing just landing would hide the one after it
        var swing = afterMs is null && _nextChampStrikeMs > from ? _nextChampStrikeMs : _replay.NextChampionStrikeAfter(from);
        if (swing > from && swing != int.MaxValue && (next is null || swing < next.Value.BeatMs))
            next = (swing, "attack", null);
        return next;
    }

    /// <summary>
    /// The next action the FIGURE will play: <see cref="NextFigureBeat"/>, unless that is a plain clip that could not
    /// reach its exit pose before the PERFORMED action right after it needs the figure — then that performed action.
    /// </summary>
    /// <remarks>
    /// THE RESERVATION (ADR-011). A performed action's wind-up is part of the action: it starts from its first pose.
    /// On the fastest builds (TEMPO trained to its cap, swings 400 ms apart) the swing before a SPRAY had not even
    /// landed when SPRAY had to start, so SPRAY entered on its third frame, every cast. The plain beat that cannot fit
    /// is not animated, as every beat that falls while the figure is busy already is: its hit, number, flash and
    /// sound still fire, because they are the fight. Traced as a `yield`, never silent.
    /// </remarks>
    private (float BeatMs, string Clip, BattleEvent? Cast)? NextAnimatedAction(HashSet<int> reactionSlots)
    {
        if (NextFigureBeat(reactionSlots) is not { } next) return null;
        if (next.Cast is { } now && TryAuthored(now, out _, out _)) return next;              // performed: it is the reservation
        if (NextFigureBeat(reactionSlots, next.BeatMs) is not { } after || after.Cast is not { } behind
            || !TryAuthored(behind, out _, out _)) return next;                              // no performed action behind it
        var (_, latest) = Reservation(after);
        if (PlainEarliestExit(next) <= latest) return next;
        if (PresentTrace.Enabled && _yieldLoggedFor != (int)next.BeatMs)
        {
            _yieldLoggedFor = (int)next.BeatMs;
            PresentTrace.Log("yield", $"{next.Clip}\tbeat={next.BeatMs:0}\texit={PlainEarliestExit(next):0}\tfor={after.Clip}\tforBeat={after.BeatMs:0}\tlatest={latest:0}");
        }
        return after;
    }

    private int _yieldLoggedFor = int.MinValue;

    /// <summary>
    /// The earliest a plain clip for <paramref name="next"/> could reach its exit pose if it began now (or at its own
    /// start, if that is later): the same speed the commitment below would give it, its protected frames whole, its
    /// recovery at the readable floor.
    /// </summary>
    private float PlainEarliestExit((float BeatMs, string Clip, BattleEvent? Cast) next)
    {
        var cast = next.Clip != "attack";
        var baseSpeed = Math.Max(0.6f, ClipMs / (_beatMs * (cast ? SkillClipShareOfBeat : ClipShareOfBeat)));
        var contactMs = ContactMs(cast);
        var start = Math.Max(_playheadMs, next.BeatMs - contactMs);
        var speed = Math.Clamp(baseSpeed * contactMs / Math.Max(1f, next.BeatMs - start), baseSpeed, Math.Max(baseSpeed, MaxClipSpeed));
        return ActionHandoff.EarliestExit(start, ActionClipTiming.Plain(ClipMs / 8f / speed, 0f), MinRecoveryPoseMs, MaxRecoveryCompression);
    }

    /// <summary>
    /// When the next action wants the figure: its IDEAL start (its whole wind-up) and its LATEST (the tightest its
    /// own timing allows: an authored clip's elastic floor, a plain clip at <see cref="MaxClipSpeed"/>).
    /// </summary>
    private (float Ideal, float Latest) Reservation((float BeatMs, string Clip, BattleEvent? Cast) next)
    {
        if (next.Cast is { } performed && TryAuthored(performed, out var recipe, out var timing))
            return (next.BeatMs - recipe.TravelMs - timing.MarkerMs(recipe.ReleaseMarker),
                    next.BeatMs - ActionPerformance.MinLeadMs(recipe, timing));
        var cast = next.Clip != "attack";
        var baseSpeed = Math.Max(0.6f, ClipMs / (_beatMs * (cast ? SkillClipShareOfBeat : ClipShareOfBeat)));
        return (next.BeatMs - ContactMs(cast), next.BeatMs - ClipMs * ContactFraction / Math.Max(baseSpeed, MaxClipSpeed));
    }

    /// <summary>True once the committed clip's handoff has been planned (once per clip, after its blow lands).</summary>
    private bool _handoffPlanned;

    /// <summary>The shortest a recovery pose may stay on screen in a handoff: two display frames. One reads as a pop.</summary>
    private const float MinRecoveryPoseMs = 2000f / 60f;

    /// <summary>The fastest a recovery may play in a handoff, relative to its own pace (see <see cref="ActionHandoff"/>).</summary>
    private const float MaxRecoveryCompression = 3f;

    /// <summary>
    /// THE HANDOFF (ADR-011, <see cref="ActionHandoff"/>): once the committed clip's blow has landed, fit its recovery
    /// so it ARRIVES at its exit pose when the next action wants the figure, instead of being cut mid-pose.
    /// </summary>
    /// <remarks>
    /// Before, at a fast TEMPO, a performed cast took the figure at its latest start whatever the outgoing clip was
    /// showing: the swing was still in its low lunge (its follow-through) and the Seeker jumped in one frame to
    /// SPRAY's ready pose. Every clip now carries its phases (a plain one through <see cref="ActionClipTiming.Plain"/>),
    /// only the recovery and its settle are retimed, and the next action starts from its own first frame.
    /// </remarks>
    private void PlanHandoff()
    {
        if (_handoffPlanned || _clipTiming is not { } t || _clipBeatMs is not { } beat || _playheadMs <= beat) return;
        _handoffPlanned = true;
        if (!t.HasMarker("recovery") || NextAnimatedAction(ReactionSlots()) is not { } next) return;
        if (ReplayHeld && HeldEventAtMs is { } heldAt && next.BeatMs >= heldAt) return;
        var (ideal, latest) = Reservation(next);
        var recoveryStart = _clipStartMs + t.MarkerMs("recovery");
        var naturalEnd = _clipStartMs + t.TotalMs;
        IActionRecipe? incoming = next.Cast is { } behind && TryAuthored(behind, out var r, out _) ? r : null;
        if (ActionHandoff.Plan(_playheadMs, recoveryStart, t.RecoveryMotionMs,
                               t.MinRecoveryMs(MinRecoveryPoseMs, MaxRecoveryCompression), naturalEnd, ideal, latest,
                               incomingPerformed: incoming is not null) is not { } plan)
            return;
        var compression = 1f;
        _clipTiming = plan.Fit == HandoffFit.Yielded
            ? t.CutAt(plan.ExitMs - _clipStartMs)
            : t.FitRecovery(plan.AvailableMs, MinRecoveryPoseMs, out compression);
        // the performance plays the retimed clip; a yield also tells it when the next action lets go of the body
        // (its release: the throw leaves from home) so a lunge's way home is carried under that wind-up
        if (_performance is { } playing && _clipName == playing.Recipe.ClipKey && playing.ClipStartMs == _clipStartMs)
            playing.Retime(_clipTiming, plan.Fit == HandoffFit.Yielded ? plan.ExitMs : null,
                           next.BeatMs - (incoming?.TravelMs ?? 0f));
        if (PresentTrace.Enabled)
        {
            // a plain clip's contact is its contact frame; a performed action's is the beat itself (ADR-011)
            var contact = t.HasMarker("contact") ? _clipStartMs + t.MarkerMs("contact") : beat;
            PresentTrace.Log("handoff", $"{_clipName}\tbeat={beat}\tcontact={contact:0}\trecovery={recoveryStart:0}"
                                        + $"\tmotion={t.RecoveryMotionMs:0}\thold={t.HoldMs:0}\tnatural={naturalEnd:0}\tavailable={plan.AvailableMs:0}"
                                        + $"\tratio={compression:0.00}\texit={plan.ExitMs:0}\tfit={plan.Fit}\tnext={next.Clip}\tnextBeat={next.BeatMs:0}"
                                        + $"\tideal={ideal:0}\tlatest={latest:0}\tprotected={t.MarkerMs("recovery"):0}");
        }
    }

    private enum PerformCommit { NoRecipe, NotYet, Committed }

    /// <summary>
    /// Commit the champion to PERFORMING <paramref name="cast"/> (ADR-011) when its Form has a recipe and its
    /// strip an authored timing: the clip, its fitted timing and the performance, whose contact is the beat.
    /// </summary>
    private PerformCommit TryCommitPerformance(BattleEvent cast, string clip)
    {
        if (cast.Slot < 0 || cast.Slot >= _waveSkills.Count) return PerformCommit.NoRecipe;
        if (!TryAuthored(cast, out var recipe, out var timing)) return PerformCommit.NoRecipe;
        if (ActionPerformance.Schedule(recipe, timing, cast.AtMs, _playheadMs) is not { } plan) return PerformCommit.NotYet;
        var sk = _waveSkills[cast.Slot];
        // the action handing the figure over is not dropped: it finishes its impact and its way home (UpdatePerformance)
        _outgoing = _performance is { } previous && !previous.Finished(_playheadMs) ? previous : _outgoing;
        _performance = recipe switch
        {
            MeleeActionRecipe melee => new MeleePerformance(melee, plan.Timing, cast.AtMs, plan.StartMs, cast.Slot, CastTargets(cast), SourceGlow(sk.Source)),
            ProjectileActionRecipe thrown => new ActionPerformance(thrown, plan.Timing, cast.AtMs, plan.StartMs, cast.Slot, CastTargets(cast), SourceGlow(sk.Source)),
            _ => throw new InvalidOperationException($"No performance plays a {recipe.GetType().Name}."),
        };
        _clipTiming = plan.Timing;
        _clipStartMs = plan.StartMs;
        _clipSpeed = 1f;
        _clipName = recipe.ClipKey;   // its own authored clip (HARD HANDS' hard_hands), not always the Form's
        _clipBeatMs = cast.AtMs;
        _handoffPlanned = false;
        if (PresentTrace.Enabled)
            PresentTrace.Log("clip-start", $"{recipe.ClipKey}\tauthored\tbeat={cast.AtMs}\tstart={plan.StartMs:0}\trelease={_performance.ReleaseMs:0}"
                             + $"\ttargets={string.Join(",", _performance.Targets)}\trecipe={recipe.Id}");
        return PerformCommit.Committed;
    }

    /// <summary>
    /// This champion's recipe for <paramref name="cast"/>'s skill (the skill's own, else its Form's: see
    /// <see cref="ActionRecipes.For"/>) and the authored timing of the strip it plays, when both exist. A recipe whose
    /// own strip is absent falls back to nothing: the skill plays its Form's plain clip.
    /// </summary>
    private bool TryAuthored(BattleEvent cast, out IActionRecipe recipe, out ActionClipTiming timing)
    {
        recipe = null!;
        timing = null!;
        if (cast.Slot < 0 || cast.Slot >= _waveSkills.Count) return false;
        var def = _waveSkills[cast.Slot].Def;
        var clip = def.ClipKey;
        if (ActionRecipes.For(Character.Id, def.Id, clip, def.FxKey) is not { } r) return false;
        var stripKey = Character.StripKeys(r.ClipKey).FirstOrDefault(k => _ui.Assets.Has(k));   // the recipe's own clip
        if (stripKey is null || ActionClipLibrary.For(stripKey) is not { } t || !t.HasMarker(r.ReleaseMarker)) return false;
        recipe = r;
        timing = t;
        return true;
    }

    /// <summary>
    /// The slots whose skill REACTS (a Trap): they fire on a bite, not on a beat, so no clip is aimed at them. One
    /// set, refilled: the clip picker asks every frame.
    /// </summary>
    private HashSet<int> ReactionSlots()
    {
        _reactionSlots.Clear();
        for (var ri = 0; ri < _waveSkills.Count; ri++)
            if (_waveSkills[ri].Def.Kind == SkillKind.Reaction) _reactionSlots.Add(ri);
        return _reactionSlots;
    }

    private readonly HashSet<int> _reactionSlots = new();

    /// <summary>
    /// Rig only (RH_SHOT_SEQ `@N`): the next PERFORMED cast whose beat is within <paramref name="withinMs"/> of the
    /// playhead, with how many enemies it strikes. Read from the replay, which already holds the wave's events.
    /// </summary>
    internal (int Targets, float BeatMs)? DevNextPerformedCast(float withinMs)
    {
        if (_replay?.NextSkillEventAfter(_playheadMs, ReactionSlots()) is not { } cast
            || cast.Slot < 0 || cast.Slot >= _waveSkills.Count || cast.AtMs - _playheadMs > withinMs) return null;
        if (!TryAuthored(cast, out _, out _)) return null;
        return (CastTargets(cast).Count, cast.AtMs);
    }

    private Point _tracedRoot;   // the last root offset traced, so the trace logs a lunge's motion once per change

    /// <summary>
    /// A performance not finished (its clip still short of its end, its impact fading, a lunge on its way home): the
    /// wave-clear break lets it play out, with the clip on the figure, rather than cutting it. With no performance
    /// running, the clip is cut at the clear as it always was.
    /// </summary>
    private bool ActionStillPlaying()
        => _performance is { } p && !p.Finished(_playheadMs)
           || _outgoing is { } o && !o.Finished(_playheadMs);

    /// <summary>
    /// PRESENTATION ROOT MOTION (ADR-011): how far the performed actions carry the figure right now — a melee lunge to
    /// its target and back, including one still on its way home under the next action's wind-up (the outgoing
    /// performance) — each read against the timing its own clip played. Zero for everything else. The fight's
    /// positions never move.
    /// </summary>
    private float PerformedRootOffset()
        => (_outgoing?.RootOffsetX(_playheadMs) ?? 0f) + (_performance?.RootOffsetX(_playheadMs) ?? 0f);

    /// <summary>The performed actions' lift off the ground right now (negative = up): a melee return's hop.</summary>
    private float PerformedRootLift()
        => (_outgoing?.RootOffsetY(_playheadMs) ?? 0f) + (_performance?.RootOffsetY(_playheadMs) ?? 0f);

    /// <summary>The enemies <paramref name="cast"/> strikes (see <see cref="ActionTargets.StruckBy"/>).</summary>
    private IReadOnlyList<int> CastTargets(BattleEvent cast)
        => _run?.LastWaveEvents is { } events ? ActionTargets.StruckBy(events, cast) : Array.Empty<int>();

    /// <summary>
    /// Advance the performance on the fight's playhead — BEFORE the frame's events are crossed, so the blades
    /// land on the very frame the fight's hits are presented — and voice its release and contact.
    /// </summary>
    private void UpdatePerformance(float dt)
    {
        // THE OUTGOING ACTION RUNS ON (ADR-011): its impact is still fading, a lunge may still be on its way home,
        // after the next action took the figure. It used to be dropped the moment the next one committed.
        if (_outgoing is { } o)
        {
            if (_playheadMs < o.ClipStartMs - 1f || o.Finished(_playheadMs)) _outgoing = null;
            else Voice(o, o.Update(_playheadMs, dt, this));
        }
        if (_performance is not { } p) return;
        if (_playheadMs < p.ClipStartMs - 1f) { _performance = null; return; }   // rewound past it (a seek)
        var step = p.Update(_playheadMs, dt, this);
        // THE MIX: from the release to the contact's ring every other one-shot plays ducked. Game1 resets the duck
        // each frame, and this runs before the frame's events are crossed, so their sounds hear it.
        if (Sound is not null) Sound.Duck = Math.Min(p.DuckAt(_playheadMs), _outgoing?.DuckAt(_playheadMs) ?? 1f);
        Voice(p, step);
        if (p.Finished(_playheadMs) && _clipName is null) _performance = null;
    }

    /// <summary>Voice what one performance's update crossed: its release, its contact, a contact tick.</summary>
    private void Voice(IActionPerformance p, PerformanceStep step)
    {
        if (step.Released)
        {
            if (PresentTrace.Enabled)
                PresentTrace.Log("release", $"{p.Recipe.Id}\tx={step.ReleaseAt.X:0}\ty={step.ReleaseAt.Y:0}\tknives={p.Targets.Count}\tlaunch={string.Join(";", p.LaunchPoints.Select(v => $"{v.X:0},{v.Y:0}"))}"
                                            + (p is MeleePerformance lunge ? $"\treach={lunge.Reach:0}" : ""));
            // THE NAME AT THE RELEASE: the callout says what the champion is DOING, so it belongs to the
            // throw, not to the impact. Presentation only — the cast's cooldown and state stay on the event.
            if (p.Recipe.CalloutAtRelease && ShowSkillCallouts && p.SkillSlot < _waveSkills.Count)
            {
                var (text, colour) = CalloutFor(_waveSkills[p.SkillSlot].Def.Style);
                Say(text, colour);
            }
            Sound?.PlayFirst(p.Recipe.ReleaseCues, p.Recipe.ReleaseVolume, p.Recipe.ReleasePitch, Pan(step.ReleaseAt.X, p.Recipe.PanWidth), 0.04f, lead: true);
        }
        if (step.Contacted)
        {
            if (PresentTrace.Enabled) PresentTrace.Log("contact", $"{p.Recipe.Id}\tx={step.ContactAt.X:0}\ty={step.ContactAt.Y:0}\tknives={p.Targets.Count}");
            // ONE contact sound for the fan: the blades land together, and four copies of one sample are one
            // sample four times as loud (and the bank's own repeat rule would drop three of them anyway).
            Sound?.PlayFirst(p.Recipe.ContactCues, p.Recipe.ContactVolume, p.Recipe.ContactPitch, Pan(step.ContactAt.X, p.Recipe.PanWidth), 0.05f, lead: true);
        }
        // the fan's WIDTH: a quiet tick for an outer target, a few ms after the one contact. Unthrottled on
        // purpose — the performance already bounds them to two, and the bank's 90 ms gap would eat the second.
        if (step.Tick is { } tick && Sound?.Resolve(p.Recipe.ContactTickCues) is { } tickKey)
        {
            if (PresentTrace.Enabled) PresentTrace.Log("contact-tick", $"x={tick.X:0}\ty={tick.Y:0}");
            Sound.Play(tickKey, p.Recipe.ContactTickVolume, 0f, Pan(tick.X, p.Recipe.PanWidth), throttle: false, vary: 0.08f, lead: true);
        }
    }

    /// <summary>A gentle stereo position for an arena x: ±<paramref name="width"/> at the arena's edges.</summary>
    private static float Pan(float x, float width)
        => Math.Clamp((x - ArenaRect.Center.X) / Math.Max(1f, ArenaRect.Width / 2f), -1f, 1f) * width;

    bool IActionStage.TryActorFrame(string clipKey, int frame, out SpriteFrame drawn, out int frameSize)
    {
        foreach (var key in Character.StripKeys(clipKey))
            if (_ui.ResolveFrame(key, _champDrawBox, (frame + 0.5f) / ChampionFps, ChampionFps, loop: false, topCrop: -1f,
                                 flip: ChampionFacesRight, placeAs: PlacedAs(key)) is { } f)
            {
                drawn = f;
                frameSize = f.Texture.Height;
                return true;
            }
        drawn = default;
        frameSize = 0;
        return false;
    }

    bool IActionStage.TryTargetBody(int slot, out Rectangle body) => TryBody(VfxSubject.Creature(slot), out body);

    Texture2D? IActionStage.Texture(string key) => _ui.Assets.Get(key);

    float IActionStage.CasterHeight => TryBody(VfxSubject.Champion, out var champ) ? champ.Height : ChampBox.Height;

    /// <summary>How long a clip runs from its first frame to its contact frame, at this wave's beat.</summary>
    /// <remarks>
    /// The ONE formula both the clip commitment above and the replay barrier's lead read, so the park the
    /// barrier chooses is exactly where the cast would have begun — a second copy could drift, and a
    /// barrier a few milliseconds late would show the first frames of the wind-up under the card.
    /// </remarks>
    private float ContactMs(bool cast)
    {
        var baseSpeed = Math.Max(0.6f, ClipMs / (_beatMs * (cast ? SkillClipShareOfBeat : ClipShareOfBeat)));
        return ClipMs * ContactFraction / baseSpeed;
    }

    /// <summary>
    /// How long before the beat at <paramref name="atMs"/> its presentation begins — the replay barrier's lead.
    /// </summary>
    /// <remarks>
    /// A cast's is its clip's contact length: the Hunter starts the cast that far ahead so the blow lands on
    /// the beat. A Reaction has none — its clip plays AFTER the bite it answers (the trap commitment) — and
    /// nothing else is animated ahead of its own millisecond.
    /// </remarks>
    private float PresentationLeadMs(int atMs)
    {
        if (HoldBeforeKind != BattleEventKind.Skill || _run is null) return 0f;
        foreach (var e in _run.LastWaveEvents)
        {
            if (e.AtMs != atMs || e.Kind != BattleEventKind.Skill) continue;
            if (e.Slot < 0 || e.Slot >= _waveSkills.Count) return 0f;
            return _waveSkills[e.Slot].Def.Kind == SkillKind.Reaction ? 0f : ContactMs(cast: true);
        }
        return 0f;
    }

    /// <summary>
    /// The effect key for a Form — this champion's own, if it has one, else the Form's shared effect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE SAME FALLBACK SHAPE AS THE CLIPS (Character.StripKeys), and for the same reason: fifty
    /// effects arrive a character at a time, and a character whose own effect has not been generated
    /// yet must play the shared one rather than nothing. <see cref="VfxPlayer.Play"/> returns silently
    /// on a missing key, so without this the skill would simply have no effect at all — the quietest
    /// possible failure and the hardest to notice.
    /// </para>
    /// <para>
    /// WHY PER CHARACTER AT ALL. The effect used to be one strip per Form, tinted by the casting
    /// skill's Source — so a Strike was the same crescent whoever swung it. Now that each character
    /// throws its OWN shape (the Anvil's two-handed smash is not the Seeker's knife stab), a shared
    /// effect lands on a motion it was not drawn for. The designer's call, 2026-08-28: "hepsinin
    /// efektinin farklı olması daha özel hissettirir."
    /// </para>
    /// <para>
    /// The Source TINT still applies on top, so a character's effect still reads as Body or Shadow —
    /// the shape says who cast it and the colour says what it is made of.
    /// </para>
    /// </remarks>
    private string FxFor(SkillDef def) => FxFor(def.FxKey);

    /// <summary>The character's own strip for an effect key, or the shared one.</summary>
    private string FxFor(string fxKey)
    {
        var own = $"fx_{Character.Id}_{fxKey}_strip8_512";
        return _ui.Assets.Has(own) ? own : $"fx_{fxKey}";
    }

    /// <summary>A skill's full-cycle length in ms, for the rail's refill sweep.</summary>
    private static float RailCooldownMs(SkillDef d)
        => d.Beats > 0 ? d.Beats * (float)SoloBattle.DefaultBeatMs
         : d.IntervalMs > 0 ? d.IntervalMs
         : Math.Max(1_000, d.RearmMs);

    // ── The action clips' own timing — presentation constants, moved here from the Core Form
    //    table (they were never gameplay: the sim acts on the beat whatever a clip does). ─────────
    private const int CastClipMs = 700;
    private const float ClipShareOfBeat = 0.55f;
    private const float SkillClipShareOfBeat = 0.90f;

    /// <summary>The build's action-speed multiplier for the wave being shown.</summary>
    private float _castRate = 1f;

    /// <summary>The beat's length for the wave being shown (SoloBattle.BeatFor) — every action clip is sized to it.</summary>
    private float _beatMs = SoloBattle.DefaultBeatMs;

    /// <summary>The settle on an action clip's last frame before idle, ms — the readable END of an action.</summary>
    /// <remarks>
    /// 300, from 150 (designer, 2026-08-28: "karakter idle'ye alıp bekliyor sonraki animasyondan önce.
    /// Bu süreyi biraz daha arttırabiliriz"). This is a CEILING, not a duration — see <see cref="SettleMs"/>.
    /// </remarks>
    private const float ClipSettleMs = 300f;

    /// <summary>
    /// The settle actually taken: the ceiling, or whatever is left of the beat after the clip, whichever
    /// is smaller.
    /// </summary>
    /// <remarks>
    /// A flat settle added to a clip already sized as a SHARE OF THE BEAT can push the pair past the beat,
    /// and a clip is a commitment — so the overrun would not shorten the pose, it would swallow the NEXT
    /// action's clip and the champion would stand still through a swing it really made. Clamping keeps
    /// the pause as long as the fight can afford and never longer. On a slow build that is the full 300 ms;
    /// on a fast one it shrinks, which is correct — a fast build should look busy.
    /// </remarks>
    private float SettleMs => Math.Min(ClipSettleMs, Math.Max(0f, _beatMs - ClipMs / Math.Max(0.01f, _clipSpeed)));

    /// <summary>Wall-clock stamp of the last clip's end — the idle loop restarts from here, not mid-breath.</summary>
    private float _idleFrom;

    /// <summary>Per creature slot: a hit flash, 1 → 0 over ~120 ms — the blow lands ON something.</summary>
    private readonly Dictionary<int, float> _hitFlash = new();

    /// <summary>Rig only (`fightflash`): pins every creature's flash so a still capture can prove it draws.</summary>
    public bool DevHoldFlash { get; set; }

    /// <summary>
    /// How white a creature is RIGHT NOW: the stored 1 -> 0 life shaped into a swell — up over its first
    /// fifth, down over the rest — so the white arrives INSIDE the blow's animation instead of snapping
    /// on at its first frame (playtest 2026-08-29).
    /// </summary>
    private float FlashAt(int slot)
    {
        if (DevHoldFlash) return 0.9f;
        var life = _hitFlash.GetValueOrDefault(slot);
        if (life <= 0f) return 0f;
        var t = 1f - life;                       // 0 at the blow, 1 at the end
        var look = FlashLook(slot);
        var shape = t < look.Rise ? t / look.Rise : 1f - (t - look.Rise) / Math.Max(1e-3f, 1f - look.Rise);
        return shape * look.Peak;
    }

    /// <summary>The fight's usual flash: full strength, swelling over its first fifth, ~200 ms of life.</summary>
    private static readonly (float Peak, float Rise, float Rate) UsualFlash = (1f, 0.2f, 5f);

    /// <summary>
    /// Each creature's current flash: its peak (1 = the fight's usual), the share of its life spent rising, and how
    /// fast its life runs out (per second). A performed hit sets its recipe's (<see cref="ProjectileActionRecipe.TargetFlashMs"/>).
    /// </summary>
    private readonly Dictionary<int, (float Peak, float Rise, float Rate)> _hitFlashLook = new();

    private (float Peak, float Rise, float Rate) FlashLook(int slot) => _hitFlashLook.GetValueOrDefault(slot, UsualFlash);

    /// <summary>
    /// Draw a creature's white silhouette over itself at <paramref name="strength"/>. EVERY enemy path
    /// calls it — the composition, the single creature and the boss — because a flash only the
    /// composition drew is a flash the player sees on swarms and nowhere else (playtest 2026-08-29:
    /// "it only works on the small multi-creatures").
    /// </summary>
    private void FlashOver(SpriteBatch b, string? stripKey, Rectangle box, float seconds, float fps, bool loop, float strength, float crop)
    {
        if (strength <= 0f || stripKey is null || _ui.Assets.WhiteMask(stripKey) is null) return;
        ActorSprite(b, AssetLibrary.MaskKey(stripKey), box, seconds, fps, loop, Color.White * (0.9f * strength), crop);
    }

    /// <summary>The slotted Aura's Source colour for the wave being shown, or null when the build carries no Aura.</summary>
    private Color? _auraColour;

    /// <summary>The held field's effect key — it decides the art, and it is not always the aura's.</summary>
    private string? _auraFxKey;

    /// <summary>Real seconds since the last aura pulse — the pulse runs on the wall clock, not the replay's.</summary>
    private float _auraSincePulse = 999f;

    /// <summary>Seconds between aura pulses — two sim ticks, so the ring is always in the room without stacking.</summary>
    private const float AuraPulseSeconds = 1f;

    /// <summary>
    /// The aura's pulse: the authored fx_aura in the Aura's Source colour, WRAPPED AROUND THE CHAMPION —
    /// an always-on field that never touches the champion's own animation.
    /// </summary>
    /// <remarks>
    /// Three tries. A fixed strip at the champion read as a decoration; a ring drawn from line segments
    /// read as thin and, because it was clocked on the REPLAY's playhead, it froze on screen the moment
    /// a wave ended and the playhead stopped (playtest 2026-08-29: "the aura effect freezes on screen
    /// when the wave ends... the first effect was good, if you can give it the expansion it would be very
    /// good"). So: the first effect, on the wall clock — VfxPlayer updates with real dt and is cleared at
    /// every wave start, so nothing can be left standing.
    ///
    /// FOURTH SHAPE, 2026-08-28, and it deliberately UNDOES the third. The growth that washed over the
    /// pack (GrowTo 5.2, 312 px out to ~1620) came from "the circle should widen as far as the enemies";
    /// the same designer has now asked for the opposite and been specific about it: "karakteri saran bir
    /// aura (gerçekten HxH'daki aura gibi), hareketli ve tetiklenme anında en parlak anına çıkıp,
    /// tetiklenmeden sonra tamamen olmayacak şekilde sönüp tekrar parlama". A field that reaches the pack
    /// and a field that clings to the body are different pictures and only one of them can be on screen,
    /// so the later instruction wins and the earlier one is recorded here rather than quietly dropped.
    ///
    /// The shape that makes it read: scale 4.4 (about 458 px, so it stands the champion's full height),
    /// a small GrowTo 1.22 for the breath outward, and the strip's own upward flow for the movement. The
    /// dying is free — VfxPlayer.Fade eases every effect to nothing across its final third, so a pulse is
    /// brightest at the tick that spawned it and gone before the next one, which IS "sönüp tekrar parlama".
    /// </remarks>
    /// <summary>The field's own metronome — see the note at its call site in UpdateFight.</summary>
    private void PulseAuraOnClock(float dt)
    {
        _auraSincePulse += dt;
        if (_auraColour is not null && _mode == Mode.Fighting && _auraSincePulse >= AuraPulseSeconds)
            _auraSincePulse = 0f;   // the tick is the BRIGHTNESS peak now, not a spawn — see HoldAura
    }

    /// <summary>
    /// The aura, held around the champion for as long as the build carries one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IT IS A STATE, NOT AN EVENT. Every earlier version fired a one-shot on each tick, so however the
    /// strip was drawn the thing on screen was something that appeared and died — and the designer kept
    /// asking for the opposite until they spelled it out: "karakterin etrafında olacak ve sürekli açık
    /// olacak, asla sönmeyecek. Sadece hasar anında en parlak halinde olacak" (2026-08-28). So the effect
    /// is HELD (VfxPlayer.Hold): it loops, it never fades, and this method sets its brightness every frame.
    /// </para>
    /// <para>
    /// The brightness is a floor plus a spike. <see cref="AuraRest"/> is what the field looks like doing
    /// nothing — always visible, never bright enough to compete with a blow; the tick adds
    /// <see cref="AuraPeak"/> and it decays over <see cref="AuraSpikeSeconds"/>, cubed so the fall reads
    /// as a flare rather than a dimmer switch (additive alpha looks brighter than its number).
    /// </para>
    /// <para>
    /// Sized to STAND THE CHAMPION UP: scale 4.8 is about 500 px against a 430 px champion box, so the
    /// flames close over the head and the feet rather than ringing the waist. The strip's dark middle
    /// costs nothing — rhart.py glow made alpha follow luminance, so the interior adds 8/255 and the
    /// flames add all of it (the "ortasında küçük yanan alev" the designer was seeing was that interior,
    /// opaque at 107 grey, hazing the champion it was meant to wrap).
    /// </para>
    /// </remarks>
    private void HoldAura()
    {
        // NOT gated on Mode.Fighting. The pulse learned this the hard way: waves run ~2 s and the
        // transition ~2 s, so a field raised only while fighting is off screen for half the cycle and
        // reads as absent. It stops only when the champion is down.
        if (_auraColour is not { } colour || _mode == Mode.Downed) return;
        var spike = 1f - Math.Clamp(_auraSincePulse / AuraSpikeSeconds, 0f, 1f);
        var level = AuraRest + (AuraPeak - AuraRest) * spike * spike * spike;
        // IT STANDS ON THE GROUND. The old placement worked the drawn height out for itself and
        // subtracted half of it — the renderer's own sizing formula, minus its canvas-scale term,
        // written out a second time in the caller. Measured, that left the field floating from 170 px
        // above the crown to 42 px ABOVE the soles: it never reached the floor it was standing on.
        // STANDING is the anchor that says the sentence, at 1.10x the champion's own visible height.
        _vfx.Hold(VfxProfiles.FieldAura, FxFor(_auraFxKey ?? "aura"), VfxSubject.Champion, colour * level);
    }

    /// <summary>
    /// THE STANDING BARRIER — held for exactly as long as the champion holds SHIELD (§23).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It was an OUTLINE first, and the capture settled it: a 400×430 rectangle around a person does not
    /// read as a barrier, it reads as a selection box in a level editor — and it enclosed a great deal
    /// of empty arena, because the champion's layout box is far larger than the champion. The effect
    /// layer already knows how to hold a state (<see cref="HoldAura"/>), and holding the shield strip is
    /// both the smaller change and the better picture.
    /// </para>
    /// <para>
    /// IT SURROUNDS THE HUNTER, and that is the acceptance clause (§106). The size is
    /// <see cref="VfxProfiles.ShieldBarrier"/>'s 1.15 × his VISIBLE height, so the same number encloses
    /// THE OATHBOUND's 162-px silhouette and QUIVER's 373-px one: measured in the arena it is a 474-px
    /// circle around a 412-px figure, clearing the crown by 39 px and the soles by 23. What made that
    /// possible was not a bigger multiplier — the strip is drawn at 0.93 of its own native size, inside
    /// the §73 budget — it was the ART. The old <c>fx_shield</c> was a squat hemisphere filling
    /// 0.762 × 0.492 of its frame, so the largest honest picture it could ever show was 315 px of dome
    /// across a hunter's ribs, and reaching the band with it would have meant magnifying a 512-px frame
    /// to 963 (LAW 16). It is a closed ring now.
    /// </para>
    /// <para>
    /// It breathes slowly and never flashes: SHIELD is a STATE, and a state that flashes is
    /// indistinguishable from an event. The gain, the absorb and UNDYING flare on the SAME rectangle,
    /// so a blow always lands on the barrier rather than beside it.
    /// </para>
    /// </remarks>
    private void HoldShieldBarrier()
    {
        if (_replay?.HasShield != true || _mode == Mode.Downed) return;
        // REDUCED MOTION HOLDS ITS BREATH (§32: drop idle motion, keep the state). The shell stays — it
        // is the only picture that says "covered" in the arena — but it stands at the middle of the
        // swing it would otherwise ride, so the end state is the same and nothing on screen is moving
        // for a player who asked for nothing to move.
        var breathe = UiMotion.Reduced
            ? ShieldShellRest + ShieldShellSwing * 0.5f
            : ShieldShellRest + ShieldShellSwing * (0.5f + 0.5f * MathF.Sin(_anim * 1.6f));
        _vfx.Hold(VfxProfiles.ShieldBarrier, FxFor("shield"), VfxSubject.Champion, Steel * breathe);
    }

    /// <summary>
    /// THE BREAK BURST — <c>fx_shield_break</c> over the champion, once, across a REWARD.
    /// </summary>
    /// <remarks>
    /// Eight frames in a row (assets/art/VFX/shield_break/fx_shield_break_strip8_512.png) played over
    /// <see cref="UiMotion.Reward"/>, so the strip's fps follows the motion vocabulary's own longest
    /// band rather than a number typed here: 8 / 0.35 s ≈ 23 fps. Scale 4 is the size the fight already
    /// grades a death or a critical at — the break is that weight of moment.
    /// <para>
    /// It is NOT held and it does not loop: the shell that stands while the shield stands is
    /// <see cref="HoldShieldBarrier"/>, and it stops being asked for on the very frame the shield hits
    /// zero. There is no permanent glow left behind.
    /// </para>
    /// </remarks>
    private void PlayShieldBreak()
    {
        // At four-fifths white, so the hunter reads through the flash instead of vanishing under a solid disc.
        PlayFx(VfxProfiles.ShieldBreak, VfxSubject.Champion, Color.White * 0.8f);
        // FIXTURE ONLY (RH_SHOT_SHIELDFX=break): jump the burst to the middle of its own strip so the
        // shutter photographs the shatter at its widest instead of its first frame. See ShotShieldFx.
        if (ShotShieldFx == "break") _vfx.FixtureAdvance(UiMotion.Reward * 0.5f);
    }

    /// <summary>How present the barrier is while nothing is happening to it.</summary>
    /// <remarks>
    /// RAISED WITH THE ART (2026-09-04). 0.16 was calibrated against the old strip, which was a solid
    /// grey dome filling a fifth of its frame — a shape that reads at almost any alpha because it is a
    /// mass. The barrier is a THIN BRIGHT RING now, about eleven drawn pixels of band around a 474-px
    /// circle, and a line that thin at 0.16 additive is a smudge on the floor rather than a shell: the
    /// first capture on the new art showed a barrier that surrounded the hunter correctly and could not
    /// be seen doing it. Brightness belongs to the art it is lighting, so it moved when the art did.
    /// </remarks>
    private const float ShieldShellRest = 0.40f;
    /// <summary>...and how far the slow breath carries it above that.</summary>
    private const float ShieldShellSwing = 0.20f;

    // THE THREE SIZE DIALS THAT USED TO LIVE HERE ARE GONE — AuraScale 6.4, ShieldShellScale 2.6 and
    // the break's scale 4, each a multiple of a 104-px "base unit" that no longer exists. A size in
    // base units cannot be right for two figures at once, and measured they were not right for one:
    // the field drew 666 px (1.30x its own 512-px art — over the §73 budget) while ending 42 px above
    // the champion's feet, and the barrier drew a 132-px dome on a 412-px hunter. Both sizes are
    // RATIOS of the figure now, authored in VfxProfiles beside every other placement number.

    /// <summary>What the field looks like when it is only existing.</summary>
    private const float AuraRest = 0.38f;
    /// <summary>...and at the instant it bites.</summary>
    private const float AuraPeak = 1f;
    /// <summary>How long the flare takes to fall back to rest.</summary>
    private const float AuraSpikeSeconds = 0.42f;

    private int _auraTotal, _auraTotalMs = -1;

    /// <summary>The pending aura tick's total as one plain number over the pack's centre (see the Strike case).</summary>
    private void FlushAuraTotal()
    {
        if (_auraTotal > 0) SpawnDamage(_auraTotal, TargetSlot(), crit: false, skill: false);
        _auraTotal = 0;
        _auraTotalMs = -1;
    }

    // ADR-011: the projectile action being performed (one at a time: the champion acts once per beat), and
    // the authored timing its clip plays by. Both null for every action that has no recipe.
    private IActionPerformance? _performance;
    private ActionClipTiming? _clipTiming;

    // the performance that handed the figure to _performance, while its impact fades and its lunge comes home
    private IActionPerformance? _outgoing;

    // THE ACTION REVIEW VIEWS (capture only; the game never sets them): effects off (does the animation read
    // alone?), champion off (does the force path read alone?), and the hand sockets drawn as crosshairs.
    private static readonly bool ShotNoVfx = Environment.GetEnvironmentVariable("RH_SHOT_NOVFX") == "1";
    private static readonly bool ShotNoChamp = Environment.GetEnvironmentVariable("RH_SHOT_NOCHAMP") == "1";
    private static readonly bool ShotSockets = Environment.GetEnvironmentVariable("RH_SHOT_SOCKETS") == "1";

    // RH_PRESENT_TRACE: the champion frame last logged, so a frame is logged when it CHANGES.
    private string? _traceClip;
    private int _traceFrame = -1;

    /// <summary>Seconds into the committed clip, at its speed — what the strip is drawn at.</summary>
    private float ClipSeconds => (_playheadMs - _clipStartMs) / 1000f * _clipSpeed;

    /// <summary>The world's corruption tier (0..CorruptionScaling.MaxTier), host-fed. It tints every
    /// creature and boss (CorruptionLook.Enemy), names the boss by its epithet and prints on the header.</summary>
    public int CorruptionTier { get; set; }

    /// <summary>Settings' quality-of-life switches (playtest 2026-08-23): the fight's text and effects.</summary>
    public bool ShowDamageNumbers { get; set; } = true;
    public bool ShowSkillCallouts { get; set; } = true;
    public bool ShowHitEffects { get => _vfx.Enabled; set => _vfx.Enabled = value; }
    public bool ShowScreenFlash { get; set; } = true;
    private Color EnemyTint
    {
        get { var e = CorruptionLook.For(CorruptionTier).Enemy; return new Color(e.R, e.G, e.B); }
    }

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
        // the shadow stays on the GROUND when a performed action lifts him (a melee return's hop), and tightens as
        // he rises: the lift is the one thing that moves the box off ChampBox's floor
        var air = Math.Clamp((ChampBox.Bottom - box.Bottom) / (box.Height * 0.1f), 0f, 1f);
        ActorShadow(b, box.Center.X, Math.Max(box.Bottom, ChampBox.Bottom),
                    (int)(box.Width * (0.62f - 0.18f * shadowEase) * (1f - 0.3f * air)), 46, (0.6f - 0.25f * shadowEase) * (1f - 0.4f * air));

        var tint = dead && !hasDeathClip
            ? Color.Lerp(new Color(0x6A, 0x60, 0x5C), new Color(0x2A, 0x26, 0x24), ease) * (1f - 0.45f * ease)
            : dead ? Color.Lerp(Color.White, new Color(0x8A, 0x84, 0x80), ease * 0.6f)
            : Color.White;

        // ATTACK and CAST do not loop. They are the ONE committed clip UpdateChampionClip aimed at the
        // next beat, drawn at its own clock — never chosen here, never re-chosen mid-swing (see that
        // method for the snapping this replaced). DevSwingPhase forces the ATTACK clip and its phase
        // (the fightswing fixture).
        var fixture = DevSwingPhase is not null && !dead;
        var clip = hasDeathClip ? "death"
            : fixture ? "attack"
            : !dead && _clipName is not null ? _clipName
            : "idle";
        var seconds = DevSwingPhase is { } ph && !dead
            ? ph * StrikeSeconds
            : hasDeathClip ? (DownedSeconds - _downedTimer)   // plays through, then CLAMPS on the last frame
            : !dead && _clipName is not null
                ? (_clipTiming is { } authored ? (authored.FrameAt(_playheadMs - _clipStartMs) + 0.5f) / ChampionFps : ClipSeconds)
            // THE IDLE RESTARTS FROM ITS FIRST FRAME after an action (ADR-011): an authored action ends on the
            // idle's first pose, and a loop resumed at a random phase snapped mid-breath.
            : _anim - _idleFrom;

        // A DEAD CHAMPION WITHOUT A DEATH CLIP HOLDS ITS LAST POSE — the fallback freezes the idle.
        if (dead && !hasDeathClip) seconds = 0f;

        // The generated strip, then the idle strip, then the base sprite, then a block. A missing or rejected
        // clip falls back to the still design, so a character whose strip did not survive the quality gate
        // stands there as themselves rather than vanishing. No mirroring: the art faces right (ArtFacesLeft).
        var loop = clip == "idle";
        // MOST SPECIFIC FIRST: the character's own clip for this Form, then the generic attack/cast it
        // stands in for. See Character.StripKeys — the art arrives a character at a time and nothing
        // may blank out while it does.
        if (PresentTrace.Enabled)
        {
            var traceFrame = loop ? (int)(seconds * ChampionFps) % 8 : Math.Clamp((int)(seconds * ChampionFps), 0, 7);
            if (clip != _traceClip || traceFrame != _traceFrame)
            {
                _traceClip = clip;
                _traceFrame = traceFrame;
                PresentTrace.Log("champ-frame", $"{clip}\tframe={traceFrame}\tbox={box.X},{box.Y},{box.Width},{box.Height}");
            }
        }
        foreach (var key in Character.StripKeys(clip))
            if (ActorSprite(b, key, box, seconds, ChampionFps, loop, tint, -1f,
                               flip: ChampionFacesRight, record: VfxSubject.Champion, placeAs: PlacedAs(key))) return;
        if (ActorSprite(b, Character.StripKey("idle"), box, dead ? 0f : _anim - _idleFrom, ChampionFps, loop: true, tint, -1f,
                           flip: ChampionFacesRight, record: VfxSubject.Champion)) return;

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

    /// <summary>
    /// The lowest y the host's overlays reach this frame — the welcome toast's or the lesson card's foot
    /// — or 0 when nothing hangs under the header stack. The callouts keep under it.
    /// </summary>
    /// <remarks>
    /// The champion's skill names stack five lanes UP from its head and the damage numbers five lanes up
    /// from the creatures' shoulders; at 150 % the lanes are 79 px each and the toast's foot is at y≈400,
    /// so SNARE printed as "SNA" behind the toast's edge and "-4 JAWS" lost its sign (release polish
    /// 2026-09-05, hunt-01). A callout that would rise into the overlay is held at its foot instead.
    /// </remarks>
    public int OverlayFloor { get; set; }

    private void DrawCallouts(SpriteBatch b)
    {
        var floor = OverlayFloor > 0 ? OverlayFloor : HeaderStackBottom;
        foreach (var c in _callouts)
        {
            var rise = (int)((1f - c.Life) * 40f);
            var px = c.Px <= 0 ? SayPx : c.Px;
            var y = Math.Max(c.Y - rise, floor + UiMetrics.Space(4));
            var fade = Math.Clamp(c.Life * 1.8f, 0f, 1f);
            // Through the dock's one-pixel shadow, so a word over a pale burst or a bright floor keeps its edge.
            ShadowText(b, c.Text, c.X - _ui.MeasureBig(c.Text, px) / 2, y, c.Color * fade, px);
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
    /// DEV: run a whole descent to its end immediately, so the fall — and the banner that points at
    /// the log — can be captured. The report itself is photographed through the log (runlog fixture).
    /// </summary>
    /// <remarks>
    /// The report only exists after a run ends, and when a run ends is a property of the build — so a
    /// timed screenshot can never reliably catch it. This is the same reasoning as RH_SHOT_SWING: the
    /// states that most need checking are the ones a clock cannot be aimed at.
    /// </remarks>
    /// <summary>
    /// DEV: hold the fallen beat open instead of restarting after the recovery beat.
    /// </summary>
    /// <remarks>
    /// The recovery beat is 1.6s and a headless capture lands at frame 60, which ought to be inside it —
    /// but the first frames of a run carry loading time, so the timer had already expired and the shot
    /// caught a fresh descent every time. Holding is the honest fix for a fixture: it changes when the
    /// screen leaves, not what it shows.
    /// </remarks>
    public bool DevHoldReport { get; set; }

    /// <summary>DEV: freeze in the downed beat with the flash held and the black held OFF, to photograph the collapse.</summary>
    /// <remarks>
    /// Separate from <see cref="DevHoldReport"/> because the two jobs are different and conflating them
    /// broke the capture: holding is what stops the timer running out and restarting the run, and this
    /// is what keeps the fall's own presentation from covering the body — the flash stays at full so the
    /// shot can prove it reaches the corners, and the black never paints whatever the clocks say. The
    /// first attempt cleared the hold in order to suppress, so the beat simply expired mid-capture and
    /// photographed a fresh wave instead.
    /// </remarks>
    public bool DevShowFall { get; set; }

    /// <summary>
    /// DEV: hold the BOSS INCOMING announcement up so it can be photographed.
    /// </summary>
    /// <remarks>
    /// It lives for a second and a half in the break before a boss wave, on a timer no capture can
    /// reach, so nobody had ever looked at it against the header above it — which is how its plate
    /// came to sit at a literal y that a taller header paints over. A state no fixture can pose is a
    /// state nobody has checked; this is the pose. Null in play, always.
    /// </remarks>
    public bool DevBossCall { get; set; }

    /// <summary>
    /// DEV: pose the wave's ARRIVAL at <paramref name="progress"/> — 1 fully off-stage, 0 just landed.
    /// </summary>
    /// <remarks>
    /// The entrance had no fixture at all: DevStart clears it (a fight pose must not sit behind a
    /// 0.8-second gate), and every other capture lands mid-fight. So the one state the player named —
    /// "enemies spawn instantly when we start; they don't walk in" — was also a state no capture mode
    /// could photograph before or after the change.
    /// </remarks>
    public void DevPoseArrival(float progress)
    {
        // REMEMBERED, not just assigned. The capture host restarts a DevStart run once on its first
        // live frame (it pushes the region's enemy baseline), and that restart runs StartRun -> DevStart
        // again — which clears the entrance. A posed value written once is wiped before the shutter;
        // the held value is re-asserted every frame instead.
        _devArrivalPose = Math.Clamp(progress, 0f, 1f);
        _enemyEnter = _devArrivalPose.Value;
        // AND THE PLAYHEAD GOES BACK TO THE WAVE'S START. An arrival happens BEFORE the fight, and
        // DevStart leaves the replay 900 ms in — deliberately, so an ordinary fight pose catches a
        // fight rather than an empty stage. Posed at 900 ms the wave was already nearly over and the
        // first capture of this fixture photographed three empty health bars.
        _playheadMs = 0f;
        _callouts.Clear();
    }

    /// <summary>
    /// The held entrance pose, re-asserted every frame while a capture is posing one.
    /// </summary>
    /// <remarks>
    /// HELD, or the shutter never sees it. The entrance decays at 1.25/second and a capture renders 60
    /// frames before it saves, so a value written once is long gone by the time the picture is taken —
    /// the same reason DevHoldReport exists. Holding changes when the beat ENDS, not what it shows.
    /// This nullable IS the hold: a separate bool beside it was written and never read.
    /// </remarks>
    private float? _devArrivalPose;

    /// <summary>
    /// DEV: pose the DEATH TRANSITION at <paramref name="t"/> — 0 the last readable instant of the fall,
    /// 0.5 the black with the next descent already begun beneath it, 1 the stage back (<see cref="DeathTransition.At"/>).
    /// </summary>
    /// <remarks>
    /// Call after <see cref="DevRunToDeath"/>. Past the restart the restart is REAL: the fallen beat is let
    /// go and StartRun opens the next descent exactly as play does, so what stands under the black is a
    /// live wave one with its entrance walking in, not a posed corpse. HELD every frame, like the arrival
    /// pose: the host's first live frame restarts the run on its Source push, and every fall clock would
    /// otherwise have run out before the shutter. Reads Reduced Motion, so RH_SHOT_REDUCED=1 poses the cuts.
    /// </remarks>
    public void DevPoseDeathTransition(float t)
    {
        _devDeathPose = Math.Clamp(t, 0f, 1f);
        if (DeathTransition.At(_devDeathPose.Value, UiMotion.Reduced).Restarted && _hunter is { } hunter)
        {
            DevHoldReport = false;
            _downedTimer = 0f;
            StartRun(hunter);
        }
        HoldDeathPose();
    }

    /// <summary>The held transition pose, re-asserted every frame while a capture is posing one — this nullable IS the hold.</summary>
    private float? _devDeathPose;

    /// <summary>
    /// Re-assert the posed transition: its clock, and past the restart the new wave's entrance where the
    /// pose has it. AT 1 THE POSE IS LET GO: the lift's last instant is held for exactly one frame and the
    /// transition then ends on its own clock, so the host crosses the edge it crosses in play — the fall's
    /// end, which hushes the coach for a beat — rather than photographing a stage that was never in
    /// transition at all (the clock's tiny remainder paints no black: Covers reads the alpha).
    /// </summary>
    private void HoldDeathPose()
    {
        if (_devDeathPose is not { } t) return;
        if (t >= 1f) { _deathFadeIn = float.Epsilon; _devDeathPose = null; return; }
        var pose = DeathTransition.At(t, UiMotion.Reduced);
        if (pose.Restarted)
        {
            _deathFadeIn = pose.FadeInClock;
            _enemyEnter = Math.Max(0f, 1f - pose.SinceRestart * EnemyEnterPerSecond);
        }
        else _downedTimer = pose.DownedTimer;
    }

    /// <param name="fallProgress">
    /// 0 poses the instant of the fall, 1 the settled body. Anything other than null ALSO holds the fall's
    /// black off (<see cref="DevShowFall"/>), so the collapse can be photographed uncovered at any point of
    /// the beat.
    /// </param>
    /// <param name="poseLimit">
    /// DEV: pose the log's diagnostic for THIS limit. The death is the real seeded one; only the single
    /// measurement that names the limit is set past its threshold (and the ones that would outrank it
    /// held under theirs), so ARMOUR, REACH and SUSTAIN each have a picture — the three
    /// <c>fightreport</c> seeds UX V2 left owed.
    /// </param>
    public void DevRunToDeath(Hunter hunter, float? fallProgress = null, RunLimit? poseLimit = null)
    {
        DevHoldReport = true;
        DevStart(hunter, 900f, 14f);

        // DevStart already plays wave 1, and at these numbers that wave can be the one that kills. The
        // first version checked _run.Over at the TOP of the loop and so broke out without ever building
        // the report — the capture caught a fresh descent every time.
        var guard = 0;
        while (_run is { Over: false } && guard++ < 400) _outcome = _descent.PushWave();

        if (_run is null) return;
        var previous = Log.PreviousIn(RegionId);

        // THE REAL COMPARISON, not a hard true. Forcing the flag made the capture print a NEW RECORD
        // title directly above a diff line showing the depth had FALLEN — a report contradicting
        // itself in the same panel. A fixture that lies cannot catch the bug it is posing for.
        var report = _run.Report(isRecord: _run.Wave > (previous?.Depth ?? 0));   // the same path the game takes
        report = poseLimit switch
        {
            // RunReport.Limit reads these in order: Stalled, Armour (absorbed ≥ 0.45), Reach (creatures
            // ≥ 2.5 and targets < half of them), Sustain (health lost ≥ 0.18), else OutScaled.
            RunLimit.Armour => report with { AbsorbedFraction = 0.52f },
            RunLimit.Reach => report with
            {
                AbsorbedFraction = MathF.Min(report.AbsorbedFraction, 0.30f),
                CreaturesPerWave = MathF.Max(report.CreaturesPerWave, 3.4f), TargetsPerActivation = 1.0f,
            },
            RunLimit.Sustain => report with
            {
                AbsorbedFraction = MathF.Min(report.AbsorbedFraction, 0.30f),
                TargetsPerActivation = MathF.Max(report.TargetsPerActivation, report.CreaturesPerWave),
                HealthLostPerWaveFraction = 0.27f,
            },
            _ => report,
        };
        Log.Add(report);
        _mode = Mode.Downed;
        _downedTimer = DownedSeconds;
        _bannerTimer = 0f;   // the wave-cleared banner has no business over a fall
        _fellWave = Math.Max(1, _run.Wave + 1);
        _replayWave = _fellWave;   // the header says the wave that fell, not the wave DevStart played (audit P1-8)

        if (fallProgress is { } fp)
        {
            // Wind the downed clock to a chosen point in the fall and FREEZE it there. The collapse is
            // driven by how much of the beat has elapsed, so this is the only way to photograph a moment
            // of it — a capture takes one frame, and the fall is over in half a second.
            _downedTimer = DownedSeconds * (1f - Math.Clamp(fp, 0f, 1f));
            DevShowFall = true;   // hold stays ON so the beat cannot expire mid-capture
            // The flash is part of the fall, and the capture must show it covering the WHOLE canvas,
            // corners included — that was its exact bug. Posed at full strength; Update holds it
            // while DevShowFall is set.
            _deathFlash = 1f;
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
        // NO ARRIVAL UNDER THE RIG. BeginWave arms the entrance for every wave open, and UpdateFight
        // holds the replay until it has finished — so a fixture that seeks 900 ms into a wave would
        // photograph frame zero of an entrance instead of the fight it poses. Every fight* capture
        // goes through here. The one fixture that WANTS an entrance keeps its posed value.
        _enemyEnter = _devArrivalPose ?? 0f;
        _playheadMs = 900f;
    }

    /// <summary>
    /// DEV: jump the replay <paramref name="seconds"/> into the wave — every beat before that lands
    /// SILENTLY (health only: no numbers, no effects), so the frame shows the bars where the fight has
    /// taken them and then the beats that follow, at their real size, over them.
    /// </summary>
    /// <remarks>
    /// A capture lands at frame 60, about a second into the wave, and the `fight` fixture's creatures are
    /// deliberately thick — so a second in, every bar is still full and a damage number has nothing
    /// visible to agree with. Seeking a few seconds ahead is the only way to photograph a number beside
    /// a bar it has already moved (playtest 2026-08-26: "the enemy bars don't drop correctly").
    /// </remarks>
    /// <remarks>
    /// PENDING, not applied here: the host pushes the region's Source down after the fixture runs, and
    /// <see cref="Update"/> restarts the run on that change — a seek applied at fixture time was thrown
    /// away with the run on the first frame (three captures at 1, 6 and 14 seconds came back pixel
    /// identical). UpdateFight applies it on the first live frame of whichever run survives.
    /// </remarks>
    public void DevSeek(float seconds) => _devSeekMs = Math.Max(0f, seconds * 1000f);

    /// <summary>
    /// DEV: hold every Active's ring at <paramref name="swept"/> of its cycle, so a COOLDOWN can be
    /// photographed. 0 is just cast, 1 the instant it comes up.
    /// </summary>
    /// <remarks>
    /// The rail's cooldown had no fixture at all. Every fight mode the rig owns runs a build whose
    /// skills come back inside one beat, so a seek to any instant photographs a rail of READY
    /// medallions — the ring, its notches, its warming edge and its comet head were all drawn by code
    /// no capture could reach. That is the exact shape of the fault this project keeps finding: a state
    /// no capture can pose is a state nobody has looked at.
    /// <para>
    /// It overrides the SHOWN sweep only. The fight underneath is untouched, the readiness word still
    /// reads off the real timing, and nothing is armed — a pose must not fire a one-shot, or the
    /// shutter would catch a ready-pop that did not happen.
    /// </para>
    /// </remarks>
    public void DevPoseCooldown(float swept) => _devCooldownPose = Math.Clamp(swept, 0f, 1f);

    private float? _devCooldownPose;

    /// <summary>
    /// DEV: the current wave's events and the playhead, as text — written beside a capture under
    /// RH_SHOT_DUMP so a pose can be checked against what the wave actually contained.
    /// </summary>
    public IEnumerable<string> DevWaveEvents()
    {
        yield return $"wave {_replayWave} playhead {_playheadMs:0} ms  health {_replay?.HealthOf(0)}  shield {_replay?.CurrentShield}/{_replay?.MaxShield}";
        if (_run is null) yield break;
        for (var i = 0; i < _run.Skills.Count; i++) yield return $"slot {i}: {_run.Skills[i].Def.Id} ({_run.Skills[i].Source})";
        foreach (var e in _run.LastWaveEvents)
            yield return $"{e.AtMs,6} ms  {e.Kind,-14} slot {e.Slot} amount {e.Amount} fromSkill {e.FromSkill}";
    }
    private float? _devSeekMs;
    private int _waveStartHealth;   // the champion's health when the replayed wave began
    private float _waveEnemyHp;     // the replayed wave's total creature health

    /// <summary>
    /// DEV: seek to just before the first wave event that <paramref name="pick"/> accepts — the first
    /// SHIELD BROKEN, the first cast of a given slot — so a capture photographs THAT instant rather
    /// than a second somebody guessed. Resolved against the live run's events on the frame the seek
    /// applies (see <see cref="DevSeek"/> for why it is pending). No such event: no seek.
    /// </summary>
    /// <param name="leadSeconds">
    /// How far before the event to land. Under the rig the seek applies two frames before the shutter
    /// (see UpdateFight), so a lead under two frames (33 ms) puts the event on the photographed frame:
    /// crossed live, callout fresh, effect on its first frame.
    /// </param>
    public void DevSeekBefore(Func<BattleEvent, bool> pick, float leadSeconds = 0.02f)
        => _devSeekPick = (pick, leadSeconds);
    private (Func<BattleEvent, bool> Pick, float Lead)? _devSeekPick;

    /// <summary>The build slot a skill occupies in the live run, or -1 — for <see cref="DevSeekBefore"/> picks.</summary>
    public int DevSlotOf(string skillId)
    {
        if (_run is null) return -1;
        for (var i = 0; i < _run.Skills.Count; i++)
            if (_run.Skills[i].Def.Id == skillId) return i;
        return -1;
    }
}
