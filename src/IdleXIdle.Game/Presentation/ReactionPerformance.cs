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
    /// The caught creature's body as DRAWN this frame, recoil included (the jaws ride it). An action's
    /// <see cref="IActionStage.TryTargetBody"/> is the creature's own place, which a reaction never moves.
    /// </summary>
    bool TryCaughtBody(int slot, out Rectangle body);

    /// <summary>The frame the creature in <paramref name="slot"/> was drawn from this frame (its silhouette).</summary>
    bool TryTargetFrame(int slot, out SpriteFrame frame);

    /// <summary>Whether the creature in <paramref name="slot"/> has fallen (its death has begun).</summary>
    bool TargetFalling(int slot);

    /// <summary>Whether the champion has fallen: the chain's own end is going down, so the jaws let go after the snap.</summary>
    bool ChampionFalling { get; }

    /// <summary>Whether an authored action is in its focus (release to contact): the reaction's light then plays secondary.</summary>
    bool ActionInFocus { get; }
}

/// <summary>What one <see cref="ReactionPerformance.Update"/> crossed, for the trace.</summary>
public readonly record struct ReactionStep(bool Snapped, bool RecoilEnded, bool Released, Vector2 SnapAt);

/// <summary>
/// ONE REACTION BEING PRESENTED (ADR-011, REACTION / TRAP): JAWS biting back at the creature whose bite set it off.
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing (idle, a swing, SPRAY, a HARD HANDS leap). It reads where his body IS each frame, so the chain's
/// end rides his belt through a leap; and where the caught creature is, so the jaws stay on it through its recoil.
/// </para>
/// <para>
/// ON THE FIGHT'S PLAYHEAD. Everything is a function of the time since the contact (<see cref="TriggerMs"/>, the bite's
/// millisecond, which is the enemy row's own contact frame): no clock of its own, so a slowed or scrubbed capture shows
/// the same frame for the same moment. It changes no outcome: its targets are the creatures the fight's reflected
/// Strikes landed on, and the recoil is where a creature is DRAWN.
/// </para>
/// </remarks>
public sealed class ReactionPerformance
{
    private const int CurveSamples = 24;
    private readonly float[] _curveLen = new float[CurveSamples + 1];   // cumulative arc length, reused every frame
    private readonly Vector2?[] _clampShare;                            // per target: the clamp point, as a share of its body
    private float? _releaseFrom;                                        // the release, once a caught creature fell
    private bool _snapped, _recoilEnded, _released;
    private int _sprites, _links;

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
    private readonly Vector2[] _clampAt;

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
        _linkAt = new Vector2[recipe.MaxLinks];   // the chain's bound, allocated once per trigger, never per frame
    }

    /// <summary>When the release begins, in ms after the contact (earlier when a caught creature fell).</summary>
    public float ReleaseFromMs => _releaseFrom ?? Recipe.ReleaseAtMs;

    /// <summary>How long the reaction is in the world, in ms after the contact.</summary>
    public float EndMs => ReleaseFromMs + Recipe.ReleaseMs;

    /// <summary>Nothing of it is left to draw.</summary>
    public bool Finished(float playheadMs) => playheadMs - TriggerMs >= EndMs;

    /// <summary>Sprites and chain links drawn last frame (the trace's cost line).</summary>
    public int SpriteCount => _sprites;

    /// <inheritdoc cref="SpriteCount"/>
    public int ChainLinks => _links;

    // ── THE CURVES (pure, so they are tested directly) ───────────────────────────────────────────

    private static float Smooth(float u) { u = Math.Clamp(u, 0f, 1f); return u * u * (3f - 2f * u); }
    private static float EaseOut(float u) { u = Math.Clamp(u, 0f, 1f); return 1f - (1f - u) * (1f - u); }

    /// <summary>The snap, 0 (open, below the clamp point) to 1 (shut, on it), at <paramref name="t"/> ms after the contact.</summary>
    public static float Snap(ReactionRecipe r, float t) => t <= 0f ? 0f : EaseOut(t / Math.Max(1f, r.SnapMs));

    /// <summary>Whether the shut jaws are drawn at <paramref name="t"/> (before the release loosens them).</summary>
    public static bool Shut(ReactionRecipe r, float t, float releaseFrom) => t >= r.SnapMs * r.ShutAt && t < releaseFrom;

    /// <summary>The chain's tension, 0 (slack) to 1 (taut), at <paramref name="t"/>; it slackens again through the release.</summary>
    public static float Tension(ReactionRecipe r, float t, float releaseFrom)
    {
        var tight = EaseOut(t / Math.Max(1f, r.TautMs));
        return t < releaseFrom ? tight : tight * (1f - Smooth((t - releaseFrom) / Math.Max(1f, r.ReleaseMs * 0.6f)));
    }

    /// <summary>The recoil, 0 to 1 and back: out fast from the snap, peaking, then eased home.</summary>
    public static float Recoil(ReactionRecipe r, float t)
    {
        var u = (t - r.SnapMs) / Math.Max(1f, r.RecoilMs);
        if (u <= 0f || u >= 1f) return 0f;
        return u < r.RecoilPeakAt ? EaseOut(u / r.RecoilPeakAt) : 1f - Smooth((u - r.RecoilPeakAt) / (1f - r.RecoilPeakAt));
    }

    /// <summary>The material's opacity: whole until the release, then gone over it.</summary>
    public static float Opacity(ReactionRecipe r, float t, float releaseFrom)
        => t < releaseFrom ? 1f : 1f - Smooth((t - releaseFrom) / Math.Max(1f, r.ReleaseMs));

    /// <summary>How many links a chain of <paramref name="length"/> px draws, and how long each is: bounded, never gapped.</summary>
    public static (int Count, float LinkPx) LinksFor(ReactionRecipe r, float length)
    {
        var step = r.LinkPx * (1f - r.LinkOverlap);
        var count = (int)MathF.Ceiling(Math.Max(0f, length) / Math.Max(1f, step));
        if (count <= r.MaxLinks) return (Math.Max(count, 1), r.LinkPx);
        return (r.MaxLinks, length / (r.MaxLinks * (1f - r.LinkOverlap)));
    }

    // ── EACH FRAME ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Advance on the fight's playhead: pin the clamp points, and notice a caught creature falling.</summary>
    public ReactionStep Update(float playheadMs, IReactionStage stage)
    {
        var t = playheadMs - TriggerMs;
        for (var k = 0; k < Targets.Count; k++)
        {
            // THE CLAMP POINT IS READ ONCE, off the pose the creature bites in, and then kept as a share of its body,
            // so the jaws ride the body (its lunge, its bob, the recoil) instead of re-sampling a moving silhouette.
            if (_clampShare[k] is null && stage.TryCaughtBody(Targets[k], out var body) && body.Width > 0 && body.Height > 0)
            {
                var share = Recipe.ClampFallback;
                if (stage.TryTargetFrame(Targets[k], out var frame)
                    && SilhouetteProbe.FrontLower(frame, facesLeft: true, Recipe.ProbeFrom, Recipe.ProbeTo, Recipe.ProbeInset) is { } p)
                    share = new Vector2(Math.Clamp((p.X - body.X) / body.Width, 0.04f, 0.6f),
                                        Math.Clamp((p.Y - body.Y) / body.Height, 0.40f, 0.94f));
                _clampShare[k] = share;
            }
            // DEATH IS THE STRONGER STATE: the jaws let go as the fall begins (never a corpse dragged against its clip)
            if (_releaseFrom is null && stage.TargetFalling(Targets[k]))
                _releaseFrom = Math.Max(Recipe.SnapMs + Recipe.DeathReleaseAfterSnapMs, t);
        }
        // ...and so it is when the bite that set it off felled the champion: bite, the snap, then his fall
        if (_releaseFrom is null && stage.ChampionFalling)
            _releaseFrom = Math.Max(Recipe.SnapMs + Recipe.DeathReleaseAfterSnapMs, t);
        var releaseFrom = ReleaseFromMs;
        var snapped = !_snapped && t >= Recipe.SnapMs * Recipe.ShutAt;
        if (snapped) _snapped = true;
        var recoilEnded = !_recoilEnded && t >= Recipe.SnapMs + Recipe.RecoilMs;
        if (recoilEnded) _recoilEnded = true;
        var released = !_released && t >= releaseFrom;
        if (released) _released = true;
        return new ReactionStep(snapped, recoilEnded, released, _clampAt.Length > 0 ? _clampAt[0] : default);
    }

    /// <summary>
    /// How far the creature in <paramref name="slot"/> is pushed away from the champion right now (+x, px): the recoil,
    /// never on a creature that fell (the fall owns it) and never after the jaws let go.
    /// </summary>
    public float RecoilOffsetX(int slot, float playheadMs, float bodyWidth, bool falling)
    {
        if (falling || _releaseFrom is not null) return 0f;
        var k = IndexOf(slot);
        if (k < 0) return 0f;
        var t = playheadMs - TriggerMs;
        var px = Math.Clamp(Recipe.RecoilShare * bodyWidth, Recipe.RecoilMinPx, Recipe.RecoilMaxPx) * (k == 0 ? 1f : 0.6f);
        return px * Recoil(Recipe, t);
    }

    private int IndexOf(int slot)
    {
        for (var k = 0; k < Targets.Count; k++) if (Targets[k] == slot) return k;
        return -1;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The jaws' on-screen height on a creature of this visible height.</summary>
    private float JawsHeight(Rectangle body) => Math.Clamp(Recipe.JawsBodyShare * body.Height, Recipe.JawsMinPx, Recipe.JawsMaxPx);

    /// <summary>Where the clamp point of target <paramref name="k"/> is this frame, and its jaws' height; false if unknown.</summary>
    private bool Clamp(IReactionStage stage, int k, out Vector2 at, out float height)
    {
        at = default;
        height = 0f;
        if (_clampShare[k] is not { } share || !stage.TryCaughtBody(Targets[k], out var body)) return false;
        at = new Vector2(body.X + share.X * body.Width, body.Y + share.Y * body.Height);
        height = JawsHeight(body) * (k == 0 ? 1f : 0.72f);   // a second target: smaller jaws, and no chain
        return true;
    }

    private bool Belt(IReactionStage stage, out Vector2 at)
    {
        at = default;
        if (!stage.TryChampionBody(out var champ)) return false;
        at = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        return true;
    }

    /// <summary>The MATERIAL, in the normal alpha batch: the chain, then the jaws over it. Iron, untinted.</summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        _links = 0;
        var t = playheadMs - TriggerMs;
        if (t < 0f || t >= EndMs) return;
        var releaseFrom = ReleaseFromMs;
        var alpha = Opacity(Recipe, t, releaseFrom);
        if (Belt(stage, out var belt)) BeltAt = belt;
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!Clamp(stage, k, out var clamp, out var h)) continue;
            _clampAt[k] = clamp;
            var scale = h / Recipe.JawsArtHeight;
            // the jaws' pose: rising open → shut with an overshoot → loosening and dropping as they let go
            var rise = (1f - Snap(Recipe, t)) * Recipe.RiseFrom * h;
            var shut = Shut(Recipe, t, releaseFrom);
            var settle = shut ? 1f + (Recipe.SnapOvershoot - 1f) * (1f - EaseOut((t - Recipe.SnapMs * Recipe.ShutAt) / Recipe.SettleMs)) : 1f;
            var drop = 0f;
            var tilt = 0f;
            if (t >= releaseFrom)
            {
                var u = Math.Clamp((t - releaseFrom) / Recipe.ReleaseMs, 0f, 1f);
                drop = 0.22f * h * u * u;
                tilt = MathHelper.ToRadians(10f) * u;
            }
            var jawScale = scale * settle * (shut ? 1f : 0.82f + 0.18f * Snap(Recipe, t));
            var jawsAt = clamp + new Vector2(0f, rise + drop);
            // THE CHAIN, from the belt to the jaws' eye (the first target only): drawn first, so the jaws sit over its end
            if (k == 0 && belt != default)
            {
                var eye = jawsAt + Rotate((Recipe.ChainEye - Recipe.JawsPivot) * jawScale, tilt);
                DrawChain(b, stage, belt, eye, t, releaseFrom, alpha);
            }
            var key = shut ? Recipe.JawsShutKey : Recipe.JawsOpenKey;
            if (stage.Texture(key) is { } jaws)
            {
                b.Draw(jaws, jawsAt, null, Color.White * alpha, tilt, Recipe.JawsPivot, jawScale, SpriteEffects.None, 0f);
                _sprites++;
            }
        }
    }

    /// <summary>The LIGHT, in the additive pass: the tooth glint, the snap flash, the sparks, the tension accent.</summary>
    public void DrawLight(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        var t = playheadMs - TriggerMs;
        if (t < 0f || t >= EndMs) return;
        var releaseFrom = ReleaseFromMs;
        var weight = stage.ActionInFocus ? Recipe.LightUnderAction : 1f;
        var sinceSnap = t - Recipe.SnapMs * Recipe.ShutAt;
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!Clamp(stage, k, out var clamp, out var h)) continue;
            var scale = h / Recipe.JawsArtHeight;
            var settle = 1f + (Recipe.SnapOvershoot - 1f) * (1f - EaseOut(sinceSnap / Recipe.SettleMs));
            var bite = clamp + (Recipe.BiteLine - Recipe.JawsPivot) * scale * settle;
            var hot = Color.Lerp(Tint, Color.White, Recipe.FlashWhite);
            // the tooth-edge glint: the teeth themselves catch the Source at the snap
            if (sinceSnap >= 0f && sinceSnap < Recipe.GlintMs && t < releaseFrom && stage.Texture(Recipe.JawsEdgeKey) is { } edge)
            {
                var g = Recipe.GlintPeak * (1f - sinceSnap / Recipe.GlintMs) * weight;
                b.Draw(edge, clamp, null, VfxBlend.Light(Color.Lerp(Tint, Color.White, 0.35f) * g), 0f, Recipe.JawsPivot,
                       scale * settle, SpriteEffects.None, 0f);
                _sprites++;
            }
            if (k > 0) continue;   // one flash, one burst of sparks, one accent: the second target's jaws only glint
            // the snap flash: small, flattened across the bite line, gone in a few frames
            if (sinceSnap >= 0f && sinceSnap < Recipe.FlashMs && stage.Texture(Recipe.FlashKey) is { } flash)
            {
                var f = 1f - sinceSnap / Recipe.FlashMs;
                var size = Recipe.FlashSize * h;
                b.Draw(flash, bite, null, VfxBlend.Light(hot * (f * f * weight)), 0f, new Vector2(flash.Width, flash.Height) / 2f,
                       new Vector2(size / flash.Width, size * 0.5f / flash.Height), SpriteEffects.None, 0f);
                _sprites++;
            }
            // sparks off the bite line, up and out, falling as they slow
            if (sinceSnap >= 0f && sinceSnap < Recipe.SparkMs && stage.Texture(Recipe.SparkKey) is { } spark)
            {
                var u = sinceSnap / Recipe.SparkMs;
                for (var s = 0; s < Recipe.Sparks; s++)
                {
                    var a = MathHelper.ToRadians(-90f + (s - (Recipe.Sparks - 1) / 2f) * 32f + (ProjectileMotion.Hash01(TriggerMs, s) - 0.5f) * 18f);
                    var speed = 0.65f + 0.35f * ProjectileMotion.Hash01(TriggerMs, s + 40);
                    var at = bite + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (Recipe.SparkReach * h * speed * EaseOut(u))
                                  + new Vector2(0f, 0.35f * h * u * u);
                    b.Draw(spark, at, null, VfxBlend.Light(Color.Lerp(Color.White, Tint, 0.4f) * ((1f - u) * weight)), 0f,
                           new Vector2(spark.Width, spark.Height) / 2f, 0.07f * h / spark.Width, SpriteEffects.None, 0f);
                    _sprites++;
                }
            }
        }
        // the tension accent: a restrained Source line along the chain while it takes the strain
        if (Targets.Count > 0 && _links > 1 && stage.Texture(Recipe.AccentKey) is { } accent)
        {
            var strain = Tension(Recipe, t, releaseFrom) * (1f - Smooth((t - Recipe.TautMs) / 160f));
            if (strain > 0.02f)
                for (var i = 0; i + 1 < _linkCount; i++)
                {
                    var a = _linkAt[i];
                    var c = _linkAt[i + 1];
                    var d = c - a;
                    var len = d.Length();
                    if (len < 0.5f) continue;
                    b.Draw(accent, a, null, VfxBlend.Light(Tint * (Recipe.AccentPeak * strain * weight)), MathF.Atan2(d.Y, d.X),
                           new Vector2(0f, accent.Height / 2f), new Vector2(len / accent.Width, Recipe.AccentPx / accent.Height),
                           SpriteEffects.None, 0f);
                    _sprites++;
                }
        }
    }

    // the chain's link centres, as last drawn (the accent follows them); preallocated to the recipe's bound
    private readonly Vector2[] _linkAt;
    private int _linkCount;

    /// <summary>
    /// The chain: a curve from the belt to the jaws' eye whose sag falls from slack to taut, shivers, and returns as it
    /// lets go; links placed at equal arc length, alternating face-on and edge-on. It whips out from the jaws first.
    /// </summary>
    private void DrawChain(SpriteBatch b, IReactionStage stage, Vector2 belt, Vector2 eye, float t, float releaseFrom, float alpha)
    {
        if (stage.Texture(Recipe.ChainLinkKey) is not { } link) return;
        var span = eye - belt;
        var spanLen = span.Length();
        if (spanLen < 4f) return;
        var normal = new Vector2(-span.Y, span.X) / spanLen;
        if (normal.Y < 0f) normal = -normal;   // "down" side: sag hangs toward the ground
        var tension = Tension(Recipe, t, releaseFrom);
        var sag = Math.Min(Recipe.SlackSag * spanLen, Recipe.SagMaxPx) * (1f - tension);
        var shiver = t > Recipe.TautMs && t < releaseFrom
            ? Recipe.ShiverPx * MathF.Exp(-(t - Recipe.TautMs) / Recipe.ShiverDecayMs) * MathF.Sin(MathF.Tau * Recipe.ShiverHz * (t - Recipe.TautMs) / 1000f)
            : 0f;
        var control = (belt + eye) / 2f + normal * (sag * 2f + shiver);   // a quadratic's control point: twice the sag
        // arc length table
        _curveLen[0] = 0f;
        var prev = belt;
        for (var i = 1; i <= CurveSamples; i++)
        {
            var p = Bezier(belt, control, eye, i / (float)CurveSamples);
            _curveLen[i] = _curveLen[i - 1] + Vector2.Distance(prev, p);
            prev = p;
        }
        var total = _curveLen[CurveSamples];
        var (count, linkPx) = LinksFor(Recipe, total);
        var whip = EaseOut(t / Math.Max(1f, Recipe.WhipMs));   // how much of the chain (from the jaws' end) is out
        var cellW = link.Width / 2;
        var cellScale = linkPx / cellW;
        _linkCount = 0;
        for (var n = 0; n < count; n++)
        {
            // from the jaws' end toward the belt
            var s = total * (1f - (n + 0.5f) / count);
            if (1f - s / total > whip) break;
            var u = ArcToParam(s);
            var at = Bezier(belt, control, eye, u);
            var tangent = BezierTangent(belt, control, eye, u);
            var angle = MathF.Atan2(tangent.Y, tangent.X);
            var cell = new Rectangle((n & 1) * cellW, 0, cellW, link.Height);
            b.Draw(link, at, cell, Color.White * alpha, angle, new Vector2(cellW / 2f, link.Height / 2f), cellScale, SpriteEffects.None, 0f);
            _linkAt[_linkCount++] = at;
            _sprites++;
        }
        _links = _linkCount;
    }

    private float ArcToParam(float s)
    {
        for (var i = 1; i <= CurveSamples; i++)
        {
            if (_curveLen[i] < s) continue;
            var seg = _curveLen[i] - _curveLen[i - 1];
            var f = seg <= 0f ? 0f : (s - _curveLen[i - 1]) / seg;
            return (i - 1 + f) / CurveSamples;
        }
        return 1f;
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
