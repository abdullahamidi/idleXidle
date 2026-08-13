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
/// Unlocks are DERIVED, not banked. <see cref="Refresh"/> is handed the world's conquered set every
/// frame, the same way both trees derive their points, so there is nothing to double-count across a
/// reload and nothing to migrate when a region's id changes. The save carries only the active id.
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

    /// <summary>True when the quest gating a character has been finished. Nothing sets these yet.</summary>
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

    /// <summary>Restore from a save. An unknown or locked id falls back to the starter rather than throwing.</summary>
    public void Restore(string? activeId, IEnumerable<string>? questsDone)
    {
        _questsDone.Clear();
        if (questsDone is not null)
            foreach (var q in questsDone)
                if (!string.IsNullOrWhiteSpace(q)) _questsDone.Add(q);

        _activeId = activeId is not null && CharacterRoster.Find(activeId) is not null
            ? activeId
            : CharacterRoster.StarterId;
        _unlocked.Add(_activeId);   // you cannot lose the character you were playing
    }

    public IReadOnlyList<string> SaveQuests() => _questsDone.ToList();
}
