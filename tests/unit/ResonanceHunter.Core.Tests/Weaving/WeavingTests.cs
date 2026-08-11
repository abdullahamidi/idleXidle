using System;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Abilities;
using Xunit;

namespace ResonanceHunter.Core.Tests.Abilities;

public class WeavingTests
{
    private static readonly WeavingTuning Tuning = WeavingTuning.Default;
    private static readonly Source[] AllSources = Enum.GetValues<Source>();

    // ── The Source table: no element may dominate ─────────────────────────────────────────────

    /// <summary>
    /// Every Source has EXACTLY two strengths and two weaknesses. There is no best element.
    /// </summary>
    [Fact]
    public void test_every_source_has_exactly_two_strengths_and_two_weaknesses()
    {
        foreach (var attacker in AllSources)
        {
            var strong = AllSources.Count(t => Weaving.SourceEffectiveness(attacker, t, Tuning) > 1.0f);
            var weak = AllSources.Count(t => Weaving.SourceEffectiveness(attacker, t, Tuning) < 1.0f);

            Assert.Equal(2, strong);
            Assert.Equal(2, weak);
        }
    }

    /// <summary>The table is antisymmetric: if A beats B, then B is weak to A.</summary>
    [Fact]
    public void test_the_source_table_is_antisymmetric()
    {
        foreach (var a in AllSources)
        foreach (var b in AllSources)
        {
            var ab = Weaving.SourceEffectiveness(a, b, Tuning);
            var ba = Weaving.SourceEffectiveness(b, a, Tuning);

            if (ab > 1.0f) Assert.True(ba < 1.0f, $"{a} beats {b}, so {b} must be weak to {a}.");
            if (ab < 1.0f) Assert.True(ba > 1.0f);
        }
    }

    /// <summary>Summed across every possible target, all Sources are exactly equal. No dominant pick.</summary>
    [Fact]
    public void test_no_source_is_better_than_another_on_average()
    {
        var totals = AllSources
            .Select(a => AllSources.Sum(t => Weaving.SourceEffectiveness(a, t, Tuning)))
            .ToList();

        Assert.All(totals, t => Assert.Equal(totals[0], t, precision: 4));
    }

    [Fact]
    public void test_a_source_is_neutral_against_itself()
        => Assert.All(AllSources, s => Assert.Equal(1.0f, Weaving.SourceEffectiveness(s, s, Tuning)));

    // ── Vows: the heart of Pillar 4 ───────────────────────────────────────────────────────────

    /// <summary>
    /// A harder Vow is ALWAYS worth more. No conditional Vow can dominate another.
    /// </summary>
    /// <remarks>
    /// If an easier-to-satisfy Vow ever paid more, it would strictly dominate and every other Vow
    /// would be dead content. The curve is strictly decreasing in uptime, so this holds by construction.
    /// </remarks>
    [Fact]
    public void test_a_rarer_vow_condition_always_grants_more_power()
    {
        for (var uptime = 0.05f; uptime < 0.95f; uptime += 0.05f)
        {
            var harder = Weaving.ConditionalMultiplier(uptime, Tuning);
            var easier = Weaving.ConditionalMultiplier(uptime + 0.05f, Tuning);

            Assert.True(harder > easier,
                $"Uptime {uptime:0.00} ({harder:0.000}) must beat {uptime + 0.05f:0.00} ({easier:0.000}).");
        }
    }

    /// <summary>An always-on Vow is not a restriction; a never-on one is not an ability.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-0.1f)]
    [InlineData(1.5f)]
    public void test_a_vow_uptime_outside_zero_to_one_is_rejected(float uptime)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Weaving.ConditionalMultiplier(uptime, Tuning));

    /// <summary>Two Vows with the same uptime are worth the same — the formula prices rarity, not flavour.</summary>
    [Fact]
    public void test_two_vows_with_identical_uptime_are_priced_identically()
    {
        var bloodied = Weaving.Catalog.Single(v => v.Id == "vow_bloodied");
        var bossBound = Weaving.Catalog.Single(v => v.Id == "vow_boss_bound");

        Assert.Equal(bloodied.ExpectedUptime, bossBound.ExpectedUptime);
        Assert.Equal(Weaving.VowMultiplier(bloodied, Tuning), Weaving.VowMultiplier(bossBound, Tuning), precision: 4);
    }

    /// <summary>
    /// THE UNBROKEN's condition can actually be FALSE — the regression for a Vow that was always-on.
    /// </summary>
    /// <remarks>
    /// Its old trigger (InTheFront) watched the squad slot, which the solo champion ALWAYS occupies, so
    /// the Vow paid its full 2.2x multiplier every fight for free — a strictly-dominant pick, priced as a
    /// restriction it never actually bore. It now watches health: active while whole, dead once a real
    /// fight brings you low. If IsActive ever returns true for BOTH states again, a conditional Vow has
    /// stopped being conditional.
    /// </remarks>
    [Fact]
    public void test_the_unbroken_vow_binds_to_health_not_a_phantom_slot()
    {
        var v = Weaving.ById("vow_vanguard")!;

        Assert.True(Weaving.IsActive(v, new WeaveContext(0.90f, 0, false, 0)), "THE UNBROKEN must hold while whole");
        Assert.False(Weaving.IsActive(v, new WeaveContext(0.30f, 0, false, 0)), "THE UNBROKEN must drop once you are hurt");
    }

    /// <summary>
    /// ALL static Vows lie on one line: 3.0 power per 1.0 of effective HP. None can dominate.
    /// </summary>
    /// <remarks>
    /// This is the test that would have caught vow_fragility. It was priced off the RAW stat fraction
    /// (-20% defense -> x1.60) while actually costing only ~10% effective HP — MORE power than
    /// reckless_offering (x1.45) for a SMALLER real cost. A textbook dominant option, and a direct
    /// Pillar 4 violation, hiding behind a plausible-looking number.
    /// </remarks>
    [Fact]
    public void test_no_static_vow_dominates_another()
    {
        var statics = Weaving.Catalog.Where(v => v.Kind == VowKind.StaticCost).ToList();
        Assert.True(statics.Count >= 2);

        foreach (var vow in statics)
        {
            var power = Weaving.VowMultiplier(vow, Tuning) - 1f;   // the bonus granted
            var cost = vow.StaticCostMagnitude;                    // the eHP paid

            var ratio = power / cost;

            // Every static Vow must buy power at the SAME exchange rate.
            Assert.Equal(Tuning.StaticCostConversionRate, ratio, precision: 2);
        }
    }

    /// <summary>vow_fragility must cost something to EVERY build — including a zero-defense one.</summary>
    [Fact]
    public void test_vow_fragility_is_not_free_for_a_zero_defense_build()
    {
        var fragility = Weaving.Catalog.Single(v => v.Id == "vow_fragility");

        // The cost is a post-mitigation damage multiplier, so it cannot be dodged by declining to
        // invest in defense. If this ever becomes a defense-percentage again, it is free once more.
        Assert.True(fragility.DamageTakenIncrease > 0f);
        Assert.True(fragility.StaticCostMagnitude > 0f);
    }

    /// <summary>A conditional Vow grants NOTHING when its condition is unmet. That is the trade.</summary>
    [Fact]
    public void test_a_conditional_vow_grants_no_power_when_its_condition_is_unmet()
    {
        var ability = new WovenAbility
        {
            Name = "TEST",
            Source = Source.Body,
            Form = Form.Strike,
            Vow = Weaving.Catalog.Single(v => v.Id == "vow_bloodied"),
        };

        var active = Weaving.AbilityPower(ability, Source.Body, 50f, vowActive: true, Tuning);
        var inactive = Weaving.AbilityPower(ability, Source.Body, 50f, vowActive: false, Tuning);

        Assert.True(active > inactive);
        Assert.Equal(Weaving.BasePower(Form.Strike, 50f, Tuning), inactive, precision: 3);
    }

    /// <summary>A static Vow is always on — its power does not depend on any condition.</summary>
    [Fact]
    public void test_a_static_vow_applies_regardless_of_condition()
    {
        var ability = new WovenAbility
        {
            Name = "TEST",
            Source = Source.Body,
            Form = Form.Strike,
            Vow = Weaving.Catalog.Single(v => v.Id == "vow_reckless_offering"),
        };

        var a = Weaving.AbilityPower(ability, Source.Body, 50f, vowActive: false, Tuning);
        var b = Weaving.AbilityPower(ability, Source.Body, 50f, vowActive: true, Tuning);

        Assert.Equal(a, b, precision: 4);
        Assert.True(a > Weaving.BasePower(Form.Strike, 50f, Tuning));
    }

    // ── Composition beats stat-stacking ───────────────────────────────────────────────────────

    /// <summary>
    /// Picking the right Source against a target beats raw affinity investment. Pillar 4.
    /// </summary>
    /// <remarks>
    /// A player who reads the matchup and brings the right element out-damages one who simply piles
    /// stats into resonance. If that were not true, builds would be stat-stacks, not trade-offs.
    /// </remarks>
    [Fact]
    public void test_the_right_source_beats_a_higher_stat_with_the_wrong_source()
    {
        var target = Source.Mind;

        var wellMatched = new WovenAbility { Name = "A", Source = Source.Body, Form = Form.Strike };
        var mismatched = new WovenAbility { Name = "B", Source = Source.Machine, Form = Form.Strike };

        Assert.True(Weaving.SourceEffectiveness(Source.Body, target, Tuning) > 1f);
        Assert.True(Weaving.SourceEffectiveness(Source.Machine, target, Tuning) < 1f);

        // The matched Source at MODEST affinity beats the mismatched one at DOUBLE the affinity.
        var matched = Weaving.AbilityPower(wellMatched, target, 60f, false, Tuning);
        var stacked = Weaving.AbilityPower(mismatched, target, 120f, false, Tuning);

        Assert.True(matched > stacked,
            $"Matched Source ({matched:0.0}) must beat a mismatched stat-stack ({stacked:0.0}).");
    }

    /// <summary>Mark deals no direct damage — it is an amplifier, not a hit.</summary>
    [Fact]
    public void test_mark_deals_no_direct_damage()
        => Assert.Equal(0f, Weaving.BasePower(Form.Mark, 100f, Tuning));

    /// <summary>Every catalog Vow is internally valid — a build-time content check.</summary>
    [Fact]
    public void test_every_catalog_vow_is_well_formed()
    {
        foreach (var vow in Weaving.Catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(vow.Description));

            if (vow.Kind == VowKind.Conditional)
                Assert.InRange(vow.ExpectedUptime, 0.01f, 0.99f);
            else
                Assert.True(vow.StaticCostMagnitude > 0f, $"{vow.Id} is a static Vow that costs nothing.");
        }
    }
}
