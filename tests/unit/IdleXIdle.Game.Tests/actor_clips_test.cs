using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// WHICH CLIPS AN ACTOR CAN PLAY — the decision, without a GraphicsDevice.
/// </summary>
/// <remarks>
/// <para>
/// The candidate list decides ordering, the per-character fallback to a generic strip, what an unknown
/// clip does and how duplicates collapse. All of it is string and list work, but it lived inside a
/// screen property whose one impure step was "is this strip loaded?" — so the whole decision could be
/// exercised by a capture and by nothing else, and a capture cannot tell you WHY a figure was measured
/// the way it was (2026-09-08).
/// </para>
/// <para>
/// <see cref="ActorClips.ChampionStrips"/> takes that predicate as a parameter, so these tests state the
/// rules directly with a fake library. The drawing, the loading and the envelope stayed in the screen;
/// the one capture that proves the graphical integration is unchanged.
/// </para>
/// </remarks>
public class actor_clips_test
{
    private static readonly Character Seeker = CharacterRoster.Get("seeker")!;

    private static EquippedSkill Skill(string id) => new(SkillCatalogue.ById(id), Source.Body);

    /// <summary>An asset library that holds exactly these keys.</summary>
    private static Func<string, bool> Shipped(params string[] keys)
    {
        var set = new HashSet<string>(keys, StringComparer.Ordinal);
        return set.Contains;
    }

    private static List<string> Strips(Func<string, bool> available, params EquippedSkill[] skills)
        => (List<string>)ActorClips.ChampionStrips(Seeker, skills, available, new List<string>());

    [Fact]
    public void test_a_skill_with_its_own_authored_clip_lists_it_before_its_forms()
    {
        // HARD HANDS plays its own strip; BLOW, on the same Strike Form, plays the Form's
        var idle = Seeker.StripKeys("idle").First();
        var own = Seeker.StripKey("hard_hands");
        var strike = Seeker.StripKey("strike");

        var hands = Strips(Shipped(idle, own, strike), Skill("sig_seeker_hard_hands"));
        var blow = Strips(Shipped(idle, own, strike), Skill("hammer_blow"));

        Assert.Equal(new[] { idle, own, strike }, hands);
        Assert.Equal(new[] { idle, strike }, blow);
    }

    [Fact]
    public void test_the_idle_and_the_swing_come_first_and_in_that_order()
    {
        // The order is the order the draw resolves in, and the envelope is measured from this list, so
        // a reshuffle would silently move the figure's published body.
        var idle = Seeker.StripKeys("idle").First();
        var attack = Seeker.StripKeys("attack").First();

        var strips = Strips(Shipped(idle, attack));

        Assert.Equal(new[] { idle, attack }, strips);
    }

    [Fact]
    public void test_a_clip_with_no_art_contributes_nothing_rather_than_a_strip_the_draw_would_not_use()
    {
        // A missing clip must not fall through to some other character's strip or to a key the draw
        // would refuse — the list is "what this figure CAN play", and a lie here becomes a wrong body.
        var idle = Seeker.StripKeys("idle").First();

        var strips = Strips(Shipped(idle));

        Assert.Equal(new[] { idle }, strips);
    }

    [Fact]
    public void test_the_generic_strip_is_the_fallback_and_only_when_the_characters_own_is_missing()
    {
        // Most specific first: the champion's own strip for the clip, then the generic it stands in for.
        var blow = Skill("hammer_blow");
        var own = Seeker.StripKeys(blow.Def.ClipKey).First();
        var ladder = Seeker.StripKeys(blow.Def.ClipKey).ToList();
        Assert.True(ladder.Count > 1, "this clip has no generic to fall back to — pick another for this test");
        var generic = ladder[1];

        var idle = Seeker.StripKeys("idle").First();
        var attack = Seeker.StripKeys("attack").First();

        // Own art present: the skill contributes ITS OWN strip and the ladder stops there.
        var specific = Strips(Shipped(idle, attack, own, generic), blow);
        Assert.Equal(new[] { idle, attack, own }, specific);

        // Own art missing: the generic stands in, in the same slot.
        var fallenBack = Strips(Shipped(idle, attack, generic), blow);
        Assert.Contains(generic, fallenBack);
        Assert.DoesNotContain(own, fallenBack);
    }

    [Fact]
    public void test_an_unknown_clip_name_contributes_nothing_and_does_not_throw()
    {
        // A skill whose clip key names art nobody drew: it has no generic, so it adds nothing at all.
        var idle = Seeker.StripKeys("idle").First();
        var strips = ActorClips.ChampionStrips(
            Seeker,
            new List<EquippedSkill>(),
            key => key == idle || key.Contains("nonesuch", StringComparison.Ordinal),
            new List<string>());

        Assert.Equal(new[] { idle }, strips);
    }

    [Fact]
    public void test_two_skills_sharing_a_strip_are_listed_once()
    {
        // Duplicates would double-count in anything that walks the list, and two skills of one style
        // routinely share a clip.
        var idle = Seeker.StripKeys("idle").First();
        var attack = Seeker.StripKeys("attack").First();
        var a = Skill("hammer_blow");
        var b = Skill("hammer_press");
        var shared = Seeker.StripKeys(a.Def.ClipKey).First();
        Assert.Equal(a.Def.ClipKey, b.Def.ClipKey);   // the fixture's premise

        var strips = Strips(Shipped(idle, attack, shared), a, b);

        Assert.Equal(strips.Count, strips.Distinct().Count());
        Assert.Single(strips.Where(s => s == shared));
    }

    [Fact]
    public void test_a_legacy_reaction_loads_no_trap_clip()
    {
        // A REACTION NEVER TAKES THE FIGURE (the remaining-skill sweep, P0.4; this test asserted the opposite before):
        // a legacy Reaction used to be drawn as the trap it sets; now, like JAWS, it loads no clip at all
        // (hunt_generic_skill_test sweeps every champion).
        var reaction = SkillCatalogue.All.FirstOrDefault(d => d.Kind == SkillKind.Reaction
                                                              && Presentation.ReactionRecipes.For(Seeker.Id, d.Id) is null);
        Assert.True(reaction is not null, "no legacy Reaction in the catalogue — this rule has nothing to guard");
        var trap = Seeker.StripKeys("trap").First();
        var idle = Seeker.StripKeys("idle").First();

        var strips = Strips(Shipped(idle, trap), new EquippedSkill(reaction!, Source.Body));

        Assert.DoesNotContain(trap, strips);
    }

    [Fact]
    public void test_the_list_is_rebuilt_into_the_caller_s_own_scratch_list()
    {
        // The screen memoises the returned list, so the builder must clear and refill the SAME list
        // rather than hand back a new one — a fresh list each frame would leak the memo's identity.
        var idle = Seeker.StripKeys("idle").First();
        var scratch = new List<string> { "stale" };

        var strips = ActorClips.ChampionStrips(Seeker, Array.Empty<EquippedSkill>(), Shipped(idle), scratch);

        Assert.Same(scratch, strips);
        Assert.Equal(new[] { idle }, scratch);
    }
}
