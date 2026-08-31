using System;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// Gift chests: the one chest that does not gamble. A new game is seeded with a WELCOME gift whose
/// contents are catalogue data — the starter's plainest weapon — minted the same on every open.
/// </summary>
public class GiftChestTest
{
    private static Chest Welcome() => GiftChests.WelcomeChest(Source.Nature, VerdantHollow.RegionId);

    [Fact]
    public void test_the_welcome_gift_is_the_starters_plainest_weapon()
    {
        var starter = CharacterRoster.Get(CharacterRoster.StarterId);
        var reward = Chests.Open(Welcome(), new Random(1));

        var weapon = Assert.Single(reward.Items);
        Assert.Equal(ItemBaseType.Weapon, weapon.BaseType);
        Assert.Equal(Rarity.Common, weapon.Rarity);
        Assert.Equal(1, weapon.ItemLevel);
        Assert.Equal(starter.Class, weapon.Class);
        // The FIRST shape the class lists — the plainest one, a blade for a WANDERER.
        Assert.Equal(ItemClasses.Get(starter.Class).WeaponFamilies[0], weapon.Family);
        Assert.Null(GearTraits.TraitOf(weapon));                     // no prefix: "NATURE BLADE", nothing more
        Assert.True(ItemClasses.CanWear(starter, weapon), "the starter cannot wear their own gift");
        Assert.Equal(LootTuning.Default.RaritySellValue[(int)Rarity.Common], weapon.SellValue);
        Assert.Empty(reward.Gems);
    }

    [Fact]
    public void test_a_gift_opens_the_same_on_every_seed()
    {
        // Deterministic contents: the seed that opens it must not matter, and neither must the build's
        // rarity tilt or the opener's class — a gift is handed over, not rolled.
        var a = Chests.Open(Welcome(), new Random(1));
        var b = Chests.Open(Welcome(), new Random(999), rarityBonus: 3f, favouredClass: ItemClass.Warden);

        Assert.Equal(a.Materials, b.Materials);
        Assert.Equal(a.Items.Select(i => i.InstanceId), b.Items.Select(i => i.InstanceId));
        Assert.Equal(a.Items.Select(i => (i.BaseType, i.Rarity, i.Class, i.Family)),
                     b.Items.Select(i => (i.BaseType, i.Rarity, i.Class, i.Family)));
    }

    [Fact]
    public void test_a_gift_takes_nothing_from_the_random_source()
    {
        // The next boss's chest must open exactly as it would have without the gift in front of it.
        var rng = new Random(7);
        Chests.Open(Welcome(), rng);
        Assert.Equal(new Random(7).Next(), rng.Next());
    }

    [Fact]
    public void test_an_unknown_gift_key_opens_as_an_ordinary_chest()
    {
        // Lenient like every name-keyed save field: a key this build does not know is not a crash, it
        // is the plain chest the grade and tier describe.
        var stranger = Welcome() with { Gift = "no_such_gift" };
        var reward = Chests.Open(stranger, new Random(3));

        Assert.True(reward.Materials > 0, "an unknown gift should roll like a plain chest and pay materials");
        Assert.All(reward.Items, i => Assert.NotEqual("itm_gift_welcome_weapon", i.InstanceId));
        Assert.Null(GiftChests.Get("no_such_gift"));
        Assert.Null(GiftChests.Get(null));
    }

    [Fact]
    public void test_the_new_game_seed_is_one_welcome_chest_from_the_starting_region()
    {
        var home = Regions.All.First(r => r.Id == VerdantHollow.RegionId);
        var chest = Assert.Single(GiftChests.NewGameChests());

        Assert.Equal(GiftChests.WelcomeKey, chest.Gift);
        Assert.Equal(Rarity.Common, chest.Rarity);
        Assert.Equal(1, chest.Tier);
        Assert.Equal(home.Theme, chest.Element);
        Assert.Equal(home.Id, chest.Region);
    }

    [Fact]
    public void test_the_gifts_weapon_carries_the_chests_element()
    {
        var reward = Chests.Open(GiftChests.WelcomeChest(Source.Shadow, null), new Random(1));
        Assert.Equal(Source.Shadow, Assert.Single(reward.Items).Element);

        var plain = Chests.Open(GiftChests.WelcomeChest(null, null), new Random(1));
        Assert.Null(Assert.Single(plain.Items).Element);
    }

    [Fact]
    public void test_a_gift_dossier_says_what_it_holds_not_what_it_might()
    {
        var d = ChestDossiers.For(Welcome());

        Assert.True(d.IsGift);
        Assert.Equal(GiftChests.Welcome.Title, d.Title);
        Assert.Equal(GiftChests.Welcome.CardPromise, d.FloorShort);
        Assert.Equal(GiftChests.Welcome.CardContents, d.RegionShort);
        Assert.Equal(GiftChests.Welcome.Lines, d.Lines);
        Assert.Contains("BLADE", d.Lines[0]);
        Assert.Contains(CharacterRoster.Get(CharacterRoster.StarterId).Name, d.Lines[0]);
        Assert.Equal("", d.RegionBlurb);   // a gift was not won anywhere; only rolled chests say where
        // The same width rule the rolled dossier is held to — the panel is the same panel.
        Assert.All(d.Lines, line => Assert.True(line.Length <= 52, $"\"{line}\" is too long for the chest card"));
        Assert.All(d.Lines, line => Assert.All(line, ch => Assert.True(ch < 128, $"'{ch}' is outside the font gate")));

        var rolled = ChestDossiers.For(Welcome() with { Gift = null });
        Assert.False(rolled.IsGift);
        Assert.Equal("COMMON CHEST", rolled.Title);
    }

    [Fact]
    public void test_a_gift_does_not_stack_with_a_plain_chest_that_reads_the_same()
    {
        // Identical grade, tier, element and region — but one is a gift. Two cards, never one ×2.
        var pile = new[] { Welcome(), Welcome() with { Gift = null } };
        var stacks = ChestDossiers.Stacked(pile);

        Assert.Equal(2, stacks.Count);
        Assert.All(stacks, s => Assert.Equal(1, s.Count));

        // And two gifts do stack, like any two identical chests.
        Assert.Single(ChestDossiers.Stacked(new[] { Welcome(), Welcome() }));
    }
}
