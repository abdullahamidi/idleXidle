namespace ResonanceHunter.Core.Progression;

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

    /// <summary>A chest is where items come from, and it is opened in the Forge.</summary>
    OpenChest,

    /// <summary>Something dropped. Gear is worn, not collected.</summary>
    EquipItem,

    /// <summary>The build is the game. Skills, and what they are made of.</summary>
    WeaveBuild,

    /// <summary>Depth conquers a region, which opens the next one.</summary>
    Conquer,

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
    int RegionsConquered = 0);

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

    /// <summary>The step a player is on, given everything they have done.</summary>
    /// <remarks>
    /// Derived rather than stored: it cannot desync from the save, it needs no migration, and a player
    /// who skips ahead is never shown a lesson they have already outgrown.
    /// </remarks>
    public static TutorialStep StepFor(TutorialFacts f)
    {
        // A CONQUEROR IS DONE, whatever else is true. This guard is what keeps the guide from
        // resurrecting: two of the facts below can genuinely decrease (unequipping an item, respeccing
        // the tree), and without this a finished player who unequipped a sword would be dragged back to
        // "SOMETHING DROPPED". Conquest is monotone and means every lesson here has been lived.
        if (f.RegionsConquered >= 1) return TutorialStep.Done;

        // THE FIRST UNSATISFIED STEP, IN ORDER — never "the first one whose conditions happen to hold".
        //
        // The order is load-bearing. Each rung's satisfaction is exactly the thing that makes the next
        // rung current, so no rung can wait on a resource a later rung explains how to obtain.
        if (f.WavesCleared < 1) return TutorialStep.Watch;
        if (f.StatsTrained < 1) return TutorialStep.SpendGleam;
        if (f.DeepestWave < BossEvery) return TutorialStep.MeetABoss;
        if (f.ItemsOwned < 1) return TutorialStep.OpenChest;
        if (f.ItemsWorn < 1) return TutorialStep.EquipItem;
        if (f.SkillsWoven < 1) return TutorialStep.WeaveBuild;
        return TutorialStep.Conquer;
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
        TutorialStep.SpendGleam => f.Gleam >= FirstRankCost,
        TutorialStep.EquipItem => f.ItemsOwned >= 1,
        TutorialStep.Done => false,
        _ => true,
    };

    /// <summary>The step to actually display, or null while the next lesson is not yet actionable.</summary>
    public static TutorialStep? Showing(TutorialFacts f)
    {
        var step = StepFor(f);
        return HasGuidance(step) && IsReady(step, f) ? step : null;
    }

    /// <summary>The line shown for a step. One sentence, and it names the key that acts on it.</summary>
    public static string Title(TutorialStep step) => step switch
    {
        TutorialStep.Watch => "YOUR CHAMPION FIGHTS ON ITS OWN",
        TutorialStep.SpendGleam => "YOU HAVE GLEAM TO SPEND",
        TutorialStep.MeetABoss => "EVERY FIFTH WAVE IS A BOSS",
        TutorialStep.OpenChest => "CHESTS ARE WHERE ITEMS COME FROM",
        TutorialStep.EquipItem => "SOMETHING DROPPED",
        TutorialStep.WeaveBuild => "THE BUILD IS THE GAME",
        TutorialStep.Conquer => "DEPTH CONQUERS A REGION",
        _ => "",
    };

    /// <summary>What to do about it. Never more than a line and a half.</summary>
    public static string Body(TutorialStep step) => step switch
    {
        TutorialStep.Watch =>
            "It clears waves without you, on every screen, even while the game is closed. "
            + "You are here to decide WHAT it is, not to swing for it.",

        TutorialStep.SpendGleam =>
            "Every wave pays Gleam. Press V for STATS and train — that is the champion's own power, "
            + "and it never resets.",

        TutorialStep.MeetABoss =>
            "Bosses hit far harder, and a boss is the only thing in the game that drops a chest. "
            + "Survive to wave " + BossEvery + " to meet one.",

        TutorialStep.OpenChest =>
            "About one boss in five drops one, so this takes a few tries. When one does, press F for "
            + "the FORGE and open it — a chest names its guaranteed rarity before you do.",

        TutorialStep.EquipItem =>
            "Press C for GEAR and equip it. An item in the bag does nothing; the fight only reads "
            + "what is worn.",

        TutorialStep.WeaveBuild =>
            "Press B for BUILD, then WEAVE. Every skill is a SOURCE and a FORM — hover either one to "
            + "read exactly what it does before you commit.",

        TutorialStep.Conquer =>
            "Reach the region's depth objective to conquer it and open the next. Deeper waves drop "
            + "rarer loot, never more of it.",

        _ => "",
    };

    /// <summary>Is there anything to show at all?</summary>
    public static bool HasGuidance(TutorialStep step) => step != TutorialStep.Done;
}
