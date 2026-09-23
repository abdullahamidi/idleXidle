using System;
using System.IO;
using System.Linq;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE COMPOSITE PROJECTILE CONTRACT (ADR-010, 2026-09-23): a projectile is a head of one readable size, a
/// trail made of where it really was, one glint, a few sparks and a directional impact — composed at
/// runtime, not played from a strip.
/// </summary>
/// <remarks>
/// <para>
/// The two regressions this file exists to stop. First, the body shrinking: a strip is sized by the box
/// around ALL its frames, so the tumbling pilot drew its knife at 0.36 of the hunter where the old strip drew
/// ~0.66. The head is sized from its own length and the caster's height, and nothing else. Second, the
/// flight changing: the composite must travel the same eased path, on the same clock, as every travelling
/// effect always has.
/// </para>
/// <para>
/// No graphics device: <see cref="ProjectileVisual.Place"/> only stores its textures and the flight never
/// reads them, so the flight is driven here with the textures left null.
/// </para>
/// </remarks>
public class VfxProjectileCompositeTest
{
    private const float Dt = 1f / 60f;
    private const float FlightSeconds = 1f;          // cast.projectile: 8 frames at 8 fps
    private static readonly Vector2 From = new(732, 683);
    private static readonly Vector2 To = new(1246, 754);
    private const float Caster = 412f;

    private static ProjectileVisual Placed(ProjectileLook look, int headContentWidth = 420, int headContentHeight = 130)
    {
        var v = new ProjectileVisual(look, seed: 7);
        v.Place(From, To, Caster, Color.White, null!, new Rectangle(46, 190, headContentWidth, headContentHeight), null!, null!, null!, null!, null!);
        return v;
    }

    /// <summary>Drive a flight on the runtime's clock until it lands (or its clock ends); returns the life at landing.</summary>
    private static float FlyToContact(ProjectileVisual v, Action<float>? each = null)
    {
        var elapsed = 0f;
        while (!v.Landed && elapsed < FlightSeconds)
        {
            elapsed += Dt;
            var life = Math.Clamp(elapsed / FlightSeconds, 0f, 1f);
            v.Fly(Dt, life, Color.White);
            each?.Invoke(life);
        }
        if (!v.Landed) v.Land();
        return elapsed / FlightSeconds;
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    [Theory]
    [InlineData(420, 130)]
    [InlineData(150, 150)]   // what the tumbling strip's union box would have measured
    [InlineData(512, 40)]
    public void test_the_head_length_depends_only_on_the_caster_not_on_the_texture_box(int w, int h)
    {
        var v = Placed(ProjectileLooks.SeekerKnife, w, h);
        Assert.Equal(ProjectileLooks.SeekerKnife.HeadLength * Caster, v.HeadLengthPx, 3);
    }

    [Fact]
    public void test_the_head_keeps_one_size_for_its_whole_flight()
    {
        var v = Placed(ProjectileLooks.SeekerKnife);
        var size = v.HeadLengthPx;
        FlyToContact(v, _ => Assert.Equal(size, v.HeadLengthPx));
    }

    [Fact]
    public void test_the_seeker_knife_is_not_the_shrunken_pilot()
    {
        // The old strip's blade read at ~0.66 of the hunter's height; the tumbling pilot's at 0.36.
        Assert.InRange(ProjectileLooks.SeekerKnife.HeadLength, 0.55f, 0.85f);
    }

    [Fact]
    public void test_the_flight_follows_the_runtime_ease_out_path()
    {
        var v = Placed(ProjectileLooks.SeekerKnife);
        FlyToContact(v, life =>
        {
            if (v.Landed) return;
            var t = 1f - (1f - life) * (1f - life);           // Anim.Drift's curve, restated
            var expected = From + (To - From) * t;
            Assert.True(Vector2.Distance(expected, v.Position) < 0.01f, $"life {life:0.00}: {v.Position} vs {expected}");
        });
    }

    [Fact]
    public void test_the_trail_is_made_of_points_on_the_real_path()
    {
        var v = Placed(ProjectileLooks.SeekerKnife);
        FlyToContact(v);
        var dir = Vector2.Normalize(To - From);
        var normal = new Vector2(-dir.Y, dir.X);
        Assert.True(v.TrailSamples > 4);
        for (var k = 0; k < v.TrailSamples; k++)
        {
            var off = Vector2.Dot(v.Trail(k).Position - From, normal);
            Assert.True(MathF.Abs(off) < 0.01f, $"sample {k} is {off:0.000} px off the flight line");
            if (k > 0) Assert.True(v.Trail(k).Time < v.Trail(k - 1).Time, "the newest sample comes first");
        }
    }

    [Fact]
    public void test_the_trail_history_is_bounded()
    {
        var h = new TrailHistory(32);
        for (var i = 0; i < 1000; i++) h.Push(new Vector2(i, 0), i);
        Assert.Equal(32, h.Count);
        Assert.Equal(999f, h[0].Time);
        Assert.Equal(968f, h[31].Time);
        Assert.Throws<ArgumentOutOfRangeException>(() => h[32]);
    }

    [Fact]
    public void test_the_flight_strikes_at_contact_before_its_clock_ends()
    {
        var look = ProjectileLooks.SeekerKnife;
        var v = Placed(look);
        var life = FlyToContact(v);
        Assert.True(v.Landed);
        Assert.True(life < 1f, "it must strike, not hover in front of the target until the clock runs out");
        Assert.True(Vector2.Distance(v.Position, To) <= v.HeadLengthPx * look.ContactReach + 0.01f);
    }

    [Fact]
    public void test_fall_starts_full_ends_empty_and_never_rises()
    {
        Assert.Equal(1f, ProjectileMotion.Fall(0f, 0.3f, 1.4f));
        Assert.Equal(0f, ProjectileMotion.Fall(0.3f, 0.3f, 1.4f));
        var last = 1f;
        for (var t = 0f; t <= 0.3f; t += 0.005f)
        {
            var k = ProjectileMotion.Fall(t, 0.3f, 1.4f);
            Assert.True(k <= last + 1e-6f);
            last = k;
        }
    }

    [Fact]
    public void test_the_glint_crosses_the_blade_once()
    {
        var look = ProjectileLooks.SeekerKnife;
        var windows = 0;
        var inside = false;
        var lastProgress = -1f;
        for (var life = 0f; life <= 1f; life += 0.002f)
        {
            var g = ProjectileMotion.GlintProgress(life, look.GlintStart, look.GlintEnd);
            if (g >= 0f && !inside) windows++;
            if (g >= 0f) { Assert.True(g >= lastProgress); lastProgress = g; }
            inside = g >= 0f;
        }
        Assert.Equal(1, windows);
    }

    [Fact]
    public void test_a_flight_sheds_at_most_its_looks_sparks()
    {
        var look = ProjectileLooks.SeekerKnife;
        var v = Placed(look);
        FlyToContact(v);
        Assert.InRange(v.SparksShed, 1, look.Sparks);
        Assert.InRange(look.Sparks, 1, 3);
    }

    [Fact]
    public void test_most_impact_shards_carry_the_incoming_direction()
    {
        var look = ProjectileLooks.SeekerKnife;
        var travel = To - From;
        var dir = Vector2.Normalize(travel);
        var cone = MathF.Cos(MathHelper.ToRadians(look.ImpactSpread + 4f));
        var forward = Enumerable.Range(0, look.ImpactShards)
            .Count(i => Vector2.Dot(ProjectileMotion.ShardDirection(i, look.ImpactShards, look.ImpactForward, look.ImpactSpread, travel, 7), dir) >= cone);
        Assert.True(forward >= (int)MathF.Round(look.ImpactForward * look.ImpactShards), $"{forward} of {look.ImpactShards} forward");
        Assert.True(forward > look.ImpactShards / 2);
    }

    [Fact]
    public void test_the_residue_finishes_and_not_before()
    {
        var look = ProjectileLooks.SeekerKnife;
        var v = Placed(look);
        FlyToContact(v);
        Assert.False(v.Finished);
        var longest = new[] { look.LandedTrailSeconds, look.SparkSeconds, look.ImpactSeconds, look.FlashSeconds }.Max();
        for (var t = 0f; t < longest + 0.05f; t += Dt) v.Linger(Dt);
        Assert.True(v.Finished);
    }

    [Fact]
    public void test_a_flight_allocates_nothing_after_launch()
    {
        var v = Placed(ProjectileLooks.SeekerKnife);
        v.Fly(Dt, Dt, Color.White);                          // warm the JIT
        var before = GC.GetAllocatedBytesForCurrentThread();
        FlyToContact(v);
        for (var i = 0; i < 30; i++) v.Linger(Dt);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void test_a_launch_allocates_under_two_kilobytes()
    {
        // Everything a flight will ever hold — its trail ring, its sparks, its shards — is allocated here,
        // once. The threshold is the budget, not a measurement: ~1 KB today.
        _ = new ProjectileVisual(ProjectileLooks.SeekerKnife, 1);   // warm the JIT
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = new ProjectileVisual(ProjectileLooks.SeekerKnife, 2);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 1, 2048);
    }

    [Fact]
    public void test_contact_life_inverts_the_ease()
    {
        var distance = Vector2.Distance(From, To);
        var life = ProjectileMotion.ContactLife(distance, 56f);
        Assert.Equal(1f - 56f / distance, ProjectileMotion.Ease(life), 4);
    }

    [Fact]
    public void test_the_pre_impact_emphasis_is_only_its_last_moment()
    {
        var look = ProjectileLooks.SeekerKnife;
        var v = Placed(look);
        var emphasised = 0;
        FlyToContact(v, _ => { if (!v.Landed && v.PreImpactEmphasis > 0f) emphasised++; });
        // PreImpactSeconds of frames, give or take the frame it lands on — never most of the flight.
        Assert.InRange(emphasised * Dt, Dt, look.PreImpactSeconds + Dt);
    }

    [Fact]
    public void test_only_listed_strips_are_composed()
    {
        Assert.Same(ProjectileLooks.SeekerKnife, ProjectileLooks.For("fx_seeker_projectile_strip8_512"));
        Assert.Null(ProjectileLooks.For("fx_seeker_strike_strip8_512"));
        Assert.Null(ProjectileLooks.For("fx_projectile"));
    }

    [Fact]
    public void test_every_look_and_its_parts_exist_on_disk()
    {
        var vfx = RepoFile("assets", "art", "VFX");
        var files = Directory.EnumerateFiles(vfx, "*.png", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        foreach (var (strip, look) in ProjectileLooks.All)
        {
            Assert.Contains(strip, files);
            foreach (var key in new[] { look.HeadKey, look.TrailKey, look.GlintKey, look.SparkKey, look.ShardKey, look.FlashKey })
                Assert.True(File.Exists(Path.Combine(vfx, "parts", key + ".png")), $"{strip}: part {key} is missing");
        }
    }
}
