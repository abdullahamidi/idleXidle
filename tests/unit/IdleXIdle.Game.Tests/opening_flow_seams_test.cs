using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE OPENING READS THE GAME; IT NEVER WRITES IT. The seams that keep a tutorial step from standing in
/// for the deed it is waiting on.
/// </summary>
/// <remarks>
/// The GEAR screen and the host need a graphics device, so the click itself is proven where it runs —
/// the autoplayed fresh-save flow (tools/check_opening_flow.sh), which drives the real mouse path. What
/// CAN be proven here is structural: the facts the opening advances on are read-only production state,
/// and the words its cards use are words the screens actually print.
/// </remarks>
public class OpeningFlowSeamsTest
{
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

    [Fact]
    public void test_the_gear_selection_the_opening_reads_cannot_be_written_from_outside_the_screen()
    {
        // The step waits on GearScreen.PickedItemId. If anything but the screen could set it — or the
        // selection behind it — a tutorial could mark an item "selected" that the inspector never showed.
        foreach (var name in new[] { nameof(GearScreen.PickedItemId), nameof(GearScreen.SelectedItemId) })
        {
            var p = typeof(GearScreen).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(p);
            Assert.False(p!.CanWrite, $"GearScreen.{name} must be read-only outside the screen");
        }
        // ...and the latch it replaced, which only two of the three pick gestures ever set, is gone.
        Assert.Null(typeof(GearScreen).GetProperty("ItemClickedEver"));
    }

    [Fact]
    public void test_the_item_step_is_answered_from_the_screens_selection_and_nothing_else()
    {
        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        // The fact is computed from the screen's own reading...
        Assert.Contains("ItemSelected: OpeningItemPicked()", host, StringComparison.Ordinal);
        Assert.Contains("PickedItemId", host, StringComparison.Ordinal);
        // ...and nothing in the host keeps a tutorial-side flag that could stand in for it.
        foreach (var forbidden in new[] { "_openingPrizeSelected", "PrizeSelected = true", "ItemSelected = true", "ItemClickedEver" })
            Assert.DoesNotContain(forbidden, host, StringComparison.Ordinal);

        // THE PICK IS SET BY EVERY PLAYER GESTURE AND BY NO SCREEN CHOICE. A left click on a bag cell
        // is the gesture the old latch forgot; the adoption and the tab change are the screen's choices.
        var gear = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "GearScreen.cs"));
        Assert.Contains("if (InvCellRect(vis).Contains(hit)) { _selectedId = list[idx].InstanceId; _picked = true;", gear, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_item_card_names_only_what_the_inspector_prints()
    {
        // "Do not teach terminology the UI itself does not show." The inspector's top line is
        // "<RARITY> <SLOT> · <ELEMENT> · LEVEL n" and its stats sit under the heading WHAT IT DOES.
        var body = OpeningScript.Find(OpeningStage.ExplainItem)!.Value.Body;
        var gear = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "GearScreen.cs"));

        Assert.Contains("WHAT IT DOES", body, StringComparison.Ordinal);
        Assert.Contains("Head(\"WHAT IT DOES\")", gear, StringComparison.Ordinal);
        Assert.Contains("LEVEL {item.ItemLevel}", gear, StringComparison.Ordinal);
        Assert.Contains("level", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rarity", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("element", body, StringComparison.OrdinalIgnoreCase);
        // The words the first draft of this card used, none of which the panel prints.
        foreach (var word in new[] { "GRADE", "SOURCE", "TIER" })
            Assert.DoesNotContain(word, body.ToUpperInvariant(), StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_equip_step_lights_the_equip_button_and_not_the_verbs_beside_it()
    {
        // The inspector's UPGRADE / REFORGE / SALVAGE row sits inside the same panel as EQUIP. The lit
        // rectangle is the one a forced click is allowed through, so it must hold EQUIP and none of them.
        foreach (var percent in new[] { 100, 125, 150 })
        {
            UiMetrics.Apply(percent);
            var equip = Assert.Single(GearScreen.Spotlights(TourTarget.EquipButton));
            var panel = Assert.Single(GearScreen.Spotlights(TourTarget.ItemDetail));
            Assert.True(panel.Contains(equip), $"EQUIP is not inside its own inspector at {percent}%");
            Assert.True(equip.Height < panel.Height / 4, $"the EQUIP light is most of the panel at {percent}%");
        }
        UiMetrics.Apply(100);
    }

    [Fact]
    public void test_a_forced_click_reaches_the_control_and_not_the_halo_around_it()
    {
        // The light is grown past its control so the frame art sits inside it. Under "EQUIP IT" that ring
        // lay over the UPGRADE / REFORGE / SALVAGE row, and a click in it went to the FORGE with no card
        // to follow. The click is tested against the control; the halo is for the eye.
        var lit = new Rectangle(400, 300, 240, 60);
        var control = Game1.ClickableOf(lit);
        Assert.True(lit.Contains(control) && control != lit, "the clickable rectangle is the light less its halo");
        Assert.True(control.Contains(lit.Center), "the control itself still takes the click");
        Assert.False(control.Contains(new Point(lit.X + 1, lit.Y + 1)), "a click in the halo reached the screen");

        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        Assert.Contains("OpeningHoles().Any(h => ClickableOf(h).Contains(ChromeMouse))", host, StringComparison.Ordinal);
    }

    [Fact]
    public void test_a_loaded_career_is_resumed_on_a_place_it_can_stand()
    {
        // The rig poses exact stages through Restore, so the resume rule lives where a SAVE is read and
        // nowhere else: a reload onto the Signature card resumes on the wait that sets the cast up again.
        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        Assert.Contains("_opening.Restore(OpeningScript.ResumeStage(OpeningScript.StageOf(_pendingOpeningStage, _saveVersionSeen)))",
                        host, StringComparison.Ordinal);
        var director = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "OpeningDirector.cs"));
        Assert.DoesNotContain("ResumeStage", director, StringComparison.Ordinal);
    }

    [Fact]
    public void test_no_card_about_the_fight_is_raised_over_a_death_still_being_shown()
    {
        // GLEAM waited for the last fall; the Signature card did not, and froze a creature's dissolve under
        // its words for as long as it was read. Both now wait on the fight's own presentation boundary.
        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        Assert.Contains("ClearShown: _expedition.ClearShown", host, StringComparison.Ordinal);
        Assert.Contains("_expedition.HoldBeforeKind == BattleEventKind.Skill && _expedition.FallsPlayed", host, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_coach_does_not_queue_a_lesson_the_opening_is_teaching()
    {
        // An observe beat raised during the opening waited it out and was said again the moment it
        // ended. The two the opening teaches are only ever raised when it is not running.
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs"));
        Assert.Contains("if (_guidanceOff || _fallsSeen > 0 || _opening.Running) return;", game, StringComparison.Ordinal);
        Assert.Contains("_bossesFelled == 0 && !_opening.Running) _coach.Raise(OnboardingLessonId.FirstBoss)", game, StringComparison.Ordinal);
    }

    [Fact]
    public void test_no_notice_is_drawn_over_the_opening_or_repeats_a_screen_it_walks_into()
    {
        // The notice's clock was already held while the opening ran; its toast and its lane were not, so
        // it stood frozen under every card and pushed GEAR's doll out of existence at 150 %.
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs"));
        Assert.Contains("_noticeTimer > 0f && !NoticeHeld && !_opening.Running", game, StringComparison.Ordinal);
        Assert.Contains("var showing = OverlayActive && !_tourActive && !_opening.Running", game, StringComparison.Ordinal);
        Assert.Contains("if (_opening.Running) return;", game, StringComparison.Ordinal);
        // The boot line is the same: not drawn over the opening, and spent when the opening completes.
        Assert.Contains("if (_opening.Running) return;\n        var fade = Math.Clamp(_bootTimer / 1.2f", game.Replace("\r\n", "\n"), StringComparison.Ordinal);
        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        Assert.Contains("if (_opening.Stage == OpeningStage.Complete) { _bootTimer = 0f; Save(); }", host, StringComparison.Ordinal);
        // A lit lesson waits for a notice toast rather than being drawn under it (one surface per slot).
        Assert.Contains("if (NoticeToastHolds) { _coachCard = Rectangle.Empty; return; }", game, StringComparison.Ordinal);
        Assert.Contains("if (_coachBlaze > 0f && !NoticeToastHolds)", game, StringComparison.Ordinal);
        // ...and the unlock notice for a screen the opening walks the player into is not posted at all.
        Assert.Contains("&& !OpeningWalksInto(opened))", game, StringComparison.Ordinal);
        var walked = OpeningScript.Steps.Where(s => s.Mode == TutorialStepMode.ForceNavigate).Select(s => s.Screen).ToHashSet();
        Assert.True(walked.SetEquals(new Activity?[] { Activity.Vault, Activity.Gear }),
                    "the opening walks the player into VAULT and GEAR, and no other screen");
    }

    [Fact]
    public void test_training_is_never_painted_with_nothing_pushed()
    {
        // The boot check's screen walk opened TRAINING on a frame that left Update before the screen's
        // block, and Draw resolved a build from a null Loadout. The push now happens where it is painted.
        var game = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.cs")).Replace("\r\n", "\n");
        Assert.Contains("PushTrainingState();   // never painted with nothing pushed — see PushTrainingState\n"
                        + "            _training.Draw(", game, StringComparison.Ordinal);
        Assert.Contains("PushTrainingState();\n            _training.Update(", game, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_welcome_gift_is_the_item_the_gear_beats_point_at()
    {
        // The item the opening lights, selects and equips is the gift the tutorial boss leaves — one
        // fixed id, so "the selected item is the Welcome Gift" is a comparison and never a guess.
        var gift = Assert.Single(GiftChests.Welcome.Items);
        Assert.Equal("itm_gift_welcome_weapon", gift.InstanceId);
        var host = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Game1.Opening.cs"));
        Assert.Contains("GiftChests.Welcome.Items[0].InstanceId", host, StringComparison.Ordinal);
    }
}
