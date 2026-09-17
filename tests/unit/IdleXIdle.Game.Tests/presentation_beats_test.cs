using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE TWO PRESENTATION BOUNDARIES THE OPENING WAITS ON, stepped one frame at a time.
/// </summary>
/// <remarks>
/// <para>
/// The playtest of 2026-09-11 found two cards that arrived on a GAMEPLAY fact instead of a moment the
/// player had SEEN: GLEAM on the purse being paid (the last creature one frame into its fall), and
/// SIGNATURE SKILL over a cast whose wind-up was already on screen. A single screenshot cannot prove
/// an order, so these tests step the same arithmetic the HUNT screen runs — the replay barrier and the
/// between-wave break — frame by frame beside the real director, in the order the host runs a frame,
/// and assert the order of events.
/// </para>
/// <para>
/// The wave is the real one: wave two of a brand-new career as measured on 2026-09-11 (the Seeker in
/// Verdant Hollow, run 1). Its third creature dies to the career's FIRST Signature cast.
/// </para>
/// </remarks>
public class PresentationBeatsTest
{
    private const float FrameMs = 1000f / 60f;
    private const float FrameS = 1f / 60f;

    /// <summary>How far ahead of HARD HANDS' event its clip starts at the Seeker's opening tempo (≈ 796 ms).</summary>
    private const float CastLeadMs = 796f;

    /// <summary>Wave two of a fresh career, exactly as SoloExpedition resolved it (probe, 2026-09-11).</summary>
    private static List<BattleEvent> FreshCareerWaveTwo() => new()
    {
        new(BattleEventKind.Strike, 0, 135, 700, HitSource.Swing),
        new(BattleEventKind.EnemyDown, 0, 0, 700),
        new(BattleEventKind.Beat, 0, 4, 700),
        new(BattleEventKind.EnemyStrike, 0, 6, 1500),
        new(BattleEventKind.Strike, 1, 135, 2200, HitSource.Swing),
        new(BattleEventKind.EnemyDown, 1, 0, 2200),
        new(BattleEventKind.Beat, 0, 5, 2200),
        new(BattleEventKind.EnemyStrike, 0, 3, 3000),
        new(BattleEventKind.Skill, 0, 0, 3700),                          // HARD HANDS — the first cast of the career
        new(BattleEventKind.Strike, 2, 510, 3700, HitSource.Primary),    // ...its blow
        new(BattleEventKind.EnemyDown, 2, 0, 3700),                      // ...the wave's killing blow
        new(BattleEventKind.Beat, 0, 6, 3700),
    };

    /// <summary>Wave one of the same career: three swings, no cast at all.</summary>
    private static List<BattleEvent> FreshCareerWaveOne() => new()
    {
        new(BattleEventKind.Strike, 0, 135, 700, HitSource.Swing),
        new(BattleEventKind.EnemyDown, 0, 0, 700),
        new(BattleEventKind.EnemyStrike, 0, 6, 1500),
        new(BattleEventKind.Strike, 1, 135, 2200, HitSource.Swing),
        new(BattleEventKind.EnemyDown, 1, 0, 2200),
        new(BattleEventKind.EnemyStrike, 0, 3, 3000),
        new(BattleEventKind.Strike, 2, 135, 3700, HitSource.Swing),
        new(BattleEventKind.EnemyDown, 2, 0, 3700),
    };

    private static WaveReplay Replay(List<BattleEvent> events)
        => new(events, new Dictionary<int, int> { [0] = 171 }, new Dictionary<int, int> { [0] = 180 }, 306f);

    // ── THE SIGNATURE: held short of its wind-up, released as the same event, once. ───────────────

    [Fact]
    public void test_the_cast_is_held_short_of_its_wind_up_however_long_the_card_is_read()
    {
        var replay = Replay(FreshCareerWaveTwo());
        var playhead = 0f;
        var crossed = new List<BattleEvent>();
        int? heldAt = null;

        // Ten seconds of frames with the barrier on — far longer than the wave itself.
        for (var frame = 0; frame < 600; frame++)
        {
            var step = ReplayBarrier.Advance(replay, playhead, playhead + FrameMs, BattleEventKind.Skill, _ => CastLeadMs);
            if (step.Held) { heldAt ??= step.HeldAtMs; Assert.Equal(heldAt, step.HeldAtMs); }
            playhead = step.PlayheadMs;
            crossed.AddRange(replay.Advance(playhead));
        }

        Assert.Equal(3700, heldAt);
        // SHORT OF THE WIND-UP, not merely of the event: the clip would start at 3700 - 796, and the
        // playhead has never reached it — so no part of the cast has been drawn.
        Assert.True(playhead < 3700 - CastLeadMs, $"the playhead stood at {playhead} ms, inside the cast's wind-up");
        Assert.DoesNotContain(crossed, e => e.Kind == BattleEventKind.Skill);
        Assert.DoesNotContain(crossed, e => e.AtMs >= 3700);
        // ...while everything before the wind-up did play: two swings, two falls, the first bite.
        Assert.Equal(2, crossed.Count(e => e.Kind == BattleEventKind.EnemyDown));
        Assert.Single(crossed, e => e.Kind == BattleEventKind.EnemyStrike);
    }

    [Fact]
    public void test_releasing_the_hold_crosses_the_same_cast_exactly_once_and_before_any_other_action_of_the_hunter()
    {
        var replay = Replay(FreshCareerWaveTwo());
        var playhead = 0f;
        for (var frame = 0; frame < 300; frame++)
        {
            var step = ReplayBarrier.Advance(replay, playhead, playhead + FrameMs, BattleEventKind.Skill, _ => CastLeadMs);
            playhead = step.PlayheadMs;
            replay.Advance(playhead);
        }
        var heldAt = ReplayBarrier.Advance(replay, playhead, playhead + FrameMs, BattleEventKind.Skill, _ => CastLeadMs).HeldAtMs;
        Assert.Equal(3700, heldAt);

        // CONTINUE: the barrier comes off, and the fight runs on its own clock.
        var after = new List<BattleEvent>();
        for (var frame = 0; frame < 300; frame++)
        {
            var step = ReplayBarrier.Advance(replay, playhead, playhead + FrameMs, kind: null, _ => CastLeadMs);
            Assert.False(step.Held);
            playhead = step.PlayheadMs;
            after.AddRange(replay.Advance(playhead));
        }

        // THE SAME EVENT, ONCE. Nothing was synthesised, nothing replayed twice.
        var casts = after.Where(e => e.Kind == BattleEventKind.Skill).ToList();
        Assert.Single(casts);
        Assert.Equal(heldAt, casts[0].AtMs);
        Assert.Equal(0, casts[0].Slot);

        // AND IT IS THE HUNTER'S NEXT ACTION. The only thing allowed to cross between the release and
        // the cast is the enemy's own bite, which keeps its own clock; no swing and no other cast.
        var beforeCast = after.TakeWhile(e => e.Kind != BattleEventKind.Skill).ToList();
        Assert.All(beforeCast, e => Assert.Equal(BattleEventKind.EnemyStrike, e.Kind));
        // ...and its blow and the fall it causes follow it on the same beat.
        var rest = after.SkipWhile(e => e.Kind != BattleEventKind.Skill).ToList();
        Assert.Contains(rest, e => e.Kind == BattleEventKind.Strike && e.FromSkill && e.AtMs == heldAt);
        Assert.Contains(rest, e => e.Kind == BattleEventKind.EnemyDown && e.AtMs == heldAt);
    }

    [Fact]
    public void test_a_playhead_already_inside_the_wind_up_is_held_where_it_stands_and_never_rewound()
    {
        var replay = Replay(FreshCareerWaveTwo());
        replay.Advance(3200f);
        var step = ReplayBarrier.Advance(replay, 3200f, 3200f + FrameMs, BattleEventKind.Skill, _ => CastLeadMs);
        Assert.True(step.Held);
        Assert.Equal(3700, step.HeldAtMs);
        Assert.Equal(3200f, step.PlayheadMs);   // not pulled back to the park — that would replay what was seen
    }

    [Fact]
    public void test_with_no_barrier_or_no_such_beat_the_replay_runs_freely()
    {
        var replay = Replay(FreshCareerWaveTwo());
        var free = ReplayBarrier.Advance(replay, 100f, 100f + FrameMs, kind: null, _ => CastLeadMs);
        Assert.False(free.Held);
        Assert.Equal(100f + FrameMs, free.PlayheadMs);

        // Wave one holds no cast, so a Skill barrier over it is a barrier over nothing.
        var one = Replay(FreshCareerWaveOne());
        var playhead = 0f;
        for (var frame = 0; frame < 400; frame++)
        {
            var step = ReplayBarrier.Advance(one, playhead, playhead + FrameMs, BattleEventKind.Skill, _ => CastLeadMs);
            Assert.False(step.Held);
            playhead = step.PlayheadMs;
            one.Advance(playhead);
        }
        Assert.True(one.Finished);
    }

    [Fact]
    public void test_the_signature_card_appears_before_the_cast_and_the_cast_crosses_only_after_continue()
    {
        // THE WHOLE SEQUENCE, IN THE HOST'S FRAME ORDER: the director reads last frame's facts, the
        // card's CONTINUE is taken, the holds are written, and only then does the fight advance — and
        // not at all while a paused card holds it.
        var d = new OpeningDirector();
        d.Restore(OpeningStage.AwaitSignature);
        var replay = Replay(FreshCareerWaveTwo());
        var playhead = 0f;
        var held = false;
        var castCrossed = false;
        int? heldFrame = null, cardFrame = null, ackFrame = null, castFrame = null, nextCardFrame = null;
        var castsBeforeAck = 0;

        for (var frame = 1; frame < 1200 && nextCardFrame is null; frame++)
        {
            if (cardFrame is { } c && frame == c + 150) { d.Acknowledge(); ackFrame = frame; }   // 2.5 s of reading
            d.Update(new OpeningFacts(SignatureHeld: held, SignatureLanded: castCrossed));
            if (d.Stage == OpeningStage.IntroduceSignature) cardFrame ??= frame;
            if (d.Stage == OpeningStage.IntroduceHealth) nextCardFrame ??= frame;
            var kind = d.HoldsReplayBefore;
            if (d.HoldsFight) continue;

            var step = ReplayBarrier.Advance(replay, playhead, playhead + FrameMs, kind, _ => CastLeadMs);
            held = step.Held && kind == BattleEventKind.Skill;
            if (held) heldFrame ??= frame;
            playhead = step.PlayheadMs;
            foreach (var e in replay.Advance(playhead))
            {
                if (e.Kind != BattleEventKind.Skill) continue;
                if (ackFrame is null) castsBeforeAck++;
                castFrame ??= frame;
                castCrossed = true;
            }
        }

        Assert.NotNull(heldFrame);
        Assert.NotNull(cardFrame);
        Assert.NotNull(ackFrame);
        Assert.NotNull(castFrame);
        Assert.NotNull(nextCardFrame);
        Assert.Equal(0, castsBeforeAck);
        Assert.True(heldFrame < cardFrame, "the card is raised by the hold, never before it");
        Assert.True(cardFrame < ackFrame);
        Assert.True(ackFrame < castFrame, "the cast crossed before CONTINUE");
        // The wind-up plays between the release and the cast — roughly the lead, never zero.
        Assert.InRange(castFrame!.Value - ackFrame!.Value, (int)(CastLeadMs / FrameMs) - 2, (int)(CastLeadMs / FrameMs) + 3);
        Assert.True(castFrame < nextCardFrame, "the next card came up before the cast it follows had landed");
    }

    // ── THE CLEAR: the fall plays out, the stage settles, THEN the card, THEN the next wave. ──────

    [Fact]
    public void test_a_fall_has_played_only_when_its_clip_its_rest_and_its_fade_are_all_over()
    {
        var at = new[] { 10f };
        Assert.False(ClearBeat.FallsPlayed(10f + HuntScreen.FallSeconds - 0.01f, at, HuntScreen.FallSeconds));
        Assert.True(ClearBeat.FallsPlayed(10f + HuntScreen.FallSeconds, at, HuntScreen.FallSeconds));

        // EVERY fall, not the first: a wave of three is shown when its LAST creature has faded.
        var three = new[] { 0f, 1.5f, 3f };
        Assert.False(ClearBeat.FallsPlayed(3f + HuntScreen.FallSeconds - 0.01f, three, HuntScreen.FallSeconds));
        Assert.True(ClearBeat.FallsPlayed(3f + HuntScreen.FallSeconds, three, HuntScreen.FallSeconds));

        // AND THE BREAK ALONE IS SHORTER THAN A FALL. That is why the opening has to hold the next
        // wave: left to itself, the next pack walks in over the last creature's body.
        Assert.True(Descent.WaveBreakSeconds < HuntScreen.FallSeconds);
    }

    [Fact]
    public void test_a_held_break_rests_on_its_last_instant_and_hands_over_the_frame_it_is_let_go()
    {
        var timer = Descent.WaveBreakSeconds;
        for (var frame = 0; frame < 1000; frame++)
        {
            Assert.False(ClearBeat.Tick(ref timer, FrameS, holdNext: true));
            Assert.True(timer > 0f, "a held break must still read as a break");
        }
        Assert.True(ClearBeat.Tick(ref timer, FrameS, holdNext: false));

        // ...and an unheld one hands over exactly when it runs out, as it always did.
        var plain = Descent.WaveBreakSeconds;
        var frames = 0;
        while (!ClearBeat.Tick(ref plain, FrameS, holdNext: false)) frames++;
        Assert.InRange(frames + 1, (int)(Descent.WaveBreakSeconds / FrameS), (int)(Descent.WaveBreakSeconds / FrameS) + 1);
    }

    [Fact]
    public void test_the_gleam_card_waits_for_the_last_fall_and_the_next_wave_waits_for_the_card()
    {
        var (kill, falls, card, ack, next) = RunFirstClear(new OpeningDirector(), OpeningStage.AwaitFirstReward);

        Assert.NotNull(card);
        Assert.NotNull(ack);
        Assert.NotNull(next);
        // final EnemyDown < final visible fall done <= resource card < next wave's entrance
        Assert.True(kill < falls, "the fall finished before it began");
        Assert.True(falls <= card, "the GLEAM card came up over a creature still falling");
        Assert.True(card < next, "the next wave arrived before the GLEAM card");
        // CONTINUE and the next wave share a frame: the host takes the press, the cursor moves on, the
        // hold is lifted, and the fight advances — all in the frame the card closes. Never before it.
        Assert.True(ack <= next, "the next wave arrived while the GLEAM card was still up");
    }

    [Fact]
    public void test_without_the_hold_the_next_wave_walks_in_over_the_last_fall()
    {
        // THE NEGATIVE CONTROL. With the opening finished nothing is held, and the break hands over at
        // its own 1.1 s — while the last creature is still lying there. This is ordinary play, and it is
        // exactly what the opening's first clear must not look like.
        var (kill, falls, _, _, next) = RunFirstClear(new OpeningDirector(), OpeningStage.Complete);
        Assert.NotNull(next);
        Assert.NotNull(falls);
        Assert.True(next < falls, "control: the unheld break was expected to hand over mid-fall");
        Assert.True(kill < next);
    }

    /// <summary>
    /// The first wave's killing blow and what follows, frame by frame in the host's order: the facts of
    /// the last frame reach the director, CONTINUE is pressed after two seconds of reading, and the fight
    /// advances only while no paused card holds it.
    /// </summary>
    private static (int Kill, int? Falls, int? Card, int? Ack, int? Next) RunFirstClear(OpeningDirector d, OpeningStage from)
    {
        d.Restore(from);
        const int killFrame = 30;
        var anim = 0f;
        var diedAt = new List<float>();
        var breakTimer = 0f;
        var rewards = 0;
        var cleared = false;
        int? falls = null, card = null, ack = null, next = null;

        // Until BOTH the handover and the end of the fall are known — the control needs to compare them.
        for (var frame = 1; frame < 3000 && (next is null || falls is null); frame++)
        {
            if (card is { } c && frame == c + 120) { d.Acknowledge(); ack = frame; }
            var shown = cleared && breakTimer > 0f && ClearBeat.FallsPlayed(anim, diedAt, HuntScreen.FallSeconds);
            d.Update(new OpeningFacts(RewardsCredited: rewards, ClearShown: shown));
            if (d.Stage == OpeningStage.IntroduceResources) card ??= frame;
            if (d.HoldsFight) continue;   // a paused card: nothing on the stage moves at all

            anim += FrameS;
            if (frame == killFrame)
            {
                // The killing blow: the fall starts, the purse is paid on the same frame, the break begins.
                diedAt.Add(anim);
                rewards = 1;
                cleared = true;
                breakTimer = Descent.WaveBreakSeconds;
                continue;
            }
            if (cleared && breakTimer > 0f && ClearBeat.Tick(ref breakTimer, FrameS, d.HoldsNextWave)) next = frame;
            if (falls is null && cleared && ClearBeat.FallsPlayed(anim, diedAt, HuntScreen.FallSeconds)) falls = frame;
        }
        return (killFrame, falls, card, ack, next);
    }

    // ── THE FALL: readable, then black, the restart under the black, then the stage back. ─────────

    /// <summary>
    /// One fall, stepped exactly as HuntScreen.Update steps it: the fade-in clock decays, the downed beat
    /// counts down, the descent restarts on the frame it reaches zero and arms the fade-in, and Draw asks
    /// for the alpha after all of that. Ends on the first frame the transition is no longer up.
    /// </summary>
    private static List<(int Frame, bool Downed, float Alpha)> Fall(bool reduced)
    {
        var frames = new List<(int, bool, float)>();
        var downed = true;
        var timer = Descent.DownedSeconds;
        var fadeIn = 0f;
        for (var frame = 1; frame < 600; frame++)
        {
            fadeIn = MathF.Max(0f, fadeIn - FrameS);
            if (downed)
            {
                timer -= FrameS;
                if (timer <= 0f) { fadeIn = DeathTransition.FadeInSeconds(reduced); downed = false; }
            }
            frames.Add((frame, downed, DeathTransition.Alpha(downed, timer, fadeIn, reduced)));
            if (!DeathTransition.Up(downed, fadeIn)) break;
        }
        return frames;
    }

    /// <summary>The frame the bare countdown restarts on — `_downedTimer -= dt; if (<= 0) StartRun` and nothing else.</summary>
    private static int BareRestartFrame()
    {
        var timer = Descent.DownedSeconds;
        for (var frame = 1; ; frame++) { timer -= FrameS; if (timer <= 0f) return frame; }
    }

    [Fact]
    public void test_the_death_stays_readable_then_fades_and_is_black_on_the_frame_the_descent_restarts()
    {
        // THE FADE SITS INSIDE THE DOWNED BEAT. OfflineHunt spends Descent.DownedSeconds per fall and the
        // Dust for a checkpoint is charged on the restart frame, so the restart may not move: it lands on
        // the frame it always did, and the black is simply full by then.
        Assert.True(DeathTransition.FadeOut < Descent.DownedSeconds);

        var fall = Fall(reduced: false);
        var restart = fall.First(f => !f.Downed).Frame;
        Assert.Equal(BareRestartFrame(), restart);
        Assert.InRange(restart, (int)(Descent.DownedSeconds / FrameS), (int)(Descent.DownedSeconds / FrameS) + 1);

        // Readable for everything but the last Reward band: the clip plays and the body lies there.
        var readableFrames = (int)((Descent.DownedSeconds - DeathTransition.FadeOut) / FrameS);
        Assert.All(fall.Where(f => f.Frame < readableFrames), f => Assert.Equal(0f, f.Alpha));
        // ...then the black rises from nothing, never brightens, and is FULL on the restart frame — not
        // a frame later.
        var fading = fall.Where(f => f.Frame >= readableFrames + 1 && f.Frame < restart).ToList();
        Assert.NotEmpty(fading);
        Assert.True(fading[0].Alpha < 0.1f, "the fade started already dark: a cut, not a fade");
        for (var i = 1; i < fading.Count; i++) Assert.True(fading[i].Alpha >= fading[i - 1].Alpha, "the fade brightened");
        Assert.Equal(1f, fall.First(f => f.Frame == restart).Alpha);
    }

    [Fact]
    public void test_the_black_holds_for_the_fast_band_after_the_restart_then_lifts_over_the_reward_band()
    {
        var fall = Fall(reduced: false);
        var restart = fall.First(f => !f.Downed).Frame;
        var after = fall.Where(f => f.Frame > restart).ToList();

        // HELD, so the reset is never seen: full black for the Fast band...
        var held = (int)(DeathTransition.Hold / FrameS);
        Assert.All(after.Take(held), f => Assert.Equal(1f, f.Alpha));
        // ...then the lift, never darkening, to nothing by Fast + Reward — and the transition is over.
        var lifting = after.Skip(held).ToList();
        for (var i = 1; i < lifting.Count; i++) Assert.True(lifting[i].Alpha <= lifting[i - 1].Alpha, "the lift darkened");
        Assert.Equal(0f, lifting[^1].Alpha);
        var over = (int)Math.Ceiling((DeathTransition.Hold + DeathTransition.FadeIn) / FrameS);
        Assert.InRange(after.Count, over - 1, over + 1);
        Assert.False(DeathTransition.Up(downed: false, fadeInClock: 0f));
        // The bands are the HUNT's own; the constants name them rather than typing seconds.
        Assert.Equal(UiMotion.Reward, DeathTransition.FadeOut);
        Assert.Equal(UiMotion.Fast, DeathTransition.Hold);
        Assert.Equal(UiMotion.Reward, DeathTransition.FadeIn);
    }

    [Fact]
    public void test_reduced_motion_cuts_to_black_and_back_with_no_frame_in_between()
    {
        // Reduced Motion is the SAME end state with no travel: a cut where the fade would start, black
        // through the hold, a cut back — and the restart on exactly the same frame.
        var fall = Fall(reduced: true);
        Assert.All(fall, f => Assert.True(f.Alpha is 0f or 1f, $"an intermediate alpha under Reduced Motion: {f.Alpha}"));
        Assert.Equal(BareRestartFrame(), fall.First(f => !f.Downed).Frame);

        // 0 then 1 then 0, each once: no flicker.
        var runs = new List<float>();
        foreach (var f in fall) if (runs.Count == 0 || runs[^1] != f.Alpha) runs.Add(f.Alpha);
        Assert.Equal(new[] { 0f, 1f, 0f }, runs);
        // The cut back comes at the hold's end, not after a fade nobody is shown.
        Assert.Equal(DeathTransition.Hold, DeathTransition.FadeInSeconds(reduced: true));
        Assert.Equal(DeathTransition.Hold + DeathTransition.FadeIn, DeathTransition.FadeInSeconds(reduced: false));
    }

    [Fact]
    public void test_the_hud_refuses_exactly_while_the_black_is_visible_and_is_live_at_every_other_frame()
    {
        // "Nothing clickable paints when its input is blocked" cuts both ways: the medallion painted
        // beside a fallen Hunter for the readable beat is LIVE, and only the black blocks it — from the
        // fade-out's first visible frame to the lift's last, never a frame longer either side.
        foreach (var reduced in new[] { false, true })
        {
            var fall = Fall(reduced);
            Assert.All(fall, f => Assert.Equal(f.Alpha > 0f, DeathTransition.Covers(f.Alpha)));
            var readableFrames = (int)((Descent.DownedSeconds - DeathTransition.FadeOut) / FrameS);
            Assert.All(fall.Where(f => f.Frame < readableFrames), f => Assert.False(DeathTransition.Covers(f.Alpha), "the readable beat refused input"));
            Assert.True(DeathTransition.Covers(fall.First(f => !f.Downed).Alpha), "the restart frame is black and must refuse");
            Assert.False(DeathTransition.Covers(fall[^1].Alpha), "the stage is back and must answer");
            // ...and the transition being UP (the coach's gate) is the wider window: it starts on the fall
            // frame, before the black, so a card is never raised beside the collapse either.
            Assert.True(DeathTransition.Up(fall[0].Downed, 0f));
            Assert.False(DeathTransition.Covers(fall[0].Alpha));
        }
        // Under Reduced Motion the black is a cut, so the refusal is a cut too: the same frames, no more.
        var cut = Fall(reduced: true);
        Assert.Equal(cut.Count(f => f.Alpha == 1f), cut.Count(f => DeathTransition.Covers(f.Alpha)));
    }

    [Fact]
    public void test_the_rigs_dial_sweeps_from_the_last_readable_instant_through_the_black_to_the_stage_back()
    {
        // RH_SHOT_T for `fightfade`: five instants a reviewer can name, linear in seconds between them.
        var readable = DeathTransition.At(0f, reduced: false);
        Assert.False(readable.Restarted);
        Assert.Equal(DeathTransition.FadeOut, readable.DownedTimer, 5);
        Assert.Equal(0f, DeathTransition.Alpha(true, readable.DownedTimer, 0f, false));

        var fadingOut = DeathTransition.At(0.25f, reduced: false);
        Assert.False(fadingOut.Restarted);
        Assert.InRange(DeathTransition.Alpha(true, fadingOut.DownedTimer, 0f, false), 0.3f, 0.8f);

        var black = DeathTransition.At(0.5f, reduced: false);
        Assert.True(black.Restarted, "at the dial's middle the next descent has already begun");
        Assert.Equal(1f, DeathTransition.Alpha(false, 0f, black.FadeInClock, false));

        var fadingIn = DeathTransition.At(0.75f, reduced: false);
        Assert.True(fadingIn.Restarted);
        Assert.InRange(DeathTransition.Alpha(false, 0f, fadingIn.FadeInClock, false), 0.3f, 0.8f);

        var done = DeathTransition.At(1f, reduced: false);
        Assert.True(done.Restarted);
        Assert.Equal(0f, done.FadeInClock);
        Assert.False(DeathTransition.Up(false, done.FadeInClock));
        // Time runs one way along the dial: the new wave's entrance is further along at every step.
        Assert.True(black.SinceRestart < fadingIn.SinceRestart && fadingIn.SinceRestart < done.SinceRestart);

        // Under Reduced Motion the same instants are the cut: black where the fade would be, and the
        // stage already back where the lift would be.
        Assert.Equal(1f, DeathTransition.Alpha(true, DeathTransition.At(0.25f, true).DownedTimer, 0f, true));
        Assert.Equal(1f, DeathTransition.Alpha(false, 0f, DeathTransition.At(0.5f, true).FadeInClock, true));
        Assert.Equal(0f, DeathTransition.At(0.75f, true).FadeInClock);
        Assert.False(DeathTransition.Up(false, DeathTransition.At(0.75f, true).FadeInClock));
    }
}
