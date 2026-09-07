using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// VOW OF THE PURE is kept by a CHOSEN single Source — <see cref="Build.ChosenSingleSource"/> — and by
/// nothing a slot's seed says on the player's behalf.
/// </summary>
/// <remarks>
/// <para>
/// A vow is a conscious restriction bought as power. The weave screen offers no Source lever but the
/// variation, so a skill still on its BODY seed has a Source nobody picked — implementation state, not
/// a restriction being kept. The vow's discovery already refused defaults
/// (<see cref="VowTemptation.EveryWovenSourceChosen"/>); since 2026-09-07 its KEPT state refuses them too.
/// One chosen skill keeps it — the two-skill floor is THE SINGLE NOTE's definition of a commitment,
/// and belongs to the trait, not to the vow.
/// </para>
/// <para>
/// The vow architecture is untouched and pinned here beside the matrix: the vow belongs to the build,
/// it is evaluated once against one <see cref="BuildContext"/>, its reward is applied once however many
/// slots swore it, and a broken vow contributes nothing at all.
/// </para>
/// </remarks>
public class vow_of_the_pure_test
{
    private const string Signature = "sig_seeker_hard_hands";   // OPEN HAND = Body, SHUT FIST = Mind
    private const string Blow = "hammer_blow";                  // FLATTEN = Body, FINISH = Shadow
    private const string Press = "hammer_press";                // CRUSHING = Body, PIN = Machine (a Field)

    private static Vow Pure => Vows.ById("vow_pure")!;

    private static Build Woven(params EquippedSkill[] skills)
    {
        var b = new Build();
        foreach (var s in skills) Assert.True(b.Equip(s), $"{s.Def.Id} did not equip — the fixture, not the design");
        return b;
    }

    private static bool Kept(Build b) => Vows.IsActive(Pure, SoloBattle.DescribeBuild(b, new Hunter()));

    // ── THE MATRIX ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_vow_of_the_pure_one_skill_on_its_seed_is_not_kept()
    {
        // Arrange — the shape a new game hands out: one skill, its variation not chosen, seeded BODY.
        var b = Woven(TestBuilds.Skill(Blow, Source.Body, Pure));

        // Act / Assert — the fight casts one Source, and the vow is still not kept: nobody chose it.
        Assert.Equal(1, SoloBattle.DescribeBuild(b, new Hunter()).DistinctSources);
        Assert.False(Kept(b));
    }

    [Fact]
    public void test_vow_of_the_pure_one_chosen_skill_is_kept()
    {
        var b = Woven(TestBuilds.Chosen(Blow, "FLATTEN", Pure));

        Assert.True(Kept(b));
    }

    [Fact]
    public void test_vow_of_the_pure_two_chosen_on_one_source_is_kept()
    {
        var b = Woven(TestBuilds.Chosen(Blow, "FLATTEN", Pure), TestBuilds.Chosen(Press, "CRUSHING", Pure));

        Assert.True(Kept(b));
    }

    [Fact]
    public void test_vow_of_the_pure_a_chosen_skill_beside_an_unchosen_one_is_not_kept()
    {
        // The seed agrees with the choice — and it still breaks the vow, because it was not chosen.
        var b = Woven(TestBuilds.Chosen(Blow, "FLATTEN", Pure), TestBuilds.Skill(Press, Source.Body, Pure));

        Assert.Equal(1, SoloBattle.DescribeBuild(b, new Hunter()).DistinctSources);
        Assert.False(Kept(b));
    }

    [Fact]
    public void test_vow_of_the_pure_two_chosen_on_different_sources_is_not_kept()
    {
        var b = Woven(TestBuilds.Chosen(Blow, "FINISH", Pure), TestBuilds.Chosen(Press, "CRUSHING", Pure));

        Assert.False(Kept(b));
    }

    [Fact]
    public void test_vow_of_the_pure_has_no_two_skill_floor_unlike_the_traits_commitment()
    {
        var lone = Woven(TestBuilds.Chosen(Signature, "OPEN HAND", Pure));

        Assert.True(Kept(lone));
        Assert.NotNull(lone.ChosenSingleSource);
        Assert.Null(lone.PureSource);   // the trait waits for a second skill; the vow does not
    }

    // ── THE REWARD: once when kept, nothing when broken ─────────────────────────────────────────

    [Fact]
    public void test_vow_of_the_pure_pays_exactly_once_however_many_slots_swore_it()
    {
        // Sworn on both skills — one promise written down twice. The build's vow list holds it once,
        // and the combined factor is the single vow's own multiplier.
        var b = Woven(TestBuilds.Chosen(Blow, "FLATTEN", Pure), TestBuilds.Chosen(Press, "CRUSHING", Pure));
        var ctx = SoloBattle.DescribeBuild(b, new Hunter());

        Assert.Single(b.Vows);
        Assert.Equal(1, Vows.KeptCount(b.Vows, ctx));
        Assert.Equal(Vows.ResolvedMultiplier(Pure, 1f), Vows.CombinedFactor(b.Vows, ctx, 1f), 4);
        Assert.True(Vows.CombinedFactor(b.Vows, ctx, 1f) > 1f);
    }

    [Fact]
    public void test_vow_of_the_pure_pays_nothing_while_broken()
    {
        var broken = Woven(TestBuilds.Chosen(Blow, "FLATTEN", Pure), TestBuilds.Skill(Press, Source.Body, Pure));
        var ctx = SoloBattle.DescribeBuild(broken, new Hunter());

        Assert.Single(broken.Vows);                              // sworn, and still sworn
        Assert.Equal(0, Vows.KeptCount(broken.Vows, ctx));
        Assert.Equal(1f, Vows.CombinedFactor(broken.Vows, ctx, 1f), 4);
    }

    [Fact]
    public void test_vow_of_the_pure_in_the_fight_a_kept_vow_pays_and_a_broken_one_deals_as_if_unsworn()
    {
        // The whole fight, not the factor: the same skill with the vow sworn, chosen against unchosen,
        // and each against itself unsworn.
        float Dealt(Build build)
        {
            var target = new WaveCreature { MaxHealth = 1e9f, Health = 1e9f, Damage = 0f };
            var (_, events) = SoloBattle.ResolveWave(
                new Champion { MaxHealth = 100_000, Health = 100_000 }, build, new Hunter(), new[] { target },
                enemyIntervalMs: 100_000, ExpeditionTuning.Default with { AutoAttackDamage = 0f }, new Random(11));
            return events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }

        var chosenSworn = Dealt(Woven(TestBuilds.Chosen(Blow, "FLATTEN", Pure)));
        var chosenPlain = Dealt(Woven(TestBuilds.Chosen(Blow, "FLATTEN")));
        var seedSworn = Dealt(Woven(TestBuilds.Skill(Blow, Source.Body, Pure)));
        var seedPlain = Dealt(Woven(TestBuilds.Skill(Blow, Source.Body)));

        Assert.True(chosenSworn > chosenPlain * 1.05f, $"a kept VOW OF THE PURE paid nothing: {chosenSworn:N0} vs {chosenPlain:N0}");
        Assert.Equal(seedPlain, seedSworn, 0.5);
    }

    // ── THE COMPOSED PATH: a real loadout, authored variations, the composer ────────────────────

    private static Character Seeker => CharacterRoster.Get("seeker");

    /// <summary>A loadout the way play builds one: the skill set, the vow sworn, the variation chosen (or not).</summary>
    private static Build Composed(params (string SkillId, string? Variation)[] slots)
    {
        var loadout = new PlayerLoadout { SkillCapacity = 4, VowCapacity = 1 };
        var progress = new SkillProgress();
        var known = new[] { Pure };
        foreach (var (id, variation) in slots)
        {
            var def = SkillCatalogue.ById(id);
            var slot = loadout.AddSkill();
            Assert.True(loadout.SetSkill(slot, id));
            Assert.True(loadout.SetVow(slot, Pure.Id, known));
            if (variation is null) continue;
            for (var i = 0; i < SkillProgress.UsesForLevel(1); i++) progress.RecordWave(id);
            Assert.True(progress.ChooseVariation(def, variation), $"{id} could not take {variation} — the fixture, not the design");
        }
        var build = loadout.ToBuild(Taught.Everything(), Seeker, progress, discoveredKeystones: null, knownVows: known);
        Assert.Equal(slots.Length, build.Skills.Count);
        return build;
    }

    [Fact]
    public void test_vow_of_the_pure_composed_through_the_loadout_is_kept_only_once_every_variation_is_chosen()
    {
        // Arrange / Act — the signature and the first road's skill, sworn to the vow, three ways.
        var bothChosen = Composed((Signature, "OPEN HAND"), (Blow, "FLATTEN"));
        var oneUnchosen = Composed((Signature, "OPEN HAND"), (Blow, null));
        var chosenApart = Composed((Signature, "SHUT FIST"), (Blow, "FLATTEN"));
        var starter = Composed((Signature, null));

        // Assert — the vow reaches the composed build once, and is kept exactly where every Source was decided.
        Assert.All(new[] { bothChosen, oneUnchosen, chosenApart, starter }, b => Assert.Single(b.Vows));
        Assert.True(Kept(bothChosen));
        Assert.False(Kept(oneUnchosen));
        Assert.False(Kept(chosenApart));
        Assert.False(Kept(starter));
    }

    // ── THE OTHER READERS agree ─────────────────────────────────────────────────────────────────

    [Fact]
    public void test_vow_of_the_pure_the_career_and_the_proof_read_the_same_kept_state()
    {
        // Career.VowWasKept (THE OATHBOUND's quest) and the vow proof (SoloExpedition, through
        // Vows.RuleHeld) both ask IsActive of the same context, so a default cannot keep the vow for
        // the quest while breaking it for the fight — or the other way round.
        var kept = Composed((Signature, "OPEN HAND"), (Blow, "FLATTEN"));
        var broken = Composed((Signature, "OPEN HAND"), (Blow, null));
        var hunter = new Hunter();

        Assert.True(Career.VowWasKept(kept, hunter));
        Assert.False(Career.VowWasKept(broken, hunter));
        Assert.True(Vows.RuleHeld(Pure, SoloBattle.DescribeBuild(kept, hunter)));
        Assert.False(Vows.RuleHeld(Pure, SoloBattle.DescribeBuild(broken, hunter)));
    }

    [Fact]
    public void test_vow_of_the_pure_an_empty_slot_does_not_participate()
    {
        // A capacity of four with one chosen skill in it: the empty slots are not skills, and they
        // neither keep the vow nor break it.
        var loadout = new PlayerLoadout { SkillCapacity = 4, VowCapacity = 1 };
        var progress = new SkillProgress();
        var slot = loadout.AddSkill();
        Assert.True(loadout.SetSkill(slot, Signature));
        Assert.True(loadout.SetVow(slot, Pure.Id, new[] { Pure }));
        for (var i = 0; i < SkillProgress.UsesForLevel(1); i++) progress.RecordWave(Signature);
        Assert.True(progress.ChooseVariation(SkillCatalogue.ById(Signature), "OPEN HAND"));
        loadout.AddSkill();   // opened, and left empty

        var build = loadout.ToBuild(Taught.Everything(), Seeker, progress, discoveredKeystones: null, knownVows: new[] { Pure });

        Assert.Single(build.Skills);
        Assert.True(Kept(build));
    }
}
