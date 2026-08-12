using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// A whole run for one character. The squad had an <see cref="Expedition"/> wrapper proven end to end;
/// the solo model needs the same, because a per-wave sim that no across-waves loop ever calls is the
/// exact dormant shape this project keeps rediscovering.
/// </summary>
public class SoloExpeditionTests
{
    private static EquippedSkill Sk(Form form, Source src = Source.Nature)
        => new(new WovenAbility { Name = form.ToString(), Source = src, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    private static Build BuildOf(BuildMods passive = default, params Form[] forms)
    {
        var b = new Build { PassiveMods = passive.Equals(default(BuildMods)) ? BuildMods.None : passive };
        foreach (var f in forms) b.Weave(Sk(f));
        return b;
    }

    private static Champion Champ(int hp = 400) => new() { MaxHealth = hp, Health = hp };

    private static SoloExpedition Run(
        Build build, Champion? champ = null, float enemyHp = 120f, float enemyDmg = 9f)
        => new(build, champ ?? Champ(), new Hunter(), enemyHp, enemyDmg,
               ExpeditionTuning.Default, enemySource: null, rng: new Random(7));

    private static int DepthOf(Build build, int hp = 400)
    {
        var run = Run(build, Champ(hp));
        while (!run.Over && run.Wave < 100) run.PushWave();
        return run.Wave;
    }

    /// <summary>
    /// A region's combat bias must reach the fight — a FAST region bites more often than a HEAVY one.
    /// </summary>
    /// <remarks>
    /// REGRESSION: AttackBias was set on every region and advertised, but nothing read it — all six fought
    /// on a flat 1500ms cadence. It now bends the enemy tempo, which is what gives regions their own feel
    /// (and, because TRAP fires on being hit, their own build synergy).
    /// </remarks>
    [Fact]
    public void test_a_regions_bias_changes_the_enemy_tempo()
    {
        int Bites(AttackBias bias)
        {
            var champ = Champ(1_000_000);   // survives the whole stalling wave, so every bite is counted
            var run = new SoloExpedition(BuildOf(default, Form.Strike), champ, new Hunter(),
                1_000_000f, 30f, ExpeditionTuning.Default, enemySource: null, rng: new Random(7)) { EnemyBias = bias };
            run.PushWave();
            return run.LastWaveEvents.Count(e => e.Kind == BattleEventKind.EnemyStrike);
        }

        Assert.True(Bites(AttackBias.Fast) > Bites(AttackBias.Heavy),
            "a FAST region must bite more often than a HEAVY one — the bias reaches no formula");
    }

    /// <summary>The run resolves with no input, and identically for the same build and seed.</summary>
    [Fact]
    public void test_a_solo_run_resolves_with_no_input_and_is_deterministic()
        => Assert.Equal(
            DepthOf(BuildOf(default, Form.Strike, Form.Projectile)),
            DepthOf(BuildOf(default, Form.Strike, Form.Projectile)));

    /// <summary>
    /// Health carries between waves — the same Champion fights the whole run, so cooldowns, UNDYING and
    /// attrition all persist. A per-wave sim handed a fresh champion each time would be a different, and
    /// far easier, game.
    /// </summary>
    [Fact]
    public void test_the_same_champion_fights_every_wave_and_attrition_carries()
    {
        var champ = Champ(500);
        var run = Run(BuildOf(default, Form.Strike), champ, enemyHp: 60f, enemyDmg: 8f);

        run.PushWave();
        var afterOne = champ.Health;
        var elapsedOne = champ.ElapsedMs;
        run.PushWave();

        Assert.Same(champ, run.Champion);                         // not replaced each wave
        Assert.True(champ.ElapsedMs > elapsedOne, "the clock must carry across waves");
        Assert.True(champ.Health < afterOne, "damage taken must persist — that's attrition");
    }

    /// <summary>
    /// THE wire. A stronger build must push DEEPER. Damage flows build -> Resolve -> the sim; if the
    /// wrapper drops it, every build reaches the same wave and the whole build layer is decoration.
    /// </summary>
    [Fact]
    public void test_a_stronger_build_pushes_deeper()
    {
        var naked = DepthOf(BuildOf(BuildMods.None, Form.Strike));
        var mighty = DepthOf(BuildOf(new BuildMods(1.6f, 1f, 1f, 1f, 1f), Form.Strike));

        Assert.True(mighty > naked, $"more damage must reach the run: naked={naked} mighty={mighty}");
    }

    /// <summary>
    /// PassiveMods is the exact field the Dust tree feeds (DustEffects.TreeMods). Prove the wrapper reads
    /// it, so the tree -> build -> run chain has no dead link at THIS layer.
    /// </summary>
    [Fact]
    public void test_the_passive_mods_reach_the_run()
    {
        var naked = DepthOf(BuildOf(BuildMods.None, Form.Strike));
        var trained = DepthOf(BuildOf(new BuildMods(1.5f, 1.5f, 1f, 1f, 1f), Form.Strike));

        Assert.True(trained > naked, $"passive nodes must reach the run: naked={naked} trained={trained}");
    }

    /// <summary>
    /// A haul build-mod must lift the gleam earned — the solo stand-in for the squad's Producer. Pushed
    /// to a fixed depth so only the multiplier varies.
    /// </summary>
    [Fact]
    public void test_a_haul_mod_increases_the_gleam_earned()
    {
        int GleamAt3(BuildMods passive)
        {
            // A fat champion against a soft enemy, so both builds clear the same three waves.
            var run = Run(BuildOf(passive, Form.Strike, Form.Projectile), Champ(4000), enemyHp: 60f, enemyDmg: 1f);
            for (var i = 0; i < 3 && !run.Over; i++) run.PushWave();
            return run.Carried.Gleam;
        }

        var plain = GleamAt3(BuildMods.None);
        var rich = GleamAt3(new BuildMods(1f, 1f, 1f, 2f, 1f));   // Haul x2

        Assert.True(rich > plain, $"a haul mod must reach the payout: plain={plain} rich={rich}");
    }

    /// <summary>
    /// DESPERATION swells the haul while the champion is near death — "a richer haul at low health".
    /// </summary>
    /// <remarks>
    /// It lives HERE, not in SoloBattleTests, because it is a HAUL effect read in
    /// <see cref="SoloExpedition"/>'s payout, not the fight — which is exactly why TriggerLivenessTests
    /// parked it for so long as "belongs to the squad Expedition". The solo model routes it through
    /// <see cref="Build.Triggers"/> now, so the same build at two health levels must pay differently:
    /// only the health gate changes, so any gap is the trigger.
    /// </remarks>
    [Fact]
    public void test_desperation_swells_the_haul_at_low_health()
    {
        int WaveGleamAt(int startHealth)
        {
            var build = BuildOf(default, Form.Strike, Form.Projectile);
            build.ExtraTriggers = new HashSet<BuildTrigger> { BuildTrigger.Desperation };
            var champ = Champ(4000);
            champ.Health = startHealth;
            // A soft enemy so the wave clears and health barely moves — the gate reads the level we set.
            var run = Run(build, champ, enemyHp: 60f, enemyDmg: 1f);
            run.PushWave();
            return run.LastWaveHaul.Gleam;
        }

        var whole = WaveGleamAt(4000);       // full — above MaxHealth/3, no kick
        var desperate = WaveGleamAt(1000);   // 25% — at or below the third, the kick applies

        Assert.True(desperate > whole,
            $"DESPERATION paid no more near death: whole={whole} desperate={desperate}");
    }

    /// <summary>
    /// The idle loop pays PER WAVE, the instant it clears — no banking. Each cleared wave must expose its
    /// own haul, and the running total must grow by exactly that. This is what lets the host credit as it
    /// goes instead of at a run's end that no longer exists.
    /// </summary>
    [Fact]
    public void test_each_cleared_wave_pays_out_immediately()
    {
        var run = Run(BuildOf(default, Form.Strike, Form.Projectile), Champ(4000), enemyHp: 60f, enemyDmg: 1f);

        var before = run.Carried;
        var outcome = run.PushWave();

        Assert.Equal(WaveOutcome.Cleared, outcome);
        Assert.True(run.LastWaveHaul.Gleam > 0, "a cleared wave must pay something on the spot");
        Assert.Equal(before.Gleam + run.LastWaveHaul.Gleam, run.Carried.Gleam);
    }

    /// <summary>A push that does NOT clear pays nothing — LastWaveHaul resets, so no phantom credit.</summary>
    [Fact]
    public void test_a_failed_push_pays_nothing()
    {
        var run = Run(BuildOf(default, Form.Strike), Champ(40), enemyHp: 400f, enemyDmg: 60f);
        while (!run.Over && run.Wave < 100) run.PushWave();

        Assert.True(run.Over);
        Assert.Equal(0, run.LastWaveHaul.Gleam);   // the final, failed push credited nothing
    }

    /// <summary>Haul compounds with depth — holding longer is the risk, so it has to be the reward.</summary>
    [Fact]
    public void test_a_deeper_run_carries_more()
    {
        var run = Run(BuildOf(default, Form.Strike, Form.Projectile), Champ(4000), enemyHp: 60f, enemyDmg: 1f);
        run.PushWave();
        var afterOne = run.Carried.Gleam;
        run.PushWave();
        run.PushWave();

        Assert.True(run.Carried.Gleam > afterOne, "deeper must carry more");
    }

    /// <summary>Once the run is over, pushing again does nothing — no negative waves, no resurrection.</summary>
    [Fact]
    public void test_pushing_after_the_run_ends_is_a_no_op()
    {
        var run = Run(BuildOf(default, Form.Strike), Champ(50), enemyHp: 400f, enemyDmg: 60f);
        while (!run.Over && run.Wave < 100) run.PushWave();

        var wave = run.Wave;
        Assert.Equal(WaveOutcome.Wiped, run.PushWave());
        Assert.Equal(wave, run.Wave);
    }

    /// <summary>
    /// A boss wave multiplies enemy HEALTH and must not touch enemy DAMAGE.
    /// </summary>
    /// <remarks>
    /// REGRESSION. <see cref="WaveScaling.EnemyScale"/> folds in the boss health spike, and
    /// <see cref="SoloExpedition.PushWave"/> passed it to the damage parameter as well — so every boss
    /// also hit 2.2x harder than the wave before it. Nothing in the design ever asked for that, and no
    /// tuning knob could have removed it without also removing the health spike it is named for.
    ///
    /// This asserts the contract directly rather than measuring run outcomes. A statistical version of
    /// this test (sweeping health and counting how many depths land on a multiple of five) passed with
    /// the bug present and is therefore worthless — the bug costs a couple of waves of depth, but it
    /// does not visibly quantise where runs end.
    /// </remarks>
    [Fact]
    public void test_a_boss_wave_spikes_health_but_not_damage()
    {
        var t = ExpeditionTuning.Default;
        var boss = 5;                       // t.BossEvery
        Assert.True(WaveScaling.IsBossWave(boss, t));
        Assert.False(WaveScaling.IsBossWave(boss - 1, t));

        var healthStep = WaveScaling.EnemyScale(boss, t) / WaveScaling.EnemyScale(boss - 1, t);
        var damageStep = WaveScaling.EnemyDamageScale(boss, t) / WaveScaling.EnemyDamageScale(boss - 1, t);

        Assert.Equal(t.EnemyScaleBase * t.BossHealthScale, healthStep, 3);
        Assert.Equal(t.EnemyScaleBase, damageStep, 3);
    }

    /// <summary>The two scales agree on every non-boss wave — the split must not introduce a drift.</summary>
    [Fact]
    public void test_health_and_damage_scales_agree_off_boss_waves()
    {
        var t = ExpeditionTuning.Default;
        for (var w = 1; w <= 24; w++)
        {
            if (WaveScaling.IsBossWave(w, t)) continue;
            Assert.Equal(WaveScaling.EnemyScale(w, t), WaveScaling.EnemyDamageScale(w, t), 3);
        }
    }

    // ── THE ARCHETYPE ENGINE, END TO END. ───────────────────────────────────────────────────────────

    private static SoloExpedition RunIn(string region, Build build, int hp = 600)
    {
        var run = new SoloExpedition(build, Champ(hp), new Hunter(), 120f, 9f,
            ExpeditionTuning.Default, enemySource: null, rng: new Random(7))
        { RegionId = region, RunIndex = 1 };
        return run;
    }

    private static int DepthIn(string region, Build build, int hp = 600)
    {
        var run = RunIn(region, build, hp);
        while (!run.Over && run.Wave < 120) run.PushWave();
        return run.Wave;
    }

    /// <summary>
    /// A region must reward the shape it is built around and punish the other one.
    /// </summary>
    /// <remarks>
    /// THE ACCEPTANCE TEST FOR THE WHOLE REDESIGN. Combat is automatic, so the only way a build can be
    /// WRONG rather than merely small is if content punishes shapes. Cinderworks is the armoured region
    /// and Umbral Reach is the swarm region; a large-hit build must do relatively better in the first
    /// and a multi-target build relatively better in the second.
    ///
    /// It compares RATIOS, not absolute depths, because the two regions are not equally hard and never
    /// will be — what matters is that changing the build changes which region suits you.
    ///
    /// If this fails, the archetype system is decorative: the bands roll, the creatures spawn, and none
    /// of it reaches the one number the player is scored on.
    /// </remarks>
    [Fact]
    public void test_a_region_rewards_the_build_shape_it_is_built_around()
    {
        var weight = BuildOf(BuildMods.None, Form.Trap, Form.Strike);       // few, enormous hits
        var spread = BuildOf(BuildMods.None, Form.Aura, Form.Projectile);   // many, small hits

        var weightArmoured = DepthIn("cinderworks", weight);
        var weightSwarm = DepthIn("umbral_reach", weight);
        var spreadArmoured = DepthIn("cinderworks", spread);
        var spreadSwarm = DepthIn("umbral_reach", spread);

        var weightPrefersArmour = weightArmoured / (float)Math.Max(1, weightSwarm);
        var spreadPrefersArmour = spreadArmoured / (float)Math.Max(1, spreadSwarm);

        Assert.True(
            weightPrefersArmour > spreadPrefersArmour,
            $"Weight build: {weightArmoured} in Cinderworks vs {weightSwarm} in Umbral Reach " +
            $"(ratio {weightPrefersArmour:F2}). Spread build: {spreadArmoured} vs {spreadSwarm} " +
            $"(ratio {spreadPrefersArmour:F2}). The regions do not distinguish build shape — the " +
            "archetype engine is not reaching depth.");
    }

    /// <summary>The band cycle must actually change what a wave holds.</summary>
    [Fact]
    public void test_bands_change_the_composition_as_depth_grows()
    {
        var run = RunIn("umbral_reach", BuildOf(BuildMods.None, Form.Aura, Form.Projectile), hp: 500_000);

        var seen = new HashSet<Archetype>();
        while (!run.Over && run.Wave < 45)
        {
            run.PushWave();
            seen.Add(run.LastWaveArchetype);
        }

        Assert.True(seen.Count >= 3,
            $"Across 45 waves only {seen.Count} archetype(s) appeared ({string.Join(", ", seen)}). " +
            "A region that asks one question has no lesson in it.");
    }

    /// <summary>Two descents of the same region produce the same waves.</summary>
    /// <remarks>
    /// Fast-forward pays the haul a wave originally paid. If a composition re-rolled, a player could
    /// bank a wave they never actually proved.
    /// </remarks>
    [Fact]
    public void test_the_same_run_index_replays_the_same_waves()
    {
        List<(Archetype, int)> Walk()
        {
            var run = RunIn("marrow_wastes", BuildOf(BuildMods.None, Form.Strike), hp: 500_000);
            var seen = new List<(Archetype, int)>();
            for (var i = 0; i < 12 && !run.Over; i++)
            {
                run.PushWave();
                seen.Add((run.LastWaveArchetype, run.LastWaveCreatures.Count));
            }
            return seen;
        }

        Assert.Equal(Walk(), Walk());
    }

    /// <summary>A boss is always a single creature, whatever shape its band supplies.</summary>
    [Fact]
    public void test_a_boss_wave_holds_exactly_one_creature()
    {
        var run = RunIn("verdant_hollow", BuildOf(BuildMods.None, Form.Strike), hp: 500_000);
        while (!run.Over && run.Wave < 20)
        {
            run.PushWave();
            if (run.LastWaveWasBoss)
                Assert.Single(run.LastWaveCreatures);
        }
    }

}
