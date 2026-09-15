using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Persistence;
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
        var holding = Playing() with { GemsHeld = 1, CanSocketNow = true };
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

        // And with a gem in hand AND somewhere to put it, it is eligible again, still behind all three.
        var withGem = mastery with { GemsHeld = 1, CanSocketNow = true };
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
            KeystonesDiscovered = 1, GemsHeld = 1, CanSocketNow = true, MasteryPointsFree = 1,
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

    // ── 6a. THE GEM LESSON IS ACTIONABLE OR SILENT ───────────────────────────────────────────────

    [Fact]
    public void test_the_gem_lesson_waits_for_a_socket_the_forge_would_actually_allow()
    {
        // FOUR STATES, and only one of them may speak. The lesson's words are SOCKET YOUR FIRST GEM,
        // so anything else is an instruction the Forge then refuses — which is the failure mode this
        // whole catalogue was built to stop. The fact is the Forge's own answer (GemCraft.CanSocketNow);
        // this only pins that the lesson reads it and reads nothing else.
        var playing = Playing();

        // 1. A gem in the bag with nowhere legal to put it. Silent.
        var gemNoHost = playing with { GemsHeld = 1, CanSocketNow = false };
        Assert.DoesNotContain(OnboardingLessonId.FirstGemSocket, OnboardingLessons.EligibleNow(gemNoHost));

        // 2. A socketable item and no gem. Silent.
        var hostNoGem = playing with { GemsHeld = 0, CanSocketNow = false };
        Assert.DoesNotContain(OnboardingLessonId.FirstGemSocket, OnboardingLessons.EligibleNow(hostNoGem));

        // 3. Both. Now it may ask, and the deed can be done the moment it is asked for.
        var both = playing with { GemsHeld = 1, CanSocketNow = true };
        Assert.Contains(OnboardingLessonId.FirstGemSocket, OnboardingLessons.EligibleNow(both));

        // 4. Already set. Complete by the deed, and never shown again — even with another gem in hand.
        var done = both with { GemsSet = 1 };
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstGemSocket, done));
        Assert.DoesNotContain(OnboardingLessonId.FirstGemSocket, OnboardingLessons.EligibleNow(done));
        Assert.NotEqual(OnboardingLessonId.FirstGemSocket, OnboardingLessons.Next(done));

        // AND IT BLOCKS NOTHING while it waits: an unopenable gem lesson never suppresses a lesson
        // that has nothing to do with gems.
        var alsoOwed = gemNoHost with { MasteryPointsFree = 1 };
        Assert.Equal(OnboardingLessonId.FirstMasterySpend, OnboardingLessons.Next(alsoOwed));
    }

    // ── 6b. THE OPENING, IN ORDER ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_opening_teaches_fight_then_training_then_the_chest_then_the_item()
    {
        // THE JOURNEY THIS PINS, and the drift it catches. A new account owns the welcome gift chest
        // from frame one, so on priority alone (chest 80 > item 78 > training 76) the opening came out
        // as FIGHT -> OPEN CHEST -> EQUIP -> TRAIN: the loot loop before the decision loop, with the
        // one screen the whole game is built on left until last. The intended order is
        //
        //     OBSERVE THE FIRST FIGHT -> TRAIN ANY STAT ONCE -> OPEN THE GIFT CHEST -> EQUIP ONE ITEM
        //
        // Priority alone cannot express it, because the chest is ELIGIBLE two waves before training
        // is. The eligibility clause is what holds the order; this test fails if EITHER the clause or
        // the ranking drifts, which is why it walks the real facts rather than asserting the numbers.
        var chests = GiftChests.NewGameChests().Count;
        Assert.True(chests >= 1, "the fresh-save journey assumes the welcome gift — SeedNewGame stopped giving it");

        // 1. FRAME ONE. Nothing has happened, nothing is owned but the gift, and one thing is said.
        var fresh = new LessonFacts(ChestsHeld: chests, HuntersOwned: 1);
        Assert.Equal(OnboardingLessonId.FirstFight, OnboardingLessons.Next(fresh));
        Assert.Equal(LessonMode.Observe, OnboardingLessons.Mode(OnboardingLessonId.FirstFight));

        // 2. THE FIRST WAVE CLEARS — and the chest does NOT jump the queue, even though it is held.
        //    Training is not actionable yet either (the screen opens at wave 3), so the game is quiet.
        var wave1 = fresh with { WavesCleared = 1, DeepestWave = 1, Gleam = 6 };
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstFight, wave1));
        Assert.Null(OnboardingLessons.Next(wave1));

        // 3. TRAINING OPENS and the purse can afford a rank. This is the first decision the game asks
        //    for, and it asks for A stat, never a particular one.
        var canTrain = wave1 with { WavesCleared = Unlocks.TrainingOpensAtWaves, DeepestWave = Unlocks.TrainingOpensAtWaves,
                                    Gleam = OnboardingLessons.FirstRankCost };
        Assert.Equal(OnboardingLessonId.FirstTrainingPurchase, OnboardingLessons.Next(canTrain));
        Assert.Equal("TRAIN ANY STAT ONCE", OnboardingLessons.Action(OnboardingLessonId.FirstTrainingPurchase));

        // 4. ONE RANK BOUGHT. Now the chest — the reward for the decision, not before it.
        var trained = canTrain with { StatsTrained = 1, Gleam = 0 };
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstTrainingPurchase, trained));
        Assert.Equal(OnboardingLessonId.FirstChestOpen, OnboardingLessons.Next(trained));

        // 5. THE CHEST OPENED, deterministically leaving one wearable item. Then, and only then, EQUIP.
        var opened = trained with { ChestsHeld = 0, ChestsOpened = 1, ItemsOwned = 1 };
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstChestOpen, opened));
        Assert.Equal(OnboardingLessonId.FirstItemEquip, OnboardingLessons.Next(opened));

        // 6. WORN. The opening is done and the game goes quiet until something new is true.
        var worn = opened with { ItemsWorn = 1 };
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstItemEquip, worn));
        Assert.Null(OnboardingLessons.Next(worn));
    }

    [Fact]
    public void test_opening_the_gift_chest_early_still_does_not_jump_the_training_lesson()
    {
        // THE OTHER ROUTE INTO THE SAME INVERSION, and the one gating only the chest missed. The VAULT
        // is open from frame one and its tile wears an unread mark, so a curious player can open the
        // welcome chest at wave 0. That completes the chest lesson and mints one wearable weapon —
        // and EQUIP ONE ITEM (78) outranks TRAIN ANY STAT ONCE (76), so the journey came out
        // FIGHT -> EQUIP -> TRAIN with the chest clause in place and doing nothing about it.
        var openedEarly = new LessonFacts(WavesCleared: 1, DeepestWave: 1, Gleam: 6,
                                          ChestsOpened: 1, ItemsOwned: 1, HuntersOwned: 1);
        Assert.True(OnboardingLessons.Completed(OnboardingLessonId.FirstChestOpen, openedEarly));
        Assert.DoesNotContain(OnboardingLessonId.FirstItemEquip, OnboardingLessons.EligibleNow(openedEarly));
        Assert.Null(OnboardingLessons.Next(openedEarly));

        // At the wave TRAINING opens, with a rank affordable: the decision is what is asked for, even
        // though a wearable has been sitting in the bag for three waves.
        var canTrain = openedEarly with { WavesCleared = Unlocks.TrainingOpensAtWaves,
                                          DeepestWave = Unlocks.TrainingOpensAtWaves,
                                          Gleam = OnboardingLessons.FirstRankCost };
        Assert.Equal(OnboardingLessonId.FirstTrainingPurchase, OnboardingLessons.Next(canTrain));

        // ...and only then the item.
        var trained = canTrain with { StatsTrained = 1, Gleam = 0 };
        Assert.Equal(OnboardingLessonId.FirstItemEquip, OnboardingLessons.Next(trained));
    }

    [Fact]
    public void test_the_gift_chest_never_speaks_before_the_first_training_decision()
    {
        // The narrow claim, stated on its own so a priority edit cannot quietly restore the old order
        // by making the chest outrank training again: it is the ELIGIBILITY that holds, at every wave
        // and any purse, for as long as no stat has been trained.
        for (var wave = 0; wave <= 12; wave++)
        {
            var untrained = new LessonFacts(WavesCleared: wave, DeepestWave: wave, Gleam: 5_000,
                                            ChestsHeld: 3, HuntersOwned: 1);
            Assert.DoesNotContain(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(untrained));
            Assert.NotEqual(OnboardingLessonId.FirstChestOpen, OnboardingLessons.Next(untrained));

            // The same for the item, whether it arrived from the chest or from anywhere else.
            var owning = untrained with { ChestsOpened = 1, ItemsOwned = 4 };
            Assert.DoesNotContain(OnboardingLessonId.FirstItemEquip, OnboardingLessons.EligibleNow(owning));
            Assert.NotEqual(OnboardingLessonId.FirstItemEquip, OnboardingLessons.Next(owning));
        }
    }

    // ── 7. RETURNING SAVES ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_returning_save_is_not_walked_through_the_fall_loop_again()
    {
        // The three fall-loop facts are the only ones onboarding persists, and a save written before
        // they existed carries none. Somebody who has conquered a region has fallen and gone again
        // many times; replaying READ THE LOG would be the game admitting it was not watching.
        const int old = OnboardingLessons.FirstVersionWithFallLoopFacts - 1;
        var veteran = new LessonFacts(WavesCleared: 400, DeepestWave: 120, RegionsConquered: 3,
                                      StatsTrained: 40, ItemsWorn: 8, MasterySpent: 12,
                                      RegionsEntered: 3, HuntersOwned: 4, Falls: 30);
        Assert.True(OnboardingLessons.SeedFallLoopAsLived(veteran, old));

        var seeded = veteran with { ReportOpenedEver = true, ChangedAfterFall = true, RetriedAfterChange = true };
        foreach (var id in new[] { OnboardingLessonId.FirstFailureReport,
                                   OnboardingLessonId.FirstPostFailureChange })
            Assert.True(OnboardingLessons.Completed(id, seeded), $"{id} would replay on a veteran save");

        // A genuinely new player is NOT seeded — the loop is the one thing they must actually live.
        Assert.False(OnboardingLessons.SeedFallLoopAsLived(new LessonFacts(WavesCleared: 3, Falls: 1), old));
    }

    [Fact]
    public void test_a_new_save_that_conquered_before_it_ever_fell_is_not_treated_as_a_veteran()
    {
        // THE BUG THIS CLOSES. The seed used to read progression alone: "has conquered a region and
        // has never opened a report" was taken to mean "a file written before those facts existed".
        // It also describes somebody who started a NEW game on this build, ran to a conquest without
        // dying, quit, and came back — and they were handed the veteran's answer, which silently
        // deletes READ THE LOG and MAKE ONE CHANGE. That is the most important lesson in the game and
        // the only one a first session exists to teach.
        var conqueredEarly = new LessonFacts(WavesCleared: 20, DeepestWave: 20, RegionsConquered: 1,
                                             StatsTrained: 6, ItemsWorn: 3, RegionsEntered: 1,
                                             HuntersOwned: 2, Falls: 0);

        // Written by THIS build: the file carries the facts, so false means false.
        Assert.False(OnboardingLessons.SeedFallLoopAsLived(conqueredEarly, SaveGame.CurrentVersion));
        Assert.False(OnboardingLessons.SeedFallLoopAsLived(conqueredEarly,
                                                           OnboardingLessons.FirstVersionWithFallLoopFacts));

        // ...and the lesson survives the reload it used to be deleted by.
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstFailureReport, conqueredEarly));
        var fell = conqueredEarly with { Falls = 1 };
        Assert.Equal(OnboardingLessonId.FirstFailureReport, OnboardingLessons.Next(fell));

        // The same progression in a file that PREDATES the facts is the veteran, and is still seeded.
        Assert.True(OnboardingLessons.SeedFallLoopAsLived(
            conqueredEarly, OnboardingLessons.FirstVersionWithFallLoopFacts - 1));
    }

    [Fact]
    public void test_the_fall_loop_migration_version_is_frozen_at_five()
    {
        // IT MUST NEVER BE SaveGame.CurrentVersion. Written that way, the next bump would start
        // seeding every version-5 file — files that carry the facts honestly — and every player who
        // had not yet lived the loop would be marked as having lived it. The number names a moment in
        // this project's history, and that moment does not move when the format does.
        Assert.Equal(5, OnboardingLessons.FirstVersionWithFallLoopFacts);
        Assert.True(OnboardingLessons.FirstVersionWithFallLoopFacts <= SaveGame.CurrentVersion,
                    "the fall-loop facts cannot first appear in a version that does not exist yet");
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
    public void test_the_fall_loop_runs_read_then_change_and_outranks_everything()
    {
        // The strongest first-session milestone there is: opened the report, changed something. Each
        // waits on the one before it, and both outrank every other lesson — including a chest sitting
        // unopened and a point waiting to be spent.
        var fell = new LessonFacts(WavesCleared: 24, DeepestWave: 24, Gleam: 400, StatsTrained: 1,
                                   BossesFelled: 4, ChestsHeld: 1, ChestsOpened: 2, ItemsOwned: 1, ItemsWorn: 1,
                                   MasteryPointsFree: 1, RegionsEntered: 1, HuntersOwned: 1, Falls: 1);

        Assert.Equal(OnboardingLessonId.FirstFailureReport, OnboardingLessons.Next(fell));

        var read = fell with { ReportOpenedEver = true };
        Assert.Equal(OnboardingLessonId.FirstPostFailureChange, OnboardingLessons.Next(read));

        // ...and once the change is made the two presented lessons are both satisfied, and the
        // ordinary lessons resume — the retry that follows is automatic and never asked for.
        var changed = read with { ChangedAfterFall = true };
        Assert.Equal(OnboardingLessonId.FirstMasterySpend, OnboardingLessons.Next(changed));
    }

    [Fact]
    public void test_the_retry_is_recorded_silently_and_never_presented()
    {
        // THE THIRD STEP OF THE LOOP IS NEVER A LESSON. Its deed — a new descent — happens by itself,
        // 1.6 seconds after the fall, whether or not the player is even looking. So once fell, read
        // and changed are all true and the next descent has begun (RetriedAfterChange latches), the
        // catalogue must have nothing left to say about it: no lesson id names it, and Next() moves on
        // to whatever the player has not yet done.
        Assert.DoesNotContain(OnboardingLessons.All,
                              id => id.ToString().Contains("Retry", StringComparison.OrdinalIgnoreCase));

        var fell = new LessonFacts(WavesCleared: 24, DeepestWave: 24, Gleam: 400, StatsTrained: 1,
                                   BossesFelled: 4, ChestsHeld: 1, ChestsOpened: 2, ItemsOwned: 1, ItemsWorn: 1,
                                   MasteryPointsFree: 1, RegionsEntered: 1, HuntersOwned: 1, Falls: 1);
        var read = fell with { ReportOpenedEver = true };
        var changed = read with { ChangedAfterFall = true };
        var retried = changed with { RetriedAfterChange = true };   // the next descent already began

        Assert.True(retried.RetriedAfterChange, "the milestone fact itself must still be true");
        Assert.Equal(OnboardingLessons.Next(changed), OnboardingLessons.Next(retried));
        Assert.Equal(OnboardingLessonId.FirstMasterySpend, OnboardingLessons.Next(retried));
    }

    [Fact]
    public void test_nothing_in_the_fall_loop_is_eligible_before_a_fall()
    {
        var never = Playing() with { Falls = 0, ReportOpenedEver = false, ChangedAfterFall = false, RetriedAfterChange = false };
        foreach (var id in new[] { OnboardingLessonId.FirstFailureReport,
                                   OnboardingLessonId.FirstPostFailureChange })
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
        var gifted = new LessonFacts(WavesCleared: 3, DeepestWave: 3, ChestsHeld: 1, StatsTrained: 1, HuntersOwned: 1);
        Assert.Contains(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(gifted));

        var none = gifted with { ChestsHeld = 0 };
        Assert.DoesNotContain(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(none));

        // ...AND IT STILL WAITS ITS TURN. The gift is owned from frame one, so without this the chest
        // would be asked for at wave 1 — before TRAINING has even opened — and the first decision the
        // game teaches would be a loot screen. The chest is second, after one training rank.
        var untrained = gifted with { StatsTrained = 0 };
        Assert.DoesNotContain(OnboardingLessonId.FirstChestOpen, OnboardingLessons.EligibleNow(untrained));
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
                || id == OnboardingLessonId.FirstPostFailureChange) continue;   // the fall loop lives on the HUNT
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

    // ── THE BACK HALF ────────────────────────────────────────────────────────────────────────────

    /// <summary>The five systems the coverage pass gave a voice to, and the fact each waits on.</summary>
    private static readonly (OnboardingLessonId Id, LessonFacts Can, LessonFacts Done)[] BackHalf =
    {
        (OnboardingLessonId.FirstAutoSell,
         new LessonFacts(ItemsOwned: 9, AutoSellUnlocked: true),
         new LessonFacts(ItemsOwned: 9, AutoSellUnlocked: true, AutoSellOn: true)),

        (OnboardingLessonId.FirstVowSworn,
         new LessonFacts(SharedSkillsEquipped: 1, VowsKnown: 2),
         new LessonFacts(SharedSkillsEquipped: 1, VowsKnown: 2, VowsSworn: 1)),

        (OnboardingLessonId.FirstSpecialisation,
         new LessonFacts(SpecialisationReachable: true),
         new LessonFacts(SpecialisationReachable: true, SpecialisationTaken: true)),

        (OnboardingLessonId.FirstTraderVisit,
         new LessonFacts(Gleam: 4000, TraderStocked: true),
         new LessonFacts(Gleam: 4000, TraderStocked: true, TraderBought: 1)),

        (OnboardingLessonId.FirstCorruptionOffer,
         new LessonFacts(CanDeepenWorld: true),
         new LessonFacts(CanDeepenWorld: true, CorruptionTier: 1)),
    };

    [Fact]
    public void test_a_back_half_lesson_waits_for_its_system_to_exist()
    {
        // The back half is where a premature prompt costs the most: these systems open long after the
        // player has learned to trust the cards, so one that cannot be obeyed poisons every card after
        // it. On an empty account none of the five is eligible, and none of them is COMPLETE either —
        // silence has to be honest about what has not been learned.
        var empty = new LessonFacts();
        foreach (var (id, _, _) in BackHalf)
        {
            Assert.DoesNotContain(id, OnboardingLessons.EligibleNow(empty));
            Assert.False(OnboardingLessons.Completed(id, empty), $"{id} completes on an empty account");
        }
    }

    [Fact]
    public void test_a_back_half_lesson_arrives_when_it_can_be_acted_on_and_ends_on_the_deed()
    {
        foreach (var (id, can, done) in BackHalf)
        {
            Assert.Contains(id, OnboardingLessons.EligibleNow(can));
            Assert.False(OnboardingLessons.Completed(id, can), $"{id} is complete before the deed");
            Assert.True(OnboardingLessons.Completed(id, done), $"{id} does not complete on its own deed");
        }
    }

    [Fact]
    public void test_the_back_half_never_outranks_the_first_hour()
    {
        // A card about the trader must never win over READ THE LOG. Every one of these arrives while
        // the player is in the middle of something older and more urgent, so the whole band ranks
        // below the lowest first-hour lesson.
        var firstHour = new[]
        {
            OnboardingLessonId.FirstFight, OnboardingLessonId.FirstChestOpen,
            OnboardingLessonId.FirstItemEquip, OnboardingLessonId.FirstTrainingPurchase,
            OnboardingLessonId.FirstFailureReport, OnboardingLessonId.FirstMasterySpend,
            OnboardingLessonId.FirstRegionTravel, OnboardingLessonId.FirstKeystoneChoice,
        };
        var floor = firstHour.Min(OnboardingLessons.Priority);
        foreach (var (id, _, _) in BackHalf)
            Assert.True(OnboardingLessons.Priority(id) < floor,
                        $"{id} ({OnboardingLessons.Priority(id)}) outranks a first-hour lesson ({floor})");
    }

    [Fact]
    public void test_the_back_half_offers_rather_than_instructs()
    {
        // Four of the five are DECISIONS — a Vow, a discipline, a purchase, the valve — and the fifth
        // is a switch. None of them is a step in a sequence the player is being walked through, so
        // none of them takes the HardGuide's bracket-and-wait shape.
        foreach (var (id, _, _) in BackHalf)
            Assert.Equal(LessonMode.SoftGuide, OnboardingLessons.Mode(id));
    }

    [Fact]
    public void test_the_back_half_says_where_it_is_and_what_it_lights()
    {
        // A lesson about a screen the player is not on has to name that screen, or the guide points
        // at nothing; and it has to name a control there, or the spotlight lights the whole page.
        foreach (var (id, _, _) in BackHalf)
        {
            Assert.NotNull(OnboardingLessons.Sends(id));
            Assert.NotNull(OnboardingLessons.Target(id));
            Assert.NotEqual("", OnboardingLessons.Title(id));
            Assert.NotEqual("", OnboardingLessons.Action(id));
        }
    }

    [Fact]
    public void test_the_forge_is_never_lectured()
    {
        // The coverage pass deliberately did NOT add an "OPEN THE FORGE" card. The screen has four
        // jobs and naming them all is the opening lecture this catalogue replaced; each is taught
        // when it first matters instead. Recorded here so a later pass has to argue with it rather
        // than rediscover it.
        var toTheForge = OnboardingLessons.All.Where(id => OnboardingLessons.Sends(id) == Activity.Forge).ToArray();
        Assert.Equal(new[] { OnboardingLessonId.FirstGemSocket }, toTheForge);
        // ...and the one that does goes for a DEED, pointing at the bag the gem is in.
        Assert.Equal(TourTarget.Bag, OnboardingLessons.Target(OnboardingLessonId.FirstGemSocket));
    }
}
