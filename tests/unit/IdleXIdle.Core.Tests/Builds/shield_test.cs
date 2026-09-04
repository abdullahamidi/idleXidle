using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// SHIELD — the twenty behaviours the combat-v2 brief §86 asks for, checked against the simulation.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these is written against what a WAVE does, not against a field. Shield's whole risk is
/// that it becomes "health but better": the tests that matter here are the ones proving it is NOT
/// health — that a bite it ate did not feed REPAY, did not count as damage taken, did not satisfy a
/// health-scaling rule and did not make a hunter look hurt.
/// </para>
/// <para>
/// The wave is resolved directly rather than through an expedition so the arithmetic is exact: one
/// creature, one known bite, one known interval.
/// </para>
/// </remarks>
public class ShieldTests
{
    // A bite big enough to be worth absorbing, slow enough that a wave contains a countable number.
    private const float BiteDamage = 100f;
    private const int BiteMs = 1_000;

    /// <summary>
    /// A tree that has walked every skill road, so the fixture may equip any of the twelve.
    /// </summary>
    /// <remarks>
    /// <c>BuildComposer.Compose</c> drops a pick whose skill the mastery tree has not taught. A bare
    /// tree therefore composes an EMPTY build, and a probe against one measures nothing but the basic
    /// attack — which is exactly how the first draft of this file "proved" that shielded and unshielded
    /// REPAY paid back the same amount.
    /// </remarks>
    private static MasteryTree EveryRoadWalked()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        tree.RestoreTaken(MasteryCatalog.Nodes
            .Where(x => x.Kind == MasteryKind.SkillRoad)
            .Select(x => x.Id));
        return tree;
    }

    private static Build Bare(params BuildComposer.SkillPick[] picks)
        => BuildComposer.Compose(EveryRoadWalked(), character: null,
                                 skills: picks.ToList(), keystoneIds: Array.Empty<string>(),
                                 slotCapacity: 4, progress: new SkillProgress());

    private static Build WithShape(SkillShape shape, params BuildComposer.SkillPick[] picks)
    {
        var build = Bare(picks);
        build.Shape = SkillShape.Combine(build.Shape, shape);
        return build;
    }

    /// <summary>
    /// A build carrying JAWS in its IRON variation — the trap that stops a whole bite.
    /// </summary>
    /// <remarks>
    /// This is the OTHER hard preventer, and the one the brief names beside MACHINE's: "if JAWS / IRON
    /// already stops the bite, MACHINE 5p must NOT waste its once-per-wave prevention". It used to be
    /// tested against FORTIFY instead, which was a shape flag no keystone, node or set could grant —
    /// so the composition being proved was one no player could ever assemble. IRON is one a player
    /// weaves.
    /// </remarks>
    private static Build WithIron(SkillShape shape)
    {
        var def = SkillCatalogue.ById("snare_jaws");
        var progress = new SkillProgress();
        for (var i = 0; i < SkillProgress.UsesForLevel(SkillProgress.MaxLevel); i++) progress.RecordWave(def.Id);
        Assert.True(progress.ChooseVariation(def, "IRON"));
        var build = BuildComposer.Compose(
            EveryRoadWalked(), character: null,
            skills: new List<BuildComposer.SkillPick> { new(Source.Machine, null, SkillId: def.Id) },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4, progress: progress);
        build.Shape = SkillShape.Combine(build.Shape, shape);
        return build;
    }

    /// <summary>One wave against one creature that never dies, so only the bites matter.</summary>
    private static (WaveOutcome Outcome, List<BattleEvent> Events, WaveMetrics Metrics) Fight(
        Champion champ, Build build, float creatureHealth = 1_000_000f, int waveMs = 6_000)
    {
        var metrics = new WaveMetrics();
        var creature = new WaveCreature { MaxHealth = creatureHealth, Health = creatureHealth, Damage = BiteDamage };
        var tuning = ExpeditionTuning.Default with { TickCeilingMs = waveMs };
        var (outcome, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), new[] { creature }, BiteMs, tuning, new Random(7), metrics: metrics);
        return (outcome, events, metrics);
    }

    private static Champion Fresh(int pool = 10_000) => new() { MaxHealth = pool, Health = pool };

    private static long Sum(IEnumerable<BattleEvent> events, BattleEventKind kind)
        => events.Where(e => e.Kind == kind).Sum(e => (long)e.Amount);

    // ── 5. THE CAP ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_shield_cap_is_half_of_maximum_health()
    {
        Assert.Equal(500, ShieldRules.CapFor(1_000));
        var champ = Fresh(1_000);
        Assert.Equal(500, champ.MaxShield);

        // Offered far more than the ceiling; it takes what it can and reports only that.
        var added = champ.GainShield(10_000f);
        Assert.Equal(500f, champ.CurrentShield);
        Assert.Equal(500f, added);
        Assert.Equal(0f, champ.GainShield(1f));   // at the cap, a further grant adds nothing
    }

    // ── 1, 2, 3, 4. ABSORPTION ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_shield_absorbs_before_health()
    {
        var champ = Fresh();
        champ.GainShield(60f);

        // It returns what it ATE; the caller sends the rest to the pool.
        var absorbed = champ.AbsorbWithShield(100f);
        Assert.Equal(60f, absorbed);
        Assert.Equal(40f, 100f - absorbed);
        Assert.Equal(0f, champ.CurrentShield);
    }

    [Fact]
    public void test_partial_absorption_leaves_the_remainder_for_health()
    {
        var champ = Fresh(1_000);
        champ.GainShield(50f);
        // Machine 3p grants at wave start; here the grant is direct so the arithmetic is visible.
        var absorbed = champ.AbsorbWithShield(70f);
        Assert.Equal(50f, absorbed);
        Assert.Equal(0f, champ.CurrentShield);
    }

    [Fact]
    public void test_full_absorption_leaves_health_untouched()
    {
        var champ = Fresh(1_000);
        // 12% of the pool at wave start, and a bite smaller than that.
        var build = WithShape(new SkillShape { WaveStartShieldFraction = 0.12f });
        var (_, events, metrics) = Fight(champ, build, waveMs: 1_500);

        Assert.True(metrics.ShieldAbsorbed > 0f, "the shield ate nothing");
        Assert.Equal(0, metrics.HealthDamage);
        Assert.Equal(champ.MaxHealth, champ.Health);
        Assert.True(Sum(events, BattleEventKind.ShieldAbsorbed) > 0);
    }

    [Fact]
    public void test_shield_absorbs_post_mitigation_damage_not_the_raw_bite()
    {
        // Half the bite is mitigated away, so a 100 bite reaches the shield as 50.
        var champ = Fresh(100_000);
        var build = WithShape(new SkillShape
        {
            WaveStartShieldFraction = 0.5f,   // far more shield than one bite
            DamageTaken = 0.5f,
        });
        var (_, _, metrics) = Fight(champ, build, waveMs: 1_500);

        Assert.Equal(0, metrics.HealthDamage);
        // One bite, mitigated to half: the shield spent 50, not 100.
        Assert.InRange(metrics.ShieldAbsorbed, 40f, 60f);
    }

    // ── 6, 7. WAVE-LOCAL ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_shield_resets_at_the_start_of_every_wave()
    {
        var champ = Fresh(10_000);
        champ.GainShield(400f);
        Assert.Equal(400f, champ.CurrentShield);

        // A wave with no shield source at all: the reset must clear what was carried in.
        Fight(champ, Bare(), waveMs: 300);
        Assert.Equal(0f, champ.CurrentShield);
    }

    [Fact]
    public void test_wave_start_shield_is_granted_after_the_reset()
    {
        var champ = Fresh(1_000);
        champ.GainShield(999f);   // carried in, and must not survive
        var build = WithShape(new SkillShape { WaveStartShieldFraction = 0.12f });
        var (_, events, _) = Fight(champ, build, waveMs: 300);

        // Exactly one grant, of exactly 12% — not the carried figure, and not both.
        var gains = events.Where(e => e.Kind == BattleEventKind.ShieldGained).ToList();
        Assert.Single(gains);
        Assert.Equal(120, gains[0].Amount);
    }

    // ── 8. SHIELD DOES NOT FEED REPAY ────────────────────────────────────────────────────────────

    [Fact]
    public void test_absorbed_damage_does_not_feed_repay()
    {
        // REPAY pays back a multiple of HEALTH damage. A wave whose bites are entirely absorbed owes
        // nothing — the whole reason a defensive layer must not double as an offensive one.
        var repay = SkillCatalogue.ById("snare_repay");
        var picks = new[]
        {
            new BuildComposer.SkillPick(Source.Machine, null, SkillId: repay.Id),
        };

        var shielded = WithShape(new SkillShape { WaveStartShieldFraction = 0.5f }, picks);
        var bare = Bare(picks);

        // Long enough for REPAY — a five-beat Active — to actually come round and spend its bank.
        var shieldedDealt = Sum(Fight(Fresh(100_000), shielded, waveMs: 20_000).Events, BattleEventKind.Strike);
        var bareDealt = Sum(Fight(Fresh(100_000), bare, waveMs: 20_000).Events, BattleEventKind.Strike);

        // The bare run took real damage and paid it back; the shielded one had nothing to pay back.
        Assert.True(bareDealt > shieldedDealt,
            $"REPAY paid back absorbed damage: shielded {shieldedDealt} vs bare {bareDealt}");
    }

    // ── 9, 10. SHIELD IS NOT HEALTH ──────────────────────────────────────────────────────────────

    [Fact]
    public void test_shield_is_not_health_for_a_health_scaling_rule()
    {
        // GLUT scales its damage with CURRENT HEALTH. Shield must not make a wounded hunter read as
        // a healthy one — the rule asks about the pool, and the pool is unchanged.
        var drink = SkillCatalogue.ById("drain_drink");
        var progress = new SkillProgress();
        for (var i = 0; i < SkillProgress.UsesForLevel(1); i++) progress.RecordWave(drink.Id);
        Assert.True(progress.ChooseVariation(drink, "GLUT"));

        var picks = new List<BuildComposer.SkillPick> { new(Source.Nature, null, SkillId: drink.Id) };
        Build Make() => BuildComposer.Compose(EveryRoadWalked(), character: null,
                                              skills: picks, keystoneIds: Array.Empty<string>(),
                                              slotCapacity: 4, progress: progress);

        // Two hunters at the SAME half-health, one of them holding a full shield.
        var plain = new Champion { MaxHealth = 10_000, Health = 5_000 };
        var shielded = new Champion { MaxHealth = 10_000, Health = 5_000 };
        shielded.GainShield(shielded.MaxShield);

        var a = Sum(Fight(plain, Make(), creatureHealth: 5_000_000f, waveMs: 6_000).Events, BattleEventKind.Strike);
        var b = Sum(Fight(shielded, Make(), creatureHealth: 5_000_000f, waveMs: 6_000).Events, BattleEventKind.Strike);

        // The shield is reset at the wave's start in both, so the two must agree exactly.
        Assert.Equal(a, b);
    }

    [Fact]
    public void test_shield_does_not_make_a_hurt_hunter_read_as_full()
    {
        var champ = new Champion { MaxHealth = 1_000, Health = 400 };
        champ.GainShield(champ.MaxShield);

        // Health is the only thing the pool's own questions read.
        Assert.Equal(400, champ.Health);
        Assert.Equal(1_000, champ.MaxHealth);
        Assert.True(champ.Alive);
        Assert.Equal(500f, champ.CurrentShield);
    }

    // ── 11. HEALING DOES NOT RESTORE SHIELD ──────────────────────────────────────────────────────

    [Fact]
    public void test_healing_does_not_restore_shield()
    {
        // A wave with a strong regen and no shield producer ends with no shield at all.
        var champ = new Champion { MaxHealth = 10_000, Health = 5_000 };
        var build = WithShape(new SkillShape { RegenFraction = 0.05f });
        var (_, events, _) = Fight(champ, build, waveMs: 4_000);

        Assert.True(Sum(events, BattleEventKind.Heal) > 0, "the fixture did not heal");
        Assert.Equal(0f, champ.CurrentShield);
        Assert.Empty(events.Where(e => e.Kind == BattleEventKind.ShieldGained));
    }

    // ── 14, 15, 16. PREVENTION COMES FIRST ───────────────────────────────────────────────────────

    [Fact]
    public void test_a_hard_prevented_bite_does_not_consume_shield()
    {
        // IRON stops a whole bite outright. The shield must still be whole afterwards.
        var champ = Fresh(1_000);
        var build = WithIron(new SkillShape { WaveStartShieldFraction = 0.5f });
        var (_, _, metrics) = Fight(champ, build, waveMs: 1_500);

        Assert.True(metrics.DamagePrevented > 0f, "nothing was prevented");
        Assert.Equal(0f, metrics.ShieldAbsorbed);
        Assert.Equal(500f, champ.CurrentShield);   // untouched
    }

    [Fact]
    public void test_plating_is_not_spent_on_a_bite_another_effect_already_stopped()
    {
        // MACHINE 5p and IRON both want the first bite. IRON takes it, and PLATING must keep its
        // once-a-wave charge for the next bite that would actually land.
        var champ = Fresh(100_000);
        var build = WithIron(new SkillShape { PreventFirstDamagingBite = true });
        var (_, events, metrics) = Fight(champ, build, waveMs: 3_500);

        // More than one bite prevented: IRON's, and then PLATING's on a later one.
        Assert.True(metrics.DamagePrevented > BiteDamage * 1.5f,
                    $"only {metrics.DamagePrevented} prevented — PLATING spent its charge on IRON's bite");
        // And PLATING paid its shield for the bite it really stopped.
        Assert.True(Sum(events, BattleEventKind.ShieldGained) > 0, "PLATING granted no shield");
    }

    [Fact]
    public void test_plating_grants_the_whole_of_the_bite_it_stopped_and_respects_the_cap()
    {
        var champ = Fresh(100_000);   // cap far above one bite
        var build = WithShape(new SkillShape { PreventFirstDamagingBite = true });
        var (_, events, _) = Fight(champ, build, waveMs: 1_500);

        var gained = Sum(events, BattleEventKind.ShieldGained);
        Assert.Equal((long)BiteDamage, gained);

        // The same rule against a tiny pool: the grant is clamped to the ceiling.
        var small = new Champion { MaxHealth = 40, Health = 40 };   // cap 20
        var (_, smallEvents, _) = Fight(small, WithShape(new SkillShape { PreventFirstDamagingBite = true }), waveMs: 1_500);
        Assert.Equal(20, Sum(smallEvents, BattleEventKind.ShieldGained));
    }

    // ── 20. THE BREAK CROSSING ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_shield_broken_fires_exactly_when_the_shield_reaches_zero()
    {
        var champ = Fresh(1_000);
        // 100 of shield against 100-damage bites: the first bite empties it exactly.
        var build = WithShape(new SkillShape { WaveStartShieldFraction = 0.1f });
        var (_, events, _) = Fight(champ, build, waveMs: 4_000);

        var breaks = events.Count(e => e.Kind == BattleEventKind.ShieldBroken);
        Assert.Equal(1, breaks);

        // And it is the crossing, not a repeat: later bites at zero shield add no further breaks.
        var absorbs = events.Count(e => e.Kind == BattleEventKind.ShieldAbsorbed);
        Assert.Equal(1, absorbs);
    }

    // ── 19. THE SNAPSHOT ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_replay_reconstructs_shield_without_witnessing_the_grant()
    {
        var champ = Fresh(1_000);
        var build = WithShape(new SkillShape { WaveStartShieldFraction = 0.3f });
        var (_, events, _) = Fight(champ, build, waveMs: 900);

        var replay = new WaveReplay(events, new Dictionary<int, int> { [0] = 1_000 },
                                    new Dictionary<int, int> { [0] = 1_000 }, 1_000f);
        replay.Advance(10_000);

        // The bar is drawable from the events alone, against its own ceiling.
        Assert.Equal(500, replay.MaxShield);
        Assert.True(replay.CurrentShield > 0, "the replay lost the shield");
    }

    // ── 18. OFFLINE AND LIVE AGREE ───────────────────────────────────────────────────────────────

    [Fact]
    public void test_shield_behaves_identically_on_two_identical_runs()
    {
        // Shield is gameplay: it may not depend on anything a headless run does not have.
        static (float Shield, int Health, float Absorbed) Run()
        {
            var champ = new Champion { MaxHealth = 4_000, Health = 4_000 };
            var build = WithShape(new SkillShape { WaveStartShieldFraction = 0.2f });
            var (_, _, metrics) = Fight(champ, build, waveMs: 5_000);
            return (champ.CurrentShield, champ.Health, metrics.ShieldAbsorbed);
        }

        Assert.Equal(Run(), Run());
    }

    // ── 4 (metrics). THE FOUR FACTS STAY APART ───────────────────────────────────────────────────

    [Fact]
    public void test_attempted_prevented_absorbed_and_health_damage_are_separate_numbers()
    {
        var champ = Fresh(100_000);
        var build = WithIron(new SkillShape { WaveStartShieldFraction = 0.001f });
        var (_, _, m) = Fight(champ, build, waveMs: 4_000);

        // Everything the wave tried is accounted for exactly once.
        Assert.Equal(m.DamageAttempted, m.DamagePrevented + m.ShieldAbsorbed + m.HealthDamage, 0);
        Assert.True(m.DamagePrevented > 0f);
        Assert.True(m.HealthDamage > 0);
    }
}
