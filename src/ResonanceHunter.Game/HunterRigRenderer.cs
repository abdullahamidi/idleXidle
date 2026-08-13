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
/// Assembles the Hunter cutout rig and draws it into a box.
/// </summary>
/// <remarks>
/// <para>
/// Extracted so the dev preview and the battle screen share ONE assembly path. They had diverged:
/// the preview assembled the rig while the battle screen drew a flat <c>hunter_idle</c> sprite.
/// </para>
/// <para>
/// THE FIGURE NO LONGER WEARS ANYTHING. Equipped gear used to change how the champion looked, by two
/// routes: a weapon pinned to the <c>hand_main</c> bone, and armour as drop-in replacement textures
/// for the bones a slot covers (<c>worn_&lt;tier&gt;_&lt;bone&gt;</c>). Both are gone.
/// </para>
/// <para>
/// The reason is worth keeping, because it cost a great deal of work to learn twice. A piece of gear
/// drawn onto a body has to agree with that body's perspective, light direction, outline weight and
/// palette, at every frame of every pose. An item icon never does — it is authored to fill a square
/// cell — so it reads as a sticker however carefully its height, pivot and rotation are tuned. Painting
/// armour onto the figure and re-cutting it on the body's own boxes fixed the perspective but not the
/// combinatorics: a helm, chest, gloves and boots chosen independently are four sets of art that were
/// never designed to sit together, and the character ends up wearing an argument.
/// </para>
/// <para>
/// A character now has ONE fixed appearance, which is also what the roster needs: each character is
/// recognisable at a glance, and gear is a sheet of numbers rather than a wardrobe. The art and the
/// binding tables are in git history if the wardrobe is ever worth another attempt.
/// </para>
/// </remarks>
public sealed class HunterRigRenderer
{
    private readonly UiKit _ui;
    private readonly Rig _rig = HunterRig.BuildRig();
    private readonly Dictionary<string, HunterPart> _byId = HunterRig.Parts.ToDictionary(p => p.Id);


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
    /// <param name="groundToBottom">
    /// Sit the figure's lowest pixel on <c>box.Bottom</c> rather than centring it — actors stand on a
    /// ground line, so the battle screen always wants this.
    /// </param>
    /// <returns>false if not one body part resolved, so callers can fall back to the flat sprite.</returns>
    public bool Draw(SpriteBatch b, Rectangle box, IReadOnlyDictionary<string, float>? pose,
                     Color tint, bool groundToBottom = true)
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

        // A paint list rather than drawing in place, because DrawOrder is not bone order — the off-hand
        // arm passes behind the torso.
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

        foreach (var (_, draw) in paint.OrderBy(e => e.Order)) draw();

        if (ShowMarkers)
            foreach (var bt in frame)
            {
                var pos = ToScreen(bt.Position);
                _ui.Fill(b, new Rectangle((int)pos.X - 3, (int)pos.Y - 3, 6, 6), new Color(0xF0, 0xA8, 0x30));
            }
        return true;
    }

}
