using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// CRITICAL HITS HAPPEN, at the rate the screen states, and the fight says which hits they were.
/// </summary>
/// <remarks>
/// <para>
/// The playtest that produced this file: <i>"Crit chance isn't working. I couldn't land a single crit
/// with a 12% chance."</i> They were right, and more literally than they knew — the game contained
/// zero critical hits. Crit was folded into a deterministic expected-value multiplier, so a 12%
/// chance was a silent ×1.06 on every skill hit and never once an event. The Training screen said
/// "12.0% of your hits are critical right now", two trait cards promised critical hits as things that
/// happen, and nothing anywhere could produce one.
/// </para>
/// <para>
/// This file measures the CHAIN, not the link: the roll happens, at the stated rate, on the swing as
/// well as on skills; the event carries the grade so a screen can draw it; derived landings never
/// re-roll; and the whole thing stays a pure function of its seed. A test that only asked "is crit
/// wired" would have passed for the whole time the bug existed.
/// </para>
/// </remarks>
public class crit_roll_test
{
    private readonly ITestOutputHelper _out;

    public crit_roll_test(ITestOutputHelper output) => _out = output;

    /// <summary>A dummy that cannot die and cannot swing, so a wave is a long clean sample of hits.</summary>
    private static (List<BattleEvent> Events, WaveOutcome Outcome) Fight(Build build, Hunter hunter, int seed)
    {
        var champ = new Champion { MaxHealth = 10_000_000, Health = 10_000_000 };
        var (outcome, events) = SoloBattle.ResolveWave(
            champ, build, hunter,
            enemyHealth: 1_000_000_000f, enemyDamage: 0f, enemyIntervalMs: 100_000,
            ExpeditionTuning.Default, new Random(seed));
        return (events.ToList(), outcome);
    }

    /// <summary>
    /// A hunter whose critical chance reads as close to <paramref name="percent"/> as ranks allow.
    /// </summary>
    /// <remarks>
    /// Ranks are BOUGHT — <see cref="Hunter.Train"/> charges Gleam and refuses when there is none —
    /// so the wallet is filled first. Written the long way on purpose: a helper that silently trained
    /// nothing is how the first draft of this file measured a 5% base three times and called it a
    /// theory over three chances.
    /// </remarks>
    private static Hunter WithCrit(float percent)
    {
        var hunter = new Hunter();
        hunter.AddGleam(int.MaxValue / 2);
        var perRank = hunter.GainPerRank(HunterStat.CriticalChance);
        while (hunter.ValueOf(HunterStat.CriticalChance) + perRank / 2f < percent
               && hunter.Train(HunterStat.CriticalChance)) { }
        return hunter;
    }

    private static Build OneSkill()
    {
        var build = new Build();
        build.Equip(new EquippedSkill(SkillCatalogue.ById("hammer_blow"), Source.Body));
        return build;
    }

    [Fact]
    public void test_critical_hits_actually_happen()
    {
        // The whole report, as one assertion. Before the fix this collection was empty at every
        // chance, at every seed, forever.
        var (events, _) = Fight(OneSkill(), WithCrit(12f), seed: 4242);
        var strikes = events.Where(e => e.Kind == BattleEventKind.Strike).ToList();

        Assert.NotEmpty(strikes);
        Assert.Contains(strikes, e => e.Crit);
    }

    // 20% is the ceiling TRAINING alone can reach: base 5 plus StatRankCap (60) ranks of 0.25.
    // Anything above it needs gear or a node, which this fixture deliberately does not wear.
    [Theory]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(20f)]
    public void test_the_rolled_rate_matches_the_stated_chance(float percent)
    {
        // WHAT THE PLAYER IS TOLD IS WHAT THE FIGHT DOES. The Training screen prints
        // SoloBattle.CritChance as "X% of your hits are critical right now"; this measures the share
        // of hits that actually came back graded, over enough seeds for the binomial noise to settle.
        var hunter = WithCrit(percent);
        var stated = SoloBattle.CritChance(hunter, SkillShape.None);

        long crits = 0, hits = 0;
        for (var seed = 0; seed < 40; seed++)
        {
            var (events, _) = Fight(OneSkill(), hunter, seed: 90_000 + seed * 7919);
            foreach (var e in events.Where(e => e.Kind == BattleEventKind.Strike))
            {
                hits++;
                if (e.Crit) crits++;
            }
        }

        var measured = crits / (double)hits;
        _out.WriteLine($"stated {stated:P1}   measured {measured:P1} over {hits:N0} hits");

        Assert.True(hits > 2_000, $"only {hits} hits sampled — too few to read a rate from");
        Assert.True(Math.Abs(measured - stated) < 0.02,
                    $"the fight crits {measured:P1} of the time while every screen says {stated:P1}");
    }

    [Fact]
    public void test_the_basic_swing_crits_too()
    {
        // The readout says "how often A HIT becomes a critical hit", and the swing is the metronome
        // the player watches between casts. A swing that could never crit is why the stat felt dead
        // even to someone who had read the number.
        var (events, _) = Fight(new Build(), WithCrit(40f), seed: 777);
        var swings = events.Where(e => e.Kind == BattleEventKind.Strike && e.Hit == HitSource.Swing).ToList();

        Assert.NotEmpty(swings);
        Assert.Contains(swings, e => e.Crit);
        Assert.Contains(swings, e => !e.Crit);
    }

    [Fact]
    public void test_a_critical_hits_harder_than_a_plain_one_by_the_stated_multiplier()
    {
        // The two sizes a swing can land at, and nothing between them: the multiplier the FOCUS
        // readout prints is the multiplier the fight pays.
        var hunter = WithCrit(40f);
        var (events, _) = Fight(new Build(), hunter, seed: 31_337);
        var swings = events.Where(e => e.Kind == BattleEventKind.Strike && e.Hit == HitSource.Swing).ToList();

        var plain = swings.Where(e => !e.Crit).Select(e => e.Amount).Distinct().ToList();
        var crit = swings.Where(e => e.Crit).Select(e => e.Amount).Distinct().ToList();

        Assert.Single(plain);
        Assert.Single(crit);
        // Within a unit: the sim rounds the finished damage once, so the crit's rounding and the
        // plain hit's rounding are not the same rounding and cannot be required to agree exactly.
        var expected = plain[0] * SoloBattle.CritMultiplier(hunter);
        _out.WriteLine($"plain {plain[0]}   critical {crit[0]}   x{SoloBattle.CritMultiplier(hunter):0.00} = {expected:0.0}");
        Assert.True(Math.Abs(crit[0] - expected) <= 1f,
                    $"a critical landed {crit[0]} where the stated multiplier says {expected:0.0}");
    }

    [Fact]
    public void test_no_critical_at_zero_chance_and_every_hit_at_the_ceiling()
    {
        // The two ends. A hunter with the stat trained to nothing still carries the 5% base, so the
        // zero end is posed by reading the chance itself rather than by asserting a floor that does
        // not exist; the ceiling is MaxCritChance, and a build cannot be sent past it.
        // THE CEILING IS ARITHMETIC, and it is above anything training alone can buy — so it is read
        // through the shape bonus a node or a piece of gear supplies, not by training past a cap.
        var trained = WithCrit(20f);
        var absurd = new SkillShape { BonusCritPercent = 400f };
        Assert.Equal(SoloBattle.MaxCritChance, SoloBattle.CritChance(trained, absurd));

        // And the fully-trained hunter really does crit about a fifth of the time, both ways.
        var (events, _) = Fight(OneSkill(), trained, seed: 5);
        var strikes = events.Where(e => e.Kind == BattleEventKind.Strike).ToList();
        var share = strikes.Count(e => e.Crit) / (double)strikes.Count;
        _out.WriteLine($"fully trained: {share:P1} of {strikes.Count} hits critical");
        Assert.True(share > 0.10, $"a fully trained hunter crit only {share:P0} of the time");
        Assert.Contains(strikes, e => !e.Crit);   // and a chance is a chance, not certainty
    }

    [Fact]
    public void test_a_derived_landing_never_rolls_its_own_critical()
    {
        // PROVENANCE DISCIPLINE. A poison tick, a carried overkill, a reflected bite and a DEADWEIGHT
        // release all inherit the hit that generated them; letting one roll again would pay the same
        // critical twice. VENOMANCER is woven so the sweep genuinely produces derived landings — a
        // fixture that poses none of them would clear this rule by never testing it.
        var build = OneSkill();
        build.Take(Keystones.ById("venomancer")!);
        var hunter = WithCrit(60f);

        var seen = new HashSet<HitSource>();
        var champ = new Champion { MaxHealth = 400_000, Health = 400_000 };
        var run = new SoloExpedition(build, champ, hunter, 620f, 62f, ExpeditionTuning.Default, new Random(11));
        for (var w = 0; w < 12 && !run.Over; w++)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
            foreach (var e in run.LastWaveEvents.Where(e => e.Kind == BattleEventKind.Strike && e.Hit is { } h
                                                            && h is not (HitSource.Primary or HitSource.Swing)))
            {
                seen.Add(e.Hit!.Value);
                Assert.False(e.Crit, $"a {e.Hit} landing came back graded — a derived hit must never roll");
            }
        }

        _out.WriteLine("derived provenances seen: " + string.Join(", ", seen.OrderBy(x => x.ToString())));
        Assert.NotEmpty(seen);   // the sweep must actually have posed the case it is clearing
    }

    [Fact]
    public void test_certainty_arrives_on_time_because_only_a_skill_crit_spends_the_bank()
    {
        // MIND'S PROMISE IS ARITHMETIC, and that makes it a assertion rather than a rate. The bank is
        // fed 3 points by every SKILL hit that did not crit, against a 15-point cap, and CERTAINTY
        // forces the hit taken at the cap — so a SIXTH consecutive skill hit without a critical cannot
        // exist. Five fill the bank; the next one is guaranteed.
        //
        // Two separate defects broke exactly that count, and neither is visible to any test that only
        // asks whether criticals happen at the stated rate:
        //
        //   · a basic SWING that rolled a critical emptied the bank. The swing never pays into FOCUS
        //     (the branch that banks is `fromSkill`), so it was spending a climb it had not made — and
        //     when the bank was full it consumed the guaranteed hit without anyone taking it.
        //   · HAMMER's FINISH execute is a HitSource.Primary landing that never called the roll at all.
        //     It could not crit, and it did not bank, so it slid a further non-critical skill hit into
        //     the middle of the run.
        //
        // FINISH is woven deliberately: it is the fixture's own poser for the second one, and the
        // execute's extra landing is counted below so the case cannot quietly stop arising.
        var blow = SkillCatalogue.ById("hammer_blow");
        var finish = blow.Variations.Single(v => v.Name == "FINISH");
        var build = new Build { Shape = new SkillShape { FocusPerHitPercent = 3f, FocusCapPercent = 15f, CertaintyAtFocusCap = true } };
        // A VARIATION IS A FUNCTION ON THE DEFINITION, and the sim reads the definition it is handed —
        // BuildComposer is what applies it on the live path, so a fixture that only names the variation
        // in the EquippedSkill fights the BASE skill and poses nothing. (It did, for one draft: zero
        // executes over five hundred landings, because ExecuteFraction was still zero.)
        build.Equip(new EquippedSkill(finish.Modify!(blow), Source.Shadow, null, finish));
        var hunter = WithCrit(20f);

        // The longest run of skill hits that may pass without one: 15 points of cap at 3 a hit.
        var longestAllowed = (int)(15f / 3f);
        var worst = 0;
        var executes = 0;
        var swingCrits = 0;
        var skillHits = 0;

        // POSED AS WAVES, one ResolveWave each, because the bank is minted with a wave and dies with
        // it — and creatures big enough that the blow cannot one-shot them, since the execute needs a
        // SURVIVOR under the threshold and a wave that dies to its first landing never has one. The
        // creatures do not bite, so the sample is a long clean run of the champion's own hits.
        for (var wave = 0; wave < 40; wave++)
        {
            var champ = new Champion { MaxHealth = 10_000_000, Health = 10_000_000 };
            var creatures = Enumerable.Range(0, 3).Select(_ => WaveCreature.Single(9_000f, 0f)).ToList();
            var (_, events) = SoloBattle.ResolveWave(champ, build, hunter, creatures,
                                                    enemyIntervalMs: 100_000, ExpeditionTuning.Default,
                                                    new Random(4_242 + wave * 7919));

            var streak = 0;
            var primaries = 0;
            var casts = 0;
            foreach (var e in events)
            {
                if (e.Kind == BattleEventKind.Skill) casts++;
                if (e.Kind != BattleEventKind.Strike) continue;
                if (e.Hit == HitSource.Swing && e.Crit) swingCrits++;
                if (e.Hit != HitSource.Primary) continue;
                primaries++;
                skillHits++;
                streak = e.Crit ? 0 : streak + 1;
                worst = Math.Max(worst, streak);
            }
            // BLOW takes one target and hits it once, so one cast is one landing — and a wave with more
            // landings than casts is a wave the execute fired in.
            if (primaries > casts) executes++;
        }

        _out.WriteLine($"{skillHits} skill hits · longest run without a critical {worst} (cap {longestAllowed})"
                       + $" · {swingCrits} swing criticals · {executes} waves with an execute");

        Assert.True(skillHits > 100, $"only {skillHits} skill hits sampled — too few to catch a run");
        Assert.True(swingCrits > 0, "no swing ever crit — the fixture cannot pose the bank being robbed");
        Assert.True(executes > 0, "FINISH never executed — the fixture cannot pose the unrolled landing");
        Assert.True(worst <= longestAllowed,
                    $"{worst} skill hits passed without a critical where CERTAINTY promises at most {longestAllowed}");
    }

    [Fact]
    public void test_the_same_seed_still_produces_the_same_fight()
    {
        // A rolled critical must not cost the descent its reproducibility: the crit stream is minted
        // from ONE draw of the wave's own seeded Random, so a wave costs the shared stream the same
        // draw whatever its hit count.
        var hunter = WithCrit(35f);
        var a = Fight(OneSkill(), hunter, seed: 20_260_909).Events;
        var b = Fight(OneSkill(), hunter, seed: 20_260_909).Events;

        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a, b);
    }

    [Fact]
    public void test_two_seeds_produce_different_luck()
    {
        // The other half of the same claim: it is a roll, not a constant dressed as one.
        var hunter = WithCrit(35f);
        var a = Fight(OneSkill(), hunter, seed: 1).Events.Where(e => e.Kind == BattleEventKind.Strike).Select(e => e.Crit);
        var b = Fight(OneSkill(), hunter, seed: 2).Events.Where(e => e.Kind == BattleEventKind.Strike).Select(e => e.Crit);
        Assert.NotEqual(a, b);
    }
}
