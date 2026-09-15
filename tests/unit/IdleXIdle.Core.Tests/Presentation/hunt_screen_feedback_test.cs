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
/// item 6 — a death writes its report to the log and fades the stage to black for the next descent; nothing over
///          the arena announces it (attention ownership pass, 2026-09-15);
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

        // Item 10: the press raises WantsMastery — the MASTERY tree is where points are spent. The old
        // button raised WantsBuild, which landed on a screen where points cannot be spent. UX V2 P1.1: the
        // label names no key any more (the tour and the help sheet teach the keys; a door says what it opens).
        Assert.Contains("$\"SPEND {Mastery.Available} POINT{(Mastery.Available == 1 ? \"\" : \"S\")}\"", src);
        Assert.DoesNotContain("(E)\"", src);
        Assert.Contains("WantsMastery = true", src);
        // Only the LOG's ADJUST BUILD door raises WantsBuild (UX V2 P1.2); the utility never does.
        var utility = Slice(src, "private void DrawRightColumn", "// \u2500\u2500 The death transition");
        Assert.DoesNotContain("WantsBuild", utility);
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
    public void test_a_fall_writes_the_report_to_the_log_and_fades_the_stage_to_black()
    {
        var src = Source();

        // Item 6: the report goes straight to the RunLog on the frame the run ends...
        Assert.Contains("Log.Add(_run!.Report(isRecord: _run.Wave > _recordToBeat))", src);
        // ...the log's panel carries the FELL marker, where the report keeps...
        Assert.Contains("FELL AT WAVE {r.WallWave}", src);
        // ...and NOTHING OVER THE ARENA ANNOUNCES THE FALL. Falling is part of the idle loop, so it is not
        // news: no plate, no door over the fight, no header line forecasting the next wave, no popup. What
        // the player sees is the collapse, the stage fading to black, and the next descent already running
        // when it comes back (attention ownership pass, 2026-09-15).
        foreach (var gone in new[] { "FallPlate", "FallDoor", "FellBannerSeconds", "READ THE LOG", "BACK TO WAVE", "RECOVERING", "DrawRunReport" })
            Assert.DoesNotContain(gone, src);

        // THE BLACK IS ONE FILL, in the unclipped HUD pass AFTER the flash (so it covers the rails and the
        // panels, and the flash is the instant of death in front of nothing), read from the two clocks
        // and the Reduced Motion switch alone — never from a click, a wheel, or the host's toast gate.
        const string fill = "if (black > 0f) _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), Color.Black * black);";
        var at = src.IndexOf(fill, StringComparison.Ordinal);
        Assert.True(at >= 0, "the death transition's fill is missing");
        Assert.Equal(at, src.LastIndexOf(fill, StringComparison.Ordinal));
        Assert.True(at > src.IndexOf("Ember * (_deathFlash * 0.35f)", StringComparison.Ordinal), "the black is painted before the flash");
        Assert.True(at > src.IndexOf("DrawHunterHud(b);", StringComparison.Ordinal), "the black is painted inside the arena pass — it will be scissored");
        var reads = Slice(src, "var black = ", fill);
        Assert.Contains("DeathTransition.Alpha(_mode == Mode.Downed, _downedTimer, _deathFadeIn, UiMotion.Reduced)", reads);
        Assert.DoesNotContain("clicked", reads);
        Assert.DoesNotContain("wheel", reads);
        Assert.DoesNotContain("suppressBanner", reads);

        // ...and this screen's own HUD does not answer under it. DECIDED IN UPDATE (ADR-006): TakeInput
        // refuses the medallion and the rail while the transition is up, after the log and after the
        // screen gate, so a click under the black cannot fire what the black covers.
        var input = Slice(src, "public void TakeInput(Point mouse, bool clicked, int wheel, bool huntOnTop)", "private string _buildStamp");
        var logGate = input.IndexOf("if (_logOpen) { TakeLogInput(mouse, clicked, wheel); return; }", StringComparison.Ordinal);
        var topGate = input.IndexOf("if (!huntOnTop) { _deathFadeIn = 0f; return; }", StringComparison.Ordinal);
        var fallGate = input.IndexOf("if (DeathTransitionUp) return;", StringComparison.Ordinal);
        var medallion = input.IndexOf("UiKit.ClickedIn(LogButtonRect", StringComparison.Ordinal);
        Assert.True(logGate >= 0, "the log gate is missing from TakeInput");
        Assert.True(topGate > logGate, "leaving the screen must snap the transition, after the log gate");
        Assert.True(fallGate > topGate, "the death transition's refusal must follow the screen gate");
        Assert.True(medallion > fallGate, "the medallion is hit-tested before the death transition refuses it");
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
