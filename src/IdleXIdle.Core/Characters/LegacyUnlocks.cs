using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Characters;

/// <summary>
/// The champion gates as they stood BEFORE the tiered roster (2026-08-26), kept only to read old saves.
/// </summary>
/// <remarks>
/// <para>
/// Until the tiered roster, the unlocked set was never saved: it was derived every frame from conquest
/// and the finished-quest list, and the save carried only the active id. That was fine while the rules
/// only ever loosened. The tiered roster TIGHTENS them — THE THORNWALL went from the first region's
/// conquest to the whole map — and a set derived from tightened rules takes champions back, which
/// breaks the game's oldest promise. So the set is persisted now (<see cref="CharacterState.SaveUnlocked"/>),
/// and a save written before it was persisted is seeded from the rules that were true when it was
/// written. This table is those rules. It must never change again; that is the point of it.
/// </para>
/// <para>
/// The two retired quests are evaluated here from the save's own record — the finished-quest list, and
/// the facts they read (best depth, kept Vows) — so a player who had met the old demand but saved before
/// the host latched the quest is still counted.
/// </para>
/// </remarks>
public static class LegacyUnlocks
{
    /// <summary>The old Hollow Hunt: depth 20 in the Verdant Hollow.</summary>
    public const string HollowHuntId = "q_hollow_hunt";

    /// <summary>The depth the old Hollow Hunt asked for.</summary>
    public const int HollowHuntDepth = 20;

    /// <summary>The old First Vow: one descent finished under a kept Vow.</summary>
    public const string FirstVowId = "q_first_vow";

    /// <summary>Champion id → the gate it had. Exactly the pre-tier roster, and never edited.</summary>
    public static IReadOnlyDictionary<string, CharacterUnlock> Before { get; } =
        new Dictionary<string, CharacterUnlock>(StringComparer.OrdinalIgnoreCase)
        {
            ["seeker"] = CharacterUnlock.Start,
            ["anvil"] = CharacterUnlock.Conquest("cinderworks"),
            ["chorus"] = CharacterUnlock.Conquest("umbral_reach"),
            ["metronome"] = CharacterUnlock.Conquest("marrow_wastes"),
            ["unbroken"] = CharacterUnlock.Conquest("still_archive"),
            ["tower"] = CharacterUnlock.Conquest("pale_choir"),
            ["quiver"] = CharacterUnlock.Quest(HollowHuntId, "Reach depth 20 in the Verdant Hollow"),
            ["thornwall"] = CharacterUnlock.Conquest("verdant_hollow"),
            ["oathbound"] = CharacterUnlock.Quest(FirstVowId, "Finish a descent with a Vow's demand still met"),
            ["magpie"] = CharacterUnlock.Conquest("cinderworks"),
        };

    /// <summary>
    /// The champions an old save had earned under the old rules — the set to bank on its first load.
    /// </summary>
    /// <param name="conqueredRegionIds">The save's conquered regions.</param>
    /// <param name="questsDone">The save's finished-quest ids, old ids included.</param>
    /// <param name="depthByRegion">Best depth per region id, from the save's region farms.</param>
    /// <param name="runsWithVowKept">The save's latched kept-Vow count.</param>
    public static IReadOnlyList<string> Seed(
        IEnumerable<string> conqueredRegionIds,
        IEnumerable<string> questsDone,
        IReadOnlyDictionary<string, int> depthByRegion,
        int runsWithVowKept)
    {
        ArgumentNullException.ThrowIfNull(conqueredRegionIds);
        ArgumentNullException.ThrowIfNull(questsDone);
        ArgumentNullException.ThrowIfNull(depthByRegion);
        var conquered = new HashSet<string>(conqueredRegionIds, StringComparer.OrdinalIgnoreCase);
        var done = new HashSet<string>(questsDone, StringComparer.OrdinalIgnoreCase);

        bool OldQuestMet(string questId) => questId switch
        {
            HollowHuntId => done.Contains(HollowHuntId)
                            || (depthByRegion.TryGetValue("verdant_hollow", out var d) && d >= HollowHuntDepth),
            FirstVowId => done.Contains(FirstVowId) || runsWithVowKept >= 1,
            _ => done.Contains(questId),
        };

        return Before
            .Where(kv => kv.Value.Kind switch
            {
                UnlockKind.Start => true,
                UnlockKind.Conquest => kv.Value.RegionId is { } r && conquered.Contains(r),
                UnlockKind.Quest => kv.Value.QuestId is { } q && OldQuestMet(q),
                _ => false,
            })
            .Select(kv => kv.Key)
            .ToList();
    }
}
