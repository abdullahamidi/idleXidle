using System;
using System.Collections.Generic;
using IdleXIdle.Core.Encounters;

namespace IdleXIdle.Core.Progression;

/// <summary>
/// Which part of a screen a tour card points at. The screen it belongs to maps it to a rectangle.
/// </summary>
/// <remarks>
/// Named regions rather than rectangles, because the rectangles are each screen's business and move
/// whenever its layout does. The copy here only needs to know WHAT it is talking about. The targets are
/// grouped by screen; a screen is only ever asked about its own, and answers the rest with nothing.
/// </remarks>
public enum TourTarget
{
    // ── HUNT ──
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

    // ── STATS ──
    /// <summary>The training rows: one per stat, with its TRAIN button and the real fight number.</summary>
    TrainingRows,
    /// <summary>The hunter card: level, gear power, mastery points.</summary>
    HunterCard,
    /// <summary>The RESET ALL TRAINING bar under the rows.</summary>
    ResetBar,

    // ── GEAR ──
    /// <summary>The paper doll: eight worn slots around the champion.</summary>
    PaperDoll,
    /// <summary>The inventory grid of unworn items.</summary>
    Inventory,
    /// <summary>The detail column for the clicked item, with EQUIP.</summary>
    ItemDetail,

    // ── BUILD (the weave) ──
    /// <summary>The skill slot rows.</summary>
    SkillSlots,
    /// <summary>The skill library picker — the twelve learnable skills.</summary>
    SkillPicker,
    /// <summary>The VOWS column and the keystone sockets.</summary>
    Vows,
    /// <summary>The MASTERY tile on the navigation rail — the door to the tree.</summary>
    MasteryTile,

    // ── MASTERY ──
    /// <summary>The tree itself: four directions out of the centre.</summary>
    MasteryTree,
    /// <summary>The attunement hex: six specialisations, one discipline.</summary>
    Specialisations,
    /// <summary>The docked node card, and the TAKE EVERY POINT BACK button.</summary>
    NodeCard,

    // ── VAULT ──
    /// <summary>The chest cards.</summary>
    ChestCards,
    /// <summary>The ? chip on a chest card that reads its promise.</summary>
    ChestQuestion,
    /// <summary>The header buttons: OPEN ALL, TRADER, PASTE A CODE.</summary>
    VaultButtons,

    // ── FORGE ──
    /// <summary>The bag of unworn items.</summary>
    Bag,
    /// <summary>The item on the bench.</summary>
    ForgeItem,
    /// <summary>The four job tabs: UPGRADE, RE-ROLL, SOCKET, SALVAGE.</summary>
    ForgeTabs,
    /// <summary>The materials wallet.</summary>
    Materials,
    /// <summary>The SOCKET tab alone — the first-gem lesson's target.</summary>
    SocketTab,

    // ── WARREN ──
    /// <summary>The facility cards.</summary>
    Facilities,
    /// <summary>The chosen facility's detail, with its upgrade button.</summary>
    FacilityDetail,
    /// <summary>The overview: level and total production.</summary>
    WarrenOverview,

    // ── MAP ──
    /// <summary>The chain of region nodes.</summary>
    RegionChain,
    /// <summary>The chosen region's detail column.</summary>
    RegionDetail,
    /// <summary>The ENTER THIS REGION button.</summary>
    EnterRegion,

    // ── TRAITS ──
    /// <summary>The trait tree canvas.</summary>
    TraitTree,
    /// <summary>The TRAIT POINTS TO SPEND readout.</summary>
    TraitPoints,
    /// <summary>The chosen trait's detail column, with LEARN.</summary>
    TraitDetail,

    // ── ROSTER ──
    /// <summary>The champion cards.</summary>
    ChampionCards,
    /// <summary>The chosen champion's detail column.</summary>
    ChampionDetail,
    /// <summary>The BECOME THEM button.</summary>
    BecomeThem,
}

/// <summary>One card of a tour: what it points at, its title, and its two or three short sentences.</summary>
public readonly record struct TourStep(TourTarget Target, string Title, string Body);

/// <summary>A note a screen shows at the top: the key that remembers it, and the words.</summary>
/// <param name="Key">Written into the save's explained list when the player closes the banner.</param>
public readonly record struct ScreenBanner(string Key, string Title, string Body);

/// <summary>
/// The first minute on every screen: a click-through tour of the HUNT before the first wave, and a tour
/// of each other screen the first time it is opened.
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
/// <b>Then the other screens explained themselves in a paragraph, and that failed too.</b> The HUNT got
/// a spotlight tour — one region lit at a time, two sentences beside it — and every other screen got a
/// dense banner in small type. Playtest: "the explanation texts on the other screens are both too
/// crowded and too small. The Hunt screen's walkthrough was much clearer." So every screen has a tour
/// now, in the HUNT's exact style, and the HUNT's intro is simply the tour of the HUNT.
/// </para>
/// <para>
/// A tour runs once per screen, the first time that screen is on top, and the screen keeps working
/// underneath it — the champion keeps fighting. The player advances it by clicking and can skip it.
/// Nothing is announced over the top of a screen the player did not ask for: an opened screen gets a
/// small NEW mark on its rail tile, and its tour waits there until the player goes.
/// </para>
/// <para>
/// Derived where it can be. Whether a tile is new is a question asked of the unlock gates and the
/// explained list, never a flag set at the moment of opening — so a screen that opened while the game
/// was closed is still marked, and no call site can forget to mark it.
/// </para>
/// </remarks>
public static class Onboarding
{
    /// <summary>The tour of the HUNT screen — the intro. Eight cards, each pointing at one region.</summary>
    /// <remarks>Kept under its old name because it is asked for by name: the intro's decision is its own
    /// (<see cref="IntroDue"/>), while every other tour is owed by the explained list.</remarks>
    public static IReadOnlyList<TourStep> Intro => TourFor(Activity.Hunt);

    /// <summary>
    /// The tour of a screen, in the order it is shown. Two to four cards, each pointing at one region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the order of looking, not of importance: the thing the screen is FOR first, then
    /// what to do to it, then the button that does it. Plain words throughout: no abbreviations, no
    /// genre vocabulary, short sentences, and no body longer than a hundred and sixty characters —
    /// the caption card is five hundred and sixty pixels wide, and a fourth line is where the old
    /// banner's "too crowded" began.
    /// </para>
    /// <para>
    /// Every fact here is one the screen itself shows or the rules enforce: the Stats rows draw the
    /// real fight number, the reset bar prices itself in Crystal and refunds no Gleam, a dimmed
    /// inventory cell is another class's, the four Forge tabs are named on the strip, trait points
    /// come from the three terms the host sums. A tour that promised something the screen does not do
    /// would be worse than none, so where a number lives in the rules it is read from them.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<TourStep> TourFor(Activity screen) => screen switch
    {
        Activity.Hunt => new[]
        {
            new TourStep(TourTarget.Champion, "YOUR CHAMPION",
                "This is your champion. It fights on its own. You never press attack."),

            new TourStep(TourTarget.Enemies, "THE ENEMIES",
                "Enemies come in waves. Every fifth wave is a boss. "
                + "The banner at the top counts the waves and the conquest."),

            new TourStep(TourTarget.HunterHud, "YOUR CHAMPION'S LIFE",
                "This is your champion's life. When it reaches zero the descent ends — "
                + "then it gets back up and starts again. Nothing is lost."),

            new TourStep(TourTarget.CurrencyPills, "GLEAM",
                "Every cleared wave pays Gleam. Spend Gleam on the STATS screen to train your champion."),

            new TourStep(TourTarget.Skills, "YOUR SKILLS",
                "Your skills. They fire on their own timers. You choose them on the BUILD screen later."),

            new TourStep(TourTarget.RightColumn, "REWARDS AND ERRANDS",
                "Rewards and errands. A chest waits in the VAULT already; when a boss drops another, "
                + "or you earn mastery points, the buttons here take you there."),

            new TourStep(TourTarget.NavRail, "THE OTHER SCREENS",
                "The other screens. Most are closed for now. They open as you play — "
                + "a gold NEW mark shows what just opened."),

            new TourStep(TourTarget.GuideStrip, "LESSONS",
                "When there is something new to do, a short lesson appears down here. Close it with the ×. "
                + "That is all — go and watch the first wave."),
        },

        Activity.Stats => new[]
        {
            new TourStep(TourTarget.TrainingRows, "TRAIN YOUR CHAMPION",
                "Every row is one thing your champion can train. Press TRAIN to spend Gleam on it. "
                + "The number on the row is the real number the fight uses."),

            new TourStep(TourTarget.HunterCard, "YOUR CHAMPION",
                "Your champion's level, life, gear power and mastery points. Gear power comes from "
                + "what you wear on the GEAR screen. Mastery points go to the MASTERY tree."),

            new TourStep(TourTarget.ResetBar, "STARTING OVER",
                "This bar takes every trained rank back, so you can train differently. "
                + "It costs a Crystal. The Gleam you spent does not come back."),
        },

        Activity.Gear => new[]
        {
            new TourStep(TourTarget.PaperDoll, "WHAT YOU WEAR",
                "Eight slots, one item each. Only what is worn counts in the fight. "
                + "Click a slot to see what is in it."),

            new TourStep(TourTarget.Inventory, "YOUR ITEMS",
                "Every item you own that is not worn. Click one to read it. "
                + "A dimmed item belongs to another class of champion, and this one cannot wear it."),

            new TourStep(TourTarget.ItemDetail, "THE ITEM",
                "The real numbers of the item you clicked, and who can wear it. Press EQUIP to wear it. "
                + "What was in that slot goes back to the bag."),
        },

        Activity.Build => new[]
        {
            new TourStep(TourTarget.SkillSlots, "YOUR SKILL SLOTS",
                "Each row is one skill slot. Click a row to change the skill in it. "
                + "More slots open as you go deeper."),

            new TourStep(TourTarget.SkillPicker, "YOUR SKILL LIBRARY",
                "Twelve skills exist, two per style. You learn them on the MASTERY tree, and once "
                + "learned they are yours for good — pick any learned one for the chosen slot."),

            new TourStep(TourTarget.Vows, "VOWS AND KEYSTONES",
                "A Vow is a promise on one skill. It pays a lot while the promise is kept, and nothing "
                + "when it is not. Keystones are rules learned on the TRAITS screen."),

            new TourStep(TourTarget.MasteryTile, "THE MASTERY TREE",
                "The MASTERY tile on the rail opens a tree of small rules that change how your skills "
                + $"behave. It opens when you {Unlocks.Requirement(Activity.Mastery).ToLowerInvariant()}."),
        },

        Activity.Mastery => new[]
        {
            new TourStep(TourTarget.MasteryTree, "FOUR DIRECTIONS",
                "Four directions grow out of the centre. RESONANCE is your skills' power. LOOT is a "
                + "richer haul. TEMPO is hitting first and often, and ENDURE outlasts the enemy."),

            new TourStep(TourTarget.Specialisations, "ONE DISCIPLINE",
                "A specialisation is your discipline: that STYLE's skills hit twice as hard, and "
                + "its road teaches the style's two skills — learned for good."),

            new TourStep(TourTarget.NodeCard, "POINTS, AND TAKING THEM BACK",
                "Rest the pointer on a node to read it here. Points come from reaching a depth you "
                + "have never reached. TAKE EVERY POINT BACK is free, any time."),
        },

        // The first visit's chest is the WELCOME GIFT a new game is seeded with (GiftChests), so both
        // cards speak of a chest that is really there — and say which kind it is.
        Activity.Vault => new[]
        {
            new TourStep(TourTarget.ChestCards, "YOUR CHESTS",
                "Every chest you hold, one card per kind. Your first is a welcome gift. A card shows "
                + "the grade, the tier and the element. Click a card to open one."),

            new TourStep(TourTarget.ChestQuestion, "WHAT IS INSIDE",
                "Rest the pointer on the small glass to read what a chest promises. A boss's chest "
                + "only promises a lowest rarity. The gift says exactly what it holds."),

            new TourStep(TourTarget.VaultButtons, "THE HEADER BUTTONS",
                "OPEN ALL opens every chest at once. TRADER opens a stall that changes every week. "
                + "PASTE A CODE reads a code someone shared with you."),
        },

        Activity.Forge => new[]
        {
            new TourStep(TourTarget.Bag, "THE BAG",
                "Everything you own that is not worn, rarest first. Click an item to put it on the "
                + "bench. The mouse wheel scrolls the list."),

            new TourStep(TourTarget.ForgeItem, "THE BENCH",
                "The item on the bench: its name, picture, element and real numbers. "
                + "Every job on the right is done to this item."),

            new TourStep(TourTarget.ForgeTabs, "FOUR JOBS",
                "UPGRADE raises the item's level. RE-ROLL gives it a new random enchant. SOCKET sets "
                + "a gem into it. SALVAGE sells it, or breaks it into materials."),

            new TourStep(TourTarget.Materials, "YOUR MATERIALS",
                "Every job costs materials. Waves pay them, and deeper waves pay better ones. "
                + "A CHART is a one-use paper that pays for one job in full."),
        },

        Activity.Warren => new[]
        {
            new TourStep(TourTarget.Facilities, "THE FACILITIES",
                "Each card is a facility. They produce on their own, even while the game is closed. "
                + "Click one to read it."),

            new TourStep(TourTarget.FacilityDetail, "UPGRADING",
                "What the chosen facility makes each minute, and what the next level costs. "
                + "The button at the bottom upgrades it. Conquering regions raises what it makes."),

            new TourStep(TourTarget.WarrenOverview, "WHAT IT ALL MAKES",
                "The total the Warren makes each minute, all facilities together. "
                + "Check in, spend, leave. It is meant to be checked, not watched."),
        },

        Activity.Map => new[]
        {
            new TourStep(TourTarget.RegionChain, "THE REGIONS",
                $"The world is a chain of regions. Each is about {RegionLadder.StepPercent:0}% tougher "
                + "than the last. Reach the goal depth in one to conquer it, which opens the next."),

            new TourStep(TourTarget.RegionDetail, "WHAT A REGION HOLDS",
                "Click a region to read it here: its element, its enemies, the depth that conquers it, "
                + "and what it drops. Where you hunt is a loot choice too."),

            new TourStep(TourTarget.EnterRegion, "GO THERE",
                "ENTER THIS REGION moves the hunt there. Once every region is yours, a corruption "
                + "ladder appears above this button: enemies hit harder and take more hits."),
        },

        Activity.Traits => new[]
        {
            new TourStep(TourTarget.TraitTree, "ONE SPINE, FOUR ROADS",
                "Traits are permanent bonuses. One spine everyone grows, and four roads: RUIN hits "
                + "harder, AEGIS lives longer, ARTIFICE changes skills, AVARICE finds more loot."),

            new TourStep(TourTarget.TraitPoints, "TRAIT POINTS",
                "Traits cost TRAIT POINTS. You earn one for each region you conquer, one each time a "
                + "region's mastery rises a level, and two for each new corruption tier."),

            new TourStep(TourTarget.TraitDetail, "LEARN",
                "Click a trait to read it here, then press LEARN. Each road ends in a capstone so "
                + "costly you cannot finish the other three. A trait never resets, not ever."),
        },

        Activity.Roster => new[]
        {
            new TourStep(TourTarget.ChampionCards, "THE CHAMPIONS",
                "One card per champion, one column per gear class, the first of the class above the "
                + "second. A card shows its road and what unlocks it. Click one to read it."),

            new TourStep(TourTarget.ChampionDetail, "WHO THEY ARE",
                "The champion's always-on power, what it is best at, and its gear class. "
                + "Two champions share each class. Gear of another class cannot be worn."),

            new TourStep(TourTarget.BecomeThem, "BECOME THEM",
                "Press BECOME THEM to play as this champion. Nothing is lost: gear it cannot wear "
                + "goes back to the bag, and your build and progress stay."),
        },

        // A cast that is not a declared Activity is a programming error, and an empty tour would reach
        // the player as a scrim with nothing lit instead of as the bug it is.
        _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, null),
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

    /// <summary>The explained-list key of the first-gem lesson — the Forge's second, smaller tour.</summary>
    /// <remarks>
    /// Lives in the same list as the screens and the slot notes, under its own name, and follows the
    /// same rule: shown once, remembered in the save.
    /// </remarks>
    public const string GemTourKey = "FirstGem";

    /// <summary>
    /// The first-gem lesson: two cards over the FORGE, the first time the player holds a gem.
    /// </summary>
    /// <remarks>
    /// Playtest 2026-08-26: "When a gem drops, take the player to the socket screen and show setting
    /// the gem." A gem is the one drop that does nothing on its own — it is worn INSIDE an item — and
    /// the Forge's tour names the SOCKET tab in one clause among four. So the gem gets a tour of its
    /// own, on the Forge, with the SOCKET tab open: what the stone is, and what to click. The price
    /// line is read from the rule (<c>GemCraft.IsFirstGemFree</c>), so the card cannot promise a
    /// discount the Forge then refuses.
    /// </remarks>
    public static IReadOnlyList<TourStep> GemTourFor(bool freeSocketUsed) => new[]
    {
        new TourStep(TourTarget.Bag, "YOUR FIRST GEM",
            "A gem is a stone you set into an item, and it adds its stat to that item. With the SOCKET "
            + "tab open, your gems are listed here in the bag."),

        new TourStep(TourTarget.SocketTab, "SETTING IT",
            "Put a RARE or better item on the bench, keep SOCKET open, then click the gem in the bag. "
            + (Economy.GemCraft.IsFirstGemFree(freeSocketUsed)
                ? "Your first gem is free to set. Later ones cost Essence."
                : "Setting a gem costs Essence.")),
    };

    /// <summary>
    /// The explained-list key of the gem lesson while it is owed, or null: owed once a gem is held,
    /// until it has been given.
    /// </summary>
    /// <param name="gemsHeld">Gems the player holds — loose in the bag or set into an item.</param>
    /// <remarks>
    /// Derived, like the screen tours: it needs no "a gem dropped" flag, so a gem that dropped while
    /// the game was closed, or came out of OPEN ALL, still earns the lesson. And it needs a gem to
    /// point at — a player who sold their only gem before visiting is owed nothing until the next.
    /// </remarks>
    public static string? GemTourDue(int gemsHeld, IReadOnlyCollection<string> explained)
        => gemsHeld >= 1 && !explained.Contains(GemTourKey) ? GemTourKey : null;

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
    /// a NEW mark on every open tile and a tour on every screen a returning player has used for
    /// hours. Such a save is recognisable — the intro is neither seen nor due — and for it every screen
    /// open at load and every skill slot already earned counts as explained. A player who has seen the
    /// intro gets the list exactly as saved: whatever they have not yet read is still waiting for them.
    /// </remarks>
    public static IReadOnlyCollection<string> SeedExplained(
        UnlockFacts f, bool introSeen, IEnumerable<string> explained, bool freeSocketUsed = false)
    {
        var set = new HashSet<string>(explained);
        // A player who has already set a gem needs no lesson in setting one — and the lesson's last
        // card would promise a free socket the Forge then refuses (review 2026-08-26). This holds for
        // every save, not only the ones from before the intro.
        if (freeSocketUsed) set.Add(GemTourKey);
        var returningFromBefore = !introSeen
            && !IntroDue(new TutorialFacts(WavesCleared: f.WavesCleared), false);
        if (!returningFromBefore) return set;

        foreach (var screen in Unlocks.Open(f)) set.Add(ScreenKey(screen));
        for (var slot = 2; slot <= Unlocks.SkillSlots(f); slot++) set.Add(SlotKey(slot));
        // A player from before the intro has had the Forge for hours; the gem lesson is not owed.
        set.Add(GemTourKey);
        return set;
    }

    /// <summary>
    /// The explained-list key of the tour this screen still owes, or null if it has been given.
    /// </summary>
    /// <remarks>
    /// The Hunt owes nothing here: its tour is the intro, and whether the intro runs is decided by
    /// <see cref="IntroDue"/> against the save's own flag. Every other screen's tour is owed until its
    /// key is in the list, which the host writes when the tour is finished or skipped.
    /// </remarks>
    public static string? TourDue(Activity screen, IReadOnlyCollection<string> explained)
        => screen != Activity.Hunt && !explained.Contains(ScreenKey(screen)) ? ScreenKey(screen) : null;

    /// <summary>
    /// The note this screen should show at the top, after its tour, or null if nothing is owed.
    /// </summary>
    /// <remarks>
    /// Only the BUILD screen has notes: one for each skill slot that opened after the screen did. They
    /// are handed over one at a time, and only once the screen's own tour has been given — what the
    /// screen is, then what changed on it. While the tour is still owed this returns null, so the
    /// two can never be on screen together.
    /// </remarks>
    public static ScreenBanner? BannerFor(Activity screen, UnlockFacts f, IReadOnlyCollection<string> explained)
    {
        if (screen != Activity.Build || TourDue(screen, explained) is not null) return null;
        for (var slot = 2; slot <= Unlocks.SkillSlots(f); slot++)
            if (!explained.Contains(SlotKey(slot)))
                return new ScreenBanner(SlotKey(slot), "A NEW SKILL SLOT", Unlocks.SkillSlotNote(slot));
        return null;
    }

    /// <summary>Does this rail tile deserve a gold NEW mark — open, with something unread waiting on it?</summary>
    public static bool IsNew(Activity screen, UnlockFacts f, IReadOnlyCollection<string> explained)
        => screen != Activity.Hunt && Unlocks.IsOpen(screen, f)
           && (TourDue(screen, explained) is not null || BannerFor(screen, f, explained) is not null);
}
