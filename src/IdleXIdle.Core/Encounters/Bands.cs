using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Encounters;

/// <summary>
/// A modifier that holds for a whole band of ten waves.
/// </summary>
/// <remarks>
/// Every affix pressures a NAMED branch of the skill tree. An affix without one is just a difficulty
/// number wearing a costume, and the player has no way to answer it — which matters more here than in
/// most games, because they cannot react inside a run and can only respond by changing the build.
/// </remarks>
public enum Affix
{
    None,

    /// <summary>+1 creature in every wave. Pressures SPREAD.</summary>
    Numbers,

    /// <summary>Enemy armour +50%. Pressures WEIGHT.</summary>
    Plated,

    /// <summary>Enemy damage climbs with each wave elapsed inside the band. Pressures TEMPO.</summary>
    Ritual,

    /// <summary>Leech and regeneration halved. Pressures ENDURE.</summary>
    Endless,

    /// <summary>Enemy health x0.6, damage x1.6 — everything dies fast, including you. Pressures ENDURE.</summary>
    Brittle,

    /// <summary>The first hit on each creature deals a quarter. Pressures TEMPO, and openers specifically.</summary>
    Entrenched,

    /// <summary>Enemies bite 30% more often. Pressures ENDURE.</summary>
    Swift,

    /// <summary>
    /// Enemies take 60% less from whichever Form dealt the most damage LAST wave. Pressures
    /// concentration rather than a shape — the strongest push toward carrying more than one Form.
    /// </summary>
    /// <remarks>
    /// It reads the PREVIOUS wave, not the current one, so the player can see it coming. An affix that
    /// reacted to the wave in progress would be unanswerable in a game with no in-run decisions.
    /// </remarks>
    Warded,

    /// <summary>No chests, doubled haul. An economy decision rather than a combat one.</summary>
    Hollow,

    /// <summary>Each creature splits once on death. Pressures SPREAD.</summary>
    Legion,
}

/// <summary>One band of ten waves: which archetypes are common, and which affix holds.</summary>
public sealed record BandDefinition(
    IReadOnlyList<(Archetype Archetype, int Weight)> Composition,
    Affix Affix,
    Affix SecondAffix = Affix.None);

/// <summary>
/// Depth is divided into bands of ten waves, and a band is a DEMAND rather than a difficulty step.
/// </summary>
/// <remarks>
/// <para>
/// The cycle is authored per region and repeats with a scaling multiplier. It is fixed and learnable
/// on purpose: the player cannot react inside a run, so they must be able to plan before one. A
/// randomised rotation would make every wall a surprise and nothing a lesson.
/// </para>
/// <para>
/// Raw enemy scaling continues underneath, but the wall a player hits should almost always be a demand
/// they have not answered rather than a number they have not reached — only the first kind tells them
/// what to do next.
/// </para>
/// </remarks>
public static class Bands
{
    public const int WavesPerBand = 10;

    /// <summary>How much harder a full cycle of five bands is than the one before it.</summary>
    public const float BandRepeatScale = 1.9f;

    public static int BandOf(int wave) => Math.Max(0, wave - 1) / WavesPerBand;

    /// <summary>Which entry of a five-band cycle this wave falls in.</summary>
    public static int CycleIndex(int wave, int cycleLength) => BandOf(wave) % Math.Max(1, cycleLength);

    /// <summary>How many complete cycles have been walked — each one scales the baseline.</summary>
    public static int ScaleTier(int wave, int cycleLength) => BandOf(wave) / Math.Max(1, cycleLength);

    public static float RepeatScale(int wave, int cycleLength)
        => MathF.Pow(BandRepeatScale, ScaleTier(wave, cycleLength));

    /// <summary>
    /// A stable seed for one wave of one descent.
    /// </summary>
    /// <remarks>
    /// NOT <see cref="HashCode.Combine(object, object, object)"/>. That is randomised per PROCESS by
    /// design, so it would give the same wave a different composition after a restart — and the whole
    /// point of seeding from (region, wave, run) is that fast-forward can pay the haul a wave originally
    /// paid. A test in one process would never catch it; a player closing the game would.
    ///
    /// FNV-1a over the region's characters, then the two integers. Cheap, stable, and portable.
    /// </remarks>
    public static int Seed(string regionId, int wave, int runIndex)
    {
        unchecked
        {
            const uint prime = 16777619;
            var hash = 2166136261;
            foreach (var ch in regionId ?? string.Empty)
            {
                hash ^= ch;
                hash *= prime;
            }
            hash ^= (uint)wave;
            hash *= prime;
            hash ^= (uint)runIndex;
            hash *= prime;
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    /// <summary>Pick this wave's archetype from the band's weighting.</summary>
    public static Archetype Roll(BandDefinition band, Random rng)
    {
        ArgumentNullException.ThrowIfNull(band);
        ArgumentNullException.ThrowIfNull(rng);

        var total = band.Composition.Sum(c => c.Weight);
        if (total <= 0) return Archetype.Bruiser;

        var pick = rng.Next(total);
        foreach (var (archetype, weight) in band.Composition)
        {
            if (pick < weight) return archetype;
            pick -= weight;
        }
        return band.Composition[^1].Archetype;
    }

    /// <summary>Both affixes a band carries, ignoring <see cref="Affix.None"/>.</summary>
    public static IEnumerable<Affix> AffixesOf(BandDefinition band)
    {
        ArgumentNullException.ThrowIfNull(band);
        if (band.Affix != Affix.None) yield return band.Affix;
        if (band.SecondAffix != Affix.None) yield return band.SecondAffix;
    }

    // ── How each affix bends the wave. Kept here rather than in the composer so that the whole
    //    catalogue's effect on the numbers is readable in one place. ──────────────────────────────

    public static float HealthMultiplier(IEnumerable<Affix> affixes)
        => affixes.Aggregate(1f, (m, a) => m * (a == Affix.Brittle ? 0.6f : 1f));

    public static float DamageMultiplier(IEnumerable<Affix> affixes, int wavesIntoBand)
        => affixes.Aggregate(1f, (m, a) => m * a switch
        {
            Affix.Brittle => 1.6f,
            Affix.Ritual => 1f + 0.08f * Math.Max(0, wavesIntoBand),
            _ => 1f,
        });

    public static float DefenseMultiplier(IEnumerable<Affix> affixes)
        => affixes.Aggregate(1f, (m, a) => m * (a == Affix.Plated ? 1.5f : 1f));

    /// <summary>The same, named for the call site that only wants to know whether to rebuild the list.</summary>
    public static float DefenceMultiplierOrOne(IEnumerable<Affix> affixes) => DefenseMultiplier(affixes);

    public static int ExtraCreatures(IEnumerable<Affix> affixes)
        => affixes.Count(a => a == Affix.Numbers);

    public static float IntervalMultiplier(IEnumerable<Affix> affixes)
        => affixes.Aggregate(1f, (m, a) => m * (a == Affix.Swift ? 0.7f : 1f));

    /// <summary>How much of the champion's leech and regeneration survives this band.</summary>
    public static float SustainMultiplier(IEnumerable<Affix> affixes)
        => affixes.Aggregate(1f, (m, a) => m * (a == Affix.Endless ? 0.5f : 1f));
}
