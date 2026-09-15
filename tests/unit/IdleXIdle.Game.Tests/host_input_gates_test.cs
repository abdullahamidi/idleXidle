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
        Assert.Contains("private KeyboardState ScreenKeys => _tourActive || _opening.OwnsInput || WelcomeUp ? default : _keys;", game, StringComparison.Ordinal);
        Assert.Contains("private bool KeyEdge(Keys k) => _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);", game, StringComparison.Ordinal);
        // The hint slot paints OVER the welcome, so under its swallow it must not paint at all.
        Assert.Contains("if (_showTitle || _tourActive || _showHelp || _showSettings || WelcomeUp || !OverlayActive) return null;", game, StringComparison.Ordinal);
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
