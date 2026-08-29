using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// What the slot split cost in raw output, measured over a whole run rather than one wave.
/// Stage 5 of the rework (design/gdd/skill-slots-and-skill-trees.md §10).
/// </summary>
/// <remarks>
/// <para>
/// Cutting the actives from four to two roughly halves cast output, and the champion gets those
/// beats back as plain swings. Whether that is a net loss depends on how the swing compares to a
/// cast, which is a number nobody has measured since the beat model landed — so this file measures
/// it instead of arguing about it.
/// </para>
/// <para>
/// <b>Measured over a RUN, not a wave.</b> A single wave says how hard the champion hits; only a run
/// says whether it still gets anywhere, because enemy health compounds with depth and the failure
/// mode is a wall rather than a slowdown. The probes this project got wrong before were the ones
/// that measured one iteration correctly and the loop around it not at all.
/// </para>
/// </remarks>
public class SlotSplitBalanceTests
{
    private readonly ITestOutputHelper _out;
    public SlotSplitBalanceTests(ITestOutputHelper output) => _out = output;

    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    private static readonly (string Name, Form Form)[] Loadout =
    {
        ("a", Form.Strike), ("b", Form.Projectile), ("c", Form.Mark), ("d", Form.Transformation),
    };

    /// <summary>The pre-rework build: four skills, all of them taking a beat.</summary>
    private static Build FourActives()
    {
        var b = new Build { ActiveCapacity = 4, PassiveCapacity = 0 };
        foreach (var (n, f) in Loadout)
            b.Weave(new EquippedSkill(
                new WovenAbility { Name = n, Source = Source.Body, Form = f },
                FormBehaviour.BaseCooldownMs(f), PassiveSlot: false));
        return b;
    }

    /// <summary>The build a real player gets now: two actives, two passives.</summary>
    private static Build TwoAndTwo()
        => BuildComposer.Compose(
            new MemoryDustTree(), new MasteryTree(), character: null,
            skills: Loadout.Select(l => new BuildComposer.SkillPick(Source.Body, l.Form, null, l.Name)).ToList(),
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

    private sealed record Run(int Reached, float Seconds);

    /// <summary>Play a fresh region until death or a cap, on a fixed seed.</summary>
    private static Run Play(Build build, int stopAtWave)
    {
        var hunter = new Hunter();
        // Through the shared mint: the pool is half of the wave-length transform, so a hand-minted
        // one fights a wave from a different game.
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, 110f, 9f, T, Source.Nature, new Random(31));

        var ms = 0;
        while (!run.Over && run.Wave < stopAtWave)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;
            ms += run.LastWaveEvents.Count == 0 ? 0 : run.LastWaveEvents.Max(e => e.AtMs);
        }
        return new Run(run.Wave, ms / 1000f);
    }

    [Fact]
    public void test_the_slot_split_is_measured_not_assumed()
    {
        const int cap = 40;
        var before = Play(FourActives(), cap);
        var after = Play(TwoAndTwo(), cap);

        _out.WriteLine("A FRESH CHAMPION, no gear, no tree — Strike, Projectile, Mark, Transformation:");
        _out.WriteLine($"   four actives      reached wave {before.Reached,3} in {before.Seconds,7:0.0}s of fight time");
        _out.WriteLine($"   two + two         reached wave {after.Reached,3} in {after.Seconds,7:0.0}s of fight time");

        var depthRatio = before.Reached == 0 ? 0f : after.Reached / (float)before.Reached;
        var paceRatio = after.Seconds <= 0f ? 0f : before.Seconds / after.Seconds;
        _out.WriteLine("");
        _out.WriteLine($"   depth   x{depthRatio:0.00}");
        _out.WriteLine($"   pace    x{paceRatio:0.00}   (over 1.00 means the split cleared FASTER)");

        // A BAND, not a point. The split is meant to change the FEEL of a fight, not its outcome, so
        // the run has to land near where it did. Outside this band the swing and the cast are badly
        // mismatched and AutoAttackDamage or FormBaseValue has to move (design §10) — which is a
        // retune this project has said out loud must be confirmed by a playtest, never done blind.
        Assert.InRange(depthRatio, 0.70f, 1.40f);
    }
}
