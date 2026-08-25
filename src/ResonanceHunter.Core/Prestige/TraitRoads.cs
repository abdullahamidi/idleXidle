using System;
using System.Collections.Generic;
using System.Linq;

namespace ResonanceHunter.Core.Prestige;

/// <summary>A road's identity: the name the screen prints and the one sentence that says what walking it does in the fight.</summary>
/// <param name="Road">Which road.</param>
/// <param name="Name">The name in capitals, as the road header and the detail panel print it.</param>
/// <param name="Sentence">One plain sentence, effect first, that a player can check against the nodes.</param>
public sealed record TraitRoadIdentity(TraitRoad Road, string Name, string Sentence);

/// <summary>
/// The five parts of the trait tree, each with the one line that says what it is FOR.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, 2026-08-25: players could not connect the traits to how the game plays, and could not tell
/// the roads apart. The screen used to print a road's name and a slogan ("The wall", "The economy
/// build") that a stranger to the genre cannot decode. These sentences are written the other way
/// round — effect first, in the words the fight uses — and they live in Core rather than on the screen
/// so a test can hold them against what the roads' nodes actually do.
/// </para>
/// <para>
/// Each sentence was checked against the catalogue before it was written. RUIN's keystones double
/// damage, scale it with lost health and cut healing; its minors are HARDER HITS. AEGIS doubles or
/// triples health and pays in skill speed; its minors are MORE HEALTH. ARTIFICE makes skills fire
/// twice, poison, or fire as another Form, and pays vows more; its minors are FASTER SKILLS. AVARICE
/// doubles loot or rarity and pays in hit size; its minors are MORE LOOT and RARER FINDS. The spine
/// sells capacity and never a number.
/// </para>
/// </remarks>
public static class TraitRoads
{
    /// <summary>Every road, spine first, in the order the screen and the catalogue use.</summary>
    public static IReadOnlyList<TraitRoadIdentity> All { get; } = new[]
    {
        new TraitRoadIdentity(TraitRoad.Spine, "THE SPINE",
            "Keystone sockets, skill slots, vows, auto-selling and the forge. Everyone grows these."),
        new TraitRoadIdentity(TraitRoad.Ruin, "RUIN",
            "Hit harder. Live closer to death."),
        new TraitRoadIdentity(TraitRoad.Aegis, "AEGIS",
            "Live longer. Strike less often."),
        new TraitRoadIdentity(TraitRoad.Artifice, "ARTIFICE",
            "Skills act differently. Vows pay more."),
        new TraitRoadIdentity(TraitRoad.Avarice, "AVARICE",
            "More loot and rarer loot. Softer hits."),
    };

    /// <summary>The identity of one road. Throws for a road the catalogue has never heard of, which is a programming error.</summary>
    public static TraitRoadIdentity Of(TraitRoad road)
        => All.FirstOrDefault(r => r.Road == road)
           ?? throw new ArgumentOutOfRangeException(nameof(road), road, "No identity is written for this road.");

    /// <summary>The road's printed name.</summary>
    public static string Name(TraitRoad road) => Of(road).Name;

    /// <summary>The road's one-sentence identity.</summary>
    public static string Sentence(TraitRoad road) => Of(road).Sentence;
}
