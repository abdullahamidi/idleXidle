using System;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// The GEZGİN TÜCCAR — same stall for everyone, a real material sink, one of each per week.
/// </summary>
public class WanderingTraderTest
{
    private static readonly LootTuning L = new();
    private static readonly TraderTuning T = TraderTuning.Default;

    [Fact]
    public void test_trader_stock_is_identical_for_the_same_week_and_differs_across_weeks()
    {
        // Arrange + Act: the same week twice, and fifty-two other weeks.
        var a = WanderingTrader.Stock(202634, itemLevel: 12, L);
        var b = WanderingTrader.Stock(202634, itemLevel: 12, L);

        // Assert: identity fields match exactly — the shared-world illusion IS this equality.
        Assert.Equal(a.Select(i => (i.InstanceId, i.BaseType, i.Rarity, i.TraitOverride, i.Element)),
                     b.Select(i => (i.InstanceId, i.BaseType, i.Rarity, i.TraitOverride, i.Element)));

        // And the year's stalls are not one stall: some other week differs in type or element.
        var year = Enumerable.Range(202601, 52).Select(w => WanderingTrader.Stock(w, 12, L)).ToList();
        var differing = year.Count(s => s[0].BaseType != a[0].BaseType || s[0].Element != a[0].Element);
        Assert.True(differing > 10, $"only {differing}/52 weeks differ from week 34 — the seed is stuck");

        // WITHIN a week the three wearables must not march in uniform. The first cut rolled
        // through seeded System.Random, and certain seeds opened degenerately (2,2,3,3,2,2,2,2) —
        // every armour piece wore the same prefix, week after week. The hash derivation is pinned
        // here: a year where most stalls are prefix-triples means the mixing broke again.
        var uniform = year.Count(s => s[0].TraitOverride == s[1].TraitOverride
                                      && s[1].TraitOverride == s[2].TraitOverride);
        Assert.True(uniform < 8, $"{uniform}/52 stalls sell one prefix three times — degenerate mixing");
    }

    [Fact]
    public void test_trader_stall_shape_three_prefixed_wearables_and_a_gem()
    {
        var stock = WanderingTrader.Stock(202634, itemLevel: 8, L);

        Assert.Equal(WanderingTrader.StallSize, stock.Count);
        Assert.Equal(new[] { Rarity.Rare, Rarity.Epic, Rarity.Legendary },
                     stock.Take(3).Select(i => i.Rarity));
        Assert.All(stock.Take(3), i =>
        {
            Assert.True(Gear.IsWearable(i), $"{i.BaseType} is not wearable");
            Assert.NotNull(i.TraitOverride);   // the stall never sells plain
            Assert.NotNull(i.Element);
        });
        Assert.Equal(ItemBaseType.Gem, stock[3].BaseType);
        Assert.All(stock, i => Assert.Equal(8, i.ItemLevel));   // the buyer's level, every slot
    }

    [Fact]
    public void test_trader_buying_is_all_or_nothing_and_actually_drains_materials()
    {
        // Arrange: enough for the Rare offer, one Essence short for a second check.
        var stock = WanderingTrader.Stock(202634, 10, L);
        var offer = stock[0];   // Rare: Scrap + Essence
        var price = WanderingTrader.PriceOf(offer, T);

        var rich = new Hunter();
        foreach (var (m, amount) in price) rich.AddMaterial(m, amount);
        var poor = new Hunter();
        foreach (var (m, amount) in price) poor.AddMaterial(m, amount - 1);

        // Act + Assert: the rich hunter pays every line down to zero...
        Assert.True(WanderingTrader.TryBuy(rich, offer, T));
        foreach (var (m, _) in price) Assert.Equal(0, rich.MaterialOf(m));

        // ...and the poor one pays NOTHING — no partial spends.
        Assert.False(WanderingTrader.TryBuy(poor, offer, T));
        foreach (var (m, amount) in price) Assert.Equal(amount - 1, poor.MaterialOf(m));
    }

    [Fact]
    public void test_trader_week_stamp_is_iso_and_never_walks_backwards_over_new_year()
    {
        // Dec 29 2025 is ISO week 1 of 2026 — a refactor to DateTime.Year would stamp it 202501,
        // 51 weeks BACKWARDS, colliding with the real week 1 and re-arming the stall. Pinned.
        Assert.Equal(202601, WanderingTrader.WeekStamp(new DateTime(2025, 12, 29, 12, 0, 0, DateTimeKind.Utc)));
        Assert.True(WanderingTrader.WeekStamp(new DateTime(2026, 1, 4, 12, 0, 0, DateTimeKind.Utc))
                    >= WanderingTrader.WeekStamp(new DateTime(2025, 12, 28, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void test_trader_goods_are_never_a_material_pump_even_with_a_salvage_charter()
    {
        // The review found the original stall was a CRYSTAL MINT: the Legendary cost 60 Core +
        // 18 Crystal and salvaged for 80 Crystal (160 with a charter). Now every offer's DOUBLED
        // salvage return is below the its-own-tier line of its price — buying to shred always loses.
        var forge = IdleXIdle.Core.Forging.ForgeTuning.Default;
        foreach (var offer in WanderingTrader.Stock(202634, 30, L).Where(o => o.BaseType != ItemBaseType.Gem))
        {
            var salvageTier = MaterialTiers.ForRarity(offer.Rarity);
            var salvageDoubled = IdleXIdle.Core.Forging.Forge.Dismantle(offer, forge) * 2;
            var sameTierCost = WanderingTrader.PriceOf(offer, T)
                .Where(p => p.Material == salvageTier).Sum(p => p.Amount);
            Assert.True(salvageDoubled < Math.Max(1, sameTierCost),
                $"{offer.Rarity}: charter salvage {salvageDoubled} {salvageTier} vs price {sameTierCost} — the pump is back");
        }
    }

    [Fact]
    public void test_trader_every_offer_has_a_positive_price()
    {
        var stock = WanderingTrader.Stock(202702, 25, L);
        foreach (var offer in stock)
        {
            var price = WanderingTrader.PriceOf(offer, T);
            Assert.NotEmpty(price);
            Assert.All(price, p => Assert.True(p.Amount > 0));
        }
    }
}
