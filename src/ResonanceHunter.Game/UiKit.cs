using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ResonanceHunter.Client;

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

    private static readonly Color VoidInk = new(0x1B, 0x16, 0x20);
    private static readonly Color PanelBg = new(0x24, 0x20, 0x2C);
    private static readonly Color PanelEdge = new(0x39, 0x33, 0x44);
    private static readonly Color Dim = new(0x3A, 0x3A, 0x44);

    // ── TEXT COLOURS. There are exactly two surfaces, and they want opposite ink. ─────────────────
    //
    // The hand-drawn UI made the panels PARCHMENT (measured mean luminance 180). Every text colour in
    // this game was chosen for the old panels, which were near-black (L≈18). So the game was drawing
    // Bone text (L=223) on a Bone panel: a contrast ratio of 1.22:1. Invisible.
    //
    // The fix is not a tweak, it is a rule, and it is MORE on-style rather than less: a manuscript is
    // parchment with iron-gall ink on it. Light panel, dark text.
    //
    // Measured against the two real surfaces (ratios are WCAG; 4.5:1 is the bar for body text):
    //
    //                        on PARCHMENT (L=180)   on the DIM ARENA (L≈54)
    //   Vellum   #EDE3C8          1.22:1  ✗              3.53:1  ok
    //   Ink      #14101A          6.25:1  ✓              1.90:1  ✗
    //   Slate    #57616F          1.77:1  ✗              1.63:1  ✗   <- failed on BOTH, always had
    //
    // Slate is the interesting one: it was never readable anywhere. It is why the muster screen's
    // hints ("SLOT 1 TAKES THE HITS") have always looked like a ghost. It is retired.

    /// <summary>Text ON CHROME — panels, buttons, anything parchment. 6.25:1. The default for UI.</summary>
    public static readonly Color Ink = new(0x14, 0x10, 0x1A);

    /// <summary>
    /// Secondary text on chrome — hints, costs, units. 3.35:1: fine for a label, not for a sentence.
    /// </summary>
    /// <remarks>
    /// There is no third option. On a parchment panel only near-black is readable, so hierarchy has to
    /// come from SIZE and POSITION, not from a lighter grey — which is exactly how a real manuscript
    /// does it. Reaching for "a slightly lighter ink" is how Slate happened.
    /// </remarks>
    public static readonly Color InkFaint = new(0x2C, 0x2C, 0x36);

    /// <summary>Text ON THE SCENE — over the dim arena, never on a panel. 3.53:1.</summary>
    public static readonly Color Vellum = new(0xED, 0xE3, 0xC8);

    /// <summary>
    /// "Gold" text ON PARCHMENT — a dark bronze, because Hearth Gold is invisible there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hearth Gold `#F0A830` is L=175. The parchment panel is L=180. That is <b>1.02:1</b> — gold text
    /// on a gold panel, the single worst contrast in the game and, cruelly, reserved for the states the
    /// player <i>earned</i>. Every "you did it" label was invisible.
    /// </para>
    /// <para>
    /// So on parchment, <b>gold stops being a text colour and becomes a SURFACE</b> — that is what
    /// <c>ui_panel_gold</c> is for. Where a word still has to carry earned-ness, this bronze does it at
    /// 3.6:1. A medieval scribe had exactly this problem and solved it the same way: gold leaf is a
    /// ground you letter ON, not an ink you letter WITH.
    /// </para>
    /// </remarks>
    public static readonly Color Bronze = new(0x3D, 0x26, 0x04);

    private readonly Texture2D _pixel;
    private readonly Texture2D _hex;
    private readonly Texture2D _diamond;
    private readonly System.Collections.Generic.Dictionary<string, float> _topPadCache = new();

    public UiKit(GraphicsDevice device, PixelFont font, AssetLibrary assets)
    {
        Font = font;
        Text2 = new SmoothFont(font);   // smooth TTF, or transparently the pixel font if none loads
        Assets = assets;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _hex = MakeHex(device, 120, 104);       // flat-top hexagon, drawn tinted + scaled (LinearClamp keeps it smooth)
        _diamond = MakeDiamond(device, 64);      // a gem for the nav / accents
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

    public bool AnimSprite(SpriteBatch b, string stripKey, Rectangle box, float seconds, float fps, bool loop, Color tint, float topCrop = 0f)
    {
        if (Assets.Get(stripKey) is not { } tex || tex.Height <= 0) return false;
        var fw = tex.Height;
        var frames = Math.Max(1, tex.Width / fw);
        var i = (int)(seconds * fps);
        i = loop ? (i % frames + frames) % frames : Math.Clamp(i, 0, frames - 1);
        // Trim the transparent headroom (and any streak artifacts) off the top of the frame, then FILL the
        // box height with the remaining figure, anchored bottom-centre. Side padding overflows harmlessly.
        // Fitting the whole square frame instead left the figure tiny and "boxed" inside the panel.
        var cropY = (int)(fw * Math.Clamp(topCrop, 0f, 0.6f));
        var srcH = tex.Height - cropY;
        var src = new Rectangle(i * fw, cropY, fw, srcH);
        var sc = box.Height / (float)srcH;
        var w = Math.Max(1, (int)(fw * sc));
        b.Draw(tex, new Rectangle(box.Center.X - w / 2, box.Bottom - box.Height, w, box.Height), src, tint);
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

    /// <summary>Logical width of a string in the active font. Use this instead of PixelFont.Measure.</summary>
    public int Measure(string s) => Text2.Measure(s);

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
        b.Draw(t, new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h),
            tint ?? Color.White);
    }

    // ── Backgrounds ─────────────────────────────────────────────────────────────────────────────
    /// <summary>Fill the whole canvas with a scene background, or the void colour if it's missing.</summary>
    public void Background(SpriteBatch b, string key, Color? tint = null)
    {
        // The shared chrome (batches A/C + title) draws at scale 1 in TRUE 1920×1080 coords, so the full-canvas
        // background must span 1920×1080, not the 480×270 logical grid. All Background callers are chrome now.
        if (Assets.Get(key) is { } bg) b.Draw(bg, new Rectangle(0, 0, 1920, 1080), tint ?? Color.White);
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
    public void Panel(SpriteBatch b, Rectangle r, bool gold = false)
    {
        var aspect = r.Width / MathF.Max(1f, r.Height);
        var key = aspect >= 1.30f ? "ui_panel_medium"
                : aspect >= 0.82f ? "ui_panel_square"
                : "ui_panel_vertical";
        var tex = Assets.Get(key);
        if (tex is null)
        {
            Fill(b, r, PanelBg);
            var edge = gold ? new Color(0xF0, 0xA8, 0x30) : PanelEdge;
            Fill(b, new Rectangle(r.X, r.Y, r.Width, 1), edge);
            Fill(b, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), edge);
            return;
        }

        NineSlice(b, tex, r, 52, gold ? new Color(0xFF, 0xDC, 0xA0) : Color.White);
    }

    /// <summary>
    /// A 9-slice at NATIVE resolution: a source corner of S px is drawn at S/4 logical, landing 1:1 on the
    /// 4×-scaled canvas (crisp corners, no upscale blur). Edges and the center stretch. <paramref name="srcCornerMax"/>
    /// is the corner slice in ASSET px — set it to cover the decorative corner (gem cluster).
    /// </summary>
    private void NineSlice(SpriteBatch b, Texture2D tex, Rectangle r, int srcCornerMax, Color tint)
    {
        var scale = Scale;
        var srcC = Math.Min(srcCornerMax, Math.Min(tex.Width, tex.Height) / 2 - 1);
        var dstC = Math.Max(2, Math.Min(srcC / scale, Math.Min(r.Width, r.Height) / 2));
        srcC = Math.Min(dstC * scale, Math.Min(tex.Width, tex.Height) / 2 - 1);
        var midSrcW = tex.Width - 2 * srcC;
        var midSrcH = tex.Height - 2 * srcC;
        var innerW = r.Width - 2 * dstC;
        var innerH = r.Height - 2 * dstC;

        void S(int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
        {
            if (dw <= 0 || dh <= 0 || sw <= 0 || sh <= 0) return;
            b.Draw(tex, new Rectangle(dx, dy, dw, dh), new Rectangle(sx, sy, sw, sh), tint);
        }

        S(0, 0, srcC, srcC, r.X, r.Y, dstC, dstC);
        S(tex.Width - srcC, 0, srcC, srcC, r.Right - dstC, r.Y, dstC, dstC);
        S(0, tex.Height - srcC, srcC, srcC, r.X, r.Bottom - dstC, dstC, dstC);
        S(tex.Width - srcC, tex.Height - srcC, srcC, srcC, r.Right - dstC, r.Bottom - dstC, dstC, dstC);
        S(srcC, 0, midSrcW, srcC, r.X + dstC, r.Y, innerW, dstC);
        S(srcC, tex.Height - srcC, midSrcW, srcC, r.X + dstC, r.Bottom - dstC, innerW, dstC);
        S(0, srcC, srcC, midSrcH, r.X, r.Y + dstC, dstC, innerH);
        S(tex.Width - srcC, srcC, srcC, midSrcH, r.Right - dstC, r.Y + dstC, dstC, innerH);
        S(srcC, srcC, midSrcW, midSrcH, r.X + dstC, r.Y + dstC, innerW, innerH);
    }

    // ── Bars (ui_bar_frame + ui_bar_fill, tinted) ───────────────────────────────────────────────
    public void Bar(SpriteBatch b, int x, int y, int w, int h, float pct, Color color)
    {
        pct = Math.Clamp(pct, 0f, 1f);
        var frame = Assets.Get("ui_bar_frame");
        var fill = Assets.Get("ui_bar_fill");

        if (frame is null || fill is null)
        {
            Fill(b, new Rectangle(x, y, w, h), Color.Lerp(VoidInk, Dim, 0.6f));
            if (pct > 0f) Fill(b, new Rectangle(x, y, (int)(w * pct), h), color);
            return;
        }

        b.Draw(frame, new Rectangle(x, y, w, h), Color.White);
        var inset = 2;
        var fw = (int)((w - inset * 2) * pct);
        if (fw > 0) b.Draw(fill, new Rectangle(x + inset, y + inset, fw, h - inset * 2), color);
    }

    // ── Currency pill ───────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// A small currency badge — icon + value + faint label — the reference's top-right currency row.
    /// Drawn right-aligned to <paramref name="right"/>; returns its LEFT edge so pills chain right-to-left.
    /// </summary>
    public int Pill(SpriteBatch b, int right, int y, string? iconKey, Color gem, string value, string label, Color accent)
    {
        // Measure returns width in the ACTIVE coordinate space; DrawCurrencyPills now draws this at scale 1 in
        // 1920 coords, so every fixed size/inset below is ×4 of its old 480-space value to match valW/labW.
        var valW = Measure(value);
        var labW = label.Length > 0 ? Measure(label) : 0;
        const int icon = 40, h = 60;
        var w = 24 + icon + 12 + valW + (labW > 0 ? 16 + labW : 0) + 24;
        var r = new Rectangle(right - w, y, w, h);

        // Ornate capsule from package_01 (guide), not a flat fill.
        if (Assets.Get("ui_panel_small") is { } cap) b.Draw(cap, r, Color.White);
        else
        {
            Fill(b, r, new Color(0x1A, 0x16, 0x26));
            Fill(b, new Rectangle(r.X, r.Y, r.Width, 4), accent * 0.55f);
            Fill(b, new Rectangle(r.X, r.Bottom - 4, r.Width, 4), Color.Black * 0.35f);
        }

        var ix = r.X + 24;
        if (iconKey is not null && Assets.Get(iconKey) is { } ic)
            b.Draw(ic, new Rectangle(ix, r.Y + 12, icon, icon), Color.White);
        else
            Diamond(b, new Rectangle(ix + 4, r.Y + 16, 32, 32), gem);

        var tx = ix + icon + 12;
        Text(b, value, tx, r.Y + 16, new Color(0xEC, 0xE6, 0xF2));
        if (labW > 0) Text(b, label, tx + valW + 16, r.Y + 20, new Color(0x8A, 0x82, 0xA0));
        return r.X;
    }

    // ── Buttons (mouse-clickable) ───────────────────────────────────────────────────────────────
    private static readonly Color Bone = new(0xE8, 0xDF, 0xC8);
    private static readonly Color Gold = new(0xF0, 0xA8, 0x30);
    private static readonly Color Slate = new(0x57, 0x61, 0x6F);

    /// <summary>
    /// Draw a clickable button and return true if it was clicked this frame.
    /// </summary>
    /// <remarks>
    /// Hover-lit when the mouse is over it. Disabled buttons render dim and never return true. This is
    /// the whole "click it" interaction model — every menu action is a labelled button, not a
    /// remembered key.
    /// </remarks>
    public bool Button(SpriteBatch b, Rectangle r, string label, Point mouse, bool clicked, bool enabled = true)
    {
        var hover = enabled && r.Contains(mouse);
        // package_01 ornate buttons: DARK (secondary) at rest, GREEN (primary) on hover, GREY when disabled.
        var key = !enabled ? "ui_button_disabled" : hover ? "ui_button_primary" : "ui_button_secondary";
        var tex = Assets.Get(key);
        // Whole-image stretch (not 9-slice): the game's buttons are short, and a fixed-size ornate corner
        // would dominate them and bury the label. Stretching keeps the border proportionally thin.
        if (tex is not null) b.Draw(tex, r, Color.White);
        else { Fill(b, r, hover ? Slate : PanelBg); Fill(b, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), enabled ? PanelEdge : Dim); }

        // Every button surface is dark or deep-green, so the label is always LIGHT — bright cream on hover
        // for feedback, dim when disabled. (No more dark-ink-on-hover; that was the unreadable flip.)
        var label3 = !enabled ? new Color(0x7C, 0x76, 0x88) : hover ? new Color(0xF6, 0xEA, 0xC6) : Bone;
        // The pixel font is drawn 1:1 in the ×4 canvas (s==1) or ×4 in the ×1 native canvas (s==4), so the
        // label keeps the SAME physical size either way. The vertical nudge scales with it.
        var s = 4 / Scale;
        Font.DrawCentered(b, label, r.Center.X, r.Center.Y - 3 * s, label3, s);
        return enabled && hover && clicked;
    }

    /// <summary>Horizontal 3-slice: fixed <paramref name="cap"/>-wide ends, stretched middle.</summary>
    private void HSlice(SpriteBatch b, Texture2D t, Rectangle r, int cap, Color tint)
    {
        var midSrc = t.Width - 2 * cap;
        var midDst = r.Width - 2 * cap;
        b.Draw(t, new Rectangle(r.X, r.Y, cap, r.Height), new Rectangle(0, 0, cap, t.Height), tint);
        if (midDst > 0) b.Draw(t, new Rectangle(r.X + cap, r.Y, midDst, r.Height), new Rectangle(cap, 0, midSrc, t.Height), tint);
        b.Draw(t, new Rectangle(r.Right - cap, r.Y, cap, r.Height), new Rectangle(t.Width - cap, 0, cap, t.Height), tint);
    }

    /// <summary>True if the mouse clicked inside a rectangle — for clickable list rows and slots.</summary>
    public static bool ClickedIn(Rectangle r, Point mouse, bool clicked) => clicked && r.Contains(mouse);
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

    /// <summary>Draw an icon in a box, or nothing if it's missing (caller may draw a fallback first).</summary>
    public bool Icon(SpriteBatch b, string key, Rectangle box, Color? tint = null)
    {
        if (Assets.Get(key) is not { } t) return false;
        SpriteFit(b, t, box, tint);
        return true;
    }

    // ── Text proxies (route through the active font — smooth TTF or pixel fallback) ────────────────
    public void Text(SpriteBatch b, string s, int x, int y, Color c) => Text2.Draw(b, s, x, y, c);
    public void TextRight(SpriteBatch b, string s, int right, int y, Color c) => Text2.DrawRight(b, s, right, y, c);
    public void TextCenter(SpriteBatch b, string s, int cx, int y, Color c) => Text2.DrawCentered(b, s, cx, y, c);

    // Sized text — build a type hierarchy (big titles/numbers, small labels). `px` is the logical height.
    public void TextBig(SpriteBatch b, string s, int x, int y, Color c, int px) => Text2.Draw(b, s, x, y, c, px);
    public void TextRightBig(SpriteBatch b, string s, int right, int y, Color c, int px) => Text2.DrawRight(b, s, right, y, c, px);
    public void TextCenterBig(SpriteBatch b, string s, int cx, int y, Color c, int px) => Text2.DrawCentered(b, s, cx, y, c, px);
    public int MeasureBig(string s, int px) => Text2.Measure(s, px);

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
        b.Draw(frame, r, Color.White);   // ornate frame + its (opaque) dark window
        if (pct > 0f && Assets.Get($"ui_bar_{type}_fill") is { } fill && fill.Width > 0)
        {
            // The fill sits inside the frame window — insets carry the ornate border/gem out of the fill area.
            var win = new Rectangle(r.X + Math.Max(2, r.Width * 7 / 100), r.Y + Math.Max(2, r.Height * 30 / 100),
                                    r.Width - Math.Max(4, r.Width * 14 / 100), Math.Max(1, r.Height * 42 / 100));
            var fw = Math.Max(1, (int)(win.Width * pct));
            var src = new Rectangle(0, 0, Math.Max(1, (int)(fill.Width * pct)), fill.Height);
            b.Draw(fill, new Rectangle(win.X, win.Y, fw, win.Height), src, Color.White);
        }
    }

    private static readonly Color NavInk = new(0x57, 0x61, 0x6F);

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
        Text2.Draw(b, title, 26 * s, (sub is null ? 6 : 4) * s, new Color(0xF0, 0xB2, 0x4A));
        if (sub is not null) Text2.Draw(b, sub, 26 * s, 14 * s, new Color(0x8A, 0x82, 0xA0));
    }
}
