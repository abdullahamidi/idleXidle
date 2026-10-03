using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Game.Rig;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// RH_SHOT_SEEK (the remaining-skill sweep's design.md section 9): the shutter aimed at an EVENT of the wave, never a guessed
/// second, and a wave without the event is refused. Each predicate on one synthetic wave; the same-ms LAST-OWNER rule
/// decides a cast's batch (a Strike belongs to the most recent Skill or Aura at its ms).
/// </summary>
public class capture_rig_shot_seek_test
{
    // the slots of the synthetic build: 0 HARD HANDS, 1 SPRAY, 2 PRESS, 3 JAWS
    private static int SlotOf(string id) => id switch
    {
        "sig_seeker_hard_hands" => 0, "volley_spray" => 1, "hammer_press" => 2, "snare_jaws" => 3, _ => -1,
    };

    private static BattleEvent E(BattleEventKind kind, int slot, int at, HitSource? hit = null) => new(kind, slot, 10, at, hit);

    /// <summary>One wave with every shape the grammar names.</summary>
    private static readonly BattleEvent[] Wave =
    {
        E(BattleEventKind.Beat, 0, 700),
        E(BattleEventKind.Strike, 0, 700, HitSource.Swing),
        E(BattleEventKind.EnemyStrike, 0, 1000),
        E(BattleEventKind.Skill, 3, 1000),                          // JAWS answers with no Strike: an unstruck reaction
        E(BattleEventKind.Beat, 0, 1400),
        E(BattleEventKind.Skill, 1, 1400),                          // SPRAY #1, struck
        E(BattleEventKind.Strike, 1, 1400, HitSource.Primary),
        E(BattleEventKind.Aura, 2, 2000),                           // PRESS's tick and its Strike...
        E(BattleEventKind.Strike, 0, 2000, HitSource.Primary),
        E(BattleEventKind.Skill, 1, 2000),                          // ...then SPRAY #2 on the same ms: its batch holds NO Strike
        E(BattleEventKind.Beat, 0, 2100),
        E(BattleEventKind.Strike, 2, 2300, HitSource.Bleed),
        E(BattleEventKind.EnemyDown, 2, 2300),
        E(BattleEventKind.Skill, 1, 2800),                          // SPRAY #3, struck, off the tick
        E(BattleEventKind.Strike, 0, 2800, HitSource.Primary),
        E(BattleEventKind.Strike, 1, 2800, HitSource.Carry),
        E(BattleEventKind.EnemyDown, 0, 2800),
        E(BattleEventKind.Aura, 2, 4000),
        E(BattleEventKind.Skill, 1, 4000),                          // SPRAY #4, on PRESS's second tick, struck
        E(BattleEventKind.Strike, 1, 4000, HitSource.Primary),
        E(BattleEventKind.EnemyStrike, 1, 4500),                    // a bite that does not fell
        E(BattleEventKind.EnemyStrike, 0, 5000),
        E(BattleEventKind.Undying, 0, 5000),                        // the bite that fells, caught by UNDYING
        E(BattleEventKind.EnemyStrike, 1, 6000),
        E(BattleEventKind.Down, 0, 6000),                           // the bite that fells
    };

    private static int? At(string spec) => ShotSeek.Parse(spec).Find(Wave, SlotOf);

    [Fact]
    public void test_skill_finds_the_nth_cast_of_the_skills_slot()
    {
        Assert.Equal(1400, At("skill:volley_spray"));
        Assert.Equal(2000, At("skill:volley_spray#2"));
        Assert.Equal(2800, At("skill:volley_spray#3"));
        Assert.Equal(1000, At("skill:snare_jaws"));
        Assert.Null(At("skill:volley_spray#5"));
    }

    [Fact]
    public void test_struck_and_unstruck_follow_the_last_owner_at_the_ms()
    {
        // SPRAY #2 shares 2000 with PRESS's Aura and its Strike, but that Strike is the Aura's: SPRAY's batch is empty
        Assert.Equal(2000, At("skill:volley_spray+unstruck"));
        Assert.Equal(1400, At("skill:volley_spray+struck"));
        Assert.Equal(2800, At("skill:volley_spray+struck#2"));
        Assert.Equal(1000, At("skill:snare_jaws+unstruck"));
        Assert.Null(At("skill:snare_jaws+struck"));
    }

    [Fact]
    public void test_ontick_finds_a_cast_sharing_its_ms_with_a_field_tick()
    {
        Assert.Equal(2000, At("skill:volley_spray+ontick"));
        Assert.Equal(4000, At("skill:volley_spray+ontick#2"));
        Assert.Equal(4000, At("skill:volley_spray+struck+ontick"));
        Assert.Null(At("skill:snare_jaws+ontick"));
    }

    [Fact]
    public void test_aura_down_hit_beat_and_event()
    {
        Assert.Equal(2000, At("aura:hammer_press"));
        Assert.Equal(4000, At("aura:hammer_press#2"));
        Assert.Equal(2300, At("down"));
        Assert.Equal(2800, At("down#2"));
        Assert.Equal(2300, At("hit:Bleed"));
        Assert.Equal(2800, At("hit:carry"));
        Assert.Equal(700, At("hit:Swing"));
        Assert.Null(At("hit:Reflect"));
        Assert.Equal(700, At("beat:1"));
        Assert.Equal(2100, At("beat:3"));
        Assert.Null(At("beat:4"));
        Assert.Equal(4500, At("event:EnemyStrike#2"));
        Assert.Equal(5000, At("event:Undying"));
    }

    [Fact]
    public void test_downing_is_the_bite_on_the_ms_of_a_down_or_an_undying()
    {
        Assert.Equal(5000, At("downing"));      // UNDYING caught it: the first fell-bite
        Assert.Equal(6000, At("downing#2"));    // the Down
        Assert.Null(At("downing#3"));
    }

    [Fact]
    public void test_not_found_is_null()
    {
        Assert.Null(At("skill:hammer_blow"));               // not woven: slotOf says -1
        Assert.Null(At("aura:volley_spray"));               // woven, but never ticks
        Assert.Null(At("event:ShieldBroken"));
        Assert.Null(ShotSeek.Parse("down").Find(Array.Empty<BattleEvent>(), SlotOf));
    }

    [Fact]
    public void test_a_malformed_seek_is_refused()
    {
        foreach (var bad in new[]
                 {
                     "", "skill:", "skill:volley_spray#0", "skill:volley_spray#x", "skill:volley_spray+loud",
                     "skill:volley_spray+struck+unstruck", "aura:hammer_press+ontick", "down:2", "hit:Fire", "hit:3",
                     "beat:0", "beat:x", "beat:2#2", "event:Nope", "lunge",
                 })
            Assert.Throws<InvalidOperationException>(() => ShotSeek.Parse(bad));
    }

    [Fact]
    public void test_a_rig_seek_that_finds_nothing_throws_and_the_fixture_picks_do_not()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "src", "IdleXIdle.Game", "HuntScreen.cs")).Replace("\r\n", "\n");
        var at = src.IndexOf("if (_devSeekPick is { } aim", StringComparison.Ordinal);
        Assert.True(at > 0);
        var block = src[at..src.IndexOf("// DEV: the fixture's seek (DevSeek / DevSeekBefore)", at, StringComparison.Ordinal)];
        Assert.Contains("else if (aim.MustFind is { } asked)\n                throw new InvalidOperationException(", block);
        // the fixtures' own picks pass no MustFind, so "no event: no seek" stands for them
        Assert.Contains("}, leadSeconds, null);", src);
        var rig = File.ReadAllText(Path.Combine(Root(), "src", "IdleXIdle.Game", "Game1.ShotBuildRig.cs"));
        Assert.Contains("_expedition.DevSeekBefore(events => aim.Find(events, _expedition.DevSlotOf), ShotLead(), $\"{ShotSeek.Variable}='{aim}'\");", rig);
    }

    private static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return dir!;
    }
}
