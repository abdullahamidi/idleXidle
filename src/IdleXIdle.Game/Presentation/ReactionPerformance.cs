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

    /// <summary>The bitten creature's body as DRAWN this frame (the jaws ride it: its lunge, its bob).</summary>
    bool TryCaughtBody(int slot, out Rectangle body);

    /// <summary>The frame the creature in <paramref name="slot"/> was drawn from this frame (its silhouette).</summary>
    bool TryTargetFrame(int slot, out SpriteFrame frame);

    /// <summary>Whether the creature in <paramref name="slot"/> has fallen (its death has begun).</summary>
    bool TargetFalling(int slot);

    /// <summary>Whether the champion has fallen on this bite.</summary>
    bool ChampionFalling { get; }

    /// <summary>Whether an authored action is in its focus (release to contact): the reaction's light then plays secondary.</summary>
    bool ActionInFocus { get; }
}

/// <summary>What one <see cref="ReactionPerformance.Update"/> crossed, for the trace.</summary>
public readonly record struct ReactionStep(bool Clamped, Vector2 ClampAt);

/// <summary>
/// ONE REACTION BEING PRESENTED (ADR-011, the REACTION archetype): the Seeker's JAWS answering the creature whose bite
/// set it off, as SHADOW PIRANHA (the production direction, 2026-09-27).
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing. It reads where the bitten creature is each frame, so the jaws stay on its body through its
/// own lunge and follow-through. It moves nothing: no yank, no chain, no reel.
/// </para>
/// <para>
/// THE PHRASE, in ms after the first frame that showed the contact (the hero-chomp readability pass, 2026-09-27). ONE
/// HERO jaw (one tiny source sprite in three states, OPEN / HALF / SHUT) appears a short way off its bite point on the
/// front of the creature's body, DARTS in with its mouth open (16 ms), stays clearly open for the first display
/// frames, is HALF closed at ~32 and SHUT at 48 (a one-frame squash), then HOLDS shut ~45 ms, the readability pause
/// the eye needs to register "that mouth is shut on the creature", before it recoils a few px and dissolves, gone by
/// 150. The answer (the reflected number, the creature's flash, the main glint, the chomp cue's transient) lands on the
/// hero's CLOSED frame (<see cref="Clamped"/>, 48). Two SMALL secondary bites follow it (0.70 at 30, chomp 70; 0.62 at
/// 50, chomp 90, drawn BEHIND the creature for depth): CHOMP, tick, tick. A faint Shadow smear at each bite point
/// fades by 170; the whole phrase is over by ~180. Nothing travels between the Seeker and the creature: the effect is
/// target-local, as if Shadow itself bites the attacker.
/// </para>
/// <para>
/// ON THE FIGHT'S PLAYHEAD. Its time is the playhead minus the first frame that showed the contact (the pump's frame).
/// No clock of its own: a slowed or scrubbed capture shows the same pose for the same moment. It changes no outcome:
/// its targets are the creatures the reflected Strikes hit, each answered on its own body.
/// </para>
/// </remarks>
public sealed class ReactionPerformance
{
    private readonly Vector2?[] _biteShare;    // per target: the front-most point's x share (read once), null = the recipe's
    private readonly Vector2[] _clampAt;       // per target: the first jaw's bite point last frame (the overlay)
    private readonly bool[] _behindDrawn;      // per target: its behind jaw was drawn this frame
    private float? _origin;                    // the playhead of the first frame that showed the contact
    private bool _clamped;
    private int _sprites;

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

    /// <summary>The Seeker's belt anchor last frame (the anchor overlay names his side of the exchange).</summary>
    public Vector2 BeltAt { get; private set; }

    /// <summary>Where each target's first jaw bit last frame (the overlay).</summary>
    public IReadOnlyList<Vector2> ClampAt => _clampAt;

    /// <summary>A presentation of <paramref name="recipe"/> answering the bite at <paramref name="triggerMs"/>.</summary>
    public ReactionPerformance(ReactionRecipe recipe, int skillSlot, int triggerMs, IReadOnlyList<int> targets, Color tint)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        SkillSlot = skillSlot;
        TriggerMs = triggerMs;
        Targets = targets?.ToArray() ?? Array.Empty<int>();
        Tint = tint;
        _biteShare = new Vector2?[Targets.Count];
        _clampAt = new Vector2[Targets.Count];
        _behindDrawn = new bool[Targets.Count];
    }

    /// <summary>The playhead of the first frame (the contact), once seen; the trigger until then.</summary>
    public float OriginMs => _origin ?? TriggerMs;

    /// <summary>Whether the main chomp has landed: the moment the screen presents the answer (its number, its flash).</summary>
    public bool Clamped => _clamped;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => Recipe.EndMs;

    /// <summary>Nothing of it is left to draw.</summary>
    public bool Finished(float playheadMs) => playheadMs - OriginMs >= EndMs;

    /// <summary>Sprites drawn last frame (the trace's cost line).</summary>
    public int SpriteCount => _sprites;

    // ── THE CURVES (pure, so they are tested directly; v = ms after the JAW's own start) ────────

    private static float Smooth(float v) { v = Math.Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    private static float EaseOut(float v) { v = Math.Clamp(v, 0f, 1f); return 1f - (1f - v) * (1f - v); }

    /// <summary>A jaw's mouth state at <paramref name="v"/> ms after its start: 0 OPEN (the dart and the first display frames), 1 HALF (from <see cref="ReactionRecipe.HalfAtShare"/> of its close), 2 SHUT (from its chomp on).</summary>
    public static int Mouth(ReactionRecipe r, ReactionRecipe.Jaw jaw, float v)
        => v >= jaw.ChompAtMs ? 2 : v >= jaw.ChompAtMs * r.HalfAtShare ? 1 : 0;

    /// <summary>Whether a jaw's mouth is SHUT at <paramref name="v"/> ms after its start.</summary>
    public static bool Shut(ReactionRecipe r, ReactionRecipe.Jaw jaw, float v) => Mouth(r, jaw, v) == 2;

    /// <summary>How far along its approach a jaw is at <paramref name="v"/>: 0 at its spawn point, 1 at the bite point (the dart, eased out).</summary>
    public static float Approach(ReactionRecipe r, float v) => v <= 0f ? 0f : EaseOut(v / Math.Max(1f, r.DartMs));

    /// <summary>
    /// The recoil outward after the jaw's HOLD, in px: 0 through the chomp and the hold (the mouth stays shut on the
    /// creature, the readability pause), then easing out to <see cref="ReactionRecipe.RecoilPx"/> and drifting a
    /// little further through the dissolve.
    /// </summary>
    public static float Recoil(ReactionRecipe r, ReactionRecipe.Jaw jaw, float v)
    {
        var since = v - (jaw.ChompAtMs + jaw.HoldMs);
        if (since <= 0f) return 0f;
        var px = r.RecoilPx * EaseOut(since / Math.Max(1f, r.RecoilMs));
        px += r.RecoilPx * 0.75f * Smooth(since / Math.Max(1f, jaw.GoneMs - jaw.ChompAtMs - jaw.HoldMs));
        return px;
    }

    /// <summary>A jaw's opacity: whole through the chomp AND its hold, dissolving after, gone by its <see cref="ReactionRecipe.Jaw.GoneMs"/>.</summary>
    public static float Opacity(ReactionRecipe r, ReactionRecipe.Jaw jaw, float v)
    {
        if (v < 0f || v >= jaw.GoneMs) return 0f;
        var dissolveFrom = jaw.ChompAtMs + jaw.HoldMs;
        if (v < dissolveFrom) return 1f;
        return 1f - Smooth((v - dissolveFrom) / Math.Max(1f, jaw.GoneMs - dissolveFrom));
    }

    /// <summary>The chomp's squash (x, y scale factors) for the frame after the mouth shuts, then 1.</summary>
    public static Vector2 Squash(ReactionRecipe r, ReactionRecipe.Jaw jaw, float v)
    {
        var since = v - jaw.ChompAtMs;
        if (since < 0f || since >= r.SquashMs) return Vector2.One;
        var k = 1f - since / r.SquashMs;
        return new Vector2(1f + 0.12f * k, 1f - 0.10f * k);
    }

    /// <summary>The residue's strength at a bite point, u ms after the first frame: from that jaw's chomp, fading to nothing by <see cref="ReactionRecipe.ResidueGoneMs"/>.</summary>
    public static float Residue(ReactionRecipe r, ReactionRecipe.Jaw jaw, float u)
    {
        var from = jaw.DelayMs + jaw.ChompAtMs;
        if (u < from || u >= r.ResidueGoneMs) return 0f;
        return r.ResiduePeak * (1f - Smooth((u - from) / Math.Max(1f, r.ResidueGoneMs - from)));
    }

    // ── EACH FRAME ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Advance on the fight's playhead: pin each target's front edge once, and notice the main chomp.</summary>
    public ReactionStep Update(float playheadMs, IReactionStage stage)
    {
        _origin ??= Math.Max(TriggerMs, playheadMs);
        var u = playheadMs - OriginMs;
        for (var k = 0; k < Targets.Count; k++)
        {
            // THE FRONT EDGE IS READ ONCE, off the pose the creature bites in, and kept as a share of its body, so the
            // front jaws ride the body instead of re-sampling a moving silhouette. The recipe's shares stand when the
            // silhouette cannot be read.
            if (_biteShare[k] is null && stage.TryCaughtBody(Targets[k], out var body) && body.Width > 0 && body.Height > 0)
            {
                var share = Vector2.Zero;   // (0, 0) = the recipe's own shares
                if (stage.TryTargetFrame(Targets[k], out var frame)
                    && SilhouetteProbe.FrontLower(frame, facesLeft: true, Recipe.ProbeFrom, Recipe.ProbeTo, Recipe.ProbeInset) is { } p)
                    share = new Vector2(Math.Clamp((p.X - body.X) / body.Width, 0.04f, 0.6f), 0f);
                _biteShare[k] = share;
            }
        }
        // the playhead runs in thirds of a millisecond: the frame drawn AT the chomp reads 24.99 ms, so a half-millisecond
        // of slack keeps the answer on the frame that shows the mouth shut
        var clamped = !_clamped && u >= Recipe.AnswerAtMs - 0.5f;
        if (clamped) _clamped = true;
        return new ReactionStep(clamped, _clampAt.Length > 0 ? _clampAt[0] : default);
    }

    // ── THE POSE ─────────────────────────────────────────────────────────────────────────────────

    private readonly record struct JawPose(Vector2 At, Vector2 Bite, float Rotation, Vector2 Scale, float Alpha, int Mouth, bool Flip, float Height);

    /// <summary>Jaw <paramref name="j"/> on target <paramref name="k"/> this frame, or false when it is not in the world.</summary>
    private bool Pose(IReactionStage stage, int k, int j, float u, out JawPose pose)
    {
        pose = default;
        var jaw = Recipe.Jaws[j];
        var v = u - jaw.DelayMs;
        if (v < 0f || v >= jaw.GoneMs || !stage.TryCaughtBody(Targets[k], out var body)) return false;
        var share = jaw.BiteShare;
        if (_biteShare[k] is { X: > 0f } probed && !jaw.Behind) share = new Vector2(probed.X + (jaw.BiteShare.X - Recipe.Jaws[0].BiteShare.X), share.Y);
        var bite = new Vector2(body.X + share.X * body.Width, body.Y + share.Y * body.Height);
        if (j == 0) _clampAt[k] = bite;
        var height = Math.Clamp(Recipe.JawBodyShare * body.Height, Recipe.JawMinPx, Recipe.JawMaxPx) * ReactionRecipes.SizeDial * jaw.Scale;
        var dist = Recipe.SpawnDistShare * height + Recipe.SpawnDistPx;
        var along = Approach(Recipe, v) * dist - Recoil(Recipe, jaw, v);
        var at = bite - jaw.From * (dist - along);
        // the art faces LEFT (its mouth at the left edge): a jaw arriving from the left is mirrored to face right, and
        // both turn about the mouth so it points along the approach
        var flip = jaw.From.X > 0f;
        var rotation = flip ? MathF.Atan2(jaw.From.Y, jaw.From.X) : MathF.Atan2(-jaw.From.Y, -jaw.From.X);
        var scale = Squash(Recipe, jaw, v) * (height / Recipe.ArtHeight);
        pose = new JawPose(at, bite, rotation, scale, Opacity(Recipe, jaw, v), Mouth(Recipe, jaw, v), flip, height);
        return true;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The BEHIND jaw of the creature in <paramref name="slot"/>: the screen calls this just before it draws that
    /// creature, in the same alpha batch, so the body is drawn over it and the swarm has depth.
    /// </summary>
    public void DrawBehind(SpriteBatch b, IReactionStage stage, float playheadMs, int slot)
    {
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        for (var k = 0; k < Targets.Count; k++)
        {
            if (Targets[k] != slot) continue;
            for (var j = 0; j < Recipe.Jaws.Count; j++)
                if (Recipe.Jaws[j].Behind && Pose(stage, k, j, u, out var pose))
                {
                    DrawJaw(b, stage, pose);
                    _behindDrawn[k] = true;
                }
        }
    }

    /// <summary>The MATERIAL, in the normal alpha batch after the figures: the front jaws, and each bite's Shadow smear.</summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs)
        {
            Array.Clear(_behindDrawn);
            return;
        }
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        var smear = stage.Texture(Recipe.SmearKey);
        for (var k = 0; k < Targets.Count; k++)
        {
            for (var j = 0; j < Recipe.Jaws.Count; j++)
            {
                var jaw = Recipe.Jaws[j];
                if (jaw.Behind)
                {
                    if (_behindDrawn[k]) _sprites++;   // drawn behind its creature this frame: counted, not drawn twice
                    else if (Pose(stage, k, j, u, out var behind)) DrawJaw(b, stage, behind);   // no body to go behind (a fallen one): in front
                    continue;
                }
                if (Pose(stage, k, j, u, out var pose)) DrawJaw(b, stage, pose);
            }
            // the residue: a dark Shadow smear at each bite point, along the approach, for a moment after the chomp
            if (smear is null) continue;
            for (var j = 0; j < Recipe.Jaws.Count; j++)
            {
                var jaw = Recipe.Jaws[j];
                var strength = Residue(Recipe, jaw, u);
                if (strength <= 0f || !stage.TryCaughtBody(Targets[k], out var body)) continue;
                var bite = new Vector2(body.X + jaw.BiteShare.X * body.Width, body.Y + jaw.BiteShare.Y * body.Height);
                var height = Math.Clamp(Recipe.JawBodyShare * body.Height, Recipe.JawMinPx, Recipe.JawMaxPx) * ReactionRecipes.SizeDial * jaw.Scale;
                var len = Recipe.ResidueLengthShare * height;
                var dark = Color.Lerp(new Color(12, 8, 18), Tint, 0.25f) * strength;
                b.Draw(smear, bite, null, dark, MathF.Atan2(-jaw.From.Y, -jaw.From.X), new Vector2(0f, smear.Height / 2f),
                       new Vector2(len / smear.Width, height * 0.35f / smear.Height), SpriteEffects.None, 0f);
                _sprites++;
            }
        }
    }

    /// <summary>One jaw-head: the OPEN or SHUT state, at its place, turned about its mouth, at its size, fading.</summary>
    private void DrawJaw(SpriteBatch b, IReactionStage stage, JawPose pose)
    {
        if (stage.Texture(pose.Mouth == 2 ? Recipe.ShutKey : pose.Mouth == 1 ? Recipe.HalfKey : Recipe.OpenKey) is not { } tex) return;
        b.Draw(tex, pose.At, null, Color.White * pose.Alpha, pose.Rotation, Recipe.MouthPoint, pose.Scale,
               pose.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        _sprites++;
    }

    /// <summary>The LIGHT, in the additive pass: a small Shadow glint at each bite point as its jaw chomps.</summary>
    public void DrawLight(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs || stage.Texture(Recipe.GlintKey) is not { } glint)
        {
            Array.Clear(_behindDrawn);
            return;
        }
        var weight = stage.ActionInFocus ? Recipe.LightUnderAction : 1f;
        for (var k = 0; k < Targets.Count; k++)
            for (var j = 0; j < Recipe.Jaws.Count; j++)
            {
                var jaw = Recipe.Jaws[j];
                var since = u - jaw.DelayMs - jaw.ChompAtMs;
                if (since < 0f || since >= Recipe.GlintMs || !Pose(stage, k, j, u, out var pose)) continue;
                var main = j == 0 ? 1f : 0.35f;   // the HERO's chomp carries the main glint; a secondary's is a third of it
                var g = VfxBlend.Light(Color.Lerp(Tint, Color.White, 0.35f) * (Recipe.GlintPeak * main * (1f - since / Recipe.GlintMs) * weight));
                var size = Recipe.GlintShare * pose.Height;
                b.Draw(glint, pose.Bite, null, g, 0f, new Vector2(glint.Width, glint.Height) / 2f, size / glint.Width, SpriteEffects.None, 0f);
                _sprites++;
            }
        Array.Clear(_behindDrawn);   // the light is the frame's last pass: next frame's creatures draw their behind jaws afresh
    }
}
