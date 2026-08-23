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
        };

        var learned = DustEffects.LearnedKeystones(tree);
        foreach (var id in keystoneIds)
            if (learned.FirstOrDefault(k => k.Id == id) is { } k)
                build.Take(k);

        foreach (var s in skills)
        {
            var vow = Weaving.ById(s.VowId);
            var ability = new WovenAbility { Name = s.Name, Source = s.Source, Form = s.Form, Vow = vow };
            build.Weave(new EquippedSkill(ability, FormBehaviour.BaseCooldownMs(s.Form)));
        }
        return build;
    }
}
