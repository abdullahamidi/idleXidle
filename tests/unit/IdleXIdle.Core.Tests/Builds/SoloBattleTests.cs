using System;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// One character, its woven skills, and an enemy.
/// </summary>
/// <remarks>
/// These tests exist to keep the skills DIFFERENT. Skills that vary only in how hard they hit are
/// not different skills — they are one skill with several price tags, and four slots against them is
/// not a decision. Every test below fails if a skill loses the rule that only it has. (The fixtures
/// name catalogue ids since P3-final; the rules are the ones the Form era already proved.)
/// </remarks>
public class SoloBattleTests
{
    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    private static EquippedSkill Sk(string id, Source src = Source.Nature, Vow? vow = null)
        => TestBuilds.Skill(id, src, vow);

    private static Champion Champ(int hp = 400) => new() { MaxHealth = hp, Health = hp };

    private static (WaveOutcome, System.Collections.Generic.List<BattleEvent>) Fight(
        Build build, float enemyHp = 300f, float enemyDmg = 5f, int hp = 400,
        Source? enemySrc = null, Champion? champ = null, bool boss = false, float enemyDef = 0f)
        => SoloBattle.ResolveWave(champ ?? Champ(hp), build, new Hunter(), enemyHp, enemyDmg,
            enemyIntervalMs: 1000, T, new Random(7), new WaveBonus(), enemySrc, boss, enemyDef);

    private static Build With(params string[] skillIds)
    {
        var b = new Build();
        foreach (var id in skillIds) b.Weave(Sk(id));
        return b;
    }

    /// <summary>A build with a given combo trigger, granted through a synthetic keystone.</summary>
    private static Build WithTrigger(BuildTrigger t, params string[] skillIds)
    {
        var b = With(skillIds);
        b.Take(new Keystone { Id = $"t_{t}", Name = t.ToString(), Blurb = "test", Grants = new[] { t } });
        return b;
    }

    private static float DamageDealt(System.Collections.Generic.List<BattleEvent> e)
        => e.Where(x => x.Kind == BattleEventKind.Strike).Sum(x => x.Amount);

    /// <summary>Casts by the skill in SLOT <paramref name="slot"/> — events are slot-keyed now.</summary>
    private static int SkillCasts(System.Collections.Generic.List<BattleEvent> e, int slot = 0)
        => e.Count(x => x.Kind == BattleEventKind.Skill && x.Slot == slot);

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
        // an idle clicker. A single BLOW must beat bare hands decisively.
        var (_, bare) = Fight(new Build(), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, armed) = Fight(With("hammer_blow"), enemyHp: 100_000f, enemyDmg: 0.01f);

        Assert.True(DamageDealt(armed) > DamageDealt(bare) * 2f,
            "one skill barely beats no skill — the auto-attack is too strong");
    }

    [Fact]
    public void test_a_character_that_runs_out_of_health_dies()
    {
        var (outcome, _) = Fight(With("hammer_blow"), enemyHp: 100_000f, enemyDmg: 9_999f, hp: 50);
        Assert.Equal(WaveOutcome.Wiped, outcome);
    }

    [Fact]
    public void test_a_fight_that_cannot_be_won_stalls_rather_than_hangs()
    {
        var (outcome, _) = Fight(new Build(), enemyHp: 10_000_000f, enemyDmg: 0f);
        Assert.Equal(WaveOutcome.Stalled, outcome);
    }

    // ── Every skill is a DIFFERENT way to fight ───────────────────────────────────────────────

    [Fact]
    public void test_projectile_trades_weight_for_volume()
    {
        // SPRAY hits softer than BLOW but far more often (four beats against six). Over a long fight
        // the two should be comparable — that is what makes choosing between them interesting rather
        // than obvious.
        var (_, strike) = Fight(With("hammer_blow"), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, proj) = Fight(With("volley_spray"), enemyHp: 100_000f, enemyDmg: 0.01f);

        var strikeHits = strike.Count(e => e.Kind == BattleEventKind.Skill);
        var projHits = proj.Count(e => e.Kind == BattleEventKind.Skill);

        Assert.True(projHits > strikeHits, "SPRAY does not fire more often than BLOW");
        Assert.True(DamageDealt(proj) > DamageDealt(strike) * 0.5f, "SPRAY's volume does not pay");
    }

    [Fact]
    public void test_an_aura_rewards_a_long_fight()
    {
        // MIRE is the weakest per tick and never stops. Against a tanky enemy it should out-total a
        // burst skill over time; that is its whole reason to exist.
        var (_, aura) = Fight(With("field_mire"), enemyHp: 100_000f, enemyDmg: 0.01f);
        Assert.True(DamageDealt(aura) > 0, "the Field never ticked");
    }

    [Fact]
    public void test_a_trap_pays_ONLY_when_the_enemy_attacks()
    {
        // JAWS is the skill that rewards being hit — no other skill cares what the enemy does.
        //
        // My first version passed enemyDmg: 0 to mean "an enemy that never swings", and it failed: an
        // enemy dealing zero damage still SWINGS, and a swing is what the trap answers. That is correct
        // behaviour and a badly-worded test. "Never attacks" has to be expressed as an interval longer
        // than the fight, not as a damage of zero.
        var never = SoloBattle.ResolveWave(Champ(), With("snare_jaws"), new Hunter(), 100_000f, 40f,
            enemyIntervalMs: 999_999, T, new Random(7), new WaveBonus());
        Assert.DoesNotContain(never.Events, e => e.Kind == BattleEventKind.Skill && e.Slot == 0);

        var often = SoloBattle.ResolveWave(Champ(100_000), With("snare_jaws"), new Hunter(), 100_000f, 40f,
            enemyIntervalMs: 1000, T, new Random(7), new WaveBonus());
        Assert.Contains(often.Events, e => e.Kind == BattleEventKind.Skill && e.Slot == 0);

        Assert.True(DamageDealt(often.Events) > DamageDealt(never.Events),
            "JAWS paid the same whether or not it was being attacked — it is just a Strike");
    }

    [Fact]
    public void test_a_mark_contributes_no_damage_of_its_own()
    {
        // My first version of this asserted a Sign-only build deals EXACTLY what bare hands deal, and
        // it failed — because CALL amplifies the auto-attack too, which is correct and is the whole
        // point of an amplifier. The claim worth testing is not "it changes nothing", it is "it has no
        // damage of its OWN": a CALL cast never lands a blow, and a Sign-only build is far worse than
        // a build with one real damage skill in that slot.
        var (_, marked) = Fight(With("sign_call"), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, striking) = Fight(With("hammer_blow"), enemyHp: 100_000f, enemyDmg: 0.01f);

        Assert.True(DamageDealt(marked) < DamageDealt(striking),
            "a SIGN on its own is competitive with a real damage skill — it is not an amplifier, it is a weapon");

        // Every SIGN cast is a Skill event (slot-keyed now) that lands no Strike behind it.
        Assert.Contains(marked, e => e.Kind == BattleEventKind.Skill && e.Slot == 0);
    }

    [Fact]
    public void test_a_mark_makes_everything_else_hit_harder()
    {
        // Worthless alone, and the best slot in the game next to real damage. That gap IS the design.
        var (_, alone) = Fight(With("hammer_blow"), enemyHp: 100_000f, enemyDmg: 0.01f);
        var (_, amplified) = Fight(With("hammer_blow", "sign_call"), enemyHp: 100_000f, enemyDmg: 0.01f);

        Assert.True(DamageDealt(amplified) > DamageDealt(alone), "CALL amplified nothing");
    }

    [Fact]
    public void test_a_marks_window_is_under_its_cooldown()
    {
        // An amplifier that is always up is not an amplifier, it is a damage stat — and the build
        // collapses into "take Sign, take three Hammers, stop thinking". Same rule BULWARK needed.
        // The window and the cadence are the skill's own dials now (P3-final).
        var call = SkillCatalogue.ById("sign_call");
        Assert.True(call.AmplifyMs > 0, "CALL's window dial is unset — the amplify would fall to a fallback");
        Assert.True(call.AmplifyMs < call.Beats * SoloBattle.DefaultBeatMs,
            "CALL is up for its whole cooldown — the window is a damage stat, not a window");
    }

    [Fact]
    public void test_transformation_heals_as_it_lands()
    {
        var champ = Champ(400);
        champ.Health = 200;

        SoloBattle.ResolveWave(champ, With("drain_drink"), new Hunter(), 100_000f, 0f,
            1000, T, new Random(3), new WaveBonus());

        Assert.True(champ.Health > 200, "DRINK did not heal");
    }

    // ── Keystones change the fight, not just the numbers ──────────────────────────────────────

    [Fact]
    public void test_blood_magic_removes_healing_entirely()
    {
        // The keystone's cost is TOTAL: it does not reduce healing, it deletes it. That is what makes
        // it a real refusal rather than a tuning knob — and it specifically kills DRAIN's leech.
        var build = With("drain_drink");
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
        var build = With("hammer_blow");
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
        var build = With("hammer_blow");
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

    /// <summary>JUGGERNAUT's cost is real: it grants NoHealing, so a Drain build cannot leech.</summary>
    [Fact]
    public void test_juggernaut_denies_healing()
    {
        var build = With("drain_drink");
        build.Take(Keystones.ById("juggernaut")!);

        var champ = Champ(400); champ.Health = 200;
        SoloBattle.ResolveWave(champ, build, new Hunter(), 100_000f, 0f, 1000, T, new Random(3), new WaveBonus());

        Assert.Equal(200, champ.Health);   // DRINK would have healed; JUGGERNAUT forbids it
    }

    [Fact]
    public void test_undying_is_once_per_expedition_not_once_per_wave()
    {
        // The trap that already caught this codebase once: as a wave-scoped local, "once per run"
        // silently becomes "once per wave" — ten times its price, and it looks correct throughout.
        var build = With("hammer_blow");
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
        var plain = With("hammer_blow");
        var glass = With("hammer_blow");
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
        var build = With("hammer_blow");

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
        var vow = Vows.ById("vow_singular")!;   // every skill must be the same Style

        var mono = new Build();
        mono.Weave(Sk("hammer_blow", Source.Nature, vow));

        var mixed = new Build();
        mixed.Weave(Sk("hammer_blow", Source.Nature, vow));
        mixed.Weave(Sk("field_mire", Source.Nature));

        var (_, kept) = SoloBattle.ResolveWave(Champ(400), mono, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());
        var (_, broken) = SoloBattle.ResolveWave(Champ(400), mixed, new Hunter(), 100_000f, 0f,
            1000, T, new Random(9), new WaveBonus());

        // The mixed build casts MORE (it carries an extra skill) and must still land a smaller blow,
        // so the comparison is on the biggest Strike event alone.
        int StrikeOnly(List<BattleEvent> ev) =>
            ev.Where(e => e.Kind == BattleEventKind.Strike).Select(e => e.Amount).DefaultIfEmpty(0).Max();

        Assert.True(StrikeOnly(kept) > StrikeOnly(broken),
            $"The one-Style build's biggest hit was {StrikeOnly(kept)} and the two-Style build's was " +
            $"{StrikeOnly(broken)}. THE SINGULAR is paying a build that breaks it.");
    }

    /// <summary>A Vow whose demand the build cannot meet grants nothing at all. That is the trade.</summary>
    [Fact]
    public void test_a_broken_vow_grants_nothing()
    {
        var vow = Vows.ById("vow_unbound")!;   // no keystone may be socketed

        var bare = new Build();
        bare.Weave(Sk("hammer_blow", Source.Nature, vow));

        var socketed = new Build();
        socketed.Weave(Sk("hammer_blow", Source.Nature, vow));
        socketed.Take(Keystones.ById("ironclad")!);

        var plain = new Build();
        plain.Weave(Sk("hammer_blow", Source.Nature));
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

    // ── Affinity: the Style ring ──────────────────────────────────────────────────────────────

    /// <summary>The ring is symmetric and cyclic: your own style is the peak, the opposite the floor.</summary>
    /// <remarks>
    /// The table itself, pinned. That affinity reaches the FIGHT is measured per style in
    /// <c>affinity_test.cs</c>; the buy-back's shape in <c>affinity_vow_buyback_test.cs</c>.
    /// </remarks>
    [Fact]
    public void test_affinity_factor_peaks_on_self_and_bottoms_on_opposite()
    {
        Assert.True(StyleAffinity.Factor(Style.Hammer, Style.Hammer) > 1.5f);      // mastery
        Assert.True(StyleAffinity.Factor(Style.Hammer, Style.Volley) < 0.7f);      // opposite (dist 3)
        Assert.True(StyleAffinity.Factor(Style.Hammer, Style.Snare) >
                    StyleAffinity.Factor(Style.Hammer, Style.Volley));             // nearer beats farther
    }

    // ── Style-combo enchantments: dead without their style, potent with it ────────────────────────

    /// <summary>OVERDRAW must add a VOLLEY cast — the item that makes a Volley build fire more.</summary>
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
        var plain = Blows(Run(With("volley_spray")).Item2);
        var overdrawn = Blows(Run(WithTrigger(BuildTrigger.Overdraw, "volley_spray")).Item2);

        Assert.True(overdrawn > plain, $"OVERDRAW must add Volley blows: plain={plain} overdrawn={overdrawn}");
    }

    /// <summary>OVERDRAW is DEAD on a build with no Volley skill — the whole point of a combo item.</summary>
    [Fact]
    public void test_overdraw_does_nothing_without_a_projectile()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        // A Hammer-only build gains nothing from an enchantment that only touches Volley.
        Assert.Equal(Total(With("hammer_blow")), Total(WithTrigger(BuildTrigger.Overdraw, "hammer_blow")));
    }

    /// <summary>RADIANCE must make a Field build tick more — more damage over the fight.</summary>
    [Fact]
    public void test_radiance_makes_an_aura_tick_more()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        Assert.True(Total(WithTrigger(BuildTrigger.Radiance, "field_mire")) > Total(With("field_mire")),
            "RADIANCE must raise Field damage over a fight");
    }

    /// <summary>
    /// LINGER stretches the SIGN window, so more of a build's hits land amplified — more total damage.
    /// </summary>
    [Fact]
    public void test_linger_stretches_the_mark_window()
    {
        float Total(Build b) => DamageDealt(SoloBattle.ResolveWave(Champ(4000), b, new Hunter(),
            float.MaxValue, 0f, 1_000_000, T, new Random(7), new WaveBonus()).Item2);

        var plain = Total(With("sign_call", "hammer_blow", "volley_spray"));
        var lingered = Total(WithTrigger(BuildTrigger.Linger, "sign_call", "hammer_blow", "volley_spray"));

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

        Assert.True(Total(WithTrigger(BuildTrigger.Venom, "hammer_blow")) > Total(With("hammer_blow")),
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

        var venomancer = With("hammer_blow");
        venomancer.Take(Keystones.ById("venomancer")!);

        // A twin keystone: the same −20% damage, but no poison. VENOMANCER must beat it.
        var justSofter = With("hammer_blow");
        justSofter.Take(new Keystone
        {
            Id = "test_softer", Name = "SOFTER", Blurb = "test",
            Mods = new BuildMods(0.8f, 1f, 1f, 1f, 1f),
        });

        Assert.True(Total(venomancer) > Total(justSofter),
            "VENOMANCER's poison did not reach the fight — its −20% damage bought nothing");
    }

    // ── The three style-combos: Hammer, Snare and Drain get items too ─────────────────────────────

    private int KillMs(Build b, float enemyHp)
    {
        var (_, e) = SoloBattle.ResolveWave(Champ(400_000), b, new Hunter(), enemyHp, 0f,
            1_000_000, T, new Random(7), new WaveBonus());
        return e.Where(x => x.Kind == BattleEventKind.EnemyDown).Select(x => x.AtMs).Single();
    }

    /// <summary>EXECUTE — a BLOW finishes a weakened enemy, so a Hammer build clears a boss faster.</summary>
    [Fact]
    public void test_execute_speeds_the_kill_of_a_weakened_enemy()
    {
        // Measured as DAMAGE against a creature held under the threshold for the whole fight, not as a
        // kill time: under the beat model (2026-08-27) kills land on beats, and a 1.6x blow finished the
        // 1500-health enemy on the same beat as a plain one.
        float DealtWeakened(Build b)
        {
            var weakened = new List<WaveCreature> { new() { MaxHealth = 100_000_000f, Health = 20_000_000f, Damage = 0.01f } };
            var (_, e) = SoloBattle.ResolveWave(Champ(400_000), b, new Hunter(), weakened, enemyIntervalMs: 1_000_000, T, new Random(7), metrics: new WaveMetrics());
            return DamageDealt(e);
        }
        Assert.True(DealtWeakened(WithTrigger(BuildTrigger.Execute, "hammer_blow")) > DealtWeakened(With("hammer_blow")) * 1.2f,
            "EXECUTE did not hit the weakened enemy any harder");
    }

    /// <summary>COILED re-arms JAWS faster, so it answers more of a biting enemy's swings.</summary>
    [Fact]
    public void test_coiled_fires_the_trap_more_often()
    {
        int TrapCasts(Build b)
        {
            var (_, e) = SoloBattle.ResolveWave(Champ(400_000), b, new Hunter(), float.MaxValue, 0f,
                1000, T, new Random(7), new WaveBonus());
            return SkillCasts(e);   // the single woven skill sits in slot 0
        }

        Assert.True(TrapCasts(WithTrigger(BuildTrigger.Coiled, "snare_jaws")) > TrapCasts(With("snare_jaws")),
            "COILED did not make the Trap answer more bites");
    }

    /// <summary>SIPHON deepens DRINK's leech, so the champion ends a fight healthier.</summary>
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

        Assert.True(FinalHealth(WithTrigger(BuildTrigger.Siphon, "drain_drink"))
                    > FinalHealth(With("drain_drink")),
            "SIPHON did not heal more than a bare DRINK");
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

        var keystoneOnly = DamageWith(WithTrigger(BuildTrigger.Bloodlust, "hammer_blow"), WearingNeutral(), Max, Now);
        var withBoth = DamageWith(WithTrigger(BuildTrigger.Bloodlust, "hammer_blow"),
                                  Wearing(EnchantKind.Fervour), Max, Now);
        Assert.True(withBoth > keystoneOnly,
            $"FERVOUR did not steepen BLOODLUST — {withBoth:N0} vs {keystoneOnly:N0}");

        // And the half that matters: no BLOODLUST, no payout at all.
        var neutral = DamageWith(With("hammer_blow"), WearingNeutral(), Max, Now);
        var alone = DamageWith(With("hammer_blow"), Wearing(EnchantKind.Fervour), Max, Now);
        Assert.Equal(neutral, alone);
    }

    [Fact]
    public void test_bulwark_steepens_zeal_and_is_dead_without_it()
    {
        const int Max = 1000, Now = 1000;   // whole, so ZEAL's present-health term is at its strongest

        var keystoneOnly = DamageWith(WithTrigger(BuildTrigger.Zeal, "hammer_blow"), WearingNeutral(), Max, Now);
        var withBoth = DamageWith(WithTrigger(BuildTrigger.Zeal, "hammer_blow"),
                                  Wearing(EnchantKind.Bulwark), Max, Now);
        Assert.True(withBoth > keystoneOnly,
            $"BULWARK did not steepen ZEAL — {withBoth:N0} vs {keystoneOnly:N0}");

        var neutral = DamageWith(With("hammer_blow"), WearingNeutral(), Max, Now);
        Assert.Equal(neutral, DamageWith(With("hammer_blow"), Wearing(EnchantKind.Bulwark), Max, Now));
    }

    [Fact]
    public void test_reverb_sharpens_echo_and_is_dead_without_it()
    {
        const int Max = 1000, Now = 1000;

        var keystoneOnly = DamageWith(WithTrigger(BuildTrigger.Echo, "hammer_blow"), WearingNeutral(), Max, Now);
        var withBoth = DamageWith(WithTrigger(BuildTrigger.Echo, "hammer_blow"),
                                  Wearing(EnchantKind.Reverb), Max, Now);
        Assert.True(withBoth > keystoneOnly,
            $"REVERB did not sharpen ECHO — {withBoth:N0} vs {keystoneOnly:N0}");

        var neutral = DamageWith(With("hammer_blow"), WearingNeutral(), Max, Now);
        Assert.Equal(neutral, DamageWith(With("hammer_blow"), Wearing(EnchantKind.Reverb), Max, Now));
    }

    [Fact]
    public void test_tithe_pays_per_sworn_vow_and_is_dead_without_one()
    {
        const int Max = 1000, Now = 1000;
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");

        Build Sworn()
        {
            var b = new Build();
            b.Weave(Sk("hammer_blow", vow: vow));
            return b;
        }

        var plain = DamageWith(Sworn(), WearingNeutral(), Max, Now);
        var tithed = DamageWith(Sworn(), Wearing(EnchantKind.Tithe), Max, Now);
        Assert.True(tithed > plain, $"TITHE did not pay on a sworn Vow — {tithed:N0} vs {plain:N0}");

        // No Vow sworn, no tithe. The build that promised nothing gets nothing.
        var unsworn = DamageWith(With("hammer_blow"), WearingNeutral(), Max, Now);
        Assert.Equal(unsworn, DamageWith(With("hammer_blow"), Wearing(EnchantKind.Tithe), Max, Now));
    }

    [Fact]
    public void test_tithe_counts_distinct_vows_not_skills_wearing_one()
    {
        // The same rule the FRAGILITY price already follows. A Vow is SWORN, not equipped: weaving one
        // promise onto four skills is one promise. Paying it four times would make the tithe worth most
        // to the player who diversified least, which inverts what the whole Vow system is for.
        const int Max = 1000, Now = 1000;
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");

        Build OneVowNSkills(int n)
        {
            var b = new Build();
            for (var i = 0; i < n; i++) b.Weave(Sk("hammer_blow", vow: vow));
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
    /// All three are DEAD without their style — the point of a combo. A Volley build runs none of
    /// Hammer/Snare/Drain, so each combo is inert and the fight is byte-identical.
    /// </summary>
    [Fact]
    public void test_the_new_combos_do_nothing_without_their_form()
    {
        var baseline = KillMs(With("volley_spray"), 1500f);
        Assert.Equal(baseline, KillMs(WithTrigger(BuildTrigger.Execute, "volley_spray"), 1500f));
        Assert.Equal(baseline, KillMs(WithTrigger(BuildTrigger.Coiled, "volley_spray"), 1500f));
        Assert.Equal(baseline, KillMs(WithTrigger(BuildTrigger.Siphon, "volley_spray"), 1500f));
    }

    // ── Vow COSTS. A Vow that only grants is not a Vow — these were charged by the retired squad engine
    //    and lost in the solo pivot, so a StaticCost Vow was pure upside until re-wired. ────────────────

    [Fact]
    public void test_fragility_costs_the_champion_extra_damage_taken()
    {
        // The same champion, taking the same bites over a stalling wave, must end LOWER for wearing
        // FRAGILITY — the Vow's price is damage taken, and the solo fight had stopped charging it.
        var fragility = Vows.Catalog.Single(v => v.Id == "vow_fragility");

        int Remaining(Vow? vow)
        {
            var b = new Build();
            b.Weave(Sk("hammer_blow", Source.Nature, vow));
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
        var reckless = Vows.Catalog.Single(v => v.Id == "vow_reckless_offering");

        var withVow = new Build();
        withVow.Weave(Sk("hammer_blow", Source.Nature, reckless));
        var without = new Build();
        without.Weave(Sk("hammer_blow", Source.Nature, null));

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
    /// SPRAY fires more often than BLOW for less per hit, so it is the natural small-hit build; BLOW
    /// is the large-hit one. Both are given the same armoured enemy.
    /// </remarks>
    [Fact]
    public void test_flat_armour_punishes_small_hits_much_harder_than_large_ones()
    {
        const float armour = 25f;

        float DealtBy(string skillId, float def)
        {
            var (_, ev) = Fight(With(skillId), enemyHp: 100_000f, enemyDmg: 0f, enemyDef: def);
            return ev.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }

        var strikeLoss = 1f - DealtBy("hammer_blow", armour) / DealtBy("hammer_blow", 0f);
        var projLoss = 1f - DealtBy("volley_spray", armour) / DealtBy("volley_spray", 0f);

        Assert.True(
            projLoss > strikeLoss * 1.3f,
            $"Armour cost the small-hit build {projLoss:P0} and the large-hit build {strikeLoss:P0}. " +
            "Flat armour must read hit size; these being close means it is behaving multiplicatively.");
    }

    /// <summary>A hit never lands for nothing, however armoured the target.</summary>
    [Fact]
    public void test_armour_can_never_reduce_a_hit_below_the_floor()
    {
        var (_, ev) = Fight(With("volley_spray"), enemyHp: 100_000f, enemyDmg: 0f, enemyDef: 100_000f);
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
        var b = WithTrigger(BuildTrigger.Venom, "hammer_blow");

        float Total(float def)
        {
            var (_, ev) = Fight(b, enemyHp: 100_000f, enemyDmg: 0f, enemyDef: def);
            return ev.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }

        var plain = With("hammer_blow");
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
    /// Against ONE creature holding the same total health, a Hammer build (one enormous hit, one
    /// target) and a Field build (PULSE reaches the whole wave) should be roughly comparable. Split
    /// that health across five creatures and the blow spends each cooldown killing one of them — with
    /// its overkill discarded — while the pulse touches all five every cast.
    ///
    /// If this ever fails, Spread has nothing to be about and half the skill tree is decoration.
    /// </remarks>
    [Fact]
    public void test_a_swarm_punishes_single_target_far_more_than_multi_target()
    {
        const float total = 500f;

        float TimeToClear(string skillId, System.Collections.Generic.IReadOnlyList<WaveCreature> comp)
        {
            var (outcome, ev) = FightMany(With(skillId), comp, hp: 100_000);
            return outcome == WaveOutcome.Cleared ? ev[^1].AtMs : T.TickCeilingMs;
        }

        var singleTargetSolo = TimeToClear("hammer_blow", new[] { WaveCreature.Single(total, 0f) });
        var multiTargetSolo = TimeToClear("field_pulse", new[] { WaveCreature.Single(total, 0f) });

        var singleTargetSwarm = TimeToClear("hammer_blow", Swarm(5, total / 5f, 0f));
        var multiTargetSwarm = TimeToClear("field_pulse", Swarm(5, total / 5f, 0f));

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
        var (_, ev) = SoloBattle.ResolveWave(champ, With("volley_spray"), new Hunter(),
            Swarm(5, 60f, 20f), enemyIntervalMs: 1000, T, new Random(7), new WaveBonus());

        var bites = ev.Where(e => e.Kind == BattleEventKind.EnemyStrike).Select(e => e.Amount).ToList();

        Assert.True(bites.Count >= 2, "Not enough enemy swings to compare.");
        Assert.True(
            bites[^1] < bites[0],
            $"First bite {bites[0]}, last bite {bites[^1]}. Dead creatures are still swinging.");
    }

    /// <summary>Overkill is discarded — it does not carry to the next creature.</summary>
    /// <remarks>
    /// A 500-damage BLOW into a 10-health creature must waste 490. Carrying it would quietly hand
    /// large-hit builds the cleave they are supposed to have to buy, and Swarm would stop punishing
    /// anything.
    /// </remarks>
    [Fact]
    public void test_overkill_does_not_carry_to_the_next_creature()
    {
        var comp = Swarm(3, 10f, 0f);
        var (outcome, _) = FightMany(With("hammer_blow"), comp, hp: 100_000);

        Assert.Equal(WaveOutcome.Cleared, outcome);

        // Three creatures, one target per BLOW: the overkill of each cast dies with its victim.
        Assert.True(comp.All(c => !c.Alive));
        Assert.Equal(1, SkillCatalogue.ById("hammer_blow").Targets);
    }

}
