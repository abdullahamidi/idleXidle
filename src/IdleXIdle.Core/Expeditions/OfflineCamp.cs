using System;
using System.Linq;
using IdleXIdle.Core.Warrens;

namespace IdleXIdle.Core.Expeditions;

/// <summary>
/// THE CAMP: how long an absence keeps paying, and at what share of live pay — both grown by the Warren.
/// </summary>
/// <remarks>
/// <para>
/// Until 2026-09-06 an absence was credited for up to twenty-four hours at half of live pay, from the
/// first minute of the game. A fresh account that slept came back with more Gleam than a week of
/// TRAINING could spend, and the Warren — the system that is supposed to BE the idle economy — had
/// nothing left to sell. The law is <b>the Warren must never outrun the hunter</b>, and its corollary
/// is that an account with no Warren must not out-earn one with a Warren by simply staying away.
/// </para>
/// <para>
/// So an absence has a CAPACITY and an EFFICIENCY, and both are Warren dimensions:
/// </para>
/// <list type="bullet">
/// <item><b>Capacity</b> — the hours the camp holds. <see cref="BaseHours"/> for a fresh account;
/// every facility level bought adds <see cref="HoursPerFacilityLevel"/>, up to <see cref="MaxHours"/>.
/// Two facilities at level 5 hold about three hours; eight at level 8 hold about eight; a developed
/// Warren holds the day's <see cref="MaxHours"/>.</item>
/// <item><b>Efficiency</b> — the share of live pay the hunt earns unwatched. <see cref="BaseEfficiency"/>
/// fresh, rising with the same levels to <see cref="MaxEfficiency"/>. Away is never as good as playing.</item>
/// </list>
/// <para>
/// The Warren's own production is held to the same capacity: a warren that produced for twenty-four
/// hours while the hunt stopped at two would outrun the hunter by definition. Everything here is
/// arithmetic over the Warren's public state, so the WARREN screen can print the capacity it sells.
/// </para>
/// </remarks>
public static class OfflineCamp
{
    /// <summary>What a fresh account's absence holds, in hours.</summary>
    public const float BaseHours = 2f;

    /// <summary>The most any camp holds — a full sleep, not a lost weekend.</summary>
    public const float MaxHours = 12f;

    /// <summary>Hours added per facility level bought — ten levels are one more hour.</summary>
    public const float HoursPerFacilityLevel = 0.1f;

    /// <summary>The share of live pay an unwatched hunt earns on a fresh account.</summary>
    public const float BaseEfficiency = 0.30f;

    /// <summary>The share a developed Warren reaches — still short of playing.</summary>
    public const float MaxEfficiency = 0.60f;

    /// <summary>Efficiency added per facility level bought — a hundred levels close the whole gap.</summary>
    public const float EfficiencyPerFacilityLevel = 0.003f;

    /// <summary>Facility levels BOUGHT — every facility starts at 1, so the first level is free.</summary>
    public static int FacilityLevels(Warren? warren) => warren?.FacilityLevelsBought ?? 0;

    public static float HoursFor(Warren? warren)
        => MathF.Min(MaxHours, BaseHours + FacilityLevels(warren) * HoursPerFacilityLevel);

    /// <summary>What the NEXT facility level would take the capacity to — the Warren screen's own reason to buy.</summary>
    public static float HoursAfterOneMoreLevel(Warren? warren)
        => MathF.Min(MaxHours, BaseHours + (FacilityLevels(warren) + 1) * HoursPerFacilityLevel);

    public static double CapSeconds(Warren? warren) => HoursFor(warren) * 3600.0;

    public static float EfficiencyFor(Warren? warren)
        => MathF.Min(MaxEfficiency, BaseEfficiency + FacilityLevels(warren) * EfficiencyPerFacilityLevel);

    /// <summary>The seconds an absence is paid for: the elapsed time, held to the camp's capacity.</summary>
    public static double CreditedSeconds(double elapsedSeconds, Warren? warren)
        => Math.Clamp(elapsedSeconds, 0.0, CapSeconds(warren));

    /// <summary>
    /// The hunt's credit for an absence, from a simulation of the CREDITED seconds: the measured live
    /// rate over those seconds at the camp's efficiency. (<see cref="OfflineHunt.Result.Gleam"/> is the
    /// same figure at <see cref="OfflineHunt.Haircut"/>; this is the one the host pays.)
    /// </summary>
    public static long HuntCredit(OfflineHunt.Result simulated, Warren? warren, double elapsedSeconds)
        => (long)Math.Round(simulated.GleamPerSecond * CreditedSeconds(elapsedSeconds, warren) * EfficiencyFor(warren));

    /// <summary>"THE CAMP HOLDS 2H OF HUNTING — EACH FACILITY LEVEL ADDS 6 MINUTES" — the sentence the screens say.</summary>
    public static string HoursText(float hours)
        => hours >= 1f && MathF.Abs(hours - MathF.Round(hours)) < 0.05f
            ? $"{(int)MathF.Round(hours)}H"
            : $"{(int)MathF.Floor(hours)}H {(int)MathF.Round((hours - MathF.Floor(hours)) * 60f)}M";
}
