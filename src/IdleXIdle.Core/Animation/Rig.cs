using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Animation;

/// <summary>A 2D point. Core cannot use Microsoft.Xna.Framework.Vector2 (ADR-001), so it owns this.</summary>
public readonly record struct Vec2(float X, float Y)
{
    public static Vec2 Zero => new(0f, 0f);
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);

    /// <summary>Rotate about the origin.</summary>
    public Vec2 Rotate(float radians)
    {
        var (sin, cos) = ((float)Math.Sin(radians), (float)Math.Cos(radians));
        return new Vec2(X * cos - Y * sin, X * sin + Y * cos);
    }
}

/// <summary>One cutout piece: a sprite that rotates about a pivot, optionally parented to another.</summary>
public sealed class Bone
{
    public required string Id { get; init; }

    /// <summary>Parent bone id, or null for a root. Children inherit the parent's transform.</summary>
    public string? ParentId { get; init; }

    /// <summary>Where this bone attaches, in the PARENT's local space (or world space if root).</summary>
    public required Vec2 Offset { get; init; }

    /// <summary>Resting rotation, radians.</summary>
    public float RestAngle { get; init; }
}

/// <summary>A bone's resolved world transform for one frame.</summary>
public readonly record struct BoneTransform(string BoneId, Vec2 Position, float Angle)
{
    /// <summary>
    /// The angle actually handed to the renderer, quantized to the rig's snap steps.
    /// </summary>
    /// <remarks>
    /// Rotating pixel art by an arbitrary angle resamples the source grid, which destroys the hard
    /// 1px edges the whole art direction depends on (art-bible: legibility rests on flat fills with
    /// hard boundaries). Worse, a *continuously* varying angle makes those edges crawl and shimmer
    /// frame to frame. Snapping to a fixed set of angles means each pose reuses the same handful of
    /// rasterizations, so edges stay stable — the classic cutout-animation trick.
    /// </remarks>
    public float SnappedAngle { get; init; } = Angle;
}

/// <summary>
/// A homebrew cutout animation rig.
///
/// MonoGame ships no animation system, so this is the project's highest technical risk
/// (design/gdd/systems-index.md, High-Risk Systems). It exists to be spiked against the Attacker's
/// strike arc — the widest motion arc in the roster, where rotation artifacts on pixel art show worst
/// — BEFORE the creature roster is committed to it.
///
/// It is deliberately tiny: a bone hierarchy, keyframed angles, and angle snapping. No IK, no
/// blending, no skinning. Cutout animation does not need them, and every feature added here is one
/// that has to survive contact with pixel art.
/// </summary>
public sealed class Rig
{
    private readonly Dictionary<string, Bone> _bones;
    private readonly List<string> _evaluationOrder;

    /// <summary>
    /// How many discrete angles a full turn is divided into. 16 => 22.5 degree steps.
    /// </summary>
    /// <remarks>
    /// The core trade-off of the whole rig. Too few steps and motion looks robotic and stepped; too
    /// many and each bone needs more pre-rasterized variants, and the edge-crawl that snapping exists
    /// to prevent creeps back in. 16 is the starting hypothesis. THIS IS THE NUMBER THE SPIKE EXISTS
    /// TO VALIDATE — it must be judged by eye on the Attacker strike arc, not argued about on paper.
    /// </remarks>
    public int SnapSteps { get; }

    public Rig(IEnumerable<Bone> bones, int snapSteps = 16)
    {
        ArgumentNullException.ThrowIfNull(bones);
        if (snapSteps < 4)
            throw new ArgumentOutOfRangeException(nameof(snapSteps), "Fewer than 4 steps is not animation.");

        _bones = bones.ToDictionary(b => b.Id);
        SnapSteps = snapSteps;
        _evaluationOrder = TopologicalOrder();
    }

    public IReadOnlyCollection<Bone> Bones => _bones.Values;

    /// <summary>Parents must resolve before their children, so the hierarchy composes correctly.</summary>
    private List<string> TopologicalOrder()
    {
        var ordered = new List<string>();
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id))
                throw new InvalidOperationException($"Bone hierarchy contains a cycle at '{id}'.");

            var bone = _bones[id];
            if (bone.ParentId is { } parent)
            {
                if (!_bones.ContainsKey(parent))
                    throw new InvalidOperationException($"Bone '{id}' names a missing parent '{parent}'.");
                Visit(parent);
            }

            visiting.Remove(id);
            visited.Add(id);
            ordered.Add(id);
        }

        foreach (var id in _bones.Keys) Visit(id);
        return ordered;
    }

    /// <summary>Quantize an angle to the nearest snap step.</summary>
    public float Snap(float radians)
    {
        var step = MathF.Tau / SnapSteps;
        return MathF.Round(radians / step) * step;
    }

    /// <summary>
    /// Resolve every bone's world transform for a pose.
    /// </summary>
    /// <param name="pose">Per-bone angle deltas from rest, in radians. Missing bones use their rest angle.</param>
    /// <param name="rootPosition">Where the rig sits in the world.</param>
    public IReadOnlyList<BoneTransform> Evaluate(IReadOnlyDictionary<string, float> pose, Vec2 rootPosition)
    {
        ArgumentNullException.ThrowIfNull(pose);

        var resolved = new Dictionary<string, BoneTransform>();
        var results = new List<BoneTransform>(_evaluationOrder.Count);

        foreach (var id in _evaluationOrder)
        {
            var bone = _bones[id];
            var localAngle = bone.RestAngle + (pose.TryGetValue(id, out var delta) ? delta : 0f);

            Vec2 position;
            float worldAngle;

            if (bone.ParentId is { } parentId)
            {
                var parent = resolved[parentId];
                // The child's offset rotates with the parent — that is what makes it a hierarchy.
                position = parent.Position + bone.Offset.Rotate(parent.Angle);
                worldAngle = parent.Angle + localAngle;
            }
            else
            {
                position = rootPosition + bone.Offset;
                worldAngle = localAngle;
            }

            // NOTE: the hierarchy composes on the UNSNAPPED angle, and only the value handed to the
            // renderer is snapped. Snapping mid-hierarchy would compound quantization error down the
            // chain — a 3-bone limb would drift visibly at its tip, which is exactly the artifact this
            // rig is supposed to prevent.
            var transform = new BoneTransform(id, position, worldAngle) { SnappedAngle = Snap(worldAngle) };

            resolved[id] = transform;
            results.Add(transform);
        }

        return results;
    }
}
