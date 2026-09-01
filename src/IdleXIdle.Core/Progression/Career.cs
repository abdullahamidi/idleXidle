using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Quests;

namespace IdleXIdle.Core.Progression;

/// <summary>
/// THE CAREER: the account's ledger, read as pure functions over the world — what the player has
/// proven, counted where a quest, a tree or the Warren can ask for it without a game running.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <c>Game1</c> (P5, 2026-08-31 — audit critique G2). These rules lived as private
/// methods on a 5,000-line MonoGame class, which made every career fact untestable and every
/// consumer (quests, trait points, the Warren's ceiling) a coupling to the host. They are DERIVED,
/// not banked — recomputed from world state each time — so they can never double-count across a
/// reload; the two latched exceptions (<c>RunsWithVowKept</c>, the account's deepest-ever wave)
/// are events the host persists and passes in.
/// </para>
/// <para>
/// The counters themselves deliberately stay WHERE THEY LIVE (region depths on <see cref="World"/>,
/// chests on the forge, the vow latch in the save): a Career object holding copies would be a
/// second place for the account's state to rot. This class is the arithmetic, not a store.
/// </para>
/// </remarks>
public static class Career
{
    /// <summary>
    /// The world as the quest layer is allowed to see it: flat, pure, rebuilt on demand.
    /// </summary>
    public static QuestProgress QuestSnapshot(World world, int runsWithVowKept, int bossesFelled,
                                              IReadOnlyDictionary<string, int> wavesBySkill)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(wavesBySkill);
        return new QuestProgress(
            DepthByRegion: Regions.All.ToDictionary(d => d.Id, d => world.RegionFarm(d.Id).BestDepth),
            RunsWithVowKept: runsWithVowKept,
            BossesFelled: bossesFelled,
            WavesBySkill: wavesBySkill);
    }

    /// <summary>
    /// Trait points earned: one per conquest, two per corruption tier REACHED (the peak, so
    /// SHALLOWER never costs a point), plus each region's mastery level.
    /// </summary>
    /// <remarks>
    /// Two per tier because one left every road's end unaffordable — with the ladder capped at
    /// five, 6 + 5 + 18 = 29 against a cheapest terminal path of 31. 6 + 10 + 18 = 34 is the
    /// budget the tree was built around (MemoryDustTests: one terminal reachable, two never).
    /// </remarks>
    public static int TraitPointsEarned(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var total = world.ConqueredIds.Count + 2 * world.PeakCorruptionTier;
        foreach (var def in Regions.All) total += (int)world.RegionFarm(def.Id).MasteryLevel;
        return total;
    }

    /// <summary>
    /// The deepest wave held ANYWHERE — the account's one depth number, which the Warren's
    /// facility ceiling is derived from (idle must never outrun the champion).
    /// </summary>
    public static int DeepestAnywhere(World world, int deepestEver)
    {
        ArgumentNullException.ThrowIfNull(world);
        var best = deepestEver;
        foreach (var def in Regions.All) best = Math.Max(best, world.RegionFarm(def.Id).BestDepth);
        return best;
    }

    /// <summary>
    /// Did the descent that just ended run under a Vow whose demand the build actually MET?
    /// </summary>
    /// <remarks>
    /// Not "was a Vow sworn". A Vow pays nothing while its demand is unmet, and a quest that
    /// counted sworn-but-unmet Vows would hand THE OATHBOUND to a player who never engaged with
    /// the system the character exists to reward. Judged the way the simulation judged it while
    /// the run was paying out: the demand against the same <see cref="WeaveContext"/>.
    /// </remarks>
    public static bool VowWasKept(Build build, Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);
        var ctx = SoloBattle.DescribeBuild(build, hunter);
        return build.Skills.Any(s => s.Vow is { } v && Vows.IsActive(v, ctx));
    }
}
