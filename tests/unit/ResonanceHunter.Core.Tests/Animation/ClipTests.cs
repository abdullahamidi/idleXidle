using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Animation;
using ResonanceHunter.Core.Combat;
using Xunit;

namespace ResonanceHunter.Core.Tests.Animation;

public class ClipTests
{
    [Fact]
    public void test_sampling_before_the_first_key_holds_the_first_key()
    {
        var clip = Clip.AttackerStrike();
        Assert.Equal(0f, clip.Sample(-5f)["upper_arm"], precision: 4);
    }

    [Fact]
    public void test_a_non_looping_clip_holds_its_final_pose_rather_than_snapping_back()
    {
        var clip = Clip.AttackerStrike();
        var atEnd = clip.Sample(clip.DurationSeconds)["upper_arm"];
        var wayPast = clip.Sample(clip.DurationSeconds + 30f)["upper_arm"];

        Assert.Equal(atEnd, wayPast, precision: 4);
    }

    [Fact]
    public void test_a_looping_clip_wraps_cleanly()
    {
        var idle = Clip.Idle();
        var baseline = idle.Sample(0.3f)["torso"];

        // Tolerance, not decimal-place rounding. The wrapped time differs from the original by a few
        // float ULPs, and Assert.Equal(precision:) rounds — so two values 2e-9 apart can straddle a
        // rounding boundary and "differ" at 4dp. That would be a flaky test asserting nothing real.
        const float tolerance = 1e-4f;

        Assert.True(MathF.Abs(idle.Sample(0.3f + idle.DurationSeconds)["torso"] - baseline) < tolerance);

        // ...and it wraps in the other direction too, so a negative time never explodes.
        Assert.True(MathF.Abs(idle.Sample(0.3f - idle.DurationSeconds)["torso"] - baseline) < tolerance);

        // Wrapping many periods out must not accumulate drift either.
        Assert.True(MathF.Abs(idle.Sample(0.3f + idle.DurationSeconds * 10f)["torso"] - baseline) < tolerance);
    }

    [Fact]
    public void test_interpolation_actually_moves_between_keys()
    {
        var clip = Clip.AttackerStrike();

        var start = clip.Sample(0f)["upper_arm"];
        var mid = clip.Sample(0.3f)["upper_arm"];
        var drawnBack = clip.Sample(0.60f)["upper_arm"];

        Assert.NotEqual(start, mid);
        Assert.True(mid > drawnBack, "Mid-windup should be partway toward the drawn-back pose.");
    }

    /// <summary>
    /// The strike clip's windup is the player's ONLY warning, so it must not be shorter than the
    /// game-wide anti-strobe floor. This is a gameplay constraint wearing an animation costume.
    /// </summary>
    [Fact]
    public void test_the_strike_windup_respects_the_anti_strobe_floor()
    {
        var clip = Clip.AttackerStrike();

        // The windup beat runs from t=0 to the drawn-back extreme.
        var drawBackKey = clip.Tracks["upper_arm"]
            .OrderBy(k => k.Angle)   // the most negative angle is the drawn-back hold
            .First();

        var windupMs = drawBackKey.TimeSeconds * 1000f;

        // 600ms was Telegraph.WindupFloorMs, the game-wide anti-strobe floor, until the manual-combat
        // Telegraph type was deleted. The floor is an animation-readability constant, not a combat one, so
        // it lives here now — the strike windup is still the player's only warning and must clear it.
        const float windupFloorMs = 600f;

        Assert.True(
            windupMs >= windupFloorMs,
            $"Strike windup is {windupMs}ms, below the {windupFloorMs}ms anti-strobe floor. " +
            "The player cannot read it.");
    }

    /// <summary>The strike beat must be genuinely fast, or it reads as another windup.</summary>
    [Fact]
    public void test_the_strike_beat_is_much_faster_than_the_windup()
    {
        var clip = Clip.AttackerStrike();
        var keys = clip.Tracks["upper_arm"];

        var windup = keys[1].TimeSeconds - keys[0].TimeSeconds;   // 0.00 -> 0.60
        var strike = keys[2].TimeSeconds - keys[1].TimeSeconds;   // 0.60 -> 0.75

        Assert.True(strike < windup / 2f, "The strike must be visibly faster than the windup that sold it.");
    }

    /// <summary>The Attacker strike really is a wide arc — that is what makes it the worst-case spike.</summary>
    [Fact]
    public void test_the_strike_sweeps_a_genuinely_wide_arc()
    {
        var keys = Clip.AttackerStrike().Tracks["upper_arm"];
        var sweepRadians = keys.Max(k => k.Angle) - keys.Min(k => k.Angle);

        Assert.True(
            sweepRadians > 2.5f, // > ~145 degrees
            $"Sweep is only {sweepRadians * 180f / MathF.PI:F0} degrees; the spike needs the widest arc.");
    }

    /// <summary>Sampling is pure. Animation must never introduce nondeterminism into the sim.</summary>
    [Fact]
    public void test_sampling_is_deterministic()
    {
        var clip = Clip.AttackerStrike();
        Assert.Equal(clip.Sample(0.42f), clip.Sample(0.42f));
    }

    /// <summary>End-to-end: drive the rig from the clip and confirm the tip actually travels.</summary>
    [Fact]
    public void test_the_clip_drives_the_rig_through_a_real_arc()
    {
        var rig = new Rig(new[]
        {
            new Bone { Id = "torso", Offset = Vec2.Zero },
            new Bone { Id = "upper_arm", ParentId = "torso", Offset = new Vec2(10f, 0f) },
            new Bone { Id = "forearm", ParentId = "upper_arm", Offset = new Vec2(12f, 0f) },
        });

        var clip = Clip.AttackerStrike();

        var drawnBack = rig.Evaluate(clip.Sample(0.60f), Vec2.Zero).Single(t => t.BoneId == "forearm");
        var struck = rig.Evaluate(clip.Sample(0.75f), Vec2.Zero).Single(t => t.BoneId == "forearm");

        var travelled = MathF.Abs(struck.Position.X - drawnBack.Position.X)
                      + MathF.Abs(struck.Position.Y - drawnBack.Position.Y);

        Assert.True(travelled > 10f, "The strike must visibly move the limb, not just rotate it in place.");
    }
}
