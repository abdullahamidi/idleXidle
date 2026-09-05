using IdleXIdle.Core.Presentation;
using Xunit;

namespace IdleXIdle.Core.Tests.Presentation;

/// <summary>
/// The canvas ↔ page transform is one function with one inverse — the capture rig's CANVAS dial and
/// the screen's PAGE hit-testing read the same numbers, at every profile and window.
/// </summary>
public class page_frame_canvas_page_test
{
    private static PageFrame Frame(int w = 1440, int h = 810)
        => new(new FitRect(0, 0, w, h), 1920, 1080, (1920f - 180f - 20f) / 1920f, 180f);

    [Theory]
    [InlineData(180f, 0f, 0f, 0f)]           // the page's origin sits where the rail ends
    [InlineData(1900f, 1080f, 1919.6f, 1205.6f)]
    [InlineData(717.4f, 376.3f, 600f, 420f)]  // the trader's first card at 150 %, measured on a capture
    public void test_canvas_to_page_and_back(float cx, float cy, float px, float py)
    {
        var f = Frame();
        var (x, y) = f.CanvasToPage(cx, cy);
        Assert.Equal(px, x, 0.5f);
        Assert.Equal(py, y, 0.5f);
        var (bx, by) = f.PageToCanvas(x, y);
        Assert.Equal(cx, bx, 0.01f);
        Assert.Equal(cy, by, 0.01f);
    }

    [Fact]
    public void test_screen_to_page_goes_through_the_same_canvas_transform()
    {
        var f = Frame(1600, 900);
        var (sx, sy) = f.CanvasToScreen(960f, 540f);
        var viaScreen = f.ScreenToPage(sx, sy);
        var viaCanvas = f.CanvasToPage(960f, 540f);
        Assert.Equal(viaCanvas.X, viaScreen.X, 0.01f);
        Assert.Equal(viaCanvas.Y, viaScreen.Y, 0.01f);
    }
}
