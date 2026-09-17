using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// THE FIRST BOSS A CAREER MEETS MUST FALL TO IT — and the run must still end somewhere afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this file exists to stop coming back.</b> Fresh-save validation on 2026-09-12 found the
/// opening was a guaranteed loss: the champion reached THORN REGENT on 72 of 180 health and died there
/// in 500 of 500 runs. Not "usually" — the SAME 500, because a wave's composition is seeded from
/// <c>Bands.Seed(region, wave, RunIndex)</c> and every career's first descent is RunIndex 1, so the
/// first five waves are one authored fight that every player gets. The boss drops the chest the
/// opening's next four beats are about, so the sequencing the game teaches was
/// <i>boss → guaranteed death</i>, delivered before the player had been handed anything to change.
/// </para>
/// <para>
/// <b>Why the fixture is built this way.</b> Every other descent probe in this suite drives
/// <c>TestBuilds.Of("hammer_blow")</c> or the four-skill quartet — builds a fresh save provably cannot
/// weave (<c>starter_loadout_test</c> pins that hammer_blow is not in a new tree). This one takes the
/// one path the real game takes on a new file: <c>Game1.StartNewGame</c>'s own
/// <c>PlayerLoadout.Starter</c> at capacity 1, an empty tree, a rank-zero Hunter, and the enemy
/// baseline <c>Game1.EnemyBaselineFor("verdant_hollow")</c> actually produces — 121 health (110 × the
/// region's own +10 % modifier) and 9 damage. A softer fixture would have reported this fight as fine.
/// </para>
/// <para>
/// <b>And what the 500 seeds actually vary.</b> Not the fight: <c>BalanceSweepTests</c> measured forty
/// <c>Random</c> seeds producing forty identical depths, because the composition comes from the wave
/// seed and not the injected stream. The seeds here vary the WITHIN-wave resolution, which is the only
/// thing a first descent can vary at all — so a green sweep says "no roll inside the wave flips this",
/// and the determinism of the composition is itself asserted below rather than assumed.
/// </para>
/// </remarks>
public class FirstBossTests
{
    /// <summary>How many runs the sweep drives. The brief's number, and it costs about a second.</summary>
    private const int Seeds = 500;

    /// <summary>The enemy baseline Game1.EnemyBaselineFor produces for a fresh save in the first region.</summary>
    /// <remarks>
    /// 110 × (1 + 0.35 × 0 progression) × RegionLadder.Health(0) × corruption 1 × the region modifier's
    /// 1.10, and 9 × 1 × 1 × 1 × 1. Derived below rather than written, so a change to any of the four
    /// terms reaches this fixture instead of leaving it measuring a game nobody plays.
    /// </remarks>
    private static (float Health, float Damage) FreshBaseline()
    {
        var mod = RegionModifiers.For(VerdantHollow.RegionId);
        var scale = CorruptionScaling.HealthMultiplier(0);
        return (110f * RegionLadder.Health(0) * scale * mod.EnemyHealthMult,
                9f * RegionLadder.Damage(0) * mod.EnemyDamageMult);
    }

    /// <summary>Exactly what a new file fights with — see the remarks on this class.</summary>
    private static (Build Build, Hunter Hunter) FreshSave()
    {
        var characters = new CharacterState();
        var loadout = PlayerLoadout.Starter(characters.Active);
        loadout.SkillCapacity = 1;   // Unlocks.SkillSlots on a save with no mastery and no depth
        return (loadout.ToBuild(new MasteryTree(), characters.Active, new SkillProgress(),
                                Array.Empty<Keystone>(), Array.Empty<Vow>()),
                new Hunter());
    }

    /// <summary>One fresh career's first descent, driven to the wave it falls on.</summary>
    /// <param name="BossWave">Whether the tutorial boss was felled.</param>
    /// <param name="HealthAfterBoss">The pool the champion carried off the boss, or 0 if it never did.</param>
    /// <param name="Fell">The wave the champion first fell on, or 0 if it survived the whole walk.</param>
    /// <param name="MaxHealth">The champion's pool — the denominator every margin is read against.</param>
    /// <param name="BossMs">
    /// How long the boss fight simulated, in ms — its last event's timestamp. The encounter's DURATION,
    /// which is the other half of "the boss must not become visually trivial": a boss that dies in two
    /// beats is not frightening however much health it took.
    /// </param>
    /// <param name="BossBeats">The champion's own actions in the boss fight — the beat is 1500 ms at action speed 1.</param>
    private readonly record struct FirstDescent(
        bool BossWave, int HealthAfterBoss, int Fell, int MaxHealth, int BossMs, int BossBeats);

    private static FirstDescent Walk(int seed, ExpeditionTuning tuning, int toWave = 60)
    {
        var (build, hunter) = FreshSave();
        var (bh, bd) = FreshBaseline();
        var descent = new Descent
        {
            RegionId = VerdantHollow.RegionId,
            EnemyBias = Regions.Get(VerdantHollow.RegionId).CombatBias,
            Tuning = tuning,
            Rng = new Random(seed),
        };
        descent.StartRun(build, hunter, bh, bd);   // RunIndex 1: a career's first descent
        var boss = OpeningScript.TutorialBossWave;
        var felledBoss = false;
        var afterBoss = 0;
        var bossMs = 0;
        var bossBeats = 0;
        for (var w = 1; w <= toWave; w++)
        {
            var outcome = descent.PushWave();
            if (w == boss)
            {
                // The encounter's shape, whether it was won or lost: how long it simulated and how many
                // actions the champion took inside it.
                var events = descent.Run!.LastWaveEvents;
                bossMs = events.Count > 0 ? events.Max(e => e.AtMs) : 0;
                bossBeats = events.Count(e => e.Kind is BattleEventKind.Strike or BattleEventKind.Skill);
                if (outcome == WaveOutcome.Cleared) { felledBoss = true; afterBoss = descent.Champion!.Health; }
            }
            if (outcome != WaveOutcome.Cleared)
                return new FirstDescent(felledBoss, afterBoss, w, descent.Champion!.MaxHealth, bossMs, bossBeats);
        }
        return new FirstDescent(felledBoss, afterBoss, 0, descent.Champion!.MaxHealth, bossMs, bossBeats);
    }

    private readonly ITestOutputHelper _out;

    public FirstBossTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// THE GUARANTEE: a fresh save fells its first boss, every time, with health to spare.
    /// </summary>
    /// <remarks>
    /// The lower bound is the one the brief asked for in words — "not 1 HP every time" — as a number:
    /// a fifth of the pool, which the measured minimum (55 of 180, 31 %) clears comfortably. The UPPER
    /// bound is the other half of the same requirement and matters just as much: a champion that walks
    /// off its first boss with most of its pool has not met anything frightening, and the opening's job
    /// is to teach that bosses are dangerous.
    /// </remarks>
    [Fact]
    public void test_a_fresh_save_fells_its_first_boss_in_every_one_of_five_hundred_runs()
    {
        var tuning = ExpeditionTuning.UntilTheFirstBossFalls;
        var runs = Enumerable.Range(0, Seeds).Select(s => Walk(s, tuning, toWave: OpeningScript.TutorialBossWave)).ToList();
        var pool = runs[0].MaxHealth;
        var left = runs.Select(r => r.HealthAfterBoss).OrderBy(h => h).ToList();
        var defeats = runs.Count(r => !r.BossWave);

        _out.WriteLine($"fresh first-boss seeds tested: {Seeds}");
        _out.WriteLine($"victories: {runs.Count - defeats}");
        _out.WriteLine($"defeats:   {defeats}");
        if (defeats == 0)
        {
            _out.WriteLine($"champion pool:      {pool}");
            _out.WriteLine($"min remaining HP:    {left.First()}  ({left.First() * 100f / pool:F0} % of the pool)");
            _out.WriteLine($"median remaining HP: {left[left.Count / 2]}  ({left[left.Count / 2] * 100f / pool:F0} %)");
            _out.WriteLine($"max remaining HP:    {left.Last()}  ({left.Last() * 100f / pool:F0} %)");
            var ms = runs.Select(r => r.BossMs).OrderBy(m => m).ToList();
            var beats = runs.Select(r => r.BossBeats).OrderBy(b => b).ToList();
            _out.WriteLine($"boss encounter duration: min {ms.First() / 1000f:F1} s, median {ms[ms.Count / 2] / 1000f:F1} s, max {ms.Last() / 1000f:F1} s");
            _out.WriteLine($"champion actions in it:  min {beats.First()}, median {beats[beats.Count / 2]}, max {beats.Last()}");
        }

        // AND IT IS STILL A FIGHT. A boss that falls in two actions is not frightening however much
        // health it took — the duration is the other half of "never visually trivial".
        Assert.All(runs, r => Assert.True(r.BossBeats >= 4,
                                          $"the boss fell in {r.BossBeats} champion actions"));

        Assert.Equal(0, defeats);
        Assert.True(left.First() >= pool / 5,
                    $"the thinnest win leaves {left.First()} of {pool} — the first boss finishes the player at a sliver");
        Assert.True(left.Last() <= pool * 3 / 5,
                    $"the fattest win leaves {left.Last()} of {pool} — the first boss is not frightening enough to be a boss");
    }

    /// <summary>
    /// THE SHIPPED DEFECT, kept as a measurement: with no taught waves the same fresh save loses the
    /// same fight in all five hundred runs.
    /// </summary>
    /// <remarks>
    /// This is the test that would have caught it, written after the fact. It also proves the repair is
    /// the taught-waves window and not something else that changed on the same day: flip one knob back
    /// to its default and the certain death returns, in full, on the same fixture.
    /// </remarks>
    [Fact]
    public void test_without_the_taught_waves_the_same_fresh_save_loses_the_same_fight_every_time()
    {
        var runs = Enumerable.Range(0, Seeds).Select(s => Walk(s, ExpeditionTuning.Default,
                                                              toWave: OpeningScript.TutorialBossWave)).ToList();
        Assert.All(runs, r => Assert.False(r.BossWave));
        Assert.All(runs, r => Assert.Equal(OpeningScript.TutorialBossWave, r.Fell));
        _out.WriteLine($"at ExpeditionTuning.Default the fresh save falls on wave {OpeningScript.TutorialBossWave} "
                       + $"in {runs.Count} of {runs.Count} runs — the 2026-09-12 validation finding, pinned.");
    }

    /// <summary>
    /// THE FIRST FAILURE STILL HAPPENS, and it happens AFTER the boss. The whole climax of the FTUE —
    /// READ THE LOG, MAKE ONE CHANGE — is gated on <c>LessonFacts.Falls >= 1</c>, so a fix that made
    /// the champion immortal would delete the game's most important lesson to save its fourth-most.
    /// </summary>
    /// <remarks>
    /// Bounded on BOTH sides on purpose. It must fall LATER than the tutorial boss (or the sequencing
    /// fault is merely moved) and it must fall SOON — within a couple of bands — or the first session
    /// never meets the loop at all. Measured with the taught waves live: the median first fall is
    /// wave 8, and nothing in 500 runs walked past wave 12.
    /// </remarks>
    [Fact]
    public void test_the_first_natural_failure_still_arrives_and_arrives_after_the_boss()
    {
        var tuning = ExpeditionTuning.UntilTheFirstBossFalls;
        var falls = Enumerable.Range(0, Seeds).Select(s => Walk(s, tuning).Fell).ToList();
        var sorted = falls.OrderBy(w => w).ToList();
        _out.WriteLine($"first fall: min {sorted.First()}, median {sorted[sorted.Count / 2]}, max {sorted.Last()}");
        _out.WriteLine("distribution: " + string.Join(" ", sorted.GroupBy(w => w).OrderBy(g => g.Key)
                                                            .Select(g => $"w{g.Key}:{g.Count()}")));

        Assert.DoesNotContain(0, falls);   // 0 means it walked 60 waves without dying
        Assert.All(falls, w => Assert.True(w > OpeningScript.TutorialBossWave,
                                           $"the champion still falls on wave {w} — at or before the tutorial boss"));
        Assert.True(sorted.Last() <= 20,
                    $"the deepest first fall is wave {sorted.Last()} — the first session stops pushing back");
    }

    /// <summary>
    /// THE WINDOW CLOSES AT THE TUTORIAL BOSS, by construction: wave six of the protected descent is
    /// already at full strength, and so is the SECOND boss.
    /// </summary>
    /// <remarks>
    /// This is what makes the repair narrow rather than a nerf. The taught-waves tuning rides the whole
    /// descent (the expedition captures it at construction) but its effect is a WAVE window, so nothing
    /// has to be revoked mid-run and no later boss is touched. THORN REGENT at wave 10, 15 and 20 bites
    /// exactly what it always did.
    /// </remarks>
    [Fact]
    public void test_the_taught_waves_end_at_the_tutorial_boss_and_never_reach_the_second_one()
    {
        var taught = ExpeditionTuning.UntilTheFirstBossFalls;
        var boss = OpeningScript.TutorialBossWave;

        Assert.Equal(boss, taught.TutorialWaves);
        Assert.True(taught.TutorialBiteScale < 1f);

        // Inside the window the bite is softened; from the very next wave it is not.
        for (var w = 1; w <= boss; w++)
            Assert.Equal(taught.TutorialBiteScale, WaveScaling.TutorialBite(w, taught), 4);
        foreach (var w in new[] { boss + 1, boss * 2, boss * 3, boss * 4, 100 })
            Assert.Equal(1f, WaveScaling.TutorialBite(w, taught), 4);

        // The SECOND boss and every later one is the shipped fight, at both tunings alike.
        foreach (var w in new[] { boss * 2, boss * 3, boss * 4 })
        {
            Assert.True(WaveScaling.IsBossWave(w, taught));
            Assert.Equal(WaveScaling.EnemyDamageScale(w, ExpeditionTuning.Default),
                         WaveScaling.EnemyDamageScale(w, taught), 4);
            Assert.Equal(WaveScaling.EnemyScale(w, ExpeditionTuning.Default),
                         WaveScaling.EnemyScale(w, taught), 4);
        }

        // And the boss's own health spike is untouched everywhere — it is the BITE that is softened,
        // so the fight keeps its length, its exchanges and its size.
        Assert.Equal(ExpeditionTuning.Default.BossHealthScale, taught.BossHealthScale);
        Assert.Equal(ExpeditionTuning.Default.EnemyScaleBase, taught.EnemyScaleBase);
        Assert.Equal(WaveScaling.EnemyScale(boss, ExpeditionTuning.Default),
                     WaveScaling.EnemyScale(boss, taught), 4);
    }

    /// <summary>
    /// AN UNTOUCHED TUNING IS THE GAME THAT SHIPPED. Both knobs default to off, and off is the identity.
    /// </summary>
    /// <remarks>
    /// The guard against the quietest possible version of this change: a knob that defaults to on would
    /// have re-tuned every balance probe, every benchmark and the whole deep game at once.
    /// </remarks>
    [Fact]
    public void test_the_default_tuning_is_untouched_by_the_taught_waves()
    {
        Assert.Equal(0, ExpeditionTuning.Default.TutorialWaves);
        Assert.Equal(1f, ExpeditionTuning.Default.TutorialBiteScale);
        foreach (var w in new[] { 0, 1, 2, 5, 10, 50, 500 })
        {
            Assert.Equal(1f, WaveScaling.TutorialBite(w, ExpeditionTuning.Default), 6);
            Assert.Equal(MathF.Pow(ExpeditionTuning.Default.EnemyDamageScaleBase, w),
                         WaveScaling.EnemyDamageScale(w, ExpeditionTuning.Default), 6);
        }
        // A window with no relief, and relief with no window, are both no-ops.
        Assert.Equal(1f, WaveScaling.TutorialBite(3, ExpeditionTuning.Default with { TutorialWaves = 5 }), 6);
        Assert.Equal(1f, WaveScaling.TutorialBite(3, ExpeditionTuning.Default with { TutorialBiteScale = 0.1f }), 6);
    }

    /// <summary>
    /// THE FIRST DESCENT IS ONE AUTHORED FIGHT, not a roll — which is why it could be certainly lost,
    /// and why a 500-seed sweep over the injected stream is the honest shape for this measurement.
    /// </summary>
    /// <remarks>
    /// Pinned because the whole diagnosis rests on it. A composition seeded from (region, wave,
    /// RunIndex) makes every player's first five waves identical, so "the boss usually kills a new
    /// player" was never the right description: it killed all of them, always, and no amount of luck
    /// was going to save one. If this ever stops being true the sweep above must start varying
    /// RunIndex instead.
    /// </remarks>
    [Fact]
    public void test_every_careers_first_descent_is_the_same_five_waves()
    {
        var tuning = ExpeditionTuning.UntilTheFirstBossFalls;
        var first = Walk(0, tuning);
        foreach (var seed in new[] { 1, 7, 99, 4242 })
        {
            var run = Walk(seed, tuning);
            Assert.Equal(first.Fell, run.Fell);
            Assert.Equal(first.BossWave, run.BossWave);
        }
        // The seed is not ignored — it moves the health carried off the boss, which is the within-wave
        // jitter the sweep exists to cover. (If this ever came out equal for every seed the sweep would
        // be measuring one run five hundred times.)
        var carried = Enumerable.Range(0, 40)
            .Select(s => Walk(s, tuning, toWave: OpeningScript.TutorialBossWave).HealthAfterBoss)
            .Distinct().Count();
        Assert.True(carried > 1, "every seed carried identical health off the boss — the sweep varies nothing");
    }

    /// <summary>
    /// THE FIRST BOSS IS THE GAME'S OWN FIRST BOSS, and the window is exactly as wide as the wave it
    /// lands on — however the cadence is later retuned.
    /// </summary>
    [Fact]
    public void test_the_taught_window_is_pinned_to_the_boss_cadence()
    {
        Assert.Equal(ExpeditionTuning.Default.BossEvery, ExpeditionTuning.UntilTheFirstBossFalls.TutorialWaves);
        Assert.Equal(OpeningScript.TutorialBossWave, ExpeditionTuning.UntilTheFirstBossFalls.TutorialWaves);
        Assert.True(WaveScaling.IsBossWave(ExpeditionTuning.UntilTheFirstBossFalls.TutorialWaves,
                                           ExpeditionTuning.UntilTheFirstBossFalls));
    }
}
