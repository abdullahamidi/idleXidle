using System.Collections.Generic;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Builds;

/// <summary>
/// What WORN GEAR adds to a build's <see cref="SkillShape"/>: today, the weapon family's favoured Forms
/// (<see cref="ItemFamilies.FavouredForms"/>) as <see cref="SkillShape.FormPower"/>.
/// </summary>
/// <remarks>
/// Folded in at the ONE place the sim takes the build's shape (<c>SoloBattle.ResolveWave</c>), behind
/// the same <c>skillForm</c> gate the character's aptitude uses — so it lifts woven skills of the
/// favoured Forms and never the auto-attack, and every consumer of the sim (the fight, DamageBench,
/// the WEAVE screen's dmg/s, EQUIP BEST's weapon ranking) sees it with no second wiring.
/// </remarks>
public static class GearShape
{
    /// <summary>The shape of what <paramref name="hunter"/> wears; <see cref="SkillShape.None"/> bare.</summary>
    public static SkillShape Of(Hunter? hunter)
    {
        if (hunter is null) return SkillShape.None;
        var forms = ItemFamilies.FavouredFormsOf(hunter.Worn(GearSlot.Weapon));
        if (forms.Count == 0) return SkillShape.None;
        var power = new Dictionary<Form, float>();
        foreach (var f in forms) power[f] = ItemFamilies.FormAffinity;
        return new SkillShape { FormPower = power };
    }
}
