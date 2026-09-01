using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Expeditions;

/// <summary>
/// The last handful of run reports, kept so the player can read them when they choose to.
/// </summary>
/// <remarks>
/// <para>
/// THE REPORT IS THE ONLY PLACE THIS GAME CAN TEACH — combat is automatic and a run has no decisions in
/// it, so the loop is build, run, READ, build. And it was shown for four seconds in an overlay that
/// cleared itself when the next descent began.
/// </para>
/// <para>
/// That is a contradiction with the genre. An idle game is played by a person who is not looking at the
/// screen; a lesson delivered only to someone watching at the exact moment their champion fell is a
/// lesson delivered to nobody. Worse, the reports the player most needs to compare — this run against
/// the last one — are precisely the ones a transient overlay throws away.
/// </para>
/// <para>
/// Ten entries, newest first. Enough to see a build's arc across an evening; short enough that the list
/// stays readable and the save stays small.
/// </para>
/// </remarks>
public sealed class RunLog
{
    public const int Capacity = 10;

    private readonly List<RunReport> _entries = new();

    /// <summary>Newest first — the order a player reads them in.</summary>
    public IReadOnlyList<RunReport> Entries => _entries;

    public int Count => _entries.Count;

    public RunReport? Newest => _entries.Count > 0 ? _entries[0] : null;

    /// <summary>
    /// The newest run in a region — what a run that has just ended should be compared against.
    /// </summary>
    /// <remarks>
    /// Per REGION, not simply "the run before". A player who pushed one region, wandered into another
    /// and came back would otherwise be shown a difference between two sets of CONTENT and told it was a
    /// change in their build, which is the exact opposite of what the report exists to say.
    ///
    /// Call it BEFORE adding the new report, or it returns the run you are about to compare.
    /// </remarks>
    public RunReport? PreviousIn(string regionId)
        => _entries.FirstOrDefault(e => e.RegionId == regionId);

    /// <summary>
    /// The next entry OLDER than <paramref name="index"/> from the same region — the log viewer's diff.
    /// </summary>
    /// <remarks>
    /// A separate method rather than a skip parameter on the one above. The first version took a skip and
    /// meant two different things depending on the caller; its own test caught it by dereferencing a
    /// null, which is what a confused API looks like from outside.
    /// </remarks>
    public RunReport? OlderThan(int index)
    {
        if (index < 0 || index >= _entries.Count) return null;
        var region = _entries[index].RegionId;
        for (var i = index + 1; i < _entries.Count; i++)
            if (_entries[i].RegionId == region) return _entries[i];
        return null;
    }

    public void Add(RunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _entries.Insert(0, report);
        while (_entries.Count > Capacity) _entries.RemoveAt(_entries.Count - 1);
    }

    /// <summary>Restore from a save, newest first, trimmed to capacity.</summary>
    public void Restore(IEnumerable<RunReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        _entries.Clear();
        _entries.AddRange(reports.Take(Capacity));
    }

    // ── Save shape conversion. It lives here, beside the log, rather than in the persistence layer:
    //    the report's fields are gameplay knowledge, and the save layer should not have to know which
    //    of them is a fraction and which an enum. ──────────────────────────────────────────────────

    public static Persistence.RunReportSave ToSave(RunReport r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new Persistence.RunReportSave
        {
            RegionId = r.RegionId,
            Depth = r.Depth,
            IsRecord = r.IsRecord,
            Outcome = (int)r.Outcome,
            OutcomeName = r.Outcome.ToString(),
            WallWave = r.WallWave,
            WallArchetype = (int)r.WallArchetype,
            WallArchetypeName = r.WallArchetype.ToString(),
            WallAffixes = r.WallAffixes.Select(a => (int)a).ToList(),
            WallAffixNames = r.WallAffixes.Select(a => a.ToString()).ToList(),
            WallCreatures = r.WallCreatures,
            AbsorbedFraction = r.AbsorbedFraction,
            AverageHitSize = r.AverageHitSize,
            TargetsPerActivation = r.TargetsPerActivation,
            CreaturesPerWave = r.CreaturesPerWave,
            HealthLostPerWaveFraction = r.HealthLostPerWaveFraction,
            ShieldAbsorbedFraction = r.ShieldAbsorbedFraction,
            ShieldAbsorbedPerWaveFraction = r.ShieldAbsorbedPerWaveFraction,
            SecondsPerWave = r.SecondsPerWave,
            SampledWaves = r.SampledWaves,
        };
    }

    public static RunReport FromSave(Persistence.RunReportSave s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new RunReport
        {
            RegionId = s.RegionId,
            Depth = s.Depth,
            IsRecord = s.IsRecord,
            Outcome = Enum.TryParse<WaveOutcome>(s.OutcomeName, out var oc) ? oc : (WaveOutcome)s.Outcome,
            WallWave = s.WallWave,
            WallArchetype = Enum.TryParse<Encounters.Archetype>(s.WallArchetypeName, out var arch)
                ? arch
                : (Encounters.Archetype)s.WallArchetype,
            WallAffixes = s.WallAffixNames.Count > 0
                ? s.WallAffixNames
                    .Select(n => Enum.TryParse<Encounters.Affix>(n, out var a) ? a : (Encounters.Affix?)null)
                    .Where(a => a is not null).Select(a => a!.Value).ToList()
                : s.WallAffixes.Select(LegacyAffix).Where(a => a is not null).Select(a => a!.Value).ToList(),
            WallCreatures = s.WallCreatures,
            AbsorbedFraction = s.AbsorbedFraction,
            AverageHitSize = s.AverageHitSize,
            TargetsPerActivation = s.TargetsPerActivation,
            CreaturesPerWave = s.CreaturesPerWave,
            HealthLostPerWaveFraction = s.HealthLostPerWaveFraction,
            ShieldAbsorbedFraction = s.ShieldAbsorbedFraction,
            ShieldAbsorbedPerWaveFraction = s.ShieldAbsorbedPerWaveFraction,
            SecondsPerWave = s.SecondsPerWave,
            SampledWaves = s.SampledWaves,
        };
    }

    /// <summary>
    /// The Affix enum's order AS IT WAS while reports saved bare ints (through 2026-08-31). Frozen:
    /// HOLLOW (9) was cut from the enum — authored into no band, implementing no rule — which shifted
    /// every later member, so a bare int from an old save must come through this table, never a cast.
    /// Saves written since carry names, and an unknown name (a future retirement) is skipped the same
    /// way the null row is here.
    /// </summary>
    private static readonly Encounters.Affix?[] LegacyAffixByIndex =
    {
        Encounters.Affix.None, Encounters.Affix.Numbers, Encounters.Affix.Plated, Encounters.Affix.Ritual,
        Encounters.Affix.Endless, Encounters.Affix.Brittle, Encounters.Affix.Entrenched, Encounters.Affix.Swift,
        Encounters.Affix.Warded, null /* Hollow — retired */, Encounters.Affix.Legion,
    };

    private static Encounters.Affix? LegacyAffix(int index)
        => index >= 0 && index < LegacyAffixByIndex.Length ? LegacyAffixByIndex[index] : null;
}
