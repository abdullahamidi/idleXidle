using System.Collections.Generic;

namespace ResonanceHunter.Core.Progression;

/// <summary>Which part of the HUNT screen an intro step points at. The host maps each one to a rectangle.</summary>
/// <remarks>
/// Named regions rather than rectangles, because the rectangles are the fight screen's business and move
/// whenever its layout does. The copy here only needs to know WHAT it is talking about.
/// </remarks>
public enum IntroTarget
{
    /// <summary>The champion, mid-arena.</summary>
    Champion,

    /// <summary>The enemy pack, plus the stage banner that counts waves and conquest.</summary>
    Enemies,

    /// <summary>The hunter panel: portrait, level, life bar.</summary>
    HunterHud,

    /// <summary>The currency capsules, top-right.</summary>
    CurrencyPills,

    /// <summary>The SKILLS rail, left of the arena.</summary>
    Skills,

    /// <summary>The right column: idle rate, reward errands, the chest filter.</summary>
    RightColumn,

    /// <summary>The navigation rail down the left edge.</summary>
    NavRail,

    /// <summary>Where the guide strip will appear, bottom centre.</summary>
    GuideStrip,
}

/// <summary>One card of the intro: what it points at, its title, and its two short sentences.</summary>
public readonly record struct IntroStep(IntroTarget Target, string Title, string Body);

/// <summary>What a screen says the first time it is opened: the key that remembers it, and the words.</summary>
/// <param name="Key">Written into the save's explained list when the player closes the banner.</param>
public readonly record struct ScreenBanner(string Key, string Title, string Body);

/// <summary>
/// The first minute of the game: a click-through intro on the HUNT screen, and one quiet banner per
/// screen the first time it is opened.
/// </summary>
/// <remarks>
/// <para>
/// <b>The game used to interrupt.</b> A new save opened onto a fight and, eight seconds later, a modal
/// panel; every screen that opened, every quest that finished and every champion that joined put another
/// modal over whatever the player was doing. Playtest, in two parts: the tutorial "throws the player
/// straight into the game", and the notifications "wore the testers out" while they were trying to
/// solve something.
/// </para>
/// <para>
/// So there are two channels now, and neither one interrupts. The INTRO runs once, on the first BEGIN
/// THE HUNT, and walks the screen one region at a time while the fight keeps running behind it — the
/// player advances it by clicking, and can skip it. Everything else is ON DEMAND: a screen that opens
/// gets a small NEW mark on its rail tile, and its explanation waits on that screen until the player
/// goes there. Nothing is announced over the top of a screen the player did not ask for.
/// </para>
/// <para>
/// Derived where it can be. Whether a tile is new is a question asked of the unlock gates and the
/// explained list, never a flag set at the moment of opening — so a screen that opened while the game
/// was closed is still marked, and no call site can forget to mark it.
/// </para>
/// </remarks>
public static class Onboarding
{
    /// <summary>The intro, in the order it is shown. Eight cards, each pointing at one region.</summary>
    /// <remarks>
    /// The order is the order of looking, not of importance: the champion first (it is the thing
    /// moving), then what it fights, then the panels around it clockwise, and finally the two things
    /// that are ABOUT what comes next — the rail and the guide strip. Plain words throughout: no
    /// abbreviations, no genre vocabulary, short sentences.
    /// </remarks>
    public static readonly IReadOnlyList<IntroStep> Intro = new[]
    {
        new IntroStep(IntroTarget.Champion, "YOUR CHAMPION",
            "This is your champion. It fights on its own. You never press attack."),

        new IntroStep(IntroTarget.Enemies, "THE ENEMIES",
            "Enemies come in waves. Every fifth wave is a boss. "
            + "The banner at the top counts the waves and the conquest."),

        new IntroStep(IntroTarget.HunterHud, "YOUR CHAMPION'S LIFE",
            "This is your champion's life. When it reaches zero the descent ends — "
            + "then it gets back up and starts again. Nothing is lost."),

        new IntroStep(IntroTarget.CurrencyPills, "GLEAM",
            "Every cleared wave pays Gleam. Spend Gleam on the STATS screen to train your champion."),

        new IntroStep(IntroTarget.Skills, "YOUR SKILLS",
            "Your skills. They fire on their own timers. You choose them on the BUILD screen later."),

        new IntroStep(IntroTarget.RightColumn, "REWARDS AND ERRANDS",
            "Rewards and errands. When a boss drops a chest, or you earn mastery points, "
            + "the buttons here take you there."),

        new IntroStep(IntroTarget.NavRail, "THE OTHER SCREENS",
            "The other screens. Most are closed for now. They open as you play — "
            + "a gold NEW mark shows what just opened."),

        new IntroStep(IntroTarget.GuideStrip, "LESSONS",
            "When there is something new to do, a short lesson appears down here. Close it with the ×. "
            + "That is all — go and watch the first wave."),
    };

    /// <summary>
    /// Should the intro run when this player leaves the title screen?
    /// </summary>
    /// <param name="f">What the player has done so far.</param>
    /// <param name="introSeen">The save's own record of having finished or skipped it.</param>
    /// <remarks>
    /// Two guards, because the save flag did not exist until the intro did. A player whose save was
    /// written before this file has <paramref name="introSeen"/> false and hundreds of cleared waves;
    /// teaching them where the champion is would be the interruption this file exists to remove. So a
    /// cleared wave counts as having seen it. Asked ONCE, at the moment the title closes — the intro
    /// must not vanish because the first wave happened to clear while the player was reading card three.
    /// </remarks>
    public static bool IntroDue(TutorialFacts f, bool introSeen) => !introSeen && f.WavesCleared < 1;

    /// <summary>The explained-list key for a screen.</summary>
    public static string ScreenKey(Activity screen) => screen.ToString();

    /// <summary>The explained-list key for a skill slot's note (slot 2, 3, 4).</summary>
    /// <remarks>
    /// The slot notes live in the same list as the screens, under their own prefix, so they need no
    /// field of their own and follow the same rule: shown once, remembered in the save.
    /// </remarks>
    public static string SlotKey(int slot) => $"SkillSlot{slot}";

    /// <summary>
    /// The explained list a player should start the session with.
    /// </summary>
    /// <param name="f">The unlock facts at load.</param>
    /// <param name="introSeen">The save's intro flag.</param>
    /// <param name="explained">The save's explained list.</param>
    /// <remarks>
    /// A save from before this file existed has an empty list, and honouring that literally would put
    /// a NEW mark on every open tile and a banner on every screen a returning player has used for
    /// hours. Such a save is recognisable — the intro is neither seen nor due — and for it every screen
    /// open at load and every skill slot already earned counts as explained. A player who has seen the
    /// intro gets the list exactly as saved: whatever they have not yet read is still waiting for them.
    /// </remarks>
    public static IReadOnlyCollection<string> SeedExplained(
        UnlockFacts f, bool introSeen, IEnumerable<string> explained)
    {
        var set = new HashSet<string>(explained);
        var returningFromBefore = !introSeen
            && !IntroDue(new TutorialFacts(WavesCleared: f.WavesCleared), false);
        if (!returningFromBefore) return set;

        foreach (var screen in Unlocks.Open(f)) set.Add(ScreenKey(screen));
        for (var slot = 2; slot <= Unlocks.SkillSlots(f); slot++) set.Add(SlotKey(slot));
        return set;
    }

    /// <summary>
    /// What this screen should say at the top the first time it is opened, or null if nothing is owed.
    /// </summary>
    /// <remarks>
    /// One banner at a time. The BUILD screen can owe two things at once — its own explanation and the
    /// note for a slot that just opened — and they are handed over in that order: what the screen is,
    /// then what changed on it. The Hunt owes nothing here; the intro is its explanation.
    /// </remarks>
    public static ScreenBanner? BannerFor(Activity screen, UnlockFacts f, IReadOnlyCollection<string> explained)
    {
        if (screen == Activity.Hunt) return null;
        if (!explained.Contains(ScreenKey(screen)))
            return new ScreenBanner(ScreenKey(screen), Unlocks.Headline(screen), Unlocks.Explain(screen));

        if (screen == Activity.Build)
            for (var slot = 2; slot <= Unlocks.SkillSlots(f); slot++)
                if (!explained.Contains(SlotKey(slot)))
                    return new ScreenBanner(SlotKey(slot), "A NEW SKILL SLOT", Unlocks.SkillSlotNote(slot));

        return null;
    }

    /// <summary>Does this rail tile deserve a gold NEW mark — open, with something unread waiting on it?</summary>
    public static bool IsNew(Activity screen, UnlockFacts f, IReadOnlyCollection<string> explained)
        => screen != Activity.Hunt && Unlocks.IsOpen(screen, f) && BannerFor(screen, f, explained) is not null;
}
