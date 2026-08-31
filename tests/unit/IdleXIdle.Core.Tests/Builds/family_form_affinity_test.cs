using System;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The weapon family favours two Forms (2026-08-23): a bow makes Projectiles hit harder, a blade makes
/// Strikes hit harder, and it happens INSIDE the sim — so the bench, the fight and any ranking agree.
/// </summary>
public class FamilyFormAffinityTests
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

    private static Build BuildWith(Form form)
    {
        var b = new Build();
        b.Weave(new EquippedSkill(new WovenAbility { Name = "S", Source = Source.Body, Form = form },
                                  FormBehaviour.BaseCooldownMs(form)));
        return b;
    }

    [Fact]
    public void test_every_form_is_favoured_by_some_family_and_each_family_favours_two()
    {
        Assert.Equal(ItemNaming.WeaponFamilies.Length, ItemFamilies.FavouredForms.Length);
        foreach (var pair in ItemFamilies.FavouredForms) Assert.Equal(2, pair.Distinct().Count());
        foreach (Form f in Enum.GetValues<Form>())
            Assert.Contains(ItemFamilies.FavouredForms, pair => pair.Contains(f));
    }

    [Fact]
    public void test_gear_shape_is_the_weapons_favoured_forms_and_nothing_bare()
    {
        Assert.Equal(SkillShape.None.FormPower.Count, GearShape.Of(null).FormPower.Count);
        var h = new Hunter();
        Assert.Empty(GearShape.Of(h).FormPower);
        h.Equip(WeaponOfFamily("bow"));
        var shape = GearShape.Of(h);
        Assert.Equal(ItemFamilies.FormAffinity, shape.FormPowerFor(Form.Projectile), 3);
        Assert.Equal(ItemFamilies.FormAffinity, shape.FormPowerFor(Form.Mark), 3);
        Assert.Equal(1f, shape.FormPowerFor(Form.Strike), 3);
    }

    [Fact]
    public void test_a_bow_benches_higher_for_projectiles_and_a_blade_for_strikes()
    {
        // Two weapons of equal rarity and level, different families, the SAME build — only the family
        // changes between the two measurements. The favoured pairing must win in the real sim.
        var bow = WeaponOfFamily("bow");
        var blade = WeaponOfFamily("blade");

        float Dps(ItemInstance weapon, Form form)
        {
            var h = new Hunter();
            h.Equip(weapon);
            return DamageBench.Measure(BuildWith(form), h).Dps;
        }

        Assert.True(Dps(bow, Form.Projectile) > Dps(blade, Form.Projectile) * 1.05f,
            "the bow must out-shoot the blade on a Projectile build");
        Assert.True(Dps(blade, Form.Strike) > Dps(bow, Form.Strike) * 1.05f,
            "the blade must out-hit the bow on a Strike build");
    }
}
