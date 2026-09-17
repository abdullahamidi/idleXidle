using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// The intro runs once, for a new player only; every other screen gives its tour once, the first time
/// it is opened; and every card of every tour is short, plain, and drawable.
/// </summary>
/// <remarks>
/// Three playtest complaints this guards: the game "throws the player straight in" (so the intro must
/// be DUE on a fresh save), the notifications "wore the testers out" (so nothing here may fire for a
/// returning player, and a screen's tour may only ever be owed once), and the other screens' first
/// explanations were "too crowded and too small" (so every screen gets cards in the Hunt's style, and
/// no card may grow back into a paragraph).
/// </remarks>
public class OnboardingTest
{

    /// <summary>A file version from before the explained list could be believed — the seeded era.</summary>
    private const int PreExplainedList = Onboarding.FirstVersionWithExplainedList - 1;
    private readonly ITestOutputHelper _out;

    public OnboardingTest(ITestOutputHelper output) => _out = output;

    /// <summary>ASCII plus the marks the font gate has seen render (tools/check_font_coverage.py).</summary>
    private const string AllowedMarks = "·×—–→←‹›…";

    /// <summary>The caption card wraps at 560px; past this a body grows a fourth line and turns back into a banner.</summary>
    private const int MaxBodyChars = 160;

    // ── The intro ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_fresh_save_is_due_the_intro()
    {
        // NOTHING CALLS THIS ON A REAL PLAYTHROUGH ANY MORE — the mandatory eight-card opening is gone
        // and the fresh journey begins on the fight. IntroDue survives for the returning-save seeding
        // in SeedExplained and for the capture rig, and this pins the answer it still has to give.
        Assert.True(Onboarding.IntroDue(0, introSeen: false));
    }

    [Fact]
    public void test_a_save_that_has_cleared_a_wave_is_not_due_the_intro()
    {
        // A save written before the intro existed carries IntroSeen = false and real progress. Teaching
        // that player where the hunter stands is the interruption the intro replaced.
        Assert.False(Onboarding.IntroDue(1, introSeen: false));
        Assert.False(Onboarding.IntroDue(400, introSeen: false));
    }

    [Fact]
    public void test_a_seen_intro_is_never_due_again()
    {
        Assert.False(Onboarding.IntroDue(0, introSeen: true));
    }

    [Fact]
    public void test_the_intro_is_the_tour_of_the_hunt_and_has_its_eight_cards()
    {
        // The intro was the first tour, and the other screens' tours were built in its image. It is
        // pinned card by card so generalising the machinery could not quietly reword the one the
        // playtest called "much clearer".
        var expected = new (TourTarget Target, string Title, string Body)[]
        {
            (TourTarget.Champion, "YOUR HUNTER",
                "This is your hunter. It fights on its own. You never press attack."),
            (TourTarget.Enemies, "THE ENEMIES",
                "Enemies come in waves, and no two waves are made of the same thing. Every fifth is a boss. The banner at the top counts the waves and the conquest."),
            (TourTarget.HunterHud, "YOUR HUNTER'S LIFE",
                "This is your hunter's life. When it reaches zero the run ends — then it gets back up and starts again. Nothing is lost."),
            (TourTarget.CurrencyPills, "GLEAM",
                "Every cleared wave pays Gleam. Spend Gleam on the TRAINING screen to make your hunter stronger."),
            (TourTarget.Skills, "YOUR SKILLS",
                "Your skills. They fire on their own timers. You choose them on the BUILD screen later."),
            (TourTarget.RightColumn, "WHAT IS WAITING",
                "What is waiting for you. A chest is in the VAULT already, and when the game has something else for you a button appears here to take you to it."),
            (TourTarget.NavRail, "THE OTHER SCREENS",
                "Every screen is on this rail from the start. The ones still in chains are not open yet — a notice says when one opens, and a gold NEW mark waits on it."),
            (TourTarget.LessonSlot, "LESSONS",
                "A gold NEW mark on a tile means that screen has something new, and the screen says what at the top. The fight's own lessons appear here. Close one with the ×."),
        };

        Assert.Equal(expected.Select(e => new TourStep(e.Target, e.Title, e.Body)), Onboarding.TourFor(Activity.Hunt));
    }

    // ── Every tour ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_screen_has_a_tour_of_two_to_four_cards_each_pointing_somewhere_different()
    {
        // The Hunt's eight is the exception the intro earned; every other screen teaches what it is
        // for and what to do first, and stops. Two cards is a screen with one idea; five is a manual.
        foreach (var screen in Enum.GetValues<Activity>())
        {
            var tour = Onboarding.TourFor(screen);
            _out.WriteLine($"{screen}: {tour.Count} cards");
            foreach (var step in tour) _out.WriteLine($"   {step.Target,-16} {step.Title} — {step.Body}");

            var (lo, hi) = screen == Activity.Hunt ? (8, 8) : (2, 4);
            Assert.InRange(tour.Count, lo, hi);
            Assert.Equal(tour.Count, tour.Select(s => s.Target).Distinct().Count());
            foreach (var step in tour)
            {
                Assert.False(string.IsNullOrWhiteSpace(step.Title), $"{screen} has a card with no title");
                Assert.False(string.IsNullOrWhiteSpace(step.Body), $"{screen} has a card with no body");
            }
        }
    }

    [Fact]
    public void test_no_two_screens_share_a_target()
    {
        // A target is resolved to a rectangle by the screen it belongs to. One shared between two
        // screens would light the right region on one of them and nothing on the other.
        var owners = new Dictionary<TourTarget, Activity>();
        foreach (var screen in Enum.GetValues<Activity>())
            foreach (var step in Onboarding.TourFor(screen))
            {
                Assert.False(owners.TryGetValue(step.Target, out var other),
                    $"{step.Target} is pointed at by both {other} and {screen}");
                owners[step.Target] = screen;
            }
    }

    [Fact]
    public void test_every_card_is_plain_and_short()
    {
        // The house rules for player-facing copy: short sentences, no abbreviations, only the
        // characters the font gate allows — and never a fourth line, which is where "too crowded" began.
        foreach (var screen in Enum.GetValues<Activity>())
            foreach (var step in Onboarding.TourFor(screen))
            {
                foreach (var ch in step.Title + step.Body)
                    Assert.True(ch < 128 || AllowedMarks.Contains(ch),
                        $"'{ch}' in {screen}/{step.Target} is outside the font gate");

                var sentences = step.Body.Split(new[] { ". ", "! ", ": " }, StringSplitOptions.RemoveEmptyEntries);
                Assert.InRange(sentences.Length, 1, 4);
                Assert.True(step.Body.Length <= MaxBodyChars,
                    $"{screen}/{step.Target} runs long: {step.Body.Length} chars — \"{step.Body}\"");
                Assert.DoesNotContain("respec", step.Body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DPS", step.Body);
                Assert.DoesNotContain("PTS", step.Body);
            }
    }

    [Fact]
    public void test_the_traits_tour_says_what_a_trait_is_and_never_offers_to_sell_one()
    {
        // REWRITTEN 2026-09-03 (P4, BRIEF §22-§27). This test used to demand the tour teach a spine,
        // four roads, a capstone and where TRAIT POINTS come from. Every one of those is now false:
        // there is no tree, no currency and nothing to buy on this screen (LAW 5). What replaces them
        // is the three things a new player must leave with — what a trait IS, that it awakens from
        // what you have done rather than being bought, and that each hunter wears exactly three.
        // The old assertions are kept below as prohibitions, so the dead vocabulary cannot come back.
        var text = string.Join(" ", Onboarding.TourFor(Activity.Traits).Select(s => s.Body));

        Assert.Contains("characteristic", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("awakens", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("three", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("???", text);
        // Free and reversible is the whole of §27, and the card has to say it.
        Assert.Contains("costs nothing", text, StringComparison.OrdinalIgnoreCase);
        // Account-wide discovery, per-character loadout (§25, §26) — the player is told the second
        // hunter does not start the collection again.
        Assert.Contains("Every hunter", text, StringComparison.OrdinalIgnoreCase);

        // THE DEAD VOCABULARY. A trait is not bought, so no card may name a price, a point, a road or
        // a node — and Memory Dust never bought one even under the tree.
        Assert.DoesNotContain("TRAIT POINT", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capstone", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("four roads", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dust", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LEARN", text, StringComparison.Ordinal);
        // The old text named the wrong faucet — "going deeper than you ever have" is the mastery
        // tree's income, not this one's.
        Assert.DoesNotContain("deeper than you ever have", text);
    }

    [Fact]
    public void test_the_build_tour_reads_the_mastery_gate_from_the_rules()
    {
        // The card that points at the MASTERY tile says when it opens. That number lives in the unlock
        // gate; the card reads it from there, so retuning the gate cannot leave the card lying.
        var card = Onboarding.TourFor(Activity.Build).Single(s => s.Target == TourTarget.MasteryTile);
        Assert.Contains(Unlocks.Requirement(Activity.Mastery).ToLowerInvariant(), card.Body);
    }

    // ── The explained list ────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_new_player_starts_with_nothing_explained()
    {
        var seeded = Onboarding.SeedExplained(new UnlockFacts(), introSeen: false, explained: Array.Empty<string>(),
                                              freeSocketUsed: false, fileVersion: PreExplainedList);
        Assert.Empty(seeded);
    }

    [Fact]
    public void test_a_save_from_before_the_intro_treats_every_open_screen_as_read()
    {
        // Wave 8 with a conquest: Stats, Build, Mastery, Warren, Map and the Hunt are all open (the Roster
        // waits for a second hunter, which the first conquest does not bring),
        // and the second, third and fourth skill slots are earned. None of it may be announced.
        // A slot needs the mastery points that pay for a skill to fill it as well as the depth, so the
        // facts carry both — a fixture posing "every slot is earned" with depth alone stopped being
        // that fixture on 2026-09-09.
        var facts = new UnlockFacts(WavesCleared: 8, DeepestWave: 12, RegionsConquered: 1,
                                    MasteryPointsEarned: Unlocks.RoadPaths[2]);
        var seeded = Onboarding.SeedExplained(facts, introSeen: false, explained: Array.Empty<string>(),
                                              freeSocketUsed: false, fileVersion: PreExplainedList);

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
        // TRAINING and GEAR are open (three waves, an item); a player who finished the intro and read
        // TRAINING but never visited GEAR still has that tour waiting — the list is honoured as saved.
        var facts = new UnlockFacts(WavesCleared: 3, DeepestWave: 3, ItemsOwned: 1);
        // An OLD save says "Stats"; the host reads it forward through ModernScreenKey before seeding
        // (the screen became TRAINING 2026-09-01), so the returning player keeps what they learned.
        var seeded = Onboarding.SeedExplained(facts, introSeen: true,
            explained: new[] { Onboarding.ModernScreenKey("Stats") },
            freeSocketUsed: false, fileVersion: PreExplainedList);

        Assert.Equal(new[] { "Training" }, seeded.OrderBy(s => s));
        Assert.True(Onboarding.IsNew(Activity.Gear, facts, seeded));
        Assert.False(Onboarding.IsNew(Activity.Map, facts, seeded));       // not open yet — nothing to be new
        Assert.False(Onboarding.IsNew(Activity.Training, facts, seeded));
        Assert.NotNull(Onboarding.TourDue(Activity.Gear, seeded));
        Assert.Null(Onboarding.TourDue(Activity.Training, seeded));
    }

    [Fact]
    public void test_the_hunt_is_never_new_and_never_owes_a_tour_or_a_banner()
    {
        // The intro IS the Hunt's tour, and it is decided by IntroDue, not by the list.
        var none = Array.Empty<string>();
        Assert.False(Onboarding.IsNew(Activity.Hunt, new UnlockFacts(), none));
        Assert.Null(Onboarding.TourDue(Activity.Hunt, none));
        Assert.Null(Onboarding.BannerFor(Activity.Hunt, new UnlockFacts(), none));
    }

    [Fact]
    public void test_a_locked_screen_is_not_new()
    {
        Assert.False(Onboarding.IsNew(Activity.Forge, new UnlockFacts(), Array.Empty<string>()));
    }

    [Fact]
    public void test_every_screen_but_the_hunt_owes_its_tour_exactly_once()
    {
        foreach (var screen in Enum.GetValues<Activity>().Where(a => a != Activity.Hunt))
        {
            var explained = new HashSet<string>();
            var key = Onboarding.TourDue(screen, explained);
            Assert.Equal(Onboarding.ScreenKey(screen), key);
            explained.Add(key!);
            Assert.Null(Onboarding.TourDue(screen, explained));
        }
    }

    [Fact]
    public void test_a_new_player_who_cleared_a_wave_and_reloaded_is_not_seeded_as_a_veteran()
    {
        // THE DEFECT THIS CLOSES, and it is the same shape as the fall-loop one. "The intro was never
        // seen and waves have been cleared" meant "written before the intro existed" only while an
        // intro existed to be seen. Nothing shows one now, so that description also fits a BRAND-NEW
        // player who cleared a wave, quit and came back — and they were being marked as having read
        // every screen, every slot reveal and the first-gem lesson, once, on their second session.
        var newPlayer = new UnlockFacts(WavesCleared: 3, DeepestWave: 3, ItemsOwned: 1);

        var honest = Onboarding.SeedExplained(newPlayer, introSeen: false, explained: Array.Empty<string>(),
                                              freeSocketUsed: false, fileVersion: SaveGame.CurrentVersion);
        Assert.Empty(honest);
        Assert.NotNull(Onboarding.TourDue(Activity.Gear, honest));
        Assert.Equal(Onboarding.GemTourKey, Onboarding.GemTourDue(gemsHeld: 1, explained: honest));

        // ...and the veteran whose file really does predate it is still seeded, unchanged.
        var veteran = Onboarding.SeedExplained(newPlayer, introSeen: false, explained: Array.Empty<string>(),
                                               freeSocketUsed: false, fileVersion: PreExplainedList);
        Assert.NotEmpty(veteran);
        Assert.Null(Onboarding.TourDue(Activity.Gear, veteran));
        Assert.Null(Onboarding.GemTourDue(gemsHeld: 1, explained: veteran));
    }

    [Fact]
    public void test_the_explained_list_migration_version_is_frozen_at_five()
    {
        // IT MUST NEVER BE SaveGame.CurrentVersion. Written that way, the next bump would start seeding
        // files that are perfectly honest, and every player mid-onboarding would be marked as having
        // read everything. Five is the first version written by a build in which no tour starts itself.
        Assert.Equal(5, Onboarding.FirstVersionWithExplainedList);
        Assert.True(Onboarding.FirstVersionWithExplainedList <= SaveGame.CurrentVersion,
                    "the explained list cannot first be trustworthy in a version that does not exist yet");
    }

    [Fact]
    public void test_the_new_slot_is_revealed_whether_or_not_the_build_tour_was_taken()
    {
        // WHAT THIS TEST USED TO SAY, and why it was wrong. Wave 5 opens the BUILD screen and the
        // second skill slot on the same frame, and the two used to be handed over in order — the
        // screen before the change on it — with the slot note withheld until the tour had been given.
        // That was sound while the tour ran itself on first open. It does not run itself any more:
        // LEARN THIS SCREEN is a button, and a player who never presses it was never told they had a
        // second skill slot. A slot opening is progression feedback the screen owes, not a reward for
        // reading optional help.
        var facts = new UnlockFacts(WavesCleared: 5, DeepestWave: 5,
                                    MasteryPointsEarned: Unlocks.RoadPaths[0]);
        var untoured = new HashSet<string>();

        // The tour is still owed — and the reveal comes anyway.
        Assert.Equal(Onboarding.ScreenKey(Activity.Build), Onboarding.TourDue(Activity.Build, untoured));
        var note = Onboarding.BannerFor(Activity.Build, facts, untoured);
        Assert.NotNull(note);
        Assert.Equal(Onboarding.SlotKey(2), note!.Value.Key);
        Assert.Equal(Unlocks.SkillSlotNote(2), note.Value.Body);

        // ...and it is the SAME reveal for a player who did press ?. Reading the help changes nothing.
        var toured = new HashSet<string> { Onboarding.ScreenKey(Activity.Build) };
        Assert.Equal(note, Onboarding.BannerFor(Activity.Build, facts, toured));

        // Acknowledged once, gone for good — from both.
        untoured.Add(note.Value.Key);
        toured.Add(note.Value.Key);
        Assert.Null(Onboarding.BannerFor(Activity.Build, facts, untoured));
        Assert.Null(Onboarding.BannerFor(Activity.Build, facts, toured));
        Assert.False(Onboarding.IsNew(Activity.Build, facts, toured));
    }

    [Fact]
    public void test_the_slots_are_revealed_one_at_a_time_and_only_up_to_the_capacity_unlocks_gives()
    {
        // Three slots can be owed at once on a save that has never opened BUILD, and three
        // congratulations in one frame is a failure this codebase has killed before. Lowest first,
        // one at a time, each acknowledged by its own key — and never one the player has not earned.
        var all = new UnlockFacts(WavesCleared: 9999, DeepestWave: 9999, RegionsConquered: 6,
                                  KeystonesDiscovered: 3, MasteryPointsEarned: 9999);
        var cap = Unlocks.SkillSlots(all);
        Assert.True(cap >= 3, "the fixture must earn more than one slot for this to prove anything");

        var explained = new HashSet<string>();
        for (var slot = 2; slot <= cap; slot++)
        {
            var owed = Onboarding.BannerFor(Activity.Build, all, explained);
            Assert.NotNull(owed);
            Assert.Equal(Onboarding.SlotKey(slot), owed!.Value.Key);
            Assert.Equal(Unlocks.SkillSlotNote(slot), owed.Value.Body);
            explained.Add(owed.Value.Key);
        }
        Assert.Null(Onboarding.BannerFor(Activity.Build, all, explained));

        // CAPACITY IS UNLOCKS', AND ONLY UNLOCKS'. The reveal never invents a slot: a player one
        // milestone short is offered exactly the slots Unlocks.SkillSlots says they have.
        var early = new UnlockFacts(WavesCleared: 5, DeepestWave: 5, MasteryPointsEarned: Unlocks.RoadPaths[0]);
        var seen = new HashSet<string>();
        for (var slot = 2; slot <= Unlocks.SkillSlots(early); slot++)
        {
            var owed = Onboarding.BannerFor(Activity.Build, early, seen);
            Assert.Equal(Onboarding.SlotKey(slot), owed!.Value.Key);
            seen.Add(owed.Value.Key);
        }
        Assert.Null(Onboarding.BannerFor(Activity.Build, early, seen));
    }

    [Fact]
    public void test_acknowledging_a_slot_reveal_fabricates_no_gameplay_fact()
    {
        // Closing the card writes one string into the explained list and nothing else. The slot is
        // still empty, the capacity is unchanged, and FirstSharedSkillEquip — which teaches the actual
        // deed — is untouched by it, because it reads the loadout and not this list.
        var facts = new UnlockFacts(WavesCleared: 5, DeepestWave: 5,
                                    MasteryPointsEarned: Unlocks.RoadPaths[0]);
        var before = Unlocks.SkillSlots(facts);

        var explained = new HashSet<string>();
        var note = Onboarding.BannerFor(Activity.Build, facts, explained)!.Value;
        explained.Add(note.Key);

        Assert.Equal(before, Unlocks.SkillSlots(facts));
        Assert.Equal(new[] { Onboarding.SlotKey(2) }, explained.ToArray());

        // The lesson that asks for the deed does not read the acknowledgement — it reads live state.
        var owed = new LessonFacts(WavesCleared: 5, DeepestWave: 5, HuntersOwned: 1,
                                   SharedSkillsAvailable: 1, EmptySkillSlots: 1);
        Assert.Contains(OnboardingLessonId.FirstSharedSkillEquip, OnboardingLessons.EligibleNow(owed));
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstSharedSkillEquip, owed));
    }

    [Fact]
    public void test_a_later_slot_marks_the_build_tile_new_again()
    {
        // Wave 12 opens the third slot. The Build screen is open (a keystone made the first choice)
        // and long explained; the tile still lights, because there is something unread on it.
        var facts = new UnlockFacts(WavesCleared: 12, DeepestWave: 12, KeystonesDiscovered: 1,
                                    MasteryPointsEarned: Unlocks.RoadPaths[1]);
        var explained = new[] { Onboarding.ScreenKey(Activity.Build), Onboarding.SlotKey(2) };

        Assert.True(Onboarding.IsNew(Activity.Build, facts, explained));
        var owed = Onboarding.BannerFor(Activity.Build, facts, explained);
        Assert.Equal(Onboarding.SlotKey(3), owed!.Value.Key);
    }

    [Fact]
    public void test_only_the_build_screen_ever_owes_a_banner()
    {
        // Every other screen's whole first explanation is its tour. A banner elsewhere would be the
        // "too crowded and too small" paragraph coming back.
        var everything = new UnlockFacts(WavesCleared: 9999, DeepestWave: 9999, ItemsOwned: 999,
                                         ChestsEverHeld: 99, RegionsConquered: 6, TraitsDiscovered: 99,
                                         MasteryPointsEarned: 9999);
        foreach (var screen in Enum.GetValues<Activity>().Where(a => a != Activity.Build))
            Assert.Null(Onboarding.BannerFor(screen, everything, Array.Empty<string>()));
    }
}
