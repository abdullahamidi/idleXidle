using System;
using Microsoft.Xna.Framework;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleXIdle.Game;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// ONE OWNER DECIDES WHOSE TURN IT IS. The host computes who has the player's attention once per
/// frame, into one field, and every surface that competes for reading attention -- the coach's light,
/// its cards, the slot, the toasts -- asks that field "is anything above me up?" and waits if so.
/// </summary>
/// <remarks>
/// <c>Game1.Update</c> needs a GraphicsDevice, so as in <c>host_input_gates_test.cs</c> the invariants
/// are pinned structurally: the one assignment site and its place in the frame, the gates that read the
/// owner (and keep no private list of what outranks them), the clocks that hold under it, and the
/// input holes it closes -- host hotkeys under a panel, the rail under the open log.
/// </remarks>
public class AttentionOwnerTest
{
    private readonly ITestOutputHelper _out;

    public AttentionOwnerTest(ITestOutputHelper output) => _out = output;

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

    private static string Game1() => Source("Game1.cs");

    /// <summary>Every partial of the host, so a count over "the host" cannot miss a file.</summary>
    private static string Host()
    {
        var dir = Path.GetDirectoryName(RepoFile("src", "IdleXIdle.Game", "Game1.cs"))!;
        return string.Concat(Directory.GetFiles(dir, "Game1*.cs").OrderBy(p => p)
                                      .Select(p => File.ReadAllText(p).Replace("\r\n", "\n")));
    }

    /// <summary>One member's text from its signature: a brace-matched body, or an expression body to its semicolon.</summary>
    private static string MemberOf(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"source no longer contains `{signature}`.");
        var open = source.IndexOf('{', at);
        var semi = source.IndexOf(';', at);
        if (open < 0 || (semi >= 0 && semi < open)) return source[at..(semi + 1)];
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[at..(i + 1)];
        }
        throw new InvalidOperationException($"`{signature}` never closes.");
    }

    private static string UpdateBody() => MemberOf(Game1(), "protected override void Update(GameTime gameTime)");

    private static int IndexOf(string haystack, string needle)
    {
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(at >= 0, $"`{needle}` not found.");
        return at;
    }

    private const string OwnerRead = "AttentionOwnedAbove(AttentionOwner.";

    /// <summary>The ranking is the enum's order, highest last, and nothing else in the host ranks anything.</summary>
    [Fact]
    public void test_the_tiers_rank_in_the_specs_order()
    {
        var order = new[]
        {
            AttentionOwner.None, AttentionOwner.Feedback, AttentionOwner.Coach, AttentionOwner.Report,
            AttentionOwner.Death, AttentionOwner.Reveal, AttentionOwner.Modal, AttentionOwner.Opening,
        };
        Assert.Equal(order, Enum.GetValues<AttentionOwner>());
        for (var i = 1; i < order.Length; i++) Assert.True(order[i - 1] < order[i]);
    }

    /// <summary>
    /// The field is assigned in exactly one place, inside Update, after the panels, the welcome, the log's
    /// doors and the reveal's tick have settled the frame's flags and before the fight is held -- so it
    /// stands on a frame the authored opening freezes the game.
    /// </summary>
    [Fact]
    public void test_the_owner_is_decided_once_per_frame_before_the_fight_is_held()
    {
        var host = Host();
        var assignments = Regex.Matches(host, @"\b_attention\s*=(?!=)").Count;
        Assert.Equal(1, assignments);

        var update = UpdateBody();
        var assign = IndexOf(update, "_attention = _opening.Running ? AttentionOwner.Opening");
        var logKey = IndexOf(update, "if ((Pressed(Keys.L) || wantsLog)");
        var revealTick = IndexOf(update, "_forge.TickReveal(");
        var hold = IndexOf(update, "if (!_opening.HoldsFight || !_expedition.RunStarted) UpdateExpedition(gameTime);");
        _out.WriteLine($"L {logKey} < reveal tick {revealTick} < assign {assign} < hold {hold}");
        Assert.True(logKey < assign, "the owner is decided before the log's doors have settled LogOpen.");
        Assert.True(revealTick < assign, "the owner is decided before the reveal's tick has settled RevealActive.");
        Assert.True(assign < hold, "the owner is decided inside the fight's hold, so a held frame has no owner.");

        // The derivation, highest first, each tier from the fact that IS that tier.
        Assert.Contains(": ProductionModalUp || WelcomeUp ? AttentionOwner.Modal", update, StringComparison.Ordinal);
        Assert.Contains(": _forge.RevealActive ? AttentionOwner.Reveal", update, StringComparison.Ordinal);
        // The death owns the frame only while it is ON the page: the fight ticks on every screen, and a
        // fall behind the Forge is not something the player is looking at.
        Assert.Contains(": !OverlayActive && _expedition.DeathTransitionUp ? AttentionOwner.Death", update, StringComparison.Ordinal);
        Assert.Contains(": _expedition.LogOpen ? AttentionOwner.Report", update, StringComparison.Ordinal);
        // The coach owns the frame only when something is LIT: a beat that aims at a fight control while the
        // player is on the Forge has no hole there, paints nothing, and must not hold the news back.
        Assert.Contains(": _coach.Showing is { } aimed && CoachHoles(aimed).Length > 0 ? AttentionOwner.Coach", update, StringComparison.Ordinal);
        Assert.Contains(": _noticeTimer > 0f || _lockedTimer > 0f || _feedbackToastTimer > 0f ? AttentionOwner.Feedback", update, StringComparison.Ordinal);
        Assert.Contains(": AttentionOwner.None;", update, StringComparison.Ordinal);

        // The modal tier names every production surface once, here and nowhere else. The three HOST
        // panels are named once BELOW it, in HostModalUp, which every gate that asks about a host panel
        // reads -- so a fourth panel joins the tier by joining one line.
        var modal = MemberOf(Game1(), "private bool ProductionModalUp");
        foreach (var term in new[] { "_showTitle", "HostModalUp", "_showTypeSpec", "_tourActive",
                                     "_masteryScreen.SpecialisationOpen", "_vault.ModalUp", "_forge.ConfirmOpen" })
            Assert.Contains(term, modal, StringComparison.Ordinal);
        var panels = MemberOf(Game1(), "private bool HostModalUp");
        foreach (var term in new[] { "_showSettings", "_showHelp", "_showDispatches" })
            Assert.Contains(term, panels, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every gate that paints something the player is meant to read asks the owner, and none keeps its
    /// own list of the surfaces that outrank it -- the drift this replaced was six hand-listed predicates.
    /// </summary>
    [Fact]
    public void test_every_foreground_gate_reads_the_owner_and_keeps_no_private_list()
    {
        var game = Game1();
        var gates = new[]
        {
            "private bool CoachLightsIt(OnboardingLessonId id)",
            "private OnboardingLessonId? HuntLessonShowing()",
            "private SlotContent? SlotShowing()",
            "private ScreenBanner? ScreenBannerShowing()",
            "private bool LearnOffered()",
            "private bool DispatchesOffered()",
            "private void DrawCoachSpotlight()",
            "private void DrawNoticeToast()",
            "private void ReserveNoticeLane()",
            "private void DrawBootToast()",
            "private void DrawLockedToast()",
        };
        // Three predicates read the owner on a gate's behalf; a gate may read the owner through them.
        var lights = MemberOf(game, "private bool CoachLightsIt(OnboardingLessonId id)");
        var boot = MemberOf(game, "private bool BootToastShowing");
        var toast = MemberOf(game, "private bool NoticeToastShowing");
        Assert.Contains(OwnerRead, lights, StringComparison.Ordinal);
        Assert.Contains(OwnerRead, boot, StringComparison.Ordinal);
        Assert.Contains(OwnerRead, toast, StringComparison.Ordinal);

        var privateLists = new[]
        {
            "_showSettings", "_showHelp", "_showDispatches", "_showTitle", "_tourActive", "WelcomeUp", "_showTypeSpec",
            "_expedition.LogOpen", "_forge.RevealActive", "DeathTransitionUp", "_opening.Running",
            "SpecialisationOpen", "NoticeToastHolds",
        };
        foreach (var gate in gates)
        {
            var body = MemberOf(game, gate);
            var reads = body.Contains(OwnerRead, StringComparison.Ordinal)
                        || body.Contains("BootToastShowing", StringComparison.Ordinal)
                        || body.Contains("NoticeToastShowing", StringComparison.Ordinal)
                        || body.Contains("CoachLightsIt(", StringComparison.Ordinal);
            Assert.True(reads, $"{gate} does not read the attention owner.");
            foreach (var term in privateLists)
                Assert.False(body.Contains(term, StringComparison.Ordinal), $"{gate} still hand-lists `{term}`.");
        }
        // The toast's inversion: it is Feedback tier and waits for a lit lesson, not the other way round.
        Assert.DoesNotContain("NoticeToastHolds", game, StringComparison.Ordinal);
        // The plate, its lane and its cue ask ONE predicate, and that predicate asks the owner.
        Assert.Contains("if (!NoticeToastShowing) return;", MemberOf(game, "private void DrawNoticeToast()"), StringComparison.Ordinal);
        Assert.Contains("&& !AttentionOwnedAbove(AttentionOwner.Feedback) && !NoticeHeld;", toast, StringComparison.Ordinal);
        Assert.Contains("if (!BootToastShowing) return;", MemberOf(game, "private void DrawBootToast()"), StringComparison.Ordinal);
        Assert.Contains("AttentionOwnedAbove(AttentionOwner.Feedback)) return;", MemberOf(game, "private void DrawLockedToast()"), StringComparison.Ordinal);
        Assert.Contains("private bool CoachLightsIt(OnboardingLessonId id) => !AttentionOwnedAbove(AttentionOwner.Coach) && CoachAims(id);", game, StringComparison.Ordinal);
        Assert.Contains("if (OverlayActive || AttentionOwnedAbove(AttentionOwner.Coach)) return null;", MemberOf(game, "private OnboardingLessonId? HuntLessonShowing()"), StringComparison.Ordinal);
        Assert.Contains("if (!OverlayActive || AttentionOwnedAbove(AttentionOwner.Feedback)) return null;", MemberOf(game, "private SlotContent? SlotShowing()"), StringComparison.Ordinal);
        Assert.Contains("if (AttentionOwnedAbove(AttentionOwner.Feedback)) return null;", MemberOf(game, "private ScreenBanner? ScreenBannerShowing()"), StringComparison.Ordinal);
        // The ? opens a tour, so it is the coach's tier: live over a lit lesson, gone under the log and above.
        Assert.Contains("!AttentionOwnedAbove(AttentionOwner.Coach)", MemberOf(game, "private bool LearnOffered()"), StringComparison.Ordinal);
        Assert.Contains("var showing = OverlayActive && NoticeToastShowing;", MemberOf(game, "private void ReserveNoticeLane()"), StringComparison.Ordinal);
    }

    /// <summary>
    /// TWO CARDS ON ONE SCREEN IS THE THING THE OWNER EXISTS TO PREVENT. The slot at the top of a menu
    /// screen is the coach's own tier, so it stands down when the coach LIGHTS something on that screen
    /// -- the WARREN photographed a lit TRAINING lesson (scrim, brackets, card) with an unrelated
    /// AN UPGRADE IS AFFORDABLE plate over the same page (production/qa/evidence/dispatches/producers).
    /// </summary>
    /// <remarks>
    /// Its guard reads Feedback rather than Coach for exactly that reason: an owner AT the coach's tier
    /// is a lit lesson, and the slot is where a lesson goes when it is not lit.
    /// </remarks>
    [Fact]
    public void test_the_slot_stands_down_while_the_coach_lights_the_same_screen()
    {
        var slot = MemberOf(Game1(), "private SlotContent? SlotShowing()");
        Assert.Contains("if (!OverlayActive || AttentionOwnedAbove(AttentionOwner.Feedback)) return null;", slot, StringComparison.Ordinal);
        Assert.DoesNotContain("AttentionOwnedAbove(AttentionOwner.Coach)", slot, StringComparison.Ordinal);
        // ...and the slot still carries a lesson the coach did NOT light, which is the path this leaves alone.
        Assert.Contains("!CoachLightsIt(lesson)", slot, StringComparison.Ordinal);
        // ...and the LOADOUT's mark on the row a slot NOTE is about reads that same predicate: the outline
        // points at the row the note explains, so a mark painted where no note is drawn explains nothing.
        Assert.Contains("_loadoutScreen.RevealingNewSlot = SlotShowing() is { Kind: SlotKind.Note };", Game1(), StringComparison.Ordinal);
        Assert.DoesNotContain("RevealingNewSlot = ScreenBannerShowing()", Game1(), StringComparison.Ordinal);
    }

    /// <summary>The clocks under an owner hold rather than burn, and none of them is advanced in Draw.</summary>
    [Fact]
    public void test_the_clocks_under_the_owner_hold_rather_than_burn()
    {
        var game = Game1();
        var update = UpdateBody();
        Assert.Contains("if (_noticeTimer > 0f && !NoticeHeld && !AttentionOwnedAbove(AttentionOwner.Feedback)) _noticeTimer = Math.Max(0f, _noticeTimer - dt);", update, StringComparison.Ordinal);
        Assert.Contains("if (_noticeTimer <= 0f && _noticeQueue.Count > 0 && !AttentionOwnedAbove(AttentionOwner.Feedback))", update, StringComparison.Ordinal);
        Assert.Contains("if (_bootTimer > 0f && !AttentionOwnedAbove(AttentionOwner.Feedback)) _bootTimer = Math.Max(0f, _bootTimer - dt);", update, StringComparison.Ordinal);
        // The refusal toast may simply expire: a refusal is over the moment it was seen or not.
        Assert.Contains("if (_lockedTimer > 0f) _lockedTimer = Math.Max(0f, _lockedTimer - dt);", update, StringComparison.Ordinal);
        var blaze = MemberOf(game, "private void TickCoachBlaze(float dt)");
        Assert.Contains("if (_coachBlaze > 0f && CoachLit) _coachBlaze = MathF.Max(0f, _coachBlaze - dt);", blaze, StringComparison.Ordinal);
        Assert.Contains("private bool CoachLit => _coach.Showing is { } lit && CoachLightsIt(lit);", game, StringComparison.Ordinal);

        foreach (var draw in new[] { "private void DrawNoticeToast()", "private void DrawBootToast()", "private void DrawCoachSpotlight()", "private void DrawLockedToast()" })
        {
            var body = MemberOf(game, draw);
            foreach (var tick in new[] { "-= dt", "_noticeTimer =", "_bootTimer =", "_coachBlaze =", "_lockedTimer =" })
                Assert.False(body.Contains(tick, StringComparison.Ordinal), $"{draw} advances a clock (`{tick}`).");
        }
    }

    /// <summary>
    /// No stale rect survives a frame its control was not painted: a hidden × cannot take a click.
    /// </summary>
    [Fact]
    public void test_no_close_rect_survives_a_frame_its_control_was_not_painted()
    {
        var game = Game1();
        var notice = MemberOf(game, "private void DrawNoticeToast()");
        Assert.True(IndexOf(notice, "_noticeCloseRect = Rectangle.Empty;") < IndexOf(notice, "return;"),
                    "the notice's close rect must be cleared before any early return.");
        var spot = MemberOf(game, "private void DrawCoachSpotlight()");
        foreach (Match ret in Regex.Matches(spot, @"return;"))
        {
            var lineStart = spot.LastIndexOf('\n', ret.Index) + 1;
            var line = spot[lineStart..ret.Index];
            Assert.True(line.Contains("_coachCard = Rectangle.Empty;", StringComparison.Ordinal),
                        $"an early return leaves the coach's card rect live: `{line.Trim()} return;`");
        }
    }

    /// <summary>
    /// The host's own keys and the rail refuse under a production panel and under the open log; Esc
    /// closes the log one layer at a time; the screens' keyboard is empty under the same three.
    /// </summary>
    [Fact]
    public void test_the_hosts_keys_and_the_rail_refuse_under_a_panel_or_the_open_log()
    {
        var game = Game1();
        var update = UpdateBody();
        Assert.Contains("var panelHolds = HostModalUp;", update, StringComparison.Ordinal);
        Assert.Contains("var navHolds = ceremonyHolds || panelHolds || _expedition.LogOpen;", update, StringComparison.Ordinal);
        Assert.Contains("for (var navKey = 0; navKey < Nav.Length && !navHolds; navKey++)", update, StringComparison.Ordinal);
        Assert.Contains("if ((Pressed(Keys.L) || wantsLog) && !ceremonyHolds && !panelHolds)", update, StringComparison.Ordinal);
        Assert.Contains("if (_expedition.LogOpen && !panelHolds)", update, StringComparison.Ordinal);
        Assert.Contains("if (Pressed(Keys.T) && !navHolds)", update, StringComparison.Ordinal);
        // The reveal's own keys wait for a panel over it too.
        Assert.Contains("(revealClick || (!panelHolds && (Pressed(Keys.Space) || Pressed(Keys.Enter))))", update, StringComparison.Ordinal);
        // Esc, F1 and F10 stay raw: they are how the panels open and close.
        Assert.Contains("if (Pressed(Keys.F1)) { _showHelp = !_showHelp; if (_showHelp) _showSettings = false; }", update, StringComparison.Ordinal);

        var nav = MemberOf(game, "private void HandleNavClick()");
        Assert.Contains("if (HostModalUp || _expedition.LogOpen) return;", nav, StringComparison.Ordinal);
        // ...and the rail is not painted as live over the log it cannot answer under.
        var hex = MemberOf(game, "private void DrawHexNav()");
        Assert.Contains("if (HostModalUp || _expedition.LogOpen) return;", hex, StringComparison.Ordinal);

        Assert.Contains("private KeyboardState ScreenKeys => _tourActive || _opening.OwnsInput || WelcomeUp || HostModalUp || _expedition.LogOpen ? default : _keys;", game, StringComparison.Ordinal);

        // Esc peels the log after the three panels and before the Forge's question.
        var help = IndexOf(update, "else if (_showHelp) _showHelp = false;");
        var mail = IndexOf(update, "else if (_showDispatches) _showDispatches = false;");
        var log = IndexOf(update, "else if (_expedition.LogOpen) _expedition.ToggleLog();");
        var forge = IndexOf(update, "else if (_showForge && _forge.ConfirmOpen) _forge.CancelConfirm();");
        Assert.True(help < mail && mail < log && log < forge, "Esc must close the log after the panels and before the Forge's question.");
    }

    /// <summary>The director is fed from the owner, and the fall's end hushes it for a beat.</summary>
    [Fact]
    public void test_the_director_reads_the_owner_and_the_falls_end_hushes_it()
    {
        var game = Game1();
        Assert.Contains("RewardUp: _forge.RevealActive || WelcomeUp,", game, StringComparison.Ordinal);
        // A PRODUCTION MODAL OR THE OPENING, named as those two things. The tier-minus-welcome form it
        // replaces was not WRONG, it was fragile: its subtraction could only bite where the opening
        // owned and the welcome stood alone, and WelcomeUp is itself `… && !_opening.Running`, so that
        // frame cannot occur. The gain is that the term no longer leans on a guard held in another
        // property -- the hole opens the day WelcomeUp's definition changes.
        Assert.Contains("ModalUp: _attention == AttentionOwner.Opening || ProductionModalUp,", game, StringComparison.Ordinal);
        Assert.DoesNotContain("!(WelcomeUp && !ProductionModalUp)", game, StringComparison.Ordinal);
        Assert.Contains("ReportUp: _expedition.LogOpen,", game, StringComparison.Ordinal);
        Assert.Contains("DeathUp: _attention == AttentionOwner.Death));", game, StringComparison.Ordinal);
        var update = UpdateBody();
        Assert.Contains("if (deathWasUp && !_deathWasUp) _coach.Hush(OnboardingDirector.QuietAfterReward);", update, StringComparison.Ordinal);
        // The edge is the ON-PAGE death's, the same fact the owner's Death tier reads: the fight ticks on
        // every screen, and a hush on the end of a fall behind the Forge dropped a lit lesson there for
        // the quiet and re-armed its full scrim when it came back -- the background-fall blink, through
        // the director this time.
        Assert.Contains("_deathWasUp = !OverlayActive && _expedition.DeathTransitionUp;", update, StringComparison.Ordinal);
        Assert.DoesNotContain("_deathWasUp = _expedition.DeathTransitionUp;", update, StringComparison.Ordinal);
        // No second hush when the log closes: the lesson is simply chosen fresh.
        Assert.Single(Regex.Matches(Host(), @"_coach\.Hush\("));
    }

    /// <summary>
    /// The hunt's own banners (BOSS INCOMING, WAVE CLEARED) are suppressed by a boot toast that is ON
    /// SCREEN, by the opening and by the welcome -- never by a toast that is merely waiting. The boot
    /// clock holds under anything above Feedback now, so the raw timer would have kept the fight's own
    /// presentation down for as long as a lit lesson stood, plus seven seconds.
    /// </summary>
    [Fact]
    public void test_the_hunts_own_banners_are_not_held_back_by_a_toast_that_is_waiting()
    {
        var game = Game1();
        Assert.Contains("EnemyArtFor(_activeRegion), BootToastShowing || _opening.Running || WelcomeUp);", game, StringComparison.Ordinal);
        Assert.DoesNotContain("EnemyArtFor(_activeRegion), _bootTimer > 0f || WelcomeUp);", game, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE CHROME'S BACKGROUND FLOURISHES WAIT, AND ITS STATE DOES NOT. A rail tile unlocks, an unread
    /// dot appears and a purse goes up whatever else is on screen -- that is what the player owns. What
    /// waits is the MOTION about it: the chains springing off a tile, the HUNT tile's hurt and cleared
    /// washes, the dot's breath, and the pills' "+N". Those four ask the COACH's tier, because a
    /// ceremony over a chest reveal, a fall, the open log, a modal or the authored opening is the game
    /// asking to be looked at in two places at once. The envelope's arrival is the ONE exception and is
    /// pinned as such below: it waits for a frame nothing owns at all, because a letter is a message
    /// and direct feedback outranks background news. <c>UiMotion.Tick</c> is never paused -- every
    /// screen's hover ease rides on it -- so what is deferred or skipped is the ARM.
    /// </summary>
    [Fact]
    public void test_the_background_flourishes_wait_while_something_owns_the_players_eyes()
    {
        var game = Game1();
        var update = UpdateBody();
        var fight = MemberOf(game, "private void UpdateExpedition(GameTime gameTime)");
        var chrome = MemberOf(game, "private void TickChromeMotion(float dt)");
        var hex = MemberOf(game, "private void DrawHexNav()");

        // ── THE CHAIN BREAK IS DEFERRED, NOT SKIPPED: it is a once-per-screen-per-career ceremony, so
        //    it is latched where the tile opens and spent on the first free frame.
        Assert.Contains("if (!_navBreakPending.Contains(opened)) _navBreakPending.Add(opened);", update, StringComparison.Ordinal);
        Assert.Contains("if (_navBreakPending.Count > 0 && !AttentionOwnedAbove(AttentionOwner.Coach))", update, StringComparison.Ordinal);
        // The reveal LATCHES and the owner SPENDS, in that order: the flash lives in the drain, not
        // where the tile opens -- which is the whole of this change.
        Assert.True(IndexOf(update, "if (!_navBreakPending.Contains(opened)) _navBreakPending.Add(opened);")
                    < IndexOf(update, "if (_navBreakPending.Count > 0 && !AttentionOwnedAbove(AttentionOwner.Coach))"),
                    "the latch must be written where the tile opens and spent later in the frame.");
        Assert.True(IndexOf(update, "if (_navBreakPending.Count > 0 && !AttentionOwnedAbove(AttentionOwner.Coach))")
                    < IndexOf(update, "UiMotion.Flash(NavBreakKey(opened), NavChainSeconds);"),
                    "the break's Flash must live inside the drain, not at the reveal.");
        // One writer, one drain -- across every partial of the host, and the reset's own clearing aside.
        Assert.Single(Regex.Matches(Host(), @"_navBreakPending\.Add\("));
        Assert.Single(Regex.Matches(update, @"_navBreakPending\.Clear\(\)"));
        Assert.Single(Regex.Matches(Host(), @"UiMotion\.Flash\(NavBreakKey\("));

        // ── THE HUNT TILE'S TICKS ARE SKIPPED: a bite and a cleared wave are over the moment they
        //    happen, so a tick played a minute late would be a lie about the fight. Both guards are
        //    pinned whole, so neither arm can drift back out of the one that holds it.
        Assert.Contains("&& !AttentionOwnedAbove(AttentionOwner.Coach))\n            UiMotion.Flash(NavHuntHurtKey, UiMotion.Fast);", fight, StringComparison.Ordinal);
        // ...and the cleared wave WITH its boss mark, inside ONE guard: the brighter, longer wash is the
        // same tick, so the flag that brightens it is set with the tick or not at all.
        Assert.Contains("if (!AttentionOwnedAbove(AttentionOwner.Coach))\n"
                        + "            {\n"
                        + "                UiMotion.Flash(NavHuntClearKey, r.IsBoss ? UiMotion.Reward : UiMotion.Transition);\n"
                        + "                _navHuntBoss = r.IsBoss;\n"
                        + "            }", fight, StringComparison.Ordinal);

        // ── THE LETTER'S ARRIVAL WAITS FOR A FRAME NOTHING OWNS -- the one gate in the host that asks
        //    AttentionOwnedAbove(None), because a letter is a MESSAGE and direct feedback outranks
        //    background news: a halo and an audible cue under a live notice toast or a locked-tile
        //    refusal is two messages landing together. It is deliberately NOT the rung the rail's
        //    ceremonies use, so the two are pinned against each other here.
        Assert.Contains("if (_dispatchArrivalHeld && !AttentionOwnedAbove(AttentionOwner.None))", update, StringComparison.Ordinal);
        Assert.DoesNotContain("if (_dispatchArrivalHeld && !AttentionOwnedAbove(AttentionOwner.Coach))", update, StringComparison.Ordinal);
        //    ...and it is the ONLY gate that asks it, so the asymmetry cannot spread by copy-paste.
        Assert.Single(Regex.Matches(Host(), @"AttentionOwnedAbove\(AttentionOwner\.None\)"));
        //    The break stays at the coach's rung: silent, on the rail rather than in the toast's band,
        //    and the only one that career gets.
        Assert.Contains("if (_navBreakPending.Count > 0 && !AttentionOwnedAbove(AttentionOwner.Coach))", update, StringComparison.Ordinal);

        // ── THE PILLS' "+N" IS DEFERRED BY ITS OWN BANK: the accumulator keeps filling and empties into
        //    one badge on the first free frame; a spend in between cancels it, as it always did.
        Assert.Contains("if (_pillGainAcc[i] >= bar && !AttentionOwnedAbove(AttentionOwner.Coach))", chrome, StringComparison.Ordinal);
        Assert.Contains("_pillGainAcc[i] = 0;", chrome, StringComparison.Ordinal);

        // ── THE NEW DOT IS STATE; ONLY ITS BREATH WAITS. The dot itself is painted from `isNew` alone.
        Assert.Contains("if (!UiMotion.Reduced && !AttentionOwnedAbove(AttentionOwner.Coach))", hex, StringComparison.Ordinal);
        Assert.Contains("if (isNew)", hex, StringComparison.Ordinal);
        var dot = IndexOf(hex, "_ui.Disc(_batch, at, UiInk.Danger);");
        var halo = IndexOf(hex, "if (!UiMotion.Reduced && !AttentionOwnedAbove(AttentionOwner.Coach))");
        Assert.True(dot < halo, "the dot must be painted before -- and regardless of -- its halo.");

        // ── AND THE RIG POSES THE BREAK THROUGH THE SAME GATE, so a capture photographs the game's own
        //    answer rather than a flourish the player would never have been shown.
        Assert.Contains("if (CaptureRig && !AttentionOwnedAbove(AttentionOwner.Coach)", fight, StringComparison.Ordinal);
        Assert.Contains("UiMotion.PoseFlash(NavBreakKey(who), part, NavChainSeconds);", fight, StringComparison.Ordinal);
    }

    /// <summary>
    /// ...AND SO DOES THE CHROME'S OWN POSE. <c>RH_SHOT_MOTION</c> re-poses the four chrome transients
    /// for a capture, and the banked "+N" is the one of them that waits in play -- so posing it under a
    /// reveal, a fall or the opening photographed a badge no player would ever be shown, which is the
    /// same rig lie <c>RH_SHOT_BREAK</c> was gated for.
    /// </summary>
    /// <remarks>
    /// The capsule's TINT is re-posed unconditionally on purpose: it is ungated in play, because the
    /// figure beside it walks to its new value under every owner, so the rim reports nothing the
    /// number is not already showing.
    /// </remarks>
    [Fact]
    public void test_the_rig_poses_no_banked_gain_the_player_would_not_be_shown()
    {
        var pose = MemberOf(Game1(), "private void PoseChromeMotion()");

        // ONE QUESTION, THE SAME ONE THE REAL ARM ASKS...
        Assert.Contains("var gainWouldShow = !AttentionOwnedAbove(AttentionOwner.Coach);", pose, StringComparison.Ordinal);
        // ...asked before the badge's two fields and after the tint's, so the tint still poses.
        var tint = IndexOf(pose, "_pillFlash[i] = UiMotion.Transition * t;");
        var gate = IndexOf(pose, "if (!gainWouldShow) continue;");
        var show = IndexOf(pose, "_pillGainShow[i] = Math.Max(PillGainFloor[i], (long)(_pillShown[i] / 20));");
        var clock = IndexOf(pose, "_pillGainT[i] = PillGainSeconds * t;");
        Assert.True(tint < gate, "the capsule's tint must be posed before the gain's gate.");
        Assert.True(gate < show && show < clock, "the gain's two fields must both sit under the gate.");

        // ...and the tint is NOT gated where it is armed for real, which is the decision this records.
        var chrome = MemberOf(Game1(), "private void TickChromeMotion(float dt)");
        Assert.Contains("_pillFlash[i] = UiMotion.Transition;", chrome, StringComparison.Ordinal);
        // EXACTLY ONE owner question in the whole tick, and it is the badge's: the tint is not gated,
        // and neither is the walking number the tint is about.
        Assert.Single(Regex.Matches(chrome, "AttentionOwnedAbove"));
        Assert.Contains("if (_pillGainAcc[i] >= bar && !AttentionOwnedAbove(AttentionOwner.Coach))", chrome, StringComparison.Ordinal);
    }

    /// <summary>
    /// A NOTE AND THE SLOT THAT PAINTS IT ANSWER AT ONE RUNG. The banner had the coach's rung while the
    /// slot had Feedback, so a caller asking the banner directly could be told a note was owed on a
    /// frame the slot would refuse to draw it -- which is how a gold mark reached the BUILD screen's
    /// first empty row with no sentence anywhere on screen (60d2e3aa).
    /// </summary>
    [Fact]
    public void test_the_slot_and_its_note_answer_at_the_same_rung()
    {
        var game = Game1();
        const string rung = "AttentionOwnedAbove(AttentionOwner.Feedback)";
        Assert.Contains("if (!OverlayActive || " + rung + ") return null;", MemberOf(game, "private SlotContent? SlotShowing()"), StringComparison.Ordinal);
        Assert.Contains("if (" + rung + ") return null;", MemberOf(game, "private ScreenBanner? ScreenBannerShowing()"), StringComparison.Ordinal);
        Assert.DoesNotContain("AttentionOwnedAbove(AttentionOwner.Coach)", MemberOf(game, "private ScreenBanner? ScreenBannerShowing()"), StringComparison.Ordinal);
        // ...and the slot is still the ONLY caller, so there is one surface and one question about it:
        // two mentions across every partial of the host -- the declaration, and SlotShowing's own call.
        Assert.Equal(2, Regex.Matches(Host(), @"ScreenBannerShowing\(\)").Count);
        Assert.Contains("if (ScreenBannerShowing() is { } note)", MemberOf(game, "private SlotContent? SlotShowing()"), StringComparison.Ordinal);
    }

    /// <summary>
    /// THE ENVELOPE ASKS THIS FRAME'S OWNER, AND TAKES ONLY A CLICK IT PAINTED. Its click sat above the
    /// one assignment until 2026-09-16, asking LAST frame's owner while <c>DrawDispatchButton</c> asked
    /// this frame's -- so on the frame an owner arrived the click accepted a control the paint refused,
    /// and on the frame one left it refused a control the paint drew. The fix is ORDER, not a second
    /// owner: the click reads the field directly below the site, through the same predicate the paint
    /// reads, and nothing writes the field between the two.
    /// </summary>
    /// <remarks>
    /// This covers every transition by construction rather than by a case per tier. The click and the
    /// paint can disagree only if (a) one of them asks a different question -- pinned away: the if-line
    /// asks <c>DispatchesOffered()</c> and hand-lists no flag, and the paint's first statement is the
    /// same call -- or (b) the field changes between the site and the click -- pinned away: no
    /// assignment sits in that span, and the host has exactly one. So free to Reveal, Report, Coach,
    /// Death, Modal or Opening, and any owner back to free, is seen by the click and the paint on the
    /// same frame, whichever tier it is and whichever way it goes.
    /// </remarks>
    [Fact]
    public void test_the_envelope_asks_this_frames_owner_and_only_takes_a_click_it_painted()
    {
        var game = Game1();
        var update = UpdateBody();

        // Below the one assignment, and nothing assigns the owner in between.
        var assign = IndexOf(update, ": AttentionOwner.None;");
        var envelope = IndexOf(update, "DispatchButton.Contains(ChromeMouse)");
        _out.WriteLine($"assign {assign} < envelope {envelope}");
        Assert.True(assign < envelope, "the envelope's click must read the owner below its one assignment.");
        Assert.Equal(0, Regex.Matches(update[assign..envelope], @"\b_attention\s*=(?!=)").Count);

        // The click asks the paint's question, and only that question: no rung of its own, no flag.
        var lineStart = update.LastIndexOf('\n', envelope) + 1;
        var line = update[lineStart..update.IndexOf('\n', envelope)];
        Assert.Contains("DispatchesOffered()", line, StringComparison.Ordinal);
        Assert.DoesNotContain("AttentionOwnedAbove(", line, StringComparison.Ordinal);
        foreach (var flag in new[] { "_showSettings", "_showHelp", "_showDispatches", "_showTitle", "_tourActive", "WelcomeUp",
                                     "_showTypeSpec", "_expedition.LogOpen", "_forge.RevealActive", "DeathTransitionUp",
                                     "_opening.Running", "SpecialisationOpen", "HostModalUp", "ProductionModalUp" })
            Assert.False(line.Contains(flag, StringComparison.Ordinal), $"the envelope's click hand-lists `{flag}`.");

        // The paint asks it first, before it draws anything.
        var draw = MemberOf(game, "private void DrawDispatchButton()");
        var gate = IndexOf(draw, "if (!DispatchesOffered()) return;");
        Assert.True(gate < IndexOf(draw, "_ui."), "DrawDispatchButton must ask the owner before its first draw.");
        Assert.Equal("if (!DispatchesOffered()) return;", draw[(draw.IndexOf('{') + 1)..].Trim().Split('\n')[0].Trim());

        // Three mentions across every partial of the host: the declaration, the paint's, the click's.
        Assert.Equal(3, Regex.Matches(Host(), @"DispatchesOffered\(\)").Count);

        // AND GEOMETRY KEEPS THE ORDER HONEST. Below the site the rail's click (HandleNavClick) and the
        // hint slot's band run before the envelope, and neither swallows a click outside its own rect —
        // so the envelope and the ? must share no pixel with a rail tile at any density. (The hint slot
        // sits under the pill row by construction; the rail is the one rect that spans the page's height.)
        var navHex = typeof(IdleXIdle.Game.Game1).GetMethod("NavHexRect", BindingFlags.NonPublic | BindingFlags.Static)!;
        var learnButton = typeof(IdleXIdle.Game.Game1).GetProperty("LearnButton", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            foreach (var percent in new[] { 100, 125, 150 })
            {
                UiMetrics.Apply(percent);
                var envelopeRect = IdleXIdle.Game.Game1.DispatchButton;
                var learnRect = (Rectangle)learnButton.GetValue(null)!;
                for (var i = 0; i < 11; i++)
                {
                    var tile = (Rectangle)navHex.Invoke(null, new object[] { i })!;
                    Assert.False(tile.Intersects(envelopeRect), $"the envelope shares pixels with rail tile {i} at {percent}%.");
                    Assert.False(tile.Intersects(learnRect), $"the ? shares pixels with rail tile {i} at {percent}%.");
                }
            }
        }
        finally { UiMetrics.Apply(100); }
    }
}
