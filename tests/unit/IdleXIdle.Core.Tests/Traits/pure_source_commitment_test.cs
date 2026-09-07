using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Tests.Builds;
using IdleXIdle.Core.Traits;
using Xunit;

namespace IdleXIdle.Core.Tests.Traits;

/// <summary>
/// THE PURE-SOURCE COMMITMENT — what it takes for "every skill shares one source" to be a decision.
/// </summary>
/// <remarks>
/// <para>
/// The fact THE SINGLE NOTE counts used to be "every woven skill agrees with the first", and a build of
/// ONE skill agrees with itself. A new game hands out exactly one skill (<see cref="PlayerLoadout.Starter"/>),
/// so every fresh account banked PURE waves from its first wave — a commitment nobody had made, priced
/// into the trait's threshold by a retune that took the counter's word for it.
/// </para>
/// <para>
/// And a second skill does not fix that on its own: the weave screen sets no Source, every woven slot is
/// seeded BODY, and the only Source lever a player has is a VARIATION. So a signature and a fresh shared
/// skill agree on BODY before anyone has decided anything. <see cref="Build.PureSource"/> is now the one
/// reading: at least <see cref="Build.PureSourceMinimumSkills"/> woven skills, every one with its Source
/// CHOSEN — the clause VOW OF THE PURE's proof already uses (<see cref="VowTemptation.EveryWovenSourceChosen"/>),
/// which on its own has no minimum — all on one Source. These tests pin that contract at
/// the three places it matters — the query itself, the composed loadout that feeds it, and the telemetry
/// that reads it from real waves — and then pin that the correction takes nothing away from an account
/// that already earned something under the old reading.
/// </para>
/// </remarks>
public class pure_source_commitment_test
{
    private const string Seeker = "seeker";
    private const string Signature = "sig_seeker_hard_hands";     // the seeker's own; an ACTIVE. OPEN HAND = Body, SHUT FIST = Mind
    private const string AnotherSignature = "sig_anvil_hardface";  // somebody else's; also an ACTIVE
    private const string SharedActive = "hammer_blow";             // an active. FLATTEN = Body, FINISH = Shadow
    private const string SharedField = "hammer_press";             // a passive: a Field. CRUSHING = Body, PIN = Machine
    private const string SharedReaction = "snare_jaws";            // a passive: a Reaction. NET = Shadow, IRON = Machine
    private const string MachineActive = "drain_drink";            // an active. SIPHON = Machine, GLUT = Nature
    private const string ThirdActive = "volley_spray";             // an active. SPLAY = Mind, CLUSTER = Body
    private const string BodyField = "drain_wilt";                 // a passive: a Field. SUP = Body, SHRIVEL = Mind

    private static readonly TraitFirst Somewhere = new(Seeker, "cinderworks", 3);

    private static Build Woven(params EquippedSkill[] skills)
    {
        var b = new Build();
        foreach (var s in skills) Assert.True(b.Equip(s), $"{s.Def.Id} did not equip — the fixture, not the design");
        return b;
    }

    /// <summary>The fight's RESOLVED count of Sources, defaults included — what VOW OF THE PURE's demand reads.</summary>
    private static int DistinctSourcesOf(Build b) => SoloBattle.DescribeBuild(b, new Hunter()).DistinctSources;

    // ── THE QUERY: Build.PureSource ─────────────────────────────────────────────────────────────

    [Fact]
    public void test_pure_source_one_woven_skill_is_not_a_commitment_even_with_its_source_chosen()
    {
        // Arrange — the shape a new game hands out, with the one Source decision it allows already made.
        var build = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"));

        // Act / Assert — one decided skill agrees with itself, and that is not a commitment.
        Assert.Equal(1, build.Skills.Count);
        Assert.NotNull(build.Skills[0].Variation);
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_two_chosen_skills_on_one_source_commit_to_it()
    {
        // Arrange — two skills, both with their Source decided, and decided the same way.
        var build = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING"));

        // Act / Assert
        Assert.Equal(Source.Body, build.PureSource);
    }

    [Fact]
    public void test_pure_source_two_chosen_skills_on_different_sources_commit_to_nothing()
    {
        // Arrange — the same pair, decided apart.
        var build = Woven(TestBuilds.Chosen(Signature, "SHUT FIST"), TestBuilds.Chosen(SharedField, "CRUSHING"));

        // Act / Assert
        Assert.Equal(Source.Mind, build.Skills[0].Source);
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_an_empty_build_commits_to_nothing()
    {
        Assert.Null(new Build().PureSource);
    }

    [Fact]
    public void test_pure_source_the_minimum_is_two_because_a_new_game_opens_one_slot()
    {
        // The number is read off the ladder, not picked: one slot at a new game, so the first skill
        // there is to agree WITH is the second. A build must have at least that many to be committed.
        Assert.Equal(2, Build.PureSourceMinimumSkills);
        Assert.Equal(1, Unlocks.SkillSlots(new UnlockFacts()));
    }

    [Fact]
    public void test_pure_source_a_pair_on_the_body_default_with_nothing_chosen_is_not_a_commitment()
    {
        // Arrange — exactly what the shipped weave produces the moment a second skill is woven: both
        // slots seeded BODY, no variation on either. The fight IS mono-Source here — and that is the
        // point: nobody decided it.
        var build = Woven(TestBuilds.Skill(Signature, Source.Body), TestBuilds.Skill(SharedField, Source.Body));

        // Act / Assert
        Assert.All(build.Skills, s => Assert.Null(s.Variation));
        Assert.Equal(1, DistinctSourcesOf(build));
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_one_chosen_and_one_default_on_the_same_source_is_not_yet_a_commitment()
    {
        // Arrange — half a decision, either way round.
        var first = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Skill(SharedField, Source.Body));
        var second = Woven(TestBuilds.Skill(Signature, Source.Body), TestBuilds.Chosen(SharedField, "CRUSHING"));

        // Act / Assert
        Assert.Null(first.PureSource);
        Assert.Null(second.PureSource);
    }

    [Fact]
    public void test_pure_source_an_unchosen_third_skill_holds_the_commitment_open_until_it_is_chosen()
    {
        // Arrange — a committed pair takes on a third skill: fresh, on the BODY default. The vow's rule
        // (every woven Source chosen) and this one agree: the build is not committed again until the
        // player says what the new skill is made of — and then it depends on what they say.
        var open = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING"),
                         TestBuilds.Skill(ThirdActive, Source.Body));
        var agreed = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING"),
                           TestBuilds.Chosen(ThirdActive, "CLUSTER"));
        var dissented = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING"),
                              TestBuilds.Chosen(ThirdActive, "SPLAY"));

        // Act / Assert
        Assert.Null(open.PureSource);
        Assert.Equal(Source.Body, agreed.PureSource);
        Assert.Null(dissented.PureSource);
    }

    [Fact]
    public void test_pure_source_signature_plus_a_chosen_shared_active_on_one_source_is_a_commitment()
    {
        // The signature is a woven skill like the shared twelve — it takes part, and one chosen shared
        // skill beside it is enough. Pinned with a shared ACTIVE, so the kind of the second skill is not
        // what makes the pair count. This is also the first pair the shipped ladder can produce: the
        // first mastery road teaches an active.
        var build = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedActive, "FLATTEN"));

        Assert.Equal(Source.Body, build.PureSource);
    }

    [Fact]
    public void test_pure_source_a_passive_and_an_active_on_one_source_is_a_commitment()
    {
        // Fields and Reactions draw their Source on every hit exactly as an Active does, so both take
        // part in the commitment — and both can break it.
        var field = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING"));
        var reaction = Woven(TestBuilds.Chosen(MachineActive, "SIPHON"), TestBuilds.Chosen(SharedReaction, "IRON"));
        var broken = Woven(TestBuilds.Chosen(MachineActive, "SIPHON"), TestBuilds.Chosen(SharedReaction, "NET"));

        Assert.False(TestBuilds.Skill(SharedField).TakesABeat, "PRESS should be a passive — the fixture, not the design");
        Assert.False(TestBuilds.Skill(SharedReaction).TakesABeat, "JAWS should be a passive — the fixture, not the design");
        Assert.Equal(Source.Body, field.PureSource);
        Assert.Equal(Source.Machine, reaction.PureSource);
        Assert.Null(broken.PureSource);
    }

    [Fact]
    public void test_pure_source_four_chosen_skills_on_one_source_commit_to_it_and_one_dissenter_breaks_it()
    {
        var pure = Woven(
            TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedActive, "FLATTEN"),
            TestBuilds.Chosen(SharedField, "CRUSHING"), TestBuilds.Chosen(BodyField, "SUP"));
        var split = Woven(
            TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedActive, "FLATTEN"),
            TestBuilds.Chosen(SharedField, "CRUSHING"), TestBuilds.Chosen(BodyField, "SHRIVEL"));

        Assert.Equal(Source.Body, pure.PureSource);
        Assert.Null(split.PureSource);
    }

    [Fact]
    public void test_pure_source_recognition_is_never_wider_than_what_the_fight_pays()
    {
        // THE SINGLE NOTE's payout and the mastery PURE node read Build.ChosenSingleSource — chosen,
        // defaults excluded. Every build the commitment names must be one the payout fires on, or a
        // player could awaken the trait on a build it refuses to pay. (VOW OF THE PURE's demand reads
        // the RESOLVED count and holds on these too; that is a separate promise, checked separately.)
        var committed = new[]
        {
            Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING")),
            Woven(TestBuilds.Chosen(MachineActive, "SIPHON"), TestBuilds.Chosen(SharedReaction, "IRON")),
            Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedActive, "FLATTEN"),
                  TestBuilds.Chosen(SharedField, "CRUSHING"), TestBuilds.Chosen(BodyField, "SUP")),
        };

        Assert.All(committed, b =>
        {
            Assert.NotNull(b.PureSource);
            Assert.Equal(b.PureSource, b.ChosenSingleSource);   // the payout predicate, and the same Source
            Assert.Equal(1, DistinctSourcesOf(b));                // and the vow's resolved demand holds as well
        });
    }

    [Fact]
    public void test_pure_source_is_stricter_than_vow_of_the_pures_proof_by_exactly_the_minimum()
    {
        // VOW OF THE PURE's proof is the demand (one Source) held while every woven Source is chosen —
        // and it has NO minimum: a lone signature with its variation taken proves it. The commitment
        // borrows the chosen clause and adds the two-skill floor, so the two are not interchangeable,
        // and this pins the one build class they disagree on.
        var vow = Vows.ById("vow_pure");
        Assert.NotNull(vow);
        var tempted = new VowTemptationFacts(EveryWovenSourceChosen: true);

        var one = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"));
        var two = Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING"));

        Assert.True(Vows.RuleHeld(vow, SoloBattle.DescribeBuild(one, new Hunter())));
        Assert.True(Vows.WasTempted(vow!, tempted));
        Assert.Null(one.PureSource);

        Assert.True(Vows.RuleHeld(vow, SoloBattle.DescribeBuild(two, new Hunter())));
        Assert.Equal(Source.Body, two.PureSource);
    }

    // ── THE COMPOSED LOADOUT: what reaches the query ────────────────────────────────────────────

    private static Character TheSeeker => CharacterRoster.Get(Seeker);

    private static Build Composed(MasteryTree mastery, int slots, SkillProgress? progress, params BuildComposer.SkillPick[] picks)
        => BuildComposer.Compose(mastery, TheSeeker, picks, Array.Empty<string>(), slots, progress);

    private static BuildComposer.SkillPick Pick(string id, bool passive) => new(Source.Body, null, passive, id);

    /// <summary>Progress with the named variations taken — each skill levelled once, exactly as play would.</summary>
    private static SkillProgress Choosing(params (string SkillId, string Variation)[] choices)
    {
        var progress = new SkillProgress();
        foreach (var (id, variation) in choices)
        {
            var def = SkillCatalogue.ById(id);
            for (var i = 0; i < SkillProgress.UsesForLevel(1); i++) progress.RecordWave(id);
            Assert.True(progress.ChooseVariation(def, variation), $"{variation} should be choosable at level 1 — the fixture, not the design");
        }
        return progress;
    }

    [Fact]
    public void test_pure_source_the_starter_loadout_of_a_new_game_is_not_a_commitment()
    {
        // Arrange — THE fresh save: the starter loadout, composed the way the game composes it, against
        // a mastery tree that has taught nothing yet.
        var mastery = new MasteryTree();
        var loadout = PlayerLoadout.Starter(TheSeeker);

        // Act
        var build = loadout.ToBuild(mastery, TheSeeker);

        // Assert — one skill fights, and it is not a commitment to anything.
        Assert.Empty(mastery.AvailableSkills());
        Assert.Equal(1, build.Skills.Count);
        Assert.Equal(Signature, build.Skills[0].Def.Id);
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_an_empty_slot_is_not_a_second_source_and_not_a_second_skill()
    {
        // Arrange — the second slot has opened and nothing has been put in it. Its woven-source
        // fallback is set to the signature's source, which is exactly the case that would invent a
        // "commitment" if empty slots were read as sources.
        var empty = new BuildComposer.SkillPick(Source.Body, null, null, SkillId: null);

        // Act
        var build = Composed(Taught.Everything(), slots: 2, Choosing((Signature, "OPEN HAND")),
            Pick(Signature, passive: false), empty);

        // Assert
        Assert.Equal(1, build.Skills.Count);
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_a_shared_skill_no_road_reaches_composes_as_nothing_and_cannot_commit()
    {
        // Arrange — a save naming a shared skill beside the signature, both variations chosen on the same
        // Source, against a mastery tree that does not reach the shared one (a respec took the road back,
        // or it was never bought). The composer refuses it, so a loadout that LOOKS like a committed pair
        // fights as one skill.
        var untaught = new MasteryTree();

        // Act
        var build = Composed(untaught, slots: 2, Choosing((Signature, "OPEN HAND"), (SharedField, "CRUSHING")),
            Pick(Signature, passive: false), Pick(SharedField, passive: true));

        // Assert
        Assert.Equal(1, build.Skills.Count);
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_another_champions_signature_composes_as_nothing_and_cannot_commit()
    {
        // Arrange — a slot left behind by a champion switch, holding a signature this champion may not
        // use, chosen to the same Source as its own. Ownership is refused before anything else is asked.
        var build = Composed(Taught.Everything(), slots: 2, Choosing((Signature, "OPEN HAND"), (AnotherSignature, "UPSET")),
            Pick(Signature, passive: false), Pick(AnotherSignature, passive: false));

        // Assert
        Assert.Equal(Source.Body, TestBuilds.SourceOf(AnotherSignature, "UPSET"));
        Assert.Equal(1, build.Skills.Count);
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_an_id_the_catalogue_does_not_know_composes_as_nothing_and_cannot_commit()
    {
        // Arrange — a save (or a share code) naming a skill that no longer exists, beside the signature.
        var build = Composed(Taught.Everything(), slots: 2, Choosing((Signature, "OPEN HAND")),
            Pick(Signature, passive: false), Pick("not_a_skill", passive: true));

        // Assert
        Assert.Equal(1, build.Skills.Count);
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_skills_composed_without_a_chosen_variation_carry_no_decision()
    {
        // Arrange — the shipped second-skill moment, through the real composer: two picks on the BODY
        // seed, no progress spent on either.
        var build = Composed(Taught.Everything(), slots: 2, progress: null,
            Pick(Signature, passive: false), Pick(SharedField, passive: true));

        // Assert — both fight as Body, neither was decided, so nothing is committed.
        Assert.Equal(2, build.Skills.Count);
        Assert.All(build.Skills, s => Assert.Equal(Source.Body, s.Source));
        Assert.All(build.Skills, s => Assert.Null(s.Variation));
        Assert.Null(build.PureSource);
    }

    [Fact]
    public void test_pure_source_chosen_variations_reach_the_composed_build_and_decide_it()
    {
        // Arrange / Act — the same two picks, with the variations a player takes at level 1.
        var body = Composed(Taught.Everything(), slots: 2, Choosing((Signature, "OPEN HAND"), (SharedField, "CRUSHING")),
            Pick(Signature, passive: false), Pick(SharedField, passive: true));
        var split = Composed(Taught.Everything(), slots: 2, Choosing((Signature, "SHUT FIST"), (SharedField, "CRUSHING")),
            Pick(Signature, passive: false), Pick(SharedField, passive: true));

        // Assert — the variation rides on the equipped skill, owns its Source, and decides the commitment.
        Assert.Equal("OPEN HAND", body.Skills[0].Variation?.Name);
        Assert.Equal(Source.Body, body.PureSource);
        Assert.Equal(Source.Mind, split.Skills[0].Source);
        Assert.Null(split.PureSource);
    }

    // ── THE TELEMETRY: real waves, real ledger ──────────────────────────────────────────────────

    private static (SoloExpedition Run, TraitLedger Ledger) Descend(Build build, int waves, int seed = 11)
    {
        var ledger = new TraitLedger();
        var watch = new TraitWatch(ledger) { CharacterId = Seeker, RegionId = Regions.All[0].Id };
        var hunter = new Hunter();
        // Generous health and soft enemies, so every pushed wave is a CLEARED wave and the count of
        // cleared waves is the count of facts fed — the assertion is about what each fact said.
        var run = new SoloExpedition(build, new Champion { MaxHealth = 50_000, Health = 50_000 }, hunter,
            enemyBaseHealth: 60f, enemyBaseDamage: 3f, ExpeditionTuning.Default, rng: new Random(seed))
        {
            RegionId = Regions.All[0].Id,
            RunIndex = 1,
            TraitWatch = watch,
        };
        for (var i = 0; i < waves && !run.Over; i++) run.PushWave();
        Assert.True(run.Wave > 0, "the fixture cleared no waves — the fixture, not the design");
        return (run, ledger);
    }

    [Fact]
    public void test_pure_source_telemetry_a_one_skill_starter_banks_no_pure_waves()
    {
        // Arrange / Act — the starter's one skill, through the real expedition and the real ledger.
        var (run, ledger) = Descend(Woven(TestBuilds.Skill(Signature, Source.Body)), waves: 8);

        // Assert — waves were cleared, and not one of them was a PURE wave.
        Assert.True(run.Wave >= 1);
        Assert.Equal(0d, ledger.Of(TraitCounter.PureWaves));
    }

    [Fact]
    public void test_pure_source_telemetry_a_pair_on_the_body_default_banks_no_pure_waves()
    {
        // Arrange / Act — the shipped second-skill moment: two skills, nothing chosen, both on the seed.
        var (run, ledger) = Descend(
            Woven(TestBuilds.Skill(Signature, Source.Body), TestBuilds.Skill(SharedField, Source.Body)), waves: 8);

        // Assert
        Assert.True(run.Wave >= 1);
        Assert.Equal(0d, ledger.Of(TraitCounter.PureWaves));
    }

    [Fact]
    public void test_pure_source_telemetry_a_chosen_matched_pair_banks_every_cleared_wave()
    {
        // Arrange / Act
        var (run, ledger) = Descend(
            Woven(TestBuilds.Chosen(Signature, "OPEN HAND"), TestBuilds.Chosen(SharedField, "CRUSHING")), waves: 8);

        // Assert — one PURE wave per cleared wave, no more and no fewer.
        Assert.Equal(run.Wave, (int)ledger.Of(TraitCounter.PureWaves));
    }

    [Fact]
    public void test_pure_source_telemetry_a_chosen_split_pair_banks_no_pure_waves()
    {
        // Arrange / Act
        var (run, ledger) = Descend(
            Woven(TestBuilds.Chosen(Signature, "SHUT FIST"), TestBuilds.Chosen(SharedField, "CRUSHING")), waves: 8);

        // Assert
        Assert.True(run.Wave >= 1);
        Assert.Equal(0d, ledger.Of(TraitCounter.PureWaves));
    }

    // ── MIGRATION: nothing earned under the old reading is taken back ───────────────────────────

    /// <summary>A cleared wave that says nothing except whether its build was committed.</summary>
    private static TraitWaveFacts Quiet(bool committed) => new(
        ChampionMaxHealth: 1_000,
        HeavyHits: 0, Overkill: 0f, ShieldGained: 0f, ShieldBreaks: 0, ShieldAbsorbed: 0f, Healed: 0,
        ReflectedDamage: 0f, LowestHealthFraction: 1f, HealthLost: 1, CreaturesKilled: 0, MarkCasts: 0,
        CarriedWideSkill: false, CreaturesPresent: 1, CritPercent: 0f,
        PureSourceBuild: committed, DistinctChosenSources: 1, VowsKept: 0);

    private static double Threshold => TraitCatalogue.ById_("t_single_note").Discovery.Threshold;

    [Fact]
    public void test_pure_source_migration_an_awakened_single_note_stays_awakened_under_the_new_reading()
    {
        // Arrange — a save that earned THE SINGLE NOTE under the old reading, restored as the game
        // restores it: the discovered id and the persisted counter, no threshold re-checked.
        var ledger = new TraitLedger();
        ledger.Restore(
            discovered: new[] { "t_single_note" },
            tally: new[] { new KeyValuePair<string, double>(nameof(TraitCounter.PureWaves), Threshold) },
            loadouts: null);
        Assert.True(ledger.Has("t_single_note"));

        // Act — the account goes on playing a build that is NOT committed under the new reading.
        for (var w = 0; w < 25; w++)
            TraitDiscovery.OnWaveCleared(ledger, Quiet(committed: false), TraitAccount.None, Somewhere);
        TraitDiscovery.Evaluate(ledger, TraitAccount.None, Somewhere);

        // Assert — still awakened, and the counter it was earned on is exactly where the save left it.
        Assert.True(ledger.Has("t_single_note"));
        Assert.Equal(Threshold, ledger.Of(TraitCounter.PureWaves));
    }

    [Fact]
    public void test_pure_source_migration_stored_progress_is_kept_and_only_committed_waves_add_to_it()
    {
        // Arrange — a save one wave short of the threshold, banked under the old reading.
        var ledger = new TraitLedger();
        ledger.Restore(
            discovered: null,
            tally: new[] { new KeyValuePair<string, double>(nameof(TraitCounter.PureWaves), Threshold - 1) },
            loadouts: null);

        // Act 1 — uncommitted waves under the new reading: nothing is subtracted and nothing is added.
        for (var w = 0; w < 10; w++)
            TraitDiscovery.OnWaveCleared(ledger, Quiet(committed: false), TraitAccount.None, Somewhere);

        // Assert 1
        Assert.Equal(Threshold - 1, ledger.Of(TraitCounter.PureWaves));
        Assert.False(ledger.Has("t_single_note"));

        // Act 2 — ONE committed wave finishes what the old save started.
        var woke = TraitDiscovery.OnWaveCleared(ledger, Quiet(committed: true), TraitAccount.None, Somewhere);

        // Assert 2
        Assert.Contains("t_single_note", woke);
        Assert.Equal(Threshold, ledger.Of(TraitCounter.PureWaves));
    }
}
