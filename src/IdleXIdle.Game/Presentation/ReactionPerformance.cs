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
/// THE PHRASE, in ms after the first frame that showed the contact. TWO LAYERS. A soft SHADOW MIST (two wisps, one
/// pocket per target, centred on the creature's drawn silhouette, in the normal alpha pass UNDER the figures: a pool
/// the creature stands in, never a veil over it) MATERIALISES from a breath (the layer ~0.28 at 15, ~0.47 at 30, 0.70
/// at the snap) while it CONTRACTS onto the creature; FOUR fangs (one hand-authored shape, drawn four times: two from above with their points down,
/// two from below with their points up, each leaning a little toward the body's centre) EMERGE from it OPEN at 0, at
/// ~0.3 opacity, the points clearly OUTSIDE the creature's top and bottom edges, with empty space between the upper
/// fangs, the body and the lower fangs. The open pose stands to ~25 (the fangs ~0.7), then the fangs close RAPIDLY
/// inward (upper down, lower up) and SNAP at 55, whole and crisp, onto the creature's outer silhouette, the points a
/// little inside its edges and the body still between them (<see cref="Snapped"/>: the cue's transient, a kill's
/// fall), the mist at its densest. They HOLD there ~85 ms, the semantic pose, TARGET BETWEEN SHADOW FANGS; the
/// number lands 40 ms into the hold (<see cref="Answered"/>); the mist stays compressed ~35 ms (the pressure), then
/// loosens outward. At the release the fangs retract a few px and fade over ~45 ms while the mist keeps expanding and
/// evaporates evenly behind them, gone by 200: fangs disappearing, faint mist remaining, mist gone. Nothing else is drawn: no
/// particles, no glint, no trail, no residue, no flash, no additive light.
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
    private int _sprites, _mistSprites;

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

    /// <summary>
    /// The fangs' opacity: they EMERGE (<see cref="ReactionRecipe.FangOpacityAtSpawn"/> on the first frame,
    /// <see cref="ReactionRecipe.FangOpacityAtOpen"/> when the open pose ends), are whole from the snap through the
    /// hold, and fade over <see cref="ReactionRecipe.FangFadeMs"/> from the release, before the mist is gone.
    /// </summary>
    public static float FangOpacity(ReactionRecipe r, float u)
    {
        if (u < 0f || u >= r.GoneMs) return 0f;
        if (u < r.OpenMs) return MathHelper.Lerp(r.FangOpacityAtSpawn, r.FangOpacityAtOpen, u / Math.Max(1f, r.OpenMs));
        if (u < r.SnapAtMs) return MathHelper.Lerp(r.FangOpacityAtOpen, 1f, (u - r.OpenMs) / Math.Max(1f, r.SnapAtMs - r.OpenMs));
        if (u < r.ReleaseAtMs) return 1f;
        return 1f - Smooth((u - r.ReleaseAtMs) / Math.Max(1f, Math.Min(r.FangFadeMs, r.GoneMs - r.ReleaseAtMs)));
    }

    /// <summary>
    /// The mist layer's opacity (a multiplier on the wisps' own feathered alpha): it MATERIALISES from a breath
    /// (<see cref="ReactionRecipe.MistAtSpawn"/>) to <see cref="ReactionRecipe.MistPeak"/> at the snap (an ease-out), stays
    /// dense <see cref="ReactionRecipe.MistDenseMs"/>, thins by <see cref="ReactionRecipe.MistHoldThinning"/> as it loosens
    /// through the rest of the hold, then evaporates linearly to 0 at <see cref="ReactionRecipe.GoneMs"/>, outlasting the fangs.
    /// </summary>
    public static float MistOpacity(ReactionRecipe r, float u)
    {
        if (u < 0f || u >= r.GoneMs) return 0f;
        if (u < r.SnapAtMs)
            return r.MistAtSpawn + (r.MistPeak - r.MistAtSpawn) * (1f - MathF.Pow(1f - u / r.SnapAtMs, r.MistRisePower));
        var loosenAt = LoosenAt(r);
        if (u < loosenAt) return r.MistPeak;
        if (u < r.ReleaseAtMs)
            return r.MistPeak * (1f - r.MistHoldThinning * Smooth((u - loosenAt) / Math.Max(1f, r.ReleaseAtMs - loosenAt)));
        return r.MistPeak * (1f - r.MistHoldThinning) * Math.Clamp(1f - (u - r.ReleaseAtMs) / Math.Max(1f, r.GoneMs - r.ReleaseAtMs), 0f, 1f);
    }

    /// <summary>When the mist begins to loosen: the dense part of the hold, never past the release (a short hold cannot make the curve jump).</summary>
    private static float LoosenAt(ReactionRecipe r) => Math.Min(r.SnapAtMs + r.MistDenseMs, r.ReleaseAtMs);

    /// <summary>
    /// The mist's scale against its size at the snap: OPEN it is <see cref="ReactionRecipe.MistOpenSpread"/> wider and
    /// looser and it CONTRACTS onto the creature as the fangs close (1 at the snap); compressed through the dense part
    /// of the hold; then it loosens outward along one smooth curve, <see cref="ReactionRecipe.MistLoosenGrow"/> larger by
    /// the time it is gone (never a pulse, never a jump into a puff at the release).
    /// </summary>
    public static Vector2 MistScale(ReactionRecipe r, float u)
    {
        var open = r.MistOpenSpread * (1f - Smooth(u / Math.Max(1f, r.SnapAtMs)));
        var loosenAt = LoosenAt(r);
        var grow = u <= loosenAt ? 0f : r.MistLoosenGrow * Smooth((u - loosenAt) / Math.Max(1f, r.GoneMs - loosenAt));
        var scale = 1f + open + grow;
        return new Vector2(scale, scale);
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
        if (u < 0f || u >= Recipe.GoneMs || !TrySilhouette(stage, k, out var body)) return false;
        var centre = new Vector2(body.X + body.Width * 0.5f, body.Y + body.Height * 0.5f);
        _snapAt[k] = centre;
        var height = Math.Clamp(Recipe.FangHeightShare * body.Height, Recipe.FangMinPx, Recipe.FangMaxPx) * ReactionRecipes.SizeDial;
        var scale = height / Recipe.ArtHeight;
        var spread = Recipe.PairSpreadShare * height;
        var release = Release(Recipe, u);
        var alpha = FangOpacity(Recipe, u);
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

    /// <inheritdoc cref="TrySilhouette(IReactionStage, int, out Rectangle, out Rectangle)"/>
    private bool TrySilhouette(IReactionStage stage, int k, out Rectangle body) => TrySilhouette(stage, k, out body, out _);

    /// <summary>
    /// One wisp of the mist pocket on target <paramref name="k"/> (<paramref name="second"/>: the looser wisp), centred a
    /// little above its drawn silhouette's centre, sized to one creature, drifting a few px.
    /// </summary>
    private void DrawMist(SpriteBatch b, IReactionStage stage, int k, float u, Texture2D tex, bool second)
    {
        var alpha = MistOpacity(Recipe, u) * (second ? Recipe.WispOpacity : 1f);
        if (alpha <= 0.004f || !TrySilhouette(stage, k, out var body, out var layout)) return;
        // the body's own height (the fangs sit on its edges); across, one creature (never a lunge's canvas)
        var height = body.Height * ReactionRecipes.SizeDial;
        var width = Math.Min(body.Width, layout.Width) * ReactionRecipes.SizeDial;
        var centre = new Vector2(body.X + body.Width * 0.5f, body.Y + body.Height * (0.5f - Recipe.MistRaiseShare));
        var s = MistScale(Recipe, u);
        var along = Math.Clamp(u / Math.Max(1f, Recipe.GoneMs), 0f, 1f);
        var turn = MathHelper.ToRadians(Recipe.MistTurnDegrees);
        var size = new Vector2(width * Recipe.MistWidthShare * s.X, height * Recipe.MistHeightShare * s.Y);
        if (second)
        {
            size *= Recipe.WispScale;
            centre += new Vector2(Recipe.WispOffsetShare.X * width + Recipe.MistDriftPx * along, Recipe.WispOffsetShare.Y * height);
        }
        else centre.X -= Recipe.MistDriftPx * along;
        b.Draw(tex, centre, null, Color.White * alpha, second ? turn * (1f - along) : turn * along,
               new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), new Vector2(size.X / tex.Width, size.Y / tex.Height),
               second ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        _mistSprites++;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The MIST, in the normal alpha batch BEFORE any creature is drawn: every target's pocket (two wisps), a Shadow pool
    /// the bitten creature stands in, so the fog never veils it and its eyes and rim stay exactly as they were. Never
    /// additive.
    /// </summary>
    public void DrawUnder(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _mistSprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs || stage.Texture(Recipe.MistKey) is not { } mist) return;
        // every target's main pocket, then every target's second wisp: two texture switches however many targets
        for (var k = 0; k < Targets.Count; k++) DrawMist(b, stage, k, u, mist, second: false);
        if (stage.Texture(Recipe.WispKey) is not { } wisp) return;
        for (var k = 0; k < Targets.Count; k++) DrawMist(b, stage, k, u, wisp, second: true);
    }

    /// <summary>
    /// The FANGS, in the normal alpha batch after the figures (and so over the mist): four sprites per target while
    /// they are visible, none once they have faded (the last frames are the mist alone). Never additive.
    /// </summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = _mistSprites;
        _mistSprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        if (FangOpacity(Recipe, u) < ReactionRecipe.FangVisibleFloor) return;
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
