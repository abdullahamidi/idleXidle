namespace IdleXIdle.Core.Encounters;

/// <summary>The starting region's identity.</summary>
/// <remarks>
/// This file once authored the region's encounter templates (creature pools, power tiers, derived
/// par times) for the retired manual-combat spawner. That chain was deleted 2026-08-31 (P13): the
/// live game composes waves from <see cref="BandCycles"/> + <see cref="Archetypes"/> and takes its
/// enemy baseline from the host's RegionLadder step — no template ever reached a fight. Only the id
/// survives, because it is how the codebase names the home region.
/// </remarks>
public static class VerdantHollow
{
    public const string RegionId = "verdant_hollow";
}
