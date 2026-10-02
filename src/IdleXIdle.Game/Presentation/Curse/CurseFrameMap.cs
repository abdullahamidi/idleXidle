using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// A drawn frame's source-to-screen scale and mirror (ADR-013 §4): how a territory baked in the idle strip's SOURCE
/// pixels lands on the frame drawn now. Each territory hangs from the current frame's authored body point, its offset
/// mirrored with the frame and scaled by the frame's own <see cref="Kx"/> / <see cref="Ky"/> (a squash, a lunge, an
/// extended canvas: the frame's transform, never the one the territories were first seen in).
/// </summary>
internal readonly struct CurseFrameMap
{
    /// <summary>Screen pixels per source pixel, across and down, and their geometric mean.</summary>
    public readonly float Kx, Ky, K;

    /// <summary>The frame is drawn mirrored.</summary>
    public readonly bool Flip;

    /// <summary>The map of <paramref name="frame"/>.</summary>
    public CurseFrameMap(SpriteFrame frame)
    {
        Kx = frame.Src.Width > 0 ? frame.Dest.Width / (float)frame.Src.Width : 1f;
        Ky = frame.Src.Height > 0 ? frame.Dest.Height / (float)frame.Src.Height : 1f;
        K = MathF.Sqrt(Kx * Ky);
        Flip = (frame.Effects & SpriteEffects.FlipHorizontally) != 0;
    }

    /// <summary>Territory <paramref name="k"/>'s centre on screen, from this frame's body point.</summary>
    public Vector2 At(Layout lay, int k, Vector2 anchor)
        => anchor + new Vector2((Flip ? -lay.Offset[k].X : lay.Offset[k].X) * Kx, lay.Offset[k].Y * Ky);

    /// <summary>Territory <paramref name="k"/>'s diameter on screen, unclamped.</summary>
    public float Diameter(Layout lay, int k) => lay.Diameter[k] * K;

    /// <summary>
    /// Territory <paramref name="k"/>'s diameter on screen as drawn: the frame's scale, then the screen clamp of the
    /// first territory (<see cref="CurseSeating.ScreenClamp"/>: only the widest bosses at the largest scales reach it).
    /// </summary>
    public float ScreenDiameter(Layout lay, int k) => Diameter(lay, k) * CurseSeating.ScreenClamp(Diameter(lay, 0));
}
