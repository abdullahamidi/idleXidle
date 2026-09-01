using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// LAW 13 (UI polish brief §2–§6): a SkillId may occupy at most ONE loadout slot. The rule lives in the
/// domain — <see cref="PlayerLoadout"/> refuses the duplicate, the restore path clears one, the composer
/// never equips one, and progression counts an equipped skill once per wave.
/// </summary>
/// <remarks>
/// Before this file existed the weave screen only refused a skill for the slot it was already in
/// (LoadoutScreen.Primary), so slot 2 could take slot 1's skill: the composer equipped it twice with
/// two independent cooldowns (Champion.ReadyAtBeat is per slot) and the expedition recorded two uses
/// per cleared wave. Every test here names the production change that would make it fail.
/// </remarks>
public class LoadoutDuplicateSkillTests
{
    private const string Blow = "hammer_blow";
    private const string Spray = "volley_spray";
    private const string Mire = "field_mire";

    private static PlayerLoadout Loadout(int capacity, params string[] ids)
    {
        var l = new PlayerLoadout { SkillCapacity = capacity };
        foreach (var id in ids) Assert.True(l.SetSkill(l.AddSkill(), id));
        return l;
    }

    // ── SetSkill: the invariant at the point of choice ──────────────────────────────────────────

    [Fact]
    public void test_equipping_a_skill_already_in_another_slot_is_refused_and_both_slots_stay()
    {
        // Arrange — BLOW in slot 1, SPRAY in slot 2.
        var l = Loadout(4, Blow, Spray);

        // Act — try to put BLOW into slot 2 as well.
        var accepted = l.SetSkill(1, Blow);

        // Assert — refused; slot 1 still BLOW, slot 2 still SPRAY.
        Assert.False(accepted);
        Assert.Equal(Blow, l.Skills[0].SkillId);
        Assert.Equal(Spray, l.Skills[1].SkillId);
    }

    [Fact]
    public void test_re_equipping_a_skill_into_the_slot_it_already_holds_is_a_no_op_success()
    {
        var l = Loadout(4, Blow, Spray);

        Assert.True(l.SetSkill(0, Blow));

        Assert.Equal(Blow, l.Skills[0].SkillId);
        Assert.Equal(Spray, l.Skills[1].SkillId);
    }

    [Fact]
    public void test_switching_a_slot_to_a_different_skill_still_works()
    {
        var l = Loadout(4, Blow, Spray);

        Assert.True(l.SetSkill(1, Mire));

        Assert.Equal(Mire, l.Skills[1].SkillId);
    }

    [Fact]
    public void test_index_of_skill_names_the_slot_that_holds_it()
    {
        var l = Loadout(4, Blow, Spray);

        Assert.Equal(0, l.IndexOfSkill(Blow));
        Assert.Equal(1, l.IndexOfSkill(Spray));
        Assert.Equal(-1, l.IndexOfSkill(Mire));
        Assert.True(l.HasSkill(Spray));
        Assert.False(l.HasSkill(Mire));
    }

    [Fact]
    public void test_an_unknown_skill_id_is_still_refused()
    {
        var l = Loadout(4, Blow);
        Assert.False(l.SetSkill(0, "not_a_skill"));
        Assert.Equal(Blow, l.Skills[0].SkillId);
    }

    [Fact]
    public void test_moving_a_skill_between_slots_keeps_the_invariant_intact()
    {
        var l = Loadout(4, Blow, Spray, Mire);

        Assert.True(l.MoveSkill(0, 2));

        Assert.Equal(new[] { Spray, Mire, Blow }, l.Skills.Select(s => s.SkillId));
        Assert.Equal(3, l.Skills.Select(s => s.SkillId).Distinct().Count());
    }

    // ── Restore: the migration for a save that already carries a duplicate ──────────────────────

    [Fact]
    public void test_restore_keeps_the_first_of_two_identical_v3_rows_and_clears_the_later_one()
    {
        var l = new PlayerLoadout { SkillCapacity = 4 };

        // Arrange — a v3 save where slot 1 and slot 2 both say BLOW; slot 2's own vow rides on it.
        l.Restore(new[]
        {
            ((string?)Blow, (string?)"Body", (string?)null, (string?)null, (bool?)false),
            ((string?)Blow, (string?)"Mind", (string?)null, (string?)"vow_fragility", (bool?)false),
            ((string?)Spray, (string?)"Mind", (string?)null, (string?)null, (bool?)false),
        }, Array.Empty<string>());

        // Assert — the first occurrence survives untouched; the duplicate slot is CLEARED, not removed,
        // so every later slot keeps its position (slot order is the sim's tie-break priority).
        Assert.Equal(3, l.Skills.Count);
        Assert.Equal(Blow, l.Skills[0].SkillId);
        Assert.Equal(Source.Body, l.Skills[0].Source);
        Assert.Null(l.Skills[1].SkillId);
        Assert.Equal(Spray, l.Skills[2].SkillId);
        Assert.Equal(1, l.Skills.Count(s => s.SkillId == Blow));
    }

    [Fact]
    public void test_restore_dedups_two_legacy_form_rows_that_resolve_to_the_same_skill()
    {
        // Arrange — a pre-v3 save with two naturally-passive AURA rows: both resolve to MIRE.
        var l = new PlayerLoadout { SkillCapacity = 4 };
        l.Restore(new[]
        {
            ((string?)null, (string?)"Body", (string?)"Strike", (string?)null, (bool?)null),
            ((string?)null, (string?)"Nature", (string?)"Aura", (string?)null, (bool?)null),
            ((string?)null, (string?)"Shadow", (string?)"Aura", (string?)null, (bool?)null),
        }, Array.Empty<string>());

        // Assert — MIRE appears once, in the earlier slot; the later AURA row is cleared.
        var ids = l.Skills.Select(s => s.SkillId).ToList();
        Assert.Equal(1, ids.Count(id => id == Mire));
        Assert.Equal(Mire, ids[1]);
        Assert.Null(ids[2]);
    }

    [Fact]
    public void test_restore_dedup_does_not_cost_a_later_skill_its_slot()
    {
        // Arrange — [BLOW, BLOW, SPRAY, MIRE] at capacity 4: the duplicate must not push MIRE out.
        var l = new PlayerLoadout { SkillCapacity = 4 };
        l.Restore(new[]
        {
            ((string?)Blow, (string?)"Body", (string?)null, (string?)null, (bool?)false),
            ((string?)Blow, (string?)"Body", (string?)null, (string?)null, (bool?)false),
            ((string?)Spray, (string?)"Mind", (string?)null, (string?)null, (bool?)false),
            ((string?)Mire, (string?)"Nature", (string?)null, (string?)null, (bool?)true),
        }, Array.Empty<string>());

        Assert.Contains(Mire, l.Skills.Select(s => s.SkillId));
        Assert.Equal(1, l.Skills.Count(s => s.SkillId == Blow));
    }

    [Fact]
    public void test_a_v3_save_with_a_duplicate_survives_a_full_round_trip_with_one_copy_and_its_progress()
    {
        // Arrange — a real serialised save carrying the duplicate plus levelled progress on that skill.
        const long now = 1_700_000_000_000L;
        var progress = new SkillProgress();
        var blowDef = SkillCatalogue.ById(Blow);
        for (var i = 0; i < SkillProgress.UsesForLevel(SkillProgress.MaxLevel); i++) progress.RecordWave(Blow);
        var variationName = blowDef.Variations[0].Name;
        Assert.True(progress.ChooseVariation(blowDef, variationName));
        var reinforcement = blowDef.Variations[0].Reinforcements[0].Name;
        Assert.True(progress.TakeReinforcement(blowDef, reinforcement));

        var save = SaveSystem.Capture(new IdleXIdle.Core.Economy.Hunter(), new List<IdleXIdle.Core.Loot.ItemInstance>(), now) with
        {
            WovenSkills = new List<SavedSkill>
            {
                new() { SkillId = Blow, Source = "Body", Passive = false },
                new() { SkillId = Blow, Source = "Body", Passive = false },
            },
            SkillProgress = progress.ToSave().Select(r => new SavedSkillProgress
            {
                SkillId = r.SkillId, Uses = r.Uses, Variation = r.Variation, Reinforcements = r.Taken.ToList(),
            }).ToList(),
        };

        // Act — serialise, deserialise, restore.
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), now);
        Assert.True(loaded.Ok);
        var back = loaded.Save!;
        var l = new PlayerLoadout { SkillCapacity = 4 };
        l.Restore(back.WovenSkills.Select(s => (s.SkillId, (string?)s.Source, (string?)s.Form, s.VowId, s.Passive)),
                  back.SocketedKeystoneIds);
        var restoredProgress = new SkillProgress();
        restoredProgress.Restore(back.SkillProgress.Select(r => (r.SkillId, r.Uses, r.Variation, (IReadOnlyList<string>)r.Reinforcements)));

        // Assert — one BLOW; the level, variation and reinforcement it earned are all still there.
        Assert.Equal(1, l.Skills.Count(s => s.SkillId == Blow));
        Assert.Equal(SkillProgress.MaxLevel, SkillProgress.LevelFor(restoredProgress.UsesOf(Blow)));
        Assert.Equal(variationName, restoredProgress.VariationOf(blowDef)?.Name);
        Assert.True(restoredProgress.HasReinforcement(Blow, reinforcement));
    }

    // ── The composer and the expedition: defence in depth, and the progression guarantee ────────

    [Fact]
    public void test_compose_never_equips_the_same_skill_twice_even_when_handed_two_picks()
    {
        var build = BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: new[]
            {
                new BuildComposer.SkillPick(Source.Body, null, SkillId: Blow),
                new BuildComposer.SkillPick(Source.Mind, null, SkillId: Blow),
                new BuildComposer.SkillPick(Source.Nature, null, SkillId: Mire),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        Assert.Equal(1, build.Skills.Count(s => s.Def.Id == Blow));
        Assert.Equal(2, build.Skills.Count);
    }

    [Fact]
    public void test_an_equipped_skill_is_recorded_once_per_cleared_wave_never_twice()
    {
        // Arrange — the composer is handed a duplicate pick; the run must still count one use per wave.
        var progress = new SkillProgress();
        var build = BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: new[]
            {
                new BuildComposer.SkillPick(Source.Body, null, SkillId: Blow),
                new BuildComposer.SkillPick(Source.Body, null, SkillId: Blow),
                new BuildComposer.SkillPick(Source.Nature, null, SkillId: Mire),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4, progress: progress);

        var hunter = new IdleXIdle.Core.Economy.Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 9f,
            IdleXIdle.Core.Expeditions.ExpeditionTuning.Default, new Random(31)) { Progress = progress };

        while (!run.Over && run.Wave < 6)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
        }

        Assert.True(run.Wave > 0, "the run cleared nothing — the fixture cannot prove the count");
        Assert.Equal(run.Wave, progress.UsesOf(Blow));
        Assert.Equal(run.Wave, progress.UsesOf(Mire));
    }

    // ── Share codes: the inspect card must not show a build the game cannot hold ────────────────

    [Fact]
    public void test_a_shared_build_carrying_a_duplicate_decodes_with_one_copy()
    {
        var code = ShareCodes.EncodeBuild(new ShareCodes.SharedBuild
        {
            Skills = new List<SavedSkill>
            {
                new() { SkillId = Blow, Source = "Body", Passive = false },
                new() { SkillId = Blow, Source = "Body", Passive = false },
                new() { SkillId = Spray, Source = "Mind", Passive = false },
            },
        });

        Assert.True(ShareCodes.TryDecodeBuild(code, out var build, out var error), error);
        Assert.Equal(1, build!.Skills.Count(s => s.SkillId == Blow));
        Assert.Equal(2, build.Skills.Count);
    }
}
