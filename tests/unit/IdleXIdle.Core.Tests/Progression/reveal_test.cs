using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// The rail reveals one screen at a time, each once, when the need that opens it is real — and never
/// takes one back. PLAY → NEED → REVEAL → EXPLAIN → USE, held from a fresh save through the milestones.
/// </summary>
public class RevealTest
{
    private readonly ITestOutputHelper _out;

    public RevealTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_a_fresh_save_reveals_the_hunt_and_nothing_else()
    {
        var revealed = Reveal.Restore(Array.Empty<string>(), new UnlockFacts());
        Assert.Equal(new[] { Activity.Hunt }, revealed.OrderBy(a => a));
        Assert.Empty(Reveal.Newly(revealed, new UnlockFacts()));
    }

    [Fact]
    public void test_the_milestones_reveal_the_screens_in_the_journeys_order_each_once()
    {
        var revealed = Reveal.Restore(Array.Empty<string>(), new UnlockFacts());
        var facts = new UnlockFacts();

        IReadOnlyList<Activity> Milestone(string what, UnlockFacts next)
        {
            facts = next;
            var fresh = Reveal.Newly(revealed, facts);
            _out.WriteLine($"{what,-40} {(fresh.Count == 0 ? "-" : string.Join(", ", fresh))}");
            Assert.Empty(Reveal.Newly(revealed, facts));   // announced once, never twice
            return fresh;
        }

        Assert.Empty(Milestone("first wave", facts with { WavesCleared = 1, DeepestWave = 1 }));
        Assert.Equal(new[] { Activity.Training }, Milestone("third wave", facts with { WavesCleared = 3, DeepestWave = 3 }));
        // ONE ITEM OPENS BOTH, and that is the honest pair: the GEAR screen is where it is worn and the
        // FORGE is where it is worked. (The Forge used to open on an unopened CHEST, one milestone
        // earlier, onto a bench with nothing on it.)
        Assert.Equal(new[] { Activity.Gear, Activity.Forge }, Milestone("first item", facts with { ItemsOwned = 1 }));
        // THE CHEST OPENS THE VAULT AND NOTHING ELSE. It used to break the FORGE's chain in the same
        // instant, which is two announcements for one event and a bench with nothing on it.
        Assert.Equal(new[] { Activity.Vault }, Milestone("first chest, from the first boss", facts with { WavesCleared = 5, DeepestWave = 5, ChestsEverHeld = 1 }));
        var points = MasteryPoints.FromDepth(12);
        Assert.True(points >= Unlocks.MasteryOpensAtPoints, $"depth 12 pays {points}; the tree needs {Unlocks.MasteryOpensAtPoints}");
        Assert.Equal(new[] { Activity.Mastery }, Milestone("depth 12 — the first meaningful points", facts with { WavesCleared = 12, DeepestWave = 12, MasteryPointsEarned = points }));
        // THE FIRST CONQUEST BRINGS NO HUNTER (THE ANVIL joins on the second, since 2026-08-26), so the
        // Roster is not among its screens — it waits for the champion it exists to show.
        Assert.Equal(new[] { Activity.Build, Activity.Map, Activity.Warren },
                     Milestone("first conquest: a keystone, a region", facts with { WavesCleared = 20, DeepestWave = 20, RegionsConquered = 1, KeystonesDiscovered = 1, CharactersUnlocked = 1 }));
        Assert.Equal(new[] { Activity.Traits }, Milestone("first characteristic", facts with { TraitsDiscovered = 1 }));
        Assert.Equal(new[] { Activity.Roster },
                     Milestone("second conquest: THE ANVIL joins", facts with { WavesCleared = 45, DeepestWave = 20, RegionsConquered = 2, CharactersUnlocked = 2 }));

        Assert.Equal(Enum.GetValues<Activity>().OrderBy(a => a), revealed.OrderBy(a => a));
    }

    [Fact]
    public void test_a_second_skill_a_keystone_or_a_vow_each_open_the_build_alone()
    {
        Assert.False(Unlocks.IsOpen(Activity.Build, new UnlockFacts(DeepestWave: 30, SkillsKnown: 1)));
        Assert.True(Unlocks.IsOpen(Activity.Build, new UnlockFacts(SkillsKnown: 2)));
        Assert.True(Unlocks.IsOpen(Activity.Build, new UnlockFacts(KeystonesDiscovered: 1)));
        Assert.True(Unlocks.IsOpen(Activity.Build, new UnlockFacts(VowsKnown: 1)));
    }

    [Fact]
    public void test_a_revealed_screen_stays_when_its_gate_closes_behind_it()
    {
        // GEAR is the one gate that can close: it reads items owned, and a bag can be salvaged empty.
        var revealed = Reveal.Restore(Array.Empty<string>(), new UnlockFacts());
        Assert.Equal(new[] { Activity.Gear, Activity.Forge }, Reveal.Newly(revealed, new UnlockFacts(ItemsOwned: 1)));

        Assert.Empty(Reveal.Newly(revealed, new UnlockFacts(ItemsOwned: 0)));
        Assert.Contains(Activity.Gear, revealed);

        // And across a reload: the save's names carry it, whatever the gates say now.
        var reloaded = Reveal.Restore(Reveal.Names(revealed), new UnlockFacts(ItemsOwned: 0));
        Assert.Contains(Activity.Gear, reloaded);
        Assert.Empty(Reveal.Newly(reloaded, new UnlockFacts(ItemsOwned: 0)));
    }

    [Fact]
    public void test_an_older_save_keeps_every_screen_its_facts_open_and_hears_no_announcement()
    {
        // A save from before the list existed loads with no names. Everything its facts open is
        // revealed at once, silently — the player has been using these screens for hours.
        var facts = new UnlockFacts(WavesCleared: 60, DeepestWave: 60, ItemsOwned: 4, ChestsEverHeld: 3,
                                    RegionsConquered: 1, TraitsDiscovered: 2, MasteryPointsEarned: 6,
                                    SkillsKnown: 2, KeystonesDiscovered: 1, CharactersUnlocked: 2);
        var revealed = Reveal.Restore(Array.Empty<string>(), facts);

        foreach (var open in Unlocks.Open(facts)) Assert.Contains(open, revealed);
        Assert.Empty(Reveal.Newly(revealed, facts));
    }

    [Fact]
    public void test_the_names_round_trip_and_ignore_what_they_do_not_know()
    {
        var revealed = new HashSet<Activity> { Activity.Hunt, Activity.Training, Activity.Gear };
        var names = Reveal.Names(revealed);
        Assert.Equal(new[] { "Gear", "Hunt", "Training" }, names);

        var back = Reveal.Restore(names.Append("Stats").Append("NotAScreen"), new UnlockFacts());
        Assert.Equal(revealed.OrderBy(a => a), back.OrderBy(a => a));
    }

    [Fact]
    public void test_the_reveal_survives_a_reload_at_every_milestone()
    {
        // Reload after each milestone — restore from names and the same facts — and the set is the
        // same, and nothing is announced again.
        var journey = new[]
        {
            new UnlockFacts(WavesCleared: 3, DeepestWave: 3),
            new UnlockFacts(WavesCleared: 5, DeepestWave: 5, ItemsOwned: 1, ChestsEverHeld: 1),
            new UnlockFacts(WavesCleared: 12, DeepestWave: 12, ItemsOwned: 2, ChestsEverHeld: 1, MasteryPointsEarned: 3),
            new UnlockFacts(WavesCleared: 20, DeepestWave: 20, ItemsOwned: 3, ChestsEverHeld: 2, MasteryPointsEarned: 4,
                            RegionsConquered: 1, KeystonesDiscovered: 1, CharactersUnlocked: 2, TraitsDiscovered: 1),
        };
        var revealed = Reveal.Restore(Array.Empty<string>(), new UnlockFacts());
        foreach (var facts in journey)
        {
            Reveal.Newly(revealed, facts);
            var reloaded = Reveal.Restore(Reveal.Names(revealed), facts);
            Assert.Equal(revealed.OrderBy(a => a), reloaded.OrderBy(a => a));
            Assert.Empty(Reveal.Newly(reloaded, facts));
            revealed = reloaded;
        }
    }
}
