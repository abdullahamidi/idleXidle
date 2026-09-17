using System;
using System.Collections.Generic;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Game;

/// <summary>
/// Everything the host knows this frame that an authored step might be waiting for.
/// </summary>
/// <remarks>
/// One record, gathered once, so a stage's gate is answered from production state and never from a
/// timer. Every field is a real fact the game produces for its own reasons — the arrival animation's
/// own completion, the replay's own hold, the Forge's own chest tally — which is what stops the
/// opening from being a puppet show running beside the game instead of on top of it.
/// </remarks>
/// <param name="Screen">Which screen is on top right now.</param>
/// <param name="ArrivalSettled">The arrival animation has reached the Hunter's idle stance.</param>
/// <param name="EnemySettled">A creature has finished walking on and is standing.</param>
/// <param name="RewardsCredited">
/// How many wave rewards have been paid into the purse — one per wave the player watched cleared.
/// </param>
/// <param name="ClearShown">
/// The wave the fight just cleared has finished being SHOWN: every fall played through and faded, and
/// the haul has landed. A presentation fact, true some time after the purse was paid.
/// </param>
/// <param name="SignatureHeld">The replay is parked just short of the first Signature cast.</param>
/// <param name="SignatureLanded">The cast the replay was held before has been released and has played.</param>
/// <param name="BossSettled">A boss is on stage and standing.</param>
/// <param name="BossesFelled">The career's boss tally.</param>
/// <param name="ChestsOpened">The Forge's monotone chest-open tally.</param>
/// <param name="ItemSelected">
/// The item the step lit is the GEAR screen's own selection, put there by the player's click — read
/// off the screen's selection, never set by the tutorial.
/// </param>
/// <param name="ItemsWorn">How many slots are filled.</param>
public readonly record struct OpeningFacts(
    Activity Screen = Activity.Hunt,
    bool ArrivalSettled = false,
    bool EnemySettled = false,
    int RewardsCredited = 0,
    bool ClearShown = false,
    bool SignatureHeld = false,
    bool SignatureLanded = false,
    bool BossSettled = false,
    int BossesFelled = 0,
    int ChestsOpened = 0,
    bool ItemSelected = false,
    int ItemsWorn = 0);

/// <summary>
/// THE AUTHORED OPENING, AS IT RUNS. A cursor over <see cref="OpeningScript"/>, and the authority on
/// what the player is allowed to touch while it holds them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not the coach.</b> <see cref="OnboardingDirector"/> chooses, every frame, whichever
/// eligible lesson ranks highest — the right shape for the long tail of a game whose systems open in
/// an order nobody authored. It is the wrong shape for the first five minutes, where the order IS the
/// teaching, and where a lesson arriving one beat early ("this is your Signature Skill", after three
/// casts) is worse than no lesson. So the opening owns the player until it is finished, and the coach
/// is silent for the whole of it.
/// </para>
/// <para>
/// <b>What it is allowed to do.</b> Stop the fight, take every control but one, and open a screen the
/// player has not unlocked yet. The previous design forbade all three on the grounds that an idle game
/// does not pause to talk to you; the playtest that followed was "I started the game and it did not
/// guide me at all". A paused beat is the only way to say a thing BEFORE it happens.
/// </para>
/// <para>
/// <b>What it may never do.</b> Reach the simulation. Core does not know this class exists: the hold
/// is a host-side decision not to call <c>UpdateExpedition</c>, and the replay barrier is a clamp on a
/// millisecond the screen already owns. Headless combat, the offline hunt and every Core test run
/// exactly as they did before.
/// </para>
/// <para>
/// <b>A deed is not a moment.</b> A gate that names a DEED (a boss felled, a chest opened) is answered
/// live only once the player has also SEEN it — the fall played out — while a reload walks over it on
/// the deed alone, because an animation has not happened at all after a load
/// (<see cref="FastForwardOverDoneDeeds"/>).
/// </para>
/// </remarks>
public sealed class OpeningDirector
{
    private OpeningStage _stage = OpeningStage.NotStarted;
    private int _rewardsAtStageStart;
    private bool _acknowledged;
    private bool _settling;

    /// <summary>Where the opening has got to.</summary>
    public OpeningStage Stage => _stage;

    /// <summary>Is the authored opening running right now?</summary>
    public bool Running => OpeningScript.Running(_stage);

    /// <summary>The step being played, or null when the opening is not running.</summary>
    public OpeningStep? Step => Running ? OpeningScript.Find(_stage) : null;

    /// <summary>The mode of the current step, or null.</summary>
    public TutorialStepMode? Mode => Step?.Mode;

    /// <summary>
    /// Does the live fight stand still this frame?
    /// </summary>
    /// <remarks>
    /// Everything but a <see cref="TutorialStepMode.LiveExplain"/> holds it. The player must SEE a
    /// frozen game — not a still picture with a simulation running behind it — so the host answers
    /// this by not advancing the fight at all: no wave starts, no reward is credited, no income
    /// accrues, and nothing piles up to be applied when the hold lifts.
    /// </remarks>
    public bool HoldsFight => Running && Mode is not TutorialStepMode.LiveExplain;

    /// <summary>
    /// The beat the replay must not cross yet, or null to let it run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two authored windows. From the arrival until the enemy has been introduced, nothing may HIT the
    /// Hunter — the player is being shown the stage, not a fight, and a blow landing during an
    /// explanation is the explanation arriving late. And before the first Signature cast, which is the
    /// moment the whole redesign exists for: the fight screen parks the playhead ahead of the cast's
    /// own wind-up, so NOTHING of it is on screen while the card is read, and it stays parked for the
    /// whole of <see cref="OpeningStage.IntroduceSignature"/>. The card's CONTINUE moves the cursor to
    /// <see cref="OpeningStage.WatchSignature"/>, which holds nothing, and the same real event crosses
    /// with its wind-up, callout, effect and blow.
    /// </para>
    /// <para>
    /// No combat balance moves for this. The wave was resolved before the barrier existed and its
    /// event list is untouched; what is held is presentation.
    /// </para>
    /// </remarks>
    public BattleEventKind? HoldsReplayBefore => _stage switch
    {
        OpeningStage.Arrival or OpeningStage.IntroduceHunter
            or OpeningStage.AwaitFirstEnemy or OpeningStage.IntroduceEnemy => BattleEventKind.EnemyStrike,
        OpeningStage.AwaitSignature or OpeningStage.IntroduceSignature => BattleEventKind.Skill,
        _ => null,
    };

    /// <summary>
    /// Must the fight keep its next wave off the stage?
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wherever a card is about to be about a clear. The fight screen then lets its between-wave break
    /// run every beat of its own — the fall, the haul, the empty stage — and rests on that empty stage
    /// instead of walking the next pack in over a body that is still falling. A fall is 1.85 s and a
    /// break 1.1 s (a boss's 1.6 s), so without this the next wave always arrives mid-fall.
    /// </para>
    /// <para>
    /// The first clear, until the GLEAM card has been answered. The released Signature cast, while it
    /// plays out: the first cast of a fresh career is the killing blow of its wave (measured 2026-09-11:
    /// wave 2, 3700 ms), so its effect IS a fall. And the tutorial boss, until its fall has been shown
    /// and "A CHEST DROPPED" can be said over an empty stage. Lifted the moment the cursor moves on.
    /// </para>
    /// </remarks>
    public bool HoldsNextWave => _stage is OpeningStage.AwaitFirstReward or OpeningStage.IntroduceResources
                                           or OpeningStage.WatchSignature or OpeningStage.AwaitBossFelled;

    /// <summary>
    /// Must the fight keep its next wave off the stage after the clear it is showing now?
    /// </summary>
    /// <param name="bossWave">Was the wave on stage — the one whose clear is being shown — a boss wave?</param>
    /// <remarks>
    /// <see cref="HoldsNextWave"/>, narrowed to the clear the beat is actually about. The boss beat is
    /// about the BOSS'S fall: a Hunter who loses to the tutorial boss starts again at wave one, and a hold
    /// on that wave's clear would rest the break forever, waiting for a boss that can no longer arrive
    /// (measured 2026-09-11 by the autoplayed opening — the one run of the whole live sequence this
    /// project had ever made). Every other holding beat is about whichever clear comes first.
    /// </remarks>
    public bool HoldsNextWaveAfter(bool bossWave)
        => HoldsNextWave && (_stage != OpeningStage.AwaitBossFelled || bossWave);

    /// <summary>Screens the opening has to open ahead of their own unlock fact, at this stage.</summary>
    /// <remarks>
    /// The VAULT's real gate is a chest that really exists and GEAR's is an item that really exists,
    /// and each becomes true within a beat of the grant. The grant is what lets the chain break ON THE
    /// TUTORIAL'S WORD — "the boss dropped a chest, open it in the Vault" — instead of a frame later,
    /// beside an unrelated animation.
    /// </remarks>
    public Activity? GrantsScreen => _stage switch
    {
        OpeningStage.IntroduceChest or OpeningStage.ForceVault or OpeningStage.ForceChestOpen
            or OpeningStage.IntroduceItem => Activity.Vault,
        OpeningStage.ForceGear or OpeningStage.ForceItemSelect or OpeningStage.ExplainItem
            or OpeningStage.ForceEquip or OpeningStage.ShowEquipped => Activity.Gear,
        _ => null,
    };

    /// <summary>Does the opening own the mouse and the keyboard this frame?</summary>
    /// <remarks>
    /// Every beat but a live one — and a LIVE beat too, for the short time between its deed and its
    /// moment: the boss has fallen but its fall is still playing, the first wave has paid but its clear
    /// is still being shown, the cast has been let go but has not landed. The next card is about that
    /// moment, and a player who used those two seconds to walk into the freshly unchained VAULT met the
    /// card on a screen it is not drawn on (adversarial review, 2026-09-11). The fight keeps running;
    /// only the controls wait for the moment to land.
    /// </remarks>
    public bool OwnsInput => Running && (Mode is not TutorialStepMode.LiveExplain || _settling);

    /// <summary>Is the player being asked to press CONTINUE?</summary>
    public bool WantsAcknowledgement => Running && Mode is TutorialStepMode.PauseExplain;

    /// <summary>
    /// The ONE rail tile that is live, or null when the rail is not the answer.
    /// </summary>
    /// <remarks>
    /// A forced navigation names the screen it wants; every other tile and every nav hotkey is dead
    /// until the player presses that one. The tile is the game's own, in its own place — there is no
    /// tutorial-only button anywhere in this design.
    /// </remarks>
    public Activity? ForcedNav
        => Step is { Mode: TutorialStepMode.ForceNavigate, Screen: { } s } ? s : null;

    /// <summary>
    /// The ONE production region that takes a click, or null.
    /// </summary>
    /// <remarks>
    /// Only a <see cref="TutorialStepMode.ForceAction"/> narrows the pointer to a rectangle: a paused
    /// explanation wants nothing but CONTINUE, and a live one takes nothing at all. The rectangle is
    /// resolved by the screen that owns the target, so draw geometry and hit geometry are the same
    /// rectangle at every UI density.
    /// </remarks>
    public TourTarget? ForcedTarget
        => Step is { Mode: TutorialStepMode.ForceAction, Target: { } t } ? t : null;

    /// <summary>The screen this step happens on, or null for "wherever the player is".</summary>
    public Activity? Screen => Step?.Screen;

    /// <summary>What the step lights, or null.</summary>
    public TourTarget? Target => Step?.Target;

    /// <summary>
    /// Begin the opening at its first stage. Called once, when a brand-new career leaves the title.
    /// </summary>
    public void Begin()
    {
        _stage = OpeningScript.First;
        _acknowledged = false;
        _settling = false;
        _rewardsAtStageStart = 0;
    }

    /// <summary>Restore a persisted cursor. Anything this build does not know resolves to Complete.</summary>
    public void Restore(OpeningStage stage)
    {
        _stage = stage;
        _acknowledged = false;
        _settling = false;
    }

    /// <summary>The player pressed CONTINUE, or the cinematic reached its end.</summary>
    public void Acknowledge() => _acknowledged = true;

    /// <summary>
    /// End the opening now, without pretending anything was taught.
    /// </summary>
    /// <remarks>
    /// SKIP TUTORIAL, and the honest kind: it releases the holds and the input restrictions and does
    /// nothing else. No item is granted, no unlock is moved, no fact is fabricated. A player who skips
    /// simply plays the game from wherever they actually are — including, if they have not yet felled
    /// the first boss, without the welcome gift, because that gift comes from the boss now.
    /// </remarks>
    public void SkipToEnd()
    {
        _stage = OpeningStage.Complete;
        _settling = false;
    }

    /// <summary>Back to nothing — START A NEW GAME runs the whole opening again.</summary>
    public void Reset()
    {
        _stage = OpeningStage.NotStarted;
        _acknowledged = false;
        _settling = false;
        _rewardsAtStageStart = 0;
    }

    /// <summary>
    /// Answer the current stage's gate from live facts, and step the cursor when it is satisfied.
    /// </summary>
    /// <remarks>
    /// Called once a frame. Pure with respect to the game: it reads facts and moves a cursor, and it
    /// cannot start a wave, grant an item or open a screen. The host does all of those, and only in
    /// response to the stage it finds the cursor on.
    /// </remarks>
    public void Update(OpeningFacts f)
    {
        if (!Running) { _settling = false; return; }
        if (Step is not { } step) { _stage = OpeningScript.After(_stage); _settling = false; return; }

        _settling = step.Mode is TutorialStepMode.LiveExplain && Settling(step.Gate, f);

        // A NARRATED LIVE BEAT IS NOT SPENT WHILE NOBODY IS LOOKING AT IT. A LiveExplain takes no
        // control, so the player is free to walk off to the FORGE while "HEALTH" waits on the next
        // cleared wave — and the wave clears anyway, because the fight is running. Holding the gate
        // until they are back on the beat's own screen costs nothing (the game is not paused) and is
        // the difference between a lesson delivered and a lesson quietly burned.
        if (step.Mode is TutorialStepMode.LiveExplain && step.Title.Length > 0
            && step.Screen is { } narratedOn && f.Screen != narratedOn) return;

        if (!Satisfied(step.Gate, f)) return;

        _acknowledged = false;
        _settling = false;
        _rewardsAtStageStart = f.RewardsCredited;
        _stage = OpeningScript.After(_stage);
    }

    /// <summary>
    /// RESUME AFTER THE DEED. A save reloaded mid-opening must never ask for something already done.
    /// </summary>
    /// <remarks>
    /// The cursor is persisted, but the cursor is not the truth — the deeds are. A player who opened
    /// the chest and quit before the save caught it must not be asked to open it again, and a player
    /// whose item is already worn must not be asked to wear it. So on load the cursor is walked
    /// forward over every stage whose DEED production state already proves. Only gates that name a
    /// lasting deed can do this, and they are asked about the deed alone — the presentation half of a
    /// live gate (a fall shown) has not happened at all after a reload. An acknowledgement or a
    /// settled animation cannot be walked over; those stages simply replay, which costs one CONTINUE.
    /// </remarks>
    public void FastForwardOverDoneDeeds(OpeningFacts f)
    {
        for (var guard = 0; guard < OpeningScript.Steps.Count + 1 && Running; guard++)
        {
            if (Step is not { } step) break;
            if (!DeedDone(step.Gate, f)) break;
            _stage = OpeningScript.After(_stage);
        }
        _acknowledged = false;
        _settling = false;
        _rewardsAtStageStart = f.RewardsCredited;
    }

    /// <summary>
    /// Has the lasting DEED this gate names been done? False for every gate that names no deed.
    /// </summary>
    /// <remarks>
    /// A chest that has been opened stays opened; an animation that has settled has not happened at
    /// all after a reload. Only the first kind may be fast-forwarded over, or a reload during the
    /// arrival would skip the whole opening on the strength of a boolean that is false again.
    /// </remarks>
    private static bool DeedDone(StageGate gate, OpeningFacts f) => gate switch
    {
        StageGate.BossFelled => f.BossesFelled > 0,
        StageGate.ChestOpened => f.ChestsOpened > 0,
        StageGate.ItemWorn => f.ItemsWorn > 0,
        _ => false,
    };

    /// <summary>
    /// Has a LIVE gate's deed happened while its moment is still on screen?
    /// </summary>
    /// <remarks>The window <see cref="OwnsInput"/> takes the controls for — see its remarks.</remarks>
    private static bool Settling(StageGate gate, OpeningFacts f) => gate switch
    {
        StageGate.FirstRewardShown => f.RewardsCredited > 0 && !f.ClearShown,
        StageGate.BossFelled => f.BossesFelled > 0 && !f.ClearShown,
        StageGate.SignatureLanded => !f.SignatureLanded,
        _ => false,
    };

    private bool Satisfied(StageGate gate, OpeningFacts f) => gate switch
    {
        StageGate.Acknowledged => _acknowledged,
        StageGate.ArrivalSettled => f.ArrivalSettled,
        StageGate.EnemySettled => f.EnemySettled,
        // PAID IS NOT SEEN. The credit lands on the kill's own frame; the card waits for the clear to
        // have been watched to its end.
        StageGate.FirstRewardShown => f.RewardsCredited > 0 && f.ClearShown,
        StageGate.SignatureImminent => f.SignatureHeld,
        StageGate.SignatureLanded => f.SignatureLanded,
        // A WAVE THE PLAYER SAW CLEARED. The descent counts its depth when a wave is PUSHED — at the
        // start of its replay — so a live card gated on depth left the screen a second after it arrived,
        // at the next wave's first frame. A reward is paid when the replay runs out, which is the clear
        // the card has been sitting over.
        StageGate.OneWaveCleared => f.RewardsCredited > _rewardsAtStageStart,
        StageGate.BossSettled => f.BossSettled,
        // FELLED AND SEEN FALLING. The tally moves on the boss's final blow; "A CHEST DROPPED" waits for
        // the fall it is about to follow, exactly as GLEAM waits for the first one.
        StageGate.BossFelled => f.BossesFelled > 0 && f.ClearShown,
        StageGate.OnScreen => Step is { Screen: { } want } && f.Screen == want,
        StageGate.ChestOpened => f.ChestsOpened > 0,
        StageGate.ItemSelected => f.ItemSelected,
        StageGate.ItemWorn => f.ItemsWorn > 0,
        _ => true,
    };
}
