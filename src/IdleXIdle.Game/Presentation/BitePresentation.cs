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
    /// The exponent that spaces the attack clip's wind-up: the clip phase is the wind-up raised to it. At 1.8 the five
    /// wind-up frames of a 900 ms wind-up change at about −530, −360, −225 and −108 ms: the rest holds, the coil takes
    /// the middle, and the two commit poses (the maw opening, the pre-contact thrust) each get a readable ~110 ms. At
    /// 2.4 (the first pass) the commit was crammed into the last ~100 ms and read as "popped forward"; 1 is the old
    /// linear slideshow (every frame 180 ms).
    /// </summary>
    public const float WindupEase = 1.8f;

    /// <summary>The share of the wind-up at which the COMMIT is said to start (the trace's `enemy-commit`): −220 ms of 900.</summary>
    public const float CommitAt = 0.755f;

    /// <summary>The clip phase (0..1 of the wind-up frames) for a wind-up of <paramref name="windup01"/>.</summary>
    public static float WindupPhase(float windup01)
        => MathF.Pow(Math.Clamp(windup01, 0f, 1f), WindupEase);

    // ── THE PACK'S LUNGE, in visible body widths (negative = toward the champion) ─────────────────────────────

    /// <summary>The leader's travel at contact, as a share of its visible body width. The second pass (2026-09-27) films
    /// 20 / 25 / 30 % over the authored strip (<c>RH_SHOT_LUNGE</c>): root motion amplifies the pose, so the smallest
    /// travel that makes a convincing approach wins.</summary>
    public const float LeaderLunge = 0.20f;

    /// <summary>The rest of the pack's travel, as a share of the leader's.</summary>
    public const float FollowerShare = 0.40f;

    /// <summary>The anticipation's pull back, as a share of the visible width (positive = away from the champion).</summary>
    public const float AnticipationBack = 0.04f;

    /// <summary>The share of the wind-up spent pulling back; the commit runs from here to the contact (−220 ms of 900).</summary>
    public const float LungeCommitAt = 0.755f;

    /// <summary>
    /// The commit's ease-in exponent. Near-linear on purpose: the first pass's 2.2 put most of the travel in the last
    /// 50 ms and read as a position pop; at 1.15 the leader has ~33 % of its travel done 120 ms before contact and
    /// ~71 % at 50 ms, so a lunge is seen crossing the gap over two or three poses.
    /// </summary>
    public const float CommitEase = 1.15f;

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
    public static float Lunge(float windup01, float sinceHitMs, bool leader, float travel = LeaderLunge)
    {
        var share = (leader ? 1f : FollowerShare) * (travel / LeaderLunge);
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
        return (AnticipationBack - (AnticipationBack + LeaderLunge) * MathF.Pow(v, CommitEase)) * share;   // the commit: a lunge, not a pop
    }

    // ── THE CHAMPION'S RECOIL ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The recoil's reach, as a share of the champion's visible body width. ZERO BY DEFAULT for a routine hit (the
    /// reset, 2026-09-27): 4, 5, 6, 8 and 10 % all read as the sprite being translated, not as a body absorbing
    /// force, so a normal auto-battle bite is told by the attacker's motion, the contact motif, the short flash, the
    /// sound and the health change instead. The curve and its layering stay for stronger authored reactions later;
    /// the capture rig can dial it (<c>CaptureViews.RecoilOverride</c>) to film a micro-impulse or a series.
    /// </summary>
    public const float RecoilShare = 0f;

    /// <summary>The recoil's downward dip, as a share of the champion's visible body HEIGHT per unit of recoil share
    /// (a 10 % recoil dipped 1.5 %), on the same curve. Zero with the recoil.</summary>
    public const float RecoilDipPerShare = 0.15f;

    // ── THE BITE CONTACT MOTIF ────────────────────────────────────────────────────────────────────────────────

    /// <summary>How long before the contact the motif's two fangs appear, open (ms).</summary>
    public const float MotifOpenMs = 24f;

    /// <summary>After the contact: how long the fangs stay shut at full strength, and when the residue is gone (ms).</summary>
    public const float MotifHoldMs = 16f;
    public const float MotifGoneMs = 70f;

    /// <summary>
    /// The motif's opening, 0 = shut, 1 = fully open: open on the frame before the contact
    /// (<paramref name="leadMs"/> &gt; 0 within <see cref="MotifOpenMs"/>), shut from the contact on. -1 when not shown.
    /// </summary>
    public static float MotifOpen(float leadMs, float sinceMs)
    {
        // the NEXT bite's lead is asked first: the screen keeps one `since the last bite` clock, which is stale (large
        // and positive) by the time the next bite is about to land
        if (leadMs > 0f && leadMs <= MotifOpenMs) return Math.Clamp(leadMs / MotifOpenMs, 0.35f, 1f);
        return sinceMs >= 0f && sinceMs < MotifGoneMs ? 0f : -1f;
    }

    /// <summary>The motif's strength, 0..1: faint while open, full on the snap, a short residue, gone by <see cref="MotifGoneMs"/>.</summary>
    public static float MotifStrength(float leadMs, float sinceMs)
    {
        if (leadMs > 0f && leadMs <= MotifOpenMs) return 0.6f;
        if (sinceMs < 0f || sinceMs >= MotifGoneMs) return 0f;
        if (sinceMs < MotifHoldMs) return 1f;
        var s = (sinceMs - MotifHoldMs) / (MotifGoneMs - MotifHoldMs);
        return (1f - s) * (1f - s);
    }

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
    public static float Recoil(float sinceHitMs, float visibleWidth, int fromDirection, float share = RecoilShare)
        => -fromDirection * share * visibleWidth * Recoil01(sinceHitMs);

    /// <summary>The recoil's dip in pixels (positive = down) for a champion <paramref name="visibleHeight"/> tall.</summary>
    public static float RecoilDip(float sinceHitMs, float visibleHeight, float share = RecoilShare)
        => RecoilDipPerShare * share * visibleHeight * Recoil01(sinceHitMs);

    // ── THE USUAL FLASH ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The fight's usual hit flash: peak 0.45 of the white mask, rising over the first 15 % of an 80 ms life
    /// (Rate = 1000 / 80 per second). Quiet on purpose: a generic hit is acknowledged, an authored action spends more.
    /// </summary>
    public static readonly (float Peak, float Rise, float Rate) UsualFlash = (0.45f, 0.15f, 12.5f);
}
