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

    /// <summary>The bitten creature's canonical layout body this frame (its standing reference box, carried by its root motion).</summary>
    bool TryCaughtBody(int slot, out Rectangle body);

    /// <summary>The frame the creature in <paramref name="slot"/> was drawn from this frame (its silhouette).</summary>
    bool TryTargetFrame(int slot, out SpriteFrame frame);
}

/// <summary>What one <see cref="ReactionPerformance.Update"/> crossed: the SNAP (the cue, a kill's fall) and the number's own frame.</summary>
public readonly record struct ReactionStep(bool Snapped, bool Answered, Vector2 SnapAt);

/// <summary>
/// ONE REACTION BEING PRESENTED (ADR-011, the REACTION archetype): the Seeker's JAWS answering the creature whose bite
/// set it off, as a FRONTAL SHADOW BITE (the owner, 2026-09-28, with Roni Kangaskorte's "Bite VFX" as the reference).
/// </summary>
/// <remarks>
/// <para>
/// A LAYER, NOT A FIGURE OWNER. It never touches the champion's clip, his root motion or his timing: he goes on doing
/// whatever he was doing. It reads where the bitten creature is each frame, so the bite stays on it through the
/// creature's own lunge and follow-through. It moves nothing.
/// </para>
/// <para>
/// THE PHRASE, in ms after the first frame that showed the contact. Two rows of Shadow fangs seen from the front, the
/// upper crown above the creature's head and the lower row below it, on the FRONT of its canonical body (where its head
/// is). APPEAR (0-100): the rows condense out of mist over six frames, one strip state a frame, growing out of nothing and
/// tightening in, wide apart. CHARGE (100 to the snap): the teeth heat from a pale lavender-grey through violet to
/// magenta, strengthening to whole; from 150 the rows part further (the wind-up). CLOSE (233-317): the rows slam together, accelerating (the largest step on the snap frame). SNAP
/// (317, on a frame): the rows meet interlocked across the creature's middle, white-hot (<see cref="Snapped"/>: the
/// cue, a kill's fall), and break into the IMPACT over the next frame: a flash that swells, holds two frames and then
/// shrinks to a hot point before it fades, a diagonal slash across it, a ring growing out past the crown, speed lines bursting out, splinters of the broken teeth
/// flying out with the ring and fading last, and a violet Shadow haze spreading behind it all, gone at 677. The number
/// lands 40 ms after the snap (<see cref="Answered"/>). A second creature bitten by the same answer gets its own,
/// smaller bite on its own head, snapping with it, its impact only a flash and splinters.
/// </para>
/// <para>
/// ON THE FIGHT'S PLAYHEAD. Its time is the playhead minus the first frame that showed the contact (the pump's frame).
/// No clock of its own: a slowed or scrubbed capture shows the same pose for the same moment. It changes no outcome:
/// its targets are the creatures the reflected Strikes hit, each answered on its own body.
/// </para>
/// </remarks>
public sealed class ReactionPerformance
{
    private readonly Vector2[] _snapAt;        // per target: the bite's meeting point last frame (the overlay)
    private int _front;                        // the index (into Targets) of the front creature, as last drawn
    private readonly Vector4?[] _silhouette;   // per target: the drawn silhouette's box as shares of the layout body (read once); null = the body itself
    private float? _origin;                    // the playhead of the first frame that showed the contact
    private bool _snapped, _answered;
    private int _sprites;

    /// <summary>The recipe being presented.</summary>
    public ReactionRecipe Recipe { get; }

    /// <summary>The reaction's slot in the build.</summary>
    public int SkillSlot { get; }

    /// <summary>The contact: the bite's millisecond, on which the fight resolved the reaction and its answer.</summary>
    public int TriggerMs { get; }

    /// <summary>The creatures the answer landed on (the reflected Strikes), in the fight's event order (NOT front to back: <see cref="FrontTarget"/> finds the front one).</summary>
    public IReadOnlyList<int> Targets { get; }

    /// <summary>The reaction's Source colour (the overlay's).</summary>
    public Color Tint { get; }

    /// <summary>The Seeker's belt anchor last frame (the anchor overlay names his side of the exchange).</summary>
    public Vector2 BeltAt { get; private set; }

    /// <summary>Where each target was bitten last frame (the rows' meeting point; the overlay).</summary>
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

    /// <summary>Whether the rows have met: the moment the screen plays the cue and presents a kill's fall.</summary>
    public bool Snapped => _snapped;

    /// <summary>Whether the number's frame has come (a little after the snap, so the bite is seen first).</summary>
    public bool Answered => _answered;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => Recipe.EndMs;

    /// <summary>Nothing of it is left to draw.</summary>
    public bool Finished(float playheadMs) => playheadMs - OriginMs >= EndMs;

    /// <summary>Sprites drawn last frame, both passes (the trace's cost line).</summary>
    public int SpriteCount => _sprites;

    // ── THE CURVES (pure, so they are tested directly; u = ms after the first frame) ─────────────

    private static float Smooth(float v) { v = Math.Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    private static float EaseOut(float v) { v = Math.Clamp(v, 0f, 1f); return 1f - (1f - v) * (1f - v); }
    private static float EaseIn(float v) { v = Math.Clamp(v, 0f, 1f); return v * v; }

    /// <summary>How far the rows have condensed out of mist at <paramref name="u"/>: 0 loose, 1 crisp (whole by the end of the appear).</summary>
    public static float Appear(ReactionRecipe r, float u) => Smooth(u / Math.Max(1f, r.AppearMs));

    /// <summary>
    /// The strip state drawn at <paramref name="u"/> (0 = loose mist, last = crisp teeth): stepped LINEARLY through the
    /// appear, one state a frame (an eased step skipped a state and read as a jump); the opacity and the tightening keep
    /// the ease.
    /// </summary>
    public static int StateAt(ReactionRecipe r, float u)
        => Math.Clamp((int)MathF.Round(Math.Clamp(u / Math.Max(1f, r.AppearMs), 0f, 1f) * (r.FangStates - 1), MidpointRounding.AwayFromZero), 0, r.FangStates - 1);

    /// <summary>
    /// The teeth's opacity at <paramref name="u"/>: growing out of nothing through the appear (an ease-in-out from
    /// <see cref="ReactionRecipe.OpacityAtSpawn"/> to <see cref="ReactionRecipe.FormedOpacity"/>), strengthening with the
    /// charge to whole on the snap frame, then breaking into the impact (half on the next frame, gone on the one after).
    /// </summary>
    public static float TeethOpacity(ReactionRecipe r, float u)
    {
        if (u < 0f) return 0f;
        if (u < r.AppearMs) return MathHelper.Lerp(r.OpacityAtSpawn, r.FormedOpacity, Smooth(u / r.AppearMs));
        if (u < r.SnapAtMs) return MathHelper.Lerp(r.FormedOpacity, 1f, Charge(r, u));
        return 1f - Math.Clamp((u - r.SnapAtMs) / Math.Max(1f, r.TeethBreakMs), 0f, 1f);
    }

    /// <summary>How far the teeth have CHARGED at <paramref name="u"/>: 0 (pale lavender) at the end of the appear, 1 (magenta) at the snap; slow at first.</summary>
    public static float Charge(ReactionRecipe r, float u)
        => MathF.Pow(Math.Clamp((u - r.AppearMs) / Math.Max(1f, r.SnapAtMs - r.AppearMs), 0f, 1f), r.ChargeEasePower);

    /// <summary>The teeth's colour at <paramref name="u"/>: along the charge ramp, flaring toward white-hot on and after the snap.</summary>
    public static Color TeethColor(ReactionRecipe r, float u)
    {
        var ramp = r.ChargeRamp;
        var t = Charge(r, u) * (ramp.Count - 1);
        var i = Math.Min((int)t, ramp.Count - 2);
        var c = Color.Lerp(ramp[i], ramp[i + 1], t - i);
        return u >= r.SnapAtMs - 0.5f ? Color.Lerp(c, r.FlashColor, r.SnapFlare) : c;
    }

    /// <summary>How far the rows have CLOSED at <paramref name="u"/>: 0 open (through the charge and the wind-up), 1 at the snap. An ease-in: it accelerates into the bite.</summary>
    public static float Close(ReactionRecipe r, float u)
    {
        if (u <= r.CloseFromMs) return 0f;
        if (u >= r.SnapAtMs) return 1f;
        return MathF.Pow((u - r.CloseFromMs) / Math.Max(1f, r.SnapAtMs - r.CloseFromMs), r.CloseEasePower);
    }

    /// <summary>How far through the wind-up <paramref name="u"/> is: 0 until <see cref="ReactionRecipe.WindUpFromMs"/>, 1 at the close (an ease-in-out).</summary>
    public static float WindUp(ReactionRecipe r, float u)
        => Smooth((u - r.WindUpFromMs) / Math.Max(1f, r.CloseFromMs - r.WindUpFromMs));

    /// <summary>
    /// The gap between the rows' bite lines at <paramref name="u"/>, in px, for a creature <paramref name="height"/> px
    /// tall and a crown drawn at <paramref name="scale"/>: open (framing the creature), parting a little further through
    /// the wind-up, then closing to MINUS the interlock (the points pass each other) at the snap and staying shut.
    /// </summary>
    public static float Gap(ReactionRecipe r, float height, float scale, float u)
    {
        var open = r.OpenGapShare * height * (1f + r.WindUpShare * WindUp(r, u));
        var shut = -r.InterlockArtPx * scale;
        return MathHelper.Lerp(open, shut, Close(r, u));
    }

    /// <summary>The bite for one creature this frame: where each row's bite line sits, the scale of the art, the meeting point, the crown's width on screen.</summary>
    public readonly record struct FangPose(Vector2 UpperAt, Vector2 LowerAt, float Scale, Vector2 Meet, float CrownWidth);

    /// <summary>
    /// The bite for a creature whose drawn silhouette (as pinned off the pose it bit in) is <paramref name="body"/> and whose
    /// canonical body this frame is <paramref name="layout"/>, at <paramref name="u"/>. HORIZONTALLY it is centred on the
    /// canonical body (a lunge's art reaches far ahead of the creature); VERTICALLY the rows meet on the drawn silhouette
    /// at <see cref="ReactionRecipe.MeetShare"/>, the upper row above that line and the lower row below it. Pure, so the
    /// bite's geometry is tested directly.
    /// </summary>
    public static FangPose Fangs(ReactionRecipe r, Rectangle body, Rectangle layout, float u, bool secondary = false)
    {
        var anchor = layout.Width > 0 ? layout : body;
        var width = Math.Clamp(r.CrownWidthShare * anchor.Width, r.CrownMinPx, r.CrownMaxPx) * ReactionRecipes.SizeDial
                    * (secondary ? r.SecondaryScale : 1f);
        var arrive = MathHelper.Lerp(r.ArriveScale, 1f, Appear(r, u));
        var scale = width / r.CrownArtWidth * arrive;
        var meet = new Vector2(anchor.X + r.CentreShare * anchor.Width, body.Y + r.MeetShare * body.Height);
        var gap = Gap(r, body.Height, width / r.CrownArtWidth, u);
        return new FangPose(meet - new Vector2(0f, gap * 0.5f), meet + new Vector2(0f, gap * 0.5f), scale, meet, width);
    }

    /// <summary>The impact this frame: each part's scale (against its size at the crown's width) and opacity, and the splinters' turn (radians).</summary>
    public readonly record struct BurstPose(
        float FlashScale, float FlashAlpha, float SlashScale, float SlashAlpha, float RingScale, float RingAlpha,
        float StreaksScale, float StreaksAlpha, float ShardsScale, float ShardsAlpha, float ShardsTurn, float PuffScale, float PuffAlpha);

    /// <summary>
    /// The IMPACT at <paramref name="u"/> (nothing before the snap): the flash swells to whole in one frame, holds two
    /// frames, then shrinks and fades; the slash strikes across it from the frame after the snap, growing as it fades; the
    /// ring grows out from under half its size and fades after a few frames; the speed lines burst out and fade; the
    /// splinters fly out with the ring from a frame after the snap, turning a little, and fade last; the haze spreads and
    /// thins.
    /// </summary>
    public static BurstPose Burst(ReactionRecipe r, float u)
    {
        var t = u - r.SnapAtMs;
        if (t < -0.5f || u >= r.GoneMs) return default;
        t = Math.Max(0f, t);
        const float Frame = 1000f / 60f;
        // the flash: after its hold it SHRINKS first (to a hot point) and fades last (shrinking slowly while it faded, its
        // tail was a dull grey star over the haze)
        var flashK = Math.Clamp((t - 2f * Frame) / Math.Max(1f, r.FlashMs - 2f * Frame), 0f, 1f);
        var flashScale = t < Frame ? MathHelper.Lerp(0.7f, 1f, t / Frame) : t < 2f * Frame ? 1f : MathHelper.Lerp(1f, 0.25f, EaseOut(flashK));
        var flashAlpha = t < 2f * Frame ? 1f : 1f - EaseIn(flashK);
        var after = t - (Frame - 1f);                                           // from the frame after the snap (a ms of slack for the playhead)
        var slashK = Math.Clamp(after / Math.Max(1f, r.SlashMs), 0f, 1f);
        var slashAlpha = after < 0f ? 0f : 1f - Smooth(slashK);
        var ringK = EaseOut(t / Math.Max(1f, r.RingMs));
        var ringAlpha = t < 8f * Frame ? 1f : 1f - Math.Clamp((t - 8f * Frame) / Math.Max(1f, r.RingMs - 8f * Frame), 0f, 1f);
        var streakK = EaseOut(t / Math.Max(1f, r.StreaksMs));
        var streakAlpha = t < 3f * Frame ? 0.95f : 0.95f * (1f - Math.Clamp((t - 3f * Frame) / Math.Max(1f, r.StreaksMs - 3f * Frame), 0f, 1f));
        var shardK = EaseOut(after / Math.Max(1f, r.ShardsMs - Frame));
        var shardAlpha = after < 0f ? 0f : t < 10f * Frame ? 1f : 1f - Math.Clamp((t - 10f * Frame) / Math.Max(1f, r.ShardsMs - 10f * Frame), 0f, 1f);
        var puffK = EaseOut(t / Math.Max(1f, r.BurstMs));
        return new BurstPose(
            flashScale, flashAlpha,
            MathHelper.Lerp(0.9f, 1.05f, slashK), slashAlpha,
            MathHelper.Lerp(0.43f, 1f, ringK), ringAlpha,
            MathHelper.Lerp(0.6f, 1.2f, streakK), Math.Max(0f, streakAlpha),
            MathHelper.Lerp(0.5f, 1.15f, shardK), shardAlpha, MathHelper.ToRadians(12f) * shardK,
            MathHelper.Lerp(0.5f, 1.3f, puffK), 0.55f * (1f - Smooth(t / Math.Max(1f, r.BurstMs))));
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
            // the bite rides the body instead of re-sampling a moving silhouette. The layout body can hold a good deal of
            // empty canvas above a crouching head; the rows meet on the DRAWN shape. The body itself stands when the
            // silhouette cannot be read.
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
        // the playhead runs in thirds of a millisecond, so a frame drawn at the snap's millisecond can read a hair under
        // it: a half-millisecond of slack keeps the cue on the first frame that shows the rows meet
        var snapped = !_snapped && u >= Recipe.SnapAtMs - 0.5f;
        if (snapped) _snapped = true;
        var answered = !_answered && u >= Recipe.AnswerAtMs - 0.5f;
        if (answered) _answered = true;
        return new ReactionStep(snapped, answered, _snapAt.Length > 0 ? _snapAt[Math.Max(0, _front)] : default);
    }

    // ── THE POSE ─────────────────────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// The FRONT creature among <paramref name="targets"/>: the index of the one whose canonical body stands nearest the
    /// Seeker (the smallest X; the first on a tie; a creature with no body is skipped), or -1 when none has a body.
    /// </summary>
    public static int FrontTarget(IReactionStage stage, IReadOnlyList<int> targets)
    {
        var front = -1;
        var frontX = int.MaxValue;
        for (var k = 0; k < targets.Count; k++)
            if (stage.TryCaughtBody(targets[k], out var at) && at.Height > 0 && at.X < frontX) { front = k; frontX = at.X; }
        return front;
    }

    // ── DRAWING ──────────────────────────────────────────────────────────────────────────────────

    private static void DrawCentred(SpriteBatch b, Texture2D tex, Vector2 at, float diameter, float scale, Color color, float turn = 0f)
        => b.Draw(tex, at, null, color, turn, new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), diameter / tex.Width * scale, SpriteEffects.None, 0f);

    /// <summary>
    /// THE MATERIAL, in the normal alpha batch after the creatures and BEFORE the champion (over the creature it bites;
    /// when the Seeker stands in front of that creature, as HARD HANDS does, she is in front of it): the Shadow haze behind
    /// the impact, the two rows of teeth (one strip state each), their GLOW (the same states again with a zero alpha, so
    /// in this premultiplied batch they only add light, and stay under the champion), and the splinters. Six sprites a
    /// target at most.
    /// </summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        var upper = stage.Texture(Recipe.UpperKey);
        var lower = stage.Texture(Recipe.LowerKey);
        var puff = stage.Texture(Recipe.PuffKey);
        var shards = stage.Texture(Recipe.ShardsKey);
        // the FRONT creature (nearest the Seeker) gets the full bite; any other creature the same answer bit gets its own,
        // smaller one over its own head
        var front = FrontTarget(stage, Targets);
        _front = front;
        var burst = Burst(Recipe, u);
        var teethAlpha = TeethOpacity(Recipe, u);
        var state = StateAt(Recipe, u);
        var upperSrc = new Rectangle(state * Recipe.UpperCell.X, 0, Recipe.UpperCell.X, Recipe.UpperCell.Y);
        var lowerSrc = new Rectangle(state * Recipe.LowerCell.X, 0, Recipe.LowerCell.X, Recipe.LowerCell.Y);
        var colour = TeethColor(Recipe, u);
        var teeth = colour * teethAlpha;
        var glowShare = Recipe.GlowShare * (u >= Recipe.SnapAtMs - 0.5f ? 1f : MathHelper.Lerp(Recipe.GlowAtFormed, 1f, Charge(Recipe, u)));
        var glow = new Color(colour.R, colour.G, colour.B, (byte)0) * (teethAlpha * glowShare);   // alpha 0: it only adds light
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!TrySilhouette(stage, k, out var body, out var layout)) continue;
            var f = Fangs(Recipe, body, layout, u, secondary: k != front);
            _snapAt[k] = f.Meet;
            if (puff is not null && burst.PuffAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCentred(b, puff, f.Meet, Recipe.PuffSize * f.CrownWidth, burst.PuffScale, Recipe.PuffColor * burst.PuffAlpha);
                _sprites++;
            }
            if (teethAlpha >= ReactionRecipe.VisibleFloor)
            {
                if (upper is not null)
                {
                    b.Draw(upper, f.UpperAt, upperSrc, teeth, 0f, Recipe.UpperBitePoint, f.Scale, SpriteEffects.None, 0f);
                    b.Draw(upper, f.UpperAt, upperSrc, glow, 0f, Recipe.UpperBitePoint, f.Scale, SpriteEffects.None, 0f);
                    _sprites += 2;
                }
                if (lower is not null)
                {
                    b.Draw(lower, f.LowerAt, lowerSrc, teeth, 0f, Recipe.LowerBitePoint, f.Scale, SpriteEffects.None, 0f);
                    b.Draw(lower, f.LowerAt, lowerSrc, glow, 0f, Recipe.LowerBitePoint, f.Scale, SpriteEffects.None, 0f);
                    _sprites += 2;
                }
            }
            if (shards is not null && burst.ShardsAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCentred(b, shards, f.Meet, Recipe.ShardsSize * f.CrownWidth, burst.ShardsScale, Recipe.ShardColor * burst.ShardsAlpha, burst.ShardsTurn);
                _sprites++;
            }
        }
    }

    /// <summary>
    /// THE LIGHT, in the shared additive pass after every figure: the impact's ring, speed lines, slash and flash (the
    /// star on the slash's middle; a second creature's impact is only its flash, with the splinters drawn in the
    /// material). Four sprites a target at most.
    /// </summary>
    public void DrawLight(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        var ring = stage.Texture(Recipe.RingKey);
        var streaks = stage.Texture(Recipe.StreaksKey);
        var flash = stage.Texture(Recipe.FlashKey);
        var slash = stage.Texture(Recipe.SlashKey);
        var burst = Burst(Recipe, u);
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!TrySilhouette(stage, k, out var body, out var layout)) continue;
            var secondary = k != _front;
            var f = Fangs(Recipe, body, layout, u, secondary);
            if (!secondary && ring is not null && burst.RingAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCentred(b, ring, f.Meet, Recipe.RingSize * f.CrownWidth, burst.RingScale, Recipe.RingColor * burst.RingAlpha);
                _sprites++;
            }
            if (!secondary && streaks is not null && burst.StreaksAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCentred(b, streaks, f.Meet, Recipe.StreaksSize * f.CrownWidth, burst.StreaksScale, Recipe.StreakColor * burst.StreaksAlpha);
                _sprites++;
            }
            if (!secondary && slash is not null && burst.SlashAlpha >= ReactionRecipe.VisibleFloor)
            {
                b.Draw(slash, f.Meet, null, Recipe.SlashColor * burst.SlashAlpha, MathHelper.ToRadians(Recipe.SlashAngleDegrees),
                       new Vector2(slash.Width * 0.5f, slash.Height * 0.5f), Recipe.SlashSize * f.CrownWidth / slash.Width * burst.SlashScale, SpriteEffects.None, 0f);
                _sprites++;
            }
            if (flash is not null && burst.FlashAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCentred(b, flash, f.Meet, Recipe.FlashSize * f.CrownWidth, burst.FlashScale, Recipe.FlashColor * burst.FlashAlpha);
                _sprites++;
            }
        }
    }
}
