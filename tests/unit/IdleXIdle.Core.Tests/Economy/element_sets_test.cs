using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// THE ELEMENT SETS (2026-08-27): worn pieces of one element unlock that element's rungs at 2, 3, 4
/// and 5 — and every rung must reach the fight, measured at the end of the chain on the sim the game runs.
/// </summary>
public class element_sets_test
{
    private static readonly GearSlot[] FiveSlots = { GearSlot.Weapon, GearSlot.Helm, GearSlot.Chest, GearSlot.Gloves, GearSlot.Boots };

    private static ItemBaseType TypeFor(GearSlot slot) => slot switch
    {
        GearSlot.Weapon => ItemBaseType.Weapon, GearSlot.Helm => ItemBaseType.Helm, GearSlot.Chest => ItemBaseType.Chest,
        GearSlot.Gloves => ItemBaseType.Gloves, GearSlot.Boots => ItemBaseType.Boots, GearSlot.Charm => ItemBaseType.Charm,
        GearSlot.Focus => ItemBaseType.AbilityFocus, _ => ItemBaseType.Ring,
    };

    private static ItemInstance Piece(GearSlot slot, Source? element, int i) => new()
    {
        InstanceId = $"set-{element}-{slot}-{i}", BaseType = TypeFor(slot), Rarity = Rarity.Common,
        SellValue = 1, ItemLevel = 1, Element = element,
        Family = 0,   // pinned: a weapon's family (and so its favoured styles) otherwise hashes off the id
    };

    /// <summary>A hunter wearing <paramref name="pieces"/> plain pieces of one element and nothing else.</summary>
    private static Hunter Wearing(Source element, int pieces)
    {
        var h = new Hunter();
        for (var i = 0; i < pieces; i++) h.Equip(Piece(FiveSlots[i], element, i));
        return h;
    }

    /// <summary>
    /// THE CONTROL: the same pieces with no element. A worn weapon multiplies every hit on its own, so
    /// "set against bare hands" would measure the weapon; set against plain measures the set.
    /// </summary>
    private static Hunter Plain(int pieces)
    {
        var h = new Hunter();
        for (var i = 0; i < pieces; i++) h.Equip(Piece(FiveSlots[i], null, i));
        return h;
    }

    private static EquippedSkill Sk(Source src, string skillId) => TestBuilds.Skill(skillId, source: src);

    private static Build BuildOf(Source src, params string[] skillIds)
    {
        var b = new Build();
        foreach (var id in skillIds) b.Equip(Sk(src, id));
        return b;
    }

    private static (List<BattleEvent> Events, Champion Champ, WaveMetrics Metrics) Fight(
        Hunter hunter, Build build, float enemyHp = 5_000_000f, float enemyDmg = 30f, int hp = 100_000, ExpeditionTuning? tuning = null)
    {
        var champ = new Champion { MaxHealth = hp, Health = hp };
        var metrics = new WaveMetrics();
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, hunter, new List<WaveCreature> { new() { MaxHealth = enemyHp, Health = enemyHp, Damage = enemyDmg } },
            enemyIntervalMs: 900, tuning ?? ExpeditionTuning.Default, new Random(11), metrics: metrics);
        return (events, champ, metrics);
    }

    private static int Dealt(IEnumerable<BattleEvent> e, bool fromSkill) =>
        e.Where(x => x.Kind == BattleEventKind.Strike && x.FromSkill == fromSkill).Sum(x => x.Amount);

    // ── the rules ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_element_has_four_rungs_at_two_three_four_and_five()
    {
        foreach (var el in Enum.GetValues<Source>())
        {
            var tiers = ElementSets.TiersOf(el);
            Assert.Equal(new[] { 2, 3, 4, 5 }, tiers.Select(t => t.Pieces));
            Assert.All(tiers, t => Assert.False(string.IsNullOrWhiteSpace(t.Line)));
        }
    }

    [Fact]
    public void test_rungs_open_as_pieces_are_worn_and_count_each_element_apart()
    {
        Assert.Empty(ElementSets.Active(new Hunter()));
        Assert.Empty(ElementSets.Active(Wearing(Source.Spirit, 1)));
        Assert.Equal(new[] { 2 }, ElementSets.Active(Wearing(Source.Spirit, 2)).Select(a => a.Tier.Pieces));
        Assert.Equal(new[] { 2, 3, 4, 5 }, ElementSets.Active(Wearing(Source.Spirit, 5)).Select(a => a.Tier.Pieces));

        // Two Spirit pieces and two Body pieces: each set counts its own, neither reaches three.
        var mixed = new Hunter();
        mixed.Equip(Piece(GearSlot.Weapon, Source.Spirit, 0));
        mixed.Equip(Piece(GearSlot.Helm, Source.Spirit, 1));
        mixed.Equip(Piece(GearSlot.Chest, Source.Body, 2));
        mixed.Equip(Piece(GearSlot.Gloves, Source.Body, 3));
        Assert.Equal(2, ElementSets.WornCount(mixed, Source.Spirit));
        Assert.Equal(2, ElementSets.WornCount(mixed, Source.Body));
        Assert.Equal(new[] { (Source.Body, 2), (Source.Spirit, 2) },
                     ElementSets.Active(mixed).Select(a => (a.Element, a.Tier.Pieces)));
    }

    [Fact]
    public void test_the_set_lines_are_plain_and_short()
    {
        var banned = new[] { "synergy", "proc", "buff", "nerf", "DPS", "AoE", "CDR", "%-", "stat " };
        foreach (var el in Enum.GetValues<Source>())
            foreach (var t in ElementSets.TiersOf(el))
            {
                // A capstone is a RULE and a lower rung is a number, so they do not get the same
                // budget: "+10% maximum health" and "once a wave the first bite that would hurt you is
                // stopped and becomes shield" are both as short as they can honestly be.
                var cap = t.Pieces == 5 ? 90 : 70;
                Assert.True(t.Line.Length <= cap, $"{el} {t.Pieces}: '{t.Line}' is {t.Line.Length} characters");
                Assert.EndsWith(".", t.Line);
                foreach (var word in banned)
                    Assert.DoesNotContain(word, t.Line, StringComparison.OrdinalIgnoreCase);
            }
        // The title the GEAR screen and the hover card compose: "NATURE SET — 3 OF 5 WORN". Past the
        // last rung it counts what is worn and says the set is complete — a fixture wearing eight
        // Nature pieces once read "8 OF 5".
        Assert.Equal("NATURE SET", ElementSets.Name(Source.Nature));
        Assert.Equal("0 OF 5 WORN", ElementSets.Progress(0));
        Assert.Equal("3 OF 5 WORN", ElementSets.Progress(3));
        Assert.Equal("5 OF 5 WORN", ElementSets.Progress(5));
        Assert.Equal("8 WORN · COMPLETE", ElementSets.Progress(8));
    }

    // ── every rung reaches the fight ──────────────────────────────────────────────────────────

    /// <summary>
    /// THE RULE THAT WENT AWAY. A set no longer pays a build for agreeing with it.
    /// </summary>
    /// <remarks>
    /// The old ladder gave "+8% to your matching-Source skills" at two pieces and again at four, and
    /// that was paying the player twice for one decision: Source already decides the variation they
    /// can take, the matchup they fight into, and the Vows they can swear. A set's Source is the
    /// philosophy of the EQUIPMENT — a BODY build may wear SHADOW plate and receive everything SHADOW
    /// offers — so this asserts the ABSENCE, which is the only way an absence stays gone.
    /// </remarks>
    [Fact]
    public void test_a_set_never_pays_a_build_for_matching_its_source()
    {
        foreach (var el in Enum.GetValues<Source>())
        {
            Assert.Empty(GearShape.Of(Wearing(el, 5)).SourceBonus);
            foreach (var tier in ElementSets.TiersOf(el))
                Assert.Empty(tier.Shape.SourceBonus);
        }

        // And measured. Not "the two builds deal the same" — they never would, because Source decides
        // the matchup they fight into before any gear is worn. What must be equal is what the SET IS
        // WORTH: the ratio each build gains over its own bare-handed self.
        float Worth(Source skillSource)
        {
            var build = BuildOf(skillSource, "hammer_blow");
            var without = Dealt(Fight(Plain(5), build).Events, true);
            var with = Dealt(Fight(Wearing(Source.Shadow, 5), build).Events, true);
            return with / (float)without;
        }
        Assert.Equal(Worth(Source.Shadow), Worth(Source.Body), precision: 2);
    }

    [Fact]
    public void test_body_grows_the_pool_swings_harder_carries_its_waste_and_lands_an_impact()
    {
        var build = BuildOf(Source.Spirit, "hammer_blow");

        // 2p — the pool.
        Assert.True(SoloBattle.ChampionHealth(build, Wearing(Source.Body, 2))
                    > SoloBattle.ChampionHealth(build, Plain(2)) * 1.08f, "two Body pieces grew no pool");

        // 3p — the swing, measured on a build whose only damage IS the swing.
        var swingBuild = BuildOf(Source.Spirit, "sign_call");
        var bare = Dealt(Fight(Plain(3), swingBuild).Events, false);
        var three = Dealt(Fight(Wearing(Source.Body, 3), swingBuild).Events, false);
        Assert.InRange(three / (float)bare, 1.15f, 1.25f);

        // 4p — the carry. A wave of creatures each far weaker than one blow: without a carry the
        // excess is thrown away, with it the next one is already hurt when its turn comes.
        int ClearedAt(Hunter h)
        {
            var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
            var (_, e) = SoloBattle.ResolveWave(champ, BuildOf(Source.Body, "hammer_blow"), h,
                Enumerable.Range(0, 6).Select(_ => new WaveCreature { MaxHealth = 40f, Health = 40f, Damage = 0f }).ToList(),
                enemyIntervalMs: 900, ExpeditionTuning.Default with { AutoAttackDamage = 0f }, new Random(11));
            return e.Where(x => x.Kind == BattleEventKind.EnemyDown).Max(x => x.AtMs);
        }
        // Counting the DEAD would prove nothing — six creatures die either way. What the carry buys is
        // the six dying sooner, because the waste of each blow arrives at the next one.
        Assert.True(ClearedAt(Wearing(Source.Body, 4)) < ClearedAt(Wearing(Source.Body, 3)),
                    "four Body pieces carried no overkill into the next enemy");

        // 5p — the IMPACT. The swing after a cast, against the same swing without the rung.
        var fourSwing = Dealt(Fight(Wearing(Source.Body, 4), BuildOf(Source.Body, "hammer_blow")).Events, false);
        var fiveSwing = Dealt(Fight(Wearing(Source.Body, 5), BuildOf(Source.Body, "hammer_blow")).Events, false);
        Assert.True(fiveSwing > fourSwing * 1.05f, "the fifth Body piece landed no IMPACT");
    }

    [Fact]
    public void test_machine_blunts_shields_softens_while_shielded_and_stops_one_bite()
    {
        int Taken(Hunter h, int hp = 100_000)
        {
            var (_, champ, _) = Fight(h, BuildOf(Source.Machine, "sign_call"), enemyHp: 5_000_000f, enemyDmg: 30f, hp: hp);
            return hp - (int)champ.Health;
        }

        // 2p — the flat blunting.
        Assert.True(Taken(Wearing(Source.Machine, 2)) < Taken(Plain(2)), "two Machine pieces blunted nothing");

        // 3p — the wave opens holding a shield, and the shield is 12% of the pool.
        var (events, _, _) = Fight(Wearing(Source.Machine, 3), BuildOf(Source.Machine, "sign_call"));
        var granted = events.Where(x => x.Kind == BattleEventKind.ShieldGained && x.AtMs == 0).Sum(x => x.Amount);
        Assert.InRange(granted, (int)(100_000 * 0.115f), (int)(100_000 * 0.125f));
        Assert.Empty(Fight(Plain(3), BuildOf(Source.Machine, "sign_call")).Events
                     .Where(x => x.Kind == BattleEventKind.ShieldGained));

        // 4p and 5p, read as EVERYTHING the wave landed — what the shield ate as well as what reached
        // the pool. Health alone cannot see them: the 3p shield is 12% of a hundred thousand and the
        // bites are thirty, so the pool never moves at all and three rungs would read identical.
        int Landed(Hunter h)
        {
            var (_, _, m) = Fight(h, BuildOf(Source.Machine, "sign_call"));
            return (int)m.ShieldAbsorbed + m.HealthDamage;
        }
        var three = Landed(Wearing(Source.Machine, 3));
        var four = Landed(Wearing(Source.Machine, 4));
        var five = Landed(Wearing(Source.Machine, 5));
        Assert.True(four < three, "the fourth Machine piece softened nothing while shielded");
        Assert.True(five < four, "the fifth Machine piece stopped no bite");

        // 5p PLATING pays the stopped bite back AS SHIELD, and never twice: one prevention a wave.
        var plating = Fight(Wearing(Source.Machine, 5), BuildOf(Source.Machine, "sign_call")).Events;
        Assert.True(plating.Count(x => x.Kind == BattleEventKind.ShieldGained) >= 2,
                    "PLATING granted no shield from the bite it stopped");
    }

    [Fact]
    public void test_mind_crits_more_crits_harder_focuses_and_reaches_certainty()
    {
        var build = BuildOf(Source.Mind, "hammer_blow");
        var bare = Dealt(Fight(Plain(2), build).Events, true);

        // Each rung strictly beats the one below it, on the same fight.
        var two = Dealt(Fight(Wearing(Source.Mind, 2), build).Events, true);
        var three = Dealt(Fight(Wearing(Source.Mind, 3), build).Events, true);
        var four = Dealt(Fight(Wearing(Source.Mind, 4), build).Events, true);
        var five = Dealt(Fight(Wearing(Source.Mind, 5), build).Events, true);

        Assert.True(two > bare, "two Mind pieces added no critical chance");
        Assert.True(three > two, "three Mind pieces added no critical damage");
        Assert.True(four > three, "four Mind pieces built no FOCUS");
        Assert.True(five > four, "five Mind pieces reached no CERTAINTY");
    }

    [Fact]
    public void test_nature_regains_heals_stronger_has_more_room_and_banks_the_overflow()
    {
        int Regained(Hunter h, int start, string skillId = "drain_wilt")
        {
            var champ = new Champion { MaxHealth = 100_000, Health = start };
            SoloBattle.ResolveWave(champ, BuildOf(Source.Nature, skillId), h,
                new List<WaveCreature> { new() { MaxHealth = 5_000_000f, Health = 5_000_000f, Damage = 0f } },
                enemyIntervalMs: 900, ExpeditionTuning.Default, new Random(11));
            return (int)champ.Health - start;
        }

        // 2p — the trickle, with no healing skill at all.
        Assert.True(Regained(Wearing(Source.Nature, 2), 50_000, "sign_call") > Regained(Plain(2), 50_000, "sign_call"),
                    "two Nature pieces regained nothing");
        // 3p and 4p — stronger healing, and more room for it.
        var three = Regained(Wearing(Source.Nature, 3), 20_000);
        var four = Regained(Wearing(Source.Nature, 4), 20_000);
        Assert.True(three > Regained(Wearing(Source.Nature, 2), 20_000), "three Nature pieces healed no harder");
        Assert.True(four > three, "four Nature pieces opened no room");

        // 5p — at FULL health the healing has nowhere to go, and that is exactly where it becomes shield.
        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var (_, e) = SoloBattle.ResolveWave(champ, BuildOf(Source.Nature, "hammer_blow"), Wearing(Source.Nature, 5),
            new List<WaveCreature> { new() { MaxHealth = 5_000_000f, Health = 5_000_000f, Damage = 0f } },
            enemyIntervalMs: 900, ExpeditionTuning.Default, new Random(11));
        Assert.True(e.Any(x => x.Kind == BattleEventKind.ShieldGained),
                    "five Nature pieces wasted the overheal instead of banking it");
        // And the shield is not healing: the pool did not move.
        Assert.Equal(100_000, champ.Health);
    }

    [Fact]
    public void test_shadow_finishes_the_weak_stores_shades_holds_two_and_strikes_again()
    {
        // A wave of many weak creatures: kills happen, so shades are made and spent.
        float Cleared(Hunter h)
        {
            var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
            var (_, e) = SoloBattle.ResolveWave(champ, BuildOf(Source.Shadow, "hammer_blow"), h,
                Enumerable.Range(0, 8).Select(_ => new WaveCreature { MaxHealth = 4_000f, Health = 4_000f, Damage = 0f }).ToList(),
                enemyIntervalMs: 900, ExpeditionTuning.Default with { AutoAttackDamage = 0f }, new Random(11));
            return Dealt(e, true);
        }

        var two = Cleared(Wearing(Source.Shadow, 2));
        var three = Cleared(Wearing(Source.Shadow, 3));
        var four = Cleared(Wearing(Source.Shadow, 4));
        var five = Cleared(Wearing(Source.Shadow, 5));

        Assert.True(two > Cleared(Plain(2)), "two Shadow pieces did nothing to the weakened");
        Assert.True(three > two, "three Shadow pieces stored no SHADE");
        Assert.True(four > three, "four Shadow pieces held no second shade");
        Assert.True(five > four, "five Shadow pieces cast no AFTERIMAGE");
    }

    /// <summary>An AFTERIMAGE is not a cast: it takes no beat, starts no cooldown, and is not use.</summary>
    [Fact]
    public void test_an_afterimage_is_damage_and_never_another_cast()
    {
        int[] CastTimes(Hunter h)
        {
            var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
            var (_, e) = SoloBattle.ResolveWave(champ, BuildOf(Source.Shadow, "hammer_blow"), h,
                Enumerable.Range(0, 8).Select(_ => new WaveCreature { MaxHealth = 4_000f, Health = 4_000f, Damage = 0f }).ToList(),
                enemyIntervalMs: 900, ExpeditionTuning.Default with { AutoAttackDamage = 0f }, new Random(11));
            return e.Where(x => x.Kind == BattleEventKind.Skill).Select(x => x.AtMs).ToArray();
        }
        // THE CADENCE IS UNTOUCHED. Counting casts would only measure how much sooner the wave ends;
        // what has to be true is that every cast falls at the same instant with the rung as without —
        // an afterimage takes no beat and starts no cooldown, so it cannot move a single one of them.
        // Against the SAME FIVE PIECES with no element on them, so the fifth item's own weight is not
        // mistaken for the rung's: a worn boot changes the fight whatever colour it is.
        var four = CastTimes(Plain(5));
        var five = CastTimes(Wearing(Source.Shadow, 5));
        var shared = Math.Min(four.Length, five.Length);
        Assert.Equal(four.Take(shared), five.Take(shared));
    }

    [Fact]
    public void test_spirit_acts_faster_opens_stronger_resonates_and_finds_harmony()
    {
        var build = BuildOf(Source.Spirit, "hammer_blow", "volley_spray", "field_mire", "drain_wilt");

        int Actions(Hunter h) => Fight(h, build).Events.Count(x => x.Kind == BattleEventKind.Strike);
        Assert.True(Actions(Wearing(Source.Spirit, 2)) > Actions(Plain(2)), "two Spirit pieces sped nothing");

        var two = Dealt(Fight(Wearing(Source.Spirit, 2), build).Events, true);
        var three = Dealt(Fight(Wearing(Source.Spirit, 3), build).Events, true);
        var four = Dealt(Fight(Wearing(Source.Spirit, 4), build).Events, true);
        var five = Dealt(Fight(Wearing(Source.Spirit, 5), build).Events, true);

        Assert.True(three > two, "three Spirit pieces did not open harder");
        Assert.True(four > three, "four Spirit pieces resonated with nothing");
        Assert.True(five > four, "five Spirit pieces found no HARMONY");
    }

    /// <summary>
    /// HARMONY asks for FOUR FILLED SLOTS, and that is a real cost rather than a special case.
    /// </summary>
    [Fact]
    public void test_harmony_needs_every_slot_filled()
    {
        var three = BuildOf(Source.Spirit, "hammer_blow", "volley_spray", "field_mire");
        var threeFour = Dealt(Fight(Wearing(Source.Spirit, 4), three).Events, true);
        var threeFive = Dealt(Fight(Wearing(Source.Spirit, 5), three).Events, true);
        Assert.Equal(threeFour, threeFive);
    }

    // ── the shapes a hunter can actually wear ──────────────────────────────────────────────────

    /// <summary>
    /// EIGHT SLOTS, so 5+3 · 4+4 · 3+3+2 are all real, and none of them is free money.
    /// </summary>
    [Fact]
    public void test_the_worn_shapes_five_three_four_four_and_three_three_two_all_open_their_rungs()
    {
        var eight = Enum.GetValues<GearSlot>();

        Hunter Shaped(params (Source Element, int Count)[] parts)
        {
            var h = new Hunter();
            var slot = 0;
            foreach (var (element, count) in parts)
                for (var i = 0; i < count; i++) h.Equip(Piece(eight[slot++], element, slot));
            return h;
        }

        var fiveThree = Shaped((Source.Machine, 5), (Source.Mind, 3));
        Assert.Equal(new[] { 2, 3, 4, 5 }, ElementSets.Active(fiveThree).Where(a => a.Element == Source.Machine).Select(a => a.Tier.Pieces));
        Assert.Equal(new[] { 2, 3 }, ElementSets.Active(fiveThree).Where(a => a.Element == Source.Mind).Select(a => a.Tier.Pieces));

        var fourFour = Shaped((Source.Body, 4), (Source.Nature, 4));
        Assert.Equal(new[] { 2, 3, 4 }, ElementSets.Active(fourFour).Where(a => a.Element == Source.Body).Select(a => a.Tier.Pieces));
        Assert.Equal(new[] { 2, 3, 4 }, ElementSets.Active(fourFour).Where(a => a.Element == Source.Nature).Select(a => a.Tier.Pieces));

        // Two rungs each from the threes and one from the two: five in all, from eight worn pieces.
        var threeThreeTwo = Shaped((Source.Shadow, 3), (Source.Spirit, 3), (Source.Mind, 2));
        Assert.Equal(5, ElementSets.Active(threeThreeTwo).Count());

        // No capstone from a spread shape, and every capstone from a committed one.
        Assert.DoesNotContain(ElementSets.Active(threeThreeTwo), a => a.Tier.Pieces == 5);
        Assert.Contains(ElementSets.Active(fiveThree), a => a.Tier.Pieces == 5);
    }

    [Fact]
    public void test_the_sets_ride_in_through_the_gear_shape_the_fight_reads()
    {
        // The whole chain: worn pieces → ElementSets.ShapeFor → GearShape.Of → SkillShape.Combine in the sim.
        var spirit = GearShape.Of(Wearing(Source.Spirit, 5));
        Assert.InRange(spirit.SkillRate, 1.059f, 1.061f);
        Assert.InRange(spirit.FirstActivationMagnitude, 1.199f, 1.201f);
        Assert.InRange(spirit.ResonanceRateCap, 0.079f, 0.081f);
        Assert.True(spirit.HarmonyCharges);

        var machine = GearShape.Of(Wearing(Source.Machine, 5));
        Assert.InRange(machine.WaveStartShieldFraction, 0.119f, 0.121f);
        Assert.InRange(machine.ShieldedDamageTaken, 0.899f, 0.901f);
        Assert.True(machine.PreventFirstDamagingBite);

        Assert.Equal(SkillShape.None.SkillRate, GearShape.Of(new Hunter()).SkillRate);
        Assert.Equal(SkillShape.None.FirstActivationMagnitude, GearShape.Of(new Hunter()).FirstActivationMagnitude);
    }

}
