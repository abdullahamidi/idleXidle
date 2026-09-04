using System;

namespace IdleXIdle.Core.Prestige;

/// <summary>
/// MEMORY DUST — a material the economy spends. Nothing else.
/// </summary>
/// <remarks>
/// <para>
/// This replaced <c>MemoryDustTree</c>, which was two systems wearing one name: a wallet, and a
/// permanent passive tree that bought stat multipliers. The tree is deleted — its balance modifiers
/// were retired design, and the dimensions they touched are owned deliberately now by Training and
/// Gear (raw numbers), Mastery (build specialisation) and Traits (discovered behaviour). Keeping them
/// alive for old accounts would have meant invisible legacy power that no new player could ever earn
/// and no screen could ever explain.
/// </para>
/// <para>
/// What survived is the part the game actually consumes. Dust is minted by the Warren tick by tick,
/// by region-mastery milestones, by conquests and by first-time corruption deepenings; it is spent on
/// Warren facility upgrades and on expedition checkpoints. That is a currency, so this is a wallet and
/// nothing more — no catalogue, no node ownership, no prerequisites, no points.
/// </para>
/// <para>
/// The name stays MEMORY DUST because it is still good player-facing terminology and still appears in
/// the top currency pills. Only the machinery behind it changed.
/// </para>
/// <para>
/// TRAIT POINTS are gone with the tree. They were <c>Earned</c> / <c>Spent</c> / <c>Available</c>
/// here, derived every frame from progress and spent on nodes; nothing spends them now, and a
/// currency with no sink is a number that means nothing.
/// </para>
/// </remarks>
public sealed class MemoryDustWallet
{
    /// <summary>How much Dust the account holds.</summary>
    public int MemoryDust { get; private set; }

    /// <summary>
    /// Credit Dust — the wallet's one faucet.
    /// </summary>
    /// <remarks>
    /// The callers are the real faucet list: the Warren's Dust facilities, region-mastery milestones,
    /// conquests, and first-time corruption deepenings — the last three priced in
    /// <c>CorruptionScaling</c>, so a test can pin them.
    /// </remarks>
    public void AddDust(int amount) => MemoryDust += Math.Max(0, amount);

    /// <summary>
    /// Spend Dust. Spends nothing and returns false when it cannot be afforded, so a caller can gate
    /// on the return rather than checking the balance itself and racing its own read.
    /// </summary>
    public bool Spend(int amount)
    {
        if (amount < 0 || MemoryDust < amount) return false;
        MemoryDust -= amount;
        return true;
    }

    /// <summary>Restore the balance from a save.</summary>
    /// <remarks>
    /// A save's Dust is the only thing left to restore. The owned-node list it may still carry is read
    /// ONCE by <c>LegacyTraitTree</c>, at load, and is never written back — see that file for what it
    /// preserves and what it deliberately discards.
    /// </remarks>
    public void Restore(int dust) => MemoryDust = Math.Max(0, dust);
}
