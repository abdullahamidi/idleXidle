using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE SWEEP'S MIX RULES (design.md section 7, "Mix rules (pinned by a sweep_audio_hierarchy_test)"), started in Phase 1
/// for what Phase 1 plays: the ten basic attacks' cue volumes and chains, and the archetypes they and later items fall
/// back to. A cue's EFFECTIVE level is the loudest 50 ms RMS of the file its chain resolves to, times its volume (dBFS);
/// nothing T1 reaches SPRAY's contact (0.50) or HARD HANDS' hit (0.55). Later phases add rows (skill contacts under SPRAY,
/// signatures under HARD HANDS, field ticks under PRESS + 0.06, afflictions under BRAND's apply).
/// </summary>
public class sweep_audio_hierarchy_test
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static readonly Lazy<Dictionary<string, string>> Files = new(() =>
        Directory.GetFiles(RepoFile("assets", "audio"), "*.wav", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f)!, f => f, StringComparer.OrdinalIgnoreCase));

    /// <summary>The first cue of a chain with a file on disk (SoundBank.Resolve's rule), or null.</summary>
    private static string? Resolve(IEnumerable<string> chain) => chain.FirstOrDefault(Files.Value.ContainsKey);

    /// <summary>A 16-bit PCM WAV's samples in -1..1, channels averaged.</summary>
    private static float[] ReadPcm16(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        int channels = 1, bits = 16, pos = 12;
        while (pos + 8 <= bytes.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
            var size = BitConverter.ToInt32(bytes, pos + 4);
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(bytes, pos + 10);
                bits = BitConverter.ToInt16(bytes, pos + 22);
            }
            else if (id == "data")
            {
                Assert.Equal(16, bits);
                var frames = size / (2 * channels);
                var x = new float[frames];
                for (var i = 0; i < frames; i++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                        sum += BitConverter.ToInt16(bytes, pos + 8 + (i * channels + c) * 2) / 32768f;
                    x[i] = sum / channels;
                }
                return x;
            }
            pos += 8 + size + (size & 1);
        }
        throw new InvalidDataException($"{path}: no data chunk");
    }

    /// <summary>The loudest 50 ms RMS of a cue's file (44.1 kHz), dBFS.</summary>
    private static double Loudest50Db(string key)
    {
        var x = ReadPcm16(Files.Value[key]);
        const int w = 2205;                                  // 50 ms at 44.1 kHz
        var acc = new double[x.Length + 1];
        for (var i = 0; i < x.Length; i++) acc[i + 1] = acc[i] + (double)x[i] * x[i];
        var best = 0.0;
        for (var i = 0; i + w <= x.Length; i++) best = Math.Max(best, acc[i + w] - acc[i]);
        if (x.Length < w) best = acc[x.Length];
        return 10 * Math.Log10(Math.Max(1e-24, best / w));
    }

    /// <summary>A cue chain's effective level at a volume: the resolved file's loudest 50 ms RMS x the volume, dBFS.</summary>
    private static double Effective(IEnumerable<string> chain, float volume)
    {
        var key = Resolve(chain);
        Assert.True(key is not null, $"no file for the chain {string.Join(" -> ", chain)}");
        return Loudest50Db(key!) + 20 * Math.Log10(volume);
    }

    private static double SprayContact => Effective(ActionRecipes.SeekerSpray.ContactCues, ActionRecipes.SeekerSpray.ContactVolume);
    private static double HardHandsContact => Effective(ActionRecipes.SeekerHardHands.ContactCues, ActionRecipes.SeekerHardHands.ContactVolume);

    [Fact]
    public void test_the_references_are_the_ceilings_the_rules_name()
    {
        Assert.Equal(0.50f, ActionRecipes.SeekerSpray.ContactVolume);
        Assert.Equal(0.55f, ActionRecipes.SeekerHardHands.ContactVolume);
        Assert.Equal(0.40f, ActionRecipes.SeekerSpray.ReleaseVolume);
        Assert.Equal(0.34f, ActionRecipes.SeekerHardHands.ReleaseVolume);
        Assert.Equal(0.28f, FieldRecipes.SeekerPress.TickVolume);
    }

    [Fact]
    public void test_every_swing_contact_is_ordinary_and_every_release_is_quiet()
    {
        Assert.Equal(10, SwingRecipes.All.Count);
        foreach (var (id, r) in SwingRecipes.All)
        {
            Assert.InRange(r.ContactVolume, 0.34f, 0.38f);
            Assert.Equal(ActionWeight.Ordinary, r.Weight);
            if (r.Kind == SwingKind.Missile)
            {
                Assert.NotEmpty(r.ReleaseCues);
                Assert.InRange(r.ReleaseVolume, 0.16f, 0.18f);
            }
            else Assert.Empty(r.ReleaseCues);
        }
    }

    [Fact]
    public void test_every_swing_cue_chain_resolves_to_an_existing_file_and_never_to_the_generic()
    {
        foreach (var (id, r) in SwingRecipes.All)
        {
            var contact = Resolve(r.ContactCues);
            Assert.True(contact is not null, $"{id}'s contact chain resolves to no file");
            Assert.NotEqual("sfx_hit", contact);
            // until its identity cue exists, a basic attack is heard through its archetype, the design's material
            Assert.True(contact == r.ContactCues[0] || ArchetypeCues.Find(contact!) is { Moment: ArchetypeMoment.Hit },
                $"{id}'s contact resolves to {contact}");
            if (r.ReleaseCues.Count == 0) continue;
            var release = Resolve(r.ReleaseCues);
            Assert.True(release is not null, $"{id}'s release chain resolves to no file");
            Assert.True(release == r.ReleaseCues[0] || release == ArchetypeCues.ThrowRelease, $"{id}'s release resolves to {release}");
        }
    }

    [Fact]
    public void test_every_t1_swing_cue_is_below_spray_and_hard_hands_contact()
    {
        var spray = SprayContact;
        var hands = HardHandsContact;
        Assert.True(spray < hands, "SPRAY's contact is the skill ceiling, under HARD HANDS' signature hit");
        foreach (var (id, r) in SwingRecipes.All)
        {
            var hit = Effective(r.ContactCues, r.ContactVolume);
            Assert.True(hit < spray, $"{id}'s contact ({hit:0.0} dBFS) is not under SPRAY's ({spray:0.0})");
            Assert.True(hit < hands, $"{id}'s contact ({hit:0.0} dBFS) is not under HARD HANDS' ({hands:0.0})");
            if (r.ReleaseCues.Count == 0) continue;
            var release = Effective(r.ReleaseCues, r.ReleaseVolume);
            Assert.True(release < hit, $"{id}'s release ({release:0.0}) is louder than its own contact ({hit:0.0})");
        }
    }

    /// <summary>The basic attacks' thirteen identity cues (P1.6): the ten swing hits and the three releases, with the volume
    /// their recipe plays them at, and (for a release) its own hit.</summary>
    public static IEnumerable<object[]> IdentityRows()
    {
        foreach (var (id, r) in SwingRecipes.All)
        {
            yield return new object[] { r.ContactCues[0], r.ContactVolume, "" };
            if (r.ReleaseCues.Count > 0) yield return new object[] { r.ReleaseCues[0], r.ReleaseVolume, r.ContactCues[0] };
        }
    }

    [Theory]
    [MemberData(nameof(IdentityRows))]
    public void test_each_identity_swing_cue_is_its_chains_file_and_sits_under_the_references(string key, float volume, string ownHit)
    {
        Assert.True(Files.Value.ContainsKey(key), $"{key}.wav is missing");
        var level = Loudest50Db(key) + 20 * Math.Log10(volume);
        if (ownHit.Length == 0)
        {
            Assert.Equal(0.36f, volume);
            Assert.True(level < SprayContact, $"{key} ({level:0.0} dBFS) is not under SPRAY's contact ({SprayContact:0.0})");
            Assert.True(level < HardHandsContact, $"{key} ({level:0.0} dBFS) is not under HARD HANDS' contact ({HardHandsContact:0.0})");
            // at the T1 band's top it still sits under both
            var top = Loudest50Db(key) + 20 * Math.Log10(0.38);
            Assert.True(top < SprayContact && top < HardHandsContact, $"{key} at 0.38 reaches a reference contact");
            return;
        }
        Assert.InRange(volume, 0.16f, 0.18f);
        var hit = Loudest50Db(ownHit) + 20 * Math.Log10(0.36f);
        Assert.True(level < hit, $"{key} ({level:0.0}) is louder than its own hit {ownHit} ({hit:0.0})");
        var sprayRelease = Effective(ActionRecipes.SeekerSpray.ReleaseCues, ActionRecipes.SeekerSpray.ReleaseVolume);
        Assert.True(level < sprayRelease, $"{key} ({level:0.0}) is not under SPRAY's release ({sprayRelease:0.0})");
    }

    [Fact]
    public void test_every_archetype_at_the_top_of_its_band_sits_under_its_ceiling()
    {
        foreach (var cue in ArchetypeCues.All)
        {
            Assert.True(Files.Value.ContainsKey(cue.Key), $"{cue.Key}.wav is missing");
            Assert.True(cue.MinVolume <= cue.MaxVolume);
            var level = Loudest50Db(cue.Key) + 20 * Math.Log10(cue.MaxVolume);
            var ceiling = Loudest50Db(cue.Ceiling) + 20 * Math.Log10(cue.CeilingVolume);
            Assert.True(level < ceiling, $"{cue.Key} at {cue.MaxVolume} ({level:0.0}) is not under {cue.Ceiling} at {cue.CeilingVolume} ({ceiling:0.0})");
            if (cue.Moment == ArchetypeMoment.Hit)
            {
                // a hit is checked against BOTH contact references
                Assert.True(level < SprayContact && level < HardHandsContact, $"{cue.Key} reaches a reference contact");
                Assert.InRange(cue.MaxVolume, 0.34f, 0.38f);
            }
        }
        // the design's bands: a field tick never above PRESS + 0.06, a commit 0.28-0.34, a basic release 0.16-0.18
        Assert.Equal(FieldRecipes.SeekerPress.TickVolume + 0.06f, ArchetypeCues.Find(ArchetypeCues.FieldTick)!.MaxVolume, 3);
        Assert.Equal(0.34f, ArchetypeCues.Find(ArchetypeCues.ClothCommit)!.MaxVolume);
        Assert.Equal(0.18f, ArchetypeCues.Find(ArchetypeCues.ThrowRelease)!.MaxVolume);
    }

    [Fact]
    public void test_a_t1_swing_is_never_lead_and_never_ducks()
    {
        // the T1 mix: "never ducks others, takes the duck" -- the swing voices through PlayFirst-style resolution, not lead
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var swing = hunt.Split('\n').Select(l => l.Trim())
            .Where(l => l.StartsWith("var cue = Sound?.PlayFirst(recipe.ContactCues, recipe.ContactVolume,", StringComparison.Ordinal)
                        || l.StartsWith("var cue = Sound?.PlayFirst(r.ReleaseCues, r.ReleaseVolume,", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, swing.Count);                         // the contact (SwingContact) and the release (UpdateSwing)
        Assert.DoesNotContain(swing, l => l.Contains("lead:"));
        var contact = hunt[hunt.IndexOf("private void SwingContact(", StringComparison.Ordinal)..];
        contact = contact[..contact.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.DoesNotContain("Duck", contact);
    }
}
