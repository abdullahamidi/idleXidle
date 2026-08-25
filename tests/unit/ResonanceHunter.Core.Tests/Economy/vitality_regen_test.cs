using System;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>VITALITY is life regained every second (2026-08-26), and no longer a second pool multiplier.</summary>
public class vitality_regen_test
{
    [Fact]
    public void test_vitality_no_longer_multiplies_the_pool()
    {
        var bare = new Hunter();
        var vital = new Hunter();
        for (var i = 0; i < 20; i++) { vital.AddGleam(1_000_000); vital.Train(HunterStat.Vitality); }
        Assert.Equal(bare.SquadHealthMultiplier, vital.SquadHealthMultiplier, 4);
        Assert.True(vital.RegenPerSecond > bare.RegenPerSecond);
        Assert.Equal(0f, bare.RegenPerSecond, 6);   // a fresh hunter regenerates nothing — VITALITY is trained into
    }

    [Fact]
    public void test_a_wounded_champion_regains_life_every_second_of_a_fight()
    {
        var hunter = new Hunter();
        for (var i = 0; i < 2; i++) { hunter.AddGleam(1_000_000); hunter.Train(HunterStat.Vitality); }   // 4 VITALITY: 0.12%/s
        Assert.True(hunter.RegenPerSecond > 0f);
        var build = new Build();
        // A huge, harmless enemy: the fight runs the whole ceiling and nothing bites.
        var champ = new Champion { MaxHealth = 10_000, Health = 5_000 };
        SoloBattle.ResolveWave(champ, build, hunter,
            enemyHealth: 1_000_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_000,
            ExpeditionTuning.Default, new Random(1));
        var seconds = ExpeditionTuning.Default.TickCeilingMs / 1000;
        var expected = Math.Max(1, (int)MathF.Round(10_000 * hunter.RegenPerSecond)) * seconds;
        Assert.True(expected < 5_000, "the fixture must not fill the pool, or the clamp hides the rate");
        Assert.InRange(champ.Health, 5_000 + expected - seconds, 5_000 + expected + seconds);
    }

    [Fact]
    public void test_regeneration_never_passes_full()
    {
        var hunter = new Hunter();
        var champ = new Champion { MaxHealth = 100, Health = 100 };
        SoloBattle.ResolveWave(champ, new Build(), hunter,
            enemyHealth: 1_000_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_000,
            ExpeditionTuning.Default, new Random(1));
        Assert.Equal(100, champ.Health);
    }
}
