using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Quests;
using Xunit;

namespace ResonanceHunter.Core.Tests;

/// <summary>
/// The quest layer, which exists so that two of the ten characters are obtainable at all.
/// </summary>
public class QuestsTest
{
    private static QuestProgress Progress(
        int hollowDepth = 0, int conquered = 0, int chests = 0, int vowRuns = 0) =>
        new(new Dictionary<string, int> { ["verdant_hollow"] = hollowDepth },
            conquered, chests, vowRuns);

    [Fact]
    public void test_a_fresh_player_has_finished_nothing()
    {
        Assert.Empty(QuestCatalogue.Satisfied(QuestProgress.Empty));
    }

    [Fact]
    public void test_depth_quest_completes_only_at_its_threshold()
    {
        var q = QuestCatalogue.Find("q_hollow_hunt")!;
        Assert.Equal(20, q.Threshold);
        Assert.False(q.IsDone(Progress(hollowDepth: 19)));
        Assert.True(q.IsDone(Progress(hollowDepth: 20)));
        Assert.True(q.IsDone(Progress(hollowDepth: 40)));
    }

    [Fact]
    public void test_depth_in_one_region_does_not_satisfy_a_quest_about_another()
    {
        var q = QuestCatalogue.Find("q_hollow_hunt")!;
        var elsewhere = new QuestProgress(
            new Dictionary<string, int> { ["cinderworks"] = 99 }, 1, 0, 0);
        Assert.False(q.IsDone(elsewhere));
        Assert.Equal(0, q.Current(elsewhere));
    }

    [Fact]
    public void test_the_vow_quest_needs_a_latched_run()
    {
        var q = QuestCatalogue.Find("q_first_vow")!;
        Assert.False(q.IsDone(Progress(hollowDepth: 999, conquered: 6, chests: 500)));
        Assert.True(q.IsDone(Progress(vowRuns: 1)));
    }

    [Fact]
    public void test_progress_text_clamps_at_the_threshold()
    {
        var q = QuestCatalogue.Find("q_hollow_hunt")!;
        Assert.Equal("14 / 20", q.ProgressText(Progress(hollowDepth: 14)));
        // Past the goal it reads "20 / 20", not "40 / 20" — a finished quest should not keep counting.
        Assert.Equal("20 / 20", q.ProgressText(Progress(hollowDepth: 40)));
    }

    [Fact]
    public void test_every_quest_gate_on_a_character_names_a_real_quest()
    {
        // The failure this catches is the one the roster shipped with: a character gated behind a
        // quest id that nothing defines is a character nobody can ever earn.
        foreach (var c in CharacterRoster.All.Where(c => c.Unlock.Kind == UnlockKind.Quest))
        {
            Assert.NotNull(c.Unlock.QuestId);
            Assert.True(QuestCatalogue.Find(c.Unlock.QuestId) is not null,
                        $"{c.Name} waits on quest '{c.Unlock.QuestId}', which does not exist.");
        }
    }

    [Fact]
    public void test_every_quest_gates_something()
    {
        // The mirror check. A quest nothing is waiting on is a to-do list entry, not a feature — and
        // it would sit there being completable with no observable effect.
        var gated = CharacterRoster.All
            .Where(c => c.Unlock.Kind == UnlockKind.Quest)
            .Select(c => c.Unlock.QuestId)
            .ToHashSet();
        foreach (var q in QuestCatalogue.All)
            Assert.True(gated.Contains(q.Id), $"{q.Name} unlocks nothing.");
    }

    [Fact]
    public void test_finishing_a_quest_unlocks_its_character_through_character_state()
    {
        // End to end across the seam: quest satisfied -> CompleteQuest -> Refresh -> unlocked.
        var state = new CharacterState();
        var everyRegion = CharacterRoster.All
            .Where(c => c.Unlock.Kind == UnlockKind.Conquest)
            .Select(c => c.Unlock.RegionId!)
            .ToList();
        state.Refresh(everyRegion);
        Assert.False(state.IsUnlocked("quiver"));

        foreach (var done in QuestCatalogue.Satisfied(Progress(hollowDepth: 20)))
            state.CompleteQuest(done.Id);

        var fresh = state.Refresh(everyRegion);
        Assert.Contains(fresh, c => c.Id == "quiver");
        Assert.True(state.IsUnlocked("quiver"));
        // The other quest character stays locked — one quest finishing must not unlock both.
        Assert.False(state.IsUnlocked("oathbound"));
    }

    [Fact]
    public void test_quest_ids_and_names_are_unique()
    {
        Assert.Equal(QuestCatalogue.All.Count, QuestCatalogue.All.Select(q => q.Id).Distinct().Count());
        Assert.Equal(QuestCatalogue.All.Count, QuestCatalogue.All.Select(q => q.Name).Distinct().Count());
    }

    [Fact]
    public void test_a_region_quest_names_a_region_that_exists()
    {
        foreach (var q in QuestCatalogue.All.Where(q => q.Goal == QuestGoal.DepthInRegion))
        {
            Assert.NotNull(q.RegionId);
            Assert.True(ResonanceHunter.Core.Encounters.Regions.Find(q.RegionId!) is not null,
                        $"{q.Name} asks for depth in '{q.RegionId}', which does not exist.");
        }
    }

    [Fact]
    public void test_the_hollow_hunt_asks_for_more_than_conquering_the_region()
    {
        // A quest that duplicated conquest would gate a character behind something the player has
        // already done by the time they can read the gate. Conquest fires at depth 7.
        var q = QuestCatalogue.Find("q_hollow_hunt")!;
        Assert.True(q.Threshold > 7, "the Hollow Hunt must ask for more than a conquest does");
    }
}
