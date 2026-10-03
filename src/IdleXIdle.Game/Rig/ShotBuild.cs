using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Game.Rig;

/// <summary>
/// RIG ONLY: <c>RH_SHOT_BUILD=&lt;skill&gt;[@Source],&lt;skill&gt;[@Source],...</c>, a take's FULL build (the remaining-skill
/// sweep's design.md section 9), applied after <c>RH_SHOT_HUNTER</c> and refused LOUDLY: the take exits non-zero rather
/// than film a build it did not pose.
/// </summary>
/// <remarks>
/// <para>
/// It replaces guessing what <c>RH_SHOT_SWAP</c> leaves behind. On the default fixture <c>RH_SHOT_HUNTER=quiver</c> sheds
/// HARD HANDS and <c>LoadoutRepair.EnsureSignature</c> returns -1 SILENTLY (SPRAY + PRESS + JAWS + BACKDRAW is three
/// passives), so THE QUIVER fought without BACKDRAW and the film looked perfectly plausible.
/// </para>
/// <para>
/// Pure, so the grammar and the refusals are tested without a GraphicsDevice: <see cref="Parse"/> reads the text,
/// <see cref="Resolve"/> finds each skill, <see cref="Refusal"/> asks the build rule, and <see cref="Mismatch"/> compares
/// the live run with what was asked (the check that catches a silent repair after the loadout accepted every write).
/// The signature holds slot one, as it does in the game (<c>PlayerLoadout.PinSignature</c>): a build that names it
/// elsewhere asks for a build the game cannot produce, and is refused.
/// </para>
/// </remarks>
public static class ShotBuild
{
    /// <summary>The environment variable this dial reads.</summary>
    public const string Variable = "RH_SHOT_BUILD";

    /// <summary>The Source a slot takes when the entry names none: a new slot's own (<c>PlayerLoadout.AddSkill</c>).</summary>
    public const Source DefaultSource = Source.Body;

    /// <summary>
    /// The entries of a build spec, in slot order: each skill id and the Source it was asked in (null: none named).
    /// Throws <see cref="InvalidOperationException"/> on an empty spec, an empty entry, a malformed entry or an unknown Source.
    /// </summary>
    public static IReadOnlyList<(string SkillId, Source? Source)> Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            throw new InvalidOperationException($"{Variable} is empty: it needs <skill>[@Source],<skill>[@Source],...");
        var entries = new List<(string, Source?)>();
        foreach (var raw in spec.Split(','))
        {
            var entry = raw.Trim();
            var parts = entry.Split('@');
            if (entry.Length == 0 || parts.Length > 2 || parts[0].Trim().Length == 0)
                throw new InvalidOperationException($"{Variable}='{spec}': the entry '{entry}' is not <skill>[@Source].");
            Source? source = null;
            if (parts.Length == 2)
            {
                var name = parts[1].Trim();
                // Enum.TryParse also accepts a NUMBER ("@7"), which would pose an undefined Source: names only.
                if (!Enum.TryParse<Source>(name, ignoreCase: true, out var s) || !Enum.IsDefined(s) || name.Any(char.IsDigit))
                    throw new InvalidOperationException(
                        $"{Variable}='{spec}': '{name}' is not a Source. Known: {string.Join(", ", Enum.GetNames<Source>())}.");
                source = s;
            }
            entries.Add((parts[0].Trim(), source));
        }
        return entries;
    }

    /// <summary>The catalogue def of every entry, in order; throws naming the first id the catalogue does not know.</summary>
    public static IReadOnlyList<SkillDef> Resolve(IReadOnlyList<(string SkillId, Source? Source)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var defs = new List<SkillDef>(entries.Count);
        foreach (var (id, _) in entries)
            defs.Add(SkillCatalogue.Find(id)
                     ?? throw new InvalidOperationException($"{Variable}: '{id}' is not a skill id."));
        return defs;
    }

    /// <summary>
    /// Why this build cannot be posed for a champion whose signature is <paramref name="signatureId"/>, or null when it can:
    /// more skills than <paramref name="capacity"/>, a skill named twice, the signature missing or not in slot one, or the
    /// ONE legality rule (<see cref="Build.WouldRefuse"/>: at most two Active and two Passive, a passive being a Field or a
    /// Reaction) refusing a skill. The kind counts are the whole build's, so the sentence names how many there were.
    /// </summary>
    public static string? Refusal(IReadOnlyList<SkillDef> defs, string? signatureId, int capacity)
    {
        ArgumentNullException.ThrowIfNull(defs);
        if (defs.Count == 0) return $"{Variable} names no skill.";
        if (defs.Count > capacity)
            return $"{Variable} names {defs.Count} skills: a build holds at most {capacity}.";
        var twice = defs.GroupBy(d => d.Id).FirstOrDefault(g => g.Count() > 1);
        if (twice is not null) return $"{Variable} names '{twice.Key}' twice: a skill takes one slot (LAW 13).";
        if (signatureId is null) return $"{Variable}: the posed champion has no signature to place.";
        var at = defs.ToList().FindIndex(d => d.Id == signatureId);
        if (at < 0) return $"{Variable} leaves out the champion's signature '{signatureId}': it must be among the skills.";
        if (at != 0) return $"{Variable} puts the signature '{signatureId}' in slot {at + 1}: the signature holds slot one.";

        int actives = 0, passives = 0;
        string? refused = null;
        foreach (var d in defs)
        {
            if (refused is null && Build.WouldRefuse(capacity, actives, passives, d.TakesABeat) is var why
                && why is SkillRefusal.ActivesFull or SkillRefusal.PassivesFull or SkillRefusal.TotalFull)
                refused = why.ToString();
            if (d.TakesABeat) actives++; else passives++;
        }
        if (refused is null) return null;
        var kinds = $"{actives} Active ({Names(defs, true)}) and {passives} passives ({Names(defs, false)})";
        return $"{Variable} is refused by the build rule ({refused}): it holds {kinds}; a build holds at most "
               + $"{Build.MaxActiveSkills} Active and {Build.MaxPassiveSkills} passives (a Field or a Reaction).";
    }

    /// <summary>
    /// Where the live run differs from the asked build, or null when it is the same: the same skills in the same slots,
    /// each in the asked Source (or <see cref="DefaultSource"/> when none was named). A chosen variation owns its Source
    /// in the game (<c>BuildComposer</c>), so it is accepted when no Source was named and refused when another was.
    /// </summary>
    public static string? Mismatch(IReadOnlyList<(string SkillId, Source? Source)> asked, IReadOnlyList<EquippedSkill> live)
    {
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentNullException.ThrowIfNull(live);
        var liveText = string.Join(",", live.Select(s => $"{s.Def.Id}@{s.Source}"));
        if (live.Count != asked.Count)
            return $"{Variable}: the live run carries {live.Count} skills ({liveText}), not the {asked.Count} asked.";
        for (var i = 0; i < asked.Count; i++)
        {
            var (id, source) = asked[i];
            var run = live[i];
            if (run.Def.Id != id)
                return $"{Variable}: slot {i + 1} of the live run is '{run.Def.Id}', not '{id}' ({liveText}).";
            if (run.Variation is not null)
            {
                if (source is { } named && named != run.Source)
                    return $"{Variable}: '{id}' was asked @{named}, but its variation {run.Variation.Name} owns the Source "
                           + $"({run.Source}); name @{run.Source} or none.";
                continue;
            }
            var want = source ?? DefaultSource;
            if (run.Source != want)
                return $"{Variable}: '{id}' fights @{run.Source} in the live run, not @{want} ({liveText}).";
        }
        return null;
    }

    private static string Names(IReadOnlyList<SkillDef> defs, bool active)
        => string.Join(", ", defs.Where(d => d.TakesABeat == active).Select(d => d.Id));
}
