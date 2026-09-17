using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Progression;

/// <summary>
/// The rail's REVEALED set: which activities the player has been shown, ever.
/// </summary>
/// <remarks>
/// <para>
/// <b>The journey (2026-09-06): PLAY → NEED → REVEAL → EXPLAIN → USE.</b> A screen is CLOSED until
/// the need that opens it is real (<see cref="Unlocks.IsOpen"/>); the moment it is, a notice says
/// what opened and why, and the tile wears NEW until the player looks. Nothing
/// here decides WHEN — that is the gates' job, from facts the save already carries. This is the one
/// piece of explicit state the journey keeps, and it keeps it for one reason: a reveal is MONOTONE,
/// and one gate is not (GEAR reads items owned, and a bag can be salvaged empty). Once shown, a tile
/// stays; if its gate has since closed it shows its price, which is the restrained locked state.
/// </para>
/// <para>
/// A save from before this file loads with an empty list and is seeded from the gates, so a returning
/// player keeps every screen they had and hears no announcement for any of them. THE HUNT is always
/// in the set: it is the game.
/// </para>
/// <para>
/// <b>AMENDED 2026-09-09 — REVEALED IS NO LONGER A DRAW FILTER.</b> This set used to decide which
/// rail tiles were DRAWN, so a fresh save's rail was the Hunt alone and grew downward as the game
/// opened. Playtest: <i>"We removed the screen buttons from the left panel, having them appear only
/// later, but that's a bad approach because the player doesn't know something will be added there.
/// All buttons should remain visible, perhaps with a chain overlay indicating they are locked."</i>
/// </para>
/// <para>
/// That is not a reversal of the journey, and the two complaints are not in conflict: the earlier one
/// was about COGNITIVE LOAD AT THE POINT OF USE (nine live doors and no explanation), this one about
/// the ABSENT MAP (a rail that grows never shows the shape of the game). So the SHAPE is revealed
/// from the first frame and the CAPABILITY is still gated — eleven tiles always drawn, the unearned
/// ones visibly bound in chains, each saying its own price on hover and refusing out loud when
/// clicked. This set keeps every other job it had: <see cref="Newly"/> still drives the notice, the
/// NEW mark still means "you have not looked", and the chains now break on the frame the notice
/// fires. Deleting it would silence every reveal in the game.
/// </para>
/// </remarks>
public static class Reveal
{
    /// <summary>
    /// The set to start a session with: what the save remembered, plus whatever the gates already open.
    /// Never announces — a returning player has used these screens.
    /// </summary>
    public static HashSet<Activity> Restore(IEnumerable<string> saved, UnlockFacts facts)
    {
        var set = new HashSet<Activity> { Activity.Hunt };
        if (saved is not null)
            foreach (var name in saved)
                if (Enum.TryParse<Activity>(name, ignoreCase: true, out var a)) set.Add(a);
        foreach (var open in Unlocks.Open(facts)) set.Add(open);
        return set;
    }

    /// <summary>
    /// Add every activity the gates open that the set has not shown yet, and return them — in the rail's
    /// order — so the host can announce each once. Empty when nothing changed, which is nearly always.
    /// </summary>
    public static IReadOnlyList<Activity> Newly(ISet<Activity> revealed, UnlockFacts facts)
    {
        ArgumentNullException.ThrowIfNull(revealed);
        List<Activity>? fresh = null;
        foreach (var a in Enum.GetValues<Activity>())
        {
            if (revealed.Contains(a) || !Unlocks.IsOpen(a, facts)) continue;
            revealed.Add(a);
            (fresh ??= new List<Activity>()).Add(a);
        }
        return fresh ?? (IReadOnlyList<Activity>)Array.Empty<Activity>();
    }

    /// <summary>The names to write to the save, in a stable order.</summary>
    public static List<string> Names(IEnumerable<Activity> revealed)
        => revealed.Select(a => a.ToString()).OrderBy(s => s, StringComparer.Ordinal).ToList();
}
