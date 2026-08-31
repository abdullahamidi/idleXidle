using System.Linq;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// The report has to outlive the four seconds it used to be shown for.
/// </summary>
/// <remarks>
/// Combat is automatic and a run has no decisions in it, so the report is the ONLY place this game can
/// teach — and it was displayed in an overlay that cleared itself when the next descent began. That is a
/// contradiction with the genre: an idle game is played by somebody who is not looking at the screen, so
/// a lesson delivered only to a player watching at the exact second their champion fell is a lesson
/// delivered to nobody.
/// </remarks>
public class RunLogTests
{
    private static RunReport Report(string region, int depth) => new()
    {
        RegionId = region,
        Depth = depth,
        IsRecord = false,
        Outcome = WaveOutcome.Wiped,
        WallWave = depth + 1,
        WallArchetype = Archetype.Bruiser,
        WallAffixes = new[] { Affix.Plated },
        WallCreatures = 1,
        AbsorbedFraction = 0.4f,
        AverageHitSize = 120f,
        TargetsPerActivation = 1.1f,
        CreaturesPerWave = 2.2f,
        HealthLostPerWaveFraction = 0.25f,
        SecondsPerWave = 1.6f,
        SampledWaves = 5,
    };

    [Fact]
    public void test_the_log_keeps_the_newest_runs_first()
    {
        var log = new RunLog();
        log.Add(Report("a", 1));
        log.Add(Report("a", 2));

        Assert.Equal(2, log.Newest!.Depth);
        Assert.Equal(2, log.Count);
    }

    /// <summary>Old entries fall off rather than the save growing without bound.</summary>
    [Fact]
    public void test_the_log_is_capped()
    {
        var log = new RunLog();
        for (var i = 0; i < RunLog.Capacity + 5; i++) log.Add(Report("a", i));

        Assert.Equal(RunLog.Capacity, log.Count);
        Assert.Equal(RunLog.Capacity + 4, log.Newest!.Depth);   // the oldest were dropped, not the newest
    }

    /// <summary>
    /// A diff is only meaningful against the same REGION.
    /// </summary>
    /// <remarks>
    /// Not simply "the run before". A player who pushed one region, wandered into another and came back
    /// would otherwise be shown a difference between two sets of CONTENT and told it was a change in
    /// their build — which is the exact opposite of what the report exists to tell them.
    /// </remarks>
    [Fact]
    public void test_the_previous_run_is_looked_up_per_region()
    {
        var log = new RunLog();
        log.Add(Report("cinderworks", 10));
        log.Add(Report("umbral_reach", 30));
        log.Add(Report("cinderworks", 20));

        // PreviousIn is "the newest in this region" — what a run that has just ended compares against.
        Assert.Equal(20, log.PreviousIn("cinderworks")!.Depth);
        Assert.Equal(30, log.PreviousIn("umbral_reach")!.Depth);
        Assert.Null(log.PreviousIn("pale_choir"));

        // OlderThan is the log VIEWER's diff: the next entry back from the same region.
        Assert.Equal(10, log.OlderThan(0)!.Depth);   // entry 0 is cinderworks 20
        Assert.Null(log.OlderThan(1));               // entry 1 is the only umbral_reach run
    }

    /// <summary>Every measurement survives being saved and reloaded.</summary>
    /// <remarks>
    /// A log that silently dropped, say, TargetsPerActivation would leave the diff quietly lying about
    /// what changed between two runs — and the diff is the whole reason to keep old reports.
    /// </remarks>
    [Fact]
    public void test_a_report_survives_a_save_round_trip()
    {
        var before = Report("marrow_wastes", 42) with { IsRecord = true, AverageHitSize = 653.25f };

        var after = RunLog.FromSave(RunLog.ToSave(before));

        // Field by field, not record equality: RunReport holds an affix LIST, and a record compares
        // that by REFERENCE — so `Assert.Equal(before, after)` fails on two identical reports and would
        // have passed on two that differed in every number if they had shared a list instance.
        Assert.Equal(before.RegionId, after.RegionId);
        Assert.Equal(before.Depth, after.Depth);
        Assert.Equal(before.IsRecord, after.IsRecord);
        Assert.Equal(before.Outcome, after.Outcome);
        Assert.Equal(before.WallWave, after.WallWave);
        Assert.Equal(before.WallArchetype, after.WallArchetype);
        Assert.Equal(before.WallAffixes, after.WallAffixes);
        Assert.Equal(before.WallCreatures, after.WallCreatures);
        Assert.Equal(before.AbsorbedFraction, after.AbsorbedFraction);
        Assert.Equal(before.AverageHitSize, after.AverageHitSize);
        Assert.Equal(before.TargetsPerActivation, after.TargetsPerActivation);
        Assert.Equal(before.CreaturesPerWave, after.CreaturesPerWave);
        Assert.Equal(before.HealthLostPerWaveFraction, after.HealthLostPerWaveFraction);
        Assert.Equal(before.SecondsPerWave, after.SecondsPerWave);
        Assert.Equal(before.SampledWaves, after.SampledWaves);
    }

    /// <summary>Old saves wrote bare enum ints; they read through the frozen table, never a cast.</summary>
    [Fact]
    public void test_a_legacy_int_only_report_maps_its_affixes_through_the_frozen_table()
    {
        // An old report's ints, written when the enum still carried HOLLOW at 9 and LEGION at 10.
        var s = RunLog.ToSave(Report("cinderworks", 31)) with
        {
            OutcomeName = null, WallArchetypeName = null, WallAffixNames = new List<string>(),
            WallAffixes = new List<int> { 8, 9, 10, 99 },   // Warded, Hollow (retired), Legion, garbage
        };

        var report = RunLog.FromSave(s);

        Assert.Equal(new[] { Affix.Warded, Affix.Legion }, report.WallAffixes);
    }

    /// <summary>A report written today carries its enums as names — retiring a member can never re-label a wall.</summary>
    [Fact]
    public void test_a_report_written_today_carries_its_enums_as_names()
    {
        var s = RunLog.ToSave(Report("cinderworks", 31) with
        {
            WallAffixes = new List<Affix> { Affix.Legion },
        });

        Assert.Equal("Legion", Assert.Single(s.WallAffixNames));
        Assert.False(string.IsNullOrEmpty(s.OutcomeName));
        Assert.False(string.IsNullOrEmpty(s.WallArchetypeName));
    }

    /// <summary>The whole log round-trips in order.</summary>
    [Fact]
    public void test_the_log_survives_a_save_round_trip()
    {
        var log = new RunLog();
        log.Add(Report("a", 1));
        log.Add(Report("b", 2));
        log.Add(Report("a", 3));

        var restored = new RunLog();
        restored.Restore(log.Entries.Select(RunLog.ToSave).Select(RunLog.FromSave));

        Assert.Equal(log.Entries.Select(e => (e.RegionId, e.Depth)),
                     restored.Entries.Select(e => (e.RegionId, e.Depth)));
        Assert.Equal(3, restored.Newest!.Depth);
    }
}
