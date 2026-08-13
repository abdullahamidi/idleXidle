using System.Linq;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Prestige;
using Xunit;

namespace ResonanceHunter.Core.Tests.Prestige;

/// <summary>
/// What Memory Dust buys. Previously: nothing at all.
/// </summary>
public class DustEffectsTests
{
    private static MemoryDustTree Rich()
    {
        var tree = new MemoryDustTree();
        tree.SetEarned(5000);
        return tree;
    }

    private static MemoryDustTree With(params string[] ids)
    {
        var tree = Rich();
        foreach (var id in ids) Assert.True(tree.Purchase(id), $"could not buy {id}");
        return tree;
    }

    // ── The guard. This is the test that makes the old failure un-repeatable. ─────────────────

    [Fact]
    public void test_every_unlock_in_the_catalog_is_actually_wired_to_something()
    {
        // THE BUG THIS EXISTS FOR: all 19 unlocks were Description strings that lit a constellation
        // node. Owns() was called only by the screen that drew the tree; no gameplay system ever asked.
        // Buying "+1 ROSTER SLOT" did not add a roster slot. A node nothing reads is a lie the game
        // tells for 55 Dust, so it must fail the build instead.
        var unwired = MemoryDustTree.Catalog
            .Select(u => u.Id)
            .Where(id => !DustEffects.WiredIds.Contains(id))
            .ToList();

        Assert.True(unwired.Count == 0,
            $"these unlocks buy NOTHING: {string.Join(", ", unwired)}. Wire them in DustEffects or cut them.");
    }

    [Fact]
    public void test_nothing_is_wired_that_does_not_exist()
    {
        // The mirror: a wire to an id the catalog no longer contains is dead code that will read as
        // working. This is exactly how the old catalog rotted — the systems went away, the nodes stayed.
        var catalog = MemoryDustTree.Catalog.Select(u => u.Id).ToHashSet();
        var orphans = DustEffects.WiredIds.Where(id => !catalog.Contains(id)).ToList();

        Assert.True(orphans.Count == 0, $"DustEffects wires ids that are not in the catalog: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void test_no_unlock_promises_a_system_the_game_deleted()
    {
        // Half the old tree amplified Resonance ("RESONANCE FILLS 5% FASTER FROM DEFENCE", "ABILITIES
        // COST 10% LESS RESONANCE", "RESONANCE CAP RAISED TO 120") — manual combat's meter, deleted —
        // and capture favourability, which was never built. 165 Dust across four nodes changed nothing.
        var dead = new[] { "RESONANCE", "CAPTURE", "SPAWN" };

        foreach (var unlock in MemoryDustTree.Catalog)
            foreach (var word in dead)
                Assert.False(unlock.Description.Contains(word, System.StringComparison.OrdinalIgnoreCase),
                    $"{unlock.Id} promises \"{word}\" — a system this game does not have");
    }

    // ── The passive tree ──────────────────────────────────────────────────────────────────────
    //
    // What used to be here: test_hatchery_actually_adds_squad_slots and its warren twin. There is no
    // squad. The nodes went, the wires went, and these went with them.

    [Fact]
    public void test_an_unbought_tree_changes_nothing()
    {
        Assert.Equal(BuildMods.None, DustEffects.TreeMods(Rich()));
        Assert.Empty(DustEffects.LearnedKeystones(Rich()));
    }

    [Fact]
    public void test_attribute_nodes_actually_reach_the_character()
    {
        // The tree sells CAPACITY now, not numbers — its attribute rungs were the bare multipliers the
        // design forbids and the skill tree owns numbers. One socket to start, three once bought.
        Assert.Equal(1, DustEffects.KeystoneSockets(With()));
        Assert.Equal(2, DustEffects.KeystoneSockets(With("socket_2")));
        Assert.Equal(3, DustEffects.KeystoneSockets(With("socket_2", "weave_5", "socket_3")));
        Assert.Equal(4, DustEffects.SkillSlots(With()));
        Assert.Equal(5, DustEffects.SkillSlots(With("socket_2", "weave_5")));
    }

    /// <summary>
    /// This tree contributes NO numbers, and that is deliberate.
    /// </summary>
    /// <remarks>
    /// It used to compound attribute rungs (1.08 x 1.08 across three tiers, five times over), which is
    /// exactly what made three separate trees feel like one screen in different colours. Numbers belong
    /// to the skill tree; this one sells capacity, keystones and behaviour. A regression that quietly
    /// re-adds a Mods value to a node here would be that failure creeping back.
    /// </remarks>
    [Fact]
    public void test_the_trait_tree_contributes_no_bare_multipliers()
    {
        var everything = new MemoryDustTree();
        everything.SetEarned(everything.TotalTreeCost);
        for (var i = 0; i < 100 && !everything.IsComplete; i++)
            if (everything.All.FirstOrDefault(u => everything.CanUnlock(u.Id)) is { } next)
                everything.Purchase(next.Id);

        Assert.Equal(BuildMods.None, DustEffects.TreeMods(everything));
    }

    [Fact]
    public void test_a_keystone_gate_teaches_its_keystone()
    {
        var learned = DustEffects.LearnedKeystones(With("socket_2", "ks_glass_cannon"));
        Assert.Single(learned);
        Assert.Equal("glass_cannon", learned[0].Id);
    }

    [Fact]
    public void test_a_keystone_gate_does_not_apply_its_keystone()
    {
        // The distinction the whole design rests on: the tree TEACHES, the build WEARS. If buying the
        // node also wore the keystone, a completed tree would wear all ten opposed keystones at once and
        // every build in the game would converge on the same mush.
        var tree = With("socket_2", "ks_glass_cannon");

        // GLASS CANNON is x2.0 damage. The tree contributes no numbers at all, so if the gate applied
        // its keystone this would be 2, not 1.
        Assert.Equal(1f, DustEffects.TreeMods(tree).Damage, 4);
        Assert.Single(DustEffects.LearnedKeystones(tree));
    }

    [Fact]
    public void test_every_keystone_is_teachable_by_some_node()
    {
        // The wire, checked from the far end. A keystone no node teaches is unreachable content — the
        // exact failure this codebase has shipped six times (Weaving, Memory Dust, evolution branches,
        // Charm/Focus, Forge.Feed, item Element): complete, tested, and called by nothing.
        var taught = MemoryDustTree.Catalog
            .Where(u => u.GrantsKeystone is not null)
            .Select(u => u.GrantsKeystone!)
            .ToHashSet();

        foreach (var k in Keystones.Catalog)
            Assert.True(taught.Contains(k.Id), $"no node in the tree teaches '{k.Id}' — it is unreachable");
    }

    [Fact]
    public void test_every_node_that_teaches_names_a_keystone_that_exists()
    {
        // And the near end: a gate naming a keystone the catalog dropped would silently teach nothing.
        foreach (var u in MemoryDustTree.Catalog.Where(u => u.GrantsKeystone is not null))
            Assert.True(Keystones.ById(u.GrantsKeystone!) is not null,
                $"{u.Id} teaches '{u.GrantsKeystone}', which is not a keystone");
    }

    [Fact]
    public void test_the_menu_of_keystones_is_longer_than_the_plate()
    {
        // The property that makes socketing a REFUSAL rather than an inventory chore. If the tree ever
        // teaches only as many keystones as a build can wear, the choice quietly disappears and nobody
        // gets a compile error about it.
        var taught = MemoryDustTree.Catalog.Count(u => u.GrantsKeystone is not null);
        Assert.True(taught > Build.KeystoneSlots,
            $"the tree teaches {taught} keystones for {Build.KeystoneSlots} sockets — nothing is being refused");
    }

    [Fact]
    public void test_reaching_a_keystone_costs_more_than_dust()
    {
        // A keystone gate hanging off the root would make the branch decorative: you would buy every
        // keystone with no detour. The PATH is the cost — walking toward GLASS CANNON is Dust not spent
        // walking toward IRONCLAD.
        foreach (var gate in MemoryDustTree.Catalog.Where(u => u.GrantsKeystone is not null))
            Assert.True(gate.Requires.Count > 0, $"{gate.Id} is a keystone you can buy from the root");
    }

    // ── Rates ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_recall_actually_speeds_mastery()
    {
        Assert.Equal(1f, DustEffects.MasteryRate(Rich()), 3);
        Assert.Equal(1.05f, DustEffects.MasteryRate(With("recall_1")), 3);

        var all = With("recall_1", "recall_2", "recall_3", "recall_4");
        Assert.Equal(1.20f, DustEffects.MasteryRate(all), 3);
    }

    [Fact]
    public void test_efficient_forge_actually_improves_dismantle()
    {
        Assert.Equal(0.4f, DustEffects.DismantleRate(Rich(), 0.4f), 3);
        Assert.True(DustEffects.DismantleRate(With("ledger", "forge_insight", "efficient_forge"), 0.4f) > 0.4f);
    }

    [Fact]
    public void test_dismantle_is_always_a_loss_no_matter_how_much_dust_you_spend()
    {
        // If Dust ever pushed it past 1.0, dismantle would become the strictly-correct action for every
        // item in the game and delete selling outright.
        var tree = With("ledger", "forge_insight", "efficient_forge");
        Assert.True(DustEffects.DismantleRate(tree, 0.9f) < 1f);
        Assert.True(DustEffects.DismantleRate(tree, 5f) < 1f, "an absurd base rate must still clamp below 1");
    }

    // ── Weaving components ────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_fresh_player_knows_no_vows()
    {
        Assert.Empty(DustEffects.KnownVows(Rich()));
        Assert.False(DustEffects.KnowsVow(Rich(), "vow_patience"));
    }

    [Fact]
    public void test_studying_vows_actually_teaches_them()
    {
        var tree = With("vow_study_1");
        Assert.Single(DustEffects.KnownVows(tree));
        Assert.True(DustEffects.KnowsVow(tree, "vow_patience"));
        Assert.False(DustEffects.KnowsVow(tree, "vow_vanguard"));

        var deeper = With("vow_study_1", "vow_study_2");
        Assert.Equal(2, DustEffects.KnownVows(deeper).Count);
        Assert.True(DustEffects.KnowsVow(deeper, "vow_vanguard"));
    }

    [Fact]
    public void test_the_whole_tree_teaches_every_vow_in_the_catalog()
    {
        // A Vow that no unlock grants is a Vow no player can ever swear — the same orphan, wearing the
        // opposite hat.
        var tree = With("vow_study_1", "vow_study_2", "vow_study_3", "vow_binding", "vow_sacrifice");
        var known = DustEffects.KnownVows(tree).Select(v => v.Id).ToHashSet();

        foreach (var vow in ResonanceHunter.Core.Abilities.Weaving.Catalog)
            Assert.Contains(vow.Id, known);
    }

    [Fact]
    public void test_a_vow_grant_never_names_a_vow_that_does_not_exist()
    {
        // The mirror of the above: a grant pointing at a renamed Vow would silently teach nothing.
        var tree = With("vow_study_1", "vow_study_2", "vow_study_3", "vow_binding", "vow_sacrifice");
        Assert.Equal(ResonanceHunter.Core.Abilities.Weaving.Catalog.Count, DustEffects.KnownVows(tree).Count);
    }

    // ── Loot filters ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_loot_filters_actually_filter()
    {
        Assert.Null(DustEffects.AutoSellAtOrBelow(Rich()));
        Assert.Equal(Rarity.Common, DustEffects.AutoSellAtOrBelow(With("ledger", "filter_common")));
        Assert.Equal(Rarity.Uncommon, DustEffects.AutoSellAtOrBelow(With("ledger", "filter_common", "filter_uncommon")));
    }

    [Fact]
    public void test_no_filter_can_ever_eat_a_rare()
    {
        // A filter that could auto-sell Rares would quietly delete the item game, which is the thing
        // the player is actually here for.
        var everything = Rich();
        foreach (var u in MemoryDustTree.Catalog.OrderBy(u => u.Requires.Count).ThenBy(u => u.Cost))
            everything.Purchase(u.Id);

        var threshold = DustEffects.AutoSellAtOrBelow(everything);
        Assert.NotNull(threshold);
        Assert.True(threshold < Rarity.Rare, $"the filter would eat {threshold}s");
    }

    // ── Automation and convenience ────────────────────────────────────────────────────────────

    [Fact]
    public void test_automation_and_convenience_unlocks_report_themselves()
    {
        Assert.False(DustEffects.AutoMergeAfterRuns(Rich()));
        Assert.True(DustEffects.AutoMergeAfterRuns(With("ledger", "forge_insight", "auto_merge")));

        Assert.False(DustEffects.ShowExactNumbers(Rich()));
        Assert.True(DustEffects.ShowExactNumbers(With("ledger")));

        Assert.False(DustEffects.ShowMergePreview(Rich()));
        Assert.True(DustEffects.ShowMergePreview(With("ledger", "forge_insight")));
    }

    [Fact]
    public void test_the_tree_is_still_completable()
    {
        // The horizon is the point: a player must be able to see the end of this system.
        var tree = Rich();
        foreach (var u in MemoryDustTree.Catalog.OrderBy(u => u.Requires.Count).ThenBy(u => u.Cost))
            tree.Purchase(u.Id);

        // Buy again in case a prerequisite ordering left something behind.
        foreach (var u in MemoryDustTree.Catalog) tree.Purchase(u.Id);

        Assert.True(DustEffects.TreeComplete(tree), "the capstone is unreachable — the tree cannot be finished");
    }
}
