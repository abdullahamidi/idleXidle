using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A SKILL'S DEPTH IS A CHOICE, AND IT TAKES LONG ENOUGH TO BE ONE.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, 2026-09-09, in three parts: <i>"once a path is chosen, all three sub-upgrades unlock
/// automatically anyway — so what's the point of making a choice? There isn't really a decision to be
/// made"</i>, <i>"the skill levels up way too fast"</i>, and <i>"no one would have even looked at it
/// if I hadn't pointed it out."</i>
/// </para>
/// <para>
/// <b>Nothing ever auto-took a reinforcement</b> — every one was bought by hand, and the only two
/// callers of <c>TakeReinforcement</c> are the BUILD screen's button and a capture fixture. The
/// complaint is right anyway: four levels bought four purchases out of a pool of four, so the only
/// freedom was the ORDER, and order stops mattering the moment the last one lands. This file pins the
/// two things that changed — the ladder stops one short of the pool, and the pool takes long enough
/// that stopping short is a decision rather than a formality.
/// </para>
/// </remarks>
public class skill_depth_is_a_decision_test
{
    private readonly ITestOutputHelper _out;

    public skill_depth_is_a_decision_test(ITestOutputHelper output) => _out = output;

    private static SkillDef Blow => SkillCatalogue.ById("hammer_blow");

    private static SkillProgress Levelled(int waves)
    {
        var p = new SkillProgress();
        for (var i = 0; i < waves; i++) p.RecordWave(Blow.Id);
        return p;
    }

    [Fact]
    public void test_a_skill_can_never_buy_every_reinforcement_of_its_variation()
    {
        // THE DECISION, as one assertion. Fully levelled, on a variation with three reinforcements,
        // the third is refused — so "which one do I give up" is a real question with a real answer.
        var p = Levelled(SkillProgress.UsesForLevel(SkillProgress.MaxLevel) + 50);
        var v = Blow.Variations[0];
        Assert.Equal(3, v.Reinforcements.Count);

        Assert.True(p.ChooseVariation(Blow, v.Name));
        Assert.True(p.TakeReinforcement(Blow, v.Reinforcements[0].Name));
        Assert.True(p.TakeReinforcement(Blow, v.Reinforcements[1].Name));
        Assert.False(p.TakeReinforcement(Blow, v.Reinforcements[2].Name),
                     "a fully levelled skill bought all three — there is nothing to choose between");
        Assert.Equal(0, p.FreeOn(Blow.Id));
    }

    [Fact]
    public void test_every_reinforcement_is_still_reachable_and_the_pairs_are_the_choice()
    {
        // The other half: two of three cuts nothing out of the catalogue. Each of the three can be
        // taken, and a skill has six distinct end-states (2 variations x 3 pairs) where it had two.
        var ends = 0;
        foreach (var v in Blow.Variations)
            for (var a = 0; a < v.Reinforcements.Count; a++)
            for (var b = a + 1; b < v.Reinforcements.Count; b++)
            {
                var p = Levelled(SkillProgress.UsesForLevel(SkillProgress.MaxLevel));
                Assert.True(p.ChooseVariation(Blow, v.Name));
                Assert.True(p.TakeReinforcement(Blow, v.Reinforcements[a].Name));
                Assert.True(p.TakeReinforcement(Blow, v.Reinforcements[b].Name));
                ends++;
            }

        _out.WriteLine($"{Blow.Name}: {ends} reachable end-states");
        Assert.Equal(6, ends);
    }

    [Fact]
    public void test_a_respec_gives_the_pair_back_so_the_choice_can_be_remade()
    {
        var p = Levelled(SkillProgress.UsesForLevel(SkillProgress.MaxLevel));
        var v = Blow.Variations[0];
        p.ChooseVariation(Blow, v.Name);
        p.TakeReinforcement(Blow, v.Reinforcements[0].Name);
        p.TakeReinforcement(Blow, v.Reinforcements[1].Name);

        p.Respec(Blow.Id);

        Assert.Equal(SkillProgress.MaxLevel, p.FreeOn(Blow.Id));
        Assert.True(p.ChooseVariation(Blow, Blow.Variations[1].Name), "the other branch must be open again");
        Assert.True(p.TakeReinforcement(Blow, Blow.Variations[1].Reinforcements[2].Name),
                    "and the one given up last time must be takeable now");
    }

    [Fact]
    public void test_a_save_that_already_bought_three_keeps_all_three()
    {
        // Nothing is confiscated from a player who levelled under the old ladder: Restore keeps every
        // valid name and FreeOn clamps at zero.
        var v = Blow.Variations[0];
        var p = new SkillProgress();
        p.Restore(new[]
        {
            (Blow.Id, 9_999, (string?)v.Name,
             (IReadOnlyList<string>)v.Reinforcements.Select(r => r.Name).ToList()),
        });

        Assert.Equal(3, v.Reinforcements.Count(r => p.HasReinforcement(Blow.Id, r.Name)));
        Assert.Equal(0, p.FreeOn(Blow.Id));   // over-spent, and that is fine
    }

    [Fact]
    public void test_the_curve_is_data_and_each_level_costs_more_than_the_last()
    {
        // The old curve was floor(sqrt(uses)/2) — 4, 16, 36, 64 waves — which is 23 seconds, 1m31,
        // 3m25 and 6m05 of watched play at the descent's measured rate. A whole skill mastered inside
        // six minutes, and every equipped skill at the same moment, because they accrue in parallel.
        var table = SkillProgress.WavesForLevel;
        Assert.Equal(SkillProgress.MaxLevel + 1, table.Count);
        Assert.Equal(0, table[0]);

        for (var i = 2; i < table.Count; i++)
            Assert.True(table[i] - table[i - 1] > table[i - 1] - table[i - 2],
                        $"level {i} costs no more than level {i - 1}: {string.Join(", ", table)}");

        // Roughly 550 cleared waves an hour on the shipped descent: the variation inside the first
        // sitting, the reinforcements measured in tens of minutes and hours.
        const double wavesPerHour = 550;
        foreach (var level in Enumerable.Range(1, SkillProgress.MaxLevel))
            _out.WriteLine($"level {level}: {table[level],5} waves  ≈ {table[level] / wavesPerHour * 60:0} minutes");

        Assert.True(table[1] >= 20, "the first choice arrives too fast to have been earned");
        Assert.True(table[1] / wavesPerHour * 60 < 15, "a skill whose identity is unchosen after a sitting is a skill nobody has met");
        Assert.True(table[SkillProgress.MaxLevel] / wavesPerHour > 1.0, "the last level is not an hour of play away");
    }

    [Fact]
    public void test_the_level_and_the_table_cannot_drift_apart()
    {
        for (var level = 0; level <= SkillProgress.MaxLevel; level++)
        {
            Assert.Equal(level, SkillProgress.LevelFor(SkillProgress.UsesForLevel(level)));
            if (level > 0)
                Assert.Equal(level - 1, SkillProgress.LevelFor(SkillProgress.UsesForLevel(level) - 1));
        }
        Assert.Equal(SkillProgress.MaxLevel, SkillProgress.LevelFor(int.MaxValue));
    }

    [Fact]
    public void test_the_build_screen_says_a_level_is_waiting()
    {
        // The third complaint: it was never surfaced. A woven skill holding an unspent level now
        // raises the BUILD screen's hint, and it OUTRANKS the empty-slot line — an empty slot is
        // answered by picking from a list, this is answered by reading three effects and choosing.
        var waiting = Onboarding.HintFor(Activity.Build,
                                         new HintFacts(EmptySkillSlots: 2, SkillWithLevelToSpend: "SPRAY", SkillLevelsToSpend: 1));
        Assert.NotNull(waiting);
        Assert.Contains("SPRAY", waiting!.Value.Text, StringComparison.Ordinal);
        Assert.Contains("LEVEL TO SPEND", waiting.Value.Text, StringComparison.Ordinal);

        // ...and it says nothing at all when nothing is waiting, rather than nagging.
        var quiet = Onboarding.HintFor(Activity.Build, new HintFacts());
        Assert.Null(quiet);

        // The empty-slot line still exists for the case it is actually about.
        var slots = Onboarding.HintFor(Activity.Build, new HintFacts(EmptySkillSlots: 1));
        Assert.NotNull(slots);
        Assert.Contains("EMPTY SKILL SLOT", slots!.Value.Text, StringComparison.Ordinal);
    }
}
