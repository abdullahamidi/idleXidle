using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE TOAST THAT IS LEFT IS HONEST. It carries only direct feedback now, which is the one kind of
/// message a player is owed immediately — so the three ways it could lie about itself matter more,
/// not less: a lane that reserves a different height from the plate it reserves it for, a close
/// control that is hit-tested on frames it was never painted on, and a cue that sounds for a plate
/// nobody sees.
/// </summary>
/// <remarks>
/// <see cref="Game1"/> needs MonoGame to instantiate and text measurement needs a graphics device, so
/// the claims here are the two kinds a test can make without either: ARITHMETIC over the private
/// statics (read at each density profile by reflection, as <see cref="ChromeReflowTests"/> does) and
/// STRUCTURAL pins on the source, which is how every other host invariant in this suite is held.
/// </remarks>
public class NoticeToastHonestyTest
{
    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Game1Source() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");

    /// <summary>One member, from its signature to its own closing brace — braces counted, not guessed.</summary>
    private static string MemberOf(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"source has no `{signature}` — the toast was reshaped without its test.");
        var open = source.IndexOf('{', at);
        var semi = source.IndexOf(';', at);
        if (open < 0 || (semi >= 0 && semi < open)) return source[at..(semi + 1)];
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[at..(i + 1)];
        }
        throw new InvalidOperationException($"`{signature}` never closes.");
    }

    private static T Read<T>(string name)
    {
        var t = typeof(Game1);
        if (t.GetProperty(name, Statics) is { } p) return (T)p.GetValue(null)!;
        if (t.GetField(name, Statics) is { } f) return (T)f.GetValue(null)!;
        throw new Xunit.Sdk.XunitException($"Game1 has no static member named {name} — the toast was renamed without its test.");
    }

    private static int HeightFor(int bodyLines)
    {
        var m = typeof(Game1).GetMethod("NoticeToastHeightFor", Statics)
                ?? throw new Xunit.Sdk.XunitException("Game1 has no NoticeToastHeightFor — the plate and the lane can measure differently again.");
        return (int)m.Invoke(null, new object[] { bodyLines })!;
    }

    /// <summary>The same conservative per-character budget the keystone test states: ten pixels at the Body rung.</summary>
    private static int LinesNeeded(string text, int width, int rung)
    {
        var perLine = width * 22 / (rung * 10);
        var lines = 1;
        var used = 0;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var add = used == 0 ? word.Length : word.Length + 1;
            if (used + add > perLine) { lines++; used = word.Length; }
            else used += add;
        }
        return lines;
    }

    /// <summary>
    /// ONE ROOM, ONE HEIGHT. The lane reserved the page's room by wrapping at <c>width - Space(60)</c>
    /// while the plate wrapped at the width less TWICE the close button's corner — so at 150 % they
    /// could count a different number of lines and the page could be pushed down by one line more or
    /// less than the plate actually needed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_lane_and_the_plate_measure_one_room(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            var width = Read<int>("NoticeToastWidth");
            var room = Read<int>("NoticeToastRoom");
            Assert.InRange(room, 1, width - 1);
            // A taller body is a taller plate, by exactly one body rung — and both halves ask this.
            Assert.Equal(HeightFor(1) + UiTypography.Pitch(UiTypography.OverlayBody), HeightFor(2));
            Assert.Equal(HeightFor(1), HeightFor(0));   // a bodiless toast still gets its one rung
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>Neither half keeps a room or a height of its own: both ask the shared members.</summary>
    [Fact]
    public void test_neither_half_wraps_with_a_room_of_its_own()
    {
        var game = Game1Source();
        var draw = MemberOf(game, "private void DrawNoticeToast()");
        var lane = MemberOf(game, "private int NoticeToastHeight()");
        var body = MemberOf(game, "private List<string> NoticeToastBody()");

        Assert.Contains("NoticeToastBody()", draw, StringComparison.Ordinal);
        Assert.Contains("NoticeToastHeightFor(", draw, StringComparison.Ordinal);
        Assert.Contains("NoticeToastBody()", lane, StringComparison.Ordinal);
        Assert.Contains("NoticeToastHeightFor(", lane, StringComparison.Ordinal);
        Assert.Contains("NoticeToastRoom", body, StringComparison.Ordinal);
        // The two rooms that used to disagree, by name.
        Assert.DoesNotContain("Space(60)", lane, StringComparison.Ordinal);
        foreach (var half in new[] { draw, lane })
            Assert.DoesNotContain("WrapBig(", half, StringComparison.Ordinal);
        // Exactly one place says how wide the text may be.
        Assert.Equal(1, Regex.Matches(game, @"private static int NoticeToastRoom").Count);
    }

    /// <summary>
    /// EVERY TOAST THE GAME CAN STILL POST FITS THE PLATE at every profile. The plate takes only
    /// <c>NoticeBodyLines</c> of wrapped body and truncates the rest with no ellipsis and no error.
    /// </summary>
    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_every_toast_the_game_can_still_post_fits_the_plate(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            var budget = (int)typeof(Game1).GetField("NoticeBodyLines", Statics)!.GetRawConstantValue()!;
            var room = Read<int>("NoticeToastRoom");
            // Read from the source, so a new toast copy is measured the day it is written.
            var details = Regex.Matches(Game1Source(), "PostNotice\\([^;]*?,\\s*\"([^\"]+)\"")
                               .Select(m => m.Groups[1].Value)
                               .Concat(Regex.Matches(Game1Source(), "PostNotice\\([^;]*?\\n\\s*\"([^\"]+)\"")
                                            .Select(m => m.Groups[1].Value))
                               .Distinct()
                               .ToList();
            Assert.NotEmpty(details);
            foreach (var detail in details)
            {
                var need = LinesNeeded(detail, room, UiTypography.OverlayBody);
                Assert.True(need <= budget,
                            $"at {percent}% the toast body wraps to {need} lines and the plate holds {budget} — "
                            + $"it would be cut mid-sentence. \"{detail}\"");
            }
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>No return in the plate's paint leaves a close rect live: a hidden × cannot take a click.</summary>
    [Fact]
    public void test_no_return_in_the_plate_leaves_its_close_live()
    {
        var draw = MemberOf(Game1Source(), "private void DrawNoticeToast()");
        var cleared = draw.IndexOf("_noticeCloseRect = Rectangle.Empty;", StringComparison.Ordinal);
        Assert.True(cleared >= 0, "the notice's close rect is never cleared.");
        foreach (Match ret in Regex.Matches(draw, @"\breturn;"))
            Assert.True(ret.Index > cleared,
                        $"an early return at offset {ret.Index} leaves the notice's × live (cleared at {cleared}).");
    }

    /// <summary>
    /// THE CUE BELONGS TO THE FRAME THE PLATE PAINTS. Dequeuing is not being seen — the band can be
    /// held by a slot card — so the reward sound used to play for a plate that never appeared.
    /// </summary>
    [Fact]
    public void test_the_cue_waits_for_the_frame_the_plate_paints()
    {
        var game = Game1Source();
        var update = MemberOf(game, "protected override void Update(GameTime gameTime)");

        // Armed where the toast is taken off the queue...
        Assert.Contains("_noticeCueOwed = true;", update, StringComparison.Ordinal);
        // ...and spent only where the plate is actually showing.
        Assert.Contains("if (_noticeCueOwed && NoticeToastShowing)", update, StringComparison.Ordinal);
        // One predicate answers for the plate, the lane and the cue.
        Assert.Contains("private bool NoticeToastShowing", game, StringComparison.Ordinal);
        Assert.Contains("if (!NoticeToastShowing) return;", MemberOf(game, "private void DrawNoticeToast()"), StringComparison.Ordinal);
        Assert.Contains("var showing = OverlayActive && NoticeToastShowing;", MemberOf(game, "private void ReserveNoticeLane()"), StringComparison.Ordinal);
        // The cue is not still sounding inside the dequeue.
        var dequeue = update[update.IndexOf("_notice = _noticeQueue.Dequeue();", StringComparison.Ordinal)..];
        dequeue = dequeue[..dequeue.IndexOf("\n        }", StringComparison.Ordinal)];
        Assert.DoesNotContain("_sound.", dequeue, StringComparison.Ordinal);
    }

    /// <summary>A new game arms no boot toast when there is nothing for it to say.</summary>
    /// <remarks>
    /// An empty seven-second timer is not a toast, but every surface that steps around one counted it:
    /// the hunt's HunterDown, BOSS INCOMING and WAVE CLEARED banners stood down for seven seconds
    /// after START A NEW GAME, for a plate with no words in it.
    /// </remarks>
    [Fact]
    public void test_a_new_game_arms_no_boot_toast_with_nothing_to_say()
    {
        var start = MemberOf(Game1Source(), "private void StartNewGame()");
        Assert.DoesNotContain("\n        _bootTimer = 7f;", start, StringComparison.Ordinal);
        Assert.Contains("_bootTimer = _bootMessage.Length > 0 ? 7f : 0f;", start, StringComparison.Ordinal);
    }

    /// <summary>The FORGE's materials strip yields the band a toast or a slot card stands in.</summary>
    /// <remarks>
    /// Every other screen lays out from <c>UiKit.PageTop</c>, which grows by the notice lane, so a
    /// plate in that lane stands ABOVE the page. The Forge cannot pay that height — three columns with
    /// no slack at 150 % — so it anchors at the page's own top and the plate landed ON its strip. The
    /// strip stands down instead: every balance it carries is also in the chrome pills.
    /// </remarks>
    [Fact]
    public void test_the_forge_strip_yields_the_band_a_toast_stands_in()
    {
        var forge = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "ForgeScreen.cs")).Replace("\r\n", "\n");
        var at = forge.IndexOf("private void DrawMaterialStrip(", StringComparison.Ordinal);
        Assert.True(at >= 0, "ForgeScreen has no DrawMaterialStrip — the strip was renamed without its test.");
        var draw = MemberOf(forge, "private void DrawMaterialStrip(");
        var yields = draw.IndexOf("if (UiKit.NoticeLane > 0) return;", StringComparison.Ordinal);
        Assert.True(yields >= 0, "the FORGE's materials strip paints under the notice lane.");
        Assert.True(yields < draw.IndexOf("_ui.Plate(b, MaterialStrip);", StringComparison.Ordinal),
                    "the strip yields after painting itself, which is not yielding.");
    }
}
