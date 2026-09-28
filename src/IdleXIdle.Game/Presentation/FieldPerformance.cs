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
    // each tick's target silhouette, pinned once as shares of its layout body (left, top, right, bottom): the layout body
    // holds the whole canvas (a lunge's extended one too), and the arcs pressed 250 px over the heads
    private readonly Vector4?[] _shape;
    private int _sprites;

    /// <summary>
    /// The ticks this wave will present: the Aura events' ms, the slot each one's Break names (or -1), and whether a
    /// presented reaction plays around it (the crush then gives way: <see cref="FieldRecipe.YieldBeforeMs"/>).
    /// </summary>
    public FieldPerformance(FieldRecipe recipe, int slot, IReadOnlyList<(float AtMs, int Target, bool Yields)> ticks)
    {
        Recipe = recipe;
        Slot = slot;
        _tickAt = new float[ticks.Count];
        _tickTarget = new int[ticks.Count];
        _tickYields = new bool[ticks.Count];
        _shape = new Vector4?[ticks.Count];
        for (var k = 0; k < ticks.Count; k++) (_tickAt[k], _tickTarget[k], _tickYields[k]) = ticks[k];
    }

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
        // out past its size in the first 40 % (an ease-out), back to 1 over the rest; the gathered density lets go
        var scale = t < 0.4f
            ? MathHelper.Lerp(r.ContractScale, r.SpringScale, 1f - (1f - t / 0.4f) * (1f - t / 0.4f))
            : MathHelper.Lerp(r.SpringScale, 1f, Smooth((t - 0.4f) / 0.6f));
        return new FieldPose(scale, MathHelper.Lerp(r.ContractAlpha, rest, Smooth(t)));
    }

    /// <summary>The pressure front: how far it has come (0 the Seeker, 1 the target), how far it has grown (0 its launch height, 1 its arrival height), its opacity.</summary>
    public readonly record struct WavePose(float Progress, float Grow, float Alpha);

    /// <summary>
    /// The front at u: it leaves at <see cref="FieldRecipe.LaunchMs"/> and ARRIVES on the tick (u = 0), eased out (fast from
    /// the Seeker, still moving as it lands), growing and gathering opacity on the way; then it collapses into the crush.
    /// </summary>
    public static WavePose Wave(FieldRecipe r, float u)
    {
        if (float.IsNaN(u) || u < r.LaunchMs || u > r.WaveCollapseMs) return default;
        if (u <= 0f)
        {
            var t = (u - r.LaunchMs) / -r.LaunchMs;
            var p = 1f - MathF.Pow(1f - t, r.TravelEasePower);
            var alpha = MathHelper.Lerp(r.WaveLaunchAlpha, r.WaveArriveAlpha, t) * Math.Clamp((u - r.LaunchMs) / 34f, 0f, 1f);
            return new WavePose(p, t, alpha);
        }
        var c = u / Math.Max(1f, r.WaveCollapseMs);
        return new WavePose(1f, 1f + 0.08f * c, r.WaveArriveAlpha * (1f - c) * (1f - c));
    }

    /// <summary>
    /// The front's height in px: from <see cref="FieldRecipe.WaveLaunchShare"/> of the Seeker's height as it leaves to
    /// <see cref="FieldRecipe.WaveArriveShare"/> of the target's silhouette on arrival (<paramref name="grow"/> 0..1, and a
    /// little past 1 as it collapses).
    /// </summary>
    public static float WaveHeight(FieldRecipe r, float grow, float champHeight, float targetHeight)
    {
        var from = r.WaveLaunchShare * champHeight;
        var to = r.WaveArriveShare * targetHeight;
        return grow <= 1f ? MathHelper.Lerp(from, to, grow) : to * grow;
    }

    /// <summary>The crush: how shut the arcs are (0 open, 1 shut), their opacity, the body's buckle (0..1), their heat (1 hot, 0 cool).</summary>
    public readonly record struct CrushPose(float Close, float Alpha, float Squash, float Heat);

    /// <summary>
    /// The crush at u: from the tick the arcs press in (accelerating, <see cref="FieldRecipe.CrushInMs"/>), hold, and let go
    /// (easing back a little as they fade); the body buckles with them and recovers early in the release; pale and hot for
    /// the first frame, cooling to violet.
    /// </summary>
    public static CrushPose Crush(FieldRecipe r, float u)
    {
        if (float.IsNaN(u) || u < 0f || u > r.EndMs) return default;
        var holdEnd = r.CrushInMs + r.CrushHoldMs;
        var outEnd = holdEnd + r.CrushOutMs;
        float close, alpha, squash;
        if (u < r.CrushInMs)
        {
            var k = u / r.CrushInMs;
            close = Smooth(k);                                  // the tips fold over and under, then press
            alpha = r.ClampPeakAlpha * Math.Clamp(0.55f + 0.45f * k * 3f, 0f, 1f);
            squash = Smooth(k);
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
            close = 1f - 0.35f * Smooth(k);
            alpha = r.ClampPeakAlpha * (1f - Smooth(k));
            squash = 1f - Smooth(Math.Min(1f, k / 0.6f));
        }
        else return default;
        var heat = 1f - Smooth(u / 150f);
        return new CrushPose(close, alpha, squash, heat);
    }

    /// <summary>The flattened front's opacity share at u on a tick that gives way (1 on the tick, gone by <see cref="FieldRecipe.FlattenMs"/>).</summary>
    public static float FlattenFade(FieldRecipe r, float u) => 1f - Smooth(u / Math.Max(1f, r.FlattenMs));

    /// <summary>The body's drawn scale (width, height) at u: 1 outside the crush.</summary>
    public static Vector2 SquashAt(FieldRecipe r, float u)
    {
        var s = Crush(r, u).Squash;
        return new Vector2(MathHelper.Lerp(1f, r.SquashX, s), MathHelper.Lerp(1f, r.SquashY, s));
    }

    /// <summary>The crush arcs' colour at u: pale and hot on the tick, the arcs' violet, cooling as they let go.</summary>
    public static Color ClampTint(FieldRecipe r, float u)
    {
        var c = Crush(r, u);
        var warm = Color.Lerp(r.ClampColor, r.ClampHotColor, c.Heat);
        var cool = Math.Clamp((u - r.CrushInMs - r.CrushHoldMs) / Math.Max(1f, r.CrushOutMs), 0f, 1f);
        return Color.Lerp(warm, r.ClampCoolColor, cool);
    }

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
    /// Over the creatures, UNDER the champion: the pressure front (its softened afterimage behind it) on its way, and the
    /// two crush arcs on the target (material: translucent violet).
    /// </summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        if (!TryPhrase(playheadMs, out var u, out var tick) || u < Recipe.LaunchMs) return;
        var target = TargetOf(tick);
        // THE TARGET'S DRAWN SHAPE, pinned once per tick (called after the creatures drew, so their frames are this frame's)
        if (target >= 0 && _shape[tick] is null && stage.TryTargetBody(target, out var layout) && layout.Width > 0 && layout.Height > 0)
        {
            var shares = new Vector4(0f, 0f, 1f, 1f);
            if (stage.TryTargetFrame(target, out var frame) && SilhouetteProbe.OpaqueBounds(frame) is { Width: > 0, Height: > 0 } s)
                shares = new Vector4(Math.Clamp((s.X - layout.X) / (float)layout.Width, -0.5f, 1f),
                                     Math.Clamp((s.Y - layout.Y) / (float)layout.Height, -0.5f, 1f),
                                     Math.Clamp((s.Right - layout.X) / (float)layout.Width, 0f, 1.5f),
                                     Math.Clamp((s.Bottom - layout.Y) / (float)layout.Height, 0f, 1.5f));
            _shape[tick] = shares;
        }
        if (!TryTargetShape(stage, tick, target, out var foe) || !stage.TryChampionBody(out var champ)) return;
        LastShape = foe;
        var quiet = stage.ChampionPerforming ? Recipe.QuietShare : 1f;
        if (stage.Texture(Recipe.WaveKey) is { } wave)
        {
            var w = Wave(Recipe, u);
            if (w.Alpha >= ReactionRecipe.VisibleFloor)
            {
                var lag = Wave(Recipe, Math.Max(Recipe.LaunchMs, u - Recipe.AfterimageLagMs));
                DrawWaveCell(b, wave, 1, champ, foe, lag.Progress, w.Grow, Recipe.WaveColor * (w.Alpha * Recipe.AfterimageAlpha * quiet));
                DrawWaveCell(b, wave, 0, champ, foe, w.Progress, w.Grow, Recipe.WaveColor * (w.Alpha * quiet));
            }
        }
        var c = Crush(Recipe, u);
        if (c.Alpha < ReactionRecipe.VisibleFloor) return;
        if (Yields(tick))
        {
            // giving way to a reaction on this creature: the front flattens against its facing side
            if (stage.Texture(Recipe.WaveKey) is { } flat)
                DrawFlattened(b, flat, champ, foe, ClampTint(Recipe, u) * (c.Alpha * quiet * FlattenFade(Recipe, u)));
        }
        else if (stage.Texture(Recipe.ClampKey) is { } clamp)
            DrawClamps(b, clamp, champ, foe, c, ClampTint(Recipe, u) * (c.Alpha * quiet));
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
        if (!TryTargetShape(stage, tick, TargetOf(tick), out var foe) || !stage.TryChampionBody(out var champ)) return;
        var quiet = stage.ChampionPerforming ? Recipe.QuietShare : 1f;
        if (stage.Texture(Recipe.WaveKey) is { } wave)
        {
            var w = Wave(Recipe, u);
            var rim = Recipe.WaveGlowShare * MathHelper.Lerp(Recipe.WaveTravelGlow, 1f, Math.Min(1f, w.Grow));
            if (w.Alpha * rim >= ReactionRecipe.VisibleFloor)
            {
                DrawWaveCell(b, wave, 1, champ, foe, w.Progress, w.Grow, Recipe.WaveColor * (w.Alpha * Recipe.WaveBodyGlowShare * quiet));
                DrawWaveCell(b, wave, 0, champ, foe, w.Progress, w.Grow, Recipe.RimColor * (w.Alpha * rim * quiet));
            }
        }
        var c = Crush(Recipe, u);
        var flare = c.Alpha * c.Heat * quiet;
        if (flare < ReactionRecipe.VisibleFloor) return;
        if (Yields(tick))
        {
            if (stage.Texture(Recipe.WaveKey) is { } flat) DrawFlattened(b, flat, champ, foe, Recipe.RimColor * (flare * FlattenFade(Recipe, u)));
        }
        else if (stage.Texture(Recipe.ClampKey) is { } clamp) DrawClamps(b, clamp, champ, foe, c, Recipe.ClampHotColor * flare);
    }

    /// <summary>The target's drawn shape this frame: its layout body cut to the silhouette pinned for this tick.</summary>
    private bool TryTargetShape(IReactionStage stage, int tick, int target, out Rectangle shape)
    {
        shape = default;
        if (target < 0 || !stage.TryTargetBody(target, out var layout) || layout.Height <= 0) return false;
        var sh = _shape[tick] ?? new Vector4(0f, 0f, 1f, 1f);
        shape = new Rectangle((int)(layout.X + sh.X * layout.Width), (int)(layout.Y + sh.Y * layout.Height),
                              Math.Max(1, (int)((sh.Z - sh.X) * layout.Width)), Math.Max(1, (int)((sh.W - sh.Y) * layout.Height)));
        return true;
    }

    /// <summary>
    /// Where the front leaves from (the FIELD's leading edge, in front of the Seeker's enemy-facing side: the wave visibly
    /// leaves the field) and where it lands (the target's front).
    /// </summary>
    public static (Vector2 From, Vector2 To) Path(FieldRecipe r, Rectangle champ, Rectangle foe)
    {
        var fieldWidth = champ.Height * r.FieldHeightShare * r.FieldCell.X / r.FieldCell.Y;
        return (new Vector2(champ.X + r.FieldCentre.X * champ.Width + r.FieldEdgeShare * fieldWidth, champ.Y + r.FieldCentre.Y * champ.Height),
                new Vector2(foe.X + 0.12f * foe.Width, foe.Y + 0.5f * foe.Height));
    }

    private void DrawWaveCell(SpriteBatch b, Texture2D tex, int cell, Rectangle champ, Rectangle foe, float progress,
                              float grow, Color tint)
    {
        var (from, to) = Path(Recipe, champ, foe);
        var at = Vector2.Lerp(from, to, progress);
        var scale = WaveHeight(Recipe, grow, champ.Height, foe.Height) / Recipe.WaveCell.Y;
        var src = new Rectangle(cell * Recipe.WaveCell.X, 0, Recipe.WaveCell.X, Recipe.WaveCell.Y);
        var turn = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        b.Draw(tex, at, src, tint, turn, new Vector2(Recipe.WaveLeadX, Recipe.WaveCell.Y * 0.5f), scale, SpriteEffects.None, 0f);
        _sprites++;
    }

    /// <summary>
    /// The crush arcs' centre and the distance of their pressing edges from it, for a crush <paramref name="close"/> (0 the
    /// arriving crescent's tips at the target's front, 1 folded over and under the target's body, biting into it).
    /// </summary>
    public static (Vector2 Centre, float Edge) ClampPlace(FieldRecipe r, Rectangle champ, Rectangle foe, float close)
    {
        var (_, arrive) = Path(r, champ, foe);
        var shut = new Vector2(foe.X + r.ClampCentreShare * foe.Width, foe.Y + 0.5f * foe.Height);
        var tips = Math.Min(0.5f * WaveHeight(r, 1f, champ.Height, foe.Height), r.ClampStartCap * foe.Height);
        return (Vector2.Lerp(arrive, shut, close), MathHelper.Lerp(tips, r.ClampShutShare * foe.Height, close));
    }

    private void DrawFlattened(SpriteBatch b, Texture2D tex, Rectangle champ, Rectangle foe, Color tint)
    {
        var (_, to) = Path(Recipe, champ, foe);
        var scale = WaveHeight(Recipe, 1f, champ.Height, foe.Height) / Recipe.WaveCell.Y;
        b.Draw(tex, to, new Rectangle(0, 0, Recipe.WaveCell.X, Recipe.WaveCell.Y), tint, 0f,
               new Vector2(Recipe.WaveLeadX, Recipe.WaveCell.Y * 0.5f), new Vector2(scale * Recipe.FlattenShare, scale * 0.92f),
               SpriteEffects.None, 0f);
        _sprites++;
    }

    private void DrawClamps(SpriteBatch b, Texture2D tex, Rectangle champ, Rectangle foe, CrushPose c, Color tint)
    {
        var (centre, edge) = ClampPlace(Recipe, champ, foe, c.Close);
        var scale = foe.Width * Recipe.ClampWidthShare / Recipe.ClampCell.X;
        var half = Recipe.ClampCell.X * 0.5f;
        // above, pressing down; below, flipped, pressing up (the pressing edges face the body)
        b.Draw(tex, new Vector2(centre.X, centre.Y - edge), null, tint, 0f, new Vector2(half, Recipe.ClampPressY), scale,
               SpriteEffects.None, 0f);
        b.Draw(tex, new Vector2(centre.X, centre.Y + edge), null, tint, 0f, new Vector2(half, Recipe.ClampCell.Y - Recipe.ClampPressY),
               scale, SpriteEffects.FlipVertically, 0f);
        _sprites += 2;
    }
}
