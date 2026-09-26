using System;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// THE BITE IS PERFORMED AND THE HIT IS RECEIVED (ADR-012): the pure arithmetic of the pack's attack spacing, its
/// presentation lunge, the champion's recoil and the fight's usual flash. Presentation only: nothing here reads or
/// changes a Core position, a Core time or a Core outcome, and every curve is a function of the replay's playhead.
/// </summary>
/// <remarks>
/// <para>
/// WHY (the foundation study, 2026-09-26, `production/qa/evidence/bite-readability/`). Blind readers of the fight could
/// not see the enemy attack: the attack strip was a crouch in place whose contact frame equalled the frame before it,
/// its wind-up was a 5.5 fps slideshow with no acceleration, the row's only travel was a shove that BEGAN on the contact
/// frame (a recoil grammar), and the champion received the bite with a centred burst and no body response. Every
/// reader: "he doesn't flinch, stagger or recoil". The full-white hit flash (peak 1, ~200 ms) turned the struck body
/// into a blank cut-out and hid whatever was drawn on it. These are the fight's shared language, upstream of any one
/// skill, so they live here rather than in a recipe.
/// </para>
/// <para>
/// THE FOUR DIALS. (1) <see cref="WindupPhase"/> spaces the wind-up so the commit is fast: the clip's wind-up frames
/// play as a power of the wind-up, holding the early frames and rushing the last ones into the contact. (2)
/// <see cref="Lunge"/> is the pack's authored root motion in VISIBLE BODY WIDTHS: back a little in anticipation, forward
/// fast to contact, one small overshoot, home. The leader (the front living creature) gets the full travel and the
/// rest of the pack a share of it: FRONT-LED LOCKSTEP, one coordinated pack attack with one visual leader, on the one
/// beat Core's aggregate bite lands. (3) <see cref="Recoil"/> is the champion's acknowledgement: a fast small recoil
/// away from the incoming force and a controlled return, layered over whatever action he is performing, re-impulsed
/// (never accumulated) by the next bite. (4) <see cref="UsualFlash"/> is the quiet generic flash; authored actions keep
/// their own.
/// </para>
/// </remarks>
public static class BitePresentation
{
    // ── THE WIND-UP'S SPACING ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The exponent that spaces the attack clip's wind-up: the clip phase is the wind-up raised to it, so at 2.4 the
    /// last two of five wind-up frames play in the final ~150 ms and the first holds for ~500 ms. 1 is the old linear
    /// slideshow (every frame 180 ms).
    /// </summary>
    public const float WindupEase = 2.4f;

    /// <summary>The share of the wind-up at which the COMMIT is said to start (the trace's `enemy-commit`).</summary>
    public const float CommitAt = 0.70f;

    /// <summary>The clip phase (0..1 of the wind-up frames) for a wind-up of <paramref name="windup01"/>.</summary>
    public static float WindupPhase(float windup01)
        => MathF.Pow(Math.Clamp(windup01, 0f, 1f), WindupEase);

    // ── THE PACK'S LUNGE, in visible body widths (negative = toward the champion) ─────────────────────────────

    /// <summary>The leader's travel at contact, as a share of its visible body width.</summary>
    public const float LeaderLunge = 0.30f;

    /// <summary>The rest of the pack's travel, as a share of the leader's.</summary>
    public const float FollowerShare = 0.40f;

    /// <summary>The anticipation's pull back, as a share of the visible width (positive = away from the champion).</summary>
    public const float AnticipationBack = 0.04f;

    /// <summary>The share of the wind-up spent pulling back; the commit runs from here to the contact.</summary>
    public const float LungeCommitAt = 0.70f;

    /// <summary>The overshoot past the contact extension, as a share of the visible width, and when it peaks.</summary>
    public const float Overshoot = 0.03f;
    public const float OvershootMs = 30f;

    /// <summary>When the row is home again after the contact (ms).</summary>
    public const float LungeHomeMs = 260f;

    /// <summary>
    /// The lunge for one creature, in visible body widths: <paramref name="windup01"/> is the row's wind-up (0 at rest,
    /// 1 at contact; 0 when no bite is coming), <paramref name="sinceHitMs"/> the time since the last contact
    /// (negative or larger than <see cref="LungeHomeMs"/> when the follow-through is over), <paramref name="leader"/>
    /// whether this creature leads the pack.
    /// </summary>
    public static float Lunge(float windup01, float sinceHitMs, bool leader)
    {
        var share = leader ? 1f : FollowerShare;
        if (sinceHitMs >= 0f && sinceHitMs < LungeHomeMs)
        {
            // the follow-through: from full extension through one small overshoot, then eased home
            float w;
            if (sinceHitMs < OvershootMs)
                w = -LeaderLunge - Overshoot * MathF.Sin(sinceHitMs / OvershootMs * MathF.PI / 2f);
            else
            {
                var s = (sinceHitMs - OvershootMs) / (LungeHomeMs - OvershootMs);
                var eased = 1f - (1f - s) * (1f - s) * (1f - s);
                w = (-LeaderLunge - Overshoot) * (1f - eased);
            }
            return w * share;
        }
        if (windup01 <= 0f) return 0f;
        var u = Math.Clamp(windup01, 0f, 1f);
        if (u < LungeCommitAt)
        {
            var a = u / LungeCommitAt;
            return AnticipationBack * (a * a * (3f - 2f * a)) * share;            // the pull back, smooth
        }
        var v = (u - LungeCommitAt) / (1f - LungeCommitAt);
        return (AnticipationBack - (AnticipationBack + LeaderLunge) * MathF.Pow(v, 2.2f)) * share;   // the commit: fast at the end
    }

    // ── THE CHAMPION'S RECOIL ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The recoil's reach, as a share of the champion's visible body width: the brief's 2-5 % "slightly more if
    /// the footage still reads static", and it did at 4 % and 5 % (blind reads: "his body did nothing").</summary>
    public const float RecoilShare = 0.06f;

    /// <summary>The recoil's downward dip, as a share of the champion's visible body HEIGHT: a small torso impulse on
    /// the same curve, so the hit lands in the body and not only along the floor.</summary>
    public const float RecoilDipShare = 0.015f;

    /// <summary>When the recoil peaks after the contact, and when the champion is home again (ms).</summary>
    public const float RecoilPeakMs = 33f;
    public const float RecoilHomeMs = 130f;

    /// <summary>
    /// The recoil's shape, 0..1: nothing before the contact, a fast rise to 1 at <see cref="RecoilPeakMs"/>, a cubic
    /// ease home by <see cref="RecoilHomeMs"/>, nothing after. A new bite restarts it; it never adds to itself.
    /// </summary>
    public static float Recoil01(float sinceHitMs)
    {
        if (sinceHitMs < 0f || sinceHitMs >= RecoilHomeMs) return 0f;
        if (sinceHitMs < RecoilPeakMs) return MathF.Sin(sinceHitMs / RecoilPeakMs * MathF.PI / 2f);
        var s = (sinceHitMs - RecoilPeakMs) / (RecoilHomeMs - RecoilPeakMs);
        return (1f - s) * (1f - s) * (1f - s);
    }

    /// <summary>
    /// The recoil in pixels for a champion <paramref name="visibleWidth"/> wide struck by a force coming from
    /// <paramref name="fromDirection"/> (+1 = from screen-right): he moves AWAY from it.
    /// </summary>
    public static float Recoil(float sinceHitMs, float visibleWidth, int fromDirection)
        => -fromDirection * RecoilShare * visibleWidth * Recoil01(sinceHitMs);

    /// <summary>The recoil's dip in pixels (positive = down) for a champion <paramref name="visibleHeight"/> tall.</summary>
    public static float RecoilDip(float sinceHitMs, float visibleHeight)
        => RecoilDipShare * visibleHeight * Recoil01(sinceHitMs);

    // ── THE USUAL FLASH ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The fight's usual hit flash: peak 0.45 of the white mask, rising over the first 15 % of an 80 ms life
    /// (Rate = 1000 / 80 per second). Quiet on purpose: a generic hit is acknowledged, an authored action spends more.
    /// </summary>
    public static readonly (float Peak, float Rise, float Rate) UsualFlash = (0.45f, 0.15f, 12.5f);
}
