using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Tests.Builds;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Characters;

/// <summary>
/// WHY does THE ANVIL measure as dead weight? The mechanic, measured directly, on builds a player can weave.
/// </summary>
/// <remarks>
/// <para>
/// The roster-parity gauntlet (2026-09-07) found DEADWEIGHT inside the 2% floor on both of HARDFACE's
/// reachable single-Source pairs — 0.0% as UPSET beside BLOW FLATTEN, +1.1% as PLANISH beside DRINK
/// SIPHON — and pinned the finding without a mechanism. Two weak pairs do not say WHICH mechanic is
/// wrong: the passive, the signature, the pairing, or the clock the gauntlet reads. This file asks the
/// sim the question the clear-time delta cannot answer, through the carry counters
/// <see cref="WaveMetrics"/> grew for it: how often a kill left overkill a carry could ride, how much
/// of that spill the rule was entitled to, how often the carry actually landed, what it sent, and
/// what it killed.
/// </para>
/// <para>
/// <b>Reachable only.</b> Every row composes through <see cref="Reachable.Compose"/> — the shipped
/// loadout, the shipped composer, a real <c>SkillProgress</c> — so no per-slot Source, no impossible
/// variation, no foreign signature and no fixture-only state can reach a row. The "no HARDFACE" rows
/// are reachable because the weave screen lets a player clear any slot, the signature's included
/// (<c>LoadoutScreen</c> → <c>PlayerLoadout.RemoveSkill</c>). What a relaunch or a champion switch
/// does next is <c>LoadoutRepair.EnsureSignature</c>: it re-weaves HARDFACE into the first EMPTY slot,
/// and if none is empty it OPENS one while the loadout is under capacity — so a HARDFACE-free loadout
/// persists only when it is FULL. The rows say which they are, and the reachability test runs the
/// repair on each to prove it. The control is THE ANVIL with the passive removed and nothing else
/// changed — same class, same signature, same skills — so the delta between the two IS DEADWEIGHT.
/// Gear is bare on both, for every row.
/// </para>
/// <para>
/// <b>Seed-invariant.</b> On bare gear the fight draws no random number that moves this gauntlet
/// (the parity file measured the same: forty seeds, one number), so the forty seeds here are forty
/// identical runs and the "2% floor" is not sampling noise — it is one beat of the cast cadence. The
/// measurement asserts the invariance rather than assuming it, so a fixture that starts to roll dice
/// is noticed the day it does.
/// </para>
/// <para>
/// <b>Diagnostic, not balance.</b> The numbers are printed; the assertions hold the counters to their
/// own arithmetic (a carry never lands more often than a kill leaves spill, never more than the
/// carry rule's share of it, never on a control that wears no carry rule) and hold the mechanic to
/// being LIVE on a reachable build. No threshold here says what DEADWEIGHT should be worth — that is
/// the decision this file exists to inform.
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
    /// i.e. a FULL loadout); <paramref name="Persists"/> says whether the composed loadout survives a
    /// relaunch unchanged (a HARDFACE-free loadout only does when it is full); <paramref name="SkillCarry"/>
    /// marks a row whose SKILL carries its own overkill, where the passive is shadowed by the max rule.
    /// </summary>
    private sealed record Row(string Label, Reachable.Slot[] Slots, int? Capacity = 4, bool Vow = false,
                              bool Persists = true, bool SkillCarry = false);

    /// <summary>The matrix: the parity pairs, each HARDFACE variation alone and crossed, DEADWEIGHT with no HARDFACE, and a skill that carries by itself.</summary>
    private static readonly Row[] Matrix =
    {
        new("HARDFACE unchosen — a fresh Anvil", new[] { new Reachable.Slot(Hardface) }, Capacity: 1),
        new("UPSET alone", new[] { new Reachable.Slot(Hardface, "UPSET") }, Capacity: 1),
        new("PLANISH alone", new[] { new Reachable.Slot(Hardface, "PLANISH") }, Capacity: 1),
        new("UPSET + BLOW FLATTEN", new[] { new Reachable.Slot(Hardface, "UPSET"), new Reachable.Slot("hammer_blow", "FLATTEN") }),
        new("PLANISH + DRINK SIPHON", new[] { new Reachable.Slot(Hardface, "PLANISH"), new Reachable.Slot("drain_drink", "SIPHON") }),
        new("PLANISH + BLOW FLATTEN", new[] { new Reachable.Slot(Hardface, "PLANISH"), new Reachable.Slot("hammer_blow", "FLATTEN") }),
        new("UPSET + DRINK SIPHON", new[] { new Reachable.Slot(Hardface, "UPSET"), new Reachable.Slot("drain_drink", "SIPHON") }),
        // A one-slot account whose one slot holds BLOW: full, so a relaunch cannot re-weave HARDFACE.
        new("BLOW FLATTEN — no HARDFACE (full one-slot loadout)", new[] { new Reachable.Slot("hammer_blow", "FLATTEN") }, Capacity: null),
        // Two actives need three slots; the third stands empty, so this build lasts the session and
        // a relaunch puts HARDFACE back into the empty slot. Measured as what the player fights with
        // in the meantime.
        new("BLOW FLATTEN + SPRAY CLUSTER — no HARDFACE (in-session: a relaunch re-weaves it)",
            new[] { new Reachable.Slot("hammer_blow", "FLATTEN"), new Reachable.Slot("volley_spray", "CLUSTER") }, Capacity: 3, Persists: false),
        // The same pair with the third slot filled by a passive field: full, so it persists.
        new("BLOW FLATTEN + SPRAY CLUSTER + MIRE NUMB — no HARDFACE (full loadout)",
            new[] { new Reachable.Slot("hammer_blow", "FLATTEN"), new Reachable.Slot("volley_spray", "CLUSTER"), new Reachable.Slot("field_mire", "NUMB") }, Capacity: null),
        // BLOW reinforced with BREAKTHROUGH carries half its own overkill: on BLOW's kills the carry
        // is max(0.33, 0.5) = 0.5 with or without the passive, so DEADWEIGHT pays only on UPSET's kills.
        new("UPSET + BLOW FLATTEN with BREAKTHROUGH — the skill carries by itself",
            new[] { new Reachable.Slot(Hardface, "UPSET"), new Reachable.Slot("hammer_blow", "FLATTEN", Reinforcements: new[] { "BREAKTHROUGH" }) },
            SkillCarry: true),
        new("UPSET + BLOW FLATTEN, VOW OF THE PURE kept (the parity row)",
            new[] { new Reachable.Slot(Hardface, "UPSET", PureVow), new Reachable.Slot("hammer_blow", "FLATTEN") }, Vow: true),
        new("PLANISH + DRINK SIPHON, VOW OF THE PURE kept (the parity row)",
            new[] { new Reachable.Slot(Hardface, "PLANISH", PureVow), new Reachable.Slot("drain_drink", "SIPHON") }, Vow: true),
    };

    /// <summary>One character's totals over every seed and wave of the gauntlet, plus per-wave detail.</summary>
    private sealed class Tally
    {
        public double ClearMs, HpLost, Delivered, Overkill, EligibleOverkill, Carried;
        public long OverkillKills, Carries, CarryKills, Killed;
        /// <summary>The largest carry rule in the build — the passive's, or a skill's own where one carries.</summary>
        public float MaxCarry;
        public readonly double[] WaveMs = new double[3], WaveDelivered = new double[3], WaveOverkill = new double[3], WaveCarried = new double[3];
        public readonly long[] WaveOverkillKills = new long[3], WaveCarries = new long[3], WaveCarryKills = new long[3];

        /// <summary>The carry's share of everything that landed — the carry's direct contribution.</summary>
        public double CarriedShare => Delivered <= 0 ? 0 : Carried / Delivered;
    }

    private static Build Compose(Character c, Row row)
    {
        var build = Reachable.Compose(c, row.Slots, slotCapacity: row.Capacity, knownVows: Vows.Catalog, vowCapacity: 1);
        if (row.Vow)
        {
            var vow = Vows.Catalog.First(v => v.Id == PureVow);
            Assert.True(Vows.IsActive(vow, SoloBattle.DescribeBuild(build, new Hunter())),
                        $"{row.Label}: the pair does not keep VOW OF THE PURE — the fixture, not the design.");
        }
        // The passive is the ONLY carry on a plain row, and not the only one on a SkillCarry row: the
        // shares below are read as the passive's on the first kind and as the max rule's on the second.
        var skillCarry = build.Skills.Max(s => s.Def.Rule.OverkillCarry);
        Assert.True(row.SkillCarry == skillCarry > 0f,
                    $"{row.Label}: a skill carries {skillCarry:0.00} of its own overkill — the row is mislabelled.");
        return build;
    }

    private static Tally Measure(Character c, Row row, int seeds = ParityGauntlet.Seeds)
    {
        var build = Compose(c, row);
        var t = new Tally { MaxCarry = MathF.Max(c.Shape.OverkillCarry, build.Skills.Max(s => s.Def.Rule.OverkillCarry)) };
        double firstCarried = double.NaN, firstClear = double.NaN;
        for (var s = 0; s < seeds; s++)
        {
            var champ = ParityGauntlet.FreshChampion();
            var hunter = new Hunter();
            var rng = new Random(ParityGauntlet.Seed(s));
            double seedCarried = 0, seedClear = 0;
            for (var w = 0; w < ParityGauntlet.Waves.Length; w++)
            {
                var metrics = new WaveMetrics();
                var (outcome, _) = SoloBattle.ResolveWave(champ, build, hunter, ParityGauntlet.Waves[w].Make(),
                                                          ParityGauntlet.EnemyIntervalMs, ParityGauntlet.Tuning, rng,
                                                          metrics: metrics);
                Assert.True(outcome == WaveOutcome.Cleared, $"{row.Label} / {ParityGauntlet.Waves[w].Name}: stalled — the clock saturates");
                t.ClearMs += metrics.DurationMs;
                t.Delivered += metrics.DeliveredDamage;
                t.Overkill += metrics.Overkill;
                t.EligibleOverkill += metrics.EligibleOverkill;
                t.Carried += metrics.CarriedDamage;
                t.OverkillKills += metrics.OverkillKills;
                t.Carries += metrics.CarriesLanded;
                t.CarryKills += metrics.CarryKills;
                t.Killed += metrics.CreaturesKilled;
                t.WaveMs[w] += metrics.DurationMs;
                t.WaveDelivered[w] += metrics.DeliveredDamage;
                t.WaveOverkill[w] += metrics.Overkill;
                t.WaveCarried[w] += metrics.CarriedDamage;
                t.WaveOverkillKills[w] += metrics.OverkillKills;
                t.WaveCarries[w] += metrics.CarriesLanded;
                t.WaveCarryKills[w] += metrics.CarryKills;
                seedCarried += metrics.CarriedDamage;
                seedClear += metrics.DurationMs;
            }
            t.HpLost += ParityGauntlet.ChampionHealth - champ.Health;

            // SEED-INVARIANT, asserted: the day a passive starts rolling dice this fails, and the
            // averages above stop being one number wearing forty coats.
            if (double.IsNaN(firstCarried)) { firstCarried = seedCarried; firstClear = seedClear; }
            Assert.True(seedCarried == firstCarried && seedClear == firstClear,
                        $"{row.Label}: seed {s} differs from seed 0 — the gauntlet is no longer seed-invariant on bare gear; read the spread before reading the table.");
        }
        return t;
    }

    [Fact]
    public void test_the_deadweight_matrix_is_measured_directly()
    {
        var carry = Anvil.Shape.OverkillCarry;
        Assert.True(carry > 0f, "THE ANVIL's passive is no longer an overkill carry — this diagnostic measures the wrong thing");
        var seeds = ParityGauntlet.Seeds;
        var roster = ParityGauntlet.Waves.Sum(w => w.Make().Count);

        _out.WriteLine($"DEADWEIGHT DIAGNOSTIC — THE ANVIL against THE ANVIL-WITHOUT-DEADWEIGHT on the parity gauntlet");
        _out.WriteLine($"(swarm 10x320 · pack 4x1,600/45 def · boss 14,000/70 def; {seeds} seeds, all identical; bare gear; passive carry {carry:0.00})");
        _out.WriteLine("");
        _out.WriteLine($"{"ROW",-84} {"FASTER",7} {"HP Δ",6} {"ELIG a/c",10} {"CARRIES",8} {"CARRIED",9} {"OF DEALT",8} {"C.KILLS",7} {"OF ENTITLED",11}");
        _out.WriteLine(new string('-', 158));

        var results = new List<(Row Row, Tally Anvil, Tally Control)>();
        foreach (var row in Matrix)
        {
            var a = Measure(Anvil, row);
            var c = Measure(Control, row);
            results.Add((row, a, c));

            var faster = a.ClearMs <= 0 ? 0 : 100.0 * (c.ClearMs / a.ClearMs - 1.0);
            var hpDelta = c.HpLost <= 0 ? 0 : 100.0 * (a.HpLost / c.HpLost - 1.0);
            // How much of the rule's ENTITLEMENT landed: its share of the spill on the kills it may
            // ride, against what it actually sent. The shortfall is spill with no living enemy left.
            var ofEntitled = a.EligibleOverkill <= 0 ? 0 : a.Carried / (a.EligibleOverkill * a.MaxCarry);
            _out.WriteLine($"{row.Label,-84} {faster,6:+0.0;-0.0;0.0}% {hpDelta,5:+0;-0;0}% " +
                           $"{a.OverkillKills / (double)seeds,4:0.0}/{c.OverkillKills / (double)seeds,-4:0.0} " +
                           $"{a.Carries / (double)seeds,8:0.0} {a.Carried / seeds,9:N0} {a.CarriedShare,7:0.0%} " +
                           $"{a.CarryKills / (double)seeds,7:0.00} {ofEntitled,10:0%}" +
                           (row.SkillCarry ? $"   (control carried {c.Carried / seeds:N0} on its own rule; the passive adds {(a.Carried - c.Carried) / seeds:N0})" : ""));
        }

        _out.WriteLine("");
        _out.WriteLine("(ELIG a/c: skill kills per run that left overkill — the Anvil's, the control's. CARRIES: of those, the ones");
        _out.WriteLine(" that had a living enemy to land on. CARRIED: damage the carry sent per run. OF DEALT: that as a share of");
        _out.WriteLine(" everything the Anvil landed. C.KILLS: creatures a carried hit finished, per run. OF ENTITLED: what the");
        _out.WriteLine(" carry sent against the rule's share of the spill on the kills it may ride — 100% means every eligible");
        _out.WriteLine(" spill found a living enemy; the rest died with nobody left, the boss room above all.)");
        _out.WriteLine("");
        _out.WriteLine("PER WAVE — where the carry lives. (Cooldowns persist across the three waves, so a wave's clear time carries");
        _out.WriteLine(" the previous wave's phase; the counters do not.)");
        foreach (var (row, a, c) in results)
        {
            _out.WriteLine($"  {row.Label}");
            for (var w = 0; w < ParityGauntlet.Waves.Length; w++)
            {
                var share = a.WaveDelivered[w] <= 0 ? 0 : a.WaveCarried[w] / a.WaveDelivered[w];
                var fasterW = a.WaveMs[w] <= 0 ? 0 : 100.0 * (c.WaveMs[w] / a.WaveMs[w] - 1.0);
                _out.WriteLine($"    {ParityGauntlet.Waves[w].Name,-6} clear {a.WaveMs[w] / seeds,8:N0} ms ({fasterW,5:+0.0;-0.0;0.0}%)  " +
                               $"eligible {a.WaveOverkillKills[w] / (double)seeds,4:0.0}  carries {a.WaveCarries[w] / (double)seeds,4:0.0}  " +
                               $"carried {a.WaveCarried[w] / seeds,7:N0} ({share,5:0.0%} of dealt)  carry kills {a.WaveCarryKills[w] / (double)seeds,4:0.00}  " +
                               $"spill {a.WaveOverkill[w] / seeds,7:N0}");
            }
        }

        // ── STRUCTURAL — the counter site's shape. These cannot fail without the site being rewritten;
        //    they are here so a rewrite is noticed, not as findings. ──
        foreach (var (row, a, c) in results)
        {
            if (!row.SkillCarry)
                Assert.True(c.Carries == 0 && c.Carried == 0 && c.CarryKills == 0,
                            $"{row.Label}: the control carried — it wears no carry rule");
            else
                Assert.True(a.Carried >= c.Carried, $"{row.Label}: the passive took carry away from a skill that carries by itself");
            Assert.True(a.Carries <= a.OverkillKills, $"{row.Label}: more carries than kills with spill");
            Assert.True(a.EligibleOverkill <= a.Overkill * 1.001 + 1.0, $"{row.Label}: eligible spill exceeds all spill");
            Assert.True(a.Carried <= a.EligibleOverkill * a.MaxCarry * 1.001 + 1.0,
                        $"{row.Label}: carried {a.Carried:N0} exceeds the rule's share of the eligible spill {a.EligibleOverkill * a.MaxCarry:N0}");
            Assert.True(a.CarryKills <= a.Carries, $"{row.Label}: more carry kills than carries");
            // Both arms cleared the same roster every run — the counters above are over the same kills.
            Assert.True(a.Killed == roster * seeds && c.Killed == roster * seeds,
                        $"{row.Label}: {a.Killed} / {c.Killed} kills for a roster of {roster} x {seeds} runs");
        }

        // ── THE MECHANIC IS LIVE on a reachable build: the passive has kills to ride and rides them. ──
        var lone = results.Single(r => r.Row.Label.StartsWith("BLOW FLATTEN — no HARDFACE", StringComparison.Ordinal));
        Assert.True(lone.Anvil.Carries > 0, "DEADWEIGHT never carried on BLOW FLATTEN alone — the passive is not reaching the fight");
        var parity = results.Single(r => r.Row.Label.StartsWith("UPSET + BLOW FLATTEN,", StringComparison.Ordinal));
        Assert.True(parity.Anvil.Carries > 0, "DEADWEIGHT never carried on the parity pair — the 0.0% is a dead trigger, not a small one");
        // And where a skill carries by itself, the passive's own delta is what the max rule leaves it: never negative.
        var shadowed = results.Single(r => r.Row.SkillCarry);
        Assert.True(shadowed.Anvil.Carried >= shadowed.Control.Carried, "the passive reduced a skill's own carry");
    }

    [Fact]
    public void test_the_carry_counters_are_deterministic()
    {
        // Licenses reading the matrix as effects: the same row gives the same counters twice over.
        var row = Matrix.Single(r => r.Label == "UPSET + BLOW FLATTEN");
        var once = Measure(Anvil, row, seeds: 3);
        var twice = Measure(Anvil, row, seeds: 3);
        Assert.Equal(once.Carried, twice.Carried);
        Assert.Equal(once.Carries, twice.Carries);
        Assert.Equal(once.CarryKills, twice.CarryKills);
        Assert.Equal(once.ClearMs, twice.ClearMs);
    }

    [Fact]
    public void test_every_matrix_row_is_a_build_a_player_can_weave()
    {
        // The whole point. A row the composer refuses throws with the reason; a row it silently
        // trims is caught by Reachable's slot-by-slot check. Both champions must be able to make it.
        foreach (var row in Matrix)
        {
            var a = Compose(Anvil, row);
            var c = Compose(Control, row);
            Assert.Equal(row.Slots.Length, a.Skills.Count);
            Assert.Equal(row.Slots.Length, c.Skills.Count);
            Assert.Equal(a.Skills.Select(s => (s.Def.Id, s.Variation?.Name, s.Source)),
                         c.Skills.Select(s => (s.Def.Id, s.Variation?.Name, s.Source)));

            // WHAT A RELAUNCH DOES TO IT. The repair the host runs on load and on a champion switch
            // re-weaves the signature into an empty slot, or opens one while there is capacity. A row
            // that says it persists must come through untouched; one that says it does not must get
            // HARDFACE back — so the label tells the truth about how long the build lasts.
            var loadout = new PlayerLoadout { SkillCapacity = row.Capacity ?? row.Slots.Length, VowCapacity = 1 };
            foreach (var s in row.Slots) Assert.True(loadout.SetSkill(loadout.AddSkill(), s.SkillId));
            var placed = LoadoutRepair.EnsureSignature(loadout, Anvil);
            Assert.True(row.Persists == (placed < 0),
                        $"{row.Label}: EnsureSignature returned {placed} — the row's claim about surviving a relaunch is wrong");
        }
        // And the control really is the Anvil minus the passive: the shape is the only difference.
        Assert.Equal(0f, Control.Shape.OverkillCarry);
        Assert.Equal(Anvil.SignatureSkillId, Control.SignatureSkillId);
        Assert.Equal(Anvil.Class, Control.Class);
        Assert.Equal(Anvil.Mods, Control.Mods);
        Assert.Empty(Control.Grants);
    }
}
