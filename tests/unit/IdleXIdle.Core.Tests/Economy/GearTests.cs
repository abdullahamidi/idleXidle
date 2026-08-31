using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// Guards the power fantasy. Playtest verdict was "I didn't feel that I'm getting stronger" — because
/// maxing the attack stat bought ~1.9x against content that gets ~75x tankier, and loot was only ever
/// currency. Gear is the fix: it must actually, measurably make you stronger.
/// </summary>
public class GearTests
{
    private static ItemInstance Item(ItemBaseType type, Rarity rarity) => new()
    {
        InstanceId = $"{type}_{rarity}", BaseType = type, Rarity = rarity, SellValue = 10,
    };

    [Fact]
    public void test_only_weapons_charms_and_focuses_are_wearable()
    {
        Assert.Equal(GearSlot.Weapon, Gear.SlotFor(ItemBaseType.Weapon));
        Assert.Equal(GearSlot.Charm, Gear.SlotFor(ItemBaseType.Charm));
        Assert.Equal(GearSlot.Focus, Gear.SlotFor(ItemBaseType.AbilityFocus));
        Assert.Null(Gear.SlotFor(ItemBaseType.Material));
        Assert.Null(Gear.SlotFor(ItemBaseType.CreatureCore));
    }

    /// <summary>Rarity must be a STEEP curve — a Legendary has to feel like an event, not a rounding error.</summary>
    [Fact]
    public void test_rarity_power_climbs_steeply()
    {
        var rarities = new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };
        var powers = rarities.Select(Gear.RarityPower).ToList();

        for (var i = 1; i < powers.Count; i++)
            Assert.True(powers[i] > powers[i - 1], "Each rarity must beat the one below it.");

        // A Legendary must be transformative versus a Common, not incremental.
        Assert.True(Gear.RarityPower(Rarity.Legendary) >= Gear.RarityPower(Rarity.Common) * 10f);
    }

    [Fact]
    public void test_equipping_a_weapon_multiplies_damage_and_swapping_returns_the_old_one()
    {
        var hunter = new Hunter();
        Assert.Equal(1f, hunter.GearDamageMultiplier); // bare-handed is the baseline

        var displaced = hunter.Equip(Item(ItemBaseType.Weapon, Rarity.Rare));
        Assert.Null(displaced);
        var withRare = hunter.GearDamageMultiplier;
        Assert.True(withRare > 1f);

        // Swapping in a Legendary returns the Rare and hits appreciably harder.
        var old = hunter.Equip(Item(ItemBaseType.Weapon, Rarity.Legendary));
        Assert.NotNull(old);
        Assert.Equal(Rarity.Rare, old!.Rarity);
        Assert.True(hunter.GearDamageMultiplier > withRare);
    }

    [Fact]
    public void test_a_charm_hardens_the_hunter()
    {
        var hunter = new Hunter();
        var baseDef = hunter.Defense;
        var baseHp = hunter.MaxHealth;

        hunter.Equip(Item(ItemBaseType.Charm, Rarity.Epic));

        Assert.True(hunter.Defense > baseDef);
        Assert.True(hunter.MaxHealth > baseHp);
    }

    [Fact]
    public void test_unequipping_returns_the_item_and_removes_its_power()
    {
        var hunter = new Hunter();
        hunter.Equip(Item(ItemBaseType.Weapon, Rarity.Epic));

        var removed = hunter.Unequip(GearSlot.Weapon);

        Assert.NotNull(removed);
        Assert.Equal(1f, hunter.GearDamageMultiplier);
    }

    /// <summary>Materials and cores can never be worn — they're crafting stock, not equipment.</summary>
    [Fact]
    public void test_materials_cannot_be_equipped()
    {
        var hunter = new Hunter();
        Assert.Null(hunter.Equip(Item(ItemBaseType.Material, Rarity.Legendary)));
        Assert.Equal(1f, hunter.GearDamageMultiplier);
    }

    /// <summary>THE regression guard: a geared Hunter must actually out-damage a bare one in real combat.</summary>
    /// <remarks>
    /// Measured through the LIVE fight (DamageBench runs the real SoloBattle against the dummy), not the
    /// retired manual-combat Encounter it once used — a worn Legendary weapon must transform the number.
    /// </remarks>
    [Fact]
    public void test_gear_makes_you_measurably_stronger_in_a_real_fight()
    {
        static long Damage(Hunter hunter)
        {
            var build = new Build();
            build.Weave(new EquippedSkill(
                new WovenAbility { Name = "STRIKE", Source = Source.Nature, Form = Form.Strike },
                FormBehaviour.BaseCooldownMs(Form.Strike)));
            return DamageBench.Measure(build, hunter).TotalDamage;
        }

        var bare = Damage(new Hunter());

        var geared = new Hunter();
        geared.Equip(Item(ItemBaseType.Weapon, Rarity.Legendary));

        Assert.True(Damage(geared) > bare * 4, $"A Legendary weapon must transform your damage. bare={bare} geared={Damage(geared)}");
    }

    /// <summary>Worn gear must survive a reload — losing it would be a silent, unexplained nerf.</summary>
    [Fact]
    public void test_worn_gear_survives_a_save_round_trip()
    {
        var weapon = Item(ItemBaseType.Weapon, Rarity.Legendary);
        var charm = Item(ItemBaseType.Charm, Rarity.Epic);

        var hunter = new Hunter();
        hunter.Equip(weapon);
        hunter.Equip(charm);

        var save = IdleXIdle.Core.Persistence.SaveSystem.Capture(
            hunter, new IdleXIdle.Core.Automation.Region("verdant_hollow", 15),
            new[] { weapon, charm }, 1_700_000_000_000L);

        var json = IdleXIdle.Core.Persistence.SaveSystem.Serialize(save);
        var loaded = IdleXIdle.Core.Persistence.SaveSystem.Deserialize(json, 1_700_000_000_000L).Save!;

        Assert.Equal(weapon.InstanceId, loaded.WornWeaponId);
        Assert.Equal(charm.InstanceId, loaded.WornCharmId);
        Assert.Null(loaded.WornFocusId);

        // And re-wearing from the restored inventory reproduces the exact power.
        var restoredInv = IdleXIdle.Core.Persistence.SaveSystem.RestoreInventory(loaded);
        var reborn = new Hunter();
        reborn.RestoreWorn(GearSlot.Weapon, restoredInv.First(i => i.InstanceId == loaded.WornWeaponId));
        Assert.Equal(hunter.GearDamageMultiplier, reborn.GearDamageMultiplier);
    }

    /// <summary>The power rating must visibly climb as you gear up — invisible growth is no growth.</summary>
    [Fact]
    public void test_power_rating_climbs_as_you_gear_up()
    {
        var hunter = new Hunter();
        var bare = hunter.PowerRating;

        hunter.Equip(Item(ItemBaseType.Weapon, Rarity.Legendary));
        hunter.Equip(Item(ItemBaseType.Charm, Rarity.Epic));

        Assert.True(hunter.PowerRating > bare * 2, "Gearing up must move the number a player can see.");
    }

    [Fact]
    public void test_item_level_deepens_a_piece_within_its_tier()
    {
        // REGRESSION: item level was nearly inert — a full iL1->iL60 loadout moved run depth ~6%, so
        // "push deeper for a better drop" (the whole point of ilvl) paid almost nothing. iL must refine
        // a piece measurably within its rarity tier.
        var fresh = Item(ItemBaseType.Weapon, Rarity.Legendary) with { ItemLevel = 1 };
        var deep = Item(ItemBaseType.Weapon, Rarity.Legendary) with { ItemLevel = 60 };

        Assert.True(Gear.WeaponDamageMultiplier(deep) > Gear.WeaponDamageMultiplier(fresh) * 1.4f,
            "a maxed item level must be markedly stronger than a fresh drop of the same rarity");
    }

    [Fact]
    public void test_item_level_stays_below_the_next_rarity_up()
    {
        // The ladder is rarity-FIRST: a maxed lower rarity may RIVAL, but must not OVERTAKE, a fresh
        // higher one — otherwise item level would quietly flatten the whole rarity ladder.
        var maxedEpic = Item(ItemBaseType.Weapon, Rarity.Epic) with { ItemLevel = 60 };
        var freshLegendary = Item(ItemBaseType.Weapon, Rarity.Legendary) with { ItemLevel = 1 };

        Assert.True(Gear.WeaponDamageMultiplier(maxedEpic) < Gear.WeaponDamageMultiplier(freshLegendary),
            "item level overtook a whole rarity — the ladder must stay rarity-first");
    }
}
