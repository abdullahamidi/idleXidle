using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Prestige;

namespace ResonanceHunter.Core.Builds;

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
    public readonly record struct SkillPick(Source Source, Form Form, string? VowId, string Name);

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
    public static IReadOnlyList<bool> SlotKinds(IReadOnlyList<SkillPick> skills, int slotCapacity)
    {
        ArgumentNullException.ThrowIfNull(skills);
        var activeBudget = Build.ActiveSlotsFor(slotCapacity);
        var passive = new bool[skills.Count];
        var actives = 0;
        for (var i = 0; i < skills.Count; i++)
        {
            var naturallyPassive = FormBehaviour.IsPassive(skills[i].Form)
                                   || FormBehaviour.FiresOnBeingHit(skills[i].Form);
            if (naturallyPassive) { passive[i] = true; continue; }
            if (actives < activeBudget) { actives++; continue; }
            passive[i] = true;
        }
        return passive;
    }

    public static Build Compose(MemoryDustTree tree, MasteryTree mastery, Character? character,
                                IEnumerable<SkillPick> skills, IEnumerable<string> keystoneIds, int slotCapacity)
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
            Shape = SkillShape.Combine(SkillShape.Combine(mastery.Shape(), character?.TotalShape ?? SkillShape.None),
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
        var passive = SlotKinds(picks, slotCapacity);
        for (var i = 0; i < picks.Count; i++)
        {
            var s = picks[i];
            var vow = Weaving.ById(s.VowId);
            var ability = new WovenAbility { Name = s.Name, Source = s.Source, Form = s.Form, Vow = vow };
            build.Weave(new EquippedSkill(ability, FormBehaviour.BaseCooldownMs(s.Form),
                                          PassiveSlot: passive[i]));
        }
        return build;
    }
}
