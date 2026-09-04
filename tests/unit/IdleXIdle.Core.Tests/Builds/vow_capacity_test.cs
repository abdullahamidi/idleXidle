using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A Vow is a promise about the BUILD, so how many you may make is a real, bounded, granted number.
/// </summary>
/// <remarks>
/// <para>
/// It was not one. A Vow rode a skill slot and each skill took exactly ONE Vow's multiplier, so four
/// DIFFERENT Vows paid precisely what one Vow repeated on four slots paid — while charging four prices
/// and demanding four restrictions hold at once. The rational play was to satisfy the single harshest
/// restriction you could and bind that one Vow everywhere, which made BIND TO SLOT a question with one
/// correct answer, asked four times. And there was nothing for a milestone to grant, because the cap
/// was a side effect of a different system's ceiling.
/// </para>
/// <para>
/// So: bonuses are SUMMED under one ceiling, capacity is an explicit count, and at capacity 1 the game
/// is numerically what it was.
/// </para>
/// </remarks>
public class VowCapacityTest
{
    private static BuildContext Met => new(
        DistinctStyles: 1, DistinctSources: 1, SkillsWoven: 4, SkillSlots: 4,
        CritPercent: 0f, BaseCritPercent: 0f, SkillRate: 1f, Defence: 0,
        KeystonesWorn: 0, WornSlots: new HashSet<BareSlot>());

    [Fact]
    public void test_one_vow_pays_exactly_what_it_always_paid()
    {
        // The compatibility floor. At capacity 1 nothing about the numbers moved, which is what makes
        // capacity 2 and 3 NEW power rather than a redistribution of power the player already had.
        var vow = Vows.ById("vow_singular")!;
        var expected = Vows.Multiplier(vow);

        Assert.Equal(expected, Vows.CombinedFactor(new[] { vow }, Met), 4);
    }

    [Fact]
    public void test_the_same_vow_on_every_slot_is_one_promise()
    {
        var vow = Vows.ById("vow_singular")!;
        Assert.Equal(Vows.CombinedFactor(new[] { vow }, Met),
                     Vows.CombinedFactor(new[] { vow, vow, vow, vow }, Met), 4);
    }

    [Fact]
    public void test_a_second_vow_adds_its_bonus_rather_than_nothing()
    {
        // The whole reason capacity is worth granting. Under the per-slot model a second, weaker Vow
        // on another slot was STRICTLY WORSE than repeating the first one there.
        var one = Vows.ById("vow_singular")!;
        var two = Vows.ById("vow_unguarded")!;

        var alone = Vows.CombinedFactor(new[] { one }, Met);
        var both = Vows.CombinedFactor(new[] { one, two }, Met);

        Assert.True(both > alone, $"a second sworn Vow paid nothing: {both} vs {alone}");
    }

    [Fact]
    public void test_a_broken_vow_pays_nothing_at_all()
    {
        // Restriction buys power. A promise you are not keeping is not a restriction.
        var broken = new BuildContext(
            DistinctStyles: 3, DistinctSources: 3, SkillsWoven: 2, SkillSlots: 4,
            CritPercent: 0f, BaseCritPercent: 0f, SkillRate: 1f, Defence: 9,
            KeystonesWorn: 2, WornSlots: new HashSet<BareSlot> { BareSlot.Boots });

        Assert.Equal(1f, Vows.CombinedFactor(new[] { Vows.ById("vow_singular") }, broken), 4);
        Assert.False(Vows.AnyKept(new[] { Vows.ById("vow_singular") }, broken));
    }

    [Fact]
    public void test_the_combined_ceiling_binds_the_worst_case_in_the_game()
    {
        // Three maximal Vows against the whole Vow-power stack — mastery's PLEDGE and ZEALOT plus THE
        // OATHBOUND's passive — is the strongest Vow build the game can express. It must land on one
        // number a sweep can print, not on a stack nobody can bound.
        var worst = Vows.Catalog
            .Where(v => v.Kind == VowKind.Demand)
            .OrderByDescending(v => Vows.Multiplier(v))
            .Take(3)
            .ToList();
        var tuning = VowTuning.Default;
        const float PowerStack = 1.30f * 1.60f * 1.50f;   // x3.12

        var raw = worst.Sum(v => Vows.Multiplier(v) - 1f);
        Assert.True(raw > tuning.CombinedBonusCeiling, "the fixture no longer reaches the ceiling");

        var factor = Vows.CombinedFactor(worst, Met, PowerStack);
        Assert.Equal(1f + tuning.CombinedBonusCeiling * PowerStack, factor, 4);
    }

    [Fact]
    public void test_the_ceiling_sits_above_the_best_single_vow_so_a_second_is_worth_swearing()
    {
        var best = Vows.Catalog.Max(v => Vows.Multiplier(v) - 1f);
        Assert.True(VowTuning.Default.CombinedBonusCeiling > best,
            "the ceiling is at or below one Vow's bonus, so capacity 2 grants nothing");
    }

    // ── THE GRANT, AND THE REFUSAL ───────────────────────────────────────────────────────────────

    [Fact]
    public void test_capacity_is_granted_by_the_world_and_never_falls()
    {
        // Same invariant the whole Unlocks layer runs on: every fact only grows, so a capacity can
        // never go backwards under a player and re-announce itself when it comes back.
        // Swept along each axis on its own, because that is what monotone means: hold one fact and
        // walk the other, and the answer may only ever rise.
        for (var conquests = 0; conquests <= Regions.All.Count; conquests++)
        {
            var last = 0;
            for (var wave = 0; wave <= 100; wave += 5)
            {
                var cap = Unlocks.VowCapacity(new UnlockFacts(DeepestWave: wave, RegionsConquered: conquests));
                Assert.True(cap >= last, $"capacity fell from {last} to {cap} at wave {wave}, {conquests} conquests");
                last = cap;
            }
        }
        for (var wave = 0; wave <= 100; wave += 5)
        {
            var last = 0;
            for (var conquests = 0; conquests <= Regions.All.Count; conquests++)
            {
                var cap = Unlocks.VowCapacity(new UnlockFacts(DeepestWave: wave, RegionsConquered: conquests));
                Assert.True(cap >= last, $"capacity fell from {last} to {cap} at wave {wave}, {conquests} conquests");
                last = cap;
            }
        }

        Assert.Equal(0, Unlocks.VowCapacity(new UnlockFacts(DeepestWave: 4)));
        Assert.Equal(1, Unlocks.VowCapacity(new UnlockFacts(DeepestWave: 5)));
        Assert.Equal(2, Unlocks.VowCapacity(new UnlockFacts(DeepestWave: 5, RegionsConquered: 2)));
        Assert.Equal(3, Unlocks.VowCapacity(new UnlockFacts(DeepestWave: 5, RegionsConquered: 4)));
    }

    [Fact]
    public void test_no_trait_point_is_read_anywhere_in_the_grant()
    {
        // Capacity used to be an accident of the skill-slot count, which the trait tree sold a piece
        // of. Trait points reach none of these three now.
        var rich = new UnlockFacts(DeepestWave: 40, RegionsConquered: 5, TraitsDiscovered: 99);
        var poor = rich with { TraitsDiscovered = 0 };

        Assert.Equal(Unlocks.VowCapacity(rich), Unlocks.VowCapacity(poor));
        Assert.Equal(Unlocks.KeystoneSockets(rich), Unlocks.KeystoneSockets(poor));
        Assert.Equal(Unlocks.SkillSlots(rich), Unlocks.SkillSlots(poor));
    }

    [Fact]
    public void test_swearing_past_capacity_is_refused_below_the_ui()
    {
        var l = new PlayerLoadout { VowCapacity = 1 };
        for (var i = 0; i < 3; i++) { l.AddSkill(); l.SetSkill(i, "hammer_blow"); }
        var known = Vows.Catalog;

        Assert.True(l.SetVow(0, "vow_singular", known));
        // THE SAME PROMISE ON A SECOND SLOT IS STILL ONE PROMISE, so it is always allowed.
        Assert.True(l.SetVow(1, "vow_singular", known));
        // A DIFFERENT one is a second promise, and capacity is one.
        Assert.False(l.SetVow(2, "vow_unguarded", known));

        l.VowCapacity = 2;
        Assert.True(l.SetVow(2, "vow_unguarded", known));
        Assert.Equal(2, l.SwornVows.Count);
    }

    [Fact]
    public void test_a_loadout_starts_at_one_vow_not_at_the_ceiling()
    {
        // THE REGRESSION THIS FILE MISSED ONCE. Every other test here SETS VowCapacity before asserting
        // on it, so the number the type starts at was never looked at — and it was 3, the ceiling. The
        // host raised it with a Math.Max against Unlocks.VowCapacity, so it began at the ceiling and
        // could never come down: a brand-new account could swear three Vows on its first frame and the
        // milestone below granted nothing it did not already have. The two halves were each correct and
        // the wire between them was dead, which is this project's signature bug.
        //
        // One is the floor because one is what the BUILD screen opens with, and at one Vow the sim's
        // numbers are exactly what they were before capacity existed at all.
        Assert.Equal(1, new PlayerLoadout().VowCapacity);

        // And the floor really does bind: a second DIFFERENT vow is refused until the world grants it.
        var l = new PlayerLoadout();
        for (var i = 0; i < 2; i++) { l.AddSkill(); l.SetSkill(i, "hammer_blow"); }
        Assert.True(l.SetVow(0, "vow_singular", Vows.Catalog));
        Assert.False(l.SetVow(1, "vow_unguarded", Vows.Catalog));

        // The milestone is the only thing that lifts it, and it starts below the ceiling.
        Assert.Equal(1, Unlocks.VowCapacity(new UnlockFacts(DeepestWave: 40)));
        Assert.Equal(new PlayerLoadout().VowCapacity, Unlocks.VowCapacity(new UnlockFacts(DeepestWave: 5)));
    }

    [Fact]
    public void test_the_cycler_never_lands_on_a_refusal()
    {
        // The gamepad and keyboard path is cycle-and-confirm, so an option that would be refused must
        // not be in the cycle at all — otherwise the confirm button silently does nothing.
        var l = new PlayerLoadout { VowCapacity = 1 };
        for (var i = 0; i < 2; i++) { l.AddSkill(); l.SetSkill(i, "hammer_blow"); }
        l.SetVow(0, "vow_singular", Vows.Catalog);

        for (var i = 0; i < 20; i++)
        {
            l.CycleVow(1, 1, Vows.Catalog);
            var id = l.Skills[1].VowId;
            Assert.True(id is null || id == "vow_singular",
                $"the cycler offered {id} at capacity 1 with vow_singular already sworn");
        }
    }

    // ── LIVENESS — a Vow that cannot move a real fight is a dial with no consumer ─────────────────

    [Fact]
    public void test_every_vow_moves_a_real_fight_when_its_rule_is_kept()
    {
        // The same bar the variation and reinforcement suites hold, and the Vow catalogue never had it.
        // The static-cost pair are the reason it matters: the sweep once found both of them NET
        // NEGATIVE to swear, which no amount of reading the catalogue would have revealed.
        foreach (var vow in Vows.Catalog)
        {
            var (plainDamage, plainHealth) = Fight(null, vow);
            var (swornDamage, swornHealth) = Fight(vow, vow);

            Assert.True(swornDamage != plainDamage || swornHealth != plainHealth,
                $"{vow.Id} changed neither damage dealt nor health kept in a real fight");
        }
    }

    /// <summary>
    /// One wave, one seed, on a build shaped so <paramref name="shapedFor"/>'s demand is met.
    /// </summary>
    /// <remarks>
    /// The build is shaped for the vow under test rather than the vow being tested against a fixed
    /// build, because a Vow whose demand is BROKEN correctly pays nothing — measuring that would prove
    /// only that the fixture could not pose the condition, which is how a bench manufactures a fault.
    /// </remarks>
    private static (float Damage, int Health) Fight(Vow? sworn, Vow shapedFor)
    {
        var hunter = new Hunter();
        var build = new Build { SlotCapacity = 1 };
        build.Equip(TestBuilds.Skill("hammer_blow", Source.Body, sworn));

        // THE TWO CADENCE DEMANDS ARE THE ONLY ONES A PLAIN BUILD CANNOT POSE, so the fixture buys the
        // skill rate they ask for — measured against the build's own rate rather than assumed, because
        // a bench that cannot pose the condition manufactures a fault and blames the content for it.
        if (shapedFor.Demand is VowDemand.CadenceAtOrBelow or VowDemand.CadenceAtOrAbove)
        {
            var rate = SoloBattle.DescribeBuild(build, hunter).SkillRate;
            var wanted = shapedFor.Demand == VowDemand.CadenceAtOrAbove
                ? shapedFor.Threshold + 0.05f : shapedFor.Threshold - 0.05f;
            build.Shape = new SkillShape { SkillRate = wanted / rate };
        }

        Assert.True(Vows.IsActive(shapedFor, SoloBattle.DescribeBuild(build, hunter)),
            $"the fixture cannot pose {shapedFor.Id}'s condition — measure the bench, not the content");

        var champion = new Champion
        {
            MaxHealth = SoloBattle.ChampionHealth(build, hunter, ExpeditionTuning.Default.ChampionPoolScale),
        };
        champion.Health = champion.MaxHealth;

        var creatures = new List<WaveCreature>
        {
            new() { MaxHealth = 5_000_000, Health = 5_000_000, Damage = 6f },
        };
        var metrics = new WaveMetrics();
        SoloBattle.ResolveWave(champion, build, hunter, creatures, enemyIntervalMs: 700,
            ExpeditionTuning.Default, new Random(11), metrics: metrics);
        return (metrics.DeliveredDamage, champion.Health);
    }
}
