using System;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Tests.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// Save v3: a woven skill's persisted identity is its SkillId, and every older save migrates onto
/// the id it has ALWAYS meant — through the same slot-kind walk the composer uses, spill included.
/// </summary>
/// <remarks>
/// The 2026-08-31 audit's #1 finding: `SkillId` existed in memory and reached neither the save, the
/// pick, nor the fight — the skill's identity everywhere durable was `(Form, Passive)`. These are
/// the fixtures that did not exist then: real JSON from the two older generations of save, walked
/// through `PlayerLoadout.Restore` (now in Core precisely so this file can exist) and asserted on
/// the resolved ids.
/// </remarks>
public class SkillIdentityMigrationTest
{
    private const long Now = 1_700_000_000_000L;

    private static PlayerLoadout Restored(SaveGame save, int capacity = 4)
    {
        var loadout = new PlayerLoadout { SkillCapacity = capacity };
        loadout.Restore(
            save.WovenSkills.Select(s => (s.SkillId, (string?)s.Source, (string?)s.Form, s.VowId, s.Passive)),
            save.SocketedKeystoneIds);
        return loadout;
    }

    [Fact]
    public void test_a_pre_slot_rework_save_migrates_each_form_onto_the_skill_it_meant()
    {
        // Arrange — the oldest live generation: Source×Form×Vow rows, no Passive flag, no SkillId.
        var json = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "WovenSkills": [
            { "Source": "Body",   "Form": "Strike",     "VowId": "vow_fragility" },
            { "Source": "Mind",   "Form": "Projectile" },
            { "Source": "Nature", "Form": "Aura" },
            { "Source": "Shadow", "Form": "Trap" }
          ]
        }
        """;
        var loaded = SaveSystem.Deserialize(json, Now);
        Assert.True(loaded.Ok);

        // Act
        var loadout = Restored(loaded.Save!);

        // Assert — actives resolve to the styles' actives, the two naturally-passive Forms to the
        // styles' passives, and the vow survives on its slot.
        Assert.Equal(new[] { "hammer_blow", "volley_spray", "field_mire", "snare_jaws" },
                     loadout.Skills.Select(s => s.SkillId).ToArray());
        Assert.Equal("vow_fragility", loadout.Skills[0].VowId);
    }

    [Fact]
    public void test_a_passive_slot_choice_migrates_onto_the_styles_other_skill()
    {
        // Arrange — the post-slot-rework, pre-v3 generation: a Passive flag but still no id.
        var json = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "WovenSkills": [ { "Source": "Body", "Form": "Strike", "Passive": true } ]
        }
        """;
        var loaded = SaveSystem.Deserialize(json, Now);

        // Act / Assert — a Strike deliberately parked in a passive slot is PRESS, not BLOW.
        Assert.Equal("hammer_press", Restored(loaded.Save!).Skills.Single().SkillId);
    }

    [Fact]
    public void test_a_four_active_legacy_build_migrates_its_spilled_skills_onto_the_passives()
    {
        // Arrange — four beat-taking Forms against a 2-active budget: the composer's spill rule
        // has always sent the overflow to the passive siblings, and the migration must agree.
        var save = new SaveGame
        {
            SavedAtMs = Now,
            WovenSkills =
            {
                new SavedSkill { Source = "Body", Form = "Strike" },
                new SavedSkill { Source = "Mind", Form = "Projectile" },
                new SavedSkill { Source = "Nature", Form = "Transformation" },
                new SavedSkill { Source = "Spirit", Form = "Mark" },
            },
        };

        // Act
        var ids = Restored(save).Skills.Select(s => s.SkillId).ToArray();

        // Assert — first two keep their actives; the third and fourth spill to WILT and BRAND.
        Assert.Equal(new[] { "hammer_blow", "volley_spray", "drain_wilt", "sign_brand" }, ids);
    }

    [Fact]
    public void test_a_v3_row_is_identified_by_its_id_even_with_no_form_beside_it()
    {
        // Arrange — the new format, minus the legacy echo entirely.
        var loadout = new PlayerLoadout { SkillCapacity = 4 };

        // Act
        loadout.Restore(new[] { ((string?)"hammer_press", (string?)"", (string?)"", (string?)null, (bool?)null) },
                        Array.Empty<string>());

        // Assert — the id wins, the slot kind comes from the skill, and the next save writes the id.
        var row = loadout.SaveSkills().Single();
        Assert.Equal("hammer_press", row.SkillId);
        Assert.True(loadout.Skills.Single().Passive);
    }

    [Fact]
    public void test_an_unknown_id_falls_back_to_the_form_and_garbage_is_dropped()
    {
        // Arrange
        var loadout = new PlayerLoadout { SkillCapacity = 4 };

        // Act — one row with a future id but a readable Form, one row with nothing readable.
        loadout.Restore(new[]
        {
            ((string?)"hammer_meteor", (string?)"Body", (string?)"Strike", (string?)null, (bool?)false),
            ((string?)null, (string?)"???", (string?)"???", (string?)null, (bool?)null),
        }, Array.Empty<string>());

        // Assert — the readable row survives under its Form-era identity; the other is gone.
        Assert.Equal("hammer_blow", loadout.Skills.Single().SkillId);
    }

    [Fact]
    public void test_the_id_reaches_the_composed_build_and_the_fight_reads_the_same_skill()
    {
        // Arrange — a real old save, restored, then composed exactly as the hunt composes.
        var json = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "WovenSkills": [
            { "Source": "Body",   "Form": "Strike" },
            { "Source": "Nature", "Form": "Aura" }
          ]
        }
        """;
        var loadout = Restored(SaveSystem.Deserialize(json, Now).Save!);

        // Act
        var build = loadout.ToBuild(new MemoryDustTree(), Taught.Everything(), character: null);

        // Assert — the resolved def carries the id: the only identity a skill has left (P3-final).
        Assert.Equal(2, build.Skills.Count);
        Assert.Equal("hammer_blow", build.Skills[0].Def.Id);
        Assert.Equal("field_mire", build.Skills[1].Def.Id);
    }

    [Fact]
    public void test_the_current_save_version_is_four()
    {
        // RE-PINNED 2026-09-03 (was three). The bump is what makes an OLDER build refuse a new file as
        // FromNewerVersion instead of reporting it Corrupt (required members) — quarantining the
        // player's live save by mistake. It is also what makes SnapshotBeforeUpgrade copy the old file
        // aside: its guard is `if (fileVersion >= CurrentVersion) return null;`, so without a bump the
        // keystone/Vow migration and the fifth-slot removal would have no undo at all.
        Assert.Equal(4, SaveGame.CurrentVersion);
    }
}
