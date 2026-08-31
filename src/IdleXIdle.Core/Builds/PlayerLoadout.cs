using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;

namespace IdleXIdle.Core.Builds;

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
    /// <summary>
    /// One woven skill: its element, its style's Form, its Vow, and WHICH SLOT it sits in.
    /// </summary>
    /// <remarks>
    /// <b>Passive is the player's choice, not a consequence.</b> A style has two skills and the slot
    /// decides which one this is — HAMMER's BLOW in an active slot, its PRESS in a passive one. It
    /// defaults to false so every construction written before the rework still means what it meant,
    /// and so a save that predates the field loads with the composer's spill rule sorting the
    /// overflow exactly as it used to.
    /// </remarks>
    /// <summary>
    /// One woven slot. <see cref="SkillId"/> is what the player chose; the rest is history.
    /// </summary>
    /// <remarks>
    /// A slot used to BE a Source and a Form, and the skill was composed from the pair. The designer
    /// retired that on 2026-08-30 — a skill is learned on the mastery tree now and has its own depth —
    /// so the slot names the skill outright. <c>Source</c> and <c>Form</c> stay for one reason each:
    /// Form is how a save written before today says which skill it meant, and Source is the fallback
    /// element for a skill whose variation has not been chosen yet (the variation owns the Source now).
    /// </remarks>
    public sealed record SkillChoice(Source Source, Form Form, string? VowId, bool? Passive = null,
                                     string? SkillId = null);

    private readonly List<SkillChoice> _skills = new();
    private readonly List<string> _keystoneIds = new();

    public IReadOnlyList<SkillChoice> Skills => _skills;

    /// <summary>
    /// A fingerprint of every choice that feeds <see cref="ToBuild"/>: the woven skills, the socketed
    /// keystones, the capacities. The hunt compares it at each wave boundary and re-composes the fight's
    /// build when it changed — no revision counter to forget in a mutator.
    /// </summary>
    public string Signature =>
        string.Join(";", _skills.Select(s => $"{s.SkillId ?? $"{s.Source}:{s.Form}"}:{s.VowId}")) + "|" + string.Join(",", _keystoneIds)
        + $"|{SkillCapacity}|{KeystoneCapacity}";
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

    /// <summary>
    /// Move a woven skill to another position in the list. Returns false when nothing moved.
    /// </summary>
    /// <remarks>
    /// SLOT ORDER IS A REAL DECISION, not presentation, which is why this exists at all. The champion
    /// takes ONE action per beat and picks the first READY skill in slot order
    /// (<c>SoloBattle.ResolveWave</c>: "a ready skill that loses the beat to an earlier slot stays ready
    /// and takes the next"), so the top of this list is the skill that wins ties. Until 2026-08-28 the
    /// only way to reorder was to unweave and re-weave everything below, which is why the priority read
    /// as something the game did to you rather than something you set.
    /// </remarks>
    public bool MoveSkill(int from, int to)
    {
        if (!InRange(from)) return false;
        to = Math.Clamp(to, 0, _skills.Count - 1);
        if (from == to) return false;
        var s = _skills[from];
        _skills.RemoveAt(from);
        _skills.Insert(to, s);
        return true;
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
    /// Put a named SKILL in a slot — what picking one from the library does.
    /// </summary>
    /// <remarks>
    /// It writes the Form and the kind alongside the id, so every reader that still speaks Form (the
    /// hunt rail's art, the affinity hexagon, an old save round-trip) keeps working while the id is
    /// what actually decides the skill.
    /// </remarks>
    public void SetSkill(int slot, string skillId)
    {
        if (!InRange(slot) || SkillCatalogue.Find(skillId) is not { } def) return;
        var form = def.LegacyForm
                   ?? (def.TakesABeat ? SkillCatalogue.PassiveOf(def.Style) : SkillCatalogue.ActiveOf(def.Style))
                       .LegacyForm!.Value;
        _skills[slot] = _skills[slot] with { SkillId = def.Id, Form = form, Passive = !def.TakesABeat };
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
    public Build ToBuild(MemoryDustTree tree, MasteryTree mastery, Character? character,
                         SkillProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(mastery);

        // The capacity travels WITH the build, because the sim asks about it: VOW OF COMPLETION wants
        // "no skill slot empty", and against the type's floor a player who owned the fifth weave met
        // that demand while looking at an empty fifth slot.

        // The assembly itself lives in Core (BuildComposer) so what reaches the sim can be tested; this
        // only gathers the loadout's lists.
        return BuildComposer.Compose(tree, mastery, character,
            _skills.Select(s => new BuildComposer.SkillPick(s.Source, s.Form, s.VowId, NameOf(s), s.Passive, s.SkillId)),
            _keystoneIds, SkillCapacity, progress);
    }

    /// <summary>A human name for a woven skill — "BODY STRIKE", "SHADOW TRAP".</summary>
    public static string NameOf(SkillChoice s) =>
        $"{s.Source.ToString().ToUpperInvariant()} {s.Form.ToString().ToUpperInvariant()}";

    // ── Persistence (id-based, so it survives a reload) ─────────────────────────────────────────────

    public IReadOnlyList<(string? SkillId, string Source, string Form, string? VowId, bool? Passive)> SaveSkills() =>
        _skills.Select(s => (s.SkillId, s.Source.ToString(), s.Form.ToString(), s.VowId, s.Passive)).ToList();

    public void Restore(
        IEnumerable<(string? SkillId, string? Source, string? Form, string? VowId, bool? Passive)> skills,
        IEnumerable<string> keystoneIds)
    {
        _skills.Clear();
        _keystoneIds.Clear();
        if (skills is not null)
            // Take(SkillCapacity), not Take(MaxSkills) — a saved fifth skill would otherwise be dropped
            // on the way in. The host sets the capacity from the trait tree BEFORE calling this, which
            // is the ordering that makes the truncation right rather than merely smaller.
            foreach (var (id, src, form, vow, passive) in skills.Take(SkillCapacity))
            {
                var sourceOk = Enum.TryParse<Source>(src, out var s);
                var formOk = Enum.TryParse<Form>(form, out var f);
                if (id is not null && SkillCatalogue.Find(id) is { } def)
                {
                    // A v3 row: the id IS the identity. The Form is only the legacy echo the old
                    // readers still want; when it is absent it is derived exactly as SetSkill
                    // writes it, so the id can never disagree with the echo beside it.
                    var echo = formOk
                        ? f
                        : def.LegacyForm
                          ?? (def.TakesABeat ? SkillCatalogue.PassiveOf(def.Style) : SkillCatalogue.ActiveOf(def.Style))
                              .LegacyForm!.Value;
                    _skills.Add(new SkillChoice(sourceOk ? s : Source.Body, echo, vow,
                                                passive ?? !def.TakesABeat, def.Id));
                    continue;
                }
                // A pre-v3 row, or an id this build does not know riding beside a readable Form:
                // the Form-era identity. Neither an id nor a readable (Source, Form) means there is
                // nothing to restore — the row is dropped, like every unknown catalogue name.
                if (sourceOk && formOk) _skills.Add(new SkillChoice(s, f, vow, passive));
            }

        // THE MIGRATION, run once per load: every id-less slot is pinned to the SkillId it has
        // always meant. The effective slot kind comes from the same walk the composer uses —
        // SlotKinds, budget spill included — so a pre-rework four-active build migrates onto
        // exactly the skills it has been fighting with, never onto four actives it cannot hold.
        if (_skills.Any(sk => sk.SkillId is null))
        {
            var picks = _skills.Select(sk =>
                new BuildComposer.SkillPick(sk.Source, sk.Form, sk.VowId, NameOf(sk), sk.Passive, sk.SkillId)).ToList();
            var passiveKinds = BuildComposer.SlotKinds(picks, SkillCapacity);
            for (var i = 0; i < _skills.Count; i++)
                if (_skills[i].SkillId is null
                    && LegacySkillForm.Resolve(_skills[i].Form.ToString(), passiveKinds[i]) is { } migrated)
                    _skills[i] = _skills[i] with { SkillId = migrated };
        }

        if (keystoneIds is not null)
            _keystoneIds.AddRange(keystoneIds.Take(MaxKeystones));
    }

    /// <summary>
    /// A functional starter build so a new character can fight before opening the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONE skill. This used to hand out four.</b> The old version added a Strike, a Projectile, a
    /// Transformation and a Mark, on the reasoning that it showed off the Form axis the whole model
    /// rests on. Playtest: <i>"4 skill açık şekilde başladığımı gördüm. Skillerin açıklamaları yok, ne
    /// olduklarını anlamadım."</i> — four skills open at the start, unexplained, and none of them
    /// understood. Four systems arriving at once does not demonstrate an axis; it hides it, because
    /// nothing on screen can be attributed to anything.
    /// </para>
    /// <para>
    /// A Body Strike, because it is the most legible thing in the game: one heavy hit, on one target,
    /// with a visible windup. The other three slots arrive one at a time with their own explanation —
    /// see <c>Unlocks.SkillSlots</c> for the gates and <c>Unlocks.SkillSlotNote</c> for what each says.
    /// No Vow, no keystone — the refusals are for the player to make, not to inherit.
    /// </para>
    /// </remarks>
    public static PlayerLoadout Starter()
    {
        var l = new PlayerLoadout();
        l._skills.Add(new SkillChoice(Source.Body, Form.Strike, null, null, "hammer_blow"));
        return l;
    }

    /// <summary>
    /// Move a woven skill between the active and passive slot — which changes WHICH SKILL it is.
    /// </summary>
    /// <remarks>
    /// This is the only way to reach two of the twelve: AURA and TRAP resolve to their styles'
    /// passives from either side, so FIELD's PULSE and SNARE's REPAY have no other door. It is also
    /// the decision the whole rework turns on — an active costs the champion an action and a passive
    /// never can — so it belongs to the player rather than to composition order.
    /// </remarks>
    public bool SetPassive(int slot, bool passive)
    {
        if (!InRange(slot)) return false;
        _skills[slot] = _skills[slot] with { Passive = passive };
        return true;
    }

    private bool InRange(int slot) => slot >= 0 && slot < _skills.Count;

    private static T Cycle<T>(T value, int dir) where T : struct, Enum
    {
        var vals = Enum.GetValues<T>();
        var i = Array.IndexOf(vals, value);
        return vals[((i + dir) % vals.Length + vals.Length) % vals.Length];
    }
}
