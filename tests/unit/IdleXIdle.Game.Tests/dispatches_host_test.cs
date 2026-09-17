using System;
using System.IO;
using System.Linq;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE HOST'S INBOX WIRING — the one door the inbox reaches the file through, the reset that puts it
/// back, and the seed that runs only for a file from before the inbox existed.
/// </summary>
/// <remarks>
/// Game1 needs MonoGame to instantiate, so as in <c>host_input_gates_test.cs</c> each invariant is
/// pinned structurally on the source: the statement that must exist, and where it must sit. The
/// wiring's live half — a v7 file booting into v8 with nothing unread — is proved where it runs, by
/// tools/check_boot.sh's migration lanes.
/// </remarks>
public class DispatchesHostTest
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

    private static string Source(string file)
        => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", file)).Replace("\r\n", "\n");

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

    private static int IndexOf(string haystack, string needle)
    {
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(at >= 0, $"`{needle}` not found.");
        return at;
    }

    /// <summary>
    /// The `with` block is the ONLY door to the file. A field added to SaveGame and forgotten here
    /// serialises empty for ever, and the ten-second autosave overwrites the loaded rows within one
    /// interval — the failure no unit test of Core can see.
    /// </summary>
    [Fact]
    public void test_the_save_write_site_carries_all_three_inbox_fields()
    {
        var save = BodyOf(Source("Game1.cs"), "private void Save()");

        Assert.Contains("Dispatches = _inbox.ToSave()", save, StringComparison.Ordinal);
        Assert.Contains("KnownDispatchKeys = _inbox.KnownToSave()", save, StringComparison.Ordinal);
        Assert.Contains("DispatchesOpenedEver = _dispatchesOpenedEver", save, StringComparison.Ordinal);
    }

    [Fact]
    public void test_start_a_new_game_puts_the_inbox_back()
    {
        var reset = BodyOf(Source("Game1.cs"), "private void StartNewGame()");

        Assert.Contains("_inbox.Clear();", reset, StringComparison.Ordinal);
        Assert.Contains("_dispatchesOpenedEver = false;", reset, StringComparison.Ordinal);
        Assert.Contains("_dispatchArrivalOwed = false;", reset, StringComparison.Ordinal);
        Assert.Contains("_pendingInboxSeed = null;", reset, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_loader_restores_the_inbox_into_the_plain_model_and_parks_the_seed()
    {
        // Initialize-safe by construction: the inbox is a plain model, never a screen, and the seed's
        // input is parked until the screens exist (tools/check_init_order.py holds the other half).
        var load = BodyOf(Source("Game1.cs"), "private void LoadOrStartFresh()");

        Assert.Contains("_inbox.Restore(SaveSystem.RestoreDispatches(save), save.KnownDispatchKeys);", load, StringComparison.Ordinal);
        Assert.Contains("_dispatchesOpenedEver = save.DispatchesOpenedEver;", load, StringComparison.Ordinal);
        Assert.Contains("_pendingInboxSeed = save;", load, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_seed_runs_after_the_revealed_set_is_real_and_only_below_the_inbox_version()
    {
        var seed = BodyOf(Source("Game1.cs"), "private void SeedExplained()");

        var restored = IndexOf(seed, "_revealed = Reveal.Restore(");
        var gate = IndexOf(seed, "_saveVersionSeen < Dispatches.FirstVersionWithInbox");
        Assert.True(restored < gate, "the seed must read the revealed set, so it runs after Reveal.Restore");
        Assert.Contains("Dispatches.SeedKnown(", seed, StringComparison.Ordinal);
        // The frozen literal, never the build's own number — the v5 lesson.
        Assert.DoesNotContain("_saveVersionSeen < SaveGame.CurrentVersion", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void test_reading_saves_at_once_and_posting_owes_the_chrome_an_arrival()
    {
        var host = Source("Game1.cs");

        var post = BodyOf(host, "private bool PostDispatch(Dispatch dispatch)");
        Assert.Contains("_dispatchArrivalOwed = true;", post, StringComparison.Ordinal);
        Assert.Contains("_inbox.Post(dispatch)", post, StringComparison.Ordinal);

        // The pattern of ReportOpenedEver: a fact the player just produced survives a crash before the
        // next autosave.
        Assert.Contains("Save();", BodyOf(host, "private void MarkDispatchRead(string key)"), StringComparison.Ordinal);
        Assert.Contains("Save();", BodyOf(host, "private void MarkAllDispatchesRead()"), StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_ledger_prints_the_inbox_row_the_boot_check_reads()
    {
        var ledger = BodyOf(Source("Game1.cs"), "private void DumpLessonLedger()");

        Assert.Contains("$\"inbox\\tunread={_inbox.Unread}\\tknown={_inbox.Known.Count}\\tseeded={_inboxSeeded}\"", ledger, StringComparison.Ordinal);
    }
}
