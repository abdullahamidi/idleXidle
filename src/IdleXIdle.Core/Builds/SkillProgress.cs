using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// What each skill has earned by being used, and what the player has spent it on.
/// </summary>
/// <remarks>
/// <para>
/// The second of the game's two progression currencies, and it answers a different question from the
/// first. Mastery points come from DEPTH and buy the champion — which styles open, how durable it is.
/// A skill's levels come from USING THAT SKILL and buy its identity: one of two variations, then that
/// variation's three reinforcements. Four purchases in all.
/// See <c>design/gdd/skill-slots-and-skill-trees.md</c> §5.
/// </para>
/// <para>
/// <b>Levels are never lost.</b> Unequipping a skill keeps everything it earned and coming back to it
/// resumes where you left off; respec inside a skill is free. The loudest complaint about the system
/// this is modelled on is that per-skill progress makes players afraid to change anything, and the
/// cost of switching here is time-to-catch-up rather than destroyed progress.
/// </para>
/// <para>
/// <b>Use is counted per WAVE CLEARED while equipped</b>, not per activation. An active casts a few
/// times a wave and a Field ticks a dozen times, so counting activations would level a passive three
/// times faster for doing the same job — and the player did not choose the tick rate, the catalogue
/// did. A wave is the unit both kinds share.
/// </para>
/// </remarks>
public sealed class SkillProgress
{
    /// <summary>Waves cleared with each skill equipped. The raw currency.</summary>
    private readonly Dictionary<string, int> _uses = new();

    /// <summary>Which of a skill's two variations the player took.</summary>
    private readonly Dictionary<string, string> _variation = new();

    /// <summary>Which reinforcements of that variation are bought.</summary>
    private readonly Dictionary<string, HashSet<string>> _taken = new();

    /// <summary>The most a single skill can be levelled: one variation and its three reinforcements.</summary>
    public const int MaxLevel = 4;

    /// <summary>
    /// Waves-to-level, on a square root so the next level is always further than the last.
    /// </summary>
    /// <remarks>
    /// Level 1 at 4 waves, 2 at 16, 3 at 36, 4 at 64. The first choice — which variation this skill
    /// IS — arrives almost immediately, because a skill whose identity is still unchosen after an
    /// hour is a skill the player has not really met. The reinforcements are the long part.
    /// </remarks>
    public static int LevelFor(int uses) => Math.Min(MaxLevel, (int)MathF.Floor(MathF.Sqrt(Math.Max(0, uses)) / 2f));

    /// <summary>Waves needed to reach a level, for a readout that has to say "how much further".</summary>
    public static int UsesForLevel(int level) => (2 * Math.Clamp(level, 0, MaxLevel)) * (2 * Math.Clamp(level, 0, MaxLevel));

    public int UsesOf(string skillId) => _uses.GetValueOrDefault(skillId);

    /// <summary>Every skill's cleared-wave tally — the career fact the quest layer reads (P10).</summary>
    public IReadOnlyDictionary<string, int> UsesBySkill() => new Dictionary<string, int>(_uses);

    public int LevelOf(string skillId) => LevelFor(UsesOf(skillId));

    /// <summary>Levels already committed — the variation counts as one, each reinforcement as one.</summary>
    public int SpentOn(string skillId)
        => (_variation.ContainsKey(skillId) ? 1 : 0) + (_taken.GetValueOrDefault(skillId)?.Count ?? 0);

    /// <summary>Levels earned and not yet spent.</summary>
    public int FreeOn(string skillId) => Math.Max(0, LevelOf(skillId) - SpentOn(skillId));

    /// <summary>Record a wave cleared with this skill equipped.</summary>
    public void RecordWave(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        _uses[skillId] = UsesOf(skillId) + 1;
    }

    /// <summary>The variation this skill was taken as, or null while it is still unchosen.</summary>
    public SkillVariation? VariationOf(SkillDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        return _variation.TryGetValue(def.Id, out var name)
            ? def.Variations.FirstOrDefault(v => v.Name == name)
            : null;
    }

    /// <summary>Has this reinforcement been bought?</summary>
    public bool HasReinforcement(string skillId, string name)
        => _taken.GetValueOrDefault(skillId)?.Contains(name) == true;

    /// <summary>
    /// Take one of the skill's two variations. Refused if a level is not free, or one is already taken.
    /// </summary>
    /// <remarks>
    /// A variation is a COMMITMENT rather than a toggle: its three reinforcements belong to it and are
    /// worthless to the other one, so swapping would silently strand everything bought after it. Use
    /// <see cref="Respec"/>, which is free and gives the levels back.
    /// </remarks>
    public bool ChooseVariation(SkillDef def, string variationName)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (_variation.ContainsKey(def.Id)) return false;
        if (def.Variations.All(v => v.Name != variationName)) return false;
        if (FreeOn(def.Id) < 1) return false;
        _variation[def.Id] = variationName;
        return true;
    }

    /// <summary>Buy one reinforcement of the chosen variation.</summary>
    public bool TakeReinforcement(SkillDef def, string name)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (VariationOf(def) is not { } v) return false;                 // the variation comes first
        if (v.Reinforcements.All(r => r.Name != name)) return false;     // and it must be ITS reinforcement
        if (FreeOn(def.Id) < 1) return false;
        var set = _taken.TryGetValue(def.Id, out var existing) ? existing : _taken[def.Id] = new HashSet<string>();
        return set.Add(name);
    }

    /// <summary>Give a skill's levels back. Free, and it keeps every level it earned.</summary>
    public void Respec(string skillId)
    {
        _variation.Remove(skillId);
        _taken.Remove(skillId);
    }

    // ── PERSISTENCE. Flattened, so a save that predates a skill simply carries no row for it. ────
    /// <summary>
    /// A fingerprint of every choice that changes a composed build — which variation each skill
    /// took and which reinforcements are bought. The hunt folds it into its build stamp, so buying
    /// a reinforcement mid-descent re-composes the fight at the next wave boundary; leaving it out
    /// was how the whole variation layer shipped dormant (audit 2026-08-31, finding #2).
    /// </summary>
    public string Signature =>
        string.Join(";", ToSave()
            .Where(r => r.Variation is not null || r.Taken.Count > 0)
            .OrderBy(r => r.SkillId, StringComparer.Ordinal)
            .Select(r => $"{r.SkillId}:{r.Variation}:{string.Join(",", r.Taken)}"));

    public IReadOnlyList<(string SkillId, int Uses, string? Variation, IReadOnlyList<string> Taken)> ToSave()
        => _uses.Keys
            .Union(_variation.Keys)
            .Union(_taken.Keys)
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => (id, UsesOf(id), _variation.GetValueOrDefault(id),
                           (IReadOnlyList<string>)(_taken.GetValueOrDefault(id)?.OrderBy(x => x, StringComparer.Ordinal).ToList()
                                                   ?? new List<string>())))
            .ToList();

    public void Restore(IEnumerable<(string SkillId, int Uses, string? Variation, IReadOnlyList<string> Taken)> rows)
    {
        _uses.Clear(); _variation.Clear(); _taken.Clear();
        if (rows is null) return;
        foreach (var (id, uses, variation, taken) in rows)
        {
            // A SKILL THAT NO LONGER EXISTS IS DROPPED, not carried. The catalogue is code and a save
            // outlives it; silently keeping a row for a deleted skill would let it come back to life
            // under a reused id.
            if (SkillCatalogue.Find(id) is not { } def) continue;
            if (uses > 0) _uses[id] = uses;
            if (variation is not null && def.Variations.Any(v => v.Name == variation))
            {
                _variation[id] = variation;
                var valid = def.Variations.First(v => v.Name == variation).Reinforcements.Select(r => r.Name).ToHashSet();
                var kept = (taken ?? Array.Empty<string>()).Where(valid.Contains).ToHashSet();
                if (kept.Count > 0) _taken[id] = kept;
            }
        }
    }
}
