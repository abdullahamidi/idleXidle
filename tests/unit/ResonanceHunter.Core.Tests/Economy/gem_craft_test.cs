using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// STAT GEMS: sockets by rarity, gems with a stable stat and their own level, set and crushed at the
/// Forge — the item-system redesign's customisation layer.
/// </summary>
public class GemCraftTest
{
    private static ItemInstance Host(Rarity rarity, string id = "host_1") => new()
    {
        InstanceId = id, BaseType = ItemBaseType.Weapon, Rarity = rarity, SellValue = 30, ItemLevel = 8,
    };

    private static ItemInstance Gem(string id = "gem_1", int level = 3) => new()
    {
        InstanceId = id, BaseType = ItemBaseType.Gem, Rarity = Rarity.Rare, SellValue = 30, ItemLevel = level,
    };

    [Fact]
    public void test_gem_craft_socket_counts_follow_rarity()
    {
        // Arrange + Act + Assert: "rare eşyada 1, epicte 2 ve legendary'de 3 slot".
        Assert.Equal(0, GemCraft.SocketCount(Rarity.Common));
        Assert.Equal(0, GemCraft.SocketCount(Rarity.Uncommon));
        Assert.Equal(1, GemCraft.SocketCount(Rarity.Rare));
        Assert.Equal(2, GemCraft.SocketCount(Rarity.Epic));
        Assert.Equal(3, GemCraft.SocketCount(Rarity.Legendary));
    }

    [Fact]
    public void test_gem_craft_socket_fills_a_slot_and_refuses_past_capacity()
    {
        // Arrange
        var host = Host(Rarity.Epic);

        // Act
        var (one, r1) = GemCraft.Socket(host, Gem("gem_a"));
        var (two, r2) = GemCraft.Socket(one!, Gem("gem_b"));
        var (three, r3) = GemCraft.Socket(two!, Gem("gem_c"));

        // Assert: two sockets on an Epic — the third is refused with a reason, not an exception.
        Assert.Null(r1);
        Assert.Null(r2);
        Assert.Equal(2, two!.Gems.Count);
        Assert.Null(three);
        Assert.False(string.IsNullOrWhiteSpace(r3));
        Assert.Equal(host.InstanceId, two.InstanceId);   // socketing never changes which item it is
    }

    [Fact]
    public void test_gem_craft_socket_refuses_non_gems_and_socketless_hosts()
    {
        Assert.NotNull(GemCraft.Socket(Host(Rarity.Rare), Host(Rarity.Rare, "not_a_gem")).Rejection);
        Assert.NotNull(GemCraft.Socket(Host(Rarity.Common), Gem()).Rejection);
    }

    [Fact]
    public void test_gem_craft_crush_destroys_the_gem_and_frees_the_slot()
    {
        // Arrange
        var host = GemCraft.Socket(Host(Rarity.Rare), Gem("gem_x")).Product!;

        // Act
        var result = GemCraft.Crush(host, 0);

        // Assert: the slot opens and a fresh gem fits again; the crushed gem is reported, not returned.
        Assert.NotNull(result);
        Assert.Empty(result!.Value.Product.Gems);
        Assert.Equal("gem_x", result.Value.Crushed.InstanceId);
        Assert.Null(GemCraft.Socket(result.Value.Product, Gem("gem_y")).Rejection);
    }

    [Fact]
    public void test_gem_craft_socket_and_crush_never_mutate_the_original_list()
    {
        // A record `with`-copy shares its lists — Socket/Crush must build NEW ones, or an old copy of
        // the item would see gems appear and vanish under it.
        var host = Host(Rarity.Legendary);
        var socketed = GemCraft.Socket(host, Gem()).Product!;

        Assert.Empty(host.Gems);
        Assert.Single(socketed.Gems);
    }

    [Fact]
    public void test_gem_craft_magnitude_grows_with_gem_level_and_stat_is_stable()
    {
        // Arrange
        var low = Gem("gem_same", 1);
        var high = Gem("gem_same", 9);

        // Assert: level buys magnitude; the stat is the gem's identity and never moves.
        Assert.Equal(GemCraft.StatOf(low), GemCraft.StatOf(high));
        Assert.True(GemCraft.Magnitude(high) > GemCraft.Magnitude(low));

        // And the growth SATURATES — REFINE-adjacent curves are all capped, this one included.
        Assert.Equal(GemCraft.Magnitude(Gem("gem_same", 60)), GemCraft.Magnitude(Gem("gem_same", 600)));
    }

    [Fact]
    public void test_gem_craft_socketed_gems_reach_the_hunters_totals()
    {
        // Arrange: two identical hunters; one wears the same weapon with a gem set in it.
        var plainHost = Host(Rarity.Rare, "host_worn");
        var gem = Gem("gem_worn", 5);
        var gemmedHost = GemCraft.Socket(plainHost, gem).Product!;

        var plain = new Hunter();
        plain.Equip(plainHost);
        var gemmed = new Hunter();
        gemmed.Equip(gemmedHost);

        // Assert: the gem's stat channel moved by exactly its magnitude.
        var stat = GemCraft.StatOf(gem);
        Assert.Equal(plain.AffixTotal(stat) + GemCraft.Magnitude(gem), gemmed.AffixTotal(stat), 5);
    }

    [Fact]
    public void test_gem_craft_minted_gems_spread_across_every_stat()
    {
        var rng = new Random(17);
        var seen = Enumerable.Range(0, 300)
            .Select(_ => GemCraft.StatOf(GemCraft.MintGem(10, rng)))
            .ToHashSet();

        Assert.Equal(Enum.GetValues<AffixStat>().Length, seen.Count);
    }

    [Fact]
    public void test_gem_craft_chests_supply_gems_on_their_own_channel()
    {
        // Arrange
        var rng = new Random(23);
        var chest = new Chest { Rarity = Rarity.Rare, Tier = 12 };

        // Act: open many — gems must appear, and NEVER inside the promised item count.
        var opens = Enumerable.Range(0, 200).Select(_ => Chests.Open(chest, rng)).ToList();

        // Assert
        Assert.Contains(opens, o => o.Gems.Count > 0);
        Assert.All(opens, o => Assert.DoesNotContain(o.Items, GemCraft.IsGem!));
    }

    /// <summary>A gem whose id-derived stat is exactly <paramref name="want"/> — the only way to aim
    /// <see cref="GemCraft.StatOf"/>, which reads the instance id and nothing else.</summary>
    private static ItemInstance GemOf(AffixStat want, int level = 4)
    {
        for (var n = 0; n < 5000; n++)
        {
            var gem = Gem($"gem_pick_{n}", level);
            if (GemCraft.StatOf(gem) == want) return gem;
        }
        throw new InvalidOperationException($"no id hashed to {want} in 5000 tries");
    }

    [Fact]
    public void test_gem_craft_describe_carries_each_stats_own_unit()
    {
        // A CRITICAL-CHANCE gem is percentage POINTS of hits; DEFENCE is a FLAT number; the four
        // multiplicative channels are percentages. The Forge once advertised a +5.6 flat defence gem
        // as "+560% DEFENCE" — one formatter, one word list, so no gem surface can re-invent that.
        var crit = GemOf(AffixStat.Crit);
        var defence = GemOf(AffixStat.Defense);
        var damage = GemOf(AffixStat.Damage);

        Assert.Equal($"+{GemCraft.Magnitude(crit):0.0}% CRITICAL CHANCE", GemCraft.Grant(crit));
        Assert.Equal($"+{GemCraft.Magnitude(defence):0} DEFENCE", GemCraft.Grant(defence));
        Assert.Equal($"+{GemCraft.Magnitude(damage) * 100f:0}% DAMAGE", GemCraft.Grant(damage));

        // The percentage sign belongs to critical chance and NOT to defence — the exact inversion.
        Assert.Contains("%", GemCraft.Grant(crit), StringComparison.Ordinal);
        Assert.DoesNotContain("%", GemCraft.Grant(defence));
    }

    [Fact]
    public void test_gem_craft_describe_names_the_gem_its_level_and_what_it_gives()
    {
        var gem = GemOf(AffixStat.Health, level: 7);

        var line = GemCraft.Describe(gem);

        Assert.StartsWith(GemCraft.NameOf(gem), line, StringComparison.Ordinal);
        Assert.Contains("LEVEL 7", line, StringComparison.Ordinal);
        Assert.Contains(GemCraft.Grant(gem), line, StringComparison.Ordinal);
        // The font gate allows ASCII plus a short list of marks; the middle dot is on it, nothing else here is.
        Assert.All(line, ch => Assert.True(ch < 128 || ch == '·', $"line carries an unsupported glyph: {ch}"));
    }

    [Fact]
    public void test_item_affixes_stat_word_covers_every_stat()
    {
        // The WORD had three copies before this (two screens and Describe). The count assert is the
        // point: adding a stat without a word here fails HERE rather than printing "SkillRate" on a card.
        var expected = new Dictionary<AffixStat, string>
        {
            [AffixStat.Damage] = "DAMAGE",
            [AffixStat.Health] = "HEALTH",
            [AffixStat.SkillRate] = "SKILL RATE",
            [AffixStat.Haul] = "LOOT",
            [AffixStat.Crit] = "CRITICAL CHANCE",
            [AffixStat.Defense] = "DEFENCE",
        };

        Assert.Equal(Enum.GetValues<AffixStat>().Length, expected.Count);
        foreach (var (stat, word) in expected) Assert.Equal(word, ItemAffixes.StatWord(stat));

        // And the shared affix line is the same two halves, so a screen cannot drift from a tooltip.
        foreach (var stat in Enum.GetValues<AffixStat>())
        {
            var affix = new ItemAffix(stat, 0.25f);
            Assert.Equal($"{ItemAffixes.GrantLabel(stat, 0.25f)} {ItemAffixes.StatWord(stat)}",
                         ItemAffixes.Describe(affix));
        }
    }

    [Fact]
    public void test_gem_craft_socketed_gems_survive_a_save_round_trip()
    {
        // Arrange: a worn-style item with a gem inside, through the REAL capture + serializer.
        var host = GemCraft.Socket(Host(Rarity.Epic, "host_save"), Gem("gem_save", 6)).Product!;
        var save = SaveSystem.Capture(
            new Hunter(), new ResonanceHunter.Core.Automation.Region("verdant_hollow", 15),
            Array.Empty<ResonanceHunter.Core.Automation.Creature>(), new[] { host }, 0, nowMs: 0);

        // Act
        var back = SaveSystem.RestoreInventory(
            SaveSystem.Deserialize(SaveSystem.Serialize(save), nowMs: 0).Save!).Single();

        // Assert: the gem came back whole — id, level, stat, magnitude.
        var gem = Assert.Single(back.Gems);
        Assert.Equal("gem_save", gem.InstanceId);
        Assert.Equal(6, gem.ItemLevel);
        Assert.Equal(GemCraft.StatOf(Gem("gem_save", 6)), GemCraft.StatOf(gem));
    }
}

