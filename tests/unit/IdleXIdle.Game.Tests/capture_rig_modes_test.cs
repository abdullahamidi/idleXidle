using System;
using System.IO;
using System.Linq;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE CAPTURE RIG'S REGISTRY IS FOUR LISTS AND A HEADER, and a fixture missing from any of them fails
/// silently: a mode absent from Game1's allow-list photographs the TITLE, absent from the fight family it
/// opens no fight, absent from the worn-gear list it poses a naked Hunter, absent from the toast exclusion
/// it photographs a WELCOME BACK toast over the state it exists to show, and absent from capture.sh's
/// header it is a mode nobody can find. Pinned by reading the sources, as host_input_gates_test.cs does,
/// since Game1.Update needs a GraphicsDevice.
/// </summary>
public class CaptureRigModesTest
{
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

    private static string Source(params string[] parts) => File.ReadAllText(RepoFile(parts)).Replace("\r\n", "\n");

    private static string Game1() => Source("src", "IdleXIdle.Game", "Game1.cs");
    private static string HuntScreen() => Source("src", "IdleXIdle.Game", "HuntScreen.cs");
    private static string CaptureHeader()
    {
        // The comment block at the top of capture.sh, up to the first non-comment line.
        var lines = Source("tools", "asset-pipeline", "capture.sh").Split('\n');
        return string.Join('\n', lines.Skip(1).TakeWhile(l => l.StartsWith('#')));
    }

    /// <summary>One `sm is "a" or "b" …` list, from its anchor to the brace that opens its block.</summary>
    private static string ListAt(string src, string anchor)
    {
        var at = src.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(at >= 0, $"`{anchor}` not found in Game1.cs");
        var brace = src.IndexOf('{', at);
        Assert.True(brace > at, $"the list at `{anchor}` never opens a block");
        return src[at..brace];
    }

    /// <summary>The body of one method, brace-matched from its signature.</summary>
    private static string BodyOf(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"source no longer contains `{signature}`.");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
        }
        throw new InvalidOperationException($"`{signature}` never closes.");
    }

    /// <summary>The four places a fight fixture has to be named, by the phrase each list opens with.</summary>
    private static readonly (string Name, string Anchor)[] FightLists =
    {
        ("the mode allow-list", "if (sm is \"vfx\" or \"forge\""),
        ("the fight family", "if (sm is \"fight\" or \"welcome\""),
        ("the worn-gear list", "if (sm is \"fightgear\" or \"fightswing\""),
        ("the welcome-toast exclusion", "if (sm is not (\"fightreport\""),
    };

    [Fact]
    public void test_fightfade_is_registered_in_every_game1_list_and_in_the_capture_header()
    {
        var game1 = Game1();
        foreach (var (name, anchor) in FightLists)
            Assert.Contains("\"fightfade\"", ListAt(game1, anchor));

        // ...and it is a real handler: a seeded death, then the transition posed and HELD at RH_SHOT_T.
        var handler = BodyOf(game1, "else if (sm == \"fightfade\")");
        Assert.Contains("_expedition.DevRunToDeath(_hunter)", handler);
        Assert.Contains("_expedition.DevPoseDeathTransition(", handler);
        Assert.Contains("RH_SHOT_T", handler);

        var header = CaptureHeader();
        Assert.Contains("fightfade", header);
        Assert.Contains("fightfall", header);
        Assert.Contains("RH_SHOT_REDUCED", header);
    }

    [Fact]
    public void test_fightregroup_is_gone_from_the_rig_with_the_plate_it_photographed()
    {
        // The state it posed — a plate naming the old descent over a live new one — no longer exists, and
        // a fixture that poses nothing is the rig's own anti-pattern.
        var game1 = Game1();
        foreach (var (name, anchor) in FightLists)
            Assert.DoesNotContain("\"fightregroup\"", ListAt(game1, anchor));
        Assert.DoesNotContain("fightregroup", game1);
        Assert.DoesNotContain("DevRunToRegroup", game1);
        Assert.DoesNotContain("fightregroup", CaptureHeader());

        var hunt = HuntScreen();
        Assert.DoesNotContain("DevRunToRegroup", hunt);
        Assert.DoesNotContain("_devHoldFellPlate", hunt);
    }

    [Fact]
    public void test_fightreport_photographs_the_open_log()
    {
        // The report's diagnostic has one surface now — the EXPEDITION LOG — so RH_SHOT_LIMIT's three
        // verdicts are photographed there, the way `runlog` already opens it.
        var handler = BodyOf(Game1(), "if (sm == \"fightreport\")");
        Assert.Contains("RH_SHOT_LIMIT", handler);
        Assert.Contains("_expedition.DevRunToDeath(_hunter, poseLimit: limit)", handler);
        Assert.Contains("_expedition.ToggleLog();", handler);
    }

    [Fact]
    public void test_the_death_transition_pose_is_held_every_frame_and_restarts_for_real_past_the_black()
    {
        var hunt = HuntScreen();
        Assert.Contains("public void DevPoseDeathTransition(float t)", hunt);
        // HELD like the arrival pose: the host's first live frame restarts the run on its Source push and
        // every fall clock decays, so a value written once is gone by the shutter.
        var update = BodyOf(hunt, "public void Update(GameTime time, Hunter hunter, float enemyBaseHealth, float enemyBaseDamage)");
        Assert.Contains("HoldDeathPose();", update);
        var hold = BodyOf(hunt, "private void HoldDeathPose()");
        Assert.Contains("_devDeathPose is not { } t", hold);
        Assert.Contains("_deathFadeIn = pose.FadeInClock;", hold);
        Assert.Contains("_downedTimer = pose.DownedTimer;", hold);
        // From the restart on, the descent under the black is the real one: StartRun ran, the hold is off.
        var pose = BodyOf(hunt, "public void DevPoseDeathTransition(float t)");
        Assert.Contains("DevHoldReport = false;", pose);
        Assert.Contains("StartRun(", pose);
        // ...and `fightfall` keeps its own hold: under DevShowFall the black never paints, whatever the clocks say.
        Assert.Contains("var black = DevShowFall ? 0f : DeathTransition.Alpha(", hunt);
    }
}
