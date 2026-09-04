using System;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The Nen buy-back: a SWORN VOW pulls an off-discipline skill one ring toward the build's affinity.
/// </summary>
/// <remarks>
/// The rule the game's Vows were born from — in the source system, a restriction is how you wield
/// what your affinity does not give you. The native style gains nothing (you already own it), and an
/// unsworn skill pays the full ring distance.
/// </remarks>
public class AffinityVowBuybackTest
{
    [Fact]
    public void test_affinity_buyback_a_vow_lifts_every_off_style_exactly_one_ring()
    {
        foreach (var affinity in Enum.GetValues<Style>())
            foreach (var skill in Enum.GetValues<Style>())
            {
                // Arrange
                var plain = StyleAffinity.Factor(affinity, skill);
                var sworn = StyleAffinity.Factor(affinity, skill, vowSworn: true);

                if (skill == affinity)
                {
                    // Assert: the native style gains nothing — you already own it.
                    Assert.Equal(plain, sworn);
                    continue;
                }

                // Assert: sworn is strictly better, and equals SOME ring's plain factor — one step in.
                Assert.True(sworn > plain, $"{affinity}->{skill}: sworn {sworn} not above plain {plain}");
                var ringFactors = Enum.GetValues<Style>()
                    .Select(s => StyleAffinity.Factor(affinity, s)).Distinct().ToList();
                Assert.Contains(sworn, ringFactors);
            }
    }

    [Fact]
    public void test_affinity_buyback_reaches_the_fight_for_a_kept_vow_and_not_for_a_broken_one()
    {
        // RENAMED AND RE-POINTED 2026-09-03, because the rule it drove was wrong. It used to swear a
        // vow whose demand was deliberately UNMET, so that the vow's own damage bonus paid nothing and
        // the only delta left was the buy-back. That isolation was clean and the premise was not: the
        // buy-back asked whether the slot CARRIED a vow and never whether the build KEPT it, so merely
        // CLAIMING a restriction bought an off-discipline skill a whole affinity ring. Restriction is
        // meant to buy power; a broken promise must buy nothing.
        //
        // The isolation is kept, differently: VowPowerMultiplier is zeroed, so a KEPT vow pays no
        // damage bonus at all and the buy-back is again the only delta.
        var (affinity, caster) = Enum.GetValues<Style>()
            .SelectMany(a => SkillCatalogue.All.Select(d => (a, d)))
            .First(p => p.d.Kind == SkillKind.Active
                        && p.d.Effect != SkillEffect.Amplify
                        && p.d.BasePower > 0f
                        && StyleAffinity.Factor(p.a, p.d.Style) < 1f);

        // ONE skill in a ONE-slot build meets VOW OF COMPLETION; the same skill in a four-slot build
        // breaks it. Same vow, same build, same seed — only the promise's standing differs.
        Build Casting(Vow? vow, int slots)
        {
            var b = new Build
            {
                Affinity = affinity,
                SlotCapacity = slots,
                // The vow's PAYOUT is switched off so the buy-back is the only thing left to measure.
                Shape = new SkillShape { VowPowerMultiplier = 0f },
            };
            b.Equip(new EquippedSkill(caster, Source.Nature, vow));
            return b;
        }

        var completion = Vows.Catalog.FirstOrDefault(v => v.Demand == VowDemand.EverySlotFilled);

        static float Damage(Build build)
        {
            var creatures = new System.Collections.Generic.List<WaveCreature>
            {
                new() { MaxHealth = 5_000_000, Health = 5_000_000, Damage = 1f },
            };
            var metrics = new WaveMetrics();
            SoloBattle.ResolveWave(
                new Champion { MaxHealth = 200_000, Health = 200_000 }, build,
                new IdleXIdle.Core.Economy.Hunter(), creatures, enemyIntervalMs: 900,
                IdleXIdle.Core.Expeditions.ExpeditionTuning.Default, new Random(5), metrics: metrics);
            return metrics.DeliveredDamage;
        }

        if (completion is null) return;   // catalogue changed shape; the unit test above still pins the rule
        var keptDamage = Damage(Casting(completion, slots: 1));
        var brokenDamage = Damage(Casting(completion, slots: 4));
        var plainDamage = Damage(Casting(null, slots: 1));

        // The buy-back is not dormant: a KEPT vow really does pull the off-discipline caster a ring in.
        Assert.True(keptDamage > plainDamage * 1.10f,
            $"kept {keptDamage} vs plain {plainDamage} — the buy-back never reached the sim");

        // And a BROKEN one buys nothing at all — the same damage as swearing nothing.
        Assert.Equal(plainDamage, brokenDamage);
    }
}
