using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Warrens;

/// <summary>The currencies the Warren's facilities produce - idle pays gear, never identity.</summary>
/// <remarks>
/// Deliberately the game's existing wallets, not new ones: Gleam is the shared gold, Dust is the
/// checkpoint fuel, Scrap and Essence are the Forge's two workhorse materials. The Warren pays NO
/// kind of point - not mastery, not trait; "idle buys gear, play buys identity" (game-flow 3.5) -
/// and every output has a real sink outside this screen. INSIGHT, the closed-loop currency two
/// facilities produced and only facility upgrades spent, was cut 2026-08-31 (P12): it never decided
/// anything the Gleam cost had not already decided, and no other system could ever spend it.
/// </remarks>
public enum WarrenResource { Gleam, Dust, Scrap, Essence }

/// <summary>The eight authored facilities. Fixed set - this is content, not free-form base placement.</summary>
/// <remarks>Member names are SAVE KEYS (<c>SaveGame.WarrenFacilities</c>) - never rename one.</remarks>
public enum FacilityKind
{
    Nursery, Tunnels, ForagingPits, ScavengerRuns,
    BreedingChamber, RitualNest, HoardVaults, SentryBurrows,
}

/// <summary>Static, authored metadata for a facility - never changes at runtime.</summary>
public sealed record FacilityInfo(FacilityKind Kind, string Name, string Description, WarrenResource Produces);

/// <summary>
/// The authored facility catalog, in UNLOCK ORDER (see <see cref="Warren.UnlockedFacilityCount"/>):
/// conquest opens the list top to bottom, so the ramp is authored here, not derived.
/// </summary>
/// <remarks>
/// <b>RATES ARE SET AGAINST THE SINKS.</b> (The original numbers were calibrated against an art
/// mock-up whose currency pill read 131,900,000 - while the game's most expensive single purchase
/// cost 445 Gleam. The 2026-08 rebase fixed Gleam; P12 finishes the job.)
/// <list type="bullet">
///   <item><b>Gleam</b> - the Warren is the IDLE economy: roughly half of what an actively-fought
///     champion earns, so leaving the game running is worth something and playing is worth more.
///     The two Gleam facilities keep the old four-facility total (59/min at L1).</item>
///   <item><b>Dust</b> - rebased ~27x down, the cut the Gleam rates got and these never did (the
///     audit derived ~619/min against 28-Dust upgrades). 21/min at L1 prices a wave-40 checkpoint
///     (1,000 Dust) at about 45 minutes of idling: fuel, not confetti.</item>
///   <item><b>Scrap / Essence</b> - calibrated against the per-wave trickle (a cleared wave pays
///     ONE material unit, <c>WaveSpoils.UnitsPerWave</c>): the L1 Warren roughly doubles an active
///     session's material income rather than dwarfing it. Idle buys gear - through the Forge,
///     never around it.</item>
/// </list>
/// </remarks>
public static class Facilities
{
    public static readonly IReadOnlyList<FacilityInfo> All = new List<FacilityInfo>
    {
        new(FacilityKind.Nursery,        "NURSERY",         "Hatch and raise young. More paws at work, more Gleam.",  WarrenResource.Gleam),
        new(FacilityKind.ForagingPits,   "FORAGING PITS",   "Search the deep soil for Memory Dust.",                  WarrenResource.Dust),
        new(FacilityKind.Tunnels,        "TUNNELS",         "Dig deeper veins. Steady Gleam from the digging.",       WarrenResource.Gleam),
        new(FacilityKind.ScavengerRuns,  "SCAVENGER RUNS",  "Send runners out to bring back Scrap.",                  WarrenResource.Scrap),
        new(FacilityKind.RitualNest,     "RITUAL NEST",     "Listen to the resonance. Slow, deep Essence.",           WarrenResource.Essence),
        new(FacilityKind.SentryBurrows,  "SENTRY BURROWS",  "Patrol the forage trails, shaking loose more Dust.",     WarrenResource.Dust),
        new(FacilityKind.HoardVaults,    "HOARD VAULTS",    "Sort and store the salvage. Steady Scrap.",              WarrenResource.Scrap),
        new(FacilityKind.BreedingChamber,"BREEDING CHAMBER","Breed resonant stock. Their sheddings carry Essence.",   WarrenResource.Essence),
    };

    public static FacilityInfo Info(FacilityKind kind) => All.First(f => f.Kind == kind);
}

/// <summary>What one facility upgrade costs.</summary>
public readonly record struct WarrenCost(int Gleam, int Dust);

/// <summary>The whole units of each currency produced across a span of time.</summary>
public readonly record struct WarrenYield(long Gleam, long Dust, long Scrap, long Essence);

/// <summary>Tunable curves for the Warren economy. Pure data, so balance lives in one place and tests pin it.</summary>
public sealed record WarrenTuning
{
    // Global "+% All Production" from Warren level, plus a per-track bonus (the bonuses strip).
    /// <summary>One track for both material tiers - Scrap and Essence rise together.</summary>

    // Facility upgrade cost - geometric in the facility's current level (cost to go level -> level+1).
    // The upgrade price is only meaningful as a multiple of what the building earns; move these and
    // the base rates together, or the Warren freezes solid / becomes self-funding in a single tick.
    public int GleamCostBase { get; init; } = 180;
    public float GleamCostGrowth { get; init; } = 1.50f;
    public int DustCostBase { get; init; } = 22;
    public float DustCostGrowth { get; init; } = 1.25f;

    /// <summary>Warren XP granted per upgrade = the facility's new level x this.</summary>
    public int XpPerUpgradeLevel { get; init; } = 100;

    /// <summary>Global production bonus granted per conquered region - ties the world map into the idle economy.</summary>

    /// <summary>A facility crosses a MILESTONE every this-many levels - a permanent step-up in its output.</summary>
    public int MilestoneEvery { get; init; } = 5;

    /// <summary>Output multiplier added per milestone crossed (e.g. +15% at L5, +30% at L10, ...).</summary>

    // -- Automation. What a facility does for you as well as what it produces. ---------------------
    //
    // These three used to be trait-tree purchases. Automation is the Warren's job: it already owns the
    // account's idle layer, it already has a currency and an upgrade ladder, and it already persists
    // every facility level, so moving them here needed no new save field at all. The levels sit BELOW
    // the first production milestone (5) on purpose, so the ladder reads as two separate legible
    // rewards: level 2 and level 4 change what the game does for you, level 5 changes how much it makes.

    /// <summary>SCAVENGER RUNS at this level sells Common drops for you when a chest is opened.</summary>
    public int AutoSellCommonLevel { get; init; } = 2;

    /// <summary>SCAVENGER RUNS at this level sells Uncommon drops too. Rare and better are always kept.</summary>
    public int AutoSellUncommonLevel { get; init; } = 4;

    /// <summary>HOARD VAULTS at this level merges your spare items when a chest is opened.</summary>
    public int AutoMergeLevel { get; init; } = 2;

    public static WarrenTuning Default { get; } = new();
}

/// <summary>One facility instance - its kind is fixed, only its level changes.</summary>
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

    // -- Milestones - the long-term goal beyond "+1 level". Every `MilestoneEvery` levels the facility
    //    crosses a milestone that permanently steps up its output. Derived from Level, so nothing extra
    //    is saved. ------------------------------------------------------------------------------------
    /// <summary>How many milestones this facility has crossed (0 below the first).</summary>
    public int MilestoneTier => Level / _t.MilestoneEvery;

    /// <summary>The level at which the next milestone lands.</summary>
    public int NextMilestoneLevel => (MilestoneTier + 1) * _t.MilestoneEvery;

    /// <summary>The permanent output multiplier the crossed milestones grant.</summary>
    public WarrenCost UpgradeCost() => new(
        (int)MathF.Round(_t.GleamCostBase * MathF.Pow(_t.GleamCostGrowth, Level)),
        (int)MathF.Round(_t.DustCostBase * MathF.Pow(_t.DustCostGrowth, Level)));

    internal void LevelUp() => Level++;
    internal void SetLevel(int level) => Level = Math.Max(1, level);
}

/// <summary>
/// The Warren - a global idle base of eight facilities that passively produce Gleam, Memory Dust and
/// the Forge's two workhorse materials.
/// </summary>
/// <remarks>
/// This is the facility economy behind the Warren screen. It owns no rendering and no currency balances:
/// <see cref="Tick"/> returns what was produced and the host credits the real Gleam / Dust / material
/// wallets, so the model stays pure and unit-testable (project standard: no I/O, DI over singletons).
/// Upgrading is instant here - a build-timer layer is a documented follow-up, not part of this version.
/// </remarks>
public sealed class Warren
{
    private readonly WarrenTuning _t;
    private readonly Dictionary<FacilityKind, Facility> _facilities = new();

    // Un-credited production carried across ticks, held as rate x seconds (i.e. "currency-seconds") so the
    // divide-by-60 happens ONCE at the floor step, not once per tick. Multiplying by 1/60 every tick
    // accumulates float drift, breaking the guarantee that many small ticks == one big tick; this doesn't.
    private readonly double[] _carry = new double[4];

    public Warren(WarrenTuning? tuning = null)
    {
        _t = tuning ?? WarrenTuning.Default;
        foreach (var info in Facilities.All)
            _facilities[info.Kind] = new Facility(info.Kind, 1, _t);
    }

    // `Name` ("THE WARREN", "set by the host") lived here until 2026-09-01. Its writer and its only
    // reader — the WARREN screen's caption — both went in UX V2 P2.4, it was never persisted, and no
    // test touched it: a dormant field on a live class is exactly the shape the next bug hides in.

    /// <summary>Regions conquered - set by the host; drives <see cref="ConquestBonus"/> and the unlock ramp.</summary>
    public int ConqueredRegions { get; set; }

    /// <summary>The balance numbers this Warren runs on. Read by <see cref="WarrenAutomation"/>.</summary>
    public WarrenTuning Tuning => _t;

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }

    /// <summary>XP needed to reach the next Warren level. Grows linearly so late levels stay meaningful.</summary>
    public int XpToNext => 1000 * (Level + 2);

    public Facility Facility(FacilityKind kind) => _facilities[kind];
    public IEnumerable<Facility> AllFacilities => Facilities.All.Select(i => _facilities[i.Kind]);

    // -- The conquest unlock ramp -----------------------------------------------------------------
    /// <summary>How many facilities conquest has opened, counted down the catalog.</summary>
    /// <remarks>
    /// game-flow 3.8: "Facilities unlock by conquest... A new player has one facility, not eight."
    /// The Warren itself opens on the first conquest (<c>Unlocks</c>), so the ramp starts at
    /// two-plus-conquests: a fresh Warren shows three cards, every further region taken opens one
    /// more, and all eight are open at the sixth conquest - full current content.
    /// </remarks>
    public int UnlockedFacilityCount => Math.Min(Facilities.All.Count, 2 + ConqueredRegions);

    /// <summary>Is this facility open - by ramp position, or by already having been raised?</summary>
    /// <remarks>
    /// The grandfather rule: a facility past level 1 stays open under any ramp. The ramp shipped
    /// after the facilities did, and a save that invested in a building must never watch its
    /// production vanish because a rule arrived later.
    /// </remarks>
    public bool IsUnlocked(FacilityKind kind)
    {
        if (_facilities[kind].Level > 1) return true;
        for (var i = 0; i < UnlockedFacilityCount; i++)
            if (Facilities.All[i].Kind == kind) return true;
        return false;
    }

    /// <summary>The global "+% All Production" the Warren's level grants.</summary>
    /// <summary>Every facility level bought across the Warren — what the budget's share and the camp's hours grow by.</summary>
    public int FacilityLevelsBought => AllFacilities.Sum(f => Math.Max(0, f.Level - 1));

    /// <summary>The Warren's share of the hunt's expected pay, for the levels bought so far (see <see cref="WarrenBudget"/>).</summary>
    public float Share => WarrenBudget.Share(FacilityLevelsBought);

    /// <summary>What the hunt at this progression is expected to pay an hour — the budget the facilities spend.</summary>
    public float HuntGleamPerHour => WarrenBudget.HuntGleamPerHour(ConqueredRegions);

    /// <summary>
    /// What one facility produces a minute, in its own resource; nothing while it is locked. A FRACTION,
    /// not a whole: Essence is a slow trickle by design and a per-minute integer lost it entirely.
    /// </summary>
    public float OutputPerMinute(FacilityKind kind)
        => IsUnlocked(kind) ? WarrenBudget.PerMinute(kind, ConqueredRegions, FacilityLevelsBought) : 0f;

    /// <summary>What the facility would produce a minute after one more level bought anywhere — the next level's promise.</summary>
    public float NextLevelOutputPerMinute(FacilityKind kind)
        => WarrenBudget.PerMinute(kind, ConqueredRegions, FacilityLevelsBought + 1);

    /// <summary>A resource's whole production a minute, across every unlocked facility that makes it.</summary>
    public float ProductionPerMinute(WarrenResource r)
        => AllFacilities.Where(f => f.Info.Produces == r).Sum(f => OutputPerMinute(f.Kind));

    public WarrenCost UpgradeCost(FacilityKind kind) => _facilities[kind].UpgradeCost();

    // -- The depth cap ----------------------------------------------------------------------------
    /// <summary>Waves of depth each facility level costs. The host derives <see cref="FacilityLevelCap"/> via <see cref="CapForDepth"/>.</summary>
    /// <remarks>
    /// THE WARREN CANNOT OUTRUN THE CHAMPION. Without a cap, an idle player's facilities out-scale the
    /// player who actually descends: the Warren pays in gleam and materials, so a long enough absence
    /// buys gear the descent never earned, and the game's answer to "how do I get stronger" becomes
    /// "close the game". One facility level per five waves of proven depth keeps the idle layer as
    /// what the design calls it - a multiplier on progress, never a substitute for it.
    ///
    /// Lives here rather than in the host because the SCREEN needs it too: a facility that cannot be
    /// upgraded has to say what would unlock it, and "descend deeper" without a number is not an
    /// instruction. With the constant on the model, the screen can name the exact depth instead of
    /// re-deriving the host's arithmetic and drifting from it.
    /// </remarks>
    public const int DepthPerFacilityLevel = 5;

    /// <summary>The facility-level ceiling a given deepest wave earns.</summary>
    /// <remarks>
    /// Floor of 1: a new player must still be able to see what a facility does before their first
    /// descent ends. On the model (with a test) rather than as host arithmetic, so the /5 and the
    /// floor cannot drift from the screen's "REACH DEPTH N" line.
    /// </remarks>
    public static int CapForDepth(int deepestWave) => Math.Max(1, deepestWave / DepthPerFacilityLevel);

    /// <summary>The depth-derived ceiling on every facility's level. int.MaxValue by default so the pure model stays testable without a host.</summary>
    public int FacilityLevelCap { get; set; } = int.MaxValue;

    /// <summary>The depth that would let <paramref name="kind"/> take its next level.</summary>
    /// <remarks>
    /// Reads the facility's OWN level, not the cap. The two are normally the same when a facility is
    /// blocked, but they can part: the cap is derived from the deepest run anywhere, and if that number
    /// ever falls - a region id renamed out from under its recorded BestDepth would do it - a level 18
    /// facility would sit under a cap of 1 and the screen would announce "CAPPED AT LEVEL 1" to a
    /// player looking at LEVEL 18. Answering with the depth the NEXT level needs is both the useful
    /// answer and one that cannot contradict what is on screen beside it.
    /// </remarks>
    public int DepthForNextLevel(FacilityKind kind) =>
        (_facilities[kind].Level + 1) * DepthPerFacilityLevel;

    /// <summary>Is this facility already at the depth-derived ceiling?</summary>
    public bool IsAtLevelCap(FacilityKind kind) => _facilities[kind].Level >= FacilityLevelCap;

    /// <summary>Can the given balances afford this facility's next upgrade?</summary>
    public bool CanAfford(FacilityKind kind, long gleam, long dust)
    {
        var c = UpgradeCost(kind);
        return gleam >= c.Gleam && dust >= c.Dust;
    }

    /// <summary>Open, under the cap AND affordable - what a caller should actually check before spending.</summary>
    public bool CanUpgrade(FacilityKind kind, long gleam, long dust)
        => IsUnlocked(kind) && !IsAtLevelCap(kind) && CanAfford(kind, gleam, dust);

    /// <summary>
    /// Raise a facility one level and grant Warren XP. The CALLER must already have checked affordability
    /// and spent the currencies - this only mutates the Warren, mirroring how Refine/Reforge split cost
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
        var outv = new long[4];
        for (var i = 0; i < 4; i++)
        {
            _carry[i] += ProductionPerMinute((WarrenResource)i) * (double)seconds;   // currency-seconds
            var whole = (long)(_carry[i] / 60.0);
            _carry[i] -= whole * 60.0;
            outv[i] = whole;
        }
        return new WarrenYield(outv[0], outv[1], outv[2], outv[3]);
    }

    // -- Save / restore / fixtures ---------------------------------------------------------------
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
