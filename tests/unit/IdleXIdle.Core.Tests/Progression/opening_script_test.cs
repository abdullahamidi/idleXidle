using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Progression;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Progression;

/// <summary>
/// THE OPENING IS AN ORDER, AND THE ORDER IS THE TEACHING.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue that came before this one chose, every frame, whichever eligible lesson ranked
/// highest. That is the right shape for the long tail of a game whose systems open in an order nobody
/// authored, and the wrong shape for its first five minutes: facts become true in an order the
/// player's understanding does not follow, and the opening came out as a shuffle — "this is your
/// Signature Skill", three casts after they had watched it.
/// </para>
/// <para>
/// So the opening is written down. These tests pin the sequence itself, the copy rule that keeps each
/// beat to one idea, and the migration law the whole family of onboarding facts shares: the file's
/// VERSION decides who is a veteran, never a guess made from progression.
/// </para>
/// </remarks>
public class OpeningScriptTest
{
    private readonly ITestOutputHelper _out;

    public OpeningScriptTest(ITestOutputHelper output) => _out = output;

    [Fact]
    public void test_the_script_walks_its_stages_in_the_enum_order_with_no_gaps()
    {
        // The cursor is persisted as the stage's ORDINAL, so a stage inserted mid-enum moves every
        // stored cursor to a different beat. Keeping the table in enum order is what makes that
        // visible: a step added out of order fails here rather than mis-resuming somebody's save.
        var stages = OpeningScript.Steps.Select(s => s.Stage).ToList();
        Assert.Equal(stages.OrderBy(s => (int)s), stages);
        Assert.Equal(stages.Distinct().Count(), stages.Count);

        // ...and every step is reachable by walking After() from the first.
        var walked = new List<OpeningStage>();
        for (var at = OpeningScript.First; OpeningScript.Running(at); at = OpeningScript.After(at))
        {
            walked.Add(at);
            Assert.True(walked.Count <= stages.Count + 1, "After() does not terminate");
        }
        Assert.Equal(stages, walked);
        Assert.Equal(OpeningStage.Complete, OpeningScript.After(stages[^1]));
    }

    [Fact]
    public void test_the_authored_order_is_the_one_the_playtest_asked_for()
    {
        // The sequence, named: story, arrival, hunter, enemy, resources, the signature BEFORE it
        // fires, the live readouts, the boss, its chest, the vault, the gear. Written out rather than
        // derived, because this list IS the product decision — if it changes, it should change here.
        var order = OpeningScript.Steps.Select(s => s.Stage).ToArray();
        Assert.Equal(new[]
        {
            OpeningStage.Prologue, OpeningStage.AwaitBegin, OpeningStage.Arrival,
            OpeningStage.IntroduceHunter,
            OpeningStage.AwaitFirstEnemy, OpeningStage.IntroduceEnemy,
            OpeningStage.AwaitFirstReward, OpeningStage.IntroduceResources,
            OpeningStage.AwaitSignature, OpeningStage.IntroduceSignature,
            OpeningStage.IntroduceHealth, OpeningStage.IntroduceStage,
            OpeningStage.AwaitBoss, OpeningStage.IntroduceBoss, OpeningStage.AwaitBossFelled,
            OpeningStage.IntroduceChest,
            OpeningStage.ForceVault, OpeningStage.ForceChestOpen, OpeningStage.IntroduceItem,
            OpeningStage.ForceGear, OpeningStage.ForceItemSelect, OpeningStage.ExplainItem,
            OpeningStage.ForceEquip, OpeningStage.ShowEquipped,
        }, order);
    }

    [Fact]
    public void test_the_signature_is_introduced_before_anything_could_have_shown_it()
    {
        // THE MOMENT THE WHOLE REDESIGN IS FOR. The step that holds the replay must come immediately
        // before the step that explains the cast — nothing may sit between them, or the barrier is
        // released for a beat and the player watches the thing they are about to be told about.
        var i = OpeningScript.Steps.ToList().FindIndex(s => s.Stage == OpeningStage.AwaitSignature);
        Assert.True(i >= 0);
        Assert.Equal(StageGate.SignatureImminent, OpeningScript.Steps[i].Gate);
        Assert.Equal(OpeningStage.IntroduceSignature, OpeningScript.Steps[i + 1].Stage);
        Assert.Equal(TutorialStepMode.PauseExplain, OpeningScript.Steps[i + 1].Mode);

        // ...and it is the LAST paused beat before the boss, so the player consciously watches the
        // cast and then gets the fight back.
        Assert.Equal(TutorialStepMode.LiveExplain, OpeningScript.Steps[i + 2].Mode);
    }

    [Fact]
    public void test_the_enemy_is_introduced_after_it_has_arrived_and_before_it_swings()
    {
        var enemy = OpeningScript.Steps.Single(s => s.Stage == OpeningStage.AwaitFirstEnemy);
        Assert.Equal(StageGate.EnemySettled, enemy.Gate);
        Assert.Equal(TutorialStepMode.LiveExplain, enemy.Mode);   // the entrance plays whole

        var intro = OpeningScript.Steps.Single(s => s.Stage == OpeningStage.IntroduceEnemy);
        Assert.Equal(TutorialStepMode.PauseExplain, intro.Mode);  // ...and then the world stops
        Assert.Equal(TourTarget.Enemies, intro.Target);
    }

    [Fact]
    public void test_the_resource_lesson_waits_for_a_reward_the_player_can_see()
    {
        var wait = OpeningScript.Steps.Single(s => s.Stage == OpeningStage.AwaitFirstReward);
        Assert.Equal(StageGate.FirstRewardCredited, wait.Gate);

        // ONLY GLEAM. The capsule row is what is lit, and at this point in a fresh career Gleam is the
        // only thing in it — teaching Scrap or Essence here would name something the player does not
        // have. (Every other material gets its own contextual lesson at its first real drop.)
        var intro = OpeningScript.Steps.Single(s => s.Stage == OpeningStage.IntroduceResources);
        Assert.Equal(TourTarget.CurrencyPills, intro.Target);
        Assert.Contains("GLEAM", intro.Title, StringComparison.OrdinalIgnoreCase);
        foreach (var other in new[] { "SCRAP", "ESSENCE", "CORE", "CRYSTAL", "DUST" })
            Assert.DoesNotContain(other, intro.Title + " " + intro.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_training_is_not_in_the_opening()
    {
        // THE ORDERING THE PREVIOUS PASS ENFORCED IS SUPERSEDED. It made the opening
        // fight -> Training -> chest -> Gear, on the reasoning that the decision loop should precede
        // the loot loop. The authored opening is a different product: the boss gives the chest, the
        // chest gives the item, and the item is what GEAR is for. Training's facts may well come true
        // somewhere around the boss; it still waits, and teaches itself afterwards.
        foreach (var step in OpeningScript.Steps)
        {
            Assert.NotEqual(Activity.Training, step.Screen);
            Assert.NotEqual(TourTarget.TrainingRows, step.Target);
            Assert.DoesNotContain("TRAIN", step.Title, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void test_a_forced_step_names_exactly_what_it_makes_live()
    {
        foreach (var step in OpeningScript.Steps)
        {
            if (step.Mode == TutorialStepMode.ForceNavigate)
            {
                // A forced navigation is a rail tile, so it needs a screen and needs no region.
                Assert.NotNull(step.Screen);
                Assert.Null(step.Target);
                Assert.Equal(StageGate.OnScreen, step.Gate);
            }
            if (step.Mode == TutorialStepMode.ForceAction)
            {
                // A forced deed is one control, so it needs the region that owns it — and its gate is
                // the DEED, never an acknowledgement: a card closed is not a chest opened.
                Assert.NotNull(step.Target);
                Assert.NotEqual(StageGate.Acknowledged, step.Gate);
            }
        }
    }

    [Fact]
    public void test_every_beat_that_speaks_is_one_idea_and_one_action()
    {
        // ONE MOMENT. ONE IDEA. ONE ACTION. No encyclopedia cards: a paused beat is a title and at
        // most two short sentences, and a title is a name rather than a summary.
        foreach (var step in OpeningScript.Steps)
        {
            if (step.Title.Length == 0) { Assert.Equal("", step.Body); continue; }

            Assert.InRange(step.Title.Split(' ').Length, 1, 5);
            Assert.True(step.Body.Length <= 110, $"{step.Stage}'s body is a paragraph: \"{step.Body}\"");
            Assert.InRange(step.Body.Count(c => c == '.'), 1, 2);
            _out.WriteLine($"{step.Stage,-22} {step.Mode,-14} {step.Title}");

            // AND IT TEACHES A CONCEPT, NEVER A SHORTCUT. "PRESS K TO OPEN VAULT" is what the real
            // control being lit exists to replace. The word itself is fine — "you never press attack"
            // is the concept — so what is forbidden is naming a KEY.
            var text = (step.Title + " " + step.Body).ToUpperInvariant();
            foreach (var key in new[] { "PRESS B", "PRESS K", "PRESS F", "PRESS T", "PRESS L", "PRESS V",
                                        "PRESS G", "PRESS M", "PRESS R", "PRESS W", "PRESS ESC",
                                        "HOTKEY", "SHORTCUT" })
                Assert.DoesNotContain(key, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void test_no_retired_vocabulary_reaches_the_opening()
    {
        string[] dead = { "RESONANCE HUNTER", "STARTING SKILL", "TRAIT TREE", "MEMORY TREE", "APTITUDE" };
        foreach (var step in OpeningScript.Steps)
            foreach (var word in dead)
                Assert.DoesNotContain(word, step.Title + " " + step.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_the_tutorial_boss_is_the_games_own_first_boss()
    {
        // NOT AN EXTRA ENCOUNTER. The game already makes every fifth wave a boss; the tutorial boss is
        // simply the first of those, so the cadence afterwards is untouched and the player has seen
        // four ordinary waves before it arrives.
        Assert.Equal(ExpeditionTuning.Default.BossEvery, OpeningScript.TutorialBossWave);
        Assert.True(WaveScaling.IsBossWave(OpeningScript.TutorialBossWave, ExpeditionTuning.Default));
        Assert.False(WaveScaling.IsBossWave(OpeningScript.TutorialBossWave - 1, ExpeditionTuning.Default));
    }

    [Fact]
    public void test_an_unknown_saved_cursor_resolves_forward_and_never_into_a_soft_lock()
    {
        // A save written by a LATER build can carry a stage this one has never heard of. Resolving it
        // to Complete releases the player into the game; resolving it to a stage they never reached
        // would restart a forced step whose deed is already done, which is a room with no door.
        Assert.Equal(OpeningStage.NotStarted, OpeningScript.StageOf(0));
        Assert.Equal(OpeningStage.Prologue, OpeningScript.StageOf((int)OpeningStage.Prologue));
        Assert.Equal(OpeningStage.Complete, OpeningScript.StageOf(9_999));
        Assert.Equal(OpeningStage.Complete, OpeningScript.StageOf(-3));
    }

    [Fact]
    public void test_the_version_decides_who_has_already_lived_the_opening()
    {
        // THE THIRD CONSTANT IN THIS FAMILY, and it carries the same law as the other two: a guess
        // made from progression cannot tell a veteran from a brand-new player who quit during the
        // arrival, and the file's version can.
        var veteran = new UnlockFacts(WavesCleared: 400, DeepestWave: 120, ItemsOwned: 20, ChestsEverHeld: 9);
        Assert.True(OpeningScript.SeedOpeningAsLived(veteran, OpeningScript.FirstVersionWithOpeningState - 1));
        Assert.False(OpeningScript.SeedOpeningAsLived(veteran, OpeningScript.FirstVersionWithOpeningState));
        Assert.False(OpeningScript.SeedOpeningAsLived(veteran, SaveGame.CurrentVersion));

        // ...and an old file with nothing behind it is not a veteran either — it simply plays.
        Assert.False(OpeningScript.SeedOpeningAsLived(new UnlockFacts(),
                                                      OpeningScript.FirstVersionWithOpeningState - 1));
    }

    [Fact]
    public void test_the_opening_migration_version_is_frozen_at_six()
    {
        // IT MUST NEVER BE SaveGame.CurrentVersion. Written that way, the next bump would migrate
        // files that already carry an honest cursor, and every player halfway through the opening
        // would be marked as having finished it.
        Assert.Equal(6, OpeningScript.FirstVersionWithOpeningState);
        Assert.True(OpeningScript.FirstVersionWithOpeningState <= SaveGame.CurrentVersion,
                    "the opening cannot first be persisted in a version that does not exist yet");
    }

    [Fact]
    public void test_the_prologue_is_the_world_and_never_a_control()
    {
        // FOUR TO SIX BEATS, and every one of them about the place rather than the interface. The
        // teaching starts on the other side of BEGIN THE HUNT; a prologue that mentioned a button
        // would be a tutorial wearing a story's clothes, which is the thing the redesign replaced.
        Assert.InRange(OpeningScript.Prologue.Count, 4, 6);

        var forbidden = new[]
        {
            "CLICK", "BUTTON", "SCREEN", "TILE", "TAB", "MENU", "GLEAM", "CHEST", "VAULT",
            "EQUIP", "TRAINING", "WAVE", "HOTKEY", "SHORTCUT", "PRESS ",
        };
        foreach (var beat in OpeningScript.Prologue)
        {
            Assert.NotEqual("", beat.Title);
            Assert.NotEqual("", beat.Body);
            // A HEADING, not a sentence — the same rule the steps keep.
            Assert.InRange(beat.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, 1, 5);
            Assert.Equal(beat.Title.ToUpperInvariant(), beat.Title);
            // Longer than a step's body, because this one is being read rather than acted on — but
            // still a subtitle, which is two sentences at a readable width.
            Assert.InRange(beat.Body.Length, 20, 165);

            var text = (beat.Title + " " + beat.Body).ToUpperInvariant();
            foreach (var word in forbidden)
                Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }

        // AND IT ENDS WHERE THE GAME BEGINS. The last beat names the place the arrival lands in, so
        // the cut from the story to the stage is a continuation rather than a subject change.
        Assert.Contains("VERDANT HOLLOW", OpeningScript.Prologue[^1].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void test_a_rail_beat_points_at_the_tile_the_chain_is_heading_for()
    {
        // "IT LEFT A CHEST — chests wait in the VAULT" wants ONE tile lit, not the whole rail, and the
        // tile it wants is by definition the one the next forced step makes the player press. Read
        // from the script so re-ordering the chain moves the light with it.
        Assert.Equal(Activity.Vault, OpeningScript.NextForcedScreen(OpeningStage.IntroduceChest));
        Assert.Equal(Activity.Vault, OpeningScript.NextForcedScreen(OpeningStage.ForceVault));
        Assert.Equal(Activity.Gear, OpeningScript.NextForcedScreen(OpeningStage.IntroduceItem));
        Assert.Equal(Activity.Gear, OpeningScript.NextForcedScreen(OpeningStage.ForceGear));

        // Past the last forced step there is nothing left to point at, and past the end of the script
        // there is no script — neither may invent a tile.
        Assert.Null(OpeningScript.NextForcedScreen(OpeningStage.ShowEquipped));
        Assert.Null(OpeningScript.NextForcedScreen(OpeningStage.Complete));

        // EVERY beat that lights the rail has somewhere for that light to land. A rail beat with no
        // destination would darken the page and cut no hole, which is the failure the whole-canvas
        // sentinel exists to make visible.
        foreach (var step in OpeningScript.Steps.Where(s => s.Target == TourTarget.NavRail))
            Assert.NotNull(OpeningScript.NextForcedScreen(step.Stage));
    }

    [Fact]
    public void test_the_depth_beat_lights_the_header_and_not_the_pack()
    {
        // "HOW DEEP YOU ARE" is about the wave number and the conquest bar, both of which live in the
        // stage header. It pointed at Enemies, whose spotlight is the creatures AND the header — so
        // the card about depth lit the thing it was not about, twice as brightly.
        var depth = Assert.IsType<OpeningStep>(OpeningScript.Find(OpeningStage.IntroduceStage));
        Assert.Equal(TourTarget.StageHeader, depth.Target);

        // ...and the beat before it, which IS about the creatures, still points at them.
        var enemies = Assert.IsType<OpeningStep>(OpeningScript.Find(OpeningStage.IntroduceEnemy));
        Assert.Equal(TourTarget.Enemies, enemies.Target);
    }
}
