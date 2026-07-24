using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The mastery tree is a PATH walked outward, not a checklist. These lock in the rules that make it a
/// tree: you can't take a node without a neighbour, points gate it, and only one Form can be mastered.
/// </summary>
public class MasteryTreeTests
{
    private static MasteryTree WithPoints(int p)
    {
        var t = new MasteryTree();
        t.SetEarned(p);
        return t;
    }

    [Fact]
    public void test_a_node_cannot_be_taken_without_a_taken_neighbour()
    {
        var t = WithPoints(5);
        // The Strike MASTERY hangs off the whole arm; you cannot leap straight to it.
        Assert.False(t.CanTake("strike_m"));
        Assert.True(t.CanTake("strike_1"));   // this one hangs off START
    }

    [Fact]
    public void test_walking_a_path_reaches_the_mastery()
    {
        var t = WithPoints(4);
        Assert.True(t.Take("strike_1"));
        Assert.True(t.Take("strike_2"));
        Assert.True(t.Take("strike_n"));
        Assert.True(t.Take("strike_m"));
        Assert.Equal(Form.Strike, t.MasteryForm());
    }

    [Fact]
    public void test_points_gate_allocation()
    {
        var t = WithPoints(1);
        Assert.True(t.Take("strike_1"));
        Assert.Equal(0, t.Available);
        Assert.False(t.CanTake("strike_2"));   // out of points, even though the neighbour is taken
    }

    [Fact]
    public void test_only_one_form_can_be_mastered()
    {
        var t = WithPoints(20);
        foreach (var id in new[] { "strike_1", "strike_2", "strike_n", "strike_m" }) Assert.True(t.Take(id));
        // Walk the Volley arm too, but its Mastery must be refused — you already mastered Strike.
        foreach (var id in new[] { "volley_1", "volley_2", "volley_n" }) Assert.True(t.Take(id));
        Assert.False(t.CanTake("volley_m"));
        Assert.Equal(Form.Strike, t.MasteryForm());
    }

    [Fact]
    public void test_taken_nodes_add_their_mods_and_triggers()
    {
        var t = WithPoints(10);
        foreach (var id in new[] { "volley_1", "volley_2", "volley_n" }) t.Take(id);

        Assert.True(t.Mods().SkillRate > 1f, "the Volley minors raise skill rate");
        Assert.Contains(BuildTrigger.Overdraw, t.Triggers());   // the notable grants it
    }

    [Fact]
    public void test_a_bridge_is_reachable_from_either_arm_it_links()
    {
        // Take only the first Strike minor. The Strike/Volley bridge hangs off strike_1 OR volley_1,
        // so one of them is enough — that is the cross-link that makes it a web, not six spokes.
        var t = WithPoints(5);
        Assert.True(t.Take("strike_1"));
        Assert.True(t.CanTake("bridge_strike_volley"));
    }

    [Fact]
    public void test_respec_gives_every_point_back()
    {
        var t = WithPoints(4);
        t.Take("trap_1");
        t.Take("trap_2");
        Assert.Equal(2, t.Spent);

        t.Respec();

        Assert.Equal(0, t.Spent);
        Assert.Equal(4, t.Available);
        Assert.False(t.IsTaken("trap_2"));
        Assert.Null(t.MasteryForm());
    }

    [Fact]
    public void test_earned_points_survive_a_restore()
    {
        var t = WithPoints(6);
        t.Take("aura_1");
        t.Take("aura_2");

        var restored = new MasteryTree();
        restored.SetEarned(t.Earned);
        restored.RestoreTaken(t.Taken);

        Assert.Equal(t.Available, restored.Available);
        Assert.True(restored.IsTaken("aura_2"));
        Assert.Equal(t.Mods().Health, restored.Mods().Health);
    }
}
