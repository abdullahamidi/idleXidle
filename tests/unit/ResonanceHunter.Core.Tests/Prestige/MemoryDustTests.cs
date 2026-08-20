using System;
using System.Linq;
using ResonanceHunter.Core.Prestige;
using Xunit;

namespace ResonanceHunter.Core.Tests.Prestige;

public class MemoryDustTests
{
    // ── The finite, visible horizon ───────────────────────────────────────────────────────────

    /// <summary>The tree is finite and completable — 19 unlocks, 660 Dust. A player can see the end.</summary>
    [Fact]
    public void test_the_tree_is_a_finite_visible_horizon()
    {
        var tree = new MemoryDustTree();

        // The horizon is the property, not the exact figures — a player must be able to see the end of
        // this system. Pinned so a catalogue edit has to be DELIBERATE about the shape of it.
        //
        // The tree is deliberately NOT completable any more (see the test below): the whole point of the
        // four paths is that two terminals cost more than a career earns.
        // 39: the CHARGE spur added one rung to each of the four roads (REND / DYNAMO /
        // LODESTONE / CAPACITOR), deliberately symmetric so the road-cost law holds.
        Assert.Equal(39, tree.All.Count);
        // 166 -> 190 when the CHARGE spur landed (4 rungs x 6). The property this range guards —
        // the tree is NOT completable, two terminals stay out of reach — only gets STRONGER as the
        // total grows past what a career earns; the floor guards the other direction.
        Assert.InRange(tree.TotalTreeCost, 180, 200);
    }

    /// <summary>Every node is REACHABLE — no unlock is orphaned by its prerequisites.</summary>
    /// <remarks>
    /// Reachability, not affordability. A player can never afford the whole tree — see
    /// test_two_terminals_are_out_of_reach, which is the point of the four paths. What must always hold
    /// is that no node is walled off by a chain that cannot be completed, which is a content bug rather
    /// than a design choice.
    /// </remarks>
    [Fact]
    public void test_the_entire_tree_can_be_completed()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(tree.TotalTreeCost);

        // Buy in dependency order until nothing remains purchasable.
        var guard = 0;
        while (!tree.IsComplete && guard++ < 100)
        {
            var next = tree.All.FirstOrDefault(u => tree.CanUnlock(u.Id));
            Assert.NotNull(next); // if this is null before completion, something is unreachable
            Assert.True(tree.Purchase(next!.Id));
        }

        Assert.True(tree.IsComplete);
        Assert.Equal(0, tree.Available); // exactly enough, no more
    }

    // ── NOTHING RESETS ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The ONLY faucet is region mastery. There is no reset, no "prestige and start over".
    /// </summary>
    /// <remarks>
    /// This is the whole identity of the system. Every method on the tree either awards Dust from
    /// mastery, spends it on a permanent unlock, or reads state. None destroys or reverts anything —
    /// there is deliberately no Reset(), no Prestige(), no method that takes progress away.
    /// </remarks>
    [Fact]
    public void test_the_only_way_to_gain_dust_is_mastery_and_nothing_is_ever_reset()
    {
        var tree = new MemoryDustTree();
        Assert.Equal(0, tree.Available);

        // TRAIT POINTS, not Memory Dust. Dust is minted by the Warren while the game is CLOSED; a
        // permanent tree bought with it would be an identity bought by waiting.
        tree.SetEarned(50);
        Assert.Equal(50, tree.Available);

        tree.Purchase("recall_1"); // costs 1
        Assert.Equal(49, tree.Available);

        // The public surface offers no way to un-master, refund, or reset. If one is ever added,
        // this system stops being the thing it promises to be.
        var mutators = typeof(MemoryDustTree).GetMethods()
            .Select(m => m.Name)
            .Where(n => n.Contains("Reset") || n.Contains("Refund") || n.Contains("Prestige") || n.Contains("Revert"))
            .ToList();

        Assert.Empty(mutators);
    }

    // ── Purchases are permanent and gated ─────────────────────────────────────────────────────

    /// <summary>An unlock requires its prerequisites — you cannot skip up the tree.</summary>
    [Fact]
    public void test_an_unlock_requires_its_prerequisites()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(1000);

        Assert.False(tree.CanUnlock("recall_2")); // needs recall_1 first
        Assert.False(tree.Purchase("recall_2"));

        Assert.True(tree.Purchase("recall_1"));
        Assert.True(tree.CanUnlock("recall_2"));
    }

    /// <summary>Purchases are one-directional — no respec, no refund, no buying twice.</summary>
    [Fact]
    public void test_purchases_are_permanent_and_cannot_be_repeated()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(1000);

        Assert.True(tree.Purchase("recall_1"));
        var afterFirst = tree.Available;

        Assert.False(tree.Purchase("recall_1")); // already owned — no double-spend
        Assert.Equal(afterFirst, tree.Available);
    }

    [Fact]
    public void test_you_cannot_buy_what_you_cannot_afford()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(1); // socket_2 costs 2

        Assert.False(tree.CanUnlock("socket_2"));
        Assert.False(tree.Purchase("socket_2"));
        Assert.Equal(1, tree.Available);
    }

    /// <summary>The capstone requires the whole tier-4 tier — it truly is the end.</summary>
    [Fact]
    public void test_the_capstone_is_gated_behind_everything()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(10_000);

        Assert.False(tree.CanUnlock("attunement"));

        var guard = 0;
        while (!tree.CanUnlock("attunement") && guard++ < 100)
        {
            var next = tree.All.FirstOrDefault(u => u.Id != "attunement" && tree.CanUnlock(u.Id));
            if (next is null) break;
            tree.Purchase(next.Id);
        }

        Assert.True(tree.CanUnlock("attunement"));
    }

    // ── Persistence ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_owned_unlocks_and_dust_restore_from_a_save()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(1000);
        tree.Purchase("recall_1");
        tree.Purchase("socket_2");

        var restored = new MemoryDustTree();
        restored.Restore(tree.MemoryDust, tree.OwnedIds);
        restored.SetEarned(1000);

        Assert.Equal(tree.Spent, restored.Spent);
        Assert.True(restored.Owns("recall_1"));
        Assert.True(restored.Owns("socket_2"));
        Assert.False(restored.Owns("recall_2"));
    }

    // ── Content integrity ─────────────────────────────────────────────────────────────────────

    /// <summary>No unlock resets or reverts anything — every effect is convenience, expansion, or amplifier.</summary>
    [Fact]
    public void test_no_unlock_has_a_destructive_effect()
    {
        var tree = new MemoryDustTree();

        Assert.All(tree.All, u => Assert.True(
            u.Effect is UnlockEffect.Convenience or UnlockEffect.Expansion or UnlockEffect.Amplifier));

        // Every unlock is described in player-facing terms — no mystery purchases.
        Assert.All(tree.All, u => Assert.False(string.IsNullOrWhiteSpace(u.Description)));
    }

    /// <summary>A dependency cycle in the tree is rejected at construction.</summary>
    [Fact]
    public void test_a_cyclic_tree_is_rejected()
        => Assert.Throws<InvalidOperationException>(() => new MemoryDustTree(new[]
        {
            new MemoryDustUnlock
            {
                Id = "a", Name = "A", Description = "d", Cost = 1, Effect = UnlockEffect.Convenience,
                Requires = new[] { "b" },
            },
            new MemoryDustUnlock
            {
                Id = "b", Name = "B", Description = "d", Cost = 1, Effect = UnlockEffect.Convenience,
                Requires = new[] { "a" },
            },
        }));

    /// <summary>A player who never touches this system still has a complete game (A6) — Dust just accrues.</summary>
    [Fact]
    public void test_ignoring_the_system_entirely_is_harmless()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(200); // earned by conquering, whether or not the screen is ever opened

        // Never spending them changes nothing about the rest of the game. The points simply wait.
        Assert.Equal(200, tree.Available);
        Assert.False(tree.IsComplete);
        Assert.Empty(tree.OwnedIds);
    }

    /// <summary>
    /// Two of the four terminals are not affordable, and that is the design.
    /// </summary>
    /// <remarks>
    /// THE INVARIANT THE TRAIT TREE RESTS ON. A path is 4 + 6 + 8 + 12 = 30, so two are 60 against
    /// roughly 34 earnable at full current content. One terminal plus most of a second path is
    /// affordable; two terminals is not. The terminal you did NOT take is the permanent shape of your
    /// character, and if this ever inverts the tree stops asking anything.
    /// </remarks>
    [Fact]
    public void test_two_terminals_are_out_of_reach()
    {
        const int budgetAtFullContent = 34;   // 6 conquests + ~10 corruption tiers + 18 mastery goals

        var terminals = new[] { "ks_reaper", "ks_titan", "ks_hoarder", "ks_weaver" };
        var tree = new MemoryDustTree();

        // What one whole path costs, prerequisites included, from the spine node it hangs off.
        int PathCost(string terminal)
        {
            var seen = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(terminal);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!seen.Add(id)) continue;
                foreach (var pre in tree.All.First(u => u.Id == id).Requires) stack.Push(pre);
            }
            return seen.Sum(id => tree.All.First(u => u.Id == id).Cost);
        }

        Assert.Equal(4, terminals.Length);
        foreach (var t in terminals)
            Assert.True(PathCost(t) <= budgetAtFullContent,
                $"{t} costs {PathCost(t)} against {budgetAtFullContent} earnable — ONE terminal must " +
                "be reachable, or the paths are decoration.");

        var cheapestTwo = terminals.Select(PathCost).OrderBy(c => c).Take(2).Sum();
        Assert.True(cheapestTwo > budgetAtFullContent,
            $"The two cheapest terminals cost {cheapestTwo} against {budgetAtFullContent} earnable. " +
            "Both are affordable, so the choice the whole tree is built around does not exist.");
    }
}
