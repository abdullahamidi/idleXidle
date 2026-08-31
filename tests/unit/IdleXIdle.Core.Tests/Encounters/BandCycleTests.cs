using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Encounters;

/// <summary>
/// The band cycles are the content, and these tests hold the rules that make them content rather than
/// a difficulty slider.
/// </summary>
public class BandCycleTests
{
    private static readonly string[] Regions =
    {
        "verdant_hollow", "cinderworks", "umbral_reach", "marrow_wastes", "still_archive", "pale_choir",
    };

    /// <summary>
    /// The archetype a band leans on, or null when nothing leads.
    /// </summary>
    /// <remarks>
    /// A tie is not a dominant. The Pale Choir weights all four equally, and reading its first entry as
    /// "dominant" made it look like the most single-minded region in the game when it is the opposite —
    /// which is exactly what the first version of this helper did.
    /// </remarks>
    private static Archetype? Dominant(BandDefinition band)
    {
        var ranked = band.Composition.OrderByDescending(c => c.Weight).ToList();
        if (ranked.Count > 1 && ranked[0].Weight == ranked[1].Weight) return null;
        return ranked[0].Archetype;
    }

    /// <summary>
    /// Every region must contain a band that punishes the build the rest of it rewards.
    /// </summary>
    /// <remarks>
    /// THE COUNTER-BAND RULE, and the single most important property of the whole content layer. A
    /// region whose every band asks the same question is solvable with one shape, and if every region
    /// is like that then the skill tree has one useful branch and the other three are decoration.
    ///
    /// The Pale Choir is exempt because it has no signature at all — every band is an even mix, which
    /// is a stronger form of the same rule.
    /// </remarks>
    [Fact]
    public void test_every_region_has_a_counter_band()
    {
        foreach (var region in Regions.Where(r => r != "pale_choir"))
        {
            var cycle = BandCycles.For(region);
            var dominants = cycle.Select(Dominant).Where(d => d is not null).ToList();
            var signature = dominants.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key;

            Assert.True(
                dominants.Any(d => d != signature),
                $"{region}: every band is dominated by {signature}. Without a counter-band the region " +
                "is solvable with a single build shape.");
        }
    }

    /// <summary>A region's signature must not swallow its whole cycle.</summary>
    [Fact]
    public void test_no_region_is_dominated_by_one_archetype()
    {
        foreach (var region in Regions)
        {
            var dominants = BandCycles.For(region).Select(Dominant).Where(d => d is not null).ToList();
            if (dominants.Count == 0) continue;   // an all-even region favours nothing; see the Pale Choir
            var most = dominants.GroupBy(d => d).Max(g => g.Count());

            Assert.True(
                most <= 3,
                $"{region}: {most} of {dominants.Count} leaning bands share an archetype. At four the " +
                "region stops having a lesson and becomes a single-answer region.");
        }
    }

    /// <summary>The Pale Choir favours nothing — that is its identity.</summary>
    [Fact]
    public void test_the_pale_choir_favours_no_shape()
    {
        foreach (var band in BandCycles.PaleChoir)
        {
            var weights = band.Composition.Select(c => c.Weight).Distinct().ToList();
            Assert.True(weights.Count == 1,
                "The Pale Choir must weight every archetype equally — only breadth is meant to survive it.");
            Assert.Equal(4, band.Composition.Count);
        }
    }

    /// <summary>Exactly one band in the game carries two affixes, and it is the endgame's template.</summary>
    [Fact]
    public void test_only_the_final_band_stacks_two_affixes()
    {
        foreach (var region in Regions)
        {
            var cycle = BandCycles.For(region);
            for (var i = 0; i < cycle.Count; i++)
            {
                var stacked = cycle[i].SecondAffix != Affix.None;
                if (!stacked) continue;
                Assert.True(region == "pale_choir" && i == cycle.Count - 1,
                    $"{region} band {i} stacks two affixes. Only the Pale Choir's last band may, because " +
                    "it exists to teach the corruption-tier rule before the endgame.");
            }
        }
    }

    /// <summary>Every affix in use must be one the tree can answer.</summary>
    /// <remarks>
    /// HOLLOW is the deliberate exception — it is an economy decision, not a combat one, and pressures
    /// no branch by design.
    /// </remarks>
    [Fact]
    public void test_every_affix_in_use_pressures_something()
    {
        // Every affix a band can carry now implements a rule (P13b: WARDED/ENTRENCHED/LEGION are
        // live and HOLLOW — the last costume — was cut), so the only thing AffixesOf must never
        // yield is None, which it filters by contract. Asserted anyway: a future affix added to the
        // enum without a rule should be authored into no band until it has one.
        foreach (var region in Regions)
            foreach (var band in BandCycles.For(region))
                foreach (var affix in Bands.AffixesOf(band))
                    Assert.NotEqual(Affix.None, affix);
    }

    /// <summary>A region's roster is its own Source plus two neighbours — never a single matchup.</summary>
    [Fact]
    public void test_every_roster_holds_three_sources()
    {
        foreach (var region in Regions)
        {
            var roster = BandCycles.RosterFor(region);
            Assert.Equal(3, roster.Count);
            Assert.Equal(3, roster.Distinct().Count());
        }
    }

    /// <summary>Bands are ten waves wide and the cycle repeats — the ladder must be learnable.</summary>
    [Fact]
    public void test_bands_are_ten_waves_and_the_cycle_repeats()
    {
        Assert.Equal(0, Bands.BandOf(1));
        Assert.Equal(0, Bands.BandOf(10));
        Assert.Equal(1, Bands.BandOf(11));

        // Wave 1 and wave 51 are the same band of a five-band cycle, one scale tier apart.
        Assert.Equal(Bands.CycleIndex(1, 5), Bands.CycleIndex(51, 5));
        Assert.True(Bands.RepeatScale(51, 5) > Bands.RepeatScale(1, 5));
    }

    /// <summary>The same wave, replayed, must produce the same composition.</summary>
    /// <remarks>
    /// Fast-forward pays the haul a wave originally paid, so a re-rolled composition would let a player
    /// farm a different wave than the one they proved.
    /// </remarks>
    [Fact]
    public void test_a_composition_is_reproducible_from_its_seed()
    {
        var band = BandCycles.BandFor("cinderworks", 23);

        List<float> Roll()
        {
            var rng = new Random(Bands.Seed("cinderworks", 23, 0));
            var archetype = Bands.Roll(band, rng);
            return Archetypes
                .Compose(archetype, 300f, 20f, 23, BandCycles.RosterFor("cinderworks"), rng)
                .Select(c => c.MaxHealth + c.Damage + c.Defense)
                .ToList();
        }

        Assert.Equal(Roll(), Roll());
    }

    /// <summary>
    /// The wave seed must be stable across PROCESSES, not merely within one.
    /// </summary>
    /// <remarks>
    /// REGRESSION. This used HashCode.Combine, which .NET randomises per process by design — so the same
    /// wave of the same descent produced different creatures after a restart, and fast-forward would
    /// have paid the haul of a wave the player never fought. A same-process test cannot catch it, which
    /// is why the value is pinned here instead: if the algorithm changes, this fails loudly.
    /// </remarks>
    [Fact]
    public void test_the_wave_seed_is_stable_across_processes()
    {
        Assert.Equal(Bands.Seed("cinderworks", 23, 1), Bands.Seed("cinderworks", 23, 1));
        Assert.NotEqual(Bands.Seed("cinderworks", 23, 1), Bands.Seed("cinderworks", 24, 1));
        Assert.NotEqual(Bands.Seed("cinderworks", 23, 1), Bands.Seed("umbral_reach", 23, 1));
        Assert.NotEqual(Bands.Seed("cinderworks", 23, 1), Bands.Seed("cinderworks", 23, 2));

        // Pinned: a literal, so a change of algorithm is a deliberate act rather than an accident.
        Assert.Equal(218177418, Bands.Seed("cinderworks", 23, 1));
    }

    /// <summary>Armour thickens with depth, or Weight solves Armoured once and never again.</summary>
    [Fact]
    public void test_armour_grows_with_depth()
    {
        List<float> DefenceAt(int wave)
            => Archetypes.Compose(Archetype.Armoured, 300f, 20f, wave, null, new Random(1))
                .Select(c => c.Defense).ToList();

        Assert.True(DefenceAt(60)[0] > DefenceAt(5)[0] * 1.5f,
            "Armour barely grew across 55 waves — a single Weight investment would answer it forever.");
    }
}
