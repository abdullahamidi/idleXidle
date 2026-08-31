using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Combat;

namespace IdleXIdle.Core.Encounters;

/// <summary>An authored region: its identity, its content, and what conquering it unlocks.</summary>
public sealed record RegionDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Source Theme { get; init; }

    /// <summary>The region that must be conquered before this one unlocks. Null = available from the start.</summary>
    public string? PrereqId { get; init; }

    /// <summary>Which attack tempo this region's creatures favour — how it feels to fight, not just look at.</summary>
    public AttackBias CombatBias { get; init; } = AttackBias.Balanced;
}

/// <summary>
/// The world: the ordered chain of regions to conquer.
/// </summary>
/// <remarks>
/// Regions are content, not code — each is a theme, a combat bias, a band cycle and a drop profile
/// over the same wave machinery. The chain gives the game its progression spine: hold the conquest
/// wave to take a region and unlock the next. (Enemy art keys live in Game1.EnemyArtFor.)
/// </remarks>
public static class Regions
{
    /// <summary>
    /// The power rating a region asks of the champion — the map's RECOMMENDED POWER, on the same ruler
    /// as <c>Hunter.PowerRating</c>.
    /// </summary>
    /// <remarks>
    /// Re-anchored 2026-08-23 with the rating itself (PowerScaleTests): a bare hunter reads ~170, a full
    /// Legendary set ~2,200. The old <c>PowerTierBase × 350</c> (1,400 … 7,000) was the linear rating's
    /// scale and would now sit above anything wearable. A flat floor near bare plus 60 per boss tier
    /// lands the first region a little above a fresh champion and the last below a full Legendary set.
    /// </remarks>
    public static int RecommendedPower(RegionDefinition region)
    {
        ArgumentNullException.ThrowIfNull(region);
        // Anchored to the world's own ladder (2026-08-23 audit): enemies grow by HealthStep per region,
        // and the rating is square-root-shaped (PowerRating = 100·sqrt(offence) + 100·sqrt(toughness)),
        // so the rating NEEDED grows by sqrt(HealthStep) ≈ 1.27 per region — a uniform step. The old
        // flat 170 + tier×60 flattened exactly where the world does not (+59%, +55%, then +18/+10/+5%).
        // The base keeps the old endpoints: region 0 still reads 410, the last still ~1,370.
        var index = -1;
        for (var i = 0; i < All.Count; i++)
            if (All[i].Id == region.Id) { index = i; break; }
        if (index < 0) return 410;   // an off-catalogue region reads as the first rung
        return (int)MathF.Round(410f * MathF.Pow(MathF.Sqrt(RegionLadder.HealthStep), index));
    }

    public static IReadOnlyList<RegionDefinition> All { get; } = new List<RegionDefinition>
    {
        new()
        {
            // The starting region stays Balanced — the player learns the baseline before it's bent.
            Id = VerdantHollow.RegionId, Name = "VERDANT HOLLOW", Theme = Source.Nature,
            PrereqId = null, CombatBias = AttackBias.Balanced,
        },
        // Cinderworks grinds you down with slow, heavy industrial blows.
        BuildRegion("cinderworks", "CINDERWORKS", Source.Machine, prereq: VerdantHollow.RegionId,
            bias: AttackBias.Heavy),
        // Umbral Reach harries you with fast, creeping strikes.
        BuildRegion("umbral_reach", "UMBRAL REACH", Source.Shadow, prereq: "cinderworks",
            bias: AttackBias.Fast),
        // The back half of the world — one region per remaining Source, so all six get a home and the
        // Source matchup has somewhere to land every element. (An older comment here promised these were
        // "shorter to conquer"; they never were — the conquest bar is one constant for the whole world.
        // What actually separates them is RegionLadder, which prices each one at a fixed multiple of the
        // last, so the back half asks for gear rather than for more waves.) So
        // the journey is SPREAD across more, sharper places rather than three long grinds.
        // Marrow Wastes: BODY. Brutal, heavy blows — a slaughterhouse that grinds you down.
        BuildRegion("marrow_wastes", "MARROW WASTES", Source.Body, prereq: "umbral_reach",
            bias: AttackBias.Heavy),
        // The Still Archive: MIND. Fast, precise psychic lances — death by a thousand cuts.
        BuildRegion("still_archive", "THE STILL ARCHIVE", Source.Mind, prereq: "marrow_wastes",
            bias: AttackBias.Fast),
        // The Pale Choir: SPIRIT. The deepest reach — balanced, relentless, the end of the known world.
        BuildRegion("pale_choir", "THE PALE CHOIR", Source.Spirit, prereq: "still_archive",
            bias: AttackBias.Balanced),
    };

    public static RegionDefinition Get(string id) => All.First(r => r.Id == id);

    /// <summary>Look up a region, or null if the id isn't in the catalog — the crash-safe form for load paths.</summary>
    public static RegionDefinition? Find(string id) => All.FirstOrDefault(r => r.Id == id);
    public static RegionDefinition? Next(string id)
    {
        var idx = All.ToList().FindIndex(r => r.Id == id);
        return idx >= 0 && idx + 1 < All.Count ? All[idx + 1] : null;
    }

    private static RegionDefinition BuildRegion(
        string id, string name, Source theme, string prereq, AttackBias bias = AttackBias.Balanced)
        => new() { Id = id, Name = name, Theme = theme, PrereqId = prereq, CombatBias = bias };
}

/// <summary>
/// The player's progress through the world: which regions are conquered, and a farm per region.
/// </summary>
/// <remarks>
/// A conquered region can be farmed independently, so each holds its own <see cref="Region"/>
/// (mastery, team, automation stage). The starting region is available immediately; every other
/// region unlocks when its prerequisite is conquered.
/// </remarks>
public sealed class World
{
    private readonly Dictionary<string, Region> _regions = new();
    private readonly HashSet<string> _conquered = new();

    public World(AutomationTuning? tuning = null)
    {
        foreach (var def in Regions.All)
            _regions[def.Id] = new Region(def.Id, tuning);
    }

    public Region RegionFarm(string id) => _regions[id];

    public bool IsConquered(string id) => _conquered.Contains(id);

    /// <summary>A region is unlocked if it is the start, is already conquered, or its prereq is conquered.</summary>
    public bool IsUnlocked(string id)
    {
        // An unknown/renamed id (e.g. a save from an older build after the region list changed) is simply
        // "not unlocked" rather than a crash — the load path then falls back to the home region.
        if (Regions.Find(id) is not { } def) return false;
        return def.PrereqId is null || _conquered.Contains(id) || _conquered.Contains(def.PrereqId);
    }

    /// <summary>Mark a region conquered (its boss was beaten). Returns the region it unlocks, if any.</summary>
    public RegionDefinition? Conquer(string id)
    {
        _conquered.Add(id);
        var next = Regions.Next(id);
        return next is not null && !_conquered.Contains(next.Id) ? next : null;
    }

    public IReadOnlyCollection<string> ConqueredIds => _conquered;
    public void RestoreConquered(IEnumerable<string> ids)
    {
        _conquered.Clear();
        foreach (var id in ids) if (_regions.ContainsKey(id)) _conquered.Add(id);
    }

    // ── Corruption: the endgame ratchet ─────────────────────────────────────────────────────────

    /// <summary>How deep into the corruption the world has been pushed. 0 = the base world.</summary>
    public int CorruptionTier { get; private set; }

    /// <summary>
    /// The deepest tier the world has EVER been pushed to. The deepening award and the trait points a
    /// career earns key off this, not the live tier — otherwise SHALLOWER then DEEPER would mint the
    /// award again every cycle, and easing would quietly take a trait point away.
    /// </summary>
    public int PeakCorruptionTier { get; private set; }

    /// <summary>True once every region has been conquered — the precondition for deepening.</summary>
    public bool AllConquered => Regions.All.All(r => _conquered.Contains(r.Id));

    /// <summary>You may only deepen the corruption once the whole world is yours — and only to the top of the ladder.</summary>
    public bool CanDeepenCorruption => AllConquered && CorruptionTier < CorruptionScaling.MaxTier;

    /// <summary>The corruption can always be eased back toward the base world.</summary>
    public bool CanEaseCorruption => CorruptionTier > 0;

    /// <summary>
    /// Push the world one corruption tier deeper. Nothing resets — this is a pure difficulty/reward
    /// ratchet with a top (<see cref="CorruptionScaling.MaxTier"/>). Returns the new tier, or the
    /// unchanged tier if the world isn't fully conquered yet or is already at the floor of the world.
    /// </summary>
    public int DeepenCorruption()
    {
        if (CanDeepenCorruption) CorruptionTier++;
        PeakCorruptionTier = Math.Max(PeakCorruptionTier, CorruptionTier);
        return CorruptionTier;
    }

    /// <summary>True when the last <see cref="DeepenCorruption"/> reached a tier never reached before.</summary>
    public bool IsNewPeak => CorruptionTier == PeakCorruptionTier;

    /// <summary>
    /// Pull the world one corruption tier back. Playtest 2026-08-23: "Deepen butonu var, bunu
    /// azaltamıyorum" — a difficulty you can only raise is a trap, not a choice.
    /// </summary>
    public int EaseCorruption()
    {
        if (CanEaseCorruption) CorruptionTier--;
        return CorruptionTier;
    }

    /// <summary>Restore the corruption tier (and the peak it has reached) from a save, clamped into the
    /// ladder — old saves could be past the top, and older ones carry no peak (then the tier is the peak).</summary>
    public void RestoreCorruption(int tier, int peak = -1)
    {
        CorruptionTier = CorruptionScaling.Clamp(tier);
        PeakCorruptionTier = Math.Max(CorruptionTier, CorruptionScaling.Clamp(peak));
    }
}
