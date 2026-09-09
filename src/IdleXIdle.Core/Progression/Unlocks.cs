using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Progression;

/// <summary>The things a player can go and do, in the order the game opens them.</summary>
/// <remarks>
/// The order is the LOOP's order. Each one is opened by the fact that makes it make sense: the Forge
/// opens when a chest is waiting, not when a wave counter says so, because a Forge with nothing in it
/// teaches a player that the Forge is empty.
/// </remarks>
public enum Activity
{
    /// <summary>The hunt itself. Never locked — it is the game.</summary>
    Hunt,

    /// <summary>Spending Gleam on the champion's stats. The first decision anyone makes.</summary>
    /// <remarks>
    /// Named TRAINING since 2026-09-01 (UX V2): the screen is a purchase screen, and calling it STATS set
    /// the wrong expectation. The save stores explained-screen keys by NAME, so
    /// <see cref="Onboarding.ModernScreenKey"/> maps the old "Stats" forward on read.
    /// </remarks>
    Training,

    /// <summary>Wearing what dropped.</summary>
    Gear,

    /// <summary>Reading an unopened chest before deciding to crack it.</summary>
    Vault,

    /// <summary>Cracking chests, refining, reforging, salvaging.</summary>
    Forge,

    /// <summary>Weaving skills, keystones and Vows.</summary>
    Build,

    /// <summary>The mastery tree — how the skills you wove behave.</summary>
    /// <remarks>
    /// Deliberately its OWN activity rather than a corner of <see cref="Build"/>. The tree was reachable
    /// only through a button labelled EDIT BUILD on the Build overview, which is neither what it edits
    /// nor what a player would go looking for, and it is the second-largest system in the game.
    /// </remarks>
    Mastery,

    /// <summary>Travelling between regions.</summary>
    Map,

    /// <summary>The facility economy that runs while you are away.</summary>
    Warren,

    /// <summary>The permanent trait tree.</summary>
    Traits,

    /// <summary>The champions you have collected.</summary>
    Roster,
}

/// <summary>What the game knows about a player, in the terms the unlock gates care about.</summary>
/// <remarks>
/// Every field defaults to zero — the state of a save that does not exist yet — so a caller naming only
/// the facts it cares about gets "a brand-new player" for the rest, rather than a compile error that
/// pushes it toward passing placeholder numbers it does not mean.
/// </remarks>
public readonly record struct UnlockFacts(
    int WavesCleared = 0,
    int DeepestWave = 0,
    int ItemsOwned = 0,
    int ChestsEverHeld = 0,
    int RegionsConquered = 0,
    /// <summary>
    /// How many characteristics the ACCOUNT has discovered. Replaced <c>TraitPointsEarned</c>, which
    /// counted a currency the retired Memory tree spent — the TRAITS screen was gated on having
    /// something to SPEND, on a screen where nothing is spent any more. It opens on having something to
    /// SEE instead, which is what the screen is for.
    /// </summary>
    int TraitsDiscovered = 0,
    /// <summary>Mastery points the career has paid so far (<c>MasteryPoints.Total</c> over the regions' best depths).</summary>
    int MasteryPointsEarned = 0,
    /// <summary>Skills the hunter may weave right now: the signature plus every road the tree has taught.</summary>
    int SkillsKnown = 0,
    /// <summary>Keystones the world has taught — the conquest gift is the first.</summary>
    int KeystonesDiscovered = 0,
    /// <summary>Vows the account has found by playing.</summary>
    int VowsKnown = 0,
    /// <summary>Hunters on the roster, the starter included.</summary>
    int CharactersUnlocked = 0);

/// <summary>
/// Which of the game's activities are open yet, and what each one is for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every screen in the game was reachable from the first frame.</b> Playtest, verbatim: <i>"Hemen
/// oyunun içine atıldım ve bilmeme rağmen kafam karıştı. Oyundaki etkinlikler yavaş yavaş açılmalı.
/// Hepsi detaylı şekilde anlatılmalı."</i> — thrown straight in and confused despite knowing the game;
/// activities should open gradually and all be explained in detail. That is from the person who
/// designed it, which is the strongest possible version of the complaint: nine destinations on the rail
/// at once is not a menu, it is a wall.
/// </para>
/// <para>
/// <b>Derived, never stored.</b> Same discipline as <see cref="Tutorial"/> and for the same reason: an
/// "unlocked" flag in the save is a second source of truth that drifts, and the drift is silent —
/// a player who lost their flags would find the game had re-locked itself around them. Everything here
/// is a question asked of facts the save already carries, so it is correct on any save, including every
/// save that existed before this file did.
/// </para>
/// <para>
/// A gate is only ever opened by something the player DID. No timers, no "after 5 minutes" — the game
/// should read as responding to you, and a thing that unlocks on a clock reads as a thing that was
/// always going to unlock.
/// </para>
/// </remarks>
public static class Unlocks
{
    /// <summary>Waves cleared before TRAINING opens. The guide's SPEND GLEAM rung waits for the same number.</summary>
    public const int TrainingOpensAtWaves = 3;

    /// <summary>Mastery points earned before the tree opens — the trunk and the first step of a route.</summary>
    public const int MasteryOpensAtPoints = 3;

    /// <summary>Is this activity open to the player yet?</summary>
    /// <remarks>
    /// <b>THE NEW-PLAYER JOURNEY (2026-09-06): PLAY → NEED → REVEAL → EXPLAIN → USE.</b> Every gate
    /// below names the need that opens it, and nothing opens before that need is real: the Map when
    /// there is somewhere else to go, the Build when there is a real choice to make, the Roster when
    /// there is a second hunter to meet. The Map and the Roster were open from the first frame for
    /// window-shopping (playtest nine); the journey brief closed them again — a screen with one
    /// thing on it and nine locks teaches the locks. What a fresh save sees is the Hunt, alone.
    /// </remarks>
    public static bool IsOpen(Activity activity, UnlockFacts f) => activity switch
    {
        // The game. If this is ever gated, the player is looking at a locked screen on launch.
        Activity.Hunt => true,

        // AFTER SEVERAL WAVES, not the first: the first wave's Gleam is a number going up, and that
        // is the whole lesson of the first minute. By the third the purse is worth a decision.
        Activity.Training => f.WavesCleared >= TrainingOpensAtWaves,

        // Opens the moment there is something to wear. Gated on OWNING, not on a wave count — a Gear
        // screen with an empty bag teaches a new player that gear is not part of this game.
        Activity.Gear => f.ItemsOwned >= 1,

        // The first chest is a moment, and the Vault is where it is read. Opens on that chest alone —
        // never on owning items, because a Vault with nothing in it is a page about nothing.
        //
        // ON THE FIRST CHEST *EVER*, NOT THE PILE RIGHT NOW. This read ChestsHeld — the count of
        // unopened chests — so opening your last chest re-LOCKED the Vault, and the next drop re-opened
        // it, and the "NEWLY OPENED" explanation panel fired again on every single chest for the rest
        // of the game. Playtest: "her chest geldiğinde newly open the vault diye önüme bi yazı parçası
        // çıkarıyor... zaten biliyorum açıldığını." A gate must be MONOTONE: every fact here only ever
        // grows, so nothing a player does can close a door they have walked through — or make the game
        // announce the same door twice.
        Activity.Vault => f.ChestsEverHeld >= 1,

        // Chests are what the Forge is FOR. Also opened by owning items, because salvaging is the other
        // half of it and a player with junk and no chest still has a reason to be here.
        Activity.Forge => f.ChestsEverHeld >= 1 || f.ItemsOwned >= 2,

        // WHEN THERE IS A REAL CHOICE. A hunter starts with one skill — its signature — and one slot;
        // a Build screen then is a page with one row and nothing to put in it. The screen opens the
        // moment the world hands over something to choose BETWEEN: a second skill taught by the tree,
        // the keystone a conquest gives, or a vow found by playing. (It was wave 5, which opened a
        // second slot with nothing to fill it.)
        Activity.Build => f.SkillsKnown >= 2 || f.KeystonesDiscovered >= 1 || f.VowsKnown >= 1,

        // ON THE FIRST MEANINGFUL POINTS. Three buys the trunk and the first step of a route — a
        // walk in, a purchase, and a fork to stand at — which the curve pays at depth 12. (It was
        // wave 25, a depth many first sessions never reach; the tree waited past its own reason.)
        Activity.Mastery => f.MasteryPointsEarned >= MasteryOpensAtPoints,

        // WHEN THERE IS SOMEWHERE TO GO: the first conquest opens the next region, and the Map is how
        // you travel there. Before that it is a chart with one door and five locks. (It was open from
        // the start for window-shopping; the journey brief closed it — see the class remarks.)
        Activity.Map => f.RegionsConquered >= 1,

        // The idle economy. STILL GATED on the first conquest while the Map and the Roster opened for
        // window-shopping, and deliberately so: the Warren's PRODUCTION is gated on this very rule
        // (Game1.TickWarren and the offline credit both ask it), so an always-open Warren would either
        // pay a brand-new player from minute zero — the exact 87%-of-all-income bug that gate exists
        // to close — or show a dashboard whose production numbers it refuses to pay, which is a lying
        // screen. It stays what it reads as: the reward for taking a region.
        Activity.Warren => f.RegionsConquered >= 1,

        // A collection screen with an empty collection teaches nothing. It opens on the first
        // characteristic the account awakens — which is also the moment the reveal fires, so the
        // screen exists exactly when the player has just been told there is something in it.
        Activity.Traits => f.TraitsDiscovered >= 1,

        // WHEN THERE IS SOMEONE TO MEET. The first conquest brings the second hunter, so the two
        // clauses coincide in the shipping game; the second is there so a quest-earned hunter can never
        // arrive on a save whose Roster is still shut (the lag this gate has shipped twice before).
        Activity.Roster => f.RegionsConquered >= 1 || f.CharactersUnlocked >= 2,

        // An out-of-range cast. Throwing rather than defaulting to true, because a gate that silently
        // opens is the failure this whole file exists to prevent.
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    /// <summary>One short line for a locked tile: what the player must do to open it.</summary>
    /// <remarks>
    /// A lock without a reason is just a closed door, and a closed door with no sign on it is the thing
    /// players actually resent. Every gate in this file states its own price.
    /// </remarks>
    public static string Requirement(Activity activity) => activity switch
    {
        Activity.Hunt => "",
        Activity.Training => "Clear three waves",
        Activity.Gear => "Find your first item",
        Activity.Vault => "Earn a chest from a boss",
        // BOTH CLAUSES, because the gate has two. It opens on a chest ever held OR two items owned,
        // and naming only the chest sent a player who already had the Forge open looking for a boss.
        Activity.Forge => "Earn a chest, or find two items",
        Activity.Build => "Learn a second skill, or find a keystone or a vow",
        Activity.Mastery => "Earn three mastery points by going deeper",
        Activity.Map => "Conquer a region",
        Activity.Warren => "Conquer a region",
        Activity.Traits => "Discover a characteristic by how you fight",
        Activity.Roster => "Conquer a region, and a second hunter joins",

        // A cast that is not a declared Activity is a programming error, and a blank string here would
        // reach the player as an empty panel instead of as the bug it is.
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    /// <summary>
    /// The one line the notice says when an activity has just opened: the NEED that opened it, and
    /// what to do about it. Past tense for the need, imperative for the use — the reveal is an answer
    /// to something the player just did, never an announcement.
    /// </summary>
    public static string OpenedLine(Activity activity) => activity switch
    {
        Activity.Hunt => "",
        Activity.Training => "Your waves paid Gleam. Spend it on your hunter's stats.",
        Activity.Gear => "An item dropped. Wear it, or read it first.",
        Activity.Vault => "A boss left a chest. Read it before you crack it.",
        Activity.Forge => "Crack chests, refine and reforge what you find.",
        Activity.Build => "You have a real choice now. Weave it.",
        Activity.Mastery => "Your depth paid mastery points. Spend them on the tree.",
        Activity.Map => "The next region is open. Travel there when you are ready.",
        Activity.Warren => "A conquered region works for you while you are away.",
        Activity.Traits => "How you fight woke a characteristic. Read what it does.",
        Activity.Roster => "Another hunter joined you. Meet them.",
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    /// <summary>The headline shown when an activity opens — what this thing IS, in one line.</summary>
    public static string Headline(Activity activity) => activity switch
    {
        Activity.Hunt => "THE HUNT",
        Activity.Training => "TRAINING — MAKE YOUR HUNTER STRONGER",
        Activity.Gear => "GEAR — WHAT YOU WEAR",
        Activity.Vault => "VAULT — CHESTS YOU HAVE NOT OPENED",
        Activity.Forge => "THE FORGE — WHERE ITEMS ARE MADE",
        Activity.Build => "THE BUILD — THE ACTUAL GAME",
        Activity.Mastery => "THE MASTERY TREE — HOW YOUR SKILLS BEHAVE",
        Activity.Map => "THE MAP — THE WORLD BEYOND",
        Activity.Warren => "THE WARREN — WORK THAT RUNS WITHOUT YOU",
        Activity.Traits => "TRAITS — CHARACTERISTICS YOU AWAKEN BY LIVING",
        Activity.Roster => "ROSTER — THE OTHER HUNTERS",

        // A cast that is not a declared Activity is a programming error, and a blank string here would
        // reach the player as an empty panel instead of as the bug it is.
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    /// <summary>
    /// How many skill slots are open. Starts at ONE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The game used to hand out four woven skills before the player had seen a fight.</b> Playtest:
    /// <i>"4 skill açık şekilde başladığımı gördüm. Skillerin açıklamaları yok, ne olduklarını
    /// anlamadım."</i> — started with four skills open, with no explanation of what they were. The old
    /// starter build had a reason (it showed off all four Forms at once) and the reason was wrong: four
    /// unexplained systems arriving simultaneously is not a demonstration, it is noise.
    /// </para>
    /// <para>
    /// So the champion starts with ONE skill, and each new slot arrives on its own, with its own line of
    /// explanation, at a moment when the player has already seen the last one fire. Four slots is where
    /// it STOPS, and four is the whole ladder: the trait tree used to sell a fifth, and it no longer
    /// does. A build is two skills that take an action and two that do not, and the fifth slot bought a
    /// THIRD action-taking skill — which pushed the demand on the champion's own swing back toward the
    /// number the slot rework existed to bring down.
    /// </para>
    /// <para>
    /// <b>A SLOT MUST NOT OPEN BEFORE THERE IS A SKILL FOR IT (2026-09-09).</b> Playtest: <i>"Four skill
    /// slots unlocked before the skills themselves were actually available."</i> The old ladder was
    /// keyed to depth and conquest alone — wave 5, wave 12, one conquest — and none of those facts pays
    /// for a skill. A fresh account can weave exactly ONE thing, its signature; every other skill is
    /// taught by a road on the mastery tree that costs <see cref="MasteryCatalog.SkillRoadCost"/> plus
    /// the trunk behind it, and depth pays points as <c>floor(0.9 · sqrt(depth))</c> per region. So all
    /// four slots stood open at wave 20 while three of them had been unfillable for another forty waves
    /// at least, and the BUILD screen greeted a first-time visitor with three congratulation banners in
    /// a row over a library of one usable skill.
    /// </para>
    /// <para>
    /// The gates are keyed to <see cref="UnlockFacts.MasteryPointsEarned"/> against the catalogue's own
    /// road-path costs, and the old depth conditions are KEPT beside them, so a slot needs both "the
    /// world could have taught you a skill" and "you have been that deep". Points are used rather than
    /// <see cref="UnlockFacts.SkillsKnown"/> because points only ever grow: a respec refunds a road and
    /// the skill leaves the known set, and a slot that could be taken away is a slot that can strand a
    /// woven build. The costs come from <see cref="MasteryCatalog.CheapestRoadPaths"/> rather than being
    /// typed here, so re-pricing a road moves these gates with it.
    /// </para>
    /// </remarks>
    public static int SkillSlots(UnlockFacts f)
    {
        var slots = 1;
        if (f.MasteryPointsEarned >= RoadPaths[0] && f.DeepestWave >= 5) slots++;
        if (f.MasteryPointsEarned >= RoadPaths[1] && f.DeepestWave >= 12) slots++;
        if (f.MasteryPointsEarned >= RoadPaths[2] && f.RegionsConquered >= 1) slots++;
        return slots;
    }

    /// <summary>What the world charges for a 1st, 2nd and 3rd taught skill — the slot ladder's prices.</summary>
    /// <remarks>
    /// A lower bound (the cheapest routes, reusing trunks already paid for), which is the honest
    /// direction for a gate: a player who spent elsewhere reaches a slot later, never sooner.
    /// </remarks>
    public static IReadOnlyList<int> RoadPaths { get; } = Builds.MasteryCatalog.CheapestRoadPaths(3);

    /// <summary>What the player must do for their next skill slot, or "" when all four are open.</summary>
    /// <remarks>
    /// Says the SCARCER of the two conditions, because naming the one already met would read as a
    /// gate that is stuck.
    /// </remarks>
    public static string NextSkillSlotNote(UnlockFacts f)
    {
        var slots = SkillSlots(f);
        if (slots >= 4) return "";
        var (points, depth, depthWord) = slots switch
        {
            1 => (RoadPaths[0], 5, "REACH WAVE 5"),
            2 => (RoadPaths[1], 12, "REACH WAVE 12"),
            _ => (RoadPaths[2], 0, "CONQUER A REGION"),
        };
        if (f.MasteryPointsEarned < points)
            return $"EARN {points} MASTERY POINTS AND LEARN A SKILL FOR A {Ordinal(slots + 1)} SKILL SLOT";
        return slots == 3 && f.RegionsConquered < 1
            ? $"{depthWord} FOR A FOURTH SKILL SLOT"
            : $"{depthWord} FOR A {Ordinal(slots + 1)} SKILL SLOT";

        static string Ordinal(int n) => n switch { 2 => "SECOND", 3 => "THIRD", _ => "FOURTH" };
    }

    // ── Build vocabulary: what the world hands you, and how much of it you may wear at once ─────
    //
    // Two capacities used to be sold on the trait tree — a second and a third keystone socket. Paying
    // for structural capacity is not an interesting choice (a player who spends a point on A BASIC
    // SOCKET has expressed nothing), so both moved here, into the file whose law is "derived, never
    // stored" and whose every fact only ever grows. Vow capacity is new: it was never a number at all,
    // only a side effect of how many skill slots a build happened to own.

    /// <summary>Conquests before a second keystone socket opens. Halfway across the world.</summary>
    public static int SecondSocketConquests { get; } = Encounters.Regions.All.Count / 2;   // 3

    /// <summary>Depth before the third keystone socket opens — four times the conquest line.</summary>
    public const int ThirdSocketWave = Encounters.Checkpoints.ConquestWave * 4;            // 80

    /// <summary>Conquests before a second vow may be sworn.</summary>
    public const int SecondVowConquests = 2;

    /// <summary>Conquests before a third vow may be sworn.</summary>
    public const int ThirdVowConquests = 4;

    /// <summary>
    /// How many keystone sockets are open. NONE until the world has taught you a keystone.
    /// </summary>
    /// <remarks>
    /// Socket one arrives on the same event as the first keystone — the first conquest — so the BUILD
    /// screen never shows an empty socket with nothing that could fill it, nor a keystone with nowhere
    /// to put it. Socket two waits until halfway across the world, which is the first moment there is a
    /// real second choice to make. Socket three is bought with DEPTH rather than breadth, so a player
    /// who has only walked the map wide still has two, and each of the game's two axes pays for one.
    /// </remarks>
    public static int KeystoneSockets(UnlockFacts f)
    {
        var sockets = 0;
        if (f.RegionsConquered >= 1) sockets++;
        if (f.RegionsConquered >= SecondSocketConquests) sockets++;
        if (f.DeepestWave >= ThirdSocketWave) sockets++;
        return sockets;   // never above Build.KeystoneSlots, which is 3
    }

    /// <summary>How many different vows the hunter may swear at once. One with the BUILD screen.</summary>
    /// <remarks>
    /// A vow is a promise about the BUILD, so how many promises you may make is a build-level number
    /// rather than a side effect of the skill-slot count. It arrives with the BUILD screen itself — the
    /// same wave-5 gate that opens the workbench a vow is sworn on — and grows on conquests two and
    /// four, interleaved with the keystone sockets above so that every conquest hands the build exactly
    /// one new thing.
    /// </remarks>
    public static int VowCapacity(UnlockFacts f)
    {
        if (f.DeepestWave < 5) return 0;
        var cap = 1;
        if (f.RegionsConquered >= SecondVowConquests) cap++;
        if (f.RegionsConquered >= ThirdVowConquests) cap++;
        return cap;
    }

    /// <summary>What the player must do for their next keystone socket, or "" when all three are open.</summary>
    public static string NextSocketNote(UnlockFacts f) => KeystoneSockets(f) switch
    {
        0 => "CONQUER A REGION TO OPEN YOUR FIRST KEYSTONE SOCKET",
        1 => $"CONQUER {SecondSocketConquests} REGIONS FOR A SECOND SOCKET",
        2 => $"REACH WAVE {ThirdSocketWave} FOR A THIRD SOCKET",
        _ => "",
    };

    /// <summary>What the player must do to be allowed one more vow, or "" at the last one.</summary>
    public static string NextVowNote(UnlockFacts f) => VowCapacity(f) switch
    {
        0 => "REACH WAVE 5 TO SWEAR YOUR FIRST VOW",
        1 => $"CONQUER {SecondVowConquests} REGIONS TO SWEAR A SECOND VOW",
        2 => $"CONQUER {ThirdVowConquests} REGIONS TO SWEAR A THIRD VOW",
        _ => "",
    };

    /// <summary>What opening a new skill slot should say, for the slot just gained.</summary>
    /// <remarks>
    /// <para>
    /// Indexed by the slot NUMBER rather than by a milestone, so the copy cannot drift out of step with
    /// <see cref="SkillSlots"/> when the gates are retuned.
    /// </para>
    /// <para>
    /// ONE SHORT LINE EACH. These were two-sentence paragraphs of advice — how timers interleave, what
    /// a SIGN is for, what a full build means — written when this note arrived at the end of the BUILD
    /// tour, with the screen already explained. It is a REVEAL now, and a reveal says what happened and
    /// gets out of the way; the advice belongs in the tour, which is still there behind LEARN THIS
    /// SCREEN, and in the skills' own readings. The slot number is kept because "another" is vaguer
    /// than "a third" for no gain.
    /// </para>
    /// </remarks>
    public static string SkillSlotNote(int slot) => slot switch
    {
        2 => "You can equip a second skill.",
        3 => "You can equip a third skill.",
        4 => "You can equip a fourth skill — a full build.",
        _ => "",
    };

    /// <summary>Every activity that is open to these facts, in unlock order.</summary>
    public static IReadOnlyList<Activity> Open(UnlockFacts f)
        => Enum.GetValues<Activity>().Where(a => IsOpen(a, f)).ToList();

    /// <summary>
    /// What is newly open, comparing where the player was against where they are.
    /// </summary>
    /// <remarks>
    /// The host keeps the previous facts and asks this each frame, so an unlock announces itself once,
    /// at the moment it happens, rather than being something the player has to notice on the rail.
    /// </remarks>
    public static IReadOnlyList<Activity> NewlyOpened(UnlockFacts before, UnlockFacts now)
        => Enum.GetValues<Activity>().Where(a => IsOpen(a, now) && !IsOpen(a, before)).ToList();
}
