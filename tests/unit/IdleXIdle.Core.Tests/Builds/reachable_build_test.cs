using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE BENCHMARK REACHABILITY LAW — a progression benchmark may only tune against a build the
/// shipped loadout model can produce. <see cref="Reachable"/> is the one construction path, and
/// these pin what it refuses and what it guarantees.
/// </summary>
public class reachable_build_test
{
    private static Character Seeker => CharacterRoster.Get("seeker");
    private const string Signature = "sig_seeker_hard_hands";

    private static Reachable.Slot S(string id, string? variation = null, string? vow = null) => new(id, variation, vow);

    [Fact]
    public void test_reachable_a_chosen_pair_composes_exactly_as_asked_with_each_source_from_its_variation()
    {
        // Arrange / Act
        var build = Reachable.Compose(Seeker, new[] { S(Signature, "OPEN HAND"), S("hammer_blow", "FLATTEN") }, slotCapacity: 4);

        // Assert — the slots, in order; the variations; and the Source is the variation's, not a pick's.
        Assert.Equal(new[] { Signature, "hammer_blow" }, build.Skills.Select(s => s.Def.Id));
        Assert.Equal(new[] { "OPEN HAND", "FLATTEN" }, build.Skills.Select(s => s.Variation?.Name));
        Assert.All(build.Skills, s => Assert.Equal(s.Variation!.Source, s.Source));
        Assert.Equal(Source.Body, build.PureSource);
    }

    [Fact]
    public void test_reachable_an_unchosen_slot_composes_on_the_seed_with_no_variation()
    {
        // The starter's shape: the signature alone, nothing chosen. Representable, and not a decision.
        var build = Reachable.Compose(Seeker, new[] { S(Signature) });

        Assert.Single(build.Skills);
        Assert.Null(build.Skills[0].Variation);
        Assert.Null(build.ChosenSingleSource);
    }

    [Fact]
    public void test_reachable_four_variations_to_four_sources_is_a_four_source_build_the_player_can_make()
    {
        // Two actives and two passives, which is the slot split a four-slot account actually has.
        var build = Reachable.Compose(Seeker, new[]
        {
            S("hammer_blow", "FLATTEN"),    // Body, active
            S("volley_spray", "SPLAY"),     // Mind, active
            S("field_mire", "NUMB"),        // Nature, field
            S("sign_brand", "ETCH"),        // Spirit, field
        });

        Assert.Equal(4, build.DistinctChosenSources);
    }

    [Fact]
    public void test_reachable_refuses_a_skill_the_catalogue_does_not_know()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Reachable.Compose(Seeker, new[] { S("not_a_skill", "FLATTEN") }));
        Assert.Contains("not a skill the catalogue knows", ex.Message);
    }

    [Fact]
    public void test_reachable_refuses_the_same_skill_woven_twice()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[] { S("hammer_blow", "FLATTEN"), S("hammer_blow", "FINISH") }));
        Assert.Contains("already woven", ex.Message);
    }

    [Fact]
    public void test_reachable_refuses_another_champions_signature()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[] { S("sig_anvil_hardface", "UPSET") }));
        Assert.Contains("signature", ex.Message);
    }

    [Fact]
    public void test_reachable_refuses_a_road_the_mastery_does_not_reach()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[] { S("hammer_blow", "FLATTEN") }, mastery: new MasteryTree()));
        Assert.Contains("no learned road", ex.Message);
    }

    [Fact]
    public void test_reachable_refuses_a_variation_the_skill_does_not_offer()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[] { S("hammer_blow", "SHUT FIST") }));
        Assert.Contains("no variation", ex.Message);
    }

    [Fact]
    public void test_reachable_refuses_a_slot_past_the_capacity()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[] { S(Signature, "OPEN HAND"), S("hammer_blow", "FLATTEN") }, slotCapacity: 1));
        Assert.Contains("no slot left", ex.Message);
    }

    [Fact]
    public void test_reachable_refuses_three_actives_because_the_split_would_drop_one()
    {
        // A four-slot account holds two actives and two passives. Three actives compose as two, and a
        // benchmark that measured that would be measuring a build with a hole it did not ask for.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[]
            {
                S("hammer_blow", "FLATTEN"), S("volley_spray", "SPLAY"), S("sign_call", "STEADY"), S("field_mire", "NUMB"),
            }));
        Assert.Contains("reached the fight", ex.Message);
    }

    [Fact]
    public void test_reachable_refuses_a_vow_the_account_has_not_found_and_one_past_its_capacity()
    {
        var complete = Vows.ById("vow_complete")!;
        var fragility = Vows.ById("vow_fragility")!;

        var unfound = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[] { S(Signature, "OPEN HAND", "vow_complete") }));
        Assert.Contains("could not be sworn", unfound.Message);

        var overCapacity = Assert.Throws<InvalidOperationException>(() =>
            Reachable.Compose(Seeker, new[] { S(Signature, "OPEN HAND", "vow_complete"), S("hammer_blow", "FLATTEN", "vow_fragility") },
                              slotCapacity: 4, knownVows: new[] { complete, fragility }, vowCapacity: 1));
        Assert.Contains("could not be sworn", overCapacity.Message);

        // Two actives need the four-slot budget (two actives, two passives) — at two slots the split
        // is one and one, and the composer would refuse the second active, which is its own test.
        var sworn = Reachable.Compose(Seeker, new[] { S(Signature, "OPEN HAND", "vow_complete"), S("hammer_blow", "FLATTEN", "vow_fragility") },
                                      slotCapacity: 4, knownVows: new[] { complete, fragility }, vowCapacity: 2);
        Assert.Equal(2, sworn.Vows.Count);
    }
}
