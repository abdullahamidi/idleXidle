using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The build: one character, its skills, its keystones, its gear.
/// </summary>
/// <remarks>
/// The roster is gone. There is no squad, no slot order, no Role — the player has ONE character and
/// every decision they make is a build decision. These tests guard the property that makes that
/// interesting: that the decisions are REFUSABLE.
/// </remarks>
public class BuildTests
{
    private static EquippedSkill Skill(string name, Form form = Form.Strike, Source src = Source.Nature)
        => new(new WovenAbility { Name = name, Source = src, Form = form }, 1500);

    // ── The law of the catalog ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_keystone_costs_something()
    {
        // THE LAW, and this game has rediscovered it four times: with gear ("I just click the best
        // weapon"), with slot order ("everybody puts the tank in front"), with Vows, and with the
        // palette. A choice with a strictly-correct answer is not a choice.
        //
        // A keystone with no drawback is an attribute node wearing a hat: everyone takes it, and every
        // build converges. This test is the only thing standing between the tree and that.
        foreach (var k in Keystones.Catalog)
            Assert.True(Keystones.IsATrade(k),
                $"{k.Id} gives without costing — every build will take it, and every build will be the same");
    }

    [Fact]
    public void test_no_keystone_strictly_dominates_another()
    {
        foreach (var a in Keystones.Catalog)
        {
            foreach (var b in Keystones.Catalog.Where(x => x.Id != a.Id))
            {
                var ma = a.Mods; var mb = b.Mods;
                var dominates =
                    ma.Damage >= mb.Damage && ma.Health >= mb.Health && ma.SkillRate >= mb.SkillRate &&
                    ma.Haul >= mb.Haul && ma.Rarity >= mb.Rarity &&
                    (ma.Damage > mb.Damage || ma.Health > mb.Health || ma.SkillRate > mb.SkillRate ||
                     ma.Haul > mb.Haul || ma.Rarity > mb.Rarity) &&
                    b.Grants.All(g => a.Grants.Contains(g));

                Assert.False(dominates, $"{a.Id} strictly dominates {b.Id} — {b.Id} is dead weight");
            }
        }
    }

    [Fact]
    public void test_the_keystones_contradict_each_other()
    {
        // A build must be forced to CHOOSE, and not merely by a budget. GLASS CANNON and IRONCLAD pull
        // in opposite directions on the same two axes: taking both is close to taking neither.
        var glass = Keystones.ById("glass_cannon")!;
        var iron = Keystones.ById("ironclad")!;

        Assert.True(glass.Mods.Damage > 1f && glass.Mods.Health < 1f);
        Assert.True(iron.Mods.Health > 1f && iron.Mods.SkillRate < 1f);

        var both = glass.Mods.Combine(iron.Mods);
        Assert.Equal(1f, both.Health, 2);   // 0.5 x 2.0 — they cancel exactly
    }

    // ── Resolving a build ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_an_empty_build_changes_nothing_about_the_character()
    {
        var hunter = new Hunter();
        var build = new Build();
        var m = build.Resolve(hunter);

        // The character's own trained stats still apply; the BUILD adds nothing on top of them.
        Assert.Equal(hunter.SquadDamageMultiplier, m.Damage, 3);
        Assert.Equal(hunter.SquadHealthMultiplier, m.Health, 3);
    }

    [Fact]
    public void test_keystones_multiply_rather_than_add()
    {
        // Additive stacking is how an idle game arrives at 400,000% and stops meaning anything.
        var hunter = new Hunter();
        var build = new Build();
        build.Take(Keystones.ById("glass_cannon")!);   // x2.0 damage
        build.Take(Keystones.ById("greed")!);          // x0.7 damage, x2.0 haul

        var m = build.Resolve(hunter);
        Assert.Equal(hunter.SquadDamageMultiplier * 2.0f * 0.7f, m.Damage, 3);
        Assert.Equal(hunter.HaulMultiplier * 2.0f, m.Haul, 3);
    }

    [Fact]
    public void test_gear_is_not_billed_twice()
    {
        // Gear's mods are ALREADY inside the hunter's Squad* multipliers. Folding them in again would
        // square every trait — a Legendary HEAVY weapon would read as +96% damage twice over.
        var hunter = new Hunter();
        var weapon = new ItemInstance
        {
            InstanceId = "w1", BaseType = ItemBaseType.Weapon, Rarity = Rarity.Legendary, SellValue = 200,
        };
        hunter.Equip(weapon);

        var m = new Build().Resolve(hunter);
        Assert.Equal(hunter.SquadDamageMultiplier, m.Damage, 3);
    }

    [Fact]
    public void test_taking_the_same_keystone_twice_takes_it_once()
    {
        var build = new Build();
        build.Take(Keystones.ById("glass_cannon")!);
        build.Take(Keystones.ById("glass_cannon")!);

        Assert.Single(build.Keystones);
        Assert.Equal(2.0f, build.Resolve(new Hunter()).Damage / new Hunter().SquadDamageMultiplier, 2);
    }

    [Fact]
    public void test_a_keystone_can_be_dropped()
    {
        var build = new Build();
        build.Take(Keystones.ById("ironclad")!);
        build.Drop("ironclad");

        Assert.Empty(build.Keystones);
    }

    // ── Skills: a bounded budget, so Form is a CHOICE ─────────────────────────────────────────

    [Fact]
    public void test_the_skill_slots_are_bounded()
    {
        // Six Forms and six slots would mean everyone carries everything, and the Form axis collapses
        // — exactly how an unbounded gear budget collapsed "which item" into "the biggest number".
        var build = new Build();
        for (var i = 0; i < Build.SkillSlots; i++)
            Assert.True(build.Weave(Skill($"s{i}")), "a slot inside the budget was refused");

        Assert.False(build.Weave(Skill("one_too_many")));
        Assert.Equal(Build.SkillSlots, build.Skills.Count);
    }

    [Fact]
    public void test_there_are_more_forms_than_slots()
    {
        // The property that makes the budget bite. If Forms ever drop to 4, the choice disappears.
        Assert.True(System.Enum.GetValues<Form>().Length > Build.SkillSlots,
            "there are no more Forms than skill slots — the player is no longer choosing anything");
    }

    [Fact]
    public void test_a_skill_can_be_unwoven_to_make_room()
    {
        var build = new Build();
        for (var i = 0; i < Build.SkillSlots; i++) build.Weave(Skill($"s{i}"));

        Assert.True(build.Unweave("s0"));
        Assert.True(build.Weave(Skill("replacement")));
    }

    // ── Triggers ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_keystone_grants_its_behaviour()
    {
        var build = new Build();
        build.Take(Keystones.ById("blood_magic")!);

        Assert.Contains(BuildTrigger.NoHealing, build.Triggers(new Hunter()));
    }

    [Fact]
    public void test_an_item_and_a_keystone_speak_the_same_language()
    {
        // One vocabulary, so the sim implements each behaviour ONCE. An item's UNDYING and a
        // keystone's UNDYING must be the same thing, or there are two half-built versions of it.
        var hunter = new Hunter();
        var charm = Enumerable.Range(0, 500)
            .Select(i => new ItemInstance
            {
                InstanceId = $"c{i}", BaseType = ItemBaseType.Charm, Rarity = Rarity.Legendary, SellValue = 200,
            })
            .First(it => Enchantments.Of(it)?.Kind == EnchantKind.Undying);
        hunter.Equip(charm);

        Assert.Contains(BuildTrigger.Undying, new Build().Triggers(hunter));
    }

    [Fact]
    public void test_two_sources_of_one_trigger_is_still_one_trigger()
    {
        // UNDYING from a charm AND from a keystone must not mean you get up twice.
        var hunter = new Hunter();
        var charm = Enumerable.Range(0, 500)
            .Select(i => new ItemInstance
            {
                InstanceId = $"c{i}", BaseType = ItemBaseType.Charm, Rarity = Rarity.Legendary, SellValue = 200,
            })
            .First(it => Enchantments.Of(it)?.Kind == EnchantKind.Undying);
        hunter.Equip(charm);

        var build = new Build();
        build.Take(Keystones.ById("undying")!);

        Assert.Equal(1, build.Triggers(hunter).Count(t => t == BuildTrigger.Undying));
    }

    [Fact]
    public void test_an_empty_build_on_a_naked_character_grants_nothing()
    {
        Assert.Empty(new Build().Triggers(new Hunter()));
    }
}
