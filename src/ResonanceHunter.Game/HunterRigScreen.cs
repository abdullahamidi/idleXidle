using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ResonanceHunter.Core.Animation;

namespace ResonanceHunter.Client;

/// <summary>
/// Dev preview (RH_SHOT_MODE=rig, or F8 in-game): assembles the batch-1 Hunter cutout rig at rest and
/// draws it fit to a box, so the part layout, pivots, and joint connections can be verified and tuned
/// before the rig is wired into the battle screen. Not part of the shipping game.
/// </summary>
public sealed class HunterRigScreen
{
    private static readonly Color Bg = new(0x24, 0x20, 0x2C);
    private static readonly Color Panel = new(0x30, 0x2B, 0x3A);
    private static readonly Color Marker = new(0xF0, 0xA8, 0x30);
    private static readonly Color Missing = new(0xD8, 0x48, 0x3A);
    private static readonly Color Ink = new(0xE8, 0xDF, 0xC8);

    private readonly UiKit _ui;
    private readonly Rig _rig = HunterRig.BuildRig();
    private readonly Dictionary<string, HunterPart> _byId = HunterRig.Parts.ToDictionary(p => p.Id);

    /// <summary>Draw the little joint dots + missing-part boxes. Toggled off for a clean beauty shot.</summary>
    public bool ShowMarkers = true;

    public HunterRigScreen(UiKit ui) => _ui = ui;

    public void Draw(SpriteBatch b)
    {
        _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Bg);

        // Rest pose: no per-bone deltas.
        var frame = _rig.Evaluate(new Dictionary<string, float>(), Vec2.Zero);

        // Native bounds of the whole figure (each part drawn origin=pivot at its bone position).
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var bt in frame)
        {
            var p = _byId[bt.BoneId];
            var tex = _ui.Assets.Get(p.TextureKey);
            float w = tex?.Width ?? 40, h = tex?.Height ?? 40;
            float l = bt.Position.X - p.Pivot.X, t = bt.Position.Y - p.Pivot.Y;
            minX = MathF.Min(minX, l); minY = MathF.Min(minY, t);
            maxX = MathF.Max(maxX, l + w); maxY = MathF.Max(maxY, t + h);
        }

        var box = new Rectangle(960 - 380, 60, 760, 980);
        _ui.Fill(b, box, Panel);

        float bw = MathF.Max(1, maxX - minX), bh = MathF.Max(1, maxY - minY);
        float fit = MathF.Min(box.Width / bw, box.Height / bh);
        var boundsMin = new Vector2(minX, minY);
        var originScreen = new Vector2(
            box.X + (box.Width - bw * fit) / 2f,
            box.Y + (box.Height - bh * fit) / 2f);

        Vector2 ToScreen(Vec2 nativePos) =>
            originScreen + (new Vector2(nativePos.X, nativePos.Y) - boundsMin) * fit;

        // Back-to-front paint.
        foreach (var bt in frame.OrderBy(t => _byId[t.BoneId].DrawOrder))
        {
            var p = _byId[bt.BoneId];
            var pos = ToScreen(bt.Position);
            var tex = _ui.Assets.Get(p.TextureKey);
            if (tex is null)
            {
                _ui.Fill(b, new Rectangle((int)(pos.X - p.Pivot.X * fit), (int)(pos.Y - p.Pivot.Y * fit),
                    (int)(40 * fit), (int)(40 * fit)), Missing);
                continue;
            }
            b.Draw(tex, pos, null, Color.White, bt.Angle,
                new Vector2(p.Pivot.X, p.Pivot.Y), fit, SpriteEffects.None, 0f);
        }

        // Joint markers on top, so mis-aligned pivots are obvious.
        if (ShowMarkers)
            foreach (var bt in frame)
            {
                var pos = ToScreen(bt.Position);
                _ui.Fill(b, new Rectangle((int)pos.X - 3, (int)pos.Y - 3, 6, 6), Marker);
            }

        _ui.TextCenterBig(b, "HUNTER RIG - REST POSE", 960, 16, Marker, 30);
        _ui.TextCenter(b, $"{HunterRig.Parts.Count} parts   fit x{fit:0.00}", 960, 1044, Ink);
    }
}
