using System;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A defence break is something the fight PUBLISHES, so the screen can put a badge on it.
/// </summary>
/// <remarks>
/// <para>
/// PRESS strips defence every two seconds and the fight showed nothing for it — a creature's defence
/// is drawn nowhere on the hunt screen. Playtest: "2 saniyede bir 6 defans kırıyor diyor ama ben
/// oyunda çok farkedemedim." The rule was running and was invisible, which for a player is the same
/// thing as not running.
/// </para>
/// <para>
/// The badge is a STATE, not a flourish — an icon with a stack count that stands over the creature for
/// as long as the break does. This is an idle game whose premise is that the champion fights while
/// nobody is watching, so a rise-and-fade is a flourish nobody sees; a badge is still there when the
/// player comes back. That is what makes the count matter, and this is what pins it.
/// </para>
/// </remarks>
public class BreakBadgeTests
{
    private readonly ITestOutputHelper _out;
    public BreakBadgeTests(ITestOutputHelper output) => _out = output;

    private static SoloExpedition Run()
    {
        var mastery = Taught.Everything();
        var build = BuildComposer.Compose(
            new MemoryDustTree(), mastery, character: null,
            skills: new[]
            {
                new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a"),
                // The same Form again, deliberately passive: HAMMER's PRESS, the skill under test.
                new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "b", true),
            },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool * 40, Health = pool * 40 };
        return new SoloExpedition(build, champ, hunter, 260f, 26f,
                                  ExpeditionTuning.Default, Source.Machine, new Random(19));
    }

    [Fact]
    public void test_pressing_a_creature_publishes_a_break_the_screen_can_read()
    {
        var run = Run();
        run.PushWave();

        var breaks = run.LastWaveEvents.Where(e => e.Kind == BattleEventKind.Break).ToList();
        _out.WriteLine($"{breaks.Count} break events; stacks reached "
                       + string.Join(", ", breaks.GroupBy(e => e.Slot)
                           .Select(g => $"creature {g.Key}: x{g.Max(e => e.Amount)}")));

        Assert.True(breaks.Count > 0,
            "PRESS ran for a whole wave and published nothing — the fight has no way to show a break, "
            + "which is exactly the state the playtest could not feel.");

        // THE COUNT CLIMBS. A badge that reads x1 for ever would say "broken" and never "how broken",
        // and the depth is the whole of what PRESS buys.
        foreach (var creature in breaks.GroupBy(e => e.Slot))
        {
            var seen = creature.Select(e => e.Amount).ToList();
            Assert.Equal(seen.OrderBy(x => x).ToList(), seen);
            Assert.Equal(1, seen[0]);
        }
    }

    [Fact]
    public void test_a_skill_that_breaks_nothing_publishes_nothing()
    {
        // The negative half: without PRESS there is no badge, so an empty sky is not a bug.
        var mastery = Taught.Everything();
        var build = BuildComposer.Compose(
            new MemoryDustTree(), mastery, character: null,
            skills: new[] { new BuildComposer.SkillPick(Source.Body, Form.Strike, null, "a") },
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

        var hunter = new Hunter();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool * 40, Health = pool * 40 };
        var run = new SoloExpedition(build, champ, hunter, 260f, 26f,
                                     ExpeditionTuning.Default, Source.Machine, new Random(19));
        run.PushWave();

        Assert.DoesNotContain(run.LastWaveEvents, e => e.Kind == BattleEventKind.Break);
    }

    [Fact]
    public void test_the_replay_carries_the_break_as_a_state_and_a_new_wave_clears_it()
    {
        // What the screen actually reads. The badge is drawn off WaveReplay, so the event reaching the
        // stream is only half the chain — this is the other half.
        var replay = new WaveReplay(
            new[]
            {
                new BattleEvent(BattleEventKind.Break, 0, 1, 100),
                new BattleEvent(BattleEventKind.Break, 0, 2, 2100),
            },
            new System.Collections.Generic.Dictionary<int, int>(),
            new System.Collections.Generic.Dictionary<int, int>(),
            100f);
        replay.SetComposition(new[] { 100f });
        replay.Advance(5000);
        Assert.Equal(2, replay.CreatureBreaks(0));
        Assert.Equal(0, replay.CreatureBreaks(1));

        // Creatures are minted per wave, so the badge is too.
        replay.SetComposition(new[] { 100f });
        Assert.Equal(0, replay.CreatureBreaks(0));
    }
}
