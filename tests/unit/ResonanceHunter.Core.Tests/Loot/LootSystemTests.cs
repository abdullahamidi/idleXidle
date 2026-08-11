using System;
using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Loot;

public class LootSystemTests
{
    private static readonly LootTuning Tuning = LootTuning.Default;
    private static Random Seeded() => new(9001);

    private static KillContext Kill(
        int tier = 1, bool boss = false, float partBreak = 0f, float eff = 100f, int? automationStage = null)
        => new()
        {
            PowerTier = tier,
            IsBoss = boss,
            LootTiltPercent = partBreak,
            ActiveEfficiencyPercent = eff,
            AutomationStage = automationStage,
        };

    /// <summary>
    /// PILLAR 3, THE LOAD-BEARING GUARANTEE: every kill mints a core, active or automated.
    /// </summary>
    /// <remarks>
    /// Cores are the ONLY way to acquire a creature at MVP (capture is Vertical-Slice-deferred). If an
    /// automated kill could ever fail to drop one, idle play would be locked out of the game's entire
    /// creature-acquisition path.
    /// </remarks>
    [Theory]
    [InlineData(null)]  // active
    [InlineData(1)]     // automated, stage 1 — the most throttled case
    [InlineData(2)]
    public void test_every_single_kill_mints_exactly_one_creature_core(int? automationStage)
    {
        var rng = Seeded();

        for (var i = 0; i < 500; i++)
        {
            var loot = LootSystem.Roll(Kill(automationStage: automationStage), rng, Tuning);
            Assert.Equal(1, loot.Count(x => x.BaseType == ItemBaseType.CreatureCore));
        }
    }

    /// <summary>
    /// Every rarity tier keeps a NON-ZERO chance, under every combination — including the worst
    /// automated case. This is the provable form of "idle is never locked out of anything".
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(2)]
    public void test_no_rarity_tier_can_ever_be_zeroed_out(int? automationStage)
    {
        foreach (var tier in new[] { 1, 10, 20 })
        foreach (var partBreak in new[] { 0f, 20f, 40f })
        {
            var weights = LootSystem.RarityWeights(
                Kill(tier: tier, partBreak: partBreak, automationStage: automationStage), Tuning);

            Assert.All(weights, w => Assert.True(w > 0f, "A rarity tier was zeroed — Pillar 3 violated."));

            // And no tier may fall BELOW its baseline: automation dampens the bonus, never the floor.
            for (var i = 0; i < 5; i++)
                Assert.True(weights[i] >= Tuning.BaseRarityWeight[i] - 0.001f);
        }
    }

    /// <summary>Common's weight is never suppressed — the i=0 term zeroes both bonuses by construction.</summary>
    [Fact]
    public void test_common_weight_is_never_tilted_away()
    {
        var extreme = LootSystem.RarityWeights(Kill(tier: 20, partBreak: 40f), Tuning);
        Assert.Equal(Tuning.BaseRarityWeight[0], extreme[0], precision: 3);
    }

    /// <summary>Higher power_tier really does tilt toward better loot.</summary>
    [Fact]
    public void test_higher_power_tier_tilts_rarity_upward()
    {
        var low = LootSystem.RarityWeights(Kill(tier: 1), Tuning);
        var high = LootSystem.RarityWeights(Kill(tier: 20), Tuning);

        Assert.True(high[4] > low[4], "Legendary must be likelier at high tier.");
        Assert.True(high[4] / high.Sum() > low[4] / low.Sum());
    }

    /// <summary>An automated kill is WORSE than an active one — but never worthless.</summary>
    [Fact]
    public void test_automation_dampens_rarity_without_ever_removing_it()
    {
        var active = LootSystem.RarityWeights(Kill(tier: 10, partBreak: 30f), Tuning);
        var stage1 = LootSystem.RarityWeights(Kill(tier: 10, partBreak: 30f, automationStage: 1), Tuning);
        var stage2 = LootSystem.RarityWeights(Kill(tier: 10, partBreak: 30f, automationStage: 2), Tuning);

        // Active > Stage 2 > Stage 1, at the top tier...
        Assert.True(active[4] > stage2[4]);
        Assert.True(stage2[4] > stage1[4]);

        // ...but Stage 1 still keeps the full baseline. Never zero.
        Assert.Equal(Tuning.BaseRarityWeight[4], stage1[4], precision: 3);
    }

    /// <summary>
    /// THE CLAMP. Without it, active efficiency compounds through both kills/hour AND loot/kill,
    /// and peak active throughput grows without a ceiling — breaching the "~3x" contract.
    /// </summary>
    [Fact]
    public void test_the_bonus_drop_chance_is_hard_clamped_at_100()
    {
        var absurd = Kill(tier: 20, partBreak: 40f, eff: 300f);
        Assert.Equal(100f, LootSystem.EffectiveBonusDropChancePercent(absurd, Tuning));
    }

    /// <summary>Drop count is strictly bounded regardless of how extreme the inputs get.</summary>
    [Fact]
    public void test_drop_count_never_exceeds_its_hard_ceiling()
    {
        var rng = Seeded();
        var absurd = Kill(tier: 20, partBreak: 40f, eff: 300f, boss: true);

        for (var i = 0; i < 1000; i++)
        {
            var n = LootSystem.DropCount(absurd, rng, Tuning);
            Assert.InRange(n, Tuning.DropCountBaseBoss, Tuning.DropCountBaseBoss + Tuning.BonusRollAttempts);
        }
    }

    /// <summary>Active play must actually produce more loot — the premium the B1 bug had deleted.</summary>
    [Fact]
    public void test_skilled_active_play_yields_more_items_than_an_automated_kill()
    {
        var automated = Mean(Kill(tier: 5, automationStage: 1));
        var skilled = Mean(Kill(tier: 5, partBreak: 40f, eff: 290f));

        Assert.True(skilled > automated, $"Active ({skilled:F2}) must beat automated ({automated:F2}).");
    }

    private static float Mean(KillContext ctx)
    {
        var rng = new Random(123);
        const int n = 3000;
        var total = 0;
        for (var i = 0; i < n; i++) total += LootSystem.DropCount(ctx, rng, Tuning);
        return (float)total / n;
    }
}

public class HunterProgressionTests
{
    private static readonly ProgressionTuning Tuning = ProgressionTuning.Default;

    /// <summary>Defense starts at ZERO — the fact that broke vow_fragility's original pricing.</summary>
    [Fact]
    public void test_defense_starts_at_zero_and_caps_at_120()
    {
        var hunter = new Hunter();
        Assert.Equal(0, hunter.Defense);

        hunter.AddGleam(1_000_000);
        for (var i = 0; i < Tuning.StatRankCap; i++) Assert.True(hunter.Train(HunterStat.Defense));

        Assert.Equal(120, hunter.Defense);
        Assert.False(hunter.Train(HunterStat.Defense)); // capped
    }

    [Fact]
    public void test_training_costs_gleam_and_rises_geometrically()
    {
        var hunter = new Hunter();
        hunter.AddGleam(10_000);

        var first = hunter.NextRankCost(HunterStat.AttackPower);
        hunter.Train(HunterStat.AttackPower);
        var second = hunter.NextRankCost(HunterStat.AttackPower);

        Assert.Equal(25, first);
        Assert.True(second > first, "Each rank must cost more than the last, or maxing is trivial.");
    }

    [Fact]
    public void test_a_broke_hunter_cannot_train()
    {
        var hunter = new Hunter();
        Assert.False(hunter.CanTrain(HunterStat.AttackPower));
        Assert.False(hunter.Train(HunterStat.AttackPower));
        Assert.Equal(0, hunter.RankOf(HunterStat.AttackPower));
    }

    /// <summary>Gleam finally has a sink big enough to matter — W1's whole point.</summary>
    [Fact]
    public void test_the_lifetime_gleam_sink_is_substantial()
    {
        var total = Hunter.TotalLifetimeSink(Tuning);

        // Previously Gleam's only sink was vow-binding (~200 each), saturating in "low tens" of buys.
        Assert.True(total > 50_000, $"Sink is only {total} Gleam — too small to absorb the faucet.");
    }

    /// <summary>An equipped charm is ineligible for sale. The hole that got shipped once already.</summary>
    [Fact]
    public void test_an_equipped_charm_cannot_be_sold()
    {
        var hunter = new Hunter();

        var equipped = new ItemInstance
        {
            InstanceId = "a", BaseType = ItemBaseType.Charm, Rarity = Rarity.Epic,
            SellValue = 82, EquippedToCreatureId = "creature_1",
        };
        var loose = new ItemInstance
        {
            InstanceId = "b", BaseType = ItemBaseType.Charm, Rarity = Rarity.Epic, SellValue = 82,
        };

        var gained = hunter.Sell(new[] { equipped, loose });

        Assert.Equal(82, gained);       // only the loose one sold
        Assert.Equal(82, hunter.Gleam);
    }

    /// <summary>
    /// Pillar 1 survives progression: maxing offence does not trivialize combat.
    /// </summary>
    /// <remarks>
    /// Measure the growth in <b>damage</b>, not in the raw stat. An earlier version of this test
    /// asserted on the stat and failed — <c>attack_power</c> grows 13x (10 -> 130) across a full
    /// Training track, which looks alarming. But the stat feeds damage sub-linearly (Formula 1's
    /// coefficient is 0.008), so 13x of stat buys only ~1.9x of damage. The raw number was never the
    /// thing that mattered; what reaches the creature is.
    ///
    /// That failure also exposed a real gap: <c>attack_power</c> was not wired into basic-attack
    /// damage at all. The encounter used a flat base value, so Training had <i>zero</i> effect on
    /// combat. Now it does.
    /// </remarks>
    [Fact]
    public void test_maxing_offence_does_not_out_scale_the_difficulty_curve()
    {
        // Measured on the LIVE damage lever (SquadDamageMultiplier, which SoloBattle multiplies every hit by),
        // not the retired manual-combat DamagePipeline. Attack training must matter but not brute-force the tier.
        var hunter = new Hunter();
        hunter.AddGleam(10_000_000);

        var untrained = hunter.SquadDamageMultiplier;

        for (var i = 0; i < Tuning.StatRankCap; i++) hunter.Train(HunterStat.AttackPower);

        var maxed = hunter.SquadDamageMultiplier;
        var damageGrowth = maxed / untrained;

        // Training must MATTER...
        Assert.True(damageGrowth > 1.3f, $"Maxing attack only bought {damageGrowth:F2}x damage — pointless.");

        // ...but content gets ~7x tankier across the tier range. If raw stats could cover that gap, the
        // build/gear layer would become optional and everything collapses into a stat check.
        Assert.True(damageGrowth < 3f,
            $"Maxing attack bought {damageGrowth:F2}x damage — enough to brute-force the tier curve.");
    }

    /// <summary>Training must actually change what the fight deals. It previously did not.</summary>
    [Fact]
    public void test_training_attack_power_increases_damage()
    {
        var hunter = new Hunter();
        hunter.AddGleam(100_000);

        var before = hunter.SquadDamageMultiplier;
        for (var i = 0; i < 20; i++) hunter.Train(HunterStat.AttackPower);
        var after = hunter.SquadDamageMultiplier;

        Assert.True(after > before);
    }
}
