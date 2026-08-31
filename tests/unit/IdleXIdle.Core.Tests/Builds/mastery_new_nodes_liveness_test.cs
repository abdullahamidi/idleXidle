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

namespace IdleXIdle.Core.Tests.Builds;

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
    private static Build BuildWith(SkillShape shape, params string[] skillIds)
    {
        var b = new Build { Shape = shape };
        foreach (var id in skillIds.Length == 0 ? new[] { "hammer_blow" } : skillIds)
            b.Weave(TestBuilds.Skill(id, Source.Spirit));
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
        int intervalMs = 900, int hp = 200_000, params string[] skillIds)
    {
        var metrics = new WaveMetrics();
        var champ = Champ(hp);
        var (_, events) = SoloBattle.ResolveWave(
            champ, BuildWith(shape, skillIds), new Hunter(), creatures, intervalMs,
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

    /// <summary>STAGGER pushes bites back, so fewer land — and only one push per bite.</summary>
    [Fact]
    public void test_stagger_holds_bites_off_and_never_for_ever()
    {
        // Bites every 900 ms against a pool nothing kills. HAMMER BLOW casts every 9,000 ms or so
        // (6 beats), so a stagger of 500 ms per bite should cut the bite count but never to zero.
        var shape = SkillShape.None with { StaggerThreshold = 1f, StaggerMs = 500, HitSize = 3f };
        var plain = Fight(SkillShape.None with { HitSize = 3f }, Wave(1, 1_000_000f, 10f), NoSwing);
        var stagger = Fight(shape, Wave(1, 1_000_000f, 10f), NoSwing);

        int Bites(List<BattleEvent> ev) => ev.Count(e => e.Kind == BattleEventKind.EnemyStrike);
        Assert.True(Bites(stagger.Events) < Bites(plain.Events),
            $"STAGGER took {Bites(stagger.Events)} bites against {Bites(plain.Events)} without — nothing is being pushed back.");
        Assert.True(Bites(stagger.Events) > 0, "STAGGER held every bite off — the once-per-bite cap is not working.");
    }

    // ── TEMPO ─────────────────────────────────────────────────────────────────────────────────────

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

    /// <summary>Every survivor of the twelve exists, costs what its ring costs, and changes a shape.</summary>
    /// <remarks>
    /// FIVE, not twelve. HEFT, HEADLONG and STAGGER were WEIGHT's and FAN, RALLY and TIDE were
    /// SPREAD's; the 2026-08-30 re-axe replaced both branches, with RESONANCE and LOOT. MENDING went
    /// in the same pass when ring 1 stopped being rules — it and RECOVERY were both regeneration, and
    /// RECOVERY is the only feeder of BetweenWaveRegen while MENDING's RegenFraction still reaches the
    /// sim from the element sets. BRISK survives and is a NOTABLE now. The nodes
    /// that replaced them are pinned by <c>MasteryNodeLivenessTests</c>, which asks a harder question of
    /// each — not "is it in the catalogue" but "does the fight come out different".
    /// </remarks>
    [Fact]
    public void test_the_surviving_new_nodes_are_in_the_catalogue()
    {
        var ids = new[]
        {
            "brisk", "rhythm", "opening_volley", "payback", "rebound",
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
