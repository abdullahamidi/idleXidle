using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE MELEE ACTION CONTRACT (ADR-011, HARD HANDS, 2026-09-25): a melee blow lands ON the fight's beat with the fist
/// on the target, the body is CARRIED there by presentation root motion (back, a fast accelerating commit, a small
/// overshoot, a controlled return) and never teleports, and only the late recovery gives way to the next action.
/// </summary>
/// <remarks>
/// The failures these stop were measured on the old HARD HANDS: it slashed the air ~350 px from the creature, the
/// strike effect played on the creature on its own, and it ended in a deep crouch that the next action popped out of.
/// </remarks>
public class MeleeActionTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static ActionClipTiming HardHands()
        => ActionClipTiming.Parse(File.ReadAllText(RepoFile("assets", "art", "Animations", "Roster", "seeker_strike",
                                                            "char_seeker_strike_strip8_512.clip.json")));

    private const float Caster = 412f;

    private sealed class Stage : IActionStage
    {
        public Rectangle Box = new(420, 451, 400, 400);

        public bool TryActorFrame(string clipKey, int frame, out SpriteFrame drawn, out int frameSize)
        {
            drawn = new SpriteFrame(null!, new Rectangle(frame * 512, 0, 512, 512), Box, SpriteEffects.None);
            frameSize = 512;
            return true;
        }

        public bool TryTargetBody(int slot, out Rectangle body)
        {
            body = new Rectangle(1200 + slot * 90, 640, 120, 160);
            return true;
        }

        public Texture2D? Texture(string key) => null;
        public float CasterHeight => Caster;
    }

    [Fact]
    public void test_hard_hands_clip_protects_its_contact_and_follow_through()
    {
        var t = HardHands();
        var recipe = ActionRecipes.SeekerHardHands;
        int commit = t.Markers[recipe.CommitMarker], contact = t.Markers[recipe.ContactMarker], recovery = t.Markers[recipe.RecoveryMarker];
        Assert.True(t.Markers["anticipation"] < commit && commit < contact && contact < recovery, "anticipation, commit, contact, recovery in order");
        for (var f = commit; f < recovery; f++)
            Assert.False(t.Elastic[f], $"frame {f} (commit to follow-through) never shrinks");
        Assert.True(t.Elastic[t.Frames - 1], "the late recovery is what gives way");
        Assert.NotNull(t.Socket(contact, recipe.HandSocket));
        Assert.True(t.Socket(contact, recipe.HandSocket)!.Value.AngleDegrees > 20f, "the blow drives DOWN and forward at contact");
    }

    [Fact]
    public void test_root_motion_pulls_back_commits_fast_overshoots_and_comes_home()
    {
        var recipe = ActionRecipes.SeekerHardHands;
        var t = HardHands();
        const float reach = 480f;
        float At(float ms) => MeleePerformance.RootMotion(recipe, t, reach, ms, Caster);
        float tCommit = t.MarkerMs(recipe.CommitMarker), tContact = t.MarkerMs(recipe.ContactMarker), tRecover = t.MarkerMs(recipe.RecoveryMarker);

        Assert.Equal(0f, At(0f));
        Assert.True(At(tCommit - 1f) < 0f, "the anticipation draws the body back first");
        Assert.Equal(reach, At(tContact), 1);                                // the fist is on the target ON the contact
        Assert.True(At(tRecover - 1f) > reach, "the follow-through carries a little past it");
        // the commit ACCELERATES into the blow: its second half covers more ground than its first
        var mid = (tCommit + tContact) / 2f;
        Assert.True(At(tContact - 1f) - At(mid) > At(mid) - At(tCommit), "fastest spacing at contact");
        Assert.Equal(0f, At(t.TotalMs - 1f), 1);                             // home before the exit pose ends
        // no teleport: at 60 fps the body never moves more than a third of the reach in one frame
        var frame = 1000f / 60f;
        for (var ms = 0f; ms < t.TotalMs; ms += frame)
            Assert.True(MathF.Abs(At(ms + frame) - At(ms)) < reach / 3f, $"a {At(ms + frame) - At(ms):0} px jump at {ms:0} ms");
    }

    [Fact]
    public void test_the_return_is_a_hop_that_lands_when_the_body_is_home()
    {
        var recipe = ActionRecipes.SeekerHardHands;
        var t = HardHands();
        const float reach = 480f;
        float Lift(float ms) => MeleePerformance.RootLift(recipe, t, reach, ms, Caster);
        float Home(float ms) => MeleePerformance.RootMotion(recipe, t, reach, ms, Caster);
        var tRecover = t.MarkerMs(recipe.RecoveryMarker);

        Assert.Equal(0f, Lift(t.MarkerMs(recipe.ContactMarker)));             // the blow is grounded
        var mid = tRecover + 0.5f * (t.TotalMs - tRecover) * recipe.ReturnShare;
        Assert.True(Lift(mid) < -0.04f * Caster, "off the ground halfway home");
        Assert.True(Home(mid) > 0f, "while still travelling");
        var landed = tRecover + (t.TotalMs - tRecover) * recipe.ReturnShare + 1f;
        Assert.Equal(0f, Lift(landed));
        Assert.Equal(0f, Home(landed), 1);                                        // it lands home, not short of it
    }

    [Fact]
    public void test_a_yielded_lunge_hops_home_under_the_next_wind_up_without_a_jump()
    {
        // Arrange: the fastest build's HARD HANDS -> SPRAY: the clip is cut 30 ms after its contact, and the body
        // must be home by SPRAY's release, 220 ms later (ADR-011's fast-TEMPO fallback)
        var recipe = ActionRecipes.SeekerHardHands;
        var t = HardHands();
        const float reach = 520f;
        var cutAt = t.MarkerMs(recipe.ContactMarker) + 30f;
        const float returnMs = 220f;
        float At(float ms) => MeleePerformance.RootMotion(recipe, t, reach, ms, Caster, cutAt, returnMs);
        float Lift(float ms) => MeleePerformance.RootLift(recipe, t, reach, ms, Caster, cutAt, returnMs);

        // Act + Assert: out at the cut, home by the release, off the ground in between, and no frame jumps
        Assert.True(At(cutAt) >= reach);
        Assert.Equal(0f, At(cutAt + returnMs));
        Assert.True(Lift(cutAt + returnMs / 2f) < 0f, "a hop, not a slide");
        var frame = 1000f / 60f;
        for (var ms = cutAt - frame; ms < cutAt + returnMs + frame; ms += frame)
            Assert.True(MathF.Abs(At(ms + frame) - At(ms)) < reach / 6f, $"a {At(ms + frame) - At(ms):0} px jump at {ms:0} ms");
    }

    [Fact]
    public void test_a_compressed_recovery_brings_the_body_home_sooner_and_keeps_the_blow()
    {
        var recipe = ActionRecipes.SeekerHardHands;
        var t = HardHands();
        var fast = t.FitRecovery(t.RecoveryMotionMs / 2f, 2000f / 60f, out var compression);
        Assert.True(compression > 1.5f);
        Assert.Equal(t.MarkerMs(recipe.ContactMarker), fast.MarkerMs(recipe.ContactMarker));
        Assert.Equal(t.MarkerMs(recipe.RecoveryMarker), fast.MarkerMs(recipe.RecoveryMarker));   // the follow-through is whole
        // home when the recovery ends, or MinReturnMs after it began if the handoff left less (never a one-frame snap)
        var home = fast.MarkerMs(recipe.RecoveryMarker) + Math.Max(recipe.MinReturnMs, fast.TotalMs - fast.MarkerMs(recipe.RecoveryMarker));
        Assert.Equal(0f, MeleePerformance.RootMotion(recipe, fast, 480f, home, Caster));
        Assert.True(MeleePerformance.RootMotion(recipe, fast, 480f, fast.MarkerMs(recipe.RecoveryMarker) + 20f, Caster) > 300f,
                    "20 ms into the return the body is still most of the way out: it travels, it does not snap");
        Assert.True(fast.FrameMs.Skip(t.Markers[recipe.RecoveryMarker]).All(ms => ms >= 2000f / 60f - 0.01f),
                    "the half-rise is still shown: never low crouch straight to standing");
    }

    [Fact]
    public void test_the_blow_contacts_on_the_beat_with_the_fist_on_the_target()
    {
        var recipe = ActionRecipes.SeekerHardHands;
        var t = HardHands();
        const float beat = 5200f;
        var plan = ActionPerformance.Schedule(recipe, t, beat, beat - t.MarkerMs(recipe.ContactMarker) + 5f)!.Value;
        Assert.Equal(beat, plan.StartMs + plan.Timing.MarkerMs(recipe.ContactMarker), 2);   // no flight: the contact IS the beat
        var stage = new Stage();
        var p = new MeleePerformance(recipe, plan.Timing, beat, plan.StartMs, 0, new[] { 0, 1 }, Color.White);

        var start = p.Update(plan.StartMs, 0.016f, stage);
        Assert.False(start.Released);
        var commit = p.Update(p.ReleaseMs, 0.016f, stage);
        Assert.True(commit.Released && !commit.Contacted);
        var hit = p.Update(beat, 0.016f, stage);
        Assert.True(hit.Contacted);
        stage.TryTargetBody(0, out var body);
        Assert.Equal(body.X + body.Width * recipe.ContactPoint.X, hit.ContactAt.X, 1);
        Assert.True(p.Reach > 300f, "the body travels to the creature; the arm does not stretch");
        Assert.Equal(p.Reach, p.RootOffsetX(beat), 1);
    }

    [Fact]
    public void test_hard_hands_is_heard_by_its_own_cues()
    {
        var hands = ActionRecipes.SeekerHardHands;
        var spray = ActionRecipes.SeekerSpray;
        Assert.True(hands.ReplacesGenericHit, "the specific impact silences the generic weak-hit puff and thud");
        // the SPECIFIC cues are its own (both chains end in the fight's generic cues, the last resort)
        Assert.DoesNotContain(hands.ReleaseCues[0], spray.ReleaseCues.Concat(spray.ContactCues));
        Assert.DoesNotContain(hands.ContactCues[0], spray.ReleaseCues.Concat(spray.ContactCues));
        foreach (var cue in new[] { hands.ReleaseCues[0], hands.ContactCues[0] })
            Assert.True(File.Exists(RepoFile("assets", "audio", "combat", cue + ".wav")), $"{cue}.wav is missing");
    }

    [Fact]
    public void test_the_wave_clear_lets_a_running_performance_play_out()
    {
        // The wave's last kill landed by a lunge 807 px out: the clear dropped the clip and the performance on the
        // spot, and the body snapped home in one frame. The break now plays a running performance out on its own
        // clock (the replay is over), and only cuts when nothing is running.
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var brk = src[src.IndexOf("if (_breakTimer > 0f)", StringComparison.Ordinal)..];
        brk = brk[..brk.IndexOf("ClearBeat.Tick(", StringComparison.Ordinal)];
        var playsOut = brk.IndexOf("if (ActionStillPlaying())", StringComparison.Ordinal);
        Assert.True(playsOut >= 0, "the clear asks whether an action is still playing");
        Assert.Contains("UpdatePerformance(dt);", brk[playsOut..]);
        Assert.True(brk.IndexOf("_performance = null;", StringComparison.Ordinal) > brk.IndexOf("else", playsOut, StringComparison.Ordinal),
                    "the performance is dropped only when nothing is running");
    }

    [Fact]
    public void test_a_shared_form_performs_only_the_effects_its_recipe_names()
    {
        if (!ActionRecipes.Enabled) return;                    // RH_ACTION_RECIPES=0 in the environment
        Assert.Same(ActionRecipes.SeekerHardHands, ActionRecipes.For("seeker", "strike", "strike"));   // HARD HANDS, BLOW
        Assert.Null(ActionRecipes.For("seeker", "strike", "press"));                                    // PRESS: a field
        Assert.Same(ActionRecipes.SeekerSpray, ActionRecipes.For("seeker", "projectile", "projectile"));
    }
}
