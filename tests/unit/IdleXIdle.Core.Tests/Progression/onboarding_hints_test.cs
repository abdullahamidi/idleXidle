using System.Linq;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// UX V2 P0.7: a screen's hint is a pure function of a fact, names the fact, and is absent without it.
/// </summary>
public class onboarding_hints_test
{
    private static readonly HintFacts Everything = new(
        NewRegionName: "Cinderworks", MasteryPointsFree: 4, EmptySkillSlots: 1,
        NewChampionName: "The Anvil", ChestsWaiting: 2, TrainableStat: "Might", TrainableCost: 108,
        AffordableUpgradeName: "Nursery");

    [Fact]
    public void test_every_hint_names_the_fact_it_comes_from()
    {
        Assert.Equal("A NEW REGION IS AVAILABLE — CINDERWORKS", Onboarding.HintFor(Activity.Map, Everything)!.Value.Text);
        // TRAITS LOST ITS HINT 2026-09-03 (P4, LAW 5): nothing is spent on that screen any more, so
        // "YOU HAVE 1 TRAIT POINT" is an offer the screen cannot honour. A hint the model cannot back
        // is a lie (brief §92), which is the same rule GEAR, FORGE and HUNT are silent under below —
        // so TRAITS joins them rather than getting a replacement hint about discovery it cannot see.
        Assert.Null(Onboarding.HintFor(Activity.Traits, Everything));
        Assert.Equal("YOU HAVE 4 MASTERY POINTS", Onboarding.HintFor(Activity.Mastery, Everything)!.Value.Text);
        Assert.Equal("AN EMPTY SKILL SLOT — EQUIP A SKILL", Onboarding.HintFor(Activity.Build, Everything)!.Value.Text);
        Assert.Equal("A NEW HUNTER HAS JOINED — THE ANVIL", Onboarding.HintFor(Activity.Roster, Everything)!.Value.Text);
        Assert.Equal("2 CHESTS ARE WAITING", Onboarding.HintFor(Activity.Vault, Everything)!.Value.Text);
        Assert.Equal("YOU CAN TRAIN MIGHT FOR 108 GLEAM", Onboarding.HintFor(Activity.Training, Everything)!.Value.Text);
        Assert.Equal("AN UPGRADE IS AFFORDABLE — NURSERY", Onboarding.HintFor(Activity.Warren, Everything)!.Value.Text);
    }

    [Fact]
    public void test_gear_forge_and_hunt_never_hint_even_with_every_fact_true()
    {
        // GEAR needs a per-slot comparison Core does not make; FORGE has no fact that says "this item wants
        // work"; HUNT teaches through its lessons. A hint the model cannot back is a lie (brief §92).
        Assert.Null(Onboarding.HintFor(Activity.Gear, Everything));
        Assert.Null(Onboarding.HintFor(Activity.Forge, Everything));
        Assert.Null(Onboarding.HintFor(Activity.Hunt, Everything));
    }

    [Fact]
    public void test_no_fact_means_no_hint_on_any_screen()
    {
        foreach (var screen in System.Enum.GetValues<Activity>())
            Assert.Null(Onboarding.HintFor(screen, new HintFacts()));
    }

    [Fact]
    public void test_a_hint_key_carries_its_fact_so_dismissing_one_chest_does_not_silence_two()
    {
        var one = Onboarding.HintFor(Activity.Vault, new HintFacts(ChestsWaiting: 1))!.Value;
        var two = Onboarding.HintFor(Activity.Vault, new HintFacts(ChestsWaiting: 2))!.Value;
        Assert.Equal("1 CHEST IS WAITING", one.Text);
        Assert.NotEqual(one.Key, two.Key);
    }

    [Fact]
    public void test_a_showing_rung_marks_only_the_tile_it_sends_to()
    {
        // Everything open, everything explained: without a rung no tile is NEW.
        var facts = new UnlockFacts(WavesCleared: 999, DeepestWave: 999, ItemsOwned: 99, ChestsEverHeld: 9,
                                    RegionsConquered: 9, TraitsDiscovered: 99, MasteryPointsEarned: 40,
                                    SkillsKnown: 6, KeystonesDiscovered: 3, VowsKnown: 2, CharactersUnlocked: 4);
        // Every tour given, every slot note read, the gem lesson had: nothing but a rung can mark a tile.
        var explained = System.Enum.GetValues<Activity>().Select(Onboarding.ScreenKey)
            .Concat(System.Linq.Enumerable.Range(1, 8).Select(Onboarding.SlotKey)).Append(Onboarding.GemTourKey).ToList();
        // THE CALLER RESOLVES WHICH SCREEN THE LESSON ASKS FOR, and passes only that: Onboarding needs
        // no opinion about which lesson is showing, which is what let the ladder be deleted from under
        // it without this rule changing at all.
        foreach (var screen in System.Enum.GetValues<Activity>())
            Assert.False(Onboarding.IsNew(screen, facts, explained, null), $"{screen} marked with no lesson showing");

        foreach (var sends in new[] { Activity.Training, Activity.Vault, Activity.Gear, Activity.Build })
        {
            Assert.True(Onboarding.IsNew(sends, facts, explained, sends));
            foreach (var screen in System.Enum.GetValues<Activity>())
                if (screen != sends)
                    Assert.False(Onboarding.IsNew(screen, facts, explained, sends), $"{screen} marked by a lesson about {sends}");
        }

        // A lesson about the fight itself sends nowhere, so it marks nothing — it renders on the HUNT.
        foreach (var screen in System.Enum.GetValues<Activity>())
            Assert.False(Onboarding.IsNew(screen, facts, explained,
                                          OnboardingLessons.Sends(OnboardingLessonId.FirstFight)));
    }
}
