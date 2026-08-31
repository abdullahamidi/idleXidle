using System;
using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Expeditions;

/// <summary>
/// What an ordinary wave hands over, besides Gleam: always a material, and rarely a charter.
/// </summary>
/// <remarks>
/// The charter is nullable rather than a second roll the caller has to remember to make. A wave's
/// payout is ONE thing to credit, so a caller cannot accidentally implement half of it — which is how
/// this codebase loses features.
/// </remarks>
public readonly record struct WaveSpoil(Material Material, int Amount, Charter? Charter = null);

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

    /// <summary>
    /// How often a wave also pays a single-use Forge charter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One wave in forty. Deliberately rarer than the tiered material, because a charter is a whole
    /// operation rather than a step toward one — a REFINE CHART skips a cost the player would otherwise
    /// spend real Gleam on, and Gleam is scarce now. At this rate a session that clears a couple of
    /// hundred waves sees a handful, which is a pleasant surprise rather than a supply line.
    /// </para>
    /// <para>
    /// It does NOT replace the material — the wave pays both. A drop that sometimes swaps one reward for
    /// another reads as the game taking something away.
    /// </para>
    /// </remarks>
    public const double CharterChance = 0.025;

    /// <summary>The depth at which the charters start appearing at all.</summary>
    /// <remarks>
    /// The Forge opens on the first chest, so a charter before then is a permission for a screen the
    /// player has not met — the same mistake the Warren was making by paying before it unlocked.
    /// </remarks>
    public const int ChartersFromWave = 5;

    /// <summary>The charters a wave can drop. MERGE charts retired with the manual merge tray.</summary>
    private static readonly Charter[] SpendablePool = { Charter.Refine, Charter.Reforge, Charter.Salvage };

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

        // Rolled FIRST and unconditionally, so the material branch below cannot change how often a
        // charter appears. Two rewards, two independent rolls — the alternative (rolling the charter
        // only on the Scrap path) would have made charters commoner in the shallows than at depth,
        // which is backwards and would have been invisible until someone measured it.
        var charter = wave >= ChartersFromWave && rng.NextDouble() < CharterChance
            ? (Charter?)SpendablePool[rng.Next(SpendablePool.Length)]
            : null;

        var best = BestTierFor(wave);
        if (best != Material.Scrap && rng.NextDouble() < TieredChance)
            return new WaveSpoil(best, 1, charter);

        return new WaveSpoil(Material.Scrap, UnitsPerWave, charter);
    }
}
