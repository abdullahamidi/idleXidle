using System;
using ResonanceHunter.Core.Progression;

namespace ResonanceHunter.Core.Automation;

/// <summary>Tuning for region mastery. Data-driven, never inline literals.</summary>
/// <remarks>
/// The creature-farm half of this record (work-tick rates, team weights, the auto-sell Gleam cap)
/// was retired with the creature subsystem on 2026-08-24 — the fields left are the ones the LIVE
/// game still reads: mastery from the champion's own kills, and the thresholds that band it.
/// </remarks>
public sealed record AutomationTuning
{
    /// <summary>Region Mastery Points paid for each kill the champion lands in the region.</summary>
    public float RmpPerActiveKillBonus { get; init; } = 5f;

    public float RmpThreshold1 { get; init; } = 500f;    // Partially Mastered
    public float RmpThreshold2 { get; init; } = 2000f;   // Fully Mastered

    public static AutomationTuning Default { get; } = new();
}

/// <summary>
/// A region's per-region progress record: mastery earned by fighting there, and the deepest wave
/// ever held (the source of skill points).
/// </summary>
/// <remarks>
/// <para>
/// This class used to be the automated creature farm — teams of creatures assigned to it, ticking
/// out kills, Gleam and Cores. That whole subsystem was retired on 2026-08-24: it was fully built
/// and completely unreachable (its screen was never drawn, so no team could ever be assigned), which
/// means the farm never produced anything for any player. What survives is exactly what live play
/// feeds and reads: <see cref="RecordActiveKill"/> accrues mastery, <see cref="RecordDepth"/> tracks
/// the depth record, and <see cref="IdleEfficiencyPercent"/> reports the away-earnings band the map
/// shows — pinned to the no-team quality score the game has always effectively had.
/// </para>
/// <para>
/// <see cref="Progression.MasteryLevel.OptimizedTeam"/> is unreachable by design here: it required a
/// complete creature team, which no player could ever assemble. The ladder tops out at
/// <see cref="Progression.MasteryLevel.FullyMastered"/>, exactly as it always did in practice.
/// </para>
/// </remarks>
public sealed class Region
{
    private readonly AutomationTuning _tuning;

    public Region(string regionId, int parClearTimeSeconds, AutomationTuning? tuning = null)
    {
        RegionId = regionId;
        ParClearTimeSeconds = parClearTimeSeconds;
        _tuning = tuning ?? AutomationTuning.Default;
    }

    public string RegionId { get; }

    /// <summary>The fixed par anchor for this region (see EfficiencyContract). Never recomputed.</summary>
    public int ParClearTimeSeconds { get; }

    public float RegionMasteryPoints { get; private set; }

    /// <summary>Restore mastery from a save. Never call this during play — mastery is earned, not set.</summary>
    public void RestoreMasteryPoints(float points) => RegionMasteryPoints = MathF.Max(0f, points);

    /// <summary>
    /// The deepest wave ever held in THIS region, which is what skill points are paid for.
    /// </summary>
    /// <remarks>
    /// Per region, and first-time only: farming a depth already reached pays haul but no points, so the
    /// only way to earn them is to push somewhere new. A single global "deepest ever" would let a player
    /// bank the whole game's tree progress in one region and walk every other one for free.
    /// </remarks>
    public int BestDepth { get; private set; }

    /// <summary>Record a depth reached here. Returns true if it was a new record.</summary>
    public bool RecordDepth(int depth)
    {
        if (depth <= BestDepth) return false;
        BestDepth = depth;
        return true;
    }

    /// <summary>Restore from a save. Like mastery, never called during play.</summary>
    public void RestoreBestDepth(int depth) => BestDepth = Math.Max(0, depth);

    /// <summary>
    /// Formula 2 — Mastery Level, from accrued points.
    /// </summary>
    /// <remarks>
    /// The OptimizedTeam tier is not returned here: it was gated on a complete creature team, and the
    /// creature subsystem is retired. The two point thresholds behave exactly as they always did.
    /// </remarks>
    public MasteryLevel MasteryLevel
    {
        get
        {
            if (RegionMasteryPoints >= _tuning.RmpThreshold2) return Progression.MasteryLevel.FullyMastered;
            if (RegionMasteryPoints >= _tuning.RmpThreshold1) return Progression.MasteryLevel.PartiallyMastered;
            return Progression.MasteryLevel.NewlyConquered;
        }
    }

    /// <summary>
    /// Percent of par this region earns while the player is away — what the map's detail panel shows.
    /// </summary>
    /// <remarks>
    /// The team-quality term is pinned to 0: it measured the assigned creature team, which no player
    /// could ever assemble (the assignment screen was never reachable), so the LIVE value was always
    /// the mastery band's floor. Retiring the subsystem preserves that exact behaviour.
    /// </remarks>
    public float IdleEfficiencyPercent()
        => EfficiencyContract.IdleEfficiencyPercent(MasteryLevel, 0f);

    /// <summary>RMP for an active kill the player fought here — playing is what builds mastery.</summary>
    public void RecordActiveKill() => RegionMasteryPoints += _tuning.RmpPerActiveKillBonus;
}
