using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE MAP STRIP IS ONE LINE, and it is one line by construction rather than by convention.
/// </summary>
/// <remarks>
/// The plate's height is a constant — one small button, with a breath above and below — so a message
/// carrying a newline does not grow it: the extra lines are drawn below its foot, across the region
/// cards. A conquest keystone reveal did exactly that, writing four lines of doctrine into it. The
/// reveal moved to the notice toast, whose body wraps and whose plate grows to the rungs it draws;
/// the strip kept the compact headline. These tests hold both halves of that split: the flattening
/// guard no future caller can get past, and the conquest line that has to fit the plate at every
/// profile. <see cref="MapScreen"/> is exercised without a <c>GraphicsDevice</c> — its constructor
/// only stores its kit, and nothing here draws.
/// </remarks>
public class MapStripTests
{
    private static MapScreen Fresh() => new(null!) { World = new World(), ActiveRegion = VerdantHollow.RegionId };

    [Fact]
    public void test_a_message_with_line_breaks_is_flattened_to_one_line()
    {
        const string spilling = "VERDANT HOLLOW CONQUERED!\n\nNEW KEYSTONE DISCOVERED\nECHO\nEVERY SKILL FIRES TWICE.";

        var line = MapScreen.OneLine(spilling);

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.Equal("VERDANT HOLLOW CONQUERED!  NEW KEYSTONE DISCOVERED  ECHO  EVERY SKILL FIRES TWICE.", line);
    }

    [Fact]
    public void test_a_line_that_is_already_one_line_keeps_its_own_spacing()
    {
        // The double space between the strip's two sentences is the copy's own separator, not an
        // accident — flattening must not tidy it away.
        const string already = "VERDANT HOLLOW CONQUERED!  NEW KEYSTONE — ECHO.";

        Assert.Equal(already, MapScreen.OneLine(already));
    }

    [Fact]
    public void test_a_message_of_nothing_but_line_breaks_leaves_the_strip_with_nothing_to_say()
    {
        // The strip costs nothing when the world has nothing to say, and whitespace is nothing.
        Assert.Equal("", MapScreen.OneLine("\n  \r\n "));
        Assert.Equal("", MapScreen.OneLine(null));
        Assert.Equal("", MapScreen.OneLine(""));
    }

    [Fact]
    public void test_the_screen_draws_the_flattened_line_whatever_the_host_hands_it()
    {
        var screen = Fresh();

        screen.Message = "A LINE\nAND ANOTHER";

        Assert.DoesNotContain('\n', screen.StripLine);
        Assert.Equal("A LINE  AND ANOTHER", screen.StripLine);
    }

    [Fact]
    public void test_a_conquest_that_teaches_a_keystone_names_it_on_one_line()
    {
        var echo = Keystones.ById("echo");
        Assert.NotNull(echo);

        var line = MapScreen.ConquestHeadline(VerdantHollow.RegionId, Regions.Next(VerdantHollow.RegionId), echo);

        Assert.Equal("VERDANT HOLLOW CONQUERED!  NEW KEYSTONE — ECHO.", line);
        Assert.DoesNotContain('\n', line);
    }

    /// <summary>
    /// The plate holds about seventy-five characters at UI SCALE 150 — the profile at which the type is
    /// largest and the chart narrowest. Every conquest the world can produce has to be readable whole at
    /// that width, so the longest of the six is measured here rather than trusted.
    /// </summary>
    [Fact]
    public void test_every_conquest_headline_the_world_can_produce_fits_the_plate()
    {
        const int plateAt150 = 75;
        var seen = 0;

        foreach (var region in Regions.All)
        {
            var source = Keystones.Sources.FirstOrDefault(
                s => s.RegionId == region.Id && s.Rung == WorldRung.Conquest);
            Assert.NotNull(source);                                  // every region teaches one on conquest
            var taught = Keystones.ById(source!.KeystoneId);
            Assert.NotNull(taught);

            var line = MapScreen.ConquestHeadline(region.Id, Regions.Next(region.Id), taught);

            Assert.Contains(taught!.Name, line);                     // the NAME is the payload, never cut
            Assert.DoesNotContain('\n', line);
            Assert.True(line.Length <= plateAt150,
                        $"{region.Id}: the strip line is {line.Length} characters — \"{line}\"");
            seen++;
        }

        Assert.Equal(Regions.All.Count, seen);
    }

    [Fact]
    public void test_a_conquest_that_teaches_nothing_still_says_what_opened()
    {
        var line = MapScreen.ConquestHeadline(VerdantHollow.RegionId, Regions.Next(VerdantHollow.RegionId), taught: null);

        Assert.Equal("VERDANT HOLLOW CONQUERED!  CINDERWORKS IS OPEN.", line);
    }

    [Fact]
    public void test_the_last_conquest_has_no_next_region_to_name()
    {
        var last = Regions.All[^1];
        Assert.Null(Regions.Next(last.Id));

        Assert.Equal("THE WORLD IS YOURS.", MapScreen.ConquestHeadline(last.Id, opened: null, taught: null));
    }
}
