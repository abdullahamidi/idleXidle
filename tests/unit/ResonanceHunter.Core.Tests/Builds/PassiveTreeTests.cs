using System;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Prestige;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The passive tree, followed all the way from a purchase to a number in a fight.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every other test in this repo proves the math. These prove the WIRE.</b> That distinction has
/// cost this project six systems — Weaving, Memory Dust, evolution branches, Charm/Focus, Forge.Feed
/// and item Element were each complete, each fully tested, and each called by nothing at all. A green
/// suite said so every time. The two questions that would have caught all six are "who calls this?"
/// and "who writes this?", and a unit test asking only "is the arithmetic right?" answers neither.
/// </para>
/// <para>
/// So these tests start at <see cref="MemoryDustTree.Purchase"/> and end at damage dealt to an enemy,
/// and they refuse to know anything about the middle. If someone deletes the line that assigns
/// <see cref="Build.PassiveMods"/>, every test in <c>DustEffectsTests</c> and <c>BuildTests</c> keeps
/// passing and these fail. That is the whole reason the file exists.
/// </para>
/// </remarks>
public class PassiveTreeTests
{
    private static MemoryDustTree Bought(params string[] ids)
    {
        var tree = new MemoryDustTree();
        tree.AwardFromMastery(5_000);
        foreach (var id in ids)
            Assert.True(tree.Purchase(id), $"could not buy {id} — prerequisite or cost changed");
        return tree;
    }

    /// <summary>Wire the tree to the build the way the game is expected to.</summary>
    private static Build BuildFrom(MemoryDustTree tree, params string[] socket)
    {
        var build = new Build { PassiveMods = DustEffects.TreeMods(tree) };

        var learned = DustEffects.LearnedKeystones(tree);
        foreach (var id in socket)
        {
            var k = learned.FirstOrDefault(x => x.Id == id);
            Assert.True(k is not null, $"the tree never taught '{id}' — buy its gate first");
            Assert.True(build.Take(k!), $"could not socket '{id}'");
        }

        return build;
    }

    private static float DamageDealt(Build build)
    {
        var hunter = new Hunter();
        var champ = new Champion { MaxHealth = 500, Health = 500 };
        // Spelled out from the root, and it has to be: inside an object initializer `Source = Source.Body`
        // resolves the right-hand side against the PROPERTY, and the obvious repair — `Abilities.Source`
        // — then binds to Tests.Abilities instead. Two shadows deep, the same family of trap that forced
        // Core.Forge to become Core.Forging.
        build.Weave(new EquippedSkill(
            new WovenAbility
            {
                Name = "cut", Source = global::ResonanceHunter.Core.Automation.Source.Body, Form = Form.Strike,
            }, 1_500));

        // A wall: enough health to survive the ceiling, and it never swings back. What comes out is a
        // clean measure of output and nothing else.
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, hunter,
            enemyHealth: 1_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_000,
            ExpeditionTuning.Default, new Random(1234));

        return events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
    }

    // ── The wire ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_passive_tree_reaches_the_fight()
    {
        // Buying nodes and dealing more damage are separated by four systems here, and the ONLY claim
        // under test is that they are connected. PassiveMods left at None reads exactly like a player
        // who bought nothing; this is what tells those two apart.
        var naked = DamageDealt(BuildFrom(new MemoryDustTree()));
        var might = DamageDealt(BuildFrom(Bought("might_1", "might_2")));

        Assert.True(might > naked,
            $"two DAMAGE nodes changed nothing ({naked} -> {might}) — Build.PassiveMods is not wired");

        // A RANGE, not an equality: every hit is banked as (int)MathF.Round, so forty rounded hits are
        // not the float total scaled — they drift by up to half a point each. Demanding 1.1664 exactly
        // would be demanding the sim stop rounding, which is not the property under test.
        Assert.InRange(might / naked, 1.15f, 1.19f);   // 1.08 x 1.08 = 1.1664
    }

    [Fact]
    public void test_a_grit_node_reaches_the_fight()
    {
        // Health is wired through a different path than damage — mods.Health DIVIDES incoming rather
        // than multiplying outgoing — so it needs its own wire check, not a corollary of the last one.
        var hunter = new Hunter();

        // Deep enough to still be standing at the 120-second ceiling — that is 120 swings at 10 apiece,
        // so anything under 1,200 HP measures nothing. The first cut of this gave the champion 100 HP and
        // asserted on the survivor's health: both runs died, both reported 0, and "two HEALTH nodes
        // changed nothing" was the TEST failing to measure, dressed up as the wire failing to carry. The
        // Assert.Alive below is there so it can never lie that way again.
        int Survive(Build build)
        {
            var champ = new Champion { MaxHealth = 3_000, Health = 3_000 };
            SoloBattle.ResolveWave(champ, build, hunter,
                enemyHealth: 1_000_000f, enemyDamage: 10f, enemyIntervalMs: 1_000,
                ExpeditionTuning.Default, new Random(7));

            Assert.True(champ.Alive, "the champion died — this test measures damage TAKEN, not who won");
            return champ.Health;
        }

        var naked = Survive(BuildFrom(new MemoryDustTree()));
        var tough = Survive(BuildFrom(Bought("grit_1", "grit_2")));

        Assert.True(tough > naked, $"two HEALTH nodes changed nothing ({naked} -> {tough})");
    }

    [Fact]
    public void test_a_socketed_keystone_reaches_the_fight()
    {
        var tree = Bought("might_1", "might_2", "ks_glass_cannon");

        var walked = DamageDealt(BuildFrom(tree));                    // path only
        var socketed = DamageDealt(BuildFrom(tree, "glass_cannon"));  // path + the keystone worn

        Assert.InRange(socketed / walked, 1.95f, 2.05f);              // x2.0, minus per-hit rounding
    }

    [Fact]
    public void test_learning_a_keystone_is_not_wearing_it()
    {
        // The load-bearing distinction. If buying the gate applied the keystone, this would be 2x.
        var gated = DamageDealt(BuildFrom(Bought("might_1", "might_2", "ks_glass_cannon")));
        var pathOnly = DamageDealt(BuildFrom(Bought("might_1", "might_2")));

        Assert.Equal(pathOnly, gated, 0);
    }

    // ── The refusal ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_completed_tree_still_cannot_wear_everything()
    {
        // THE point of the socket cap, stated as the scenario it exists to prevent. The tree is
        // completable on purpose — a player deserves to see the horizon — so the ONLY thing standing
        // between "I finished the tree" and "I wear all ten opposed keystones and stopped choosing" is
        // Build.KeystoneSlots. If it is ever removed to be generous, this is the test that objects.
        var tree = new MemoryDustTree();
        tree.AwardFromMastery(tree.TotalTreeCost);
        while (!tree.IsComplete)
        {
            var next = tree.All.FirstOrDefault(u => tree.CanUnlock(u.Id));
            Assert.True(next is not null, "the tree stalled before completion — it is not buyable in full");
            tree.Purchase(next!.Id);
        }

        var learned = DustEffects.LearnedKeystones(tree);
        Assert.Equal(Keystones.Catalog.Count, learned.Count);   // everything is KNOWN

        var build = new Build();
        var worn = learned.Count(k => build.Take(k));
        Assert.Equal(Build.KeystoneSlots, worn);                // and most of it must stay on the shelf
    }

    [Fact]
    public void test_the_branches_pull_against_each_other()
    {
        // A tree whose branches all improve the same thing is a shopping list with extra steps. MIGHT
        // must buy something GRIT does not, or walking one instead of the other decides nothing.
        var might = DustEffects.TreeMods(Bought("might_1", "might_2"));
        var grit = DustEffects.TreeMods(Bought("grit_1", "grit_2"));

        Assert.True(might.Damage > grit.Damage, "MIGHT does not out-damage GRIT");
        Assert.True(grit.Health > might.Health, "GRIT does not out-live MIGHT");
    }

    [Fact]
    public void test_every_keystone_gate_sits_behind_attribute_nodes_of_its_own_branch()
    {
        // A gate whose prerequisite is another GATE would let a player collect keystones without ever
        // walking a branch, and the path — which is the actual cost — would evaporate.
        var byId = MemoryDustTree.Catalog.ToDictionary(u => u.Id);

        foreach (var gate in MemoryDustTree.Catalog.Where(u => u.GrantsKeystone is not null))
        {
            foreach (var req in gate.Requires)
            {
                Assert.True(byId[req].GrantsKeystone is null,
                    $"{gate.Id} is reached through another keystone gate ({req}) — the branch is a chain, not a path");
            }
        }
    }
}
