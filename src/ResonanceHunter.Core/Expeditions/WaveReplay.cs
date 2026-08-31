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
        // A NEW WAVE IS A NEW SET OF CREATURES. Carrying the breaks over would badge wave two's front
        // enemy with wave one's damage — creatures are minted per wave and so is this.
        _creatureBreak.Clear();
        for (var i = 0; i < maxHealths.Count; i++)
        {
            _creatureHealth[i] = maxHealths[i];
            _creatureMax[i] = MathF.Max(1f, maxHealths[i]);
        }
    }

    private readonly Dictionary<int, int> _creatureBreak = new();

    /// <summary>How many defence breaks this creature is carrying right now. 0 for an untouched one.</summary>
    public int CreatureBreaks(int index) => _creatureBreak.GetValueOrDefault(index);

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
    public int NextChampionStrikeAfter(float ms)
    {
        // BASIC strikes only: a skill's landing blows already have their cast clip, and a swing clip
        // aimed at one of them would start a second animation inside the first.
        foreach (var e in _events)
            if (e.Kind == BattleEventKind.Strike && !e.FromSkill && e.AtMs > ms)
                return e.AtMs;
        return int.MaxValue;
    }

    /// <summary>
    /// The next SKILL event after <paramref name="ms"/> — any Form — or null when the wave casts nothing
    /// more. The HUNT screen anticipates it the way it anticipates the swing: the cast (or, for a Strike,
    /// the heavier attack) clip runs up to the moment the skill lands, so the hand opens exactly as the
    /// effect appears instead of a second after it.
    /// </summary>
    public BattleEvent? NextSkillEventAfter(float ms)
    {
        // TRAPS ARE SKIPPED, not stopped at. A Trap fires on a bite and gets no clip; returning it made
        // the screen's clip picker give up and miss the real cast behind it (review 2026-08-30).
        foreach (var e in _events)
            if (e.Kind == BattleEventKind.Skill && (Abilities.Form)e.Amount != Abilities.Form.Trap && e.AtMs > ms) return e;
        return null;
    }

    /// <summary>
    /// When a given FORM next fires after <paramref name="ms"/>, and when it last fired before it.
    /// </summary>
    /// <remarks>
    /// The fight screen could show a skill going off — it already reads Skill events and plays a VFX for
    /// them — but it could not show a skill WAITING, which is the half a player actually watches. The
    /// events already carry the Form in <c>Amount</c> and the moment in <c>AtMs</c>, so a cooldown
    /// readout built on these is the real rhythm of the resolved wave rather than a decorative bar
    /// ticking at a rate nobody chose.
    ///
    /// Returns <see cref="int.MaxValue"/> when the Form never fires again in this wave, and -1 when it
    /// has not fired yet — the caller needs to tell "waiting for the first cast" from "on cooldown".
    /// </remarks>
    public int NextSkillAfter(float ms, int form)
    {
        foreach (var e in _events)
            if (e.Kind == BattleEventKind.Skill && e.Amount == form && e.AtMs > ms)
                return e.AtMs;

        return int.MaxValue;
    }

    /// <summary>
    /// <see cref="NextSkillAfter(float, int)"/> for ONE skill: its Source (the event's Slot) and its
    /// Form. Two skills of the same Form — two Projectiles — used to share one readout, so their bars
    /// filled and emptied in lockstep and read as "both skills fire at once" (playtest 2026-08-26).
    /// </summary>
    public int NextSkillAfter(float ms, int source, int form)
    {
        foreach (var e in _events)
            if (IsCastOf(e, source, form) && e.AtMs > ms)
                return e.AtMs;
        return int.MaxValue;
    }

    /// <summary>
    /// A cast of one skill: its Skill event, or — for a passive Form — its Aura tick, which is the
    /// closest thing an always-on field has to a cast. Without this the rail could never answer "when
    /// did the Aura last fire" and pinned that row's readout at empty forever (review 2026-08-30).
    /// </summary>
    private static bool IsCastOf(BattleEvent e, int source, int form) =>
        (e.Kind == BattleEventKind.Skill || e.Kind == BattleEventKind.Aura)
        && e.Slot == source && e.Amount == form;

    /// <inheritdoc cref="NextSkillAfter(float, int, int)"/>
    public int LastSkillBefore(float ms, int source, int form)
    {
        var last = -1;
        foreach (var e in _events)
        {
            if (!IsCastOf(e, source, form)) continue;
            if (e.AtMs > ms) break;
            last = e.AtMs;
        }
        return last;
    }

    /// <inheritdoc cref="NextSkillAfter"/>
    public int LastSkillBefore(float ms, int form)
    {
        var last = -1;
        foreach (var e in _events)
        {
            if (e.Kind != BattleEventKind.Skill || e.Amount != form) continue;
            if (e.AtMs > ms) break;
            last = e.AtMs;
        }
        return last;
    }

    /// <summary>
    /// How many ACTIONS the champion took in (<paramref name="fromMs"/>, <paramref name="toMs"/>] — one
    /// per beat: a cast (a Skill event) or a basic attack (a Strike the sim did not mark FromSkill).
    /// </summary>
    /// <remarks>
    /// Counted by DISTINCT timestamp, because one cast lands one Strike per creature it reaches and an
    /// Aura ticks Strikes of its own between beats — counting events would count a four-target cast as
    /// four actions. The hunt's cooldown dial reads this: a beat-counted skill steps exactly once per
    /// action, where deriving the step from elapsed TIME between two casts made it jump two at once or
    /// stall (playtest 2026-08-30: "sometimes it counts 2 while the swing plays, sometimes not at all").
    /// </remarks>
    /// <summary>
    /// The champion's beat number at <paramref name="ms"/> — the sim's own count, or -1 before the
    /// wave's first action.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS REPLACES A HEURISTIC. The rail used to count "actions" by walking the damage stream and
    /// treating every <c>Strike</c> that was not a skill's own hit as one — which is a guess about
    /// DAMAGE standing in for a fact about RHYTHM, and it was wrong for the poison bleed, THORNS,
    /// BREAKER's overkill spill and a MARK detonation, all of which land with the same flag.
    /// </para>
    /// <para>
    /// <see cref="BattleEventKind.Beat"/> carries the run-cumulative count, so this is comparable
    /// ACROSS waves: a skill that cast late in one wave can be measured against a beat in the next
    /// without the caller carrying an offset of its own.
    /// </para>
    /// </remarks>
    public int BeatAt(float ms)
    {
        var beat = -1;
        foreach (var e in _events)
        {
            if (e.Kind != BattleEventKind.Beat) continue;
            if (e.AtMs > ms) break;
            beat = e.Amount;
        }
        return beat;
    }

    /// <summary>The beat number this wave ended on, or -1 if it held no action at all.</summary>
    public int LastBeat => BeatAt(float.MaxValue);

    /// <summary>
    /// When a TRAP last bit at or before <paramref name="ms"/>, or null.
    /// </summary>
    /// <remarks>
    /// A Trap is the one skill that is not an action: it answers the enemy's swing rather than the
    /// champion's beat, so it can never be aimed at one and had no champion animation for the whole
    /// life of the fight. The screen commits its clip opportunistically off this, which is why the
    /// question is "when did one last fire" rather than "when is the next".
    /// </remarks>
    public float? LastTrapBefore(float ms)
    {
        float? last = null;
        foreach (var e in _events)
        {
            if (e.Kind != BattleEventKind.Skill || (Abilities.Form)e.Amount != Abilities.Form.Trap) continue;
            if (e.AtMs > ms) break;
            last = e.AtMs;
        }
        return last;
    }

    /// <summary>The beat the wave opened on, or -1. Used to measure a wave that a skill sat out.</summary>
    public int FirstBeat
    {
        get
        {
            foreach (var e in _events)
                if (e.Kind == BattleEventKind.Beat) return e.Amount;
            return -1;
        }
    }

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

            // HOW DEEP THIS CREATURE IS BROKEN, as of the playhead. A STATE rather than a moment: the
            // fight screen draws it as a standing badge, so scrubbing backwards has to un-break it too,
            // which is why it is assigned rather than incremented.
            case BattleEventKind.Break:
                _creatureBreak[e.Slot] = e.Amount;
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
