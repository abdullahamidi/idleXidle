using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>
/// Shared drawing kit — backgrounds, 9-slice panels, framed bars, key-caps, text.
/// </summary>
/// <remarks>
/// Every screen draws through this so the look is consistent and the flat-shape fallbacks live in one
/// place. When an asset is missing, each method degrades to the old flat rendering, so the game still
/// runs (and looks like the greybox) with zero art. Nothing here decides game state — it only draws.
/// </remarks>
public sealed class UiKit
{
    public const int CanvasWidth = 480;
    public const int CanvasHeight = 270;

    /// <summary>
    /// Counter-scale for native-resolution drawing inside the ×N canvas transform. 4 for screens authored
    /// in 480×270 logical units (the default), 1 for screens authored directly in 1920×1080. Set per-region
    /// by the host before drawing so a source corner of S px still lands 1:1 on the canvas.
    /// </summary>
    public int Scale = 4;

    /// <summary>
    /// THE PAGE: the logical rectangle a menu screen lays out into, in the screen's own 1920-space units.
    /// 1920×1080 at EVERY UI SCALE: the profile is a density (<see cref="UiMetrics"/>) — bigger type,
    /// rows, buttons and paddings inside the same page — not a zoom. (It shrank with the scale in UX V2,
    /// to 1280×720 at 150 %, and every screen laid out for 1080 px overflowed; that is why the step was
    /// withdrawn, and why it is a density now.)
    /// </summary>
    /// <remarks>
    /// Not a layout engine (brief §91): the three anchors below are the numbers screens already subtract
    /// from 1920 and 1080, given one name so that every right edge, bottom edge and centre come from one
    /// place. A screen still writes its top-level regions against these; what it must not write is a
    /// literal row height or button height — those come from <see cref="UiMetrics"/>.
    /// </remarks>
    public static Rectangle Page { get; internal set; } = new(0, 0, 1920, 1080);

    /// <summary>The page's right edge, <paramref name="inset"/> px in.</summary>
    public static int PageRight(int inset) => Page.Right - inset;

    /// <summary>The page's bottom edge, <paramref name="inset"/> px up.</summary>
    public static int PageBottom(int inset) => Page.Bottom - inset;

    /// <summary>The page's horizontal centre — where a screen title sits.</summary>
    public static int PageCenterX => Page.Center.X;

    /// <summary>
    /// The page y a menu screen's FIRST PANEL starts at — the first row under the chrome's band (title,
    /// rule, subtitle, the hint slot). 150 at 100 %; lower at 125 and 150 %, where the band is taller.
    /// </summary>
    /// <remarks>
    /// Every page screen wrote the literal 150 (GEAR and ROSTER wrote 120) with the comment "the hint slot
    /// owns canvas y 86–134". That was true at 100 % only: the hint slot hangs under the subtitle, and at
    /// 150 % the subtitle ends 40 page px lower and the slot's line is a third taller, so the slot sat
    /// across the VAULT's toolbar and the MASTERY tree's POINTS plate (release polish 2026-09-05,
    /// post1_vault_150.png). Set by <c>Game1.ApplyUiScale</c> from the band's own arithmetic; read here
    /// by every screen's top anchor, so the page starts under the chrome at every profile.
    /// </remarks>
    public static int PageTop { get; internal set; } = 150;

    /// <summary>
    /// THE NOTICE LANE (2026-09-06): the height a menu screen's page notice takes above the body, in
    /// page pixels — zero while none is showing. The host sets it every frame and the screens lay out
    /// from <see cref="PageTop"/>, which already includes it, so a notice pushes the body down rather
    /// than sitting on its first row. It opens and shuts over a Transition (at once under Reduced
    /// Motion), so the body slides rather than jumps.
    /// </summary>
    public static int NoticeLane { get; internal set; }

    /// <summary>Where the page starts with no notice showing — the lane's own anchor.</summary>
    public static int PageTopBase { get; internal set; } = 150;

    private static readonly Color VoidInk = UiInk.Void;
    private static readonly Color PanelBg = UiInk.Raised;
    private static readonly Color PanelEdge = UiInk.Rule;
    private static readonly Color Dim = UiInk.Rule;

    // ── TEXT COLOURS live in UiInk (UX V2 P0.3). The block that stood here described PARCHMENT panels
    //    the game has not drawn since the 2026-08 art pass; every panel is near-black now, and the inks
    //    below are aliases kept for their remaining callers. ───────────────────────────────────────

    /// <summary>The near-black of the letterbox and the deepest wells. Not a text colour on today's panels.</summary>
    public static readonly Color Ink = new(0x16, 0x11, 0x10);

    /// <summary>Text on the scene — the same Primary ink every panel uses; the second name is kept for its callers.</summary>
    public static readonly Color Vellum = UiInk.Primary;

    /// <summary>
    /// The five ITEM CLASS colours, one per class, shared by every screen that names a class.
    /// </summary>
    /// <remarks>
    /// The four road classes borrow the MASTERY TREE's own branch colours (the roster's card edges
    /// already use them), so a WARDEN's gear is the same red as the WEIGHT road it was built for. The
    /// WANDERER, which has no road, gets an olive that no rarity uses: it must never read as Uncommon
    /// green (6EC87A), Rare blue, Epic violet or Legendary gold, because those own the frame tint and
    /// the left bar of every item cell.
    /// </remarks>
    public static Color ClassColor(IdleXIdle.Core.Economy.ItemClass cls) => cls switch
    {
        IdleXIdle.Core.Economy.ItemClass.Warden => new Color(0xD6, 0x48, 0x5C),
        IdleXIdle.Core.Economy.ItemClass.Ranger => new Color(0x48, 0xB8, 0x88),
        IdleXIdle.Core.Economy.ItemClass.Mystic => new Color(0x74, 0xC6, 0xE8),
        IdleXIdle.Core.Economy.ItemClass.Bulwark => new Color(0xC0, 0x6E, 0xE0),
        _ => new Color(0xB4, 0xB8, 0x62),
    };

    private readonly Texture2D _pixel;
    private readonly Texture2D _hex;
    private readonly Texture2D _diamond;
    private readonly Texture2D _blob;
    private readonly System.Collections.Generic.Dictionary<string, float> _topPadCache = new();
    private readonly System.Collections.Generic.Dictionary<string, float> _bottomPadCache = new();
    private readonly System.Collections.Generic.Dictionary<string, float> _sidePadCache = new();
    private readonly System.Collections.Generic.Dictionary<Texture2D, FrameSpec> _frameSpecs = new();

    // ── FRAME SPECS: where a frame's ornaments are, measured off the art, so the slicer never stretches one ──

    /// <summary>
    /// Where a frame texture keeps its ornaments: the corner flourish's extent from each edge, and the
    /// centre ornament's span along the top/bottom edge and along the left/right edge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE OLD NINE-SLICE CUT A FIXED 40 PX CORNER AND STRETCHED EVERYTHING ELSE.</b> Measured off the
    /// art (<c>tools/measure_frames.py</c>), the corner scrollwork on the panel frames runs 55–90 px, and
    /// the medium, square and small frames carry a gem or a crest in the middle of every edge. Both landed
    /// in the stretched edge band: the tail of every corner curl was smeared along the rail, and every
    /// mid-edge gem was pulled into a streak as long as the panel — the EQUIPPED panel's side gems at 100 %
    /// were 4.5× their own height. That is the "çerçeve kötü görünüyor" of the release polish pass, and it
    /// is not the art's fault: the art was never stretched, the slice was.
    /// </para>
    /// <para>
    /// A spec is measured ONCE per texture, from its pixels, on first use — so a regenerated frame with a
    /// different flourish measures itself and needs no table edited. <see cref="Measure"/> is the
    /// algorithm; <c>RH_UI_FRAMES=1</c> prints every spec as it is measured, which is how a new frame is
    /// checked against its picture.
    /// </para>
    /// </remarks>
    /// <param name="CornerX">How far the corner flourish reaches in from the left/right edge, in texture px.</param>
    /// <param name="CornerY">How far it reaches in from the top/bottom edge. Equal to the texture's height for a STRIP (a bar, a button) — one band, no vertical slicing.</param>
    /// <param name="TopOrnX0">The centre ornament on the top/bottom edge: its first column, or equal to <paramref name="TopOrnX1"/> when there is none.</param>
    /// <param name="TopOrnX1">One past its last column.</param>
    /// <param name="SideOrnY0">The centre ornament on the left/right edge: its first row, or equal to <paramref name="SideOrnY1"/> when there is none.</param>
    /// <param name="SideOrnY1">One past its last row.</param>
    public readonly record struct FrameSpec(int CornerX, int CornerY, int TopOrnX0, int TopOrnX1, int SideOrnY0, int SideOrnY1)
    {
        /// <summary>There is an ornament in the middle of the top and bottom edges.</summary>
        public bool HasTopOrnament => TopOrnX1 > TopOrnX0;

        /// <summary>There is an ornament in the middle of the left and right edges.</summary>
        public bool HasSideOrnament => SideOrnY1 > SideOrnY0;

        /// <summary>The plain fallback for art the measurement cannot read: a 40 px corner and no ornaments.</summary>
        public static FrameSpec Plain(int w, int h)
            => new(Math.Min(PanelCorner, w / 2 - 1), Math.Min(PanelCorner, h / 2 - 1), w / 2, w / 2, h / 2, h / 2);
    }

    /// <summary>Set <c>RH_UI_FRAMES=1</c> to print every frame spec as it is measured.</summary>
    private static readonly bool DumpFrameSpecs =
        Environment.GetEnvironmentVariable("RH_UI_FRAMES") is { Length: > 0 } fv && fv != "0";

    /// <summary>This texture's <see cref="FrameSpec"/>, measured on first use and cached.</summary>
    public FrameSpec FrameSpecOf(Texture2D tex)
    {
        if (_frameSpecs.TryGetValue(tex, out var had)) return had;
        var spec = Measure(tex);
        _frameSpecs[tex] = spec;
        if (DumpFrameSpecs)
            Console.Out.WriteLine($"frame\tkey={Assets.KeyOf(tex) ?? "(unkeyed)"}\t{tex.Width}x{tex.Height}\tcorner={spec.CornerX}x{spec.CornerY}"
                                  + $"\ttop=[{spec.TopOrnX0},{spec.TopOrnX1})\tside=[{spec.SideOrnY0},{spec.SideOrnY1})");
        return spec;
    }

    /// <summary>
    /// Measure a frame's ornaments from its pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Along each edge, every column (or row) of the edge band is a profile of (alpha, luma) pixels. The
    /// PLAIN RAIL is the profile most columns share — found as the medoid of a sample, so a frame whose
    /// ornaments cover half its width still elects a plain column, because the plain ones agree with each
    /// other and the ornamental ones agree with nothing. A column is plain when few of its pixels differ
    /// from the rail by more than a texture's own grain (the rails are painted stone, so this is a count
    /// of pixels past a luma threshold, not a mean difference — a mean would let a small gem hide in a
    /// tall band, and a strict match would call every stone column an ornament).
    /// </para>
    /// <para>
    /// The CORNER is where the first run of plain columns begins, from either side (the larger of the two,
    /// so an asymmetric flourish is never cut). The CENTRE ORNAMENT is the widest non-plain span between
    /// the corners, ignored when it is under three pixels (grain). A texture the scan cannot read — no
    /// plain run at all — gets <see cref="FrameSpec.Plain"/>, which is the old 40 px behaviour, so the
    /// worst case is the frame we shipped for a year, never a torn one.
    /// </para>
    /// </remarks>
    private static FrameSpec Measure(Texture2D tex)
    {
        int w = tex.Width, h = tex.Height;
        if (w < 8 || h < 8) return FrameSpec.Plain(w, h);
        var data = new Color[w * h];
        tex.GetData(data);

        // The band a scan reads: a quarter of the other dimension, never under 12 px. A texture at least
        // twice as wide as it is tall (a bar, a button, a tab), or too short to hold two bands and a
        // middle, is a STRIP — one band the whole height, no vertical slicing, and it scales with the
        // destination's height so its end scrollwork keeps its proportion at every control height.
        var bandY = Math.Clamp(h / 4, 12, h);
        var strip = w >= 2 * h || h <= bandY * 2 + 8;
        var (cx, ox0, ox1) = MeasureAxis(data, w, h, bandY, horizontal: true);
        if (cx < 0) return FrameSpec.Plain(w, h);
        if (strip) return new FrameSpec(cx, h, ox0, ox1, h / 2, h / 2);
        var bandX = Math.Clamp(w / 4, 12, w);
        var (cy, oy0, oy1) = MeasureAxis(data, w, h, bandX, horizontal: false);
        if (cy < 0) return FrameSpec.Plain(w, h);
        return new FrameSpec(cx, cy, ox0, ox1, oy0, oy1);
    }

    /// <summary>One axis of <see cref="Measure"/>: (corner extent, ornament start, ornament end), or corner -1 when unreadable.</summary>
    private static (int Corner, int Orn0, int Orn1) MeasureAxis(Color[] data, int w, int h, int band, bool horizontal)
    {
        var n = horizontal ? w : h;
        // Profiles, smoothed over the neighbouring column on each side so painted grain does not vote.
        var alpha = new int[n, band];
        var luma = new int[n, band];
        for (var i = 0; i < n; i++)
            for (var k = 0; k < band; k++)
            {
                int a = 0, l = 0, c = 0;
                for (var d = -1; d <= 1; d++)
                {
                    var j = i + d;
                    if (j < 0 || j >= n) continue;
                    var p = horizontal ? data[k * w + j] : data[j * w + k];
                    a += p.A; l += (p.R * 3 + p.G * 6 + p.B) / 10; c++;
                }
                alpha[i, k] = a / c; luma[i, k] = l / c;
            }

        int Differ(int i, int j)
        {
            var count = 0;
            for (var k = 0; k < band; k++)
                if (Math.Abs(luma[i, k] - luma[j, k]) > 48 || Math.Abs(alpha[i, k] - alpha[j, k]) > 64) count++;
            return count;
        }

        // The medoid of a stride sample elects the rail.
        var stride = Math.Max(1, n / 64);
        int refIdx = 0, refScore = int.MaxValue;
        for (var i = 0; i < n; i += stride)
        {
            var score = 0;
            for (var j = 0; j < n; j += stride) score += Differ(i, j);
            if (score < refScore) { refScore = score; refIdx = i; }
        }
        var limit = Math.Max(2, (int)MathF.Round(band * 0.10f));
        var plain = new bool[n];
        for (var i = 0; i < n; i++) plain[i] = Differ(i, refIdx) <= limit;

        int CornerFrom(bool left)
        {
            var run = 0;
            for (var step = 0; step < n; step++)
            {
                var i = left ? step : n - 1 - step;
                run = plain[i] ? run + 1 : 0;
                if (run >= 4) return step - 3;
            }
            return n;
        }
        var corner = Math.Max(CornerFrom(true), CornerFrom(false));
        if (corner >= n / 2 - 2) return (-1, n / 2, n / 2);

        int best = 0, o0 = n / 2, o1 = n / 2;
        for (var i = corner; i < n - corner;)
        {
            if (plain[i]) { i++; continue; }
            var j = i;
            while (j < n - corner && !plain[j]) j++;
            if (j - i > best) { best = j - i; o0 = i; o1 = j; }
            i = j;
        }
        if (best < 3) { o0 = o1 = n / 2; }
        return (corner, o0, o1);
    }

    /// <summary>
    /// Draw a frame into <paramref name="r"/> so that NOTHING ORNAMENTAL IS STRETCHED: corners and centre
    /// ornaments at their own scale, only the plain rails and the interior stretched to fill.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A grid, not a nine-slice: up to five cuts on each axis — corner · rail · ornament · rail · corner —
    /// so a frame with a gem on every edge draws as 25 pieces and a plain one as 9. The two rails on an
    /// edge share the room left after the corners and the ornament, in proportion to their source widths,
    /// so the ornament stays where the artist put it. A STRIP (a bar, a button: <see cref="FrameSpec.CornerY"/>
    /// equal to its height) has one row and scales with the destination's HEIGHT, which is what keeps a
    /// button's end scrollwork in proportion at every control height.
    /// </para>
    /// <para>
    /// A rectangle too small for its corners and ornament at native scale shrinks them together, in
    /// proportion, rather than letting the pieces overlap — a settings row 54 px tall wears a proportionally
    /// finer frame, never a torn one. The ledger records the corner's magnification, as it always has.
    /// </para>
    /// </remarks>
    public void SliceFrame(SpriteBatch b, Texture2D tex, Rectangle r, Color tint)
    {
        var spec = FrameSpecOf(tex);
        int w = tex.Width, h = tex.Height;
        var strip = spec.CornerY >= h;
        int ornW = spec.HasTopOrnament ? spec.TopOrnX1 - spec.TopOrnX0 : 0;
        int ornH = !strip && spec.HasSideOrnament ? spec.SideOrnY1 - spec.SideOrnY0 : 0;

        // The scale the ornaments draw at: native (1:1 on the canvas at this counter-scale), a strip's
        // height ratio, and never more than the rectangle can hold.
        var s = strip ? r.Height / (float)h : 1f / Scale;
        var needW = 2 * spec.CornerX + ornW;
        var needH = strip ? h : 2 * spec.CornerY + ornH;
        if (needW > 0) s = MathF.Min(s, r.Width / (float)needW);
        if (needH > 0) s = MathF.Min(s, r.Height / (float)needH);
        if (s <= 0f) { b.Draw(tex, r, tint); return; }

        // Column cuts, source and destination.
        var sx = spec.HasTopOrnament
            ? new[] { 0, spec.CornerX, spec.TopOrnX0, spec.TopOrnX1, w - spec.CornerX, w }
            : new[] { 0, spec.CornerX, w - spec.CornerX, w };
        var dx = Cuts(sx, r.X, r.Width, s);
        int[] sy, dy;
        if (strip)
        {
            sy = new[] { 0, h };
            dy = new[] { r.Y, r.Bottom };
        }
        else
        {
            sy = spec.HasSideOrnament
                ? new[] { 0, spec.CornerY, spec.SideOrnY0, spec.SideOrnY1, h - spec.CornerY, h }
                : new[] { 0, spec.CornerY, h - spec.CornerY, h };
            dy = Cuts(sy, r.Y, r.Height, s);
        }

        UiRasterLedger.Note(Assets.KeyOf(tex) ?? "(unkeyed)", spec.CornerX, strip ? h : spec.CornerY,
                            dx[1] - dx[0], dy[1] - dy[0], "UiKit.SliceFrame corner");

        for (var j = 0; j + 1 < sy.Length; j++)
            for (var i = 0; i + 1 < sx.Length; i++)
            {
                int sw = sx[i + 1] - sx[i], sh = sy[j + 1] - sy[j];
                int dw = dx[i + 1] - dx[i], dh = dy[j + 1] - dy[j];
                if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0) continue;
                b.Draw(tex, new Rectangle(dx[i], dy[j], dw, dh), new Rectangle(sx[i], sy[j], sw, sh), tint);
            }
    }

    /// <summary>
    /// Destination cuts for one axis: fixed pieces (corners, the ornament) at <paramref name="s"/> times
    /// their source size, the plain rails sharing what is left in proportion to their source widths.
    /// </summary>
    /// <remarks>
    /// The cuts are the ODD-indexed pieces' edges: piece 0 is a corner, piece 1 a rail, piece 2 the
    /// ornament (or the far corner when there is none), and so on. Integer arithmetic on purpose — a cut
    /// that lands on a half pixel is a seam that shows under LinearClamp.
    /// </remarks>
    private static int[] Cuts(int[] src, int origin, int length, float s)
    {
        var pieces = src.Length - 1;
        var fixedTotal = 0;
        var railSrc = 0;
        var fixedDst = new int[pieces];
        for (var i = 0; i < pieces; i++)
        {
            var sw = src[i + 1] - src[i];
            // Piece 1 and piece pieces-2 are the rails on a 4- or 6-cut axis; on a 2-cut axis (a strip's
            // single row) there is only the one piece, which fills.
            var rail = pieces >= 3 && (i == 1 || i == pieces - 2);
            if (pieces == 1) { fixedDst[i] = length; continue; }
            if (rail) { railSrc += sw; continue; }
            fixedDst[i] = Math.Max(0, (int)MathF.Round(sw * s));
            fixedTotal += fixedDst[i];
        }
        var railDst = Math.Max(0, length - fixedTotal);
        var cuts = new int[src.Length];
        cuts[0] = origin;
        var railSpent = 0;
        var railSeen = 0;
        for (var i = 0; i < pieces; i++)
        {
            var sw = src[i + 1] - src[i];
            var rail = pieces >= 3 && (i == 1 || i == pieces - 2);
            int dw;
            if (pieces == 1) dw = length;
            else if (rail)
            {
                railSeen += sw;
                // The last rail takes the remainder, so rounding never opens a one-pixel gap.
                var upTo = railSrc > 0 ? (int)MathF.Round(railDst * (railSeen / (float)railSrc)) : railDst;
                dw = upTo - railSpent;
                railSpent = upTo;
            }
            else dw = fixedDst[i];
            cuts[i + 1] = cuts[i] + dw;
        }
        cuts[^1] = origin + length;   // the far edge is the rectangle's edge, whatever rounding did
        return cuts;
    }

    public UiKit(GraphicsDevice device, PixelFont font, AssetLibrary assets)
    {
        Font = font;
        Text2 = new SmoothFont(font);   // smooth TTF, or transparently the pixel font if none loads
        Assets = assets;
        Device = device;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _hex = MakeHex(device, 120, 104);       // flat-top hexagon, drawn tinted + scaled (LinearClamp keeps it smooth)
        _diamond = MakeDiamond(device, 64);      // a gem for the nav / accents
        _blob = MakeBlob(device, 128);           // soft contact shadow under the fighters
    }

    /// <summary>Draw the flat-top hexagon filling <paramref name="dest"/>, tinted.</summary>
    public void Hex(SpriteBatch b, Rectangle dest, Color fill) => b.Draw(_hex, dest, fill);

    /// <summary>Draw a diamond gem filling <paramref name="dest"/>, tinted.</summary>
    public void Diamond(SpriteBatch b, Rectangle dest, Color fill) => b.Draw(_diamond, dest, fill);

    /// <summary>
    /// Draw the current frame of a horizontal animation strip (package_02A/03A/04A/06 flipbooks), fit
    /// bottom-centered into <paramref name="box"/>. Frames are square (frame width = strip height), count
    /// auto-detected. <paramref name="seconds"/> is a running clock; loop wraps, else it holds the last
    /// frame. Returns false if the strip asset is missing, so the caller can fall back to a static pose.
    /// </summary>
    /// <summary>Draw a single static sprite (a full-body pose), trimming top padding and filling the box
    /// height, anchored bottom-centre — same framing as <see cref="AnimSprite"/> but for one texture.</summary>
    public bool Sprite(SpriteBatch b, string key, Rectangle box, Color tint, float topCrop = 0f)
    {
        if (Assets.Get(key) is not { } tex || tex.Height <= 0) return false;
        var cropY = (int)(tex.Height * Math.Clamp(topCrop, 0f, 0.6f));
        var srcH = tex.Height - cropY;
        var sc = box.Height / (float)srcH;
        var w = Math.Max(1, (int)(tex.Width * sc));
        UiRasterLedger.Note(key, tex.Width, srcH, w, box.Height, "UiKit.Sprite");
        b.Draw(tex, new Rectangle(box.Center.X - w / 2, box.Bottom - box.Height, w, box.Height),
            new Rectangle(0, cropY, tex.Width, srcH), tint);
        return true;
    }

    /// <summary>
    /// Fraction of a texture's height that is fully transparent across the TOP (max alpha ≤ 8 per row),
    /// cached per key. Rev 4 §12: lets a caller anchor an overhead bar / shadow to a sprite's VISIBLE opaque
    /// bounds instead of its padded canvas. A runtime alpha scan stands in for the offline bounds catalog the
    /// spec asks for; it does not distinguish smoke/trails, so bosses still use their authored crop.
    /// </summary>
    public float TopPadFraction(string key)
    {
        if (_topPadCache.TryGetValue(key, out var cached)) return cached;
        var frac = 0f;
        if (Assets.Get(key) is { } t && t is { Width: > 0, Height: > 0 })
        {
            var data = new Color[t.Width * t.Height];
            t.GetData(data);
            var top = t.Height;
            for (var y = 0; y < t.Height; y++)
            {
                var opaque = false;
                for (var x = 0; x < t.Width; x++)
                    if (data[y * t.Width + x].A > 8) { opaque = true; break; }
                if (opaque) { top = y; break; }
            }
            frac = top >= t.Height ? 0f : top / (float)t.Height;
        }
        _topPadCache[key] = frac;
        return frac;
    }

    /// <summary>
    /// Fraction of a texture's height that is fully transparent across the BOTTOM, cached per key.
    /// </summary>
    /// <remarks>
    /// The mirror of <see cref="TopPadFraction"/>, and the fix for characters that look like they are
    /// fighting in mid-air. <see cref="Sprite"/> scales the WHOLE texture — padding included — to fill
    /// the destination box, so a sprite with empty rows under its feet lands its visible sole that many
    /// pixels ABOVE the box bottom. Measured on the shipped art that gap is 43 px for the hunter and
    /// 15 px for an enemy at the arena's box size, which is plainly visible against a ground line.
    /// Callers that must stand something on the floor use <see cref="SpriteGrounded"/>, which pushes the
    /// draw down by this fraction so the opaque sole meets <c>box.Bottom</c>.
    /// </remarks>
    /// <summary>
    /// Empty columns on BOTH sides of an animation strip, as a fraction of FRAME width — the least any
    /// frame has, so trimming by it can never cut the one frame that reaches furthest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The twin of <see cref="TopPadFraction"/>, and it fixes a CLIPPED figure rather than a small one.
    /// <see cref="AnimSprite"/> scales a SQUARE frame by the destination's HEIGHT, so the width it draws
    /// is the frame's full width at that scale whatever the art actually fills. The generated champion
    /// strips are letterboxed on every side, so a 430-tall champion box drew 573 pixels wide; centred at
    /// x=700 that reaches x=413, and the arena's scissor starts at 492. The cloak came off against a
    /// straight vertical edge (playtest 2026-08-28: "spriteyi kesiyor").
    /// </para>
    /// <para>
    /// SYMMETRIC, and measured across the WHOLE strip — both deliberately. Cropping each side to its own
    /// content would re-centre a figure the artist placed off-centre and make it jump between frames;
    /// taking the least pad of any frame keeps every pose intact. What is removed is margin transparent
    /// in all eight frames, so the visible figure is pixel-identical and only its rectangle shrinks.
    /// </para>
    /// </remarks>
    /// <param name="declaredFrames">
    /// How many frames the strip HAS, when the caller knows. Zero (the default) infers square frames
    /// from the height — what every draw caller wants, and what all the shipped art is. The VFX
    /// placement contract DECLARES its frame count instead of inferring it, because a strip
    /// regenerated at another aspect would otherwise be mis-sliced in silence.
    /// </param>
    public float SidePadFraction(string key, int declaredFrames = 0)
    {
        var cacheKey = declaredFrames > 0 ? $"{key}#{declaredFrames}" : key;
        if (_sidePadCache.TryGetValue(cacheKey, out var cached)) return cached;
        var frac = 0f;
        if (Assets.Get(key) is { } t && t is { Width: > 0, Height: > 0 })
        {
            var frames = declaredFrames > 0 ? declaredFrames : Math.Max(1, t.Width / t.Height);
            var fw = Math.Max(1, t.Width / frames);
            var data = new Color[t.Width * t.Height];
            t.GetData(data);
            var least = fw / 2;                      // the tightest pad found on any frame, either side
            for (var f = 0; f < frames; f++)
            {
                int left = fw, right = fw;
                for (var x = 0; x < fw && left == fw; x++)
                    for (var y = 0; y < t.Height; y++)
                        if (data[y * t.Width + f * fw + x].A > 8) { left = x; break; }
                for (var x = fw - 1; x >= 0 && right == fw; x--)
                    for (var y = 0; y < t.Height; y++)
                        if (data[y * t.Width + f * fw + x].A > 8) { right = fw - 1 - x; break; }
                if (left == fw) continue;            // an empty frame says nothing about the margin
                least = Math.Min(least, Math.Min(left, right));
            }
            frac = Math.Max(0, least - 1) / (float)fw;   // a pixel of slack, so no soft edge is shaved
        }
        _sidePadCache[cacheKey] = frac;
        return frac;
    }

    /// <summary>
    /// A strip's transparent margins as fractions of one FRAME — the union across every frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one queryable visual-bounds API in the game, and the thing the VFX placement contract is
    /// built on. The measurement is not new: it is the same three cached alpha scans the draw path has
    /// always used (<see cref="SidePadFraction"/>, <see cref="TopPadFraction"/>,
    /// <see cref="BottomPadFraction"/>), which nothing outside <see cref="AnimSprite"/> and
    /// <see cref="SpriteGrounded"/> could reach. The function shaped like this one — <c>ContentPad</c>,
    /// which returned a full bounding box — had ZERO consumers and took its left and right edges from
    /// the first and last frame in TEXTURE coordinates rather than the union in FRAME coordinates, so
    /// it was wrong for a strip. It is gone; this replaces it.
    /// </para>
    /// <para>
    /// Left and right are equal by construction — that is <see cref="SidePadFraction"/>'s own rule, and
    /// it is deliberate: cropping each side to its own content would re-centre a figure the artist
    /// placed off-centre.
    /// </para>
    /// <para>
    /// Missing art measures as <see cref="Vfx.ContentBox.Full"/> rather than throwing, so an effect with
    /// no strip is placed honestly at its frame size instead of dividing by a zero-height content box.
    /// </para>
    /// </remarks>
    public Vfx.ContentBox Content(string key, int declaredFrames = 0)
    {
        if (Assets.Get(key) is null) return Vfx.ContentBox.Full;
        var side = SidePadFraction(key, declaredFrames);
        return new Vfx.ContentBox(side, TopPadFraction(key), side, BottomPadFraction(key));
    }

    /// <summary>
    /// Empty rows under the lowest opaque pixel, as a fraction of height. Cached per key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mirror of <see cref="TopPadFraction"/>, and the fix for characters that look like they are
    /// fighting in mid-air. <see cref="Sprite"/> scales the WHOLE texture — padding included — to fill
    /// the destination box, so a sprite with empty rows under its feet lands its visible sole that many
    /// pixels ABOVE the box bottom. Callers that must stand something on the floor use
    /// <see cref="SpriteGrounded"/> or <see cref="AnimSprite"/>, which push the draw down by this
    /// fraction so the opaque sole meets <c>box.Bottom</c>.
    /// </para>
    /// <para>
    /// ONE function, for a texture or a strip alike. There used to be two — this and a byte-identical
    /// <c>StripBottomPadFraction</c> — sharing a single cache, so they could never have disagreed and
    /// one of them was pure duplication waiting to drift.
    /// </para>
    /// </remarks>
    public float BottomPadFraction(string key)
    {
        if (_bottomPadCache.TryGetValue(key, out var cached)) return cached;
        var frac = 0f;
        if (Assets.Get(key) is { } t && t is { Width: > 0, Height: > 0 })
        {
            var data = new Color[t.Width * t.Height];
            t.GetData(data);
            var bottom = -1;
            for (var y = t.Height - 1; y >= 0; y--)
            {
                var opaque = false;
                for (var x = 0; x < t.Width; x++)
                    if (data[y * t.Width + x].A > 8) { opaque = true; break; }
                if (opaque) { bottom = y; break; }
            }
            frac = bottom < 0 ? 0f : (t.Height - 1 - bottom) / (float)t.Height;
        }
        _bottomPadCache[key] = frac;
        return frac;
    }

    /// <summary>
    /// Draw a sprite so its VISIBLE bottom edge rests on <paramref name="box"/>.Bottom, rather than its
    /// padded canvas bottom. Use for anything that stands on the ground.
    /// </summary>
    /// <returns>false if the texture is missing, so callers can fall back exactly as with <see cref="Sprite"/>.</returns>
    public bool SpriteGrounded(SpriteBatch b, string key, Rectangle box, Color tint, float topCrop = 0f, bool flip = false)
    {
        if (Assets.Get(key) is not { } tex || tex.Height <= 0) return false;
        var cropY = (int)(tex.Height * Math.Clamp(topCrop, 0f, 0.6f));
        var srcH = tex.Height - cropY;
        var sc = box.Height / (float)srcH;
        var w = Math.Max(1, (int)(tex.Width * sc));

        // The pad fraction is of the FULL texture; the visible gap after scaling is that
        // fraction of the drawn height. Shifting down by it plants the sole on box.Bottom.
        var drop = (int)MathF.Round(BottomPadFraction(key) * tex.Height * sc);
        UiRasterLedger.Note(key, tex.Width, srcH, w, box.Height, "UiKit.SpriteGrounded");
        b.Draw(tex, new Rectangle(box.Center.X - w / 2, box.Y + drop, w, box.Height),
            new Rectangle(0, cropY, tex.Width, srcH), tint,
            0f, Vector2.Zero, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        return true;
    }

    /// <param name="flip">
    /// Mirror horizontally. THE RENDERER HAD NO CONCEPT OF FACING AT ALL before this parameter.
    /// </param>
    /// <remarks>
    /// <b>Every sprite entry point here ended in the four-argument <c>SpriteBatch.Draw</c> overload,
    /// which cannot express orientation</b> — <c>SpriteEffects</c> appeared in zero source files in the
    /// whole repository. Meanwhile the arena is an explicit left-versus-right composition and encodes
    /// that direction in MOTION: the champion lunges +40 right, the enemies lunge -40 left and slide in
    /// from +280 right. So every figure was drawn in whatever orientation its generator happened to
    /// produce, and the starter champion's attack clip swings LEFT — away from the enemies it is
    /// hitting. Playtest: "Karakter animasyonları ters tarafa oynuyor gibi, yön hatası var sanırım."
    /// </remarks>
    public bool AnimSprite(SpriteBatch b, string stripKey, Rectangle box, float seconds, float fps, bool loop, Color tint, float topCrop = 0f, bool flip = false)
    {
        if (Assets.Get(stripKey) is not { } tex || tex.Height <= 0) return false;
        var fw = tex.Height;
        var frames = Math.Max(1, tex.Width / fw);
        // Rev 4 §8/§9: a horizontal strip must be exactly N square frames, and we draw exactly ONE of them
        // (the src rect below is a single frame). A width that isn't a whole multiple means the sheet is
        // mis-declared (or a preview/contact sheet slipped in) and frame extraction would smear.
        if (tex.Width % fw != 0)   // dev warning only — a Debug.Assert here would abort the game's Debug build
            System.Diagnostics.Debug.WriteLine($"Animation strip '{stripKey}' width {tex.Width} is not a whole multiple of frame size {fw}.");
        // REDUCED MOTION holds the strip on its first frame. This is the one call every looping
        // sprite in the game goes through — the hunt's hunter and creatures, the roster's ten cards —
        // so the accessibility switch reaches all of them here rather than in each screen.
        var i = Game1.ReducedMotion ? 0 : (int)(seconds * fps);
        if (!Game1.ReducedMotion)
            i = loop ? (i % frames + frames) % frames : Math.Clamp(i, 0, frames - 1);
        // A NEGATIVE topCrop means "measure it". Character strips are generated from a source that was
        // deliberately letterboxed to leave the animator's crop somewhere to land, so a third of the
        // frame is empty sky — and drawn as-is, that empty sky is a third of the box and the champion
        // renders two thirds the size the layout asked for. The pad differs per clip (an attack is
        // letterboxed harder than an idle), so it has to be measured rather than passed in.
        //
        // TopPadFraction scans the WHOLE strip and reports the LEAST headroom any frame has, so
        // cropping by it can never cut into the figure on the one frame that raises its arms.
        if (topCrop < 0f) topCrop = TopPadFraction(stripKey);
        // Trim the transparent headroom (and any streak artifacts) off the top of the frame, then FILL the
        // box height with the remaining figure, anchored bottom-centre. Side padding overflows harmlessly.
        // Fitting the whole square frame instead left the figure tiny and "boxed" inside the panel.
        var cropY = (int)(fw * Math.Clamp(topCrop, 0f, 0.6f));
        var srcH = tex.Height - cropY;
        // ...and the same for the SIDES, symmetrically. The frame is SQUARE and the scale comes from the
        // HEIGHT, so a 430-tall box drew a 573-wide sprite — 170px of it empty margin — and on the
        // champion (centred at x=700, in an arena whose scissor starts at 492) that margin plus the cloak
        // fell outside the clip and the figure was sliced by a straight vertical edge. Trimming the
        // margin that is empty in EVERY frame changes no pixel of the figure — same height, same centre,
        // same scale — it only stops the draw claiming space the art never used.
        var cropX = (int)(fw * Math.Clamp(SidePadFraction(stripKey), 0f, 0.4f));
        var srcW = fw - 2 * cropX;
        var src = new Rectangle(i * fw + cropX, cropY, srcW, srcH);
        // THE BOX ASKS; THE ART ANSWERS (BRIEF sec.74). A box that fills whatever room is left over will
        // happily ask a 398-px figure to be 528 px tall, and the answer used to be yes — the GEAR
        // paper doll drew the hunter at 1.33x native while the same strip in the fight drew at 1.08.
        // The rule is that decorative raster may not be arbitrarily enlarged, so the scale is capped at
        // RasterCeiling and the figure is simply drawn at the largest honest size, still centred and
        // still grounded. Shrinking a box to fit its art is a layout number; capping the magnification
        // is the rule, and it holds for every caller rather than for the one that was noticed.
        var sc = MathF.Min(box.Height / (float)srcH, RasterCeiling);
        // TRUNCATE, never round: at the ceiling exactly, rounding up puts the drawn height one
        // pixel PAST the budget and the ledger correctly reports the cap itself as a violation.
        var drawnH = Math.Max(1, (int)(srcH * sc));
        var w = Math.Max(1, (int)(srcW * sc));
        // Same grounding as SpriteGrounded. The pad is measured once for the whole strip rather than
        // per frame on purpose: a per-frame sole would make the figure slide up and down as the
        // animation played. One offset for the clip keeps the feet planted while it animates.
        var drop = (int)MathF.Round(BottomPadFraction(stripKey) * fw * sc);
        UiRasterLedger.Note(stripKey, srcW, srcH, w, drawnH, "UiKit.AnimSprite");
        // Bottom-anchored: a capped figure keeps its feet where an uncapped one had them, so the cap
        // never lifts a hunter off the floor its slots and shadow were laid out against.
        b.Draw(tex, new Rectangle(box.Center.X - w / 2, box.Bottom - drawnH + drop, w, drawnH), src, tint,
               0f, Vector2.Zero, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        return true;
    }

    private static Texture2D MakeHex(GraphicsDevice d, int w, int h)
    {
        var tex = new Texture2D(d, w, h);
        var data = new Color[w * h];
        for (var y = 0; y < h; y++)
        {
            var ny = y / (float)(h - 1);                       // 0..1
            var inset = MathF.Abs(ny - 0.5f) * 2f * (w * 0.25f); // 0 at mid-height, w/4 at the flat top/bottom
            for (var x = 0; x < w; x++)
                data[y * w + x] = x >= inset && x <= w - inset ? Color.White : Color.Transparent;
        }
        tex.SetData(data);
        return tex;
    }

    /// <summary>
    /// A soft radial blob, used as the fighters' contact shadow.
    /// </summary>
    /// <remarks>
    /// Deliberately procedural rather than a generated sprite: a shadow is pure alpha falloff, and the
    /// art pipeline's border flood-fill knockout keys on a background colour, so it would clip the very
    /// gradient that makes a shadow read. A hard-edged rectangle under each fighter — which is what shipped
    /// — announces itself as a rectangle; this fades out and just reads as ground contact.
    /// </remarks>
    private static Texture2D MakeBlob(GraphicsDevice d, int s)
    {
        var tex = new Texture2D(d, s, s);
        var data = new Color[s * s];
        var c = (s - 1) / 2f;
        for (var y = 0; y < s; y++)
            for (var x = 0; x < s; x++)
            {
                var r = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                // Squared falloff: a linear ramp still shows a visible disc edge at low alpha.
                var a = r >= 1f ? 0f : (1f - r) * (1f - r);
                data[y * s + x] = new Color(0f, 0f, 0f, a);
            }
        tex.SetData(data);
        return tex;
    }

    /// <summary>A soft elliptical contact shadow centred on (cx, cy).</summary>
    public void GroundShadow(SpriteBatch b, int cx, int cy, int width, int height, float strength = 0.55f)
        => b.Draw(_blob, new Rectangle(cx - width / 2, cy - height / 2, width, height), Color.White * strength);

    private static Texture2D MakeDiamond(GraphicsDevice d, int s)
    {
        var tex = new Texture2D(d, s, s);
        var data = new Color[s * s];
        var c = (s - 1) / 2f;
        for (var y = 0; y < s; y++)
            for (var x = 0; x < s; x++)
                data[y * s + x] = MathF.Abs(x - c) + MathF.Abs(y - c) <= c ? Color.White : Color.Transparent;
        tex.SetData(data);
        return tex;
    }

    public PixelFont Font { get; }
    /// <summary>The active text renderer — smooth when a TTF is present, else the pixel font.</summary>
    public SmoothFont Text2 { get; }
    public AssetLibrary Assets { get; }
    /// <summary>The graphics device — exposed so screens can set a scissor rectangle for arena clipping.</summary>
    public GraphicsDevice Device { get; }

    /// <summary>Logical width of a string in the active font. Use this instead of PixelFont.Measure.</summary>
    public int Measure(string s) => Text2.Measure(s, UiTypography.Label);

    // ── Primitives ──────────────────────────────────────────────────────────────────────────────
    public void Fill(SpriteBatch b, Rectangle r, Color c) => b.Draw(_pixel, r, c);

    public void Sprite(SpriteBatch b, Texture2D t, Rectangle dest, Color? tint = null)
        => b.Draw(t, dest, tint ?? Color.White);

    /// <summary>Draw a sprite centered in bounds at the largest INTEGER scale that fits (stays crisp).</summary>
    public void SpriteFit(SpriteBatch b, Texture2D t, Rectangle bounds, Color? tint = null)
    {
        // FLOAT scale, and it may be < 1.
        //
        // This was `Math.Max(1, Math.Min(bounds.Width / t.Width, ...))` — INTEGER division clamped to a
        // minimum of 1. That was correct for pixel art, where every icon was authored at its display
        // size and a fractional scale would have shimmered. It is catastrophic for hand-drawn art
        // authored at 4x: a 64px icon fitted into an 11px box computes 64/11 = 0 in integer maths,
        // clamps to 1, and draws at 64px — SIX TIMES too big, straight over everything around it.
        //
        // The whole roster arrived at once and every role icon in the Warren rendered as a giant cream
        // blob on top of the creatures. It read as "the icons are broken"; it was one Math.Max, left
        // over from a rendering model this game stopped using.
        var scale = MathF.Min(bounds.Width / (float)t.Width, bounds.Height / (float)t.Height);
        var w = Math.Max(1, (int)MathF.Round(t.Width * scale));
        var h = Math.Max(1, (int)MathF.Round(t.Height * scale));
        UiRasterLedger.Note(Assets.KeyOf(t) ?? "(unkeyed)", t.Width, t.Height, w, h, "UiKit.SpriteFit");
        b.Draw(t, new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h),
            tint ?? Color.White);
    }

    // ── Backgrounds ─────────────────────────────────────────────────────────────────────────────
    /// <summary>Fill the whole canvas with a scene background, or the void colour if it's missing.</summary>
    public void Background(SpriteBatch b, string key, Color? tint = null)
    {
        // The shared chrome (batches A/C + title) draws at scale 1 in TRUE 1920×1080 coords, so the full-canvas
        // background must span 1920×1080, not the 480×270 logical grid. All Background callers are chrome now.
        if (Assets.Get(key) is { } bg)
        {
            UiRasterLedger.Note(key, bg.Width, bg.Height, 1920, 1080, "UiKit.Background");
            b.Draw(bg, new Rectangle(0, 0, 1920, 1080), tint ?? Color.White);
        }
        else Fill(b, new Rectangle(0, 0, 1920, 1080), VoidInk);
    }

    /// <summary>A translucent scrim over the whole screen — used to dim a reused background.</summary>
    /// <remarks>Sized to the full 1920×1080 physical canvas: at scale-1 (settings modal) it fills exactly, and
    /// at scale-4 (a batch-B screen dimming its scene) the oversize is a harmless uniform overdraw, clipped.</remarks>
    public void Scrim(SpriteBatch b, float alpha)
        => Fill(b, new Rectangle(0, 0, 1920, 1080), VoidInk * Math.Clamp(alpha, 0f, 1f));

    // ── Panels (9-slice ui_panel — a 256×256 dark-glass plate with a ~34px cut-corner bevel) ────────
    // Corner MUST match the asset's bevel: at 16 the slice cut the diagonal in half, so every panel's
    // cut corners rendered as a broken half-bevel meeting a straight edge — the "frame doesn't fit" look.
    private const int Corner = 34;

    /// <summary>
    /// An ornate `ui_panel_*` frame (package_01) as a 9-slice at NATIVE resolution. The asset closest in
    /// ASPECT is chosen so the ornate border stretches least; the corner slice is large enough to keep the
    /// corner gems intact, and the canvas being 4×-scaled means a source corner of S px is drawn at S/4
    /// logical (1:1 on screen — crisp). Edge-midpoint gems do stretch with the edge bands; that is the
    /// known cost of scaling a decorated frame to arbitrary panel sizes.
    /// </summary>
    /// <param name="artKey">
    /// Pin the frame art (<c>ui_panel_medium</c> / <c>ui_panel_square</c> / <c>ui_panel_vertical</c>) instead of
    /// letting <see cref="PanelArtKey"/> choose by aspect. For a panel whose HEIGHT follows the profile
    /// while its width is a page anchor — the HUNT's right column — the aspect crosses 1.30 at 125 %, and
    /// the panel alone changed frames beside three siblings that did not (release polish 2026-09-05).
    /// </param>
    public void Panel(SpriteBatch b, Rectangle r, bool gold = false, string? artKey = null)
        => PanelAt(b, r, gold ? new Color(0xFF, 0xDC, 0xA0) : Color.White, gold, artKey);

    /// <summary>
    /// Tier 2: a SUPPORTING surface. The same frame, drawn quiet, so the ornate one is the subject.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EVERY PANEL IN THE GAME WORE THE SAME GOLD FILIGREE</b>, from a 630px paper-doll down to a
    /// 60x28 training chip. A frame that heavy is a claim — "look here" — and when eleven regions on one
    /// screen all make it, none of them wins and the screen reads as an undifferentiated thicket. That is
    /// the "her şey çok karmaşıkmış gibi" from the playtest, and it is not a density problem: the same
    /// information inside two visual weights reads as half as much.
    /// </para>
    /// <para>
    /// <b>This tints the art rather than replacing it with a flat rectangle</b>, which was the other
    /// option and the wrong one. The ornate frame IS the game's visual identity; ninety percent of the
    /// screens turning into plain boxes would read as unfinished, not as calm. Tinted, the filigree
    /// survives as a dark bronze texture at the edge — present if you look at it, silent if you are
    /// looking for something else.
    /// </para>
    /// <para>
    /// It also costs NOTHING in layout. A quiet panel occupies exactly the rectangle its ornate twin did,
    /// so a screen converts by changing the call and nothing else — no content moves, and no hand-placed
    /// literal can collide with a border that was already there.
    /// </para>
    /// <para>
    /// <b>THE FRAME RULE (playtest 2026-08-26):</b> the gold nine-slice (<see cref="PanelNine"/> with
    /// <c>ui_panel_modal_wide</c>) is for MODALS only — a panel that takes the screen and asks something.
    /// Every panel that lives IN a screen wears this quiet brown, both columns alike; a screen with one
    /// gold column and one brown column reads as two screens.
    /// </para>
    /// </remarks>
    /// <param name="alpha">A fade for transient panels (a toast on its way out); 1 draws it solid.</param>
    /// <param name="artKey">Pin the frame art — see <see cref="Panel"/>. Null lets the aspect choose.</param>
    public void PanelQuiet(SpriteBatch b, Rectangle r, float alpha = 1f, string? artKey = null) => PanelAt(b, r, QuietFrame * alpha, false, artKey);

    /// <summary>The tint that turns the gold frame to dark bronze. Multiplied, so the interior stays black.</summary>
    private static readonly Color QuietFrame = UiInk.Bronze;

    /// <summary>
    /// Tier 3, the QUIET surface: a dark translucent plate with a one-pixel rule — for lists, grids,
    /// metadata rows, resource strips, toasts. No filigree at all.
    /// </summary>
    /// <remarks>
    /// The 2026-09-01 UX audit found there was no quiet tier: <see cref="PanelQuiet"/> is the ORNATE frame
    /// tinted bronze, so a stat row and a paper-doll wore the same corner gems, and at least eight screens
    /// hand-drew this plate with their own fills. One method, so the hierarchy PRIMARY (ornate) /
    /// SECONDARY (quiet frame) / QUIET (plate) is three calls a reader can tell apart.
    /// </remarks>
    /// <param name="accent">A 5 px left rule in this colour — the guide strip's gold, a Source's tint. Null for none.</param>
    /// <param name="alpha">A fade for transient plates; 1 draws it solid.</param>
    public void Plate(SpriteBatch b, Rectangle r, Color? accent = null, float alpha = 1f)
    {
        Fill(b, r, UiInk.Plate * alpha);
        var rule = UiInk.Rule * alpha;
        Fill(b, new Rectangle(r.X, r.Y, r.Width, 1), rule);
        Fill(b, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), rule);
        Fill(b, new Rectangle(r.X, r.Y, 1, r.Height), rule);
        Fill(b, new Rectangle(r.Right - 1, r.Y, 1, r.Height), rule);
        if (accent is { } a) Fill(b, new Rectangle(r.X, r.Y, 5, r.Height), a * alpha);
    }

    /// <summary>
    /// A plate's EDGE and accent alone — for a plate whose face is art (a region banner, a portrait
    /// well): draw the art, then this over it, so the plate keeps its hairline and its accent rule
    /// instead of the art painting them out (release polish 2026-09-05, map-10).
    /// </summary>
    public void PlateEdge(SpriteBatch b, Rectangle r, Color? accent = null, float alpha = 1f)
    {
        var rule = UiInk.Rule * alpha;
        Fill(b, new Rectangle(r.X, r.Y, r.Width, 1), rule);
        Fill(b, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), rule);
        Fill(b, new Rectangle(r.X, r.Y, 1, r.Height), rule);
        Fill(b, new Rectangle(r.Right - 1, r.Y, 1, r.Height), rule);
        if (accent is { } a) Fill(b, new Rectangle(r.X, r.Y, 5, r.Height), a * alpha);
    }

    /// <summary>
    /// WHICH FRAME ART a panel of this shape will wear. Chosen by aspect ratio, so resizing a panel
    /// silently changes its texture — and the three have visibly different ornaments.
    /// </summary>
    /// <remarks>
    /// It bit twice in one session (the HUNT skills rail, the Forge bag's counter), so the switch lives
    /// in one public method rather than inline: the layout helpers below have to ask the same question
    /// the renderer does, and two copies of this arithmetic would drift.
    /// </remarks>
    public static string PanelArtKey(Rectangle r)
    {
        var aspect = r.Width / MathF.Max(1f, r.Height);
        return aspect >= 1.30f ? "ui_panel_medium"
             : aspect >= 0.82f ? "ui_panel_square"
             : "ui_panel_vertical";
    }

    /// <summary>
    /// How much deeper this panel's frame reaches than the house standard assumes.
    /// </summary>
    /// <remarks>
    /// Measured off the art, not guessed: at the panel's own centre the top ornament of
    /// <c>ui_panel_medium</c> is 20 source px deep and <c>ui_panel_vertical</c>'s is 21, but the SQUARE
    /// frame carries a crest that runs to 51 — two and a half times as far. That single fact is why
    /// TrainingScreen's PROGRESS panel had to write its title at +40 while every other panel on the same
    /// screen wrote +22, and why ROSTER's detail column sat 27 px lower than the grid beside it. The
    /// screens had each rediscovered it by eye and written a different number; this returns it.
    /// </remarks>
    public static int FrameDrop(Rectangle r) => PanelArtKey(r) == "ui_panel_square" ? UiTypography.SquareFrameDrop : 0;

    /// <summary>
    /// This panel's right content edge for a row that sits high enough to run into the CORNER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ContentRight"/> answers for the panel's SIDE RAIL, which is what almost every row
    /// meets. A row inside the top <see cref="PanelCorner"/> px meets the corner instead, and the corner
    /// is where the scrollwork is: on the GEAR screen the EQUIPPED header's right-aligned GEAR POWER
    /// label was drawn under the ornament's curl, so it read "GEAR POWE" with a gold flourish over the R.
    /// </para>
    /// <para>
    /// The panel already picks its art by aspect to keep the SIDE ornament off its content
    /// (see GearScreen's column widths), which fixed the same class of fault one band lower. This is
    /// that rule for the top band, in one place rather than in each screen's eye.
    /// </para>
    /// </remarks>
    /// <summary>
    /// How far down an ornate panel the CORNER scrollwork reaches before the plain side rail takes over,
    /// and how far in it comes while it does. Measured off a rendered frame, not read off the texture.
    /// </summary>
    /// <remarks>
    /// The texture cannot be read for this directly: a nine-slice puts part of the corner flourish in the
    /// top EDGE strip, which is stretched, so the ornament's drawn extent is not its source extent. The
    /// numbers come from scanning a real capture of the EQUIPPED panel for gold, row by row, measuring
    /// how far in from the frame's outer edge the scrollwork reaches:
    /// <code>
    ///   y=150  reaches 94 px in      y=220  reaches 47 px in
    ///   y=180  reaches 73 px in      y=240  reaches 22 px in   &lt;- the plain rail
    ///   y=200  reaches 49 px in      y=260  reaches 18 px in
    /// </code>
    /// So the flourish occupies the top ~120 px against a side rail of 18, and the header row sits in it.
    /// The INSET has to beat the panel's ordinary <see cref="PadX"/> to change anything — the first
    /// attempt used 56, which lost the <c>Math.Max</c> to PadX and moved the label not one pixel. 96 is
    /// the measured reach plus clearance, confirmed by capture at 100 / 125 / 150.
    /// <see cref="PanelCorner"/> is the nine-slice's SOURCE corner: a different number for a different
    /// job, and using it here left the label still under the curl.
    /// </remarks>
    public const int OrnamentBand = 120, OrnamentInset = 96;

    public static int ContentRightAt(Rectangle r, int y, int rowHeight = 0)
        => InOrnamentBand(r, y, rowHeight) ? r.Right - Math.Max(PadX(r), UiMetrics.Space(OrnamentInset))
                                           : ContentRight(r);

    /// <summary>The mirror of <see cref="ContentRightAt"/> for a left-aligned row in the top band.</summary>
    public static int ContentLeftAt(Rectangle r, int y, int rowHeight = 0)
        => InOrnamentBand(r, y, rowHeight) ? r.X + Math.Max(PadX(r), UiMetrics.Space(OrnamentInset))
                                           : ContentLeft(r);

    /// <summary>Does a row of this height, starting at this y, overlap the panel's corner flourish?</summary>
    private static bool InOrnamentBand(Rectangle r, int y, int rowHeight)
        => PanelArtKey(r) is "ui_panel_square" or "ui_panel_vertical"
           && y < r.Y + UiMetrics.Space(OrnamentBand) + FrameDrop(r)
           && y + rowHeight > r.Y;

    /// <summary>Where this panel's TITLE sits, as an absolute y. See <see cref="UiTypography.PanelTitleTop"/>.</summary>
    public static int TitleTop(Rectangle r) => r.Y + UiTypography.PanelTitleTop + FrameDrop(r);

    /// <summary>Where this panel's one-line CAPTION sits, under its title.</summary>
    public static int CaptionTop(Rectangle r) => r.Y + UiTypography.PanelCaptionTop + FrameDrop(r);

    /// <summary>Where this panel's first content row starts, under a title AND a caption.</summary>
    public static int BodyTop(Rectangle r) => r.Y + UiTypography.PanelBodyTop + FrameDrop(r);

    /// <summary>Where this panel's first content row starts under a title with NO caption.</summary>
    public static int BodyTopBare(Rectangle r) => r.Y + UiTypography.PanelBodyTopBare + FrameDrop(r);

    /// <summary>
    /// This panel's left/right content inset: the house margin, widened for a narrow plate's sake and for
    /// the square frame's deeper side rails. See <see cref="UiTypography.PanelPadX"/>.
    /// </summary>
    public static int PadX(Rectangle r)
        // A SQUARE-FRAMED PANEL NEVER TAKES THE NARROW MARGIN, however narrow it is: its side rails are
        // 49 source px deep, so the narrow 24 would put the first column ON the flourish. TrainingScreen's
        // PROGRESS panel is 372 px wide and had already been pushed to 60 by hand for exactly this.
        => (PanelArtKey(r) == "ui_panel_square" || r.Width >= UiTypography.WidePanelFrom
                ? UiTypography.PanelPadX
                : UiTypography.PanelPadNarrow)
           + FrameDrop(r);

    /// <summary>This panel's left content edge, as an absolute x.</summary>
    public static int ContentLeft(Rectangle r) => r.X + PadX(r);

    /// <summary>This panel's right content edge, as an absolute x — where a right-aligned value ends.</summary>
    public static int ContentRight(Rectangle r) => r.Right - PadX(r);

    /// <summary>This panel's bottom content edge — the last row must end above it.</summary>
    public static int ContentBottom(Rectangle r) => r.Bottom - UiTypography.PanelPadBottom - FrameDrop(r);

    private void PanelAt(SpriteBatch b, Rectangle r, Color tint, bool gold, string? artKey = null)
    {
        var key = artKey ?? PanelArtKey(r);
        var tex = Assets.Get(key);
        if (tex is null)
        {
            Fill(b, r, PanelBg);
            var edge = gold ? new Color(0xF0, 0xA8, 0x30) : PanelEdge;
            Fill(b, new Rectangle(r.X, r.Y, r.Width, 1), edge);
            Fill(b, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), edge);
            return;
        }

        NineSlice(b, tex, r, PanelCorner, tint);
    }

    /// <summary>
    /// How far a <see cref="Panel"/>'s ornate border reaches in from its rectangle.
    /// </summary>
    /// <remarks>
    /// Was 52. Screens had been written against a plain rectangle and inset their content by 24–32, so on
    /// several panels the first column of text sat ON the filigree and right-aligned values were clipped by
    /// the opposite edge. Thinning the border to 40 is the change that costs nothing — the corner art is
    /// sampled whole and drawn smaller, so it reads as a FINER frame rather than a cropped one — and it
    /// closes most of the gap without touching several hundred hand-placed literals.
    /// </remarks>
    /// <summary>A straight line of a given thickness, as ONE rotated quad rather than a stamp of squares.</summary>
    /// <remarks>
    /// <b>THE MASTERY TREE DREW EVERY EDGE BY STAMPING AN 8x8 SQUARE AT EVERY PIXEL OF ITS LONGER AXIS.</b>
    /// Three consequences, all of them visible: the thickness was a hardcoded 8 with no zoom term, so at
    /// the default zoom a wire was 29% of the node it connected and at minimum zoom 57% — the tree read
    /// as a grey asterisk; a square brush is 8*sqrt(2) = 11.3px across on a diagonal, so the diagonals
    /// were half again as fat as the horizontals; and fifty edges averaging 150px cost about 7,500
    /// sprite draws per frame, rising to 35,000 zoomed in.
    ///
    /// One quad is one draw, its thickness is perpendicular so a diagonal is no fatter than a
    /// horizontal, and the width can be fractional — which is what lets it scale with the camera.
    /// </remarks>
    /// <summary>
    /// A cooldown sweep over a round icon: the part of the circle still WAITING is shaded, from the
    /// moving edge clockwise round to twelve o'clock, and the shade unwinds clockwise as
    /// <paramref name="ready"/> grows from 0 to 1 — the clock-face wipe every action game uses.
    /// Drawn as a fan of thin radial strokes from one pixel texture, so it batches with everything else.
    /// Nothing is drawn at ready ≥ 1 (the icon is lit) and the whole disc is shaded at ready ≤ 0.
    /// </summary>
    public void CooldownSweep(SpriteBatch b, Vector2 centre, float radius, float ready, Color shade, Color edge)
    {
        ready = Math.Clamp(ready, 0f, 1f);
        if (ready >= 1f) return;
        const float step = MathF.PI / 72f;                     // 2.5° strokes
        var from = -MathF.PI / 2f + ready * MathF.Tau;          // the moving edge (twelve o'clock + the elapsed share)
        var to = -MathF.PI / 2f + MathF.Tau;                     // round to twelve o'clock again
        var thick = MathF.Max(2f, radius * step * 1.9f);
        // A RING, not a pie. Filled wedges from the centre covered the medallion's own glyph, so for most
        // of every second the player could not see WHICH skill a slot held — and a skill that is never on
        // cooldown (an Aura) was painted out entirely (review 2026-08-30). The waiting share is a band
        // around the rim; the art inside stays readable.
        var inner = radius * 0.84f;   // a thin band ON the medallion's rim — see the note above
        for (var a = from; a < to; a += step)
        {
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            LineSeg(b, centre + dir * inner, centre + dir * radius, thick, shade);
        }
        // the edge that moves, so the eye can read the sweep even when it is slow
        var e = new Vector2(MathF.Cos(from), MathF.Sin(from));
        LineSeg(b, centre + e * inner, centre + e * radius, 2f, edge);
    }

    public void LineSeg(SpriteBatch b, Vector2 a, Vector2 c, float thickness, Color col)
    {
        var d = c - a;
        var len = d.Length();
        if (len < 0.5f || thickness <= 0f) return;
        b.Draw(_pixel, a, null, col, MathF.Atan2(d.Y, d.X), new Vector2(0f, 0.5f),
               new Vector2(len, thickness), SpriteEffects.None, 0f);
    }

    /// <summary>
    /// A scrim rect, in AUTHORED coordinates, that actually covers the whole picture.
    /// </summary>
    /// <remarks>
    /// <b>EVERY MENU SCREEN WAS DIMMING 1920x1080 AND THE PICTURE IS BIGGER THAN THAT.</b> These screens
    /// are authored in 1920x1080 and drawn through Game1's overlay inset — scale 1720/1920 = 0.8958,
    /// translated right by the nav rail's 180px. So authored y 1080 lands at canvas y 967, and authored
    /// x 1920 lands at canvas x 1900: a 112px band across the bottom of every menu screen, plus a 20px
    /// strip down the right, showed the fight scene UNDIMMED behind the panels.
    ///
    /// That band is most of what "çok fazla boş alan var, her şey yukarıya sıkışmış" was pointing at. It
    /// is not empty space a panel failed to fill — it is a brighter, busier strip that makes the dimmed
    /// area above it read as a box the content has been squeezed into.
    ///
    /// 2144 x 1206 authored covers canvas 1920 x 1080 with a margin: 2144 * 0.8958 + 180 = 2100, and
    /// 1206 * 0.8958 = 1080. Overshoot is free — Fill clips to the render target.
    /// </remarks>
    public static readonly Rectangle OverlayScrim = new(0, 0, 2144, 1206);

    public const int PanelCorner = 40;

    /// <summary>
    /// The usable interior of a panel: its rectangle less the ornate border.
    /// </summary>
    /// <remarks>
    /// The inset is clamped the same way <see cref="NineSlice"/> clamps its corner, so a SHORT panel (a
    /// settings row is 54px tall) gets a proportionally smaller border rather than an inverted rectangle.
    /// Without the clamp this returned a negative-height rect and everything drawn into it vanished.
    /// </remarks>
    public static Rectangle PanelInner(Rectangle r)
    {
        var c = Math.Max(2, Math.Min(PanelCorner, Math.Min(r.Width, r.Height) / 2 - 2));
        return new Rectangle(r.X + c, r.Y + c, r.Width - c * 2, r.Height - c * 2);
    }

    /// <summary>
    /// A 9-slice at NATIVE resolution: a source corner of S px is drawn at S/4 logical, landing 1:1 on the
    /// 4×-scaled canvas (crisp corners, no upscale blur). Edges and the center stretch. <paramref name="srcCornerMax"/>
    /// is the corner slice in ASSET px — set it to cover the decorative corner (gem cluster).
    /// </summary>
    /// <summary>
    /// Draw a frame texture by key as a 9-slice — corners at native scale, edges and centre stretched.
    /// </summary>
    /// <remarks>
    /// The stage banner, the welcome toast and the currency pill each stretched a whole frame texture
    /// into a rect of a wildly different aspect (a 384x224 modal frame into 560x135 — 2.4x anisotropic,
    /// and a DOWNscale on the vertical), which is exactly the "çerçeve scaleden dolayı sırıtıyor" of the
    /// 2026-08-23 playtest. The slicer existed but was private; this is its public door. Falls back to
    /// the flat panel when the key is missing.
    /// </remarks>
    public void PanelNine(SpriteBatch b, Rectangle r, string key, int cornerPx = PanelCorner, Color? tint = null)
    {
        if (Assets.Get(key) is { } tex) NineSlice(b, tex, r, cornerPx, tint ?? Color.White);
        else Panel(b, r);
    }

    /// <summary>
    /// The most any raster may be enlarged past its own pixels (BRIEF sec.74, and the same 1.25 the VFX
    /// asset-scale budget uses, so one habit reads both ledgers).
    /// </summary>
    /// <remarks>
    /// Above this a hand-drawn edge visibly softens under LinearClamp. The rule's teeth are that the
    /// answer to "it looks small" is a bigger ASSET, never a bigger number — see LAW 16.
    /// </remarks>
    public const float RasterCeiling = 1.25f;

    /// <summary>
    /// The frame slicer every panel goes through. The corner size argument is HISTORY: the corner is
    /// measured off the art now (<see cref="FrameSpecOf"/>), and the slice stretches only plain rails.
    /// </summary>
    private void NineSlice(SpriteBatch b, Texture2D tex, Rectangle r, int srcCornerMax, Color tint)
        => SliceFrame(b, tex, r, tint);

    // ── Bars ──────────────────────────────────────────────────────────────────────────────────────
    //
    // THE PROGRESS BAR CONTRACT (release polish, 2026-09-05). Two bars, and only two:
    //
    //   BarArt(r, pct, type)     the ORNATE bar — the package_01 frame (health / boss / shield / progress
    //                            / xp) through SliceFrame, so its caps and centre scroll are drawn in
    //                            proportion to the bar's height and only the plain stone stretches. For
    //                            bars 22 px and taller: a life pool, a boss, a conquest, a Warren level.
    //   Bar(x, y, w, h, pct, c)  the SLIM bar — a crisp procedural strip in the caller's colour, drawn
    //                            from rectangles at integer pixels. For every thin bar: a gate's progress,
    //                            a training rank, a quest count. It has no raster at all, so it cannot be
    //                            stretched, blurred or magnified at any UI SCALE.
    //
    // Bar used to draw the whole 256x64 ui_bar_frame into a 16 px strip — a 4x squash of scrollwork into
    // three rows of mush — which is the "stretched, low quality" progress bar the ROSTER's character
    // gates wore. Below 22 px the ornate art cannot read; above it the slim bar looks bare. One threshold,
    // in UiTypography's own terms, and a caller that wants the ornate bar names its type.

    /// <summary>The tallest a bar may be and still be drawn SLIM; from here up, use <see cref="BarArt"/>.</summary>
    public const int SlimBarMax = 21;

    /// <summary>The slim bar's frame ink — a dark bronze, the quiet frame's own, so it belongs to the family without claiming attention.</summary>
    private static readonly Color SlimBarEdge = new(0x5A, 0x4A, 0x36);

    /// <summary>The slim bar's well — the depleted part.</summary>
    private static readonly Color SlimBarWell = UiInk.Ground;

    /// <summary>
    /// The SLIM bar: a dark well with a one-pixel bronze frame (corner pixels knocked out, so it reads as
    /// a rounded strip at native density), and the fill in <paramref name="color"/> with a one-pixel
    /// highlight along its top and a one-pixel shade along its bottom, so it has the same lit-from-above
    /// bevel the ornate fills carry. Every edge is an integer rectangle — nothing is sampled.
    /// </summary>
    public void Bar(SpriteBatch b, int x, int y, int w, int h, float pct, Color color)
    {
        pct = Math.Clamp(pct, 0f, 1f);
        if (w < 4 || h < 3) return;
        var r = new Rectangle(x, y, w, h);
        Fill(b, r, SlimBarWell);
        // The frame: four one-pixel lines, each stopping one pixel short of the corner.
        Fill(b, new Rectangle(x + 1, y, w - 2, 1), SlimBarEdge);
        Fill(b, new Rectangle(x + 1, r.Bottom - 1, w - 2, 1), SlimBarEdge);
        Fill(b, new Rectangle(x, y + 1, 1, h - 2), SlimBarEdge);
        Fill(b, new Rectangle(r.Right - 1, y + 1, 1, h - 2), SlimBarEdge);
        if (pct <= 0f) return;
        var inner = new Rectangle(x + 2, y + 2, w - 4, h - 4);
        if (inner.Width <= 0 || inner.Height <= 0) { Fill(b, new Rectangle(x + 1, y + 1, Math.Max(1, (int)((w - 2) * pct)), h - 2), color); return; }
        var fw = Math.Max(1, (int)MathF.Round(inner.Width * pct));
        Fill(b, new Rectangle(inner.X, inner.Y, fw, inner.Height), color);
        if (inner.Height >= 4)
        {
            Fill(b, new Rectangle(inner.X, inner.Y, fw, 1), Color.Lerp(color, Color.White, 0.38f));
            Fill(b, new Rectangle(inner.X, inner.Bottom - 1, fw, 1), Color.Lerp(color, Color.Black, 0.35f));
        }
        // The leading edge: one brighter column, so a bar that is filling can be seen to move.
        if (fw >= 3 && pct < 1f) Fill(b, new Rectangle(inner.X + fw - 1, inner.Y, 1, inner.Height), Color.Lerp(color, Color.White, 0.55f));
    }

    // ── Currency pill ───────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// A small currency badge — icon + value + faint label — the reference's top-right currency row.
    /// Drawn right-aligned to <paramref name="right"/>; returns its LEFT edge so pills chain right-to-left.
    /// </summary>
    // ── THE RESOURCE CELL (2026-09-06): the one way a balance is shown on a strip. ─────────────────
    //    An icon in a dark well, a line over the amount (the resource's name, or the mark that says what
    //    the open action needs or gives), and the amount at the value rung. The FORGE's strip wore
    //    five hand-placed icon+name+figure stacks and read as a debug bar; this is the shared component
    //    so the quality cannot drift screen by screen.

    /// <summary>The well a resource icon sits in — a dark square with the plate's rule.</summary>
    public void IconWell(SpriteBatch b, Rectangle well, string? iconKey, Color fallback)
    {
        Fill(b, well, new Color(0x0C, 0x09, 0x12));
        var rule = UiInk.Rule;
        Fill(b, new Rectangle(well.X, well.Y, well.Width, 1), rule);
        Fill(b, new Rectangle(well.X, well.Bottom - 1, well.Width, 1), rule);
        Fill(b, new Rectangle(well.X, well.Y, 1, well.Height), rule);
        Fill(b, new Rectangle(well.Right - 1, well.Y, 1, well.Height), rule);
        var inset = Math.Max(3, well.Width / 7);
        var inner = new Rectangle(well.X + inset, well.Y + inset, well.Width - inset * 2, well.Height - inset * 2);
        if (iconKey is null || !Icon(b, iconKey, inner)) Diamond(b, inner, fallback);
    }

    /// <summary>
    /// A resource cell: the icon well, then <paramref name="line"/> (the name, or the action's mark)
    /// over <paramref name="amount"/>. <paramref name="glow"/> washes the cell in the accent while a
    /// balance is being counted up or down.
    /// </summary>
    public void ResourceCell(SpriteBatch b, Rectangle cell, string? iconKey, Color tint,
                             string line, Color lineInk, string amount, Color amountInk, float glow = 0f)
    {
        if (glow > 0f) Fill(b, cell, UiInk.Accent * (0.16f * glow));
        var wellEdge = Math.Min(UiMetrics.Control(40), cell.Height);
        var well = new Rectangle(cell.X, cell.Y + (cell.Height - wellEdge) / 2, wellEdge, wellEdge);
        IconWell(b, well, iconKey, tint);
        var tx = well.Right + UiMetrics.Space(10);
        var room = Math.Max(1, cell.Right - tx);
        var stack = UiTypography.Pitch(UiTypography.Caption) + UiTypography.Headline;
        var ty = cell.Y + Math.Max(0, (cell.Height - stack) / 2);
        TextBig(b, ShortenBig(line, room, UiTypography.Caption), tx, ty, lineInk, UiTypography.Caption);
        TextBig(b, ShortenBig(amount, room, UiTypography.Headline), tx, ty + UiTypography.Pitch(UiTypography.Caption), amountInk, UiTypography.Headline);
    }

    /// <summary>What a capsule of height <paramref name="h"/> wraps around its value: left pad, icon, gap, right pad.</summary>
    public static int PillChrome(int h) => h * 24 / 60 + h * 40 / 60 + h * 12 / 60 + h * 24 / 60;

    public int Pill(SpriteBatch b, int right, int y, string? iconKey, Color gem, string value, string label, Color accent, int h = 60)
    {
        // Measure returns width in the ACTIVE coordinate space; DrawCurrencyPills now draws this at scale 1 in
        // 1920 coords, so every fixed size/inset below is ×4 of its old 480-space value to match valW/labW.
        // The capsule's proportions are the 60 px design's, scaled to the height the host asks for — so at
        // 150 % the icon and the pads grow with the digits instead of the digits crowding a 100 % capsule
        // (release polish 2026-09-05, chrome-17).
        var valW = Measure(value);
        var labW = label.Length > 0 ? Measure(label) : 0;
        var icon = h * 40 / 60;
        var padX = h * 24 / 60;
        var gap = h * 12 / 60;
        var w = padX + icon + gap + valW + (labW > 0 ? 16 + labW : 0) + padX;
        var r = new Rectangle(right - w, y, w, h);

        // Ornate capsule from package_01 (guide), not a flat fill. Through the measured slicer: the small
        // frame's corners and mid-edge gems shrink TOGETHER to fit the capsule's height (SliceFrame scales
        // its fixed pieces to the rectangle), so the capsule wears a finer copy of the frame rather than a
        // 0.47 × 0.23 squash of it. The 2026-08-23 nine-slice that smeared the corners cut them at a fixed
        // 40 px; the measured one cuts at the ornament's own edge, which is what that attempt lacked.
        if (Assets.Get("ui_panel_small") is { } cap) SliceFrame(b, cap, r, Color.White);
        else
        {
            Fill(b, r, new Color(0x1A, 0x16, 0x26));
            Fill(b, new Rectangle(r.X, r.Y, r.Width, 4), accent * 0.55f);
            Fill(b, new Rectangle(r.X, r.Bottom - 4, r.Width, 4), Color.Black * 0.35f);
        }

        var ix = r.X + padX;
        if (iconKey is not null && Assets.Get(iconKey) is { } ic)
            b.Draw(ic, new Rectangle(ix, r.Y + (h - icon) / 2, icon, icon), Color.White);
        else
            Diamond(b, new Rectangle(ix + icon / 10, r.Y + (h - icon * 4 / 5) / 2, icon * 4 / 5, icon * 4 / 5), gem);

        var tx = ix + icon + gap;
        Text(b, value, tx, r.Y + h * 16 / 60, UiInk.Primary);
        if (labW > 0) Text(b, label, tx + valW + 16, r.Y + h * 20 / 60, UiInk.Secondary);
        return r.X;
    }

    // ── Buttons (mouse-clickable) ───────────────────────────────────────────────────────────────

    /// <summary>The left mouse button is down this frame — set by the host before any screen draws; the PRESSED state reads it.</summary>
    public static bool MouseHeld { get; set; }

    private static readonly Color Bone = UiInk.Primary;
    private static readonly Color Gold = UiInk.Accent;
    private static readonly Color Slate = UiInk.Secondary;

    /// <summary>
    /// Draw a clickable button and return true if it was clicked this frame.
    /// </summary>
    /// <remarks>
    /// Hover-lit when the mouse is over it. Disabled buttons render dim and never return true. This is
    /// the whole "click it" interaction model — every menu action is a labelled button, not a
    /// remembered key.
    /// </remarks>
    public bool Button(SpriteBatch b, Rectangle r, string label, Point mouse, bool clicked, bool enabled = true,
                       ButtonStyle style = ButtonStyle.Secondary)
    {
        var hover = enabled && r.Contains(mouse);
        // THE STANDARD STATES (UI polish §25–§29). HOVER eases in over ~100 ms as a thin luminance lift on
        // top of the art swap, so a hover is noticed without a card jumping; PRESSED is the mouse held
        // over the button — the whole face drops 2 px and darkens for exactly as long as the button is
        // held, and the click still fires on release like everything else. DISABLED keeps its label
        // readable (UiInk.Disabled on the grey art) so a button can say WHY it is off.
        var lift = UiMotion.Ease(UiMotion.KeyOf(r), hover ? 1f : 0f);
        var pressed = hover && MouseHeld;
        if (pressed) r = new Rectangle(r.X, r.Y + 2, r.Width, r.Height);
        // package_01 ornate buttons: DARK (secondary) at rest, GREEN (primary) on hover — and, since UX V2
        // P0.3, GREEN at rest too for the ONE button on a screen that is its primary action (brief §84:
        // one clear primary decision per screen, never five equal gold claims). GREY when disabled.
        var key = !enabled ? "ui_button_disabled"
                : hover || style == ButtonStyle.Primary ? "ui_button_primary"
                : "ui_button_secondary";
        // THE WELL, FIRST — ui_button_primary's interior is fully TRANSPARENT (this is why
        // <see cref="Field"/> paints one), so the moment a button took the lit art its face became a
        // window: a hovered UNEQUIP ALL standing over the page's own art showed the scene through
        // itself (build/shots/pressed_100.png, photographed the day RH_SHOT_HELD made a pressed face
        // visible at all). The resting secondary art is opaque and needs nothing, so the well goes
        // only under the lit one, inside the frame's own inset so no ornament is covered.
        // A PRIMARY IS LIT ON ITS FACE, not only at its rim (release polish 2026-09-05, gear-02). The
        // well used to be the panel's own near-black, inset by the end ornament's width, so the one lit
        // button on a screen read as a broken secondary: a dark rectangle floating inside a gold frame
        // with two darker bands where the transparent art showed the panel through. The well now runs
        // to the frame's own inset on every side and carries a warm lift toward the accent — a face a
        // player reads as "pressable" before the label is read.
        if (key == "ui_button_primary") Fill(b, PrimaryWell(r), PrimaryFace);
        var tex = Assets.Get(key);
        // Every button is sliced (playtest 2026-08-26: "the button frame is stretched and looks cheap" —
        // the 420 px reset button, the 400 px settings pair). The end scrollwork AND the centre ornament
        // are measured off the art and drawn in proportion to the button's height; only the plain runs
        // between them stretch. A button near the art's own aspect used to be stretched whole, which
        // squashed its scrollwork a little on every 52 px button; the slice costs nothing at that size.
        if (tex is not null) SliceFrame(b, tex, r, Color.White);
        else { Fill(b, r, hover ? Slate : PanelBg); Fill(b, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), enabled ? PanelEdge : Dim); }
        if (lift > 0f) Fill(b, ButtonFace(r), Color.White * (0.07f * lift));
        if (pressed) Fill(b, ButtonFace(r), Color.Black * 0.18f);

        // Every button surface is dark or deep-green, so the label is always LIGHT — bright cream on hover
        // for feedback, dim when disabled. (No more dark-ink-on-hover; that was the unreadable flip.)
        var label3 = !enabled ? UiInk.Disabled : hover ? new Color(0xF6, 0xEA, 0xC6) : Bone;
        // The SAME font as every other label. Buttons alone still went through the blocky pixel font, so
        // "UPGRADE" / "RE-ENTER" / "CONFIRM UPGRADE" rendered in a wide monospace face that belongs to no
        // other text on the screen — the single loudest inconsistency in the UI.
        // ALWAYS the structural weight, never the size-derived one. A button label is an action, and a
        // short button that shrank its text to fit would otherwise drop below the weight threshold and
        // come out lighter than the button beside it — the same control, two different voices.
        //
        // ONE SIZE FOR EVERY BUTTON. It used to be Clamp(height / 2, 14, 22), so a 44px button spoke at
        // 22 and a 36px one beside it at 18 — the same control, two sizes, decided by a dimension the
        // player cannot see. It is UiTypography.ButtonText now, shrinking only when the label genuinely
        // does not fit, and never past Caption. A very short button still gets a size that fits it.
        var px = Math.Min(UiTypography.ButtonText, Math.Max(UiTypography.Caption, r.Height - UiMetrics.Space(14)));
        // THE LABEL MUST CLEAR THE ART'S END ORNAMENT, not a flat 12px. The button art draws its
        // scrollwork over FieldCapWidth(height) at each end, which is 20px on a 44px button and 24 on a
        // 52 — so "MERGE THREES INTO BETTER" and "SALVAGE ALL THE JUNK" were printing straight onto it.
        var pad = Math.Max(UiTypography.ButtonPadX, FieldCapWidth(r.Height));
        while (px > UiTypography.Caption && Text2.Measure(label, px, TextFace.Strong) > r.Width - pad * 2) px--;
        TextCenterBig(b, label, r.Center.X, r.Center.Y - px * 27 / 40, label3, px, TextFace.Strong);
        return enabled && hover && clicked;
    }

    /// <summary>The ornamented end of the 256×96 button art, in source pixels — everything past it is plain border.</summary>
    private const int ButtonCapSrcPx = 44;   // ui-size-ok: source pixels in the button art, not type

    /// <summary>
    /// The FACE of a button: the area inside its own frame art, which is what the hover lift and the
    /// pressed darkening tint.
    /// </summary>
    /// <remarks>
    /// This used to be <see cref="PanelInner"/>, whose inset is the PANEL corner ornament — 40 px, or
    /// half the shorter side, whichever is less. On a button that is the whole button: a 64 px-tall
    /// button inset by 30 leaves a face FOUR PIXELS tall, so the hover lift was not a face at all but a
    /// bright stripe drawn straight through the label, and the pressed state was the same stripe in
    /// black. Every button in the game had it, at every profile (build/shots/btn_before.png).
    /// A button's frame is thin and proportional: an eighth of its height above and below, and the end
    /// ornament's own width at the sides (<see cref="FieldCapWidth"/>), so the tint stops where the
    /// scrollwork begins instead of washing over it.
    /// </remarks>
    private static Rectangle ButtonFace(Rectangle r)
    {
        var inset = Math.Max(2, r.Height / 8);
        var side = Math.Min(FieldCapWidth(r.Height), Math.Max(2, r.Width / 2 - 2));
        return new Rectangle(r.X + side, r.Y + inset, Math.Max(1, r.Width - side * 2), Math.Max(1, r.Height - inset * 2));
    }

    /// <summary>The primary button's well: inside the frame's own inset on every side, so no band of the panel shows between the well and the end scrollwork.</summary>
    private static Rectangle PrimaryWell(Rectangle r)
    {
        var inset = Math.Max(2, r.Height / 8);
        var side = Math.Max(2, r.Height / 6);
        return new Rectangle(r.X + side, r.Y + inset, Math.Max(1, r.Width - side * 2), Math.Max(1, r.Height - inset * 2));
    }

    /// <summary>The primary's face: the panel ground lifted a fifth of the way to the accent — warm, dark enough for a Bone label at 8:1.</summary>
    private static readonly Color PrimaryFace = Color.Lerp(new Color(0x1C, 0x16, 0x14), UiInk.Accent, 0.18f);

    /// <summary>
    /// How wide the button art's end ornament lands at a given control height — the inset a caller
    /// laying out its OWN content (see <see cref="Field"/>) must clear on both sides.
    /// </summary>
    public static int FieldCapWidth(int height)
        => Math.Max(2, (int)MathF.Round(ButtonCapSrcPx * height / 96f));

    /// <summary>
    /// A FIELD: the button's frame with no label of its own, for a control whose content the caller
    /// lays out — a dropdown showing a left-aligned value and a chevron, rather than a centred verb.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same three arts as <see cref="Button"/>, so a field and a button on one panel read as the
    /// same family: dark frame at rest, bright gold when <paramref name="lit"/> (hovered, or holding an
    /// open list), grey when disabled. Always 3-sliced — a field is wide by nature, and stretching the
    /// whole 256×96 art across 524 px is the "the frame looks cheap" of the 2026-08-26 playtest.
    /// </para>
    /// <para>
    /// The WELL IS PAINTED FIRST, and it has to be: <c>ui_button_primary</c>'s interior is fully
    /// transparent, so the lit state without it is a gold frame around whatever happens to be behind.
    /// </para>
    /// <para>
    /// DISABLED IS THE RESTING ART, DIMMED — not <c>ui_button_disabled</c>, which a <see cref="Button"/>
    /// wears. That asset has a light grey OPAQUE interior: it reads as an off switch, which is right for
    /// a verb and wrong for a field, where it makes the one control you cannot use the brightest thing
    /// on a near-black panel. Tinting the resting art keeps the same silhouette and only takes the gold
    /// out of it, which is what "you cannot change this right now" should look like.
    /// </para>
    /// </remarks>
    public void Field(SpriteBatch b, Rectangle r, bool enabled, bool lit, Color? well = null)
    {
        Fill(b, new Rectangle(r.X + 6, r.Y + 5, r.Width - 12, r.Height - 10), well ?? Ink);
        var key = enabled && lit ? "ui_button_primary" : "ui_button_secondary";
        if (Assets.Get(key) is { } tex) SliceFrame(b, tex, r, enabled ? Color.White : FieldOff);
        else
        {
            Fill(b, r, PanelBg);
            Fill(b, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), enabled ? PanelEdge : Dim);
        }
    }

    /// <summary>The multiply tint that takes the gold out of a field's frame when it is inert.</summary>
    private static readonly Color FieldOff = new(0x5C, 0x56, 0x62);

    // The 256×64 bar frames: the fill window starts 14 source px in from each end, behind the corner
    // scrollwork. The caps and the centre ornament themselves are measured off each frame (FrameSpecOf).
    private const int BarWindowInsetSrcPx = 14;   // ui-size-ok: source pixels in the bar frame art

    /// <summary>
    /// THE close button — one icon (icon_close, a gold × in a round medallion) for every panel, strip
    /// and overlay that can be shut. Draws it fitted in <paramref name="r"/>, brighter under the mouse,
    /// and returns true on the click. Playtest 2026-08-26: "replace the drawn × with a proper icon and
    /// use it everywhere." Falls back to the old dark square with a × if the art is missing.
    /// </summary>
    /// <summary>The close icon's edge, in pixels — one size everywhere so the corners of the game match.</summary>
    public const int CloseSize = 44;

    /// <summary>
    /// WHERE a close icon sits on an ornate panel: inside the frame's corner ornament, not on it. The
    /// nine-sliced frames keep <see cref="PanelCorner"/> px of scrollwork on every edge; the icon starts
    /// 8 px past that on the right and 4 px short of it on top, so it clears the ornament with a
    /// visible margin and lines up with the panel's title row (playtest 2026-08-26: "the close buttons
    /// have no margin, they run into the frame").
    /// </summary>
    /// <summary>
    /// The part of an element the player can actually see under a clip — the rectangle a hover, a
    /// click or a tip must test, never the element's own (2026-09-06). A card scrolled under a strip
    /// used to raise a tooltip from the rows nobody could see; a wholly hidden element gives an empty
    /// rectangle, which contains nothing.
    /// </summary>
    public static Rectangle VisibleWithin(Rectangle element, Rectangle clip) => Rectangle.Intersect(element, clip);

    public static Rectangle CloseRect(Rectangle panel, int size = 0)
    {
        if (size <= 0) size = UiMetrics.Control(CloseSize);   // a hit target: it follows the profile
        return new Rectangle(panel.Right - PanelCorner - 8 - size, panel.Y + PanelCorner - 4, size, size);
    }

    public bool CloseButton(SpriteBatch b, Rectangle r, Point mouse, bool clicked)
    {
        var hot = r.Contains(mouse);
        if (Assets.Get("icon_close") is { } t)
        {
            var box = hot ? new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4) : r;
            SpriteFit(b, t, box, hot ? Color.White : new Color(0xE0, 0xD8, 0xC8));
        }
        else
        {
            Fill(b, r, hot ? new Color(0x4A, 0x28, 0x30) : new Color(0x22, 0x1A, 0x30));
            TextCenterBig(b, "×", r.Center.X, r.Y + r.Height / 2 - 12, hot ? Color.White : Vellum, UiTypography.Headline);
        }
        return clicked && hot;
    }

    /// <summary>True if the mouse clicked inside a rectangle — for clickable list rows and slots.</summary>
    public static bool ClickedIn(Rectangle r, Point mouse, bool clicked) => clicked && r.Contains(mouse);

    /// <summary>
    /// A vertical scrollbar for a list showing <paramref name="visible"/> of <paramref name="total"/> rows
    /// from <paramref name="first"/>: a quiet track and a thumb whose length is the visible share. Drawn
    /// only when there is something to scroll. <see cref="UiMetrics.ScrollbarWidth"/> wide — the caller
    /// reserves that lane. Returns true when it drew, so a caller can shorten its rows once.
    /// </summary>
    public bool ScrollBar(SpriteBatch b, Rectangle track, int first, int visible, int total)
    {
        if (total <= visible || visible <= 0 || track.Height <= 0) return false;
        Fill(b, track, UiInk.Plate);
        Fill(b, new Rectangle(track.X, track.Y, track.Width, 1), UiInk.Rule);
        Fill(b, new Rectangle(track.X, track.Bottom - 1, track.Width, 1), UiInk.Rule);
        var thumbH = Math.Max(UiMetrics.Control(24), track.Height * visible / total);
        var maxFirst = Math.Max(1, total - visible);
        var thumbY = track.Y + (track.Height - thumbH) * Math.Clamp(first, 0, maxFirst) / maxFirst;
        Fill(b, new Rectangle(track.X + 2, thumbY, track.Width - 4, thumbH), UiInk.Secondary * 0.85f);
        return true;
    }

    /// <summary>The first visible row after a wheel notch, clamped so the last page stays full.</summary>
    public static int Scrolled(int first, int wheel, int visible, int total)
        => Math.Clamp(first - wheel, 0, Math.Max(0, total - visible));
    public static bool Hover(Rectangle r, Point mouse) => r.Contains(mouse);

    // ── Key-caps and icons ──────────────────────────────────────────────────────────────────────
    /// <summary>A key-cap chip with the key letters printed on it.</summary>
    public void KeyCap(SpriteBatch b, int x, int y, string key, Color? textColor = null)
    {
        var w = Math.Max(16, Measure(key) + 8);
        if (Assets.Get("ui_keycap") is { } cap) b.Draw(cap, new Rectangle(x, y, w, 16), Color.White);
        else { Fill(b, new Rectangle(x, y, w, 16), Dim); Fill(b, new Rectangle(x + 1, y + 1, w - 2, 14), PanelBg); }
        Font.DrawCentered(b, key, x + w / 2, y + 5, textColor ?? new Color(0xE8, 0xDF, 0xC8));
    }

    /// <summary>
    /// THE LOCK GLYPH: the padlock cut from the centre of the locked-slot tile, so it fills its box.
    /// </summary>
    /// <remarks>
    /// <c>ui_slot_locked</c> is a 128 px SLOT — a dark tile with a gold frame and a small padlock in the
    /// middle. Drawn whole at 20–24 px it was a grey smudge (BUILD), a lilac square (MAP) or, tinted
    /// with a dark ink on a coloured band, a solid black square (ROSTER) — release polish 2026-09-05,
    /// build-05 / map-05 / roster-01. This crops the padlock (the centre 62 of 128) and draws THAT, in
    /// the caller's tint, and draws a plain padlock from rectangles when the art is missing.
    /// </remarks>
    public void LockGlyph(SpriteBatch b, Rectangle box, Color tint)
    {
        if (Assets.Get("ui_slot_locked") is { } t)
        {
            // The padlock itself lives at 58..71 × 53..74 of the 128 px tile (measured); a breath of
            // dark tile around it, and the whole square frame stays out of the crop.
            var src = new Rectangle(t.Width * 54 / 128, t.Height * 50 / 128, t.Width * 22 / 128, t.Height * 28 / 128);
            var dh = box.Height;
            var dw = Math.Max(1, dh * src.Width / src.Height);
            var dst = new Rectangle(box.Center.X - dw / 2, box.Y, dw, dh);
            UiRasterLedger.Note("ui_slot_locked", src.Width, src.Height, dst.Width, dst.Height, "UiKit.LockGlyph");
            b.Draw(t, dst, src, tint);
            return;
        }
        var bodyH = box.Height * 11 / 20;
        var body = new Rectangle(box.X + box.Width / 8, box.Bottom - bodyH, box.Width * 3 / 4, bodyH);
        var sw = Math.Max(2, box.Width / 6);
        Fill(b, body, tint);
        Fill(b, new Rectangle(body.X + sw, box.Y, sw, body.Y - box.Y + 1), tint);
        Fill(b, new Rectangle(body.Right - sw * 2, box.Y, sw, body.Y - box.Y + 1), tint);
        Fill(b, new Rectangle(body.X + sw, box.Y, body.Width - sw * 2, sw), tint);
    }

    /// <summary>Draw an icon in a box, or nothing if it's missing (caller may draw a fallback first).</summary>
    public bool Icon(SpriteBatch b, string key, Rectangle box, Color? tint = null)
    {
        if (Assets.Get(key) is not { } t) return false;
        SpriteFit(b, t, box, tint);
        return true;
    }

    /// <summary>
    /// A gear class's icon in a box — <c>icon_class_warden</c> and its four siblings — or a diamond
    /// in the class colour (or <paramref name="fallback"/>) while that art has not shipped.
    /// </summary>
    /// <remarks>
    /// The same shape as the STATS screen's stat rows: try the key, draw the flat shape when it is
    /// absent. The five keys are built from <see cref="IdleXIdle.Core.Economy.ItemClasses.IconKey"/>,
    /// so tools/check_asset_keys.py cannot see them as literals; it checks the family by name instead.
    /// </remarks>
    public void ClassIcon(SpriteBatch b, IdleXIdle.Core.Economy.ItemClass cls, Rectangle box, Color? fallback = null)
    {
        if (Icon(b, IdleXIdle.Core.Economy.ItemClasses.IconKey(cls), box, Color.White)) return;
        var inset = box.Width / 6;
        Diamond(b, new Rectangle(box.X + inset, box.Y + inset, box.Width - 2 * inset, box.Height - 2 * inset),
                fallback ?? ClassColor(cls));
    }

    // ── Text proxies (route through the active font — smooth TTF or pixel fallback) ────────────────
    // THE UNSIZED PROXIES NOW HAVE A NAMED SIZE. They used to fall through to the font's raster height
    // (32px), which outranked every typography token but ScreenTitle — see UiTypography.Label for what
    // that did to the hierarchy of half the screens in the game.
    public void Text(SpriteBatch b, string s, int x, int y, Color c) => Text2.Draw(b, s, x, y, c, UiTypography.Label);
    public void TextRight(SpriteBatch b, string s, int right, int y, Color c) => Text2.DrawRight(b, s, right, y, c, UiTypography.Label);
    public void TextCenter(SpriteBatch b, string s, int cx, int y, Color c) => Text2.DrawCentered(b, s, cx, y, c, UiTypography.Label);

    // Sized text — build a type hierarchy (big titles/numbers, small labels). `px` is the logical height.
    // `face` picks the typeface: the data face at the weight `px` implies by default, or the ceremony
    // face for a screen title. See SmoothFont for why weight is derived from size rather than passed in.
    public void TextBig(SpriteBatch b, string s, int x, int y, Color c, int px, TextFace face = TextFace.Data) => Text2.Draw(b, s, x, y, c, px, face);
    public void TextRightBig(SpriteBatch b, string s, int right, int y, Color c, int px, TextFace face = TextFace.Data) => Text2.DrawRight(b, s, right, y, c, px, face);
    public void TextCenterBig(SpriteBatch b, string s, int cx, int y, Color c, int px, TextFace face = TextFace.Data) => Text2.DrawCentered(b, s, cx, y, c, px, face);
    public int MeasureBig(string s, int px, TextFace face = TextFace.Data) => Text2.Measure(s, px, face);

    /// <summary>
    /// Trim a string until it fits <paramref name="width"/> pixels, ending in an ellipsis.
    /// </summary>
    /// <remarks>
    /// Here rather than on one screen because the alternative is a second copy, and a screen without
    /// one does not fail loudly — it draws the label straight off the panel and through whatever the
    /// frame art has there, which reads as a rendering glitch rather than as a string that is too long.
    /// Every catalogue label is authored freely and no rule caps its length, so any list that draws one
    /// into a fixed column needs this.
    /// </remarks>
    public string Shorten(string text, int width)
    {
        if (string.IsNullOrEmpty(text) || width <= 0) return "";
        if (Measure(text) <= width) return text;
        var s = text;
        while (s.Length > 1 && Measure(s + "…") > width) s = s[..^1];
        return s.TrimEnd() + "…";
    }

    /// <summary>
    /// Break a sentence into lines that each fit <paramref name="width"/> pixels.
    /// </summary>
    /// <remarks>
    /// There was no wrapping in this codebase at all, which is why every explanatory string in it is a
    /// fragment sized to a column — "ON KILL: RICHER LOOT", 21 characters, because that is what fits on
    /// one line. That constraint is fine for a label and fatal for an explanation: a build game has to
    /// be able to say "deals NO damage; it opens a window in which everything else hits harder", and
    /// there is no way to shorten that into a caption without deleting the part the player needs.
    /// Breaks on spaces only — a word longer than the column is left long rather than cut mid-word,
    /// because a truncated word reads as a rendering fault.
    /// </remarks>
    public IReadOnlyList<string> WrapBig(string text, int width, int px)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || width <= 0) return lines;

        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && MeasureBig(candidate, px) > width)
            {
                lines.Add(line);
                line = word;
            }
            else line = candidate;
        }

        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    /// <summary>
    /// A small explanation panel beside the cursor — the hover tooltip the settings rows use.
    /// </summary>
    /// <remarks>
    /// Callers draw it LAST so it sits over the row it explains. Near the right or bottom edge of the
    /// 1920×1080 chrome space it flips to the other side of the anchor, so a tip on the last row is
    /// never clipped off screen. Deliberately a flat plate rather than an ornate panel: a tooltip is
    /// furniture, and the ornate frame is a claim of importance this text should not make.
    /// </remarks>
    public void HoverTip(SpriteBatch b, string text, Point anchor)
    {
        int width = UiMetrics.Text(430), pad = UiMetrics.Space(14);
        var lineH = UiTypography.Pitch(UiTypography.Secondary);
        var lines = WrapBig(text, width - pad * 2, UiTypography.Secondary);
        if (lines.Count == 0) return;
        var h = pad * 2 + lines.Count * lineH;
        // THROUGH THE ONE PLACEMENT RULE (PopoverPlacement, 2026-09-06): under and to the right of the
        // pointer first, above it before the page's ACTION BAND — the bottom rows where every inspector
        // keeps its refusal line and its one primary button — and never off the page on any side. It
        // used to flip left at the right edge with no left clamp, so a tip near the rail went negative.
        var actionBand = new Rectangle(0, PageBottom(UiMetrics.Space(36)) - UiMetrics.ButtonHeightPrimary - UiMetrics.Space(24),
                                       Page.Width, UiMetrics.ButtonHeightPrimary + UiMetrics.Space(60));
        var cursor = new Rectangle(anchor.X, anchor.Y, 26, 30);
        var tip = PopoverPlacement.Place(cursor, new Point(width, h), Page, new[] { actionBand },
                                         new[] { PopoverSide.Below, PopoverSide.Above, PopoverSide.Right, PopoverSide.Left }, 4);
        var x = tip.X;
        var y = tip.Y;
        // THE HOUSE PLATE (§2 QUIET), not a private purple box with a gold rule: a tip is furniture, and
        // furniture wears the tier every list and chip on the page wears. The 4 px shadow stays, so the tip
        // still lifts off what it covers.
        Fill(b, new Rectangle(x + 4, y + 5, width, h), new Color(0, 0, 0) * 0.45f);
        Plate(b, new Rectangle(x, y, width, h));
        var ty = y + pad;
        foreach (var l in lines) { TextBig(b, l, x + pad, ty, Vellum, UiTypography.Secondary); ty += lineH; }
    }

    /// <summary>The sized-text twin of <see cref="Shorten"/>, for anything drawn with TextBig.</summary>
    public string ShortenBig(string text, int width, int px)
    {
        if (string.IsNullOrEmpty(text) || width <= 0) return "";
        if (MeasureBig(text, px) <= width) return text;
        var s = text;
        while (s.Length > 1 && MeasureBig(s + "…", px) > width) s = s[..^1];
        return s.TrimEnd() + "…";
    }

    /// <summary>A bar from the package_01 art: the ornate frame (ui_bar_&lt;type&gt;_frame) with the pre-coloured
    /// fill (ui_bar_&lt;type&gt;_fill) clipped to <paramref name="pct"/> drawn INSIDE its window (on top, because
    /// the frame's centre is opaque). <paramref name="type"/> is health / mana / progress / boss / xp.</summary>
    public void BarArt(SpriteBatch b, Rectangle r, float pct, string type)
    {
        pct = Math.Clamp(pct, 0f, 1f);
        var frame = Assets.Get($"ui_bar_{type}_frame");
        if (frame is null)   // fallback: a clean styled bar
        {
            Fill(b, r, new Color(0, 0, 0, 160));
            if (pct > 0f) Fill(b, new Rectangle(r.X + 1, r.Y + 1, (int)((r.Width - 2) * pct), r.Height - 2), new Color(0xF0, 0xA8, 0x30));
            return;
        }
        // An opaque TRACK first. The frame art's window is not opaque everywhere, and on the enemy
        // nameplate — the one bar that floats over the scene rather than sitting on a panel — the
        // forest showed through the depleted section, so a half-dead enemy read as a bar with a hole.
        // EVERY bar is sliced (SliceFrame): the corner and centre ornaments are measured off each frame
        // and drawn in proportion to the bar's height, and only the plain stone between them stretches —
        // whole-image stretching smeared the boss bar's scrollwork 2.3x (playtest 2026-08-26: "the boss
        // bar's frame is stretched and looks cheap"), and the 120 px hunter bar it left un-sliced was
        // squashed 2.1x one way and 2.5x the other. The window's inset follows the scaled art.
        var scale = r.Height / (float)frame.Height;
        var insetX = Math.Max(2, (int)MathF.Round(BarWindowInsetSrcPx * scale));
        var win = new Rectangle(r.X + insetX, r.Y + Math.Max(2, r.Height * 30 / 100),
                                r.Width - insetX * 2, Math.Max(1, r.Height * 42 / 100));
        Fill(b, win, new Color(0x12, 0x0C, 0x10));
        SliceFrame(b, frame, r, Color.White);   // ornate frame + its (opaque) dark window
        if (pct > 0f && Assets.Get($"ui_bar_{type}_fill") is { } fill && fill.Width > 0)
        {
            var fw = Math.Max(1, (int)(win.Width * pct));
            var src = new Rectangle(0, 0, Math.Max(1, (int)(fill.Width * pct)), fill.Height);
            b.Draw(fill, new Rectangle(win.X, win.Y, fw, win.Height), src, Color.White);
        }
    }

    private static readonly Color NavInk = new(0x6E, 0x68, 0x62);

    /// <summary>
    /// The shared navigation legend — every destination, one line, at the canvas foot.
    /// </summary>
    /// <remarks>
    /// Screens are toggled by GLOBAL hotkeys (C/V/B/F/A/W/P work from anywhere), but only the fight screen
    /// showed the full list — leave it and you had no on-screen proof the other places even existed. Drawing
    /// the same legend on every menu screen fixes the "lost once you leave home" wayfinding gap the way
    /// Melvor's persistent sidebar does, at a one-line cost the tiny canvas can afford.
    /// </remarks>
    /// <summary>Superseded by the shared hexagonal nav bar the host draws over every screen. Kept as a no-op
    /// so existing callers need no change.</summary>
    public void NavLegend(SpriteBatch b, int y = 263) { }

    /// <summary>The reference's screen header: a turquoise gem, the title, and an optional subtitle.</summary>
    public void Title(SpriteBatch b, string title, string? sub = null)
    {
        // Baked 480-space coords → physical, scaled for the active canvas transform: s==1 at ×4 (480 screens,
        // unchanged), s==4 at ×1 (native-1920 screens). Text2's glyph SIZE is already scale-invariant; only the
        // POSITION literals need this. Keeps the header pixel-identical across the coordinate migration.
        var s = 4 / Scale;
        Diamond(b, new Rectangle(8 * s, 5 * s, 13 * s, 13 * s), new Color(0x5F, 0xE0, 0xC8));
        Text2.Draw(b, title, 26 * s, (sub is null ? 6 : 4) * s, UiInk.Accent);
        if (sub is not null) Text2.Draw(b, sub, 26 * s, 14 * s, new Color(0x8A, 0x82, 0xA0));
    }
}

/// <summary>How loud a button is. One PRIMARY per screen — the decision the screen exists for.</summary>
public enum ButtonStyle
{
    /// <summary>Dark at rest, lit on hover — every ordinary action.</summary>
    Secondary,

    /// <summary>Lit at rest: the screen's one primary decision (EQUIP, TAKE, OPEN ALL, HUNT HERE).</summary>
    Primary,
}
