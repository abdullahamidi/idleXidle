using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>
/// THE FOCUS LIGHT: the page darkened, and the one thing a card is about left lit — cut to its own
/// shape, with a soft edge and a faint ivory rim, rather than framed by a box.
/// </summary>
/// <remarks>
/// <para>
/// No shader. The scrim is a half-resolution texture built once per distinct shape list and drawn over
/// the canvas every frame at the orchestrator's weight, so the three surfaces that light things — the
/// coach, a tour, the authored opening — pay one textured quad a frame and a rebuild only when the
/// shapes change (an animating figure changes every frame; that is expected and costs a handful of
/// quads). The build is two passes: the MASK, every shape drawn white and additive onto a cleared
/// target, with eight offset copies giving a figure's outline its feather; then the SCRIM, cleared to
/// the ink at full alpha, the mask added round itself at eight offsets in ivory for the rim, and
/// finally the mask ERASED from it — <c>dest × (1 − mask)</c> for colour and alpha alike — which cuts
/// the hole and keeps the ivory only outside it. Compositing a premultiplied scrim at weight
/// <c>w</c> is exactly what the old flat fill did (<c>ink × w</c> over the page) everywhere the mask
/// is zero, so the darkness the player learned is unchanged; only the hole's shape is new.
/// </para>
/// <para>
/// Nothing here hit-tests. The rectangles the click and the card use are the step's own
/// (<c>Game1.TourSpotlights</c>, <c>ClickableOf</c>, <c>TourCardRect</c>); this is handed shapes and
/// paints them. Building switches render targets, which is why the canvas is created with
/// <see cref="RenderTargetUsage.PreserveContents"/> — a discard-contents target would be wiped the
/// moment the host bound it again — and why the host closes its batch before <see cref="Build"/> and
/// reopens it after.
/// </para>
/// </remarks>
public sealed class FocusRenderer
{
    /// <summary>The scrim's ink — the near-black every focus surface darkened the page with before this.</summary>
    public static readonly Color Ink = new(0x05, 0x03, 0x0A);

    /// <summary>The rim's colour: warm ivory, the house light on a dark ground.</summary>
    private static readonly Color Ivory = new(0xF2, 0xE9, 0xD6);

    /// <summary>The scrim is built at half the canvas; LinearClamp on the way back up is part of the softness.</summary>
    private const int Half = 2;
    private const int Width = 1920 / Half;
    private const int Height = 1080 / Half;
    private static readonly Rectangle Canvas = new(0, 0, 1920, 1080);
    private static readonly Matrix HalfScale = Matrix.CreateScale(1f / Half);

    // ── THE PLATE: a rounded rectangle with a soft edge, nine-sliced so its rails stretch. ──────────
    private const int Radius = 16;         // canvas px, at the opaque edge
    private const int Feather = 12;        // canvas px of falloff outside the rectangle
    private const int Mid = 8;             // the stretchable rail, in the texture
    private const int Corner = Radius + Feather;
    private const float RimLine = 1.5f;    // the outline's width, texture px
    private const float RimAlpha = 0.35f;

    // ── THE SILHOUETTE'S FEATHER: eight copies of the mask round itself. ───────────────────────────
    private const int SilhouetteFeather = 7;     // canvas px
    private const float FeatherWeight = 0.35f;

    // ── THE HALO: the mask, ivory, round itself on the scrim. ─────────────────────────────────────
    private const int HaloReach = 10;            // canvas px
    private const float HaloWeight = 0.14f;      // per copy; the eight overlap near the edge

    /// <summary>The soft disc an ellipse is drawn with, and how much of it is falloff.</summary>
    private const int DiscSize = 128;
    private const int DiscFeather = 16;

    private static readonly Point[] Ring8 =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, 1), new(1, -1), new(-1, -1),
    };

    /// <summary>dest × (1 − src alpha), colour and alpha alike: the mask cuts its hole out of the scrim.</summary>
    private static readonly BlendState Erase = new()
    {
        Name = "FocusRenderer.Erase",
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.InverseSourceAlpha,
        ColorBlendFunction = BlendFunction.Add,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private readonly GraphicsDevice _device;
    private readonly Texture2D _plate;
    private readonly Texture2D _ring;
    private readonly Texture2D _disc;
    private RenderTarget2D? _mask;
    private RenderTarget2D? _scrim;

    /// <summary>The shapes the scrim was last built from — value-compared, so an unchanged list costs no rebuild.</summary>
    private readonly List<FocusShape> _last = new();

    public FocusRenderer(GraphicsDevice device)
    {
        _device = device;
        _plate = UiKit.MakeSoftRounded(device, Radius, Feather, Mid);
        _ring = UiKit.MakeSoftRounded(device, Radius, Feather, Mid, RimLine);
        _disc = UiKit.MakeSoftDisc(device, DiscSize, DiscFeather);
    }

    /// <summary>Does the scrim need rebuilding for these shapes — never built, or a different list?</summary>
    public bool Stale(IReadOnlyList<FocusShape> shapes)
    {
        if (_scrim is null || _last.Count != shapes.Count) return true;
        for (var i = 0; i < shapes.Count; i++)
            if (!_last[i].Equals(shapes[i])) return true;
        return false;
    }

    /// <summary>
    /// Build the mask and the scrim for these shapes. The host's batch must be closed; on return the
    /// scrim target is still bound, and the host rebinds its canvas and reopens its batch.
    /// </summary>
    public void Build(SpriteBatch b, IReadOnlyList<FocusShape> shapes)
    {
        _mask ??= new RenderTarget2D(_device, Width, Height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _scrim ??= new RenderTarget2D(_device, Width, Height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        // THE MASK: white where the light falls. Additive, so overlapping shapes and the feather copies
        // sum and saturate rather than blend back toward transparent.
        _device.SetRenderTarget(_mask);
        _device.Clear(Color.Transparent);
        b.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, null, null, HalfScale);
        for (var i = 0; i < shapes.Count; i++) White(b, shapes[i]);
        b.End();

        // THE SCRIM: the ink everywhere, the rim added round the light, then the light cut out.
        _device.SetRenderTarget(_scrim);
        _device.Clear(Ink);
        b.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp);
        var halo = Ivory * HaloWeight;
        for (var i = 0; i < Ring8.Length; i++)
            b.Draw(_mask, new Vector2(Ring8[i].X * HaloReach / (float)Half, Ring8[i].Y * HaloReach / (float)Half), halo);
        b.End();
        b.Begin(SpriteSortMode.Deferred, Erase, SamplerState.LinearClamp);
        b.Draw(_mask, Vector2.Zero, Color.White);
        b.End();

        _last.Clear();
        for (var i = 0; i < shapes.Count; i++) _last.Add(shapes[i]);
    }

    /// <summary>
    /// Composite the scrim over the canvas at <paramref name="weight"/> — the orchestrator's own alpha,
    /// zero while a blaze has faded — and rim every rounded rectangle so the lit control keeps its
    /// definition after the scrim has gone.
    /// </summary>
    public void Draw(SpriteBatch b, IReadOnlyList<FocusShape> shapes, float weight)
    {
        if (weight > 0f && _scrim is not null) b.Draw(_scrim, Canvas, Color.White * weight);
        var rim = Ivory * RimAlpha;
        for (var i = 0; i < shapes.Count; i++)
        {
            if (shapes[i].Kind != FocusKind.RoundedRect) continue;
            var dest = shapes[i].Rect;
            dest.Inflate(Feather, Feather);
            NineSlice(b, _ring, dest, rim);
        }
    }

    /// <summary>One shape, white, into the mask.</summary>
    private void White(SpriteBatch b, in FocusShape shape)
    {
        switch (shape.Kind)
        {
            case FocusKind.Silhouette when shape.Mask is { } mask:
                b.Draw(mask, shape.Rect, shape.Src, Color.White, 0f, Vector2.Zero, shape.Effects, 0f);
                var soft = Color.White * FeatherWeight;
                for (var i = 0; i < Ring8.Length; i++)
                {
                    var r = shape.Rect;
                    // The diagonals sit on the same circle as the axes, so the feather is round, not square.
                    var d = Ring8[i].X != 0 && Ring8[i].Y != 0 ? (int)MathF.Round(SilhouetteFeather * 0.7071f) : SilhouetteFeather;
                    r.Offset(Ring8[i].X * d, Ring8[i].Y * d);
                    b.Draw(mask, r, shape.Src, soft, 0f, Vector2.Zero, shape.Effects, 0f);
                }
                break;
            case FocusKind.Ellipse:
                var e = shape.Rect;
                e.Inflate(Feather, Feather);
                b.Draw(_disc, e, Color.White);
                break;
            default:
                var p = shape.Rect;
                p.Inflate(Feather, Feather);
                NineSlice(b, _plate, p, Color.White);
                break;
        }
    }

    /// <summary>
    /// Nine-slice a square texture whose corners are <see cref="Corner"/> pixels into <paramref name="dest"/>.
    /// A destination too small for two full corners shrinks them to fit rather than overlapping them.
    /// </summary>
    private static void NineSlice(SpriteBatch b, Texture2D tex, Rectangle dest, Color tint)
    {
        var s = tex.Width;
        var c = Math.Min(Corner, Math.Min(dest.Width, dest.Height) / 2);
        if (c <= 0) return;
        var midW = dest.Width - 2 * c;
        var midH = dest.Height - 2 * c;
        var sMid = s - 2 * Corner;
        int x0 = dest.X, x1 = dest.X + c, x2 = dest.Right - c;
        int y0 = dest.Y, y1 = dest.Y + c, y2 = dest.Bottom - c;
        int sx1 = Corner, sx2 = s - Corner;

        b.Draw(tex, new Rectangle(x0, y0, c, c), new Rectangle(0, 0, Corner, Corner), tint);
        if (midW > 0) b.Draw(tex, new Rectangle(x1, y0, midW, c), new Rectangle(sx1, 0, sMid, Corner), tint);
        b.Draw(tex, new Rectangle(x2, y0, c, c), new Rectangle(sx2, 0, Corner, Corner), tint);
        if (midH > 0)
        {
            b.Draw(tex, new Rectangle(x0, y1, c, midH), new Rectangle(0, sx1, Corner, sMid), tint);
            if (midW > 0) b.Draw(tex, new Rectangle(x1, y1, midW, midH), new Rectangle(sx1, sx1, sMid, sMid), tint);
            b.Draw(tex, new Rectangle(x2, y1, c, midH), new Rectangle(sx2, sx1, Corner, sMid), tint);
        }
        b.Draw(tex, new Rectangle(x0, y2, c, c), new Rectangle(0, sx2, Corner, Corner), tint);
        if (midW > 0) b.Draw(tex, new Rectangle(x1, y2, midW, c), new Rectangle(sx1, sx2, sMid, Corner), tint);
        b.Draw(tex, new Rectangle(x2, y2, c, c), new Rectangle(sx2, sx2, Corner, Corner), tint);
    }
}
