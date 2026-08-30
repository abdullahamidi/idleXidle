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
/// Cutting the actives from four to two roughly halves cast output, and the champion gets those beats
/// back as plain swings. Whether that is a net loss depends on how the swing compares to a cast, which
/// is a number nobody had measured since the beat model landed — so this file measures it instead of
/// arguing about it.
/// </para>
/// <para>
/// <b>Measured over a RUN, not a wave.</b> A single wave says how hard the champion hits; only a run
/// says whether it still gets anywhere, because enemy health compounds with depth and the failure mode
/// is a wall rather than a slowdown. The probes this project got wrong before were the ones that
/// measured one iteration correctly and the loop around it not at all.
/// </para>
/// </remarks>
public class SlotSplitBalanceTests
{
    private readonly ITestOutputHelper _out;
    public SlotSplitBalanceTests(ITestOutputHelper output) => _out = output;

    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    /// <summary>The four beat-taking Forms — the only loadout the pre-rework model could hold.</summary>
    private static readonly (string Name, Form Form)[] AllCasters =
    {
        ("a", Form.Strike), ("b", Form.Projectile), ("c", Form.Mark), ("d", Form.Transformation),
    };

    /// <summary>Two casters and two passives that both pay — what a player would actually build.</summary>
    private static readonly (string Name, Form Form)[] Mixed =
    {
        ("a", Form.Strike), ("b", Form.Projectile), ("c", Form.Aura), ("d", Form.Trap),
    };

    /// <summary>
    /// The pre-rework build for a given loadout: four undifferentiated slots, each skill in its
    /// natural kind, and no per-kind budget at all.
    /// </summary>
    private static Build OldModel((string Name, Form Form)[] loadout)
    {
        var b = new Build();   // an unset per-kind capacity is the whole budget — the old behaviour
        foreach (var (n, f) in loadout)
            b.Weave(new EquippedSkill(
                new WovenAbility { Name = n, Source = Source.Body, Form = f },
                FormBehaviour.BaseCooldownMs(f)));
        return b;
    }

    /// <summary>The build a real player gets now: two actives, two passives, through the composer.</summary>
    private static Build TwoAndTwo((string Name, Form Form)[] loadout)
        => BuildComposer.Compose(
            new MemoryDustTree(), Taught.Everything(), character: null,
            skills: loadout.Select(l => new BuildComposer.SkillPick(Source.Body, l.Form, null, l.Name)).ToList(),
            keystoneIds: Array.Empty<string>(), slotCapacity: 4);

    private sealed record Run(int Reached, float Seconds);

    /// <summary>Play a fresh region until death or a cap, on a fixed seed.</summary>
    private static Run Play(Build build, int stopAtWave)
    {
        var hunter = new Hunter();
        // Through the shared mint: the pool is half of the wave-length transform, so a hand-minted one
        // fights a wave from a different game.
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

        // THE SAME LOADOUT, BEFORE AND AFTER. Comparing two different loadouts measures the content,
        // not the change — which is what an earlier version of this file accidentally did.
        var castersBefore = Play(OldModel(AllCasters), cap);
        var castersAfter = Play(TwoAndTwo(AllCasters), cap);
        var mixedBefore = Play(OldModel(Mixed), cap);
        var mixedAfter = Play(TwoAndTwo(Mixed), cap);

        static float Ratio(Run before, Run after) => before.Reached == 0 ? 0f : after.Reached / (float)before.Reached;

        _out.WriteLine("A FRESH CHAMPION, no gear, no tree. The same loadout under both models:");
        _out.WriteLine($"   four casters   before wave {castersBefore.Reached,3}   after wave {castersAfter.Reached,3}"
                       + $"   depth x{Ratio(castersBefore, castersAfter):0.00}");
        _out.WriteLine($"   two + aura/trap before wave {mixedBefore.Reached,3}   after wave {mixedAfter.Reached,3}"
                       + $"   depth x{Ratio(mixedBefore, mixedAfter):0.00}");
        _out.WriteLine("");
        _out.WriteLine("   A build that already carried AURA and TRAP was ALREADY two actives and two");
        _out.WriteLine("   passives, so the split costs it nothing. A build of four casters loses two of them");
        _out.WriteLine("   to BRAND and WILT — an amplify and an attack break, neither of which kills — and");
        _out.WriteLine("   that is the migration's real price. Printed rather than asserted: whether the price");
        _out.WriteLine("   is right is a content decision for a playtest, not a regression to paper over.");

        // A build the split does not restructure must not move. This is the honest floor: if even the
        // loadout that was ALREADY 2+2 lost depth, something in the rework broke rather than changed.
        Assert.InRange(Ratio(mixedBefore, mixedAfter), 0.85f, 1.25f);
    }
}
