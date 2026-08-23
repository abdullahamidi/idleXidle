using System;
using System.Collections.Generic;
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
        tree.SetEarned(5_000);
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
        // Buying nodes and fighting differently are separated by four systems here, and the ONLY claim
        // under test is that they are connected.
        //
        // It reads a KEYSTONE now, not an attribute node. The tree's flat rungs (+8% DAMAGE, three
        // times, and the same for health, tempo, haul and rarity) are gone: they were the bare
        // multipliers the design forbids, they made this tree read as the skill tree in another colour,
        // and numbers are the skill tree's job. What is left is what only a permanent tree can sell.
        var tree = Bought("socket_2", "ks_glass_cannon");

        var naked = DamageDealt(BuildFrom(new MemoryDustTree()));
        var socketed = DamageDealt(BuildFrom(tree, "glass_cannon"));

        Assert.True(socketed > naked,
            $"a learned and socketed keystone changed nothing ({naked} -> {socketed}) — the tree is " +
            "not reaching the fight at all");
        Assert.InRange(socketed / naked, 1.9f, 2.1f);   // GLASS CANNON is x2.0, minus per-hit rounding
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
        var tough = Survive(BuildFrom(Bought("socket_2", "ks_ironclad"), "ironclad"));

        Assert.True(tough > naked, $"a socketed IRONCLAD changed nothing ({naked} -> {tough})");
    }

    [Fact]
    public void test_a_socketed_keystone_reaches_the_fight()
    {
        var tree = Bought("socket_2", "ks_glass_cannon");

        var walked = DamageDealt(BuildFrom(tree));                    // path only
        var socketed = DamageDealt(BuildFrom(tree, "glass_cannon"));  // path + the keystone worn

        Assert.InRange(socketed / walked, 1.95f, 2.05f);              // x2.0, minus per-hit rounding
    }

    [Fact]
    public void test_learning_a_keystone_is_not_wearing_it()
    {
        // The load-bearing distinction. If buying the gate applied the keystone, this would be 2x.
        var gated = DamageDealt(BuildFrom(Bought("socket_2", "ks_glass_cannon")));
        var pathOnly = DamageDealt(BuildFrom(Bought("socket_2")));

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
        tree.SetEarned(tree.TotalTreeCost);
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
        // A tree whose paths all improve the same thing is a shopping list with extra steps. RUIN must
        // buy something AEGIS does not, or walking one instead of the other decides nothing. Read off
        // the terminals, because a terminal is what the path is FOR.
        var ruin = Keystones.ById("reaper")!.Mods;
        var aegis = Keystones.ById("titan")!.Mods;
        var avarice = Keystones.ById("hoarder")!.Mods;

        Assert.True(aegis.Health > ruin.Health, "AEGIS does not out-live RUIN");
        Assert.True(ruin.SkillRate > aegis.SkillRate || ruin.Damage > aegis.Damage,
            "RUIN buys neither speed nor damage over AEGIS");
        Assert.True(avarice.Rarity < 1f, "AVARICE's terminal charges nothing for its force");
    }

    [Fact]
    public void test_each_path_is_a_chain_of_four_rising_costs()
    {
        // THE PATHS ARE CHAINS NOW, and deliberately so — the old rule was the opposite ("a gate must
        // never hang off another gate") because gates hung off attribute rungs that no longer exist.
        // What replaced that rule is this: three keystones and a terminal at 4 / 6 / 8 / 12, so the
        // terminal costs more than the rest of its path and cannot be splashed.
        var terminals = new[] { "ks_reaper", "ks_titan", "ks_hoarder", "ks_weaver" };
        var byId = MemoryDustTree.Catalog.ToDictionary(u => u.Id);

        foreach (var terminalId in terminals)
        {
            var chain = new List<MemoryDustUnlock>();
            var id = terminalId;
            while (true)
            {
                var node = byId[id];
                chain.Add(node);
                // Walk back along the path — the prerequisite that is itself part of a path.
                var next = node.Requires.FirstOrDefault(r => r.StartsWith("ks_") || r == "artifice_vows");
                if (next is null) break;
                id = next;
            }
            chain.Reverse();

            Assert.Equal(4, chain.Count);
            Assert.Equal(new[] { 4, 6, 8, 12 }, chain.Select(n => n.Cost).ToArray());
            Assert.True(chain[^1].Cost > chain[0].Cost + chain[1].Cost,
                $"{terminalId} costs less than the two rungs that open its path — a terminal has to be " +
                "the commitment, or a player reaches it as a side effect of browsing.");
        }
    }

    /// <summary>Every terminal is a keystone the game can actually teach.</summary>
    /// <remarks>
    /// HOARDER and WEAVER were authored for this rebuild; the design named them as missing. A terminal
    /// that grants nothing is twelve points for a label.
    /// </remarks>
    [Fact]
    public void test_every_terminal_teaches_a_keystone()
    {
        foreach (var id in new[] { "ks_reaper", "ks_titan", "ks_hoarder", "ks_weaver" })
        {
            var node = MemoryDustTree.Catalog.First(u => u.Id == id);
            Assert.NotNull(node.GrantsKeystone);
            Assert.NotNull(Keystones.ById(node.GrantsKeystone));
        }
    }

    [Fact]
    public void test_a_minor_strand_node_reaches_the_tree_mods_and_the_fight()
    {
        // The twelve MINOR STRANDS (2026-08-23) are BuildMods-only nodes: TreeMods must carry them, and a
        // build wearing one must hit harder than the same build without it — otherwise the node is a
        // label. (The old "the tree contributes no bare multipliers" pin would have stayed green with
        // every strand dead.)
        var walked = Bought("socket_2", "ks_glass_cannon", "ks_bloodlust");
        var sharpened = Bought("socket_2", "ks_glass_cannon", "ks_bloodlust", "ruin_edge_1");
        Assert.True(sharpened.Owns("ruin_edge_1"), "the fixture must actually own SHARP EDGE");
        Assert.Equal(1.05f, DustEffects.TreeMods(sharpened).Damage, 3);
        Assert.True(DamageDealt(BuildFrom(sharpened)) > DamageDealt(BuildFrom(walked)) * 1.02f,
            "SHARP EDGE must reach the fight");
    }
}
