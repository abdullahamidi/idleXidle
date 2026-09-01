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
    public void test_an_untaught_vow_composes_as_no_vow_and_a_taught_one_pays()
    {
        // Arrange — the same loadout against a tree that never studied, and one that did.
        var loadout = BlowWithVow("vow_complete");
        var ignorant = new MemoryDustTree();
        var studied = new MemoryDustTree();
        studied.Restore(0, new[] { "vow_study_1" });   // teaches VOW OF COMPLETION

        // Act
        var stripped = loadout.ToBuild(ignorant, Taught.Everything(), character: null);
        var sworn = loadout.ToBuild(studied, Taught.Everything(), character: null);

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

        // Assert — the sockets the spine sold are the sockets the build wears.
        Assert.Single(l.KeystoneIds);
    }

    [Fact]
    public void test_the_perfected_tier_pays_the_third_trait_point()
    {
        // Arrange — one region conquered and fought to PERFECTED.
        var world = new World();
        var region = Regions.All[0].Id;
        world.Conquer(region);
        world.RegionFarm(region).RestoreMasteryPoints(AutomationTuning.Default.RmpThreshold3);

        // Act/Assert — 1 conquest + 3 mastery levels. Before P7 the third level was unreachable
        // and this world could only ever pay 3.
        Assert.Equal(MasteryLevel.Perfected, world.RegionFarm(region).MasteryLevel);
        Assert.Equal(4, Career.TraitPointsEarned(world));
    }
}
