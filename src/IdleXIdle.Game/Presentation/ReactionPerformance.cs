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
/// set it off, as SHADOW FANGS MADE OF MIST (the owner, 2026-09-28).
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing. It reads where the bitten creature is each frame, so the jaws stay on it through the
/// creature's own lunge and follow-through. It moves nothing.
/// </para>
/// <para>
/// THE PHRASE, in ms after the first frame that showed the contact. FOUR teeth, each ONE state of a strip of a fang in
/// sixteen condensation states (loose mist to condensed tooth), two above the creature's middle with their points down
/// and two below with their points up, the rows interlocking like a shut mouth (the upper pair a little wider apart
/// than the lower, each point leaning in). GATHER (0-65): the mist grows out of nothing, comes IN from the sides (the
/// teeth start far wider apart and larger) and condenses slowly (an ease-in), so the loose smoke is what is seen; the
/// open jaw forms on the creature's body, below its health bar and above the skill dock. CLOSE (65-130): the jaws shut,
/// accelerating, three frames showing them move; at the SNAP (130) the upper points have passed the lower ones across
/// the creature's lower middle: the teeth are CLOSED on it (<see cref="Snapped"/>: the cue, a kill's fall). BITE (130-190):
/// the shut jaws hold, the number lands 40 ms in (<see cref="Answered"/>). DISSOLVE (190-300): the jaws let go and
/// open, the teeth LOOSEN back into smoke over several frames (the strip backward) while the smoke rises and spreads,
/// fading evenly to nothing. Nothing else is drawn: no separate fog, no particles, no glint, no trail, no flash, no additive light.
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

    /// <summary>How far through the release (the dissolve) <paramref name="u"/> is: 0 until the release, 1 at the end.</summary>
    public static float Dissolve(ReactionRecipe r, float u)
        => Math.Clamp((u - r.ReleaseAtMs) / Math.Max(1f, r.GoneMs - r.ReleaseAtMs), 0f, 1f);

    /// <summary>
    /// How CONDENSED the teeth are at <paramref name="u"/>, 0 (a loose drift of mist) to 1 (the condensed tooth). It
    /// EASES IN while the mist gathers (to <see cref="ReactionRecipe.CondenseAtGather"/>), so the loose smoke is what the
    /// eye sees for most of the gathering; it reaches 1 as the jaws shut, holds through the bite, and as the teeth let go
    /// it falls back to 0 over the first <see cref="ReactionRecipe.LoosenShare"/> of the dissolve (an ease-in-out: the
    /// teeth come apart into smoke over several frames), the smoke then fading on its own.
    /// </summary>
    public static float Condense(ReactionRecipe r, float u)
    {
        if (u <= 0f) return 0f;
        if (u < r.GatherMs) { var g = u / r.GatherMs; return r.CondenseAtGather * g * g; }
        if (u < r.SnapAtMs) return MathHelper.Lerp(r.CondenseAtGather, 1f, (u - r.GatherMs) / Math.Max(1f, r.SnapAtMs - r.GatherMs));
        if (u < r.ReleaseAtMs) return 1f;
        return 1f - Smooth(Dissolve(r, u) / Math.Max(0.05f, r.LoosenShare));
    }

    /// <summary>The strip state drawn at <paramref name="u"/> (0 = loose mist, last = the condensed tooth): one state per frame.</summary>
    public static int StateAt(ReactionRecipe r, float u)
        => Math.Clamp((int)MathF.Round(Condense(r, u) * (r.FangStates - 1)), 0, r.FangStates - 1);

    /// <summary>
    /// How far the jaws have CLOSED at <paramref name="u"/>: 0 open (through the gathering), 1 shut (from the snap on).
    /// The close ACCELERATES (an ease-in of <see cref="ReactionRecipe.CloseEasePower"/>), so it reads as a snap, yet
    /// three 60 fps frames show it moving, each step larger than the last.
    /// </summary>
    public static float Close(ReactionRecipe r, float u)
    {
        if (u <= r.GatherMs) return 0f;
        if (u >= r.SnapAtMs) return 1f;
        return MathF.Pow((u - r.GatherMs) / Math.Max(1f, r.SnapAtMs - r.GatherMs), r.CloseEasePower);
    }

    /// <summary>
    /// Where a row's points are at <paramref name="u"/>, as a share of the silhouette's height measured inward from that
    /// row's own edge (the upper row from the top, the lower row from the bottom): its open share while the mist
    /// gathers, closing to <see cref="ReactionRecipe.MeetShare"/> + <see cref="ReactionRecipe.OverlapShare"/> at the
    /// snap, past the middle, so the upper points end BELOW the lower ones and the teeth are closed.
    /// </summary>
    public static float PointShare(ReactionRecipe r, float u, bool lower)
    {
        var open = lower ? r.LowerOpenShare : r.UpperOpenShare;
        return open + (r.MeetShare + r.OverlapShare - open) * Close(r, u);
    }

    /// <summary>How far (px) each row of teeth has pulled back from the bite: 0 until the release, then easing out to <see cref="ReactionRecipe.ReleaseDriftPx"/>.</summary>
    public static float ReleaseDrift(ReactionRecipe r, float u) => r.ReleaseDriftPx * EaseOut(Dissolve(r, u));

    /// <summary>How far (px) the dissolving smoke has risen off the creature: 0 until the release, then easing in-out to <see cref="ReactionRecipe.ReleaseRisePx"/>.</summary>
    public static float ReleaseRise(ReactionRecipe r, float u) => r.ReleaseRisePx * Smooth(Dissolve(r, u));

    /// <summary>
    /// The teeth's scale against their condensed size, and the spread of the pairs against the jaw's: the gathering mist
    /// is larger and much wider apart and draws together into the jaw across the whole gathering (an ease-in-out, 1 at
    /// its end); through the close and the bite both are 1; as the teeth dissolve the smoke spreads out again.
    /// </summary>
    public static (float Scale, float Spread) MistSpread(ReactionRecipe r, float u)
    {
        if (u < r.GatherMs)
        {
            var k = 1f - Smooth(Math.Max(0f, u) / r.GatherMs);
            return (1f + (r.ArriveScale - 1f) * k, 1f + (r.ArriveSpread - 1f) * k);
        }
        var grow = r.ReleaseGrow * Smooth(Dissolve(r, u));
        return (1f + grow, 1f + 0.6f * grow);
    }

    /// <summary>
    /// The teeth's opacity (over the strip's own alpha, which is already thin while the teeth are mist): it grows out of
    /// nothing along an ease-in-out (<see cref="ReactionRecipe.OpacityAtSpawn"/>, whole after
    /// <see cref="ReactionRecipe.OpacityRampMs"/>), stays whole through the close and the bite, and fades EVENLY through
    /// the dissolve (a gentle curve, so the last visible frame is faint and nothing cuts off).
    /// </summary>
    public static float Opacity(ReactionRecipe r, float u)
    {
        if (u < 0f || u >= r.GoneMs) return 0f;
        if (u < r.OpacityRampMs) return MathHelper.Lerp(r.OpacityAtSpawn, 1f, Smooth(u / r.OpacityRampMs));
        return 1f - MathF.Pow(Dissolve(r, u), 1.25f);
    }

    /// <summary>One tooth this frame: its POINT on screen, its turn about the point, its scale, whether it is a lower (flipped) tooth.</summary>
    public readonly record struct ToothPose(Vector2 At, float Rotation, float Scale, bool Lower);

    /// <summary>
    /// The four teeth for a creature whose drawn silhouette is <paramref name="body"/>, at <paramref name="u"/>, into
    /// <paramref name="teeth"/>: upper-left, upper-right, lower-left, lower-right. Pure, so the jaw's geometry is tested
    /// directly (the points meet and pass, the rows interlock, the lower pair never overlaps itself).
    /// </summary>
    public static void ToothPoses(ReactionRecipe r, Rectangle body, float u, Span<ToothPose> teeth)
    {
        var centreX = body.X + body.Width * 0.5f;
        var upper = Math.Clamp(r.UpperToothShare * body.Height, r.FangMinPx, r.FangMaxPx) * ReactionRecipes.SizeDial;
        var lower = upper * r.LowerToothScale;
        var (grow, spreadK) = MistSpread(r, u);
        var drift = ReleaseDrift(r, u);
        var rise = ReleaseRise(r, u);
        var upperY = body.Y + PointShare(r, u, lower: false) * body.Height - drift - rise;
        var lowerY = body.Y + body.Height - PointShare(r, u, lower: true) * body.Height + drift - rise;
        var upperX = r.UpperSpreadShare * upper * spreadK;
        var lowerX = r.LowerSpreadShare * lower * spreadK;
        var tilt = MathHelper.ToRadians(r.TiltDegrees);
        // each point leans toward the centre line: the art's upper tooth points down, so turning it anticlockwise (a
        // negative angle on screen) swings its root out and its point in for the LEFT tooth; the right tooth mirrors it;
        // a lower tooth is the same sprite flipped vertically, so its turn is mirrored too
        var us = upper / r.ToothArtHeight * grow;
        var ls = lower / r.ToothArtHeight * grow;
        teeth[0] = new ToothPose(new Vector2(centreX - upperX, upperY), -tilt, us, Lower: false);
        teeth[1] = new ToothPose(new Vector2(centreX + upperX, upperY), +tilt, us, Lower: false);
        teeth[2] = new ToothPose(new Vector2(centreX - lowerX, lowerY), +tilt, ls, Lower: true);
        teeth[3] = new ToothPose(new Vector2(centreX + lowerX, lowerY), -tilt, ls, Lower: true);
    }

    /// <summary>
    /// The sprite origin that puts a tooth's POINT at its pose: the state's own point, or, for a lower tooth (the state
    /// flipped vertically), the same point measured from the state's bottom.
    /// </summary>
    public static Vector2 ToothOrigin(ReactionRecipe r, bool lower)
        => lower ? new Vector2(r.TipPoint.X, r.StateHeight - 1f - r.TipPoint.Y) : r.TipPoint;

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
        return new ReactionStep(snapped, answered, _snapAt.Length > 0 ? _snapAt[0] : default);
    }

    // ── THE POSE ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The creature's DRAWN silhouette box this frame (the pinned shares of its layout body; the body itself when unread).</summary>
    private bool TrySilhouette(IReactionStage stage, int k, out Rectangle body)
    {
        body = default;
        if (!stage.TryCaughtBody(Targets[k], out var layout) || layout.Height <= 0) return false;
        var sh = _silhouette[k] ?? new Vector4(0f, 0f, 1f, 1f);
        body = new Rectangle((int)(layout.X + sh.X * layout.Width), (int)(layout.Y + sh.Y * layout.Height),
                             Math.Max(1, (int)((sh.Z - sh.X) * layout.Width)), Math.Max(1, (int)((sh.W - sh.Y) * layout.Height)));
        return true;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The teeth, in the normal alpha batch after the figures (always foreground, over the creature they bite): each tooth
    /// is ONE condensation state off the strip, four sprites per target. Never additive.
    /// </summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        var alpha = Opacity(Recipe, u);
        if (alpha < ReactionRecipe.FangVisibleFloor || stage.Texture(Recipe.FangKey) is not { } tex) return;
        var state = StateAt(Recipe, u);
        var src = new Rectangle(state * Recipe.StateWidth, 0, Recipe.StateWidth, Recipe.StateHeight);
        Span<ToothPose> teeth = stackalloc ToothPose[4];
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!TrySilhouette(stage, k, out var body)) continue;
            _snapAt[k] = new Vector2(body.X + body.Width * 0.5f, body.Y + body.Height * 0.5f);
            ToothPoses(Recipe, body, u, teeth);
            for (var f = 0; f < 4; f++)
            {
                var p = teeth[f];
                b.Draw(tex, p.At, src, Color.White * alpha, p.Rotation, ToothOrigin(Recipe, p.Lower), p.Scale,
                       p.Lower ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);
                _sprites++;
            }
        }
    }
}
