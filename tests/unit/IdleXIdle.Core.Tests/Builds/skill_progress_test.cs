using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Prestige;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A skill's own levels, and what they buy (design §5).
/// </summary>
/// <remarks>
/// The second of the game's two currencies. Mastery points come from DEPTH and buy the champion; a
/// skill's levels come from USING THAT SKILL and buy its identity — one of two variations, then that
/// variation's three reinforcements. Four purchases in all.
/// </remarks>
public class SkillProgressTests
{
    private static SkillDef Hammer => SkillCatalogue.ActiveOf(Style.Hammer);

    private static SkillProgress Levelled(SkillDef def, int level)
    {
        var p = new SkillProgress();
        for (var i = 0; i < SkillProgress.UsesForLevel(level); i++) p.RecordWave(def.Id);
        return p;
    }

    [Fact]
    public void test_a_skill_levels_by_being_used_and_the_next_level_is_always_further()
    {
        var p = new SkillProgress();
        Assert.Equal(0, p.LevelOf(Hammer.Id));

        var thresholds = new List<int>();
        var last = 0;
        for (var wave = 1; wave <= 200; wave++)
        {
            p.RecordWave(Hammer.Id);
            var now = p.LevelOf(Hammer.Id);
            if (now > last) { thresholds.Add(wave); last = now; }
        }

        Assert.Equal(SkillProgress.MaxLevel, last);
        // A square root, so each level costs more than the one before it — the shape a wall should have.
        for (var i = 1; i < thresholds.Count; i++)
            Assert.True(thresholds[i] - thresholds[i - 1] > thresholds[i - 1] - (i >= 2 ? thresholds[i - 2] : 0),
                $"level {i + 1} cost no more than level {i}: {string.Join(", ", thresholds)}");
    }

    [Fact]
    public void test_using_one_skill_does_not_level_another()
    {
        var p = new SkillProgress();
        for (var i = 0; i < 40; i++) p.RecordWave(Hammer.Id);
        Assert.True(p.LevelOf(Hammer.Id) > 0);
        Assert.Equal(0, p.LevelOf(SkillCatalogue.PassiveOf(Style.Hammer).Id));
    }

    [Fact]
    public void test_the_variation_comes_first_and_a_reinforcement_must_belong_to_it()
    {
        var p = Levelled(Hammer, 4);
        var a = Hammer.Variations[0];
        var b = Hammer.Variations[1];

        // A reinforcement before the variation is refused: there is nothing yet for it to strengthen.
        Assert.False(p.TakeReinforcement(Hammer, a.Reinforcements[0].Name));

        Assert.True(p.ChooseVariation(Hammer, a.Name));
        Assert.Equal(a.Name, p.VariationOf(Hammer)!.Name);

        // The OTHER variation's reinforcements are worthless to this one, and refused.
        Assert.False(p.TakeReinforcement(Hammer, b.Reinforcements[0].Name));
        Assert.True(p.TakeReinforcement(Hammer, a.Reinforcements[0].Name));
    }

    [Fact]
    public void test_a_skill_cannot_spend_levels_it_has_not_earned()
    {
        var p = Levelled(Hammer, 1);
        var v = Hammer.Variations[0];
        Assert.True(p.ChooseVariation(Hammer, v.Name));
        Assert.Equal(0, p.FreeOn(Hammer.Id));
        Assert.False(p.TakeReinforcement(Hammer, v.Reinforcements[0].Name));

        // Earn one more and it goes through.
        for (var i = 0; i < SkillProgress.UsesForLevel(2); i++) p.RecordWave(Hammer.Id);
        Assert.True(p.TakeReinforcement(Hammer, v.Reinforcements[0].Name));
    }

    [Fact]
    public void test_a_variation_is_a_commitment_until_it_is_respecced()
    {
        var p = Levelled(Hammer, 4);
        Assert.True(p.ChooseVariation(Hammer, Hammer.Variations[0].Name));
        // Swapping in place would strand everything bought after it, so it is refused outright.
        Assert.False(p.ChooseVariation(Hammer, Hammer.Variations[1].Name));

        // Respec is free and keeps every level the skill earned — the whole point, because per-skill
        // progress that punishes experimenting is the known failure of the system this copies.
        var earned = p.LevelOf(Hammer.Id);
        p.Respec(Hammer.Id);
        Assert.Null(p.VariationOf(Hammer));
        Assert.Equal(earned, p.LevelOf(Hammer.Id));
        Assert.Equal(earned, p.FreeOn(Hammer.Id));
        Assert.True(p.ChooseVariation(Hammer, Hammer.Variations[1].Name));
    }

    [Fact]
    public void test_progress_survives_a_save_and_drops_what_no_longer_exists()
    {
        var p = Levelled(Hammer, 3);
        var v = Hammer.Variations[0];
        p.ChooseVariation(Hammer, v.Name);
        p.TakeReinforcement(Hammer, v.Reinforcements[0].Name);

        var rows = p.ToSave().ToList();
        // A row for a skill the catalogue no longer has, and a reinforcement that is not this
        // variation's: both must be dropped rather than carried, or a deleted skill comes back to
        // life under a reused id.
        rows.Add(("a_skill_that_was_deleted", 99, "WHATEVER", new[] { "GONE" }));

        var back = new SkillProgress();
        back.Restore(rows);

        Assert.Equal(p.UsesOf(Hammer.Id), back.UsesOf(Hammer.Id));
        Assert.Equal(v.Name, back.VariationOf(Hammer)!.Name);
        Assert.True(back.HasReinforcement(Hammer.Id, v.Reinforcements[0].Name));
        Assert.Equal(0, back.UsesOf("a_skill_that_was_deleted"));
    }

    [Fact]
    public void test_the_composed_build_carries_what_the_player_bought()
    {
        var p = Levelled(Hammer, 2);
        var v = Hammer.Variations[0];
        p.ChooseVariation(Hammer, v.Name);
        p.TakeReinforcement(Hammer, v.Reinforcements[0].Name);

        var build = BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: new[] { new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a") },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4, progress: p);

        var woven = build.Skills.Single();
        Assert.Equal(v.Name, woven.Variation?.Name);
        Assert.Single(woven.Reinforcements);
        Assert.Equal(v.Reinforcements[0].Name, woven.Reinforcements[0].Name);
    }

    [Fact]
    public void test_an_unlevelled_build_runs_every_skill_at_its_base_line()
    {
        // Null progress is a champion that has not fought yet, and every test that does not care.
        var build = BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: new[] { new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a") },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        var woven = build.Skills.Single();
        Assert.Null(woven.Variation);
        Assert.Empty(woven.Reinforcements);
        Assert.Equal(SkillCatalogue.ActiveOf(Style.Hammer).Id, woven.Def.Id);
    }
}

/// <summary>
/// The levels are actually EARNED by playing, not merely awardable in a unit test.
/// </summary>
/// <remarks>
/// A progression system that only a test can advance is the dormant-feature failure with a
/// scoreboard attached. This runs a real expedition and requires the skills to have levelled by the
/// end of it.
/// </remarks>
public class SkillProgressLivenessTests
{
    [Fact]
    public void test_clearing_waves_levels_the_skills_that_were_equipped()
    {
        var progress = new SkillProgress();
        var build = BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: new[]
            {
                new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a"),
                new BuildComposer.SkillPick(Source.Nature, Form.Aura, null, "b"),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4, progress: progress);

        var hunter = new IdleXIdle.Core.Economy.Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new IdleXIdle.Core.Builds.SoloExpedition(
            build, champ, hunter, 110f, 9f,
            IdleXIdle.Core.Expeditions.ExpeditionTuning.Default, Source.Nature, new Random(31))
        {
            Progress = progress,
        };

        while (!run.Over && run.Wave < 8)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
        }

        var blow = SkillCatalogue.ActiveOf(Style.Hammer).Id;
        var mire = SkillCatalogue.PassiveOf(Style.Field).Id;

        Assert.True(progress.UsesOf(blow) > 0,
            "the active skill cleared waves and earned nothing — the expedition is not recording use.");
        Assert.Equal(progress.UsesOf(blow), progress.UsesOf(mire));   // a wave is the unit both kinds share
        Assert.Equal(run.Wave, progress.UsesOf(blow));                 // one per CLEARED wave, no more
    }

    [Fact]
    public void test_a_skill_that_was_not_equipped_earns_nothing()
    {
        var progress = new SkillProgress();
        var build = BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: new[] { new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a") },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4, progress: progress);

        var hunter = new IdleXIdle.Core.Economy.Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new IdleXIdle.Core.Builds.SoloExpedition(
            build, champ, hunter, 110f, 9f,
            IdleXIdle.Core.Expeditions.ExpeditionTuning.Default, Source.Nature, new Random(31))
        {
            Progress = progress,
        };
        while (!run.Over && run.Wave < 6) { var b = run.Wave; run.PushWave(); if (run.Wave == b) break; }

        Assert.Equal(0, progress.UsesOf(SkillCatalogue.ActiveOf(Style.Drain).Id));
    }
}
