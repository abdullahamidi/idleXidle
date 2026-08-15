namespace ResonanceHunter.Core.Progression;

/// <summary>One rung of the first-run guide, in the order a player meets them.</summary>
/// <remarks>
/// The order is the LOOP's order, not the menu's: watch a fight, spend what it paid, wear what it
/// dropped, meet a boss, then open the build. Teaching the build first — which a menu-shaped tutorial
/// would — asks someone to choose four skills before they have seen a skill fire.
/// </remarks>
public enum TutorialStep
{
    /// <summary>The champion fights on its own. That is the single least obvious thing about this game.</summary>
    Watch,

    /// <summary>Waves pay Gleam, and Gleam is spent on the champion's own stats.</summary>
    SpendGleam,

    /// <summary>Something dropped. Gear is worn, not collected.</summary>
    EquipItem,

    /// <summary>Every fifth wave is a boss, and a boss is where chests come from.</summary>
    MeetABoss,

    /// <summary>The build is the game. Four skills, and what they are made of.</summary>
    WeaveBuild,

    /// <summary>Depth conquers a region, which opens the next one.</summary>
    Conquer,

    /// <summary>Nothing left to say.</summary>
    Done,
}

/// <summary>What the game knows about a player, in the terms the guide cares about.</summary>
public readonly record struct TutorialFacts(
    int WavesCleared,
    int Gleam,
    int StatsTrained,
    int ItemsOwned,
    int ItemsWorn,
    int BossesFelled,
    int SkillsWoven,
    int DeepestWave,
    int RegionsConquered);

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
/// Every step is gated on a fact the player has already produced, never on a timer. A prompt that
/// arrives before the player can act on it is noise, and a prompt that arrives after they worked it out
/// is worse — it tells them the game was not watching. So "spend your Gleam" waits until there is Gleam
/// to spend, and "equip that" waits until something has actually dropped.
/// </para>
/// <para>
/// It is also SKIPPABLE by playing: each step completes when its lesson is performed, whether or not it
/// was read. A returning player who already knows the loop clears the whole guide in the first minute
/// without being stopped once.
/// </para>
/// </remarks>
public static class Tutorial
{
    /// <summary>The step a player is on, given everything they have done.</summary>
    /// <remarks>
    /// Derived rather than stored, and that is the important property: it cannot desync from the save,
    /// it needs no migration, and a player who somehow skips ahead is never shown a lesson they have
    /// already outgrown. The only thing persisted is <see cref="TutorialFacts"/>'s inputs, all of which
    /// the save already carried for other reasons.
    /// </remarks>
    public static TutorialStep StepFor(TutorialFacts f)
    {
        // THE FIRST UNSATISFIED STEP, IN ORDER — never "the first one whose conditions happen to hold".
        //
        // The first version folded "is it done" and "can it be acted on" into one condition per step,
        // and that made the guide travel BACKWARDS: a player one wave in with no Gleam yet failed the
        // SpendGleam condition, fell through to MeetABoss, and then jumped back to SpendGleam the moment
        // a wave paid out. A guide that reverses is worse than none — it re-teaches something already
        // done, which reads as the game losing track of you.
        //
        // Splitting the two fixes it by construction. Satisfaction only ever moves one way (every fact
        // below is a lifetime count), so the step index can only ever rise. Whether the lesson is worth
        // SHOWING yet is a separate question, answered by IsReady.
        if (f.WavesCleared < 1) return TutorialStep.Watch;
        if (f.StatsTrained < 1) return TutorialStep.SpendGleam;
        if (f.ItemsWorn < 1) return TutorialStep.EquipItem;
        if (f.BossesFelled < 1) return TutorialStep.MeetABoss;
        if (f.SkillsWoven < 1) return TutorialStep.WeaveBuild;
        if (f.RegionsConquered < 1) return TutorialStep.Conquer;
        return TutorialStep.Done;
    }

    /// <summary>
    /// Can the player act on this lesson yet?
    /// </summary>
    /// <remarks>
    /// "Spend your Gleam" with no Gleam is noise, and noise teaches a player to stop reading the
    /// prompts — after which the one that matters is invisible too. So a step that is next in line but
    /// not yet actionable shows NOTHING and waits, rather than skipping to a later lesson out of order.
    /// Silence is a better neighbour to a guide than a prompt that cannot be obeyed.
    /// </remarks>
    public static bool IsReady(TutorialStep step, TutorialFacts f) => step switch
    {
        TutorialStep.SpendGleam => f.Gleam >= 20,
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
        TutorialStep.EquipItem => "SOMETHING DROPPED",
        TutorialStep.MeetABoss => "EVERY FIFTH WAVE IS A BOSS",
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

        TutorialStep.EquipItem =>
            "Press C for GEAR and equip it. An item in the bag does nothing; the fight only reads "
            + "what is worn.",

        TutorialStep.MeetABoss =>
            "Bosses hit far harder and sometimes drop a chest. Chests are opened in the FORGE (F).",

        TutorialStep.WeaveBuild =>
            "Press B for BUILD. Four skills, each a SOURCE and a FORM — hover either one to read "
            + "exactly what it does before you commit.",

        TutorialStep.Conquer =>
            "Reach the region's depth objective to conquer it and open the next. Deeper waves drop "
            + "rarer loot, never more of it.",

        _ => "",
    };

    /// <summary>Is there anything to show at all?</summary>
    public static bool HasGuidance(TutorialStep step) => step != TutorialStep.Done;
}
