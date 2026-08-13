using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// Regression for the gear-compare bug: the character screen compared items by <see cref="Gear.ItemScore"/>
/// (a rarity + affix heuristic) while the HUD's power was <see cref="Hunter.PowerRating"/> (a weapon /
/// charm / defense formula). The two are different metrics, so an item could show LOWER power in the
/// compare strip yet RAISE PowerRating when equipped — "the info shows less power but my power went up".
/// The fix compares by <see cref="Hunter.PowerContribution"/>, which is defined FROM PowerRating so the
/// shown delta is exactly the real change; these tests pin that guarantee.
/// </summary>
public class PowerContributionTests
{
    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity, int ilvl = 1) => new()
    {
        InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 10, ItemLevel = ilvl,
    };

    [Fact]
    public void test_power_contribution_equals_the_real_powerrating_change_on_a_swap()
    {
        // The load-bearing invariant: the delta the gear screen shows (contribution(new) − contribution(old))
        // must equal the actual change to PowerRating on equip, in every slot — so the two can never disagree.
        var pairs = new[]
        {
            (Item("a", ItemBaseType.Weapon, Rarity.Common), Item("b", ItemBaseType.Weapon, Rarity.Legendary, 40)),
            (Item("c", ItemBaseType.Charm, Rarity.Rare), Item("d", ItemBaseType.Charm, Rarity.Epic, 20)),
            (Item("e", ItemBaseType.Ring, Rarity.Uncommon), Item("f", ItemBaseType.Ring, Rarity.Legendary, 30)),
            (Item("g", ItemBaseType.Helm, Rarity.Epic, 12), Item("h", ItemBaseType.Helm, Rarity.Rare, 55)),
        };

        foreach (var (a, b) in pairs)
        {
            var hunter = new Hunter();
            hunter.Equip(a);
            var before = hunter.PowerRating;
            var shownDelta = hunter.PowerContribution(b) - hunter.PowerContribution(a);

            hunter.Equip(b);
            var realDelta = hunter.PowerRating - before;

            Assert.Equal(realDelta, shownDelta);
        }
    }

    [Fact]
    public void test_a_stronger_weapon_has_a_higher_contribution_and_raises_powerrating()
    {
        var hunter = new Hunter();
        var weak = Item("weak", ItemBaseType.Weapon, Rarity.Common, 1);
        var strong = Item("strong", ItemBaseType.Weapon, Rarity.Legendary, 50);

        hunter.Equip(weak);
        var before = hunter.PowerRating;
        hunter.Equip(strong);

        Assert.True(hunter.PowerRating > before, "a Legendary weapon must raise PowerRating over a Common one");
        Assert.True(hunter.PowerContribution(strong) > hunter.PowerContribution(weak),
            "and the screen's power number must rank it above the weaker one");
    }

    [Fact]
    public void test_contribution_is_pure_it_does_not_change_worn_gear()
    {
        var hunter = new Hunter();
        var worn = Item("worn", ItemBaseType.Weapon, Rarity.Rare);
        hunter.Equip(worn);

        var _ = hunter.PowerContribution(Item("probe", ItemBaseType.Weapon, Rarity.Legendary, 60));

        Assert.Same(worn, hunter.Worn(GearSlot.Weapon));   // probing must not disturb what is equipped
    }

    [Fact]
    public void test_a_non_wearable_or_null_item_contributes_no_power()
    {
        var hunter = new Hunter();
        Assert.Equal(0, hunter.PowerContribution(Item("mat", ItemBaseType.Material, Rarity.Rare)));
        Assert.Equal(0, hunter.PowerContribution(null));
    }

    /// <summary>
    /// The power number must move when the things the SIM multiplies by move.
    /// </summary>
    /// <remarks>
    /// REGRESSION. PowerRating read the raw weapon multiplier and the raw attack-power stat, so gear
    /// TRAITS and DAMAGE/HEALTH affixes — which multiply every hit and every health pool in the sim —
    /// moved it by nothing. A player equipping a piece that genuinely made them stronger watched their
    /// "power" sit still, and EQUIP BEST, which ranks by this number, would hand back the weaker item.
    ///
    /// Nothing caught it: the whole suite passed with the bug in place, because every existing test
    /// compared PowerRating against ITSELF (contribution deltas) rather than against what the fight
    /// actually reads.
    /// </remarks>
    [Fact]
    public void test_power_rating_moves_with_the_multipliers_the_sim_reads()
    {
        var checkedAny = false;

        foreach (var type in new[]
                 {
                     ItemBaseType.Ring, ItemBaseType.Helm, ItemBaseType.Boots,
                     ItemBaseType.Gloves, ItemBaseType.Chest, ItemBaseType.AbilityFocus,
                 })
            foreach (var rarity in new[] { Rarity.Rare, Rarity.Epic, Rarity.Legendary })
            {
                var bare = new Hunter();
                var worn = new Hunter();
                worn.Equip(Item($"{type}-{rarity}", type, rarity, ilvl: 40));

                // STRICT improvements only. Gear traits are trades — a Rare Ring raises health 12% and
                // costs 10% damage — so "either multiplier went up" is not a claim about power at all,
                // and the first version of this test failed on exactly that piece while PowerRating was
                // behaving correctly. Only a piece that is better on one axis and no worse on the other
                // says anything about whether the number follows the sim.
                var damageUp = worn.SquadDamageMultiplier > bare.SquadDamageMultiplier * 1.001f;
                var healthUp = worn.SquadHealthMultiplier > bare.SquadHealthMultiplier * 1.001f;
                var damageDown = worn.SquadDamageMultiplier < bare.SquadDamageMultiplier * 0.999f;
                var healthDown = worn.SquadHealthMultiplier < bare.SquadHealthMultiplier * 0.999f;
                if (!((damageUp && !healthDown) || (healthUp && !damageDown))) continue;

                checkedAny = true;
                Assert.True(worn.PowerRating > bare.PowerRating,
                    $"{rarity} {type} raised the multipliers the sim reads " +
                    $"(damage {bare.SquadDamageMultiplier:F3} -> {worn.SquadDamageMultiplier:F3}, " +
                    $"health {bare.SquadHealthMultiplier:F3} -> {worn.SquadHealthMultiplier:F3}) " +
                    $"and PowerRating did not move ({bare.PowerRating} -> {worn.PowerRating}).");
            }

        Assert.True(checkedAny,
            "No generated item moved the sim's multipliers, so this test proved nothing. Either the " +
            "affix roller stopped producing DAMAGE/HEALTH affixes or the fixture is wrong.");
    }
}
