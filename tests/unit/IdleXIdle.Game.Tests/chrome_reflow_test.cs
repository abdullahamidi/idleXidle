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
    /// THE ACCESSIBILITY COLUMN'S CAPTION SITS UNDER ITS LAST SWITCH. Its y was written for five
    /// switches ("row four, plus a row") and GUIDANCE became the sixth, so the sentence printed across
    /// GUIDANCE's own row — label under it, words running beneath the ON button — at every profile, until
    /// the alpha check photographed it (2026-09-17). The layout now reads a switch count; the second half
    /// pins that count to the table Update and Draw walk, so a seventh switch cannot repeat this.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_settings_guidance_caption_sits_under_the_last_switch(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            // Arrange — the frame's unscrolled geometry at this profile.
            var last = Read<Rectangle>("SettingsLastToggleRow");
            var captionTop = Read<int>("SettingsSwitchCaptionY");
            var captionBottom = Read<int>("SettingsSwitchCaptionBottom");
            var rowsBottom = Read<int>("SettingsRowsBottom");
            var scrolls = Read<bool>("SettingsRowsScroll");
            var danger = Read<Rectangle>("SettingsDanger");

            // Assert — under the last row, inside what scrolls, and clear of the danger zone at rest.
            Assert.True(captionTop >= last.Bottom,
                        $"the switches' caption starts at y={captionTop}, inside the last switch's row {last} at {percent}%");
            Assert.True(captionBottom <= rowsBottom,
                        $"the caption ends at y={captionBottom}, past the rows' scroll range (bottom {rowsBottom}) at {percent}%");
            if (!scrolls)
                Assert.True(captionBottom <= danger.Y,
                            $"the caption ends at y={captionBottom}, inside the DANGER ZONE {danger} at {percent}%");
        }
        finally { UiMetrics.Apply(100); }
    }

    [Fact]
    public void test_settings_switch_count_matches_the_switch_table()
    {
        // Arrange — the table is an instance method (its switches close over the host's fields), so it
        // is counted in the source, as the other host-shape tests here read Game1.
        var source = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs"));

        // Act
        var rows = System.Text.RegularExpressions.Regex.Matches(source, @"new SettingsSwitch\(").Count;

        // Assert
        Assert.Equal(Read<int>("SettingsSwitchCount"), rows);
    }

    [Fact]
    public void test_settings_scroll_resets_on_close_before_draw_and_on_a_scale_change()
    {
        // Arrange — the panel's rows began to scroll at 150 % with the GUIDANCE caption, which made two
        // old paths live (alpha review, 2026-09-17): the "closed modal is back at its top" reset sat in
        // Draw's in-game branch, which the title's Draw returns before, and a UI SCALE click kept the
        // old offset for the frame it changed the layout under.
        var source = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");
        const string reset = "if (!_showSettings) _settingsScroll = 0;";
        var update = source.IndexOf("protected override void Update(GameTime gameTime)", StringComparison.Ordinal);
        var draw = source.IndexOf("protected override void Draw(GameTime gameTime)", StringComparison.Ordinal);

        // Act
        var at = source.IndexOf(reset, StringComparison.Ordinal);
        var scaleClick = source.IndexOf("CycleUiScale();\n            SaveDisplay();", StringComparison.Ordinal);

        // Assert — one reset, and it is in the frame's first half, which every screen runs.
        Assert.True(update >= 0 && draw >= 0, "Game1's Update/Draw signatures moved — re-anchor this test.");
        Assert.Equal(at, source.LastIndexOf(reset, StringComparison.Ordinal));
        Assert.True(at > update && at < update + 3000, "the settings scroll reset is not at the top of Update.");
        Assert.True(scaleClick >= 0, "the UI SCALE click moved — re-anchor this test.");
        Assert.Contains("_settingsScroll = 0;", source.Substring(scaleClick, 400), StringComparison.Ordinal);
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(string.Join('/', parts) + " not found above the test binary.");
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
            // Read from the grid, not re-typed: until 2026-09-16 this test held its own copy of the
            // draw's four literals, which pinned the copy and not the draw. NavTileBarTests holds the
            // bar against the label, the icon and the tile's edges; this is only the foot band.
            var first = (Rectangle)rect.Invoke(null, new object[] { 0 })!;
            var bar = NavTileGrid.Of(first).HealthBar;
            Assert.True(bar.Bottom <= first.Bottom,
                        $"the HUNT tile's health bar runs past its own foot at {percent}%");
            Assert.True(bar.Y >= first.Bottom - foot,
                        $"the HUNT tile's health bar at {percent}% is above the {foot} px foot band and would cross the label");
            Assert.True(bar.Width >= 4 && bar.Height >= 3,
                        "the bar is narrower or lower than UiKit.Bar will draw");
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
    /// THE CHROME ROW IS A CHAIN AND NOTHING IN IT SITS ON TOP OF ITS NEIGHBOUR. Gear, ?, envelope,
    /// then the currency capsules — right to left, each taking its own room. The first cut of the ?
    /// was hung off the gear at the capsules' y and drew a ? through the MATERIALS pill; the envelope
    /// is the third link and the same mistake is one line away, at three densities.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_chrome_row_is_a_chain_with_no_shared_pixel(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            var gear = Read<Rectangle>("SettingsGear");
            var learn = Read<Rectangle>("LearnButton");
            var mail = Read<Rectangle>("DispatchButton");
            var pillRight = Read<int>("PillRowRight");
            var page = UiKit.Page;

            foreach (var (name, r) in new[] { ("SettingsGear", gear), ("LearnButton", learn), ("DispatchButton", mail) })
            {
                Assert.True(page.Contains(r), $"{name} {r} leaves the page at {percent}%");
                Assert.True(r.Width >= UiMetrics.HitTargetMinimum && r.Height >= UiMetrics.HitTargetMinimum,
                            $"{name} is {r.Width}x{r.Height} at {percent}%, under the {UiMetrics.HitTargetMinimum} px hit floor");
                Assert.True(r.X >= pillRight + 16,
                            $"{name} {r} reaches into the currency row, which ends at {pillRight} at {percent}%");
            }

            Assert.False(gear.Intersects(learn), $"the gear {gear} and the ? {learn} share a pixel at {percent}%");
            Assert.False(learn.Intersects(mail), $"the ? {learn} and the envelope {mail} share a pixel at {percent}%");
            Assert.False(gear.Intersects(mail), $"the gear {gear} and the envelope {mail} share a pixel at {percent}%");

            // Right to left: gear, ?, envelope. The order is the one-line change PillRowRight follows.
            Assert.True(learn.Right <= gear.X, $"the ? {learn} is not left of the gear {gear} at {percent}%");
            Assert.True(mail.Right <= learn.X, $"the envelope {mail} is not left of the ? {learn} at {percent}%");
            // All three stand on the capsules' centre line, so the row reads as one row.
            Assert.Equal(learn.Center.Y, mail.Center.Y);
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>
    /// THE DISPATCHES PANEL IS REACHABLE AT EVERY PROFILE — the close icon, the list of letters and
    /// MARK ALL READ. A modal whose close has left the page, or whose footer has grown over its own
    /// rows, is a surface the player cannot get out of or cannot finish with.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_dispatches_panel_keeps_its_close_list_and_mark_all_read_reachable(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            var panel = Read<Rectangle>("DispatchesPanel");
            var close = Read<Rectangle>("DispatchesCornerClose");
            var list = Read<Rectangle>("DispatchesListView");
            var pane = Read<Rectangle>("DispatchesPane");
            var mark = Read<Rectangle>("DispatchesMarkAll");

            Assert.True(UiKit.Page.Contains(panel), $"the DISPATCHES panel {panel} leaves the page at {percent}%");
            foreach (var (name, r) in new[] { ("the close icon", close), ("the list", list), ("the reading pane", pane), ("MARK ALL READ", mark) })
                Assert.True(panel.Contains(r), $"{name} {r} left the DISPATCHES panel {panel} at {percent}%");

            Assert.True(close.Height >= UiMetrics.HitTargetMinimum,
                        $"the close icon is {close.Height} px at {percent}%, under the hit floor");
            Assert.True(mark.Height >= UiMetrics.HitTargetMinimum,
                        $"MARK ALL READ is {mark.Height} px tall at {percent}%, under the hit floor");
            Assert.True(UiMetrics.RowHeight >= UiMetrics.HitTargetMinimum,
                        $"a letter's row is {UiMetrics.RowHeight} px at {percent}%, under the hit floor");

            Assert.False(list.Intersects(pane), $"the list {list} prints through the reading pane {pane} at {percent}%");
            Assert.False(list.Intersects(mark), $"the list {list} runs under MARK ALL READ {mark} at {percent}%");
            Assert.False(pane.Intersects(mark), $"the reading pane {pane} runs under MARK ALL READ {mark} at {percent}%");
            Assert.False(list.Intersects(close), $"the list {list} is under the close icon {close} at {percent}%");
            Assert.True(list.Right <= pane.X, $"the list {list} is not left of the pane {pane} at {percent}%");

            // The list is worth scrolling: at least four letters stand in it at every profile.
            Assert.True(list.Height / UiMetrics.RowHeight >= 4,
                        $"only {list.Height / UiMetrics.RowHeight} letters fit in the list at {percent}%");
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
