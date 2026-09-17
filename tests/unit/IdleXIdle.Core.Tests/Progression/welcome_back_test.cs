using System.Linq;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Warrens;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>UX V2 P1.3: the welcome says only what happened, in a person's words, and only when it is worth a panel.</summary>
public class welcome_back_test
{
    private static OfflineHunt.Result Hunt(long gleam = 0, int waves = 0, int falls = 0, int deepest = 0)
        => new(gleam, waves, falls, deepest, 0, 0, 0);

    [Fact]
    public void test_the_absence_reads_the_way_a_person_says_it()
    {
        Assert.Equal("18 MIN", WelcomeSummary.AwayText(18 * 60));
        Assert.Equal("1 MIN", WelcomeSummary.AwayText(20));
        Assert.Equal("6h 42m", WelcomeSummary.AwayText(6 * 3600 + 42 * 60));
        Assert.Equal("2h", WelcomeSummary.AwayText(2 * 3600));
        Assert.Equal("3 DAYS 2h", WelcomeSummary.AwayText(3 * 86400 + 2 * 3600));
        Assert.Equal("1 DAY", WelcomeSummary.AwayText(86400));
    }

    [Fact]
    public void test_zero_figures_are_omitted_not_printed()
    {
        var w = new WelcomeSummary(3600, Hunt(gleam: 11320, waves: 47, falls: 0, deepest: 41), new WarrenYield(7100, 1240, 0, 0), WarrenOpen: true);

        // THE PANEL IS A RESULTS GRID SINCE 2026-09-09, so the rule is pinned on the tiles: a figure
        // that did not happen gets no tile at all, rather than a tile reading zero. Falls is zero here
        // and Essence and Scrap are zero, and none of the three may appear.
        Assert.Equal(new[] { "47", "11,320", "41" }, w.HuntTiles().Select(t => t.Value));
        Assert.Equal(new[] { WelcomeFigure.Waves, WelcomeFigure.Gleam, WelcomeFigure.Deepest },
                     w.HuntTiles().Select(t => t.Figure));
        Assert.Equal(new[] { WelcomeFigure.Gleam, WelcomeFigure.Dust }, w.WarrenTiles().Select(t => t.Figure));

        // ...and the Warren screen's own inline sentence still says the same two figures its own way.
        Assert.Equal(new[] { "+7,100 GLEAM", "+1,240 DUST" }, w.WarrenParts());
    }

    [Fact]
    public void test_a_closed_warren_has_no_row_even_if_a_yield_was_computed()
    {
        var w = new WelcomeSummary(3600, Hunt(gleam: 10), new WarrenYield(500, 0, 0, 0), WarrenOpen: false);
        Assert.Empty(w.WarrenParts());
        Assert.True(w.HasNews);
    }

    [Fact]
    public void test_the_panel_needs_a_real_absence_and_something_to_say()
    {
        Assert.False(new WelcomeSummary(59, Hunt(gleam: 900), default, false).ShowsPanel);   // a short trip is a toast
        Assert.True(new WelcomeSummary(60, Hunt(gleam: 900), default, false).ShowsPanel);
        Assert.False(new WelcomeSummary(36000, Hunt(), default, true).ShowsPanel);            // ten hours, nothing earned
        Assert.True(new WelcomeSummary(36000, Hunt(), new WarrenYield(0, 12, 0, 0), true).ShowsPanel);
    }
}
