using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ResonanceHunter.Core.Animation;

namespace ResonanceHunter.Client;

/// <summary>
/// The Attacker-strike-arc spike: <b>snapped</b> (left) vs <b>continuous</b> (right), same pose data.
///
/// <b>REVISION 2 — the first version of this spike was INVALID.</b> It rendered limbs as stretched 1x1
/// white rectangles. A flat, untextured fill has no internal pixel grid, so resampling it produces
/// nothing visible: the continuous side <i>could not</i> shimmer no matter how badly it resampled, and
/// the entire reason angle-snapping exists was untestable. It made continuous rotation look great and
/// snapping look pointlessly chunky — which is exactly the wrong conclusion, drawn from a rigged test.
///
/// Limbs are now real pixel art, generated in <see cref="BuildLimbTexture"/> using the art bible's own
/// grammar: a flat fill, an unbroken 1px dark contour, and internal seam lines. Those hard edges are
/// precisely what rotation destroys, and precisely what the legibility model depends on. Now the
/// comparison means something.
///
/// Controls: <b>Left/Right</b> snap steps · <b>Space</b> replay · <b>Hold D</b> slow-mo ·
/// <b>H</b> hold the strike's worst frame · <b>Z</b> zoom 4x · <b>Tab</b> back to combat.
/// </summary>
public sealed class RigSpikeScreen
{
    private static readonly Color VoidInk = new(0x1B, 0x16, 0x20);
    private static readonly Color BoneParchment = new(0xE8, 0xDF, 0xC8);
    private static readonly Color HearthGold = new(0xF0, 0xA8, 0x30);
    private static readonly Color EmberThreat = new(0xD8, 0x48, 0x3A);
    private static readonly Color ColdSlate = new(0x57, 0x61, 0x6F);

    private static readonly Dictionary<string, (int Length, int Thickness)> LimbSize = new()
    {
        ["torso"] = (26, 14),
        ["upper_arm"] = (22, 8),
        ["forearm"] = (20, 6),
    };

    private readonly Texture2D _pixel;
    private readonly Dictionary<string, Texture2D> _limbTextures = new();
    private readonly Clip _strike = Clip.AttackerStrike();

    private Rig _snappedRig;
    private readonly Rig _smoothRig;

    private int _snapSteps = 16;
    private float _time;
    private bool _hold;
    private bool _zoom;
    private KeyboardState _prevKeys;

    /// <summary>Mid-strike — the fastest, widest part of the sweep, where artifacts peak.</summary>
    private const float WorstFrameSeconds = 0.68f;

    public RigSpikeScreen(GraphicsDevice device, Texture2D pixel)
    {
        _pixel = pixel;

        foreach (var (id, size) in LimbSize)
            _limbTextures[id] = BuildLimbTexture(device, size.Length, size.Thickness, id);

        _snappedRig = BuildRig(_snapSteps);
        _smoothRig = BuildRig(4096); // effectively continuous — the control case
    }

    /// <summary>
    /// Generate a limb sprite that behaves like the real art will.
    /// </summary>
    /// <remarks>
    /// The art bible's legibility model rests on: an unbroken contour, flat interior fills, and internal
    /// seam lines. All three are <i>hard 1px boundaries</i> — the exact features rotation resampling
    /// softens and makes crawl. A limb texture without them cannot test the thing this spike exists to
    /// test. Hence the dark outline and the seam stripes: they are not decoration, they are the
    /// measuring instrument.
    /// </remarks>
    private static Texture2D BuildLimbTexture(GraphicsDevice device, int w, int h, string boneId)
    {
        var data = new Color[w * h];
        var fill = boneId switch
        {
            "forearm" => HearthGold,
            "upper_arm" => BoneParchment,
            _ => ColdSlate,
        };

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var onContour = x == 0 || y == 0 || x == w - 1 || y == h - 1;

            // Internal seam lines — the art bible's "the contour never breaks, the seams read inside it".
            var onSeam = x > 1 && x < w - 2 && (x % 6 == 0);

            data[y * w + x] =
                onContour ? VoidInk :
                onSeam ? EmberThreat :
                fill;
        }

        var tex = new Texture2D(device, w, h);
        tex.SetData(data);
        return tex;
    }

    private static Rig BuildRig(int snapSteps) => new(
        new[]
        {
            new Bone { Id = "torso", Offset = Vec2.Zero },
            new Bone { Id = "upper_arm", ParentId = "torso", Offset = new Vec2(0f, -72f) },
            new Bone { Id = "forearm", ParentId = "upper_arm", Offset = new Vec2(88f, 0f) },
        },
        snapSteps);

    public void Update(GameTime gameTime, KeyboardState keys)
    {
        _hold = keys.IsKeyDown(Keys.H);
        _zoom = keys.IsKeyDown(Keys.Z);

        if (!_hold)
        {
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds * (keys.IsKeyDown(Keys.D) ? 0.12f : 1f);
            _time += dt;
            if (_time > _strike.DurationSeconds + 0.6f) _time = 0f;
        }

        if (Pressed(keys, Keys.Space)) _time = 0f;
        if (Pressed(keys, Keys.Right)) SetSteps(_snapSteps * 2);
        if (Pressed(keys, Keys.Left)) SetSteps(_snapSteps / 2);

        _prevKeys = keys;
    }

    private void SetSteps(int steps)
    {
        _snapSteps = Math.Clamp(steps, 4, 128);
        _snappedRig = BuildRig(_snapSteps);
    }

    private bool Pressed(KeyboardState now, Keys key) => now.IsKeyDown(key) && _prevKeys.IsKeyUp(key);

    public void Draw(SpriteBatch batch)
    {
        var t = _hold ? WorstFrameSeconds : _time;
        var pose = _strike.Sample(t);
        var scale = _zoom ? 16f : 4f;

        // Zooming magnifies the pixels themselves (PointClamp), which is how you actually SEE
        // edge crawl rather than merely suspect it.
        DrawRig(batch, _snappedRig, pose, new Vec2(480f, 660f), useSnapped: true, scale);
        DrawRig(batch, _smoothRig, pose, new Vec2(1360f, 660f), useSnapped: false, scale);

        Fill(batch, new Rectangle(956, 120, 4, 800), ColdSlate);

        Fill(batch, new Rectangle(320, 144, 320, 20), HearthGold);   // SNAPPED
        Fill(batch, new Rectangle(1200, 144, 320, 20), ColdSlate);   // CONTINUOUS

        for (var i = 0; i < _snapSteps && i < 64; i++)
            Fill(batch, new Rectangle(240 + i * 20, 1000, 12, 32), i % 4 == 0 ? HearthGold : BoneParchment);

        var striking = t is >= 0.60f and <= 0.75f;
        Fill(batch, new Rectangle(80, 80, 48, 48), striking ? EmberThreat : ColdSlate);

        Fill(batch, new Rectangle(240, 944, 1440, 8), ColdSlate);
        var head = (int)(1440f * Math.Clamp(t / _strike.DurationSeconds, 0f, 1f));
        Fill(batch, new Rectangle(240 + head, 932, 8, 32), EmberThreat);
    }

    private void DrawRig(
        SpriteBatch batch, Rig rig, IReadOnlyDictionary<string, float> pose,
        Vec2 root, bool useSnapped, float scale)
    {
        var frame = rig.Evaluate(pose, root);

        foreach (var bt in frame)
        {
            if (!_limbTextures.TryGetValue(bt.BoneId, out var tex)) continue;

            var angle = useSnapped ? bt.SnappedAngle : bt.Angle;

            // Note: no destination Rectangle. Drawing to a stretched rect would resample the texture a
            // SECOND time, on top of the rotation — confounding the very artifact under test. Draw at
            // native size with an explicit scale, pivoting at the bone root.
            batch.Draw(
                tex,
                position: new Vector2(bt.Position.X, bt.Position.Y),
                sourceRectangle: null,
                color: Color.White,
                rotation: angle,
                origin: new Vector2(0f, tex.Height / 2f),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 0f);

            Fill(batch, new Rectangle((int)bt.Position.X - 4, (int)bt.Position.Y - 4, 12, 12), EmberThreat);
        }
    }

    private void Fill(SpriteBatch batch, Rectangle r, Color c) => batch.Draw(_pixel, r, c);
}
