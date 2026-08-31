using System;
using System.Collections.Generic;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Core.Encounters;

/// <summary>
/// The four shapes an enemy can take, and the four questions the content asks a build.
/// </summary>
/// <remarks>
/// <para>
/// With combat fully automatic, the only way a build can be WRONG rather than merely SMALL is if
/// different content punishes different build shapes. These four cover the four ways a build is
/// shaped, and each maps to one branch of the skill tree:
/// </para>
/// <list type="bullet">
/// <item><b>Swarm</b> punishes single-target and long cooldowns — answered by Spread.</item>
/// <item><b>Armoured</b> punishes many small hits — answered by Weight.</item>
/// <item><b>Caster</b> punishes slow kills — answered by Tempo.</item>
/// <item><b>Bruiser</b> punishes nothing in particular — it is the honest check, answered by Endure.</item>
/// </list>
/// <para>
/// An archetype is a ROLE, not a creature. The same template appears as a Swarm of three at reduced
/// scale in one band and as a single Bruiser in another; the player reads which from the count and the
/// archetype glyph. That is the game's own visual anchor — every mechanically important state gets a
/// distinct glyph — applied to its most important mechanical state, and it means new content costs a
/// table row rather than a creature sprite.
/// </para>
/// </remarks>
public enum Archetype
{
    Swarm,
    Armoured,
    Caster,
    Bruiser,
}

/// <summary>The stat shape of one archetype, as multipliers against the wave's baseline.</summary>
public sealed record ArchetypeShape(
    Archetype Archetype,
    int MinCount,
    int MaxCount,
    float HealthMult,
    float DamageMult,
    float DefenseBase);

public static class Archetypes
{
    /// <summary>
    /// The authored shapes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SWARM's combined damage is deliberately higher than a Bruiser's while its combined health is
    /// lower, so ignoring action economy is punished immediately rather than slowly.
    /// </para>
    /// <para>
    /// ARMOURED is the only archetype with meaningful Defense, and Defense is the only flat-subtraction
    /// number in the game (<see cref="SoloBattle.MinHitFraction"/>). That is what makes hit SIZE matter.
    /// </para>
    /// <para>
    /// CASTER's health is low enough that a fast build deletes it before it swings and its damage is
    /// high enough that a slow one eats the whole hit.
    /// </para>
    /// <para>
    /// BRUISER has no trick, on purpose. It exists so a player who has solved the other three with
    /// gimmicks still has to have real numbers.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<Archetype, ArchetypeShape> Shapes { get; } =
        new Dictionary<Archetype, ArchetypeShape>
        {
            [Archetype.Swarm] = new(Archetype.Swarm, 3, 5, 0.30f, 0.32f, 0f),
            [Archetype.Armoured] = new(Archetype.Armoured, 1, 2, 1.25f, 0.85f, 25f),
            [Archetype.Caster] = new(Archetype.Caster, 1, 2, 0.50f, 1.00f, 0f),
            [Archetype.Bruiser] = new(Archetype.Bruiser, 1, 1, 2.40f, 1.35f, 10f),
        };

    /// <summary>
    /// The per-creature multipliers are balanced on their TOTALS, not on their face values.
    /// </summary>
    /// <remarks>
    /// The first draft read well per creature and was wrong per wave. Multiplied by count, a Swarm of
    /// five at 0.45 threw 2.25x a single enemy's damage and two Casters at 2.20 threw 4.4x — against a
    /// baseline tuned when every wave held exactly one creature. Depth collapsed to 4, and a test that
    /// only asked "does more damage push deeper" caught it because BOTH builds died in the same place.
    ///
    /// Expected totals now, at average count:
    ///
    ///     Swarm     4 x 0.32 = 1.28 damage,  4 x 0.30 = 1.20 health
    ///     Armoured  1.5 x 0.85 = 1.28,       1.5 x 1.25 = 1.88
    ///     Caster    1.5 x 1.00 = 1.50,       1.5 x 0.50 = 0.75
    ///     Bruiser   1 x 1.35 = 1.35,         1 x 2.40 = 2.40
    ///
    /// Threat stays comparable across archetypes while the SHAPES stay distinct: the Caster still hits
    /// hardest per creature and dies fastest, the Bruiser is still the wall, the Swarm still wins on
    /// action economy rather than on raw output.
    /// </remarks>

    /// <summary>How fast an Armoured creature's plate thickens with depth.</summary>
    /// <remarks>
    /// Armour must grow, or a Weight build solves Armoured once and never thinks about it again. It
    /// grows slowly (2% of base per wave) so that the answer is a build shape rather than a stat race.
    /// </remarks>
    public const float DefenseGrowthPerWave = 0.02f;

    public static ArchetypeShape Shape(Archetype a) => Shapes[a];

    /// <summary>
    /// Build the creatures for one wave.
    /// </summary>
    /// <param name="archetype">The role every creature in this wave takes.</param>
    /// <param name="baseHealth">The wave's baseline health, already scaled for depth and boss.</param>
    /// <param name="baseDamage">The wave's baseline damage, already scaled for depth (never for boss).</param>
    /// <param name="wave">Used only for armour growth.</param>
    /// <param name="sources">Drawn per creature, so a wave holds mixed matchups.</param>
    /// <param name="rng">Seeded by the caller — a replayed wave must be identical.</param>
    /// <param name="forceSingle">Bosses are always one creature; the archetype supplies the shape only.</param>
    public static List<WaveCreature> Compose(
        Archetype archetype,
        float baseHealth,
        float baseDamage,
        int wave,
        IReadOnlyList<Source>? sources,
        Random rng,
        bool forceSingle = false)
    {
        ArgumentNullException.ThrowIfNull(rng);

        var shape = Shape(archetype);
        var count = forceSingle
            ? 1
            : rng.Next(shape.MinCount, shape.MaxCount + 1);

        var defense = shape.DefenseBase * (1f + DefenseGrowthPerWave * Math.Max(0, wave));

        var made = new List<WaveCreature>(count);
        for (var i = 0; i < count; i++)
        {
            var src = sources is { Count: > 0 } ? sources[rng.Next(sources.Count)] : (Source?)null;
            made.Add(new WaveCreature
            {
                MaxHealth = baseHealth * shape.HealthMult,
                Health = baseHealth * shape.HealthMult,
                Damage = baseDamage * shape.DamageMult,
                Defense = defense,
                Source = src,
                Archetype = archetype,
            });
        }

        return made;
    }
}
