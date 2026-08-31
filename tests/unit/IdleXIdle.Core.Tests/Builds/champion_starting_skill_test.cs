using System;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// Every champion brings one skill, and a brand-new champion can actually weave it.
/// </summary>
/// <remarks>
/// <para>
/// All twelve skills are learned on the mastery tree since 2026-08-30, and a new game has no points —
/// so this is the ONLY thing standing between a fresh champion and an empty build. The designer chose
/// it as a character rule as well as an answer: "Her karakterin basic pasif veya aktif bir skilli
/// olabilir. Böylelikle karakterlerin de ayrımı daha net olabilir."
/// </para>
/// <para>
/// It asserts the whole chain, not the field. A <c>StartingSkillId</c> that composes to nothing is the
/// dormant-feature failure this project produces most reliably: declared on the champion, shown on its
/// card, and never once woven. So each champion's skill is woven the way a player would reach it — its
/// style's Form, in the slot kind its own kind demands — and the composed build has to contain it.
/// </para>
/// </remarks>
public class ChampionStartingSkillTests
{
    private readonly ITestOutputHelper _out;
    public ChampionStartingSkillTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_every_champion_brings_a_skill_it_can_actually_weave()
    {
        foreach (var c in CharacterRoster.All)
        {
            Assert.False(string.IsNullOrEmpty(c.StartingSkillId),
                $"{c.Name} brings no skill, and with every skill on the mastery tree it would stand "
                + "in its first wave with nothing woven.");

            var def = SkillCatalogue.Find(c.StartingSkillId!);
            Assert.True(def is not null, $"{c.Name} names {c.StartingSkillId}, which is not a skill.");

            // The door a player would use: the style's Form, and the slot kind the skill demands.
            // Six of the twelve have no Form of their own and are reached through their style's other
            // Form plus the ACTIVE/PASSIVE switch, which is exactly what this walks.
            var form = def!.LegacyForm
                       ?? (def.TakesABeat
                               ? SkillCatalogue.PassiveOf(def.Style)
                               : SkillCatalogue.ActiveOf(def.Style)).LegacyForm!.Value;

            var build = BuildComposer.Compose(
                new MemoryDustTree(), new MasteryTree(), c,
                skills: new[] { new BuildComposer.SkillPick(Source.Body, form, null, "a", !def.TakesABeat) },
                keystoneIds: Array.Empty<string>(), slotCapacity: 4);

            _out.WriteLine($"{c.Name,-20} brings {def.Name,-6} ({(def.TakesABeat ? "ACTIVE" : "PASSIVE")})"
                           + $"  woven: {build.Skills.Count}");

            Assert.True(build.Skills.Count == 1,
                $"{c.Name} cannot weave {def.Name} on a fresh tree — its starting skill is unreachable.");
            Assert.Equal(def.Id, build.Skills[0].Def.Id);
        }
    }

    [Fact]
    public void test_the_roster_is_not_all_one_kind()
    {
        // The designer's reason for the rule was that it tells champions apart. A roster that all
        // opens with an active would be one champion with ten portraits.
        var kinds = CharacterRoster.All
            .Where(c => c.StartingSkillId is not null)
            .Select(c => SkillCatalogue.ById(c.StartingSkillId!).TakesABeat)
            .ToList();
        Assert.Contains(true, kinds);
        Assert.Contains(false, kinds);
    }
}
