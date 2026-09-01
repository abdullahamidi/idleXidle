using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The bench that lets a player (and this test suite) ask "did that change the damage?".
/// </summary>
public class DamageBenchTests
{
    private static Build StrikeBuild(params string[] keystones)
    {
        var b = new Build();
        b.Weave(TestBuilds.Skill("hammer_blow"));
        foreach (var k in keystones) b.Take(Keystones.ById(k)!);
        return b;
    }

    [Fact]
    public void test_the_bench_is_deterministic()
    {
        var build = StrikeBuild();
        var hunter = new Hunter();
        Assert.Equal(DamageBench.Measure(build, hunter), DamageBench.Measure(build, hunter));
    }

    [Fact]
    public void test_a_real_build_measures_positive_dps()
    {
        var r = DamageBench.Measure(StrikeBuild(), new Hunter());
        Assert.True(r.Dps > 0f, "the bench read zero damage for a build that swings a Strike");
        Assert.True(r.Seconds > 0f);
    }

    [Fact]
    public void test_a_damage_keystone_measures_higher_than_a_plain_build()
    {
        var hunter = new Hunter();
        Assert.True(DamageBench.Measure(StrikeBuild("glass_cannon"), hunter).Dps
                    > DamageBench.Measure(StrikeBuild(), hunter).Dps,
            "GLASS CANNON did not read as more damage on the bench");
    }

    [Fact]
    public void test_training_attack_power_raises_the_measured_dps()
    {
        // The bench's reason to exist: a trained stat must MOVE the number, or it reaches no formula.
        // AttackPower feeds SquadDamageMultiplier, which Build.Resolve folds in — so this must hold, and
        // when it stops holding the wire is broken. (FOCUS and CRIT are wired too now — see
        // HunterStatWiringTests; the bench revealing them dead is exactly what got them wired.)
        var build = StrikeBuild();

        var fresh = new Hunter();
        var trained = new Hunter();
        trained.AddGleam(1_000_000);
        for (var i = 0; i < 20; i++) trained.Train(HunterStat.AttackPower);

        Assert.True(DamageBench.Measure(build, trained).Dps > DamageBench.Measure(build, fresh).Dps,
            "training AttackPower did not raise measured damage — the stat reaches no formula");
    }
}
