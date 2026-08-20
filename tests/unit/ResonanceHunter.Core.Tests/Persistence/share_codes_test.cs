using System.Collections.Generic;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Core.Tests.Persistence;

/// <summary>
/// Share codes: perfect round-trips, loud failures. Show, don't trade.
/// </summary>
public class ShareCodesTest
{
    private static ItemInstance Ornate() => new()
    {
        InstanceId = "itm_deadbeef", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary,
        SellValue = 200, ItemLevel = 23, Upgrades = 11,
        TraitOverride = ResonanceHunter.Core.Economy.GearTrait.Savage,
        Element = ResonanceHunter.Core.Automation.Source.Shadow,
        Gems = new List<ItemInstance>
        {
            new() { InstanceId = "gem_1", BaseType = ItemBaseType.Gem, Rarity = Rarity.Epic,
                    SellValue = 80, ItemLevel = 7 },
        },
    };

    [Fact]
    public void test_share_codes_item_round_trips_every_field_including_the_socketed_gem()
    {
        // Arrange
        var item = Ornate();

        // Act
        var code = ShareCodes.EncodeItem(item);
        var ok = ShareCodes.TryDecodeItem(code, out var back, out var error);

        // Assert
        Assert.True(ok, error);
        Assert.NotNull(back);
        Assert.Equal(item.InstanceId, back!.InstanceId);
        Assert.Equal(item.BaseType, back.BaseType);
        Assert.Equal(item.Rarity, back.Rarity);
        Assert.Equal(item.ItemLevel, back.ItemLevel);
        Assert.Equal(item.Upgrades, back.Upgrades);
        Assert.Equal(item.TraitOverride, back.TraitOverride);
        Assert.Equal(item.Element, back.Element);
        var gem = Assert.Single(back.Gems);
        Assert.Equal(7, gem.ItemLevel);
        Assert.True(ShareCodes.LooksLikeItem(code));
        Assert.False(ShareCodes.LooksLikeBuild(code));
    }

    [Fact]
    public void test_share_codes_build_round_trips_skills_keystones_and_mastery()
    {
        // Arrange
        var build = new ShareCodes.SharedBuild
        {
            Skills = new List<SavedSkill>
            {
                new() { Source = "Shadow", Form = "Strike", VowId = "vow_bloodied" },
                new() { Source = "Body", Form = "Aura", VowId = null },
            },
            Keystones = new List<string> { "rend", "glass_cannon" },
            Mastery = new List<string> { "heavy_hand", "sunder", "spec_strike" },
        };

        // Act
        var code = ShareCodes.EncodeBuild(build);
        var ok = ShareCodes.TryDecodeBuild(code, out var back, out var error);

        // Assert
        Assert.True(ok, error);
        Assert.Equal(2, back!.Skills.Count);
        Assert.Equal("vow_bloodied", back.Skills[0].VowId);
        Assert.Null(back.Skills[1].VowId);
        Assert.Equal(build.Keystones, back.Keystones);
        Assert.Equal(build.Mastery, back.Mastery);
        Assert.True(ShareCodes.LooksLikeBuild(code));
    }

    [Fact]
    public void test_share_codes_fail_loudly_never_half_decode()
    {
        // A truncated paste — the checksum catches it.
        var code = ShareCodes.EncodeItem(Ornate());
        Assert.False(ShareCodes.TryDecodeItem(code[..^6], out var item, out var err1));
        Assert.Null(item);
        Assert.NotEmpty(err1);

        // Not a code at all.
        Assert.False(ShareCodes.TryDecodeItem("hello there", out _, out var err2));
        Assert.Equal("THIS IS NOT A SHARE CODE.", err2);

        // A build code fed to the item decoder is refused, not misread.
        var bcode = ShareCodes.EncodeBuild(new ShareCodes.SharedBuild());
        Assert.False(ShareCodes.TryDecodeItem(bcode, out _, out _));

        // A future format version is named as such, not guessed at.
        var future = "RHI9." + code.Split('.')[1] + "." + code.Split('.')[2];
        Assert.False(ShareCodes.TryDecodeItem(future, out _, out var err3));
        Assert.Contains("NEWER VERSION", err3);
    }
}
