using System;
using System.Collections.Generic;
using System.Linq;

namespace ResonanceHunter.Core.Warrens;

/// <summary>The three real currencies the Warren's facilities produce and consume.</summary>
/// <remarks>
/// Deliberately the game's existing currencies, not new ones: Gleam is the shared gold, Mastery is the
/// build-tree currency, Dust is the prestige currency. The Warren is therefore a real cross-system idle
/// loop — it funds progression — rather than a closed loop that produces resources only to upgrade itself.
/// </remarks>
public enum WarrenResource { Gleam, Mastery, Dust }

/// <summary>The eight authored facilities. Fixed set — this is content, not free-form base placement.</summary>
public enum FacilityKind
{
    Nursery, Tunnels, ForagingPits, ScavengerRuns,
    BreedingChamber, RitualNest, HoardVaults, SentryBurrows,
}

/// <summary>Static, authored metadata for a facility — never changes at runtime.</summary>
public sealed record FacilityInfo(FacilityKind Kind, string Name, string Description, WarrenResource Produces, float BaseRatePerMin);

/// <summary>The authored facility catalog. Base rates are chosen so a mid-Warren reads on the reference's scale.</summary>
public static class Facilities
{
    public static readonly IReadOnlyList<FacilityInfo> All = new List<FacilityInfo>
    {
        new(FacilityKind.Nursery,        "NURSERY",         "Hatch and nurture young. Increases gold production.",        WarrenResource.Gleam,   520f),
        new(FacilityKind.Tunnels,        "TUNNELS",         "Dig deeper veins. Steady gold from the warren's diggings.", WarrenResource.Gleam,   480f),
        new(FacilityKind.ForagingPits,   "FORAGING PITS",   "Forage the deep loam for memory-rich spores.",              WarrenResource.Dust,    450f),
        new(FacilityKind.ScavengerRuns,  "SCAVENGER RUNS",  "Send runners abroad to bring back gold.",                   WarrenResource.Gleam,   410f),
        new(FacilityKind.BreedingChamber,"BREEDING CHAMBER","Breed keener minds. Yields mastery insight.",               WarrenResource.Mastery, 122f),
        new(FacilityKind.RitualNest,     "RITUAL NEST",     "Commune with the resonance. Slow, deep mastery.",           WarrenResource.Mastery, 118f),
        new(FacilityKind.HoardVaults,    "HOARD VAULTS",    "Store and compound the warren's gold.",                     WarrenResource.Gleam,   370f),
        new(FacilityKind.SentryBurrows,  "SENTRY BURROWS",  "Guard the forage lines, protecting dust yield.",            WarrenResource.Dust,    108f),
    };

    public static FacilityInfo Info(FacilityKind kind) => All.First(f => f.Kind == kind);
}

/// <summary>What one facility upgrade costs across the three currencies.</summary>
public readonly record struct WarrenCost(int Gleam, int Mastery, int Dust);

/// <summary>The whole units of each currency produced across a span of time.</summary>
public readonly record struct WarrenYield(long Gleam, long Mastery, long Dust)
{
    public bool Any => Gleam > 0 || Mastery > 0 || Dust > 0;
}

/// <summary>Tunable curves for the Warren economy. Pure data, so balance lives in one place and tests pin it.</summary>
public sealed record WarrenTuning
{
    // Global "+% All Production" from Warren level, plus a per-currency track (the reference's bonuses strip).
    public float AllProductionPerLevel { get; init; } = 0.03f;
    public float GleamBonusPerLevel { get; init; } = 0.02f;
    public float MasteryBonusPerLevel { get; init; } = 0.013f;
    public float DustBonusPerLevel { get; init; } = 0.009f;

    // Facility upgrade cost — geometric in the facility's current level (cost to go level -> level+1).
    // Bases/growths are tuned so an ~L18 facility costs on the reference's scale (~8M gold, ~2.4K mastery).
    public int GleamCostBase { get; init; } = 18_700;
    public float GleamCostGrowth { get; init; } = 1.40f;
    public int MasteryCostBase { get; init; } = 43;
    public float MasteryCostGrowth { get; init; } = 1.25f;
    public int DustCostBase { get; init; } = 22;
    public float DustCostGrowth { get; init; } = 1.25f;

    /// <summary>Warren XP granted per upgrade = the facility's new level × this.</summary>
    public int XpPerUpgradeLevel { get; init; } = 100;

    public static WarrenTuning Default { get; } = new();
}

/// <summary>One facility instance — its kind is fixed, only its level changes.</summary>
public sealed class Facility
{
    private readonly WarrenTuning _t;

    public Facility(FacilityKind kind, int level, WarrenTuning? tuning = null)
    {
        Kind = kind;
        Level = Math.Max(1, level);
        _t = tuning ?? WarrenTuning.Default;
    }

    public FacilityKind Kind { get; }
    public int Level { get; private set; }
    public FacilityInfo Info => Facilities.Info(Kind);

    /// <summary>Raw per-minute output at this level, BEFORE Warren bonuses — the number shown on the card.</summary>
    public int BaseOutputPerMin => (int)MathF.Round(Info.BaseRatePerMin * Level);

    /// <summary>The cost to raise this facility one level (grows with its current level).</summary>
    public WarrenCost UpgradeCost() => new(
        (int)MathF.Round(_t.GleamCostBase * MathF.Pow(_t.GleamCostGrowth, Level)),
        (int)MathF.Round(_t.MasteryCostBase * MathF.Pow(_t.MasteryCostGrowth, Level)),
        (int)MathF.Round(_t.DustCostBase * MathF.Pow(_t.DustCostGrowth, Level)));

    internal void LevelUp() => Level++;
    internal void SetLevel(int level) => Level = Math.Max(1, level);
}

/// <summary>
/// The Warren — a global idle base of eight facilities that passively produce the game's real currencies.
/// </summary>
/// <remarks>
/// This is the facility economy behind the Warren screen. It owns no rendering and no currency balances:
/// <see cref="Tick"/> returns what was produced and the host credits the real Gleam / Mastery / Dust, so
/// the model stays pure and unit-testable (project standard: no I/O, DI over singletons). Upgrading is
/// instant here — a build-timer layer is a documented follow-up, not part of this version.
/// </remarks>
public sealed class Warren
{
    private readonly WarrenTuning _t;
    private readonly Dictionary<FacilityKind, Facility> _facilities = new();

    // Un-credited production carried across ticks, held as rate×seconds (i.e. "currency-seconds") so the
    // divide-by-60 happens ONCE at the floor step, not once per tick. Multiplying by 1/60 every tick
    // accumulates float drift, breaking the guarantee that many small ticks == one big tick; this doesn't.
    private readonly double[] _carry = new double[3];

    public Warren(WarrenTuning? tuning = null)
    {
        _t = tuning ?? WarrenTuning.Default;
        foreach (var info in Facilities.All)
            _facilities[info.Kind] = new Facility(info.Kind, 1, _t);
    }

    /// <summary>The Warren's display name — the deepest conquered region's, set by the host.</summary>
    public string Name { get; set; } = "THE WARREN";

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }

    /// <summary>XP needed to reach the next Warren level. Grows linearly so late levels stay meaningful.</summary>
    public int XpToNext => 1000 * (Level + 2);

    public Facility Facility(FacilityKind kind) => _facilities[kind];
    public IEnumerable<Facility> AllFacilities => Facilities.All.Select(i => _facilities[i.Kind]);

    /// <summary>The global "+% All Production" the Warren's level grants.</summary>
    public float AllProductionBonus => (Level - 1) * _t.AllProductionPerLevel;

    /// <summary>A per-currency production bonus (the Warren-bonuses strip), on top of All Production.</summary>
    public float ResourceBonus(WarrenResource r) => r switch
    {
        WarrenResource.Gleam => Level * _t.GleamBonusPerLevel,
        WarrenResource.Mastery => Level * _t.MasteryBonusPerLevel,
        _ => Level * _t.DustBonusPerLevel,
    };

    /// <summary>The total production multiplier applied to a resource's raw facility output.</summary>
    public float Multiplier(WarrenResource r) => 1f + AllProductionBonus + ResourceBonus(r);

    /// <summary>Total production per minute for a resource, Warren bonuses applied — the overview readout.</summary>
    public long ProductionPerMinute(WarrenResource r)
    {
        var raw = AllFacilities.Where(f => f.Info.Produces == r).Sum(f => (long)f.BaseOutputPerMin);
        return (long)MathF.Round(raw * Multiplier(r));
    }

    public WarrenCost UpgradeCost(FacilityKind kind) => _facilities[kind].UpgradeCost();

    /// <summary>Can the given balances afford this facility's next upgrade?</summary>
    public bool CanAfford(FacilityKind kind, long gleam, long mastery, long dust)
    {
        var c = UpgradeCost(kind);
        return gleam >= c.Gleam && mastery >= c.Mastery && dust >= c.Dust;
    }

    /// <summary>
    /// Raise a facility one level and grant Warren XP. The CALLER must already have checked affordability
    /// and spent the currencies — this only mutates the Warren, mirroring how Refine/Reforge split cost
    /// (the pure model) from the spend (the host).
    /// </summary>
    public void Upgrade(FacilityKind kind)
    {
        var f = _facilities[kind];
        f.LevelUp();
        AddXp(f.Level * _t.XpPerUpgradeLevel);
    }

    private void AddXp(int amount)
    {
        Xp += Math.Max(0, amount);
        while (Xp >= XpToNext) { Xp -= XpToNext; Level++; }
    }

    /// <summary>
    /// Advance production by <paramref name="seconds"/> and return the whole units produced. Fractions carry
    /// across calls, so ticking every second yields exactly what one big offline catch-up tick would.
    /// </summary>
    public WarrenYield Tick(float seconds)
    {
        if (seconds <= 0f) return default;
        var outv = new long[3];
        for (var i = 0; i < 3; i++)
        {
            _carry[i] += ProductionPerMinute((WarrenResource)i) * (double)seconds;   // currency-seconds
            var whole = (long)(_carry[i] / 60.0);
            _carry[i] -= whole * 60.0;
            outv[i] = whole;
        }
        return new WarrenYield(outv[0], outv[1], outv[2]);
    }

    // ── Save / restore / fixtures ───────────────────────────────────────────────────────────────
    public IReadOnlyDictionary<FacilityKind, int> FacilityLevels =>
        Facilities.All.ToDictionary(i => i.Kind, i => _facilities[i.Kind].Level);

    /// <summary>Restore Warren state from a save. Unknown facility keys are ignored (crash-safe load).</summary>
    public void Restore(int level, int xp, IReadOnlyDictionary<FacilityKind, int>? facilityLevels)
    {
        Level = Math.Max(1, level);
        Xp = Math.Max(0, xp);
        if (facilityLevels is null) return;
        foreach (var (kind, lvl) in facilityLevels)
            if (_facilities.TryGetValue(kind, out var f)) f.SetLevel(lvl);
    }
}
