using System.Collections.Generic;
using ResonanceHunter.Core.Animation;

namespace ResonanceHunter.Client;

/// <summary>
/// One rig part: the texture drawn for a bone, plus where that texture pivots.
/// </summary>
/// <param name="Id">Bone id (matches the Core <see cref="Bone"/>).</param>
/// <param name="Parent">Parent bone id, or null for the root.</param>
/// <param name="TextureKey">AssetLibrary key (the part PNG's basename).</param>
/// <param name="Offset">Attach point in the PARENT's local space at rest — i.e. the parent's child-joint
/// pixel minus the parent's pivot pixel. For the root this is zero (its pivot sits at the root position).</param>
/// <param name="Pivot">The proximal joint in THIS part's own pixels — handed to SpriteBatch as the draw
/// origin, and the point the part rotates about.</param>
/// <param name="DrawOrder">Back-to-front paint order (independent of the parent→child transform order).</param>
public sealed record HunterPart(
    string Id,
    string? Parent,
    string TextureKey,
    Vec2 Offset,
    Vec2 Pivot,
    int DrawOrder);

/// <summary>
/// The player Hunter as a homebrew cutout rig (batch-1 art, ADR-002 / Option B).
///
/// <para>Hand-authored here (v1) rather than loaded from the delivered <c>hunter_rig.json</c>, whose
/// coordinates were unusable. All rest angles are 0: each part PNG is drawn in its natural rest
/// orientation, so the renderer draws it unrotated at rest and only rotates it for poses. Coordinates are
/// in authored part pixels; the whole assembled figure is fit-scaled to the champion box at draw time.</para>
///
/// <para>These numbers are the tuning surface — verified by eye against the assembled preview
/// (RH_SHOT_MODE=rig). Once locked they can be externalised back to a corrected JSON.</para>
/// </summary>
public static class HunterRig
{
    public static readonly IReadOnlyList<HunterPart> Parts = new[]
    {
        //             id                parent            textureKey                     offset(parent-local)  pivot(part px)   draw
        new HunterPart("pelvis",         null,             "hunter_part_pelvis",          new Vec2(0, 0),       new Vec2(99, 100),  7),
        new HunterPart("torso",          "pelvis",         "hunter_part_torso",           new Vec2(0, -90),     new Vec2(82, 220),  8),
        new HunterPart("head",           "torso",          "hunter_part_head",            new Vec2(0, -212),    new Vec2(65, 198), 12),
        new HunterPart("cloak",          "torso",          "hunter_part_cloak",           new Vec2(0, -205),    new Vec2(138, 20),  0),
        new HunterPart("arm_upper_main", "torso",          "hunter_part_arm_upper_main",  new Vec2(68, -185),   new Vec2(60, 20),  13),
        new HunterPart("arm_fore_main",  "arm_upper_main", "hunter_part_arm_fore_main",   new Vec2(0, 250),     new Vec2(56, 15),  14),
        new HunterPart("hand_main",      "arm_fore_main",  "hunter_part_hand_main",       new Vec2(0, 140),     new Vec2(45, 12),  15),
        new HunterPart("arm_upper_off",  "torso",          "hunter_part_arm_upper_off",   new Vec2(-68, -185),  new Vec2(110, 20),  1),
        new HunterPart("arm_fore_off",   "arm_upper_off",  "hunter_part_arm_fore_off",    new Vec2(0, 155),     new Vec2(38, 15),   2),
        new HunterPart("hand_off",       "arm_fore_off",   "hunter_part_hand_off",        new Vec2(0, 200),     new Vec2(38, 12),   3),
        new HunterPart("leg_thigh_main", "pelvis",         "hunter_part_leg_thigh_main",  new Vec2(31, 50),     new Vec2(59, 15),   9),
        new HunterPart("leg_shin_main",  "leg_thigh_main", "hunter_part_leg_shin_main",   new Vec2(0, 165),     new Vec2(45, 15),  10),
        new HunterPart("foot_main",      "leg_shin_main",  "hunter_part_foot_main",       new Vec2(0, 325),     new Vec2(45, 25),  11),
        new HunterPart("leg_thigh_off",  "pelvis",         "hunter_part_leg_thigh_off",   new Vec2(-31, 50),    new Vec2(50, 15),   4),
        new HunterPart("leg_shin_off",   "leg_thigh_off",  "hunter_part_leg_shin_off",    new Vec2(0, 145),     new Vec2(45, 15),   5),
        new HunterPart("foot_off",       "leg_shin_off",   "hunter_part_foot_off",        new Vec2(0, 195),     new Vec2(45, 25),   6),
    };

    /// <summary>Build the Core geometry rig (no textures) from the part table.</summary>
    public static Rig BuildRig()
    {
        var bones = new List<Bone>();
        foreach (var p in Parts)
            bones.Add(new Bone { Id = p.Id, ParentId = p.Parent, Offset = p.Offset });
        return new Rig(bones);
    }
}
