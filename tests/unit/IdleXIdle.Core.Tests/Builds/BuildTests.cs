using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

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
    // Six distinct catalogue ids, so a fixture can fill five slots and still hold one to refuse.
    // Distinct because Unweave removes by Def.Id — a build of four copies would unweave all four.
    private static readonly string[] SkillIds =
        { "hammer_blow", "snare_repay", "sign_call", "volley_spray", "field_pulse", "drain_drink" };

    private static EquippedSkill Skill(int slot) => TestBuilds.Skill(SkillIds[slot]);

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

    // ── Skills: a bounded budget, so WHICH skills is a CHOICE ─────────────────────────────────

    [Fact]
    public void test_the_skill_slots_are_bounded()
    {
        // Six styles and six slots would mean everyone carries everything, and the style axis collapses
        // — exactly how an unbounded gear budget collapsed "which item" into "the biggest number".
        var build = new Build();
        for (var i = 0; i < Build.SkillSlots; i++)
            Assert.True(build.Equip(Skill(i)), "a slot inside the budget was refused");

        Assert.False(build.Equip(Skill(Build.SkillSlots)));
        Assert.Equal(Build.SkillSlots, build.Skills.Count);
    }

    [Fact]
    public void test_there_are_more_styles_than_slots()
    {
        // The property that makes the budget bite. If the styles ever drop to 4, the choice disappears.
        Assert.True(System.Enum.GetValues<Style>().Length > Build.SkillSlots,
            "there are no more styles than skill slots — the player is no longer choosing anything");
        Assert.True(SkillCatalogue.All.Count > Build.SkillSlots,
            "there are no more skills than skill slots — the player is no longer choosing anything");
    }

    [Fact]
    public void test_a_skill_can_be_unwoven_to_make_room()
    {
        var build = new Build();
        for (var i = 0; i < Build.SkillSlots; i++) build.Equip(Skill(i));

        Assert.True(build.Unequip(SkillIds[0]));
        Assert.True(build.Equip(Skill(4)));
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

    // ── THE FIFTH WEAVE, AND ITS REMOVAL ─────────────────────────────────────────────────────────

    [Fact]
    public void test_a_build_weaves_up_to_its_own_capacity_and_never_past_four()
    {
        // CHANGED 2026-09-03, because the capability changed. This test used to pin the OPPOSITE rule —
        // that a capacity of 5 really weaves five — because FIFTH WEAVE was a trait-spine node that was
        // bought, saved, resolved to 5 and then clamped back to 4 by a Math.Min against a const, which
        // is this codebase's signature failure.
        //
        // The fifth slot is REMOVED now rather than granted: at five slots the active/passive split
        // gives THREE action-taking skills, which is the beat demand the slot rework existed to bring
        // down. So a stale capacity of 5 must be ANSWERED with four rather than honoured, and this test
        // pins the clamp that answers it — in the same place the old bug was visible.
        var build = new Build { SlotCapacity = 5 };
        Assert.Equal(Build.SkillSlots, build.SlotCapacity);

        for (var i = 0; i < 4; i++)
            Assert.True(build.Equip(Skill(i)), $"slot {i} was refused at a capacity of 4");

        Assert.Equal(4, build.Skills.Count);
        Assert.False(build.Equip(Skill(4)), "four slots must refuse a fifth");
    }

    [Fact]
    public void test_capacity_never_reports_fewer_slots_than_there_are_skills_in_them()
    {
        // THIS TEST USED TO PIN THE WRONG RULE. It asserted the capacity floors at Build.SkillSlots (4),
        // which was sound while every player had four slots from the first frame. The gradual-unlock
        // pass ended that — a new champion has ONE — and against a hard floor of 4 the build reported
        // four slots to a player who had one. That is not cosmetic: VOW OF COMPLETION demands "no skill
        // slot is empty", so it compared one woven against four slots and was UNMEETABLE for the whole
        // of onboarding, showing UNMET on the Weave screen for a build with no empty slot at all.
        //
        // The protection it was reaching for is real and is kept: a capacity must never be small enough
        // to unweave something. That is a floor of WHAT IS WOVEN, not of a constant.
        var narrow = new Build { SlotCapacity = 1 };
        Assert.Equal(1, narrow.SlotCapacity);
        Assert.True(narrow.Equip(Skill(0)));
        Assert.False(narrow.Equip(Skill(1)), "a one-slot build accepted a second skill");

        // Now shrink a build that already holds more than the new capacity: it must not lose any.
        var full = new Build { SlotCapacity = 4 };
        for (var i = 0; i < 4; i++) Assert.True(full.Equip(Skill(i)));

        full.SlotCapacity = 1;
        Assert.Equal(4, full.SlotCapacity);
        Assert.Equal(4, full.Skills.Count);
    }

    [Fact]
    public void test_a_default_build_still_stops_at_four()
    {
        var build = new Build();
        Assert.Equal(Build.SkillSlots, build.SlotCapacity);
        for (var i = 0; i < Build.SkillSlots; i++) Assert.True(build.Equip(Skill(i)));
        Assert.False(build.Equip(Skill(4)), "a build nobody expanded must not grow one for free");
    }

    [Fact]
    public void test_the_vow_of_completion_is_meetable_during_onboarding()
    {
        // THE PLAYER-VISIBLE HALF OF THE CAPACITY BUG. A champion in the unlock window holds one skill
        // in one slot — which is, plainly, no empty slot — and the Weave screen showed VOW OF COMPLETION
        // as UNMET because the build believed it had four. A Vow that cannot be met is a Vow nobody can
        // swear, and it sat at the top of the list on the game's most distinctive screen.
        var hunter = new Hunter();

        var onboarding = new Build { SlotCapacity = 1 };
        onboarding.Equip(Skill(0));

        var ctx = SoloBattle.DescribeBuild(onboarding, hunter);
        Assert.Equal(1, ctx.SkillSlots);
        Assert.Equal(1, ctx.SkillsWoven);

        var vow = Vows.Catalog.First(v => v.Id == "vow_complete");
        Assert.True(Vows.IsActive(vow, ctx),
            "a one-skill champion with one slot has no empty slot, but VOW OF COMPLETION reads UNMET.");

        // And it must still REFUSE a build that genuinely has a gap.
        var gappy = new Build { SlotCapacity = 3 };
        gappy.Equip(Skill(0));
        Assert.False(Vows.IsActive(vow, SoloBattle.DescribeBuild(gappy, hunter)),
            "a build with two empty slots met a Vow that demands none.");
    }
}
