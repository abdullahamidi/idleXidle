using System;

namespace IdleXIdle.Game;

/// <summary>
/// <c>RH_PRESENT_TRACE=1</c>: one tab-separated line per PRESENTATION event of the fight, stamped with the
/// screen's own clock and the replay playhead — the evidence a synchronisation audit is read from.
/// </summary>
/// <remarks>
/// <para>
/// The fight is simulated ahead and replayed, and its presentation runs on several clocks: the replay
/// playhead (events, the champion's clip), the wall clock (effects, flashes, idle loops) and the sound
/// throttle's real time. A screenshot cannot say which of them put a picture or a sound where it is. This
/// log can: every clip start and drawn frame, every event crossed, every effect spawned and every sound
/// asked for, on one line each, in the order they happened.
/// </para>
/// <para>
/// Diagnostic only. Every call site guards on <see cref="Enabled"/> first, so a normal run builds no
/// strings: <c>if (PresentTrace.Enabled) PresentTrace.Log(...)</c>.
/// </para>
/// </remarks>
public static class PresentTrace
{
    /// <summary>True when <c>RH_PRESENT_TRACE</c> is set to 1.</summary>
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("RH_PRESENT_TRACE") is "1" or "true";

    /// <summary>The hunt screen's clock in milliseconds: the sum of every Update's delta.</summary>
    public static double ClockMs { get; private set; }

    /// <summary>The replay playhead the screen last set, in milliseconds of fight time.</summary>
    public static float PlayheadMs { get; set; }

    /// <summary>Advance the clock by one Update's delta, in seconds.</summary>
    public static void Tick(float dt) => ClockMs += dt * 1000.0;

    /// <summary>Write one line: <c>present, clock ms, playhead ms, kind, detail</c>.</summary>
    public static void Log(string kind, string detail)
    {
        if (Enabled) Console.WriteLine($"present\t{ClockMs:0}\t{PlayheadMs:0}\t{kind}\t{detail}");
    }
}
