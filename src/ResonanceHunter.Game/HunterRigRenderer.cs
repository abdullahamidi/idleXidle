using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Animation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Client;

/// <summary>
/// Where a gear slot hangs on the skeleton, and how big it draws relative to the bone it rides.
/// </summary>
/// <param name="Bone">Bone id from <see cref="HunterRig"/> whose transform this part inherits.</param>
/// <param name="PivotU">Pivot X as a fraction of the gear texture's own width.</param>
/// <param name="PivotV">Pivot Y as a fraction of the gear texture's own height.</param>
/// <param name="Height">Drawn height in RIG-NATIVE pixels (the same space as the part table).</param>
/// <param name="DrawOrder">Back-to-front paint order, interleaved with the body parts.</param>
/// <param name="Rotation">
/// Extra rotation in radians, added to the bone's angle. Item art is drawn on the diagonal for an
/// inventory grid, which reads as a sword swept up across the chest once it is pinned to a hand —
/// this rotates it back to a natural carry.
/// </param>
public readonly record struct GearBinding(string Bone, float PivotU, float PivotV, float Height,
                                          int DrawOrder, float Rotation = 0f);

/// <summary>
/// Assembles the Hunter cutout rig — body parts plus equipped gear — and draws it into a box.
/// </summary>
/// <remarks>
/// <para>
/// Extracted so the dev preview and the battle screen share ONE assembly path. They had diverged:
/// the preview assembled the rig while the battle screen drew a flat <c>hunter_idle</c> sprite, which
/// is why equipment had nowhere to attach.
/// </para>
/// <para>
/// Gear binds to BONES, not to the champion box. A bone's transform already carries the animation, so a
/// helm bound to <c>head</c> or a blade bound to <c>hand_main</c> follows the pose for free — that is
/// the whole reason for doing it this way rather than at fixed offsets. Swapping a part is then just
/// swapping the texture the binding resolves to.
/// </para>
/// </remarks>
public sealed class HunterRigRenderer
{
    private readonly UiKit _ui;
    private readonly Rig _rig = HunterRig.BuildRig();
    private readonly Dictionary<string, HunterPart> _byId = HunterRig.Parts.ToDictionary(p => p.Id);

    /// <summary>
    /// Gear bindings. Pivots are fractions of the gear texture so art of any size can be dropped in
    /// without re-deriving pixel pivots; heights are rig-native so gear scales with the body.
    /// </summary>
    /// <remarks>
    /// Draw orders interleave with <see cref="HunterRig.Parts"/>. The body occupies 0..17 (the shoulder
    /// caps are 16/17 and the elbow caps 18/19), so gear painting over a limb sits above that —
    /// boots 18, gloves 20, weapon 21. Boots at 11 tied with foot_main's own order, so the body's
    /// leather boot sometimes painted over the worn one.
    /// Helm at 13 still paints over the head (12) while the off-hand arm (1..3) passes behind the torso.
    /// </remarks>
    private static readonly Dictionary<GearSlot, GearBinding> Bindings = new()
    {
        // Heights are calibrated against the assembled figure's NATIVE size (~476 tall) and the part
        // each piece covers, measured off hunter_rig_base: figure 512 tall, head 210x168, torso 103x90,
        // hand 54x42, shin+foot 112 tall. Re-tuning these is the cost of changing the rig source — the numbers are
        // rig pixels, not fractions, so a figure with different proportions needs new ones.
        // Pivot V sits low in the texture so a piece hangs DOWN from its joint, the way a helm sits on
        // a neck and a blade hangs from a grip.
        // Boots bind to the FOOT with a tall shaft that rises past the knee, rather than to the shin.
        // Both were tried: at 62px on the foot the piece covered the toe only and read as a bootie
        // pulled over the character's own boot; bound to the shin it covered the shaft but the foot
        // juts ~18px forward of the shin bone, so the body's leather toe kept showing beyond it. The
        // foot bone carries that forward offset for free, and a boot IS attached to a foot.
        [GearSlot.Boots] = new("foot_main", 0.50f, 0.62f, 142f, 18),
        [GearSlot.Chest] = new("torso", 0.50f, 0.80f, 120f, 9),
        [GearSlot.Gloves] = new("hand_main", 0.50f, 0.26f, 52f, 20),
        [GearSlot.Helm] = new("head", 0.50f, 0.84f, 168f, 13),
        // gear_weapon_* is authored VERTICAL with the hilt at the bottom, so the pivot sits low in
        // the texture (the grip).
        //
        // The tilt was SWEPT against real captures, not derived — two poses have to be right at once and
        // they pull against each other. At rest the blade must hang clear of the body; at the strike
        // apex, where the hand has rotated ~150 degrees, it must point at the enemy rather than back
        // across the character's own face. -0.30 satisfied only the first, and the sword swept through
        // the helmet on every attack. -2.10 lays the blade alongside the leg at rest and level at the
        // target on impact.
        [GearSlot.Weapon] = new("hand_main", 0.50f, 0.87f, 160f, 21, -2.10f),
    };

    /// <summary>Boots and gloves are worn on both limbs; the mirror bone draws the same texture.</summary>
    private static readonly Dictionary<GearSlot, string> MirrorBone = new()
    {
        [GearSlot.Boots] = "foot_off",
        [GearSlot.Gloves] = "hand_off",
    };

    /// <summary>
    /// Clip track name -> rig bone id.
    /// </summary>
    /// <remarks>
    /// The clips in <see cref="Clip"/> are authored against generic limb names ("upper_arm") so one clip
    /// can drive any creature, while the Hunter rig names bones per side ("arm_upper_main"). Without
    /// this remap every track silently misses and the figure animates not at all.
    /// </remarks>
    private static readonly Dictionary<string, string> TrackToBone = new()
    {
        ["torso"] = "torso",
        ["head"] = "head",
        ["upper_arm"] = "arm_upper_main",
        ["forearm"] = "arm_fore_main",
        ["hand"] = "hand_main",
        ["upper_arm_off"] = "arm_upper_off",
        ["forearm_off"] = "arm_fore_off",
        ["thigh"] = "leg_thigh_main",
        ["shin"] = "leg_shin_main",
    };

    /// <summary>Translate a sampled clip pose into bone-keyed angles this rig understands.</summary>
    /// <remarks>
    /// Angles are NEGATED. <see cref="Rig"/> rotates counter-clockwise in maths convention, but screen
    /// Y points down, so a limb hanging as (0, +1) rotated by t becomes (-sin t, cos t) — swinging it
    /// FORWARD (towards +X, the enemy) needs a NEGATIVE angle. The clips are authored the other way
    /// round: <c>AttackerStrike</c> calls -1.20 "drawn back" and +1.75 the strike. Played as written the
    /// swing therefore ran backwards — the wind-up threw the arm forward and the strike pulled it back.
    /// Flipping here keeps the clips readable and shared with other creatures.
    /// </remarks>
    public static Dictionary<string, float> MapPose(IReadOnlyDictionary<string, float> sampled)
    {
        var pose = new Dictionary<string, float>();
        foreach (var (track, angle) in sampled)
            pose[TrackToBone.TryGetValue(track, out var bone) ? bone : track] = -angle;
        return pose;
    }

    /// <summary>A slumped collapse, posed on the rig so gear stays on the body as the champion falls.</summary>
    public static readonly Dictionary<string, float> DeathPose = new()
    {
        // Deliberately shallow. A cutout limb is a flat rectangle pinned at one end, so past roughly
        // 0.4 rad the pieces visibly separate — an earlier attempt at a real collapse (torso 0.55,
        // knees 1.0) tore the figure apart. This reads as a beaten slump instead of a fall, which is
        // the most a rectangular cutout will honestly carry.
        ["torso"] = 0.22f,
        ["head"] = 0.20f,
        ["arm_upper_main"] = 0.34f,
        ["arm_fore_main"] = 0.22f,
        ["arm_upper_off"] = -0.30f,
        ["arm_fore_off"] = -0.20f,
        ["leg_thigh_main"] = -0.16f,
        ["leg_shin_main"] = 0.24f,
        ["leg_thigh_off"] = -0.10f,
        ["leg_shin_off"] = 0.18f,
    };

    private static readonly Dictionary<string, float> RestPose =
        HunterRig.Parts.Where(p => p.RestAngle != 0f).ToDictionary(p => p.Id, p => p.RestAngle);

    private (float MinX, float MinY, float MaxX, float MaxY, int Resolved)? _restBounds;

    /// <summary>Clip angles added on top of the parts' rest angles (see <see cref="HunterPart.RestAngle"/>).</summary>
    private static Dictionary<string, float> Compose(IReadOnlyDictionary<string, float>? pose)
    {
        var merged = new Dictionary<string, float>(RestPose);
        if (pose is not null)
            foreach (var (bone, angle) in pose)
                merged[bone] = merged.TryGetValue(bone, out var rest) ? rest + angle : angle;
        return merged;
    }

    /// <summary>The assembled figure's native bounds at rest, computed once.</summary>
    private (float MinX, float MinY, float MaxX, float MaxY, int Resolved) RestBounds()
    {
        if (_restBounds is { } cached) return cached;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        var resolved = 0;
        foreach (var bt in _rig.Evaluate(RestPose, Vec2.Zero))
        {
            if (!_byId.TryGetValue(bt.BoneId, out var p)) continue;
            var tex = _ui.Assets.Get(p.TextureKey);
            if (tex is not null) resolved++;
            float w = tex?.Width ?? 40, h = tex?.Height ?? 40;
            float l = bt.Position.X - p.Pivot.X, t = bt.Position.Y - p.Pivot.Y;
            minX = MathF.Min(minX, l); minY = MathF.Min(minY, t);
            maxX = MathF.Max(maxX, l + w); maxY = MathF.Max(maxY, t + h);
        }
        var r = (minX, minY, maxX, maxY, resolved);
        if (resolved > 0) _restBounds = r;
        return r;
    }

    public HunterRigRenderer(UiKit ui) => _ui = ui;

    /// <summary>Joint dots, for tuning pivots in the dev preview.</summary>
    public bool ShowMarkers;

    /// <summary>
    /// Draw the assembled hunter into <paramref name="box"/>.
    /// </summary>
    /// <param name="pose">Per-bone angle deltas, e.g. from <c>Clip.Sample(t)</c>. Empty = rest pose.</param>
    /// <param name="hunter">Supplies equipped gear; null draws the bare body.</param>
    /// <param name="groundToBottom">
    /// Sit the figure's lowest pixel on <c>box.Bottom</c> rather than centring it — actors stand on a
    /// ground line, so the battle screen always wants this.
    /// </param>
    /// <returns>false if not one body part resolved, so callers can fall back to the flat sprite.</returns>
    public bool Draw(SpriteBatch b, Rectangle box, IReadOnlyDictionary<string, float>? pose,
                     Hunter? hunter, Color tint, bool groundToBottom = true)
    {
        var frame = _rig.Evaluate(Compose(pose), Vec2.Zero);

        // Bounds come from the REST pose, not from this frame. Measuring the current frame made the
        // figure rescale and shift on every animation frame — a swing widens the bounds, the fit
        // shrinks, and the whole character breathes in and out mid-attack. A character's footprint is
        // a property of the character, not of what it is doing.
        var (minX, minY, maxX, maxY, resolved) = RestBounds();
        if (resolved == 0) return false;

        var bw = MathF.Max(1, maxX - minX);
        var bh = MathF.Max(1, maxY - minY);
        var fit = MathF.Min(box.Width / bw, box.Height / bh);
        var boundsMin = new Vector2(minX, minY);
        var origin = new Vector2(
            box.X + (box.Width - bw * fit) / 2f,
            groundToBottom ? box.Bottom - bh * fit : box.Y + (box.Height - bh * fit) / 2f);

        Vector2 ToScreen(Vec2 v) => origin + (new Vector2(v.X, v.Y) - boundsMin) * fit;

        // One paint list for body and gear so they interleave by DrawOrder instead of gear always
        // sitting on top — otherwise a weapon in the off hand would draw over the torso.
        var paint = new List<(int Order, Action Draw)>();

        foreach (var bt in frame)
        {
            if (!_byId.TryGetValue(bt.BoneId, out var p)) continue;
            var tex = _ui.Assets.Get(p.TextureKey);
            if (tex is null) continue;
            var pos = ToScreen(bt.Position);
            var angle = bt.Angle;
            paint.Add((p.DrawOrder, () => b.Draw(tex, pos, null, tint, angle,
                new Vector2(p.Pivot.X, p.Pivot.Y), fit, SpriteEffects.None, 0f)));
        }

        if (hunter is not null)
            foreach (var (slot, bind) in Bindings)
            {
                if (hunter.Worn(slot) is not { } item) continue;
                var tex = GearTexture(slot, item);
                if (tex is null || tex.Height <= 0) continue;

                foreach (var boneId in BonesFor(slot, bind))
                {
                    var bt = frame.FirstOrDefault(t => t.BoneId == boneId);
                    if (bt.BoneId is null) continue;
                    var pos = ToScreen(bt.Position);
                    var angle = bt.Angle;
                    // Sized and pivoted against the art's CONTENT, not its canvas. Height is how tall the
                    // visible piece should be in rig pixels, and the pivot fractions address the content
                    // box — so a generated piece carrying 6% empty canvas and one carrying 18% both land
                    // in the same place, which canvas-relative numbers could never do.
                    var pad = _ui.ContentPad(tex);
                    var contentH = MathF.Max(1f, tex.Height * (1f - pad.Y - pad.W));
                    var contentW = MathF.Max(1f, tex.Width * (1f - pad.X - pad.Z));
                    var gearScale = bind.Height / contentH * fit;
                    var pivot = new Vector2(tex.Width * pad.X + contentW * bind.PivotU,
                                            tex.Height * pad.Y + contentH * bind.PivotV);
                    var texRef = tex;
                    paint.Add((bind.DrawOrder, () => b.Draw(texRef, pos, null, tint, angle + bind.Rotation,
                        pivot, gearScale, SpriteEffects.None, 0f)));
                }
            }

        foreach (var (_, draw) in paint.OrderBy(e => e.Order)) draw();

        if (ShowMarkers)
            foreach (var bt in frame)
            {
                var pos = ToScreen(bt.Position);
                _ui.Fill(b, new Rectangle((int)pos.X - 3, (int)pos.Y - 3, 6, 6), new Color(0xF0, 0xA8, 0x30));
            }
        return true;
    }

    private static IEnumerable<string> BonesFor(GearSlot slot, GearBinding bind)
    {
        yield return bind.Bone;
        if (MirrorBone.TryGetValue(slot, out var mirror)) yield return mirror;
    }

    /// <summary>
    /// Resolve a slot's art: the trait-specific rig part, else a generic one, else the inventory icon.
    /// </summary>
    /// <remarks>
    /// The inventory-icon fallback keeps gear visible while the dedicated rig art is still being
    /// authored — an icon on the right bone reads far better than a hole where the helm should be.
    /// </remarks>
    private Texture2D? GearTexture(GearSlot slot, ItemInstance item)
    {
        var name = slot.ToString().ToLowerInvariant();
        var trait = GearTraits.TraitOf(item)?.ToString().ToLowerInvariant();
        return (trait is not null ? _ui.Assets.Get($"gear_{name}_{trait}") : null)
               ?? _ui.Assets.Get($"gear_{name}")
               ?? (trait is not null ? _ui.Assets.Get($"item_{name}_{trait}") : null)
               ?? _ui.Assets.Get($"item_slot_{name}");
    }
}
