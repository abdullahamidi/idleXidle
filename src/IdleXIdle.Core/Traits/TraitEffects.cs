using System;
using System.Collections.Generic;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Core.Traits;

/// <summary>
/// Turns the three traits a champion is wearing into ONE <see cref="SkillShape"/> contribution.
/// </summary>
/// <remarks>
/// <para>
/// <b>A trait is a shape contribution, exactly like a set rung.</b> <c>ElementSets.Catalogue</c>
/// already proves the pattern: a small authored record carrying a shape, folded through
/// <see cref="SkillShape.Combine"/>. Traits use the same seam, so there is no second engine, no
/// trait-specific branch in the fight, and nothing to register.
/// </para>
/// <para>
/// <b>World-conditional traits are resolved HERE, never inside the fight.</b> Four of the twenty-six
/// read the region, the worn set or the last three descents; each one is a lambda taking a
/// <see cref="TraitContext"/> and returning dials. The fight receives a plain number and cannot tell
/// where it came from — which is why <c>SoloBattle</c> never learns about regions, sets or the run log.
/// </para>
/// <para>
/// <b>An unequipped trait is invisible.</b> Every dial is neutral at its default, so a champion
/// wearing nothing composes byte-identically to a build from before traits existed. That is the same
/// house rule <c>SkillShape.None</c> obeys, and it is what makes the liveness suite's with/without
/// comparison mean anything.
/// </para>
/// </remarks>
public static class TraitEffects
{
    /// <summary>
    /// The shape the worn traits contribute. Unknown ids are ignored; there is no cap here because
    /// <see cref="TraitLedger"/> already holds one.
    /// </summary>
    public static SkillShape Compose(IEnumerable<string>? traitIds, TraitContext context)
    {
        if (traitIds is null) return SkillShape.None;

        var shape = SkillShape.None;
        var any = false;
        foreach (var id in traitIds)
        {
            if (TraitCatalogue.Find(id) is not { } def) continue;
            shape = any ? SkillShape.Combine(shape, def.Shape(context)) : def.Shape(context);
            any = true;
        }
        return shape;
    }
}
