using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE FIRST DECISION: EXPLAIN, THEN DO — and the DO is a real purchase or it is nothing.
/// </summary>
/// <remarks>
/// <para>
/// Training used to be left out of the authored opening on the reasoning that it could teach itself
/// afterwards. The result was an opening that reached its first boss without ever asking the player
/// to choose anything, in a game whose whole premise is that you choose how your hunter grows — and
/// whose next two chapters hand over a chest and an item, which are things the game GIVES.
/// </para>
/// <para>
/// What this file guards is the shape of the fix, not the fact of it: the chapter forces the DEED and
/// never the CHOICE, it waits until the deed can actually be performed, and it cannot be satisfied by
/// anything except the Hunter's own ranks going up. There is no tutorial-only button anywhere in it.
/// </para>
/// </remarks>
public class OpeningTrainingChapterTest
{
    /// <summary>Answer whatever the step is waiting for — the same walk the director test uses.</summary>
    private static OpeningFacts Answer(OpeningStep step, OpeningFacts f) => step.Gate switch
    {
        StageGate.ArrivalSettled => f with { ArrivalSettled = true },
        StageGate.EnemySettled => f with { EnemySettled = true },
        StageGate.FirstRewardShown => f with { RewardsCredited = f.RewardsCredited + 1, ClearShown = true },
        StageGate.SignatureImminent => f with { SignatureHeld = true },
        StageGate.SignatureLanded => f with { SignatureLanded = true },
        StageGate.OneWaveCleared => f with { RewardsCredited = f.RewardsCredited + 1 },
        StageGate.TrainingAffordable => f with { TrainingOpen = true, CanAffordFirstRank = true },
        StageGate.StatTrained => f with { StatsTrained = f.StatsTrained + 1 },
        StageGate.BossSettled => f with { BossSettled = true },
        StageGate.BossFelled => f with { BossesFelled = 1, ClearShown = true },
        StageGate.OnScreen => f with { Screen = step.Screen ?? f.Screen },
        StageGate.ChestOpened => f with { ChestsOpened = 1 },
        StageGate.ItemSelected => f with { ItemSelected = true },
        StageGate.ItemWorn => f with { ItemsWorn = 1 },
        _ => f,
    };

    /// <summary>Walk a fresh career up to <paramref name="until"/>, answering every gate honestly.</summary>
    private static (OpeningDirector Director, OpeningFacts Facts) WalkTo(OpeningStage until)
    {
        var d = new OpeningDirector();
        d.Begin();
        var f = new OpeningFacts();
        for (var guard = 0; guard < 200 && d.Running && d.Stage != until; guard++)
        {
            var step = Assert.IsType<OpeningStep>(d.Step);
            if (step.Gate == StageGate.Acknowledged) d.Acknowledge();
            else f = Answer(step, f);
            d.Update(f);
        }
        Assert.Equal(until, d.Stage);
        return (d, f);
    }

    [Fact]
    public void test_the_forced_train_step_cannot_complete_without_a_real_trained_rank()
    {
        var (d, facts) = WalkTo(OpeningStage.ForceTrainStat);

        // NOTHING BUT THE DEED MOVES IT. Not an acknowledgement — a card closed is not a rank bought —
        // not standing on the screen, and not the fight running on underneath.
        d.Acknowledge();
        d.Update(facts);
        Assert.Equal(OpeningStage.ForceTrainStat, d.Stage);

        d.Update(facts with { Screen = Activity.Training });
        Assert.Equal(OpeningStage.ForceTrainStat, d.Stage);

        d.Update(facts with { RewardsCredited = facts.RewardsCredited + 5, ClearShown = true });
        Assert.Equal(OpeningStage.ForceTrainStat, d.Stage);

        // ...AND EVEN BEING ABLE TO AFFORD ONE IS NOT BUYING ONE. This is the whole distinction the
        // brief draws between a tutorial that asks and a tutorial that pretends: the step is answered
        // by the Hunter's ranks, which only the real TRAIN path can move.
        d.Update(facts with { TrainingOpen = true, CanAffordFirstRank = true, StatsTrained = 0 });
        Assert.Equal(OpeningStage.ForceTrainStat, d.Stage);

        // THE RANK, AND THE STEP GOES.
        d.Update(facts with { StatsTrained = 1 });
        Assert.Equal(OpeningStage.ShowTrained, d.Stage);
    }

    [Fact]
    public void test_the_chapter_waits_until_the_rank_can_actually_be_bought()
    {
        var (d, facts) = WalkTo(OpeningStage.AwaitTraining);

        // A CARD THAT CANNOT BE OBEYED IS NOISE. Neither half of the gate is sufficient alone: an open
        // screen with an empty purse refuses every row, and a full purse with the screen still chained
        // sends the player at a locked tile.
        d.Update(facts with { TrainingOpen = true, CanAffordFirstRank = false });
        Assert.Equal(OpeningStage.AwaitTraining, d.Stage);

        d.Update(facts with { TrainingOpen = false, CanAffordFirstRank = true });
        Assert.Equal(OpeningStage.AwaitTraining, d.Stage);

        d.Update(facts with { TrainingOpen = true, CanAffordFirstRank = true });
        Assert.Equal(OpeningStage.IntroduceTraining, d.Stage);
    }

    [Fact]
    public void test_the_chapter_is_explain_then_do_and_never_both_at_once()
    {
        // THE GRAMMAR, BEAT BY BEAT. The explanation is read on the HUNT and is GONE before the player
        // is walked anywhere; the navigation asks for one tile; the deed asks for one region and is
        // gated on the deed itself; the last card reports what changed.
        var chapter = new[]
        {
            OpeningStage.AwaitTraining, OpeningStage.IntroduceTraining, OpeningStage.ForceTraining,
            OpeningStage.ForceTrainStat, OpeningStage.ShowTrained,
        };
        var steps = chapter.Select(s => OpeningScript.Find(s)!.Value).ToArray();

        Assert.Equal(TutorialStepMode.LiveExplain, steps[0].Mode);     // the fight earns the Gleam
        Assert.Equal(TutorialStepMode.PauseExplain, steps[1].Mode);    // EXPLAIN
        Assert.Equal(TutorialStepMode.ForceNavigate, steps[2].Mode);   // DO: go there
        Assert.Equal(TutorialStepMode.ForceAction, steps[3].Mode);     // DO: the deed
        Assert.Equal(TutorialStepMode.PauseExplain, steps[4].Mode);    // EXPLAIN what happened

        // NO BEAT BOTH EXPLAINS AND DEMANDS. A forced beat is never gated on an acknowledgement, and an
        // explanatory one is never gated on a deed: that separation is what lets the card say one thing.
        foreach (var step in steps)
        {
            if (step.Mode is TutorialStepMode.ForceAction or TutorialStepMode.ForceNavigate)
                Assert.NotEqual(StageGate.Acknowledged, step.Gate);
            if (step.Mode == TutorialStepMode.PauseExplain)
                Assert.Equal(StageGate.Acknowledged, step.Gate);
        }
    }

    [Fact]
    public void test_the_fight_is_frozen_for_the_whole_chapter_so_the_boss_cannot_walk_on_during_it()
    {
        // THE TUTORIAL BOSS STANDS ON WAVE 5 AND TRAINING OPENS ON WAVE 3, so the chapter runs inside a
        // two-wave window. It does not have to RACE that window: every beat after the live wait holds
        // the fight outright — no wave starts, no reward is credited — so the boss cannot arrive while
        // the player is reading about stats or standing on the TRAINING screen.
        foreach (var stage in new[] { OpeningStage.IntroduceTraining, OpeningStage.ForceTraining,
                                      OpeningStage.ForceTrainStat, OpeningStage.ShowTrained })
        {
            var d = new OpeningDirector();
            d.Restore(stage);
            Assert.True(d.HoldsFight, $"{stage} must freeze the fight");
        }

        // ...and the one beat that does NOT hold it is the one whose whole job is to let the fight pay.
        var wait = new OpeningDirector();
        wait.Restore(OpeningStage.AwaitTraining);
        Assert.False(wait.HoldsFight);
    }

    [Fact]
    public void test_the_last_card_of_the_chapter_returns_the_player_to_the_hunt()
    {
        // The beat AFTER the chapter waits for the tutorial boss, which happens on the HUNT. A player
        // left standing in TRAINING would be reading a screen while the thing the next card is about
        // arrived behind it — the same reason the gear chapter navigates home on its last card.
        Assert.Equal(OpeningStage.AwaitBoss, OpeningScript.After(OpeningStage.ShowTrained));
        Assert.Equal(Activity.Training, OpeningScript.Find(OpeningStage.ShowTrained)!.Value.Screen);
        Assert.Equal(Activity.Hunt, OpeningScript.Find(OpeningStage.AwaitBoss)!.Value.Screen);

        var host = System.IO.File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        Assert.Contains("if (was == OpeningStage.ShowTrained && _opening.Stage == OpeningStage.AwaitBoss && !CaptureRig) OpenNav(0);",
                        host, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_training_screen_can_actually_receive_the_forced_click()
    {
        // THE GAP THIS CLOSES. GEAR and the VAULT take `MouseClicked || ForcedScreenClick()`; TRAINING
        // took MouseClicked alone, because nothing had ever forced a deed on it. With the chapter added
        // and that term missing, the one control the beat lights is the one control it cannot press —
        // a soft lock behind a card that says TRAIN ANY STAT.
        var game = System.IO.File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");
        Assert.Contains("_training.Update(ScreenKeys, _prevKeys, PageCursor, MouseClicked || ForcedScreenClick(),",
                        game, StringComparison.Ordinal);

        // ...and the deed's target is the ROWS, which is a panel rather than one control — deliberately,
        // because the deed is "make a choice". What makes that safe is that the panel holds only the
        // stat rows and their TRAIN buttons: RESET ALL TRAINING is a separate target, below it.
        var deed = OpeningScript.Find(OpeningStage.ForceTrainStat)!.Value;
        Assert.Equal(TourTarget.TrainingRows, deed.Target);
        Assert.NotEqual(TourTarget.ResetBar, deed.Target);
        var screen = System.IO.File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "TrainingScreen.cs")).Replace("\r\n", "\n");
        Assert.Contains("TourTarget.TrainingRows => new[] { ListPanel },", screen, StringComparison.Ordinal);
        Assert.Contains("new(38, Top, InspectorPanel.X - ColumnGap - 38, ResetBar.Y - FooterGap - Top);",
                        screen, StringComparison.Ordinal);
    }

    [Fact]
    public void test_a_save_written_before_the_chapter_resumes_on_the_beat_it_actually_reached()
    {
        // THE MIGRATION, WORKED. OpeningStage persists by ORDINAL, so five stages inserted before the
        // tutorial boss move every cursor after them. A v8 file read as written would put a player who
        // had reached the boss five beats earlier — in the middle of a chapter they never played.
        const int v8 = 8;

        // Untouched: everything before the insertion point.
        Assert.Equal(OpeningStage.IntroduceStage, OpeningScript.StageOf(13, v8));
        Assert.Equal(OpeningStage.IntroduceHealth, OpeningScript.StageOf(12, v8));

        // Lifted by the chapter: a v8 AwaitBoss was 14, and AwaitBoss is 19 now.
        Assert.Equal(OpeningStage.AwaitBoss, OpeningScript.StageOf(14, v8));
        Assert.Equal(OpeningStage.ShowEquipped, OpeningScript.StageOf(25, v8));
        Assert.Equal(OpeningStage.Complete, OpeningScript.StageOf(26, v8));

        // AND THE TWO SHIFTS COMPOSE, oldest first. A v6 file predates WatchSignature too, so its
        // AwaitBoss (13) is lifted once into v7/v8 numbering and again into this one.
        const int v6 = 6;
        Assert.Equal(OpeningStage.AwaitBoss, OpeningScript.StageOf(13, v6));
        Assert.Equal(OpeningStage.IntroduceHealth, OpeningScript.StageOf(11, v6));   // below both boundaries after the first lift
        Assert.Equal(OpeningStage.IntroduceStage, OpeningScript.StageOf(12, v6));

        // A CURRENT FILE IS READ EXACTLY AS WRITTEN — no lift may apply at the version that introduced it.
        foreach (var stage in Enum.GetValues<OpeningStage>())
            Assert.Equal(stage, OpeningScript.StageOf((int)stage, OpeningScript.FirstVersionWithTrainingChapter));
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !System.IO.File.Exists(System.IO.Path.Combine(dir, "IdleXIdle.sln")))
            dir = System.IO.Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return System.IO.Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }
}
