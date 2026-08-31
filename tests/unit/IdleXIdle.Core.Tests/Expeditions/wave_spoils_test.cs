using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// Ordinary waves pay in things you spend, and depth changes what — never how much.
/// </summary>
public class WaveSpoilsTest
{
    private readonly ITestOutputHelper _out;

    public WaveSpoilsTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_a_wave_never_pays_nothing()
    {
        // The Scrap floor is the whole reason the tiered roll is allowed to fail two thirds of the time.
        // If a failed roll ever returned an empty spoil, most waves in the game would pay no material at
        // all and the change would read to a player as the drop rate having been cut.
        var rng = new Random(5);
        for (var wave = 1; wave <= 120; wave++)
            for (var i = 0; i < 20; i++)
                Assert.True(WaveSpoils.Roll(wave, rng).Amount >= 1,
                    $"wave {wave} paid nothing.");
    }

    [Fact]
    public void test_a_material_never_appears_before_its_depth()
    {
        // The tier gates are the progression. A Crystal falling out of wave 3 would hand a new player the
        // Forge's dearest operation before they have met the Forge.
        var rng = new Random(7);
        var firstSeen = new Dictionary<Material, int>();

        for (var wave = 1; wave <= 200; wave++)
            for (var i = 0; i < 60; i++)
            {
                var m = WaveSpoils.Roll(wave, rng).Material;
                if (!firstSeen.ContainsKey(m)) firstSeen[m] = wave;
            }

        foreach (var (m, wave) in firstSeen.OrderBy(kv => kv.Value))
            _out.WriteLine($"   {m,-8} first seen at wave {wave}");

        Assert.Equal(1, firstSeen[Material.Scrap]);
        Assert.True(firstSeen[Material.Essence] >= WaveSpoils.EssenceFromWave);
        Assert.True(firstSeen[Material.Core] >= WaveSpoils.CoreFromWave);
        Assert.True(firstSeen[Material.Crystal] >= WaveSpoils.CrystalFromWave);
    }

    [Fact]
    public void test_depth_buys_quality_and_not_quantity()
    {
        // The rule this whole file exists to hold. If the deep wave also paid MORE units, depth would be
        // a multiplier on the material economy and the numbers would run away exactly as the gear score
        // did — the same failure, one system over.
        var rng = new Random(9);

        static (float perWave, string best) Sample(Random rng, int wave)
        {
            var total = 0;
            var best = Material.Scrap;
            const int n = 4000;
            for (var i = 0; i < n; i++)
            {
                var s = WaveSpoils.Roll(wave, rng);
                total += s.Amount;
                if (s.Material > best) best = s.Material;
            }
            return (total / (float)n, best.ToString());
        }

        var shallow = Sample(rng, 5);
        var deep = Sample(rng, 80);

        _out.WriteLine($"   wave  5: {shallow.perWave:0.00} units/wave, best tier {shallow.best}");
        _out.WriteLine($"   wave 80: {deep.perWave:0.00} units/wave, best tier {deep.best}");

        Assert.Equal("Scrap", shallow.best);
        Assert.Equal("Crystal", deep.best);

        // THIS ASSERTION FAILED WHEN FIRST WRITTEN, and the failure was correct: the inherited Scrap
        // ramp (1 + wave/20) survived the move to tiered materials, so a deep wave paid 3.58 units AND
        // paid them in Crystal. Depth was multiplying quality and quantity at once. The ramp was cut;
        // the bound is exact now because there is nothing left to drift.
        Assert.Equal(shallow.perWave, deep.perWave, 3);
    }

    [Fact]
    public void test_the_interesting_drop_stays_an_event()
    {
        // A banner that fires on most waves is wallpaper. This pins the announcement rate the arena's
        // FlashSpoil actually produces, since that call is gated on the tier being better than Scrap.
        var rng = new Random(13);
        const int n = 20000;
        var tiered = 0;
        for (var i = 0; i < n; i++)
            if (WaveSpoils.Roll(40, rng).Material != Material.Scrap) tiered++;

        var share = tiered / (float)n;
        _out.WriteLine($"   at wave 40, {share:0.0%} of waves announce a tiered material");

        Assert.InRange(share, 0.25f, 0.45f);
    }
}
