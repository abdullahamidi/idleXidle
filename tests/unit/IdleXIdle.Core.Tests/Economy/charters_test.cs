using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// Single-use Forge papers: they drop, they are spent whole, and they survive a reload.
/// </summary>
/// <remarks>
/// Playtest, two passes ago: <i>"Waveler de materyal-yükseltme-reforge-salvage-merge kağıdı gibi
/// yardımcı itemler atsın."</i> It was deferred twice, so the tests carry the whole feature: a drop
/// nobody receives, a stock that vanishes on reload, or a paper spent on a refusal would each turn this
/// into another thing that exists in the code and not in the game.
/// </remarks>
public class ChartersTest
{
    private readonly ITestOutputHelper _out;

    public ChartersTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_every_charter_says_what_it_is_and_what_it_does()
    {
        // Text lives beside the enum so a screen cannot become a second, drifting statement of the rule.
        foreach (var c in Enum.GetValues<Charter>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Charters.Name(c)), $"{c} has no name.");
            Assert.False(string.IsNullOrWhiteSpace(Charters.Blurb(c)), $"{c} has no blurb.");
            Assert.False(string.IsNullOrWhiteSpace(Charters.Short(c)), $"{c} has no short label.");
            Assert.False(string.IsNullOrWhiteSpace(Charters.Plain(c)), $"{c} has no plain-words line.");

            // A blurb that does not say what it PERMITS is a flavour line, not an explanation.
            Assert.Contains("One", Charters.Blurb(c));
        }

        // The plain line speaks the Forge's BUTTON words, so the wallet row and the button it pays for
        // read as the same thing — a REFINE CHART pays for the button labelled UPGRADE.
        Assert.Contains("UPGRADE", Charters.Plain(Charter.Refine));
        Assert.Contains("RE-ROLL", Charters.Plain(Charter.Reforge));
        Assert.Contains("SALVAGE", Charters.Plain(Charter.Salvage));
    }

    [Fact]
    public void test_charters_drop_rarely_and_never_before_the_forge_exists()
    {
        // A charter is a whole Forge operation, so it must stay rarer than the tiered material — and it
        // must not arrive before the player has met the Forge, the mistake the Warren was making by
        // paying out before it unlocked.
        var rng = new Random(21);
        const int waves = 200_000;
        var found = 0;
        var earliest = int.MaxValue;

        for (var i = 0; i < waves; i++)
        {
            var wave = 1 + i % 80;
            if (WaveSpoils.Roll(wave, rng).Charter is null) continue;
            found++;
            earliest = Math.Min(earliest, wave);
        }

        var rate = found / (float)waves;
        _out.WriteLine($"a charter drops on {rate:0.0%} of waves; earliest seen at wave {earliest}");

        Assert.InRange(rate, 0.015f, 0.030f);
        Assert.True(earliest >= WaveSpoils.ChartersFromWave,
            $"a charter dropped at wave {earliest}, before the Forge is open.");
    }

    [Fact]
    public void test_a_charter_never_replaces_the_material_the_wave_owed()
    {
        // A drop that sometimes swaps one reward for another reads as the game taking something away.
        var rng = new Random(23);
        for (var i = 0; i < 20_000; i++)
        {
            var spoil = WaveSpoils.Roll(1 + i % 90, rng);
            Assert.True(spoil.Amount >= 1, "a wave that paid a charter paid no material.");
        }
    }

    [Fact]
    public void test_every_spendable_charter_drops_and_merge_never_does()
    {
        // A charter that can never drop is a feature no player will ever reach — and a charter that
        // drops but can never be SPENT is dead paper. The manual merge tray was retired with the pile
        // screen, so MERGE charts must stop dropping (held ones are converted on load), while the three
        // charters that still have a spend must all keep appearing.
        var rng = new Random(29);
        var seen = new HashSet<Charter>();
        for (var i = 0; i < 200_000; i++)
            if (WaveSpoils.Roll(40, rng).Charter is { } c) seen.Add(c);

        _out.WriteLine("charters seen: " + string.Join(", ", seen.OrderBy(c => c.ToString())));
        Assert.DoesNotContain(Charter.Merge, seen);
        Assert.Equal(new[] { Charter.Refine, Charter.Reforge, Charter.Salvage }.OrderBy(c => c),
                     seen.OrderBy(c => c));
    }

    [Fact]
    public void test_a_charter_is_spent_whole_and_only_when_you_have_one()
    {
        var h = new Hunter();
        Assert.False(h.SpendCharter(Charter.Refine));

        h.AddCharter(Charter.Refine, 2);
        Assert.Equal(2, h.CharterCount(Charter.Refine));

        Assert.True(h.SpendCharter(Charter.Refine));
        Assert.Equal(1, h.CharterCount(Charter.Refine));
        Assert.True(h.SpendCharter(Charter.Refine));
        Assert.Equal(0, h.CharterCount(Charter.Refine));

        // And it cannot go negative, which would let one paper pay for two operations.
        Assert.False(h.SpendCharter(Charter.Refine));
        Assert.Equal(0, h.CharterCount(Charter.Refine));
    }

    [Fact]
    public void test_restoring_replaces_the_stock_rather_than_adding_to_it()
    {
        // A reload must not double what a player holds. RestoreCharters is called from the load path,
        // and "add" would have been the easy, wrong verb.
        var h = new Hunter();
        h.AddCharter(Charter.Merge, 3);

        h.RestoreCharters(new Dictionary<Charter, int> { [Charter.Merge] = 3 });
        Assert.Equal(3, h.CharterCount(Charter.Merge));

        h.RestoreCharters(new Dictionary<Charter, int> { [Charter.Merge] = 3 });
        Assert.Equal(3, h.CharterCount(Charter.Merge));

        // And a stock absent from the save loads as none, not as whatever was in memory.
        h.RestoreCharters(new Dictionary<Charter, int>());
        Assert.Equal(0, h.CharterCount(Charter.Merge));
    }

    [Fact]
    public void test_a_merge_chart_mixes_rarities_and_the_weakest_input_sets_the_grade()
    {
        // The chart's whole effect and its whole price. It does NOT remove the protection the
        // same-rarity rule provides — feeding a Legendary in beside two Commons still costs you the
        // difference, visibly, rather than silently eating the Legendary.
        var rng = new Random(31);
        var mixed = new[]
        {
            Item("a", Rarity.Rare), Item("b", Rarity.Uncommon), Item("c", Rarity.Epic),
        };

        var refused = Forge.Merge(mixed, rng, ForgeTuning.Default, LootTuning.Default);
        Assert.False(refused.Success);
        Assert.Contains("SAME RARITY", refused.Rejection!);

        var allowed = Forge.Merge(mixed, rng, ForgeTuning.Default, LootTuning.Default,
                                             ignoreRarity: true);
        Assert.True(allowed.Success, allowed.Rejection);

        // Lowest input is Uncommon, so the product is one above THAT — not one above the Epic.
        _out.WriteLine($"mixed Rare+Uncommon+Epic with a chart -> {allowed.Product!.Rarity}");
        Assert.Equal(Rarity.Rare, allowed.Product!.Rarity);
    }

    [Fact]
    public void test_a_matched_trio_still_merges_without_a_chart()
    {
        // The chart is additive. If lifting the rule had changed the ordinary path, every player who
        // never finds one would have been affected by a feature they do not have.
        var rng = new Random(37);
        var matched = new[]
        {
            Item("d", Rarity.Rare), Item("e", Rarity.Rare), Item("f", Rarity.Rare),
        };

        var result = Forge.Merge(matched, rng, ForgeTuning.Default, LootTuning.Default);
        Assert.True(result.Success, result.Rejection);
        Assert.Equal(Rarity.Epic, result.Product!.Rarity);
    }

    private static ItemInstance Item(string id, Rarity rarity) => new()
    {
        InstanceId = $"itm_{id}",
        BaseType = ItemBaseType.Ring,
        Rarity = rarity,
        ItemLevel = 5,
        SellValue = 10,
    };
}
