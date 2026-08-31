using System;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A specialisation must be worth specialising in.
/// </summary>
/// <remarks>
/// The mastery tree sells one node per Form: take it and that Form hits far harder while the others
/// hit softer. <b>MARK SPECIALIST was strictly negative.</b> Affinity is applied inside
/// <c>Amp()</c>, and Mark's branch returns before ever reaching it — Mark deals no damage, so it has
/// no hit to multiply. The x2.00 landed on nothing while the off-Form penalties (Projectile to x0.45,
/// Strike and Aura to x0.75) landed on everything else. Six points to make yourself weaker.
///
/// This file measures every affinity against the real simulation rather than reading the table, so a
/// bonus that is defined but never reaches a formula fails here the way it failed the player.
/// </remarks>
public class AffinityTest
{
    private readonly ITestOutputHelper _out;

    public AffinityTest(ITestOutputHelper output) => _out = output;

    /// <summary>A build whose skills are all one Form, optionally with an affinity declared.</summary>
    private static Build Of(Form form, Form? affinity, int skills = 4)
    {
        var b = new Build { Affinity = affinity };
        for (var i = 0; i < skills; i++)
            b.Weave(new EquippedSkill(
                new WovenAbility { Name = "S", Source = Source.Body, Form = form },
                FormBehaviour.BaseCooldownMs(form)));
        return b;
    }

    /// <summary>A Mark build cannot be all Marks — a Mark amplifies something, so it needs a partner.</summary>
    private static Build MarkBuild(Form? affinity)
    {
        var b = new Build { Affinity = affinity };
        b.Weave(new EquippedSkill(
            new WovenAbility { Name = "M", Source = Source.Body, Form = Form.Mark },
            FormBehaviour.BaseCooldownMs(Form.Mark)));
        for (var i = 0; i < 3; i++)
            b.Weave(new EquippedSkill(
                new WovenAbility { Name = "S", Source = Source.Body, Form = Form.Strike },
                FormBehaviour.BaseCooldownMs(Form.Strike)));
        return b;
    }

    [Fact]
    public void test_taking_the_mark_specialisation_makes_a_mark_build_stronger()
    {
        // THE REGRESSION THIS FILE EXISTS FOR. Same build, same skills, the only difference being
        // whether the player spent six mastery points on MARK SPECIALIST.
        var hunter = new Hunter();

        var without = DamageBench.Measure(MarkBuild(null), hunter).Dps;
        var with = DamageBench.Measure(MarkBuild(Form.Mark), hunter).Dps;

        _out.WriteLine($"a Mark build: {without:N0} dmg/s without the specialisation, {with:N0} with it "
                       + $"({(with / MathF.Max(0.01f, without) - 1f) * 100f:+0;-0}%)");

        Assert.True(with > without,
            $"MARK SPECIALIST leaves the build at {with:N0} dmg/s against {without:N0} without it. It is "
            + "a six-point node that can only make you weaker — the bonus reaches no formula while the "
            + "off-Form penalties reach every other skill.");
    }

    [Fact]
    public void test_every_form_specialisation_is_worth_taking_for_that_form()
    {
        // The general property. A node that is negative for the very build it is named after is broken,
        // whichever Form it is — and reading the affinity TABLE would not have caught Mark, because the
        // table was right and the formula never asked it.
        var hunter = new Hunter();

        foreach (var form in Enum.GetValues<Form>())
        {
            var build = form == Form.Mark ? MarkBuild(null) : Of(form, null);
            var spec = form == Form.Mark ? MarkBuild(form) : Of(form, form);

            var without = DamageBench.Measure(build, hunter).Dps;
            var with = DamageBench.Measure(spec, hunter).Dps;
            var pct = (with / MathF.Max(0.01f, without) - 1f) * 100f;

            _out.WriteLine($"   {form,-15} {without,10:N0} -> {with,10:N0}   {pct,+7:+0.0;-0.0}%");

            Assert.True(with > without,
                $"{form} SPECIALIST makes a {form} build WEAKER ({with:N0} against {without:N0}).");
        }
    }

    [Fact]
    public void test_a_specialisation_still_costs_you_on_the_other_forms()
    {
        // The counterweight. If the fix had simply made affinity free, the tree would be selling a
        // strict upgrade instead of a commitment, and every player would take every node.
        var hunter = new Hunter();

        var neutral = DamageBench.Measure(Of(Form.Projectile, null), hunter).Dps;
        var offForm = DamageBench.Measure(Of(Form.Projectile, Form.Mark), hunter).Dps;

        _out.WriteLine($"a Projectile build under a MARK affinity: {offForm:N0} against {neutral:N0} neutral");

        Assert.True(offForm < neutral,
            "specialising in MARK left a PROJECTILE build no worse off — the commitment has no price.");
    }
}
