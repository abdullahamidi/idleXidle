using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// The first-gem lesson: a two-card tour over the Forge, owed the first time a gem is held and given
/// once; and the chest rung of the guide, which now knows a chest is waiting from the first minute.
/// </summary>
public class GemTourTest
{

    /// <summary>A file version from before the explained list could be believed — the seeded era.</summary>
    private const int PreExplainedList = Onboarding.FirstVersionWithExplainedList - 1;
    private const int MaxBodyChars = 160;
    private static readonly HashSet<char> AllowedMarks = new("·×—–→←‹›…");

    [Fact]
    public void test_the_gem_lesson_is_owed_once_a_gem_is_held_and_only_until_given()
    {
        var nothingRead = Array.Empty<string>();

        Assert.Null(Onboarding.GemTourDue(0, nothingRead));                       // nothing to point at
        Assert.Equal(Onboarding.GemTourKey, Onboarding.GemTourDue(1, nothingRead));
        Assert.Equal(Onboarding.GemTourKey, Onboarding.GemTourDue(4, nothingRead));
        Assert.Null(Onboarding.GemTourDue(4, new[] { Onboarding.GemTourKey }));   // given, never again
        Assert.Null(Onboarding.GemTourDue(0, new[] { Onboarding.GemTourKey }));
    }

    [Fact]
    public void test_the_gem_lesson_has_two_cards_ending_on_the_socket_tab_and_says_the_first_is_free()
    {
        var tour = Onboarding.GemTourFor(freeSocketUsed: false);

        Assert.Equal(2, tour.Count);
        Assert.Equal(tour.Count, tour.Select(s => s.Target).Distinct().Count());
        Assert.Equal(TourTarget.SocketTab, tour[^1].Target);
        Assert.Contains("free", tour[^1].Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SOCKET", tour[^1].Body);
    }

    [Fact]
    public void test_the_gem_lessons_target_belongs_to_no_screen_tour()
    {
        // A target is resolved by the screen that owns it; the SOCKET tab is the Forge's, and no
        // screen's own tour may point at it or the two tours would light the same thing twice.
        foreach (var screen in Enum.GetValues<Activity>())
            Assert.DoesNotContain(TourTarget.SocketTab, Onboarding.TourFor(screen).Select(s => s.Target));
    }

    [Fact]
    public void test_the_gem_lessons_cards_are_plain_and_short()
    {
        foreach (var step in Onboarding.GemTourFor(freeSocketUsed: false))
        {
            foreach (var ch in step.Title + step.Body)
                Assert.True(ch < 128 || AllowedMarks.Contains(ch), $"'{ch}' in {step.Target} is outside the font gate");
            Assert.True(step.Body.Length <= MaxBodyChars, $"{step.Target} runs long: {step.Body.Length} chars");
            Assert.InRange(step.Body.Split(new[] { ". ", "! ", ": " }, StringSplitOptions.RemoveEmptyEntries).Length, 1, 4);
        }
    }

    [Fact]
    public void test_a_player_from_before_the_intro_is_not_owed_the_gem_lesson()
    {
        // A pre-intro save with hours of play: every open screen counts as read, and so does the gem.
        var veteran = new UnlockFacts(WavesCleared: 300, DeepestWave: 300, ItemsOwned: 40, ChestsEverHeld: 20);
        var seeded = Onboarding.SeedExplained(veteran, introSeen: false, explained: Array.Empty<string>(),
                                              freeSocketUsed: false, fileVersion: PreExplainedList);

        Assert.Contains(Onboarding.GemTourKey, seeded);
        Assert.Null(Onboarding.GemTourDue(3, seeded));
    }

    [Fact]
    public void test_a_player_who_has_seen_the_intro_keeps_the_gem_lesson_owed()
    {
        // The list is taken as saved: whatever they have not read is still waiting for them.
        var seeded = Onboarding.SeedExplained(new UnlockFacts(WavesCleared: 30), introSeen: true,
                                              explained: new[] { "Stats" },
                                              freeSocketUsed: false, fileVersion: PreExplainedList);
        Assert.DoesNotContain(Onboarding.GemTourKey, seeded);
        Assert.Equal(Onboarding.GemTourKey, Onboarding.GemTourDue(1, seeded));
    }

    [Fact]
    public void test_the_vault_tour_speaks_of_the_welcome_gift()
    {
        // A new game's vault holds exactly one chest — the gift — so the tour's cards must be about it.
        var vault = Onboarding.TourFor(Activity.Vault);
        Assert.Contains(vault, s => s.Body.Contains("gift", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vault, s => s.Body.Contains(" ? "));   // the chip is an icon now, not a question mark
    }

    [Fact]
    public void test_the_chest_lesson_is_eligible_on_a_chest_in_hand_and_not_on_a_drop_roll()
    {
        // WHAT THIS TEST USED TO BE. The ladder's chest rung had two bodies — one for the wait and one
        // for a chest already held — because it spoke DURING a 20% drop roll that could last tens of
        // waves, and going silent for all of them was a reported bug. The lesson has no wait to speak
        // through: it is eligible only when a chest is actually in the vault, which a fresh save has
        // from the welcome gift, so the critical path never depends on a roll.
        var holding = new LessonFacts(WavesCleared: 5, DeepestWave: 5, ChestsHeld: 1, StatsTrained: 1, HuntersOwned: 1);
        Assert.Contains(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(holding));
        Assert.Equal(Activity.Vault, OnboardingLessons.Sends(OnboardingLessonId.FirstChestOpen));

        var empty = holding with { ChestsHeld = 0 };
        Assert.DoesNotContain(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(empty));
    }

    [Fact]
    public void test_a_player_who_already_set_a_gem_is_not_owed_the_lesson_even_after_the_intro()
    {
        // Review 2026-08-26: a save that has already socketed a gem must not be handed the YOUR FIRST
        // GEM cards, which promise a free socket the Forge then refuses. FreeSocketUsed answers that on
        // its own, for EVERY save — note the current file version below: this does not ride on the
        // old-save seed, which no longer fires for a file this build wrote.
        // (The premise this comment used to carry — "every save since the intro build carries
        // introSeen = true" — died with the intro. Nothing sets that flag automatically now.)
        var seeded = Onboarding.SeedExplained(new UnlockFacts(WavesCleared: 30), introSeen: true,
                                              explained: Array.Empty<string>(), freeSocketUsed: true,
                                              fileVersion: SaveGame.CurrentVersion);
        Assert.Contains(Onboarding.GemTourKey, seeded);
        Assert.Null(Onboarding.GemTourDue(gemsHeld: 2, explained: seeded));
    }

    [Fact]
    public void test_the_gem_tours_price_line_reads_the_real_rule()
    {
        Assert.Contains("free", Onboarding.GemTourFor(freeSocketUsed: false)[1].Body);
        Assert.DoesNotContain("free", Onboarding.GemTourFor(freeSocketUsed: true)[1].Body);
        Assert.Contains("Essence", Onboarding.GemTourFor(freeSocketUsed: true)[1].Body);
    }
}
