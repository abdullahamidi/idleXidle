using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Warrens;
using Xunit;

namespace IdleXIdle.Core.Tests.Warrens;

/// <summary>
/// Structural capability leaves the trait tree, and every piece of it lands on an owner that already
/// exists, is already monotone, and is already persisted.
/// </summary>
/// <remarks>
/// Automation belongs to the Warren, which owns the account's idle layer and has its own currency and
/// ladder. Capacity belongs to Unlocks, whose law is derived-never-stored. Neither charges a
/// choice-currency for a convenience, which is the whole point: a player who spends a trait point on
/// A BASIC SOCKET has expressed nothing about their build.
/// </remarks>
public class WarrenAutomationTest
{
    private static Warren At(FacilityKind kind, int level)
    {
        var w = new Warren();
        w.Restore(1, 0, new System.Collections.Generic.Dictionary<FacilityKind, int> { [kind] = level });
        return w;
    }

    [Fact]
    public void test_scavenger_runs_sells_common_at_two_and_uncommon_at_four()
    {
        var t = WarrenTuning.Default;

        Assert.Null(WarrenAutomation.AutoSellAtOrBelow(At(FacilityKind.ScavengerRuns, t.AutoSellCommonLevel - 1)));
        Assert.Equal(Rarity.Common, WarrenAutomation.AutoSellAtOrBelow(At(FacilityKind.ScavengerRuns, t.AutoSellCommonLevel)));
        Assert.Equal(Rarity.Uncommon, WarrenAutomation.AutoSellAtOrBelow(At(FacilityKind.ScavengerRuns, t.AutoSellUncommonLevel)));
    }

    [Fact]
    public void test_no_facility_level_can_ever_eat_a_rare()
    {
        // Not a tuning value, a rule: a bag of ninety Commons spends the player's attention on nothing,
        // but a rule that could sell a Rare would auto-sell the item game, which is what they are here
        // for. The cap is asserted against every level a facility could ever reach.
        for (var level = 1; level <= 60; level++)
        {
            var floor = WarrenAutomation.AutoSellAtOrBelow(At(FacilityKind.ScavengerRuns, level));
            Assert.True(floor is null or Rarity.Common or Rarity.Uncommon,
                $"SCAVENGER RUNS at level {level} would sell {floor}");
        }
    }

    [Fact]
    public void test_hoard_vaults_merges_at_two()
    {
        var t = WarrenTuning.Default;

        Assert.False(WarrenAutomation.AutoMergeOnChestOpen(At(FacilityKind.HoardVaults, t.AutoMergeLevel - 1)));
        Assert.True(WarrenAutomation.AutoMergeOnChestOpen(At(FacilityKind.HoardVaults, t.AutoMergeLevel)));
    }

    [Fact]
    public void test_automation_is_monotone_in_the_facility_level()
    {
        // A facility level is never lowered, so neither is what it does for you — the same invariant the
        // unlock layer runs on, and the reason this move needed no new save field.
        Rarity? last = null;
        for (var level = 1; level <= 10; level++)
        {
            var now = WarrenAutomation.AutoSellAtOrBelow(At(FacilityKind.ScavengerRuns, level));
            Assert.True((int?)now >= (int?)last || last is null,
                $"the auto-sell floor fell from {last} to {now} at level {level}");
            last = now;
        }
    }

    [Fact]
    public void test_the_automation_levels_sit_below_the_first_production_milestone()
    {
        // Deliberate placement: level 2 and level 4 change what the game DOES for you, level 5 changes
        // how much it makes. Two legible rewards rather than one lump.
        var t = WarrenTuning.Default;
        Assert.True(t.AutoSellCommonLevel < t.MilestoneEvery);
        Assert.True(t.AutoSellUncommonLevel < t.MilestoneEvery);
        Assert.True(t.AutoMergeLevel < t.MilestoneEvery);
    }

    [Fact]
    public void test_every_automation_threshold_says_what_it_does_in_plain_words()
    {
        // The replacement for the trait cards' own liveness rule: a threshold with no copy is a level
        // that changes the game and never tells the player. And the old cards LIED — they said items
        // sold "the moment they drop" and the forge merged "after every expedition"; both actually fire
        // when a chest is opened, which is what these lines say.
        var t = WarrenTuning.Default;
        var w = new Warren();

        foreach (var (kind, level) in new[]
                 {
                     (FacilityKind.ScavengerRuns, t.AutoSellCommonLevel),
                     (FacilityKind.ScavengerRuns, t.AutoSellUncommonLevel),
                     (FacilityKind.HoardVaults, t.AutoMergeLevel),
                 })
        {
            var note = WarrenAutomation.NoteFor(w, kind, level);
            Assert.False(string.IsNullOrWhiteSpace(note), $"{kind} level {level} changes the game silently");
            Assert.Contains("CHEST", note);   // it fires on a chest open, and the copy must say so
        }

        // A level that only changes output says nothing, rather than inventing a promise.
        Assert.Equal("", WarrenAutomation.NoteFor(w, FacilityKind.Nursery, 2));
        Assert.Equal("", WarrenAutomation.NoteFor(w, FacilityKind.ScavengerRuns, 3));
    }

    [Fact]
    public void test_a_facility_that_already_automates_says_so()
    {
        Assert.Equal("", WarrenAutomation.ActiveNoteFor(new Warren(), FacilityKind.ScavengerRuns));
        Assert.Contains("COMMON", WarrenAutomation.ActiveNoteFor(At(FacilityKind.ScavengerRuns, 2), FacilityKind.ScavengerRuns));
        Assert.Contains("UNCOMMON", WarrenAutomation.ActiveNoteFor(At(FacilityKind.ScavengerRuns, 4), FacilityKind.ScavengerRuns));
        Assert.Contains("STACKS", WarrenAutomation.ActiveNoteFor(At(FacilityKind.HoardVaults, 2), FacilityKind.HoardVaults));
    }

    // ── THE OTHER STRUCTURAL OWNERS ──────────────────────────────────────────────────────────────

    [Fact]
    public void test_skill_slots_come_from_progression_alone_and_stop_at_four()
    {
        // The fifth slot is REMOVED, not moved. Nothing in the game sells one, and the ladder's top
        // rung is four — which is two skills that take an action and two that do not.
        //
        // EVERY RUNG NOW HAS TWO CONDITIONS: the depth it always had, and the mastery points the
        // world charges for a skill to put in the slot. Depth alone opened all four by wave 20 while
        // three of them stayed unfillable for another forty waves (playtest, 2026-09-09).
        var paths = Unlocks.RoadPaths;

        Assert.Equal(1, Unlocks.SkillSlots(new UnlockFacts()));

        // Depth without the points buys nothing — this is the whole regression.
        Assert.Equal(1, Unlocks.SkillSlots(new UnlockFacts(DeepestWave: 5)));
        Assert.Equal(1, Unlocks.SkillSlots(new UnlockFacts(DeepestWave: 12)));
        Assert.Equal(1, Unlocks.SkillSlots(new UnlockFacts(DeepestWave: 12, RegionsConquered: 1)));

        // ...and the points without the depth buy nothing either.
        Assert.Equal(1, Unlocks.SkillSlots(new UnlockFacts(MasteryPointsEarned: paths[2])));

        Assert.Equal(2, Unlocks.SkillSlots(new UnlockFacts(DeepestWave: 5, MasteryPointsEarned: paths[0])));
        Assert.Equal(3, Unlocks.SkillSlots(new UnlockFacts(DeepestWave: 12, MasteryPointsEarned: paths[1])));
        Assert.Equal(4, Unlocks.SkillSlots(new UnlockFacts(DeepestWave: 12, RegionsConquered: 1,
                                                          MasteryPointsEarned: paths[2])));

        // The prices are the catalogue's own, so re-pricing a road moves the gates rather than
        // stranding them: three roads, each dearer than the last, and the first is a real cost.
        Assert.Equal(3, paths.Count);
        Assert.True(paths[0] >= MasteryCatalog.SkillRoadCost);
        Assert.True(paths[0] < paths[1] && paths[1] < paths[2]);

        var everything = new UnlockFacts(DeepestWave: 9_999, RegionsConquered: 99, TraitsDiscovered: 999,
                                         MasteryPointsEarned: 9_999);
        Assert.Equal(Build.SkillSlots, Unlocks.SkillSlots(everything));
    }

    [Fact]
    public void test_the_forge_is_free_and_always_was()
    {
        // The brief's premise that the Forge hides behind a trait purchase does not hold: no node ever
        // gated it. Only the node NAME lied.
        //
        // RE-PINNED 2026-09-10 (was "a chest ever held, or two items owned"). A chest is the VAULT's
        // business until it has been opened: every one of the Forge's four jobs needs an ITEM on the
        // bench, so the chest clause opened the screen onto NOTHING ON THE BENCH — and, worse, it made
        // the tutorial boss break two chains in the same instant.
        Assert.False(Unlocks.IsOpen(Activity.Forge, new UnlockFacts()));
        Assert.False(Unlocks.IsOpen(Activity.Forge, new UnlockFacts(ChestsEverHeld: 1)));
        Assert.True(Unlocks.IsOpen(Activity.Forge, new UnlockFacts(ItemsOwned: 1)));
        Assert.True(Unlocks.IsOpen(Activity.Forge, new UnlockFacts(ItemsOwned: 2, TraitsDiscovered: 0)));

        // AND ITS CAPTION NAMES WHAT IT NOW ASKS FOR — an item, and only an item.
        var caption = Unlocks.Requirement(Activity.Forge);
        Assert.Contains("item", caption, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chest", caption, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_keystone_sockets_come_from_the_world_and_never_fall()
    {
        Assert.Equal(0, Unlocks.KeystoneSockets(new UnlockFacts()));
        Assert.Equal(1, Unlocks.KeystoneSockets(new UnlockFacts(RegionsConquered: 1)));
        Assert.Equal(2, Unlocks.KeystoneSockets(new UnlockFacts(RegionsConquered: Unlocks.SecondSocketConquests)));
        Assert.Equal(3, Unlocks.KeystoneSockets(
            new UnlockFacts(RegionsConquered: Unlocks.SecondSocketConquests, DeepestWave: Unlocks.ThirdSocketWave)));

        // Never above the plate.
        var everything = new UnlockFacts(DeepestWave: 9_999, RegionsConquered: 99);
        Assert.Equal(Build.KeystoneSlots, Unlocks.KeystoneSockets(everything));

        for (var conquests = 0; conquests <= Regions.All.Count; conquests++)
        {
            var last = 0;
            for (var wave = 0; wave <= 200; wave += 10)
            {
                var n = Unlocks.KeystoneSockets(new UnlockFacts(DeepestWave: wave, RegionsConquered: conquests));
                Assert.True(n >= last, $"sockets fell from {last} to {n}");
                last = n;
            }
        }
    }

    [Fact]
    public void test_the_first_socket_opens_no_later_than_the_first_keystone()
    {
        // The BUILD screen must never show a keystone with nowhere to put it, nor an empty socket with
        // nothing that could fill it: both arrive on the very same event, the first conquest.
        Assert.Equal(0, Unlocks.KeystoneSockets(new UnlockFacts()));
        Assert.Empty(Keystones.DiscoveredBy(new World()));

        var w = new World();
        w.RestoreConquered(new[] { VerdantHollow.RegionId });
        Assert.Single(Keystones.DiscoveredBy(w));
        Assert.Equal(1, Unlocks.KeystoneSockets(new UnlockFacts(RegionsConquered: 1)));
    }

    [Fact]
    public void test_every_locked_capacity_states_its_own_price()
    {
        // A lock with no sign on it is the thing players actually resent, and each of these replaced a
        // line that told them to go to a screen which no longer sells any of it.
        Assert.Contains("CONQUER", Unlocks.NextSocketNote(new UnlockFacts()));
        Assert.Contains("REGIONS", Unlocks.NextSocketNote(new UnlockFacts(RegionsConquered: 1)));
        Assert.Contains("WAVE", Unlocks.NextSocketNote(new UnlockFacts(RegionsConquered: 3)));
        Assert.Equal("", Unlocks.NextSocketNote(new UnlockFacts(RegionsConquered: 3, DeepestWave: 80)));

        Assert.Contains("WAVE 5", Unlocks.NextVowNote(new UnlockFacts()));
        Assert.Contains("SECOND", Unlocks.NextVowNote(new UnlockFacts(DeepestWave: 5)));
        Assert.Contains("THIRD", Unlocks.NextVowNote(new UnlockFacts(DeepestWave: 5, RegionsConquered: 2)));
        Assert.Equal("", Unlocks.NextVowNote(new UnlockFacts(DeepestWave: 5, RegionsConquered: 4)));

        // NO ABBREVIATIONS, and nothing pointing at the trait screen any more.
        foreach (var line in new[]
                 {
                     Unlocks.NextSocketNote(new UnlockFacts()),
                     Unlocks.NextSocketNote(new UnlockFacts(RegionsConquered: 1)),
                     Unlocks.NextSocketNote(new UnlockFacts(RegionsConquered: 3)),
                     Unlocks.NextVowNote(new UnlockFacts()),
                     Unlocks.NextVowNote(new UnlockFacts(DeepestWave: 5)),
                 })
            Assert.DoesNotContain("TRAIT", line, StringComparison.OrdinalIgnoreCase);
    }
}
