using System;
using System.Linq;
using System.Collections.Generic;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Expeditions;
using Xunit.Abstractions;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

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
    [Fact]
    public void test_a_slot_chooses_the_face_and_the_face_decides_the_beat()
    {
        // A style's two skills are the two slots' answers: same STYLE and same art, and only the
        // active costs the champion its action. (The kind is the def's own since P3-final — there is
        // no PassiveSlot flag left to disagree with it.)
        foreach (var style in Enum.GetValues<Style>())
        {
            var active = TestBuilds.Skill(SkillCatalogue.ActiveOf(style).Id);
            var passive = TestBuilds.Skill(SkillCatalogue.PassiveOf(style).Id);

            Assert.True(active.TakesABeat);
            Assert.False(passive.TakesABeat);
            Assert.Equal(active.Def.Style, passive.Def.Style);
            Assert.Equal(active.Def.ClipKey, passive.Def.ClipKey);
            Assert.NotEqual(active.Def.Name, passive.Def.Name);
        }
    }

    [Fact]
    public void test_every_legacy_form_resolves_inside_its_own_style()
    {
        // A pre-v3 save carries a Form name. It must always land on a real skill — and always on
        // one of ITS OWN STYLE'S two, because the slot may change which ability it is but never who
        // the champion is. The map is FROZEN history now (LegacySkillForm), read once at restore.
        foreach (var form in new[] { "Strike", "Trap", "Mark", "Projectile", "Aura", "Transformation" })
        {
            var activeId = LegacySkillForm.Resolve(form, passive: false);
            var passiveId = LegacySkillForm.Resolve(form, passive: true);
            Assert.NotNull(activeId);
            Assert.NotNull(passiveId);

            var active = SkillCatalogue.ById(activeId!);
            var passive = SkillCatalogue.ById(passiveId!);
            Assert.Equal(active.Style, passive.Style);
            Assert.True(active.TakesABeat, $"{form}'s active face resolved to something that costs no beat.");
            Assert.False(passive.TakesABeat, $"{form} in a passive slot resolved to something that costs a beat.");
        }
    }

    [Fact]
    public void test_actives_and_passives_are_counted_separately()
    {
        var b = new Build();
        b.Equip(TestBuilds.Skill("hammer_blow"));
        b.Equip(TestBuilds.Skill("volley_spray"));
        b.Equip(TestBuilds.Skill("field_mire"));

        Assert.Equal(2, b.ActiveCount);
        Assert.Equal(1, b.PassiveCount);
        Assert.Equal(3, b.Skills.Count);
    }

    [Fact]
    public void test_a_full_active_budget_does_not_block_a_passive()
    {
        var b = new Build { ActiveCapacity = 2, PassiveCapacity = 2 };
        Assert.True(b.Equip(TestBuilds.Skill("hammer_blow")));
        Assert.True(b.Equip(TestBuilds.Skill("volley_spray")));

        // Actives are full; a third active is refused...
        Assert.False(b.Equip(TestBuilds.Skill("sign_call")));
        // ...but the passive budget is untouched, which is the entire point of splitting them.
        Assert.True(b.Equip(TestBuilds.Skill("field_mire")));
        Assert.True(b.Equip(TestBuilds.Skill("snare_jaws")));
        Assert.False(b.Equip(TestBuilds.Skill("drain_wilt")));

        Assert.Equal(2, b.ActiveCount);
        Assert.Equal(2, b.PassiveCount);
    }

    [Fact]
    public void test_an_unset_capacity_is_the_whole_budget()
    {
        // With no per-kind cap set, a build's budget is undivided and the whole of it is spendable on
        // one kind. UPDATED 2026-09-03: it used to ask for FIVE, because the trait spine sold a fifth
        // slot and a per-kind cap frozen at the old constant would have clamped it off. The fifth slot
        // is removed, so the whole budget is four — the rule under test is unchanged, only its size.
        // Four DIFFERENT actives: a build holds a skill once (LAW 13), so the budget is filled with
        // four of the six styles' actives rather than four copies of one.
        var b = new Build { SlotCapacity = Build.SkillSlots };
        var actives = new[] { "hammer_blow", "snare_repay", "sign_call", "volley_spray" };
        for (var i = 0; i < actives.Length; i++)
            Assert.True(b.Equip(TestBuilds.Skill(actives[i])), $"slot {i + 1} of 4 was refused");
        Assert.Equal(4, b.Skills.Count);
    }

    [Fact]
    public void test_beat_demand_is_what_the_rework_exists_to_lower()
    {
        // Four beat-taking skills is the state that made the basic attack disappear: the swing only
        // lands when no skill claimed the beat, so ~0.8 demand leaves ~1 beat in 5 for it.
        var four = new Build { ActiveCapacity = 4 };
        four.Equip(TestBuilds.Skill("hammer_blow"));
        four.Equip(TestBuilds.Skill("volley_spray"));
        four.Equip(TestBuilds.Skill("sign_call"));
        four.Equip(TestBuilds.Skill("drain_drink"));
        Assert.True(four.BeatDemand > 0.75f, $"four actives demanded only {four.BeatDemand:0.00}");

        // Two actives plus two passives: the passives add NOTHING, by construction.
        var two = new Build { ActiveCapacity = 2, PassiveCapacity = 2 };
        two.Equip(TestBuilds.Skill("hammer_blow"));
        two.Equip(TestBuilds.Skill("volley_spray"));
        var withoutPassives = two.BeatDemand;
        two.Equip(TestBuilds.Skill("field_mire"));
        two.Equip(TestBuilds.Skill("snare_jaws"));

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
        // ORDERED BY REALISED DEMAND, not by beat count. An Active is no longer always counted in
        // beats — CLOCKWORK is counted in milliseconds — and a Beats-0 Active sorted as the SHORTEST
        // cooldown in the catalogue while contributing 0.00 to Build.BeatDemand, so the "worst pair"
        // would have been the cheapest one. Demand is 1/beats for a beat-counted skill and
        // beat/interval for a clock-counted one, which is what each really costs per action.
        //
        // ACROSS THE WHOLE CATALOGUE, signatures included: a champion may weave its own signature
        // beside any shared active, so that pair is one a player can really build.
        static float Demand(SkillDef d)
            => d.Beats > 0 ? 1f / d.Beats
             : d.IntervalMs > 0 ? SoloBattle.DefaultBeatMs / (float)d.IntervalMs
             : 0f;

        var shortest = SkillCatalogue.All
            .Where(s => s.TakesABeat).OrderByDescending(Demand).Take(2).ToList();

        var demand = shortest.Sum(Demand);
        Assert.True(demand < 0.5f,
            $"the worst active pair ({string.Join(" + ", shortest.Select(s => s.Name))}) " +
            $"demands {demand:0.00} of the beats");
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
    private static BuildComposer.SkillPick P(string skillId)
        => new(Source.Body, null, SkillId: skillId);

    private static Build Compose(int slots, params BuildComposer.SkillPick[] skills)
        => BuildComposer.Compose(Taught.Everything(), character: null,
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
            P("hammer_blow"), P("volley_spray"),
            P("sign_brand"), P("drain_wilt"));

        Assert.Equal(2, b.ActiveCapacity);
        Assert.Equal(2, b.PassiveCapacity);
        Assert.Equal(2, b.ActiveCount);
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
            mastery, character: null,
            skills: new[]
            {
                P("hammer_blow"),
                // DELIBERATELY the style's passive: HAMMER's PRESS, the road's second node.
                P("hammer_press"),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        // ALL TWELVE are learned on the tree since 2026-08-30, so an untaught champion with no
        // character weaves NOTHING — which is exactly why every champion brings one skill of its own.
        var untaught = new MasteryTree();
        Assert.Empty(With(untaught).Skills);

        var walked = new MasteryTree();
        walked.SetEarned(999);
        walked.RestoreTaken(new[] { "spec_strike", "road_hammer", "road_hammer_2" }, repair: false);
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
        // skills demanded about 0.80 and the basic attack only lands on what is left over; the
        // two-and-two loadout must leave the swing the majority.
        var b = Compose(4,
            P("hammer_blow"), P("volley_spray"),
            P("sign_brand"), P("drain_wilt"));

        Assert.True(b.BeatDemand < 0.5f,
            $"a full composed build still demands {b.BeatDemand:0.00} of the beats");
    }

    [Fact]
    public void test_aura_and_trap_still_cost_no_beat_after_the_split()
    {
        // Fields and Reactions never cost a beat, and the split must not have quietly promoted them
        // into the active budget: if it had, a classic MIRE+JAWS build would now be spending actions
        // it never spent.
        var b = Compose(4,
            P("field_mire"), P("snare_jaws"),
            P("hammer_blow"), P("volley_spray"));

        Assert.Equal(4, b.Skills.Count);
        Assert.Equal(2, b.ActiveCount);
        Assert.Equal(new[] { "hammer_blow", "volley_spray" },
            b.Skills.Where(s => s.TakesABeat).Select(s => s.Def.Id).ToArray());
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

    private static BuildComposer.SkillPick P(string skillId)
        => new(Source.Body, null, SkillId: skillId);

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
        // A cast is a Skill event that claimed the beat. A Reaction never does, so it is not an
        // action — and events are slot-keyed now, so the reactions are found by what they are.
        var reactionSlots = build.Skills
            .Select((s, i) => (Skill: s, Slot: i))
            .Where(x => x.Skill.Def.Kind == SkillKind.Reaction)
            .Select(x => x.Slot)
            .ToHashSet();
        var casts = events.Count(e => e.Kind == BattleEventKind.Skill && !reactionSlots.Contains(e.Slot));
        return (beats, casts);
    }

    [Fact]
    public void test_four_beat_taking_skills_leave_almost_no_room_for_the_swing()
    {
        // The state the playtest complained about, reproduced: "there are even scenarios with no
        // plain attack at all". This is the BEFORE half of the measurement and it must keep failing
        // to leave room, or the AFTER half proves nothing.
        var before = new Build { ActiveCapacity = 4, PassiveCapacity = 0 };
        foreach (var id in new[] { "hammer_blow", "volley_spray", "sign_call", "drain_drink" })
            before.Equip(TestBuilds.Skill(id));

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
        // The two-and-two loadout — the same shape the restore migration hands an old four-caster
        // save (BLOW, SPRAY, and the overflow landed on BRAND and WILT).
        var after = BuildComposer.Compose(
            Taught.Everything(), character: null,
            skills: new[] { P("hammer_blow"), P("volley_spray"),
                            P("sign_brand"), P("drain_wilt") },
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
            Taught.Everything(), character: null,
            skills: new[] { P("hammer_blow"), P("volley_spray"),
                            P("field_mire"), P("snare_jaws") },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, after, new Hunter(),
            enemyHealth: 3_000_000f, enemyDamage: 40f, enemyIntervalMs: 1_500,
            ExpeditionTuning.Default, new Random(7));

        Assert.True(events.Any(e => e.Kind == BattleEventKind.Aura),
            "the woven Field never ticked — a passive slot that costs no beat must still act.");
        // Events carry the SLOT now, so the reaction is found by what it IS.
        var trapSlot = after.Skills.ToList().FindIndex(s => s.Def.Kind == SkillKind.Reaction);
        Assert.True(trapSlot >= 0, "the composed build carries no Reaction at all");
        Assert.True(events.Any(e => e.Kind == BattleEventKind.Skill && e.Slot == trapSlot),
            "the woven Reaction never fired, though the champion was being bitten.");
    }
}

/// <summary>
/// A skill in a passive slot must still DO its thing — not merely stop costing a beat.
/// </summary>
/// <remarks>
/// The slot split made this reachable for the first time: a build saved with four beat-taking skills
/// lands its overflow on the styles' passive skills (a restore-time migration since P3-final — see
/// <see cref="LegacySkillForm"/>), so a Sign or a Drain slot can arrive at the Field branch. The
/// Field branch only knew how to deal damage — so an amplifying or breaking Field sat in a slot the
/// player could see, doing nothing whatever. That is this codebase's signature failure, and it was
/// introduced by the fix for another one.
/// </remarks>
public class PassiveEffectTests
{
    private static BuildComposer.SkillPick P(string skillId)
        => new(Source.Body, null, SkillId: skillId);

    private static Build Compose(params BuildComposer.SkillPick[] skills)
        => BuildComposer.Compose(Taught.Everything(), character: null,
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
        // Same two actives either way; the third slot is the variable. If the passive BRAND did
        // nothing, these two would deal the same damage.
        var withMark = Compose(P("hammer_blow"), P("volley_spray"), P("sign_brand"));
        var without  = Compose(P("hammer_blow"), P("volley_spray"));

        Assert.Equal(1, withMark.PassiveCount);
        Assert.False(withMark.Skills.Single(s => s.Def.Id == "sign_brand").TakesABeat);

        var amplified = DamageOver(withMark, 2_000_000f);
        var plain = DamageOver(without, 2_000_000f);
        Assert.True(amplified > plain,
            $"BRAND in a passive slot changed nothing: {amplified:0} against {plain:0}. It costs no " +
            "beat, but it must still open its window or it is a slot the player can see doing nothing.");
    }

    [Fact]
    public void test_a_spilled_transformation_becomes_wilt_and_breaks_the_waves_attack()
    {
        // A spill changes which SKILL is woven: a legacy Transformation slot that lands passive
        // migrates onto DRAIN's WILT, which is an attack break rather than a lifesteal. It must DO
        // that — a slot the player can see doing nothing is the failure this whole file exists to
        // refuse — but what it does is WILT's job, not the one the active face used to have.
        Assert.Equal("drain_wilt", LegacySkillForm.Resolve("Transformation", passive: true));

        var build = Compose(P("hammer_blow"), P("volley_spray"), P("drain_wilt"));
        var spilled = build.Skills.Single(s => s.Def.Id == "drain_wilt");
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
        var without = HealthAfter(Compose(P("hammer_blow"), P("volley_spray")));
        Assert.True(withWilt > without,
            $"WILT changed nothing: {withWilt} health against {without} without it. Its whole line is " +
            "that every enemy bites softer.");
    }
}

/// <summary>
/// Every one of the twelve can actually be reached by a player.
/// </summary>
/// <remarks>
/// A catalogue entry nothing can select is the same dead weight as a field nothing reads, and this
/// gap was real: slot POSITION used to decide which skill a slot resolved to, so a style's active
/// or passive could have no door at all. The door is the mastery tree now — a skill is a thing you
/// LEARN — and this file is what stops a future skill being added with no road to it.
/// </remarks>
public class SkillReachabilityTests
{
    [Fact]
    public void test_every_skill_in_the_catalogue_can_be_selected()
    {
        // The tree with every style road walked must know all twelve SHARED skills — one no road
        // teaches is unreachable, since composing an untaught skill is refused.
        //
        // THE TEN SIGNATURES ARE EXCLUDED, and that is the law rather than an exemption: no mastery
        // node teaches one (BRIEF sec.18), the composer adds the active champion's own signature to
        // the taught set outright, and SignatureOwnershipTest asserts that a tree with EVERY node
        // taken still cannot hand somebody else's over. A road that taught one would be the bug.
        var taught = Taught.Everything().AvailableSkills();

        var missing = SkillCatalogue.Shared.Select(s => s.Id).Where(id => !taught.Contains(id)).ToList();
        Assert.True(missing.Count == 0,
            "no style road teaches: " + string.Join(", ", missing) +
            ". A skill the player cannot learn is dead weight in the catalogue.");

        // And the roads teach no ghosts: every taught id is a real catalogue skill.
        foreach (var id in taught)
            Assert.NotNull(SkillCatalogue.Find(id));
    }

    [Fact]
    public void test_the_players_choice_of_slot_beats_the_spill_rule()
    {
        // The Passive flag survives as the save's echo of the slot the player once chose (a named
        // skill brings its own kind). Once a player has made that choice it must win, or the
        // workbench is showing a decision the fight ignores.
        var picks = new[]
        {
            new BuildComposer.SkillPick(Source.Body, null, Passive: true),
            new BuildComposer.SkillPick(Source.Body, null, Passive: false),
            new BuildComposer.SkillPick(Source.Body, null, Passive: false),
        };
        var kinds = BuildComposer.SlotKinds(picks, slotCapacity: 4);

        Assert.True(kinds[0], "the player asked for a passive slot and the composer overruled it.");
        Assert.False(kinds[1]);
        Assert.False(kinds[2]);   // the active budget is two, and the first skill did not spend one
    }
}
