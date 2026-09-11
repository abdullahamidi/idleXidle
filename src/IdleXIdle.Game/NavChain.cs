using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game;

/// <summary>Which art a piece of a rail tile's chain is drawn with.</summary>
public enum NavChainSprite
{
    /// <summary>A link seen face-on — an oval ring with its hole showing (<c>ui_chain_link_face</c>).</summary>
    FaceLink,

    /// <summary>A link seen edge-on — the slim one threaded through its neighbours' holes (<c>ui_chain_link_edge</c>).</summary>
    EdgeLink,

    /// <summary>The small padlock hung where the two runs cross (<c>ui_chain_padlock</c>).</summary>
    Clasp,
}

/// <summary>One drawn piece of a locked rail tile's chain.</summary>
/// <param name="Sprite">Which art it is drawn with.</param>
/// <param name="Run">The run it belongs to (0 is the strong run, 1 the other); -1 for the clasp.</param>
/// <param name="Side">
/// Which way it recoils when the chain breaks: -1 back along its run toward the edge the run enters by,
/// +1 on toward the edge it leaves by; 0 for the link that snaps and for the clasp, which do neither.
/// </param>
/// <param name="Rest">Where it hangs while the tile is bound, in canvas space.</param>
/// <param name="Centre">Where it is on this frame.</param>
/// <param name="Rotation">Its long axis, in radians from +X (screen space, so positive turns clockwise).</param>
/// <param name="Length">Its drawn size along its long axis in canvas px — for the clasp, its width.</param>
/// <param name="Thickness">Its drawn size across that axis — for the clasp, its height.</param>
/// <param name="Alpha">Its opacity on this frame, 0..1.</param>
public readonly record struct NavChainPiece(
    NavChainSprite Sprite, int Run, int Side, Vector2 Rest, Vector2 Centre,
    float Rotation, float Length, float Thickness, float Alpha)
{
    /// <summary>Half the axis-aligned box the rotated piece covers — what the tile's edges are measured against.</summary>
    public Vector2 HalfExtent => NavChain.HalfExtent(Length, Thickness, Rotation);
}

/// <summary>One run of chain at rest: its first link's centre to its last's.</summary>
/// <param name="Start">The link nearest the edge the run enters by.</param>
/// <param name="End">The link nearest the edge it leaves by.</param>
public readonly record struct NavChainRun(Vector2 Start, Vector2 End)
{
    /// <summary>The unit vector from entry to exit — the line every link of the run lies on.</summary>
    public Vector2 Direction => Vector2.Normalize(End - Start);

    /// <summary>Its angle from the horizontal in degrees, screen space: positive falls to the right.</summary>
    public float SlopeDegrees => MathHelper.ToDegrees(MathF.Atan2(End.Y - Start.Y, End.X - Start.X));
}

/// <summary>
/// THE CHAINS ON A LOCKED RAIL TILE: two diagonal runs of forged links binding the button shut, and the
/// one-shot that springs them apart when the screen opens. Pure geometry, so the Game tests can pin what
/// a screenshot can only suggest; <see cref="Draw"/> is the thin half that puts it in the host's batch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two designs were rejected before this one.</b> A RING around the icon (the sworn Vow's bind strip)
/// read as a decoration on the icon rather than a restraint on the tile. Then ONE STRAIGHT RUN across the
/// tile at the icon's height with a padlock in the middle — a belt, and its break slid two halves
/// sideways. The playtest's words for what it should be: <i>this rectangular button has been physically
/// chained shut</i> (FTUE playtest correction pass, item 4).
/// </para>
/// <para>
/// <b>So two runs, on two different diagonals, the way chains cross a door.</b> The strong run falls
/// from the top-left toward the lower right; the other climbs from the left edge to the right one at a
/// shallower slope, and they cross left of and above the tile's centre. Asymmetric on purpose — a
/// mirrored X is a logo, not a restraint — and the two runs start on different links and lean their
/// links differently, so neither reads as a copy of the other.
/// </para>
/// <para>
/// <b>Built, never stretched.</b> Every run is laid link by link, face-on rings alternating with edge-on
/// links, tight enough that each ring nearly meets the next and the edge-on link between them sits in
/// both their holes. Each piece is drawn at its art's own aspect, and a run is as long as the tile
/// allows at the density's link size — so it is a chain at 100, 125 and 150 alike.
/// </para>
/// <para>
/// <b>The break follows the chain.</b> One link in each run flashes and bursts, and the two halves of
/// every run spring back along their OWN run toward the edge they came in or go out by — fast first and
/// settling, a released spring rather than a drift — while the padlock drops out from under the
/// crossing. A piece whose centre has left the tile is not drawn, and the host clips a break to its
/// tile, so nothing lands on the neighbouring tile or the page. Reduced Motion moves nothing: the
/// snapped links are simply gone, and what is left fades.
/// </para>
/// </remarks>
public static class NavChain
{
    /// <summary>How many runs bind a tile.</summary>
    public const int RunCount = 2;

    /// <summary>Thickness over length of the face-on link art (<c>ui_chain_link_face</c>: 48 x 27 inside its border).</summary>
    public const float FaceAspect = 0.5625f;

    /// <summary>Thickness over length of the edge-on link art (<c>ui_chain_link_edge</c>: 48 x 12).</summary>
    public const float EdgeAspect = 0.25f;

    /// <summary>Height over width of the padlock art (<c>ui_chain_padlock</c>: 29 x 40).</summary>
    public const float ClaspAspect = 1.38f;

    /// <summary>
    /// A link's drawn length at this UI density — 22 px at 100 %, grown at the SPACING rate.
    /// </summary>
    /// <remarks>
    /// The tile does not grow with the profile (it is 1080/11 at every one), so links grown at the full
    /// control rate would make the chain a third of the tile thick at 150 %, over a label that has grown too.
    /// </remarks>
    public static int LinkLength => UiMetrics.Space(BaseLinkPx);

    private const int BaseLinkPx = 22;   // ui-size-ok: a chain link's drawn length in canvas px at 100 % (art, not type)
    private const int MinLinkPx = 8;     // ui-size-ok: the floor under which a link stops reading as one (art, not type)

    /// <summary>
    /// Link centre to link centre, as a share of a link's length. Tight on purpose: at 0.74 the same art
    /// read as rings strung on a wire; here each ring nearly meets the next and the edge-on link between
    /// them is mostly inside their holes, which is what makes it read as links that hold each other.
    /// </summary>
    private const float Pitch = 0.60f;

    /// <summary>Where the runs cross, as a share of the tile: left of centre and a little above it.</summary>
    private static readonly Vector2 Crossing = new(0.42f, 0.44f);

    /// <summary>
    /// THE STRONG RUN falls to the right at 30°, from the top-left corner toward the bottom edge's right
    /// end. A face-on link sits on the crossing (the padlock hangs from it), and it snaps at the face-on
    /// link two along — so what bursts is a whole ring, not a sliver of an edge-on one.
    /// </summary>
    private static readonly RunShape Strong = new(30f, 0f, FaceFirst: true, SnapAt: 2, 2.5f, 1.9f, 0f);

    /// <summary>
    /// THE OTHER RUN climbs at 19°, from the left edge low down to the right edge up high. Its links
    /// straddle the crossing, it starts on an edge-on link, and it snaps at a face-on link on the other
    /// side of the crossing from the strong run's snap.
    /// </summary>
    private static readonly RunShape Other = new(-19f, 0.5f, FaceFirst: false, SnapAt: -3, 3f, 2.7f, 0.8f);

    // ── The break's timeline, as shares of the break (0 bound .. 1 gone) ───────────────────────────
    /// <summary>The snapped link flashes, bursts, and is gone by here.</summary>
    private const float SnapPop = 0.14f;
    /// <summary>
    /// How far a half travels along its run over the whole break, in tile widths: enough to carry every
    /// piece off the tile and no more, so the halves are still leaving at the midpoint and the last of
    /// them is on its way out at nine tenths. (At 0.8 the tile stood empty from half-way.)
    /// </summary>
    private const float RecoilReach = 0.62f;
    /// <summary>How far each half's links swing as they go, in degrees.</summary>
    private const float WhipDegrees = 9f;
    /// <summary>Whatever is still on the tile starts to fade here.</summary>
    private const float FadeStart = 0.6f;
    /// <summary>The padlock lets go just after the snap...</summary>
    private const float ClaspLetGo = 0.06f;
    /// <summary>...and falls this many tile heights over the rest of the break...</summary>
    private const float ClaspDrop = 0.85f;
    /// <summary>...tipping this far as it goes, in degrees.</summary>
    private const float ClaspTilt = 20f;
    /// <summary>The padlock's width, as a share of a link's length.</summary>
    private const float ClaspWidth = 0.72f;
    /// <summary>How far below the crossing the padlock's centre hangs, in padlock heights — its shackle reaches up round the links.</summary>
    private const float ClaspHang = 0.36f;

    /// <summary>COLD AND UNDER-LIT: the restraint is why the tile is out of reach, not a prize on it.</summary>
    private static readonly Color LinkInk = new Color(0xC8, 0xCE, 0xDC) * 0.86f;
    private static readonly Color ClaspInk = new Color(0xC4, 0xC4, 0xCC) * 0.92f;
    /// <summary>The snapping link flashes pale and hot for its seventh of the break — the moment the eye is sent to.</summary>
    private static readonly Color SnapInk = new(0xF6, 0xEA, 0xD0);

    /// <summary>One run's shape: its slope, where its links fall, which link comes first, where it snaps and how its links lean.</summary>
    private readonly record struct RunShape(
        float SlopeDegrees, float Phase, bool FaceFirst, int SnapAt,
        float JitterDegrees, float JitterRate, float JitterShift);

    /// <summary>
    /// One run at rest on a tile: the centre of its first link to the centre of its last.
    /// </summary>
    /// <param name="tile">The rail tile, canvas space.</param>
    /// <param name="linkPx">A link's drawn length (<see cref="LinkLength"/> at the current density).</param>
    /// <param name="run">0 for the strong run, 1 for the other.</param>
    public static NavChainRun Run(Rectangle tile, int linkPx, int run)
    {
        var shape = run == 0 ? Strong : Other;
        var len = Math.Max(MinLinkPx, linkPx);
        var cross = CrossingPoint(tile);
        var dir = Along(shape.SlopeDegrees);
        if (!Range(tile, cross, dir, len, shape, out var kMin, out var kMax)) return new NavChainRun(cross, cross + dir);
        var pitch = len * Pitch;
        return new NavChainRun(cross + (kMin + shape.Phase) * pitch * dir, cross + (kMax + shape.Phase) * pitch * dir);
    }

    /// <summary>
    /// Every piece of a tile's chain on this frame, in draw order (the under run first, each face-on
    /// ring after the edge-on links threaded through it, the padlock last). Pieces whose centre has left
    /// the tile are not returned.
    /// </summary>
    /// <param name="tile">The rail tile, canvas space.</param>
    /// <param name="linkPx">A link's drawn length (<see cref="LinkLength"/> at the current density).</param>
    /// <param name="progress">0 while bound, rising to 1 as the break plays; at 1 the tile is clear.</param>
    /// <param name="reduced">Reduced Motion: nothing moves, the snapped links are gone and the rest fades.</param>
    /// <param name="into">Cleared by the caller; pieces are appended. A reused list keeps a frame allocation-free.</param>
    public static void Compose(Rectangle tile, int linkPx, float progress, bool reduced, List<NavChainPiece> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        if (progress >= 1f || tile.Width <= 0 || tile.Height <= 0) return;
        var p = Math.Max(0f, progress);
        var broken = p > 0f;
        var len = Math.Max(MinLinkPx, linkPx);
        var pitch = len * Pitch;
        var cross = CrossingPoint(tile);
        var reach = RecoilReach * tile.Width;
        // The halves leave from the very first frame, so that frame already shows a gap, not a flinch.
        var travel = reduced ? 0f : EaseOut(p) * reach;
        var fade = reduced ? 1f - UiMotion.Smooth(p) : 1f - UiMotion.Smooth((p - FadeStart) / (1f - FadeStart));
        if (fade <= 0.01f) return;

        // The other run first: the strong one lies over it where they cross.
        for (var run = RunCount - 1; run >= 0; run--)
        {
            var shape = run == 0 ? Strong : Other;
            var dir = Along(shape.SlopeDegrees);
            if (!Range(tile, cross, dir, len, shape, out var kMin, out var kMax)) continue;
            var snap = Math.Clamp(shape.SnapAt, kMin + 1, kMax - 1);
            var slope = MathHelper.ToRadians(shape.SlopeDegrees);
            // Two passes, so every face-on ring lies over the ends of the edge-on links threaded through it.
            for (var pass = 0; pass < 2; pass++)
                for (var k = kMin; k <= kMax; k++)
                {
                    var face = ((k & 1) == 0) == shape.FaceFirst;
                    if (face != (pass == 1)) continue;
                    var sprite = face ? NavChainSprite.FaceLink : NavChainSprite.EdgeLink;
                    var thick = len * (face ? FaceAspect : EdgeAspect);
                    var rest = cross + (k + shape.Phase) * pitch * dir;
                    var lean = slope + MathHelper.ToRadians(shape.JitterDegrees * MathF.Sin(shape.JitterRate * k + shape.JitterShift));
                    var side = Math.Sign(k - snap);
                    if (side == 0)
                    {
                        if (!broken) { Add(into, tile, sprite, run, 0, rest, rest, lean, len, thick, fade); continue; }
                        // THE LINK THAT SNAPS flashes, swells and is gone inside the first seventh of the
                        // break. Under Reduced Motion it is simply absent: the gap is the event.
                        if (reduced || p >= SnapPop) continue;
                        var burst = p / SnapPop;
                        Add(into, tile, sprite, run, 0, rest, rest, lean, len * (1f + 0.5f * burst), thick * (1f + 0.5f * burst), fade * (1f - burst));
                        continue;
                    }
                    // BACK ALONG ITS OWN RUN: the half before the snap toward the edge the run enters by,
                    // the half after it toward the edge it leaves by. Never sideways, never down.
                    var centre = rest + side * travel * dir;
                    var whip = side * MathHelper.ToRadians(WhipDegrees) * (travel / reach);
                    Add(into, tile, sprite, run, side, rest, centre, lean + whip, len, thick, fade);
                }
        }

        // THE PADLOCK hangs from the crossing, its shackle up round the links, and drops out from under
        // it once the chain has snapped. Small on purpose: the chains are the restraint, it only holds them.
        var cw = len * ClaspWidth;
        var ch = cw * ClaspAspect;
        var claspRest = cross + new Vector2(0f, ch * ClaspHang);
        var let = reduced || !broken ? 0f : Math.Clamp((p - ClaspLetGo) / (1f - ClaspLetGo), 0f, 1f);
        var claspAt = claspRest + new Vector2(0f, ClaspDrop * tile.Height * let * let);
        Add(into, tile, NavChainSprite.Clasp, -1, 0, claspRest, claspAt, MathHelper.ToRadians(ClaspTilt) * let, cw, ch, fade);
    }

    /// <summary>
    /// Draw a tile's chain into the host's batch — the house batch as it stands (smooth sampling,
    /// premultiplied art), each piece scaled whole and rotated about its own centre.
    /// </summary>
    /// <param name="b">The chrome batch, already begun (clipped to the tile while a break plays).</param>
    /// <param name="ui">The kit, for its asset library.</param>
    /// <param name="tile">The rail tile, canvas space.</param>
    /// <param name="linkPx">A link's drawn length (<see cref="LinkLength"/>).</param>
    /// <param name="progress">0 while bound, 1 when the break has finished.</param>
    /// <param name="reduced">Reduced Motion.</param>
    public static void Draw(SpriteBatch b, UiKit ui, Rectangle tile, int linkPx, float progress, bool reduced)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(ui);
        Scratch.Clear();
        Compose(tile, linkPx, progress, reduced, Scratch);
        if (Scratch.Count == 0) return;
        var face = ui.Assets.Get("ui_chain_link_face");
        var edge = ui.Assets.Get("ui_chain_link_edge");
        var clasp = ui.Assets.Get("ui_chain_padlock");
        foreach (var piece in Scratch)
        {
            var tex = piece.Sprite switch
            {
                NavChainSprite.FaceLink => face,
                NavChainSprite.EdgeLink => edge,
                _ => clasp,
            };
            if (tex is null) continue;   // fails soft, like every keyed draw; tools/check_asset_keys.py guards the keys
            var snapping = progress > 0f && piece.Side == 0 && piece.Run >= 0;
            var ink = (snapping ? SnapInk : piece.Sprite == NavChainSprite.Clasp ? ClaspInk : LinkInk) * piece.Alpha;
            // Scaled WHOLE from its length (the art carries a 1 px clear border), so it keeps its aspect.
            var scale = piece.Length / Math.Max(1f, tex.Width - 2f);
            b.Draw(tex, piece.Centre, null, ink, piece.Rotation,
                   new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Half the axis-aligned box a <paramref name="length"/> x <paramref name="thickness"/> piece covers, turned by <paramref name="rotation"/>.</summary>
    public static Vector2 HalfExtent(float length, float thickness, float rotation)
    {
        var c = MathF.Abs(MathF.Cos(rotation));
        var s = MathF.Abs(MathF.Sin(rotation));
        return new Vector2((length * c + thickness * s) * 0.5f, (length * s + thickness * c) * 0.5f);
    }

    /// <summary>Is a point on the tile, edges included — the one test both culling and the Game tests use.</summary>
    public static bool OnTile(Rectangle tile, Vector2 p)
        => p.X >= tile.Left && p.X <= tile.Right && p.Y >= tile.Top && p.Y <= tile.Bottom;

    private static readonly List<NavChainPiece> Scratch = new(64);

    private static Vector2 CrossingPoint(Rectangle tile)
        => new(tile.X + tile.Width * Crossing.X, tile.Y + tile.Height * Crossing.Y);

    private static Vector2 Along(float degrees)
    {
        var r = MathHelper.ToRadians(degrees);
        return new Vector2(MathF.Cos(r), MathF.Sin(r));
    }

    /// <summary>The back half of the house smoothstep: full speed at 0, settled at 1.</summary>
    private static float EaseOut(float t) => 2f * (UiMotion.Smooth(0.5f + 0.5f * Math.Clamp(t, 0f, 1f)) - 0.5f);

    /// <summary>
    /// The links a run can hold: every step along its line at which a face-on link (the widest piece,
    /// leaning as far as its jitter lets it) lies wholly on the tile with a pixel to spare.
    /// </summary>
    private static bool Range(Rectangle tile, Vector2 cross, Vector2 dir, int len, RunShape shape, out int kMin, out int kMax)
    {
        kMin = 0;
        kMax = -1;
        var a = MathHelper.ToRadians(shape.SlopeDegrees);
        var j = MathHelper.ToRadians(shape.JitterDegrees);
        var h1 = HalfExtent(len, len * FaceAspect, a - j);
        var h2 = HalfExtent(len, len * FaceAspect, a + j);
        var hx = MathF.Max(h1.X, h2.X) + 1f;
        var hy = MathF.Max(h1.Y, h2.Y) + 1f;
        float lo = float.NegativeInfinity, hi = float.PositiveInfinity;
        if (!Clip(cross.X, dir.X, tile.Left + hx, tile.Right - hx, ref lo, ref hi)) return false;
        if (!Clip(cross.Y, dir.Y, tile.Top + hy, tile.Bottom - hy, ref lo, ref hi)) return false;
        if (lo > hi) return false;
        var pitch = len * Pitch;
        kMin = (int)MathF.Ceiling(lo / pitch - shape.Phase);
        kMax = (int)MathF.Floor(hi / pitch - shape.Phase);
        return kMax - kMin >= 2;   // a run needs a snap with a link either side of it
    }

    private static bool Clip(float origin, float d, float min, float max, ref float lo, ref float hi)
    {
        if (min > max) return false;
        if (MathF.Abs(d) < 1e-6f) return origin >= min && origin <= max;
        var t0 = (min - origin) / d;
        var t1 = (max - origin) / d;
        if (t0 > t1) (t0, t1) = (t1, t0);
        lo = MathF.Max(lo, t0);
        hi = MathF.Min(hi, t1);
        return true;
    }

    /// <summary>Append a piece unless its centre has left the tile, or it has faded to nothing.</summary>
    private static void Add(List<NavChainPiece> into, Rectangle tile, NavChainSprite sprite, int run, int side,
                            Vector2 rest, Vector2 centre, float rotation, float length, float thickness, float alpha)
    {
        if (!OnTile(tile, centre) || alpha <= 0.01f) return;
        into.Add(new NavChainPiece(sprite, run, side, rest, centre, rotation, length, thickness, alpha));
    }
}
