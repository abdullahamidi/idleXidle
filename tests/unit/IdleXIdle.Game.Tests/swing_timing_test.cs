using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE SWING TIMING DATA (vfx sweep Phase 1, P1.1): every champion's basic attack strip carries an authored timing file
/// (<c>char_&lt;id&gt;_attack_strip8_512.clip.json</c>) with its key moments and its hand socket, for the swing
/// performances that read them, and those files change NOTHING on screen yet: each says <c>"place": "own"</c>, so the
/// strip keeps its own measured placement, and no skill's recipe ever resolves the <c>attack</c> clip.
/// </summary>
public class SwingTimingTest
{
    /// <summary>The ten basic attacks: champion, the anchor marker (contact for a blow or a reach, release for a
    /// missile), the anchor frame and the socket the performance reads.</summary>
    public static IEnumerable<object[]> Swings() => new[]
    {
        new object[] { "seeker", "contact", 4, "StrikeHand" },
        new object[] { "anvil", "contact", 5, "StrikeHand" },
        new object[] { "metronome", "contact", 2, "StrikeHand" },
        new object[] { "tower", "contact", 5, "StrikeHand" },
        new object[] { "thornwall", "contact", 3, "StrikeHand" },
        new object[] { "magpie", "contact", 3, "StrikeHand" },
        new object[] { "quiver", "release", 4, "BowHand" },
        new object[] { "chorus", "release", 5, "ThrowHand" },
        new object[] { "unbroken", "release", 5, "ThrowHand" },
        new object[] { "oathbound", "contact", 4, "LashHand" },
    };

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string TimingPath(string id)
        => RepoFile("assets", "art", "Animations", "Roster", $"{id}_attack", $"char_{id}_attack_strip8_512.clip.json");

    private static ActionClipTiming Timing(string id) => ActionClipTiming.Parse(File.ReadAllText(TimingPath(id)));

    [Theory]
    [MemberData(nameof(Swings))]
    public void test_attack_timing_file_parses_with_eight_frames(string id, string anchor, int frame, string socket)
    {
        Assert.True(File.Exists(TimingPath(id)), $"{id}: no timing file beside its attack strip");
        var t = Timing(id);
        Assert.Equal(8, t.Frames);
        Assert.Equal(8, t.Elastic.Count);
        Assert.All(t.FrameMs, ms => Assert.True(ms >= 1f));
        Assert.Equal(frame, t.Markers[anchor]);
        Assert.NotNull(socket);
    }

    [Theory]
    [MemberData(nameof(Swings))]
    public void test_attack_markers_are_in_order_for_their_kind(string id, string anchor, int frame, string socket)
    {
        var t = Timing(id);
        foreach (var m in new[] { "commit", "recovery", "settle", anchor })
            Assert.True(t.HasMarker(m), $"{id}: no '{m}' marker (the step-in curve reads commit and settle)");
        Assert.Equal(t.Frames - 1, t.Markers["settle"]);
        if (anchor == "contact")
        {
            // a melee blow or a reach: commit < contact < recovery <= settle
            Assert.False(t.HasMarker("release"), $"{id}: a blow names a contact, not a release");
            Assert.True(t.Markers["commit"] < t.Markers["contact"], $"{id}: commit before contact");
            Assert.True(t.Markers["contact"] < t.Markers["recovery"], $"{id}: contact before recovery");
            Assert.True(t.Markers["recovery"] <= t.Markers["settle"], $"{id}: recovery no later than settle");
        }
        else
        {
            // a missile: it leaves the hand before the figure recovers
            Assert.False(t.HasMarker("contact"), $"{id}: a missile names a release, not a contact");
            Assert.True(t.Markers["commit"] < t.Markers["release"], $"{id}: commit before release");
            Assert.True(t.Markers["release"] < t.Markers["recovery"], $"{id}: release before recovery");
        }
        Assert.Equal(frame, t.Markers[anchor]);
        Assert.False(string.IsNullOrEmpty(socket), $"{id}: the swing names its socket");
    }

    [Theory]
    [MemberData(nameof(Swings))]
    public void test_attack_anchor_frame_is_never_elastic(string id, string anchor, int frame, string socket)
    {
        // ELASTIC frames absorb a faster beat; the commit to the anchor never shrinks (the blow lands at its own pace)
        var t = Timing(id);
        for (var f = t.Markers["commit"]; f <= frame; f++)
            Assert.False(t.Elastic[f], $"{id}: frame {f}, between the commit and the {anchor}, must not be elastic");
        Assert.True(t.Elastic[0], $"{id}: the wait before the swing gives way to a faster beat");
        Assert.NotNull(socket);
    }

    [Theory]
    [MemberData(nameof(Swings))]
    public void test_attack_hand_socket_exists_around_the_anchor(string id, string anchor, int frame, string socket)
    {
        var t = Timing(id);
        foreach (var f in new[] { frame - 1, frame, frame + 1 })
        {
            var s = t.Socket(f, socket);
            Assert.True(s is not null, $"{id}: no {socket} on frame {f} ({anchor} is {frame})");
            Assert.InRange(s!.Value.X, 0f, 1f);
            Assert.InRange(s.Value.Y, 0f, 1f);
        }
    }

    [Theory]
    [MemberData(nameof(Swings))]
    public void test_attack_timing_keeps_its_own_placement(string id, string anchor, int frame, string socket)
    {
        Assert.True(Timing(id).PlaceOwn, $"{id}: the basic attack strip must keep its own placement (\"place\": \"own\")");
        Assert.NotNull(anchor + frame + socket);
    }

    [Theory]
    [MemberData(nameof(Swings))]
    public void test_attack_timing_names_the_tool_and_the_eye(string id, string anchor, int frame, string socket)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(TimingPath(id)));
        var source = doc.RootElement.GetProperty("source");
        Assert.Contains("clip_markers.py", source.GetProperty("tool").GetString());
        Assert.Equal(JsonValueKind.Object, source.GetProperty("proposed").ValueKind);   // what the tool proposed
        Assert.Equal(JsonValueKind.Object, source.GetProperty("eye").ValueKind);        // what was judged by eye instead
        Assert.NotNull(anchor + frame + socket);
    }

    [Fact]
    public void test_place_own_parses_only_the_word_own()
    {
        const string body = "\"frameMs\":[100,100,100],\"markers\":{\"contact\":1}";
        Assert.False(ActionClipTiming.Parse("{" + body + "}").PlaceOwn);
        Assert.True(ActionClipTiming.Parse("{" + body + ",\"place\":\"own\"}").PlaceOwn);
        Assert.False(ActionClipTiming.Parse("{" + body + ",\"place\":\"idle\"}").PlaceOwn);
        Assert.False(ActionClipTiming.Parse("{" + body + ",\"place\":true}").PlaceOwn);
        Assert.False(new ActionClipTiming(new[] { 100f }).PlaceOwn);
    }

    [Fact]
    public void test_place_own_survives_every_refit()
    {
        var t = Timing("seeker");
        Assert.True(t.FitBefore("contact", 120f).PlaceOwn);
        Assert.True(t.FitRecovery(60f, 30f, out _).PlaceOwn);
        Assert.True(t.CutAt(t.MarkerMs("contact") + 10f).PlaceOwn);
    }

    [Fact]
    public void test_authored_skill_strips_are_still_placed_as_the_idle()
    {
        // HARD HANDS and SPRAY were drawn on the idle's transform (keyposes.py): their files do not say "own"
        foreach (var clip in new[] { "hard_hands", "projectile" })
        {
            var path = RepoFile("assets", "art", "Animations", "Roster", $"seeker_{clip}", $"char_seeker_{clip}_strip8_512.clip.json");
            Assert.False(ActionClipTiming.Parse(File.ReadAllText(path)).PlaceOwn, $"seeker_{clip} must stay placed as the idle");
        }
    }

    [Fact]
    public void test_library_loads_all_ten_attack_timings_beside_the_seekers_two()
    {
        var lib = ActionClipLibrary.Load(RepoFile("assets", "art"));
        foreach (var id in Swings().Select(s => (string)s[0]))
            Assert.True(lib.TryGetValue($"char_{id}_attack_strip8_512", out var t) && t.PlaceOwn, $"{id}: attack timing not loaded");
        Assert.False(lib["char_seeker_hard_hands_strip8_512"].PlaceOwn);
        Assert.False(lib["char_seeker_projectile_strip8_512"].PlaceOwn);
    }

    [Fact]
    public void test_placed_as_honours_place_own()
    {
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var at = src.IndexOf("private string? PlacedAs(string stripKey)", StringComparison.Ordinal);
        Assert.True(at >= 0, "HuntScreen.PlacedAs not found");
        var body = src[at..src.IndexOf(';', at)];
        Assert.Contains("ActionClipLibrary.For(stripKey) is { PlaceOwn: false }", body);
        Assert.Contains("ChampionReferenceStrip", body);
    }

    [Fact]
    public void test_no_recipe_resolves_the_attack_clip()
    {
        // TryAuthored resolves a skill's recipe through ActionRecipes.For; the basic attack is not a skill, and its
        // timing file must not make any champion's swing a performed action before its swing recipe exists
        foreach (var id in Swings().Select(s => (string)s[0]))
            Assert.Null(ActionRecipes.For(id, "none", "attack", "attack"));
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var body = src[src.IndexOf("private bool TryAuthored(", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("return true;", StringComparison.Ordinal)];
        Assert.Contains("ActionRecipes.For(Character.Id, def.Id, clip, def.FxKey)", body);
        Assert.Contains("Character.StripKeys(r.ClipKey)", body);
    }
}
