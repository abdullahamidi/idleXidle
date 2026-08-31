using System;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The fight uses the build you have NOW. Playtest 2026-08-23: skills changed on the build screen reached
/// the hunt only after a death — the run composed its build once at StartRun. <see cref="SoloExpedition.ReplaceBuild"/>
/// swaps the build between waves and keeps everything else about the run.
/// </summary>
public class LiveBuildTests
{
    private static EquippedSkill Sk(Form form)
        => new(new WovenAbility { Name = form.ToString(), Source = Source.Nature, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    private static Build Bare() => new() { PassiveMods = BuildMods.None };

    private static Build Armed()
    {
        var b = new Build { PassiveMods = BuildMods.None };
        foreach (var f in new[] { Form.Strike, Form.Projectile, Form.Aura, Form.Trap }) b.Weave(Sk(f));
        return b;
    }

    private static SoloExpedition Run(Build build)
        => new(build, new Champion { MaxHealth = 400, Health = 400 }, new Hunter(), 120f, 9f,
               ExpeditionTuning.Default, enemySource: null, rng: new Random(7));

    [Fact]
    public void test_live_build_replace_keeps_the_wave_and_the_haul()
    {
        // Arrange — one wave fought with nothing woven.
        var run = Run(Bare());
        run.PushWave();
        var wave = run.Wave;
        var carried = run.Carried;

        // Act
        run.ReplaceBuild(Armed());

        // Assert — the run is the same run; only the next waves' build changed.
        Assert.Equal(wave, run.Wave);
        Assert.Equal(carried, run.Carried);
    }

    [Fact]
    public void test_live_build_replace_changes_how_the_next_waves_go()
    {
        // Arrange — two runs, same seed: one stays bare, one arms itself after wave 1.
        int Depth(bool armAfterFirst)
        {
            var run = Run(Bare());
            run.PushWave();
            if (armAfterFirst) run.ReplaceBuild(Armed());
            while (!run.Over && run.Wave < 100) run.PushWave();
            return run.Wave;
        }

        // Act
        var bare = Depth(false);
        var armed = Depth(true);

        // Assert — four woven skills reach the fight from wave 2 on.
        Assert.True(armed > bare, $"armed run reached wave {armed}, bare run {bare}");
    }

    [Fact]
    public void test_live_build_replace_refuses_nothing()
    {
        var run = Run(Bare());
        Assert.Throws<ArgumentNullException>(() => run.ReplaceBuild(null!));
    }
    [Fact]
    public void test_live_build_replace_forgets_only_the_swapped_slots_cooldown()
    {
        // Arrange — Strike in slot 0, Projectile in slot 1 (both ACTIVE forms — passives and
        // on-hit forms never get a ready-at); fight until both slots carry one.
        Build Two(Form first)
        {
            var b = new Build { PassiveMods = BuildMods.None };
            b.Weave(Sk(first)); b.Weave(Sk(Form.Projectile));
            return b;
        }
        var champ = new Champion { MaxHealth = 400, Health = 400 };
        var run = new SoloExpedition(Two(Form.Strike), champ, new Hunter(), 120f, 9f,
            ExpeditionTuning.Default, enemySource: null, rng: new Random(7));
        // One cast at a time — a short wave may end before the second slot ever fires, so fight until both have.
        // Strike and Projectile count their cooldowns in BEATS (2026-08-27), so the carried table is ReadyAtBeat.
        for (var w = 0; w < 20 && !(champ.ReadyAtBeat.ContainsKey(0) && champ.ReadyAtBeat.ContainsKey(1)) && !run.Over; w++) run.PushWave();
        Assert.True(champ.ReadyAtBeat.ContainsKey(0) && champ.ReadyAtBeat.ContainsKey(1), "both slots should carry a cooldown once both have cast");
        var secondReady = champ.ReadyAtBeat[1];

        // Act — slot 0 becomes a Mark, slot 1 stays a Projectile.
        run.ReplaceBuild(Two(Form.Mark));

        // Assert — the swapped slot starts fresh; the unchanged slot keeps its carried cooldown.
        Assert.False(champ.ReadyAt.ContainsKey(0) || champ.ReadyAtBeat.ContainsKey(0), "the swapped-in skill must not inherit the old slot's cooldown");
        Assert.Equal(secondReady, champ.ReadyAtBeat[1]);
    }
}
