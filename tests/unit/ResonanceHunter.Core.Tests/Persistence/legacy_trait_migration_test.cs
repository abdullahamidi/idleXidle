using System;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Core.Tests.Persistence;

/// <summary>
/// The prefix migration: pre-redesign saves keep the traits they HAD; new plain items stay plain.
/// </summary>
/// <remarks>
/// Adversarial review, pass five (HIGH): deleting the id-derived trait without this stripped every
/// legacy item's prefix AND its combat mods on load — a 2-5x stealth nerf across a worn loadout.
/// The disambiguator is the "NONE" sentinel new saves write for genuinely plain items.
/// </remarks>
public class LegacyTraitMigrationTest
{
    private static SaveGame WithItem(string? traitField) => new()
    {
        Inventory =
        {
            new SavedItem
            {
                InstanceId = "itm_legacy", BaseType = "Weapon", Rarity = 3, SellValue = 50,
                ItemLevel = 12, TraitOverride = traitField,
            },
        },
    };

    [Fact]
    public void test_save_migration_a_pre_redesign_item_keeps_its_id_derived_trait()
    {
        // Arrange: an old save never wrote the field at all.
        var json = SaveSystem.Serialize(WithItem(null));

        // Act
        var back = SaveSystem.RestoreInventory(SaveSystem.Deserialize(json, 0).Save!)[0];

        // Assert: the exact trait the old derivation produced, so no legacy loadout changes power.
        Assert.Equal(GearTraits.LegacyDerivedTrait("itm_legacy", ItemBaseType.Weapon), back.TraitOverride);
        Assert.NotNull(back.TraitOverride);
    }

    [Fact]
    public void test_save_migration_a_plain_item_stays_plain_across_a_round_trip()
    {
        // Arrange: a NEW mint that rolled no prefix — captured through the real converter.
        var plain = new ItemInstance
        {
            InstanceId = "itm_plain", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Epic,
            SellValue = 50, ItemLevel = 3,
        };
        var save = SaveSystem.Capture(
            new Hunter(), new ResonanceHunter.Core.Automation.Region("verdant_hollow", 15),
            new[] { plain }, nowMs: 0);

        // Act
        var back = SaveSystem.RestoreInventory(
            SaveSystem.Deserialize(SaveSystem.Serialize(save), 0).Save!)[0];

        // Assert: "NONE" round-trips to null — plain never resurrects a trait.
        Assert.Null(back.TraitOverride);
    }

    [Fact]
    public void test_save_migration_an_explicit_trait_round_trips_verbatim()
    {
        var json = SaveSystem.Serialize(WithItem(nameof(GearTrait.Savage)));
        var back = SaveSystem.RestoreInventory(SaveSystem.Deserialize(json, 0).Save!)[0];

        Assert.Equal(GearTrait.Savage, back.TraitOverride);
    }
}
