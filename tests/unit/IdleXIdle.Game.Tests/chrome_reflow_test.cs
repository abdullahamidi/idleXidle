using System.Reflection;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE HOST CHROME AT EVERY DENSITY PROFILE (UI polish C6, brief §7–§11, §17, §83–§85). The settings
/// modal, the nav rail and the hint slot are drawn by Game1 in true 1920×1080 canvas space, so nothing
/// in a screen file can hold them right — and at 125 / 150 % each of them used to break in a way that
/// only a capture could see: the panel's rows printed through each other, the rail's eleventh tile ran
/// under its own label, and the hint plate sat across the screens' subtitle line as ghost text.
/// </summary>
/// <remarks>
/// These are ARITHMETIC claims about the layout, so they belong in a test rather than in the capture
/// rig: each one is a relation between two numbers the profile moves, and a relation is exactly what a
/// screenshot cannot assert. What the captures still own is whether the result LOOKS right — that is
/// the split <c>tools/asset-pipeline/capture.sh</c> exists for.
///
/// Game1's layout members are private statics, so they are read by reflection, the same way
/// <see cref="PageLayoutTests"/> walks them.
/// </remarks>
public class ChromeReflowTests
{
    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static T Read<T>(string name)
    {
        var t = typeof(Game1);
        if (t.GetProperty(name, Statics) is { } p) return (T)p.GetValue(null)!;
        if (t.GetField(name, Statics) is { } f) return (T)f.GetValue(null)!;
        throw new Xunit.Sdk.XunitException($"Game1 has no static member named {name} — the chrome was renamed without its test.");
    }

    /// <summary>Call a private static one-argument helper — the banner's geometry takes "does this screen draw a subtitle".</summary>
    private static T Call<T>(string name, params object[] args)
    {
        var m = typeof(Game1).GetMethod(name, Statics)
                ?? throw new Xunit.Sdk.XunitException($"Game1 has no static method named {name} — the chrome was renamed without its test.");
        return (T)m.Invoke(null, args)!;
    }

    /// <summary>
    /// THE ESCAPE HATCH. A player who picks 150 % on a small screen must be able to get back to 100 %,
    /// so the UI SCALE row has to be reachable at rest — inside the panel, above the anchored QUIT row
    /// (which is the top of what scrolls away), clear of the close icon, and still a real hit target.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_ui_scale_row_is_reachable_at_every_profile(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            var panel = Read<Rectangle>("SettingsPanel");
            var scale = Read<Rectangle>("SettingsScaleRow");
            var quit = Read<Rectangle>("SettingsQuit");
            var close = Read<Rectangle>("SettingsCornerClose");

            Assert.True(panel.Contains(scale), $"the UI SCALE row {scale} left the settings panel {panel} at {percent}%");
            Assert.True(panel.Contains(quit), $"QUIT TO DESKTOP {quit} left the settings panel {panel} at {percent}%");
            Assert.True(panel.Contains(close), $"the close icon {close} left the settings panel {panel} at {percent}%");
            // Above the footer: the QUIT row is anchored outside the scroll, so anything under it is
            // only reachable by scrolling — and the one row that must never be is this one.
            Assert.True(scale.Bottom <= quit.Y, $"the UI SCALE row {scale} is under the anchored QUIT row {quit} at {percent}%");
            Assert.False(scale.Intersects(close), $"the UI SCALE row {scale} is under the close icon {close} at {percent}%");
            Assert.True(scale.Height >= UiMetrics.HitTargetMinimum,
                        $"the UI SCALE row is {scale.Height} px tall at {percent}%, under the {UiMetrics.HitTargetMinimum} px hit floor");
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>
    /// THE RAIL'S ELEVEN TILES. The rail is a fixed 1080 tall at every profile — it is canvas geometry —
    /// so what must fit is the LABEL at the profile's own rung: its foot clearance, the breath over it
    /// and the icon at its floor. If this fails, the icon is being drawn through its own caption, which
    /// is what happened when the eleventh tile took every tile from 108 px to 98.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_every_nav_tile_holds_its_label_and_its_icon(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            // The tile's grid moved out of Game1 into NavTileGrid (2026-09-12) so the locked tile's
            // chain could read the same rule the icon and label do — so these are named consts now
            // rather than private statics behind reflection.
            var tile = Read<int>("NavTileHeight");
            const int foot = NavTileGrid.LabelFoot;
            const int gap = NavTileGrid.IconGap;
            const int iconMin = NavTileGrid.IconMin;
            var stack = UiTypography.NavigationLabel + foot + gap + iconMin;
            Assert.True(stack <= tile,
                        $"a nav tile is {tile} px at {percent}% but its label ({UiTypography.NavigationLabel}) + " +
                        $"foot ({foot}) + gap ({gap}) + the icon's floor ({iconMin}) needs {stack}");

            // The last tile — ROSTER — ends on the canvas, not past it.
            var rect = typeof(Game1).GetMethod("NavHexRect", Statics)!;
            var tiles = ((System.Collections.ICollection)typeof(Game1).GetField("Nav", Statics)!.GetValue(null)!).Count;
            var last = (Rectangle)rect.Invoke(null, new object[] { tiles - 1 })!;
            Assert.True(last.Bottom <= 1080, $"the last nav tile {last} hangs off the 1080 canvas at {percent}%");
            // And no two tiles share a pixel.
            var previous = (Rectangle)rect.Invoke(null, new object[] { tiles - 2 })!;
            Assert.True(previous.Bottom <= last.Y, $"nav tiles {previous} and {last} overlap at {percent}%");

            // THE HUNT TILE'S LIVE BAR sits in the foot band the label already leaves, so it costs the
            // tile no geometry at any profile — and it must not print through the label above it or
            // off the tile below. (Playtest 2026-09-09: the rail shows that the fight is running.)
            var first = (Rectangle)rect.Invoke(null, new object[] { 0 })!;
            var barTop = first.Bottom - 7;
            Assert.True(barTop + 4 <= first.Bottom,
                        $"the HUNT tile's health bar runs past its own foot at {percent}%");
            Assert.True(barTop >= first.Bottom - foot,
                        $"the HUNT tile's health bar at {percent}% is above the {foot} px foot band and would cross the label");
            Assert.True(Read<int>("NavRailWidth") - 52 >= 4,
                        "the bar is narrower than UiKit.Bar will draw");
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>
    /// THE HINT SLOT STARTS BELOW THE SUBTITLE. Every inset screen draws its name at page y 24 and, on
    /// TRAITS / MASTERY / VAULT, one <see cref="UiTypography.Secondary"/> subtitle line at page y 80.
    /// The slot is chrome — canvas space — so it must clear that line THROUGH the overlay matrix at
    /// every profile. Pinned at the 100 % literal (canvas 86) it sat across the subtitle at 125 and
    /// 150 %, and the subtitle showed through the plate as ghost text.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_hint_slot_clears_the_screen_subtitle(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            const int screenSubtitleTop = 80;   // the literal the screens draw their subtitle at, in PAGE space
            var subtitleBottomCanvas = (int)MathF.Round(
                (screenSubtitleTop + UiTypography.Pitch(UiTypography.Secondary)) * Read<float>("OverlayScale"));
            // ON A SCREEN THAT DRAWS ONE. Only VAULT, TRAITS and MASTERY do; on the other eight the
            // subtitle's room is the slot's, and taking it is what keeps the slot off the first panel.
            var slotTop = Call<int>("ScreenBannerTop", true);
            Assert.True(slotTop >= subtitleBottomCanvas,
                        $"the hint slot starts at canvas {slotTop} at {percent}%, over a subtitle that ends at {subtitleBottomCanvas}");
            // And it is never so low that it starts off the page.
            Assert.True(slotTop < UiKit.Page.Bottom, $"the hint slot starts at canvas {slotTop} at {percent}%, off the page");

            // A screen with NO subtitle starts its slot higher — never lower, and never above the rule.
            var bare = Call<int>("ScreenBannerTop", false);
            Assert.True(bare <= slotTop, $"a subtitle-less screen's slot starts at {bare}, below a subtitled screen's {slotTop}");
            Assert.True(bare > 0, $"a subtitle-less screen's slot starts at {bare}, off the top of the page");
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>
    /// A HINT IS NEVER SQUEEZED. Its one line keeps the paragraph rung with a pad above and below at
    /// every profile — the rule is reflow, never smaller type.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_a_one_line_hint_keeps_its_rung(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            foreach (var hasSubtitle in new[] { true, false })
            {
                var height = Call<int>("HintLineHeight", hasSubtitle);
                Assert.True(height >= UiTypography.Body,
                            $"a one-line hint is {height} px tall at {percent}% (subtitle: {hasSubtitle}), "
                            + $"under its own {UiTypography.Body} px line");
            }
        }
        finally { UiMetrics.Apply(100); }
    }
}
