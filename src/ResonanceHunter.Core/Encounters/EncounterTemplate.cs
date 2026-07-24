using System.Collections.Generic;

namespace ResonanceHunter.Core.Encounters;

/// <summary>One creature species/variant that a template may spawn.</summary>
public sealed record CreatureTemplate
{
    public required string Id { get; init; }
    public required int BaseHealth { get; init; }

    /// <summary>Weighted share of the pool draw. Must be positive.</summary>
    public float Weight { get; init; } = 1f;
}

/// <summary>
/// An authored encounter: "the thing you can fight here". Owns the authored <c>power_tier</c> and the
/// fixed <c>par_clear_time_seconds</c> anchor.
/// </summary>
/// <remarks>
/// This type exists because <c>power_tier</c> had consumers (creature health, loot rarity, and once the
/// retired combat's windup/incoming-damage) and <b>no producer</b> — every design document deferred the
/// question upstream and the buck never stopped (Blocker B2).
/// </remarks>
public sealed record EncounterTemplate
{
    public required string TemplateId { get; init; }
    public required string RegionId { get; init; }
    public bool IsBoss { get; init; }

    public required IReadOnlyList<CreatureTemplate> CreaturePool { get; init; }

    /// <summary>The template's authored anchor difficulty. Par is derived at THIS tier, never a live roll.</summary>
    public required int PowerTierBase { get; init; }

    /// <summary>Relative share of the region's standard draw. Bosses are excluded from the draw entirely.</summary>
    public float SelectionWeight { get; init; } = 1f;

    /// <summary>
    /// THE ANCHOR of the entire active/idle contract: the clear time of a competent, fully-mastered
    /// automated team on this encounter. Hand-authored, fixed, and <b>never</b> recomputed from live
    /// mastery. Cached here rather than derived per spawn.
    /// </summary>
    /// <remarks>
    /// If this is ever made to vary with the player's mastery, team, or automation stage, Blocker B1
    /// has regressed and Pillar 3 inverts. See EfficiencyContract.
    /// </remarks>
    public required int ParClearTimeSeconds { get; init; }
}

/// <summary>Tuning for par-time derivation. Data-driven, never inline literals.</summary>
public sealed record SpawnTuning
{
    // ── par derivation (Formulas 3 and 4) ────────────────────────────────────────────────
    public float ReferenceTeamBaseDps { get; init; } = 6.0f;
    public float ReferenceTeamDpsFalloffScalar { get; init; } = 0.05f;
    public float ReferenceTeamDpsFloor { get; init; } = 1.0f;
    public float BossOverheadMultiplier { get; init; } = 1.15f;
    public float ParClearTimeOverheadSeconds { get; init; } = 2.0f;

    /// <summary>Shared with creature-data-schema — one scalar drives the tier-health curve.</summary>
    public float TierHealthScalar { get; init; } = 0.15f;

    public static SpawnTuning Default { get; } = new();
}
