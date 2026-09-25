using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// ONE MELEE ACTION BEING PERFORMED (ADR-011): the champion's authored clip, carried to its target by presentation
/// root motion, landing its blow ON THE BEAT, with an impact that begins where the blow lands and follows its force.
/// </summary>
/// <remarks>
/// <para>
/// It runs on the fight's playhead like <see cref="ActionPerformance"/>, so the fist, the impact, the flash, the
/// health, the number and the sound meet on one frame. It changes no outcome and moves nothing in the fight: the
/// targets are the ones the fight resolved, and the lunge is where the figure is DRAWN.
/// </para>
/// <para>
/// THE LUNGE. At the clip's start the distance is measured from the fist's authored socket on the contact frame to
/// the target's contact point. The body then eases back a little through the anticipation, crosses the whole
/// distance during the commit (accelerating into the hit, so the fastest spacing is at contact), overshoots a
/// little in the follow-through, and travels home in the first part of the recovery. The curve is read against the
/// timing the figure is actually playing, so a handoff that compresses the recovery also brings the body home sooner.
/// </para>
/// </remarks>
public sealed class MeleePerformance : IActionPerformance
{
    private float _reach;              // how far the body travels to put the fist on the target (arena px, +x)
    private ActionClipTiming _played;  // the timing the clip actually played (a handoff may compress or cut it)
    private float? _returnFrom;        // a yield: the way home starts here (ms into the clip), not at the recovery
    private float _returnMs;           // ...and takes this long
    private bool _planned;
    private Vector2 _fistAtContact;    // where the fist lands (arena), once planned
    private float _forceDegrees = 40f; // the blow's direction at contact (screen degrees, 0 = right, +90 = down)
    private readonly List<Impact> _impacts = new();
    private int _sprites;

    private sealed class Impact
    {
        public Vector2 At;
        public float Scale = 1f, Age, Degrees;
        public bool Primary;
        public (Vector2 Dir, float Speed, float Spin)[] Shards = Array.Empty<(Vector2, float, float)>();
        public (Vector2 Dir, float Speed, float Spin)[] Chips = Array.Empty<(Vector2, float, float)>();
        public (Vector2 Dir, float Speed)[] Sparks = Array.Empty<(Vector2, float)>();
    }

    /// <summary>The recipe being performed.</summary>
    public MeleeActionRecipe Recipe { get; }

    IActionRecipe IActionPerformance.Recipe => Recipe;

    /// <inheritdoc/>
    public ActionClipTiming Timing { get; }

    /// <inheritdoc/>
    public float BeatMs { get; }

    /// <inheritdoc/>
    public float ClipStartMs { get; }

    /// <summary>When the body launches (the commit marker), in playhead ms.</summary>
    public float ReleaseMs => ClipStartMs + Timing.MarkerMs(Recipe.CommitMarker);

    /// <inheritdoc/>
    public int SkillSlot { get; }

    /// <inheritdoc/>
    public IReadOnlyList<int> Targets { get; }

    /// <summary>The cast's Source colour: the light in the impact.</summary>
    public Color Tint { get; }

    /// <summary>True once the body has launched (the commit).</summary>
    public bool Released { get; private set; }

    /// <summary>True once the blow has landed.</summary>
    public bool Contacted { get; private set; }

    /// <summary>How far the lunge carries the body to reach its target (arena px), once planned.</summary>
    public float Reach => _reach;

    /// <summary>A performance of <paramref name="recipe"/> whose contact is the beat at <paramref name="beatMs"/>.</summary>
    public MeleePerformance(MeleeActionRecipe recipe, ActionClipTiming timing, float beatMs, float clipStartMs,
                            int skillSlot, IReadOnlyList<int> targets, Color tint)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        foreach (var m in new[] { recipe.CommitMarker, recipe.ContactMarker, recipe.RecoveryMarker })
            if (!timing.HasMarker(m)) throw new ArgumentException($"A melee clip needs a '{m}' marker.", nameof(timing));
        BeatMs = beatMs;
        ClipStartMs = clipStartMs;
        _played = timing;
        SkillSlot = skillSlot;
        Targets = targets?.ToArray() ?? Array.Empty<int>();
        Tint = tint;
    }

    /// <inheritdoc/>
    public int FrameAt(float playheadMs) => Timing.FrameAt(playheadMs - ClipStartMs);

    /// <inheritdoc/>
    public bool ClipOver(float playheadMs) => playheadMs >= ClipStartMs + Timing.TotalMs;

    /// <inheritdoc/>
    public bool Finished(float playheadMs)
    {
        if (!ClipOver(playheadMs) || RootOffsetX(playheadMs) != 0f) return false;   // still on its way home
        var life = Math.Max(Recipe.Impact.DebrisSeconds, Math.Max(Recipe.Impact.RingSeconds, Recipe.Impact.FlashSeconds));
        return _impacts.All(i => i.Age >= life);
    }

    /// <inheritdoc/>
    public bool IsBeat(int atMs) => Math.Abs(atMs - BeatMs) < 1f;

    /// <inheritdoc/>
    public float DuckAt(float playheadMs)
        => playheadMs >= ReleaseMs && playheadMs < BeatMs + Recipe.DuckTailMs ? Recipe.DuckOthers : 1f;

    /// <inheritdoc/>
    public PerformanceStep Update(float playheadMs, float dt, IActionStage stage)
    {
        _stage = stage;
        if (!_planned) Plan(stage);
        bool released = false, contacted = false;
        Vector2 releaseAt = default, contactAt = default;
        if (!Released && playheadMs >= ReleaseMs)
        {
            Released = released = true;
            releaseAt = FistNow(stage, playheadMs);
        }
        if (!Contacted && playheadMs >= BeatMs)
        {
            Contacted = contacted = true;
            contactAt = _fistAtContact;
            Land(stage);
        }
        foreach (var i in _impacts) i.Age += dt;
        return new PerformanceStep(released, contacted, releaseAt, contactAt);
    }

    /// <summary>Measure the lunge: from the fist's socket on the contact frame, at rest, to the target.</summary>
    private void Plan(IActionStage stage)
    {
        _planned = true;
        CasterHeightForMotion = stage.CasterHeight;
        var contactFrame = Timing.Markers[Recipe.ContactMarker];
        if (Timing.Socket(contactFrame, Recipe.HandSocket) is not { } socket
            || !stage.TryActorFrame(Recipe.ClipKey, contactFrame, out var drawn, out var size)) return;
        var fist = ActorSocketMap.ToArena(drawn, size, contactFrame, socket);
        _forceDegrees = ActorSocketMap.AngleInArena(socket, drawn.Effects.HasFlag(SpriteEffects.FlipHorizontally)) * 180f / MathF.PI;
        if (Targets.Count > 0 && stage.TryTargetBody(Targets[0], out var body))
        {
            var target = new Vector2(body.X + body.Width * Recipe.ContactPoint.X, body.Y + body.Height * Recipe.ContactPoint.Y);
            _reach = Math.Max(0f, target.X - fist.X);
        }
        _fistAtContact = fist + new Vector2(_reach, 0f);
    }

    private Vector2 FistNow(IActionStage stage, float playheadMs)
    {
        var frame = FrameAt(playheadMs);
        if (Timing.Socket(frame, Recipe.HandSocket) is { } s && stage.TryActorFrame(Recipe.ClipKey, frame, out var d, out var size))
            return ActorSocketMap.ToArena(d, size, frame, s);
        return _fistAtContact;
    }

    /// <summary>The contact: the primary impact where the fist lands, a smaller one on any other struck target.</summary>
    private void Land(IActionStage stage)
    {
        var look = Recipe.Impact;
        _impacts.Add(Burst(_fistAtContact, 1f, true, look));
        for (var k = 1; k < Targets.Count; k++)
            if (stage.TryTargetBody(Targets[k], out var body))
                _impacts.Add(Burst(new Vector2(body.X + body.Width * Recipe.ContactPoint.X, body.Y + body.Height * Recipe.ContactPoint.Y),
                                   look.SecondaryScale, false, look));
    }

    private Impact Burst(Vector2 at, float scale, bool primary, MeleeImpactLook look)
    {
        var i = new Impact { At = at, Scale = scale, Primary = primary, Degrees = _forceDegrees };
        if (!primary) return i;
        var seed = (int)(at.X * 7 + at.Y * 13);
        var force = MathHelper.ToRadians(_forceDegrees);
        i.Shards = Enumerable.Range(0, look.Shards).Select(k =>
        {
            var spread = MathHelper.ToRadians(look.SpreadDegrees) * (ProjectileMotion.Hash01(seed, k) * 2f - 1f);
            var a = force + spread;
            return (new Vector2(MathF.Cos(a), MathF.Sin(a)), 0.55f + 0.45f * ProjectileMotion.Hash01(seed, k + 50),
                    (ProjectileMotion.Hash01(seed, k + 90) - 0.5f) * 14f);
        }).ToArray();
        // the light chips: most along the force, a share kicked back off the blow (up and away from the fist)
        i.Chips = Enumerable.Range(0, look.Chips).Select(k =>
        {
            var back = ProjectileMotion.Hash01(seed, k + 300) < look.Rebound;
            var spread = MathHelper.ToRadians(look.SpreadDegrees) * (ProjectileMotion.Hash01(seed, k + 330) * 2f - 1f);
            var a = (back ? force + MathF.PI * 0.72f * (k % 2 == 0 ? 1f : -1f) : force) + spread * (back ? 0.4f : 1f);
            return (new Vector2(MathF.Cos(a), MathF.Sin(a)), (back ? 0.45f : 0.7f) + 0.4f * ProjectileMotion.Hash01(seed, k + 360),
                    (ProjectileMotion.Hash01(seed, k + 390) - 0.5f) * 10f);
        }).ToArray();
        i.Sparks = Enumerable.Range(0, look.Sparks).Select(k =>
        {
            var spread = MathHelper.ToRadians(look.SpreadDegrees * 1.4f) * (ProjectileMotion.Hash01(seed, k + 200) * 2f - 1f);
            var a = force + spread;
            return (new Vector2(MathF.Cos(a), MathF.Sin(a)), 0.7f + 0.6f * ProjectileMotion.Hash01(seed, k + 250));
        }).ToArray();
        return i;
    }

    /// <inheritdoc/>
    public void Retime(ActionClipTiming played, float? yieldAtMs = null, float returnByMs = 0f)
    {
        _played = played ?? throw new ArgumentNullException(nameof(played));
        if (yieldAtMs is not { } at) return;
        _returnFrom = at - ClipStartMs;
        _returnMs = Math.Max(Recipe.MinReturnMs, returnByMs - at);
    }

    /// <inheritdoc/>
    public float RootOffsetX(float playheadMs)
        => RootMotion(Recipe, _played, _reach, playheadMs - ClipStartMs, CasterHeightForMotion, _returnFrom, _returnMs);

    /// <inheritdoc/>
    public float RootOffsetY(float playheadMs)
        => RootLift(Recipe, _played, _reach, playheadMs - ClipStartMs, CasterHeightForMotion, _returnFrom, _returnMs);

    /// <summary>
    /// When the way home starts (ms into the clip) and how long it takes: from the recovery, over
    /// <see cref="MeleeActionRecipe.ReturnShare"/> of it and never under <see cref="MeleeActionRecipe.MinReturnMs"/>;
    /// or from a yield, over the time it was given.
    /// </summary>
    private static (float From, float Ms) ReturnWindow(MeleeActionRecipe recipe, ActionClipTiming timing, float? returnFrom, float returnMs)
    {
        if (returnFrom is { } from) return (from, Math.Max(recipe.MinReturnMs, returnMs));
        var tRecover = timing.MarkerMs(recipe.RecoveryMarker);
        return (tRecover, Math.Max(recipe.MinReturnMs, (timing.TotalMs - tRecover) * Math.Clamp(recipe.ReturnShare, 0.05f, 1f)));
    }

    /// <summary>
    /// The retreat's low bound as a function of time into the clip: 0 until the retreat, a low arc up to
    /// <see cref="MeleeActionRecipe.HopHeight"/> through its middle (<see cref="MeleeActionRecipe.BoundFrom"/> to
    /// <see cref="MeleeActionRecipe.BoundTo"/>), grounded again before it settles home. Negative is up. Pure, so it
    /// is tested directly.
    /// </summary>
    public static float RootLift(MeleeActionRecipe recipe, ActionClipTiming timing, float reach, float t, float casterHeight,
                                 float? returnFrom = null, float returnMs = 0f)
    {
        if (reach <= 0f || recipe.HopHeight <= 0f) return 0f;
        var (from, ms) = ReturnWindow(recipe, timing, returnFrom, returnMs);
        var u = (t - from) / Math.Max(1f, ms);
        var b = (u - recipe.BoundFrom) / Math.Max(0.01f, recipe.BoundTo - recipe.BoundFrom);
        if (b <= 0f || b >= 1f) return 0f;
        return -recipe.HopHeight * casterHeight * MathF.Sin(MathF.PI * b);
    }

    /// <summary>The retreat's spacing, 0 at the target to 1 at home: slow to leave, fastest mid-way, settling in.</summary>
    public static float Retreat(MeleeActionRecipe recipe, float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return MathF.Pow(u * u * (3f - 2f * u), Math.Max(0.1f, recipe.ReturnEase));
    }

    /// <summary>The caster's height the lunge's pull-back and overshoot are shares of (set by the screen each frame).</summary>
    public float CasterHeightForMotion { get; set; } = 412f;

    /// <summary>
    /// The lunge as a function of time into the clip (<paramref name="t"/>): 0 at rest, the pull-back through the
    /// anticipation, the whole <paramref name="reach"/> by the contact, the overshoot through the follow-through,
    /// then home: from the recovery over <see cref="MeleeActionRecipe.ReturnShare"/> of it (never quicker than
    /// <see cref="MeleeActionRecipe.MinReturnMs"/>, so it may outlive the clip), or, when the clip YIELDED, from
    /// <paramref name="returnFrom"/> over <paramref name="returnMs"/>. Pure, so it is tested directly.
    /// </summary>
    public static float RootMotion(MeleeActionRecipe recipe, ActionClipTiming timing, float reach, float t, float casterHeight,
                                   float? returnFrom = null, float returnMs = 0f)
    {
        if (reach <= 0f || t <= 0f) return 0f;
        var (from, ms) = ReturnWindow(recipe, timing, returnFrom, returnMs);
        if (t >= from + ms) return 0f;
        if (t >= from) return Travel(recipe, timing, reach, from, casterHeight) * (1f - Retreat(recipe, (t - from) / Math.Max(1f, ms)));
        return Travel(recipe, timing, reach, t, casterHeight);
    }

    /// <summary>The way out: pull-back, the accelerating commit, the overshoot (held once it is spent).</summary>
    private static float Travel(MeleeActionRecipe recipe, ActionClipTiming timing, float reach, float t, float casterHeight)
    {
        var back = -recipe.LungeBack * casterHeight;
        var over = reach + recipe.LungeOvershoot * casterHeight;
        var tAnt = timing.HasMarker("anticipation") ? timing.MarkerMs("anticipation") : 0f;
        var tCommit = timing.MarkerMs(recipe.CommitMarker);
        var tContact = timing.MarkerMs(recipe.ContactMarker);
        var tRecover = timing.MarkerMs(recipe.RecoveryMarker);
        static float Smooth(float u) => u * u * (3f - 2f * u);
        if (t < tAnt) return 0f;
        if (t < tCommit) return back * Smooth((t - tAnt) / Math.Max(1f, tCommit - tAnt));
        if (t < tContact) return back + (reach - back) * MathF.Pow((t - tCommit) / Math.Max(1f, tContact - tCommit), recipe.LungeAccel);
        var u = Math.Clamp((t - tContact) / Math.Max(1f, tRecover - tContact), 0f, 1f);
        return reach + (over - reach) * (1f - (1f - u) * (1f - u));   // eases out: momentum spent
    }

    /// <inheritdoc/>
    public void DrawProp(SpriteBatch b, IActionStage stage, float playheadMs)
    {
    }

    /// <inheritdoc/>
    public void DrawMaterial(SpriteBatch b)
    {
        _sprites = 0;
        foreach (var i in _impacts.Where(i => i.Primary))
            DrawShards(b, i);
    }

    /// <inheritdoc/>
    public void DrawLight(SpriteBatch b, float playheadMs, Texture2D? streak)
    {
        foreach (var i in _impacts)
            DrawBurst(b, i);
    }

    /// <summary>
    /// The speed lines behind a lunging body (drawn by the screen, which knows where the body is): only on the
    /// COMMIT, the dash into the blow. The way home is a controlled return, not a second attack, so it leaves none.
    /// </summary>
    public void DrawSpeedLines(SpriteBatch b, Texture2D streak, Rectangle body, float playheadMs)
    {
        if (Recipe.SpeedLines <= 0 || _reach <= 0f) return;
        var moved = RootOffsetX(playheadMs) - RootOffsetX(playheadMs - Recipe.SpeedLineMs);
        if (moved < CasterHeightForMotion * 0.12f) return;   // only a real forward dash leaves lines
        var colour = VfxBlend.Light(Color.Lerp(new Color(210, 214, 226), Tint, 0.3f) * Recipe.SpeedLineBrightness);
        for (var k = 0; k < Recipe.SpeedLines; k++)
        {
            var y = body.Y + body.Height * (0.3f + 0.2f * k);
            var len = moved * (0.9f - 0.2f * k);
            var from = new Vector2(body.X + body.Width * 0.2f - len, y);
            b.Draw(streak, from, null, colour, 0f, new Vector2(0f, streak.Height / 2f),
                   new Vector2(len / streak.Width, 6f / streak.Height), SpriteEffects.None, 0f);
            _sprites++;
        }
    }

    private void DrawBurst(SpriteBatch b, Impact i)
    {
        var look = Recipe.Impact;
        var h = CasterHeightForMotion * i.Scale;
        var force = MathHelper.ToRadians(i.Degrees);
        // the flash: white-hot, COMPRESSED across the force (the blow flattening into what it struck), gone in a
        // few frames; it opens a little as it fades
        if (i.Primary && i.Age < look.FlashSeconds && Tex(look.FlashKey) is { } flash)
        {
            var k = 1f - i.Age / look.FlashSeconds;
            var across = look.FlashSize * h * (0.85f + 0.3f * (1f - k));
            var along = across * (look.FlashSquash + (1f - look.FlashSquash) * 0.4f * (1f - k));
            b.Draw(flash, i.At, null, VfxBlend.Light(Color.Lerp(Color.White, Tint, 0.3f) * (k * k)), force,
                   new Vector2(flash.Width, flash.Height) / 2f, new Vector2(along / flash.Width, across / flash.Height),
                   SpriteEffects.None, 0f);
            _sprites++;
        }
        // the compression ring: a shock front ACROSS the force, squashed along it, racing outward
        if (i.Age < look.RingSeconds && Tex(look.RingKey) is { } ring)
        {
            var u = i.Age / look.RingSeconds;
            var grow = 1f - (1f - u) * (1f - u);
            var across = (look.RingFrom + (look.RingTo - look.RingFrom) * grow) * h;
            var along = across * look.RingSquash;
            var fade = (1f - u) * (1f - 0.5f * u);   // a shock: brightest as it leaves the fist, gone as it opens
            b.Draw(ring, i.At, null, VfxBlend.Light(Color.Lerp(Color.White, Tint, 0.45f) * fade), force,
                   new Vector2(ring.Width, ring.Height) / 2f, new Vector2(along / ring.Width, across / ring.Height),
                   SpriteEffects.None, 0f);
            _sprites++;
        }
        // the chips: slivers of light thrown off the blow, falling as they slow
        if (i.Primary && i.Age < look.DebrisSeconds && Tex(look.ShardKey) is { } chip)
        {
            var u = i.Age / look.DebrisSeconds;
            foreach (var (dir, speed, spin) in i.Chips)
            {
                var at = i.At + dir * (look.Reach * speed * h * (1f - (1f - u) * (1f - u))) + new Vector2(0f, look.Gravity * h * 0.1f * u * u);
                var len = look.ShardLength * h * (1f - 0.5f * u);
                b.Draw(chip, at, null, VfxBlend.Light(Color.Lerp(Tint, Color.White, look.ChipWhite) * (1f - u)),
                       MathF.Atan2(dir.Y, dir.X) + spin * u, new Vector2(chip.Width, chip.Height) / 2f,
                       new Vector2(len / chip.Width, len * 0.3f / chip.Height), SpriteEffects.None, 0f);
                _sprites++;
            }
        }
        // sparks: a few hot points thrown along the force
        if (i.Primary && i.Age < look.DebrisSeconds * 0.7f && Tex(look.SparkKey) is { } spark)
        {
            var u = i.Age / (look.DebrisSeconds * 0.7f);
            foreach (var (dir, speed) in i.Sparks)
            {
                var at = i.At + dir * (look.Reach * 1.1f * speed * h * (1f - (1f - u) * (1f - u)));
                b.Draw(spark, at, null, VfxBlend.Light(Color.Lerp(Color.White, Tint, 0.4f) * (1f - u)), 0f,
                       new Vector2(spark.Width, spark.Height) / 2f, 0.05f * h / spark.Width, SpriteEffects.None, 0f);
                _sprites++;
            }
        }
    }

    private void DrawShards(SpriteBatch b, Impact i)
    {
        var look = Recipe.Impact;
        if (i.Age >= look.DebrisSeconds || Tex(look.ShardKey) is not { } shard) return;
        var h = CasterHeightForMotion;
        var u = i.Age / look.DebrisSeconds;
        foreach (var (dir, speed, spin) in i.Shards)
        {
            var travel = look.Reach * speed * h * (1f - (1f - u) * (1f - u));
            var fall = look.Gravity * h * 0.1f * u * u;
            var at = i.At + dir * travel + new Vector2(0f, fall);
            var angle = MathF.Atan2(dir.Y, dir.X) + spin * u;
            var len = look.ShardLength * h * (1f - 0.4f * u);
            b.Draw(shard, at, null, look.ShardColour * (1f - u * u), angle, new Vector2(shard.Width, shard.Height) / 2f,
                   new Vector2(len / shard.Width, len * 0.35f / shard.Height), SpriteEffects.None, 0f);
            _sprites++;
        }
    }

    private IActionStage? _stage;   // the stage the performance was last updated on: its textures

    private Texture2D? Tex(string key) => _stage?.Texture(key);

    /// <inheritdoc/>
    public IEnumerable<Vector2> LaunchPoints => _planned ? new[] { _fistAtContact } : Array.Empty<Vector2>();

    /// <inheritdoc/>
    public int SpriteCount => _sprites;
}
