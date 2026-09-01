using System;
using System.Collections.Generic;

namespace IdleXIdle.Core.Encounters;

/// <summary>
/// Conquest, and the checkpoints a conquered region offers — the thing Memory Dust is for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Conquest is wave 20.</b> It was wave 7 (playtest 2026-08-26: "seven waves is far too short to
/// clear a map"). Beyond the bar the hunt's banner counts OVERWAVE — how far past the conquest this
/// descent has gone.
/// </para>
/// <para>
/// <b>Checkpoints.</b> Once a region is conquered, every <see cref="Step"/> waves the champion has ever
/// held there becomes a place the next descent may START from (playtest: "if I cleared wave 40 I should
/// be able to start from 0, 10, 20, 30 or 40"). A checkpoint start skips the waves below it — their
/// haul is not paid — and it costs MEMORY DUST every descent: <see cref="DustPerWave"/> per wave
/// skipped. That is the currency's whole job now. Dust used to buy traits, then bought nothing but
/// Warren upgrades, and the playtest read its pill as a lie ("it says it buys traits, and it piles up
/// with nowhere to go"). A repeatable, scaling sink tied to the deepest content is the honest answer:
/// the Warren mints Dust while you are away; Dust buys back the waves you have already walked.
/// </para>
/// </remarks>
public static class Checkpoints
{
    /// <summary>Waves held to conquer a region. Mirrored by the host's ConquerWaveDepth and the hunt's ConquerAt.</summary>
    public const int ConquestWave = 20;

    /// <summary>A checkpoint every this many waves.</summary>
    public const int Step = 10;

    /// <summary>Memory Dust per wave skipped, per descent. Tuning knob; pinned by checkpoints_test.</summary>
    public const int DustPerWave = 25;

    /// <summary>The start waves a region offers: 0, then every Step up to the deepest wave held — only once conquered.</summary>
    public static IReadOnlyList<int> Options(int bestDepth, bool conquered)
    {
        var list = new List<int> { 0 };
        if (!conquered) return list;
        for (var w = Step; w <= bestDepth; w += Step) list.Add(w);
        return list;
    }

    /// <summary>What a descent starting after <paramref name="startWave"/> costs, in Memory Dust.</summary>
    public static int DustCost(int startWave) => Math.Max(0, startWave) * DustPerWave;

    /// <summary>The nearest valid start at or below the wish — a save that remembers wave 40 in a region whose record is now lower falls back.</summary>
    public static int Clamp(int wish, int bestDepth, bool conquered)
    {
        if (!conquered || wish <= 0) return 0;
        var w = Math.Min(wish, bestDepth) / Step * Step;
        return Math.Max(0, w);
    }
}
