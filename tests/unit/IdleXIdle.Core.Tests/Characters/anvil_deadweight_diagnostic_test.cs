using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Tests.Builds;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Characters;

/// <summary>
/// What is THE ANVIL's DEADWEIGHT worth? The redesigned mechanic, measured directly, on builds a player can weave.
/// </summary>
/// <remarks>
/// <para>
/// The first version of this file (2026-09-07) measured DEADWEIGHT as the overkill carry it then was
/// and found it live, small and zero in a boss room — and shadowed outright by any other carry rule,
/// since all of them wrote one field combined by MAX. The passive is its own rule now: a primary direct
/// hit (a skill's or the swing) that leaves its target standing leaves a share of its damage in it, and the next such hit lands
/// it too (<see cref="SkillShape.DeadweightShare"/>, <see cref="WaveCreature.StoredDeadweight"/>).
/// This file measures THAT — stores, releases, the released damage, its share of everything dealt —
/// on the parity gauntlet, beside every carry rule it must live with, and against the lone durable
/// enemy it was redesigned for.
/// </para>
/// <para>
/// <b>Reachable only.</b> Every row composes through <see cref="Reachable.Compose"/> — the shipped
/// loadout, composer and skill progress — so no impossible variation, foreign signature or
/// fixture-only state can reach a row; gear rows wear real items of no class, which anyone may wear.
/// The control is THE ANVIL with the passive removed and nothing else changed, so the delta between
/// the two IS DEADWEIGHT. The rows say whether a relaunch's signature repair leaves them alone (a
/// HARDFACE-free loadout persists only when FULL) and the reachability test runs that repair.
/// </para>
/// <para>
/// <b>Seed-invariant, asserted.</b> On bare gear the fight draws no random number that moves this
/// gauntlet, so the forty seeds are forty identical runs and the "2% floor" is one beat of the cast
/// cadence, not sampling noise. <b>Diagnostic, not balance:</b> the numbers are printed, the
/// assertions hold the counters to their own arithmetic and the mechanic to being LIVE where its
/// rule says it must be. No threshold here says what the passive should be worth.
/// </para>
/// </remarks>
public class AnvilDeadweightDiagnosticTest
{
    private readonly ITestOutputHelper _out;

    public AnvilDeadweightDiagnosticTest(ITestOutputHelper output) => _out = output;

    private static readonly Character Anvil = CharacterRoster.Get("anvil");

    /// <summary>THE ANVIL with DEADWEIGHT taken off: same id (so HARDFACE is weavable), same class, no shape.</summary>
    private static readonly Character Control = Anvil with
    {
        Name = "NO PASSIVE",
        PassiveName = "NONE",
        PassiveText = "THE ANVIL with DEADWEIGHT removed. The control.",
        Shape = SkillShape.None,
    };

    private const string Hardface = "sig_anvil_hardface";
    private const string PureVow = "vow_pure";

    /// <summary>
    /// One matrix row. <paramref name="Capacity"/> is the account's slot count (null = as many as woven,
    /// a FULL loadout); <paramref name="Persists"/> says whether the composed loadout survives a relaunch
    /// unchanged; <paramref name="BodyPieces"/> dresses both arms in that many BODY pieces (four = the
    /// set's carry, 0.35); <paramref name="BossOnly"/> fights the gauntlet's boss wave alone — the lone
    /// durable enemy the passive was redesigned for.
    /// </summary>
    private sealed record Row(string Label, Reachable.Slot[] Slots, int? Capacity = 4, bool Vow = false,
                              bool Persists = true, int BodyPieces = 0, bool BossOnly = false);

    private static Reachable.Slot Blow(params string[] reinforcements) => new("hammer_blow", "FLATTEN", Reinforcements: reinforcements.Length == 0 ? null : reinforcements);

    /// <summary>The matrix: the parity pairs, the passive beside every carry rule, and the lone boss.</summary>
    private static readonly Row[] Matrix =
    {
        new("HARDFACE unchosen — a fresh Anvil", new[] { new Reachable.Slot(Hardface) }, Capacity: 1),
        new("UPSET alone", new[] { new Reachable.Slot(Hardface, "UPSET") }, Capacity: 1),
        new("PLANISH alone", new[] { new Reachable.Slot(Hardface, "PLANISH") }, Capacity: 1),
        new("UPSET + BLOW FLATTEN", new[] { new Reachable.Slot(Hardface, "UPSET"), Blow() }),
        new("PLANISH + DRINK SIPHON", new[] { new Reachable.Slot(Hardface, "PLANISH"), new Reachable.Slot("drain_drink", "SIPHON") }),
        new("UPSET + BLOW FLATTEN, VOW OF THE PURE kept (the parity row)",
            new[] { new Reachable.Slot(Hardface, "UPSET", PureVow), Blow() }, Vow: true),
        new("PLANISH + DRINK SIPHON, VOW OF THE PURE kept (the parity row)",
            new[] { new Reachable.Slot(Hardface, "PLANISH", PureVow), new Reachable.Slot("drain_drink", "SIPHON") }, Vow: true),
        new("BLOW FLATTEN — no HARDFACE (full one-slot loadout)", new[] { Blow() }, Capacity: null),
        // Beside every carry rule: the set's, BREAKTHROUGH's, CLEAN CUT's.
        new("UPSET + BLOW FLATTEN — four BODY pieces worn (the set's carry, 0.35)", new[] { new Reachable.Slot(Hardface, "UPSET"), Blow() }, BodyPieces: 4),
        new("UPSET + BLOW FLATTEN with BREAKTHROUGH (the blow's own carry, 0.5)", new[] { new Reachable.Slot(Hardface, "UPSET"), Blow("BREAKTHROUGH") }),
        new("UPSET + BLOW FINISH with CLEAN CUT (the execute's carry, 1.0)",
            new[] { new Reachable.Slot(Hardface, "UPSET"), new Reachable.Slot("hammer_blow", "FINISH", Reinforcements: new[] { "CLEAN CUT" }) }),
        // The lone durable enemy — the case the redesign is for.
        new("HARDFACE unchosen — the boss alone", new[] { new Reachable.Slot(Hardface) }, Capacity: 1, BossOnly: true),
        new("PLANISH + DRINK SIPHON — the boss alone", new[] { new Reachable.Slot(Hardface, "PLANISH"), new Reachable.Slot("drain_drink", "SIPHON") }, BossOnly: true),
    };

    /// <summary>One character's totals over every seed and wave of the row's gauntlet, plus per-wave detail.</summary>
    private sealed class Tally
    {
        public double ClearMs, HpLost, Delivered, Carried, DeadweightDamage;
        public long Carries, Stores, Releases, Killed;
        public readonly double[] WaveMs = new double[3], WaveDelivered = new double[3], WaveDeadweight = new double[3];
        public readonly long[] WaveStores = new long[3], WaveReleases = new long[3];

        /// <summary>The passive's direct contribution — released damage as a share of everything landed.</summary>
        public double DeadweightShare => Delivered <= 0 ? 0 : DeadweightDamage / Delivered;
    }

    private static Hunter Dressed(int bodyPieces)
    {
        var hunter = new Hunter();
        var types = new[] { ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring };
        for (var i = 0; i < bodyPieces; i++)
            hunter.Equip(new ItemInstance { InstanceId = $"body{i}", BaseType = types[i], Rarity = Rarity.Common, SellValue = 1, Element = Source.Body, ItemLevel = 1 });
        return hunter;
    }

    private static (string Name, Func<List<WaveCreature>> Make)[] WavesOf(Row row)
        => row.BossOnly ? new[] { ParityGauntlet.Waves[2] } : ParityGauntlet.Waves;

    private static Build Compose(Character c, Row row)
    {
        var build = Reachable.Compose(c, row.Slots, slotCapacity: row.Capacity, knownVows: Vows.Catalog, vowCapacity: 1);
        if (row.Vow)
        {
            var vow = Vows.Catalog.First(v => v.Id == PureVow);
            Assert.True(Vows.IsActive(vow, SoloBattle.DescribeBuild(build, new Hunter())),
                        $"{row.Label}: the pair does not keep VOW OF THE PURE — the fixture, not the design.");
        }
        return build;
    }

    private static Tally Measure(Character c, Row row, int seeds = ParityGauntlet.Seeds)
    {
        var build = Compose(c, row);
        var waves = WavesOf(row);
        var t = new Tally();
        double firstDeadweight = double.NaN, firstClear = double.NaN;
        for (var s = 0; s < seeds; s++)
        {
            var champ = ParityGauntlet.FreshChampion();
            var hunter = Dressed(row.BodyPieces);
            var rng = new Random(ParityGauntlet.Seed(s));
            double seedDeadweight = 0, seedClear = 0;
            for (var w = 0; w < waves.Length; w++)
            {
                var metrics = new WaveMetrics();
                var (outcome, _) = SoloBattle.ResolveWave(champ, build, hunter, waves[w].Make(),
                                                          ParityGauntlet.EnemyIntervalMs, ParityGauntlet.Tuning, rng,
                                                          metrics: metrics);
                Assert.True(outcome == WaveOutcome.Cleared, $"{row.Label} / {waves[w].Name}: stalled — the clock saturates");
                t.ClearMs += metrics.DurationMs;
                t.Delivered += metrics.DeliveredDamage;
                t.Carried += metrics.CarriedDamage;
                t.Carries += metrics.CarriesLanded;
                t.Stores += metrics.DeadweightStores;
                t.Releases += metrics.DeadweightReleases;
                t.DeadweightDamage += metrics.DeadweightDamage;
                t.Killed += metrics.CreaturesKilled;
                t.WaveMs[w] += metrics.DurationMs;
                t.WaveDelivered[w] += metrics.DeliveredDamage;
                t.WaveDeadweight[w] += metrics.DeadweightDamage;
                t.WaveStores[w] += metrics.DeadweightStores;
                t.WaveReleases[w] += metrics.DeadweightReleases;
                seedDeadweight += metrics.DeadweightDamage;
                seedClear += metrics.DurationMs;
            }
            t.HpLost += ParityGauntlet.ChampionHealth - champ.Health;

            if (double.IsNaN(firstDeadweight)) { firstDeadweight = seedDeadweight; firstClear = seedClear; }
            Assert.True(seedDeadweight == firstDeadweight && seedClear == firstClear,
                        $"{row.Label}: seed {s} differs from seed 0 — the gauntlet is no longer seed-invariant on bare gear; read the spread before reading the table.");
        }
        return t;
    }

    [Fact]
    public void test_the_deadweight_matrix_is_measured_directly()
    {
        var share = Anvil.Shape.DeadweightShare;
        Assert.True(share > 0f, "THE ANVIL's passive is no longer DEADWEIGHT — this diagnostic measures the wrong thing");
        Assert.Equal(0f, Anvil.Shape.OverkillCarry);
        var seeds = ParityGauntlet.Seeds;

        _out.WriteLine("DEADWEIGHT DIAGNOSTIC — THE ANVIL against THE ANVIL-WITHOUT-DEADWEIGHT on the parity gauntlet");
        _out.WriteLine($"(swarm 10x320 · pack 4x1,600/45 def · boss 14,000/70 def; {seeds} seeds, all identical; bare gear unless the row dresses; share {share:0.00})");
        _out.WriteLine("");
        _out.WriteLine($"{"ROW",-74} {"FASTER",7} {"HP Δ",6} {"STORES",7} {"RELEASE",7} {"DW DMG",8} {"OF DEALT",8} {"CARRIES a/c",12} {"CARRIED a/c",14}");
        _out.WriteLine(new string('-', 154));

        var results = new List<(Row Row, Tally Anvil, Tally Control)>();
        foreach (var row in Matrix)
        {
            var a = Measure(Anvil, row);
            var c = Measure(Control, row);
            results.Add((row, a, c));

            var faster = a.ClearMs <= 0 ? 0 : 100.0 * (c.ClearMs / a.ClearMs - 1.0);
            var hpDelta = c.HpLost <= 0 ? 0 : 100.0 * (a.HpLost / c.HpLost - 1.0);
            _out.WriteLine($"{row.Label,-74} {faster,6:+0.0;-0.0;0.0}% {hpDelta,5:+0;-0;0}% " +
                           $"{a.Stores / (double)seeds,7:0.0} {a.Releases / (double)seeds,7:0.0} {a.DeadweightDamage / seeds,8:N0} {a.DeadweightShare,7:0.0%} " +
                           $"{a.Carries / (double)seeds,5:0.0}/{c.Carries / (double)seeds,-6:0.0} {a.Carried / seeds,6:N0}/{c.Carried / seeds,-7:N0}");
        }

        _out.WriteLine("");
        _out.WriteLine("(STORES / RELEASE: hits (skill or swing) per run that left DEADWEIGHT in a standing enemy / that landed it. DW DMG: the");
        _out.WriteLine(" released damage per run. OF DEALT: that as a share of everything the Anvil landed. CARRIES / CARRIED: the");
        _out.WriteLine(" overkill carry the build wears, both arms — unchanged by the passive, which is no longer a carry.)");
        _out.WriteLine("");
        _out.WriteLine("PER WAVE — where the weight lives. (Cooldowns persist across waves, so a wave's clear time carries the");
        _out.WriteLine(" previous wave's phase; the counters do not.)");
        foreach (var (row, a, c) in results)
        {
            _out.WriteLine($"  {row.Label}");
            var waves = WavesOf(row);
            for (var w = 0; w < waves.Length; w++)
            {
                var name = waves[w].Name;
                var fasterW = a.WaveMs[w] <= 0 ? 0 : 100.0 * (c.WaveMs[w] / a.WaveMs[w] - 1.0);
                var shareW = a.WaveDelivered[w] <= 0 ? 0 : a.WaveDeadweight[w] / a.WaveDelivered[w];
                _out.WriteLine($"    {name,-6} clear {a.WaveMs[w] / seeds,8:N0} ms ({fasterW,5:+0.0;-0.0;0.0}%)  stores {a.WaveStores[w] / (double)seeds,5:0.0}  " +
                               $"releases {a.WaveReleases[w] / (double)seeds,5:0.0}  released {a.WaveDeadweight[w] / seeds,7:N0} ({shareW,5:0.0%} of dealt)");
            }
        }

        // ── STRUCTURAL — the counter site's shape. ──
        foreach (var (row, a, c) in results)
        {
            Assert.True(c.Stores == 0 && c.Releases == 0 && c.DeadweightDamage == 0, $"{row.Label}: the control stored or released — it wears no share");
            Assert.True(a.Releases <= a.Stores, $"{row.Label}: more releases than stores");
            Assert.True(a.DeadweightDamage <= a.Delivered * share * 1.001 + 1.0,
                        $"{row.Label}: released {a.DeadweightDamage:N0} — more than the share of everything dealt");
            // The carry is nobody's passive now: the RULE is the same on both arms (the Anvil's shape
            // writes no carry), so a row that wears no carry rule carries on neither arm. Where one is
            // worn the two arms' carried totals may differ — the releases change which hit kills and
            // how much spills — which is the passive changing the fight, not writing the field.
            if (row.BodyPieces == 0 && !row.Slots.Any(s => s.Reinforcements is { Count: > 0 }))
                Assert.True(a.Carries == 0 && c.Carries == 0, $"{row.Label}: a carry landed on a build that wears no carry rule");
        }

        // ── THE MECHANIC IS LIVE where its rule says it must be. ──
        foreach (var (row, a, _) in results.Where(r => r.Row.BossOnly))
            Assert.True(a.Releases > 0 && a.DeadweightDamage > 0,
                        $"{row.Label}: a lone boss took no DEADWEIGHT — the passive is not reaching the fight it was redesigned for");
        foreach (var (row, a, c) in results.Where(r => r.Row.BodyPieces > 0 || r.Row.Slots.Any(s => s.Reinforcements is { Count: > 0 })))
        {
            Assert.True(a.Releases > 0, $"{row.Label}: DEADWEIGHT vanished beside a carry rule");
            Assert.True(c.Carries > 0, $"{row.Label}: the carry rule the row wears never carried — the fixture is not posing coexistence");
        }
    }

    [Fact]
    public void test_the_deadweight_counters_are_deterministic()
    {
        var row = Matrix.Single(r => r.Label == "UPSET + BLOW FLATTEN");
        var once = Measure(Anvil, row, seeds: 3);
        var twice = Measure(Anvil, row, seeds: 3);
        Assert.Equal(once.DeadweightDamage, twice.DeadweightDamage);
        Assert.Equal(once.Stores, twice.Stores);
        Assert.Equal(once.Releases, twice.Releases);
        Assert.Equal(once.ClearMs, twice.ClearMs);
    }

    [Fact]
    public void test_every_matrix_row_is_a_build_a_player_can_weave()
    {
        foreach (var row in Matrix)
        {
            var a = Compose(Anvil, row);
            var c = Compose(Control, row);
            Assert.Equal(row.Slots.Length, a.Skills.Count);
            Assert.Equal(row.Slots.Length, c.Skills.Count);
            Assert.Equal(a.Skills.Select(s => (s.Def.Id, s.Variation?.Name, s.Source, s.Def.Rule.OverkillCarry)),
                         c.Skills.Select(s => (s.Def.Id, s.Variation?.Name, s.Source, s.Def.Rule.OverkillCarry)));

            // WHAT A RELAUNCH DOES TO IT: the repair the host runs on load re-weaves the signature into
            // an empty slot, or opens one while there is capacity — a row's claim about surviving that
            // must be true.
            var loadout = new PlayerLoadout { SkillCapacity = row.Capacity ?? row.Slots.Length, VowCapacity = 1 };
            foreach (var s in row.Slots) Assert.True(loadout.SetSkill(loadout.AddSkill(), s.SkillId));
            var placed = LoadoutRepair.EnsureSignature(loadout, Anvil);
            Assert.True(row.Persists == (placed < 0),
                        $"{row.Label}: EnsureSignature returned {placed} — the row's claim about surviving a relaunch is wrong");
        }
        // The control really is the Anvil minus the passive: the shape is the only difference.
        Assert.Equal(0f, Control.Shape.DeadweightShare);
        Assert.Equal(Anvil.SignatureSkillId, Control.SignatureSkillId);
        Assert.Equal(Anvil.Class, Control.Class);
        Assert.Equal(Anvil.Mods, Control.Mods);
        Assert.Empty(Control.Grants);
    }
}
