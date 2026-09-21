using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// FULL FOCUS IS FOR CRITICAL FIRST-USE TEACHING — and a demoted lesson goes QUIET, never silent.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LessonMode"/> was dead classification: the host asked only whether a rectangle could be
/// resolved, so Observe, SoftGuide and HardGuide all rendered as the same full-page scrim with a
/// spotlight cut in it. Twenty-four lessons declared three loudnesses and the game had one — and nine
/// of them blacked out the screen for six seconds to say what the screen's own quiet banner was
/// already saying.
/// </para>
/// <para>
/// The demotion is therefore a one-line data decision in Core. What makes it safe is that the slot
/// already exists and already takes a lesson: <c>SlotShowing</c> hands the top-of-screen banner to
/// "the one lesson the director chose, if the deed it asks for happens on this screen", precisely when
/// the coach is NOT lighting it. Nothing is lost — the title, the line and the × all survive.
/// </para>
/// </remarks>
public class LessonDemotionTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    /// <summary>The only lessons still allowed to darken the whole page.</summary>
    private static readonly OnboardingLessonId[] StillLoud =
    {
        OnboardingLessonId.FirstFailureReport,
        OnboardingLessonId.FirstPostFailureChange,
    };

    [Fact]
    public void test_only_the_fall_loop_still_darkens_the_page()
    {
        // THE LOOP THIS GAME IS: observe, ask why it stopped, change one thing, go again. It is the
        // strongest first-session milestone there is and the one piece of teaching that is genuinely
        // critical — so it keeps the full light, and nothing else does.
        var loud = OnboardingLessons.All
                                    .Where(id => OnboardingLessons.Mode(id) == LessonMode.HardGuide)
                                    .ToArray();
        Assert.Equal(StillLoud, loud);
    }

    [Fact]
    public void test_every_demoted_lesson_can_still_be_carried_by_the_quiet_slot()
    {
        // A DEMOTED LESSON GOES QUIET, NEVER SILENT — and there are exactly TWO quiet channels, both of
        // which already existed and are chosen by the same fact:
        //
        //   Sends != null  → the top-of-screen slot on the screen the deed happens on (SlotShowing)
        //   Sends == null  → the HUNT's own lesson card, which is, in the host's words, "what is left
        //                    for the few that light nothing" (HuntLessonShowing)
        //
        // A lesson that matched neither would be demoted into nowhere. FirstDispatchOpened is the one
        // that takes the second road: it is about the envelope in the chrome, which is on no screen.
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");
        Assert.Contains("return OnboardingLessons.Sends(step) is null ? step : null;", game, StringComparison.Ordinal);

        foreach (var id in OnboardingLessons.All)
        {
            if (OnboardingLessons.Mode(id) != LessonMode.SoftGuide) continue;
            Assert.NotEqual("", OnboardingLessons.Title(id));
            Assert.NotEqual("", OnboardingLessons.Action(id));
            // ...and it still names something to light, because the rail's NEW mark and any later
            // re-promotion both read the target. Demotion changes the TREATMENT, never the aim.
            Assert.True(OnboardingLessons.Target(id) is not null, $"{id} lost its target in the demotion");
        }

        // ...and the one lesson with no screen is the one the HUNT card exists for.
        var homeless = OnboardingLessons.All
                                        .Where(id => OnboardingLessons.Mode(id) == LessonMode.SoftGuide
                                                     && OnboardingLessons.Sends(id) is null)
                                        .ToArray();
        Assert.Equal(new[] { OnboardingLessonId.FirstDispatchOpened }, homeless);
    }

    [Fact]
    public void test_the_three_loudest_demotions_were_saying_what_the_screen_already_says()
    {
        // The case for demoting these three is not "they matter less" — it is that the screen they
        // send the player to prints the same fact as a quiet one-line banner, from Core, whenever it
        // is true. Darkening the page to repeat it is the whole complaint.
        var hint = File.ReadAllText(RepoFile("src", "IdleXIdle.Core", "Progression", "Onboarding.cs"));
        Assert.Contains("Activity.Mastery when f.MasteryPointsFree > 0", hint, StringComparison.Ordinal);
        Assert.Contains("Activity.Build when f.EmptySkillSlots > 0", hint, StringComparison.Ordinal);
        Assert.Contains("Activity.Build when f.SkillWithLevelToSpend is", hint, StringComparison.Ordinal);

        foreach (var id in new[] { OnboardingLessonId.FirstMasterySpend,
                                   OnboardingLessonId.FirstSharedSkillEquip,
                                   OnboardingLessonId.FirstVariationChoice })
            Assert.Equal(LessonMode.SoftGuide, OnboardingLessons.Mode(id));
    }

    [Fact]
    public void test_the_coach_releases_the_frame_for_a_lesson_it_has_stopped_lighting()
    {
        // THE BUG THIS PINS, AND IT WAS FOUND BY PHOTOGRAPHING THE DEMOTION RATHER THAN REASONING
        // ABOUT IT. The attention owner asked only whether the lesson RESOLVED a rectangle, which a
        // demoted lesson still does — so the coach took the frame for a light it no longer painted.
        // SlotShowing stands the slot down for anything above Feedback, and Coach is above it, so the
        // channel the lesson had just been moved INTO was closed by the lesson itself: seven lessons
        // made silent rather than quiet, with nothing on screen to show for them.
        //
        // The owner now asks the same question the paint does (CoachAims), which reads no owner and so
        // cannot be circular the way CoachLightsIt would be here.
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");
        Assert.Contains(": _coach.Showing is { } aimed && CoachAims(aimed) && CoachHoles(aimed).Length > 0 ? AttentionOwner.Coach",
                        game, StringComparison.Ordinal);

        // ...and the paint's own gate is what carries the Mode decision, in one place.
        Assert.Contains("if (OnboardingLessons.Mode(id) != LessonMode.HardGuide) return false;",
                        game, StringComparison.Ordinal);

        // ...and the slot is still the demoted lesson's home: it takes a lesson exactly when the coach
        // is not lighting it. If this line goes, the demotions become silence again.
        Assert.Contains("if (_coach.Showing is { } lesson && OnboardingLessons.Sends(lesson) == screen && !CoachLightsIt(lesson))",
                        game, StringComparison.Ordinal);
    }
}
