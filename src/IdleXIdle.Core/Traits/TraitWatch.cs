using System;
using System.Collections.Generic;

namespace IdleXIdle.Core.Traits;

/// <summary>
/// What a running expedition holds so its cleared waves reach the account's trait ledger.
/// </summary>
/// <remarks>
/// <para>
/// One object rather than four parameters threaded through the run: the ledger it writes, the account
/// facts the six account-fed rules read, the champion and region a discovery is stamped with, and the
/// queue of ids that have just awakened. The host refreshes the facts, the run feeds the waves, and
/// the host drains the queue to announce them.
/// </para>
/// <para>
/// <b>The awakened queue is drained, not read.</b> A reveal that could be shown twice is a reveal that
/// eventually is — the same reason the screens in this project hand back a cue by consuming it.
/// </para>
/// </remarks>
public sealed class TraitWatch
{
    private readonly List<string> _awakened = new();

    public TraitWatch(TraitLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        Ledger = ledger;
    }

    /// <summary>The account's ledger. Shared with the host — this class never owns a copy.</summary>
    public TraitLedger Ledger { get; }

    /// <summary>
    /// The six facts the account already keeps. Refreshed by the host, because they change outside a
    /// run: a set is completed in the Forge, a region is conquered on the Map.
    /// </summary>
    public TraitAccount Account { get; set; } = TraitAccount.None;

    /// <summary>Who is being played, for the one line of provenance a first awakening records (§33).</summary>
    public string CharacterId { get; set; } = "";

    /// <summary>Where. Same purpose, same one line.</summary>
    public string RegionId { get; set; } = "";

    /// <summary>Fold one cleared wave in, and queue anything it awakened.</summary>
    public void WaveCleared(TraitWaveFacts facts, int wave)
        => _awakened.AddRange(TraitDiscovery.OnWaveCleared(
            Ledger, facts, Account, new TraitFirst(CharacterId, RegionId, wave)));

    /// <summary>Re-check every rule without adding anything — at a run's end, or on load.</summary>
    public void Recheck(int wave = 0)
        => _awakened.AddRange(TraitDiscovery.Evaluate(
            Ledger, Account, new TraitFirst(CharacterId, RegionId, wave)));

    /// <summary>Anything awakened since the last drain. Empty is the normal answer.</summary>
    public IReadOnlyList<string> TakeAwakened()
    {
        if (_awakened.Count == 0) return Array.Empty<string>();
        var got = _awakened.ToArray();
        _awakened.Clear();
        return got;
    }
}
