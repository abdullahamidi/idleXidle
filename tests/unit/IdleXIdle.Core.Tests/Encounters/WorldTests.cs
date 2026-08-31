using System.Linq;
using IdleXIdle.Core.Combat;
using IdleXIdle.Core.Encounters;
using Xunit;

namespace IdleXIdle.Core.Tests.Encounters;

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

    /// <summary>Each region has its own progress record — mastery and depth are earned independently.</summary>
    [Fact]
    public void test_each_region_has_its_own_independent_farm()
    {
        // Arrange
        var world = new World();
        var home = world.RegionFarm(VerdantHollow.RegionId);
        var cinder = world.RegionFarm("cinderworks");
        Assert.NotSame(home, cinder);

        // Act — fight in the home region only.
        for (var i = 0; i < 10; i++) home.RecordActiveKill();
        home.RecordDepth(12);

        // Assert — the other region's record is untouched.
        Assert.True(home.RegionMasteryPoints > 0);
        Assert.Equal(12, home.BestDepth);
        Assert.Equal(0f, cinder.RegionMasteryPoints);
        Assert.Equal(0, cinder.BestDepth);
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

    [Fact]
    public void test_corruption_stops_at_the_top_and_can_be_eased_back()
    {
        var world = new World();
        foreach (var r in Regions.All) world.Conquer(r.Id);
        Assert.False(world.CanEaseCorruption);
        for (var i = 0; i < CorruptionScaling.MaxTier + 3; i++) world.DeepenCorruption();
        Assert.Equal(CorruptionScaling.MaxTier, world.CorruptionTier);
        Assert.False(world.CanDeepenCorruption);
        Assert.True(world.CanEaseCorruption);
        Assert.Equal(CorruptionScaling.MaxTier - 1, world.EaseCorruption());
        Assert.True(world.CanDeepenCorruption);
        while (world.CanEaseCorruption) world.EaseCorruption();
        Assert.Equal(0, world.CorruptionTier);
        Assert.Equal(0, world.EaseCorruption());   // the floor holds
        world.RestoreCorruption(99);                 // an old save past the top lands ON the top
        Assert.Equal(CorruptionScaling.MaxTier, world.CorruptionTier);
    }

    [Fact]
    public void test_the_peak_tier_is_remembered_across_easing_and_restores()
    {
        // The deepening award and the trait points key off the PEAK, so SHALLOWER then DEEPER pays nothing
        // twice and easing takes nothing away (review, 2026-08-23).
        var world = new World();
        foreach (var r in Regions.All) world.Conquer(r.Id);
        world.DeepenCorruption(); world.DeepenCorruption(); world.DeepenCorruption();
        Assert.Equal(3, world.PeakCorruptionTier);
        Assert.True(world.IsNewPeak);
        world.EaseCorruption(); world.EaseCorruption();
        Assert.Equal(1, world.CorruptionTier);
        Assert.Equal(3, world.PeakCorruptionTier);
        world.DeepenCorruption();
        Assert.False(world.IsNewPeak);          // tier 2 again — reached before
        world.DeepenCorruption(); world.DeepenCorruption();
        Assert.True(world.IsNewPeak);           // tier 4 — new
        Assert.Equal(4, world.PeakCorruptionTier);

        var restored = new World();
        foreach (var r in Regions.All) restored.Conquer(r.Id);
        restored.RestoreCorruption(tier: 1, peak: 4);
        Assert.Equal(4, restored.PeakCorruptionTier);
        var legacy = new World();
        legacy.RestoreCorruption(2);             // an older save carries no peak: the tier is the peak
        Assert.Equal(2, legacy.PeakCorruptionTier);
    }
}
