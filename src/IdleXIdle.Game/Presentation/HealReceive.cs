using System;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// THE GENERIC HEAL RECEIVE (the remaining-skill sweep, design.md sections 4 RETURN and 5.28): a heal no recipe claims is
/// a soft chest glow and ONE "+N" summed over 400 ms, silent. It replaces the <c>fx_heal</c> column, which stood a whole
/// strip on the hunter for every lifesteal sip and printed a number per Heal event (a swing build said "+3 +2 +3" in a
/// stack over his head).
/// </summary>
/// <remarks>
/// <para>
/// A fixed-size accumulator: three numbers, no collection, nothing allocated frame to frame. The hunt adds each
/// unclaimed Heal at its fight millisecond, asks <see cref="Update"/> once per frame for a due sum, and reads
/// <see cref="GlowAlpha"/> in its light pass (a pure read: Draw never changes it).
/// </para>
/// <para>
/// The window opens on the FIRST heal not yet printed and closes 400 ms later, whatever arrived in between; a heal
/// after the flush starts the next sum. The glow restarts on every heal (the latest one), so a stream of sips keeps the
/// chest lit without stacking brightness.
/// </para>
/// </remarks>
public sealed class HealReceive
{
    /// <summary>How long a sum stays open after its first heal before its one "+N" prints (design.md 4 RETURN).</summary>
    public const float SumWindowMs = 400f;

    /// <summary>The chest glow's brightness at the heal (design.md 4 RETURN: 0.25).</summary>
    public const float GlowPeak = 0.25f;

    /// <summary>How long the chest glow takes to go from <see cref="GlowPeak"/> to nothing (design.md 4 RETURN: 300 ms).</summary>
    public const float GlowMs = 300f;

    /// <summary>
    /// The share of max health a sum must reach before it prints its "+N" (the review of Phase 0): the same 1 % the design's
    /// RETURN stream starts counting motes at (design.md 4 RETURN: 1 mote under 1 %). Below it the chest glow alone says
    /// "healed", so a 1-point MIRE or lifesteal sip does not print "+1" every tick.
    /// </summary>
    public const float NumberShare = 0.01f;

    /// <summary>The smallest sum that prints its number for a hunter of <paramref name="maxHealth"/>: 1 % of it, at least 1.</summary>
    public static int NumberFloor(int maxHealth) => Math.Max(1, (int)MathF.Ceiling(Math.Max(0, maxHealth) * NumberShare));

    /// <summary>True when a flushed sum of <paramref name="sum"/> prints its "+N" (see <see cref="NumberShare"/>).</summary>
    public static bool ShowsNumber(int sum, int maxHealth) => sum > 0 && sum >= NumberFloor(maxHealth);

    private int _pending;
    private float _firstMs = float.NaN;
    private float _latestMs = float.NaN;

    /// <summary>The health summed so far and not yet printed.</summary>
    public int Pending => _pending;

    /// <summary>Take one heal at its fight millisecond. A zero or negative amount is not a heal and is ignored.</summary>
    /// <param name="amount">The health the Heal event gave back.</param>
    /// <param name="playheadMs">The heal's fight millisecond (the event's, so the glow and the window read the fight's clock).</param>
    public void Add(int amount, float playheadMs)
    {
        if (amount <= 0) return;
        if (_pending == 0) _firstMs = playheadMs;
        _pending += amount;
        _latestMs = playheadMs;
    }

    /// <summary>
    /// Advance to <paramref name="playheadMs"/>: when the open sum's window has passed, hand back its total (once) and
    /// close it.
    /// </summary>
    /// <param name="playheadMs">The hunt's playhead.</param>
    /// <param name="flushAmount">The sum to print now, or 0.</param>
    /// <returns>True when a sum is due.</returns>
    public bool Update(float playheadMs, out int flushAmount)
    {
        if (_pending > 0 && playheadMs >= _firstMs + SumWindowMs) return Flush(out flushAmount);
        flushAmount = 0;
        return false;
    }

    /// <summary>Close the open sum now (the wave ended: its last heal still owes its number).</summary>
    /// <param name="flushAmount">The sum to print, or 0.</param>
    /// <returns>True when there was a sum.</returns>
    public bool Flush(out int flushAmount)
    {
        flushAmount = _pending;
        _pending = 0;
        _firstMs = float.NaN;
        return flushAmount > 0;
    }

    /// <summary>
    /// Age the chest glow by <paramref name="ms"/> while the fight's playhead rests (the break after a wave: no event reads
    /// it any more, and a glow read at a frozen playhead would hang lit). The sum's window is not moved.
    /// </summary>
    public void Age(float ms)
    {
        if (ms > 0f && !float.IsNaN(_latestMs)) _latestMs -= ms;
    }

    /// <summary>Forget everything (a new wave's clock, or a posed seek: the heals before it landed silently).</summary>
    public void Reset()
    {
        _pending = 0;
        _firstMs = float.NaN;
        _latestMs = float.NaN;
    }

    /// <summary>
    /// The chest glow's alpha at <paramref name="playheadMs"/>: <see cref="GlowPeak"/> on the latest heal, easing out to 0
    /// over <see cref="GlowMs"/> (quadratic: it leaves quickly and settles softly). 0 with no heal, before it, or after.
    /// </summary>
    public float GlowAlpha(float playheadMs)
    {
        if (float.IsNaN(_latestMs)) return 0f;
        var t = (playheadMs - _latestMs) / GlowMs;
        if (t < 0f || t >= 1f) return 0f;
        var left = 1f - t;
        return GlowPeak * left * left;
    }
}
