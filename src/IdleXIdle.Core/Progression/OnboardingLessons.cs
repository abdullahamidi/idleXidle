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

    // ── THE CLIMAX: fail, diagnose, change, retry. The loop this whole game is. ───────────────────
    /// <summary>The first fall, and the report that says what stopped it.</summary>
    FirstFailureReport,

    /// <summary>Anything real: a stat, a skill, a node, a worn piece.</summary>
    FirstPostFailureChange,

    /// <summary>Back into the fight with the change made.</summary>
    FirstRetry,

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
    bool GuidanceOff = false);

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
/// thing, try again, see if it went deeper. That is why the failure trio outranks everything: the
/// strongest first-session milestone is not "finished the tutorial", it is
/// <i>opened the report, changed something, and went back down</i>.
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
        OnboardingLessonId.FirstChestOpen => f.ChestsOpened >= 1 || f.ItemsOwned >= 1 || f.ItemsWorn >= 1,
        OnboardingLessonId.FirstItemEquip => f.ItemsWorn >= 1,

        OnboardingLessonId.FirstFailureReport => f.ReportOpenedEver,
        OnboardingLessonId.FirstPostFailureChange => f.ChangedAfterFall,
        OnboardingLessonId.FirstRetry => f.RetriedAfterChange,

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
        OnboardingLessonId.FirstChestOpen => f.ChestsHeld >= 1,
        OnboardingLessonId.FirstItemEquip => f.ItemsOwned >= 1,

        // ── THE TRIO. Each waits on the one before it, because they are one sequence. ────────────
        OnboardingLessonId.FirstFailureReport => f.Falls >= 1,
        OnboardingLessonId.FirstPostFailureChange => f.Falls >= 1 && f.ReportOpenedEver,
        OnboardingLessonId.FirstRetry => f.Falls >= 1 && f.ChangedAfterFall,

        OnboardingLessonId.FirstMasterySpend => f.MasteryPointsFree >= 1,
        // AVAILABLE means the tree reaches it NOW. Mastery access is not permanent: a respec that
        // gives the road back takes the skill with it, so this asks the live count.
        OnboardingLessonId.FirstSharedSkillEquip => f.SharedSkillsAvailable >= 1 && f.EmptySkillSlots >= 1,
        OnboardingLessonId.FirstVariationChoice => f.VariationsOffered >= 1,

        OnboardingLessonId.FirstRegionConquest => false,   // an Observe beat the conquest raises
        OnboardingLessonId.FirstRegionTravel => f.RegionsConquered >= 1 && f.RegionsOpen >= 2,
        OnboardingLessonId.FirstKeystoneChoice => f.KeystonesDiscovered >= 1,

        OnboardingLessonId.FirstGemSocket => f.GemsHeld >= 1,
        OnboardingLessonId.FirstTraitEquip => f.TraitsDiscovered >= 1,
        OnboardingLessonId.FirstHunterInteraction => f.HuntersOwned >= 2,
        // NEVER BEFORE THE CAMP HAS PAID. A Warren tour given to somebody the Warren has done nothing
        // for is a theory; given after an offline payout it answers a question they already have.
        OnboardingLessonId.FirstWarrenReturn => f.WarrenOpen && f.WarrenPaidOffline,
        _ => false,
    };

    // ── PRIORITY ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Which lesson wins when several are eligible. Higher is louder; ties break on declaration order.
    /// </summary>
    /// <remarks>
    /// The failure trio is the top band because it is the game's actual loop and the strongest
    /// first-session milestone there is. The opening beats come next because a player who cannot get
    /// started has no loop to learn. Everything contextual sits at the bottom, where it can wait
    /// without blocking anything — which is the whole point of replacing the ladder.
    /// </remarks>
    public static int Priority(OnboardingLessonId id) => id switch
    {
        OnboardingLessonId.FirstFailureReport => 100,
        OnboardingLessonId.FirstPostFailureChange => 99,
        OnboardingLessonId.FirstRetry => 98,

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
        OnboardingLessonId.FirstHunterInteraction => 20,
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
        OnboardingLessonId.FirstKeystoneChoice => LessonMode.SoftGuide,
        OnboardingLessonId.FirstRegionTravel => LessonMode.SoftGuide,

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
        OnboardingLessonId.FirstRetry => "GO AGAIN",
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
        OnboardingLessonId.FirstRetry => "TRY AGAIN",
        OnboardingLessonId.FirstMasterySpend => "SPEND ONE MASTERY POINT",
        OnboardingLessonId.FirstSharedSkillEquip => "EQUIP ONE SHARED SKILL",
        OnboardingLessonId.FirstVariationChoice => "CHOOSE A VARIATION",
        OnboardingLessonId.FirstRegionTravel => "TRAVEL TO THE NEXT REGION",
        OnboardingLessonId.FirstKeystoneChoice => "CHOOSE A KEYSTONE",
        OnboardingLessonId.FirstGemSocket => "SOCKET YOUR FIRST GEM",
        OnboardingLessonId.FirstTraitEquip => "CHOOSE ONE TRAIT",
        OnboardingLessonId.FirstHunterInteraction => "TRY ANOTHER HUNTER",
        OnboardingLessonId.FirstWarrenReturn => "OPEN WARREN",
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
    /// Should a returning save be treated as having already lived the fall loop?
    /// </summary>
    /// <remarks>
    /// The three fall-loop facts are the only ones onboarding has to persist, and a save written before
    /// they existed carries none of them. A player who has already conquered a region has fallen, read
    /// or ignored the log, changed their build and gone again many times over — replaying READ THE LOG
    /// at them would be the game announcing it has not been watching. So a save with real progression
    /// behind it is seeded as having done all three.
    /// </remarks>
    public static bool SeedFallLoopAsLived(LessonFacts f)
        => f.RegionsConquered >= 1 || f.DeepestWave >= Encounters.Checkpoints.ConquestWave || f.MasterySpent >= 1;
}
