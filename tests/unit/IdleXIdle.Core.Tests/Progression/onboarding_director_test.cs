using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// THE LESSON CATALOGUE: independently eligible, chosen by priority, completed only by deeds.
/// </summary>
/// <remarks>
/// <para>
/// This file exists because the guide it replaced was a FIFO ladder, and a FIFO ladder has one
/// structural fault that no amount of reordering fixes: the first rung nobody can act on stops every
/// rung behind it. A player with no gem in the bag could not be taught about a conquest, a run report
/// or a mastery point, because a gem lesson sat in front of all three.
/// </para>
/// <para>
/// So the tests here are mostly about INDEPENDENCE and PRIORITY, not about copy. The copy has its own
/// file; what this one guards is that the right single lesson is chosen out of a set that may contain
/// four eligible ones at once — which is exactly the state the first conquest produces.
/// </para>
/// </remarks>
public class onboarding_director_test
{
    private readonly ITestOutputHelper _out;

    public onboarding_director_test(ITestOutputHelper output) => _out = output;

    /// <summary>A player mid-game: past the opening beats, so the opening lessons cannot win by default.</summary>
    private static LessonFacts Playing() => new(
        WavesCleared: 30, DeepestWave: 30, Gleam: 900, StatsTrained: 4,
        BossesFelled: 6, ChestsOpened: 2, ItemsOwned: 3, ItemsWorn: 2,
        RegionsEntered: 1, RegionsOpen: 1, HuntersOwned: 1,
        ReportOpenedEver: true, ChangedAfterFall: true, RetriedAfterChange: true);

    // ── 1. A DEED, NEVER A DISMISSAL ─────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_lesson_completes_only_from_a_real_fact()
    {
        // The catalogue has no dismissal input at all, which is the point: there is nowhere for a
        // closed card to become a socketed gem. Suppression is a separate argument to Next, and it is
        // a DISPLAY filter — the lesson stays incomplete underneath and returns when the mute lifts.
        var holding = Playing() with { GemsHeld = 1 };
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstGemSocket, holding));
        Assert.Equal(OnboardingLessonId.FirstGemSocket, OnboardingLessons.Next(holding));

        var muted = new[] { OnboardingLessonId.FirstGemSocket };
        Assert.Null(OnboardingLessons.Next(holding, muted));
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstGemSocket, holding));

        // ...and the deed, which is the only thing that completes it.
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstGemSocket, holding with { GemsSet = 1 }));
    }

    // ── 2. THE FAULT THAT KILLED THE LADDER ──────────────────────────────────────────────────────

    [Fact]
    public void test_a_gem_lesson_nobody_can_act_on_blocks_nothing()
    {
        // THE REGRESSION THIS WHOLE FILE IS FOR. Under the ladder a gem lesson with no gem sat at the
        // head of the queue and silenced everything behind it. Here it is simply not eligible, and
        // three unrelated lessons are chosen over it in turn.
        var noGem = Playing() with { GemsHeld = 0 };

        var fell = noGem with { Falls = 1, ReportOpenedEver = false };
        Assert.Equal(OnboardingLessonId.FirstFailureReport, OnboardingLessons.Next(fell));

        var mastery = noGem with { MasteryPointsFree = 2 };
        Assert.Equal(OnboardingLessonId.FirstMasterySpend, OnboardingLessons.Next(mastery));

        var conquered = noGem with { RegionsConquered = 1, RegionsOpen = 2 };
        Assert.Equal(OnboardingLessonId.FirstRegionTravel, OnboardingLessons.Next(conquered));

        // And with a gem in hand it is eligible again, still behind all three.
        var withGem = mastery with { GemsHeld = 1 };
        Assert.Contains(OnboardingLessonId.FirstGemSocket, OnboardingLessons.EligibleNow(withGem));
        Assert.Equal(OnboardingLessonId.FirstMasterySpend, OnboardingLessons.Next(withGem));
    }

    // ── 3. DONE BEFORE IT WAS EVER ASKED ─────────────────────────────────────────────────────────

    [Fact]
    public void test_a_lesson_performed_before_the_prompt_never_appears()
    {
        // A player who trains a stat in the first ten seconds is not told to train a stat. Completion
        // is derived from the fact, so there is no window in which the prompt could have been queued
        // and then arrive stale.
        var trained = new LessonFacts(WavesCleared: 20, Gleam: 500, StatsTrained: 3, RegionsEntered: 1, HuntersOwned: 1);
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstTrainingPurchase, trained));
        Assert.DoesNotContain(OnboardingLessonId.FirstTrainingPurchase, OnboardingLessons.EligibleNow(trained));
        Assert.NotEqual(OnboardingLessonId.FirstTrainingPurchase, OnboardingLessons.Next(trained));
    }

    // ── 4, 5. ONE AT A TIME, AND BY PRIORITY ─────────────────────────────────────────────────────

    [Fact]
    public void test_only_one_lesson_is_ever_chosen_however_many_are_eligible()
    {
        // The first conquest's own state: map, camp, roster and a keystone all arrive together, and
        // the player may be holding a gem and an unspent point on the same frame.
        var loud = Playing() with
        {
            RegionsConquered = 1, RegionsOpen = 2,
            KeystonesDiscovered = 1, GemsHeld = 1, MasteryPointsFree = 1,
            TraitsDiscovered = 3, HuntersOwned = 2,
            WarrenOpen = true, WarrenPaidOffline = true,
        };

        var eligible = OnboardingLessons.EligibleNow(loud);
        _out.WriteLine("eligible: " + string.Join(", ", eligible));
        Assert.True(eligible.Count >= 5, $"the fixture only posed {eligible.Count} lessons — it cannot show serialisation");

        // Exactly one is chosen, and it is the highest priority rather than the first declared.
        var chosen = OnboardingLessons.Next(loud);
        Assert.NotNull(chosen);
        Assert.Equal(eligible.OrderByDescending(OnboardingLessons.Priority).First(), chosen);
    }

    [Fact]
    public void test_priority_beats_declaration_order()
    {
        // FirstChestOpen is declared BEFORE FirstFailureReport in the enum, and loses to it — which is
        // the whole difference between this and the queue it replaced.
        var both = Playing() with { Falls = 1, ReportOpenedEver = false, ChestsHeld = 1, ChestsOpened = 0, ItemsOwned = 0, ItemsWorn = 0 };
        Assert.True((int)OnboardingLessonId.FirstChestOpen < (int)OnboardingLessonId.FirstFailureReport,
                    "the fixture no longer poses the order it is about");
        Assert.Equal(OnboardingLessonId.FirstFailureReport, OnboardingLessons.Next(both));
    }

    // ── 6. AVAILABLE IS NOT TAUGHT ───────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_first_conquest_teaches_travel_and_lets_the_rest_wait()
    {
        // Conquest makes MAP, WARREN, ROSTER and the first keystone available in one frame. Every one
        // is AVAILABLE; exactly one is TAUGHT, and it is the one that continues the loop.
        var justConquered = Playing() with
        {
            RegionsConquered = 1, RegionsOpen = 2,
            KeystonesDiscovered = 1, HuntersOwned = 2,
            WarrenOpen = true, WarrenPaidOffline = true,
        };
        Assert.Equal(OnboardingLessonId.FirstRegionTravel, OnboardingLessons.Next(justConquered));

        // ...and once travelled, the rest are still there, in their own order, one at a time.
        var travelled = justConquered with { RegionsEntered = 2 };
        Assert.Equal(OnboardingLessonId.FirstKeystoneChoice, OnboardingLessons.Next(travelled));
        var keyed = travelled with { KeystonesWorn = 1 };
        Assert.Equal(OnboardingLessonId.FirstWarrenReturn, OnboardingLessons.Next(keyed));
    }

    // ── 7. RETURNING SAVES ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_returning_save_is_not_walked_through_the_fall_loop_again()
    {
        // The three fall-loop facts are the only ones onboarding persists, and a save written before
        // they existed carries none. Somebody who has conquered a region has fallen and gone again
        // many times; replaying READ THE LOG would be the game admitting it was not watching.
        var veteran = new LessonFacts(WavesCleared: 400, DeepestWave: 120, RegionsConquered: 3,
                                      StatsTrained: 40, ItemsWorn: 8, MasterySpent: 12,
                                      RegionsEntered: 3, HuntersOwned: 4, Falls: 30);
        Assert.True(OnboardingLessons.SeedFallLoopAsLived(veteran));

        var seeded = veteran with { ReportOpenedEver = true, ChangedAfterFall = true, RetriedAfterChange = true };
        foreach (var id in new[] { OnboardingLessonId.FirstFailureReport,
                                   OnboardingLessonId.FirstPostFailureChange,
                                   OnboardingLessonId.FirstRetry })
            Assert.True(OnboardingLessons.Completed(id, seeded), $"{id} would replay on a veteran save");

        // A genuinely new player is NOT seeded — the loop is the one thing they must actually live.
        Assert.False(OnboardingLessons.SeedFallLoopAsLived(new LessonFacts(WavesCleared: 3, Falls: 1)));
    }

    // ── 8. GLOBAL SKIP ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_skip_guidance_silences_presentation_and_fabricates_nothing()
    {
        var fresh = new LessonFacts(WavesCleared: 0, HuntersOwned: 1);
        Assert.Equal(OnboardingLessonId.FirstFight, OnboardingLessons.Next(fresh));

        var off = fresh with { GuidanceOff = true };
        Assert.Null(OnboardingLessons.Next(off));

        // Nothing was completed, no gate moved, no fact invented: the lessons are simply not spoken.
        foreach (var id in OnboardingLessons.All)
            Assert.Equal(OnboardingLessons.Completed(id, fresh), OnboardingLessons.Completed(id, off));
        Assert.Equal(OnboardingLessons.EligibleNow(fresh), OnboardingLessons.EligibleNow(off));
    }

    // ── 9, 10, 11. THE BUILD INVARIANTS THE GUIDANCE MUST RESPECT ────────────────────────────────

    [Fact]
    public void test_shared_skill_guidance_reads_live_mastery_access_not_a_latch()
    {
        // Access to a shared skill follows the CURRENT allocation: a respec that gives the road back
        // takes the skill with it. So the lesson asks the live count, and a player who reset their
        // tree stops being told to equip something they can no longer reach.
        var open = Playing() with { SharedSkillsAvailable = 2, EmptySkillSlots = 1 };
        Assert.Contains(OnboardingLessonId.FirstSharedSkillEquip, OnboardingLessons.EligibleNow(open));

        var respecced = open with { SharedSkillsAvailable = 0 };
        Assert.DoesNotContain(OnboardingLessonId.FirstSharedSkillEquip, OnboardingLessons.EligibleNow(respecced));

        // ...and with no slot free there is nowhere to put one, so it waits rather than nagging.
        Assert.DoesNotContain(OnboardingLessonId.FirstSharedSkillEquip,
                              OnboardingLessons.EligibleNow(open with { EmptySkillSlots = 0 }));
    }

    [Fact]
    public void test_the_shared_skill_lesson_never_names_a_skill()
    {
        // FORCE "MAKE A CHOICE", NEVER "WHICH CHOICE". The catalogue holds no SkillId, no stat, no
        // node and no region anywhere, so it cannot prescribe one — which also means it cannot
        // conflict with the one-slot-per-skill rule or with a signature's ownership.
        foreach (var id in OnboardingLessons.All)
        {
            var text = OnboardingLessons.Title(id) + " " + OnboardingLessons.Action(id) + " " + OnboardingLessons.Why(id);
            foreach (var named in new[] { "BLOW", "SPRAY", "MIGHT", "RESONANCE", "HARD HANDS", "VERDANT" })
                Assert.DoesNotContain(named, text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal("EQUIP ONE SHARED SKILL", OnboardingLessons.Action(OnboardingLessonId.FirstSharedSkillEquip));
        Assert.Equal("TRAIN ANY STAT ONCE", OnboardingLessons.Action(OnboardingLessonId.FirstTrainingPurchase));
        Assert.Equal("SPEND ONE MASTERY POINT", OnboardingLessons.Action(OnboardingLessonId.FirstMasterySpend));
    }

    // ── 12, 13, 14. THE THREE DEEDS THAT ARE EASY TO FAKE ────────────────────────────────────────

    [Fact]
    public void test_variation_gem_and_trait_complete_from_their_own_real_state()
    {
        var f = Playing();

        // A variation OFFERED is not a variation CHOSEN.
        var offered = f with { VariationsOffered = 1 };
        Assert.Contains(OnboardingLessonId.FirstVariationChoice, OnboardingLessons.EligibleNow(offered));
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstVariationChoice, offered));
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstVariationChoice, offered with { VariationsChosen = 1 }));

        // A gem HELD is not a gem SET.
        var held = f with { GemsHeld = 2 };
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstGemSocket, held));
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstGemSocket, held with { GemsSet = 1 }));

        // A trait DISCOVERED is not a trait WORN. Three are worn at a time and none is bought.
        var known = f with { TraitsDiscovered = 5 };
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstTraitEquip, known));
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstTraitEquip, known with { TraitsEquipped = 1 }));
    }

    // ── THE CLIMAX, AS A SEQUENCE ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_fall_loop_runs_read_then_change_then_retry_and_outranks_everything()
    {
        // The strongest first-session milestone there is: opened the report, changed something, went
        // back down. Each waits on the one before it, and all three outrank every other lesson —
        // including a chest sitting unopened and a point waiting to be spent.
        var fell = new LessonFacts(WavesCleared: 24, DeepestWave: 24, Gleam: 400, StatsTrained: 1,
                                   BossesFelled: 4, ChestsHeld: 1, ItemsOwned: 1, ItemsWorn: 1,
                                   MasteryPointsFree: 1, RegionsEntered: 1, HuntersOwned: 1, Falls: 1);

        Assert.Equal(OnboardingLessonId.FirstFailureReport, OnboardingLessons.Next(fell));

        var read = fell with { ReportOpenedEver = true };
        Assert.Equal(OnboardingLessonId.FirstPostFailureChange, OnboardingLessons.Next(read));

        var changed = read with { ChangedAfterFall = true };
        Assert.Equal(OnboardingLessonId.FirstRetry, OnboardingLessons.Next(changed));

        // ...and once the loop has been lived it stops asking, and the ordinary lessons resume.
        var done = changed with { RetriedAfterChange = true };
        Assert.NotEqual(OnboardingLessonId.FirstRetry, OnboardingLessons.Next(done));
        Assert.Equal(OnboardingLessonId.FirstMasterySpend, OnboardingLessons.Next(done));
    }

    [Fact]
    public void test_nothing_in_the_fall_loop_is_eligible_before_a_fall()
    {
        var never = Playing() with { Falls = 0, ReportOpenedEver = false, ChangedAfterFall = false, RetriedAfterChange = false };
        foreach (var id in new[] { OnboardingLessonId.FirstFailureReport,
                                   OnboardingLessonId.FirstPostFailureChange,
                                   OnboardingLessonId.FirstRetry })
            Assert.False(OnboardingLessons.Eligible(id, never), $"{id} is eligible before the player has ever fallen");
    }

    // ── THE OPENING, AND WHAT IT MUST NOT BE ─────────────────────────────────────────────────────

    [Fact]
    public void test_a_brand_new_save_is_told_one_thing_and_it_asks_for_nothing()
    {
        var fresh = new LessonFacts(HuntersOwned: 1);
        var first = OnboardingLessons.Next(fresh);

        Assert.Equal(OnboardingLessonId.FirstFight, first);
        Assert.Equal(LessonMode.Observe, OnboardingLessons.Mode(first!.Value));
        Assert.Equal("", OnboardingLessons.Action(first.Value));   // it asks for nothing
        Assert.Null(OnboardingLessons.Sends(first.Value));          // and sends nowhere
        Assert.Equal("YOUR HUNTER FIGHTS FOR YOU", OnboardingLessons.Title(first.Value));

        // One cleared wave is the whole of it, and then silence.
        var watched = fresh with { WavesCleared = 1, DeepestWave = 1 };
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstFight, watched));
        Assert.Null(OnboardingLessons.Next(watched));
    }

    [Fact]
    public void test_the_chest_lesson_never_waits_on_a_drop_roll()
    {
        // The critical path may not depend on a 20% boss roll, so the lesson is eligible on HOLDING a
        // chest — which a new save does, from the welcome gift — and never on felling bosses until one
        // drops. A player with no chest is simply not asked.
        var gifted = new LessonFacts(WavesCleared: 3, DeepestWave: 3, ChestsHeld: 1, HuntersOwned: 1);
        Assert.Contains(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(gifted));

        var none = gifted with { ChestsHeld = 0 };
        Assert.DoesNotContain(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(none));
    }

    [Fact]
    public void test_the_warren_is_taught_only_after_it_has_actually_paid()
    {
        var open = Playing() with { WarrenOpen = true, WarrenPaidOffline = false };
        Assert.DoesNotContain(OnboardingLessonId.FirstWarrenReturn, OnboardingLessons.EligibleNow(open));

        var paid = open with { WarrenPaidOffline = true };
        Assert.Contains(OnboardingLessonId.FirstWarrenReturn, OnboardingLessons.EligibleNow(paid));
    }

    // ── THE COPY RULES, AS ASSERTIONS ────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_prompt_is_a_prompt_and_not_documentation()
    {
        foreach (var id in OnboardingLessons.All)
        {
            var title = OnboardingLessons.Title(id);
            var action = OnboardingLessons.Action(id);
            var why = OnboardingLessons.Why(id);
            if (title.Length == 0) continue;

            Assert.InRange(title.Split(' ').Length, 1, 5);
            if (action.Length > 0) Assert.InRange(action.Split(' ').Length, 2, 8);
            Assert.True(why.Length <= 40, $"{id}'s second line is a paragraph: \"{why}\"");

            // A guided lesson names its deed; an Observe lesson asks for nothing.
            if (OnboardingLessons.Mode(id) == LessonMode.HardGuide)
                Assert.True(action.Length > 0, $"{id} is a HardGuide with no deed to wait for");
            if (OnboardingLessons.Mode(id) == LessonMode.Observe)
                Assert.Equal("", action);
        }
    }

    [Fact]
    public void test_no_retired_vocabulary_survives_in_a_prompt()
    {
        // Resonance Hunter, Starting Skill, Trait Tree, Memory Tree and the Source x Form vocabulary
        // are all gone from the game; a tutorial is the easiest place for a dead word to come back.
        string[] dead = { "RESONANCE HUNTER", "STARTING SKILL", "TRAIT TREE", "MEMORY TREE", "APTITUDE", "FORM " };
        foreach (var id in OnboardingLessons.All)
        {
            var text = $"{OnboardingLessons.Title(id)} {OnboardingLessons.Action(id)} {OnboardingLessons.Why(id)}";
            foreach (var word in dead)
                Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void test_every_guided_lesson_points_at_a_screen_and_a_control()
    {
        // A HardGuide lights a real production rectangle, so every one of them has to name both the
        // screen the deed happens on and the semantic target the screen resolves.
        foreach (var id in OnboardingLessons.All)
        {
            if (OnboardingLessons.Mode(id) == LessonMode.Observe) continue;
            if (id == OnboardingLessonId.FirstFailureReport
                || id == OnboardingLessonId.FirstPostFailureChange
                || id == OnboardingLessonId.FirstRetry) continue;   // the fall loop lives on the HUNT
            Assert.NotNull(OnboardingLessons.Sends(id));
            Assert.NotNull(OnboardingLessons.Target(id));
        }
    }

    // ── THE TWO CONSTANTS THAT MIRROR REAL TUNING ────────────────────────────────────────────────

    [Fact]
    public void test_the_first_rank_cost_and_the_boss_cadence_match_the_real_tuning()
    {
        Assert.Equal(OnboardingLessons.FirstRankCost, Hunter.CostOfRank(0, new ProgressionTuning()));
        Assert.Equal(OnboardingLessons.BossEvery, ExpeditionTuning.Default.BossEvery);
    }
}
