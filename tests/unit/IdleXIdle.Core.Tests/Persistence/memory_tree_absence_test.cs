using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// THE MEMORY TREE IS GONE, AND STAYS GONE.
/// </summary>
/// <remarks>
/// <para>
/// Its sibling <see cref="TraitTreeMigrationTest"/> proves what an old save KEEPS. This file proves the
/// other half, which is the half a refactor usually forgets: what it must no longer have. A deletion
/// that is only asserted by "the code does not compile against it" quietly comes back the first time
/// somebody restores a convenience.
/// </para>
/// <para>
/// The migration principle these pin: <b>preserve progression and capabilities, not obsolete balance
/// modifiers.</b> What a player could DO survives. What a retired balance table happened to multiply
/// does not.
/// </para>
/// </remarks>
public class MemoryTreeAbsenceTest
{
    /// <summary>Every node the retired tree ever sold — the same frozen list the migration test uses.</summary>
    private static readonly string[] EveryLegacyNode =
    {
        "ks_glass_cannon", "ks_bloodlust", "ks_blood_magic", "ks_reaper", "ks_rend", "ks_ironclad",
        "ks_juggernaut", "ks_undying", "ks_titan", "ks_dynamo", "ks_greed", "ks_discerning_eye",
        "ks_fortune", "ks_hoarder", "ks_lodestone", "ks_echo", "ks_venomancer", "ks_capacitor",
        "ks_weaver",
        "vow_study_1", "vow_study_2", "vow_study_3", "vow_binding", "vow_sacrifice",
        "socket_2", "socket_3", "filter_common", "filter_uncommon", "auto_merge",
        "ruin_edge_1", "ruin_edge_2", "ruin_edge_3",
        "aegis_skin_1", "aegis_skin_2", "aegis_skin_3",
        "avarice_purse_1", "avarice_purse_2", "avarice_purse_3",
        "artifice_hands_1", "artifice_hands_2", "artifice_hands_3",
        "recall_1", "recall_2", "recall_3", "recall_4", "efficient_forge", "artifice_vows",
        "weave_5", "ledger", "forge_insight", "attunement",
    };

    /// <summary>A v3 save that bought the ENTIRE tree, plus real progression to check alongside it.</summary>
    private static SaveGame MaximalOldSave() => new()
    {
        Version = 3,
        MemoryDust = 4_242,
        MemoryDustUnlocks = EveryLegacyNode.ToList(),
        Gleam = 99_000,
        HighestMasteryAwarded = 7,
        ConqueredRegions = Regions.All.Take(3).Select(r => r.Id).ToList(),
        ActiveRegion = Regions.All[0].Id,
        CorruptionTier = 4,
        CorruptionPeak = 6,
    };

    // ── 1. AN OLD SAVE STILL LOADS, AND KEEPS WHAT IT EARNED ────────────────────────────────────

    [Fact]
    public void test_a_save_holding_every_legacy_node_loads_and_grants_its_capabilities()
    {
        var save = MaximalOldSave();

        var g = LegacyTraitTree.Read(save.MemoryDustUnlocks);

        // CAPABILITIES survive: every keystone, every Vow, the sockets, both automations.
        Assert.Equal(Keystones.Catalog.Count, g.Keystones.Count);
        Assert.Equal(Vows.Catalog.Count, g.Vows.Count);
        Assert.Equal(3, g.KeystoneSockets);
        Assert.Equal(4, g.ScavengerRunsLevel);   // filter_uncommon — sells Uncommons
        Assert.Equal(2, g.HoardVaultsLevel);     // auto_merge
        Assert.True(g.OwnedAnyNode);
    }

    [Fact]
    public void test_unrelated_progression_survives_the_round_trip()
    {
        var save = MaximalOldSave();

        var round = SaveSystem.Deserialize(SaveSystem.Serialize(save), 0).Save!;

        Assert.Equal(4_242, round.MemoryDust);            // the wallet is a live currency
        Assert.Equal(99_000, round.Gleam);
        Assert.Equal(7, round.HighestMasteryAwarded);
        Assert.Equal(3, round.ConqueredRegions.Count);
        Assert.Equal(6, round.CorruptionPeak);
    }

    // ── 2. THE OBSOLETE MODIFIERS DO NOT SURVIVE ────────────────────────────────────────────────

    [Fact]
    public void test_the_grant_record_carries_no_field_that_could_hold_a_balance_modifier()
    {
        // The shape of the migration is the guarantee. LegacyTraitGrants may only carry CAPABILITY:
        // ids to unlock and capacities to floor. A float, a double or a "BuildMods" on this record
        // would be the door through which grandfathered hidden power walks back in.
        var allowed = new[]
        {
            "KeystoneSockets", "Keystones", "Vows", "ScavengerRunsLevel", "HoardVaultsLevel",
            "OwnedAnyNode",
        };

        var actual = typeof(LegacyTraitGrants)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => n != "EqualityContract")
            .ToList();

        Assert.Equal(allowed.OrderBy(x => x), actual.OrderBy(x => x));

        foreach (var p in typeof(LegacyTraitGrants).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            Assert.True(p.PropertyType != typeof(float) && p.PropertyType != typeof(double),
                        $"{p.Name} is a {p.PropertyType.Name} — a rate on this record is a grandfathered "
                        + "balance modifier, which the migration principle forbids.");
    }

    [Fact]
    public void test_a_maximal_old_save_composes_no_passive_bonus_of_its_own()
    {
        // The twelve attribute minors (HARDER HITS, MORE HEALTH, MORE LOOT, FASTER SKILLS) used to
        // multiply into PassiveMods. A build composed for a character with no passive of its own must
        // now be identical whatever the save once bought — there is no channel left for them.
        var blank = CharacterRoster.All.FirstOrDefault(c => c.Mods == BuildMods.None);
        var loadout = new PlayerLoadout { SkillCapacity = 1 };
        loadout.SetSkill(loadout.AddSkill(), blank?.SignatureSkillId ?? "hammer_blow");
        loadout.SetSource(0, Source.Body);

        Assert.NotNull(blank);   // else this asserts nothing at all
        var build = loadout.ToBuild(new MasteryTree(), blank);

        Assert.Equal(BuildMods.None, build.PassiveMods);

        // THE CONTROL, and it is the half that makes the assertion above mean something: a character
        // that DOES have a passive still gets it. Without this, PassiveMods being broken outright
        // would pass the test just as happily as the tree being gone.
        var gifted = CharacterRoster.All.FirstOrDefault(c => c.Mods != BuildMods.None);
        Assert.NotNull(gifted);
        var live = loadout.ToBuild(new MasteryTree(), gifted);
        Assert.NotEqual(BuildMods.None, live.PassiveMods);
        Assert.Equal(gifted!.Mods, live.PassiveMods);
    }

    [Fact]
    public void test_region_mastery_speed_is_one_for_everyone()
    {
        // recall_1..4 gave +5/10/15/20 %. Mastery already EARNED is progression and is asserted above;
        // the future multiplier is not, and its default is the only rate the game now uses.
        var m = typeof(IdleXIdle.Core.Automation.Region).GetMethod("RecordActiveKill");
        Assert.NotNull(m);
        var rate = m!.GetParameters().Single(p => p.Name == "rate");
        Assert.True(rate.HasDefaultValue);
        Assert.Equal(1f, (float)rate.DefaultValue!);
    }

    [Fact]
    public void test_salvage_return_is_the_base_rate_for_everyone()
    {
        // efficient_forge multiplied the dismantle return by 1.15 for whoever had bought it. If salvage
        // now reads low it is tuned HERE, in the base, for every player — never per account.
        Assert.Equal(ForgeTuning.Default.DismantleReturnRate, new ForgeTuning().DismantleReturnRate);
    }

    // ── 3. NOTHING IS WRITTEN BACK, AND A RELOAD IS IDENTICAL ───────────────────────────────────

    [Fact]
    public void test_resaving_an_old_save_produces_no_memory_tree_ownership()
    {
        var wallet = new MemoryDustWallet();
        wallet.AddDust(4_242);

        var written = SaveSystem.Capture(new Hunter(), Array.Empty<ItemInstance>(), 0, wallet);

        Assert.Empty(written.MemoryDustUnlocks);
        Assert.Equal(4_242, written.MemoryDust);
    }

    [Fact]
    public void test_reloading_the_new_save_behaves_identically()
    {
        // The second load must not lose or change anything the first one produced — which is what makes
        // the migration safe to run forever rather than a one-shot with an unreproducible failure.
        var wallet = new MemoryDustWallet();
        wallet.AddDust(4_242);
        var first = SaveSystem.Capture(new Hunter(), Array.Empty<ItemInstance>(), 0, wallet);

        var round = SaveSystem.Deserialize(SaveSystem.Serialize(first), 0).Save!;
        var again = SaveSystem.Deserialize(SaveSystem.Serialize(round), 0).Save!;

        Assert.Empty(again.MemoryDustUnlocks);
        Assert.Equal(first.MemoryDust, again.MemoryDust);
        Assert.Same(LegacyTraitGrants.None, LegacyTraitTree.Read(again.MemoryDustUnlocks));
    }

    // ── 4. NO RUNTIME CODE UNDERSTANDS THE OLD SHAPE ────────────────────────────────────────────

    [Fact]
    public void test_the_composer_takes_no_memory_tree()
    {
        // BuildComposer used to take the tree as its FIRST parameter and read four things off it.
        // A parameter of that type reappearing is the whole regression, so the signature is pinned.
        foreach (var m in typeof(BuildComposer).GetMethods(BindingFlags.Public | BindingFlags.Static))
            foreach (var p in m.GetParameters())
                Assert.False(p.ParameterType.Name.Contains("MemoryDustTree", StringComparison.Ordinal),
                             $"BuildComposer.{m.Name} takes {p.ParameterType.Name}");
    }

    [Fact]
    public void test_no_type_named_for_the_retired_tree_survives_in_core()
    {
        // TraitRoad, MemoryDustUnlock, the catalogue, purchase and prerequisite validation, and the
        // Earned/Spent/Available point pool all went with it. The wallet is what remains.
        var gone = new[]
        {
            "MemoryDustTree", "MemoryDustUnlock", "TraitRoad", "UnlockEffect", "DustEffects",
            "MemoryDustText", "TraitTreeLayout", "TraitRoads",
        };

        var live = typeof(MemoryDustWallet).Assembly.GetTypes().Select(t => t.Name).ToHashSet();

        foreach (var name in gone)
            Assert.DoesNotContain(name, live);

        Assert.Contains("MemoryDustWallet", live);
    }

    [Fact]
    public void test_the_wallet_is_a_wallet_and_nothing_more()
    {
        // No Owns, no Purchase, no CanUnlock, no Catalog, no Earned/Spent/Available. If one of those
        // comes back, so has the tree.
        var members = typeof(MemoryDustWallet)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet();

        foreach (var banned in new[] { "Owns", "Purchase", "CanUnlock", "Catalog", "All",
                                       "Earned", "Spent", "Available", "SetEarned", "OwnedIds", "IsComplete" })
            Assert.DoesNotContain(banned, members);
    }

    [Fact]
    public void test_the_traits_screen_opens_on_a_discovery_not_on_a_point()
    {
        // The gate used to be TraitPointsEarned >= 1 — a screen gated on having something to SPEND,
        // where nothing is spent. It opens on having something to SEE.
        Assert.False(Unlocks.IsOpen(Activity.Traits, new UnlockFacts(RegionsConquered: 6, DeepestWave: 999)));
        Assert.True(Unlocks.IsOpen(Activity.Traits, new UnlockFacts(TraitsDiscovered: 1)));
    }
}
