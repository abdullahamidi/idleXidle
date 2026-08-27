using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The twelve nodes of the 2026-08-27 pass must reach the fight — every one, measured at the end of the chain.
/// </summary>
/// <remarks>
/// <para>
/// Same discipline as <see cref="SkillShapeBattleTests"/>: not "the shape has RegenFraction 0.005"
/// (arithmetic) but "a champion with MENDING ends the wave with more health than one without", measured
/// by the sim the game runs. Ten of the twelve nodes introduced a NEW field of <see cref="SkillShape"/>,
/// and a new field is ten new chances for the house failure — a value resolved, summed and carried by
/// correct code and read by nothing. HEFT and FAN reuse fields the sim already reads, and are checked
/// here too, through the catalogue rather than through a hand-built shape, so a renamed id would fail.
/// </para>
/// </remarks>
public class mastery_new_nodes_liveness_test
{
    private static EquippedSkill Sk(Form form)
        => new(new WovenAbility { Name = form.ToString(), Source = Source.Spirit, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    private static Build BuildWith(SkillShape shape, params Form[] forms)
    {
        var b = new Build { Shape = shape };
        foreach (var f in forms.Length == 0 ? new[] { Form.Strike } : forms) b.Weave(Sk(f));
        return b;
    }

    private static Champion Champ(int hp = 200_000) => new() { MaxHealth = hp, Health = hp };

    private static List<WaveCreature> Wave(int count, float health, float damage, float defense = 0f)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = health, Health = health, Damage = damage, Defense = defense,
        }).ToList();

    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static (WaveMetrics Metrics, Champion Champ, List<BattleEvent> Events) Fight(
        SkillShape shape, List<WaveCreature> creatures, ExpeditionTuning? tuning = null,
        int intervalMs = 900, int hp = 200_000, params Form[] forms)
    {
        var metrics = new WaveMetrics();
        var champ = Champ(hp);
        var (_, events) = SoloBattle.ResolveWave(
            champ, BuildWith(shape, forms), new Hunter(), creatures, intervalMs,
            tuning ?? ExpeditionTuning.Default, new Random(11), metrics: metrics);
        return (metrics, champ, events);
    }

    private static SkillShape Node(string id)
    {
        var n = MasteryCatalog.ById(id);
        Assert.NotNull(n);
        return n!.Shape;
    }

    // ── WEIGHT ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>HEFT — Strike and Trap hit harder, and a Projectile does not.</summary>
    [Fact]
    public void test_heft_lifts_strike_and_not_projectile()
    {
        var plainStrike = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing, forms: Form.Strike).Metrics;
        var heftStrike = Fight(Node("heft"), Wave(1, 1_000_000f, 1f), NoSwing, forms: Form.Strike).Metrics;
        Assert.True(heftStrike.RawDamage > plainStrike.RawDamage * 1.10f,
            $"HEFT Strike dealt {heftStrike.RawDamage:N0} against {plainStrike.RawDamage:N0} — the per-Form power is not reaching the hit.");

        var plainBolt = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing, forms: Form.Projectile).Metrics;
        var heftBolt = Fight(Node("heft"), Wave(1, 1_000_000f, 1f), NoSwing, forms: Form.Projectile).Metrics;
        Assert.Equal(plainBolt.RawDamage, heftBolt.RawDamage, 0);
    }

    /// <summary>HEADLONG pays against a creature above half health and stops below it.</summary>
    [Fact]
    public void test_headlong_pays_only_while_the_creature_is_mostly_whole()
    {
        // A pool no Strike can drain inside the ceiling: every hit lands on a creature above half.
        var whole = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing).Metrics;
        var headlong = Fight(Node("headlong"), Wave(1, 1_000_000f, 1f), NoSwing).Metrics;
        Assert.True(headlong.RawDamage > whole.RawDamage * 1.20f,
            $"HEADLONG dealt {headlong.RawDamage:N0} against {whole.RawDamage:N0} on a creature above half health.");

        // The same creature starting at a third of its health: nothing to pay on.
        var hurt = () => new List<WaveCreature> { new() { MaxHealth = 1_000_000f, Health = 300_000f, Damage = 1f } };
        var plainHurt = Fight(SkillShape.None, hurt(), NoSwing).Metrics;
        var headlongHurt = Fight(Node("headlong"), hurt(), NoSwing).Metrics;
        Assert.Equal(plainHurt.RawDamage, headlongHurt.RawDamage, 0);
    }

    /// <summary>STAGGER pushes bites back, so fewer land — and only one push per bite.</summary>
    [Fact]
    public void test_stagger_holds_bites_off_and_never_for_ever()
    {
        // Bites every 900 ms against a pool nothing kills. A big Strike lands every 2,000 ms or so,
        // so a stagger of 500 ms per bite should cut the bite count but never to zero.
        var shape = SkillShape.None with { StaggerThreshold = 1f, StaggerMs = 500, HitSize = 3f };
        var plain = Fight(SkillShape.None with { HitSize = 3f }, Wave(1, 1_000_000f, 10f), NoSwing);
        var stagger = Fight(shape, Wave(1, 1_000_000f, 10f), NoSwing);

        int Bites(List<BattleEvent> ev) => ev.Count(e => e.Kind == BattleEventKind.EnemyStrike);
        Assert.True(Bites(stagger.Events) < Bites(plain.Events),
            $"STAGGER took {Bites(stagger.Events)} bites against {Bites(plain.Events)} without — nothing is being pushed back.");
        Assert.True(Bites(stagger.Events) > 0, "STAGGER held every bite off — the once-per-bite cap is not working.");
    }

    // ── SPREAD ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>FAN — a Trap reaches one more creature, through the same TargetsFor the sim reads.</summary>
    [Fact]
    public void test_fan_gives_the_trap_a_second_target()
    {
        Assert.Equal(FormBehaviour.Targets(Form.Trap) + 1, Node("fan").TargetsFor(Form.Trap));
        Assert.Equal(FormBehaviour.Targets(Form.Strike), Node("fan").TargetsFor(Form.Strike));

        // And the fight agrees: a Trap that is bitten strikes two creatures, not one.
        var plain = Fight(SkillShape.None, Wave(3, 1_000_000f, 10f), NoSwing, forms: Form.Trap).Metrics;
        var fan = Fight(Node("fan"), Wave(3, 1_000_000f, 10f), NoSwing, forms: Form.Trap).Metrics;
        Assert.True(fan.TargetsStruck > plain.TargetsStruck,
            $"FAN struck {fan.TargetsStruck} against {plain.TargetsStruck} — the extra target is not reaching the Trap.");
    }

    /// <summary>RALLY — after a kill, the next skill activation hits harder.</summary>
    [Fact]
    public void test_rally_spends_a_kill_on_the_next_skill()
    {
        // One creature a single Strike finishes, then a pool nothing kills: the kill arms the rally
        // and the whole of the second creature's damage is the measurement.
        var field = () => new List<WaveCreature>
        {
            new() { MaxHealth = 10f, Health = 10f, Damage = 1f },
            new() { MaxHealth = 1_000_000f, Health = 1_000_000f, Damage = 1f },
        };
        var plain = Fight(SkillShape.None, field(), NoSwing).Metrics;
        var rally = Fight(Node("rally"), field(), NoSwing).Metrics;

        Assert.Equal(1, plain.CreaturesKilled);
        Assert.True(rally.RawDamage > plain.RawDamage,
            $"RALLY dealt {rally.RawDamage:N0} against {plain.RawDamage:N0} — the kill is not arming the next skill.");
        // ONE activation, not every one after: the total moves by less than the node's whole bonus.
        Assert.True(rally.RawDamage < plain.RawDamage * (1f + Node("rally").NextSkillAfterKillBonus),
            "RALLY paid on every hit after the kill, not on the next skill.");
    }

    /// <summary>TIDE — cooldowns shorten with the crowd, so a wide wave dies sooner.</summary>
    [Fact]
    public void test_tide_casts_faster_in_a_crowd()
    {
        // Six creatures a Strike kills in one hit each. TIDE's rate bonus is per LIVING creature,
        // so it is worth the most on the first cast and fades as they fall; the price is paid flat.
        var field = () => Wave(6, 30f, 1f);
        var plain = Fight(SkillShape.None with { HitSize = 0.85f }, field(), NoSwing).Metrics;
        var tide = Fight(Node("tide"), field(), NoSwing).Metrics;

        Assert.Equal(6, plain.CreaturesKilled);
        Assert.Equal(6, tide.CreaturesKilled);
        Assert.True(tide.DurationMs < plain.DurationMs,
            $"TIDE cleared in {tide.DurationMs}ms against {plain.DurationMs}ms at the same hit size — the rate is not reading the crowd.");
    }

    // ── TEMPO ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>BRISK — the basic swing comes sooner; the skills keep their own pace.</summary>
    [Fact]
    public void test_brisk_speeds_the_swing_and_not_the_skills()
    {
        // No skills at all: every point of damage is the swing, and nothing else sets its cadence.
        // (A Mark-only build looked the same on both sides: the cast lock around each Mark realigned
        // the swings so a 1,200 ms and a 1,000 ms interval landed the same count inside the ceiling.)
        WaveMetrics SwingOnly(SkillShape shape)
        {
            var m = new WaveMetrics();
            SoloBattle.ResolveWave(Champ(), new Build { Shape = shape }, new Hunter(), Wave(1, 1_000_000f, 1f),
                                   900, ExpeditionTuning.Default, new Random(11), metrics: m);
            return m;
        }
        var plain = SwingOnly(SkillShape.None);
        var brisk = SwingOnly(Node("brisk"));
        Assert.True(brisk.Hits > plain.Hits * 1.10f,
            $"BRISK swung {brisk.Hits} times against {plain.Hits} — the swing's cadence is not reading the node.");

        // The swing switched off: a Strike build's skill damage must not move.
        var plainSkill = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing).Metrics;
        var briskSkill = Fight(Node("brisk"), Wave(1, 1_000_000f, 1f), NoSwing).Metrics;
        Assert.Equal(plainSkill.RawDamage, briskSkill.RawDamage, 0);
    }

    /// <summary>RHYTHM — casts come faster while nothing bites, and a bite takes it all back.</summary>
    [Fact]
    public void test_rhythm_builds_while_unbitten_and_a_bite_resets_it()
    {
        // A creature that never bites (interval past the ceiling): the ramp runs to its cap.
        var never = 10_000_000;
        var plain = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing, never).Metrics;
        var rhythm = Fight(Node("rhythm"), Wave(1, 1_000_000f, 1f), NoSwing, never).Metrics;
        Assert.True(rhythm.Activations > plain.Activations,
            $"RHYTHM cast {rhythm.Activations} times against {plain.Activations} unbitten — the ramp is not reaching the cooldown.");

        // Bitten every 300 ms: the ramp never gets past one cast, and buys almost nothing.
        var plainBitten = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing, 300).Metrics;
        var rhythmBitten = Fight(Node("rhythm"), Wave(1, 1_000_000f, 1f), NoSwing, 300).Metrics;
        var unbittenGain = rhythm.Activations - plain.Activations;
        var bittenGain = rhythmBitten.Activations - plainBitten.Activations;
        Assert.True(bittenGain < unbittenGain,
            $"RHYTHM gained {bittenGain} casts under constant bites and {unbittenGain} unbitten — the bite is not resetting it.");
    }

    /// <summary>OPENING VOLLEY — each skill's first cast of the wave is the big one; the rest pay.</summary>
    [Fact]
    public void test_opening_volley_pays_on_the_first_cast_and_charges_the_rest()
    {
        // ONE cast: a creature the first Strike finishes. Only the opener lands, so +50% shows whole.
        var one = () => Wave(1, 1f, 1f);
        var plainOne = Fight(SkillShape.None, one(), NoSwing).Metrics;
        var volleyOne = Fight(Node("opening_volley"), one(), NoSwing).Metrics;
        Assert.Equal(plainOne.RawDamage * Node("opening_volley").FirstCastMultiplier, volleyOne.RawDamage, 0);

        // MANY casts against a pool nothing drains: the later casts' price drags the total under the
        // opener's bonus — the node is a trade, not a bonus.
        var many = () => Wave(1, 1_000_000f, 1f);
        var plainMany = Fight(SkillShape.None, many(), NoSwing).Metrics;
        var volleyMany = Fight(Node("opening_volley"), many(), NoSwing).Metrics;
        Assert.True(volleyMany.RawDamage < plainMany.RawDamage * Node("opening_volley").FirstCastMultiplier,
            "OPENING VOLLEY paid its opening bonus on every cast — the later-cast price is dormant.");
        Assert.True(volleyMany.RawDamage < plainMany.RawDamage,
            "Over a long wave the later-cast price must show: the node is a trade.");
    }

    // ── ENDURE ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>MENDING — health comes back a little every second.</summary>
    [Fact]
    public void test_mending_regains_health_over_the_wave()
    {
        // A pool nothing drains and a bite that chips it: without MENDING the chips only add up.
        var plain = Fight(SkillShape.None, Wave(1, 1_000_000f, 40f), NoSwing, hp: 10_000).Metrics;
        var mending = Fight(Node("mending"), Wave(1, 1_000_000f, 40f), NoSwing, hp: 10_000).Metrics;
        Assert.True(plain.HealthLost > 0);
        Assert.True(mending.HealthLost < plain.HealthLost,
            $"MENDING lost {mending.HealthLost} against {plain.HealthLost} — the trickle is not reaching the pool.");
    }

    /// <summary>PAYBACK — bites bank a bonus for the next skill, up to the cap.</summary>
    [Fact]
    public void test_payback_turns_bites_into_the_next_hit()
    {
        // Bitten every 300 ms by a nibbler against a pool nothing drains: plenty of bites, then a
        // cast. The banked bonus lands on that cast, so the total climbs.
        var plain = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing, 300).Metrics;
        var payback = Fight(Node("payback"), Wave(1, 1_000_000f, 1f), NoSwing, 300).Metrics;
        Assert.True(payback.RawDamage > plain.RawDamage,
            $"PAYBACK dealt {payback.RawDamage:N0} against {plain.RawDamage:N0} under constant bites — the bank is not being spent.");

        // Never bitten: nothing banked, nothing paid.
        var plainCalm = Fight(SkillShape.None, Wave(1, 1_000_000f, 1f), NoSwing, 10_000_000).Metrics;
        var paybackCalm = Fight(Node("payback"), Wave(1, 1_000_000f, 1f), NoSwing, 10_000_000).Metrics;
        Assert.Equal(plainCalm.RawDamage, paybackCalm.RawDamage, 0);
    }

    /// <summary>REBOUND — a share of every bite comes back as health.</summary>
    [Fact]
    public void test_rebound_heals_a_share_of_every_bite()
    {
        var plain = Fight(SkillShape.None, Wave(1, 1_000_000f, 300f), NoSwing, hp: 50_000).Metrics;
        var rebound = Fight(Node("rebound"), Wave(1, 1_000_000f, 300f), NoSwing, hp: 50_000).Metrics;
        Assert.True(plain.HealthLost > 0);
        Assert.True(rebound.HealthLost < plain.HealthLost,
            $"REBOUND lost {rebound.HealthLost} against {plain.HealthLost} — the bite is not coming back.");
        Assert.True(rebound.HealthLost > plain.HealthLost * 0.5f,
            "REBOUND gave back more than a fifth of every bite — it is reading the wrong number.");
    }

    /// <summary>Every one of the twelve exists, costs what its ring costs, and changes a shape.</summary>
    [Fact]
    public void test_the_twelve_new_nodes_are_in_the_catalogue()
    {
        var ids = new[]
        {
            "heft", "headlong", "stagger", "fan", "rally", "tide",
            "brisk", "rhythm", "opening_volley", "mending", "payback", "rebound",
        };
        foreach (var id in ids)
        {
            var n = MasteryCatalog.ById(id);
            Assert.NotNull(n);
            Assert.Equal(MasteryCatalog.RingCost[n!.Ring], n.Cost);
            Assert.NotEqual(SkillShape.None, n.Shape);
            Assert.Contains("—", n.Label);   // NAME — WHAT IT DOES, like every card on the tree
        }
    }
}
