using System;
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
        Assert.Contains("if (!OverlayActive || AttentionOwnedAbove(AttentionOwner.Coach)) return null;", MemberOf(game, "private SlotContent? SlotShowing()"), StringComparison.Ordinal);
        Assert.Contains("if (AttentionOwnedAbove(AttentionOwner.Coach)) return null;", MemberOf(game, "private ScreenBanner? ScreenBannerShowing()"), StringComparison.Ordinal);
        // The ? opens a tour, so it is the coach's tier: live over a lit lesson, gone under the log and above.
        Assert.Contains("!AttentionOwnedAbove(AttentionOwner.Coach)", MemberOf(game, "private bool LearnOffered()"), StringComparison.Ordinal);
        Assert.Contains("var showing = OverlayActive && NoticeToastShowing;", MemberOf(game, "private void ReserveNoticeLane()"), StringComparison.Ordinal);
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
        Assert.Contains("ModalUp: _attention >= AttentionOwner.Modal && !(WelcomeUp && !ProductionModalUp),", game, StringComparison.Ordinal);
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
}
