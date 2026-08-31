using System;
using System.Collections.Generic;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// The first gem is free to set; every later one costs Essence as before. The rule lives in
/// <see cref="GemCraft"/>, the memory of having used it in the save.
/// </summary>
public class FirstGemFreeTest
{
    private const long Now = 1_700_000_000_000L;

    [Fact]
    public void test_the_first_gem_is_free_and_the_second_costs_the_full_price()
    {
        foreach (var host in new[] { Rarity.Rare, Rarity.Epic, Rarity.Legendary })
        {
            Assert.Equal(0, GemCraft.SocketCost(host, freeSocketUsed: false));
            Assert.Equal(GemCraft.SocketCost(host), GemCraft.SocketCost(host, freeSocketUsed: true));
            Assert.True(GemCraft.SocketCost(host) > 0, "the ordinary socket price must still be a price");
        }
        Assert.True(GemCraft.IsFirstGemFree(freeSocketUsed: false));
        Assert.False(GemCraft.IsFirstGemFree(freeSocketUsed: true));
    }

    [Fact]
    public void test_the_free_first_gem_is_a_tuning_knob()
    {
        var off = new SocketTuning { FirstGemFree = false };
        Assert.False(GemCraft.IsFirstGemFree(freeSocketUsed: false, off));
        Assert.Equal(GemCraft.SocketCost(Rarity.Rare), GemCraft.SocketCost(Rarity.Rare, freeSocketUsed: false, off));
    }

    [Fact]
    public void test_free_socket_used_round_trips_through_the_save()
    {
        var save = new SaveGame { SavedAtMs = Now, FreeSocketUsed = true };
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(save), Now);

        Assert.True(loaded.Ok);
        Assert.True(loaded.Save!.FreeSocketUsed);
        Assert.True(SaveSystem.RestoreFreeSocketUsed(loaded.Save));

        var fresh = SaveSystem.Deserialize(SaveSystem.Serialize(new SaveGame { SavedAtMs = Now }), Now);
        Assert.False(fresh.Save!.FreeSocketUsed);
        Assert.False(SaveSystem.RestoreFreeSocketUsed(fresh.Save));
    }

    [Fact]
    public void test_an_old_save_that_already_socketed_a_gem_has_used_its_free_one()
    {
        // The field did not exist when this save was written, but a gem is sitting in one of its items:
        // that player is not on their first gem, and the loader must not hand them a free fifth.
        var legacyJson = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "Inventory": [
            {
              "InstanceId": "itm_old_helm", "BaseType": "Helm", "Rarity": 2, "SellValue": 34,
              "Gems": [ { "InstanceId": "gem_old", "BaseType": "Gem", "Rarity": 1, "SellValue": 30 } ]
            }
          ]
        }
        """;
        var loaded = SaveSystem.Deserialize(legacyJson, Now);

        Assert.True(loaded.Ok);
        Assert.False(loaded.Save!.FreeSocketUsed);                 // the loader invents nothing...
        Assert.True(SaveSystem.RestoreFreeSocketUsed(loaded.Save)); // ...the restore rule reads the gem
    }

    [Fact]
    public void test_an_old_save_with_no_gems_still_gets_its_free_first_gem()
    {
        var legacyJson = """
        {
          "Version": 2,
          "SavedAtMs": 1700000000000,
          "Inventory": [ { "InstanceId": "itm_old_helm", "BaseType": "Helm", "Rarity": 2, "SellValue": 34 } ]
        }
        """;
        var loaded = SaveSystem.Deserialize(legacyJson, Now);

        Assert.True(loaded.Ok);
        Assert.False(SaveSystem.RestoreFreeSocketUsed(loaded.Save!));
    }

    [Fact]
    public void test_a_free_first_socket_then_a_paid_second_walks_a_real_wallet()
    {
        // The whole loop at the Forge's altitude: host, two gems, a wallet with exactly one full price.
        // Gem one is set for nothing; gem two takes the price; the wallet ends empty, not negative.
        var rng = new Random(5);
        var host = new ItemInstance
        {
            InstanceId = "itm_host", BaseType = ItemBaseType.Helm, Rarity = Rarity.Epic, SellValue = 82,
        };
        var hunter = new Hunter();
        hunter.AddMaterial(Material.Essence, GemCraft.SocketCost(Rarity.Epic));
        var freeSocketUsed = false;

        var first = GemCraft.SocketCost(host.Rarity, freeSocketUsed);
        Assert.Equal(0, first);
        var (afterOne, rej1) = GemCraft.Socket(host, GemCraft.MintGem(3, rng));
        Assert.Null(rej1);
        freeSocketUsed = true;   // what the Forge records the moment a gem is set

        var second = GemCraft.SocketCost(host.Rarity, freeSocketUsed);
        Assert.Equal(GemCraft.SocketCost(Rarity.Epic), second);
        Assert.True(hunter.SpendMaterial(Material.Essence, second));
        var (afterTwo, rej2) = GemCraft.Socket(afterOne!, GemCraft.MintGem(3, rng));
        Assert.Null(rej2);

        Assert.Equal(2, afterTwo!.Gems.Count);
        Assert.Equal(0, hunter.MaterialOf(Material.Essence));
    }
}
