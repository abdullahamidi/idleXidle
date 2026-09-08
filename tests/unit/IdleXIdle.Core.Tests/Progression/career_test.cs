using System.Collections.Generic;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// The Career arithmetic (P5) — the account's ledger rules that used to live as private methods on
/// Game1, now stated where a test can reach them without a game.
/// </summary>
public class CareerTest
{
    [Fact]
    public void test_deepest_anywhere_is_the_best_of_the_ledger_and_every_region()
    {
        // Arrange — one region holds wave 12; the account ledger remembers 7.
        var world = new World();
        world.RegionFarm(Regions.All[0].Id).RecordDepth(12);

        // Act/Assert — whichever is deeper wins; the Warren's ceiling hangs off this number.
        Assert.Equal(12, Career.DeepestAnywhere(world, deepestEver: 7));
        Assert.Equal(30, Career.DeepestAnywhere(world, deepestEver: 30));
    }

    [Fact]
    public void test_quest_snapshot_reads_the_world_flat()
    {
        // Arrange
        var world = new World();
        var region = Regions.All[0].Id;
        world.RegionFarm(region).RecordDepth(9);

        // Act
        var p = Career.QuestSnapshot(world, runsWithVowKept: 2, bossesFelled: 7,
                                     wavesBySkill: new Dictionary<string, int> { ["volley_spray"] = 5 });

        // Assert — every field is the fact it names, nothing derived twice.
        Assert.Equal(9, p.DepthByRegion[region]);
        Assert.Equal(2, p.RunsWithVowKept);
        Assert.Equal(7, p.BossesFelled);
        Assert.Equal(5, p.WavesBySkill["volley_spray"]);
    }

    [Fact]
    public void test_volley_practice_sums_across_the_styles_two_skills()
    {
        // THE QUIVER's demand reads the same tally that levels skills, and BOTH volley skills feed
        // it — carrying the pair is faster practice, which is exactly what a quiver would say.
        var quiver = IdleXIdle.Core.Quests.QuestCatalogue.Find("q_quiver_volleys")!;
        var p = IdleXIdle.Core.Quests.QuestProgress.Empty with
        {
            WavesBySkill = new Dictionary<string, int> { ["volley_spray"] = 90, ["volley_weep"] = 60 },
        };

        Assert.Equal(150, quiver.Current(p));
        Assert.True(quiver.IsDone(p));
    }

    [Fact]
    public void test_a_kept_vow_is_a_met_demand_not_a_sworn_one()
    {
        // Arrange — VOW OF COMPLETION demands every slot filled. One skill in a one-slot build
        // meets it; the same build against a two-slot capacity does not.
        var vow = Vows.ById("vow_complete")!;
        var met = new Build { SlotCapacity = 1 };
        met.Equip(TestBuilds.Skill("hammer_blow", Source.Body, vow));
        var unmet = new Build { SlotCapacity = 2 };
        unmet.Equip(TestBuilds.Skill("hammer_blow", Source.Body, vow));

        // Act/Assert
        Assert.True(Career.VowWasKept(met, new Hunter()));
        Assert.False(Career.VowWasKept(unmet, new Hunter()),
            "a sworn-but-unmet Vow must not count — THE OATHBOUND is earned by engaging the system");
    }
}
