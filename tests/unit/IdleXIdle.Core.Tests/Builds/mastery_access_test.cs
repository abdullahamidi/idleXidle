using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// ACCESS IS TEMPORARY. EXPERIENCE IS PERMANENT.
/// </summary>
/// <remarks>
/// <para>
/// This replaces <c>mastery_learned_permanence_test.cs</c>, which asserted the opposite rule: that a
/// skill discovered through mastery stayed usable for ever, so respec returned the points and never
/// the skill. That made every skill-unlock node effectively free — buy it once, refund it, keep it —
/// and it is the exploit the systems refactor exists to close (BRIEF sec.15-17, LAWS 3 and 4).
/// </para>
/// <para>
/// The old rule was not laziness; it was load-bearing. Only one specialisation may be taken at a
/// time and every skill road hung off one, so permanence-through-respec was the only way a four-slot
/// build ever filled from twelve skills. Removing it therefore required moving the roads off the
/// specialisations (PLAN decision D11) — which is why <see cref="test_a_hunter_can_reach_more_skills_than_it_has_slots"/>
/// is in this file rather than in the tree's own. Without it, this rule makes the game unplayable.
/// </para>
/// </remarks>
public class MasteryAccessTest
{
    /// <summary>Walk the prerequisite chain and take the node — the way a player reaches it.</summary>
    private static void TakeWithPrereqs(MasteryTree t, string id)
    {
        var n = MasteryCatalog.ById(id);
        Assert.NotNull(n);
        if (n!.Prereqs.Count > 0 && !n.Prereqs.Any(t.IsTaken)) TakeWithPrereqs(t, n.Prereqs[0]);
        if (n.SecondPrereqs.Count > 0 && !n.SecondPrereqs.Any(t.IsTaken)) TakeWithPrereqs(t, n.SecondPrereqs[0]);
        Assert.True(t.Take(id), $"could not take {id}");
    }

    // ── The brief's own eight steps (sec.99), in order ────────────────────────────────────────────

    [Fact]
    public void test_allocating_the_node_makes_the_skill_available()
    {
        var t = new MasteryTree();
        t.SetEarned(60);

        Assert.DoesNotContain("hammer_blow", t.AvailableSkills());
        TakeWithPrereqs(t, "road_hammer");
        Assert.Contains("hammer_blow", t.AvailableSkills());
    }

    [Fact]
    public void test_respec_takes_the_access_away()
    {
        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");

        t.Respec();

        Assert.Equal(0, t.Spent);
        Assert.DoesNotContain("hammer_blow", t.AvailableSkills());
    }

    [Fact]
    public void test_refunding_the_one_node_takes_the_access_away()
    {
        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");

        Assert.True(t.Refund("road_hammer"));

        Assert.DoesNotContain("hammer_blow", t.AvailableSkills());
    }

    [Fact]
    public void test_reallocating_the_node_gives_the_access_back()
    {
        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");
        t.Respec();

        TakeWithPrereqs(t, "road_hammer");

        Assert.Contains("hammer_blow", t.AvailableSkills());
    }

    [Fact]
    public void test_experience_is_untouched_by_losing_and_regaining_access()
    {
        // The whole point of the rule: the tree owns ACCESS and never owns EXPERIENCE. SkillProgress
        // is keyed by skill id and knows nothing about mastery, which is what makes LAW 4 structural
        // rather than a promise somebody has to keep.
        var progress = new SkillProgress();
        for (var i = 0; i < 400; i++) progress.RecordWave("hammer_blow");
        var earned = progress.LevelOf("hammer_blow");
        Assert.True(earned >= 1, "the fixture must earn at least one level for this test to mean anything");

        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");
        t.Respec();

        Assert.DoesNotContain("hammer_blow", t.AvailableSkills());
        Assert.Equal(earned, progress.LevelOf("hammer_blow"));

        TakeWithPrereqs(t, "road_hammer");
        Assert.Contains("hammer_blow", t.AvailableSkills());
        Assert.Equal(earned, progress.LevelOf("hammer_blow"));
    }

    // ── What the composer must do with a slot the tree no longer allows ──────────────────────────

    [Fact]
    public void test_the_composer_drops_a_skill_whose_node_is_gone()
    {
        var t = new MasteryTree();
        t.SetEarned(60);
        TakeWithPrereqs(t, "road_hammer");

        var loadout = new PlayerLoadout { SkillCapacity = 1 };
        loadout.SetSkill(loadout.AddSkill(), "hammer_blow");
        loadout.SetSource(0, Source.Body);

        Assert.Single(loadout.ToBuild(new MemoryDustTree(), t, character: null).Skills);

        t.Respec();

        // The slot keeps its choice — the player's loadout is not silently rewritten by a respec —
        // but the FIGHT carries only what mastery currently allows.
        Assert.Empty(loadout.ToBuild(new MemoryDustTree(), t, character: null).Skills);
    }

    // ── The reason D11 had to happen (see the class remarks) ─────────────────────────────────────

    [Fact]
    public void test_a_hunter_can_reach_more_skills_than_it_has_slots()
    {
        // Four slots, and a career's worth of points. If a hunter cannot reach at least five skills
        // at once, "which shared techniques am I investing into right now" is not a question — the
        // answer is always "the only two I can see".
        var t = new MasteryTree();
        t.SetEarned(66);   // MasteryPoints: about what six regions at depth 150 yields

        foreach (var road in new[] { "road_hammer", "road_snare", "road_volley", "road_field", "road_sign" })
            TakeWithPrereqs(t, road);

        Assert.True(t.AvailableSkills().Count >= 5,
                    $"a full career reaches {t.AvailableSkills().Count} skills; four slots need more than that to be a choice");
        Assert.True(t.Spent <= t.Earned, $"spent {t.Spent} of {t.Earned}");
    }

    [Fact]
    public void test_a_skill_road_no_longer_needs_its_specialisation()
    {
        // D11: the road hangs off its branch's own first minor, not off the one specialisation a
        // hunter is allowed. Two roads of DIFFERENT specialisations, at the same time, on one tree.
        var t = new MasteryTree();
        t.SetEarned(60);

        TakeWithPrereqs(t, "road_hammer");    // was gated on spec_strike
        TakeWithPrereqs(t, "road_volley");    // was gated on spec_projectile

        Assert.Contains("hammer_blow", t.AvailableSkills());
        Assert.Contains("volley_spray", t.AvailableSkills());
        Assert.Null(t.Affinity());            // and neither specialisation was needed to get there
    }

    [Fact]
    public void test_one_specialisation_per_hunter_is_unchanged()
    {
        // D11 moved the roads off the specialisations. It did not touch what a specialisation IS:
        // the affinity ring, its trigger, and one per hunter.
        var t = new MasteryTree();
        t.SetEarned(80);

        TakeWithPrereqs(t, "spec_strike");
        Assert.NotNull(t.Affinity());
        Assert.False(t.CanTake("spec_projectile"), "a second specialisation must still be refused");
    }
}
