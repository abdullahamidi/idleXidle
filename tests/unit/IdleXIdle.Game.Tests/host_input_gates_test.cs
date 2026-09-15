using System;
using System.IO;
using System.Linq;
using IdleXIdle.Game;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE HOST'S INPUT GATES AGREE WITH WHAT IT PAINTS -- five sites the ADR-006 reachability review of
/// 2026-09-15 found where a control was drawn live and its input was read late, elsewhere, or not at all.
/// </summary>
/// <remarks>
/// <c>Game1.Update</c> needs a GraphicsDevice, so as in <c>title_menu_input_test.cs</c> each invariant is
/// pinned structurally: the statement that must exist, and the order it must sit in.
/// </remarks>
public class HostInputGatesTest
{
    private readonly ITestOutputHelper _out;

    public HostInputGatesTest(ITestOutputHelper output) => _out = output;

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

    private static string UpdateBody() => BodyOf(Source("Game1.cs"), "protected override void Update(GameTime gameTime)");

    private static int IndexOf(string haystack, string needle)
    {
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(at >= 0, $"`{needle}` not found.");
        return at;
    }

    /// <summary>
    /// F1 and F10 swap the HELP and SETTINGS panels rather than stacking them: the modal block runs
    /// exactly one of the two, so a stacked HELP painted its close icon dead.
    /// </summary>
    [Fact]
    public void test_f1_and_f10_never_stack_the_help_and_settings_panels()
    {
        var update = UpdateBody();
        Assert.Contains("if (Pressed(Keys.F1)) { _showHelp = !_showHelp; if (_showHelp) _showSettings = false; }", update, StringComparison.Ordinal);
        Assert.Contains("if (Pressed(Keys.F10)) { _showSettings = !_showSettings; if (_showSettings) _showHelp = false; }", update, StringComparison.Ordinal);
        // The title's F10 has no HELP to clear -- F1 is not read there -- and stays the plain toggle.
        Assert.Contains("if (Pressed(Keys.F10)) _showSettings = !_showSettings;", update, StringComparison.Ordinal);
    }

    /// <summary>
    /// The notice toast's close is read after this frame's click edge is latched and after the frame's
    /// swallow is settled, and before the gear, the hint slot, the nav and the screens take the click.
    /// </summary>
    [Fact]
    public void test_the_notice_toasts_close_is_read_after_the_edge_and_the_swallow_are_settled()
    {
        var update = UpdateBody();
        var dismiss = IndexOf(update, "DismissNoticeIfClosed();");
        var latch = IndexOf(update, "_clicked = _mouse.LeftButton == ButtonState.Pressed");
        var swallow = IndexOf(update, "_swallowInput = _tourActive || _opening.OwnsInput");
        var gear = IndexOf(update, "SettingsGear.Contains(ChromeMouse)");
        _out.WriteLine($"latch {latch} < swallow {swallow} < dismiss {dismiss} < gear {gear}");
        Assert.True(latch < dismiss, "the toast's close is hit-tested before this frame's click edge exists.");
        Assert.True(swallow < dismiss, "the toast's swallow is overwritten by the frame's swallow recompute.");
        Assert.True(dismiss < gear, "the toast's close is read after the gear and the screens have taken the click.");
        Assert.Equal(1, update.Split("DismissNoticeIfClosed();").Length - 1);
    }

    /// <summary>
    /// The WELCOME BACK panel owns the frame's input for as long as it is up, and its own keys are raw
    /// edges so the swallow cannot silence them.
    /// </summary>
    [Fact]
    public void test_the_welcome_panel_swallows_the_frames_input_and_keeps_its_own_keys()
    {
        var game = Source("Game1.cs");
        var update = BodyOf(game, "protected override void Update(GameTime gameTime)");
        Assert.Contains("_swallowInput = _tourActive || _opening.OwnsInput || WelcomeUp;", update, StringComparison.Ordinal);
        Assert.DoesNotContain("if (WelcomeUp) _swallowInput = true;", update, StringComparison.Ordinal);
        Assert.Contains("if (WelcomeUp && (KeyEdge(Keys.Enter) || KeyEdge(Keys.Space)", update, StringComparison.Ordinal);
        Assert.Contains("KeyEdge(Keys.Escape)", update, StringComparison.Ordinal);
        // The screens' keyboard is empty under the welcome -- and under the two host panels and the open log,
        // which hold the host's own keys the same way (attention_owner_test).
        Assert.Contains("private KeyboardState ScreenKeys => _tourActive || _opening.OwnsInput || WelcomeUp || _showSettings || _showHelp || _expedition.LogOpen ? default : _keys;", game, StringComparison.Ordinal);
        Assert.Contains("private bool KeyEdge(Keys k) => _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);", game, StringComparison.Ordinal);
        // The hint slot paints OVER the welcome, so under its swallow it must not paint at all: the welcome is
        // Modal tier to the attention owner, and the slot is the coach's tier, so it asks the owner and waits.
        Assert.Contains("if (!OverlayActive || AttentionOwnedAbove(AttentionOwner.Coach)) return null;", game, StringComparison.Ordinal);
        Assert.Contains("ProductionModalUp || WelcomeUp ? AttentionOwner.Modal", game, StringComparison.Ordinal);
    }

    /// <summary>A right click is gated by every modal a left click is.</summary>
    [Fact]
    public void test_right_click_is_gated_like_a_left_click()
    {
        var game = Source("Game1.cs");
        Assert.Contains("private bool MouseClicked => _clicked && !_showSettings && !_showHelp && !WelcomeUp && !_swallowInput;", game, StringComparison.Ordinal);
        Assert.Contains("private bool MouseRightClicked => _rightClicked && !_showSettings && !_showHelp && !WelcomeUp && !_swallowInput;", game, StringComparison.Ordinal);
    }

    /// <summary>
    /// The coach yields to a fall on the HUNT only. The fight ticks on every screen, so a fall's downed
    /// beat runs while the Forge is up; gated without the screen term, a lesson lit there blinked spotlight
    /// to card and back for every background fall -- the transition's state dragged onto another screen.
    /// The gate is the attention owner's now: the death owns the frame only while it is ON the page, and
    /// neither the spotlight nor the lesson card asks about the fall itself.
    /// </summary>
    [Fact]
    public void test_the_coach_yields_to_a_fall_only_on_the_hunt()
    {
        var game = Source("Game1.cs");
        var update = UpdateBody();
        Assert.Contains(": !OverlayActive && _expedition.DeathTransitionUp ? AttentionOwner.Death", update, StringComparison.Ordinal);
        var lights = game[game.IndexOf("private bool CoachLightsIt(OnboardingLessonId id)", StringComparison.Ordinal)..];
        lights = lights[..lights.IndexOf(';')];
        Assert.Contains("AttentionOwnedAbove(AttentionOwner.Coach)", lights, StringComparison.Ordinal);
        Assert.DoesNotContain("DeathTransitionUp", lights, StringComparison.Ordinal);
        // The lesson card leaves another screen and asks the owner in one breath, and never about the fall.
        var showing = BodyOf(game, "private OnboardingLessonId? HuntLessonShowing()");
        Assert.Contains("if (OverlayActive || AttentionOwnedAbove(AttentionOwner.Coach)) return null;", showing, StringComparison.Ordinal);
        Assert.DoesNotContain("DeathTransitionUp", showing, StringComparison.Ordinal);
    }

    /// <summary>
    /// While an authored beat owns input, Escape opens the settings panel -- and Escape closes it again.
    /// One Escape, one layer: a handler that spends the edge marks it spent for the rest of the frame.
    /// </summary>
    [Fact]
    public void test_escape_closes_the_settings_panel_the_opening_opened()
    {
        var take = BodyOf(Source("Game1.Opening.cs"), "private void TakeOpeningInput()");
        var closer = IndexOf(take, "if (escape && !_settingsEscSpent && _showSettings) { _showSettings = false; _settingsEscSpent = true; }");
        var opener = IndexOf(take, "_modalOpenedNow = true;");
        Assert.True(closer < opener, "the closer must be tested before the opener re-opens on the same edge.");

        // And the in-game Escape block marks the edge spent once it has acted, so the opening's own
        // Escape below it cannot act on the same press.
        var update = UpdateBody();
        var esc = IndexOf(update, "if (!_settingsEscSpent && Pressed(Keys.Escape))\n        {");
        var block = update[esc..];
        block = block[..IndexOf(block, "\n        }\n")];
        Assert.Contains("_settingsEscSpent = true;", block, StringComparison.Ordinal);
    }
}
