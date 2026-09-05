using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Warrens;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// THE OFFLINE ECONOMY, MEASURED — four representative accounts, what an hour of play pays them, what
/// an absence pays them, and what the things they would spend it on cost.
/// </summary>
/// <remarks>
/// A benchmark, not a rule: it prints the numbers the offline cap (<see cref="OfflineCamp"/>) was
/// chosen from, so the next retune starts from measurements rather than from one multiplier. The
/// assertions are the law the numbers must keep: no absence pays more than the same hours of play,
/// and a fresh account's absence is bounded.
/// </remarks>
public class offline_economy_benchmark_test
{
    private readonly ITestOutputHelper _out;
    public offline_economy_benchmark_test(ITestOutputHelper output) => _out = output;

    /// <param name="Conquered">Regions taken — what the Warren has unlocked.</param>
    /// <param name="FacilityLevel">The level each facility is bought to, held to what the profile's own depth allows.</param>
    public sealed record Profile(string Name, Hunter Hunter, Build Build, int RegionIndex, int Progression, int Conquered, int FacilityLevel)
    {
        /// <summary>The Warren this account could actually have: facility levels capped by its deepest wave (5 waves a level).</summary>
        public Warren WarrenFor(int deepestWave)
            => WarrenAt(Conquered, Math.Min(FacilityLevel, Math.Max(1, Warren.CapForDepth(deepestWave))));
    }

    private static Build StarterBuild(string characterId)
    {
        var c = CharacterRoster.Get(characterId);
        return PlayerLoadout.Starter(c).ToBuild(new MasteryTree(), c, new SkillProgress(), Array.Empty<Keystone>(), Array.Empty<Vow>());
    }

    private static Hunter Trained(int ranksEach, params HunterStat[] stats)
    {
        var h = new Hunter();
        foreach (var s in stats)
            for (var i = 0; i < ranksEach; i++)
            {
                h.AddGleam(h.NextRankCost(s));
                Assert.True(h.Train(s));
            }
        return h;
    }

    private static Warren WarrenAt(int conquered, int levelEach)
    {
        var w = new Warren { ConqueredRegions = conquered, FacilityLevelCap = int.MaxValue };
        foreach (var f in Facilities.All)
            if (w.IsUnlocked(f.Kind))
                for (var i = 1; i < levelEach; i++) w.Upgrade(f.Kind);
        return w;
    }

    // Each profile hunts a region it can actually clear (deepest wave ≥ 10 in the printed hour), so the
    // comparison is between an absence and play the SAME account would do — not a wall it cannot pass.
    public static IEnumerable<Profile> Profiles()
    {
        yield return new("FRESH", new Hunter(), StarterBuild("seeker"), 0, 0, 0, 1);
        yield return new("EARLY (1 conquest, 10 ranks)", Trained(10, HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Vitality),
                         StarterBuild("seeker"), 0, 1, 1, 3);
        yield return new("MID (3 conquests, 20 ranks)", Trained(20, HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Vitality, HunterStat.Defense, HunterStat.ResonanceAffinity, HunterStat.Focus),
                         StarterBuild("seeker"), 1, 1, 3, 8);
        yield return new("DEVELOPED (5 conquests, 40 ranks)", Trained(40, HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Vitality, HunterStat.Defense, HunterStat.ResonanceAffinity, HunterStat.Focus),
                         StarterBuild("seeker"), 2, 1, 5, 20);
    }

    private static (float Health, float Damage) Baseline(int regionIndex, int prog)
    {
        var region = Regions.All[Math.Min(regionIndex, Regions.All.Count - 1)];
        var mod = RegionModifiers.For(region.Id);
        return (110f * (1f + 0.35f * prog) * RegionLadder.Health(regionIndex) * mod.EnemyHealthMult,
                9f * (1f + 0.20f * prog) * RegionLadder.Damage(regionIndex) * mod.EnemyDamageMult);
    }

    private static OfflineHunt.Result Away(Profile p, double seconds)
    {
        var region = Regions.All[Math.Min(p.RegionIndex, Regions.All.Count - 1)];
        var (hp, dmg) = Baseline(p.RegionIndex, p.Progression);
        return OfflineHunt.Simulate(p.Build, p.Hunter, seconds, region.Id, region.CombatBias, hp, dmg, seed: 7);
    }

    [Fact]
    public void test_offline_economy_benchmark_prints_and_holds_the_law()
    {
        foreach (var p in Profiles())
        {
            var hour = Away(p, 3600);
            var activePerHour = hour.GleamPerSecond * 3600;
            var warren = p.WarrenFor(hour.DeepestWave);
            var open = p.Conquered >= 1;
            _out.WriteLine($"== {p.Name}: region {p.RegionIndex} prog {p.Progression}; active ≈ {activePerHour:N0} gleam/h (deepest {hour.DeepestWave}, {hour.WavesCleared} waves, fell {hour.Falls})");
            _out.WriteLine($"   camp: holds {OfflineCamp.HoursText(OfflineCamp.HoursFor(open ? warren : null))} at {OfflineCamp.EfficiencyFor(open ? warren : null):P0} of live pay; warren {(open ? "open" : "closed")}, facility levels {OfflineCamp.FacilityLevels(warren)} (depth cap {Warren.CapForDepth(hour.DeepestWave)})");
            foreach (var hours in new[] { 1, 2, 4, 8, 12, 24 })
            {
                var before = Away(p, hours * 3600);                              // the old credit: the whole absence at the haircut
                var held = OfflineCamp.CreditedSeconds(hours * 3600, open ? warren : null);
                var r = Away(p, held);
                var w = open ? warren.Tick((float)held) : default;
                _out.WriteLine($"   away {hours,2}h: hunt was +{before.Gleam:N0} → now +{OfflineCamp.HuntCredit(r, open ? warren : null, hours * 3600):N0} gleam; warren +{w.Gleam:N0} gleam +{w.Dust:N0} dust +{w.Scrap:N0} scrap +{w.Essence:N0} essence");
            }
            var costs = string.Join(" · ", new[] { HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Defense }
                .Select(s => $"{s} next {p.Hunter.NextRankCost(s):N0}"));
            _out.WriteLine($"   training: {costs}; five more ranks of ATTACK ≈ {FiveRanks(p.Hunter, HunterStat.AttackPower):N0}");
            var up = warren.AllFacilities.Where(f => warren.IsUnlocked(f.Kind)).Select(f => warren.UpgradeCost(f.Kind)).ToList();
            _out.WriteLine($"   warren: level {warren.Level}, next upgrades from {up.Min(c => c.Gleam):N0} gleam");

            // THE LAW: an absence never pays more than the hours it holds of the same play would.
            var dayHeld = OfflineCamp.CreditedSeconds(24 * 3600, open ? warren : null);
            var day = Away(p, dayHeld);
            Assert.True(OfflineCamp.HuntCredit(day, open ? warren : null, 24 * 3600) <= day.GleamPerSecond * dayHeld + 1,
                        $"{p.Name}: a day away out-earned the play it stands for");
        }
    }

    private static long FiveRanks(Hunter h, HunterStat s)
    {
        long sum = 0;
        var probe = new Hunter();
        for (var i = 0; i < h.RankOf(s); i++) { probe.AddGleam(probe.NextRankCost(s)); probe.Train(s); }
        for (var i = 0; i < 5; i++) { var c = probe.NextRankCost(s); sum += c; probe.AddGleam(c); probe.Train(s); }
        return sum;
    }
}
