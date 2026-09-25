using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// What a performance needs from the screen it plays on: where the actor's frames are drawn, where the
/// targets stand, and the textures. Implemented by the hunt screen; faked in tests.
/// </summary>
public interface IActionStage
{
    /// <summary>The actor's <paramref name="clipKey"/> clip, frame <paramref name="frame"/>, resolved exactly as the draw draws it.</summary>
    bool TryActorFrame(string clipKey, int frame, out SpriteFrame drawn, out int frameSize);

    /// <summary>The visible body of the creature in <paramref name="slot"/>.</summary>
    bool TryTargetBody(int slot, out Rectangle body);

    /// <summary>A texture by asset key, or null.</summary>
    Texture2D? Texture(string key);

    /// <summary>The acting champion's visible height, in arena pixels.</summary>
    float CasterHeight { get; }
}

/// <summary>
/// What one <see cref="ActionPerformance.Update"/> crossed, for the screen to voice: the release, the contact, and
/// at most one of the contact's secondary ticks (<paramref name="Tick"/> is where, when one is due).
/// </summary>
public readonly record struct PerformanceStep(bool Released, bool Contacted, Vector2 ReleaseAt, Vector2 ContactAt, Vector2? Tick = null);

/// <summary>
/// ONE AUTHORED ACTION BEING PERFORMED (ADR-011), as the screen sees it: a projectile
/// (<see cref="ActionPerformance"/>) or a melee strike (<see cref="MeleePerformance"/>). Both run on the fight's
/// playhead, both land their contact on the beat, and the screen voices and draws them the same way.
/// </summary>
public interface IActionPerformance
{
    /// <summary>The recipe being performed.</summary>
    IActionRecipe Recipe { get; }

    /// <summary>The clip's timing, fitted to the time there was before the beat.</summary>
    ActionClipTiming Timing { get; }

    /// <summary>The fight's beat: the moment of CONTACT, in playhead ms.</summary>
    float BeatMs { get; }

    /// <summary>When the clip starts (playhead ms).</summary>
    float ClipStartMs { get; }

    /// <summary>When the release (a throw) or the commit (a lunge) happens (playhead ms).</summary>
    float ReleaseMs { get; }

    /// <summary>The skill slot whose cast this is.</summary>
    int SkillSlot { get; }

    /// <summary>The enemies the cast strikes, in the order the fight resolved them.</summary>
    IReadOnlyList<int> Targets { get; }

    /// <summary>True once the release (or the commit) has happened.</summary>
    bool Released { get; }

    /// <summary>The clip frame showing at <paramref name="playheadMs"/>.</summary>
    int FrameAt(float playheadMs);

    /// <summary>True once the clip has played to its last frame.</summary>
    bool ClipOver(float playheadMs);

    /// <summary>True when the clip is over and everything the action left has faded.</summary>
    bool Finished(float playheadMs);

    /// <summary>True when <paramref name="atMs"/> is this performance's beat.</summary>
    bool IsBeat(int atMs);

    /// <summary>Advance to <paramref name="playheadMs"/>.</summary>
    PerformanceStep Update(float playheadMs, float dt, IActionStage stage);

    /// <summary>The level every OTHER one-shot plays at on <paramref name="playheadMs"/>.</summary>
    float DuckAt(float playheadMs);

    /// <summary>What the action holds before it acts (a projectile's bundle), alpha-blended over the figure.</summary>
    void DrawProp(SpriteBatch b, IActionStage stage, float playheadMs);

    /// <summary>The action's material layer (steel, debris), alpha-blended over the figures.</summary>
    void DrawMaterial(SpriteBatch b);

    /// <summary>Everything that is LIGHT, into an additive batch.</summary>
    void DrawLight(SpriteBatch b, float playheadMs, Texture2D? streak);

    /// <summary>The first positions of what the action launched, for the socket overlay.</summary>
    IEnumerable<Vector2> LaunchPoints { get; }

    /// <summary>How many sprites the action submitted last frame (performance metrics).</summary>
    int SpriteCount { get; }

    /// <summary>
    /// PRESENTATION ROOT MOTION: how far the performing figure is carried along x at <paramref name="playheadMs"/>,
    /// read against the timing its clip actually played (<see cref="Retime"/>: a handoff may have compressed or cut
    /// it). A throw stands still; a melee lunge carries the body to its target and back, and its way home may run on
    /// under the next clip, so it is asked even after its own clip has ended.
    /// </summary>
    float RootOffsetX(float playheadMs) => 0f;

    /// <summary>
    /// PRESENTATION ROOT MOTION, vertical: how far the performing figure is lifted off the ground (negative = up) at
    /// <paramref name="playheadMs"/>. A melee return is a HOP back, not a slide: the body leaves the ground and lands
    /// home. His shadow stays on the ground.
    /// </summary>
    float RootOffsetY(float playheadMs) => 0f;

    /// <summary>
    /// The handoff retimed the clip this performance plays (<paramref name="played"/>). When it YIELDED
    /// (<see cref="HandoffFit.Yielded"/>) at <paramref name="yieldAtMs"/>, anything the performance still owes the
    /// figure (a lunge's way home) is finished by <paramref name="returnByMs"/>, under the next action's wind-up.
    /// </summary>
    void Retime(ActionClipTiming played, float? yieldAtMs = null, float returnByMs = 0f)
    {
    }
}

/// <summary>
/// ONE PROJECTILE ACTION BEING PERFORMED (ADR-011): the champion's authored clip, the bundle in the hand,
/// the release from the hand's socket, one blade per struck enemy, and the contact ON THE BEAT.
/// </summary>
/// <remarks>
/// <para>
/// It runs on the fight's playhead, the same clock that crosses the fight's events, so the blades land on
/// exactly the frame the hits are presented — flash, number, health, death — and a held beat freezes the
/// whole action consistently instead of leaving effects running on the wall clock.
/// </para>
/// <para>
/// It changes no outcome: the targets are read from the fight's own resolved events before the release,
/// and every piece of feedback the fight already had still fires from those events at the beat. The
/// performance only decides where and when the picture and the sounds of the throw happen around them.
/// </para>
/// </remarks>
public sealed class ActionPerformance : IActionPerformance
{
    private const int KnifePad = 3;   // the canonical props carry one transparent source pixel (x3) of margin

    private readonly Flight[] _flights;
    private Vector2 _smearFrom, _smearTo;
    private bool _smear;
    // the contact's secondary ticks: where (the outermost targets) and how many are still due
    private readonly Vector2[] _ticks = new Vector2[2];
    private int _tickCount, _ticksPlayed;

    private sealed class Flight
    {
        public int Slot;
        public Vector2 From, To;
        public float Bulge;
        public bool Placed;              // its path is known (the sounds need it even when no picture loaded)
        public ProjectileVisual? Visual;
    }

    /// <summary>The recipe being performed.</summary>
    public ProjectileActionRecipe Recipe { get; }

    IActionRecipe IActionPerformance.Recipe => Recipe;

    /// <summary>The clip's timing, fitted to the time there was before the beat.</summary>
    public ActionClipTiming Timing { get; }

    /// <summary>The fight's beat: the moment of CONTACT, in playhead ms.</summary>
    public float BeatMs { get; }

    /// <summary>When the clip starts (playhead ms).</summary>
    public float ClipStartMs { get; }

    /// <summary>When the blades leave the hand (playhead ms): the beat minus the travel.</summary>
    public float ReleaseMs => BeatMs - Recipe.TravelMs;

    /// <summary>The skill slot whose cast this is.</summary>
    public int SkillSlot { get; }

    /// <summary>The enemies the cast strikes, in the order the fight resolved them: one blade each.</summary>
    public IReadOnlyList<int> Targets { get; }

    /// <summary>The cast's Source colour: the energy in the edge light, trail, sparks and contact.</summary>
    public Color Tint { get; }

    /// <summary>True once the blades have left the hand.</summary>
    public bool Released { get; private set; }

    /// <summary>True once the blades have landed.</summary>
    public bool Contacted { get; private set; }

    /// <summary>A performance of <paramref name="recipe"/> whose contact is the beat at <paramref name="beatMs"/>.</summary>
    /// <param name="timing">The clip timing, already fitted (<see cref="Schedule"/>).</param>
    public ActionPerformance(ProjectileActionRecipe recipe, ActionClipTiming timing, float beatMs, float clipStartMs,
                             int skillSlot, IReadOnlyList<int> targets, Color tint)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        BeatMs = beatMs;
        ClipStartMs = clipStartMs;
        SkillSlot = skillSlot;
        Targets = targets?.ToArray() ?? Array.Empty<int>();
        Tint = tint;
        _flights = Targets.Select(s => new Flight { Slot = s }).ToArray();
    }

    /// <summary>
    /// When to start the clip so its release lands <see cref="ProjectileActionRecipe.TravelMs"/> before the
    /// beat, and the timing to play it with. Null while it is too early to start; a late start compresses the
    /// elastic frames before the release (never the release itself).
    /// </summary>
    /// <param name="nowMs">The playhead now.</param>
    public static (float StartMs, ActionClipTiming Timing)? Schedule(IActionRecipe recipe, ActionClipTiming timing, float beatMs, float nowMs)
    {
        var releaseMs = beatMs - recipe.TravelMs;
        var lead = releaseMs - nowMs;                       // time left before the release
        var needed = timing.MarkerMs(recipe.ReleaseMarker);
        if (lead > needed) return null;                     // not yet: the clip starts exactly `needed` before the release
        var fitted = timing.FitBefore(recipe.ReleaseMarker, Math.Max(0f, lead));
        return (releaseMs - fitted.MarkerMs(recipe.ReleaseMarker), fitted);
    }

    /// <summary>
    /// The least time before its beat the action can start and still play its whole wind-up at the tightest fit
    /// <see cref="Schedule"/> allows: the rigid frames, the elastic ones at their floor, and the flight.
    /// </summary>
    public static float MinLeadMs(IActionRecipe recipe, ActionClipTiming timing)
        => recipe.TravelMs + timing.FitBefore(recipe.ReleaseMarker, 0f).MarkerMs(recipe.ReleaseMarker);

    /// <summary>The clip frame showing at <paramref name="playheadMs"/>.</summary>
    public int FrameAt(float playheadMs) => Timing.FrameAt(playheadMs - ClipStartMs);

    /// <summary>True once the clip has played to its last frame.</summary>
    public bool ClipOver(float playheadMs) => playheadMs >= ClipStartMs + Timing.TotalMs;

    /// <summary>True when the clip is over and everything the blades left has faded.</summary>
    public bool Finished(float playheadMs)
    {
        if (!ClipOver(playheadMs)) return false;
        if (!Released) return true;
        foreach (var f in _flights)
            if (f.Visual is { Finished: false }) return false;
        return true;
    }

    /// <summary>True when <paramref name="e"/>'s timestamp is this performance's beat (the fight event it presents).</summary>
    public bool IsBeat(int atMs) => Math.Abs(atMs - BeatMs) < 1f;

    /// <summary>Advance to <paramref name="playheadMs"/>: release at the release marker, fly, contact at the beat.</summary>
    public PerformanceStep Update(float playheadMs, float dt, IActionStage stage)
    {
        bool released = false, contacted = false;
        Vector2 releaseAt = default, contactAt = default;
        if (!Released && playheadMs >= ReleaseMs)
        {
            Released = true;
            released = true;
            releaseAt = Release(stage);
        }
        if (Released)
        {
            var u = Recipe.TravelMs <= 0f ? 1f : Math.Clamp((playheadMs - ReleaseMs) / Recipe.TravelMs, 0f, 1f);
            foreach (var f in _flights)
            {
                if (f.Visual is not { } v) continue;
                if (!Contacted)
                    v.Drive(dt, u, ProjectileMotion.ThrowPosition(f.From, f.To, f.Bulge, Recipe.Departure, u),
                            ProjectileMotion.ThrowHeading(f.From, f.To, f.Bulge, Recipe.Departure, u));
                else v.Linger(dt);
            }
            if (!Contacted && playheadMs >= BeatMs)
            {
                Contacted = true;
                contacted = true;
                var sum = Vector2.Zero;
                var n = 0;
                foreach (var f in _flights)
                {
                    if (!f.Placed) continue;
                    f.Visual?.Land();
                    sum += f.To;
                    n++;
                }
                contactAt = n > 0 ? sum / n : releaseAt;
                PlanTicks(contactAt);
            }
        }
        Vector2? tick = null;
        if (Contacted && _ticksPlayed < _tickCount
            && playheadMs >= BeatMs + Recipe.ContactTickSpacingMs * (_ticksPlayed + 1))
            tick = _ticks[_ticksPlayed++];
        return new PerformanceStep(released, contacted, releaseAt, contactAt, tick);
    }

    /// <summary>
    /// The level every OTHER one-shot should play at on <paramref name="playheadMs"/>: the recipe's
    /// <see cref="ProjectileActionRecipe.DuckOthers"/> from the release to <see cref="ProjectileActionRecipe.DuckTailMs"/>
    /// after the contact, 1 outside that window.
    /// </summary>
    public float DuckAt(float playheadMs)
        => playheadMs >= ReleaseMs && playheadMs < BeatMs + Recipe.DuckTailMs ? Recipe.DuckOthers : 1f;

    /// <summary>The outermost contacts of a fan of three or more, farthest from its centre first.</summary>
    private void PlanTicks(Vector2 centre)
    {
        _tickCount = 0;
        if (_flights.Length < 3) return;
        var limit = Math.Min(Math.Min(Recipe.ContactTicks, _ticks.Length), _flights.Length - 1);
        for (var k = 0; k < limit; k++)
        {
            float best = -1f;
            var pick = Vector2.Zero;
            foreach (var f in _flights)
            {
                if (!f.Placed) continue;
                var d = Vector2.DistanceSquared(f.To, centre);
                var taken = false;
                for (var j = 0; j < _tickCount; j++) taken |= _ticks[j] == f.To;
                if (!taken && d > best) { best = d; pick = f.To; }
            }
            if (best < 0f) break;
            _ticks[_tickCount++] = pick;
        }
    }

    private Vector2 Release(IActionStage stage)
    {
        var releaseFrame = Timing.Markers[Recipe.ReleaseMarker];
        if (!TryHand(stage, releaseFrame, out var hand, out var angle, out var scale)) return Vector2.Zero;
        // the smear: from where the hand last held the bundle to where it lets go
        var before = Timing.FramesWithSocket(Recipe.HandSocket).Where(f => f < releaseFrame).DefaultIfEmpty(-1).Max();
        if (before >= 0 && TryHand(stage, before, out var coil, out _, out _))
        {
            _smearFrom = coil;
            _smearTo = hand;
            _smear = true;
        }
        var material = stage.Texture(Recipe.PropKey);
        var edge = stage.Texture(Recipe.PropEdgeKey);
        var look = Recipe.Look;
        var trail = stage.Texture(look.TrailKey);
        var glint = stage.Texture(look.GlintKey);
        var spark = stage.Texture(look.SparkKey);
        var shard = stage.Texture(look.ShardKey);
        var flash = stage.Texture(look.FlashKey);
        // Without its pictures the throw is still PLACED — the contact sound and its ticks pan by where the
        // blades land — it just draws nothing.
        var drawable = material is not null && edge is not null && trail is not null && glint is not null
                       && spark is not null && shard is not null && flash is not null;

        // The blades leave the hand AS THE FAN it holds: each starts where its copy in the bundle is, turned by
        // its share of the fan, and flies to its own enemy. The outermost bow apart at mid-flight, ordered by
        // their target's height so the paths never cross.
        var n = _flights.Length;
        var order = _flights.Select((f, i) => (i, y: stage.TryTargetBody(f.Slot, out var body) ? body.Center.Y : 0))
                            .OrderBy(t => t.y).Select(t => t.i).ToArray();
        var centreFromPivot = material is null ? Vector2.Zero : new Vector2(material.Width / 2f, material.Height / 2f) - Recipe.PropPivot;
        var tipFromCentre = material is null ? 0f : (material.Width / 2f - KnifePad) * scale;
        for (var rank = 0; rank < n; rank++)
        {
            var f = _flights[order[rank]];
            if (!stage.TryTargetBody(f.Slot, out var body)) continue;
            var share = n == 1 ? 0f : rank / (float)(n - 1) * 2f - 1f;          // -1 (highest target) .. +1
            var fanAngle = angle + MathHelper.ToRadians(Recipe.PropFanDegrees) * share;
            var from = hand + Rotate(centreFromPivot * scale, fanAngle);
            var jitter = (ProjectileMotion.Hash01(f.Slot, 7) - 0.5f) * 0.14f;
            var contact = new Vector2(body.X + body.Width * Recipe.ContactPoint.X,
                                      body.Y + body.Height * (Recipe.ContactPoint.Y + jitter));
            var line = contact - from;
            var dir = line.LengthSquared() > 1e-6f ? Vector2.Normalize(line) : Vector2.UnitX;
            f.From = from;
            f.To = contact - dir * tipFromCentre;                                  // the TIP meets the contact point
            f.Bulge = Recipe.FanBulge * stage.CasterHeight * share;
            f.Placed = true;
            if (!drawable) continue;
            var v = new ProjectileVisual(look, f.Slot * 97 + 13);
            v.PlaceDriven(f.From, f.To, Tint, material!, edge!, scale, KnifePad, trail!, glint!, spark!, shard!, flash!);
            v.Drive(0f, 0f, f.From, ProjectileMotion.ThrowHeading(f.From, f.To, f.Bulge, Recipe.Departure, 0f));
            f.Visual = v;
        }
        return hand;
    }

    private bool TryHand(IActionStage stage, int frame, out Vector2 hand, out float angle, out float scale)
    {
        hand = default;
        angle = 0f;
        scale = 1f;
        if (Timing.Socket(frame, Recipe.HandSocket) is not { } socket) return false;
        if (!stage.TryActorFrame(Recipe.ClipKey, frame, out var drawn, out var size)) return false;
        hand = ActorSocketMap.ToArena(drawn, size, frame, socket);
        angle = ActorSocketMap.AngleInArena(socket, drawn.Effects.HasFlag(SpriteEffects.FlipHorizontally));
        scale = ActorSocketMap.DrawScale(drawn);
        return true;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var (s, c) = MathF.SinCos(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    /// <summary>
    /// The bundle in the hand, before the release: <see cref="ProjectileActionRecipe.PropBundle"/> copies of the
    /// SAME knife that will fly, fanned about the socket on every frame that authors one. Alpha-blended.
    /// </summary>
    public void DrawProp(SpriteBatch b, IActionStage stage, float playheadMs)
    {
        if (Released) return;
        var frame = FrameAt(playheadMs);
        if (!TryHand(stage, frame, out var hand, out var angle, out var scale)) return;
        if (stage.Texture(Recipe.PropKey) is not { } material) return;
        var k = Math.Max(1, Recipe.PropBundle);
        for (var i = 0; i < k; i++)
        {
            var share = k == 1 ? 0f : i / (float)(k - 1) * 2f - 1f;
            var a = angle + MathHelper.ToRadians(Recipe.PropFanDegrees) * share;
            b.Draw(material, hand, null, Color.White, a, Recipe.PropPivot, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>The flying blades' material (steel), alpha-blended, over the figures.</summary>
    public void DrawMaterial(SpriteBatch b)
    {
        foreach (var f in _flights) f.Visual?.DrawMaterial(b);
    }

    /// <summary>
    /// Everything that is LIGHT, into an additive batch: the release smear, then each blade's edge, glint,
    /// trail, sparks and contact.
    /// </summary>
    public void DrawLight(SpriteBatch b, float playheadMs, Texture2D? streak)
    {
        if (_smear && streak is not null)
        {
            // THE RELEASE ACCENT: the last stretch of the hand's path, arriving at the release point — drawn from
            // the two real hand positions, not invented by an interpolator. Only the END: the whole path from the
            // coil runs through the head (a full-length streak read as a beam from the eye), and the accent starts
            // ON the release frame, because before it the hand is still up behind the head.
            var t = (playheadMs - ReleaseMs) / Recipe.SmearMs;
            if (t is >= 0f and < 1f)
            {
                var keep = 1f - t * t;
                var span = Vector2.Distance(_smearFrom, _smearTo);
                var over = new Vector2((_smearFrom.X + _smearTo.X) / 2f,
                                       MathF.Min(_smearFrom.Y, _smearTo.Y) - span * Recipe.SmearLift);
                var from = 1f - Math.Clamp(Recipe.SmearTail, 0.02f, 1f);
                var prev = Bezier(_smearFrom, over, _smearTo, from);
                // three pieces, thin and faint to thick and bright: the arm is fastest where it lets go
                for (var k = 1; k <= 3; k++)
                {
                    var next = Bezier(_smearFrom, over, _smearTo, from + (1f - from) * k / 3f);
                    ArcSegment(b, streak, prev, next, keep * (0.15f + 0.2f * k), 4f + 4f * k);
                    prev = next;
                }
            }
        }
        foreach (var f in _flights) f.Visual?.Draw(b);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
    {
        var u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    private void ArcSegment(SpriteBatch b, Texture2D tex, Vector2 from, Vector2 to, float opacity, float width)
    {
        var d = to - from;
        var len = d.Length();
        if (len < 1f) return;
        var colour = Color.Lerp(new Color(200, 210, 225), Tint, 0.35f) * opacity;
        b.Draw(tex, from, null, VfxBlend.Light(colour), MathF.Atan2(d.Y, d.X), new Vector2(0f, tex.Height / 2f),
               new Vector2(len / tex.Width, width / tex.Height), SpriteEffects.None, 0f);
    }

    /// <summary>The first positions of the blades (after <see cref="Released"/>), for the socket overlay.</summary>
    public IEnumerable<Vector2> LaunchPoints => _flights.Where(f => f.Visual is not null).Select(f => f.From);

    /// <summary>How many sprites the blades submitted last frame (performance metrics).</summary>
    public int SpriteCount
    {
        get
        {
            var n = 0;
            foreach (var f in _flights) n += f.Visual?.LastSprites ?? 0;
            return n;
        }
    }
}
