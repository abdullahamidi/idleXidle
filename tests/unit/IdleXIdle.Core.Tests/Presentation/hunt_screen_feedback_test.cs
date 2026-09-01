using System;
using System.IO;
using Xunit;

namespace IdleXIdle.Core.Tests.Presentation;

/// <summary>
/// Pins the HUNT screen fixes from playtest ten against the screen's source text.
/// </summary>
/// <remarks>
/// <para>
/// The screen lives in the Game project, which this test project cannot reference (it would drag the
/// whole MonoGame runtime into a headless test host). But every one of these regressions shipped once
/// and was found by a player, so they are pinned the only way they can be from here: against the source
/// itself, the same way the repository's Python gates read it. The reads are deterministic — the file
/// is part of the same checkout the test runs from.
/// </para>
/// <para>
/// What is pinned, by playtest item:
/// item 2 — a reward whose screen is locked is hidden, not advertised;
/// item 10 — the SPEND POINTS button goes to MASTERY (E), not BUILD (B);
/// item 5 — the wave-total health bar above a swarm is gone (the per-creature pips remain);
/// item 6 — a death writes its report to the log and shows a banner pointing there, not a popup;
/// item 9 — the death flash draws in the unclipped HUD pass, so it covers the whole screen.
/// </para>
/// </remarks>
public class HuntScreenFeedbackTests
{
    /// <summary>The screen's source, found by walking up from the test binary to the repo root.</summary>
    private static string Source()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "IdleXIdle.Game", "HuntScreen.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("HuntScreen.cs not found above the test binary.");
    }

    /// <summary>One method's text, so an assertion about DrawComposition cannot pass on DrawBossBar.</summary>
    private static string Slice(string source, string from, string to)
    {
        var start = source.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"marker not found: {from}");
        var end = source.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end > start, $"marker not found after {from}: {to}");
        return source[start..end];
    }

    [Fact]
    public void test_a_locked_reward_is_hidden_from_the_rail()
    {
        var src = Source();

        // Item 2: both errand buttons are gated on a host-fed unlock flag. Without the gate the rail
        // advertised "SPEND 3 POINTS (B)" to a fresh player whose mastery screen was still locked.
        Assert.Contains("ChestCount > 0 && VaultOpen", src);
        Assert.Contains("Mastery.Available > 0 && MasteryOpen", src);
        Assert.Contains("public bool VaultOpen { get; set; }", src);
        Assert.Contains("public bool MasteryOpen { get; set; }", src);
    }

    [Fact]
    public void test_the_points_button_names_and_requests_the_mastery_screen()
    {
        var src = Source();

        // Item 10: the label says (E) — the MASTERY key — and the press raises WantsMastery. The old
        // button said (B) and raised WantsBuild, which landed on a screen where points cannot be spent.
        Assert.Contains("POINT{(Mastery.Available == 1 ? \"\" : \"S\")}  (E)", src);
        Assert.Contains("WantsMastery = true", src);
        Assert.DoesNotContain("WantsBuild = true", src);
    }

    [Fact]
    public void test_the_wave_total_bar_is_gone_but_the_per_creature_pips_stay()
    {
        var src = Source();
        var composition = Slice(src, "private void DrawComposition", "private void DrawNormalEnemy");

        // Item 5: no framed bar art in the composition draw — the aggregate bar summed every creature
        // into one fill and "reset" when a big one died. The per-creature pips are plain fills.
        Assert.DoesNotContain("BarArt(", composition);
        Assert.Contains("CreatureHealthFraction", composition);
    }

    [Fact]
    public void test_a_fall_writes_the_report_to_the_log_and_points_there()
    {
        var src = Source();

        // Item 6: the report goes straight to the RunLog on the frame the run ends...
        Assert.Contains("Log.Add(_run!.Report(isRecord: _run.Wave > _recordToBeat))", src);
        // ...the banner names the wave and points at the log...
        Assert.Contains("YOUR CHAMPION FELL AT WAVE {_fellWave}", src);
        Assert.Contains("THE FULL REPORT IS IN THE LOG — PRESS L", src);
        // ...the log's panel carries the FELL marker...
        Assert.Contains("FELL AT WAVE {r.WallWave}", src);
        // ...and the auto-popup that covered the fall is gone.
        Assert.DoesNotContain("DrawRunReport", src);
    }

    [Fact]
    public void test_the_death_flash_draws_in_the_unclipped_hud_pass()
    {
        var src = Source();
        const string flashDraw =
            "if (_deathFlash > 0f && ShowScreenFlash) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Ember * (_deathFlash * 0.35f));";

        // Item 9: drawn exactly once, and AFTER the HUD pass opens. Inside the arena pass the
        // rasterizer scissors everything to ArenaRect, which cropped the "full screen" flash to the
        // arena rectangle. DrawHunterHud is the first call of the unclipped pass.
        var first = src.IndexOf(flashDraw, StringComparison.Ordinal);
        Assert.True(first >= 0, "the death-flash draw is missing");
        Assert.Equal(first, src.LastIndexOf(flashDraw, StringComparison.Ordinal));

        var hudPass = src.IndexOf("DrawHunterHud(b);", StringComparison.Ordinal);
        Assert.True(hudPass >= 0, "the HUD pass marker is missing");
        Assert.True(first > hudPass, "the death flash is drawn before the unclipped HUD pass — it will be scissored to the arena");
    }
}
