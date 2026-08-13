using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Progression;

namespace ResonanceHunter.Core.Automation;

public sealed record AutomationTuning
{
    public float RmpPerHealthyMemberTick { get; init; } = 1f;
    public float RmpPerActiveKillBonus { get; init; } = 5f;

    public float RmpThreshold1 { get; init; } = 500f;    // Partially Mastered
    public float RmpThreshold2 { get; init; } = 2000f;   // Fully Mastered
    public float RmpThreshold3 { get; init; } = 5000f;   // Optimized (also REQUIRES a complete team)

    public int PowerTierReferenceCeiling { get; init; } = 20;

    /// <summary>Deliberately the larger weight — composition dominates raw power.</summary>
    public float CcsWeight { get; init; } = 0.65f;
    public float PqsWeight { get; init; } = 0.35f;

    public int MaxTeamSize { get; init; } = 6;

    /// <summary>
    /// The hard cap on Gleam realized per hour by automated selling.
    /// </summary>
    /// <remarks>
    /// This defuses a genuine inflation bomb. Without it, the loot faucet projected roughly 760,000
    /// Gleam from a single 100-hour unattended session — enough to buy essentially the entire game.
    /// With it, that same session realizes 2,400.
    /// </remarks>
    public int AutoSellGleamCapPerHour { get; init; } = 24;

    public static AutomationTuning Default { get; } = new();
}

/// <summary>What one automation tick produced. Returned rather than applied, so it can be shown.</summary>
public sealed record AutomationYield
{
    public int Kills { get; init; }
    public int GleamRealized { get; init; }
    public int GleamForfeitedToCap { get; init; }
    public int CoresProduced { get; init; }
    public float RmpGained { get; init; }
}

/// <summary>
/// A region you have conquered and can farm.
///
/// This is the game's thesis in one class: <b>automation is earned, not assumed</b>, and once earned
/// it is genuinely good — but active play always beats it.
/// </summary>
public sealed class Region
{
    private readonly AutomationTuning _tuning;
    private readonly List<Creature> _team = new();

    /// <summary>
    /// Leftover fractions of a work tick, per creature id.
    /// </summary>
    /// <remarks>
    /// The farm ticks once a second but a work tick takes many seconds, so without carrying the
    /// remainder every tick would floor to zero and no creature would ever record a single one — the
    /// job-diligence evolution branch would stay just as dead as it was, in a subtler way.
    /// </remarks>
    private readonly Dictionary<string, float> _tickFraction = new();

    /// <summary>Carries fractional Gleam across ticks so the hourly cap is exact, not rounded away.</summary>
    private float _gleamCreditThisHour;
    private float _secondsIntoHour;

    /// <summary>
    /// Carries fractional kill progress across ticks.
    /// </summary>
    /// <remarks>
    /// <b>Load-bearing, and its absence was a real bug.</b> Kills were computed as
    /// <c>(int)(seconds / clearTime)</c>, which truncates. The game ticks automation once per second,
    /// and a clear takes tens of seconds — so every tick computed <c>(int)0.025 == 0</c> and the live
    /// farm produced <b>nothing, ever</b>. It only appeared to work when tested with one big offline
    /// catch-up tick. Progress must accumulate, or a farm that is watched behaves differently from one
    /// that is not.
    /// </remarks>
    private float _killProgress;

    public Region(string regionId, int parClearTimeSeconds, AutomationTuning? tuning = null)
    {
        RegionId = regionId;
        ParClearTimeSeconds = parClearTimeSeconds;
        _tuning = tuning ?? AutomationTuning.Default;
    }

    public string RegionId { get; }

    /// <summary>The fixed anchor. Automation's throughput is DERIVED from this — never the reverse.</summary>
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
    public IReadOnlyList<Creature> Team => _team;

    /// <summary>1 = auto-attack, 2 = auto-loot, 3 = auto-sell. Earned, not given.</summary>
    public int AutomationStage { get; set; } = 1;

    public bool Assign(Creature creature)
    {
        if (_team.Count >= _tuning.MaxTeamSize) return false;
        if (_team.Any(c => c.Id == creature.Id)) return false;

        _team.Add(creature);
        return true;
    }

    public bool Unassign(string creatureId) => _team.RemoveAll(c => c.Id == creatureId) > 0;

    /// <summary>
    /// CCS — Composition Completeness. The four chain roles: a generator (Attacker OR Producer),
    /// a Crafter, a Defender, and a Support.
    /// </summary>
    public float CompositionCompleteness()
    {
        var healthy = _team.Where(c => c.IsHealthy).ToList();

        var hasGenerator = healthy.Any(c => c.Role is Role.Attacker or Role.Producer);
        var hasCrafter = healthy.Any(c => c.Role == Role.Crafter);
        var hasDefender = healthy.Any(c => c.Role == Role.Defender);
        var hasSupport = healthy.Any(c => c.Role == Role.Support);

        var filled = (hasGenerator ? 1 : 0) + (hasCrafter ? 1 : 0) + (hasDefender ? 1 : 0) + (hasSupport ? 1 : 0);
        return filled / 4f;
    }

    /// <summary>PQS — the team's average power, normalized against the tier ceiling.</summary>
    public float PowerQuality()
    {
        var healthy = _team.Where(c => c.IsHealthy).ToList();
        if (healthy.Count == 0) return 0f;

        var mean = (float)healthy.Average(c => c.PowerTier);
        return Math.Clamp(mean / _tuning.PowerTierReferenceCeiling, 0f, 1f);
    }

    /// <summary>
    /// Formula 3 — Team Quality. <b>Composition beats power-stacking, and it is not close.</b>
    /// </summary>
    /// <remarks>
    /// Six Producers of the same average power score 0.25. A complete four-role team at the same
    /// average power scores 0.7375 — nearly 3x — because CCS carries the larger weight. Stacking one
    /// strong role can never substitute for filling the chain. That is the composition puzzle the
    /// design promises, made arithmetic rather than asserted.
    /// </remarks>
    public float TeamQualityScore()
        => Math.Clamp(_tuning.CcsWeight * CompositionCompleteness() + _tuning.PqsWeight * PowerQuality(), 0f, 1f);

    /// <summary>
    /// Formula 2 — Mastery Level.
    /// </summary>
    /// <remarks>
    /// Optimized is gated on BOTH points and a complete team. Grinding RMP alone can never reach it —
    /// you must actually solve the composition. A team missing its Defender stalls at Fully Mastered
    /// forever, however long it farms.
    /// </remarks>
    public MasteryLevel MasteryLevel
    {
        get
        {
            if (RegionMasteryPoints >= _tuning.RmpThreshold3 && CompositionCompleteness() >= 1f)
                return Progression.MasteryLevel.OptimizedTeam;
            if (RegionMasteryPoints >= _tuning.RmpThreshold2) return Progression.MasteryLevel.FullyMastered;
            if (RegionMasteryPoints >= _tuning.RmpThreshold1) return Progression.MasteryLevel.PartiallyMastered;
            return Progression.MasteryLevel.NewlyConquered;
        }
    }

    /// <summary>Percent of par. The SAME anchor active play is scored against — so they are comparable.</summary>
    public float IdleEfficiencyPercent()
        => EfficiencyContract.IdleEfficiencyPercent(MasteryLevel, TeamQualityScore());

    /// <summary>Derived, never an anchor. This is a readout, not a reference.</summary>
    public float AutomationClearTimeSeconds()
        => EfficiencyContract.AutomationClearTimeSeconds(ParClearTimeSeconds, IdleEfficiencyPercent());

    /// <summary>RMP for an active kill the player fought here — active play still builds mastery.</summary>
    public void RecordActiveKill() => RegionMasteryPoints += _tuning.RmpPerActiveKillBonus;

    /// <summary>Progress to the NEXT mastery level, 0..1. Null at the top.</summary>
    public float? ProgressToNextLevel()
    {
        var (from, to) = MasteryLevel switch
        {
            Progression.MasteryLevel.NewlyConquered => (0f, _tuning.RmpThreshold1),
            Progression.MasteryLevel.PartiallyMastered => (_tuning.RmpThreshold1, _tuning.RmpThreshold2),
            Progression.MasteryLevel.FullyMastered => (_tuning.RmpThreshold2, _tuning.RmpThreshold3),
            _ => (0f, 0f),
        };

        if (to <= 0f) return null;
        return Math.Clamp((RegionMasteryPoints - from) / (to - from), 0f, 1f);
    }

    /// <summary>
    /// Advance the farm by <paramref name="seconds"/>. Used for both live ticking and offline catch-up.
    /// </summary>
    /// <remarks>
    /// <b>Without a healthy GENERATOR (an Attacker or a Producer), an automated region produces nothing.</b>
    /// Other roles build mastery, but nothing dies, so nothing drops. Automation is a team, not a switch.
    /// </remarks>
    /// <param name="masteryRate">
    /// Region-mastery multiplier from Memory Dust (SHARPENED RECALL). 1.0 is unmodified. It is a
    /// PARAMETER rather than something the caller applies afterwards because RMP is accrued into
    /// RegionMasteryPoints in here — a caller scaling the returned yield would report a boosted number
    /// while the actual mastery ticked up at the base rate.
    /// </param>
    public AutomationYield Tick(float seconds, int gleamPerKill, float masteryRate = 1f)
    {
        if (seconds <= 0f) return new AutomationYield();

        var healthy = _team.Where(c => c.IsHealthy).ToList();

        // The auto-sell allowance accrues with ELAPSED TIME, unconditionally — before any kill check.
        // It must not depend on whether a kill happened to land this tick, or the cap would be a
        // function of kill cadence rather than of time, and a farm ticked every second would earn a
        // fraction of what the same farm ticked once per hour earns.
        AccrueGleamAllowance(seconds);

        // Formula 1 — RMP accrues from every healthy member's work ticks, Attacker or not.
        var workTicks = healthy.Sum(c => seconds / c.WorkTickIntervalSeconds);
        var rmp = workTicks * _tuning.RmpPerHealthyMemberTick * MathF.Max(0f, masteryRate);
        RegionMasteryPoints += rmp;

        // ...and TELL THE CREATURE it worked.
        //
        // This is the "assigned job" half of branching evolution, and it had no production caller at
        // all: the sum above collapsed everyone's labour into one float for mastery, and no creature
        // was ever informed it had done anything. So EvolutionProgress.HealthyWorkTicks stayed empty
        // for every creature in every save, and BULWARK MOSS — which wants 40 of them — could not
        // evolve for any player, ever. The farm was computing the exact number the evolution system
        // was starving for and throwing it away.
        //
        // Per-creature and floored to whole ticks, because a fractional tick is not a tick: the edge
        // asks "has it worked forty times", and 0.7 of a shift is not an answer to that.
        foreach (var c in healthy)
        {
            _tickFraction.TryGetValue(c.Id, out var carried);
            var accrued = carried + seconds / c.WorkTickIntervalSeconds;
            var whole = (int)accrued;

            // Carry the remainder rather than dropping it — otherwise a farm ticked every second would
            // accrue nothing at all, since each tick's fraction would round to zero forever.
            _tickFraction[c.Id] = accrued - whole;
            for (var i = 0; i < whole; i++) c.Evolution?.RecordWorkTick(c.Role, c.IsHealthy);
        }

        // The GENERATOR is what makes kills — Attacker OR Producer, the exact pair the Warren's generator
        // station and CompositionCompleteness accept. This used to demand an Attacker specifically, so a
        // Producer staffed into the generator slot formed a "complete", fully-mastered team (CCS = 1, up to
        // ~120% efficiency) that nonetheless produced zero kills/cores/gleam forever — a maxed farm paying
        // nothing. Matching the composition contract closes that gap.
        var hasGenerator = healthy.Any(c => c.Role is Role.Attacker or Role.Producer);
        if (!hasGenerator)
            return new AutomationYield { RmpGained = rmp };

        // Kills come from the derived clear time — which comes from par and the team's efficiency.
        // Fractional progress CARRIES ACROSS TICKS: a 1-second tick against a 40-second clear must
        // bank 1/40th of a kill, not truncate to zero. See _killProgress.
        var clearTime = AutomationClearTimeSeconds();
        _killProgress += seconds / MathF.Max(0.01f, clearTime);

        var kills = (int)_killProgress;
        _killProgress -= kills;

        if (kills <= 0) return new AutomationYield { RmpGained = rmp };

        // Stage 1 kills but does not collect. Stage 2 collects. Stage 3 also sells.
        var cores = AutomationStage >= 2 ? kills : 0;
        var (realized, forfeited) = AutomationStage >= 3
            ? RealizeGleam(kills * gleamPerKill)
            : (0, 0);

        return new AutomationYield
        {
            Kills = kills,
            CoresProduced = cores,
            GleamRealized = realized,
            GleamForfeitedToCap = forfeited,
            RmpGained = rmp,
        };
    }

    /// <summary>
    /// Apply the hourly auto-sell cap.
    /// </summary>
    /// <remarks>
    /// The cap is on GLEAM PER HOUR OF ELAPSED TIME, not per kill and not per session, so it cannot be
    /// gamed by batching. Anything above it is forfeited, and we report how much — a silently swallowed
    /// cap would look like a bug to a player watching their loot vanish.
    /// </remarks>
    /// <summary>Bank auto-sell allowance for elapsed time. Called every tick, kills or not.</summary>
    private void AccrueGleamAllowance(float seconds)
    {
        _secondsIntoHour += seconds;
        _gleamCreditThisHour += _tuning.AutoSellGleamCapPerHour * (seconds / 3600f);
    }

    /// <summary>Spend banked allowance on this tick's earnings. Anything above it is forfeited.</summary>
    private (int Realized, int Forfeited) RealizeGleam(int earned)
    {
        var realized = (int)MathF.Min(earned, _gleamCreditThisHour);
        _gleamCreditThisHour -= realized;

        return (realized, Math.Max(0, earned - realized));
    }
}
