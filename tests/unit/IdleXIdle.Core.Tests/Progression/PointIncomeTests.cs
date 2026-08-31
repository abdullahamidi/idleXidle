using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Warrens;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// Where progress comes from — the rules that decide whether the trees mean anything.
/// </summary>
/// <remarks>
/// A skill point has value only if it is scarce and only if it is EARNED BY PLAYING. The build tree
/// used to be funded by the Warren's mastery pool, which meant idle time bought build identity and no
/// node in the tree was ever a choice. These tests hold the two rules that replaced that.
/// </remarks>
public class PointIncomeTests
{
    private static Region RegionOf(string id) => new(id, parClearTimeSeconds: 60);

    /// <summary>Points are paid for NEW depth, once.</summary>
    /// <remarks>
    /// Re-clearing a depth already held pays haul, materials and mastery — everything except tree
    /// points. Otherwise the cheapest way to fill a tree is to farm wave 5 forever, and depth, which is
    /// the game's only real measure of a build, stops being the thing progress is made of.
    /// </remarks>
    [Fact]
    public void test_depth_pays_only_the_first_time_it_is_reached()
    {
        var region = RegionOf("cinderworks");

        Assert.True(region.RecordDepth(20));
        Assert.False(region.RecordDepth(20));
        Assert.False(region.RecordDepth(11));
        Assert.True(region.RecordDepth(21));
        Assert.Equal(21, region.BestDepth);
    }

    /// <summary>Each region banks its own record.</summary>
    /// <remarks>
    /// With a single global deepest-ever, a player who pushes one region to depth 60 walks into every
    /// other region already holding the whole tree — so five of the six regions have no progression in
    /// them at all, and the counter-band rule they are built around never gets to teach anybody
    /// anything.
    /// </remarks>
    [Fact]
    public void test_depth_records_are_per_region()
    {
        var deep = RegionOf("cinderworks");
        var fresh = RegionOf("umbral_reach");

        deep.RecordDepth(60);

        Assert.Equal(60, deep.BestDepth);
        Assert.Equal(0, fresh.BestDepth);
    }

    /// <summary>Facilities cannot climb past the champion's proven depth.</summary>
    /// <remarks>
    /// The idle layer multiplies progress; it never substitutes for it. Uncapped, a long enough absence
    /// funds gear the descent never earned and the answer to "how do I get stronger" becomes "close
    /// the game" — which is the failure mode the whole flow document is written against.
    /// </remarks>
    [Fact]
    public void test_a_facility_cannot_be_raised_past_the_depth_ceiling()
    {
        var warren = new Warren() { FacilityLevelCap = 1 };
        const long plenty = 1_000_000_000;

        Assert.True(warren.CanAfford(FacilityKind.Nursery, plenty, plenty, plenty),
            "The fixture must be able to afford the upgrade, or this proves nothing about the cap.");
        Assert.False(warren.CanUpgrade(FacilityKind.Nursery, plenty, plenty, plenty));

        warren.FacilityLevelCap = 4;
        Assert.True(warren.CanUpgrade(FacilityKind.Nursery, plenty, plenty, plenty));
    }

    /// <summary>The cap binds every facility, not the selected one.</summary>
    [Fact]
    public void test_the_depth_ceiling_binds_every_facility()
    {
        var warren = new Warren() { FacilityLevelCap = 1 };
        const long plenty = 1_000_000_000;

        foreach (var kind in warren.AllFacilities.Select(f => f.Kind))
            Assert.False(warren.CanUpgrade(kind, plenty, plenty, plenty),
                $"{kind} escaped the depth ceiling — one uncapped facility is enough to reopen the hole.");
    }
}
