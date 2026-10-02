using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// One creature's territories, in its idle strip's SOURCE pixels, never the screen's: each centre's offset from the
/// strip's mean authored body point (unflipped, the art's own facing) and its diameter, each territory's Ash-Burn share,
/// and how many territories the body has room for. Baked offline (ADR-013 §3-4); each drawn frame maps it through its
/// own transform and hangs it from that frame's own authored body point.
/// </summary>
public sealed class Layout
{
    /// <summary>Each territory's centre, from the mean authored body point (source pixels, unflipped).</summary>
    public readonly Vector2[] Offset = new Vector2[CurseSeating.MaxTerritories];

    /// <summary>Each territory's diameter (source pixels, before the runtime's screen clamp).</summary>
    public readonly float[] Diameter = new float[CurseSeating.MaxTerritories];

    /// <summary>Each territory's Ash-Burn share (0 Shadow Violet .. 1 Ash-Burn).</summary>
    public readonly float[] Burn = new float[CurseSeating.MaxTerritories];

    /// <summary>How many territories the body holds (depth 3 shows at most this many).</summary>
    public int Available;
}

/// <summary>
/// One strip's pixels as the seating reads them, frame-major (<c>(f * size + y) * size + x</c>): each pixel's opacity,
/// how visibly the drain changes it (0..255) and whether its own colour competes with the curse's violet (0 / 1).
/// Built on the CPU from the PNG's premultiplied pixels, offline; never from a GPU read-back.
/// </summary>
internal sealed class HostStrip
{
    /// <summary>The square frame's side (source pixels).</summary>
    public readonly int Size;

    /// <summary>The number of frames.</summary>
    public readonly int Frames;

    /// <summary>Each pixel's alpha, frame-major.</summary>
    public readonly byte[] Alpha;

    /// <summary>Each pixel's visible change under the drain (0..255), frame-major.</summary>
    public readonly byte[] Change;

    /// <summary>1 where the pixel's own colour is blue-violet, frame-major.</summary>
    public readonly byte[] Compete;

    private HostStrip(int size, int frames)
    {
        Size = size;
        Frames = frames;
        Alpha = new byte[size * size * frames];
        Change = new byte[Alpha.Length];
        Compete = new byte[Alpha.Length];
    }

    /// <summary>
    /// A strip (one row of square frames, <paramref name="width"/> x <paramref name="height"/>, premultiplied, row-major)
    /// read for seating against the host's <paramref name="look"/> and Ash-Burn share <paramref name="burn"/>.
    /// </summary>
    public static HostStrip From(ReadOnlySpan<Color> pixels, int width, int height, HostLook look, float burn)
    {
        if (height <= 0 || width % height != 0) throw new ArgumentException($"not a strip of square frames: {width}x{height}");
        var s = new HostStrip(height, width / height);
        for (var f = 0; f < s.Frames; f++)
            for (var y = 0; y < height; y++)
                for (var x = 0; x < height; x++)
                {
                    var c = pixels[y * width + f * height + x];
                    var i = (f * height + y) * height + x;
                    s.Alpha[i] = c.A;
                    s.Change[i] = CurseHost.Visible(c, CurseHost.DrainPixel(c, look, burn));
                    s.Compete[i] = CurseHost.Competes(c) ? (byte)1 : (byte)0;
                }
        return s;
    }

    /// <summary>The strip's own figure: its opaque bounds (alpha &gt;= 128) over every frame, in frame pixels.</summary>
    public Rectangle Content()
    {
        int x0 = Size, x1 = -1, y0 = Size, y1 = -1;
        for (var f = 0; f < Frames; f++)
            for (var y = 0; y < Size; y++)
            {
                var row = (f * Size + y) * Size;
                for (var x = 0; x < Size; x++)
                {
                    if (Alpha[row + x] < 128) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
        return x1 < 0 ? new Rectangle(0, 0, Size, Size) : new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }
}

/// <summary>
/// The seats' extra evidence, beyond the idle silhouette: the creature's OTHER frames (its attack strip, aligned to the
/// idle by each frame's authored body point), for telling its main body from a part that swings away. Given, the first
/// seat also leans to the upper torso and keeps its whole disc off the authored head (ADR-013 §4).
/// </summary>
internal sealed class SeatSignals
{
    /// <summary>Extra frames' alpha already placed in the idle frame's space (frame-major, idle size), or null.</summary>
    public byte[]? OtherAlpha;

    /// <summary>The number of frames in <see cref="OtherAlpha"/>.</summary>
    public int OtherFrames;
}

/// <summary>
/// WHERE THE TERRITORIES SIT (ADR-013 §4), pure and device-free: seated once per creature and slot parity, offline,
/// from the idle strip's STABLE silhouette (cells solid in three quarters of its frames), its mean authored body point
/// and head boxes, and its canonical figure; never from whatever frame was drawn first. The bake
/// (<c>brand_host_bake_test</c>) runs this on every creature and writes <c>&lt;idle strip&gt;.brand.json</c>.
/// </summary>
internal static class CurseSeating
{
    /// <summary>At most this many territories on one host (depth 3 shows three or four, as the body allows).</summary>
    internal const int MaxTerritories = 4;

    /// <summary>Territories shown at a depth: one, two, then as many as the body holds (three or four).</summary>
    internal static int TerritoriesAt(int stage) => stage <= 0 ? 0 : stage == 1 ? 1 : stage == 2 ? 2 : MaxTerritories;

    /// <summary>Each territory's share of the first one's diameter (later infections are a little smaller).</summary>
    internal static readonly float[] TerritoryShare = { 1f, 0.82f, 0.72f, 0.64f };

    /// <summary>The first territory's screen diameter is a share of the body: never under this (a buckle-sized patch)...</summary>
    internal const float MinScreenDiameter = 56f;

    /// <summary>... nor over this (a boss's whole chest).</summary>
    internal const float MaxScreenDiameter = 210f;

    /// <summary>The first territory's diameter as a share of the body's geometric mean side.</summary>
    internal const float DiameterShare = 0.4f;

    /// <summary>The first territory's diameter on screen: a share of the whole body, never a buckle-sized patch.</summary>
    internal static float FirstDiameter(Rectangle body)
        => Math.Clamp(DiameterShare * MathF.Sqrt(body.Width * (float)body.Height), MinScreenDiameter, MaxScreenDiameter);

    /// <summary>
    /// The first territory's diameter in the idle strip's SOURCE pixels for a canonical body on screen: unclamped it is
    /// exactly <see cref="CanonicalDiameter"/> (the body's scale cancels); the screen clamp only bites on the largest
    /// bosses and the smallest bodies.
    /// </summary>
    internal static float SourceDiameter(Rectangle body, Point content)
        => FirstDiameter(body) / MathF.Sqrt(body.Width / (float)Math.Max(1, content.X) * (body.Height / (float)Math.Max(1, content.Y)));

    /// <summary>The first territory's diameter in source pixels, scale-free: a share of the strip's own figure.</summary>
    internal static float CanonicalDiameter(Point content) => DiameterShare * MathF.Sqrt(Math.Max(1, content.X) * (float)Math.Max(1, content.Y));

    /// <summary>
    /// The factor the runtime scales every baked diameter by (offsets are kept): 1 unless the first territory, drawn at
    /// <paramref name="screenFirstDiameter"/> screen pixels, falls outside the screen clamp. The seats were placed at the
    /// unclamped size, so a clamp that shrinks them only widens the gaps.
    /// </summary>
    internal static float ScreenClamp(float screenFirstDiameter)
        => screenFirstDiameter <= 0f ? 1f : Math.Clamp(screenFirstDiameter, MinScreenDiameter, MaxScreenDiameter) / screenFirstDiameter;

    /// <summary>
    /// THE BAKE of one creature: its look and Ash-Burn share measured over every frame of its idle strip
    /// (<paramref name="idle"/>, premultiplied, <paramref name="width"/> x <paramref name="height"/>, one row of square
    /// frames), and its territories seated for both slot parities. Pure: the same pixels give the same data.
    /// </summary>
    /// <remarks>The creature's attack strip (<paramref name="attack"/>, with its own authored points), when given, is
    /// placed over the idle frame by each attack frame's body point: body that stays body through the lunge is where the
    /// first territory holds best.</remarks>
    internal static CurseHostData Bake(ReadOnlySpan<Color> idle, int width, int height, MarkPoints points,
                                       ReadOnlySpan<Color> attack = default, int attackWidth = 0, int attackHeight = 0, MarkPoints? attackPoints = null)
    {
        var look = CurseHost.Measure(idle);
        var burn = CurseHost.AshBurn(look);
        var strip = HostStrip.From(idle, width, height, look, burn);
        var anchor = points.MeanPoint * strip.Size;
        var signals = new SeatSignals();
        if (attackPoints is not null && attackHeight > 0 && attack.Length == attackWidth * attackHeight)
        {
            // (each clip is drawn at its own unit: its frame less its top headroom fills the same box, UiKit.DrawScale)
            var scale = (attackHeight - TopRow(attack, attackWidth, attackHeight)) / (float)Math.Max(1, height - TopRow(idle, width, height));
            signals.OtherAlpha = Aligned(attack, attackWidth, attackHeight, attackPoints, strip.Size, anchor, scale);
            signals.OtherFrames = attackWidth / attackHeight;
        }
        return new CurseHostData(look, burn, strip.Size, strip.Frames, strip.Content(), anchor,
                                 Seat(strip, points, look, burn, odd: false, signals), Seat(strip, points, look, burn, odd: true, signals));
    }

    /// <summary>
    /// Another strip's alpha (one row of square frames, any frame size: an extended attack canvas too) placed in the idle
    /// frame's space, frame by frame, so that each frame's authored body point lands on the idle strip's mean point;
    /// <paramref name="scale"/> is that strip's source pixels per idle source pixel (as the two are drawn).
    /// </summary>
    internal static byte[] Aligned(ReadOnlySpan<Color> other, int width, int height, MarkPoints points, int size, Vector2 anchor, float scale = 1f)
    {
        var frames = width / height;
        var o = new byte[size * size * frames];
        for (var f = 0; f < frames; f++)
        {
            var p = points.Points[Math.Min(f, points.Points.Count - 1)] * height;
            for (var y = 0; y < size; y++)
            {
                var ty = (int)MathF.Floor(p.Y + (y + 0.5f - anchor.Y) * scale);
                if (ty < 0 || ty >= height) continue;
                for (var x = 0; x < size; x++)
                {
                    var tx = (int)MathF.Floor(p.X + (x + 0.5f - anchor.X) * scale);
                    if (tx < 0 || tx >= height) continue;
                    o[(f * size + y) * size + x] = other[ty * width + f * height + tx].A;
                }
            }
        }
        return o;
    }

    /// <summary>A strip's top headroom: its first row with any visible pixel (alpha &gt; 8, the draw's own crop rule).</summary>
    internal static int TopRow(ReadOnlySpan<Color> pixels, int width, int height)
    {
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (pixels[y * width + x].A > 8) return y;
        return 0;
    }

    /// <summary>
    /// Seats one creature (one slot parity) on its idle strip: the territories' offsets from the strip's mean authored body
    /// point, their diameters and Ash-Burn shares. The art's own facing is canonical (enemies are drawn unflipped, head at
    /// the left); <paramref name="odd"/> is the deliberate slot-parity variety of the first seat's pull.
    /// </summary>
    internal static Layout Seat(HostStrip idle, MarkPoints points, HostLook look, float burn, bool odd, SeatSignals? signals = null)
    {
        var size = idle.Size;
        var anchor = points.MeanPoint * size;
        var content = idle.Content();
        var d0 = CanonicalDiameter(new Point(content.Width, content.Height));
        // the first territory's pull: a little off the body's centre line toward its back (away from the face, which the
        // art draws at the left), the odd slots a little toward the front instead
        var wideBody = content.Width > content.Height * 1.05f;
        var target = anchor + new Vector2((wideBody ? 0f : odd ? -0.5f : 1f) * 0.08f * content.Width, -0.02f * content.Height);
        var sizes = new float[MaxTerritories];
        for (var k = 0; k < MaxTerritories; k++) sizes[k] = d0 * TerritoryShare[k];
        List<Vector4>? heads = null;
        if (points.Heads.Count > 0)
        {
            heads = new List<Vector4>(points.Heads.Count);
            foreach (var hb in points.Heads) heads.Add(hb * size);
        }
        var seats = TerritorySeats(idle.Alpha, size, size, target, sizes, headAtLeft: true, idle.Change, idle.Frames, heads, signals);
        var lay = new Layout { Available = seats.Count };
        var paleOff = 1f - CurseHost.PaleHost(look.Luma);
        for (var k = 0; k < seats.Count; k++)
        {
            lay.Offset[k] = seats[k] - anchor;
            lay.Diameter[k] = sizes[k];
            // where the host's OWN colour under this territory is blue-violet, the curse's violet competes: it burns there
            var compete = CompeteShare(idle.Alpha, idle.Compete, size, size, idle.Frames, seats[k], 0.5f * sizes[k]);
            lay.Burn[k] = Math.Max(burn, paleOff * 0.8f * CurseHost.Ramp(compete, 0.25f, 0.55f));
        }
        return lay;
    }

    /// <summary>The share of the host's opaque pixels within <paramref name="r"/> of <paramref name="c"/> (frame-local, over
    /// every frame) whose own colour is blue-violet.</summary>
    internal static float CompeteShare(byte[] alpha, byte[] competes, int w, int h, int frames, Vector2 c, float r)
    {
        long opaque = 0, compete = 0;
        int x0 = Math.Max(0, (int)(c.X - r)), x1 = Math.Min(w - 1, (int)(c.X + r));
        int y0 = Math.Max(0, (int)(c.Y - r)), y1 = Math.Min(h - 1, (int)(c.Y + r));
        var r2 = r * r;
        for (var f = 0; f < frames; f++)
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    if ((x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) > r2) continue;
                    var i = (f * h + y) * w + x;
                    if (alpha[i] < 128) continue;
                    opaque++;
                    compete += competes[i];
                }
        return opaque == 0 ? 0f : compete / (float)opaque;
    }

    /// <summary>The thickness of a silhouette on a coarse grid: each cell's chamfer distance to the outline (0 outside).</summary>
    internal static float[] Thickness(byte[] alpha, int w, int h, out int gw, out int gh, out int cell, out float max)
    {
        cell = Math.Max(2, Math.Max(w, h) / 96);
        gw = (w + cell - 1) / cell;
        gh = (h + cell - 1) / cell;
        var d = new float[gw * gh];
        for (var cy = 0; cy < gh; cy++)
            for (var cx = 0; cx < gw; cx++)
            {
                int solid = 0, all = 0;
                for (var y = cy * cell; y < Math.Min(h, cy * cell + cell); y++)
                    for (var x = cx * cell; x < Math.Min(w, cx * cell + cell); x++)
                    {
                        all++;
                        if (alpha[y * w + x] >= 128) solid++;
                    }
                d[cy * gw + cx] = solid * 2 > all ? float.MaxValue : 0f;
            }
        max = Chamfer(d, gw, gh);
        return d;
    }

    /// <summary>Turns a grid of inside (MaxValue) and outside (0) cells into each cell's chamfer distance to the outline;
    /// returns the largest.</summary>
    internal static float Chamfer(float[] d, int gw, int gh)
    {
        float At(int x, int y) => x < 0 || y < 0 || x >= gw || y >= gh ? 0f : d[y * gw + x];
        for (var y = 0; y < gh; y++)
            for (var x = 0; x < gw; x++)
            {
                var i = y * gw + x;
                if (d[i] == 0f) continue;
                d[i] = Math.Min(d[i], Math.Min(Math.Min(At(x - 1, y), At(x, y - 1)) + 1f, Math.Min(At(x - 1, y - 1), At(x + 1, y - 1)) + 1.414f));
            }
        for (var y = gh - 1; y >= 0; y--)
            for (var x = gw - 1; x >= 0; x--)
            {
                var i = y * gw + x;
                if (d[i] == 0f) continue;
                d[i] = Math.Min(d[i], Math.Min(Math.Min(At(x + 1, y), At(x, y + 1)) + 1f, Math.Min(At(x + 1, y + 1), At(x - 1, y + 1)) + 1.414f));
            }
        var max = 0f;
        foreach (var v in d) max = Math.Max(max, v);
        return max;
    }

    /// <summary>
    /// The point nearest <paramref name="p"/> (source pixels) where the silhouette is at least <paramref name="share"/>
    /// as thick as at its thickest; <paramref name="p"/> itself when it is already that deep in the body (on a wrist, a
    /// hand or a weapon the curse read as the creature's own magic, "a bracelet").
    /// </summary>
    internal static Vector2 ThickNear(byte[] alpha, int w, int h, Vector2 p, float share)
    {
        var d = Thickness(alpha, w, h, out var gw, out var gh, out var cell, out var max);
        if (max <= 0f) return p;
        var need = share * max;
        int px = (int)(p.X / cell), py = (int)(p.Y / cell);
        if (px >= 0 && py >= 0 && px < gw && py < gh && d[py * gw + px] >= need) return p;
        var best = 0;
        var bestD = float.MaxValue;
        for (var i = 0; i < d.Length; i++)
        {
            if (d[i] < need) continue;
            var dx = (i % gw + 0.5f) * cell - p.X;
            var dy = (i / gw + 0.5f) * cell - p.Y;
            if (dx * dx + dy * dy < bestD) { bestD = dx * dx + dy * dy; best = i; }
        }
        return new Vector2((best % gw + 0.5f) * cell, (best / gw + 0.5f) * cell);
    }

    /// <summary>The rules (0 strict, 1 gentle, 2 loose) and sizes a later territory tries, in order, when the body has no
    /// room for it whole.</summary>
    private static readonly (int Level, float Scale)[] Attempts =
    {
        (0, 1f), (0, 0.82f), (0, 0.67f), (0, 0.55f), (0, 0.45f),
        (1, 0.82f), (1, 0.67f), (1, 0.55f), (1, 0.45f),
        (2, 0.67f), (2, 0.55f), (2, 0.45f),
    };

    /// <summary>
    /// THE TERRITORIES' SEATS on a silhouette (source pixels of one frame; <paramref name="alpha"/> and
    /// <paramref name="change"/> hold <paramref name="frames"/> frames of <paramref name="w"/> x <paramref name="h"/>, one
    /// after another). Only the STABLE body counts: the cells solid in three quarters of the frames (a claw, a sleeve, a
    /// weapon or a wing that moves never holds a territory). The first, the anchor of the whole state, in the body's
    /// central MASS (thick, far from the extremities, where the host visibly changes, a weak pull toward
    /// <paramref name="first"/>); each next one ELSEWHERE on the body (in its thick parts, a gap clear of every earlier
    /// territory, about a territory and a third away, never far from its centre), on an upright body the third never in
    /// a row with the first two (three in a line drew a band again: "a sash"); none on the head (the authored
    /// <paramref name="heads"/> boxes, else the leading fifth of a wide body or the top of a tall one's mass) or on the
    /// feet. A later territory the body has no room for at its full size takes a smaller diameter (written back into
    /// <paramref name="diameters"/>); fewer seats when the body has no room even then.
    /// </summary>
    internal static List<Vector2> TerritorySeats(byte[] alpha, int w, int h, Vector2 first, float[] diameters, bool headAtLeft,
                                                 byte[]? change = null, int frames = 1, IReadOnlyList<Vector4>? heads = null,
                                                 SeatSignals? signals = null)
    {
        var seats = new List<Vector2>();
        var cell = Math.Max(2, Math.Max(w, h) / 96);
        int gw = (w + cell - 1) / cell, gh = (h + cell - 1) / cell;
        // THE STABLE BODY: each cell's count of frames it is solid in
        var solid = new int[gw * gh];
        var visSum = new float[gw * gh];
        var visCount = new int[gw * gh];
        var used = 0;
        for (var f = 0; f < frames; f++)
        {
            var any = false;
            var o = f * w * h;
            for (var cy = 0; cy < gh; cy++)
                for (var cx = 0; cx < gw; cx++)
                {
                    int opaque = 0, all = 0;
                    for (var y = cy * cell; y < Math.Min(h, cy * cell + cell); y++)
                        for (var x = cx * cell; x < Math.Min(w, cx * cell + cell); x++)
                        {
                            all++;
                            var i = o + y * w + x;
                            if (alpha[i] < 128) continue;
                            opaque++;
                            if (change is null) continue;
                            visSum[cy * gw + cx] += change[i];
                            visCount[cy * gw + cx]++;
                        }
                    if (opaque * 2 <= all) continue;
                    solid[cy * gw + cx]++;
                    any = true;
                }
            if (any) used++;
        }
        if (used == 0) return seats;
        var d = new float[gw * gh];
        for (var i = 0; i < d.Length; i++) d[i] = solid[i] >= 0.75f * used ? float.MaxValue : 0f;
        var max = Chamfer(d, gw, gh);
        if (max <= 0f) return seats;
        // WHERE THE HOST VISIBLY CHANGES: each cell's mean change under the drain (0..1 of the host's most). A territory
        // on a dark sash, an inner tunic or trousers barely changed and read as part of the costume ("a pattern on the
        // robe", "a sash"); where the host loses its colour or brightness it reads as damage
        var vis = new float[gw * gh];
        if (change is not null)
        {
            for (var i = 0; i < vis.Length; i++) vis[i] = visCount[i] == 0 ? 0f : visSum[i] / visCount[i];
            // (a territory's worth of neighbourhood, so one bright pixel never wins)
            var smooth = new float[vis.Length];
            var r = Math.Max(1, (int)(diameters[0] * 0.25f / cell));
            for (var cy = 0; cy < gh; cy++)
                for (var cx = 0; cx < gw; cx++)
                {
                    float sum = 0f;
                    var cnt = 0;
                    for (var y = Math.Max(0, cy - r); y <= Math.Min(gh - 1, cy + r); y++)
                        for (var x = Math.Max(0, cx - r); x <= Math.Min(gw - 1, cx + r); x++)
                        {
                            if (d[y * gw + x] <= 0f) continue;
                            sum += vis[y * gw + x];
                            cnt++;
                        }
                    smooth[cy * gw + cx] = cnt == 0 ? 0f : sum / cnt;
                }
            var visMax = 0f;
            foreach (var v in smooth) visMax = Math.Max(visMax, v);
            for (var i = 0; i < vis.Length; i++) vis[i] = visMax > 0f ? smooth[i] / visMax : 0f;
        }
        // the whole stable body (its hem and feet), and its MASS (the cells a fifth as thick as the thickest: no thin
        // sleeve, wing tip, antler or tail stretches it)
        int minX = gw, maxX = -1, minY = gh, maxY = -1, mX0 = gw, mX1 = -1, mY0 = gh, mY1 = -1;
        for (var i = 0; i < d.Length; i++)
        {
            if (d[i] <= 0f) continue;
            int x = i % gw, y = i / gw;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            if (d[i] < 0.2f * max) continue;
            mX0 = Math.Min(mX0, x); mX1 = Math.Max(mX1, x); mY0 = Math.Min(mY0, y); mY1 = Math.Max(mY1, y);
        }
        float bw = maxX - minX + 1, bh = maxY - minY + 1;
        float mh = mY1 - mY0 + 1;
        var wide = bw > bh * 1.05f;
        var column = 0.22f;
        var authored = heads is { Count: > 0 };
        Vector2 At(int i) => new((i % gw + 0.5f) * cell, (i / gw + 0.5f) * cell);
        bool OnHead(int i, float margin)
        {
            var c = At(i);
            foreach (var hb in heads!)
                if (c.X > hb.X - margin && c.X < hb.Z + margin && c.Y > hb.Y - margin && c.Y < hb.W + margin) return true;
            return false;
        }
        // (an upright body's chin: nothing beside the head either, a raised wing, antlers or a shoulder spike)
        var chin = 0f;
        if (authored)
        {
            foreach (var hb in heads!) chin += hb.W;
            chin /= heads.Count;
        }
        // the body's centre (its thickest core, the head left out: a whelp's round head is its thickest part, an angel's
        // wing roots pulled a plain centroid sideways) and its reach (the radius of a disc as large as its mass)
        var centre = Vector2.Zero;
        var reach = 1f;
        {
            double sx = 0, sy = 0, sw = 0;
            var mass = 0;
            for (var i = 0; i < d.Length; i++)
            {
                if (d[i] < 0.2f * max || (authored && OnHead(i, 0f))) continue;
                mass++;
                if (d[i] < 0.5f * max) continue;
                var c = At(i);
                var wgt = (double)d[i] * d[i] * d[i];
                sx += c.X * wgt; sy += c.Y * wgt; sw += wgt;
            }
            centre = sw > 0 ? new Vector2((float)(sx / sw), (float)(sy / sw)) : At(Array.IndexOf(d, max));
            // (never under the first territory's own size: a small body is mostly that territory)
            reach = Math.Max(MathF.Sqrt(Math.Max(1, mass) / MathF.PI) * cell, 0.6f * diameters[0]);
        }
        // an upright body's torso height (from its chin, when it is authored): its column is a share of it
        var torso = authored ? Math.Max(0.5f * mh * cell, (mY1 + 1) * cell - Math.Max(mY0 * cell, chin)) : mh * cell;
        bool Excluded(int x, int y)
        {
            // the hem, the feet, a beast's legs (a territory there read as "a stain on the hem")
            if (y > maxY - (wide ? 0.25f : 0.2f) * bh) return true;
            // the authored HEAD of every frame, and a territory's reach around it (on the face it read as face paint, on
            // a beast's head as its markings)
            if (authored && OnHead(y * gw + x, 0.15f * diameters[0])) return true;
            if (wide) return !authored && (headAtLeft ? x < minX + 0.22f * bw : x > maxX - 0.22f * bw);
            // an upright body: below the head and hood (a territory landed on the Matron's face), and inside its torso's
            // column (on an outstretched sleeve, a wing or a weapon it read as the creature's own magic); the column is
            // a share of the torso's HEIGHT round the thick core's centre (wings, fists and a held staff widened the
            // mass itself, and a column of its width held them)
            return y < mY0 + (authored ? 0.15f : 0.28f) * mh || (authored && (y + 0.5f) * cell < chin)
                   || (column > 0f && MathF.Abs((x + 0.5f) * cell - centre.X) > Math.Max(column * torso, 0.5f * reach));
        }
        var persistence = Persistence(w, h, cell, gw, gh, solid, used, diameters[0], signals);
        // (a cell that is body in the idle but gone in most of the other frames is a limb that swings away: a club arm
        // hanging still beside the body, raised in the attack; a territory there hangs in the air when it lunges)
        bool Fleeting(int i) => persistence is { } p && p.Raw[i] < MinPersistence;
        // (an upright body's TORSO, from under its head to above its hem: the first leans to its upper half, the chest.
        // A long robe's flared skirt is its thickest, most central mass, so on thickness and centrality alone the first
        // took the waist cloth hanging below a sash and read as part of the costume; the chest's change reads as damage
        // to the creature itself. Body that stays body through the attack holds it best, and its whole disc, not only
        // its centre, keeps off the authored head)
        var torsoTop = authored ? chin : (mY0 + 0.28f * mh) * cell;
        var torsoBottom = (maxY - 0.2f * bh) * cell;
        var torsoMid = 0.5f * (torsoTop + torsoBottom);
        var torsoHalf = Math.Max(cell, 0.5f * (torsoBottom - torsoTop));
        var discR = Math.Max(1, (int)(0.5f * diameters[0] / cell));
        // (the share of the first territory's disc over the authored head: its centre kept off the head, a leaning-up
        // seat still spread over the face)
        float HeadShare(int i)
        {
            int cx = i % gw, cy = i / gw, on = 0, all = 0;
            for (var dy = -discR; dy <= discR; dy++)
                for (var dx = -discR; dx <= discR; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (dx * dx + dy * dy > discR * discR || x < 0 || y < 0 || x >= gw || y >= gh) continue;
                    all++;
                    if (OnHead(y * gw + x, 0f)) on++;
                }
            return all == 0 ? 0f : on / (float)all;
        }
        // THE FIRST: the anchor of the whole state, so the body's stable central MASS; a hand, a claw, a weapon, a wing,
        // a sleeve or antlers never hold it (on the Matron's claw it read as "a spell it is charging")
        {
            var far = 1f;
            for (var i = 0; i < d.Length; i++)
                if (d[i] >= 0.5f * max && !Excluded(i % gw, i / gw)) far = Math.Max(far, Vector2.Distance(At(i), centre));
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < d.Length; i++)
            {
                if (d[i] < 0.5f * max || Excluded(i % gw, i / gw) || Fleeting(i)) continue;
                var c = At(i);
                // (an upright body's first leans to its CHEST: at the hip it took the hand hanging in front of it, the
                // Matron's claw, the Reaper's grip on its scythe)
                var score = 0.45f * d[i] / max + 0.35f * (1f - Vector2.Distance(c, centre) / far)
                            + (change is null ? 0f : 0.5f * vis[i]) - 0.12f * Vector2.Distance(c, first) / diameters[0]
                            + (wide ? 0f : 0.2f * Math.Clamp((centre.Y - c.Y) / reach, -1f, 1f))
                            + (wide || signals is null ? 0f : UpperWeight * Math.Clamp((torsoMid - c.Y) / torsoHalf, -1f, 1f))
                            + (persistence is { } persist ? PersistWeight * persist.Smooth[i] : 0f)
                            - (authored && signals is not null ? HeadWeight * Math.Max(0f, HeadShare(i) - HeadTolerance) : 0f);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0) seats.Add(At(best));
        }
        if (seats.Count == 0)
        {
            for (var pass = 0; pass < 2 && seats.Count == 0; pass++)
            {
                var need = (pass == 0 ? 0.6f : 0.35f) * max;
                var best = -1;
                var bestD = float.MaxValue;
                for (var i = 0; i < d.Length; i++)
                {
                    if (d[i] < need || Excluded(i % gw, i / gw)) continue;
                    var dd = Vector2.DistanceSquared(At(i), first);
                    if (dd < bestD) { bestD = dd; best = i; }
                }
                if (best >= 0) seats.Add(At(best));
            }
            if (seats.Count == 0) seats.Add(first);
        }
        // THE OTHERS: elsewhere on the body, in its mass (reaching for the gap alone, they landed on a wing, a fist, a
        // hand on a staff). A body too small or too slim for a territory of its full size holds a smaller one (a slim
        // angel's second territory went to its wing, a small whelp held only one); a THIN body (a skeleton's ribs and
        // pelvis) that still cannot hold three gets a gentler pass (thinner parts, a smaller gap, a wider column), so
        // depth 3 always shows more of the body than depth 2. The second is chosen so that a third still fits (a second
        // straight above the first left no room for a third off their line)
        for (var k = 1; k < Math.Min(diameters.Length, MaxTerritories) && seats.Count == k; k++)
            if (!Place(k, k + 1 < Math.Min(diameters.Length, 3))) break;
        return seats;

        // the best cell for territory k under the strict (0), gentle (1) or loose (2: anywhere thick enough on the body,
        // a small or sideways body's last resort) rules (-1: none); every candidate when asked
        int Search(int k, int level, List<(int I, float Score)>? all)
        {
            var minThick = level == 0 ? 0.4f : 0.22f;
            var minGap = level == 0 ? 0.95f : 0.8f;
            var minArea = level == 0 ? 0.1f : 0.06f;
            var maxOut = level == 0 ? 1.3f : level == 1 ? 1.45f : 1.7f;
            column = level == 0 ? 0.22f : level == 1 ? 0.26f : 0f;
            var best = -1;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < d.Length; i++)
            {
                if (d[i] < minThick * max || Excluded(i % gw, i / gw) || Fleeting(i)) continue;
                var c = At(i);
                var out_ = Vector2.Distance(c, centre) / reach;
                if (out_ > maxOut) continue;
                var ok = true;
                var near = float.MaxValue;
                for (var j = 0; j < seats.Count; j++)
                {
                    var gap = Vector2.Distance(c, seats[j]) / (0.5f * (diameters[j] + diameters[k]));
                    if (gap < minGap) { ok = false; break; }
                    near = Math.Min(near, gap);
                }
                if (!ok) continue;
                if (seats.Count == 2 && !wide)
                {
                    // never three in a row on an upright body: the triangle's area against its longest side, squared
                    // (on a wide beast the body itself is the row; three in a line down a torso drew "a sash")
                    var a0 = seats[0];
                    var b0 = seats[1];
                    var area = MathF.Abs((b0.X - a0.X) * (c.Y - a0.Y) - (b0.Y - a0.Y) * (c.X - a0.X)) * 0.5f;
                    var side = MathF.Max(Vector2.DistanceSquared(a0, b0), MathF.Max(Vector2.DistanceSquared(a0, c), Vector2.DistanceSquared(b0, c)));
                    if (area / side < minArea) continue;
                }
                var score = 0.5f * d[i] / max - 0.5f * MathF.Abs(near - 1.3f) + 0.6f * vis[i] - 0.9f * out_
                            + (persistence is { } stays ? LaterPersistWeight * stays.Smooth[i] : 0f);
                all?.Add((i, score));
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        // seats territory k (smaller when it must; the gentle and loose rules only up to the third, and smaller); with a
        // look-ahead, only where the next one still fits, by the strictest rules it can
        bool Place(int k, bool lookahead, int maxLevel = 2)
        {
            var full = diameters[k];
            int fallback = -1;
            float fallbackSize = full;
            foreach (var (level, scale) in Attempts)
            {
                if (level > maxLevel || (level > 0 && k >= 3)) break;
                diameters[k] = full * scale;
                if (!lookahead)
                {
                    var b = Search(k, level, null);
                    if (b < 0) continue;
                    seats.Add(At(b));
                    return true;
                }
                var all = new List<(int I, float Score)>();
                Search(k, level, all);
                if (all.Count == 0) continue;
                all.Sort((x, y) => y.Score.CompareTo(x.Score));
                if (fallback < 0) { fallback = all[0].I; fallbackSize = diameters[k]; }
                var next = diameters[k + 1];
                for (var nextLevel = 1; nextLevel <= 2; nextLevel++)
                    for (var n = 0; n < Math.Min(30, all.Count); n++)
                    {
                        seats.Add(At(all[n].I));
                        var fits = Place(k + 1, false, nextLevel);
                        diameters[k + 1] = next;
                        // (the probe seat k + 1 comes off; on success the candidate for k stays at index k)
                        if (fits) seats.RemoveAt(seats.Count - 1);
                        if (fits) return true;
                        seats.RemoveAt(seats.Count - 1);
                    }
            }
            if (fallback < 0)
            {
                diameters[k] = full;
                return false;
            }
            diameters[k] = fallbackSize;
            seats.Add(At(fallback));
            return true;
        }
    }

    /// <summary>How much the first seat leans to the upper half of an upright torso.</summary>
    internal const float UpperWeight = 0.4f;

    /// <summary>How much the first seat prefers body that stays body in the creature's other frames.</summary>
    internal const float PersistWeight = 0.3f;

    /// <summary>How much a later territory prefers body that stays body in the other frames (a sleeve that swings out
    /// in the attack never holds one).</summary>
    internal const float LaterPersistWeight = 0.6f;

    /// <summary>No territory on a cell solid in less than this share of all the frames, the idle's and the other strip's.</summary>
    internal const float MinPersistence = 0.55f;

    /// <summary>How much the first seat keeps its whole disc off the authored head.</summary>
    internal const float HeadWeight = 2f;

    /// <summary>The share of the first disc that may cross the head (its chin, a collar) unpenalised.</summary>
    internal const float HeadTolerance = 0.05f;

    /// <summary>
    /// PERSISTENCE on the seating grid (0..1, or null without other frames): the share of every frame, the idle's and
    /// the other strip's, each cell is solid in, over a territory's neighbourhood (a disc half on a part that swings away
    /// in the attack is half gone, and its curse would hang in the air there).
    /// </summary>
    private static (float[] Raw, float[] Smooth)? Persistence(int w, int h, int cell, int gw, int gh, int[] solid, int used, float d0, SeatSignals? signals)
    {
        if (signals?.OtherAlpha is not { } other || signals.OtherFrames <= 0) return null;
        var count = new float[gw * gh];
        var otherUsed = 0;
        for (var f = 0; f < signals.OtherFrames; f++)
        {
            var any = false;
            var o = f * w * h;
            for (var cy = 0; cy < gh; cy++)
                for (var cx = 0; cx < gw; cx++)
                {
                    int opaque = 0, all = 0;
                    for (var y = cy * cell; y < Math.Min(h, cy * cell + cell); y++)
                        for (var x = cx * cell; x < Math.Min(w, cx * cell + cell); x++)
                        {
                            all++;
                            if (other[o + y * w + x] >= 128) opaque++;
                        }
                    if (opaque * 2 <= all) continue;
                    count[cy * gw + cx]++;
                    any = true;
                }
            if (any) otherUsed++;
        }
        var raw = new float[gw * gh];
        for (var i = 0; i < raw.Length; i++) raw[i] = (solid[i] + count[i]) / Math.Max(1, used + otherUsed);
        // (a territory's worth of neighbourhood)
        var r = Math.Max(1, (int)(d0 * 0.4f / cell));
        var smooth = new float[raw.Length];
        for (var cy = 0; cy < gh; cy++)
            for (var cx = 0; cx < gw; cx++)
            {
                float sum = 0f;
                var cnt = 0;
                for (var y = Math.Max(0, cy - r); y <= Math.Min(gh - 1, cy + r); y++)
                    for (var x = Math.Max(0, cx - r); x <= Math.Min(gw - 1, cx + r); x++)
                    {
                        sum += raw[y * gw + x];
                        cnt++;
                    }
                smooth[cy * gw + cx] = sum / cnt;
            }
        return (raw, smooth);
    }
}
