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
    /// The SkillId a Form-era slot means, given the EFFECTIVE slot kind — i.e. after the composer's
    /// budget walk has decided which picks spill into passive slots. Null for a Form this game never
    /// had, which the caller drops like every other unknown catalogue name.
    /// </summary>
    public static string? Resolve(string? form, bool passive)
        => form is not null && Map.TryGetValue(form, out var pair)
            ? (passive ? pair.Passive : pair.Active)
            : null;
}
