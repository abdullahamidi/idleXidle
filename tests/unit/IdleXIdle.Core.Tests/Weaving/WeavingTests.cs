using System;
using System.Linq;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Weaving;

public class WeavingTests
{
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
            var strong = AllSources.Count(t => SourceMatchup.Effectiveness(attacker, t) > 1.0f);
            var weak = AllSources.Count(t => SourceMatchup.Effectiveness(attacker, t) < 1.0f);

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
            var ab = SourceMatchup.Effectiveness(a, b);
            var ba = SourceMatchup.Effectiveness(b, a);

            if (ab > 1.0f) Assert.True(ba < 1.0f, $"{a} beats {b}, so {b} must be weak to {a}.");
            if (ab < 1.0f) Assert.True(ba > 1.0f);
        }
    }

    /// <summary>Summed across every possible target, all Sources are exactly equal. No dominant pick.</summary>
    [Fact]
    public void test_no_source_is_better_than_another_on_average()
    {
        var totals = AllSources
            .Select(a => AllSources.Sum(t => SourceMatchup.Effectiveness(a, t)))
            .ToList();

        Assert.All(totals, t => Assert.Equal(totals[0], t, precision: 4));
    }

    [Fact]
    public void test_a_source_is_neutral_against_itself()
        => Assert.All(AllSources, s => Assert.Equal(1.0f, SourceMatchup.Effectiveness(s, s)));

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
            var harder = Vows.ConditionalMultiplier(uptime);
            var easier = Vows.ConditionalMultiplier(uptime + 0.05f);

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
        => Assert.Throws<ArgumentOutOfRangeException>(() => Vows.ConditionalMultiplier(uptime));

    /// <summary>Two Vows with the same uptime are worth the same — the formula prices rarity, not flavour.</summary>
    [Fact]
    public void test_two_vows_with_identical_uptime_are_priced_identically()
    {
        var a = Vows.Catalog.Single(v => v.Id == "vow_deliberate");
        var b = Vows.Catalog.Single(v => v.Id == "vow_frantic");

        Assert.Equal(a.Severity, b.Severity);
        Assert.Equal(Vows.Multiplier(a), Vows.Multiplier(b), precision: 4);
    }

    /// <summary>
    /// Every demand can be BOTH met and unmet. The regression for a Vow that was always-on.
    /// </summary>
    /// <remarks>
    /// THE UNBROKEN's old trigger watched the squad slot, which the solo champion ALWAYS occupies, so it
    /// paid its full multiplier every fight for free — a strictly-dominant pick priced as a restriction
    /// it never bore. The same hole exists for build demands: a demand every build satisfies is a free
    /// multiplier, and one no build can satisfy is a dead entry. This walks the whole catalogue rather
    /// than one Vow, so neither can be added quietly.
    /// </remarks>
    [Fact]
    public void test_every_demand_can_be_both_met_and_unmet()
    {
        // Two extremes of the build space. Nothing in between is needed: a demand that is true in both
        // or false in both is broken whichever way it leans.
        var wide = new BuildContext(
            DistinctStyles: 4, DistinctSources: 4, SkillsWoven: 4, SkillSlots: 4,
            CritPercent: 40f, BaseCritPercent: 5f, SkillRate: 2.0f, Defence: 60,
            KeystonesWorn: 3, WornSlots: new HashSet<BareSlot>
                { BareSlot.Boots, BareSlot.Gloves, BareSlot.Helm, BareSlot.Ring, BareSlot.Charm });

        var narrow = new BuildContext(
            DistinctStyles: 1, DistinctSources: 1, SkillsWoven: 1, SkillSlots: 4,
            CritPercent: 5f, BaseCritPercent: 5f, SkillRate: 0.6f, Defence: 0,
            KeystonesWorn: 0, WornSlots: new HashSet<BareSlot>());

        foreach (var vow in Vows.Catalog.Where(v => v.Kind == VowKind.Demand))
        {
            var met = Vows.IsActive(vow, wide) || Vows.IsActive(vow, narrow);
            var unmet = !Vows.IsActive(vow, wide) || !Vows.IsActive(vow, narrow);

            Assert.True(met, $"{vow.Id} is satisfied by no build at all — it is a dead catalogue entry.");
            Assert.True(unmet, $"{vow.Id} is satisfied by every build — it is a free multiplier wearing " +
                               "the word VOW, which is exactly what THE UNBROKEN was.");
        }
    }

    /// <summary>A demand is answered at the WORKBENCH, so it cannot read the fight.</summary>
    /// <remarks>
    /// The whole point of the rewrite. A Vow that paid only below 40% health was a lottery on how the
    /// wave went, in a game where the player cannot react to a wave at all — so whether the Vow paid was
    /// decided by the content rather than by them.
    /// </remarks>
    [Fact]
    public void test_a_demand_does_not_depend_on_how_the_fight_goes()
    {
        // UPDATED 2026-09-03. The RULE is unchanged — a context field must be something the player SET
        // at the workbench, never something a wave did — but the test's proxy for it was a substring
        // ban, and Vow discovery added two build-shape fields whose names contain banned words:
        //
        //   DamageTakenMultiplier    = build.Shape.DamageTaken, raised only by mastery greaters
        //   KeystoneHealthMultiplier = the health multiplier the SOCKETED KEYSTONES contribute
        //
        // Both are decisions taken before the descent, not readings taken during it, and each has
        // exactly one reader: the conduct proof of one of the two static-cost Vows, which have no
        // demand to keep and would otherwise have needed a fake one invented for them.
        //
        // They are named here rather than the ban being loosened, so a future field called
        // ChampionHealth or DamageDealt still fails this test — the exception is a list, not a hole.
        var allowed = new[] { "DamageTakenMultiplier", "KeystoneHealthMultiplier" };
        var fields = typeof(BuildContext).GetProperties().Select(p => p.Name).ToList();

        foreach (var name in allowed)
            Assert.Contains(name, fields);   // the exception must not outlive the field it is for

        var judged = fields.Where(f => !allowed.Contains(f)).ToList();
        foreach (var banned in new[] { "Health", "Elapsed", "Boss", "Wave", "Damage" })
            Assert.DoesNotContain(judged, f => f.Contains(banned, StringComparison.OrdinalIgnoreCase));
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
        var statics = Vows.Catalog.Where(v => v.Kind == VowKind.StaticCost).ToList();
        Assert.True(statics.Count >= 2);

        foreach (var vow in statics)
        {
            var power = Vows.Multiplier(vow) - 1f;   // the bonus granted
            var cost = vow.StaticCostMagnitude;                    // the eHP paid

            var ratio = power / cost;

            // Every static Vow must buy power at the SAME exchange rate.
            Assert.Equal(VowTuning.Default.StaticCostConversionRate, ratio, precision: 2);
        }
    }

    /// <summary>vow_fragility must cost something to EVERY build — including a zero-defense one.</summary>
    [Fact]
    public void test_vow_fragility_is_not_free_for_a_zero_defense_build()
    {
        var fragility = Vows.Catalog.Single(v => v.Id == "vow_fragility");

        // The cost is a post-mitigation damage multiplier, so it cannot be dodged by declining to
        // invest in defense. If this ever becomes a defense-percentage again, it is free once more.
        Assert.True(fragility.DamageTakenIncrease > 0f);
        Assert.True(fragility.StaticCostMagnitude > 0f);
    }

    // ── Composition beats stat-stacking ───────────────────────────────────────────────────────

    /// <summary>
    /// The matchup is a NUDGE now: it wins ties, and it no longer beats real investment.
    /// </summary>
    /// <remarks>
    /// This test used to pin the opposite — a matched Source at modest stats beating a mismatched
    /// one at DOUBLE stats — and that world is exactly what the playtest called "çok basit": the
    /// deepest-looking axis reduced to a colour chart. Since the SIGNATURE pass, an element is an
    /// identity (what it DOES when it lands), and the matchup is seasoning. Both directions are
    /// pinned so neither a re-inflation nor a total flattening can slip through. The root of a
    /// skill hit is SkillCatalogue.PoweredBase (the RESONANCE investment) times the matchup factor
    /// — the retired Weaving pipeline said the same sentence with more machinery.
    /// </remarks>
    [Fact]
    public void test_the_matchup_wins_ties_but_no_longer_beats_real_investment()
    {
        var target = Source.Mind;
        var def = SkillCatalogue.ById("hammer_blow");

        Assert.True(SourceMatchup.Effectiveness(Source.Body, target) > 1f);
        Assert.True(SourceMatchup.Effectiveness(Source.Machine, target) < 1f);

        // At EQUAL resonance the counter still wins — the nudge is real...
        var matched = SkillCatalogue.PoweredBase(def, 100f) * SourceMatchup.Effectiveness(Source.Body, target);
        var evenMismatch = SkillCatalogue.PoweredBase(def, 100f) * SourceMatchup.Effectiveness(Source.Machine, target);
        Assert.True(matched > evenMismatch,
            $"At equal resonance the matched Source ({matched:0.0}) must still beat the mismatched ({evenMismatch:0.0}).");

        // ...but DOUBLED resonance with the wrong element beats it COMFORTABLY. Evaluated at
        // 100/200, past the ~150 crossover of the linear resonance curve, with a stated margin —
        // a knife-edge pass here would trip on any retune for the wrong reason.
        var stacked = SkillCatalogue.PoweredBase(def, 200f) * SourceMatchup.Effectiveness(Source.Machine, target);
        Assert.True(stacked > matched * 1.05f,
            $"Doubled investment ({stacked:0.0}) must out-damage the bare counter ({matched:0.0}) clearly.");
    }

    /// <summary>Every catalog Vow is internally valid — a build-time content check.</summary>
    [Fact]
    public void test_every_catalog_vow_is_well_formed()
    {
        foreach (var vow in Vows.Catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(vow.Description));

            if (vow.Kind == VowKind.Demand)
                Assert.InRange(vow.Severity, 0.01f, 0.99f);
            else
                Assert.True(vow.StaticCostMagnitude > 0f, $"{vow.Id} is a static Vow that costs nothing.");
        }
    }
}
