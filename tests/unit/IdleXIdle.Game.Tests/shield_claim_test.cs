using System;
using System.IO;
using System.Linq;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE SHIELDGAINED CLAIM (the remaining-skill sweep, Phase 0 / P0.5; design.md 4 SHIELD and 5.29): a recipe that presents
/// a gain itself claims its ms, and the generic one-shot, <c>sfx_shield_gain</c> and "+N SHIELD" give way; the bar's rim
/// flare ALWAYS stays. A wave-open gain (ms 0: GROUNDWORK, CARRIED) is shown silently. The decision is
/// <see cref="ShieldGainShow.For"/> over a <see cref="ClaimedMs"/> table; the text pins hold the hunt's case to it.
/// </summary>
public class ShieldClaimTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Hunt() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));

    /// <summary>The hunt's ShieldGained case, up to the absorb.</summary>
    private static string GainCase()
    {
        var src = Hunt();
        var gain = src[src.IndexOf("case BattleEventKind.ShieldGained:", StringComparison.Ordinal)..];
        return gain[..gain.IndexOf("case BattleEventKind.ShieldAbsorbed:", StringComparison.Ordinal)];
    }

    [Fact]
    public void test_a_claimed_gain_keeps_the_rim_and_drops_the_cue_and_one_shot()
    {
        // Arrange: HOLD FAST's tick at 2000 claims its gain
        var claims = new ClaimedMs();
        claims.Claim(2000);

        // Act
        var show = ShieldGainShow.For(2000, claims.IsClaimed(2000));

        // Assert: the rim only
        Assert.Equal(new ShieldGainShow(Rim: true, Cue: false, OneShot: false, Number: false), show);
        // a claim names its ms and no other
        Assert.False(claims.IsClaimed(2001));
        // and the hunt obeys each flag; the rim flare is unconditional
        var gain = GainCase();
        Assert.Contains("var show = ShieldGainShow.For(e.AtMs, _shieldClaims.IsClaimed(e.AtMs));", gain);
        Assert.Contains("if (show.Cue) Sound?.Play(\"sfx_shield_gain\"", gain);
        Assert.Contains("if (show.OneShot) PlayFx(VfxProfiles.ShieldGain", gain);
        Assert.Contains("if (show.Number && ShowDamageNumbers) Say(", gain);
        Assert.Contains("\n                    UiMotion.Flash(ShieldGainKey, UiMotion.Transition);", gain);
        Assert.Equal(1, gain.Split("Sound?.Play(").Length - 1);
        Assert.Equal(1, gain.Split("PlayFx(").Length - 1);
        Assert.Contains("internal void ClaimShieldGain(int atMs) => _shieldClaims.Claim(atMs);", Hunt());
    }

    [Fact]
    public void test_a_wave_open_gain_is_silent()
    {
        // GROUNDWORK / CARRIED: the barrier is simply up at ms 0: no cue, no one-shot, no callout; the bar's rim stays
        var show = ShieldGainShow.For(0, claimed: false);
        Assert.True(show.Rim);
        Assert.False(show.Cue);
        Assert.False(show.OneShot);
        Assert.False(show.Number);
        Assert.Equal(show, ShieldGainShow.For(0, claimed: true));
    }

    [Fact]
    public void test_an_unclaimed_mid_wave_gain_is_unchanged()
    {
        // the existing grammar: rim + sfx_shield_gain 0.40 + the one-shot + "+N SHIELD"
        Assert.Equal(new ShieldGainShow(true, true, true, true), ShieldGainShow.For(1500, claimed: false));
        var gain = GainCase();
        Assert.Contains("Sound?.Play(\"sfx_shield_gain\", 0.40f, vary: 0.05f);", gain);
        Assert.Contains("PlayFx(VfxProfiles.ShieldGain, VfxSubject.Champion, Steel);", gain);
        Assert.Contains("Say($\"+{e.Amount} SHIELD\", Steel);", gain);
        // absorb and break are untouched by the claim
        var src = Hunt();
        var rest = src[src.IndexOf("case BattleEventKind.ShieldAbsorbed:", StringComparison.Ordinal)..];
        rest = rest[..rest.IndexOf("PlayShieldBreak();", StringComparison.Ordinal)];
        Assert.DoesNotContain("_shieldClaims", rest);
        Assert.Contains("Sound?.Play(\"sfx_shield_hit\", 0.26f, vary: 0.07f);", rest);
        Assert.Contains("Sound?.Play(\"sfx_shield_break\", 0.58f, vary: 0.03f);", rest);
    }

    [Fact]
    public void test_claims_are_a_fixed_table_cleared_per_wave()
    {
        var claims = new ClaimedMs(capacity: 4);
        claims.Claim(100);
        claims.Claim(100);   // the same claim
        Assert.Equal(1, claims.Count);
        foreach (var ms in new[] { 200, 300, 400, 500 }) claims.Claim(ms);
        // full: the oldest is overwritten, the newest held
        Assert.Equal(4, claims.Count);
        Assert.False(claims.IsClaimed(100));
        Assert.True(claims.IsClaimed(500));
        claims.Clear();
        Assert.Equal(0, claims.Count);
        Assert.False(claims.IsClaimed(500));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClaimedMs(0));

        // the hunt forgets every claim with the wave's clock
        var src = Hunt();
        var begin = src[src.IndexOf("private void BeginWave()", StringComparison.Ordinal)..];
        begin = begin[..begin.IndexOf("_reactions.Clear();", StringComparison.Ordinal)];
        Assert.Contains("_shieldClaims.Clear();", begin);
    }
}
