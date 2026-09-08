using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A NEW GAME CAN FIGHT.
/// </summary>
/// <remarks>
/// <para>
/// The starter loadout held <c>hammer_blow</c> — one of the twelve shared skills — from the day the
/// solo build shipped. That was safe while a champion's own skill was banked account-wide and while
/// mastery access, once bought, was permanent. It is not safe now: a shared skill is available only
/// while its mastery road is allocated (BRIEF sec.16), and the MASTERY screen itself does not open
/// until wave 25. A fresh champion therefore stood in its first twenty-five waves with a locked slot
/// and nothing but its basic attack.
/// </para>
/// <para>
/// The composer's own comment already said what the answer is: the signature is "what keeps a fresh
/// champion able to fight". The starter carries it.
/// </para>
/// </remarks>
public class StarterLoadoutTest
{
    [Fact]
    public void test_the_starter_carries_the_champions_own_signature()
    {
        var seeker = CharacterRoster.Get(CharacterRoster.StarterId);
        var l = PlayerLoadout.Starter(seeker);

        var only = Assert.Single(l.Skills);
        Assert.Equal(seeker.SignatureSkillId, only.SkillId);
    }

    [Fact]
    public void test_a_new_game_composes_a_skill_it_can_actually_use()
    {
        // The whole point: an empty mastery tree, an empty dust tree, a champion with no history —
        // exactly a first session — must still put a skill into the fight.
        var seeker = CharacterRoster.Get(CharacterRoster.StarterId);
        var build = PlayerLoadout.Starter(seeker).ToBuild(new MasteryTree(), seeker);

        Assert.Single(build.Skills);
    }

    [Fact]
    public void test_every_champion_can_fight_from_its_own_starter()
    {
        // Not only the starter champion: a save that begins on any of the ten (a share code, a test
        // fixture, a future starting choice) must not compose to an empty build.
        foreach (var c in CharacterRoster.All)
        {
            var build = PlayerLoadout.Starter(c).ToBuild(new MasteryTree(), c);
            Assert.True(build.Skills.Count == 1, $"{c.Name} starts unable to weave anything.");
        }
    }

    [Fact]
    public void test_the_old_starter_would_now_be_locked()
    {
        // The regression this test exists for, stated as an assertion rather than as a comment: the
        // skill the starter used to hold is NOT available to a fresh hunter any more.
        Assert.DoesNotContain("hammer_blow", new MasteryTree().AvailableSkills());
    }
}
