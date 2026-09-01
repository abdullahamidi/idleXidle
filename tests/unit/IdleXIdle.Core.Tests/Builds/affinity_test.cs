using System;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A specialisation must be worth specialising in.
/// </summary>
/// <remarks>
/// The mastery tree sells one Specialisation per <see cref="Style"/>: take it and skills of your
/// discipline hit far harder (x2.00) while the far side of the ring hits softer (x0.75 off, x0.45
/// opposite). <b>The old MARK SPECIALIST — today's SIGN — was strictly negative.</b> Affinity is
/// applied inside <c>Amp()</c>, and an amplifier deals no damage, so the x2.00 landed on nothing
/// while the off-style penalties landed on everything else. Six points to make yourself weaker.
/// The fix points a SIGN adept's x2.00 at the amplifier's DEPTH — the one lever that scales with
/// the whole build (see SoloBattle's amplify block).
///
/// This file measures every affinity against the real simulation rather than reading the table, so a
/// bonus that is defined but never reaches a formula fails here the way it failed the player.
/// </remarks>
public class AffinityTest
{
    private readonly ITestOutputHelper _out;

    public AffinityTest(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// One equipped copy of a catalogue skill at its base line. The id is the identity; cadence is
    /// the def's own (an Active counts its <c>Beats</c>, a Reaction re-arms on its <c>RearmMs</c>).
    /// </summary>
    private static EquippedSkill Skill(string id) => TestBuilds.Skill(id);

    /// <summary>A build woven entirely from one style's active skill, optionally with an affinity declared.</summary>
    private static Build Of(Style style, Style? affinity, int skills = 4)
    {
        var b = new Build { Affinity = affinity };
        for (var i = 0; i < skills; i++)
            b.Weave(Skill(SkillCatalogue.ActiveOf(style).Id));
        return b;
    }

    /// <summary>
    /// A Sign build cannot be all Signs — a Sign amplifies something, so it needs a partner. Hammer
    /// partners sit two steps around the ring (x0.75 under a SIGN affinity), the same distance the old
    /// fixture's Strike partners sat from Mark, so the specialisation still has a real off-style
    /// penalty to out-earn.
    /// </summary>
    private static Build SignBuild(Style? affinity)
    {
        var b = new Build { Affinity = affinity };
        b.Weave(Skill("sign_call"));
        for (var i = 0; i < 3; i++)
            b.Weave(Skill("hammer_blow"));
        return b;
    }

    /// <summary>
    /// A Snare build: REPAY pays back the damage you have taken, JAWS answers the bite that dealt it.
    /// "Being attacked works in your favour" — which is why it must be measured on a dummy that bites.
    /// </summary>
    private static Build SnareBuild(Style? affinity)
    {
        var b = new Build { Affinity = affinity };
        b.Weave(Skill("snare_repay"));
        b.Weave(Skill("snare_jaws"));
        b.Weave(Skill("snare_repay"));
        b.Weave(Skill("snare_jaws"));
        return b;
    }

    /// <summary>
    /// The same instrument as <see cref="DamageBench"/> — the real <see cref="SoloBattle.ResolveWave"/>
    /// against an unkillable dummy over one full tick window — except this dummy BITES. SNARE's entire
    /// output is an answer to being attacked, and the standard bench's dummy never swings, so a Snare
    /// build reads 0 dmg/s there with or without its specialisation. The champion's pool is minted deep
    /// enough that the whole window is always measured.
    /// </summary>
    private static float BittenDps(Build build, Hunter hunter)
    {
        var t = ExpeditionTuning.Default;
        var champ = new Champion { MaxHealth = 1_000_000_000, Health = 1_000_000_000 };

        var (_, events) = SoloBattle.ResolveWave(
            champ, build, hunter, DamageBench.DummyHealth, enemyDamage: 40f, enemyIntervalMs: 1500,
            t, new Random(1234));

        var total = events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => (long)e.Amount);
        return total / (t.TickCeilingMs / 1000f);
    }

    [Fact]
    public void test_taking_the_sign_specialisation_makes_a_sign_build_stronger()
    {
        // THE REGRESSION THIS FILE EXISTS FOR. Same build, same skills, the only difference being
        // whether the player spent six mastery points on the SIGN specialisation. The adept's x2.00
        // must land on the amplifier's depth, because a Sign skill has no hit of its own to multiply.
        var hunter = new Hunter();

        var without = DamageBench.Measure(SignBuild(null), hunter).Dps;
        var with = DamageBench.Measure(SignBuild(Style.Sign), hunter).Dps;

        _out.WriteLine($"a Sign build: {without:N0} dmg/s without the specialisation, {with:N0} with it "
                       + $"({(with / MathF.Max(0.01f, without) - 1f) * 100f:+0;-0}%)");

        Assert.True(with > without,
            $"the SIGN specialisation leaves the build at {with:N0} dmg/s against {without:N0} without it. "
            + "It is a six-point node that can only make you weaker — the bonus reaches no formula while "
            + "the off-style penalties reach every other skill.");
    }

    [Fact]
    public void test_every_style_specialisation_is_worth_taking_for_that_style()
    {
        // The general property. A node that is negative for the very build it is named after is broken,
        // whichever style it is — and reading the affinity TABLE would not have caught Mark (Sign's
        // ancestor), because the table was right and the formula never asked it.
        var hunter = new Hunter();

        foreach (var style in Enum.GetValues<Style>())
        {
            float without, with;
            if (style == Style.Snare)
            {
                // SNARE only acts when attacked, so it is the one style the no-contact bench is blind to.
                without = BittenDps(SnareBuild(null), hunter);
                with = BittenDps(SnareBuild(style), hunter);
            }
            else
            {
                var build = style == Style.Sign ? SignBuild(null) : Of(style, null);
                var spec = style == Style.Sign ? SignBuild(style) : Of(style, style);
                without = DamageBench.Measure(build, hunter).Dps;
                with = DamageBench.Measure(spec, hunter).Dps;
            }

            var pct = (with / MathF.Max(0.01f, without) - 1f) * 100f;
            _out.WriteLine($"   {style,-15} {without,10:N0} -> {with,10:N0}   {pct,+7:+0.0;-0.0}%");

            Assert.True(with > without,
                $"the {style} specialisation makes a {style} build WEAKER ({with:N0} against {without:N0}).");
        }
    }

    [Fact]
    public void test_a_specialisation_still_costs_you_on_far_styles()
    {
        // The counterweight. If the fix had simply made affinity free, the tree would be selling a
        // strict upgrade instead of a commitment, and every player would take every node. The price is
        // the ring's own geometry: HAMMER<->VOLLEY is a designed opposition (x0.45).
        var hunter = new Hunter();

        var neutral = DamageBench.Measure(Of(Style.Volley, null), hunter).Dps;
        var farStyle = DamageBench.Measure(Of(Style.Volley, Style.Hammer), hunter).Dps;

        _out.WriteLine($"a Volley build under a HAMMER affinity: {farStyle:N0} against {neutral:N0} neutral");

        Assert.True(farStyle < neutral,
            "specialising in HAMMER left a VOLLEY build no worse off — the commitment has no price, "
            + "and Hammer's ring opposite is exactly where it must bite hardest.");
    }
}
