using System.Collections.Generic;
using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Progression;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A KEYSTONE REVEAL FITS THE SURFACE IT WAS GIVEN.
/// </summary>
/// <remarks>
/// <para>
/// The reveal is deliberately split across two surfaces, and each has a different contract:
/// </para>
/// <list type="bullet">
///   <item>the MAP's one-line strip carries only a headline — <c>&lt;REGION&gt; CONQUERED!  NEW
///   KEYSTONE — &lt;NAME&gt;.</c> — and <see cref="MapStripTests"/> owns that half;</item>
///   <item>the DISPATCH carries the full description, WRAPPED, and this file owns that half.</item>
/// </list>
/// <para>
/// The split exists because the two failure modes are opposite. Forcing a 164-character description
/// into the strip either shrinks the type or spills it across the region cards; dropping the
/// description entirely leaves a reveal that names a keystone and never says what it does, and the
/// word IRONCLAD teaches nobody anything.
/// </para>
/// <para>
/// The description used to live in a notice toast, which took only <c>NoticeBodyLines</c> of wrapped
/// text and TRUNCATED anything longer with no error, no ellipsis and no test. It is a letter now: the
/// reading pane wraps every line and cuts none, so the risk moved with it — a reveal longer than the
/// pane is tall would run off the bottom of the panel instead. That is what this file asserts, at
/// every density profile, against the pane's real rectangle.
/// </para>
/// </remarks>
public class KeystoneNoticeTests
{
    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>The reading pane's own rectangle, read rather than restated so the two cannot drift.</summary>
    private static Rectangle Pane()
    {
        var p = typeof(Game1).GetProperty("DispatchesPane", Statics)
                ?? throw new Xunit.Sdk.XunitException(
                    "Game1 has no DispatchesPane — the reading surface was reshaped without its test.");
        return (Rectangle)p.GetValue(null)!;
    }

    /// <summary>
    /// How many wrapped lines a string takes in a box of this width at this type rung.
    /// </summary>
    /// <remarks>
    /// Real font measurement needs a graphics device, so this is the same arithmetic claim
    /// <see cref="MapStripTests"/> makes about the strip: a conservative per-character budget, stated
    /// once. A notice plate 800 px wide was proved to hold 80 characters at the Body rung (22 px at
    /// 100 %), so ten pixels per character is the figure, scaled with the rung the way every other
    /// measurement in this project scales. Conservative is the safe direction — real glyphs are
    /// narrower, so a real line fits MORE.
    /// </remarks>
    private static int LinesNeeded(string text, int width, int rung)
    {
        var perLine = width * 22 / (rung * 10);
        Assert.True(perLine > 8, $"a {width} px box holds {perLine} characters at rung {rung} — that is not a column");
        var lines = 1;
        var used = 0;
        foreach (var word in text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
        {
            var add = used == 0 ? word.Length : word.Length + 1;
            if (used + add > perLine) { lines++; used = word.Length; }
            else used += add;
        }
        return lines;
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_every_keystone_the_world_can_reveal_fits_the_dispatch_reading_pane(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            var pane = Pane();
            var seen = 0;
            foreach (var source in Keystones.Sources)
            {
                var k = Keystones.ById(source.KeystoneId);
                Assert.NotNull(k);

                // The words the pane actually paints — asked of the copy the game renders, so the two
                // cannot drift. The body is built by the one sentence-maker all four producers share.
                var letter = Dispatches.Keystone(k!.Id, 1);
                var head = DispatchCopy.Headline(letter);
                var body = DispatchCopy.Body(letter);
                Assert.Equal(DispatchCopy.KeystoneRevealDetail(k), body);

                var used = LinesNeeded(head, pane.Width, UiTypography.Headline) * UiTypography.Pitch(UiTypography.Headline)
                           + UiMetrics.Space(10)
                           + LinesNeeded(body, pane.Width, UiTypography.Body) * UiTypography.Pitch(UiTypography.Body);
                Assert.True(used <= pane.Height,
                            $"{k.Id}: the reveal needs {used} px of the {pane.Height} px reading pane at {percent}% — "
                            + $"it would run off the bottom. ({body.Length} chars) \"{body}\"");
                seen++;
            }

            Assert.True(seen > 0, "no keystone has a world source — the reveal has nothing to show");
        }
        finally { UiMetrics.Apply(100); }
    }

    [Fact]
    public void test_the_headline_never_carries_the_description()
    {
        // The other half of the ruling, asserted from this side: whatever the strip says, the body
        // copy is the letter's job. A headline that grew a description would silently re-create the
        // overflow the split was made to fix.
        UiMetrics.Apply(100);
        try
        {
            var pane = Pane();
            foreach (var source in Keystones.Sources)
            {
                var k = Keystones.ById(source.KeystoneId)!;
                var head = DispatchCopy.Headline(Dispatches.Keystone(k.Id, 1));

                Assert.Equal($"NEW KEYSTONE — {k.Name}", head);
                Assert.DoesNotContain(k.Blurb, head);
                Assert.True(LinesNeeded(head, pane.Width, UiTypography.Headline) == 1,
                            $"{k.Id}: the reveal headline is not one line — \"{head}\"");
            }
        }
        finally { UiMetrics.Apply(100); }
    }
}
