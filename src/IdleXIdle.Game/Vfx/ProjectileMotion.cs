using System;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Vfx;

/// <summary>One point of a projectile's recent flight: where its rear was, and when.</summary>
public readonly record struct TrailSample(Vector2 Position, float Time);

/// <summary>
/// A fixed-capacity ring of a projectile's recent rear positions — the ONE thing a trail is drawn from.
/// </summary>
/// <remarks>
/// The trail follows the projectile's real world path because it is made of the positions the projectile
/// actually occupied, sampled once per update. Fixed capacity: pushing past it drops the oldest sample, and
/// nothing is allocated after construction.
/// </remarks>
public sealed class TrailHistory
{
    private readonly TrailSample[] _samples;
    private int _newest = -1;

    /// <summary>A history that keeps at most <paramref name="capacity"/> samples.</summary>
    public TrailHistory(int capacity) => _samples = new TrailSample[Math.Max(2, capacity)];

    /// <summary>How many samples are held.</summary>
    public int Count { get; private set; }

    /// <summary>The most it will ever hold.</summary>
    public int Capacity => _samples.Length;

    /// <summary>Record where the rear was at <paramref name="time"/>.</summary>
    public void Push(Vector2 position, float time)
    {
        _newest = (_newest + 1) % _samples.Length;
        _samples[_newest] = new TrailSample(position, time);
        if (Count < _samples.Length) Count++;
    }

    /// <summary>The <paramref name="k"/>-th newest sample (0 = the newest).</summary>
    public TrailSample this[int k]
    {
        get
        {
            if ((uint)k >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(k));
            return _samples[((_newest - k) % _samples.Length + _samples.Length) % _samples.Length];
        }
    }

    /// <summary>Forget everything.</summary>
    public void Clear() { Count = 0; _newest = -1; }
}

/// <summary>
/// The arithmetic of a composite projectile (ADR-010). Pure: no GraphicsDevice, no clock of its own.
/// </summary>
public static class ProjectileMotion
{
    /// <summary>
    /// The flight's position curve: the SAME ease-out <c>VfxPlayer</c> has always used for travelling
    /// effects, so the world trajectory and its timing do not change with the composite.
    /// </summary>
    public static float Ease(float life)
    {
        var t = Math.Clamp(life, 0f, 1f);
        return 1f - (1f - t) * (1f - t);
    }

    /// <summary>
    /// THE BODY-SIZE CONTRACT: the scale that makes the head's visible length
    /// <see cref="ProjectileLook.HeadLength"/> × the caster's visible height.
    /// </summary>
    /// <param name="headContentPx">The head texture's content length, in texture pixels (its own box, one frame).</param>
    /// <param name="casterHeight">The caster's visible height, in canvas pixels.</param>
    /// <param name="headLength">The look's head length share.</param>
    /// <remarks>
    /// Nothing else enters: not the strip's union box (the rotating pilot shrank to 40 % through it), not
    /// the trail, not the wobble. The head is the gameplay element; it keeps one size for its whole flight.
    /// </remarks>
    public static float HeadScale(float headContentPx, float casterHeight, float headLength)
        => headLength * Math.Max(1f, casterHeight) / Math.Max(1f, headContentPx);

    /// <summary>
    /// The share of the flight at which the head is <paramref name="contactPx"/> from its target: the inverse
    /// of <see cref="Ease"/> over a flight <paramref name="distance"/> long.
    /// </summary>
    public static float ContactLife(float distance, float contactPx)
    {
        if (distance <= 0f) return 0f;
        var remaining = Math.Clamp(contactPx / distance, 0f, 1f);
        return 1f - MathF.Sqrt(remaining);
    }

    /// <summary>
    /// THE PHYSICAL THROW (ADR-011): how far along its line a thrown blade is at <paramref name="u"/> (0..1 of
    /// its flight). Nearly constant speed — <paramref name="departure"/> 0.06 leaves 6 % faster than average and
    /// arrives 6 % slower — so it keeps its momentum through contact instead of coasting in on the old ease-out.
    /// </summary>
    public static float ThrowProgress(float u, float departure)
    {
        var t = Math.Clamp(u, 0f, 1f);
        return t + departure * t * (1f - t);
    }

    /// <summary>
    /// Where a thrown blade is at <paramref name="u"/>: along its line by <see cref="ThrowProgress"/>, bowed
    /// sideways by <paramref name="bulge"/> pixels at mid-flight (the fan separating), exactly at
    /// <paramref name="to"/> at u = 1.
    /// </summary>
    public static Vector2 ThrowPosition(Vector2 from, Vector2 to, float bulge, float departure, float u)
    {
        var line = to - from;
        var normal = line.LengthSquared() > 1e-6f ? Vector2.Normalize(new Vector2(-line.Y, line.X)) : Vector2.Zero;
        var t = Math.Clamp(u, 0f, 1f);
        return from + line * ThrowProgress(t, departure) + normal * (bulge * MathF.Sin(MathHelper.Pi * t));
    }

    /// <summary>The direction a thrown blade is moving at <paramref name="u"/> (its heading), unit length.</summary>
    public static Vector2 ThrowHeading(Vector2 from, Vector2 to, float bulge, float departure, float u)
    {
        var line = to - from;
        var normal = line.LengthSquared() > 1e-6f ? Vector2.Normalize(new Vector2(-line.Y, line.X)) : Vector2.Zero;
        var t = Math.Clamp(u, 0f, 1f);
        var v = line * (1f + departure * (1f - 2f * t)) + normal * (bulge * MathHelper.Pi * MathF.Cos(MathHelper.Pi * t));
        return v.LengthSquared() > 1e-6f ? Vector2.Normalize(v) : Vector2.UnitX;
    }

    /// <summary>How far the head still has to go, in canvas pixels.</summary>
    public static float Remaining(Vector2 position, Vector2 target) => Vector2.Distance(position, target);

    /// <summary>The head's orientation offset from its direction of travel, radians: a small, continuous wobble.</summary>
    public static float Wobble(float time, float degrees, float hz, float phase)
        => MathHelper.ToRadians(degrees) * MathF.Sin(MathHelper.TwoPi * hz * time + phase);

    /// <summary>
    /// What is left of something aged <paramref name="age"/> of a <paramref name="lifetime"/>: 1 at birth,
    /// 0 at the end, shaped by <paramref name="power"/> (higher = gone sooner).
    /// </summary>
    public static float Fall(float age, float lifetime, float power)
    {
        if (lifetime <= 0f || age >= lifetime) return 0f;
        if (age <= 0f) return 1f;
        return MathF.Pow(1f - age / lifetime, power);
    }

    /// <summary>
    /// Where the glint is along its run (0 = starting at the rear, 1 = reaching the tip), or -1 outside
    /// its window. It crosses once per flight; it never flashes every frame.
    /// </summary>
    public static float GlintProgress(float life, float start, float end)
    {
        if (end <= start || life < start || life > end) return -1f;
        return (life - start) / (end - start);
    }

    /// <summary>A deterministic value in [0, 1) for (seed, index) — so a capture shows the same flight every run.</summary>
    public static float Hash01(int seed, int index)
    {
        unchecked
        {
            var x = (uint)(seed * 73856093) ^ (uint)(index * 19349663) ^ 0x9E3779B9u;
            x ^= x >> 13; x *= 0x5BD1E995u; x ^= x >> 15; x *= 0x27D4EB2Du; x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>When the <paramref name="i"/>-th spark is shed, as a share of the flight: spread over its middle.</summary>
    public static float SparkLife(int i, int count, int seed)
    {
        var slot = (i + 0.5f) / Math.Max(1, count);
        return 0.18f + 0.62f * slot + 0.08f * (Hash01(seed, 100 + i) - 0.5f);
    }

    /// <summary>
    /// The direction of the <paramref name="i"/>-th impact shard: most of them FORWARD, inside the cone the
    /// projectile came in on, and the rest radial, so the hit reads as momentum carried into the target
    /// rather than a round explosion.
    /// </summary>
    public static Vector2 ShardDirection(int i, int count, float forwardShare, float spreadDegrees, Vector2 travel, int seed)
    {
        var dir = travel.LengthSquared() > 1e-6f ? Vector2.Normalize(travel) : Vector2.UnitX;
        var baseAngle = MathF.Atan2(dir.Y, dir.X);
        var forward = (int)MathF.Round(Math.Clamp(forwardShare, 0f, 1f) * count);
        float angle;
        if (i < forward)
        {
            // spread evenly across the cone, then jittered a little so the fan is not a comb
            var t = forward == 1 ? 0.5f : i / (float)(forward - 1);
            angle = baseAngle + MathHelper.ToRadians(spreadDegrees) * (2f * t - 1f)
                    + MathHelper.ToRadians(6f) * (Hash01(seed, 200 + i) - 0.5f);
        }
        else
        {
            var k = i - forward;
            var rest = Math.Max(1, count - forward);
            angle = baseAngle + MathHelper.TwoPi * (k + 0.5f) / rest + 0.5f * (Hash01(seed, 300 + i) - 0.5f);
        }
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}
