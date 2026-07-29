using ResonanceHunter.Core.Encounters;
using Xunit;

namespace ResonanceHunter.Core.Tests.Encounters;

/// <summary>
/// Region modifiers give each region a themed combat/loot identity on top of the corruption ladder.
/// </summary>
public class RegionModifiersTests
{
    [Fact]
    public void test_every_region_has_a_modifier_that_actually_does_something()
    {
        foreach (var def in Regions.All)
        {
            var m = RegionModifiers.For(def.Id);
            Assert.NotEqual(RegionModifier.None, m);
            Assert.True(m.EnemyHealthMult > 1f || m.EnemyDamageMult > 1f || m.LootTierBonus > 0,
                $"{def.Id}'s modifier '{m.Name}' has no effect");
        }
    }

    [Fact]
    public void test_deeper_regions_carry_a_bigger_loot_bonus()
    {
        // "Push deeper for better loot" — the last region out-rewards the first.
        Assert.True(RegionModifiers.For("pale_choir").LootTierBonus
                    > RegionModifiers.For("verdant_hollow").LootTierBonus);
    }

    [Fact]
    public void test_an_unknown_region_has_no_modifier()
    {
        Assert.Equal(RegionModifier.None, RegionModifiers.For("nowhere"));
    }
}
