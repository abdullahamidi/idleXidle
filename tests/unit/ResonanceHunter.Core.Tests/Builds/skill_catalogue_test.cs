using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The twelve skills, pinned (design/gdd/skill-slots-and-skill-trees.md §5, §6).
/// </summary>
/// <remarks>
/// <para>
/// Two per style, one active and one passive, and they are DISTINCT ABILITIES rather than two faces
/// of one. <see cref="Style"/> carries identity and nothing else; <see cref="SkillDef"/> carries
/// behaviour. If a later change lets behaviour leak onto the style, or lets a style own two actives,
/// these fail.
/// </para>
/// <para>
/// The catalogue is data, and data that nothing checks is how this project's characteristic failure
/// starts. These tests are the check: they run before the fight is asked to rely on any of it.
/// </para>
/// </remarks>
public class SkillCatalogueTests
{
    private static IEnumerable<string> EveryName(SkillDef d)
    {
        yield return d.Name;
        foreach (var v in d.Variations)
        {
            yield return v.Name;
            foreach (var r in v.Reinforcements) yield return r.Name;
        }
    }

    [Fact]
    public void test_every_style_owns_exactly_one_active_and_one_passive()
    {
        Assert.Equal(6, Enum.GetValues<Style>().Length);
        Assert.Equal(12, SkillCatalogue.All.Count);

        foreach (var style in Enum.GetValues<Style>())
        {
            var owned = SkillCatalogue.All.Where(s => s.Style == style).ToList();
            Assert.True(owned.Count == 2, $"{style} owns {owned.Count} skills; it must own exactly two.");
            Assert.Single(owned, s => s.TakesABeat);
            Assert.Single(owned, s => !s.TakesABeat);
            Assert.Equal(SkillCatalogue.ActiveOf(style), owned.Single(s => s.TakesABeat));
            Assert.Equal(SkillCatalogue.PassiveOf(style), owned.Single(s => !s.TakesABeat));
        }
    }

    [Fact]
    public void test_a_styles_two_skills_are_different_abilities()
    {
        // The whole reason the two-faces model was replaced. If a style's passive is its active with
        // the beat removed, the player is carrying an abstraction rather than a second ability.
        foreach (var style in Enum.GetValues<Style>())
        {
            var a = SkillCatalogue.ActiveOf(style);
            var p = SkillCatalogue.PassiveOf(style);
            Assert.NotEqual(a.Id, p.Id);
            Assert.NotEqual(a.Name, p.Name);
            Assert.NotEqual(a.Line, p.Line);
        }
    }

    [Fact]
    public void test_ids_are_unique_and_safe_to_persist()
    {
        Assert.Equal(12, SkillCatalogue.All.Select(s => s.Id).Distinct().Count());
        Assert.All(SkillCatalogue.All, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.Equal(s.Id.ToLowerInvariant(), s.Id);   // saves never carry display text
            Assert.DoesNotContain(' ', s.Id);
            Assert.Equal(SkillCatalogue.ById(s.Id), s);
        });
    }

    [Fact]
    public void test_only_the_active_skill_costs_a_beat()
    {
        // The load-bearing guarantee of the whole rework: two passive slots are free ONLY because a
        // passive structurally cannot take the champion's action.
        foreach (var d in SkillCatalogue.All)
        {
            Assert.Equal(d.Kind == SkillKind.Active, d.TakesABeat);
            if (!d.TakesABeat) Assert.True(d.Kind is SkillKind.Field or SkillKind.Reaction);
        }
    }

    [Fact]
    public void test_a_skill_carries_the_timing_its_kind_needs_and_no_other()
    {
        foreach (var d in SkillCatalogue.All)
            switch (d.Kind)
            {
                case SkillKind.Active:
                    Assert.True(d.Beats > 0, $"{d.Name} is Active and must count beats.");
                    Assert.Equal(0, d.IntervalMs);
                    Assert.Equal(ReactionOn.None, d.On);
                    break;
                case SkillKind.Field:
                    Assert.Equal(0, d.Beats);
                    Assert.True(d.IntervalMs > 0, $"{d.Name} is a Field and must have a clock.");
                    // ExpeditionTuning.TickMs is 100 and the tick loop is gated on a modulo. A clock
                    // that is not a multiple of it never fires at all, and nothing would say so.
                    Assert.True(d.IntervalMs % 100 == 0,
                        $"{d.Name} ticks every {d.IntervalMs}ms; the sim's modulo would never land.");
                    Assert.Equal(ReactionOn.None, d.On);
                    break;
                case SkillKind.Reaction:
                    Assert.Equal(0, d.Beats);
                    Assert.Equal(0, d.IntervalMs);
                    Assert.NotEqual(ReactionOn.None, d.On);
                    break;
            }
    }

    [Fact]
    public void test_no_active_cooldown_outlives_a_wave()
    {
        // Waves run 6-15 seconds; a cooldown longer than one can never fire. The invariant
        // test_cooldowns_are_sized_to_a_wave_not_to_each_other pins for the Form table, kept here so
        // a new skill cannot reintroduce it.
        foreach (var d in SkillCatalogue.All.Where(s => s.TakesABeat))
            Assert.True(d.Beats <= 6, $"{d.Name} waits {d.Beats} beats, which outlives a wave.");
    }

    [Fact]
    public void test_volley_still_fires_more_often_than_hammer()
    {
        var hammer = SkillCatalogue.ActiveOf(Style.Hammer);
        var volley = SkillCatalogue.ActiveOf(Style.Volley);
        Assert.True(volley.Beats < hammer.Beats,
            $"{volley.Name} waits {volley.Beats} beats and {hammer.Name} {hammer.Beats}; the light style must fire more often.");
        Assert.True(volley.Targets > hammer.Targets, "VOLLEY must reach more creatures than HAMMER.");
    }

    [Theory]
    [InlineData(Style.Hammer, Style.Volley)]
    [InlineData(Style.Snare, Style.Field)]
    [InlineData(Style.Sign, Style.Drain)]
    public void test_the_ring_puts_the_right_styles_opposite(Style a, Style b)
    {
        Assert.Equal(3, SkillCatalogue.RingDistance(a, b));
        Assert.Equal(b, SkillCatalogue.Opposite(a));
        Assert.Equal(a, SkillCatalogue.Opposite(b));
    }

    [Fact]
    public void test_ring_distance_is_symmetric_and_bounded()
    {
        foreach (var a in Enum.GetValues<Style>())
        foreach (var b in Enum.GetValues<Style>())
        {
            var d = SkillCatalogue.RingDistance(a, b);
            Assert.Equal(SkillCatalogue.RingDistance(b, a), d);
            Assert.InRange(d, 0, 3);
            Assert.Equal(a == b, d == 0);
        }
    }

    [Fact]
    public void test_every_saved_form_resolves_in_both_slots()
    {
        // The migration bridge. A saved build holds a Form ordinal; every one must resolve, in either
        // slot, or a player's build is silently unwoven on load.
        foreach (var form in Enum.GetValues<Form>())
        {
            var active = SkillCatalogue.Resolve(form, passive: false);
            var passive = SkillCatalogue.Resolve(form, passive: true);
            Assert.NotNull(active);
            Assert.NotNull(passive);
            Assert.False(passive.TakesABeat, $"{form} in a passive slot resolved to something that costs a beat.");
            // Both stay inside the style: a spill changes the ABILITY, never the identity.
            Assert.Equal(active.Style, passive.Style);
        }
    }

    [Fact]
    public void test_a_spilled_active_becomes_its_styles_passive_skill()
    {
        // Under the two-faces design an overflowing active took the passive face of the SAME skill.
        // With distinct skills it becomes a different ability of the same style — which is why the
        // spill has to be resolved here rather than by a flag on the equipped skill.
        var strike = SkillCatalogue.Resolve(Form.Strike, passive: false);
        Assert.True(strike.TakesABeat);
        var spilled = SkillCatalogue.Resolve(Form.Strike, passive: true);
        Assert.Equal(Style.Hammer, spilled.Style);
        Assert.False(spilled.TakesABeat);
        Assert.NotEqual(strike.Id, spilled.Id);

        // AURA and TRAP were never actives, so they resolve to their own passive whichever is asked.
        foreach (var f in new[] { Form.Aura, Form.Trap })
        {
            Assert.False(SkillCatalogue.Resolve(f, passive: false).TakesABeat);
            Assert.Equal(SkillCatalogue.Resolve(f, passive: false), SkillCatalogue.Resolve(f, passive: true));
        }
    }

    [Fact]
    public void test_every_skill_has_two_variations_of_three_reinforcements()
    {
        foreach (var d in SkillCatalogue.All)
        {
            Assert.True(d.Variations.Count == 2,
                $"{d.Name} offers {d.Variations.Count} variations; the shape is two, and one of them must be refused.");
            foreach (var v in d.Variations)
            {
                Assert.Equal(3, v.Reinforcements.Count);
                Assert.False(string.IsNullOrWhiteSpace(v.Line));
                Assert.All(v.Reinforcements, r => Assert.False(string.IsNullOrWhiteSpace(r.Line)));
            }
            // A variation that reads the same as its sibling is not a fork.
            Assert.NotEqual(d.Variations[0].Line, d.Variations[1].Line);
        }
    }

    [Fact]
    public void test_every_name_in_the_catalogue_is_unique()
    {
        // 108 entries. Two of them sharing a word is how a player ends up unable to say which one
        // they took — the audit that produced this list found WAKE used four times for four
        // unrelated mechanics.
        var names = SkillCatalogue.All.SelectMany(EveryName).ToList();
        var dups = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dups.Count == 0, "repeated names: " + string.Join(", ", dups));
        Assert.Equal(12 * (1 + 2 + 6), names.Count);
    }

    [Fact]
    public void test_no_name_is_a_style_name_or_uses_the_champion_form()
    {
        // "THE X" is the champion naming scheme (THE QUIVER, THE ANVIL...), and a node named after a
        // style reads as the style itself on an adjacent screen.
        var styles = Enum.GetValues<Style>().Select(s => s.ToString().ToUpperInvariant()).ToHashSet();
        foreach (var n in SkillCatalogue.All.SelectMany(EveryName))
        {
            Assert.False(n.StartsWith("THE ", StringComparison.Ordinal), $"{n} uses the champion naming form.");
            Assert.DoesNotContain(n, styles);
        }
    }

    [Fact]
    public void test_every_skill_names_its_art_and_says_what_it_does()
    {
        Assert.All(SkillCatalogue.All, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.ClipKey));
            Assert.False(string.IsNullOrWhiteSpace(s.FxKey));
            Assert.False(string.IsNullOrWhiteSpace(s.Line));
        });
    }
}
