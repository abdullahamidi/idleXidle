using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Core.Tests.Persistence;

public class SaveSystemTests
{
    private const long Now = 1_700_000_000_000L;

    private static (Hunter, Region, List<ItemInstance>) BuildGame()
    {
        var hunter = new Hunter();
        hunter.AddGleam(5_000);
        for (var i = 0; i < 12; i++) hunter.Train(HunterStat.AttackPower);
        for (var i = 0; i < 5; i++) hunter.Train(HunterStat.Defense);

        var region = new Region("verdant_hollow", 15);
        for (var i = 0; i < 30; i++) region.RecordActiveKill();
        region.RecordDepth(9);

        var inventory = new List<ItemInstance>
        {
            new() { InstanceId = "i1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 34 },
            new() { InstanceId = "i2", BaseType = ItemBaseType.Charm, Rarity = Rarity.Epic, SellValue = 82 },
        };

        return (hunter, region, inventory);
    }

    /// <summary>A full round trip must lose nothing. Everything the player earned comes back.</summary>
    [Fact]
    public void test_a_save_round_trip_preserves_everything_the_player_earned()
    {
        var (hunter, region, inventory) = BuildGame();

        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, inventory, Now));

        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);

        var save = loaded.Save!;

        Assert.Equal(hunter.Gleam, save.Gleam);
        Assert.Equal(12, save.TrainingRanks[nameof(HunterStat.AttackPower)]);
        Assert.Equal(5, save.TrainingRanks[nameof(HunterStat.Defense)]);
        Assert.Equal(region.RegionMasteryPoints, save.RegionMasteryPoints, precision: 1);
        Assert.Equal(2, save.Inventory.Count);
    }

    /// <summary>
    /// A save written by a build that still had the creature subsystem must load clean.
    /// </summary>
    /// <remarks>
    /// The creature/evolution/automation subsystem was retired 2026-08-24. Every save written before
    /// that carries UnhatchedCores, a Roster of SavedCreatures (with full evolution state),
    /// AssignedCreatureIds, AutomationStage, ChestsCredited, and per-farm Stage/AssignedIds — all
    /// gone from the format. System.Text.Json's default is to SKIP unknown members
    /// (JsonUnmappedMemberHandling.Skip; nothing in SaveSystem.Options overrides it), so the load
    /// must succeed and every SURVIVING field must round-trip untouched. This test writes the
    /// legacy JSON by hand so no current code has to be able to produce it.
    /// </remarks>
    [Fact]
    public void test_a_save_carrying_retired_creature_fields_still_loads_and_keeps_what_survives()
    {
        // Arrange — a hand-written pre-retirement save, retired members included, version current.
        var legacyJson = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "Gleam": 777,
          "Materials": 55,
          "TrainingRanks": { "AttackPower": 3 },
          "RegionMasteryPoints": 1500,
          "AutomationStage": 2,
          "UnhatchedCores": 9,
          "Roster": [
            {
              "Id": "start_atk",
              "Source": "Nature",
              "Role": "Attacker",
              "PowerTier": 3,
              "IsHealthy": true,
              "VowId": "vow_bloodied",
              "EvolutionNodeId": "nature_whelp",
              "EvolutionMaterials": 25,
              "EvolutionWorkTicks": { "Attacker": 12 },
              "EvolutionCombatTally": { "boss_felled": 3 },
              "EvolutionEquippedTrait": "Warding"
            }
          ],
          "AssignedCreatureIds": [ "start_atk" ],
          "ChestsOpened": 412,
          "ChestsCredited": 400,
          "WornWeaponId": "i1",
          "ConqueredRegions": [ "verdant_hollow" ],
          "ActiveRegion": "cinderworks",
          "RegionFarms": [
            {
              "Id": "verdant_hollow",
              "MasteryPoints": 650,
              "BestDepth": 14,
              "Stage": 3,
              "AssignedIds": [ "start_atk" ]
            }
          ],
          "WarrenLevel": 7,
          "WarrenXp": 120,
          "Inventory": [
            {
              "InstanceId": "i1",
              "BaseType": "Weapon",
              "Rarity": 3,
              "SellValue": 34,
              "ItemLevel": 5,
              "Element": "Shadow",
              "EquippedToCreatureId": "start_atk"
            }
          ]
        }
        """;

        // Act
        var loaded = SaveSystem.Deserialize(legacyJson, Now);

        // Assert — the load succeeds, and every surviving field is intact.
        Assert.True(loaded.Ok, "a pre-retirement save must load, not fail");
        var save = loaded.Save!;
        Assert.Equal(777, save.Gleam);
        Assert.Equal(55, save.Materials);
        Assert.Equal(3, save.TrainingRanks["AttackPower"]);
        Assert.Equal(1500f, save.RegionMasteryPoints);
        Assert.Equal(412, save.ChestsOpened);
        Assert.Equal("i1", save.WornWeaponId);
        Assert.Equal("cinderworks", save.ActiveRegion);
        Assert.Contains("verdant_hollow", save.ConqueredRegions);
        Assert.Equal(7, save.WarrenLevel);
        Assert.Equal(120, save.WarrenXp);

        var farm = save.RegionFarms.Single();
        Assert.Equal("verdant_hollow", farm.Id);
        Assert.Equal(650f, farm.MasteryPoints);
        Assert.Equal(14, farm.BestDepth);
        Assert.Equal(0, farm.StartWave);   // a save from before checkpoints starts from the top

        var item = SaveSystem.RestoreInventory(save).Single();
        Assert.Equal("i1", item.InstanceId);
        Assert.Equal(Source.Shadow, item.Element);

        // ...and re-saving it writes the retired members out of existence, losing nothing that survives.
        var rewritten = SaveSystem.Serialize(save);
        Assert.DoesNotContain("\"Roster\"", rewritten);
        Assert.DoesNotContain("UnhatchedCores", rewritten);
        Assert.DoesNotContain("AssignedCreatureIds", rewritten);
        Assert.DoesNotContain("AutomationStage", rewritten);

        var again = SaveSystem.Deserialize(rewritten, Now).Save!;
        Assert.Equal(777, again.Gleam);
        Assert.Equal(1500f, again.RegionMasteryPoints);
        Assert.Equal(14, again.RegionFarms.Single().BestDepth);
    }

    [Fact]
    public void test_a_chosen_checkpoint_survives_the_save()
    {
        var save = new SaveGame
        {
            RegionFarms = new List<RegionFarmSave> { new() { Id = "verdant_hollow", MasteryPoints = 1f, BestDepth = 44, StartWave = 30 } },
        };
        var again = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now).Save!;
        Assert.Equal(30, again.RegionFarms.Single().StartWave);
    }

    /// <summary>
    /// A save carrying DUPLICATE InstanceIds must restore to one item per id.
    /// </summary>
    /// <remarks>
    /// REGRESSION: screenshot runs seeded fixed-id items ("cd0", "up_wpn", ...) and the game's autosave
    /// wrote them into the real save, again and again, so an inventory grew dozens of copies sharing one id.
    /// Two things then broke, both traced to that one id collision: every copy matched the worn item's id, so
    /// a single equipped weapon read as five WORN; and an auto-merge trio of copies was rejected as "an item
    /// with itself", stalling the whole merge. Deduping on restore heals a polluted save the moment it loads.
    /// </remarks>
    [Fact]
    public void test_duplicate_item_ids_are_deduped_on_restore()
    {
        var (hunter, region, _) = BuildGame();
        var dupe = new ItemInstance { InstanceId = "dupe", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 10 };
        var inventory = new List<ItemInstance>
        {
            dupe, dupe, dupe,
            new() { InstanceId = "unique", BaseType = ItemBaseType.Charm, Rarity = Rarity.Common, SellValue = 5 },
        };

        var restored = SaveSystem.RestoreInventory(SaveSystem.Capture(hunter, region, inventory, Now));

        Assert.Equal(2, restored.Count);
        Assert.Single(restored.Where(i => i.InstanceId == "dupe"));
        Assert.Single(restored.Where(i => i.InstanceId == "unique"));
    }

    /// <summary>
    /// An item's region ELEMENT must survive a reload — it is live state the merge rule reads.
    /// </summary>
    /// <remarks>
    /// REGRESSION: Element was set at mint and carried through merges, but SavedItem never captured it, so
    /// every item's element reset to null on load and a saved matched-element trio became unmergeable. The
    /// chest's element already round-tripped; the item's was simply forgotten.
    /// </remarks>
    [Fact]
    public void test_an_items_element_survives_a_reload()
    {
        var (hunter, region, _) = BuildGame();
        var inv = new List<ItemInstance>
        {
            new() { InstanceId = "e1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 10, Element = Source.Shadow },
            new() { InstanceId = "e2", BaseType = ItemBaseType.Charm, Rarity = Rarity.Common, SellValue = 5 },
        };

        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, inv, Now));
        var back = SaveSystem.RestoreInventory(SaveSystem.Deserialize(json, Now).Save!);

        Assert.Equal(Source.Shadow, back.Single(i => i.InstanceId == "e1").Element);
        Assert.Null(back.Single(i => i.InstanceId == "e2").Element);
    }

    /// <summary>
    /// A REFORGED item must come back reforged.
    /// </summary>
    /// <remarks>
    /// The trait/enchant overrides ride the save as new <see cref="SavedItem"/> fields — exactly the kind
    /// of new field a serializer quietly drops — so this round-trips one through the real
    /// Serialize/Deserialize/RestoreInventory the game uses, and checks the lookups read the RESTORED
    /// override rather than falling back to the id-derived roll.
    /// </remarks>
    [Fact]
    public void test_a_reforged_items_passives_survive_a_reload()
    {
        var (hunter, region, _) = BuildGame();

        var baseItem = new ItemInstance
        {
            InstanceId = "reforged_1", BaseType = ItemBaseType.AbilityFocus,
            Rarity = Rarity.Epic, SellValue = 82, Element = Source.Shadow,
        };
        var reforged = baseItem with { TraitOverride = GearTrait.Focused, EnchantOverride = EnchantKind.Radiance };
        var inventory = new List<ItemInstance> { reforged };

        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, inventory, Now));
        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);

        var back = SaveSystem.RestoreInventory(loaded.Save!).Single();
        Assert.Equal(GearTrait.Focused, back.TraitOverride);
        Assert.Equal(EnchantKind.Radiance, back.EnchantOverride);
        Assert.Equal(GearTrait.Focused, GearTraits.TraitOf(back));       // the lookup reads the restored override
        Assert.Equal(EnchantKind.Radiance, Enchantments.Of(back)!.Kind);
    }

    /// <summary>Unopened chests survive a reload — a boss's drop is loot, and losing it would lose the drop.</summary>
    [Fact]
    public void test_unopened_chests_survive_a_reload()
    {
        var (hunter, region, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, inventory, Now) with
        {
            UnopenedChests = new List<SavedChest>
            {
                new() { Rarity = (int)Rarity.Legendary, Tier = 12, Element = "Shadow" },
                new() { Rarity = (int)Rarity.Rare, Tier = 3, Element = null },
            },
        };

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);
        Assert.True(loaded.Ok);

        var back = loaded.Save!.UnopenedChests;
        Assert.Equal(2, back.Count);
        Assert.Equal((int)Rarity.Legendary, back[0].Rarity);
        Assert.Equal(12, back[0].Tier);
        Assert.Equal("Shadow", back[0].Element);
        Assert.Null(back[1].Element);
    }

    /// <summary>An un-reforged item restores with NO override — null, not some enum's first member.</summary>
    [Fact]
    public void test_an_un_reforged_item_restores_with_no_override()
    {
        var (hunter, region, _) = BuildGame();
        var plain = new ItemInstance
        {
            InstanceId = "plain_1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 34,
        };

        var json = SaveSystem.Serialize(
            SaveSystem.Capture(hunter, region, new List<ItemInstance> { plain }, Now));
        var back = SaveSystem.RestoreInventory(SaveSystem.Deserialize(json, Now).Save!).Single();

        Assert.Null(back.TraitOverride);
        Assert.Null(back.EnchantOverride);
    }

    /// <summary>
    /// The woven build survives a reload — the four skills and the sockets, by name/id.
    /// </summary>
    /// <remarks>
    /// The build rides on the save as new records (<see cref="SavedSkill"/>), added by the Game layer via
    /// a <c>with</c> expression. New record shapes are exactly where a serializer quietly drops data, so
    /// this round-trips one through the ACTUAL Serialize/Deserialize the game uses.
    /// </remarks>
    [Fact]
    public void test_a_saved_build_survives_a_reload()
    {
        var (hunter, region, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, inventory, Now) with
        {
            WovenSkills = new List<SavedSkill>
            {
                new() { Source = "Body", Form = "Strike", VowId = null },
                new() { Source = "Shadow", Form = "Trap", VowId = "vow_bloodied" },
            },
            SocketedKeystoneIds = new List<string> { "glass_cannon", "reaper" },
            MasteryTaken = new List<string> { "strike_1", "strike_2" },
            MasteryEarned = 42,
            MasteryZoom = 0.57f,
            MasteryPanX = -120.5f,
            MasteryPanY = 88f,
            ChampionGleamRate = 3.5f,
        };

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);

        Assert.True(loaded.Ok);
        var back = loaded.Save!;
        Assert.Equal(2, back.WovenSkills.Count);
        Assert.Equal("Shadow", back.WovenSkills[1].Source);
        Assert.Equal("Trap", back.WovenSkills[1].Form);
        Assert.Equal("vow_bloodied", back.WovenSkills[1].VowId);
        Assert.Equal(new[] { "glass_cannon", "reaper" }, back.SocketedKeystoneIds);
        Assert.Equal(new[] { "strike_1", "strike_2" }, back.MasteryTaken);
        Assert.Equal(42, back.MasteryEarned);
        Assert.Equal(0.57f, back.MasteryZoom);
        Assert.Equal(-120.5f, back.MasteryPanX);
        Assert.Equal(88f, back.MasteryPanY);
        Assert.Equal(3.5f, back.ChampionGleamRate);
    }

    /// <summary>
    /// A save written before the tree camera existed loads with a zoom of 0 — which the screen reads as
    /// "never opened" and answers with the first-open framing. No version bump, no migration.
    /// </summary>
    [Fact]
    public void test_a_save_without_a_tree_camera_asks_for_the_first_open_framing()
    {
        var (hunter, region, inventory) = BuildGame();
        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, inventory, Now));
        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);
        Assert.Equal(0f, loaded.Save!.MasteryZoom);
        Assert.Equal(0f, loaded.Save!.MasteryPanX);
        Assert.Equal(0f, loaded.Save!.MasteryPanY);
    }

    /// <summary>Restoring a Hunter must reproduce their exact stats — not an approximation.</summary>
    [Fact]
    public void test_a_restored_hunter_has_identical_stats()
    {
        var (hunter, region, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, inventory, Now);

        var restored = new Hunter();
        SaveSystem.RestoreHunter(save, restored);

        Assert.Equal(hunter.Gleam, restored.Gleam);
        Assert.Equal(hunter.AttackPower, restored.AttackPower);
        Assert.Equal(hunter.Defense, restored.Defense);
        Assert.Equal(hunter.MaxHealth, restored.MaxHealth);
        Assert.Equal(hunter.RankOf(HunterStat.AttackPower), restored.RankOf(HunterStat.AttackPower));
    }

    // ── Failure handling. A save is a player's hours. Never guess. ─────────────────────────────

    [Fact]
    public void test_a_missing_save_is_reported_rather_than_crashing()
    {
        Assert.Equal(LoadFailure.Missing, SaveSystem.Deserialize(null, Now).Failure);
        Assert.Equal(LoadFailure.Missing, SaveSystem.Deserialize("   ", Now).Failure);
    }

    /// <summary>
    /// A corrupt save must be REPORTED, never silently replaced by a fresh game.
    /// </summary>
    /// <remarks>
    /// Silently starting over is the single worst failure mode a save system has: it looks exactly
    /// like the game deleted the player's progress on purpose.
    /// </remarks>
    [Fact]
    public void test_a_corrupt_save_is_reported_and_never_silently_becomes_a_new_game()
    {
        var result = SaveSystem.Deserialize("{ this is not json ]]", Now);

        Assert.Equal(LoadFailure.Corrupt, result.Failure);
        Assert.Null(result.Save);
        Assert.False(result.Ok);
        Assert.Contains("NOT BEEN OVERWRITTEN", SaveSystem.Explain(LoadFailure.Corrupt));
    }

    /// <summary>A save from a newer build is refused rather than half-read.</summary>
    [Fact]
    public void test_a_save_from_a_newer_version_is_refused()
    {
        var future = SaveSystem.Serialize(new SaveGame { Version = SaveGame.CurrentVersion + 1, SavedAtMs = Now });
        // (CurrentVersion is 2 as of the item-system redesign; this test floats with the constant.)

        Assert.Equal(LoadFailure.FromNewerVersion, SaveSystem.Deserialize(future, Now).Failure);
    }

    // ── Offline progression ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_offline_time_is_measured_from_the_save_timestamp()
    {
        var save = SaveSystem.Serialize(new SaveGame { SavedAtMs = Now });
        var twoHoursLater = Now + 2 * 3600 * 1000L;

        Assert.Equal(7200.0, SaveSystem.Deserialize(save, twoHoursLater).OfflineSeconds, precision: 1);
    }

    /// <summary>
    /// A clock that moves BACKWARDS must never produce negative offline time.
    /// </summary>
    /// <remarks>
    /// Timezone changes, DST, and manual clock edits all do this. Negative time would either crash the
    /// catch-up loop or, worse, silently subtract progress.
    /// </remarks>
    [Fact]
    public void test_a_backwards_clock_yields_zero_offline_time_rather_than_negative()
    {
        var save = SaveSystem.Serialize(new SaveGame { SavedAtMs = Now });
        var earlier = Now - 10 * 3600 * 1000L;

        Assert.Equal(0.0, SaveSystem.Deserialize(save, earlier).OfflineSeconds);
    }

    /// <summary>Offline credit is capped — six months away must not hand over six months of income.</summary>
    [Fact]
    public void test_offline_credit_is_capped()
    {
        var sixMonths = 180 * 24 * 3600.0;

        Assert.Equal(SaveSystem.MaxOfflineSeconds, SaveSystem.CreditedOfflineSeconds(sixMonths));
        Assert.Equal(3600.0, SaveSystem.CreditedOfflineSeconds(3600.0)); // a normal absence is untouched
    }

    /// <summary>Memory Dust and its owned unlocks survive a reload — nothing prestige is ever lost.</summary>
    [Fact]
    public void test_memory_dust_and_unlocks_survive_a_reload()
    {
        var tree = new ResonanceHunter.Core.Prestige.MemoryDustTree();
        tree.AwardFromMastery(200);   // dust, the Warren material — still saved
        tree.SetEarned(200);          // trait points, which is what the tree actually spends
        tree.Purchase("recall_1");
        tree.Purchase("socket_2");

        var region = new Region("r", 15);
        var save = SaveSystem.Capture(new Hunter(), region,
            Array.Empty<ItemInstance>(), Now, tree, highestMasteryAwarded: 2);

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now).Save!;

        Assert.Equal(tree.MemoryDust, loaded.MemoryDust);
        Assert.Equal(2, loaded.HighestMasteryAwarded);
        Assert.Contains("recall_1", loaded.MemoryDustUnlocks);
        Assert.Contains("socket_2", loaded.MemoryDustUnlocks);

        var restored = new ResonanceHunter.Core.Prestige.MemoryDustTree();
        restored.Restore(loaded.MemoryDust, loaded.MemoryDustUnlocks);
        Assert.True(restored.Owns("recall_1"));
        Assert.True(restored.Owns("socket_2"));
    }

    [Fact]
    public void test_serialization_is_stable_and_round_trips_byte_for_byte()
    {
        var (hunter, region, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, inventory, Now);

        var once = SaveSystem.Serialize(save);
        var twice = SaveSystem.Serialize(SaveSystem.Deserialize(once, Now).Save!);

        Assert.Equal(once, twice);
    }
}

// World / multi-region persistence.
public class WorldSaveTests
{
    private const long Now = 1_700_000_000_000L;

    [Xunit.Fact]
    public void test_conquered_regions_and_per_region_farms_survive_a_reload()
    {
        var world = new ResonanceHunter.Core.Encounters.World();
        world.Conquer(ResonanceHunter.Core.Encounters.VerdantHollow.RegionId); // unlocks cinderworks

        var home = world.RegionFarm(ResonanceHunter.Core.Encounters.VerdantHollow.RegionId);
        for (var i = 0; i < 40; i++) home.RecordActiveKill();
        home.RecordDepth(17);

        var save = SaveSystem.Capture(new Hunter(), home, System.Array.Empty<ItemInstance>(),
            Now, world: world, activeRegion: "cinderworks");

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now).Save!;

        Xunit.Assert.Contains(ResonanceHunter.Core.Encounters.VerdantHollow.RegionId, loaded.ConqueredRegions);
        Xunit.Assert.Equal("cinderworks", loaded.ActiveRegion);
        Xunit.Assert.Equal(ResonanceHunter.Core.Encounters.Regions.All.Count, loaded.RegionFarms.Count);

        // Restore into a fresh world.
        var w2 = new ResonanceHunter.Core.Encounters.World();
        w2.RestoreConquered(loaded.ConqueredRegions);
        Xunit.Assert.True(w2.IsConquered(ResonanceHunter.Core.Encounters.VerdantHollow.RegionId));
        Xunit.Assert.True(w2.IsUnlocked("cinderworks"));

        var homeFarm = loaded.RegionFarms.First(f => f.Id == ResonanceHunter.Core.Encounters.VerdantHollow.RegionId);
        Xunit.Assert.True(homeFarm.MasteryPoints > 0);
        Xunit.Assert.Equal(17, homeFarm.BestDepth);
    }

    /// <summary>An old single-region save (no RegionFarms) still loads — the fields are simply absent.</summary>
    [Xunit.Fact]
    public void test_a_legacy_single_region_save_still_loads()
    {
        var legacy = SaveSystem.Serialize(new SaveGame
        {
            SavedAtMs = Now, RegionMasteryPoints = 1500,
        });

        var loaded = SaveSystem.Deserialize(legacy, Now);
        Xunit.Assert.True(loaded.Ok);
        Xunit.Assert.Empty(loaded.Save!.RegionFarms);          // no multi-region data...
        Xunit.Assert.Equal(1500, loaded.Save.RegionMasteryPoints); // ...but the old fields are intact
    }

    /// <summary>
    /// A career counter shown beside persisted ones must itself be persisted.
    /// </summary>
    /// <remarks>
    /// CHESTS OPENED sat on the STATS page between HIGHEST WAVE and MASTERY POINTS, both of which
    /// survive a reload, and it did not — it lived in a field on the Forge screen that nothing saved,
    /// so a career of hundreds read zero after every launch. It also feeds
    /// <c>QuestGoal.ChestsOpened</c>, so the loss was not only cosmetic.
    /// </remarks>
    [Fact]
    public void test_the_career_chest_count_survives_a_reload()
    {
        var save = new SaveGame { ChestsOpened = 412 };

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);
        Assert.True(loaded.Ok);

        Assert.Equal(412, loaded.Save!.ChestsOpened);
    }

    [Fact]
    public void test_a_save_written_before_chests_were_counted_restores_to_zero_not_to_garbage()
    {
        // The migration case. An older save has no such field; it must come back as 0.
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(new SaveGame()), Now);
        Assert.True(loaded.Ok);
        Assert.Equal(0, loaded.Save!.ChestsOpened);
    }
}
