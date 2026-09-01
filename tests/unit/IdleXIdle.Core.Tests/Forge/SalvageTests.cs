using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using Xunit;

// NOTE: Tests.Forging, not Tests.Forge — a Tests.Forge namespace SHADOWS the Forge class itself, so
// every call in this file would resolve to the namespace and fail to compile. The same trap already
// forced Core.Forge -> Core.Forging and, in its day, Core.Weaving -> Core.Abilities (a namespace
// since deleted outright with the Form era, P3-final).
namespace IdleXIdle.Core.Tests.Forging;

/// <summary>
/// The three fates of unwanted loot: sell, dismantle, feed.
/// </summary>
/// <remarks>
/// The design asks for all three. SELL worked. DISMANTLE returned Gleam despite its own tuning doc
/// saying "returned as materials" — so it was a strictly worse sell, a trap for anyone who read the
/// button. FEED did not exist at all: the enum member had zero references and its conversion rate
/// carried a fifteen-line justification of a number nothing multiplied by.
/// </remarks>
public class SalvageTests
{
    private static ItemInstance Item(int sellValue = 100)
        => new() { InstanceId = "i1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = sellValue };

    private static readonly ForgeTuning T = ForgeTuning.Default;

    // ── The trade ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_dismantling_is_a_real_loss()
    {
        var item = Item(100);
        Assert.True(Forge.Dismantle(item, T) < item.SellValue);
    }

    // ── The materials stock ───────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_hunter_starts_with_no_materials()
    {
        Assert.Equal(0, new Hunter().Materials);
    }

    [Fact]
    public void test_materials_accrue_and_spend()
    {
        var h = new Hunter();
        h.AddMaterials(30);
        Assert.Equal(30, h.Materials);

        Assert.True(h.SpendMaterials(10));
        Assert.Equal(20, h.Materials);
    }

    [Fact]
    public void test_you_cannot_spend_materials_you_do_not_have()
    {
        // A partial spend would silently eat the stock and give nothing back — the worst possible
        // outcome for a player who mis-clicked.
        var h = new Hunter();
        h.AddMaterials(5);

        Assert.False(h.SpendMaterials(10));
        Assert.Equal(5, h.Materials);
    }

    [Fact]
    public void test_negative_amounts_cannot_mint_or_refund_materials()
    {
        var h = new Hunter();
        h.AddMaterials(-50);
        Assert.Equal(0, h.Materials);

        h.AddMaterials(10);
        Assert.False(h.SpendMaterials(-5));
        Assert.Equal(10, h.Materials);
    }

    [Fact]
    public void test_the_merge_economy_is_still_closed()
    {
        // Dismantle changed what it PAYS, not what it costs. If merge -> dismantle -> merge ever became
        // value-neutral, rarity could be laundered forever.
        foreach (var r in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic })
            Assert.True(Forge.MergeEconomyIsClosed(r, T, LootTuning.Default), $"{r} merges can be laundered");
    }
}
