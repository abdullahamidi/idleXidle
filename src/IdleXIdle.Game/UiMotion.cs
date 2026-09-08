using System;
using System.Collections.Generic;

namespace IdleXIdle.Game;

/// <summary>
/// THE MOTION VOCABULARY: three durations, one easing, and a keyed store of small eased values so a
/// screen can ask "how far along is this hover / this pulse / this number" without owning a timer per
/// control. Animation explains CHANGE (UI polish brief §30–§32, LAW 10); nothing here loops.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keys.</b> A caller names each animated thing with an int — a rectangle's hash, a
/// <c>HashCode.Combine("train", row)</c> — and the store keeps its current value. Entries are dropped
/// the moment they settle, so the dictionary holds only what is moving (a screen at rest costs nothing
/// per frame, and there is no per-frame allocation: the dictionaries are pre-sized and reused).
/// </para>
/// <para>
/// <b>Reduced Motion.</b> Eases jump straight to their target; pulses still run, because a pulse is a
/// short fade that marks "this just changed" and the brief keeps simple fades. A screen that wants a
/// static highlight under Reduced Motion reads <see cref="Reduced"/> and draws it.
/// </para>
/// </remarks>
public static class UiMotion
{
    /// <summary>Fast feedback — hover, press, selection. 80–120 ms in the brief; 100 here.</summary>
    public const float Fast = 0.10f;

    /// <summary>A state transition — equip, purchase, unlock, an inspector's new content, a number changing.</summary>
    public const float Transition = 0.18f;

    /// <summary>A reward — a chest, a capstone, a set completing, a shield breaking.</summary>
    public const float Reward = 0.35f;

    /// <summary>Set by the host each frame from the accessibility setting.</summary>
    public static bool Reduced { get; set; }

    private static readonly Dictionary<int, float> Eased = new(64);
    private static readonly Dictionary<int, (float Life, float Length)> Pulses = new(32);
    private static readonly List<int> Settled = new(32);
    private static float _dt;

    /// <summary>Advance every stored value. Called once per frame by the host, before any screen draws.</summary>
    public static void Tick(float dt)
    {
        _dt = Math.Clamp(dt, 0f, 0.1f);
        if (Pulses.Count == 0) return;
        Settled.Clear();
        foreach (var (key, p) in Pulses)
        {
            var life = p.Life - _dt;
            if (life <= 0f) Settled.Add(key);
            else Pulses[key] = (life, p.Length);
        }
        foreach (var key in Settled) Pulses.Remove(key);
    }

    /// <summary>
    /// A value that eases toward <paramref name="target"/> (0 or 1, typically) over <paramref name="seconds"/>,
    /// smoothstepped. Ask it EVERY frame, for every control, whether or not anything is happening: an
    /// unknown key starts at 0, so a hover fades IN and a control standing still reads a flat 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ZERO IS REST. A key this has never seen is a control at rest, so it starts at 0 and a hover
    /// (target 1) fades in from there. It used to start at <c>1 - target</c> instead, on the reasoning
    /// that "the first ask starts at the target's opposite so a hover fades IN" — which is right for a
    /// hover and wrong for everything else, because a settled 0 FORGETS its key (a rested control
    /// costs nothing) and the very next ask then read a missing key as 1 and began fading down again.
    /// A control nobody was pointing at sawtoothed between 0 and 0.9 forever, which is the opposite of
    /// brief §30's "animate CHANGE, not everything".
    /// </para>
    /// <para>
    /// Four of the eleven screens in the UI polish pass had independently written a private guard
    /// around this call — "only ask while something is actually moving" — which is what a shared
    /// primitive looks like when it cannot be trusted, and two of the eleven arrived at THIS fix on
    /// their own from opposite ends of the game. <c>ui_motion_rest_test.cs</c> and
    /// <c>ui_motion_test.cs</c> in the Game test project pin the contract so the guards can come out.
    /// </para>
    /// </remarks>
    public static float Ease(int key, float target, float seconds = Fast)
    {
        if (Reduced) { Eased.Remove(key); return target; }
        // AN ABSENT KEY MEANS ZERO, not "the target's opposite". A value that settles at 1 is REMEMBERED
        // (below), so absence can only ever mean "rested at 0" — and reading it as 1 made every RESTING
        // control fade 1 → 0, settle, be forgotten, and start over on the next frame: a permanent 10 Hz
        // shimmer on every button, tile, card and row that asks for a hover it is not getting, which is
        // the one thing brief §30 forbids and this class's own summary promises does not happen.
        // Measured on BUILD's library tiles at 60 fps: mean ink 19,16,26 ↔ 42,37,59 on a six-frame cycle.
        // A hover still fades IN from 0 — the only thing the "opposite" was ever for.
        var have = Eased.TryGetValue(key, out var v) ? v : 0f;
        var step = seconds <= 0f ? 1f : _dt / seconds;
        v = have < target ? MathF.Min(target, have + step) : MathF.Max(target, have - step);
        if (MathF.Abs(v - target) < 1e-4f)
        {
            // Settled at 0 → forget it (a rested control costs nothing); settled at 1 → remember so
            // the next ask does not restart the fade.
            if (target <= 0f) Eased.Remove(key); else Eased[key] = target;
            return target;
        }
        Eased[key] = v;
        return Smooth(v);
    }

    /// <summary>Start (or restart) a one-shot pulse: <see cref="Pulse"/> then reads 1 → 0 over its length.</summary>
    public static void Flash(int key, float seconds = Transition) => Pulses[key] = (seconds, seconds);

    /// <summary>How much of a pulse is left, 1 (just fired) → 0 (done). 0 when nothing is running.</summary>
    public static float Pulse(int key)
        => Pulses.TryGetValue(key, out var p) && p.Length > 0f ? Math.Clamp(p.Life / p.Length, 0f, 1f) : 0f;

    /// <summary>True while a pulse is running — for a screen that wants "just changed" as a yes/no.</summary>
    public static bool Pulsing(int key) => Pulses.ContainsKey(key);

    /// <summary>Smoothstep: the one easing curve, so every motion in the game has the same hand.</summary>
    public static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>A stable key for a rectangle — what a hover on a control is keyed by.</summary>
    public static int KeyOf(Microsoft.Xna.Framework.Rectangle r) => HashCode.Combine(r.X, r.Y, r.Width, r.Height);

    /// <summary>Forget everything — a screen change, a new game.</summary>
    public static void Clear()
    {
        Eased.Clear();
        Pulses.Clear();
    }
}
