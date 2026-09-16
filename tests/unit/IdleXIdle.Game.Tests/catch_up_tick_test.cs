using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Warrens;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// ONE PRESS IS ONE ACTION, HOWEVER MANY TIMES THE FRAME UPDATED.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this models, and what it deliberately does not.</b> MonoGame's <c>Game.Tick</c> makes AT
/// LEAST ONE call to Update and exactly one to Draw, and this project never sets
/// <c>IsFixedTimeStep = false</c> — so a frame over budget runs Update twice and Draw once. That is
/// MonoGame's behaviour and is not what is tested here. What IS tested is OUR input ownership under it:
/// a screen driven with the edge true on the first Update of a tick and false on the rest must perform
/// its action exactly once, whether the tick updated one, two or four times.
/// </para>
/// <para>
/// <b>Why "false on the rest" is the honest drive.</b> <c>Game1</c> latches the edge at the top of
/// Update (<c>_clicked = _mouse.Pressed &amp;&amp; _prevMouse.Released</c>) and overwrites
/// <c>_prevMouse</c> in <c>Latch()</c> at the end of every Update. So the second Update of a tick
/// recomputes the edge as FALSE while the button is still physically down. A screen that reads the edge
/// in Update sees it once and acts once; a screen that read it in Draw saw nothing at all, because Draw
/// runs after the last Update of the tick. The "1 update" row is the normal frame and proves the action
/// still happens; the "2" and "4" rows are the catch-up frames and prove it happens exactly once.
/// </para>
/// <para>
/// <b>Why these screens.</b> A screen's Update lays out, hit-tests and edits the model without drawing,
/// so it runs with a null <see cref="UiKit"/> — the seam <c>mastery_click_selects_test.cs</c> already
/// uses. <c>Draw</c> needs a GraphicsDevice and cannot be called here at all, which is exactly why the
/// structural half of this invariant is a gate (<c>tools/check_draw_purity.py</c>) rather than a test:
/// together they say "the action is in Update" and "the action is not in Draw".
/// </para>
/// </remarks>
public class CatchUpTickTest
{
    private readonly ITestOutputHelper _out;

    public CatchUpTickTest(ITestOutputHelper output) => _out = output;

    /// <summary>The three tick shapes: a normal frame, a frame just over budget, and a badly late one.</summary>
    public static TheoryData<int> Ticks() => new() { 1, 2, 4 };

    /// <summary>
    /// Drive ONE tick of <paramref name="updates"/> Updates, with the press held throughout.
    /// </summary>
    /// <remarks>
    /// The edge is true only on the first Update, because that is the only Update that can compute it —
    /// see the class remarks. A test that passed <c>true</c> to every Update would be driving a game
    /// MonoGame does not run, and would fail a correctly-written screen.
    /// </remarks>
    private static void Tick(int updates, Action<bool> update)
    {
        for (var i = 0; i < updates; i++) update(i == 0);
    }

    // ── MASTERY: the TAKE button spends a point. ─────────────────────────────────────────────────
    //
    // The reference screen for this whole architecture (ADR-006), and the one action in the game that
    // is both irreversible-feeling and cheap to pose: a mastery point.

    private static readonly Character Seeker = CharacterRoster.Get("seeker");

    private static Rectangle MasteryTakeBtn
        => (Rectangle)typeof(MasteryScreen)
            .GetProperty("TakeBtn", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static (MasteryScreen Screen, MasteryTree Mastery, MasteryNode Node) PosedMastery()
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var mastery = new MasteryTree();
        mastery.SetEarned(9999);
        var screen = new MasteryScreen(null!)
        {
            Loadout = new PlayerLoadout { SkillCapacity = 4 },
            Mastery = mastery,
            Character = Seeker,
        };
        screen.DevOpenTree(0.30f);
        var node = MasteryCatalog.Nodes.First(n => n.Kind != MasteryKind.Start && mastery.CanTake(n.Id));
        screen.DevPin(node.Id);   // the inspector's subject, which is what TAKE acts on
        return (screen, mastery, node);
    }

    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_the_mastery_take_button_spends_exactly_one_point_per_press(int updates)
    {
        var (screen, mastery, node) = PosedMastery();
        var before = mastery.Spent;

        Tick(updates, clicked => screen.Update(default(KeyboardState), default(KeyboardState),
                                               MasteryTakeBtn.Center, clicked, held: false, wheel: 0));

        _out.WriteLine($"{updates} update(s): spent {before} -> {mastery.Spent}, taken={mastery.IsTaken(node.Id)}");
        Assert.True(mastery.IsTaken(node.Id), $"the node was not taken at all on a {updates}-update tick");
        Assert.Equal(before + node.Cost, mastery.Spent);
    }

    /// <summary>
    /// AND A HELD BUTTON IS NOT A SECOND PRESS. The tick after the one that acted brings no edge at
    /// all, and must do nothing — a drag across a row of controls is one action, not one per frame.
    /// </summary>
    [Fact]
    public void test_holding_the_button_down_after_a_press_takes_nothing_more()
    {
        var (screen, mastery, node) = PosedMastery();
        Tick(1, clicked => screen.Update(default(KeyboardState), default(KeyboardState),
                                         MasteryTakeBtn.Center, clicked, held: false, wheel: 0));
        var afterFirst = mastery.Spent;
        Assert.True(mastery.IsTaken(node.Id));

        // Four more ticks with the button still down: no edge, so no action.
        for (var t = 0; t < 4; t++)
            Tick(2, _ => screen.Update(default(KeyboardState), default(KeyboardState),
                                       MasteryTakeBtn.Center, clicked: false, held: true, wheel: 0));

        _out.WriteLine($"held for four more ticks: spent {afterFirst} -> {mastery.Spent}");
        Assert.Equal(afterFirst, mastery.Spent);
    }

    /// <summary>
    /// AND A PRESS THAT LANDS NOWHERE DOES NOTHING, on any tick shape — the guard against a test that
    /// passes because the screen acts on every Update regardless of where the cursor is.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_a_press_outside_the_button_spends_nothing(int updates)
    {
        var (screen, mastery, _) = PosedMastery();
        var before = mastery.Spent;
        Tick(updates, clicked => screen.Update(default(KeyboardState), default(KeyboardState),
                                               new Point(-9999, -9999), clicked, held: false, wheel: 0));
        Assert.Equal(before, mastery.Spent);
    }

    // ── ROSTER: SET ACTIVE switches champion. ────────────────────────────────────────────────────

    private static Rectangle RosterActionRect
        => (Rectangle)typeof(RosterScreen)
            .GetProperty("ActionRect", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_set_active_switches_the_champion_exactly_once_per_press(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var state = new CharacterState();
        // A second champion to switch TO: Restore is the roster's own entry point and unlocks what it
        // restores, so the fixture can offer one the save has not earned without a second unlock path.
        var other = CharacterRoster.All.First(c => c.Id != state.ActiveId);
        state.Restore(state.ActiveId, state.SaveQuests(), state.SaveUnlocked().Append(other.Id));
        var screen = new RosterScreen(null!);
        screen.DevSelect(other.Id);

        var started = state.ActiveId;
        Assert.NotEqual(other.Id, started);

        Tick(updates, clicked => screen.Update(RosterActionRect.Center, clicked, state));
        var once = state.ActiveId;

        // A second press on the same control, on the same tick shape, must be its own single action —
        // and here it is a no-op, because the champion it names is already active.
        Tick(updates, clicked => screen.Update(RosterActionRect.Center, clicked, state));

        _out.WriteLine($"{updates} update(s): active {started} -> {once} -> {state.ActiveId}");
        Assert.Equal(other.Id, once);
        Assert.Equal(other.Id, state.ActiveId);
    }

    // ── WHAT IS DELIBERATELY NOT HERE, AND WHY. ─────────────────────────────────────────────────
    //
    // WARREN's UPGRADE is the most valuable press of the lot — the host answers it by spending Gleam
    // and Memory Dust — but WarrenScreen.Update calls MeasureInspector(), which wraps its two refusal
    // sentences through the UiKit to find the row list's room. So its Update needs a real font and
    // cannot run against `new WarrenScreen(null!)`. TRAITS cannot even be CONSTRUCTED: its constructor
    // does ArgumentNullException.ThrowIfNull(ui). FORGE's Update reaches text measurement on the same
    // first frame.
    //
    // Adding a null-UiKit escape purely to make those tests possible would mean each test exercised a
    // path the game never takes — the fixture would pose a control measured from nothing — which is
    // worse than not testing it. Those three presses are covered instead by
    // tools/check_draw_purity.py (the press is not in Draw), by the per-screen migration review, and by
    // the live fresh-save opening run at 100 / 125 / 150, which drives the Vault's forced chest open
    // and the Gear equip through the real input path.

    // ── MAP: the second click on a selected region travels. ──────────────────────────────────────
    //
    // Two presses, two meanings, and the second is a navigation — so it is the clearest case of "one
    // edge, one action" on this screen: the first press must select and NOT travel, the second must
    // raise exactly one ENTER request.

    private static Rectangle MapNode(MapScreen s, int i)
        => (Rectangle)typeof(MapScreen).GetMethod("Node", BindingFlags.NonPublic | BindingFlags.Instance)!
                                       .Invoke(s, new object[] { i })!;

    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_the_map_enters_a_region_on_exactly_one_of_its_second_press(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var world = new World();
        var screen = new MapScreen(null!) { World = world, ActiveRegion = Regions.All[0].Id };

        void Press(Rectangle node) => Tick(updates, clicked => screen.Update(
            default(KeyboardState), default(KeyboardState), node.Center, clicked));

        // The chart opens with region 0 already selected, and region 0 is the only one a fresh world has
        // unlocked — so park the selection elsewhere first (region 1 is locked: it selects and refuses,
        // and cannot travel) to reach the "first press selects" state this is about.
        Press(MapNode(screen, 1));
        Assert.Null(screen.ConsumeEnter());

        Press(MapNode(screen, 0));
        var afterFirst = screen.ConsumeEnter();
        Press(MapNode(screen, 0));
        var afterSecond = screen.ConsumeEnter();

        _out.WriteLine($"{updates} update(s): first press -> {afterFirst ?? "(select only)"}, "
                       + $"second press -> {afterSecond ?? "(nothing)"}");
        Assert.Null(afterFirst);                                   // a first press selects; it does not travel
        Assert.Equal(Regions.All[0].Id, afterSecond);              // the second travels, once
        Assert.Null(screen.ConsumeEnter());                        // and the request is a one-shot
    }

    // ── TRAINING: the TRAIN button asks the host to spend Gleam. ─────────────────────────────────
    //
    // The most expensive migrated press in the game: the host answers it with `_hunter.Train(stat)` and
    // a Save(). Before this pass BOTH halves of that were in the paint — the screen raised the request
    // from `DrawRow` and Game1 spent and saved it at the foot of `_training.Draw`.

    private static Rectangle TrainingBuyOfFirstRow(TrainingScreen s)
    {
        var laid = (System.Collections.IEnumerable)typeof(TrainingScreen)
            .GetMethod("LayoutList", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(s, new object[] { 0 })!;
        foreach (var item in laid)
        {
            var row = (Rectangle)item.GetType().GetProperty("Row")!.GetValue(item)!;
            if (row == Rectangle.Empty) continue;   // a group heading has no plate and no button
            return (Rectangle)typeof(TrainingScreen)
                .GetMethod("BuyRect", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { row })!;
        }
        throw new Xunit.Sdk.XunitException("the training list laid out no stat row");
    }

    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_the_train_button_asks_for_exactly_one_rank_per_press(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var hunter = new Hunter();
        hunter.AddGleam(int.MaxValue / 4);   // affordable, so the press ASKS rather than refusing
        var screen = new TrainingScreen(null!)
        {
            Loadout = new PlayerLoadout { SkillCapacity = 4 },
            Mastery = new MasteryTree(),
            Character = Seeker,
        };
        var buy = TrainingBuyOfFirstRow(screen);
        Assert.Null(screen.ConsumeTrain());

        Tick(updates, clicked => screen.Update(default(KeyboardState), default(KeyboardState),
                                               buy.Center, clicked, wheel: 0, hunter));

        var asked = screen.ConsumeTrain();
        _out.WriteLine($"{updates} update(s): buy {buy}, asked {asked?.ToString() ?? "nothing"}");
        Assert.NotNull(asked);
        Assert.Null(screen.ConsumeTrain());   // a one-shot the host drains: never two ranks for one press
    }

    /// <summary>And a press away from the button asks for nothing, on every tick shape.</summary>
    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_a_press_off_the_train_button_asks_for_nothing(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var hunter = new Hunter();
        hunter.AddGleam(int.MaxValue / 4);
        var screen = new TrainingScreen(null!)
        {
            Loadout = new PlayerLoadout { SkillCapacity = 4 },
            Mastery = new MasteryTree(),
            Character = Seeker,
        };
        Tick(updates, clicked => screen.Update(default(KeyboardState), default(KeyboardState),
                                               new Point(-9999, -9999), clicked, wheel: 0, hunter));
        Assert.Null(screen.ConsumeTrain());
    }

    // ── VAULT: OPEN THIS CHEST asks the host to open one. ────────────────────────────────────────
    //
    // The chest open is the FTUE's own forced beat (the authored opening drives this very control), so
    // it is the migrated press most worth pinning: the screen receives
    // `MouseClicked || ForcedScreenClick()` and must raise exactly one request per edge.

    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_the_vault_asks_to_open_a_chest_exactly_once_per_press(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var screen = new VaultScreen(null!);
        var chests = new List<Chest>
        {
            new() { Rarity = Rarity.Rare, Tier = 4, Element = Source.Nature },
        };

        // One settling Update with no press: the screen adopts the pile and lays its cards out.
        screen.Update(0.016f, chests, new Point(-9999, -9999), clicked: false, wheel: 0);
        var found = screen.CardOf(chests[0], chests);
        Assert.NotNull(found);
        var card = found!.Value;
        Assert.Equal(VaultScreen.OpenRequest.None, screen.ConsumeOpen());

        // A first press SELECTS the stack; the second asks to open it. Two edges, two meanings — so
        // this also pins that neither one fires twice.
        Tick(updates, clicked => screen.Update(0.016f, chests, card.Center, clicked, wheel: 0));
        var afterFirst = screen.ConsumeOpen();
        Tick(updates, clicked => screen.Update(0.016f, chests, card.Center, clicked, wheel: 0));
        var afterSecond = screen.ConsumeOpen();

        _out.WriteLine($"{updates} update(s): card {card}, first -> {afterFirst}, second -> {afterSecond}");
        Assert.True(afterFirst == VaultScreen.OpenRequest.Selected || afterSecond == VaultScreen.OpenRequest.Selected,
                    $"neither press asked to open a chest ({afterFirst} then {afterSecond})");
        Assert.Equal(VaultScreen.OpenRequest.None, screen.ConsumeOpen());   // the request is a one-shot
    }

    /// <summary>...and a press on empty space asks for nothing, on any tick shape.</summary>
    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_a_press_off_the_vault_cards_asks_for_nothing(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var screen = new VaultScreen(null!);
        var chests = new List<Chest> { new() { Rarity = Rarity.Rare, Tier = 4, Element = Source.Nature } };
        screen.Update(0.016f, chests, new Point(-9999, -9999), clicked: false, wheel: 0);
        Tick(updates, clicked => screen.Update(0.016f, chests, new Point(-9999, -9999), clicked, wheel: 0));
        Assert.Equal(VaultScreen.OpenRequest.None, screen.ConsumeOpen());
    }

    // ── DISPATCHES: one press picks one letter, and the letter in the pane is read. ───────────────
    //
    // The panel lives inside Game1, which cannot be constructed here — so what is driven is the exact
    // seam TakeDispatchesInput runs: Game1.DispatchRowAt resolves the cursor against the one geometry
    // the paint reads, the EDGE moves the selection, and the SELECTED letter is marked read every
    // frame (the pane is showing it; showing it IS reading it). The Inbox is the real one, so the
    // "exactly one" claim is the model's own, not a mock's.

    private static Rectangle DispatchesList
        => (Rectangle)typeof(Game1).GetProperty("DispatchesListView", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static Inbox ThreeLetters()
    {
        var inbox = new Inbox();
        inbox.Post(Dispatches.Socket(1000));            // oldest
        inbox.Post(Dispatches.Gem(2000));
        inbox.Post(Dispatches.Region("verdant_hollow", 3000));   // newest — row 0
        return inbox;
    }

    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_one_press_in_the_dispatches_list_marks_exactly_one_letter_read(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var inbox = ThreeLetters();
        Assert.Equal(3, inbox.Unread);

        // The cursor on the SECOND row, so a test that passes by marking the top letter fails.
        var list = DispatchesList;
        var at = new Point(list.Center.X, list.Y + UiMetrics.RowHeight + UiMetrics.RowHeight / 2);

        var selected = -1;
        Tick(updates, clicked =>
        {
            var row = Game1.DispatchRowAt(at, first: 0, count: inbox.Count);
            if (clicked && row >= 0) selected = row;
            if (selected >= 0 && selected < inbox.Count) inbox.MarkRead(inbox.Rows[selected].Key);
        });

        _out.WriteLine($"{updates} update(s): selected {selected}, unread {inbox.Unread}");
        Assert.Equal(1, selected);
        Assert.Equal(2, inbox.Unread);
        Assert.True(inbox.Rows[1].Read, "the letter under the cursor was not the one that was read");
        Assert.False(inbox.Rows[0].Read);
    }

    /// <summary>...and a press off the rows picks nothing, on any tick shape.</summary>
    [Theory]
    [MemberData(nameof(Ticks))]
    public void test_a_press_off_the_dispatches_rows_reads_nothing(int updates)
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var inbox = ThreeLetters();
        var selected = -1;
        Tick(updates, clicked =>
        {
            var row = Game1.DispatchRowAt(new Point(-9999, -9999), first: 0, count: inbox.Count);
            if (clicked && row >= 0) selected = row;
            if (selected >= 0 && selected < inbox.Count) inbox.MarkRead(inbox.Rows[selected].Key);
        });
        Assert.Equal(-1, selected);
        Assert.Equal(3, inbox.Unread);
    }
}
