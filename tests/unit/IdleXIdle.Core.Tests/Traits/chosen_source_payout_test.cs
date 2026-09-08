using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Traits;
using Xunit;

namespace IdleXIdle.Core.Tests.Traits;

/// <summary>
/// CHOSEN SOURCE != RESOLVED DEFAULT SOURCE — the three readings of "one source", and who pays for which.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Build.ChosenSingleSource"/> — one or more skills, every one CHOSEN (its variation
///   taken), all the same Source. What THE SINGLE NOTE's payout and the mastery PURE node read.</item>
/// <item><see cref="Build.PureSource"/> — the same, with at least two skills. What awakens THE SINGLE
///   NOTE: a lone chosen skill is a decision the fight may pay, not yet a commitment between skills.</item>
/// <item><see cref="Build.DistinctChosenSources"/> — how many different Sources were CHOSEN. What MANY
///   TONGUES' payout and the MOTLEY waves that awaken it read.</item>
/// </list>
/// <para>
/// A slot is seeded BODY before its variation is chosen and the weave screen offers no lever on it,
/// so every fresh pair agrees on BODY by implementation. These tests pin that such agreement proves
/// nothing: it pays nothing, it awakens nothing, and it is not a voice.
/// </para>
/// </remarks>
public class chosen_source_payout_test
{
    private const string Signature = "sig_seeker_hard_hands";   // active; OPEN HAND = Body, SHUT FIST = Mind
    private const string Blow = "hammer_blow";                  // active; FLATTEN = Body, FINISH = Shadow
    private const string Press = "hammer_press";                // field;  CRUSHING = Body, PIN = Machine
    private const string Jaws = "snare_jaws";                   // reaction; NET = Shadow, IRON = Machine
    private const string Drink = "drain_drink";                 // active; SIPHON = Machine, GLUT = Nature
    private const string Spray = "volley_spray";                // active; SPLAY = Mind, CLUSTER = Body
    private const string Mire = "field_mire";                   // field;  NUMB = Nature, TEEMING = Spirit
    private const string Brand = "sign_brand";                  // field;  SPRAWL = Mind, ETCH = Spirit

    private static Build Woven(params EquippedSkill[] skills)
    {
        var b = new Build();
        foreach (var s in skills) Assert.True(b.Equip(s), $"{s.Def.Id} did not equip — the fixture, not the design");
        return b;
    }

    private static BuildContext Ctx(Build b) => SoloBattle.DescribeBuild(b, new Hunter());

    // ── THE MATRIX the brief asked for ──────────────────────────────────────────────────────────

    [Fact]
    public void test_chosen_source_one_skill_unchosen_is_nothing()
    {
        var b = Woven(TestBuilds.Skill(Signature, Source.Body));

        Assert.Null(b.ChosenSingleSource);
        Assert.Null(b.PureSource);
        Assert.Equal(0, b.DistinctChosenSources);
        Assert.Equal(1, Ctx(b).DistinctSources);          // the fight still casts Body
        Assert.Equal(0, Ctx(b).DistinctChosenSources);
    }

    [Fact]
    public void test_chosen_source_one_skill_chosen_is_a_single_chosen_source_but_not_a_commitment()
    {
        var b = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"));

        Assert.Equal(Source.Body, b.ChosenSingleSource);
        Assert.Null(b.PureSource);
        Assert.Equal(1, b.DistinctChosenSources);
    }

    [Fact]
    public void test_chosen_source_two_chosen_on_one_source_is_both()
    {
        var b = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(Press, "CRUSHING"));

        Assert.Equal(Source.Body, b.ChosenSingleSource);
        Assert.Equal(Source.Body, b.PureSource);
        Assert.Equal(1, b.DistinctChosenSources);
    }

    [Fact]
    public void test_chosen_source_two_chosen_apart_is_neither()
    {
        var b = Woven(TestBuilds.Chosen(Signature, "SHUT FIST"), TestBuilds.Chosen(Press, "CRUSHING"));

        Assert.Null(b.ChosenSingleSource);
        Assert.Null(b.PureSource);
        Assert.Equal(2, b.DistinctChosenSources);
    }

    [Fact]
    public void test_chosen_source_two_skills_one_unchosen_is_neither_even_when_the_seed_agrees()
    {
        var b = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Skill(Press, Source.Body));

        Assert.Equal(1, Ctx(b).DistinctSources);          // the fight agrees — by implementation
        Assert.Null(b.ChosenSingleSource);
        Assert.Null(b.PureSource);
        Assert.Equal(1, b.DistinctChosenSources);
    }

    [Fact]
    public void test_chosen_source_a_pair_on_the_body_seed_with_nothing_chosen_is_neither()
    {
        var b = Woven(TestBuilds.Skill(Signature, Source.Body), TestBuilds.Skill(Press, Source.Body));

        Assert.Null(b.ChosenSingleSource);
        Assert.Null(b.PureSource);
        Assert.Equal(0, b.DistinctChosenSources);
    }

    [Fact]
    public void test_chosen_source_every_kind_counts_when_chosen_signature_active_field_reaction()
    {
        // Pinned per kind, alone and paired: a chosen skill is a decision whatever slot it fills.
        Assert.Equal(Source.Body, Woven(TestBuilds.Chosen(Signature, "OPEN HAND")).ChosenSingleSource);   // signature
        Assert.Equal(Source.Body, Woven(TestBuilds.Chosen(Blow, "FLATTEN")).ChosenSingleSource);          // shared active
        Assert.Equal(Source.Body, Woven(TestBuilds.Chosen(Press, "CRUSHING")).ChosenSingleSource);        // field
        Assert.Equal(Source.Machine, Woven(TestBuilds.Chosen(Jaws, "IRON")).ChosenSingleSource);          // reaction

        Assert.Equal(Source.Machine, Woven(TestBuilds.Chosen(Drink, "SIPHON"), TestBuilds.Chosen(Jaws, "IRON")).PureSource);
        Assert.Null(Woven(TestBuilds.Chosen(Drink, "SIPHON"), TestBuilds.Chosen(Jaws, "NET")).ChosenSingleSource);
    }

    [Fact]
    public void test_chosen_source_four_chosen_to_four_sources_is_four_voices()
    {
        var b = Woven(TestBuilds.Chosen(Blow, "FLATTEN"), TestBuilds.Chosen(Spray, "SPLAY"),
                      TestBuilds.Chosen(Mire, "NUMB"), TestBuilds.Chosen(Brand, "ETCH"));

        Assert.Equal(4, b.DistinctChosenSources);
        Assert.Equal(4, Ctx(b).DistinctChosenSources);
    }

    [Fact]
    public void test_chosen_source_three_chosen_and_a_default_that_is_the_missing_colour_is_three_voices()
    {
        // Body, Mind, Nature chosen; the fourth slot sits on a SPIRIT seed. The resolved count says
        // four — exactly the coincidence a hidden trait fact must not reward.
        var b = Woven(TestBuilds.Chosen(Blow, "FLATTEN"), TestBuilds.Chosen(Spray, "SPLAY"),
                      TestBuilds.Chosen(Mire, "NUMB"), TestBuilds.Skill(Brand, Source.Spirit));

        Assert.Equal(4, Ctx(b).DistinctSources);
        Assert.Equal(3, b.DistinctChosenSources);
        Assert.Equal(3, Ctx(b).DistinctChosenSources);
    }

    // ── THE PAYOUTS: THE SINGLE NOTE and the mastery PURE node, in the real fight ───────────────

    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static float Dealt(Build build)
    {
        var crowd = Enumerable.Range(0, 4).Select(_ => new WaveCreature { MaxHealth = 900_000f, Health = 900_000f, Damage = 20f }).ToList();
        var m = new WaveMetrics();
        SoloBattle.ResolveWave(new Champion { MaxHealth = 200_000, Health = 200_000 }, build, new Hunter(), crowd,
                               900, NoSwing, new Random(11), metrics: m);
        return m.DeliveredDamage;
    }

    private static SkillShape SingleNote => TraitCatalogue.ById_("t_single_note").Shape(TraitContext.None);
    private static SkillShape PureNode => MasteryCatalog.ById("pure")!.Shape;

    private static void Pays(Func<SkillShape, Build> make, SkillShape shape, string what)
    {
        var without = Dealt(make(SkillShape.None));
        var with = Dealt(make(shape));
        Assert.True(with > without * 1.02f, $"{what}: {without:0.##} without, {with:0.##} with — the payout did not fire");
    }

    private static void Silent(Func<SkillShape, Build> make, SkillShape shape, string what)
    {
        var without = Dealt(make(SkillShape.None));
        var with = Dealt(make(shape));
        Assert.True(MathF.Abs(with - without) <= MathF.Max(1f, without * 0.001f),
            $"{what}: moved from {without:0.##} to {with:0.##} — a payout fired where no source was chosen");
    }

    private static Build Lone(SkillShape shape, bool chosen)
    {
        var b = new Build { Shape = shape, SlotCapacity = 1 };
        b.Equip(chosen ? TestBuilds.Chosen(Blow, "FLATTEN") : TestBuilds.Skill(Blow, Source.Body));
        return b;
    }

    private static Build Pair(SkillShape shape, string second)
    {
        var b = new Build { Shape = shape, SlotCapacity = 2 };
        b.Equip(TestBuilds.Chosen(Blow, "FLATTEN"));
        b.Equip(second switch
        {
            "chosen-same" => TestBuilds.Chosen(Spray, "CLUSTER"),      // Body
            "chosen-other" => TestBuilds.Chosen(Spray, "SPLAY"),       // Mind
            _ => TestBuilds.Skill(Spray, Source.Body),                 // unchosen, on the seed
        });
        return b;
    }

    [Fact]
    public void test_single_note_payout_follows_the_chosen_single_source()
    {
        Silent(s => Lone(s, chosen: false), SingleNote, "THE SINGLE NOTE on one unchosen skill");
        Pays(s => Lone(s, chosen: true), SingleNote, "THE SINGLE NOTE on one chosen skill");
        Pays(s => Pair(s, "chosen-same"), SingleNote, "THE SINGLE NOTE on two chosen skills, one source");
        Silent(s => Pair(s, "unchosen"), SingleNote, "THE SINGLE NOTE with one skill still on its default");
        Silent(s => Pair(s, "chosen-other"), SingleNote, "THE SINGLE NOTE on two sources");
    }

    [Fact]
    public void test_mastery_pure_follows_the_chosen_single_source()
    {
        Assert.True(PureNode.OneSourceBonus > 0f, "the PURE node's dial is not authored — the fixture, not the design");

        Silent(s => Lone(s, chosen: false), PureNode, "PURE on one unchosen skill");
        Pays(s => Lone(s, chosen: true), PureNode, "PURE on one chosen skill");
        Pays(s => Pair(s, "chosen-same"), PureNode, "PURE on two chosen skills, one source");
        Silent(s => Pair(s, "unchosen"), PureNode, "PURE with one skill still on its default");
        Silent(s => Pair(s, "chosen-other"), PureNode, "PURE on two sources");
    }

    // ── DISCOVERY stays stricter than payout ────────────────────────────────────────────────────

    [Fact]
    public void test_a_lone_chosen_skill_is_paid_by_the_awakened_trait_but_banks_no_pure_waves()
    {
        var lone = Lone(SkillShape.None, chosen: true);
        Assert.NotNull(lone.ChosenSingleSource);
        Assert.Null(lone.PureSource);

        var ledger = new TraitLedger();
        var watch = new TraitWatch(ledger) { CharacterId = "seeker", RegionId = Regions.All[0].Id };
        var run = new SoloExpedition(lone, new Champion { MaxHealth = 50_000, Health = 50_000 }, new Hunter(),
            enemyBaseHealth: 60f, enemyBaseDamage: 3f, ExpeditionTuning.Default, rng: new Random(5))
        { RegionId = Regions.All[0].Id, RunIndex = 1, TraitWatch = watch };
        for (var i = 0; i < 6 && !run.Over; i++) run.PushWave();

        Assert.True(run.Wave >= 1, "the fixture cleared no waves — the fixture, not the design");
        Assert.Equal(0d, ledger.Of(TraitCounter.PureWaves));
    }

    // ── MOTLEY WAVES: four real choices ─────────────────────────────────────────────────────────

    private static double MotleyWavesOf(Build build)
    {
        var ledger = new TraitLedger();
        var watch = new TraitWatch(ledger) { CharacterId = "seeker", RegionId = Regions.All[0].Id };
        var run = new SoloExpedition(build, new Champion { MaxHealth = 50_000, Health = 50_000 }, new Hunter(),
            enemyBaseHealth: 60f, enemyBaseDamage: 3f, ExpeditionTuning.Default, rng: new Random(7))
        { RegionId = Regions.All[0].Id, RunIndex = 1, TraitWatch = watch };
        for (var i = 0; i < 6 && !run.Over; i++) run.PushWave();
        Assert.True(run.Wave >= 1, "the fixture cleared no waves — the fixture, not the design");
        return ledger.Of(TraitCounter.MotleyWaves) / run.Wave;   // 1 = every cleared wave counted
    }

    [Fact]
    public void test_motley_waves_count_four_chosen_sources_and_not_three_plus_a_default()
    {
        var four = Woven(TestBuilds.Chosen(Blow, "FLATTEN"), TestBuilds.Chosen(Spray, "SPLAY"),
                         TestBuilds.Chosen(Mire, "NUMB"), TestBuilds.Chosen(Brand, "ETCH"));
        var threeAndSeed = Woven(TestBuilds.Chosen(Blow, "FLATTEN"), TestBuilds.Chosen(Spray, "SPLAY"),
                                 TestBuilds.Chosen(Mire, "NUMB"), TestBuilds.Skill(Brand, Source.Spirit));

        Assert.Equal(TraitDiscovery.MotleySources, four.DistinctChosenSources);
        Assert.Equal(1d, MotleyWavesOf(four));
        Assert.Equal(4, Ctx(threeAndSeed).DistinctSources);
        Assert.Equal(0d, MotleyWavesOf(threeAndSeed));
    }

    [Fact]
    public void test_motley_waves_a_signature_with_a_chosen_variation_is_one_of_the_four_voices()
    {
        // The seeker's OPEN HAND is Body; three shared skills chosen to Mind, Nature and Spirit.
        var four = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(Spray, "SPLAY"),
                         TestBuilds.Chosen(Mire, "NUMB"), TestBuilds.Chosen(Brand, "ETCH"));

        Assert.Equal(4, four.DistinctChosenSources);
        Assert.Equal(1d, MotleyWavesOf(four));
    }
}
