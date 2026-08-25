using System;
using System.Linq;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Progression;

/// <summary>
/// The game opens one thing at a time, and every locked thing says why.
/// </summary>
public class UnlocksTest
{
    private readonly ITestOutputHelper _out;

    public UnlocksTest(ITestOutputHelper output) => _out = output;

    private static readonly UnlockFacts Fresh = new();

    [Fact]
    public void test_a_brand_new_player_can_look_around_but_has_one_place_to_act()
    {
        // THE HEADLINE, revised (playtest nine, item 11: "let the player look around"). The DOING
        // screens still open one at a time — that is what cured "thrown straight in and confused" —
        // but the WINDOW-SHOPPING screens, the Map and the Roster, are open from the first frame:
        // both carry their own internal locks (locked regions, locked champions), so to a new player
        // they read as a promise of what is coming, not as a wall of things to learn.
        var open = Unlocks.Open(Fresh);

        _out.WriteLine("a fresh save opens with: " + string.Join(", ", open));

        Assert.Equal(new[] { Activity.Hunt, Activity.Map, Activity.Roster }, open);
    }

    [Fact]
    public void test_a_brand_new_player_has_one_skill()
    {
        // The other half of the same complaint: "4 skill açık şekilde başladığımı gördüm".
        Assert.Equal(1, Unlocks.SkillSlots(Fresh));
    }

    [Fact]
    public void test_the_whole_opening_sequence_is_reported_in_order()
    {
        // Reported rather than asserted line by line, because the ORDER is a design decision someone
        // should be able to read off a test run without reconstructing it from nine switch arms.
        var journey = new (string moment, UnlockFacts facts)[]
        {
            ("fresh save",            new UnlockFacts()),
            ("first wave cleared",    new UnlockFacts(WavesCleared: 1, DeepestWave: 1)),
            ("first item found",      new UnlockFacts(WavesCleared: 3, DeepestWave: 3, ItemsOwned: 1)),
            ("first chest earned",    new UnlockFacts(WavesCleared: 5, DeepestWave: 5, ItemsOwned: 1, ChestsEverHeld: 1)),
            ("wave 12",               new UnlockFacts(WavesCleared: 20, DeepestWave: 12, ItemsOwned: 3, ChestsEverHeld: 1, TraitPointsEarned: 2)),
            ("first region taken",    new UnlockFacts(WavesCleared: 40, DeepestWave: 20, ItemsOwned: 5, ChestsEverHeld: 2, RegionsConquered: 1, TraitPointsEarned: 4)),
            ("second region taken",   new UnlockFacts(WavesCleared: 90, DeepestWave: 30, ItemsOwned: 9, ChestsEverHeld: 3, RegionsConquered: 2, TraitPointsEarned: 6)),
        };

        UnlockFacts? previous = null;
        foreach (var (moment, facts) in journey)
        {
            var fresh = previous is null
                ? Unlocks.Open(facts)
                : Unlocks.NewlyOpened(previous.Value, facts);

            _out.WriteLine($"{moment,-22} skills {Unlocks.SkillSlots(facts)}   "
                           + (fresh.Count == 0 ? "-" : "OPENS: " + string.Join(", ", fresh)));
            previous = facts;
        }

        // By the end of that journey the whole game is available — a gate that never opens is a feature
        // that was cut without anyone deciding to cut it.
        var final = Unlocks.Open(journey[^1].facts);
        foreach (var activity in Enum.GetValues<Activity>())
            Assert.Contains(activity, final);
    }

    [Fact]
    public void test_nothing_ever_re_locks_as_a_player_progresses()
    {
        // Facts only ever grow, so a gate must be monotone in every one of them. A gate written with an
        // upper bound (or a subtraction) would close behind the player, and it would do it silently.
        var rng = new Random(3);
        var facts = new UnlockFacts();
        var openBefore = Unlocks.Open(facts).ToHashSet();
        var slotsBefore = Unlocks.SkillSlots(facts);

        for (var step = 0; step < 400; step++)
        {
            facts = facts with
            {
                WavesCleared = facts.WavesCleared + rng.Next(0, 3),
                DeepestWave = facts.DeepestWave + rng.Next(0, 2),
                ItemsOwned = facts.ItemsOwned + rng.Next(0, 2),
                ChestsEverHeld = facts.ChestsEverHeld + rng.Next(0, 2),
                RegionsConquered = facts.RegionsConquered + (rng.Next(0, 40) == 0 ? 1 : 0),
                TraitPointsEarned = facts.TraitPointsEarned + rng.Next(0, 2),
            };

            var openNow = Unlocks.Open(facts).ToHashSet();
            foreach (var was in openBefore)
                Assert.True(openNow.Contains(was), $"{was} re-locked after progress.");

            Assert.True(Unlocks.SkillSlots(facts) >= slotsBefore, "a skill slot was taken away.");

            openBefore = openNow;
            slotsBefore = Unlocks.SkillSlots(facts);
        }
    }

    [Fact]
    public void test_every_gate_states_its_price_and_explains_itself()
    {
        // A lock with no sign on it is the thing players resent, and an explanation that was never
        // written reads at runtime as an empty panel rather than as a missing string.
        foreach (var activity in Enum.GetValues<Activity>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Unlocks.Headline(activity)),
                $"{activity} has no headline.");
            Assert.False(string.IsNullOrWhiteSpace(Unlocks.Explain(activity)),
                $"{activity} opens with no explanation.");

            // A price is owed by exactly the doors that can be found shut. A screen open from the
            // first frame (the Hunt, the Map, the Roster) has nothing to require — a price line on an
            // open door would read as a lock that opened early, i.e. as a bug.
            if (Unlocks.IsOpen(activity, Fresh))
                Assert.True(string.IsNullOrWhiteSpace(Unlocks.Requirement(activity)),
                    $"{activity} is open from the start but still states a price.");
            else
                Assert.False(string.IsNullOrWhiteSpace(Unlocks.Requirement(activity)),
                    $"{activity} is locked but does not say what opens it.");
        }
    }

    [Fact]
    public void test_the_traits_explanation_states_purpose_sources_roads_and_permanence()
    {
        // Playtest, 2026-08-25: the first explanation of traits must say what they are FOR. Three
        // things a new player has to leave with — what a trait is, where the points come from, and
        // that nothing resets — and the sources must be the TRUE ones (Game1.TraitPointsEarned:
        // conquests, corruption tiers, region mastery levels). Memory Dust no longer buys traits, so
        // the word may not appear.
        var text = Unlocks.Explain(Activity.Traits);
        var headline = Unlocks.Headline(Activity.Traits);

        Assert.Contains("permanent", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TRAIT POINTS", text);
        Assert.Contains("conquer", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("corruption", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mastery", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four roads", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capstone", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never resets", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dust", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dust", headline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PERMANENT", headline);
        // The old text named the wrong faucet — "going deeper than you ever have" is the mastery
        // tree's income, not this one's.
        Assert.DoesNotContain("deeper than you ever have", text);
    }

    [Fact]
    public void test_the_explanations_are_actually_detailed()
    {
        // The brief was "hepsi detaylı şekilde anlatılmalı" — explained in detail. A one-line tooltip
        // would satisfy the test above and fail the request, so this pins the thing that was asked for.
        foreach (var activity in Enum.GetValues<Activity>())
        {
            var text = Unlocks.Explain(activity);
            var sentences = text.Count(c => c == '.');
            _out.WriteLine($"   {activity,-8} {text.Length,4} chars, {sentences} sentences");

            Assert.True(sentences >= 3,
                $"{activity} is explained in {sentences} sentence(s) — that is a tooltip, not an "
                + "explanation.");
        }
    }

    [Fact]
    public void test_every_new_skill_slot_is_explained_when_it_arrives()
    {
        // A slot that appears with no comment is a fifth thing on screen the player did not ask for.
        for (var slot = 2; slot <= 4; slot++)
            Assert.False(string.IsNullOrWhiteSpace(Unlocks.SkillSlotNote(slot)),
                $"slot {slot} opens with nothing said about it.");

        // And the gate really does stop at four — past that the trait tree is the source, which is an
        // existing reward rather than a starting condition.
        var everything = new UnlockFacts(WavesCleared: 9999, DeepestWave: 9999, ItemsOwned: 999,
                                         ChestsEverHeld: 99, RegionsConquered: 6, TraitPointsEarned: 99);
        Assert.Equal(4, Unlocks.SkillSlots(everything));
    }

    [Fact]
    public void test_the_roster_opens_no_later_than_the_first_champion_it_would_show()
    {
        // A GATE AND ITS REWARD TUNED INDEPENDENTLY. Activity.Roster asked for two conquests while
        // THE THORNWALL is earned by conquering the FIRST region — so the player's second champion
        // arrived while the only screen that could show it was still a dimmed tile. Nothing in either
        // table knew about the other; this test is the thing that knows.
        var earliest = CharacterRoster.All
            .Where(c => c.Unlock.RegionId is not null)
            .Select(c => Regions.All.ToList().FindIndex(r => r.Id == c.Unlock.RegionId) + 1)
            .Where(conquests => conquests > 0)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        _out.WriteLine($"the first conquest-earned champion arrives after {earliest} conquest(s)");

        var atThatPoint = new UnlockFacts(RegionsConquered: earliest);
        Assert.True(Unlocks.IsOpen(Activity.Roster, atThatPoint),
            $"a champion is earned on conquest {earliest}, but the ROSTER is still locked then — the "
            + "player is handed something they cannot look at.");
    }

    [Fact]
    public void test_a_requirement_that_names_a_conquest_count_matches_its_gate()
    {
        // A CAPTION THAT DRIFTED FROM ITS RULE. Activity.Roster's gate moved from two conquests to one
        // and its requirement line was left saying "Conquer two regions" — which reads to a player as a
        // lock that opened early, a bug in the opposite direction. Every gate in this file states its
        // own price, so the price has to be checked against the gate.
        foreach (var activity in Enum.GetValues<Activity>())
        {
            var text = Unlocks.Requirement(activity);
            if (!text.Contains("Conquer", StringComparison.OrdinalIgnoreCase)) continue;

            var claimed = text.Contains(" two ", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

            // Open exactly at the claimed count, and shut one below it.
            Assert.True(Unlocks.IsOpen(activity, new UnlockFacts(RegionsConquered: claimed)),
                $"{activity} says \"{text}\" but is still locked at {claimed} conquest(s).");
            Assert.False(Unlocks.IsOpen(activity, new UnlockFacts(RegionsConquered: claimed - 1)),
                $"{activity} says \"{text}\" but is already open at {claimed - 1} conquest(s).");
        }
    }
}
