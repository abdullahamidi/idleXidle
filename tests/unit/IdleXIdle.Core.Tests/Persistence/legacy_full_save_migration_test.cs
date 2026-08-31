using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// THE WHOLE JOURNEY, one fixture: a hand-written save from the Form era (pre-v3, creature-era and
/// retired keys included) loads, migrates, plays as ids, saves as ids, and reloads STABLE. Every
/// migration this suite pins piecemeal elsewhere is walked here end to end — the shape a real
/// player's file actually takes through an update.
/// </summary>
public class LegacyFullSaveMigrationTest
{
    private const long Now = 1_700_000_000_000L;

    private const string FormEraJson = """
    {
      "Version": 2,
      "SavedAtMs": 1700000000000,
      "Gleam": 4200,
      "Materials": 55,
      "Charters": { "Merge": 2, "Refine": 1, "Sharpen": 9 },
      "TrainingRanks": { "AttackPower": 3 },
      "RegionMasteryPoints": 900,
      "Affinity": "Nature",
      "WarrenMasteryPool": 77400,
      "AutomationStage": 2,
      "ChestKeepSlot": "Ring",
      "WovenSkills": [
        { "Source": "Body", "Form": "Strike", "VowId": null },
        { "Source": "Nature", "Form": "Aura" },
        { "Source": "Mind", "Form": "Trap" },
        { "Source": "Shadow", "Form": "Ghostform" }
      ],
      "SocketedKeystoneIds": []
    }
    """;

    [Fact]
    public void test_a_form_era_save_loads_migrates_and_reloads_stable()
    {
        // ── OLD → LOADED. Retired keys (Affinity, WarrenMasteryPool, AutomationStage) are skipped
        //    silently; legacy fields that still migrate survive as data. ──
        var loaded = SaveSystem.Deserialize(FormEraJson, Now);
        Assert.True(loaded.Ok, "a Form-era save must load, not fail");
        var save = loaded.Save!;
        Assert.Equal(900f, save.RegionMasteryPoints);
        Assert.Equal("Ring", save.ChestKeepSlot);

        // ── THE SKILL MIGRATION: (Form, Passive) rows land on the ids they always meant — the
        //    frozen walk decides slot kinds, and the unknown Form ("Ghostform") is dropped like
        //    every unknown catalogue name. ──
        var loadout = new PlayerLoadout { SkillCapacity = 4 };
        loadout.Restore(
            save.WovenSkills.Select(s => (s.SkillId, s.Source, s.Form, s.VowId, s.Passive)),
            save.SocketedKeystoneIds);
        var ids = loadout.SaveSkills().Select(s => s.SkillId).ToList();
        Assert.Equal(new[] { "hammer_blow", "field_mire", "snare_jaws" }, ids);

        // ── THE WORLD FOLD: the single pre-multi-region number lands on the home region. ──
        var world = new World();
        SaveSystem.RestoreWorld(save, world);
        Assert.Equal(900f, world.RegionFarm(VerdantHollow.RegionId).RegionMasteryPoints, 1);

        // ── THE CHARTER FOLD, in Core now: retired MERGE papers become SALVAGE; the unknown
        //    charter name is dropped. ──
        var hunter = new Hunter();
        SaveSystem.RestoreHunter(save, hunter);
        Assert.Equal(0, hunter.CharterCount(Charter.Merge));
        Assert.Equal(2, hunter.CharterCount(Charter.Salvage));
        Assert.Equal(1, hunter.CharterCount(Charter.Refine));

        // ── NEW → SAVED → RELOADED. The rewritten save speaks ids, writes none of the legacy
        //    vocabulary, and a second restore lands on the same build: the migration runs ONCE,
        //    then retires. ──
        var resaved = SaveSystem.Capture(hunter, new List<ItemInstance>(), Now, world: world) with
        {
            WovenSkills = loadout.SaveSkills()
                .Select(s => new SavedSkill { SkillId = s.SkillId, Source = s.Source, VowId = s.VowId, Passive = s.Passive })
                .ToList(),
        };
        var json2 = SaveSystem.Serialize(resaved);
        Assert.DoesNotContain("\"Form\"", json2);              // the Form vocabulary is never written again
        Assert.Equal(0f, resaved.RegionMasteryPoints);          // the single-region write is retired (P14)

        var again = SaveSystem.Deserialize(json2, Now).Save!;
        var loadout2 = new PlayerLoadout { SkillCapacity = 4 };
        loadout2.Restore(
            again.WovenSkills.Select(s => (s.SkillId, s.Source, s.Form, s.VowId, s.Passive)),
            again.SocketedKeystoneIds);
        Assert.Equal(ids, loadout2.SaveSkills().Select(s => s.SkillId).ToList());
        Assert.Equal(2, SaveSystem.Deserialize(json2, Now).Save!.Charters.GetValueOrDefault("Salvage"));
    }
}
