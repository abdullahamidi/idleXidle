using System;
using System.Linq;
using System.Reflection;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// PURCHASING A NODE MUST NEVER CHANGE THE TREE'S GRAPH TOPOLOGY.
/// </summary>
/// <remarks>
/// The drawn graph — every node's position and every wire — is a function of the catalogue alone.
/// Before the path redesign the screen chose one wire per node from an "any of these" list, preferring
/// a taken prerequisite, so a purchase re-routed the lines a player had planned against. This holds
/// the layout to the law three ways: by measurement (positions and edges identical before and after
/// buying everything a career could buy), by construction (nothing in the layout's public surface
/// accepts a tree or a predicate), and by the graph's own consistency (one edge per parent, no loose
/// ends, no islands).
/// </remarks>
public class mastery_topology_test
{
    private static (TreePoint[] Positions, (string, string)[] Edges) Snapshot()
        => (MasteryCatalog.Nodes.Select(MasteryLayout.PositionOf).ToArray(),
            MasteryLayout.Edges.Select(e => (e.From, e.To)).ToArray());

    [Fact]
    public void test_positions_and_edges_do_not_move_when_nodes_are_bought()
    {
        var before = Snapshot();

        var t = new MasteryTree();
        t.SetEarned(10_000);
        bool progress;
        do
        {
            progress = false;
            foreach (var n in MasteryCatalog.Nodes)
                if (!t.IsTaken(n.Id) && t.Take(n.Id)) progress = true;
        } while (progress);
        Assert.True(t.Spent > 100, "the walk bought almost nothing; the test is not exercising the tree.");

        var after = Snapshot();
        Assert.Equal(before.Positions, after.Positions);
        Assert.Equal(before.Edges, after.Edges);

        t.Respec();
        var reset = Snapshot();
        Assert.Equal(before.Positions, reset.Positions);
        Assert.Equal(before.Edges, reset.Edges);
    }

    [Fact]
    public void test_the_layout_cannot_read_an_allocation()
    {
        var members = typeof(MasteryLayout).GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        foreach (var m in members.OfType<MethodInfo>())
            foreach (var p in m.GetParameters())
            {
                Assert.NotEqual(typeof(MasteryTree), p.ParameterType);
                Assert.False(typeof(Delegate).IsAssignableFrom(p.ParameterType),
                    $"MasteryLayout.{m.Name} takes a {p.ParameterType.Name}; a predicate is how an allocation gets in.");
            }
    }

    [Fact]
    public void test_the_drawn_graph_is_exactly_the_prerequisites()
    {
        var expected = MasteryCatalog.Nodes
            .SelectMany(n => n.Prereqs.Concat(n.SecondPrereqs).Select(p => (From: p, To: n.Id)))
            .OrderBy(e => e.To).ThenBy(e => e.From).ToList();
        var drawn = MasteryLayout.Edges.OrderBy(e => e.To).ThenBy(e => e.From).ToList();
        Assert.Equal(expected, drawn);
        Assert.Equal(MasteryLayout.Edges.Count, MasteryLayout.Edges.Distinct().Count());
    }

    [Fact]
    public void test_every_edge_joins_two_real_nodes_and_nothing_is_an_island()
    {
        foreach (var (from, to) in MasteryLayout.Edges)
        {
            Assert.NotNull(MasteryCatalog.ById(from));
            Assert.NotNull(MasteryCatalog.ById(to));
            Assert.NotEqual(from, to);
        }
        foreach (var n in MasteryCatalog.Nodes.Where(n => n.Kind != MasteryKind.Start))
            Assert.Contains(MasteryLayout.Edges, e => e.To == n.Id);
        Assert.DoesNotContain(MasteryLayout.Edges, e => e.To == MasteryCatalog.StartId);
    }

    [Fact]
    public void test_a_hunter_can_never_hold_two_capstones_or_two_specialisations()
    {
        var t = new MasteryTree();
        t.SetEarned(10_000);
        bool progress;
        do
        {
            progress = false;
            foreach (var n in MasteryCatalog.Nodes)
                if (!t.IsTaken(n.Id) && t.Take(n.Id)) progress = true;
        } while (progress);

        Assert.Equal(1, MasteryCatalog.Nodes.Count(n => n.Kind == MasteryKind.Mastery && t.IsTaken(n.Id)));
        Assert.Equal(1, MasteryCatalog.Nodes.Count(n => n.Kind == MasteryKind.Specialisation && t.IsTaken(n.Id)));
        Assert.True(t.Spent < MasteryCatalog.TotalCost, "the whole tree was bought; the one-per-hunter rules are not holding.");
    }

    [Fact]
    public void test_the_whole_tree_costs_more_than_three_careers()
    {
        var career = MasteryPoints.Total(new[] { 225, 225, 225, 225, 225, 225 });
        Assert.True(MasteryCatalog.TotalCost > career * 3, $"the tree costs {MasteryCatalog.TotalCost} against a career's {career}.");
    }
}
