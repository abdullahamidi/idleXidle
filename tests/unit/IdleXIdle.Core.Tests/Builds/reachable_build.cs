using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A benchmark build the shipped loadout model can actually produce — and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE LAW: a progression benchmark build must be representable by the current player loadout.</b>
/// The trait pacing bench used to weave its quartets by handing <c>Build.Equip</c> a Source per slot —
/// a lever the weave screen retired when variations took ownership of a skill's Source — so the curve
/// was tuned against builds no player can make. This composes a build the way play does: a
/// <see cref="PlayerLoadout"/> with the skills SET, a <see cref="SkillProgress"/> with the variations
/// CHOSEN (each earned at level 1, four waves, as it is earned in play), the vows SWORN against the
/// account's known list, and <see cref="PlayerLoadout.ToBuild"/> to compose it. A Source arrives from
/// the chosen variation or not at all; there is no Source parameter here to hand one in.
/// </para>
/// <para>
/// <b>It rejects rather than approximates.</b> A skill the catalogue does not know, a skill woven twice,
/// a signature that belongs to another champion, a road the mastery does not reach, a variation the
/// skill does not offer, a vow the account has not found or has no capacity for, and a slot past the
/// capacity all throw with the reason — and so does a composed build that does not contain exactly what
/// was asked, in the order it was asked, so a pick the composer silently dropped can never fight as if
/// it had been woven. It is deliberately NOT a second weave screen: it knows nothing about slot kinds
/// or unlock order beyond what the composer enforces, and it leaves gear and crit to the caller.
/// </para>
/// </remarks>
internal static class Reachable
{
    /// <summary>One woven slot: the skill, the variation it is taken as (null = still unchosen), the vow sworn on it.</summary>
    internal sealed record Slot(string SkillId, string? Variation = null, string? VowId = null);

    /// <summary>
    /// Compose the slots into the build a player with these skills, choices and vows would fight with.
    /// </summary>
    /// <param name="character">Who is fighting — their signature is weavable, everyone else's is not.</param>
    /// <param name="slots">The woven slots, in slot order.</param>
    /// <param name="slotCapacity">How many slots the account has opened. Defaults to the number woven.</param>
    /// <param name="knownVows">The vows the account has found. A vow named on a slot must be here.</param>
    /// <param name="vowCapacity">How many distinct vows the account may swear at once.</param>
    /// <param name="mastery">The mastery tree that grants skill access. Defaults to every road learned.</param>
    internal static Build Compose(Character character, IReadOnlyList<Slot> slots, int? slotCapacity = null,
                                  IReadOnlyList<Vow>? knownVows = null, int vowCapacity = 1,
                                  MasteryTree? mastery = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(slots);
        mastery ??= Taught.Everything();
        var vows = knownVows ?? Array.Empty<Vow>();
        var reach = mastery.AvailableSkills();

        var loadout = new PlayerLoadout { SkillCapacity = slotCapacity ?? slots.Count, VowCapacity = vowCapacity };
        var progress = new SkillProgress();

        foreach (var s in slots)
        {
            var def = SkillCatalogue.Find(s.SkillId)
                      ?? throw Reject($"'{s.SkillId}' is not a skill the catalogue knows.");
            if (!LoadoutRepair.CanUse(def, reach, character))
                throw Reject($"{def.Name} cannot be woven by {character.Name}: "
                             + (def.OwnerCharacterId is { } owner ? $"it is {owner}'s signature." : "no learned road reaches it."));

            var slot = loadout.AddSkill();
            if (slot < 0) throw Reject($"no slot left for {def.Name}: the account has opened {loadout.SkillCapacity}.");
            if (!loadout.SetSkill(slot, def.Id))
                throw Reject($"{def.Name} could not be woven into slot {slot} — it is already woven elsewhere.");
            if (s.VowId is not null && !loadout.SetVow(slot, s.VowId, vows))
                throw Reject($"{s.VowId} could not be sworn on {def.Name}: not a found vow, or past the vow capacity of {vowCapacity}.");

            if (s.Variation is { } wanted)
            {
                // Earned the way play earns it: a level is four waves with the skill woven, and the
                // variation is the first thing a level buys.
                for (var i = progress.UsesOf(def.Id); i < SkillProgress.UsesForLevel(1); i++) progress.RecordWave(def.Id);
                if (!progress.ChooseVariation(def, wanted))
                    throw Reject($"{def.Name} offers no variation '{wanted}' (it has {string.Join(", ", def.Variations.Select(v => v.Name))}).");
            }
        }

        var build = loadout.ToBuild(mastery, character, progress, discoveredKeystones: null, knownVows: vows);

        // WHAT WAS ASKED IS WHAT FIGHTS. The composer refuses quietly — a pick it drops composes as
        // nothing — so the build is checked slot by slot against the request rather than trusted.
        if (build.Skills.Count != slots.Count)
            throw Reject($"{slots.Count} slots were woven and {build.Skills.Count} reached the fight — a pick was refused "
                         + "(the active/passive split, or access the composer denied).");
        for (var i = 0; i < slots.Count; i++)
        {
            var got = build.Skills[i];
            if (got.Def.Id != slots[i].SkillId)
                throw Reject($"slot {i} asked for {slots[i].SkillId} and composed {got.Def.Id}.");
            if (got.Variation?.Name != slots[i].Variation)
                throw Reject($"{got.Def.Name} composed as '{got.Variation?.Name ?? "unchosen"}', not '{slots[i].Variation ?? "unchosen"}'.");
            if (got.Variation is { } v && got.Source != v.Source)
                throw Reject($"{got.Def.Name}'s Source is {got.Source} but its variation {v.Name} is {v.Source} — a Source was injected.");
        }
        return build;
    }

    private static InvalidOperationException Reject(string why)
        => new("UNREACHABLE BENCHMARK BUILD — " + why);
}
