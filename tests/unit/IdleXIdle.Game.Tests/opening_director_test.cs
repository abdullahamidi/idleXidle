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
        StageGate.FirstRewardCredited => f with { RewardsCredited = f.RewardsCredited + 1 },
        StageGate.SignatureImminent => f with { SignatureHeld = true },
        StageGate.OneWaveCleared => f with { WavesCleared = f.WavesCleared + 1 },
        StageGate.BossSettled => f with { BossSettled = true },
        StageGate.BossFelled => f with { BossesFelled = 1 },
        StageGate.OnScreen => f with { Screen = step.Screen ?? f.Screen },
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
        Assert.Null(done.HoldsReplayBefore);
        Assert.Null(done.GrantsScreen);
        Assert.Null(done.ForcedNav);
        Assert.Null(done.ForcedTarget);
    }

    [Fact]
    public void test_nothing_hits_the_hunter_between_the_arrival_and_the_enemys_introduction()
    {
        // THE WHOLE POINT OF THE BARRIER. The player is being shown a stage, not a fight; a blow
        // landing during "THIS IS YOUR HUNTER" is the explanation arriving late.
        foreach (var stage in new[] { OpeningStage.Arrival, OpeningStage.IntroduceHunter,
                                      OpeningStage.AwaitFirstEnemy, OpeningStage.IntroduceEnemy })
            Assert.Equal(BattleEventKind.EnemyStrike, At(stage).HoldsReplayBefore);

        // ...and the replay parks one millisecond short of the first cast for the signature beat.
        foreach (var stage in new[] { OpeningStage.AwaitSignature, OpeningStage.IntroduceSignature })
            Assert.Equal(BattleEventKind.Skill, At(stage).HoldsReplayBefore);

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
        var away = new OpeningFacts(Screen: Activity.Forge, WavesCleared: 9);
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
        d.Update(new OpeningFacts(Screen: Activity.Warren, RewardsCredited: 1));
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
    }

    [Fact]
    public void test_skipping_the_tutorial_grants_nothing()
    {
        // SKIP TUTORIAL is a presentation switch. It ends the beats and releases the holds; it cannot
        // reach an item, a resource or an unlock, because the director has no way to touch any of them
        // — the whole class is a cursor and five read-only properties.
        var d = At(OpeningStage.IntroduceBoss);
        d.SkipToEnd();

        Assert.Equal(OpeningStage.Complete, d.Stage);
        Assert.False(d.Running);
        Assert.False(d.HoldsFight);
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
