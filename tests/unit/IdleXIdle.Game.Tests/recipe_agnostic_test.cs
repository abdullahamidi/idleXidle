using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE CHAMPION-AGNOSTIC TIER (the remaining-skill sweep, design.md section 8, "Closed references on other champions"):
/// JAWS, PRESS and BRAND are their SKILLS' identities, so every champion resolves them to the SAME recipe instance (no
/// Source parameter, no colour table, no cue change); the Seeker's five reference pairs resolve exactly as before;
/// HARD HANDS stays the Seeker's; the action tier is empty in Phase 1.
/// </summary>
public class recipe_agnostic_test
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static IActionRecipe? Action(string champion, string skillId)
    {
        var def = SkillCatalogue.ById(skillId);
        return ActionRecipes.For(champion, def.Id, def.ClipKey, def.FxKey);
    }

    public static IEnumerable<object[]> Champions => CharacterRoster.All.Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(Champions))]
    public void agnostic_recipes_resolve_on_every_champion_test(string champion)
    {
        // the SAME instances, on every champion: the closed pictures, byte-identical by construction
        Assert.Same(ReactionRecipes.SeekerJaws, ReactionRecipes.For(champion, "snare_jaws"));
        Assert.Same(FieldRecipes.SeekerPress, FieldRecipes.For(champion, "hammer_press"));
        Assert.Same(MarkRecipes.SeekerBrand, MarkRecipes.For(champion, "sign_brand"));
        // ...and each in its own family only
        Assert.Null(FieldRecipes.For(champion, "snare_jaws"));
        Assert.Null(MarkRecipes.For(champion, "hammer_press"));
        Assert.Null(FieldRecipes.For(champion, "sign_brand"));
        Assert.Null(ReactionRecipes.For(champion, "sign_brand"));
        // the tier says which: the Seeker's own, everyone else's through the agnostic tier
        var expected = champion == "seeker" ? RecipeTierKind.Own : RecipeTierKind.Agnostic;
        Assert.Equal(expected, RecipeTier.Of(RecipeFamily.Reaction, champion, "snare_jaws"));
        Assert.Equal(expected, RecipeTier.Of(RecipeFamily.Field, champion, "hammer_press"));
        Assert.Equal(expected, RecipeTier.Of(RecipeFamily.Mark, champion, "sign_brand"));
    }

    [Fact]
    public void test_the_seekers_five_pairs_resolve_unchanged()
    {
        Assert.Same(ActionRecipes.SeekerSpray, Action("seeker", "volley_spray"));                // via ByForm
        Assert.Same(ActionRecipes.SeekerHardHands, Action("seeker", "sig_seeker_hard_hands"));   // via BySkill
        Assert.Same(ReactionRecipes.SeekerJaws, ReactionRecipes.For("seeker", "snare_jaws"));
        Assert.Same(FieldRecipes.SeekerPress, FieldRecipes.For("seeker", "hammer_press"));
        Assert.Same(MarkRecipes.SeekerBrand, MarkRecipes.For("seeker", "sign_brand"));
        Assert.Equal(RecipeTierKind.Own, ReactionRecipes.TierOf("seeker", "snare_jaws"));
        Assert.Equal(RecipeTierKind.Own, FieldRecipes.TierOf("seeker", "hammer_press"));
        Assert.Equal(RecipeTierKind.Own, MarkRecipes.TierOf("seeker", "sign_brand"));
    }

    [Theory]
    [MemberData(nameof(Champions))]
    public void test_hard_hands_and_spray_stay_the_seekers_in_phase_one(string champion)
    {
        if (champion == "seeker") return;
        Assert.Null(Action(champion, "sig_seeker_hard_hands"));   // HARD HANDS: Seeker-only, no agnostic entry
        Assert.Null(Action(champion, "volley_spray"));            // SPRAY-agnostic waits for Phase 4's strips and missiles
    }

    [Fact]
    public void test_the_action_tier_exists_and_is_empty_in_phase_one()
    {
        Assert.Equal(0, ActionRecipes.AgnosticCount);
        // IN ITS ORDER: BySkill -> ByAnySkill -> ByForm (the Phase 4 hazard: the Seeker's SPRAY needs a BySkill entry
        // before ByAnySkill["volley_spray"] is added, or it would resolve to the agnostic recipe)
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ActionRecipe.cs"));
        var own = src.IndexOf("BySkill.TryGetValue((characterId, skillId), out var own)", StringComparison.Ordinal);
        var any = src.IndexOf("ByAnySkill.TryGetValue(skillId, out var any)", StringComparison.Ordinal);
        var form = src.IndexOf("ByForm.TryGetValue((characterId, clipKey), out var shared)", StringComparison.Ordinal);
        Assert.True(own > 0 && own < any && any < form, "the action lookup order is BySkill -> ByAnySkill -> ByForm");
        Assert.Contains("PHASE 4 HAZARD", src);
    }

    [Theory]
    [MemberData(nameof(Champions))]
    public void test_repay_weep_call_and_the_signatures_still_resolve_to_null(string champion)
    {
        foreach (var id in new[] { "snare_repay", "volley_weep", "sign_call" })
        {
            Assert.Null(ReactionRecipes.For(champion, id));
            Assert.Null(FieldRecipes.For(champion, id));
            Assert.Null(MarkRecipes.For(champion, id));
            Assert.Equal(RecipeTierKind.None, RecipeTier.Of(RecipeFamily.Reaction, champion, id));
            Assert.Equal(RecipeTierKind.None, RecipeTier.Of(RecipeFamily.Field, champion, id));
            Assert.Equal(RecipeTierKind.None, RecipeTier.Of(RecipeFamily.Mark, champion, id));
        }
        foreach (var sig in SkillCatalogue.All.Where(d => d.Id.StartsWith("sig_", StringComparison.Ordinal)))
        {
            Assert.Null(ReactionRecipes.For(champion, sig.Id));
            Assert.Null(FieldRecipes.For(champion, sig.Id));
            Assert.Null(MarkRecipes.For(champion, sig.Id));
            // a signature is only ever woven by its own champion; there, only HARD HANDS has an action recipe
            if (sig.OwnerCharacterId == champion && sig.Id != "sig_seeker_hard_hands") Assert.Null(Action(champion, sig.Id));
        }
    }

    [Fact]
    public void test_a_switched_off_family_resolves_nothing_on_either_tier()
    {
        // RH_ACTION_RECIPES / RH_*_RECIPES are read once per process, so the switch is proven on the shared lookup itself...
        var own = new Dictionary<(string Character, string Skill), string> { [("seeker", "snare_jaws")] = "own" };
        var any = new Dictionary<string, string> { ["snare_jaws"] = "any" };
        Assert.Null(RecipeTier.Resolve(false, own, any, "seeker", "snare_jaws", out var offOwn));
        Assert.Equal(RecipeTierKind.None, offOwn);
        Assert.Null(RecipeTier.Resolve(false, own, any, "anvil", "snare_jaws", out var offAny));
        Assert.Equal(RecipeTierKind.None, offAny);
        Assert.Equal("own", RecipeTier.Resolve(true, own, any, "seeker", "snare_jaws", out var onOwn));
        Assert.Equal(RecipeTierKind.Own, onOwn);
        Assert.Equal("any", RecipeTier.Resolve(true, own, any, "anvil", "snare_jaws", out var onAny));
        Assert.Equal(RecipeTierKind.Agnostic, onAny);
        // ...and every family hands it both of its switches, for For and TierOf alike
        foreach (var (file, flag) in new[] { ("ReactionRecipe.cs", "RH_REACTION_RECIPES"), ("FieldRecipe.cs", "RH_FIELD_RECIPES"),
                                            ("MarkRecipe.cs", "RH_MARK_RECIPES") })
        {
            var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", file));
            Assert.Contains(flag, src);
            Assert.Equal(2, CountOf(src, "RecipeTier.Resolve(ActionRecipes.Enabled && Enabled, BySkill, ByAnySkill, characterId, skillId"));
        }
    }

    [Fact]
    public void test_a_chorus_with_grave_song_and_press_performs_press_and_holds_grave_song()
    {
        var skills = new List<EquippedSkill>
        {
            new(SkillCatalogue.ById("sig_chorus_grave_song"), Source.Body),
            new(SkillCatalogue.ById("hammer_press"), Source.Body),
        };
        var roles = FieldRoles.Choose(skills, "chorus");
        Assert.Equal(1, roles.Performed);   // PRESS, through the agnostic tier
        Assert.Equal(-1, roles.Mark);
        Assert.Equal(0, roles.Held);        // GRAVE SONG keeps the generic held aura
    }

    private static int CountOf(string text, string needle)
    {
        var n = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
