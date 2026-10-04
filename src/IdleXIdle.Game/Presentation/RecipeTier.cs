using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>Which tier a recipe lookup resolved through (the remaining-skill sweep, design.md section 8).</summary>
public enum RecipeTierKind
{
    /// <summary>No recipe: the skill keeps its generic / legacy presentation (or the family is switched off).</summary>
    None,

    /// <summary>The champion's own entry, <c>BySkill[(champion, skill)]</c>: the Seeker's closed references.</summary>
    Own,

    /// <summary>
    /// The champion-agnostic tier, <c>ByAnySkill[skill]</c>: a closed reference reused byte-identical on another champion.
    /// The only place it shows the slot's Source is its NUMBER (<see cref="NumberOutline"/>).
    /// </summary>
    Agnostic,
}

/// <summary>The recipe families that carry a champion-agnostic tier for the closed references.</summary>
public enum RecipeFamily
{
    /// <summary><see cref="ReactionRecipes"/> (JAWS).</summary>
    Reaction,

    /// <summary><see cref="FieldRecipes"/> (PRESS).</summary>
    Field,

    /// <summary><see cref="MarkRecipes"/> (BRAND).</summary>
    Mark,
}

/// <summary>
/// THE CHAMPION-AGNOSTIC TIER (design.md section 8, "Closed references on other champions"): a closed reference is the
/// SKILL's identity, not the Seeker's, so JAWS, PRESS and BRAND resolve on every champion to the SAME recipe instance,
/// with no Source parameter, no colour table and no cue change. The lookup order is the champion's own entry first, then
/// the skill's agnostic entry; every family keeps its own switch (RH_ACTION_RECIPES, RH_*_RECIPES).
/// </summary>
public static class RecipeTier
{
    /// <summary>
    /// The tier <paramref name="skillId"/> resolves through for <paramref name="characterId"/> in <paramref name="family"/>,
    /// with the family's switches applied (a family switched off is <see cref="RecipeTierKind.None"/>). Pure.
    /// </summary>
    public static RecipeTierKind Of(RecipeFamily family, string characterId, string skillId) => family switch
    {
        RecipeFamily.Reaction => ReactionRecipes.TierOf(characterId, skillId),
        RecipeFamily.Field => FieldRecipes.TierOf(characterId, skillId),
        _ => MarkRecipes.TierOf(characterId, skillId),
    };

    /// <summary>
    /// The shared two-tier lookup: <paramref name="bySkill"/> (champion + skill) first, then <paramref name="byAnySkill"/>
    /// (skill alone). <paramref name="enabled"/> false resolves nothing. Allocation-free.
    /// </summary>
    internal static T? Resolve<T>(bool enabled, IReadOnlyDictionary<(string Character, string Skill), T> bySkill,
                                  IReadOnlyDictionary<string, T> byAnySkill, string characterId, string skillId,
                                  out RecipeTierKind tier) where T : class
    {
        tier = RecipeTierKind.None;
        if (!enabled) return null;
        if (bySkill.TryGetValue((characterId, skillId), out var own)) { tier = RecipeTierKind.Own; return own; }
        if (byAnySkill.TryGetValue(skillId, out var any)) { tier = RecipeTierKind.Agnostic; return any; }
        return null;
    }
}

/// <summary>One pass of a number's glyphs at a position and ink (the screen's text draw, a struct so nothing boxes).</summary>
public interface IGlyphPass
{
    /// <summary>Draw the number's glyphs with their top-left at (<paramref name="x"/>, <paramref name="y"/>) in <paramref name="ink"/>.</summary>
    void Glyphs(int x, int y, Color ink);
}

/// <summary>
/// THE SOURCE IN THE NUMBER (design.md section 8): a one-pixel, four-way outline under a damage number's glyphs, in the
/// slot's Source light. An agnostic closed reference (JAWS / PRESS / BRAND on a champion other than the Seeker) shows its
/// slot's Source here and nowhere else; the Seeker's own JAWS / PRESS / BRAND numbers are closed pictures and carry none.
/// The same outline is what FIRST BEAT and the EMPOWERED-HIT accent use.
/// </summary>
public static class NumberOutline
{
    /// <summary>
    /// The outline a number resolved through <paramref name="tier"/> takes: <paramref name="sourceGlow"/> on the
    /// <see cref="RecipeTierKind.Agnostic"/> tier only, otherwise none.
    /// </summary>
    public static Color? For(RecipeTierKind tier, Color sourceGlow) => tier == RecipeTierKind.Agnostic ? sourceGlow : null;

    /// <summary>
    /// Draw the glyphs once per neighbour (left, right, up, down) in <paramref name="outline"/>, then once at
    /// (<paramref name="x"/>, <paramref name="y"/>) in <paramref name="ink"/> on top. Five passes, no allocation.
    /// </summary>
    public static void Draw<TPass>(ref TPass pass, int x, int y, Color outline, Color ink) where TPass : struct, IGlyphPass
    {
        pass.Glyphs(x - 1, y, outline);
        pass.Glyphs(x + 1, y, outline);
        pass.Glyphs(x, y - 1, outline);
        pass.Glyphs(x, y + 1, outline);
        pass.Glyphs(x, y, ink);
    }

    /// <summary>
    /// The outline WITH A DARK HALO (the Phase 1 review): eight passes in <paramref name="halo"/> one pixel beyond the
    /// colour stroke (two pixels on the axes, one on the diagonals), then <see cref="Draw{TPass}"/>. A colour stroke alone
    /// vanishes when it matches the ink (FIRST BEAT's white on white skill-grade ink read only as a bolder glyph); the halo
    /// separates any accent from any ink, the way the Shadow outline already separated itself from white. Thirteen
    /// passes, no allocation.
    /// </summary>
    public static void DrawHaloed<TPass>(ref TPass pass, int x, int y, Color outline, Color ink, Color halo) where TPass : struct, IGlyphPass
    {
        pass.Glyphs(x - 2, y, halo);
        pass.Glyphs(x + 2, y, halo);
        pass.Glyphs(x, y - 2, halo);
        pass.Glyphs(x, y + 2, halo);
        pass.Glyphs(x - 1, y - 1, halo);
        pass.Glyphs(x + 1, y - 1, halo);
        pass.Glyphs(x - 1, y + 1, halo);
        pass.Glyphs(x + 1, y + 1, halo);
        Draw(ref pass, x, y, outline, ink);
    }

    /// <summary>The outline's colour as RRGGBB, for the presentation trace (<c>outline=</c>).</summary>
    public static string Hex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";
}
