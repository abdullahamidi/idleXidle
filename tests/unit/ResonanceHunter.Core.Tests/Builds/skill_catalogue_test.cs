using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The STYLE / SKILL split, pinned. Stage 1 of the skill-slot rework
/// (design/gdd/skill-slots-and-skill-trees.md §3, §11).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Form"/> was doing four jobs at once — identity, mastery destination, behaviour and
/// damage. This file pins the two halves apart: <see cref="Style"/> carries identity and nothing
/// else, <see cref="SkillDef"/> carries behaviour. If a later change lets behaviour leak back onto
/// the style, or lets a style own two skills, these tests fail.
/// </para>
/// <para>
/// <b>Stage 1 is additive on purpose.</b> Nothing in the fight reads this catalogue yet; the point
/// of these tests is that the data layer is correct BEFORE <c>SoloBattle</c> is asked to run on it,
/// because this codebase's signature failure is built-and-green code that nothing executes.
/// </para>
/// </remarks>
public class SkillCatalogueTests
{
    [Fact]
    public void test_every_style_has_exactly_one_skill()
    {
        var styles = Enum.GetValues<Style>();
        Assert.Equal(6, styles.Length);
        Assert.Equal(styles.Length, SkillCatalogue.All.Count);

        foreach (var style in styles)
        {
            var owned = SkillCatalogue.All.Where(s => s.Style == style).ToList();
            Assert.True(owned.Count == 1,
                $"{style} owns {owned.Count} skills; a style must own exactly one, or 'how much does " +
                "this road strengthen it' has no single answer — which is how the balance knotted.");
        }
    }

    [Fact]
    public void test_ids_and_names_are_unique_and_stable()
    {
        Assert.Equal(SkillCatalogue.All.Count, SkillCatalogue.All.Select(s => s.Id).Distinct().Count());
        Assert.Equal(SkillCatalogue.All.Count, SkillCatalogue.All.Select(s => s.Name).Distinct().Count());
        Assert.All(SkillCatalogue.All, s => Assert.False(string.IsNullOrWhiteSpace(s.Id)));
        // Ids are persisted in saves. Lower-case, no spaces, so a save file never carries display text.
        Assert.All(SkillCatalogue.All, s => Assert.Equal(s.Id.ToLowerInvariant(), s.Id));
        Assert.All(SkillCatalogue.All, s => Assert.DoesNotContain(' ', s.Id));
    }

    [Fact]
    public void test_every_legacy_form_maps_to_exactly_one_skill()
    {
        // The migration bridge. A saved build holds a Form ordinal; every one of them must resolve,
        // or a player's build is silently unwoven on load.
        foreach (var form in Enum.GetValues<Form>())
        {
            var def = SkillCatalogue.ForLegacy(form);
            Assert.Equal(form, def.LegacyForm);
        }
        Assert.Equal(6, SkillCatalogue.All.Select(s => s.LegacyForm).Distinct().Count());
    }

    [Fact]
    public void test_only_the_active_face_costs_a_beat()
    {
        // The load-bearing guarantee of the whole rework. Two passive slots are free ONLY because a
        // passive face structurally cannot take the champion's action; if one ever could, the beat
        // demand climbs back toward the 0.80 that made the basic attack disappear.
        foreach (var def in SkillCatalogue.All)
        {
            Assert.True(def.Active.TakesABeat, $"{def.Name}'s active face must take a beat.");
            Assert.False(def.Passive.TakesABeat,
                $"{def.Name}'s passive face takes a beat. A passive that costs an action is an active " +
                "wearing a different name, and it re-creates the bug this rework exists to fix.");
            Assert.True(def.Passive.Kind is SkillKind.Field or SkillKind.Reaction);
        }
    }

    [Fact]
    public void test_a_face_carries_the_timing_its_kind_needs_and_no_other()
    {
        foreach (var def in SkillCatalogue.All)
        foreach (var face in new[] { def.Active, def.Passive })
        {
            switch (face.Kind)
            {
                case SkillKind.Active:
                    Assert.True(face.Beats > 0, $"{face.Name} is Active and must count beats.");
                    Assert.Equal(0, face.IntervalMs);
                    Assert.Equal(ReactionOn.None, face.On);
                    break;
                case SkillKind.Field:
                    Assert.Equal(0, face.Beats);
                    Assert.True(face.IntervalMs > 0, $"{face.Name} is a Field and must have a clock.");
                    Assert.Equal(ReactionOn.None, face.On);
                    break;
                case SkillKind.Reaction:
                    Assert.Equal(0, face.Beats);
                    Assert.Equal(0, face.IntervalMs);
                    Assert.NotEqual(ReactionOn.None, face.On);
                    break;
            }
        }
    }

    [Fact]
    public void test_a_field_clock_lands_on_the_sims_tick()
    {
        // ExpeditionTuning.TickMs is 100 and the tick loop is gated on a modulo. A Field whose clock
        // is not a multiple of it never fires at all — which is precisely the dormant-feature failure
        // this project keeps hitting, and it would be invisible until someone watched a fight.
        foreach (var def in SkillCatalogue.All)
        foreach (var face in new[] { def.Active, def.Passive })
            if (face.Kind == SkillKind.Field)
                Assert.True(face.IntervalMs % 100 == 0,
                    $"{face.Name} ticks every {face.IntervalMs}ms, which is not a multiple of the " +
                    "sim's 100ms tick, so its modulo would never land.");
    }

    [Fact]
    public void test_no_active_cooldown_outlives_a_wave()
    {
        // Waves run 6-15 seconds. A cooldown longer than a wave can never fire — the invariant
        // test_cooldowns_are_sized_to_a_wave_not_to_each_other already pins for the Form table, kept
        // here so the new catalogue cannot reintroduce it.
        const int ceilingBeats = 6;
        foreach (var def in SkillCatalogue.All)
            Assert.True(def.Active.Beats <= ceilingBeats,
                $"{def.Active.Name} waits {def.Active.Beats} beats; anything over {ceilingBeats} " +
                "outlives a wave and would never fire.");
    }

    [Fact]
    public void test_volley_still_fires_more_often_than_hammer()
    {
        // The other pinned invariant, carried across the rename: VOLLEY is the light style and must
        // out-pace HAMMER, or "volume over weight" is a sentence with no mechanic under it.
        var hammer = SkillCatalogue.Of(Style.Hammer).Active;
        var volley = SkillCatalogue.Of(Style.Volley).Active;
        Assert.True(volley.Beats < hammer.Beats,
            $"VOLLEY waits {volley.Beats} beats and HAMMER {hammer.Beats}; the light style must fire more often.");
        Assert.True(volley.Targets > hammer.Targets, "VOLLEY must reach more creatures than HAMMER.");
    }

    [Theory]
    [InlineData(Style.Hammer, Style.Volley)]
    [InlineData(Style.Snare, Style.Field)]
    [InlineData(Style.Sign, Style.Drain)]
    public void test_the_ring_puts_the_right_styles_opposite(Style a, Style b)
    {
        // The corrected ring (§4). Form's order had Strike opposite Trap while the mastery tree put
        // both in Weight — the tree said they deepen together and the hexagon said they fight.
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
    public void test_every_skill_names_its_art()
    {
        // A skill with no clip key draws nothing and the champion stands still through its own cast.
        Assert.All(SkillCatalogue.All, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.ClipKey));
            Assert.False(string.IsNullOrWhiteSpace(s.FxKey));
        });
    }

    [Fact]
    public void test_face_names_are_unique_across_the_whole_catalogue()
    {
        // Twelve faces, twelve names. Two faces sharing a word is how a player ends up unable to say
        // which one they took — the same audit that caught SAP in three trees at once.
        var names = SkillCatalogue.All.SelectMany(s => new[] { s.Active.Name, s.Passive.Name }).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void test_face_picks_the_right_half()
    {
        foreach (var def in SkillCatalogue.All)
        {
            Assert.Equal(def.Active, def.Face(passive: false));
            Assert.Equal(def.Passive, def.Face(passive: true));
        }
    }
}
