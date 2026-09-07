using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Characters;

/// <summary>
/// EVERY CHARACTER'S INNATE HAS A LIVE CONSUMER — the same build fought with and without the
/// character, and something the player can see moves.
/// </summary>
/// <remarks>
/// <para>
/// The roster's cards promise an effect in a sentence ("Vows pay far more. Your Marks last longer
/// and hit harder."). A sentence with no consumer is the failure this codebase keeps finding —
/// built, tested green, and never reaching the fight. So this file does not read the character's
/// fields; it composes the build the way the game does (<see cref="BuildComposer.Compose"/>, once
/// with the character and once with nobody) and measures the fight, the haul, or the Vow factor.
/// </para>
/// <para>
/// Each test names the metric its card's sentence is ABOUT. If a card is rewritten, the test must be
/// rewritten with it — the point is that the sentence and the measurement agree.
/// </para>
/// </remarks>
public class character_innate_liveness_test
{
    // ── The harness: the game's own composition, with a road to every shared skill the test needs ──

    private static MasteryTree Teaching(params string[] skillIds)
    {
        var tree = new MasteryTree();
        tree.SetEarned(1_000);
        foreach (var skill in skillIds)
        {
            var road = MasteryCatalog.Nodes.FirstOrDefault(n => n.GrantsSkillId == skill)
                       ?? throw new InvalidOperationException($"no mastery road teaches {skill}");
            TakeWithPrereqs(tree, road);
        }
        return tree;
    }

    private static void TakeWithPrereqs(MasteryTree tree, MasteryNode node)
    {
        if (tree.IsTaken(node.Id)) return;
        // Any ONE prerequisite opens the node; walk the first that exists.
        if (node.Prereqs.Count > 0 && !node.Prereqs.Any(tree.IsTaken))
        {
            var pre = MasteryCatalog.ById(node.Prereqs[0]) ?? throw new InvalidOperationException(node.Prereqs[0]);
            TakeWithPrereqs(tree, pre);
        }
        if (node.SecondPrereqs.Count > 0 && !node.SecondPrereqs.Any(tree.IsTaken))
        {
            var pre = MasteryCatalog.ById(node.SecondPrereqs[0]) ?? throw new InvalidOperationException(node.SecondPrereqs[0]);
            TakeWithPrereqs(tree, pre);
        }
        Assert.True(tree.Take(node.Id), $"could not take {node.Id}");
    }

    // Four slots — two that take an action and two that do not — so a trap or a field composes
    // into a passive slot the way it does in play; a one-slot build has no passive slot at all.
    private static Build Compose(Character? c, string[] skills, Source source = Source.Body, string? vowId = null,
                                 int slotCapacity = 4)
    {
        var picks = skills.Select(s => new BuildComposer.SkillPick(source, vowId, null, s)).ToList();
        var known = vowId is null ? Array.Empty<Vow>() : new[] { Vows.ById(vowId)! };
        var build = BuildComposer.Compose(Teaching(skills), c, picks, Array.Empty<string>(), slotCapacity,
                                          knownVows: known);
        Assert.Equal(skills.Length, build.Skills.Count);
        return build;
    }

    private static List<WaveCreature> Wave(int count, float health, float damage, float defense = 0f)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = health, Health = health, Damage = damage, Defense = defense,
        }).ToList();

    private sealed record Run(WaveMetrics Metrics, Champion Champ, WaveOutcome Outcome, List<BattleEvent> Events);

    private static Run Fight(Build build, List<WaveCreature> creatures, int hp = 200_000,
                             int intervalMs = 900, ExpeditionTuning? tuning = null)
    {
        var metrics = new WaveMetrics();
        var champ = new Champion { MaxHealth = hp, Health = hp };
        var (outcome, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), creatures, intervalMs,
            tuning ?? ExpeditionTuning.Default, new Random(11), metrics: metrics);
        return new Run(metrics, champ, outcome, events);
    }

    private static Character Of(string id) => CharacterRoster.Get(id);

    /// <summary>The fight without the basic swing, so a card about SKILLS is measured on skills alone.</summary>
    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static (Run With, Run Without) Pair(string characterId, string[] skills, List<WaveCreature> creatures,
                                                int hp = 200_000, int intervalMs = 900, Source source = Source.Body,
                                                ExpeditionTuning? tuning = null)
        => (Fight(Compose(Of(characterId), skills, source), creatures, hp, intervalMs, tuning),
            Fight(Compose(null, skills, source), creatures, hp, intervalMs, tuning));

    // ── Every card, one measurement each ─────────────────────────────────────────────────────────

    [Fact]
    public void test_the_seeker_every_skill_hits_harder()
    {
        var (with, without) = Pair("seeker", new[] { "hammer_blow" }, Wave(1, 1_000_000f, 1f), tuning: NoSwing);
        Assert.True(with.Metrics.DeliveredDamage > without.Metrics.DeliveredDamage * 1.05f,
                    $"EVEN HAND: {with.Metrics.DeliveredDamage} vs {without.Metrics.DeliveredDamage}");
    }

    [Fact]
    public void test_the_anvil_leaves_weight_in_a_standing_enemy()
    {
        // An enemy nothing kills in one blow: a third of every hit stays in it and the next hit lands
        // it too, so the same window deals more — and the sim says each store and release as it goes.
        var (with, without) = Pair("anvil", new[] { "hammer_blow" }, Wave(1, 1_000_000f, 1f));
        Assert.True(with.Metrics.DeadweightReleases > 0, "DEADWEIGHT never released");
        Assert.Contains(with.Events, e => e.Kind == BattleEventKind.DeadweightStored);
        Assert.Contains(with.Events, e => e.Kind == BattleEventKind.DeadweightReleased);
        Assert.True(with.Metrics.DeliveredDamage > without.Metrics.DeliveredDamage,
                    $"DEADWEIGHT: delivered {with.Metrics.DeliveredDamage} vs {without.Metrics.DeliveredDamage}");
    }

    [Fact]
    public void test_the_chorus_grows_with_the_crowd()
    {
        var (with, without) = Pair("chorus", new[] { "hammer_blow" }, Wave(5, 1_000_000f, 1f));
        Assert.True(with.Metrics.DeliveredDamage > without.Metrics.DeliveredDamage * 1.15f,
                    $"MANY MOUTHS: {with.Metrics.DeliveredDamage} vs {without.Metrics.DeliveredDamage}");
    }

    [Fact]
    public void test_the_metronome_opens_without_waiting_and_doubles_the_first_hit()
    {
        var (with, without) = Pair("metronome", new[] { "hammer_blow" }, Wave(1, 1_000_000f, 1f));
        var firstWith = with.Events.First(e => e.Kind == BattleEventKind.Skill).AtMs;
        var firstWithout = without.Events.First(e => e.Kind == BattleEventKind.Skill).AtMs;
        Assert.True(firstWith < firstWithout, $"FIRST BEAT: first cast at {firstWith} vs {firstWithout}");
        Assert.True(with.Metrics.DeliveredDamage > without.Metrics.DeliveredDamage,
                    $"FIRST BEAT: {with.Metrics.DeliveredDamage} vs {without.Metrics.DeliveredDamage}");
    }

    [Fact]
    public void test_the_unbroken_survives_the_hit_that_would_kill_it()
    {
        // A wave that bites for more than the pool: without SECOND WIND the champion falls; with it,
        // the first lethal hit leaves 1 health and the fight goes on.
        var (with, without) = Pair("unbroken", new[] { "hammer_blow" }, Wave(1, 1_000_000f, 5_000f), hp: 1_000);
        Assert.Contains(with.Events, e => e.Kind == BattleEventKind.Undying);
        Assert.DoesNotContain(without.Events, e => e.Kind == BattleEventKind.Undying);
        Assert.True(Fell(with) > Fell(without), $"SECOND WIND: fell at {Fell(with)} vs {Fell(without)}");
    }

    [Fact]
    public void test_the_falling_tower_trades_the_first_hit_for_every_later_one()
    {
        var (with, without) = Pair("tower", new[] { "hammer_blow" }, Wave(1, 1_000_000f, 1f), tuning: NoSwing);
        var strikesWith = with.Events.Where(e => e.Kind == BattleEventKind.Strike && e.FromSkill).Select(e => e.Amount).ToList();
        var strikesWithout = without.Events.Where(e => e.Kind == BattleEventKind.Strike && e.FromSkill).Select(e => e.Amount).ToList();
        Assert.True(strikesWith.Count > 2 && strikesWithout.Count > 2);
        Assert.True(strikesWith[0] < strikesWithout[0], $"MOMENTUM: first hit {strikesWith[0]} vs {strikesWithout[0]}");
        Assert.True(strikesWith.Skip(1).Sum() > strikesWithout.Skip(1).Sum(), "MOMENTUM: later hits did not grow");
    }

    [Fact]
    public void test_the_quiver_shoots_again_on_a_kill_and_casts_sooner()
    {
        var (with, without) = Pair("quiver", new[] { "volley_spray" }, Wave(8, 30f, 1f), source: Source.Mind);
        var castsWith = with.Events.Count(e => e.Kind == BattleEventKind.Skill);
        var castsWithout = without.Events.Count(e => e.Kind == BattleEventKind.Skill);
        Assert.True(castsWith > castsWithout || Clear(with) < Clear(without),
                    $"LOOSE AGAIN: {castsWith} casts vs {castsWithout}, cleared at {Clear(with)} vs {Clear(without)}");
    }

    [Fact]
    public void test_the_thornwall_takes_less_and_traps_harder()
    {
        var (with, without) = Pair("thornwall", new[] { "snare_jaws" }, Wave(3, 1_000_000f, 60f), source: Source.Shadow);
        Assert.True(with.Metrics.HealthDamage < without.Metrics.HealthDamage,
                    $"REPRISAL: took {with.Metrics.HealthDamage} vs {without.Metrics.HealthDamage}");
        // JAWS answers every bite with a share of it; the trap's answer is what REPRISAL's second
        // clause is about, and the sim books it as reflected damage.
        Assert.True(with.Metrics.ReflectedDamage > without.Metrics.ReflectedDamage,
                    $"REPRISAL: trap damage {with.Metrics.ReflectedDamage} vs {without.Metrics.ReflectedDamage}");
    }

    [Fact]
    public void test_the_oathbound_makes_a_kept_vow_pay_more_in_the_resolved_factor_and_the_fight()
    {
        // VOW OF COMPLETION asks every slot to be filled — one slot, one skill, kept.
        var with = Compose(Of("oathbound"), new[] { "hammer_blow" }, vowId: "vow_complete", slotCapacity: 1);
        var without = Compose(null, new[] { "hammer_blow" }, vowId: "vow_complete", slotCapacity: 1);
        var hunter = new Hunter();
        var ctxWith = SoloBattle.DescribeBuild(with, hunter);
        var ctxWithout = SoloBattle.DescribeBuild(without, hunter);
        Assert.True(Vows.IsActive(with.Vows[0], ctxWith), "the fixture's vow is not kept");

        var factorWith = Vows.CombinedFactor(with.Vows, ctxWith, with.Shape.VowPowerMultiplier);
        var factorWithout = Vows.CombinedFactor(without.Vows, ctxWithout, without.Shape.VowPowerMultiplier);
        Assert.True(factorWithout > 1f, "a kept vow pays something");
        Assert.True(factorWith > factorWithout, $"TWICE SWORN: vows pay x{factorWith} vs x{factorWithout}");
        // The number a screen must print for this vow is the resolved one, and it moves with the character.
        Assert.Equal(factorWith, Vows.ResolvedMultiplier(with.Vows[0], with.Shape.VowPowerMultiplier), 3);

        var fightWith = Fight(with, Wave(1, 1_000_000f, 1f));
        var fightWithout = Fight(without, Wave(1, 1_000_000f, 1f));
        Assert.True(fightWith.Metrics.DeliveredDamage > fightWithout.Metrics.DeliveredDamage,
                    $"TWICE SWORN in the fight: {fightWith.Metrics.DeliveredDamage} vs {fightWithout.Metrics.DeliveredDamage}");
    }

    [Fact]
    public void test_the_oathbound_marks_last_longer_and_hit_harder()
    {
        // CALL opens a Mark; the basic swing lands inside it. A longer, deeper window means more of
        // the swings are amplified — the second clause of the card, measured through the fight alone.
        var (with, without) = Pair("oathbound", new[] { "sign_call" }, Wave(1, 1_000_000f, 1f), source: Source.Mind);
        Assert.True(with.Metrics.DeliveredDamage > without.Metrics.DeliveredDamage,
                    $"TWICE SWORN (marks): {with.Metrics.DeliveredDamage} vs {without.Metrics.DeliveredDamage}");
    }

    [Fact]
    public void test_the_magpie_brings_home_more_and_rarer()
    {
        var with = Compose(Of("magpie"), new[] { "hammer_blow" });
        var without = Compose(null, new[] { "hammer_blow" });
        var hunter = new Hunter();
        var modsWith = with.Resolve(hunter);
        var modsWithout = without.Resolve(hunter);
        Assert.True(modsWith.Haul > modsWithout.Haul * 1.2f, $"FULL POCKETS: haul {modsWith.Haul} vs {modsWithout.Haul}");
        Assert.True(modsWith.Rarity > modsWithout.Rarity * 1.1f, $"FULL POCKETS: rarity {modsWith.Rarity} vs {modsWithout.Rarity}");
    }

    [Fact]
    public void test_every_character_in_the_roster_has_a_test_here()
    {
        // The roster grows; this file must grow with it. A character with no test above is a card
        // whose sentence nobody measured.
        var covered = new HashSet<string>
        {
            "seeker", "anvil", "chorus", "metronome", "unbroken", "tower", "quiver", "thornwall", "oathbound", "magpie",
        };
        foreach (var c in CharacterRoster.All)
            Assert.Contains(c.Id, covered);
    }

    // ── Readings ──────────────────────────────────────────────────────────────────────────────────

    private static int Clear(Run r)
        => r.Events.Where(e => e.Kind == BattleEventKind.EnemyDown).Select(e => e.AtMs).DefaultIfEmpty(int.MaxValue).Max();

    private static int Fell(Run r)
        => r.Events.Where(e => e.Kind == BattleEventKind.Down).Select(e => e.AtMs).DefaultIfEmpty(int.MaxValue).Min();
}
