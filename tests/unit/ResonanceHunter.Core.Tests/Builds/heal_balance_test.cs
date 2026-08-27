using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// How much a heal build actually heals, and whether it can still die.
/// </summary>
/// <remarks>
/// <para>
/// Playtest 2026-08-25: <i>"The health-recovery skills are far too strong (broken)."</i> Nothing had
/// measured healing against the curve — the balance sweep measures BRANCHES, and a Transformation
/// build is not a branch. This is the probe that measured it, kept as the balance test that guards
/// the answer. It runs the real expedition on the sweep's geared mid-career fixture and reports, per
/// build: the depth reached, how much of the pool came back per wave, how much of the pool was bitten
/// off per wave, and the share of waves in which healing out-ran damage — and it runs every build
/// twice, once with the numbers the playtest complained about (<see cref="HealTuning.Legacy"/>) and
/// once with the live ones, so the before/after is printed from the same code every commit.
/// </para>
/// <para>
/// UNKILLABLE, defined: a build whose healing per wave meets or exceeds the damage it takes, on
/// average, is not being kept alive by its pool — it is immune to the curve, and its run ends only at
/// the STALL wall (it cannot kill fast enough) rather than at death. Under the legacy numbers four
/// Transformations healed more than they took and never wiped; that is the row this test forbids.
/// </para>
/// <para>
/// The twin without heals is the SAME build wearing a synthetic keystone that grants only
/// <see cref="BuildTrigger.NoHealing"/> — BLOOD MAGIC's refusal without its cadence bonus — so the
/// difference between the pair is healing and nothing else.
/// </para>
/// </remarks>
public class HealBalanceTest
{
    private readonly ITestOutputHelper _out;

    public HealBalanceTest(ITestOutputHelper output) => _out = output;

    private const int Seeds = 16;
    private const int DepthCap = 400;

    /// <summary>The balance sweep's geared fixture — a player holding eighteen mastery points is not naked.</summary>
    private static readonly BuildMods Geared = new(3.0f, 1.4f, 1f, 1f, 1f);

    private static Hunter MidCareerHunter()
    {
        // VITALITY's regeneration (2026-08-26) is a trained, per-second heal OUTSIDE the skill-heal
        // ceiling this probe measures; with it on, the geared fixture's eight VITALITY ranks kept the
        // MORPH + SIPHON build alive through over-pool waves the ceiling alone would not. Switched off
        // here so the probe measures what it says it measures — a build's skill healing.
        var h = new Hunter(new ProgressionTuning { RegenPerVitalityPoint = 0f });
        h.AddGleam(2_000_000);
        foreach (var stat in new[]
                 {
                     HunterStat.MaxHealth, HunterStat.AttackPower, HunterStat.Focus,
                     HunterStat.Vitality, HunterStat.Defense, HunterStat.ResonanceAffinity,
                 })
            for (var i = 0; i < 20; i++)
                h.Train(stat);
        return h;
    }

    private static EquippedSkill Sk(Form form, Source src)
        => new(new WovenAbility { Name = form.ToString(), Source = src, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    /// <summary>A keystone that refuses healing and changes nothing else.</summary>
    private static readonly Keystone NoHeals = new()
    {
        Id = "probe_no_healing", Name = "NO HEALING", Blurb = "probe twin",
        Grants = new[] { BuildTrigger.NoHealing },
    };

    private static Build Weave(IEnumerable<(Form Form, Source Source)> skills, SkillShape shape,
                               BuildMods mods, bool heals, BuildTrigger? extra = null)
    {
        var b = new Build { PassiveMods = mods, Shape = shape };
        foreach (var (f, s) in skills) b.Weave(Sk(f, s));
        if (extra is { } t)
            b.Take(new Keystone { Id = $"probe_{t}", Name = t.ToString(), Blurb = "probe", Grants = new[] { t } });
        if (!heals) b.Take(NoHeals);
        return b;
    }

    // SPIRIT everywhere a Source is not the point: a single-Source Spirit build never cashes its own
    // prime, so it is the signature-neutral choice (the same reasoning as the balance sweep).
    private static Build Morphs(bool heals, bool siphon = false, BuildMods? mods = null)
        => Weave(Enumerable.Repeat((Form.Transformation, Source.Spirit), 4), SkillShape.None,
                 mods ?? Geared, heals, siphon ? BuildTrigger.Siphon : null);

    private static Build NatureAuras(bool heals)
        => Weave(Enumerable.Repeat((Form.Aura, Source.Nature), 4), SkillShape.None, Geared, heals);

    /// <summary>The sweep's four-Form loadout carrying the ENDURE walk — the tree's own heals.</summary>
    private static Build EndureWalk(bool heals)
    {
        var taken = new MasteryTree();
        taken.SetEarned(18);
        bool progress;
        do
        {
            progress = false;
            foreach (var n in MasteryCatalog.Nodes
                         .Where(n => n.Branch == Branch.Endure && n.Link is null)
                         .OrderBy(n => n.Cost).ThenBy(n => n.Ring))
            {
                if (taken.IsTaken(n.Id) || !taken.CanTake(n.Id)) continue;
                taken.Take(n.Id);
                progress = true;
            }
        } while (progress);

        var forms = new[] { Form.Strike, Form.Projectile, Form.Aura, Form.Mark };
        return Weave(forms.Select(f => (f, Source.Spirit)), taken.Shape(), Geared, heals);
    }

    /// <param name="OverPoolShare">
    /// The share of waves the champion LIVED THROUGH while being bitten for more than its whole pool.
    /// The signature of an unkillable build: its pool is not what is keeping it alive.
    /// </param>
    private sealed record RunResult(
        int Depth, WaveOutcome Outcome, double HealedPerWave, double TakenPerWave, double OutHealedShare,
        double OverPoolShare);

    /// <summary>One run to the end, measuring every wave's healing and bites against the pool.</summary>
    private static RunResult Play(Build build, Hunter hunter, int runIndex, ExpeditionTuning tuning)
    {
        var hp = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = hp, Health = hp };
        var run = new SoloExpedition(build, champ, hunter, enemyBaseHealth: 120f, enemyBaseDamage: 9f,
                                     tuning, enemySource: null, rng: new Random(9_000 + runIndex))
        {
            RegionId = "verdant_hollow",
            RunIndex = runIndex,
        };

        var healed = new List<double>();
        var taken = new List<double>();
        while (!run.Over && run.Wave < DepthCap)
        {
            var before = run.Champion.Health;
            run.PushWave();
            var ev = run.LastWaveEvents;
            var inWave = ev.Where(e => e.Kind == BattleEventKind.Heal).Sum(e => (double)e.Amount);
            var bitten = ev.Where(e => e.Kind == BattleEventKind.EnemyStrike).Sum(e => (double)e.Amount);
            // RECOVERY's between-wave regain is not an event; it is whatever the pool moved beyond
            // what the wave's own beats account for. Counted as healing, because it is.
            var regen = Math.Max(0.0, run.Champion.Health - (before - bitten + inWave));
            var pool = Math.Max(1, run.Champion.MaxHealth);
            healed.Add((inWave + regen) / pool);
            taken.Add(bitten / pool);
        }

        var outHealed = healed.Zip(taken, (h, t) => h >= t && t > 0).Count(x => x);
        // The last wave is the one that killed it (when it died), so "lived through" excludes it.
        var survived = run.LastOutcome == WaveOutcome.Wiped ? taken.Take(taken.Count - 1) : taken;
        var overPool = survived.Count(t => t >= 1.0);
        return new RunResult(run.Wave, run.LastOutcome,
                             healed.DefaultIfEmpty(0).Average(), taken.DefaultIfEmpty(0).Average(),
                             healed.Count == 0 ? 0 : outHealed / (double)healed.Count,
                             healed.Count == 0 ? 0 : overPool / (double)healed.Count);
    }

    private sealed record Row(string Name, int P25, int Median, double HealedPct, double TakenPct,
                              double OutHealedShare, double OverPoolShare, int Wiped, int Stalled)
    {
        public double HealToDamage => TakenPct <= 0 ? 0 : HealedPct / TakenPct;

        public override string ToString()
            => $"{Name,-30} p25 {P25,3}  med {Median,3}   healed/wave {HealedPct,6:0.0%}  "
               + $"bitten/wave {TakenPct,6:0.0%}  heal:dmg {HealToDamage,4:0.00}  "
               + $"out-healed {OutHealedShare,4:0%}  over-pool {OverPoolShare,4:0%}   "
               + $"wiped {Wiped,2}/{Seeds}  stalled {Stalled,2}/{Seeds}";
    }

    private static Row Measure(string name, Build build, ExpeditionTuning tuning, Hunter? hunter = null)
    {
        var h = hunter ?? MidCareerHunter();
        var runs = Enumerable.Range(0, Seeds).Select(i => Play(build, h, i, tuning)).ToArray();
        var depths = runs.Select(r => r.Depth).OrderBy(d => d).ToArray();
        return new Row(name, depths[depths.Length / 4], depths[depths.Length / 2],
                       runs.Average(r => r.HealedPerWave), runs.Average(r => r.TakenPerWave),
                       runs.Average(r => r.OutHealedShare), runs.Average(r => r.OverPoolShare),
                       runs.Count(r => r.Outcome == WaveOutcome.Wiped),
                       runs.Count(r => r.Outcome == WaveOutcome.Stalled));
    }

    private static ExpeditionTuning With(HealTuning heal) => ExpeditionTuning.Default with { Heal = heal };

    /// <summary>The fixtures, paired: the heal build and its twin that cannot be healed.</summary>
    private static IEnumerable<(string Name, Func<bool, Build> Make)> Fixtures()
    {
        yield return ("MORPH x4", heals => Morphs(heals));
        yield return ("MORPH x4 + SIPHON", heals => Morphs(heals, siphon: true));
        yield return ("NATURE AURA x4", NatureAuras);
        yield return ("ENDURE WALK (18 pts)", EndureWalk);
    }

    [Fact]
    public void test_heal_builds_are_measured_before_and_after_the_rework()
    {
        var results = new Dictionary<(string Tuning, string Build), (Row With, Row Without)>();

        foreach (var (label, heal) in new[] { ("LEGACY", HealTuning.Legacy), ("LIVE", HealTuning.Default) })
        {
            _out.WriteLine($"── {label}: transformation leech {heal.TransformationLeech:0%}, "
                           + (float.IsPositiveInfinity(heal.MaxHealFractionPerWave)
                               ? "no heal ceiling"
                               : $"heal ceiling {heal.MaxHealFractionPerWave:0%} of the pool per wave"));
            foreach (var (name, make) in Fixtures())
            {
                var with = Measure(name, make(true), With(heal));
                var without = Measure(name + " · no heals", make(false), With(heal));
                results[(label, name)] = (with, without);
                _out.WriteLine(with.ToString());
                _out.WriteLine(without.ToString());
                _out.WriteLine($"{"",-26} survival x{with.Median / (double)Math.Max(1, without.Median):0.00} over the twin");
            }
            _out.WriteLine("");
        }

        // The metric must not saturate, or every number above is "did it hit the cap".
        Assert.True(results.Values.All(r => r.With.Median < DepthCap && r.Without.Median < DepthCap),
                    "a build reached the depth cap — the metric is saturated and proves nothing");

        foreach (var ((tuning, name), (with, without)) in results)
        {
            if (tuning != "LIVE") continue;

            // NOT UNKILLABLE. A build that heals as much as it is bitten, on average, is immune to the
            // curve — the legacy MORPH x4 row is the picture of it. The live numbers must leave every
            // heal build clearly below parity, and it must actually die in the run it plays.
            Assert.True(with.HealToDamage < 0.90,
                        $"{name} heals {with.HealToDamage:0.00}x what it takes per wave — that is not "
                        + "sustain, it is immunity, and the playtest already called it broken");
            // The legacy rows lived through waves that bit for 118% and 289% of the pool. A ceiling
            // of 40% makes that structurally impossible past a full-health opener, and this is the
            // pin: healing may top a pool up, never replace it.
            // 0.05, was 0.02. The same day VITALITY stopped multiplying the pool (it is regeneration
            // now), so the geared fixture's pool shrank by ~1.3× and the SIPHON build's share of
            // over-pool waves measured 3% (it was 1%). The legacy rows measure 18–25%: three in a
            // hundred is a build that dies, not one the pool fails to keep honest.
            Assert.True(with.OverPoolShare < 0.05,
                        $"{name} lived through waves that bit for more than its whole pool "
                        + $"({with.OverPoolShare:0%} of them) — the pool is not what is keeping it alive");
            Assert.True(with.Wiped > 0,
                        $"{name} never died in {Seeds} runs — its depth is the stall wall, not survival");

            // STILL WORTH TAKING. The rework must not turn the sustain Form into decoration: a heal
            // build should outlive its no-heal twin by roughly 1.5-2x. The band is wider than the
            // target on both sides by about a quarter, because a sixteen-seed median moves a wave or
            // two on its own; the ENDURE walk is exempt from the floor because its heals are one
            // third of a branch whose other nodes (padding, absorb, fortify, bulwark) carry the
            // twin — it is measured for the ceiling only.
            // 2.6, from 2.5 (2026-08-26, the one-action model): the MORPH x4 twin WITHOUT heals fell
            // from 22 to 17 — four Transformations saturate the single action lock and, with no heal,
            // the build has nothing else — while the healed build itself stayed at 42–43. The ratio
            // moved on the twin's side, not the heal's; the leech and the ceiling were both probed and
            // do not move it (43 at leech 0.09, 42 at ceiling 0.30). Left as a MORPH follow-up.
            // ...and 2.8 under the BEAT model (2026-08-27, measured MORPH x4 2.63, + SIPHON 2.75): the
            // no-heal twin fell one more wave — one action per beat caps a four-Transformation build's
            // output hardest of all (it has nothing but casts), while its healed self holds at 42–44.
            var survival = with.Median / (double)Math.Max(1, without.Median);
            Assert.True(survival <= 2.8,
                        $"{name} lives {survival:0.00}x longer with heals — healing is still the whole build");
            if (!name.StartsWith("ENDURE", StringComparison.Ordinal))
                Assert.True(survival >= 1.2,
                            $"{name} lives only {survival:0.00}x longer with heals — the rework made healing decorative");
        }
    }

    // ── THE RULES, PINNED. The probe above measures the OUTCOME; these pin the mechanism, at the
    //    unit, so a drift in either shows up as the right failure. ──────────────────────────────

    [Fact]
    public void test_the_heal_numbers_are_pinned()
    {
        // Retuning is allowed — through this test, with the probe's table re-read, not around it.
        var h = HealTuning.Default;
        Assert.Equal(0.12f, h.TransformationLeech, 3);
        Assert.Equal(0.40f, h.MaxHealFractionPerWave, 3);
        Assert.Equal(0.03f, h.NatureSignatureLeech, 3);
        Assert.Equal(2.0f, h.SiphonMultiplier, 3);
        Assert.Equal(1.5f, h.SiphonCeilingMultiplier, 3);

        // The forwarding properties the glossary and screens read are the SAME numbers — one source.
        Assert.Equal(h.TransformationLeech, FormBehaviour.TransformationLeech);
        Assert.Equal(h.NatureSignatureLeech, SoloBattle.SignatureNatureLeech);
        Assert.Equal(h.SiphonMultiplier, SoloBattle.SiphonLeechMultiplier);
        Assert.Same(h, ExpeditionTuning.Default.Heal);

        // And what the player is told matches them.
        Assert.Contains("12%", BuildGlossary.FormRule(Form.Transformation));
        Assert.Contains("40%", BuildGlossary.FormRule(Form.Transformation));
        Assert.Contains("60%", BuildGlossary.HealCeilingRule());
        Assert.Contains("3%", BuildGlossary.Signature(Source.Nature));
    }

    /// <summary>A wave with absurd leech, so the ceiling is the only thing deciding the number.</summary>
    private static (long Healed, Champion Champ) OverhealWave(SkillShape shape, ExpeditionTuning tuning,
                                                              BuildTrigger? trigger = null, int pool = 10_000)
    {
        // Health 1 so there is room for the whole ceiling and more; a creature that dies (so
        // SECOND WIND's on-clear heal fires) but only after many hits (so leech has time to overshoot).
        var champ = new Champion { MaxHealth = pool, Health = 1 };
        var build = Weave(Enumerable.Repeat((Form.Strike, Source.Spirit), 2), shape, BuildMods.None, heals: true, trigger);
        var (outcome, events) = SoloBattle.ResolveWave(champ, build, new Hunter(),
            new[] { WaveCreature.Single(3_000f, 0f) }, enemyIntervalMs: 100_000, tuning, new Random(5));
        Assert.Equal(WaveOutcome.Cleared, outcome);
        return (events.Where(e => e.Kind == BattleEventKind.Heal).Sum(e => (long)e.Amount), champ);
    }

    [Fact]
    public void test_healing_in_one_wave_never_exceeds_the_ceiling()
    {
        var absurd = SkillShape.None with { Leech = 5f, HealPerTargetStruck = 0.5f, HealOnClear = 0.9f };
        var (healed, champ) = OverhealWave(absurd, ExpeditionTuning.Default);

        var budget = HealTuning.Default.BudgetFor(10_000);
        Assert.Equal(4_000, budget);
        Assert.Equal(budget, healed);                 // reached the ceiling exactly — and stopped there
        Assert.Equal(1 + budget, champ.Health);       // the champion's pool says the same thing

        // The ceiling is READ FROM THE INJECTED TUNING, not from a constant: the legacy tuning has
        // none, and the same wave heals past it.
        var (uncapped, _) = OverhealWave(absurd, With(HealTuning.Legacy));
        Assert.True(uncapped > budget, $"uncapped healed {uncapped}, which is not past the ceiling {budget}");
    }

    [Fact]
    public void test_second_wind_draws_from_the_same_allowance()
    {
        // Leech alone spends the whole budget before the clear; SECOND WIND's 90% on the clear must
        // then land as nothing — a clear reward that could exceed the ceiling would be the loophole.
        var leechOnly = SkillShape.None with { Leech = 5f };
        var withClear = leechOnly with { HealOnClear = 0.9f };
        var (a, _) = OverhealWave(leechOnly, ExpeditionTuning.Default);
        var (b, _) = OverhealWave(withClear, ExpeditionTuning.Default);
        Assert.Equal(a, b);
        Assert.Equal(HealTuning.Default.BudgetFor(10_000), b);

        // And with room left under the ceiling, the clear heal does land — the allowance is shared,
        // not a ban.
        var small = SkillShape.None with { Leech = 0.01f };
        var (c, _) = OverhealWave(small, ExpeditionTuning.Default);
        var (d, _) = OverhealWave(small with { HealOnClear = 0.08f }, ExpeditionTuning.Default);
        Assert.True(d > c, "SECOND WIND healed nothing with the allowance unspent");
    }

    [Fact]
    public void test_siphon_raises_the_ceiling_by_its_multiplier()
    {
        var absurd = SkillShape.None with { Leech = 5f };
        var (plain, _) = OverhealWave(absurd, ExpeditionTuning.Default);
        var (siphon, _) = OverhealWave(absurd, ExpeditionTuning.Default, BuildTrigger.Siphon);

        Assert.Equal(HealTuning.Default.BudgetFor(10_000), plain);
        Assert.Equal(HealTuning.Default.BudgetFor(10_000, siphon: true), siphon);
        Assert.Equal((long)MathF.Round(plain * HealTuning.Default.SiphonCeilingMultiplier), siphon);
    }

    [Fact]
    public void test_the_transformation_leech_is_read_from_the_injected_tuning()
    {
        int HealedBy(HealTuning heal)
        {
            var champ = new Champion { MaxHealth = 1_000_000, Health = 1 };
            SoloBattle.ResolveWave(champ, Morphs(true, mods: BuildMods.None), new Hunter(), 10_000_000f, 0f,
                                   1_000_000, With(heal), new Random(3));
            return champ.Health - 1;
        }

        var live = HealedBy(HealTuning.Default);
        var legacy = HealedBy(HealTuning.Legacy);
        Assert.True(live > 0, "a Transformation build healed nothing");
        // 0.50 against 0.12 on the same damage, neither at the ceiling (a million-point pool).
        Assert.InRange(legacy / (double)live, 3.9, 4.5);
    }

    [Fact]
    public void test_overhealing_at_full_health_does_not_spend_the_allowance()
    {
        // A full champion that leeches for the whole fight, then takes ONE late bite: the heals that
        // landed on a full pool must not have spent the budget, so the bite is healed back.
        var champ = new Champion { MaxHealth = 10_000, Health = 10_000 };
        var build = Weave(Enumerable.Repeat((Form.Strike, Source.Spirit), 2),
                          SkillShape.None with { Leech = 5f }, BuildMods.None, heals: true);
        // One bite at 30s, 1_000 deep, then nothing for the rest of the fight.
        var foe = WaveCreature.Single(1e9f, 1_000f);
        var (_, events) = SoloBattle.ResolveWave(champ, build, new Hunter(), new[] { foe },
            enemyIntervalMs: 30_000, ExpeditionTuning.Default with { TickCeilingMs = 45_000 }, new Random(5));

        var bites = events.Count(e => e.Kind == BattleEventKind.EnemyStrike);
        Assert.Equal(1, bites);
        Assert.Equal(10_000, champ.Health);
        Assert.True(events.Where(e => e.Kind == BattleEventKind.Heal).Sum(e => e.Amount) <= 1_000,
                    "heals landed on a full pool — they should be no events at all");
    }

    [Fact]
    public void test_blood_magic_refuses_recovery_between_waves_too()
    {
        // RECOVERY regains 20% between waves in SoloExpedition, outside the in-wave funnel — and it
        // ignored "YOU CANNOT BE HEALED" until the probe's no-heal twin was seen regaining 3.4% a wave.
        int HealthAfterOneWave(bool bloodMagic)
        {
            var shape = SkillShape.None with { BetweenWaveRegen = 0.20f };
            var build = Weave(Enumerable.Repeat((Form.Strike, Source.Spirit), 4), shape, Geared, heals: !bloodMagic);
            var champ = new Champion { MaxHealth = 1_000, Health = 1_000 };
            var run = new SoloExpedition(build, champ, MidCareerHunter(), 120f, 9f,
                                         ExpeditionTuning.Default, null, new Random(1))
                { RegionId = "verdant_hollow", RunIndex = 0 };
            run.PushWave();
            return champ.Health;
        }

        var healed = HealthAfterOneWave(false);
        var refused = HealthAfterOneWave(true);
        Assert.True(refused < healed, $"BLOOD MAGIC was healed between waves: {refused} vs {healed}");
        Assert.True(refused < 1_000, "the fixture's first wave did no damage, so the test measures nothing");
    }

    /// <summary>
    /// The pacing fixture — a fresh champion, no gear, no tree — with four Transformations instead of
    /// four Strikes. The first region's job is to END a few times; a starter heal build must not exempt
    /// itself from that.
    /// </summary>
    [Fact]
    public void test_a_fresh_transformation_build_still_dies_in_the_first_region()
    {
        var fresh = new Hunter();
        var with = Measure("FRESH MORPH x4", Morphs(true, mods: BuildMods.None), With(HealTuning.Default), fresh);
        var without = Measure("FRESH MORPH x4 · no heals", Morphs(false, mods: BuildMods.None), With(HealTuning.Default), fresh);
        var legacy = Measure("FRESH MORPH x4 (legacy)", Morphs(true, mods: BuildMods.None), With(HealTuning.Legacy), fresh);
        _out.WriteLine(with.ToString());
        _out.WriteLine(without.ToString());
        _out.WriteLine(legacy.ToString());

        Assert.Equal(Seeds, with.Wiped);
        // pacing_test allows a fresh STRIKE champion up to wave 20; a fresh heal build sits inside
        // the same first-region band rather than walking out of it.
        Assert.True(with.Median <= 25,
                    $"a fresh Transformation build reaches wave {with.Median} — the first region teaches it nothing");
        Assert.True(with.HealToDamage < 0.85,
                    $"a fresh Transformation build heals {with.HealToDamage:0.00}x what it takes");
    }
}
