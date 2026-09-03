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
    int TraitPointsEarned = 0);

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
    /// <summary>Is this activity open to the player yet?</summary>
    public static bool IsOpen(Activity activity, UnlockFacts f) => activity switch
    {
        // The game. If this is ever gated, the player is looking at a locked screen on launch.
        Activity.Hunt => true,

        // The first wave pays Gleam, so the first wave is when spending it becomes a real thought.
        Activity.Training => f.WavesCleared >= 1,

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

        // The build is the deepest system in the game and the least self-explanatory, so it waits until
        // the player has watched enough fights to have seen skills actually fire. Asking someone to
        // choose four skills before they have seen one go off is asking them to guess.
        Activity.Build => f.DeepestWave >= 5,

        // A BEAT AFTER THE BUILD, not with it. Mastery points arrive one per five waves of first-time
        // depth, so a player at wave 5 has exactly one and no sense yet of what a Form does. Opening the
        // tree at 8 means it arrives when there is something to spend and a fight to spend it on, and it
        // arrives SECOND — you choose your skills, then you shape how they behave.
        // Wave 25 — the fifth boss. It was wave 8; the tree is a mid-game workbench, not a first
        // errand (playtest 2026-08-25: "mastery is not a starting thing"), and MasteryPoints pays
        // four points by then, enough to walk in and buy something.
        Activity.Mastery => f.DeepestWave >= 25,

        // OPEN FROM THE START (playtest nine, item 11: "let the player look around"). It used to open
        // on the first conquest, but the Map already locks its regions individually INSIDE — so a new
        // player sees the whole six-region chain with exactly one door open, which teaches the shape
        // of the game better than a dimmed tile ever did. Window-shopping is the point: the internal
        // locks stay, and travelling anywhere still requires conquering the way there.
        Activity.Map => true,

        // The idle economy. STILL GATED on the first conquest while the Map and the Roster opened for
        // window-shopping, and deliberately so: the Warren's PRODUCTION is gated on this very rule
        // (Game1.TickWarren and the offline credit both ask it), so an always-open Warren would either
        // pay a brand-new player from minute zero — the exact 87%-of-all-income bug that gate exists
        // to close — or show a dashboard whose production numbers it refuses to pay, which is a lying
        // screen. It stays what it reads as: the reward for taking a region.
        Activity.Warren => f.RegionsConquered >= 1,

        // The permanent tree only means anything once there are points in it.
        Activity.Traits => f.TraitPointsEarned >= 1,

        // OPEN FROM THE START (playtest nine, item 11 — same reasoning as the Map). A fresh save
        // renders the starter champion plus the locked cast, and every locked card already explains
        // its own price, so the screen is a promise rather than a wall. (Its history is instructive:
        // the gate was `>= 2` while the first earned champion arrived on conquest ONE, then `>= 1` —
        // always one tuning pass behind the reward it gated. An always-open gallery cannot lag.)
        Activity.Roster => true,

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
        Activity.Training => "Clear your first wave",
        Activity.Gear => "Find your first item",
        Activity.Vault => "Earn a chest from a boss",
        Activity.Forge => "Earn a chest from a boss",
        Activity.Build => "Reach wave 5",
        Activity.Mastery => "Reach wave 25",
        // Open from the start, like the Hunt — nothing to require, so nothing to say. A price line on
        // a door that is never shut would read as a lock that opened early, i.e. as a bug.
        Activity.Map => "",
        Activity.Warren => "Conquer a region",
        Activity.Traits => "Earn a trait point",
        // Open from the start too (it was "Conquer a region" when the gate was; the caption follows
        // the rule — this file has already shipped one caption that drifted from its gate).
        Activity.Roster => "",

        // A cast that is not a declared Activity is a programming error, and a blank string here would
        // reach the player as an empty panel instead of as the bug it is.
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
        Activity.Traits => "TRAITS — PERMANENT BONUSES THAT NEVER RESET",
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
    /// </remarks>
    public static int SkillSlots(UnlockFacts f)
    {
        var slots = 1;
        if (f.DeepestWave >= 5) slots++;      // with the Build screen itself
        if (f.DeepestWave >= 12) slots++;
        if (f.RegionsConquered >= 1) slots++;
        return slots;
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
    /// Indexed by the slot NUMBER rather than by a milestone, so the copy cannot drift out of step with
    /// <see cref="SkillSlots"/> when the gates are retuned.
    /// </remarks>
    public static string SkillSlotNote(int slot) => slot switch
    {
        2 => "A SECOND SKILL. Each skill fires on its own timer, so what you pair matters — a slow "
             + "heavy hit beside a fast one fills the gap the heavy one leaves.",
        3 => "A THIRD SKILL. Room for a plan now: something to keep you alive, or a SIGN to make "
             + "the other two hit harder.",
        4 => "A FOURTH SKILL. A full build. Every skill and every style is open to you — the "
             + "build is now the main thing you are playing with.",
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
