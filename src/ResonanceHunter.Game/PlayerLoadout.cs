using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Client;

/// <summary>
/// The player's build, as CHOICES — the four woven skills and the sockets — before it is resolved.
/// </summary>
/// <remarks>
/// <para>
/// This is the editable, persistable half of a build: which Forms the player wove, which Source and Vow
/// each carries, and which keystones they socketed. <see cref="ToBuild"/> turns those choices into the
/// pure <see cref="Build"/> the sim runs, folding in the Dust tree's passive nodes.
/// </para>
/// <para>
/// It lives in the GAME layer, not Core.Builds, precisely because assembling it needs the
/// <see cref="MemoryDustTree"/> — for the tree's <see cref="DustEffects.TreeMods"/> and the keystones it
/// has taught. Core.Builds deliberately knows nothing about Prestige, so the tree→build wiring belongs
/// here, on the Prestige side of that one-way dependency.
/// </para>
/// </remarks>
public sealed class PlayerLoadout
{
    /// <summary>One woven skill, before it becomes an <see cref="EquippedSkill"/>. Vow is by id, so it saves.</summary>
    public sealed record SkillChoice(Source Source, Form Form, string? VowId);

    private readonly List<SkillChoice> _skills = new();
    private readonly List<string> _keystoneIds = new();

    public IReadOnlyList<SkillChoice> Skills => _skills;
    public IReadOnlyList<string> KeystoneIds => _keystoneIds;

    public const int MaxSkills = Build.SkillSlots;        // 4 — a build is a CHOICE of Forms
    public const int MaxKeystones = Build.KeystoneSlots;  // 3 — the tree teaches ten, you wear three

    // ── Skill editing ───────────────────────────────────────────────────────────────────────────

    /// <summary>Add an empty-ish skill slot if there is room. Returns its index, or -1 when full.</summary>
    public int AddSkill()
    {
        if (_skills.Count >= MaxSkills) return -1;
        _skills.Add(new SkillChoice(Source.Body, Form.Strike, null));
        return _skills.Count - 1;
    }

    public void RemoveSkill(int slot)
    {
        if (slot >= 0 && slot < _skills.Count) _skills.RemoveAt(slot);
    }

    public void CycleSource(int slot, int dir)
    {
        if (!InRange(slot)) return;
        _skills[slot] = _skills[slot] with { Source = Cycle(_skills[slot].Source, dir) };
    }

    public void CycleForm(int slot, int dir)
    {
        if (!InRange(slot)) return;
        _skills[slot] = _skills[slot] with { Form = Cycle(_skills[slot].Form, dir) };
    }

    /// <summary>Cycle the Vow through the studied ones plus "no vow". Only studied Vows are offerable.</summary>
    public void CycleVow(int slot, int dir, IReadOnlyList<Vow> knownVows)
    {
        ArgumentNullException.ThrowIfNull(knownVows);
        if (!InRange(slot)) return;

        // The options are: no vow, then each known vow, in order. Cycle across that list.
        var ids = new List<string?> { null };
        ids.AddRange(knownVows.Select(v => (string?)v.Id));

        var cur = ids.IndexOf(_skills[slot].VowId);
        if (cur < 0) cur = 0;
        var next = ((cur + dir) % ids.Count + ids.Count) % ids.Count;
        _skills[slot] = _skills[slot] with { VowId = ids[next] };
    }

    // ── Keystone sockets ──────────────────────────────────────────────────────────────────────────

    public bool HasKeystone(string id) => _keystoneIds.Contains(id);

    /// <summary>Socket or unsocket a keystone the tree has taught. Bounded by <see cref="MaxKeystones"/>.</summary>
    public bool ToggleKeystone(string id, IReadOnlyList<Keystone> learned)
    {
        ArgumentNullException.ThrowIfNull(learned);

        if (_keystoneIds.Contains(id)) { _keystoneIds.Remove(id); return true; }
        if (_keystoneIds.Count >= MaxKeystones) return false;      // three sockets, no more
        if (learned.All(k => k.Id != id)) return false;            // must be taught first
        _keystoneIds.Add(id);
        return true;
    }

    // ── Resolution ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Turn these choices into the <see cref="Build"/> the sim runs, against the current tree.
    /// </summary>
    /// <remarks>
    /// Rebuilt every time rather than cached, because the tree changes underneath it: buying a passive
    /// node must show up on the very next expedition without the player re-opening the editor. Keystones
    /// no longer taught (impossible today — purchases are permanent — but cheap to guard) are dropped.
    /// </remarks>
    public Build ToBuild(MemoryDustTree tree, MasteryTree mastery)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(mastery);

        // The build's passive numbers are the Dust tree AND the Mastery tree, multiplied together. Its
        // affinity and its extra triggers come from the Mastery tree — the corner you took and the
        // notables you walked past to reach it.
        var build = new Build
        {
            PassiveMods = DustEffects.TreeMods(tree).Combine(mastery.Mods()),
            Affinity = mastery.MasteryForm(),
            ExtraTriggers = mastery.Triggers(),
        };

        var learned = DustEffects.LearnedKeystones(tree);
        foreach (var id in _keystoneIds)
            if (learned.FirstOrDefault(k => k.Id == id) is { } k)
                build.Take(k);

        foreach (var s in _skills)
        {
            var vow = Weaving.ById(s.VowId);
            var ability = new WovenAbility { Name = NameOf(s), Source = s.Source, Form = s.Form, Vow = vow };
            build.Weave(new EquippedSkill(ability, FormBehaviour.BaseCooldownMs(s.Form)));
        }

        return build;
    }

    /// <summary>A human name for a woven skill — "BODY STRIKE", "SHADOW TRAP".</summary>
    public static string NameOf(SkillChoice s) =>
        $"{s.Source.ToString().ToUpperInvariant()} {s.Form.ToString().ToUpperInvariant()}";

    // ── Persistence (id-based, so it survives a reload) ─────────────────────────────────────────────

    public IReadOnlyList<(string Source, string Form, string? VowId)> SaveSkills() =>
        _skills.Select(s => (s.Source.ToString(), s.Form.ToString(), s.VowId)).ToList();

    public void Restore(
        IEnumerable<(string Source, string Form, string? VowId)> skills, IEnumerable<string> keystoneIds)
    {
        _skills.Clear();
        _keystoneIds.Clear();
        if (skills is not null)
            foreach (var (src, form, vow) in skills.Take(MaxSkills))
                if (Enum.TryParse<Source>(src, out var s) && Enum.TryParse<Form>(form, out var f))
                    _skills.Add(new SkillChoice(s, f, vow));
        if (keystoneIds is not null)
            _keystoneIds.AddRange(keystoneIds.Take(MaxKeystones));
    }

    /// <summary>
    /// A functional starter build so a new character can fight before opening the editor.
    /// </summary>
    /// <remarks>
    /// Four Forms that show off the axis the whole model rests on: a STRIKE for weight, a PROJECTILE for
    /// volume, a TRANSFORMATION for sustain, a MARK to amplify the rest. No Vow, no keystone — the
    /// refusals are for the player to make, not to inherit.
    /// </remarks>
    public static PlayerLoadout Starter()
    {
        var l = new PlayerLoadout();
        l._skills.Add(new SkillChoice(Source.Body, Form.Strike, null));
        l._skills.Add(new SkillChoice(Source.Mind, Form.Projectile, null));
        l._skills.Add(new SkillChoice(Source.Nature, Form.Transformation, null));
        l._skills.Add(new SkillChoice(Source.Spirit, Form.Mark, null));
        return l;
    }

    private bool InRange(int slot) => slot >= 0 && slot < _skills.Count;

    private static T Cycle<T>(T value, int dir) where T : struct, Enum
    {
        var vals = Enum.GetValues<T>();
        var i = Array.IndexOf(vals, value);
        return vals[((i + dir) % vals.Length + vals.Length) % vals.Length];
    }
}
