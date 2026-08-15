using System;
using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Expeditions;

/// <summary>What an ordinary wave hands over, besides Gleam.</summary>
public readonly record struct WaveSpoil(Material Material, int Amount);

/// <summary>
/// The per-wave material trickle — the reward for the waves that are not bosses.
/// </summary>
/// <remarks>
/// <para>
/// A cleared wave paid Gleam and exactly one Scrap-ish trickle, so ninety percent of the waves a player
/// fights produced a number going up and nothing they could hold. Playtest, in the player's words: the
/// waves should also drop the things you spend on upgrading, reforging and salvaging.
/// </para>
/// <para>
/// So the trickle has TIERS now, and depth chooses which. Scrap everywhere, Essence once a run is past
/// the shallows, Core deeper, Crystal rarely and only far down. That gives the material economy the
/// same shape everything else here has — <b>depth buys quality, never quantity</b> — and it means the
/// Forge's dearer operations become reachable by going deeper rather than by grinding longer.
/// </para>
/// </remarks>
public static class WaveSpoils
{
    /// <summary>
    /// What a wave pays, in units. One. Always one, at every depth, in every region.
    /// </summary>
    /// <remarks>
    /// <b>This was a ramp (<c>1 + wave/20</c>) and the ramp was the bug.</b> Once depth started choosing
    /// the material TIER, a surviving quantity ramp meant depth multiplied quality AND quantity at the
    /// same time — a deep wave paid 3.6x the units AND paid them in Crystal. That is compounding, and
    /// compounding is precisely how the gear score reached 90,000. The test that pins this
    /// (<c>test_depth_buys_quality_and_not_quantity</c>) is what caught it.
    /// </remarks>
    public const int UnitsPerWave = 1;

    /// <summary>The depth at which each tier starts appearing at all.</summary>
    public const int EssenceFromWave = 8;
    public const int CoreFromWave = 25;
    public const int CrystalFromWave = 60;

    /// <summary>How often a qualifying wave pays its best available tier rather than Scrap.</summary>
    /// <remarks>
    /// Deliberately under a half. The tiered material is the interesting drop, and an interesting drop
    /// that arrives every time is a resource rather than an event — the same rule the chest follows.
    /// </remarks>
    public const double TieredChance = 0.35;

    /// <summary>The best material tier a wave at this depth can pay.</summary>
    public static Material BestTierFor(int wave) => wave switch
    {
        >= CrystalFromWave => Material.Crystal,
        >= CoreFromWave => Material.Core,
        >= EssenceFromWave => Material.Essence,
        _ => Material.Scrap,
    };

    /// <summary>
    /// Roll a wave's spoils. Always something; sometimes something better.
    /// </summary>
    /// <remarks>
    /// The Scrap floor is never removed, because a wave that pays nothing is a wave that did not
    /// happen — and at these depths the tiered roll fails about two thirds of the time. The AMOUNT is
    /// the same everywhere by design; see <see cref="UnitsPerWave"/> for why the depth ramp was cut.
    /// </remarks>
    public static WaveSpoil Roll(int wave, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        var best = BestTierFor(wave);
        if (best != Material.Scrap && rng.NextDouble() < TieredChance)
            return new WaveSpoil(best, 1);

        return new WaveSpoil(Material.Scrap, UnitsPerWave);
    }
}
