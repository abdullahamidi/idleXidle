using System;
using System.Collections.Generic;

namespace IdleXIdle.Core.Persistence;

/// <summary>
/// THE Form-era migration seam: maps a pre-v3 saved skill — a Form name plus the slot kind it
/// resolved into — onto the <c>SkillId</c> that build has always meant.
/// </summary>
/// <remarks>
/// <para>
/// Until save version 3 a woven skill's only persisted identity was <c>(Form, Passive)</c>, and the
/// runtime round-tripped it through <c>SkillCatalogue.Resolve(Form, bool)</c> on every read. Version
/// 3 makes <c>SavedSkill.SkillId</c> the identity; this table is where the old vocabulary goes to be
/// understood ONE more time — at restore — and nowhere else.
/// </para>
/// <para>
/// Deliberately its own hard-coded table rather than a call into <c>SkillCatalogue</c>'s bridge:
/// the refactor deletes <c>SkillDef.LegacyForm</c> and <c>Resolve(Form, bool)</c> from runtime, and
/// a migration that leaned on them would have kept them alive forever. These six rows are frozen
/// history, like <c>LegacyUnlocks</c> — they must never change again, whatever the catalogue does.
/// </para>
/// </remarks>
public static class LegacySkillForm
{
    /// <summary>Form name → (the style's active skill, the style's passive skill).</summary>
    private static readonly IReadOnlyDictionary<string, (string Active, string Passive)> Map =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["Strike"] = ("hammer_blow", "hammer_press"),
            ["Trap"] = ("snare_repay", "snare_jaws"),
            ["Mark"] = ("sign_call", "sign_brand"),
            ["Projectile"] = ("volley_spray", "volley_weep"),
            ["Aura"] = ("field_pulse", "field_mire"),
            ["Transformation"] = ("drain_drink", "drain_wilt"),
        };

    /// <summary>
    /// The Forms that never took a beat. A pre-slot-rework save carries no <c>Passive</c> flag, and
    /// these two always resolved to their style's passive skill whichever slot they sat in.
    /// </summary>
    private static readonly HashSet<string> NaturallyPassive = new(StringComparer.Ordinal) { "Aura", "Trap" };

    /// <summary>Was this Form passive by nature, before the slot ever recorded a choice?</summary>
    public static bool IsNaturallyPassive(string? form) => form is not null && NaturallyPassive.Contains(form);

    /// <summary>
    /// The SkillId a Form-era slot means, given the EFFECTIVE slot kind — i.e. after the frozen
    /// budget walk has decided which picks spill into passive slots. Null for a Form this game never
    /// had, which the caller drops like every other unknown catalogue name.
    /// </summary>
    public static string? Resolve(string? form, bool passive)
        => form is not null && Map.TryGetValue(form, out var pair)
            ? (passive ? pair.Passive : pair.Active)
            : null;

    /// <summary>The six skills that never take a beat — frozen ids, for the walk below.</summary>
    private static readonly HashSet<string> PassiveSkills = new(StringComparer.Ordinal)
        { "hammer_press", "snare_jaws", "sign_brand", "volley_weep", "field_mire", "drain_wilt" };

    /// <summary>
    /// The composer's slot-kind walk AS IT STOOD the day the Form vocabulary was retired — frozen,
    /// like the map above. <c>PlayerLoadout.Restore</c> uses it to decide which of a style's two
    /// skills each pre-v3 row migrates onto; the LIVE walk (<c>BuildComposer.SlotKinds</c>) is free
    /// to evolve without rewriting anyone's save a second time.
    /// </summary>
    public static IReadOnlyList<bool> SlotKinds(
        IReadOnlyList<(string? Form, bool? Passive, string? SkillId)> rows, int capacity)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var activeBudget = (Math.Max(1, capacity) + 1) / 2;   // Build.ActiveSlotsFor, frozen
        var passive = new bool[rows.Count];
        var actives = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            // A named skill brings its own kind and counts its own budget — exactly what the live
            // walk did for a v3 row on the freeze date.
            if (rows[i].SkillId is { } id)
            {
                if (PassiveSkills.Contains(id)) { passive[i] = true; continue; }
                actives++;
                continue;
            }
            if (rows[i].Passive is { } chosen)
            {
                if (chosen) { passive[i] = true; continue; }
                if (actives < activeBudget) { actives++; continue; }
                passive[i] = true;   // chosen active, but the budget is spent: it spills
                continue;
            }
            if (IsNaturallyPassive(rows[i].Form)) { passive[i] = true; continue; }
            if (actives < activeBudget) { actives++; continue; }
            passive[i] = true;
        }
        return passive;
    }
}
