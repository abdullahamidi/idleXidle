using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using Xunit;

// Tests.Forging, not Tests.Forge — the latter shadows the Forge class. See SalvageTests.
namespace IdleXIdle.Core.Tests.Forging;

/// <summary>
/// Cross-family merging: predictable hybrids, and elements that carry.
/// </summary>
/// <remarks>
/// The merge output was <c>inputs[rng.Next(inputs.Count)].BaseType</c> under a comment claiming the
/// player's inputs steered the result. With three same-type inputs that is a no-op; with mixed inputs
/// it is a coin flip. No hybrid, no family, no combination rule existed anywhere.
/// </remarks>
public class MergeRecipeTests
{
    private static ItemInstance It(string id, ItemBaseType type, Source? element = null, Rarity rarity = Rarity.Common)
        => new() { InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 10, Element = element };

    private static MergeResult Merge(params ItemInstance[] items)
        => Forge.Merge(items, new Random(1), ForgeTuning.Default, LootTuning.Default);

    // ── Predictability. The entire feature. ──────────────────────────────────────────────────

    [Fact]
    public void test_the_same_trio_always_makes_the_same_thing()
    {
        // A random output makes the Forge a slot machine, where the only correct move is to shovel in
        // whatever you have. A decided one makes it a recipe book, where the inputs are a plan.
        ItemInstance[] Trio() => new[]
        {
            It("a", ItemBaseType.Weapon), It("b", ItemBaseType.Charm), It("c", ItemBaseType.Material),
        };

        var first = Merge(Trio()).Product!.BaseType;
        for (var i = 0; i < 40; i++)
        {
            // A different rng seed each time: the TYPE must not depend on it at all.
            var result = Forge.Merge(Trio(), new Random(i), ForgeTuning.Default, LootTuning.Default);
            Assert.Equal(first, result.Product!.BaseType);
        }
    }

    [Fact]
    public void test_a_majority_of_a_type_wins()
    {
        // Two weapons and a charm: you get a weapon, and the charm was the fuel.
        var r = Merge(It("a", ItemBaseType.Weapon), It("b", ItemBaseType.Weapon), It("c", ItemBaseType.Charm));
        Assert.Equal(ItemBaseType.Weapon, r.Product!.BaseType);
    }

    [Fact]
    public void test_three_of_a_kind_makes_that_kind()
    {
        var r = Merge(It("a", ItemBaseType.Charm), It("b", ItemBaseType.Charm), It("c", ItemBaseType.Charm));
        Assert.Equal(ItemBaseType.Charm, r.Product!.BaseType);
    }

    [Fact]
    public void test_a_majority_beats_the_rng_every_time()
    {
        // The old code could return the ODD ONE OUT a third of the time — feed it two weapons and a
        // charm and get a charm, for no reason a player could ever discover.
        for (var seed = 0; seed < 40; seed++)
        {
            var r = Forge.Merge(
                new[] { It("a", ItemBaseType.Weapon), It("b", ItemBaseType.Weapon), It("c", ItemBaseType.Charm) },
                new Random(seed), ForgeTuning.Default, LootTuning.Default);
            Assert.Equal(ItemBaseType.Weapon, r.Product!.BaseType);
        }
    }

    // ── Hybrids ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_three_different_types_are_a_hybrid()
    {
        var trio = new[] { It("a", ItemBaseType.Weapon), It("b", ItemBaseType.Charm), It("c", ItemBaseType.Material) };
        Assert.True(MergeRecipe.IsHybrid(trio));

        var majority = new[] { It("a", ItemBaseType.Weapon), It("b", ItemBaseType.Weapon), It("c", ItemBaseType.Charm) };
        Assert.False(MergeRecipe.IsHybrid(majority));
    }

    [Fact]
    public void test_every_three_type_mixture_resolves_deterministically_to_an_input()
    {
        // The guard against a coin flip. Once, an unlisted mixture fell back to the lowest enum member —
        // arbitrary, so "is one of the inputs" could not tell an authored answer from junk. There are
        // eight wearable slots now (C(10,3)=120 mixtures), too many to hand-author, so the fallback is a
        // REAL rule: the most equippable input wins (see MergeRecipe.HybridPriority). The property that
        // matters is therefore simply — every mixture has ONE deterministic answer, it is always one of
        // the inputs, and the load order never changes it. That is what lets a player aim.
        var types = Enum.GetValues<ItemBaseType>();
        var combos = from a in types
                     from b in types
                     from c in types
                     where (int)a < (int)b && (int)b < (int)c
                     select new[] { a, b, c };

        foreach (var combo in combos)
        {
            var result = MergeRecipe.HybridOf(combo);
            Assert.Contains(result, combo);                                  // invented nothing
            Assert.Equal(result, MergeRecipe.HybridOf(combo.Reverse()));     // order-independent — no coin flip
        }
    }

    [Fact]
    public void test_a_recipe_does_not_care_what_order_you_load_it_in()
    {
        // {weapon, charm, material} and {charm, material, weapon} are ONE recipe. A player who has to
        // discover that click order matters has discovered a bug, not a mechanic.
        var a = MergeRecipe.HybridOf(new[] { ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.Material });
        var b = MergeRecipe.HybridOf(new[] { ItemBaseType.Material, ItemBaseType.Weapon, ItemBaseType.Charm });
        var c = MergeRecipe.HybridOf(new[] { ItemBaseType.Charm, ItemBaseType.Material, ItemBaseType.Weapon });

        Assert.Equal(a, b);
        Assert.Equal(b, c);
    }

    [Fact]
    public void test_a_focus_can_be_built_rather_than_only_found()
    {
        // The point of a hybrid table: a route to a slot you want that does not depend on the drop.
        var r = Merge(
            It("a", ItemBaseType.Weapon), It("b", ItemBaseType.Charm), It("c", ItemBaseType.AbilityFocus));
        Assert.Equal(ItemBaseType.AbilityFocus, r.Product!.BaseType);
    }

    // ── Elements ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_shared_element_carries_through_the_merge()
    {
        // The reason to hoard by element rather than by rarity — and the Forge's first input that the
        // world map actually asks for, via the Source matchup.
        var r = Merge(
            It("a", ItemBaseType.Charm, Source.Shadow),
            It("b", ItemBaseType.Charm, Source.Shadow),
            It("c", ItemBaseType.Charm, Source.Shadow));

        Assert.Equal(Source.Shadow, r.Product!.Element);
    }

    [Fact]
    public void test_mixing_elements_costs_you_the_element()
    {
        // A real cost, not a randomisation: mixing is how you get a hybrid TYPE, and it is paid for in
        // attunement. "Which do I want more" is the decision.
        var r = Merge(
            It("a", ItemBaseType.Charm, Source.Shadow),
            It("b", ItemBaseType.Charm, Source.Nature),
            It("c", ItemBaseType.Charm, Source.Shadow));

        Assert.Null(r.Product!.Element);
    }

    [Fact]
    public void test_elementless_inputs_make_an_elementless_product()
    {
        var r = Merge(It("a", ItemBaseType.Charm), It("b", ItemBaseType.Charm), It("c", ItemBaseType.Charm));
        Assert.Null(r.Product!.Element);
    }

    [Fact]
    public void test_one_elementless_input_spoils_the_batch()
    {
        // Absent is not a wildcard. If it were, a player could not tell by looking whether a trio would
        // carry — which is precisely the unpredictability this whole class exists to remove.
        var r = Merge(
            It("a", ItemBaseType.Charm, Source.Shadow),
            It("b", ItemBaseType.Charm, Source.Shadow),
            It("c", ItemBaseType.Charm));

        Assert.Null(r.Product!.Element);
    }

    // ── Preview ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_preview_tells_the_truth()
    {
        // FORGE INSIGHT ("preview a merge's result before committing") is only honest if the preview
        // and the merge cannot disagree — which is only possible because the result is decided.
        var trio = new[]
        {
            It("a", ItemBaseType.Weapon, Source.Mind),
            It("b", ItemBaseType.Charm, Source.Mind),
            It("c", ItemBaseType.Material, Source.Mind),
        };

        var (type, element, isHybrid) = Forge.Preview(trio);
        var product = Merge(trio).Product!;

        Assert.Equal(product.BaseType, type);
        Assert.Equal(product.Element, element);
        Assert.True(isHybrid);
    }

    [Fact]
    public void test_the_merge_economy_is_still_closed_with_recipes()
    {
        // Deciding the OUTPUT TYPE must not change what a merge costs. If it ever did, a player could
        // launder rarity through whichever recipe paid best.
        foreach (var r in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic })
            Assert.True(Forge.MergeEconomyIsClosed(r, ForgeTuning.Default, LootTuning.Default));
    }
}
