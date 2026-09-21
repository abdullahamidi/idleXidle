using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE AUTHORED OPENING, DRIVEN. The cursor, the holds, the input authority and the resume.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OpeningScript"/>'s own tests assert what is WRITTEN — the order, the copy rule, that
/// training is not in it. This file asserts what HAPPENS: that a career walked through the script one
/// production fact at a time reaches the end in the authored order, that the fight is held on every
/// beat that says it is held and on none that does not, and that a reload never asks for a deed the
/// save can prove was already done.
/// </para>
/// <para>
/// Nothing here touches a graphics device. The director takes a record of facts and returns a cursor,
/// which is the whole reason the split exists: the part of the opening that can be reasoned about is
/// the part that can be tested, and the part that draws is the part a capture looks at.
/// </para>
/// </remarks>
public class OpeningDirectorTest
{
    /// <summary>A director standing on one stage, with the acknowledgement spent.</summary>
    private static OpeningDirector At(OpeningStage stage)
    {
        var d = new OpeningDirector();
        d.Restore(stage);
        return d;
    }

    /// <summary>Answer whatever the current step is waiting for, and step once.</summary>
    /// <remarks>
    /// The point of the walk below: every advance is a fact the GAME produces. There is no "next"
    /// call anywhere in this file that is not one of these, which is the property that separates an
    /// authored sequence from a slideshow.
    /// </remarks>
    private static OpeningFacts Answer(OpeningStep step, OpeningFacts f) => step.Gate switch
    {
        StageGate.ArrivalSettled => f with { ArrivalSettled = true },
        StageGate.EnemySettled => f with { EnemySettled = true },
        StageGate.FirstRewardShown => f with { RewardsCredited = f.RewardsCredited + 1, ClearShown = true },
        StageGate.SignatureImminent => f with { SignatureHeld = true },
        StageGate.SignatureLanded => f with { SignatureLanded = true },
        StageGate.OneWaveCleared => f with { RewardsCredited = f.RewardsCredited + 1 },
        StageGate.BossSettled => f with { BossSettled = true },
        StageGate.BossFelled => f with { BossesFelled = 1, ClearShown = true },
        StageGate.OnScreen => f with { Screen = step.Screen ?? f.Screen },
        // BOTH HALVES, because the gate asks for both: the screen has to be open and the purse has to
        // cover a rank. A harness that answered only one of them would let the chapter start over rows
        // that refuse — the exact state the gate exists to wait out.
        StageGate.TrainingAffordable => f with { TrainingOpen = true, CanAffordFirstRank = true },
        // THE DEED, and it is the Hunter's own summed ranks, so it is a number going up and not a flag.
        StageGate.StatTrained => f with { StatsTrained = f.StatsTrained + 1 },
        StageGate.ChestOpened => f with { ChestsOpened = 1 },
        StageGate.ItemSelected => f with { ItemSelected = true },
        StageGate.ItemWorn => f with { ItemsWorn = 1 },
        _ => f,
    };

    [Fact]
    public void test_a_fresh_career_walks_the_whole_script_in_the_authored_order()
    {
        var d = new OpeningDirector();
        d.Begin();
        var facts = new OpeningFacts();
        var walked = new List<OpeningStage>();

        // A generous bound, not a count: if the cursor ever stops moving this loop ends and the
        // comparison below names the stage it stopped on.
        for (var guard = 0; guard < 200 && d.Running; guard++)
        {
            var step = Assert.IsType<OpeningStep>(d.Step);
            if (walked.LastOrDefault() != step.Stage) walked.Add(step.Stage);

            // An acknowledged beat is the ONE advance the player makes; everything else is the game.
            if (step.Gate == StageGate.Acknowledged) d.Acknowledge();
            else facts = Answer(step, facts);
            d.Update(facts);
        }

        Assert.Equal(OpeningScript.Steps.Select(s => s.Stage), walked);
        Assert.Equal(OpeningStage.Complete, d.Stage);
        Assert.False(d.Running);
    }

    [Fact]
    public void test_the_fight_is_held_on_every_beat_but_a_live_one()
    {
        foreach (var step in OpeningScript.Steps)
        {
            var d = At(step.Stage);
            var live = step.Mode == TutorialStepMode.LiveExplain;
            Assert.Equal(!live, d.HoldsFight);
            // The input authority is the same line: a live beat takes nothing, everything else takes
            // everything. They are separate properties because they answer different questions and
            // are read by different systems, and a future beat may well hold one without the other.
            Assert.Equal(!live, d.OwnsInput);
        }

        // ...and once it is over, nothing is held at all.
        var done = At(OpeningStage.Complete);
        Assert.False(done.HoldsFight);
        Assert.False(done.OwnsInput);
        Assert.False(done.HoldsNextWave);
        Assert.Null(done.HoldsReplayBefore);
        Assert.Null(done.GrantsScreen);
        Assert.Null(done.ForcedNav);
        Assert.Null(done.ForcedTarget);
    }

    [Fact]
    public void test_nothing_hits_the_hunter_between_the_arrival_and_the_enemys_introduction()
    {
        // THE WHOLE POINT OF THE BARRIER. The player is being shown a stage, not a fight; a blow
        // landing during "YOUR HUNTER" is the explanation arriving late.
        foreach (var stage in new[] { OpeningStage.Arrival, OpeningStage.IntroduceHunter,
                                      OpeningStage.AwaitFirstEnemy, OpeningStage.IntroduceEnemy })
            Assert.Equal(BattleEventKind.EnemyStrike, At(stage).HoldsReplayBefore);

        // ...and the replay parks short of the first cast for the signature beat — on BOTH of its
        // stages, the wait and the card. The watch after it holds nothing: that is the release.
        foreach (var stage in new[] { OpeningStage.AwaitSignature, OpeningStage.IntroduceSignature })
            Assert.Equal(BattleEventKind.Skill, At(stage).HoldsReplayBefore);
        Assert.Null(At(OpeningStage.WatchSignature).HoldsReplayBefore);

        // Every other beat lets the replay run. A barrier left on would be a fight that never lands
        // a blow, which is a far worse failure than a beat that arrives a moment late.
        foreach (var step in OpeningScript.Steps)
        {
            var held = step.Stage is OpeningStage.Arrival or OpeningStage.IntroduceHunter
                       or OpeningStage.AwaitFirstEnemy or OpeningStage.IntroduceEnemy
                       or OpeningStage.AwaitSignature or OpeningStage.IntroduceSignature;
            Assert.Equal(held, At(step.Stage).HoldsReplayBefore is not null);
        }
    }

    [Fact]
    public void test_the_signature_card_keeps_holding_the_same_cast_until_continue_releases_it()
    {
        // THE BUG, AS A SEQUENCE. The replay reaches the cast and parks; the card comes up; the card
        // is read for as long as the player likes; and only CONTINUE lets the cast go.
        var d = At(OpeningStage.AwaitSignature);
        Assert.Equal(BattleEventKind.Skill, d.HoldsReplayBefore);

        d.Update(new OpeningFacts(SignatureHeld: true));
        Assert.Equal(OpeningStage.IntroduceSignature, d.Stage);

        // While the card is up — any number of frames — the fight is frozen AND the barrier is still
        // on. A barrier that dropped here would let the cast cross underneath its own card.
        for (var frame = 0; frame < 600; frame++)
        {
            d.Update(new OpeningFacts(SignatureHeld: true, SignatureLanded: frame % 2 == 0));
            Assert.Equal(OpeningStage.IntroduceSignature, d.Stage);
            Assert.True(d.HoldsFight);
            Assert.Equal(BattleEventKind.Skill, d.HoldsReplayBefore);
            Assert.True(d.WantsAcknowledgement);
        }

        // CONTINUE: the card goes, the hold goes, and the fight runs so the same event can cross.
        d.Acknowledge();
        d.Update(new OpeningFacts(SignatureHeld: true));
        Assert.Equal(OpeningStage.WatchSignature, d.Stage);
        Assert.Null(d.HoldsReplayBefore);
        Assert.False(d.HoldsFight);
        Assert.False(d.OwnsInput);

        // Nothing is drawn over the cast until it has PLAYED — the next card waits for the fact.
        d.Update(new OpeningFacts(SignatureHeld: false, SignatureLanded: false));
        Assert.Equal(OpeningStage.WatchSignature, d.Stage);
        d.Update(new OpeningFacts(SignatureLanded: true));
        Assert.Equal(OpeningStage.IntroduceHealth, d.Stage);
    }

    [Fact]
    public void test_a_credited_reward_alone_does_not_raise_the_gleam_card()
    {
        // PAID IS NOT SEEN. The purse moves on the kill's own frame, with the last creature one frame
        // into its fall; the card must wait for the clear to have been watched to its end.
        var d = At(OpeningStage.AwaitFirstReward);
        for (var frame = 0; frame < 300; frame++)
        {
            d.Update(new OpeningFacts(RewardsCredited: 1, ClearShown: false));
            Assert.Equal(OpeningStage.AwaitFirstReward, d.Stage);
        }

        // ...and the presentation alone, with nothing paid, is not it either.
        d.Update(new OpeningFacts(RewardsCredited: 0, ClearShown: true));
        Assert.Equal(OpeningStage.AwaitFirstReward, d.Stage);

        // Paid AND shown: now.
        d.Update(new OpeningFacts(RewardsCredited: 1, ClearShown: true));
        Assert.Equal(OpeningStage.IntroduceResources, d.Stage);
    }

    [Fact]
    public void test_the_chest_card_waits_for_the_bosss_fall_but_a_reload_does_not()
    {
        // THE SAME BUG, AT THE BOSS. The tally moves on the final blow; "A CHEST DROPPED" over a boss
        // still falling is GLEAM over a creature still falling, one boss later.
        var d = At(OpeningStage.AwaitBossFelled);
        Assert.True(d.HoldsNextWave);   // the next pack stays off the stage while the boss falls
        d.Update(new OpeningFacts(BossesFelled: 1, ClearShown: false));
        Assert.Equal(OpeningStage.AwaitBossFelled, d.Stage);
        d.Update(new OpeningFacts(BossesFelled: 1, ClearShown: true));
        Assert.Equal(OpeningStage.IntroduceChest, d.Stage);

        // ...but a RELOAD walks over the deed alone: a fall shown is a moment, and after a load the
        // moment has not happened at all. Waiting for it would stall a career whose boss is long dead.
        var reloaded = At(OpeningStage.AwaitBossFelled);
        reloaded.FastForwardOverDoneDeeds(new OpeningFacts(BossesFelled: 1, ClearShown: false));
        Assert.Equal(OpeningStage.IntroduceChest, reloaded.Stage);
    }

    [Fact]
    public void test_a_hunter_who_loses_to_the_boss_is_not_held_on_the_next_wave_one()
    {
        // THE DEADLOCK THE AUTOPLAYED OPENING FOUND. The fresh Hunter can lose to the tutorial boss and
        // start again at wave one. The boss beat holds the BOSS'S clear; holding wave one's would rest the
        // break forever, waiting for a boss that can only come after the next wave — which it holds back.
        var d = At(OpeningStage.AwaitBossFelled);
        Assert.True(d.HoldsNextWaveAfter(bossWave: true));
        Assert.False(d.HoldsNextWaveAfter(bossWave: false));

        // Every other holding beat is about whichever clear comes first, boss or not.
        foreach (var stage in new[] { OpeningStage.AwaitFirstReward, OpeningStage.IntroduceResources, OpeningStage.WatchSignature })
        {
            Assert.True(At(stage).HoldsNextWaveAfter(bossWave: false));
            Assert.True(At(stage).HoldsNextWaveAfter(bossWave: true));
        }
        // ...and a beat that holds nothing holds nothing either way.
        Assert.False(At(OpeningStage.IntroduceHealth).HoldsNextWaveAfter(bossWave: true));
        Assert.False(At(OpeningStage.Complete).HoldsNextWaveAfter(bossWave: true));
    }

    [Fact]
    public void test_the_next_wave_is_held_back_until_the_gleam_card_is_answered()
    {
        // From the first wave being watched until the GLEAM card is answered, the next pack stays off
        // the stage — and again while the released Signature cast plays, because the first cast of a
        // fresh career is its wave's killing blow and its effect is a fall. On no other beat does the
        // opening touch the wave transition at all.
        foreach (var step in OpeningScript.Steps)
        {
            var expected = step.Stage is OpeningStage.AwaitFirstReward or OpeningStage.IntroduceResources
                                      or OpeningStage.WatchSignature or OpeningStage.AwaitBossFelled;
            Assert.Equal(expected, At(step.Stage).HoldsNextWave);
        }

        var d = At(OpeningStage.IntroduceResources);
        d.Update(new OpeningFacts(RewardsCredited: 1, ClearShown: true));
        Assert.True(d.HoldsNextWave);        // the card is up: still held
        Assert.True(d.HoldsFight);
        d.Acknowledge();
        d.Update(new OpeningFacts(RewardsCredited: 1, ClearShown: true));
        Assert.Equal(OpeningStage.AwaitSignature, d.Stage);
        Assert.False(d.HoldsNextWave);       // answered: the next wave may come
    }

    [Fact]
    public void test_the_item_step_waits_for_the_players_own_pick_and_nothing_else()
    {
        // The fact is read off the GEAR screen's selection as the player made it. Being on the screen,
        // an item existing, and a chest having been opened are all true already, and none of them is it.
        var d = At(OpeningStage.ForceItemSelect);
        d.Update(new OpeningFacts(Screen: Activity.Gear, ChestsOpened: 1, ItemsWorn: 0, ItemSelected: false));
        Assert.Equal(OpeningStage.ForceItemSelect, d.Stage);
        Assert.Equal(TourTarget.InventoryItem, d.ForcedTarget);

        d.Update(new OpeningFacts(Screen: Activity.Gear, ChestsOpened: 1, ItemSelected: true));
        Assert.Equal(OpeningStage.ExplainItem, d.Stage);
        Assert.Null(d.ForcedTarget);                     // a paused card takes CONTINUE and nothing else
        Assert.Equal(TourTarget.ItemDetail, d.Target);   // ...and it lights the inspector the pick filled

        d.Acknowledge();
        d.Update(new OpeningFacts(Screen: Activity.Gear, ItemSelected: true));
        Assert.Equal(OpeningStage.ForceEquip, d.Stage);
        Assert.Equal(TourTarget.EquipButton, d.ForcedTarget);

        d.Update(new OpeningFacts(Screen: Activity.Gear, ItemSelected: true, ItemsWorn: 1));
        Assert.Equal(OpeningStage.ShowEquipped, d.Stage);
    }

    [Fact]
    public void test_a_screen_is_opened_no_earlier_than_the_beat_that_names_it()
    {
        // The VAULT opens on the beat that says a chest was left, and stays open through the item's
        // introduction; the GEAR screen opens on the beat that sends the player to it. Neither is
        // granted before its own sentence has been said, and neither is granted after the opening.
        foreach (var step in OpeningScript.Steps)
        {
            var expected = step.Stage switch
            {
                OpeningStage.IntroduceChest or OpeningStage.ForceVault
                    or OpeningStage.ForceChestOpen or OpeningStage.IntroduceItem => (Activity?)Activity.Vault,
                OpeningStage.ForceGear or OpeningStage.ForceItemSelect or OpeningStage.ExplainItem
                    or OpeningStage.ForceEquip or OpeningStage.ShowEquipped => Activity.Gear,
                _ => null,
            };
            Assert.Equal(expected, At(step.Stage).GrantsScreen);
        }

        // AND THE GRANT IS NEVER THE ONLY REASON THE SCREEN IS OPEN. Both screens' real gates become
        // true within a beat of their grant — a chest that really exists, an item that really exists —
        // so the grant buys presentation timing and never permanent access.
        Assert.True(Unlocks.IsOpen(Activity.Vault, new UnlockFacts(ChestsEverHeld: 1)));
        Assert.True(Unlocks.IsOpen(Activity.Gear, new UnlockFacts(ItemsOwned: 1)));
    }

    [Fact]
    public void test_a_forced_beat_names_exactly_one_tile_or_exactly_one_control()
    {
        foreach (var step in OpeningScript.Steps)
        {
            var d = At(step.Stage);
            if (step.Mode == TutorialStepMode.ForceNavigate)
            {
                Assert.Equal(step.Screen, d.ForcedNav);
                Assert.Null(d.ForcedTarget);   // the rail is the control; there is no second one
            }
            else if (step.Mode == TutorialStepMode.ForceAction)
            {
                Assert.Equal(step.Target, d.ForcedTarget);
                Assert.Null(d.ForcedNav);
            }
            else
            {
                Assert.Null(d.ForcedNav);
                Assert.Null(d.ForcedTarget);
            }
        }
    }

    [Fact]
    public void test_a_narrated_live_beat_is_not_spent_while_the_player_is_elsewhere()
    {
        // IntroduceHealth is a LiveExplain about the Hunter's card, on the HUNT screen, and its gate
        // is a cleared wave — which the fight clears whether or not anyone is watching. Without this
        // rule the beat is silently burned by a player who walked into the FORGE for ten seconds.
        var d = At(OpeningStage.IntroduceHealth);
        var away = new OpeningFacts(Screen: Activity.Forge, RewardsCredited: 9);
        d.Update(away);
        Assert.Equal(OpeningStage.IntroduceHealth, d.Stage);

        d.Update(away with { Screen = Activity.Hunt });
        Assert.Equal(OpeningStage.IntroduceStage, d.Stage);
    }

    [Fact]
    public void test_a_wait_beat_with_no_words_is_spent_wherever_the_player_is()
    {
        // The rule above is about a beat that SAYS something. A silent wait is plumbing — it is
        // holding the cursor until the game produces a fact — and holding it hostage to a screen
        // would stall the opening for a player who walked off during it.
        var d = At(OpeningStage.AwaitFirstReward);
        d.Update(new OpeningFacts(Screen: Activity.Warren, RewardsCredited: 1, ClearShown: true));
        Assert.Equal(OpeningStage.IntroduceResources, d.Stage);
    }

    [Fact]
    public void test_a_reload_never_asks_for_a_deed_the_save_can_prove_was_done()
    {
        // Quit after wearing the item, before the autosave caught the last two beats.
        var d = At(OpeningStage.ForceChestOpen);
        d.FastForwardOverDoneDeeds(new OpeningFacts(
            Screen: Activity.Gear, BossesFelled: 1, ChestsOpened: 1, ItemSelected: true, ItemsWorn: 1));
        // It walks over the chest and the item and stops on the first beat whose gate is NOT a lasting
        // fact — the one that only asks to be read.
        Assert.Equal(OpeningStage.IntroduceItem, d.Stage);
    }

    [Fact]
    public void test_a_settled_animation_is_never_fast_forwarded_over()
    {
        // ArrivalSettled and EnemySettled are TRUE in the frame they are asked and false again after a
        // reload. Walking over them on load would skip the whole opening on the strength of a boolean
        // that has already expired — the exact bug class the two save-version constants exist for.
        var d = At(OpeningStage.Arrival);
        d.FastForwardOverDoneDeeds(new OpeningFacts(ArrivalSettled: true, EnemySettled: true));
        Assert.Equal(OpeningStage.Arrival, d.Stage);

        // ...and neither are the two presentation facts this pass added: a cast that landed and a
        // clear that was shown are things that happened on a screen, not things a save remembers.
        var watch = At(OpeningStage.WatchSignature);
        watch.FastForwardOverDoneDeeds(new OpeningFacts(SignatureLanded: true, ClearShown: true, RewardsCredited: 3));
        Assert.Equal(OpeningStage.WatchSignature, watch.Stage);
    }

    [Fact]
    public void test_the_player_is_held_while_the_moment_the_next_card_is_about_plays_out()
    {
        // A LIVE BEAT GIVES THE PLAYER THE GAME — except while the moment its gate waits on is playing.
        // A click on the rail during the first clear's fall walked the player to the WARREN, and GLEAM
        // then came up on a screen with no fight behind it (adversarial review, 2026-09-11).
        var reward = At(OpeningStage.AwaitFirstReward);
        Assert.False(reward.OwnsInput);
        reward.Update(new OpeningFacts(RewardsCredited: 0));
        Assert.False(reward.OwnsInput);                        // nothing yet: the fight is theirs to watch
        reward.Update(new OpeningFacts(RewardsCredited: 1, ClearShown: false));
        Assert.Equal(OpeningStage.AwaitFirstReward, reward.Stage);
        Assert.True(reward.OwnsInput);                         // paid and still falling: hands off
        Assert.False(reward.HoldsFight);                       // ...but the fall itself is never frozen
        reward.Update(new OpeningFacts(RewardsCredited: 1, ClearShown: true));
        Assert.Equal(OpeningStage.IntroduceResources, reward.Stage);

        // The released cast is the same: from CONTINUE until it has played, it is the only thing on.
        var watch = At(OpeningStage.WatchSignature);
        watch.Update(new OpeningFacts(SignatureLanded: false));
        Assert.True(watch.OwnsInput);
        Assert.False(watch.HoldsFight);
        watch.Update(new OpeningFacts(SignatureLanded: true));
        Assert.Equal(OpeningStage.IntroduceHealth, watch.Stage);

        // ...and the boss's fall, once the boss is felled and not before.
        var boss = At(OpeningStage.AwaitBossFelled);
        boss.Update(new OpeningFacts(BossesFelled: 0));
        Assert.False(boss.OwnsInput);
        boss.Update(new OpeningFacts(BossesFelled: 1, ClearShown: false));
        Assert.True(boss.OwnsInput);
        boss.Update(new OpeningFacts(BossesFelled: 1, ClearShown: true));
        Assert.Equal(OpeningStage.IntroduceChest, boss.Stage);

        // The hold is the moment's, never the stage's: a live beat whose gate is not a moment holds nothing.
        var health = At(OpeningStage.IntroduceHealth);
        health.Update(new OpeningFacts(RewardsCredited: 0));
        Assert.False(health.OwnsInput);
    }

    [Fact]
    public void test_skipping_the_tutorial_grants_nothing()
    {
        // SKIP TUTORIAL is a presentation switch. It ends the beats and releases the holds; it cannot
        // reach an item, a resource or an unlock, because the director has no way to touch any of them
        // — the whole class is a cursor and a handful of read-only properties.
        var d = At(OpeningStage.IntroduceBoss);
        d.SkipToEnd();

        Assert.Equal(OpeningStage.Complete, d.Stage);
        Assert.False(d.Running);
        Assert.False(d.HoldsFight);
        Assert.False(d.HoldsNextWave);
        Assert.False(d.OwnsInput);
        Assert.Null(d.GrantsScreen);
        // ...and a player who skips before the first boss still has no welcome gift, because that gift
        // comes from the boss. Nothing here can hand it over.
        Assert.False(Unlocks.IsOpen(Activity.Vault, new UnlockFacts()));
    }

    [Fact]
    public void test_a_new_career_starts_over_from_the_prologue()
    {
        var d = At(OpeningStage.ShowEquipped);
        d.Reset();
        Assert.Equal(OpeningStage.NotStarted, d.Stage);
        Assert.False(d.Running);

        d.Begin();
        Assert.Equal(OpeningScript.First, d.Stage);
        Assert.Equal(OpeningStage.Prologue, d.Stage);
    }

    [Fact]
    public void test_an_unknown_cursor_resolves_forward_rather_than_into_a_forced_step()
    {
        // A file written by a later build carries a stage this one does not know. Resolving it
        // BACKWARDS would drop the player into "OPEN THE VAULT" with the chest long since opened and
        // no way out; resolving it forward costs them nothing they have not already had.
        var d = new OpeningDirector();
        d.Restore(OpeningScript.StageOf(9999));
        Assert.Equal(OpeningStage.Complete, d.Stage);
        Assert.False(d.Running);
    }

    [Fact]
    public void test_an_acknowledgement_belongs_to_the_beat_that_asked_for_it()
    {
        // CONTINUE pressed on one beat must not also spend the next one. The director clears the
        // latch as it steps, so a held key cannot walk the whole opening in four frames.
        var d = At(OpeningStage.IntroduceHunter);
        d.Acknowledge();
        d.Update(new OpeningFacts());
        Assert.Equal(OpeningStage.AwaitFirstEnemy, d.Stage);

        d.Update(new OpeningFacts());
        Assert.Equal(OpeningStage.AwaitFirstEnemy, d.Stage);
    }

    [Fact]
    public void test_only_a_paused_beat_asks_for_continue()
    {
        foreach (var step in OpeningScript.Steps)
            Assert.Equal(step.Mode == TutorialStepMode.PauseExplain, At(step.Stage).WantsAcknowledgement);
    }
}
