using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The arena's cast is chosen by REGION and ARCHETYPE: twenty-four ordinary creatures, six bosses, one
/// catalogue, and the art for every row on disk.
/// </summary>
/// <remarks>
/// Until 2026-09-16 the arena drew one body per Source and told the four roles apart only by scale,
/// and the name on a creature was its art file's name. These tests keep the replacement honest: every
/// cell resolves to its own body, the files are the shipped strip shape, the generation recipe in
/// <c>spec.json</c> names the same rows, and the arena reads the catalogue rather than a Source table.
/// Presentation only: the catalogue must never reach for a combat number.
/// </remarks>
public class EnemyPresentationTest
{
    private static readonly Archetype[] Roles = Enum.GetValues<Archetype>();

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

    private static string RepoRoot()
    {
        var marker = RepoFile("src", "IdleXIdle.Game", "EnemyPresentation.cs");
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(marker)!, "..", ".."));
    }

    private static (int Width, int Height) PngSize(string path)
    {
        // IHDR: width and height are the big-endian ints at bytes 16..24. No GraphicsDevice needed.
        var bytes = new byte[24];
        using (var stream = File.OpenRead(path)) Assert.Equal(24, stream.Read(bytes, 0, 24));
        Assert.True(bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G', $"{path} is not a PNG");
        int Be(int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
        return (Be(16), Be(20));
    }

    [Fact]
    public void test_every_region_and_role_resolves_to_its_own_look()
    {
        Assert.Equal(Regions.All.Count * Roles.Length, EnemyPresentation.Normals.Count);
        foreach (var region in Regions.All)
            foreach (var role in Roles)
            {
                var look = EnemyPresentation.For(region.Id, role);
                Assert.Equal(region.Id, look.RegionId);
                Assert.Equal(role, look.Archetype);
                Assert.EndsWith("_" + role.ToString().ToLowerInvariant(), look.ArtKey, StringComparison.Ordinal);
            }

        var keys = EnemyPresentation.Normals.Select(l => l.ArtKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        var names = EnemyPresentation.Normals.Select(l => l.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var name in names)
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.Equal(name.ToUpperInvariant(), name);
            Assert.True(name.Length <= 16, $"'{name}' is not a short name");
        }
    }

    [Fact]
    public void test_a_region_draws_one_family_and_a_new_region_draws_another()
    {
        var families = Regions.All.ToDictionary(
            r => r.Id,
            r => Roles.Select(a => EnemyPresentation.For(r.Id, a).ArtKey).ToHashSet(StringComparer.Ordinal));
        foreach (var (region, keys) in families)
        {
            // One slug per region: every key in a family shares the head before the role.
            var heads = keys.Select(k => k[..k.LastIndexOf('_')]).Distinct(StringComparer.Ordinal).ToList();
            Assert.Single(heads);
            foreach (var (other, otherKeys) in families)
                if (other != region)
                    Assert.Empty(keys.Intersect(otherKeys));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no_such_region")]
    public void test_an_unknown_region_draws_the_starting_family(string? region)
    {
        Assert.Equal(Regions.All[0].Id, EnemyPresentation.StartingRegion);
        foreach (var role in Roles)
            Assert.Same(EnemyPresentation.For(EnemyPresentation.StartingRegion, role), EnemyPresentation.For(region, role));
    }

    [Fact]
    public void test_every_region_has_its_boss_and_the_rig_can_pin_one_by_key()
    {
        foreach (var region in Regions.All)
        {
            var boss = EnemyPresentation.BossFor(region.Id);
            Assert.NotNull(boss);
            Assert.Equal(region.Id, boss!.RegionId);
            Assert.Same(boss, EnemyPresentation.BossByArtKey(boss.ArtKey));
            Assert.Equal(boss.Name.ToUpperInvariant(), boss.Name);
        }
        Assert.Equal(Regions.All.Count, EnemyPresentation.Bosses.Select(b => b.ArtKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Null(EnemyPresentation.BossFor("no_such_region"));
        Assert.NotNull(EnemyPresentation.BossByArtKey("crystal_lich"));   // the F6 / RH_SHOT boss pose
    }

    [Fact]
    public void test_the_source_pose_picks_the_family_of_the_region_whose_theme_it_is()
    {
        foreach (var region in Regions.All)
            Assert.Equal(region.Id, EnemyPresentation.RegionOfTheme(region.Theme));
        Assert.Equal(Enum.GetValues<Source>().Length, Regions.All.Select(r => r.Theme).Distinct().Count());
    }

    [Fact]
    public void test_every_look_is_on_disk_in_the_shipped_shape()
    {
        var root = RepoRoot();
        foreach (var look in EnemyPresentation.Normals)
        {
            foreach (var (clip, strip) in new[] { ("idle", look.IdleStrip), ("attack", look.AttackStrip), ("death", look.DeathStrip) })
            {
                var path = Path.Combine(root, "assets", "art", "Animations", "Enemies", $"{look.ArtKey}_{clip}", strip + ".png");
                Assert.True(File.Exists(path), $"{look.ArtKey}: {strip}.png is not filed");
                Assert.Equal((512 * 8, 512), PngSize(path));
            }
            foreach (var still in new[] { look.IdleStill, look.AttackStill })
            {
                var path = Path.Combine(root, "assets", "art", "Enemies", "enemies", look.ArtKey, still + ".png");
                Assert.True(File.Exists(path), $"{look.ArtKey}: {still}.png is not derived");
                Assert.Equal((512, 512), PngSize(path));
            }
        }
        foreach (var boss in EnemyPresentation.Bosses)
            foreach (var (clip, strip) in new[] { ("idle", boss.IdleStrip), ("attack", boss.AttackStrip), ("death", boss.DeathStrip) })
                Assert.Equal((512 * 8, 512), PngSize(Path.Combine(root, "assets", "art", "Animations", "Bosses", $"{boss.ArtKey}_{clip}", strip + ".png")));
    }

    [Fact]
    public void test_the_six_source_bodies_are_retired_with_nothing_left_pointing_at_them()
    {
        var root = RepoRoot();
        var old = new[] { "bonecrawler", "soul_leech", "wisp", "stone_sentinel", "shadeling", "rift_guardian" };
        var enemyDirs = Directory.GetDirectories(Path.Combine(root, "assets", "art", "Animations", "Enemies")).Select(Path.GetFileName).ToList();
        var keys = EnemyPresentation.Normals.Select(l => l.ArtKey).ToHashSet(StringComparer.Ordinal);
        foreach (var dir in enemyDirs)
            Assert.Contains(dir![..dir!.LastIndexOf('_')], keys);
        foreach (var body in old)
            Assert.False(Directory.Exists(Path.Combine(root, "assets", "art", "Enemies", "enemies", body)), $"{body}'s stills are still filed");

        var library = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "AssetLibrary.cs"));
        Assert.DoesNotContain("[\"crea_", library, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_generation_recipe_names_the_same_rows()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "spec.json")));
        var matrix = doc.RootElement.GetProperty("enemy_matrix");
        var items = matrix.GetProperty("items").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.Equal(
            EnemyPresentation.Normals.Select(l => l.ArtKey).OrderBy(k => k, StringComparer.Ordinal),
            items.Keys.OrderBy(k => k, StringComparer.Ordinal));
        var done = matrix.GetProperty("done");
        foreach (var look in EnemyPresentation.Normals)
        {
            var item = items[look.ArtKey];
            Assert.Equal(look.RegionId, item.GetProperty("region").GetString());
            Assert.Equal(look.Archetype.ToString(), item.GetProperty("archetype").GetString());
            Assert.Equal(look.Name, item.GetProperty("name").GetString());
            // Every filed row records the PixelLab ids it was assembled from, on one of the two routes.
            var ids = done.GetProperty(look.ArtKey);
            var character = ids.TryGetProperty("character_id", out _) && new[] { "idle", "attack", "death" }.All(c => ids.TryGetProperty(c, out _));
            var image = ids.TryGetProperty("image_job", out _) && new[] { "idle_job", "attack_job", "death_job" }.All(c => ids.TryGetProperty(c, out _));
            Assert.True(character || image, $"{look.ArtKey}: spec.json enemy_matrix.done does not record its clips");
        }
    }

    [Fact]
    public void test_the_arena_asks_the_catalogue_and_the_catalogue_asks_for_no_combat_number()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        foreach (var gone in new[] { "EnemyForSource", "PrettyName(", "BossForRegion", "BossNameFor" })
            Assert.DoesNotContain(gone, hunt, StringComparison.Ordinal);
        Assert.Contains("EnemyPresentation.For(ArtRegion, WaveArchetype)", hunt, StringComparison.Ordinal);
        Assert.Contains("EnemyPresentation.BossFor(RegionId)", hunt, StringComparison.Ordinal);
        Assert.Contains("_ui.TextCenterBig(b, look.Name,", hunt, StringComparison.Ordinal);
        // The lone creature's name and bar slide clear of the right rail, which is painted after the arena.
        Assert.Contains("THE PLATE KEEPS CLEAR OF THE RIGHT RAIL", hunt, StringComparison.Ordinal);
        Assert.Contains("var rail = _utilityPanel;", hunt, StringComparison.Ordinal);

        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs"));
        Assert.DoesNotContain("EnemyArtFor", host, StringComparison.Ordinal);

        // The catalogue's CODE (its doc comments may say whose numbers those are) names no combat type or dial.
        var catalogue = string.Join('\n', File.ReadAllLines(RepoFile("src", "IdleXIdle.Game", "EnemyPresentation.cs"))
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        foreach (var combat in new[] { "SoloBattle", "SoloExpedition", "Archetypes", "Health", "Damage", "Defence", "Tuning", "Baseline", "Random", "Expeditions" })
            Assert.DoesNotContain(combat, catalogue, StringComparison.Ordinal);

        // Core never learns what a creature looks like (a comment may say where that lives; code may not reach it).
        var core = Path.Combine(RepoRoot(), "src", "IdleXIdle.Core");
        foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            foreach (var line in File.ReadLines(file))
                if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    Assert.DoesNotContain("EnemyPresentation", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void test_a_creatures_source_is_said_by_the_strip_and_the_inspector_not_by_its_body()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        // The wave strip shows every Source the wave carries, from a list kept between frames.
        Assert.Contains("private readonly List<Source> _waveSources", hunt, StringComparison.Ordinal);
        Assert.Contains("foreach (var ws in _waveSources)", hunt, StringComparison.Ordinal);
        // The inspector wears the hovered creature's own Source, not the region's theme.
        Assert.Contains("var source = CreatureSource(slot);", hunt, StringComparison.Ordinal);
        Assert.Contains("_ui.Plate(b, plate, source is { } src ? SourceGlow(src) : null);", hunt, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString().ToLowerInvariant()}\") is { } ownGlyph", hunt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("assets/art/Animations/Enemies/verdant_swarm_idle/verdant_swarm_idle_strip8_512.png", true)]
    [InlineData("assets/art/Animations/Roster/anvil_cast/char_anvil_cast_strip8_512.png", true)]
    [InlineData("assets/art/Animations/Bosses/thorn_regent_idle/thorn_regent_idle_strip8_512.png", true)]
    [InlineData(@"assets\art\Enemies\enemies\verdant_swarm\verdant_swarm_idle_01.png", true)]
    [InlineData("assets/art/UI/icons/source_body.png", false)]
    [InlineData("assets/art/VFX/hit/fx_hit_strip8_512.png", false)]
    [InlineData("assets/art/VFX/anvil_mark/fx_anvil_mark_strip8_512.png", true)]
    [InlineData("assets/art/VFX/mark/fx_mark_strip8_512.png", false)]
    [InlineData("assets/art/VFX/traits/vfx_trait_burst.png", false)]
    [InlineData("assets/art/Environments/prologue/plate_prologue_tear.png", false)]
    public void test_only_the_one_at_a_time_families_wait_for_first_use(string path, bool deferred)
        => Assert.Equal(deferred, AssetLibrary.IsDeferred(path));

    [Fact]
    public void test_the_arena_warms_its_families_from_update_and_has_never_loads()
    {
        var library = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "AssetLibrary.cs")).Replace("\r\n", "\n");
        // Has answers from the index: it asks Present, never Resolve/Loaded (which decode a deferred key).
        // The body is read rather than matched character by character — RH_ASSET_TRACE wraps the key in a
        // Trace(...) call (2026-09-17), and the claim under test is which lookup it uses, not its spelling.
        var has = library.IndexOf("public bool Has(string key)", StringComparison.Ordinal);
        Assert.True(has > 0, "AssetLibrary.Has was renamed — re-anchor this test.");
        var body = library[has..library.IndexOf(';', has)];
        Assert.Contains("Present(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolve(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Loaded(", body, StringComparison.Ordinal);

        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs"));
        Assert.Contains("_expedition.WarmArt();", host, StringComparison.Ordinal);
        Assert.Contains("_assets.Warm(champion.StripKey(\"idle\"))", host, StringComparison.Ordinal);
        // WarmArt is an Update-side call; the Draw path never asks to warm.
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var draw = hunt.IndexOf("public void Draw(SpriteBatch b, Point mouse, string regionName, bool suppressBanner = false)", StringComparison.Ordinal);
        Assert.True(draw > 0);
        Assert.DoesNotContain(".Warm(", hunt[draw..], StringComparison.Ordinal);
    }
}
