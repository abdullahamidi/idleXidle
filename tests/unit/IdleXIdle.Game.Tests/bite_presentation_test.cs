using System;
using IdleXIdle.Game.Presentation;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE BITE IS PERFORMED AND THE HIT IS RECEIVED (ADR-012): the pure curves of <see cref="BitePresentation"/>. They
/// are presentation arithmetic on the replay's playhead; these pin their shape, their bounds and their timing so a
/// retune cannot silently turn the attack back into a recoil grammar or let a recoil accumulate.
/// </summary>
public class bite_presentation_test
{
    // ── the wind-up's spacing ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_windup_phase_is_monotonic_and_reaches_contact()
    {
        var last = -1f;
        for (var u = 0f; u <= 1f; u += 0.01f)
        {
            var p = BitePresentation.WindupPhase(u);
            Assert.True(p >= last, $"phase fell at {u}");
            last = p;
        }
        Assert.Equal(0f, BitePresentation.WindupPhase(0f));
        Assert.Equal(1f, BitePresentation.WindupPhase(1f), 5);
        Assert.Equal(0f, BitePresentation.WindupPhase(-1f));
        Assert.Equal(1f, BitePresentation.WindupPhase(2f), 5);
    }

    [Fact]
    public void test_windup_spacing_holds_the_first_frame_and_rushes_the_last()
    {
        // five wind-up frames: the phase's fifths are the frames; the first must own most of the wind-up and the
        // last two must fit inside its final ~20 % (the commit), which is the whole point of the spacing
        float FrameAt(float u) => MathF.Floor(BitePresentation.WindupPhase(u) * 5f);
        Assert.Equal(0f, FrameAt(0.40f));            // well into the wind-up, still the rest frame
        Assert.True(FrameAt(0.80f) <= 3f);
        Assert.Equal(4f, FrameAt(0.98f));            // the last frame, just before contact
        // the two commit poses each get a readable stretch: the third frame starts 160-260 ms before contact of 900,
        // the last 80-140 ms before it (the polish brief: the commit begins 160-220 ms out, 2-3 poses participate)
        float StartOf(int frame) { for (var u = 0f; u <= 1f; u += 0.001f) if (FrameAt(u) >= frame) return (1f - u) * 900f; return 0f; }
        Assert.InRange(StartOf(3), 160f, 260f);
        Assert.InRange(StartOf(4), 80f, 140f);
    }

    [Fact]
    public void test_lunge_commit_is_a_lunge_not_a_pop()
    {
        // the polish brief's spacing: ~200 ms out the forward motion begins; ~120 ms out 35-50 % of the travel is
        // done; ~50 ms out 65-85 %; contact 100 %
        float Share(float msBefore) => BitePresentation.Lunge(1f - msBefore / 900f, -1f, leader: true) / -BitePresentation.LeaderLunge;
        Assert.True(Share(220f) <= 0f, "still at or behind home 220 ms out");
        Assert.InRange(Share(120f), 0.30f, 0.50f);
        Assert.InRange(Share(50f), 0.65f, 0.85f);
        Assert.Equal(1f, Share(0f), 3);
    }

    // ── the pack's lunge ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_lunge_pulls_back_then_arrives_extended_on_the_contact()
    {
        Assert.Equal(0f, BitePresentation.Lunge(0f, -1f, leader: true));                 // nothing coming: home
        Assert.True(BitePresentation.Lunge(0.5f, -1f, leader: true) > 0f, "anticipation pulls BACK (away from the champion)");
        Assert.True(BitePresentation.Lunge(0.5f, -1f, leader: true) <= BitePresentation.AnticipationBack + 1e-4f);
        Assert.Equal(-BitePresentation.LeaderLunge, BitePresentation.Lunge(1f, -1f, leader: true), 4);   // full extension at contact
        // the rush is at the end: most of the travel happens in the last 15 % of the wind-up
        var at85 = BitePresentation.Lunge(0.85f, -1f, leader: true);
        var at100 = BitePresentation.Lunge(1f, -1f, leader: true);
        Assert.True(at100 - at85 < -(BitePresentation.LeaderLunge * 0.5f), $"only {at100 - at85} of the travel in the last 15 %");
    }

    [Fact]
    public void test_lunge_follow_through_overshoots_once_and_comes_home()
    {
        var atContact = BitePresentation.Lunge(1f, 0f, leader: true);
        var atOvershoot = BitePresentation.Lunge(0f, BitePresentation.OvershootMs, leader: true);
        Assert.True(atOvershoot < atContact, "one small overshoot past the extension");
        Assert.Equal(-(BitePresentation.LeaderLunge + BitePresentation.Overshoot), atOvershoot, 4);
        // home, and never past home, by the end of the follow-through
        var last = atOvershoot;
        for (var ms = BitePresentation.OvershootMs; ms < BitePresentation.LungeHomeMs; ms += 5f)
        {
            var w = BitePresentation.Lunge(0f, ms, leader: true);
            Assert.True(w >= last - 1e-5f && w <= 0f, $"the return must ease home monotonically ({w} at {ms})");
            last = w;
        }
        Assert.Equal(0f, BitePresentation.Lunge(0f, BitePresentation.LungeHomeMs, leader: true));
        Assert.Equal(0f, BitePresentation.Lunge(0f, 1000f, leader: true));
    }

    [Fact]
    public void test_lunge_is_front_led_the_pack_moves_a_share()
    {
        var leader = BitePresentation.Lunge(1f, -1f, leader: true);
        var follower = BitePresentation.Lunge(1f, -1f, leader: false);
        Assert.Equal(leader * BitePresentation.FollowerShare, follower, 4);
        Assert.True(BitePresentation.FollowerShare > 0f && BitePresentation.FollowerShare < 1f, "the pack still visibly participates");
    }

    // ── the champion's recoil ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_recoil_is_immediate_peaks_early_and_is_home_in_time()
    {
        Assert.Equal(0f, BitePresentation.Recoil01(-1f));                                  // nothing before the bite
        Assert.True(BitePresentation.Recoil01(8f) > 0.3f, "the impulse begins on the contact");
        Assert.Equal(1f, BitePresentation.Recoil01(BitePresentation.RecoilPeakMs), 4);
        Assert.True(BitePresentation.RecoilPeakMs >= 16f && BitePresentation.RecoilPeakMs <= 50f, "peak inside 16-50 ms");
        Assert.True(BitePresentation.RecoilHomeMs >= 80f && BitePresentation.RecoilHomeMs <= 140f, "settled inside 80-140 ms");
        Assert.Equal(0f, BitePresentation.Recoil01(BitePresentation.RecoilHomeMs));
        var last = 1f;
        for (var ms = BitePresentation.RecoilPeakMs; ms <= BitePresentation.RecoilHomeMs; ms += 2f)
        {
            var r = BitePresentation.Recoil01(ms);
            Assert.True(r <= last + 1e-5f && r >= 0f, "no bounce on the way home");
            last = r;
        }
    }

    [Fact]
    public void test_recoil_is_off_for_a_routine_hit_and_moves_away_from_the_force_when_dialled()
    {
        // the routine bite slides nobody (the reset, 2026-09-27); the curve is kept for authored reactions and dials
        Assert.Equal(0f, BitePresentation.RecoilShare);
        Assert.Equal(0f, BitePresentation.Recoil(BitePresentation.RecoilPeakMs, 300f, fromDirection: +1));
        Assert.Equal(0f, BitePresentation.RecoilDip(BitePresentation.RecoilPeakMs, 400f));
        var px = BitePresentation.Recoil(BitePresentation.RecoilPeakMs, 300f, fromDirection: +1, share: 0.05f);
        Assert.True(px < 0f, "a force from the right moves him left");
        Assert.Equal(-0.05f * 300f, px, 3);
        Assert.True(BitePresentation.Recoil(BitePresentation.RecoilPeakMs, 300f, fromDirection: -1, share: 0.05f) > 0f);
    }

    [Fact]
    public void test_bite_motif_opens_before_the_contact_snaps_on_it_and_is_gone_in_time()
    {
        Assert.Equal(-1f, BitePresentation.MotifOpen(200f, -200f));                     // not yet
        Assert.True(BitePresentation.MotifOpen(16f, -16f) > 0.5f, "open on the frame before the contact");
        // the regression: the screen's `since the last bite` clock is stale (a bite 3 s ago) when the next one is about to
        // land; the open frame must still show, and be faint
        Assert.True(BitePresentation.MotifOpen(17f, 2966f) > 0.5f, "open on the frame before the NEXT contact, whatever the last one's clock says");
        Assert.InRange(BitePresentation.MotifStrength(17f, 2966f), 0.3f, 0.8f);
        Assert.Equal(0f, BitePresentation.MotifOpen(0f, 0f));                            // shut on the beat
        Assert.Equal(0f, BitePresentation.MotifOpen(-30f, 30f));
        Assert.Equal(-1f, BitePresentation.MotifOpen(-200f, 200f));                       // gone
        Assert.True(BitePresentation.MotifStrength(16f, -16f) < 1f, "faint while open");
        Assert.Equal(1f, BitePresentation.MotifStrength(0f, 0f));
        Assert.True(BitePresentation.MotifStrength(-40f, 40f) < 1f && BitePresentation.MotifStrength(-40f, 40f) > 0f, "a short residue");
        Assert.Equal(0f, BitePresentation.MotifStrength(-100f, 100f));
        Assert.True(BitePresentation.MotifGoneMs <= 100f, "no bite icon floats on the player");
    }

    [Fact]
    public void test_recoil_is_re_impulsed_never_accumulated()
    {
        // the screen keeps ONE `since the last bite` clock; a second bite restarts it. Two bites 60 ms apart thus
        // read as the shape at 60 ms, then the shape at 0: bounded by the single-bite peak, never the sum.
        var single = BitePresentation.Recoil01(BitePresentation.RecoilPeakMs);
        var afterRestart = BitePresentation.Recoil01(0f);
        Assert.True(afterRestart <= single);
        for (var ms = 0f; ms < 2000f; ms += 7f)
            Assert.True(MathF.Abs(BitePresentation.Recoil01(ms)) <= 1f);
    }

    // ── the usual flash ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_usual_flash_is_quiet_and_short()
    {
        var (peak, rise, rate) = BitePresentation.UsualFlash;
        Assert.InRange(peak, 0.40f, 0.50f);
        Assert.InRange(1000f / rate, 70f, 90f);        // ~80 ms of life
        Assert.InRange(rise, 0.05f, 0.30f);            // a short shaped rise, not a snap
    }
}
