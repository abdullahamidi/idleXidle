using System;
using System.IO;
using System.Linq;
using IdleXIdle.Game.Presentation;
using IdleXIdle.Game.Vfx;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE GENERIC HEAL RECEIVE (the remaining-skill sweep, Phase 0 / P0.5; design.md 4 RETURN and 5.28): a heal no recipe
/// claims is a chest glow (0.25, 300 ms) and ONE "+N" summed over 400 ms, silent; the <c>fx_heal</c> column is no longer
/// spawned. The arithmetic lives in <see cref="HealReceive"/>; the text pins hold the hunt's Heal case and light pass to it.
/// </summary>
public class HealReceiveTest
{
    private const float Frame = 1000f / 60f;

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Hunt() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));

    /// <summary>The hunt's Heal case, up to UNDYING.</summary>
    private static string HealCase()
    {
        var src = Hunt();
        var heal = src[src.IndexOf("case BattleEventKind.Heal:", StringComparison.Ordinal)..];
        return heal[..heal.IndexOf("case BattleEventKind.Undying:", StringComparison.Ordinal)];
    }

    [Fact]
    public void test_heals_within_400ms_print_one_summed_number()
    {
        // Arrange: three lifesteal sips inside one window
        var r = new HealReceive();
        r.Add(3, 1000f);
        r.Add(2, 1150f);
        r.Add(4, 1390f);

        // Act / Assert: nothing is due before the window closes...
        Assert.False(r.Update(1399f, out var early));
        Assert.Equal(0, early);
        // ...then ONE number, the sum, exactly once
        Assert.True(r.Update(1400f, out var sum));
        Assert.Equal(9, sum);
        Assert.False(r.Update(1500f, out var again));
        Assert.Equal(0, again);
    }

    [Fact]
    public void test_a_heal_after_the_window_starts_a_new_sum()
    {
        var r = new HealReceive();
        r.Add(5, 0f);
        Assert.True(r.Update(400f, out var first));
        Assert.Equal(5, first);

        // a heal after the flush opens a new window from ITSELF, not from the first heal
        r.Add(7, 600f);
        Assert.False(r.Update(800f, out _));
        Assert.True(r.Update(1000f, out var second));
        Assert.Equal(7, second);

        // a zero heal is not a heal: it neither opens a window nor lights the chest
        var z = new HealReceive();
        z.Add(0, 100f);
        Assert.Equal(0, z.Pending);
        Assert.Equal(0f, z.GlowAlpha(100f));
        // the wave's end prints what is still open
        r.Add(2, 1200f);
        Assert.True(r.Flush(out var last));
        Assert.Equal(2, last);
    }

    [Fact]
    public void test_the_glow_peaks_at_a_quarter_and_is_gone_by_300ms()
    {
        var r = new HealReceive();
        Assert.Equal(0f, r.GlowAlpha(0f));   // no heal, no glow

        r.Add(4, 2000f);
        Assert.Equal(0.25f, r.GlowAlpha(2000f), 4);
        Assert.Equal(0f, r.GlowAlpha(1999f));          // nothing before the heal
        var mid = r.GlowAlpha(2150f);
        Assert.InRange(mid, 0.01f, 0.25f * 0.5f);      // EASE-OUT: under half the peak at half the life
        Assert.True(r.GlowAlpha(2100f) > mid && mid > r.GlowAlpha(2250f), "the glow falls monotonically");
        Assert.Equal(0f, r.GlowAlpha(2300f));
        Assert.Equal(0f, r.GlowAlpha(5000f));

        // it restarts from the LATEST heal (a stream of sips keeps the chest lit, never brighter than the peak)
        r.Add(1, 2200f);
        Assert.Equal(0.25f, r.GlowAlpha(2200f), 4);
        Assert.True(r.GlowAlpha(2400f) > 0f);

        // the break after a wave rests the playhead: the glow ages on the break's clock and is gone by 300 ms of it
        r.Age(150f);
        Assert.True(r.GlowAlpha(2200f) < 0.25f * 0.5f);
        r.Age(150f);
        Assert.Equal(0f, r.GlowAlpha(2200f));

        // a new wave's clock forgets it
        r.Reset();
        Assert.Equal(0f, r.GlowAlpha(2200f));
        Assert.Equal(0, r.Pending);
    }

    [Fact]
    public void test_the_receive_allocates_nothing_frame_to_frame()
    {
        var r = new HealReceive();
        var claims = new ClaimedMs();
        r.Add(1, 0f);
        r.Update(1f, out _);
        r.GlowAlpha(1f);
        claims.Claim(1);
        claims.IsClaimed(1);
        claims.Clear();
        r.Reset();   // the warm-up's sip is not part of the loop's sum

        var before = GC.GetAllocatedBytesForCurrentThread();
        var printed = 0;
        var heals = 0;
        // 10 s at 60 Hz: a sip every ~250 ms, a claimed heal every second
        for (var ms = 0f; ms < 10_000f; ms += Frame)
        {
            var at = (int)ms;
            if (at % 1000 < Frame) claims.Claim(at);
            if (at % 250 < Frame && !claims.IsClaimed(at)) { r.Add(3, ms); heals++; }
            if (r.Update(ms, out var flushed)) printed += flushed;
            r.GlowAlpha(ms);
        }
        if (r.Flush(out var tail)) printed += tail;
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);

        // ...and every unclaimed heal was printed once, in a sum
        Assert.Equal(heals * 3, printed);
        Assert.True(heals > 20, $"the loop healed {heals} times");
    }

    [Fact]
    public void test_the_heal_case_spawns_no_heal_column_and_plays_no_sound()
    {
        var heal = HealCase();
        Assert.DoesNotContain("HealColumn", heal);
        Assert.DoesNotContain("PlayFx(", heal);
        Assert.DoesNotContain("Sound?.Play", heal);
        Assert.DoesNotContain("Say(", heal);   // no number per event: the sum prints it
        Assert.Contains("if (!_healClaims.IsClaimed(e.AtMs)) _healReceive.Add(e.Amount, e.AtMs);", heal);
        // the column's profile is out of the table (the strip stays on disk until the sweep's legacy move)
        Assert.DoesNotContain(VfxProfiles.All, p => p.AssetKey == "fx_heal");
    }

    [Theory]
    [InlineData(0, 1)]        // no pool: the floor is 1
    [InlineData(57, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]      // 1 % rounds up
    [InlineData(1450, 15)]
    public void test_the_number_floor_is_one_percent_of_max_health_at_least_one(int maxHealth, int floor)
    {
        Assert.Equal(floor, HealReceive.NumberFloor(maxHealth));
        Assert.True(HealReceive.ShowsNumber(floor, maxHealth));
        Assert.False(HealReceive.ShowsNumber(floor - 1, maxHealth));
    }

    [Fact]
    public void test_a_one_point_tick_lights_the_chest_but_prints_nothing()
    {
        // MIRE's 1-point Heal on a hunter of 700: the glow says "healed", the text stays quiet
        var receive = new HealReceive();
        receive.Add(1, 9400f);
        Assert.True(receive.GlowAlpha(9400f) > 0f);
        Assert.True(receive.Update(9800f, out var sum));
        Assert.Equal(1, sum);
        Assert.False(HealReceive.ShowsNumber(sum, 700));
        // DRINK's summed 57 prints
        Assert.True(HealReceive.ShowsNumber(57, 700));
        Assert.Equal(0.01f, HealReceive.NumberShare);
    }

    [Fact]
    public void test_the_hunt_prints_the_sum_and_draws_the_glow_in_the_light_pass()
    {
        var src = Hunt();
        // the one number per window, from Update, still following DAMAGE NUMBERS
        Assert.Contains("if (_healReceive.Update(_playheadMs, out var healed)) SayHealed(healed);", src);
        // ...as a NUMBER at his chest, in its own lane, never through Say (the skill-name lane): the review of Phase 0
        var say = src[src.IndexOf("private void SayHealed(int healed)", StringComparison.Ordinal)..];
        say = say[..say.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.Contains("if (!ShowDamageNumbers || !HealReceive.ShowsNumber(healed, _champ?.MaxHealth ?? 0)) return;", say);
        Assert.DoesNotContain("Say(", say.Replace("SayHealed(", ""));
        Assert.Contains("Lane = CalloutLane.HunterBody,", say);
        Assert.Contains("Px = DamagePx,", say);
        Assert.Contains("_champDrawBox", say);
        Assert.Contains("HealGlowChestY", say);
        // the glow opens the light pass on its own and rides the drawn box (a lunge)
        Assert.Contains("|| healGlow > 0f))", src);
        Assert.Contains("if (healGlow > 0f) DrawHealGlow(b, healGlow);", src);
        var draw = src[src.IndexOf("private void DrawHealGlow(", StringComparison.Ordinal)..];
        draw = draw[..draw.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.Contains("\"fxp_flash_soft\"", draw);
        Assert.Contains("_champDrawBox", draw);
        Assert.Contains("HealGlowInk = new(0x7F, 0xCB, 0x4A)", src);
        // per wave: the open sum is printed, then the clock and the claims are forgotten
        var begin = src[src.IndexOf("private void BeginWave()", StringComparison.Ordinal)..];
        begin = begin[..begin.IndexOf("_reactions.Clear();", StringComparison.Ordinal)];
        Assert.Contains("FlushHealReceive();", begin);
        Assert.Contains("_healReceive.Reset();", begin);
        Assert.Contains("_healClaims.Clear();", begin);
        Assert.Contains("internal void ClaimHeal(int atMs)", src);
        // the wave's last heal prints with the clear, and the glow fades while the playhead rests
        var brk = src[src.IndexOf("if (_breakTimer > 0f)", StringComparison.Ordinal)..];
        brk = brk[..brk.IndexOf("ClearBeat.Tick(", StringComparison.Ordinal)];
        Assert.Contains("FlushHealReceive();", brk);
        Assert.Contains("_healReceive.Age(dt * 1000f * _speedMul);", brk);
    }
}
