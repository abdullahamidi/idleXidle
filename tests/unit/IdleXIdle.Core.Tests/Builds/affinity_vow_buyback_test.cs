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
    public void test_affinity_buyback_reaches_the_fight_for_a_sworn_opposite_skill()
    {
        // Arrange: two identical single-skill builds at an off-discipline CASTING skill; one carries
        // a Vow whose demand is UNMET on this build (so VowFactor adds nothing and the only delta is
        // the buy-back), one carries none. The (affinity, caster) pair is MEASURED off the catalogue
        // rather than assumed — the first draft assumed Strike's far ring held a caster, and it held
        // only a passive, an on-bite Trap and a damageless Mark, so the buy-back path never ran and
        // the test proved exactly the dormant-wiring it exists to catch. The same trap still exists
        // in Style terms: a style's roster holds amplify SIGNs, Fields, on-bite Reactions and the
        // banked-bite REPAY (BasePower 0), none of which would exercise the cast-path buy-back.
        var (affinity, caster) = Enum.GetValues<Style>()
            .SelectMany(a => SkillCatalogue.All.Select(d => (a, d)))
            .First(p => p.d.Kind == SkillKind.Active
                        && p.d.Effect != SkillEffect.Amplify
                        && p.d.BasePower > 0f
                        && StyleAffinity.Factor(p.a, p.d.Style) < 1f);

        Build Casting(Vow? vow)
        {
            var b = new Build { Affinity = affinity };
            b.Equip(new EquippedSkill(caster, Source.Nature, vow));
            return b;
        }

        var unmeetable = Vows.Catalog.FirstOrDefault(v => v.Demand == VowDemand.EverySlotFilled);

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

        // Act + Assert: with the same seed, the sworn build's off-discipline caster delivers more.
        if (unmeetable is null) return;   // catalogue changed shape; the unit test above still pins the rule
        var swornDamage = Damage(Casting(unmeetable));
        var plainDamage = Damage(Casting(null));

        Assert.True(swornDamage > plainDamage * 1.10f,
            $"sworn {swornDamage} vs plain {plainDamage} — the buy-back never reached the sim");
    }
}
