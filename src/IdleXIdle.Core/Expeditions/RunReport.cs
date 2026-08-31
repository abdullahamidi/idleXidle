using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;

namespace IdleXIdle.Core.Expeditions;

/// <summary>
/// What one descent did, and why it ended.
/// </summary>
/// <remarks>
/// <para>
/// This is the most important object in the game. Combat is automatic and a run has no decisions in it,
/// so the report is the ONLY moment the player can learn anything — the loop is build, run, read, build.
/// </para>
/// <para>
/// It gives a DIAGNOSIS, never a prescription. It names the demand that beat them and shows the
/// measurements that prove it; it never says which node to buy. That line is what keeps theorycrafting
/// in the player's hands: a report that says "take Heavy Strike" has played the game for them.
/// </para>
/// <para>
/// The measurements cover the LAST BAND only, because that is where the run actually failed. Averaging
/// over a whole descent buries the wall under fifty waves the build handled comfortably.
/// </para>
/// </remarks>
public sealed record RunReport
{
    public required string RegionId { get; init; }
    public required int Depth { get; init; }
    public required bool IsRecord { get; init; }
    public required WaveOutcome Outcome { get; init; }

    /// <summary>The wave that ended it, and what it held.</summary>
    public required int WallWave { get; init; }
    public required Archetype WallArchetype { get; init; }
    public required IReadOnlyList<Affix> WallAffixes { get; init; }
    public required int WallCreatures { get; init; }

    // ── The measurements, over the last band. Each one points at a lever. ──────────────────────────

    /// <summary>How much of the swing enemy armour ate. Points at hit size.</summary>
    public required float AbsorbedFraction { get; init; }

    /// <summary>Points at hit size from the other direction.</summary>
    public required float AverageHitSize { get; init; }

    /// <summary>Creatures reached per cast, against how many were there. Points at action economy.</summary>
    public required float TargetsPerActivation { get; init; }
    public required float CreaturesPerWave { get; init; }

    /// <summary>Share of the champion's pool lost per wave. Points at sustain.</summary>
    public required float HealthLostPerWaveFraction { get; init; }

    /// <summary>Seconds to clear a wave. Points at throughput.</summary>
    public required float SecondsPerWave { get; init; }

    /// <summary>Waves the measurements were taken over.</summary>
    public required int SampledWaves { get; init; }

    /// <summary>
    /// The plainest honest statement of why the run ended.
    /// </summary>
    /// <remarks>
    /// Names the DEMAND, not the fix. "Armour ate 61% of your damage" tells the player where to look
    /// without telling them what to do about it, which is the whole design line for this screen.
    /// </remarks>
    public string Verdict()
    {
        if (Outcome == WaveOutcome.Stalled)
            return $"Stalled on wave {WallWave} — the wave outlived the clock, so this is damage, not health.";

        if (AbsorbedFraction >= 0.45f)
            return $"Armour ate {AbsorbedFraction:P0} of your damage. Your hits average {AverageHitSize:F0}.";

        if (CreaturesPerWave >= 2.5f && TargetsPerActivation < CreaturesPerWave * 0.5f)
            return $"You reached {TargetsPerActivation:F1} of {CreaturesPerWave:F1} creatures per cast.";

        if (HealthLostPerWaveFraction >= 0.18f)
            return $"You lost {HealthLostPerWaveFraction:P0} of your health per wave over the last band.";

        return $"Out-scaled on wave {WallWave} — nothing specific beat you, the numbers did.";
    }

    /// <summary>What changed since the previous run in this region, as ready-to-show lines.</summary>
    /// <remarks>
    /// The diff is what makes iteration legible: it is how a player sees that a change raised their
    /// average hit by 60% and moved their wall from Armoured to Swarm. Without it every run is judged in
    /// isolation and the build lab has no feedback at all.
    /// </remarks>
    public IEnumerable<string> DiffAgainst(RunReport? previous)
    {
        foreach (var e in DiffEntries(previous))
            yield return e.Numeric ? Line(e.Label, e.Was, e.Now, e.Unit) : $"{e.Label}   {e.WasText} -> {e.NowText}";
    }

    /// <summary>
    /// The same diff as <see cref="DiffAgainst"/>, as structured rows — for a screen that wants to lay
    /// the before, the after and the change out in columns rather than print one string per line.
    /// </summary>
    /// <remarks>
    /// Nothing new is measured here; every row is one <see cref="DiffAgainst"/> line taken apart. The
    /// screen colours a change by <see cref="DiffEntry.Improved"/>, which is why each row says which
    /// direction is the good one: more depth, bigger hits and more reach are better, less absorbed is.
    /// </remarks>
    public IEnumerable<DiffEntry> DiffEntries(RunReport? previous)
    {
        if (previous is null || previous.RegionId != RegionId) yield break;

        yield return DiffEntry.Of("DEPTH", previous.Depth, Depth, "", higherIsBetter: true);
        yield return DiffEntry.Of("HIT SIZE", previous.AverageHitSize, AverageHitSize, "", higherIsBetter: true);
        yield return DiffEntry.Of("REACH", previous.TargetsPerActivation, TargetsPerActivation, " targets/cast", higherIsBetter: true);
        yield return DiffEntry.Of("ABSORBED", previous.AbsorbedFraction * 100f, AbsorbedFraction * 100f, "%", higherIsBetter: false);

        if (previous.WallArchetype != WallArchetype)
            yield return DiffEntry.Named("WALL", previous.WallArchetype.ToString(), WallArchetype.ToString());
    }

    private static string Line(string label, float was, float now, string unit)
    {
        var delta = now - was;
        var sign = delta >= 0 ? "+" : "";
        return $"{label,-10} {was:F1}{unit} -> {now:F1}{unit}   ({sign}{delta:F1})";
    }
}

/// <summary>One row of a run-to-run diff: what it is called, what it was, what it is now.</summary>
/// <param name="Label">The measure's name, as the report prints it.</param>
/// <param name="Numeric">True for a measured value; false for a named one (the wall's archetype).</param>
/// <param name="Was">The previous run's value (numeric rows only).</param>
/// <param name="Now">This run's value (numeric rows only).</param>
/// <param name="Unit">The unit the value is printed in, leading space included ("" for none).</param>
/// <param name="HigherIsBetter">Which direction of change is the good one (numeric rows only).</param>
/// <param name="WasText">The previous run's value as a word (named rows only).</param>
/// <param name="NowText">This run's value as a word (named rows only).</param>
public readonly record struct DiffEntry(
    string Label, bool Numeric, float Was, float Now, string Unit, bool HigherIsBetter, string WasText, string NowText)
{
    /// <summary>A measured row.</summary>
    public static DiffEntry Of(string label, float was, float now, string unit, bool higherIsBetter)
        => new(label, true, was, now, unit, higherIsBetter, "", "");

    /// <summary>A named row — no delta, only a change of word.</summary>
    public static DiffEntry Named(string label, string was, string now)
        => new(label, false, 0f, 0f, "", true, was, now);

    /// <summary>How much the value moved (numeric rows only).</summary>
    public float Delta => Now - Was;

    /// <summary>Whether the change is in the good direction; null when nothing moved or the row is named.</summary>
    public bool? Improved => !Numeric || MathF.Abs(Delta) < 0.05f ? null : (Delta > 0f) == HigherIsBetter;
}

/// <summary>Accumulates per-wave metrics into the band-scoped summary the report needs.</summary>
public sealed class RunRecorder
{
    private readonly List<(int Wave, WaveMetrics M)> _waves = new();

    public void Record(int wave, WaveMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        _waves.Add((wave, metrics));
    }

    public int WaveCount => _waves.Count;

    /// <summary>
    /// Build the report from the last band's waves.
    /// </summary>
    /// <param name="championMaxHealth">Used to express health lost as a share of the pool.</param>
    public RunReport Build(
        string regionId, int depth, bool isRecord, WaveOutcome outcome,
        int wallWave, Archetype wallArchetype, IReadOnlyList<Affix> wallAffixes, int wallCreatures,
        int championMaxHealth)
    {
        // The last band only — a build that walked forty waves and died in the forty-first is not
        // described by an average over all forty-one.
        var band = Bands.BandOf(Math.Max(1, wallWave));
        var sample = _waves.Where(w => Bands.BandOf(w.Wave) == band).Select(w => w.M).ToList();
        if (sample.Count == 0) sample = _waves.TakeLast(5).Select(w => w.M).ToList();

        var raw = sample.Sum(m => m.RawDamage);
        var delivered = sample.Sum(m => m.DeliveredDamage);
        var hits = sample.Sum(m => m.Hits);
        var activations = sample.Sum(m => m.Activations);
        var struck = sample.Sum(m => m.TargetsStruck);
        var present = sample.Sum(m => m.CreaturesPresent);
        var lost = sample.Sum(m => m.HealthLost);
        var ms = sample.Sum(m => m.DurationMs);
        var n = Math.Max(1, sample.Count);

        return new RunReport
        {
            RegionId = regionId,
            Depth = depth,
            IsRecord = isRecord,
            Outcome = outcome,
            WallWave = wallWave,
            WallArchetype = wallArchetype,
            WallAffixes = wallAffixes,
            WallCreatures = wallCreatures,
            AbsorbedFraction = raw <= 0f ? 0f : 1f - delivered / raw,
            AverageHitSize = hits <= 0 ? 0f : delivered / hits,
            TargetsPerActivation = activations <= 0 ? 0f : struck / (float)activations,
            CreaturesPerWave = present / (float)n,
            HealthLostPerWaveFraction = championMaxHealth <= 0 ? 0f : lost / (float)n / championMaxHealth,
            SecondsPerWave = ms / 1000f / n,
            SampledWaves = sample.Count,
        };
    }
}
