using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// Reads a drawn figure's own silhouette for a point on it (ADR-011, JAWS): the front-most opaque pixel of its lower
/// body, on the side that faces the champion. A trap must close ON the attacker, and a bounding box's corner is often
/// empty air in front of a paw or behind a lunging head.
/// </summary>
/// <remarks>
/// One <see cref="Texture2D.GetData{T}(int, Rectangle?, T[], int, int)"/> of the frame's source rectangle, the first
/// time a (texture, frame) pair is asked, and the answer is kept; the scratch buffer is reused, so a steady fight asks
/// nothing of the GPU and allocates nothing. The answer is in the frame's own pixels, mapped through the same Src and
/// Dest the draw used (the rule <see cref="ActorSocketMap"/> keeps for sockets).
/// </remarks>
public static class SilhouetteProbe
{
    private static readonly Dictionary<(Texture2D, Rectangle, bool), Vector2?> Known = new();
    private static Color[] _scratch = Array.Empty<Color>();

    /// <summary>
    /// The arena point of the frame's front-most opaque pixel between <paramref name="rowFrom"/> and
    /// <paramref name="rowTo"/> of its opaque height, facing <paramref name="facesLeft"/>, moved
    /// <paramref name="inset"/> of the drawn width back into the body; null when the frame has no opaque pixel.
    /// </summary>
    public static Vector2? FrontLower(SpriteFrame frame, bool facesLeft, float rowFrom, float rowTo, float inset)
    {
        var flipped = frame.Effects.HasFlag(SpriteEffects.FlipHorizontally);
        // which side of the SOURCE pixels faces the champion: the drawn left, unless the draw mirrored them
        var frontIsSrcLeft = facesLeft != flipped;
        if (!Known.TryGetValue((frame.Texture, frame.Src, frontIsSrcLeft), out var local))
            Known[(frame.Texture, frame.Src, frontIsSrcLeft)] = local = Measure(frame.Texture, frame.Src, frontIsSrcLeft, rowFrom, rowTo);
        if (local is not { } p) return null;
        var sx = frame.Dest.Width / (float)Math.Max(1, frame.Src.Width);
        var sy = frame.Dest.Height / (float)Math.Max(1, frame.Src.Height);
        var x = flipped ? frame.Dest.Right - p.X * sx : frame.Dest.X + p.X * sx;
        var back = inset * frame.Dest.Width * (facesLeft ? 1f : -1f);   // into the body, away from the champion
        return new Vector2(x + back, frame.Dest.Y + p.Y * sy);
    }

    private static readonly Dictionary<(Texture2D, Rectangle), Rectangle?> KnownBounds = new();

    /// <summary>
    /// The arena rectangle of the frame's OPAQUE pixels (its drawn silhouette's bounding box), mapped through the same
    /// Src and Dest the draw used; null when the frame has no opaque pixel. A creature's layout rectangle can hold a
    /// good deal of empty canvas above a crouching head, and a shape that must sit just outside the silhouette (the
    /// Shadow fangs, ADR-011) needs the silhouette, not the layout.
    /// </summary>
    public static Rectangle? OpaqueBounds(SpriteFrame frame)
    {
        if (!KnownBounds.TryGetValue((frame.Texture, frame.Src), out var local))
            KnownBounds[(frame.Texture, frame.Src)] = local = MeasureBounds(frame.Texture, frame.Src);
        if (local is not { } b) return null;
        var sx = frame.Dest.Width / (float)Math.Max(1, frame.Src.Width);
        var sy = frame.Dest.Height / (float)Math.Max(1, frame.Src.Height);
        var flipped = frame.Effects.HasFlag(SpriteEffects.FlipHorizontally);
        var x = flipped ? frame.Dest.Right - (b.Right) * sx : frame.Dest.X + b.X * sx;
        return new Rectangle((int)MathF.Round(x), (int)MathF.Round(frame.Dest.Y + b.Y * sy),
                             (int)MathF.Round(b.Width * sx), (int)MathF.Round(b.Height * sy));
    }

    /// <summary>The bounding box of the opaque pixels, in the source rectangle's own pixels.</summary>
    private static Rectangle? MeasureBounds(Texture2D tex, Rectangle src)
    {
        var n = src.Width * src.Height;
        if (n <= 0) return null;
        if (_scratch.Length < n) _scratch = new Color[n];
        tex.GetData(0, src, _scratch, 0, n);
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < src.Height; y++)
            for (var x = 0; x < src.Width; x++)
                if (_scratch[y * src.Width + x].A > 100)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
        if (right < 0) return null;
        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>The front-most opaque pixel of the rows asked, in the source rectangle's own pixels.</summary>
    private static Vector2? Measure(Texture2D tex, Rectangle src, bool frontIsLeft, float rowFrom, float rowTo)
    {
        var n = src.Width * src.Height;
        if (n <= 0) return null;
        if (_scratch.Length < n) _scratch = new Color[n];
        tex.GetData(0, src, _scratch, 0, n);
        int top = -1, bottom = -1;
        for (var y = 0; y < src.Height && top < 0; y++)
            for (var x = 0; x < src.Width; x++)
                if (_scratch[y * src.Width + x].A > 100) { top = y; break; }
        for (var y = src.Height - 1; y >= 0 && bottom < 0; y--)
            for (var x = 0; x < src.Width; x++)
                if (_scratch[y * src.Width + x].A > 100) { bottom = y; break; }
        if (top < 0 || bottom < top) return null;
        var from = top + (int)((bottom - top) * rowFrom);
        var to = top + (int)((bottom - top) * rowTo);
        Vector2? best = null;
        for (var y = from; y <= to; y++)
        {
            var edge = -1;
            if (frontIsLeft)
            {
                for (var x = 0; x < src.Width; x++)
                    if (_scratch[y * src.Width + x].A > 100) { edge = x; break; }
                if (edge >= 0 && (best is null || edge < best.Value.X)) best = new Vector2(edge, y);
            }
            else
            {
                for (var x = src.Width - 1; x >= 0; x--)
                    if (_scratch[y * src.Width + x].A > 100) { edge = x; break; }
                if (edge >= 0 && (best is null || edge > best.Value.X)) best = new Vector2(edge, y);
            }
        }
        return best;
    }
}
