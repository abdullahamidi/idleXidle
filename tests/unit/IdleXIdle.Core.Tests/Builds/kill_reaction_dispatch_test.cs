using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// The two dead paths phase 2 reopened, and the exact bounds they were reopened inside.
/// </summary>
/// <remarks>
/// <para>
/// <b>ReactionOn.Kill</b> was declared in the catalogue and dispatched nowhere: the wave body only ever
/// asked <c>On == ReactionOn.Bitten</c>. BACKDRAW answers a death, so the enum member is live again —
/// but WEEP declares the same member and is answered by the bleed scan instead, so a dispatch written
/// as the plain mirror of the Bitten gate would fire a skill event and a projectile on every corpse for
/// a build that bought neither. The gate reads <c>BasePower</c> as well, and the first test below is
/// what says so.
/// </para>
/// <para>
/// <b>A time-counted Active</b> was shut rather than deleted by the 2026-08-31 dead-path audit, with a
/// note asking that it be reopened "deliberately, with a test". This is that test: the clock decides
/// when CLOCKWORK is ELIGIBLE and nothing in the game may hurry it, which is the whole of its card and
/// the one thing about it a future rate change could silently break.
/// </para>
/// </remarks>
public class KillReactionDispatchTests
{
    /// <summary>A tree that has walked every skill road, so the fixture may equip any shared skill.</summary>
    private static MasteryTree EveryRoadWalked()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        tree.RestoreTaken(MasteryCatalog.Nodes
            .Where(x => x.Kind == MasteryKind.SkillRoad)
            .Select(x => x.Id));
        return tree;
    }

    private static Build Woven(Character? character, params string[] ids)
        => BuildComposer.Compose(
            EveryRoadWalked(), character,
            skills: ids.Select(id => new BuildComposer.SkillPick(Source.Body, null, SkillId: id)).ToList(),
            keystoneIds: Array.Empty<string>(), slotCapacity: 4, progress: new SkillProgress());

    /// <summary>A wave of small creatures that die, so the kill path is actually walked.</summary>
    private static List<BattleEvent> Fight(Build build, int creatures = 4, float health = 60f,
                                           int ceilingMs = 20_000)
    {
        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var wave = Enumerable.Range(0, creatures)
            .Select(_ => WaveCreature.Single(health, 5f))
            .ToList();
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), wave, enemyIntervalMs: 1_500,
            ExpeditionTuning.Default with { TickCeilingMs = ceilingMs }, new Random(11));
        return events;
    }

    // ── The Kill dispatch, and what it must NOT sweep in ─────────────────────────────────────────

    [Fact]
    public void test_a_weep_build_emits_no_skill_event_on_a_kill()
    {
        // WEEP is a Reaction whose On is Kill, and its whole behaviour is the bleed the death section
        // already runs. If the dispatch were gated on Kind and On alone it would ALSO fire WEEP here —
        // a skill event and a volley, twice per death, on a skill with no number to fire.
        var build = Woven(null, "hammer_blow", "volley_weep");
        var weepSlot = build.Skills.ToList().FindIndex(s => s.Def.Id == "volley_weep");
        Assert.True(weepSlot >= 0, "the fixture must actually weave WEEP");

        var events = Fight(build);
        Assert.Contains(events, e => e.Kind == BattleEventKind.EnemyDown);   // deaths happened
        Assert.DoesNotContain(events, e => e.Kind == BattleEventKind.Skill && e.Slot == weepSlot);
    }

    [Fact]
    public void test_a_backdraw_build_answers_a_kill_with_a_volley()
    {
        // The other half of the same gate: a Kill reaction that DOES carry a number fires, on the same
        // millisecond as the death it answers.
        var quiver = CharacterRoster.Get("quiver");
        var build = Woven(quiver, "hammer_blow", quiver.SignatureSkillId!);
        var slot = build.Skills.ToList().FindIndex(s => s.Def.Id == quiver.SignatureSkillId);
        Assert.True(slot >= 0, "the fixture must actually weave BACKDRAW");

        var events = Fight(build);
        var answered = events.Where(e => e.Kind == BattleEventKind.Skill && e.Slot == slot).ToList();
        Assert.NotEmpty(answered);
        foreach (var shot in answered)
            Assert.Contains(events, e => e.Kind == BattleEventKind.EnemyDown && e.AtMs == shot.AtMs);
    }

    [Fact]
    public void test_the_volleys_own_rearm_survives_the_kill_that_produced_it()
    {
        // THE BOUND, and it is the whole reason the volley is safe on the champion it belongs to.
        // LOOSE AGAIN — which THE QUIVER grants — zeroes every cooldown on every death, and the
        // dispatch runs on every death too. If it cleared this slot as well the volley would fire on
        // every corpse for ever, re-arm or no re-arm. The re-arm is 2.5s and a wave is seconds long,
        // so a wave with four deaths must not produce four volleys.
        var quiver = CharacterRoster.Get("quiver");
        var build = Woven(quiver, "hammer_blow", quiver.SignatureSkillId!);
        var slot = build.Skills.ToList().FindIndex(s => s.Def.Id == quiver.SignatureSkillId);

        var events = Fight(build);
        var deaths = events.Count(e => e.Kind == BattleEventKind.EnemyDown);
        var volleys = events.Count(e => e.Kind == BattleEventKind.Skill && e.Slot == slot);

        Assert.True(deaths >= 3, $"the fixture must produce several deaths; it produced {deaths}");
        Assert.True(volleys < deaths,
            $"{deaths} deaths produced {volleys} volleys — LOOSE AGAIN is clearing the one entry that bounds it");
    }

    // ── The clock nothing hurries ────────────────────────────────────────────────────────────────

    [Fact]
    public void test_a_time_counted_active_is_eligible_on_its_clock_and_still_spends_the_beat()
    {
        var metronome = CharacterRoster.Get("metronome");
        var def = SkillCatalogue.ById(metronome.SignatureSkillId!);
        Assert.Equal(SkillKind.Active, def.Kind);
        Assert.Equal(0, def.Beats);
        Assert.True(def.IntervalMs > 0);

        var build = Woven(metronome, def.Id);
        var slot = build.Skills.ToList().FindIndex(s => s.Def.Id == def.Id);
        Assert.True(slot >= 0, "the fixture must actually weave the clock-counted active");

        // A long window against creatures nothing can finish, so the count is the clock's and not the
        // wave's. The champion's innate waives the opening wait, so the first arrival is at the wave's
        // first beat and the rest are one interval apart.
        var champ = new Champion { MaxHealth = 1_000_000, Health = 1_000_000 };
        var wall = new List<WaveCreature> { WaveCreature.Single(1e9f, 1f) };
        var (_, events) = SoloBattle.ResolveWave(
            champ, build, new Hunter(), wall, enemyIntervalMs: 5_000,
            ExpeditionTuning.Default with { TickCeilingMs = 30_000 }, new Random(11));

        var arrivals = events.Where(e => e.Kind == BattleEventKind.Skill && e.Slot == slot)
                             .Select(e => e.AtMs).ToList();
        Assert.True(arrivals.Count >= 3, $"expected several arrivals in 30s at {def.IntervalMs}ms; got {arrivals.Count}");
        for (var i = 1; i < arrivals.Count; i++)
            Assert.True(arrivals[i] - arrivals[i - 1] >= def.IntervalMs,
                        $"two arrivals were {arrivals[i] - arrivals[i - 1]}ms apart, inside the {def.IntervalMs}ms clock");

        // AND THE CAST STILL COSTS THE ACTION IT LANDS ON. Every arrival sits on a beat, which is what
        // keeps a Beats-0 Active from being a free skill.
        var beats = events.Where(e => e.Kind == BattleEventKind.Beat).Select(e => e.AtMs).ToHashSet();
        Assert.All(arrivals, at => Assert.Contains(at, beats));
    }

    [Fact]
    public void test_no_rate_bonus_makes_the_clock_come_sooner()
    {
        // The card's second sentence, and the one a future cadence change could quietly break: every
        // other cooldown in the game is divided by the champion's rate, and this one is not.
        //
        // THE ONE HONEST EXCEPTION is beat quantisation. The clock decides when the skill is ELIGIBLE
        // and the beat decides when it LANDS, so a champion acting twice as often waits less of a beat
        // for the arrival it was already owed — and over a long window that slack can be worth one
        // extra arrival. What it can never do is shorten the interval itself, so the rule is stated as
        // the two things that actually hold: no two arrivals are closer than the clock, and tripling
        // the rate does not triple the arrivals.
        var metronome = CharacterRoster.Get("metronome");
        var def = SkillCatalogue.ById(metronome.SignatureSkillId!);

        static List<int> Arrivals(Build build, int slot)
        {
            var champ = new Champion { MaxHealth = 1_000_000, Health = 1_000_000 };
            var wall = new List<WaveCreature> { WaveCreature.Single(1e9f, 1f) };
            var (_, events) = SoloBattle.ResolveWave(
                champ, build, new Hunter(), wall, enemyIntervalMs: 5_000,
                ExpeditionTuning.Default with { TickCeilingMs = 30_000 }, new Random(11));
            return events.Where(e => e.Kind == BattleEventKind.Skill && e.Slot == slot)
                         .Select(e => e.AtMs).ToList();
        }

        var plain = Woven(metronome, def.Id);
        var hasted = Woven(metronome, def.Id);
        hasted.Shape = SkillShape.Combine(hasted.Shape, new SkillShape { SkillRate = 3f });

        var slot = plain.Skills.ToList().FindIndex(s => s.Def.Id == def.Id);
        var slow = Arrivals(plain, slot);
        var fast = Arrivals(hasted, slot);

        Assert.NotEmpty(slow);
        // Tripling the rate buys at most the beat the arrival was waiting on, never a shorter clock.
        Assert.True(fast.Count <= slow.Count + 1,
            $"a 3x skill rate turned {slow.Count} arrivals into {fast.Count} — the clock is being divided by the rate");
        for (var i = 1; i < fast.Count; i++)
            Assert.True(fast[i] - fast[i - 1] >= def.IntervalMs,
                $"a hasted champion put two arrivals {fast[i] - fast[i - 1]}ms apart, inside the {def.IntervalMs}ms clock");
    }
}
