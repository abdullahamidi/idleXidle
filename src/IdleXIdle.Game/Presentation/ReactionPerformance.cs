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

    /// <summary>The bitten creature's body as DRAWN this frame (the fangs ride it: its lunge, its bob, its fall).</summary>
    bool TryCaughtBody(int slot, out Rectangle body);

    /// <summary>The frame the creature in <paramref name="slot"/> was drawn from this frame (its silhouette).</summary>
    bool TryTargetFrame(int slot, out SpriteFrame frame);
}

/// <summary>What one <see cref="ReactionPerformance.Update"/> crossed: the SNAP (the cue, a kill's fall) and the number's own frame.</summary>
public readonly record struct ReactionStep(bool Snapped, bool Answered, Vector2 SnapAt);

/// <summary>
/// ONE REACTION BEING PRESENTED (ADR-011, the REACTION archetype): the Seeker's JAWS answering the creature whose bite
/// set it off, as SHADOW FANGS (the final direction, 2026-09-27).
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing. It reads where the bitten creature is each frame, so the fangs stay round it through the
/// creature's own lunge and follow-through. It moves nothing.
/// </para>
/// <para>
/// THE PHRASE, in ms after the first frame that showed the contact. FOUR fangs (one hand-authored shape, drawn four
/// times: two from above with their points down, two from below with their points up, each leaning a little toward
/// the body's centre) appear OPEN at 0: the points clearly OUTSIDE the creature's top and bottom edges, with empty
/// space between the upper fangs, the body and the lower fangs. The open pose stands to ~25, then the fangs close
/// RAPIDLY inward (upper down, lower up) and SNAP at 55 onto the creature's outer silhouette, the points a little
/// inside its edges and the body still between them (<see cref="Snapped"/>: the cue's transient, a kill's fall).
/// They HOLD there ~85 ms, the semantic pose, TARGET BETWEEN SHADOW FANGS; the number lands 40 ms into the hold
/// (<see cref="Answered"/>). Then they retract a few px outward and fade, gone by 200. Nothing else is drawn: no
/// particles, no glint, no trail, no residue, no flash under them.
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

    /// <summary>The creatures the answer landed on (the reflected Strikes), front to back.</summary>
    public IReadOnlyList<int> Targets { get; }

    /// <summary>The reaction's Source colour (the overlay's).</summary>
    public Color Tint { get; }

    /// <summary>The Seeker's belt anchor last frame (the anchor overlay names his side of the exchange).</summary>
    public Vector2 BeltAt { get; private set; }

    /// <summary>Where each target was clamped last frame (its body's centre; the overlay).</summary>
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

    /// <summary>Whether the fangs have snapped: the moment the screen plays the cue and presents a kill's fall.</summary>
    public bool Snapped => _snapped;

    /// <summary>Whether the number's frame has come (a little after the snap, so the fangs are seen first).</summary>
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
    /// How far closed the fangs are at <paramref name="u"/>: 0 OPEN (the points outside the silhouette; held through
    /// <see cref="ReactionRecipe.OpenMs"/>), 1 SNAPPED (the points on the silhouette, from <see cref="ReactionRecipe.SnapAtMs"/>
    /// on). The close ACCELERATES (an ease-in): it begins gently and snaps shut, so the last frames carry the motion.
    /// </summary>
    public static float Close(ReactionRecipe r, float u)
    {
        if (u <= r.OpenMs) return 0f;
        if (u >= r.SnapAtMs) return 1f;
        var k = (u - r.OpenMs) / Math.Max(1f, r.SnapAtMs - r.OpenMs);
        return k * k;
    }

    /// <summary>The release's outward retraction, in px: 0 through the snap and the hold, then easing out to <see cref="ReactionRecipe.ReleasePx"/>.</summary>
    public static float Release(ReactionRecipe r, float u)
    {
        var since = u - (r.SnapAtMs + r.HoldMs);
        if (since <= 0f) return 0f;
        return r.ReleasePx * EaseOut(since / Math.Max(1f, r.ReleaseMs));
    }

    /// <summary>The fangs' opacity: whole through the snap AND the hold, a quick fade after, gone by <see cref="ReactionRecipe.GoneMs"/>.</summary>
    public static float Opacity(ReactionRecipe r, float u)
    {
        if (u < 0f || u >= r.GoneMs) return 0f;
        var fadeFrom = r.SnapAtMs + r.HoldMs;
        if (u < fadeFrom) return 1f;
        return 1f - Smooth((u - fadeFrom) / Math.Max(1f, r.GoneMs - fadeFrom));
    }

    /// <summary>
    /// Where the UPPER fangs' points are at <paramref name="u"/>, as a signed share of the body's height from its TOP
    /// edge (negative: outside, above it; positive: inside). The lower fangs mirror it about the bottom edge.
    /// </summary>
    public static float PointShare(ReactionRecipe r, float u)
    {
        var closed = Close(r, u);
        var share = -r.OpenGapShare + (r.BiteDepthShare + r.OpenGapShare) * closed;
        return share;
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
            // the fangs ride the body instead of re-sampling a moving silhouette. The layout body can hold a good deal
            // of empty canvas above a crouching head; the fangs must sit just outside the DRAWN shape. The body itself
            // stands when the silhouette cannot be read.
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
        // the playhead runs in thirds of a millisecond: the frame drawn AT the snap reads 54.99 ms, so a half-millisecond
        // of slack keeps the cue on the frame that shows the fangs shut
        var snapped = !_snapped && u >= Recipe.SnapAtMs - 0.5f;
        if (snapped) _snapped = true;
        var answered = !_answered && u >= Recipe.AnswerAtMs - 0.5f;
        if (answered) _answered = true;
        return new ReactionStep(snapped, answered, _snapAt.Length > 0 ? _snapAt[0] : default);
    }

    // ── THE POSE ─────────────────────────────────────────────────────────────────────────────────

    private readonly record struct FangPose(Vector2 At, float Rotation, float Scale, float Alpha, bool Lower);

    /// <summary>
    /// The four fangs on target <paramref name="k"/> this frame, into <paramref name="fangs"/> (upper-left, upper-right,
    /// lower-left, lower-right); false when the reaction is not in the world.
    /// </summary>
    private bool Pose(IReactionStage stage, int k, float u, Span<FangPose> fangs)
    {
        if (u < 0f || u >= Recipe.GoneMs || !stage.TryCaughtBody(Targets[k], out var layout) || layout.Height <= 0) return false;
        // the DRAWN silhouette's box this frame: the pinned shares of the layout body
        var sh = _silhouette[k] ?? new Vector4(0f, 0f, 1f, 1f);
        var body = new Rectangle((int)(layout.X + sh.X * layout.Width), (int)(layout.Y + sh.Y * layout.Height),
                                 Math.Max(1, (int)((sh.Z - sh.X) * layout.Width)), Math.Max(1, (int)((sh.W - sh.Y) * layout.Height)));
        var centre = new Vector2(body.X + body.Width * 0.5f, body.Y + body.Height * 0.5f);
        _snapAt[k] = centre;
        var height = Math.Clamp(Recipe.FangHeightShare * body.Height, Recipe.FangMinPx, Recipe.FangMaxPx) * ReactionRecipes.SizeDial;
        var scale = height / Recipe.ArtHeight;
        var spread = Recipe.PairSpreadShare * height;
        var release = Release(Recipe, u);
        var alpha = Opacity(Recipe, u);
        // the points: outside the top/bottom edges when OPEN, a little inside them when SNAPPED, retracting on the release
        var inside = PointShare(Recipe, u) * body.Height - release;
        var upperY = body.Y + inside;
        var lowerY = body.Y + body.Height - inside;
        var tilt = MathHelper.ToRadians(Recipe.TiltDegrees);
        // an upper fang's point is down (the art's own way); a left fang leans right (toward the centre), a right fang
        // leans left; a lower fang is the same sprite flipped vertically, so its lean is mirrored too
        fangs[0] = new FangPose(new Vector2(centre.X - spread, upperY), +tilt, scale, alpha, Lower: false);
        fangs[1] = new FangPose(new Vector2(centre.X + spread, upperY), -tilt, scale, alpha, Lower: false);
        fangs[2] = new FangPose(new Vector2(centre.X - spread, lowerY), -tilt, scale, alpha, Lower: true);
        fangs[3] = new FangPose(new Vector2(centre.X + spread, lowerY), +tilt, scale, alpha, Lower: true);
        return true;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The fangs, in the normal alpha batch after the figures: always foreground, four sprites per target.</summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        if (stage.Texture(Recipe.FangKey) is not { } tex) return;
        Span<FangPose> fangs = stackalloc FangPose[4];
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!Pose(stage, k, u, fangs)) continue;
            for (var f = 0; f < 4; f++)
            {
                var p = fangs[f];
                // the origin is the POINT: a lower fang is flipped vertically, so its point is at the canvas's top row
                var origin = p.Lower ? new Vector2(Recipe.TipPoint.X, Recipe.ArtHeight - 1f - Recipe.TipPoint.Y) : Recipe.TipPoint;
                b.Draw(tex, p.At, null, Color.White * p.Alpha, p.Rotation, origin, p.Scale,
                       p.Lower ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
                _sprites++;
            }
        }
    }
}
