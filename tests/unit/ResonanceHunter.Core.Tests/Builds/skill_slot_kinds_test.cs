using System;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Prestige;
using ResonanceHunter.Core.Expeditions;
using Xunit.Abstractions;
using System.Collections.Generic;
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
            PassiveSlot: passive);
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

/// <summary>
/// The live composition path, where the slot split is actually applied. Stage 2b of the rework.
/// </summary>
/// <remarks>
/// The tests above build a <see cref="Build"/> bare, which keeps the undivided budget on purpose so
/// a test about damage need not learn the slot rules. That means NOTHING above would notice if the
/// split were never applied to a real player's build — which is exactly the dormant-feature shape
/// this codebase keeps producing. These pin the composer instead.
/// </remarks>
public class ComposedSlotSplitTests
{
    private static BuildComposer.SkillPick S(string name, Form form)
        => new(Source.Body, form, null, name);

    private static Build Compose(int slots, params BuildComposer.SkillPick[] skills)
        => BuildComposer.Compose(new MemoryDustTree(), new MasteryTree(), character: null,
                                 skills: skills, keystoneIds: Array.Empty<string>(), slotCapacity: slots);

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 1, 1)]
    [InlineData(3, 2, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(5, 3, 2)]
    public void test_slots_unlock_active_passive_active_passive(int total, int actives, int passives)
    {
        Assert.Equal(actives, Build.ActiveSlotsFor(total));
        Assert.Equal(passives, Build.PassiveSlotsFor(total));
        Assert.Equal(total, Build.ActiveSlotsFor(total) + Build.PassiveSlotsFor(total));
    }

    [Fact]
    public void test_a_composed_build_gets_two_active_slots_not_four()
    {
        var b = Compose(4,
            S("a", Form.Strike), S("b", Form.Projectile),
            S("c", Form.Mark), S("d", Form.Transformation));

        Assert.Equal(2, b.ActiveCapacity);
        Assert.Equal(2, b.PassiveCapacity);
        Assert.Equal(2, b.ActiveCount);
    }

    [Fact]
    public void test_an_old_four_active_build_keeps_all_four_skills()
    {
        // The migration that matters. Before the rework a player could weave four beat-taking
        // skills; the active budget is two now. Dropping the overflow would take half of someone's
        // build away on load without a word, so it spills into the passive slots instead.
        var b = Compose(4,
            S("a", Form.Strike), S("b", Form.Projectile),
            S("c", Form.Mark), S("d", Form.Transformation));

        Assert.Equal(4, b.Skills.Count);
        Assert.Equal(new[] { "a", "b", "c", "d" }, b.Skills.Select(s => s.Name).ToArray());
        // The two woven FIRST keep their actives; any other rule reorders the player's build for them.
        Assert.Equal(new[] { "a", "b" }, b.Skills.Where(s => s.TakesABeat).Select(s => s.Name).ToArray());
        Assert.Equal(new[] { "c", "d" }, b.Skills.Where(s => !s.TakesABeat).Select(s => s.Name).ToArray());
    }

    [Fact]
    public void test_the_composed_build_leaves_the_swing_most_of_its_beats()
    {
        // The whole point, measured on the path a real player actually takes. Four beat-taking
        // skills demanded about 0.80 and the basic attack only lands on what is left over.
        var b = Compose(4,
            S("a", Form.Strike), S("b", Form.Projectile),
            S("c", Form.Mark), S("d", Form.Transformation));

        Assert.True(b.BeatDemand < 0.5f,
            $"a full composed build still demands {b.BeatDemand:0.00} of the beats");
    }

    [Fact]
    public void test_aura_and_trap_still_cost_no_beat_after_the_split()
    {
        // They never did, and the split must not have quietly promoted them into the active budget:
        // if it had, a classic Aura+Trap build would now be spending actions it never spent.
        var b = Compose(4,
            S("a", Form.Aura), S("b", Form.Trap),
            S("c", Form.Strike), S("d", Form.Projectile));

        Assert.Equal(4, b.Skills.Count);
        Assert.Equal(2, b.ActiveCount);
        Assert.Equal(new[] { "c", "d" }, b.Skills.Where(s => s.TakesABeat).Select(s => s.Name).ToArray());
    }
}

/// <summary>
/// The rework's whole purpose, measured in a real fight rather than derived from the cooldowns.
/// Stage 4 (design/gdd/skill-slots-and-skill-trees.md §11).
/// </summary>
/// <remarks>
/// <c>Build.BeatDemand</c> is arithmetic over the cooldown table and an upper bound: two ready skills
/// contend for one beat and the loser waits. This file runs <see cref="SoloBattle"/> and COUNTS, so
/// that the claim "the champion's own swing came back" is evidence and not a calculation. The
/// playtest that started this work was a person watching the screen and seeing no plain attack at
/// all; the equivalent of that observation belongs in the suite.
/// </remarks>
public class SwingShareTests
{
    private readonly ITestOutputHelper _out;
    public SwingShareTests(ITestOutputHelper output) => _out = output;

    private static BuildComposer.SkillPick S(string name, Form form) => new(Source.Body, form, null, name);

    /// <summary>Actions the champion took, and how many of them were plain swings.</summary>
    private static (int beats, int casts) Cadence(Build build)
    {
        // One enormous creature that cannot kill the champion: the fight has to last long enough to
        // hold many cycles, because a wave that ends early measures one cycle of the cadence.
        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(),
            enemyHealth: 3_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_500,
            ExpeditionTuning.Default, new Random(7));

        var beats = events.Count(e => e.Kind == BattleEventKind.Beat);
        // A cast is a Skill event that claimed the beat. A Reaction never does, so it is not an action.
        var casts = events.Count(e => e.Kind == BattleEventKind.Skill && (Form)e.Amount != Form.Trap);
        return (beats, casts);
    }

    [Fact]
    public void test_four_beat_taking_skills_leave_almost_no_room_for_the_swing()
    {
        // The state the playtest complained about, reproduced: "there are even scenarios with no
        // plain attack at all". This is the BEFORE half of the measurement and it must keep failing
        // to leave room, or the AFTER half proves nothing.
        var before = new Build { ActiveCapacity = 4, PassiveCapacity = 0 };
        foreach (var (n, f) in new[] { ("a", Form.Strike), ("b", Form.Projectile),
                                       ("c", Form.Mark), ("d", Form.Transformation) })
            before.Weave(new EquippedSkill(
                new WovenAbility { Name = n, Source = Source.Body, Form = f },
                FormBehaviour.BaseCooldownMs(f)));

        var (beats, casts) = Cadence(before);
        var swingShare = beats == 0 ? 0f : (beats - casts) / (float)beats;
        _out.WriteLine($"four actives: {beats} actions, {casts} casts, swing share {swingShare:0.00}");

        Assert.True(swingShare < 0.4f,
            $"the four-slot build already left the swing {swingShare:0.00} of its actions, so there " +
            "was nothing for this rework to fix — check the cooldown table before trusting the next assert.");
    }

    [Fact]
    public void test_a_composed_build_gives_the_swing_back_the_majority_of_its_actions()
    {
        var after = BuildComposer.Compose(
            new MemoryDustTree(), new MasteryTree(), character: null,
            skills: new[] { S("a", Form.Strike), S("b", Form.Projectile),
                            S("c", Form.Mark), S("d", Form.Transformation) },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        var (beats, casts) = Cadence(after);
        var swingShare = beats == 0 ? 0f : (beats - casts) / (float)beats;
        _out.WriteLine($"two actives + two passives: {beats} actions, {casts} casts, swing share {swingShare:0.00}");

        Assert.True(beats > 20, $"only {beats} actions were taken; the wave was too short to measure a cadence");
        Assert.True(swingShare > 0.5f,
            $"the champion swung on {swingShare:0.00} of its actions. The rework exists so the plain " +
            "attack is a right and not a leftover, and more than half is the bar.");
    }

    [Fact]
    public void test_the_passives_still_act_even_though_they_take_no_beat()
    {
        // The other half of the promise. Moving two skills into passive slots must not silence them —
        // if it did, the swing would come back only because half the build stopped working.
        var after = BuildComposer.Compose(
            new MemoryDustTree(), new MasteryTree(), character: null,
            skills: new[] { S("a", Form.Strike), S("b", Form.Projectile),
                            S("c", Form.Aura), S("d", Form.Trap) },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, after, new Hunter(),
            enemyHealth: 3_000_000f, enemyDamage: 40f, enemyIntervalMs: 1_500,
            ExpeditionTuning.Default, new Random(7));

        Assert.True(events.Any(e => e.Kind == BattleEventKind.Aura),
            "the woven Field never ticked — a passive slot that costs no beat must still act.");
        Assert.True(events.Any(e => e.Kind == BattleEventKind.Skill && (Form)e.Amount == Form.Trap),
            "the woven Reaction never fired, though the champion was being bitten.");
    }
}

/// <summary>
/// A skill in a passive slot must still DO its thing — not merely stop costing a beat.
/// </summary>
/// <remarks>
/// The slot split made this reachable for the first time: a build saved with four beat-taking skills
/// spills its overflow into the passive slots, so a MARK or a TRANSFORMATION can now arrive at the
/// Field branch. The Field branch only knew how to deal damage, and <c>BaseDamage(Mark)</c> is zero
/// by definition — so a spilled Mark sat in a slot the player could see, doing nothing whatever.
/// That is this codebase's signature failure, and it was introduced by the fix for another one.
/// </remarks>
public class PassiveEffectTests
{
    private static BuildComposer.SkillPick S(string name, Form form) => new(Source.Body, form, null, name);

    private static Build Compose(params BuildComposer.SkillPick[] skills)
        => BuildComposer.Compose(new MemoryDustTree(), new MasteryTree(), character: null,
                                 skills: skills, keystoneIds: Array.Empty<string>(), slotCapacity: 4);

    /// <summary>Total damage the champion put out in one wave, summed from the event stream.</summary>
    private static long DamageOver(Build build, float enemyHealth)
    {
        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(),
            enemyHealth: enemyHealth, enemyDamage: 0f, enemyIntervalMs: 1_500,
            ExpeditionTuning.Default, new Random(3));
        return events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => (long)e.Amount);
    }

    [Fact]
    public void test_a_mark_in_a_passive_slot_still_amplifies()
    {
        // Same two actives either way; the third slot is the variable. If the passive Mark did
        // nothing, these two would deal the same damage.
        var withMark = Compose(S("a", Form.Strike), S("b", Form.Projectile), S("c", Form.Mark));
        var without  = Compose(S("a", Form.Strike), S("b", Form.Projectile));

        Assert.Equal(1, withMark.PassiveCount);
        Assert.False(withMark.Skills.Single(s => s.Name == "c").TakesABeat);

        var amplified = DamageOver(withMark, 2_000_000f);
        var plain = DamageOver(without, 2_000_000f);
        Assert.True(amplified > plain,
            $"a MARK in a passive slot changed nothing: {amplified:0} against {plain:0}. It costs no " +
            "beat, but it must still open its window or it is a slot the player can see doing nothing.");
    }

    [Fact]
    public void test_a_transformation_in_a_passive_slot_still_heals()
    {
        var build = Compose(S("a", Form.Strike), S("b", Form.Projectile), S("c", Form.Transformation));
        Assert.False(build.Skills.Single(s => s.Name == "c").TakesABeat);

        // Hurt to begin with, and bitten for nothing, so any recovery is the skill's.
        var champ = new Champion { MaxHealth = 100_000, Health = 40_000 };
        SoloBattle.ResolveWave(champ, build, new Hunter(),
            enemyHealth: 2_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_500,
            ExpeditionTuning.Default, new Random(3));

        Assert.True(champ.Health > 40_000,
            $"a TRANSFORMATION in a passive slot healed nothing (health {champ.Health}); giving back " +
            "what it deals is the style's whole identity and it cannot be lost by changing slot.");
    }
}
