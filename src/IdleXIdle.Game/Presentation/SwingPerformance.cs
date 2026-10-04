using System;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>Which part texture a <see cref="SwingSprite"/> draws.</summary>
public enum SwingPart
{
    /// <summary>The directional slash (<c>fxp_trail_soft</c>, drawn from its left end).</summary>
    Slash,

    /// <summary>A thin sliver of light (<c>fxp_shard_sliver</c>).</summary>
    Sliver,

    /// <summary>The compressed flash (<c>fxp_flash_soft</c>).</summary>
    Flash,

    /// <summary>The flat ring (<c>fxp_ring_soft</c>).</summary>
    Ring,

    /// <summary>A hot point (<c>fxp_spark_dot</c>).</summary>
    Spark,

    /// <summary>The dust puff (<c>fxp_dust_soft</c>), a MATERIAL: untinted, alpha-blended.</summary>
    Dust,

    /// <summary>A chip of the missile's own material (<see cref="SwingImpactLook.ChipKey"/>), a MATERIAL: untinted.</summary>
    Chip,
}

/// <summary>
/// One sprite of a swing's contact picture, placed: where, how big (px), turned how far, in what colour. The screen
/// draws it centred on <see cref="At"/>, or from its left end when <see cref="FromLeft"/>.
/// </summary>
public readonly record struct SwingSprite(SwingPart Part, Vector2 At, float Rotation, Vector2 Size, Color Color, bool FromLeft)
{
    /// <summary>The texture key when it is not the part's own (<see cref="SwingPerformance.KeyOf"/>): a chip's prop.</summary>
    public string? Key { get; init; }
}

/// <summary>
/// What a performed swing needs from the screen it plays on (P1.4): the champion's own <c>attack</c> strip frame as the
/// draw draws it, where the creatures stand, the textures and the champion's height. Implemented by the hunt screen
/// (allocation-free: its strip key is resolved once per swing); faked in tests.
/// </summary>
public interface ISwingStage
{
    /// <summary>The swing's strip, frame <paramref name="frame"/>, resolved exactly as the draw draws it.</summary>
    bool TrySwingFrame(int frame, out SpriteFrame drawn, out int frameSize);

    /// <summary>The visible body of the creature in <paramref name="slot"/>.</summary>
    bool TrySwingTarget(int slot, out Rectangle body);

    /// <summary>A texture by asset key, or null (the flight is still placed and timed; it only draws nothing).</summary>
    Texture2D? SwingTexture(string key);

    /// <summary>The champion's visible height, px.</summary>
    float SwingCasterHeight { get; }
}

/// <summary>What one <see cref="SwingPerformance.Update"/> crossed, for the screen to voice and trace.</summary>
/// <param name="Released">The missiles left the hand this update (the release cue's moment).</param>
/// <param name="ReleaseAt">The hand, arena px.</param>
/// <param name="Landed">The missiles landed this update (on the beat).</param>
public readonly record struct SwingStep(bool Released, Vector2 ReleaseAt, bool Landed);

/// <summary>
/// THE BASIC ATTACK, PERFORMED (design.md 5.18-5.27, P1.3): the authored frames of the champion's own <c>attack</c>
/// strip inside the plain envelope (<see cref="SwingClock"/>), the melee STEP-IN as a draw offset, and the small contact
/// picture AT the creature on the beat. ONE instance per screen, <see cref="Begin"/> per swing: fixed arrays, nothing
/// allocated after construction.
/// </summary>
/// <remarks>
/// <para>
/// THE STEP-IN IS NOT ROOT MOTION. It is added to the champion's draw push beside (never inside) the performed actions'
/// root offset, so the <c>root</c> trace never carries it; its curve reads the playhead, not a frame's dt (P0.2's
/// LungePx lesson), and it is home before the planned exit for every handoff fit (Natural, Floor, Compressed, Cut).
/// </para>
/// <para>
/// THE PICTURE OUTLIVES THE CLIP: a contact is a small ring of impacts aged by the playhead, so the next swing's
/// <see cref="Begin"/> (often on the frame this clip ends) never cuts the last one short.
/// </para>
/// </remarks>
public sealed class SwingPerformance
{
    /// <summary>The most impacts alive at once (a swing strikes one creature; a spread strikes a few).</summary>
    public const int MaxImpacts = 4;

    /// <summary>The most sprites one pass composes.</summary>
    public const int MaxSprites = 32;

    /// <summary>The most targets a swing's beat names.</summary>
    public const int MaxTargets = 8;

    private struct Impact
    {
        public Vector2 At;
        public float Height;
        public float BornMs;
        public int Seed;
        public bool Alive;
        public SwingImpactLook Look;
    }

    // THE MISSILES (P1.4): ONE pre-built composite per possible target, re-initialised per flight (nothing allocated)
    private readonly ProjectileVisual[] _flights = new ProjectileVisual[MaxTargets];
    private readonly Vector2[] _flightFrom = new Vector2[MaxTargets];
    private readonly Vector2[] _flightTo = new Vector2[MaxTargets];
    private readonly float[] _flightBulge = new float[MaxTargets];
    private int _flightCount;
    private float _flightReleaseMs, _flightBeatMs, _flightDeparture;
    private readonly ReachStrand _strand = new();
    private readonly Impact[] _impacts = new Impact[MaxImpacts];
    private int _nextImpact;
    private readonly SwingSprite[] _sprites = new SwingSprite[MaxSprites];
    private readonly int[] _targets = new int[MaxTargets];
    private int _anchor, _commit, _settle;
    private int _voicedAtMs = int.MinValue;

    /// <summary>One reusable composite per possible target, built once (the largest look any missile has fits it).</summary>
    public SwingPerformance()
    {
        for (var k = 0; k < MaxTargets; k++)
            _flights[k] = new ProjectileVisual(ProjectileLooks.QuiverArrow, k, sparkCapacity: 4, shardCapacity: 8);
    }

    /// <summary>The recipe being performed (null before the first swing).</summary>
    public SwingRecipe? Recipe { get; private set; }

    /// <summary>The authored timing played (the <c>.clip.json</c>).</summary>
    public ActionClipTiming? Timing { get; private set; }

    /// <summary>True while a swing's clip is on the figure (from <see cref="Begin"/> to <see cref="EndClip"/>).</summary>
    public bool ClipLive { get; private set; }

    /// <summary>The plain envelope's start (playhead ms).</summary>
    public float StartMs { get; private set; }

    /// <summary>The swing's beat: the Strike's ms.</summary>
    public int BeatMs { get; private set; }

    /// <summary>When the anchor frame begins: the beat less the travel (never before the start).</summary>
    public float AnchorAtMs { get; private set; }

    /// <summary>The plain envelope's planned exit (playhead ms); a handoff moves it (<see cref="Retime"/>).</summary>
    public float ExitMs { get; private set; }

    /// <summary>The row gap measured at the commit, px (the target's left edge less the champion's right edge).</summary>
    public float GapPx { get; private set; }

    /// <summary>The step-in's peak, px: the recipe's share of the gap.</summary>
    public float PeakPx { get; private set; }

    /// <summary>How many targets the swing's beat names (<see cref="Target"/>).</summary>
    public int TargetCount { get; private set; }

    /// <summary>The <paramref name="k"/>-th creature slot the swing's beat strikes.</summary>
    public int Target(int k) => k >= 0 && k < TargetCount ? _targets[k] : -1;

    /// <summary>The target slots, filled by the screen before <see cref="Begin"/> (<see cref="ActionTargets.SwungAt"/>).</summary>
    public int[] TargetBuffer => _targets;

    /// <summary>
    /// A MISSILE's release, playhead ms: the anchor (release) frame's start, i.e. the beat less the travel, CLAMPED to the
    /// clip's start when the plain envelope began later than that (a late or fast start): then the flight is shorter and
    /// the contact is still on the beat.
    /// </summary>
    public float ReleaseMs => AnchorAtMs;

    /// <summary>A missile's flight, ms: <see cref="SwingRecipe.TravelMs"/>, or less when the release was clamped.</summary>
    public float FlightMs => Math.Max(0f, BeatMs - AnchorAtMs);

    /// <summary>True when the release was clamped to the clip's start (the flight is shorter than its travel).</summary>
    public bool ReleaseClamped => Recipe is { Kind: SwingKind.Missile } r && BeatMs - r.TravelMs < StartMs;

    /// <summary>True once this swing's missiles have left the hand.</summary>
    public bool Released { get; private set; }

    /// <summary>True once this swing's missiles have landed (on the beat).</summary>
    public bool Landed { get; private set; }

    /// <summary>How many flights the last release placed (one per swung target).</summary>
    public int FlightCount => _flightCount;

    /// <summary>The <paramref name="k"/>-th flight of the last release.</summary>
    public ProjectileVisual Flight(int k) => _flights[k];

    /// <summary>A REACH's strand (the Oathbound's chain); not <see cref="ReachStrand.Live"/> for the other kinds.</summary>
    public ReachStrand Strand => _strand;

    /// <summary>Sprites the last <see cref="Compose"/> placed.</summary>
    public int SpriteCount { get; private set; }

    /// <summary>The sprites the last <see cref="Compose"/> placed (the first <see cref="SpriteCount"/>).</summary>
    public ReadOnlySpan<SwingSprite> Sprites => new(_sprites, 0, SpriteCount);

    /// <summary>
    /// Commit a swing: <paramref name="recipe"/> played by <paramref name="timing"/> inside the plain envelope
    /// [<paramref name="startMs"/>, <paramref name="exitMs"/>], its anchor at <paramref name="beatMs"/> less the travel,
    /// stepping in <see cref="SwingRecipe.StepInShare"/> of <paramref name="gapPx"/>. The targets are the first
    /// <paramref name="targets"/> of <see cref="TargetBuffer"/>. The impacts of the swing before keep fading.
    /// </summary>
    public void Begin(SwingRecipe recipe, ActionClipTiming timing, float startMs, int beatMs, float exitMs, float gapPx, int targets)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        _anchor = timing.Markers.TryGetValue(recipe.AnchorMarker, out var a) ? a : timing.Frames / 2;
        _commit = timing.Markers.TryGetValue(recipe.CommitMarker, out var c) ? Math.Min(c, _anchor) : 0;
        _settle = timing.Markers.TryGetValue(recipe.SettleMarker, out var s) ? Math.Max(s, _anchor) : timing.Frames - 1;
        StartMs = startMs;
        BeatMs = beatMs;
        AnchorAtMs = SwingClock.AnchorStart(startMs, beatMs - recipe.TravelMs);
        ExitMs = exitMs;
        GapPx = Math.Max(0f, gapPx);
        PeakPx = recipe.Kind == SwingKind.StepIn ? recipe.StepInShare * GapPx : 0f;
        TargetCount = Math.Clamp(targets, 0, MaxTargets);
        ClipLive = true;
        Released = false;
        Landed = false;
        // A REACH leaves the hand on its first lash frame (the first frame that authors the hand socket) and is taut on the
        // beat; any other swing has no strand
        if (recipe is { Kind: SwingKind.Reach, Reach: { } reach })
        {
            var first = _anchor;
            for (var f = 0; f < timing.Frames; f++)
                if (timing.Socket(f, recipe.HandSocket) is not null) { first = Math.Min(f, _anchor); break; }
            _strand.Begin(reach, SwingClock.FrameStart(timing, _anchor, startMs, AnchorAtMs, exitMs, first), beatMs);
        }
        else _strand.Reset();
    }

    /// <summary>The handoff moved the plain envelope's exit: the frames after the anchor and the way home follow it.</summary>
    public void Retime(float exitMs) => ExitMs = exitMs;

    /// <summary>The clip left the figure (its end, a rewind, a new wave). The picture still fades.</summary>
    public void EndClip() => ClipLive = false;

    /// <summary>
    /// The replay was REBUILT (a rig seek rewinds the wave): forget which swing ms was voiced, so a swing the live run
    /// already voiced is voiced and traced again when the replay crosses it. Nothing else changes (the clip, its picture).
    /// </summary>
    public void ForgetVoiced() => _voicedAtMs = int.MinValue;

    /// <summary>Forget everything: a new wave (its impacts too).</summary>
    public void Reset()
    {
        ClipLive = false;
        Recipe = null;
        Timing = null;
        TargetCount = 0;
        SpriteCount = 0;
        _voicedAtMs = int.MinValue;
        for (var i = 0; i < _impacts.Length; i++) _impacts[i].Alive = false;
        Released = false;
        Landed = false;
        _flightCount = 0;
        _strand.Reset();
    }

    /// <summary>
    /// Advance the swing's MISSILES and its REACH to <paramref name="playheadMs"/> (before the frame's events are crossed,
    /// so a flight lands on the frame its Strike is presented). A missile leaves the hand's socket on the release frame
    /// (<see cref="ReleaseMs"/>), one per swung creature, flies the throw path (a lob bows up) and lands ON the beat; a
    /// reach is aimed from the hand on the frame on the figure to the creature's contact point. Nothing allocates.
    /// </summary>
    public SwingStep Update(float playheadMs, float dt, ISwingStage stage)
    {
        var released = false;
        var landed = false;
        var hand = Vector2.Zero;
        if (Recipe is { Kind: SwingKind.Missile, Missile: { } look } r && ClipLive && !Released && playheadMs >= ReleaseMs)
        {
            Released = true;
            released = true;
            hand = Release(r, look, stage);
        }
        for (var k = 0; k < _flightCount; k++)
        {
            var v = _flights[k];
            if (!v.Placed) continue;
            if (v.Landed) { v.Linger(dt); continue; }
            var flight = _flightBeatMs - _flightReleaseMs;
            var u = flight <= 0f ? 1f : Math.Clamp((playheadMs - _flightReleaseMs) / flight, 0f, 1f);
            v.Drive(dt, u, ProjectileMotion.ThrowPosition(_flightFrom[k], _flightTo[k], _flightBulge[k], _flightDeparture, u),
                    ProjectileMotion.ThrowHeading(_flightFrom[k], _flightTo[k], _flightBulge[k], _flightDeparture, u));
            if (playheadMs >= _flightBeatMs) v.Land();
        }
        if (Released && !Landed && playheadMs >= BeatMs)
        {
            Landed = true;
            landed = true;
        }
        if (Recipe is { Kind: SwingKind.Reach } reachRecipe && _strand.Live && Timing is { } t)
            AimStrand(reachRecipe, t, playheadMs, stage);
        return new SwingStep(released, hand, landed);
    }

    // THE RELEASE: one flight per swung creature, from the hand's socket on the release frame; the tip meets the contact
    private Vector2 Release(SwingRecipe r, ProjectileLook look, ISwingStage stage)
    {
        _flightCount = 0;
        _flightReleaseMs = ReleaseMs;
        _flightBeatMs = BeatMs;
        _flightDeparture = r.Departure;
        if (Timing is not { } t || !TryHand(t, _anchor, r.HandSocket, stage, out var hand, out var scale)) return Vector2.Zero;
        var material = look.MaterialKey is { } mk ? stage.SwingTexture(mk) : null;
        var edge = stage.SwingTexture(look.HeadKey);
        var trail = stage.SwingTexture(look.TrailKey);
        var glint = stage.SwingTexture(look.GlintKey);
        var spark = stage.SwingTexture(look.SparkKey);
        var shard = stage.SwingTexture(look.ShardKey);
        var flash = stage.SwingTexture(look.FlashKey);
        var content = look.HeadContent ?? new Rectangle(0, 0, material?.Width ?? 1, material?.Height ?? 1);
        var caster = stage.SwingCasterHeight;
        for (var k = 0; k < TargetCount; k++)
        {
            if (!stage.TrySwingTarget(_targets[k], out var body)) continue;
            var contact = new Vector2(body.X + body.Width * r.ContactPoint.X, body.Y + body.Height * r.ContactPoint.Y);
            var line = contact - hand;
            var dir = line.LengthSquared() > 1e-6f ? Vector2.Normalize(line) : Vector2.UnitX;
            var to = contact - dir * (content.Width * 0.5f * scale);   // the TIP meets the contact point
            var n = _flightCount++;
            _flightFrom[n] = hand;
            _flightTo[n] = to;
            _flightBulge[n] = r.FlightBulge * caster;
            var v = _flights[n];
            v.Reset(look, _targets[k] * 97 + 29);
            v.PlaceDriven(hand, to, r.FlightLight, material, edge, scale, content, trail, glint, spark, shard, flash);
            v.Drive(0f, 0f, hand, ProjectileMotion.ThrowHeading(hand, to, _flightBulge[n], r.Departure, 0f));
        }
        return hand;
    }

    // THE STRAND's two ends, this update: the hand on the frame on the figure (or the nearest frame that authors it) and
    // the first swung creature's contact point
    private void AimStrand(SwingRecipe r, ActionClipTiming t, float playheadMs, ISwingStage stage)
    {
        var frame = FrameAt(playheadMs);
        var hand = _strand.Hand;
        var scale = _strand.PixelScale;
        for (var d = 0; d < t.Frames; d++)
        {
            if (TryHand(t, frame - d, r.HandSocket, stage, out hand, out scale)) break;
            if (TryHand(t, frame + d, r.HandSocket, stage, out hand, out scale)) break;
        }
        var target = _strand.Target;
        if (TargetCount > 0 && stage.TrySwingTarget(_targets[0], out var body))
            target = new Vector2(body.X + body.Width * r.ContactPoint.X, body.Y + body.Height * r.ContactPoint.Y);
        _strand.Aim(hand, target, stage.SwingCasterHeight, scale);
    }

    // the socket named `socket` on `frame`, in the arena, with the strip's draw scale
    private static bool TryHand(ActionClipTiming t, int frame, string socket, ISwingStage stage, out Vector2 hand, out float scale)
    {
        hand = Vector2.Zero;
        scale = 1f;
        if (frame < 0 || frame >= t.Frames || t.Socket(frame, socket) is not { } s) return false;
        if (!stage.TrySwingFrame(frame, out var drawn, out var size)) return false;
        // (no Enum.HasFlag: un-tiered code boxes it, and the reach asks every frame)
        hand = ActorSocketMap.ToArena(drawn.Src, drawn.Dest, (drawn.Effects & SpriteEffects.FlipHorizontally) != 0, size, frame, s);
        scale = ActorSocketMap.DrawScale(drawn);
        return true;
    }

    /// <summary>True while a missile of the last release is in the air or its contact is still fading.</summary>
    public bool Flying()
    {
        for (var k = 0; k < _flightCount; k++)
            if (_flights[k].Placed && !_flights[k].Finished) return true;
        return false;
    }

    /// <summary>True when <paramref name="atMs"/> is this live swing's beat.</summary>
    public bool IsBeat(int atMs) => ClipLive && atMs == BeatMs;

    /// <summary>The authored frame on the figure at <paramref name="playheadMs"/> (<see cref="SwingClock.FrameAt"/>).</summary>
    public int FrameAt(float playheadMs)
        => Timing is { } t ? SwingClock.FrameAt(t, _anchor, StartMs, AnchorAtMs, ExitMs, playheadMs) : 0;

    /// <summary>The step-in's draw offset at <paramref name="playheadMs"/>, px (0 when no swing is on the figure).</summary>
    public float StepPx(float playheadMs)
        => ClipLive && Timing is { } t
            ? SwingClock.StepIn(t, _anchor, _commit, _settle, StartMs, AnchorAtMs, ExitMs, PeakPx, playheadMs,
                               Recipe?.StepInPower ?? 2f)
            : 0f;

    /// <summary>
    /// The swing's blow LANDS on the creature whose visible body is <paramref name="body"/>, at <paramref name="atMs"/>
    /// (the Strike's ms; its picture is born there). True for the first contact at this ms: the one the screen voices
    /// (one cue per swing ms, however many creatures it struck).
    /// </summary>
    public bool Contact(int atMs, Rectangle body)
    {
        if (Recipe is not { } r) return false;
        ref var i = ref _impacts[_nextImpact];
        _nextImpact = (_nextImpact + 1) % MaxImpacts;
        i.At = new Vector2(body.X + body.Width * r.ContactPoint.X, body.Y + body.Height * r.ContactPoint.Y);
        i.Height = body.Height;
        i.BornMs = atMs;
        i.Seed = atMs * 31 + body.X;
        i.Alive = true;
        i.Look = r.Impact;
        if (_voicedAtMs == atMs) return false;
        _voicedAtMs = atMs;
        return true;
    }

    /// <summary>True while any contact picture is still on screen at <paramref name="playheadMs"/>.</summary>
    public bool Drawing(float playheadMs)
    {
        if (Flying() || _strand.Drawing(playheadMs)) return true;
        for (var k = 0; k < _impacts.Length; k++)
        {
            ref readonly var i = ref _impacts[k];
            if (i.Alive && playheadMs >= i.BornMs && playheadMs - i.BornMs < i.Look.LifeMs) return true;
        }
        return false;
    }

    /// <summary>
    /// Place the contact picture at <paramref name="playheadMs"/>: the LIGHT sprites (<paramref name="light"/> true: the
    /// slash, the slivers, the flash, the ring, the sparks) or the MATERIAL ones (the dust). Returns the count; the sprites
    /// are in <see cref="Sprites"/>. Nothing allocates.
    /// </summary>
    public int Compose(float playheadMs, bool light)
    {
        var n = 0;
        for (var k = 0; k < _impacts.Length; k++)
        {
            ref var i = ref _impacts[k];
            if (!i.Alive) continue;
            var age = playheadMs - i.BornMs;
            if (age < 0f) continue;
            if (age >= i.Look.LifeMs) { i.Alive = false; continue; }
            n = light ? ComposeLight(in i, age, n) : ComposeMaterial(in i, age, n);
        }
        SpriteCount = n;
        return n;
    }

    private int ComposeLight(in Impact i, float age, int n)
    {
        var look = i.Look;
        var h = i.Height;
        var ink = look.Light * look.Brightness;
        var force = MathHelper.ToRadians(look.ForceDegrees);
        // THE SLASH: drawn along its length over two display frames, then thinning and fading
        if (look.SlashLength > 0f && age < look.SlashMs && n < MaxSprites)
        {
            var u = age / look.SlashMs;
            var grow = Math.Min(1f, (age + 1000f / 60f) / (2000f / 60f));
            var dir = MathHelper.ToRadians(look.SlashDegrees);
            var full = look.SlashLength * h;
            var from = i.At - new Vector2(MathF.Cos(dir), MathF.Sin(dir)) * (0.5f * full);
            var fade = (1f - u) * (1f - u);
            _sprites[n++] = new SwingSprite(SwingPart.Slash, from, dir,
                                            new Vector2(full * grow, look.SlashThickness * h * (1f - 0.5f * u)), ink * fade, FromLeft: true);
        }
        // THE FLASH: compressed along the force, gone in a few frames, opening a little as it fades
        if (look.FlashSize > 0f && age < look.FlashMs && n < MaxSprites)
        {
            var k = 1f - age / look.FlashMs;
            var across = look.FlashSize * h * (0.85f + 0.15f * (1f - k));
            var along = across * look.FlashSquash;
            _sprites[n++] = new SwingSprite(SwingPart.Flash, i.At, force, new Vector2(along, across), ink * (0.4f + 0.6f * k * k), false);
        }
        // THE FLAT RING: on the ground plane (never turned by the force), racing out, brightest as it leaves
        if (look.RingSize > 0f && age < look.RingMs && n < MaxSprites)
        {
            var u = age / look.RingMs;
            var grow = 1f - (1f - u) * (1f - u);
            var w = (look.RingFrom + (look.RingSize - look.RingFrom) * grow) * h;
            _sprites[n++] = new SwingSprite(SwingPart.Ring, i.At, 0f, new Vector2(w, w * look.RingSquash), ink * ((1f - u) * (1f - 0.5f * u) * 0.8f), false);
        }
        // SLIVERS and SPARKS: a few, thrown along the force, slowing
        var debrisMs = Math.Max(look.SlashMs, 160f);
        if (age < debrisMs)
        {
            var u = age / debrisMs;
            var ease = 1f - (1f - u) * (1f - u);
            for (var s = 0; s < look.Slivers && n < MaxSprites; s++)
            {
                var a = force + MathHelper.ToRadians(28f) * (Hash01(i.Seed, s) * 2f - 1f);
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                var at = i.At + dir * (look.SliverTravel * h * (0.6f + 0.4f * Hash01(i.Seed, s + 40)) * ease);
                var len = look.SliverLength * h * (1f - 0.5f * u);
                _sprites[n++] = new SwingSprite(SwingPart.Sliver, at, a, new Vector2(len, len * 0.3f), ink * (1f - u), false);
            }
            for (var s = 0; s < look.Sparks && n < MaxSprites; s++)
            {
                var a = force + MathHelper.ToRadians(40f) * (Hash01(i.Seed, s + 80) * 2f - 1f);
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                var at = i.At + dir * (look.SparkTravel * h * (0.6f + 0.4f * Hash01(i.Seed, s + 120)) * ease);
                var size = 0.04f * h;
                _sprites[n++] = new SwingSprite(SwingPart.Spark, at, 0f, new Vector2(size, size), ink * (1f - u), false);
            }
        }
        return n;
    }

    private int ComposeMaterial(in Impact i, float age, int n)
    {
        var look = i.Look;
        // CHIPS of the missile's own material, knocked off along the force and turning as they go
        var chipMs = Math.Max(look.SlashMs, 160f);
        if (look.Chips > 0 && look.ChipKey is { } chipKey && age < chipMs)
        {
            var cu = age / chipMs;
            var cease = 1f - (1f - cu) * (1f - cu);
            var force = MathHelper.ToRadians(look.ForceDegrees);
            for (var s = 0; s < look.Chips && n < MaxSprites; s++)
            {
                var a = force + MathHelper.ToRadians(36f) * (Hash01(i.Seed, s + 160) * 2f - 1f);
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                // thrown out, and falling a little as they go (stone, not light)
                var chipAt = i.At + dir * (look.ChipTravel * i.Height * (0.6f + 0.4f * Hash01(i.Seed, s + 200)) * cease)
                         + new Vector2(0f, 0.08f * i.Height * cu * cu);
                var size = look.ChipSize * i.Height * (1f - 0.3f * cu);
                var spin = (Hash01(i.Seed, s + 240) < 0.5f ? -1f : 1f) * 9f * age / 1000f;
                _sprites[n++] = new SwingSprite(SwingPart.Chip, chipAt, a + spin, new Vector2(size, size), Color.White * (1f - cu * cu), false)
                {
                    Key = chipKey,
                };
            }
        }
        if (look.DustSize <= 0f || age >= look.DustMs || n >= MaxSprites) return n;
        var u = age / look.DustMs;
        var ease = 1f - (1f - u) * (1f - u);
        var w = look.DustSize * i.Height * (0.6f + 0.4f * ease);
        var at = i.At + new Vector2(0f, -0.05f * i.Height * ease);   // the dust lifts a little as it spreads
        var fade = (1f - u) * MathF.Sqrt(1f - u);
        _sprites[n++] = new SwingSprite(SwingPart.Dust, at, 0f, new Vector2(w, w * 0.6f), Color.White * (0.85f * fade), false);
        return n;
    }

    /// <summary>A deterministic 0..1 from a seed and an index (no Random: the picture is the same every run).</summary>
    private static float Hash01(int seed, int k)
    {
        unchecked
        {
            var x = (uint)(seed * 73856093) ^ (uint)((k + 1) * 19349663);
            x ^= x >> 13;
            x *= 0x5bd1e995;
            x ^= x >> 15;
            return (x & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>The asset key a part draws.</summary>
    public static string KeyOf(SwingPart part) => part switch
    {
        SwingPart.Slash => "fxp_trail_soft",
        SwingPart.Sliver => "fxp_shard_sliver",
        SwingPart.Flash => "fxp_flash_soft",
        SwingPart.Ring => "fxp_ring_soft",
        SwingPart.Spark => "fxp_spark_dot",
        SwingPart.Chip => "prop_unbroken_chip",
        _ => "fxp_dust_soft",
    };
}
