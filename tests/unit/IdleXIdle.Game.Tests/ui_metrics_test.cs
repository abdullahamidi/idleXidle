using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// UI SCALE is a DENSITY profile (UI polish brief §7–§11, LAW 7): the page stays 1920×1080 and what grows
/// is type, rows, buttons, icons, hit targets and padding — through ONE central source, <see cref="UiMetrics"/>.
/// These tests fail if a profile ever scales the page, if the ladder stops following the profile, or if
/// spacing and controls stop coming from the metrics.
/// </summary>
public class UiMetricsTests
{
    [Theory]
    [InlineData(100, 22, 22, 52, 52, 24, 24)]
    [InlineData(125, 22, 28, 52, 65, 24, 27)]
    [InlineData(150, 22, 33, 52, 78, 24, 30)]
    public void test_text_and_controls_scale_at_full_rate_and_spacing_at_half_rate(
        int percent, int textBase, int text, int controlBase, int control, int spaceBase, int space)
    {
        // Arrange
        UiMetrics.Apply(percent);

        // Assert
        Assert.Equal(percent, UiMetrics.Percent);
        Assert.Equal(text, UiMetrics.Text(textBase));
        Assert.Equal(control, UiMetrics.Control(controlBase));
        Assert.Equal(space, UiMetrics.Space(spaceBase));
    }

    [Fact]
    public void test_a_percent_the_game_does_not_offer_falls_back_to_100()
    {
        UiMetrics.Apply(137);
        Assert.Equal(100, UiMetrics.Percent);
        UiMetrics.Apply(0);
        Assert.Equal(100, UiMetrics.Percent);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void test_the_page_never_shrinks_with_the_profile(int percent)
    {
        // LAW 7: 150 % means reflow, not a 1.5× canvas zoom. The page every screen anchors to is fixed.
        UiMetrics.Apply(percent);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), UiKit.Page);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void test_the_typography_ladder_follows_the_profile_and_keeps_its_order(int percent)
    {
        UiMetrics.Apply(percent);

        var ladder = new[]
        {
            UiTypography.ScreenTitle, UiTypography.PrimaryValue, UiTypography.PanelTitle, UiTypography.Headline,
            UiTypography.NavigationLabel, UiTypography.Body, UiTypography.Secondary, UiTypography.Caption,
        };
        for (var i = 1; i < ladder.Length; i++)
            Assert.True(ladder[i - 1] > ladder[i], $"rung {i - 1} ({ladder[i - 1]}) must be louder than rung {i} ({ladder[i]}) at {percent}%");

        Assert.Equal(UiMetrics.Text(22), UiTypography.Body);
        Assert.Equal(UiMetrics.Text(16), UiTypography.Caption);
        Assert.Equal(UiTypography.Body * 13 / 10, UiTypography.Pitch(UiTypography.Body));
        // The aliases stay aliases.
        Assert.Equal(UiTypography.Body, UiTypography.Label);
        Assert.Equal(UiTypography.NavigationLabel, UiTypography.ButtonText);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void test_the_panel_grid_is_derived_from_the_title_rung_not_frozen(int percent)
    {
        UiMetrics.Apply(percent);
        // A caption sits one title line under the title; the first row one caption line under that.
        Assert.True(UiTypography.PanelCaptionTop >= UiTypography.PanelTitleTop + UiTypography.PanelTitle,
            $"caption top {UiTypography.PanelCaptionTop} sits inside the title line at {percent}%");
        Assert.True(UiTypography.PanelBodyTop >= UiTypography.PanelCaptionTop + UiTypography.Secondary,
            $"body top {UiTypography.PanelBodyTop} sits inside the caption line at {percent}%");
        Assert.True(UiTypography.PanelBodyTopBare >= UiTypography.PanelTitleTop + UiTypography.PanelTitle);
    }

    [Fact]
    public void test_the_100_percent_panel_grid_is_the_house_standard()
    {
        // The numbers TrainingScreen was measured at (UiTypography's own remarks) must not drift at 100 %.
        UiMetrics.Apply(100);
        Assert.Equal(22, UiTypography.PanelTitleTop);
        Assert.Equal(56, UiTypography.PanelCaptionTop);
        Assert.Equal(92, UiTypography.PanelBodyTop);
        Assert.Equal(62, UiTypography.PanelBodyTopBare);
        Assert.Equal(44, UiTypography.ModalTitleTop);
    }

    [Fact]
    public void test_hit_targets_rows_and_buttons_grow_with_the_profile()
    {
        UiMetrics.Apply(100);
        var (hit100, row100, btn100, icon100) = (UiMetrics.HitTargetMinimum, UiMetrics.RowHeight, UiMetrics.ButtonHeight, UiMetrics.IconSize);
        UiMetrics.Apply(150);
        Assert.True(UiMetrics.HitTargetMinimum > hit100 && UiMetrics.RowHeight > row100
                    && UiMetrics.ButtonHeight > btn100 && UiMetrics.IconSize > icon100);
        Assert.True(hit100 >= 40, "a 100 % hit target under 40 px is too small to point at");
        UiMetrics.Apply(100);
    }

    [Theory]
    [InlineData(1920, 100)]
    [InlineData(1920, 150)]
    public void test_the_inspector_never_takes_more_than_two_fifths_of_the_page(int pageWidth, int percent)
    {
        UiMetrics.Apply(percent);
        Assert.True(UiMetrics.InspectorWidth(pageWidth) <= pageWidth * 2 / 5);
        Assert.True(UiMetrics.InspectorWidth(pageWidth) >= 496);
        UiMetrics.Apply(100);
    }
}

/// <summary>The display profile offers 150 % again, and a saved 150 stays 150 (brief §17, §83).</summary>
public class UiScaleProfileTests
{
    [Fact]
    public void test_the_game_offers_all_three_profiles_and_auto()
    {
        Assert.Equal(new[] { 100, 125, 150, 0 }, Display.UiScaleSteps);
        Assert.Equal(150, Display.ResolveUiScale(150, 1920));
        Assert.Equal(125, Display.ResolveUiScale(0, 1280));
        Assert.Equal(100, Display.ResolveUiScale(0, 1920));
    }
}
