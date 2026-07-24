using System;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Progression;
using Xunit;

namespace ResonanceHunter.Core.Tests.Automation;

public class RegionAutomationTests
{
    private static readonly AutomationTuning Tuning = AutomationTuning.Default;
    private const int Par = 15;

    private static Region NewRegion() => new("verdant_hollow", Par, Tuning);

    private static Creature C(Role role, int tier = 5, string? id = null)
        => Creature.Hatch(id ?? $"{role}_{Guid.NewGuid():N}", Source.Nature, role, tier);

    /// <summary>A complete team: a generator, plus Crafter, Defender and Support.</summary>
    private static Region CompleteTeam(int tier = 5)
    {
        var r = NewRegion();
        r.Assign(C(Role.Attacker, tier));
        r.Assign(C(Role.Crafter, tier));
        r.Assign(C(Role.Defender, tier));
        r.Assign(C(Role.Support, tier));
        r.Assign(C(Role.Producer, tier));
        r.Assign(C(Role.Producer, tier));
        return r;
    }

    // ── Formula 3: the composition puzzle must be REAL, not cosmetic ───────────────────────────

    /// <summary>
    /// THE design's central claim about automation: composition beats power-stacking.
    /// </summary>
    /// <remarks>
    /// Six Producers and a complete team have IDENTICAL average power here. If stacking one role could
    /// match a balanced team, the whole "assign creatures thoughtfully" layer would be busywork.
    /// </remarks>
    [Fact]
    public void test_a_complete_team_beats_a_stack_of_producers_at_identical_power()
    {
        var complete = CompleteTeam(tier: 5);

        var stacked = NewRegion();
        for (var i = 0; i < 6; i++) stacked.Assign(C(Role.Producer, 5, $"p{i}"));

        // Tolerance, not Assert.Equal(precision:). 0.7375 lands exactly on a rounding boundary, so
        // decimal-place comparison can fail on a value that is correct to 8 places. That would be a
        // flaky test asserting nothing real. (Same trap bit the animation clip tests.)
        Assert.True(MathF.Abs(complete.TeamQualityScore() - 0.7375f) < 1e-4f);
        Assert.True(MathF.Abs(stacked.TeamQualityScore() - 0.25f) < 1e-4f);

        Assert.True(complete.TeamQualityScore() > stacked.TeamQualityScore() * 2.5f,
            "A complete team must decisively beat a power-stack, or composition is decoration.");
    }

    [Fact]
    public void test_a_maxed_complete_team_reaches_the_quality_ceiling()
    {
        var maxed = CompleteTeam(tier: 20);
        Assert.Equal(1.0f, maxed.TeamQualityScore(), precision: 3);
    }

    // ── Formula 2: mastery is EARNED, and Optimized needs a real team ─────────────────────────

    /// <summary>
    /// You cannot grind your way to Optimized. It is gated on solving the composition.
    /// </summary>
    [Fact]
    public void test_optimized_is_unreachable_without_a_complete_team_however_long_you_farm()
    {
        var incomplete = NewRegion();
        incomplete.Assign(C(Role.Attacker));
        incomplete.Assign(C(Role.Crafter));
        incomplete.Assign(C(Role.Support));
        // No Defender — CCS = 0.75.

        incomplete.Tick(seconds: 3600f * 500f, gleamPerKill: 10); // farm for 500 hours

        Assert.True(incomplete.RegionMasteryPoints > Tuning.RmpThreshold3 * 10);
        Assert.Equal(MasteryLevel.FullyMastered, incomplete.MasteryLevel); // stalled, forever

        // Add the missing Defender and it unlocks immediately — the points were never the problem.
        incomplete.Assign(C(Role.Defender));
        Assert.Equal(MasteryLevel.OptimizedTeam, incomplete.MasteryLevel);
    }

    [Fact]
    public void test_mastery_climbs_through_its_levels_in_order()
    {
        var region = CompleteTeam();
        Assert.Equal(MasteryLevel.NewlyConquered, region.MasteryLevel);

        region.Tick(3600f, 10); // one hour
        Assert.True(region.RegionMasteryPoints > 1000f);
        Assert.True(region.MasteryLevel >= MasteryLevel.PartiallyMastered);

        region.Tick(3600f * 5f, 10);
        Assert.Equal(MasteryLevel.OptimizedTeam, region.MasteryLevel);
    }

    /// <summary>Formula 1's worked example: a complete 6-slot team earns ~1164 RMP in one hour.</summary>
    [Fact]
    public void test_rmp_accrual_reproduces_the_documented_hourly_rate()
    {
        var region = CompleteTeam();
        var yield_ = region.Tick(3600f, gleamPerKill: 10);

        // 3600/15 + 3600/8 + 3600/25 + 3600/15 + 3600/40 + 3600/40 = 240+450+144+240+90+90 = 1254
        // (The document's 1164 assumes a single Producer; this team has two.)
        Assert.InRange(yield_.RmpGained, 1100f, 1300f);
    }

    // ── Pillar 2: automation is EARNED. Pillar 3: it is never worthless. ──────────────────────

    /// <summary>Without a healthy GENERATOR (Attacker or Producer), nothing dies — so nothing drops. Automation is a team.</summary>
    [Fact]
    public void test_a_region_with_no_generator_kills_nothing()
    {
        var region = NewRegion();
        region.AutomationStage = 3;
        region.Assign(C(Role.Crafter));    // no Attacker AND no Producer — no generator at all
        region.Assign(C(Role.Defender));
        region.Assign(C(Role.Support));

        var y = region.Tick(3600f, gleamPerKill: 10);

        Assert.Equal(0, y.Kills);
        Assert.Equal(0, y.GleamRealized);
        Assert.True(y.RmpGained > 0f, "...but the team still builds mastery. Work is not wasted.");
    }

    /// <summary>
    /// REGRESSION: a PRODUCER in the generator slot actually kills. The Warren's generator station and
    /// CompositionCompleteness both accept Attacker OR Producer, so Tick must too. It used to demand an
    /// Attacker specifically, so a Producer-generator team read as "complete" and fully mastered yet
    /// produced zero kills/cores/gleam forever.
    /// </summary>
    [Fact]
    public void test_a_producer_in_the_generator_slot_actually_produces_kills()
    {
        var region = NewRegion();
        region.AutomationStage = 3;
        region.Assign(C(Role.Producer));   // the generator — no Attacker anywhere on this team
        region.Assign(C(Role.Crafter));
        region.Assign(C(Role.Defender));
        region.Assign(C(Role.Support));

        var y = region.Tick(3600f, gleamPerKill: 10);

        Assert.True(y.Kills > 0, "a Producer in the generator slot must generate kills, not just mastery.");
        Assert.True(y.GleamRealized > 0);
    }

    /// <summary>An unhealthy creature contributes nothing — a farm whose only generators are down produces nothing.</summary>
    [Fact]
    public void test_an_unhealthy_generator_stops_the_farm()
    {
        var region = CompleteTeam();
        region.AutomationStage = 2;

        // Down EVERY generator (the Attacker and both Producers), or a healthy Producer keeps killing.
        foreach (var c in region.Team.Where(c => c.Role is Role.Attacker or Role.Producer)) c.IsHealthy = false;

        Assert.Equal(0, region.Tick(3600f, 10).Kills);
    }

    /// <summary>Idle efficiency lands inside the locked band for its mastery level, always.</summary>
    [Fact]
    public void test_idle_efficiency_stays_inside_its_locked_band()
    {
        var region = CompleteTeam(tier: 20);

        for (var hour = 0; hour < 12; hour++)
        {
            region.Tick(3600f, 10);

            var (min, max) = EfficiencyContract.IdleBand(region.MasteryLevel);
            Assert.InRange(region.IdleEfficiencyPercent(), min, max);
        }
    }

    /// <summary>Automation gets meaningfully better as it is earned — that is Pillar 2's payoff.</summary>
    [Fact]
    public void test_automation_gets_faster_as_the_region_is_mastered()
    {
        var region = CompleteTeam(tier: 20);
        var fresh = region.AutomationClearTimeSeconds();

        region.Tick(3600f * 6f, 10);
        var mastered = region.AutomationClearTimeSeconds();

        Assert.True(mastered < fresh);
        Assert.Equal(MasteryLevel.OptimizedTeam, region.MasteryLevel);

        // ...but it never beats par by more than the locked 120% ceiling.
        Assert.True(region.IdleEfficiencyPercent() <= EfficiencyContract.MaxIdleEfficiencyPercent);
    }

    /// <summary>Par is the anchor. Automation's clear time is DERIVED from it — never the reverse.</summary>
    [Fact]
    public void test_par_is_never_altered_by_automation()
    {
        var region = CompleteTeam(tier: 20);
        region.Tick(3600f * 20f, 10);

        Assert.Equal(Par, region.ParClearTimeSeconds);
    }

    // ── The inflation bomb ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 100 hours unattended must realize ~2,400 Gleam — not the ~760,000 the raw faucet implies.
    /// </summary>
    [Fact]
    public void test_a_hundred_hour_unattended_session_cannot_break_the_economy()
    {
        var region = CompleteTeam(tier: 20);
        region.AutomationStage = 3;

        var realized = 0;
        var forfeited = 0;

        for (var hour = 0; hour < 100; hour++)
        {
            var y = region.Tick(3600f, gleamPerKill: 30); // a deliberately generous per-kill value
            realized += y.GleamRealized;
            forfeited += y.GleamForfeitedToCap;
        }

        var ceiling = Tuning.AutoSellGleamCapPerHour * 100;

        Assert.True(realized <= ceiling + 5, $"Realized {realized} Gleam — the cap leaked.");
        Assert.True(forfeited > realized * 10, "The cap should be visibly biting, or it is not doing its job.");
    }

    /// <summary>Stage gates are real: killing, collecting and selling are separately earned.</summary>
    [Theory]
    [InlineData(1, false, false)] // auto-attack only
    [InlineData(2, true, false)]  // + auto-loot
    [InlineData(3, true, true)]   // + auto-sell
    public void test_automation_stages_unlock_capabilities_in_order(int stage, bool collects, bool sells)
    {
        var region = CompleteTeam(tier: 20);
        region.AutomationStage = stage;
        region.Tick(3600f * 6f, 10); // reach a good mastery level first

        var y = region.Tick(3600f, gleamPerKill: 10);

        Assert.True(y.Kills > 0);
        Assert.Equal(collects, y.CoresProduced > 0);
        Assert.Equal(sells, y.GleamRealized > 0);
    }

    /// <summary>Offline catch-up must equal live ticking. A player must not be punished for closing the game.</summary>
    [Fact]
    public void test_offline_progress_matches_live_ticking()
    {
        var live = CompleteTeam(tier: 12);
        var offline = CompleteTeam(tier: 12);

        for (var i = 0; i < 3600; i++) live.Tick(1f, 10);   // one hour, second by second
        offline.Tick(3600f, 10);                            // one hour, in a single catch-up

        Assert.Equal(live.RegionMasteryPoints, offline.RegionMasteryPoints, precision: 0);
        Assert.Equal(live.MasteryLevel, offline.MasteryLevel);
    }

    /// <summary>
    /// A farm ticked once per second must KILL as much as one ticked in a single block.
    /// </summary>
    /// <remarks>
    /// This is a regression test for a bug that made the entire idle half of the game silently dead.
    /// Kills were <c>(int)(seconds / clearTime)</c>, which truncates. The game ticks automation once per
    /// second and a clear takes tens of seconds, so every live tick computed <c>(int)0.025 == 0</c> and
    /// the farm produced NOTHING while you watched it. It only "worked" in tests that used one large
    /// offline tick. Fractional kill progress must carry across ticks.
    /// </remarks>
    [Fact]
    public void test_a_farm_ticked_every_second_kills_as_much_as_one_ticked_in_a_single_block()
    {
        var perSecond = CompleteTeam(tier: 20);
        var oneBlock = CompleteTeam(tier: 20);

        foreach (var r in new[] { perSecond, oneBlock })
        {
            r.AutomationStage = 2;
            r.Tick(3600f * 6f, 10); // reach a good mastery level first, identically
        }

        var live = 0;
        for (var i = 0; i < 3600; i++) live += perSecond.Tick(1f, 10).Kills;

        var block = oneBlock.Tick(3600f, 10).Kills;

        Assert.True(live > 0, "A farm ticked once per second killed NOTHING. The idle game is dead.");
        Assert.InRange(live, block - 2, block + 2);
    }

    /// <summary>A team is capped in size — you cannot brute-force by assigning everything you own.</summary>
    [Fact]
    public void test_team_size_is_capped()
    {
        var region = NewRegion();
        for (var i = 0; i < Tuning.MaxTeamSize; i++)
            Assert.True(region.Assign(C(Role.Producer, 5, $"p{i}")));

        Assert.False(region.Assign(C(Role.Producer, 5, "overflow")));
        Assert.Equal(Tuning.MaxTeamSize, region.Team.Count);
    }
}
