using System;
using System.Collections.Generic;
using System.Linq;

namespace ResonanceHunter.Core.Characters;

/// <summary>
/// Which characters you have, and which one you are.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately thin. Switching character changes no progress whatsoever — the mastery tree, the trait
/// tree, gear, Gleam and the Warren are all shared and none of them resets. That was the decision that
/// made the roster affordable to build, and it is also the one consistent with the game's oldest
/// promise, which is that nothing is ever taken back.
/// </para>
/// <para>
/// Unlocks are DERIVED every frame AND banked. <see cref="Refresh"/> is handed the world's conquered
/// set each frame, the same way both trees derive their points, so a conquest made while logged out
/// still counts. But the set it grows is also saved (<see cref="SaveUnlocked"/>) and restored, because
/// a gate can tighten — the tiered roster moved THE THORNWALL from the first region's conquest to the
/// whole map — and a set that was only ever derived would take a champion back the day it did. Nothing
/// is ever taken back; that is the promise the bank keeps. Saves from before the bank existed are
/// seeded by <see cref="LegacyUnlocks"/>.
/// </para>
/// </remarks>
public sealed class CharacterState
{
    private readonly HashSet<string> _unlocked = new(StringComparer.OrdinalIgnoreCase)
    {
        CharacterRoster.StarterId,
    };

    private readonly HashSet<string> _questsDone = new(StringComparer.OrdinalIgnoreCase);

    private string _activeId = CharacterRoster.StarterId;

    /// <summary>The character you are playing. Never null and never locked.</summary>
    public Character Active => CharacterRoster.Get(_activeId);

    public string ActiveId => _activeId;

    public bool IsUnlocked(string id) => _unlocked.Contains(id);

    /// <summary>True when the quest gating a character has been finished.</summary>
    public bool QuestDone(string questId) => _questsDone.Contains(questId);

    /// <summary>Mark a quest finished — the hook the quest system will call.</summary>
    public void CompleteQuest(string questId)
    {
        if (!string.IsNullOrWhiteSpace(questId)) _questsDone.Add(questId);
    }

    /// <summary>
    /// Recompute which characters are available from world progress.
    /// </summary>
    /// <remarks>
    /// Called every frame by the host. Returns the ids that became available on THIS call, so the host
    /// can announce them — an unlock nobody is told about is the same as no unlock.
    /// </remarks>
    public IReadOnlyList<Character> Refresh(IEnumerable<string> conqueredRegionIds)
    {
        ArgumentNullException.ThrowIfNull(conqueredRegionIds);
        var conquered = new HashSet<string>(conqueredRegionIds, StringComparer.OrdinalIgnoreCase);

        var fresh = new List<Character>();
        foreach (var c in CharacterRoster.All)
        {
            if (_unlocked.Contains(c.Id)) continue;
            var earned = c.Unlock.Kind switch
            {
                UnlockKind.Start => true,
                UnlockKind.Conquest => c.Unlock.RegionId is { } r && conquered.Contains(r),
                UnlockKind.Quest => c.Unlock.QuestId is { } q && _questsDone.Contains(q),
                _ => false,
            };
            if (!earned) continue;
            _unlocked.Add(c.Id);
            fresh.Add(c);
        }
        return fresh;
    }

    /// <summary>Become this character. Refuses a locked or unknown id and reports it.</summary>
    public bool Select(string id)
    {
        if (CharacterRoster.Find(id) is null || !_unlocked.Contains(id)) return false;
        _activeId = id;
        return true;
    }

    public IReadOnlyList<Character> Unlocked =>
        CharacterRoster.All.Where(c => _unlocked.Contains(c.Id)).ToList();

    // ── Persistence ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restore from a save. An unknown or locked id falls back to the starter rather than throwing.
    /// </summary>
    /// <param name="activeId">The champion being played.</param>
    /// <param name="questsDone">Finished quest ids.</param>
    /// <param name="unlockedIds">
    /// The banked unlocked set. Ids the roster no longer has are dropped; the starter is always kept.
    /// Null or empty means "nothing banked" — the caller is expected to have seeded from
    /// <see cref="LegacyUnlocks"/> first, and <see cref="Refresh"/> re-derives the rest either way.
    /// </param>
    public void Restore(string? activeId, IEnumerable<string>? questsDone, IEnumerable<string>? unlockedIds = null)
    {
        _questsDone.Clear();
        if (questsDone is not null)
            foreach (var q in questsDone)
                if (!string.IsNullOrWhiteSpace(q)) _questsDone.Add(q);

        _unlocked.Clear();
        _unlocked.Add(CharacterRoster.StarterId);
        if (unlockedIds is not null)
            foreach (var id in unlockedIds)
                if (CharacterRoster.Find(id) is not null) _unlocked.Add(id);

        _activeId = activeId is not null && CharacterRoster.Find(activeId) is not null
            ? activeId
            : CharacterRoster.StarterId;
        _unlocked.Add(_activeId);   // you cannot lose the character you were playing
    }

    public IReadOnlyList<string> SaveQuests() => _questsDone.ToList();

    /// <summary>The banked unlocked set, roster order. Always contains the starter, so it is never empty.</summary>
    public IReadOnlyList<string> SaveUnlocked() =>
        CharacterRoster.All.Where(c => _unlocked.Contains(c.Id)).Select(c => c.Id).ToList();
}
