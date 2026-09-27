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

    /// <summary>The bitten creature's body as DRAWN this frame (the piranha rides it: its lunge, its bob).</summary>
    bool TryCaughtBody(int slot, out Rectangle body);

    /// <summary>The frame the creature in <paramref name="slot"/> was drawn from this frame (its silhouette).</summary>
    bool TryTargetFrame(int slot, out SpriteFrame frame);
}

/// <summary>What one <see cref="ReactionPerformance.Update"/> crossed: the chomp (the answer's frame) and the flash's own frame.</summary>
public readonly record struct ReactionStep(bool Clamped, bool Flashed, Vector2 ClampAt);

/// <summary>
/// ONE REACTION BEING PRESENTED (ADR-011, the REACTION archetype): the Seeker's JAWS answering the creature whose bite
/// set it off, as ONE SHADOW PIRANHA (the production direction, 2026-09-27).
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing. It reads where the bitten creature is each frame, so the piranha stays on its front edge
/// through the creature's own lunge and follow-through. It moves nothing.
/// </para>
/// <para>
/// THE PHRASE, in ms after the first frame that showed the contact. ONE piranha (one tiny source sprite in three
/// states, OPEN / HALF / SHUT) appears with its mouth open in the EMPTY SPACE in front of the creature, about
/// three-quarters of its own width outside the bite point, its whole head against the arena; it moves in over the
/// close (at ~25 still open and approaching, HALF closed at ~45 near contact) and arrives SHUT on the creature's outer
/// front edge at 65, the MAIN CHOMP, with a one-frame squash; the answer (the reflected number, the chomp cue; the
/// creature's reduced flash on or a frame after it) lands there (<see cref="Clamped"/>). It HOLDS shut on the creature
/// ~65 ms, the semantic pose, then recoils a few px away and fades, gone by 200. Nothing else is drawn: no second
/// bite, no smear, no glint. Nothing travels between the Seeker and the creature; the effect is target-local.
/// </para>
/// <para>
/// ON THE FIGHT'S PLAYHEAD. Its time is the playhead minus the first frame that showed the contact (the pump's frame).
/// No clock of its own: a slowed or scrubbed capture shows the same pose for the same moment. It changes no outcome:
/// its targets are the creatures the reflected Strikes hit, each answered on its own body.
/// </para>
/// </remarks>
public sealed class ReactionPerformance
{
    private readonly float?[] _frontShare;     // per target: the front-most column's x share (read once), null = the recipe's
    private readonly Vector2[] _clampAt;       // per target: the bite point last frame (the overlay)
    private float? _origin;                    // the playhead of the first frame that showed the contact
    private bool _clamped, _flashed;
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

    /// <summary>Where each target was bitten last frame (the overlay).</summary>
    public IReadOnlyList<Vector2> ClampAt => _clampAt;

    /// <summary>A presentation of <paramref name="recipe"/> answering the bite at <paramref name="triggerMs"/>.</summary>
    public ReactionPerformance(ReactionRecipe recipe, int skillSlot, int triggerMs, IReadOnlyList<int> targets, Color tint)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        SkillSlot = skillSlot;
        TriggerMs = triggerMs;
        Targets = targets?.ToArray() ?? Array.Empty<int>();
        Tint = tint;
        _frontShare = new float?[Targets.Count];
        _clampAt = new Vector2[Targets.Count];
    }

    /// <summary>The playhead of the first frame (the contact), once seen; the trigger until then.</summary>
    public float OriginMs => _origin ?? TriggerMs;

    /// <summary>Whether the chomp has landed: the moment the screen presents the answer (its number, the cue, a kill's fall).</summary>
    public bool Clamped => _clamped;

    /// <summary>Whether the target's flash frame has come (on the chomp, or the recipe's delay after it).</summary>
    public bool Flashed => _flashed;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => Recipe.EndMs;

    /// <summary>Nothing of it is left to draw.</summary>
    public bool Finished(float playheadMs) => playheadMs - OriginMs >= EndMs;

    /// <summary>Sprites drawn last frame (the trace's cost line).</summary>
    public int SpriteCount => _sprites;

    // ── THE CURVES (pure, so they are tested directly; u = ms after the first frame) ─────────────

    private static float Smooth(float v) { v = Math.Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    private static float EaseOut(float v) { v = Math.Clamp(v, 0f, 1f); return 1f - (1f - v) * (1f - v); }

    /// <summary>The mouth state at <paramref name="u"/>: 0 OPEN (staged and approaching), 1 HALF (from <see cref="ReactionRecipe.HalfAtShare"/> of the close, near contact), 2 SHUT (from the chomp on).</summary>
    public static int Mouth(ReactionRecipe r, float u)
        => u >= r.ChompAtMs ? 2 : u >= r.ChompAtMs * r.HalfAtShare ? 1 : 0;

    /// <summary>
    /// How far along its approach the head is at <paramref name="u"/>: 0 at its staged spawn point in the empty space
    /// (held for the first display frame, so the open head is seen against the arena), 1 on the bite point at the
    /// chomp; a smooth ease so it visibly MOVES toward the target over the close rather than popping.
    /// </summary>
    public static float Approach(ReactionRecipe r, float u)
    {
        if (u <= 0f) return 0f;
        if (u >= r.ChompAtMs) return 1f;
        return Smooth(u / r.ChompAtMs);
    }

    /// <summary>The exit's recoil away from the target, in px: 0 through the chomp and the hold, then easing out to <see cref="ReactionRecipe.RecoilPx"/>.</summary>
    public static float Recoil(ReactionRecipe r, float u)
    {
        var since = u - (r.ChompAtMs + r.HoldMs);
        if (since <= 0f) return 0f;
        return r.RecoilPx * EaseOut(since / Math.Max(1f, r.RecoilMs));
    }

    /// <summary>The head's opacity: whole through the chomp AND the hold, a quick fade after, gone by <see cref="ReactionRecipe.GoneMs"/>.</summary>
    public static float Opacity(ReactionRecipe r, float u)
    {
        if (u < 0f || u >= r.GoneMs) return 0f;
        var fadeFrom = r.ChompAtMs + r.HoldMs;
        if (u < fadeFrom) return 1f;
        return 1f - Smooth((u - fadeFrom) / Math.Max(1f, r.GoneMs - fadeFrom));
    }

    /// <summary>The chomp's squash (x, y scale factors) for the frame after the mouth shuts, then 1.</summary>
    public static Vector2 Squash(ReactionRecipe r, float u)
    {
        var since = u - r.ChompAtMs;
        if (since < 0f || since >= r.SquashMs) return Vector2.One;
        var k = 1f - since / r.SquashMs;
        return new Vector2(1f + 0.12f * k, 1f - 0.10f * k);
    }

    // ── EACH FRAME ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Advance on the fight's playhead: pin each target's front edge once, and notice the chomp and the flash's frame.</summary>
    public ReactionStep Update(float playheadMs, IReactionStage stage)
    {
        _origin ??= Math.Max(TriggerMs, playheadMs);
        var u = playheadMs - OriginMs;
        for (var k = 0; k < Targets.Count; k++)
        {
            // THE FRONT EDGE IS READ ONCE, off the pose the creature bites in, and kept as a share of its body, so the
            // bite point rides the body instead of re-sampling a moving silhouette. The recipe's share stands when the
            // silhouette cannot be read.
            if (_frontShare[k] is null && stage.TryCaughtBody(Targets[k], out var body) && body.Width > 0 && body.Height > 0)
            {
                var share = -1f;   // < 0 = the recipe's own share
                if (stage.TryTargetFrame(Targets[k], out var frame)
                    && SilhouetteProbe.FrontLower(frame, facesLeft: true, Recipe.ProbeFrom, Recipe.ProbeTo, Recipe.ProbeInset) is { } p)
                    share = Math.Clamp((p.X - body.X) / body.Width, 0.02f, 0.5f);
                _frontShare[k] = share;
            }
        }
        // the playhead runs in thirds of a millisecond: the frame drawn AT the chomp reads 64.99 ms, so a half-millisecond
        // of slack keeps the answer on the frame that shows the mouth shut
        var clamped = !_clamped && u >= Recipe.ChompAtMs - 0.5f;
        if (clamped) _clamped = true;
        var flashed = !_flashed && u >= Recipe.ChompAtMs + Recipe.TargetFlashDelayMs - 0.5f;
        if (flashed) _flashed = true;
        return new ReactionStep(clamped, flashed, _clampAt.Length > 0 ? _clampAt[0] : default);
    }

    // ── THE POSE ─────────────────────────────────────────────────────────────────────────────────

    private readonly record struct HeadPose(Vector2 At, float Rotation, Vector2 Scale, float Alpha, int Mouth, bool Flip);

    /// <summary>The piranha on target <paramref name="k"/> this frame, or false when it is not in the world.</summary>
    private bool Pose(IReactionStage stage, int k, float u, out HeadPose pose)
    {
        pose = default;
        if (u < 0f || u >= Recipe.GoneMs || !stage.TryCaughtBody(Targets[k], out var body)) return false;
        var xShare = _frontShare[k] is { } probed && probed >= 0f ? probed : Recipe.BiteShare.X;
        var bite = new Vector2(body.X + xShare * body.Width, body.Y + Recipe.BiteShare.Y * body.Height);
        _clampAt[k] = bite;
        var height = Math.Clamp(Recipe.JawBodyShare * body.Height, Recipe.JawMinPx, Recipe.JawMaxPx) * ReactionRecipes.SizeDial;
        var scale = height / Recipe.ArtHeight;
        var headWidth = Recipe.ArtHeadWidth * scale;
        // STAGED IN NEGATIVE SPACE: the spawn point is a share of the head's own width outside the bite point, on the
        // side the head comes from (the Seeker's); the whole open head is seen against the arena before it overlaps
        var dist = Recipe.SpawnWidthShare * headWidth;
        var along = Approach(Recipe, u) * dist - Recoil(Recipe, u);
        var at = bite - Recipe.From * (dist - along);
        // the art faces LEFT (its mouth at the left edge): arriving from the left it is mirrored to face right, and it
        // turns about the mouth so the mouth points along the approach
        var flip = Recipe.From.X > 0f;
        var rotation = flip ? MathF.Atan2(Recipe.From.Y, Recipe.From.X) : MathF.Atan2(-Recipe.From.Y, -Recipe.From.X);
        pose = new HeadPose(at, rotation, Squash(Recipe, u) * scale, Opacity(Recipe, u), Mouth(Recipe, u), flip);
        return true;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The piranha, in the normal alpha batch after the figures: always foreground, one sprite per target.</summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!Pose(stage, k, u, out var pose)) continue;
            if (stage.Texture(pose.Mouth == 2 ? Recipe.ShutKey : pose.Mouth == 1 ? Recipe.HalfKey : Recipe.OpenKey) is not { } tex) continue;
            b.Draw(tex, pose.At, null, Color.White * pose.Alpha, pose.Rotation, Recipe.MouthPoint, pose.Scale,
                   pose.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            _sprites++;
        }
    }
}
