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

    /// <summary>Whether the champion is performing an authored action this frame (SPRAY, HARD HANDS): a reaction then stays quieter than it.</summary>
    bool ChampionPerforming { get; }
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
/// (317, on a frame): the rows meet interlocked across the creature's middle, white-hot for that one frame
/// (<see cref="Snapped"/>: the cue, a kill's fall), the mist behind them at its densest and most compressed, and break
/// into the IMPACT over the next frame as the energy cools: a flash that swells, holds two frames and then shrinks to a
/// hot point before it fades, cooling to magenta at once, a pressure ring growing out past the crown and
/// dissolving into the mist, speed lines bursting out, splinters of the broken teeth
/// flying out with the ring and fading last, and the near-black Shadow mist behind the creature releasing outward and evaporating, gone at 677. The number
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
    private float? _hotU;                      // the time of the first frame DRAWN at or after the snap: the one white-hot frame

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

    /// <summary>The strip cell drawn at <paramref name="u"/>: the smoke's condensation state, or on the snap frame (the first frame drawn at the snap) the <see cref="ReactionRecipe.SnapCell"/>, the smoke condensed hard into solid teeth.</summary>
    public static int CellAt(ReactionRecipe r, float u, bool snapFrame) => snapFrame ? r.SnapCell : StateAt(r, u);

    /// <summary>
    /// The teeth's opacity at <paramref name="u"/>: growing out of nothing through the appear (an ease-in-out from
    /// <see cref="ReactionRecipe.OpacityAtSpawn"/> to <see cref="ReactionRecipe.FormedOpacity"/>), strengthening with the
    /// charge to <see cref="ReactionRecipe.SnapOpacity"/> on the snap frame (where <see cref="CellAt"/> draws the solid
    /// <see cref="ReactionRecipe.SnapCell"/>: the smoke condensed hard), then breaking back into smoke with the impact
    /// (half on the next frame, gone on the one after).
    /// </summary>
    public static float TeethOpacity(ReactionRecipe r, float u)
    {
        if (u < 0f) return 0f;
        if (u < r.AppearMs) return MathHelper.Lerp(r.OpacityAtSpawn, r.FormedOpacity, Smooth(u / r.AppearMs));
        if (u < r.SnapAtMs) return MathHelper.Lerp(r.FormedOpacity, r.SnapOpacity, Charge(r, u));
        return r.SnapOpacity * (1f - Math.Clamp((u - r.SnapAtMs) / Math.Max(1f, r.TeethBreakMs), 0f, 1f));
    }

    /// <summary>How far the teeth have CHARGED at <paramref name="u"/>: 0 (pale lavender) at the end of the appear, 1 (magenta) at the snap; slow at first.</summary>
    public static float Charge(ReactionRecipe r, float u)
        => MathF.Pow(Math.Clamp((u - r.AppearMs) / Math.Max(1f, r.SnapAtMs - r.AppearMs), 0f, 1f), r.ChargeEasePower);

    private const float Frame = 1000f / 60f;

    /// <summary>
    /// The teeth's colour at <paramref name="u"/>: along the charge ramp, flaring toward white-hot on the snap frame ONLY
    /// (an impact punctuation, never the effect's colour), back to the hot magenta on the frame after as they break.
    /// </summary>
    public static Color TeethColor(ReactionRecipe r, float u) => TeethColor(r, u, IsSnapFrame(r, u));

    /// <summary>The teeth's colour at <paramref name="u"/>, white-hot only when <paramref name="hot"/> (the one hot frame the screen actually drew; never for a quiet bite).</summary>
    public static Color TeethColor(ReactionRecipe r, float u, bool hot)
    {
        var ramp = r.ChargeRamp;
        var t = Charge(r, u) * (ramp.Count - 1);
        var i = Math.Min((int)t, ramp.Count - 2);
        var c = Color.Lerp(ramp[i], ramp[i + 1], t - i);
        return hot ? Color.Lerp(c, r.FlashColor, r.SnapFlare) : c;
    }

    /// <summary>Whether <paramref name="u"/> falls on the snap frame (the one frame of white-hot).</summary>
    public static bool IsSnapFrame(ReactionRecipe r, float u) => u >= r.SnapAtMs - 0.5f && u < r.SnapAtMs + Frame - 1f;

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

    /// <summary>
    /// The impact this frame: each part's scale (against its size at the crown's width) and opacity; the crisp ring's
    /// hand-over to its softened copy; the splinters' turn (radians) and how far they have softened into Shadow (0..1).
    /// </summary>
    public readonly record struct BurstPose(
        float FlashScale, float FlashAlpha, float RingScale, float RingAlpha, float RingSoftAlpha,
        float StreaksScale, float StreaksAlpha, float ShardsScale, float ShardsAlpha, float ShardsTurn, float ShardsSoft);

    /// <summary>
    /// The IMPACT at <paramref name="u"/> (nothing before the snap), in its hierarchy. The HOT CORE: the flash swells to
    /// whole in one frame, holds two, then shrinks to a point first and fades last, by <see cref="ReactionRecipe.FlashMs"/>.
    /// The PRESSURE RING: it grows out from under half its size past the crown, the crisp ring whole a few frames then gone
    /// by <see cref="ReactionRecipe.RingCrispMs"/>, its softened copy rising as it goes and dissolving by
    /// <see cref="ReactionRecipe.RingMs"/>. The SPLINTERS fly out with it from a frame after the snap, turning, softening
    /// into Shadow through the second half of their life, the last to fade. ACCENT: the speed lines, bursting out and gone
    /// near the peak.
    /// </summary>
    public static BurstPose Burst(ReactionRecipe r, float u)
    {
        var t = u - r.SnapAtMs;
        if (t < -0.5f || u >= r.GoneMs) return default;
        t = Math.Max(0f, t);
        var flashK = Math.Clamp((t - 2f * Frame) / Math.Max(1f, r.FlashMs - 2f * Frame), 0f, 1f);
        var flashScale = t < Frame ? MathHelper.Lerp(0.7f, 1f, t / Frame) : t < 2f * Frame ? 1f : MathHelper.Lerp(1f, 0.25f, EaseOut(flashK));
        var flashAlpha = t < 2f * Frame ? 1f : 1f - EaseIn(flashK);
        var after = t - (Frame - 1f);                                           // from the frame after the snap (a ms of slack for the playhead)
        var ringK = EaseOut(t / Math.Max(1f, r.RingMs));
        var crispK = Math.Clamp((t - 4f * Frame) / Math.Max(1f, r.RingCrispMs - 4f * Frame), 0f, 1f);
        var ringAlpha = t < 4f * Frame ? 1f : 1f - Smooth(crispK);
        var softIn = Smooth(Math.Clamp((t - 2f * Frame) / (5f * Frame), 0f, 1f));
        var softOut = 1f - Smooth(Math.Clamp((t - r.RingCrispMs * 0.6f) / Math.Max(1f, r.RingMs - r.RingCrispMs * 0.6f), 0f, 1f));
        var streakK = EaseOut(t / Math.Max(1f, r.StreaksMs));
        var streakAlpha = t < Frame ? r.StreaksPeakAlpha : r.StreaksPeakAlpha * (1f - Math.Clamp((t - Frame) / Math.Max(1f, r.StreaksMs - Frame), 0f, 1f));
        var shardK = EaseOut(after / Math.Max(1f, r.ShardsMs - Frame));
        var shardAlpha = after < 0f ? 0f : t < 9f * Frame ? 1f : 1f - Math.Clamp((t - 9f * Frame) / Math.Max(1f, r.ShardsMs - 9f * Frame), 0f, 1f);
        var shardSoft = Smooth(Math.Clamp((t - 6f * Frame) / Math.Max(1f, r.ShardsMs * 0.55f), 0f, 1f));
        return new BurstPose(
            flashScale, flashAlpha,
            MathHelper.Lerp(0.43f, 1f, ringK), ringAlpha, r.RingSoftPeakAlpha * softIn * softOut,
            MathHelper.Lerp(0.6f, 1.2f, streakK), Math.Max(0f, streakAlpha),
            MathHelper.Lerp(0.5f, 1.15f, shardK), shardAlpha, MathHelper.ToRadians(12f) * shardK, shardSoft);
    }

    /// <summary>The flash's colour at <paramref name="u"/>: white-hot on the snap frame, then cooling through magenta toward violet as it shrinks and fades.</summary>
    public static Color FlashTint(ReactionRecipe r, float u) => FlashTint(r, u, IsSnapFrame(r, u));

    /// <summary>
    /// The flash's colour at <paramref name="u"/>: white-hot only when <paramref name="hot"/> (the one hot frame drawn; a
    /// quiet bite never), then already MAGENTA on the next frame (a flash still two-thirds white there made the frame after
    /// the snap the brightest of the bite), cooling toward violet as it shrinks and fades.
    /// </summary>
    public static Color FlashTint(ReactionRecipe r, float u, bool hot)
    {
        if (hot) return r.FlashColor;
        var k = Math.Clamp((u - r.SnapAtMs - Frame) / Math.Max(1f, r.FlashMs - Frame), 0f, 1f);
        var warm = Color.Lerp(r.FlashColor, r.RingColor, 0.85f);
        return Color.Lerp(warm, r.CoolColor, Smooth(k));
    }

    /// <summary>The speed lines' colour at <paramref name="u"/>: pale at the snap, cooling toward violet as they fade.</summary>
    public static Color StreakTint(ReactionRecipe r, float u)
        => Color.Lerp(r.StreakColor, r.CoolColor, Smooth(Math.Clamp((u - r.SnapAtMs) / Math.Max(1f, r.StreaksMs), 0f, 1f)));

    /// <summary>The share of <paramref name="target"/> opacity each of <paramref name="lobes"/> stacked sprites is drawn at, so the stack reads as the target.</summary>
    public static float StackedShare(float target, int lobes)
        => lobes <= 1 ? target : 1f - MathF.Pow(1f - Math.Clamp(target, 0f, 0.999f), 1f / lobes);

    /// <summary>The crisp ring's colour at <paramref name="u"/>: magenta at the snap, cooling to violet as it grows.</summary>
    public static Color RingTint(ReactionRecipe r, float u)
        => Color.Lerp(r.RingColor, r.CoolColor, Smooth(Math.Clamp((u - r.SnapAtMs) / Math.Max(1f, r.RingCrispMs), 0f, 1f)));

    /// <summary>The splinters' colour at <paramref name="u"/>: pale magenta and solid as the teeth break, darkening to Shadow violet.</summary>
    public static Color ShardTint(ReactionRecipe r, float u)
        => Color.Lerp(r.ShardColor, r.ShardCoolColor, Smooth(Math.Clamp((u - r.SnapAtMs) / Math.Max(1f, r.ShardsMs * 0.7f), 0f, 1f)));

    /// <summary>The Shadow mist this frame: its scale, its height's stretch against its width, its core opacity, and how far its lobes have drawn together (0..1).</summary>
    public readonly record struct MistPose(float Scale, float StretchY, float Alpha, float Converge);

    /// <summary>
    /// THE SHADOW MIST at <paramref name="u"/>, with ONE lifecycle (fade in, condense, compress, snap, expand, fade out;
    /// its alpha only ever rises to the snap and only ever falls after it): it gathers while the fangs condense from it
    /// (a whisper, large, tightening in), COMPRESSES around the bite through the charge (its form and density, not a pulse
    /// of light), stretches and squeezes with the rows' own gap (the fang geometry controls it), is densest and most
    /// compressed at the snap, then releases slowly OUTWARD and evaporates by the end.
    /// </summary>
    public static MistPose Mist(ReactionRecipe r, float u)
    {
        if (u < 0f || u >= r.GoneMs) return default;
        var gap = Gap(r, 1f, 0f, u) / Math.Max(1e-3f, r.OpenGapShare) - 1f;        // the rows' gap against the open gap: + wind-up, -1 shut
        var squeeze = Math.Clamp(1f + r.MistStretch * gap, r.MistStretchMin, r.MistStretchMax);
        if (u < r.AppearMs)
        {
            var k = Smooth(u / r.AppearMs);
            return new MistPose(MathHelper.Lerp(r.MistScaleAtSpawn, 1f, k), squeeze, MathHelper.Lerp(r.MistAlphaAtSpawn, r.MistAlphaFormed, k), 0f);
        }
        if (u < r.SnapAtMs - 0.5f)
        {
            var k = Math.Clamp((u - r.AppearMs) / Math.Max(1f, r.SnapAtMs - r.AppearMs), 0f, 1f);
            return new MistPose(MathHelper.Lerp(1f, r.MistScaleAtSnap, Smooth(k)), squeeze, MathHelper.Lerp(r.MistAlphaFormed, r.MistAlphaAtSnap, k), Smooth(k));
        }
        var tail = Math.Clamp((u - r.SnapAtMs) / Math.Max(1f, r.BurstMs), 0f, 1f);
        return new MistPose(MathHelper.Lerp(r.MistScaleAtSnap, r.MistScaleTail, EaseOut(tail)),
                            MathHelper.Lerp(r.MistStretchMin, 1f, EaseOut(tail)),
                            r.MistAlphaAtSnap * (1f - Smooth(tail)), 1f - EaseOut(tail));
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

    /// <summary>One cell of a two-cell part (0 crisp, 1 softened), centred, <paramref name="diameter"/> wide at scale 1.</summary>
    private static void DrawCell(SpriteBatch b, Texture2D tex, int cell, Vector2 at, float diameter, float scale, Color color, float turn = 0f)
    {
        var w = tex.Width / 2;
        b.Draw(tex, at, new Rectangle(cell * w, 0, w, tex.Height), color, turn, new Vector2(w * 0.5f, tex.Height * 0.5f), diameter / w * scale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// THE MIST, in the normal alpha batch BEFORE the creatures: the near-black Shadow volume the fangs condense from,
    /// compress into and burst out of, as ATMOSPHERE behind the bitten creature (drawn over it, it greyed the black body and
    /// dimmed its eyes): its lobes, each at the share that stacks to the recipe's opacity. Three sprites a target.
    /// </summary>
    public void DrawUnder(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        _sprites = 0;
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs || stage.Texture(Recipe.MistKey) is not { } mistTex) return;
        var mist = Mist(Recipe, u);
        if (mist.Alpha < ReactionRecipe.VisibleFloor) return;
        var front = FrontTarget(stage, Targets);
        var share = StackedShare(mist.Alpha, Recipe.MistLobes.Count);
        var origin = new Vector2(mistTex.Width * 0.5f, mistTex.Height * 0.5f);
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!TrySilhouette(stage, k, out var body, out var layout)) continue;
            var f = Fangs(Recipe, body, layout, u, secondary: k != front);
            var size = new Vector2(Recipe.MistSize.X, Recipe.MistSize.Y * mist.StretchY) * f.CrownWidth * mist.Scale;
            for (var l = 0; l < Recipe.MistLobes.Count; l++)
            {
                var lobe = Recipe.MistLobes[l];
                var at = f.Meet + new Vector2(lobe.X, lobe.Y) * f.CrownWidth * (1f - Recipe.MistConverge * mist.Converge);
                var tint = l == 0 ? Recipe.MistEdgeColor * (share * Recipe.MistEdgeShare) : Recipe.MistCoreColor * share;
                var flip = (int)lobe.Z;
                var effects = (flip & 1) != 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                if ((flip & 2) != 0) effects |= SpriteEffects.FlipVertically;
                b.Draw(mistTex, at, null, tint, 0f, origin, size * lobe.W / new Vector2(mistTex.Width, mistTex.Height), effects, 0f);
                _sprites++;
            }
        }
    }

    /// <summary>
    /// THE MATERIAL, in the normal alpha batch after the creatures and BEFORE the champion (over the creature it bites;
    /// when the Seeker stands in front of that creature, as HARD HANDS does, she is in front of it): the two rows of teeth
    /// (one strip state each) and their GLOW (the same states with a zero alpha: in this premultiplied batch they only add
    /// light, under the champion), the pressure ring's softened copy dissolving into the mist, and the splinters (crisp,
    /// then softened). The teeth are white-hot on the first frame drawn at the snap only, and never for a quiet bite (the
    /// champion performing, or a second creature). Seven sprites a target at most.
    /// </summary>
    public void DrawMaterial(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        if (stage.TryChampionBody(out var champ))
            BeltAt = new Vector2(champ.X + Recipe.ChampionAnchor.X * champ.Width, champ.Y + Recipe.ChampionAnchor.Y * champ.Height);
        var upper = stage.Texture(Recipe.UpperKey);
        var lower = stage.Texture(Recipe.LowerKey);
        var ring = stage.Texture(Recipe.RingKey);
        var shards = stage.Texture(Recipe.ShardsKey);
        // the FRONT creature (nearest the Seeker) gets the full bite; any other creature the same answer bit gets its own,
        // smaller, quieter one over its own head
        var front = FrontTarget(stage, Targets);
        _front = front;
        if (_hotU is null && u >= Recipe.SnapAtMs - 0.5f) _hotU = u;       // the first frame DRAWN at the snap: the one hot frame
        var hotFrame = _hotU is { } h && u < h + Frame - 1f;
        var quiet = stage.ChampionPerforming;
        var burst = Burst(Recipe, u);
        var teethAlpha = TeethOpacity(Recipe, u);
        var state = CellAt(Recipe, u, hotFrame);        // the snap frame: the smoke condensed hard
        var upperSrc = new Rectangle(state * Recipe.UpperCell.X, 0, Recipe.UpperCell.X, Recipe.UpperCell.Y);
        var lowerSrc = new Rectangle(state * Recipe.LowerCell.X, 0, Recipe.LowerCell.X, Recipe.LowerCell.Y);
        var glowShare = Recipe.GlowShare * (u >= Recipe.SnapAtMs - 0.5f ? 1f : MathHelper.Lerp(Recipe.GlowAtFormed, 1f, Charge(Recipe, u)));
        var shardTint = ShardTint(Recipe, u);
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!TrySilhouette(stage, k, out var body, out var layout)) continue;
            var secondary = k != front;
            var f = Fangs(Recipe, body, layout, u, secondary);
            _snapAt[k] = f.Meet;
            if (teethAlpha >= ReactionRecipe.VisibleFloor)
            {
                var colour = TeethColor(Recipe, u, hotFrame && !quiet && !secondary);
                var teeth = colour * teethAlpha;
                var glow = new Color(colour.R, colour.G, colour.B, (byte)0) * (teethAlpha * glowShare);   // alpha 0: it only adds light
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
            if (!secondary && ring is not null && burst.RingSoftAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCell(b, ring, 1, f.Meet, Recipe.RingSize * f.CrownWidth, burst.RingScale * 1.06f, Recipe.MistEdgeColor * burst.RingSoftAlpha);
                _sprites++;
            }
            if (shards is not null && burst.ShardsAlpha >= ReactionRecipe.VisibleFloor)
            {
                var crisp = burst.ShardsAlpha * (1f - burst.ShardsSoft);
                var soft = burst.ShardsAlpha * burst.ShardsSoft;
                if (crisp >= ReactionRecipe.VisibleFloor)
                {
                    DrawCell(b, shards, 0, f.Meet, Recipe.ShardsSize * f.CrownWidth, burst.ShardsScale, shardTint * crisp, burst.ShardsTurn);
                    _sprites++;
                }
                if (soft >= ReactionRecipe.VisibleFloor)
                {
                    DrawCell(b, shards, 1, f.Meet, Recipe.ShardsSize * f.CrownWidth, burst.ShardsScale, shardTint * soft, burst.ShardsTurn);
                    _sprites++;
                }
            }
        }
    }

    /// <summary>
    /// THE LIGHT, in the shared additive pass after every figure: the crisp pressure ring and the speed lines, then the
    /// flash; a second creature's impact is only its small flash (its splinters drawn in the material, its mist under the
    /// creatures). While the champion performs, the flash is dimmer and smaller and there are no speed lines. Three
    /// sprites a target at most.
    /// </summary>
    public void DrawLight(SpriteBatch b, IReactionStage stage, float playheadMs)
    {
        var u = playheadMs - OriginMs;
        if (u < 0f || u >= EndMs) return;
        var ring = stage.Texture(Recipe.RingKey);
        var streaks = stage.Texture(Recipe.StreaksKey);
        var flash = stage.Texture(Recipe.FlashKey);
        var burst = Burst(Recipe, u);
        var hotFrame = _hotU is { } h && u < h + Frame - 1f;
        var quiet = stage.ChampionPerforming;
        var ringTint = RingTint(Recipe, u);
        var streakTint = StreakTint(Recipe, u);
        for (var k = 0; k < Targets.Count; k++)
        {
            if (!TrySilhouette(stage, k, out var body, out var layout)) continue;
            var secondary = k != _front;
            var f = Fangs(Recipe, body, layout, u, secondary);
            if (!secondary && ring is not null && burst.RingAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCell(b, ring, 0, f.Meet, Recipe.RingSize * f.CrownWidth, burst.RingScale, ringTint * burst.RingAlpha);
                _sprites++;
            }
            if (!secondary && !quiet && streaks is not null && burst.StreaksAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCentred(b, streaks, f.Meet, Recipe.StreaksSize * f.CrownWidth, burst.StreaksScale, streakTint * burst.StreaksAlpha);
                _sprites++;
            }
            var hushed = quiet || secondary;
            var flashAlpha = burst.FlashAlpha * (hushed ? Recipe.QuietFlashAlpha : 1f);
            if (flash is not null && flashAlpha >= ReactionRecipe.VisibleFloor)
            {
                DrawCentred(b, flash, f.Meet, Recipe.FlashSize * f.CrownWidth * (hushed ? Recipe.QuietFlashSize : 1f), burst.FlashScale,
                            FlashTint(Recipe, u, hotFrame && !hushed) * flashAlpha);
                _sprites++;
            }
        }
    }
}
