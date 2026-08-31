using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
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
    /// <summary>
    /// One woven slot. <see cref="SkillId"/> is what the player chose — null is an EMPTY slot.
    /// </summary>
    /// <remarks>
    /// A slot used to BE a Source and a Form, and the skill was composed from the pair; the Form
    /// column died with P3-final (a pre-v3 save's Form is understood once, at
    /// <see cref="Restore"/>, through <see cref="LegacySkillForm"/>). <c>Source</c> stays as the
    /// fallback element for a skill whose variation has not been chosen yet (the variation owns the
    /// Source now); <c>Passive</c> stays only as the save's echo of the slot the player once chose.
    /// </remarks>
    public sealed record SkillChoice(Source Source, string? VowId, bool? Passive = null,
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
        string.Join(";", _skills.Select(s => $"{s.SkillId ?? "-"}:{s.Source}:{s.VowId}")) + "|" + string.Join(",", _keystoneIds)
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

    /// <summary>
    /// Add an EMPTY skill slot if there is room. Returns its index, or -1 when full.
    /// </summary>
    /// <remarks>
    /// Empty means empty: the slot holds no skill until one is picked from the library. It used to
    /// hold a default Body Strike, and the screen then showed a skill the fight refused — the
    /// dormant-feature failure read backwards, and worse, because the player believes the screen.
    /// </remarks>
    public int AddSkill()
    {
        if (_skills.Count >= SkillCapacity) return -1;
        _skills.Add(new SkillChoice(Source.Body, null));
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

    /// <summary>
    /// Put a named SKILL in a slot — what picking one from the library does. The slot's kind is the
    /// skill's own; there is nothing else to write.
    /// </summary>
    public void SetSkill(int slot, string skillId)
    {
        if (!InRange(slot) || SkillCatalogue.Find(skillId) is not { } def) return;
        _skills[slot] = _skills[slot] with { SkillId = def.Id, Passive = !def.TakesABeat };
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
            _skills.Select(s => new BuildComposer.SkillPick(s.Source, s.VowId, s.Passive, s.SkillId)),
            _keystoneIds, SkillCapacity, progress);
    }

    // ── Persistence (id-based, so it survives a reload) ─────────────────────────────────────────────

    public IReadOnlyList<(string? SkillId, string Source, string? VowId, bool? Passive)> SaveSkills() =>
        _skills.Select(s => (s.SkillId, s.Source.ToString(), s.VowId, s.Passive)).ToList();

    public void Restore(
        IEnumerable<(string? SkillId, string? Source, string? Form, string? VowId, bool? Passive)> skills,
        IEnumerable<string> keystoneIds)
    {
        _skills.Clear();
        _keystoneIds.Clear();
        if (skills is not null)
        {
            // Take(SkillCapacity), not Take(MaxSkills) — a saved fifth skill would otherwise be dropped
            // on the way in. The host sets the capacity from the trait tree BEFORE calling this, which
            // is the ordering that makes the truncation right rather than merely smaller.
            var rows = skills.Take(SkillCapacity).ToList();

            // THE MIGRATION, run once per load: a pre-v3 row carries only (Form, Passive), and its
            // effective slot kind comes from LegacySkillForm's FROZEN walk — the composer's budget
            // spill as it stood the day the Form vocabulary was retired — so a pre-rework
            // four-active build lands on exactly the skills it has been fighting with, never on
            // four actives it cannot hold. The LIVE walk is free to evolve without rewriting
            // anyone's save a second time.
            var kinds = LegacySkillForm.SlotKinds(
                rows.Select(r => (r.Form, r.Passive, r.SkillId)).ToList(), SkillCapacity);

            for (var i = 0; i < rows.Count; i++)
            {
                var (id, src, form, vow, passive) = rows[i];
                var sourceOk = Enum.TryParse<Source>(src, out var s);
                var source = sourceOk ? s : Source.Body;
                if (SkillCatalogue.Find(id) is { } def)
                {
                    // A v3 row: the id IS the identity.
                    _skills.Add(new SkillChoice(source, vow, passive ?? !def.TakesABeat, def.Id));
                    continue;
                }
                // A pre-v3 row: the Form-era identity, understood ONE more time. Neither an id nor
                // a readable (Source, Form) means there is nothing to restore — the row is dropped,
                // like every unknown catalogue name.
                if (sourceOk && LegacySkillForm.Resolve(form, kinds[i]) is { } migrated
                    && SkillCatalogue.Find(migrated) is { } def2)
                    _skills.Add(new SkillChoice(source, vow, passive ?? !def2.TakesABeat, def2.Id));
            }
        }

        if (keystoneIds is not null)
            // THE SOCKET GATE, at last (P7). This truncated to the constant 3, so a save written
            // when three sockets were free kept wearing three keystones over ONE bought socket —
            // socket_2/socket_3 (7 spine points) bought nothing for that player and the sim
            // honoured the unpaid sockets. The host sets KeystoneCapacity from the trait tree
            // BEFORE calling this, exactly as it does SkillCapacity for the skill rows above.
            _keystoneIds.AddRange(keystoneIds.Take(Math.Min(MaxKeystones, KeystoneCapacity)));
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
        l._skills.Add(new SkillChoice(Source.Body, null, false, "hammer_blow"));
        return l;
    }

    /// <summary>
    /// The STYLES this loadout carries, one per slot that resolves — what the Forge's combo-enchant
    /// badge and the weave screen's "your gear is waiting for a …" hint read.
    /// </summary>
    public IReadOnlyList<Style> WovenStyles()
        => _skills.Select(sk => SkillCatalogue.Find(sk.SkillId))
                  .Where(d => d is not null)
                  .Select(d => d!.Style)
                  .ToList();

    private bool InRange(int slot) => slot >= 0 && slot < _skills.Count;

    private static T Cycle<T>(T value, int dir) where T : struct, Enum
    {
        var vals = Enum.GetValues<T>();
        var i = Array.IndexOf(vals, value);
        return vals[((i + dir) % vals.Length + vals.Length) % vals.Length];
    }
}
