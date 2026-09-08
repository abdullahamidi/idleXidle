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
/// <summary>
/// What ended the run, in one word — the thresholds <see cref="RunReport.Verdict"/> has always applied,
/// exposed so the HUNT's fall plate and the log's diagnosis line can NAME the limit before the numbers.
/// </summary>
/// <remarks>
/// UX V2 P1.1 (brief §22/§24): "1.0 of 3.0 creatures per cast" is a magnitude; REACH is its meaning. No new
/// telemetry — the same four comparisons, in the same order, returning a name instead of a sentence.
/// </remarks>
public enum RunLimit
{
    /// <summary>The wave outlived the clock: damage, not health.</summary>
    Stalled,
    /// <summary>Armour ate too much of the damage.</summary>
    Armour,
    /// <summary>Casts reached too few of the creatures in the wave.</summary>
    Reach,
    /// <summary>Too much health lost per wave.</summary>
    Sustain,
    /// <summary>Nothing specific — the numbers won.</summary>
    OutScaled,
}

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

    /// <summary>
    /// What SHIELD ate, as a share of everything the wave landed on the champion — absorbed plus what
    /// reached health. Zero for a build with no shield, which is how the log knows not to draw the row.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT folded into <see cref="AbsorbedFraction"/>, which answers a different question:
    /// that one is armour eating the champion's OUTGOING damage, this one is shield eating the wave's
    /// INCOMING damage. One number for both would be a number for neither.
    /// </remarks>
    public float ShieldAbsorbedFraction { get; init; }

    /// <summary>Shield absorbed per wave, as a share of the champion's pool — the figure the log shows.</summary>
    public float ShieldAbsorbedPerWaveFraction { get; init; }

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
    public string Verdict() => Limit switch
    {
        RunLimit.Stalled => $"Stalled on wave {WallWave} — the wave outlived the clock, so this is damage, not health.",
        RunLimit.Armour => $"Armour ate {AbsorbedFraction:P0} of your damage. Your hits average {AverageHitSize:F0}.",
        RunLimit.Reach => $"You reached {TargetsPerActivation:F1} of {CreaturesPerWave:F1} creatures per cast.",
        RunLimit.Sustain => $"You lost {HealthLostPerWaveFraction:P0} of your health per wave over the last band.",
        _ => $"Out-scaled on wave {WallWave} — nothing specific beat you, the numbers did.",
    };

    /// <summary>The one limit this run hit — the first of the verdict's thresholds that holds.</summary>
    public RunLimit Limit =>
        Outcome == WaveOutcome.Stalled ? RunLimit.Stalled
        : AbsorbedFraction >= 0.45f ? RunLimit.Armour
        : CreaturesPerWave >= 2.5f && TargetsPerActivation < CreaturesPerWave * 0.5f ? RunLimit.Reach
        : HealthLostPerWaveFraction >= 0.18f ? RunLimit.Sustain
        : RunLimit.OutScaled;

    /// <summary>The limit as the label a plate prints: ARMOUR · REACH · SUSTAIN · DAMAGE · OUT-SCALED.</summary>
    public string LimitLabel() => Limit switch
    {
        RunLimit.Stalled => "DAMAGE",
        RunLimit.Armour => "ARMOUR",
        RunLimit.Reach => "REACH",
        RunLimit.Sustain => "SUSTAIN",
        _ => "OUT-SCALED",
    };


    /// <summary>
    /// What changed since the previous run in this region, as structured rows. The diff is what makes
    /// iteration legible — without it every run is judged in isolation and the build lab has no
    /// feedback at all.
    /// </summary>
    /// <remarks>
    /// The
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
        var shieldAte = sample.Sum(m => m.ShieldAbsorbed);
        var healthAte = (float)sample.Sum(m => m.HealthDamage);
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
            ShieldAbsorbedFraction = shieldAte + healthAte <= 0f ? 0f : shieldAte / (shieldAte + healthAte),
            ShieldAbsorbedPerWaveFraction = championMaxHealth <= 0 ? 0f : shieldAte / n / championMaxHealth,
            SecondsPerWave = ms / 1000f / n,
            SampledWaves = sample.Count,
        };
    }
}
