using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Tests.Builds;
using Xunit;

namespace IdleXIdle.Core.Tests.Characters;

/// <summary>
/// DEADWEIGHT — THE ANVIL's innate, as the rule it is now (2026-09-07): a primary direct hit (a skill's
/// or the swing) that leaves its target standing leaves a share of its damage in it, and the next such hit on that
/// target lands it too.
/// </summary>
/// <remarks>
/// <para>
/// It was an overkill carry — a third of a kill's spill into the next enemy — on the same generic
/// field BODY's fourth rung, BREAKTHROUGH and CLEAN CUT write, combined by MAX, so the innate added
/// nothing beside any of them and nothing at all against a lone enemy. These tests hold the redesign
/// to its law on builds a player can weave (<see cref="Reachable.Compose"/>): it stores only on a
/// surviving target, one amount per target, replaced not added; the next primary hit releases it,
/// once; a release stores nothing and releases nothing; a carried hit arms nothing; death clears it;
/// and it lives beside every carry rule in the game rather than under it.
/// </para>
/// </remarks>
public class deadweight_test
{
    private static readonly Character Anvil = CharacterRoster.Get("anvil");
    private static readonly Character NoPassive = Anvil with { Name = "NO PASSIVE", Shape = SkillShape.None };
    private static float Share => Anvil.Shape.DeadweightShare;

    private const string Hardface = "sig_anvil_hardface";

    /// <summary>A fight that ends on the clock, never on a stall: the boss below cannot die.</summary>
    private static readonly ExpeditionTuning Window = ExpeditionTuning.Default with { TickCeilingMs = 60_000 };
    /// <summary>Casts only — a skill's opener waits one cooldown, so the swing would otherwise own the first six beats.</summary>
    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };
    private static readonly ExpeditionTuning WindowNoSwing = Window with { AutoAttackDamage = 0f };

    private sealed record Run(WaveOutcome Outcome, List<BattleEvent> Events, WaveMetrics Metrics, List<WaveCreature> Wave);

    private static List<WaveCreature> Boss(float health = 1e9f) => new() { new WaveCreature { MaxHealth = health, Health = health, Damage = 0f, Defense = 0f } };

    private static List<WaveCreature> Many(int count, float health)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature { MaxHealth = health, Health = health, Damage = 0f, Defense = 0f }).ToList();

    private static Build Compose(Character c, params Reachable.Slot[] slots)
        => Reachable.Compose(c, slots, slotCapacity: Math.Max(slots.Length, 3), knownVows: Vows.Catalog);

    private static Run Fight(Build build, List<WaveCreature> wave, ExpeditionTuning? tuning = null, Hunter? hunter = null)
    {
        var champ = new Champion { MaxHealth = 200_000, Health = 200_000 };
        var metrics = new WaveMetrics();
        var (outcome, events) = SoloBattle.ResolveWave(champ, build, hunter ?? new Hunter(), wave,
                                                       enemyIntervalMs: 900, tuning ?? Window, new Random(7), metrics: metrics);
        return new Run(outcome, events, metrics, wave);
    }

    private static List<BattleEvent> Of(Run r, BattleEventKind kind) => r.Events.Where(e => e.Kind == kind).ToList();

    /// <summary>Four BODY pieces of no class (an "old make" anyone may wear) — the fourth rung's carry, 0.35.</summary>
    private static Hunter WearingBody(int pieces)
    {
        var hunter = new Hunter();
        var types = new[] { ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring };
        for (var i = 0; i < pieces; i++)
            hunter.Equip(new ItemInstance { InstanceId = $"body{i}", BaseType = types[i], Rarity = Rarity.Common, SellValue = 1, Element = Source.Body, ItemLevel = 1 });
        return hunter;
    }

    // ── A. A durable enemy: stored, then released by the next primary hit — and the boss feels it ──

    [Fact]
    public void test_deadweight_a_surviving_boss_stores_the_hit_and_the_next_hit_releases_it()
    {
        var with = Fight(Compose(Anvil, new Reachable.Slot(Hardface)), Boss());
        var without = Fight(Compose(NoPassive, new Reachable.Slot(Hardface)), Boss());

        Assert.True(with.Metrics.DeadweightStores > 0, "no hit stored DEADWEIGHT on a boss that survived every one");
        Assert.True(with.Metrics.DeadweightReleases > 0, "nothing released the DEADWEIGHT a boss was holding");
        Assert.True(with.Metrics.DeadweightDamage > 0f);
        Assert.True(with.Metrics.DeadweightReleases <= with.Metrics.DeadweightStores, "more releases than stores");
        Assert.True(with.Metrics.DeliveredDamage > without.Metrics.DeliveredDamage,
                    $"the boss took no more from THE ANVIL ({with.Metrics.DeliveredDamage:N0}) than from nobody ({without.Metrics.DeliveredDamage:N0})");
        Assert.Equal(0, without.Metrics.DeadweightStores);
        Assert.Equal(0, without.Metrics.DeadweightReleases);

        // Each release lands exactly the amount the previous store left, and the released damage is
        // the passive's whole contribution over the control.
        var stored = Of(with, BattleEventKind.DeadweightStored).Select(e => e.Amount).ToList();
        var released = Of(with, BattleEventKind.DeadweightReleased).Select(e => e.Amount).ToList();
        for (var i = 0; i < released.Count; i++)
            Assert.True(Math.Abs(released[i] - stored[i]) <= 1, $"release {i} landed {released[i]} but the store before it was {stored[i]}");

        // THE SWING IS A PRIMARY DIRECT HIT TOO — it stores and it releases, as a cast does. A swing
        // and a cast never share a beat, so an instant names which one struck: stores and releases
        // land at swing instants, and stores land at cast-only instants.
        var strikes = Of(with, BattleEventKind.Strike);
        var swingAt = strikes.Where(e => !e.FromSkill).Select(e => e.AtMs).ToHashSet();
        var castOnlyAt = strikes.Where(e => e.FromSkill).Select(e => e.AtMs).Where(t => !swingAt.Contains(t)).ToHashSet();
        Assert.NotEmpty(swingAt);
        Assert.NotEmpty(castOnlyAt);
        Assert.Contains(Of(with, BattleEventKind.DeadweightStored), e => swingAt.Contains(e.AtMs));
        Assert.Contains(Of(with, BattleEventKind.DeadweightReleased), e => swingAt.Contains(e.AtMs));
        Assert.Contains(Of(with, BattleEventKind.DeadweightStored), e => castOnlyAt.Contains(e.AtMs));

        // The state is readable off the creature: what it holds now is the last store the clock left unreleased.
        var boss = with.Wave[0];
        Assert.True(boss.Alive);
        Assert.Equal(stored.Count - released.Count, boss.StoredDeadweight > 0f ? 1 : 0);
        if (boss.StoredDeadweight > 0f) Assert.True(Math.Abs(boss.StoredDeadweight - stored[^1]) <= 1f);
    }

    // ── B. A one-shot kill stores nothing, for anyone ──

    [Fact]
    public void test_deadweight_a_kill_stores_nothing_and_never_moves_to_another_enemy()
    {
        var run = Fight(Compose(Anvil, new Reachable.Slot(Hardface, "UPSET"), new Reachable.Slot("hammer_blow", "FLATTEN")), Many(6, 1f),
                        ExpeditionTuning.Default);
        Assert.Equal(WaveOutcome.Cleared, run.Outcome);
        Assert.Equal(0, run.Metrics.DeadweightStores);
        Assert.Equal(0, run.Metrics.DeadweightReleases);
        Assert.Empty(Of(run, BattleEventKind.DeadweightStored));
        Assert.All(run.Wave, c => Assert.Equal(0f, c.StoredDeadweight));
    }

    // ── C. Storing again before release is bounded: one amount, replaced, never added ──

    [Fact]
    public void test_deadweight_repeated_stores_stay_bounded_by_one_hits_share()
    {
        // UPSET lands twice on every enemy per cast, and every cast comes round again: many stores per
        // creature, none of them ever bigger than a third of the largest single primary hit it took.
        var run = Fight(Compose(Anvil, new Reachable.Slot(Hardface, "UPSET")), Many(3, 1e9f), WindowNoSwing);
        var stores = Of(run, BattleEventKind.DeadweightStored);
        var releases = Of(run, BattleEventKind.DeadweightReleased);
        Assert.True(stores.Count >= 6, $"only {stores.Count} stores — the fixture is not posing repeated stores");

        for (var slot = 0; slot < 3; slot++)
        {
            var releasedOn = releases.Where(e => e.Slot == slot).Select(e => e.Amount).ToHashSet();
            // The largest PRIMARY hit on this creature: a Strike whose amount was not itself a release.
            var primaryHits = run.Events.Where(e => e.Kind == BattleEventKind.Strike && e.Slot == slot && !releasedOn.Contains(e.Amount)).Select(e => e.Amount);
            var biggest = primaryHits.Max();
            var ceiling = biggest * Share + 1f;
            foreach (var s in stores.Where(e => e.Slot == slot))
                Assert.True(s.Amount <= ceiling, $"creature {slot} held {s.Amount} — more than a third of its biggest hit ({biggest}); stores added up");
            Assert.True(run.Wave[slot].StoredDeadweight <= ceiling);
        }
    }

    // ── D. One release per store, and the state is consumed ──

    [Fact]
    public void test_deadweight_is_released_exactly_once_and_consumed()
    {
        // No swing, one single-target skill: cast N stores, cast N+1 releases it and stores its own.
        var run = Fight(Compose(Anvil, new Reachable.Slot(Hardface)), Boss(), WindowNoSwing);
        var stores = Of(run, BattleEventKind.DeadweightStored);
        var releases = Of(run, BattleEventKind.DeadweightReleased);
        Assert.True(stores.Count >= 3, $"only {stores.Count} casts landed in the window");
        Assert.Equal(stores.Count - 1, releases.Count);   // the last store waits for a hit the clock never gave
        for (var i = 0; i < releases.Count; i++)
        {
            Assert.True(Math.Abs(releases[i].Amount - stores[i].Amount) <= 1);
            // The release is the store's own instant, and comes AFTER the store of the hit that released it.
            Assert.Equal(stores[i + 1].AtMs, releases[i].AtMs);
            Assert.True(run.Events.IndexOf(releases[i]) > run.Events.IndexOf(stores[i + 1]));
        }
        Assert.True(Math.Abs(run.Metrics.DeadweightDamage - releases.Sum(e => (float)e.Amount)) <= releases.Count);
    }

    // ── E. A release cannot store, and cannot release itself ──

    [Fact]
    public void test_deadweight_supplemental_damage_neither_stores_nor_releases()
    {
        var run = Fight(Compose(Anvil, new Reachable.Slot(Hardface)), Boss(), WindowNoSwing);
        var strikes = Of(run, BattleEventKind.Strike).Count;
        var stores = run.Metrics.DeadweightStores;
        var releases = run.Metrics.DeadweightReleases;
        // Every Strike on the boss is a primary hit or a release; every primary hit stored (the boss
        // survived all of them). If a release could store, stores would exceed the primary hits;
        // if a release could release, releases would exceed stores - 1.
        Assert.Equal(strikes - releases, stores);
        Assert.Equal(stores - 1, releases);
        // And a release lands at the same instant as the primary hit that released it, never later
        // on its own — it is not a second store waiting for a third hit.
        foreach (var r in Of(run, BattleEventKind.DeadweightReleased))
            Assert.Contains(run.Events, e => e.Kind == BattleEventKind.DeadweightStored && e.AtMs == r.AtMs);
    }

    // ── F / G / H. Beside every carry rule: the carry still carries, DEADWEIGHT still stores ──

    [Fact]
    public void test_deadweight_lives_beside_the_body_sets_carry()
    {
        var build = Compose(Anvil, new Reachable.Slot(Hardface, "UPSET"), new Reachable.Slot("hammer_blow", "FLATTEN"));
        var control = Compose(NoPassive, new Reachable.Slot(Hardface, "UPSET"), new Reachable.Slot("hammer_blow", "FLATTEN"));

        // The fourth rung's carry (0.35) is worn by both arms and carries on a swarm...
        var swarmWith = Fight(build, Many(8, 30f), NoSwing, WearingBody(4));
        var swarmWithout = Fight(control, Many(8, 30f), NoSwing, WearingBody(4));
        Assert.True(swarmWith.Metrics.CarriesLanded > 0 && swarmWithout.Metrics.CarriesLanded > 0, "BODY's carry stopped carrying");
        Assert.Equal(swarmWithout.Metrics.CarriedDamage, swarmWith.Metrics.CarriedDamage, 1);   // the passive is not a carry any more

        // ...and DEADWEIGHT stores and releases on a boss with the same gear on.
        var bossWith = Fight(build, Boss(), Window, WearingBody(4));
        var bossWithout = Fight(control, Boss(), Window, WearingBody(4));
        Assert.True(bossWith.Metrics.DeadweightReleases > 0, "DEADWEIGHT vanished under four BODY pieces");
        Assert.Equal(0, bossWithout.Metrics.DeadweightReleases);
        Assert.True(bossWith.Metrics.DeliveredDamage > bossWithout.Metrics.DeliveredDamage);
    }

    [Fact]
    public void test_deadweight_lives_beside_breakthrough()
    {
        // The blow is woven FIRST: slot order is cast order, and UPSET's whole-wave falls would wipe a
        // swarm before the blow's kill — the one that carries — ever came round.
        var blow = new Reachable.Slot("hammer_blow", "FLATTEN", Reinforcements: new[] { "BREAKTHROUGH" });
        var build = Compose(Anvil, blow, new Reachable.Slot(Hardface, "UPSET"));
        var control = Compose(NoPassive, blow, new Reachable.Slot(Hardface, "UPSET"));
        Assert.True(build.Skills.Any(s => s.Def.Rule.OverkillCarry > 0f), "BREAKTHROUGH did not reach the build");

        var swarmWith = Fight(build, Many(8, 30f), NoSwing);
        var swarmWithout = Fight(control, Many(8, 30f), NoSwing);
        Assert.True(swarmWith.Metrics.CarriesLanded > 0 && swarmWithout.Metrics.CarriesLanded > 0,
                    $"BREAKTHROUGH stopped carrying: carries {swarmWith.Metrics.CarriesLanded}/{swarmWithout.Metrics.CarriesLanded}");
        Assert.Equal(swarmWithout.Metrics.CarriedDamage, swarmWith.Metrics.CarriedDamage, 1);

        var bossWith = Fight(build, Boss(), Window);
        var bossWithout = Fight(control, Boss(), Window);
        Assert.True(bossWith.Metrics.DeadweightReleases > 0, "DEADWEIGHT vanished beside BREAKTHROUGH");
        Assert.Equal(0, bossWithout.Metrics.DeadweightReleases);
        Assert.True(bossWith.Metrics.DeliveredDamage > bossWithout.Metrics.DeliveredDamage);
    }

    [Fact]
    public void test_deadweight_lives_beside_clean_cut()
    {
        // FINISH executes a target under 15% once a wave, and CLEAN CUT carries the execute's whole
        // overkill. Creatures sized so the blow leaves them under the line: the execute fires, kills,
        // and its spill carries — with THE ANVIL and without.
        var blow = new Reachable.Slot("hammer_blow", "FINISH", Reinforcements: new[] { "CLEAN CUT" });
        var build = Compose(Anvil, blow);
        var control = Compose(NoPassive, blow);
        Assert.True(build.Skills.Any(s => s.Def.Rule.OverkillCarry >= 1f), "CLEAN CUT did not reach the build");

        // The blow's size, measured on a creature nothing can kill, so the wave below is sized from
        // the sim's own number rather than a guess.
        var probe = Fight(build, Boss(), WindowNoSwing);
        var blowHit = Of(probe, BattleEventKind.Strike).First().Amount;
        // 10% is left after one blow — inside FINISH's 15% line. A wave PER ARM: creatures are
        // mutable state, and a list the first arm fought is a list of corpses for the second.
        var health = blowHit * 1.10f;

        var with = Fight(build, Many(4, health), NoSwing);
        var without = Fight(control, Many(4, health), NoSwing);
        Assert.True(with.Metrics.CarriesLanded > 0 && without.Metrics.CarriesLanded > 0,
                    $"CLEAN CUT's execute did not carry: carries {with.Metrics.CarriesLanded}/{without.Metrics.CarriesLanded}, kills with spill {with.Metrics.OverkillKills}/{without.Metrics.OverkillKills}, killed {with.Metrics.CreaturesKilled}/{without.Metrics.CreaturesKilled}, blow {blowHit}, strikes {string.Join(" ", Of(with, BattleEventKind.Strike).Take(8).Select(e => $"{e.Slot}:{e.Amount}"))} / control {string.Join(" ", Of(without, BattleEventKind.Strike).Take(8).Select(e => $"{e.Slot}:{e.Amount}"))} outcome {without.Outcome} ms {without.Metrics.DurationMs}");
        Assert.True(with.Metrics.DeadweightStores > 0, "the blow that left 10% standing stored nothing");
        Assert.Equal(0, without.Metrics.DeadweightStores);

        var bossWith = Fight(build, Boss(), Window);
        Assert.True(bossWith.Metrics.DeadweightReleases > 0, "DEADWEIGHT vanished beside CLEAN CUT");
    }

    // ── The state never crosses a wave, and a carried hit arms nothing ──

    [Fact]
    public void test_deadweight_does_not_cross_waves_and_a_carried_hit_arms_nothing()
    {
        var build = Compose(Anvil, new Reachable.Slot("hammer_blow", "FLATTEN", Reinforcements: new[] { "BREAKTHROUGH" }));
        // A fresh wave is a fresh set of creatures: what the first boss still holds when the window
        // closes is not waiting for the next one. The first hit on a fresh creature never releases,
        // and a second identical fight releases exactly what the first did.
        var first = Fight(build, Boss(), Window);
        Assert.True(first.Wave[0].StoredDeadweight > 0f, "the boss held nothing when the window closed — every hit it survived should have left a share");
        var second = Fight(build, Boss(), Window);
        var firstStrike = Of(second, BattleEventKind.Strike).First();
        Assert.DoesNotContain(Of(second, BattleEventKind.DeadweightReleased), e => e.AtMs <= firstStrike.AtMs);
        Assert.Equal(first.Metrics.DeadweightReleases, second.Metrics.DeadweightReleases);

        // Two creatures: the first dies to the blow with spill, BREAKTHROUGH carries half into the
        // second, which survives it. A carried hit is not a primary hit: it leaves no DEADWEIGHT.
        var probe = Fight(build, Boss(), WindowNoSwing);
        var blowHit = Of(probe, BattleEventKind.Strike).First().Amount;
        var pair = new List<WaveCreature>
        {
            new() { MaxHealth = 1f, Health = 1f, Damage = 0f },
            new() { MaxHealth = blowHit * 5f, Health = blowHit * 5f, Damage = 0f },
        };
        var run = Fight(build, pair, NoSwing with { TickCeilingMs = 15_000 });
        var carried = run.Events.First(e => e.Kind == BattleEventKind.Strike && e.Slot == 1);
        Assert.True(run.Metrics.CarriesLanded >= 1, "the blow's spill did not carry into the second creature");
        // The first store on creature 1 comes from a PRIMARY hit, never from the carried hit: the
        // carried Strike's instant carries no store of a third of the carried amount.
        var storesOn1 = Of(run, BattleEventKind.DeadweightStored).Where(e => e.Slot == 1).ToList();
        Assert.DoesNotContain(storesOn1, s => s.AtMs == carried.AtMs && Math.Abs(s.Amount - carried.Amount * Share) <= 1);
    }
}
