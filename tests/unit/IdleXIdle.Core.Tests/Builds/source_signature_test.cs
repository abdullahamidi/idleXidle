using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// SIGNATURES — every Source does one small mechanical thing when its skills land.
/// </summary>
/// <remarks>
/// Each test pairs the signature under proof with a CONTROL Source whose own signature is provably
/// inert in that arena (no armour to bend, a foe too healthy to execute), and picks a target Source
/// against which BOTH attackers hold the same matchup — so the only living difference is the
/// signature itself. The pairing is SEARCHED from <see cref="SourceMatchup.Effectiveness"/>, never
/// hand-listed, so an enum reorder cannot silently break the premise.
/// </remarks>
public class SourceSignatureTest
{
    /// <summary>A target Source both attackers are equally effective against — the level arena.</summary>
    private static Source EqualTarget(Source a, Source b)
        => Enum.GetValues<Source>().First(t =>
            MathF.Abs(SourceMatchup.Effectiveness(a, t)
                      - SourceMatchup.Effectiveness(b, t)) < 0.001f);

    private static Build OneSkill(Source source)
    {
        var b = new Build();
        b.Weave(TestBuilds.Skill("hammer_blow", source));
        return b;
    }

    private static (float Delivered, Champion Champ, WaveCreature Foe) Run(Build build, WaveCreature foe)
    {
        var champ = new Champion { MaxHealth = 200_000, Health = 200_000 };
        var metrics = new WaveMetrics();
        SoloBattle.ResolveWave(
            champ, build, new IdleXIdle.Core.Economy.Hunter(),
            new List<WaveCreature> { foe }, enemyIntervalMs: 900,
            IdleXIdle.Core.Expeditions.ExpeditionTuning.Default, new Random(7), metrics: metrics);
        return (metrics.DeliveredDamage, champ, foe);
    }

    [Fact]
    public void test_signature_body_wounds_ramp_the_whole_fight()
    {
        // Arrange: MACHINE is the control — no armour on the foe, so its signature is inert.
        var target = EqualTarget(Source.Body, Source.Machine);
        var huge = () => WaveCreature.Single(5_000_000f, 1f, 0f, target);

        // Act
        var body = Run(OneSkill(Source.Body), huge()).Delivered;
        var control = Run(OneSkill(Source.Machine), huge()).Delivered;

        // Assert: the wound ramp is real damage, not a tooltip.
        Assert.True(body > control * 1.05f, $"body {body} vs control {control}");
    }

    [Fact]
    public void test_signature_shadow_cuts_deeper_below_half_health()
    {
        // Arrange: the foe STARTS below half, so every Shadow hit qualifies. MIND is the control —
        // no MARK in either build, so its signature has no window to stretch.
        var target = EqualTarget(Source.Shadow, Source.Mind);
        var hurt = () => new WaveCreature
        {
            MaxHealth = 5_000_000f, Health = 2_000_000f, Damage = 1f, Source = target,
        };

        // Act
        var shadow = Run(OneSkill(Source.Shadow), hurt()).Delivered;
        var control = Run(OneSkill(Source.Mind), hurt()).Delivered;

        // Assert
        Assert.True(shadow > control * 1.05f, $"shadow {shadow} vs control {control}");
    }

    [Fact]
    public void test_signature_machine_bends_armour_down_to_its_cap_and_no_further()
    {
        // Arrange: an armoured foe. SHADOW is the control — the foe never drops below half.
        var target = EqualTarget(Source.Machine, Source.Shadow);

        // Act
        var (_, _, bent) = Run(OneSkill(Source.Machine),
                               WaveCreature.Single(5_000_000f, 1f, 40f, target));
        var (_, _, whole) = Run(OneSkill(Source.Shadow),
                                WaveCreature.Single(5_000_000f, 1f, 40f, target));

        // Assert: exactly the cap comes off — no more, and the control's plate is untouched.
        Assert.Equal(40f - SoloBattle.SignatureMachineStripCap * SoloBattle.SignatureMachineStrip,
                     bent.Defense);
        Assert.Equal(40f, whole.Defense);
    }

    [Fact]
    public void test_signature_nature_heals_a_sliver_of_what_it_deals()
    {
        // Arrange: real bites, so there is lost health to win back. MACHINE controls (no armour).
        var target = EqualTarget(Source.Nature, Source.Machine);
        // 800 a bite: enough to carve a visible dent, small enough that neither champion dies
        // before the wave's ceiling (a dead champion ends both runs at 0 and proves nothing).
        var huge = () => WaveCreature.Single(5_000_000f, 800f, 0f, target);

        // Act — BLOW, not DRINK: the signature must heal on its OWN, not ride the Drain style's
        // leech.
        var nature = Run(OneSkill(Source.Nature), huge()).Champ;
        var control = Run(OneSkill(Source.Machine), huge()).Champ;

        // Assert
        Assert.True(nature.Health > control.Health,
            $"nature end {nature.Health} vs control end {control.Health}");
    }

    [Fact]
    public void test_signature_spirit_primes_the_next_other_source_cast()
    {
        // Arrange: [SPIRIT, BODY] against [SHADOW, BODY] — the Body slot is identical on both
        // sides (same wounds, same timing), the foe never drops below half so Shadow is inert,
        // and the only living difference is the prime Spirit hands the Body casts.
        var target = EqualTarget(Source.Spirit, Source.Shadow);
        var huge = () => WaveCreature.Single(5_000_000f, 1f, 0f, target);

        Build Pair(Source first)
        {
            var b = new Build();
            b.Weave(TestBuilds.Skill("hammer_blow", first));
            b.Weave(TestBuilds.Skill("hammer_blow", Source.Body));
            return b;
        }

        // Act
        var primed = Run(Pair(Source.Spirit), huge()).Delivered;
        var control = Run(Pair(Source.Shadow), huge()).Delivered;

        // Assert
        Assert.True(primed > control * 1.02f, $"primed {primed} vs control {control}");
    }

    [Fact]
    public void test_signature_mind_stretches_the_mark_window()
    {
        // Arrange: both builds open MARK windows with the same Nature CALL (deals nothing, so its
        // Source is inert); the caster slot is MIND against inert SHADOW. A stretched window keeps
        // the amplifier lit for more of the casts that follow.
        var target = EqualTarget(Source.Mind, Source.Shadow);
        var huge = () => WaveCreature.Single(5_000_000f, 1f, 0f, target);

        Build MarkAnd(Source caster)
        {
            var b = new Build();
            b.Weave(TestBuilds.Skill("sign_call", Source.Nature));
            // TWO casters, so the stretches ROLL: the first cast inside a window pushes its edge
            // far enough that the second cast still lands lit, and that cast pushes it again. A
            // single caster at these cadences always misses the edge it just moved — which is the
            // realistic shape too: the signature pays in rotations, not in a one-skill vacuum.
            b.Weave(TestBuilds.Skill("hammer_blow", caster));
            b.Weave(TestBuilds.Skill("hammer_blow", caster));
            // DESYNC. The casts must land at a cadence where the window's edge matters, so the
            // stretch has something to convert. Set via Shape — the sim recomputes cooldowns from
            // each def's Beats and the build's SkillRate; an EquippedSkill carries no cooldown of
            // its own. A window-edge effect only some cadences catch, so the rate is part of the
            // fixture (probed, not guessed).
            b.Shape = SkillShape.None with { SkillRate = 1.0f };
            return b;
        }

        // Act
        var stretched = Run(MarkAnd(Source.Mind), huge()).Delivered;
        var control = Run(MarkAnd(Source.Shadow), huge()).Delivered;

        // Assert
        Assert.True(stretched > control * 1.01f, $"stretched {stretched} vs control {control}");
    }
}
