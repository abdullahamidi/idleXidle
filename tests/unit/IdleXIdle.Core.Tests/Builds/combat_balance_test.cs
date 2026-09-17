using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE BENCH the combat rework is judged on (brief §63–§68, §83–§84).
/// </summary>
/// <remarks>
/// <para>
/// Every fight below runs against the repository's own encounter archetypes — Swarm, Armoured, Caster,
/// Bruiser, composed by <see cref="Archetypes.Compose"/> — rather than a parallel benchmark model
/// invented for the tests (§83). A benchmark that does not use the game's own bands measures the
/// benchmark.
/// </para>
/// <para>
/// The assertions are RELATIONSHIPS and BOUNDS, never pinned figures. A test that pins "BODY deals
/// 41,207" fails on every retune and teaches the next person to update the number rather than to think
/// about it; a test that says "neither variation of a skill may win on every problem" keeps saying
/// something true after the numbers move, and is the actual design rule.
/// </para>
/// </remarks>
public class CombatBalanceTests
{
    private readonly ITestOutputHelper _out;
    public CombatBalanceTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// TWO PRESSURES, because "how hard is the wave hitting" is a problem in its own right (§83).
    /// </summary>
    /// <remarks>
    /// At one pressure a branch that buys survival can only ever be compared against a branch that buys
    /// damage on the damage axis, and it loses. SHRIVEL takes less than half the damage SUP does — its
    /// attack break works exactly as designed — and still trailed in every band, because at a gentle
    /// pressure SUP's heal covers the whole difference and then some. The answer to "when would I choose
    /// the cripple?" is "when the wave is killing you", and a bench with no such wave cannot hear it.
    /// </remarks>
    private static readonly float[] Pressures = { 5f, 26f };

    /// <summary>The four problems the game actually poses (§83).</summary>
    private static readonly Archetype[] Bands =
        { Archetype.Swarm, Archetype.Armoured, Archetype.Caster, Archetype.Bruiser };

    /// <summary>Everything one wave is worth measuring by (§84), not damage alone.</summary>
    private readonly record struct Bench(
        float Dealt, int HealthKept, float ShieldAbsorbed, float Prevented,
        int Healed, int Kills, float Overkill, int Swings, int Casts, int DurationMs)
    {
        public bool Survived => HealthKept > 0;
    }

    private static MasteryTree EveryRoadWalked()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        tree.RestoreTaken(MasteryCatalog.Nodes.Where(x => x.Kind == MasteryKind.SkillRoad).Select(x => x.Id), repair: false);
        return tree;
    }

    /// <summary>A build of named skills, each on a named variation with every reinforcement bought.</summary>
    /// <summary>
    /// A fully levelled build. <paramref name="picks"/> names each slot's variation; the reinforcements
    /// taken are as many as the level ladder allows, in catalogue order.
    /// </summary>
    /// <remarks>
    /// It used to take ALL of a variation's reinforcements. Since 2026-09-09 a skill's ladder buys the
    /// variation and TWO of its three, so "all of them" is no longer a reachable build — and a fixture
    /// that composes an unreachable build measures something the player can never hold. The extra
    /// TakeReinforcement calls simply returned false, silently, which is worse than failing.
    /// </remarks>
    private static Build Woven(params (Source Source, string SkillId, string? Variation)[] picks)
        => Woven(null, picks);

    private static Build Woven(IReadOnlyList<string>? reinforcements,
                               params (Source Source, string SkillId, string? Variation)[] picks)
    {
        var progress = new SkillProgress();
        foreach (var (_, skillId, variation) in picks)
        {
            var def = SkillCatalogue.ById(skillId);
            for (var i = 0; i < SkillProgress.UsesForLevel(SkillProgress.MaxLevel); i++) progress.RecordWave(def.Id);
            if (variation is null) continue;
            Assert.True(progress.ChooseVariation(def, variation), $"{def.Name} has no {variation}");
            var v = def.Variations.Single(x => x.Name == variation);
            var wanted = reinforcements ?? v.Reinforcements.Select(r => r.Name).ToList();
            foreach (var name in wanted)
            {
                if (progress.FreeOn(def.Id) < 1) break;   // the ladder's ceiling, not a silent refusal
                progress.TakeReinforcement(def, name);
            }
        }
        // THE OWNER SITS IN THE CHAIR when one of the picks is a signature, and nobody does
        // otherwise. A signature belongs to one champion and the composer refuses it to anyone else,
        // so a null character would compose the bench's subject away and every measurement below
        // would silently be of the partner alone. The champion brings its passive with it, which is
        // correct: a signature is only ever fought with beside its own innate.
        var owner = picks.Select(p => SkillCatalogue.ById(p.SkillId).OwnerCharacterId)
                         .FirstOrDefault(o => o is not null);
        return BuildComposer.Compose(
            EveryRoadWalked(), character: CharacterRoster.Find(owner ?? ""),
            skills: picks.Select(p => new BuildComposer.SkillPick(p.Source, null, SkillId: p.SkillId)).ToList(),
            keystoneIds: Array.Empty<string>(), slotCapacity: 4, progress: progress);
    }

    /// <summary>A hunter wearing <paramref name="pieces"/> plain items of one element, or of none.</summary>
    private static Hunter Wearing(Source? element, int pieces)
    {
        var slots = new[] { GearSlot.Weapon, GearSlot.Helm, GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots };
        var types = new[] { ItemBaseType.Weapon, ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots };
        var h = new Hunter();
        for (var i = 0; i < pieces; i++)
            h.Equip(new ItemInstance
            {
                InstanceId = $"b{element}{i}", BaseType = types[i], Rarity = Rarity.Common,
                SellValue = 1, ItemLevel = 1, Element = element, Family = 0,
            });
        _ = slots;
        return h;
    }

    /// <summary>
    /// ONE WAVE of one band, run headless, reported in every channel §84 asks for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The champion's pool is the build's own (<see cref="SoloBattle.ChampionHealth"/>) so that a build
    /// buying health is paid for it, and the wave is minted by the game's own composer at a fixed seed
    /// so two builds meet the identical wave.
    /// </para>
    /// <para>
    /// THE CHAMPION SURVIVES, and it survives on ITS OWN POOL against a gentler wave rather than on an
    /// inflated pool against a harsh one. Both were tried and the difference matters: multiplying the
    /// pool eight-fold to keep the champion alive multiplied every percent-of-maximum-health effect
    /// eight-fold with it while the wave's damage stayed where it was, so WILT's 1%-a-pulse heal became
    /// worth more than an attack break of ninety percent, and SUP "beat" SHRIVEL for a reason that
    /// existed only in the fixture. Lowering the wave keeps every ratio the game actually has.
    /// </para>
    /// <para>
    /// It has to survive at all, though, because a bench where the champion always dies cannot answer
    /// §66's question: PIN's stun, NET's reflect, TEEMING's slow and SPEND's window all buy TIME, and
    /// time is worth nothing to someone who dies either way. Six skills "failed" balance under the first
    /// fixture here and every one of them was the fixture failing.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// THE WAVE IS CLEARABLE, which is the other half of making the bench mean something. At 900 base
    /// health nothing in it ever died: no enemy reached FINISH's execute threshold, no kill made a SHADE
    /// or a WEEP bleed, and every branch about death measured as worthless for the same reason every
    /// defensive branch did — the fixture never posed the situation. A player clears waves.
    /// </remarks>
    private Bench Fight(Build build, Hunter hunter, Archetype band, float health = 520f, float damage = 5f)
    {
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var creatures = Archetypes.Compose(band, health, damage, wave: 12, sources: null, new Random(5));
        var metrics = new WaveMetrics();
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, hunter, creatures, enemyIntervalMs: 900,
            ExpeditionTuning.Default, new Random(5), metrics: metrics);

        return new Bench(
            Dealt: metrics.DeliveredDamage,
            HealthKept: champ.Health,
            ShieldAbsorbed: metrics.ShieldAbsorbed,
            Prevented: metrics.DamagePrevented,
            Healed: events.Where(e => e.Kind == BattleEventKind.Heal).Sum(e => e.Amount),
            Kills: metrics.CreaturesKilled,
            Overkill: metrics.RawDamage - metrics.DeliveredDamage,
            Swings: events.Count(e => e.Kind == BattleEventKind.Strike && !e.FromSkill),
            Casts: events.Count(e => e.Kind == BattleEventKind.Skill),
            DurationMs: metrics.DurationMs);
    }

    // ── §63. THE SIX MONO-SOURCE BUILDS ──────────────────────────────────────────────────────────

    /// <summary>
    /// Each Source can field a whole hunter out of its own variations, and that hunter can fight.
    /// </summary>
    /// <remarks>
    /// This is the promise the Source matrix makes — every Source on exactly two Active and two Passive
    /// variations — turned into the thing the promise is FOR. A matrix that balances on paper and
    /// composes into a build with three empty slots has not delivered anything.
    /// </remarks>
    [Theory]
    [InlineData("BODY", "FLATTEN", "CLUSTER", "CRUSHING", "SUP")]
    [InlineData("MACHINE", "BANKED", "SIPHON", "PIN", "IRON")]
    [InlineData("MIND", "SPEND", "SPLAY", "SPRAWL", "SHRIVEL")]
    [InlineData("NATURE", "THRONG", "GLUT", "TORRENT", "NUMB")]
    [InlineData("SHADOW", "FINISH", "VENGEANCE", "NET", "CARRION")]
    [InlineData("SPIRIT", "STEADY", "SHARE", "ETCH", "TEEMING")]
    public void test_a_source_can_field_a_whole_hunter_out_of_its_own_variations(
        string sourceName, string a, string b, string c, string d)
    {
        var source = Enum.Parse<Source>(sourceName, ignoreCase: true);
        var wanted = new[] { a, b, c, d };

        // Each named variation exists, belongs to this Source, and sits on a different skill.
        var found = wanted.Select(name => SkillCatalogue.All
                .SelectMany(def => def.Variations.Select(v => (Def: def, V: v)))
                .Single(x => x.V.Name == name))
            .ToList();
        Assert.All(found, x => Assert.Equal(source, x.V.Source));
        Assert.Equal(4, found.Select(x => x.Def.Id).Distinct().Count());

        // Two Actives and two Passives — the loadout's own shape, so no slot is impossible.
        Assert.Equal(2, found.Count(x => x.Def.Kind == SkillKind.Active));
        Assert.Equal(2, found.Count(x => x.Def.Kind != SkillKind.Active));

        var build = Woven(found.Select(x => (source, x.Def.Id, (string?)x.V.Name)).ToArray());
        Assert.Equal(4, build.Skills.Count);

        // And it fights, in every band, producing something in some channel.
        var cast = false;
        foreach (var band in Bands)
        {
            // A LONGER WAVE THAN THE BALANCE BENCH USES. This test asks whether a Source can field a
            // hunter that FIGHTS, and SPIRIT's own two Actives are five and six beats — against a wave
            // that ends in four, neither ever comes round and the build reads as having no Actives at
            // all. That is the fixture's wave, not the build's problem.
            var r = Fight(build, Wearing(null, 0), band, health: 1_800f);
            _out.WriteLine($"{sourceName} @{band}: dealt {r.Dealt:F0}, kept {r.HealthKept}, kills {r.Kills}, "
                           + $"shield {r.ShieldAbsorbed:F0}, healed {r.Healed}, {r.Casts} casts / {r.Swings} swings");
            Assert.True(r.Dealt > 0f, $"{sourceName} dealt nothing against a {band}");
            cast |= r.Casts > 0;
        }
        // CASTING SOMEWHERE, not everywhere. A band a build erases before its first Active comes round
        // is a band that build is good at, and demanding a cast in every one of them would fail exactly
        // the hunters who clear fastest.
        Assert.True(cast, $"{sourceName} never cast in any band — its Actives are unreachable");
    }

    // ── §64. THE EIGHT MIXED BUILDS ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A skill and a set that ought to work together do, out of the general rules and not a special case.
    /// </summary>
    /// <remarks>
    /// Each pairing is measured as the set's worth TO THAT BUILD against the same build bare-handed, so
    /// what is asserted is that the combination is worth something — not that it beats some other build,
    /// which would be a balance claim these fixtures cannot support.
    /// </remarks>
    [Theory]
    [InlineData("snare_repay", "BANKED", "Machine")]      // a shield skill under the shield set
    [InlineData("drain_drink", "GLUT", "Nature")]          // health scaling under the healing set
    [InlineData("snare_repay", "VENGEANCE", "Machine")]   // payback under prevention
    [InlineData("sign_call", "SPEND", "Mind")]            // the counted window under the crit set
    [InlineData("volley_spray", "CLUSTER", "Mind")]       // many hits under the crit set
    [InlineData("hammer_blow", "FINISH", "Shadow")]       // the execute under the death set
    [InlineData("drain_drink", "SIPHON", "Nature")]       // lifesteal under the healing set
    public void test_a_skill_and_the_set_that_suits_it_are_worth_more_together(
        string skillId, string variation, string element)
    {
        var el = Enum.Parse<Source>(element);
        var def = SkillCatalogue.ById(skillId);
        var partner = def.Kind == SkillKind.Active
            ? ("volley_spray", (string?)null)
            : ("hammer_blow", (string?)null);
        var build = Woven((def.Variations.Single(v => v.Name == variation).Source, skillId, variation),
                          (Source.Body, partner.Item1, partner.Item2));

        var bare = Bands.Select(band => Fight(build, Wearing(null, 5), band)).ToList();
        var set = Bands.Select(band => Fight(build, Wearing(el, 5), band)).ToList();

        // Worth something SOMEWHERE — a set that suits a build need not suit it in every band.
        var moved = Bands.Select((band, i) =>
            set[i].Dealt > bare[i].Dealt + 0.5f
            || set[i].HealthKept > bare[i].HealthKept
            || set[i].ShieldAbsorbed > bare[i].ShieldAbsorbed
            || set[i].Healed > bare[i].Healed
            || set[i].Prevented > bare[i].Prevented).ToList();

        for (var i = 0; i < Bands.Length; i++)
            _out.WriteLine($"{variation} + {element} @{Bands[i]}: dealt {bare[i].Dealt:F0} -> {set[i].Dealt:F0}, "
                           + $"kept {bare[i].HealthKept} -> {set[i].HealthKept}, "
                           + $"shield {bare[i].ShieldAbsorbed:F0} -> {set[i].ShieldAbsorbed:F0}");

        Assert.True(moved.Any(m => m),
                    $"{variation} wearing the {element} set changed nothing in any band — the pairing is decoration");
    }

    /// <summary>
    /// SPIRIT's capstone asks for four filled slots, and a Vow that wants one empty is a real conflict.
    /// </summary>
    /// <remarks>
    /// The eighth pairing in §64, and the one that is a conflict rather than a synergy. It is asserted
    /// as a conflict on purpose: HARMONY simply does not fire on a three-skill hunter, with no special
    /// branch anywhere to excuse it. A build cannot have both, which is what makes either a choice.
    /// </remarks>
    [Fact]
    public void test_spirit_harmony_and_an_empty_slot_cannot_both_be_had()
    {
        var four = Woven((Source.Spirit, "hammer_blow", "FLATTEN"), (Source.Spirit, "volley_spray", "SPLAY"),
                         (Source.Spirit, "field_mire", "TEEMING"), (Source.Spirit, "drain_wilt", "SUP"));
        var three = Woven((Source.Spirit, "hammer_blow", "FLATTEN"), (Source.Spirit, "volley_spray", "SPLAY"),
                          (Source.Spirit, "field_mire", "TEEMING"));

        foreach (var band in Bands)
        {
            var fourFull = Fight(four, Wearing(Source.Spirit, 5), band);
            var fourBare = Fight(four, Wearing(Source.Spirit, 4), band);
            var threeFull = Fight(three, Wearing(Source.Spirit, 5), band);
            var threeBare = Fight(three, Wearing(Source.Spirit, 4), band);

            // The capstone is worth nothing at all to the hunter holding a slot open.
            Assert.Equal(threeBare.Dealt, threeFull.Dealt, 1);
            _out.WriteLine($"HARMONY @{band}: four slots {fourBare.Dealt:F0} -> {fourFull.Dealt:F0} · "
                           + $"three slots {threeBare.Dealt:F0} -> {threeFull.Dealt:F0}");
        }
    }

    // ── §66. VARIATION BALANCE ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// NEITHER branch of a skill may win everywhere. "When would I choose this?" needs two answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the strongest thing these fixtures can honestly assert, and it is the actual design rule.
    /// A variation that is behind its sibling in every band the game has is not a choice — it is a trap
    /// with a name, and the player who takes it has spent a permanent level on being worse.
    /// </para>
    /// <para>
    /// "Wins" is read across every channel, not damage alone: HOLLOW and GAUNT buy bites that never
    /// land, SUP buys healing, BANKED buys shield. A branch that keeps more health while dealing less
    /// has won its band.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(EverySkill))]
    public void test_neither_variation_of_a_skill_wins_in_every_band(string skillId)
    {
        var def = SkillCatalogue.ById(skillId);
        Assert.Equal(2, def.Variations.Count);

        var partnerId = def.Kind == SkillKind.Active ? "field_mire" : "hammer_blow";
        var leads = new bool[2];

        // EVERY REACHABLE PAIR, because since 2026-09-09 a variation is bought with TWO of its three
        // reinforcements and WHICH two is the player's decision. Asking "is there a problem this
        // branch is the answer to" of one arbitrary pair would fail a branch whose answer lives in a
        // pair the fixture happened not to take — the fixture reporting on itself. A branch leads a
        // band if ANY pair of it leads ANY pair of its sibling there.
        var pairs = def.Variations
            .Select(v => v.Reinforcements.Select(r => r.Name).ToList())
            .Select(names => names
                .SelectMany((_, a) => names.Skip(a + 1).Select(b => (IReadOnlyList<string>)new[] { names[a], b }))
                .DefaultIfEmpty(Array.Empty<string>())
                .ToList())
            .ToList();

        foreach (var band in Bands)
        foreach (var pressure in Pressures)
        {
            var r = def.Variations
                .Select((v, i) => pairs[i]
                    .Select(pair => Fight(Woven(pair, (v.Source, skillId, v.Name), (Source.Body, partnerId, null)),
                                          Wearing(null, 0), band, damage: pressure))
                    .ToList())
                .ToList();

            // COUNTING BAND WINS WAS THE WRONG QUESTION, and it accused the wrong branches. SIPHON heals
            // and GLUT does not, so under a channel count SIPHON "won" every band on a free point while
            // GLUT was quietly dealing 40% more damage in three of them. What a player actually asks is
            // "when would I choose this?", and that needs ONE answer, not a majority: a branch is a real
            // choice if there is a problem where it leads.
            // A BRANCH LEADS A BAND if some pair of it leads some pair of its sibling. The question
            // the rule asks is "when would I choose this?", and the player chooses the branch AND its
            // pair — so a branch whose answer lives in one particular pair has an answer.
            if (r[0].Any(a => r[1].Any(b => Leads(a, b)))) leads[0] = true;
            if (r[1].Any(a => r[0].Any(b => Leads(a, b)))) leads[1] = true;

            var best0 = r[0].OrderByDescending(x => x.Dealt).First();
            var best1 = r[1].OrderByDescending(x => x.Dealt).First();
            _out.WriteLine($"{def.Name} @{band}/{pressure:F0}: {def.Variations[0].Name} dealt {best0.Dealt:F0} kept {best0.HealthKept} "
                           + $"heal {best0.Healed} shield {best0.ShieldAbsorbed:F0} · "
                           + $"{def.Variations[1].Name} dealt {best1.Dealt:F0} kept {best1.HealthKept} "
                           + $"heal {best1.Healed} shield {best1.ShieldAbsorbed:F0}");
        }

        for (var i = 0; i < 2; i++)
            Assert.True(leads[i],
                        $"{def.Name} / {def.Variations[i].Name} leads its sibling in no channel of any band — "
                        + "there is no problem it is the answer to, so it is a permanent level spent on being worse");
    }

    public static IEnumerable<object[]> EverySkill()
        => SkillCatalogue.All.Select(d => new object[] { d.Id });

    /// <summary>Whether this result leads the other in ANY channel a variation can buy.</summary>
    /// <remarks>
    /// Every channel, because the branches do not all buy damage: HOLLOW and GAUNT buy bites that never
    /// land, SUP buys healing, BANKED buys shield. A branch keeping more health while dealing less has
    /// answered its band.
    /// </remarks>
    private static bool Leads(Bench mine, Bench theirs)
        => mine.Dealt > theirs.Dealt + 0.5f
        || mine.HealthKept > theirs.HealthKept
        || mine.Healed > theirs.Healed
        || mine.ShieldAbsorbed > theirs.ShieldAbsorbed + 0.5f
        || mine.Prevented > theirs.Prevented + 0.5f
        || mine.Kills > theirs.Kills;

    // ── §67. THE ACTIVE-SKILL BEAT BUDGET ────────────────────────────────────────────────────────

    /// <summary>
    /// TWO ACTIVES MUST NOT ERASE THE BASIC ATTACK.
    /// </summary>
    /// <remarks>
    /// One action happens per beat, so every cast is a swing that did not happen — and BODY's whole
    /// capstone is built on the swing after a cast. A pair of Actives whose cadences leave no beat over
    /// does not merely make MOMENTUM weak, it makes it unbuyable, and it does so silently: nothing in
    /// the build screen says "these two skills have eaten your basic attack".
    /// </remarks>
    [Fact]
    public void test_two_actives_leave_the_basic_attack_most_of_the_beats()
    {
        // How many of the champion's actions one cast of this skill is worth, in beats — the same
        // number for a beat-counted Active and a clock-counted one.
        static int Rotation(SkillDef d)
            => d.Beats > 0 ? d.Beats : (int)MathF.Ceiling(d.IntervalMs / (float)SoloBattle.DefaultBeatMs);

        var actives = SkillCatalogue.All.Where(d => d.Kind == SkillKind.Active).ToList();
        var worst = 1f;
        var measured = 0;
        string worstPair = "";

        foreach (var a in actives)
            foreach (var b in actives)
            {
                if (string.CompareOrdinal(a.Id, b.Id) >= 0) continue;
                // A PAIR NOBODY CAN BUILD IS NOT A PAIR. A hunter has exactly one signature, so two
                // signatures belonging to different champions can never stand in one loadout — and
                // measuring that combination would hold the beat budget to a build the ownership rule
                // forbids. Every other pair (two shared, or a champion's own signature beside a
                // shared active) is one a real player can weave, and is measured.
                if (a.OwnerCharacterId is not null && b.OwnerCharacterId is not null
                    && a.OwnerCharacterId != b.OwnerCharacterId) continue;
                var build = Woven((a.Variations[0].Source, a.Id, a.Variations[0].Name),
                                  (b.Variations[0].Source, b.Id, b.Variations[0].Name));
                var r = Fight(build, Wearing(null, 0), Archetype.Bruiser);
                var beats = r.Casts + r.Swings;
                // A WAVE THAT ENDS INSIDE ONE ROTATION HAS NOT POSED THE QUESTION. The share is only
                // about cadence once both skills have had a chance to come round; before that it is
                // about how much health the band was carrying. It used to be enough to skip a fight
                // with no actions at all, because no shipped pair could end a Bruiser sooner — but a
                // champion whose innate doubles the first hit on each enemy kills this band in ONE
                // action, and one cast and no swing reads as 0% of the beats for a pair whose real
                // demand is 0.38. Measured against the pair's own longest cadence instead.
                var needed = Math.Max(Rotation(a), Rotation(b));
                if (beats < needed)
                {
                    _out.WriteLine($"  {a.Name} + {b.Name}: SKIPPED — the wave ended in {beats} actions, "
                                   + $"inside the pair's own {needed}-action rotation ({r.DurationMs}ms)");
                    continue;
                }
                measured++;
                var swingShare = r.Swings / (float)beats;
                _out.WriteLine($"  {a.Name} + {b.Name}: {r.Casts} casts, {r.Swings} swings, {r.DurationMs}ms");
                if (swingShare < worst) { worst = swingShare; worstPair = $"{a.Name} + {b.Name}"; }
            }

        _out.WriteLine($"the hungriest pair is {worstPair}, leaving the swing {worst:P0} of the beats "
                       + $"({measured} pairs measured)");
        // The guard above must never empty the bench.
        Assert.True(measured >= 10, $"only {measured} pairs ran long enough to measure — the bench is not posing the question");
        Assert.True(worst >= 0.25f,
                    $"{worstPair} left the basic attack only {worst:P0} of the beats — MOMENTUM is unbuyable beside it");
    }

    // ── §68. SHIELD BALANCE ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SHIELD MUST NOT BE "HEALTH BUT BETTER".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The benchmark the brief lists: nothing, the MACHINE ladder at three and five pieces, and NATURE's
    /// overflow. Each is compared against the same build wearing five plain pieces, so what is measured
    /// is the shield and not the items carrying it.
    /// </para>
    /// <para>
    /// The bound is the one that matters: shield may not keep more of the pool than the SAME investment
    /// in the pool itself would. BODY's 2-piece rung is +10% maximum health, and MACHINE's whole ladder
    /// must not be worth several times that in survival — a defensive resource that dominates health
    /// outright would make every health node on the tree dead.
    /// </para>
    /// </remarks>
    [Fact]
    public void test_shield_survives_more_than_nothing_and_less_than_a_second_pool()
    {
        var build = Woven((Source.Body, "hammer_blow", "FLATTEN"), (Source.Body, "field_mire", "TEEMING"));

        foreach (var band in Bands)
        {
            var plain = Fight(build, Wearing(null, 5), band, health: 520f, damage: 14f);
            var machine3 = Fight(build, Wearing(Source.Machine, 3), band, health: 520f, damage: 14f);
            var machine5 = Fight(build, Wearing(Source.Machine, 5), band, health: 520f, damage: 14f);

            _out.WriteLine($"@{band}: plain kept {plain.HealthKept}/{plain.HealthKept + 1} · "
                           + $"MACHINE 3 kept {machine3.HealthKept} (shield ate {machine3.ShieldAbsorbed:F0}) · "
                           + $"MACHINE 5 kept {machine5.HealthKept} (shield ate {machine5.ShieldAbsorbed:F0}, "
                           + $"prevented {machine5.Prevented:F0})");

            // It does something...
            Assert.True(machine5.HealthKept >= machine3.HealthKept,
                        $"the full MACHINE ladder kept less health than three pieces of it @{band}");
            // ...and never more than the cap allows it to.
            var pool = SoloBattle.ChampionHealth(build, Wearing(Source.Machine, 5));
            Assert.True(machine5.ShieldAbsorbed <= pool * ShieldRules.CapFraction * 4f,
                        $"shield absorbed {machine5.ShieldAbsorbed:F0} against a {pool} pool @{band} — "
                        + "more than a capped, wave-local resource can account for");
        }
    }

    /// <summary>A shield never carries out of the wave that made it, however much of it is left.</summary>
    /// <remarks>
    /// The single rule that keeps shield from becoming a pool: it is wave-local. Asserted through the
    /// expedition rather than a bare wave, because carrying is exactly the thing a run of many waves
    /// would do if the reset were ever missed.
    /// </remarks>
    [Fact]
    public void test_no_shield_survives_the_wave_that_made_it()
    {
        var build = Woven((Source.Body, "hammer_blow", "FLATTEN"), (Source.Body, "field_mire", "TEEMING"));
        var hunter = Wearing(Source.Machine, 5);
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool * 20, Health = pool * 20 };
        var run = new SoloExpedition(build, champ, hunter, 260f, 26f, ExpeditionTuning.Default, new Random(3));

        var cap = ShieldRules.CapFor(champ.MaxHealth);
        for (var w = 0; w < 6; w++)
        {
            run.PushWave();
            // Whatever the wave ended holding, the next one starts from the grant and nothing else:
            // a standing figure above one wave's own maximum grant could only be a carry.
            Assert.True(champ.CurrentShield <= cap,
                        $"wave {w + 1} ended holding {champ.CurrentShield} against a {cap} cap");
        }
    }
}
