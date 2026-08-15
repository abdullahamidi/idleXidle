using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Expeditions;

/// <summary>
/// The chest page cannot lie about what is in the chest.
/// </summary>
/// <remarks>
/// A screen that describes a reward is a second statement of the rules, and this codebase has been
/// bitten repeatedly by a second statement drifting from the first. So these tests do not check that
/// the dossier says something plausible — they OPEN thousands of real chests and check that every
/// promise it made actually held.
/// </remarks>
public class ChestDossierTest
{
    private readonly ITestOutputHelper _out;

    public ChestDossierTest(ITestOutputHelper output) => _out = output;

    private static Chest Make(Rarity grade, int tier = 10, Source? element = Source.Nature,
                              string? region = "cinderworks", float runTilt = 1f)
        => new() { Rarity = grade, Tier = tier, Element = element, Region = region, RunTilt = runTilt };

    [Fact]
    public void test_the_guaranteed_floor_is_never_broken_by_a_real_open()
    {
        // THE PROMISE THE WHOLE PAGE RESTS ON. If a chest advertising "guaranteed EPIC or better" ever
        // yields a Rare, the page has done more damage than showing nothing would have.
        var rng = new Random(41);
        var checks = 0;

        foreach (var grade in Enum.GetValues<Rarity>())
            for (var trial = 0; trial < 400; trial++)
            {
                var chest = Make(grade, tier: 5 + trial % 30);
                var dossier = ChestDossiers.For(chest);
                var reward = Chests.Open(chest, rng);

                foreach (var item in reward.Items)
                {
                    checks++;
                    Assert.True(item.Rarity >= dossier.GuaranteedFloor,
                        $"a {grade} chest promised {dossier.GuaranteedFloor} or better and gave "
                        + $"{item.Rarity}.");
                }
            }

        _out.WriteLine($"   {checks} items checked against their chest's advertised floor");
        Assert.True(checks > 0, "no items were produced at all — the test proved nothing.");
    }

    [Fact]
    public void test_the_advertised_item_and_material_ranges_actually_hold()
    {
        // The other half: a range the player reads as a bound has to BE a bound. An off-by-one here
        // would read to a player as the game shorting them.
        var rng = new Random(43);

        foreach (var grade in Enum.GetValues<Rarity>())
            for (var trial = 0; trial < 300; trial++)
            {
                var chest = Make(grade, tier: 1 + trial % 40);
                var d = ChestDossiers.For(chest);
                var reward = Chests.Open(chest, rng);

                Assert.InRange(reward.Items.Count, d.MinItems, d.MaxItems);
                Assert.InRange(reward.Materials, d.MinMaterials, d.MaxMaterials);
            }
    }

    [Fact]
    public void test_a_chest_with_no_real_floor_says_so_rather_than_promising_common()
    {
        // "Guaranteed COMMON or better" is technically true and reads as a guarantee, which is worse
        // than admitting there is none. Common and Uncommon chests share a Common floor.
        Assert.Contains("gamble", ChestDossiers.For(Make(Rarity.Common)).FloorLine);
        Assert.Contains("gamble", ChestDossiers.For(Make(Rarity.Uncommon)).FloorLine);

        Assert.Contains("UNCOMMON", ChestDossiers.For(Make(Rarity.Rare)).FloorLine);
        Assert.Contains("RARE", ChestDossiers.For(Make(Rarity.Epic)).FloorLine);
        Assert.Contains("EPIC", ChestDossiers.For(Make(Rarity.Legendary)).FloorLine);
    }

    [Fact]
    public void test_the_run_tilt_line_is_silent_when_the_run_was_ordinary()
    {
        // A line that appears on every chest saying "+0%" is a line players learn to skip, and skipping
        // it is exactly how they would miss the case where the tilt is real.
        Assert.Null(ChestDossiers.For(Make(Rarity.Rare, runTilt: 1f)).RunLine);
        Assert.Null(ChestDossiers.For(Make(Rarity.Rare, runTilt: 1.01f)).RunLine);

        Assert.Contains("better", ChestDossiers.For(Make(Rarity.Rare, runTilt: 1.3f)).RunLine!);
        Assert.Contains("worse", ChestDossiers.For(Make(Rarity.Rare, runTilt: 0.8f)).RunLine!);
    }

    [Fact]
    public void test_every_chest_produces_lines_that_fit_a_panel()
    {
        // The page is a fixed-width list. A line that runs long is the defect this codebase produces
        // most, and it is invisible until someone looks at the screen.
        foreach (var grade in Enum.GetValues<Rarity>())
            foreach (var element in new Source?[] { null, Source.Body, Source.Machine })
                foreach (var region in new[] { null, "cinderworks", "verdant_hollow", "pale_choir" })
                {
                    var d = ChestDossiers.For(Make(grade, tier: 37, element: element, region: region));
                    foreach (var line in d.Lines)
                    {
                        Assert.False(string.IsNullOrWhiteSpace(line), $"{grade} produced a blank line.");
                        Assert.True(line.Length <= 52,
                            $"\"{line}\" is {line.Length} chars — too long for the chest card.");
                    }
                }
    }

    [Fact]
    public void test_the_pile_is_ordered_best_first_to_match_the_open_button()
    {
        // The Forge's OPEN button opens the BEST chest. A list in any other order would put the button's
        // target somewhere other than the top of what the player is reading.
        var chests = new[]
        {
            Make(Rarity.Common, tier: 40), Make(Rarity.Legendary, tier: 2),
            Make(Rarity.Rare, tier: 30), Make(Rarity.Rare, tier: 31),
        };

        var sorted = ChestDossiers.BestFirst(chests);

        Assert.Equal(Rarity.Legendary, sorted[0].Rarity);
        Assert.Equal(Rarity.Rare, sorted[1].Rarity);
        Assert.Equal(31, sorted[1].Tier);          // ties break on depth
        Assert.Equal(Rarity.Common, sorted[^1].Rarity);
    }

    [Fact]
    public void test_the_tally_counts_every_chest_exactly_once()
    {
        var chests = new List<Chest>
        {
            Make(Rarity.Rare), Make(Rarity.Rare), Make(Rarity.Legendary), Make(Rarity.Common),
        };

        var tally = ChestDossiers.Tally(chests);

        Assert.Equal(chests.Count, tally.Sum(t => t.Count));
        Assert.Equal(Rarity.Legendary, tally[0].Grade);          // best first, same as the list
        Assert.Equal(2, tally.Single(t => t.Grade == Rarity.Rare).Count);
    }

    [Fact]
    public void test_the_dossier_reports_what_the_region_is_known_for()
    {
        // The whole reason a chest carries its region. If this came back empty the page would show a
        // regional lean that the roll applies but the player cannot see.
        var d = ChestDossiers.For(Make(Rarity.Epic, region: "cinderworks"));

        _out.WriteLine($"   cinderworks chest: {d.RegionLine}  ({d.RegionBlurb})");

        Assert.NotEmpty(d.Favoured);
        Assert.DoesNotContain("No regional lean", d.RegionLine);
        Assert.False(string.IsNullOrWhiteSpace(d.RegionBlurb));
    }
}
