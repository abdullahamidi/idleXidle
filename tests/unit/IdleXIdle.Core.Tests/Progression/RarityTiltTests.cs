using System;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// The loot-quality tilt must reach the roll that mints an item.
/// </summary>
/// <remarks>
/// REGRESSION. Rarity was resolved from keystones, gear and the trait tree's whole FORTUNE road, carried
/// through the expedition as Haul.Quality, summed across waves — and read by nothing. Every node on that
/// road and every "+RARITY" affix in the game was inert, and no test noticed because each half of the
/// chain was individually correct. These tests assert the END of the chain, which is the only place the
/// break was visible.
/// </remarks>
public class RarityTiltTests
{
    private static Chest ChestOf(Rarity grade) => new()
    {
        Rarity = grade,
        Tier = 20,
        Element = null,
    };

    /// <summary>
    /// How often a chest of this grade yields an Epic or better, at a given build tilt.
    /// </summary>
    /// <remarks>
    /// THE TAIL, not the mean. A chest grade guarantees a rarity FLOOR, so most of every roll lands on
    /// that floor and the average is nearly insensitive to anything — measured, a tripled rarity stat
    /// moved the mean by 4%, which reads as "the stat does nothing" even when the same change lifts the
    /// Epic-or-better rate by a third. The tail is also what a player actually experiences: nobody
    /// notices a shifted average, everybody notices the purple items arriving more often.
    ///
    /// Many independent seeds, not one long stream. A single stream is not a paired comparison — a
    /// changed weight shifts how many draws each roll consumes, so everything downstream decorrelates.
    /// The first version of this test read the tilt as running BACKWARDS for exactly that reason.
    /// </remarks>
    private static float EpicOrBetterRate(Rarity grade, float bonus)
    {
        var items = Enumerable.Range(0, 200)
            .SelectMany(seed => Enumerable.Range(0, 40)
                .SelectMany(_ => Chests.Open(ChestOf(grade), new Random(seed * 7919 + 13),
                                             rarityBonus: bonus).Items))
            .ToList();

        Assert.NotEmpty(items);
        return items.Count(i => i.Rarity >= Rarity.Epic) / (float)items.Count;
    }

    /// <summary>A rarity build opens better chests than a neutral one.</summary>
    [Fact]
    public void test_the_rarity_tilt_reaches_the_chest_roll()
    {
        var neutral = EpicOrBetterRate(Rarity.Rare, 1f);
        var fortunate = EpicOrBetterRate(Rarity.Rare, 1.5f);

        Assert.True(
            fortunate > neutral * 1.15f,
            $"A +50% rarity build saw an Epic-or-better {fortunate:P2} of the time against {neutral:P2} " +
            "for a neutral one. The tilt is resolved, carried and summed, and then thrown away — every " +
            "node on the FORTUNE road is decoration.");
    }

    /// <summary>The tilt is a multiplier on the grade, not a replacement for it.</summary>
    /// <remarks>
    /// A rarity build must make GOOD chests better rather than making bad ones adequate — otherwise
    /// chest grade, which is what depth and bosses pay out, stops mattering.
    /// </remarks>
    [Fact]
    public void test_chest_grade_still_dominates_the_tilt()
    {
        Assert.True(
            EpicOrBetterRate(Rarity.Legendary, 1f) > EpicOrBetterRate(Rarity.Common, 2f),
            "A doubled tilt on a Common chest beat a Legendary one. Grade is what depth pays out; if a " +
            "build stat can out-weigh it, chest quality stops being a reason to go deeper.");
    }

    /// <summary>A degenerate tilt cannot make a chest worthless or throw.</summary>
    [Fact]
    public void test_a_zero_tilt_is_clamped_rather_than_ruinous()
    {
        var reward = Chests.Open(ChestOf(Rarity.Rare), new Random(1), rarityBonus: 0f);

        Assert.NotEmpty(reward.Items);
        Assert.True(reward.Materials > 0);
    }
}
