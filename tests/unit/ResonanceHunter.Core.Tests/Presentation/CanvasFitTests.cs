using ResonanceHunter.Core.Presentation;
using Xunit;

namespace ResonanceHunter.Core.Tests.Presentation;

/// <summary>
/// The canvas-to-monitor fit. These are cheap tests guarding an expensive class of bug: a wrong
/// letterbox offset does not crash, it just makes every button in the game click a few pixels off.
/// </summary>
public class CanvasFitTests
{
    [Theory]
    [InlineData(1920, 1080, 4)]  // exactly 4x — the common 1080p case
    [InlineData(2560, 1440, 5)]  // exactly 5x
    [InlineData(1440, 810, 3)]   // the default window
    [InlineData(1366, 768, 2)]   // awkward laptop panel: 2.84x wide, 2.84x tall → 2x
    public void test_largest_integer_scale_picks_biggest_whole_multiple_that_fits(int w, int h, int expected)
    {
        Assert.Equal(expected, CanvasFit.LargestIntegerScale(w, h));
    }

    [Fact]
    public void test_largest_integer_scale_is_bound_by_the_tighter_axis()
    {
        // Ultrawide: 8x the canvas across, but only 3x tall. Fitting the width would crop the game.
        Assert.Equal(3, CanvasFit.LargestIntegerScale(3840, 810));
    }

    [Fact]
    public void test_largest_integer_scale_never_returns_zero_on_a_tiny_viewport()
    {
        // A 0 scale is a divide-by-zero in the mouse mapping and a collapsed draw rect — a black screen.
        Assert.Equal(1, CanvasFit.LargestIntegerScale(320, 200));
    }

    [Fact]
    public void test_present_centres_the_canvas_and_letterboxes_the_remainder()
    {
        // 1920x1080 at 4x = 1920x1080 exactly wide, so no pillarbox; height fits exactly too.
        var exact = CanvasFit.Present(1920, 1080, 4);
        Assert.Equal(new FitRect(0, 0, 1920, 1080), exact);

        // 1920x1200 at 4x leaves 120px of vertical slack — 60 top, 60 bottom.
        var boxed = CanvasFit.Present(1920, 1200, 4);
        Assert.Equal(new FitRect(0, 60, 1920, 1080), boxed);
    }

    [Fact]
    public void test_present_pillarboxes_an_ultrawide_viewport()
    {
        // 3440x1440 at 5x → 2400x1350 canvas; 1040px of horizontal slack, split evenly.
        var r = CanvasFit.Present(3440, 1440, 5);
        Assert.Equal(new FitRect(520, 45, 2400, 1350), r);
    }

    [Fact]
    public void test_to_canvas_maps_a_click_back_through_the_letterbox()
    {
        var present = CanvasFit.Present(1920, 1200, 4); // origin (0,60), 4x

        // The canvas's own origin.
        Assert.Equal((0, 0), CanvasFit.ToCanvas(0, 60, present));

        // Dead centre of the screen is dead centre of the canvas.
        Assert.Equal((240, 135), CanvasFit.ToCanvas(960, 600, present));

        // The last real pixel.
        Assert.Equal((479, 269), CanvasFit.ToCanvas(1919, 1139, present));
    }

    [Fact]
    public void test_to_canvas_round_trips_the_default_window()
    {
        var present = CanvasFit.Present(1440, 810, 3);
        Assert.Equal((0, 0), CanvasFit.ToCanvas(0, 0, present));
        Assert.Equal((100, 50), CanvasFit.ToCanvas(300, 150, present));
    }

    [Fact]
    public void test_to_canvas_maps_letterbox_clicks_outside_the_canvas()
    {
        // Regression: C# integer division truncates TOWARD ZERO, so a click on the letterbox bar one
        // pixel above the canvas divided to y=0 — landing on the canvas's top row and pressing whatever
        // was there. Off-canvas must stay off-canvas, which every caller reads as "no hit".
        var present = CanvasFit.Present(1920, 1200, 4); // 60px bar top and bottom

        var (_, aboveY) = CanvasFit.ToCanvas(960, 59, present);
        Assert.True(aboveY < 0, $"a click on the top letterbox bar mapped to canvas y={aboveY}");

        var (_, belowY) = CanvasFit.ToCanvas(960, 1141, present);
        Assert.True(belowY >= CanvasFit.CanvasHeight, $"a click below the canvas mapped to y={belowY}");
    }

    [Fact]
    public void test_to_canvas_maps_pillarbox_clicks_outside_the_canvas()
    {
        var present = CanvasFit.Present(3440, 1440, 5); // 520px bar left and right

        var (leftX, _) = CanvasFit.ToCanvas(519, 700, present);
        Assert.True(leftX < 0, $"a click on the left pillarbox mapped to canvas x={leftX}");

        var (rightX, _) = CanvasFit.ToCanvas(2921, 700, present);
        Assert.True(rightX >= CanvasFit.CanvasWidth, $"a click right of the canvas mapped to x={rightX}");
    }

    [Fact]
    public void test_every_offered_windowed_scale_fits_a_1080p_desktop()
    {
        // A window the player cannot fit on their monitor is a window they cannot close.
        foreach (var s in CanvasFit.WindowedScales)
        {
            Assert.True(CanvasFit.CanvasWidth * s <= 1920, $"{s}x is wider than 1080p");
            Assert.True(CanvasFit.CanvasHeight * s <= 1080, $"{s}x is taller than 1080p");
        }
    }
    [Fact]
    public void test_present_fit_fills_a_small_laptop_edge_to_edge()
    {
        // Arrange + Act — the two panels the integer rule served worst.
        var hd = CanvasFit.PresentFit(1280, 720);
        var laptop = CanvasFit.PresentFit(1366, 768);

        // Assert — 1280x720 is exactly 16:9, so the fit IS the screen; 1366x768 fills the height.
        Assert.Equal((0, 0, 1280, 720), (hd.X, hd.Y, hd.Width, hd.Height));
        Assert.Equal(768, laptop.Height);
        Assert.True(laptop.Width >= 1364, $"width {laptop.Width} should fill nearly all of 1366");
    }

    [Fact]
    public void test_to_canvas_maps_a_fractional_fit_exactly()
    {
        // Arrange — a 1280x720 fit is scale 8/3: no integer maps it.
        var present = CanvasFit.PresentFit(1280, 720);

        // Act + Assert — corners land on the canvas corners, the centre on the centre,
        // and a point one pixel left of the canvas still maps OFF it.
        Assert.Equal((0, 0), CanvasFit.ToCanvas(0, 0, present));
        Assert.Equal((479, 269), CanvasFit.ToCanvas(1279, 719, present));
        Assert.Equal((240, 135), CanvasFit.ToCanvas(640, 360, present));
        Assert.True(CanvasFit.ToCanvas(-1, 0, present).X < 0);
    }
}
