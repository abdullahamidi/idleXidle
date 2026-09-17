using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Traits;
using Xunit;

namespace IdleXIdle.Core.Tests.Traits;

/// <summary>
/// A trait a FALL awakens carries the wave that fell — not the wave the previous fall ended on.
/// </summary>
/// <remarks>
/// <para>
/// <c>TraitFirst.Wave</c> is provenance the SAVE keeps for the life of a career: it is written by
/// <see cref="TraitLedger.SaveProvenance"/> beside the champion and the region — the two the TRAITS
/// screen prints today — read back on every load, and available to whatever prints it next. A stamp
/// one fall behind is therefore a wrong number persisted for good, which is worth fixing whether or
/// not a surface shows it yet. The stamp comes from <c>HuntScreen</c>'s fall branch, which does two
/// things in an order that matters: it assigns <c>_fellWave</c> for THIS fall, and it re-checks every
/// rule with that wave. Until 2026-09-16 the re-check ran first.
/// </para>
/// <para>
/// So this drives two real falls at two different waves through the real <see cref="TraitWatch"/> and
/// <see cref="TraitLedger"/>, <b>in the order the screen's own source has them</b> — the order is read
/// out of <c>HuntScreen.cs</c> rather than assumed, which is what makes this a test of the product and
/// not of a model of it. The screen lives in the Game project, which this test project cannot
/// reference, so its source is read the way <c>HuntScreenFeedbackTests</c> and the repository's Python
/// gates read it.
/// </para>
/// </remarks>
public class fall_trait_stamp_test
{
    /// <summary>The wave that fell is assigned before the rules are re-checked with it.</summary>
    private const string Assign = "_fellWave = Math.Max(1, _replayWave);";

    /// <summary>The re-check, which stamps whatever <c>_fellWave</c> holds at that instant.</summary>
    private const string Recheck = "watchFell.Recheck(_fellWave);";

    /// <summary>The report is written before either — WHAT KILLED YOU reads the log the fall just wrote.</summary>
    private const string WriteLog = "Log.Add(_run!.Report(isRecord: _run.Wave > _recordToBeat));";

    private static string HuntScreenSource()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "IdleXIdle.Game", "HuntScreen.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("HuntScreen.cs not found above the test binary.");
    }

    [Fact]
    public void test_a_trait_awakened_by_a_fall_is_stamped_with_the_wave_that_fell()
    {
        var src = HuntScreenSource();
        var assignAt = src.IndexOf(Assign, StringComparison.Ordinal);
        var recheckAt = src.IndexOf(Recheck, StringComparison.Ordinal);
        var logAt = src.IndexOf(WriteLog, StringComparison.Ordinal);
        Assert.True(assignAt >= 0, "the fall branch no longer assigns the wave that fell");
        Assert.True(recheckAt >= 0, "the fall branch no longer re-checks the trait rules");
        Assert.True(logAt >= 0, "the fall branch no longer writes its report to the log");

        // The one order the log's own rule needs: the report is written before the rules read it.
        Assert.True(logAt < recheckAt, "the rules are re-checked before the report they read is written");

        var ledger = new TraitLedger();
        var watch = new TraitWatch(ledger) { CharacterId = "seeker", RegionId = "verdant_hollow" };
        var fellWave = 1;

        // ONE FALL, AS THE SCREEN TAKES IT. The wave assignment and the re-check run in the source's
        // own order, so a screen that stamps before it assigns stamps the PREVIOUS fall's wave here too.
        void Fall(int wave, TraitAccount account)
        {
            watch.Account = account;
            if (assignAt < recheckAt)
            {
                fellWave = Math.Max(1, wave);
                watch.Recheck(fellWave);
            }
            else
            {
                watch.Recheck(fellWave);
                fellWave = Math.Max(1, wave);
            }
        }

        // FALL ONE, at wave 12, with three straight deaths to one kind of creature behind it: WHAT
        // KILLED YOU is the one rule failure itself feeds, so it is the rule a fall awakens.
        Fall(12, TraitAccount.None with { WallStreak = 3 });
        var first = watch.TakeAwakened();
        Assert.Contains("t_what_killed_you", first);
        Assert.Equal(12, ledger.FirstOf("t_what_killed_you")!.Value.Wave);

        // FALL TWO, eighteen waves deeper, with a fourth region conquered between them. A different
        // rule crosses, so a different trait wakes — and it must carry THIS fall's wave.
        Fall(30, TraitAccount.None with { WallStreak = 3, RegionsConquered = 4 });
        var second = watch.TakeAwakened();
        Assert.Contains("t_homeground", second);
        var stamp = ledger.FirstOf("t_homeground")!.Value;
        Assert.Equal(30, stamp.Wave);
        Assert.NotEqual(12, stamp.Wave);

        // ...and the provenance's other two halves are the run's, as they always were.
        Assert.Equal("seeker", stamp.CharacterId);
        Assert.Equal("verdant_hollow", stamp.RegionId);
    }
}
