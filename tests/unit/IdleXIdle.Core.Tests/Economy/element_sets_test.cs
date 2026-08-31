using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
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
        Family = 0,   // pinned: a weapon's family (and so its favoured Forms) otherwise hashes off the id
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

    private static EquippedSkill Sk(Source src, Form form)
        => new(new WovenAbility { Name = form.ToString(), Source = src, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    private static Build BuildOf(Source src, params Form[] forms)
    {
        var b = new Build();
        foreach (var f in forms) b.Weave(Sk(src, f));
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
                Assert.True(t.Line.Length <= 70, $"{el} {t.Pieces}: '{t.Line}' is {t.Line.Length} characters");
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

    [Fact]
    public void test_two_pieces_make_the_elements_own_skills_hit_harder_and_no_other()
    {
        var bare = Fight(Plain(2), BuildOf(Source.Spirit, Form.Strike));
        var two = Fight(Wearing(Source.Spirit, 2), BuildOf(Source.Spirit, Form.Strike));
        var other = Fight(Wearing(Source.Spirit, 2), BuildOf(Source.Body, Form.Strike));
        var bareOther = Fight(Plain(2), BuildOf(Source.Body, Form.Strike));

        Assert.True(Dealt(two.Events, true) > Dealt(bare.Events, true) * 1.05f, $"two Spirit pieces did not make Spirit skills hit harder: {Dealt(two.Events, true)} vs plain {Dealt(bare.Events, true)}");
        Assert.Equal(Dealt(bareOther.Events, true), Dealt(other.Events, true));   // a Body skill gets nothing from Spirit pieces
        Assert.Equal(Dealt(bare.Events, false), Dealt(two.Events, false));       // the swing gets nothing from the own-skill rung
    }

    [Fact]
    public void test_four_pieces_pay_twice_what_two_do()
    {
        var plain2 = Dealt(Fight(Plain(2), BuildOf(Source.Shadow, Form.Strike)).Events, true);
        var plain4 = Dealt(Fight(Plain(4), BuildOf(Source.Shadow, Form.Strike)).Events, true);
        var two = Dealt(Fight(Wearing(Source.Shadow, 2), BuildOf(Source.Shadow, Form.Strike)).Events, true);
        var four = Dealt(Fight(Wearing(Source.Shadow, 4), BuildOf(Source.Shadow, Form.Strike)).Events, true);
        Assert.InRange(two / (float)plain2, 1.06f, 1.10f);
        Assert.InRange(four / (float)plain4, 1.14f, 1.18f);
    }

    [Fact]
    public void test_body_three_grows_the_pool_and_five_swings_harder()
    {
        var h5 = Wearing(Source.Body, 5);
        var build = BuildOf(Source.Spirit, Form.Mark);   // a build that deals nothing of its own: the swing is the measure
        Assert.True(SoloBattle.ChampionHealth(build, Wearing(Source.Body, 3)) > SoloBattle.ChampionHealth(build, Plain(3)) * 1.08f);
        var bare = Dealt(Fight(Plain(5), build).Events, false);
        var five = Dealt(Fight(h5, build).Events, false);
        Assert.InRange(five / (float)bare, 1.20f, 1.30f);
    }

    [Fact]
    public void test_machine_three_blunts_every_bite_and_five_waives_the_first()
    {
        int Taken(Hunter h, int hp = 100_000)
        {
            var (_, champ, _) = Fight(h, BuildOf(Source.Machine, Form.Mark), enemyHp: 5_000_000f, enemyDmg: 30f, hp: hp);
            return hp - (int)champ.Health;
        }
        Assert.True(Taken(Wearing(Source.Machine, 3)) < Taken(Plain(3)), "three Machine pieces blunted nothing");
        // The fifth piece's own worth is measured against the fifth plain piece's, so boots stay out of it.
        var fourGain = Taken(Plain(4)) - Taken(Wearing(Source.Machine, 4));
        var fiveGain = Taken(Plain(5)) - Taken(Wearing(Source.Machine, 5));
        Assert.True(fiveGain > fourGain, "the fifth Machine piece waived no bite");
    }

    [Fact]
    public void test_mind_three_crits_more_and_five_stretches_the_mark()
    {
        var bare = Fight(Plain(3), BuildOf(Source.Mind, Form.Strike));
        var three = Fight(Wearing(Source.Mind, 3), BuildOf(Source.Mind, Form.Strike));
        // more crits land as bigger blows: the mean skill hit rises beyond the 2-piece rung's 8%
        Assert.True(Dealt(three.Events, true) > Dealt(bare.Events, true) * 1.09f, "three Mind pieces changed no crit");
        // The fifth piece (boots, no damage of their own) stretches the window: the second Strike after a
        // MARK lands lit at 3.75 s where the plain 2.5 s window had closed.
        var marked = Fight(Wearing(Source.Mind, 4), BuildOf(Source.Mind, Form.Mark, Form.Strike, Form.Strike));
        var stretched = Fight(Wearing(Source.Mind, 5), BuildOf(Source.Mind, Form.Mark, Form.Strike, Form.Strike));
        Assert.True(Dealt(stretched.Events, true) > Dealt(marked.Events, true) * 1.03f, "five Mind pieces stretched no window");
    }

    [Fact]
    public void test_nature_three_regenerates_and_five_leeches()
    {
        var (_, bareChamp, _) = Fight(new Hunter(), BuildOf(Source.Nature, Form.Strike), enemyDmg: 0f, hp: 100_000);
        var champ3 = new Champion { MaxHealth = 100_000, Health = 50_000 };
        SoloBattle.ResolveWave(champ3, BuildOf(Source.Nature, Form.Mark), Wearing(Source.Nature, 3),
            new List<WaveCreature> { new() { MaxHealth = 5_000_000f, Health = 5_000_000f, Damage = 0f } },
            enemyIntervalMs: 900, ExpeditionTuning.Default, new Random(11));
        Assert.True(champ3.Health > 50_000 + 100_000 * 0.003f * 60, "three Nature pieces regained nothing");

        var champ5 = new Champion { MaxHealth = 100_000, Health = 50_000 };
        SoloBattle.ResolveWave(champ5, BuildOf(Source.Nature, Form.Strike), Wearing(Source.Nature, 5),
            new List<WaveCreature> { new() { MaxHealth = 5_000_000f, Health = 5_000_000f, Damage = 0f } },
            enemyIntervalMs: 900, ExpeditionTuning.Default, new Random(11));
        var champ4 = new Champion { MaxHealth = 100_000, Health = 50_000 };
        SoloBattle.ResolveWave(champ4, BuildOf(Source.Nature, Form.Strike), Wearing(Source.Nature, 4),
            new List<WaveCreature> { new() { MaxHealth = 5_000_000f, Health = 5_000_000f, Damage = 0f } },
            enemyIntervalMs: 900, ExpeditionTuning.Default, new Random(11));
        Assert.True(champ5.Health > champ4.Health, "the fifth Nature piece healed nothing");
        _ = bareChamp;
    }

    [Fact]
    public void test_shadow_three_finishes_the_weak_and_five_refunds_a_beat()
    {
        float Weakened(Hunter h)
        {
            var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
            var (_, e) = SoloBattle.ResolveWave(champ, BuildOf(Source.Shadow, Form.Strike), h,
                new List<WaveCreature> { new() { MaxHealth = 100_000_000f, Health = 20_000_000f, Damage = 0f } },
                enemyIntervalMs: 900, ExpeditionTuning.Default, new Random(11));
            return Dealt(e, true);
        }
        Assert.True(Weakened(Wearing(Source.Shadow, 3)) > Weakened(Wearing(Source.Shadow, 2)) * 1.08f, "three Shadow pieces did nothing to the weakened");

        int Casts(Hunter h)
        {
            var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
            var (_, e) = SoloBattle.ResolveWave(champ, BuildOf(Source.Shadow, Form.Strike), h,
                Enumerable.Range(0, 5).Select(_ => new WaveCreature { MaxHealth = 30f, Health = 30f, Damage = 0f }).ToList(),
                enemyIntervalMs: 900, ExpeditionTuning.Default with { AutoAttackDamage = 0f }, new Random(11));
            return e.Count(x => x.Kind == BattleEventKind.Skill);
        }
        // Five kills, each refunding a beat: the five Strikes land in fewer beats than without.
        int KillMs(Hunter h)
        {
            var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
            var (_, e) = SoloBattle.ResolveWave(champ, BuildOf(Source.Shadow, Form.Strike), h,
                Enumerable.Range(0, 5).Select(_ => new WaveCreature { MaxHealth = 30f, Health = 30f, Damage = 0f }).ToList(),
                enemyIntervalMs: 900, ExpeditionTuning.Default with { AutoAttackDamage = 0f }, new Random(11));
            return e.Where(x => x.Kind == BattleEventKind.EnemyDown).Max(x => x.AtMs);
        }
        Assert.True(KillMs(Wearing(Source.Shadow, 5)) < KillMs(Wearing(Source.Shadow, 4)), "the fifth Shadow piece refunded no beat");
        _ = Casts(new Hunter());
    }

    [Fact]
    public void test_spirit_three_acts_faster_and_five_opens_harder()
    {
        int Actions(Hunter h)
        {
            var (e, _, _) = Fight(h, BuildOf(Source.Spirit, Form.Strike));
            return e.Count(x => x.Kind == BattleEventKind.Strike);
        }
        Assert.True(Actions(Wearing(Source.Spirit, 3)) > Actions(Wearing(Source.Spirit, 2)), "three Spirit pieces sped nothing");

        int FirstHit(Hunter h)
        {
            var (e, _, _) = Fight(h, BuildOf(Source.Spirit, Form.Strike));
            return e.First(x => x.Kind == BattleEventKind.Strike && x.FromSkill).Amount;
        }
        Assert.InRange(FirstHit(Wearing(Source.Spirit, 5)) / (float)FirstHit(Wearing(Source.Spirit, 4)), 1.20f, 1.30f);
    }

    [Fact]
    public void test_the_sets_ride_in_through_the_gear_shape_the_fight_reads()
    {
        // The whole chain: worn pieces → ElementSets.ShapeFor → GearShape.Of → SkillShape.Combine in the sim.
        var shape = GearShape.Of(Wearing(Source.Spirit, 5));
        Assert.InRange(shape.SkillRate, 1.059f, 1.061f);
        Assert.InRange(shape.FirstCastMultiplier, 1.249f, 1.251f);
        Assert.InRange(shape.SourceBonus[Source.Spirit], 0.159f, 0.161f);
        Assert.Equal(SkillShape.None.SkillRate, GearShape.Of(new Hunter()).SkillRate);
    }
}
