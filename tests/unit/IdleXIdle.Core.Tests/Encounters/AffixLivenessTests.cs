using System;
using System.Collections.Generic;
using System.Linq;
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
        foreach (var id in new[] { "hammer_blow", "volley_spray", "field_mire", "sign_call" })
            b.Equip(TestBuilds.Skill(id, Source.Nature));
        return b;
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
    public void test_the_endless_affix_halves_what_healing_returns()
    {
        // Bands.SustainMultiplier computed 0.5 for this affix and had NO CALLER in the solution, so the
        // band that exists to pressure ENDURE did nothing to it at all.
        var heal = SkillShape.None with { HealPerTargetStruck = 0.002f };
        var full = HealthAfterAWave(heal, sustain: 1f);
        var halved = HealthAfterAWave(heal, sustain: Bands.SustainMultiplier(new[] { Affix.Endless }));
        _out.WriteLine($"heal under a plain band {full} · under ENDLESS {halved}");

        Assert.True(halved < full,
                    $"ENDLESS must return less health than a plain band — got {halved} against {full}");
    }

    [Fact]
    public void test_a_plain_band_leaves_sustain_alone()
    {
        // The mirror. A multiplier applied unconditionally would be just as wrong as one applied never,
        // and would be just as invisible.
        var heal = SkillShape.None with { HealPerTargetStruck = 0.002f };
        Assert.Equal(1f, Bands.SustainMultiplier(Array.Empty<Affix>()));
        Assert.Equal(HealthAfterAWave(heal, 1f), HealthAfterAWave(heal, Bands.SustainMultiplier(Array.Empty<Affix>())));
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
                                  ExpeditionTuning.Default, rng: new Random(seed))
        { RegionId = region, RunIndex = 0 };
    }

    /// <summary>Total damage a build lands into one creature over a fixed window.</summary>

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

    /// <summary>Champion health at the end of one long wave, with heal doing the healing.</summary>
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

    // ── ENTRENCHED ──

    [Fact]
    public void test_entrenched_makes_the_first_hit_on_a_creature_land_smaller()
    {
        // Direction, not magnitude: the same build's FIRST strike into a fresh creature lands smaller
        // under ENTRENCHED; what happens after the opener is the fight's own business.
        var plain = FirstStrikeAmount(entrenched: false);
        var dugIn = FirstStrikeAmount(entrenched: true);
        _out.WriteLine($"first hit plain {plain} · entrenched {dugIn}");

        Assert.True(dugIn < plain,
            $"ENTRENCHED must shrink the opener — got {dugIn} against {plain}");
    }

    private static int FirstStrikeAmount(bool entrenched)
    {
        var champ = new Champion { MaxHealth = int.MaxValue, Health = int.MaxValue };
        var target = new WaveCreature { MaxHealth = 1e12f, Health = 1e12f, Damage = 0f };
        var (_, events) = SoloBattle.ResolveWave(
            champ, BuildWith(SkillShape.None), new Hunter(), new List<WaveCreature> { target },
            enemyIntervalMs: 100_000, ExpeditionTuning.Default, new Random(11),
            bonus: null, isBoss: false, metrics: null, sustain: 1f, entrenched: entrenched);
        return events.First(e => e.Kind == BattleEventKind.Strike).Amount;
    }

    // ── WARDED ──

    [Fact]
    public void test_warded_shrinks_the_warded_styles_damage_and_spares_the_rest()
    {
        // hammer_blow's style is HAMMER; ward it and less lands. Ward a style the build does not
        // carry and nothing changes — the affix reads the skill, never the swing.
        var free = DeliveredBy(warded: null);
        var warded = DeliveredBy(warded: Style.Hammer);
        _out.WriteLine($"delivered free {free:0} · HAMMER warded {warded:0}");

        Assert.True(warded < free, "warding the build's own style must shrink what lands");
        Assert.Equal(free, DeliveredBy(warded: Style.Drain), precision: 1);
    }

    private static float DeliveredBy(Style? warded)
    {
        var champ = new Champion { MaxHealth = int.MaxValue, Health = int.MaxValue };
        var target = new WaveCreature { MaxHealth = 1e12f, Health = 1e12f, Damage = 0f };
        var metrics = new WaveMetrics();
        SoloBattle.ResolveWave(
            champ, BuildWith(SkillShape.None), new Hunter(), new List<WaveCreature> { target },
            enemyIntervalMs: 100_000, ExpeditionTuning.Default, new Random(11),
            bonus: null, isBoss: false, metrics: metrics, sustain: 1f, wardedStyle: warded);
        return metrics.DeliveredDamage;
    }

    [Fact]
    public void test_warded_reads_the_previous_wave_through_the_expedition()
    {
        // The expedition keeps the ledger: before any wave there is no history, so WARDED can never
        // guess on a run's first wave; after a wave in which skills fired, the top style is known.
        var run = Run("verdant_hollow", seed: 3);
        Assert.Null(run.LastWaveTopStyle);

        run.PushWave();
        Assert.NotNull(run.LastWaveTopStyle);
    }

    // ── LEGION ──

    [Fact]
    public void test_legion_splits_a_dying_creature_into_two_and_respects_the_cap()
    {
        // A killable pair under LEGION: the wave ends with more corpses than it started with bodies,
        // because each ORIGINAL split once — children do not re-split, and the roster never passes
        // the cap. The children land in the caller's own list, which is what the screen composes from.
        var champ = new Champion { MaxHealth = int.MaxValue, Health = int.MaxValue };
        var roster = new List<WaveCreature>
        {
            new() { MaxHealth = 40f, Health = 40f, Damage = 0f },
            new() { MaxHealth = 40f, Health = 40f, Damage = 0f },
        };
        var (outcome, events) = SoloBattle.ResolveWave(
            champ, BuildWith(SkillShape.None), new Hunter(), roster,
            enemyIntervalMs: 100_000, ExpeditionTuning.Default, new Random(11),
            bonus: null, isBoss: false, metrics: null, sustain: 1f, legionSplits: true);

        Assert.Equal(WaveOutcome.Cleared, outcome);
        Assert.True(roster.Count > 2, "the dying originals must have split children into the list");
        Assert.True(roster.Count <= SoloBattle.LegionMaxCreatures);
        Assert.Equal(roster.Count, events.Count(e => e.Kind == BattleEventKind.EnemyDown));
        Assert.All(roster, c => Assert.False(c.Alive));
    }
}
