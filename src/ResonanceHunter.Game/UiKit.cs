using System;
using System.Collections.Generic;
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
    public static Color ClassColor(ResonanceHunter.Core.Economy.ItemClass cls) => cls switch
    {
        ResonanceHunter.Core.Economy.ItemClass.Warden => new Color(0xD6, 0x48, 0x5C),
        ResonanceHunter.Core.Economy.ItemClass.Ranger => new Color(0x48, 0xB8, 0x88),
        ResonanceHunter.Core.Economy.ItemClass.Mystic => new Color(0x74, 0xC6, 0xE8),
        ResonanceHunter.Core.Economy.ItemClass.Bulwark => new Color(0xC0, 0x6E, 0xE0),
        _ => new Color(0xB4, 0xB8, 0x62),
    };

    private readonly Texture2D _pixel;
    private readonly Texture2D _hex;
    private readonly Texture2D _diamond;
    private readonly Texture2D _blob;
    private readonly System.Collections.Generic.Dictionary<string, float> _topPadCache = new();
    private readonly System.Collections.Generic.Dictionary<string, float> _bottomPadCache = new();

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
        var i = (int)(seconds * fps);
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
        var src = new Rectangle(i * fw, cropY, fw, srcH);
        var sc = box.Height / (float)srcH;
        var w = Math.Max(1, (int)(fw * sc));
        // Same grounding as SpriteGrounded. The pad is measured once for the whole strip rather than
        // per frame on purpose: a per-frame sole would make the figure slide up and down as the
        // animation played. One offset for the clip keeps the feet planted while it animates.
        var drop = (int)MathF.Round(StripBottomPadFraction(stripKey) * fw * sc);
        b.Draw(tex, new Rectangle(box.Center.X - w / 2, box.Y + drop, w, box.Height), src, tint,
               0f, Vector2.Zero, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        return true;
    }

    /// <summary>
    /// Empty rows under the lowest opaque pixel of an animation strip, as a fraction of FRAME height.
    /// </summary>
    /// <remarks>
    /// Measured across the whole strip, so every frame of a clip shares one ground offset and the figure
    /// does not bob as it plays. Cached per key — this scans the full texture, which for a boss strip is
    /// 8192x1024.
    /// </remarks>
    public float StripBottomPadFraction(string stripKey)
    {
        if (_bottomPadCache.TryGetValue(stripKey, out var cached)) return cached;
        var frac = 0f;
        if (Assets.Get(stripKey) is { } t && t is { Width: > 0, Height: > 0 })
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
        _bottomPadCache[stripKey] = frac;
        return frac;
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

    private readonly System.Collections.Generic.Dictionary<Texture2D, Vector4> _contentPad = new();

    /// <summary>
    /// A texture's transparent margins as fractions of its size: (left, top, right, bottom).
    /// </summary>
    /// <remarks>
    /// Generated art arrives on a square canvas with however much empty space the model felt like
    /// leaving — measured across the ten boot variants that is anywhere from 6% to 18% vertically. Any
    /// placement expressed against the CANVAS therefore lands somewhere different for every variant,
    /// which is why a worn boot calibrated on one trait sat halfway up the shin on another. Callers
    /// that place art by its content are immune to that.
    /// </remarks>
    public Vector4 ContentPad(Texture2D t)
    {
        if (_contentPad.TryGetValue(t, out var cached)) return cached;
        var pad = Vector4.Zero;
        if (t is { Width: > 0, Height: > 0 })
        {
            var data = new Color[t.Width * t.Height];
            t.GetData(data);
            int l = t.Width, r = 0, top = t.Height, bot = 0;
            for (var y = 0; y < t.Height; y++)
                for (var x = 0; x < t.Width; x++)
                    if (data[y * t.Width + x].A > 8)
                    {
                        if (x < l) l = x;
                        if (x >= r) r = x + 1;
                        if (y < top) top = y;
                        if (y >= bot) bot = y + 1;
                    }
            if (r > l && bot > top)
                pad = new Vector4(l / (float)t.Width, top / (float)t.Height,
                                  (t.Width - r) / (float)t.Width, (t.Height - bot) / (float)t.Height);
        }
        _contentPad[t] = pad;
        return pad;
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
        => PanelAt(b, r, gold ? new Color(0xFF, 0xDC, 0xA0) : Color.White, gold);

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
    /// </remarks>
    public void PanelQuiet(SpriteBatch b, Rectangle r) => PanelAt(b, r, QuietFrame, false);

    /// <summary>The tint that turns the gold frame to dark bronze. Multiplied, so the interior stays black.</summary>
    private static readonly Color QuietFrame = new(0x58, 0x52, 0x62);

    private void PanelAt(SpriteBatch b, Rectangle r, Color tint, bool gold)
    {
        // THE FRAME ART IS CHOSEN BY ASPECT RATIO, which means resizing a panel silently changes which
        // texture it wears — and the three have visibly different corner ornaments. It bit twice in one
        // session (the HUNT skills rail, the Forge bag's counter), so it is called out here rather than
        // left to be rediscovered.
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
        // Whole-texture stretch ON PURPOSE: the 256-px square frame's corner ornaments are ~50 px, so a
        // 9-slice into a 60-px capsule cuts them into the edge bands and smears them across the text
        // (tried 2026-08-23; reverted). The uniform squash reads as a capsule; the slice did not.
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
    // 0x8A96A8, was 0x57616F. The old value measured 1.77:1 on parchment and 1.63:1 on the dim
    // arena — it failed on BOTH of this game's surfaces, which is why every secondary label ("TEMPO
    // 4.94x SKILL RATE", "BATTLE SPEED", "Deepest wave reached") read as a ghost. This one measures
    // ~6.9:1 on the dark panels while staying clearly below Bone, so the hierarchy survives.
    private static readonly Color Slate = new(0x8A, 0x96, 0xA8);

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
        // Whole-image stretch for a button near the art's own aspect; a WIDE button is 3-sliced so its
        // ornamented ends keep their shape and only the plain middle stretches (playtest 2026-08-26:
        // "the button frame is stretched and looks cheap" — the 420 px reset button, the 400 px settings
        // pair). The end caps are the art's ornament, scaled with the button's height.
        if (tex is not null)
        {
            if (r.Width > r.Height * tex.Width / tex.Height * 1.15f) HSliceScaled(b, tex, r, ButtonCapSrcPx, Color.White);
            else b.Draw(tex, r, Color.White);
        }
        else { Fill(b, r, hover ? Slate : PanelBg); Fill(b, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), enabled ? PanelEdge : Dim); }

        // Every button surface is dark or deep-green, so the label is always LIGHT — bright cream on hover
        // for feedback, dim when disabled. (No more dark-ink-on-hover; that was the unreadable flip.)
        var label3 = !enabled ? new Color(0x7C, 0x76, 0x88) : hover ? new Color(0xF6, 0xEA, 0xC6) : Bone;
        // The SAME font as every other label. Buttons alone still went through the blocky pixel font, so
        // "UPGRADE" / "RE-ENTER" / "CONFIRM UPGRADE" rendered in a wide monospace face that belongs to no
        // other text on the screen — the single loudest inconsistency in the UI.
        // ALWAYS the structural weight, never the size-derived one. A button label is an action, and a
        // short button that shrank its text to fit would otherwise drop below the weight threshold and
        // come out lighter than the button beside it — the same control, two different voices.
        var px = Math.Clamp(r.Height / 2, 14, 22);
        while (px > 12 && Text2.Measure(label, px, TextFace.Strong) > r.Width - 24) px--;
        TextCenterBig(b, label, r.Center.X, r.Center.Y - px * 27 / 40, label3, px, TextFace.Strong);
        return enabled && hover && clicked;
    }

    /// <summary>The ornamented end of the 256×96 button art, in source pixels — everything past it is plain border.</summary>
    private const int ButtonCapSrcPx = 44;

    /// <summary>
    /// Horizontal 3-slice with the caps SCALED to the destination height: <paramref name="srcCap"/> source
    /// pixels at each end become srcCap × (r.Height ÷ t.Height) destination pixels, so the ornament keeps
    /// its own proportions at any button height; only the middle stretches.
    /// </summary>
    private static void HSliceScaled(SpriteBatch b, Texture2D t, Rectangle r, int srcCap, Color tint)
    {
        var dstCap = Math.Max(2, (int)MathF.Round(srcCap * r.Height / (float)t.Height));
        dstCap = Math.Min(dstCap, r.Width / 2);
        var midSrc = t.Width - 2 * srcCap;
        var midDst = r.Width - 2 * dstCap;
        b.Draw(t, new Rectangle(r.X, r.Y, dstCap, r.Height), new Rectangle(0, 0, srcCap, t.Height), tint);
        if (midDst > 0) b.Draw(t, new Rectangle(r.X + dstCap, r.Y, midDst, r.Height), new Rectangle(srcCap, 0, midSrc, t.Height), tint);
        b.Draw(t, new Rectangle(r.Right - dstCap, r.Y, dstCap, r.Height), new Rectangle(t.Width - srcCap, 0, srcCap, t.Height), tint);
    }

    /// <summary>
    /// THE close button — one icon (icon_close, a gold × in a round medallion) for every panel, strip
    /// and overlay that can be shut. Draws it fitted in <paramref name="r"/>, brighter under the mouse,
    /// and returns true on the click. Playtest 2026-08-26: "replace the drawn × with a proper icon and
    /// use it everywhere." Falls back to the old dark square with a × if the art is missing.
    /// </summary>
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
            TextCenterBig(b, "×", r.Center.X, r.Y + r.Height / 2 - 12, hot ? Color.White : Vellum, 22);
        }
        return clicked && hot;
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

    /// <summary>
    /// A gear class's icon in a box — <c>icon_class_warden</c> and its four siblings — or a diamond
    /// in the class colour (or <paramref name="fallback"/>) while that art has not shipped.
    /// </summary>
    /// <remarks>
    /// The same shape as the STATS screen's stat rows: try the key, draw the flat shape when it is
    /// absent. The five keys are built from <see cref="ResonanceHunter.Core.Economy.ItemClasses.IconKey"/>,
    /// so tools/check_asset_keys.py cannot see them as literals; it checks the family by name instead.
    /// </remarks>
    public void ClassIcon(SpriteBatch b, ResonanceHunter.Core.Economy.ItemClass cls, Rectangle box, Color? fallback = null)
    {
        if (Icon(b, ResonanceHunter.Core.Economy.ItemClasses.IconKey(cls), box, Color.White)) return;
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
        const int width = 430, pad = 14, lineH = 22;
        var lines = WrapBig(text, width - pad * 2, UiTypography.Secondary);
        if (lines.Count == 0) return;
        var h = pad * 2 + lines.Count * lineH;
        var x = anchor.X + 26;
        if (x + width > 1912) x = anchor.X - width - 12;
        var y = anchor.Y + 30;
        if (y + h > 1072) y = anchor.Y - h - 14;
        Fill(b, new Rectangle(x + 4, y + 5, width, h), new Color(0, 0, 0) * 0.45f);   // soft drop shadow
        Fill(b, new Rectangle(x, y, width, h), new Color(0x14, 0x0E, 0x20));
        Fill(b, new Rectangle(x, y, width, 2), new Color(0xC8, 0x9A, 0x3C));
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
        var win = new Rectangle(r.X + Math.Max(2, r.Width * 7 / 100), r.Y + Math.Max(2, r.Height * 30 / 100),
                                r.Width - Math.Max(4, r.Width * 14 / 100), Math.Max(1, r.Height * 42 / 100));
        Fill(b, win, new Color(0x12, 0x0C, 0x10));
        b.Draw(frame, r, Color.White);   // ornate frame + its (opaque) dark window
        if (pct > 0f && Assets.Get($"ui_bar_{type}_fill") is { } fill && fill.Width > 0)
        {
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
