using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// A PERSISTENT FIELD, performed (ADR-011, the FIELD / AURA archetype; <see cref="FieldRecipe"/> says what and when). One
/// per wave, built from the wave's own Aura events for the field's slot, each with the creature its tick affected. It
/// draws on the reaction layer's passes and never touches the champion's figure; it asks the screen for one thing about
/// the creatures, <see cref="Squash"/>, the crushed one's body buckling for a moment.
/// </summary>
/// <remarks>
/// Every curve is a pure function of u = playhead - the tick's ms (tested); the draw allocates nothing.
/// </remarks>
public sealed class FieldPerformance
{
    /// <summary>What the field is, and when each state happens.</summary>
    public FieldRecipe Recipe { get; }

    /// <summary>The field's skill slot (what its Aura events index).</summary>
    public int Slot { get; }

    private readonly float[] _tickAt;
    private readonly int[] _tickTarget;
    private readonly bool[] _tickYields;
    private readonly bool[] _tickQuiet;
    // each tick's target silhouette, pinned once as shares of its layout body (left, top, right, bottom): the layout body
    // holds the whole canvas (a lunge's extended one too), and the arcs pressed 250 px over the heads
    private readonly Vector4?[] _shape;
    // ...and pinned again one frame into the crush (the arcs press a creature caught in its own lunge where it is drawn)
    private readonly Vector4?[] _crush;
    // ...and the arcs' width in px, from the silhouette pinned at launch (their size never follows a pose)
    private readonly float[] _arcWidth;
    // ...and whether its fold has been drawn (the first frame after the contact folds, even a slow one), and the frame the
    // material pass folded on (the light pass holds the arcs' heat back on it)
    private readonly bool[] _folded;
    // (by a frame count, never by the playhead: on the frozen playhead that ends a wave every frame matched it, and the
    // fold's lit edge was drawn again and again without its body)
    private int _frame, _foldedFrame = -1;
    // each tick's cue: asked once as the playhead crosses its moment (a rewind before it re-arms it)
    private readonly bool[] _cued;
    // ...and the arcs' horizontal centre, latched on contact: forward motion dies there (followed, the arcs drifted on with
    // the creature through the hold and onto the next one's head)
    private readonly float[] _arcX;
    private int _sprites;

    /// <summary>
    /// The ticks this wave will present: the Aura events' ms, the slot each one's Break names (or -1), and whether a
    /// presented reaction plays around it (the crush then gives way: <see cref="FieldRecipe.YieldBeforeMs"/>), and whether an
    /// action's contact lands close to it (the tick is then quiet: <see cref="FieldRecipe.QuietBeforeMs"/>).
    /// </summary>
    public FieldPerformance(FieldRecipe recipe, int slot, IReadOnlyList<(float AtMs, int Target, bool Yields, bool Quiet)> ticks)
    {
        Recipe = recipe;
        Slot = slot;
        _tickAt = new float[ticks.Count];
        _tickTarget = new int[ticks.Count];
        _tickYields = new bool[ticks.Count];
        _tickQuiet = new bool[ticks.Count];
        _shape = new Vector4?[ticks.Count];
        _crush = new Vector4?[ticks.Count];
        _arcWidth = new float[ticks.Count];
        _folded = new bool[ticks.Count];
        _arcX = new float[ticks.Count];
        _cued = new bool[ticks.Count];
        Array.Fill(_arcX, float.NaN);
        for (var k = 0; k < ticks.Count; k++) (_tickAt[k], _tickTarget[k], _tickYields[k], _tickQuiet[k]) = ticks[k];
    }

    /// <summary>Is the k-th tick quiet (an action's contact lands close to it)?</summary>
    public bool Quiet(int tick) => tick >= 0 && _tickQuiet[tick];

    private float QuietShareOf(IReactionStage stage, int tick) => stage.ChampionPerforming || Quiet(tick) ? Recipe.QuietShare : 1f;

    /// <summary>Does the k-th tick give way to a reaction on its creature (no arcs; the front flattens against its side)?</summary>
    public bool Yields(int tick) => tick >= 0 && _tickYields[tick];

    /// <summary>How many ticks this wave presents.</summary>
    public int TickCount => _tickAt.Length;

    /// <summary>The ms of the k-th tick.</summary>
    public float TickAt(int k) => _tickAt[k];

    /// <summary>
    /// Is a creature still standing (the fallback target's rule: the first one standing, as the fight picks it)? Set once by
    /// the screen (a cached delegate, so the draw allocates nothing).
    /// </summary>
    public Func<int, bool>? IsStanding { get; set; }

    /// <summary>How many creature slots there are (for the fallback target).</summary>
    public int CreatureSlots { get; set; }

    /// <summary>The target's drawn shape the last material pass used (the trace).</summary>
    public Rectangle LastShape { get; private set; }

    /// <summary>Sprites drawn this frame (the trace's budget line).</summary>
    public int SpriteCount => _sprites;

    // ── THE CURVES (pure) ─────────────────────────────────────────────────────────────────────────────

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>The field's size and opacity.</summary>
    public readonly record struct FieldPose(float Scale, float Alpha);

    /// <summary>
    /// The field at u (NaN when no phrase is near: at rest): quiet at rest (breathing with <paramref name="breath"/> in
    /// -1..1), drawing IN and gathering before the tick (the compression), springing back OUT as it lets the front go,
    /// quiet again.
    /// </summary>
    public static FieldPose Field(FieldRecipe r, float u, float breath)
    {
        var rest = r.RestAlpha + r.BreathAlpha * breath;
        if (float.IsNaN(u) || u < r.ContractFromMs || u > r.EndMs) return new FieldPose(1f, rest);
        if (u < r.LaunchMs)
        {
            var k = Smooth((u - r.ContractFromMs) / (r.LaunchMs - r.ContractFromMs));
            return new FieldPose(MathHelper.Lerp(1f, r.ContractScale, k), MathHelper.Lerp(rest, r.ContractAlpha, k));
        }
        var t = (u - r.LaunchMs) / Math.Max(1f, r.SpringMs);
        if (t >= 1f) return new FieldPose(1f, rest);
        // SNAPS out past its size in the first quarter (a hard ease-out), settles over the rest; the gathered density lets go
        var scale = t < 0.25f
            ? MathHelper.Lerp(r.ContractScale, r.SpringScale, 1f - (1f - t / 0.25f) * (1f - t / 0.25f) * (1f - t / 0.25f))
            : MathHelper.Lerp(r.SpringScale, 1f, Smooth((t - 0.25f) / 0.75f));
        return new FieldPose(scale, MathHelper.Lerp(r.ContractAlpha, rest, Smooth(t)));
    }

    /// <summary>The pressure front: how far it has come (0 the Seeker, 1 the target), how far it has grown (0 its launch height, 1 its arrival height), its opacity.</summary>
    public readonly record struct WavePose(float Progress, float Grow, float Alpha);

    /// <summary>
    /// The front at u: it leaves at <see cref="FieldRecipe.LaunchMs"/> and ARRIVES on the tick (u = 0), ACCELERATING into the
    /// contact (the largest step is the last), growing and gathering opacity on the way; then it collapses into the crush.
    /// </summary>
    public static WavePose Wave(FieldRecipe r, float u)
    {
        if (float.IsNaN(u) || u < r.LaunchMs || u > r.WaveCollapseMs) return default;
        if (u <= 0f)
        {
            var t = (u - r.LaunchMs) / -r.LaunchMs;
            var p = MathF.Pow(t, r.TravelEasePower);
            var alpha = MathHelper.Lerp(r.WaveLaunchAlpha, r.WaveArriveAlpha, t) * Math.Clamp((u - r.LaunchMs) / 34f, 0f, 1f);
            return new WavePose(p, t, alpha);
        }
        var c = u / Math.Max(1f, r.WaveCollapseMs);
        return new WavePose(1f, 1f + 0.08f * c, r.WaveArriveAlpha * (1f - c) * (1f - c));
    }

    /// <summary>
    /// The front's height in px: <see cref="FieldRecipe.WaveArriveShare"/> of the target's silhouette on arrival, and
    /// <see cref="FieldRecipe.WaveLaunchShare"/> of that as it leaves (<paramref name="grow"/> 0..1, and a little past 1 as
    /// it collapses). The Seeker's height no longer sizes it: its art pixel stays near the world's in flight.
    /// </summary>
    public static float WaveHeight(FieldRecipe r, float grow, float champHeight, float targetHeight)
    {
        var to = r.WaveArriveShare * targetHeight;
        var from = r.WaveLaunchShare * to;
        return grow <= 1f ? MathHelper.Lerp(from, to, grow) : to * grow;
    }

    /// <summary>The crush: how shut the arcs are (0 open, 1 shut), their opacity, the body's buckle (0..1), their heat (1 hot, 0 cool).</summary>
    public readonly record struct CrushPose(float Close, float Alpha, float Squash, float Heat);

    /// <summary>
    /// The crush at u: from the tick the arcs press in (accelerating, <see cref="FieldRecipe.CrushInMs"/>), hold, and let go
    /// (easing back a little as they fade); the body buckles with them and recovers early in the release; their pressing
    /// edge hot for the first frames (<see cref="CrushPose.Heat"/>), the arcs cooling to violet.
    /// </summary>
    public static CrushPose Crush(FieldRecipe r, float u)
    {
        if (float.IsNaN(u) || u < 0f || u > r.EndMs) return default;
        var holdEnd = r.CrushInMs + r.CrushHoldMs;
        var outEnd = holdEnd + r.CrushOutMs;
        float close, alpha, squash;
        if (u < r.CrushInMs)
        {
            // HARD: arrival -> the compressed pose in two frames (an ease-out: the tips fold over and under at once)
            var k = u / r.CrushInMs;
            close = 1f - (1f - k) * (1f - k);
            alpha = r.ClampPeakAlpha * Math.Clamp(0.7f + 0.3f * k * 2f, 0f, 1f);
            squash = close;
        }
        else if (u < holdEnd)
        {
            close = 1f;
            alpha = r.ClampPeakAlpha;
            squash = 1f;
        }
        else if (u < outEnd)
        {
            var k = (u - holdEnd) / r.CrushOutMs;
            close = 1f - 0.25f * Smooth(k);
            alpha = r.ClampPeakAlpha * (1f - 0.55f * Smooth(k));   // the release is carried by the chunk dissolve, not a long fade
            squash = 1f - Smooth(Math.Min(1f, k / 0.5f));
        }
        else return default;
        var heat = 1f - Smooth(Math.Max(0f, u - r.FoldMs) / 90f);    // hot as the arcs take over from the fold
        return new CrushPose(close, alpha, squash, heat);
    }

    /// <summary>
    /// The fold's moment: the FIRST frame drawn after the contact inside it folds, once (a frame phase never shows it twice
    /// or skips it: at 60 fps a fold window longer than a frame showed it twice on an early phase).
    /// </summary>
    public static bool CanFold(FieldRecipe r, float u) => u > 0f && u <= r.FoldMs + r.CrushInMs;

    /// <summary>The clamp's cell at u: whole through the press and the hold, then the three dissolve states over the release.</summary>
    public static int ClampCellAt(FieldRecipe r, float u)
    {
        var holdEnd = r.CrushInMs + r.CrushHoldMs;
        if (u < holdEnd) return 0;
        var k = Math.Clamp((u - holdEnd) / Math.Max(1f, r.CrushOutMs), 0f, 0.999f);
        return 1 + (int)(k * (FieldRecipe.ClampEdgeCell - 1));   // the release states sit between the whole cell and the edge cell
    }

    /// <summary>The flattened front's opacity share at u on a tick that gives way (1 on the tick, gone by <see cref="FieldRecipe.FlattenMs"/>).</summary>
    public static float FlattenFade(FieldRecipe r, float u) => 1f - Smooth(u / Math.Max(1f, r.FlattenMs));

    /// <summary>The body's drawn scale (width, height) at u: 1 outside the crush.</summary>
    public static Vector2 SquashAt(FieldRecipe r, float u)
    {
        var s = Crush(r, u).Squash;
        return new Vector2(MathHelper.Lerp(1f, r.SquashX, s), MathHelper.Lerp(1f, r.SquashY, s));
    }

    /// <summary>
    /// The crush arcs' body colour at u: the arcs' violet, cooling as they let go. The tick's heat never tints the body (the
    /// whole arc lit pale collapsed its value bands into one); it lights the pressing edge in the additive pass.
    /// </summary>
    public static Color ClampTint(FieldRecipe r, float u)
    {
        var cool = Math.Clamp((u - r.CrushInMs - r.CrushHoldMs) / Math.Max(1f, r.CrushOutMs), 0f, 1f);
        return Color.Lerp(r.ClampColor, r.ClampCoolColor, cool);
    }

    // ── THE VOICE ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The tick whose cue starts now (its moment, <see cref="FieldRecipe.CueStartMs"/> from the tick, has just been
    /// crossed), or -1. Each tick's cue is asked once; a rewind before its moment re-arms it; a moment passed by more than
    /// <see cref="FieldRecipe.CueLateMs"/> (a seek, a long hitch) is skipped rather than played late.
    /// </summary>
    public int CueDue(float playheadMs)
    {
        var due = -1;
        for (var k = 0; k < _tickAt.Length; k++)
        {
            var at = _tickAt[k] + Recipe.CueStartMs;
            if (playheadMs < at - 1f) { _cued[k] = false; continue; }
            if (_cued[k]) continue;
            _cued[k] = true;
            if (playheadMs <= at + Recipe.CueLateMs && due < 0) due = k;
        }
        return due;
    }

    /// <summary>
    /// The k-th tick cue's volume: quieter on a quiet tick, while the champion is <paramref name="performing"/> an action
    /// (the picture's own quiet rule) and on one that gives way to a reaction (JAWS). A quiet tick already
    /// <paramref name="ducked"/> by an authored action takes the duck alone (the product buried its crack).
    /// </summary>
    public float CueVolume(int tick, bool ducked = false, bool performing = false)
        => Recipe.TickVolume * (Yields(tick) ? Recipe.CueYieldShare
            : (Quiet(tick) || performing) && !ducked ? Recipe.CueQuietShare : 1f);

    /// <summary>Is the k-th tick's crush still playing at the playhead (from the tick to the phrase's end)?</summary>
    public bool Crushing(float playheadMs) => TryPhrase(playheadMs, out var u, out _) && u >= 0f && u < Recipe.EndMs;

    // ── WHICH TICK, WHICH CREATURE ─────────────────────────────────────────────────────────────────────

    /// <summary>The phrase the playhead is in (u, and the tick's index), or false: the field is at rest.</summary>
    public bool TryPhrase(float playheadMs, out float u, out int tick)
    {
        for (var k = 0; k < _tickAt.Length; k++)
        {
            var d = playheadMs - _tickAt[k];
            if (d >= Recipe.ContractFromMs && d <= Recipe.EndMs)
            {
                u = d;
                tick = k;
                return true;
            }
        }
        u = float.NaN;
        tick = -1;
        return false;
    }

    /// <summary>The creature the k-th tick crushes: the one its Break names, else the first still standing (the fight's rule).</summary>
    public int TargetOf(int tick)
    {
        if (tick < 0) return -1;
        if (_tickTarget[tick] >= 0) return _tickTarget[tick];
        for (var s = 0; s < CreatureSlots; s++)
            if (IsStanding?.Invoke(s) ?? s == 0) return s;
        return -1;
    }

    /// <summary>The crushed creature's drawn scale this frame (width, height; feet on the floor), or (1, 1).</summary>
    public Vector2 Squash(int slot, float playheadMs)
        => TryPhrase(playheadMs, out var u, out var k) && TargetOf(k) == slot ? SquashAt(Recipe, u) : Vector2.One;

    // ── THE DRAW (three passes; nothing allocated) ─────────────────────────────────────────────────────

    /// <summary>
    /// UNDER the figures (after the effects' own under-pass): the field around the Seeker, behind him. Quiet at rest;
    /// gathering before a tick; springing out as it lets the front go.
    /// </summary>
    public void DrawUnder(SpriteBatch b, IReactionStage stage, float playheadMs, float wallSeconds)
    {
        _sprites = 0;
        if (stage.Texture(Recipe.FieldKey) is not { } tex || !stage.TryChampionBody(out var body)) return;
        TryPhrase(playheadMs, out var u, out _);
        var pose = Field(Recipe, u, MathF.Sin(wallSeconds * MathHelper.TwoPi * Recipe.BreathHz));
        if (pose.Alpha < ReactionRecipe.VisibleFloor) return;
        DrawField(b, tex, body, pose, Recipe.FieldColor * pose.Alpha);
    }

    private void DrawField(SpriteBatch b, Texture2D tex, Rectangle body, FieldPose pose, Color tint)
    {
        var at = new Vector2(body.X + Recipe.FieldCentre.X * body.Width, body.Y + Recipe.FieldCentre.Y * body.Height);
        var scale = body.Height * Recipe.FieldHeightShare * pose.Scale / Recipe.FieldCell.Y;
        b.Draw(tex, at, null, tint, 0f, new Vector2(Recipe.FieldCell.X, Recipe.FieldCell.Y) * 0.5f, scale, SpriteEffects.None, 0f);
        _sprites++;
    }

    /// <summary>
    /// Over the creatures, UNDER the champion: the pressure front on its way, the fold it becomes, and the two crush arcs on
    /// the target (material: translucent violet).
    /// </summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _frame++;                                                                 // one material pass a frame
        if (!TryPhrase(playheadMs, out var u, out var tick) || u < Recipe.LaunchMs) return;
        var target = TargetOf(tick);
        // THE TARGET'S DRAWN SHAPE (called after the creatures drew, so their frames are this frame's): pinned at launch for
        // the front's travel, and pinned AGAIN one frame into the crush, where the creature is drawn as it is pressed -- one
        // caught in its own lunge is pressed there (on the launch shape the arcs sat behind its head and on the next
        // creature); pinned, not followed: followed frame by frame the arcs jumped with every lunge frame mid-hold
        if (target >= 0 && _shape[tick] is null && TryProbeShares(stage, target, out var launch)) _shape[tick] = launch;
        if (target >= 0 && u >= 0.5f * Recipe.CrushInMs && _crush[tick] is null && TryProbeShares(stage, target, out var now)) _crush[tick] = now;
        if (!TryTargetShape(stage, tick, target, false, out var foe) || !stage.TryChampionBody(out var champ)) return;
        if (_arcWidth[tick] <= 0f) _arcWidth[tick] = foe.Width * Recipe.ClampWidthShare;
        var body = PressedShape(stage, tick, target, u);
        LastShape = u >= 0f ? body : foe;
        // WHERE THE FRONT STOPPED, latched on the first frame after the contact: the fold and the arcs form there, and nothing
        // moves forward after it
        if (u > 0f && float.IsNaN(_arcX[tick])) _arcX[tick] = ClampPlace(Recipe, champ, foe, 1f, _arcWidth[tick]).Centre.X;
        var quiet = QuietShareOf(stage, tick);
        if (stage.Texture(Recipe.WaveKey) is { } wave)
        {
            var w = Wave(Recipe, u);
            if (w.Alpha >= ReactionRecipe.VisibleFloor)
            {
                // arrived, it STOPS: on a tick that gives way it loses cohesion against the creature's face; otherwise it
                // does not linger or fade out -- it folds into the crush (below)
                if (u <= 0f || Yields(tick))
                    DrawWaveCell(b, wave, u <= 0f ? FieldRecipe.WaveBodyCell : FieldRecipe.WaveDissolveCell, champ, foe, w.Progress, w.Grow,
                                 Recipe.WaveColor * (w.Alpha * quiet));
            }
        }
        var c = Crush(Recipe, u);
        if (c.Alpha < ReactionRecipe.VisibleFloor) return;
        if (!Yields(tick) && !_folded[tick] && CanFold(Recipe, u))
        {
            _folded[tick] = true;
            _foldedFrame = _frame;
            // THE FRONT BECOMES THE CRUSH: in its own violet and weight, over the arcs' own span (forward motion dead), its
            // wall collapsing at the creature's middle, its ends bent over and under it -- the arcs take over next frame
            if (stage.Texture(Recipe.FoldKey) is { } fold)
                DrawFold(b, fold, FieldRecipe.FoldBodyCell, stage, tick, target, champ, foe, Recipe.WaveColor * (Recipe.WaveArriveAlpha * quiet));
            return;
        }
        if (Yields(tick))
        {
            // giving way to a reaction on this creature: the front flattens against its facing side in its own violet
            // (the hot tint over the whole dissolve cell was a pale slab competing with the bite)
            if (stage.Texture(Recipe.WaveKey) is { } flat)
                DrawFlattened(b, flat, FieldRecipe.WaveDissolveCell, champ, foe, Recipe.WaveColor * (c.Alpha * quiet * FlattenFade(Recipe, u)));
        }
        else if (stage.Texture(Recipe.ClampKey) is { } clamp)
            DrawClamps(b, clamp, ClampCellAt(Recipe, u), champ, body, ArcX(tick, champ, foe), _arcWidth[tick], c, ClampTint(Recipe, u) * (c.Alpha * quiet));
    }

    /// <summary>
    /// In the shared additive pass: the front's rim and, on the tick, the arcs' pressing edges flaring (the phrase's
    /// brightness peak is the crush).
    /// </summary>
    public void DrawLight(SpriteBatch b, IReactionStage stage, float playheadMs, float wallSeconds)
    {
        // the field's edge, faintly, so the quiet state reads on dark stone (always, not only in a phrase)
        TryPhrase(playheadMs, out var fu, out _);
        if (stage.Texture(Recipe.FieldKey) is { } fieldTex && stage.TryChampionBody(out var me))
        {
            var pose = Field(Recipe, fu, MathF.Sin(wallSeconds * MathHelper.TwoPi * Recipe.BreathHz));
            if (pose.Alpha * Recipe.FieldGlowShare >= ReactionRecipe.VisibleFloor)
                DrawField(b, fieldTex, me, pose, Recipe.FieldColor * (pose.Alpha * Recipe.FieldGlowShare));
        }
        if (!TryPhrase(playheadMs, out var u, out var tick) || u < Recipe.LaunchMs) return;
        if (!TryTargetShape(stage, tick, TargetOf(tick), false, out var foe) || !stage.TryChampionBody(out var champ)) return;
        var body = PressedShape(stage, tick, TargetOf(tick), u);
        var quiet = QuietShareOf(stage, tick);
        if (stage.Texture(Recipe.WaveKey) is { } wave)
        {
            var w = Wave(Recipe, u);
            var rim = Recipe.WaveGlowShare * MathHelper.Lerp(Recipe.WaveTravelGlow, 1f, Math.Min(1f, w.Grow));
            if (u <= 0f && w.Alpha * rim >= ReactionRecipe.VisibleFloor)
                DrawWaveCell(b, wave, FieldRecipe.WaveEdgeCell, champ, foe, w.Progress, w.Grow, Recipe.RimColor * (w.Alpha * rim * quiet));
        }
        var c = Crush(Recipe, u);
        var flare = c.Alpha * c.Heat * quiet;
        if (Yields(tick))
        {
            // only the leading edge stays lit, continuing the arrival rim (a line, never a flash over the target)
            if (flare * Recipe.WaveGlowShare >= ReactionRecipe.VisibleFloor && stage.Texture(Recipe.WaveKey) is { } flat)
                DrawFlattened(b, flat, FieldRecipe.WaveEdgeCell, champ, foe, Recipe.RimColor * (flare * Recipe.WaveGlowShare * FlattenFade(Recipe, u)));
            return;
        }
        // the fold is lit like the front it is (its inner edge at the arrival rim's light: never brighter, no flash)
        if (_foldedFrame == _frame)
        {
            if (stage.Texture(Recipe.FoldKey) is { } fold)
                DrawFold(b, fold, FieldRecipe.FoldEdgeCell, stage, tick, TargetOf(tick), champ, foe,
                         Recipe.RimColor * (Recipe.WaveArriveAlpha * Recipe.WaveGlowShare * Recipe.FoldGlowShare * quiet));
            return;
        }
        // the heat lights the pressing EDGE only: a dim line and a short pale accent at the contact (the body stays violet),
        // once the arcs have taken over from the fold
        if (flare * Recipe.ClampGlowShare >= ReactionRecipe.VisibleFloor && stage.Texture(Recipe.ClampKey) is { } clamp)
            DrawClamps(b, clamp, FieldRecipe.ClampEdgeCell, champ, body, ArcX(tick, champ, foe), _arcWidth[tick], c,
                       Recipe.ClampHotColor * (flare * Recipe.ClampGlowShare));
    }

    /// <summary>The fold's and the arcs' horizontal centre: where the front stopped, latched on contact.</summary>
    private float ArcX(int tick, Rectangle champ, Rectangle foe)
        => float.IsNaN(_arcX[tick]) ? ClampPlace(Recipe, champ, foe, 1f, _arcWidth[tick]).Centre.X : _arcX[tick];

    /// <summary>
    /// Where the arcs press: the target's drawn body as it is pressed (the silhouette re-pinned one frame into the crush),
    /// BUCKLED as the body buckles, about its feet -- so the arcs press it down with it and touch it.
    /// </summary>
    private Rectangle PressedShape(IReactionStage stage, int tick, int target, float u)
    {
        TryTargetShape(stage, tick, target, true, out var body);
        return UiKit.Buckle(body, SquashAt(Recipe, u));
    }

    /// <summary>The target's drawn silhouette this frame as shares of its layout body (measured once per frame rectangle).</summary>
    private static bool TryProbeShares(IReactionStage stage, int target, out Vector4 shares)
    {
        shares = new Vector4(0f, 0f, 1f, 1f);
        if (!stage.TryTargetBody(target, out var layout) || layout.Width <= 0 || layout.Height <= 0) return false;
        if (stage.TryTargetFrame(target, out var frame) && SilhouetteProbe.OpaqueBounds(frame) is { Width: > 0, Height: > 0 } s)
            shares = new Vector4(Math.Clamp((s.X - layout.X) / (float)layout.Width, -0.5f, 1f),
                                 Math.Clamp((s.Y - layout.Y) / (float)layout.Height, -0.5f, 1f),
                                 Math.Clamp((s.Right - layout.X) / (float)layout.Width, 0f, 1.5f),
                                 Math.Clamp((s.Bottom - layout.Y) / (float)layout.Height, 0f, 1.5f));
        return true;
    }

    /// <summary>
    /// The target's drawn shape this frame: its layout body cut to the silhouette pinned for this tick at launch (the
    /// front's travel), or, for the <paramref name="crush"/>, the one measured this frame (the pinned one until then).
    /// </summary>
    private bool TryTargetShape(IReactionStage stage, int tick, int target, bool crush, out Rectangle shape)
    {
        shape = default;
        if (target < 0 || !stage.TryTargetBody(target, out var layout) || layout.Height <= 0) return false;
        var sh = (crush ? _crush[tick] ?? _shape[tick] : _shape[tick]) ?? new Vector4(0f, 0f, 1f, 1f);
        shape = new Rectangle((int)(layout.X + sh.X * layout.Width), (int)(layout.Y + sh.Y * layout.Height),
                              Math.Max(1, (int)((sh.Z - sh.X) * layout.Width)), Math.Max(1, (int)((sh.W - sh.Y) * layout.Height)));
        return true;
    }

    /// <summary>
    /// Where the front leaves from (the FIELD's leading edge, in front of the Seeker's enemy-facing side: the wave visibly
    /// leaves the field) and where it lands (the target's front). LEVEL: the front propagates along the arena's horizontal
    /// combat axis, between the field's height and the target's (<see cref="FieldRecipe.WaveAxisShare"/>) -- it never
    /// climbs or dips toward the target's centre (steered at it, the wall read as a projectile homing on a target); the
    /// target is crushed because the wall reaches where it stands.
    /// </summary>
    public static (Vector2 From, Vector2 To) Path(FieldRecipe r, Rectangle champ, Rectangle foe)
    {
        var fieldWidth = champ.Height * r.FieldHeightShare * r.FieldCell.X / r.FieldCell.Y;
        var axis = MathHelper.Lerp(champ.Y + r.FieldCentre.Y * champ.Height, foe.Y + 0.5f * foe.Height, r.WaveAxisShare);
        // ...never so high or low that the arriving wall leaves its target's head or feet outside it (a short creature)
        var half = 0.5f * WaveHeight(r, 1f, champ.Height, foe.Height);
        axis = Math.Clamp(axis, foe.Bottom - half, Math.Max(foe.Bottom - half, foe.Y + half));
        return (new Vector2(champ.X + r.FieldCentre.X * champ.Width + r.FieldEdgeShare * fieldWidth, axis),
                new Vector2(foe.X + 0.12f * foe.Width, axis));
    }

    private void DrawWaveCell(SpriteBatch b, Texture2D tex, int cell, Rectangle champ, Rectangle foe, float progress,
                              float grow, Color tint)
    {
        var (from, to) = Path(Recipe, champ, foe);
        var at = Vector2.Lerp(from, to, progress);
        var scale = WaveHeight(Recipe, grow, champ.Height, foe.Height) / Recipe.WaveCell.Y;
        var src = new Rectangle(cell * Recipe.WaveCell.X, 0, Recipe.WaveCell.X, Recipe.WaveCell.Y);
        // AXIS-ALIGNED on whole screen pixels: turned along its path (~12 degrees on this stage) the NEAREST grid tilted off
        // the stage's and its lit rim broke into a serrated saw-tooth (the crescent already faces the enemy)
        b.Draw(tex, new Rectangle((int)MathF.Round(at.X - Recipe.WaveLeadX * scale), (int)MathF.Round(at.Y - 0.5f * Recipe.WaveCell.Y * scale),
                                  (int)MathF.Round(Recipe.WaveCell.X * scale), (int)MathF.Round(Recipe.WaveCell.Y * scale)), src, tint);
        _sprites++;
    }

    /// <summary>
    /// The crush arcs' centre and the distance of their pressing edges from it, for a crush <paramref name="close"/> (0 at
    /// the arriving crescent's tips, 1 over and under the target's body, biting into it), for arcs <paramref name="width"/>
    /// wide. The centre is where the FRONT STOPPED (<see cref="FieldRecipe.FoldBackShare"/> of the width behind its stop) and
    /// never moves: forward motion dies on contact and the energy turns VERTICAL (the upper arc presses down, the lower one
    /// up; sliding on from the front's arrival, the arcs carried the travel on through the crush).
    /// </summary>
    public static (Vector2 Centre, float Edge) ClampPlace(FieldRecipe r, Rectangle champ, Rectangle foe, float close, float width)
    {
        var shut = new Vector2(Path(r, champ, foe).To.X + (0.5f - r.FoldBackShare) * width, foe.Y + 0.5f * foe.Height);
        var tips = Math.Min(0.5f * WaveHeight(r, 1f, champ.Height, foe.Height), r.ClampStartCap * foe.Height);
        return (shut, MathHelper.Lerp(tips, r.ClampShutShare * foe.Height, close));
    }

    /// <summary>
    /// The fold: over the arcs' own span (their width and latched centre: it becomes them in place), around the creature's
    /// UPRIGHT middle (anchored to the buckle it slid down with it, the lower end moving away from the body); at the arcs'
    /// scale (their pixel), whole pixels, the lower piece flipped.
    /// </summary>
    private void DrawFold(SpriteBatch b, Texture2D tex, int cell, IReactionStage stage, int tick, int target, Rectangle champ, Rectangle foe, Color tint)
    {
        TryTargetShape(stage, tick, target, true, out var upright);
        var w = Math.Max(1, (int)MathF.Round(_arcWidth[tick]));
        var h = Math.Max(1, (int)MathF.Round(Recipe.FoldCell.Y * (w / (float)Recipe.FoldCell.X)));
        var x = (int)MathF.Round(ArcX(tick, champ, foe) - 0.5f * w);
        var mid = (int)MathF.Round(upright.Y + 0.5f * upright.Height);
        var src = new Rectangle(cell * Recipe.FoldCell.X, 0, Recipe.FoldCell.X, Recipe.FoldCell.Y);
        b.Draw(tex, new Rectangle(x, mid - h, w, h), src, tint);
        b.Draw(tex, new Rectangle(x, mid, w, h), src, tint, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
        _sprites += 2;
    }

    private void DrawFlattened(SpriteBatch b, Texture2D tex, int cell, Rectangle champ, Rectangle foe, Color tint)
    {
        var (_, to) = Path(Recipe, champ, foe);
        var scale = WaveHeight(Recipe, 1f, champ.Height, foe.Height) / Recipe.WaveCell.Y;
        // against the creature's face it loses cohesion (the dissolve cell, at its own scale: a squeeze to half its width
        // crushed its pixels back into a smooth vector lens), on whole pixels
        b.Draw(tex, new Rectangle((int)MathF.Round(to.X - Recipe.WaveLeadX * scale), (int)MathF.Round(to.Y - 0.5f * Recipe.WaveCell.Y * scale),
                                  (int)MathF.Round(Recipe.WaveCell.X * scale), (int)MathF.Round(Recipe.WaveCell.Y * scale)),
               new Rectangle(cell * Recipe.WaveCell.X, 0, Recipe.WaveCell.X, Recipe.WaveCell.Y), tint);
        _sprites++;
    }

    private void DrawClamps(SpriteBatch b, Texture2D tex, int cell, Rectangle champ, Rectangle foe, float centreX, float width, CrushPose c, Color tint)
    {
        var (centre, edge) = ClampPlace(Recipe, champ, foe, c.Close, width);
        var w = Math.Max(1, (int)MathF.Round(width));
        var scale = w / (float)Recipe.ClampCell.X;
        var h = Math.Max(1, (int)MathF.Round(Recipe.ClampCell.Y * scale));
        var x = (int)MathF.Round(centreX - 0.5f * w);
        var src = new Rectangle(cell * Recipe.ClampCell.X, 0, Recipe.ClampCell.X, Recipe.ClampCell.Y);
        // above, pressing down; below, flipped, pressing up (the pressing edges face the body); on whole pixels
        b.Draw(tex, new Rectangle(x, (int)MathF.Round(centre.Y - edge - Recipe.ClampPressY * scale), w, h), src, tint);
        b.Draw(tex, new Rectangle(x, (int)MathF.Round(centre.Y + edge - (Recipe.ClampCell.Y - Recipe.ClampPressY) * scale), w, h), src, tint,
               0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
        _sprites += 2;
    }
}
