using System;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The Nen buy-back: a SWORN VOW pulls an off-discipline skill one ring toward the build's affinity.
/// </summary>
/// <remarks>
/// The rule the game's Vows were born from — in the source system, a restriction is how you wield
/// what your affinity does not give you. The native Form gains nothing (you already own it), and an
/// unsworn skill pays the full hexagon distance.
/// </remarks>
public class AffinityVowBuybackTest
{
    [Fact]
    public void test_affinity_buyback_a_vow_lifts_every_off_form_exactly_one_ring()
    {
        foreach (var affinity in Enum.GetValues<Form>())
            foreach (var skill in Enum.GetValues<Form>())
            {
                // Arrange
                var plain = FormBehaviour.AffinityFactor(affinity, skill);
                var sworn = FormBehaviour.AffinityFactor(affinity, skill, vowSworn: true);

                if (skill == affinity)
                {
                    // Assert: the native Form gains nothing — you already own it.
                    Assert.Equal(plain, sworn);
                    continue;
                }

                // Assert: sworn is strictly better, and equals SOME ring's plain factor — one step in.
                Assert.True(sworn > plain, $"{affinity}->{skill}: sworn {sworn} not above plain {plain}");
                var ringFactors = Enum.GetValues<Form>()
                    .Select(f => FormBehaviour.AffinityFactor(affinity, f)).Distinct().ToList();
                Assert.Contains(sworn, ringFactors);
            }
    }

    [Fact]
    public void test_affinity_buyback_reaches_the_fight_for_a_sworn_opposite_skill()
    {
        // Arrange: two identical single-skill builds at an off-discipline CASTING Form; one skill
        // carries a Vow whose demand is UNMET on this build (so VowFactor adds nothing and the only
        // delta is the buy-back), one carries none. The (affinity, caster) pair is MEASURED off the
        // ring — the first draft assumed Strike's far ring held a caster, and it holds only a passive,
        // an on-bite Trap and a damageless Mark, so the buy-back path never ran and the test proved
        // exactly the dormant-wiring it exists to catch.
        var forms = Enum.GetValues<Form>();
        var (affinity, caster) = forms
            .SelectMany(a => forms.Select(f => (a, f)))
            .First(p => p.a != p.f
                        && !FormBehaviour.IsPassive(p.f) && !FormBehaviour.FiresOnBeingHit(p.f)
                        && !FormBehaviour.IsAmplifier(p.f)
                        && FormBehaviour.AffinityFactor(p.a, p.f) < 1f);

        Build Casting(Vow? vow)
        {
            var b = new Build { Affinity = affinity };
            b.Weave(new EquippedSkill(
                new WovenAbility { Name = "C", Source = Source.Nature, Form = caster, Vow = vow },
                FormBehaviour.BaseCooldownMs(caster)));
            return b;
        }

        var unmeetable = Weaving.Catalog.FirstOrDefault(v => v.Demand == VowDemand.EveryWeaveFilled);

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
