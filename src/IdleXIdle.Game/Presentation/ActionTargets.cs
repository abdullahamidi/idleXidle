using System.Collections.Generic;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Game.Presentation;

/// <summary>Reads, from the fight's own resolved events, what a cast is about to do (ADR-011).</summary>
public static class ActionTargets
{
    /// <summary>
    /// The enemies <paramref name="cast"/> strikes: the skill Strikes that follow that Skill event at its own
    /// timestamp (until the next Skill or Beat), one slot each, in the order the fight resolved them.
    /// </summary>
    /// <remarks>
    /// The performance needs them BEFORE the beat — the blades leave the hand a flight earlier — and the fight
    /// was resolved ahead, so they are already there. Nothing is predicted and nothing is changed: a CLUSTER
    /// that lands five blows on one creature is one target, a SPLAY over the whole pack is one per creature.
    /// </remarks>
    public static IReadOnlyList<int> StruckBy(IReadOnlyList<BattleEvent> events, BattleEvent cast)
    {
        var slots = new List<int>();
        if (events is null) return slots;
        var i = 0;
        for (; i < events.Count; i++)
            if (events[i].Kind == BattleEventKind.Skill && events[i].AtMs == cast.AtMs && events[i].Slot == cast.Slot) break;
        for (i++; i < events.Count && events[i].AtMs == cast.AtMs; i++)
        {
            var e = events[i];
            if (e.Kind is BattleEventKind.Skill or BattleEventKind.Beat) break;
            if (e.Kind == BattleEventKind.Strike && e.FromSkill && !slots.Contains(e.Slot)) slots.Add(e.Slot);
        }
        return slots;
    }

    /// <summary>
    /// The creatures the champion's BASIC ATTACK at <paramref name="atMs"/> strikes: the Strikes at that ms whose
    /// provenance is the swing (<c>Hit == Swing</c>; never a skill's, a carry's or a bleed's), one slot each, in the order
    /// the fight resolved them, written into <paramref name="into"/>. Returns how many. Allocation-free.
    /// </summary>
    public static int SwungAt(IReadOnlyList<BattleEvent> events, int atMs, int[] into)
    {
        var n = 0;
        if (events is null || into is null) return 0;
        for (var i = 0; i < events.Count && n < into.Length; i++)
        {
            var e = events[i];
            if (e.AtMs != atMs || e.Kind != BattleEventKind.Strike || e.Hit != IdleXIdle.Core.Builds.HitSource.Swing) continue;
            var seen = false;
            for (var k = 0; k < n; k++) seen |= into[k] == e.Slot;
            if (!seen) into[n++] = e.Slot;
        }
        return n;
    }
}
