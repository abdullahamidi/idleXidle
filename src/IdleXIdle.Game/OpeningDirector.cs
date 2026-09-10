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
/// <param name="RewardsCredited">How many wave rewards have been paid into the purse.</param>
/// <param name="SignatureHeld">The replay is parked one millisecond before the first Signature cast.</param>
/// <param name="WavesCleared">The descent's own wave counter.</param>
/// <param name="BossSettled">A boss is on stage and standing.</param>
/// <param name="BossesFelled">The career's boss tally.</param>
/// <param name="ChestsOpened">The Forge's monotone chest-open tally.</param>
/// <param name="ItemSelected">An item is selected on the GEAR screen.</param>
/// <param name="ItemsWorn">How many slots are filled.</param>
public readonly record struct OpeningFacts(
    Activity Screen = Activity.Hunt,
    bool ArrivalSettled = false,
    bool EnemySettled = false,
    int RewardsCredited = 0,
    bool SignatureHeld = false,
    int WavesCleared = 0,
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
/// </remarks>
public sealed class OpeningDirector
{
    private OpeningStage _stage = OpeningStage.NotStarted;
    private int _rewardsAtStageStart;
    private int _wavesAtStageStart;
    private bool _acknowledged;

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

    /// <summary>Does the opening own the mouse and the keyboard this frame?</summary>
    public bool OwnsInput => Running && Mode is not TutorialStepMode.LiveExplain;

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
        _rewardsAtStageStart = 0;
        _wavesAtStageStart = 0;
    }

    /// <summary>Restore a persisted cursor. Anything this build does not know resolves to Complete.</summary>
    public void Restore(OpeningStage stage)
    {
        _stage = stage;
        _acknowledged = false;
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
    public void SkipToEnd() => _stage = OpeningStage.Complete;

    /// <summary>Back to nothing — START A NEW GAME runs the whole opening again.</summary>
    public void Reset()
    {
        _stage = OpeningStage.NotStarted;
        _acknowledged = false;
        _rewardsAtStageStart = 0;
        _wavesAtStageStart = 0;
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
        if (!Running) return;
        if (Step is not { } step) { _stage = OpeningScript.After(_stage); return; }

        if (!Satisfied(step.Gate, f)) return;

        _acknowledged = false;
        _rewardsAtStageStart = f.RewardsCredited;
        _wavesAtStageStart = f.WavesCleared;
        _stage = OpeningScript.After(_stage);
    }

    /// <summary>
    /// RESUME AFTER THE DEED. A save reloaded mid-opening must never ask for something already done.
    /// </summary>
    /// <remarks>
    /// The cursor is persisted, but the cursor is not the truth — the deeds are. A player who opened
    /// the chest and quit before the save caught it must not be asked to open it again, and a player
    /// whose item is already worn must not be asked to wear it. So on load the cursor is walked
    /// forward over every stage whose gate is ALREADY answered by production state. Only gates that
    /// describe a lasting fact can do this; an acknowledgement or a settled animation cannot, and
    /// those stages simply replay, which costs the player one CONTINUE.
    /// </remarks>
    public void FastForwardOverDoneDeeds(OpeningFacts f)
    {
        for (var guard = 0; guard < OpeningScript.Steps.Count + 1 && Running; guard++)
        {
            if (Step is not { } step) break;
            if (!Lasting(step.Gate) || !Satisfied(step.Gate, f)) break;
            _stage = OpeningScript.After(_stage);
        }
        _acknowledged = false;
        _rewardsAtStageStart = f.RewardsCredited;
        _wavesAtStageStart = f.WavesCleared;
    }

    /// <summary>
    /// Is this gate answered by a fact that OUTLIVES the session?
    /// </summary>
    /// <remarks>
    /// A chest that has been opened stays opened; an animation that has settled has not happened at
    /// all after a reload. Only the first kind may be fast-forwarded over, or a reload during the
    /// arrival would skip the whole opening on the strength of a boolean that is false again.
    /// </remarks>
    private static bool Lasting(StageGate gate) => gate switch
    {
        StageGate.BossFelled => true,
        StageGate.ChestOpened => true,
        StageGate.ItemWorn => true,
        _ => false,
    };

    private bool Satisfied(StageGate gate, OpeningFacts f) => gate switch
    {
        StageGate.Acknowledged => _acknowledged,
        StageGate.ArrivalSettled => f.ArrivalSettled,
        StageGate.EnemySettled => f.EnemySettled,
        StageGate.FirstRewardCredited => f.RewardsCredited > 0,
        StageGate.SignatureImminent => f.SignatureHeld,
        StageGate.OneWaveCleared => f.WavesCleared > _wavesAtStageStart,
        StageGate.BossSettled => f.BossSettled,
        StageGate.BossFelled => f.BossesFelled > 0,
        StageGate.OnScreen => Step is { Screen: { } want } && f.Screen == want,
        StageGate.ChestOpened => f.ChestsOpened > 0,
        StageGate.ItemSelected => f.ItemSelected,
        StageGate.ItemWorn => f.ItemsWorn > 0,
        _ => true,
    };
}
