using System;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The weapon family favours two Styles (2026-08-23, ported to the Style ring in P3c): a bow makes
/// Volley skills hit harder, a blade makes Hammer skills hit harder, and it happens INSIDE the sim —
/// so the bench, the fight and any ranking agree.
/// </summary>
public class FamilyStyleAffinityTests
{
    private static ItemInstance WeaponOfFamily(string family)
    {
        // The family is id-derived (ItemNaming.WeaponFamilyIndex); walk ids until the wanted one appears.
        var want = Array.IndexOf(ItemNaming.WeaponFamilies, family);
        for (var i = 0; i < 10_000; i++)
        {
            var item = new ItemInstance { InstanceId = $"w{i}", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Rare, SellValue = 1, ItemLevel = 10 };
            if (ItemNaming.WeaponFamilyIndex(item) == want) return item;
        }
        throw new InvalidOperationException("no id landed on " + family);
    }

    private static Build BuildWith(Style style)
    {
        // The style's own Active from the catalogue, at its base line — the id is the identity.
        var b = new Build();
        b.Weave(TestBuilds.Skill(SkillCatalogue.ActiveOf(style).Id));
        return b;
    }

    [Fact]
    public void test_every_style_is_favoured_by_some_family_and_each_family_favours_two()
    {
        Assert.Equal(ItemNaming.WeaponFamilies.Length, ItemFamilies.FavouredStyles.Length);
        foreach (var pair in ItemFamilies.FavouredStyles) Assert.Equal(2, pair.Distinct().Count());
        foreach (Style s in Enum.GetValues<Style>())
            Assert.Contains(ItemFamilies.FavouredStyles, pair => pair.Contains(s));
    }

    [Fact]
    public void test_gear_shape_is_the_weapons_favoured_styles_and_nothing_bare()
    {
        Assert.Equal(SkillShape.None.StylePower.Count, GearShape.Of(null).StylePower.Count);
        var h = new Hunter();
        Assert.Empty(GearShape.Of(h).StylePower);
        h.Equip(WeaponOfFamily("bow"));
        var shape = GearShape.Of(h);
        Assert.Equal(ItemFamilies.StyleAffinityBonus, shape.StylePowerFor(Style.Volley), 3);
        Assert.Equal(ItemFamilies.StyleAffinityBonus, shape.StylePowerFor(Style.Sign), 3);
        Assert.Equal(1f, shape.StylePowerFor(Style.Hammer), 3);
    }

    [Fact]
    public void test_a_bow_benches_higher_for_volleys_and_a_blade_for_hammers()
    {
        // Two weapons of equal rarity and level, different families, the SAME build — only the family
        // changes between the two measurements. The favoured pairing must win in the real sim.
        var bow = WeaponOfFamily("bow");
        var blade = WeaponOfFamily("blade");

        float Dps(ItemInstance weapon, Style style)
        {
            var h = new Hunter();
            h.Equip(weapon);
            return DamageBench.Measure(BuildWith(style), h).Dps;
        }

        Assert.True(Dps(bow, Style.Volley) > Dps(blade, Style.Volley) * 1.05f,
            "the bow must out-shoot the blade on a Volley build");
        Assert.True(Dps(blade, Style.Hammer) > Dps(bow, Style.Hammer) * 1.05f,
            "the blade must out-hit the bow on a Hammer build");
    }
}
