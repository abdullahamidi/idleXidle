using System;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// ACTIVE and PASSIVE are two budgets, not one. Stage 2 of the skill-slot rework
/// (design/gdd/skill-slots-and-skill-trees.md §11).
/// </summary>
/// <remarks>
/// <para>
/// An active costs the champion its action; a passive never can. That is the structural guarantee
/// the whole rework rests on — two passive slots are free only because a Field ticks on its own
/// clock above the beat gate and a Reaction answers an event. These tests pin the guarantee at the
/// build layer, before <c>SoloBattle</c> is asked to rely on it.
/// </para>
/// <para>
/// <b>The capacities are enforced from the moment they exist.</b> A capacity nothing checks is this
/// codebase's signature failure: FIFTH WEAVE was bought, persisted, resolved and displayed, then
/// clamped off at the last step by a constant nobody had updated.
/// </para>
/// </remarks>
public class SkillSlotKindTests
{
    private static EquippedSkill Skill(Form form, bool passive = false, string? name = null)
    {
        var def = SkillCatalogue.ForLegacy(form);
        return new EquippedSkill(
            new WovenAbility { Name = name ?? def.Face(passive).Name, Source = Source.Body, Form = form },
            CooldownMs: 1000,
            Passive: passive);
    }

    [Fact]
    public void test_a_slot_chooses_the_face_and_the_face_decides_the_beat()
    {
        var active = Skill(Form.Strike);
        var passive = Skill(Form.Strike, passive: true);

        Assert.True(active.TakesABeat);
        Assert.False(passive.TakesABeat);
        // Same skill, same style, same art — the slot is the only difference.
        Assert.Equal(active.Def.Id, passive.Def.Id);
        Assert.Equal(active.Def.ClipKey, passive.Def.ClipKey);
        Assert.NotEqual(active.Face.Name, passive.Face.Name);
    }

    [Fact]
    public void test_every_legacy_form_resolves_to_a_catalogue_entry()
    {
        // A saved build carries a Form ordinal. If any one of them failed to resolve, the player's
        // build would be silently unwoven on load.
        foreach (var form in Enum.GetValues<Form>())
            Assert.Equal(form, Skill(form).Def.LegacyForm);
    }

    [Fact]
    public void test_actives_and_passives_are_counted_separately()
    {
        var b = new Build();
        b.Weave(Skill(Form.Strike, name: "a"));
        b.Weave(Skill(Form.Projectile, name: "b"));
        b.Weave(Skill(Form.Aura, passive: true, name: "c"));

        Assert.Equal(2, b.ActiveCount);
        Assert.Equal(1, b.PassiveCount);
        Assert.Equal(3, b.Skills.Count);
    }

    [Fact]
    public void test_a_full_active_budget_does_not_block_a_passive()
    {
        var b = new Build { ActiveCapacity = 2, PassiveCapacity = 2 };
        Assert.True(b.Weave(Skill(Form.Strike, name: "a")));
        Assert.True(b.Weave(Skill(Form.Projectile, name: "b")));

        // Actives are full; a third active is refused...
        Assert.False(b.Weave(Skill(Form.Mark, name: "c")));
        // ...but the passive budget is untouched, which is the entire point of splitting them.
        Assert.True(b.Weave(Skill(Form.Aura, passive: true, name: "d")));
        Assert.True(b.Weave(Skill(Form.Trap, passive: true, name: "e")));
        Assert.False(b.Weave(Skill(Form.Transformation, passive: true, name: "f")));

        Assert.Equal(2, b.ActiveCount);
        Assert.Equal(2, b.PassiveCount);
    }

    [Fact]
    public void test_an_unset_capacity_is_the_whole_budget()
    {
        // Today's behaviour, preserved exactly: until stage 2b sets the per-kind caps, a build's
        // budget is undivided. A fifth slot sold by the trait spine must not be clamped off by a
        // per-kind cap frozen at the old constant.
        var b = new Build { SlotCapacity = 5 };
        for (var i = 0; i < 5; i++)
            Assert.True(b.Weave(Skill(Form.Strike, name: $"s{i}")), $"slot {i + 1} of 5 was refused");
        Assert.Equal(5, b.Skills.Count);
    }

    [Fact]
    public void test_beat_demand_is_what_the_rework_exists_to_lower()
    {
        // Four beat-taking skills is the state that made the basic attack disappear: the swing only
        // lands when no skill claimed the beat, so ~0.8 demand leaves ~1 beat in 5 for it.
        var four = new Build { ActiveCapacity = 4 };
        four.Weave(Skill(Form.Strike, name: "a"));
        four.Weave(Skill(Form.Projectile, name: "b"));
        four.Weave(Skill(Form.Mark, name: "c"));
        four.Weave(Skill(Form.Transformation, name: "d"));
        Assert.True(four.BeatDemand > 0.75f, $"four actives demanded only {four.BeatDemand:0.00}");

        // Two actives plus two passives: the passives add NOTHING, by construction.
        var two = new Build { ActiveCapacity = 2, PassiveCapacity = 2 };
        two.Weave(Skill(Form.Strike, name: "a"));
        two.Weave(Skill(Form.Projectile, name: "b"));
        var withoutPassives = two.BeatDemand;
        two.Weave(Skill(Form.Aura, passive: true, name: "c"));
        two.Weave(Skill(Form.Trap, passive: true, name: "d"));

        Assert.Equal(withoutPassives, two.BeatDemand);
        Assert.True(two.BeatDemand < 0.5f,
            $"two actives demanded {two.BeatDemand:0.00}; the swing must keep more than half the beats");
    }

    [Fact]
    public void test_the_worst_pair_still_leaves_the_swing_most_of_the_beats()
    {
        // The two shortest cooldowns woven together is the worst case a player can build. Even that
        // must leave the champion's own swing the majority of its beats, or the rework has not
        // actually fixed the thing it exists for.
        var shortest = SkillCatalogue.All
            .OrderBy(s => s.Active.Beats).Take(2).ToList();

        var b = new Build { ActiveCapacity = 2 };
        foreach (var def in shortest)
            b.Weave(Skill(def.LegacyForm, name: def.Id));

        Assert.True(b.BeatDemand < 0.5f,
            $"the worst active pair ({string.Join(" + ", shortest.Select(s => s.Active.Name))}) " +
            $"demands {b.BeatDemand:0.00} of the beats");
    }
}
