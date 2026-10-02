using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// A creature strip's authored BODY POINT, one per frame: where a MARK sits on that creature (ADR-011, the MARK
/// reference, BRAND). Read from a small <c>&lt;strip&gt;.mark.json</c> beside the strip, <c>{"points": [[x, y], ...]}</c>
/// in fractions of the square frame, one point per frame.
/// </summary>
/// <remarks>
/// <para>
/// AUTHORED, NOT GUESSED: a luma heuristic (the eyes are the brightest texels in a dark face) moved the coil onto the
/// head, the legs, a staff or a spike tip on the families it was not tuned on, and against the NEXT creature's face in
/// the pack's round-head pose. So every point is proposed offline by <c>tools/asset-pipeline/v2/mark_points.py</c> (a
/// reviewed seed per strip, tracked frame to frame, the coil's footprint kept inside the silhouette) and reviewed once on
/// a contact sheet. The point is on the torso, off the head and the eyes, forward (toward the champion) of the body's
/// middle, so the creature standing behind never presses a face against it.
/// </para>
/// <para>
/// The point is mapped to the arena with exactly the transform that drew the frame (<see cref="ActorSocketMap"/>): it
/// rides the breath, the lunge and PRESS's buckle. Read once, on first use; a strip with no file keeps the fallback.
/// </para>
/// </remarks>
public sealed class MarkPoints
{
    private static Dictionary<string, MarkPoints>? _byStrip;

    /// <summary>The frame points (fractions of the square frame), in strip order.</summary>
    public IReadOnlyList<Vector2> Points { get; }

    /// <summary>The authored HEAD box of each frame (x0, y0, x1, y1, fractions of the square frame), in strip order; empty
    /// when the file has none. A mark keeps off it (the curse's territories read as the creature's face paint there).</summary>
    public IReadOnlyList<Vector4> Heads { get; }

    /// <summary>A strip's points (and, optionally, its head boxes).</summary>
    public MarkPoints(IReadOnlyList<Vector2> points, IReadOnlyList<Vector4>? heads = null)
    {
        Points = points;
        Heads = heads ?? Array.Empty<Vector4>();
    }

    /// <summary>The mean body point over the strip's frames (fractions of the square frame).</summary>
    public Vector2 MeanPoint
    {
        get
        {
            var sum = Vector2.Zero;
            foreach (var p in Points) sum += p;
            return sum / Points.Count;
        }
    }

    /// <summary>The body point of the frame drawn as <paramref name="frame"/>, in the arena; null if the frame is not one of the strip's.</summary>
    public Vector2? ToArena(SpriteFrame frame)
        => ToArena(Points, frame.Texture.Height, frame.Src, frame.Dest, (frame.Effects & SpriteEffects.FlipHorizontally) != 0);

    /// <summary>The body point of the strip frame drawn from <paramref name="src"/> into <paramref name="dest"/> (the strips
    /// are one row of square <paramref name="frameSize"/> frames; the source may be cropped inside its frame).</summary>
    internal static Vector2? ToArena(IReadOnlyList<Vector2> points, int frameSize, Rectangle src, Rectangle dest, bool flipped)
    {
        if (frameSize <= 0) return null;
        var index = src.X / frameSize;
        if (index < 0 || index >= points.Count) return null;
        var p = points[index];
        return ActorSocketMap.ToArena(src, dest, flipped, frameSize, index, new ActionSocket(p.X, p.Y, 0f));
    }

    /// <summary>The points authored for <paramref name="stripKey"/>, or null: that strip keeps the fallback torso probe.</summary>
    public static MarkPoints? For(string stripKey)
    {
        Warm();
        return _byStrip!.GetValueOrDefault(stripKey);
    }

    /// <summary>Reads every body-point file once (~30-40 ms): called when a marked wave begins, never on a Draw frame.</summary>
    public static void Warm() => _byStrip ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "art"));

    /// <summary>Read every <c>*.mark.json</c> under <paramref name="root"/>, keyed by the strip name it sits beside.</summary>
    public static Dictionary<string, MarkPoints> Load(string root)
    {
        var map = new Dictionary<string, MarkPoints>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return map;
        foreach (var path in Directory.EnumerateFiles(root, "*.mark.json", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            try { map[name[..^".mark.json".Length]] = Parse(File.ReadAllText(path)); }
            catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException
                                           or IndexOutOfRangeException or IOException or UnauthorizedAccessException)
            {
                // a broken file must not take the fight down: that strip keeps the fallback, loudly
                Console.Error.WriteLine($"mark points '{name}' ignored: {ex.Message}");
            }
        }
        return map;
    }

    /// <summary>Parse a <c>.mark.json</c>: <c>{"points": [[x, y], ...], "head": [[x0, y0, x1, y1], ...]}</c>, fractions of
    /// the frame (the head boxes are optional).</summary>
    public static MarkPoints Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var points = new List<Vector2>();
        foreach (var p in doc.RootElement.GetProperty("points").EnumerateArray())
        {
            var x = p[0].GetSingle();
            var y = p[1].GetSingle();
            if (x is < 0f or > 1f || y is < 0f or > 1f) throw new FormatException($"a point outside its frame: {x}, {y}");
            points.Add(new Vector2(x, y));
        }
        if (points.Count == 0) throw new FormatException("no points");
        var heads = new List<Vector4>();
        if (doc.RootElement.TryGetProperty("head", out var head) && head.ValueKind == JsonValueKind.Array)
            foreach (var h in head.EnumerateArray())
            {
                var box = new Vector4(h[0].GetSingle(), h[1].GetSingle(), h[2].GetSingle(), h[3].GetSingle());
                if (box.X < 0f || box.Y < 0f || box.Z > 1f || box.W > 1f || box.Z < box.X || box.W < box.Y)
                    throw new FormatException($"a head box outside its frame: {box}");
                heads.Add(box);
            }
        return new MarkPoints(points, heads);
    }
}
