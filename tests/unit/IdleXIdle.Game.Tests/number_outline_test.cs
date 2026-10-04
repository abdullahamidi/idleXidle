using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Characters;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE SOURCE IN THE NUMBER (design.md section 8): an agnostic closed reference (JAWS / PRESS / BRAND on a champion other
/// than the Seeker) shows its slot's Source in its NUMBER's one-pixel, four-way outline, and nowhere else. The Seeker's
/// own closed numbers carry none. The outline is drawn in the existing number pass and allocates nothing.
/// </summary>
public class number_outline_test
{
    private static readonly Color Shadow = new(0x9B, 0x7B, 0xFF);

    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    /// <summary>A pass that records what the outline asked of it, with no allocation.</summary>
    private struct Recorder : IGlyphPass
    {
        public int Calls, SumX, SumY, OutlineCalls, LastX, LastY;
        public Color Last;
        public Color Outline;

        public void Glyphs(int x, int y, Color ink)
        {
            Calls++;
            SumX += x;
            SumY += y;
            if (ink == Outline) OutlineCalls++;
            LastX = x;
            LastY = y;
            Last = ink;
        }
    }

    [Fact]
    public void test_only_the_agnostic_tier_takes_an_outline()
    {
        Assert.Equal(Shadow, NumberOutline.For(RecipeTierKind.Agnostic, Shadow));
        Assert.Null(NumberOutline.For(RecipeTierKind.Own, Shadow));
        Assert.Null(NumberOutline.For(RecipeTierKind.None, Shadow));
    }

    [Theory]
    [InlineData(RecipeFamily.Reaction, "snare_jaws")]
    [InlineData(RecipeFamily.Field, "hammer_press")]
    [InlineData(RecipeFamily.Mark, "sign_brand")]
    public void test_the_seekers_closed_numbers_stay_bare_and_every_other_champions_are_outlined(RecipeFamily family, string skill)
    {
        foreach (var c in CharacterRoster.All)
        {
            var outline = NumberOutline.For(RecipeTier.Of(family, c.Id, skill), Shadow);
            if (c.Id == "seeker") Assert.Null(outline);
            else Assert.Equal(Shadow, outline);
        }
        // a skill with no closed reference never takes one
        Assert.Null(NumberOutline.For(RecipeTier.Of(family, "anvil", "snare_repay"), Shadow));
    }

    [Fact]
    public void test_the_outline_is_four_neighbours_then_the_glyphs_on_top()
    {
        var pass = new Recorder { Outline = Color.Red };
        NumberOutline.Draw(ref pass, 100, 50, Color.Red, Color.White);
        Assert.Equal(5, pass.Calls);
        Assert.Equal(4, pass.OutlineCalls);
        Assert.Equal(500, pass.SumX);   // 99 + 101 + 100 + 100 + 100
        Assert.Equal(250, pass.SumY);   // 50 + 50 + 49 + 51 + 50
        Assert.Equal(Color.White, pass.Last);
        Assert.Equal(100, pass.LastX);
        Assert.Equal(50, pass.LastY);
        Assert.Equal("9B7BFF", NumberOutline.Hex(Shadow));
    }

    [Fact]
    public void test_the_haloed_outline_rings_the_colour_stroke_in_the_halo_then_draws_the_glyphs_on_top()
    {
        // the Phase 1 review: FIRST BEAT's white stroke on white ink read only as a bolder glyph; a dark halo one pixel
        // beyond the stroke makes any accent visible on any ink
        var halo = new Color(0x16, 0x11, 0x10);
        var pass = new Recorder { Outline = halo };
        NumberOutline.DrawHaloed(ref pass, 100, 50, Color.White, Color.White, halo);
        Assert.Equal(13, pass.Calls);
        Assert.Equal(8, pass.OutlineCalls);                  // the halo's eight passes
        Assert.Equal(13 * 100, pass.SumX);                   // symmetric about the glyph
        Assert.Equal(13 * 50, pass.SumY);
        Assert.Equal(Color.White, pass.Last);                // the ink last, on top
        Assert.Equal(100, pass.LastX);
        Assert.Equal(50, pass.LastY);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++) NumberOutline.DrawHaloed(ref pass, i & 255, i & 127, Color.White, Color.White, halo);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void test_the_outline_draw_allocates_nothing()
    {
        var pass = new Recorder { Outline = Shadow };
        NumberOutline.Draw(ref pass, 0, 0, Shadow, Color.White);   // warm
        _ = NumberOutline.For(RecipeTierKind.Agnostic, Shadow);
        _ = RecipeTier.Of(RecipeFamily.Reaction, "anvil", "snare_jaws");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            var outline = NumberOutline.For(RecipeTier.Of(RecipeFamily.Reaction, "anvil", "snare_jaws"), Shadow);
            if (outline is { } o) NumberOutline.Draw(ref pass, i & 255, i & 127, o * 0.8f, Color.White);
        }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(50_005, pass.Calls);
    }

    [Fact]
    public void test_the_screen_outlines_only_the_agnostic_reaction_and_field_numbers()
    {
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        // the reaction's answer (held for the snap, or spawned at once) and the field / mark tick's total
        Assert.Contains("AgnosticOutline(reactionHitSlot) ?? FirstBeatOutline(firstBeat));", hunt);
        Assert.Contains("(reactionHit ? AgnosticOutline(reactionHitSlot) : null) ?? FirstBeatOutline(firstBeat));", hunt);
        Assert.Contains("outline: AgnosticOutline(_auraTotalSlot));", hunt);
        Assert.Contains("_auraTotalSlot = lastOwner.Slot;", hunt);
        Assert.Contains("echo.Outline);", hunt);
        // the outline is decided by the tier, nowhere else
        Assert.Contains("return NumberOutline.For(tier, SourceGlow(sk.Source));", hunt);
        // drawn in the existing number pass, with the trace naming it
        Assert.Contains("NumberOutline.DrawHaloed(ref pass, x, y,", hunt);
        Assert.Contains("\\toutline={NumberOutline.Hex(traced)}", hunt);
    }

    private static IdleXIdle.Core.Expeditions.BattleEvent Strike(int slot, IdleXIdle.Core.Builds.HitSource hit, int atMs = 1000)
        => new(IdleXIdle.Core.Expeditions.BattleEventKind.Strike, slot, 10, atMs, hit);

    [Fact]
    public void test_first_beat_outlines_the_first_primary_hit_per_creature_white()
    {
        // FIRST BEAT is the Metronome's (FirstHitMultiplier 2); MOMENTUM's 0.85 first hit is not doubled; the Seeker has none
        Assert.True(FirstBeat.Applies(CharacterRoster.All.Single(c => c.Id == "metronome").Shape));
        Assert.False(FirstBeat.Applies(CharacterRoster.All.Single(c => c.Id == "tower").Shape));
        Assert.False(FirstBeat.Applies(CharacterRoster.All.Single(c => c.Id == "seeker").Shape));
        Assert.Equal("FFFFFF", NumberOutline.Hex(FirstBeat.Outline));

        // Core's struckOnce: only a PRIMARY landing adds the creature (LandOn), and the multiplier is read in the skill tree
        // (never on the swing); a carry, a bleed, a reflect and DEADWEIGHT neither take it nor spend it
        var fb = new FirstBeat();
        var swing = IdleXIdle.Core.Builds.HitSource.Swing;
        var primary = IdleXIdle.Core.Builds.HitSource.Primary;
        Assert.False(fb.Cross(Strike(0, swing)));                                       // the swing is never doubled...
        Assert.True(fb.Cross(Strike(0, primary)));                                      // ...so the first cast on 0 still is
        Assert.False(fb.Cross(Strike(0, primary)));                                     // once per creature
        Assert.False(fb.Cross(Strike(1, IdleXIdle.Core.Builds.HitSource.Carry)));
        Assert.False(fb.Cross(Strike(1, IdleXIdle.Core.Builds.HitSource.Bleed)));
        Assert.False(fb.Cross(Strike(1, IdleXIdle.Core.Builds.HitSource.Reflect)));
        Assert.False(fb.Cross(Strike(1, IdleXIdle.Core.Builds.HitSource.Deadweight)));
        Assert.True(fb.Cross(Strike(1, primary)));                                      // the derived hits did not spend it
        Assert.False(fb.Cross(new IdleXIdle.Core.Expeditions.BattleEvent(IdleXIdle.Core.Expeditions.BattleEventKind.EnemyDown, 2, 0, 1000)));
        Assert.True(fb.Cross(Strike(2, primary)));
        Assert.True(fb.Cross(Strike(40, primary)));                                     // a wide composition grows the set once
        fb.Reset();                                                                     // a new wave: Core's set is wave-local
        Assert.True(fb.Cross(Strike(0, primary)));

        // the predicate allocates nothing on a steady wave
        fb.Reset();
        _ = fb.Cross(Strike(3, primary));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++) { if ((i & 255) == 0) fb.Reset(); _ = fb.Cross(Strike(i & 7, (i & 1) == 0 ? primary : swing)); }
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);

        // the screen crosses EVERY Strike in order, and gives the white outline only where no Source outline is set
        var hunt = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));
        var strike = hunt[hunt.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("var firstBeat = _firstBeat.Cross(e) && FirstBeat.Applies(Character.Shape);", strike);
        Assert.True(strike.IndexOf("_firstBeat.Cross(e)", StringComparison.Ordinal) < strike.IndexOf("if (auraTick)", StringComparison.Ordinal),
                    "FIRST BEAT must cross every Strike, aura ticks included, before any branch");
        Assert.Contains("private static Color? FirstBeatOutline(bool firstBeat) => firstBeat ? FirstBeat.Outline : null;", hunt);
        Assert.Contains("_firstBeat.Reset();", hunt);
    }
}
