using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A whole run for one character. The squad had an <see cref="Expedition"/> wrapper proven end to end;
/// the solo model needs the same, because a per-wave sim that no across-waves loop ever calls is the
/// exact dormant shape this project keeps rediscovering.
/// </summary>
public class SoloExpeditionTests
{
    private static EquippedSkill Sk(string id, Source src = Source.Nature)
        => TestBuilds.Skill(id, src);

    private static Build BuildOf(BuildMods passive = default, params string[] skillIds)
    {
        var b = new Build { PassiveMods = passive.Equals(default(BuildMods)) ? BuildMods.None : passive };
        foreach (var id in skillIds) b.Equip(Sk(id));
        return b;
    }

    private static Champion Champ(int hp = 400) => new() { MaxHealth = hp, Health = hp };

    private static SoloExpedition Run(
        Build build, Champion? champ = null, float enemyHp = 120f, float enemyDmg = 9f)
        => new(build, champ ?? Champ(), new Hunter(), enemyHp, enemyDmg,
               ExpeditionTuning.Default, rng: new Random(7));

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
    /// (and, because a Snare Reaction fires on being hit, their own build synergy).
    /// </remarks>
    [Fact]
    public void test_a_regions_bias_changes_the_enemy_tempo()
    {
        int Bites(AttackBias bias)
        {
            var champ = Champ(1_000_000);   // survives the whole stalling wave, so every bite is counted
            var run = new SoloExpedition(BuildOf(default, "hammer_blow"), champ, new Hunter(),
                1_000_000f, 30f, ExpeditionTuning.Default, rng: new Random(7)) { EnemyBias = bias };
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
            DepthOf(BuildOf(default, "hammer_blow", "volley_spray")),
            DepthOf(BuildOf(default, "hammer_blow", "volley_spray")));

    /// <summary>
    /// Health carries between waves — the same Champion fights the whole run, so cooldowns, UNDYING and
    /// attrition all persist. A per-wave sim handed a fresh champion each time would be a different, and
    /// far easier, game.
    /// </summary>
    [Fact]
    public void test_the_same_champion_fights_every_wave_and_attrition_carries()
    {
        var champ = Champ(500);
        var run = Run(BuildOf(default, "hammer_blow"), champ, enemyHp: 60f, enemyDmg: 8f);

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
        // 2.5x, was 1.6x — depth moves in whole waves and the beat's longer waits widened a wave's step;
        // the claim is that damage REACHES the run, not what a particular multiplier buys.
        var naked = DepthOf(BuildOf(BuildMods.None, "hammer_blow"));
        var mighty = DepthOf(BuildOf(new BuildMods(2.5f, 1f, 1f, 1f, 1f), "hammer_blow"));

        Assert.True(mighty > naked, $"more damage must reach the run: naked={naked} mighty={mighty}");
    }

    /// <summary>
    /// PassiveMods is the exact field the Dust tree feeds (DustEffects.TreeMods). Prove the wrapper reads
    /// it, so the tree -> build -> run chain has no dead link at THIS layer.
    /// </summary>
    [Fact]
    public void test_the_passive_mods_reach_the_run()
    {
        // 2.5x, was 1.5x: depth moves in whole waves, and with the beat's longer waits a one-and-a-half
        // multiplier no longer crosses a wave boundary on this fixture. The claim is that the passive
        // mods REACH the run at all, not how much a particular multiplier is worth.
        var naked = DepthOf(BuildOf(BuildMods.None, "hammer_blow"));
        var trained = DepthOf(BuildOf(new BuildMods(2.5f, 2.5f, 1f, 1f, 1f), "hammer_blow"));

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
            var run = Run(BuildOf(passive, "hammer_blow", "volley_spray"), Champ(4000), enemyHp: 60f, enemyDmg: 1f);
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
            var build = BuildOf(default, "hammer_blow", "volley_spray");
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
        var run = Run(BuildOf(default, "hammer_blow", "volley_spray"), Champ(4000), enemyHp: 60f, enemyDmg: 1f);

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
        var run = Run(BuildOf(default, "hammer_blow"), Champ(40), enemyHp: 400f, enemyDmg: 60f);
        while (!run.Over && run.Wave < 100) run.PushWave();

        Assert.True(run.Over);
        Assert.Equal(0, run.LastWaveHaul.Gleam);   // the final, failed push credited nothing
    }

    /// <summary>Haul compounds with depth — holding longer is the risk, so it has to be the reward.</summary>
    [Fact]
    public void test_a_deeper_run_carries_more()
    {
        var run = Run(BuildOf(default, "hammer_blow", "volley_spray"), Champ(4000), enemyHp: 60f, enemyDmg: 1f);
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
        var run = Run(BuildOf(default, "hammer_blow"), Champ(50), enemyHp: 400f, enemyDmg: 60f);
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
        Assert.Equal(t.EnemyDamageScaleBase, damageStep, 3);
    }

    /// <summary>Enemy damage compounds more slowly than enemy health, and always has to.</summary>
    /// <remarks>
    /// This test used to assert the opposite — that the two scales AGREE off boss waves — because they
    /// did, and the assertion was guarding the boss-damage bug above rather than stating a design rule.
    /// Making them agree turned out to be the reason the mastery tree was a ranking instead of four
    /// choices: when damage keeps pace with health, no build ever stalls, so nothing ends a run except
    /// the champion dying, so depth measures survival alone and the survival branch wins every region.
    /// See <c>ExpeditionTuning.EnemyDamageScaleBase</c> for the measurements. The gap is the mechanism,
    /// so the gap is what gets asserted.
    /// </remarks>
    [Fact]
    public void test_enemy_damage_grows_more_slowly_than_enemy_health()
    {
        var t = ExpeditionTuning.Default;
        Assert.True(t.EnemyDamageScaleBase < t.EnemyScaleBase,
                    "damage must compound below health or every build is survival-bound");

        for (var w = 2; w <= 24; w++)
        {
            if (WaveScaling.IsBossWave(w, t)) continue;
            Assert.True(WaveScaling.EnemyDamageScale(w, t) < WaveScaling.EnemyScale(w, t),
                        $"wave {w}: damage scale caught up with health scale");
        }

        // And the gap widens, rather than being a constant offset applied once at wave one.
        var early = WaveScaling.EnemyScale(4, t) - WaveScaling.EnemyDamageScale(4, t);
        var late = WaveScaling.EnemyScale(24, t) - WaveScaling.EnemyDamageScale(24, t);
        Assert.True(late > early, "the health/damage gap must grow with depth, not sit still");
    }

    // ── THE ARCHETYPE ENGINE, END TO END. ───────────────────────────────────────────────────────────

    private static SoloExpedition RunIn(string region, Build build, int hp = 600)
    {
        var run = new SoloExpedition(build, Champ(hp), new Hunter(), 120f, 9f,
            ExpeditionTuning.Default, rng: new Random(7))
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
        var weight = BuildOf(BuildMods.None, "snare_jaws", "hammer_blow");     // few, enormous hits
        var spread = BuildOf(BuildMods.None, "field_mire", "volley_spray");    // many, small hits

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
        var run = RunIn("umbral_reach", BuildOf(BuildMods.None, "field_mire", "volley_spray"), hp: 500_000);

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
            var run = RunIn("marrow_wastes", BuildOf(BuildMods.None, "hammer_blow"), hp: 500_000);
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
        var run = RunIn("verdant_hollow", BuildOf(BuildMods.None, "hammer_blow"), hp: 500_000);
        while (!run.Over && run.Wave < 20)
        {
            run.PushWave();
            if (run.LastWaveWasBoss)
                Assert.Single(run.LastWaveCreatures);
        }
    }

}
