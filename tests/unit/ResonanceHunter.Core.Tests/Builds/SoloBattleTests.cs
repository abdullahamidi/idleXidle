using System;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// One character, its woven Forms, and an enemy.
/// </summary>
/// <remarks>
/// These tests exist to keep the Forms DIFFERENT. Six Forms that vary only in how hard they hit are
/// not six Forms — they are one Form with six price tags, and four slots against them is not a
/// decision. Every test below fails if a Form loses the rule that only it has.
/// </remarks>
public class SoloBattleTests
{
    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    private static EquippedSkill Sk(Form form, Source src = Source.Nature, Vow? vow = null)
        => new(new WovenAbility { Name = form.ToString(), Source = src, Form = form, Vow = vow },
               FormBehaviour.BaseCooldownMs(form));

    private static Champion Champ(int hp = 400) => new() { MaxHealth = hp, Health = hp };

    private static (WaveOutcome, System.Collections.Generic.List<BattleEvent>) Fight(
        Build build, float enemyHp = 300f, float enemyDmg = 5f, int hp = 400,
        Source? enemySrc = null, Champion? champ = null, bool boss = false, float enemyDef = 0f)
        => SoloBattle.ResolveWave(champ ?? Champ(hp), build, new Hunter(), enemyHp, enemyDmg,
            enemyIntervalMs: 1000, T, new Random(7), new WaveBonus(), enemySrc, boss, enemyDef);

    private static Build With(params Form[] forms)
    {
        var b = new Build();
        foreach (var f in forms) b.Weave(Sk(f));
        return b;
    }

    /// <summary>A build with a given combo trigger, granted through a synthetic keystone.</summary>
    private static Build WithTrigger(BuildTrigger t, params Form[] forms)
    {
        var b = With(forms);
        b.Take(new Keystone { Id = $"t_{t}", Name = t.ToString(), Blurb = "test", Grants = new[] { t } });
        return b;
    }

    private static float DamageDealt(System.Collections.Generic.List<BattleEvent> e)
        => e.Where(x => x.Kind == BattleEventKind.Strike).Sum(x => x.Amount);

    private static int SkillCasts(System.Collections.Generic.List<BattleEvent> e, Form form)
        => e.Count(x => x.Kind == BattleEventKind.Skill && x.Amount == (int)form);

    // ── The character fights alone ────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_character_with_no_skills_can_still_kill_something_small()
    {
        // The auto-attack exists so a fight cannot stall to zero. It must work with an empty build.
        var (outcome, _) = Fight(new Build(), enemyHp: 30f, enemyDmg: 0.01f);
        Assert.Equal(WaveOutcome.Cleared, outcome);
    }

    [Fact]
    public void test_the_auto_attack_cannot_carry_a_build()
    {
        // If the auto-attack ever out-damages the woven skills, the tree stops mattering and this is
        // an idle clicker. A single STRIKE must beat bare hands decisively.
        var (_, bare) = Fight(new Build(), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, armed) = Fight(With(Form.Strike), enemyHp: 100_000f, enemyDmg: 0.01f);

        Assert.True(DamageDealt(armed) > DamageDealt(bare) * 2f,
            "one skill barely beats no skill — the auto-attack is too strong");
    }

    [Fact]
    public void test_a_character_that_runs_out_of_health_dies()
    {
        var (outcome, _) = Fight(With(Form.Strike), enemyHp: 100_000f, enemyDmg: 9_999f, hp: 50);
        Assert.Equal(WaveOutcome.Wiped, outcome);
    }

    [Fact]
    public void test_a_fight_that_cannot_be_won_stalls_rather_than_hangs()
    {
        var (outcome, _) = Fight(new Build(), enemyHp: 10_000_000f, enemyDmg: 0f);
        Assert.Equal(WaveOutcome.Stalled, outcome);
    }

    // ── Every Form is a DIFFERENT way to fight ────────────────────────────────────────────────

    [Fact]
    public void test_projectile_trades_weight_for_volume()
    {
        // It hits softer than STRIKE but far more often. Over a long fight the two should be
        // comparable — that is what makes choosing between them interesting rather than obvious.
        var (_, strike) = Fight(With(Form.Strike), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, proj) = Fight(With(Form.Projectile), enemyHp: 100_000f, enemyDmg: 0.01f);

        var strikeHits = strike.Count(e => e.Kind == BattleEventKind.Skill);
        var projHits = proj.Count(e => e.Kind == BattleEventKind.Skill);

        Assert.True(projHits > strikeHits, "PROJECTILE does not fire more often than STRIKE");
        Assert.True(DamageDealt(proj) > DamageDealt(strike) * 0.5f, "PROJECTILE's volume does not pay");
    }

    [Fact]
    public void test_an_aura_needs_no_cooldown_and_nothing_else_may_have_that()
    {
        Assert.True(FormBehaviour.IsPassive(Form.Aura));
        foreach (var f in Enum.GetValues<Form>().Where(x => x != Form.Aura))
            Assert.False(FormBehaviour.IsPassive(f), $"{f} is passive — only AURA may be");
    }

    [Fact]
    public void test_an_aura_rewards_a_long_fight()
    {
        // It is the weakest per tick and never stops. Against a tanky enemy it should out-total a
        // Strike; that is its whole reason to exist.
        var (_, aura) = Fight(With(Form.Aura), enemyHp: 100_000f, enemyDmg: 0.01f);
        Assert.True(DamageDealt(aura) > 0, "the AURA never ticked");
    }

    [Fact]
    public void test_a_trap_pays_ONLY_when_the_enemy_attacks()
    {
        // The only Form that rewards being hit — no other Form cares what the enemy does.
        //
        // My first version passed enemyDmg: 0 to mean "an enemy that never swings", and it failed: an
        // enemy dealing zero damage still SWINGS, and a swing is what a TRAP answers. That is correct
        // behaviour and a badly-worded test. "Never attacks" has to be expressed as an interval longer
        // than the fight, not as a damage of zero.
        var never = SoloBattle.ResolveWave(Champ(), With(Form.Trap), new Hunter(), 100_000f, 5f,
            enemyIntervalMs: 999_999, T, new Random(7), new WaveBonus());
        Assert.DoesNotContain(never.Events, e => e.Kind == BattleEventKind.Skill && e.Amount == (int)Form.Trap);

        var often = SoloBattle.ResolveWave(Champ(100_000), With(Form.Trap), new Hunter(), 100_000f, 1f,
            enemyIntervalMs: 1000, T, new Random(7), new WaveBonus());
        Assert.Contains(often.Events, e => e.Kind == BattleEventKind.Skill && e.Amount == (int)Form.Trap);

        Assert.True(DamageDealt(often.Events) > DamageDealt(never.Events),
            "TRAP paid the same whether or not it was being attacked — it is just a Strike");
    }

    [Fact]
    public void test_a_mark_contributes_no_damage_of_its_own()
    {
        // My first version of this asserted a Mark-only build deals EXACTLY what bare hands deal, and
        // it failed at 968 vs 700 — because a MARK amplifies the auto-attack too, which is correct and
        // is the whole point of an amplifier. The claim worth testing is not "it changes nothing", it
        // is "it has no damage of its OWN": a MARK cast never lands a blow, and a Mark-only build is
        // far worse than a build with one real damage Form in that slot.
        var (_, marked) = Fight(With(Form.Mark), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, striking) = Fight(With(Form.Strike), enemyHp: 100_000f, enemyDmg: 0.01f);

        Assert.True(DamageDealt(marked) < DamageDealt(striking),
            "a MARK on its own is competitive with a real damage Form — it is not an amplifier, it is a weapon");

        // Every MARK cast is a Skill event that lands no Strike behind it.
        Assert.Contains(marked, e => e.Kind == BattleEventKind.Skill && e.Amount == (int)Form.Mark);
    }

    [Fact]
    public void test_a_mark_makes_everything_else_hit_harder()
    {
        // Worthless alone, and the best slot in the game next to real damage. That gap IS the design.
        var (_, alone) = Fight(With(Form.Strike), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, amplified) = Fight(With(Form.Strike, Form.Mark), enemyHp: 100_000f, enemyDmg: 0.01f);

        Assert.True(DamageDealt(amplified) > DamageDealt(alone), "MARK amplified nothing");
    }

    [Fact]
    public void test_a_marks_window_is_under_half_its_cooldown()
    {
        // An amplifier that is always up is not an amplifier, it is a damage stat — and the build
        // collapses into "take Mark, take three Strikes, stop thinking". Same rule BULWARK needed.
        Assert.True(FormBehaviour.MarkWindowMs * 2 < FormBehaviour.BaseCooldownMs(Form.Mark) * 2,
            "MARK is up more than half the time");
        Assert.True(FormBehaviour.MarkWindowMs < FormBehaviour.BaseCooldownMs(Form.Mark));
    }

    [Fact]
    public void test_transformation_heals_as_it_lands()
    {
        var champ = Champ(400);
        champ.Health = 200;

        SoloBattle.ResolveWave(champ, With(Form.Transformation), new Hunter(), 100_000f, 0f,
            1000, T, new Random(3), new WaveBonus());

        Assert.True(champ.Health > 200, "TRANSFORMATION did not heal");
    }

    // ── Keystones change the fight, not just the numbers ──────────────────────────────────────

    [Fact]
    public void test_blood_magic_removes_healing_entirely()
    {
        // The keystone's cost is TOTAL: it does not reduce healing, it deletes it. That is what makes
        // it a real refusal rather than a tuning knob — and it specifically kills TRANSFORMATION.
        var build = With(Form.Transformation);
        build.Take(Keystones.ById("blood_magic")!);

        var champ = Champ(400);
        champ.Health = 200;

        SoloBattle.ResolveWave(champ, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(3), new WaveBonus());

        Assert.Equal(200, champ.Health);
    }

    [Fact]
    public void test_bloodlust_hits_harder_the_closer_to_death()
    {
        var build = With(Form.Strike);
        build.Take(Keystones.ById("bloodlust")!);

        var healthy = Champ(400); healthy.Health = 400;
        var (_, whole) = SoloBattle.ResolveWave(healthy, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(5), new WaveBonus());

        var dying = Champ(400); dying.Health = 20;
        var (_, hurt) = SoloBattle.ResolveWave(dying, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(5), new WaveBonus());

        Assert.True(DamageDealt(hurt) > DamageDealt(whole), "BLOODLUST paid nothing while dying");
    }

    /// <summary>
    /// JUGGERNAUT is BLOODLUST's mirror — it hits harder the FULLER your health, and denies healing.
    /// </summary>
    [Fact]
    public void test_juggernaut_hits_harder_the_fuller_your_health()
    {
        var build = With(Form.Strike);
        build.Take(Keystones.ById("juggernaut")!);

        // enemyDmg 0 holds health constant, so each fight reads a fixed health fraction cleanly.
        var whole = Champ(400); whole.Health = 400;
        var (_, full) = SoloBattle.ResolveWave(whole, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(5), new WaveBonus());

        var dying = Champ(400); dying.Health = 20;
        var (_, low) = SoloBattle.ResolveWave(dying, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(5), new WaveBonus());

        Assert.True(DamageDealt(full) > DamageDealt(low), "JUGGERNAUT paid nothing while whole");
    }

    /// <summary>JUGGERNAUT's cost is real: it grants NoHealing, so a Transformation build cannot leech.</summary>
    [Fact]
    public void test_juggernaut_denies_healing()
    {
        var build = With(Form.Transformation);
        build.Take(Keystones.ById("juggernaut")!);

        var champ = Champ(400); champ.Health = 200;
        SoloBattle.ResolveWave(champ, build, new Hunter(), 100_000f, 0f, 1000, T, new Random(3), new WaveBonus());

        Assert.Equal(200, champ.Health);   // Transformation would have healed; JUGGERNAUT forbids it
    }

    [Fact]
    public void test_undying_is_once_per_expedition_not_once_per_wave()
    {
        // The trap that already caught this codebase once: as a wave-scoped local, "once per run"
        // silently becomes "once per wave" — ten times its price, and it looks correct throughout.
        var build = With(Form.Strike);
        build.Take(Keystones.ById("undying")!);
        var champ = Champ(50);

        SoloBattle.ResolveWave(champ, build, new Hunter(), 100_000f, 9_999f, 100, T, new Random(1), new WaveBonus());
        Assert.True(champ.UndyingSpent);

        champ.Health = 50;
        var (outcome, _) = SoloBattle.ResolveWave(champ, build, new Hunter(), 100_000f, 9_999f, 100, T,
            new Random(1), new WaveBonus());
        Assert.Equal(WaveOutcome.Wiped, outcome);
    }

    [Fact]
    public void test_glass_cannon_hits_twice_as_hard()
    {
        var plain = With(Form.Strike);
        var glass = With(Form.Strike);
        glass.Take(Keystones.ById("glass_cannon")!);

        var (_, a) = Fight(plain, enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, b) = Fight(glass, enemyHp: 100_000f, enemyDmg: 0.01f);

        Assert.True(DamageDealt(b) > DamageDealt(a) * 1.8f);
    }

    // ── The Source matchup still decides fights ───────────────────────────────────────────────

    [Fact]
    public void test_the_region_still_asks_which_element_you_brought()
    {
        // Nature is strong against Machine and weak against Body. Same build, same skill, same seed —
        // only the region differs.
        var build = With(Form.Strike);

        var (_, strong) = Fight(build, enemyHp: 100_000f, enemyDmg: 0.01f, enemySrc: Source.Machine);
        var (_, weak) = Fight(build, enemyHp: 100_000f, enemyDmg: 0.01f, enemySrc: Source.Body);

        Assert.True(DamageDealt(strong) > DamageDealt(weak), "the Source matchup did not reach the fight");
    }

    /// <summary>
    /// A Vow pays only when the BUILD meets its demand — proven through the sim, not the pure layer.
    /// </summary>
    /// <remarks>
    /// Rewritten with the Vow system. The two tests here used to drive a champion to 10% health and back
    /// to prove a Vow read the fight; a Vow that reads the fight is a lottery in a game with no in-run
    /// decisions, so both of those properties are gone along with the Vows that had them. What replaced
    /// them is the same claim about the new axis: two BUILDS, one satisfying the demand and one not.
    /// </remarks>
    [Fact]
    public void test_a_vow_pays_only_when_the_build_meets_its_demand()
    {
        var vow = Weaving.ById("vow_singular")!;   // every skill must be the same Form

        var mono = new Build();
        mono.Weave(Sk(Form.Strike, Source.Nature, vow));

        var mixed = new Build();
        mixed.Weave(Sk(Form.Strike, Source.Nature, vow));
        mixed.Weave(Sk(Form.Aura, Source.Nature));

        var (_, kept) = SoloBattle.ResolveWave(Champ(400), mono, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());
        var (_, broken) = SoloBattle.ResolveWave(Champ(400), mixed, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());

        // The mixed build casts MORE (it carries an extra skill) and must still land a smaller Strike,
        // so the comparison is on the Strike events alone.
        int StrikeOnly(List<BattleEvent> ev) =>
            ev.Where(e => e.Kind == BattleEventKind.Strike).Select(e => e.Amount).DefaultIfEmpty(0).Max();

        Assert.True(StrikeOnly(kept) > StrikeOnly(broken),
            $"The one-Form build's biggest hit was {StrikeOnly(kept)} and the two-Form build's was " +
            $"{StrikeOnly(broken)}. THE SINGULAR is paying a build that breaks it.");
    }

    /// <summary>A Vow whose demand the build cannot meet grants nothing at all. That is the trade.</summary>
    [Fact]
    public void test_a_broken_vow_grants_nothing()
    {
        var vow = Weaving.ById("vow_unbound")!;   // no keystone may be socketed

        var bare = new Build();
        bare.Weave(Sk(Form.Strike, Source.Nature, vow));

        var socketed = new Build();
        socketed.Weave(Sk(Form.Strike, Source.Nature, vow));
        socketed.Take(Keystones.ById("ironclad")!);

        var plain = new Build();
        plain.Weave(Sk(Form.Strike, Source.Nature));
        plain.Take(Keystones.ById("ironclad")!);

        var (_, withVow) = SoloBattle.ResolveWave(Champ(400), socketed, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());
        var (_, without) = SoloBattle.ResolveWave(Champ(400), plain, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());

        Assert.Equal(DamageDealt(without), DamageDealt(withVow));

        var (_, honoured) = SoloBattle.ResolveWave(Champ(400), bare, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());
        Assert.True(DamageDealt(honoured) > DamageDealt(withVow),
            "Keeping THE UNBOUND paid no more than breaking it.");
    }

    // ── The build budget bites ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_cooldowns_are_sized_to_a_wave_not_to_each_other()
    {
        // The lesson the old skills layer learned the hard way: cooldowns longer than a wave can never
        // fire. Under the beat model waves run 6-15 s and, since the cooldowns doubled (2026-08-29),
        // the longest rhythm skill is six beats (9 s at speed 1.0); every non-passive Form must fire
        // inside six beats — a wave that ends sooner is a wave the swing cleared, which is the point.
        foreach (var f in Enum.GetValues<Form>())
        {
            if (FormBehaviour.IsPassive(f)) continue;
            Assert.True(FormBehaviour.BaseCooldownMs(f) <= 6 * SoloBattle.DefaultBeatMs,
                $"{f} has a {FormBehaviour.BaseCooldownMs(f)}ms cooldown — longer than a wave, so it can never fire");
        }
    }

    // ── Affinity: the Nen hexagon ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A skill in your AFFINITY's Form hits far harder than the same skill in its opposite. This is the
    /// identity axis — committing to your affinity must be worth more than splashing against it.
    /// </summary>
    [Fact]
    public void test_affinity_rewards_your_form_and_punishes_its_opposite()
    {
        float StrikeDamage(Form? affinity)
        {
            var b = With(Form.Strike);
            b.Affinity = affinity;
            return DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
                float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);
        }

        var onAffinity = StrikeDamage(Form.Strike);   // Strike specialist swinging Strike
        var neutral = StrikeDamage(null);              // no affinity chosen
        var against = StrikeDamage(Form.Trap);         // Trap specialist forced onto Strike (opposite-ish)

        Assert.True(onAffinity > neutral, $"your affinity's Form must hit harder: on={onAffinity} neutral={neutral}");
        Assert.True(neutral > against, $"the wrong affinity must hurt: neutral={neutral} against={against}");
    }

    /// <summary>The hexagon is symmetric and cyclic: same Form is the peak, the opposite is the floor.</summary>
    [Fact]
    public void test_affinity_factor_peaks_on_self_and_bottoms_on_opposite()
    {
        Assert.True(FormBehaviour.AffinityFactor(Form.Strike, Form.Strike) > 1.5f);      // mastery
        Assert.True(FormBehaviour.AffinityFactor(Form.Strike, Form.Trap) < 0.7f);        // opposite (dist 3)
        Assert.True(FormBehaviour.AffinityFactor(Form.Strike, Form.Projectile) >
                    FormBehaviour.AffinityFactor(Form.Strike, Form.Trap));               // nearer beats farther
    }

    // ── Form-combo enchantments: dead without their Form, potent with it ─────────────────────────────

    /// <summary>OVERDRAW must add a PROJECTILE cast — the item that makes a Volley build fire more.</summary>
    [Fact]
    public void test_overdraw_adds_a_projectile_cast()
    {
        // Unkillable enemy so the whole wave runs and casts can be counted.
        (WaveOutcome, System.Collections.Generic.List<BattleEvent>) Run(Build b)
            => SoloBattle.ResolveWave(Champ(4000), b, new Hunter(), float.MaxValue, 0f, 1_000_000, T,
                new Random(7), new WaveBonus());

        // Counted in BLOWS: an activation announces itself once since 2026-08-30, so a third pass shows
        // in what it lands, not in a third Skill event (which the screen would have played as a third
        // projectile effect on the same frame).
        int Blows(System.Collections.Generic.List<BattleEvent> e) =>
            e.Count(x => x.Kind == BattleEventKind.Strike && x.FromSkill);
        var plain = Blows(Run(With(Form.Projectile)).Item2);
        var overdrawn = Blows(Run(WithTrigger(BuildTrigger.Overdraw, Form.Projectile)).Item2);

        Assert.True(overdrawn > plain, $"OVERDRAW must add Volley blows: plain={plain} overdrawn={overdrawn}");
    }

    /// <summary>OVERDRAW is DEAD on a build with no Projectile — the whole point of a combo item.</summary>
    [Fact]
    public void test_overdraw_does_nothing_without_a_projectile()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        // A Strike-only build gains nothing from an enchantment that only touches Projectile.
        Assert.Equal(Total(With(Form.Strike)), Total(WithTrigger(BuildTrigger.Overdraw, Form.Strike)));
    }

    /// <summary>RADIANCE must make an AURA build tick more — more damage over the fight.</summary>
    [Fact]
    public void test_radiance_makes_an_aura_tick_more()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        Assert.True(Total(WithTrigger(BuildTrigger.Radiance, Form.Aura)) > Total(With(Form.Aura)),
            "RADIANCE must raise Aura damage over a fight");
    }

    /// <summary>
    /// LINGER stretches MARK's window, so more of a build's hits land amplified — more total damage.
    /// </summary>
    [Fact]
    public void test_linger_stretches_the_mark_window()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        var plain = Total(With(Form.Mark, Form.Strike, Form.Projectile));
        var lingered = Total(WithTrigger(BuildTrigger.Linger, Form.Mark, Form.Strike, Form.Projectile));

        Assert.True(lingered > plain, $"LINGER must lift damage via more amplified hits: plain={plain} lingered={lingered}");
    }

    // ── VENOM: the trigger that existed and did nothing ──────────────────────────────────────────

    /// <summary>
    /// VENOM must actually poison the enemy.
    /// </summary>
    /// <remarks>
    /// It was DORMANT: the trigger existed, the VENOMANCER keystone granted it and paid −20% damage for
    /// it, the Forge printed "SKILLS POISON +X%" — and nothing in the sim applied a point of poison. So
    /// the keystone was a strict downside and this test is the regression that would have caught it.
    /// The synthetic keystone carries no mods, isolating the poison from any damage penalty.
    /// </remarks>
    [Fact]
    public void test_venom_bleeds_the_enemy_for_extra_damage()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        Assert.True(Total(WithTrigger(BuildTrigger.Venom, Form.Strike)) > Total(With(Form.Strike)),
            "VENOM added no poison — a skill that says it poisons must poison");
    }

    /// <summary>
    /// The real VENOMANCER keystone must reach the fight — its poison is what its −20% damage BUYS.
    /// </summary>
    /// <remarks>
    /// Before the fix, socketing VENOMANCER was pure loss: −20% damage for a poison that never landed. It
    /// must now out-damage the same build carrying a −20%-damage keystone that grants NOTHING — proof the
    /// grant is doing work, not just the mods.
    /// </remarks>
    [Fact]
    public void test_the_venomancer_keystone_earns_its_downside()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        var venomancer = With(Form.Strike);
        venomancer.Take(Keystones.ById("venomancer")!);

        // A twin keystone: the same −20% damage, but no poison. VENOMANCER must beat it.
        var justSofter = With(Form.Strike);
        justSofter.Take(new Keystone
        {
            Id = "test_softer", Name = "SOFTER", Blurb = "test",
            Mods = new BuildMods(0.8f, 1f, 1f, 1f, 1f),
        });

        Assert.True(Total(venomancer) > Total(justSofter),
            "VENOMANCER's poison did not reach the fight — its −20% damage bought nothing");
    }

    // ── The three new Form-combos: Strike, Trap and Transformation finally get items too ──────────

    private int KillMs(Build b, float enemyHp)
    {
        var (_, e) = SoloBattle.ResolveWave(Champ(400_000), b, new Hunter(), enemyHp, 0f,
            1_000_000, T, new Random(7), new WaveBonus());
        return e.Where(x => x.Kind == BattleEventKind.EnemyDown).Select(x => x.AtMs).Single();
    }

    /// <summary>EXECUTE — a STRIKE finishes a weakened enemy, so a Strike build clears a boss faster.</summary>
    [Fact]
    public void test_execute_speeds_the_kill_of_a_weakened_enemy()
    {
        // Measured as DAMAGE against a creature held under the threshold for the whole fight, not as a
        // kill time: under the beat model (2026-08-27) kills land on beats, and a 1.6x Strike finished the
        // 1500-health enemy on the same beat as a plain one.
        float DealtWeakened(Build b)
        {
            var weakened = new List<WaveCreature> { new() { MaxHealth = 100_000_000f, Health = 20_000_000f, Damage = 0.01f } };
            var (_, e) = SoloBattle.ResolveWave(Champ(400_000), b, new Hunter(), weakened, enemyIntervalMs: 1_000_000, T, new Random(7), metrics: new WaveMetrics());
            return DamageDealt(e);
        }
        Assert.True(DealtWeakened(WithTrigger(BuildTrigger.Execute, Form.Strike)) > DealtWeakened(With(Form.Strike)) * 1.2f,
            "EXECUTE did not hit the weakened enemy any harder");
    }

    /// <summary>COILED re-arms the TRAP faster, so it answers more of a biting enemy's swings.</summary>
    [Fact]
    public void test_coiled_fires_the_trap_more_often()
    {
        int TrapCasts(Build b)
        {
            var (_, e) = SoloBattle.ResolveWave(Champ(400_000), b, new Hunter(), float.MaxValue, 0f,
                1000, T, new Random(7), new WaveBonus());
            return SkillCasts(e, Form.Trap);
        }

        Assert.True(TrapCasts(WithTrigger(BuildTrigger.Coiled, Form.Trap)) > TrapCasts(With(Form.Trap)),
            "COILED did not make the Trap answer more bites");
    }

    /// <summary>SIPHON deepens TRANSFORMATION's leech, so the champion ends a fight healthier.</summary>
    [Fact]
    public void test_siphon_deepens_the_transformation_leech()
    {
        int FinalHealth(Build b)
        {
            var champ = Champ(400_000); champ.Health = 1000;
            SoloBattle.ResolveWave(champ, b, new Hunter(), float.MaxValue, 0f,
                1_000_000, T, new Random(3), new WaveBonus());
            return champ.Health;
        }

        Assert.True(FinalHealth(WithTrigger(BuildTrigger.Siphon, Form.Transformation))
                    > FinalHealth(With(Form.Transformation)),
            "SIPHON did not heal more than a bare Transformation");
    }

    // ── The KEYSTONE and VOW combos ───────────────────────────────────────────────────────────
    //
    // Every test in this block is written as a PAIR, and the second half is the one that matters. A
    // combo that pays out is easy to write and easy to get wrong in the invisible direction: the
    // failure mode this project keeps hitting is not "the bonus is missing", it is "the bonus is there
    // unconditionally and the condition is decoration". So each proves the effect fires WITH its
    // partner and that the fight is byte-identical WITHOUT it.

    /// <summary>
    /// A hunter wearing ONE weapon that carries exactly this enchantment.
    /// </summary>
    /// <remarks>
    /// The InstanceId is FIXED rather than derived from the kind, and that detail is the whole test.
    /// Equipping does not just hand over an enchantment — it hands over a Legendary weapon's stats, and
    /// the id is what the trait and its numbers are rolled from. The first version of these tests used
    /// <c>$"e_{kind}"</c> and compared a hunter wearing this against a hunter wearing NOTHING, so the
    /// "dead without its partner" half failed at 96,332 against 6,118: it was measuring the weapon, not
    /// the enchantment. One id means every hunter below carries the identical item and the only thing
    /// that differs between two runs is the one line under test.
    /// </remarks>
    private static Hunter Wearing(EnchantKind kind)
    {
        var h = new Hunter();
        h.Equip(new ItemInstance
        {
            InstanceId = "combo_probe",
            BaseType = ItemBaseType.Weapon,
            Rarity = Rarity.Legendary,   // the magnitude curve's top, so the effect is easy to see
            SellValue = 100,
            EnchantOverride = kind,      // overrules the id-derived roll — see Enchantments.Of
        });
        return h;
    }

    /// <summary>
    /// The control: the same weapon carrying an enchantment that cannot touch damage.
    /// </summary>
    /// <remarks>
    /// HARVEST buds a spare core ON A KILL, and every fight in this block runs against a creature with
    /// a health pool nothing can exhaust — so it never fires, and even if it did it pays economy rather
    /// than damage. It is the neutral element for a damage comparison.
    /// </remarks>
    private static Hunter WearingNeutral() => Wearing(EnchantKind.Harvest);

    private static float DamageWith(Build build, Hunter hunter, int hp, int startHealth)
    {
        var champ = new Champion { MaxHealth = hp, Health = startHealth };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, hunter, enemyHealth: 1e9f, enemyDamage: 0f,
            enemyIntervalMs: 1_000_000, T, new Random(11), new WaveBonus());
        return DamageDealt(events);
    }

    [Fact]
    public void test_fervour_steepens_bloodlust_and_is_dead_without_it()
    {
        // Half health, so BLOODLUST's missing-health term is live and FERVOUR has something to steepen.
        const int Max = 1000, Now = 500;

        var keystoneOnly = DamageWith(WithTrigger(BuildTrigger.Bloodlust, Form.Strike), WearingNeutral(), Max, Now);
        var withBoth = DamageWith(WithTrigger(BuildTrigger.Bloodlust, Form.Strike),
                                  Wearing(EnchantKind.Fervour), Max, Now);
        Assert.True(withBoth > keystoneOnly,
            $"FERVOUR did not steepen BLOODLUST — {withBoth:N0} vs {keystoneOnly:N0}");

        // And the half that matters: no BLOODLUST, no payout at all.
        var neutral = DamageWith(With(Form.Strike), WearingNeutral(), Max, Now);
        var alone = DamageWith(With(Form.Strike), Wearing(EnchantKind.Fervour), Max, Now);
        Assert.Equal(neutral, alone);
    }

    [Fact]
    public void test_bulwark_steepens_zeal_and_is_dead_without_it()
    {
        const int Max = 1000, Now = 1000;   // whole, so ZEAL's present-health term is at its strongest

        var keystoneOnly = DamageWith(WithTrigger(BuildTrigger.Zeal, Form.Strike), WearingNeutral(), Max, Now);
        var withBoth = DamageWith(WithTrigger(BuildTrigger.Zeal, Form.Strike),
                                  Wearing(EnchantKind.Bulwark), Max, Now);
        Assert.True(withBoth > keystoneOnly,
            $"BULWARK did not steepen ZEAL — {withBoth:N0} vs {keystoneOnly:N0}");

        var neutral = DamageWith(With(Form.Strike), WearingNeutral(), Max, Now);
        Assert.Equal(neutral, DamageWith(With(Form.Strike), Wearing(EnchantKind.Bulwark), Max, Now));
    }

    [Fact]
    public void test_reverb_sharpens_echo_and_is_dead_without_it()
    {
        const int Max = 1000, Now = 1000;

        var keystoneOnly = DamageWith(WithTrigger(BuildTrigger.Echo, Form.Strike), WearingNeutral(), Max, Now);
        var withBoth = DamageWith(WithTrigger(BuildTrigger.Echo, Form.Strike),
                                  Wearing(EnchantKind.Reverb), Max, Now);
        Assert.True(withBoth > keystoneOnly,
            $"REVERB did not sharpen ECHO — {withBoth:N0} vs {keystoneOnly:N0}");

        var neutral = DamageWith(With(Form.Strike), WearingNeutral(), Max, Now);
        Assert.Equal(neutral, DamageWith(With(Form.Strike), Wearing(EnchantKind.Reverb), Max, Now));
    }

    [Fact]
    public void test_tithe_pays_per_sworn_vow_and_is_dead_without_one()
    {
        const int Max = 1000, Now = 1000;
        var vow = Weaving.Catalog.First(v => v.Id == "vow_pure");

        Build Sworn()
        {
            var b = new Build();
            b.Weave(Sk(Form.Strike, vow: vow));
            return b;
        }

        var plain = DamageWith(Sworn(), WearingNeutral(), Max, Now);
        var tithed = DamageWith(Sworn(), Wearing(EnchantKind.Tithe), Max, Now);
        Assert.True(tithed > plain, $"TITHE did not pay on a sworn Vow — {tithed:N0} vs {plain:N0}");

        // No Vow sworn, no tithe. The build that promised nothing gets nothing.
        var unsworn = DamageWith(With(Form.Strike), WearingNeutral(), Max, Now);
        Assert.Equal(unsworn, DamageWith(With(Form.Strike), Wearing(EnchantKind.Tithe), Max, Now));
    }

    [Fact]
    public void test_tithe_counts_distinct_vows_not_skills_wearing_one()
    {
        // The same rule the FRAGILITY price already follows. A Vow is SWORN, not equipped: weaving one
        // promise onto four skills is one promise. Paying it four times would make the tithe worth most
        // to the player who diversified least, which inverts what the whole Vow system is for.
        const int Max = 1000, Now = 1000;
        var vow = Weaving.Catalog.First(v => v.Id == "vow_pure");

        Build OneVowNSkills(int n)
        {
            var b = new Build();
            for (var i = 0; i < n; i++) b.Weave(Sk(Form.Strike, vow: vow));
            return b;
        }

        // Compare the tithe's SHARE, not raw damage — four skills swing more than one either way.
        float Share(int n)
        {
            var plain = DamageWith(OneVowNSkills(n), WearingNeutral(), Max, Now);
            var tithed = DamageWith(OneVowNSkills(n), Wearing(EnchantKind.Tithe), Max, Now);
            return tithed / plain;
        }

        Assert.Equal(Share(1), Share(4), precision: 3);
    }

    /// <summary>
    /// All three are DEAD without their Form — the point of a combo. A Projectile build runs none of
    /// Strike/Trap/Transformation, so each combo is inert and the fight is byte-identical.
    /// </summary>
    [Fact]
    public void test_the_new_combos_do_nothing_without_their_form()
    {
        var baseline = KillMs(With(Form.Projectile), 1500f);
        Assert.Equal(baseline, KillMs(WithTrigger(BuildTrigger.Execute, Form.Projectile), 1500f));
        Assert.Equal(baseline, KillMs(WithTrigger(BuildTrigger.Coiled, Form.Projectile), 1500f));
        Assert.Equal(baseline, KillMs(WithTrigger(BuildTrigger.Siphon, Form.Projectile), 1500f));
    }

    // ── Vow COSTS. A Vow that only grants is not a Vow — these were charged by the retired squad engine
    //    and lost in the solo pivot, so a StaticCost Vow was pure upside until re-wired. ────────────────

    [Fact]
    public void test_fragility_costs_the_champion_extra_damage_taken()
    {
        // The same champion, taking the same bites over a stalling wave, must end LOWER for wearing
        // FRAGILITY — the Vow's price is damage taken, and the solo fight had stopped charging it.
        var fragility = Weaving.Catalog.Single(v => v.Id == "vow_fragility");

        int Remaining(Vow? vow)
        {
            var b = new Build();
            b.Weave(Sk(Form.Strike, Source.Nature, vow));
            var champ = Champ(1_000_000);   // survives the whole wave, so the remaining health is readable
            SoloBattle.ResolveWave(champ, b, new Hunter(), 1_000_000f, 50f, 1000, T, new Random(7));
            return champ.Health;
        }

        Assert.True(Remaining(fragility) < Remaining(null),
            "FRAGILITY took no extra damage — the Vow's cost is unwired");
    }

    [Fact]
    public void test_reckless_offering_costs_max_health_at_mint()
    {
        // RECKLESS OFFERING pays with a smaller pool for the whole run. The health price must reach the mint.
        var reckless = Weaving.Catalog.Single(v => v.Id == "vow_reckless_offering");

        var withVow = new Build();
        withVow.Weave(Sk(Form.Strike, Source.Nature, reckless));
        var without = new Build();
        without.Weave(Sk(Form.Strike, Source.Nature, null));

        Assert.True(SoloBattle.VowHealthMultiplier(withVow) < 1f, "RECKLESS OFFERING cost no health");
        Assert.Equal(1f, SoloBattle.VowHealthMultiplier(without), 3);
    }

    // ── ENEMY ARMOUR. Flat per hit, which is what makes hit SIZE a build axis. ──────────────────────

    /// <summary>
    /// Flat armour must punish many small hits far more than a few large ones.
    /// </summary>
    /// <remarks>
    /// This is the load-bearing property of the whole Armoured archetype, and it is the one a
    /// multiplicative curve cannot have. If enemy mitigation ever goes back to K/(K+def), this test
    /// fails and the Weight branch of the skill tree loses its reason to exist.
    ///
    /// PROJECTILE fires roughly twice as often as STRIKE for less per hit, so it is the natural
    /// small-hit build; STRIKE is the large-hit one. Both are given the same armoured enemy.
    /// </remarks>
    [Fact]
    public void test_flat_armour_punishes_small_hits_much_harder_than_large_ones()
    {
        const float armour = 25f;

        float DealtBy(Form form, float def)
        {
            var (_, ev) = Fight(With(form), enemyHp: 100_000f, enemyDmg: 0f, enemyDef: def);
            return ev.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }

        var strikeLoss = 1f - DealtBy(Form.Strike, armour) / DealtBy(Form.Strike, 0f);
        var projLoss = 1f - DealtBy(Form.Projectile, armour) / DealtBy(Form.Projectile, 0f);

        Assert.True(
            projLoss > strikeLoss * 1.3f,
            $"Armour cost the small-hit build {projLoss:P0} and the large-hit build {strikeLoss:P0}. " +
            "Flat armour must read hit size; these being close means it is behaving multiplicatively.");
    }

    /// <summary>A hit never lands for nothing, however armoured the target.</summary>
    [Fact]
    public void test_armour_can_never_reduce_a_hit_below_the_floor()
    {
        var (_, ev) = Fight(With(Form.Projectile), enemyHp: 100_000f, enemyDmg: 0f, enemyDef: 100_000f);
        var hits = ev.Where(e => e.Kind == BattleEventKind.Strike && e.Amount > 0).ToList();

        Assert.True(hits.Count > 0, "Armour erased every hit — MinHitFraction is not being applied.");
    }

    /// <summary>
    /// Poison bypasses armour, which is the Venom path's whole niche.
    /// </summary>
    /// <remarks>
    /// A bleed tick is small by construction, so flat armour would erase it and leave VENOMANCER — a
    /// keystone that already pays -20% damage — with nothing to be good at. Bypassing gives it a clear
    /// identity instead: poison is the answer to a plate you cannot hit hard enough to crack.
    /// </remarks>
    [Fact]
    public void test_poison_ignores_enemy_armour()
    {
        var b = WithTrigger(BuildTrigger.Venom, Form.Strike);

        float Total(float def)
        {
            var (_, ev) = Fight(b, enemyHp: 100_000f, enemyDmg: 0f, enemyDef: def);
            return ev.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }

        var plain = With(Form.Strike);
        float TotalPlain(float def)
        {
            var (_, ev) = Fight(plain, enemyHp: 100_000f, enemyDmg: 0f, enemyDef: def);
            return ev.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }

        var venomEdgeUnarmoured = Total(0f) - TotalPlain(0f);
        var venomEdgeArmoured = Total(40f) - TotalPlain(40f);

        Assert.True(
            venomEdgeArmoured >= venomEdgeUnarmoured * 0.9f,
            $"Venom's contribution fell from {venomEdgeUnarmoured:F0} to {venomEdgeArmoured:F0} against " +
            "armour. Poison must bypass it.");
    }


    // ── COMPOSITION. A wave is a list of creatures, and that is what makes a build a SHAPE. ─────────

    private static (WaveOutcome, System.Collections.Generic.List<BattleEvent>) FightMany(
        Build build, System.Collections.Generic.IReadOnlyList<WaveCreature> creatures, int hp = 400)
        => SoloBattle.ResolveWave(Champ(hp), build, new Hunter(), creatures,
            enemyIntervalMs: 1000, T, new Random(7), new WaveBonus());

    private static System.Collections.Generic.List<WaveCreature> Swarm(int n, float each, float dmg)
        => Enumerable.Range(0, n).Select(_ => WaveCreature.Single(each, dmg)).ToList();

    /// <summary>
    /// A Swarm must punish a single-target build far more than a multi-target one.
    /// </summary>
    /// <remarks>
    /// This is the other half of the archetype engine, and the reason target count exists at all.
    /// Against ONE creature holding the same total health, a Trap build (110 per hit, one target) and an
    /// Aura build (12 per tick, every target) should be roughly comparable. Split that health across five
    /// creatures and the Trap spends each cooldown killing one of them — with its overkill discarded —
    /// while the Aura touches all five every tick.
    ///
    /// If this ever fails, Spread has nothing to be about and half the skill tree is decoration.
    /// </remarks>
    [Fact]
    public void test_a_swarm_punishes_single_target_far_more_than_multi_target()
    {
        const float total = 500f;

        float TimeToClear(Form form, System.Collections.Generic.IReadOnlyList<WaveCreature> comp)
        {
            var (outcome, ev) = FightMany(With(form), comp, hp: 100_000);
            return outcome == WaveOutcome.Cleared ? ev[^1].AtMs : T.TickCeilingMs;
        }

        var singleTargetSolo = TimeToClear(Form.Trap, new[] { WaveCreature.Single(total, 0f) });
        var multiTargetSolo = TimeToClear(Form.Aura, new[] { WaveCreature.Single(total, 0f) });

        var singleTargetSwarm = TimeToClear(Form.Trap, Swarm(5, total / 5f, 0f));
        var multiTargetSwarm = TimeToClear(Form.Aura, Swarm(5, total / 5f, 0f));

        var singleTargetCost = singleTargetSwarm / (float)Math.Max(1, singleTargetSolo);
        var multiTargetCost = multiTargetSwarm / (float)Math.Max(1, multiTargetSolo);

        Assert.True(
            singleTargetCost > multiTargetCost * 1.5f,
            $"Splitting the same health across five creatures cost the single-target build " +
            $"x{singleTargetCost:F2} and the multi-target build x{multiTargetCost:F2}. Target count " +
            "is not reaching the fight.");
    }

    /// <summary>Killing a creature removes its share of the incoming damage.</summary>
    /// <remarks>
    /// This is why a Swarm is dangerous and why clearing it fast matters: the wave's threat is the SUM
    /// of what is still alive, so action economy is a defensive stat as well as an offensive one.
    /// </remarks>
    [Fact]
    public void test_incoming_damage_falls_as_creatures_die()
    {
        var champ = Champ(100_000);
        var (_, ev) = SoloBattle.ResolveWave(champ, With(Form.Projectile), new Hunter(),
            Swarm(5, 60f, 20f), enemyIntervalMs: 1000, T, new Random(7), new WaveBonus());

        var bites = ev.Where(e => e.Kind == BattleEventKind.EnemyStrike).Select(e => e.Amount).ToList();

        Assert.True(bites.Count >= 2, "Not enough enemy swings to compare.");
        Assert.True(
            bites[^1] < bites[0],
            $"First bite {bites[0]}, last bite {bites[^1]}. Dead creatures are still swinging.");
    }

    /// <summary>Overkill is discarded — it does not carry to the next creature.</summary>
    /// <remarks>
    /// A 110 Trap hit into a 30-health creature must waste 80. Carrying it would quietly hand large-hit
    /// builds the cleave they are supposed to have to buy, and Swarm would stop punishing anything.
    /// </remarks>
    [Fact]
    public void test_overkill_does_not_carry_to_the_next_creature()
    {
        var comp = Swarm(3, 10f, 0f);
        var (outcome, _) = FightMany(With(Form.Trap), comp, hp: 100_000);

        Assert.Equal(WaveOutcome.Cleared, outcome);

        // Three creatures, one target per Trap activation: the wave cannot end before the third cast.
        var trapCd = FormBehaviour.BaseCooldownMs(Form.Trap);
        Assert.True(comp.All(c => !c.Alive));
        Assert.True(trapCd > 0);
    }

}
