using System;
using System.Collections.Generic;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;

namespace IdleXIdle.Game;

/// <summary>
/// WHICH CLIPS AN ACTOR CAN BE DRAWN IN — the decision alone, with asset existence injected.
/// </summary>
/// <remarks>
/// <para>
/// The list decides ordering, the per-character fallback to a generic strip, what an unknown clip does,
/// and duplicate suppression — all pure string and list work over Core types. It lived inside a screen
/// property whose only impure step was a one-line "is this strip loaded?" predicate, which meant the
/// whole decision could be exercised by a capture and by nothing else (2026-09-08).
/// </para>
/// <para>
/// The predicate is a parameter now, so the decision is testable with no GraphicsDevice. Everything
/// that touches a texture — loading, measuring, drawing, the envelope — stays in the screen that owns
/// it. This type draws nothing.
/// </para>
/// </remarks>
public static class ActorClips
{
    /// <summary>
    /// Every strip the champion can play this wave, most specific first and without duplicates: the
    /// idle, the basic swing, then one per equipped skill (a Reaction commits to the trap clip), and a skill's own
    /// authored clip before its Form's (ADR-011).
    /// </summary>
    /// <param name="who">The champion, which owns the clip-to-strip-key ladder.</param>
    /// <param name="skills">The skills woven for this wave, in rail order.</param>
    /// <param name="available">Is this strip key drawable? The screen passes its asset library's answer.</param>
    /// <param name="into">Scratch list, cleared and filled, returned so the caller can memo it.</param>
    /// <remarks>
    /// A clip whose art is missing contributes NOTHING rather than falling through to a strip the draw
    /// would not use — and an unknown clip name has no generic to fall back to, so it contributes
    /// nothing either. Both are silent by design: the figure is measured from what it CAN play.
    /// </remarks>
    public static IReadOnlyList<string> ChampionStrips(
        Character who, IReadOnlyList<EquippedSkill> skills, Func<string, bool> available, List<string> into)
    {
        ArgumentNullException.ThrowIfNull(who);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(into);

        into.Clear();
        Add("idle");
        Add("attack");
        foreach (var equipped in skills)
        {
            var def = equipped.Def;
            if (def.Kind == SkillKind.Reaction) { Add("trap"); continue; }
            // a skill with its OWN authored clip (HARD HANDS' hard_hands) plays it; its Form's is the fallback
            if (Presentation.ActionRecipes.For(who.Id, def.Id, def.ClipKey, def.FxKey) is { } recipe && recipe.ClipKey != def.ClipKey)
                Add(recipe.ClipKey);
            Add(def.ClipKey);
        }
        return into;

        void Add(string clip)
        {
            foreach (var key in who.StripKeys(clip))
                if (available(key))
                {
                    if (!into.Contains(key)) into.Add(key);
                    return;
                }
        }
    }
}
