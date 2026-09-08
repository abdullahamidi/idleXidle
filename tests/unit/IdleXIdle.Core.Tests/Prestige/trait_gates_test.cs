using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Tests.Builds;   // the Taught mastery helper
using Xunit;

namespace IdleXIdle.Core.Tests.Prestige;

/// <summary>
/// THE TRAIT TREE'S GATES REACH THE SIM (P7). Until now the vow-study chain gated only what the
/// weave MENU offered, and the socket nodes gated only the toggle — a restored save wore any vow
/// and three keystones over one bought socket, so eighteen spine points sold rules the fight
/// never checked.
/// </summary>
public class TraitGatesTest
{
    private static PlayerLoadout BlowWithVow(string vowId)
    {
        var l = new PlayerLoadout { SkillCapacity = 1 };
        l.SetSkill(l.AddSkill(), "hammer_blow");
        l.SetVow(0, vowId, Vows.Catalog);
        return l;
    }

    [Fact]
    public void test_an_undiscovered_vow_composes_as_no_vow_and_a_found_one_pays()
    {
        // Arrange — the same loadout against an account that has found nothing, and one that has found
        // this vow. (This used to be two Memory trees, one with the study node bought; the tree is
        // deleted and a Vow is FOUND by keeping its rule once without it.)
        var loadout = BlowWithVow("vow_complete");
        var found = Vows.Catalog.Where(v => v.Id == "vow_complete").ToList();

        // Act
        var stripped = loadout.ToBuild(Taught.Everything(), character: null);
        var sworn = loadout.ToBuild(Taught.Everything(), character: null, knownVows: found);

        // Assert — the SKILL survives either way; only the unpaid promise is refused.
        Assert.Null(stripped.Skills.Single().Vow);
        Assert.Equal("vow_complete", sworn.Skills.Single().Vow?.Id);
    }

    [Fact]
    public void test_restore_honours_the_bought_socket_count()
    {
        // Arrange — one bought socket, a save wearing three keystones (written when all were free).
        var l = new PlayerLoadout { KeystoneCapacity = 1 };

        // Act
        l.Restore(
            new (string?, string?, string?, string?, bool?)[]
            {
                ("hammer_blow", "Body", null, null, false),
            },
            new[] { "glass_cannon", "ironclad", "echo" });

        // Assert — the capacity the account has earned is the capacity the build wears.
        Assert.Single(l.KeystoneIds);
    }

    [Fact]
    public void test_a_conquered_region_can_reach_the_perfected_mastery_tier()
    {
        // Arrange — one region conquered and fought to PERFECTED.
        var world = new World();
        var region = Regions.All[0].Id;
        world.Conquer(region);
        world.RegionFarm(region).RestoreMasteryPoints(AutomationTuning.Default.RmpThreshold3);

        // Act/Assert — the top tier is reachable at all. This used to also assert the four TRAIT POINTS
        // that world paid; trait points were the retired Memory tree's currency and are deleted with it.
        // Region mastery itself is progression and survives untouched, which is what this now pins.
        Assert.Equal(MasteryLevel.Perfected, world.RegionFarm(region).MasteryLevel);
    }
}
