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
}
