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

    private static readonly Dictionary<(Texture2D, int, bool, float, float), Vector2?> KnownTorso = new();
    private static int[] _dist = Array.Empty<int>();

    /// <summary>
    /// The FALLBACK torso of a frame (ADR-011, the MARK reference, BRAND), for a creature strip with no authored body
    /// point (<see cref="MarkPoints"/>): the centroid, weighted by the cube of the distance to the silhouette's edge, of
    /// the opaque pixels between 28 % and 72 % of the opaque height and between <paramref name="from"/> and
    /// <paramref name="to"/> of the opaque width counted from the side facing the champion; null when the frame has no
    /// opaque pixel.
    /// </summary>
    /// <remarks>
    /// Every strip in the game has an authored point; this answers only a strip added without one. A luma rule that steered
    /// it off the eyes was tried and removed: it put the coil on legs, hems, staffs and spike tips on the families it was
    /// not tuned on. MonoGame's GetData copies the WHOLE texture whatever rectangle it is asked for, so the first question
    /// about a strip reads it once and measures every frame (keyed by the texture and the frame index, so a side-cropped
    /// source never reads it again); after that nothing is read or allocated.
    /// </remarks>
    public static Vector2? Torso(SpriteFrame frame, bool facesLeft, float from = 0.30f, float to = 0.80f)
    {
        var flipped = (frame.Effects & SpriteEffects.FlipHorizontally) != 0;
        var frontIsSrcLeft = facesLeft != flipped;
        var size = frame.Texture.Height;              // one row of square frames
        if (size <= 0) return null;
        var index = frame.Src.X / size;
        var key = (frame.Texture, index, frontIsSrcLeft, from, to);
        if (!KnownTorso.TryGetValue(key, out var local))
        {
            MeasureTorsoStrip(frame.Texture, frontIsSrcLeft, from, to);
            if (!KnownTorso.TryGetValue(key, out local))
                KnownTorso[key] = local = null;
        }
        if (local is not { } p) return null;
        var inX = p.X - (frame.Src.X - index * size);   // measured on the whole frame; the drawn source may be cropped
        var inY = p.Y - frame.Src.Y;
        var sx = frame.Dest.Width / (float)Math.Max(1, frame.Src.Width);
        var sy = frame.Dest.Height / (float)Math.Max(1, frame.Src.Height);
        var x = flipped ? frame.Dest.Right - inX * sx : frame.Dest.X + inX * sx;
        return new Vector2(x, frame.Dest.Y + inY * sy);
    }

    /// <summary>Reads a strip (one row of square frames) ONCE and records the torso of every frame in it.</summary>
    private static void MeasureTorsoStrip(Texture2D tex, bool frontIsLeft, float from, float to)
    {
        var size = tex.Height;
        if (size <= 0) return;
        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        for (var k = 0; k < tex.Width / size; k++)
            KnownTorso[(tex, k, frontIsLeft, from, to)] = MeasureTorso(data, tex.Width, k * size, size, size, frontIsLeft, from, to);
    }

    /// <summary>
    /// The torso point of one frame inside <paramref name="data"/> (row stride <paramref name="stride"/>, the frame from
    /// column <paramref name="x0"/>), in the frame's own pixels (see <see cref="Torso"/>).
    /// </summary>
    internal static Vector2? MeasureTorso(Color[] data, int stride, int x0, int fw, int fh, bool frontIsLeft, float from, float to)
    {
        // half resolution: a pixel is opaque when any of its four source pixels is
        int w = (fw + 1) / 2, h = (fh + 1) / 2;
        if (_dist.Length < w * h) _dist = new int[w * h];
        int left = int.MaxValue, right = -1, top = int.MaxValue, bottom = -1;
        const int Far = 1 << 20;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var solid = false;
                for (var dy = 0; dy < 2 && !solid; dy++)
                    for (var dx = 0; dx < 2 && !solid; dx++)
                    {
                        int sx = x * 2 + dx, sy = y * 2 + dy;
                        if (sx < fw && sy < fh && data[sy * stride + x0 + sx].A > 100) solid = true;
                    }
                _dist[y * w + x] = solid ? Far : 0;
                if (!solid) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        if (right < 0) return null;
        Chamfer(_dist, w, h);
        var span = right - left;
        int colFrom = frontIsLeft ? left + (int)(span * from) : left + (int)(span * (1f - to));
        int colTo = frontIsLeft ? left + (int)(span * to) : left + (int)(span * (1f - from));
        int rowFrom = top + (int)((bottom - top) * 0.28f), rowTo = top + (int)((bottom - top) * 0.72f);
        double sw = 0, swx = 0, swy = 0;
        for (var y = rowFrom; y <= rowTo; y++)
            for (var x = colFrom; x <= colTo; x++)
            {
                var d = _dist[y * w + x] / 3.0;
                if (d <= 0) continue;
                var wt = d * d * d;
                sw += wt;
                swx += wt * x;
                swy += wt * y;
            }
        return sw > 0 ? new Vector2((float)(swx / sw) * 2f + 1f, (float)(swy / sw) * 2f + 1f) : null;
    }

    /// <summary>The chamfer (3-4) distance, in place, from the zero cells, two passes.</summary>
    private static void Chamfer(int[] d, int w, int h)
    {
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (d[i] == 0) continue;
                var v = d[i];
                if (x > 0) v = Math.Min(v, d[i - 1] + 3);
                if (y > 0)
                {
                    v = Math.Min(v, d[i - w] + 3);
                    if (x > 0) v = Math.Min(v, d[i - w - 1] + 4);
                    if (x < w - 1) v = Math.Min(v, d[i - w + 1] + 4);
                }
                d[i] = v;
            }
        for (var y = h - 1; y >= 0; y--)
            for (var x = w - 1; x >= 0; x--)
            {
                var i = y * w + x;
                if (d[i] == 0) continue;
                var v = d[i];
                if (x < w - 1) v = Math.Min(v, d[i + 1] + 3);
                if (y < h - 1)
                {
                    v = Math.Min(v, d[i + w] + 3);
                    if (x < w - 1) v = Math.Min(v, d[i + w + 1] + 4);
                    if (x > 0) v = Math.Min(v, d[i + w - 1] + 4);
                }
                d[i] = v;
            }
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
