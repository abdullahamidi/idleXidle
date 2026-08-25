using ResonanceHunter.Core.Builds;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>Pins the mastery-point curve (MasteryPoints) — the faucet the tree's pacing hangs on.</summary>
public class mastery_points_test
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(4, 1)]
    [InlineData(25, 4)]
    [InlineData(40, 5)]
    [InlineData(100, 8)]
    [InlineData(225, 12)]
    public void test_depth_pays_on_a_square_root(int depth, int points)
        => Assert.Equal(points, MasteryPoints.FromDepth(depth));

    [Fact]
    public void test_fifteen_minutes_cannot_buy_a_specialisation()
    {
        // Playtest 2026-08-25: wave 40 in the first region in the first quarter hour. The shortest path
        // to a specialisation is a minor (1) + a notable (3) + the specialisation itself.
        var earned = MasteryPoints.Total(new[] { 40, 0, 0, 0, 0, 0 });
        var cheapest = MasteryCatalog.RingCost[1] + MasteryCatalog.RingCost[2] + MasteryCatalog.SpecialisationCost;
        Assert.True(earned < cheapest, $"wave 40 paid {earned}, a specialisation path costs {cheapest}");
    }

    [Fact]
    public void test_the_curve_never_goes_backwards()
    {
        var last = 0;
        for (var d = 0; d <= 500; d++)
        {
            var p = MasteryPoints.FromDepth(d);
            Assert.True(p >= last, $"depth {d} paid {p} after {last}");
            last = p;
        }
    }

    [Fact]
    public void test_full_content_stays_under_the_tree()
    {
        // The design's invariant: at full current content roughly a third of the tree is reachable,
        // and two complete branches never are. Six regions walked to wave 150 each.
        var earned = MasteryPoints.Total(new[] { 150, 150, 150, 150, 150, 150 });
        Assert.InRange(earned, 40, MasteryCatalog.TotalCost / 2);
    }
}
