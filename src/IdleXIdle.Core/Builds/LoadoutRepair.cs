using System;
using System.Collections.Generic;
using IdleXIdle.Core.Characters;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// Which woven slots hold a skill this hunter may not actually use, and how to empty exactly those.
/// </summary>
/// <remarks>
/// <para>
/// Access stopped being permanent (BRIEF sec.15-17). A respec now takes back the road that unlocked a
/// shared skill, and a champion switch leaves the previous champion's signature sitting in a slot that
/// only they could ever use. In both cases <see cref="BuildComposer"/> silently stops carrying that
/// slot: the fight is correct, and the player is told nothing and sees a slot they cannot use.
/// </para>
/// <para>
/// This is the one place that answers "may this slot fight?", and it answers it with the composer's own
/// two rules in the composer's own order — ownership first, then current mastery access — so the screen
/// that warns, the host that repairs and the simulation that fights can never disagree about which slots
/// are affected. It never touches <see cref="SkillProgress"/>: LAW 4 is that experience is permanent
/// even when access is not, and a repair that cost the player their waves would be the worst reading of
/// sec.19 available.
/// </para>
/// </remarks>
public static class LoadoutRepair
{
    /// <summary>
    /// True when a champion may put this skill in a slot and have the fight honour it.
    /// </summary>
    /// <param name="def">The skill.</param>
    /// <param name="available">The shared skills the CURRENT mastery allocation reaches.</param>
    /// <param name="character">Who is fighting, or null when nobody is.</param>
    public static bool CanUse(SkillDef def, IReadOnlySet<string> available, Character? character)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(available);
        // OWNERSHIP FIRST, exactly as BuildComposer checks it: a signature belongs to one champion and
        // is exempt from mastery, so asking the tree about it would be asking the wrong system.
        if (def.OwnerCharacterId is { } owner) return owner == character?.Id;
        return available.Contains(def.Id);
    }

    /// <summary>
    /// The slots this loadout holds that the given access and champion would refuse, in slot order.
    /// </summary>
    /// <remarks>
    /// An empty slot is not unusable — it is empty. A slot naming a skill the catalogue no longer knows
    /// is not reported either: it already composes as nothing and there is no name to show the player.
    /// </remarks>
    public static IReadOnlyList<int> UnusableSlots(PlayerLoadout loadout, IReadOnlySet<string> available, Character? character)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        var slots = new List<int>();
        for (var i = 0; i < loadout.Skills.Count; i++)
        {
            if (loadout.Skills[i].SkillId is not { } id) continue;
            if (SkillCatalogue.Find(id) is not { } def) continue;
            if (!CanUse(def, available, character)) slots.Add(i);
        }
        return slots;
    }

    /// <summary>The same slots, as the skills the player would see named in a warning.</summary>
    public static IReadOnlyList<SkillDef> UnusableSkills(PlayerLoadout loadout, IReadOnlySet<string> available, Character? character)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        var defs = new List<SkillDef>();
        foreach (var slot in UnusableSlots(loadout, available, character))
            if (loadout.Skills[slot].SkillId is { } id && SkillCatalogue.Find(id) is { } def) defs.Add(def);
        return defs;
    }

    /// <summary>
    /// Empty every slot holding a SIGNATURE that belongs to somebody else, and return what was emptied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What a champion switch owes the loadout (BRIEF sec.13). One loadout follows the player across
    /// every switch — there is no per-character build to swap in — so the champion you just left behind
    /// leaves their own skill sitting in a slot. <see cref="BuildComposer"/> refuses it, so that slot
    /// simply stops fighting: a slot the player owns, sees filled, and silently cannot use.
    /// </para>
    /// <para>
    /// SHARED skills are deliberately untouched here, even ones the current allocation has locked. A
    /// switch is not a respec and must not quietly undo one: a locked shared skill reads LOCKED on the
    /// build screen and keeps its slot until the player says otherwise (sec.20). And nothing is ever
    /// converted — the new champion's signature is not put in the emptied slot. The player chooses that.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<SkillDef> ShedForeignSignatures(PlayerLoadout loadout, Character? character)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        var cleared = new List<SkillDef>();
        for (var i = 0; i < loadout.Skills.Count; i++)
        {
            if (loadout.Skills[i].SkillId is not { } id || SkillCatalogue.Find(id) is not { } def) continue;
            if (def.OwnerCharacterId is not { } owner || owner == character?.Id) continue;
            if (loadout.ClearSkill(i)) cleared.Add(def);
        }
        return cleared;
    }

    /// <summary>
    /// Put this champion's own SIGNATURE into the loadout if it is not already there, and return the
    /// slot it went into, or -1 when it was already woven, there is no room, or there is no champion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE SIGNATURE IS EQUIPPED FOR THE PLAYER (release polish 2026-09-05).</b> A champion switch
    /// used to leave the new champion's own skill in the library and the slot the old signature had held
    /// EMPTY, on the reasoning that the player chooses what fills it. Playtest: the switch read as
    /// losing a skill, and a new champion stood in its first waves with its birth skill unequipped. The
    /// signature is the champion's identity — it needs no road, no other champion may hold it, and a
    /// build without it is a build the roster card did not describe — so it is placed, not offered.
    /// </para>
    /// <para>
    /// Placed where it costs nothing: the first EMPTY slot, else a new slot if capacity allows, and if
    /// the loadout is full of shared skills the player chose, it is NOT placed — the player's full build
    /// is not overwritten. The caller (the host's switch repair) empties the previous champion's
    /// signature first, so the common case is that the signature takes exactly the slot the old one
    /// left. Ownership is checked the composer's way; a character whose id is not the signature's
    /// owner is never handed it.
    /// </para>
    /// </remarks>
    public static int EnsureSignature(PlayerLoadout loadout, Character? character)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        if (character?.SignatureSkillId is not { } sigId) return -1;
        if (SkillCatalogue.Find(sigId) is not { } def || def.OwnerCharacterId != character.Id) return -1;
        // ALREADY WOVEN — but is it at the TOP? The signature is locked to slot one (playtest
        // 2026-09-09: "that skill must remain unchangeable"), and slot order decides which skill wins
        // a tied beat, so a save written before the rule — or one whose repair placed the signature in
        // the first empty slot further down — is lifted here. PinSignature is the only mover that may
        // touch it.
        if (loadout.HasSkill(sigId))
        {
            loadout.PinSignature();
            return -1;
        }

        // SLOT ONE FIRST, and only then the fallbacks. Writing it straight into slot 0 would overwrite
        // a shared skill the player chose, so the old placement rule still decides WHERE it lands; the
        // pin then lifts it, carrying whatever was on top down one rather than deleting it.
        for (var i = 0; i < loadout.Skills.Count; i++)
            if (loadout.Skills[i].SkillId is null && loadout.SetSkill(i, sigId))
            {
                loadout.PinSignature();
                return loadout.SignatureSlot >= 0 ? loadout.SignatureSlot : i;
            }

        var added = loadout.AddSkill();
        if (added >= 0 && loadout.SetSkill(added, sigId))
        {
            loadout.PinSignature();
            return loadout.SignatureSlot >= 0 ? loadout.SignatureSlot : added;
        }
        if (added >= 0) loadout.RemoveSkill(added);   // never leave a slot we opened and could not fill
        return -1;
    }

    /// <summary>
    /// Empty every slot the given access and champion would refuse, and return what was emptied.
    /// </summary>
    /// <remarks>
    /// The slots stay: capacity is earned and a repair does not take it back. Only the skill leaves, and
    /// only from the slots that could not fight — a slot the rules still allow is never touched, however
    /// the loadout is ordered.
    /// </remarks>
    public static IReadOnlyList<SkillDef> Repair(PlayerLoadout loadout, IReadOnlySet<string> available, Character? character)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        var cleared = new List<SkillDef>();
        foreach (var slot in UnusableSlots(loadout, available, character))
        {
            if (loadout.Skills[slot].SkillId is not { } id || SkillCatalogue.Find(id) is not { } def) continue;
            if (loadout.ClearSkill(slot)) cleared.Add(def);
        }
        return cleared;
    }
}
