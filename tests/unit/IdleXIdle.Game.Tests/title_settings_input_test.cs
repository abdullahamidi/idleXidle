using System;
using System.IO;
using System.Linq;
using IdleXIdle.Game;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE SETTINGS PANEL TAKES ITS CLICKS ON EVERY PATH THAT PAINTS IT — the title screen included.
/// </summary>
/// <remarks>
/// ADR-006 moved every control on the panel out of <c>DrawSettings</c> and into <c>UpdateSettings</c>,
/// and wired that call into the in-game modal block at the end of <c>Game1.Update</c>. The title
/// screen's branch of Update returns long before that block, so a panel opened from the title painted
/// (hover worked, because Draw still reads the cursor) and took no click at all — 2026-09-15, "I can't
/// click settings panel buttons however hovering is working". <c>Game1.Update</c> needs a
/// GraphicsDevice, so as in <c>title_menu_input_test.cs</c> the provable half is structural: the
/// branch that returns for the title must call the panel's Update before it does.
/// </remarks>
public class TitleSettingsInputTest
{
    private readonly ITestOutputHelper _out;

    public TitleSettingsInputTest(ITestOutputHelper output) => _out = output;

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

    /// <summary>The body of one method of Game1.cs, brace-matched from its signature.</summary>
    private static string BodyOf(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"Game1.cs no longer contains `{signature}`.");
        var open = source.IndexOf('{', at);
        Assert.True(open > 0, $"`{signature}` has no body.");
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
        }
        throw new InvalidOperationException($"`{signature}` never closes.");
    }

    private static string Game1Source()
        => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");

    /// <summary>
    /// The title's own settings block — from its F10 toggle to the <c>Latch(gameTime);</c> it returns
    /// through — calls <c>UpdateSettings()</c>. The in-game block below it still does too.
    /// </summary>
    [Fact]
    public void test_the_title_screens_settings_panel_takes_its_clicks_in_update()
    {
        var update = BodyOf(Game1Source(), "protected override void Update(GameTime gameTime)");

        const string toggle = "if (Pressed(Keys.F10)) _showSettings = !_showSettings;";
        var titleToggle = update.IndexOf(toggle, StringComparison.Ordinal);
        Assert.True(titleToggle >= 0, "the title branch of Update no longer toggles the settings panel on F10.");
        var titleBlock = update[titleToggle..];
        titleBlock = titleBlock[..titleBlock.IndexOf("Latch(gameTime);", StringComparison.Ordinal)];
        _out.WriteLine("title settings block:\n" + titleBlock);

        Assert.Contains("UpdateSettings();", titleBlock, StringComparison.Ordinal);

        // And the in-game path, which is where ADR-006 wired it first: the same call, further down.
        var modalBlock = update.IndexOf("if (_showSettings) UpdateSettings();", StringComparison.Ordinal);
        Assert.True(modalBlock > titleToggle, "the in-game modal block no longer runs the settings panel's Update.");
    }

    /// <summary>
    /// The other half of the invariant: <c>DrawSettings</c> paints from the cursor and never from the
    /// click edge, and the CLOSE control is decided in <c>UpdateSettings</c>.
    /// </summary>
    [Fact]
    public void test_the_settings_panel_paints_without_reading_the_click_edge()
    {
        var game = Game1Source();

        var draw = BodyOf(game, "private void DrawSettings()");
        foreach (var spelling in new[] { "_clicked", "ClickedIn(", "clicked: true" })
            Assert.DoesNotContain(spelling, draw, StringComparison.Ordinal);

        var update = BodyOf(game, "private void UpdateSettings()");
        Assert.Contains("ClickedIn(f.Close", update, StringComparison.Ordinal);
    }
}
