using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation.Curse;

/// <summary>
/// One creature's BAKED BRAND host data (ADR-013 §3): its look and Ash-Burn share, measured from its idle strip, and its
/// two territory layouts (slot parity 0 / 1), seated on the idle strip's stable body. Read from
/// <c>&lt;idle strip&gt;.brand.json</c> beside the strip, written offline by <c>brand_host_bake_test</c> (with
/// <c>RH_BRAND_BAKE=1</c>) from the same C# measuring and seating code; the game never reads a texture back for it.
/// </summary>
/// <remarks>
/// <para>
/// Every number is in the idle strip's SOURCE pixels. A seat is an offset from the strip's MEAN authored body point
/// (<see cref="MarkPoints.MeanPoint"/>) in the art's own facing (unflipped); at runtime each territory hangs from the
/// current frame's own authored body point and is mirrored with the frame. Attack and death strips use their creature's
/// idle data.
/// </para>
/// <para>
/// SIZE: the baked diameters are the scale-free share of the strip's own figure (<see cref="CurseSeating.CanonicalDiameter"/>);
/// on screen they are multiplied by the frame's scale and then by <see cref="CurseSeating.ScreenClamp"/> of the first
/// territory's screen diameter (offsets are not clamped). Only the widest bosses at the largest UI scales reach the clamp.
/// </para>
/// </remarks>
public sealed class CurseHostData
{
    private static Dictionary<string, CurseHostData>? _byStrip;

    /// <summary>The host's look, measured over every frame of its idle strip.</summary>
    public HostLook Look { get; }

    /// <summary>The host's own Ash-Burn share (0 Shadow Violet .. 1 Ash-Burn): <see cref="CurseHost.AshBurn"/> of its look.</summary>
    public float Burn { get; }

    /// <summary>The idle strip's square frame side (source pixels).</summary>
    public int FrameSize { get; }

    /// <summary>The idle strip's frame count.</summary>
    public int Frames { get; }

    /// <summary>The idle strip's figure: its opaque bounds over every frame (frame pixels).</summary>
    public Rectangle Content { get; }

    /// <summary>The mean authored body point the seats hang from (frame pixels).</summary>
    public Vector2 Anchor { get; }

    /// <summary>The territory layouts for an even (0) and an odd (1) slot.</summary>
    public IReadOnlyList<Layout> Layouts { get; }

    /// <summary>A creature's baked host data.</summary>
    public CurseHostData(HostLook look, float burn, int frameSize, int frames, Rectangle content, Vector2 anchor, Layout even, Layout odd)
    {
        Look = look;
        Burn = burn;
        FrameSize = frameSize;
        Frames = frames;
        Content = content;
        Anchor = anchor;
        Layouts = new[] { even, odd };
    }

    /// <summary>The layout for the creature in <paramref name="slot"/> (by its parity: a row of one creature is never a stamp).</summary>
    public Layout LayoutFor(int slot) => Layouts[slot & 1];

    /// <summary>The data baked for the idle strip <paramref name="idleStripKey"/>, or null (no file: that creature is
    /// never cursed visibly). Allocates nothing once warm.</summary>
    public static CurseHostData? For(string idleStripKey)
    {
        Warm();
        return _byStrip!.GetValueOrDefault(idleStripKey);
    }

    /// <summary>Reads every baked file once, at load (like <see cref="MarkPoints.Warm"/>), never on a Draw frame.</summary>
    public static void Warm() => _byStrip ??= Load(Path.Combine(AppContext.BaseDirectory, "assets", "art"));

    /// <summary>Read every <c>*.brand.json</c> under <paramref name="root"/>, keyed by the strip name it sits beside.</summary>
    public static Dictionary<string, CurseHostData> Load(string root)
    {
        var map = new Dictionary<string, CurseHostData>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return map;
        foreach (var path in Directory.EnumerateFiles(root, "*" + Extension, SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            try { map[name[..^Extension.Length]] = Parse(File.ReadAllText(path)); }
            catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException
                                           or IndexOutOfRangeException or IOException or UnauthorizedAccessException)
            {
                // a broken file must not take the fight down: that creature shows no curse, loudly
                Console.Error.WriteLine($"brand host data '{name}' ignored: {ex.Message}");
            }
        }
        return map;
    }

    /// <summary>The baked file's extension, beside the idle strip's PNG.</summary>
    public const string Extension = ".brand.json";

    /// <summary>Parse a <c>.brand.json</c> (see <see cref="Format"/>).</summary>
    public static CurseHostData Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var look = root.GetProperty("look");
        var host = new HostLook(look.GetProperty("luma").GetSingle(), look.GetProperty("chroma").GetSingle(),
                                look.GetProperty("violet").GetSingle(), look.GetProperty("shadow").GetSingle());
        var burn = look.GetProperty("burn").GetSingle();
        var frame = root.GetProperty("frame").GetInt32();
        var frames = root.GetProperty("frames").GetInt32();
        var c = root.GetProperty("content");
        var content = new Rectangle(c[0].GetInt32(), c[1].GetInt32(), c[2].GetInt32(), c[3].GetInt32());
        var a = root.GetProperty("anchor");
        var anchor = new Vector2(a[0].GetSingle(), a[1].GetSingle());
        if (frame <= 0 || frames <= 0) throw new FormatException("no frames");
        var layouts = new Layout[2];
        var n = 0;
        foreach (var l in root.GetProperty("layouts").EnumerateArray())
        {
            if (n >= 2) throw new FormatException("more than two layouts");
            var lay = new Layout();
            var k = 0;
            foreach (var s in l.GetProperty("seats").EnumerateArray())
            {
                if (k >= CurseSeating.MaxTerritories) throw new FormatException("more seats than territories");
                lay.Offset[k] = new Vector2(s.GetProperty("x").GetSingle(), s.GetProperty("y").GetSingle());
                lay.Diameter[k] = s.GetProperty("diameter").GetSingle();
                lay.Burn[k] = s.GetProperty("burn").GetSingle();
                if (lay.Diameter[k] <= 0f || lay.Burn[k] is < 0f or > 1f) throw new FormatException($"a bad seat: {s}");
                k++;
            }
            lay.Available = l.GetProperty("available").GetInt32();
            if (lay.Available != k || k == 0) throw new FormatException($"available {lay.Available} against {k} seats");
            layouts[n++] = lay;
        }
        if (n != 2) throw new FormatException("two layouts expected (slot parity 0 and 1)");
        return new CurseHostData(host, burn, frame, frames, content, anchor, layouts[0], layouts[1]);
    }

    /// <summary>
    /// The file's text: every number rounded to four decimals with invariant formatting, so a recomputation is compared
    /// to the committed file byte for byte (<c>brand_host_bake_test</c>).
    /// </summary>
    public static string Format(CurseHostData d, string source)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"look\": {\"luma\": ").Append(N(d.Look.Luma)).Append(", \"chroma\": ").Append(N(d.Look.Chroma))
          .Append(", \"violet\": ").Append(N(d.Look.Violet)).Append(", \"shadow\": ").Append(N(d.Look.Shadow))
          .Append(", \"burn\": ").Append(N(d.Burn)).Append("},\n");
        sb.Append("  \"frame\": ").Append(d.FrameSize).Append(", \"frames\": ").Append(d.Frames).Append(",\n");
        sb.Append("  \"content\": [").Append(d.Content.X).Append(", ").Append(d.Content.Y).Append(", ").Append(d.Content.Width)
          .Append(", ").Append(d.Content.Height).Append("],\n");
        sb.Append("  \"anchor\": [").Append(N(d.Anchor.X)).Append(", ").Append(N(d.Anchor.Y)).Append("],\n");
        sb.Append("  \"layouts\": [\n");
        for (var p = 0; p < 2; p++)
        {
            var lay = d.Layouts[p];
            sb.Append("    {\"available\": ").Append(lay.Available).Append(", \"seats\": [\n");
            for (var k = 0; k < lay.Available; k++)
            {
                sb.Append("      {\"x\": ").Append(N(lay.Offset[k].X)).Append(", \"y\": ").Append(N(lay.Offset[k].Y))
                  .Append(", \"diameter\": ").Append(N(lay.Diameter[k])).Append(", \"burn\": ").Append(N(lay.Burn[k])).Append('}');
                sb.Append(k + 1 < lay.Available ? ",\n" : "\n");
            }
            sb.Append("    ]}").Append(p == 0 ? ",\n" : "\n");
        }
        sb.Append("  ],\n");
        sb.Append("  \"source\": \"").Append(source).Append("\"\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>A number as the file holds it: four decimals, invariant, never "-0".</summary>
    internal static float Round(float v) => MathF.Round(v, 4) + 0f;

    private static string N(float v) => Round(v).ToString("0.####", CultureInfo.InvariantCulture);
}
