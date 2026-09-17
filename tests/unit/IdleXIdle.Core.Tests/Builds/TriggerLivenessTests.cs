using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// Every <see cref="BuildTrigger"/>, proved to change something.
/// </summary>
/// <remarks>
/// <para>
/// <b>VENOM is why this file exists.</b> The venomancer keystone granted it, the tree taught it, a node
/// was named THE SLOW ROAD after it, and <c>SoloBattle</c> never once read it. Every test passed. The
/// keystone cost 20% damage for nothing at all, and the only reason it was caught is that someone
/// counted the reads by hand before wiring the next system.
/// </para>
/// <para>
/// That is the seventh time this project has built a complete, tested, entirely uncalled system —
/// Weaving, Memory Dust, evolution branches, Charm/Focus, Forge.Feed and item Element were the first
/// six. A trigger is exactly the shape that keeps slipping through: an enum that reads as a promise,
/// granted in one file and consumed in another, with nothing in the compiler joining the two.
/// </para>
/// <para>
/// So: one test per trigger, each showing an OUTCOME differ, and a roll-call that fails when a trigger
/// is added without one. The roll-call cannot prove a trigger is read — only that someone claimed it
/// is. The behavioural tests are the proof; the roll-call is what makes forgetting them loud.
/// </para>
/// </remarks>
public class TriggerLivenessTests
{
    private static readonly global::IdleXIdle.Core.Sources.Source Body
        = global::IdleXIdle.Core.Sources.Source.Body;

    private static EquippedSkill Sk(string skillId = "hammer_blow")
        => TestBuilds.Skill(skillId, Body);

    private static Build BuildWith(params string[] keystoneIds)
    {
        var build = new Build();
        build.Equip(Sk());
        foreach (var id in keystoneIds) build.Take(Keystones.ById(id)!);
        return build;
    }

    /// <summary>Total damage dealt to an unkillable, optionally-swinging enemy over the full ceiling.</summary>
    /// <summary>How many seeded runs <see cref="Output"/> averages — crit is a rolled event.</summary>
    /// <remarks>
    /// One seed stopped being a readable number on 2026-09-09. A trigger worth a few percent is
    /// smaller than one run's crit noise, so a single-seed comparison could report a live trigger as
    /// dead (and, worse, an inert one as live). Same discipline as <see cref="DamageBench.Runs"/>.
    /// </remarks>
    private const int OutputRuns = 24;

    private static float Output(Build build, float enemyDamage = 0f)
    {
        var total = 0f;
        for (var run = 0; run < OutputRuns; run++)
        {
            var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };
            var (_, events) = SoloBattle.ResolveWave(
                champ, build, new Hunter(),
                enemyHealth: 10_000_000f, enemyDamage: enemyDamage, enemyIntervalMs: 1_000,
                ExpeditionTuning.Default, new Random(99 + run * 7919));

            total += events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }
        return total / OutputRuns;
    }

    [Fact]
    public void test_loose_again_actually_looses_again()
    {
        // THE QUIVER's card reads "a kill sends the next shot immediately" and the character granted
        // SPLINTER, whose own blurb is "on kill: richer loot". Two different sentences, and the one on
        // the card was the one nothing implemented — on a passive a player unlocks by finishing a quest
        // specifically to get it.
        //
        // Measured on a SWARM: many weak creatures, so kills come often and the readied cooldowns
        // compound. A single-creature fixture would show nothing, which is correct — the passive is
        // meant to be dead weight on a boss.
        var swarm = Enumerable.Range(0, 8)
            .Select(_ => new WaveCreature { MaxHealth = 40f, Health = 40f, Damage = 0f })
            .ToArray();

        // CLEAR TIME, not cast count. Counting casts measures the creatures, not the cadence: a wave
        // ends when the last one dies, so eight creatures take eight killing casts however fast they
        // arrive. What the passive buys is those casts arriving SOONER.
        var plain = ClearMs(swarm.Select(Fresh).ToArray());
        var loose = ClearMs(swarm.Select(Fresh).ToArray(), BuildTrigger.LooseAgain);

        Assert.True(loose < plain,
                    $"LOOSE AGAIN must clear a swarm faster — {loose}ms against {plain}ms");
    }

    private static WaveCreature Fresh(WaveCreature c)
        => new() { MaxHealth = c.MaxHealth, Health = c.MaxHealth, Damage = c.Damage };

    /// <summary>The millisecond the wave was cleared — lower is a faster build.</summary>
    private static int ClearMs(WaveCreature[] creatures, params BuildTrigger[] triggers)
    {
        var build = new Build
        {
            PassiveMods = BuildMods.None,
            Shape = SkillShape.None,
            ExtraTriggers = new HashSet<BuildTrigger>(triggers),
        };
        build.Equip(TestBuilds.Skill("volley_spray", Source.Nature));

        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), creatures,
            enemyIntervalMs: 100_000, ExpeditionTuning.Default, new Random(5));
        return events.Count == 0 ? int.MaxValue : events.Max(e => e.AtMs);
    }

    // ── The roll-call ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_trigger_is_claimed_by_a_test_in_this_file()
    {
        // Hand-maintained ON PURPOSE. There is no reflection that can ask "does the sim read this?", so
        // the honest move is to make the omission impossible to commit quietly: add a trigger, this
        // fails, and the failure names the thing you have to go prove. The proof may live in a sibling
        // file — the combo triggers and ZEAL are proven in SoloBattleTests, where the Form-combo home is
        // — but it MUST exist and be named here, so this set stays the single index of "what is wired".
        var proved = new HashSet<BuildTrigger>
        {
            BuildTrigger.Venom,         // test_venom_actually_poisons
            BuildTrigger.Echo,          // test_echo_fires_twice
            BuildTrigger.Bloodlust,     // test_bloodlust_pays_for_being_hurt
            BuildTrigger.NoHealing,     // test_no_healing_switches_transformation_off
            BuildTrigger.Undying,       // test_undying_buys_exactly_one_death
            BuildTrigger.Splinter,      // test_splinter_pays_out_on_a_kill
            BuildTrigger.Harvest,       // test_harvest_pays_out_on_a_kill
            BuildTrigger.Zeal,          // SoloBattleTests.test_juggernaut_hits_harder_the_fuller_your_health
            BuildTrigger.Overdraw,      // SoloBattleTests.test_overdraw_adds_a_projectile_cast
            BuildTrigger.Linger,        // SoloBattleTests.test_linger_stretches_the_mark_window
            BuildTrigger.Radiance,      // SoloBattleTests.test_radiance_makes_an_aura_tick_more
            BuildTrigger.Execute,       // SoloBattleTests.test_execute_speeds_the_kill_of_a_weakened_enemy
            BuildTrigger.Coiled,        // SoloBattleTests.test_coiled_fires_the_trap_more_often
            BuildTrigger.Siphon,        // SoloBattleTests.test_siphon_deepens_the_transformation_leech
            BuildTrigger.Desperation,   // SoloExpeditionTests.test_desperation_swells_the_haul_at_low_health
            BuildTrigger.Hoarder,       // test_hoarder_turns_haul_into_force
            BuildTrigger.Weaver,        // test_weaver_fires_the_next_form_as_well
            BuildTrigger.LooseAgain,    // test_loose_again_actually_looses_again
            BuildTrigger.Rend,          // ChargeKeystoneTest.test_charge_rend_spends_the_pool_and_the_spend_pays
            BuildTrigger.Capacitor,     // ChargeKeystoneTest.test_charge_capacitor_raises_the_cap_and_alone_it_only_fills
            BuildTrigger.Dynamo,        // ChargeKeystoneTest.test_charge_dynamo_winds_the_pool_on_bites
            BuildTrigger.Lodestone,     // ChargeKeystoneTest.test_charge_lodestone_pays_a_core_for_a_full_pool_at_the_clear
        };

        // DESPERATION was parked here for a long time as "dead" — a HAUL effect the squad Expedition read
        // directly, knowing nothing about triggers. The solo model routes it through Build.Triggers, and
        // SoloExpedition.HaulForWave reads it, so it is proven like the rest and no longer the exception.
        foreach (var t in Enum.GetValues<BuildTrigger>())
            Assert.True(proved.Contains(t),
                $"{t} has no test proving anything reads it. This is how VENOM shipped dead.");
    }

    // ── VENOM ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_venom_actually_poisons()
    {
        // The test that did not exist, for the trigger that did nothing.
        var plain = Output(BuildWith());
        var venom = Output(BuildWith("venomancer"));

        Assert.True(venom > plain,
            $"VENOMANCER changed nothing ({plain} -> {venom}) — the trigger is granted and never read");
    }

    [Fact]
    public void test_venom_beats_its_own_damage_penalty_given_time()
    {
        // VENOMANCER is x0.8 damage. If poison could not out-earn that over a long fight, the keystone
        // would be a strict downgrade — and a keystone nobody can justify is a dead branch, not a choice.
        Assert.True(Output(BuildWith("venomancer")) > Output(BuildWith()));
    }

    [Fact]
    public void test_venom_ramps_rather_than_arriving_whole()
    {
        // Its identity, and the reason it is allowed to out-damage a plain build at the ceiling: poison
        // must be WORSE early. If a venom build were ahead from the first second there would be no trade,
        // just a better button.
        var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, BuildWith("venomancer"), new Hunter(),
            enemyHealth: 10_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_000,
            ExpeditionTuning.Default, new Random(99));

        // TWO CAST CYCLES PER WINDOW, not five seconds each: since the cooldowns doubled (2026-08-29) a
        // rhythm skill fires every four to six beats, so a five-second window holds one cast or none and
        // the comparison measured which window a cast happened to land in.
        var poison = events.Where(e => e.Kind == BattleEventKind.Strike).ToList();
        var early = poison.Where(e => e.AtMs <= 15_000).Sum(e => e.Amount);
        var late = poison.Where(e => e.AtMs > 15_000 && e.AtMs <= 30_000).Sum(e => e.Amount);

        Assert.True(late > early, $"venom did not ramp ({early} in the first 5s, {late} in the next)");
    }

    [Fact]
    public void test_venom_stops_climbing_at_the_cap()
    {
        // Venom is no longer a stack count with a hard ceiling; it is a decaying POOL — each skill adds
        // poison and half of the standing pool bleeds out every half-second — so it converges to an
        // equilibrium rather than a wall. The invariant the old cap protected is unchanged: poison must
        // PLATEAU. Under the old stacks model a 120-second fight ended at 120 stacks, an unbounded bleed
        // no boss could be tuned against; that regression would make the late window dwarf the mid one.
        var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };
        var (outcome, events) = SoloBattle.ResolveWave(
            champ, BuildWith("venomancer"), new Hunter(),
            enemyHealth: 10_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_000,
            ExpeditionTuning.Default, new Random(99));

        // The dummy is unkillable, so the fight runs the full ceiling — ample time for venom to settle
        // (the pool's half-life is one bleed tick, so it is fully converged within a few seconds).
        Assert.Equal(WaveOutcome.Stalled, outcome);

        float Window(int fromMs, int toMs) => events
            .Where(e => e.Kind == BattleEventKind.Strike && e.AtMs > fromMs && e.AtMs <= toMs)
            .Sum(e => e.Amount);

        // Skills and auto-attacks land at a fixed cadence and magnitude, so two equal LATE windows —
        // both well past the ramp — differ only by their venom contribution. Once poison has converged
        // the later window cannot out-damage the earlier one. (Tolerance absorbs one skill cast landing
        // on a window boundary; a pool that is still climbing would be multiples larger, not 10%.)
        var mid = Window(30_000, 60_000);
        var late = Window(90_000, 120_000);

        // 1.25, was 1.10: a STRIKE leaves WOUNDS (+3% per wound, five deep) and with six beats between
        // casts the wound stack is still filling through the mid window — the late window's extra is the
        // wound ramp, not the poison pool, which converges within a few casts either way.
        Assert.True(late <= mid * 1.25f,
            $"venom is still climbing late in the fight ({mid} over 30-60s, {late} over 90-120s) — the pool has no equilibrium");
    }

    [Fact]
    public void test_an_auto_attack_does_not_apply_venom()
    {
        // If the free swing stung, poison would cost the build nothing to maintain and a venom build
        // would want NO skills — which is the opposite of what the skill budget is for.
        var noSkills = new Build();
        noSkills.Take(Keystones.ById("venomancer")!);

        var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, noSkills, new Hunter(),
            enemyHealth: 10_000_000f, enemyDamage: 0f, enemyIntervalMs: 1_000,
            ExpeditionTuning.Default, new Random(99));

        var mods = noSkills.Resolve(new Hunter());
        // MIGHT's and the weapon's multiplier is the basic attack's own (2026-08-26); the shared mods still apply.
        var plain = SoloBattle.AutoAttackDamage * new Hunter().AutoDamageMultiplier * mods.Damage;
        var expectedAuto = (int)MathF.Round(plain);
        // THE SWING CRITS (2026-09-09). Since crit became a rolled event the basic attack rolls too, so
        // a swing lands at one of exactly TWO sizes and never at a third — which is still the whole
        // claim: a poisoned swing would land a bleed on top, and no bleed exists at either size.
        var expectedCrit = (int)MathF.Round(plain * SoloBattle.CritMultiplier(new Hunter()));

        var strikes = events.Where(e => e.Kind == BattleEventKind.Strike).ToList();
        Assert.All(strikes, e => Assert.Equal(e.Crit ? expectedCrit : expectedAuto, e.Amount));
        Assert.All(strikes, e => Assert.Equal(HitSource.Swing, e.Hit));
        Assert.Contains(strikes, e => e.Crit);   // and the base rate really does land some
    }

    // ── The rest of the roll-call ─────────────────────────────────────────────────────────────

    [Fact]
    public void test_echo_fires_twice()
    {
        // ECHO is x0.6 damage for two casts — a 1.2x net. Counted in BLOWS, not in Skill events: since
        // 2026-08-30 an activation announces itself exactly once however many passes it makes, because
        // the screen starts an effect, a sound and a callout per announcement and ECHO fired all three
        // twice on the same frame.
        var plain = new Build(); plain.Equip(Sk());
        var echo = new Build(); echo.Equip(Sk()); echo.Take(Keystones.ById("echo")!);

        (int Blows, int Damage) Landed(Build b)
        {
            var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };
            var (_, events) = SoloBattle.ResolveWave(champ, b, new Hunter(),
                10_000_000f, 0f, 1_000, ExpeditionTuning.Default, new Random(3));
            var blows = events.Where(e => e.Kind == BattleEventKind.Strike && e.FromSkill).ToList();
            return (blows.Count, blows.Sum(e => e.Amount));
        }

        var one = Landed(plain);
        var twice = Landed(echo);
        Assert.Equal(one.Blows * 2, twice.Blows);
        // ...and the pair lands about 1.2x what one cast does, which is the keystone's whole bargain.
        Assert.InRange(twice.Damage / (float)one.Damage, 1.1f, 1.3f);
        // One announcement per activation, whatever ECHO does behind it.
        var champCheck = new Champion { MaxHealth = 10_000, Health = 10_000 };
        var (_, echoEvents) = SoloBattle.ResolveWave(champCheck, echo, new Hunter(),
            10_000_000f, 0f, 1_000, ExpeditionTuning.Default, new Random(3));
        var announceMs = echoEvents.Where(e => e.Kind == BattleEventKind.Skill).Select(e => e.AtMs).ToList();
        Assert.Equal(announceMs.Distinct().Count(), announceMs.Count);
    }

    [Fact]
    public void test_bloodlust_pays_for_being_hurt()
    {
        // Damage scales with health MISSING, so the same build must hit harder while wounded. Compared
        // against ITSELF at two health levels — comparing to a plain build would fold in the 0.75 health
        // penalty and prove nothing about the trigger.
        float Hurt(int startingHealth)
        {
            var build = new Build(); build.Equip(Sk());
            build.Take(Keystones.ById("bloodlust")!);

            var champ = new Champion { MaxHealth = 1_000, Health = startingHealth };
            var (_, events) = SoloBattle.ResolveWave(champ, build, new Hunter(),
                10_000_000f, 0f, 1_000, ExpeditionTuning.Default, new Random(5));
            return events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
        }

        Assert.True(Hurt(100) > Hurt(1_000), "BLOODLUST did not pay more at low health");
    }

    [Fact]
    public void test_no_healing_switches_transformation_off()
    {
        // BLOOD MAGIC's cost is total: it does not reduce healing, it removes it. DRINK's lifesteal is
        // the healing it argues with, so DRAIN's active is what proves it.
        int HealEvents(bool bloodMagic)
        {
            var build = new Build();
            build.Equip(Sk("drain_drink"));
            if (bloodMagic) build.Take(Keystones.ById("blood_magic")!);

            var champ = new Champion { MaxHealth = 1_000, Health = 500 };
            var (_, events) = SoloBattle.ResolveWave(champ, build, new Hunter(),
                10_000_000f, 0f, 1_000, ExpeditionTuning.Default, new Random(11));
            return events.Count(e => e.Kind == BattleEventKind.Heal);
        }

        Assert.True(HealEvents(false) > 0, "TRANSFORMATION never healed — the baseline is broken");
        Assert.Equal(0, HealEvents(true));
    }

    [Fact]
    public void test_undying_buys_exactly_one_death()
    {
        // "Once per EXPEDITION" is why it lives on the Champion rather than in a wave-scoped local. A
        // second lethal wave must kill.
        //
        // ONE swing per wave, deliberately: the first cut of this let the enemy swing every second, and
        // UNDYING correctly bought one extra second before the next blow finished the job — so the wave
        // still read as Wiped and the test called a working trigger broken. What UNDYING promises is to
        // survive A lethal blow, not to survive an enemy standing over you for two minutes.
        const int OneSwingOnly = 100_000;

        var build = new Build();
        build.Take(Keystones.ById("undying")!);
        var champ = new Champion { MaxHealth = 10, Health = 10 };

        var (first, _) = SoloBattle.ResolveWave(champ, build, new Hunter(),
            10_000_000f, 500f, OneSwingOnly, ExpeditionTuning.Default, new Random(13));
        Assert.True(champ.UndyingSpent, "UNDYING was never spent — nothing read the trigger");
        Assert.NotEqual(WaveOutcome.Wiped, first);
        Assert.Equal(1, champ.Health);

        champ.Health = 10;
        var (second, _) = SoloBattle.ResolveWave(champ, build, new Hunter(),
            10_000_000f, 500f, OneSwingOnly, ExpeditionTuning.Default, new Random(13));
        Assert.Equal(WaveOutcome.Wiped, second);
    }

    [Fact]
    public void test_splinter_pays_out_on_a_kill()
    {
        var build = new Build(); build.Equip(Sk());
        build.Take(Keystones.ById("reaper")!);          // grants Splinter

        var bonus = new WaveBonus();
        var champ = new Champion { MaxHealth = 1_000, Health = 1_000 };
        SoloBattle.ResolveWave(champ, build, new Hunter(),
            enemyHealth: 1f, enemyDamage: 0f, enemyIntervalMs: 1_000,
            ExpeditionTuning.Default, new Random(17), bonus);

        Assert.True(bonus.Quality > 0f, "SPLINTER paid nothing on a kill");
    }

    [Fact]
    public void test_harvest_pays_out_on_a_kill()
    {
        // HARVEST comes from an enchantment rather than a keystone, and its on-kill core is rolled at
        // 25% — so this walks seeds until one pays rather than asserting on a single lucky run. It lives
        // in the WEAPON pool ({Splinter, Venom, Harvest}), not the Charm pool — the combo-axis rework
        // moved it — so this must roll weapons to reach it at all.
        var build = new Build();
        var hunter = new Hunter();

        var weapon = Enumerable.Range(0, 500)
            .Select(i => new ItemInstance
            {
                InstanceId = $"h{i}", BaseType = ItemBaseType.Weapon,
                Rarity = Rarity.Legendary, SellValue = 200,
            })
            .FirstOrDefault(it => Enchantments.Of(it)?.Kind == EnchantKind.Harvest);

        Assert.True(weapon is not null, "no weapon in 500 rolls carries HARVEST — the enchant is unreachable");
        hunter.Equip(weapon!);
        Assert.Contains(BuildTrigger.Harvest, build.Triggers(hunter));

        var paid = Enumerable.Range(1, 40).Any(seed =>
        {
            var bonus = new WaveBonus();
            var champ = new Champion { MaxHealth = 1_000, Health = 1_000 };
            SoloBattle.ResolveWave(champ, build, hunter,
                enemyHealth: 1f, enemyDamage: 0f, enemyIntervalMs: 1_000,
                ExpeditionTuning.Default, new Random(seed), bonus);
            return bonus.Cores > 0;
        });

        Assert.True(paid, "HARVEST never paid a core across 40 kills — nothing reads the trigger");
    }

    // ── HOARDER — the AVARICE terminal ────────────────────────────────────────────────────────

    /// <summary>Haul becomes force, or the whole economy path ends in money and nothing else.</summary>
    /// <remarks>
    /// The path HOARDER terminates buys no combat power at all — that is what makes choosing it a real
    /// decision — so this terminal is the only thing standing between "the economy build" and "the build
    /// that cannot fight". If it reads nothing, twelve permanent points buy a label.
    /// </remarks>
    [Fact]
    public void test_hoarder_turns_haul_into_force()
    {
        // GREED is the haul keystone; HOARDER is what converts what it bought.
        var greedy = BuildWith("greed");
        var hoarding = BuildWith("greed", "hoarder");

        Assert.True(Output(hoarding) > Output(greedy),
            $"HOARDER changed nothing ({Output(greedy):F0} -> {Output(hoarding):F0}). The AVARICE path's " +
            "terminal is inert and its whole road ends in money.");
    }

    /// <summary>Without haul to convert, HOARDER is nearly all price.</summary>
    /// <remarks>
    /// The other half of the trade, and what stops it being a free damage keystone. NOT "pays nothing":
    /// the Hunter's base Guile puts the haul multiplier a little above 1 before any investment, so a
    /// bare build does gain about 2% — measured, not assumed. What matters is the GAP: the same keystone
    /// is worth an order of magnitude more to a build that actually walked the economy path.
    /// </remarks>
    [Fact]
    public void test_hoarder_is_worth_far_more_to_a_haul_build()
    {
        var bareGain = Output(BuildWith("hoarder")) / Output(BuildWith()) - 1f;
        var haulGain = Output(BuildWith("greed", "hoarder")) / Output(BuildWith("greed")) - 1f;

        Assert.True(haulGain > bareGain * 5f,
            $"HOARDER gave {haulGain:P1} to a haul build and {bareGain:P1} to a bare one. It is meant " +
            "to convert what the AVARICE path bought, not to be a damage keystone anyone can splash.");
        Assert.True(Keystones.ById("hoarder")!.Mods.Rarity < 1f, "HOARDER must still charge its price.");
    }

    // ── WEAVER — the ARTIFICE terminal ────────────────────────────────────────────────────────

    /// <summary>
    /// Every skill also fires as the NEXT skill carried — one slot answering two demands.
    /// </summary>
    /// <remarks>
    /// The design's "two styles in one slot", and the only thing in the game that lets a single
    /// activation be both a Weight hit and a Spread hit. Needs two skills woven to mean anything,
    /// which is exactly the build commitment it is meant to demand.
    /// </remarks>
    [Fact]
    public void test_weaver_fires_the_next_form_as_well()
    {
        Build TwoWoven(params string[] keystones)
        {
            var b = new Build();
            b.Equip(Sk("hammer_blow"));
            b.Equip(Sk("volley_spray"));
            foreach (var id in keystones) b.Take(Keystones.ById(id)!);
            return b;
        }

        var plain = Output(TwoWoven());
        var woven = Output(TwoWoven("weaver"));

        Assert.True(woven > plain,
            $"WEAVER changed nothing ({plain:F0} -> {woven:F0}) — the ARTIFICE terminal is a label. " +
            "It costs 30% cadence, so an inert one is strictly worse than not taking it.");
    }
}
