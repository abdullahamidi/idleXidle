using System;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Vfx;

/// <summary>Which figure's visual bounds an effect is measured and placed against.</summary>
/// <remarks>
/// A subject is a FIGURE, not a rectangle: the champion, one creature in the row, or the row itself.
/// The screen publishes a <see cref="VisualBounds"/> for each one every frame (see
/// <see cref="VfxBoundsRegistry"/>), and the effect is resolved against that rather than against a
/// layout box. This is what makes an effect follow the champion's lunge without any spawn site
/// knowing the lunge exists.
/// </remarks>
public enum VfxSubjectKind
{
    /// <summary>The hunter.</summary>
    Champion,
    /// <summary>One creature of the wave, by slot. A boss is slot 0.</summary>
    Creature,
    /// <summary>The whole enemy row — the pack, not a body. Used by the ground ring.</summary>
    EnemyRow,
}

/// <summary>Where on the subject the effect's CONTENT lands. Four members, edge- or centre-matching.</summary>
public enum VfxAnchor
{
    /// <summary>Content centre on the subject's visual centre.</summary>
    Center,
    /// <summary>Content centre on the subject's visible crown — an overhead sigil.</summary>
    Head,
    /// <summary>Content BOTTOM on the subject's visible sole — it stands on the ground.</summary>
    /// <remarks>
    /// A FLAT RING LYING ON THE GROUND WOULD WANT A FIFTH ANCHOR — content CENTRE on the sole, so half
    /// the ellipse falls behind the feet — and it was built and then removed, because no art in this
    /// project is a flat ring. Every trap strip is a near-square burst (measured: the content fills
    /// 0.15 to 0.94 of a 512 frame at roughly 1:1), so centring one on the pack's soles put 220 px of
    /// glow below the floor and straight across the skill dock. Standing is the honest reading of the
    /// art that exists: a burst that rises from the ground the pack is on. When a flat ring is
    /// generated, the anchor comes back with its consumer and not before.
    /// </remarks>
    Standing,
}

/// <summary>Which dimension of the subject's visual bounds <c>RelativeScale</c> multiplies.</summary>
public enum VfxBasis
{
    /// <summary>The subject's visible height. Nearly clip-invariant, so this is the stable default.</summary>
    SubjectHeight,
    /// <summary>The subject's visible width. Only right when height is meaningless — a row is one creature tall.</summary>
    SubjectWidth,
}

/// <summary>Draw order. <see cref="GroundUnder"/> and <see cref="BehindSubject"/> run BEFORE the figures.</summary>
public enum VfxLayer
{
    /// <summary>On the floor, under everything — a ring the pack stands in.</summary>
    GroundUnder,
    /// <summary>Behind the figure, so the strip's opaque interior cannot haze the body it wraps.</summary>
    BehindSubject,
    /// <summary>Over the figure. Where almost everything lives.</summary>
    OnSubject,
    /// <summary>Over the figure and over every other effect — a sigil above the crown.</summary>
    Overhead,
}

/// <summary>Whether +OffsetX means screen-right or "toward the opponent".</summary>
public enum VfxFacing
{
    /// <summary>+OffsetX is screen-right, whoever the subject is.</summary>
    Fixed,
    /// <summary>+OffsetX is toward the subject's opponent — right for the champion, left for a creature.</summary>
    Forward,
}

/// <summary>Detached resolves once, on its first drawn frame; Pinned re-resolves every frame.</summary>
public enum VfxFollow
{
    /// <summary>A spark stays where the blow landed, even as the body moves off it.</summary>
    Detached,
    /// <summary>The effect tracks its subject — a barrier is a property of the body, not of a place.</summary>
    Pinned,
}

/// <summary>OneShot plays once, fades, dies. Held loops and lives only while re-asserted this frame.</summary>
public enum VfxLifetime
{
    OneShot,
    Held,
}

/// <summary>Whether the effect crosses to a second subject across its life.</summary>
public enum VfxTravel
{
    None,
    /// <summary>Eased from the caster toward the target across the effect's own life.</summary>
    ToTarget,
}

/// <summary>A subject INSTANCE: the kind plus, for a creature, which slot.</summary>
public readonly record struct VfxSubject(VfxSubjectKind Kind, int Slot)
{
    public static readonly VfxSubject Champion = new(VfxSubjectKind.Champion, -1);
    public static readonly VfxSubject EnemyRow = new(VfxSubjectKind.EnemyRow, -1);
    public static VfxSubject Creature(int slot) => new(VfxSubjectKind.Creature, slot);

    public override string ToString() => Kind == VfxSubjectKind.Creature ? $"Creature/{Slot}" : Kind.ToString();
}

/// <summary>A figure's VISIBLE opaque rectangle this frame, plus which way it faces (+1 right, -1 left).</summary>
/// <remarks>
/// <para>
/// The rectangle is where the ART is, not where the layout box is. <c>UiKit.AnimSprite</c> crops a
/// strip's transparent margin and fills the box HEIGHT with what is left, so a 430-tall champion box
/// holds a 412-px figure whose width runs from 162 px (THE OATHBOUND) to 373 px (QUIVER). Placing an
/// effect against the box instead of against the figure is how a barrier ended up a third the size of
/// the hunter it was supposed to enclose.
/// </para>
/// <para>
/// <see cref="Facing"/> is NOT a draw mirror — nothing in the arena is ever flipped
/// (<c>HuntScreen.ArtFacesLeft</c> is const false). Its single job is the sign of an offset under
/// <see cref="VfxFacing.Forward"/>.
/// </para>
/// </remarks>
public readonly record struct VisualBounds(Rectangle Rect, int Facing)
{
    /// <summary>The figure's visible middle. The one reading anything outside the resolver wants.</summary>
    /// <remarks>
    /// CenterY, Top and Bottom were written beside this and nothing read them — the resolver takes its
    /// anchors off <see cref="Rect"/> directly. A convenience nobody calls is the dial-with-no-consumer
    /// this project keeps finding, so they are gone; add one back with the site that needs it.
    /// </remarks>
    public int CenterX => Rect.Center.X;
}

/// <summary>A strip frame's transparent margins as fractions of the FRAME, union across all frames.</summary>
/// <remarks>
/// Measured by <c>UiKit.Content(key)</c> from the same cached alpha scans the draw path already uses.
/// It matters because <c>VfxPlayer</c> draws the WHOLE padded frame. The case that named the type:
/// <c>fx_shield</c> was a hemisphere filling 0.762 x 0.492 of its 512-px frame, so a "270 px" shield
/// was a 132-px picture, and no multiplier could have fixed it — asking for the barrier's real size
/// meant a 963-px frame, which is LAW 16's forbidden magnification. The strip was regenerated
/// (2026-09-04) as a closed ring filling its whole frame and the same authored 1.15 now draws 474 px
/// at 0.93 of native. Relative scale measures the CONTENT; the frame size is derived from it.
/// </remarks>
public readonly record struct ContentBox(float Left, float Top, float Right, float Bottom)
{
    /// <summary>A frame with no measurable margin — the honest fallback when art is missing.</summary>
    public static readonly ContentBox Full = new(0f, 0f, 0f, 0f);

    public float Width => Math.Max(0.01f, 1f - Left - Right);
    public float Height => Math.Max(0.01f, 1f - Top - Bottom);
    public float CenterX => Left + Width * 0.5f;
    public float CenterY => Top + Height * 0.5f;
}

/// <summary>
/// One effect's placement rule — authored data, five required members and the rest defaulted.
/// </summary>
/// <remarks>
/// <para>
/// This replaces eighteen ad-hoc spawn expressions of the shape <c>ChampBox.Center.Y + 40</c>,
/// <c>ChampBox.Bottom - 156</c>, <c>Clamp((tx - ChampBox.Right) / 104, 2, 5)</c> — the
/// <c>x += 17 / y -= 23 / scale = 0.42</c> pattern the brief's §62 names. Every number here is a
/// RATIO of the subject, so it is correct on ten silhouettes and at every UI SCALE at once.
/// </para>
/// <para>
/// <b>Tint is deliberately not a member.</b> It is per-cast — the Source glow of the skill that fired,
/// the shield's breathing steel, the field's brightness spike — so it stays a spawn argument. Authoring
/// it here would need a Source-by-profile matrix for no gain.
/// </para>
/// <para>
/// <b>Rotation is deliberately absent.</b> Nothing in the repo draws with an angle and the projectile's
/// flight is nearly horizontal (about 11°), so a Rotate member would be a dial with one marginal
/// consumer — this project's named failure mode. Recorded here so the omission is a decision.
/// </para>
/// </remarks>
/// <param name="Id">Stable id — printed by the debug view, named by tests, keys the held table.</param>
/// <param name="AssetKey">The LITERAL base key. The per-character rule expands it (see HuntScreen.FxFor).</param>
/// <param name="Subject">Whose bounds this is measured and placed against.</param>
/// <param name="Anchor">Where on the subject the content lands.</param>
/// <param name="RelativeScale">Multiple of the subject's visual basis dimension, measured on CONTENT.</param>
/// <param name="Layer">Draw order tier.</param>
/// <param name="Fps">Frames per second of the strip.</param>
/// <param name="Basis">Which dimension <paramref name="RelativeScale"/> multiplies.</param>
/// <param name="OffsetX">Subject WIDTHS. + is screen-right, or forward when Facing is Forward.</param>
/// <param name="OffsetY">Subject HEIGHTS. + is down.</param>
/// <param name="Facing">Whether OffsetX follows the subject's facing.</param>
/// <param name="Follow">Detached resolves once; Pinned tracks the subject every frame.</param>
/// <param name="Lifetime">OneShot fades and dies; Held loops while re-asserted.</param>
/// <param name="Travel">Whether the effect crosses to a second subject.</param>
/// <param name="DelaySeconds">A negative start — the effect exists but is not drawn until its clock crosses 0.</param>
/// <param name="Frames">
/// DECLARED, never inferred from the aspect. Every fx strip on disk today is 4096x512 and the old
/// player read the count out of that ratio, so a regenerated asset at another aspect would have broken
/// playback silently. Declaring it makes a wrong strip a loud mismatch instead.
/// </param>
public sealed record VfxProfile(
    string Id,
    string AssetKey,
    VfxSubjectKind Subject,
    VfxAnchor Anchor,
    float RelativeScale,
    VfxLayer Layer = VfxLayer.OnSubject,
    float Fps = 12f,
    VfxBasis Basis = VfxBasis.SubjectHeight,
    float OffsetX = 0f,
    float OffsetY = 0f,
    VfxFacing Facing = VfxFacing.Fixed,
    VfxFollow Follow = VfxFollow.Detached,
    VfxLifetime Lifetime = VfxLifetime.OneShot,
    VfxTravel Travel = VfxTravel.None,
    float DelaySeconds = 0f,
    int Frames = 8);

/// <summary>What the resolver produced. Pure data — testable without a GraphicsDevice.</summary>
/// <param name="Frame">Where the whole strip frame draws.</param>
/// <param name="Content">The visible part — what the player actually sees.</param>
/// <param name="Anchor">The resolved anchor point, before the offset.</param>
/// <param name="NativeRatio">Frame height over the strip's native frame height — the §73 / LAW 16 number.</param>
public readonly record struct VfxPlacement(Rectangle Frame, Rectangle Content, Point Anchor, float NativeRatio);

/// <summary>Published each frame by the screen so the player can resolve subjects to pixels.</summary>
public interface IVfxBoundsSource
{
    bool TryBounds(VfxSubject subject, out VisualBounds bounds);
}

/// <summary>
/// The §73 / LAW 16 budget: how far a strip may be scaled from the size it was authored at.
/// </summary>
/// <remarks>
/// <para>
/// Magnifying a 512-px frame to 963 px to make a barrier the right size IS <c>scale = 3.4</c> wearing
/// a new coat, and the brief forbids it in as many words. So the ratio is not advice — it is the
/// diagnosis, and the two ends of the band mean different things:
/// </para>
/// <list type="bullet">
/// <item>above <see cref="Max"/> the strip is being magnified: the asset is wrong and must be
/// regenerated. The renderer CLAMPS to <see cref="Max"/> rather than drawing the magnification, so the
/// effect is as large as the art can honestly go and the shortfall shows up as a flagged number
/// instead of as a blurry picture nobody measured.</item>
/// <item>below <see cref="Min"/> the strip is authored larger than it is ever drawn: wasteful, never
/// ugly. Listed, never enforced.</item>
/// </list>
/// </remarks>
public static class VfxBudget
{
    /// <summary>Above this a strip is being magnified. The renderer refuses to go past it.</summary>
    public const float Max = 1.25f;

    /// <summary>Below this a strip is authored bigger than it is drawn — waste, not a defect.</summary>
    public const float Min = 0.75f;

    public enum Verdict { Under, Ok, Over }

    public static Verdict Of(float nativeRatio)
        => nativeRatio > Max ? Verdict.Over : nativeRatio < Min ? Verdict.Under : Verdict.Ok;
}

/// <summary>
/// Turns a profile plus a subject's visual bounds into pixels. One pure static function, no globals.
/// </summary>
public static class VfxResolver
{
    /// <summary>
    /// Resolve <paramref name="p"/> against subject bounds <paramref name="s"/> and the strip's own
    /// content box <paramref name="c"/>.
    /// </summary>
    /// <param name="nativeFrameW">The strip's authored frame width in px.</param>
    /// <param name="nativeFrameH">The strip's authored frame height in px.</param>
    /// <param name="clampToBudget">
    /// When true (the renderer's own call), a frame that would exceed <see cref="VfxBudget.Max"/> is
    /// shrunk to exactly that ratio — LAW 16 enforced where it is visible. Tests pass false to see the
    /// size the design ASKED for, which is the number the asset order is written from.
    /// </param>
    public static VfxPlacement Resolve(VfxProfile p, VisualBounds s, ContentBox c,
                                       int nativeFrameW, int nativeFrameH, bool clampToBudget = false)
    {
        ArgumentNullException.ThrowIfNull(p);
        nativeFrameW = Math.Max(1, nativeFrameW);
        nativeFrameH = Math.Max(1, nativeFrameH);

        // 1-2. The visible size, in pixels. This is the number the design argues about.
        var basis = p.Basis == VfxBasis.SubjectHeight ? s.Rect.Height : s.Rect.Width;
        var contentDim = MathF.Max(1f, p.RelativeScale * basis);

        // 3-4. The frame that CONTAINS that much content, at the strip's own aspect. A padded strip
        //      needs a bigger frame to show the same picture — which is exactly what makes the padding
        //      visible as a number instead of as a mystery.
        var frameH = p.Basis == VfxBasis.SubjectHeight
            ? contentDim / c.Height
            : contentDim / c.Width * (nativeFrameH / (float)nativeFrameW);
        var ratio = frameH / nativeFrameH;
        if (clampToBudget && ratio > VfxBudget.Max)
        {
            frameH = VfxBudget.Max * nativeFrameH;
            ratio = VfxBudget.Max;
        }
        var frameW = frameH * nativeFrameW / nativeFrameH;

        // 5. The anchor: a semantic point on the FIGURE.
        var anchorY = p.Anchor switch
        {
            VfxAnchor.Head => s.Rect.Top,
            VfxAnchor.Standing => s.Rect.Bottom,
            _ => s.Rect.Center.Y,
        };
        var anchor = new Point(s.Rect.Center.X, anchorY);

        // 6-7. The offset, in the subject's own units, and the one anchor-specific term in the whole
        //      resolver: STANDING puts the content's BOTTOM on the sole, which is what the hand-tuned
        //      "- 156" at the heal's spawn site was reaching for.
        var sign = p.Facing == VfxFacing.Forward ? (s.Facing < 0 ? -1f : 1f) : 1f;
        var cx = anchor.X + sign * p.OffsetX * s.Rect.Width;
        var cy = anchor.Y + p.OffsetY * s.Rect.Height
                 + (p.Anchor == VfxAnchor.Standing ? -frameH * c.Height * 0.5f : 0f);

        // 8-10. Frame and content rectangles around that content centre.
        var frame = new Rectangle(
            (int)MathF.Round(cx - frameW * c.CenterX),
            (int)MathF.Round(cy - frameH * c.CenterY),
            Math.Max(1, (int)MathF.Round(frameW)),
            Math.Max(1, (int)MathF.Round(frameH)));
        var content = new Rectangle(
            (int)MathF.Round(cx - frameW * c.Width * 0.5f),
            (int)MathF.Round(cy - frameH * c.Height * 0.5f),
            Math.Max(1, (int)MathF.Round(frameW * c.Width)),
            Math.Max(1, (int)MathF.Round(frameH * c.Height)));

        return new VfxPlacement(frame, content, anchor, ratio);
    }
}

/// <summary>
/// Where a FIGURE is, once its strip's transparent margin is taken off.
/// </summary>
/// <remarks>
/// The arithmetic is <c>UiKit.AnimSprite</c>'s own, stated as a function so it can be checked without a
/// GraphicsDevice. AnimSprite crops the top pad, fills the destination box's HEIGHT with what is left,
/// and pushes the draw down by the bottom pad so the visible sole lands exactly on <c>box.Bottom</c>.
/// The consequences the VFX contract exists to handle: the drawn figure is SHORTER than its box, and it
/// is almost always much narrower — a 400-px-wide champion box holds a hunter who draws 162 px.
/// </remarks>
public static class VfxFigure
{
    /// <summary>The visible rectangle a strip with content box <paramref name="c"/> fills in <paramref name="box"/>.</summary>
    public static Rectangle VisualRect(Rectangle box, ContentBox c)
    {
        var shown = MathF.Max(0.01f, 1f - c.Top);          // what is left after the top crop
        var h = Math.Max(1, (int)MathF.Round(box.Height * (1f - c.Top - c.Bottom) / shown));
        var w = Math.Max(1, (int)MathF.Round((1f - 2f * c.Left) * box.Height / shown));
        return new Rectangle(box.Center.X - w / 2, box.Bottom - h, w, h);
    }
}

/// <summary>
/// The frame's visual bounds, published by the screen each frame and read by the effects pass.
/// </summary>
/// <remarks>
/// <para>
/// This exists to kill two audited defects with one change. Creature rectangles used to be written
/// inside <c>Draw</c> while every spawn ran inside <c>Update</c>, so effects aimed at where a creature
/// was LAST frame and fell back to a literal <c>(_rowCentreX, _rowTopY + 110)</c> on a wave's first
/// frame. And every champion-side effect used the un-pushed layout box while the champion is drawn at
/// <c>ChampBox.X + push</c> — up to 40 px away during a swing.
/// </para>
/// <para>
/// The fix is an ordering rule, not a dial: <b>spawn sites name a subject and a profile and never
/// compute a pixel</b>; the screen publishes bounds as the first thing it draws; the effects pass
/// resolves against what was published this frame.
/// </para>
/// </remarks>
public sealed class VfxBoundsRegistry : IVfxBoundsSource
{
    private readonly System.Collections.Generic.Dictionary<VfxSubject, VisualBounds> _bounds = new();

    /// <summary>Forget every subject. Only for a wave/replay rebuild — a frame REPLACES, it does not clear.</summary>
    public void Clear() => _bounds.Clear();

    /// <summary>Publish (or replace) one subject's visible rectangle for this frame.</summary>
    public void Publish(VfxSubject subject, Rectangle visible, int facing)
        => _bounds[subject] = new VisualBounds(visible, facing);

    public bool TryBounds(VfxSubject subject, out VisualBounds bounds)
        => _bounds.TryGetValue(subject, out bounds);

    /// <summary>Every published subject — the debug view walks it.</summary>
    public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<VfxSubject, VisualBounds>> All => _bounds;
}
