using System;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Expeditions;

/// <summary>
/// The report is the only place a player can learn anything, so these tests are about whether it tells
/// the truth about WHY a run ended — not about whether it renders.
/// </summary>
public class RunReportTests
{
    private static EquippedSkill Sk(Form form)
        => new(new WovenAbility { Name = form.ToString(), Source = Source.Nature, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    private static Build BuildOf(params Form[] forms)
    {
        var b = new Build();
        foreach (var f in forms) b.Weave(Sk(f));
        return b;
    }

    private static RunReport RunIn(string region, Build build, int hp = 600, int cap = 90)
    {
        var run = new SoloExpedition(build, new Champion { MaxHealth = hp, Health = hp }, new Hunter(),
            120f, 9f, ExpeditionTuning.Default, enemySource: null, rng: new Random(7))
        { RegionId = region, RunIndex = 1 };

        while (!run.Over && run.Wave < cap) run.PushWave();
        return run.Report(isRecord: true);
    }

    /// <summary>
    /// A small-hit build dying in an armoured region must be TOLD that armour is eating its damage.
    /// </summary>
    /// <remarks>
    /// The single most important property of the report. With no in-run decisions, a player who cannot
    /// tell "my build is the wrong shape" from "my numbers are too small" is playing a slot machine —
    /// the failure state the design names explicitly.
    /// </remarks>
    [Fact]
    public void test_a_small_hit_build_is_told_that_armour_is_the_problem()
    {
        var spread = RunIn("cinderworks", BuildOf(Form.Aura, Form.Projectile));
        var weight = RunIn("cinderworks", BuildOf(Form.Trap, Form.Strike));

        Assert.True(
            spread.AbsorbedFraction > weight.AbsorbedFraction * 1.4f,
            $"Armour ate {spread.AbsorbedFraction:P0} of the small-hit build and " +
            $"{weight.AbsorbedFraction:P0} of the large-hit one. The report cannot distinguish shape " +
            "from size, which is the one thing it exists to do.");
    }

    /// <summary>A single-target build in a swarm region must see its reach reported as poor.</summary>
    [Fact]
    public void test_a_single_target_build_is_told_that_reach_is_the_problem()
    {
        var single = RunIn("umbral_reach", BuildOf(Form.Trap, Form.Strike));
        var multi = RunIn("umbral_reach", BuildOf(Form.Aura, Form.Projectile));

        Assert.True(
            multi.TargetsPerActivation > single.TargetsPerActivation,
            $"Reach was {single.TargetsPerActivation:F2} for the single-target build and " +
            $"{multi.TargetsPerActivation:F2} for the multi-target one — the report is not measuring " +
            "action economy.");
    }

    /// <summary>Every report names a wall the player can go and look at.</summary>
    [Fact]
    public void test_the_report_names_the_wave_that_ended_the_run()
    {
        var r = RunIn("marrow_wastes", BuildOf(Form.Strike));

        Assert.True(r.WallWave > 0);
        Assert.True(r.WallCreatures > 0);
        Assert.False(string.IsNullOrWhiteSpace(r.Verdict()));
    }

    /// <summary>
    /// The verdict names a demand, never a fix.
    /// </summary>
    /// <remarks>
    /// A guard on the design line for this screen. If a verdict ever starts telling the player which
    /// node to take, the report has stopped diagnosing and started playing.
    /// </remarks>
    [Fact]
    public void test_the_verdict_never_prescribes_a_fix()
    {
        var banned = new[] { "take ", "buy ", "equip ", "spend ", "you should", "try " };

        foreach (var region in new[] { "cinderworks", "umbral_reach", "marrow_wastes", "still_archive" })
            foreach (var build in new[]
                     {
                         BuildOf(Form.Trap), BuildOf(Form.Aura),
                         BuildOf(Form.Strike, Form.Projectile), BuildOf(Form.Transformation),
                     })
            {
                var verdict = RunIn(region, build).Verdict().ToLowerInvariant();
                foreach (var phrase in banned)
                    Assert.False(verdict.Contains(phrase),
                        $"Verdict prescribes rather than diagnoses: {verdict}");
            }
    }

    /// <summary>Measurements come from the last band, not from the whole descent.</summary>
    /// <remarks>
    /// A build that walked forty waves and died in the forty-first is not described by an average over
    /// all forty-one — the wall would be buried under the waves it handled comfortably.
    /// </remarks>
    [Fact]
    public void test_measurements_are_scoped_to_the_last_band()
    {
        var r = RunIn("verdant_hollow", BuildOf(Form.Strike, Form.Projectile), hp: 4000);

        Assert.True(r.SampledWaves > 0);
        Assert.True(r.SampledWaves <= Bands.WavesPerBand,
            $"Sampled {r.SampledWaves} waves — the report must describe the band the run died in, " +
            "not the whole descent.");
    }

    /// <summary>The diff is what makes iteration legible.</summary>
    [Fact]
    public void test_the_diff_reports_what_changed_between_runs()
    {
        var before = RunIn("cinderworks", BuildOf(Form.Aura, Form.Projectile));
        var after = RunIn("cinderworks", BuildOf(Form.Trap, Form.Strike));

        var lines = after.DiffAgainst(before).ToList();

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Contains("HIT SIZE"));
    }

    /// <summary>A diff across different regions is meaningless and must not be offered.</summary>
    [Fact]
    public void test_no_diff_across_regions()
    {
        var a = RunIn("cinderworks", BuildOf(Form.Strike));
        var b = RunIn("umbral_reach", BuildOf(Form.Strike));

        Assert.Empty(b.DiffAgainst(a));
    }
}
