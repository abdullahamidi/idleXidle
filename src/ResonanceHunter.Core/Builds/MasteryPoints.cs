using System;
using System.Collections.Generic;

namespace ResonanceHunter.Core.Builds;

/// <summary>
/// How mastery points are earned: from FIRST-TIME DEPTH, per region, on a square-root curve.
/// </summary>
/// <remarks>
/// <para>
/// The previous rule was <c>3 + depth ÷ 5</c> per region — linear, with three free points. Playtest
/// 2026-08-25: "progress through the mastery tree is far too fast; almost at a specialisation in the
/// first fifteen minutes." It was: wave 40 in the first region paid 11 points, and a specialisation
/// costs 10 along its shortest path (a minor, a notable, the specialisation). The tree's whole design
/// is that ring 4 is a commitment and a second discipline is out of reach, and a linear faucet made
/// the first commitment a formality.
/// </para>
/// <para>
/// A square root pays quickly at first and slows for ever after, which is the shape a WALL should have:
/// the next point is always visible and always further than the last. With <see cref="Scale"/> = 0.8:
/// wave 25 → 4, wave 40 → 5, wave 64 → 6, wave 100 → 8, wave 150 → 9, wave 225 → 12 — per region.
/// The free three are gone: the tree opens later now (Unlocks: wave 25), and a player who has walked
/// twenty-five waves arrives with four points, which buys a minor and a notable and leaves the
/// specialisation as the first thing to want.
/// </para>
/// </remarks>
public static class MasteryPoints
{
    /// <summary>The multiplier on the square root of depth. Tuning knob; pinned by mastery_points_test.</summary>
    public const float Scale = 0.8f;

    /// <summary>Points one region's best depth is worth.</summary>
    public static int FromDepth(int bestDepth)
        => bestDepth <= 0 ? 0 : (int)MathF.Floor(MathF.Sqrt(bestDepth) * Scale);

    /// <summary>Points across every region — the number the tree's <c>SetEarned</c> is fed each frame.</summary>
    public static int Total(IEnumerable<int> bestDepths)
    {
        ArgumentNullException.ThrowIfNull(bestDepths);
        var total = 0;
        foreach (var d in bestDepths) total += FromDepth(d);
        return total;
    }
}
