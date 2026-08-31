using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Encounters;

/// <summary>
/// Every region drops something of its own, and the tilt is real rather than a caption.
/// </summary>
public class RegionDropsTest
{
    private readonly ITestOutputHelper _out;

    public RegionDropsTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_every_region_in_the_world_has_a_drop_profile()
    {
        // RegionDrops.For falls back to a neutral profile rather than throwing, so a region added to
        // the world and forgotten here would silently go back to dropping the same uniform loot as
        // everywhere else — the exact thing this feature exists to end, failing quietly.
        foreach (var region in Regions.All)
        {
            var profile = RegionDrops.For(region.Id);
            _out.WriteLine($"   {region.Name,-20} {string.Join(", ", profile.FavouredNames)}  ({profile.Blurb})");

            Assert.True(profile.Favoured.Count > 0,
                $"{region.Name} ({region.Id}) has no drop profile — it falls back to uniform loot.");
            Assert.False(string.IsNullOrWhiteSpace(profile.Blurb),
                $"{region.Name} has a profile the chest screen cannot describe.");
            // The map lists the favoured slots by name, one per line: every one needs a plain,
            // capitalised name the font can draw — no enum leaking through as "ABILITYFOCUS".
            Assert.Equal(profile.Favoured.Count, profile.FavouredNames.Count);
            foreach (var name in profile.FavouredNames)
            {
                Assert.False(string.IsNullOrWhiteSpace(name), $"{region.Name} favours a slot with no name.");
                Assert.Equal(name.ToUpperInvariant(), name);
                Assert.DoesNotContain("ABILITYFOCUS", name);
                Assert.All(name, ch => Assert.True(ch < 128, $"{name} uses a glyph the font cannot draw"));
            }
        }
    }

    [Fact]
    public void test_the_profile_table_has_no_entries_for_regions_that_do_not_exist()
    {
        // The mirror of the test above. A stale id here is a profile nothing can ever reach, and it
        // reads as coverage while providing none.
        var real = Regions.All.Select(r => r.Id).ToHashSet();
        foreach (var id in RegionDrops.CoveredIds)
            Assert.True(real.Contains(id), $"RegionDrops covers '{id}', which is not a region.");
    }

    [Fact]
    public void test_a_region_actually_drops_more_of_what_it_is_known_for()
    {
        // The claim under test is a DROP RULE, not a blurb. Roll a lot of loot in one place and check
        // its favoured slots really do come up more often than they would anywhere else.
        const int rolls = 4000;
        var profile = RegionDrops.For("cinderworks");   // a forge: weapons and focuses

        var rng = new Random(11);
        var favoured = 0;
        var wearables = 0;

        for (var i = 0; i < rolls; i++)
        {
            var ctx = new KillContext { PowerTier = 10, FavouredTypes = profile.Favoured };
            foreach (var item in LootSystem.Roll(ctx, rng, LootTuning.Default))
            {
                if (Gear.SlotFor(item.BaseType) is null) continue;
                wearables++;
                if (profile.Favoured.Contains(item.BaseType)) favoured++;
            }
        }

        var share = (float)favoured / wearables;

        // Two of eight slots are favoured, so uniform would be 25%. The rule sends half the wearables
        // through the favoured pool, which should land near 62%.
        _out.WriteLine($"cinderworks: {share:0.0%} of wearables were weapons or focuses "
                       + $"({favoured} of {wearables}); uniform would be 25%");

        Assert.True(share > 0.45f,
            $"the favoured slots came up {share:0.0%} of the time — barely above the {2f / 8f:0%} they "
            + "would get anywhere. The profile is a caption, not a drop rule.");

        // And never exclusive: a player must be able to finish a set without touring the map.
        Assert.True(share < 0.85f,
            $"the favoured slots came up {share:0.0%} of the time — a region that drops almost nothing "
            + "else forces a tour of the world to complete a loadout.");
    }

    [Fact]
    public void test_later_regions_carry_a_better_rarity_floor()
    {
        // Pushing to a new place should be a loot decision as well as a difficulty one, and the order
        // of the tilts has to match the order of the chain — otherwise the map's "recommended power"
        // ladder and its reward ladder point in different directions.
        var chain = Regions.All.Select(r => (r.Name, RegionDrops.For(r.Id).RarityTilt)).ToList();
        foreach (var (name, tilt) in chain) _out.WriteLine($"   {name,-20} tilt {tilt,5:0.0}");

        for (var i = 1; i < chain.Count; i++)
            Assert.True(chain[i].RarityTilt >= chain[i - 1].RarityTilt,
                $"{chain[i].Name} sits later than {chain[i - 1].Name} and rewards less.");
    }
}
