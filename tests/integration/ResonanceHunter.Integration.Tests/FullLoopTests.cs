using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using ResonanceHunter.Core.Progression;
using Xunit;

namespace ResonanceHunter.Integration.Tests;

/// <summary>
/// The whole game, end to end: loot, forge, train, hatch, farm, save, quit, return.
/// </summary>
/// <remarks>
/// Unit tests prove each system is internally correct. They cannot prove the systems FIT — and the two
/// worst bugs in this project's history were both integration failures that every unit test happily passed:
/// the efficiency contract inverted across two systems, and the automated farm produced nothing because of a
/// truncation at the boundary between tick rate and clear time.
///
/// The COMBAT step is driven through the live loot faucet (LootSystem.Roll) rather than the retired
/// manual-combat Encounter — the fight itself is proven in Builds/SoloBattleTests + SoloExpeditionTests, and
/// what this loop cares about is the ECONOMY chain: loot -> forge -> train -> hatch -> farm -> save.
/// </remarks>
public class FullLoopTests
{
    private static readonly Random Rng = new(20260714);

    [Fact]
    public void test_a_full_session_produces_a_stronger_hunter_and_a_working_farm()
    {
        var hunter = new Hunter();
        var region = new Region(VerdantHollow.RegionId, parClearTimeSeconds: 15);
        var inventory = new List<ItemInstance>();
        var cores = 0;
        var powerTier = VerdantHollow.Templates.First(t => !t.IsBoss).PowerTierBase;

        // ── 1. Hunt. Twenty kills' worth of loot from the live roller. ────────────────────────────
        for (var i = 0; i < 20; i++)
        {
            var loot = LootSystem.Roll(
                new KillContext { PowerTier = powerTier, LootTiltPercent = 20f }, Rng, LootTuning.Default);

            cores += loot.Count(l => l.BaseType == ItemBaseType.CreatureCore);
            inventory.AddRange(loot.Where(l => l.BaseType != ItemBaseType.CreatureCore));

            region.RecordActiveKill();
        }

        Assert.Equal(20, cores);                      // every kill gives exactly one core
        Assert.NotEmpty(inventory);

        // ── 2. Forge. Sell everything. ────────────────────────────────────────────────────────
        var earned = inventory.Where(Forge.IsEligible).Sum(i => i.SellValue);
        hunter.AddGleam(earned);
        Assert.True(hunter.Gleam > 0, "Twenty kills must be worth something.");

        // ── 3. Train. The live damage lever actually moves. ───────────────────────────────────
        var damageBefore = hunter.SquadDamageMultiplier;

        while (hunter.CanTrain(HunterStat.AttackPower) && hunter.RankOf(HunterStat.AttackPower) < 10)
            hunter.Train(HunterStat.AttackPower);

        var damageAfter = hunter.SquadDamageMultiplier;
        Assert.True(damageAfter > damageBefore, "Training must change what the fight deals.");

        // ── 4. Hatch a farm team from the cores. ──────────────────────────────────────────────
        var roster = new List<Creature>
        {
            Creature.Hatch("a", Source.Nature, Role.Attacker, 6),
            Creature.Hatch("c", Source.Nature, Role.Crafter, 5),
            Creature.Hatch("d", Source.Nature, Role.Defender, 5),
            Creature.Hatch("s", Source.Nature, Role.Support, 5),
        };
        Assert.True(cores >= roster.Count, "There must be enough cores to build a team.");

        foreach (var c in roster) region.Assign(c);
        region.AutomationStage = 3;

        // ── 5. Farm for six hours. Automation is earned, and it works. ────────────────────────
        var farmed = region.Tick(3600f * 6f, gleamPerKill: 8);

        Assert.True(farmed.Kills > 0, "The farm must actually kill things.");
        Assert.Equal(MasteryLevel.OptimizedTeam, region.MasteryLevel);
        Assert.True(region.IdleEfficiencyPercent() > 100f, "An optimized farm should exceed par.");

        // ── 6. Save, quit, come back two hours later. ─────────────────────────────────────────
        var now = 1_700_000_000_000L;
        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, roster, inventory, cores, now));

        var loaded = SaveSystem.Deserialize(json, now + 2 * 3600 * 1000L);
        Assert.True(loaded.Ok);
        Assert.Equal(7200.0, loaded.OfflineSeconds, precision: 0);

        var restoredHunter = new Hunter();
        SaveSystem.RestoreHunter(loaded.Save!, restoredHunter);

        Assert.Equal(hunter.AttackPower, restoredHunter.AttackPower);
        Assert.Equal(hunter.Gleam, restoredHunter.Gleam);
    }

    /// <summary>Idle can reach every rarity and every core. It is never locked out of progression.</summary>
    [Fact]
    public void test_a_purely_idle_player_is_never_locked_out_of_anything()
    {
        var rng = new Random(7);
        var seenRarities = new HashSet<Rarity>();
        var cores = 0;

        // The most throttled automation there is: Stage 1 parity, low tier, no tilt.
        for (var i = 0; i < 40_000; i++)
        {
            var loot = LootSystem.Roll(new KillContext
            {
                PowerTier = 1,
                LootTiltPercent = 0f,
                AutomationStage = 1,
            }, rng, LootTuning.Default);

            cores += loot.Count(l => l.BaseType == ItemBaseType.CreatureCore);
            foreach (var item in loot) seenRarities.Add(item.Rarity);
        }

        Assert.Equal(40_000, cores);                                  // a core on EVERY kill
        Assert.Equal(5, seenRarities.Count);                          // and every rarity is reachable
        Assert.Contains(Rarity.Legendary, seenRarities);
    }
}
