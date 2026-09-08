using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Tests.Builds;
using IdleXIdle.Core.Traits;
using Xunit;

namespace IdleXIdle.Core.Tests.Traits;

/// <summary>
/// THE SEAM: from the three ids a champion wears to the dials the fight reads.
/// </summary>
/// <remarks>
/// <para>
/// ADDED BY REVIEW, 2026-09-04. Neither of the two suites P4 shipped crosses this seam.
/// <c>trait_liveness_test</c> hands <c>SoloBattle</c> a shape it fetched from the catalogue itself
/// (<c>Trait(id)</c>), and <c>trait_system_test</c> stops at the ledger — so
/// <see cref="TraitEffects.Compose"/>, <c>PlayerLoadout.TraitShape</c> and the fold in
/// <c>BuildComposer.Compose</c> had NO test at all. Break any one of those three and every one of
/// the forty-six trait tests stays green while not one trait reaches a real fight: built, tested,
/// green, and never running — the exact species this project hunts, and the same finding the
/// 2026-08-31 audit made about the whole variation layer (see <c>BlowVerticalSliceTest</c>, which
/// exists for the same reason and is the pattern this file follows).
/// </para>
/// <para>
/// So every assertion below goes through the HUNT'S OWN DOOR — <see cref="PlayerLoadout.ToBuild"/> —
/// and reads the dial off the composed <c>Build.Shape.Traits</c>, which is the record
/// <c>SoloBattle</c> hoists into its <c>traits</c> local on the first line of a wave.
/// </para>
/// </remarks>
public class trait_composition_seam_test
{
    private static readonly TraitFirst Somewhere = new("seeker", "cinderworks", 12);

    /// <summary>A loadout carrying one skill, so <c>ToBuild</c> composes a real build.</summary>
    private static PlayerLoadout Woven()
    {
        var l = new PlayerLoadout { SkillCapacity = 1 };
        l.SetSkill(l.AddSkill(), "hammer_blow");
        return l;
    }

    private static TraitLedger Wearing(string characterId, params string[] traitIds)
    {
        var ledger = new TraitLedger();
        foreach (var id in traitIds)
        {
            Assert.True(ledger.Discover(id, Somewhere), $"{id} is not a catalogue id");
            Assert.True(ledger.Equip(characterId, id), $"{id} could not be worn");
        }
        return ledger;
    }

    private static Build Compose(PlayerLoadout loadout)
        => loadout.ToBuild(Taught.Everything(), character: null);

    [Fact]
    public void test_a_worn_trait_reaches_the_composed_build_the_fight_reads()
    {
        // LAST WORD's dial, carried the whole way: ledger -> TraitEffects -> PlayerLoadout ->
        // BuildComposer -> Build.Shape.Traits. This is the only assertion in the suite that would
        // fail if the fold in BuildComposer.Compose were dropped.
        var ledger = Wearing("seeker", "t_last_word");
        var loadout = Woven();
        loadout.TraitShape = TraitEffects.Compose(ledger.LoadoutOf("seeker"), TraitContext.None);

        var build = Compose(loadout);

        Assert.Equal(TraitCatalogue.ById_("t_last_word").Shape(TraitContext.None).Traits.LastEnemyBonus,
                     build.Shape.Traits.LastEnemyBonus, precision: 4);
        Assert.True(build.Shape.Traits.LastEnemyBonus > 0f);
    }

    [Fact]
    public void test_a_champion_wearing_nothing_composes_exactly_as_it_did_before_traits_existed()
    {
        // The other half, and the reason a neutral default on every dial matters: an empty loadout
        // must compose to TraitRules.None, or the liveness suite's with-and-without comparison is
        // measuring two things at once.
        var loadout = Woven();
        loadout.TraitShape = TraitEffects.Compose(new TraitLedger().LoadoutOf("seeker"), TraitContext.None);

        Assert.Equal(TraitRules.None, Compose(loadout).Shape.Traits);
    }

    [Fact]
    public void test_all_three_worn_traits_arrive_together_and_none_overwrites_another()
    {
        // Three slots, three different dials, one fold. A Combine that returned one side would pass
        // a one-trait test and fail here.
        var ledger = Wearing("seeker", "t_last_word", "t_scar_tissue", "t_settling_weight");
        var loadout = Woven();
        loadout.TraitShape = TraitEffects.Compose(ledger.LoadoutOf("seeker"), TraitContext.None);

        var traits = Compose(loadout).Shape.Traits;
        Assert.True(traits.LastEnemyBonus > 0f, "LAST WORD did not survive the fold");
        Assert.True(traits.ShieldAfterBreakBonus > 0f, "SCAR TISSUE did not survive the fold");
        Assert.True(traits.PowerPerDeadEnemy > 0f, "SETTLING WEIGHT did not survive the fold");
    }

    [Fact]
    public void test_a_world_conditional_trait_is_resolved_at_composition_and_not_in_the_fight()
    {
        // WHAT KILLED YOU carries no dial until the world names a wall — which is what keeps
        // SoloBattle from ever learning there is a run log. Both readings, through the same door.
        var ledger = Wearing("seeker", "t_what_killed_you");
        var loadout = Woven();

        loadout.TraitShape = TraitEffects.Compose(ledger.LoadoutOf("seeker"), TraitContext.None);
        Assert.Null(Compose(loadout).Shape.Traits.GrudgeArchetype);

        loadout.TraitShape = TraitEffects.Compose(
            ledger.LoadoutOf("seeker"), new TraitContext(LastWallArchetype: Archetype.Armoured));
        var armed = Compose(loadout).Shape.Traits;
        Assert.Equal(Archetype.Armoured, armed.GrudgeArchetype);
        Assert.True(armed.GrudgeBonus > 0f);
    }

    [Fact]
    public void test_the_loadout_signature_moves_when_the_worn_three_change()
    {
        // The hunt re-composes the running build when PlayerLoadout.Signature changes (HuntScreen
        // BuildStamp). Without the trait stamp in it, a player who swapped a trait mid-run would go
        // on fighting with the old three until they also touched a skill — a change made and not
        // applied, which is dormancy wearing a different coat.
        var ledger = Wearing("seeker", "t_last_word", "t_scar_tissue");
        var loadout = Woven();

        var bare = loadout.Signature;

        loadout.TraitShape = TraitEffects.Compose(new[] { "t_last_word" }, TraitContext.None);
        var one = loadout.Signature;

        loadout.TraitShape = TraitEffects.Compose(new[] { "t_scar_tissue" }, TraitContext.None);
        var other = loadout.Signature;

        Assert.NotEqual(bare, one);
        Assert.NotEqual(one, other);
        Assert.True(ledger.IsEquipped("seeker", "t_last_word"));
    }

    [Fact]
    public void test_a_worn_trait_actually_changes_what_a_real_wave_delivers()
    {
        // AND THE END OF THE WIRE. Everything above proves the dial arrives; this proves the arrival
        // is worth something, through the composed build rather than a hand-built shape — the same
        // fight twice, differing only by the three ids the champion is wearing.
        var plain = Woven();
        var worn = Woven();
        worn.TraitShape = TraitEffects.Compose(
            Wearing("seeker", "t_last_word").LoadoutOf("seeker"), TraitContext.None);

        float Delivered(PlayerLoadout loadout)
        {
            var metrics = new WaveMetrics();
            // One creature, so `alive == 1` from the first tick — LAST WORD's condition, posed.
            var creatures = new System.Collections.Generic.List<WaveCreature>
            {
                new() { MaxHealth = 400_000f, Health = 400_000f, Damage = 0f },
            };
            SoloBattle.ResolveWave(new Champion { MaxHealth = 200_000, Health = 200_000 },
                                   Compose(loadout), new Hunter(), creatures, 900,
                                   ExpeditionTuning.Default with { AutoAttackDamage = 0f },
                                   new System.Random(11), metrics: metrics);
            return metrics.DeliveredDamage;
        }

        var without = Delivered(plain);
        var with = Delivered(worn);
        Assert.True(with > without * 1.05f,
            $"LAST WORD composed through PlayerLoadout delivered {with:0} against {without:0} — " +
            "the trait reaches the build but not the fight.");
    }
}
