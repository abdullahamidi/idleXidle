using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// THE SOURCE MATRIX — every Source must be able to build a whole loadout out of its own variations.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue's Sources were distributed by feel, and the result was that some Sources could not
/// fill a build at all: a mono-Source player could reach two Actives and no Passive, or the reverse,
/// and a Single-Source Vow was therefore available to some Sources and not others for no stated
/// reason. Six Sources × (2 Active + 2 Passive) is exactly twenty-four, which is exactly the number of
/// variations the twelve skills have — so the distribution is not a preference, it is the only one
/// that lets all six build.
/// </para>
/// <para>
/// This file is written FIRST and fails until the catalogue matches it. It is the target, not a
/// description of what happens to be there.
/// </para>
/// </remarks>
public class SourceMatrixTests
{
    /// <summary>The authored target: for each Source, the two Active and the two Passive variations.</summary>
    /// <remarks>
    /// Written as skill id + variation name so a rename cannot quietly satisfy the invariant, and so
    /// this table reads as the design document it is.
    /// </remarks>
    public static readonly IReadOnlyDictionary<Source, (string[] Active, string[] Passive)> Target =
        new Dictionary<Source, (string[], string[])>
        {
            [Source.Body] = (new[] { "hammer_blow/FLATTEN", "volley_spray/CLUSTER" },
                             new[] { "hammer_press/CRUSHING", "drain_wilt/SUP" }),
            [Source.Machine] = (new[] { "snare_repay/BANKED", "drain_drink/SIPHON" },
                                new[] { "hammer_press/PIN", "snare_jaws/IRON" }),
            [Source.Mind] = (new[] { "sign_call/SPEND", "volley_spray/SPLAY" },
                             new[] { "sign_brand/SPRAWL", "drain_wilt/SHRIVEL" }),
            [Source.Nature] = (new[] { "field_pulse/THRONG", "drain_drink/GLUT" },
                               new[] { "volley_weep/TORRENT", "field_mire/NUMB" }),
            [Source.Shadow] = (new[] { "hammer_blow/FINISH", "snare_repay/VENGEANCE" },
                               new[] { "snare_jaws/NET", "volley_weep/CARRION" }),
            [Source.Spirit] = (new[] { "sign_call/STEADY", "field_pulse/SHARE" },
                               new[] { "sign_brand/ETCH", "field_mire/TEEMING" }),
        };

    private static IEnumerable<(SkillDef Def, SkillVariation Var)> EveryVariation()
        => SkillCatalogue.All.SelectMany(d => d.Variations.Select(v => (d, v)));

    private static string Key(SkillDef d, SkillVariation v) => $"{d.Id}/{v.Name}";

    [Fact]
    public void test_every_source_owns_exactly_two_active_and_two_passive_variations()
    {
        foreach (var source in Enum.GetValues<Source>())
        {
            var mine = EveryVariation().Where(x => x.Var.Source == source).ToList();

            var active = mine.Where(x => x.Def.TakesABeat).Select(x => Key(x.Def, x.Var)).OrderBy(s => s).ToList();
            var passive = mine.Where(x => !x.Def.TakesABeat).Select(x => Key(x.Def, x.Var)).OrderBy(s => s).ToList();

            Assert.Equal(Target[source].Active.OrderBy(s => s), active);
            Assert.Equal(Target[source].Passive.OrderBy(s => s), passive);
        }
    }

    [Fact]
    public void test_the_matrix_accounts_for_every_variation_exactly_once()
    {
        // Twenty-four variations, twenty-four cells: no variation may be missing from the target and
        // none may appear twice.
        var authored = EveryVariation().Select(x => Key(x.Def, x.Var)).OrderBy(s => s).ToList();
        var targeted = Target.Values.SelectMany(v => v.Active.Concat(v.Passive)).OrderBy(s => s).ToList();

        Assert.Equal(24, authored.Count);
        Assert.Equal(targeted, authored);
    }

    [Fact]
    public void test_every_skill_has_two_variations_with_three_reinforcements_each()
    {
        foreach (var def in SkillCatalogue.All)
        {
            Assert.Equal(2, def.Variations.Count);
            foreach (var v in def.Variations)
                Assert.Equal(3, v.Reinforcements.Count);
        }
    }

    [Fact]
    public void test_a_skill_never_offers_the_same_source_twice()
    {
        // Both variations of one skill sharing a Source would make that skill's fork a fork about
        // nothing for a mono-Source player — they would take the same Source either way.
        foreach (var def in SkillCatalogue.All)
            Assert.Equal(2, def.Variations.Select(v => v.Source).Distinct().Count());
    }

    [Fact]
    public void test_every_source_can_fill_a_whole_loadout_from_its_own_variations()
    {
        // The point of the matrix: two Actives and two Passives, from four DIFFERENT skills, so the
        // loadout is legal (a skill cannot be equipped twice).
        foreach (var source in Enum.GetValues<Source>())
        {
            var mine = EveryVariation().Where(x => x.Var.Source == source).ToList();
            var actives = mine.Where(x => x.Def.TakesABeat).ToList();
            var passives = mine.Where(x => !x.Def.TakesABeat).ToList();

            Assert.Equal(2, actives.Count);
            Assert.Equal(2, passives.Count);
            Assert.Equal(4, mine.Select(x => x.Def.Id).Distinct().Count());
        }
    }

    [Fact]
    public void test_a_reinforcement_name_is_unique_inside_its_variation()
    {
        foreach (var (def, v) in EveryVariation())
            Assert.Equal(v.Reinforcements.Count, v.Reinforcements.Select(r => r.Name).Distinct().Count());
    }

    [Fact]
    public void test_every_reinforcement_actually_changes_the_definition()
    {
        // A reinforcement with no Modify is a purchase that does nothing — they shipped that way once.
        foreach (var (def, v) in EveryVariation())
            foreach (var r in v.Reinforcements)
                Assert.True(r.Modify is not null, $"{def.Id}/{v.Name}/{r.Name} carries no change at all");
    }
}
