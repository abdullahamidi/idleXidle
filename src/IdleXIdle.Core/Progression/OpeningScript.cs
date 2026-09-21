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

    /// <summary>
    /// A wave has paid AND the player has watched it finish: the last creature's fall has played
    /// through and faded, and what the wave paid has landed.
    /// </summary>
    /// <remarks>
    /// Two facts, and the second is the lesson. The purse is paid on the kill's own frame, which is the
    /// right moment for the economy and the wrong one for "cleared waves give you Gleam": a card raised
    /// on the credit alone froze the last creature one frame into its fall (playtest 2026-09-11). The
    /// host answers the second half from the fight screen's own presentation, never from a timer.
    /// </remarks>
    FirstRewardShown,

    /// <summary>The replay is holding just short of the first Signature cast — before any of it shows.</summary>
    SignatureImminent,

    /// <summary>
    /// The cast the replay was held before has been let go and has PLAYED: that same event crossed and
    /// the Hunter's cast animation finished.
    /// </summary>
    /// <remarks>
    /// What stops the next beat's card from being drawn over the moment the previous one promised.
    /// "It activates automatically" is followed by the cast, on its own, with nothing on top of it.
    /// </remarks>
    SignatureLanded,

    /// <summary>A wave has been cleared since the step began — used to let a live lesson expire.</summary>
    OneWaveCleared,

    /// <summary>The tutorial boss has arrived and is standing.</summary>
    BossSettled,

    /// <summary>
    /// The tutorial boss is dead — and, live, its fall has been SHOWN, the way the first clear's is.
    /// </summary>
    /// <remarks>
    /// On a reload the deed alone answers it: a fall is a moment on a screen, and after a load that
    /// moment has not happened at all.
    /// </remarks>
    BossFelled,

    /// <summary>The player is standing on the screen this step named.</summary>
    OnScreen,

    /// <summary>
    /// TRAINING is open and the purse can afford a first rank — the moment the lesson can be obeyed.
    /// </summary>
    /// <remarks>
    /// Both halves, because either alone is a prompt that cannot be acted on. The screen's gate is
    /// <c>Unlocks.IsOpen(Activity.Training, …)</c> — three cleared waves — and the price is
    /// <c>OnboardingLessons.FirstRankCost</c>, which is pinned against the real tuning. Asked of the
    /// same facts navigation uses, so the opening can never walk the player at a chained tile.
    /// </remarks>
    TrainingAffordable,

    /// <summary>A rank has actually been bought — any stat, never a named one.</summary>
    /// <remarks>
    /// The deed, and only the deed. A card closed is not a rank trained, exactly as a card closed is
    /// not a chest opened: the host answers this from the Hunter's own summed ranks, so the fact the
    /// step waits for is the fact the game records.
    /// </remarks>
    StatTrained,

    /// <summary>A chest has actually been opened.</summary>
    ChestOpened,

    /// <summary>
    /// The item the step lit is the GEAR screen's selection, and the player's own click put it there.
    /// </summary>
    ItemSelected,

    /// <summary>Something is worn.</summary>
    ItemWorn,
}

/// <summary>The ordered stages of the opening. The cursor is one of these; there is no other order.</summary>
/// <remarks>
/// Deliberately a fixed enum rather than a list of ids: the opening is a sequence, and a sequence
/// whose order can be recomputed is a sequence that will be recomputed wrongly. The persisted save
/// stores the ordinal of the stage the player reached, so adding a stage in the middle is a migration
/// decision rather than an accident. It has been made once — <see cref="WatchSignature"/>, save
/// version 7 — and <see cref="OpeningScript.StageOf(int, int)"/> is where the older numbering is read.
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

    /// <summary>YOUR HUNTER. Paused.</summary>
    IntroduceHunter = 4,

    /// <summary>Waiting for the first creature to finish walking on.</summary>
    AwaitFirstEnemy = 5,

    /// <summary>ENEMIES. Paused, before the first blow lands.</summary>
    IntroduceEnemy = 6,

    /// <summary>Waiting for the first wave to pay, and for its clear to finish being shown.</summary>
    AwaitFirstReward = 7,

    /// <summary>GLEAM. Paused, on the capsule that just moved, with the next wave held back.</summary>
    IntroduceResources = 8,

    /// <summary>The replay is held just short of the first Signature cast.</summary>
    AwaitSignature = 9,

    /// <summary>SIGNATURE SKILL. Paused, before the cast. CONTINUE lets the real cast go.</summary>
    IntroduceSignature = 10,

    /// <summary>The released cast plays out, whole, with nothing drawn over it.</summary>
    WatchSignature = 11,

    /// <summary>HEALTH. Live, over a running fight.</summary>
    IntroduceHealth = 12,

    /// <summary>THE HUNT. Live, on the stage header.</summary>
    IntroduceStage = 13,

    // ── THE FIRST DECISION. Live until the purse can pay for one, then four beats that spend it. ──

    /// <summary>Waiting for TRAINING to open and for the purse to afford a first rank. Live.</summary>
    AwaitTraining = 14,

    /// <summary>TRAINING. Paused, on the TRAINING tile the next step makes the player press.</summary>
    IntroduceTraining = 15,

    /// <summary>OPEN TRAINING. Only the TRAINING tile is live.</summary>
    ForceTraining = 16,

    /// <summary>TRAIN ANY STAT. Only the stat rows are live; a real rank advances it.</summary>
    ForceTrainStat = 17,

    /// <summary>YOUR CHOICE STANDS. Paused, on the rows the rank just moved.</summary>
    ShowTrained = 18,

    /// <summary>Waiting for the tutorial boss to arrive.</summary>
    AwaitBoss = 19,

    /// <summary>BOSS WAVE. Paused, before it swings.</summary>
    IntroduceBoss = 20,

    /// <summary>Waiting for the boss to fall.</summary>
    AwaitBossFelled = 21,

    /// <summary>A CHEST DROPPED. Paused, on the VAULT tile the chest went to.</summary>
    IntroduceChest = 22,

    /// <summary>OPEN THE VAULT. Only the VAULT tile is live.</summary>
    ForceVault = 23,

    /// <summary>OPEN THE CHEST. Only the chest's own card is live.</summary>
    ForceChestOpen = 24,

    /// <summary>YOUR FIRST ITEM. Paused, on the reveal of what came out, which is held open while it is read.</summary>
    IntroduceItem = 25,

    /// <summary>OPEN GEAR. Only the GEAR tile is live.</summary>
    ForceGear = 26,

    /// <summary>SELECT THE ITEM. Only the item's own cell is live.</summary>
    ForceItemSelect = 27,

    /// <summary>ITEM STATS. Paused, on the inspector the click filled.</summary>
    ExplainItem = 28,

    /// <summary>EQUIP IT. Only the EQUIP button is live.</summary>
    ForceEquip = 29,

    /// <summary>BACK TO THE HUNT. Paused, on the paper doll; the acknowledgement returns to the fight.</summary>
    ShowEquipped = 30,

    /// <summary>The opening is over and normal play is released.</summary>
    Complete = 31,
}

/// <summary>One authored beat: what it says, what it lights, and what lets it go.</summary>
/// <param name="Stage">Which stage this is.</param>
/// <param name="Mode">How much of the game it takes.</param>
/// <param name="Gate">What advances it.</param>
/// <param name="Screen">The screen it happens on, or null for "wherever the player is".</param>
/// <param name="Target">The production region it lights, or null for a step with nothing to light.</param>
/// <param name="Title">Two to five words.</param>
/// <param name="Body">One or two short, literal sentences. Never a paragraph, never a metaphor.</param>
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
/// <b>The words are plain on purpose.</b> The prologue may be atmospheric; a card the player has to
/// act on may not. Each body is one or two short literal sentences about the one thing that is lit —
/// no metaphor, no dash-joined clauses, nothing about a system the player has not seen yet. The
/// playtest that set this rule (2026-09-11) stopped on "No two waves are made of the same thing."
/// </para>
/// <para>
/// <b>Training is taught here, and that reverses an earlier decision.</b> It used to be deliberately
/// absent, on the reasoning that the opening owns the player until it is finished and Training could
/// teach itself afterwards as a contextual lesson. The playtest that reversed it found the obvious
/// hole in that: the first minutes teach the player to WATCH — the hunter fights, the waves pay, the
/// skill fires on its own — and then hand over a game whose entire premise is that you choose how it
/// grows, without ever asking them to choose anything. The chest and the item that follow are things
/// the game GIVES; a rank is the first thing the player SPENDS. So the chapter sits before the boss,
/// where its facts first become true, and it forces the deed and never the choice: TRAIN ANY STAT,
/// never TRAIN MIGHT. Nothing about which stat is authored anywhere in this file.
/// </para>
/// <para>
/// <b>And it waits for a purse that can pay.</b> <see cref="StageGate.TrainingAffordable"/> is both
/// halves — the screen's own unlock and the first rank's price — because a card that says TRAIN ANY
/// STAT over a row that refuses is the prompt-that-cannot-be-obeyed the whole catalogue forbids.
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
                        "YOUR HUNTER",
                        "Your Hunter fights automatically. You choose how they grow."),

        new OpeningStep(OpeningStage.AwaitFirstEnemy, TutorialStepMode.LiveExplain, StageGate.EnemySettled,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceEnemy, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.Enemies,
                        "ENEMIES",
                        "Enemies arrive in waves. Every fifth wave is a boss."),

        // THE CLEAR IS WATCHED BEFORE IT IS NAMED: the last fall plays through and fades, the haul
        // lands, and only then does the card arrive — with the next wave held back behind it.
        new OpeningStep(OpeningStage.AwaitFirstReward, TutorialStepMode.LiveExplain, StageGate.FirstRewardShown,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceResources, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.CurrencyPills,
                        "GLEAM",
                        "Cleared waves give you Gleam. You spend Gleam to grow stronger."),

        // THE CAST IS NAMED BEFORE ANY OF IT IS SHOWN: the replay parks ahead of the cast's own wind-up,
        // CONTINUE lets that same event go, and nothing is drawn over it until it has played.
        new OpeningStep(OpeningStage.AwaitSignature, TutorialStepMode.LiveExplain, StageGate.SignatureImminent,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceSignature, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.Skills,
                        "SIGNATURE SKILL",
                        "This skill belongs to this Hunter. It activates automatically."),

        new OpeningStep(OpeningStage.WatchSignature, TutorialStepMode.LiveExplain, StageGate.SignatureLanded,
                        Activity.Hunt, null, "", ""),

        // ── LIVE. The fight runs underneath these; they leave on their own. ──────────────────────
        new OpeningStep(OpeningStage.IntroduceHealth, TutorialStepMode.LiveExplain, StageGate.OneWaveCleared,
                        Activity.Hunt, TourTarget.HunterHud,
                        "HEALTH",
                        // THE HUD'S OWN WORDS. The header's wave lane reads "WAVE 1" again after a fall on a
                        // fresh career; "descent" is a word nothing on this screen shows (review 2026-09-11).
                        "If Health reaches zero, your Hunter falls. They start again from wave 1."),

        new OpeningStep(OpeningStage.IntroduceStage, TutorialStepMode.LiveExplain, StageGate.OneWaveCleared,
                        Activity.Hunt, TourTarget.StageHeader,
                        "THE HUNT",
                        "This shows your wave and your progress through the region."),

        // ── THE FIRST DECISION. The fight runs until the purse can pay for a rank; then it stops. ──
        //
        // The wait is LIVE, because the Gleam that pays for the rank is earned by the fight the player
        // is watching. Everything after it is paused or forced, which is also what keeps the tutorial
        // boss off the stage: a held beat does not advance the fight at all, so wave five cannot walk
        // on in the middle of the chapter.
        new OpeningStep(OpeningStage.AwaitTraining, TutorialStepMode.LiveExplain, StageGate.TrainingAffordable,
                        Activity.Hunt, null, "", ""),

        // THE TILE IS LIT BY THE CHAIN, not by name: a beat aimed at the rail lights whichever tile the
        // next forced step is going to ask for (NextForcedScreen), which is TRAINING.
        new OpeningStep(OpeningStage.IntroduceTraining, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.NavRail,
                        "TRAINING",
                        "Gleam buys permanent stats for your Hunter. Spend some in Training."),

        new OpeningStep(OpeningStage.ForceTraining, TutorialStepMode.ForceNavigate, StageGate.OnScreen,
                        Activity.Training, null,
                        "OPEN TRAINING",
                        "Your Gleam is spent here."),

        // ANY STAT. The rows are lit as one region because the deed is "make a choice", and lighting
        // one row would be making it for them. The panel holds only the stat rows and their TRAIN
        // buttons — RESET ALL TRAINING sits below it, outside the light and outside the click.
        new OpeningStep(OpeningStage.ForceTrainStat, TutorialStepMode.ForceAction, StageGate.StatTrained,
                        Activity.Training, TourTarget.TrainingRows,
                        "TRAIN ANY STAT",
                        // THE DEED, NEVER THE CONTROL — and never a key. "Press TRAIN" named a button
                        // and tripped the hotkey guard that exists to stop exactly that habit; the lit
                        // control is what says where to click, and the card says what it is for.
                        "Pick a stat and train it once. Any of them is a fine first choice."),

        new OpeningStep(OpeningStage.ShowTrained, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Training, TourTarget.TrainingRows,
                        "YOUR CHOICE STANDS",
                        "That rank is permanent. Keep earning Gleam and keep spending it."),

        // ── THE TUTORIAL BOSS, which is simply the game's own first boss. ────────────────────────
        new OpeningStep(OpeningStage.AwaitBoss, TutorialStepMode.LiveExplain, StageGate.BossSettled,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceBoss, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.Enemies,
                        "BOSS WAVE",
                        "Bosses are tougher enemies. This one carries a special reward."),

        new OpeningStep(OpeningStage.AwaitBossFelled, TutorialStepMode.LiveExplain, StageGate.BossFelled,
                        Activity.Hunt, null, "", ""),

        new OpeningStep(OpeningStage.IntroduceChest, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Hunt, TourTarget.NavRail,
                        "A CHEST DROPPED",
                        "The boss dropped a chest. Open it in the Vault."),

        // ── FORCED. One control is live at a time, and it is the real one. ───────────────────────
        new OpeningStep(OpeningStage.ForceVault, TutorialStepMode.ForceNavigate, StageGate.OnScreen,
                        Activity.Vault, null,
                        "OPEN THE VAULT",
                        "Your chest is waiting here."),

        new OpeningStep(OpeningStage.ForceChestOpen, TutorialStepMode.ForceAction, StageGate.ChestOpened,
                        Activity.Vault, TourTarget.ChestCard,
                        "OPEN THE CHEST",
                        "Open it to reveal your reward."),

        // THE ITEM ITSELF IS LIT — the chest's reveal card, held open while this is read. Lighting the
        // chest row instead lit "THE VAULT IS EMPTY" the moment the reveal faded (review 2026-09-11).
        new OpeningStep(OpeningStage.IntroduceItem, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Vault, TourTarget.RevealedItem,
                        "YOUR FIRST ITEM",
                        "Items only help when equipped. Let's look at this one first."),

        new OpeningStep(OpeningStage.ForceGear, TutorialStepMode.ForceNavigate, StageGate.OnScreen,
                        Activity.Gear, null,
                        "OPEN GEAR",
                        "Your new item is here."),

        // ONE CARD ASKS FOR THE CLICK AND THE NEXT ONE EXPLAINS WHAT IT OPENED. The inspector is empty
        // until the player picks the item; a card that named its fields before then would be pointing
        // at something that is not on the screen yet.
        new OpeningStep(OpeningStage.ForceItemSelect, TutorialStepMode.ForceAction, StageGate.ItemSelected,
                        Activity.Gear, TourTarget.InventoryItem,
                        "SELECT THE ITEM",
                        "Click the item to see its stats."),

        // THE WORDS THE PANEL PRINTS. Its top line reads "COMMON BLADE · NATURE · LEVEL 1" and its
        // stats sit under the heading WHAT IT DOES — the card names those, never a term the panel lacks.
        new OpeningStep(OpeningStage.ExplainItem, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Gear, TourTarget.ItemDetail,
                        "ITEM STATS",
                        // The panel's own heading sits early on the first line, so no card width can
                        // wrap it into "WHAT IT / DOES" (seen at 100 %, 2026-09-11).
                        "The top line shows rarity, element and level. WHAT IT DOES lists the stats."),

        new OpeningStep(OpeningStage.ForceEquip, TutorialStepMode.ForceAction, StageGate.ItemWorn,
                        Activity.Gear, TourTarget.EquipButton,
                        "EQUIP IT",
                        "Equip the item to use its stats."),

        new OpeningStep(OpeningStage.ShowEquipped, TutorialStepMode.PauseExplain, StageGate.Acknowledged,
                        Activity.Gear, TourTarget.PaperDoll,
                        "BACK TO THE HUNT",
                        "Your Hunter is stronger now."),
    };

    /// <summary>
    /// One beat of the illustrated prologue: a stable id (the host's plate is keyed by it — see
    /// <c>PrologueArt</c> in the Game assembly), a heading and the line under it.
    /// </summary>
    public readonly record struct PrologueBeat(string Id, string Title, string Body);

    /// <summary>
    /// THE STORY, BEFORE THE GAME. Six beats, once per career.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every line here is <c>design/narrative/world.md</c> said out loud, and nothing in it is
    /// invented: the six pressures, the joints that hold a place level by remembering its shape, the
    /// tear, and a Hunter as the reference a joint measures itself against. It is here rather than in
    /// the host because it is COPY — the same reason the steps are here — and because the one thing a
    /// prologue must never do is drift away from what the rest of the game says it is.
    /// </para>
    /// <para>
    /// It says nothing about a control, a screen or a resource. Teaching starts after BEGIN THE HUNT;
    /// this is the reason any of it matters.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<PrologueBeat> Prologue = new[]
    {
        new PrologueBeat("pressures", "SIX PRESSURES",
                         "Under every place in this world lies one of six pressures. "
                         + "Where one lies too close, the place above it forgets what it was."),
        new PrologueBeat("joints", "THE JOINTS",
                         "An older order drove joints into those places to hold them level. "
                         + "A joint works by remembering the shape a place is supposed to have."),
        new PrologueBeat("tear", "THE NETWORK FAILED",
                         "The joints tore. What rises through a torn one is corruption — "
                         + "a pressure coming up through a place with nothing left to say no."),
        new PrologueBeat("hunter", "A HUNTER",
                         "A Hunter is a person the network learned by heart. "
                         + "It sends one down, and it leaves her there to stand."),
        new PrologueBeat("stands", "SHE STANDS",
                         "Stand deep enough, for long enough, and the joint remembers. "
                         + "Nothing else has ever worked, and it is not kind."),
        new PrologueBeat("hollow", "VERDANT HOLLOW",
                         "The first joint that ever held, and the one the camp was dug into. "
                         + "It is where you begin."),
    };

    /// <summary>
    /// The screen the next forced navigation asks for, from this stage on — or null.
    /// </summary>
    /// <remarks>
    /// What a beat aimed at <c>TourTarget.NavRail</c> is really about. "The boss dropped a chest — open
    /// it in the Vault" wants the VAULT'S TILE lit, not the whole rail, and the tile it wants is by
    /// definition the one the very next forced step is going to make the player press. Read from the
    /// script rather than written down twice, so re-ordering the chain moves the light with it.
    /// </remarks>
    public static Activity? NextForcedScreen(OpeningStage stage)
    {
        var reached = false;
        foreach (var s in Steps)
        {
            if (s.Stage == stage) reached = true;
            if (reached && s.Mode == TutorialStepMode.ForceNavigate && s.Screen is { } want) return want;
        }
        return null;
    }

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
    /// A persisted ordinal read back as a stage, in the numbering of the file that wrote it.
    /// </summary>
    /// <param name="saved">The ordinal the file carries.</param>
    /// <param name="fileVersion">The Version of the file that was LOADED.</param>
    /// <remarks>
    /// <para>
    /// <see cref="OpeningStage.WatchSignature"/> is the first stage ever inserted mid-sequence. A file
    /// written before it (version 6) numbered IntroduceHealth as 11 and Complete as 25, so every
    /// ordinal from 11 up is read one place further on. Read as written, a v6 player on IntroduceHealth
    /// would resume waiting for a cast that already landed, and a finished v6 opening would reopen on
    /// its last card.
    /// </para>
    /// <para>
    /// <see cref="OpeningStage.AwaitTraining"/> is the SECOND such insertion (version 9), and it lands
    /// five stages at once. The two shifts COMPOSE, oldest first: a v6 ordinal is lifted into v7/v8
    /// numbering, and that result is lifted into v9 numbering. Applying them in the other order — or
    /// comparing a raw v6 ordinal against this file's <c>AwaitTraining</c> — reads a v6 cursor against a
    /// boundary that did not exist in its own numbering and moves it to the wrong beat. The worked
    /// cases: a v6 AwaitBoss (13) becomes 14, then 19, which is this build's AwaitBoss; a v6
    /// IntroduceHealth (11) becomes 12 and stops, because 12 is below the Training boundary; a v8
    /// IntroduceStage (13) is untouched by both.
    /// </para>
    /// <para>
    /// A cursor that lands INSIDE the new chapter cannot exist — no file was ever written there — so
    /// nothing has to be mapped onto the Training beats. A resumed player either has not reached them
    /// (and will play them) or is already past them (and will not).
    /// </para>
    /// </remarks>
    public static OpeningStage StageOf(int saved, int fileVersion)
    {
        var ordinal = saved;
        if (fileVersion < FirstVersionWithSignatureWatch && ordinal >= (int)OpeningStage.WatchSignature)
            ordinal += 1;
        if (fileVersion < FirstVersionWithTrainingChapter && ordinal >= (int)OpeningStage.AwaitTraining)
            ordinal += TrainingChapterLength;
        return StageOf(ordinal);
    }

    /// <summary>How many stages the Training chapter inserted — the v8 → v9 shift.</summary>
    /// <remarks>
    /// Derived from the enum rather than written down, so adding a beat to the chapter cannot leave the
    /// migration reading one place short: AwaitBoss is the first stage that existed on both sides of
    /// the insertion, and the gap in front of it IS the chapter.
    /// </remarks>
    private const int TrainingChapterLength = (int)OpeningStage.AwaitBoss - (int)OpeningStage.AwaitTraining;

    /// <summary>
    /// The stage a LOADED cursor resumes on: the saved one, unless that beat's moment cannot exist
    /// after a load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some beats are about something the fight or a screen is showing RIGHT NOW — the first creature
    /// standing, the replay held before a cast, the cast playing out, a boss on the stage, a chest's
    /// reveal — and a relaunch has none of them: the descent starts again and the reveal has closed.
    /// Resumed ON such a beat, its card pointed at nothing; on the Signature it soft-locked the fight,
    /// because the release that beat waits for can only be made in the session that made the hold
    /// (adversarial review, 2026-09-11).
    /// </para>
    /// <para>
    /// Each maps back to the wait that sets its moment up again — so the replay is held before the
    /// next cast, the card is shown before it, and CONTINUE releases it in this session — or, for the
    /// item's reveal, forward to the step that takes the player to the item.
    /// </para>
    /// </remarks>
    public static OpeningStage ResumeStage(OpeningStage saved) => saved switch
    {
        OpeningStage.IntroduceEnemy => OpeningStage.AwaitFirstEnemy,
        OpeningStage.IntroduceSignature or OpeningStage.WatchSignature => OpeningStage.AwaitSignature,
        OpeningStage.IntroduceBoss => OpeningStage.AwaitBoss,
        OpeningStage.IntroduceItem => OpeningStage.ForceGear,
        _ => saved,
    };

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

    /// <summary>
    /// THE FIRST SAVE VERSION WHOSE CURSOR COUNTS <see cref="OpeningStage.WatchSignature"/>. Frozen.
    /// </summary>
    /// <remarks>A literal for the reason <see cref="FirstVersionWithOpeningState"/> is one.</remarks>
    public const int FirstVersionWithSignatureWatch = 7;

    /// <summary>
    /// THE FIRST SAVE VERSION WHOSE CURSOR COUNTS THE TRAINING CHAPTER. Frozen.
    /// </summary>
    /// <remarks>
    /// A literal for the reason the two above are, and the one this family has been warned about three
    /// times: written as <c>SaveGame.CurrentVersion</c>, the next unrelated version bump would start
    /// lifting cursors that are already in this numbering, and every player standing in the Training
    /// chapter would be moved five beats into the boss.
    /// </remarks>
    public const int FirstVersionWithTrainingChapter = 9;
}
