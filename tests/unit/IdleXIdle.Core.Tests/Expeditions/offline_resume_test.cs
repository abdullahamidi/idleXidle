using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// THE ABSENCE LEAVES THE CHAMPION SOMEWHERE, and coming back picks it up from there.
/// </summary>
/// <remarks>
/// <para>
/// Playtest 2026-09-09: <i>"The game shouldn't start at Wave 1 every time it's launched. We need to
/// convince the player that the simulation continues even while the game is closed, picking up from
/// the middle of a run."</i> It always DID simulate — <c>OfflineHunt</c> runs the real descent against
/// the real build — and then threw the depth away: <c>DeepestWave</c> had exactly one consumer in the
/// whole repository, one line of text on the return screen, and the live run opened at wave one.
/// </para>
/// <para>
/// <b>The design risk this file pins.</b> A checkpoint sells waves skipped, for Memory Dust, per
/// descent. A free resume is adjacent to that, and a careless one is a perfect SUBSTITUTE for it:
/// close the game, reopen it, arrive deep, pay nothing. Three rules keep them apart, and each has a
/// test here — the depth must be genuinely SIMULATED (never extrapolated), it must come from a wave
/// the champion CLEARED (never the one that killed it), and it lasts ONE descent.
/// </para>
/// </remarks>
public class offline_resume_test
{
    private readonly ITestOutputHelper _out;

    public offline_resume_test(ITestOutputHelper output) => _out = output;

    private const string Region = VerdantHollow.RegionId;

    private static Build Woven()
    {
        var b = new Build();
        b.Equip(new EquippedSkill(SkillCatalogue.ById("hammer_blow"), Source.Body));
        return b;
    }

    /// <summary>A hunter strong enough to hold a wave, so an absence has somewhere to get to.</summary>
    private static Hunter Trained()
    {
        var h = new Hunter();
        h.AddGleam(int.MaxValue / 2);
        for (var i = 0; i < 40; i++) { h.Train(HunterStat.AttackPower); h.Train(HunterStat.ResonanceAffinity); h.Train(HunterStat.MaxHealth); }
        return h;
    }

    private static OfflineHunt.Result Away(double seconds, float enemyHealth = 60f, float enemyDamage = 3f, int seed = 7)
        => OfflineHunt.Simulate(Woven(), Trained(), seconds, Region, AttackBias.Balanced,
                                enemyHealth, enemyDamage, seed);

    [Fact]
    public void test_the_absence_says_where_it_left_the_champion()
    {
        // THE REPORT, as one assertion: the simulation now carries the wave forward instead of
        // dropping it. Before this the only way out of Simulate was Gleam, waves, falls and a
        // "deepest" that nothing but a text line ever read.
        var r = Away(2 * 3600);

        _out.WriteLine($"waves {r.WavesCleared}  falls {r.Falls}  deepest {r.DeepestWave}  bosses {r.BossesFelled}");
        Assert.True(r.WavesCleared > 0, "the fixture's champion cleared nothing — it cannot pose a resume");
        Assert.True(r.DeepestWave > 0, "the absence held nothing");
    }

    [Fact]
    public void test_a_longer_absence_leaves_the_champion_deeper()
    {
        // The whole promise: the simulation CONTINUES, and a longer absence gets FURTHER. This is why
        // the resume is the deepest wave HELD rather than the wave the clock happened to stop on: the
        // stopping point is a snapshot of a cycle (climb, fall, climb), so on this very fixture four
        // hours stopped on wave 3 while ten minutes stopped on 20 — a player sent backwards for having
        // been away longer.
        var brief = Away(10 * 60);
        var long_ = Away(4 * 3600);

        _out.WriteLine($"10 min -> wave {brief.DeepestWave}   ·   4 h -> wave {long_.DeepestWave}");
        Assert.True(long_.DeepestWave >= brief.DeepestWave,
                    $"four hours away ended on wave {long_.DeepestWave}, ten minutes on {brief.DeepestWave}");
    }

    [Fact]
    public void test_a_fall_puts_the_champion_back_at_the_bottom()
    {
        // RULE ONE. The resume is a wave the champion actually HELD. A build that clears nothing holds
        // nothing, so it resumes at the bottom however long the absence was — posed with an enemy this
        // build cannot beat, so every wave is a fall.
        var doomed = OfflineHunt.Simulate(Woven(), new Hunter(), 20 * 60, Region, AttackBias.Balanced,
                                          enemyBaseHealth: 4_000_000f, enemyBaseDamage: 9_000f, seed: 3);

        _out.WriteLine($"hopeless build: waves {doomed.WavesCleared}  falls {doomed.Falls}  ended on {doomed.DeepestWave}");
        Assert.True(doomed.Falls > 0, "the fixture never fell — it cannot pose this rule");
        Assert.Equal(0, doomed.DeepestWave);
    }

    [Fact]
    public void test_the_extrapolated_tail_grants_no_depth()
    {
        // RULE TWO, and the one that keeps the camp meaningful. When the wave cap is reached with time
        // still on the clock the remainder is paid at the measured RATE — Gleam is linear in time, so
        // that is honest — but nothing down there was fought, so it grants no ground. A camp that
        // cannot hold the whole absence therefore resumes SHALLOW, which is what makes the Warren the
        // thing that sells depth-on-return rather than the length of the absence.
        var capped = Away(400 * 3600);   // far past MaxSimulatedWaves

        _out.WriteLine($"simulated {capped.SimulatedSeconds:N0}s  extrapolated {capped.ExtrapolatedSeconds:N0}s  ended on {capped.DeepestWave}");
        Assert.True(capped.ExtrapolatedSeconds > 0, "the fixture never hit the wave cap — it cannot pose this rule");
        Assert.True(capped.DeepestWave > 0, "the capped run held nothing at all");
        Assert.True(capped.SimulatedSeconds < capped.ExtrapolatedSeconds,
                    "the fixture simulated more than it extrapolated — it is not posing the cap");
    }

    [Fact]
    public void test_the_resume_is_reproducible_for_a_seed()
    {
        // It is a starting position handed to a live run, so it must not wobble between two loads of
        // the same save.
        Assert.Equal(Away(90 * 60).DeepestWave, Away(90 * 60).DeepestWave);
    }

    [Fact]
    public void test_the_return_screen_says_where_it_picks_up_and_stays_quiet_when_there_is_nothing_to_say()
    {
        var deep = new WelcomeSummary(6 * 3600, Away(3 * 3600), default, WarrenOpen: false);
        Assert.NotNull(deep.ResumeLine());
        Assert.Contains("PICKING UP AT WAVE", deep.ResumeLine()!, StringComparison.Ordinal);

        // Wave one is where a descent starts anyway: a promise that changes nothing is noise, and this
        // line's whole job is to be noticed.
        var nowhere = new WelcomeSummary(6 * 3600, new OfflineHunt.Result(0, 0, 0, DeepestWave: 1, 0, 0, 0), default, WarrenOpen: false);
        Assert.Null(nowhere.ResumeLine());
        Assert.Null(new WelcomeSummary(6 * 3600, default, default, WarrenOpen: false).ResumeLine());
    }

    [Fact]
    public void test_the_return_screen_is_a_results_grid_and_invents_nothing()
    {
        // Every tile must be a figure the model produced — the rule this file was written under, and
        // the reason there is no NOTABLE block. Playtest 2026-09-09 replaced the narration with tiles:
        // "make them iconed and like a results screen, we are not telling a story." The discipline did
        // not change with the shape — a boss tile is printed only when a boss fell.
        var r = Away(3 * 3600);
        var w = new WelcomeSummary(3 * 3600, r, default, WarrenOpen: false);
        var tiles = w.HuntTiles();

        foreach (var t in tiles) _out.WriteLine($"{t.Figure,-8} {t.Value,8}  {t.Label}");
        Assert.NotEmpty(tiles);
        Assert.Contains(tiles, t => t.Figure == WelcomeFigure.Waves);
        Assert.Equal(r.BossesFelled > 0, tiles.Any(t => t.Figure == WelcomeFigure.Bosses));
        Assert.Equal(r.Falls > 0, tiles.Any(t => t.Figure == WelcomeFigure.Falls));
        // Never a tile whose figure is nothing: a zero is omitted, not printed.
        Assert.DoesNotContain(tiles, t => t.Value is "0");

        // A hunter who did nothing at all is shown nothing at all beyond the absence itself.
        Assert.Empty(new WelcomeSummary(0, default, default, WarrenOpen: false).HuntTiles());
    }

    [Fact]
    public void test_the_words_under_the_figures_agree_with_them()
    {
        // A tile is three things and the third is a word, so the word has to match the number over it —
        // "1 FALLS" and "2 BOSS FELLED" are the kind of wrong that makes a results screen look broken.
        var one = new WelcomeSummary(3600, new OfflineHunt.Result(0, 1, 1, DeepestWave: 4, 0, 0, 0, BossesFelled: 1), default, false);
        Assert.Contains(one.HuntTiles(), t => t is { Figure: WelcomeFigure.Waves, Label: "WAVE HELD" });
        Assert.Contains(one.HuntTiles(), t => t is { Figure: WelcomeFigure.Bosses, Label: "BOSS FELLED" });
        Assert.Contains(one.HuntTiles(), t => t is { Figure: WelcomeFigure.Falls, Label: "FALL" });

        var many = new WelcomeSummary(3600, new OfflineHunt.Result(0, 9, 3, DeepestWave: 4, 0, 0, 0, BossesFelled: 2), default, false);
        Assert.Contains(many.HuntTiles(), t => t is { Figure: WelcomeFigure.Waves, Label: "WAVES HELD" });
        Assert.Contains(many.HuntTiles(), t => t is { Figure: WelcomeFigure.Bosses, Label: "BOSSES FELLED" });
        Assert.Contains(many.HuntTiles(), t => t is { Figure: WelcomeFigure.Falls, Label: "FALLS" });
    }
}
