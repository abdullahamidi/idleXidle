using System.Globalization;
using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// HUNT's combat feedback, in the two places it can be pinned without a GraphicsDevice: what a damage
/// callout SAYS (UI polish brief §61–§64), and the one-shot motion contract every piece of shield
/// feedback on that screen is built on (§30–§32, §65–§70).
/// </summary>
/// <remarks>
/// The screen itself needs a <c>UiKit</c> and therefore a device, so it is verified by the capture rig
/// (tools/asset-pipeline/capture.sh fight / fightshield / fightshieldbroken / fightmulti, plus the
/// RH_SHOT_SHIELDFX poses). These are the parts a screenshot cannot prove: the exact wording of a
/// caption, and that a pulse ends where it started rather than running forever.
/// </remarks>
public class HuntFeedbackTests
{
    // ── The callout's words ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_plain_blow_is_its_number_alone()
        => Assert.Equal("-12", HuntScreen.DamageCalloutText(12, crit: false, hits: 1));

    [Fact]
    public void test_a_true_critical_still_says_critical()
        => Assert.Equal("-12 CRITICAL", HuntScreen.DamageCalloutText(12, crit: true, hits: 1));

    [Fact]
    public void test_a_reaction_blow_carries_the_skills_own_name_not_critical()
    {
        // A Reaction answers every bite, so its blow is graded up on EVERY bite — captioning that
        // CRITICAL taught the player that a critical is the ordinary case and left the skill that
        // actually fired unnamed. The skill's word, and no grade it did not earn.
        var text = HuntScreen.DamageCalloutText(4, crit: false, hits: 1, critWord: "JAWS");
        Assert.Equal("-4 JAWS", text);
        Assert.DoesNotContain("CRITICAL", text);
    }

    [Fact]
    public void test_a_reaction_that_also_crits_says_both()
    {
        // The two questions are separate since 2026-09-09 — WHICH SKILL landed it, and WHETHER the
        // roll beat the odds. A Reaction can do both, and before the split the caption could say
        // only one of them (and in practice never CRITICAL, because the flag that chose between them
        // meant "a Reaction").
        Assert.Equal("-4 JAWS CRITICAL", HuntScreen.DamageCalloutText(4, crit: true, hits: 1, critWord: "JAWS"));
    }

    [Fact]
    public void test_the_multi_hit_fold_survives_on_both_grades()
    {
        // §21: one creature, one instant, one number — with the count that makes it read as one cast.
        Assert.Equal("-635 ×5", HuntScreen.DamageCalloutText(635, crit: false, hits: 5));
        Assert.Equal("-635 JAWS ×5", HuntScreen.DamageCalloutText(635, crit: false, hits: 5, critWord: "JAWS"));
        Assert.Equal("-635 CRITICAL ×5", HuntScreen.DamageCalloutText(635, crit: true, hits: 5));
    }

    [Fact]
    public void test_a_big_number_is_grouped_so_it_can_be_read_at_a_glance()
        => Assert.Equal("-1,067 ×4",
            HuntScreen.DamageCalloutText(1067, crit: false, hits: 4).Replace(
                CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator, ","));

    // ── The one-shot contract the shield feedback rides on ─────────────────────────────────────────
    //
    // The three shield beats — the bar's rim on a grant, the barrier's notch on an absorb, and the
    // burst on a break — are UiMotion pulses armed on the sim's event. What has to hold is: full at the
    // instant it fires, part-way through its middle, and GONE at its end. A pulse that never reached
    // zero would be the "nothing flashes continuously" law broken by a rounding error.

    [Fact]
    public void test_a_shield_pulse_runs_once_from_full_to_nothing()
    {
        UiMotion.Clear();
        var key = System.HashCode.Combine("test.shield");

        // t = 0: armed, at full.
        UiMotion.Flash(key, UiMotion.Transition);
        Assert.True(UiMotion.Pulsing(key));
        Assert.Equal(1f, UiMotion.Pulse(key), 3);

        // t = half: half spent, and still the same one shot.
        UiMotion.Tick(UiMotion.Transition / 2f);
        Assert.InRange(UiMotion.Pulse(key), 0.4f, 0.6f);

        // t = its length: finished, forgotten, and reading zero for anything that asks.
        UiMotion.Tick(UiMotion.Transition);
        Assert.False(UiMotion.Pulsing(key));
        Assert.Equal(0f, UiMotion.Pulse(key));

        // ...and nothing re-arms it. Ticking forever leaves it at zero.
        UiMotion.Tick(1f);
        Assert.Equal(0f, UiMotion.Pulse(key));
    }

    [Theory]
    [InlineData(0.10f)]   // Fast — the absorb notch
    [InlineData(0.18f)]   // Transition — the gain rim, and a skill tile's cast
    [InlineData(0.35f)]   // Reward — the break burst
    public void test_every_band_this_screen_uses_is_inside_the_briefs_own_durations(float seconds)
    {
        // §31: fast 80–120 ms, transition 150–220 ms, reward 250–450 ms. The screen names the constants
        // rather than typing numbers; this is the assertion that the constants are still the bands.
        Assert.Contains(seconds, new[] { UiMotion.Fast, UiMotion.Transition, UiMotion.Reward });
        Assert.InRange(seconds, 0.08f, 0.45f);
    }

    [Fact]
    public void test_reduced_motion_lands_on_the_same_end_state_instantly()
    {
        // §32 / the polish law: Reduced Motion is not "no feedback", it is the SAME end state with no
        // travel. An ease asked for 1 returns 1 on the first frame instead of climbing to it.
        UiMotion.Clear();
        var key = System.HashCode.Combine("test.hover");
        try
        {
            UiMotion.Reduced = false;
            UiMotion.Tick(1f / 60f);
            var climbing = UiMotion.Ease(key, 1f, UiMotion.Fast);

            UiMotion.Clear();
            UiMotion.Reduced = true;
            var instant = UiMotion.Ease(key, 1f, UiMotion.Fast);

            Assert.True(climbing < 1f, "an eased hover should still be on its way up on frame one");
            Assert.Equal(1f, instant);
        }
        finally
        {
            UiMotion.Reduced = false;
            UiMotion.Clear();
        }
    }
}
