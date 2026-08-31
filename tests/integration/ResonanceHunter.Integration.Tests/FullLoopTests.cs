using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Forging;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Persistence;
using Xunit;

namespace ResonanceHunter.Integration.Tests;

/// <summary>
/// The whole game, end to end: loot, forge, train, master, save, quit, return.
/// </summary>
/// <remarks>
/// Unit tests prove each system is internally correct. They cannot prove the systems FIT — and the two
/// worst bugs in this project's history were both integration failures that every unit test happily passed
/// (the efficiency contract inverted across two systems; a truncation at a tick-rate boundary silenced a
/// whole subsystem).
///
/// The COMBAT step is driven through the live loot faucet (LootSystem.Roll) rather than a battle sim —
/// the fight itself is proven in Builds/SoloBattleTests + SoloExpeditionTests, and what this loop cares
/// about is the ECONOMY chain: loot -> forge -> train -> region mastery -> save. (The hatch/farm steps
/// this loop used to walk retired with the creature subsystem, 2026-08-24.)
/// </remarks>
public class FullLoopTests
{
    private static readonly Random Rng = new(20260714);

    [Fact]
    public void test_a_full_session_produces_a_stronger_hunter_and_a_mastered_region()
    {
        var hunter = new Hunter();
        var region = new Region(VerdantHollow.RegionId, parClearTimeSeconds: 15);
        var inventory = new List<ItemInstance>();
        var powerTier = VerdantHollow.Templates.First(t => !t.IsBoss).PowerTierBase;

        // ── 1. Hunt. Twenty kills' worth of loot from the live roller. ────────────────────────────
        for (var i = 0; i < 20; i++)
        {
            var loot = LootSystem.Roll(
                new KillContext { PowerTier = powerTier, LootTiltPercent = 20f }, Rng, LootTuning.Default);

            inventory.AddRange(loot);
            region.RecordActiveKill();
        }

        Assert.NotEmpty(inventory);

        // ── 2. Forge. Sell everything sellable. ───────────────────────────────────────────────
        var earned = inventory.Where(Forge.IsEligible).Sum(i => i.SellValue);
        hunter.AddGleam(earned);
        Assert.True(hunter.Gleam > 0, "Twenty kills must be worth something.");

        // ── 3. Train. The live damage lever actually moves. ───────────────────────────────────
        //
        // AutoDamageMultiplier, not SquadDamageMultiplier. This step had been failing since the
        // MIGHT/RESONANCE split of 2026-08-26: SquadDamageMultiplier is the WORN gear's product
        // (weapon × affixes × charm) and MIGHT left it that day for the basic attack's own channel,
        // so the loop was training AttackPower and then asserting against a number AttackPower no
        // longer touches. The chain was never broken; the test was reading the wrong end of it.
        var damageBefore = hunter.AutoDamageMultiplier;

        while (hunter.CanTrain(HunterStat.AttackPower) && hunter.RankOf(HunterStat.AttackPower) < 10)
            hunter.Train(HunterStat.AttackPower);

        Assert.True(hunter.RankOf(HunterStat.AttackPower) > 0,
            "Twenty kills' loot did not fund a single rank — the loot -> forge -> train chain is broken "
            + "before the damage assertion below can say anything.");

        var damageAfter = hunter.AutoDamageMultiplier;
        Assert.True(damageAfter > damageBefore, "Training must change what the fight deals.");

        // ── 4. Mastery. The kills the champion landed built the region's record. ──────────────
        Assert.True(region.RegionMasteryPoints > 0, "Active kills must accrue region mastery.");
        Assert.True(region.RecordDepth(12), "Reaching a new depth must set the record.");
        Assert.Equal(12, region.BestDepth);

        // ── 5. Save, quit, come back two hours later. ─────────────────────────────────────────
        var now = 1_700_000_000_000L;
        var json = SaveSystem.Serialize(SaveSystem.Capture(hunter, region, inventory, now));

        var loaded = SaveSystem.Deserialize(json, now + 2 * 3600 * 1000L);
        Assert.True(loaded.Ok);
        Assert.Equal(7200.0, loaded.OfflineSeconds, precision: 0);
        Assert.Equal(region.RegionMasteryPoints, loaded.Save!.RegionMasteryPoints, precision: 1);

        var restoredHunter = new Hunter();
        SaveSystem.RestoreHunter(loaded.Save!, restoredHunter);

        Assert.Equal(hunter.AttackPower, restoredHunter.AttackPower);
        Assert.Equal(hunter.Gleam, restoredHunter.Gleam);
    }

    /// <summary>Idle can reach every rarity. It is never locked out of progression.</summary>
    [Fact]
    public void test_a_purely_idle_player_is_never_locked_out_of_anything()
    {
        var rng = new Random(7);
        var seenRarities = new HashSet<Rarity>();

        // The most throttled loot context there is: low tier, no tilt.
        for (var i = 0; i < 40_000; i++)
        {
            var loot = LootSystem.Roll(new KillContext
            {
                PowerTier = 1,
                LootTiltPercent = 0f,
                AutomationStage = 1,
            }, rng, LootTuning.Default);

            foreach (var item in loot) seenRarities.Add(item.Rarity);
        }

        Assert.Equal(5, seenRarities.Count);                          // every rarity is reachable
        Assert.Contains(Rarity.Legendary, seenRarities);
    }
}
