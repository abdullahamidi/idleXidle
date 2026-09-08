using System.Linq;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// ONE DISCIPLINE PER HUNTER — the tree refuses a second Specialisation out loud.
/// </summary>
/// <remarks>
/// Before the gate, a second Specialisation was legal and silently counted for nothing:
/// Affinity() follows the FIRST, so the player read "hybridised" while the style ring ignored them —
/// the exact dormant-rule species this project hunts. The gate lives in CanTake; this pins it.
/// </remarks>
public class OneDisciplineTest
{
    [Fact]
    public void test_mastery_tree_refuses_a_second_specialisation_and_frees_it_on_refund()
    {
        // Arrange: walk RESONANCE's trunk and both routes to their last notables legitimately, then
        // attune to STRIKE (Style.Hammer) — the leaf off DEEP. TRAP is the leaf off NARROW.
        var tree = new MasteryTree();
        tree.SetEarned(80);
        foreach (var id in new[] { "chime", "tone", "steep", "road_hammer", "keyed", "deep",
                                   "clarity_res", "road_snare", "pledge", "narrow" })
            Assert.True(tree.Take(id), $"fixture could not take {id}");
        Assert.True(tree.Take("spec_strike"));
        Assert.Equal(Style.Hammer, tree.Affinity());

        // Act + Assert: the second discipline is refused — same branch, prereqs met, points there.
        Assert.False(tree.CanTake("spec_trap"));
        Assert.False(tree.Take("spec_trap"));
        Assert.Equal(Style.Hammer, tree.Affinity());

        // And a refund reopens the choice — attunement is one-at-a-time, not once-forever.
        Assert.True(tree.Refund("spec_strike"));
        Assert.Null(tree.Affinity());
        Assert.True(tree.CanTake("spec_trap"));
    }
}
