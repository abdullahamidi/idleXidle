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

    private static (Hunter, Region, List<Creature>, List<ItemInstance>) BuildGame()
    {
        var hunter = new Hunter();
        hunter.AddGleam(5_000);
        for (var i = 0; i < 12; i++) hunter.Train(HunterStat.AttackPower);
        for (var i = 0; i < 5; i++) hunter.Train(HunterStat.Defense);

        var region = new Region("verdant_hollow", 15);
        region.AutomationStage = 2;

        var roster = new List<Creature>
        {
            Creature.Hatch("c1", Source.Nature, Role.Attacker, 7),
            Creature.Hatch("c2", Source.Shadow, Role.Crafter, 4),
            Creature.Hatch("c3", Source.Machine, Role.Defender, 9),
        };
        foreach (var c in roster) region.Assign(c);
        region.Tick(3600f, 10);

        var inventory = new List<ItemInstance>
        {
            new() { InstanceId = "i1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 34 },
            new() { InstanceId = "i2", BaseType = ItemBaseType.Charm, Rarity = Rarity.Epic, SellValue = 82,
                    EquippedToCreatureId = "c1" },
        };

        return (hunter, region, roster, inventory);
    }

    /// <summary>A full round trip must lose nothing. Everything the player earned comes back.</summary>
    [Fact]
    public void test_a_save_round_trip_preserves_everything_the_player_earned()
    {
        var (hunter, region, roster, inventory) = BuildGame();

        var json = SaveSystem.Serialize(
            SaveSystem.Capture(hunter, region, roster, inventory, unhatchedCores: 4, Now));

        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);

        var save = loaded.Save!;

        Assert.Equal(hunter.Gleam, save.Gleam);
        Assert.Equal(12, save.TrainingRanks[nameof(HunterStat.AttackPower)]);
        Assert.Equal(5, save.TrainingRanks[nameof(HunterStat.Defense)]);
        Assert.Equal(region.RegionMasteryPoints, save.RegionMasteryPoints, precision: 1);
        Assert.Equal(2, save.AutomationStage);
        Assert.Equal(4, save.UnhatchedCores);
        Assert.Equal(3, save.Roster.Count);
        Assert.Equal(3, save.AssignedCreatureIds.Count);
        Assert.Equal(2, save.Inventory.Count);
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
        var (hunter, region, roster, _) = BuildGame();
        var dupe = new ItemInstance { InstanceId = "dupe", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 10 };
        var inventory = new List<ItemInstance>
        {
            dupe, dupe, dupe,
            new() { InstanceId = "unique", BaseType = ItemBaseType.Charm, Rarity = Rarity.Common, SellValue = 5 },
        };

        var restored = SaveSystem.RestoreInventory(SaveSystem.Capture(hunter, region, roster, inventory, 0, Now));

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
        var (hunter, region, roster, _) = BuildGame();
        var inv = new List<ItemInstance>
        {
            new() { InstanceId = "e1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 10, Element = Source.Shadow },
            new() { InstanceId = "e2", BaseType = ItemBaseType.Charm, Rarity = Rarity.Common, SellValue = 5 },
        };

        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, roster, inv, 0, Now));
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
        var (hunter, region, roster, _) = BuildGame();

        var baseItem = new ItemInstance
        {
            InstanceId = "reforged_1", BaseType = ItemBaseType.AbilityFocus,
            Rarity = Rarity.Epic, SellValue = 82, Element = Source.Shadow,
        };
        var reforged = baseItem with { TraitOverride = GearTrait.Focused, EnchantOverride = EnchantKind.Radiance };
        var inventory = new List<ItemInstance> { reforged };

        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, roster, inventory, 0, Now));
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
        var (hunter, region, roster, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, roster, inventory, 0, Now) with
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
        var (hunter, region, roster, _) = BuildGame();
        var plain = new ItemInstance
        {
            InstanceId = "plain_1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 34,
        };

        var json = SaveSystem.Serialize(
            SaveSystem.Capture(hunter, region, roster, new List<ItemInstance> { plain }, 0, Now));
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
        var (hunter, region, roster, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, roster, inventory, unhatchedCores: 0, Now) with
        {
            WovenSkills = new List<SavedSkill>
            {
                new() { Source = "Body", Form = "Strike", VowId = null },
                new() { Source = "Shadow", Form = "Trap", VowId = "vow_bloodied" },
            },
            SocketedKeystoneIds = new List<string> { "glass_cannon", "reaper" },
            MasteryTaken = new List<string> { "strike_1", "strike_2" },
            MasteryEarned = 42,
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
        Assert.Equal(3.5f, back.ChampionGleamRate);
    }

    /// <summary>Restoring a Hunter must reproduce their exact stats — not an approximation.</summary>
    [Fact]
    public void test_a_restored_hunter_has_identical_stats()
    {
        var (hunter, region, roster, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, roster, inventory, 0, Now);

        var restored = new Hunter();
        SaveSystem.RestoreHunter(save, restored);

        Assert.Equal(hunter.Gleam, restored.Gleam);
        Assert.Equal(hunter.AttackPower, restored.AttackPower);
        Assert.Equal(hunter.Defense, restored.Defense);
        Assert.Equal(hunter.MaxHealth, restored.MaxHealth);
        Assert.Equal(hunter.RankOf(HunterStat.AttackPower), restored.RankOf(HunterStat.AttackPower));
    }

    /// <summary>An equipped charm must still be equipped after a reload — or it becomes sellable again.</summary>
    [Fact]
    public void test_equipped_state_survives_a_reload()
    {
        var (hunter, region, roster, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, roster, inventory, 0, Now);

        var restored = SaveSystem.RestoreInventory(save);
        var charm = restored.Single(i => i.InstanceId == "i2");

        Assert.Equal("c1", charm.EquippedToCreatureId);
    }

    [Fact]
    public void test_the_roster_and_its_assignments_survive_a_reload()
    {
        var (hunter, region, roster, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, roster, inventory, 0, Now);

        var restoredRoster = SaveSystem.RestoreRoster(save);
        var newRegion = new Region("verdant_hollow", 15);

        foreach (var c in restoredRoster.Where(c => save.AssignedCreatureIds.Contains(c.Id)))
            newRegion.Assign(c);

        Assert.Equal(3, newRegion.Team.Count);
        Assert.Contains(newRegion.Team, c => c.Role == Role.Attacker && c.PowerTier == 7);
        Assert.Contains(newRegion.Team, c => c.Source == Source.Machine);
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

    /// <summary>Offline credit is capped — six months away must not hand over six months of farm.</summary>
    [Fact]
    public void test_offline_credit_is_capped()
    {
        var sixMonths = 180 * 24 * 3600.0;

        Assert.Equal(SaveSystem.MaxOfflineSeconds, SaveSystem.CreditedOfflineSeconds(sixMonths));
        Assert.Equal(3600.0, SaveSystem.CreditedOfflineSeconds(3600.0)); // a normal absence is untouched
    }

    /// <summary>
    /// Offline catch-up must produce the SAME result as having left the game running.
    /// </summary>
    /// <remarks>
    /// If it did not, the player would be punished (or rewarded) for closing the window, and the idle
    /// half of the game would quietly become a lie.
    /// </remarks>
    [Fact]
    public void test_offline_catch_up_equals_leaving_the_game_running()
    {
        var live = new Region("r", 15);
        var offline = new Region("r", 15);

        foreach (var region in new[] { live, offline })
        {
            region.AutomationStage = 3;
            region.Assign(Creature.Hatch("a", Source.Nature, Role.Attacker, 10));
            region.Assign(Creature.Hatch("c", Source.Nature, Role.Crafter, 10));
            region.Assign(Creature.Hatch("d", Source.Nature, Role.Defender, 10));
            region.Assign(Creature.Hatch("s", Source.Nature, Role.Support, 10));
        }

        var liveGleam = 0;
        for (var i = 0; i < 7200; i++) liveGleam += live.Tick(1f, 10).GleamRealized;

        var offlineGleam = offline.Tick(7200f, 10).GleamRealized;

        Assert.Equal(live.RegionMasteryPoints, offline.RegionMasteryPoints, precision: 0);
        Assert.Equal(live.MasteryLevel, offline.MasteryLevel);

        // The hourly Gleam cap must bite identically either way — offline must not dodge it.
        Assert.InRange(offlineGleam, liveGleam - 2, liveGleam + 2);
    }

    /// <summary>Evolution progress must survive a reload in FULL — node, materials, and every tally.</summary>
    [Fact]
    public void test_evolution_progress_survives_a_reload()
    {
        var tree = ResonanceHunter.Core.Evolution.EvolutionTrees.For(ResonanceHunter.Core.Automation.Source.Nature);

        var whelp = Creature.Hatch("evo1", Source.Nature, Role.Attacker, 3);
        whelp.BeginEvolution(tree, new ResonanceHunter.Core.Evolution.EvolutionProgress());
        for (var i = 0; i < 25; i++) whelp.Evolution!.FeedMaterial(1);
        for (var i = 0; i < 3; i++) whelp.Evolution!.RecordCombat(ResonanceHunter.Core.Evolution.CombatTags.BossFelled);
        for (var i = 0; i < 12; i++) whelp.Evolution!.RecordWorkTick(Role.Attacker, healthy: true);
        whelp.Evolution!.EquippedTrait = ResonanceHunter.Core.Evolution.CombatTags.WardingTrait;

        var region = new Region("r", 15);
        var save = SaveSystem.Capture(new Hunter(), region, new[] { whelp },
            Array.Empty<ItemInstance>(), 0, Now);

        var json = SaveSystem.Serialize(save);
        var restored = SaveSystem.RestoreRoster(SaveSystem.Deserialize(json, Now).Save!).Single();

        Assert.Equal(ResonanceHunter.Core.Evolution.EvolutionTrees.RootIdFor(ResonanceHunter.Core.Automation.Source.Nature), restored.EvolutionNodeId);
        Assert.Equal(25, restored.Evolution!.Materials);
        Assert.Equal(3, restored.Evolution.CombatTally.GetValueOrDefault(ResonanceHunter.Core.Evolution.CombatTags.BossFelled));
        Assert.Equal(12, restored.Evolution.HealthyWorkTicks.GetValueOrDefault(Role.Attacker));
        Assert.Equal(ResonanceHunter.Core.Evolution.CombatTags.WardingTrait, restored.Evolution.EquippedTrait);

        // ...and it can still evolve from exactly where it left off, once the last condition is met.
        restored.Evolution.RecordCombat(ResonanceHunter.Core.Evolution.CombatTags.BossFelled); // now 4
        for (var i = 0; i < 5; i++) restored.Evolution.FeedMaterial(1);            // now 30
        Assert.Equal("nature_atk", restored.TryEvolve(tree));
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
        var save = SaveSystem.Capture(new Hunter(), region, Array.Empty<Creature>(),
            Array.Empty<ItemInstance>(), 0, Now, tree, highestMasteryAwarded: 2);

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
        var (hunter, region, roster, inventory) = BuildGame();
        var save = SaveSystem.Capture(hunter, region, roster, inventory, 2, Now);

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
        home.Assign(Creature.Hatch("a", Source.Nature, Role.Attacker, 5));
        home.Tick(3600f, 10);
        home.AutomationStage = 3;

        var cinder = world.RegionFarm("cinderworks");
        cinder.Assign(Creature.Hatch("b", Source.Machine, Role.Crafter, 6));

        var roster = new[]
        {
            Creature.Hatch("a", Source.Nature, Role.Attacker, 5),
            Creature.Hatch("b", Source.Machine, Role.Crafter, 6),
        };

        var save = SaveSystem.Capture(new Hunter(), home, roster, System.Array.Empty<ItemInstance>(),
            0, Now, world: world, activeRegion: "cinderworks");

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
        Xunit.Assert.Equal(3, homeFarm.Stage);
        Xunit.Assert.Contains("a", homeFarm.AssignedIds);

        var cinderFarm = loaded.RegionFarms.First(f => f.Id == "cinderworks");
        Xunit.Assert.Contains("b", cinderFarm.AssignedIds);
    }

    /// <summary>An old single-region save (no RegionFarms) still loads — the fields are simply absent.</summary>
    [Xunit.Fact]
    public void test_a_legacy_single_region_save_still_loads()
    {
        var legacy = SaveSystem.Serialize(new SaveGame
        {
            SavedAtMs = Now, RegionMasteryPoints = 1500, AutomationStage = 2,
            AssignedCreatureIds = new System.Collections.Generic.List<string> { "x" },
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
    /// so a career of hundreds read zero after every launch. It also feeds the CRAFTER evolution path
    /// and <c>QuestGoal.ChestsOpened</c>, so the loss was not only cosmetic.
    ///
    /// The CREDITED counter round-trips with it, and has to: CRAFTER is paid on the delta between the
    /// two, so restoring the total alone would hand a whole restored career to the evolution path again
    /// on the first frame after every load.
    /// </remarks>
    [Fact]
    public void test_the_career_chest_count_and_its_credited_half_survive_a_reload()
    {
        var save = new SaveGame { ChestsOpened = 412, ChestsCredited = 400 };

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);
        Assert.True(loaded.Ok);

        Assert.Equal(412, loaded.Save!.ChestsOpened);
        Assert.Equal(400, loaded.Save!.ChestsCredited);
    }

    [Fact]
    public void test_a_save_written_before_chests_were_counted_restores_to_zero_not_to_garbage()
    {
        // The migration case. An older save has neither field; both must come back as 0 so the delta
        // the host credits is 0 - 0 rather than "everything you ever opened".
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(new SaveGame()), Now);
        Assert.True(loaded.Ok);
        Assert.Equal(0, loaded.Save!.ChestsOpened);
        Assert.Equal(0, loaded.Save!.ChestsCredited);
    }
}