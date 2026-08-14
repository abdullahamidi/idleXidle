using System;
using System.Collections.Generic;
using System.Linq;

namespace ResonanceHunter.Core.Quests;

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

    /// <summary>Conquer <see cref="Quest.Threshold"/> regions.</summary>
    RegionsConquered,

    /// <summary>Open <see cref="Quest.Threshold"/> chests.</summary>
    ChestsOpened,

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

    public int Threshold { get; init; } = 1;

    /// <summary>How far along this quest is, given the world as it stands.</summary>
    public int Current(QuestProgress p) => Goal switch
    {
        QuestGoal.DepthInRegion => RegionId is { } r && p.DepthByRegion.TryGetValue(r, out var d) ? d : 0,
        QuestGoal.RegionsConquered => p.RegionsConquered,
        QuestGoal.ChestsOpened => p.ChestsOpened,
        QuestGoal.RunsWithVowKept => p.RunsWithVowKept,
        _ => 0,
    };

    public bool IsDone(QuestProgress p) => Current(p) >= Threshold;

    /// <summary>"14 / 20" — the line a gate should show instead of only its demand.</summary>
    public string ProgressText(QuestProgress p) => $"{Math.Min(Current(p), Threshold)} / {Threshold}";
}

/// <summary>
/// Everything the quest layer is allowed to know about the player, as one flat snapshot.
/// </summary>
/// <remarks>
/// Pure data, no host types — the same contract <c>WeaveContext</c> keeps, and for the
/// same reason: it makes every quest testable without a game, and it stops the catalogue from quietly
/// growing a dependency on whatever screen happened to be open.
///
/// Everything here is DERIVED from world state each frame except <see cref="RunsWithVowKept"/>, which
/// is a latched counter the host increments once per qualifying descent and persists.
/// </remarks>
public readonly record struct QuestProgress(
    IReadOnlyDictionary<string, int> DepthByRegion,
    int RegionsConquered,
    int ChestsOpened,
    int RunsWithVowKept)
{
    public static QuestProgress Empty { get; } = new(new Dictionary<string, int>(), 0, 0, 0);
}

/// <summary>The quests that exist.</summary>
/// <remarks>
/// Two of them, because two characters are gated behind quests and a quest that gates nothing is a
/// to-do list entry rather than a feature. They are deliberately not "conquer region X": conquest
/// already unlocks four characters on its own, and a quest that duplicated it would teach the player
/// nothing new about the game.
///
/// THE HOLLOW HUNT asks you to go deeper in the first region than conquering it requires, which is how
/// a player finds out that a conquered region is still worth descending.
///
/// THE FIRST VOW asks you to finish a run with a Vow's demand still met — which cannot be done by
/// accident, because a Vow pays nothing unless the BUILD satisfies it and the weave editor is the only
/// place you can check. It is the quest that teaches the system the game is built around, and it gates
/// THE OATHBOUND, whose whole passive is about Vows.
/// </remarks>
public static class QuestCatalogue
{
    public static IReadOnlyList<Quest> All { get; } = new List<Quest>
    {
        new()
        {
            Id = "q_hollow_hunt", Name = "THE HOLLOW HUNT",
            Demand = "Reach depth 20 in the Verdant Hollow",
            Goal = QuestGoal.DepthInRegion, RegionId = "verdant_hollow", Threshold = 20,
        },
        new()
        {
            Id = "q_first_vow", Name = "THE FIRST VOW",
            Demand = "Finish a descent with a Vow's demand still met",
            Goal = QuestGoal.RunsWithVowKept, Threshold = 1,
        },
    };

    public static Quest? Find(string? id) => id is null ? null : All.FirstOrDefault(q => q.Id == id);

    /// <summary>Every quest satisfied by this snapshot. The host decides what to do about it.</summary>
    public static IReadOnlyList<Quest> Satisfied(QuestProgress p) =>
        All.Where(q => q.IsDone(p)).ToList();
}
