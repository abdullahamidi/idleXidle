using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using IdleXIdle.Game.Presentation;
using IdleXIdle.Game.Presentation.Curse;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE CURSE'S VOICE (design/audio/seeker-brand-audio-brief.md; ADR-011's MARK reference, BRAND): the cue SCHEDULE built
/// once per wave from the same truth the curse is drawn from (<see cref="MarkVoice"/>), asked on the playhead with PRESS's
/// rule, weighted by the mix rules, and wired into the screen beside PRESS's voice. The cue FILES are Candidate A
/// (SUBTLE / INTERNAL), HUMAN-APPROVED by the owner 2026-10-02: their bytes are pinned here and in the provenance manifest
/// (tools/asset-pipeline/foley/brand_curse/brand_audio_manifest.json); B and C are rejected and archive-only.
/// </summary>
public class brand_audio_test
{
    private static readonly MarkRecipe Brand = MarkRecipes.SeekerBrand;
    private const float Frame = 1000f / 60f;

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static readonly (float, int, bool)[] Plain =
        { (2000f, 70, false), (4000f, 70, false), (6000f, 70, false), (8000f, 70, false), (10000f, 70, false) };

    private static readonly (float, int, bool)[] Etch =
        { (2000f, 120, false), (4000f, 170, false), (6000f, 220, false), (8000f, 240, false), (10000f, 240, false) };

    private static MarkPerformance Front((float, int, bool)[] ticks, params (float, int)[] falls)
        => new(Brand, 2, ticks, falls.Length > 0 ? falls : new[] { (6700f, 0), (8200f, 1) }, 4, wholeWave: false, frontFullPercent: 0);

    private static MarkPerformance Sprawl((float, int, bool)[] ticks, params (float, int)[] falls)
        => new(Brand, 2, ticks, falls, 4, wholeWave: true, frontFullPercent: 0);

    private static MarkVoice Voice(MarkPerformance m, float burn = 0f, float[]? snaps = null, float[]? quiet = null, int seats = 4)
        => new(m, Enumerable.Repeat(burn, 4).ToArray(), Enumerable.Repeat(seats, 4).ToArray(),
               snaps ?? Array.Empty<float>(), quiet ?? Array.Empty<float>());

    private static List<MarkCue> Cues(MarkVoice v) => Enumerable.Range(0, v.Count).Select(v.Cue).ToList();

    private static List<MarkCue> Mains(MarkVoice v) => Cues(v).Where(c => c.Kind != MarkCueKind.Ash).ToList();

    private static bool IsDeepen(MarkCue c) => c.Kind is MarkCueKind.Deepen or MarkCueKind.DeepenDeep;

    /// <summary>The steps the CURSE finds, frame by frame at 60 Hz, as the screen draws it (its own timeline per slot).</summary>
    private static List<(float At, int Slot, int From, int To)> VisualSteps(MarkPerformance m, float untilMs = 20000f)
    {
        var steps = new List<(float, int, int, int)>();
        for (var s = 0; s < 4; s++)
        {
            var tl = new CurseTimeline();
            for (var t = 0f; t < untilMs; t += Frame) tl.ClockAt(m, s, t, CurseSeating.MaxTerritories);
            steps.AddRange(tl.Steps.Select(st => (st.At, s, st.From, st.To)));
        }
        return steps;
    }

    // ── WHEN EACH CUE PLAYS ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_apply_is_one_cue_whose_take_hold_lands_on_the_flare()
    {
        var v = Voice(Front(Plain));
        var apply = Assert.Single(Mains(v), c => c.Kind == MarkCueKind.Apply);
        Assert.Equal(0, apply.Slot);
        Assert.Equal(2000f, apply.AtMs);
        Assert.Equal(2000f + Brand.ApplyTakeHoldMs, apply.TakeHoldMs);                 // the flare peaks 60..160 after the tick
        Assert.Equal(apply.TakeHoldMs - Brand.ApplyCueStartMs, apply.StartMs);        // start = moment - transient offset
        Assert.InRange(Brand.ApplyTakeHoldMs, 60f, 160f);
        // plain BRAND never deepens: apply, then a leave and an awaken per transfer, and the idle state is silent
        Assert.DoesNotContain(Mains(v), IsDeepen);
        Assert.Equal(new[] { MarkCueKind.Apply, MarkCueKind.Leave, MarkCueKind.Awaken, MarkCueKind.Leave, MarkCueKind.Awaken },
                     Mains(v).OrderBy(c => c.AtMs).Select(c => c.Kind).ToArray());
    }

    [Fact]
    public void test_etch_deepens_once_per_shown_step_and_a_multi_depth_jump_is_one_deep_cue()
    {
        var v = Voice(Front(Etch));
        var deepens = Mains(v).Where(IsDeepen).OrderBy(c => c.AtMs).ToList();
        Assert.Equal(2, deepens.Count);
        // depth 1 -> 2 on the 4000 tick, shown two chisel steps later; 2 -> 3 on the 6000 tick
        Assert.Equal(MarkCueKind.Deepen, deepens[0].Kind);
        Assert.Equal(4000f + Brand.CarveStepMs * 2f, deepens[0].AtMs, 0);
        Assert.Equal(MarkCueKind.DeepenDeep, deepens[1].Kind);
        Assert.Equal(6000f + Brand.CarveStepMs * 2f, deepens[1].AtMs, 0);
        Assert.All(deepens, d => Assert.Equal(d.AtMs, d.StartMs, 3));                 // the file's 0 on the step: take-hold at +230
        Assert.All(deepens, d => Assert.Equal(d.AtMs + Brand.DeepenTakeHoldMs, d.TakeHoldMs, 3));
        // 220 -> 240 % stays fully branded: no cue (no step)
        Assert.DoesNotContain(deepens, d => d.AtMs > 7000f);
        // a 1 -> 3 jump on one tick: ONE step, ONE deep cue, never a cue per new territory
        var jump = Front(new[] { (2000f, 70, false), (4000f, 220, false) });
        var steps = VisualSteps(jump).Where(s => s.To > s.From).ToList();
        var step = Assert.Single(steps);
        Assert.Equal((1, 3), (step.From, step.To));
        var cue = Assert.Single(Mains(Voice(jump)), IsDeepen);
        Assert.Equal(MarkCueKind.DeepenDeep, cue.Kind);
        Assert.Equal(step.At, cue.AtMs, 3);
    }

    [Fact]
    public void test_every_scheduled_deepen_is_a_step_the_curse_shows_within_a_millisecond()
    {
        var strikes = Etch.Select(t => t.Item1).ToArray();
        var scenarios = new (string Name, MarkPerformance Mark)[]
        {
            ("etch", Front(Etch)),
            ("etch bitten", new MarkPerformance(Brand, 2, Etch, new[] { (6700f, 0), (8200f, 1) }, 4, false, 0, strikes)),
            ("transfer mid-deepen", new MarkPerformance(Brand, 2, Etch, new[] { (5200f, 0), (6320f, 1) }, 4, false, 0, strikes)),
            ("in flight", Front(Etch, (3800f, 0), (9000f, 1))),
            ("sprawl", Sprawl(new[] { (2000f, 35, false), (4000f, 170, false), (6000f, 220, false) })),
            ("sprawl bitten", new MarkPerformance(Brand, 2, new[] { (2000f, 35, false), (4000f, 170, false), (6000f, 220, false) },
                                                  new[] { (7000f, 1) }, 4, true, 0, strikes)),
            ("anchor", new MarkPerformance(Brand, 2, new[] { (2000f, 35, false), (4000f, 55, false) }, new[] { (4050f, 0) }, 4, true, 70)),
        };
        foreach (var (name, m) in scenarios)
        {
            var v = Voice(m);
            var visual = VisualSteps(m).Where(s => s.To > s.From && CurseSeating.TerritoriesAt(s.To) > CurseSeating.TerritoriesAt(s.From)).ToList();
            var deepens = Mains(v).Where(IsDeepen).ToList();
            // every scheduled deepen IS a step the curse shows on that creature (the same truth, the same lattice)
            foreach (var d in deepens)
                Assert.True(visual.Any(s => s.Slot == d.Slot && Math.Abs(s.At - d.AtMs) <= 1f),
                            $"{name}: deepen at {d.AtMs} on slot {d.Slot} is no shown step ({string.Join(",", visual.Select(s => $"{s.At}>{s.Slot}"))})");
            // and every shown step whose new territory is born on a standing host is voiced (a SPRAWL ripple by its first)
            foreach (var s in visual.Where(s => m.DeathAt(s.Slot) >= s.At + CurseTimeline.TravelMs))
                Assert.True(deepens.Any(d => s.At - d.AtMs >= -1f && s.At - d.AtMs <= v.DeepenRippleMs),
                            $"{name}: the step at {s.At} on slot {s.Slot} has no deepen cue");
            Assert.True(visual.Count == 0 || deepens.Count > 0, name);
        }
    }

    [Fact]
    public void test_sprawl_is_the_sources_apply_then_one_quieter_infect_per_victim_down_the_row()
    {
        var m = Sprawl(new[] { (2000f, 35, false), (4000f, 35, false) });
        var v = Voice(m);
        Assert.Single(Mains(v), c => c.Kind == MarkCueKind.Apply);                      // never four applies
        var infects = Mains(v).Where(c => c.Kind == MarkCueKind.Infect).OrderBy(c => c.AtMs).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, infects.Select(c => c.Slot).ToArray());
        for (var i = 0; i < infects.Count; i++)
        {
            Assert.Equal(m.HopLandsAt(infects[i].Slot), infects[i].AtMs, 3);           // at its landing
            Assert.Equal(infects[i].AtMs, infects[i].StartMs, 3);
        }
        Assert.Equal(new[] { 2200f, 2240f, 2280f }, infects.Select(c => c.AtMs).ToArray());   // staggered as the picture
        var idx = infects.Select(c => Enumerable.Range(0, v.Count).First(k => v.Cue(k) == c)).ToArray();
        for (var i = 1; i < idx.Length; i++)
        {
            Assert.Equal(Brand.InfectStepShare, v.CueVolume(idx[i]) / v.CueVolume(idx[i - 1]), 3);   // each a little quieter
            Assert.Equal(Brand.InfectStepPitch, infects[i].Pitch - infects[i - 1].Pitch, 3);         // and a little lower
        }
        Assert.Equal(0f, infects[0].Pitch);
        Assert.True(v.CueVolume(idx[0]) < v.CueVolume(Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == MarkCueKind.Apply)));
        Assert.InRange(-Brand.InfectStepPitch, 0.001f, 1f / 12f);                       // a semitone or less per victim
        // a victim that falls before its landing is passed over: no infect for it
        var passed = Voice(Sprawl(new[] { (2000f, 35, false) }, (2150f, 1)));
        Assert.DoesNotContain(Mains(passed), c => c.Kind == MarkCueKind.Infect && c.Slot == 1);
        Assert.Equal(2, Mains(passed).Count(c => c.Kind == MarkCueKind.Infect));
        Assert.True(MarkRecipe.Unthrottled(MarkCueKind.Infect));                      // 40 ms apart: past the bank's throttle
    }

    [Fact]
    public void test_a_transfer_is_a_leave_on_the_fall_silence_then_an_awaken_on_the_landing()
    {
        var m = Front(Plain);
        var main = Mains(Voice(m)).OrderBy(c => c.AtMs).ToList();
        var leave = main.First(c => c.Kind == MarkCueKind.Leave);
        var awaken = main.First(c => c.Kind == MarkCueKind.Awaken);
        Assert.Equal(6700f, leave.AtMs);
        Assert.Equal(0, leave.Slot);
        Assert.Equal(1, awaken.Slot);
        var lands = 6700f + Brand.FlightFromMs + Brand.FlightMs;
        Assert.Equal(MarkPhase.Reform, m.TryPhase(1, lands + 1f, out _));
        Assert.Equal(lands - 6700f, awaken.AtMs - leave.AtMs, 3);                     // gap = lands - F
        Assert.Equal(lands, awaken.StartMs, 3);                                         // the inhale begins on the landing
        // no travel cue, no beam: nothing starts between the leave and the awaken
        Assert.DoesNotContain(main, c => c.StartMs > leave.StartMs && c.StartMs < awaken.StartMs);
        Assert.Equal(leave.AtMs + Brand.LeaveTakeHoldMs - Brand.LeaveCueStartMs, leave.StartMs, 3);
    }

    [Fact]
    public void test_a_death_without_a_transfer_is_a_leave_alone_and_a_row_kill_is_one_leave()
    {
        // the last host falls: a leave, nothing receives it
        var last = new MarkPerformance(Brand, 2, Plain, new[] { (6700f, 0) }, 1, false, 0);
        var main = Mains(Voice(last));
        Assert.Single(main, c => c.Kind == MarkCueKind.Leave);
        Assert.DoesNotContain(main, c => c.Kind == MarkCueKind.Awaken);
        // a SPRAWL row kill: four cursed hosts on one frame are ONE leave
        var row = Sprawl(new[] { (2000f, 35, false), (4000f, 35, false) }, (5000f, 0), (5000f, 1), (5000f, 2), (5000f, 3));
        var leaves = Mains(Voice(row)).Where(c => c.Kind == MarkCueKind.Leave).ToList();
        var one = Assert.Single(leaves);
        Assert.Equal(5000f, one.AtMs);
        // two falls a frame apart are one; two falls clearly apart are two
        var near = Sprawl(new[] { (2000f, 35, false) }, (5000f, 0), (5000f + Frame, 1), (5600f, 2));
        Assert.Equal(new[] { 5000f, 5600f }, Mains(Voice(near)).Where(c => c.Kind == MarkCueKind.Leave).Select(c => c.AtMs).ToArray());
        // the final leave of a wave-ending kill rides the curse's own StillLeaving (the break keeps the playhead running)
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        Assert.Contains("|| _curse is { } c && c.StillLeaving(_playheadMs)", hunt);
    }

    [Fact]
    public void test_a_host_dying_mid_transition_gets_no_cue_for_what_never_blooms()
    {
        // ETCH, a bite on every tick: slot 1's catch-up 2 -> 3 is shown at 6380, its new territories would be born from
        // 6550, and it falls at 6450: the curse shows the step but nothing blooms, so no deepen is voiced
        var strikes = Etch.Select(t => t.Item1).ToArray();
        var m = new MarkPerformance(Brand, 2, Etch, new[] { (5200f, 0), (6450f, 1) }, 4, false, 0, strikes);
        var shown = VisualSteps(m).Where(s => s.Slot == 1 && s.To > s.From).ToList();
        var step = Assert.Single(shown);
        Assert.True(m.DeathAt(1) < step.At + CurseTimeline.TravelMs, $"step at {step.At}");
        var main = Mains(Voice(m));
        Assert.DoesNotContain(main, c => IsDeepen(c) && c.Slot == 1);
        Assert.Contains(main, c => c.Kind == MarkCueKind.Leave && c.Slot == 1);         // its curse still leaves it
        // a creature the fall passes over (it falls while the smoke flies) never awakens and never leaves
        var passed = Mains(Voice(Front(Plain, (6700f, 0), (6800f, 1))));
        Assert.DoesNotContain(passed, c => c.Slot == 1);
        Assert.Single(passed, c => c.Kind == MarkCueKind.Awaken && c.Slot == 2);
        Assert.Equal(new[] { 6700f }, passed.Where(c => c.Kind == MarkCueKind.Leave).Select(c => c.AtMs).ToArray());
    }

    [Fact]
    public void test_an_arrival_and_its_catch_up_deepen_are_two_different_cues_never_a_duplicate()
    {
        // the front falls at 3800 and the 4000 tick deepens the mark in flight: it awakens on slot 1, then is cut deeper
        var m = Front(Etch, (3800f, 0), (9000f, 1));
        var onOne = Mains(Voice(m)).Where(c => c.Slot == 1).OrderBy(c => c.AtMs).ToList();
        Assert.Equal(MarkCueKind.Awaken, onOne[0].Kind);
        Assert.Equal(MarkCueKind.Deepen, onOne[1].Kind);
        Assert.True(onOne[1].AtMs > onOne[0].AtMs);
        Assert.Equal(onOne.Count, onOne.Select(c => (c.Kind, c.AtMs)).Distinct().Count());
        for (var i = 1; i < onOne.Count; i++)
            Assert.False(onOne[i].Kind == onOne[i - 1].Kind && onOne[i].AtMs - onOne[i - 1].AtMs < 1000f, "a cue voiced twice");
    }

    // ── ASKED ONCE ON THE PLAYHEAD ───────────────────────────────────────────────────────────────────────────────────

    private static int[] Due(MarkVoice v, float playheadMs)
    {
        var due = new List<int>();
        for (var i = v.CueDue(playheadMs); i >= 0; i = v.CueDue(playheadMs, i + 1)) due.Add(i);
        return due.ToArray();
    }

    [Fact]
    public void test_a_cue_fires_once_rearms_on_a_rewind_and_is_skipped_when_late()
    {
        var v = Voice(Front(Plain));
        var apply = Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == MarkCueKind.Apply);
        var at = v.Cue(apply).StartMs;
        Assert.Empty(Due(v, at - 10f));
        Assert.Equal(new[] { apply }, Due(v, at + 5f));
        Assert.Empty(Due(v, at + 5f));                                                   // a frozen playhead: never again
        Assert.Empty(Due(v, at + 20f));
        Assert.Empty(Due(v, at - 100f));                                                 // a rewind re-arms it...
        Assert.Equal(new[] { apply }, Due(v, at + 2f));                                  // ...and it plays again
        Assert.Empty(Due(v, at - 100f));
        Assert.Empty(Due(v, at + Brand.CueLateMs + 50f));                                // a seek past it: skipped, not late
        Assert.Empty(Due(v, at + Brand.CueLateMs + 60f));
        // the wave's last leave on a frozen playhead (the break): asked once in a hundred frames
        var leave = Enumerable.Range(0, v.Count).Last(k => v.Cue(k).Kind == MarkCueKind.Leave);
        var fired = 0;
        for (var f = 0; f < 100; f++) fired += Due(v, v.Cue(leave).StartMs + 3f).Count(k => k == leave);
        Assert.Equal(1, fired);
        // several cues due on one frame are all returned (the main cue and its ash accent)
        var ash = Voice(Front(Plain), burn: 1f);
        var leaveCue = Enumerable.Range(0, ash.Count).First(k => ash.Cue(k).Kind == MarkCueKind.Leave);
        var accent = Enumerable.Range(0, ash.Count).First(k => ash.Cue(k).Kind == MarkCueKind.Ash && ash.Cue(k).AtMs == ash.Cue(leaveCue).AtMs);
        var frame = Math.Max(ash.Cue(leaveCue).StartMs, ash.Cue(accent).StartMs) + 1f;
        Assert.Empty(Due(ash, ash.Cue(leaveCue).StartMs - 50f).Where(k => k == leaveCue || k == accent));
        Assert.Equal(new[] { leaveCue, accent }.OrderBy(k => k), Due(ash, frame).Where(k => k == leaveCue || k == accent).OrderBy(k => k));
        // the cues are in start order (the screen walks them forward)
        Assert.True(Cues(v).Select(c => c.StartMs).SequenceEqual(Cues(v).Select(c => c.StartMs).OrderBy(x => x)));
    }

    // ── THE MIX ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_quiet_beside_an_action_yield_to_jaws_and_the_duck_alone()
    {
        // HARD HANDS' contact on the fall at 6700 (quiet), JAWS snapping at 7300 near the awaken's take-hold (yield)
        var v = Voice(Front(Plain), snaps: new[] { 7300f }, quiet: new[] { 6700f });
        int Of(MarkCueKind kind, float at) => Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == kind && v.Cue(k).AtMs == at);
        var apply = Of(MarkCueKind.Apply, 2000f);
        var leave = Of(MarkCueKind.Leave, 6700f);
        var awaken = Of(MarkCueKind.Awaken, 6700f + Brand.FlightFromMs + Brand.FlightMs);
        Assert.Equal(Brand.ApplyVolume, v.CueVolume(apply), 4);
        Assert.Equal(Brand.ApplyVolume * Brand.CueQuietShare, v.CueVolume(apply, performing: true), 4);
        Assert.Equal(Brand.ApplyVolume, v.CueVolume(apply, ducked: true, performing: true), 4);   // the duck alone
        Assert.Equal(Brand.LeaveVolume * Brand.CueQuietShare, v.CueVolume(leave), 4);
        Assert.Equal(Brand.LeaveVolume, v.CueVolume(leave, ducked: true), 4);                      // never duck x quiet
        Assert.Equal(Brand.AwakenVolume * Brand.CueYieldShare, v.CueVolume(awaken), 4);
        Assert.Equal(Brand.AwakenVolume * Brand.CueYieldShare, v.CueVolume(awaken, ducked: true, performing: true), 4);
        // the windows are the picture's own (and PRESS's)
        Assert.Equal((700f, 400f, 200f, 350f), (Brand.YieldBeforeMs, Brand.YieldAfterMs, Brand.QuietBeforeMs, Brand.QuietAfterMs));
        Assert.Equal((0.6f, 0.5f, 30f), (Brand.CueQuietShare, Brand.CueYieldShare, Brand.CueLateMs));
        Assert.True(v.Cue(awaken).Yields && !v.Cue(awaken).Quiet && v.Cue(leave).Quiet && !v.Cue(apply).Quiet && !v.Cue(apply).Yields);
    }

    [Fact]
    public void test_the_ash_accent_is_continuous_in_the_hosts_burn_and_never_a_name()
    {
        var previousMain = float.MaxValue;
        var previousAsh = -1f;
        foreach (var burn in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f })
        {
            var v = Voice(Front(Plain), burn: burn);
            var apply = Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == MarkCueKind.Apply);
            Assert.Equal(Brand.ApplyVolume * (1f - Brand.AshMainCut * burn), v.CueVolume(apply), 4);
            var accents = Enumerable.Range(0, v.Count).Where(k => v.Cue(k).Kind == MarkCueKind.Ash).ToArray();
            // one accent per main cue on a burnt host, none on a host with no Ash-Burn
            Assert.Equal(burn > 0f ? Mains(v).Count : 0, accents.Length);
            var ash = accents.Length > 0 ? v.CueVolume(accents.First(k => v.Cue(k).AtMs == 2000f)) : 0f;
            Assert.Equal(Brand.AshVolume * burn, ash, 4);
            Assert.True(v.CueVolume(apply) < previousMain && ash > previousAsh);
            previousMain = v.CueVolume(apply);
            previousAsh = ash;
            // the accent lands on its cue's take-hold
            foreach (var k in accents)
                Assert.Equal(v.Cue(k).TakeHoldMs - Brand.AshCueStartMs, v.Cue(k).StartMs, 3);
        }
        // giving way to JAWS, the accent (the least important layer) is omitted and the main cue is whole
        var y = Voice(Front(Plain), burn: 1f, snaps: new[] { 2100f });
        var yApply = Enumerable.Range(0, y.Count).First(k => y.Cue(k).Kind == MarkCueKind.Apply);
        var yAsh = Enumerable.Range(0, y.Count).First(k => y.Cue(k).Kind == MarkCueKind.Ash && y.Cue(k).AtMs == 2000f);
        Assert.Equal(0f, y.CueVolume(yAsh));
        Assert.Equal(Brand.ApplyVolume * Brand.CueYieldShare, y.CueVolume(yApply), 4);
        // a host with no baked data shows no curse, so it is not voiced either
        Assert.Equal(0, Voice(Front(Plain), seats: 0).Count);
        // the weight is the baked host data's, never a creature's name
        var code = string.Join("\n", File.ReadAllLines(RepoFile("src", "IdleXIdle.Game", "Presentation", "MarkVoice.cs"))
                                          .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        Assert.DoesNotContain("\"", code);                                                // no string at all: no name to check
        Assert.DoesNotContain("IdleStrip", code);
    }

    [Fact]
    public void test_the_ash_accent_is_heard_at_the_share_its_cue_was_never_louder_than_the_layer_it_accents()
    {
        // a deepen's accent starts ~225 ms after the deepen (on its take-hold): the champion may have stopped performing,
        // or an action's duck begun, in between (the live etch take: the deepen quiet, its accent at full)
        var v = Voice(Front(Etch, (5200f, 0), (6700f, 1)), burn: 1f);
        var deepen = Enumerable.Range(0, v.Count).First(k => IsDeepen(v.Cue(k)));
        var accent = Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == MarkCueKind.Ash && v.Cue(k).TakeHoldMs == v.Cue(deepen).TakeHoldMs);
        var full = Brand.AshVolume;
        // asked before its cue was (a seek landed between them): the cue never played, so neither does its accent
        Assert.Equal(0f, v.CueVolume(accent, performing: true));
        // the cue quiet (the champion performing), the accent's frame not: the accent quiet too
        v.CueVolume(deepen, performing: true);
        Assert.Equal(full * Brand.CueQuietShare, v.CueVolume(accent), 4);
        // the cue under the action's duck, the accent's frame not: the accent at the duck's level
        v.CueVolume(deepen, ducked: true, duck: 0.45f);
        Assert.Equal(full * 0.45f, v.CueVolume(accent), 4);
        // the cue whole, the accent's frame ducked: the bank's duck alone (never above full, never duck x share)
        v.CueVolume(deepen);
        Assert.Equal(full, v.CueVolume(accent, ducked: true, duck: 0.45f), 4);
        // the cue quiet, the accent's frame ducked: the cue's share, the frame's duck taken back out
        v.CueVolume(deepen, performing: true);
        Assert.Equal(full * Brand.CueQuietShare / 0.45f > full ? full : full * Brand.CueQuietShare / 0.45f,
                     v.CueVolume(accent, ducked: true, duck: 0.45f), 4);
        // a rewind re-arms both: the accent forgets the share its cue was heard at (and is silent until it is asked again)
        Assert.Equal(-1, v.CueDue(v.Cue(deepen).StartMs - 100f));
        Assert.Equal(0f, v.CueVolume(accent));
        v.CueVolume(deepen);
        Assert.Equal(full, v.CueVolume(accent), 4);
    }

    [Fact]
    public void test_every_brand_cue_is_quieter_than_press_jaws_and_the_action_contacts()
    {
        var ceiling = new[]
        {
            FieldRecipes.SeekerPress.TickVolume, ReactionRecipes.SeekerJaws.SnapVolume,
            ActionRecipes.SeekerSpray.ContactVolume, ActionRecipes.SeekerHardHands.ContactVolume,
        }.Min();
        foreach (var kind in Enum.GetValues<MarkCueKind>())
        {
            Assert.True(Brand.VolumeOf(kind) < ceiling, $"{kind} at {Brand.VolumeOf(kind)}");
            Assert.NotEmpty(Brand.CuesOf(kind));
            Assert.All(Brand.CuesOf(kind), k => Assert.StartsWith("sfx_seeker_brand_", k));
            Assert.InRange(Brand.CueStartOf(kind), 0f, 250f);
        }
        // the deep deepen is never louder than its sibling by more than the brief's half decibel
        Assert.InRange(Brand.DeepenDeepVolume / Brand.DeepenVolume, 1f, 1.07f);
        // the loudest a cue ever plays (full, no burn) in a busy wave is under the ceiling too
        var v = Voice(Sprawl(new[] { (2000f, 35, false), (4000f, 170, false), (6000f, 220, false) }, (7000f, 0)), burn: 0.6f);
        for (var i = 0; i < v.Count; i++) Assert.True(v.CueVolume(i) < ceiling);
        Assert.Equal((0.35f, 0.03f), (Brand.CuePanWidth, Brand.CueVary));
        // the brief's take-hold offsets
        Assert.Equal(new[] { 180f, 230f, 230f, 15f, 10f, 120f, 5f },
                     new[] { MarkCueKind.Apply, MarkCueKind.Deepen, MarkCueKind.DeepenDeep, MarkCueKind.Infect, MarkCueKind.Leave,
                             MarkCueKind.Awaken, MarkCueKind.Ash }.Select(Brand.CueStartOf).ToArray());
    }

    [Fact]
    public void test_a_sprawl_ripple_down_a_long_row_is_one_deepen_and_one_creatures_steps_are_never_merged()
    {
        // LEGION's eight-creature row: one tick's ripple steps 60 ms per place, the last 420 ms after the first
        var span = MarkVoice.RippleSpanMs(Brand, 8);
        Assert.True(span >= 7 * Brand.CarveStepMs * 1.5f, $"span {span}");
        var row = Enumerable.Range(0, 8).Select(p => (2000f + p * Brand.CarveStepMs * 1.5f, p, false)).ToList();
        Assert.Single(MarkVoice.RippleGroups(row, wholeWave: true, span, 8));
        Assert.Equal(8, MarkVoice.RippleGroups(row, wholeWave: true, span, 8)[0].Count);
        // the next tick's ripple is its own cue
        var two = row.Concat(row.Select(x => (x.Item1 + 2000f, x.p, x.Item3))).ToList();
        Assert.Equal(2, MarkVoice.RippleGroups(two, wholeWave: true, span, 8).Count);
        // two shown steps on ONE creature 120 ms apart (a catch-up, then the tick's chisel): two cues, never one
        var same = new List<(float, int, bool)> { (2000f, 0, false), (2060f, 1, false), (2120f, 0, true) };
        var groups = MarkVoice.RippleGroups(same, wholeWave: true, span, 8);
        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { 0, 1 }, groups[0]);
        Assert.Equal(new[] { 2 }, groups[1]);
        // outside SPRAWL nothing is ever folded: every shown step is its own cue
        Assert.Equal(3, MarkVoice.RippleGroups(same, wholeWave: false, span, 8).Count);
        Assert.Equal(2, MarkVoice.RippleGroups(new List<(float, int, bool)> { (2000f, 0, false), (2010f, 1, false) },
                                                wholeWave: false, span, 8).Count);
        // the voice uses the row's own span (MarkPerformance's slot count), so a longer row is still one deepen per tick
        var m = new MarkPerformance(Brand, 2, new[] { (2000f, 35, false), (4000f, 170, false), (6000f, 220, false) },
                                    Array.Empty<(float, int)>(), 8, wholeWave: true, frontFullPercent: 0);
        var v = new MarkVoice(m, new float[8], Enumerable.Repeat(4, 8).ToArray(), Array.Empty<float>(), Array.Empty<float>());
        Assert.Equal(MarkVoice.RippleSpanMs(Brand, 8), v.DeepenRippleMs);
        var deepens = Mains(v).Where(IsDeepen).OrderBy(c => c.AtMs).ToList();
        var ticks = deepens.Select(d => MathF.Floor(d.AtMs / 2000f)).ToList();
        Assert.Equal(ticks.Count, ticks.Distinct().Count());                          // never two deepens for one ripple
        Assert.NotEmpty(deepens);
    }

    [Fact]
    public void test_an_ash_accent_never_plays_without_the_cue_it_rides_on()
    {
        int Of(MarkVoice v, MarkCueKind kind) => Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == kind);
        int AccentOf(MarkVoice v, int main)
            => Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == MarkCueKind.Ash && v.Cue(k).TakeHoldMs == v.Cue(main).TakeHoldMs);
        // the main cue's start skipped late (a hitch past CueLateMs): its accent, due 175 ms later, is omitted
        var v = Voice(Front(Plain), burn: 1f);
        var apply = Of(v, MarkCueKind.Apply);
        var accent = AccentOf(v, apply);
        Assert.DoesNotContain(apply, Due(v, v.Cue(apply).StartMs + Brand.CueLateMs + 50f));
        Assert.Contains(accent, Due(v, v.Cue(accent).StartMs + 1f));
        Assert.Equal(0f, v.CueVolume(accent));
        // the bank's throttle dropped the main cue: the accent is omitted too
        var d = Voice(Front(Plain), burn: 1f);
        var leave = Of(d, MarkCueKind.Leave);
        var leaveAccent = AccentOf(d, leave);
        Assert.Contains(leave, Due(d, d.Cue(leave).StartMs + 1f));
        Assert.True(d.CueVolume(leave) > 0f);
        d.Heard(leave, 0f);
        Assert.Equal(0f, d.CueVolume(leaveAccent));
        // ...made quieter by it (1/sqrt(recent)): the accent is as much quieter, never louder than its cue's layer
        var q = Voice(Front(Plain), burn: 1f);
        var ql = Of(q, MarkCueKind.Leave);
        q.CueVolume(ql);
        q.Heard(ql, 0.5f);
        var whole = Brand.AshVolume * Brand.AshShareUnder(MarkCueKind.Leave);
        Assert.Equal(whole * 0.5f, q.CueVolume(AccentOf(q, ql)), 4);
        // a whole play changes nothing; an accent and an unasked cue ignore the report
        var w = Voice(Front(Plain), burn: 1f);
        var wl = Of(w, MarkCueKind.Leave);
        w.Heard(wl, 1f);                                                                  // not asked yet: ignored
        Assert.Equal(0f, w.CueVolume(AccentOf(w, wl)));
        w.CueVolume(wl);
        w.Heard(wl, 1f);
        w.Heard(AccentOf(w, wl), 0f);
        Assert.Equal(whole, w.CueVolume(AccentOf(w, wl)), 4);
    }

    [Fact]
    public void test_the_ash_accent_sits_under_every_cue_it_rides_at_any_burn_a_sprawl_infect_included()
    {
        // the recipe's loudness targets and volumes are make_brand_cues.py's (each file is mastered to them)
        var make = File.ReadAllText(RepoFile("tools", "asset-pipeline", "make_brand_cues.py"));
        Dictionary<string, float> Table(string name)
            => Regex.Matches(Regex.Match(make, name + @" = \{([^}]*)\}").Groups[1].Value, "\"(\\w+)\": (-?[0-9.]+)")
                    .ToDictionary(x => x.Groups[1].Value, x => float.Parse(x.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        var target = Table("TARGET");
        var volume = Table("VOLUME");
        var keys = new Dictionary<MarkCueKind, string>
        {
            [MarkCueKind.Apply] = "apply", [MarkCueKind.Deepen] = "deepen", [MarkCueKind.DeepenDeep] = "deepen_deep",
            [MarkCueKind.Infect] = "infect", [MarkCueKind.Leave] = "leave", [MarkCueKind.Awaken] = "awaken", [MarkCueKind.Ash] = "ash",
        };
        foreach (var (kind, key) in keys)
        {
            Assert.Equal(target[key], Brand.TargetDbOf(kind), 3);
            Assert.Equal(volume[key], Brand.VolumeOf(kind), 3);
        }
        // the played level of a cue: its file's target moved by its volume against the one it was mastered at
        float Db(MarkCueKind kind, float vol) => Brand.TargetDbOf(kind) + 20f * MathF.Log10(vol / Brand.VolumeOf(kind));
        foreach (var kind in keys.Keys.Where(k => k != MarkCueKind.Ash))
            for (var burn = 0.05f; burn <= 1.0001f; burn += 0.05f)
            {
                var main = Db(kind, Brand.VolumeOf(kind) * (1f - Brand.AshMainCut * burn));
                var ash = Db(MarkCueKind.Ash, Brand.AshVolume * Brand.AshShareUnder(kind) * burn);
                Assert.True(main - ash >= Brand.AshUnderMainDb - 0.001f, $"{kind} at burn {burn}: main {main:0.00} ash {ash:0.00}");
            }
        Assert.Equal(1f, Brand.AshShareUnder(MarkCueKind.Apply));                       // only where it was needed
        Assert.InRange(Brand.AshShareUnder(MarkCueKind.Infect), 0.7f, 0.76f);
        // in a played SPRAWL row on a burn-1 host (the shipped fight's): every infect leads its crumble by >= 2 dB
        var v = Voice(Sprawl(new[] { (2000f, 35, false), (4000f, 35, false) }), burn: 1f);
        var pairs = 0;
        for (var i = 0; i < v.Count; i++)
        {
            if (v.Cue(i).Kind == MarkCueKind.Ash) continue;
            var main = v.CueVolume(i);
            var a = Enumerable.Range(0, v.Count).First(k => v.Cue(k).Kind == MarkCueKind.Ash && v.Cue(k).TakeHoldMs == v.Cue(i).TakeHoldMs
                                                             && v.Cue(k).Slot == v.Cue(i).Slot);
            Assert.True(Db(v.Cue(i).Kind, main) - Db(MarkCueKind.Ash, v.CueVolume(a)) >= Brand.AshUnderMainDb - 0.001f, $"{v.Cue(i).Kind}");
            if (v.Cue(i).Kind == MarkCueKind.Infect) pairs++;
        }
        Assert.Equal(3, pairs);
    }

    // ── IT NEVER COSTS THE FRAME ─────────────────────────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static float Sweep(MarkVoice v)
    {
        var sink = 0f;
        for (var p = 0f; p < 20000f; p += Frame)
            for (var i = v.CueDue(p); i >= 0; i = v.CueDue(p, i + 1))
                sink += v.CueVolume(i, ducked: (i & 1) == 0, performing: (i & 2) == 0) + v.Cue(i).Pitch + 1f;
        return sink;
    }

    [Fact]
    public void test_voicing_the_mark_on_the_playhead_allocates_nothing()
    {
        var v = Voice(Sprawl(new[] { (2000f, 35, false), (4000f, 170, false), (6000f, 220, false) }, (7000f, 0), (9000f, 1)),
                      burn: 0.5f, snaps: new[] { 4300f }, quiet: new[] { 6000f });
        var sink = Sweep(v) + Sweep(v);   // warm every method on the path (tiering); a sweep from 0 re-arms every cue
        var least = long.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            sink += Sweep(v);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0L, least);
        Assert.True(sink > 0f);
    }

    // ── THE SCREEN ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_screen_voices_the_mark_beside_press_never_lead_never_downed()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        // both update paths (the fight, and the break while an action or the curse's leaving plays), after the duck
        Assert.Equal(2, hunt.Split("VoiceMark();").Length - 1);
        Assert.Equal(2, Regex.Matches(hunt, @"VoiceField\(\);\s+VoiceMark\(\);").Count);
        var start = hunt.IndexOf("private void VoiceMark()", StringComparison.Ordinal);
        Assert.True(start > 0);
        var body = hunt.Substring(start, hunt.IndexOf("private static float Pan(", start, StringComparison.Ordinal) - start);
        Assert.Contains("if (_mark is not { } m || _markVoice is not { } v || _mode == Mode.Downed) return;", body);
        Assert.Contains("Sound?.PlayFirst(m.Recipe.CuesOf(c.Kind), volume, c.Pitch, Pan(cx, m.Recipe.CuePanWidth), m.Recipe.CueVary,", body);
        Assert.Contains("throttle: !MarkRecipe.Unthrottled(c.Kind));", body);
        Assert.DoesNotContain("lead", body);
        Assert.Contains("var volume = v.CueVolume(i, ducked, performing, Sound?.Duck ?? 1f);", body);
        // the bank's throttle is reported back for each cue, after it is played (the accent follows a dropped cue)
        Assert.Contains("v.Heard(i, cue is null ? 0f : Sound!.LastThrottleShare);", body);
        Assert.True(body.IndexOf("v.Heard(i,", StringComparison.Ordinal) > body.IndexOf("Sound?.PlayFirst(", StringComparison.Ordinal));
        var bank = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "SoundBank.cs"));
        Assert.Contains("{ LastThrottleShare = 0f; return; }", bank);
        Assert.Contains("LastThrottleShare = 1f / MathF.Sqrt(recent);", bank);
        Assert.Contains("PresentTrace.Log(\"mark-cue\"", body);
        // scheduled once per wave, dropped with the mark (RH_MARK_RECIPES=0: no mark, no voice)
        Assert.Equal(1, hunt.Split("_markVoice = new MarkVoice(").Length - 1);
        Assert.Contains("_markVoice = null;", hunt);
        // the film soundtrack replays the throttle: every key the game plays unthrottled is exempt there too (since the
        // sweep's P0.2 film_audio.py reads the generated sound_throttle.json, whose "unthrottled" list is pinned here)
        var table = File.ReadAllText(RepoFile("tools", "asset-pipeline", "sound_throttle.json"));
        var unthrottled = Regex.Match(table, @"""unthrottled"": \[([^\]]*)\]").Groups[1].Value;
        Assert.Contains("sound_throttle.json", File.ReadAllText(RepoFile("tools", "asset-pipeline", "film_audio.py")));
        foreach (var kind in Enum.GetValues<MarkCueKind>())
            foreach (var key in Brand.CuesOf(kind))
                Assert.Equal(MarkRecipe.Unthrottled(kind), unthrottled.Contains($"\"{key}\""));
    }

    // ── THE APPROVED BYTES (Candidate A, SUBTLE / INTERNAL) ───────────────────────────────────────────────────────────

    /// <summary>
    /// HUMAN-APPROVED (the owner, 2026-10-02: BRAND audio Candidate A, SUBTLE / INTERNAL, is canonical; B and C are
    /// rejected and archive-only): these are the bytes the owner listened to. A different file is a new approval, never a
    /// regeneration.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ApprovedCues = new Dictionary<string, string>
    {
        ["sfx_seeker_brand_apply"] = "87bc3b624e3171f31959c8e622927f68c921ec0ffc45ac8274c0d646c6fe4779",
        ["sfx_seeker_brand_deepen"] = "688ea4eac54d971c522d1cf7eabd502bce44699e1dc25daea5bbcb3fd2af46af",
        ["sfx_seeker_brand_deepen_deep"] = "a82ec0eb948db46d6cc0c7c354c7daf8792542aae2acb61e56fc1b89b54d4fc2",
        ["sfx_seeker_brand_infect"] = "48cc643de86f34e4e492598949d3fe77ade4520f5b924888125a25184d0b819f",
        ["sfx_seeker_brand_leave"] = "696361cf23e8d8ae1011eeb787da3348ae2e92c80494e014f8d292944880f3b0",
        ["sfx_seeker_brand_awaken"] = "81e8cc582fa9149b6652441b7b506d06f06e34906a59ba92fb04b5164744e05c",
        ["sfx_seeker_brand_ash"] = "bb771c68e3afc3795788bc8aae01fa53011dc92620c6c02c717846678acf2b34",
    };

    private static string Sha256Of(string path)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static System.Text.Json.JsonElement Manifest()
    {
        var path = RepoFile("tools", "asset-pipeline", "foley", "brand_curse", "brand_audio_manifest.json");
        Assert.True(File.Exists(path), "the BRAND audio provenance manifest is missing");
        return System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    [Fact]
    public void test_brand_audio_the_seven_shipped_cues_are_the_approved_candidate_a_bytes()
    {
        // every key the recipe can ask for is one of the seven approved files, and each ships with its approved bytes
        var asked = Enum.GetValues<MarkCueKind>().SelectMany(Brand.CuesOf).Distinct().OrderBy(k => k).ToArray();
        Assert.Equal(ApprovedCues.Keys.OrderBy(k => k).ToArray(), asked);
        foreach (var (key, sha) in ApprovedCues)
        {
            var wav = RepoFile("assets", "audio", "combat", key + ".wav");
            Assert.True(File.Exists(wav), $"{key} is missing");
            Assert.Equal(sha, Sha256Of(wav));
            // the review archive's A is the same file the game plays (the owner heard A from there)
            Assert.Equal(sha, Sha256Of(RepoFile("tools", "asset-pipeline", "audio_history", "brand_curse", "A", key + ".wav")));
        }
    }

    [Fact]
    public void test_brand_audio_the_manifest_records_the_pinned_cues_and_every_excerpt_candidate_a_reads()
    {
        var m = Manifest();
        Assert.Equal("A", m.GetProperty("candidate").GetString());
        Assert.Equal("2026-10-02", m.GetProperty("approved").GetString());
        Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("approval_note").GetString()));
        Assert.Equal("CC0 1.0", m.GetProperty("licence").GetProperty("name").GetString());
        Assert.Equal("tools/asset-pipeline/make_brand_cues.py", m.GetProperty("generator").GetProperty("path").GetString());
        Assert.Matches("^[0-9a-f]{64}$", m.GetProperty("generator").GetProperty("sha256").GetString()!);
        Assert.Equal(new[] { "tools/asset-pipeline/make_jaws_bite.py", "tools/asset-pipeline/make_press_tick.py" },
                     m.GetProperty("shared_dsp").EnumerateArray().Select(d => d.GetProperty("path").GetString()).ToArray());
        // the manifest's cue hashes are the pinned ones, and the files on disk
        var cues = m.GetProperty("shipped_cues").EnumerateArray()
                    .ToDictionary(c => c.GetProperty("key").GetString()!, c => (Path: c.GetProperty("path").GetString()!, Sha: c.GetProperty("sha256").GetString()!));
        Assert.Equal(ApprovedCues.Keys.OrderBy(k => k), cues.Keys.OrderBy(k => k));
        foreach (var (key, sha) in ApprovedCues)
        {
            Assert.Equal($"assets/audio/combat/{key}.wav", cues[key].Path);
            Assert.Equal(sha, cues[key].Sha);
            Assert.Equal(sha, Sha256Of(RepoFile(cues[key].Path.Split('/'))));
        }
        // every excerpt Candidate A reads exists with the bytes it was built from, and is CC0 with a Freesound page
        var excerpts = m.GetProperty("excerpts").EnumerateArray().ToList();
        Assert.NotEmpty(excerpts);
        foreach (var e in excerpts)
        {
            var path = e.GetProperty("path").GetString()!;
            var file = RepoFile(path.Split('/'));
            Assert.True(File.Exists(file), $"{path} is missing");
            Assert.Equal(e.GetProperty("sha256").GetString(), Sha256Of(file));
            Assert.Equal("CC0 1.0", e.GetProperty("licence").GetString());
            Assert.StartsWith("https://freesound.org/people/", e.GetProperty("freesound_page").GetString());
            Assert.NotEmpty(e.GetProperty("used_by_cues").EnumerateArray());
            Assert.All(e.GetProperty("used_by_cues").EnumerateArray(), k => Assert.Contains(k.GetString()!, ApprovedCues.Keys));
        }
        // and SOURCES.md answers the same question: every A excerpt is listed under SHIPPED
        var sources = File.ReadAllText(RepoFile("tools", "asset-pipeline", "foley", "brand_curse", "SOURCES.md"));
        var shipped = sources.Substring(0, sources.IndexOf("## ARCHIVE ONLY", StringComparison.Ordinal));
        Assert.Contains("## SHIPPED (Candidate A)", shipped);
        foreach (var e in excerpts)
            Assert.Contains($"`{e.GetProperty("name").GetString()}.wav`", shipped);
    }

    [Fact]
    public void test_brand_audio_no_rejected_candidate_file_ships_anywhere_under_assets_audio()
    {
        var history = RepoFile("tools", "asset-pipeline", "audio_history", "brand_curse");
        var rejected = new[] { "B", "C" }
            .SelectMany(c => Directory.GetFiles(Path.Combine(history, c), "*", SearchOption.AllDirectories))
            .ToDictionary(Sha256Of, f => f);
        Assert.Equal(14, rejected.Count);                                                 // 7 + 7, all distinct
        foreach (var file in Directory.GetFiles(RepoFile("assets", "audio"), "*", SearchOption.AllDirectories))
            Assert.False(rejected.TryGetValue(Sha256Of(file), out var copy), $"{file} is a byte copy of the rejected {copy}");
    }

    [Fact]
    public void test_brand_audio_the_game_has_no_candidate_selector()
    {
        // the game plays the one approved set: no source string names an archived candidate, and no environment variable
        // chooses BRAND's audio (RH_MARK_RECIPES only switches the whole mark off, never its sound)
        var literal = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
        var env = new Regex("GetEnvironmentVariable\\(\\s*\"([^\"]+)\"");
        foreach (var file in Directory.GetFiles(RepoFile("src"), "*.cs", SearchOption.AllDirectories))
        {
            var code = File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)).ToArray();
            foreach (var line in code)
            {
                foreach (Match s in literal.Matches(line))
                {
                    var text = s.Value.Replace('\\', '/');
                    Assert.DoesNotContain("brand_curse/B", text);
                    Assert.DoesNotContain("brand_curse/C", text);
                    Assert.DoesNotContain("audio_history", text);
                    Assert.False(text.Contains("candidate", StringComparison.OrdinalIgnoreCase), $"{Path.GetFileName(file)}: {line.Trim()}");
                }
                foreach (Match e in env.Matches(line))
                {
                    var name = e.Groups[1].Value.ToUpperInvariant();
                    Assert.False(name.Contains("CANDIDATE") || Regex.IsMatch(name, "(BRAND|CURSE|MARK).*(AUDIO|CUE|SOUND|SFX)|(AUDIO|CUE|SOUND|SFX).*(BRAND|CURSE|MARK)"),
                                 $"{Path.GetFileName(file)} reads {name}");
                }
            }
        }
        // the generator installs only the approved letter; B and C build into the archive alone
        var make = File.ReadAllText(RepoFile("tools", "asset-pipeline", "make_brand_cues.py"));
        Assert.Contains("APPROVED = \"A\"", make);
        Assert.Contains("ARCHIVE = (\"B\", \"C\")", make);
        Assert.DoesNotContain("SHIPPED = ", make);
    }
}
