using System.Collections.Generic;
using IdleXIdle.Core.Automation;

namespace IdleXIdle.Core.Encounters;

/// <summary>
/// Each region's five-band cycle and its creature roster — the content that asks the questions the
/// skill tree answers.
/// </summary>
/// <remarks>
/// <para>
/// A region's identity is which archetype it leans on and which affix it repeats, not its enemy art.
/// The six regions already implied theirs in their own flavour text — Cinderworks grinds with "slow,
/// heavy industrial blows", the Still Archive kills by "a thousand cuts", Marrow Wastes is "a
/// slaughterhouse" — and this makes that implication mechanical.
/// </para>
/// <para>
/// <b>The counter-band rule.</b> Every cycle carries its signature archetype in two of five bands and
/// exactly ONE band of the archetype it answers least well. Without it a region is solvable with a
/// single shape and the whole tree collapses to one branch. It stays at one: at two the region has no
/// identity, at zero it has no lesson.
/// </para>
/// <para>
/// Rosters draw a region's own Source plus its two neighbours on the wheel, so no region is a
/// single-matchup lookup and picking a Source is a bet rather than an answer.
/// </para>
/// </remarks>
public static class BandCycles
{
    private static BandDefinition Band(
        Affix affix, (Archetype, int)[] composition, Affix second = Affix.None)
        => new(composition, affix, second);

    /// <summary>VERDANT HOLLOW — the baseline. Teaches what a wave is and what the report is for.</summary>
    /// <remarks>
    /// Band 1 is the only band in the game with no affix at all. Band 5 is the counter-band: a Spread
    /// build that walked bands 1-4 meets a Weight question and learns what a wall is somewhere safe.
    /// </remarks>
    public static readonly IReadOnlyList<BandDefinition> VerdantHollow = new[]
    {
        Band(Affix.None, new[] { (Archetype.Swarm, 40), (Archetype.Bruiser, 30), (Archetype.Caster, 30) }),
        Band(Affix.Numbers, new[] { (Archetype.Swarm, 55), (Archetype.Armoured, 25), (Archetype.Bruiser, 20) }),
        Band(Affix.None, new[] { (Archetype.Bruiser, 50), (Archetype.Caster, 30), (Archetype.Swarm, 20) }),
        Band(Affix.Legion, new[] { (Archetype.Swarm, 45), (Archetype.Caster, 35), (Archetype.Armoured, 20) }),
        Band(Affix.Plated, new[] { (Archetype.Armoured, 40), (Archetype.Bruiser, 35), (Archetype.Swarm, 25) }),
    };

    /// <summary>CINDERWORKS — the plated region. Teaches hit size.</summary>
    /// <remarks>Band 3 is the counter-band: a pure Weight build meets five creatures at once.</remarks>
    public static readonly IReadOnlyList<BandDefinition> Cinderworks = new[]
    {
        Band(Affix.None, new[] { (Archetype.Armoured, 50), (Archetype.Bruiser, 30), (Archetype.Swarm, 20) }),
        Band(Affix.Plated, new[] { (Archetype.Armoured, 45), (Archetype.Caster, 30), (Archetype.Bruiser, 25) }),
        Band(Affix.Numbers, new[] { (Archetype.Swarm, 55), (Archetype.Armoured, 25), (Archetype.Caster, 20) }),
        Band(Affix.Swift, new[] { (Archetype.Bruiser, 45), (Archetype.Armoured, 35), (Archetype.Caster, 20) }),
        Band(Affix.Plated, new[] { (Archetype.Armoured, 60), (Archetype.Bruiser, 40) }),
    };

    /// <summary>UMBRAL REACH — the swarm region. Teaches action economy.</summary>
    /// <remarks>Band 3 is the counter-band: armour, against a build that has bought target count.</remarks>
    public static readonly IReadOnlyList<BandDefinition> UmbralReach = new[]
    {
        Band(Affix.None, new[] { (Archetype.Swarm, 60), (Archetype.Caster, 25), (Archetype.Bruiser, 15) }),
        Band(Affix.Legion, new[] { (Archetype.Swarm, 50), (Archetype.Caster, 35), (Archetype.Armoured, 15) }),
        Band(Affix.Plated, new[] { (Archetype.Armoured, 55), (Archetype.Bruiser, 30), (Archetype.Swarm, 15) }),
        Band(Affix.Numbers, new[] { (Archetype.Swarm, 55), (Archetype.Caster, 30), (Archetype.Bruiser, 15) }),
        Band(Affix.Brittle, new[] { (Archetype.Caster, 45), (Archetype.Swarm, 40), (Archetype.Bruiser, 15) }),
    };

    /// <summary>MARROW WASTES — attrition. The first region where health, not damage, is the constraint.</summary>
    /// <remarks>
    /// Two ENDLESS bands stop a leech build brute-forcing it. Band 3 is the counter: everything is
    /// fragile and fast, so a slow tanky build takes more total damage than a quick one.
    /// </remarks>
    public static readonly IReadOnlyList<BandDefinition> MarrowWastes = new[]
    {
        Band(Affix.None, new[] { (Archetype.Bruiser, 55), (Archetype.Armoured, 25), (Archetype.Swarm, 20) }),
        Band(Affix.Endless, new[] { (Archetype.Bruiser, 50), (Archetype.Swarm, 30), (Archetype.Caster, 20) }),
        Band(Affix.Brittle, new[] { (Archetype.Caster, 50), (Archetype.Swarm, 30), (Archetype.Bruiser, 20) }),
        // Band 4 leans ARMOURED rather than Bruiser. With five Bruiser-dominant bands out of five this
        // region asked one question and the test caught it: a region whose every band wants the same
        // shape has no lesson in it. Attrition still owns three of five, and asking for hit size in the
        // middle of it means an Endure build cannot simply out-last the whole place.
        Band(Affix.Swift, new[] { (Archetype.Armoured, 45), (Archetype.Bruiser, 35), (Archetype.Swarm, 20) }),
        Band(Affix.Endless, new[] { (Archetype.Bruiser, 45), (Archetype.Armoured, 30), (Archetype.Caster, 25) }),
    };

    /// <summary>THE STILL ARCHIVE — casters. Teaches speed, and that a mono-Form build has a ceiling.</summary>
    /// <remarks>WARDED lives here. Band 3 stacks ENTRENCHED onto Bruisers and armour.</remarks>
    public static readonly IReadOnlyList<BandDefinition> StillArchive = new[]
    {
        Band(Affix.None, new[] { (Archetype.Caster, 55), (Archetype.Swarm, 25), (Archetype.Bruiser, 20) }),
        Band(Affix.Warded, new[] { (Archetype.Caster, 50), (Archetype.Swarm, 35), (Archetype.Armoured, 15) }),
        Band(Affix.Entrenched, new[] { (Archetype.Bruiser, 50), (Archetype.Armoured, 35), (Archetype.Caster, 15) }),
        // Band 4 leans SWARM. Four Caster-dominant bands made the Archive a single-answer region, which
        // its own test caught. Casters still own three of five and both WARDED bands, so its identity —
        // kill it before it acts, and do not put everything into one Form — is intact.
        Band(Affix.Ritual, new[] { (Archetype.Swarm, 45), (Archetype.Caster, 40), (Archetype.Armoured, 15) }),
        Band(Affix.Warded, new[] { (Archetype.Caster, 40), (Archetype.Armoured, 35), (Archetype.Bruiser, 25) }),
    };

    /// <summary>THE PALE CHOIR — the examination. Teaches nothing; tests everything.</summary>
    /// <remarks>
    /// Every band is an even mix, so no shape is favoured and only breadth survives. Band 5 carries TWO
    /// affixes — the first place in the game that does, and the template for how corruption tiers work
    /// everywhere else.
    /// </remarks>
    public static readonly IReadOnlyList<BandDefinition> PaleChoir = new[]
    {
        Band(Affix.None, Even()),
        Band(Affix.Warded, Even()),
        Band(Affix.Ritual, Even()),
        Band(Affix.Endless, Even()),
        Band(Affix.Numbers, Even(), Affix.Plated),
    };

    private static (Archetype, int)[] Even() => new[]
    {
        (Archetype.Swarm, 25), (Archetype.Armoured, 25), (Archetype.Caster, 25), (Archetype.Bruiser, 25),
    };

    /// <summary>Cycle by region id. Falls back to the baseline so a new region is never empty.</summary>
    public static IReadOnlyList<BandDefinition> For(string regionId) => regionId switch
    {
        "cinderworks" => Cinderworks,
        "umbral_reach" => UmbralReach,
        "marrow_wastes" => MarrowWastes,
        "still_archive" => StillArchive,
        "pale_choir" => PaleChoir,
        _ => VerdantHollow,
    };

    /// <summary>
    /// The creature Sources a region draws from: its own, plus its two neighbours on the wheel.
    /// </summary>
    public static IReadOnlyList<Source> RosterFor(string regionId) => regionId switch
    {
        "cinderworks" => new[] { Source.Machine, Source.Nature, Source.Shadow },
        "umbral_reach" => new[] { Source.Shadow, Source.Machine, Source.Spirit },
        "marrow_wastes" => new[] { Source.Body, Source.Spirit, Source.Nature },
        "still_archive" => new[] { Source.Mind, Source.Body, Source.Shadow },
        "pale_choir" => new[] { Source.Spirit, Source.Mind, Source.Machine },
        _ => new[] { Source.Nature, Source.Body, Source.Mind },
    };

    public static BandDefinition BandFor(string regionId, int wave)
    {
        var cycle = For(regionId);
        return cycle[Bands.CycleIndex(wave, cycle.Count)];
    }
}
