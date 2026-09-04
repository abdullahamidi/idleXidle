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

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// EVERY ONE OF THE TWENTY-SIX TRAITS MUST REACH THE FIGHT (BRIEF §101).
/// </summary>
/// <remarks>
/// <para>
/// The bar is the one <see cref="mastery_new_nodes_liveness_test"/> already holds, and it is not
/// "the record carries the number" — that is arithmetic, and arithmetic has never been this project's
/// failure. The failure is a dial resolved, summed and carried by correct code and read by NOTHING.
/// So each test here runs the SAME fight twice, once with the trait and once without, and asserts
/// that damage dealt or health kept moves in the direction the card promises when the trait's
/// condition is posed.
/// </para>
/// <para>
/// <b>Every shape comes from the catalogue</b>, through <see cref="Trait"/> — never a hand-built
/// <c>TraitRules</c>. A test that built its own dials would keep passing after the catalogue entry
/// was renamed, retuned or deleted, which is precisely the state this suite exists to make
/// impossible.
/// </para>
/// <para>
/// <b>Eleven carry a NEGATIVE control as well.</b> A conditional trait that fires unconditionally is
/// the same bug as one that never fires, and only the negative half catches it: LAST WORD must not
/// move a five-creature wave's opening, WHAT KILLED YOU must not move a wave of the wrong archetype,
/// THE MATCHED SUIT must not move a build whose elements do not match the set, and so on.
/// </para>
/// </remarks>
public class trait_liveness_test
{
    // ── The harness ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What one trait contributes, READ FROM THE CATALOGUE. The whole point of the suite.
    /// </summary>
    private static SkillShape Trait(string id, TraitContext ctx = default)
    {
        var def = TraitCatalogue.Find(id);
        Assert.NotNull(def);
        return def!.Shape(ctx);
    }

    private static Champion Champ(int hp = 200_000, int at = -1)
        => new() { MaxHealth = hp, Health = at < 0 ? hp : at };

    private static List<WaveCreature> Wave(int count, float health, float damage,
                                           float defense = 0f, Source? source = null)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = health, Health = health, Damage = damage, Defense = defense, Source = source,
        }).ToList();

    /// <summary>The swing switched off, so every point of damage measured is the build's.</summary>
    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static Build BuildOf(SkillShape shape, Source source, params string[] skillIds)
    {
        var b = new Build { Shape = shape };
        foreach (var id in skillIds.Length == 0 ? new[] { "hammer_blow" } : skillIds)
            b.Equip(TestBuilds.Skill(id, source));
        return b;
    }

    private sealed record Run(WaveMetrics Metrics, Champion Champ, WaveOutcome Outcome);

    /// <summary>
    /// One wave, resolved. Same seed, same creatures, same cadence on both sides of every comparison —
    /// the ONLY difference a test may introduce is the shape.
    /// </summary>
    private static Run Fight(SkillShape shape, List<WaveCreature> creatures,
                             ExpeditionTuning? tuning = null, int intervalMs = 900,
                             int hp = 200_000, int startHealth = -1,
                             Source source = Source.Spirit, params string[] skillIds)
    {
        var metrics = new WaveMetrics();
        var champ = Champ(hp, startHealth);
        var (outcome, _) = SoloBattle.ResolveWave(
            champ, BuildOf(shape, source, skillIds), new Hunter(), creatures, intervalMs,
            tuning ?? ExpeditionTuning.Default, new Random(11), metrics: metrics);
        return new Run(metrics, champ, outcome);
    }

    /// <summary>
    /// Several waves in a row against ONE champion, which is what a carry, a ladder or a per-wave
    /// charge needs: a trait whose sentence says "each wave" is only observable across more than one.
    /// </summary>
    private static (WaveMetrics Total, Champion Champ) Waves(
        SkillShape shape, int count, Func<List<WaveCreature>> creatures,
        ExpeditionTuning? tuning = null, int intervalMs = 900, int hp = 200_000,
        Source source = Source.Spirit, params string[] skillIds)
    {
        var champ = Champ(hp);
        var total = new WaveMetrics();
        var build = BuildOf(shape, source, skillIds);
        for (var w = 0; w < count; w++)
        {
            var m = new WaveMetrics();
            SoloBattle.ResolveWave(champ, build, new Hunter(), creatures(), intervalMs,
                                   tuning ?? ExpeditionTuning.Default, new Random(11), metrics: m);
            total.DeliveredDamage += m.DeliveredDamage;
            total.RawDamage += m.RawDamage;
            total.HealthDamage += m.HealthDamage;
            total.ShieldAbsorbed += m.ShieldAbsorbed;
            total.ShieldGained += m.ShieldGained;
            total.ShieldBreaks += m.ShieldBreaks;
            total.ReflectedDamage += m.ReflectedDamage;
            total.Healed += m.Healed;
            total.DamagePrevented += m.DamagePrevented;
            total.HeavyHits += m.HeavyHits;
            total.Overkill += m.Overkill;
            total.Hits += m.Hits;
            total.Activations += m.Activations;
            total.TargetsStruck += m.TargetsStruck;
            total.CreaturesKilled += m.CreaturesKilled;
            total.DurationMs += m.DurationMs;
            if (!champ.Alive) break;
        }
        return (total, champ);
    }

    private static void Up(float without, float with, float byAtLeast, string trait, string what)
        => Assert.True(with > without * (1f + byAtLeast),
            $"{trait}: {what} was {without:0.##} without and {with:0.##} with — " +
            $"the dial is not reaching the fight (wanted at least +{byAtLeast:P0}).");

    private static void Same(float without, float with, string trait, string what)
        => Assert.True(MathF.Abs(with - without) <= MathF.Max(1f, MathF.Abs(without) * 0.001f),
            $"{trait}: {what} moved from {without:0.##} to {with:0.##} where the trait's condition is " +
            "NOT posed — a conditional trait that fires unconditionally is the same bug as one that never fires.");

    // ── HEAVY HITS ────────────────────────────────────────────────────────────────────────────────

    /// <summary>DEEP CUT — a heavy hit strips defence, so every hit after it lands harder.</summary>
    [Fact]
    public void test_deep_cut_strips_defence_off_what_it_hits_hard()
    {
        // One plated Bruiser: BLOW at 500 base takes far more than a fifth of 2,000 health, so every
        // landing is a heavy hit and the plate comes off on the first one.
        List<WaveCreature> Plated() => Wave(1, 2_000f, 40f, defense: 25f);

        var plainWave = Plated();
        var traitWave = Plated();
        var plain = Fight(SkillShape.None, plainWave, NoSwing);
        var deep = Fight(Trait("t_deep_cut"), traitWave, NoSwing);

        Up(plain.Metrics.DeliveredDamage, deep.Metrics.DeliveredDamage, 0.02f, "DEEP CUT", "damage delivered");
        Assert.True(deep.Metrics.AbsorbedFraction < plain.Metrics.AbsorbedFraction,
            "DEEP CUT: the plate absorbed the same share with the trait as without — the strip is not landing.");
        // And the strip is real, on the creature, for the rest of the wave.
        Assert.True(traitWave[0].Defense < plainWave[0].Defense,
            $"DEEP CUT: defence ended at {traitWave[0].Defense} with the trait and {plainWave[0].Defense} without.");

        // NEGATIVE CONTROL: a hit that is not heavy strips nothing. A creature with a pool BLOW cannot
        // take a fifth of in one landing keeps its plate.
        var hugePlain = Fight(SkillShape.None, Wave(1, 4_000_000f, 40f, defense: 25f), NoSwing);
        var hugeDeep = Fight(Trait("t_deep_cut"), Wave(1, 4_000_000f, 40f, defense: 25f), NoSwing);
        Same(hugePlain.Metrics.DeliveredDamage, hugeDeep.Metrics.DeliveredDamage, "DEEP CUT", "damage against a pool no hit is a fifth of");
    }

    // ── WASTED DAMAGE ─────────────────────────────────────────────────────────────────────────────

    /// <summary>THE SPILL — damage wasted past a kill comes back as shield.</summary>
    [Fact]
    public void test_the_spill_turns_waste_into_shield()
    {
        // Five paper creatures against a 500-power BLOW: every kill wastes most of the blow.
        List<WaveCreature> Paper() => Wave(5, 60f, 900f);

        var plain = Fight(SkillShape.None, Paper(), intervalMs: 400);
        var spill = Fight(Trait("t_spill"), Paper(), intervalMs: 400);

        Assert.True(plain.Metrics.Overkill > 0f, "The fixture wasted nothing — it cannot pose THE SPILL's condition.");
        Assert.Equal(0f, plain.Metrics.ShieldGained);
        Assert.True(spill.Metrics.ShieldGained > 0f,
            "THE SPILL: no shield was granted from the waste — OverkillToShield is not reaching GrantShield.");
        Assert.True(spill.Metrics.HealthDamage <= plain.Metrics.HealthDamage,
            $"THE SPILL: health damage rose from {plain.Metrics.HealthDamage} to {spill.Metrics.HealthDamage} " +
            "with a shield the plain run did not have.");
    }

    /// <summary>CARRION WEIGHT — the same waste bleeds into the rest of the wave.</summary>
    [Fact]
    public void test_carrion_weight_bleeds_the_waste_into_the_wave()
    {
        // MEASURED IN TIME, NOT IN DAMAGE, and that is the honest measure here: the bleed kills the
        // wave sooner, so FEWER blows land and total delivered damage falls even though the trait is
        // working perfectly. A damage assertion on this fixture would accuse working content.
        // Twelve creatures BLOW kills with almost the whole blow wasted, so the bleed pool is real.
        List<WaveCreature> Paper() => Wave(12, 40f, 40f);

        var plain = Fight(SkillShape.None, Paper(), NoSwing, intervalMs: 400);
        var carrion = Fight(Trait("t_carrion_weight"), Paper(), NoSwing, intervalMs: 400);

        Assert.True(plain.Metrics.Overkill > 0f, "The fixture wasted nothing — it cannot pose CARRION WEIGHT's condition.");
        Assert.Equal(12, plain.Metrics.CreaturesKilled);
        Assert.Equal(12, carrion.Metrics.CreaturesKilled);
        Assert.True(carrion.Metrics.DurationMs < plain.Metrics.DurationMs,
            $"CARRION WEIGHT: the wave took {carrion.Metrics.DurationMs} ms with the bleed and " +
            $"{plain.Metrics.DurationMs} ms without — OverkillToBleed is not reaching the poison pool.");
        // CASTS, not hits: the bleed lands through LandOn like any other unskilled damage, so it ADDS
        // hits while removing the blows that would have been needed. Activations is the honest count.
        Assert.True(carrion.Metrics.Activations < plain.Metrics.Activations,
            $"CARRION WEIGHT: {carrion.Metrics.Activations} casts cleared the wave with the bleed and " +
            $"{plain.Metrics.Activations} without — the waste is not doing the work.");
    }

    // ── REACH ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>SPREADING FIRE — a cast that already reaches three reaches one more.</summary>
    [Fact]
    public void test_spreading_fire_widens_a_cast_that_is_already_wide()
    {
        // SPRAY reaches 5. Eight creatures, so there is always a sixth to reach.
        List<WaveCreature> Crowd() => Wave(8, 500_000f, 10f);

        var plain = Fight(SkillShape.None, Crowd(), NoSwing, skillIds: "volley_spray");
        var wide = Fight(Trait("t_spreading_fire"), Crowd(), NoSwing, skillIds: "volley_spray");

        Assert.True(wide.Metrics.TargetsPerActivation > plain.Metrics.TargetsPerActivation,
            $"SPREADING FIRE: {wide.Metrics.TargetsPerActivation:0.##} creatures per cast against " +
            $"{plain.Metrics.TargetsPerActivation:0.##} — the extra target is not reaching LandSpread.");
        Up(plain.Metrics.DeliveredDamage, wide.Metrics.DeliveredDamage, 0.05f, "SPREADING FIRE", "damage delivered");

        // NEGATIVE CONTROL: BLOW reaches one. A narrow cast earns nothing.
        var narrowPlain = Fight(SkillShape.None, Crowd(), NoSwing, skillIds: "hammer_blow");
        var narrowWide = Fight(Trait("t_spreading_fire"), Crowd(), NoSwing, skillIds: "hammer_blow");
        Same(narrowPlain.Metrics.DeliveredDamage, narrowWide.Metrics.DeliveredDamage,
             "SPREADING FIRE", "damage from a single-target skill");
    }

    // ── CRITICAL HITS ─────────────────────────────────────────────────────────────────────────────

    /// <summary>THE CERTAIN HAND — the wave's first skill hit crits, and harder than a crit does.</summary>
    [Fact]
    public void test_the_certain_hand_makes_the_first_hit_of_a_wave_certain()
    {
        List<WaveCreature> Bruiser() => Wave(1, 4_000_000f, 30f);

        var plain = Fight(SkillShape.None, Bruiser(), NoSwing);
        var hand = Fight(Trait("t_certain_hand"), Bruiser(), NoSwing);

        Up(plain.Metrics.DeliveredDamage, hand.Metrics.DeliveredDamage, 0.005f, "THE CERTAIN HAND", "damage delivered");

        // ONCE A WAVE, not once a hit: five waves must gain about five times what one wave gains, not
        // five times more. The ceiling is what separates this from a flat damage multiplier (LAW 9).
        var onePlain = Fight(SkillShape.None, Bruiser(), NoSwing).Metrics.DeliveredDamage;
        var oneHand = Fight(Trait("t_certain_hand"), Bruiser(), NoSwing).Metrics.DeliveredDamage;
        var perWave = oneHand - onePlain;
        var fivePlain = Waves(SkillShape.None, 5, Bruiser, NoSwing).Total.DeliveredDamage;
        var fiveHand = Waves(Trait("t_certain_hand"), 5, Bruiser, NoSwing).Total.DeliveredDamage;
        var acrossFive = fiveHand - fivePlain;
        Assert.True(acrossFive < perWave * 8f,
            $"THE CERTAIN HAND: one wave gained {perWave:0} and five waves gained {acrossFive:0} — " +
            "the charge is not being spent once per wave.");
    }

    /// <summary>THE OPENED VEIN — criticals leave the enemy bleeding.</summary>
    [Fact]
    public void test_the_opened_vein_leaves_a_bleed_behind_a_critical()
    {
        // A hunter with a real critical chance: the trait pays at the rate the hit criticals, so a
        // build that never crits must gain nothing (checked below).
        var crit = new Hunter();
        List<WaveCreature> Crowd() => Wave(5, 300_000f, 20f);

        WaveMetrics Go(SkillShape shape)
        {
            var m = new WaveMetrics();
            SoloBattle.ResolveWave(Champ(), BuildOf(shape, Source.Spirit, "volley_spray"), crit,
                                   Crowd(), 900, NoSwing, new Random(11), metrics: m);
            return m;
        }

        var plain = Go(SkillShape.None);
        var vein = Go(Trait("t_opened_vein"));
        Up(plain.DeliveredDamage, vein.DeliveredDamage, 0.01f, "THE OPENED VEIN", "damage delivered");
    }

    // ── SHIELD ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>SCAR TISSUE — the shield granted after a break is larger.</summary>
    [Fact]
    public void test_scar_tissue_makes_the_shield_after_a_break_larger()
    {
        // A small wave-start shield broken by the first bite, and HOLD FAST pulsing shield after it —
        // which is the grant SCAR TISSUE lifts. The trait reads the ONE funnel every producer passes
        // through, not one named source, so ANY grant arriving after the break is the one it enlarges.
        var plate = SkillShape.None with { WaveStartShieldFraction = 0.01f };
        List<WaveCreature> Biters() => Wave(3, 900_000f, 3_000f);

        var plain = Waves(plate, 4, Biters, NoSwing, intervalMs: 600, skillIds: "sig_unbroken_hold_fast");
        var scar = Waves(SkillShape.Combine(plate, Trait("t_scar_tissue")), 4, Biters, NoSwing,
                         intervalMs: 600, skillIds: "sig_unbroken_hold_fast");

        Assert.True(plain.Total.ShieldBreaks > 0, "The fixture never broke a shield — it cannot pose SCAR TISSUE's condition.");
        Up(plain.Total.ShieldGained, scar.Total.ShieldGained, 0.01f, "SCAR TISSUE", "shield granted");
        Assert.True(scar.Champ.Health >= plain.Champ.Health,
            $"SCAR TISSUE: the champion ended on {scar.Champ.Health} with the trait and {plain.Champ.Health} without.");
    }

    /// <summary>STANDING PLATE — half of a surviving shield is carried into the next wave.</summary>
    [Fact]
    public void test_standing_plate_carries_half_the_shield_into_the_next_wave()
    {
        // THE WAVES MUST DIFFER, and that took a wrong fixture to see. With identical waves the trait
        // is arithmetically invisible: absorption is bounded by the BITES, so a shield the wave never
        // empties absorbs the same either way, and a shield the wave empties leaves nothing to carry.
        // The carry only ever buys anything when a QUIET wave banks half its plate for a LOUD one —
        // which is the sentence on the card. So: a gentle wave, then a brutal one.
        var plate = SkillShape.None with { WaveStartShieldFraction = 0.05f };
        List<WaveCreature> Quiet() => Wave(1, 400f, 40f);
        List<WaveCreature> Loud() => Wave(1, 400f, 9_000f);

        (float Absorbed, int Lost) TwoWaves(SkillShape shape)
        {
            var champ = Champ();
            var build = BuildOf(shape, Source.Spirit);
            var absorbed = 0f;
            var lost = 0;
            foreach (var wave in new[] { Quiet(), Quiet(), Loud() })
            {
                var m = new WaveMetrics();
                SoloBattle.ResolveWave(champ, build, new Hunter(), wave, 900, NoSwing, new Random(11), metrics: m);
                absorbed += m.ShieldAbsorbed;
                lost += m.HealthDamage;
            }
            return (absorbed, lost);
        }

        var plain = TwoWaves(plate);
        var carry = TwoWaves(SkillShape.Combine(plate, Trait("t_standing_plate")));

        Up(plain.Absorbed, carry.Absorbed, 0.02f, "STANDING PLATE", "shield spent across the three waves");
        Assert.True(carry.Lost < plain.Lost,
            $"STANDING PLATE: {carry.Lost} health lost with the carry and {plain.Lost} without — " +
            "the quiet waves' plate is not reaching the loud one.");

        // HALF, NOT ALL — the named exception to the wave-local rule is bounded, and the bound is the
        // reason the invariant on ShieldRules survives. A carried shield must be strictly less than
        // what was held, which a full carry would not be.
        var champ = Champ();
        var build = BuildOf(SkillShape.Combine(plate, Trait("t_standing_plate")), Source.Spirit);
        // Nibbled rather than bitten, so a shield actually survives to be carried.
        SoloBattle.ResolveWave(champ, build, new Hunter(), Wave(1, 40_000f, 30f), 900,
                               ExpeditionTuning.Default, new Random(11));
        var held = champ.CurrentShield;
        Assert.True(held > 0f, "The fixture spent the whole shield — it cannot pose the carry.");
        champ.CarryShield(0.5f);
        Assert.True(champ.CurrentShield < held && champ.CurrentShield > 0f,
            $"STANDING PLATE: {held:0.#} held became {champ.CurrentShield:0.#} carried — the share is not half.");
    }

    /// <summary>THE ANSWERING WALL — a bite taken on plate is answered.</summary>
    [Fact]
    public void test_the_answering_wall_answers_a_bite_taken_on_plate()
    {
        var plate = SkillShape.None with { WaveStartShieldFraction = 0.40f };
        List<WaveCreature> Biters() => Wave(5, 300_000f, 500f);

        var plain = Fight(plate, Biters(), NoSwing, intervalMs: 400);
        var wall = Fight(SkillShape.Combine(plate, Trait("t_answering_wall")), Biters(), NoSwing, intervalMs: 400);

        Assert.True(plain.Metrics.ShieldAbsorbed > 0f, "The fixture never took a bite on plate.");
        Assert.Equal(0f, plain.Metrics.ReflectedDamage);
        Assert.True(wall.Metrics.ReflectedDamage > 0f,
            "THE ANSWERING WALL: nothing was sent back — ShieldedReflectFraction is not reaching the bite path.");
        Up(plain.Metrics.DeliveredDamage, wall.Metrics.DeliveredDamage, 0.005f, "THE ANSWERING WALL", "damage delivered");

        // NEGATIVE CONTROL: no shield, no answer. The wall is about WEARING plate when bitten.
        var barePlain = Fight(SkillShape.None, Biters(), NoSwing, intervalMs: 400);
        var bareWall = Fight(Trait("t_answering_wall"), Biters(), NoSwing, intervalMs: 400);
        Assert.Equal(0f, bareWall.Metrics.ReflectedDamage);
        Same(barePlain.Metrics.DeliveredDamage, bareWall.Metrics.DeliveredDamage,
             "THE ANSWERING WALL", "damage with no shield held");
    }

    // ── LOW HEALTH ────────────────────────────────────────────────────────────────────────────────

    /// <summary>LAST BREATH — at the brink, the skills come back sooner.</summary>
    [Fact]
    public void test_last_breath_speeds_the_skills_at_the_brink()
    {
        // Minted at a fifth of the pool, which is under the quarter line the card names.
        List<WaveCreature> Bruiser() => Wave(1, 8_000_000f, 20f);

        var plain = Fight(SkillShape.None, Bruiser(), NoSwing, startHealth: 40_000, skillIds: "hammer_blow", source: Source.Spirit);
        var breath = Fight(Trait("t_last_breath"), Bruiser(), NoSwing, startHealth: 40_000, skillIds: "hammer_blow", source: Source.Spirit);

        Up(plain.Metrics.Activations, breath.Metrics.Activations, 0.10f, "LAST BREATH", "casts");
        Up(plain.Metrics.DeliveredDamage, breath.Metrics.DeliveredDamage, 0.10f, "LAST BREATH", "damage delivered");

        // NEGATIVE CONTROL: a champion standing whole gains nothing at all.
        var wholePlain = Fight(SkillShape.None, Bruiser(), NoSwing);
        var wholeBreath = Fight(Trait("t_last_breath"), Bruiser(), NoSwing);
        Same(wholePlain.Metrics.DeliveredDamage, wholeBreath.Metrics.DeliveredDamage,
             "LAST BREATH", "damage at full health");
    }

    /// <summary>THE THIN LINE — the first bite that would kill deals half.</summary>
    [Fact]
    public void test_the_thin_line_halves_the_first_killing_bite()
    {
        // ONE creature the champion can kill, biting harder than the pool: without the trait the
        // first bite is a wipe, with it the champion is halved and lives to finish the wave. The
        // creature must be killable or the champion simply dies to the second bite either way, and
        // the fixture would report a working trait as dead.
        // Two creatures BLOW one-shots, and one bite inside the time that takes: the charge is spent
        // once, which is what the card says, so a fixture that lets a second bite land would report
        // a working trait as dead.
        List<WaveCreature> OneBigBite() => Wave(2, 400f, 12_000f);

        var plain = Fight(SkillShape.None, OneBigBite(), NoSwing, intervalMs: 12_000, hp: 10_000);
        var thin = Fight(Trait("t_thin_line"), OneBigBite(), NoSwing, intervalMs: 12_000, hp: 10_000);

        Assert.False(plain.Champ.Alive, "The fixture did not kill the champion — it cannot pose THE THIN LINE.");
        Assert.True(thin.Champ.Alive,
            $"THE THIN LINE: the champion still died, on {thin.Champ.Health} health — " +
            "the halved bite is not reaching the loss.");
        Assert.Equal(WaveOutcome.Cleared, thin.Outcome);
    }

    // ── HEALING ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>PRACTISED FLESH — every heal widens the wave's healing limit.</summary>
    [Fact]
    public void test_practised_flesh_widens_the_healing_limit()
    {
        // A leech build against a long wave, with the ceiling low enough that it is actually reached:
        // that is the condition, and a ceiling never reached would make the trait invisible for the
        // right reason and prove nothing.
        // THE CEILING MUST ACTUALLY BE REACHED. A budget the wave never spends makes the trait
        // correctly invisible and proves nothing, so the ceiling here is deliberately tiny — a
        // twentieth of one percent of the pool — and the leech runs past it long before the wave ends.
        var capped = ExpeditionTuning.Default with
        {
            AutoAttackDamage = 0f,
            Heal = HealTuning.Default with { MaxHealFractionPerWave = 0.0005f },
        };
        List<WaveCreature> Grinders() => Wave(4, 900_000f, 700f);

        var plain = Fight(SkillShape.None, Grinders(), capped, intervalMs: 500, skillIds: "drain_drink");
        var flesh = Fight(Trait("t_practised_flesh"), Grinders(), capped, intervalMs: 500, skillIds: "drain_drink");

        Assert.True(plain.Metrics.Healed > 0, "The fixture healed nothing — it cannot pose PRACTISED FLESH.");
        Up(plain.Metrics.Healed, flesh.Metrics.Healed, 0.01f, "PRACTISED FLESH", "health restored");
        Assert.True(flesh.Champ.Health >= plain.Champ.Health,
            $"PRACTISED FLESH: the champion ended on {flesh.Champ.Health} and {plain.Champ.Health} without.");
    }

    /// <summary>THE GIVEN HAND — healing with nowhere to go is thrown at the enemy.</summary>
    [Fact]
    public void test_the_given_hand_throws_unusable_healing_at_the_enemy()
    {
        // A champion at full health taking no damage: every point of leech is overheal.
        List<WaveCreature> Harmless() => Wave(3, 400_000f, 0f);

        var plain = Fight(SkillShape.None, Harmless(), NoSwing, skillIds: "drain_drink");
        var given = Fight(Trait("t_given_hand"), Harmless(), NoSwing, skillIds: "drain_drink");

        Assert.Equal(0, plain.Metrics.Healed);   // nothing to heal — it is all overheal
        Up(plain.Metrics.DeliveredDamage, given.Metrics.DeliveredDamage, 0.02f, "THE GIVEN HAND", "damage delivered");
        Assert.True(given.Metrics.DurationMs <= plain.Metrics.DurationMs,
            "THE GIVEN HAND: the wave took longer with the extra damage than without it.");

        // NEGATIVE CONTROL: a hurt champion has room, so the healing is used rather than thrown.
        var hurtPlain = Fight(SkillShape.None, Harmless(), NoSwing, startHealth: 20_000, skillIds: "drain_drink");
        var hurtGiven = Fight(Trait("t_given_hand"), Harmless(), NoSwing, startHealth: 20_000, skillIds: "drain_drink");
        Assert.True(hurtGiven.Metrics.DeliveredDamage - hurtPlain.Metrics.DeliveredDamage
                    < given.Metrics.DeliveredDamage - plain.Metrics.DeliveredDamage,
            "THE GIVEN HAND: a champion with room to heal threw as much away as one with none.");
    }

    // ── REFLECTED DAMAGE ──────────────────────────────────────────────────────────────────────────

    /// <summary>THE MIRROR — each bite makes the next reflection stronger.</summary>
    [Fact]
    public void test_the_mirror_ramps_the_reflection_across_a_wave()
    {
        var thorns = SkillShape.None with { ReflectFraction = 0.20f };
        List<WaveCreature> Biters() => Wave(5, 900_000f, 400f);

        var plain = Fight(thorns, Biters(), NoSwing, intervalMs: 300);
        var mirror = Fight(SkillShape.Combine(thorns, Trait("t_mirror")), Biters(), NoSwing, intervalMs: 300);

        Assert.True(plain.Metrics.ReflectedDamage > 0f, "The fixture reflected nothing — it cannot pose THE MIRROR.");
        Up(plain.Metrics.ReflectedDamage, mirror.Metrics.ReflectedDamage, 0.20f, "THE MIRROR", "damage reflected");
        Up(plain.Metrics.DeliveredDamage, mirror.Metrics.DeliveredDamage, 0.01f, "THE MIRROR", "damage delivered");

        // NEGATIVE CONTROL: nothing to ramp. A build with no reflection of its own reflects nothing.
        var bare = Fight(Trait("t_mirror"), Biters(), NoSwing, intervalMs: 300);
        Assert.Equal(0f, bare.Metrics.ReflectedDamage);
    }

    // ── KILLS ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>SETTLING WEIGHT — the dead make the hits heavier.</summary>
    [Fact]
    public void test_settling_weight_grows_as_the_wave_empties()
    {
        // BLOW, one creature at a time, so the wave empties in steps and each step is heavier than
        // the last. A crowd nothing can kill inside the tick ceiling poses no deaths at all.
        List<WaveCreature> Crowd() => Wave(7, 400f, 30f);

        var plain = Fight(SkillShape.None, Crowd(), NoSwing, skillIds: "hammer_blow");
        var weight = Fight(Trait("t_settling_weight"), Crowd(), NoSwing, skillIds: "hammer_blow");

        Assert.True(plain.Metrics.CreaturesKilled > 1, "The fixture killed nothing — it cannot pose SETTLING WEIGHT.");
        // MEASURED ON THE HIT, not on the clock. Each blow already kills one creature here, so the
        // wave's LENGTH is fixed by the cast cadence whatever the bonus does — a duration assertion
        // on this fixture would report a working trait as dead. The hit growing as the wave empties
        // is the thing the card promises, and AverageHitSize is exactly that number.
        Up(plain.Metrics.AverageHitSize, weight.Metrics.AverageHitSize, 0.05f,
           "SETTLING WEIGHT", "the average hit");

        // NEGATIVE CONTROL: one creature, so nothing ever dies before the end. Nothing may move.
        var solePlain = Fight(SkillShape.None, Wave(1, 700_000f, 30f), NoSwing, skillIds: "hammer_blow");
        var soleWeight = Fight(Trait("t_settling_weight"), Wave(1, 700_000f, 30f), NoSwing, skillIds: "hammer_blow");
        Same(solePlain.Metrics.AverageHitSize, soleWeight.Metrics.AverageHitSize,
             "SETTLING WEIGHT", "the average hit in a wave where nothing dies first");
    }

    /// <summary>LAST WORD — the last enemy alive takes more.</summary>
    [Fact]
    public void test_last_word_hits_the_last_enemy_alive_harder()
    {
        // ONE creature: alive == 1 from the first tick, so the bonus applies to the whole wave.
        List<WaveCreature> Alone() => Wave(1, 3_000_000f, 30f);

        var plain = Fight(SkillShape.None, Alone(), NoSwing);
        var last = Fight(Trait("t_last_word"), Alone(), NoSwing);
        Up(plain.Metrics.DeliveredDamage, last.Metrics.DeliveredDamage, 0.20f, "LAST WORD", "damage against a lone enemy");

        // NEGATIVE CONTROL: a crowded wave's opening is not the last enemy. Measured on the first
        // cast alone (the wave's total would move once the crowd thins, which is the trait working).
        var crowdPlain = Fight(SkillShape.None, Wave(6, 3_000_000f, 30f), NoSwing).Metrics;
        var crowdLast = Fight(Trait("t_last_word"), Wave(6, 3_000_000f, 30f), NoSwing).Metrics;
        Same(crowdPlain.AverageHitSize, crowdLast.AverageHitSize, "LAST WORD", "average hit size in a crowd that never thins");
    }

    // ── MARKS ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>THE LINGERING MARK — a mark cast before a crowd holds longer.</summary>
    [Fact]
    public void test_the_lingering_mark_holds_longer_before_a_crowd()
    {
        // CALL is the mark; BLOW is what the window is spent on. Five creatures alive when it opens.
        List<WaveCreature> Crowd() => Wave(5, 900_000f, 20f);

        var plain = Fight(SkillShape.None, Crowd(), NoSwing, skillIds: new[] { "sign_call", "hammer_blow" });
        var mark = Fight(Trait("t_lingering_mark"), Crowd(), NoSwing, skillIds: new[] { "sign_call", "hammer_blow" });

        // MarkCasts, not StyleActivations[Sign]: the latter is written inside LandSpread and every
        // SIGN skill is an Amplify that lands nothing, so it is zero for every build ever run.
        Assert.True(plain.Metrics.MarkCasts > 0,
            "The fixture never cast a mark — it cannot pose THE LINGERING MARK.");
        Up(plain.Metrics.DeliveredDamage, mark.Metrics.DeliveredDamage, 0.02f, "THE LINGERING MARK", "damage delivered");
    }

    // ── VOWS ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>THE KEPT WORD — a build that has sworn nothing borrows the weakest vow it has found.</summary>
    /// <remarks>
    /// THE RULE CHANGED UNDER THIS TRAIT, and the test changed with it. It was authored while a vow
    /// hung on a SKILL SLOT, so "a skill with no vow borrows one" named a real thing and this test
    /// posed one sworn slot beside one unsworn slot. A vow is a promise about the BUILD now — which
    /// is what lets vow capacity be a number a milestone grants instead of an accident of how many
    /// skill slots a hunter owns — and under that model no slot has, or lacks, a vow of its own.
    ///
    /// The build-level equivalent of an unsworn slot is a build that swore NOTHING, so that is the
    /// rule, and it is a better one: the trait pays only a hunter who leaves every vow unsworn, which
    /// is a decision rather than a free rider on someone else's promise. The loan is decided once at
    /// compose time (<c>BuildComposer</c>, the only place that can see both what the account has
    /// found and what the build has sworn) and joins <c>Build.Vows</c>, so the combined factor, the
    /// fragility bill, the health price and TITHE all bill it exactly like a sworn one.
    /// </remarks>
    [Fact]
    public void test_the_kept_word_lends_the_weakest_found_vow_to_a_build_that_swore_nothing()
    {
        var complete = Vows.ById("vow_complete");
        Assert.NotNull(complete);

        // A build that has sworn NOTHING. The loan is what the composer would have lent it.
        Build Woven(SkillShape shape, bool lent)
        {
            var b = new Build { Shape = shape, SlotCapacity = 2 };
            b.Equip(TestBuilds.Skill("hammer_blow", Source.Spirit));
            b.Equip(TestBuilds.Skill("volley_spray", Source.Spirit));
            if (lent) b.BorrowedVow = complete;
            return b;
        }

        WaveMetrics Go(SkillShape shape, bool lent)
        {
            var m = new WaveMetrics();
            SoloBattle.ResolveWave(Champ(), Woven(shape, lent), new Hunter(), Wave(3, 700_000f, 20f),
                                   900, NoSwing, new Random(11), metrics: m);
            return m;
        }

        Up(Go(SkillShape.None, lent: false).DeliveredDamage,
           Go(Trait("t_kept_word"), lent: true).DeliveredDamage,
           0.01f, "THE KEPT WORD", "damage delivered");

        // NEGATIVE CONTROL: the trait without a loan changes nothing — the dial is the borrowed vow,
        // not the trait's presence, so a build the composer declined to lend to fights identically.
        Same(Go(SkillShape.None, lent: false).DeliveredDamage,
             Go(Trait("t_kept_word"), lent: false).DeliveredDamage,
             "THE KEPT WORD", "damage with nothing lent");
    }

    /// <summary>THE PRICE PAID — a vow whose demand is broken still pays half.</summary>
    [Fact]
    public void test_the_price_paid_pays_half_of_a_broken_vow()
    {
        // VOW OF THE SINGULAR demands one style; the build carries two, so the demand is broken and
        // the vow pays nothing at all without the trait.
        var singular = Vows.ById("vow_singular");
        Assert.NotNull(singular);

        WaveMetrics Go(SkillShape shape)
        {
            var m = new WaveMetrics();
            var b = new Build { Shape = shape, SlotCapacity = 2 };
            b.Equip(TestBuilds.Skill("hammer_blow", Source.Spirit, singular));
            b.Equip(TestBuilds.Skill("volley_spray", Source.Spirit, singular));
            SoloBattle.ResolveWave(Champ(), b, new Hunter(), Wave(3, 700_000f, 20f),
                                   900, NoSwing, new Random(11), metrics: m);
            return m;
        }

        var plain = Go(SkillShape.None);
        var paid = Go(Trait("t_price_paid"));
        Up(plain.DeliveredDamage, paid.DeliveredDamage, 0.05f, "THE PRICE PAID", "damage delivered");

        // NEGATIVE CONTROL: a vow whose demand IS met already pays in full, so the trait adds nothing.
        WaveMetrics Kept(SkillShape shape)
        {
            var m = new WaveMetrics();
            var b = new Build { Shape = shape, SlotCapacity = 2 };
            b.Equip(TestBuilds.Skill("hammer_blow", Source.Spirit, singular));
            b.Equip(TestBuilds.Skill("hammer_press", Source.Spirit, singular));
            SoloBattle.ResolveWave(Champ(), b, new Hunter(), Wave(3, 700_000f, 20f),
                                   900, NoSwing, new Random(11), metrics: m);
            return m;
        }
        Same(Kept(SkillShape.None).DeliveredDamage, Kept(Trait("t_price_paid")).DeliveredDamage,
             "THE PRICE PAID", "damage with the vow's demand met");
    }

    // ── ELEMENTS ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>THE SINGLE NOTE — one element, and the wave's first cast lands twice.</summary>
    [Fact]
    public void test_the_single_note_repeats_the_first_cast_of_a_pure_build()
    {
        List<WaveCreature> Crowd() => Wave(4, 900_000f, 20f);

        Build Pure(SkillShape shape)
        {
            var b = new Build { Shape = shape, SlotCapacity = 2 };
            b.Equip(TestBuilds.Skill("hammer_blow", Source.Machine));
            b.Equip(TestBuilds.Skill("volley_spray", Source.Machine));
            return b;
        }
        Build Mixed(SkillShape shape)
        {
            var b = new Build { Shape = shape, SlotCapacity = 2 };
            b.Equip(TestBuilds.Skill("hammer_blow", Source.Machine));
            b.Equip(TestBuilds.Skill("volley_spray", Source.Body));
            return b;
        }
        WaveMetrics Go(Func<SkillShape, Build> make, SkillShape shape)
        {
            var m = new WaveMetrics();
            SoloBattle.ResolveWave(Champ(), make(shape), new Hunter(), Crowd(), 900, NoSwing,
                                   new Random(11), metrics: m);
            return m;
        }

        Up(Go(Pure, SkillShape.None).DeliveredDamage, Go(Pure, Trait("t_single_note")).DeliveredDamage,
           0.02f, "THE SINGLE NOTE", "damage from a one-element build");

        // NEGATIVE CONTROL: one slot on another element and the whole thing is off.
        Same(Go(Mixed, SkillShape.None).DeliveredDamage, Go(Mixed, Trait("t_single_note")).DeliveredDamage,
             "THE SINGLE NOTE", "damage from a two-element build");
    }

    /// <summary>MANY TONGUES — four elements, and every matchup counts as a strong one.</summary>
    [Fact]
    public void test_many_tongues_makes_every_matchup_strong_at_four_elements()
    {
        // The wave's element is fixed so the matchup is determined: without the trait some of these
        // skills are answering badly, and with it none of them are.
        List<WaveCreature> Themed() => Wave(4, 900_000f, 20f, source: Source.Machine);

        Build Motley(SkillShape shape)
        {
            var b = new Build { Shape = shape, SlotCapacity = 4 };
            b.Equip(TestBuilds.Skill("hammer_blow", Source.Body));
            b.Equip(TestBuilds.Skill("volley_spray", Source.Mind));
            b.Equip(TestBuilds.Skill("field_pulse", Source.Spirit));
            b.Equip(TestBuilds.Skill("snare_repay", Source.Nature));
            return b;
        }
        Build Three(SkillShape shape)
        {
            var b = new Build { Shape = shape, SlotCapacity = 4 };
            b.Equip(TestBuilds.Skill("hammer_blow", Source.Body));
            b.Equip(TestBuilds.Skill("volley_spray", Source.Mind));
            b.Equip(TestBuilds.Skill("field_pulse", Source.Spirit));
            b.Equip(TestBuilds.Skill("snare_repay", Source.Spirit));
            return b;
        }
        WaveMetrics Go(Func<SkillShape, Build> make, SkillShape shape)
        {
            var m = new WaveMetrics();
            SoloBattle.ResolveWave(Champ(), make(shape), new Hunter(), Themed(), 900, NoSwing,
                                   new Random(11), metrics: m);
            return m;
        }

        Up(Go(Motley, SkillShape.None).DeliveredDamage, Go(Motley, Trait("t_many_tongues")).DeliveredDamage,
           0.02f, "MANY TONGUES", "damage from a four-element build");

        // NEGATIVE CONTROL: three elements is not four.
        Same(Go(Three, SkillShape.None).DeliveredDamage, Go(Three, Trait("t_many_tongues")).DeliveredDamage,
             "MANY TONGUES", "damage from a three-element build");
    }

    // ── GEAR SETS ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>THE MATCHED SUIT — skills that share the worn set's element hit harder.</summary>
    [Fact]
    public void test_the_matched_suit_pays_the_skills_that_match_the_set()
    {
        // The set element is a COMPOSITION-time fact: the catalogue entry reads TraitContext and
        // returns nothing at all when no set is complete, which is the negative control below.
        var suited = Trait("t_matched_suit", new TraitContext(SetElement: Source.Machine));
        List<WaveCreature> Crowd() => Wave(3, 900_000f, 20f);

        var plain = Fight(SkillShape.None, Crowd(), NoSwing, source: Source.Machine);
        var matched = Fight(suited, Crowd(), NoSwing, source: Source.Machine);
        Up(plain.Metrics.DeliveredDamage, matched.Metrics.DeliveredDamage, 0.10f, "THE MATCHED SUIT", "damage on the set's element");

        // NEGATIVE CONTROL 1: a skill on another element gets nothing. The narrow reading of this
        // trait is exactly this — it pays agreement, it does not FORCE the element.
        var offPlain = Fight(SkillShape.None, Crowd(), NoSwing, source: Source.Body);
        var offMatched = Fight(suited, Crowd(), NoSwing, source: Source.Body);
        Same(offPlain.Metrics.DeliveredDamage, offMatched.Metrics.DeliveredDamage,
             "THE MATCHED SUIT", "damage on an element the set does not share");

        // NEGATIVE CONTROL 2: no set worn, no dials at all.
        Assert.Equal(SkillShape.None.Traits, Trait("t_matched_suit").Traits);
    }

    // ── REGIONS ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>HOMEGROUND — in a conquered region the first cast does not wait.</summary>
    [Fact]
    public void test_homeground_waives_the_opening_wait_in_a_conquered_region()
    {
        var home = Trait("t_homeground", new TraitContext(RegionConquered: true));
        List<WaveCreature> Bruiser() => Wave(1, 6_000_000f, 20f);

        var plain = Fight(SkillShape.None, Bruiser(), NoSwing);
        var conquered = Fight(home, Bruiser(), NoSwing);

        Up(plain.Metrics.Activations, conquered.Metrics.Activations, 0.001f, "HOMEGROUND", "casts");
        Up(plain.Metrics.DeliveredDamage, conquered.Metrics.DeliveredDamage, 0.001f, "HOMEGROUND", "damage delivered");

        // NEGATIVE CONTROL: an unconquered region composes to nothing whatsoever.
        Assert.False(Trait("t_homeground").FreeOpeningCast,
            "HOMEGROUND: the opening was waived in a region that has not been conquered.");
    }

    /// <summary>THE STUDIED PLACE — a mastered region's dead leave the wave biting softer.</summary>
    [Fact]
    public void test_the_studied_place_softens_the_wave_as_it_dies()
    {
        // CREATURES HAVE TO DIE for the weakening to have anything to read (`deadThisWave > 0` at the
        // bite path), so the wave must be killable — a fixture of unkillable biters would report a
        // working trait as dead.
        var studied = Trait("t_studied_place", new TraitContext(RegionsMastered: 3));
        List<WaveCreature> Biters() => Wave(7, 400f, 260f);

        var plain = Fight(SkillShape.None, Biters(), NoSwing, intervalMs: 400);
        var known = Fight(studied, Biters(), NoSwing, intervalMs: 400);

        Assert.True(plain.Metrics.CreaturesKilled > 1, "The fixture killed nothing — the wave never softens.");

        Assert.True(plain.Metrics.HealthDamage > 0, "The fixture took no damage — it cannot pose THE STUDIED PLACE.");
        Assert.True(known.Metrics.HealthDamage < plain.Metrics.HealthDamage,
            $"THE STUDIED PLACE: {known.Metrics.HealthDamage} health lost with three regions mastered against " +
            $"{plain.Metrics.HealthDamage} without — the weakening is not reaching the bite path.");

        // NEGATIVE CONTROL: nothing mastered, nothing softened.
        Assert.Equal(SkillShape.None.Traits, Trait("t_studied_place").Traits);
    }

    // ── FAILURE ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>WHAT KILLED YOU — the archetype that ended the last descents takes more.</summary>
    [Fact]
    public void test_what_killed_you_pays_only_against_the_kind_that_killed_you()
    {
        var grudge = Trait("t_what_killed_you", new TraitContext(LastWallArchetype: Archetype.Armoured));

        List<WaveCreature> Armoured() => Enumerable.Range(0, 3).Select(_ => new WaveCreature
        {
            MaxHealth = 900_000f, Health = 900_000f, Damage = 20f, Archetype = Archetype.Armoured,
        }).ToList();
        List<WaveCreature> Swarm() => Enumerable.Range(0, 3).Select(_ => new WaveCreature
        {
            MaxHealth = 900_000f, Health = 900_000f, Damage = 20f, Archetype = Archetype.Swarm,
        }).ToList();

        var plain = Fight(SkillShape.None, Armoured(), NoSwing);
        var angry = Fight(grudge, Armoured(), NoSwing);
        Up(plain.Metrics.DeliveredDamage, angry.Metrics.DeliveredDamage, 0.10f, "WHAT KILLED YOU", "damage against the wall");

        // NEGATIVE CONTROL: the wrong kind of creature. The identical fixture must not move at all.
        var otherPlain = Fight(SkillShape.None, Swarm(), NoSwing);
        var otherAngry = Fight(grudge, Swarm(), NoSwing);
        Same(otherPlain.Metrics.DeliveredDamage, otherAngry.Metrics.DeliveredDamage,
             "WHAT KILLED YOU", "damage against another archetype");

        // And with no streak at all it composes to nothing.
        Assert.Equal(SkillShape.None.Traits, Trait("t_what_killed_you").Traits);
    }

    // ── SURVIVAL ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>THE UNBROKEN THREAD — whole, and the first bite of the wave is stopped.</summary>
    [Fact]
    public void test_the_unbroken_thread_stops_the_first_bite_while_whole()
    {
        // ONE wave, opened whole. The guard reads HEALTH at full rather than "once a wave", so a
        // multi-wave fixture would only ever pose it once anyway — the champion is hurt from wave two
        // onward and the card's sentence is exactly that. Bites must not kill, or both runs end at
        // zero and the comparison says nothing.
        List<WaveCreature> Biters() => Wave(1, 700_000f, 500f);

        var plain = Fight(SkillShape.None, Biters(), NoSwing, intervalMs: 900);
        var thread = Fight(Trait("t_unbroken_thread"), Biters(), NoSwing, intervalMs: 900);

        Assert.True(plain.Metrics.HealthDamage > 0, "The fixture took no damage — it cannot pose THE UNBROKEN THREAD.");
        Assert.True(thread.Champ.Alive && plain.Champ.Alive, "The fixture killed the champion — the comparison says nothing.");
        Assert.True(thread.Metrics.DamagePrevented > plain.Metrics.DamagePrevented,
            "THE UNBROKEN THREAD: nothing was prevented — the guard is not reaching the bite path.");
        Assert.True(thread.Champ.Health > plain.Champ.Health,
            $"THE UNBROKEN THREAD: the champion ended on {thread.Champ.Health} and {plain.Champ.Health} without.");

        // NEGATIVE CONTROL: a champion who opens hurt is not whole, and the guard does not fire.
        var hurtPlain = Fight(SkillShape.None, Biters(), NoSwing, intervalMs: 900, startHealth: 100_000);
        var hurtThread = Fight(Trait("t_unbroken_thread"), Biters(), NoSwing, intervalMs: 900, startHealth: 100_000);
        Same(hurtPlain.Metrics.DamagePrevented, hurtThread.Metrics.DamagePrevented,
             "THE UNBROKEN THREAD", "damage prevented for a champion who opened hurt");
    }

    // ── THE WHOLE CATALOGUE ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every trait in the catalogue has a liveness test above, by name. This is the guard that makes
    /// the suite fail when a trait is ADDED without one, which is the only way twenty-six tests stay
    /// twenty-six tests.
    /// </summary>
    [Fact]
    public void test_every_trait_in_the_catalogue_has_a_liveness_test()
    {
        var covered = new[]
        {
            "t_deep_cut", "t_spill", "t_carrion_weight", "t_spreading_fire", "t_certain_hand",
            "t_opened_vein", "t_scar_tissue", "t_standing_plate", "t_answering_wall", "t_last_breath",
            "t_thin_line", "t_practised_flesh", "t_given_hand", "t_mirror", "t_settling_weight",
            "t_last_word", "t_lingering_mark", "t_kept_word", "t_price_paid", "t_single_note",
            "t_many_tongues", "t_matched_suit", "t_homeground", "t_studied_place", "t_what_killed_you",
            "t_unbroken_thread",
        };

        var missing = TraitCatalogue.All.Select(t => t.Id).Except(covered).ToList();
        Assert.True(missing.Count == 0,
            $"These traits have no liveness test: {string.Join(", ", missing)}. " +
            "A decorative trait is a bug (BRIEF §101) — add the fight that proves it moves something.");

        var stale = covered.Except(TraitCatalogue.All.Select(t => t.Id)).ToList();
        Assert.True(stale.Count == 0, $"These ids are tested but no longer in the catalogue: {string.Join(", ", stale)}.");
        Assert.Equal(26, TraitCatalogue.All.Count);
    }
}
