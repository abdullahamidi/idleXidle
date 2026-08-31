using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Encounters;

/// <summary>
/// Every band affix must do the thing its name promises, in the direction its name promises.
/// </summary>
/// <remarks>
/// <para>
/// Three affixes were found doing nothing or the opposite, and the whole suite of 578 tests passed
/// through all three, because nothing had ever driven an affix through <c>SoloExpedition.PushWave</c>
/// and looked at what came out the far side. The battle tests build their creatures by hand, which is
/// exactly the seam the defects lived in — <c>PushWave</c> re-mints creatures to apply an affix, and
/// the re-mint was where the information was lost.
/// </para>
/// <para>
/// So these tests go through the real wave pipeline, and they assert DIRECTION rather than a magic
/// number: an affix that makes creatures bite more often must bite more often. A test that pins the
/// exact figure would have to be rewritten every time the band is tuned, and would still have passed
/// while SWIFT ran backwards.
/// </para>
/// </remarks>
public class AffixLivenessTests
{
    private readonly ITestOutputHelper _out;
    public AffixLivenessTests(ITestOutputHelper o) => _out = o;

    private static Build BuildWith(SkillShape shape)
    {
        var b = new Build { PassiveMods = BuildMods.None, Shape = shape };
        foreach (var f in new[] { Form.Strike, Form.Projectile, Form.Aura, Form.Mark })
            b.Weave(new EquippedSkill(
                new WovenAbility { Name = f.ToString(), Source = Source.Nature, Form = f },
                FormBehaviour.BaseCooldownMs(f)));
        return b;
    }

    // ── SIEGE AND THE PLATED RE-MINT ──────────────────────────────────────────────────────────────

    [Fact]
    public void test_siege_pays_its_bonus_against_a_plated_armoured_wave()
    {
        // THE REGRESSION TEST FOR THE INVERTED NODE. PushWave re-mints every creature to apply PLATED's
        // armour multiplier, and the re-mint copied MaxHealth, Health, Damage, Defense and Source while
        // silently dropping Archetype. WaveCreature.Archetype has exactly one reader — SIEGE, "+45% to
        // ARMOURED, -25% to everything else" — so a null tag turned the anti-armour Greater into a
        // penalty on the one wave type it was bought to answer.
        var siege = SkillShape.None with { VsArmouredBonus = 0.45f, VsOtherPenalty = 0.25f };

        // HEALTH FAR BEYOND WHAT THE WINDOW CAN CHEW. The first version of this fixture gave the
        // creature 4,000 health, both builds killed it, and both therefore "dealt" about 4,000 — the
        // test passed by 1.4% on rounding noise and would have passed just as happily with SIEGE still
        // inverted. A fixture that saturates its own metric proves nothing; the creature has to survive
        // so that the number being compared is throughput and not the target's health bar.
        const float unkillable = 1e9f;
        var armoured = new WaveCreature
        {
            MaxHealth = unkillable, Health = unkillable, Damage = 10f, Defense = 20f,
            Archetype = Archetype.Armoured,
        };
        var untagged = new WaveCreature
        {
            MaxHealth = unkillable, Health = unkillable, Damage = 10f, Defense = 20f,
        };

        var withTag = DamageInto(siege, armoured);
        var without = DamageInto(siege, untagged);
        _out.WriteLine($"SIEGE into ARMOURED {withTag:N0} · into an untagged clone of it {without:N0}");

        // The node is +45% against Armoured and -25% against everything else, so the ratio it is worth
        // is 1.45 / 0.75 = 1.93. Asserted well under that, at 1.5, because hit quantisation and flat
        // armour both eat into the raw multiplier — but far enough above 1 that noise cannot carry it.
        Assert.True(withTag > without * 1.5f,
                    "SIEGE must pay MUCH more into a creature tagged Armoured than into the same "
                    + $"creature with its tag missing — got {withTag:N0} against {without:N0}, a ratio "
                    + $"of {withTag / Math.Max(1f, without):0.00} where 1.93 is the node's own");
    }

    [Fact]
    public void test_the_plated_affix_does_not_strip_the_archetype_it_armours()
    {
        // The defect itself, at the seam, rather than through its consequence: drive real waves and
        // require that a wave whose band is Armoured produces creatures that SAY they are Armoured.
        // Every band that carries PLATED is majority-Armoured by composition (BandCycles.cs:45-65),
        // which is what made this the worst possible tag to lose.
        var run = Run("cinderworks", seed: 7);
        var checkedAny = false;

        for (var w = 1; w <= 40 && !run.Over; w++)
        {
            run.PushWave();
            if (!run.LastWaveAffixes.Contains(Affix.Plated)) continue;
            if (run.LastWaveArchetype != Archetype.Armoured) continue;

            checkedAny = true;
            Assert.Contains(run.LastWaveCreatures, c => c.Archetype == Archetype.Armoured);
            Assert.DoesNotContain(run.LastWaveCreatures, c => c.Archetype is null);
        }

        Assert.True(checkedAny, "no PLATED + Armoured wave was reached — the fixture proves nothing");
    }

    // ── SWIFT ─────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1000)]   // AttackBias.Fast
    [InlineData(1500)]   // the default
    [InlineData(2200)]   // AttackBias.Heavy
    public void test_swift_makes_the_creatures_bite_more_often_not_less(int baseInterval)
    {
        // THE REGRESSION TEST FOR THE BACKWARDS AFFIX. The bite fired on `ms % interval == 0` while the
        // loop stepped in TickMs, so the real gap was lcm(tick, interval) and SWIFT's x0.7 could push it
        // UP: 1500 -> 2100 instead of 1050, and 2200 -> 7700 instead of 1540. Only the 1000ms case
        // behaved, which is why it was never noticed — and why this is a Theory over all three biases.
        var swiftInterval = Math.Max(100, (int)(baseInterval * Bands.IntervalMultiplier(new[] { Affix.Swift })));

        var plain = BitesTaken(baseInterval);
        var swift = BitesTaken(swiftInterval);
        _out.WriteLine($"base {baseInterval}ms -> {plain} bites · SWIFT {swiftInterval}ms -> {swift} bites");

        Assert.True(swift > plain,
                    $"SWIFT shortened the interval {baseInterval} -> {swiftInterval} and the champion "
                    + $"took {swift} bites against {plain} — the affix is running backwards");
    }

    // ── ENDLESS ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_endless_affix_halves_what_leech_returns()
    {
        // Bands.SustainMultiplier computed 0.5 for this affix and had NO CALLER in the solution, so the
        // band that exists to pressure ENDURE did nothing to it at all.
        var leech = SkillShape.None with { Leech = 0.25f };
        var full = HealthAfterAWave(leech, sustain: 1f);
        var halved = HealthAfterAWave(leech, sustain: Bands.SustainMultiplier(new[] { Affix.Endless }));
        _out.WriteLine($"leech under a plain band {full} · under ENDLESS {halved}");

        Assert.True(halved < full,
                    $"ENDLESS must return less health than a plain band — got {halved} against {full}");
    }

    [Fact]
    public void test_a_plain_band_leaves_sustain_alone()
    {
        // The mirror. A multiplier applied unconditionally would be just as wrong as one applied never,
        // and would be just as invisible.
        var leech = SkillShape.None with { Leech = 0.25f };
        Assert.Equal(1f, Bands.SustainMultiplier(Array.Empty<Affix>()));
        Assert.Equal(HealthAfterAWave(leech, 1f), HealthAfterAWave(leech, Bands.SustainMultiplier(Array.Empty<Affix>())));
    }

    // ── THE BAR THE PLAYER WATCHES ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_waves_true_total_health_is_not_its_pre_composition_estimate()
    {
        // The fight screen sized its enemy bar with `baseHealth * EnemyScale(wave + 1)`, computed BEFORE
        // PushWave and therefore blind to everything PushWave decides: the archetype's health
        // multiplier, PLATED thickening armour, NUMBERS adding a creature. On an average wave the two
        // agree and nothing looks wrong; on exactly the waves that are NOT average — which is to say
        // the interesting ones — the bar was measuring against a number the simulation never used.
        //
        // This does not test the screen (there is no test project for the Game layer). It tests the
        // premise the screen was relying on, which is the part that was false.
        var run = Run("cinderworks", seed: 7);
        var disagreed = 0;

        for (var w = 1; w <= 30 && !run.Over; w++)
        {
            run.PushWave();
            var estimate = 1f * WaveScaling.EnemyScale(run.Wave, ExpeditionTuning.Default);
            var truth = run.LastWaveCreatures.Sum(c => c.MaxHealth);
            if (MathF.Abs(truth - estimate) > 0.001f) disagreed++;
        }

        _out.WriteLine($"{disagreed} of the waves walked had a real total the estimate did not predict");
        Assert.True(disagreed > 0,
                    "if the estimate always matched, the bar would have been fine and this fix pointless");
    }

    // ── fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static SoloExpedition Run(string region, int seed)
    {
        var build = BuildWith(SkillShape.None);
        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        return new SoloExpedition(build, champ, new Hunter(), 0.01f, 0f,
                                  ExpeditionTuning.Default, enemySource: null, rng: new Random(seed))
        { RegionId = region, RunIndex = 0 };
    }

    /// <summary>Total damage a build lands into one creature over a fixed window.</summary>
    private static float DamageInto(SkillShape shape, WaveCreature target)
    {
        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var (_, events) = SoloBattle.ResolveWave(
            champ, BuildWith(shape), new Hunter(), new[] { target },
            enemyIntervalMs: 100_000, ExpeditionTuning.Default, new Random(11));
        return events.Where(e => e.Kind == BattleEventKind.Strike).Sum(e => e.Amount);
    }

    /// <summary>How many times the champion was bitten inside one wave.</summary>
    private static int BitesTaken(int intervalMs)
    {
        // A creature that cannot be killed and a champion that cannot die, so the wave runs the full
        // ceiling and the only thing the count measures is the bite schedule.
        var champ = new Champion { MaxHealth = int.MaxValue, Health = int.MaxValue };
        var target = new WaveCreature { MaxHealth = 1e12f, Health = 1e12f, Damage = 1f };
        var (_, events) = SoloBattle.ResolveWave(
            champ, BuildWith(SkillShape.None), new Hunter(), new[] { target },
            intervalMs, ExpeditionTuning.Default, new Random(11));
        return events.Count(e => e.Kind == BattleEventKind.EnemyStrike);
    }

    /// <summary>Champion health at the end of one long wave, with leech doing the healing.</summary>
    private static int HealthAfterAWave(SkillShape shape, float sustain)
    {
        var champ = new Champion { MaxHealth = 200_000, Health = 100_000 };
        var target = new WaveCreature { MaxHealth = 1e12f, Health = 1e12f, Damage = 0f };
        SoloBattle.ResolveWave(
            champ, BuildWith(shape), new Hunter(), new[] { target },
            enemyIntervalMs: 100_000, ExpeditionTuning.Default, new Random(11),
            bonus: null, isBoss: false, metrics: null, sustain: sustain);
        return champ.Health;
    }
}
