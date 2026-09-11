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
            // A NULL Sends IS THE HUNT — the host's own fallback (Game1.DrawCoachSpotlight). Skipping
            // those here would have left READ THE LOG, the most important light in the game, with no
            // density coverage at all.
            var screen = OnboardingLessons.Sends(id) ?? Activity.Hunt;

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
                var screen = OnboardingLessons.Sends(id) ?? Activity.Hunt;
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
        // player to a MENU screen — and that null Sends is load-bearing twice over: it is what makes
        // Game1.HuntLessonShowing draw their cards on the fight, and what the spotlight's
        // `?? Activity.Hunt` fallback reads.
        foreach (var id in new[] { OnboardingLessonId.FirstFailureReport,
                                   OnboardingLessonId.FirstPostFailureChange,
                                   OnboardingLessonId.FirstRetry })
        {
            Assert.Null(OnboardingLessons.Sends(id));
            Assert.NotEqual(LessonMode.Observe, OnboardingLessons.Mode(id));
        }

        // ...AND ALL THREE POINT AT SOMETHING, because the copy now rides beside a light and a light
        // needs a hole. IT FELL marks the door to the report; MAKE ONE CHANGE marks the RAIL, since a
        // change is a rank, a worn piece, a node or the weave and those live on four different
        // screens; GO AGAIN marks the champion, who is the one doing it.
        Assert.Equal(TourTarget.LogButton, OnboardingLessons.Target(OnboardingLessonId.FirstFailureReport));
        Assert.Equal(TourTarget.NavRail, OnboardingLessons.Target(OnboardingLessonId.FirstPostFailureChange));
        Assert.Equal(TourTarget.Champion, OnboardingLessons.Target(OnboardingLessonId.FirstRetry));
    }

    [Fact]
    public void test_every_lesson_names_something_to_light()
    {
        // THE PLAYTEST THIS PINS. "The message box at the top is not read and not taken seriously —
        // act as if it does not exist." So the copy moved beside a spotlight, and a spotlight needs a
        // hole: a lesson with no Target and no other screen to send the player to would fall back to
        // the very slot the playtest said nobody reads. Every lesson must name one or the other.
        foreach (var id in OnboardingLessons.All)
        {
            var lights = OnboardingLessons.Target(id) is not null || OnboardingLessons.Sends(id) is not null;
            Assert.True(lights, $"{id} has nothing to light, so its words would go back in the toast slot");
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_observe_beats_light_a_real_part_of_the_fight(int percent)
    {
        // They ask for nothing, but they are ABOUT something, and a remark beside a lit champion is
        // read where a toast is not. All four live on the HUNT, so the HUNT must answer for all four.
        UiMetrics.Apply(percent);
        foreach (var id in OnboardingLessons.All)
        {
            if (OnboardingLessons.Mode(id) != LessonMode.Observe) continue;
            Assert.Equal("", OnboardingLessons.Action(id));      // still asks for nothing
            var target = OnboardingLessons.Target(id);
            Assert.NotNull(target);
            var lit = HuntScreen.Spotlights(target!.Value);
            Assert.True(lit.Length > 0, $"{id}: the HUNT does not answer for {target} at {percent}%");
            foreach (var r in lit)
                Assert.True(r.Width > 0 && r.Height > 0 && !(r.Width >= 1900 && r.Height >= 1060),
                            $"{id}: {target} is empty or the whole canvas at {percent}% ({r})");
        }
        UiMetrics.Apply(100);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_read_the_log_lights_the_expedition_log_medallion_at_every_density(int percent)
    {
        // THE CLAIM: the bracket falls on the button that opens the report, at every profile — the
        // same rectangle HuntScreen.DrawLogButton draws from and hit-tests, never a copy of it, and
        // never a hardcoded pixel. The medallion's EDGE follows the density profile, so this also
        // proves the light grows with the control rather than staying at its 100 % size.
        UiMetrics.Apply(percent);
        var lit = HuntScreen.Spotlights(TourTarget.LogButton);
        Assert.Single(lit);
        var r = lit[0];

        Assert.True(r.Width >= UiMetrics.Control(64) && r.Height >= UiMetrics.Control(64),
                    $"the light is smaller than the control it marks at {percent}% ({r})");
        Assert.True(r.Intersects(new Rectangle(0, 0, 1920, 1080)), $"off the page at {percent}% ({r})");
        Assert.False(r.Width >= 1900 && r.Height >= 1060, $"the whole-canvas fallback at {percent}%");

        // ...and it is the HUNT that answers for it, through the same route the host takes.
        Assert.Null(OnboardingLessons.Sends(OnboardingLessonId.FirstFailureReport));
        Assert.Equal(r, Resolve(Activity.Hunt, OnboardingLessons.Target(OnboardingLessonId.FirstFailureReport)!.Value)[0]);

        // AND THE CONTROL IS NOT UNDER THE CHROME. At 150 % the capsules are half again as tall and
        // their chain reaches back past the medallion's left edge, so a medallion pinned at a literal
        // y of 40 was drawn half underneath the GLEAM pill — a light pointing at something invisible.
        // Both now hang off Game1.ChromeRowBottom, and this is what keeps them apart.
        var control = HuntScreen.LogButtonRect;
        Assert.True(control.Top >= Game1.ChromeRowBottom,
                    $"the log medallion runs up into the currency row at {percent}% ({control})");
        Assert.True(r.Contains(control), $"the light does not cover the control at {percent}% ({r} vs {control})");
        UiMetrics.Apply(100);
    }

    [Fact]
    public void test_the_wave_lane_takes_a_second_row_only_when_one_will_not_hold_it()
    {
        // The rule the fall banner's overlap came down to, on its own and without a font. The wave
        // line is the one line in the stage header with no fit ladder: the region title above it steps
        // its rung down to fit, the conquest row below it reserves its bar around a measured label,
        // and this was simply centred and drawn — so at 150 % "WAVE 11 — RECOVERING — BACK TO WAVE 11"
        // grew past both of the header's rails and printed itself over the hunter's health readout.
        foreach (var percent in new[] { 100, 125, 150 })
        {
            UiMetrics.Apply(percent);
            Assert.Equal(1, HuntScreen.WaveRowsFor(10, 480));
            Assert.Equal(1, HuntScreen.WaveRowsFor(480, 480));    // exactly full still fits
            Assert.Equal(2, HuntScreen.WaveRowsFor(481, 480));
            Assert.Equal(2, HuntScreen.WaveRowsFor(9_999, 480));
        }
        UiMetrics.Apply(100);
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
        var watched = Fresh() with { WavesCleared = 3, DeepestWave = 3, Gleam = 60, ChestsHeld = 1 };
        d.Update(1 / 60f, watched, Idle);
        Assert.True(d.Quiet, "a satisfied lesson did not start a quiet period");
        Assert.Null(d.Showing);

        // ...and after the quiet, the next one — never two at once, and TRAINING before the chest.
        for (var t = 0f; t < OnboardingDirector.QuietAfterLesson + 0.5f; t += 0.25f) d.Update(0.25f, watched, Idle);
        Assert.Equal(OnboardingLessonId.FirstTrainingPurchase, d.Showing);
    }

    [Fact]
    public void test_the_director_does_not_talk_over_a_reward_or_a_modal()
    {
        var d = new OnboardingDirector();
        var facts = Fresh() with { WavesCleared = 4, DeepestWave = 4, StatsTrained = 1, ChestsHeld = 1 };

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
    public void test_nothing_is_said_over_a_modal_not_even_a_lesson_that_belongs_to_the_moment()
    {
        // The authored opening is ModalUp. The fall lessons' exemption is for the run log and the reward
        // panel; it let READ THE LOG through the opening after the tutorial boss's first win.
        var d = new OnboardingDirector();
        var fell = Fresh() with { WavesCleared = 4, DeepestWave = 4, StatsTrained = 1, ChestsHeld = 1, Falls = 1 };
        for (var i = 0; i < 30; i++)
        {
            d.Update(1 / 60f, fell, new OnboardingDirector.Busy(RewardUp: false, ModalUp: true, ReportUp: false));
            Assert.Null(d.Showing);
        }

        // ...and it is said the moment the modal hands the game back.
        d.Update(1 / 60f, fell, Idle);
        Assert.Equal(OnboardingLessonId.FirstFailureReport, d.Showing);
    }

    [Fact]
    public void test_muting_a_card_silences_it_and_completes_nothing()
    {
        var d = new OnboardingDirector();
        var facts = Fresh() with { WavesCleared = 4, DeepestWave = 4, StatsTrained = 1, ChestsHeld = 1 };
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
        var facts = Fresh() with { WavesCleared = 4, DeepestWave = 4, StatsTrained = 1, ChestsHeld = 1, GuidanceOff = true };
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
        var mid = Fresh() with { WavesCleared = 4, DeepestWave = 4, StatsTrained = 1, ChestsHeld = 1 };
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
