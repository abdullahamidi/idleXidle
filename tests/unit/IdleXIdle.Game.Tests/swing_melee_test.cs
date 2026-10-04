using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE MELEE BASIC ATTACKS, PERFORMED (design.md 5.18-5.27, vfx sweep Phase 1 / P1.3): the Seeker, the Anvil, the
/// Metronome, the Tower, the Thornwall and the Magpie swing their own authored <c>attack</c> strip, the contact frame ON
/// THE BEAT, stepping in 20-25 % of the row gap as a draw offset (never root motion), with one contact cue and a small
/// contact picture at the creature. THE PLAIN ENVELOPE OWNS TIME (start, speed, handoff, yield, end are the old plain
/// clip's), THE AUTHORED FILE OWNS THE FRAMES (<see cref="SwingClock"/>).
/// </summary>
public class swing_melee_test
{
    private const float ClipMs = 1000f;           // HuntScreen.ClipMs: eight frames at the champion's 8 fps
    private const float ShareOfBeat = 0.55f;      // HuntScreen.ClipShareOfBeat (the swing's)
    private const float MaxClipSpeed = 1.5f;      // HuntScreen.MaxClipSpeed
    private const float SettleCeiling = 300f;     // HuntScreen.ClipSettleMs
    private const float Frame = 1000f / 60f;

    /// <summary>The six melee basics: champion, anchor frame, the step-in share design.md gives it, its archetype cue.</summary>
    public static IEnumerable<object[]> Melee() => new[]
    {
        new object[] { "seeker", 4, 0.25f, "sfx_blade_hit" },
        new object[] { "anvil", 5, 0.20f, "sfx_fist_hit" },
        new object[] { "metronome", 2, 0.25f, "sfx_fist_hit" },
        new object[] { "tower", 5, 0.20f, "sfx_stone_hit" },
        new object[] { "thornwall", 3, 0.20f, "sfx_wood_hit" },
        new object[] { "magpie", 3, 0.25f, "sfx_blade_hit" },
    };

    /// <summary>Three TEMPOs: the beat's period (slow, the default 1500, the ref_fast TEMPO's 792 ms).</summary>
    public static readonly float[] Tempos = { 2500f, SoloBattle.DefaultBeatMs, 792f };

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static ActionClipTiming Timing(string id)
        => ActionClipTiming.Parse(File.ReadAllText(RepoFile("assets", "art", "Animations", "Roster", $"{id}_attack", $"char_{id}_attack_strip8_512.clip.json")));

    private static string Hunt() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));

    /// <summary>
    /// The plain envelope exactly as HuntScreen commits a swing (UpdateChampionClip / CommitPlainClip): it starts one
    /// contact-length before the beat (or later, <paramref name="lateLeadMs"/> before it, faster up to MaxClipSpeed), eight
    /// equal frames, the settle clamped to what the beat leaves.
    /// </summary>
    private static (float Start, ActionClipTiming Plain) Envelope(float period, int beat, float? lateLeadMs = null)
    {
        var baseSpeed = Math.Max(0.6f, ClipMs / (period * ShareOfBeat));
        var contactMs = ClipMs * 5f / 8f / baseSpeed;
        var lead = lateLeadMs ?? contactMs;
        var speed = Math.Clamp(baseSpeed * contactMs / Math.Max(1f, lead), baseSpeed, Math.Max(baseSpeed, MaxClipSpeed));
        var settle = Math.Min(SettleCeiling, Math.Max(0f, period - ClipMs / speed));
        return (beat - lead, ActionClipTiming.Plain(ClipMs / 8f / speed, settle));
    }

    // ── THE TABLE ────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Melee))]
    public void test_each_melee_swing_recipe_respects_the_t1_ceilings(string id, int anchor, float share, string archetype)
    {
        var r = SwingRecipes.For(id);
        Assert.NotNull(r);
        Assert.Equal($"{id}.swing", r!.Id);
        Assert.Equal(SwingKind.StepIn, r.Kind);
        Assert.Equal("attack", r.ClipKey);
        Assert.Equal("contact", r.AnchorMarker);
        Assert.Equal("StrikeHand", r.HandSocket);
        Assert.Equal(anchor, Timing(id).Markers[r.AnchorMarker]);
        Assert.Equal(share, r.StepInShare, 3);
        Assert.InRange(r.StepInShare, 0.20f, 0.25f);
        // T1 Ordinary (design.md section 3): hit cue 0.34-0.38, flash 0.30 / 110 ms with no rise, impact <= 0.35 of the
        // creature's height, gone within 250 ms; no release cue for a blow, no travel
        Assert.Equal(ActionWeight.Ordinary, r.Weight);
        Assert.InRange(r.ContactVolume, 0.34f, 0.38f);
        Assert.Equal(0.30f, r.TargetFlash, 3);
        Assert.Equal(110f, r.TargetFlashMs, 3);
        Assert.Equal(0f, r.TargetFlashRise);
        Assert.InRange(r.Impact.Extent, 0.05f, 0.35f);
        Assert.InRange(r.Impact.LifeMs, 1f, 250f);
        Assert.Empty(r.ReleaseCues);
        Assert.Equal(0f, r.TravelMs);
        Assert.InRange(r.ContactPoint.X, 0f, 1f);
        Assert.InRange(r.ContactPoint.Y, 0f, 1f);
        // the resolution chain: the champion's own cue, its archetype, the generic (design.md section 7)
        Assert.Equal(new[] { $"sfx_{id}_swing_hit", archetype, "sfx_hit" }, r.ContactCues);
        Assert.InRange(r.Impact.Brightness, 0.3f, 0.9f);   // quiet: a basic attack is never the brightest thing on screen
    }

    [Fact]
    public void test_each_champions_contact_picture_is_the_one_design_md_names()
    {
        var seeker = SwingRecipes.Seeker.Impact;
        Assert.Equal(0.30f, seeker.SlashLength, 3);
        Assert.Equal(2, seeker.Slivers);
        Assert.Equal(0f, seeker.FlashSize);
        var anvil = SwingRecipes.Anvil.Impact;
        Assert.True(anvil.FlashSize > 0f && anvil.FlashSquash < 1f && anvil.RingSize > 0f && anvil.RingSquash < 0.5f, "a compressed flash + a small flat ring");
        var metronome = SwingRecipes.Metronome.Impact;
        Assert.True(metronome.FlashSize > 0f && metronome.FlashSquash < 1f && metronome.Sparks == 2, "a compressed flash + 2 sparks");
        var tower = SwingRecipes.Tower.Impact;
        Assert.Equal(0.35f, tower.RingSize, 3);
        Assert.True(tower.DustSize > 0f && tower.RingSquash < 0.5f, "dust + a flat ring of 0.35");
        var thornwall = SwingRecipes.Thornwall.Impact;
        Assert.Equal(0.35f, thornwall.FlashSize, 3);
        Assert.InRange(thornwall.FlashMs, 2f * Frame - 0.5f, 2f * Frame + 0.5f);   // for 2 frames
        var magpie = SwingRecipes.Magpie.Impact;
        Assert.Equal(1, magpie.Slivers);
        Assert.True(magpie.SlashLength > 0f && magpie.SlashLength < seeker.SlashLength, "a thin slash, smaller than the Seeker's cut");
        Assert.True(magpie.SlashMs < seeker.SlashMs && magpie.Brightness > seeker.Brightness, "the fastest profile, a bright nick");
        // the missiles and the reach arrived in P1.4 (swing_missile_test, swing_reach_test): all ten perform, and none of
        // those four steps in
        foreach (var later in new[] { "quiver", "chorus", "unbroken", "oathbound" }) Assert.NotEqual(SwingKind.StepIn, SwingRecipes.For(later)!.Kind);
        Assert.Equal(10, SwingRecipes.All.Count);
    }

    [Fact]
    public void test_swing_recipes_never_resolve_a_skill_and_no_skill_resolves_the_attack_clip()
    {
        // a basic attack is not a SkillDef: no skill id is a key of the swing tier...
        foreach (var def in SkillCatalogue.All) Assert.Null(SwingRecipes.For(def.Id));
        // ...and the action tier still resolves nothing for the attack clip, on any champion, whatever the effect key
        foreach (var c in CharacterRoster.All)
        {
            foreach (var def in SkillCatalogue.All)
                Assert.Null(ActionRecipes.For(c.Id, "form:attack", "attack", def.FxKey));
            Assert.Null(ActionRecipes.For(c.Id, "attack", "attack", "attack"));
        }
        // gated by ActionRecipes.Enabled alone: no switch of its own
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "SwingRecipe.cs"));
        Assert.Contains("=> ActionRecipes.Enabled && ByCharacter.TryGetValue(characterId, out var recipe) ? recipe : null;", src);
        Assert.DoesNotContain("GetEnvironmentVariable", src);
    }

    // ── THE CLOCK ────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Melee))]
    public void test_the_contact_frame_lands_on_the_beat_at_every_tempo_and_on_a_late_start(string id, int anchor, float share, string archetype)
    {
        _ = share;
        _ = archetype;
        var t = Timing(id);
        const int beat = 5000;
        var cases = Tempos.Select(p => Envelope(p, beat)).Append(Envelope(SoloBattle.DefaultBeatMs, beat, lateLeadMs: 60f)).ToList();
        foreach (var (start, plain) in cases)
        {
            var exit = start + plain.TotalMs;
            Assert.Equal(beat, SwingClock.FrameStart(t, anchor, start, beat, exit, anchor));
            Assert.Equal(anchor, SwingClock.FrameAt(t, anchor, start, beat, exit, beat));
            Assert.True(SwingClock.FrameAt(t, anchor, start, beat, exit, beat - 0.5f) < anchor, $"{id}: the contact frame shows before the beat");
            // the authored frames fill exactly the plain envelope: the clip-start and clip-end lines cannot move
            Assert.Equal(0, SwingClock.FrameAt(t, anchor, start, beat, exit, start));
            Assert.Equal(exit, SwingClock.FrameStart(t, anchor, start, beat, exit, t.Frames), 2);
            // and play forward only, every frame shown in order
            var last = 0;
            for (var ms = start; ms < exit; ms += 1f)
            {
                var f = SwingClock.FrameAt(t, anchor, start, beat, exit, ms);
                Assert.True(f >= last, $"{id}: frame {f} after {last} at {ms}");
                last = f;
            }
        }
    }

    [Fact]
    public void test_spare_time_goes_to_the_elastic_wind_up_and_the_settle_holds_after_the_contact()
    {
        var t = Timing("seeker");   // 80 80 80 70 | 90 80 90 90, elastic 0 1 6 7, contact 4
        const int beat = 5000;
        // a slow tempo's 1000 ms before the beat: the rigid commit frames keep their length, the elastic wind-up stretches
        var start = beat - 1000f;
        var exit = beat + 800f;
        Assert.Equal(80f, SwingClock.FrameMs(t, 4, start, beat, exit, 2), 3);
        Assert.Equal(70f, SwingClock.FrameMs(t, 4, start, beat, exit, 3), 3);
        Assert.Equal(425f, SwingClock.FrameMs(t, 4, start, beat, exit, 0), 3);   // 80 + 690 x 80 / 160
        // after the contact: the authored pace, the spare held on the settle frame
        Assert.Equal(90f, SwingClock.FrameMs(t, 4, start, beat, exit, 4), 3);
        Assert.Equal(800f - 90f - 80f - 90f, SwingClock.FrameMs(t, 4, start, beat, exit, 7), 3);
        // a fast tempo's 150 ms: the elastic wind-up gives way first, the commit frames last (150 = 80 + 70)
        Assert.Equal(0f, SwingClock.FrameMs(t, 4, beat - 150f, beat, exit, 0), 3);
        Assert.Equal(80f, SwingClock.FrameMs(t, 4, beat - 150f, beat, exit, 2), 3);
        // a squeezed recovery: the follow-through keeps its pace, the elastic recovery shrinks
        Assert.Equal(90f, SwingClock.FrameMs(t, 4, start, beat, beat + 200f, 4), 3);
        Assert.Equal(80f, SwingClock.FrameMs(t, 4, start, beat, beat + 200f, 5), 3);
        Assert.Equal(15f, SwingClock.FrameMs(t, 4, start, beat, beat + 200f, 6), 3);
    }

    // ── THE STEP-IN ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Melee))]
    public void test_the_step_in_peaks_on_the_contact_and_is_home_by_the_exit_for_every_fit(string id, int anchor, float share, string archetype)
    {
        _ = archetype;
        var r = SwingRecipes.For(id)!;
        var t = Timing(id);
        const int beat = 5000;
        const float gap = 400f;
        foreach (var period in Tempos)
        {
            var (start, plain) = Envelope(period, beat);
            var perf = new SwingPerformance();
            perf.Begin(r, t, start, beat, start + plain.TotalMs, gap, 1);
            Assert.Equal(share * gap, perf.PeakPx, 3);
            Assert.Equal(0f, perf.StepPx(start));                                                   // 0 at the commit of the swing
            Assert.Equal(0f, perf.StepPx(SwingClock.FrameStart(t, anchor, start, beat, perf.ExitMs, t.Markers["commit"])));   // ...and to the commit frame
            Assert.Equal(share * gap, perf.StepPx(beat), 3);                                         // the peak ON the contact
            Assert.True(perf.StepPx(beat - 10f) < perf.StepPx(beat), "the step accelerates into the hit");
            Assert.True(perf.StepPx(beat - 10f) > 0f);
            Assert.Equal(0f, perf.StepPx(SwingClock.FrameStart(t, anchor, start, beat, perf.ExitMs, t.Markers["settle"])));   // home by the settle

            // EVERY FIT the handoff can make of the envelope: Natural (nothing borrowed), Floor / Compressed (the recovery
            // squeezed), Cut (a yield right after the contact). Home, 0, from two display frames before the planned exit on.
            var fits = new[]
            {
                plain.FitRecovery(10_000f, 2000f / 60f, out _),
                plain.FitRecovery(40f, 2000f / 60f, out _),
                plain.FitRecovery(plain.RecoveryMotionMs * 0.6f, 2000f / 60f, out _),
                plain.CutAt(beat - start + 25f),
                plain.CutAt(beat - start + 1f),
            };
            foreach (var fit in fits)
            {
                var exit = start + fit.TotalMs;
                perf.Retime(exit);
                Assert.Equal(share * gap, perf.StepPx(beat), 3);
                // (a Cut a hair after the contact leaves no margin: home from the first instant after the beat)
                foreach (var at in new[] { Math.Max(exit - SwingClock.HomeMarginMs, beat + 0.5f), exit, exit + 1f, exit + 500f })
                    Assert.Equal(0f, perf.StepPx(at));
                for (var ms = start; ms < exit + 50f; ms += 1f) Assert.InRange(perf.StepPx(ms), 0f, share * gap + 0.01f);
            }
            perf.EndClip();
            Assert.Equal(0f, perf.StepPx(beat));   // a clip off the figure carries nothing
        }
    }

    [Fact]
    public void test_a_planted_stance_power_of_three_holds_the_stance_and_lands_the_translation_under_the_hit()
    {
        // the Phase 1 review: the Thornwall's (and the Anvil's) wide planted stance translated over the whole wind-up read
        // as a slide; u^3 keeps it near home and puts the move into the last ~100 ms; the peak and home are unchanged
        Assert.Equal(3f, SwingRecipes.Thornwall.StepInPower);
        Assert.Equal(3f, SwingRecipes.Anvil.StepInPower);
        foreach (var other in new[] { SwingRecipes.Seeker, SwingRecipes.Metronome, SwingRecipes.Tower, SwingRecipes.Magpie })
            Assert.Equal(2f, other.StepInPower);
        var t = Timing("thornwall");
        const int beat = 5000;
        const float gap = 400f;
        var (start, plain) = Envelope(SoloBattle.DefaultBeatMs, beat);
        var planted = new SwingPerformance();
        planted.Begin(SwingRecipes.Thornwall, t, start, beat, start + plain.TotalMs, gap, 1);
        var eased = new SwingPerformance();
        eased.Begin(SwingRecipes.Thornwall with { StepInPower = 2f }, t, start, beat, start + plain.TotalMs, gap, 1);
        Assert.Equal(0.20f * gap, planted.StepPx(beat), 3);   // the same peak ON the contact
        var commit = SwingClock.FrameStart(t, 3, start, beat, planted.ExitMs, t.Markers["commit"]);
        var mid = (commit + beat) / 2f;
        Assert.True(planted.StepPx(mid) < eased.StepPx(mid), "the planted stance holds longer");
        Assert.InRange(planted.StepPx(mid), 0f, 0.20f * gap * 0.13f);   // (1/2)^3 of the peak at the wind-up's middle
        // most of the translation lands in the last ~100 ms before the contact
        Assert.True(planted.StepPx(beat) - planted.StepPx(beat - 100f) > 0.5f * planted.StepPx(beat));
        // the recovery is unchanged by the power
        Assert.Equal(eased.StepPx(beat + 60f), planted.StepPx(beat + 60f), 3);
    }

    [Fact]
    public void test_missiles_and_reaches_never_step_and_a_negative_gap_steps_nowhere()
    {
        var t = Timing("seeker");
        var perf = new SwingPerformance();
        perf.Begin(SwingRecipes.Seeker with { Kind = SwingKind.Missile, TravelMs = 200f }, t, 4000f, 5000, 6000f, 400f, 1);
        Assert.Equal(0f, perf.PeakPx);
        Assert.Equal(4800f, perf.AnchorAtMs);   // a missile's release is one flight before the beat
        perf.Begin(SwingRecipes.Seeker with { Kind = SwingKind.Reach }, t, 4000f, 5000, 6000f, 400f, 1);
        Assert.Equal(0f, perf.StepPx(5000f));
        perf.Begin(SwingRecipes.Seeker, t, 4000f, 5000, 6000f, -50f, 1);   // a champion wider than the row: no step at all
        Assert.Equal(0f, perf.StepPx(5000f));
    }

    // ── THE CONTACT ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_one_contact_cue_per_swing_ms_and_a_picture_on_each_struck_creature()
    {
        var perf = new SwingPerformance();
        perf.Begin(SwingRecipes.Anvil, Timing("anvil"), 4500f, 5000, 5800f, 300f, 2);
        Assert.True(perf.IsBeat(5000));
        Assert.False(perf.IsBeat(5001));
        Assert.True(perf.Contact(5000, new Rectangle(1100, 600, 260, 250)));    // the swing ms's first blow: voiced
        Assert.False(perf.Contact(5000, new Rectangle(1400, 600, 260, 250)));   // a second creature: its picture, no cue
        Assert.True(perf.Drawing(5010f));
        var light = perf.Compose(5010f, light: true);
        Assert.Equal(4, light);                                                  // a flash + a ring on each
        Assert.All(perf.Sprites.ToArray(), s => Assert.True(s.Part is SwingPart.Flash or SwingPart.Ring));
        // the picture is AT the creature: the contact point of its body, and no bigger than 0.35 of its height
        var flash = perf.Sprites.ToArray().First(s => s.Part == SwingPart.Flash);
        Assert.Equal(1100f + 260f * SwingRecipes.Anvil.ContactPoint.X, flash.At.X, 2);
        Assert.True(Math.Max(flash.Size.X, flash.Size.Y) <= 0.35f * 250f + 0.01f);
        Assert.False(perf.Drawing(5000f + SwingRecipes.Anvil.Impact.LifeMs));
        // a new swing's beat voices again, and its picture does not cut the last one short
        perf.Begin(SwingRecipes.Anvil, Timing("anvil"), 5100f, 5600, 6300f, 300f, 1);
        Assert.True(perf.Contact(5600, new Rectangle(1100, 600, 260, 250)));
        Assert.Equal(0, perf.Compose(5600f, light: false));                     // the Anvil raises no dust
        perf.Begin(SwingRecipes.Tower, Timing("tower"), 5700f, 6200, 6900f, 300f, 1);
        Assert.True(perf.Contact(6200, new Rectangle(1100, 600, 260, 250)));
        Assert.Equal(1, perf.Compose(6230f, light: false));                     // the Tower's dust, a material
        Assert.Equal(SwingPart.Dust, perf.Sprites[0].Part);
        Assert.Equal("fxp_dust_soft", SwingPerformance.KeyOf(SwingPart.Dust));
        perf.Reset();
        Assert.False(perf.Drawing(6230f));
        Assert.False(perf.IsBeat(6200));
    }

    [Fact]
    public void test_a_rewound_replay_voices_an_already_voiced_swing_ms_again()
    {
        // the rig's seek rebuilds the replay and re-crosses a swing the live run already voiced: ForgetVoiced lets it voice
        var perf = new SwingPerformance();
        var body = new Rectangle(1100, 600, 260, 250);
        perf.Begin(SwingRecipes.Anvil, Timing("anvil"), 200f, 700, 1300f, 300f, 1);
        Assert.True(perf.Contact(700, body));
        Assert.False(perf.Contact(700, body));    // without the rewind: the same ms stays silent
        perf.ForgetVoiced();
        Assert.True(perf.Contact(700, body));     // after it: voiced (and traced) once more
        Assert.False(perf.Contact(700, body));
    }

    [Fact]
    public void test_swung_at_names_only_the_swings_own_strikes()
    {
        var events = new List<BattleEvent>
        {
            new(BattleEventKind.Strike, 0, 50, 4000, HitSource.Swing),
            new(BattleEventKind.Skill, 1, 0, 5000),
            new(BattleEventKind.Strike, 1, 80, 5000, HitSource.Primary),
            new(BattleEventKind.Strike, 2, 40, 5000, HitSource.Swing),
            new(BattleEventKind.Strike, 2, 40, 5000, HitSource.Swing),
            new(BattleEventKind.Strike, 3, 10, 5000, HitSource.Carry),
            new(BattleEventKind.Strike, 0, 40, 5000, HitSource.Swing),
        };
        var into = new int[SwingPerformance.MaxTargets];
        Assert.Equal(2, ActionTargets.SwungAt(events, 5000, into));
        Assert.Equal(2, into[0]);
        Assert.Equal(0, into[1]);
        Assert.Equal(0, ActionTargets.SwungAt(events, 4500, into));
    }

    [Fact]
    public void test_two_hundred_swings_of_thirty_frames_allocate_nothing()
    {
        var recipes = SwingRecipes.All.Values.ToArray();
        var timings = recipes.Select(r => Timing(r.Id[..r.Id.IndexOf('.')])).ToArray();
        var perf = new SwingPerformance();
        var body = new Rectangle(1100, 600, 260, 250);
        // warm every path once
        perf.Begin(recipes[0], timings[0], 0f, 400, 900f, 300f, 1);
        perf.Contact(400, body);
        perf.Compose(410f, true);
        perf.Compose(410f, false);
        var sink = 0f;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var s = 0; s < 200; s++)
        {
            var k = s % recipes.Length;
            var start = 1000f * s;
            var beat = (int)start + 450;
            perf.Begin(recipes[k], timings[k], start, beat, start + 800f, 300f + s, 1);
            for (var f = 0; f < 30; f++)
            {
                var ms = start + f * Frame;
                if (f == 20) perf.Retime(start + 700f);
                sink += perf.StepPx(ms) + perf.FrameAt(ms);
                if (perf.IsBeat((int)ms) || (ms >= beat && ms - Frame < beat)) perf.Contact(beat, body);
                sink += perf.Compose(ms, light: false) + perf.Compose(ms, light: true);
                foreach (var sprite in perf.Sprites) sink += sprite.Size.X;
                sink += perf.Drawing(ms) ? 1f : 0f;
            }
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sink > 0f);
    }

    // ── THE SCREEN ───────────────────────────────────────────────────────────────────────────────────────────────

    private static string StrikeCase()
    {
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        return strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
    }

    [Fact]
    public void test_a_swings_own_blow_takes_no_generic_thud_no_puff_no_push_and_the_t1_flash()
    {
        var strike = StrikeCase();
        Assert.Contains("var swingHit = e.Hit == HitSource.Swing && _swing.IsBeat(e.AtMs);", strike);
        // the contact voices BEFORE the thud line and marks the ms voiced: the generic sfx_hit's one-line shape is untouched
        var contact = strike.IndexOf("if (swingHit) SwingContact(e);", StringComparison.Ordinal);
        Assert.True(contact >= 0);
        Assert.True(contact < strike.IndexOf("var voicedThisMs = e.AtMs == _hitVoicedAtMs;", StringComparison.Ordinal));
        Assert.Contains("if (!auraTick && !quietHit && !voicedThisMs && !performedHit && !reactionHit) Sound?.Play(\"sfx_hit\"", strike);
        // no puff (its turn still counted), the legacy 40 px push only off the swing path, the T1 flash
        Assert.Contains("if (!auraTick && !quietHit && !performedHit && !reactionHit && (_strikeCount++ & 1) == 0)", strike);
        Assert.Contains("if (!swingHit) PlayFx(VfxProfiles.ImpactWeak, VfxSubject.Creature(e.Slot), Steel);", strike);
        Assert.Contains("if (!e.FromSkill && !swingHit) _champLunge = 1f;", strike);
        Assert.Contains("? (_swing.Recipe!.TargetFlash, _swing.Recipe.TargetFlashRise, 1000f / Math.Max(1f, _swing.Recipe.TargetFlashMs))", strike);
        // ONE cue per swing ms, through the chain, never lead (it takes a duck, it never makes one), no callout
        var src = Hunt();
        var voice = src[src.IndexOf("private void SwingContact(BattleEvent e)", StringComparison.Ordinal)..];
        voice = voice[..voice.IndexOf("private void DrawSwing(", StringComparison.Ordinal)];
        Assert.Contains("if (!_swing.Contact(e.AtMs, body)) return;", voice);
        Assert.Contains("Sound?.PlayFirst(recipe.ContactCues, recipe.ContactVolume, 0f, Pan(body.Center.X, 0.3f), 0.05f);", voice);
        Assert.Contains("_hitVoicedAtMs = e.AtMs;", voice);
        Assert.DoesNotContain("lead: true", voice);
        Assert.DoesNotContain("Say(", voice);
        Assert.DoesNotContain("Duck", voice);
        Assert.DoesNotContain("\"sfx_hit\"", voice);
    }

    [Fact]
    public void test_the_swing_commits_inside_the_plain_envelope_and_its_step_never_rides_the_root()
    {
        var src = Hunt();
        // the plain clip is committed exactly as before; the swing joins it, it never replaces it
        var commit = src.IndexOf("CommitPlainClip(clip!, _playheadMs,", StringComparison.Ordinal);
        var swing = src.IndexOf("if (clip == \"attack\" && next?.Cast is null) CommitSwing((int)beatMs.Value);", StringComparison.Ordinal);
        Assert.True(commit >= 0 && swing > commit, "the swing commits right after the plain clip");
        Assert.Contains("if (SwingRecipes.For(Character.Id) is not { } recipe || _clipTiming is not { } envelope) return;", src);
        Assert.Contains("ActionClipLibrary.For(key) is not { } timing", src);
        Assert.Contains("_swing.Begin(recipe, timing, _clipStartMs, beatMs, _clipStartMs + envelope.TotalMs, gap, targets);", src);
        Assert.Contains("gap = foe.Left - me.Right;", src);
        // the handoff's new exit reaches the swing (every fit)
        Assert.Contains("if (SwingOnFigure) _swing.Retime(_clipStartMs + _clipTiming.TotalMs);", src);
        // the figure takes the authored frame from the swing clock
        Assert.Contains("SwingOnFigure ? (_swing.FrameAt(_playheadMs) + 0.5f) / ChampionFps", src);
        // the step is added BESIDE the push: the root trace logs `push`, never the step
        Assert.Contains("var step = SwingOnFigure ? (int)MathF.Round(_swing.StepPx(_playheadMs)) : 0;", src);
        Assert.Contains("_champDrawBox = new Rectangle(ChampBox.X + push + step, ChampBox.Y + lift, ChampBox.Width, ChampBox.Height);", src);
        Assert.Contains("PresentTrace.Log(\"root\", $\"x={push}\\ty={lift}\");", src);
        Assert.Contains("PresentTrace.Log(\"swing-step\", $\"x={step}\");", src);
        Assert.Contains("_swing.Reset();", src);
    }

    [Fact]
    public void test_the_swing_draws_its_material_after_the_figures_and_its_light_in_the_light_pass()
    {
        var src = Hunt();
        int At(string s, int from = 0)
        {
            var i = src.IndexOf(s, from, StringComparison.Ordinal);
            Assert.True(i >= 0, $"missing: {s}");
            return i;
        }
        var champion = At("DrawChampion(b, _champDrawBox, dead: _mode == Mode.Downed);");
        var actionMaterial = At("if (!ShotNoVfx) _performance?.DrawMaterial(b);", champion);
        var material = At("if (!ShotNoVfx) DrawSwing(b, light: false);", actionMaterial);
        var effects = At("if (!ShotNoVfx) _vfx.DrawOver(b);", material);
        var begin = At("_vfx.BeginLight(b);", effects);
        var light = At("DrawSwing(b, light: true);", begin);
        var end = At("_vfx.EndLight(b);", light);
        Assert.True(champion < actionMaterial && actionMaterial < material && material < effects && effects < begin && begin < light && light < end);
        Assert.Contains("|| _field is not null || swingLit || healGlow > 0f))", src);
        Assert.Contains("PresentTrace.Log(\"swing-draw\", $\"sprites={_swingDrawSprites}\\talloc={_swingAllocBytes}\");", src);
        // light through ADR-009's blend; the material untinted
        Assert.Contains("light ? VfxBlend.Light(s.Color) : s.Color", src);
        // a wave-ending swing's contact plays out instead of freezing lit
        Assert.Contains("|| _swing.Drawing(_playheadMs)", src);
    }
}
