namespace IdleXIdle.Core.Progression;

/// <summary>How far a region has been mastered — the three goals a region's own kills climb.</summary>
/// <remarks>
/// Formerly declared beside the idle "EfficiencyContract" (par times, idle-efficiency bands), deleted
/// 2026-08-31 (P13): no payout ever implemented the band — offline earnings are a real simulation
/// (OfflineHunt) plus the Warren, and the map caption that read it advertised a rule nothing backed.
/// The ladder itself is live everywhere: it hardens the region (the host's region-progression step),
/// and pays the Dust milestone (<c>CorruptionScaling.MasteryLevelDust</c>)
/// — one per level, three per region at PERFECTED.
/// </remarks>
public enum MasteryLevel
{
    NewlyConquered = 0,
    PartiallyMastered = 1,
    FullyMastered = 2,
    Perfected = 3,
}
