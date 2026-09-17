using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// Every prologue beat stands on its own illustration, and no environment plate ever stands in.
/// </summary>
/// <remarks>
/// The prologue borrowed the constellation, the region map and three arenas until 2026-09-16, and the
/// method that chose them said so in its own comment ("bespoke prologue art is owed"). These tests
/// are what keeps the debt paid: a beat without a plate, a plate that is not on disk at the shipped
/// size, or a <c>bg_*</c> key reaching back into the prologue fails here rather than on a player's
/// screen.
/// </remarks>
public class PrologueArtTest
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

    private static (int Width, int Height) PngSize(string path)
    {
        // IHDR: width and height are the big-endian ints at bytes 16..24. No GraphicsDevice needed.
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 24 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G', $"{path} is not a PNG");
        int Be(int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
        return (Be(16), Be(20));
    }

    [Fact]
    public void test_every_beat_has_its_own_plate_on_disk_at_the_shipped_size()
    {
        var keys = OpeningScript.Prologue.Select(PrologueArt.For).ToList();
        Assert.Equal(OpeningScript.Prologue.Count, keys.Distinct(StringComparer.Ordinal).Count());
        foreach (var (beat, key) in OpeningScript.Prologue.Zip(keys))
        {
            Assert.StartsWith(PrologueArt.Prefix, key, StringComparison.Ordinal);
            Assert.EndsWith(beat.Id, key, StringComparison.Ordinal);
            Assert.False(key.StartsWith("bg_", StringComparison.Ordinal), $"{beat.Id} reaches for an environment plate");
            var png = RepoFile("assets", "art", "Environments", "prologue", key + ".png");
            Assert.Equal((1920, 1080), PngSize(png));
        }
    }

    [Fact]
    public void test_the_number_of_plates_follows_the_script()
    {
        // The catalogue is exactly the script's beats — no orphan plate, no beat without one.
        var wanted = OpeningScript.Prologue.Select(PrologueArt.For).OrderBy(k => k, StringComparer.Ordinal);
        var carried = PrologueArt.All.OrderBy(k => k, StringComparer.Ordinal);
        Assert.Equal(wanted, carried);
        Assert.Equal(OpeningScript.Prologue.Count, OpeningScript.Prologue.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void test_no_beat_falls_back_to_an_environment_plate()
    {
        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        Assert.Contains("PrologueArt.For(", host, StringComparison.Ordinal);
        foreach (var old in new[] { "bg_constellation", "bg_regionmap", "bg_arena_shadow", "bg_title", "bg_arena_body", "bg_arena_nature" })
            Assert.False(host.Contains($"\"{old}\"", StringComparison.Ordinal), $"Game1.Opening.cs still names {old}");
        Assert.DoesNotContain("art is owed", host, StringComparison.OrdinalIgnoreCase);

        var catalogue = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "PrologueArt.cs"));
        Assert.DoesNotContain("\"bg_", catalogue, StringComparison.Ordinal);

        // And the alias table does not quietly point a plate key at older art.
        var library = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "AssetLibrary.cs"));
        Assert.DoesNotContain(PrologueArt.Prefix, library, StringComparison.Ordinal);
    }
}
