using IdleXIdle.Game;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE MASTERY SCREEN'S MOTION CONTRACT (UI polish brief §30–§32, §34, LAW 10).
/// </summary>
/// <remarks>
/// <para>
/// Everything this screen animates is photographed by the capture rig — the take's bloom, the wire
/// lighting, the hover, the press, the selection ring, the ceremony's fade — with one exception:
/// <b>Reduced Motion</b>. The rig poses UI SCALE (<c>RH_SHOT_UISCALE</c>) but has no dial for the
/// accessibility setting, and the host that owns it is frozen this round, so that state cannot be
/// posed and cannot be looked at. This file is what stands in for the photograph: it drives the same
/// functions the screen draws from, at t = 0, mid and end, on both settings.
/// </para>
/// <para>
/// It is deliberately about the FUNCTIONS and not about the screen: <see cref="MasteryScreen"/> cannot
/// be constructed without a GraphicsDevice, which this project keeps out of the test assemblies.
/// </para>
/// </remarks>
public class MasteryMotionTests
{
    /// <summary>Sixty frames at the rig's fixed step — the clock every duration below is measured in.</summary>
    private const float Frame = 1f / 60f;

    private static void Advance(float seconds)
    {
        for (var t = 0f; t < seconds; t += Frame) UiMotion.Tick(Frame);
    }

    [Fact]
    public void test_the_breath_moves_normally_and_holds_at_its_brightest_under_reduced_motion()
    {
        try
        {
            // NORMAL: it is a breath — it moves, and it never leaves the band the halo is drawn in.
            UiMotion.Reduced = false;
            var seen = new HashSet<float>();
            for (var t = 0.0; t < 6.0; t += 0.05)
            {
                var v = MasteryScreen.BreathPhase(t);
                Assert.InRange(v, 0.10f, 1.0001f);
                seen.Add(MathF.Round(v, 2));
            }
            Assert.True(seen.Count > 10, "the breath must actually move when motion is allowed");

            // REDUCED: the same six nodes keep the HIGHLIGHT and lose the MOTION — held at the top of
            // the breath, which is the brightest state the halo ever reaches, at every instant.
            UiMotion.Reduced = true;
            for (var t = 0.0; t < 6.0; t += 0.05) Assert.Equal(1f, MasteryScreen.BreathPhase(t));
        }
        finally { UiMotion.Reduced = false; }
    }

    [Fact]
    public void test_the_ceremony_fades_in_over_a_transition_and_is_open_at_once_under_reduced_motion()
    {
        try
        {
            // t = 0 (the flash fires, the whole pulse is left), mid, and the end.
            UiMotion.Reduced = false;
            Assert.Equal(0f, MasteryScreen.CeremonyFade(1f));
            Assert.Equal(0.5f, MasteryScreen.CeremonyFade(0.5f), 3);
            Assert.Equal(1f, MasteryScreen.CeremonyFade(0f));
            // Monotone: a fade that went backwards would read as a flicker.
            var last = -1f;
            for (var left = 1f; left >= 0f; left -= 0.05f)
            {
                var v = MasteryScreen.CeremonyFade(left);
                Assert.True(v >= last, "the ceremony's fade must never go backwards");
                last = v;
            }

            // REDUCED: the SAME end state, reached instantly — never a half-lit modal.
            UiMotion.Reduced = true;
            Assert.Equal(1f, MasteryScreen.CeremonyFade(1f));
            Assert.Equal(1f, MasteryScreen.CeremonyFade(0.5f));
            Assert.Equal(1f, MasteryScreen.CeremonyFade(0f));
        }
        finally { UiMotion.Reduced = false; }
    }

    [Fact]
    public void test_a_take_pulse_plays_once_and_then_is_over()
    {
        UiMotion.Reduced = false;
        UiMotion.Clear();
        var key = 0x5EED;

        // t = 0: the whole pulse is ahead of it.
        UiMotion.Flash(key, UiMotion.Transition);
        Assert.Equal(1f, UiMotion.Pulse(key));
        Assert.True(UiMotion.Pulsing(key));

        // MID: about half of a Transition left, so the bloom is about half as bright.
        Advance(UiMotion.Transition / 2f);
        Assert.InRange(UiMotion.Pulse(key), 0.35f, 0.65f);

        // END: over, and — this is the law — it does NOT come back. Nothing re-arms it but another
        // take, so a node cannot sit there flashing at the player.
        Advance(UiMotion.Transition);
        Assert.Equal(0f, UiMotion.Pulse(key));
        Assert.False(UiMotion.Pulsing(key));
        Advance(1f);
        Assert.Equal(0f, UiMotion.Pulse(key));

        UiMotion.Clear();
    }

    [Fact]
    public void test_a_hover_lands_on_the_same_state_whether_or_not_motion_is_reduced()
    {
        try
        {
            // NORMAL: a hover fades IN over Fast, and it is FULLY lit at the end of it.
            UiMotion.Reduced = false;
            UiMotion.Clear();
            var key = 0xB0BA;
            var first = UiMotion.Ease(key, 1f, UiMotion.Fast);
            Assert.InRange(first, 0f, 0.999f);
            for (var t = 0f; t < UiMotion.Fast * 2f; t += Frame) { UiMotion.Tick(Frame); UiMotion.Ease(key, 1f, UiMotion.Fast); }
            Assert.Equal(1f, UiMotion.Ease(key, 1f, UiMotion.Fast));

            // REDUCED: the same end state, on the first frame — the state change without the fade.
            UiMotion.Reduced = true;
            UiMotion.Clear();
            Assert.Equal(1f, UiMotion.Ease(key, 1f, UiMotion.Fast));
            Assert.Equal(0f, UiMotion.Ease(key, 0f, UiMotion.Fast));
        }
        finally { UiMotion.Reduced = false; UiMotion.Clear(); }
    }
}
