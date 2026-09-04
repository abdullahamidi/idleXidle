using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE RESPEC WARNS FIRST, THEN REPAIRS (BRIEF sec.19).
/// </summary>
/// <remarks>
/// <para>
/// Access follows the current allocation now, so TAKE EVERY POINT BACK takes back every shared skill's
/// road with the points. Until this change the composer simply stopped carrying those slots: the player
/// pressed a button that said it returned points, and silently lost half their build with no line of text
/// anywhere on the screen. The two presses this screen already required are now the warning and the
/// commit — and the commit empties exactly the slots it made unusable, and nothing else.
/// </para>
/// <para>
/// Driven through the screen's real Update, on the screen's real button rectangle, with a null
/// <see cref="UiKit"/>: Update lays out, hit-tests and edits the model and never draws.
/// </para>
/// </remarks>
public class MasteryRespecRepairTests
{
    private static readonly Type T = typeof(MasteryScreen);
    private static readonly Character Seeker = CharacterRoster.Get("seeker");

    private static Rectangle ResetBtn
        => (Rectangle)T.GetProperty("ResetBtn", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static bool Armed(MasteryScreen s)
        => (bool)T.GetField("_resetArmed", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!;

    private static string Said(MasteryScreen s)
        => (string)T.GetField("_msg", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!;

    private static IReadOnlyList<SkillDef> WouldUnequip(MasteryScreen s)
        => (IReadOnlyList<SkillDef>)T.GetMethod("RespecWouldUnequip", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(s, null)!;

    private static void Press(MasteryScreen s)
        => s.Update(default(KeyboardState), default(KeyboardState), ResetBtn.Center, clicked: true,
                    held: false, wheel: 0);

    /// <summary>A hunter two roads deep, wearing both their skills and their own signature.</summary>
    private static (MasteryScreen Screen, PlayerLoadout Loadout, SkillProgress Progress) Posed()
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var mastery = new MasteryTree();
        mastery.SetEarned(9999);
        mastery.RestoreTaken(new[] { "road_hammer", "road_snare" });

        var loadout = new PlayerLoadout { SkillCapacity = 4 };
        foreach (var id in new[] { "hammer_blow", "snare_jaws", Seeker.SignatureSkillId! })
        {
            var slot = loadout.AddSkill();
            loadout.SetSource(slot, Source.Body);
            loadout.SetSkill(slot, id);
        }

        var progress = new SkillProgress();
        for (var i = 0; i < 400; i++) progress.RecordWave("hammer_blow");

        var screen = new MasteryScreen(null!) { Loadout = loadout, Mastery = mastery, Character = Seeker };
        return (screen, loadout, progress);
    }

    // ── Before it commits ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_first_press_names_the_skills_the_respec_would_unequip()
    {
        var (s, _, _) = Posed();

        Press(s);

        Assert.True(Armed(s), "the first press must arm, not commit");
        Assert.Equal(new[] { "hammer_blow", "snare_jaws" }, WouldUnequip(s).Select(d => d.Id));
        Assert.Contains("BLOW", Said(s), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JAWS", Said(s), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNEQUIP", Said(s), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_the_warning_never_names_the_champions_own_signature()
    {
        // It is exempt from mastery (sec.18), so a respec cannot cost it and must not claim to.
        var (s, _, _) = Posed();
        var sig = SkillCatalogue.Find(Seeker.SignatureSkillId!)!;

        Press(s);

        Assert.DoesNotContain(WouldUnequip(s), d => d.Id == sig.Id);
        Assert.DoesNotContain(sig.Name.ToUpperInvariant(), Said(s));
    }

    [Fact]
    public void test_nothing_is_unequipped_by_the_warning_itself()
    {
        var (s, loadout, _) = Posed();

        Press(s);

        Assert.Equal(3, loadout.Skills.Count(x => x.SkillId is not null));
        Assert.True(s.Mastery.Spent > 0, "the first press must not spend or return anything");
    }

    // ── After it commits ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_second_press_returns_the_points_and_clears_exactly_those_slots()
    {
        var (s, loadout, _) = Posed();

        Press(s);
        Press(s);

        Assert.False(Armed(s));
        Assert.Equal(0, s.Mastery.Spent);
        Assert.Null(loadout.Skills[0].SkillId);                       // hammer_blow: its road is gone
        Assert.Null(loadout.Skills[1].SkillId);                       // snare_jaws: likewise
        Assert.Equal(Seeker.SignatureSkillId, loadout.Skills[2].SkillId);
        Assert.Equal(3, loadout.Skills.Count);                        // and no slot was taken away
    }

    [Fact]
    public void test_the_respec_says_what_left_the_slots_and_that_the_levels_are_kept()
    {
        var (s, _, _) = Posed();

        Press(s);
        Press(s);

        Assert.Contains("RETURNED", Said(s), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BLOW", Said(s), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KEPT", Said(s), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_a_respec_is_never_blocked_by_an_equipped_skill()
    {
        // sec.19 says so outright, and it is the difference between a warning and a wall.
        var (s, _, _) = Posed();

        Press(s);
        Press(s);

        Assert.Equal(0, s.Mastery.Spent);
        Assert.True(s.Mastery.Available > 0, "every point must come back");
    }

    [Fact]
    public void test_a_respec_never_touches_what_the_skill_has_earned()
    {
        var (s, _, progress) = Posed();
        var level = progress.LevelOf("hammer_blow");
        Assert.True(level >= 1, "the fixture must earn a level for this test to mean anything");

        Press(s);
        Press(s);

        Assert.Equal(level, progress.LevelOf("hammer_blow"));   // LAW 4
    }

    [Fact]
    public void test_a_respec_with_nothing_to_lose_says_only_that_the_points_came_back()
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        var mastery = new MasteryTree();
        mastery.SetEarned(9999);
        mastery.RestoreTaken(new[] { "road_hammer" });
        var s = new MasteryScreen(null!) { Loadout = new PlayerLoadout { SkillCapacity = 4 }, Mastery = mastery, Character = Seeker };

        Press(s);
        Assert.DoesNotContain("UNEQUIP", Said(s), StringComparison.OrdinalIgnoreCase);
        Press(s);
        Assert.DoesNotContain("LEFT YOUR SLOTS", Said(s), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, mastery.Spent);
    }

    // ── The capture dial, which is the only way this state can be looked at ───────────────────────

    [Fact]
    public void test_the_capture_dial_poses_the_armed_warning()
    {
        var (s, _, _) = Posed();

        s.DevArmRespec();

        Assert.True(Armed(s));
        Assert.Contains("UNEQUIP", Said(s), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_a_click_on_the_warning_disarms_instead_of_spending_a_point()
    {
        // The list is a card, not glass. Reading it must never buy the node underneath it.
        var (s, _, _) = Posed();
        var band = (Rectangle)T.GetProperty("RespecWarning", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Press(s);
        var spent = s.Mastery.Spent;

        s.Update(default(KeyboardState), default(KeyboardState), band.Center, clicked: true,
                 held: false, wheel: 0);

        Assert.False(Armed(s));
        Assert.Equal(spent, s.Mastery.Spent);
    }
}
