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
using ResonanceHunter.Core.Loot;
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
    private const float FallenBeat = 0.45f;
    private const float SpoilsBeat = 0.45f;
    private const float BreathBeat = 0.20f;
    private const float WaveBreakSeconds = FallenBeat + SpoilsBeat + BreathBeat;
    private const float DownedSeconds = 1.6f;   // the recovery beat before the champion tries again

    /// <summary>How long the fallen banner outlives the recovery beat.</summary>
    /// <remarks>
    /// The banner replaced a full report popup whose exact complaint was "it closes before I can even
    /// read it" — the recovery beat is 1.6 seconds. So the banner deliberately stays up into the next
    /// descent, and the full report waits in the EXPEDITION LOG (L) for as long as the player needs.
    /// </remarks>
    private const float FellBannerSeconds = 4.5f;

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
    private static readonly Rectangle ChampBox = new(700 - 200, GroundY - 430, 400, 430);   // 760 → 700 (2026-08-28: "the champion a little left")
    // Rev 3 §16.1: one normal enemy bottom-centred at (1160,735), visible ~320px (range 280–360). A boss is
    // drawn far larger from its own anchor (see the draw), so this box is the NORMAL-enemy size only.
    private static readonly Rectangle EnemyBox = new(1380 - 218, GroundY - 440, 436, 440);   // 1320 → 1380 ("the enemies a little right")

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
    private static readonly Point BossAnchor = new(1290, GroundY + 10);   // with the pack, 60 px right
    private Rectangle _bossBodyRect, _bossFullRect;   // rendered screen rects, set by DrawBoss for the bar/overlay
    private int _bossFrame;
    private string _bossName = "BOSS";

    // package_03: one representative common enemy per Source (no Nature enemy shipped — a wisp stands in).
    private static readonly Dictionary<Source, string> EnemyForSource = new()
    {
        [Source.Body] = "bonecrawler", [Source.Mind] = "soul_leech", [Source.Nature] = "wisp",
        [Source.Machine] = "stone_sentinel", [Source.Shadow] = "shadeling", [Source.Spirit] = "rift_guardian",
    };

    /// <summary>The enemy key back out of its strip key ("shadeling_idle_strip8_512" → "shadeling").</summary>
    private static string EnemyKeyOf(string stripKey)
    {
        var cut = stripKey.IndexOf("_idle_", StringComparison.Ordinal);
        if (cut < 0) cut = stripKey.IndexOf("_attack_", StringComparison.Ordinal);
        return cut < 0 ? stripKey : stripKey[..cut];
    }

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

    /// <summary>Set by StartRun when a descent began at a checkpoint: the Dust the host must now take.</summary>
    public int CheckpointCharge { get; set; }

    /// <summary>Host-fed: the region is already conquered, so the banner says CONQUERED from wave one (review 2026-08-26).</summary>
    public bool RegionConquered { get; set; }

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

    /// <summary>One key per skill for the rail's flash and readout: the Source (a Skill event's Slot) and the Form.</summary>
    private static int SkillKey(int source, int form) => source * 16 + form;

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
        foreach (var s in Loadout.Skills)
        {
            var key = SkillKey((int)s.Source, (int)s.Form);
            var last = _replay.LastSkillBefore(end, (int)s.Source, (int)s.Form);
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
    private const float MaxClipSpeed = 2.5f;
    private float _enemyBaseHealth = 120f, _enemyBaseDamage = 9f;
    private WaveOutcome _outcome = WaveOutcome.Cleared;

    // Feedback so a WATCHED fight actually reads: a wave-cleared banner, the next enemy sliding IN from
    // the right, and a red flash when the champion falls. Without these, waves passed silently and the
    // playtest note was exactly that — "I can't tell what's happening".
    private float _enemyEnter;    // 1 → 0: how far off-screen-right the new enemy still is
    private float _bannerTimer;   // 1.2 → 0: the "WAVE N CLEARED" flash
    private string _bannerText = "";
    private float _deathFlash;    // 1 → 0: red flash on a fall — drawn over the WHOLE canvas (see Draw)
    private float _fellTimer;     // seconds left on the fallen banner (FellBannerSeconds)
    private int _fellWave = 1;    // the wave the champion fell at, named by that banner
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

    // The three combat-callout sizes, from the one place the game keeps its type. They were three
    // private constants here while UiTypography carried a DamageNormal and a DamageCritical that
    // nothing drew with — the same three sizes, written down twice, disagreeing.
    private const int DamagePx = UiTypography.DamageNormal;
    private const int SkillHitPx = UiTypography.DamageSkill;
    private const int CritPx = UiTypography.DamageCritical;

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
    private static readonly Rectangle ArenaRect = new(492, 100, 1062, 940);

    /// <summary>
    /// The spotlight cut-outs for one of this screen's tour cards, in the screen's own coordinates. The
    /// first is the one the caption card is placed beside; a target that is not this screen's gets none.
    /// </summary>
    /// <remarks>
    /// Hand-measured against this screen's real layout (the hunter panel at (196,20,420,205), the stage
    /// header at (630,18,560,135), the SKILLS rail at (190,236,286,350) for one skill, the right column
    /// from x 1570 down to the CHEST FILTER row — closed, as a fresh save shows it — the champion in
    /// ChampBox, the pack right of it) with a margin of about ten pixels so
    /// the frame art is inside the light, not cut by it. A fresh save is the only state this ever draws
    /// over, so the one-skill rail height is the right one. The fight screen is not inset, so these are
    /// already chrome coordinates and the host adds no margin of its own.
    /// </remarks>
    internal static Rectangle[] Spotlights(TourTarget target) => target switch
    {
        TourTarget.Champion => new[] { new Rectangle(470, 560, 460, 450) },
        TourTarget.Enemies => new[] { new Rectangle(940, 560, 680, 450), new Rectangle(620, 8, 580, 152) },
        TourTarget.HunterHud => new[] { new Rectangle(186, 10, 440, 225) },
        TourTarget.CurrencyPills => new[] { new Rectangle(1440, 4, 400, 84) },
        TourTarget.Skills => new[] { new Rectangle(180, 226, 306, 370) },
        // Idle rate, errands, the filter row (closed) — down to wherever the filter row LAST drew. The
        // rail's height depends on how many errands are up, and a new game now holds the welcome chest,
        // so the fixed 268 px measured for an empty rail sliced the filter row in half on every first
        // intro (review 2026-08-26). The screen draws before the tour asks, so the measure is fresh.
        TourTarget.RightColumn => new[] { new Rectangle(1560, 100, 350, Math.Max(268, s_railBottom - 100)) },
        TourTarget.NavRail => new[] { new Rectangle(0, 0, 184, 1080) },
        TourTarget.GuideStrip => new[] { new Rectangle(456, 936, 1008, 130) },
        _ => Array.Empty<Rectangle>(),
    };
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

        // THE FALL IS NOT COVERED ANY MORE. A full report panel used to slide over the body and was
        // gone with the next descent — playtest ten: "it closes before I can even read it". The report
        // lives in the EXPEDITION LOG (L) now; the fall shows only a short banner naming the wave and
        // pointing there. DevShowFall keeps even the banner off, so the collapse can be photographed.
        if (_mode == Mode.Downed)
            return DevShowFall ? HuntOverlay.None : HuntOverlay.HunterDown;
        // The banner outlives the recovery beat (FellBannerSeconds), so it stays readable into the
        // next descent — and it outranks the wave banner, because the fall is the news.
        if (_fellTimer > 0f) return HuntOverlay.HunterDown;
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
        _enemySinceHit += dt;
        _enemyLunge = Math.Max(0f, _enemyLunge - dt * 5f);
        // 1.25, not 2.5: the slide-in was over in 0.4s, which is too quick to register as creatures
        // ARRIVING rather than simply appearing. Twice as long, and the beat before it is now empty
        // stage, so the entrance has something to be an entrance from.
        _enemyEnter = Math.Max(0f, _enemyEnter - dt * 1.25f);   // the new enemy slides in over ~0.8s
        _bannerTimer = Math.Max(0f, _bannerTimer - dt);
        _bossIncomingTimer = Math.Max(0f, _bossIncomingTimer - dt);
        // The flash holds while the fall fixture is posing it — a capture must be able to photograph
        // the one frame it exists to check (does the flash cover the WHOLE screen, corners included).
        if (!DevShowFall) _deathFlash = Math.Max(0f, _deathFlash - dt * 1.5f);
        _fellTimer = Math.Max(0f, _fellTimer - dt);
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
            // The fall's presentation belongs to the region it happened in — carried across travel,
            // the old "YOUR CHAMPION FELL" banner outranked the new region's own banners for six
            // seconds (review 2026-08-24). The flash clear is defensive; it decays in under a second.
            _fellTimer = 0f;
            _deathFlash = 0f;
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
                if (DevHoldReport) break;   // capture fixture: keep the fallen beat up instead of restarting
                _downedTimer -= dt;
                if (_downedTimer <= 0f) StartRun(hunter);
                break;
        }
    }

    /// <summary>Counts descents, so each one seeds its own compositions. See SoloExpedition.RunIndex.</summary>
    private int _runIndex;

    /// <summary>What the run's build was composed from — compared at every wave boundary (see BeginWave).</summary>
    private string _buildStamp = "";
    private string BuildStamp()
        => $"{Loadout.Signature}|{Tree.OwnedIds.Count}:{string.Join(",", Tree.OwnedIds)}|{Mastery.Taken.Count}:{string.Join(",", Mastery.Taken)}|{Character.Id}";

    /// <summary>Compose the build and refresh what the screen caches off it (the CHARGE pill).</summary>
    private Build ComposeBuild(Hunter hunter)
    {
        var build = Loadout.ToBuild(Tree, Mastery, Character);
        _buildStamp = BuildStamp();
        // The CHARGE pill only exists when the pool does.
        var trig = build.Triggers(hunter);
        _chargeLive = trig.Contains(BuildTrigger.Rend) || trig.Contains(BuildTrigger.Capacitor)
                      || trig.Contains(BuildTrigger.Dynamo) || trig.Contains(BuildTrigger.Lodestone);
        _chargeCap = trig.Contains(BuildTrigger.Capacitor)
            ? SoloBattle.ChargeCapExtended : SoloBattle.ChargeCap;
        return build;
    }

    private void StartRun(Hunter hunter)
    {
        var build = ComposeBuild(hunter);
        _recordToBeat = BestDepthHere;   // before a wave is pushed, or the run competes with itself
        _chargeNow = 0;

        // A NEW DESCENT IS A NEW CHAMPION. The rail carries a skill's place in its cycle across wave
        // boundaries (see _carryBeats), and carrying it across a DEATH would open the next run with
        // rings inherited from the corpse — the sim mints a fresh Champion here, cooldowns and all.
        _carryBeats.Clear();
        _carryMs.Clear();
        _replayEndMs = 0f;

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
        // A CHECKPOINT START. The region's chosen start wave (Map screen) skips the waves already
        // cleared, and the Memory Dust it costs is charged through the host (CheckpointCharge) — the
        // host fed StartWave = 0 when the Dust was not there, so a start here is always affordable.
        if (StartWave > 0)
        {
            _run.StartAtWave(StartWave);
            Deepest = Math.Max(Deepest, StartWave);
            CheckpointCharge += Checkpoints.DustCost(StartWave);
        }
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
            var aura = Loadout.Skills.FirstOrDefault(k => k.Form == Form.Aura);
            _auraColour = aura is null ? null : SourceColor.GetValueOrDefault(aura.Source, Bone);
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
        _outcome = _run.PushWave();

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

        _replay = new WaveReplay(_run.LastWaveEvents, startHealth, maxHealth, enemyHp);
        _replayEndMs = _run.LastWaveEvents.Count == 0 ? 0f : _run.LastWaveEvents.Max(e => e.AtMs);
        _diedAt.Clear();          // the previous wave's fallen are gone with its replay
        _creatureRect.Clear();
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
    /// <param name="crit">A Trap's bite: gold, larger, and it lingers.</param>
    /// <param name="skill">A cast's hit, drawn a size up from the auto-swing's.</param>
    private void SpawnDamage(int amount, int slot, bool crit, bool skill)
    {
        if (!ShowDamageNumbers) return;   // settings: DAMAGE NUMBERS off
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
            Text = crit ? $"-{amount:N0} CRITICAL" : $"-{amount:N0}",
            Color = crit ? Gold : skill ? UiKit.Vellum : Bone,
            // Over the creature it struck, not the row's centre: in a swarm the row centre is the gap
            // between two creatures, and a number there names neither of them.
            X = EnemyPoint(slot, 0f).X,
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

        if (_breakTimer > 0f)
        {
            _enemyWindup = 0f;
            _clipName = null;   // a clip never survives the wave it was swung in
            _breakTimer -= dt;
            if (_breakTimer <= 0f) { BeginWave(); _enemyEnter = 1f; }
            return;
        }

        // Hold the fight until the new enemy has finished sliding in — otherwise the champion swings at empty
        // air while the enemy is still off to the right ("hunter hits before the enemy arrives").
        if (_enemyEnter > 0f) { _enemyWindup = 0f; _clipName = null; return; }

        // DEV: the fixture's seek (DevSeek) — the beats before it land silently, health only.
        if (_devSeekMs is { } seek)
        {
            _devSeekMs = null;
            _playheadMs = seek;
            _replay.Advance(seek);
            _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(seek);
            _nextChampStrikeMs = _replay.NextChampionStrikeAfter(seek);
        }

        _playheadMs += dt * 1000f * _speedMul;
        // THE AURA'S PULSE, ON THE WALL CLOCK. It used to be raised by an aura's damage event, which
        // meant a tick landing on a cast's own millisecond was read as that cast's blow and raised
        // nothing — and in a real four-skill build that happened often enough that the field looked
        // absent (playtest 2026-08-30: "there is no aura effect on screen"). An always-on field is
        // always on: while a wave is running and the build carries an Aura, it pulses on its own beat.

        // The aura's tick prints ONE number, and it prints on time: it used to wait for the NEXT tick's
        // events to arrive, which put it 500 ms late over whatever creature was alive by then.
        if (_auraTotal > 0 && _playheadMs > _auraTotalMs + FormBehaviour.AuraTickMs * 0.5f) FlushAuraTotal();
        if (_hitFlash.Count > 0)
            foreach (var key in _hitFlash.Keys.ToList())
            {
                var left = _hitFlash[key] - dt * 5f;   // ~200 ms of life; FlashAt shapes it
                if (left <= 0f) _hitFlash.Remove(key); else _hitFlash[key] = left;
            }
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

        // The champion's clip: one committed swing at a time, aimed at the next beat.
        UpdateChampionClip();

        var batch = _replay.Advance(_playheadMs);
        // The beat a cast lands on. A Skill event precedes the Strike events its hit produces, at the
        // same timestamp, so a Strike stamped with the cast's beat is that cast's blow — graded a size
        // up, and gold when the cast was a Trap. Anything else at another beat is the auto-swing.
        var skillAtMs = -1;
        var trapAtMs = -1;
        var auraAtMs = -1;
        for (var bi = 0; bi < batch.Count; bi++)
        {
            var e = batch[bi];
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
                    // The swing lunges; a cast already has its clip (UpdateChampionClip aims it at the beat).
                    if (!e.FromSkill) _champLunge = 1f;
                    _nextChampStrikeMs = _replay.NextChampionStrikeAfter(e.AtMs);
                    // The swing's thud at full weight; a skill's landing blows quieter — the cast's breath
                    // already announced them, and four projectile impacts on top of it were "two sounds at
                    // once" (playtest 2026-08-26). An aura tick is silent: it hums, it does not strike.
                    if (!auraTick) Sound?.Play("sfx_hit", e.FromSkill ? 0.22f : 0.38f, vary: 0.06f);
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
                    else if (e.Amount > 0) SpawnDamage(e.Amount, e.Slot, crit: e.FromSkill && e.AtMs == trapAtMs, skill: e.FromSkill && e.AtMs == skillAtMs);
                    // The creature that took it FLASHES — but ONLY for a real blow, and only once its last
                    // flash has finished. An aura ticks twice a second and the swing lands every beat, and
                    // together they strobed the pack ("the enemy blinks like a disco ball", playtest
                    // 2026-08-30); an always-on field has its own picture (the pulse) and needs no flash.
                    if (!auraTick && _hitFlash.GetValueOrDefault(e.Slot) <= 0f) _hitFlash[e.Slot] = 1f;
                    if (!auraTick && (_strikeCount++ & 1) == 0)   // every other blow: a small, quiet puff
                    {
                        var (hx, hy) = EnemyPoint(e.Slot, 0.45f);
                        _vfx.Play("fx_weakhit", hx, hy, scale: EnemyScale(e.Slot, 0.6f), fps: 16f, tint: Steel);
                    }
                    break;
                }
                case BattleEventKind.EnemyStrike:
                    _enemyLunge = 1f;
                    _enemySinceHit = 0f;
                    _nextEnemyStrikeMs = _replay.NextEnemyStrikeAfter(e.AtMs);
                    Sound?.Play("sfx_hit", 0.30f, pitch: -0.25f, vary: 0.06f);   // same thud pitched down: taking, not giving
                    _vfx.Play("fx_hit", ChampBox.Center.X, ChampBox.Center.Y + 40, scale: 2, fps: 14f, tint: Ember);
                    break;
                case BattleEventKind.Skill:
                    var form = (Form)e.Amount;
                    _skillFlash[SkillKey(e.Slot, e.Amount)] = 0.42f;
                    var (text, colour) = CalloutFor(form);
                    if (ShowSkillCallouts) Say(text, colour);   // settings: SKILL NAMES hides exactly this
                    // Slot carries the casting skill's Source (WaveModel.BattleEvent) — the effect is the
                    // Form's shape in the Source's colour, which is the whole hexagon in one flash.
                    // The creature this cast HITS is the one its own Strike in the same batch names — the
                    // batch has already applied the kill, so "first alive" would point past a creature the
                    // cast just killed and the flash would land on its neighbour.
                    int? castTarget = null;
                    for (var k = bi + 1; k < batch.Count && batch[k].AtMs <= e.AtMs + 1; k++)
                        if (batch[k].Kind == BattleEventKind.Strike) { castTarget = batch[k].Slot; break; }
                    PlayFormVfx(form, (Source)e.Slot, castTarget);
                    Sound?.Play("sfx_cast", 0.42f, vary: 0.06f);
                    if (form == Form.Trap) Sound?.Play("sfx_crit", 0.46f, vary: 0.06f);   // the crit-graded blow (SpawnDamage's crit flag below)
                    skillAtMs = e.AtMs;                          // the Strikes at this beat are this cast's
                    if (form == Form.Trap) trapAtMs = e.AtMs;    // ...and a Trap's are the crit-graded ones
                    break;
                case BattleEventKind.Heal:
                    if (ShowDamageNumbers) Say($"+{e.Amount}", Verdant);   // a number — follows DAMAGE NUMBERS; UNDYING below always shows
                    // Centred so the effect's FOOT (its ground ring) sits on the ground line: at scale 3 the
                    // effect is 312 px tall, and centred on the champion's box its ring floated at the waist
                    // (playtest 2026-08-28: "the heal effect's ground part appears at the character's middle").
                    _vfx.Play("fx_heal", ChampBox.Center.X, ChampBox.Bottom - 156, scale: 3, fps: 10f, tint: Verdant);
                    break;
                case BattleEventKind.Shield:
                    Say("UNDYING", Gold);
                    _vfx.Play("fx_shield", ChampBox.Center.X, ChampBox.Center.Y - 20, scale: 4, fps: 12f, tint: Gold);
                    break;
                case BattleEventKind.Down:
                    Sound?.Play("sfx_champ_down", 0.62f, vary: 0.03f);
                    _vfx.Play("fx_death", ChampBox.Center.X, ChampBox.Center.Y, scale: 4, fps: 9f);
                    break;
                case BattleEventKind.EnemyDown:
                {
                    // The creature FALLS (its death clip, from this moment), and the plume rises over the
                    // body half a second later — after the fall, not instead of it. Playtest: "düşman
                    // ölüyor ama önünde bir duman animasyonu çıkıyor, herkesin ölme animasyonu olması lazım."
                    _diedAt[e.Slot] = _anim;
                    // A boss has its own fall (sfx_boss_down: deeper, longer, a second thump when the mass lands)
                    // rather than the creature's death pitched down — a pitched-down crumble is a slower crumble.
                    if (_isBossWave) Sound?.Play("sfx_boss_down", 0.46f, vary: 0.03f);
                    else Sound?.Play("sfx_enemy_down", 0.36f, vary: 0.06f);
                    var (dx, dy) = EnemyPoint(e.Slot, 0.55f);
                    // A boss falling is the loudest beat in the fight: the starburst AND the plume.
                    if (_isBossWave) _vfx.Play("fx_crit", dx, dy - 40, scale: EnemyScale(e.Slot, 1.2f), fps: 10f, tint: Gold);
                    _vfx.Play("fx_death", dx, dy, scale: EnemyScale(e.Slot, 0.9f), fps: 9f, delay: 0.45f);
                    break;
                }
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
            // The REPORT is taken here, at the exact moment the run ended, and goes STRAIGHT into the
            // EXPEDITION LOG. The popup that used to show it covered the fall and closed before it
            // could be read; what the player sees now is the short fallen banner (the HunterDown
            // overlay), which names the wave and points at the log (L), where the report keeps.
            Log.Add(_run!.Report(isRecord: _run.Wave > _recordToBeat));   // kept and saved — see the Log property
            LogDirty = true;

            _fellWave = Math.Max(1, _replayWave);
            _fellTimer = DownedSeconds + FellBannerSeconds;
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
    private void PlayFormVfx(Form form, Source source, int? hitSlot = null)
    {
        var glow = SourceGlow(source);
        // ON the creature being hit, sized to it: the sim lands single-target Forms on the first living
        // creature, so that is where the flash goes — a swarm creature gets a small one, a bruiser a big
        // one, a boss the biggest (EnemyScale). A Trap bursts under the whole row; the champion's own
        // Forms stay on the champion.
        var target = hitSlot ?? TargetSlot();
        var (tx, ty) = EnemyPoint(target, 0.45f);
        switch (form)
        {
            case Form.Strike:
                _vfx.Play(FxFor(Form.Strike), tx, ty, scale: EnemyScale(target, 1.25f), fps: 12f, tint: glow);
                break;
            case Form.Projectile:
                // The bolt flies left-to-right inside its own frame, so it is centred between the two figures.
                // SCALED TO THE GAP IT CROSSES, not to the creature it hits: a Swarm creature used to
                // get a 208 px bolt and a boss a 520 px one for the same flight. And 8 fps, not 14 —
                // only the strip's first three frames carry the streak (measured coverage 5/11/9% then
                // under 2%), so at 14 fps the bolt was over in 214 ms (review 2026-08-30).
                _vfx.Play(FxFor(Form.Projectile), (ChampBox.Right + tx) / 2, ty,
                          scale: Math.Clamp((tx - ChampBox.Right) / 104, 2, 5), fps: 8f, tint: glow);
                break;
            case Form.Aura:
                _vfx.Play(FxFor(Form.Aura), ChampBox.Center.X, ChampBox.Center.Y + 20, scale: 3, fps: 12f, tint: glow);
                break;
            case Form.Trap:
                _vfx.Play(FxFor(Form.Trap), _rowCentreX, EnemyPoint(target, 0.7f).Y, scale: EnemyScale(target, 1.3f), fps: 12f, tint: glow);
                break;
            case Form.Mark:
                _vfx.Play(FxFor(Form.Mark), tx, ty, scale: EnemyScale(target, 0.9f), fps: 12f, tint: glow);
                break;
            case Form.Transformation:
                _vfx.Play(FxFor(Form.Transformation), ChampBox.Center.X, ChampBox.Center.Y, scale: 3, fps: 12f, tint: glow);
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
        if (DevForceBoss) { _vfx.Clear(); _callouts.Clear(); _deathFlash = 0f; _bannerTimer = 0f; _fellTimer = 0f; }

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
        _ui.Device.ScissorRectangle = ArenaRect;
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
        DrawLogButton(b, hit, clicked && !_logOpen);
        // Under the EXPEDITION LOG's full-screen scrim the rail is furniture — no TAKE ONLY edits, no errands.
        DrawRightColumn(b, hit, clicked && !_logOpen);
        // Rail frame first, then its contents. The old order relied on the rail being TRANSLUCENT — the
        // skill dock was drawn under it and read through as a washed-out ghost. With a real opaque panel
        // that hid the dock outright.
        DrawBattleControls(b, hit, clicked && !_logOpen);
        DrawSkillDock(b);
        HeaderStackBottom = StageHeaderBottomY;                   // the strip or the boss bar lowers it
        if (_isBossWave) DrawBossBar(b);                          // §10/§12: screen-space, NOT arena-clipped
        DrawEnemyLine(b);                                          // the wave's live strip under the header
        // The red flash on a fall covers the whole 1920x1080 canvas, so it draws in this UNCLIPPED
        // pass, over the rails and panels too — inside the arena batch the scissor cut it down to the
        // arena rectangle. The settings' SCREEN FLASH switch still governs it.
        if (_deathFlash > 0f && ShowScreenFlash) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Ember * (_deathFlash * 0.35f));
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
        // The death flash used to be drawn HERE, inside the arena pass — whose rasterizer scissors
        // everything to ArenaRect, so the "full screen" flash was silently cropped to the arena
        // rectangle (playtest ten: "it only glows in a limited area"). It draws in the unclipped HUD
        // pass now (see Draw), where full screen actually means full screen.

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
    private float _enemySinceHit = 99f;
    /// <summary>How long an enemy's follow-through stays on its attack clip: three frames at ~10 fps.</summary>
    private const float EnemyFollowSeconds = 0.3f;

    /// <summary>The wave whose replay is on screen (1-based) — see BeginWave; the header and the boss flag read it.</summary>
    private int _replayWave;

    // ── WHERE EACH CREATURE IS, published by the draw for the update (2026-08-23). ─────────────────
    //    Every enemy-side effect used to spawn at the ROW's centre — right for one creature, 150 px off
    //    for a swarm of five, and the death plume rose beside the wrong body. The draw now records each
    //    creature's on-screen rectangle (slot → centre x, top, height) and the effects ask for a point on
    //    the creature the event names (EnemyDown and Strike carry the slot) or the one the sim is hitting.
    private readonly Dictionary<int, (int X, int Top, int H)> _creatureRect = new();
    /// <summary>Screen time at which each creature died — the death clip plays from it.</summary>
    private readonly Dictionary<int, float> _diedAt = new();
    private const float DeathFps = 10f;              // 8 frames in 0.8 s
    private const float DeathHoldSeconds = 0.6f;     // the body lies there
    private const float DeathFadeSeconds = 0.45f;    // then fades out

    private void PublishCreature(int slot, Rectangle box) => _creatureRect[slot] = (box.Center.X, box.Y, box.Height);

    /// <summary>A point on creature <paramref name="slot"/>, <paramref name="yFrac"/> of the way down its body;
    /// the row's old centre point if the slot has not been drawn yet.</summary>
    private (int X, int Y) EnemyPoint(int slot, float yFrac)
        => _creatureRect.TryGetValue(slot, out var r) ? (r.X, r.Top + (int)(r.H * yFrac)) : (_rowCentreX, _rowTopY + 110);

    /// <summary>An effect scale sized to the creature it lands on (1..5 of 104 px) — a swarm creature gets a
    /// small flash, a bruiser a big one, a boss the biggest — instead of one size for every body.</summary>
    private int EnemyScale(int slot, float mult = 1f)
        => Math.Clamp((int)MathF.Round((_creatureRect.TryGetValue(slot, out var r) ? r.H : 300f) / 140f * mult), 1, 5);

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
        _ui.GroundShadow(b, box.Center.X, EnemyBox.Bottom - 10, (int)(box.Width * 0.55f), 30, 0.5f * fade);
        _ui.AnimSprite(b, $"{enemyKey}_death_strip8_512", box, t, DeathFps, loop: false, EnemyTint * fade, -1f);
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
        var scale = ArchetypeScale(_run?.LastWaveArchetype ?? Archetype.Bruiser);
        // A shorter slide that FADES in: the old 280-px entry began past the scissor edge, so a wave
        // appeared as a hard-cut slice growing out of nothing — the box the playtest could see.
        var enter = (int)(_enemyEnter * 150f);
        var enterTint = Color.Lerp(EnemyTint * (1f - _enemyEnter * _enemyEnter), Ember, _enemyWindup * 0.38f);   // fade-in, then the ember wind-up flush
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
            // Back-to-front by index so the row overlaps consistently, and each creature bobs on its own
            // phase — five sprites bobbing in unison read as one animated object, not as five creatures.
            var cx = left + spacing * i;
            var bob = (int)(MathF.Sin(_anim * 2f + i * 1.7f) * 7f);
            var box = new Rectangle(cx - w / 2, EnemyBox.Bottom - h + bob, w, h);
            PublishCreature(i, box);
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

            _ui.GroundShadow(b, box.Center.X, EnemyBox.Bottom - 10, (int)(w * 0.55f), (int)(38 * scale), 0.55f);

            // MEASURED headroom, not a constant. The 2026-08-22 strips fill ~90% of their frame with the
            // figure sat 3.5% up from the floor, so a fixed 8% crop bit the top of the tallest creatures;
            // a negative topCrop makes AnimSprite trim exactly the empty rows and never the figure.
            const float crop = -1f;
            var compFps = attacking ? 16f : 12f;
            if (stripKey is null || !_ui.AnimSprite(b, stripKey, box,
                    EnemyClipSeconds(attacking, compFps, i * 0.31f), compFps,
                    !attacking, creatureTint, crop))
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
            var pip = new Rectangle(box.Center.X - 28, box.Y - 14, 56, 6);
            _ui.Fill(b, pip, new Color(0x12, 0x0C, 0x10));
            if (frac > 0f) _ui.Fill(b, new Rectangle(pip.X, pip.Y, (int)(pip.Width * frac), pip.Height), Ember);
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
        var comp = _run?.LastWaveCreatures ?? Array.Empty<WaveCreature>();
        if (comp.Count > 1)
        {
            DrawComposition(b, attacking, comp);
            return;
        }

        var elunge = (int)(_enemyLunge * -40f);
        var enter = (int)(_enemyEnter * 150f);   // shorter + faded, see DrawComposition
        var enterTint = Color.Lerp(EnemyTint * (1f - _enemyEnter * _enemyEnter), Ember, _enemyWindup * 0.38f);   // fade-in, then the ember wind-up flush
        var ebox = new Rectangle(EnemyBox.X + elunge + enter, EnemyBox.Y, EnemyBox.Width, EnemyBox.Height);
        _ui.GroundShadow(b, ebox.Center.X, ebox.Bottom - 10, (int)(ebox.Width * 0.60f), 42, 0.6f);

        var bob = (int)(MathF.Sin(_anim * 2f) * 8f);
        const float crop = -1f;   // measured headroom — see DrawComposition
        var figTop = ebox.Bottom - ebox.Height;
        var ab = new Rectangle(ebox.X, figTop + bob, ebox.Width, ebox.Height);
        PublishCreature(0, ab);
        _rowCentreX = EnemyBox.Center.X;   // the row anchor the effects fall back to — resting, like the composition's
        _rowTopY = figTop;
        if (_replay is not null && !_replay.CreatureAlive(0))
        {
            var deadKey = EnemySource is { } ds && EnemyForSource.TryGetValue(ds, out var dk) ? dk : null;
            DrawCreatureDeath(b, 0, new Rectangle(ebox.X, figTop, ebox.Width, ebox.Height), deadKey);
            return;
        }

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
                                                !attacking, enterTint, crop))
        {
            // Grounded so the static fallback stands where the animated strip does — otherwise the enemy
            // visibly hopped whenever the strip was missing and this path took over.
            if (staticKey is null || !_ui.SpriteGrounded(b, staticKey, ab, enterTint, crop))
                _ui.Fill(b, new Rectangle(ebox.X + 40, ebox.Y + 40, ebox.Width - 80, ebox.Height - 80), Ember);
        }
        FlashOver(b, stripKey, ab, EnemyClipSeconds(attacking, fps), fps, !attacking, FlashAt(0), crop);

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
        // The corruption's epithet on the boss — "FEVERED CRYSTAL LICH" — so the tier is a thing with a
        // name that looks back at you, not a number on another screen.
        var epithet = CorruptionLook.For(CorruptionTier).Epithet;
        if (epithet.Length > 0) _bossName = epithet + " " + _bossName;

        var lunge = (int)(_enemyLunge * -40f);
        var box = new Rectangle(BossAnchor.X - BossTargetBodyHeight / 2 + lunge, BossAnchor.Y - BossTargetBodyHeight,
                                BossTargetBodyHeight, BossTargetBodyHeight);
        _ui.GroundShadow(b, box.Center.X, BossAnchor.Y - 8, (int)(box.Width * 0.62f), 44, 0.6f);

        // The swing rides the same windup as everything else (see EnemyClipSeconds): the strike lands on the
        // frame the blow is credited, instead of the boss cycling its attack strip on the free clock.
        PublishCreature(0, box);
        _rowCentreX = BossAnchor.X;
        _rowTopY = box.Y;
        if (bossKey is not null && _replay is not null && !_replay.CreatureAlive(0) && _diedAt.TryGetValue(0, out var bossDiedAt)
            && _ui.Assets.Has($"{bossKey}_death_strip8_512"))
        {
            // The boss falls and lies there for the whole break — no fade; the next wave clears it.
            _ui.AnimSprite(b, $"{bossKey}_death_strip8_512", box, _anim - bossDiedAt, DeathFps, loop: false, EnemyTint, -1f);
            _bossBodyRect = box; _bossFullRect = box;
            return;
        }
        var fps = attacking ? 10f : 8f;
        var key = bossKey is null ? null : $"{bossKey}_{(attacking ? "attack" : "idle")}_strip8_512";
        var seconds = EnemyClipSeconds(attacking, fps);
        if (key is null || !_ui.AnimSprite(b, key, box, seconds, fps, !attacking, EnemyTint, -1f))
        {
            _bossBodyRect = new Rectangle(BossAnchor.X - 110, BossAnchor.Y - BossTargetBodyHeight, 220, BossTargetBodyHeight);
            _bossFullRect = _bossBodyRect;
            _ui.Fill(b, _bossBodyRect, Ember);
            return;
        }
        // The boss flashes white for a blow like every other creature (playtest 2026-08-30: "the bosses
        // do not flash"). Same silhouette pass, same shaped life — the boss is always slot 0.
        FlashOver(b, key, box, seconds, fps, !attacking, FlashAt(0), -1f);
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
        HeaderStackBottom = bar.Bottom;   // the host's toasts hang under the boss bar, not across it
        // The dev fixture's underlying wave-2 enemy is already dead (0%), so pose a representative fill.
        var frac = DevForceBoss ? 0.78f : Math.Clamp(_replay!.EnemyHealthFraction, 0f, 1f);
        // Name ABOVE the bar, not on it. Gold on the molten fill was gold-on-gold — the boss's name, the
        // one label that has to land, was the least readable thing on screen. Same rule as the parchment
        // panels: when the surface is already gold, the word moves off it.
        _ui.BarArt(b, bar, frac, "boss");
        _ui.TextCenterBig(b, _bossName, bar.Center.X, bar.Y - 34, UiKit.Vellum, UiTypography.Headline);
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

        var panel = LogPanel;
        DrawReportPanel(b, panel, shown, older);

        // A close icon, because "L CLOSES" in the footer is not a door a mouse player can see
        // (playtest 2026-08-26). It walks the same host path the L key does.
        if (_ui.CloseButton(b, UiKit.CloseRect(panel), hit, clicked))
            WantsLog = true;

        // THE FOOTER: the page buttons belong to the panel they page. They were once placed by absolute
        // literals that landed on the battle-control rail and the REWARD ACTIVITY rows; now they sit in
        // the band the report leaves empty at its foot, with the entry count between them and the keys
        // under it, and labelled with a verb \u2014 a bare chevron tells a new player nothing.
        var prev = new Rectangle(UiKit.ContentLeft(panel), panel.Bottom - 104, 200, 64);
        var next = new Rectangle(UiKit.ContentRight(panel) - 200, panel.Bottom - 104, 200, 64);
        _ui.TextCenterBig(b, $"ENTRY {_logIndex + 1} OF {Log.Count}", panel.Center.X, panel.Bottom - 96, Bone, UiTypography.Body);
        _ui.TextCenterBig(b, "\u2039 \u203a  STEP THROUGH THE LOG   \u00b7   L  CLOSES IT", panel.Center.X, panel.Bottom - 68, Slate, UiTypography.Secondary);
        if (_ui.Button(b, prev, "\u2039  OLDER", hit, clicked, enabled: _logIndex < Log.Count - 1)
            && _logIndex < Log.Count - 1) _logIndex++;
        if (_ui.Button(b, next, "NEWER  \u203a", hit, clicked, enabled: _logIndex > 0)
            && _logIndex > 0) _logIndex--;
    }

    /// <summary>
    /// The log's panel: the arena's width less a margin, from under the currency pills to above the dock.
    /// </summary>
    /// <remarks>
    /// Its aspect is held at or above 1.30 on purpose: <see cref="UiKit.Panel"/> picks its frame art by
    /// aspect, and the squarer frame wears diamonds at every edge's midpoint that reach thirty pixels
    /// into the panel — they crossed the table's right column and the footer's key hint the first time
    /// this was drawn taller than wide-ish. The medium frame's ornaments are small.
    /// </remarks>
    private static readonly Rectangle LogPanel = new(ArenaRect.X + 10, 130, ArenaRect.Width - 20, 800);

    /// <summary>What a diff row is called on screen \u2014 the table's own names, so the two blocks agree.</summary>
    private static string DiffLabel(string coreLabel) => coreLabel switch
    {
        "DEPTH" => "WAVES CLEARED",
        "HIT SIZE" => "AVERAGE HIT",
        "ABSORBED" => "ARMOUR ABSORBED",
        "WALL" => "THE WALL",
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

    private static readonly Color Better = new(0x8C, 0xC8, 0x5A);

    /// <summary>
    /// The report itself, as the log shows it. The death popup that shared this layout is gone — on a
    /// fall the arena shows only the short fallen banner, and this panel waits in the log.
    /// </summary>
    /// <remarks>
    /// Laid out as a report to be read, top to bottom (playtest 2026-08-28: "the Expedition Log's
    /// content, its texts and its UX look quite bad"): a caption naming the screen; a title band coloured
    /// by the outcome, with the wave it ended at on the left and what was cleared on the right; the
    /// wave that ended it; the verdict, large enough to read as the one sentence that matters; the five
    /// measures as a table — names left, figures right-aligned to one edge with their units dimmer,
    /// what each points at on the far right, a hairline between rows; then what changed since the last
    /// run here as before → after pairs with the change in a coloured chip. The footer with the page
    /// buttons is the caller's. Nothing here is measured; every number is <see cref="RunReport"/>'s.
    /// </remarks>
    private void DrawReportPanel(SpriteBatch b, Rectangle panel, RunReport r, RunReport? previous)
    {
        _ui.Panel(b, panel);   // the gold nine-slice: this IS a modal
        // The house margin. It was a hand-measured 72, which is 32 px past what this frame needs — the
        // capture shows its side band ending 22 px in, so a column at 40 clears it with room.
        var x0 = UiKit.ContentLeft(panel);
        var x1 = UiKit.ContentRight(panel);
        var w = x1 - x0;
        var top = panel.Y;

        // THE CAPTION, level with the close icon: what this screen is.
        _ui.TextBig(b, "EXPEDITION LOG", x0, top + UiTypography.ModalTitleTop, Gold, UiTypography.PanelTitle,
                    TextFace.Display);

        // THE TITLE BAND. FELL, in the title: every entry in this log is the end of a run, and it must
        // say so plainly — "DEPTH 12" once read as a score, not as an ending. Ember for a fall, gold
        // for a stall (the clock ran out, the champion stood); a record is a chip, not a second title.
        var stalled = r.Outcome == WaveOutcome.Stalled;
        var tint = stalled ? Gold : Ember;
        var band = new Rectangle(x0, top + 92, w, 56);
        _ui.Fill(b, band, tint * 0.16f);
        _ui.Fill(b, new Rectangle(band.X, band.Y, 5, band.Height), tint);
        Hairline(b, band.X, band.Bottom - 1, band.Width, tint * 0.5f);
        _ui.TextBig(b, stalled ? $"STALLED AT WAVE {r.WallWave}" : $"FELL AT WAVE {r.WallWave}",
            band.X + 24, band.Y + 8, tint, UiTypography.RegionTitle, TextFace.Display);
        var cleared = $"{r.Depth} WAVE{(r.Depth == 1 ? "" : "S")} CLEARED";
        var clearedRight = band.Right - 24;
        _ui.TextRightBig(b, cleared, clearedRight, band.Y + 18, Bone, UiTypography.Body);
        if (r.IsRecord)
        {
            var chipX = clearedRight - _ui.MeasureBig(cleared, UiTypography.Body) - 16 - ChipWidth("NEW RECORD", UiTypography.Caption);
            Chip(b, chipX, band.Y + 17, "NEW RECORD", Gold, Gold * 0.7f, UiTypography.Caption);
        }

        // THE WALL — named plainly, because the player has to be able to go and look at it.
        var affixes = r.WallAffixes.Count > 0
            ? string.Join(" + ", r.WallAffixes.Select(a => a.ToString().ToUpperInvariant()))
            : "NO AFFIX";
        var y = band.Bottom + 16;
        Runs(b, x0, y, UiTypography.Secondary,
            ("THE WAVE THAT ENDED IT   ", Slate),
            ($"{r.WallArchetype.ToString().ToUpperInvariant()} × {r.WallCreatures}", UiKit.Vellum),
            ("   ·   ", Slate),
            (affixes, Bone));

        // THE VERDICT: the one sentence. Large, wrapped, at most two lines. Everything under it flows
        // from where it ends, so a two-line verdict pushes the table down rather than into it.
        y += 28;
        foreach (var line in _ui.WrapBig(r.Verdict(), w, UiTypography.Headline).Take(2))
        {
            _ui.TextBig(b, line, x0, y, UiKit.Vellum, UiTypography.Headline);
            y += 30;
        }

        // THE MEASUREMENTS. Each line is a lever. Figures right-aligned to one edge, units dimmer
        // beside them, and what the measure points at on the far right. The column head says which
        // waves the figures cover — the last band, where the run actually failed.
        var ty = y + 14;
        Hairline(b, x0, ty, w, Slate * 0.45f);
        ty += 12;
        var xv = x0 + 440;   // the figures' right edge
        _ui.TextBig(b, "MEASURE", x0, ty, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, $"OVER THE LAST {r.SampledWaves} WAVE{(r.SampledWaves == 1 ? "" : "S")}", xv, ty, Slate, UiTypography.Secondary);
        _ui.TextRightBig(b, "POINTS AT", x1, ty, Slate, UiTypography.Secondary);
        ty += 28;
        void Row(string label, string figure, string unit, string points)
        {
            _ui.TextBig(b, label, x0, ty, Bone, UiTypography.Body);
            _ui.TextRightBig(b, figure, xv, ty, UiKit.Vellum, UiTypography.Body);
            if (unit.Length > 0) _ui.TextBig(b, unit, xv + 8, ty + 3, Slate, UiTypography.Secondary);
            _ui.TextRightBig(b, points, x1, ty + 3, Slate, UiTypography.Secondary);
            ty += 36;
            Hairline(b, x0, ty - 7, w, Slate * 0.18f);
        }

        Row("ARMOUR ABSORBED", $"{r.AbsorbedFraction * 100f:F0}", "% of your damage", "HIT SIZE");
        Row("AVERAGE HIT", $"{r.AverageHitSize:F0}", "", "HIT SIZE");
        Row("REACH", $"{r.TargetsPerActivation:F1}", $"of {r.CreaturesPerWave:F1} creatures per cast", "HITS PER CAST");
        Row("HEALTH LOST PER WAVE", $"{r.HealthLostPerWaveFraction * 100f:F0}", "% of your health", "STAYING ALIVE");
        Row("TIME PER WAVE", $"{r.SecondsPerWave:F1}", "seconds", "SPEED");

        // THE DIFF — what changed since the last attempt here. This is what makes iteration legible:
        // before → after, and the change in a chip coloured by whether it went the good way.
        var dy = ty + 18;
        _ui.TextBig(b, "SINCE YOUR LAST RUN HERE", x0, dy, Gold, UiTypography.Secondary);
        Hairline(b, x0, dy + 24, w, Slate * 0.45f);
        dy += 34;
        var entries = r.DiffEntries(previous).ToList();
        if (entries.Count == 0)
            _ui.TextBig(b, "NO EARLIER RUN IN THIS REGION TO COMPARE WITH.", x0, dy, Slate, UiTypography.Secondary);

        // CLAMPED TO THE ROOM THERE ACTUALLY IS, above the footer's buttons: the next row added to
        // RunReport's diff must never go under the frame. A report that quietly drops its last row is
        // better than one that draws it where it cannot be read — and the count is derived.
        const int pitch = 26;
        var room = Math.Max(0, (panel.Bottom - 112 - dy) / pitch);
        var xa = x0 + 330;   // the "before" figures' right edge
        foreach (var e in entries.Take(room))
        {
            _ui.TextBig(b, DiffLabel(e.Label), x0, dy, Slate, UiTypography.Secondary);
            var was = e.Numeric ? DiffValue(e.Label, e.Was, e.Unit) : e.WasText.ToUpperInvariant();
            var now = e.Numeric ? DiffValue(e.Label, e.Now, e.Unit) : e.NowText.ToUpperInvariant();
            _ui.TextRightBig(b, was, xa, dy, Bone, UiTypography.Secondary);
            _ui.TextBig(b, "→", xa + 12, dy, Slate, UiTypography.Secondary);
            _ui.TextBig(b, now, xa + 36, dy, UiKit.Vellum, UiTypography.Secondary);
            if (e.Numeric)
            {
                var sign = e.Delta >= 0f ? "+" : "";
                var change = e.Improved switch { true => Better, false => Ember, null => Slate };
                Chip(b, x0 + 520, dy - 4, $"{sign}{DiffValue(e.Label, e.Delta, e.Unit)}", change, change * 0.6f);
            }
            dy += pitch;
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
            {
                // A short line, not a report. The full report is in the EXPEDITION LOG (L) — the
                // popup that used to open here vanished with the next descent and taught nobody
                // anything. Full strength while the champion is down, then it fades out.
                var fade = _mode == Mode.Downed ? 1f : Math.Clamp(_fellTimer * 1.4f, 0f, 1f);
                _ui.Fill(b, new Rectangle(500, 200, 920, 110), PanelBg * fade);
                _ui.TextCenterBig(b, $"YOUR CHAMPION FELL AT WAVE {_fellWave}", 960, 222, Ember * fade,
                    UiTypography.StageLabel, TextFace.Display);
                _ui.TextCenterBig(b, "THE FULL REPORT IS IN THE LOG — PRESS L", 960, 266, Bone * fade,
                    UiTypography.OverlayBody);
                break;
            }
            case HuntOverlay.BossIncoming:
            {
                var fade = Math.Clamp(_bossIncomingTimer * 1.4f, 0f, 1f);
                _ui.Fill(b, new Rectangle(610, 200, 700, 110), PanelBg * fade);
                _ui.TextCenterBig(b, "BOSS INCOMING", 960, 224, Gold * fade, UiTypography.RegionTitle, TextFace.Display);
                _ui.TextCenterBig(b, "GET READY", 960, 274, Bone * fade, UiTypography.OverlayBody);
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
        if (_ui.Assets.GetFirst(Character.PortraitKey, "hunter_portrait") is { } p) b.Draw(p, por, Color.White);
        else if (_ui.Assets.Get("ui_medallion_round") is { } mfr) b.Draw(mfr, por, Color.White);

        // WHO, AND WHAT CLASS: "SEEKER · WANDERER", the class in its own colour. Playtest
        // (2026-08-26): "I cannot see the characters' classes." The name used to be the mastery
        // title ("STRIKE ADEPT"), which is not who you are; that moves to a small line beneath.
        // The three pieces shrink together until they fit the column, so THE FALLING TOWER's
        // long name and WARDEN sit on one line rather than running under the power figure.
        var name = Character.ShortName;
        var cls = ItemClasses.NameOf(Character.Class);
        const string sep = " · ";
        const int nameColumn = 270;   // 334 to the panel's inner edge
        var px = UiTypography.PanelTitle;
        while (px > UiTypography.Secondary && _ui.MeasureBig(name, px) + _ui.MeasureBig(sep, px) + _ui.MeasureBig(cls, px) > nameColumn) px--;
        var nx = 334;
        var ny = 40 + (UiTypography.PanelTitle - px) / 2;
        _ui.TextBig(b, name, nx, ny, Bone, px);
        nx += _ui.MeasureBig(name, px);
        _ui.TextBig(b, sep, nx, ny, Slate, px);
        nx += _ui.MeasureBig(sep, px);
        _ui.TextBig(b, cls, nx, ny, UiKit.ClassColor(Character.Class), px);
        if (Mastery.Affinity() is { } mf)
            _ui.TextBig(b, $"{FormShort(mf)} ADEPT", 334, 67, Slate, UiTypography.Caption);           // mastery title
        _ui.TextBig(b, $"LV {_hunter?.HunterLevel ?? 1}", 334, 86, Gold, UiTypography.Body);        // level
        // Combat power — an icon + value (spec: an icon, not a "PWR" label).
        if (_ui.Assets.Get("state_resonance_128") is { } pi) b.Draw(pi, new Rectangle(440, 81, 30, 30), Ember);
        _ui.TextBig(b, Game1.Abbrev(_hunter?.PowerRating ?? 0), 476, 83, Ember, UiTypography.Headline);
        // HP bar (162,112,220,24), value centred on it.
        var hp = Math.Max(0, _replay?.HealthOf(0) ?? 0);
        var hpBar = new Rectangle(334, 112, 220, 24);
        _ui.BarArt(b, hpBar, _replay?.HealthFractionOf(0) ?? 1f, "health");
        _ui.TextCenterBig(b, $"{hp}/{_champ?.MaxHealth ?? 0}", hpBar.Center.X, hpBar.Y + 3, Bone, UiTypography.Secondary);
        // Secondary stat line — TEMPO × skill rate (spec §8.6: no fake mana bar; real SquadSkillRate).
        _ui.TextBig(b, $"TEMPO {(_hunter?.SquadSkillRate ?? 1f):0.00}x ACTION SPEED", 334, 145, Slate, UiTypography.Secondary);

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
    /// <summary>Where the EXPEDITION LOG button sits: just right of the stage header, clear of the pills.</summary>
    private static readonly Rectangle LogButtonRect = new(1206, 40, 64, 64);

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
    private void DrawLogButton(SpriteBatch b, Point hit, bool clicked)
    {
        var r = LogButtonRect;
        var hot = r.Contains(hit);
        // 52 px at rest, 56 under the mouse — the same lift the close icons use — inside the 64 px hit box.
        var box = hot ? new Rectangle(r.X + 4, r.Y + 4, 56, 56) : new Rectangle(r.X + 6, r.Y + 6, 52, 52);
        var tint = _logOpen ? Gold : hot ? Color.White : new Color(0xE0, 0xD8, 0xC8);
        if (!_ui.Icon(b, "icon_log", box, tint))
        {
            // No medallion on disk: a plain page so the door still shows.
            _ui.Fill(b, box, new Color(0x14, 0x10, 0x1A, 0xE0));
            _ui.TextCenterBig(b, "LOG", box.Center.X, box.Center.Y - 8, tint, UiTypography.Caption);
        }
        if (hot) _ui.HoverTip(b, "EXPEDITION LOG — every descent's report. The L key opens it too.", hit);
        if (UiKit.ClickedIn(r, hit, clicked)) WantsLog = true;
    }

    /// <summary>Set by the log button; the host routes it through its own L handling and clears it.</summary>
    public bool WantsLog { get; set; }

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
    private void DrawEnemyLine(SpriteBatch b)
    {
        if (_replay is null || _run is null || _isBossWave) return;
        var strip = EnemyStrip;
        _ui.Fill(b, strip, new Color(0x14, 0x10, 0x1A, 0xE0));
        Outline(b, strip, PlateEdge, 2);
        HeaderStackBottom = strip.Bottom;

        // LEFT: the wave's Source glyph, then its kind.
        var glyph = new Rectangle(strip.X + 12, strip.Y + 7, 30, 30);
        if (EnemySource is { } es && _ui.Assets.Get($"source_{es.ToString().ToLowerInvariant()}") is { } g)
            b.Draw(g, glyph, Color.White);
        else _ui.Diamond(b, new Rectangle(glyph.X + 6, glyph.Y + 6, 18, 18), EnemySource is { } s2 ? SourceGlow(s2) : Slate);
        var x = glyph.Right + 10;
        var kind = _run.LastWaveArchetype.ToString().ToUpperInvariant();
        _ui.TextBig(b, kind, x, strip.Y + 12, UiKit.Vellum, UiTypography.Body);
        x += _ui.MeasureBig(kind, UiTypography.Body) + 10;

        // RIGHT: how many still stand. (The pack's life bar that stood here was "unnecessary" — every
        // creature wears its own pip, playtest 2026-08-28.)
        var total = _replay.CreatureCount;
        var alive = 0;
        for (var i = 0; i < total; i++) if (_replay.CreatureAlive(i)) alive++;
        var countLeft = strip.Right - 16;
        if (total > 0)
            countLeft = RunsRight(b, countLeft, strip.Y + 13, UiTypography.Secondary, ($"{alive} OF {total}", Bone), ("  STANDING", Slate));

        // BETWEEN: the affixes as chips, as many as fit; the rest fold into a "+N" chip.
        var affixes = _run.LastWaveAffixes ?? Array.Empty<Affix>();
        var limit = countLeft - 16;
        for (var i = 0; i < affixes.Count; i++)
        {
            var text = affixes[i].ToString().ToUpperInvariant();
            var rest = affixes.Count - i;
            var more = rest > 1 ? ChipWidth($"+{rest - 1}") + 6 : 0;
            if (x + ChipWidth(text) + more > limit)
            {
                if (x + ChipWidth($"+{rest}") <= limit) Chip(b, x, strip.Y + 11, $"+{rest}", Bone, PlateEdge);
                break;
            }
            x += Chip(b, x, strip.Y + 11, text, Bone, PlateEdge) + 6;
        }
    }

    /// <summary>The stage header's centre and bottom edge — the enemy strip hangs from it.</summary>
    private const int StageHeaderCentreX = 910;
    private const int StageHeaderBottomY = 153;

    /// <summary>The enemy strip: the header's width, six pixels under its frame.</summary>
    private static readonly Rectangle EnemyStrip = new(630, StageHeaderBottomY + 7, 560, 44);

    /// <summary>
    /// Where the header stack ends this frame — the enemy strip's foot, the boss bar's, or the header's
    /// own — so the host can hang its transient toasts under it rather than across it.
    /// </summary>
    public int HeaderStackBottom { get; private set; } = StageHeaderBottomY;

    private void DrawStageHeader(SpriteBatch b, string regionName, bool isBossWave)
    {
        // Rev 3 §12: stage header (630,18,560,135) — narrower, so it clears the currency bar (≥20px gap),
        // and inside the tour's header spotlight (620,8,580,152). ONE HIERARCHY, top to bottom: WHERE (the
        // region, in the display face), WHEN (the wave, and the run's state beside it), HOW FAR (the
        // conquest, a small label and a thin bar on one line). WHAT is being fought hangs under the panel
        // in DrawEnemyLine at the same width, so the two read as one stack (playtest 2026-08-28: "the wave
        // information texts look quite bad" — three lines of three sizes with a bar between two of them).
        var bar = new Rectangle(630, 18, 560, 135);
        // The quiet frame, like every other panel on this screen (UiKit.PanelQuiet: gold is for modals).
        _ui.PanelQuiet(b, bar);
        const int cx = StageHeaderCentreX;
        // §19.2: render the region title at 36, shrinking to a floor of 28 to fit 490px — never ellipsize the
        // ACTIVE region title. (Two-line fallback below 28 is a noted follow-up; region names fit at 28.)
        var title = regionName.ToUpperInvariant();
        var titlePx = UiTypography.RegionTitle;
        while (titlePx > 28 && _ui.MeasureBig(title, titlePx) > 490) titlePx--;
        _ui.TextCenterBig(b, title, cx, 28 + (UiTypography.RegionTitle - titlePx) / 2, Gold, titlePx, TextFace.Display);

        // THE WAVE LINE CARRIES THE RUN'S STATE, which is what the deleted EXPEDITION plate was for.
        // Nothing is appended while the run is simply running: "ACTIVE" was true of every frame this
        // screen has ever drawn, so it distinguished nothing and only made the line longer.
        var wave = $"WAVE {Math.Max(1, _replayWave)}";
        if (CorruptionTier > 0) wave += $"  ·  {CorruptionLook.For(CorruptionTier).Name}";
        var waveTint = isBossWave ? Gold : Bone;
        const int waveY = 70;
        if (RunState() is { } st)
            RunsCenter(b, cx, waveY, UiTypography.StageLabel, (wave, waveTint), ("  —  ", Slate), (st.Text, st.Tint));
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
        const int barW = 240, barH = 16, gap = 16, barY = 106;
        var labelW = RunsWidth(UiTypography.Secondary, label);
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
                     int px = UiTypography.Caption)
    {
        var w = ChipWidth(text, px);
        var r = new Rectangle(x, y, w, 22);
        _ui.Fill(b, r, new Color(0x14, 0x10, 0x1A, 0xE0));
        Outline(b, r, edge, 1);
        _ui.TextBig(b, text, x + UiTypography.ChipPadX, y + UiTypography.ChipPadY, ink, px);
        return w;
    }

    private int ChipWidth(string text, int px = UiTypography.Caption)
        => _ui.MeasureBig(text, px) + UiTypography.ChipPadX * 2;

    private void Hairline(SpriteBatch b, int x, int y, int w, Color c) => _ui.Fill(b, new Rectangle(x, y, w, 1), c);

    private static readonly Color PlateEdge = new(0x74, 0x62, 0x3E);

    // ── The right rail's measure. ─────────────────────────────────────────────────────────────────
    //    Every plate is a UiKit.PanelQuiet, whose ornament reaches about 18 px in; the insets below are
    //    measured against that frame, not the 40 px the gold modal frame needed.
    private const int ColumnX = 1570, ColumnW = 326;
    private const int RailInset = UiTypography.PanelPadNarrow;   // the house margin for a narrow plate
    // THE PLATE TITLES ARE PANEL TITLES NOW. They were 17 px at +24 — every menu screen in the game
    // sets a panel's title at 26 px on the standard line, and these three plates were the last ones
    // speaking in their own voice. The band grows with them.
    private const int RailTitleBand = UiTypography.PanelBodyTopBare;
    private const int RailGap = 12;          // between plates
    private const int IdlePlateHeight = RailTitleBand + 46;   // the coin and the rate on one line
    private const int RewardButtonHeight = 44;
    private const int RewardRowPitch = 48;
    private const int RewardFootLine = 18;   // the DEEPEST WAVE line under the errands
    private const int FilterRowHeight = 54;  // the CHEST FILTER button row
    private const int FilterPopoverHeight = 420;   // measured: the footer line cleared the frame at 386 by nothing

    /// <summary>A plate in the right rail, in the house frame, returning the space its content may use.</summary>
    /// <remarks>
    /// THE QUIET FRAME, the one the hero card and the SKILLS rail already wear across the arena. The rail
    /// spent one round (2026-08-26) in the gold modal frame to match the CHEST FILTER plate, and the
    /// playtest chose the other way: "the right frames are gold while the left ones are brown — pick one;
    /// I prefer the brown". One screen, one frame; the gold nine-slice is for modals (see
    /// <see cref="UiKit.PanelQuiet"/>).
    /// </remarks>
    private Rectangle CleanPanel(SpriteBatch b, Rectangle r, string title)
    {
        _ui.PanelQuiet(b, r);
        // CENTRED, like every other panel title in the game (playtest 2026-08-29: "the hunt panels'
        // titles are left-aligned; make them centred"). The quiet frame's top rail is ~20 px deep, so the
        // baseline comes from UiKit.TitleTop rather than a literal.
        _ui.TextCenterBig(b, title, r.Center.X, UiKit.TitleTop(r), Gold, UiTypography.PanelTitle);
        return new Rectangle(r.X + RailInset, r.Y + RailTitleBand, r.Width - RailInset * 2, r.Height - RailTitleBand - RailInset);
    }

    /// <summary>Right context column: the idle rate, the errands worth doing now, and the chest filter's door.</summary>
    /// <remarks>
    /// It was four plates. OBJECTIVE and EXPEDITION printed the depth, the progress bar and the wave that
    /// the banner over the arena already prints, so half the rail was a second copy of the header. Then
    /// the two that remained were "too big" (playtest 2026-08-26): each is now about two-thirds its old
    /// height, and the filter — which stood open at full height under them — is a row that opens a
    /// popover on demand.
    /// </remarks>
    private void DrawRightColumn(SpriteBatch b, Point hit, bool clicked)
    {
        // Rev 3 §20: right context rail from (1570,110) — secondary panels, all live data. The spec's
        // "no keyboard hints" rule is deliberately broken by the two REWARD ACTIVITY buttons, which name
        // their key: they are the one place on this screen that asks the player to go somewhere, and a
        // door worth opening should say how. Idle rate → reward activity → chest filter.

        // Idle rate: the coin and the rate on one line. Auto-credited, so no Claim button (§20.2).
        var idle = new Rectangle(ColumnX, 110, ColumnW, IdlePlateHeight);
        var inner = CleanPanel(b, idle, "IDLE RATE");
        var rowY = inner.Y - 2;
        if (_ui.Assets.Get("currency_gleam") is { } gi) b.Draw(gi, new Rectangle(inner.X, rowY, 30, 30), Color.White);
        _ui.TextBig(b, $"+{Game1.Abbrev((long)(IdleGleamRate * 60f))}/min", inner.X + 40, rowY + 2, Gold, UiTypography.Headline);

        // Reward activity — real summary, no fake loot grid (§20.4/§21). Sized to what it actually holds —
        // two errands, one, or just the career line. An ornate frame around a void reads as content the
        // eye has somehow failed to find.
        var chestRow = ChestCount > 0 && VaultOpen;
        var pointsRow = Mastery.Available > 0 && MasteryOpen;
        var rows = (chestRow ? 1 : 0) + (pointsRow ? 1 : 0);
        var reward = new Rectangle(ColumnX, idle.Bottom + RailGap, ColumnW,
                                   RailTitleBand + rows * RewardRowPitch + RewardFootLine + RailInset);
        inner = CleanPanel(b, reward, "REWARD ACTIVITY");
        // THE TWO THINGS A PLAYER SHOULD DO NOW WERE INERT GREY TEXT, drawn in exactly the same style as
        // the dead career stat below them — so "1 chest available" read as trivia rather than as an
        // errand. They are buttons with verbs now, naming their own key. They only navigate: the actual
        // work still happens on the screen that owns it. A rail that opened chests would be a second Forge.
        // A REWARD WHOSE SCREEN IS LOCKED IS HIDDEN — not greyed, not clickable-into-a-refusal. A door
        // this rail advertises must open; until the host says the screen is unlocked, the errand simply
        // is not offered.
        var ry = inner.Y;
        var anyReward = false;
        if (chestRow)
        {
            if (_ui.Button(b, new Rectangle(inner.X, ry, inner.Width, RewardButtonHeight),
                           $"OPEN {ChestCount} CHEST{(ChestCount == 1 ? "" : "S")}  (K)", hit, clicked))
                WantsVault = true;
            ry += RewardRowPitch; anyReward = true;
        }
        // Mastery points are spent on the MASTERY tree — the E screen — so that is where this button
        // goes and the key it names.
        if (pointsRow)
        {
            if (_ui.Button(b, new Rectangle(inner.X, ry, inner.Width, RewardButtonHeight),
                           $"SPEND {Mastery.Available} POINT{(Mastery.Available == 1 ? "" : "S")}  (E)", hit, clicked))
                WantsMastery = true;
            ry += RewardRowPitch; anyReward = true;
        }
        if (Deepest > 0) _ui.TextBig(b, $"DEEPEST WAVE REACHED  {Deepest}", inner.X, ry + 1, Slate, UiTypography.Secondary);
        else if (!anyReward) _ui.TextBig(b, "NOTHING TO CLAIM YET", inner.X, ry + 1, Slate, UiTypography.Secondary);

        DrawKeepFilter(b, new Rectangle(ColumnX, reward.Bottom + RailGap, ColumnW, FilterRowHeight), hit, clicked);
    }

    // ── CHEST FILTER — on the screen whose drops it decides (2026-08-23). ─────────────────────────
    //    It was a row on the VAULT; the playtest put it here: "drop filtresi hunt ekranını
    //    ilgilendiriyor oraya taşınsın." Host-fed and host-persisted; this screen only edits it and
    //    raises FilterDirty. NOTE for the host: the edit happens in DRAW, so read it back on the dirty
    //    flag and never push the saved value every frame — that exact push clobbered the vault's edit
    //    a frame later in playtest five.
    /// <summary>Chests below this tier never land — they arrive as a little Scrap instead. 0 = all.</summary>
    public int KeepMinTier { get; set; }
    /// <summary>Keep only chests whose region favours ANY of these slots (no-lean chests always pass). Empty = any.</summary>
    public HashSet<ItemBaseType> KeepSlots { get; } = new();
    /// <summary>Set when the player edited the filter — the host copies it back and saves.</summary>
    public bool FilterDirty { get; set; }

    /// <summary>
    /// The filter's popover is up. The row toggles it; its close icon, a click anywhere outside it, and
    /// Escape (routed by the host) close it. The row keeps the setting readable while it is closed.
    /// </summary>
    /// <remarks>
    /// It used to stand open at full height, permanently — the tallest thing in the rail, for a setting
    /// most players touch once. Playtest 2026-08-26: "a button that opens it, I set it, it closes."
    /// </remarks>
    public bool FilterOpen { get; set; }

    /// <summary>The bottom edge of the CHEST FILTER row as last drawn (+ margin) — the right column's true extent.</summary>
    private static int s_railBottom = 368;

    private static readonly ItemBaseType[] SlotChips =
    {
        ItemBaseType.Weapon, ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves,
        ItemBaseType.Boots, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Ring,
    };

    private static string SlotLabel(ItemBaseType t) => t switch
    {
        ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.Chest => "ARMOUR",
        _ => t.ToString().ToUpperInvariant(),
    };

    /// <summary>The slot's medallion, <c>item_slot_&lt;slot&gt;</c> — the same art the GEAR screen's paper doll wears.</summary>
    private static string SlotIconKey(ItemBaseType t) => t switch
    {
        ItemBaseType.AbilityFocus => "item_slot_focus",
        _ => "item_slot_" + t.ToString().ToLowerInvariant(),
    };

    /// <summary>What the medallion is, in plain words, for the hover tip (playtest 2026-08-26: icons, with the name on hover).</summary>
    private static string SlotTip(ItemBaseType t) => t switch
    {
        ItemBaseType.Weapon => "WEAPON — what your champion strikes with.",
        ItemBaseType.Helm => "HELM — head armour.",
        ItemBaseType.Chest => "ARMOUR — body armour, worn on the chest.",
        ItemBaseType.Gloves => "GLOVES — hand armour.",
        ItemBaseType.Boots => "BOOTS — foot armour.",
        ItemBaseType.Charm => "CHARM — a trinket worn for its power.",
        ItemBaseType.AbilityFocus => "FOCUS — the piece that channels your skills.",
        ItemBaseType.Ring => "RING — a ring worn for its power.",
        _ => SlotLabel(t),
    };

    /// <summary>The filter's setting in words: "ANY TIER · ALL SLOTS", "TIER 3 AND UP · HELM, BOOTS".</summary>
    private string FilterSummary()
    {
        var tier = KeepMinTier <= 0 ? "ANY TIER" : $"TIER {KeepMinTier} AND UP";
        var slots = KeepSlots.Count == 0 ? "ALL SLOTS" : string.Join(", ", SlotChips.Where(KeepSlots.Contains).Select(SlotLabel));
        return $"{tier} · {slots}";
    }

    /// <summary>The CHEST FILTER row — its name, its current setting in words, and the door to change it.</summary>
    private void DrawKeepFilter(SpriteBatch b, Rectangle row, Point hit, bool clicked)
    {
        var pop = new Rectangle(row.X, row.Bottom + 8, row.Width, FilterPopoverHeight);
        s_railBottom = row.Bottom + 10;
        var wasOpen = FilterOpen;
        var hot = row.Contains(hit);
        if (UiKit.ClickedIn(row, hit, clicked)) FilterOpen = !wasOpen;
        else if (wasOpen && clicked && !pop.Contains(hit)) FilterOpen = false;   // a click anywhere else closes it

        // The row: the chip style of the filter's own buttons, lit gold while the popover is up.
        _ui.Fill(b, row, new Color(0x14, 0x10, 0x1A, 0xE0));
        Outline(b, row, wasOpen ? Gold * 0.8f : hot ? Bone : PlateEdge, 2);   // bronze at rest: a button, in the rail's own brown
        _ui.TextBig(b, "CHEST FILTER", row.X + 14, row.Y + 6, wasOpen || hot ? Gold : Gold * 0.85f, UiTypography.Secondary);
        var door = wasOpen ? "CLOSE  ×" : "CHANGE  ›";
        _ui.TextRightBig(b, door, row.Right - 14, row.Y + 8, hot ? Bone : Slate, UiTypography.Caption);
        _ui.TextBig(b, _ui.ShortenBig(FilterSummary(), row.Width - 28, UiTypography.Caption), row.X + 14, row.Y + 30,
                    Bone, UiTypography.Caption);

        if (wasOpen) DrawFilterPopover(b, pop, hit, clicked && pop.Contains(hit));
        else if (hot) _ui.HoverTip(b, "CHEST FILTER — which chests to keep. The rest turn into a little Scrap. Click to change it.", hit);
    }

    /// <summary>The filter's controls: the lowest tier to keep, and the gear slots — as medallions.</summary>
    /// <remarks>
    /// Drawn after the rail, so it sits over whatever is under it. Only clicks INSIDE it reach here (the
    /// row handles the outside click); the tip for the hovered medallion is drawn last, over everything.
    /// </remarks>
    private void DrawFilterPopover(SpriteBatch b, Rectangle pop, Point hit, bool clicked)
    {
        _ui.PanelQuiet(b, pop);
        var inner = new Rectangle(pop.X + RailInset, pop.Y + RailInset, pop.Width - RailInset * 2, pop.Height - RailInset * 2);
        // The title sits on the close icon's row, so the two read as one header.
        var close = UiKit.CloseRect(pop, 36);
        _ui.TextBig(b, "CHEST FILTER", inner.X, close.Y + 4, Gold, UiTypography.PanelTitle);
        if (_ui.CloseButton(b, close, hit, clicked)) FilterOpen = false;
        var y = close.Bottom + 8;
        _ui.TextBig(b, "WHICH CHESTS TO KEEP", inner.X, y, Slate, UiTypography.Secondary);
        y += 30;

        // LOWEST TIER   [-]  ANY TIER / TIER N AND UP  [+]
        _ui.TextBig(b, "LOWEST TIER", inner.X, y, Bone, UiTypography.Secondary);
        y += 22;
        var minus = new Rectangle(inner.X, y, 34, 34);
        var plus = new Rectangle(inner.Right - 34, y, 34, 34);
        MiniButton(b, minus, "-", hit);
        MiniButton(b, plus, "+", hit);
        _ui.TextCenter(b, KeepMinTier <= 0 ? "ANY TIER" : $"TIER {KeepMinTier} AND UP", inner.Center.X, y + 6, KeepMinTier > 0 ? Gold : Slate);
        if (UiKit.ClickedIn(minus, hit, clicked) && KeepMinTier > 0) { KeepMinTier -= 1; FilterDirty = true; }
        if (UiKit.ClickedIn(plus, hit, clicked) && KeepMinTier < 99) { KeepMinTier += 1; FilterDirty = true; }

        // The slots, as TOGGLES — several at once ("hem bot hem kolye"). Two rows of four medallions, the
        // slot art the GEAR screen wears, lit when kept; ALL SLOTS under them clears the lot.
        y += 48;
        _ui.TextBig(b, "GEAR SLOTS THE CHEST IS FOR", inner.X, y, Bone, UiTypography.Secondary);
        y += 24;
        string? tip = null;
        const int cellH = 60;
        var cellW = inner.Width / 4;
        for (var i = 0; i < SlotChips.Length; i++)
        {
            var slot = SlotChips[i];
            var cell = new Rectangle(inner.X + (i % 4) * cellW, y + (i / 4) * (cellH + 4), cellW, cellH);
            var lit = KeepSlots.Contains(slot);
            var hot = cell.Contains(hit);
            if (lit) _ui.Fill(b, cell, Gold * 0.16f);
            var box = hot ? new Rectangle(cell.Center.X - 27, cell.Center.Y - 27, 54, 54)
                          : new Rectangle(cell.Center.X - 25, cell.Center.Y - 25, 50, 50);
            var tint = lit || hot ? Color.White : new Color(0x8C, 0x86, 0x80);
            if (!_ui.Icon(b, SlotIconKey(slot), box, tint))
                _ui.TextCenterBig(b, SlotLabel(slot), cell.Center.X, cell.Center.Y - 8, tint, UiTypography.Caption);
            if (lit) Outline(b, cell, Gold * 0.8f, 2);
            if (hot) tip = SlotTip(slot) + (lit ? " Click to stop keeping its chests." : " Click to keep the chests made for it.");
            if (UiKit.ClickedIn(cell, hit, clicked))
            {
                if (!KeepSlots.Remove(slot)) KeepSlots.Add(slot);
                FilterDirty = true;
            }
        }
        y += 2 * (cellH + 4) + 6;
        var all = new Rectangle(inner.X, y, inner.Width, 30);
        MiniButton(b, all, "ALL SLOTS", hit, KeepSlots.Count == 0);
        if (UiKit.ClickedIn(all, hit, clicked) && KeepSlots.Count > 0) { KeepSlots.Clear(); FilterDirty = true; }
        // The last line: what happens to the rest, only while a filter is set.
        y += 40;
        // Slate, not Dim: Dim on the quiet frame's black interior was a line the capture could not read.
        _ui.TextBig(b, KeepMinTier > 0 || KeepSlots.Count > 0 ? "OTHER CHESTS TURN INTO A LITTLE SCRAP" : "EVERY CHEST IS KEPT",
                    inner.X, y, Slate, UiTypography.Caption);
        if (tip is not null) _ui.HoverTip(b, tip, hit);
    }

    private void MiniButton(SpriteBatch b, Rectangle r, string label, Point hit, bool lit = false)
    {
        var hot = r.Contains(hit);
        _ui.Fill(b, r, new Color(0x14, 0x10, 0x1A, 0xE0));
        var edge = lit ? Gold * 0.8f : hot ? Bone : Dim;
        _ui.Fill(b, new Rectangle(r.X, r.Y, r.Width, 2), edge);
        _ui.Fill(b, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), edge);
        _ui.Fill(b, new Rectangle(r.X, r.Y, 2, r.Height), edge);
        _ui.Fill(b, new Rectangle(r.Right - 2, r.Y, 2, r.Height), edge);
        _ui.TextCenterBig(b, label, r.Center.X, r.Y + (r.Height - UiTypography.Secondary) / 2 - 1,
                          lit ? Gold : hot ? Bone : Slate, UiTypography.Secondary);
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
        _ui.TextCenterBig(b, "SKILLS", RailContentX + RailContentW / 2, RailTop + UiTypography.PanelTitleTop, Gold, UiTypography.PanelTitle);
        // THE POOL, beside the header it feeds. Gold at the brim — the REND moment worth waiting for.
        if (_chargeLive)
            _ui.TextRightBig(b, $"CHARGE {_chargeNow}/{_chargeCap}", RailContentX + RailContentW,
                             RailTop + UiTypography.PanelTitleTop + (UiTypography.PanelTitle - UiTypography.Secondary) / 2,
                             _chargeNow >= _chargeCap ? Gold : Slate, UiTypography.Secondary);
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
                // Keyed by SOURCE and FORM — one readout per skill. Keyed by Form alone, two Projectiles
                // shared a bar and "fired at once" (playtest 2026-08-26).
                var formKey = (int)s.Form;
                var skillKey = SkillKey((int)s.Source, formKey);
                var flash = _skillFlash.TryGetValue(skillKey, out var fl) ? Math.Clamp(fl / 0.42f, 0f, 1f) : 0f;
                // A PASSIVE IS ALWAYS READY. Every branch below asks the replay when this skill last
                // cast, and an Aura never "casts" — so ready stayed 0 and the dial shaded the whole
                // medallion for the entire run, under a label reading ALWAYS ON (review 2026-08-30).
                var ready = FormBehaviour.IsPassive(s.Form) ? 1f : 0f;
                // How many notches the ring has. Set by the beat-counted branch to the plain actions in
                // THIS cycle, so one action is always exactly one notch; 0 leaves the ring continuous.
                var ringSteps = 0;
                var telegraph = 0f;
                if (_replay is not null)
                {
                    var next = _replay.NextSkillAfter(_playheadMs, (int)s.Source, formKey);
                    var prev = _replay.LastSkillBefore(_playheadMs, (int)s.Source, formKey);
                    // WHAT THE PREVIOUS WAVES ALREADY SPENT. Used only when THIS wave holds no cast to
                    // measure from — once the skill has fired inside this replay, that cast is the
                    // truth and the carry is stale. See _carryBeats.
                    var carryBeats = prev >= 0 ? 0 : _carryBeats.GetValueOrDefault(skillKey);
                    var carryMs = prev >= 0 ? 0f : _carryMs.GetValueOrDefault(skillKey);
                    if (next != int.MaxValue && next - _playheadMs is > 0f and <= 300f) telegraph = 1f - (next - _playheadMs) / 300f;
                    if (FormBehaviour.CooldownBeats(s.Form) is var bts and > 0)
                    {
                        // COUNTED IN BEATS, AND THE SIM'S OWN BEATS. The rail used to rebuild a count
                        // by walking the damage stream — see WaveReplay.BeatAt for why that was a
                        // guess. Champion.BeatCount is what the cooldown is actually measured against,
                        // and it is in the event stream now, so this is the same number the fight used.
                        //
                        // THE DENOMINATOR IS THE CYCLE'S PLAIN ACTIONS — one fewer than the cooldown,
                        // because the next cast is itself an action. A four-action skill's cycle is
                        // cast, hit, hit, hit, cast: three hits to fill across (playtest 2026-08-28,
                        // "skill henüz cooldowndayken atıyor" — dividing by four could only ever reach
                        // three quarters, so the ring was never once seen full).
                        //
                        // It CAN sit full for an action, and that is now the truth rather than a bug:
                        // only one action happens per beat, so a ready skill that loses the beat to an
                        // earlier slot is ready and waiting its turn. The ring saying so is right.
                        var beatNow = _replay!.BeatAt(_playheadMs);
                        var since = prev >= 0
                            ? Math.Max(0, beatNow - _replay.BeatAt(prev))
                            : Math.Max(0, beatNow - _replay.FirstBeat + 1) + carryBeats;
                        ringSteps = Math.Max(1, bts - 1);
                        ready = Math.Clamp(since / (float)ringSteps, 0f, 1f);
                    }
                    else if (next != int.MaxValue)
                    {
                        // From the previous cast, or — for the first one of a wave — from wherever the
                        // last wave left the cooldown, which is BEFORE this wave's zero.
                        var from = prev >= 0 ? prev : -carryMs;
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
                    else
                    {
                        // A TIMED SKILL THAT NEITHER FIRED THIS WAVE NOR WILL. Its ring used to sit dead
                        // at empty for the whole wave, which reads as a broken skill rather than a
                        // waiting one — and it is the case a short wave produces most often. The carry
                        // knows how long it has really been waiting, so it keeps filling.
                        var cdMs = MathF.Max(1f, FormBehaviour.BaseCooldownMs(s.Form));
                        ready = Math.Clamp((_playheadMs + carryMs) / cdMs, 0f, 1f);
                    }
                }

                // THE DIAL'S OWN VALUE, needed here rather than below because the medallion is DIMMED
                // while it is short of full. A beat-counted skill STEPS — a sixth of the circle unwinds
                // per action for a six-action skill — so the player reads "two more swings" at a glance
                // instead of a smooth clock that lies about being timed (playtest 2026-08-30: "no need
                // for a bar; the radial gauge again, and each action takes 360/X off it"). A timed skill
                // sweeps continuously.
                var swept = ringSteps > 0
                    ? Math.Clamp((int)MathF.Floor(ready * ringSteps + 0.001f), 0, ringSteps) / (float)ringSteps
                    : ready;

                // READY OR NOT, IN THE ICON'S OWN BRIGHTNESS (playtest 2026-08-28: "kullanılmaya hazır
                // değilken skill'in ikonu kararsın"). The ring around the rim answers HOW MUCH LONGER;
                // this answers WHETHER, and it is the faster read of the two — a glance down the rail
                // sorts the rows into lit and unlit without counting anything.
                //
                // Deliberately a STEP and not a fade: brightening in proportion to the fill would be a
                // second, blurrier copy of the ring, and the moment worth seeing is the one where the
                // skill becomes available. A passive is always ready and so never dims, which is what
                // its ALWAYS ON label promises. The ring itself is drawn at full strength over the top,
                // so a dark medallion never costs the readout any contrast.
                var waiting = swept < 1f && flash <= 0f;
                var wake = waiting ? WaitingSkillDim : 1f;

                // TELEGRAPH: the medallion warms to gold over the 300 ms before its cast, so the effect
                // in the arena has a cause the eye already saw (playtest 2026-08-27).
                var lit = Math.Max(flash, telegraph);
                if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl)
                    b.Draw(sl, box, lit > 0f ? Color.Lerp(Color.White, Gold, lit) : Color.White * wake);
                // §18.3 Layer 1: source-coloured inner glow (no Form-glyph asset ships, so the Source glyph is
                // the central identity and the Form name labels it — a quieter composition per §36).
                _ui.Diamond(b, new Rectangle(box.Center.X - 22, box.Center.Y - 22, 44, 44), sc * (0.28f * wake));
                if (_ui.Assets.Get($"source_{s.Source.ToString().ToLowerInvariant()}") is { } g)
                    b.Draw(g, new Rectangle(box.X + 16, box.Y + 14, box.Width - 32, box.Height - 28), Color.White * wake);
                else _ui.Diamond(b, new Rectangle(box.Center.X - 24, box.Center.Y - 24, 48, 48), sc * wake);
                // (§16 Vow glyph omitted: the loadout SkillChoice doesn't carry the Vow — it lives on the built
                // ability. Wiring the built skills through would add it; deferred, logged once below.)
                // §16.2: a DESCRIPTIVE Source+Form label ("SHADOW STRIKE"), never a bare form name — shrunk to
                // fit the slot pitch rather than clipped.
                var label = $"{s.Source.ToString().ToUpperInvariant()} {FormShort(s.Form)}";
                var lpx = UiTypography.Secondary;
                while (lpx > UiTypography.Caption && _ui.MeasureBig(label, lpx) > RailContentW - box.Width - 20) lpx--;
                // Beside the slot: the name is short and pairs with the medallion.
                _ui.TextBig(b, label, box.Right + 12, box.Y + 10, Bone, lpx);
                // The rhythm, in words, under the name: what the pips count.
                var rhythm = FormBehaviour.CooldownBeats(s.Form) switch
                {
                    2 => "EVERY OTHER ACTION",
                    3 => "EVERY THIRD ACTION",
                    // ORDINAL, like the two above it. "EVERY 4 ACTIONS" reads as four actions of
                    // WAITING and the rule is the other one: the cast IS the fourth action, so the
                    // cycle is cast, hit, hit, hit, cast (playtest 2026-08-28 — "it says 4 but it
                    // casts on the 4th hit"). The dial already agrees: each action fills a quarter of
                    // the ring and it is full exactly on the action it casts on.
                    > 3 => $"EVERY {OrdinalWord(FormBehaviour.CooldownBeats(s.Form))} ACTION",
                    _ => s.Form == Form.Aura ? "ALWAYS ON" : s.Form == Form.Trap ? "WHEN BITTEN" : "TIMED",
                };
                _ui.TextBig(b, rhythm, box.Right + 12, box.Y + 32, Slate, UiTypography.Caption);

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
                // THE COOLDOWN IS ON THE ICON, not under it (playtest 2026-08-26: "the bars confused me —
                // remove the progress bar; while a skill waits, wind a cooldown clockwise over its icon").
                // The waiting share of the medallion is shaded and the shade unwinds clockwise as the
                // skill comes ready; a lit medallion is a ready skill; the gold halo is the cast itself.
                // A BEAT-COUNTED skill (Strike every third action, Projectile every other) shows BEATS: a
                // row of pips under the medallion, one lit per action taken since its last cast, all lit
                // when it is next. A clock on a skill that counts actions read as "still timed" (playtest
                // 2026-08-28). Time-counted skills keep the clockwise sweep.
                // ONE READOUT FOR BOTH KINDS: the clockwise sweep over the medallion — computed above the
                // medallion's own draw, because the icon is dimmed while this is short of full.
                //
                // DRAWN LAST, AND AROUND THE RIM. The dial used to be painted before the medallion's
                // own art, so the frame and the Source glyph covered it — the readout the player is
                // watching sat behind the icon it belongs to (playtest 2026-08-28). It is a ring at the
                // medallion's edge now, over everything, and it never hides the glyph.
                if (flash <= 0f)
                    _ui.CooldownSweep(b, new Vector2(box.Center.X, box.Center.Y), box.Width * 0.50f, swept,
                                      new Color(0x0C, 0x09, 0x16, 0xE0), Gold * 0.95f);
                if (flash > 0f)
                {
                    var halo = new Rectangle(box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6);
                    _ui.Fill(b, new Rectangle(halo.X, halo.Y, halo.Width, 2), Gold * flash);
                    _ui.Fill(b, new Rectangle(halo.X, halo.Bottom - 2, halo.Width, 2), Gold * flash);
                    _ui.Fill(b, new Rectangle(halo.X, halo.Y, 2, halo.Height), Gold * flash);
                    _ui.Fill(b, new Rectangle(halo.Right - 2, halo.Y, 2, halo.Height), Gold * flash);
                }

                var head = BuildGlossary.FormHeadline(s.Form);
                // CLEAR OF THE PIPS. The beat row sits at box.Bottom + 4 and is 10 px tall, so a
                // headline at +12 printed through it — visible in every capture of a beat-counted skill.
                var hy = box.Bottom + 12;
                foreach (var line in _ui.WrapBig(head, RailContentW, UiTypography.Caption).Take(2))
                {
                    _ui.TextBig(b, line, RailContentX, hy, Gold, UiTypography.Caption);
                    hy += 18;
                }
            }
            else
            {
                if (_ui.Assets.Get("ui_slot_skill_hex") is { } sl) b.Draw(sl, box, Color.White * 0.5f);
                _ui.TextCenterBig(b, i == skills.Count ? "+B" : "-", box.Center.X, box.Center.Y - 10, Dim, UiTypography.Secondary);
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
    private const int RailHeadroom = UiTypography.PanelBodyTopBare + 8;   // rail top to the first medallion
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
    // The house margin for a narrow plate. It was 44 a side on a 286-wide rail — a third of the column
    // spent on a vertical frame whose side rail is 24 — which is why the skill lines had to drop to 12
    // and 13 px to fit. At 28 they fit at the Caption rung with room to spare.
    private const int RailContentX = RailX + UiTypography.PanelPadNarrow;
    private const int RailContentW = RailW - UiTypography.PanelPadNarrow * 2;

    /// <summary>How bright a skill's medallion is while it is still waiting to come ready.</summary>
    /// <remarks>
    /// 0.45, which is dark enough to sort the rail into lit and unlit at a glance and light enough that
    /// the Source glyph stays legible — a waiting skill still has to say WHICH skill it is. The empty
    /// slots further down the rail sit at 0.5, so a waiting skill reads as dimmer than a real row but
    /// not as absent.
    /// </remarks>
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
        if (_clipName is not null && (_playheadMs >= _clipStartMs + ClipMs / _clipSpeed + ClipSettleMs || _playheadMs < _clipStartMs))
        {
            _clipName = null;
            _idleFrom = _anim;   // the idle picks up from ITS first frame, not from a random loop phase
        }
        if (_clipName is not null) return;   // committed — plays through
        if (_replay is null) return;

        float? beatMs = null;
        string? clip = null;
        // EACH FORM THROWS ITS OWN SHAPE. The clip is named after the Form, and Character.StripKeys
        // falls back to the old attack/cast pair for any character whose strip is not generated yet —
        // so a Strike is still a swing and a Mark is still a cast until the art lands.
        //
        // A Trap is still excluded HERE because it fires on being bitten rather than on the beat, so it
        // has no beat to be aimed at. It gets its own commitment below.
        if (_replay.NextSkillEventAfter(_playheadMs) is { } nextSkill && (Form)nextSkill.Amount != Form.Trap)
        {
            beatMs = nextSkill.AtMs;
            clip = ((Form)nextSkill.Amount).ToString().ToLowerInvariant();
        }
        // The basic attack's swing. It is a real action now (MIGHT's hit, at TEMPO's cadence) and the
        // sim holds one lock for swings and casts alike, so this clip can never start inside a cast nor
        // a cast inside it — the sword-draw between two Projectiles is a swing the fight actually made.
        if (_nextChampStrikeMs > _playheadMs && (beatMs is null || _nextChampStrikeMs < beatMs.Value))
        {
            beatMs = _nextChampStrikeMs;
            clip = "attack";
        }
        // THE TRAP, WHICH IS NOT AN ACTION. It answers the enemy's bite, off the beat, so it can never
        // be aimed at one — and for the whole life of the fight it therefore had no champion animation
        // at all: the trap bit, the enemy took damage, and the figure stood still through it.
        //
        // Committed opportunistically, and only when nothing else is due: a Trap consumes no beat, so
        // it must never take the clip a real action was about to use. That it fires on the enemy's
        // swing — off the champion's own metronome — is what makes the opening usually there.
        if (beatMs is null && _replay.LastTrapBefore(_playheadMs) is { } trapMs
            && _playheadMs - trapMs < TrapClipGraceMs)
        {
            _clipSpeed = Math.Max(0.6f, ClipMs / (_beatMs * FormBehaviour.ClipShareOfBeat));
            _clipStartMs = trapMs;
            _clipName = "trap";
            return;
        }

        if (beatMs is null) return;

        // EVERY action fills its share of the BEAT (FormBehaviour.ClipShareOfBeat): the sim acts on the
        // beat and only on it, so a clip sized to 0.65 of a beat — plus its settle — is always over
        // before the next action's clip may start, and a fast build visibly fights fast.
        var baseSpeed = Math.Max(0.6f, ClipMs / (_beatMs * FormBehaviour.ClipShareOfBeat));
        var contactMs = ClipMs * ContactFraction / baseSpeed;
        var lead = beatMs.Value - _playheadMs;
        if (lead > contactMs) return;   // not yet: the clip starts one contact-length before the beat

        _clipSpeed = Math.Clamp(baseSpeed * contactMs / Math.Max(1f, lead), baseSpeed, Math.Max(baseSpeed, MaxClipSpeed));
        _clipStartMs = _playheadMs;
        _clipName = clip;
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
    private string FxFor(Form form)
    {
        var f = form.ToString().ToLowerInvariant();
        var own = $"fx_{Character.Id}_{f}_strip8_512";
        return _ui.Assets.Has(own) ? own : $"fx_{f}";
    }

    /// <summary>The build's action-speed multiplier for the wave being shown.</summary>
    private float _castRate = 1f;

    /// <summary>The beat's length for the wave being shown (SoloBattle.BeatFor) — every action clip is sized to it.</summary>
    private float _beatMs = SoloBattle.DefaultBeatMs;

    /// <summary>The settle on an action clip's last frame before idle, ms — the readable END of an action.</summary>
    private const float ClipSettleMs = 150f;

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
        return t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
    }

    /// <summary>
    /// Draw a creature's white silhouette over itself at <paramref name="strength"/>. EVERY enemy path
    /// calls it — the composition, the single creature and the boss — because a flash only the
    /// composition drew is a flash the player sees on swarms and nowhere else (playtest 2026-08-29:
    /// "it only works on the small multi-creatures").
    /// </summary>
    private void FlashOver(SpriteBatch b, string? stripKey, Rectangle box, float seconds, float fps, bool loop, float strength, float crop)
    {
        if (strength <= 0f || stripKey is null || _ui.Assets.WhiteMask(stripKey) is null) return;
        _ui.AnimSprite(b, AssetLibrary.MaskKey(stripKey), box, seconds, fps, loop, Color.White * (0.9f * strength), crop);
    }

    /// <summary>The slotted Aura's Source colour for the wave being shown, or null when the build carries no Aura.</summary>
    private Color? _auraColour;

    /// <summary>Real seconds since the last aura pulse — the pulse runs on the wall clock, not the replay's.</summary>
    private float _auraSincePulse = 999f;

    /// <summary>Seconds between aura pulses — two sim ticks, so the ring is always in the room without stacking.</summary>
    private const float AuraPulseSeconds = FormBehaviour.AuraTickMs / 1000f;

    /// <summary>
    /// The aura's pulse: the authored fx_aura ring at the champion, in the Aura's Source colour, GROWING
    /// across its life until it washes over the enemy row — an always-on field that never touches the
    /// champion's own animation.
    /// </summary>
    /// <remarks>
    /// Three tries. A fixed strip at the champion read as a decoration; a ring drawn from line segments
    /// read as thin and, because it was clocked on the REPLAY's playhead, it froze on screen the moment
    /// a wave ended and the playhead stopped (playtest 2026-08-29: "the aura effect freezes on screen
    /// when the wave ends... the first effect was good, if you can give it the expansion it would be very
    /// good"). So: the first effect, with growth, on the wall clock — VfxPlayer updates with real dt and
    /// is cleared at every wave start, so nothing can be left standing. GrowTo 5.2 takes the ring from
    /// 312 px across to about 1620, so its last frames wash over the pack.
    /// </remarks>
    /// <summary>The field's own metronome — see the note at its call site in UpdateFight.</summary>
    private void PulseAuraOnClock(float dt)
    {
        _auraSincePulse += dt;
        if (_auraColour is not null && _mode == Mode.Fighting && _auraSincePulse >= AuraPulseSeconds)
            PulseAura();
    }

    private void PulseAura()
    {
        if (_auraColour is not { } colour) return;
        _auraSincePulse = 0f;
        _vfx.Play("fx_aura", ChampBox.Center.X, ChampBox.Center.Y + 30, scale: 3, fps: 11f,
                  tint: colour, growTo: 5.2f);
    }
    private int _auraTotal, _auraTotalMs = -1;

    /// <summary>The pending aura tick's total as one plain number over the pack's centre (see the Strike case).</summary>
    private void FlushAuraTotal()
    {
        if (_auraTotal > 0) SpawnDamage(_auraTotal, TargetSlot(), crit: false, skill: false);
        _auraTotal = 0;
        _auraTotalMs = -1;
    }

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
        _ui.GroundShadow(b, box.Center.X, box.Bottom - 10,
                         (int)(box.Width * (0.62f - 0.18f * shadowEase)), 46, 0.6f - 0.25f * shadowEase);

        var tint = dead && !hasDeathClip
            ? Color.Lerp(new Color(0x6A, 0x5A, 0x62), new Color(0x2A, 0x28, 0x34), ease) * (1f - 0.45f * ease)
            : dead ? Color.Lerp(Color.White, new Color(0x8A, 0x80, 0x88), ease * 0.6f)
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
            : !dead && _clipName is not null ? ClipSeconds
            : _anim;

        // A DEAD CHAMPION WITHOUT A DEATH CLIP HOLDS ITS LAST POSE — the fallback freezes the idle.
        if (dead && !hasDeathClip) seconds = 0f;

        // The generated strip, then the idle strip, then the base sprite, then a block. A missing or rejected
        // clip falls back to the still design, so a character whose strip did not survive the quality gate
        // stands there as themselves rather than vanishing. No mirroring: the art faces right (ArtFacesLeft).
        var loop = clip == "idle";
        // MOST SPECIFIC FIRST: the character's own clip for this Form, then the generic attack/cast it
        // stands in for. See Character.StripKeys — the art arrives a character at a time and nothing
        // may blank out while it does.
        foreach (var key in Character.StripKeys(clip))
            if (_ui.AnimSprite(b, key, box, seconds, ChampionFps, loop, tint, -1f,
                               flip: ChampionFacesRight)) return;
        if (_ui.AnimSprite(b, Character.StripKey("idle"), box, dead ? 0f : _anim - _idleFrom, ChampionFps, loop: true, tint, -1f,
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
        var previous = Log.PreviousIn(RegionId);

        // THE REAL COMPARISON, not a hard true. Forcing the flag made the capture print a NEW RECORD
        // title directly above a diff line showing the depth had FALLEN — a report contradicting
        // itself in the same panel. A fixture that lies cannot catch the bug it is posing for.
        Log.Add(_run.Report(isRecord: _run.Wave > (previous?.Depth ?? 0)));   // the same path the game takes
        _mode = Mode.Downed;
        _downedTimer = DownedSeconds;
        _bannerTimer = 0f;   // the wave-cleared banner would otherwise sit over the fallen banner
        _fellWave = Math.Max(1, _run.Wave + 1);
        _fellTimer = DownedSeconds + FellBannerSeconds;

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
    private float? _devSeekMs;
}
