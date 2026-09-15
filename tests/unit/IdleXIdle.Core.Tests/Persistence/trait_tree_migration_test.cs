using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Warrens;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// A save that already bought these must not lose them — the whole of the trait-tree migration.
/// </summary>
/// <remarks>
/// <para>
/// Nineteen keystones, thirteen Vows, two keystone sockets, two loot filters and an auto-merge lived on
/// the old trait tree, and every one of them moves to a new owner here. The rule the migration runs on
/// is that everything it grants is a FLOOR or a UNION, never an assignment — which is what makes it
/// safe to run on every load forever, and what these tests actually pin.
/// </para>
/// <para>
/// The one entitlement deliberately removed anywhere in this table is the fifth skill slot, and it is
/// removed loudly: the fifth woven row is unwoven, its levels are kept, and the player is told.
/// </para>
/// </remarks>
public class TraitTreeMigrationTest
{
    /// <summary>
    /// EVERY NODE THE RETIRED MEMORY TREE EVER SOLD — all 51 ids, frozen here on the day the tree was
    /// deleted.
    /// </summary>
    /// <remarks>
    /// This list used to be <c>MemoryDustTree.Catalog.Select(u =&gt; u.Id)</c>. The tree is gone, and
    /// reading the catalogue was the last thing keeping it alive, so the ids live here — in the test
    /// that exists to prove an old save still loads — rather than in production code that would have to
    /// understand them. Nothing in the running game knows these strings; only
    /// <see cref="LegacyTraitTree"/> and this file do.
    /// </remarks>
    private static readonly string[] EveryLegacyNode =
    {
        // The 19 keystone nodes — PRESERVED as keystone discoveries.
        "ks_glass_cannon", "ks_bloodlust", "ks_blood_magic", "ks_reaper", "ks_rend", "ks_ironclad",
        "ks_juggernaut", "ks_undying", "ks_titan", "ks_dynamo", "ks_greed", "ks_discerning_eye",
        "ks_fortune", "ks_hoarder", "ks_lodestone", "ks_echo", "ks_venomancer", "ks_capacitor",
        "ks_weaver",
        // The 5 study nodes — PRESERVED as Vow discoveries.
        "vow_study_1", "vow_study_2", "vow_study_3", "vow_binding", "vow_sacrifice",
        // Structural capacity — PRESERVED.
        "socket_2", "socket_3",
        // The 2 loot filters and auto-merge — PRESERVED as Warren facility levels.
        "filter_common", "filter_uncommon", "auto_merge",
        // The 12 attribute minors — DISCARDED. HARDER HITS I-III, MORE HEALTH I-III, MORE LOOT /
        // RARER FINDS / MORE AND RARER LOOT, FASTER SKILLS I-III.
        "ruin_edge_1", "ruin_edge_2", "ruin_edge_3",
        "aegis_skin_1", "aegis_skin_2", "aegis_skin_3",
        "avarice_purse_1", "avarice_purse_2", "avarice_purse_3",
        "artifice_hands_1", "artifice_hands_2", "artifice_hands_3",
        // The rate nodes — DISCARDED.
        "recall_1", "recall_2", "recall_3", "recall_4", "efficient_forge", "artifice_vows",
        // The fifth skill slot — DISCARDED, loudly (see the test for it below).
        "weave_5",
        // Pure gates that never did anything, and the completion mark.
        "ledger", "forge_insight", "attunement",
    };

    private static SaveGame V3(params string[] nodes) => new()
    {
        Version = 3,
        MemoryDust = 4_242,
        MemoryDustUnlocks = nodes.ToList(),
    };

    [Fact]
    public void test_a_fresh_save_grants_nothing()
    {
        var g = LegacyTraitTree.Read(V3().MemoryDustUnlocks);

        Assert.False(g.OwnedAnyNode);
        Assert.Equal(0, g.KeystoneSockets);
        Assert.Empty(g.Keystones);
        Assert.Empty(g.Vows);
        Assert.Equal(0, g.ScavengerRunsLevel);
        Assert.Equal(0, g.HoardVaultsLevel);
    }

    [Fact]
    public void test_an_unknown_node_id_is_ignored_and_does_not_throw()
    {
        // Forward compatibility, and the never-throw guarantee the load path is written under: a node
        // id from a build that does not exist yet must be skipped, not crash the boot.
        var g = LegacyTraitTree.Read(new[] { "ks_glass_cannon", "a_node_from_the_future", "" });

        Assert.Equal(new[] { "glass_cannon" }, g.Keystones);
        Assert.True(g.OwnedAnyNode);
    }

    [Fact]
    public void test_every_keystone_node_maps_to_a_keystone_that_exists()
    {
        // The nineteen ks_* ids were copied out of the tree on the day it stopped being the producer.
        // If one of them names a keystone the catalogue no longer has, a returning player silently
        // loses it — which is the one thing the migration is not allowed to do.
        var everything = EveryLegacyNode.ToList();
        var g = LegacyTraitTree.Read(everything);

        Assert.Equal(19, g.Keystones.Count);
        foreach (var id in g.Keystones)
            Assert.True(Keystones.ById(id) is not null, $"the frozen table grants {id}, which is not a keystone");
        Assert.Equal(Keystones.Catalog.Count, g.Keystones.Count);
    }

    [Fact]
    public void test_every_vow_node_maps_to_a_vow_that_exists_and_the_five_teach_all_thirteen()
    {
        var g = LegacyTraitTree.Read(EveryLegacyNode);

        Assert.Equal(Vows.Catalog.Count, g.Vows.Count);
        foreach (var id in g.Vows)
            Assert.True(Vows.ById(id) is not null, $"the frozen table grants {id}, which is not a Vow");
    }

    [Fact]
    public void test_already_unlocked_vows_stay_unlocked()
    {
        var g = LegacyTraitTree.Read(V3("vow_study_1", "vow_binding").MemoryDustUnlocks);

        Assert.Contains("vow_complete", g.Vows);
        Assert.Contains("vow_deliberate", g.Vows);
        Assert.Contains("vow_barefoot", g.Vows);
        Assert.DoesNotContain("vow_unbound", g.Vows);   // vow_sacrifice was not bought
    }

    [Fact]
    public void test_already_earned_socket_capacity_is_preserved_even_with_one_conquest()
    {
        // THE CASE PURE DERIVATION WOULD BREAK. Two trait points was reachable on a single conquest, so
        // a save can hold the second socket while the new rule (three conquests) would say one. The
        // stored latch is what stops a refactor taking a capacity away.
        var g = LegacyTraitTree.Read(V3("socket_2", "weave_5", "socket_3").MemoryDustUnlocks);
        Assert.Equal(3, g.KeystoneSockets);

        var derived = Unlocks.KeystoneSockets(new UnlockFacts(RegionsConquered: 1));
        Assert.Equal(1, derived);

        var earned = Math.Max(Math.Max(0, g.KeystoneSockets), derived);
        Assert.Equal(3, earned);
    }

    [Fact]
    public void test_a_worn_keystone_is_never_truncated_away_on_load()
    {
        // The load runs before conquest is restored, so every unlock fact is zero at that moment. The
        // floor is the save's OWN worn count — without it, PlayerLoadout.Restore truncates a returning
        // player's keystones to nothing and the save written back agrees.
        var save = V3() with { SocketedKeystoneIds = new List<string> { "echo", "greed", "titan" } };
        var g = LegacyTraitTree.Read(save.MemoryDustUnlocks);

        var capacity = Math.Max(Math.Max(save.KeystoneSocketsEarned, g.KeystoneSockets),
                                save.SocketedKeystoneIds.Count);
        Assert.Equal(3, capacity);

        var loadout = new PlayerLoadout { KeystoneCapacity = capacity };
        loadout.Restore(Array.Empty<(string?, string?, string?, string?, bool?)>(), save.SocketedKeystoneIds);
        Assert.Equal(3, loadout.KeystoneIds.Count);
    }

    [Fact]
    public void test_a_v3_save_with_filter_uncommon_still_sells_uncommon()
    {
        var warren = new Warren();
        SaveSystem.RestoreWarren(V3("ledger", "filter_common", "filter_uncommon"), warren,
                                 LegacyTraitTree.Read(V3("ledger", "filter_common", "filter_uncommon").MemoryDustUnlocks));

        Assert.Equal(Rarity.Uncommon, WarrenAutomation.AutoSellAtOrBelow(warren));
    }

    [Fact]
    public void test_a_v3_save_with_filter_common_sells_common_and_no_more()
    {
        var save = V3("ledger", "filter_common");
        var warren = new Warren();
        SaveSystem.RestoreWarren(save, warren, LegacyTraitTree.Read(save.MemoryDustUnlocks));

        Assert.Equal(Rarity.Common, WarrenAutomation.AutoSellAtOrBelow(warren));
    }

    [Fact]
    public void test_a_v3_save_with_auto_merge_still_merges()
    {
        var save = V3("ledger", "forge_insight", "auto_merge");
        var warren = new Warren();
        SaveSystem.RestoreWarren(save, warren, LegacyTraitTree.Read(save.MemoryDustUnlocks));

        Assert.True(WarrenAutomation.AutoMergeOnChestOpen(warren));
    }

    [Fact]
    public void test_the_facility_floor_never_lowers_a_level_the_warren_already_had()
    {
        // A player deep in the Warren must not be dragged back to the migration's floor.
        var save = V3("ledger", "filter_common") with
        {
            WarrenFacilities = new Dictionary<string, int> { ["ScavengerRuns"] = 9 },
        };
        var warren = new Warren();
        SaveSystem.RestoreWarren(save, warren, LegacyTraitTree.Read(save.MemoryDustUnlocks));

        Assert.Equal(9, warren.Facility(FacilityKind.ScavengerRuns).Level);
    }

    [Fact]
    public void test_a_migrated_facility_is_open_even_on_one_conquest()
    {
        // SCAVENGER RUNS opens on the ramp at two conquests, and the migrated entitlement must not wait
        // for that. Warren.IsUnlocked grandfathers any facility past level 1 open under any ramp.
        var save = V3("ledger", "filter_common");
        var warren = new Warren { ConqueredRegions = 1 };
        SaveSystem.RestoreWarren(save, warren, LegacyTraitTree.Read(save.MemoryDustUnlocks));

        Assert.True(warren.IsUnlocked(FacilityKind.ScavengerRuns));
    }

    [Fact]
    public void test_the_migration_is_idempotent()
    {
        // THE LOAD-BEARING ONE. Everything the migration grants is a floor or a union, so running it a
        // second time can grant nothing new — which is what stops it being a one-shot with a failure
        // mode nobody can reproduce twice.
        var save = V3("socket_2", "socket_3", "ks_echo", "vow_study_1", "filter_uncommon", "auto_merge");
        var first = LegacyTraitTree.Read(save.MemoryDustUnlocks);
        var again = LegacyTraitTree.Read(save.MemoryDustUnlocks);

        Assert.Equal(first.KeystoneSockets, again.KeystoneSockets);
        Assert.Equal(first.Keystones, again.Keystones);
        Assert.Equal(first.Vows, again.Vows);

        var warren = new Warren();
        SaveSystem.RestoreWarren(save, warren, first);
        var afterFirst = warren.FacilityLevels.ToDictionary(kv => kv.Key, kv => kv.Value);
        SaveSystem.RestoreWarren(save, warren, again);
        Assert.Equal(afterFirst, warren.FacilityLevels);

        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in first.Keystones) known.Add(id);
        var count = known.Count;
        foreach (var id in again.Keystones) known.Add(id);
        Assert.Equal(count, known.Count);
    }

    [Fact]
    public void test_a_save_written_after_the_migration_grants_nothing_a_second_time()
    {
        // Once the tree is gone the field serialises empty, and every step becomes a max against zero.
        var g = LegacyTraitTree.Read(new List<string>());
        Assert.Same(LegacyTraitGrants.None, g);
    }

    [Fact]
    public void test_the_dust_wallet_survives_the_migration_untouched()
    {
        // MemoryDustTree WAS two systems in one class: the trait tree AND the Memory Dust wallet, a
        // live currency with five faucets and two sinks. The tree is deleted and the wallet survives as
        // MemoryDustWallet — dropping SaveGame.MemoryDust with it would have zeroed every player's
        // Warren currency and expedition checkpoints ten seconds after launch.
        var save = V3("socket_2");
        Assert.Equal(4_242, save.MemoryDust);

        var round = SaveSystem.Deserialize(SaveSystem.Serialize(save), 0).Save!;
        Assert.Equal(4_242, round.MemoryDust);
    }

    [Fact]
    public void test_a_five_slot_save_loads_four_and_the_fifth_skill_keeps_its_levels()
    {
        // THE ONE ENTITLEMENT REMOVED ANYWHERE IN THE MIGRATION. The row is unwoven; SkillProgress is
        // keyed by skill id and not by slot, so every level, variation and reinforcement that skill
        // earned is still there and it can be woven again in place of another at any time.
        var rows = new[] { "hammer_blow", "volley_spray", "sign_call", "snare_repay", "field_pulse" };
        var loadout = new PlayerLoadout { SkillCapacity = Math.Max(1, rows.Length) };

        Assert.Equal(PlayerLoadout.MaxSkills, loadout.SkillCapacity);

        loadout.Restore(rows.Select(id => ((string?)id, (string?)"Body", (string?)null, (string?)null, (bool?)null)),
                        Array.Empty<string>());
        Assert.Equal(4, loadout.Skills.Count);
        Assert.DoesNotContain("field_pulse", loadout.Skills.Select(s => s.SkillId));

        // Enough waves to be past the SECOND rung of the curve, read from the curve rather than typed
        // (SkillProgress.WavesForLevel moved on 2026-09-09 and a hard 40 quietly stopped meaning
        // "well levelled").
        var progress = new SkillProgress();
        for (var i = 0; i < SkillProgress.UsesForLevel(2); i++) progress.RecordWave("field_pulse");
        Assert.True(progress.LevelOf("field_pulse") > 1, "the dropped skill must keep what it earned");
    }

    [Fact]
    public void test_the_version_bump_is_what_takes_the_pre_change_snapshot()
    {
        // SaveStore.SnapshotBeforeUpgrade's guard is `if (fileVersion >= CurrentVersion) return null;`.
        // Without the bump the ten-second autosave overwrites the only copy of the pre-migration file
        // and there is no undo at all — which is why the bump is a data-safety requirement here rather
        // than bookkeeping.
        // RE-PINNED 2026-09-11 (was six): the opening's cursor gained a stage mid-sequence
        // (OpeningScript.FirstVersionWithSignatureWatch).
        // RE-PINNED 2026-09-15 (was seven): the DISPATCHES inbox, whose seed reads the file's version
        // to tell a file that was never told anything from one whose inbox is honestly empty
        // (Dispatches.FirstVersionWithInbox).
        Assert.Equal(8, SaveGame.CurrentVersion);
        Assert.True(5 < SaveGame.CurrentVersion);
    }
}
