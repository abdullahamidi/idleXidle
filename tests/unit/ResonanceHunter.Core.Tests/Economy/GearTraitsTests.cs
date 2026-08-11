using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Economy;

/// <summary>
/// Item passives, and the two gear slots that were doing nothing at all.
/// </summary>
public class GearTraitsTests
{
    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity = Rarity.Rare)
        => new() { InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 34 };

    // ── The trait itself ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_trait_of_the_same_item_is_always_the_same()
    {
        // Derived, not stored — so it must be a pure function of the id. FNV-1a, not GetHashCode(),
        // because .NET randomises string hashing per process: an item's trait would change on restart.
        var item = Item("weapon_abc123", ItemBaseType.Weapon);
        var first = GearTraits.TraitOf(item);

        for (var i = 0; i < 50; i++)
            Assert.Equal(first, GearTraits.TraitOf(Item("weapon_abc123", ItemBaseType.Weapon)));
    }

    [Fact]
    public void test_trait_of_an_unwearable_item_is_null()
    {
        Assert.Null(GearTraits.TraitOf(Item("mat_1", ItemBaseType.Material)));
        Assert.Null(GearTraits.TraitOf(null));
    }

    [Fact]
    public void test_a_trait_always_comes_from_its_slots_pool()
    {
        // A charm must never roll HEAVY: the trait has to mean something for the slot it sits in.
        foreach (var (type, slot) in new[]
                 {
                     (ItemBaseType.Weapon, GearSlot.Weapon),
                     (ItemBaseType.Charm, GearSlot.Charm),
                     (ItemBaseType.AbilityFocus, GearSlot.Focus),
                 })
        {
            var pool = GearTraits.PoolFor(slot);
            for (var i = 0; i < 200; i++)
            {
                var t = GearTraits.TraitOf(Item($"item_{i}", type));
                Assert.NotNull(t);
                Assert.Contains(t!.Value, pool);
            }
        }
    }

    [Fact]
    public void test_ids_spread_across_the_whole_pool()
    {
        // A hash that collapsed onto one trait would technically pass every test above while quietly
        // making every weapon in the game identical.
        var seen = new HashSet<GearTrait>();
        for (var i = 0; i < 200; i++)
            seen.Add(GearTraits.TraitOf(Item($"weapon_{i}", ItemBaseType.Weapon))!.Value);

        Assert.Equal(GearTraits.PoolFor(GearSlot.Weapon).Length, seen.Count);
    }

    // ── The trade. This is the whole point of the layer. ──────────────────────────────────────

    [Fact]
    public void test_in_every_pool_the_drawback_free_traits_have_the_smaller_ceiling()
    {
        // The playtest complaint was "I just click the best weapon" — true because every stat was
        // rarity x constant, monotonic in one variable. The fix is that reach must be PAID for: within
        // a pool, the highest number always belongs to a trait that costs you something. A drawback-free
        // trait is allowed to be good (KEEN is +35% at Legendary); it is not allowed to be the best.
        foreach (var slot in new[] { GearSlot.Weapon, GearSlot.Charm, GearSlot.Focus })
        {
            var pool = GearTraits.PoolFor(slot);
            float Ceiling(GearTrait t)
            {
                var m = GearTraits.ModsFor(t, Rarity.Legendary);
                return new[] { m.Damage, m.Health, m.Haul, m.SkillRate }.Max();
            }
            bool Free(GearTrait t)
            {
                var m = GearTraits.ModsFor(t, Rarity.Legendary);
                return m.Damage >= 1f && m.Health >= 1f && m.Haul >= 1f && m.SkillRate >= 1f;
            }

            var free = pool.Where(Free).ToList();
            var paid = pool.Where(t => !Free(t)).ToList();
            if (free.Count == 0 || paid.Count == 0) continue;

            Assert.True(free.Max(Ceiling) < paid.Max(Ceiling),
                $"in the {slot} pool a drawback-free trait reaches as high as a trait that pays for it");
        }
    }

    [Fact]
    public void test_no_drawback_traits_are_the_weak_ones()
    {
        // "Safe" must cost you the ceiling, otherwise safe is just correct.
        var keen = GearTraits.ModsFor(GearTrait.Keen, Rarity.Legendary);
        var heavy = GearTraits.ModsFor(GearTrait.Heavy, Rarity.Legendary);

        Assert.True(heavy.Damage > keen.Damage, "HEAVY must out-hit drawback-free KEEN");
        Assert.Equal(1f, keen.Health);
        Assert.True(heavy.SkillRate < 1f, "HEAVY must actually drag the skill clock");
    }

    [Fact]
    public void test_an_epic_swift_beats_a_legendary_heavy_on_skill_rate()
    {
        // The decision the layer exists to create: rarity decides HOW MUCH, the trait decides WHAT
        // KIND. If a Legendary won every axis, "which item" would still be a sort.
        var epicSwift = GearTraits.ModsFor(GearTrait.Swift, Rarity.Epic);
        var legendaryHeavy = GearTraits.ModsFor(GearTrait.Heavy, Rarity.Legendary);

        Assert.True(epicSwift.SkillRate > legendaryHeavy.SkillRate);
        Assert.True(legendaryHeavy.Damage > epicSwift.Damage); // ...and it still hits far harder
    }

    [Fact]
    public void test_rarity_scales_the_upside_but_not_the_drawback()
    {
        var common = GearTraits.ModsFor(GearTrait.Heavy, Rarity.Common);
        var legendary = GearTraits.ModsFor(GearTrait.Heavy, Rarity.Legendary);

        Assert.True(legendary.Damage > common.Damage, "a Legendary must still feel like an event");
        Assert.Equal(common.SkillRate, legendary.SkillRate);
    }

    [Fact]
    public void test_no_trait_is_strictly_better_than_another_in_the_same_pool()
    {
        // The formal version of "there is no correct answer". For every ordered pair in a pool, one
        // must not dominate the other on all four axes.
        foreach (var slot in new[] { GearSlot.Weapon, GearSlot.Charm, GearSlot.Focus })
        {
            var pool = GearTraits.PoolFor(slot);
            foreach (var a in pool)
            {
                foreach (var b in pool.Where(x => x != a))
                {
                    var ma = GearTraits.ModsFor(a, Rarity.Rare);
                    var mb = GearTraits.ModsFor(b, Rarity.Rare);
                    var dominates = ma.Damage >= mb.Damage && ma.Health >= mb.Health
                                    && ma.Haul >= mb.Haul && ma.SkillRate >= mb.SkillRate
                                    && (ma.Damage > mb.Damage || ma.Health > mb.Health
                                        || ma.Haul > mb.Haul || ma.SkillRate > mb.SkillRate);
                    Assert.False(dominates, $"{a} strictly dominates {b} in the {slot} pool — {b} is dead weight");
                }
            }
        }
    }

    [Fact]
    public void test_mods_combine_multiplicatively()
    {
        var a = new GearMods(1.5f, 0.8f, 1f, 1.2f);
        var b = new GearMods(2f, 1f, 1.5f, 0.5f);
        var c = a.Combine(b);

        Assert.Equal(3f, c.Damage, 3);
        Assert.Equal(0.8f, c.Health, 3);
        Assert.Equal(1.5f, c.Haul, 3);
        Assert.Equal(0.6f, c.SkillRate, 3);
    }

    [Fact]
    public void test_none_is_the_identity()
    {
        var m = new GearMods(1.5f, 0.8f, 1.1f, 1.2f);
        Assert.Equal(m, m.Combine(GearMods.None));
    }

    // ── The dead slots. Regressions: equipping these did NOTHING. ─────────────────────────────

    [Fact]
    public void test_wearing_a_charm_makes_the_squad_tougher()
    {
        // REGRESSION: the pivot pointed the squad at SquadHealthMultiplier, but Charm still fed the
        // Hunter's own Defense/MaxHealth, which nothing has read since manual combat was deleted.
        // Equipping a charm did literally nothing. Playtest: "There is no charming feature."
        var bare = new Hunter();
        var charmed = new Hunter();
        charmed.Equip(Item("charm_vital_1", ItemBaseType.Charm, Rarity.Epic));

        Assert.NotEqual(bare.SquadHealthMultiplier, charmed.SquadHealthMultiplier);
    }

    [Fact]
    public void test_wearing_a_focus_changes_how_fast_skills_come_back()
    {
        // REGRESSION: Focus fed "Resonance affinity" — the manual-combat ability stat. The slot was
        // inert; the Forge printed "+18 RES" for a number that reached no fight.
        var bare = new Hunter();
        var focused = new Hunter();
        focused.Equip(Item("focus_1", ItemBaseType.AbilityFocus, Rarity.Epic));

        Assert.NotEqual(bare.SquadSkillRate, focused.SquadSkillRate);
    }

    [Fact]
    public void test_an_empty_slot_pays_out_nothing()
    {
        // `_worn[slot]?.Rarity ?? 0` would be Rarity.Common, whose RarityPower is 0.20 — an empty slot
        // would quietly pay like a Common item. Note SquadHealthMultiplier is NOT 1.0 while bare: the
        // Vitality stat has a non-zero base value, so it starts at 1.12. Nothing to do with gear.
        var bare = new Hunter();
        Assert.Equal(GearMods.None, bare.WornMods);

        // Neither multiplier is 1.0 while bare: Vitality and Engineering both have non-zero base stat
        // values, so they start at 1.12 and 1.06. Nothing to do with gear — what must hold is that an
        // EMPTY slot adds nothing on top of the stat baseline.
        Assert.Equal(1f + 0.012f * bare.ValueOf(HunterStat.Vitality), bare.SquadHealthMultiplier, 4);
        Assert.Equal(1f + 0.006f * bare.ValueOf(HunterStat.Engineering), bare.SquadSkillRate, 4);
    }

    private static ItemInstance CharmWith(GearTrait trait, Rarity rarity)
        => Enumerable.Range(0, 500)
            .Select(i => Item($"charm_{i}", ItemBaseType.Charm, rarity))
            .First(it => GearTraits.TraitOf(it) == trait);

    [Fact]
    public void test_a_worn_traits_drawback_actually_reaches_the_squad()
    {
        // GREEDY pays a richer haul and costs health. Measured against a same-rarity VITAL charm rather
        // than against 1.0: a Legendary charm's own toughness bonus (x1.42) is larger than GREEDY's
        // 0.85, so in absolute terms the drawback is real but invisible. What must hold is that GREEDY
        // is TOUGHER-THAN-NOTHING yet FRAILER-THAN-VITAL, and pays for the difference.
        var greedy = new Hunter();
        greedy.Equip(CharmWith(GearTrait.Greedy, Rarity.Legendary));

        var vital = new Hunter();
        vital.Equip(CharmWith(GearTrait.Vital, Rarity.Legendary));

        Assert.True(greedy.WornMods.Health < 1f, "GREEDY must cost real health");
        Assert.True(greedy.SquadHealthMultiplier < vital.SquadHealthMultiplier,
            "the drawback never reached the squad — GREEDY is as tough as VITAL");
        Assert.True(greedy.HaulMultiplier > vital.HaulMultiplier, "GREEDY must still pay");
    }

    // The health-penalty regression once tested here through the retired squad Expedition — that a gear
    // drawback with a sub-1.0 multiplier is not clamped away — now lives in
    // test_a_worn_traits_drawback_actually_reaches_the_squad above, at the Hunter level where the champion
    // actually reads its health. The squad-mint floor it also guarded is gone with the squad.

    [Fact]
    public void test_all_three_slots_stack()
    {
        // Commons on purpose: they carry a TRAIT but no explicit affixes (ItemAffixes.CountFor is 0), so
        // WornMods is exactly the trait product here. Affixes fold into WornMods too — proven separately in
        // ItemAffixesTests — but they would muddy this test of the trait-stacking alone.
        var hunter = new Hunter();
        hunter.Equip(Item("weapon_1", ItemBaseType.Weapon, Rarity.Common));
        hunter.Equip(Item("charm_1", ItemBaseType.Charm, Rarity.Common));
        hunter.Equip(Item("focus_1", ItemBaseType.AbilityFocus, Rarity.Common));

        var expected = GearTraits.ModsOf(Item("weapon_1", ItemBaseType.Weapon, Rarity.Common))
            .Combine(GearTraits.ModsOf(Item("charm_1", ItemBaseType.Charm, Rarity.Common)))
            .Combine(GearTraits.ModsOf(Item("focus_1", ItemBaseType.AbilityFocus, Rarity.Common)));

        Assert.Equal(expected, hunter.WornMods);
    }

    [Fact]
    public void test_every_trait_has_a_blurb_that_names_its_cost()
    {
        foreach (var trait in System.Enum.GetValues<GearTrait>())
        {
            var blurb = GearTraits.BlurbOf(trait);
            Assert.False(string.IsNullOrWhiteSpace(blurb));

            var m = GearTraits.ModsFor(trait, Rarity.Rare);
            var hasDrawback = m.Damage < 1f || m.Health < 1f || m.Haul < 1f || m.SkillRate < 1f;

            // The player must be able to read the trade off the tooltip, not discover it by dying.
            if (hasDrawback)
                Assert.DoesNotContain("No drawback", blurb);
            else
                Assert.Contains("No drawback", blurb);
        }
    }
}
