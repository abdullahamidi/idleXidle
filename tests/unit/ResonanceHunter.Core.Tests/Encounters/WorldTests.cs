using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Combat;
using ResonanceHunter.Core.Encounters;
using Xunit;

namespace ResonanceHunter.Core.Tests.Encounters;

public class WorldTests
{
    [Fact]
    public void test_the_world_has_a_chain_of_regions()
    {
        Assert.True(Regions.All.Count >= 3);
        Assert.Equal(VerdantHollow.RegionId, Regions.All[0].Id);
        Assert.All(Regions.All, r => Assert.Contains(r.Templates, t => t.IsBoss));
    }

    /// <summary>Only the first region is open at the start; the rest are locked behind their prereqs.</summary>
    [Fact]
    public void test_an_unknown_region_id_is_not_unlocked_rather_than_a_crash()
    {
        // REGRESSION: IsUnlocked went through Regions.Get (a throwing .First), so a saved ActiveRegion whose
        // id was renamed/removed in an update crashed the LOAD instead of falling back to the home region.
        var world = new World();
        Assert.False(world.IsUnlocked("a_region_that_no_longer_exists"));
    }

    [Fact]
    public void test_only_the_starting_region_is_unlocked_at_first()
    {
        var world = new World();

        Assert.True(world.IsUnlocked(VerdantHollow.RegionId));
        Assert.False(world.IsUnlocked("cinderworks"));
        Assert.False(world.IsUnlocked("umbral_reach"));
    }

    /// <summary>Conquering a region's boss unlocks the next region — the progression spine.</summary>
    [Fact]
    public void test_conquering_a_region_unlocks_the_next()
    {
        var world = new World();

        var unlocked = world.Conquer(VerdantHollow.RegionId);

        Assert.NotNull(unlocked);
        Assert.Equal("cinderworks", unlocked!.Id);
        Assert.True(world.IsConquered(VerdantHollow.RegionId));
        Assert.True(world.IsUnlocked("cinderworks"));
        Assert.False(world.IsUnlocked("umbral_reach")); // still gated on cinderworks
    }

    [Fact]
    public void test_the_whole_chain_can_be_conquered_in_order()
    {
        var world = new World();
        foreach (var r in Regions.All)
        {
            Assert.True(world.IsUnlocked(r.Id), $"{r.Id} should be unlocked by the time we reach it.");
            world.Conquer(r.Id);
        }
        Assert.All(Regions.All, r => Assert.True(world.IsConquered(r.Id)));
    }

    [Fact]
    public void test_conquering_the_last_region_unlocks_nothing()
    {
        var world = new World();
        foreach (var r in Regions.All.Take(Regions.All.Count - 1)) world.Conquer(r.Id);

        var unlocked = world.Conquer(Regions.All[^1].Id);
        Assert.Null(unlocked);
    }

    /// <summary>Each region has its own farm — you can master and staff them independently.</summary>
    [Fact]
    public void test_each_region_has_its_own_independent_farm()
    {
        var world = new World();
        var home = world.RegionFarm(VerdantHollow.RegionId);
        var cinder = world.RegionFarm("cinderworks");

        Assert.NotSame(home, cinder);
        home.Assign(Creature.Hatch("a", Source.Nature, Role.Attacker, 5));
        Assert.Single(home.Team);
        Assert.Empty(cinder.Team); // assigning to one does not touch the other
    }

    /// <summary>
    /// Two staffed region farms both produce when ticked — the payoff of conquest is that every
    /// region you own farms at once, not just the one you are standing in.
    /// </summary>
    [Fact]
    public void test_two_staffed_regions_both_produce_when_ticked()
    {
        var world = new World();
        var home = world.RegionFarm(VerdantHollow.RegionId);
        var cinder = world.RegionFarm("cinderworks");

        // Stage 3 = fully automated (kills, collects cores, and auto-sells for Gleam).
        home.AutomationStage = 3;
        cinder.AutomationStage = 3;
        home.Assign(Creature.Hatch("a", Source.Nature, Role.Attacker, 10));
        cinder.Assign(Creature.Hatch("b", Source.Machine, Role.Attacker, 10));

        var homeYield = home.Tick(3600f, gleamPerKill: 8);
        var cinderYield = cinder.Tick(3600f, gleamPerKill: 8);

        Assert.True(homeYield.Kills > 0 && homeYield.GleamRealized > 0);
        Assert.True(cinderYield.Kills > 0 && cinderYield.GleamRealized > 0);
    }

    /// <summary>An unstaffed region farm yields nothing — the caller can safely skip it.</summary>
    [Fact]
    public void test_an_unstaffed_region_produces_nothing()
    {
        var world = new World();
        var yield = world.RegionFarm("cinderworks").Tick(3600f, gleamPerKill: 8);

        Assert.Equal(0, yield.GleamRealized);
        Assert.Equal(0, yield.Kills);
    }

    /// <summary>Conquered state restores from a save.</summary>
    [Fact]
    public void test_conquered_state_restores()
    {
        var world = new World();
        world.RestoreConquered(new[] { VerdantHollow.RegionId, "cinderworks" });

        Assert.True(world.IsConquered(VerdantHollow.RegionId));
        Assert.True(world.IsUnlocked("umbral_reach")); // cinderworks conquered -> umbral open
        Assert.False(world.IsConquered("umbral_reach"));
    }

    /// <summary>Later regions are harder — higher tiers and longer par times than the start.</summary>
    [Fact]
    public void test_later_regions_are_tougher_than_earlier_ones()
    {
        var verdant = Regions.Get(VerdantHollow.RegionId).Boss;
        var umbral = Regions.Get("umbral_reach").Boss;

        Assert.True(umbral.PowerTierBase > verdant.PowerTierBase);
        Assert.True(umbral.ParClearTimeSeconds > verdant.ParClearTimeSeconds);
    }

    // Removed test_no_region_boss_is_rare_eligible: RareEligible was a capture-system property, and capture
    // is gone with the manual-combat model.

    /// <summary>The regions declare distinct combat biases, so they fight differently, not just look so.</summary>
    [Fact]
    public void test_regions_declare_distinct_combat_biases()
    {
        Assert.Equal(AttackBias.Balanced, Regions.Get(VerdantHollow.RegionId).CombatBias);
        Assert.Equal(AttackBias.Heavy, Regions.Get("cinderworks").CombatBias);
        Assert.Equal(AttackBias.Fast, Regions.Get("umbral_reach").CombatBias);
    }

    /// <summary>Verdant Hollow's standard encounters draw from more than one creature, for variety.</summary>
    [Fact]
    public void test_verdant_hollow_offers_creature_variety()
    {
        var standard = VerdantHollow.Templates.Where(t => !t.IsBoss).ToList();
        // At least one slot must roll between multiple creatures — otherwise every hunt looks identical.
        Assert.Contains(standard, t => t.CreaturePool.Count > 1);

        // Every creature in every pool is a real, distinct id (no accidental duplicates).
        var ids = standard.SelectMany(t => t.CreaturePool).Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ── Corruption endgame ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_corruption_cannot_be_deepened_until_the_whole_world_is_conquered()
    {
        var world = new World();
        Assert.False(world.CanDeepenCorruption);

        world.DeepenCorruption(); // a no-op while regions remain
        Assert.Equal(0, world.CorruptionTier);

        foreach (var r in Regions.All) world.Conquer(r.Id);
        Assert.True(world.CanDeepenCorruption);
    }

    [Fact]
    public void test_deepening_corruption_ratchets_the_tier_up_without_resetting_conquest()
    {
        var world = new World();
        foreach (var r in Regions.All) world.Conquer(r.Id);

        Assert.Equal(1, world.DeepenCorruption());
        Assert.Equal(2, world.DeepenCorruption());

        // Nothing resets — the world stays conquered.
        Assert.True(world.AllConquered);
        Assert.All(Regions.All, r => Assert.True(world.IsConquered(r.Id)));
    }

    [Fact]
    public void test_corruption_tier_restores_from_a_save()
    {
        var world = new World();
        world.RestoreCorruption(3);
        Assert.Equal(3, world.CorruptionTier);

        world.RestoreCorruption(-5); // a corrupt value floors at zero, never negative
        Assert.Equal(0, world.CorruptionTier);
    }

    /// <summary>Each region declares its standard pool of encounters and exactly one boss.</summary>
    [Fact]
    public void test_each_region_declares_its_standard_pool_and_a_boss()
    {
        // Asserted straight off the region content now — the old EncounterSpawner that rolled from it was
        // retired with the manual-combat model.
        Assert.Equal(3, Regions.Get(VerdantHollow.RegionId).Templates.Count(t => !t.IsBoss));
        Assert.Equal(3, Regions.Get("cinderworks").Templates.Count(t => !t.IsBoss));
        Assert.Contains(Regions.Get("umbral_reach").Templates, t => t.IsBoss);
    }
}
