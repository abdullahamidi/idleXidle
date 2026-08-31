using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// The build stamp: never empty, and the short form really is a shortening of the full one.
/// </summary>
public class BuildStampTest
{
    [Fact]
    public void test_build_stamp_is_never_empty_and_short_form_fits_a_screen_corner()
    {
        // Arrange / Act — the stamps are computed once at type load.
        var full = BuildStamp.Full;
        var shortForm = BuildStamp.Short;

        // Assert — both present, the short one bounded, and the short one a prefix-truncation of the
        // full one (same version, commit cut to 8 characters when a commit is present).
        Assert.False(string.IsNullOrWhiteSpace(full));
        Assert.False(string.IsNullOrWhiteSpace(shortForm));
        Assert.True(shortForm.Length <= 32, $"short stamp too long for a screen corner: {shortForm}");
        Assert.StartsWith(shortForm.Split('+')[0], full);
        if (full.Contains('+'))
            Assert.True(shortForm.Split('+')[1].Length <= 8, $"commit not shortened: {shortForm}");
    }
}
