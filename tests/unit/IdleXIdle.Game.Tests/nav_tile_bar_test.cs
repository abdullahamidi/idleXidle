using System.Reflection;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE HUNT TILE'S LIVE BAR, held against the rest of its tile at every density profile. The rail is
/// canvas geometry (180 wide, 98 per tile, at every UI SCALE) while the label's rung grows with the
/// profile, so the one place the bar can go wrong is the seam between the two: printing through the
/// label's foot at 150 %, or off the tile's own foot. The bar is a thin 4 px thing in a 10 px band
/// under a word — exactly the kind of state a capture can show and nothing could assert until the
/// rectangle was a member of <see cref="NavTileGrid"/> rather than four literals inside the draw.
/// </summary>
/// <remarks>
/// The arithmetic is here; what it LOOKS like — the green fill against the red wash, the bar under
/// Reduced Motion, the length against the health the fight was drained to — is in
/// <c>production/qa/evidence/visual-pass/hunttile/</c>, which is what the two rig dials pinned at the
/// end of this file exist to photograph.
/// </remarks>
public class NavTileBarTests
{
    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>The HUNT tile: the rail's first slot, from Game1's own tile rule.</summary>
    private static Rectangle HuntTile()
    {
        var rect = typeof(Game1).GetMethod("NavHexRect", Statics)
                   ?? throw new Xunit.Sdk.XunitException("Game1 has no static NavHexRect — the rail was renamed without its test.");
        return (Rectangle)rect.Invoke(null, new object[] { 0 })!;
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

    private static string Source(params string[] parts) => File.ReadAllText(RepoFile(parts)).Replace("\r\n", "\n");

    /// <summary>The brace-matched block that opens after the END of <paramref name="anchor"/> (an anchor may hold a pattern's own braces).</summary>
    private static string BlockAfter(string source, string anchor)
    {
        var at = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(at >= 0, $"source no longer contains `{anchor}`.");
        var open = source.IndexOf('{', at + anchor.Length);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
        }
        throw new InvalidOperationException($"`{anchor}` never closes.");
    }

    /// <summary>
    /// THE BAR IS IN THE FOOT BAND AND NOWHERE ELSE: under the label's line box, on the tile, clear of
    /// the icon, inside the label's own side insets, and at least what <c>UiKit.Bar</c> will draw (it
    /// returns without drawing under 4 wide or 3 tall).
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_hunt_tile_bar_sits_in_the_foot_band_clear_of_the_label_the_icon_and_the_edges(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            // Arrange
            var tile = HuntTile();
            var grid = NavTileGrid.Of(tile);

            // Act
            var bar = grid.HealthBar;

            // Assert
            Assert.True(bar.Top >= grid.LabelTop + grid.LabelHeight,
                        $"the bar {bar} starts inside the label's line box (top {grid.LabelTop}, rung {grid.LabelHeight}) at {percent}%");
            Assert.True(bar.Bottom <= tile.Bottom, $"the bar {bar} runs past the tile's foot {tile.Bottom} at {percent}%");
            Assert.True(bar.Height >= 3 && bar.Width >= 4, $"the bar {bar} is under what UiKit.Bar will draw at {percent}%");
            Assert.False(bar.Intersects(grid.Icon), $"the bar {bar} prints on the icon {grid.Icon} at {percent}%");
            Assert.True(bar.X >= tile.X + NavTileGrid.LabelInset,
                        $"the bar {bar} starts inside the rail's seam at {percent}%");
            Assert.True(bar.Right <= tile.Right - 3, $"the bar {bar} reaches the tile's right edge at {percent}%");
            // The NEW dot and the count badge both sit ABOVE the label's line box (each is clamped to
            // labelY - IconGap - its own height), so a bar under the label cannot meet either.
            Assert.True(grid.LabelTop - NavTileGrid.IconGap <= bar.Top,
                        $"the band the dot and the badge are clamped to reaches the bar at {percent}%");
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>
    /// THE DRAW READS THE GRID. The bar's rectangle was four literals in <c>DrawHexNav</c> and a second
    /// copy of them in the chrome test — the test pinned its own copy and could not see the draw move.
    /// The HUNT block asks the grid for its bar and types no foot offset of its own.
    /// </summary>
    [Fact]
    public void test_draw_hex_nav_reads_the_bar_from_the_grid_and_types_no_literal()
    {
        var game1 = Source("src", "IdleXIdle.Game", "Game1.cs");
        var hunt = BlockAfter(game1, "if (activity == Activity.Hunt && !on)");

        Assert.Contains(".HealthBar", hunt);
        Assert.DoesNotContain("- 7,", hunt);
        Assert.DoesNotContain("NavRailWidth - 52", hunt);
        Assert.Contains("_ui.Bar(_batch, bar.X, bar.Y, bar.Width, bar.Height, life,", hunt);
        // ...and the grid's bar is the geometry the draw used to type — a move, not a redesign.
        Assert.Contains("public Rectangle HealthBar => new(Tile.X + 26, Tile.Bottom - 7, Tile.Width - 52, 4);",
                        Source("src", "IdleXIdle.Game", "NavTileGrid.cs"));
    }

    /// <summary>
    /// THE TWO RIG DIALS THAT PHOTOGRAPH THIS TILE exist, keep the live ticks' gates, and are forwarded
    /// and documented by capture.sh — a dial the script does not forward photographs the default pose,
    /// and one its header does not name is a dial nobody can find.
    /// </summary>
    [Fact]
    public void test_the_hunt_tile_poses_keep_the_live_gates_and_capture_sh_forwards_and_documents_them()
    {
        var game1 = Source("src", "IdleXIdle.Game", "Game1.cs");

        // The wash pose: the break's gate (a capture photographs the game's own answer under an owner),
        // and for `hurt` the arm's own two — RED FLASH on, Reduced Motion off.
        var pose = BlockAfter(game1, "&& Environment.GetEnvironmentVariable(\"RH_SHOT_HUNTTILE\") is { Length: > 0 } huntTileSpec)");
        Assert.Contains("if (CaptureRig && !AttentionOwnedAbove(AttentionOwner.Coach)\n"
                        + "            && Environment.GetEnvironmentVariable(\"RH_SHOT_HUNTTILE\")", game1, StringComparison.Ordinal);
        Assert.Contains("if (_showScreenFlash && !UiMotion.Reduced) UiMotion.PoseFlash(NavHuntHurtKey, part, UiMotion.Fast);", pose);
        Assert.Contains("UiMotion.PoseFlash(NavHuntClearKey, part, boss ? UiMotion.Reward : UiMotion.Transition);", pose);
        Assert.Contains("_navHuntBoss = boss;", pose);
        Assert.Contains("Known: hurt, cleared, boss.", pose);

        // The drain: the fight fixture's own three lines, for the menus.
        var drain = BlockAfter(game1, "System.Globalization.CultureInfo.InvariantCulture, out var huntT))");
        Assert.Contains("_shotEnemyBaseline = (1400f, 9f);", drain);
        Assert.Contains("_expedition.DevStart(_hunter, 1400f, 9f);", drain);
        Assert.Contains("_expedition.DevSeek(huntT);", drain);

        var capture = Source("tools", "asset-pipeline", "capture.sh");
        var header = string.Join('\n', capture.Split('\n').Skip(1).TakeWhile(l => l.StartsWith('#')));
        Assert.Contains("RH_SHOT_HUNTTILE", header);
        Assert.Contains("RH_SHOT_HUNT_T", header);
        Assert.Contains("RH_ENV+=(RH_SHOT_HUNTTILE=\"$RH_SHOT_HUNTTILE\")", capture);
        Assert.Contains("RH_ENV+=(RH_SHOT_HUNT_T=\"$RH_SHOT_HUNT_T\")", capture);
    }
}
