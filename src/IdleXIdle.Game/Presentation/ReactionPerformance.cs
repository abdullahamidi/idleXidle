using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>What a reaction needs from the screen: where the figures are this frame.</summary>
public interface IReactionStage : IActionStage
{
    /// <summary>The champion's visible body this frame, WITH any presentation root motion (a HARD HANDS leap).</summary>
    bool TryChampionBody(out Rectangle body);

    /// <summary>The bitten creature's body as DRAWN this frame (the jaws ride it: its lunge, its bob, its fall).</summary>
    bool TryCaughtBody(int slot, out Rectangle body);

    /// <summary>The frame the creature in <paramref name="slot"/> was drawn from this frame (its silhouette).</summary>
    bool TryTargetFrame(int slot, out SpriteFrame frame);
}

/// <summary>What one <see cref="ReactionPerformance.Update"/> crossed: the SNAP (the cue, a kill's fall) and the number's own frame.</summary>
public readonly record struct ReactionStep(bool Snapped, bool Answered, Vector2 SnapAt);

/// <summary>
/// ONE REACTION BEING PRESENTED (ADR-011, the REACTION archetype): the Seeker's JAWS answering the creature whose bite
/// set it off, as a SHADOW MAW MADE OF MIST (the owner, 2026-09-28).
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing. It reads where the bitten creature is each frame, so the jaws stay on it through the
/// creature's own lunge and follow-through. It moves nothing.
/// </para>
/// <para>
/// THE PHRASE, in ms after the first frame that showed the contact. A MAW of two jaw pieces (an upper and a lower
/// tapering jaw with a blunt snout, each lined with graded teeth, turning about the hinge at the back corner of the
/// mouth), each piece ONE state of a sixteen-state mist-to-jaw strip, the whole maw leaning a little snout-down,
/// anchored to the creature's canonical body and its bite line (the throat, under the head). GATHER (0-83): the maw
/// grows out of nothing as loose violet smoke in front of the creature on the Seeker's side, a nearly shut jaw; it comes
/// in (40 px) and tightens, and waits, nearly shut, to 133 (the biting creature's own lunge drawing is up until then).
/// OPEN (133-200): the jaw visibly opens WIDE around the creature's head (the upper jaw clear of its eyes), condensing
/// until its teeth are clearly seen, and holds it to 233 (the wind-up before the bite). CLOSE (233-300): the jaws snap
/// shut, accelerating (the largest step on the snap frame). SNAP (300, on a frame): the jaws meet PAST their rest across
/// the creature's throat and stay there two
/// frames, the rows of teeth interlocking (<see cref="Snapped"/>: the cue, a kill's fall), the jaw HARDENING only now,
/// its teeth catching the light (it is smoke before and after). CLENCH: the pronounced first bite: the maw drives into
/// the creature along the jaw (part-way on the snap frame, whole on the next, then easing back: one push) and swells
/// (two frames, then easing back evenly), then settles to its rest (a slightly open mouth: two wedges, the creature
/// showing between them, the teeth interlocked). BITE (to 433): held, tightening slowly; the number lands 40 ms after
/// the snap (<see cref="Answered"/>). A second creature bitten by the same answer gets its own, smaller maw over its own
/// body. DISSOLVE (433-593): the jaw lets go, opening, and comes apart back into smoke one or two strip states a frame,
/// rising away and fading in even steps to nothing. Nothing else is drawn: no separate fog, no particles, no glint, no
/// trail, no flash, no light.
/// </para>
/// <para>
/// ON THE FIGHT'S PLAYHEAD. Its time is the playhead minus the first frame that showed the contact (the pump's frame).
/// No clock of its own: a slowed or scrubbed capture shows the same pose for the same moment. It changes no outcome:
/// its targets are the creatures the reflected Strikes hit, each answered on its own body.
/// </para>
/// </remarks>
public sealed class ReactionPerformance
{
    private readonly Vector2[] _snapAt;        // per target: the body's centre last frame (the overlay)
    private int _front;                        // the index (into Targets) of the front creature, as last drawn
    private readonly Vector4?[] _silhouette;   // per target: the drawn silhouette's box as shares of the layout body (read once); null = the body itself
    private float? _origin;                    // the playhead of the first frame that showed the contact
    private bool _snapped, _answered;
    private int _sprites;

    /// <summary>The recipe being presented.</summary>
    public ReactionRecipe Recipe { get; }

    /// <summary>The reaction's slot in the build.</summary>
    public int SkillSlot { get; }

    /// <summary>The contact: the bite's millisecond, on which the fight resolved the reaction and its answer.</summary>
    public int TriggerMs { get; }

    /// <summary>The creatures the answer landed on (the reflected Strikes), in the fight's event order (NOT front to back: <see cref="FrontTarget"/> finds the front one).</summary>
    public IReadOnlyList<int> Targets { get; }

    /// <summary>The reaction's Source colour (the overlay's).</summary>
    public Color Tint { get; }

    /// <summary>The Seeker's belt anchor last frame (the anchor overlay names his side of the exchange).</summary>
    public Vector2 BeltAt { get; private set; }

    /// <summary>Where each target was bitten last frame (its body's centre; the overlay).</summary>
    public IReadOnlyList<Vector2> ClampAt => _snapAt;

    /// <summary>A presentation of <paramref name="recipe"/> answering the bite at <paramref name="triggerMs"/>.</summary>
    public ReactionPerformance(ReactionRecipe recipe, int skillSlot, int triggerMs, IReadOnlyList<int> targets, Color tint)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        SkillSlot = skillSlot;
        TriggerMs = triggerMs;
        Targets = targets?.ToArray() ?? Array.Empty<int>();
        Tint = tint;
        _snapAt = new Vector2[Targets.Count];
        _silhouette = new Vector4?[Targets.Count];
    }

    /// <summary>The playhead of the first frame (the contact), once seen; the trigger until then.</summary>
    public float OriginMs => _origin ?? TriggerMs;

    /// <summary>Whether the jaws have shut: the moment the screen plays the cue and presents a kill's fall.</summary>
    public bool Snapped => _snapped;

    /// <summary>Whether the number's frame has come (a little after the snap, so the bite is seen first).</summary>
    public bool Answered => _answered;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => Recipe.EndMs;

    /// <summary>Nothing of it is left to draw.</summary>
    public bool Finished(float playheadMs) => playheadMs - OriginMs >= EndMs;

    /// <summary>Sprites drawn last frame (the trace's cost line).</summary>
    public int SpriteCount => _sprites;

    // ── THE CURVES (pure, so they are tested directly; u = ms after the first frame) ─────────────

    private static float Smooth(float v) { v = Math.Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    private static float EaseOut(float v) { v = Math.Clamp(v, 0f, 1f); return 1f - (1f - v) * (1f - v); }

    /// <summary>
    /// The bite's segments after the snap, clamped to the release (a retune never runs one past it): the jaws stay past
    /// their rest until <c>SettleFrom</c>, reach their rest at <c>Settled</c>, and let go at the release.
    /// </summary>
    private static (float SettleFrom, float Settled) BiteSpans(ReactionRecipe r)
    {
        var settleFrom = Math.Min(r.SnapAtMs + r.OvershootHoldMs, r.ReleaseAtMs);
        return (settleFrom, Math.Min(settleFrom + r.SettleMs, r.ReleaseAtMs));
    }

    /// <summary>How far through the release (the dissolve) <paramref name="u"/> is: 0 until the release, 1 at the end.</summary>
    public static float Dissolve(ReactionRecipe r, float u)
        => Math.Clamp((u - r.ReleaseAtMs) / Math.Max(1f, r.GoneMs - r.ReleaseAtMs), 0f, 1f);

    /// <summary>
    /// How CONDENSED the maw is at <paramref name="u"/>, 0 (a loose drift of mist) to 1 (the hardened jaw), along ONE smooth
    /// climb (never more than ~2 strip states a frame): loose smoke while it gathers (to <see cref="ReactionRecipe.CondenseAtGather"/>),
    /// still smoky while it opens and holds wide, HARDENING only as it closes (the crisp jaw is the bite: the snap and the
    /// two frames past its rest), easing back to a smoky jaw while it holds, and coming apart into smoke evenly through
    /// the dissolve.
    /// </summary>
    public static float Condense(ReactionRecipe r, float u)
    {
        if (u <= 0f) return 0f;
        if (u < r.GatherMs) return r.CondenseAtGather * Smooth(u / r.GatherMs);
        if (u < r.OpenFromMs) return MathHelper.Lerp(r.CondenseAtGather, r.CondenseFormed, (u - r.GatherMs) / Math.Max(1f, r.OpenFromMs - r.GatherMs));
        if (u < r.OpenedAtMs) return MathHelper.Lerp(r.CondenseFormed, r.CondenseOpened, (u - r.OpenFromMs) / Math.Max(1f, r.OpenedAtMs - r.OpenFromMs));
        if (u < r.CloseFromMs) return MathHelper.Lerp(r.CondenseOpened, r.CondenseHeld, (u - r.OpenedAtMs) / Math.Max(1f, r.CloseFromMs - r.OpenedAtMs));
        if (u < r.SnapAtMs) return MathHelper.Lerp(r.CondenseHeld, 1f, Close(r, u));
        var (settleFrom, _) = BiteSpans(r);
        if (u < settleFrom) return 1f;
        if (u < r.ReleaseAtMs) return MathHelper.Lerp(1f, r.CondenseHolding, Smooth((u - settleFrom) / Math.Max(1f, r.ReleaseAtMs - settleFrom)));
        return (settleFrom < r.ReleaseAtMs ? r.CondenseHolding : 1f) * (1f - Dissolve(r, u));
    }

    /// <summary>The strip state drawn at <paramref name="u"/> (0 = loose mist, last = the condensed jaw): one state per frame.</summary>
    public static int StateAt(ReactionRecipe r, float u)
        => Math.Clamp((int)MathF.Round(Condense(r, u) * (r.MawStates - 1)), 0, r.MawStates - 1);

    /// <summary>How far the jaws have CLOSED at <paramref name="u"/>: 0 wide open (through the held gape), 1 at the snap. An ease-in: it accelerates into the bite.</summary>
    public static float Close(ReactionRecipe r, float u)
    {
        if (u <= r.CloseFromMs) return 0f;
        if (u >= r.SnapAtMs) return 1f;
        return MathF.Pow((u - r.CloseFromMs) / Math.Max(1f, r.SnapAtMs - r.CloseFromMs), r.CloseEasePower);
    }

    /// <summary>
    /// The GAPE at <paramref name="u"/>, in degrees (X the upper jaw, Y the lower; screen: + is clockwise, so an open
    /// upper jaw is negative and an open lower jaw positive; before the maw's own tilt). The mist condenses into a nearly
    /// shut jaw, which visibly OPENS WIDE (an ease-in-out) and holds it; the jaws snap shut PAST their rest
    /// (<see cref="ReactionRecipe.Overshoot"/>) and stay there two frames; they settle to their REST gape (two wedges,
    /// the teeth interlocked across the creature), tighten slowly through the bite, and open (<see cref="ReactionRecipe.ReleaseGape"/>)
    /// as the jaw lets go and dissolves.
    /// </summary>
    public static Vector2 Gape(ReactionRecipe r, float u)
    {
        if (u < r.OpenFromMs) return r.GatherGape;
        if (u < r.OpenedAtMs) return Vector2.Lerp(r.GatherGape, r.WideGape, Smooth((u - r.OpenFromMs) / Math.Max(1f, r.OpenedAtMs - r.OpenFromMs)));
        if (u < r.CloseFromMs) return r.WideGape;
        if (u < r.SnapAtMs) return Vector2.Lerp(r.WideGape, r.Overshoot, Close(r, u));
        var (settleFrom, settled) = BiteSpans(r);
        if (u < settleFrom) return r.Overshoot;
        if (u < settled) return Vector2.Lerp(r.Overshoot, r.RestGape, EaseOut((u - settleFrom) / Math.Max(1f, settled - settleFrom)));
        if (u < r.ReleaseAtMs) return Vector2.Lerp(r.RestGape, r.TightGape, Smooth((u - settled) / Math.Max(1f, r.ReleaseAtMs - settled)));
        // the release starts from where the hold ended (the tight gape, or the rest when a short hold never tightened)
        var held = settled < r.ReleaseAtMs ? r.TightGape : settleFrom < r.ReleaseAtMs ? r.RestGape : r.Overshoot;
        return Vector2.Lerp(held, r.ReleaseGape, EaseOut(Dissolve(r, u)));
    }

    /// <summary>
    /// THE CLENCH's jolt at <paramref name="u"/>, in px ALONG the jaw (positive: into the creature): nothing before the
    /// snap; the maw drives in part-way on the snap frame, its whole <see cref="ReactionRecipe.ShakePx"/> on the next (the
    /// bite is SEEN to drive in, after the cue), then eases back over three frames: ONE push, never an in-out buzz, and
    /// never a bounce off the creature (at its peak on the snap frame and backing off at once, it read as a retreat).
    /// </summary>
    public static float Jolt(ReactionRecipe r, float u)
    {
        const float Frame = 1000f / 60f;
        var end = Math.Min(r.SnapAtMs + r.ClenchMs, r.ReleaseAtMs);            // never into the dissolve
        if (u < r.SnapAtMs - 0.5f || u >= end) return 0f;
        var frame = (int)MathF.Floor((u - r.SnapAtMs + 0.5f) / Frame);
        if (frame == 0) return 0.7f * r.ShakePx;                               // in part-way on the snap frame...
        var t = frame * Frame;                                                // ...whole on the next, then back in a straight line
        return r.ShakePx * Math.Clamp(1f - (t - Frame) / Math.Max(1f, end - r.SnapAtMs - Frame), 0f, 1f);
    }

    /// <summary>
    /// The maw's scale against its size at the bite: larger while the mist arrives (<see cref="ReactionRecipe.ArriveScale"/>,
    /// tightening to 1 by the end of the gathering), SWELLING on the snap (<see cref="ReactionRecipe.ClenchPulse"/>, held
    /// two frames, then easing back as the jaws settle), spreading as it dissolves.
    /// </summary>
    public static float MawScale(ReactionRecipe r, float u)
    {
        if (u < r.GatherMs) return 1f + (r.ArriveScale - 1f) * (1f - Smooth(Math.Max(0f, u) / r.GatherMs));
        if (u < r.SnapAtMs - 0.5f) return 1f;
        var easeFrom = Math.Min(r.SnapAtMs + r.SwellHoldMs, r.ReleaseAtMs);
        var easeEnd = Math.Min(easeFrom + r.SwellEaseMs, r.ReleaseAtMs);          // the swell is gone by the release, whatever the hold
        if (u < easeFrom) return 1f + r.ClenchPulse;
        if (u < r.ReleaseAtMs) return 1f + r.ClenchPulse * (1f - Math.Clamp((u - easeFrom) / Math.Max(1f, easeEnd - easeFrom), 0f, 1f));
        return 1f + r.ReleaseGrow * Smooth(Dissolve(r, u));
    }

    /// <summary>
    /// The maw's opacity (over the strip's own alpha, which is thinner while the jaw is mist): it grows out of nothing
    /// along an ease-in-out (<see cref="ReactionRecipe.OpacityAtSpawn"/>, whole after <see cref="ReactionRecipe.OpacityRampMs"/>),
    /// stays whole through the open, the close and the bite, and fades in even steps through the dissolve.
    /// </summary>
    public static float Opacity(ReactionRecipe r, float u)
    {
        if (u < 0f || u >= r.GoneMs) return 0f;
        if (u < r.OpacityRampMs) return MathHelper.Lerp(r.OpacityAtSpawn, 1f, Smooth(u / r.OpacityRampMs));
        return 1f - MathF.Pow(Dissolve(r, u), 1.1f);
    }

    /// <summary>The maw this frame: its HINGE on screen, each jaw's turn about it (radians), and its scale.</summary>
    public readonly record struct MawPose(Vector2 HingeAt, float UpperRotation, float LowerRotation, float Scale);

    /// <summary>
    /// The maw for a creature whose drawn silhouette (as pinned off the pose it bit in) is <paramref name="body"/> and whose
    /// canonical body this frame is <paramref name="layout"/>, at <paramref name="u"/>. HORIZONTALLY it is anchored to the
    /// canonical body (it follows the creature home: a lunge's art reaches far ahead of the creature, and a maw anchored to
    /// it shut on the empty floor in front of the creature once it stood back up); VERTICALLY the shut seam crosses the
    /// drawn silhouette on its bite line where the teeth close (the hinge is set higher by the tilt's drop along the jaw).
    /// Pure, so the jaw's geometry is tested directly.
    /// </summary>
    public static MawPose Maw(ReactionRecipe r, Rectangle body, Rectangle layout, float u, bool secondary = false)
    {
        var anchor = layout.Width > 0 ? layout : body;
        var length = Math.Clamp(r.MawLengthShare * anchor.Width, r.MawMinPx, r.MawMaxPx) * ReactionRecipes.SizeDial
                     * (secondary ? r.SecondaryScale : 1f);
        var lead = secondary ? r.SecondaryLeadShare : r.HingeLeadShare;
        var tilt = MathHelper.ToRadians(r.MawTiltDegrees);
        var arrive = !secondary && u < r.GatherMs ? r.ArrivePx * (1f - Smooth(Math.Max(0f, u) / r.GatherMs)) : 0f;   // a rear maw forms in place
        var rise = r.ReleaseRisePx * EaseOut(Dissolve(r, u));
        var biteY = body.Y + r.BiteLineShare * body.Height;
        var hinge = new Vector2(anchor.X - lead * length - arrive, biteY - r.BiteSeamShare * length * MathF.Sin(tilt) - rise);
        hinge += Jolt(r, u) * new Vector2(MathF.Cos(tilt), MathF.Sin(tilt));   // the clench drives along the jaw, into the creature
        var gape = Gape(r, u) + new Vector2(r.MawTiltDegrees, r.MawTiltDegrees);
        return new MawPose(hinge, MathHelper.ToRadians(gape.X), MathHelper.ToRadians(gape.Y), length / r.JawArtLength * MawScale(r, u));
    }

    // ── EACH FRAME ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Advance on the fight's playhead: pin each target's silhouette once, and notice the snap and the number's frame.</summary>
    public ReactionStep Update(float playheadMs, IReactionStage stage)
    {
        _origin ??= Math.Max(TriggerMs, playheadMs);
        var u = playheadMs - OriginMs;
        for (var k = 0; k < Targets.Count; k++)
        {
            // THE SILHOUETTE IS READ ONCE, off the pose the creature bites in, and kept as shares of its layout body, so
            // the jaws ride the body instead of re-sampling a moving silhouette. The layout body can hold a good deal of
            // empty canvas above a crouching head; the jaws are placed on the DRAWN shape. The body itself stands when the
            // silhouette cannot be read.
            if (_silhouette[k] is null && stage.TryCaughtBody(Targets[k], out var body) && body.Width > 0 && body.Height > 0)
            {
                var shares = new Vector4(0f, 0f, 1f, 1f);   // left, top, right, bottom as shares of the body
                if (stage.TryTargetFrame(Targets[k], out var frame) && SilhouetteProbe.OpaqueBounds(frame) is { Width: > 0, Height: > 0 } s)
                    shares = new Vector4(Math.Clamp((s.X - body.X) / (float)body.Width, -0.5f, 1f),
                                         Math.Clamp((s.Y - body.Y) / (float)body.Height, -0.5f, 1f),
                                         Math.Clamp((s.Right - body.X) / (float)body.Width, 0f, 1.5f),
                                         Math.Clamp((s.Bottom - body.Y) / (float)body.Height, 0f, 1.5f));
                _silhouette[k] = shares;
            }
        }
        // the playhead runs in thirds of a millisecond, so a frame drawn at the snap's millisecond can read a hair under
        // it: a half-millisecond of slack keeps the cue on the first frame that shows the jaws shut
        var snapped = !_snapped && u >= Recipe.SnapAtMs - 0.5f;
        if (snapped) _snapped = true;
        var answered = !_answered && u >= Recipe.AnswerAtMs - 0.5f;
        if (answered) _answered = true;
        return new ReactionStep(snapped, answered, _snapAt.Length > 0 ? _snapAt[Math.Max(0, _front)] : default);
    }

    // ── THE POSE ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The creature's DRAWN silhouette box this frame (the pinned shares of its layout body; the body itself when unread),
    /// and its canonical layout body.
    /// </summary>
    private bool TrySilhouette(IReactionStage stage, int k, out Rectangle body, out Rectangle layout)
    {
        body = default;
        if (!stage.TryCaughtBody(Targets[k], out layout) || layout.Height <= 0) return false;
        var sh = _silhouette[k] ?? new Vector4(0f, 0f, 1f, 1f);
        body = new Rectangle((int)(layout.X + sh.X * layout.Width), (int)(layout.Y + sh.Y * layout.Height),
                             Math.Max(1, (int)((sh.Z - sh.X) * layout.Width)), Math.Max(1, (int)((sh.W - sh.Y) * layout.Height)));
        return true;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The FRONT creature among <paramref name="targets"/>: the index of the one whose canonical body stands nearest the
    /// Seeker (the smallest X; the first on a tie; a creature with no body is skipped), or -1 when none has a body.
    /// </summary>
    public static int FrontTarget(IReactionStage stage, IReadOnlyList<int> targets)
    {
        var front = -1;
        var frontX = int.MaxValue;
        for (var k = 0; k < targets.Count; k++)
            if (stage.TryCaughtBody(targets[k], out var at) && at.Height > 0 && at.X < frontX) { front = k; frontX = at.X; }
        return front;
    }

    /// <summary>
    /// The maw, in the normal alpha batch after the creatures and BEFORE the champion (over the creature it bites; when the
    /// Seeker stands in front of that creature, as HARD HANDS does, she is in front of its jaw): the upper and the lower
    /// jaw, each ONE condensation state off the strip, turned about the shared hinge: two sprites per target. Never additive.
    /// </summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        var alpha = Opacity(Recipe, u);
        if (alpha < ReactionRecipe.VisibleFloor || stage.Texture(Recipe.MawKey) is not { } tex) return;
        var state = StateAt(Recipe, u);
        var upper = new Rectangle(state * Recipe.PieceWidth, 0, Recipe.PieceWidth, Recipe.PieceHeight);
        var lower = new Rectangle(state * Recipe.PieceWidth, Recipe.PieceHeight, Recipe.PieceWidth, Recipe.PieceHeight);
        var tint = Color.White * alpha;
        // the FRONT creature (nearest the Seeker) gets the full maw; any other creature the same answer bit gets its own,
        // smaller one over its own body (a full maw reached back across the creature in front of it)
        var front = FrontTarget(stage, Targets);
        _front = front;
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!TrySilhouette(stage, k, out var body, out var layout)) continue;
            _snapAt[k] = new Vector2(body.X + body.Width * 0.5f, body.Y + body.Height * 0.5f);
            var m = Maw(Recipe, body, layout, u, secondary: k != front);
            b.Draw(tex, m.HingeAt, upper, tint, m.UpperRotation, Recipe.Hinge, m.Scale, SpriteEffects.None, 0f);
            b.Draw(tex, m.HingeAt, lower, tint, m.LowerRotation, Recipe.Hinge, m.Scale, SpriteEffects.None, 0f);
            _sprites += 2;
        }
    }
}
