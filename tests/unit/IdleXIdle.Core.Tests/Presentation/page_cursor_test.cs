using System;
using System.Collections.Generic;
using IdleXIdle.Core.Presentation;
using Xunit;

namespace IdleXIdle.Core.Tests.Presentation;

/// <summary>
/// ONE float transform for the cursor (UI polish brief §12–§16, LAW 5 and LAW 6): the page-space cursor
/// is the exact inverse of the transform the page is drawn through, computed once in floating point;
/// the only rounding is the final floor to a page pixel, which is where the drawn rectangle's edge is.
/// </summary>
/// <remarks>
/// Before this file the menu screens received a cursor floored to 480×270 canvas space, multiplied by 4
/// and divided by the page scale — a ~4.5 page-pixel grid, so a click one pixel inside a button's drawn
/// edge could land outside its hit rect. These tests fail if any rounding creeps back in before the
/// final floor, if the inverse and the forward transform disagree, or if a point one screen pixel left
/// of the page rounds INTO it.
/// </remarks>
public class PageCursorTests
{
    private const int CanvasW = 1920;
    private const int CanvasH = 1080;
    private const float OverlayScale = (1920f - 180f - 20f) / 1920f;   // Game1.BaseOverlayScale
    private const float OverlayLeft = 180f;

    /// <summary>The present rects a player can actually have: each window size aspect-fit.</summary>
    public static IEnumerable<object[]> Presents()
    {
        foreach (var (w, h) in new[] { (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160) })
            yield return new object[] { CanvasFit.PresentFit(w, h) };
    }

    private static PageFrame Frame(FitRect present) => new(present, CanvasW, CanvasH, OverlayScale, OverlayLeft);

    [Theory]
    [MemberData(nameof(Presents))]
    public void test_page_cursor_is_the_exact_inverse_of_the_page_transform(FitRect present)
    {
        // Arrange — a grid of page points, including the page's own corners.
        var frame = Frame(present);
        for (var px = 0; px <= 1920; px += 240)
        for (var py = 0; py <= 1080; py += 135)
        {
            // Act — draw the point (page → screen), then ask where that screen position points.
            var (sx, sy) = frame.PageToScreen(px, py);
            var (bx, by) = frame.ScreenToPage(sx, sy);

            // Assert — back where it started, to well under a page pixel.
            Assert.True(MathF.Abs(bx - px) < 1e-2f && MathF.Abs(by - py) < 1e-2f,
                $"page ({px},{py}) → screen ({sx},{sy}) → page ({bx},{by}) at present {present}");
        }
    }

    [Theory]
    [MemberData(nameof(Presents))]
    public void test_a_click_just_inside_a_drawn_edge_is_inside_and_just_outside_is_outside(FitRect present)
    {
        // Arrange — a button drawn at page (100, 100); its left edge lands at a screen x.
        var frame = Frame(present);
        var (edgeX, edgeY) = frame.PageToScreen(100, 100);

        // Act — a click half a screen pixel inside and half a pixel outside the edge.
        var inside = PageFrame.Floor(frame.ScreenToPage(edgeX + 0.5f, edgeY + 0.5f));
        var outside = PageFrame.Floor(frame.ScreenToPage(edgeX - 0.5f, edgeY - 0.5f));

        // Assert — the floored cursor agrees with the drawn edge on both sides.
        Assert.True(inside.X >= 100 && inside.Y >= 100, $"inside click floored to {inside} at present {present}");
        Assert.True(outside.X < 100 && outside.Y < 100, $"outside click floored to {outside} at present {present}");
    }

    [Fact]
    public void test_a_screen_pixel_left_of_the_page_maps_outside_it_never_onto_its_first_column()
    {
        // Arrange — 1280×720: a page pixel is smaller than a screen pixel, the case where truncation
        // toward zero used to fold a point at page x = -0.3 onto column 0.
        var frame = Frame(CanvasFit.PresentFit(1280, 720));
        var (edgeX, edgeY) = frame.PageToScreen(0, 0);

        // Act
        var cursor = PageFrame.Floor(frame.ScreenToPage(edgeX - 1f, edgeY - 1f));

        // Assert
        Assert.True(cursor.X < 0 && cursor.Y < 0, $"a point left of the page landed on {cursor}");
    }

    [Theory]
    [MemberData(nameof(Presents))]
    public void test_no_page_column_a_screen_pixel_can_reach_is_skipped(FitRect present)
    {
        // Arrange — walk 300 consecutive screen pixels across the page and collect the columns hit.
        var frame = Frame(present);
        var (startX, y) = frame.PageToScreen(400, 400);
        var columns = new SortedSet<int>();
        for (var i = 0; i < 300; i++)
            columns.Add(PageFrame.Floor(frame.ScreenToPage(startX + i, y)).X);

        // Assert — consecutive hits are never further apart than one screen pixel's worth of page pixels
        // (rounded up), i.e. the cursor is quantised to the SCREEN, never to a coarser grid.
        var pagePerScreenPixel = 1f / (frame.PresentScale * OverlayScale);
        var allowed = (int)MathF.Ceiling(pagePerScreenPixel);
        int? previous = null;
        foreach (var c in columns)
        {
            if (previous is { } p)
                Assert.True(c - p <= allowed, $"columns {p} → {c} skip more than {allowed} at present {present}");
            previous = c;
        }
    }

    [Fact]
    public void test_the_chrome_cursor_has_no_overlay_inset_and_no_page_scale()
    {
        // Arrange — the chrome (nav rail, pills, settings) is drawn at scale 1 in canvas coordinates.
        var frame = Frame(CanvasFit.PresentFit(1600, 900));
        var (sx, sy) = frame.CanvasToScreen(180, 540);

        // Act
        var chrome = frame.ScreenToCanvas(sx, sy);

        // Assert
        Assert.True(MathF.Abs(chrome.X - 180f) < 1e-2f && MathF.Abs(chrome.Y - 540f) < 1e-2f, $"chrome cursor {chrome}");
    }

    [Fact]
    public void test_floor_rounds_toward_negative_infinity_not_toward_zero()
    {
        Assert.Equal((-1, -1), PageFrame.Floor((-0.25f, -0.75f)));
        Assert.Equal((3, 0), PageFrame.Floor((3.999f, 0.001f)));
    }
}
