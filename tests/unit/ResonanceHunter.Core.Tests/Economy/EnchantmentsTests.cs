using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// Enchantments: effects that change what HAPPENS, which the data model could not previously express.
/// </summary>
/// <remarks>
/// This file owns the DATA MODEL — which item carries which enchant, its magnitude curve, its blurb. The
/// triggers firing INSIDE a fight are proven against the live single-champion engine in
/// <c>Builds/TriggerLivenessTests</c>, <c>Builds/SoloBattleTests</c> and <c>Builds/SoloExpeditionTests</c>
/// (UNDYING, HARVEST, SPLINTER, DESPERATION and every Form-combo), so they are not re-proven here through
/// the retired squad engine.
/// </remarks>
public class EnchantmentsTests
{
    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity = Rarity.Epic)
        => new() { InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 82 };

    private static ItemInstance WeaponWith(EnchantKind kind, Rarity rarity = Rarity.Epic)
        => Enumerable.Range(0, 800)
            .Select(i => Item($"w_{i}", ItemBaseType.Weapon, rarity))
            .First(it => Enchantments.Of(it)?.Kind == kind);

    // ── The data model ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_an_item_can_finally_carry_an_element()
    {
        // ItemInstance had five fields and no room for an element — a grep for "Element" returned zero
        // hits in the whole repo. It reuses Source, so an item's fire is a creature's fire.
        var item = Item("w1", ItemBaseType.Weapon) with { Element = Source.Shadow };
        Assert.Equal(Source.Shadow, item.Element);
    }

    [Fact]
    public void test_an_item_without_an_element_has_no_element()
    {
        // Nullable, not defaulted: `?? 0` would make every pre-element save's loot Body-attuned.
        Assert.Null(Item("w1", ItemBaseType.Weapon).Element);
    }

    [Fact]
    public void test_an_enchantment_is_stable_for_a_given_item()
    {
        var first = Enchantments.Of(Item("weapon_xyz", ItemBaseType.Weapon));
        for (var i = 0; i < 30; i++)
            Assert.Equal(first, Enchantments.Of(Item("weapon_xyz", ItemBaseType.Weapon)));
    }

    [Fact]
    public void test_the_enchantment_does_not_correlate_with_the_trait()
    {
        // Sharing one hash would lock the two axes together forever: every HEAVY weapon in the game
        // would carry the same enchantment, and two axes would collapse back into one.
        var pairs = new HashSet<(GearTrait, EnchantKind)>();
        for (var i = 0; i < 400; i++)
        {
            var it = Item($"w_{i}", ItemBaseType.Weapon);
            if (GearTraits.TraitOf(it) is { } t && Enchantments.Of(it) is { } e)
                pairs.Add((t, e.Kind));
        }

        // 4 traits x 3 weapon enchants = 12 combinations. Correlated hashes would yield at most 4.
        Assert.True(pairs.Count > 8, $"only {pairs.Count} trait/enchant pairings exist — the axes are correlated");
    }

    [Fact]
    public void test_commons_and_uncommons_are_never_enchanted()
    {
        for (var i = 0; i < 50; i++)
        {
            Assert.Null(Enchantments.Of(Item($"w_{i}", ItemBaseType.Weapon, Rarity.Common)));
            Assert.Null(Enchantments.Of(Item($"w_{i}", ItemBaseType.Weapon, Rarity.Uncommon)));
        }
        Assert.NotNull(Enchantments.Of(Item("w_1", ItemBaseType.Weapon, Rarity.Rare)));
    }

    [Fact]
    public void test_materials_and_cores_are_never_enchanted()
    {
        Assert.Null(Enchantments.Of(Item("m1", ItemBaseType.Material, Rarity.Legendary)));
        Assert.Null(Enchantments.Of(Item("c1", ItemBaseType.CreatureCore, Rarity.Legendary)));
        Assert.Null(Enchantments.Of(null));
    }

    [Fact]
    public void test_no_chance_based_enchantment_ever_reaches_certainty()
    {
        // A trigger that always fires is not a trigger, it is a stat — which is the thing this layer
        // exists to not be. The rarity curve runs to 7.00, so an undamped magnitude would be a 700%
        // chance.
        foreach (var kind in new[] { EnchantKind.Splinter, EnchantKind.Harvest, EnchantKind.Venom })
        {
            var m = Enchantments.MagnitudeFor(kind, Rarity.Legendary);
            Assert.InRange(m, 0.01f, 0.99f);
        }
    }

    [Fact]
    public void test_rarity_makes_an_enchantment_stronger()
    {
        Assert.True(Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Legendary)
                    > Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Rare));
    }

    [Fact]
    public void test_every_form_has_a_combo_enchantment()
    {
        // The combo axis is complete only if committing to ANY Form has an item that rewards it. A Form
        // with no combo enchant is a build path with nothing to chase — the gap this feature closed for
        // Strike, Trap and Transformation.
        foreach (var form in System.Enum.GetValues<ResonanceHunter.Core.Abilities.Form>())
            Assert.Contains(System.Enum.GetValues<EnchantKind>(),
                k => new Enchantment(k, 1f).NeedsForm == form);
    }

    [Fact]
    public void test_an_enchantment_pool_matches_its_slot()
    {
        foreach (var (type, slot) in new[]
                 {
                     (ItemBaseType.Weapon, GearSlot.Weapon),
                     (ItemBaseType.Charm, GearSlot.Charm),
                     (ItemBaseType.AbilityFocus, GearSlot.Focus),
                 })
        {
            for (var i = 0; i < 100; i++)
                if (Enchantments.Of(Item($"i_{i}", type)) is { } e)
                    Assert.Contains(e.Kind, Enchantments.PoolFor(slot));
        }
    }

    [Fact]
    public void test_magnitude_of_an_absent_enchantment_is_zero()
    {
        var worn = Enchantments.Worn(WeaponWith(EnchantKind.Splinter));
        Assert.Equal(0f, Enchantments.MagnitudeOf(worn, EnchantKind.Undying));
    }

    [Fact]
    public void test_every_enchantment_blurb_fits_the_forge_row()
    {
        // The Forge gives the blurb a fixed column, x=344 to the panel edge: 21 characters. Right-
        // aligning it instead let a long name meet the blurb in the middle and render as one word
        // ("SPLINTERON A KILL"), which is why the column is fixed and this cap exists.
        foreach (var kind in System.Enum.GetValues<EnchantKind>())
        {
            var blurb = new Enchantment(kind, Enchantments.MagnitudeFor(kind, Rarity.Legendary)).Blurb;
            Assert.False(string.IsNullOrWhiteSpace(blurb));
            Assert.True(blurb.Length <= 21, $"{kind}'s blurb is {blurb.Length} chars — the Forge column fits 21: \"{blurb}\"");
        }
    }
}
