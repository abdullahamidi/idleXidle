using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The mastery layout is a pure function of the catalogue: this holds it apart, inside the rim, and
/// in the shape the design draws — a trunk on the axis, two routes either side, a capstone past them.
/// </summary>
/// <remarks>
/// Rewritten with the path redesign (2026-09-06). The ring formulas the previous suite reproduced are
/// gone; every position is now Route × Step on a polar grid, so what is worth holding is the grid's
/// consequences — clearance, symmetry, the rim — rather than its arithmetic.
/// </remarks>
public class MasteryLayoutTests
{
    private static float Dist(TreePoint a, TreePoint b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static float Radius(TreePoint p) => MathF.Sqrt(p.X * p.X + p.Y * p.Y);

    /// <summary>No two nodes overlap, with a hand's width between them.</summary>
    [Fact]
    public void test_no_two_nodes_in_the_catalogue_overlap()
    {
        const float margin = 40f;
        var nodes = MasteryCatalog.Nodes.ToList();
        for (var i = 0; i < nodes.Count; i++)
            for (var j = i + 1; j < nodes.Count; j++)
            {
                var a = nodes[i];
                var b = nodes[j];
                var need = MasteryLayout.NodeWorldRadius(a.Kind) + MasteryLayout.NodeWorldRadius(b.Kind) + margin;
                var d = Dist(MasteryLayout.PositionOf(a), MasteryLayout.PositionOf(b));
                Assert.True(d >= need, $"{a.Id} and {b.Id} are {d:0} apart; they need {need:0}.");
            }
    }

    /// <summary>Every node sits inside the rim, with its own radius to spare.</summary>
    [Fact]
    public void test_every_node_is_inside_the_rim()
    {
        foreach (var n in MasteryCatalog.Nodes)
        {
            var r = Radius(MasteryLayout.PositionOf(n)) + MasteryLayout.NodeWorldRadius(n.Kind);
            Assert.True(r <= MasteryLayout.WorldRadius, $"{n.Id} reaches {r:0}; the rim is {MasteryLayout.WorldRadius:0}.");
        }
    }

    /// <summary>The first-open framing holds START and the four first trunk nodes, and nothing further.</summary>
    [Fact]
    public void test_the_first_open_radius_holds_the_trunks_first_step_and_nothing_further()
    {
        var inside = MasteryCatalog.Nodes
            .Where(n => Radius(MasteryLayout.PositionOf(n)) + MasteryLayout.NodeWorldRadius(n.Kind) <= MasteryLayout.FirstOpenRadius + 0.5f)
            .Select(n => n.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "bite", "chime", "glean", "hide", "start" }, inside);
    }

    /// <summary>The trunk and the capstone lie on the branch axis; the two routes mirror each other about it.</summary>
    [Fact]
    public void test_trunk_and_capstone_on_the_axis_and_the_routes_mirror()
    {
        foreach (var b in Enum.GetValues<Branch>())
        {
            var axis = MasteryLayout.AngleOf(b);
            foreach (var n in MasteryCatalog.Nodes.Where(n => n.Branch == b && n.Link is null && n.Kind != MasteryKind.Start && n.Route is MasteryRoute.Trunk or MasteryRoute.Capstone))
            {
                var p = MasteryLayout.PositionOf(n);
                var along = p.X * MathF.Cos(axis) + p.Y * MathF.Sin(axis);
                var across = -p.X * MathF.Sin(axis) + p.Y * MathF.Cos(axis);
                Assert.True(MathF.Abs(across) < 0.5f, $"{n.Id} is off its axis by {across:0}.");
                Assert.True(along > 0f, $"{n.Id} is behind START.");
            }

            var left = MasteryCatalog.Nodes.Where(n => n.Branch == b && n.Route == MasteryRoute.Left && n.Kind != MasteryKind.Specialisation).OrderBy(n => n.Step).ToList();
            var right = MasteryCatalog.Nodes.Where(n => n.Branch == b && n.Route == MasteryRoute.Right && n.Kind != MasteryKind.Specialisation).OrderBy(n => n.Step).ToList();
            Assert.Equal(left.Count, right.Count);
            for (var i = 0; i < left.Count; i++)
            {
                var l = MasteryLayout.PositionOf(left[i]);
                var r = MasteryLayout.PositionOf(right[i]);
                Assert.Equal(Radius(l), Radius(r), 1);
                var lAcross = -l.X * MathF.Sin(axis) + l.Y * MathF.Cos(axis);
                var rAcross = -r.X * MathF.Sin(axis) + r.Y * MathF.Cos(axis);
                Assert.True(MathF.Abs(lAcross + rAcross) < 0.5f, $"{left[i].Id} and {right[i].Id} do not mirror.");
                Assert.True(lAcross < 0f && rAcross > 0f, $"{left[i].Id} is not on the left of {right[i].Id}.");
            }
        }
    }

    /// <summary>A route marches outward: each step further from START than the last, and the capstone past them all.</summary>
    [Fact]
    public void test_routes_march_outward_to_the_capstone()
    {
        foreach (var b in Enum.GetValues<Branch>())
        {
            var cap = MasteryCatalog.Nodes.Single(n => n.Branch == b && n.Kind == MasteryKind.Mastery);
            var capR = Radius(MasteryLayout.PositionOf(cap));
            foreach (var route in new[] { MasteryRoute.Left, MasteryRoute.Right })
            {
                var road = MasteryCatalog.Nodes.Where(n => n.Branch == b && n.Route == route && n.Link is null).OrderBy(n => n.Step).ToList();
                var last = Radius(MasteryLayout.PositionOf(MasteryCatalog.Nodes.Single(n => n.Branch == b && n.Route == MasteryRoute.Trunk && n.Step == 1)));
                foreach (var n in road.Where(n => n.Kind != MasteryKind.Specialisation))
                {
                    var r = Radius(MasteryLayout.PositionOf(n));
                    Assert.True(r > last, $"{n.Id} at {r:0} is not past the node before it ({last:0}).");
                    last = r;
                }
                Assert.True(capR > last, $"{cap.Id} at {capR:0} is inside its route ({last:0}).");
            }
        }
    }

    /// <summary>A specialisation is a leaf: further from the axis than its route, beside its parent.</summary>
    [Fact]
    public void test_a_specialisation_hangs_outside_its_route()
    {
        foreach (var s in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Specialisation))
        {
            var axis = MasteryLayout.AngleOf(s.Branch);
            var parent = MasteryCatalog.ById(s.Prereqs.Single())!;
            var sp = MasteryLayout.PositionOf(s);
            var pp = MasteryLayout.PositionOf(parent);
            var sAcross = MathF.Abs(-sp.X * MathF.Sin(axis) + sp.Y * MathF.Cos(axis));
            var pAcross = MathF.Abs(-pp.X * MathF.Sin(axis) + pp.Y * MathF.Cos(axis));
            Assert.True(sAcross > pAcross, $"{s.Id} is not further from the axis than {parent.Id}.");
            Assert.True(Radius(sp) > Radius(pp), $"{s.Id} is not further out than {parent.Id}.");
        }
    }

    /// <summary>A bridge sits at the midpoint angle between its two branches, inside the routes' first steps.</summary>
    [Fact]
    public void test_a_bridge_sits_between_its_branches()
    {
        foreach (var br in MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.Bridge))
        {
            var p = MasteryLayout.PositionOf(br);
            var angle = MathF.Atan2(p.Y, p.X);
            var a = MasteryLayout.AngleOf(br.Branch);
            var c = MasteryLayout.AngleOf(br.Link!.Value);
            static float Wrap(float x) => MathF.Atan2(MathF.Sin(x), MathF.Cos(x));
            Assert.Equal(0f, Wrap(angle - a) + Wrap(angle - c), 3);   // the same angular distance to each axis
            Assert.Equal(MasteryLayout.BridgeRadius, Radius(p), 1);
        }
    }

    /// <summary>The wires never cross a node they are not attached to.</summary>
    [Fact]
    public void test_no_wire_passes_through_an_unrelated_node()
    {
        foreach (var (fromId, toId) in MasteryLayout.Edges)
        {
            var a = MasteryLayout.PositionOf(MasteryCatalog.ById(fromId)!);
            var c = MasteryLayout.PositionOf(MasteryCatalog.ById(toId)!);
            foreach (var n in MasteryCatalog.Nodes.Where(n => n.Id != fromId && n.Id != toId))
            {
                var p = MasteryLayout.PositionOf(n);
                var d = SegmentDistance(a, c, p);
                Assert.True(d > MasteryLayout.NodeWorldRadius(n.Kind), $"the wire {fromId} → {toId} passes through {n.Id} ({d:0}).");
            }
        }
    }

    private static float SegmentDistance(TreePoint a, TreePoint b, TreePoint p)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var len2 = abx * abx + aby * aby;
        var t = len2 <= 0f ? 0f : Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / len2, 0f, 1f);
        return Dist(new TreePoint(a.X + abx * t, a.Y + aby * t), p);
    }

    /// <summary>A specialisation is the second-largest node and a bridge is not — the sizes carry the kinds.</summary>
    [Fact]
    public void test_sizes_rank_the_kinds()
    {
        float R(MasteryKind k) => MasteryLayout.NodeWorldRadius(k);
        Assert.True(R(MasteryKind.Mastery) > R(MasteryKind.Specialisation));
        Assert.True(R(MasteryKind.Specialisation) > R(MasteryKind.SkillRoad));
        Assert.True(R(MasteryKind.SkillRoad) > R(MasteryKind.Greater));
        Assert.True(R(MasteryKind.Greater) > R(MasteryKind.Bridge));
        Assert.True(R(MasteryKind.Bridge) > R(MasteryKind.Notable));
        Assert.True(R(MasteryKind.Notable) > R(MasteryKind.Minor));
    }
}
