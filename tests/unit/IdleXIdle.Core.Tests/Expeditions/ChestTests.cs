using System;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// Chests: the reward container that replaced the item firehose.
/// </summary>
/// <remarks>
/// The player's rule for chests: a higher GRADE means VALUABLE items, never MORE of them — "çok fazla
/// çıkarsa oyuncu eşyalarla ve materyallerle boğulur". So the QUANTITY of a chest's output stays tight and
/// roughly constant whatever the grade; only the item RARITY climbs. These tests pin that.
/// </remarks>
public class ChestTests
{
    private static Chest Chest(Rarity rarity, int tier = 5, Source? element = null)
        => new() { Rarity = rarity, Tier = tier, Element = element };

    // ── Materials: a tight band, NOT scaled by grade ────────────────────────────────────────────

    [Fact]
    public void test_opening_a_chest_always_pays_some_materials()
    {
        var reward = Chests.Open(Chest(Rarity.Common, tier: 1), new Random(1));
        Assert.True(reward.Materials > 0, "a chest paid no materials");
    }

    [Fact]
    public void test_grade_does_not_change_the_material_payout()
    {
        // The rule, at its sharpest: a Legendary chest and a Common chest — same seed, same tier — pay the
        // SAME materials. The grade's whole payoff is the item's rarity below, not a bigger pile.
        foreach (var grade in Enum.GetValues<Rarity>())
            Assert.Equal(
                Chests.Open(Chest(Rarity.Common, tier: 5), new Random(1)).Materials,
                Chests.Open(Chest(grade, tier: 5), new Random(1)).Materials);
    }

    [Fact]
    public void test_depth_nudges_materials_up_but_only_a_little()
    {
        var shallow = Chests.Open(Chest(Rarity.Rare, tier: 1), new Random(1)).Materials;
        var deep = Chests.Open(Chest(Rarity.Rare, tier: 40), new Random(1)).Materials;

        Assert.True(deep > shallow, "depth should nudge the material payout up");
        Assert.True(deep < shallow * 3, $"the depth nudge must stay a NUDGE, not a flood ({shallow} -> {deep})");
    }

    // ── Items: tight count, grade buys RARITY ───────────────────────────────────────────────────

    [Fact]
    public void test_the_item_count_stays_tight_for_every_grade()
    {
        // No grade may ever dump a pile. However rich the chest, it carries at most a couple of items — the
        // player is never buried, whatever they open.
        foreach (var grade in Enum.GetValues<Rarity>())
            for (var seed = 0; seed < 150; seed++)
                Assert.True(Chests.Open(Chest(grade), new Random(seed)).Items.Count <= 2,
                    $"a {grade} chest dumped a pile — grade must buy rarity, not quantity");
    }

    [Fact]
    public void test_a_richer_chest_yields_rarer_items_not_more_of_them()
    {
        // The heart of the rule. Across many opens a Legendary chest's items are RARER on average than a
        // Common chest's — but no more numerous. Grade climbs the quality axis, holds the quantity axis.
        (double Count, double Rarity) Profile(Rarity grade)
        {
            var bags = Enumerable.Range(0, 600)
                .Select(seed => Chests.Open(Chest(grade), new Random(seed)).Items)
                .ToList();
            var count = bags.Average(b => b.Count);
            var rarity = bags.SelectMany(b => b).Select(i => (double)(int)i.Rarity).DefaultIfEmpty(0).Average();
            return (count, rarity);
        }

        var common = Profile(Rarity.Common);
        var legendary = Profile(Rarity.Legendary);

        Assert.True(legendary.Rarity > common.Rarity,
            $"a richer chest must yield RARER items ({legendary.Rarity:0.000} vs {common.Rarity:0.000})");
        Assert.True(Math.Abs(legendary.Count - common.Count) < 0.4,
            $"a richer chest must NOT yield more items ({legendary.Count:0.00} vs {common.Count:0.00})");
    }

    [Fact]
    public void test_a_grade_guarantees_a_minimum_item_rarity()
    {
        // Bounded reward — a good chest CANNOT disappoint. Every item from a Legendary chest is at least
        // Epic; from an Epic, at least Rare. This is the "değerli eşyalar" promise made reliable, and the
        // loot-box rule that a good roll must never cough up a Common (a betrayal that breaks the loop).
        foreach (var (grade, floor) in new[]
                 {
                     (Rarity.Legendary, Rarity.Epic),
                     (Rarity.Epic, Rarity.Rare),
                     (Rarity.Rare, Rarity.Uncommon),
                 })
            for (var seed = 0; seed < 200; seed++)
                foreach (var item in Chests.Open(Chest(grade, tier: 3), new Random(seed)).Items)
                {
                    Assert.True(item.Rarity >= floor, $"a {grade} chest dropped a {item.Rarity}, below its {floor} floor");
                    // Sell value must track the elevated rarity — rarity and value move together.
                    Assert.Equal(LootTuning.Default.RaritySellValue[(int)item.Rarity], item.SellValue);
                }
    }

    [Fact]
    public void test_a_chests_items_are_attuned_to_its_element()
    {
        // A regional chest's wearable loot must carry that region's element, like any other regional drop.
        var reward = Chests.Open(Chest(Rarity.Legendary, tier: 8, element: Source.Shadow), new Random(4));

        foreach (var item in reward.Items.Where(Gear.IsWearable))
            Assert.Equal(Source.Shadow, item.Element);
    }

    [Fact]
    public void test_chest_items_are_always_gear_never_a_raw_material()
    {
        // The chest pays material CURRENCY separately, so its ITEMS are the valuable gear — a Material-type
        // item would be redundant clutter and a flat reveal. Holds for every grade, even when the roll was
        // all materials (a gear piece is minted at the floor).
        foreach (var grade in Enum.GetValues<Rarity>())
            for (var seed = 0; seed < 200; seed++)
                foreach (var item in Chests.Open(Chest(grade, tier: 6), new Random(seed)).Items)
                    Assert.True(Gear.IsWearable(item), $"a chest dropped a {item.BaseType} — chest items must be gear");
    }

    [Fact]
    public void test_a_chest_never_contains_a_creature_core()
    {
        // RollBoss strips cores (the haul pays those per wave); a chest must not re-mint them.
        for (var seed = 0; seed < 50; seed++)
            Assert.DoesNotContain(Chests.Open(Chest(Rarity.Epic), new Random(seed)).Items,
                i => i.BaseType == ItemBaseType.CreatureCore);
    }

    // ── Drop grade: deeper drops a RICHER chest (the grade climbs with depth, not the count) ─────

    [Fact]
    public void test_deeper_tiers_drop_richer_chests_on_average()
    {
        double AverageGrade(int tier)
            => Enumerable.Range(0, 400)
                .Select(seed => (int)Chests.RollRarity(tier, new Random(seed)))
                .Average();

        Assert.True(AverageGrade(30) > AverageGrade(1),
            "a deep boss drops chests no better than a shallow one — depth buys nothing");
    }

    [Fact]
    public void test_a_drop_records_its_tier_and_element()
    {
        var chest = Chests.RollDrop(12, Source.Machine, new Random(7));

        Assert.Equal(12, chest.Tier);
        Assert.Equal(Source.Machine, chest.Element);
    }

    [Fact]
    public void test_the_same_seed_opens_the_same_chest()
    {
        // Determinism: a build must be reasoned about, not tested by feel (the whole sim's contract).
        var a = Chests.Open(Chest(Rarity.Epic, tier: 6, element: Source.Nature), new Random(99));
        var b = Chests.Open(Chest(Rarity.Epic, tier: 6, element: Source.Nature), new Random(99));

        Assert.Equal(a.Materials, b.Materials);
        Assert.Equal(a.Items.Count, b.Items.Count);
    }

    [Fact]
    public void test_a_boss_chest_is_one_flat_percentage_at_every_depth()
    {
        // THIS TEST USED TO ASSERT THE OPPOSITE — that the chance rose with depth and was capped. It was
        // replaced on player direction: from inside the game the ramp was invisible (nobody feels 12%
        // creeping toward 35%), so it bought no felt progression and quietly made deep farming the only
        // sensible place to be. A chest is now "one boss in five", the same sentence everywhere.
        //
        // Depth still buys the chest's GRADE and the tier of what is inside. Only the frequency is flat.
        var shallow = Chests.DropChance(0);
        var deep = Chests.DropChance(1000);

        Assert.Equal(shallow, deep, 4);
        Assert.InRange(shallow, 0.01f, 0.5f);        // an event, not a paycheck
        Assert.True(shallow < 1f, "a chest must never be guaranteed.");
    }
}
