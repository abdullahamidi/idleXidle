using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Prestige;
using Xunit;

namespace IdleXIdle.Core.Tests.Prestige;

/// <summary>
/// The traits rework of 2026-08-25: names that say what a node does, roads with a one-line identity,
/// ids that never move, and a layout where no two nodes overlap.
/// </summary>
/// <remarks>
/// Playtest: "players could not connect the traits to the game and to how it plays". The old names were
/// metaphors ("SHARP EDGE", "THE GLASS ROAD", "SORTER'S EYE") a player had to decode by reading the
/// panel. These tests hold the fix in place: every name is the effect in plain words, every keystone
/// gate is named after the keystone it teaches, and the old metaphors are pinned as forbidden so a
/// future content pass cannot drift back to them by habit.
/// </remarks>
public class TraitNamesTests
{
    /// <summary>The whole catalogue, by id. Pinned exactly — saves store ids, so an id can never move.</summary>
    private static readonly string[] PinnedIds =
    {
        "socket_2", "weave_5", "socket_3",
        "vow_study_1", "vow_study_2", "vow_study_3", "vow_binding", "vow_sacrifice",
        "ledger", "filter_common", "filter_uncommon", "forge_insight", "efficient_forge", "auto_merge",
        "recall_1", "recall_2", "recall_3", "recall_4",
        "ks_glass_cannon", "ks_bloodlust", "ks_blood_magic", "ks_reaper", "ks_rend",
        "ks_ironclad", "ks_juggernaut", "ks_undying", "ks_titan", "ks_dynamo",
        "ks_lodestone", "ks_capacitor",
        "ks_greed", "ks_discerning_eye", "ks_fortune", "ks_hoarder",
        "ks_echo", "ks_venomancer", "artifice_vows", "ks_weaver",
        "ruin_edge_1", "ruin_edge_2", "ruin_edge_3",
        "aegis_skin_1", "aegis_skin_2", "aegis_skin_3",
        "avarice_purse_1", "avarice_purse_2", "avarice_purse_3",
        "artifice_hands_1", "artifice_hands_2", "artifice_hands_3",
        "attunement",
    };

    /// <summary>The names the playtest could not decode. None may come back.</summary>
    private static readonly string[] OldMetaphors =
    {
        "SECOND SOCKET", "FIFTH WEAVE", "THIRD SOCKET", "FIRST VOW", "SECOND VOW", "THIRD VOW",
        "BINDING VOWS", "VOWS OF SACRIFICE", "HUNTER'S LEDGER", "SORTER'S EYE", "SORTER'S DISCIPLINE",
        "FORGE INSIGHT", "EFFICIENT FORGE", "TIRELESS FORGE",
        "SHARPENED RECALL I", "SHARPENED RECALL II", "SHARPENED RECALL III", "PERFECT RECALL",
        "THE GLASS ROAD", "THE EDGE", "THE RED ROAD", "THE HARVEST", "THE STORED BLOW",
        "THE IRON ROAD", "THE UNMOVED", "THE LAST BREATH", "THE TITAN ROAD", "THE WOUND SPRING",
        "THE KEPT COIL", "THE DEEP WELL",
        "THE GOLDEN ROAD", "THE NARROW EYE", "THE GILDED ROAD", "THE FULL VAULT",
        "THE TWICE-SPOKEN", "THE SLOW ROAD", "THE BOUND HAND", "THE DOUBLE THREAD",
        "SHARP EDGE", "RAZOR EDGE", "THE RED HARVEST",
        "THICK SKIN", "IRON SKIN", "TITAN'S BULK",
        "A DEEPER PURSE", "A KEENER EYE", "HOARDER'S SHARE",
        "QUICK HANDS", "DEFT HANDS", "WEAVER'S PACE",
        "COMPLETE ATTUNEMENT",
    };

    /// <summary>What the font can draw: ASCII, and exactly these seven-and-a-bit extras.</summary>
    private const string FontExtras = "·×—–→←‹›…";

    [Fact]
    public void test_ids_never_change_because_saves_store_them()
    {
        var ids = MemoryDustTree.Catalog.Select(u => u.Id).OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.Equal(PinnedIds.OrderBy(s => s, StringComparer.Ordinal).ToList(), ids);
    }

    [Fact]
    public void test_every_node_has_a_name_and_none_is_an_old_metaphor()
    {
        foreach (var u in MemoryDustTree.Catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(u.Name), $"{u.Id} has no name");
            Assert.DoesNotContain(u.Name, OldMetaphors);
            // The font gate: ASCII plus the handful of glyphs the atlas carries, nothing else.
            foreach (var ch in u.Name)
                Assert.True(ch < 128 || FontExtras.Contains(ch), $"{u.Id}'s name uses a glyph the font cannot draw: '{ch}'");
        }

        // And no two nodes share a name — a name that says what a node does must say which node.
        Assert.Equal(MemoryDustTree.Catalog.Count, MemoryDustTree.Catalog.Select(u => u.Name).Distinct().Count());
    }

    [Fact]
    public void test_a_keystone_gate_is_named_after_the_keystone_it_teaches()
    {
        // The node's whole effect is "you may now wear that keystone", so its name says so — and uses
        // the keystone's REAL catalogue name, never a road metaphor for it.
        foreach (var gate in MemoryDustTree.Catalog.Where(u => u.GrantsKeystone is not null))
        {
            var keystone = Keystones.ById(gate.GrantsKeystone)!;
            Assert.Equal($"KEYSTONE — {keystone.Name}", gate.Name);
        }
    }

    [Theory]
    [InlineData("ruin_edge_1", "HARDER HITS I")]
    [InlineData("ruin_edge_3", "HARDER HITS III")]
    [InlineData("aegis_skin_1", "MORE HEALTH I")]
    [InlineData("avarice_purse_1", "MORE LOOT I")]
    [InlineData("avarice_purse_2", "RARER FINDS I")]
    [InlineData("artifice_hands_1", "FASTER SKILLS I")]
    [InlineData("filter_common", "AUTO-SELL COMMON DROPS")]
    [InlineData("auto_merge", "AUTO-MERGE SPARE ITEMS")]
    [InlineData("efficient_forge", "SALVAGE PAYS 15% MORE")]
    [InlineData("artifice_vows", "VOWS PAY 25% MORE")]
    [InlineData("weave_5", "FIFTH SKILL SLOT")]
    [InlineData("recall_4", "FASTER REGION MASTERY IV")]
    public void test_a_node_is_named_after_what_it_does(string id, string expected)
        => Assert.Equal(expected, MemoryDustTree.Catalog.First(u => u.Id == id).Name);

    [Fact]
    public void test_an_attribute_node_is_named_after_the_number_it_moves()
    {
        // The name must say the same thing the Mods do: a node that raises Damage is HARDER HITS, one
        // that raises Health is MORE HEALTH, and so on. Checked from the numbers, not the other way
        // round, so retuning a node's field without renaming it fails here.
        foreach (var u in MemoryDustTree.Catalog.Where(u => u.Mods != BuildMods.None))
        {
            var m = u.Mods;
            if (m.Damage > 1f) Assert.Contains("HARDER HITS", u.Name);
            if (m.Health > 1f) Assert.Contains("MORE HEALTH", u.Name);
            if (m.SkillRate > 1f) Assert.Contains("FASTER SKILLS", u.Name);
            if (m.Haul > 1f) Assert.Contains("LOOT", u.Name);
            if (m.Rarity > 1f) Assert.Contains("RARER", u.Name);
        }
    }

    // ── Roads ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_road_has_a_name_and_an_identity_sentence()
    {
        foreach (var road in Enum.GetValues<TraitRoad>())
        {
            var id = TraitRoads.Of(road);
            Assert.False(string.IsNullOrWhiteSpace(id.Name), $"{road} has no name");
            Assert.False(string.IsNullOrWhiteSpace(id.Sentence), $"{road} has no identity sentence");
            Assert.EndsWith(".", id.Sentence);
            Assert.NotEqual(id.Sentence.ToUpperInvariant(), id.Sentence);   // a sentence, not a slogan
            Assert.Equal(id.Name, TraitRoads.Name(road));
            Assert.Equal(id.Sentence, TraitRoads.Sentence(road));
        }
        Assert.Equal(Enum.GetValues<TraitRoad>().Length, TraitRoads.All.Count);
    }

    [Theory]
    [InlineData(TraitRoad.Ruin, "Hit harder. Live closer to death.")]
    [InlineData(TraitRoad.Aegis, "Live longer. Strike less often.")]
    [InlineData(TraitRoad.Artifice, "Skills act differently. Vows pay more.")]
    [InlineData(TraitRoad.Avarice, "More loot and rarer loot. Softer hits.")]
    public void test_the_four_roads_say_what_they_are_for(TraitRoad road, string sentence)
        => Assert.Equal(sentence, TraitRoads.Sentence(road));

    [Fact]
    public void test_a_road_identity_agrees_with_what_its_nodes_do()
    {
        // The sentence is a promise about the catalogue. RUIN's minors raise Damage; AEGIS's raise
        // Health; ARTIFICE's raise SkillRate; AVARICE's raise Haul or Rarity — and no road's minors
        // raise a field that belongs to another road's sentence.
        BuildMods Minors(TraitRoad r) => BuildMods.Sum(MemoryDustTree.Catalog.Where(u => u.Road == r).Select(u => u.Mods));

        var ruin = Minors(TraitRoad.Ruin);
        Assert.True(ruin.Damage > 1f && ruin.Health == 1f && ruin.SkillRate == 1f && ruin.Haul == 1f && ruin.Rarity == 1f);
        var aegis = Minors(TraitRoad.Aegis);
        Assert.True(aegis.Health > 1f && aegis.Damage == 1f && aegis.SkillRate == 1f && aegis.Haul == 1f && aegis.Rarity == 1f);
        var artifice = Minors(TraitRoad.Artifice);
        Assert.True(artifice.SkillRate > 1f && artifice.Damage == 1f && artifice.Health == 1f && artifice.Haul == 1f && artifice.Rarity == 1f);
        var avarice = Minors(TraitRoad.Avarice);
        Assert.True(avarice.Haul > 1f && avarice.Rarity > 1f && avarice.Damage == 1f && avarice.Health == 1f && avarice.SkillRate == 1f);

        // The spine sells capacity and never a number.
        Assert.Equal(BuildMods.None, Minors(TraitRoad.Spine));
    }

    // ── The layout ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_layout_places_every_node_and_no_two_overlap()
    {
        var ids = MemoryDustTree.Catalog.Select(u => u.Id).ToList();
        Assert.True(TraitTreeLayout.Covers(ids), "a catalogue node has no position");
        Assert.Empty(TraitTreeLayout.Unauthored);   // every node has a hand-chosen home

        // The room a node is drawn in fits inside a lane and a rung, so neighbours cannot touch.
        Assert.True(TraitTreeLayout.NodeWidth <= TraitTreeLayout.LaneStep);
        Assert.True(TraitTreeLayout.NodeHeight <= TraitTreeLayout.ChainStep);

        // And every pair keeps clear on at least one axis — which, with every node drawn inside that
        // room, is what "no two nodes overlap" means at the default zoom or any other.
        var overlaps = new List<string>();
        for (var i = 0; i < ids.Count; i++)
            for (var j = i + 1; j < ids.Count; j++)
                if (!TraitTreeLayout.KeepClear(TraitTreeLayout.Positions[ids[i]], TraitTreeLayout.Positions[ids[j]]))
                    overlaps.Add($"{ids[i]} / {ids[j]}");
        Assert.True(overlaps.Count == 0, "nodes drawn on top of each other: " + string.Join(", ", overlaps));
    }

    [Fact]
    public void test_the_layout_is_a_tree_with_roots_below_the_ground_and_crowns_level()
    {
        var p = TraitTreeLayout.Positions;
        // Roots (the spine) hang at or below row 0; boughs climb above it.
        foreach (var u in MemoryDustTree.Catalog)
        {
            if (u.Road == TraitRoad.Spine) Assert.True(p[u.Id].Y >= 0f, $"{u.Id} is a root drawn above the ground");
            else Assert.True(p[u.Id].Y < 0f, $"{u.Id} is on a road but drawn below the ground");
        }
        // The four crowns sit at one altitude, evenly spaced.
        var crowns = new[] { "ks_reaper", "ks_titan", "ks_weaver", "ks_hoarder" }.Select(id => p[id]).OrderBy(v => v.X).ToList();
        Assert.All(crowns, c => Assert.Equal(crowns[0].Y, c.Y));
        var gap = crowns[1].X - crowns[0].X;
        Assert.Equal(gap, crowns[2].X - crowns[1].X, 3);
        Assert.Equal(gap, crowns[3].X - crowns[2].X, 3);
    }

    // ── The wire the rename exposed ───────────────────────────────────────────────────────────

    [Fact]
    public void test_faster_region_mastery_actually_makes_region_mastery_faster()
    {
        // DustEffects.MasteryRate existed and nothing read it: the four recall nodes were a label. A
        // node named FASTER REGION MASTERY has to reach the one place mastery grows.
        var bare = new MemoryDustTree();
        var sharp = new MemoryDustTree();
        sharp.SetEarned(100);
        foreach (var id in new[] { "recall_1", "recall_2", "recall_3", "recall_4" }) Assert.True(sharp.Purchase(id));

        var plain = new Region("verdant_hollow", AutomationTuning.Default);
        var boosted = new Region("verdant_hollow", AutomationTuning.Default);
        for (var i = 0; i < 20; i++)
        {
            plain.RecordActiveKill(DustEffects.MasteryRate(bare));
            boosted.RecordActiveKill(DustEffects.MasteryRate(sharp));
        }

        Assert.Equal(1.2f, DustEffects.MasteryRate(sharp), 3);
        Assert.Equal(plain.RegionMasteryPoints * 1.2f, boosted.RegionMasteryPoints, 2);
        Assert.Equal(20 * AutomationTuning.Default.RmpPerActiveKillBonus, plain.RegionMasteryPoints, 3);   // no trait, no change
    }
}
