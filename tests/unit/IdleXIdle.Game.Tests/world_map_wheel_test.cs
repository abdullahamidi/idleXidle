using System;
using System.IO;
using System.Linq;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE HOST PASSES THE WHEEL TO THE WORLD MAP, as it does to every other inset screen: the map's
/// inspector paints a scrollbar and its Update takes a wheel, and for a while the one-line hand-off in
/// Game1 was the only thing missing.
/// </summary>
public class WorldMapWheelTest
{
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

    [Fact]
    public void test_the_host_passes_the_wheel_to_the_map_inspector()
    {
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");
        Assert.Contains("_mapScreen.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked, MouseWheel);", game, StringComparison.Ordinal);
        Assert.DoesNotContain("_mapScreen.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked);", game, StringComparison.Ordinal);
    }
}
