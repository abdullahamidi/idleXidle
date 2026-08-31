using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Core.Quests;

/// <summary>What a quest asks for. One entry per shape of demand, not one per quest.</summary>
/// <remarks>
/// Deliberately the same shape as <c>VowDemand</c>: an enum plus a threshold, read by a
/// pure function over a snapshot struct. That is not stylistic. A quest evaluated by a stored delegate
/// cannot be tested without constructing the host, cannot be serialised, and cannot be looked at — and
/// this codebase's most expensive bug was a value nothing read, which is exactly the failure a lambda
/// hidden in a catalogue invites.
/// </remarks>
public enum QuestGoal
{
    /// <summary>Reach <see cref="Quest.Threshold"/> depth in <see cref="Quest.RegionId"/>.</summary>
    DepthInRegion,

    /// <summary>
    /// Clear <see cref="Quest.Threshold"/> waves with a skill of <see cref="Quest.Style"/> woven
    /// (P10) — summed across the style's two skills, from the same tally that levels them
    /// (<see cref="Builds.SkillProgress"/>). Deterministic practice, never luck.
    /// </summary>
    WavesWithStyle,

    /// <summary>
    /// Fell <see cref="Quest.Threshold"/> bosses (P10). Every fifth wave holds one — no roll.
    /// </summary>
    /// <remarks>
    /// Replaced ChestsOpened, whose quest was the one gate in the game hostage to a drop roll
    /// (a flat 20% boss chest). RegionsConquered went in the same pass: no quest ever used it —
    /// deliberately, per the roster's own rule — which made it a dead member wearing a goal's name.
    /// </remarks>
    BossesFelled,

    /// <summary>
    /// Finish <see cref="Quest.Threshold"/> descents with a Vow whose demand was MET at the end.
    /// </summary>
    /// <remarks>
    /// The one goal that cannot be derived. Every other line here is a fact still true when you look at
    /// it — the depth you reached is on the region, the chests you opened are counted. "You once ran a
    /// whole descent under a kept Vow" is an EVENT, and an event that is not latched when it happens is
    /// an event nobody can prove afterwards. See <see cref="QuestProgress.RunsWithVowKept"/>.
    /// </remarks>
    RunsWithVowKept,
}

/// <summary>One quest: what it is called, what it asks, and how much of it.</summary>
public sealed record Quest
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>The demand in the player's words. Shown wherever the quest gates something.</summary>
    public required string Demand { get; init; }

    public required QuestGoal Goal { get; init; }

    /// <summary>For <see cref="QuestGoal.DepthInRegion"/>.</summary>
    public string? RegionId { get; init; }

    /// <summary>For <see cref="QuestGoal.WavesWithStyle"/>.</summary>
    public Style? Style { get; init; }

    public int Threshold { get; init; } = 1;

    /// <summary>
    /// The word after the count on a progress line — "CHESTS" in "12 / 30 CHESTS". Empty for a
    /// quest whose count needs no noun.
    /// </summary>
    public string Unit { get; init; } = "";

    /// <summary>How far along this quest is, given the world as it stands.</summary>
    public int Current(QuestProgress p) => Goal switch
    {
        QuestGoal.DepthInRegion => RegionId is { } r && p.DepthByRegion.TryGetValue(r, out var d) ? d : 0,
        QuestGoal.WavesWithStyle => Style is { } st
            ? SkillCatalogue.All.Where(s => s.Style == st).Sum(s => p.WavesBySkill.GetValueOrDefault(s.Id))
            : 0,
        QuestGoal.BossesFelled => p.BossesFelled,
        QuestGoal.RunsWithVowKept => p.RunsWithVowKept,
        _ => 0,
    };

    public bool IsDone(QuestProgress p) => Current(p) >= Threshold;

    /// <summary>"14 / 20" — the count a gate should show instead of only its demand.</summary>
    public string ProgressText(QuestProgress p) => $"{Math.Min(Current(p), Threshold)} / {Threshold}";

    /// <summary>"14 / 20 WAVES" — the count with its noun, for a card that has room for the noun.</summary>
    public string ProgressLine(QuestProgress p) =>
        Unit.Length == 0 ? ProgressText(p) : $"{ProgressText(p)} {Unit}";
}

/// <summary>
/// Everything the quest layer is allowed to know about the player, as one flat snapshot.
/// </summary>
/// <remarks>
/// Pure data, no host types — the same contract <c>WeaveContext</c> keeps, and for the
/// same reason: it makes every quest testable without a game, and it stops the catalogue from quietly
/// growing a dependency on whatever screen happened to be open.
///
/// Everything here is DERIVED from live state each frame except <see cref="RunsWithVowKept"/> and
/// <see cref="BossesFelled"/> — latched counters the host increments and persists (a descent ends,
/// a boss falls; neither is a fact you can re-ask the world about later).
/// </remarks>
public readonly record struct QuestProgress(
    IReadOnlyDictionary<string, int> DepthByRegion,
    int RunsWithVowKept,
    int BossesFelled,
    IReadOnlyDictionary<string, int> WavesBySkill)
{
    public static QuestProgress Empty { get; } =
        new(new Dictionary<string, int>(), 0, 0, new Dictionary<string, int>());
}

/// <summary>The quests that exist.</summary>
/// <remarks>
/// <para>
/// Five of them, one per class, because the SECOND champion of every class is gated behind a quest
/// (see <see cref="Characters.ClassTier"/>) and a quest that gates nothing is a to-do list entry
/// rather than a feature. None of them is a conquest of any shape: conquest already unlocks the FIRST
/// of each class on its own, and a quest that duplicated it would teach the player nothing new. Each
/// one asks for something no conquest gives — waves well past the conquest line, chests, kept Vows —
/// so the second half of the roster is earned rather than arriving in the first hours.
/// </para>
/// <para>
/// The two original quests (<c>q_hollow_hunt</c> at depth 20, <c>q_first_vow</c> at one kept Vow)
/// are gone from here and live only in <see cref="Characters.LegacyUnlocks"/>, which reads an old
/// save's record of them so nobody loses a champion they had already earned. Their ids were retired
/// rather than reused on purpose: a save that had "q_hollow_hunt" done had reached depth 20, and if the
/// same id now meant depth 60 that save would hand out THE MAGPIE for free.
/// </para>
/// <para>
/// A third id is retired the same way: <c>q_every_region</c>, "conquer every region", gated THE
/// THORNWALL for one day. It was written to keep the two Bulwarks off the same frame — the fifth
/// region's conquest opens THE UNBROKEN — but the sixth conquest is the very next thing a player does
/// after the fifth, so the pair still arrived together (playtest 2026-08-26: "both Bulwarks unlock at
/// the same time — silly. Bulwark stands for endurance, so the second one should unlock by clearing
/// a certain wave at deeper levels"). The Bulwark quest is now a HOLD in the Body region, far past
/// the conquest line; a save that had the old id done already has THE THORNWALL banked.
/// </para>
/// <para>
/// Two more retired the same day: <c>q_thirty_chests</c> (was THE QUIVER's) and <c>q_hollow_deep</c>
/// (was THE MAGPIE's). The playtest swapped the champions — the loot champion gets the chest quest —
/// and swapping only the champions under the old ids would have handed a save with "q_thirty_chests"
/// done THE MAGPIE on its first frame while it kept THE QUIVER. The new ids are <c>q_magpie_chests</c>
/// and <c>q_quiver_hollow</c>; progress is derived from chests opened and depth, so nobody mid-way
/// loses a step, and the bank keeps what was earned under the old names.
/// </para>
/// <para>
/// P10 (2026-08-31) retires those two in turn, by the same law. <c>q_quiver_hollow</c>'s depth
/// demand was the third "reach wave N" in a five-quest catalogue; <c>q_quiver_volleys</c> asks for
/// the thing the champion is ABOUT. <c>q_magpie_chests</c> was the one gate hostage to a drop roll;
/// <c>q_magpie_bosses</c> counts the bosses the chests came from. A champion already earned rides
/// the unlock BANK (nothing is ever taken back); partial progress under an old demand does not
/// carry — except that BossesFelled seeds from ChestsOpened as an honest floor, since every opened
/// chest was a felled boss.
/// </para>
/// </remarks>
public static class QuestCatalogue
{
    public static IReadOnlyList<Quest> All { get; } = new List<Quest>
    {
        new()
        {
            Id = "q_cinder_deep", Name = "THE LONG FURNACE",
            Demand = "Reach wave 50 in Cinderworks",
            Goal = QuestGoal.DepthInRegion, RegionId = "cinderworks", Threshold = 50, Unit = "WAVES",
        },
        new()
        {
            Id = "q_three_vows", Name = "THE THIRD OATH",
            Demand = "Finish three descents with a Vow's demand still met",
            Goal = QuestGoal.RunsWithVowKept, Threshold = 3, Unit = "DESCENTS",
        },
        new()
        {
            // Re-keyed from q_magpie_chests (P10): the only drop-roll gate in the game. Bosses are
            // the deterministic thing the chests came from — every fifth wave, no luck involved.
            Id = "q_magpie_bosses", Name = "THE FULL HOLD",
            Demand = "Fell 40 bosses",
            Goal = QuestGoal.BossesFelled, Threshold = 40, Unit = "BOSSES",
        },
        new()
        {
            // The Bulwark's quest is ENDURANCE: not reaching a wave but holding one, in the Body region,
            // at four times the conquest line. "Hold", because that is the word the hunt's banner
            // uses for staying past the bar, and the one the class is named for.
            Id = "q_marrow_hold", Name = "THE LONG STAND",
            Demand = "Hold wave 80 in Marrow Wastes",
            Goal = QuestGoal.DepthInRegion, RegionId = "marrow_wastes",
            Threshold = Encounters.Checkpoints.ConquestWave * 4, Unit = "WAVES",
        },
        new()
        {
            // Re-keyed from q_quiver_hollow (P10): the catalogue's THIRD "reach wave N" became the
            // quest about what THE QUIVER is — practice with VOLLEY skills, counted by the same
            // tally that levels them, across both of the style's skills.
            Id = "q_quiver_volleys", Name = "THE COUNTED ARROWS",
            Demand = "Clear 150 waves with VOLLEY skills woven",
            Goal = QuestGoal.WavesWithStyle, Style = Builds.Style.Volley, Threshold = 150, Unit = "WAVES",
        },
    };

    public static Quest? Find(string? id) => id is null ? null : All.FirstOrDefault(q => q.Id == id);

    /// <summary>Every quest satisfied by this snapshot. The host decides what to do about it.</summary>
    public static IReadOnlyList<Quest> Satisfied(QuestProgress p) =>
        All.Where(q => q.IsDone(p)).ToList();
}
