using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// EVERY GUIDED LESSON LIGHTS A REAL CONTROL, AT EVERY DENSITY.
/// </summary>
/// <remarks>
/// <para>
/// A HardGuide's whole claim is that the thing it marks is the thing that takes the click. That claim
/// is only true if the semantic target it names resolves, through the screen that owns it, to the same
/// rectangle the screen draws and hit-tests — and if it keeps resolving when the layout reflows at 125
/// and 150 per cent, where panels shrink, columns scroll and a fixed coordinate would point at empty
/// floor.
/// </para>
/// <para>
/// So this file asks every screen for every target the catalogue names, at all three profiles. It
/// cannot check that the lit rectangle is the RIGHT control — no test can read intent — but it can
/// prove that none of them is missing, empty, off the page, or the whole-canvas fallback that means
/// "this screen has never heard of that target", which is what a silently renamed target would leave
/// behind.
/// </para>
/// </remarks>
public class OnboardingTargetTests
{
    private readonly ITestOutputHelper _out;

    public OnboardingTargetTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    /// <summary>The host's resolver, in miniature: the screen that owns a target answers for it.</summary>
    private static Rectangle[] Resolve(Activity screen, TourTarget target) => screen switch
    {
        Activity.Hunt => HuntScreen.Spotlights(target),
        Activity.Training => TrainingScreen.Spotlights(target),
        Activity.Gear => GearScreen.Spotlights(target),
        Activity.Build => new LoadoutScreen(null!).Spotlights(target),
        Activity.Mastery => MasteryScreen.Spotlights(target),
        Activity.Vault => VaultScreen.Spotlights(target),
        Activity.Forge => ForgeScreen.Spotlights(target),
        Activity.Warren => new WarrenScreen(null!).Spotlights(target),
        Activity.Map => new MapScreen(null!).Spotlights(target),
        Activity.Traits => TraitCollectionScreen.Spotlights(target),
        Activity.Roster => RosterScreen.Spotlights(target),
        _ => Array.Empty<Rectangle>(),
    };

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_every_guided_lesson_resolves_to_a_real_rectangle(int percent)
    {
        UiMetrics.Apply(percent);
        var page = new Rectangle(0, 0, 1920, 1080);
        var checkedAny = 0;

        foreach (var id in OnboardingLessons.All)
        {
            if (OnboardingLessons.Target(id) is not { } target) continue;
            if (OnboardingLessons.Sends(id) is not { } screen) continue;

            var rects = Resolve(screen, target);
            Assert.True(rects.Length > 0, $"{id}: {screen} does not answer for {target} at {percent}%");
            foreach (var r in rects)
            {
                Assert.True(r.Width > 0 && r.Height > 0, $"{id}: {target} is empty at {percent}% ({r})");
                Assert.True(r.Intersects(page), $"{id}: {target} lands off the page at {percent}% ({r})");
            }
            checkedAny++;
            _out.WriteLine($"{id,-28} {screen,-9} {target,-18} {rects[0]}");
        }

        Assert.True(checkedAny >= 10, $"only {checkedAny} guided lessons were checked — the catalogue lost its targets");
        UiMetrics.Apply(100);
    }

    [Fact]
    public void test_no_guided_lesson_falls_back_to_the_whole_canvas()
    {
        // THE SILENT FAILURE THIS CATCHES. A screen answers an unknown target with nothing, and the
        // host turns nothing into "light the entire page" — which draws brackets round the whole screen
        // and points at everything at once. That is what a renamed or mistyped target looks like, and
        // it looks like a working spotlight until somebody photographs it.
        foreach (var percent in new[] { 100, 125, 150 })
        {
            UiMetrics.Apply(percent);
            foreach (var id in OnboardingLessons.All)
            {
                if (OnboardingLessons.Target(id) is not { } target) continue;
                if (OnboardingLessons.Sends(id) is not { } screen) continue;
                foreach (var r in Resolve(screen, target))
                    Assert.False(r.Width >= 1900 && r.Height >= 1060,
                                 $"{id} lights the whole canvas at {percent}% — {screen} does not know {target}");
            }
        }
        UiMetrics.Apply(100);
    }

    [Fact]
    public void test_the_gem_lesson_points_at_the_bag_and_not_at_the_four_forge_jobs()
    {
        // "Do NOT first tour all four Forge jobs." The first gem is one deed — put this stone in that
        // item — and the tab strip is the thing a first-visit Forge tour used to explain in full
        // before the player could do anything. The lesson marks the BAG, where the gem is.
        Assert.Equal(TourTarget.Bag, OnboardingLessons.Target(OnboardingLessonId.FirstGemSocket));
        Assert.NotEqual(TourTarget.ForgeTabs, OnboardingLessons.Target(OnboardingLessonId.FirstGemSocket));

        // ...and no lesson anywhere marks the tab strip: UPGRADE, RE-ROLL and SALVAGE are taught when
        // they first matter, by the screen's own state, not by an opening lecture.
        foreach (var id in OnboardingLessons.All)
            Assert.NotEqual(TourTarget.ForgeTabs, OnboardingLessons.Target(id));
    }

    [Fact]
    public void test_the_fall_lessons_stay_on_the_hunt_where_the_report_is()
    {
        // READ THE LOG → MAKE ONE CHANGE → TRY AGAIN is one sequence and it lives around the arena:
        // the log opens over the HUNT, and the retry is the next descent. None of the three sends the
        // player to a menu screen, so none of them marks a control on one.
        foreach (var id in new[] { OnboardingLessonId.FirstFailureReport,
                                   OnboardingLessonId.FirstPostFailureChange,
                                   OnboardingLessonId.FirstRetry })
        {
            Assert.Null(OnboardingLessons.Sends(id));
            Assert.Null(OnboardingLessons.Target(id));
            Assert.NotEqual(LessonMode.Observe, OnboardingLessons.Mode(id));
        }
    }

    // ── THE DIRECTOR'S OWN BEHAVIOUR ─────────────────────────────────────────────────────────────

    private static LessonFacts Fresh() => new(HuntersOwned: 1);

    private static OnboardingDirector.Busy Idle => new(RewardUp: false, ModalUp: false, ReportUp: false);

    [Fact]
    public void test_the_director_says_one_thing_and_then_holds_its_tongue()
    {
        var d = new OnboardingDirector();
        d.Update(1 / 60f, Fresh(), Idle);
        Assert.Equal(OnboardingLessonId.FirstFight, d.Showing);

        // The deed done: the lesson goes, and the QUIET follows it so the next ask does not land on
        // top of the reward for this one.
        var watched = Fresh() with { WavesCleared = 1, DeepestWave = 1, ChestsHeld = 1 };
        d.Update(1 / 60f, watched, Idle);
        Assert.True(d.Quiet, "a satisfied lesson did not start a quiet period");
        Assert.Null(d.Showing);

        // ...and after the quiet, the next one — never two at once.
        for (var t = 0f; t < OnboardingDirector.QuietAfterLesson + 0.5f; t += 0.25f) d.Update(0.25f, watched, Idle);
        Assert.Equal(OnboardingLessonId.FirstChestOpen, d.Showing);
    }

    [Fact]
    public void test_the_director_does_not_talk_over_a_reward_or_a_modal()
    {
        var d = new OnboardingDirector();
        var facts = Fresh() with { WavesCleared = 4, DeepestWave = 4, ChestsHeld = 1 };

        d.Update(1 / 60f, facts, new OnboardingDirector.Busy(RewardUp: true, ModalUp: false, ReportUp: false));
        Assert.Null(d.Showing);
        d.Update(1 / 60f, facts, new OnboardingDirector.Busy(RewardUp: false, ModalUp: true, ReportUp: false));
        Assert.Null(d.Showing);

        // ...but the fall lessons belong to the report, so they survive it being open.
        var fell = facts with { Falls = 1 };
        d.Update(1 / 60f, fell, new OnboardingDirector.Busy(RewardUp: false, ModalUp: false, ReportUp: true));
        Assert.Equal(OnboardingLessonId.FirstFailureReport, d.Showing);
    }

    [Fact]
    public void test_muting_a_card_silences_it_and_completes_nothing()
    {
        var d = new OnboardingDirector();
        var facts = Fresh() with { WavesCleared = 4, DeepestWave = 4, ChestsHeld = 1 };
        d.Update(1 / 60f, facts, Idle);
        Assert.Equal(OnboardingLessonId.FirstChestOpen, d.Showing);

        d.Mute(OnboardingLessonId.FirstChestOpen);
        d.Update(1 / 60f, facts, Idle);
        Assert.NotEqual(OnboardingLessonId.FirstChestOpen, d.Showing);

        // The chest is still unopened, and every gate that reads that is untouched.
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstChestOpen, facts));
    }

    [Fact]
    public void test_skip_guidance_makes_the_director_silent_without_touching_the_facts()
    {
        var d = new OnboardingDirector();
        var facts = Fresh() with { WavesCleared = 4, DeepestWave = 4, ChestsHeld = 1, GuidanceOff = true };
        for (var i = 0; i < 10; i++) d.Update(1 / 60f, facts, Idle);
        Assert.Null(d.Showing);
        Assert.False(OnboardingLessons.Completed(OnboardingLessonId.FirstChestOpen, facts));
    }

    [Fact]
    public void test_a_load_mid_lesson_resumes_rather_than_dead_ending()
    {
        // SAVE AND LOAD IN THE MIDDLE OF A LESSON. There is no stored lesson state to restore, so the
        // fresh director simply re-derives from the world — which is the whole reason completion was
        // put on facts. Two cases, and neither can trap anybody: the deed was done while away (the
        // lesson is complete and never reappears), or it was not (it resumes exactly where it was).
        var mid = Fresh() with { WavesCleared = 4, DeepestWave = 4, ChestsHeld = 1 };
        var before = new OnboardingDirector();
        before.Update(1 / 60f, mid, Idle);
        Assert.Equal(OnboardingLessonId.FirstChestOpen, before.Showing);

        var afterLoadStillOwed = new OnboardingDirector();
        afterLoadStillOwed.Update(1 / 60f, mid, Idle);
        Assert.Equal(OnboardingLessonId.FirstChestOpen, afterLoadStillOwed.Showing);

        var done = mid with { ChestsOpened = 1, ChestsHeld = 0, ItemsOwned = 1 };
        var afterLoadDone = new OnboardingDirector();
        afterLoadDone.Update(1 / 60f, done, Idle);
        Assert.NotEqual(OnboardingLessonId.FirstChestOpen, afterLoadDone.Showing);
    }

    // ── THE RAISED OBSERVE BEATS ─────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_raised_beat_waits_for_the_panel_it_would_have_landed_under()
    {
        // The first conquest raises its beat on the same frame as the conquest banner, the keystone
        // reveal and the socket notice. A card drawn under all of that is a card nobody saw — so the
        // beat holds, at full dwell, and speaks when the surface clears.
        var d = new OnboardingDirector();
        var won = Fresh() with { WavesCleared = 20, DeepestWave = 20, RegionsConquered = 1, RegionsOpen = 2 };
        d.Raise(OnboardingLessonId.FirstRegionConquest);

        var reading = new OnboardingDirector.Busy(RewardUp: true, ModalUp: false, ReportUp: false);
        for (var i = 0; i < 300; i++) d.Update(1 / 60f, won, reading);   // five seconds behind the panel
        Assert.Null(d.Showing);

        d.Update(1 / 60f, won, Idle);
        Assert.Equal(OnboardingLessonId.FirstRegionConquest, d.Showing);
    }

    [Fact]
    public void test_a_raised_beat_leaves_by_itself_and_is_never_raised_twice()
    {
        var d = new OnboardingDirector();
        var facts = Fresh() with { WavesCleared = 6, DeepestWave = 6, BossesFelled = 1 };
        d.Raise(OnboardingLessonId.FirstBoss);
        d.Update(1 / 60f, facts, Idle);
        Assert.Equal(OnboardingLessonId.FirstBoss, d.Showing);

        // It asks for nothing, so nothing has to be done to it: the dwell runs out and it goes.
        for (var t = 0f; t < OnboardingDirector.ObserveDwell + 0.5f; t += 0.25f) d.Update(0.25f, facts, Idle);
        Assert.NotEqual(OnboardingLessonId.FirstBoss, d.Showing);

        // ...and the second boss says nothing at all.
        d.Raise(OnboardingLessonId.FirstBoss);
        for (var t = 0f; t < OnboardingDirector.QuietAfterReward + 1f; t += 0.25f) d.Update(0.25f, facts, Idle);
        Assert.NotEqual(OnboardingLessonId.FirstBoss, d.Showing);
    }

    [Fact]
    public void test_skip_guidance_silences_a_raised_beat_too()
    {
        var d = new OnboardingDirector();
        var facts = Fresh() with { WavesCleared = 6, DeepestWave = 6, BossesFelled = 1, GuidanceOff = true };
        d.Raise(OnboardingLessonId.FirstBoss);
        for (var i = 0; i < 60; i++) d.Update(1 / 60f, facts, Idle);
        Assert.Null(d.Showing);
    }

    [Fact]
    public void test_every_observe_beat_has_something_to_say()
    {
        // An Observe lesson has no deed, so its Why IS its body — an Observe with neither would draw
        // an empty card, which is the one outcome worse than saying nothing.
        foreach (var id in OnboardingLessons.All)
        {
            if (OnboardingLessons.Mode(id) != LessonMode.Observe) continue;
            Assert.Equal("", OnboardingLessons.Action(id));
            Assert.True(OnboardingLessons.Why(id).Length > 0, $"{id} is an Observe beat with an empty body");
            Assert.True(OnboardingLessons.Title(id).Length > 0, $"{id} is an Observe beat with no title");
        }
    }

    [Fact]
    public void test_the_telemetry_names_the_milestone_that_actually_matters()
    {
        // Not "tutorial completed". The FTUE question worth asking is whether the player opened their
        // first failure report, changed something, and went back down.
        var d = new OnboardingDirector();
        var lived = Fresh() with { WavesCleared = 30, DeepestWave = 30, Falls = 2,
                                   ReportOpenedEver = true, ChangedAfterFall = true, RetriedAfterChange = true };
        d.Update(1 / 60f, lived, Idle);
        Assert.Contains(d.Telemetry(lived), row => row.Contains("ftue_loop_lived=True", StringComparison.Ordinal));

        var notYet = lived with { RetriedAfterChange = false };
        Assert.Contains(new OnboardingDirector().Telemetry(notYet),
                        row => row.Contains("ftue_loop_lived=False", StringComparison.Ordinal));
    }
}
