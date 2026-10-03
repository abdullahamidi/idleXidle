using System;
using System.Collections.Generic;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Game.Rig;

/// <summary>
/// RIG ONLY: <c>RH_SHOT_SEEK=&lt;predicate&gt;</c>, a take aimed at an EVENT of the wave rather than a guessed second (the
/// remaining-skill sweep's design.md section 9). The shutter lands <c>RH_SHOT_LEAD</c> seconds before the event the
/// predicate finds, through <c>HuntScreen.DevSeekBefore</c>; a predicate that finds NOTHING in the wave throws, so a take
/// of the wrong instant is never filmed.
/// </summary>
/// <remarks>
/// The grammar (ids and kinds as the trace prints them; <c>#n</c> is the n-th match, 1-based, the first by default):
/// <list type="bullet">
/// <item><c>skill:&lt;id&gt;[#n][+struck|+unstruck][+ontick]</c>: a Skill event of the slot that holds skill &lt;id&gt; in the
/// live run. <c>+struck</c>: its batch holds a Strike; <c>+unstruck</c>: it holds none (BACKDRAW's phantom). A Strike belongs
/// to the most recent Skill or Aura at its ms (the same-ms LAST-OWNER rule). <c>+ontick</c>: an Aura shares its ms (a
/// cast posed on a field tick).</item>
/// <item><c>aura:&lt;id&gt;[#n]</c>: an Aura (a field's tick) of the slot that holds &lt;id&gt;.</item>
/// <item><c>down[#n]</c>: an EnemyDown (a creature falls).</item>
/// <item><c>hit:&lt;HitSource&gt;[#n]</c>: a Strike whose provenance is that <see cref="HitSource"/> (Bleed, Carry, ...).</item>
/// <item><c>downing[#n]</c>: an EnemyStrike sharing its ms with the champion's Down or Undying (the bite that fells).</item>
/// <item><c>beat:&lt;n&gt;</c>: the n-th Beat.</item>
/// <item><c>event:&lt;BattleEventKind&gt;[#n]</c>: any event of that kind.</item>
/// </list>
/// </remarks>
public sealed class ShotSeek
{
    /// <summary>The environment variable this dial reads.</summary>
    public const string Variable = "RH_SHOT_SEEK";

    /// <summary>What a seek looks for.</summary>
    public enum Predicate
    {
        /// <summary>A Skill event of a skill's slot.</summary>
        Skill,
        /// <summary>An Aura (field tick) of a skill's slot.</summary>
        Aura,
        /// <summary>An EnemyDown.</summary>
        Down,
        /// <summary>A Strike of one provenance.</summary>
        Hit,
        /// <summary>An EnemyStrike sharing its ms with Down or Undying.</summary>
        Downing,
        /// <summary>The n-th Beat.</summary>
        Beat,
        /// <summary>Any event of one kind.</summary>
        Event,
    }

    private ShotSeek(string spec) => Spec = spec;

    /// <summary>The text this seek was parsed from.</summary>
    public string Spec { get; }

    /// <summary>What it looks for.</summary>
    public Predicate What { get; private init; }

    /// <summary>The skill id of a <see cref="Predicate.Skill"/> or <see cref="Predicate.Aura"/> seek.</summary>
    public string? SkillId { get; private init; }

    /// <summary>The provenance of a <see cref="Predicate.Hit"/> seek.</summary>
    public HitSource? Hit { get; private init; }

    /// <summary>The kind of an <see cref="Predicate.Event"/> seek.</summary>
    public BattleEventKind? Kind { get; private init; }

    /// <summary>Which match, 1-based.</summary>
    public int Nth { get; private init; } = 1;

    /// <summary>A Skill seek's batch condition: true +struck, false +unstruck, null either.</summary>
    public bool? Struck { get; private init; }

    /// <summary>A Skill seek that must share its ms with an Aura.</summary>
    public bool OnTick { get; private init; }

    /// <summary>Read a seek; throws <see cref="InvalidOperationException"/> naming what is wrong with it.</summary>
    public static ShotSeek Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) throw Bad(spec ?? "", "it is empty");
        var text = spec.Trim();
        // #n may close the head or the modifiers (skill:x#2+struck and skill:x+struck#2 read the same); once only
        var nth = 1;
        var hash = text.IndexOf('#');
        var body = text;
        if (hash >= 0)
        {
            var end = text.IndexOf('+', hash);
            var digits = end < 0 ? text[(hash + 1)..] : text[(hash + 1)..end];
            if (!int.TryParse(digits, System.Globalization.NumberStyles.None,
                              System.Globalization.CultureInfo.InvariantCulture, out nth) || nth < 1)
                throw Bad(text, $"'#{digits}' is not #n with n >= 1");
            body = text[..hash] + (end < 0 ? "" : text[end..]);
            if (body.Contains('#')) throw Bad(text, "#n is given twice");
        }
        var plus = body.Split('+');
        var head = plus[0];
        string[] mods = plus[1..];
        var colon = head.IndexOf(':');
        var word = (colon >= 0 ? head[..colon] : head).Trim().ToLowerInvariant();
        var arg = colon >= 0 ? head[(colon + 1)..].Trim() : null;

        if (word != "skill" && mods.Length > 0) throw Bad(text, "only skill:<id> takes +struck / +unstruck / +ontick");
        switch (word)
        {
            case "skill":
            {
                if (string.IsNullOrEmpty(arg)) throw Bad(text, "skill: needs a skill id");
                bool? struck = null;
                var onTick = false;
                foreach (var m in mods)
                    switch (m.Trim().ToLowerInvariant())
                    {
                        case "struck" when struck is null: struck = true; break;
                        case "unstruck" when struck is null: struck = false; break;
                        case "ontick" when !onTick: onTick = true; break;
                        default: throw Bad(text, $"'+{m}' is not +struck, +unstruck or +ontick (each once, one of the first two)");
                    }
                return new ShotSeek(text) { What = Predicate.Skill, SkillId = arg, Nth = nth, Struck = struck, OnTick = onTick };
            }
            case "aura":
                if (string.IsNullOrEmpty(arg)) throw Bad(text, "aura: needs a skill id");
                return new ShotSeek(text) { What = Predicate.Aura, SkillId = arg, Nth = nth };
            case "down" when arg is null:
                return new ShotSeek(text) { What = Predicate.Down, Nth = nth };
            case "downing" when arg is null:
                return new ShotSeek(text) { What = Predicate.Downing, Nth = nth };
            case "hit":
                if (!TryName<HitSource>(arg, out var hit))
                    throw Bad(text, $"'{arg}' is not a HitSource ({string.Join(", ", Enum.GetNames<HitSource>())})");
                return new ShotSeek(text) { What = Predicate.Hit, Hit = hit, Nth = nth };
            case "beat":
                if (hash >= 0) throw Bad(text, "beat:<n> takes no #n");
                if (!int.TryParse(arg, System.Globalization.NumberStyles.None,
                                  System.Globalization.CultureInfo.InvariantCulture, out var beat) || beat < 1)
                    throw Bad(text, "beat: needs a number n >= 1");
                return new ShotSeek(text) { What = Predicate.Beat, Nth = beat };
            case "event":
                if (!TryName<BattleEventKind>(arg, out var kind))
                    throw Bad(text, $"'{arg}' is not a BattleEventKind");
                return new ShotSeek(text) { What = Predicate.Event, Kind = kind, Nth = nth };
            default:
                throw Bad(text, "it is not skill:, aura:, down, hit:, downing, beat: or event:");
        }
    }

    /// <summary>
    /// The ms of the event this seek aims at in <paramref name="events"/> (one wave, in order), or null when the wave has
    /// none. <paramref name="slotOf"/> maps a skill id to its slot in the live run (-1: not woven, so nothing matches).
    /// </summary>
    public int? Find(IReadOnlyList<BattleEvent> events, Func<string, int> slotOf)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(slotOf);
        var slot = SkillId is { } id ? slotOf(id) : -1;
        if (SkillId is not null && slot < 0) return null;
        var seen = 0;
        for (var i = 0; i < events.Count; i++)
        {
            if (!Matches(events, i, slot)) continue;
            if (++seen == Nth) return events[i].AtMs;
        }
        return null;
    }

    private bool Matches(IReadOnlyList<BattleEvent> events, int i, int slot)
    {
        var e = events[i];
        switch (What)
        {
            case Predicate.Skill:
                if (e.Kind != BattleEventKind.Skill || e.Slot != slot) return false;
                if (Struck is { } want && BatchHasStrike(events, i) != want) return false;
                return !OnTick || SharesMsWith(events, i, BattleEventKind.Aura, BattleEventKind.Aura);
            case Predicate.Aura:
                return e.Kind == BattleEventKind.Aura && e.Slot == slot;
            case Predicate.Down:
                return e.Kind == BattleEventKind.EnemyDown;
            case Predicate.Hit:
                return e.Kind == BattleEventKind.Strike && e.Hit == Hit;
            case Predicate.Downing:
                return e.Kind == BattleEventKind.EnemyStrike
                       && SharesMsWith(events, i, BattleEventKind.Down, BattleEventKind.Undying);
            case Predicate.Beat:
                return e.Kind == BattleEventKind.Beat;
            default:
                return e.Kind == Kind;
        }
    }

    /// <summary>The batch of the owner at <paramref name="owner"/>: the events after it at its ms, up to the next Skill or Aura.</summary>
    private static bool BatchHasStrike(IReadOnlyList<BattleEvent> events, int owner)
    {
        var ms = events[owner].AtMs;
        for (var j = owner + 1; j < events.Count && events[j].AtMs == ms; j++)
        {
            if (events[j].Kind is BattleEventKind.Skill or BattleEventKind.Aura) return false;
            if (events[j].Kind == BattleEventKind.Strike) return true;
        }
        return false;
    }

    private static bool SharesMsWith(IReadOnlyList<BattleEvent> events, int i, BattleEventKind a, BattleEventKind b)
    {
        var ms = events[i].AtMs;
        for (var j = 0; j < events.Count; j++)
            if (j != i && events[j].AtMs == ms && (events[j].Kind == a || events[j].Kind == b)) return true;
        return false;
    }

    private static bool TryName<T>(string? name, out T value) where T : struct, Enum
    {
        value = default;
        return !string.IsNullOrEmpty(name) && !char.IsDigit(name[0]) && !name.StartsWith('-')
               && Enum.TryParse(name, ignoreCase: true, out value) && Enum.IsDefined(value);
    }

    private static InvalidOperationException Bad(string spec, string why)
        => new($"{Variable}='{spec}' cannot be posed: {why}. Grammar: skill:<id>[#n][+struck|+unstruck][+ontick], "
               + "aura:<id>[#n], down[#n], hit:<HitSource>[#n], downing[#n], beat:<n>, event:<BattleEventKind>[#n].");

    /// <inheritdoc />
    public override string ToString() => Spec;
}
