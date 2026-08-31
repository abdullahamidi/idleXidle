using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Quests;
using Xunit;

namespace IdleXIdle.Core.Tests;

/// <summary>
/// The quest layer, which exists so that the SECOND champion of every class is obtainable at all.
/// </summary>
public class QuestsTest
{
    private static QuestProgress Progress(
        int hollowDepth = 0, int vowRuns = 0, int cinderDepth = 0, int bosses = 0) =>
        new(new Dictionary<string, int> { ["verdant_hollow"] = hollowDepth, ["cinderworks"] = cinderDepth },
            vowRuns, bosses, new Dictionary<string, int>());

    [Fact]
    public void test_a_fresh_player_has_finished_nothing()
    {
        Assert.Empty(QuestCatalogue.Satisfied(QuestProgress.Empty));
    }

    [Fact]
    public void test_depth_quest_completes_only_at_its_threshold()
    {
        var q = QuestCatalogue.Find("q_cinder_deep")!;
        Assert.Equal(50, q.Threshold);
        Assert.False(q.IsDone(Progress(cinderDepth: 49)));
        Assert.True(q.IsDone(Progress(cinderDepth: 50)));
        Assert.True(q.IsDone(Progress(cinderDepth: 90)));
    }

    [Fact]
    public void test_depth_in_one_region_does_not_satisfy_a_quest_about_another()
    {
        var q = QuestCatalogue.Find("q_cinder_deep")!;
        var elsewhere = Progress(hollowDepth: 99);
        Assert.False(q.IsDone(elsewhere));
        Assert.Equal(0, q.Current(elsewhere));
    }

    [Fact]
    public void test_the_vow_quest_needs_a_latched_run()
    {
        var q = QuestCatalogue.Find("q_three_vows")!;
        Assert.False(q.IsDone(Progress(hollowDepth: 999, cinderDepth: 999, bosses: 500)));
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
        Assert.False(state.IsUnlocked("magpie"));

        // Forty bosses is THE MAGPIE's quest (P10) — the loot champion is earned by felling the
        // things the loot comes from, deterministically.
        foreach (var done in QuestCatalogue.Satisfied(Progress(bosses: 40)))
            state.CompleteQuest(done.Id);

        var fresh = state.Refresh(everyRegion);
        Assert.Contains(fresh, c => c.Id == "magpie");
        Assert.True(state.IsUnlocked("magpie"));
        // The other quest characters stay locked — one quest finishing must not unlock the rest.
        Assert.False(state.IsUnlocked("oathbound"));
        Assert.False(state.IsUnlocked("quiver"));
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
            Assert.True(IdleXIdle.Core.Encounters.Regions.Find(q.RegionId!) is not null,
                        $"{q.Name} asks for depth in '{q.RegionId}', which does not exist.");
        }
    }

    [Fact]
    public void test_every_depth_quest_asks_for_more_than_conquering_the_region()
    {
        // A quest that duplicated conquest would gate a character behind something the player has
        // already done by the time they can read the gate. Conquest is wave 20 (Checkpoints.ConquestWave,
        // which the host mirrors); every depth quest has to clear it by a margin, or it is a conquest
        // wearing a quest's name.
        const int conquestLine = IdleXIdle.Core.Encounters.Checkpoints.ConquestWave;
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
    public void test_no_gate_depends_on_a_drop_roll()
    {
        // The brief's rule: measurable, deterministic. Depth, practice, bosses and kept vows are
        // facts a player can WALK toward; the retired chest gate was a 20% roll they could only
        // wait on — the one gate in the game hostage to luck.
        foreach (var q in QuestCatalogue.All)
            Assert.True(q.Goal is QuestGoal.DepthInRegion or QuestGoal.WavesWithStyle
                            or QuestGoal.BossesFelled or QuestGoal.RunsWithVowKept,
                        $"{q.Name} counts {q.Goal}, which is not a deterministic career fact");
    }

    [Fact]
    public void test_the_bulwark_quest_is_an_endurance_hold_far_past_the_conquest_line()
    {
        // Playtest 2026-08-26: "both Bulwarks unlock at the same time — silly. Bulwark stands for
        // endurance, so the second one should unlock by clearing a certain wave at deeper levels."
        // THE WHOLE MAP (conquer all six) was retired: the fifth conquest opens THE UNBROKEN and the
        // sixth is the very next thing a player does. The gate is a hold in the Body region now.
        var thornwall = CharacterRoster.Get("thornwall");
        var q = QuestCatalogue.Find(thornwall.Unlock.QuestId)!;

        Assert.Equal("q_marrow_hold", q.Id);
        Assert.Equal(QuestGoal.DepthInRegion, q.Goal);
        Assert.Equal("marrow_wastes", q.RegionId);
        Assert.Equal(80, q.Threshold);
        Assert.True(q.Threshold >= IdleXIdle.Core.Encounters.Checkpoints.ConquestWave * 4);
        Assert.Null(QuestCatalogue.Find("q_every_region"));

        // What the card and the EARNED line say, in the player's words.
        Assert.Equal("HOLD WAVE 80 IN MARROW WASTES", q.Demand.ToUpperInvariant());
        Assert.Equal("34 / 80 WAVES", q.ProgressLine(new QuestProgress(
            new Dictionary<string, int> { ["marrow_wastes"] = 34 }, 0, 0, new Dictionary<string, int>())));
    }

    [Fact]
    public void test_the_re_keyed_gates_retired_their_old_ids()
    {
        // The catalogue's own law, applied twice more in P10: a quest whose MEANING changes takes a
        // NEW id, because a banked "done" under the old id must neither hand out a champion for a
        // different demand nor be revoked. Champions already earned ride the unlock BANK either way.
        Assert.Null(QuestCatalogue.Find("q_thirty_chests"));
        Assert.Null(QuestCatalogue.Find("q_hollow_deep"));
        Assert.Null(QuestCatalogue.Find("q_magpie_chests"));   // was the one drop-roll gate
        Assert.Null(QuestCatalogue.Find("q_quiver_hollow"));   // was the catalogue's THIRD depth quest

        Assert.Equal("q_magpie_bosses", CharacterRoster.Get("magpie").Unlock.QuestId);
        Assert.Equal(QuestGoal.BossesFelled, QuestCatalogue.Find("q_magpie_bosses")!.Goal);
        Assert.Equal("q_quiver_volleys", CharacterRoster.Get("quiver").Unlock.QuestId);
        Assert.Equal(QuestGoal.WavesWithStyle, QuestCatalogue.Find("q_quiver_volleys")!.Goal);
        Assert.Equal(Style.Volley, QuestCatalogue.Find("q_quiver_volleys")!.Style);
    }
}
