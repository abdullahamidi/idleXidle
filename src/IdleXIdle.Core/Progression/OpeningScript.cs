using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Progression;

/// <summary>
/// How loudly a tutorial step holds the player.
/// </summary>
/// <remarks>
/// <para>
/// The old coach had one answer to this — never interrupt, never block, let every click through —
/// and the playtest that followed it was "I started the game and it did not guide me at all". The
/// first minutes of this game are AUTHORED, and an authored moment is allowed to stop the world.
/// </para>
/// <para>
/// The four modes are a ladder of how much they take: a <see cref="Cinematic"/> takes the screen, a
/// <see cref="PauseExplain"/> takes the clock, a <see cref="ForceAction"/> takes every control but
/// one, and a <see cref="LiveExplain"/> takes nothing at all.
/// </para>
/// </remarks>
public enum TutorialStepMode
{
    /// <summary>The prologue's own beats: full screen, no game behind them, its own advance.</summary>
    Cinematic,

    /// <summary>
    /// The world stops and one thing is lit. The player reads it and presses CONTINUE.
    /// </summary>
    /// <remarks>
    /// The fight does not advance: no wave starts, no reward is credited, no income accrues. This is
    /// the mode the whole redesign exists for — "THIS IS YOUR SIGNATURE SKILL" shown after the player
    /// has watched it fire three times teaches nothing.
    /// </remarks>
    PauseExplain,

    /// <summary>The fight carries on underneath. A light and a line, and it leaves on its own.</summary>
    LiveExplain,

    /// <summary>Every control is dead but one rail tile. The player presses the real tile.</summary>
    ForceNavigate,

    /// <summary>Every control is dead but the one the deed needs. The real deed advances the step.</summary>
    ForceAction,
}

/// <summary>
/// What the opening is waiting for before it moves on.
/// </summary>
/// <remarks>
/// A closed set of typed conditions, deliberately: this is not a scripting language and there is no
/// place to write an expression. Each arm is answered by the host from real production state, so a
/// step can only wait for something the game actually produces.
/// </remarks>
public enum StageGate
{
    /// <summary>The player pressed CONTINUE (or the cinematic's own advance).</summary>
    Acknowledged,

    /// <summary>The arrival animation reached the Hunter's idle stance.</summary>
    ArrivalSettled,

    /// <summary>The first creature finished walking on and is standing in its combat position.</summary>
    EnemySettled,

    /// <summary>A creature died and its reward has been credited where the player can see it.</summary>
    FirstRewardCredited,

    /// <summary>The replay is holding one millisecond before the first Signature cast.</summary>
    SignatureImminent,

    /// <summary>A wave has been cleared since the step began — used to let a live lesson expire.</summary>
    OneWaveCleared,

    /// <summary>The tutorial boss has arrived and is standing.</summary>
    BossSettled,

    /// <summary>The tutorial boss is dead.</summary>
    BossFelled,

    /// <summary>The player is standing on the screen this step named.</summary>
    OnScreen,

    /// <summary>A chest has actually been opened.</summary>
    ChestOpened,

    /// <summary>An item is selected in the GEAR inventory.</summary>
    ItemSelected,

    /// <summary>Something is worn.</summary>
    ItemWorn,
}

/// <summary>The ordered stages of the opening. The cursor is one of these; there is no other order.</summary>
/// <remarks>
/// Deliberately a fixed enum rather than a list of ids: the opening is a sequence, and a sequence
/// whose order can be recomputed is a sequence that will be recomputed wrongly. The persisted save
/// stores the ordinal of the stage the player reached, so adding a stage in the middle is a migration
/// decision rather than an accident — see <see cref="OpeningScript.StageOf"/>.
/// </remarks>
public enum OpeningStage
{
    /// <summary>Nothing has happened. A brand-new career sits here until the title is dismissed.</summary>
    NotStarted = 0,

    /// <summary>The illustrated prologue is playing.</summary>
    Prologue = 1,

    /// <summary>The prologue is over; the title's BEGIN THE HUNT is the next real click.</summary>
    AwaitBegin = 2,

    /// <summary>The one-time arrival into Verdant Hollow.</summary>
    Arrival = 3,

    /// <summary>THIS IS YOUR HUNTER. Paused.</summary>
    IntroduceHunter = 4,

    /// <summary>Waiting for the first creature to finish walking on.</summary>
    AwaitFirstEnemy = 5,

    /// <summary>ENEMIES COME IN WAVES. Paused, before the first blow lands.</summary>
    IntroduceEnemy = 6,

    /// <summary>Waiting for the first kill to pay.</summary>
    AwaitFirstReward = 7,

    /// <summary>WAVES PAY GLEAM. Paused, on the capsule that just moved.</summary>
    IntroduceResources = 8,

    /// <summary>The replay is held one millisecond short of the first Signature cast.</summary>
    AwaitSignature = 9,

    /// <summary>SIGNATURE SKILL. Paused. Releasing it fires the real cast.</summary>
    IntroduceSignature = 10,

    /// <summary>YOUR LIFE. Live, over a running fight.</summary>
    IntroduceHealth = 11,

    /// <summary>THE HUNT. Live, on the stage header.</summary>
    IntroduceStage = 12,

    /// <summary>Waiting for the tutorial boss to arrive.</summary>
    AwaitBoss = 13,

    /// <summary>BOSS WAVE. Paused, before it swings.</summary>
    IntroduceBoss = 14,

    /// <summary>Waiting for the boss to fall.</summary>
    AwaitBossFelled = 15,

    /// <summary>A CHEST DROPPED. Paused, on the chest the boss left.</summary>
    IntroduceChest = 16,

    /// <summary>OPEN THE VAULT. Only the VAULT tile is live.</summary>
    ForceVault = 17,

    /// <summary>OPEN THE CHEST. Only the chest card is live.</summary>
    ForceChestOpen = 18,

    /// <summary>YOUR FIRST ITEM. Paused, on what came out.</summary>
    IntroduceItem = 19,

    /// <summary>OPEN GEAR. Only the GEAR tile is live.</summary>
    ForceGear = 20,

    /// <summary>READ IT. Only the inventory is live.</summary>
    ForceItemSelect = 21,

    /// <summary>WHAT IT SAYS. Paused, on the item's own reading.</summary>
    ExplainItem = 22,

    /// <summary>WEAR IT. Only the equip control is live.</summary>
    ForceEquip = 23,

    /// <summary>WORN. Paused, on the paper doll.</summary>
    ShowEquipped = 24,

    /// <summary>The opening is over and normal play is released.</summary>
    Complete = 25,
}

/// <summary>One authored beat: what it says, what it lights, and what lets it go.</summary>
/// <param name="Stage">Which stage this is.</param>
/// <param name="Mode">How much of the game it takes.</param>
/// <param name="Gate">What advances it.</param>
/// <param name="Screen">The screen it happens on, or null for "wherever the player is".</param>
/// <param name="Target">The production region it lights, or null for a step with nothing to light.</param>
/// <param name="Title">Two to five words.</param>
/// <param name="Body">One or two short sentences. Never a paragraph.</param>
public readonly record struct OpeningStep(
    OpeningStage Stage,
    TutorialStepMode Mode,
    StageGate Gate,
    Activity? Screen,
    TourTarget? Target,
    string Title,
    string Body);

/// <summary>
/// THE AUTHORED OPENING. An exact ordered sequence, and the only thing that decides it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a priority list and it must never become one.</b> The previous model chose the
/// highest-priority eligible lesson each frame, which is the right shape for the long tail of the
/// game and the wrong shape for its first five minutes: facts become true in an order the player's
/// understanding does not follow, and the opening came out as a shuffle. Here the order is written
/// down, and a stage advances only when its own <see cref="StageGate"/> is answered.
/// </para>
/// <para>
/// <b>Core knows nothing about how any of this is drawn.</b> A step names a semantic
/// <see cref="TourTarget"/> and the screen that owns it resolves the rectangle, exactly as the
/// optional tours already do. There is no rectangle, no timing, no input and no MonoGame in this
/// file — which is what lets the headless simulation, the offline hunt and every Core test stay
/// completely unaware that a tutorial exists.
/// </para>
/// <para>
/// <b>Training is deliberately absent.</b> Its facts may well become true during the opening — three
/// waves and twenty-five Gleam arrive somewhere around the tutorial boss — and it still does not
/// appear here. The opening owns the player's attention until it is finished; Training teaches itself
/// afterwards, as a contextual lesson, when the player is free to be interrupted.
/// </para>
/// </remarks>
public static class OpeningScript
{
    /// <summary>The wave the tutorial boss stands on. Mirrors the game's own boss cadence.</summary>
    /// <remarks>
    /// Not an extra encounter bolted on for the tutorial: the game already makes every fifth wave a
    /// boss, so the FIRST of those is the tutorial boss and the rhythm afterwards is untouched. Pinned
    /// against <c>ExpeditionTuning.BossEvery</c> by a test.
    /// </remarks>
    public const int TutorialBossWave = 5;

    /// <summary>Every step, in the one order they happen.</summary>
    public static readonly IReadOnlyList<OpeningStep> Steps = new[]
    {
        // ── THE FRONT END. The prologue plays before the player has begun anything. ──────────────
        new OpeningStep(OpeningStage.Prologue, TutorialStepMode.Cinematic, StageGate.Acknowledged,
                        null, null, "", ""),
        new OpeningStep(OpeningStage.AwaitBegin, TutorialStepMode.Cinematic, StageGate.Acknowledged,
                        null, null, "", ""),

        // ── ARRIVAL. One time, per career. No enemy is on stage behind it. ───────────────────────
        new OpeningStep(OpeningStage.Arrival, TutorialStepMode.Cinematic, StageGate.ArrivalSettled,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceHunter, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.Champion,
                        "THIS IS YOUR HUNTER",
                        "It fights on its own — you never press attack. What you decide is how it grows."),

        new OpeningStep(OpeningStage.AwaitFirstEnemy, TutorialStepMode.LiveExplain, StageGate.EnemySettled,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceEnemy, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.Enemies,
                        "ENEMIES COME IN WAVES",
                        "Clear one and the next arrives. No two waves are made of the same thing."),

        new OpeningStep(OpeningStage.AwaitFirstReward, TutorialStepMode.LiveExplain, StageGate.FirstRewardCredited,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceResources, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.CurrencyPills,
                        "WAVES PAY GLEAM",
                        "Every wave your Hunter clears pays Gleam. It is what you grow with."),

        new OpeningStep(OpeningStage.AwaitSignature, TutorialStepMode.LiveExplain, StageGate.SignatureImminent,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceSignature, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.Skills,
                        "ITS OWN SKILL",
                        "Every Hunter has one skill nobody else can use. It fires on its own timer — watch."),

        // ── LIVE. The fight runs underneath these; they leave on their own. ──────────────────────
        new OpeningStep(OpeningStage.IntroduceHealth, TutorialStepMode.LiveExplain, StageGate.OneWaveCleared,
                        Activity.Hunt, TourTarget.HunterHud,
                        "YOUR HUNTER'S LIFE",
                        "At zero the descent ends and it gets back up. You lose nothing you earned."),

        new OpeningStep(OpeningStage.IntroduceStage, TutorialStepMode.LiveExplain, StageGate.OneWaveCleared,
                        Activity.Hunt, TourTarget.Enemies,
                        "HOW DEEP YOU ARE",
                        "The wave you are on, and how far this region is from being yours."),

        // ── THE TUTORIAL BOSS, which is simply the game's own first boss. ────────────────────────
        new OpeningStep(OpeningStage.AwaitBoss, TutorialStepMode.LiveExplain, StageGate.BossSettled,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceBoss, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.Enemies,
                        "A BOSS",
                        "Every fifth wave. Harder, and it leaves something behind."),

        new OpeningStep(OpeningStage.AwaitBossFelled, TutorialStepMode.LiveExplain, StageGate.BossFelled,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceChest, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.NavRail,
                        "IT LEFT A CHEST",
                        "Chests wait in the VAULT until you open them."),

        // ── FORCED. One control is live at a time, and it is the real one. ───────────────────────
        new OpeningStep(OpeningStage.ForceVault, TutorialStepMode.ForceNavigate, StageGate.OnScreen,
                        Activity.Vault, null,
                        "OPEN THE VAULT",
                        "Your chest is in there."),

        new OpeningStep(OpeningStage.ForceChestOpen, TutorialStepMode.ForceAction, StageGate.ChestOpened,
                        Activity.Vault, TourTarget.ChestCards,
                        "OPEN THE CHEST",
                        "See what the boss left you."),

        new OpeningStep(OpeningStage.IntroduceItem, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Vault, TourTarget.ChestCards,
                        "YOUR FIRST ITEM",
                        "Gear is how a Hunter carries what you have earned."),

        new OpeningStep(OpeningStage.ForceGear, TutorialStepMode.ForceNavigate, StageGate.OnScreen,
                        Activity.Gear, null,
                        "OPEN GEAR",
                        "Everything you own is worn from here."),

        new OpeningStep(OpeningStage.ForceItemSelect, TutorialStepMode.ForceAction, StageGate.ItemSelected,
                        Activity.Gear, TourTarget.Inventory,
                        "READ IT FIRST",
                        "Click the item to see what it actually does."),

        new OpeningStep(OpeningStage.ExplainItem, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Gear, TourTarget.ItemDetail,
                        "WHAT AN ITEM SAYS",
                        "Its GRADE, its LEVEL, and the ELEMENT it carries."),

        new OpeningStep(OpeningStage.ForceEquip, TutorialStepMode.ForceAction, StageGate.ItemWorn,
                        Activity.Gear, TourTarget.ItemDetail,
                        "WEAR IT",
                        "The fight only reads what is worn."),

        new OpeningStep(OpeningStage.ShowEquipped, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Gear, TourTarget.PaperDoll,
                        "WORN",
                        "Your Hunter is carrying it now. Go and see what it changes."),
    };

    /// <summary>The step for a stage, or null for a stage the script does not author.</summary>
    public static OpeningStep? Find(OpeningStage stage)
    {
        foreach (var s in Steps)
            if (s.Stage == stage) return s;
        return null;
    }

    /// <summary>The stage after this one, or <see cref="OpeningStage.Complete"/> at the end.</summary>
    public static OpeningStage After(OpeningStage stage)
    {
        for (var i = 0; i < Steps.Count; i++)
            if (Steps[i].Stage == stage)
                return i + 1 < Steps.Count ? Steps[i + 1].Stage : OpeningStage.Complete;
        return OpeningStage.Complete;
    }

    /// <summary>The first stage of a fresh career.</summary>
    public static OpeningStage First => Steps[0].Stage;

    /// <summary>Is the opening still running at this stage?</summary>
    public static bool Running(OpeningStage stage) => stage > OpeningStage.NotStarted && stage < OpeningStage.Complete;

    /// <summary>
    /// A persisted ordinal read back as a stage — the save's own migration guard.
    /// </summary>
    /// <remarks>
    /// The save stores the NUMBER, so a stage inserted in the middle of the enum would silently move
    /// every stored cursor to a different beat. Anything the current build does not recognise resolves
    /// to <see cref="OpeningStage.Complete"/> rather than to a stage the player never reached: the
    /// failure mode of "you are further on than you thought" is a released game, and the failure mode
    /// of the alternative is a soft lock.
    /// </remarks>
    public static OpeningStage StageOf(int saved)
        => Enum.IsDefined(typeof(OpeningStage), saved) ? (OpeningStage)saved : OpeningStage.Complete;

    /// <summary>
    /// Should a loaded save be treated as having already lived the opening?
    /// </summary>
    /// <param name="f">The account's progression, as corroboration.</param>
    /// <param name="fileVersion">The Version of the file that was LOADED.</param>
    /// <remarks>
    /// The sibling of <c>OnboardingLessons.SeedFallLoopAsLived</c> and
    /// <c>Onboarding.SeedExplained</c>'s own gate, and it carries the same law: <b>the version is the
    /// test</b>. A file written before the opening existed cannot carry a cursor, and a player at wave
    /// three hundred must not be shown a prologue. A file at or past
    /// <see cref="FirstVersionWithOpeningState"/> is believed exactly as written — including a brand-new
    /// career that quit during the arrival, which is the case a progression guess gets wrong.
    /// </remarks>
    public static bool SeedOpeningAsLived(UnlockFacts f, int fileVersion)
        => fileVersion < FirstVersionWithOpeningState
           && (f.WavesCleared >= 1 || f.ItemsOwned >= 1 || f.ChestsEverHeld >= 1);

    /// <summary>
    /// THE FIRST SAVE VERSION WHOSE FILES CARRY AN OPENING CURSOR. Frozen forever.
    /// </summary>
    /// <remarks>
    /// A literal, never <c>SaveGame.CurrentVersion</c> — the third constant in this family to carry
    /// that warning, and for the same reason: written against CurrentVersion, the next bump would
    /// start migrating files that already hold an honest cursor, and every player halfway through the
    /// opening would be marked as having finished it.
    /// </remarks>
    public const int FirstVersionWithOpeningState = 6;
}
