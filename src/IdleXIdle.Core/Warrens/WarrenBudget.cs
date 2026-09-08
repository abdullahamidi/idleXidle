using System;

namespace IdleXIdle.Core.Warrens;

/// <summary>
/// THE WARREN'S PRODUCTION BUDGET: what the Warren may produce, as a share of what an active hunter
/// at the account's progression is expected to earn.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE WARREN MUST SUPPORT THE HUNTER, NOT OUTRUN THE HUNTER.</b> Before 2026-09-06 every facility
/// had a flat "/min" of its own that grew linearly with its level and again with a milestone, a Warren
/// level and each conquest — and the benchmark measured it at three to five times the hunt's own hourly
/// pay by the second region. The Warren now spends ONE budget: a stable baseline of active pay for the
/// account's progression band, times a SHARE that every facility level bought raises. Facilities hold
/// fixed portions of that budget; a locked facility's portion goes unspent, which is why a conquest's
/// unlock is felt.
/// </para>
/// <para>
/// <b>THE BASELINE IS STABLE ON PURPOSE.</b> It reads the regions conquered — never the build, the
/// loadout, a buff or the last session's measured income — so passive pay cannot swing when the player
/// re-weaves. It is calibrated against the representative profiles of
/// <c>offline_economy_benchmark_test</c>, which holds the Warren under the hard cap against the rate it
/// MEASURES; if the baseline ever drifts above what a hunter really earns, that test says so.
/// </para>
/// <para>
/// <b>Exchange rates</b> put every resource on the same scale as Gleam, from prices the game already
/// charges: a facility level costs 180 Gleam and 22 Dust (8 Gleam a Dust), a Forge level 79 Gleam and
/// 12 Scrap (7 a Scrap), and the trader asks 120 Essence where it asks 350 Scrap (20 a Essence). Dust is
/// Warren-specialised — the Warren is its only faucet — so it carries more weight than the materials;
/// it still buys checkpoint skips and facility levels at a pace measured in hours, not minutes.
/// </para>
/// </remarks>
public static class WarrenBudget
{
    /// <summary>Active Gleam an hour of play pays a fresh account, measured (FRESH profile ≈ 5,978/h).</summary>
    public const float FreshHuntGleamPerHour = 6_000f;

    /// <summary>How much more an hour pays for each region taken — the ladder's pay, measured on hunters geared for it.</summary>
    public const float GrowthPerConquest = 1.22f;

    /// <summary>The baseline stops growing past the last region.</summary>
    public const int BandCap = 6;

    /// <summary>The Warren's share of the baseline with nothing bought, and the ceiling it climbs toward.</summary>
    public const float BaseShare = 0.20f;
    public const float MaxShare = 0.60f;

    /// <summary>
    /// How many facility levels take the share most of the way (63 %) from the base to the ceiling. The
    /// climb is a saturating curve, never a wall: every level bought raises the share, by less each
    /// time, so a deep Warren still feels a level while the law's cap is never crossed. The rest of a
    /// level's worth is the camp's — six minutes of capacity and a little efficiency, every time.
    /// </summary>
    public const float ShareLevels = 30f;

    /// <summary>The law's ceiling: Warren Gleam an hour never exceeds this share of what the hunt really pays.</summary>
    public const float HardCap = 0.70f;

    /// <summary>What an active hunter at this progression is expected to earn in an hour — the budget's base.</summary>
    public static float HuntGleamPerHour(int regionsConquered)
        => FreshHuntGleamPerHour * MathF.Pow(GrowthPerConquest, Math.Clamp(regionsConquered, 0, BandCap));

    /// <summary>The share of the baseline the Warren produces, for the facility levels bought across it.</summary>
    public static float Share(int facilityLevelsBought)
        => BaseShare + (MaxShare - BaseShare) * (1f - MathF.Exp(-Math.Max(0, facilityLevelsBought) / ShareLevels));

    /// <summary>A resource's weight in the budget, against Gleam's 1.</summary>
    public static float Weight(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => 1.0f,
        WarrenResource.Dust => 0.8f,
        WarrenResource.Scrap => 0.35f,
        WarrenResource.Essence => 0.35f,
        _ => throw new ArgumentOutOfRangeException(nameof(r), r, null),
    };

    /// <summary>How many Gleam one unit of the resource stands for, from the prices the game charges.</summary>
    public static float GleamPer(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => 1f,
        WarrenResource.Dust => 8f,
        WarrenResource.Scrap => 7f,
        WarrenResource.Essence => 20f,
        _ => throw new ArgumentOutOfRangeException(nameof(r), r, null),
    };

    /// <summary>A facility's fixed portion of its resource's budget. The two facilities of a resource sum to one.</summary>
    public static float Portion(FacilityKind k) => k switch
    {
        FacilityKind.Nursery => 0.70f,
        FacilityKind.Tunnels => 0.30f,
        FacilityKind.ForagingPits => 0.70f,
        FacilityKind.SentryBurrows => 0.30f,
        FacilityKind.ScavengerRuns => 0.65f,
        FacilityKind.HoardVaults => 0.35f,
        FacilityKind.RitualNest => 0.60f,
        FacilityKind.BreedingChamber => 0.40f,
        _ => throw new ArgumentOutOfRangeException(nameof(k), k, null),
    };

    /// <summary>What a facility produces a minute, in its own resource, at this progression and this many levels bought.</summary>
    public static float PerMinute(FacilityKind kind, int regionsConquered, int facilityLevelsBought)
    {
        var res = Facilities.Info(kind).Produces;
        return HuntGleamPerHour(regionsConquered) / 60f * Share(facilityLevelsBought) * Weight(res) * Portion(kind) / GleamPer(res);
    }
}
