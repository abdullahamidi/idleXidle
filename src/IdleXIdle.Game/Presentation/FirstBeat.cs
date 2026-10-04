using System;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Expeditions;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// FIRST BEAT (the Metronome's passive, design.md 5.20 and section 4's EMPOWERED-HIT): the hit Core doubled prints its
/// number with a WHITE outline (<see cref="NumberOutline"/>'s style). Presentation only: it reads the replay, it changes
/// nothing.
/// </summary>
/// <remarks>
/// <para>
/// IT MIRRORS CORE'S <c>struckOnce</c> RULE EXACTLY (SoloBattle's Amp, :1469; LandOn, :1765). A creature joins the set
/// when a PRIMARY landing (a cast, a field tick, a reaction's answer; <c>HitSource.Primary</c>) strikes it; the basic
/// swing, a carry, a bleed, a reflect and DEADWEIGHT never add it. The first-hit multiplier is read inside the skill
/// tree's half of Amp (after <c>if (skillDef is null) return m;</c>), so it never touches the swing either. So the doubled
/// hit is the first PRIMARY Strike on each creature slot of the wave, and nothing else. Every LandOn emits exactly one
/// Strike, so "first Primary Strike event per slot" is Core's set, event for event.
/// </para>
/// <para>
/// The predicate is per wave and per slot (Core's set is wave-local and keyed by the creature, whose composition index is
/// the slot). <see cref="Reset"/> at the wave's start. Fixed storage; nothing allocates.
/// </para>
/// </remarks>
public sealed class FirstBeat
{
    /// <summary>The outline FIRST BEAT's number takes.</summary>
    public static readonly Color Outline = Color.White;

    private bool[] _struck = new bool[16];

    /// <summary>True when the champion's own passive doubles a creature's first hit (<c>FirstHitMultiplier &gt; 1</c>).</summary>
    public static bool Applies(SkillShape shape) => shape is not null && shape.FirstHitMultiplier > 1f;

    /// <summary>A new wave: no creature has been struck.</summary>
    public void Reset() => Array.Clear(_struck);

    /// <summary>
    /// Cross one event in the order the fight resolved it. True when it is the first PRIMARY Strike on its creature this
    /// wave: the hit Core multiplied by the first-hit multiplier. Any other event is false and changes nothing.
    /// </summary>
    public bool Cross(BattleEvent e)
    {
        if (e.Kind != BattleEventKind.Strike || e.Hit != HitSource.Primary || e.Slot < 0) return false;
        if (e.Slot >= _struck.Length) Array.Resize(ref _struck, Math.Max(e.Slot + 1, _struck.Length * 2));   // a composition wider than any shipped: once, not per frame
        if (_struck[e.Slot]) return false;
        _struck[e.Slot] = true;
        return true;
    }
}
