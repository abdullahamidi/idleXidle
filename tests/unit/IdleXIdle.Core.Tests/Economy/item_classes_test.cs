using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// Item classes: five groups of two champions, and the one rule — can this champion wear this?
/// </summary>
/// <remarks>
/// The roster half pins the catalogue to the roster (two champions per class, every champion in
/// exactly one, no champion walking a road its class is not named for). The item half pins the
/// mint paths (a drop is the favoured class four times in five; a weapon's family is one its class
/// carries), the save (a pre-class item loads as "anyone's" and keeps its id-derived family) and
/// the share code (class and family survive the round trip).
/// </remarks>
public class ItemClassesTest
{
    private static ItemInstance Piece(ItemBaseType type, ItemClass? cls = null, int? family = null, string id = "itm_test") => new()
    {
        InstanceId = id, BaseType = type, Rarity = Rarity.Rare, SellValue = 30, ItemLevel = 5,
        Class = cls, Family = family,
    };

    private static Character Champion(ItemClass cls) =>
        CharacterRoster.All.First(c => c.Class == cls);

    // ── The catalogue against the roster ──────────────────────────────────────────────────────────

    [Fact]
    public void test_every_class_has_exactly_two_champions_and_every_champion_has_exactly_one_class()
    {
        foreach (var def in ItemClasses.All)
        {
            Assert.Equal(2, def.ChampionIds.Count);
            foreach (var id in def.ChampionIds)
            {
                var c = CharacterRoster.Find(id);
                Assert.NotNull(c);
                Assert.Equal(def.Class, c!.Class);
            }
        }

        // Every champion appears in exactly one class's list, and it is the class on their card.
        foreach (var c in CharacterRoster.All)
        {
            var listedIn = ItemClasses.All.Where(d => d.ChampionIds.Contains(c.Id)).ToList();
            var home = Assert.Single(listedIn);
            Assert.Equal(c.Class, home.Class);
        }

        Assert.Equal(Enum.GetValues<ItemClass>().Length, ItemClasses.All.Count);
    }

    [Fact]
    public void test_no_champion_leans_a_different_road_than_their_class_is_built_for()
    {
        // A WARDEN who leans TEMPO would be the first lie on the card. A champion with no lean fits
        // any class; a champion with a lean must sit in the class named for that road.
        foreach (var c in CharacterRoster.All)
        {
            var road = ItemClasses.Get(c.Class).Road;
            Assert.True(c.Lean is null || c.Lean == road,
                $"{c.Name} leans {c.Lean} but wears {c.Class}, which is built for {road}.");
        }
    }

    [Fact]
    public void test_the_starter_is_a_wanderer_and_wanderers_carry_every_weapon_shape()
    {
        Assert.Equal(ItemClass.Wanderer, CharacterRoster.Get(CharacterRoster.StarterId).Class);
        Assert.Equal(ItemNaming.WeaponFamilies.Length, ItemClasses.Get(ItemClass.Wanderer).WeaponFamilies.Count);
        foreach (var def in ItemClasses.All)
            Assert.All(def.WeaponFamilies, f => Assert.InRange(f, 0, ItemNaming.WeaponFamilies.Length - 1));
    }

    // ── The rule ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_universal_slot_is_wearable_by_every_class_whatever_it_carries()
    {
        foreach (var type in new[] { ItemBaseType.Charm, ItemBaseType.Ring, ItemBaseType.AbilityFocus })
        foreach (var wearer in Enum.GetValues<ItemClass>())
        {
            Assert.True(ItemClasses.CanWear(wearer, Piece(type)));
            Assert.True(ItemClasses.CanWear(wearer, Piece(type, ItemClass.Warden)));
        }
    }

    [Fact]
    public void test_a_legacy_class_locked_item_with_no_class_is_wearable_by_everyone()
    {
        foreach (var type in new[] { ItemBaseType.Weapon, ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots })
        foreach (var wearer in Enum.GetValues<ItemClass>())
            Assert.True(ItemClasses.CanWear(wearer, Piece(type)));
    }

    [Fact]
    public void test_a_class_locked_item_is_wearable_only_by_its_own_class()
    {
        var helm = Piece(ItemBaseType.Helm, ItemClass.Bulwark);
        Assert.True(ItemClasses.CanWear(ItemClass.Bulwark, helm));
        Assert.True(Gear.CanWear(Champion(ItemClass.Bulwark), helm));
        foreach (var other in Enum.GetValues<ItemClass>().Where(c => c != ItemClass.Bulwark))
        {
            Assert.False(ItemClasses.CanWear(other, helm));
            Assert.False(Gear.CanWear(Champion(other), helm));
        }
    }

    [Fact]
    public void test_a_legacy_weapon_of_any_family_is_never_rejected_by_family()
    {
        // A pre-class bow keeps its bow and its null class; a WARDEN, who cannot be minted a bow, may
        // still wear the old one. The rule reads the class, never the family.
        var bow = Piece(ItemBaseType.Weapon, family: 1);
        Assert.False(ItemClasses.FamilyAllowed(ItemClass.Warden, 1));
        Assert.True(ItemClasses.CanWear(ItemClass.Warden, bow));
    }

    [Fact]
    public void test_materials_and_gems_are_not_wearable_by_anyone()
    {
        Assert.False(ItemClasses.CanWear(ItemClass.Wanderer, Piece(ItemBaseType.Material)));
        Assert.False(ItemClasses.CanWear(ItemClass.Wanderer, Piece(ItemBaseType.Gem)));
    }

    [Fact]
    public void test_why_not_names_the_slot_and_the_two_champions_who_can()
    {
        var helm = Piece(ItemBaseType.Helm, ItemClass.Warden);
        var reason = ItemClasses.WhyNot(Champion(ItemClass.Ranger), helm);
        Assert.Equal("A WARDEN'S HELM — THE ANVIL OR THE FALLING TOWER CAN WEAR IT", reason);
        Assert.Null(ItemClasses.WhyNot(Champion(ItemClass.Warden), helm));
        Assert.Null(ItemClasses.WhyNot(Champion(ItemClass.Ranger), Piece(ItemBaseType.Ring, ItemClass.Warden)));
    }

    [Fact]
    public void test_the_class_line_reads_the_three_cases()
    {
        Assert.Equal("WARDEN GEAR", ItemClasses.ClassLine(Piece(ItemBaseType.Helm, ItemClass.Warden)));
        Assert.Equal("ANY CLASS", ItemClasses.ClassLine(Piece(ItemBaseType.Ring)));
        Assert.Equal("ANY CLASS · OLD MAKE", ItemClasses.ClassLine(Piece(ItemBaseType.Helm)));
    }

    // ── The mint paths ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_chest_drops_are_the_favoured_class_four_times_in_five()
    {
        var rng = new Random(1234);
        var chest = new Chest { Rarity = Rarity.Rare, Tier = 5 };
        var locked = new List<ItemInstance>();
        for (var i = 0; i < 3000; i++)
            locked.AddRange(Chests.Open(chest, rng, favouredClass: ItemClass.Mystic).Items
                                  .Where(it => ItemClasses.IsClassLocked(it.BaseType)));

        Assert.True(locked.Count > 1000, $"only {locked.Count} class-locked pieces rolled");
        Assert.All(locked, it => Assert.NotNull(it.Class));
        var own = locked.Count(it => it.Class == ItemClass.Mystic) / (float)locked.Count;
        Assert.InRange(own, ClassRollTuning.Default.OwnClassChance - 0.04f, ClassRollTuning.Default.OwnClassChance + 0.04f);

        // The other fifth is spread over the other four, none of them missing.
        foreach (var other in Enum.GetValues<ItemClass>().Where(c => c != ItemClass.Mystic))
            Assert.Contains(locked, it => it.Class == other);
    }

    [Fact]
    public void test_universal_slots_never_mint_with_a_class()
    {
        var rng = new Random(77);
        var chest = new Chest { Rarity = Rarity.Epic, Tier = 8 };
        var universal = Enumerable.Range(0, 800)
            .SelectMany(_ => Chests.Open(chest, rng, favouredClass: ItemClass.Warden).Items)
            .Where(it => ItemClasses.IsUniversal(it.BaseType)).ToList();
        Assert.NotEmpty(universal);
        Assert.All(universal, it => Assert.Null(it.Class));
    }

    [Fact]
    public void test_a_minted_weapon_carries_a_family_its_class_can_hold()
    {
        var rng = new Random(99);
        var ctx = new KillContext { PowerTier = 3, FavouredClass = ItemClass.Warden };
        var weapons = Enumerable.Range(0, 2000)
            .SelectMany(_ => LootSystem.Roll(ctx, rng, LootTuning.Default))
            .Where(it => it.BaseType == ItemBaseType.Weapon).ToList();

        Assert.True(weapons.Count > 100, $"only {weapons.Count} weapons rolled");
        foreach (var w in weapons)
        {
            Assert.NotNull(w.Class);
            Assert.NotNull(w.Family);
            Assert.True(ItemClasses.FamilyAllowed(w.Class!.Value, ItemNaming.WeaponFamilyIndex(w)),
                $"a {w.Class} weapon rolled family {ItemNaming.WeaponFamilyIndex(w)}");
        }
        // Both of the class's shapes actually turn up — the roll is not stuck on one.
        var wardenFamilies = weapons.Where(w => w.Class == ItemClass.Warden).Select(w => w.Family!.Value).Distinct().ToList();
        Assert.Equal(ItemClasses.Get(ItemClass.Warden).WeaponFamilies.OrderBy(f => f), wardenFamilies.OrderBy(f => f));
    }

    [Fact]
    public void test_the_trader_stall_is_classed_the_same_way_and_never_sells_an_impossible_weapon()
    {
        for (var week = 202601; week <= 202652; week++)
        foreach (var offer in WanderingTrader.Stock(week, 10, LootTuning.Default, ItemClass.Ranger))
        {
            if (!ItemClasses.IsClassLocked(offer.BaseType)) { Assert.Null(offer.Class); continue; }
            Assert.NotNull(offer.Class);
            if (offer.BaseType == ItemBaseType.Weapon)
                Assert.True(ItemClasses.FamilyAllowed(offer.Class!.Value, ItemNaming.WeaponFamilyIndex(offer)));
        }

        // The stall is still the same stall for two players of the same class.
        var a = WanderingTrader.Stock(202634, 10, LootTuning.Default, ItemClass.Bulwark);
        var b = WanderingTrader.Stock(202634, 10, LootTuning.Default, ItemClass.Bulwark);
        Assert.Equal(a.Select(i => (i.InstanceId, i.Class, i.Family)), b.Select(i => (i.InstanceId, i.Class, i.Family)));
    }

    [Fact]
    public void test_a_merge_product_takes_the_majority_class_not_the_first()
    {
        // Review 2026-08-25: [WARDEN, RANGER, RANGER] fused into a WARDEN helm — two wearable pieces
        // spent on one the champion could not wear. Majority wins; a tie goes to the earliest input.
        var rng = new Random(9);
        var trio = new[]
        {
            Piece(ItemBaseType.Helm, ItemClass.Warden, id: "a"),
            Piece(ItemBaseType.Helm, ItemClass.Ranger, id: "b"),
            Piece(ItemBaseType.Helm, ItemClass.Ranger, id: "c"),
        };
        var product = Forge.Merge(trio, rng, ForgeTuning.Default, LootTuning.Default).Product;
        Assert.NotNull(product);
        Assert.Equal(ItemClass.Ranger, product!.Class);

        var tie = new[]
        {
            Piece(ItemBaseType.Helm, ItemClass.Mystic, id: "t1"),
            Piece(ItemBaseType.Helm, ItemClass.Bulwark, id: "t2"),
            Piece(ItemBaseType.Ring, id: "t3"),
        };
        Assert.Equal(ItemClass.Mystic, Forge.Merge(tie, rng, ForgeTuning.Default, LootTuning.Default).Product!.Class);
    }

    [Fact]
    public void test_a_merge_product_inherits_the_class_of_its_own_type_and_a_legacy_trio_stays_legacy()
    {
        var rng = new Random(5);
        var trio = new[]
        {
            Piece(ItemBaseType.Helm, ItemClass.Mystic, id: "a"),
            Piece(ItemBaseType.Helm, ItemClass.Warden, id: "b"),
            Piece(ItemBaseType.Ring, id: "c"),
        };
        var product = Forge.Merge(trio, rng, ForgeTuning.Default, LootTuning.Default).Product;
        Assert.NotNull(product);
        Assert.Equal(ItemBaseType.Helm, product!.BaseType);
        Assert.Equal(ItemClass.Mystic, product.Class);

        var legacy = new[] { Piece(ItemBaseType.Boots, id: "x"), Piece(ItemBaseType.Boots, id: "y"), Piece(ItemBaseType.Boots, id: "z") };
        var old = Forge.Merge(legacy, rng, ForgeTuning.Default, LootTuning.Default).Product;
        Assert.NotNull(old);
        Assert.Null(old!.Class);

        // A merged weapon keeps a kept input's shape when the class allows it, and never a shape it cannot.
        var blades = new[]
        {
            Piece(ItemBaseType.Weapon, ItemClass.Bulwark, family: 0, id: "w1"),
            Piece(ItemBaseType.Weapon, ItemClass.Bulwark, family: 0, id: "w2"),
            Piece(ItemBaseType.Weapon, ItemClass.Bulwark, family: 0, id: "w3"),
        };
        var fused = Forge.Merge(blades, rng, ForgeTuning.Default, LootTuning.Default).Product!;
        Assert.Equal(ItemClass.Bulwark, fused.Class);
        Assert.Equal(0, fused.Family);
    }

    // ── Persistence ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_pre_class_item_loads_with_no_class_and_keeps_its_id_derived_family()
    {
        // An old save never wrote Class or Family at all.
        var save = new SaveGame
        {
            Inventory =
            {
                new SavedItem { InstanceId = "itm_legacy_bow", BaseType = "Weapon", Rarity = 2, SellValue = 34, ItemLevel = 9 },
            },
        };
        var back = SaveSystem.RestoreInventory(SaveSystem.Deserialize(SaveSystem.Serialize(save), 0).Save!)[0];

        Assert.Null(back.Class);
        Assert.Null(back.Family);
        // The exact family the old derivation gave this id, so the art and the stat channel do not move.
        Assert.Equal(LegacyFamily("itm_legacy_bow"), ItemNaming.WeaponFamilyIndex(back));
        Assert.True(ItemClasses.CanWear(ItemClass.Warden, back));
    }

    [Fact]
    public void test_class_and_family_survive_a_save_round_trip_and_an_out_of_range_family_is_dropped()
    {
        // The DTO as a current build writes it (the write side is covered by the share-code test,
        // which goes through the same ToSavedItem).
        var save = new SaveGame
        {
            Inventory =
            {
                new SavedItem { InstanceId = "itm_spear", BaseType = "Weapon", Rarity = 2, SellValue = 30, ItemLevel = 5, Class = "Ranger", Family = 2 },
            },
        };
        var back = SaveSystem.RestoreInventory(SaveSystem.Deserialize(SaveSystem.Serialize(save), 0).Save!)[0];
        Assert.Equal(ItemClass.Ranger, back.Class);
        Assert.Equal(2, back.Family);

        var bad = new SaveGame { Inventory = { new SavedItem { InstanceId = "itm_bad", BaseType = "Weapon", Rarity = 1, SellValue = 1, Class = "Paladin", Family = 42 } } };
        var fixedUp = SaveSystem.RestoreInventory(SaveSystem.Deserialize(SaveSystem.Serialize(bad), 0).Save!)[0];
        Assert.Null(fixedUp.Class);
        Assert.Null(fixedUp.Family);
    }

    [Fact]
    public void test_a_share_code_keeps_class_and_family()
    {
        var item = Piece(ItemBaseType.Weapon, ItemClass.Mystic, family: 3, id: "itm_share");
        var code = ShareCodes.EncodeItem(item);
        Assert.True(ShareCodes.TryDecodeItem(code, out var back, out var error), error);
        Assert.Equal(ItemClass.Mystic, back!.Class);
        Assert.Equal(3, back.Family);
    }

    /// <summary>The family derivation as it was before classes: FNV-1a over id + "|fam", mod four.</summary>
    private static int LegacyFamily(string id)
    {
        var hash = 2166136261u;
        foreach (var ch in id + "|fam") { hash ^= ch; hash *= 16777619u; }
        return (int)(hash % (uint)ItemNaming.WeaponFamilies.Length);
    }
}
