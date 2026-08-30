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
        var def = SkillCatalogue.Resolve(form, passive);
        return new EquippedSkill(
            new WovenAbility { Name = name ?? def.Name, Source = Source.Body, Form = form },
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
        // Same STYLE and same art; a spilled active becomes a different ability of that style.
        Assert.Equal(active.Def.Style, passive.Def.Style);
        Assert.Equal(active.Def.ClipKey, passive.Def.ClipKey);
        Assert.NotEqual(active.Def.Name, passive.Def.Name);
    }

    [Fact]
    public void test_every_legacy_form_resolves_inside_its_own_style()
    {
        // A saved build carries a Form ordinal. It must always land on a real skill — and always on
        // one of ITS OWN STYLE'S two, because the slot may change which ability it is but never who
        // the champion is. LegacyForm itself is not the assertion: six of the twelve have none, since
        // no old save could hold them.
        foreach (var form in Enum.GetValues<Form>())
        {
            var style = SkillCatalogue.Resolve(form, passive: false).Style;
            Assert.Equal(style, SkillCatalogue.Resolve(form, passive: true).Style);
            Assert.Equal(style, Skill(form).Def.Style);
        }
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
            .Where(s => s.TakesABeat).OrderBy(s => s.Beats).Take(2).ToList();

        var b = new Build { ActiveCapacity = 2 };
        foreach (var def in shortest)
            b.Weave(Skill(def.LegacyForm ?? Form.Strike, name: def.Id));

        Assert.True(b.BeatDemand < 0.5f,
            $"the worst active pair ({string.Join(" + ", shortest.Select(s => s.Name))}) " +
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
        => BuildComposer.Compose(new MemoryDustTree(), Taught.Everything(), character: null,
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
        //
        // THE CHAMPION HAS TO KNOW THEM. Every skill is learned on the mastery tree since
        // 2026-08-30, so the migration is only a migration for a champion that walked the roads —
        // which is what this poses. Without them the build is empty, and that is the gate's own test.
        var mastery = new MasteryTree();
        mastery.SetEarned(9999);
        mastery.RestoreTaken(MasteryCatalog.Nodes
            .Where(x => x.Kind == MasteryKind.SkillRoad).Select(x => x.Id));
        var b = BuildComposer.Compose(
            new MemoryDustTree(), mastery, character: null,
            skills: new[]
            {
                S("a", Form.Strike), S("b", Form.Projectile),
                S("c", Form.Mark), S("d", Form.Transformation),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        Assert.Equal(4, b.Skills.Count);
        Assert.Equal(new[] { "a", "b", "c", "d" }, b.Skills.Select(s => s.Name).ToArray());
        // The two woven FIRST keep their actives; any other rule reorders the player's build for them.
        Assert.Equal(new[] { "a", "b" }, b.Skills.Where(s => s.TakesABeat).Select(s => s.Name).ToArray());
        Assert.Equal(new[] { "c", "d" }, b.Skills.Where(s => !s.TakesABeat).Select(s => s.Name).ToArray());
    }

    /// <summary>
    /// Six skills have to be TAUGHT, and choosing one you have not learned is refused (design §5).
    /// </summary>
    /// <remarks>
    /// The gate the style roads exist for. Respec RE-LOCKS (designer, 2026-08-30), so this is asserted
    /// in both directions: without the road the slot is not woven, with it the slot is.
    /// </remarks>
    [Fact]
    public void test_a_skill_the_tree_has_not_taught_cannot_be_chosen()
    {
        Build With(MasteryTree mastery) => BuildComposer.Compose(
            new MemoryDustTree(), mastery, character: null,
            skills: new[]
            {
                S("a", Form.Strike),
                // DELIBERATELY passive: HAMMER's PRESS, one of the six with no Form of its own.
                S("b", Form.Strike) with { Passive = true },
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        // ALL TWELVE are learned on the tree since 2026-08-30, so an untaught champion with no
        // character weaves NOTHING — which is exactly why every champion brings one skill of its own.
        var untaught = new MasteryTree();
        Assert.Empty(With(untaught).Skills);

        var walked = new MasteryTree();
        walked.SetEarned(999);
        walked.RestoreTaken(new[] { "spec_strike", "road_hammer", "road_hammer_2" });
        var b = With(walked);
        Assert.Equal(2, b.Skills.Count);
        Assert.Equal(SkillCatalogue.PassiveOf(Style.Hammer).Id, b.Skills[1].Def.Id);

        // AND RESPEC TAKES IT BACK. The designer chose a road that re-locks, so the same build
        // composed after the points are refunded weaves nothing at all.
        walked.Respec();
        Assert.Empty(With(walked).Skills);
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
            new MemoryDustTree(), Taught.Everything(), character: null,
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
            new MemoryDustTree(), Taught.Everything(), character: null,
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
        => BuildComposer.Compose(new MemoryDustTree(), Taught.Everything(), character: null,
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
    public void test_a_spilled_transformation_becomes_wilt_and_breaks_the_waves_attack()
    {
        // A spill changes which SKILL is woven: TRANSFORMATION overflows onto DRAIN's passive, WILT,
        // which is an attack break rather than a lifesteal. It must DO that — a slot the player can
        // see doing nothing is the failure this whole file exists to refuse — but what it does is
        // WILT's job, not the one the active face used to have.
        var build = Compose(S("a", Form.Strike), S("b", Form.Projectile), S("c", Form.Transformation));
        var spilled = build.Skills.Single(s => s.Name == "c");
        Assert.False(spilled.TakesABeat);
        Assert.Equal("WILT", spilled.Def.Name);
        Assert.True(spilled.Def.AttackBreakPerTick > 0f);

        // Bitten hard, for long enough that the break has deepened. Against the same wave without it,
        // the champion must have taken visibly less.
        static int HealthAfter(Build b)
        {
            var champ = new Champion { MaxHealth = 400_000, Health = 400_000 };
            SoloBattle.ResolveWave(champ, b, new Hunter(),
                enemyHealth: 4_000_000f, enemyDamage: 900f, enemyIntervalMs: 700,
                ExpeditionTuning.Default, new Random(3));
            return champ.Health;
        }

        var withWilt = HealthAfter(build);
        var without = HealthAfter(Compose(S("a", Form.Strike), S("b", Form.Projectile)));
        Assert.True(withWilt > without,
            $"WILT changed nothing: {withWilt} health against {without} without it. Its whole line is " +
            "that every enemy bites softer.");
    }
}

/// <summary>
/// A skill moved into a passive slot trades burst for steadiness — not power.
/// </summary>
/// <remarks>
/// <para>
/// <c>WeavingTuning.FormBaseValue</c> quotes each Form in the units it is PAID in: AURA's 12 is one
/// second's worth, but STRIKE's 500 is a single nine-second cast and TRANSFORMATION's 260 an
/// eight-second one. The Field branch ticks every second, so a spilled skill paid its whole cast
/// value every tick — nine and eight times its intended output, on the very path the slot split
/// created.
/// </para>
/// <para>
/// Parity is the deliberate starting point rather than a law: a passive costs no beat, so it may
/// well deserve to sit under its active face. What must never return is a passive slot silently
/// multiplying a skill by its cooldown.
/// </para>
/// </remarks>
public class PassiveCadenceTests
{
    private static Build OneSkill(Form form, bool passive)
    {
        var b = new Build { ActiveCapacity = 1, PassiveCapacity = 1 };
        b.Weave(new EquippedSkill(
            new WovenAbility { Name = form.ToString(), Source = Source.Body, Form = form },
            FormBehaviour.BaseCooldownMs(form), PassiveSlot: passive));
        return b;
    }

    private static long DamageIn(Build build, int waveMs)
    {
        var champ = new Champion { MaxHealth = 50_000_000, Health = 50_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(),
            // Health far beyond anything the wave can chew through, so the fight runs the full clock
            // and the comparison is over the same elapsed time on both sides.
            enemyHealth: 500_000_000f, enemyDamage: 0f, enemyIntervalMs: 100_000,
            ExpeditionTuning.Default, new Random(5));
        return events.Where(e => e.Kind == BattleEventKind.Strike && e.AtMs <= waveMs)
                     .Sum(e => (long)e.Amount);
    }

    [Theory]
    [InlineData(Form.Strike)]
    [InlineData(Form.Transformation)]
    [InlineData(Form.Projectile)]
    public void test_a_spilled_skill_does_not_multiply_itself_by_its_cooldown(Form form)
    {
        const int window = 30_000;
        var asActive = DamageIn(OneSkill(form, passive: false), window);
        var asPassive = DamageIn(OneSkill(form, passive: true), window);

        Assert.True(asPassive > 0, $"{form} in a passive slot dealt nothing at all.");

        // Both builds also swing, so neither figure is the skill alone; what is being caught is an
        // ORDER-OF-MAGNITUDE break, which is what ticking a cast value every second produces.
        var ratio = asPassive / (double)asActive;
        Assert.True(ratio < 2.0,
            $"{form} dealt {ratio:0.00}x as much from a passive slot as from an active one. A Field " +
            "ticks every second and FormBaseValue is quoted per CAST, so an unscaled spill multiplies " +
            "the skill by its whole cooldown.");
        Assert.True(ratio > 0.4,
            $"{form} dealt only {ratio:0.00}x as much passively; moving a slot should cost steadiness, " +
            "not most of the skill.");
    }
}

/// <summary>
/// The per-skill cooldown is READ. It went unread for the whole of the project's life.
/// </summary>
/// <remarks>
/// <para>
/// <c>EquippedSkill(WovenAbility, int CooldownMs)</c> has carried that field since it was written.
/// It was set from the Form table at construction, persisted, and then dereferenced NOWHERE — a test
/// comment elsewhere in this suite says so outright. Somebody opened a per-skill cooldown and there
/// was never a system to fill it, because the Form owned the number.
/// </para>
/// <para>
/// It has to be live before a variation or a reinforcement can change one skill's cooldown without
/// changing every skill of that style. This test exists so it cannot quietly go back to sleep: it
/// fails the moment the sim stops asking the skill and goes back to asking the table.
/// </para>
/// </remarks>
public class PerSkillCooldownTests
{
    private static Build WithCooldown(Form form, int cooldownMs)
    {
        var b = new Build { ActiveCapacity = 1, PassiveCapacity = 0 };
        b.Weave(new EquippedSkill(
            new WovenAbility { Name = "s", Source = Source.Body, Form = form },
            cooldownMs, PassiveSlot: false));
        return b;
    }

    private static int CastsIn(Build build)
    {
        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(),
            enemyHealth: 20_000_000f, enemyDamage: 0f, enemyIntervalMs: 100_000,
            ExpeditionTuning.Default, new Random(9));
        return events.Count(e => e.Kind == BattleEventKind.Skill);
    }

    [Fact]
    public void test_a_skills_own_beat_count_drives_its_cadence()
    {
        // Every ACTIVE is beat-counted now, so the cadence a variation would change is Def.Beats.
        // A four-beat skill must cast more often than a six-beat one over the same wave.
        var six = CastsIn(WithCooldown(Form.Strike, FormBehaviour.BaseCooldownMs(Form.Strike)));
        var four = CastsIn(WithCooldown(Form.Projectile, FormBehaviour.BaseCooldownMs(Form.Projectile)));

        Assert.Equal(6, SkillCatalogue.Resolve(Form.Strike, false).Beats);
        Assert.Equal(4, SkillCatalogue.Resolve(Form.Projectile, false).Beats);
        Assert.True(six > 0, "the slower build never cast at all — the wave was too short to measure.");
        Assert.True(four > six,
            $"the four-beat skill cast {four} times against the six-beat skill's {six}. The sim is not " +
            "reading the skill's own beat count.");
    }

    [Fact]
    public void test_the_form_table_now_answers_from_the_catalogue()
    {
        // ONE SOURCE. FormBehaviour is the Form-shaped door onto SkillCatalogue, not a second table:
        // the hunt rail's cooldown ring reads it, the sim reads the catalogue, and they must agree or
        // the dial lies about the fight — the exact failure the Beat event was added to stop.
        foreach (var form in Enum.GetValues<Form>())
        {
            // The table answers for the Form's NATURAL kind — what this Form has always been — not
            // for the style's active. Forcing AURA to its style's active would make a field
            // beat-counted and hand it a cast's cooldown.
            var natural = FormBehaviour.IsPassive(form) || FormBehaviour.FiresOnBeingHit(form);
            var def = SkillCatalogue.Resolve(form, natural);
            Assert.Equal(def.Beats, FormBehaviour.CooldownBeats(form));
        }
    }
}

/// <summary>
/// Every one of the twelve can actually be reached by a player.
/// </summary>
/// <remarks>
/// A catalogue entry nothing can select is the same dead weight as a field nothing reads, and this
/// gap was real: slot POSITION used to decide which face a Form resolved to, so AURA and TRAP
/// resolved passive from either side and FIELD's PULSE and SNARE's REPAY had no door at all. The slot
/// kind is the player's own choice now, and this file is what stops a future skill being added with
/// no way in.
/// </remarks>
public class SkillReachabilityTests
{
    [Fact]
    public void test_every_skill_in_the_catalogue_can_be_selected()
    {
        var reachable = new HashSet<string>();
        foreach (var form in Enum.GetValues<Form>())
        foreach (var passive in new[] { false, true })
            reachable.Add(SkillCatalogue.Resolve(form, passive).Id);

        var missing = SkillCatalogue.All.Select(s => s.Id).Where(id => !reachable.Contains(id)).ToList();
        Assert.True(missing.Count == 0,
            "no (Form, slot) pair reaches: " + string.Join(", ", missing) +
            ". A skill the player cannot select is dead weight in the catalogue.");
        Assert.Equal(SkillCatalogue.All.Count, reachable.Count);
    }

    [Fact]
    public void test_the_players_choice_of_slot_beats_the_spill_rule()
    {
        // The spill exists for saves that predate the choice. Once a player has made one it must win,
        // or the workbench is showing a decision the fight ignores.
        var picks = new[]
        {
            new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a", Passive: true),
            new BuildComposer.SkillPick(Source.Body, Form.Projectile, null, "b"),
            new BuildComposer.SkillPick(Source.Body, Form.Mark, null, "c"),
        };
        var kinds = BuildComposer.SlotKinds(picks, slotCapacity: 4);

        Assert.True(kinds[0], "the player asked for a passive Strike and the composer overruled it.");
        Assert.False(kinds[1]);
        Assert.False(kinds[2]);   // the active budget is two, and the first skill did not spend one
    }

    [Fact]
    public void test_choosing_the_passive_slot_reaches_the_styles_other_skill()
    {
        // The whole point of the switch: the same Form in the other slot is a DIFFERENT ability.
        foreach (var form in new[] { Form.Strike, Form.Projectile, Form.Mark, Form.Transformation })
        {
            var active = SkillCatalogue.Resolve(form, passive: false);
            var passive = SkillCatalogue.Resolve(form, passive: true);
            Assert.Equal(active.Style, passive.Style);
            Assert.NotEqual(active.Id, passive.Id);
            Assert.True(active.TakesABeat);
            Assert.False(passive.TakesABeat);
        }
    }
}
