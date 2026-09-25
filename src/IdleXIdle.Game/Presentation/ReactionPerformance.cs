using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>What a reaction needs from the screen: where the figures are this frame, and what they are doing.</summary>
public interface IReactionStage : IActionStage
{
    /// <summary>The champion's visible body this frame, WITH any presentation root motion (a HARD HANDS leap).</summary>
    bool TryChampionBody(out Rectangle body);

    /// <summary>
    /// The caught creature's body as DRAWN this frame, the yank included (the clamp rides it). An action's
    /// <see cref="IActionStage.TryTargetBody"/> is the creature's own place, which a reaction never moves.
    /// </summary>
    bool TryCaughtBody(int slot, out Rectangle body);

    /// <summary>The frame the creature in <paramref name="slot"/> was drawn from this frame (its silhouette).</summary>
    bool TryTargetFrame(int slot, out SpriteFrame frame);

    /// <summary>Whether the creature in <paramref name="slot"/> has fallen (its death has begun).</summary>
    bool TargetFalling(int slot);

    /// <summary>Whether the champion has fallen: the chain's own end is going down, so the clamp slackens and fades.</summary>
    bool ChampionFalling { get; }

    /// <summary>Whether an authored action is in its focus (release to contact): the reaction's light then plays secondary.</summary>
    bool ActionInFocus { get; }
}

/// <summary>What one <see cref="ReactionPerformance.Update"/> crossed, for the trace.</summary>
public readonly record struct ReactionStep(bool Clamped, bool YankEnded, bool Retracting, Vector2 ClampAt);

/// <summary>
/// ONE REACTION BEING PRESENTED (ADR-011, REACTION / TRAP): the Seeker's JAWS answering the creature whose bite set it off.
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing. It reads where his body IS each frame, so the tether's end rides his belt through a leap; and
/// where the caught creature is, so the clamp stays on it through the yank.
/// </para>
/// <para>
/// A MECHANISM: a spring-loaded hunting clamp, never a head. The bite FIRES the tether from his belt: on the first frame
/// the open clamp is at the creature with the chain whipping out behind it and a Source streak along it. The two arms
/// are rigid and each turns about its OWN hinge pin in the housing's brackets: from open to a hard stop (a few degrees
/// open: they stopped on a limb) in one frame, a small recoil, locked. The chain takes the strain and JERKS the creature
/// toward the Seeker. Then the arms unlock and the clamp is REELED BACK along its chain into his belt, which is why
/// nothing stays in the world and why the dock now reads REARMING. A creature that falls is let go and not dragged; a
/// champion who falls lets his chain go slack.
/// </para>
/// <para>
/// ON THE FIGHT'S PLAYHEAD. Its time is the playhead minus the first frame that showed the contact (the pump's frame,
/// so the open arms are always seen once before they slam). No clock of its own: a slowed or scrubbed capture shows the
/// same pose for the same moment. It changes no outcome: its targets are the creatures the reflected Strikes hit.
/// </para>
/// </remarks>
public sealed class ReactionPerformance
{
    private readonly Vector2?[] _clampShare;   // per target: the clamp point, as a share of its body (read once)
    private readonly Vector2[] _clampAt;       // per target: where it was drawn last frame (the overlay)
    private readonly Vector2[] _chainPts;      // the chain's sampled curve, reused every frame (the streak follows it)
    private float? _origin;                    // the playhead of the first frame that showed the contact
    private float? _retractFrom;               // a creature that fell: the clamp lets go and retracts early
    private float? _slackFrom;                 // the champion fell: the chain slackens and the clamp fades in place
    private bool _clamped, _yankEnded, _retracting;
    private int _sprites, _links, _chainCount;

    /// <summary>The recipe being presented.</summary>
    public ReactionRecipe Recipe { get; }

    /// <summary>The reaction's slot in the build.</summary>
    public int SkillSlot { get; }

    /// <summary>The contact: the bite's millisecond, on which the fight resolved the reaction and its answer.</summary>
    public int TriggerMs { get; }

    /// <summary>The creatures the answer landed on (the reflected Strikes), front to back.</summary>
    public IReadOnlyList<int> Targets { get; }

    /// <summary>The reaction's Source colour: its light.</summary>
    public Color Tint { get; }

    /// <summary>Where the belt anchor and each clamp point were drawn last frame (the anchor overlay reads these).</summary>
    public Vector2 BeltAt { get; private set; }

    /// <inheritdoc cref="BeltAt"/>
    public IReadOnlyList<Vector2> ClampAt => _clampAt;

    /// <summary>A presentation of <paramref name="recipe"/> answering the bite at <paramref name="triggerMs"/>.</summary>
    public ReactionPerformance(ReactionRecipe recipe, int skillSlot, int triggerMs, IReadOnlyList<int> targets, Color tint)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        SkillSlot = skillSlot;
        TriggerMs = triggerMs;
        Targets = targets?.ToArray() ?? Array.Empty<int>();
        Tint = tint;
        _clampShare = new Vector2?[Targets.Count];
        _clampAt = new Vector2[Targets.Count];
        _chainPts = new Vector2[Math.Max(2, recipe.BodySegments + 1)];   // allocated once per trigger, never per frame
    }

    /// <summary>The playhead of the first frame (the contact), once seen; the trigger until then.</summary>
    public float OriginMs => _origin ?? TriggerMs;

    /// <summary>When the clamp starts back to the belt, in ms after the first frame (earlier when its creature fell).</summary>
    public float RetractFromMs => _retractFrom ?? Recipe.RetractAtMs;

    /// <summary>Whether the champion fell on this bite (the chain slackens, nothing is reeled in).</summary>
    public bool Slack => _slackFrom is not null;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => _slackFrom is { } s ? s + Recipe.SlackFadeMs : RetractFromMs + Recipe.RetractMs;

    /// <summary>Nothing of it is left to draw.</summary>
    public bool Finished(float playheadMs) => playheadMs - OriginMs >= EndMs;

    /// <summary>Sprites and chain link accents drawn last frame (the trace's cost line).</summary>
    public int SpriteCount => _sprites;

    /// <inheritdoc cref="SpriteCount"/>
    public int ChainLinks => _links;

    // ── THE CURVES (pure, so they are tested directly; u = ms after the first frame) ─────────────

    private static float Smooth(float v) { v = Math.Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    private static float EaseOut(float v) { v = Math.Clamp(v, 0f, 1f); return 1f - (1f - v) * (1f - v); }

    /// <summary>
    /// Each arm's angle off its drawn stop (degrees, about its own hinge): open on the first frame, an ACCELERATING slam
    /// to the hard stop by <see cref="ReactionRecipe.CloseMs"/>, a small damped recoil, locked; unlocking when it
    /// retracts. Rotation only.
    /// </summary>
    public static float JawAngle(ReactionRecipe r, float u, float retractFrom)
    {
        if (u <= 0f) return r.OpenDeg;
        if (u < r.CloseMs)
        {
            var v = u / r.CloseMs;
            return r.StopDeg + (r.OpenDeg - r.StopDeg) * (1f - v * v);
        }
        var angle = r.StopDeg;
        var since = u - r.CloseMs;
        if (since < r.ReboundMs)
        {
            var v = since / r.ReboundMs;
            angle += r.ReboundDeg * MathF.Sin(MathF.PI * v) * (1f - v);
        }
        if (u >= retractFrom) angle += (r.UnlockDeg - r.StopDeg) * Smooth((u - retractFrom) / Math.Max(1f, r.UnlockMs));
        return angle;
    }

    /// <summary>The yank, 0 → 1 → 0: from the clamp the chain jerks the creature toward the Seeker, then it settles.</summary>
    public static float Yank(ReactionRecipe r, float u)
    {
        var v = u - r.CloseMs;
        if (v <= 0f || v >= r.YankMs) return 0f;
        return v < r.YankPeakMs ? EaseOut(v / r.YankPeakMs) : 1f - Smooth((v - r.YankPeakMs) / (r.YankMs - r.YankPeakMs));
    }

    /// <summary>How far the clamp has been reeled back to the belt, 0 → 1, accelerating (a reel takes up slack, then snaps it home).</summary>
    public static float Retract(ReactionRecipe r, float u, float retractFrom)
    {
        var e = Math.Clamp((u - retractFrom) / Math.Max(1f, r.RetractMs), 0f, 1f);
        return e * e;
    }

    /// <summary>The chain's tension, 0 (the whip's curve) to 1 (taut); reeling in keeps it taut.</summary>
    public static float Tension(ReactionRecipe r, float u) => EaseOut(u / Math.Max(1f, r.TautMs));

    /// <summary>The clamp's and chain's opacity: whole until the last of the retract (or the slack), then gone.</summary>
    public static float Opacity(ReactionRecipe r, float u, float retractFrom, float? slackFrom)
    {
        if (slackFrom is { } s) return 1f - Smooth((u - s) / Math.Max(1f, r.SlackFadeMs));
        var e = Math.Clamp((u - retractFrom) / Math.Max(1f, r.RetractMs), 0f, 1f);
        var fadeFrom = 1f - r.RetractFadeShare;
        return e <= fadeFrom ? 1f : 1f - Smooth((e - fadeFrom) / Math.Max(0.01f, r.RetractFadeShare));
    }

    // ── EACH FRAME ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Advance on the fight's playhead: pin the clamp points, and notice a creature (or the champion) falling.</summary>
    public ReactionStep Update(float playheadMs, IReactionStage stage)
    {
        _origin ??= Math.Max(TriggerMs, playheadMs);
        var u = playheadMs - OriginMs;
        for (var k = 0; k < Targets.Count; k++)
        {
            // THE CLAMP POINT IS READ ONCE, off the pose the creature bites in, and then kept as a share of its body,
            // so the clamp rides the body (its lunge, its bob, the yank) instead of re-sampling a moving silhouette.
            if (_clampShare[k] is null && stage.TryCaughtBody(Targets[k], out var body) && body.Width > 0 && body.Height > 0)
            {
                var share = Recipe.ClampFallback;
                if (stage.TryTargetFrame(Targets[k], out var frame)
                    && SilhouetteProbe.FrontLower(frame, facesLeft: true, Recipe.ProbeFrom, Recipe.ProbeTo, Recipe.ProbeInset) is { } p)
                    share = new Vector2(Math.Clamp((p.X - body.X) / body.Width, 0.04f, 0.6f),
                                        Math.Clamp((p.Y - body.Y) / body.Height, 0.45f, 0.94f));
                _clampShare[k] = share;
            }
            // DEATH IS THE STRONGER STATE: the snap still reads, then the clamp lets go and goes home; no corpse is pulled
            if (_retractFrom is null && _slackFrom is null && stage.TargetFalling(Targets[k]))
                _retractFrom = Math.Max(Recipe.CloseMs + Recipe.DeathHoldMs, u);
        }
        // the bite felled the champion: after the snap his end of the chain goes down, so it slackens and fades in place
        if (_slackFrom is null && stage.ChampionFalling)
            _slackFrom = Math.Max(Recipe.CloseMs + 30f, u);
        var clamped = !_clamped && u >= Recipe.CloseMs;
        if (clamped) _clamped = true;
        var yankEnded = !_yankEnded && u >= Recipe.CloseMs + Recipe.YankMs;
        if (yankEnded) _yankEnded = true;
        var retracting = !_retracting && _slackFrom is null && u >= RetractFromMs;
        if (retracting) _retracting = true;
        return new ReactionStep(clamped, yankEnded, retracting, _clampAt.Length > 0 ? _clampAt[0] : default);
    }

    /// <summary>
    /// The YANK on the creature in <paramref name="slot"/> right now (px, NEGATIVE: toward the champion, who stands to
    /// its left): the chain catches it and pulls. Never on a creature that fell, never once the clamp has let go.
    /// </summary>
    public float YankOffsetX(int slot, float playheadMs, float bodyWidth, bool falling)
    {
        if (falling || _retractFrom is not null || _slackFrom is not null) return 0f;
        var k = IndexOf(slot);
        if (k != 0) return 0f;   // the chain holds the first creature; a second is only bitten
        var px = Math.Clamp(Recipe.YankShare * bodyWidth, Recipe.YankMinPx, Recipe.YankMaxPx);
        return -px * Yank(Recipe, playheadMs - OriginMs);
    }

    private int IndexOf(int slot)
    {
        for (var k = 0; k < Targets.Count; k++) if (Targets[k] == slot) return k;
        return -1;
    }

    // ── THE POSE ─────────────────────────────────────────────────────────────────────────────────

    private readonly record struct ClampPose(Vector2 Bite, Vector2 UpperPivot, Vector2 LowerPivot, Vector2 Eye, float Angle, float Scale, float Jaw);

    private bool Belt(IReactionStage stage, out Vector2 at)
    {
        at = default;
        if (!stage.TryChampionBody(out var champ)) return false;
        at = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        return true;
    }

    /// <summary>Target <paramref name="k"/>'s clamp this frame: where its clamp point is, its line, its size and its arms.</summary>
    private bool Pose(IReactionStage stage, int k, Vector2 belt, float u, out ClampPose pose)
    {
        pose = default;
        if (_clampShare[k] is not { } share || !stage.TryCaughtBody(Targets[k], out var body)) return false;
        var clamp = new Vector2(body.X + share.X * body.Width, body.Y + share.Y * body.Height);
        _clampAt[k] = clamp;
        var length = Math.Clamp(Recipe.ClampBodyShare * body.Height, Recipe.ClampMinPx, Recipe.ClampMaxPx)
                     * ReactionRecipes.SizeDial * (k == 0 ? 1f : 0.8f);
        var scale = length / Recipe.ClampArtLength;
        // the clamp lies along its tether, its arms to the creature, never tipped past MaxTilt
        var toClamp = clamp - belt;
        var max = MathHelper.ToRadians(Recipe.MaxTiltDeg);
        var angle = Math.Clamp(MathF.Atan2(toClamp.Y, Math.Max(1f, toClamp.X)), -max, max);
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var bite = clamp;
        // THE RETRACT: reeled back along its line until its shackle reaches the belt (the first target only; a second
        // clamp has no chain of its own and simply fades with the first)
        if (_slackFrom is null && k == 0)
        {
            var reel = Retract(Recipe, u, RetractFromMs);
            if (reel > 0f)
            {
                var docked = belt + dir * ((Recipe.BitePoint.X - Recipe.Eye.X) * scale);
                bite = Vector2.Lerp(clamp, docked, reel);
            }
        }
        var upper = bite + Rotate((Recipe.UpperPivot - Recipe.BitePoint) * scale, angle);
        var lower = bite + Rotate((Recipe.LowerPivot - Recipe.BitePoint) * scale, angle);
        var eye = bite + Rotate((Recipe.Eye - Recipe.BitePoint) * scale, angle);
        pose = new ClampPose(bite, upper, lower, eye, angle, scale, MathHelper.ToRadians(JawAngle(Recipe, u, RetractFromMs)));
        return true;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The MATERIAL, in the normal alpha batch: the chain, then the two arms, then the housing over their roots. Iron, untinted.</summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        _links = 0;
        _chainCount = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs || !Belt(stage, out var belt)) return;
        BeltAt = belt;
        var alpha = Opacity(Recipe, u, RetractFromMs, _slackFrom);
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!Pose(stage, k, belt, u, out var pose)) continue;
            if (k == 0) DrawChain(b, stage, belt, pose.Eye, u, alpha);
            DrawPart(b, stage, Recipe.LowerKey, pose.LowerPivot, Recipe.LowerPivot, pose, pose.Angle + pose.Jaw, Color.White * alpha);
            DrawPart(b, stage, Recipe.UpperKey, pose.UpperPivot, Recipe.UpperPivot, pose, pose.Angle - pose.Jaw, Color.White * alpha);
            DrawPart(b, stage, Recipe.BaseKey, pose.Bite, Recipe.BitePoint, pose, pose.Angle, Color.White * alpha);
        }
    }

    /// <summary>One rigid part at <paramref name="at"/> (where its canvas point <paramref name="origin"/> lies this frame), turned, at the clamp's one scale.</summary>
    private void DrawPart(SpriteBatch b, IReactionStage stage, string key, Vector2 at, Vector2 origin, ClampPose pose, float rotation, Color colour)
    {
        if (stage.Texture(key) is not { } tex) return;
        b.Draw(tex, at, null, colour, rotation, origin, pose.Scale, SpriteEffects.None, 0f);
        _sprites++;
    }

    /// <summary>The LIGHT, in the additive pass: the teeth's glint and a spark at each hinge at the stop, the tether's streak as it fires.</summary>
    public void DrawLight(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs || !Belt(stage, out var belt)) return;
        var weight = stage.ActionInFocus ? Recipe.LightUnderAction : 1f;
        // THE TETHER FIRES: a Source streak along the chain, brightest at the clamp (it came FROM the belt), gone in ~60 ms
        if (u < Recipe.WhipMs && _chainCount > 1 && stage.Texture(Recipe.StreakKey) is { } streak)
        {
            var fade = 1f - u / Recipe.WhipMs;
            for (var i = 0; i + 1 < _chainCount; i++)
            {
                var a = _chainPts[i];
                var d = _chainPts[i + 1] - a;
                var len = d.Length();
                if (len < 0.5f) continue;
                var along = (i + 1f) / (_chainCount - 1f);   // 0 at the belt, 1 at the clamp
                b.Draw(streak, a, null, VfxBlend.Light(Color.Lerp(Tint, Color.White, 0.3f) * (Recipe.StreakPeak * fade * along * along * weight)),
                       MathF.Atan2(d.Y, d.X), new Vector2(0f, streak.Height / 2f),
                       new Vector2(len / streak.Width, Recipe.StreakPx / streak.Height), SpriteEffects.None, 0f);
                _sprites++;
            }
        }
        var since = u - Recipe.CloseMs;
        if (since < 0f) return;
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!Pose(stage, k, belt, u, out var pose)) continue;
            // the teeth catch the Source on the stop: the brightest thing on screen for a moment, then iron again
            if (since < Recipe.GlintMs)
            {
                var g = VfxBlend.Light(Color.Lerp(Tint, Color.White, 0.45f) * (Recipe.GlintPeak * (1f - since / Recipe.GlintMs) * weight));
                DrawPart(b, stage, Recipe.LowerEdgeKey, pose.LowerPivot, Recipe.LowerPivot, pose, pose.Angle + pose.Jaw, g);
                DrawPart(b, stage, Recipe.UpperEdgeKey, pose.UpperPivot, Recipe.UpperPivot, pose, pose.Angle - pose.Jaw, g);
            }
            // a spark squeezed out of each HINGE as its arm hits the stop: steel met steel at the pins, not on the hide.
            // Thrown forward and outward, off the arm it came from (never a fan round the target)
            if (k == 0 && since < Recipe.SparkMs && stage.Texture(Recipe.SparkKey) is { } spark)
            {
                var v = since / Recipe.SparkMs;
                var length = Recipe.ClampArtLength * pose.Scale;
                for (var s = 0; s < Math.Min(Recipe.Sparks, 2); s++)
                {
                    var side = s == 0 ? -1f : 1f;   // the upper hinge throws up and forward, the lower down and forward
                    var hinge = s == 0 ? pose.UpperPivot : pose.LowerPivot;
                    var a = pose.Angle + side * (0.75f + (ProjectileMotion.Hash01(TriggerMs, s) - 0.5f) * 0.3f);
                    var speed = 0.75f + 0.25f * ProjectileMotion.Hash01(TriggerMs, s + 40);
                    var at = hinge + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (Recipe.SparkReach * length * speed * EaseOut(v))
                             + new Vector2(0f, 0.08f * length * v * v);
                    b.Draw(spark, at, null, VfxBlend.Light(Color.Lerp(Color.White, Tint, 0.35f) * ((1f - v) * weight)), 0f,
                           new Vector2(spark.Width, spark.Height) / 2f, 0.05f * length / spark.Width, SpriteEffects.None, 0f);
                    _sprites++;
                }
            }
        }
    }

    /// <summary>
    /// The chain: a dark metal body along a curve from the belt to the clamp's shackle (the whip's curve going taut, a small
    /// shiver, slack if the champion fell), with a few link accents, dense at its two ends. No gaps, no 64 equal stamps.
    /// </summary>
    private void DrawChain(SpriteBatch b, IReactionStage stage, Vector2 belt, Vector2 eye, float u, float alpha)
    {
        var span = eye - belt;
        var spanLen = span.Length();
        if (spanLen < 4f) return;
        var normal = new Vector2(-span.Y, span.X) / spanLen;
        if (normal.Y < 0f) normal = -normal;   // the curve hangs toward the ground
        var sag = Math.Min(Recipe.WhipSag * spanLen, Recipe.WhipSagMaxPx) * (1f - Tension(Recipe, u));
        if (_slackFrom is { } s) sag += Math.Min(0.18f * spanLen, 60f) * Smooth((u - s) / Math.Max(1f, Recipe.SlackFadeMs));
        var shiver = u > Recipe.TautMs && _slackFrom is null && u < RetractFromMs
            ? Recipe.ShiverPx * MathF.Exp(-(u - Recipe.TautMs) / Recipe.ShiverDecayMs) * MathF.Sin(MathF.Tau * Recipe.ShiverHz * (u - Recipe.TautMs) / 1000f)
            : 0f;
        var control = (belt + eye) / 2f + normal * (sag * 2f + shiver);
        var n = _chainPts.Length;
        for (var i = 0; i < n; i++) _chainPts[i] = Bezier(belt, control, eye, i / (n - 1f));
        _chainCount = n;
        if (stage.Texture(Recipe.ChainBodyKey) is { } body)
            for (var i = 0; i + 1 < n; i++)
            {
                var a = _chainPts[i];
                var d = _chainPts[i + 1] - a;
                var len = d.Length();
                if (len < 0.5f) continue;
                b.Draw(body, a, null, Color.White * alpha, MathF.Atan2(d.Y, d.X), new Vector2(0f, body.Height / 2f),
                       new Vector2((len + 0.6f) / body.Width, Recipe.BodyPx / body.Height), SpriteEffects.None, 0f);
                _sprites++;
            }
        if (stage.Texture(Recipe.ChainLinkKey) is not { } link) return;
        var cellW = link.Width / 2;
        var cellScale = Recipe.LinkPx / cellW;
        // a reeled-in chain is short: it keeps only the accents it has room for, from both ends inward
        var room = (int)(spanLen / (Recipe.LinkPx * 0.9f));
        for (var i = 0; i < Recipe.LinkAt.Count; i++)
        {
            if (room < Recipe.LinkAt.Count && i % 2 == 1 && i > 1 && i < Recipe.LinkAt.Count - 2) continue;
            var f = Recipe.LinkAt[i];
            var at = Bezier(belt, control, eye, f);
            var tangent = BezierTangent(belt, control, eye, f);
            var cell = new Rectangle((i & 1) * cellW, 0, cellW, link.Height);
            b.Draw(link, at, cell, Color.White * alpha, MathF.Atan2(tangent.Y, tangent.X), new Vector2(cellW / 2f, link.Height / 2f),
                   cellScale, SpriteEffects.None, 0f);
            _sprites++;
            _links++;
        }
    }

    private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float u)
        => (1f - u) * (1f - u) * a + 2f * (1f - u) * u * c + u * u * b;

    private static Vector2 BezierTangent(Vector2 a, Vector2 c, Vector2 b, float u)
        => 2f * (1f - u) * (c - a) + 2f * u * (b - c);

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        if (radians == 0f) return v;
        var (s, c) = MathF.SinCos(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
