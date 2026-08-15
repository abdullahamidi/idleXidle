using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Per-creature health, so the screen can show a composition dying one creature at a time.
    /// </summary>
    /// <remarks>
    /// The aggregate <see cref="EnemyHealth"/> stays: it is what the wave's own bar shows and what the
    /// existing tests prove against. This is additive — a wave of one behaves exactly as before.
    /// </remarks>
    private readonly Dictionary<int, float> _creatureHealth = new();
    private readonly Dictionary<int, float> _creatureMax = new();

    /// <summary>Tell the replay what the wave's composition was, so it can track each creature.</summary>
    public void SetComposition(IReadOnlyList<float> maxHealths)
    {
        ArgumentNullException.ThrowIfNull(maxHealths);
        _creatureHealth.Clear();
        _creatureMax.Clear();
        for (var i = 0; i < maxHealths.Count; i++)
        {
            _creatureHealth[i] = maxHealths[i];
            _creatureMax[i] = MathF.Max(1f, maxHealths[i]);
        }
    }

    public int CreatureCount => _creatureMax.Count;

    public bool CreatureAlive(int index)
        => !_creatureHealth.TryGetValue(index, out var hp) || hp > 0f;

    public float CreatureHealthFraction(int index)
        => _creatureHealth.TryGetValue(index, out var hp) && _creatureMax.TryGetValue(index, out var max)
            ? Math.Clamp(hp / max, 0f, 1f)
            : 1f;

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
    public int NextEnemyStrikeAfter(float ms) => NextAfter(ms, BattleEventKind.EnemyStrike);

    /// <summary>
    /// The next CHAMPION swing after <paramref name="ms"/>, so its clip can anticipate the blow too.
    /// </summary>
    /// <remarks>
    /// The twin of <see cref="NextEnemyStrikeAfter"/>, and it exists because the two actors were running
    /// in opposite animation phase. The enemy's clip was driven by an anticipation clock and completed
    /// AT the impact; the champion's was armed by the impact itself and therefore played entirely AFTER
    /// its own hit landed. On screen the champion struck and then wound up, which is the reported
    /// "animasyon geçişleri garip görünüyor" — the same fight running forwards on one side of the arena
    /// and backwards on the other.
    /// </remarks>
    public int NextChampionStrikeAfter(float ms) => NextAfter(ms, BattleEventKind.Strike);

    private int NextAfter(float ms, BattleEventKind kind)
    {
        foreach (var e in _events)
            if (e.Kind == kind && e.AtMs > ms)
                return e.AtMs;

        return int.MaxValue;
    }

    private void Apply(BattleEvent e)
    {
        switch (e.Kind)
        {
            case BattleEventKind.Strike:
                EnemyHealth = MathF.Max(0f, EnemyHealth - e.Amount);
                if (_creatureHealth.ContainsKey(e.Slot))
                    _creatureHealth[e.Slot] = MathF.Max(0f, _creatureHealth[e.Slot] - e.Amount);
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
                // Now emitted once per CREATURE as it falls, not once per wave. Zeroing that creature
                // keeps the screen honest when rounding leaves a fraction behind; the aggregate only
                // hits zero when the last one does.
                if (e.Slot >= 0 && _creatureHealth.ContainsKey(e.Slot)) _creatureHealth[e.Slot] = 0f;
                if (_creatureMax.Count == 0 || _creatureHealth.Values.All(h => h <= 0f)) EnemyHealth = 0f;
                break;

            case BattleEventKind.Shield:
                _shieldUntil[e.Slot] = e.AtMs + e.Amount;   // the sim tells us the reach; we just draw it
                break;
        }
    }
}
