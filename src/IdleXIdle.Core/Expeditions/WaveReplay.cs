using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Expeditions;

/// <summary>
/// Replays a resolved wave's <see cref="BattleEvent"/> stream back into on-screen state.
/// </summary>
/// <remarks>
/// <para>
/// The sim resolves a whole wave in one call, but the player must watch it happen over several seconds.
/// So the screen rewinds to zero and re-derives the champion's health from the event stream as a playhead
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
        // The bar's ceiling comes from the pool, so the screen can draw a shield against its own
        // capacity rather than against whatever the largest grant happened to be.
        MaxShield = Builds.ShieldRules.CapFor(maxHealth.GetValueOrDefault(0));

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

    // ── THE INSPECTOR'S STANDING STATE (2026-09-06): what a hovered creature's base and current
    //    numbers are, and which statuses stand at the playhead. Fed by the sim's own events
    //    (DefenceNow, AttackBreak, Slowed, Staggered, Marked), never re-derived from its rules. ──

    private readonly Dictionary<int, float> _baseDamage = new();
    private readonly Dictionary<int, float> _baseDefence = new();
    private readonly Dictionary<int, float> _defenceNow = new();
    private int _breakAll, _breakFront, _breakSecond;   // whole percent, <= 0
    private int _slowPercent;
    private int _staggerUntilMs = -1;
    private int _staggerMs;
    private int _markUntilMs = -1;
    private int _markPercent;

    /// <summary>The wave's own bite clock, before any slow — set with the composition.</summary>
    public int EnemyIntervalMs { get; private set; }

    /// <summary>
    /// The composition WITH its base numbers, so the inspector can say BASE → CURRENT. Replaces the
    /// health-only overload's data and resets every standing status for the new wave.
    /// </summary>
    public void SetComposition(IReadOnlyList<Builds.WaveCreature> creatures, int enemyIntervalMs)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        SetComposition(creatures.Select(c => c.MaxHealth).ToList());
        _baseDamage.Clear();
        _baseDefence.Clear();
        _defenceNow.Clear();
        for (var i = 0; i < creatures.Count; i++)
        {
            _baseDamage[i] = creatures[i].Damage;
            _baseDefence[i] = creatures[i].Defense;
            _defenceNow[i] = creatures[i].Defense;
        }
        EnemyIntervalMs = Math.Max(1, enemyIntervalMs);
        _breakAll = _breakFront = _breakSecond = 0;
        _slowPercent = 0;
        _staggerUntilMs = _markUntilMs = -1;
        _staggerMs = _markPercent = 0;
    }

    /// <summary>This creature's health as it stands, and its ceiling — the inspector's HEALTH line.</summary>
    public float CreatureHealth(int index) => _creatureHealth.GetValueOrDefault(index);
    public float CreatureMaxHealth(int index) => _creatureMax.GetValueOrDefault(index);

    public float CreatureBaseDamage(int index) => _baseDamage.GetValueOrDefault(index);
    public float CreatureBaseDefence(int index) => _baseDefence.GetValueOrDefault(index);
    public float CreatureDefenceNow(int index)
        => _defenceNow.TryGetValue(index, out var d) ? d : _baseDefence.GetValueOrDefault(index);

    /// <summary>The standing attack break on this creature, in whole percent (<= 0): the wave-wide break, or the front/second creature's own if deeper.</summary>
    public int CreatureAttackBreakPercent(int index)
    {
        var order = AliveOrder(index);
        var own = order == 0 ? _breakFront : order == 1 ? _breakSecond : 0;
        return Math.Min(_breakAll, own);
    }

    /// <summary>What this creature's bite does now: its base damage under its standing attack break.</summary>
    public float CreatureDamageNow(int index)
        => CreatureBaseDamage(index) * MathF.Max(0f, 1f + CreatureAttackBreakPercent(index) / 100f);

    /// <summary>The standing slow on the wave's bite clock, in whole percent (>= 0).</summary>
    public int SlowPercent => _slowPercent;

    /// <summary>The bite clock as it stands: the base interval stretched by the slow.</summary>
    public int BiteEveryMsNow => (int)MathF.Round(EnemyIntervalMs * (1f + _slowPercent / 100f));

    /// <summary>Milliseconds of stagger still standing at the playhead, or 0.</summary>
    public int StaggerLeftMs => _staggerUntilMs > _playheadMs ? (int)(_staggerUntilMs - _playheadMs) : 0;
    public int StaggerMs => _staggerMs;

    /// <summary>Milliseconds of the open Mark still standing at the playhead, or 0.</summary>
    public int MarkLeftMs => _markUntilMs > _playheadMs ? (int)(_markUntilMs - _playheadMs) : 0;
    public int MarkPercent => _markPercent;

    /// <summary>This creature's place among the living, front first — the sim's own FirstAlive / SecondAlive order.</summary>
    private int AliveOrder(int index)
    {
        var order = 0;
        for (var i = 0; i < index; i++)
            if (CreatureAlive(i)) order++;
        return CreatureAlive(index) ? order : -1;
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

    /// <summary>
    /// SHIELD held at the playhead. Reconstructed from the wave's own events, so scrubbing to the
    /// middle of a fight shows the bar the fight actually had at that moment.
    /// </summary>
    public int CurrentShield { get; private set; }

    /// <summary>The ceiling that Shield is drawn against — half the pool.</summary>
    public int MaxShield { get; private set; }

    public int HealthOf(int slot) => _health.GetValueOrDefault(slot);
    private int MaxHealthOf(int slot) => _maxHealth.GetValueOrDefault(slot);

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

    /// <summary>
    /// Whether the champion is holding SHIELD right now — the one meaning that word has.
    /// </summary>
    /// <remarks>
    /// This used to be <c>IsShielded(slot)</c>, fed by the UNDYING event and meaning "undying is
    /// covering you". The chip it drew said SHIELDED beside a bar that says SHIELD and meant something
    /// else, which is precisely the collision the brief's one-word rule exists to prevent — and
    /// UNDYING already has its own chip two positions along, so the screen was saying the same thing
    /// twice under two names.
    /// </remarks>
    public bool HasShield => CurrentShield > 0;

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
    public BattleEvent? NextSkillEventAfter(float ms, IReadOnlySet<int>? reactionSlots = null)
    {
        // REACTION SLOTS ARE SKIPPED, not stopped at. A reaction fires on a bite and gets no cast
        // clip; returning it made the clip picker give up and miss the real cast behind it. The
        // caller says which slots react — the event no longer carries a Form to guess from.
        foreach (var e in _events)
            if (e.Kind == BattleEventKind.Skill && e.AtMs > ms
                && (reactionSlots is null || !reactionSlots.Contains(e.Slot)))
                return e;
        return null;
    }

    /// <summary>
    /// When the skill in SLOT <paramref name="slot"/> next acts after <paramref name="ms"/> — its
    /// cast, or its Aura tick, which is the closest thing an always-on field has to one. Slot-keyed:
    /// the Form-era overloads collided for two skills of one style ("both bars fill at once").
    /// Returns <see cref="int.MaxValue"/> when it never acts again this wave.
    /// </summary>
    public int NextSkillAfter(float ms, int slot)
    {
        foreach (var e in _events)
            if (IsActOf(e, slot) && e.AtMs > ms)
                return e.AtMs;
        return int.MaxValue;
    }

    /// <inheritdoc cref="NextSkillAfter(float, int)"/> Returns -1 when it has not acted yet.
    public int LastSkillBefore(float ms, int slot)
    {
        var last = -1;
        foreach (var e in _events)
        {
            if (!IsActOf(e, slot)) continue;
            if (e.AtMs > ms) break;
            last = e.AtMs;
        }
        return last;
    }

    /// <summary>One act of the slot's skill: its cast, or its field's tick.</summary>
    private static bool IsActOf(BattleEvent e, int slot) =>
        (e.Kind == BattleEventKind.Skill || e.Kind == BattleEventKind.Aura) && e.Slot == slot;

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

    /// <summary>
    /// When the next beat of this kind lands, or <see cref="int.MaxValue"/> if none remains.
    /// </summary>
    /// <remarks>
    /// The presentation asks this to park the playhead just short of a beat it wants the player to
    /// SEE arrive — the first Signature cast, told about before it happens rather than after the third
    /// time. Nothing about a tutorial enters this file: it is a question about the wave's own event
    /// list, and the answer is a millisecond.
    /// </remarks>
    public int NextEventOfKindAfter(float ms, BattleEventKind kind) => NextAfter(ms, kind);

    /// <summary>
    /// Whether the Reaction in <paramref name="slot"/> is rearming at <paramref name="ms"/>, and how far through,
    /// read off the fight's own <see cref="BattleEventKind.ReactionArmed"/> reports — never rebuilt from a rearm
    /// time.
    /// </summary>
    /// <param name="pendingReadyMs">
    /// When a rearm still running at the wave's end comes due, in this wave's ms (the expedition's
    /// <c>ReactionReadyAfterLastWave</c>); null when none is. Its report belongs to the next wave, so without this
    /// the wave's last trigger would have no end to sweep toward.
    /// </param>
    /// <remarks>
    /// A report at R carrying Amount L says the reaction rearmed over [R - L, R]; the window that holds
    /// <paramref name="ms"/> is the first report after it whose rearm had already begun. A rearm carried in from
    /// the previous wave begins before 0. With no report ahead, a trigger this wave has not closed is still
    /// rearming only when the fight says one is pending; a reaction that never rearms (a Reaction with no number
    /// of its own) is simply always armed.
    /// </remarks>
    public ReactionReadiness ReactionReadinessAt(float ms, int slot, int? pendingReadyMs = null)
    {
        int? open = null;   // a trigger at or before ms that no report at or before ms has closed
        foreach (var e in _events)
        {
            if (e.Slot != slot) continue;
            if (e.AtMs <= ms)
            {
                if (e.Kind == BattleEventKind.Skill) open = e.AtMs;
                else if (e.Kind == BattleEventKind.ReactionArmed) open = null;
                continue;
            }
            if (e.Kind != BattleEventKind.ReactionArmed) continue;
            var from = e.AtMs - e.Amount;
            return from <= ms ? ReactionReadiness.Rearming(from, e.AtMs, ms) : ReactionReadiness.Armed;
        }
        return open is { } t && pendingReadyMs is { } ready && ready > t
            ? ReactionReadiness.Rearming(t, ready, ms)
            : ReactionReadiness.Armed;
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

            // SHIELD is a running total here: the events say what CHANGED, and the replay keeps the
            // standing figure so a screen that joins mid-wave draws the bar without having seen the
            // grant that filled it.
            case BattleEventKind.ShieldGained:
                CurrentShield += e.Amount;
                break;

            case BattleEventKind.ShieldAbsorbed:
                CurrentShield = Math.Max(0, CurrentShield - e.Amount);
                break;

            case BattleEventKind.ShieldBroken:
                CurrentShield = 0;
                break;

            // THE INSPECTOR'S STANDING STATE — each event carries the value as it stands, so the replay
            // assigns rather than accumulates and can never drift from the sim by a rounding.
            case BattleEventKind.DefenceNow:
                _defenceNow[e.Slot] = e.Amount;
                break;
            case BattleEventKind.AttackBreak:
                if (e.Slot < 0) _breakAll = Math.Min(0, e.Amount);
                else if (e.Slot == 0) _breakFront = Math.Min(0, e.Amount);
                else _breakSecond = Math.Min(0, e.Amount);
                break;
            case BattleEventKind.Slowed:
                _slowPercent = Math.Max(0, e.Amount);
                break;
            case BattleEventKind.Staggered:
                _staggerMs = e.Amount;
                _staggerUntilMs = e.AtMs + e.Amount;
                break;
            case BattleEventKind.Marked:
                _markPercent = e.Slot;
                _markUntilMs = e.AtMs + e.Amount;
                break;
        }
    }
}

/// <summary>
/// A Reaction's readiness at one moment of the replay (<see cref="WaveReplay.ReactionReadinessAt"/>): armed, or
/// rearming from <see cref="FromMs"/> to <see cref="ReadyMs"/> (wave ms; FromMs may be negative, a rearm begun in
/// the previous wave) with <see cref="Progress"/> of it done.
/// </summary>
public readonly record struct ReactionReadiness(bool IsRearming, float Progress, int FromMs, int ReadyMs)
{
    /// <summary>Armed: nothing to wait for.</summary>
    public static ReactionReadiness Armed => new(false, 1f, 0, 0);

    /// <summary>Rearming over [<paramref name="fromMs"/>, <paramref name="readyMs"/>], seen at <paramref name="ms"/>.</summary>
    public static ReactionReadiness Rearming(int fromMs, int readyMs, float ms)
        => new(true, Math.Clamp((ms - fromMs) / Math.Max(1f, readyMs - fromMs), 0f, 1f), fromMs, readyMs);
}
