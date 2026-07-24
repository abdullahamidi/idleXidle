using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Animation;
using Xunit;

namespace ResonanceHunter.Core.Tests.Animation;

public class RigTests
{
    private static Rig NewArm(int snapSteps = 16) => new(
        new[]
        {
            new Bone { Id = "torso", Offset = Vec2.Zero },
            new Bone { Id = "upper_arm", ParentId = "torso", Offset = new Vec2(10f, 0f) },
            new Bone { Id = "forearm", ParentId = "upper_arm", Offset = new Vec2(12f, 0f) },
        },
        snapSteps);

    [Fact]
    public void test_parents_always_resolve_before_their_children()
    {
        // Declared child-first on purpose — construction must not depend on declaration order.
        var rig = new Rig(new[]
        {
            new Bone { Id = "forearm", ParentId = "upper_arm", Offset = new Vec2(12f, 0f) },
            new Bone { Id = "upper_arm", ParentId = "torso", Offset = new Vec2(10f, 0f) },
            new Bone { Id = "torso", Offset = Vec2.Zero },
        });

        var order = rig.Evaluate(new Dictionary<string, float>(), Vec2.Zero).Select(t => t.BoneId).ToList();

        Assert.True(order.IndexOf("torso") < order.IndexOf("upper_arm"));
        Assert.True(order.IndexOf("upper_arm") < order.IndexOf("forearm"));
    }

    [Fact]
    public void test_a_cyclic_hierarchy_is_rejected_rather_than_hanging()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new Rig(new[]
        {
            new Bone { Id = "a", ParentId = "b", Offset = Vec2.Zero },
            new Bone { Id = "b", ParentId = "a", Offset = Vec2.Zero },
        }));

        Assert.Contains("cycle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_a_missing_parent_is_rejected()
        => Assert.Throws<InvalidOperationException>(() => new Rig(new[]
        {
            new Bone { Id = "hand", ParentId = "ghost", Offset = Vec2.Zero },
        }));

    /// <summary>Rotating a parent must carry its children with it — that is the whole point of a rig.</summary>
    [Fact]
    public void test_rotating_a_parent_moves_its_children()
    {
        var rig = NewArm();

        var rest = rig.Evaluate(new Dictionary<string, float>(), Vec2.Zero);
        var bent = rig.Evaluate(new Dictionary<string, float> { ["upper_arm"] = MathF.PI / 2f }, Vec2.Zero);

        var restForearm = rest.Single(t => t.BoneId == "forearm").Position;
        var bentForearm = bent.Single(t => t.BoneId == "forearm").Position;

        Assert.NotEqual(restForearm, bentForearm);

        // upper_arm sits at (10,0); rotating it 90 degrees swings the forearm's (12,0) offset to (0,12).
        Assert.Equal(10f, bentForearm.X, precision: 3);
        Assert.Equal(12f, bentForearm.Y, precision: 3);
    }

    /// <summary>Angles are quantized to the snap grid — this is what stops pixel edges crawling.</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(8)]
    [InlineData(32)]
    public void test_every_snapped_angle_lands_exactly_on_the_snap_grid(int steps)
    {
        var rig = NewArm(steps);
        var stepSize = MathF.Tau / steps;

        for (var a = -MathF.PI; a <= MathF.PI; a += 0.017f)
        {
            var snapped = rig.Snap(a);
            var quotient = snapped / stepSize;

            Assert.Equal(MathF.Round(quotient), quotient, precision: 3);
        }
    }

    /// <summary>A snapped angle never lands more than half a step from the true angle.</summary>
    [Fact]
    public void test_snapping_never_drifts_more_than_half_a_step()
    {
        var rig = NewArm(16);
        var halfStep = MathF.Tau / 16f / 2f;

        for (var a = -MathF.PI; a <= MathF.PI; a += 0.01f)
            Assert.True(MathF.Abs(rig.Snap(a) - a) <= halfStep + 1e-4f);
    }

    /// <summary>
    /// The hierarchy must compose on TRUE angles, snapping only at the render boundary.
    /// </summary>
    /// <remarks>
    /// If each bone snapped before its child inherited it, quantization error would compound down the
    /// chain and a limb's tip would visibly drift — exactly the artifact the rig exists to prevent.
    /// </remarks>
    [Fact]
    public void test_quantization_error_does_not_compound_down_the_chain()
    {
        var rig = NewArm(8); // Deliberately coarse: error would be obvious if it compounded.

        // An angle that sits awkwardly between snap steps.
        var awkward = MathF.Tau / 8f * 0.5f;

        var frame = rig.Evaluate(
            new Dictionary<string, float>
            {
                ["torso"] = awkward,
                ["upper_arm"] = awkward,
                ["forearm"] = awkward,
            },
            Vec2.Zero);

        var forearm = frame.Single(t => t.BoneId == "forearm");

        // The TRUE composed angle is the exact sum — unpolluted by any intermediate snapping.
        Assert.Equal(awkward * 3f, forearm.Angle, precision: 4);

        // And only the render-facing value is quantized.
        Assert.Equal(rig.Snap(awkward * 3f), forearm.SnappedAngle, precision: 4);
    }

    [Fact]
    public void test_a_rig_with_too_few_snap_steps_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => NewArm(snapSteps: 2));

    /// <summary>Evaluation is pure — same pose, same result, every time.</summary>
    [Fact]
    public void test_evaluation_is_deterministic()
    {
        var rig = NewArm();
        var pose = new Dictionary<string, float> { ["upper_arm"] = 0.7f, ["forearm"] = -0.3f };

        Assert.Equal(rig.Evaluate(pose, Vec2.Zero), rig.Evaluate(pose, Vec2.Zero));
    }
}
