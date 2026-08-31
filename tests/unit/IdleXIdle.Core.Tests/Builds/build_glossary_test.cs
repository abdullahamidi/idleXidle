using System;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The glossary must not lie.
/// </summary>
/// <remarks>
/// Explanatory text is the one kind of content that is worse than useless when it drifts. A missing
/// description costs a player a run of experimenting; a WRONG one costs them the build they planned
/// around it, and they have no way to tell the difference until it has already failed. So every claim
/// the glossary makes is checked against the rule it claims to describe.
/// </remarks>
public class BuildGlossaryTest
{
    private readonly ITestOutputHelper _out;

    public BuildGlossaryTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_the_strong_and_weak_claims_match_what_the_fight_actually_does()
    {
        var t = WeavingTuning.Default;

        foreach (var attacker in Enum.GetValues<Source>())
        {
            var (s1, s2) = BuildGlossary.StrongAgainst(attacker);
            var (w1, w2) = BuildGlossary.WeakAgainst(attacker);

            foreach (var target in new[] { s1, s2 })
                Assert.True(SourceMatchup.Effectiveness(attacker, target) > 1f,
                    $"the glossary says {attacker} is strong against {target}, and the fight disagrees.");

            foreach (var target in new[] { w1, w2 })
                Assert.True(SourceMatchup.Effectiveness(attacker, target) < 1f,
                    $"the glossary says {attacker} is weak against {target}, and the fight disagrees.");

            // And the two it names must be the ONLY ones, or the line is true and still misleading.
            var actuallyStrong = Enum.GetValues<Source>()
                .Where(x => SourceMatchup.Effectiveness(attacker, x) > 1f).ToList();
            Assert.Equal(2, actuallyStrong.Count);
            Assert.Contains(s1, actuallyStrong);
            Assert.Contains(s2, actuallyStrong);
        }
    }

    [Fact]
    public void test_every_form_is_described_and_the_numbers_are_the_real_ones()
    {
        _out.WriteLine("THE FORMS, as the player will read them:");
        foreach (var form in Enum.GetValues<Form>())
        {
            var headline = BuildGlossary.FormHeadline(form);
            var rule = BuildGlossary.FormRule(form);

            _out.WriteLine($"   {form,-16} {headline}");
            _out.WriteLine($"                    {rule}");

            Assert.False(string.IsNullOrWhiteSpace(headline), $"{form} has no headline");
            Assert.False(string.IsNullOrWhiteSpace(rule), $"{form} has no rule line");

            // The cooldown is quoted in seconds; it must be the cooldown the sim uses. A description
            // with a hand-typed number is a second copy of the design, and the copy is what goes stale.
            var cd = FormBehaviour.BaseCooldownMs(form) / 1000f;
            if (!FormBehaviour.IsPassive(form))
                Assert.Contains($"{cd:0.#}s", rule);
        }
    }

    [Fact]
    public void test_the_form_that_deals_no_damage_says_so()
    {
        // MARK is the single most misreadable pick in the game: it is the only Form that deals nothing,
        // and a player who slots four of them has built a champion that cannot kill anything. The
        // parity harness made exactly this mistake with a four-Mark build before the glossary existed.
        var rule = BuildGlossary.FormRule(Form.Mark);
        Assert.Contains("NO damage", rule);
        Assert.Contains($"{FormBehaviour.MarkMultiplier:0.0}x", rule);
    }

    [Fact]
    public void test_the_form_that_only_pays_when_attacked_says_so()
    {
        // TRAP is the other trap. Against a boss that swings rarely it is close to dead weight, and
        // nothing on screen said so — the player just watched a slot do nothing and blamed the game.
        Assert.Contains("ONLY pays when the enemy attacks", BuildGlossary.FormRule(Form.Trap));
        Assert.True(FormBehaviour.FiresOnBeingHit(Form.Trap));
    }

    [Fact]
    public void test_an_items_trait_effect_states_its_real_numbers()
    {
        // The trait line on the gear panel is now the computed effect rather than the blurb, which
        // means it is an assertion about the item and can be wrong. It must agree with the mods the
        // fight will actually apply — a stated "+60% DMG" that the sim does not honour is worse than
        // the vague sentence it replaced, because the player will build around it.
        foreach (var rarity in new[] { Rarity.Common, Rarity.Rare, Rarity.Legendary })
        foreach (var level in new[] { 1, 20, 200 })
        {
            var item = new ItemInstance
            {
                InstanceId = "trait_probe",
                BaseType = ItemBaseType.Weapon,
                Rarity = rarity,
                ItemLevel = level,
                SellValue = 10,
                TraitOverride = GearTrait.Heavy,   // the prefix is mint data now, so the probe sets one
            };

            var trait = GearTraits.TraitOf(item)!.Value;
            var mods = GearTraits.ModsFor(trait, rarity, level);
            var text = GearTraits.EffectOf(item);

            _out.WriteLine($"   {rarity,-10} iL{level,-4} {trait,-8} {text}");

            // Every channel that actually moved must appear, and nothing that did not may. The labels
            // come FROM GearTraits rather than being retyped here: a local copy would still pass the
            // day someone renames a channel, because the positive case would look for a word that is no
            // longer in the string and the negative case asserts exactly that absence.
            var moved = new[] { mods.Damage, mods.Health, mods.SkillRate, mods.Haul }
                .Select((value, i) => (Label: GearTraits.Channels[i], Value: value));

            foreach (var (label, value) in moved)
            {
                var pct = (value - 1f) * 100f;
                if (MathF.Abs(pct) >= 0.5f)
                    Assert.Contains(label, text);
                else
                    Assert.DoesNotContain($" {label}", text);
            }
        }
    }

    [Fact]
    public void test_every_source_line_names_the_regions_lever()
    {
        foreach (var source in Enum.GetValues<Source>())
        {
            var line = BuildGlossary.SourceRule(source);
            _out.WriteLine($"   {source,-10} {line}");
            Assert.Contains("region", line, StringComparison.OrdinalIgnoreCase);
        }
    }
}
