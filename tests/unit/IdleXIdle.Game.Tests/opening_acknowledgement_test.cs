using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Progression;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// ONE GRAMMAR FOR EVERY EXPLANATION, AND A SPOTLIGHT THAT IS NEVER A LIE.
/// </summary>
/// <remarks>
/// <para>
/// The playtest this pass answers: the tutorial used the same picture — the same scrim, the same
/// halo, the same card — for beats that wanted four different things. Sometimes CONTINUE, sometimes
/// nothing, sometimes a click on the lit control, and sometimes a lit control that glowed under the
/// mouse while the tutorial's own swallow made it unpressable. A highlighted control that lights up
/// under the pointer looks actionable whether or not it is.
/// </para>
/// <para>
/// So: an EXPLAIN beat takes any key or any click and the production UI under it is presentation
/// only, and a DO beat comes after the explanation is gone and allows exactly the real control the
/// deed needs. The two halves are one change and neither is safe alone — the optional tour shipped
/// "click anywhere" WITHOUT the presentation-only half in 2026-09-09, and every gold ring in the
/// intro became a button that did the wrong thing.
/// </para>
/// <para>
/// These are source assertions because the seams they guard are host wiring — a field read in one
/// place, a gate asked before another — and the host needs a graphics device to instantiate. Same
/// shape as <c>opening_flow_seams_test</c> and <c>attention_owner_test</c> beside them.
/// </para>
/// </remarks>
public class OpeningAcknowledgementTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static string Source(string file)
        => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", file)).Replace("\r\n", "\n");

    // ── THE GRAMMAR ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_an_explanatory_beat_takes_any_key_and_any_click_and_offers_no_button()
    {
        var opening = Source("Game1.Opening.cs");

        // ANY KEY OR ANY CLICK, in one condition, so the two inputs cannot drift apart. A keyboard
        // player and a mouse player answer the same card the same way; neither is the lesser path.
        Assert.Contains("if (_opening.WantsAcknowledgement && (AnyKeyPressed() || _clicked))",
                        opening, StringComparison.Ordinal);

        // ...AND THE CARD SAYS SO, in the one sentence every explanation ends with.
        Assert.Contains("\"PRESS ANY KEY OR CLICK TO CONTINUE\"", opening, StringComparison.Ordinal);

        // NO BUTTON ON AN EXPLANATORY BEAT. The CONTINUE plate was the only thing distinguishing the
        // two identical-looking pictures, which is precisely the reading task this removes: a player
        // had to find and parse a footer to learn whether the spotlight was clickable.
        Assert.DoesNotContain("\"CONTINUE\"", opening, StringComparison.Ordinal);
        Assert.Contains("_openingButton = Rectangle.Empty;", opening, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_way_out_is_never_an_acknowledgement()
    {
        var game = Source("Game1.cs");

        // ESCAPE OPENS SETTINGS, where GUIDANCE lives, on every single beat. A card that consumed it
        // would make the tutorial the one thing in this game a player cannot leave — and "any key"
        // is exactly the rule that would have eaten it.
        Assert.Contains("private static bool IgnoredAsAcknowledgement(Keys k) => k is Keys.Escape",
                        game, StringComparison.Ordinal);
        Assert.Contains("if (_prevKeys.IsKeyUp(k) && !IgnoredAsAcknowledgement(k)) return true;",
                        game, StringComparison.Ordinal);

        // ...and a bare modifier is not a press a player MEANT. A hand reaching for Alt-Tab has read
        // nothing, and an acknowledgement that fires on it is a card the player never saw.
        foreach (var held in new[] { "Keys.LeftShift", "Keys.LeftControl", "Keys.LeftAlt", "Keys.LeftWindows" })
            Assert.Contains(held, game, StringComparison.Ordinal);

        // AND THE OPENING'S OWN ESCAPE PATH IS STILL FIRST AND UNCONDITIONAL.
        var opening = Source("Game1.Opening.cs");
        var escape = opening.IndexOf("if (escape || (_clicked && SettingsGear.Contains(ChromeMouse)))", StringComparison.Ordinal);
        var ack = opening.IndexOf("if (_opening.WantsAcknowledgement && (AnyKeyPressed() || _clicked))", StringComparison.Ordinal);
        Assert.True(escape > 0 && ack > escape,
                    "the way out must be taken before the acknowledgement, or ESCAPE advances the card");
    }

    // ── PRESENTATION ONLY ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_screen_under_an_explanatory_card_gets_no_pointer_at_all()
    {
        var game = Source("Game1.cs");

        // THE MECHANISM IS THE TOUR'S, EXTENDED. The opening swallowed CLICKS from its first day and
        // never the POINTER, so the control it was lighting went on hovering, glowing and lifting
        // under a mouse that could not press it. One term added to a gate that already existed.
        Assert.Contains("if (_tourActive || PointerWithheldFromScreens)", game, StringComparison.Ordinal);
        Assert.Contains("PageCursor = new Point(-1, -1);", game, StringComparison.Ordinal);

        // AND THE HELD-BUTTON FACE WITH IT. PRESSED is the third half of the hover story — the face
        // drops 2 px and darkens for as long as the button is held — and it reads a global the blanked
        // cursor cannot reach, so it is answered where that global is assigned.
        Assert.Contains("&& !_tourActive && !PointerWithheldFromScreens;", game, StringComparison.Ordinal);
    }

    [Fact]
    public void test_only_a_forced_deed_lets_the_pointer_through_and_only_where_the_deed_is()
    {
        var opening = Source("Game1.Opening.cs");

        // THE RULE'S TRUTH TABLE IS TESTED DIRECTLY ABOVE. What this pins is the WIRING: that the host
        // asks that rule with the beat's real facts, and asks it about the same rectangle the click
        // uses — so a control that hovers is a control that would answer, and one that would answer is
        // one that hovers. One rectangle for the light, the hover and the click, resolved once.
        Assert.Contains("=> PointerWithheld(_opening.Running && _opening.OwnsInput,", opening, StringComparison.Ordinal);
        Assert.Contains("_opening.ForcedTarget is not null,", opening, StringComparison.Ordinal);
        Assert.Contains("() => OpeningHoles().Any(h => ClickableOf(h).Contains(ChromeMouse)));",
                        opening, StringComparison.Ordinal);
        Assert.Contains("&& OpeningHoles().Any(h => ClickableOf(h).Contains(ChromeMouse));",
                        opening, StringComparison.Ordinal);
    }

    [Fact]
    public void test_a_lit_rail_tile_does_not_glow_while_it_is_only_being_pointed_at()
    {
        // THE CASE THAT NAMED THIS PASS. "A CHEST DROPPED" is an explanation, and it lights the VAULT
        // tile — the tile the NEXT beat will make the one live control. The rail is chrome and reads
        // ChromeMouse, which stays live so the opening's own card can be hit-tested, so the blanked
        // page cursor does not reach it: without its own answer the lit tile painted a gold hover wash
        // under a mouse HandleNavClick would refuse.
        var game = Source("Game1.cs");
        Assert.Contains("var hover = r.Contains(ChromeMouse) && NavTileTakesPointer(activity);",
                        game, StringComparison.Ordinal);

        var opening = Source("Game1.Opening.cs");
        Assert.Contains("=> NavTileTakesPointer(_opening.Running && _opening.OwnsInput, _opening.ForcedNav, activity);",
                        opening, StringComparison.Ordinal);

        // ...and the chrome's other hoverable, the DISPATCHES envelope, answers the same question.
        Assert.Contains("&& !_tourActive && !(_opening.Running && _opening.OwnsInput);",
                        game, StringComparison.Ordinal);

        // THE BEAT IS REALLY AN EXPLANATION, so the rule above really applies to it. If this ever
        // becomes a ForceNavigate the tile SHOULD glow, and this test should be the thing that says so.
        var chest = OpeningScript.Find(OpeningStage.IntroduceChest)!.Value;
        Assert.Equal(TutorialStepMode.PauseExplain, chest.Mode);
        Assert.Equal(TourTarget.NavRail, chest.Target);
        Assert.Equal(Activity.Vault, OpeningScript.NextForcedScreen(chest.Stage));
    }

    // ── ONE EDGE, AT MOST ONE ACTION ─────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_acknowledging_press_cannot_also_perform_the_deed_underneath_it()
    {
        // ADR-006 §3. The cursor moves on the SAME frame the card is answered, and the beat it moves
        // to may be a forced one whose control is under the pointer right now. Both forced paths read
        // the RAW edge — deliberately, so the lit control can take a click the swallow would eat — so
        // neither is protected by _swallowInput and both must ask this latch.
        var opening = Source("Game1.Opening.cs");
        var game = Source("Game1.cs");

        Assert.Contains("_openingAckSpent = true;", opening, StringComparison.Ordinal);
        Assert.Contains("private bool _openingAckSpent;", opening, StringComparison.Ordinal);

        // THE FORCED ACTION asks it...
        Assert.Contains("=> _clicked && !_openingAckSpent", opening, StringComparison.Ordinal);
        // ...AND THE FORCED NAVIGATION asks it, before it reads the edge at all.
        Assert.Contains("if (_openingAckSpent) return;", game, StringComparison.Ordinal);

        // ...AND SO DOES EVERY OTHER CONSUMER OF THE FRAME'S INPUT, which took the keyboard lane of the
        // opening rig to find. The acknowledgement lands while the beat still owns the frame, but the
        // cursor advances in the SAME Update — so by the time _swallowInput is recomputed the opening
        // can be OVER, and the key or click that closed the last card is still a live edge. The rig
        // pressed K to answer BACK TO THE HUNT and the game opened the VAULT on it (K is the Vault's
        // hotkey); with only the two forced paths guarded, the leak was invisible on the mouse lane.
        Assert.Contains("private bool Pressed(Keys k) => !_swallowInput && !_openingAckSpent && KeyEdge(k);",
                        game, StringComparison.Ordinal);
        Assert.Contains("&& !_swallowInput && !_openingAckSpent;", game, StringComparison.Ordinal);
        Assert.Contains("_tourActive || _opening.OwnsInput || _openingAckSpent", game, StringComparison.Ordinal);

        // ...AND IT LIVES EXACTLY AS LONG AS THE EDGE, cleared beside the sibling latch that spends a
        // modal-opening click, at the top of Update where the edge itself is latched.
        Assert.Contains("_modalOpenedNow = false;", game, StringComparison.Ordinal);
        var cleared = game.IndexOf("_openingAckSpent = false;", StringComparison.Ordinal);
        var edge = game.IndexOf("_clicked = _mouse.LeftButton == ButtonState.Pressed", StringComparison.Ordinal);
        Assert.True(cleared > edge && cleared - edge < 1200,
                    "the acknowledgement latch must be cleared with the edge it describes");
    }

    [Fact]
    public void test_every_explanation_that_precedes_a_forced_beat_is_covered_by_the_latch()
    {
        // WHY THE LATCH IS NOT BELT-AND-BRACES. These are the real transitions where one edge could
        // have done two things: an explanation answered while the pointer rests on the control the
        // very next beat makes live. Both of them light that control while explaining it.
        var steps = OpeningScript.Steps;
        var risky = steps.Where((s, i) => s.Mode == TutorialStepMode.PauseExplain
                                          && i + 1 < steps.Count
                                          && steps[i + 1].Mode is TutorialStepMode.ForceAction
                                                                or TutorialStepMode.ForceNavigate)
                         .Select(s => s.Stage)
                         .ToArray();

        // If a future chapter adds another, this list is what makes somebody look at it. Four today:
        // TRAINING and the VAULT light the rail tile the next beat forces, and the two GEAR beats sit
        // on the screen whose control the next beat forces.
        Assert.Equal(new[]
                     {
                         OpeningStage.IntroduceTraining, OpeningStage.IntroduceChest,
                         OpeningStage.IntroduceItem, OpeningStage.ExplainItem,
                     },
                     risky);

        // ...and the worst of them is same-screen: ITEM STATS is read on GEAR with EQUIP lit under the
        // card, and EQUIP IT is the next beat. Nothing about that is hypothetical.
        var explain = steps.ToList().IndexOf(OpeningScript.Find(OpeningStage.ExplainItem)!.Value);
        Assert.Equal(OpeningStage.ForceEquip, steps[explain + 1].Stage);
        Assert.Equal(Activity.Gear, steps[explain].Screen);
        Assert.Equal(Activity.Gear, steps[explain + 1].Screen);
    }

    // ── THE KEYBOARD IS NOT THE LESSER PATH ──────────────────────────────────────────────────────

    [Fact]
    public void test_the_rig_can_prove_the_key_half_of_the_grammar()
    {
        // A card takes any key OR any click, and a rig that can only click proves exactly half of it.
        // RH_OPENING_KEYS makes the autoplayed hand answer explanations with a key instead.
        var rig = Source("Game1.OpeningRig.cs");
        Assert.Contains("RigVariable(\"RH_OPENING_KEYS\")", rig, StringComparison.Ordinal);
        Assert.Contains("_autoKeys = new KeyboardState(AutoplayAckKey);", rig, StringComparison.Ordinal);

        // AND THE KEY IT PRESSES IS BOUND TO NOTHING. SPACE and ENTER advanced a card before this pass
        // and would pass on the old code; K proves the ANY-key rule and not a key that happened to work.
        Assert.Contains("private static readonly Keys[] AutoplayAckKey = { Keys.K };", rig, StringComparison.Ordinal);

        // ...and the synthetic board replaces the real one through the field every reader already uses.
        var game = Source("Game1.cs");
        Assert.Contains("if (Autoplay && AutoplayUsesKeys) _keys = _autoKeys;", game, StringComparison.Ordinal);

        // THE HAND NO LONGER AIMS AT A BUTTON, because there is not one; it clicks the card, which is
        // not a control either. The one-edge test above is what deliberately aims at the lit thing.
        Assert.Contains("if (_opening.WantsAcknowledgement) return Centre(_openingCard);", rig, StringComparison.Ordinal);
    }

    // ── THE RULE ITSELF, LOOKED AT DIRECTLY ──────────────────────────────────────────────────────

    [Fact]
    public void test_the_pointer_is_withheld_from_every_explanation_and_granted_only_on_the_deed()
    {
        // The whole truth table, called rather than inferred from a wiring assertion. `asked` records
        // whether the expensive question — is the cursor on the lit control? — was put at all, because
        // the branches that must answer without it are the ones that must never cost a screen layout.
        var asked = false;
        bool OnControl(bool answer) { asked = true; return answer; }

        // NO BEAT HAS THE CONTROLS: ordinary play, and the pointer is nobody's business but the page's.
        asked = false;
        Assert.False(Game1.PointerWithheld(beatOwnsInput: false, deedIsForced: false, () => OnControl(true)));
        Assert.False(Game1.PointerWithheld(beatOwnsInput: false, deedIsForced: true, () => OnControl(true)));
        Assert.False(asked, "a beat that owns nothing must decide without resolving a spotlight");

        // AN EXPLANATION — and a forced NAVIGATION, whose live control is a rail tile in the chrome.
        // Withheld wherever the cursor is, so nothing on the page hovers, glows, lifts or tips.
        asked = false;
        Assert.True(Game1.PointerWithheld(beatOwnsInput: true, deedIsForced: false, () => OnControl(true)));
        Assert.True(Game1.PointerWithheld(beatOwnsInput: true, deedIsForced: false, () => OnControl(false)));
        Assert.False(asked, "an explanation is presentation-only everywhere; it must not ask where the cursor is");

        // A FORCED ACTION: granted ON the lit control, withheld everywhere else on the same screen.
        // The second half is the part that is easy to forget — the dozen controls AROUND the lit one
        // are just as unpressable, so they must be just as quiet.
        Assert.False(Game1.PointerWithheld(beatOwnsInput: true, deedIsForced: true, () => true));
        Assert.True(Game1.PointerWithheld(beatOwnsInput: true, deedIsForced: true, () => false));
    }

    [Fact]
    public void test_only_the_one_forced_rail_tile_reacts_to_the_pointer()
    {
        // Ordinary play: every tile behaves like the button it is.
        foreach (var tile in new[] { Activity.Hunt, Activity.Training, Activity.Vault, Activity.Gear })
            Assert.True(Game1.NavTileTakesPointer(beatOwnsInput: false, forcedNav: null, tile));

        // AN EXPLANATION THAT LIGHTS A TILE: lit, and inert. This is "A CHEST DROPPED" — the VAULT tile
        // is the spotlight, and the very next beat makes it live, but right now it answers nothing.
        Assert.False(Game1.NavTileTakesPointer(beatOwnsInput: true, forcedNav: null, Activity.Vault));

        // THE FORCED NAVIGATION THAT FOLLOWS: that one tile reacts, and only that one.
        Assert.True(Game1.NavTileTakesPointer(beatOwnsInput: true, forcedNav: Activity.Vault, Activity.Vault));
        foreach (var other in new[] { Activity.Hunt, Activity.Training, Activity.Gear, Activity.Forge })
            Assert.False(Game1.NavTileTakesPointer(beatOwnsInput: true, forcedNav: Activity.Vault, other));

        // ...and the same for the tile the TRAINING chapter forces.
        Assert.True(Game1.NavTileTakesPointer(beatOwnsInput: true, forcedNav: Activity.Training, Activity.Training));
        Assert.False(Game1.NavTileTakesPointer(beatOwnsInput: true, forcedNav: Activity.Training, Activity.Vault));
    }
}
