using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
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

    /// <summary>The skill slots every character starts with. The FLOOR, not the rule — see <see cref="SkillCapacity"/>.</summary>
    /// <remarks>
    /// This const used to be the rule, and that was the bug. <c>AddSkill</c> took
    /// <c>Math.Min(MaxSkills, SkillCapacity)</c>, so FIFTH WEAVE — three points on the trait spine —
    /// was bought, saved, resolved to 5, written into the capacity by a host line whose own comment
    /// reads "without this the fifth weave is bought and never granted", and then clamped straight back
    /// to 4 one call later. Nothing reads it as a ceiling any more.
    /// </remarks>
    public const int MaxSkills = Build.SkillSlots;        // 4 — a build is a CHOICE of Forms
    public const int MaxKeystones = Build.KeystoneSlots;  // 3 — the tree teaches fifteen, you wear three

    /// <summary>
    /// How many skill slots and keystone sockets the character currently HAS, set by the host from the
    /// trait tree's spine. The consts above are the hard ceiling; these are what is unlocked.
    /// </summary>
    /// <remarks>
    /// Host-set rather than resolved here, like every other cross-system value the loadout reads, and
    /// defaulted to the ceiling so a caller that never sets them (a test, a bench) behaves as before.
    /// A socket the player has not bought is the scarce thing the AVARICE and RUIN paths compete over —
    /// with three free sockets, learning a keystone was the only decision and wearing it was automatic.
    /// </remarks>
    public int SkillCapacity { get; set; } = MaxSkills;

    public int KeystoneCapacity { get; set; } = MaxKeystones;

    // ── Skill editing ───────────────────────────────────────────────────────────────────────────

    /// <summary>Add an empty-ish skill slot if there is room. Returns its index, or -1 when full.</summary>
    public int AddSkill()
    {
        if (_skills.Count >= SkillCapacity) return -1;
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

    // ── Direct setters. ───────────────────────────────────────────────────────────────────────────
    //
    // The cycling methods above exist because the editor used to be three `< VALUE >` cells, where the
    // only verb available is "next". That is a fine control for a list of two and a poor one for a list
    // of six: picking SPIRIT from BODY is five clicks and five reads, and nothing on screen says what
    // the other five even are. The weave editor shows all six and sets the one you point at.
    //
    // The cyclers stay — the gamepad path is cycle-and-confirm by design (technical-preferences.md),
    // and it needs a "next" that does not depend on pointing at a specific cell.

    /// <summary>Set a slot's Source outright.</summary>
    public void SetSource(int slot, Source source)
    {
        if (!InRange(slot)) return;
        _skills[slot] = _skills[slot] with { Source = source };
    }

    /// <summary>Set a slot's Form outright.</summary>
    public void SetForm(int slot, Form form)
    {
        if (!InRange(slot)) return;
        _skills[slot] = _skills[slot] with { Form = form };
    }

    /// <summary>
    /// Swear a Vow on a slot, or clear it with null. Refuses a Vow that has not been studied.
    /// </summary>
    /// <remarks>
    /// The guard is not defensive padding: a Vow is a permanent-feeling commitment bought from the
    /// trait tree, and a screen that could set one the player never learned would be granting the
    /// trait tree's reward for free.
    /// </remarks>
    public bool SetVow(int slot, string? vowId, IReadOnlyList<Vow> knownVows)
    {
        ArgumentNullException.ThrowIfNull(knownVows);
        if (!InRange(slot)) return false;
        if (vowId is not null && knownVows.All(v => v.Id != vowId)) return false;
        _skills[slot] = _skills[slot] with { VowId = vowId };
        return true;
    }

    // ── Keystone sockets ──────────────────────────────────────────────────────────────────────────

    public bool HasKeystone(string id) => _keystoneIds.Contains(id);

    /// <summary>Socket or unsocket a keystone the tree has taught. Bounded by <see cref="MaxKeystones"/>.</summary>
    public bool ToggleKeystone(string id, IReadOnlyList<Keystone> learned)
    {
        ArgumentNullException.ThrowIfNull(learned);

        if (_keystoneIds.Contains(id)) { _keystoneIds.Remove(id); return true; }
        if (_keystoneIds.Count >= Math.Min(MaxKeystones, KeystoneCapacity)) return false;   // no free socket
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
    /// <param name="character">
    /// The character being played, or null for no character at all.
    /// </param>
    /// <remarks>
    /// There is deliberately NO two-argument overload defaulting this to null. There was one, and the
    /// Stats screen called it — so the page whose entire job is to state the player's numbers computed
    /// them against a build the sim never runs, silently dropping the character's passive and aptitude.
    /// That is the second time this screen lost a whole layer that way; the first was the mastery tree,
    /// and <c>DrawDerived</c> still carries the comment about fixing it. An omitted argument is
    /// indistinguishable from a deliberate null at the call site, so the omission is no longer offered:
    /// callers that mean "no character" now have to say so.
    /// </remarks>
    public Build ToBuild(MemoryDustTree tree, MasteryTree mastery, Character? character)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(mastery);

        // The capacity travels WITH the build, because the sim asks about it: VOW OF COMPLETION wants
        // "no skill slot empty", and against the type's floor a player who owned the fifth weave met
        // that demand while looking at an empty fifth slot.

        // The build's passive numbers are the Dust tree, the Mastery tree AND the character, multiplied
        // together. Its affinity and its extra triggers come from the Mastery tree — the corner you took
        // and the notables you walked past to reach it — plus whatever the character grants outright.
        //
        // This is the ONE place a character reaches the simulation. Everything a character is — its
        // passive, its aptitude, its granted behaviours — arrives as the same three things the two trees
        // already contribute, so the sim never learns that characters exist and there is exactly one
        // seam to get wrong instead of one per effect.
        var build = new Build
        {
            PassiveMods = DustEffects.TreeMods(tree).Combine(mastery.Mods())
                                     .Combine(character?.Mods ?? BuildMods.None),
            Affinity = mastery.Affinity(),
            ExtraTriggers = new HashSet<BuildTrigger>(
                mastery.Triggers().Concat(character?.Grants ?? Array.Empty<BuildTrigger>())),
            Shape = SkillShape.Combine(mastery.Shape(), character?.TotalShape ?? SkillShape.None),
            SlotCapacity = SkillCapacity,
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
            // Take(SkillCapacity), not Take(MaxSkills) — a saved fifth skill would otherwise be dropped
            // on the way in. The host sets the capacity from the trait tree BEFORE calling this, which
            // is the ordering that makes the truncation right rather than merely smaller.
            foreach (var (src, form, vow) in skills.Take(SkillCapacity))
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
