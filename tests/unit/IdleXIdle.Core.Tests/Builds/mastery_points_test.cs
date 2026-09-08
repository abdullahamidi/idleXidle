using System.Linq;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>Pins the mastery-point curve (MasteryPoints) — the faucet the tree's pacing hangs on.</summary>
public class mastery_points_test
{
    private static int PathCost(string id)
    {
        var n = MasteryCatalog.ById(id)!;
        if (n.Kind == MasteryKind.Start) return 0;
        return n.Cost + n.Prereqs.Min(PathCost) + (n.SecondPrereqs.Count > 0 ? n.SecondPrereqs.Min(PathCost) : 0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(4, 1)]
    [InlineData(25, 4)]
    [InlineData(40, 5)]
    [InlineData(64, 7)]
    [InlineData(100, 9)]
    [InlineData(150, 11)]
    [InlineData(225, 13)]
    public void test_depth_pays_on_a_square_root(int depth, int points)
        => Assert.Equal(points, MasteryPoints.FromDepth(depth));

    [Fact]
    public void test_fifteen_minutes_cannot_buy_a_specialisation()
    {
        // Playtest 2026-08-25: wave 40 in the first region in the first quarter hour. The shortest path
        // to a specialisation is walked from the catalogue: the trunk, a route's minor, its first skill,
        // two notables, then the specialisation itself (1+1+1+4+3+3+6 = 19 since the path redesign).
        var earned = MasteryPoints.Total(new[] { 40, 0, 0, 0, 0, 0 });
        var cheapest = MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Specialisation).Min(n => PathCost(n.Id));
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
