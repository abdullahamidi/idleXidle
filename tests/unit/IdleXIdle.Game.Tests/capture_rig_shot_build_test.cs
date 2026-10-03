using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game.Rig;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// RH_SHOT_BUILD (the remaining-skill sweep's design.md section 9): a take's WHOLE build, refused LOUDLY. The Quiver on the
/// default fixture used to fight without BACKDRAW because <c>EnsureSignature</c> returned -1 in silence (three passives);
/// the rig now refuses the build the rule refuses, and checks the live run against the asked build once the fixture has
/// started it. The grammar and the refusals are pure (<see cref="ShotBuild"/>); the wiring into Game1 is pinned by
/// reading the source, as capture_rig_modes_test does, since Game1.Update needs a GraphicsDevice.
/// </summary>
public class capture_rig_shot_build_test
{
    private static SkillDef Def(string id) => SkillCatalogue.Find(id) ?? throw new InvalidOperationException(id);

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray())).Replace("\r\n", "\n");
    }

    [Fact]
    public void test_a_three_passive_build_is_refused_loudly()
    {
        // the DONE-WHEN case: BACKDRAW (a Reaction) + PRESS (a Field) + JAWS (a Reaction) is three passives
        var entries = ShotBuild.Parse("sig_quiver_backdraw,hammer_press@Body,snare_jaws@Shadow,volley_spray@Mind");
        var refusal = ShotBuild.Refusal(ShotBuild.Resolve(entries), "sig_quiver_backdraw", PlayerLoadout.MaxSkills);
        Assert.NotNull(refusal);
        Assert.Contains("3 passives", refusal);
        Assert.Contains("sig_quiver_backdraw, hammer_press, snare_jaws", refusal);
        Assert.Contains("PassivesFull", refusal);
    }

    [Fact]
    public void test_three_actives_are_refused_by_the_same_rule()
    {
        var defs = ShotBuild.Resolve(ShotBuild.Parse("sig_seeker_hard_hands,volley_spray,hammer_blow"));
        var refusal = ShotBuild.Refusal(defs, "sig_seeker_hard_hands", PlayerLoadout.MaxSkills);
        Assert.NotNull(refusal);
        Assert.Contains("3 Active", refusal);
        Assert.Contains("ActivesFull", refusal);
    }

    [Fact]
    public void test_a_build_without_the_signature_is_refused()
    {
        var defs = ShotBuild.Resolve(ShotBuild.Parse("volley_spray@Mind,hammer_press@Body,hammer_blow@Body"));
        var refusal = ShotBuild.Refusal(defs, "sig_quiver_backdraw", PlayerLoadout.MaxSkills);
        Assert.NotNull(refusal);
        Assert.Contains("leaves out the champion's signature 'sig_quiver_backdraw'", refusal);
    }

    [Fact]
    public void test_a_signature_out_of_slot_one_is_refused()
    {
        var defs = ShotBuild.Resolve(ShotBuild.Parse("volley_spray@Mind,sig_quiver_backdraw,hammer_press@Body"));
        Assert.Contains("holds slot one", ShotBuild.Refusal(defs, "sig_quiver_backdraw", PlayerLoadout.MaxSkills));
    }

    [Fact]
    public void test_a_legal_four_slot_build_parses_in_order_with_sources()
    {
        var entries = ShotBuild.Parse(" sig_quiver_backdraw , volley_spray@Mind,hammer_press@body, hammer_blow@Body ");
        Assert.Equal(new (string, Source?)[]
        {
            ("sig_quiver_backdraw", null), ("volley_spray", Source.Mind), ("hammer_press", Source.Body), ("hammer_blow", Source.Body),
        }, entries);
        Assert.Null(ShotBuild.Refusal(ShotBuild.Resolve(entries), "sig_quiver_backdraw", PlayerLoadout.MaxSkills));
    }

    [Fact]
    public void test_an_unknown_source_is_refused()
    {
        var bad = Assert.Throws<InvalidOperationException>(() => ShotBuild.Parse("sig_seeker_hard_hands,volley_spray@Fire"));
        Assert.Contains("'Fire' is not a Source", bad.Message);
        Assert.Throws<InvalidOperationException>(() => ShotBuild.Parse("sig_seeker_hard_hands,volley_spray@3"));
        Assert.Throws<InvalidOperationException>(() => ShotBuild.Parse("sig_seeker_hard_hands,volley_spray@Mind@Body"));
        Assert.Throws<InvalidOperationException>(() => ShotBuild.Parse("sig_seeker_hard_hands,,volley_spray"));
        Assert.Throws<InvalidOperationException>(() => ShotBuild.Parse(" "));
    }

    [Fact]
    public void test_an_unknown_id_a_duplicate_and_a_fifth_skill_are_refused()
    {
        Assert.Contains("'no_such_skill' is not a skill id",
            Assert.Throws<InvalidOperationException>(() => ShotBuild.Resolve(ShotBuild.Parse("sig_seeker_hard_hands,no_such_skill"))).Message);
        Assert.Contains("twice", ShotBuild.Refusal(
            ShotBuild.Resolve(ShotBuild.Parse("sig_seeker_hard_hands,volley_spray,volley_spray")), "sig_seeker_hard_hands", 4));
        Assert.Contains("at most 4", ShotBuild.Refusal(
            ShotBuild.Resolve(ShotBuild.Parse("sig_seeker_hard_hands,volley_spray,hammer_press,snare_jaws,hammer_blow")),
            "sig_seeker_hard_hands", 4));
    }

    [Fact]
    public void test_the_live_run_must_equal_the_asked_build()
    {
        var asked = ShotBuild.Parse("sig_quiver_backdraw,volley_spray@Mind,hammer_press@Body,hammer_blow@Body");
        var same = new[]
        {
            new EquippedSkill(Def("sig_quiver_backdraw"), Source.Body), new EquippedSkill(Def("volley_spray"), Source.Mind),
            new EquippedSkill(Def("hammer_press"), Source.Body), new EquippedSkill(Def("hammer_blow"), Source.Body),
        };
        Assert.Null(ShotBuild.Mismatch(asked, same));
        // the silent repair: the signature never reached the run
        Assert.Contains("carries 3 skills", ShotBuild.Mismatch(asked, same.Skip(1).ToArray()));
        // another order, another Source
        Assert.Contains("slot 2", ShotBuild.Mismatch(asked, new[] { same[0], same[2], same[1], same[3] }));
        Assert.Contains("fights @Body", ShotBuild.Mismatch(asked, new[] { same[0], same[1] with { Source = Source.Body }, same[2], same[3] }));
        // no Source named is the game's own default (a new slot's Body)
        Assert.Contains("not @Body", ShotBuild.Mismatch(asked, new[] { same[0] with { Source = Source.Shadow }, same[1], same[2], same[3] }));
    }

    [Fact]
    public void test_game1_refuses_build_beside_swap_and_verifies_the_live_run()
    {
        var rig = RepoFile("src", "IdleXIdle.Game", "Game1.ShotBuildRig.cs");
        var apply = rig[rig.IndexOf("ApplyShotBuild()", StringComparison.Ordinal)..];
        // RH_SHOT_BUILD and RH_SHOT_SWAP together throw before anything is written
        var swap = apply.IndexOf("ShotDial(\"RH_SHOT_SWAP\") is not null", StringComparison.Ordinal);
        Assert.True(swap > 0, "ApplyShotBuild no longer refuses RH_SHOT_SWAP beside it");
        Assert.True(swap < apply.IndexOf("ClearSkill", StringComparison.Ordinal));
        Assert.Contains("both set", apply[swap..(swap + 400)]);
        // every refusal and every declined write throws
        Assert.Contains("ShotBuild.Refusal(defs, _loadout.SignatureSkillId, PlayerLoadout.MaxSkills) is { } refusal)\n            throw", rig);
        Assert.Contains("if (!_loadout.SetSkill(i, defs[i].Id))\n                throw", rig);
        // the live run is compared with what was asked, AT THE SHUTTER (the run the host restarted, the one filmed), and a
        // difference throws there
        Assert.Contains("_expedition.DevCheckRunAtShutter(() => ShotRunMismatch(build, keystones, print: true));", rig);
        Assert.Contains("var live = _expedition.DevRunSkills();", rig);
        Assert.Contains("if (build is not null && ShotBuild.Mismatch(build, live) is { } mismatch) return mismatch;", rig);
        var hunt = RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs");
        var gate = hunt.IndexOf("if (_devRunCheck is { } check && _run is not null", StringComparison.Ordinal);
        Assert.True(gate > 0 && gate < hunt.IndexOf("if (_devSeekPick is { } aim && _run is not null", StringComparison.Ordinal),
            "the run is checked on the seek's frame, before the seek applies");
        Assert.Contains("if (check() is { } wrong) throw new InvalidOperationException(wrong);", hunt);

        var game1 = RepoFile("src", "IdleXIdle.Game", "Game1.cs");
        var applied = game1.IndexOf("var shotBuild = ApplyShotBuild();", StringComparison.Ordinal);
        var swapDial = game1.IndexOf("if (Environment.GetEnvironmentVariable(\"RH_SHOT_SWAP\")", StringComparison.Ordinal);
        var lastStart = game1.LastIndexOf("_expedition.DevStart(_hunter, fxHealth, fxBite);", StringComparison.Ordinal);
        var verified = game1.IndexOf("VerifyShotBuild(shotBuild, shotKeystones);", StringComparison.Ordinal);
        Assert.True(applied > 0 && applied < swapDial, "the build is applied before the swap dial is read");
        Assert.True(lastStart > 0 && verified > lastStart, "the build is verified AFTER the fixture started the run");
        // posed after RH_SHOT_HUNTER's signature, and refused on a fixture that reads no build
        Assert.True(game1.IndexOf("RefuseUnreadShotDials(sm);", StringComparison.Ordinal)
                    > game1.IndexOf("LoadoutRepair.EnsureSignature(_loadout, posed);", StringComparison.Ordinal));
    }
}
