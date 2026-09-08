using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Forging;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Warrens;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Expeditions;

/// <summary>
/// THE ECONOMY, MEASURED — four representative accounts: what an hour of play pays them, what the
/// Warren pays beside it, what an absence holds, and what a training rank costs in minutes of play.
/// </summary>
/// <remarks>
/// <para>
/// A benchmark AND the law. It prints the table the Warren budget (<see cref="WarrenBudget"/>) and the
/// camp (<see cref="OfflineCamp"/>) were calibrated from, so the next retune starts from measurements;
/// and it asserts what those numbers must keep: the Warren is subordinate to the hunt, an absence is
/// capped, a level bought visibly raises passive pay, and training stays affordable early without
/// becoming trivial in the middle.
/// </para>
/// <para>
/// <b>BEFORE (2026-09-06, the flat-rate Warren, the same profiles' Warrens):</b> Warren Gleam an hour was
/// EARLY 7,920 · MID 9,360 · DEVELOPED 22,260 against measured active rates of 5,683 · 3,510 · 4,861 on
/// the old starter-build profiles — 1.4×, 2.7× and 4.6× the hunt. The profiles now carry the build and
/// gear an account of that age actually has, and the Warren reads a budget instead.
/// </para>
/// </remarks>
public class offline_economy_benchmark_test
{
    private readonly ITestOutputHelper _out;
    public offline_economy_benchmark_test(ITestOutputHelper output) => _out = output;

    /// <param name="Conquered">Regions taken — what the Warren has unlocked and the budget's band.</param>
    /// <param name="FacilityLevel">The level each facility is bought to, held to what the profile's own depth allows.</param>
    /// <param name="BeforeWarrenGleamPerHour">What the flat-rate Warren paid this profile an hour (measured before the budget).</param>
    public sealed record Profile(string Name, Hunter Hunter, Build Build, int RegionIndex, int Progression, int Conquered,
                                 int FacilityLevel, long BeforeWarrenGleamPerHour)
    {
        public Warren WarrenFor(int deepestWave)
            => WarrenAt(Conquered, Math.Min(FacilityLevel, Math.Max(1, Warren.CapForDepth(deepestWave))));
    }

    /// <summary>The skills a woven account fights with — three actives and the MIRE field.</summary>
    private static readonly string[] Quartet = { "hammer_blow", "volley_spray", "field_mire", "sign_call" };

    private static Build StarterBuild(string characterId)
    {
        var c = CharacterRoster.Get(characterId);
        return PlayerLoadout.Starter(c).ToBuild(new MasteryTree(), c, new SkillProgress(), Array.Empty<Keystone>(), Array.Empty<Vow>());
    }

    /// <summary>A woven, geared build: the quartet, with the gear multipliers an account of that age wears.</summary>
    private static Build WovenBuild(float damage, float health)
    {
        var b = new Build { PassiveMods = new BuildMods(damage, health, 1f, 1f, 1f) };
        foreach (var id in Quartet) b.Equip(TestBuilds.Skill(id, Source.Spirit));
        return b;
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

    private static readonly HunterStat[] Six =
    {
        HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Vitality,
        HunterStat.Defense, HunterStat.ResonanceAffinity, HunterStat.Focus,
    };

    // Each profile hunts a region it can actually clear, with the build and gear an account of that age
    // has — a starter build in the third region is not a developed account, it is a wall.
    public static IEnumerable<Profile> Profiles()
    {
        yield return new("FRESH", new Hunter(), StarterBuild("seeker"), 0, 0, 0, 1, 0);
        yield return new("EARLY (1 conquest, 10 ranks)", Trained(10, HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Vitality),
                         WovenBuild(1.2f, 1.1f), 0, 1, 1, 3, 7_920);
        yield return new("MID (3 conquests, 20 ranks)", Trained(20, Six), WovenBuild(1.8f, 1.25f), 1, 1, 3, 8, 9_360);
        yield return new("DEVELOPED (5 conquests, 40 ranks)", Trained(40, Six), WovenBuild(3.0f, 1.4f), 2, 1, 5, 20, 22_260);
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

    /// <summary>How many ranks of a stat the purse of so many minutes of play buys, from the hunter's current rank.</summary>
    private static int RanksFor(Hunter h, HunterStat s, double gleam)
    {
        var probe = new Hunter();
        for (var i = 0; i < h.RankOf(s); i++) { probe.AddGleam(probe.NextRankCost(s)); probe.Train(s); }
        var ranks = 0;
        while (gleam >= probe.NextRankCost(s))
        {
            gleam -= probe.NextRankCost(s);
            probe.AddGleam(probe.NextRankCost(s));
            probe.Train(s);
            ranks++;
        }
        return ranks;
    }

    [Fact]
    public void test_economy_benchmark_prints_and_holds_the_law()
    {
        _out.WriteLine($"{"PROFILE",-36} {"HUNT/h",9} {"WARREN/h",9} {"BEFORE",9} {"RATIO",6} {"CAP",8} {"EFF",5} {"2h AWAY",14} {"8h AWAY",14} {"RANK",7} {"MIN",5} {"10m",4} {"30m",4}");
        foreach (var p in Profiles())
        {
            var hour = Away(p, 3600);
            var activePerHour = hour.GleamPerSecond * 3600;
            var warren = p.WarrenFor(hour.DeepestWave);
            var open = p.Conquered >= 1;
            var camp = open ? warren : null;
            var warrenPerHour = open ? warren.ProductionPerMinute(WarrenResource.Gleam) * 60.0 : 0.0;
            var ratio = warrenPerHour / Math.Max(1.0, activePerHour);

            string Absence(int hours)
            {
                var held = OfflineCamp.CreditedSeconds(hours * 3600, camp);
                var r = Away(p, held);
                var w = open ? WarrenAt(p.Conquered, Math.Min(p.FacilityLevel, Math.Max(1, Warren.CapForDepth(hour.DeepestWave)))).Tick((float)held) : default;
                return $"{OfflineCamp.HuntCredit(r, camp, hours * 3600):N0}+{w.Gleam:N0}w";
            }

            var rankCost = p.Hunter.NextRankCost(HunterStat.AttackPower);
            var minutesForRank = rankCost / Math.Max(1.0, activePerHour) * 60.0;
            var ranks10 = RanksFor(p.Hunter, HunterStat.AttackPower, activePerHour / 6.0);
            var ranks30 = RanksFor(p.Hunter, HunterStat.AttackPower, activePerHour / 2.0);
            _out.WriteLine($"{p.Name,-36} {activePerHour,9:N0} {warrenPerHour,9:N0} {p.BeforeWarrenGleamPerHour,9:N0} {ratio,6:P0} "
                           + $"{OfflineCamp.HoursText(OfflineCamp.HoursFor(camp)),8} {OfflineCamp.EfficiencyFor(camp),5:P0} {Absence(2),14} {Absence(8),14} "
                           + $"{rankCost,7:N0} {minutesForRank,5:N1} {ranks10,4} {ranks30,4}");
            _out.WriteLine($"{"",-36} region {p.RegionIndex} prog {p.Progression}, deepest {hour.DeepestWave}, {hour.WavesCleared} waves, fell {hour.Falls}; "
                           + $"warren levels {warren.FacilityLevelsBought}, share {warren.Share:P0} of a {warren.HuntGleamPerHour:N0}/h baseline; "
                           + $"dust {warren.ProductionPerMinute(WarrenResource.Dust) * 60:N0}/h scrap {warren.ProductionPerMinute(WarrenResource.Scrap) * 60:N0}/h essence {warren.ProductionPerMinute(WarrenResource.Essence) * 60:N0}/h");
            // THE MATERIALS, AGAINST WHAT THEY BUY (playtest debt, 2026-09-06 — recorded, not tuned): an hour of
            // the Warren's Dust, Scrap and Essence beside the representative sinks at this progression. The
            // weights and exchange rates in WarrenBudget came from prices; the decision to move them waits for play.
            var dustH = warren.ProductionPerMinute(WarrenResource.Dust) * 60f;
            var scrapH = warren.ProductionPerMinute(WarrenResource.Scrap) * 60f;
            var essenceH = warren.ProductionPerMinute(WarrenResource.Essence) * 60f;
            var nextLevelDust = warren.UpgradeCost(FacilityKind.Nursery).Dust;
            var skipTenWaves = Checkpoints.DustPerWave * 10;
            var refineScrap = (ForgeTuning.Default.RefineScrapBase + Math.Max(1, hour.DeepestWave)) * 4 / 4;   // a first refine at the depth's item level
            var socketEssence = GemCraft.SocketCost(Rarity.Rare);
            string Hours(float perHour, int cost) => perHour <= 0f ? "—" : $"{cost / perHour:0.0}h";
            _out.WriteLine($"{"",-36} materials/h: dust {dustH:N0} (next facility level {nextLevelDust} = {Hours(dustH, nextLevelDust)}, skip 10 waves {skipTenWaves} = {Hours(dustH, skipTenWaves)}) · "
                           + $"scrap {scrapH:N0} (a refine ≈ {refineScrap} = {Hours(scrapH, refineScrap)}) · essence {essenceH:N0} (a rare socket {socketEssence} = {Hours(essenceH, socketEssence)})");

            // ── THE LAW ──
            // 1. The Warren is subordinate to the hunt: never past the hard cap of the MEASURED rate.
            if (open)
                Assert.True(warrenPerHour <= WarrenBudget.HardCap * activePerHour,
                            $"{p.Name}: the Warren pays {warrenPerHour:N0}/h against a hunt of {activePerHour:N0}/h — {ratio:P0}, past the {WarrenBudget.HardCap:P0} cap");
            // 2. And not meaningless: a Warren that is open pays at least a tenth of the hunt.
            if (open)
                Assert.True(warrenPerHour >= 0.10 * activePerHour, $"{p.Name}: the Warren pays only {ratio:P0} of the hunt");
            // 3. The baseline the Warren reads never claims more than the hunt really earns.
            if (open)
                Assert.True(warren.HuntGleamPerHour <= activePerHour * 1.15,
                            $"{p.Name}: the budget's baseline ({warren.HuntGleamPerHour:N0}/h) is above the measured hunt ({activePerHour:N0}/h)");
            // 4. An absence is capped: a day away pays the camp's hours, not the day.
            var dayHeld = OfflineCamp.CreditedSeconds(24 * 3600, camp);
            Assert.True(dayHeld <= OfflineCamp.HoursFor(camp) * 3600 + 1, $"{p.Name}: the camp held more than its hours");
            var day = Away(p, dayHeld);
            Assert.True(OfflineCamp.HuntCredit(day, camp, 24 * 3600) <= day.GleamPerSecond * dayHeld + 1,
                        $"{p.Name}: a day away out-earned the play it stands for");
            // 5. A level bought raises passive pay — the share climbs with every level, by less each time,
            //    and the camp's hours climb with it (the other half of a level's worth).
            if (open)
            {
                Assert.True(WarrenBudget.Share(warren.FacilityLevelsBought + 1) > warren.Share, $"{p.Name}: one more level pays nothing more");
                Assert.True(OfflineCamp.HoursAfterOneMoreLevel(warren) > OfflineCamp.HoursFor(warren), $"{p.Name}: one more level holds nothing more");
            }
            // 6. Training: the first ranks are cheap in minutes; the middle is not a shopping spree.
            if (p.Name == "FRESH")
                Assert.True(minutesForRank <= 3.0, $"a fresh hunter's next rank takes {minutesForRank:N1} minutes of play");
            if (p.Conquered >= 3)
                Assert.True(ranks30 <= 12, $"{p.Name}: thirty minutes of play buys {ranks30} ranks of ATTACK — the board is a shopping list");
        }
    }
}
