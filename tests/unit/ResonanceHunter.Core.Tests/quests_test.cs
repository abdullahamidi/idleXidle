using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Quests;
using Xunit;

namespace ResonanceHunter.Core.Tests;

/// <summary>
/// The quest layer, which exists so that the SECOND champion of every class is obtainable at all.
/// </summary>
public class QuestsTest
{
    private static QuestProgress Progress(
        int hollowDepth = 0, int conquered = 0, int chests = 0, int vowRuns = 0, int cinderDepth = 0) =>
        new(new Dictionary<string, int> { ["verdant_hollow"] = hollowDepth, ["cinderworks"] = cinderDepth },
            conquered, chests, vowRuns);

    [Fact]
    public void test_a_fresh_player_has_finished_nothing()
    {
        Assert.Empty(QuestCatalogue.Satisfied(QuestProgress.Empty));
    }

    [Fact]
    public void test_depth_quest_completes_only_at_its_threshold()
    {
        var q = QuestCatalogue.Find("q_hollow_deep")!;
        Assert.Equal(60, q.Threshold);
        Assert.False(q.IsDone(Progress(hollowDepth: 59)));
        Assert.True(q.IsDone(Progress(hollowDepth: 60)));
        Assert.True(q.IsDone(Progress(hollowDepth: 90)));
    }

    [Fact]
    public void test_depth_in_one_region_does_not_satisfy_a_quest_about_another()
    {
        var q = QuestCatalogue.Find("q_hollow_deep")!;
        var elsewhere = new QuestProgress(
            new Dictionary<string, int> { ["cinderworks"] = 99 }, 1, 0, 0);
        Assert.False(q.IsDone(elsewhere));
        Assert.Equal(0, q.Current(elsewhere));
    }

    [Fact]
    public void test_the_vow_quest_needs_a_latched_run()
    {
        var q = QuestCatalogue.Find("q_three_vows")!;
        Assert.False(q.IsDone(Progress(hollowDepth: 999, conquered: 6, chests: 500)));
        Assert.False(q.IsDone(Progress(vowRuns: 2)));
        Assert.True(q.IsDone(Progress(vowRuns: 3)));
    }

    [Fact]
    public void test_progress_text_clamps_at_the_threshold()
    {
        var q = QuestCatalogue.Find("q_cinder_deep")!;
        Assert.Equal("14 / 50", q.ProgressText(Progress(cinderDepth: 14)));
        Assert.Equal("14 / 50 WAVES", q.ProgressLine(Progress(cinderDepth: 14)));
        // Past the goal it reads "50 / 50", not "90 / 50" — a finished quest should not keep counting.
        Assert.Equal("50 / 50", q.ProgressText(Progress(cinderDepth: 90)));
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

        foreach (var done in QuestCatalogue.Satisfied(Progress(chests: 30)))
            state.CompleteQuest(done.Id);

        var fresh = state.Refresh(everyRegion);
        Assert.Contains(fresh, c => c.Id == "quiver");
        Assert.True(state.IsUnlocked("quiver"));
        // The other quest characters stay locked — one quest finishing must not unlock the rest.
        Assert.False(state.IsUnlocked("oathbound"));
        Assert.False(state.IsUnlocked("magpie"));
        Assert.False(state.IsUnlocked("tower"));
        Assert.False(state.IsUnlocked("thornwall"));
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
    public void test_every_depth_quest_asks_for_more_than_conquering_the_region()
    {
        // A quest that duplicated conquest would gate a character behind something the player has
        // already done by the time they can read the gate. Conquest is moving from wave 7 to wave
        // 20 (Game1.ConquerWaveDepth, which Core cannot see); every depth quest has to clear the
        // higher of the two by a margin, or it is a conquest wearing a quest's name.
        const int conquestLine = 20;
        foreach (var q in QuestCatalogue.All.Where(q => q.Goal == QuestGoal.DepthInRegion))
            Assert.True(q.Threshold >= conquestLine * 2,
                        $"{q.Name} asks for depth {q.Threshold}, which conquest at {conquestLine} nearly gives away");
    }

    [Fact]
    public void test_every_quest_names_its_unit_so_a_card_can_count()
    {
        // "12 / 30" on a locked card is a fraction of nothing. Every quest says what it is counting.
        foreach (var q in QuestCatalogue.All)
            Assert.False(string.IsNullOrWhiteSpace(q.Unit), $"{q.Name} has no unit for its progress line");
    }

    [Fact]
    public void test_the_whole_map_quest_asks_for_every_region_there_is()
    {
        // Not a literal five: the brief said five, but the fifth conquest is the first Bulwark's
        // gate, and a second champion arriving on the same frame as the first is what the tier ends.
        var q = QuestCatalogue.Find("q_every_region")!;
        Assert.Equal(ResonanceHunter.Core.Encounters.Regions.All.Count, q.Threshold);
        Assert.True(q.Threshold > 5);
    }
}
