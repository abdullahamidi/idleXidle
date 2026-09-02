using System.Linq;
using System.Reflection;
using IdleXIdle.Core.Characters;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The ROSTER's SET ACTIVE feedback (UI polish brief §31, §73–§82): the switch lands at once, the card
/// you became and the inspector's header light for one <see cref="UiMotion.Transition"/>, the host is
/// handed <c>sfx_nav</c> once, and Reduced Motion reaches the same end state. The rig shoots frame 60
/// and the highlight is eleven frames long, so its shape over time is proved here and its peak is
/// photographed under <c>RH_SHOT_ROSTER=flash</c>.
/// </summary>
/// <remarks>
/// The screen is built with no <see cref="UiKit"/>: <c>Update</c> and <c>Confirm</c> touch only the
/// page-anchored statics and Core, which is what makes the state machine drivable without a device.
/// <see cref="UiMotion"/> is process-wide — the assembly runs its tests one at a time (AssemblyInfo).
/// </remarks>
public class RosterSwitchFeedbackTests
{
    private const string Starter = CharacterRoster.StarterId;   // seeker
    private const string Other = "anvil";                        // the Warden Cinderworks unlocks
    private const string Locked = "chorus";                      // behind Umbral Reach, never conquered here

    private static (RosterScreen Screen, CharacterState State) Rig()
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        UiMetrics.Apply(100);
        var state = new CharacterState();
        state.Refresh(new[] { "cinderworks" });
        Assert.True(state.IsUnlocked(Other));
        Assert.False(state.IsUnlocked(Locked));
        var screen = new RosterScreen(null!);
        return (screen, state);
    }

    private static Rectangle ActionRect()
    {
        var p = typeof(RosterScreen).GetProperty("ActionRect", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(p);
        return (Rectangle)p!.GetValue(null)!;
    }

    /// <summary>A real card's rect — the top-left one — so the hover key under test is the card's own.</summary>
    private static Rectangle Card(int column, int row)
    {
        var m = typeof(RosterScreen).GetMethod("Card", BindingFlags.Static | BindingFlags.NonPublic,
                                               new[] { typeof(int), typeof(int) });
        Assert.NotNull(m);
        return (Rectangle)m!.Invoke(null, new object[] { column, row })!;
    }

    /// <summary>The key the card's hover lift is stored under — <c>UiMotion.KeyOf</c> of its rect.</summary>
    private static int HoverKey => UiMotion.KeyOf(Card(0, 0));

    [Fact]
    public void test_set_active_lights_the_card_and_the_header_once_and_hands_the_host_one_nav_cue()
    {
        var (screen, state) = Rig();
        screen.DevSelect(Other);

        // t = 0: the switch has landed, the highlight is at its peak on the card you became only.
        Assert.True(screen.Confirm(state));
        Assert.Equal(Other, state.ActiveId);
        Assert.Equal(1f, screen.SwitchHighlight(Other));
        Assert.Equal(0f, screen.SwitchHighlight(Starter));
        Assert.Equal(1f, screen.InspectorHighlight);
        Assert.Equal("sfx_nav", screen.ConsumeCue());
        Assert.Null(screen.ConsumeCue());
        Assert.Equal(CharacterRoster.Get(Other).Name, screen.TakeNotice());

        // mid: half a Transition in, half the wash is left.
        UiMotion.Tick(UiMotion.Transition / 2f);
        Assert.InRange(screen.SwitchHighlight(Other), 0.45f, 0.55f);
        Assert.InRange(screen.InspectorHighlight, 0.45f, 0.55f);

        // end: a Transition later it has settled, and nothing re-arms it.
        UiMotion.Tick(UiMotion.Transition);
        Assert.Equal(0f, screen.SwitchHighlight(Other));
        Assert.Equal(0f, screen.InspectorHighlight);
        Assert.Null(screen.ConsumeCue());
        UiMotion.Clear();
    }

    [Fact]
    public void test_reduced_motion_reaches_the_same_end_state_with_the_same_cue()
    {
        var (screen, state) = Rig();
        UiMotion.Reduced = true;
        screen.DevSelect(Other);

        Assert.True(screen.Confirm(state));
        Assert.Equal(Other, state.ActiveId);
        Assert.Equal("sfx_nav", screen.ConsumeCue());

        // THE CARD'S HOVER AND PRESS COLLAPSE. The card's lift is a UiMotion.Ease keyed on its rect, and
        // under Reduced Motion an Ease is its target on the first ask — the hover has no travel left to
        // watch. (The press is a 2 px offset applied straight from UiKit.MouseHeld: no easing at all.)
        Assert.Equal(1f, UiMotion.Ease(HoverKey, 1f));
        Assert.Equal(0f, UiMotion.Ease(HoverKey, 0f));

        // THE SWITCH HIGHLIGHT stays a short fade — brief §32 keeps simple fades, and UiMotion's own
        // contract runs pulses under Reduced Motion for exactly that reason. What matters is that the
        // END STATE is identical: the switch is made, the highlight is gone, nothing re-arms it.
        // Tick is dt-clamped at 0.1 s, so one Transition takes two frames' worth of ticking to drain.
        UiMotion.Tick(0.1f);
        UiMotion.Tick(0.1f);
        Assert.Equal(0f, screen.SwitchHighlight(Other));
        Assert.Equal(0f, screen.InspectorHighlight);
        UiMotion.Tick(0.1f);
        Assert.Equal(0f, screen.SwitchHighlight(Other));
        UiMotion.Reduced = false;
        UiMotion.Clear();
    }

    [Fact]
    public void test_the_press_is_taken_in_update_on_the_buttons_own_rect_and_only_once()
    {
        var (screen, state) = Rig();
        screen.DevSelect(Other);
        var at = ActionRect().Center;

        // A click elsewhere does nothing; a click on SET ACTIVE switches.
        screen.Update(new Point(at.X, at.Y - ActionRect().Height * 2), clicked: true, state);
        Assert.Equal(Starter, state.ActiveId);
        Assert.Null(screen.ConsumeCue());

        screen.Update(at, clicked: true, state);
        Assert.Equal(Other, state.ActiveId);
        Assert.Equal("sfx_nav", screen.ConsumeCue());
        Assert.Equal(1f, screen.SwitchHighlight(Other));

        // Clicking the same spot again, now that this hunter is active, is a press on a button that is
        // not drawn: no second switch, no second cue, no re-armed highlight.
        UiMotion.Tick(UiMotion.Transition / 2f);
        var mid = screen.SwitchHighlight(Other);
        screen.Update(at, clicked: true, state);
        Assert.Equal(Other, state.ActiveId);
        Assert.Null(screen.ConsumeCue());
        Assert.Equal(mid, screen.SwitchHighlight(Other));
        UiMotion.Clear();
    }

    [Fact]
    public void test_a_refused_switch_arms_nothing()
    {
        var (screen, state) = Rig();

        // Locked: refused, silent.
        screen.DevSelect(Locked);
        Assert.False(screen.Confirm(state));
        Assert.Equal(Starter, state.ActiveId);
        Assert.Null(screen.ConsumeCue());
        Assert.Equal(0f, screen.SwitchHighlight(Locked));
        Assert.Equal(0f, screen.InspectorHighlight);

        // Already you: refused, silent.
        screen.DevSelect(Starter);
        Assert.False(screen.Confirm(state));
        Assert.Null(screen.ConsumeCue());
        Assert.Equal(0f, screen.SwitchHighlight(Starter));
        Assert.Null(screen.TakeNotice());
        UiMotion.Clear();
    }

    [Fact]
    public void test_a_held_card_is_still_clicked_on_the_rect_it_was_drawn_at()
    {
        // §15 / §27: PRESSED drops the DRAWN face two pixels; the rect that takes the hit does not move.
        // A card picked with the button held selects the same hunter as one picked with it up — which is
        // what makes the depression a look rather than a two-pixel dead band along every card's top edge.
        var (screen, state) = Rig();
        var cell = CharacterRoster.Grid.First(g => g.Character.Id == Other);
        var at = Card(cell.Column, cell.Row).Center;
        try
        {
            UiKit.MouseHeld = true;
            screen.Update(at, clicked: true, state);
            Assert.True(screen.Confirm(state));      // the click landed on THIS card, held or not
            Assert.Equal(Other, state.ActiveId);
        }
        finally { UiKit.MouseHeld = false; UiMotion.Clear(); }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public void test_the_button_the_press_lands_on_is_the_one_the_page_draws(int percent)
    {
        // §15: one authoritative rect per control. The click Update takes and the button Draw shows
        // are the same static ActionRect at every profile, inside the page and above its foot.
        UiMetrics.Apply(percent);
        var r = ActionRect();
        Assert.True(r.Width > 0 && r.Height > 0);
        Assert.True(r.Bottom <= UiKit.Page.Bottom && r.X >= 0 && r.Right <= UiKit.Page.Right, $"{r} leaves the page at {percent}%");
        Assert.Equal(UiMetrics.ButtonHeightPrimary, r.Height);
        UiMetrics.Apply(100);
    }
}
