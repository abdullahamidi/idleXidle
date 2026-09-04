using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BUILD's skill library: the SIGNATURE has a row of its own above the twelve, and a shared skill without
/// its mastery node reads LOCKED with the level it keeps.
/// </summary>
/// <remarks>
/// <para>
/// BRIEF sec.12 and sec.20. The screen is constructed with a null <see cref="UiKit"/>, the way
/// <see cref="LoadoutFeedbackTests"/> does: Update lays out, hit-tests and edits the model and never draws.
/// The rectangles and the copy are private, and a test that copied their arithmetic would pass while the
/// screen drew the row somewhere else — so both are read back off the real members by reflection.
/// </para>
/// <para>
/// Every geometric claim is made at all three density profiles. This library was reflowed once already at
/// 100 / 125 / 150 and the signature row is one more row inside that reflow, not an exception to it.
/// </para>
/// </remarks>
public class LoadoutSignatureLibraryTests
{
    private static readonly Type T = typeof(LoadoutScreen);

    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    private static LoadoutScreen Screen(Character? who, params string[] roads)
    {
        UiMotion.Clear();
        UiMotion.Reduced = false;
        var mastery = new MasteryTree();
        mastery.SetEarned(9999);
        mastery.RestoreTaken(roads);
        var loadout = new PlayerLoadout { SkillCapacity = 4 };
        return new LoadoutScreen(null!)
        {
            Loadout = loadout,
            Mastery = mastery,
            SkillLevels = new SkillProgress(),
            Character = who,
        };
    }

    private static Rectangle Rect(LoadoutScreen s, string name)
        => (Rectangle)T.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!;

    private static int Int(LoadoutScreen s, string name)
        => (int)T.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!;

    private static Rectangle Tile(LoadoutScreen s, int style, int which)
        => (Rectangle)T.GetMethod("LibTile", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(s, new object[] { style, which })!;

    private static (string Label, bool Enabled, string Refusal) Primary(LoadoutScreen s)
    {
        var v = T.GetMethod("Primary", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(s, null)!;
        var t = v.GetType();
        return ((string)t.GetField("Item1")!.GetValue(v)!,
                (bool)t.GetField("Item2")!.GetValue(v)!,
                (string)t.GetField("Item3")!.GetValue(v)!);
    }

    private static string LockLine(LoadoutScreen s, SkillDef def, bool full)
        => (string)T.GetMethod("LockLine", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(s, new object[] { def, full })!;

    private static string LockTip(LoadoutScreen s, SkillDef def)
        => (string)T.GetMethod("LockTip", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(s, new object[] { def })!;

    // ── sec.12: the signature is offered, and it is unmistakable ─────────────────────────────────

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_signature_has_its_own_row_above_the_twelve(int percent)
    {
        UiMetrics.Apply(percent);
        var s = Screen(CharacterRoster.Get("seeker"));

        var sig = Rect(s, "SigTile");
        var first = Tile(s, 0, 0);

        Assert.True(sig.Width > 0 && sig.Height > 0, $"the signature tile is empty at {percent}%");
        Assert.True(sig.Bottom <= first.Y, $"the signature row overlaps the first style row at {percent}%: {sig} vs {first}");
        // Across BOTH tile columns, so it cannot read as half of a style's pair.
        Assert.True(sig.Right >= Tile(s, 0, 1).Right, $"the signature tile is narrower than the two it sits over at {percent}%");
        UiMetrics.Apply(100);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_twelve_move_down_by_exactly_the_signature_block(int percent)
    {
        UiMetrics.Apply(percent);
        var with = Screen(CharacterRoster.Get("seeker"));
        var without = Screen(null);

        var block = Int(with, "SigBlockH");
        Assert.True(block > 0, $"a champion with a signature reserves no room for it at {percent}%");
        Assert.Equal(0, Int(without, "SigBlockH"));
        Assert.Equal(Tile(without, 0, 0).Y + block, Tile(with, 0, 0).Y);
        Assert.Equal(Int(without, "TreeTop") + block, Int(with, "TreeTop"));
        UiMetrics.Apply(100);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_no_row_of_the_library_overlaps_another_or_the_tree_under_it(int percent)
    {
        UiMetrics.Apply(percent);
        var s = Screen(CharacterRoster.Get("seeker"));

        var previous = Rect(s, "SigTile");
        for (var st = 0; st < 6; st++)
        {
            var left = Tile(s, st, 0);
            var right = Tile(s, st, 1);
            Assert.True(previous.Bottom <= left.Y, $"row {st} overlaps the row above it at {percent}%");
            Assert.True(left.Right <= right.X, $"row {st}'s two tiles overlap at {percent}%");
            previous = left;
        }
        Assert.True(previous.Bottom <= Int(s, "TreeTop"), $"the last row runs into the skill tree at {percent}%");
        UiMetrics.Apply(100);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_signature_row_is_inside_the_skills_column(int percent)
    {
        UiMetrics.Apply(percent);
        var s = Screen(CharacterRoster.Get("seeker"));

        var region = (Rectangle)T.GetProperty("SkillsRegion", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var sig = Rect(s, "SigTile");

        Assert.True(sig.X >= region.X && sig.Right <= region.Right, $"the signature tile leaves its column at {percent}%: {sig} vs {region}");
        Assert.True(sig.Y >= region.Y, $"the signature tile starts above its column at {percent}%");
        UiMetrics.Apply(100);
    }

    [Fact]
    public void test_clicking_the_signature_row_selects_the_signature()
    {
        UiMetrics.Apply(100);
        var who = CharacterRoster.Get("seeker");
        var s = Screen(who);
        s.Loadout.AddSkill();

        s.Update(Rect(s, "SigTile").Center, clicked: true, held: false, wheel: 0);

        // It is offered, not merely drawn: the primary button commits it to the picked slot.
        var (label, enabled, _) = Primary(s);
        Assert.True(enabled, $"the signature could not be equipped from its own row — the button said \"{label}\"");
        Assert.Contains("EQUIP", label);
    }

    [Fact]
    public void test_no_other_champions_signature_has_a_cell_in_the_grid()
    {
        // LAW 1, structurally: the twelve cells are built from SkillCatalogue.Shared, which is defined
        // as the skills that name no owner, so a foreign signature has nowhere to appear.
        for (var st = 0; st < 6; st++)
        {
            Assert.Null(SkillCatalogue.ActiveOf((Style)st).OwnerCharacterId);
            Assert.Null(SkillCatalogue.PassiveOf((Style)st).OwnerCharacterId);
        }
        Assert.Equal(12, SkillCatalogue.Shared.Count());
    }

    // ── sec.20: LOCKED, never unlearned ──────────────────────────────────────────────────────────

    [Fact]
    public void test_a_skill_with_waves_behind_it_and_no_node_reads_locked_with_its_level()
    {
        UiMetrics.Apply(100);
        var s = Screen(CharacterRoster.Get("seeker"));
        for (var i = 0; i < 400; i++) s.SkillLevels.RecordWave("hammer_blow");
        var level = s.SkillLevels.LevelOf("hammer_blow");
        Assert.True(level >= 1, "the fixture must earn a level for this test to mean anything");

        var blow = SkillCatalogue.Find("hammer_blow")!;
        foreach (var line in new[] { LockLine(s, blow, full: true), LockLine(s, blow, full: false), LockTip(s, blow) })
        {
            Assert.Contains("LOCKED", line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(level.ToString(), line);
            Assert.DoesNotContain("UNLEARN", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NOT LEARNED", line, StringComparison.OrdinalIgnoreCase);
        }
        // Even the narrow form keeps the level: the kind is what goes, never the experience.
        Assert.Contains($"LEVEL {level}", LockLine(s, blow, full: false));
    }

    [Fact]
    public void test_the_locked_button_says_locked_names_the_road_and_keeps_the_level()
    {
        UiMetrics.Apply(100);
        var s = Screen(CharacterRoster.Get("seeker"));
        for (var i = 0; i < 400; i++) s.SkillLevels.RecordWave("hammer_blow");
        var level = s.SkillLevels.LevelOf("hammer_blow");
        s.Loadout.AddSkill();
        s.Update(Tile(s, (int)Style.Hammer, 0).Center, clicked: true, held: false, wheel: 0);

        var (label, enabled, refusal) = Primary(s);

        Assert.False(enabled);
        Assert.Contains("LOCKED", label);
        Assert.Contains($"LEVEL {level}", label);
        Assert.DoesNotContain("LEARN ON", label);
        Assert.Contains("HAMMER", refusal);          // what would unlock it, named
        Assert.Contains("MASTERY TREE", refusal);
        Assert.Contains(level.ToString(), refusal);  // and what is kept while it is locked
    }

    [Fact]
    public void test_a_skill_with_no_waves_behind_it_says_locked_without_inventing_a_level()
    {
        UiMetrics.Apply(100);
        var s = Screen(CharacterRoster.Get("seeker"));
        var blow = SkillCatalogue.Find("hammer_blow")!;

        Assert.Equal("ACTIVE · LOCKED", LockLine(s, blow, full: true));
        Assert.Equal("LOCKED", LockLine(s, blow, full: false));
        Assert.DoesNotContain("LEVEL", LockTip(s, blow), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_a_signature_is_never_locked_for_its_owner()
    {
        UiMetrics.Apply(100);
        var who = CharacterRoster.Get("seeker");
        var s = Screen(who);   // no roads taken at all
        s.Loadout.AddSkill();

        s.Update(Rect(s, "SigTile").Center, clicked: true, held: false, wheel: 0);

        var (label, enabled, _) = Primary(s);
        Assert.True(enabled, $"the signature read \"{label}\" with an empty mastery tree — it is exempt from mastery (sec.18)");
    }

    [Fact]
    public void test_an_unlocked_skill_says_its_kind_and_says_nothing_about_locks()
    {
        UiMetrics.Apply(100);
        var s = Screen(CharacterRoster.Get("seeker"), "road_hammer");
        s.Loadout.AddSkill();
        s.Update(Tile(s, (int)Style.Hammer, 0).Center, clicked: true, held: false, wheel: 0);

        var (label, enabled, _) = Primary(s);
        Assert.True(enabled);
        Assert.DoesNotContain("LOCKED", label);
    }
}
