using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE EIGHT ARCHETYPE CUES (the remaining-skill sweep, design.md section 7, Phase 1 / P1.5): built once from CC0 recorded
/// foley by <c>tools/asset-pipeline/make_archetypes.py</c>, ONE candidate each, and pinned here by SHA-256 as SHIPPED. That
/// is the design's lower pin level: a change is a deliberate commit, never a regeneration, and no sweep cue claims
/// HUMAN-APPROVED before the owner has heard the film. No candidate, no selector, and no Seeker key: the closed
/// references keep their own files, which resolve first in their chains.
/// </summary>
public class archetype_cue_test
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

    /// <summary>The first cue of a chain with a file under assets/audio (SoundBank.Resolve's rule, read from the repo).</summary>
    internal static string? Resolve(IEnumerable<string> chain)
    {
        var known = Directory.GetFiles(RepoFile("assets", "audio"), "*.wav", SearchOption.AllDirectories)
            .Select(f => Path.GetFileNameWithoutExtension(f)!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return chain.FirstOrDefault(known.Contains);
    }

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>The SHIPPED bytes (make_archetypes.py, 2026-10-04).</summary>
    private static readonly IReadOnlyDictionary<string, string> Shipped = new Dictionary<string, string>
    {
        ["sfx_fist_hit"] = "baddc0bb5af538baf74d55015d844f9015ed9650b91586dbe2b715413a164090",
        ["sfx_blade_hit"] = "ed8386ef0a022f06966af35576afbb0a93f2eee86789754705c5c158adda8c95",
        ["sfx_stone_hit"] = "ccd0ec599fc69d576c8e30116de0b48bde010e98590b034051adf1a7bb2d101c",
        ["sfx_wood_hit"] = "adebacac20e7ab9de234f473a157ea0304a7fbf8bc41ad8aa757fbf00676b9a8",
        ["sfx_throw_release"] = "d588f0424e66d14a269aeaecff0774730e2b8ecf55c26cc5a5bd02312bdcb326",
        ["sfx_air_release"] = "b7397ecde5aa84ebcbb552cc267376ccab031e9279a2ca7cf93bdc6cf488b8b3",
        ["sfx_field_tick"] = "bbb8f575e73b2a9b7cbdc483cd8af31bad0741d5a0c32cdbdb1021746fe162d3",
        ["sfx_cloth_commit"] = "fbeb771fd49d9eda9a047fc6f8fb4bd8d39f3e6f324d52cabb98c6ed41155687",
    };

    private static void AssertShipped(string key)
    {
        var path = Wav(key);
        Assert.True(File.Exists(path), $"{key}.wav is missing");
        Assert.True(Shipped[key] == Sha(path),
            $"{key}.wav changed: a SHIPPED archetype is changed by a deliberate commit that updates this pin, never by a regeneration.");
    }

    [Fact] public void test_sfx_fist_hit_is_shipped() => AssertShipped(ArchetypeCues.FistHit);
    [Fact] public void test_sfx_blade_hit_is_shipped() => AssertShipped(ArchetypeCues.BladeHit);
    [Fact] public void test_sfx_stone_hit_is_shipped() => AssertShipped(ArchetypeCues.StoneHit);
    [Fact] public void test_sfx_wood_hit_is_shipped() => AssertShipped(ArchetypeCues.WoodHit);
    [Fact] public void test_sfx_throw_release_is_shipped() => AssertShipped(ArchetypeCues.ThrowRelease);
    [Fact] public void test_sfx_air_release_is_shipped() => AssertShipped(ArchetypeCues.AirRelease);
    [Fact] public void test_sfx_field_tick_is_shipped() => AssertShipped(ArchetypeCues.FieldTick);
    [Fact] public void test_sfx_cloth_commit_is_shipped() => AssertShipped(ArchetypeCues.ClothCommit);

    [Fact]
    public void test_the_table_names_the_eight_pinned_files_in_the_designs_order()
    {
        Assert.Equal(new[] { "sfx_fist_hit", "sfx_blade_hit", "sfx_stone_hit", "sfx_wood_hit", "sfx_throw_release",
                             "sfx_air_release", "sfx_field_tick", "sfx_cloth_commit" },
                     ArchetypeCues.All.Select(c => c.Key));
        Assert.Equal(Shipped.Keys.OrderBy(k => k, StringComparer.Ordinal), ArchetypeCues.All.Select(c => c.Key).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(4, ArchetypeCues.All.Count(c => c.Moment == ArchetypeMoment.Hit));
        Assert.Equal(ArchetypeMoment.Tick, ArchetypeCues.Find(ArchetypeCues.FieldTick)!.Moment);
        Assert.Equal(ArchetypeMoment.Commit, ArchetypeCues.Find(ArchetypeCues.ClothCommit)!.Moment);
        Assert.Null(ArchetypeCues.Find("sfx_hit"));
    }

    [Fact]
    public void test_no_archetype_key_starts_with_sfx_seeker()
    {
        foreach (var cue in ArchetypeCues.All)
        {
            Assert.False(cue.Key.StartsWith("sfx_seeker_", StringComparison.Ordinal), $"{cue.Key} is a Seeker key");
            Assert.StartsWith("sfx_", cue.Key);
        }
        // ...and their ceilings are the closed references' own files
        foreach (var cue in ArchetypeCues.All)
        {
            Assert.StartsWith("sfx_seeker_", cue.Ceiling);
            Assert.True(File.Exists(Wav(cue.Ceiling)), cue.Ceiling);
        }
    }

    [Fact]
    public void test_no_candidate_or_selector_exists()
    {
        // one file per archetype: no lettered / numbered / named candidate beside it, anywhere under assets/audio
        var audio = Directory.GetFiles(RepoFile("assets", "audio"), "*.wav", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension).ToList();
        foreach (var cue in ArchetypeCues.All)
            Assert.Equal(new[] { cue.Key }, audio.Where(n => n!.StartsWith(cue.Key, StringComparison.Ordinal)));
        // no review history of candidates (there were none), and no candidate machinery in the builder
        Assert.False(Directory.Exists(RepoFile("tools", "asset-pipeline", "audio_history", "archetypes")));
        var py = File.ReadAllText(RepoFile("tools", "asset-pipeline", "make_archetypes.py"));
        foreach (var word in new[] { "APPROVED", "BUILDS", "HISTORY", "candidate(", "ARCHIVE" })
            Assert.DoesNotContain(word, py);
        // no switch in the game picks among versions of an archetype
        var src = Directory.GetFiles(RepoFile("src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText);
        foreach (var text in src)
        {
            Assert.DoesNotContain("RH_ARCHETYPE_", text);
            foreach (var cue in ArchetypeCues.All)
                Assert.DoesNotContain(cue.Key + "_", text);
        }
    }

    [Fact]
    public void test_the_manifest_and_the_measured_levels_name_these_bytes()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "foley", "archetypes", "archetype_manifest.json")));
        using var levels = JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "archetype_levels.json")));
        Assert.StartsWith("SHIPPED", manifest.RootElement.GetProperty("pin_level").GetString());
        Assert.True(levels.RootElement.GetProperty("all_pass").GetBoolean(), "an archetype is over its ceiling");
        foreach (var cue in ArchetypeCues.All)
        {
            Assert.Equal(Shipped[cue.Key], manifest.RootElement.GetProperty("cues").GetProperty(cue.Key).GetProperty("sha256").GetString());
            var row = levels.RootElement.GetProperty("cues").GetProperty(cue.Key);
            Assert.Equal(Shipped[cue.Key], row.GetProperty("sha256").GetString());
            Assert.True(row.GetProperty("pass").GetBoolean(), $"{cue.Key} failed its ceiling");
            // the file was mastered at a volume its consumers use
            var v = row.GetProperty("volume").GetSingle();
            Assert.InRange(v, cue.MinVolume, cue.MaxVolume);
        }
    }

    [Fact]
    public void test_every_excerpt_is_cc0_listed_with_its_page_and_read_by_a_cue()
    {
        var foley = RepoFile("tools", "asset-pipeline", "foley", "archetypes");
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
            Assert.Contains("`sfx_", row);                      // read by at least one shipped cue
        }
        // every cue is made of excerpts listed there, and no raw download is committed beside them
        Assert.Empty(Directory.GetFiles(foley).Where(f => f.EndsWith(".mp3") || f.EndsWith(".flac") || f.EndsWith(".ogg")));
        foreach (var cue in ArchetypeCues.All)
            Assert.Contains($"`{cue.Key}`", sources);
    }

    [Fact]
    public void test_the_closed_references_resolve_their_own_files_before_any_archetype()
    {
        // adding sfx_blade_hit / sfx_throw_release / sfx_fist_hit changes neither SPRAY nor HARD HANDS: their own files
        // come first in their chains and exist
        Assert.Equal("sfx_seeker_spray_hit", Resolve(ActionRecipes.SeekerSpray.ContactCues));
        Assert.Equal("sfx_seeker_spray_release", Resolve(ActionRecipes.SeekerSpray.ReleaseCues));
        Assert.Equal("sfx_seeker_spray_tick", Resolve(ActionRecipes.SeekerSpray.ContactTickCues));
        Assert.Equal("sfx_seeker_hard_hands_hit", Resolve(ActionRecipes.SeekerHardHands.ContactCues));
        Assert.Equal("sfx_seeker_hard_hands_commit", Resolve(ActionRecipes.SeekerHardHands.ReleaseCues));
        Assert.Equal(1, ActionRecipes.SeekerSpray.ContactCues.ToList().IndexOf(ArchetypeCues.BladeHit));
        Assert.Equal(1, ActionRecipes.SeekerSpray.ReleaseCues.ToList().IndexOf(ArchetypeCues.ThrowRelease));
        Assert.Equal(1, ActionRecipes.SeekerHardHands.ContactCues.ToList().IndexOf(ArchetypeCues.FistHit));
    }

    [Fact]
    public void test_the_throttle_holds_hit_families_at_60_and_ticks_at_90()
    {
        Assert.Equal(60, ArchetypeCues.HitGapMs);
        Assert.Equal(90, ArchetypeCues.TickGapMs);
        foreach (var cue in ArchetypeCues.All)
        {
            if (cue.Moment == ArchetypeMoment.Hit) Assert.Equal(60, SoundBank.ThrottleTable[cue.Key]);
            else if (cue.Moment == ArchetypeMoment.Tick) Assert.Equal(90, SoundBank.ThrottleTable[cue.Key]);
            else Assert.False(SoundBank.ThrottleTable.ContainsKey(cue.Key), $"{cue.Key} keeps the default gap");
            Assert.DoesNotContain(cue.Key, SoundBank.Unthrottled);
        }
        // every basic attack's own contact cue is a hit family too (its identity cue, when built, is throttled already)
        foreach (var (id, recipe) in SwingRecipes.All)
            Assert.Equal(60, SoundBank.ThrottleTable[recipe.ContactCues[0]]);
        // the hand-tuned rows are untouched, and no Seeker reference voice gains a row
        Assert.Equal(60, SoundBank.ThrottleTable["sfx_hit"]);
        Assert.Equal(900, SoundBank.ThrottleTable["sfx_dispatch"]);
        Assert.DoesNotContain(SoundBank.ThrottleTable.Keys, k => k.StartsWith("sfx_seeker_", StringComparison.Ordinal)
                                                               && !k.EndsWith("_swing_hit", StringComparison.Ordinal));
    }
}
