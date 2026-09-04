using IdleXIdle.Core.Builds;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// The attunement ring: how hard a skill hits given the STYLE this hunter has specialised in.
/// </summary>
/// <remarks>
/// <para>
/// The port of the old Form hexagon onto the Style ring (2026-08-31, P3c). The SHAPE is identical —
/// your own discipline ×2, neighbours ×1.15, off ×0.75, the opposite ×0.45, and a sworn Vow pulls an
/// off-discipline skill one ring closer — but the OPPOSITES are the designed ones at last:
/// HAMMER↔VOLLEY, SNARE↔FIELD, SIGN↔DRAIN (<see cref="Style"/>'s enum order is the ring). The Form
/// ring contradicted the tree it was supposed to describe; this one is the tree's own geometry, and
/// swapping them is a measured balance change, not a rename.
/// </para>
/// <para>
/// The opposite is weakened, never zeroed: a Hammer adept CAN still carry a Volley skill, it just
/// hurts. A zero would make the choice a lock instead of a lean.
/// </para>
/// </remarks>
public static class StyleAffinity
{
    /// <summary>The multiplier on a skill of <paramref name="skill"/>'s style for an adept of <paramref name="affinity"/>.</summary>
    public static float Factor(Style affinity, Style skill)
        => FactorAtDistance(SkillCatalogue.RingDistance(affinity, skill));

    /// <summary>
    /// The factor WITH the buy-back: while the build is KEEPING a vow, every off-discipline skill is
    /// pulled one ring toward the discipline. Straight from the system this game was born from — a
    /// restriction is how you wield what your affinity does not give you.
    /// </summary>
    /// <remarks>
    /// A vow is a promise about the whole BUILD, so the question is asked of the build once
    /// (<see cref="Vows.AnyKept"/>) and answered the same way for every skill in it. The native style
    /// gains nothing — you already own it — and a build keeping no promise pays the full distance.
    /// </remarks>
    public static float Factor(Style affinity, Style skill, bool vowKept)
    {
        var dist = SkillCatalogue.RingDistance(affinity, skill);
        if (vowKept && dist > 0) dist -= 1;
        return FactorAtDistance(dist);
    }

    // A SHARPER class: committing leans in hard and the far styles fight you. It stays a lean, not a
    // lock — the opposite is still castable for a splash. Your discipline is WHAT you are best at;
    // the woven skills and keystones are HOW you build within it.
    private static float FactorAtDistance(int dist) => dist switch
    {
        0 => 2.00f,   // your mastery — you are a specialist now, and it shows
        1 => 1.15f,   // adjacent — the comfortable neighbours
        2 => 0.75f,   // off-affinity — a real cost
        _ => 0.45f,   // the opposite style — usable for a splash, but it truly fights you
    };
}
