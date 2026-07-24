using System;

namespace ResonanceHunter.Core.Encounters;

/// <summary>
/// The reference-fight math the region content uses to stamp each encounter's par clear time.
/// </summary>
/// <remarks>
/// This was once a full spawner — it rolled encounters (with tier variance and weighted selection) for the
/// manual-combat model, serving both the player's hunt and an automated tick from one path. That whole
/// machinery is gone: the champion auto-fights waves now, nothing rolls encounters this way. What survives
/// are the pure, authoring-time formulas — <see cref="DerivePar"/> and its two inputs — which
/// <c>Regions</c>/<c>VerdantHollow</c> still call to compute <c>ParClearTimeSeconds</c> for each template.
/// </remarks>
public static class EncounterSpawner
{
    /// <summary>creature-data-schema Formula 1 — reused by name, never redefined.</summary>
    public static int EffectiveMaxHealth(int baseHealth, int powerTier, SpawnTuning tuning)
        => Math.Max(1, (int)Math.Round(
            baseHealth * (1f + tuning.TierHealthScalar * (powerTier - 1)),
            MidpointRounding.AwayFromZero));

    /// <summary>Formula 3 — the reference team's effective DPS at a given tier.</summary>
    public static float ReferenceTeamEffectiveDps(int powerTier, SpawnTuning tuning)
        => Math.Max(
            tuning.ReferenceTeamDpsFloor,
            tuning.ReferenceTeamBaseDps * (1f - tuning.ReferenceTeamDpsFalloffScalar * (powerTier - 1)));

    /// <summary>
    /// Formula 4 — derive <c>par_clear_time_seconds</c> for a template.
    /// </summary>
    /// <remarks>
    /// An AUTHORING-TIME helper. Par is evaluated once, at the template's fixed <c>PowerTierBase</c> — never
    /// at a live rolled tier, and never against automation's actual throughput. The anchor cannot move.
    /// </remarks>
    public static int DerivePar(
        int hardestBaseHealth, int powerTierBase, bool isBoss, SpawnTuning tuning)
    {
        var health = EffectiveMaxHealth(hardestBaseHealth, powerTierBase, tuning);
        var dps = ReferenceTeamEffectiveDps(powerTierBase, tuning);
        var overhead = isBoss ? tuning.BossOverheadMultiplier : 1f;

        return (int)Math.Round(
            health / dps * overhead + tuning.ParClearTimeOverheadSeconds,
            MidpointRounding.AwayFromZero);
    }
}
