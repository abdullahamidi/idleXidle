using System;
using System.Collections.Generic;

namespace ResonanceHunter.Core.Expeditions;

/// <summary>
/// Replays a resolved wave's <see cref="BattleEvent"/> stream back into on-screen state.
/// </summary>
/// <remarks>
/// <para>
/// The sim resolves a whole wave in one call, but the player must watch it happen over several seconds.
/// So the screen rewinds to zero and re-derives the squad's health from the event stream as a playhead
/// crosses each beat. That re-derivation is the thing that must not drift: if the replay and the sim
/// disagree, the health bars LIE, and the player judges the squad on a fiction.
/// </para>
/// <para>
/// It lives in Core, not in the screen, for exactly the reason the sim does — it is pure logic with a
/// provable property (<c>replayed health == simulated health</c>), and it can be proven without opening
/// a window. It was a private loop inside the screen when it was subtly wrong; see the tests.
/// </para>
/// </remarks>
public sealed class WaveReplay
{
    private readonly IReadOnlyList<BattleEvent> _events;
    private readonly Dictionary<int, int> _health = new();
    private readonly Dictionary<int, int> _maxHealth = new();
    private readonly Dictionary<int, int> _shieldUntil = new();
    private int _cursor;
    private float _playheadMs;

    /// <param name="events">The wave's beats, in ascending <see cref="BattleEvent.AtMs"/> order.</param>
    /// <param name="startHealth">Each slot's health BEFORE the wave resolved.</param>
    /// <param name="maxHealth">Each slot's ceiling, so healing clamps exactly as the sim clamps it.</param>
    /// <param name="enemyHealth">The enemy's health at the top of the wave.</param>
    public WaveReplay(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyDictionary<int, int> startHealth,
        IReadOnlyDictionary<int, int> maxHealth,
        float enemyHealth)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(startHealth);
        ArgumentNullException.ThrowIfNull(maxHealth);

        _events = events;
        foreach (var (slot, hp) in startHealth) _health[slot] = hp;
        foreach (var (slot, hp) in maxHealth) _maxHealth[slot] = hp;

        EnemyMaxHealth = MathF.Max(1f, enemyHealth);
        EnemyHealth = enemyHealth;
    }

    /// <summary>Live health per slot, as of the playhead.</summary>
    public IReadOnlyDictionary<int, int> Health => _health;

    public float EnemyHealth { get; private set; }
    public float EnemyMaxHealth { get; }
    public float EnemyHealthFraction => EnemyMaxHealth <= 0f ? 0f : Math.Clamp(EnemyHealth / EnemyMaxHealth, 0f, 1f);

    /// <summary>True once every beat has been played out — the wave is done animating.</summary>
    public bool Finished => _cursor >= _events.Count;

    public int HealthOf(int slot) => _health.GetValueOrDefault(slot);
    public int MaxHealthOf(int slot) => _maxHealth.GetValueOrDefault(slot);

    public float HealthFractionOf(int slot)
    {
        var max = MaxHealthOf(slot);
        return max <= 0 ? 0f : Math.Clamp(HealthOf(slot) / (float)max, 0f, 1f);
    }

    /// <summary>
    /// Advance the playhead, applying every beat it crosses and returning them so the presentation can
    /// react (lunges, callouts, floating numbers).
    /// </summary>
    public IReadOnlyList<BattleEvent> Advance(float toMs)
    {
        _playheadMs = toMs;
        var crossed = new List<BattleEvent>();

        while (_cursor < _events.Count && _events[_cursor].AtMs <= toMs)
        {
            var e = _events[_cursor++];
            Apply(e);
            crossed.Add(e);
        }

        return crossed;
    }

    /// <summary>Whether a BULWARK is currently covering this slot — the Defender's reach, made visible.</summary>
    public bool IsShielded(int slot) =>
        _shieldUntil.TryGetValue(slot, out var until) && _playheadMs < until;

    /// <summary>The next enemy swing after <paramref name="ms"/>, for the anticipation windup.</summary>
    public int NextEnemyStrikeAfter(float ms)
    {
        foreach (var e in _events)
            if (e.Kind == BattleEventKind.EnemyStrike && e.AtMs > ms)
                return e.AtMs;

        return int.MaxValue;
    }

    private void Apply(BattleEvent e)
    {
        switch (e.Kind)
        {
            case BattleEventKind.Strike:
                EnemyHealth = MathF.Max(0f, EnemyHealth - e.Amount);
                break;

            case BattleEventKind.EnemyStrike:
                _health[e.Slot] = Math.Max(0, HealthOf(e.Slot) - e.Amount);
                break;

            // Clamped to the ceiling because the SIM clamps before it heals but reports the UNCLAMPED
            // amount: a MEND on a nearly-full creature emits "18" and delivers 2. Subtracting strikes
            // without ever adding these is what let the screen show a corpse the sim had at 99% health.
            case BattleEventKind.Heal:
                _health[e.Slot] = Math.Min(MaxHealthOf(e.Slot), HealthOf(e.Slot) + e.Amount);
                break;

            case BattleEventKind.Down:
                _health[e.Slot] = 0;
                break;

            case BattleEventKind.EnemyDown:
                EnemyHealth = 0f;
                break;

            case BattleEventKind.Shield:
                _shieldUntil[e.Slot] = e.AtMs + e.Amount;   // the sim tells us the reach; we just draw it
                break;
        }
    }
}
