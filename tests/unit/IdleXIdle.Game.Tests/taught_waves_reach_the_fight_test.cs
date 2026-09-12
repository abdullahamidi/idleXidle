using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Game;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE TAUGHT WAVES ACTUALLY REACH THE FIGHT. A tuning nothing hands in is a fix that does not exist.
/// </summary>
/// <remarks>
/// <para>
/// <c>ExpeditionTuning.UntilTheFirstBossFalls</c> is inert on its own: <c>Descent.Tuning</c> defaults to
/// <c>ExpeditionTuning.Default</c>, and until 2026-09-12 the LIVE game assigned it nowhere at all — the
/// only assignment in <c>src/</c> was the offline simulation's. So Core can pass every first-boss test
/// in <c>first_boss_test.cs</c> while the shipped game still kills the player on wave 5, which is this
/// project's most-repeated bug shape: green code nothing runs.
/// </para>
/// <para>
/// The end-to-end proof is the live one — <c>tools/check_opening_flow.sh</c> at 100 / 125 / 150 %, whose
/// traces now show <c>ENEMY_DOWN wave=5 boss=True</c> and no <c>HUNTER_DOWN</c>, and
/// <c>tools/check_boot.sh</c>, whose soak walks a fresh save to wave 8 instead of dying on 5. Game1 and
/// HuntScreen both need a GraphicsDevice, so what is provable HERE is that the chain of hand-offs
/// exists: host reads the career fact, pushes it to the screen, screen pushes it to the descent.
/// </para>
/// </remarks>
public class TaughtWavesReachTheFightTest
{
    private readonly ITestOutputHelper _out;

    public TaughtWavesReachTheFightTest(ITestOutputHelper output) => _out = output;

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(string.Join('/', parts) + " not found above the test binary.");
    }

    /// <summary>THE SCREEN CAN CARRY A TUNING AT ALL, and it defaults to the game everyone plays.</summary>
    [Fact]
    public void test_the_hunt_screen_carries_a_tuning_and_defaults_to_the_shipped_one()
    {
        var p = typeof(HuntScreen).GetProperty(nameof(HuntScreen.Tuning),
                                               BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(p);
        Assert.Equal(typeof(ExpeditionTuning), p!.PropertyType);
        Assert.True(p.CanRead && p.CanWrite, "the host has to be able to hand one in");
        // A screen nobody has configured fights the shipped game, never the taught one. Asserted from
        // the declaration because HuntScreen takes a UiKit, which needs a GraphicsDevice — no test in
        // this project constructs one.
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs")).Replace("\r\n", "\n");
        Assert.Contains("public ExpeditionTuning Tuning { get; set; } = ExpeditionTuning.Default;",
                        hunt, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE THREE HAND-OFFS, in the order that makes them effective: the host decides from a persisted
    /// career fact, pushes it to the screen every frame, and the screen pushes it to the descent at
    /// <c>StartRun</c> — before the expedition captures it.
    /// </summary>
    /// <remarks>
    /// Read from source because WHERE each assignment sits is the whole point. The expedition captures
    /// its tuning readonly at construction, so a push after <c>StartRun</c> would fight the game the
    /// PREVIOUS descent fought — the bug being guarded against is an assignment that exists but is
    /// too late, which reflection cannot see.
    /// </remarks>
    [Fact]
    public void test_the_host_hands_the_taught_tuning_down_to_the_descent()
    {
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs")).Replace("\r\n", "\n");

        // 1. THE DECISION, from the persisted career fact — not from the authored opening, so it
        //    survives a reload, a SKIP TUTORIAL, and losing the protected boss anyway.
        Assert.Contains("private ExpeditionTuning TuningNow()\n"
                        + "        => _bossesFelled == 0 ? ExpeditionTuning.UntilTheFirstBossFalls : ExpeditionTuning.Default;",
                        game, StringComparison.Ordinal);

        // 2. THE PUSH to the screen, in UpdateExpedition beside the region's own inputs.
        Assert.Contains("_expedition.Tuning = TuningNow();", game, StringComparison.Ordinal);

        // 3. THE PUSH to the descent, inside StartRun and BEFORE the StartRun call that captures it.
        var startRun = hunt[hunt.IndexOf("private void StartRun(Hunter hunter)", StringComparison.Ordinal)..];
        startRun = startRun[..startRun.IndexOf("BeginWave();", StringComparison.Ordinal)];
        var assigned = startRun.IndexOf("_descent.Tuning = Tuning;", StringComparison.Ordinal);
        var minted = startRun.IndexOf("_descent.StartRun(", StringComparison.Ordinal);
        Assert.True(assigned >= 0, "HuntScreen.StartRun never hands the descent a tuning — the fix is dormant.");
        Assert.True(minted >= 0, "HuntScreen.StartRun no longer starts a run.");
        Assert.True(assigned < minted,
                    "the tuning is assigned AFTER StartRun, so the expedition captured the previous descent's game.");

        // ...AND THE OFFLINE SIMULATION FIGHTS THE SAME GAME, or an absence out-earns the session it
        // stands in for. (The second Simulate call site is a capture fixture posing a veteran and
        // deliberately keeps the default — see its own comment.)
        Assert.Contains("tuning: TuningNow());", game, StringComparison.Ordinal);
    }

    /// <summary>
    /// AND THE KNOB IS REACHABLE FROM THE GAME'S SIDE AT ALL: the taught tuning differs from the
    /// default in exactly two fields, both of them the window and its relief.
    /// </summary>
    /// <remarks>
    /// A guard against the broad version of this change. If a later edit widened
    /// <c>UntilTheFirstBossFalls</c> to move health, haul, the beat or the boss's spike, the "narrow,
    /// production-safe" claim would quietly stop being true and every balance probe would be measuring
    /// a different game for the first five waves of a career.
    /// </remarks>
    [Fact]
    public void test_the_taught_tuning_differs_from_the_shipped_one_in_exactly_two_fields()
    {
        var taught = ExpeditionTuning.UntilTheFirstBossFalls;
        var shipped = ExpeditionTuning.Default;
        var differ = typeof(ExpeditionTuning)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => !Equals(p.GetValue(taught), p.GetValue(shipped)))
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();
        _out.WriteLine("differs in: " + string.Join(", ", differ));
        Assert.Equal(new[] { nameof(ExpeditionTuning.TutorialBiteScale), nameof(ExpeditionTuning.TutorialWaves) },
                     differ);
    }
}
