using System;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// CRIT, FOCUS and DEFENSE were trained but read by NOTHING after the pivot to the solo model — the
/// Character screen's damage bench showed them moving nothing. These prove each now reaches the fight.
/// </summary>
public class HunterStatWiringTests
{
    private static Build StrikeBuild()
    {
        var b = new Build();
        b.Equip(TestBuilds.Skill("hammer_blow"));
        return b;
    }

    private static Hunter Trained(HunterStat stat, int ranks)
    {
        var h = new Hunter();
        h.AddGleam(10_000_000);
        for (var i = 0; i < ranks; i++) h.Train(stat);
        return h;
    }

    [Fact]
    public void test_training_crit_raises_damage()
        // More critical chance → a bigger expected-value crit factor on skill hits → more damage.
        => Assert.True(DamageBench.Measure(StrikeBuild(), Trained(HunterStat.CriticalChance, 40)).Dps
                       > DamageBench.Measure(StrikeBuild(), new Hunter()).Dps,
            "CRIT reached no damage — training critical chance moved nothing");

    [Fact]
    public void test_training_focus_raises_damage()
        // FOCUS is crit DAMAGE, so it pays through the (base 5%) crit chance — enough to make a larger crit
        // multiplier show as more damage.
        => Assert.True(DamageBench.Measure(StrikeBuild(), Trained(HunterStat.Focus, 40)).Dps
                       > DamageBench.Measure(StrikeBuild(), new Hunter()).Dps,
            "FOCUS reached no damage — training crit damage moved nothing");

    [Fact]
    public void test_training_defense_reduces_damage_taken()
    {
        int Taken(Hunter h)
        {
            var champ = new Champion { MaxHealth = 500_000, Health = 500_000 };   // survives the whole window
            var (_, e) = SoloBattle.ResolveWave(champ, StrikeBuild(), h,
                float.MaxValue, enemyDamage: 100f, enemyIntervalMs: 1_000,
                ExpeditionTuning.Default, new Random(7));
            return e.Where(x => x.Kind == BattleEventKind.EnemyStrike).Sum(x => x.Amount);
        }

        Assert.True(Taken(Trained(HunterStat.Defense, 40)) < Taken(new Hunter()),
            "DEFENSE reached no fight — training it did not reduce incoming damage");
    }
}
