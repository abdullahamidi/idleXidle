using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Traits;

/// <summary>Where a trait first awakened — enough for one line of flavour, and nothing more (§33).</summary>
/// <param name="CharacterId">The champion who was being played.</param>
/// <param name="RegionId">The region it happened in.</param>
/// <param name="Wave">The wave it happened on. Zero for an awakening that was not in a fight.</param>
public readonly record struct TraitFirst(string CharacterId, string RegionId, int Wave);

/// <summary>
/// Everything the ACCOUNT knows about traits: what has awakened, what fed it, and which three each
/// champion is wearing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Discovery is account-wide and the loadout is per character</b> (§25, §26, LAWS 6-7). Those two
/// halves are stored differently on purpose: <see cref="Discovered"/> is one flat list, and the
/// loadouts are keyed by character id. Storing them the other way round is the ten-parallel-careers
/// failure §25 names — ten champions each rediscovering the whole catalogue.
/// </para>
/// <para>
/// <b>The cap lives here, below the UI.</b> Exactly three per champion, no duplicates, and nothing
/// undiscovered — enforced at every mutation and again at restore, exactly as the duplicate-skill
/// rule is (<c>PlayerLoadout</c>, §14's precedent). Nothing in the catalogue carries a downside to
/// price it (§35), so the three slots ARE the cost and the cap has to be real.
/// </para>
/// <para>
/// <b>Monotone.</b> Nothing removes a discovered id and nothing decreases a counter. That is what
/// makes every rule unmissable: a player who was not looking when a threshold was crossed still owns
/// the trait, and <see cref="TraitDiscovery"/> re-checks every rule on every pass.
/// </para>
/// <para>
/// <b>Swapping is free and reversible</b> (§27). There is no cost field anywhere in this file and no
/// currency reads it.
/// </para>
/// </remarks>
public sealed class TraitLedger
{
    private readonly HashSet<string> _discovered = new(StringComparer.Ordinal);
    private readonly Dictionary<TraitCounter, double> _tally = new();
    private readonly Dictionary<string, List<string>> _loadouts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TraitFirst> _first = new(StringComparer.Ordinal);

    /// <summary>Every trait the account has awakened, in catalogue order.</summary>
    public IReadOnlyList<string> Discovered =>
        _discovered.OrderBy(TraitCatalogue.OrderOf).ToList();

    /// <summary>How many have awakened. The screen counts tiles with it; it is never shown as "17 / 26".</summary>
    public int DiscoveredCount => _discovered.Count;

    /// <summary>Has this trait awakened on this account?</summary>
    public bool Has(string? id) => id is not null && _discovered.Contains(id);

    /// <summary>Where a trait first awakened, if it has.</summary>
    public TraitFirst? FirstOf(string id) => _first.TryGetValue(id, out var f) ? f : null;

    // ── The accumulators ─────────────────────────────────────────────────────────────────────────

    /// <summary>What this counter stands at.</summary>
    public double Of(TraitCounter counter) => _tally.TryGetValue(counter, out var v) ? v : 0d;

    /// <summary>Add to a counter. Never subtracts: a negative contribution is ignored, not applied.</summary>
    public void Add(TraitCounter counter, double amount)
    {
        if (double.IsNaN(amount) || amount <= 0d) return;
        _tally[counter] = Of(counter) + amount;
    }

    // ── Discovery ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaken a trait. Returns true only the FIRST time, which is what the reveal is keyed on.
    /// </summary>
    /// <remarks>
    /// <see cref="TraitDiscovery"/> is the only caller in the game. Nothing else may write this set —
    /// a second writer is how an account-wide fact becomes two facts that disagree.
    /// </remarks>
    public bool Discover(string id, TraitFirst first)
    {
        if (!TraitCatalogue.Known(id)) return false;
        if (!_discovered.Add(id)) return false;
        _first[id] = first;
        return true;
    }

    // ── The per-character loadout ────────────────────────────────────────────────────────────────

    /// <summary>The three (or fewer) traits this champion is wearing, in the order they were equipped.</summary>
    public IReadOnlyList<string> LoadoutOf(string characterId)
        => _loadouts.TryGetValue(characterId ?? "", out var row) ? row : Array.Empty<string>();

    /// <summary>Is this champion wearing this trait right now?</summary>
    public bool IsEquipped(string characterId, string traitId)
        => LoadoutOf(characterId).Contains(traitId, StringComparer.Ordinal);

    /// <summary>
    /// Wear a trait. Refuses an undiscovered one, a duplicate, and a fourth.
    /// </summary>
    /// <returns>True if the loadout changed.</returns>
    public bool Equip(string characterId, string traitId)
    {
        if (string.IsNullOrEmpty(characterId) || !Has(traitId)) return false;
        var row = Row(characterId);
        if (row.Contains(traitId, StringComparer.Ordinal)) return false;
        if (row.Count >= TraitCatalogue.SlotsPerCharacter) return false;
        row.Add(traitId);
        return true;
    }

    /// <summary>Take a trait off. Free and reversible — the discovery is untouched (§27).</summary>
    public bool Unequip(string characterId, string traitId)
    {
        if (string.IsNullOrEmpty(characterId)) return false;
        return _loadouts.TryGetValue(characterId, out var row) && row.Remove(traitId);
    }

    /// <summary>
    /// Wear it if it is off, take it off if it is on. What the screen's one button does.
    /// </summary>
    /// <returns>True if the loadout changed — false when all three slots are already full.</returns>
    public bool Toggle(string characterId, string traitId)
        => IsEquipped(characterId, traitId) ? Unequip(characterId, traitId) : Equip(characterId, traitId);

    /// <summary>Put a trait into a NAMED slot, replacing whatever was there. The screen's drag-free swap.</summary>
    public bool EquipInto(string characterId, int slot, string traitId)
    {
        if (string.IsNullOrEmpty(characterId) || !Has(traitId)) return false;
        if (slot < 0 || slot >= TraitCatalogue.SlotsPerCharacter) return false;
        var row = Row(characterId);
        // Already worn somewhere else: this is a MOVE, not a second copy.
        var at = row.FindIndex(x => string.Equals(x, traitId, StringComparison.Ordinal));
        if (at >= 0) row.RemoveAt(at);
        while (row.Count <= slot && row.Count < TraitCatalogue.SlotsPerCharacter) row.Add("");
        if (slot >= row.Count) return false;
        row[slot] = traitId;
        row.RemoveAll(string.IsNullOrEmpty);
        return true;
    }

    private List<string> Row(string characterId)
    {
        if (!_loadouts.TryGetValue(characterId, out var row))
            _loadouts[characterId] = row = new List<string>(TraitCatalogue.SlotsPerCharacter);
        return row;
    }

    // ── Persistence ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Every champion that has ever worn a trait, and what it wears.</summary>
    public IEnumerable<(string CharacterId, IReadOnlyList<string> TraitIds)> SaveLoadouts()
        => _loadouts
            .Where(kv => kv.Value.Count > 0)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (kv.Key, (IReadOnlyList<string>)kv.Value.ToList()));

    /// <summary>The accumulators, keyed by counter NAME — the enum's own name is the save contract.</summary>
    public IEnumerable<KeyValuePair<string, double>> SaveTally()
        => _tally.OrderBy(kv => kv.Key).Select(kv => new KeyValuePair<string, double>(kv.Key.ToString(), kv.Value));

    /// <summary>Where each trait first awakened.</summary>
    public IEnumerable<(string TraitId, TraitFirst First)> SaveProvenance()
        => _first.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, kv.Value));

    /// <summary>
    /// Read the account back. Unknown ids and unknown counter names are DROPPED, not carried.
    /// </summary>
    /// <remarks>
    /// The key set is closed by <see cref="TraitCounter"/> (§29), so a save written by a build with a
    /// counter this one does not have loses that counter rather than growing an untended field. Every
    /// loadout is re-clamped on the way in — to three, to no duplicates, and to what has actually
    /// awakened — because a save is an input like any other.
    /// </remarks>
    public void Restore(
        IEnumerable<string>? discovered,
        IEnumerable<KeyValuePair<string, double>>? tally,
        IEnumerable<(string CharacterId, IReadOnlyList<string> TraitIds)>? loadouts,
        IEnumerable<(string TraitId, TraitFirst First)>? provenance = null)
    {
        _discovered.Clear();
        _tally.Clear();
        _loadouts.Clear();
        _first.Clear();

        if (discovered is not null)
            foreach (var id in discovered)
                if (TraitCatalogue.Known(id)) _discovered.Add(id);

        if (tally is not null)
            foreach (var (key, value) in tally)
                if (Enum.TryParse<TraitCounter>(key, out var counter) && value > 0d && !double.IsNaN(value))
                    _tally[counter] = value;

        if (loadouts is not null)
            foreach (var (characterId, ids) in loadouts)
            {
                if (string.IsNullOrEmpty(characterId) || ids is null) continue;
                var row = Row(characterId);
                foreach (var id in ids)
                {
                    if (row.Count >= TraitCatalogue.SlotsPerCharacter) break;
                    if (!Has(id) || row.Contains(id, StringComparer.Ordinal)) continue;
                    row.Add(id);
                }
                if (row.Count == 0) _loadouts.Remove(characterId);
            }

        if (provenance is not null)
            foreach (var (id, first) in provenance)
                if (_discovered.Contains(id)) _first[id] = first;
    }
}
