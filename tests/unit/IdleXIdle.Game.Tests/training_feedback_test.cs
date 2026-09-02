using IdleXIdle.Core.Economy;
using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The TRAINING screen's one transient: the feedback a bought rank plays (UI polish brief §36–§37, §39 —
/// "on train: cost paid → stat highlights → progress animates → inspector updates; no modal").
/// </summary>
/// <remarks>
/// <para>
/// The capture rig can photograph this held at ONE instant (<c>RH_SHOT_TRAIN=MIGHT</c> with
/// <c>RH_SHOT_TRAIN_T</c>), and three such captures exist. What a still cannot show is that the tick is
/// continuous and that it ENDS ON THE TRUTH — the Hunter's live figures — rather than on a number the
/// animation invented. That is what these drive: t = 0, the middle of the transition, and the end.
/// </para>
/// <para>
/// The numbers are the `stats` capture fixture's own, read off the two captures either side of the
/// purchase (<c>p2_training_rest_100.png</c> → <c>p2_training_train_t1_100.png</c>): MIGHT rank 12 → 13,
/// one rank costing 108 Gleam out of 18,670, BASIC HIT 96 → 98 now and 98 → 99 after. They are constants
/// here rather than inline literals so the test says which state it is posing.
/// </para>
/// </remarks>
public sealed class TrainingFeedbackTests : IDisposable
{
    private const HunterStat Row = HunterStat.AttackPower;

    // The `stats` fixture, either side of one TRAIN — what the screen SHOWED, then what the Hunter says.
    private const float GleamShown = 18_670f, GleamLive = 18_562f;
    private const float NowShown = 96f, NowLive = 98f;
    private const float AfterShown = 98f, AfterLive = 99f;
    private const float RankShown = 12f, RankLive = 13f;

    /// <summary>Half the transition — the capture's own middle, and one clean step of the ease.</summary>
    private const float HalfTransition = UiMotion.Transition / 2f;

    /// <summary>UiMotion is a process-wide store; every test starts from an empty one and hands it back.</summary>
    public void Dispose()
    {
        UiMotion.Reduced = false;
        UiMotion.Clear();
    }

    /// <summary>A feedback started exactly the way the screen starts one: the host ticks, then Update begins it.</summary>
    private static TrainingScreen.TrainFeedback Started(bool reduced = false)
    {
        UiMotion.Clear();
        UiMotion.Reduced = reduced;
        UiMotion.Tick(0f);
        var fx = new TrainingScreen.TrainFeedback();
        fx.Start(Row, GleamShown, NowShown, AfterShown, RankShown);
        return fx;
    }

    private static float ShownNow(TrainingScreen.TrainFeedback fx)
        => TrainingScreen.TrainFeedback.Mix(fx.NowFrom, NowLive, fx.Progress);

    private static float ShownAfter(TrainingScreen.TrainFeedback fx)
        => TrainingScreen.TrainFeedback.Mix(fx.AfterFrom, AfterLive, fx.Progress);

    private static float ShownGleam(TrainingScreen.TrainFeedback fx)
        => TrainingScreen.TrainFeedback.Mix(fx.GleamFrom, GleamLive, fx.Progress);

    private static float ShownRank(TrainingScreen.TrainFeedback fx)
        => TrainingScreen.TrainFeedback.Mix(fx.RankFrom, RankLive, fx.Progress);

    [Fact]
    public void test_the_first_frame_shows_the_old_figures_under_a_full_flash()
    {
        var fx = Started();
        fx.Advance();

        Assert.True(fx.Live);
        Assert.True(fx.Playing(Row));
        Assert.Equal(0f, fx.Progress, 3);
        Assert.Equal(1f, fx.Glow, 3);
        // Nothing has moved yet: the row, the inspector and the purse still read what they read when
        // the button was pressed, so the change is seen to happen rather than having already happened.
        Assert.Equal(NowShown, ShownNow(fx), 3);
        Assert.Equal(AfterShown, ShownAfter(fx), 3);
        Assert.Equal(GleamShown, ShownGleam(fx), 3);
        Assert.Equal(RankShown, ShownRank(fx), 3);
    }

    [Fact]
    public void test_the_middle_of_the_transition_is_half_way_between_both_figures()
    {
        var fx = Started();
        fx.Advance();
        UiMotion.Tick(HalfTransition);
        fx.Advance();

        Assert.Equal(0.5f, fx.Progress, 3);
        Assert.Equal(0.5f, fx.Glow, 3);
        Assert.Equal(97f, ShownNow(fx), 3);          // 96 → 98
        Assert.Equal(98.5f, ShownAfter(fx), 3);      // 98 → 99
        Assert.Equal(18_616f, ShownGleam(fx), 3);    // 18,670 → 18,562
        Assert.Equal(12.5f, ShownRank(fx), 3);       // the bar, half a rank along
    }

    [Fact]
    public void test_the_tick_ends_on_the_hunters_own_figures_and_then_stops()
    {
        var fx = Started();
        fx.Advance();
        UiMotion.Tick(HalfTransition);
        fx.Advance();
        UiMotion.Tick(HalfTransition);
        fx.Advance();

        Assert.Equal(1f, fx.Progress, 3);
        Assert.Equal(0f, fx.Glow, 3);
        // The end state is the truth, never a number the animation invented.
        Assert.Equal(NowLive, ShownNow(fx), 3);
        Assert.Equal(AfterLive, ShownAfter(fx), 3);
        Assert.Equal(GleamLive, ShownGleam(fx), 3);
        Assert.Equal(RankLive, ShownRank(fx), 3);
        // And it is over: nothing is still playing, and the flash is not re-armed by anything.
        Assert.False(fx.Live);
        Assert.False(fx.Playing(Row));
        Assert.False(UiMotion.Pulsing(TrainingScreen.TrainFeedback.KeyFor(Row)));

        UiMotion.Tick(HalfTransition);
        fx.Advance();
        Assert.False(UiMotion.Pulsing(TrainingScreen.TrainFeedback.KeyFor(Row)));
    }

    [Fact]
    public void test_reduced_motion_reaches_the_same_end_state_at_once()
    {
        var fx = Started(reduced: true);
        fx.Advance();

        // No tick has passed, and every figure is already the Hunter's: Reduced Motion collapses the
        // tick to an instant state change with the SAME end state (brief §32).
        Assert.Equal(1f, fx.Progress, 3);
        Assert.Equal(NowLive, ShownNow(fx), 3);
        Assert.Equal(AfterLive, ShownAfter(fx), 3);
        Assert.Equal(GleamLive, ShownGleam(fx), 3);
        Assert.Equal(RankLive, ShownRank(fx), 3);

        // What survives is the fade that marks "this just changed" — a simple fade, which §32 keeps.
        Assert.Equal(1f, fx.Glow, 3);
        UiMotion.Tick(HalfTransition);
        fx.Advance();
        UiMotion.Tick(HalfTransition);
        fx.Advance();
        Assert.Equal(0f, fx.Glow, 3);
        Assert.False(fx.Live);
    }

    [Fact]
    public void test_a_second_rank_replays_from_where_the_figures_are()
    {
        var fx = Started();
        fx.Advance();
        UiMotion.Tick(HalfTransition);
        fx.Advance();

        // The screen photographs what it is SHOWING, not what it showed the first time, so a second
        // click part-way through the first tick continues from the number on the screen.
        var midNow = ShownNow(fx);
        fx.Start(Row, ShownGleam(fx), midNow, ShownAfter(fx), ShownRank(fx));
        fx.Advance();

        Assert.Equal(0.5f, fx.Progress, 3);   // one tick of dt is still in flight from the frame above
        Assert.Equal(1f, fx.Glow, 3);         // a NEW flash, one per event
        Assert.Equal(midNow, fx.NowFrom, 3);
    }

    [Fact]
    public void test_a_posed_capture_holds_the_feedback_at_one_instant()
    {
        var fx = Started();
        fx.Advance(0.5f);

        Assert.Equal(0.5f, fx.Progress, 3);
        Assert.Equal(0.5f, fx.Glow, 3);
        Assert.Equal(97f, ShownNow(fx), 3);

        // The shutter fires at frame 60: the pose must still be there, not have played out.
        for (var frame = 0; frame < 60; frame++)
        {
            UiMotion.Tick(1f / 60f);
            fx.Advance(0.5f);
        }
        Assert.True(fx.Live);
        Assert.Equal(0.5f, fx.Progress, 3);
        Assert.Equal(0.5f, fx.Glow, 3);
        Assert.Equal(97f, ShownNow(fx), 3);
    }

    [Fact]
    public void test_the_transition_stays_inside_the_briefs_band()
    {
        // §31: fast 80–120 ms · transition 150–220 ms · reward 250–450 ms. The train feedback is a
        // purchase, so it is the middle band, and it takes the shared constant rather than a number.
        Assert.InRange(UiMotion.Transition, 0.150f, 0.220f);
        Assert.InRange(UiMotion.Fast, 0.080f, 0.120f);
    }
}
