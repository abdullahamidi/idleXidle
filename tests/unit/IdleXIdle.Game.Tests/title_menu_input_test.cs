using System;
using System.IO;
using System.Linq;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// ONE PHYSICAL CLICK, ONE MENU ACTION — AND IT DOES NOT DEPEND ON WHETHER DRAW RAN.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> The title menu's only mouse hit test lived inside <c>DrawTitle</c>, against the
/// click edge latched in Update. MonoGame's <c>IsFixedTimeStep</c> is never overridden in this project,
/// and a fixed-timestep tick makes <i>at least one</i> call to Update and exactly one to Draw — so on a
/// catch-up tick Update runs twice and Draw once. The first Update latches the edge; <c>Latch()</c> then
/// copies <c>_mouse</c> into <c>_prevMouse</c>; the second Update recomputes the edge as false; and the
/// single Draw that follows hit-tests an edge that no longer exists. A human holds the button for three
/// to six frames, so it never re-arms — the press is simply dropped. Fresh-save validation saw exactly
/// one dropped title click on a slow 150 % frame, which is the profile whose glyph atlases are re-keyed
/// and therefore the one most likely to run over budget.
/// </para>
/// <para>
/// <b>What can be proven here.</b> <c>Game1.Update</c> needs a GraphicsDevice, so this file cannot pump
/// a real click; the live proof is the autoplayed opening (<c>tools/check_opening_flow.sh</c>), which
/// clicks BEGIN THE HUNT through the real input path at every profile. What IS provable without a
/// device is the structure — that the plates are laid out by one pure function, that the function is
/// what both halves read, and that <c>DrawTitle</c> no longer touches the click edge at all. The
/// structural assertion is the one that fails if somebody moves the hit test back.
/// </para>
/// </remarks>
public class TitleMenuInputTest
{
    private readonly ITestOutputHelper _out;

    public TitleMenuInputTest(ITestOutputHelper output) => _out = output;

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
        Assert.True(at >= 0, $"Game1.cs no longer contains `{signature}` — the title screen was renamed without its test.");
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

    /// <summary>
    /// THE INVARIANT, structurally: nothing in <c>DrawTitle</c> reads the click edge, and the title's
    /// Update branch does.
    /// </summary>
    /// <remarks>
    /// Read from the source rather than by reflection because what matters is WHERE the call sits, and a
    /// method body is the only thing that says so. The three names checked are every way this codebase
    /// spells "consume a click": the latched field itself, the rect helper, and <c>UiKit.Button</c>,
    /// which hit-tests as it renders.
    /// </remarks>
    [Fact]
    public void test_the_title_menu_consumes_its_click_in_update_and_never_in_draw()
    {
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");

        var draw = BodyOf(game, "private void DrawTitle()");
        foreach (var spelling in new[] { "_clicked", "ClickedIn", "_ui.Button(" })
            Assert.DoesNotContain(spelling, draw, StringComparison.Ordinal);
        // It still PAINTS from the one layout — so what is hit-tested and what is painted cannot differ.
        Assert.Contains("TitlePlate(i)", draw, StringComparison.Ordinal);

        // And the Update branch — the keyboard block the title returns from — now owns the hit test.
        var update = game[game.IndexOf("if (Pressed(Keys.Up)) _titleCursor", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("Latch(gameTime);", StringComparison.Ordinal)];
        Assert.Contains("TitlePlate(i)", update, StringComparison.Ordinal);
        Assert.Contains("if (_clicked) { ChooseTitleItem(i); break; }", update, StringComparison.Ordinal);
        Assert.Contains("_titleCursor = i;", update, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE MECHANISM, DETERMINISTICALLY: on a catch-up tick a Draw-time consumer loses the click and an
    /// Update-time consumer does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Game1's own loop needs a GraphicsDevice, so this models MonoGame's documented tick contract —
    /// <i>at least one Update, exactly one Draw</i> — over Game1's exact edge rule: latch
    /// <c>clicked = mouse.Pressed &amp;&amp; prev.Released</c> at the top of Update, and copy
    /// <c>prev = mouse</c> at the end of it (<c>Latch()</c>). Everything about the defect follows from
    /// those two lines meeting a tick that updates twice, and nothing about it is specific to the title:
    /// it is why <c>_clicked</c> must be read where it is latched.
    /// </para>
    /// <para>
    /// Note the button is HELD for every frame of the tick, as a real one is for three to six frames.
    /// That is what makes the loss permanent rather than recoverable: the edge never re-arms while the
    /// finger is down.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1)]   // a normal tick
    [InlineData(2)]   // a frame just over budget
    [InlineData(4)]   // badly over budget
    public void test_a_click_edge_read_in_draw_is_lost_on_a_catch_up_tick_and_one_read_in_update_is_not(int updatesThisTick)
    {
        var drawActions = 0;
        var updateActions = 0;

        // Game1's two lines, verbatim in shape: the edge latched at the top of Update, and _prevMouse
        // overwritten at the end of it.
        var prevPressed = false;
        var clicked = false;
        const bool pressed = true;   // the button is DOWN for the whole tick

        for (var i = 0; i < updatesThisTick; i++)
        {
            clicked = pressed && !prevPressed;   // Game1.cs: _clicked = _mouse.Pressed && _prevMouse.Released
            if (clicked) updateActions++;        // consumed HERE — the title's new home
            prevPressed = pressed;               // Game1.Latch(): _prevMouse = _mouse
        }
        if (clicked) drawActions++;              // consumed in Draw — the title's old home

        _out.WriteLine($"{updatesThisTick} update(s) + 1 draw -> update-side {updateActions}, draw-side {drawActions}");

        // Consumed in Update: exactly one action per press edge, whatever the tick did.
        Assert.Equal(1, updateActions);
        // Consumed in Draw: correct on a normal tick, silently DROPPED on a catch-up tick.
        Assert.Equal(updatesThisTick == 1 ? 1 : 0, drawActions);
    }

    /// <summary>
    /// ONE ACTION PER CLICK: the plates do not overlap, so a single cursor position is inside at most
    /// one of them — and the loop that walks them breaks on the first hit.
    /// </summary>
    /// <remarks>
    /// The <c>break</c> is belt-and-braces given non-overlapping rects, and it is what makes the
    /// guarantee hold even if a future layout crowds them: at most one <c>ChooseTitleItem</c> per press
    /// edge, and the edge itself exists for exactly one Update.
    /// </remarks>
    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void test_no_cursor_position_can_be_inside_two_title_plates(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            var plates = Enumerable.Range(0, Game1.TitleItems).Select(Game1.TitlePlate).ToList();
            Assert.Equal(3, plates.Count);   // CONTINUE / BEGIN THE HUNT, SETTINGS, QUIT
            for (var i = 0; i < plates.Count; i++)
            {
                _out.WriteLine($"{percent}% plate {i}: {plates[i]}");
                for (var j = i + 1; j < plates.Count; j++)
                    Assert.False(plates[i].Intersects(plates[j]),
                                 $"title plates {i} {plates[i]} and {j} {plates[j]} overlap at {percent}%");
            }
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>
    /// EVERY PLATE IS ON THE CANVAS AND BIG ENOUGH TO HIT, at every density profile.
    /// </summary>
    /// <remarks>
    /// The plates are authored in true 1920x1080 chrome and deliberately do NOT scale — only their
    /// labels follow the rung — so this is the assertion that the deliberate choice is still safe: a
    /// 640x96 plate is far over the hit floor even at 150 %, where that floor is largest.
    /// </remarks>
    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void test_every_title_plate_is_on_the_canvas_and_over_the_hit_floor(int percent)
    {
        UiMetrics.Apply(percent);
        try
        {
            foreach (var i in Enumerable.Range(0, Game1.TitleItems))
            {
                var plate = Game1.TitlePlate(i);
                Assert.True(plate.Left >= 0 && plate.Right <= 1920 && plate.Top >= 0 && plate.Bottom <= 1080,
                            $"title plate {i} {plate} is off the 1920x1080 canvas at {percent}%");
                Assert.True(plate.Height >= UiMetrics.HitTargetMinimum && plate.Width >= UiMetrics.HitTargetMinimum,
                            $"title plate {i} is {plate.Width}x{plate.Height} against a {UiMetrics.HitTargetMinimum} px hit floor at {percent}%");
            }
        }
        finally { UiMetrics.Apply(100); }
    }

    /// <summary>
    /// THE AUTOPLAY RIG STILL AIMS AT A PLATE. The opening rig clicks the title's first plate through
    /// the real input path, and its aim point is written as a literal — so it has to be pinned to the
    /// layout it aims at rather than left to drift.
    /// </summary>
    /// <remarks>
    /// That rig is the only automated title click in the project, and it is the live half of this fix's
    /// proof (<c>tools/check_opening_flow.sh</c> at 100 / 125 / 150). If the plates ever move and the
    /// aim does not, the opening-flow check would hang on BEGIN THE HUNT rather than report a layout
    /// change — a harness lying about the thing it is checking.
    /// </remarks>
    [Fact]
    public void test_the_autoplay_rigs_title_aim_lands_on_the_first_plate()
    {
        var rig = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.OpeningRig.cs")).Replace("\r\n", "\n");
        var at = rig.IndexOf("if (_showTitle) return new Vector2(", StringComparison.Ordinal);
        Assert.True(at >= 0, "the opening rig no longer aims at the title screen.");
        var args = rig[(at + "if (_showTitle) return new Vector2(".Length)..];
        args = args[..args.IndexOf(')')];
        var parts = args.Split(',').Select(p => float.Parse(p.Trim().TrimEnd('f'),
                                                            System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var aim = new Point((int)parts[0], (int)parts[1]);
        _out.WriteLine($"the rig aims at {aim}; plate 0 is {Game1.TitlePlate(0)}");
        Assert.True(Game1.TitlePlate(0).Contains(aim),
                    $"the opening rig aims at {aim}, which is outside the first title plate {Game1.TitlePlate(0)}");
    }
}
