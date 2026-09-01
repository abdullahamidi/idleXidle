using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// How long the first hour actually takes.
/// </summary>
/// <remarks>
/// <para>
/// The playtest cannot start until the flow is worth playing, and nothing here had ever measured the
/// one number a first session is made of: how long a wave takes. Every other figure in the game hangs
/// off it — the loot rate is drops-per-boss times bosses-per-wave times waves-per-minute, the tutorial
/// paces itself against waves cleared, and "reach depth 7 to conquer" is a sentence about time only
/// once you know what a wave costs.
/// </para>
/// <para>
/// This runs the REAL expedition from a fresh champion, and reports what a new player lives through.
/// </para>
/// </remarks>
public class PacingTest
{
    private readonly ITestOutputHelper _out;

    public PacingTest(ITestOutputHelper output) => _out = output;

    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    /// <summary>
    /// Four Body actives, no Vows, no tree — the full four-slot baseline these probes were tuned
    /// against. (A brand-new save now opens with a single slot and earns the rest; the probe keeps
    /// the four-slot shape so its measured bands stay comparable.)
    /// </summary>
    /// <remarks>
    /// It was four copies of BLOW until 2026-09-01. A build holds a skill ONCE now (LAW 13, enforced
    /// in <c>Build.Equip</c>), so the probe fills its four slots with four different actives — the
    /// nearest legal shape to what it measured before, and one a player can actually weave.
    /// </remarks>
    private static Build StarterBuild()
    {
        var b = new Build();
        foreach (var id in new[] { "hammer_blow", "volley_spray", "field_pulse", "drain_drink" })
            Assert.True(b.Equip(TestBuilds.Skill(id)), $"{id} refused");
        return b;
    }

    private sealed record Run(int Reached, IReadOnlyList<int> WaveMs)
    {
        public float TotalSeconds => WaveMs.Sum() / 1000f;
    }

    /// <summary>Play a fresh region until death or a wave cap, recording how long each wave took.</summary>
    private static Run PlayFresh(int stopAtWave, float baseHealth = 110f, float baseDamage = 9f)
    {
        var hunter = new Hunter();
        // THROUGH THE SHARED MINT, not `hunter.MaxHealth`. The pool is half of the wave-length
        // transform (ExpeditionTuning.WaveLengthScale), so a hand-minted pool fights a wave from a
        // different game — this file measured a fresh champion dying at wave 2 for exactly that reason.
        var build = StarterBuild();
        var pool = SoloBattle.ChampionHealth(build, hunter);
        var champ = new Champion { MaxHealth = pool, Health = pool };
        var run = new SoloExpedition(build, champ, hunter, baseHealth, baseDamage,
                                     T, new Random(31));

        var perWave = new List<int>();
        while (!run.Over && run.Wave < stopAtWave)
        {
            var before = run.Wave;
            run.PushWave();
            if (run.Wave == before) break;          // stalled: no progress, do not spin
            // The wave's length is when its last beat landed. SoloExpedition does not expose a
            // duration directly, and the event stream is what the replay itself is timed from.
            perWave.Add(run.LastWaveEvents.Count == 0 ? 0 : run.LastWaveEvents.Max(e => e.AtMs));
        }

        return new Run(run.Wave, perWave);
    }

    [Fact]
    public void test_the_first_session_is_reported_wave_by_wave()
    {
        var run = PlayFresh(stopAtWave: 20);

        _out.WriteLine("A FRESH CHAMPION, no gear, no tree, four Body BLOWs:");
        for (var i = 0; i < run.WaveMs.Count; i++)
            _out.WriteLine($"   wave {i + 1,3}   {run.WaveMs[i] / 1000f,6:0.0}s"
                           + (WaveScaling.IsBossWave(i + 1, T) ? "   (boss)" : ""));

        _out.WriteLine("");
        _out.WriteLine($"   reached wave {run.Reached} in {run.TotalSeconds:0.0}s of fight time");

        var toConquer = run.WaveMs.Take(7).Sum() / 1000f;
        _out.WriteLine($"   the 7 waves that conquer a region: {toConquer:0.0}s");
    }

    [Fact]
    public void test_the_effect_of_the_enemy_health_base_is_swept()
    {
        // The dial that sets every other number in the first session. Reported as a sweep rather than
        // argued about, because "is 110 right" is not answerable from the constant — only from what a
        // player lives through at each value.
        _out.WriteLine($"{"base hp",8} {"wave 1",8} {"shortest",9} {"7 waves",9} {"died at",8}");

        foreach (var baseHp in new[] { 110f, 160f, 220f, 300f, 400f })
        {
            var run = PlayFresh(stopAtWave: 60, baseHealth: baseHp);
            var first = run.WaveMs.Count > 0 ? run.WaveMs[0] / 1000f : 0f;
            var shortest = run.WaveMs.Count > 0 ? run.WaveMs.Min() / 1000f : 0f;
            var seven = run.WaveMs.Take(7).Sum() / 1000f;

            _out.WriteLine($"{baseHp,8:0} {first,7:0.0}s {shortest,8:0.0}s {seven,8:0.0}s {run.Reached,8}");
        }
    }

    [Fact]
    public void test_conquest_sits_at_the_edge_of_what_a_fresh_champion_survives()
    {
        // WHAT THIS TEST ORIGINALLY ASSERTED WAS WRONG, and the sweep above is how that surfaced. It
        // demanded that seven waves take at least 25 seconds, on the theory that a region conquered in
        // 13 seconds is over before the player has read anything. But raising enemy health does not
        // lengthen the run — it kills the champion sooner (wave 8 down to wave 4 across the sweep),
        // because the enemy's damage scales with it. "Time to conquer IF YOU SURVIVE" is not an
        // experience anybody has.
        //
        // What a new player actually lives is a fifteen-second run that ENDS, several times, until they
        // have grown enough to hold seven waves. That is a coherent idle loop and the thing worth
        // protecting: conquest must sit near the edge of a fresh champion's survival — comfortably
        // inside it and the first region teaches nothing, far outside it and the game opens with a wall.
        var run = PlayFresh(stopAtWave: 60);

        _out.WriteLine($"a fresh champion reaches wave {run.Reached}; conquest asks for 7");

        Assert.True(run.Reached >= 4,
            $"a fresh champion dies at wave {run.Reached}. The first region opens with a wall.");
        Assert.True(run.Reached <= 20,
            $"a fresh champion reaches wave {run.Reached} unaided, so the seven waves that conquer the "
            + "first region are free and the region teaches nothing.");
    }

    [Fact]
    public void test_a_starting_champion_cannot_walk_forever()
    {
        // The other end of the same dial. An idle game's first session should END somewhere — the death,
        // the report and the restart are the loop, and a champion that never dies never meets it.
        var run = PlayFresh(stopAtWave: 400);

        _out.WriteLine($"a starting champion stops at wave {run.Reached} "
                       + $"after {run.TotalSeconds / 60f:0.0} minutes of fight time");

        Assert.True(run.Reached < 400,
            "a fresh champion with no gear and no tree walked 400 waves without dying — nothing in the "
            + "first session ever pushes back.");
    }
}
