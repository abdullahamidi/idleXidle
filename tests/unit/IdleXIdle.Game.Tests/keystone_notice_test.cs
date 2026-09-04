using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A KEYSTONE REVEAL FITS THE PRESENTATION IT WAS GIVEN.
/// </summary>
/// <remarks>
/// <para>
/// The reveal is deliberately split across two surfaces, and each has a different contract:
/// </para>
/// <list type="bullet">
///   <item>the MAP's one-line strip carries only a headline — <c>&lt;REGION&gt; CONQUERED!  NEW
///   KEYSTONE — &lt;NAME&gt;.</c> — and <see cref="MapStripTests"/> owns that half;</item>
///   <item>the notice toast carries the full description, WRAPPED, and this file owns that half.</item>
/// </list>
/// <para>
/// The split exists because the two failure modes are opposite. Forcing a 164-character description
/// into the strip either shrinks the type or spills it across the region cards; dropping the
/// description entirely leaves a reveal that names a keystone and never says what it does, and the
/// word IRONCLAD teaches nobody anything.
/// </para>
/// <para>
/// The toast's own risk is quieter and is the reason this file exists: <c>DrawNoticeToast</c> takes
/// only <c>NoticeBodyLines</c> of wrapped text, so a description one line too long is TRUNCATED with
/// no error, no ellipsis and no test — the reveal would simply stop mid-sentence. The budget was
/// sized against a stated 164-characters-against-an-80-character-line, but that figure lived in a
/// comment, where it cannot fail. It is an assertion now, so a keystone authored longer than the
/// plate can hold breaks the build instead of quietly losing its last clause.
/// </para>
/// </remarks>
public class KeystoneNoticeTests
{
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>The renderer's own line budget, read rather than restated so the two cannot drift.</summary>
    private static int BodyLines =>
        (int)(typeof(Game1).GetField("NoticeBodyLines", Statics)
              ?? throw new Xunit.Sdk.XunitException(
                  "Game1 has no NoticeBodyLines — the notice plate was reshaped without its test."))
            .GetRawConstantValue()!;

    /// <summary>
    /// The measured width of the toast's body line, in characters. Real font measurement needs a
    /// graphics device, so this is the same arithmetic claim <see cref="MapStripTests"/> makes about
    /// the strip: a conservative per-line budget the renderer's own comment states, asserted here
    /// rather than trusted. Conservative is the safe direction — a real line fits MORE than 80.
    /// </summary>
    private const int CharsPerLine = 80;

    private static int LinesNeeded(string text)
    {
        // Greedy word wrap, the shape WrapBig uses: a word that does not fit starts the next line.
        var lines = 1;
        var used = 0;
        foreach (var word in text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
        {
            var add = used == 0 ? word.Length : word.Length + 1;
            if (used + add > CharsPerLine) { lines++; used = word.Length; }
            else used += add;
        }
        return lines;
    }

    [Fact]
    public void test_every_keystone_the_world_can_reveal_fits_the_notice_plate()
    {
        var budget = BodyLines;
        Assert.True(budget >= 2, "a one-line body is the ellipsised plate this split replaced");

        var seen = 0;
        foreach (var source in Keystones.Sources)
        {
            var k = Keystones.ById(source.KeystoneId);
            Assert.NotNull(k);

            // The detail the host actually posts: where it came from, then what it does.
            // (Game1.RevealKeystone: PostNotice($"NEW KEYSTONE — {k.Name}", $"{where} {k.Blurb}").)
            var where = $"{Regions.Find(source.RegionId)?.Name ?? source.RegionId} gave it up.";
            var detail = $"{where} {k!.Blurb}";

            var need = LinesNeeded(detail);
            Assert.True(need <= budget,
                        $"{k.Id}: the reveal body wraps to {need} lines and the plate holds {budget} — "
                        + $"it would be truncated mid-sentence. ({detail.Length} chars) \"{detail}\"");
            seen++;
        }

        Assert.True(seen > 0, "no keystone has a world source — the reveal has nothing to show");
    }

    [Fact]
    public void test_the_headline_never_carries_the_description()
    {
        // The other half of the ruling, asserted from this side: whatever the strip says, the body
        // copy is the notice's job. A headline that grew a description would silently re-create the
        // overflow the split was made to fix.
        foreach (var source in Keystones.Sources)
        {
            var k = Keystones.ById(source.KeystoneId)!;
            var head = $"NEW KEYSTONE — {k.Name}";

            Assert.DoesNotContain(k.Blurb, head);
            Assert.True(LinesNeeded(head) == 1, $"{k.Id}: the reveal headline is not one line — \"{head}\"");
        }
    }
}
