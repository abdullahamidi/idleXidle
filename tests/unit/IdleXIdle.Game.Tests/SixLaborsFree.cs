using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A tiny CPU PNG reader for the tests (8-bit RGBA, non-interlaced: the strips and atlases the pipeline writes), shared by
/// the mark tests and BRAND's host bake (<c>brand_host_bake_test</c>), which reads every creature's idle strip with it.
/// </summary>
internal sealed class SixLaborsFree : IDisposable
{
    private readonly byte[] _rgba;
    private readonly int _w;

    private SixLaborsFree(byte[] rgba, int w) { _rgba = rgba; _w = w; }

    public int Width => _w;

    public int Height => _rgba.Length / 4 / _w;

    /// <summary>Every pixel PREMULTIPLIED exactly as <c>AssetLibrary.Premultiply</c> does at load
    /// (<see cref="Color.FromNonPremultiplied(int, int, int, int)"/>), row-major: the numbers the game itself holds.</summary>
    public Color[] Premultiplied()
    {
        var c = new Color[_rgba.Length / 4];
        for (var i = 0; i < c.Length; i++)
            c[i] = Color.FromNonPremultiplied(_rgba[i * 4], _rgba[i * 4 + 1], _rgba[i * 4 + 2], _rgba[i * 4 + 3]);
        return c;
    }

    /// <summary>Every pixel's opacity (alpha above 100, the probes' and the authoring tool's rule), row-major.</summary>
    public bool[] Opaque()
    {
        var o = new bool[_rgba.Length / 4];
        for (var i = 0; i < o.Length; i++) o[i] = _rgba[i * 4 + 3] > 100;
        return o;
    }

    /// <summary>The pixels of square frame <paramref name="k"/> brighter than <paramref name="luma"/> (an eye's white speck), in
    /// the frame's own px, from its upper 70 %.</summary>
    public IEnumerable<(int X, int Y)> BrightSpecks(int k, int size, int luma)
    {
        for (var y = 0; y < Height * 7 / 10; y++)
            for (var x = 0; x < size; x++)
            {
                var i = (y * _w + k * size + x) * 4;
                if (_rgba[i + 3] > 200 && (_rgba[i] * 3 + _rgba[i + 1] * 6 + _rgba[i + 2]) / 10 > luma)
                    yield return (x, y);
            }
    }

    /// <summary>The opaque centroid of square frame <paramref name="k"/>, in the frame's own px.</summary>
    public Vector2 Centroid(int k, int size)
    {
        double sx = 0, sy = 0;
        var n = 0;
        for (var y = 0; y < Height; y += 2)
            for (var x = 0; x < size; x += 2)
                if (_rgba[(y * _w + k * size + x) * 4 + 3] > 100) { sx += x; sy += y; n++; }
        return n == 0 ? default : new Vector2((float)(sx / n), (float)(sy / n));
    }

    /// <summary>Whether square frames <paramref name="a"/> and <paramref name="b"/> of a strip are the same picture.</summary>
    public bool SameFrame(int a, int b, int size)
    {
        for (var y = 0; y < Height; y++)
            if (!_rgba.AsSpan((y * _w + a * size) * 4, size * 4).SequenceEqual(_rgba.AsSpan((y * _w + b * size) * 4, size * 4)))
                return false;
        return true;
    }

    /// <summary>The first and last opaque rows of the square frame from column <paramref name="x0"/>.</summary>
    public (int Top, int Bottom) OpaqueRows(int x0, int size)
    {
        int top = -1, bottom = -1;
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < size; x++)
                if (_rgba[(y * _w + x0 + x) * 4 + 3] > 100)
                {
                    if (top < 0) top = y;
                    bottom = y;
                    break;
                }
        return (top, bottom);
    }

    public static SixLaborsFree Load(string path)
    {
        var png = File.ReadAllBytes(path);
        int Be(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
        var w = Be(16);
        var h = Be(20);
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);
        Assert.Equal(0, png[28]);   // (non-interlaced)
        using var idat = new MemoryStream();
        for (var at = 8; at < png.Length;)
        {
            var len = Be(at);
            var type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            if (type == "IDAT") idat.Write(png, at + 8, len);
            at += 12 + len;
        }
        idat.Position = 2;   // the zlib header
        using var z = new DeflateStream(idat, CompressionMode.Decompress);
        var raw = new byte[(w * 4 + 1) * h];
        var read = 0;
        while (read < raw.Length)
        {
            var n = z.Read(raw, read, raw.Length - read);
            if (n <= 0) break;
            read += n;
        }
        var rgba = new byte[w * h * 4];
        var stride = w * 4;
        for (var y = 0; y < h; y++)
        {
            var f = raw[y * (stride + 1)];
            for (var x = 0; x < stride; x++)
            {
                var v = raw[y * (stride + 1) + 1 + x];
                var a = x >= 4 ? rgba[y * stride + x - 4] : 0;
                var b = y > 0 ? rgba[(y - 1) * stride + x] : 0;
                var c = x >= 4 && y > 0 ? rgba[(y - 1) * stride + x - 4] : 0;
                int pr = f switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                };
                rgba[y * stride + x] = (byte)(v + pr);
            }
        }
        return new SixLaborsFree(rgba, w);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    /// <summary>A square cell: its alpha and its grey (the red channel of the white-tinted art).</summary>
    public (byte[] A, byte[] L) Cell(int x0, int y0, int size)
    {
        var a = new byte[size * size];
        var l = new byte[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = ((y0 + y) * _w + x0 + x) * 4;
                a[y * size + x] = _rgba[i + 3];
                l[y * size + x] = _rgba[i];
            }
        return (a, l);
    }

    /// <summary>Writes an 8-bit RGBA PNG (row-major, straight alpha): the tests' contact sheets.</summary>
    public static void Save(string path, byte[] rgba, int w, int h)
    {
        var raw = new byte[(w * 4 + 1) * h];
        for (var y = 0; y < h; y++) Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
        using var zipped = new MemoryStream();
        using (var z = new ZLibStream(zipped, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        using var f = File.Create(path);
        f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, w);
        WriteBe(ihdr, 4, h);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(f, "IHDR", ihdr);
        Chunk(f, "IDAT", zipped.ToArray());
        Chunk(f, "IEND", Array.Empty<byte>());
    }

    private static void WriteBe(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24);
        b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8);
        b[at + 3] = (byte)v;
    }

    private static void Chunk(Stream f, string type, byte[] data)
    {
        var head = new byte[8];
        WriteBe(head, 0, data.Length);
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        Buffer.BlockCopy(t, 0, head, 4, 4);
        f.Write(head);
        f.Write(data);
        var crc = 0xFFFFFFFFu;
        foreach (var bt in t) crc = Crc(crc, bt);
        foreach (var bt in data) crc = Crc(crc, bt);
        var tail = new byte[4];
        WriteBe(tail, 0, (int)(crc ^ 0xFFFFFFFFu));
        f.Write(tail);
    }

    private static uint Crc(uint crc, byte b)
    {
        crc ^= b;
        for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        return crc;
    }

    public void Dispose() { }
}
