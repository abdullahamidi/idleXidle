using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// CHARGE — the shared stack primitive: every cast stores a point, keystones bend the pool.
/// </summary>
/// <remarks>
/// The dormancy guards matter as much as the payoffs here: a pool nobody reads must not even be
/// tracked, and the events the HUD latches must exist exactly when a charge keystone does.
/// </remarks>
public class ChargeKeystoneTest
{
    private static Build TwoSkills(params string[] keystoneIds)
    {
        var b = new Build();
        b.Weave(new EquippedSkill(
            new WovenAbility { Name = "A", Source = Source.Nature, Form = Form.Strike },
            FormBehaviour.BaseCooldownMs(Form.Strike)));
        b.Weave(new EquippedSkill(
            new WovenAbility { Name = "B", Source = Source.Nature, Form = Form.Projectile },
            FormBehaviour.BaseCooldownMs(Form.Projectile)));
        foreach (var id in keystoneIds)
            Assert.True(b.Take(Keystones.ById(id)!), $"could not socket {id}");
        return b;
    }

    private static (float Delivered, IReadOnlyList<BattleEvent> Events) Run(Build build)
    {
        var champ = new Champion { MaxHealth = 200_000, Health = 200_000 };
        var metrics = new WaveMetrics();
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new ResonanceHunter.Core.Economy.Hunter(),
            new List<WaveCreature> { WaveCreature.Single(5_000_000f, 1f, 0f, Source.Nature) },
            enemyIntervalMs: 900,
            ResonanceHunter.Core.Expeditions.ExpeditionTuning.Default, new Random(9), metrics: metrics);
        return (metrics.DeliveredDamage, events);
    }

    [Fact]
    public void test_charge_pool_is_not_even_tracked_without_a_reading_keystone()
    {
        // Arrange + Act: no charge keystone, no Charge events — the dormancy guard.
        var (_, events) = Run(TwoSkills());

        // Assert
        Assert.DoesNotContain(events, e => e.Kind == BattleEventKind.Charge);
    }

    [Fact]
    public void test_charge_rend_spends_the_pool_and_the_spend_pays()
    {
        // Arrange: the control carries REND's flat tax (x0.9 damage) WITHOUT the trigger, via the
        // PassiveMods channel — so the only living difference is the pool and its dumps.
        var rend = TwoSkills("rend");
        var taxed = TwoSkills();
        taxed.PassiveMods = new BuildMods(Damage: 0.9f, Health: 1f, SkillRate: 1f, Haul: 1f, Rarity: 1f);

        // Act
        var (rendDealt, rendEvents) = Run(rend);
        var (taxedDealt, _) = Run(taxed);

        // Assert: the dump exists (a Charge event at 0) and the rhythm nets above the tax alone.
        Assert.Contains(rendEvents, e => e.Kind == BattleEventKind.Charge && e.Amount == 0);
        Assert.True(rendDealt > taxedDealt,
            $"rend {rendDealt} vs tax-only {taxedDealt} — the pool never paid");
    }

    [Fact]
    public void test_charge_capacitor_raises_the_cap_and_alone_it_only_fills()
    {
        // Arrange: CAPACITOR with no spender — the pool climbs past 10, and is never dumped.
        var (_, events) = Run(TwoSkills("capacitor"));
        var charges = events.Where(e => e.Kind == BattleEventKind.Charge).ToList();

        // Assert
        Assert.NotEmpty(charges);
        Assert.True(charges.Max(e => e.Amount) > SoloBattle.ChargeCap,
            $"the pool peaked at {charges.Max(e => e.Amount)} — the extended cap never mattered");
        Assert.True(charges.Max(e => e.Amount) <= SoloBattle.ChargeCapExtended);
        Assert.DoesNotContain(charges, e => e.Amount == 0);
    }

    [Fact]
    public void test_charge_lodestone_pays_a_core_for_a_full_pool_at_the_clear()
    {
        // Arrange: a killable foe, big enough that the pool fills before the clear. No REND, so
        // nothing ever dumps the pool — the holder's condition is met by simply playing on.
        (WaveOutcome Outcome, WaveBonus Bonus) Clear(params string[] keystones)
        {
            var champ = new Champion { MaxHealth = 200_000, Health = 200_000 };
            var bonus = new WaveBonus();
            var (outcome, _) = SoloBattle.ResolveWave(
                champ, TwoSkills(keystones), new ResonanceHunter.Core.Economy.Hunter(),
                new List<WaveCreature> { WaveCreature.Single(3_000f, 1f, 0f, Source.Nature) },
                enemyIntervalMs: 900,
                ResonanceHunter.Core.Expeditions.ExpeditionTuning.Default, new Random(9),
                bonus: bonus);
            return (outcome, bonus);
        }

        // Act
        var held = Clear("lodestone");
        var plain = Clear();

        // Assert
        Assert.Equal(WaveOutcome.Cleared, held.Outcome);
        Assert.Equal(1, held.Bonus.Cores);
        Assert.Equal(0, plain.Bonus.Cores);
    }

    [Fact]
    public void test_charge_dynamo_winds_the_pool_on_bites()
    {
        // Arrange + Act
        var (_, events) = Run(TwoSkills("dynamo"));

        // Assert: at least one pool change lands on a bite's own timestamp.
        var biteTimes = events.Where(e => e.Kind == BattleEventKind.EnemyStrike)
                              .Select(e => e.AtMs).ToHashSet();
        Assert.Contains(events, e => e.Kind == BattleEventKind.Charge && biteTimes.Contains(e.AtMs));
    }
}
