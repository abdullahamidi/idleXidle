using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Game.Presentation;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE ACTION PRESENTATION CONTRACT (ADR-011, 2026-09-24): an action clip plays by its own authored timing; a
/// projectile leaves from the hand's socket, flies with momentum, and lands ON the fight's beat; the object
/// in the air is the object that was in the hand.
/// </summary>
/// <remarks>
/// The failures these stop were all measured on the Seeker's SPRAY before the slice: the enemy reacted 683 ms
/// before the knife arrived, the knife appeared in front of the torso, it was ~5x the knife in the hand, it
/// arrived at a third of its launch speed, and every clip of every champion contacted on frame 5 of 8.
/// </remarks>
public class ActionPresentationTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static ActionClipTiming SeekerThrow()
        => ActionClipTiming.Parse(File.ReadAllText(RepoFile("assets", "art", "Animations", "Roster", "seeker_projectile",
                                                            "char_seeker_projectile_strip8_512.clip.json")));

    [Fact]
    public void test_action_timing_frames_carry_their_own_durations()
    {
        var t = SeekerThrow();
        Assert.Equal(8, t.Frames);
        Assert.True(t.FrameMs.Distinct().Count() > 2, "an authored clip is not eight equal beats");
        var release = t.Markers["release"];
        Assert.True(t.FrameMs[release] < t.FrameMs[release - 1], "the release is quicker than the coil it releases");
        Assert.Equal(0, t.FrameAt(-5f));
        Assert.Equal(release, t.FrameAt(t.MarkerMs("release") + 1f));
        Assert.Equal(t.Frames - 1, t.FrameAt(t.TotalMs + 100f));
    }

    [Fact]
    public void test_action_timing_fit_shrinks_only_elastic_frames_before_the_marker()
    {
        var t = new ActionClipTiming(new[] { 100f, 100f, 150f, 50f, 100f }, new[] { true, true, false, false, true },
                                     new Dictionary<string, int> { ["release"] = 3 });
        var fitted = t.FitBefore("release", 250f);
        Assert.Equal(150f, fitted.FrameMs[2]);                 // the held coil keeps its length
        Assert.Equal(50f, fitted.FrameMs[3]);                  // the release never slows
        Assert.Equal(100f, fitted.FrameMs[4]);                 // nothing after the marker moves
        Assert.Equal(250f, fitted.MarkerMs("release"), 3);
        Assert.Same(t, t.FitBefore("release", 400f));          // already fits: untouched
    }

    [Fact]
    public void test_uniform_timing_is_the_old_five_of_eight_model()
    {
        var old = ActionClipTiming.Uniform(8, 125f, 5);
        Assert.Equal(625f, old.MarkerMs("contact"), 3);
        Assert.Equal(1000f, old.TotalMs, 3);
    }

    [Fact]
    public void test_action_timing_refuses_a_marker_outside_the_clip()
    {
        Assert.Throws<ArgumentException>(() =>
            new ActionClipTiming(new[] { 100f, 100f }, markers: new Dictionary<string, int> { ["release"] = 5 }));
    }

    [Fact]
    public void test_a_socket_maps_with_the_transform_that_drew_the_frame()
    {
        // frame 2 of a 512 strip, cropped 100 px each side and 50 px on top, drawn at 0.5 into (400, 300)
        var src = new Rectangle(2 * 512 + 100, 50, 312, 462);
        var dest = new Rectangle(400, 300, 156, 231);
        var socket = new ActionSocket(300f / 512f, 250f / 512f, 10f);
        var at = ActorSocketMap.ToArena(src, dest, flipped: false, 512, 2, socket);
        Assert.Equal(400f + (300f - 100f) * 0.5f, at.X, 2);
        Assert.Equal(300f + (250f - 50f) * 0.5f, at.Y, 2);
        var mirrored = ActorSocketMap.ToArena(src, dest, flipped: true, 512, 2, socket);
        Assert.Equal(dest.Right - (300f - 100f) * 0.5f, mirrored.X, 2);
        Assert.Equal(MathHelper.Pi - MathHelper.ToRadians(10f), ActorSocketMap.AngleInArena(socket, true), 4);
    }

    [Fact]
    public void test_a_thrown_blade_keeps_its_momentum_to_the_contact()
    {
        var from = new Vector2(760, 580);
        var to = new Vector2(1300, 740);
        const float d = 0.06f;
        Assert.Equal(from, ProjectileMotion.ThrowPosition(from, to, 30f, d, 0f));
        Assert.Equal(to, ProjectileMotion.ThrowPosition(from, to, 30f, d, 1f));
        float Speed(float u) => Vector2.Distance(ProjectileMotion.ThrowPosition(from, to, 0f, d, u + 0.005f),
                                                 ProjectileMotion.ThrowPosition(from, to, 0f, d, u - 0.005f));
        var ratio = Speed(0.995f) / Speed(0.005f);
        Assert.InRange(ratio, 0.85f, 1f);                       // the old ease-out arrived at 0.33
    }

    [Fact]
    public void test_the_fan_bows_apart_and_meets_its_line_at_both_ends()
    {
        var from = new Vector2(0, 0);
        var to = new Vector2(500, 0);
        var mid = ProjectileMotion.ThrowPosition(from, to, 40f, 0.06f, 0.5f);
        Assert.Equal(40f, MathF.Abs(mid.Y), 2);
        var heading = ProjectileMotion.ThrowHeading(from, to, 40f, 0.06f, 0f);
        Assert.True(MathF.Abs(heading.Y) > 0.1f, "the blades leave the hand at their fan angle");
    }

    [Fact]
    public void test_the_clip_is_scheduled_so_its_release_is_one_flight_before_the_beat()
    {
        var recipe = ActionRecipes.SeekerSpray;
        var t = SeekerThrow();
        const float beat = 5200f;
        var releaseMs = beat - recipe.TravelMs;
        var startMs = releaseMs - t.MarkerMs(recipe.ReleaseMarker);
        Assert.Null(ActionPerformance.Schedule(recipe, t, beat, startMs - 50f));   // too early: not yet
        var plan = ActionPerformance.Schedule(recipe, t, beat, startMs + 5f);
        Assert.NotNull(plan);
        Assert.Equal(releaseMs, plan!.Value.StartMs + plan.Value.Timing.MarkerMs(recipe.ReleaseMarker), 2);
        // late: only the elastic wait compresses, and the release still lands a flight before the beat
        var late = ActionPerformance.Schedule(recipe, t, beat, releaseMs - 200f)!.Value;
        Assert.Equal(releaseMs, late.StartMs + late.Timing.MarkerMs(recipe.ReleaseMarker), 2);
    }

    [Fact]
    public void test_the_tightest_start_keeps_the_extreme_hold_and_the_whole_flight()
    {
        // Arrange: the latest a recovering clip may hold the figure before a performed cast (HuntScreen cuts there)
        var recipe = ActionRecipes.SeekerSpray;
        var t = SeekerThrow();
        const float beat = 5200f;
        var minLead = ActionPerformance.MinLeadMs(recipe, t);

        // Act
        var plan = ActionPerformance.Schedule(recipe, t, beat, beat - minLead)!.Value;

        // Assert: the flight is whole, the rigid frames (the extreme hold) are untouched, only the elastic shrank
        Assert.Equal(beat - recipe.TravelMs, plan.StartMs + plan.Timing.MarkerMs(recipe.ReleaseMarker), 2);
        Assert.Equal(beat - minLead, plan.StartMs, 2);
        var commit = t.Markers["commit"];
        Assert.Equal(t.FrameMs[commit], plan.Timing.FrameMs[commit]);
        Assert.True(minLead < recipe.TravelMs + t.MarkerMs(recipe.ReleaseMarker));
    }

    private sealed class Stage : IActionStage
    {
        public bool TryActorFrame(string clipKey, int frame, out SpriteFrame drawn, out int frameSize)
        {
            drawn = new SpriteFrame(null!, new Rectangle(frame * 512, 0, 512, 512), new Rectangle(420, 451, 400, 400), SpriteEffects.None);
            frameSize = 512;
            return true;
        }

        public bool TryTargetBody(int slot, out Rectangle body)
        {
            body = new Rectangle(1200 + slot * 90, 640, 120, 160);
            return true;
        }

        public Texture2D? Texture(string key) => null;
        public float CasterHeight => 412f;
    }

    [Fact]
    public void test_the_performance_releases_at_its_marker_and_contacts_on_the_beat()
    {
        var recipe = ActionRecipes.SeekerSpray;
        var t = SeekerThrow();
        const float beat = 5200f;
        var plan = ActionPerformance.Schedule(recipe, t, beat, beat - recipe.TravelMs - t.MarkerMs(recipe.ReleaseMarker))!.Value;
        var p = new ActionPerformance(recipe, plan.Timing, beat, plan.StartMs, 1, new[] { 0, 1, 2, 3 }, Color.Red);
        var stage = new Stage();
        float? releasedAt = null, contactedAt = null;
        for (var ph = plan.StartMs; ph <= beat + 400f; ph += 1000f / 60f)
        {
            var step = p.Update(ph, 1f / 60f, stage);
            if (step.Released) { Assert.Null(releasedAt); releasedAt = ph; }
            if (step.Contacted) { Assert.Null(contactedAt); contactedAt = ph; }
        }
        Assert.NotNull(releasedAt);
        Assert.NotNull(contactedAt);
        Assert.InRange(releasedAt!.Value, p.ReleaseMs, p.ReleaseMs + 1000f / 60f);
        Assert.InRange(contactedAt!.Value, beat, beat + 1000f / 60f);   // the frame the fight's hits are crossed
        Assert.True(p.IsBeat(5200) && !p.IsBeat(5217));
    }

    private static List<(float At, PerformanceStep Step)> Perform(int[] targets)
    {
        var recipe = ActionRecipes.SeekerSpray;
        var t = SeekerThrow();
        const float beat = 5200f;
        var plan = ActionPerformance.Schedule(recipe, t, beat, beat - recipe.TravelMs - t.MarkerMs(recipe.ReleaseMarker))!.Value;
        var p = new ActionPerformance(recipe, plan.Timing, beat, plan.StartMs, 1, targets, Color.Red);
        var stage = new Stage();
        var steps = new List<(float, PerformanceStep)>();
        for (var ph = plan.StartMs; ph <= beat + 400f; ph += 1000f / 60f)
            steps.Add((ph, p.Update(ph, 1f / 60f, stage)));
        return steps;
    }

    [Fact]
    public void test_a_five_blade_fan_is_heard_as_one_contact_and_two_outer_ticks()
    {
        // Arrange / Act: the whole pack struck (the stage lays the bodies out left to right, 90 px apart)
        var steps = Perform(new[] { 0, 1, 2, 3, 4 });

        // Assert: ONE contact, then at most the recipe's two ticks, after it, at the OUTERMOST blades
        var contact = Assert.Single(steps, s => s.Step.Contacted);
        var ticks = steps.Where(s => s.Step.Tick is not null).ToList();
        Assert.Equal(ActionRecipes.SeekerSpray.ContactTicks, ticks.Count);
        Assert.All(ticks, k => Assert.True(k.At > contact.At));
        var xs = ticks.Select(k => k.Step.Tick!.Value.X).OrderBy(x => x).ToArray();
        Assert.True(xs[0] < contact.Step.ContactAt.X && xs[1] > contact.Step.ContactAt.X, "one tick on each flank");
    }

    [Fact]
    public void test_a_narrow_fan_has_no_ticks()
    {
        var steps = Perform(new[] { 0, 1 });
        Assert.Single(steps, s => s.Step.Contacted);
        Assert.DoesNotContain(steps, s => s.Step.Tick is not null);
    }

    [Fact]
    public void test_other_sounds_duck_only_from_the_release_to_the_contact_ring()
    {
        // Arrange
        var recipe = ActionRecipes.SeekerSpray;
        var t = SeekerThrow();
        const float beat = 5200f;
        var plan = ActionPerformance.Schedule(recipe, t, beat, beat - recipe.TravelMs - t.MarkerMs(recipe.ReleaseMarker))!.Value;
        var p = new ActionPerformance(recipe, plan.Timing, beat, plan.StartMs, 1, new[] { 0 }, Color.Red);

        // Act / Assert: a duck, never a mute, and only inside the window
        Assert.InRange(recipe.DuckOthers, 0.2f, 0.9f);
        Assert.Equal(1f, p.DuckAt(p.ReleaseMs - 1f));
        Assert.Equal(recipe.DuckOthers, p.DuckAt(p.ReleaseMs));
        Assert.Equal(recipe.DuckOthers, p.DuckAt(beat + recipe.DuckTailMs - 1f));
        Assert.Equal(1f, p.DuckAt(beat + recipe.DuckTailMs));
    }

    [Fact]
    public void test_one_blade_per_enemy_the_cast_actually_strikes()
    {
        var cast = new BattleEvent(BattleEventKind.Skill, 1, 0, 5200);
        var events = new List<BattleEvent>
        {
            new(BattleEventKind.Strike, 0, 140, 3700),
            cast,
            new(BattleEventKind.Strike, 0, 248, 5200, IdleXIdle.Core.Builds.HitSource.Primary),
            new(BattleEventKind.Strike, 2, 238, 5200, IdleXIdle.Core.Builds.HitSource.Primary),
            new(BattleEventKind.Strike, 2, 238, 5200, IdleXIdle.Core.Builds.HitSource.Primary),   // a second blow on one enemy
            new(BattleEventKind.EnemyDown, 2, 0, 5200),
            new(BattleEventKind.Beat, 0, 4, 5200),
            new(BattleEventKind.Strike, 3, 150, 5200),                                                   // after the beat: not the cast's
        };
        Assert.Equal(new[] { 0, 2 }, ActionTargets.StruckBy(events, cast));
    }

    [Fact]
    public void test_the_seeker_throws_the_object_his_hand_holds()
    {
        var r = ActionRecipes.SeekerSpray;
        Assert.Equal(r.PropKey, r.Look.MaterialKey);          // the same texture in the hand and in the air: 1:1
        Assert.Equal(r.PropEdgeKey, r.Look.HeadKey);
        foreach (var key in new[] { r.PropKey, r.PropEdgeKey })
            Assert.True(File.Exists(RepoFile("assets", "art", "Props", key + ".png")), $"{key} is missing");
        Assert.InRange(r.TravelMs, 220f, 280f);                // the approved flight
        Assert.InRange(r.Departure, 0f, 0.1f);                 // near-constant velocity
        Assert.True(r.ReplacesGenericHit && r.CalloutAtRelease);
    }

    [Fact]
    public void test_the_seeker_throw_authors_its_hand_on_the_release_frame()
    {
        var t = SeekerThrow();
        var release = t.Markers["release"];
        Assert.NotNull(t.Socket(release, "ThrowHand"));
        Assert.Contains(release - 1, t.FramesWithSocket("ThrowHand"));   // the coil the smear sweeps from
        Assert.True(t.Elastic[0] && !t.Elastic[release], "the wait stretches with tempo; the release never does");
    }

    [Fact]
    public void test_the_seeker_throw_holds_its_follow_through_until_the_knives_land()
    {
        // Arrange
        var t = SeekerThrow();
        var release = t.Markers["release"];

        // Act: the release frame plus the follow-through frame, from the moment the knives leave
        var reaching = t.FrameMs[release] + t.FrameMs[release + 1];

        // Assert: the open hand still reaches toward the pack on the contact frame, and the recovery begins within
        // two 60 fps frames after it (not before it: the arm was down when the blades hit; not on it: it dropped AT the hit)
        var travel = ActionRecipes.SeekerSpray.TravelMs;
        Assert.InRange(reaching, travel + 1000f / 60f, travel + 2 * 1000f / 60f);
    }

    [Fact]
    public void test_a_thrown_blades_flash_peaks_on_the_contact_and_is_shorter_than_a_swings()
    {
        // Arrange: the recipe the fight's flash takes for a performed hit, and the defaults every other hit keeps
        var spray = ActionRecipes.SeekerSpray;
        var usual = new ProjectileActionRecipe { Id = "x", ClipKey = "x", PropKey = "x", PropEdgeKey = "x", Look = spray.Look };

        // Assert: no swell (the blade has arrived), a shorter life, a reduced peak; a swing's flash is unchanged
        Assert.Equal(0f, spray.TargetFlashRise);
        Assert.True(spray.TargetFlashMs < usual.TargetFlashMs);
        Assert.InRange(spray.TargetFlash, 0.2f, 0.6f);
        Assert.Equal((1f, 0.2f, 200f), (usual.TargetFlash, usual.TargetFlashRise, usual.TargetFlashMs));
    }

    [Fact]
    public void test_only_listed_actions_are_performed()
    {
        if (!ActionRecipes.Enabled) return;                    // RH_ACTION_RECIPES=0 in the environment
        Assert.Same(ActionRecipes.SeekerSpray, ActionRecipes.For("seeker", "projectile"));
        Assert.Same(ActionRecipes.SeekerHardHands, ActionRecipes.For("seeker", "strike"));
        Assert.Null(ActionRecipes.For("seeker", "attack"));    // the basic swing plays as it always has
        Assert.Null(ActionRecipes.For("anvil", "projectile"));
    }
}
