using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Progression;

/// <summary>How loudly a lesson asks.</summary>
public enum LessonMode
{
    /// <summary>A remark on something that just happened. It asks for nothing and completes by itself.</summary>
    Observe,

    /// <summary>A prompt with a named deed. It points, and the player may ignore it.</summary>
    SoftGuide,

    /// <summary>
    /// A prompt that lights the REAL control and waits for the REAL deed.
    /// </summary>
    /// <remarks>
    /// A HardGuide is not a modal, a NEXT button, a tutorial-only copy of a control, or a prescribed
    /// build choice. It is the production control, lit, and a fact that only the production path can
    /// produce. It forces <b>make a choice</b> and never <b>which choice</b>: TRAIN ANY STAT ONCE,
    /// never TRAIN MIGHT.
    /// </remarks>
    HardGuide,
}

/// <summary>The authored lessons. Roughly twenty, typed, and named for the deed they teach.</summary>
public enum OnboardingLessonId
{
    /// <summary>The single least obvious thing about this game: the hunter fights without you.</summary>
    FirstFight,

    /// <summary>The signature skill firing for the first time — a remark, not a lesson.</summary>
    SignatureSeen,

    /// <summary>One rank of anything. The first decision the player ever makes.</summary>
    FirstTrainingPurchase,

    /// <summary>A boss arrived. Two words.</summary>
    FirstBoss,

    /// <summary>The welcome chest, which is deterministic — the critical path never waits on a drop roll.</summary>
    FirstChestOpen,

    /// <summary>Gear does nothing in the bag.</summary>
    FirstItemEquip,

    // ── THE CLIMAX: fail, diagnose, change. The loop this whole game is. The retry that follows a
    //    real change is automatic — the regroup timer sends the Hunter back down by itself — so it is
    //    never asked for; LessonFacts.RetriedAfterChange records that it happened, silently. ────────
    /// <summary>The first fall, and the report that says what stopped it.</summary>
    FirstFailureReport,

    /// <summary>Anything real: a stat, a skill, a node, a worn piece.</summary>
    FirstPostFailureChange,

    /// <summary>A point in the tree. Any node.</summary>
    FirstMasterySpend,

    /// <summary>A skill that is not the signature, in a slot.</summary>
    FirstSharedSkillEquip,

    /// <summary>The decision the BUILD screen hides best.</summary>
    FirstVariationChoice,

    /// <summary>A chapter ended. A remark, and then quiet.</summary>
    FirstRegionConquest,

    /// <summary>...and then exactly one lesson: go somewhere new.</summary>
    FirstRegionTravel,

    /// <summary>A keystone is a doctrine, and the first one arrives with the first conquest.</summary>
    FirstKeystoneChoice,

    /// <summary>The first socket is free.</summary>
    FirstGemSocket,

    /// <summary>Three at a time, and they are not bought.</summary>
    FirstTraitEquip,

    /// <summary>Somebody else joined.</summary>
    FirstHunterInteraction,

    /// <summary>Taught after the camp has actually paid, never before.</summary>
    FirstWarrenReturn,

    /// <summary>
    /// A letter is waiting in the envelope and the player has never opened the surface it waits on.
    /// </summary>
    /// <remarks>
    /// The one lesson about the shared chrome rather than a screen. It waits for REAL mail — there is
    /// nothing to teach about an empty inbox — and it is finished by the one deed that proves the
    /// player found it: opening DISPATCHES. Somebody who opened the envelope out of curiosity before
    /// the lesson could be chosen is never taught about it at all, which is the correct outcome.
    /// </remarks>
    FirstDispatchOpened,

    // ── THE BACK HALF (coverage pass, 2026-09-10). Six systems the game shipped with no voice at
    //    all: a player reached each of them and was told nothing about any of them. Every one is
    //    answered by a fact the account already proves, and every one waits for the moment it can
    //    actually be acted on — which for four of these is a long way in. ──────────────────────────

    // NO "OPEN THE FORGE" CARD, and that is a decision rather than an omission. The screen has four
    // jobs and a card naming them is the opening lecture this catalogue exists to replace — the rule
    // is already written down and already tested (onboarding_targets: no lesson marks ForgeTabs).
    // Each job is taught when it first matters instead: the gem by FirstGemSocket, which points at the
    // BAG where the gem is, and salvage by FirstAutoSell, which is the same idea without a screen.

    /// <summary>The camp taught the vault to sell, and the switch is still off.</summary>
    FirstAutoSell,

    /// <summary>A Vow is known and none is sworn — the one rule the player writes themselves.</summary>
    FirstVowSworn,

    /// <summary>A specialisation is affordable and none is taken. One per Hunter, forever.</summary>
    FirstSpecialisation,

    /// <summary>The stall has stock and nothing has been bought from it.</summary>
    FirstTraderVisit,

    /// <summary>The world is conquered and the taps have never been opened.</summary>
    FirstCorruptionOffer,
}

/// <summary>
/// Everything the lesson catalogue reads, and nothing it does not.
/// </summary>
/// <remarks>
/// <para>
/// Every field is a MONOTONE fact the save already proves, with four exceptions marked below that
/// cannot be reconstructed and are persisted. Defaults describe a brand-new save, so a caller naming
/// only what it means does not have to invent the rest.
/// </para>
/// <para>
/// This record is Core's whole contract with onboarding. It carries no time, no frames, no rectangles
/// and no presentation state: the catalogue answers "is this true yet", and the Game decides when and
/// how to say so.
/// </para>
/// </remarks>
public readonly record struct LessonFacts(
    int WavesCleared = 0,
    int DeepestWave = 0,
    long Gleam = 0,
    int StatsTrained = 0,
    int BossesFelled = 0,
    int ChestsHeld = 0,
    int ChestsOpened = 0,
    int ItemsOwned = 0,
    int ItemsWorn = 0,
    int GemsHeld = 0,
    /// <summary>
    /// Could a gem be set RIGHT NOW — the Forge's own answer, not a second copy of its rules.
    /// </summary>
    /// <remarks>
    /// <see cref="Economy.GemCraft.CanSocketNow"/> asks the real rule whether some loose gem fits some
    /// held item at a price this wallet can pay. HOLDING a gem is not the same question: a first gem
    /// arrives long before a Rare item does, and RARE AND BETTER GEAR CARRIES SOCKETS is what the
    /// Forge says to everybody else. This exists so SOCKET YOUR FIRST GEM is never shown to a player
    /// the Forge would refuse.
    /// </remarks>
    bool CanSocketNow = false,
    int GemsSet = 0,
    int MasterySpent = 0,
    int MasteryPointsFree = 0,
    int SharedSkillsEquipped = 0,
    int SharedSkillsAvailable = 0,
    int EmptySkillSlots = 0,
    int VariationsChosen = 0,
    int VariationsOffered = 0,
    int KeystonesDiscovered = 0,
    int KeystonesWorn = 0,
    int RegionsConquered = 0,
    int RegionsOpen = 0,
    int RegionsEntered = 0,
    int TraitsDiscovered = 0,
    int TraitsEquipped = 0,
    int HuntersOwned = 0,
    int HunterSwitches = 0,
    bool WarrenOpen = false,
    /// <summary>A real, non-zero offline payout has landed. The only honest moment to teach the camp.</summary>
    bool WarrenPaidOffline = false,
    bool WarrenVisited = false,
    /// <summary>Falls so far. One is what makes the report lesson eligible at all.</summary>
    int Falls = 0,

    // ── THE FOUR THAT CANNOT BE DERIVED. Persisted; the first three seeded for old saves. ────────
    /// <summary>The player has opened the run log at least once, ever.</summary>
    bool ReportOpenedEver = false,
    /// <summary>A real build/training/gear/mastery change was made after the first fall.</summary>
    bool ChangedAfterFall = false,
    /// <summary>A descent was started after that change.</summary>
    bool RetriedAfterChange = false,

    /// <summary>The global SKIP GUIDANCE setting. Silences presentation; changes no gameplay fact.</summary>
    bool GuidanceOff = false,

    // ── THE BACK HALF'S FACTS. Each pair is "can this be done" and "has it been done". ───────────

    /// <summary>Has the camp reached the facility level that lets the vault sell for you?</summary>
    bool AutoSellUnlocked = false,
    /// <summary>...and is the switch on? The facility unlocks it; the player turns it on.</summary>
    bool AutoSellOn = false,

    /// <summary>Vows the account has met.</summary>
    int VowsKnown = 0,
    /// <summary>Vows sworn on the woven build right now.</summary>
    int VowsSworn = 0,

    /// <summary>Can a Specialisation be taken THIS INSTANT — the tree's own CanTake, not a guess?</summary>
    bool SpecialisationReachable = false,
    /// <summary>Has this Hunter chosen its discipline? One per Hunter, and it does not come back.</summary>
    bool SpecialisationTaken = false,

    /// <summary>Does the stall have anything on it this week?</summary>
    bool TraderStocked = false,
    /// <summary>How many of its slots have been bought.</summary>
    int TraderBought = 0,

    /// <summary>Every region conquered and the world not yet deepened — the valve can be opened.</summary>
    bool CanDeepenWorld = false,
    /// <summary>How far it has been opened.</summary>
    int CorruptionTier = 0,

    // ── THE ACCOUNT'S OWN NEWS. One pair again: is there something to open, and has it been opened. ──

    /// <summary>Letters waiting in the inbox. One is what makes the dispatch lesson eligible at all.</summary>
    int DispatchesUnread = 0,
    /// <summary>
    /// The player has opened the DISPATCHES surface at least once, ever.
    /// </summary>
    /// <remarks>
    /// The fifth fact that cannot be derived, and persisted for the same reason the other four are:
    /// nothing else in the account proves that somebody looked. NOT "has read a letter" — the deed the
    /// lesson asks for is finding the surface, and a player who opened it and read nothing has found it.
    /// </remarks>
    bool DispatchesOpenedEver = false);

/// <summary>
/// THE LESSON CATALOGUE: what is true, what is actionable, and which one matters most.
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaced a FIFO ladder, and the reason is structural.</b> The old guide walked an ordered
/// array and showed the first unsatisfied rung, so one rung nobody could act on stopped every rung
/// behind it: a gem lesson with no gem in the bag silenced the conquest lesson, the report lesson and
/// the mastery lesson, none of which have anything to do with gems. Reordering the array only moves
/// which lesson does the blocking. The fix is that lessons become <b>independently eligible</b> and
/// one is chosen by <b>priority</b>.
/// </para>
/// <para>
/// <b>Completion is a FACT, never a dismissal.</b> Closing a card silences presentation; it does not
/// socket a gem. Every <see cref="Completed"/> arm reads something the player actually produced, so a
/// lesson performed before it was ever shown is complete and never appears — and a save that loads
/// mid-lesson reconstructs its own state with no stored machine.
/// </para>
/// <para>
/// <b>What the game teaches is the loop, not the menu.</b> Observe, ask why it stopped, change one
/// thing, try again, see if it went deeper. That is why the two fall lessons outrank everything: the
/// strongest first-session milestone is not "finished the tutorial", it is
/// <i>opened the report, changed something, and went back down</i> — and the third step is never
/// asked for, only recorded.
/// </para>
/// </remarks>
public static class OnboardingLessons
{
    /// <summary>Every lesson, in declaration order — the tie-break when two share a priority.</summary>
    public static readonly IReadOnlyList<OnboardingLessonId> All =
        Enum.GetValues<OnboardingLessonId>();

    /// <summary>What the first rank of any stat costs. Mirrors <c>ProgressionTuning.BaseTrainingCost</c>.</summary>
    /// <remarks>
    /// Pinned by a test against the real tuning. The old guide guessed 20 against a real 25 and spent
    /// five Gleam telling the player to buy something every row refused — the exact
    /// prompt-that-cannot-be-obeyed this file forbids.
    /// </remarks>
    public const int FirstRankCost = 25;

    /// <summary>Waves between bosses. Mirrors <c>ExpeditionTuning.BossEvery</c>, pinned by a test.</summary>
    public const int BossEvery = 5;

    // ── COMPLETION ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Has the player actually done this? Facts only — a closed card completes nothing.</summary>
    public static bool Completed(OnboardingLessonId id, LessonFacts f) => id switch
    {
        OnboardingLessonId.FirstFight => f.WavesCleared >= 1,
        OnboardingLessonId.SignatureSeen => f.WavesCleared >= 1,
        OnboardingLessonId.FirstTrainingPurchase => f.StatsTrained >= 1,
        OnboardingLessonId.FirstBoss => f.BossesFelled >= 1 || f.DeepestWave > BossEvery,
        // THE DEED IS OPENING A CHEST, so that is the only thing that completes it. It used to accept
        // "owns an item" and "wears an item" as proxies, which are not the same deed and are reachable
        // without ever touching the VAULT — a trader purchase, a share code, a fixture, a seeded save.
        // A save that genuinely predates the counter is handled in migration, never by loosening this.
        OnboardingLessonId.FirstChestOpen => f.ChestsOpened >= 1,
        OnboardingLessonId.FirstItemEquip => f.ItemsWorn >= 1,

        OnboardingLessonId.FirstFailureReport => f.ReportOpenedEver,
        OnboardingLessonId.FirstPostFailureChange => f.ChangedAfterFall,

        OnboardingLessonId.FirstMasterySpend => f.MasterySpent >= 1,
        OnboardingLessonId.FirstSharedSkillEquip => f.SharedSkillsEquipped >= 1,
        OnboardingLessonId.FirstVariationChoice => f.VariationsChosen >= 1,

        OnboardingLessonId.FirstRegionConquest => f.RegionsConquered >= 1,
        // ENTERED, not merely open: the deed is arriving somewhere else, and a second region that is
        // available but never visited has taught nothing.
        OnboardingLessonId.FirstRegionTravel => f.RegionsEntered >= 2,
        OnboardingLessonId.FirstKeystoneChoice => f.KeystonesWorn >= 1,

        OnboardingLessonId.FirstGemSocket => f.GemsSet >= 1,
        OnboardingLessonId.FirstTraitEquip => f.TraitsEquipped >= 1,
        OnboardingLessonId.FirstHunterInteraction => f.HunterSwitches >= 1,
        OnboardingLessonId.FirstWarrenReturn => f.WarrenVisited,
        OnboardingLessonId.FirstDispatchOpened => f.DispatchesOpenedEver,

        // THE BACK HALF. Each one is finished by the deed itself, never by the card being closed.
        OnboardingLessonId.FirstAutoSell => f.AutoSellOn,
        OnboardingLessonId.FirstVowSworn => f.VowsSworn >= 1,
        OnboardingLessonId.FirstSpecialisation => f.SpecialisationTaken,
        OnboardingLessonId.FirstTraderVisit => f.TraderBought >= 1,
        OnboardingLessonId.FirstCorruptionOffer => f.CorruptionTier >= 1,
        _ => true,
    };

    // ── ELIGIBILITY ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Can the player act on this right now?
    /// </summary>
    /// <remarks>
    /// A prompt that arrives before it can be obeyed is noise, and noise teaches a player to stop
    /// reading prompts — after which the one that matters is invisible too. So eligibility asks for
    /// everything the deed needs: the resource, the screen's own gate, and the moment.
    /// </remarks>
    public static bool Eligible(OnboardingLessonId id, LessonFacts f) => id switch
    {
        // The first thing said, and it is said before anything has happened.
        OnboardingLessonId.FirstFight => true,
        OnboardingLessonId.SignatureSeen => false,   // fired by the fight, not chosen by the director

        OnboardingLessonId.FirstTrainingPurchase =>
            f.WavesCleared >= Unlocks.TrainingOpensAtWaves && f.Gleam >= FirstRankCost,

        OnboardingLessonId.FirstBoss => false,       // an Observe beat the arena raises; see the note below
        // ── THE OPENING IS AN ORDER, NOT A RACE. ────────────────────────────────────────────────
        //
        // The welcome chest used to sit in a new account's vault from frame one, so on priority alone
        // the chest lesson won the moment the fight lesson was satisfied — at wave 1, two waves before
        // TRAINING even opens. The journey came out as FIGHT -> OPEN CHEST -> EQUIP -> TRAIN, which
        // teaches the loot loop before the decision loop and leaves the one screen the whole game is
        // built on until last. The intended order is FIGHT -> TRAIN -> OPEN -> EQUIP: watch it fight,
        // make one decision, then be paid for it.
        //
        // THAT PREMISE IS GONE (2026-09-10). The gift comes from the tutorial boss now, so no chest
        // can exist before wave five and the inversion cannot happen by that route at all. These two
        // gates stay because a chest can still arrive EARLY BY ANOTHER ROUTE — a rolled boss drop, a
        // shared code, a trader purchase — and because a player who ends the tutorial in Settings on
        // their first minute should still meet the game in this order. They gate nothing: the VAULT
        // opens on a chest and its tile carries the count, so anyone who opens one early simply
        // completes the lesson without ever being asked.
        OnboardingLessonId.FirstChestOpen => f.ChestsHeld >= 1 && f.StatsTrained >= 1,
        // ...AND SO DOES THE ITEM. Gating only the chest left EQUIP ONE ITEM (78) outranking TRAIN ANY
        // STAT ONCE (76), and the journey came out FIGHT -> EQUIP -> TRAIN — the same inversion,
        // reached by a different route.
        OnboardingLessonId.FirstItemEquip => f.ItemsOwned >= 1 && f.StatsTrained >= 1,

        // ── THE FALL LOOP'S TWO PRESENTED LESSONS. Each waits on the one before it. ────────────────
        OnboardingLessonId.FirstFailureReport => f.Falls >= 1,
        OnboardingLessonId.FirstPostFailureChange => f.Falls >= 1 && f.ReportOpenedEver,

        OnboardingLessonId.FirstMasterySpend => f.MasteryPointsFree >= 1,
        // AVAILABLE means the tree reaches it NOW. Mastery access is not permanent: a respec that
        // gives the road back takes the skill with it, so this asks the live count.
        OnboardingLessonId.FirstSharedSkillEquip => f.SharedSkillsAvailable >= 1 && f.EmptySkillSlots >= 1,
        OnboardingLessonId.FirstVariationChoice => f.VariationsOffered >= 1,

        OnboardingLessonId.FirstRegionConquest => false,   // an Observe beat the conquest raises
        OnboardingLessonId.FirstRegionTravel => f.RegionsConquered >= 1 && f.RegionsOpen >= 2,
        OnboardingLessonId.FirstKeystoneChoice => f.KeystonesDiscovered >= 1,

        // NOT MERELY HOLDING ONE. The lesson's words are SOCKET YOUR FIRST GEM, so the deed has to be
        // performable at the instant it is asked for: a loose gem, an item that gem legally fits, and
        // the price. The Forge answers all three (GemCraft.CanSocketNow) — asking here would be a
        // second set of socket rules, free to drift from the one the SET button obeys.
        OnboardingLessonId.FirstGemSocket => f.CanSocketNow,
        OnboardingLessonId.FirstTraitEquip => f.TraitsDiscovered >= 1,
        OnboardingLessonId.FirstHunterInteraction => f.HuntersOwned >= 2,
        // NEVER BEFORE THE CAMP HAS PAID. A Warren tour given to somebody the Warren has done nothing
        // for is a theory; given after an offline payout it answers a question they already have.
        OnboardingLessonId.FirstWarrenReturn => f.WarrenOpen && f.WarrenPaidOffline,
        // NEVER ABOUT AN EMPTY INBOX. The envelope is in the chrome from the first frame and says
        // nothing while there is nothing in it; the lesson arrives with the first real letter, which
        // is the only moment "messages wait here" is a fact rather than a promise.
        OnboardingLessonId.FirstDispatchOpened => f.DispatchesUnread >= 1,

        // ── THE BACK HALF. Every one of these waits on the thing it asks for EXISTING. ──────────
        //
        // A prompt that arrives before it can be obeyed is noise, and the back half is where that is
        // easiest to get wrong: these systems open long after the player has learned to trust the
        // prompts, so one that cannot be acted on costs more here than it would have in minute two.

        // AUTO-SELL is a facility's permission and a player's decision, and it is offered only once
        // the pile it would thin actually exists. Never while the bag is small: selling from an empty
        // bag teaches nothing and risks the one item they own.
        OnboardingLessonId.FirstAutoSell => f.AutoSellUnlocked && f.ItemsOwned >= 8,

        // A VOW is the only rule the player writes for themselves, and it needs a skill to write it
        // on — a build with one woven skill has nowhere to put one.
        OnboardingLessonId.FirstVowSworn => f.VowsKnown >= 1 && f.SharedSkillsEquipped >= 1,

        // A SPECIALISATION is asked for by the tree, not by a threshold: the catalogue is consulted
        // for a node that can be taken at this instant, so the card cannot arrive one point early.
        OnboardingLessonId.FirstSpecialisation => f.SpecialisationReachable,

        // THE STALL is weekly and its stock is real: no stock, no card.
        OnboardingLessonId.FirstTraderVisit => f.TraderStocked && f.Gleam >= 1,

        // THE VALVE only exists once the whole world is held, which is the point of it.
        OnboardingLessonId.FirstCorruptionOffer => f.CanDeepenWorld,
        _ => false,
    };

    // ── PRIORITY ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Which lesson wins when several are eligible. Higher is louder; ties break on declaration order.
    /// </summary>
    /// <remarks>
    /// The two fall lessons are the top band because they are the game's actual loop and the strongest
    /// first-session milestone there is. The opening beats come next because a player who cannot get
    /// started has no loop to learn. Everything contextual sits at the bottom, where it can wait
    /// without blocking anything — which is the whole point of replacing the ladder.
    /// </remarks>
    public static int Priority(OnboardingLessonId id) => id switch
    {
        OnboardingLessonId.FirstFailureReport => 100,
        OnboardingLessonId.FirstPostFailureChange => 99,

        OnboardingLessonId.FirstFight => 90,
        OnboardingLessonId.FirstChestOpen => 80,
        OnboardingLessonId.FirstItemEquip => 78,
        OnboardingLessonId.FirstTrainingPurchase => 76,

        OnboardingLessonId.FirstMasterySpend => 70,
        OnboardingLessonId.FirstSharedSkillEquip => 68,
        OnboardingLessonId.FirstVariationChoice => 66,

        // TRAVEL IS THE ONE POST-CONQUEST LESSON. Conquest opens the map, the camp, the roster and
        // often the first keystone all at once; every one of them is AVAILABLE and only this one is
        // taught. The rest wait below, for the screen the player walks into on their own.
        OnboardingLessonId.FirstRegionTravel => 60,

        OnboardingLessonId.FirstKeystoneChoice => 40,
        OnboardingLessonId.FirstGemSocket => 38,
        OnboardingLessonId.FirstTraitEquip => 36,
        OnboardingLessonId.FirstWarrenReturn => 30,
        // BELOW THE WARREN AND ABOVE THE BACK HALF. A letter can wait — it is the one thing in the
        // game that is explicitly designed to — so it never wins over a lesson the player is in the
        // middle of; but it arrives in the first hour, long before the trader or the valve.
        OnboardingLessonId.FirstDispatchOpened => 28,
        OnboardingLessonId.FirstHunterInteraction => 20,

        // ── THE BACK HALF RANKS BELOW EVERYTHING THE FIRST HOUR TEACHES. ────────────────────────
        //
        // Not because it matters less, but because it arrives later and must never outrank a lesson
        // the player is in the middle of. Within the band the order is how soon each becomes true:
        // the FORGE opens on the third item, auto-sell on a facility level, a vow on a woven skill,
        // a specialisation deep in the tree, the stall on a week, and the valve on a whole world.
        OnboardingLessonId.FirstAutoSell => 26,
        OnboardingLessonId.FirstVowSworn => 24,
        OnboardingLessonId.FirstSpecialisation => 22,
        OnboardingLessonId.FirstTraderVisit => 16,
        OnboardingLessonId.FirstCorruptionOffer => 14,
        _ => 0,
    };

    // ── PRESENTATION SHAPE ───────────────────────────────────────────────────────────────────────

    public static LessonMode Mode(OnboardingLessonId id) => id switch
    {
        OnboardingLessonId.FirstFight => LessonMode.Observe,
        OnboardingLessonId.SignatureSeen => LessonMode.Observe,
        OnboardingLessonId.FirstBoss => LessonMode.Observe,
        OnboardingLessonId.FirstRegionConquest => LessonMode.Observe,

        OnboardingLessonId.FirstHunterInteraction => LessonMode.SoftGuide,
        OnboardingLessonId.FirstTraitEquip => LessonMode.SoftGuide,
        OnboardingLessonId.FirstWarrenReturn => LessonMode.SoftGuide,
        OnboardingLessonId.FirstDispatchOpened => LessonMode.SoftGuide,
        OnboardingLessonId.FirstKeystoneChoice => LessonMode.SoftGuide,
        OnboardingLessonId.FirstRegionTravel => LessonMode.SoftGuide,

        // THE BACK HALF IS SOFT, ALL OF IT. These are offers rather than instructions — a player who
        // has held a whole region does not need a bracket round a button, and four of the six are
        // decisions (a Vow, a discipline, a purchase, the valve) that it would be wrong to push.
        OnboardingLessonId.FirstAutoSell => LessonMode.SoftGuide,
        OnboardingLessonId.FirstVowSworn => LessonMode.SoftGuide,
        OnboardingLessonId.FirstSpecialisation => LessonMode.SoftGuide,
        OnboardingLessonId.FirstTraderVisit => LessonMode.SoftGuide,
        OnboardingLessonId.FirstCorruptionOffer => LessonMode.SoftGuide,

        _ => LessonMode.HardGuide,
    };

    /// <summary>The screen the deed is done on, or null when it is about the fight itself.</summary>
    public static Activity? Sends(OnboardingLessonId id) => id switch
    {
        OnboardingLessonId.FirstTrainingPurchase => Activity.Training,
        OnboardingLessonId.FirstChestOpen => Activity.Vault,
        OnboardingLessonId.FirstItemEquip => Activity.Gear,
        OnboardingLessonId.FirstMasterySpend => Activity.Mastery,
        OnboardingLessonId.FirstSharedSkillEquip => Activity.Build,
        OnboardingLessonId.FirstVariationChoice => Activity.Build,
        OnboardingLessonId.FirstKeystoneChoice => Activity.Build,
        OnboardingLessonId.FirstRegionTravel => Activity.Map,
        OnboardingLessonId.FirstGemSocket => Activity.Forge,
        OnboardingLessonId.FirstTraitEquip => Activity.Traits,
        OnboardingLessonId.FirstHunterInteraction => Activity.Roster,
        OnboardingLessonId.FirstWarrenReturn => Activity.Warren,

        OnboardingLessonId.FirstAutoSell => Activity.Vault,
        OnboardingLessonId.FirstVowSworn => Activity.Build,
        OnboardingLessonId.FirstSpecialisation => Activity.Mastery,
        OnboardingLessonId.FirstTraderVisit => Activity.Vault,
        OnboardingLessonId.FirstCorruptionOffer => Activity.Map,
        _ => null,
    };

    /// <summary>
    /// Which production control the guide lights, or null when the lesson points at nothing.
    /// </summary>
    /// <remarks>
    /// A semantic name, resolved to a rectangle by the screen that owns it — the same resolver the
    /// screen tours use. Nothing here knows a pixel, so the light lands correctly at every UI density.
    /// </remarks>
    public static TourTarget? Target(OnboardingLessonId id) => id switch
    {
        // ── EVERY LESSON NAMES SOMETHING TO LIGHT. ──────────────────────────────────────────────
        //
        // Playtest 2026-09-10: "the message box at the top does not attract attention, is not read and
        // is not taken seriously — act as if it does not exist, or do not put important things there."
        // So nothing important lives there any more: the host darkens the screen and cuts a hole
        // around what the lesson is about, and the copy rides beside the hole. That only works if
        // there is a hole, so the four OBSERVE beats name their subject too — they ask for nothing,
        // but they are ABOUT something, and a remark with a light is read where a toast is not.
        OnboardingLessonId.FirstFight => TourTarget.Champion,
        OnboardingLessonId.SignatureSeen => TourTarget.Skills,
        OnboardingLessonId.FirstBoss => TourTarget.Enemies,
        OnboardingLessonId.FirstRegionConquest => TourTarget.Enemies,

        // THE FALL LOOP'S TWO LESSONS keep their null Sends — the fight itself is where they belong,
        // and the host reads a null Sends as "the HUNT" when it resolves these lights.
        OnboardingLessonId.FirstFailureReport => TourTarget.LogButton,
        // MAKE ONE CHANGE is the one instruction that names no single control: a change is a rank, a
        // worn piece, a node or the weave, on four different screens. The rail is where all four are.
        OnboardingLessonId.FirstPostFailureChange => TourTarget.NavRail,
        OnboardingLessonId.FirstTrainingPurchase => TourTarget.TrainingRows,
        OnboardingLessonId.FirstChestOpen => TourTarget.ChestCards,
        OnboardingLessonId.FirstItemEquip => TourTarget.Inventory,
        OnboardingLessonId.FirstMasterySpend => TourTarget.MasteryTree,
        OnboardingLessonId.FirstSharedSkillEquip => TourTarget.SkillPicker,
        OnboardingLessonId.FirstVariationChoice => TourTarget.SkillSlots,
        OnboardingLessonId.FirstKeystoneChoice => TourTarget.Vows,
        OnboardingLessonId.FirstRegionTravel => TourTarget.RegionChain,
        OnboardingLessonId.FirstGemSocket => TourTarget.Bag,
        OnboardingLessonId.FirstTraitEquip => TourTarget.TraitCollection,
        OnboardingLessonId.FirstHunterInteraction => TourTarget.ChampionCards,
        OnboardingLessonId.FirstWarrenReturn => TourTarget.Facilities,
        // THE ENVELOPE IN THE SHARED CHROME, and so no Sends: the control is already beside the
        // player's cursor on whatever screen they are standing on, and sending them somewhere to
        // press it would be a lie about where it lives.
        OnboardingLessonId.FirstDispatchOpened => TourTarget.DispatchIcon,

        OnboardingLessonId.FirstAutoSell => TourTarget.VaultButtons,
        OnboardingLessonId.FirstVowSworn => TourTarget.Vows,
        OnboardingLessonId.FirstSpecialisation => TourTarget.Specialisations,
        OnboardingLessonId.FirstTraderVisit => TourTarget.VaultButtons,
        OnboardingLessonId.FirstCorruptionOffer => TourTarget.RegionChain,
        _ => null,
    };

    // ── COPY. Two to five words of title, three to eight of action, and a second line only when it
    //    answers "why should I care". Never a paragraph: this is a prompt, not documentation. ──────

    public static string Title(OnboardingLessonId id) => id switch
    {
        OnboardingLessonId.FirstFight => "YOUR HUNTER FIGHTS FOR YOU",
        OnboardingLessonId.SignatureSeen => "SIGNATURE",
        OnboardingLessonId.FirstTrainingPurchase => "TRAINING",
        OnboardingLessonId.FirstBoss => "BOSS WAVE",
        OnboardingLessonId.FirstChestOpen => "A CHEST IS WAITING",
        OnboardingLessonId.FirstItemEquip => "SOMETHING DROPPED",
        OnboardingLessonId.FirstFailureReport => "IT FELL",
        OnboardingLessonId.FirstPostFailureChange => "THIS STOPPED THE RUN",
        OnboardingLessonId.FirstMasterySpend => "MASTERY",
        OnboardingLessonId.FirstSharedSkillEquip => "A SKILL IS OPEN",
        OnboardingLessonId.FirstVariationChoice => "A LEVEL TO SPEND",
        OnboardingLessonId.FirstRegionConquest => "REGION CONQUERED",
        OnboardingLessonId.FirstRegionTravel => "THE WORLD IS WIDER",
        OnboardingLessonId.FirstKeystoneChoice => "A KEYSTONE IS YOURS",
        OnboardingLessonId.FirstGemSocket => "A GEM",
        OnboardingLessonId.FirstTraitEquip => "A TRAIT HAS AWAKENED",
        OnboardingLessonId.FirstHunterInteraction => "A NEW HUNTER HAS JOINED",
        OnboardingLessonId.FirstWarrenReturn => "THE WARREN WORKED",
        OnboardingLessonId.FirstDispatchOpened => "A DISPATCH ARRIVED",

        OnboardingLessonId.FirstAutoSell => "IT CAN SELL FOR YOU",
        OnboardingLessonId.FirstVowSworn => "A VOW IS A RULE",
        OnboardingLessonId.FirstSpecialisation => "CHOOSE A DISCIPLINE",
        OnboardingLessonId.FirstTraderVisit => "SOMEBODY IS SELLING",
        OnboardingLessonId.FirstCorruptionOffer => "THE WORLD IS YOURS",
        _ => "",
    };

    /// <summary>The deed, in the imperative. Empty for an Observe lesson, which asks for nothing.</summary>
    public static string Action(OnboardingLessonId id) => id switch
    {
        OnboardingLessonId.FirstTrainingPurchase => "TRAIN ANY STAT ONCE",
        OnboardingLessonId.FirstChestOpen => "OPEN THE GIFT CHEST",
        OnboardingLessonId.FirstItemEquip => "EQUIP ONE ITEM",
        OnboardingLessonId.FirstFailureReport => "READ THE LOG",
        OnboardingLessonId.FirstPostFailureChange => "MAKE ONE CHANGE",
        OnboardingLessonId.FirstMasterySpend => "SPEND ONE MASTERY POINT",
        OnboardingLessonId.FirstSharedSkillEquip => "EQUIP ONE SHARED SKILL",
        OnboardingLessonId.FirstVariationChoice => "CHOOSE A VARIATION",
        OnboardingLessonId.FirstRegionTravel => "TRAVEL TO THE NEXT REGION",
        OnboardingLessonId.FirstKeystoneChoice => "CHOOSE A KEYSTONE",
        OnboardingLessonId.FirstGemSocket => "SOCKET YOUR FIRST GEM",
        OnboardingLessonId.FirstTraitEquip => "CHOOSE ONE TRAIT",
        OnboardingLessonId.FirstHunterInteraction => "TRY ANOTHER HUNTER",
        OnboardingLessonId.FirstWarrenReturn => "OPEN WARREN",
        OnboardingLessonId.FirstDispatchOpened => "OPEN DISPATCHES",

        OnboardingLessonId.FirstAutoSell => "TURN ON AUTO-SELL",
        OnboardingLessonId.FirstVowSworn => "SWEAR ONE VOW",
        OnboardingLessonId.FirstSpecialisation => "TAKE ONE SPECIALISATION",
        OnboardingLessonId.FirstTraderVisit => "LOOK AT THE STALL",
        OnboardingLessonId.FirstCorruptionOffer => "DEEPEN THE WORLD",
        _ => "",
    };

    /// <summary>One short line, and only where it answers "why should I care?". Usually nothing.</summary>
    public static string Why(OnboardingLessonId id) => id switch
    {
        // THE FOUR OBSERVE BEATS ASK FOR NOTHING, so their second line is all they have to say. Each
        // names the thing on screen at the moment it is on screen, and then goes.
        OnboardingLessonId.FirstFight => "It fights on while you are away.",
        OnboardingLessonId.SignatureSeen => "Unique to this Hunter.",
        OnboardingLessonId.FirstBoss => "Every fifth wave. Harder, richer.",
        OnboardingLessonId.FirstRegionConquest => "The next region is open.",
        OnboardingLessonId.FirstItemEquip => "The fight only reads what is worn.",
        OnboardingLessonId.FirstGemSocket => "Your first socket is free.",
        OnboardingLessonId.FirstWarrenReturn => "It paid while you were away.",
        OnboardingLessonId.FirstDispatchOpened => "Messages wait here.",

        OnboardingLessonId.FirstAutoSell => "Junk becomes materials on its own.",
        OnboardingLessonId.FirstVowSworn => "A rule a skill keeps, and pays for.",
        OnboardingLessonId.FirstSpecialisation => "One per Hunter. It never comes back.",
        OnboardingLessonId.FirstTraderVisit => "New stock every week.",
        OnboardingLessonId.FirstCorruptionOffer => "Harder and richer. You can close it.",
        _ => "",
    };

    // ── SELECTION ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The one lesson to present, or null for silence.
    /// </summary>
    /// <param name="f">Everything the player has done.</param>
    /// <param name="suppressed">
    /// Lessons presentation is currently muting — a quiet period after a reward, a card the player
    /// closed. Purely a display filter: nothing here fakes a fact, so a suppressed lesson is still
    /// incomplete and will be offered again when the mute lifts.
    /// </param>
    /// <remarks>
    /// AT MOST ONE, ALWAYS. Two prompts at once is how a player learns to read neither, and the first
    /// conquest can make four systems available in the same frame.
    /// </remarks>
    public static OnboardingLessonId? Next(LessonFacts f, IReadOnlyCollection<OnboardingLessonId>? suppressed = null)
    {
        // GLOBAL SKIP SILENCES PRESENTATION AND NOTHING ELSE. No fact is fabricated, no gate moves, no
        // reward is granted: an experienced player simply plays, and every lesson stays honestly
        // incomplete underneath.
        if (f.GuidanceOff) return null;

        OnboardingLessonId? best = null;
        var bestPriority = int.MinValue;
        foreach (var id in All)
        {
            if (Completed(id, f) || !Eligible(id, f)) continue;
            if (suppressed is not null && suppressed.Contains(id)) continue;
            var p = Priority(id);
            if (p <= bestPriority) continue;   // ties keep the earlier declaration
            bestPriority = p;
            best = id;
        }
        return best;
    }

    /// <summary>Every lesson that is currently actionable — for tests and telemetry, not for display.</summary>
    public static IReadOnlyList<OnboardingLessonId> EligibleNow(LessonFacts f)
        => All.Where(id => !Completed(id, f) && Eligible(id, f)).ToList();

    // ── MIGRATION ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// THE FIRST SAVE VERSION WHOSE FILES CARRY THE THREE FALL-LOOP FACTS. Frozen forever.
    /// </summary>
    /// <remarks>
    /// A literal, never <c>SaveGame.CurrentVersion</c>. Written against CurrentVersion this would
    /// re-open the bug it closes on the very next bump: at version 6, every version-5 file — which
    /// does carry the facts, honestly — would start being seeded again, and every player who had not
    /// yet lived the loop would silently be marked as having lived it. The number describes a moment
    /// in this project's history and that moment does not move.
    /// </remarks>
    public const int FirstVersionWithFallLoopFacts = 5;

    /// <summary>
    /// Should a returning save be treated as having already lived the fall loop?
    /// </summary>
    /// <param name="f">The player's progression, as corroboration.</param>
    /// <param name="fileVersion">The Version of the file that was LOADED — not the build's.</param>
    /// <remarks>
    /// <para>
    /// A save written before the three fall-loop facts existed carries none of them, and a player who
    /// has conquered a region has fallen, read or ignored the log, changed their build and gone again
    /// many times over — replaying READ THE LOG at them would be the game announcing it has not been
    /// watching. So an OLD file with real progression behind it is seeded as having done all three.
    /// </para>
    /// <para>
    /// <b>THE VERSION IS THE TEST; the progression is only the corroboration.</b> This asked the
    /// progression alone, and "high progress, ReportOpenedEver false" describes two different players:
    /// the veteran whose file predates the field, and a brand-new player on this build who conquered a
    /// region before their first fall and then reloaded. The second one was being handed the veteran's
    /// answer and losing the lesson the whole first session is for. Only a file older than
    /// <see cref="FirstVersionWithFallLoopFacts"/> can be the first player.
    /// </para>
    /// </remarks>
    public static bool SeedFallLoopAsLived(LessonFacts f, int fileVersion)
        => fileVersion < FirstVersionWithFallLoopFacts
           && (f.RegionsConquered >= 1 || f.DeepestWave >= Encounters.Checkpoints.ConquestWave
               || f.MasterySpent >= 1);
}
