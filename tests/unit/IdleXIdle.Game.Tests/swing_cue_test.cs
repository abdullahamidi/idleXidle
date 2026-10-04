using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using IdleXIdle.Game;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE BASIC ATTACKS' IDENTITY CUES (the remaining-skill sweep, design.md section 7 "Basic-attack hit family", Phase 1 /
/// P1.6): ten swing hits and three releases, each in its champion's own material, built once from CC0 recorded foley by
/// <c>tools/asset-pipeline/make_swing_cues.py</c>, ONE build each, and pinned here by SHA-256 as SHIPPED (a change is a
/// deliberate commit, never a regeneration; no sweep cue claims HUMAN-APPROVED before the owner has heard the film). Every
/// SwingRecipe's chain names its identity key first, so with the files present each champion is heard through its own
/// file, its archetype second. <c>sfx_seeker_swing_hit</c> is the Seeker's basic attack, not a closed reference: it
/// matches none of the reference voices the action regression compares.
/// </summary>
public class swing_cue_test
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Wav(string key) => RepoFile("assets", "audio", "combat", key + ".wav");

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>The SHIPPED bytes (make_swing_cues.py, 2026-10-04), in the design's order.</summary>
    private static readonly IReadOnlyDictionary<string, string> Shipped = new Dictionary<string, string>
    {
        ["sfx_seeker_swing_hit"] = "55fd7dead9a618d65077ff89f5e54d97f141db3f0e60b26126d3084aa8d30f42",
        ["sfx_anvil_swing_hit"] = "4d31dc722568ade769d69e25790395fbeef50c0695521ec0d30f9e388703f067",
        ["sfx_metronome_swing_hit"] = "054182c7df7cc61715b48ecdcc7442c0aa03bae328e08be644367127627b8306",
        ["sfx_tower_swing_hit"] = "75e4d3d201ca46868ad8cf25b5298e9ffc8d7e4806b8ad3af09cf3bfe821c200",
        ["sfx_thornwall_swing_hit"] = "55f9a13772acbb09ee2dfcb1ee98731108d2b82c3c807a6e0a6b1e1920cc3cb4",
        ["sfx_magpie_swing_hit"] = "9e21b691c65974c56f418e379056ab226cebdb341ed3a7afb2c00a9719fd5cf5",
        ["sfx_quiver_swing_hit"] = "fdded4685faa2f364fffd816e9088784d642e28a67b4c6d3d72278386803ea79",
        ["sfx_quiver_loose"] = "4c0971eb8c33201278b818f4b0f353e258c14c4336dd04dcb489fd1a15f8fc47",
        ["sfx_chorus_swing_hit"] = "d280d329e740056389fa1d644eef34a08a0c2645c5feabf0a1ae31be80fc1125",
        ["sfx_chorus_toss"] = "9056d45e6a481859ff61678a2bed1fdd14bde85ed7efe937e06d1efed12b3a4a",
        ["sfx_unbroken_swing_hit"] = "ee76c722339c9f159c5f0b4b05f1bdd3c40aebc1a86dba76f8b1b32b497eb1e9",
        ["sfx_unbroken_toss"] = "8f6a0dfce1973d93bf0706f9324e19df2c02d8c618b092d44796fb0e516890c4",
        ["sfx_oathbound_swing_hit"] = "1d4cd77bd565b4977babe3e68df23a30a5b87d09c4b927fd2145661632fb92bf",
    };

    /// <summary>The three releases, and the champion whose missile each voices.</summary>
    private static readonly IReadOnlyDictionary<string, string> Releases = new Dictionary<string, string>
    {
        ["quiver"] = "sfx_quiver_loose",
        ["chorus"] = "sfx_chorus_toss",
        ["unbroken"] = "sfx_unbroken_toss",
    };

    private static void AssertShipped(string key)
    {
        var path = Wav(key);
        Assert.True(File.Exists(path), $"{key}.wav is missing");
        Assert.True(Shipped[key] == Sha(path),
            $"{key}.wav changed: a SHIPPED identity cue is changed by a deliberate commit that updates this pin, never by a regeneration.");
    }

    [Fact] public void test_sfx_seeker_swing_hit_is_shipped() => AssertShipped("sfx_seeker_swing_hit");
    [Fact] public void test_sfx_anvil_swing_hit_is_shipped() => AssertShipped("sfx_anvil_swing_hit");
    [Fact] public void test_sfx_metronome_swing_hit_is_shipped() => AssertShipped("sfx_metronome_swing_hit");
    [Fact] public void test_sfx_tower_swing_hit_is_shipped() => AssertShipped("sfx_tower_swing_hit");
    [Fact] public void test_sfx_thornwall_swing_hit_is_shipped() => AssertShipped("sfx_thornwall_swing_hit");
    [Fact] public void test_sfx_magpie_swing_hit_is_shipped() => AssertShipped("sfx_magpie_swing_hit");
    [Fact] public void test_sfx_quiver_swing_hit_is_shipped() => AssertShipped("sfx_quiver_swing_hit");
    [Fact] public void test_sfx_quiver_loose_is_shipped() => AssertShipped("sfx_quiver_loose");
    [Fact] public void test_sfx_chorus_swing_hit_is_shipped() => AssertShipped("sfx_chorus_swing_hit");
    [Fact] public void test_sfx_chorus_toss_is_shipped() => AssertShipped("sfx_chorus_toss");
    [Fact] public void test_sfx_unbroken_swing_hit_is_shipped() => AssertShipped("sfx_unbroken_swing_hit");
    [Fact] public void test_sfx_unbroken_toss_is_shipped() => AssertShipped("sfx_unbroken_toss");
    [Fact] public void test_sfx_oathbound_swing_hit_is_shipped() => AssertShipped("sfx_oathbound_swing_hit");

    [Fact]
    public void test_every_swing_chain_resolves_to_its_own_champions_file_first()
    {
        Assert.Equal(10, SwingRecipes.All.Count);
        foreach (var (id, r) in SwingRecipes.All)
        {
            // the contact: the champion's own swing hit, its archetype second, the generic last
            Assert.Equal($"sfx_{id}_swing_hit", r.ContactCues[0]);
            Assert.Equal(r.ContactCues[0], archetype_cue_test.Resolve(r.ContactCues));
            Assert.True(ArchetypeCues.Find(r.ContactCues[1]) is { Moment: ArchetypeMoment.Hit }, $"{id}: {r.ContactCues[1]}");
            Assert.Equal("sfx_hit", r.ContactCues[^1]);
            if (!Releases.TryGetValue(id, out var release))
            {
                Assert.Empty(r.ReleaseCues);
                continue;
            }
            // the release: the champion's own, the throw archetype second
            Assert.Equal(release, r.ReleaseCues[0]);
            Assert.Equal(release, archetype_cue_test.Resolve(r.ReleaseCues));
            Assert.Equal(ArchetypeCues.ThrowRelease, r.ReleaseCues[1]);
        }
        // the thirteen recipe keys ARE the pinned thirteen
        var keys = SwingRecipes.All.Values.Select(r => r.ContactCues[0])
            .Concat(SwingRecipes.All.Values.Where(r => r.ReleaseCues.Count > 0).Select(r => r.ReleaseCues[0]));
        Assert.Equal(Shipped.Keys.OrderBy(k => k, StringComparer.Ordinal), keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void test_no_identity_key_is_a_closed_reference_voice()
    {
        // action_regression compares the five references' voices by prefix; the Seeker's basic attack is not one of them,
        // and the prefixes are exactly the five (not widened to take it in)
        var regression = File.ReadAllText(RepoFile("tools", "asset-pipeline", "action_regression.py"));
        Assert.Contains("VOICES = (\"sfx_seeker_spray\", \"sfx_seeker_hard_hands\")", regression);
        Assert.Contains("REF_VOICES = VOICES + (\"sfx_seeker_jaws\", \"sfx_seeker_press\", \"sfx_seeker_brand\")", regression);
        var references = new[] { "sfx_seeker_spray", "sfx_seeker_hard_hands", "sfx_seeker_jaws", "sfx_seeker_press", "sfx_seeker_brand" };
        foreach (var key in Shipped.Keys)
            Assert.DoesNotContain(references, p => key.StartsWith(p, StringComparison.Ordinal));
        Assert.DoesNotContain(Shipped.Keys, k => ArchetypeCues.Find(k) is not null);
    }

    [Fact]
    public void test_each_cue_is_short_dry_and_mastered_in_its_band()
    {
        using var levels = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "swing_cue_levels.json")));
        Assert.True(levels.RootElement.GetProperty("all_pass").GetBoolean(), "a swing cue failed its measurement");
        foreach (var (key, sha) in Shipped)
        {
            var row = levels.RootElement.GetProperty("cues").GetProperty(key);
            Assert.Equal(sha, row.GetProperty("sha256").GetString());
            Assert.True(row.GetProperty("pass").GetBoolean(), $"{key} failed");
            var release = Releases.Values.Contains(key);
            // the file's own length (16-bit mono 44.1 kHz): a hit <= 0.25 s, a release 0.16-0.18 s; no reverb tail
            var seconds = (new FileInfo(Wav(key)).Length - 44) / 2.0 / 44100.0;
            if (release) Assert.InRange(seconds, 0.16, 0.181);
            else Assert.InRange(seconds, 0.05, 0.25);
            Assert.True(row.GetProperty("tail_db").GetDouble() <= -30.0, $"{key} rings on");
            // mastered at the volume its recipe plays it at
            var v = row.GetProperty("volume").GetSingle();
            if (release) Assert.InRange(v, 0.16f, 0.18f);
            else Assert.Equal(0.36f, v);
            // every ceiling (SPRAY's and HARD HANDS' contacts, SPRAY's release) at least 3 dB above, K-weighted
            foreach (var c in row.GetProperty("ceilings").EnumerateArray())
                Assert.True(c.GetProperty("under_db").GetDouble() >= 3.0, $"{key} within 3 dB of {c.GetProperty("ref").GetString()}");
            // a hit earns its place: its material sits apart from the archetype it replaces
            if (!release) Assert.True(row.GetProperty("distance_db").GetDouble() >= 3.0, $"{key} is its archetype again");
        }
        // the volumes the levels were mastered at are the recipes' own
        foreach (var (id, r) in SwingRecipes.All)
        {
            Assert.Equal(r.ContactVolume, levels.RootElement.GetProperty("cues").GetProperty(r.ContactCues[0]).GetProperty("volume").GetSingle());
            if (r.ReleaseCues.Count > 0)
                Assert.Equal(r.ReleaseVolume, levels.RootElement.GetProperty("cues").GetProperty(r.ReleaseCues[0]).GetProperty("volume").GetSingle());
        }
    }

    [Fact]
    public void test_every_excerpt_is_cc0_listed_and_none_is_an_archetypes_recording()
    {
        var foley = RepoFile("tools", "asset-pipeline", "foley", "swing_hits");
        var sources = File.ReadAllText(Path.Combine(foley, "SOURCES.md"));
        Assert.Contains("CC0 1.0", sources);
        Assert.Contains("creativecommons.org/publicdomain/zero/1.0/", sources);
        var excerpts = Directory.GetFiles(foley, "*.wav").Select(Path.GetFileName).ToList();
        Assert.NotEmpty(excerpts);
        foreach (var name in excerpts)
        {
            var row = sources.Split('\n').FirstOrDefault(l => l.StartsWith($"| `{name}`", StringComparison.Ordinal));
            Assert.True(row is not null, $"{name} has no SOURCES.md row");
            Assert.Contains("| CC0 1.0 |", row);
            Assert.Contains("https://freesound.org/people/", row);
            Assert.Contains("`sfx_", row);
        }
        Assert.Empty(Directory.GetFiles(foley).Where(f => f.EndsWith(".mp3") || f.EndsWith(".flac") || f.EndsWith(".ogg")));
        foreach (var key in Shipped.Keys)
            Assert.Contains($"`{key}`", sources);
        // the manifest names these bytes, and no recording is one an archetype is made of
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(foley, "swing_manifest.json")));
        using var archetypes = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "foley", "archetypes", "archetype_manifest.json")));
        Assert.StartsWith("SHIPPED", manifest.RootElement.GetProperty("pin_level").GetString());
        foreach (var (key, sha) in Shipped)
            Assert.Equal(sha, manifest.RootElement.GetProperty("cues").GetProperty(key).GetProperty("sha256").GetString());
        var archetypePages = archetypes.RootElement.GetProperty("excerpts").EnumerateArray()
            .Select(e => e.GetProperty("freesound_page").GetString()).ToHashSet();
        foreach (var e in manifest.RootElement.GetProperty("excerpts").EnumerateArray())
            Assert.DoesNotContain(e.GetProperty("freesound_page").GetString(), archetypePages);
    }

    [Fact]
    public void test_no_candidate_or_selector_exists()
    {
        var audio = Directory.GetFiles(RepoFile("assets", "audio"), "*.wav", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension).ToList();
        foreach (var key in Shipped.Keys)
            Assert.Equal(new[] { key }, audio.Where(n => n!.StartsWith(key, StringComparison.Ordinal)));
        Assert.False(Directory.Exists(RepoFile("tools", "asset-pipeline", "audio_history", "swing_hits")));
        var py = File.ReadAllText(RepoFile("tools", "asset-pipeline", "make_swing_cues.py"));
        foreach (var word in new[] { "APPROVED", "BUILDS", "HISTORY", "candidate(", "ARCHIVE" })
            Assert.DoesNotContain(word, py);
        var src = Directory.GetFiles(RepoFile("src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText);
        foreach (var text in src)
        {
            Assert.DoesNotContain("RH_SWING_CUE", text);
            foreach (var key in Shipped.Keys)
                Assert.DoesNotContain(key + "_", text);
        }
    }

    [Fact]
    public void test_the_thirteen_are_throttled_at_60()
    {
        var table = File.ReadAllText(RepoFile("tools", "asset-pipeline", "sound_throttle.json"));
        foreach (var key in Shipped.Keys)
        {
            Assert.Equal(60, SoundBank.ThrottleTable[key]);
            Assert.DoesNotContain(key, SoundBank.Unthrottled);
            Assert.Contains($"\"{key}\": 60", table);
        }
        // the archetype releases keep the default; the throw release is not given the swing's row
        Assert.False(SoundBank.ThrottleTable.ContainsKey(ArchetypeCues.ThrowRelease));
    }
}
