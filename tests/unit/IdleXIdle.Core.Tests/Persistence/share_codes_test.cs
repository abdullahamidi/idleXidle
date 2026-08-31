using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using Xunit;

namespace IdleXIdle.Core.Tests.Persistence;

/// <summary>
/// Share codes: perfect round-trips, loud failures. Show, don't trade.
/// </summary>
public class ShareCodesTest
{
    private static ItemInstance Ornate() => new()
    {
        InstanceId = "itm_deadbeef", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary,
        SellValue = 200, ItemLevel = 23, Upgrades = 11,
        TraitOverride = IdleXIdle.Core.Economy.GearTrait.Savage,
        Element = IdleXIdle.Core.Automation.Source.Shadow,
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

    /// <summary>An independent encoder for HOSTILE payloads — and a pin on the wire format
    /// itself: if Encode's format drifts, these crafted codes stop matching and fail loudly.</summary>
    private static string Craft(string prefix, string json)
    {
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(Encoding.UTF8.GetBytes(json));
        var payload = Convert.ToBase64String(buffer.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var h = 2166136261u;
        foreach (var c in payload) h = (h ^ c) * 16777619u;
        return $"{prefix}.{payload}.{h:x8}";
    }

    [Fact]
    public void test_share_codes_hostile_payloads_return_false_never_throw()
    {
        // The checksum is integrity, not authentication — a crafted code passes it, and none of
        // these may crash (the review measured ArgumentException and NREs escaping the old catch).
        var hostiles = new[]
        {
            // an unknown BaseType — Enum.Parse used to throw ArgumentException through the catch
            Craft("RHI1", "{\"InstanceId\":\"x\",\"BaseType\":\"Sword\",\"Rarity\":2,\"SellValue\":5,\"ItemLevel\":1}"),
            // an out-of-range Rarity — used to decode ok and index a colour table out of bounds
            Craft("RHI1", "{\"InstanceId\":\"x\",\"BaseType\":\"Weapon\",\"Rarity\":77,\"SellValue\":5,\"ItemLevel\":1}"),
            // an explicit-null Gems list — used to NRE inside FromSavedItem
            Craft("RHI1", "{\"InstanceId\":\"x\",\"BaseType\":\"Weapon\",\"Rarity\":2,\"SellValue\":5,\"ItemLevel\":1,\"Gems\":null}"),
            // not JSON at all
            Craft("RHI1", "))) not json ((("),
        };
        foreach (var code in hostiles)
        {
            var ok = ShareCodes.TryDecodeItem(code, out var item, out var error);
            Assert.False(ok, $"hostile code decoded: {code[..32]}...");
            Assert.Null(item);
            Assert.NotEmpty(error);
        }

        // Build payloads: `required` checks presence, not null — all of these must be refused,
        // because the inspect card prints exactly these fields.
        var hostileBuilds = new[]
        {
            Craft("RHB1", "{\"Skills\":null,\"Keystones\":[],\"Mastery\":[]}"),
            Craft("RHB1", "{\"Skills\":[{\"Source\":null,\"Form\":\"Strike\"}],\"Keystones\":[],\"Mastery\":[]}"),
            Craft("RHB1", "{\"Skills\":[],\"Keystones\":[null],\"Mastery\":[]}"),
            Craft("RHB1", "{\"Skills\":[],\"Keystones\":[],\"Mastery\":[\"" + new string('m', 300) + "\"]}"),
        };
        foreach (var code in hostileBuilds)
        {
            Assert.False(ShareCodes.TryDecodeBuild(code, out var build, out var err2));
            Assert.Null(build);
            Assert.NotEmpty(err2);
        }

        // The deflate bomb: a large low-entropy payload must be refused by the caps, not inflated.
        var bomb = Craft("RHI1", "{\"InstanceId\":\"" + new string('a', 2_000_000) + "\"}");
        Assert.False(ShareCodes.TryDecodeItem(bomb, out _, out var err3));
        Assert.NotEmpty(err3);
    }

    [Fact]
    public void test_share_codes_feedback_round_trips_progress_build_and_run_log()
    {
        // Arrange — a report with every field carrying a distinctive value.
        var feedback = new ShareCodes.SharedFeedback
        {
            Build = "1.0.0+c8049643",
            SaveVersion = 2,
            DeepestWave = 23,
            RegionsConquered = 3,
            CorruptionTier = 4,
            Gleam = 131_900_000,
            TrainingRanks = 29,
            Loadout = new ShareCodes.SharedBuild
            {
                Skills = new List<SavedSkill> { new() { Source = "Shadow", Form = "Strike", VowId = "vow_bloodied" } },
                Keystones = new List<string> { "glass_cannon" },
                Mastery = new List<string> { "heavy_hand" },
            },
            Worn = new List<ShareCodes.WornItemSummary>
            {
                new() { Slot = "Weapon", Rarity = 4, Level = 62 },
                new() { Slot = "Charm", Rarity = 2, Level = 12 },
            },
            RunLog = new List<RunReportSave>
            {
                new() { RegionId = "cinderworks", Depth = 18, Outcome = 1, WallWave = 18 },
            },
        };

        // Act
        var code = ShareCodes.EncodeFeedback(feedback);
        var ok = ShareCodes.TryDecodeFeedback(code, out var back, out var error);

        // Assert — every field survives, and the dispatchers agree about what kind of code it is.
        Assert.True(ok, error);
        Assert.True(ShareCodes.LooksLikeFeedback(code));
        Assert.False(ShareCodes.LooksLikeItem(code));
        Assert.False(ShareCodes.LooksLikeBuild(code));
        Assert.Equal("1.0.0+c8049643", back!.Build);
        Assert.Equal(2, back.SaveVersion);
        Assert.Equal(23, back.DeepestWave);
        Assert.Equal(3, back.RegionsConquered);
        Assert.Equal(4, back.CorruptionTier);
        Assert.Equal(131_900_000, back.Gleam);
        Assert.Equal(29, back.TrainingRanks);
        Assert.Equal("vow_bloodied", back.Loadout.Skills[0].VowId);
        Assert.Equal(new List<string> { "glass_cannon" }, back.Loadout.Keystones);
        Assert.Equal(2, back.Worn.Count);
        Assert.Equal("Weapon", back.Worn[0].Slot);
        Assert.Equal(62, back.Worn[0].Level);
        var run = Assert.Single(back.RunLog);
        Assert.Equal("cinderworks", run.RegionId);
        Assert.Equal(18, run.Depth);
    }

    [Fact]
    public void test_share_codes_feedback_checksum_rejects_tampering()
    {
        // Arrange — flip one payload character; the checksum must catch it, or a mangled paste would
        // half-decode into a report describing a game state that never existed.
        var code = ShareCodes.EncodeFeedback(new ShareCodes.SharedFeedback { Build = "1.0.0" });
        var parts = code.Split('.');
        var payload = parts[1].ToCharArray();
        payload[payload.Length / 2] = payload[payload.Length / 2] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{new string(payload)}.{parts[2]}";

        // Act / Assert — the flipped character is refused, and so is a truncated paste.
        Assert.False(ShareCodes.TryDecodeFeedback(tampered, out var back, out var error));
        Assert.Null(back);
        Assert.NotEmpty(error);

        Assert.False(ShareCodes.TryDecodeFeedback(code[..^5], out _, out var err2));
        Assert.NotEmpty(err2);
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
