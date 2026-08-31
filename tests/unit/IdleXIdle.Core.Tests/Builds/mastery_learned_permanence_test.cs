using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// SKILL DISCOVERY IS PERMANENT (P6, decision D7). A road node TEACHES; respec returns the points
/// and never the skill — otherwise moving six mastery points costs a player their abilities, and
/// the four-slot build could never be filled at all (one discipline gates one style's road, so
/// permanence-through-respec is the only door to the other ten skills).
/// </summary>
public class MasteryLearnedPermanenceTest
{
    /// <summary>Walk the prerequisite chain and take the node — the way a player reaches it.</summary>
    private static void TakeWithPrereqs(MasteryTree t, string id)
    {
        var n = MasteryCatalog.ById(id);
        Assert.NotNull(n);
        if (n!.Prereqs.Count > 0 && !n.Prereqs.Any(t.IsTaken)) TakeWithPrereqs(t, n.Prereqs[0]);
        if (n.SecondPrereqs.Count > 0 && !n.SecondPrereqs.Any(t.IsTaken)) TakeWithPrereqs(t, n.SecondPrereqs[0]);
        Assert.True(t.Take(id), $"could not take {id}");
    }

    [Fact]
    public void test_respec_returns_points_but_the_skill_stays_learned()
    {
        // Arrange — walk to HAMMER's road the way a player does.
        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");
        Assert.Contains("hammer_blow", t.LearnedSkills());

        // Act — every point back.
        t.Respec();

        // Assert — the points returned; the skill did not go with them.
        Assert.Equal(0, t.Spent);
        Assert.Contains("hammer_blow", t.LearnedSkills());
    }

    [Fact]
    public void test_a_single_node_refund_keeps_the_skill_too()
    {
        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");

        Assert.True(t.Refund("road_hammer"));
        Assert.Contains("hammer_blow", t.LearnedSkills());
    }

    [Fact]
    public void test_restored_learned_skills_survive_and_unknown_ids_are_dropped()
    {
        // Arrange/Act — the save's latched set, with a ghost from a renamed catalogue.
        var t = new MasteryTree();
        t.RestoreLearned(new[] { "volley_spray", "ghost_skill" });

        // Assert
        Assert.Contains("volley_spray", t.LearnedSkills());
        Assert.DoesNotContain("ghost_skill", t.LearnedSkills());
    }

    [Fact]
    public void test_an_old_save_seeds_learned_from_its_taken_roads()
    {
        // A pre-D7 save carries no LearnedSkills list — only MasteryTaken. The derived half of the
        // union keeps what those roads taught, and the next save latches it for good.
        var t = new MasteryTree();
        t.RestoreTaken(new[] { "spec_strike", "road_hammer" });

        Assert.Contains("hammer_blow", t.LearnedSkills());
    }

    [Fact]
    public void test_the_composer_weaves_a_learned_skill_after_the_points_moved_on()
    {
        // Arrange — learn HAMMER's road, then respec into nothing.
        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");
        t.Respec();

        var loadout = new PlayerLoadout { SkillCapacity = 1 };
        loadout.SetSkill(loadout.AddSkill(), "hammer_blow");
        loadout.SetSource(0, Source.Body);

        // Act — compose through the hunt's own door, with no character (no birth-skill help).
        var build = loadout.ToBuild(new MemoryDustTree(), t, character: null);

        // Assert — the fight carries the skill the points once bought.
        var sk = Assert.Single(build.Skills);
        Assert.Equal("hammer_blow", sk.Def.Id);
    }
}
