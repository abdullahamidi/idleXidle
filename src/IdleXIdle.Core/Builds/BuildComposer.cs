using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
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
        /// <param name="Passive">
    /// Which slot the player put this skill in, or <c>null</c> for a save written before the choice
    /// existed. THREE-STATE on purpose: a plain <c>false</c> cannot tell "the player chose ACTIVE"
    /// from "nobody has chosen", and those must differ — a TRAP has always been passive, so an unset
    /// one stays passive, while a player who deliberately puts one in an active slot gets SNARE's
    /// REPAY instead.
    /// </param>
    /// <summary>
    /// One slot's worth of choice. <see cref="SkillId"/> names the skill outright when the player
    /// picked it from the library; without one the Form and the slot kind resolve it, which is how
    /// every save written before 2026-08-30 still says what it meant.
    /// </summary>
    public readonly record struct SkillPick(Source Source, Form Form, string? VowId, string Name,
                                            bool? Passive = null, string? SkillId = null);

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
    /// <item>A skill whose Form never took a beat (AURA, TRAP) is passive and spends no active budget.</item>
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
    public static IReadOnlyList<bool> SlotKinds(IReadOnlyList<SkillPick> skills, int slotCapacity,
                                               IReadOnlySet<string>? taught = null)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var activeBudget = Build.ActiveSlotsFor(slotCapacity);

        // THE SPILL IS NOT GATED, and the `taught` set it is handed is only for the screen to read
        // back. A spill is the BUDGET's decision, not the player's, and §11 promises an old
        // four-active build keeps all four skills — refusing there would silently unweave half of
        // someone's build on load, and Build.Weave would drop the overflow rather than keep it as an
        // active, because the active capacity refuses a third. What IS gated is the player's own
        // choice, in Compose below.
        _ = taught;

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

            var naturallyPassive = FormBehaviour.IsPassive(skills[i].Form)
                                   || FormBehaviour.FiresOnBeingHit(skills[i].Form);
            if (naturallyPassive) { passive[i] = true; continue; }
            if (actives < activeBudget) { actives++; continue; }
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
            PassiveMods = DustEffects.TreeMods(tree).Combine(mastery.Mods())
                                     .Combine(character?.Mods ?? BuildMods.None),
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
        // WHAT THIS CHAMPION KNOWS: the roads it has walked, plus the one skill it was born with.
        // Without the second half a fresh champion could weave nothing at all, because all twelve are
        // learned on the tree now and a new game has no points.
        var taughtSkills = mastery.LearnedSkills().ToHashSet(StringComparer.Ordinal);
        if (character?.StartingSkillId is { } born) taughtSkills.Add(born);
        var passive = SlotKinds(picks, slotCapacity, taughtSkills);

        // WHAT THIS CHAMPION HAS BEEN TAUGHT feeds BOTH halves of the rule: SlotKinds above will not
        // spill into an unlearned passive, and the loop below refuses to weave one that was chosen
        // outright. Respec RE-LOCKS (designer, 2026-08-30), so this set shrinks the moment a point is
        // given back — and the weave screen says which skill went.
        for (var i = 0; i < picks.Count; i++)
        {
            var s = picks[i];
            var vow = Vows.ById(s.VowId);
            // The skill's own timing seeds its cooldown. A beat-counted skill still counts BEATS in
            // the fight; this is the millisecond figure the readouts and the wave-length rule use.
            // THE ID WINS when there is one: the library named this skill, and no Form-and-kind
            // round trip can contradict it. Without one it is an old save, and the Form resolves it.
            var def = s.SkillId is { } id && SkillCatalogue.Find(id) is { } named
                ? named
                : SkillCatalogue.Resolve(s.Form, passive[i]);
            // THE GATE, AND IT IS UNCONDITIONAL NOW. You weave what you know: all twelve skills are
            // learned on the mastery tree since 2026-08-30, so there is no longer a free half for a
            // spill to fall back on. The exemption a spill used to get existed only to protect the six
            // that a Source and a Form composed for free, and those six are gone with that door.
            if (!taughtSkills.Contains(def.Id)) continue;
            // The ability carries the RESOLVED id from here on: whichever door this pick came
            // through — the library's own name, or a legacy (Form, slot) pair — what reaches the
            // sim and the next save is the SkillId.
            var ability = new WovenAbility { Name = s.Name, Source = s.Source, Form = s.Form, Vow = vow, SkillId = def.Id };
            var cooldown = def.Beats > 0 ? def.Beats * SoloBattle.DefaultBeatMs
                                         : FormBehaviour.BaseCooldownMs(s.Form);

            // WHAT THE PLAYER HAS SPENT ON THIS SKILL. Null progress means an unlevelled build — a
            // test, or a champion that has not fought yet — and every skill runs at its base line.
            var variation = progress?.VariationOf(def);
            var taken = variation is null
                ? Array.Empty<Reinforcement>()
                : variation.Reinforcements.Where(r => progress!.HasReinforcement(def.Id, r.Name)).ToArray();

            build.Weave(new EquippedSkill(ability, cooldown, PassiveSlot: passive[i])
            {
                Variation = variation,
                Reinforcements = taken,
            });
        }
        return build;
    }
}
