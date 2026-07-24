using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Expeditions;

/// <summary>
/// Resonance Weaving pricing, and the boss-wave heartbeat it keys off.
/// </summary>
/// <remarks>
/// The in-fight wiring — that a Vow's BENEFIT changes what a strike does, that the Source matchup reaches a
/// blow — is proven against the live single-champion engine in <c>Builds/SoloBattleTests</c> (the Source
/// matchup, every conditional Vow, affinity). This file keeps the vehicle-free half: the circulant Source
/// table, the Vow pricing curve, the "every conditional Vow can both hold AND fail" structural guard, and
/// the shared boss-wave scaling helpers.
///
/// The Vow COSTS the squad engine used to charge (RECKLESS OFFERING's health, FRAGILITY's damage-taken)
/// are wired into the solo fight too — FRAGILITY in <c>SoloBattle.ResolveWave</c>, RECKLESS at champion
/// mint via <c>SoloBattle.VowHealthMultiplier</c> — and proven in <c>Builds/SoloBattleTests</c>.
/// </remarks>
public class WeaveInBattleTests
{
    // ── The Source matchup ────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_no_source_is_the_best_source()
    {
        // The circulant table's whole promise: every Source has exactly 2 strengths and 2 weaknesses,
        // so there is no creature you simply always bring.
        var t = WeavingTuning.Default;
        foreach (var attacker in System.Enum.GetValues<Source>())
        {
            var targets = System.Enum.GetValues<Source>();
            Assert.Equal(2, targets.Count(x => Weaving.SourceEffectiveness(attacker, x, t) > 1f));
            Assert.Equal(2, targets.Count(x => Weaving.SourceEffectiveness(attacker, x, t) < 1f));
        }
    }

    // ── Vow pricing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_rarer_vow_is_worth_more_power()
    {
        // The pricing promise: a Vow that is easier to satisfy is always worth less, so no conditional
        // Vow can dominate another.
        var t = WeavingTuning.Default;
        var bloodied = Weaving.ById("vow_bloodied")!;  // uptime 0.25
        var patience = Weaving.ById("vow_patience")!;  // uptime 0.60

        Assert.True(Weaving.VowMultiplier(bloodied, t) > Weaving.VowMultiplier(patience, t));
    }

    [Fact]
    public void test_every_conditional_vow_can_both_hold_and_fail()
    {
        // A real restriction sits STRICTLY between two dead ends. VOW OF THE OPENING could never hold — it
        // waited on a weak-point window manual combat took with it, priced +90% for a bonus that never
        // paid. VOW OF THE VANGUARD had the mirror flaw after the solo pivot: it watched the front slot the
        // lone champion ALWAYS occupies, so it could never fail — a full multiplier for a restriction it
        // never bore. Both are dead content. Every conditional Vow must provably do BOTH — the old version
        // of this test only checked "can hold", which is exactly why the always-on Vanguard slipped past.
        foreach (var vow in Weaving.Catalog.Where(v => v.Kind == VowKind.Conditional))
        {
            Assert.NotEqual(VowTrigger.None, vow.Trigger);

            var (hold, fail) = vow.Trigger switch
            {
                VowTrigger.BelowHealthFraction => (new WeaveContext(0f, 0, false, 0), new WeaveContext(1f, 0, false, 0)),
                VowTrigger.AboveHealthFraction => (new WeaveContext(1f, 0, false, 0), new WeaveContext(0f, 0, false, 0)),
                VowTrigger.AgainstBoss => (new WeaveContext(1f, 0, true, 0), new WeaveContext(1f, 0, false, 0)),
                VowTrigger.AfterElapsedMs => (new WeaveContext(1f, 999_999, false, 0), new WeaveContext(1f, 0, false, 0)),
                _ => (new WeaveContext(0f, 0, false, 0), new WeaveContext(0f, 0, false, 0)),
            };

            Assert.True(Weaving.IsActive(vow, hold), $"{vow.Id} has a condition that can never hold");
            Assert.False(Weaving.IsActive(vow, fail), $"{vow.Id} has a condition that can never fail — not a restriction");
        }
    }

    // ── Boss waves ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_fifth_wave_is_a_boss()
    {
        var t = ExpeditionTuning.Default;
        Assert.False(WaveScaling.IsBossWave(0, t)); // wave 0 is not a boss — the run has not started
        Assert.False(WaveScaling.IsBossWave(4, t));
        Assert.True(WaveScaling.IsBossWave(5, t));
        Assert.True(WaveScaling.IsBossWave(10, t));
    }

    [Fact]
    public void test_a_boss_is_tougher_than_the_wave_before_it()
    {
        var t = ExpeditionTuning.Default;
        Assert.True(WaveScaling.EnemyScale(5, t) > WaveScaling.EnemyScale(4, t) * 2f);
    }

    [Fact]
    public void test_a_boss_pays_for_the_spike_it_is()
    {
        // A boss that were only tougher would make "bank or push into the boss" always-bank — a wall
        // with a countdown, not a decision.
        var t = ExpeditionTuning.Default;
        var jump = WaveScaling.HaulScale(5, t) / WaveScaling.HaulScale(4, t);
        Assert.True(jump > 1.5f, $"pushing into a boss paid only {jump:0.00}x — that is a tax, not a bet");
    }
}
