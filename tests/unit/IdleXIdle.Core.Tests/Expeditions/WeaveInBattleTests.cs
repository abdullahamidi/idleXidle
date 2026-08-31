using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using Xunit;

namespace IdleXIdle.Core.Tests.Expeditions;

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
            Assert.Equal(2, targets.Count(x => SourceMatchup.Effectiveness(attacker, x) > 1f));
            Assert.Equal(2, targets.Count(x => SourceMatchup.Effectiveness(attacker, x) < 1f));
        }
    }

    // ── Vow pricing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_harsher_vow_is_worth_more_power()
    {
        // The pricing promise: a Vow that costs the build less is always worth less, so no demand Vow
        // can dominate another.
        var t = WeavingTuning.Default;
        var unbound = Vows.ById("vow_unbound")!;    // severity 0.80 — refuse the trait tree's payoff
        var complete = Vows.ById("vow_complete")!;  // severity 0.20 — most builds already satisfy it

        Assert.True(Vows.Multiplier(unbound) > Vows.Multiplier(complete));
    }

    /// <summary>
    /// Every demand is answered by the BUILD the sim actually describes.
    /// </summary>
    /// <remarks>
    /// The other half of WeavingTests.test_every_demand_can_be_both_met_and_unmet. That one proves the
    /// demands are satisfiable in the abstract; this one proves SoloBattle.DescribeBuild produces a
    /// context that can satisfy them — the join between the two halves, which is where every dead system
    /// in this project has hidden.
    /// </remarks>
    [Fact]
    public void test_the_sim_describes_a_build_the_vows_can_read()
    {
        static EquippedSkill Sk(Form form, Source src)
            => new(new WovenAbility { Name = "s", Source = src, Form = form }, 1_500);

        var mono = new Build();
        mono.Weave(Sk(Form.Strike, Source.Body));

        var broad = new Build();
        broad.Weave(Sk(Form.Strike, Source.Body));
        broad.Weave(Sk(Form.Aura, Source.Mind));

        var monoCtx = SoloBattle.DescribeBuild(mono, new Hunter());
        var broadCtx = SoloBattle.DescribeBuild(broad, new Hunter());

        Assert.True(Vows.IsActive(Vows.ById("vow_singular")!, monoCtx),
            "A one-Form build does not satisfy THE SINGULAR — the sim is not describing Forms.");
        Assert.False(Vows.IsActive(Vows.ById("vow_singular")!, broadCtx));

        Assert.True(Vows.IsActive(Vows.ById("vow_pure")!, monoCtx));
        Assert.False(Vows.IsActive(Vows.ById("vow_pure")!, broadCtx));

        // A fresh Hunter trains no crit, wears nothing and sockets nothing.
        Assert.True(Vows.IsActive(Vows.ById("vow_bluntedge")!, monoCtx));
        Assert.True(Vows.IsActive(Vows.ById("vow_unguarded")!, monoCtx));
        Assert.True(Vows.IsActive(Vows.ById("vow_unbound")!, monoCtx));
        Assert.True(Vows.IsActive(Vows.ById("vow_barefoot")!, monoCtx));
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
