using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Core.Tests;

/// <summary>
/// Fixture shorthand for the P3-final <see cref="EquippedSkill"/> — a RESOLVED def, an element,
/// a Vow. The Form-era fixture idiom (<c>new WovenAbility { … }</c> wrapped in an EquippedSkill
/// with a cooldown) died with the Form enum; every test that weaves a skill goes through here so
/// the next shape change is one file's problem.
/// </summary>
public static class TestBuilds
{
    /// <summary>An equipped skill by catalogue id, optionally tweaked the way a variation would be.</summary>
    public static EquippedSkill Skill(string id, Source source = Source.Body, Vow? vow = null,
                                      Func<SkillDef, SkillDef>? tweak = null)
    {
        var def = SkillCatalogue.ById(id);
        return new EquippedSkill(tweak is null ? def : tweak(def), source, vow);
    }

    /// <summary>
    /// The resolved def a chosen variation (and any of its reinforcements) produces — the same
    /// arithmetic <c>BuildComposer.Compose</c> runs, so a test states the content, not a copy of it.
    /// </summary>
    public static SkillDef Resolved(string id, string variation, params string[] reinforcements)
    {
        var def = SkillCatalogue.ById(id);
        var v = def.Variations.Single(x => x.Name == variation);
        var resolved = v.Modify is { } m ? m(def) : def;
        foreach (var name in reinforcements)
        {
            var r = v.Reinforcements.Single(x => x.Name == name);
            if (r.Modify is { } rm) resolved = rm(resolved);
        }
        return resolved;
    }

    /// <summary>The element a variation commits its skill to — for fixtures that weave it.</summary>
    public static Source SourceOf(string id, string variation)
        => SkillCatalogue.ById(id).Variations.Single(x => x.Name == variation).Source;

    /// <summary>A build carrying the named skills, in slot order, at their base line.</summary>
    public static Build Of(params string[] ids)
    {
        var b = new Build();
        foreach (var id in ids) b.Equip(Skill(id));
        return b;
    }
}
