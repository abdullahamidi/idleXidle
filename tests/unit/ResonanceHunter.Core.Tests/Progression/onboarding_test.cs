using System;
using System.Linq;
using ResonanceHunter.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Progression;

/// <summary>
/// The intro runs once, for a new player only, and every later explanation waits to be asked for.
/// </summary>
/// <remarks>
/// The two playtest complaints this guards: the game "throws the player straight in" (so the intro must
/// be DUE on a fresh save) and the notifications "wore the testers out" (so nothing here may fire for a
/// returning player, and a screen's explanation may only ever be owed once).
/// </remarks>
public class OnboardingTest
{
    private readonly ITestOutputHelper _out;

    public OnboardingTest(ITestOutputHelper output) => _out = output;

    // ── The intro ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_fresh_save_is_due_the_intro()
    {
        Assert.True(Onboarding.IntroDue(new TutorialFacts(), introSeen: false));
    }

    [Fact]
    public void test_a_save_that_has_cleared_a_wave_is_not_due_the_intro()
    {
        // A save written before the intro existed carries IntroSeen = false and real progress. Teaching
        // that player where the champion stands is the interruption the intro replaced.
        Assert.False(Onboarding.IntroDue(new TutorialFacts(WavesCleared: 1), introSeen: false));
        Assert.False(Onboarding.IntroDue(new TutorialFacts(WavesCleared: 400), introSeen: false));
    }

    [Fact]
    public void test_a_seen_intro_is_never_due_again()
    {
        Assert.False(Onboarding.IntroDue(new TutorialFacts(), introSeen: true));
    }

    [Fact]
    public void test_the_intro_has_eight_cards_each_pointing_somewhere_different()
    {
        Assert.Equal(8, Onboarding.Intro.Count);
        Assert.Equal(8, Onboarding.Intro.Select(s => s.Target).Distinct().Count());
        foreach (var step in Onboarding.Intro)
        {
            _out.WriteLine($"{step.Target}: {step.Title} — {step.Body}");
            Assert.False(string.IsNullOrWhiteSpace(step.Title));
            Assert.False(string.IsNullOrWhiteSpace(step.Body));
        }
    }

    [Fact]
    public void test_the_intro_copy_is_plain_and_short()
    {
        // The house rules for player-facing copy: short sentences, no abbreviations, and only the
        // characters the font gate allows (ASCII plus a handful of marks seen rendering).
        const string allowedMarks = "·×—–→←‹›…";
        foreach (var step in Onboarding.Intro)
        {
            foreach (var ch in step.Title + step.Body)
                Assert.True(ch < 128 || allowedMarks.Contains(ch), $"'{ch}' in card {step.Target} is outside the font gate");

            // Three short sentences at most, and none of them long.
            var sentences = step.Body.Split(new[] { ". ", "! " }, StringSplitOptions.RemoveEmptyEntries);
            Assert.InRange(sentences.Length, 1, 4);
            Assert.True(step.Body.Length <= 170, $"card {step.Target} runs long: {step.Body.Length} chars");
        }
    }

    // ── The explained list ────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_new_player_starts_with_nothing_explained()
    {
        var seeded = Onboarding.SeedExplained(new UnlockFacts(), introSeen: false, explained: Array.Empty<string>());
        Assert.Empty(seeded);
    }

    [Fact]
    public void test_a_save_from_before_the_intro_treats_every_open_screen_as_read()
    {
        // Wave 8 with a conquest: Stats, Build, Mastery, Warren, Map, Roster and the Hunt are all open,
        // and the second, third and fourth skill slots are earned. None of it may be announced.
        var facts = new UnlockFacts(WavesCleared: 8, DeepestWave: 12, RegionsConquered: 1);
        var seeded = Onboarding.SeedExplained(facts, introSeen: false, explained: Array.Empty<string>());

        _out.WriteLine("seeded: " + string.Join(", ", seeded.OrderBy(s => s)));

        foreach (var open in Unlocks.Open(facts))
            Assert.Contains(Onboarding.ScreenKey(open), seeded);
        Assert.Contains(Onboarding.SlotKey(2), seeded);
        Assert.Contains(Onboarding.SlotKey(3), seeded);
        Assert.Contains(Onboarding.SlotKey(4), seeded);
        Assert.DoesNotContain(Onboarding.ScreenKey(Activity.Forge), seeded);   // still locked, still owed

        foreach (var tile in Enum.GetValues<Activity>())
            Assert.False(Onboarding.IsNew(tile, facts, seeded), $"{tile} would be marked NEW on a returning player");
    }

    [Fact]
    public void test_a_player_who_saw_the_intro_keeps_exactly_what_the_save_says()
    {
        // The Map and Roster are open from the first frame; a player who finished the intro and never
        // visited them still has both explanations waiting — the list is honoured as saved.
        var facts = new UnlockFacts(WavesCleared: 3, DeepestWave: 3);
        var seeded = Onboarding.SeedExplained(facts, introSeen: true, explained: new[] { "Stats" });

        Assert.Equal(new[] { "Stats" }, seeded.OrderBy(s => s));
        Assert.True(Onboarding.IsNew(Activity.Map, facts, seeded));
        Assert.True(Onboarding.IsNew(Activity.Roster, facts, seeded));
        Assert.False(Onboarding.IsNew(Activity.Stats, facts, seeded));
    }

    [Fact]
    public void test_the_hunt_is_never_new_and_never_owes_a_banner()
    {
        // The intro IS the Hunt's explanation.
        var none = Array.Empty<string>();
        Assert.False(Onboarding.IsNew(Activity.Hunt, new UnlockFacts(), none));
        Assert.Null(Onboarding.BannerFor(Activity.Hunt, new UnlockFacts(), none));
    }

    [Fact]
    public void test_a_locked_screen_is_not_new()
    {
        Assert.False(Onboarding.IsNew(Activity.Forge, new UnlockFacts(), Array.Empty<string>()));
    }

    [Fact]
    public void test_the_build_screen_explains_itself_first_and_then_the_new_slot()
    {
        // Wave 5 opens the Build screen AND the second skill slot on the same frame. Two things are
        // owed; they arrive one at a time, the screen before the change on it.
        var facts = new UnlockFacts(WavesCleared: 5, DeepestWave: 5);
        var explained = new System.Collections.Generic.HashSet<string>();

        var first = Onboarding.BannerFor(Activity.Build, facts, explained);
        Assert.NotNull(first);
        Assert.Equal(Onboarding.ScreenKey(Activity.Build), first!.Value.Key);
        Assert.Equal(Unlocks.Explain(Activity.Build), first.Value.Body);
        explained.Add(first.Value.Key);

        var second = Onboarding.BannerFor(Activity.Build, facts, explained);
        Assert.NotNull(second);
        Assert.Equal(Onboarding.SlotKey(2), second!.Value.Key);
        Assert.Equal(Unlocks.SkillSlotNote(2), second.Value.Body);
        explained.Add(second.Value.Key);

        Assert.Null(Onboarding.BannerFor(Activity.Build, facts, explained));
        Assert.False(Onboarding.IsNew(Activity.Build, facts, explained));
    }

    [Fact]
    public void test_a_later_slot_marks_the_build_tile_new_again()
    {
        // Wave 12 opens the third slot. The Build screen is long explained; the tile still lights,
        // because there is something unread on it.
        var facts = new UnlockFacts(WavesCleared: 12, DeepestWave: 12);
        var explained = new[] { Onboarding.ScreenKey(Activity.Build), Onboarding.SlotKey(2) };

        Assert.True(Onboarding.IsNew(Activity.Build, facts, explained));
        var owed = Onboarding.BannerFor(Activity.Build, facts, explained);
        Assert.Equal(Onboarding.SlotKey(3), owed!.Value.Key);
    }
}
