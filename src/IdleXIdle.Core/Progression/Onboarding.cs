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
    /// <summary>Where the fight's lesson card appears — the toast slot under the header stack.</summary>
    LessonSlot,

    // ── TRAINING ──
    /// <summary>The training rows: one per stat, grouped, each with what it is now and after a rank.</summary>
    TrainingRows,
    /// <summary>The right-hand column that explains the selected stat in full.</summary>
    TrainingDetail,
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
    /// <summary>The region's own button — HUNT HERE, or RESUME HERE where you already are.</summary>
    EnterRegion,

    // ── TRAITS ──
    /// <summary>The trait tree canvas. Belongs to the OLD tree screen, which P5 deletes with the tree.</summary>
    TraitTree,
    /// <summary>The TRAIT POINTS TO SPEND readout. As above — nothing points a tour at it any more.</summary>
    TraitPoints,
    /// <summary>The three worn characteristic slots at the top of the TRAITS screen.</summary>
    TraitSlots,
    /// <summary>The grid of awakened and undiscovered characteristics.</summary>
    TraitCollection,
    /// <summary>The chosen characteristic's reading column, with the one button that wears it.</summary>
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
/// What is true right now that a screen could point at — the inputs to <see cref="Onboarding.HintFor"/>.
/// Every field is a fact the host already holds; nothing here is telemetry.
/// </summary>
/// <param name="NewRegionName">A region that is open, not conquered, and never hunted — or null.</param>
/// <param name="MasteryPointsFree">Mastery points earned and not spent.</param>
/// <param name="EmptySkillSlots">Open skill slots with nothing equipped.</param>
/// <param name="NewChampionName">A champion who joined and has not been looked at on the roster — or null.</param>
/// <param name="ChestsWaiting">Unopened chests in the vault.</param>
/// <param name="TrainableStat">The cheapest stat the player can afford to train right now — or null.</param>
/// <param name="TrainableCost">What that rank costs.</param>
/// <param name="AffordableUpgradeName">The first Warren facility whose next level is affordable — or null.</param>
/// <param name="SkillWithLevelToSpend">The first woven skill holding an unspent level — or null.</param>
/// <param name="SkillLevelsToSpend">Unspent levels across every woven skill.</param>
public readonly record struct HintFacts(
    string? NewRegionName = null,
    int MasteryPointsFree = 0,
    int EmptySkillSlots = 0,
    string? NewChampionName = null,
    int ChestsWaiting = 0,
    string? TrainableStat = null,
    long TrainableCost = 0,
    string? AffordableUpgradeName = null,
    string? SkillWithLevelToSpend = null,
    int SkillLevelsToSpend = 0);

/// <summary>One line a screen says at the top about its own state, and the key that dismisses it.</summary>
/// <param name="Key">Encodes the fact, so the same hint returns when the fact changes (two chests after one).</param>
public readonly record struct ScreenHint(string Key, string Text);

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
            new TourStep(TourTarget.Champion, "YOUR HUNTER",
                "This is your hunter. It fights on its own. You never press attack."),

            // ITEM 7 LIVES HERE: "each wave is randomized, but we never mention this fact." This says
            // the half a player can act on — what comes at you changes from wave to wave. (The band is
            // fixed by wave number and the creatures inside it are rolled from a seed of region, wave
            // and run, so a REPLAYED wave is the same wave; that is a fact about fast-forward, not
            // about what to expect, and a tour card is not where it belongs.)
            new TourStep(TourTarget.Enemies, "THE ENEMIES",
                "Enemies come in waves, and no two waves are made of the same thing. Every fifth is "
                + "a boss. The banner at the top counts the waves and the conquest."),

            new TourStep(TourTarget.HunterHud, "YOUR HUNTER'S LIFE",
                "This is your hunter's life. When it reaches zero the run ends — "
                + "then it gets back up and starts again. Nothing is lost."),

            new TourStep(TourTarget.CurrencyPills, "GLEAM",
                "Every cleared wave pays Gleam. Spend Gleam on the TRAINING screen to make your hunter stronger."),

            new TourStep(TourTarget.Skills, "YOUR SKILLS",
                "Your skills. They fire on their own timers. You choose them on the BUILD screen later."),

            new TourStep(TourTarget.RightColumn, "WHAT IS WAITING",
                "What is waiting for you. A chest is in the VAULT already, and when the game has "
                + "something else for you a button appears here to take you to it."),

            // EVERY TILE IS ON THE RAIL NOW, bound in chains until its screen opens (playtest
            // 2026-09-09: hiding them was the wrong call, and the card was written for the rail
            // that hid them — it told a first-run player that four tiles they could plainly see
            // were not there).
            new TourStep(TourTarget.NavRail, "THE OTHER SCREENS",
                "Every screen is on this rail from the start. The ones still in chains are not open yet — "
                + "a notice says when one opens, and a gold NEW mark waits on it."),

            new TourStep(TourTarget.LessonSlot, "LESSONS",
                "A gold NEW mark on a tile means that screen has something new, and the screen says what at the top. "
                + "The fight's own lessons appear here. Close one with the ×."),
        },

        Activity.Training => new[]
        {
            new TourStep(TourTarget.TrainingRows, "WHAT TO TRAIN",
                "Every row is one thing your hunter can train, grouped by what it changes. The two "
                + "numbers are what it is now and what it becomes if you buy one rank."),

            new TourStep(TourTarget.TrainingDetail, "THE FULL STORY",
                "This column explains the row you pick: what the stat does, what one rank adds, "
                + "and what the next rank costs."),

            // NO CRYSTAL HERE. A Crystal has no currency pill anywhere — the three pills are Scrap,
            // Memory Dust and Gleam, and Essence, Core and Crystal live on the Forge, which is
            // normally still shut when this tour runs at wave three. So the word arrived with nothing
            // to point at (playtest 2026-09-09). The bar prints its own price to a player who can by
            // then see one.
            new TourStep(TourTarget.ResetBar, "STARTING OVER",
                "This bar takes every trained rank back, so you can train differently. "
                + "It says on it what that costs, and the Gleam you spent does not come back."),
        },

        Activity.Gear => new[]
        {
            new TourStep(TourTarget.PaperDoll, "WHAT YOU WEAR",
                "Eight slots, one item each. Only what is worn counts in the fight. "
                + "A slot reads out on the right when you pick it."),

            new TourStep(TourTarget.Inventory, "YOUR ITEMS",
                "Every item you own that is not worn. Any of them can be dragged onto the doll. "
                + "A dimmed item belongs to another class, and this hunter cannot wear it."),

            new TourStep(TourTarget.ItemDetail, "THE ITEM",
                "The real numbers of the item you picked, and who can wear it. EQUIP wears it, "
                + "and what was in that slot goes back to the bag."),
        },

        Activity.Build => new[]
        {
            new TourStep(TourTarget.SkillSlots, "YOUR SKILL SLOTS",
                "Each row is one skill slot, and picking a row is how its skill is changed. "
                + "More slots open as you go deeper."),

            // Was the hardest sentence in the whole tour set: a conditional inside a conditional,
            // naming "roads" and "SIGNATURE" for the first time in the same breath.
            new TourStep(TourTarget.SkillPicker, "YOUR SKILL LIBRARY",
                "Twelve shared skills, each opened by the MASTERY tree. Your hunter's own skill is "
                + "the one at the top: it needs nothing, and it cannot be changed."),

            new TourStep(TourTarget.Vows, "VOWS AND KEYSTONES",
                "A Vow is a promise about your build. It pays a lot while it is kept and nothing when "
                + "it is not. Keystones are rules the world gives you for conquering it."),

            new TourStep(TourTarget.MasteryTile, "THE MASTERY TREE",
                "The MASTERY tile on the rail opens a tree of small rules that change how your skills "
                + $"behave. It opens when you {Unlocks.Requirement(Activity.Mastery).ToLowerInvariant()}."),
        },

        Activity.Mastery => new[]
        {
            new TourStep(TourTarget.MasteryTree, "FOUR DIRECTIONS",
                "Four directions grow out of the centre. RESONANCE is your skills' power and LOOT is "
                + "a richer haul. TEMPO hits sooner and more often; ENDURE outlasts the enemy."),

            new TourStep(TourTarget.Specialisations, "ONE STYLE",
                "A specialisation chooses your Style: that Style's skills hit twice as hard. One Style "
                + "per hunter, and TAKE EVERY POINT BACK lets you choose again."),

            new TourStep(TourTarget.NodeCard, "READING A NODE, AND TAKING IT",
                "A node reads out here when you pick it, and nothing is spent until TAKE. Points come "
                + "from reaching a depth you never reached, and TAKE EVERY POINT BACK is free."),
        },

        // The first visit's chest is the WELCOME GIFT a new game is seeded with (GiftChests), so both
        // cards speak of a chest that is really there — and say which kind it is.
        Activity.Vault => new[]
        {
            new TourStep(TourTarget.ChestCards, "YOUR CHESTS",
                "Every chest you hold, one card for each kind. Your first is a welcome gift. The card "
                + "says what it promises before it is opened."),

            // THE BUTTON SAYS WHAT IT SAYS. This card read "OPEN ALL opens every chest at once" — and
            // the tour's first visit is guaranteed to be the ONE-chest state (the welcome gift), where
            // VaultScreen draws OPEN THE CHEST. So the card named a control that was not on the screen
            // (playtest 2026-09-09), and the reading it did have was odd besides. It now names the
            // button by what it does at any count, and stops naming two tools it does not explain.
            new TourStep(TourTarget.VaultButtons, "THE TOOLBAR",
                "The button on the right opens your chests — one at a time, or all of them when you "
                + "hold more than one. Everything a chest gives you goes into your bag."),
        },

        Activity.Forge => new[]
        {
            new TourStep(TourTarget.Bag, "THE BAG",
                "Everything you own that is not worn, rarest first. An item goes onto the "
                + "bench. The mouse wheel scrolls the list."),

            new TourStep(TourTarget.ForgeItem, "THE BENCH",
                "The item on the bench: its name, picture, element and real numbers. "
                + "Every job on the right is done to this item."),

            new TourStep(TourTarget.ForgeTabs, "FOUR JOBS",
                "UPGRADE raises the item's level. RE-ROLL gives it a new random enchant. SOCKET sets "
                + "a gem into it. SALVAGE sells it, or breaks it into materials."),

            new TourStep(TourTarget.Materials, "YOUR MATERIALS",
                "Every job costs materials. Waves pay them, and deeper waves pay better ones. "
                + "Sometimes a wave pays a paper that covers one whole job on its own."),
        },

        Activity.Warren => new[]
        {
            new TourStep(TourTarget.Facilities, "THE FACILITIES",
                "Each card is a facility. They produce on their own, even while the game is closed. "
                + "Conquering regions opens more of them."),

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
                "A region reads out here: its element, how its enemies fight, the wave that "
                + "conquers it, and what it drops. Where you hunt is a loot choice too."),

            new TourStep(TourTarget.EnterRegion, "GO THERE",
                "HUNT HERE moves the hunt to the region you picked. A locked region names the one to "
                + "conquer first."),
        },

        Activity.Traits => new[]
        {
            // REWRITTEN FOR THE NEW SYSTEM (P4). The three cards used to teach a spine, four roads
            // and a currency, and every word of that is now false: traits are not bought, there is no
            // tree, and nothing on the screen costs anything.
            new TourStep(TourTarget.TraitSlots, "THREE AT A TIME",
                "A trait is a characteristic your hunter has. Each hunter wears three of them, and "
                + "changing which three costs nothing at all."),

            new TourStep(TourTarget.TraitCollection, "THEY AWAKEN, THEY ARE NOT BOUGHT",
                "A trait awakens from what you have done — how you fight, what you survive, where "
                + "you go. One you have not awakened yet shows only as ???."),

            new TourStep(TourTarget.TraitDetail, "READING ONE",
                "A trait reads out here, and WEAR IT puts it on — or drag it onto a slot. Every hunter you own can "
                + "wear any trait you have awakened."),
        },

        Activity.Roster => new[]
        {
            new TourStep(TourTarget.ChampionCards, "THE HUNTERS",
                "One card per hunter, one column per gear class. A card shows its innate power and "
                + "what unlocks it."),

            new TourStep(TourTarget.ChampionDetail, "WHO THEY ARE",
                "The hunter's starting skill, its always-on innate power, its road and its gear "
                + "class. Gear of another class cannot be worn."),

            new TourStep(TourTarget.BecomeThem, "SET ACTIVE",
                "SET ACTIVE plays as this hunter. Nothing resets. Gear this hunter cannot "
                + "wear goes back to your bag."),
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

    /// <summary>
    /// A saved explained-screen key, read forward across renames. The save stores <see cref="Activity"/>
    /// NAMES; when STATS became TRAINING (2026-09-01, UX V2) every existing save still said "Stats", and
    /// without this a returning player would be re-toured through a screen they had already learned.
    /// </summary>
    public static string ModernScreenKey(string saved) => saved switch
    {
        "Stats" => nameof(Activity.Training),
        _ => saved,
    };

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

    /// <summary>
    /// <see cref="IsNew(Activity, UnlockFacts, IReadOnlyCollection{string})"/>, plus the lesson clause: while
    /// a guide rung is showing, the tile it <see cref="Tutorial.Sends"/> the player to wears the mark too.
    /// </summary>
    /// <remarks>
    /// UX V2 P0.7. A lesson used to be a strip across the bottom of EVERY screen ("press V for STATS" over
    /// the Forge). Now it renders only on the screen it is about; everywhere else, the rung is this mark
    /// on the tile it points at — the rail says where, the screen says what.
    /// </remarks>
    public static bool IsNew(Activity screen, UnlockFacts f, IReadOnlyCollection<string> explained,
                             TutorialStep? showing, TutorialFacts tf)
        => IsNew(screen, f, explained)
           || (screen != Activity.Hunt && Unlocks.IsOpen(screen, f)
               && showing is { } s && Tutorial.HasGuidance(s) && Tutorial.Sends(s, tf) == screen);

    /// <summary>
    /// The one line this screen says about its own state right now, or null when nothing is true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UX V2 P0.7 (chrome audit §7). After the tours and the slot notes, a screen teaches by pointing at a
    /// fact: unspent points, a waiting chest, a rank you can afford. Every line is a pure function of state
    /// the host already holds, so it appears when the fact becomes true and disappears on its own when it
    /// stops — nothing is scheduled, nothing is guessed. The key carries the fact, so dismissing "1 CHEST"
    /// does not silence "2 CHESTS".
    /// </para>
    /// <para>
    /// GEAR says nothing: "an unworn item may beat what you wear" needs a per-slot comparison Core does not
    /// make yet. FORGE says nothing: nothing in Core says an item wants work. HUNT teaches through its
    /// lessons, not a hint. A hint is a line, never a paragraph.
    /// </para>
    /// </remarks>
    public static ScreenHint? HintFor(Activity screen, HintFacts f) => screen switch
    {
        Activity.Map when f.NewRegionName is { Length: > 0 } r =>
            new ScreenHint($"Hint:Map:{r}", $"A NEW REGION IS AVAILABLE — {r.ToUpperInvariant()}"),
        // NOTHING IS SPENT ON THE TRAITS SCREEN, so there is no "you have N to spend" hint to give.
        // TraitPointsFree was kept on these facts for the old Memory tree screen; both are deleted.
        Activity.Mastery when f.MasteryPointsFree > 0 =>
            new ScreenHint($"Hint:Mastery:{f.MasteryPointsFree}", $"YOU HAVE {f.MasteryPointsFree} MASTERY POINT{Plural(f.MasteryPointsFree)}"),
        // A LEVEL TO SPEND OUTRANKS AN EMPTY SLOT. Playtest 2026-09-09: "the skill upgrade section
        // wasn't understood at all; no one would have even looked at it if I hadn't pointed it out. We
        // didn't provide any guidance, and it's tucked away in a hidden spot." A skill's variation is
        // the one decision in the game the player is never told they can make, and it is the deeper of
        // the two — an empty slot is answered by picking from a list, this is answered by reading three
        // effects and choosing.
        Activity.Build when f.SkillWithLevelToSpend is { Length: > 0 } sk =>
            new ScreenHint($"Hint:Build:Level:{sk}:{f.SkillLevelsToSpend}",
                           f.SkillLevelsToSpend == 1
                               ? $"{sk.ToUpperInvariant()} HAS A LEVEL TO SPEND — CHOOSE WHAT IT BECOMES"
                               : $"{f.SkillLevelsToSpend} SKILL LEVELS TO SPEND — CHOOSE WHAT THEY BECOME"),
        Activity.Build when f.EmptySkillSlots > 0 =>
            new ScreenHint($"Hint:Build:{f.EmptySkillSlots}",
                           f.EmptySkillSlots == 1 ? "AN EMPTY SKILL SLOT — EQUIP A SKILL" : $"{f.EmptySkillSlots} EMPTY SKILL SLOTS — EQUIP SKILLS"),
        Activity.Roster when f.NewChampionName is { Length: > 0 } c =>
            new ScreenHint($"Hint:Roster:{c}", $"A NEW HUNTER HAS JOINED — {c.ToUpperInvariant()}"),
        Activity.Vault when f.ChestsWaiting > 0 =>
            new ScreenHint($"Hint:Vault:{f.ChestsWaiting}", f.ChestsWaiting == 1 ? "1 CHEST IS WAITING" : $"{f.ChestsWaiting} CHESTS ARE WAITING"),
        Activity.Training when f.TrainableStat is { Length: > 0 } s =>
            new ScreenHint($"Hint:Training:{s}:{f.TrainableCost}", $"YOU CAN TRAIN {s.ToUpperInvariant()} FOR {f.TrainableCost} GLEAM"),
        Activity.Warren when f.AffordableUpgradeName is { Length: > 0 } u =>
            new ScreenHint($"Hint:Warren:{u}", $"AN UPGRADE IS AFFORDABLE — {u.ToUpperInvariant()}"),
        _ => null,
    };

    private static string Plural(int n) => n == 1 ? "" : "S";
}
