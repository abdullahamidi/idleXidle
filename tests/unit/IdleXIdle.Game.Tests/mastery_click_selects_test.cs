using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A CLICK ON THE MASTERY TREE READS A NODE. THE BUTTON BUYS ONE.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, 2026-09-09: <i>"Let's disable automatic purchasing when clicking in the mastery tree;
/// sometimes players just want to click to view information."</i> A left-click on an affordable node
/// pinned it in the inspector AND called <c>TakeNode</c> in the same breath — the point spent, the
/// save written on that frame, and a 0.88-second celebration played, from a click that may have meant
/// "what does this do?". The comment above the line already described select-to-inspect; only the code
/// disagreed.
/// </para>
/// <para>
/// The commit was already built: the inspector's TAKE button, which reads the pinned node and runs the
/// same rules. Driven through the screen's real Update with a null UiKit — Update lays out, hit-tests
/// and edits the model without drawing.
/// </para>
/// </remarks>
public class MasteryClickSelectsTests
{
    private static readonly Type T = typeof(MasteryScreen);
    private static readonly Character Seeker = CharacterRoster.Get("seeker");

    private static Rectangle TakeBtn
        => (Rectangle)T.GetProperty("TakeBtn", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static string? Pinned(MasteryScreen s)
        => (string?)T.GetField("_pinnedNodeId", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s);

    private static Point ScreenPointOf(MasteryScreen s, MasteryNode n)
    {
        var world = (Vector2)T.GetMethod("NodePos", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!
                              .Invoke(s, new object[] { n })!;
        var screen = (Vector2)T.GetMethod("Screen", BindingFlags.NonPublic | BindingFlags.Instance)!
                               .Invoke(s, new object[] { world })!;
        return screen.ToPoint();
    }

    /// <summary>A hunter with points to burn and nothing taken, framed so the tree is hit-testable.</summary>
    private static (MasteryScreen Screen, MasteryTree Mastery) Posed()
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var mastery = new MasteryTree();
        mastery.SetEarned(9999);

        var loadout = new PlayerLoadout { SkillCapacity = 4 };
        var screen = new MasteryScreen(null!) { Loadout = loadout, Mastery = mastery, Character = Seeker };
        // A zoom is passed explicitly: the no-argument overview measures its branch promises
        // through the UiKit, and this harness deliberately has none.
        screen.DevOpenTree(0.30f);
        return (screen, mastery);
    }

    /// <summary>The cheapest node a fresh tree can actually take — the one a curious player clicks first.</summary>
    private static MasteryNode Affordable(MasteryTree mastery)
        => MasteryCatalog.Nodes.First(n => n.Kind != MasteryKind.Start && mastery.CanTake(n.Id));

    private static void ClickAt(MasteryScreen s, Point at)
        => s.Update(default(KeyboardState), default(KeyboardState), at, clicked: true, held: false, wheel: 0);

    [Fact]
    public void test_a_click_on_an_affordable_node_selects_it_and_spends_nothing()
    {
        // THE REGRESSION THE PLAYTEST ASKED FOR, as one assertion.
        var (s, mastery) = Posed();
        var node = Affordable(mastery);
        var spentBefore = mastery.Spent;

        ClickAt(s, ScreenPointOf(s, node));

        Assert.Equal(node.Id, Pinned(s));
        Assert.False(mastery.IsTaken(node.Id), "clicking a node bought it");
        Assert.Equal(spentBefore, mastery.Spent);
    }

    [Fact]
    public void test_the_take_button_spends_the_selected_node()
    {
        // ...and the door that IS meant to spend still does, or the first test would pass on a screen
        // where nothing can be bought at all.
        var (s, mastery) = Posed();
        var node = Affordable(mastery);

        ClickAt(s, ScreenPointOf(s, node));
        ClickAt(s, TakeBtn.Center);

        Assert.True(mastery.IsTaken(node.Id));
        Assert.Equal(node.Cost, mastery.Spent);
    }

    [Fact]
    public void test_clicking_a_node_you_cannot_afford_still_reads_it()
    {
        // The node you cannot afford is the one you most want to read — that was true before this
        // change and must stay true after it.
        var mastery = new MasteryTree();
        mastery.SetEarned(0);
        var screen = new MasteryScreen(null!) { Loadout = new PlayerLoadout(), Mastery = mastery, Character = Seeker };
        UiMetrics.Apply(100);
        UiMotion.Clear();
        screen.DevOpenTree(0.30f);

        var node = MasteryCatalog.Nodes.First(n => n.Kind != MasteryKind.Start);
        ClickAt(screen, ScreenPointOf(screen, node));

        Assert.Equal(node.Id, Pinned(screen));
        Assert.False(mastery.IsTaken(node.Id));
    }

    [Fact]
    public void test_a_second_click_on_the_same_node_still_does_not_buy_it()
    {
        // The accident this removes was one click. Two clicks must not quietly restore it — a
        // double-click shortcut would put the mis-purchase straight back.
        var (s, mastery) = Posed();
        var node = Affordable(mastery);
        var at = ScreenPointOf(s, node);

        ClickAt(s, at);
        ClickAt(s, at);

        Assert.False(mastery.IsTaken(node.Id));
        Assert.Equal(0, mastery.Spent);
    }
}
