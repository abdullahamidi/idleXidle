using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// Turns the player's choices into the <see cref="Build"/> the sim runs — the Dust tree, the mastery
/// tree, the character, the woven skills and the socketed keystones. THE one seam where all of them
/// meet; <c>PlayerLoadout.ToBuild</c> (Game) only gathers its lists and calls this.
/// </summary>
/// <remarks>
/// Moved here from the Game assembly (review, 2026-08-23) so that what reaches the sim can be PINNED:
/// THE BOUND HAND's vow power, the character's aptitude and the tree's multipliers used to be folded
/// in a method no test could call.
/// </remarks>
public static class BuildComposer
{
    /// <summary>
    /// One slot's worth of choice. <see cref="SkillId"/> names the skill outright — a null id is an
    /// EMPTY slot and composes nothing. <see cref="Source"/> is the woven element, the fallback
    /// until a chosen variation owns it; <see cref="Passive"/> survives only as the save's echo of
    /// the slot the player once chose (a named skill brings its own kind).
    /// </summary>
    public readonly record struct SkillPick(Source Source, string? VowId, bool? Passive = null,
                                            string? SkillId = null);

    /// <summary>
    /// Which slot each woven skill lands in — <c>true</c> for a passive slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ONE RULE, READ BY BOTH THE SIM AND THE SCREEN.</b> The weave screen has to tell the player
    /// which of their skills costs an action and which does not, and the fight has to act on the same
    /// answer. Two implementations of that walk would agree right up until one of them was edited —
    /// which is the shape this project keeps paying for, most recently as a cadence dial that
    /// reconstructed the beat count instead of reading it.
    /// </para>
    /// <para>
    /// The walk, in composition order:
    /// </para>
    /// <list type="number">
    /// <item>A named skill brings its own kind — a Field or a Reaction is passive wherever it lands.</item>
    /// <item>Otherwise it takes an active slot while the active budget has room.</item>
    /// <item>Otherwise it SPILLS into a passive slot — it is not dropped. A build saved before the
    ///   rework can hold four beat-taking skills against an active budget of two, and refusing the
    ///   overflow would take half of someone's build away on load without a word.</item>
    /// </list>
    /// <para>
    /// Composition order decides, so the skills woven FIRST keep their actives. Any other rule would
    /// reorder a player's build for them.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<bool> SlotKinds(IReadOnlyList<SkillPick> skills, int slotCapacity)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var activeBudget = Build.ActiveSlotsFor(slotCapacity);

        var passive = new bool[skills.Count];
        var actives = 0;
        for (var i = 0; i < skills.Count; i++)
        {
            // THE PLAYER'S OWN CHOICE FIRST, and it is final. A slot's kind decides WHICH of its
            // style's two skills this is, so it is a decision rather than a derivation; everything
            // below is only what happens when nobody has chosen.
            // A NAMED SKILL BRINGS ITS OWN KIND. PRESS is a Field whatever slot it lands in, so the
            // walk cannot put it in an active slot and then wonder why the beat budget does not add up.
            if (skills[i].SkillId is { } named && SkillCatalogue.Find(named) is { } def0)
            {
                if (!def0.TakesABeat) { passive[i] = true; continue; }
                actives++;
                continue;
            }
            if (skills[i].Passive is { } chosen)
            {
                if (chosen) { passive[i] = true; continue; }
                if (actives < activeBudget) { actives++; continue; }
                // chosen active, but the budget is spent: it spills — if it has anywhere to spill to.
                passive[i] = true;   // chosen active, but the budget is spent: it still spills
                continue;
            }

            // NO ID AND NO CHOICE IS AN EMPTY SLOT. It spends neither budget — there is nothing in
            // it — and it reads as passive only so the walk's answer stays one bool per slot.
            passive[i] = true;
        }
        return passive;
    }

    public static Build Compose(MemoryDustTree tree, MasteryTree mastery, Character? character,
                                IEnumerable<SkillPick> skills, IEnumerable<string> keystoneIds, int slotCapacity,
                                SkillProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(mastery);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(keystoneIds);

        // The build's passive numbers are the Dust tree, the Mastery tree AND the character, multiplied
        // together. Its affinity and its extra triggers come from the Mastery tree plus whatever the
        // character grants outright. Its SHAPE is the mastery tree's, the character's aptitude, and the
        // Dust tree's (THE BOUND HAND's vow power). This is the ONE place a character reaches the sim.
        var build = new Build
        {
            // The mastery tree contributes SHAPES, never BuildMods — its old Mods() channel
            // returned None unconditionally and died with P6.
            PassiveMods = DustEffects.TreeMods(tree).Combine(character?.Mods ?? BuildMods.None),
            Affinity = mastery.Affinity(),
            ExtraTriggers = new HashSet<BuildTrigger>(
                mastery.Triggers().Concat(character?.Grants ?? Array.Empty<BuildTrigger>())),
            Shape = SkillShape.Combine(SkillShape.Combine(mastery.Shape(), character?.Shape ?? SkillShape.None),
                                       DustEffects.TreeShape(tree)),
            SlotCapacity = slotCapacity,
            // THE SLOT SPLIT (rework stage 2b). A composed build is a real player's, so its budget is
            // divided: two actives and two passives at four slots, unlocked active-passive-active-
            // passive. This is the change the whole rework is for — four beat-taking skills demanded
            // about 0.80 of the beats and the champion's own swing only lands on what is left over,
            // so the plain attack had almost stopped appearing. Two actives take that to about 0.44
            // WITHOUT any cooldown moving, which is why the knob that had no room left in it stops
            // being the problem. See design/gdd/skill-slots-and-skill-trees.md §2 and §10.
            ActiveCapacity = Build.ActiveSlotsFor(slotCapacity),
            PassiveCapacity = Build.PassiveSlotsFor(slotCapacity),
        };

        var learned = DustEffects.LearnedKeystones(tree);
        foreach (var id in keystoneIds)
            if (learned.FirstOrDefault(k => k.Id == id) is { } k)
                build.Take(k);

        var picks = skills as IReadOnlyList<SkillPick> ?? skills.ToList();
        // WHAT THIS CHAMPION MAY WEAVE: the shared skills its CURRENT mastery allocation reaches,
        // plus its own signature — and nothing else, ever, by any route.
        //
        // Two sources, deliberately (BRIEF sec.10). Mastery access comes and goes with the points
        // (sec.16); a signature is exempt from mastery entirely (sec.18) and belongs to one champion
        // (sec.9). The second half is also what keeps a fresh champion able to fight, since every
        // shared skill is behind a node and a new game has no points.
        var taughtSkills = mastery.AvailableSkills().ToHashSet(StringComparer.Ordinal);
        if (character?.SignatureSkillId is { } own) taughtSkills.Add(own);

        for (var i = 0; i < picks.Count; i++)
        {
            var s = picks[i];
            // THE VOW GATE (P7). The trait tree TEACHES vows (vow_study_*, 11 spine points), and
            // until now only the weave MENU read that — a save could arrive wearing any VowId and
            // the sim paid it, so the chain gated what was offered, never what was worn. An
            // untaught vow composes as NO vow: the skill stays, the unpaid promise does not.
            var vow = DustEffects.KnowsVow(tree, s.VowId) ? Vows.ById(s.VowId) : null;
            // THE ID IS THE IDENTITY — an id-less pick is an EMPTY SLOT and composes nothing.
            // (Form-era picks stopped reaching this loop at P3-final: PlayerLoadout.Restore pins
            // every legacy row to its SkillId through LegacySkillForm before anything composes.)
            if (s.SkillId is not { } id || SkillCatalogue.Find(id) is not { } def) continue;
            // THE GATE, AND IT IS UNCONDITIONAL. You weave what you may weave right now: the shared
            // skills the CURRENT mastery allocation reaches, plus this champion's own signature.
            // Access is no longer permanent (BRIEF sec.16), so this set shrinks as well as grows and
            // a respec silently stops carrying what it no longer allows — which is why the host warns
            // before one and repairs the slots after (sec.19).
            //
            // AND OWNERSHIP IS CHECKED SEPARATELY, because taughtSkills is a set of ids and cannot
            // say WHY an id is in it. A signature that belongs to somebody else is refused here
            // however it arrived — an old save, a share code, or a slot left behind by a champion
            // switch (sec.9, sec.13, LAW 1).
            if (def.OwnerCharacterId is { } owner && owner != character?.Id) continue;
            if (!taughtSkills.Contains(def.Id)) continue;

            // WHAT THE PLAYER HAS SPENT ON THIS SKILL, applied ONCE, here. Null progress means an
            // unlevelled build — a test, or a champion that has not fought yet — and the skill runs
            // at its base line. The fight reads the finished def and never re-derives any of this,
            // which is what keeps twenty-four variations from becoming twenty-four branches.
            var variation = progress?.VariationOf(def);
            var resolved = def;
            if (variation?.Modify is { } m) resolved = m(resolved);
            if (variation is not null)
                foreach (var r in variation.Reinforcements)
                    if (progress!.HasReinforcement(def.Id, r.Name) && r.Modify is { } rm)
                        resolved = rm(resolved);

            // The chosen variation owns the element; the woven element is the fallback before it.
            build.Equip(new EquippedSkill(resolved, variation?.Source ?? s.Source, vow));
        }
        return build;
    }
}
