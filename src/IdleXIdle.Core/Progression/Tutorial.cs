using System.Collections.Generic;
using IdleXIdle.Core.Encounters;

namespace IdleXIdle.Core.Progression;

/// <summary>One rung of the first-run guide, in the order a player meets them.</summary>
/// <remarks>
/// The order is the LOOP's order, not the menu's: watch a fight, spend what it paid, meet a boss, crack
/// what it dropped, wear it, then open the build. Teaching the build first — which a menu-shaped
/// tutorial would — asks someone to choose skills before they have seen a skill fire.
/// </remarks>
public enum TutorialStep
{
    /// <summary>The champion fights on its own. That is the single least obvious thing about this game.</summary>
    Watch,

    /// <summary>Waves pay Gleam, and Gleam is spent on the champion's own stats.</summary>
    SpendGleam,

    /// <summary>Every fifth wave is a boss, and a boss is the only thing that drops a chest.</summary>
    MeetABoss,

    /// <summary>Depth conquers a region, which opens the next one.</summary>
    /// <remarks>
    /// BETWEEN the boss and the chest, because that is where the career puts it: conquest needs
    /// depth 7, the first boss is wave 5, and the first chest is a 20% roll per boss that usually
    /// lands far later. Sat at the end of the enum, this rung was already satisfied by the time it
    /// became current on every real playthrough — a lesson positioned where nobody could meet it.
    /// </remarks>
    Conquer,

    /// <summary>A chest is where items come from. The VAULT reads one; the Forge cracks it.</summary>
    OpenChest,

    /// <summary>Something dropped. Gear is worn, not collected.</summary>
    EquipItem,

    /// <summary>The build is the game. Skills, and what they are made of.</summary>
    ChooseBuild,

    /// <summary>Nothing left to say.</summary>
    Done,
}

/// <summary>What the game knows about a player, in the terms the guide cares about.</summary>
/// <remarks>
/// Defaults to a brand-new save so a caller naming only the facts it cares about does not have to
/// invent values it does not mean.
/// </remarks>
public readonly record struct TutorialFacts(
    int WavesCleared = 0,
    int Gleam = 0,
    int StatsTrained = 0,
    int ItemsOwned = 0,
    int ItemsWorn = 0,
    int ChestsHeld = 0,
    int SkillsWoven = 0,
    int DeepestWave = 0,
    int RegionsConquered = 0,
    /// <summary>
    /// How many things the BUILD screen has to choose between beyond the signature: taught skills,
    /// keystones, vows. Zero means the screen is not open yet (Unlocks gates it on the same facts),
    /// and the CHOOSE BUILD rung waits rather than name a door the game has not opened.
    /// </summary>
    int BuildChoices = 0);

/// <summary>
/// The first-run guide: one sentence at a time, and only once the player can act on it.
/// </summary>
/// <remarks>
/// <para>
/// There was no onboarding at all. The only explanation in the game was a help screen behind F1, which
/// a player has to already suspect exists — and the thing it most needs to say is the thing that is
/// least visible: <b>the champion fights without you.</b> Someone who does not know that sits waiting
/// for a control that never comes.
/// </para>
/// <para>
/// <b>THE FIRST VERSION OF THIS FILE DEADLOCKED, and the way it did is worth keeping.</b> Playtest:
/// "Tutorial bozuk, düzgün ilerlemiyor." Three faults compounded:
/// </para>
/// <para>
/// (1) EquipItem sat third and waited for the player to OWN an item. Items come from one place — a
/// chest, dropped by a boss on a flat 20% roll, then opened by hand in the Forge — so the guide went
/// silent for roughly twenty-five waves. (2) MeetABoss, the ONLY step that named the Forge or the word
/// "chest", sat BEHIND it: the instruction for escaping the room was locked inside the room. (3) Worse,
/// MeetABoss could never display at all, because owning an item implies a chest implies a felled boss —
/// so its condition was always already satisfied by the time it became current, and it was skipped on
/// every save that has ever existed.
/// </para>
/// <para>
/// The fix is structural, not a threshold tweak: <b>every step must be reachable from the state that
/// precedes it</b>, which means a step may not depend on a resource that a LATER step explains how to
/// get. MeetABoss now precedes the chest, OpenChest exists as its own rung and speaks during the wait,
/// and <see cref="StepFor"/> is ordered so that satisfying rung N is exactly what makes rung N+1
/// current. <c>test_every_step_is_displayed_on_a_real_career</c> walks a simulated career and fails if
/// any rung is skipped.
/// </para>
/// <para>
/// Every step is gated on a fact the player has already produced, never on a timer. A prompt that
/// arrives before the player can act on it is noise, and a prompt that arrives after they worked it out
/// is worse — it tells them the game was not watching.
/// </para>
/// <para>
/// It is also SKIPPABLE by playing: each step completes when its lesson is performed, whether or not it
/// was read. A returning player who already knows the loop clears the whole guide without being
/// stopped once.
/// </para>
/// </remarks>
public static class Tutorial
{
    /// <summary>Waves between bosses. Mirrors <c>ExpeditionTuning.BossEvery</c>.</summary>
    /// <remarks>
    /// Duplicated rather than referenced because Progression must not depend on Expeditions, and pinned
    /// by <c>test_the_boss_cadence_matches_the_expedition_tuning</c> so the copy cannot drift.
    /// </remarks>
    public const int BossEvery = 5;

    /// <summary>What the first rank of any stat costs. Mirrors <c>ProgressionTuning.BaseTrainingCost</c>.</summary>
    /// <remarks>
    /// The SpendGleam prompt used to fire at 20 Gleam while the cheapest rank cost 25, so for five Gleam
    /// the guide said "press V and train" and every row refused. That is precisely the prompt-that-
    /// cannot-be-obeyed this file forbids, and it was caused by a threshold guessed instead of read.
    /// Pinned by a test against the real tuning.
    /// </remarks>
    public const int FirstRankCost = 25;

    /// <summary>The rungs a player can be shown, in the ladder's (the career's) order.</summary>
    private static readonly TutorialStep[] Ladder =
    {
        // Conquer sat LAST since conquest moved to wave 20 (2026-08-26). CHOOSE BUILD moved behind it
        // on 2026-09-06: the BUILD screen opens on a real choice now — a second skill, a keystone or a
        // vow — and in the shipping game the first of those is the keystone the first conquest
        // teaches. A build lesson seated before the conquest would wait, silent, and hide the
        // conquest lesson behind it.
        TutorialStep.Watch, TutorialStep.SpendGleam, TutorialStep.MeetABoss,
        TutorialStep.OpenChest, TutorialStep.EquipItem, TutorialStep.Conquer, TutorialStep.ChooseBuild,
    };

    /// <summary>Has this rung's lesson already been performed, whether or not it was read?</summary>
    /// <remarks>
    /// The order of <see cref="Ladder"/> is load-bearing, and it is the CAREER's order. Each rung's
    /// satisfaction is exactly the thing that makes the next rung current, so no rung can wait on a
    /// resource a later rung explains how to obtain — and no rung can be already satisfied before it
    /// has ever been shown. Conquer sits between the boss and the chest because that is where the
    /// tuning puts it.
    /// </remarks>
    private static bool Satisfied(TutorialStep step, TutorialFacts f) => step switch
    {
        TutorialStep.Watch => f.WavesCleared >= 1,
        TutorialStep.SpendGleam => f.StatsTrained >= 1,
        TutorialStep.MeetABoss => f.DeepestWave >= BossEvery,
        TutorialStep.Conquer => f.RegionsConquered >= 1,
        TutorialStep.OpenChest => f.ItemsOwned >= 1,
        TutorialStep.EquipItem => f.ItemsWorn >= 1,
        TutorialStep.ChooseBuild => f.SkillsWoven >= 1,
        _ => true,
    };

    /// <summary>The step a player is on, given everything they have done.</summary>
    /// <remarks>
    /// Derived rather than stored: it cannot desync from the save, it needs no migration, and a player
    /// who skips ahead is never shown a lesson they have already outgrown.
    /// </remarks>
    public static TutorialStep StepFor(TutorialFacts f) => StepFor(f, null);

    /// <summary>
    /// The step a player is on, skipping any rung they closed by hand.
    /// </summary>
    /// <param name="f">What the player has done so far.</param>
    /// <param name="dismissedRungs">
    /// Rung NAMES (<c>TutorialStep.ToString()</c>) the player dismissed with a lesson's ×
    /// button, or null for none. A dismissed rung is skipped exactly as if it were completed — but only
    /// for DISPLAY: nothing here fakes the underlying facts, so the gates and unlocks that read those
    /// facts are untouched. A dismissed rung never returns.
    /// </param>
    public static TutorialStep StepFor(TutorialFacts f, IReadOnlyCollection<string>? dismissedRungs)
    {
        // DONE MEANS THE LOOP WAS LIVED, NOT MERELY THAT A REGION FELL. This guard used to read
        // conquest alone — and conquest arrives at depth 7, roughly half a minute in, so the whole
        // guide ended before MeetABoss, OpenChest, EquipItem or WeaveBuild had ever been shown to a
        // real player. Done now asks for the facts that prove the loop was actually touched: the
        // conquest (monotone), something worn, and a build that is no longer the one the game handed
        // over.
        //
        // Two of those CAN decrease — stripping every slot bare, or reverting the weave to the exact
        // starter — and then the matching lesson returns. Accepted deliberately: a player wearing
        // nothing at all is truly in the state the lesson describes, and the alternative (the
        // conquest-only guard) silently deleted four lessons from every playthrough that ever
        // happened.
        if (f.RegionsConquered >= 1 && f.ItemsWorn >= 1 && f.SkillsWoven >= 1) return TutorialStep.Done;

        // THE FIRST UNSATISFIED, UNDISMISSED STEP, IN ORDER — never "the first one whose conditions
        // happen to hold". See the remarks on Satisfied for why the order matters.
        foreach (var step in Ladder)
        {
            if (Satisfied(step, f)) continue;
            if (dismissedRungs is not null && dismissedRungs.Contains(step.ToString())) continue;
            return step;
        }

        // Every rung satisfied or dismissed: nothing left to say.
        return TutorialStep.Done;
    }

    /// <summary>
    /// Can the player act on this lesson yet?
    /// </summary>
    /// <remarks>
    /// "Spend your Gleam" with no Gleam is noise, and noise teaches a player to stop reading the
    /// prompts — after which the one that matters is invisible too. So a step that is next in line but
    /// not yet actionable shows NOTHING and waits, rather than skipping to a later lesson out of order.
    ///
    /// The exception is OpenChest, which is deliberately always ready: the wait for a 20% drop is long
    /// enough that silence would read as the guide having died, which is the exact complaint this file
    /// was rewritten for. Its body is written to be true and useful while you are still waiting.
    /// </remarks>
    public static bool IsReady(TutorialStep step, TutorialFacts f) => step switch
    {
        // AND THE SCREEN IT SENDS TO IS OPEN: TRAINING opens after several waves (Unlocks), and a
        // lesson naming a door the game has not opened is worse than silence.
        TutorialStep.SpendGleam => f.Gleam >= FirstRankCost && f.WavesCleared >= Unlocks.TrainingOpensAtWaves,
        TutorialStep.EquipItem => f.ItemsOwned >= 1,
        TutorialStep.ChooseBuild => f.BuildChoices >= 1,
        TutorialStep.Done => false,
        _ => true,
    };

    /// <summary>The step to actually display, or null while the next lesson is not yet actionable.</summary>
    public static TutorialStep? Showing(TutorialFacts f) => Showing(f, null);

    /// <summary>
    /// The step to display given the rungs the player closed by hand, or null for silence.
    /// </summary>
    /// <remarks>
    /// A dismissed rung is skipped for display exactly as if it were completed. If the rung after it
    /// is not yet actionable the guide waits silently — the same rule the ladder always had — rather
    /// than skipping further ahead to a lesson out of order.
    /// </remarks>
    public static TutorialStep? Showing(TutorialFacts f, IReadOnlyCollection<string>? dismissedRungs)
    {
        var step = StepFor(f, dismissedRungs);
        return HasGuidance(step) && IsReady(step, f) ? step : null;
    }

    /// <summary>The line shown for a step. One sentence, and it names the key that acts on it.</summary>
    /// <summary>
    /// A saved dismissed-rung name, read forward across renames: WEAVEBUILD became CHOOSEBUILD when the
    /// weave vocabulary retired (2026-09-01, UX V2). Without this a player who had closed that lesson
    /// would see it again.
    /// </summary>
    public static string ModernRungName(string saved) => saved switch
    {
        "WeaveBuild" => nameof(TutorialStep.ChooseBuild),
        _ => saved,
    };

    public static string Title(TutorialStep step) => step switch
    {
        TutorialStep.Watch => "YOUR HUNTER FIGHTS ON ITS OWN",
        TutorialStep.SpendGleam => "YOU HAVE GLEAM TO SPEND",
        TutorialStep.MeetABoss => "EVERY FIFTH WAVE IS A BOSS",
        TutorialStep.OpenChest => "CHESTS ARE WHERE ITEMS COME FROM",
        TutorialStep.EquipItem => "SOMETHING DROPPED",
        TutorialStep.ChooseBuild => "THE BUILD IS THE GAME",
        TutorialStep.Conquer => $"{Checkpoints.ConquestWave} WAVES CONQUER A REGION",
        _ => "",
    };

    /// <summary>What to do about it. Never more than a line and a half.</summary>
    public static string Body(TutorialStep step) => step switch
    {
        TutorialStep.Watch =>
            "It clears waves without you, on every screen, even while the game is closed. "
            + "You are here to decide WHAT it is, not to swing for it.",

        TutorialStep.SpendGleam =>
            "Every wave pays Gleam. Press V for TRAINING and spend it — that is the hunter's own "
            + "power, and it never resets.",

        TutorialStep.MeetABoss =>
            "Bosses hit far harder, and a boss is the only thing in the game that drops a chest. "
            + "Survive to wave " + BossEvery + " to meet one.",

        TutorialStep.OpenChest =>
            "About one boss in five drops one, so this takes a few tries. When one does, press K for "
            + "the VAULT — it names the chest's guaranteed rarity, its tier and what its region favours "
            + "before you decide to crack it.",

        TutorialStep.EquipItem =>
            "Press C for GEAR and equip it. An item in the bag does nothing; the fight only reads "
            + "what is worn.",

        TutorialStep.ChooseBuild =>
            "Press B for BUILD. Pick a slot, then a skill from the library — a shared skill is unlocked "
            + "by its road on the MASTERY tree (E), and stays unlocked while you keep that road.",

        TutorialStep.Conquer =>
            $"Reach CONQUEST {Checkpoints.ConquestWave} / {Checkpoints.ConquestWave} in the banner over the arena to conquer the region and open the next. "
            + "Deeper waves drop rarer loot, never more of it.",

        _ => "",
    };

    /// <summary>
    /// The body, given what the player has — the one rung whose truth depends on the moment.
    /// </summary>
    /// <remarks>
    /// OpenChest speaks during a wait and after it, and since a new game is seeded with a welcome
    /// gift (<c>GiftChests</c>) the wait is usually already over: a chest is in the vault from the
    /// first minute. Telling that player "this takes a few tries" while their VAULT tile wears a NEW
    /// mark and a chest count is the guide contradicting the rail. With a chest held, the rung says
    /// so and points at it; with none, it explains the wait exactly as before.
    /// </remarks>
    public static string Body(TutorialStep step, TutorialFacts f) => step switch
    {
        TutorialStep.OpenChest when f.ChestsHeld >= 1 =>
            "A chest is waiting in the VAULT — press K and click it to open it. Bosses drop more, "
            + "about one boss in five, and the vault reads each one before you decide to crack it.",
        _ => Body(step),
    };

    /// <summary>
    /// The screen a step sends the player to, or null if it asks nothing of the rail.
    /// </summary>
    /// <remarks>
    /// <b>Exists so a test can check the guide never names a locked door.</b> Every body from SpendGleam
    /// onward tells the player to press a key, and those keys are gated by <see cref="Unlocks"/> — two
    /// systems written days apart, each correct on its own. A step that says "press F for the FORGE"
    /// while the Forge is still a dimmed tile teaches the player that the guide does not know what is
    /// going on, which is worse than saying nothing.
    ///
    /// Kept here rather than in the test so the mapping is part of the design and moves with the copy.
    /// </remarks>
    public static Activity? Sends(TutorialStep step, TutorialFacts f) => step switch
    {
        TutorialStep.SpendGleam => Activity.Training,

        // OpenChest is the one step that speaks DURING A WAIT — a chest is a 20% roll and the guide
        // would otherwise go silent for tens of waves. Until one actually drops its body is in the
        // future tense ("when one does, press K"), and the dimmed VAULT tile says the same thing in the
        // same breath, so the two agree rather than contradict. It only SENDS you anywhere once there
        // is something to go and read.
        TutorialStep.OpenChest => f.ChestsHeld >= 1 ? Activity.Vault : null,

        TutorialStep.EquipItem => Activity.Gear,
        TutorialStep.ChooseBuild => Activity.Build,
        _ => null,   // Watch, MeetABoss and Conquer are about the fight itself
    };

    /// <summary>Is there anything to show at all?</summary>
    public static bool HasGuidance(TutorialStep step) => step != TutorialStep.Done;
}
