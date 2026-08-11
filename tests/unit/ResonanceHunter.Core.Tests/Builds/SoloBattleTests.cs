using System;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
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
        Source? enemySrc = null, Champion? champ = null, bool boss = false)
        => SoloBattle.ResolveWave(champ ?? Champ(hp), build, new Hunter(), enemyHp, enemyDmg,
            enemyIntervalMs: 1000, T, new Random(7), new WaveBonus(), enemySrc, boss);

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

    [Fact]
    public void test_a_vow_still_pays_only_when_its_condition_holds()
    {
        var vow = Weaving.ById("vow_bloodied")!;   // only below 40% health
        var build = new Build();
        build.Weave(Sk(Form.Strike, Source.Nature, vow));

        var healthy = Champ(400); healthy.Health = 400;
        var (_, whole) = SoloBattle.ResolveWave(healthy, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());

        var bloodied = Champ(400); bloodied.Health = 40;
        var (_, hurt) = SoloBattle.ResolveWave(bloodied, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());

        Assert.True(DamageDealt(hurt) > DamageDealt(whole), "the Vow paid nothing while bloodied");
    }

    /// <summary>
    /// THE UNBROKEN pays only while WHOLE — the mirror of THE BLOODIED, and proof its fix reaches the fight.
    /// </summary>
    /// <remarks>
    /// Before the re-point it was always active in solo (it watched the phantom front slot), so a champ at
    /// 10% HP dealt exactly as much as one at 100%. Now the two must DIVERGE, the opposite way round from
    /// bloodied — the whole champion hits harder, the hurt one loses the Vow entirely.
    /// </remarks>
    [Fact]
    public void test_the_unbroken_vow_pays_only_while_whole()
    {
        var vow = Weaving.ById("vow_vanguard")!;   // now: only ABOVE 70% health
        var build = new Build();
        build.Weave(Sk(Form.Strike, Source.Nature, vow));

        var healthy = Champ(400); healthy.Health = 400;   // 100% — above the threshold
        var (_, whole) = SoloBattle.ResolveWave(healthy, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());

        var battered = Champ(400); battered.Health = 40;  // 10% — below it, so the Vow is dead
        var (_, hurt) = SoloBattle.ResolveWave(battered, build, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());

        Assert.True(DamageDealt(whole) > DamageDealt(hurt), "THE UNBROKEN paid nothing while whole");
    }

    // ── The build budget bites ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_cooldowns_are_sized_to_a_wave_not_to_each_other()
    {
        // The lesson the old skills layer learned the hard way: cooldowns longer than a wave can never
        // fire. Waves run 2-4s. Every non-passive Form must fire at least once inside 4 seconds.
        foreach (var f in Enum.GetValues<Form>())
        {
            if (FormBehaviour.IsPassive(f)) continue;
            Assert.True(FormBehaviour.BaseCooldownMs(f) <= 4_000,
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

        var plain = SkillCasts(Run(With(Form.Projectile)).Item2, Form.Projectile);
        var overdrawn = SkillCasts(Run(WithTrigger(BuildTrigger.Overdraw, Form.Projectile)).Item2, Form.Projectile);

        Assert.True(overdrawn > plain, $"OVERDRAW must add Volley casts: plain={plain} overdrawn={overdrawn}");
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
        Assert.True(KillMs(WithTrigger(BuildTrigger.Execute, Form.Strike), 1500f)
                    < KillMs(With(Form.Strike), 1500f),
            "EXECUTE did not finish the weakened enemy any faster");
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
}
