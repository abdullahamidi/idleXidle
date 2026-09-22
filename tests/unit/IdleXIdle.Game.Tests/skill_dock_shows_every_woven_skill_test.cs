using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE DOCK IS THE COMPOSED BUILD — so a skill the composer drops is a skill HUNT cannot show.
/// </summary>
/// <remarks>
/// <para>
/// The 2026-09-22 report was "two Active skills can be configured in BUILD, but on HUNT one is missing
/// and never casts". HUNT was innocent: it draws one tile per skill the RUN is carrying, and the run
/// carries the composed build, so a skill the composer refused had already ceased to exist by the time
/// the dock was asked. Fixing the caps fixed the dock.
/// </para>
/// <para>
/// What this file guards is that the chain stays that short. The dock must keep reading the run rather
/// than the loadout — the two differ for a whole wave after a mid-descent edit, and a dock that showed
/// the loadout would promise a skill the fight is not running, which is the same class of lie in the
/// other direction.
/// </para>
/// </remarks>
public class SkillDockShowsEveryWovenSkillTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    [Fact]
    public void test_the_dock_draws_one_tile_per_skill_the_run_is_carrying()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs")).Replace("\r\n", "\n");

        // THE DOCK'S SOURCE IS THE RUN, not the loadout, and the comment beside it says why.
        Assert.Contains("_waveSkills = _run.Skills;", hunt, StringComparison.Ordinal);

        // ...and it is walked slot by slot, so every skill the run carries gets a tile. If the
        // composer drops one, there is simply one fewer tile — which is exactly what the player saw.
        Assert.Contains("for (var slot = 0; slot < _waveSkills.Count; slot++)", hunt, StringComparison.Ordinal);

        // ...and the run's skills ARE the composed build's, with nothing in between to filter them.
        var run = File.ReadAllText(RepoFile("src", "IdleXIdle.Core", "Builds", "SoloExpedition.cs")).Replace("\r\n", "\n");
        Assert.Contains("public IReadOnlyList<EquippedSkill> Skills => _build.Skills;", run, StringComparison.Ordinal);
    }

    [Fact]
    public void test_two_actives_at_capacity_two_give_the_dock_two_tiles()
    {
        // THE REPORTED CASE, ended where the dock begins. Before the fix this composed to ONE skill,
        // so HUNT drew one tile and one of the player's two chosen skills was simply not in the game.
        var loadout = new PlayerLoadout { SkillCapacity = 2 };
        var a = loadout.AddSkill();
        Assert.True(loadout.SetSkill(a, "hammer_blow"));
        var b = loadout.AddSkill();
        Assert.True(loadout.SetSkill(b, "volley_spray"));

        var build = loadout.ToBuild(TaughtEverything(), character: null);

        // The dock's count is this count — see the chain pinned above.
        Assert.Equal(2, build.Skills.Count);
        Assert.Equal(new[] { "hammer_blow", "volley_spray" }, build.Skills.Select(s => s.Def.Id));
        Assert.Empty(build.RefusedSkills);
    }

    [Fact]
    public void test_a_passive_signature_champion_reaches_the_dock_with_its_one_skill()
    {
        // Six of the ten champions have a signature that takes no beat. At capacity 1 the old passive
        // budget was ZERO, so their only skill was dropped and the dock had nothing at all to draw.
        var tower = CharacterRoster.Get("tower");
        var loadout = new PlayerLoadout { SkillCapacity = 1, SignatureSkillId = tower.SignatureSkillId };
        var slot = loadout.AddSkill();
        Assert.True(loadout.SetSkill(slot, tower.SignatureSkillId!));

        var build = loadout.ToBuild(TaughtEverything(), tower);

        Assert.Equal(new[] { tower.SignatureSkillId }, build.Skills.Select(s => s.Def.Id));
        Assert.Empty(build.RefusedSkills);
    }

    [Fact]
    public void test_the_build_screen_refuses_a_third_of_either_kind_with_a_reason()
    {
        // THE DECISION SURFACE. An illegal build must never become a saved loadout, and the button has
        // to SAY why rather than going grey — this screen already does that for the signature lock and
        // for LAW 13, and the kind caps join them rather than inventing a second idiom.
        var screen = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "LoadoutScreen.cs")).Replace("\r\n", "\n");

        // THE RULE IS ASKED OF THE MODEL, not re-derived here. That is the whole fix: one rule, and
        // the screen reads the same answer the composer will.
        Assert.Contains("switch (Loadout.RefusalFor(_slot, def))", screen, StringComparison.Ordinal);

        // ...and every arm the player can meet has a sentence.
        Assert.Contains("case SkillRefusal.ActivesFull:", screen, StringComparison.Ordinal);
        Assert.Contains("SKILLS THAT TAKE AN ACTION", screen, StringComparison.Ordinal);
        Assert.Contains("case SkillRefusal.PassivesFull:", screen, StringComparison.Ordinal);
        Assert.Contains("SKILLS THAT TAKE NO ACTION", screen, StringComparison.Ordinal);
        Assert.Contains("case SkillRefusal.TotalFull:", screen, StringComparison.Ordinal);
        Assert.Contains("EVERY SKILL SLOT YOU HAVE OPENED IS FULL", screen, StringComparison.Ordinal);

        // ...and the model refuses the write too, so an enabled button is never a promise it breaks.
        var loadout = new PlayerLoadout { SkillCapacity = 4 };
        var a = loadout.AddSkill(); Assert.True(loadout.SetSkill(a, "hammer_blow"));
        var b = loadout.AddSkill(); Assert.True(loadout.SetSkill(b, "volley_spray"));
        var c = loadout.AddSkill();
        Assert.Equal(SkillRefusal.ActivesFull, loadout.RefusalFor(c, SkillCatalogue.Find("snare_repay")!));
        Assert.False(loadout.SetSkill(c, "snare_repay"));
    }

    /// <summary>A mastery tree that has learned every road — the Game suite's local Taught.Everything.</summary>
    private static MasteryTree TaughtEverything()
    {
        var tree = new MasteryTree();
        tree.SetEarned(9999);
        tree.RestoreTaken(MasteryCatalog.Nodes.Where(n => n.Kind == MasteryKind.SkillRoad).Select(n => n.Id),
                          repair: false);
        return tree;
    }
}
