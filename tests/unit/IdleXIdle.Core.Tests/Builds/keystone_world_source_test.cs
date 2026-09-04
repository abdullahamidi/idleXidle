using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// Keystones come from the WORLD, not from a menu — and the table that says so has to hold.
/// </summary>
/// <remarks>
/// The trait tree was the only producer of all nineteen keystones. Delete it without a replacement and
/// the whole catalogue dies silently, which is exactly the failure this project keeps paying for. These
/// tests are the replacement's wire test: every keystone reachable, nothing orphaned, nothing arriving
/// before the thing it needs, and nothing the rest of the game depends on hidden behind a farm.
/// </remarks>
public class KeystoneWorldSourceTest
{
    private static World WorldWith(params string[] conquered)
    {
        var w = new World();
        w.RestoreConquered(conquered);
        return w;
    }

    /// <summary>Push a region to a mastery level by paying it the points its thresholds ask for.</summary>
    private static World Mastered(string regionId, MasteryLevel level)
    {
        var t = AutomationTuning.Default;
        var w = new World();
        w.RestoreConquered(new[] { regionId });
        w.RegionFarm(regionId).RestoreMasteryPoints(level switch
        {
            MasteryLevel.PartiallyMastered => t.RmpThreshold1,
            MasteryLevel.FullyMastered => t.RmpThreshold2,
            MasteryLevel.Perfected => t.RmpThreshold3,
            _ => 0f,
        });
        return w;
    }

    [Fact]
    public void test_every_keystone_has_exactly_one_world_source()
    {
        // The WiredIds guard, restated for the new producer: a keystone with no source is unreachable
        // content, and a keystone with two is a reveal that can fire twice.
        var ids = Keystones.Sources.Select(s => s.KeystoneId).ToList();

        Assert.Equal(Keystones.Catalog.Count, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var k in Keystones.Catalog)
            Assert.True(ids.Contains(k.Id), $"{k.Id} is in the catalogue and nothing in the world teaches it");
        foreach (var s in Keystones.Sources)
            Assert.True(Keystones.ById(s.KeystoneId) is not null, $"{s.KeystoneId} is taught and is not a keystone");
    }

    [Fact]
    public void test_every_region_teaches_three_and_the_corruption_teaches_the_nineteenth()
    {
        // The sentence a player can learn: every region teaches you three — one for taking it, one for
        // knowing it, one for mastering it. If that stops being true the map's strip starts lying.
        foreach (var region in Regions.All)
        {
            var rungs = Keystones.Sources.Where(s => s.RegionId == region.Id).Select(s => s.Rung).ToList();
            Assert.Equal(3, rungs.Count);
            Assert.Contains(WorldRung.Conquest, rungs);
            Assert.Contains(WorldRung.PartlyMastered, rungs);
            Assert.Contains(WorldRung.FullyMastered, rungs);
        }
        Assert.Single(Keystones.Sources.Where(s => s.Rung == WorldRung.Corruption));
    }

    [Fact]
    public void test_a_keystone_that_needs_another_is_never_discovered_first()
    {
        // DYNAMO alone fills a charge pool nothing reads and charges 15% more damage taken for it;
        // CAPACITOR alone is a bigger pool nothing spends. Both need REND or LODESTONE. Regions unlock
        // in a fixed chain, so "an earlier region's CONQUEST" is a guarantee — a region's own mastery
        // rungs are not, because a hundred waves accrue whether or not you ever hold wave 20.
        var order = Regions.All.Select((r, i) => (r.Id, i)).ToDictionary(p => p.Id, p => p.i);

        void NeedsBefore(string dependent, string reader)
        {
            var d = Keystones.SourceOf(dependent)!;
            var r = Keystones.SourceOf(reader)!;
            Assert.Equal(WorldRung.Conquest, r.Rung);
            Assert.True(order[r.RegionId!] < order[d.RegionId!],
                $"{reader} (region {order[r.RegionId!]}) must be strictly before {dependent} "
                + $"(region {order[d.RegionId!]}) or the reward is pure downside when it arrives");
        }

        NeedsBefore("dynamo", "rend");
        NeedsBefore("capacitor", "rend");
    }

    [Fact]
    public void test_every_combo_enchantment_partner_is_a_conquest_keystone()
    {
        // Two of the five weapon enchantments need a keystone, so 40% of every Rare-or-better weapon
        // hangs on this. A conquest costs holding wave 20 in a chain every player walks; PARTLY
        // MASTERED costs a hundred waves of farming one place, which a pushing player may never do —
        // putting a partner behind that makes a rare weapon's headline hostage to a play style.
        var partners = Enum.GetValues<EnchantKind>()
            .Select(k => new Enchantment(k, 0.1f).Needs?.Keystone)
            .OfType<BuildTrigger>()
            .Distinct()
            .ToList();

        Assert.NotEmpty(partners);   // if this empties, the guard below is guarding nothing
        foreach (var trigger in partners)
        {
            var k = Keystones.GrantingTrigger(trigger);
            Assert.True(k is not null, $"{trigger} is asked for by an enchantment and no keystone grants it");
            var src = Keystones.SourceOf(k!.Id)!;
            Assert.Equal(WorldRung.Conquest, src.Rung);
            Assert.True(Regions.All.Select(r => r.Id).Take(5).Contains(src.RegionId),
                $"{k.Id} is an enchantment partner and sits past the world's first five conquests");
        }
    }

    [Fact]
    public void test_a_conquest_teaches_its_region_keystone_and_only_that_one()
    {
        var w = WorldWith(VerdantHollow.RegionId);
        var found = Keystones.DiscoveredBy(w).Select(k => k.Id).ToList();

        Assert.Equal(new[] { "echo" }, found);
    }

    [Fact]
    public void test_a_mastery_rung_teaches_and_the_keystone_stays_taught()
    {
        // Region mastery only ever grows, so the reveal cannot re-fire — but the point of the test is
        // the other half: what a rung taught is still taught when the next rung arrives.
        var partly = Mastered(VerdantHollow.RegionId, MasteryLevel.PartiallyMastered);
        Assert.Contains("ironclad", Keystones.DiscoveredBy(partly).Select(k => k.Id));

        var fully = Mastered(VerdantHollow.RegionId, MasteryLevel.FullyMastered);
        var ids = Keystones.DiscoveredBy(fully).Select(k => k.Id).ToList();
        Assert.Contains("ironclad", ids);
        Assert.Contains("greed", ids);
    }

    [Fact]
    public void test_the_corruption_teaches_weaver_only_after_it_is_deepened()
    {
        var w = new World();
        w.RestoreConquered(Regions.All.Select(r => r.Id));
        Assert.DoesNotContain("weaver", Keystones.DiscoveredBy(w).Select(k => k.Id));

        w.DeepenCorruption();
        Assert.Contains("weaver", Keystones.DiscoveredBy(w).Select(k => k.Id));

        // AND IT STAYS TAUGHT WHEN THE WORLD IS EASED. The peak never falls, by construction, which is
        // what stops the reveal firing again on the next deepening.
        w.EaseCorruption();
        Assert.Contains("weaver", Keystones.DiscoveredBy(w).Select(k => k.Id));
    }

    [Fact]
    public void test_traits_are_not_required_for_any_of_it()
    {
        // The whole point of the move. Nothing in the discovery path reads a trait point, a trait node
        // or the tree at all — the only inputs are the world and a legacy grant list.
        var w = WorldWith(VerdantHollow.RegionId, "cinderworks");
        Assert.Equal(2, Keystones.DiscoveredBy(w).Count);
    }

    [Fact]
    public void test_a_legacy_grant_is_unioned_in_so_nobody_re_conquers_what_they_bought()
    {
        // The migration case: a save that bought ks_titan on the old tree keeps TITAN, even though THE
        // PALE CHOIR is nowhere near mastered.
        var w = WorldWith(VerdantHollow.RegionId);
        var ids = Keystones.DiscoveredBy(w, new[] { "titan" }).Select(k => k.Id).ToList();

        Assert.Contains("echo", ids);
        Assert.Contains("titan", ids);
    }

    [Fact]
    public void test_the_menu_is_catalogue_order_so_the_build_screen_never_reshuffles()
    {
        var w = new World();
        w.RestoreConquered(Regions.All.Select(r => r.Id));
        var found = Keystones.DiscoveredBy(w).Select(k => k.Id).ToList();
        var expected = Keystones.Catalog.Select(k => k.Id).Where(found.Contains).ToList();

        Assert.Equal(expected, found);
    }

    [Fact]
    public void test_where_to_find_names_a_real_place_for_every_keystone()
    {
        // The Forge draws this line on an unpaired combo enchantment. A blank one would put the player
        // back where they were: told they need BLOODLUST and given no route to it.
        foreach (var k in Keystones.Catalog)
            Assert.False(string.IsNullOrWhiteSpace(Keystones.WhereToFind(k.Id)),
                $"{k.Id} has no sentence telling the player where it is");
    }
}
