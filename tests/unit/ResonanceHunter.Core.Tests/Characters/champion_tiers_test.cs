using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Quests;
using Xunit;

namespace ResonanceHunter.Core.Tests.Characters;

/// <summary>
/// The tiered roster: one FIRST and one SECOND per class, the first behind a conquest, the second
/// behind a quest with a real demand.
/// </summary>
/// <remarks>
/// Playtest (2026-08-26): "All the characters unlock far too easily. The first character of each
/// class may unlock somewhat easily, but the second one should take effort." These hold the roster
/// to that sentence, and to the quest catalogue's ability to actually measure each demand from a
/// snapshot — a gate the host cannot evaluate is a champion nobody can earn.
/// </remarks>
public class ChampionTiersTest
{
    private static IEnumerable<ItemClass> Classes => ItemClasses.All.Select(d => d.Class);

    [Fact]
    public void test_every_class_has_exactly_one_first_and_one_second()
    {
        foreach (var cls in Classes)
        {
            var mine = CharacterRoster.All.Where(c => c.Class == cls).ToList();
            Assert.Equal(2, mine.Count);
            Assert.Single(mine, c => c.Tier == ClassTier.First);
            Assert.Single(mine, c => c.Tier == ClassTier.Second);
        }
    }

    [Fact]
    public void test_every_second_is_quest_gated_and_every_first_is_conquest_or_start()
    {
        foreach (var c in CharacterRoster.All)
            if (c.Tier == ClassTier.Second)
                Assert.True(c.Unlock.Kind == UnlockKind.Quest, $"{c.Name} is SECOND but not quest-gated");
            else
                Assert.True(c.Unlock.Kind is UnlockKind.Conquest or UnlockKind.Start,
                            $"{c.Name} is FIRST but gated by {c.Unlock.Kind}");
    }

    [Fact]
    public void test_exactly_one_champion_is_yours_from_the_start_and_it_is_the_starter()
    {
        var start = CharacterRoster.All.Where(c => c.Unlock.Kind == UnlockKind.Start).ToList();
        Assert.Single(start);
        Assert.Equal(CharacterRoster.StarterId, start[0].Id);
        Assert.Equal(ClassTier.First, start[0].Tier);
    }

    [Fact]
    public void test_every_second_champion_waits_on_a_quest_the_catalogue_has()
    {
        foreach (var c in CharacterRoster.All.Where(c => c.Tier == ClassTier.Second))
            Assert.NotNull(QuestCatalogue.Find(c.Unlock.QuestId));
    }

    [Fact]
    public void test_conquering_the_whole_map_alone_unlocks_every_first_and_no_second()
    {
        // The whole point: a player who has only conquered has half the roster, not all of it.
        var state = new CharacterState();
        var everyRegion = ResonanceHunter.Core.Encounters.Regions.All.Select(r => r.Id).ToList();
        state.Refresh(everyRegion);

        foreach (var c in CharacterRoster.All)
            Assert.Equal(c.Tier == ClassTier.First, state.IsUnlocked(c.Id));
    }

    [Fact]
    public void test_no_second_shares_a_moment_with_its_first()
    {
        // For each class, the snapshot that has JUST unlocked the FIRST must not also satisfy the
        // SECOND's quest — otherwise the two arrive on the same frame and the tier is a label.
        foreach (var cls in Classes)
        {
            var first = CharacterRoster.All.Single(c => c.Class == cls && c.Tier == ClassTier.First);
            var second = CharacterRoster.All.Single(c => c.Class == cls && c.Tier == ClassTier.Second);
            var quest = QuestCatalogue.Find(second.Unlock.QuestId)!;

            // The moment the first unlocks: the regions up to and including its own are conquered,
            // each at exactly the conquest line, and nothing else has happened.
            var regions = ResonanceHunter.Core.Encounters.Regions.All.Select(r => r.Id).ToList();
            var upTo = first.Unlock.Kind == UnlockKind.Conquest
                ? regions.Take(regions.IndexOf(first.Unlock.RegionId!) + 1).ToList()
                : new List<string>();
            var snapshot = new QuestProgress(
                upTo.ToDictionary(id => id, _ => 20),
                upTo.Count, ChestsOpened: 0, RunsWithVowKept: 0);

            Assert.False(quest.IsDone(snapshot),
                         $"{second.Name} would unlock on the same frame as {first.Name}");
        }
    }

    [Fact]
    public void test_the_class_catalogue_lists_the_first_champion_before_the_second()
    {
        // "THE ANVIL OR THE FALLING TOWER CAN WEAR IT" — the easy one first, on every line that
        // names the pair.
        foreach (var def in ItemClasses.All)
        {
            Assert.Equal(ClassTier.First, CharacterRoster.Get(def.ChampionIds[0]).Tier);
            Assert.Equal(ClassTier.Second, CharacterRoster.Get(def.ChampionIds[1]).Tier);
        }
    }

    [Fact]
    public void test_the_tier_line_reads_first_or_second_of_the_class()
    {
        Assert.Equal("FIRST OF THE WARDENS", CharacterRoster.Get("anvil").TierLine);
        Assert.Equal("SECOND OF THE WARDENS", CharacterRoster.Get("tower").TierLine);
        Assert.Equal("FIRST OF THE WANDERERS", CharacterRoster.Get("seeker").TierLine);
        Assert.Equal("SECOND OF THE MYSTICS", CharacterRoster.Get("quiver").TierLine);
    }

    [Fact]
    public void test_the_short_name_drops_the_article_for_the_hud()
    {
        Assert.Equal("SEEKER", CharacterRoster.Get("seeker").ShortName);
        Assert.Equal("FALLING TOWER", CharacterRoster.Get("tower").ShortName);
    }

    [Fact]
    public void test_the_class_icon_key_follows_the_asset_naming()
    {
        foreach (var cls in Classes)
            Assert.Equal($"icon_class_{cls.ToString().ToLowerInvariant()}", ItemClasses.IconKey(cls));
    }

    // ── Every second champion's demand is measurable from a snapshot ─────────────────────────────

    public static IEnumerable<object[]> SecondChampions() =>
        CharacterRoster.All.Where(c => c.Tier == ClassTier.Second).Select(c => new object[] { c.Id });

    /// <summary>A snapshot that meets the quest by exactly <paramref name="margin"/> over or under its threshold.</summary>
    private static QuestProgress AtThreshold(Quest q, int margin)
    {
        var n = q.Threshold + margin;
        return q.Goal switch
        {
            QuestGoal.DepthInRegion => new QuestProgress(new Dictionary<string, int> { [q.RegionId!] = n }, 0, 0, 0),
            QuestGoal.RegionsConquered => new QuestProgress(new Dictionary<string, int>(), n, 0, 0),
            QuestGoal.ChestsOpened => new QuestProgress(new Dictionary<string, int>(), 0, n, 0),
            QuestGoal.RunsWithVowKept => new QuestProgress(new Dictionary<string, int>(), 0, 0, n),
            _ => throw new System.NotSupportedException(q.Goal.ToString()),
        };
    }

    [Theory]
    [MemberData(nameof(SecondChampions))]
    public void test_each_second_champions_quest_turns_at_its_threshold(string id)
    {
        var c = CharacterRoster.Get(id);
        var q = QuestCatalogue.Find(c.Unlock.QuestId)!;

        Assert.False(q.IsDone(QuestProgress.Empty), $"{q.Name} is done for a fresh player");
        Assert.False(q.IsDone(AtThreshold(q, -1)), $"{q.Name} is done one short of its demand");
        Assert.True(q.IsDone(AtThreshold(q, 0)), $"{q.Name} is not done at its demand");
        Assert.Equal(q.Threshold - 1, q.Current(AtThreshold(q, -1)));
        Assert.EndsWith(q.Unit, q.ProgressLine(AtThreshold(q, -1)));
    }

    [Theory]
    [MemberData(nameof(SecondChampions))]
    public void test_each_second_champion_unlocks_through_the_state_when_its_quest_is_met(string id)
    {
        var c = CharacterRoster.Get(id);
        var q = QuestCatalogue.Find(c.Unlock.QuestId)!;
        var state = new CharacterState();

        foreach (var done in QuestCatalogue.Satisfied(AtThreshold(q, 0)))
            state.CompleteQuest(done.Id);
        var fresh = state.Refresh(System.Array.Empty<string>());

        Assert.Contains(fresh, f => f.Id == id);
        // And ONLY this one of the seconds — meeting one demand is not meeting the others.
        Assert.Equal(1, fresh.Count(f => f.Tier == ClassTier.Second));
    }

    [Fact]
    public void test_every_quest_demand_is_plain_english_for_the_card()
    {
        // The font gate's alphabet plus the marks it has proven; no jargon abbreviations.
        foreach (var q in QuestCatalogue.All)
        {
            Assert.Matches("^[A-Za-z0-9 ',.·×—–]+$", q.Demand);
            Assert.DoesNotContain("depth", q.Demand);   // the player's word is "wave"
        }
    }
}
