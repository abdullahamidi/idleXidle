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

        // The exact figures are not the property under test — the HORIZON is: a player must be able to
        // see the end of this system rather than face an infinite multiplier grind. These are pinned so
        // that a catalog edit has to be deliberate about the shape of that horizon. (44/1,775 after the
        // release-variety pass added the FORTUNE road, four third-rung nodes, and the FORTUNE + TITAN
        // keystone gates; was 36/1,430 before.)
        Assert.Equal(44, tree.All.Count);
        Assert.Equal(1_775, tree.TotalTreeCost);
    }

    /// <summary>The whole tree can actually be bought — no unlock is unreachable.</summary>
    [Fact]
    public void test_the_entire_tree_can_be_completed()
    {
        var tree = new MemoryDustTree();
        tree.AwardFromMastery(tree.TotalTreeCost);

        // Buy in dependency order until nothing remains purchasable.
        var guard = 0;
        while (!tree.IsComplete && guard++ < 100)
        {
            var next = tree.All.FirstOrDefault(u => tree.CanUnlock(u.Id));
            Assert.NotNull(next); // if this is null before completion, something is unreachable
            Assert.True(tree.Purchase(next!.Id));
        }

        Assert.True(tree.IsComplete);
        Assert.Equal(0, tree.MemoryDust); // exactly enough, no more
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
        Assert.Equal(0, tree.MemoryDust);

        tree.AwardFromMastery(50);
        Assert.Equal(50, tree.MemoryDust);

        tree.Purchase("recall_1"); // costs 10
        Assert.Equal(40, tree.MemoryDust);

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
        tree.AwardFromMastery(1000);

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
        tree.AwardFromMastery(1000);

        Assert.True(tree.Purchase("recall_1"));
        var afterFirst = tree.MemoryDust;

        Assert.False(tree.Purchase("recall_1")); // already owned — no double-spend
        Assert.Equal(afterFirst, tree.MemoryDust);
    }

    [Fact]
    public void test_you_cannot_buy_what_you_cannot_afford()
    {
        var tree = new MemoryDustTree();
        tree.AwardFromMastery(5); // recall_1 costs 10

        Assert.False(tree.CanUnlock("recall_1"));
        Assert.False(tree.Purchase("recall_1"));
        Assert.Equal(5, tree.MemoryDust);
    }

    /// <summary>The capstone requires the whole tier-4 tier — it truly is the end.</summary>
    [Fact]
    public void test_the_capstone_is_gated_behind_everything()
    {
        var tree = new MemoryDustTree();
        tree.AwardFromMastery(10_000);

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
        tree.AwardFromMastery(1000);
        tree.Purchase("recall_1");
        tree.Purchase("might_1");

        var restored = new MemoryDustTree();
        restored.Restore(tree.MemoryDust, tree.OwnedIds);

        Assert.Equal(tree.MemoryDust, restored.MemoryDust);
        Assert.True(restored.Owns("recall_1"));
        Assert.True(restored.Owns("might_1"));
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
        tree.AwardFromMastery(200); // earned passively while playing

        // Never spending it changes nothing about the rest of the game. The Dust simply waits.
        Assert.Equal(200, tree.MemoryDust);
        Assert.False(tree.IsComplete);
        Assert.Empty(tree.OwnedIds);
    }
}
