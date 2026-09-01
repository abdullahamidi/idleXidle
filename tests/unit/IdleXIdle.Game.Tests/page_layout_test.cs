using System.Reflection;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// Every page-anchored layout rectangle a screen exposes as a STATIC member lies inside the page, at every
/// density profile (brief §120: "layout computes, no NaN/invalid rects, controls remain in viewport").
/// </summary>
/// <remarks>
/// Reflection rather than a hand list, so a screen that grows a new static rectangle is covered the day it
/// is written. The rects checked are the top-level regions (panels, inspectors, strips, buttons); rows
/// inside a scroll region are computed per frame from instance state and are the capture rig's job.
/// </remarks>
public class PageLayoutTests
{
    private static IEnumerable<(string Owner, string Name, Func<Rectangle> Get)> StaticRects()
    {
        var asm = typeof(UiKit).Assembly;
        foreach (var type in asm.GetTypes().Where(t => t.Name.EndsWith("Screen", StringComparison.Ordinal) || t.Name == "Game1"))
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var p in type.GetProperties(flags).Where(p => p.PropertyType == typeof(Rectangle) && p.GetIndexParameters().Length == 0))
            {
                var prop = p;
                yield return (type.Name, p.Name, () => (Rectangle)prop.GetValue(null)!);
            }
            foreach (var f in type.GetFields(flags).Where(f => f.FieldType == typeof(Rectangle)))
            {
                var field = f;
                yield return (type.Name, f.Name, () => (Rectangle)field.GetValue(null)!);
            }
        }
    }

    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_every_static_layout_rect_lies_inside_the_page(int percent)
    {
        UiMetrics.Apply(percent);
        var page = UiKit.Page;
        var failures = new List<string>();
        var count = 0;
        foreach (var (owner, name, get) in StaticRects())
        {
            // The arena's clip and the fight's stage geometry are the canvas's, not the page's: they are
            // checked against the canvas (which is the page's size) like everything else.
            Rectangle r;
            try { r = get(); }
            catch (TargetInvocationException e) { failures.Add($"{owner}.{name} threw {e.InnerException?.GetType().Name} at {percent}%"); continue; }
            count++;
            if (r.Width <= 0 || r.Height <= 0) failures.Add($"{owner}.{name} is empty ({r}) at {percent}%");
            else if (r.X < 0 || r.Y < 0 || r.Right > page.Right || r.Bottom > page.Bottom)
                failures.Add($"{owner}.{name} = {r} leaves the page at {percent}%");
        }
        UiMetrics.Apply(100);
        Assert.True(count > 20, $"only {count} static rects found — the reflection walk is broken");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
