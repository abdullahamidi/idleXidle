using System.Reflection;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Prestige;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// BUILD's change feedback (UI polish brief §30–§38, §39–§44): an equip pulses the slot it landed in, a
/// chosen variation lights its branch, a reinforcement taken pulses its chip, a refused equip asks for the
/// dull cue and lights nothing, and RESPEC stays calm.
/// </summary>
/// <remarks>
/// <para>
/// These drive the screen's real Update path — the one the host calls — and read the phase back out of
/// <see cref="UiMotion"/>, because that is what "a pulse plays once, on the right thing" MEANS. The pulse
/// itself is drawn, and a drawn thing is the capture rig's job (build/shots/p2_loadout_fx_*.png, posed with
/// RH_SHOT_BUILD_FX); what a still frame cannot show is WHEN a pulse is armed and when it is not, and that
/// is exactly what these assert: at t = 0, halfway, and at the end.
/// </para>
/// <para>
/// The screen is constructed with a null <see cref="UiKit"/> on purpose. Update lays out, hit-tests and
/// edits the model; it never draws, so it never touches a GraphicsDevice — the same rule the rest of this
/// project holds to.
/// </para>
/// </remarks>
public class LoadoutFeedbackTests
{
    private const int Mid = 90;         // ms: halfway through a 180 ms Transition
    private static float Seconds(int ms) => ms / 1000f;

    // ── the screen under test ───────────────────────────────────────────────────────────────────────
    private static LoadoutScreen Screen()
    {
        UiMetrics.Apply(100);
        UiMotion.Clear();
        UiMotion.Reduced = false;
        var mastery = new MasteryTree();
        mastery.SetEarned(9999);
        mastery.RestoreTaken(MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.SkillRoad).Select(n => n.Id).ToList());
        // THE CHAMPION HAS TO BE IN THE CHAIR. The starter loadout carries that champion's own
        // SIGNATURE now, and a signature answers only to its owner — so a fixture that seats nobody
        // holds a skill the screen correctly refuses to know, and the tree it opens is null.
        var champion = CharacterRoster.Get(CharacterRoster.StarterId);
        var loadout = PlayerLoadout.Starter(champion);
        loadout.SkillCapacity = 4;
        return new LoadoutScreen(null!)
        {
            Loadout = loadout,
            Mastery = mastery,
            Tree = new MemoryDustTree(),
            SkillLevels = new SkillProgress(),
            Character = champion,
        };
    }

    private static void Click(LoadoutScreen s, Rectangle r) => s.Update(r.Center, clicked: true, held: false, wheel: 0);
    private static void Idle(LoadoutScreen s) => s.Update(new Point(-1, -1), clicked: false, held: false, wheel: 0);

    // ── reflection: the keys and rectangles are private, and a test that copied their arithmetic would
    // pass while the screen pulsed the wrong cell. ───────────────────────────────────────────────────
    private static readonly Type T = typeof(LoadoutScreen);
    private static int Key(string name, params object[] args)
        => (int)T.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;
    private static int FadeKey()
        => (int)T.GetField("InsFadeKey", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    private static Rectangle StaticRect(string name)
        => (Rectangle)T.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    private static Rectangle Rect(LoadoutScreen s, string name, params object[] args)
        => (Rectangle)T.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(s, args)!;

    private static Rectangle Tile(LoadoutScreen s, Style style, int which) => Rect(s, "LibTile", (int)style, which);
    private static Rectangle Primary() => StaticRect("PrimaryBtn");

    /// <summary>Level a skill far enough that it has spare levels to spend on its own tree.</summary>
    private static void Level(LoadoutScreen s, string skillId, int level)
    {
        for (var i = 0; i < SkillProgress.UsesForLevel(level); i++) s.SkillLevels.RecordWave(skillId);
    }

    // ── THE EQUIP PULSE ─────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void test_equipping_a_skill_pulses_that_slot_once_and_settles()
    {
        var s = Screen();
        var call = SkillCatalogue.ActiveOf(Style.Sign);
        Click(s, Tile(s, Style.Sign, 0));      // select it in the library
        Click(s, Primary());                   // EQUIP TO SLOT 1

        Assert.Equal(call.Id, s.Loadout.Skills[0].SkillId);
        var slot = Key("SlotKey", 0);
        Assert.Equal(1f, UiMotion.Pulse(slot), 2);              // t = 0: just fired

        UiMotion.Tick(Seconds(Mid));
        Assert.Equal(0.5f, UiMotion.Pulse(slot), 1);            // halfway

        UiMotion.Tick(Seconds(Mid));
        Assert.Equal(0f, UiMotion.Pulse(slot));                 // done, and forgotten

        // AND IT IS NOT RE-ARMED BY STANDING STILL. A pulse that Draw or Update re-armed every frame would
        // be a flashing cell, which is the one thing §30 forbids.
        for (var i = 0; i < 10; i++) { Idle(s); UiMotion.Tick(Seconds(16)); }
        Assert.Equal(0f, UiMotion.Pulse(slot));
    }

    [Fact]
    public void test_the_pulse_lands_on_the_slot_that_changed_and_not_its_neighbour()
    {
        var s = Screen();
        s.Loadout.AddSkill();                                   // slot 2, empty
        Click(s, Tile(s, Style.Sign, 0));
        Click(s, Primary());                                    // still slot 1 — nothing selected slot 2

        Assert.True(UiMotion.Pulse(Key("SlotKey", 0)) > 0f);
        Assert.Equal(0f, UiMotion.Pulse(Key("SlotKey", 1)));
    }

    // ── THE REFUSAL ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void test_a_refused_equip_asks_for_the_error_cue_and_lights_nothing()
    {
        var s = Screen();
        var call = SkillCatalogue.ActiveOf(Style.Sign);
        var second = s.Loadout.AddSkill();
        Assert.True(s.Loadout.SetSkill(second, call.Id));        // it is worn in SLOT 2 already
        Assert.Null(s.ConsumeCue());

        Click(s, Tile(s, Style.Sign, 0));                        // select it while SLOT 1 is the target
        Click(s, Primary());                                     // LAW 13 refuses it

        Assert.Equal("sfx_error", s.ConsumeCue());
        Assert.Null(s.ConsumeCue());                             // read once, then cleared
        Assert.Equal(0f, UiMotion.Pulse(Key("SlotKey", 0)));     // nothing changed, so nothing lit
        Assert.Equal(0f, UiMotion.Pulse(Key("SlotKey", 1)));
        Assert.NotEqual(call.Id, s.Loadout.Skills[0].SkillId);   // and the model refused it too
    }

    // ── THE TREE ────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void test_choosing_a_variation_lights_that_branch_and_taking_a_reinforcement_pulses_that_chip()
    {
        var s = Screen();
        var def = SkillCatalogue.Find(s.Loadout.Skills[0].SkillId)!;
        Level(s, def.Id, 3);                                     // levels to spend on its own tree

        Click(s, Rect(s, "VarCard", 0));                         // the left fork
        Click(s, Primary());                                     // CHOOSE it

        Assert.Equal(def.Variations[0].Name, s.SkillLevels.VariationOf(def)!.Name);
        var branch = Key("BranchKey", def.Id, 0);
        Assert.Equal(1f, UiMotion.Pulse(branch), 2);
        UiMotion.Tick(Seconds(Mid));
        Assert.Equal(0.5f, UiMotion.Pulse(branch), 1);
        UiMotion.Tick(Seconds(Mid));
        Assert.Equal(0f, UiMotion.Pulse(branch));
        Assert.Equal(0f, UiMotion.Pulse(Key("BranchKey", def.Id, 1)));   // the fork NOT taken stays dark

        Click(s, Rect(s, "ReinfChip", 0, 0));                    // its first reinforcement
        Click(s, Primary());                                     // TAKE it

        var r = def.Variations[0].Reinforcements[0];
        Assert.True(s.SkillLevels.HasReinforcement(def.Id, r.Name));
        var chip = Key("ReinfKey", def.Id, 0, 0);
        Assert.Equal(1f, UiMotion.Pulse(chip), 2);
        Assert.Equal(0f, UiMotion.Pulse(Key("ReinfKey", def.Id, 0, 1)));
        UiMotion.Tick(Seconds(Mid));
        Assert.Equal(0.5f, UiMotion.Pulse(chip), 1);
        UiMotion.Tick(Seconds(Mid));
        Assert.Equal(0f, UiMotion.Pulse(chip));
    }

    // ── RESPEC STAYS CALM (§44) ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void test_respec_changes_the_build_and_lights_nothing()
    {
        var s = Screen();
        var def = SkillCatalogue.Find(s.Loadout.Skills[0].SkillId)!;
        Level(s, def.Id, 3);
        Click(s, Rect(s, "VarCard", 0));
        Click(s, Primary());
        UiMotion.Clear();                                        // the choice's own light has played out

        // RESPEC is only offered once the body has decided it applies, which is a Draw's job; the frame
        // after that Draw is what this poses.
        T.GetField("_respecShown", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(s, true);
        Click(s, StaticRect("RespecText"));

        Assert.Null(s.SkillLevels.VariationOf(def));             // it did happen
        Assert.Equal(0f, UiMotion.Pulse(Key("SlotKey", 0)));     // and it said so in words, not in light
        Assert.Equal(0f, UiMotion.Pulse(Key("BranchKey", def.Id, 0)));
        Assert.Equal(0f, UiMotion.Pulse(Key("ReinfKey", def.Id, 0, 0)));
        Assert.Null(s.ConsumeCue());
    }

    // ── THE INSPECTOR'S CONTENT FADE (§35) ──────────────────────────────────────────────────────────
    [Fact]
    public void test_the_inspector_fades_when_the_selection_changes_and_not_while_it_stands_still()
    {
        var s = Screen();
        Idle(s);                                                 // the first frame settles the signature
        UiMotion.Clear();
        var fade = FadeKey();

        Click(s, Tile(s, Style.Sign, 0));                        // a new thing to be about
        Assert.Equal(1f, UiMotion.Pulse(fade), 2);
        UiMotion.Tick(Seconds(50));
        Assert.Equal(0.5f, UiMotion.Pulse(fade), 1);
        UiMotion.Tick(Seconds(50));
        Assert.Equal(0f, UiMotion.Pulse(fade));

        Click(s, Tile(s, Style.Sign, 0));                        // the SAME thing: no second fade
        Assert.Equal(0f, UiMotion.Pulse(fade));
        for (var i = 0; i < 10; i++) { Idle(s); UiMotion.Tick(Seconds(16)); }
        Assert.Equal(0f, UiMotion.Pulse(fade));

        Click(s, Tile(s, Style.Field, 1));                       // a different thing: it fades again
        Assert.Equal(1f, UiMotion.Pulse(fade), 2);
    }

    // ── THE KEYS ────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void test_every_feedback_key_names_exactly_one_thing()
    {
        var a = SkillCatalogue.ActiveOf(Style.Sign).Id;
        var b = SkillCatalogue.ActiveOf(Style.Field).Id;
        var keys = new[]
        {
            Key("SlotKey", 0), Key("SlotKey", 1),
            Key("BindKey", 0), Key("BindKey", 1),
            Key("BranchKey", a, 0), Key("BranchKey", a, 1), Key("BranchKey", b, 0),
            Key("ReinfKey", a, 0, 0), Key("ReinfKey", a, 0, 1), Key("ReinfKey", a, 1, 0), Key("ReinfKey", b, 0, 0),
            FadeKey(),
        };
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    // ── REDUCED MOTION ENDS WHERE FULL MOTION ENDS (§32, §102–§107) ─────────────────────────────────
    [Fact]
    public void test_reduced_motion_reaches_the_same_end_state()
    {
        var s = Screen();
        var hover = UiMotion.KeyOf(Tile(s, Style.Sign, 0));

        UiMotion.Reduced = false;
        UiMotion.Tick(Seconds(16));
        var eased = UiMotion.Ease(hover, 1f, UiMotion.Fast);
        Assert.True(eased < 1f);                                 // full motion takes its 100 ms

        UiMotion.Clear();
        UiMotion.Reduced = true;
        Assert.Equal(1f, UiMotion.Ease(hover, 1f, UiMotion.Fast));   // reduced arrives at the same value at once

        Click(s, Tile(s, Style.Sign, 0));
        Click(s, Primary());
        UiMotion.Tick(Seconds(100)); UiMotion.Tick(Seconds(100));   // Tick clamps one step to 100 ms
        Assert.Equal(0f, UiMotion.Pulse(Key("SlotKey", 0)));     // and a one-shot still ends at nothing
        UiMotion.Reduced = false;
    }
}
