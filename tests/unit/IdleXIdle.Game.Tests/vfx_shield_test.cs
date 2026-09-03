using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Game;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// SHIELD is the acceptance case for the VFX placement contract (brief §106, §71, §105).
/// </summary>
/// <remarks>
/// <para>
/// §106 asks that the barrier surround THE SEEKER, surround THE MAGPIE, not sit as a tiny sprite in
/// the torso centre, scale with visual bounds, follow movement, take the absorb on itself and place the
/// break correctly. The measured starting point: a 203 x 132 px dome on a 412 px hunter — 0.32x his
/// height, spanning 39 % to 71 % down his torso. It was, exactly, a tiny sprite in the torso centre.
/// </para>
/// <para>
/// <b>And §71's named pair cannot fail.</b> <c>UiKit.AnimSprite</c> fills the box height with the
/// cropped figure, so all ten champions draw within four pixels of the same HEIGHT: THE SEEKER is
/// 412 px and THE MAGPIE is 412 px. A height-based barrier is identical on them by construction. The
/// spread that can actually break a placement is WIDTH — THE OATHBOUND draws 162 px and QUIVER 373,
/// a 2.30x range against those two hunters' 13 %. So this runs over all ten, and the width check is
/// the one that has teeth.
/// </para>
/// <para>
/// The content boxes below are MEASURED from the shipped art by
/// <c>tools/asset-pipeline/fx_bounds.py</c>, which reads the PNGs with the same alpha rule
/// <c>UiKit</c> uses at runtime. Re-run it if the art is regenerated; the numbers here are the art's,
/// not the test's.
/// </para>
/// </remarks>
public class VfxShieldTests
{
    /// <summary>Idle-strip content boxes, all ten champions, measured 2026-09-03.</summary>
    private static readonly Dictionary<string, ContentBox> Champions = new()
    {
        ["oathbound"] = new(0.324f, 0.068f, 0.324f, 0.033f),
        ["chorus"] = new(0.283f, 0.068f, 0.283f, 0.033f),
        ["tower"] = new(0.311f, 0.234f, 0.311f, 0.033f),
        ["metronome"] = new(0.287f, 0.234f, 0.287f, 0.033f),
        ["seeker"] = new(0.258f, 0.223f, 0.258f, 0.033f),
        ["anvil"] = new(0.191f, 0.068f, 0.191f, 0.033f),
        ["magpie"] = new(0.219f, 0.199f, 0.219f, 0.033f),
        ["thornwall"] = new(0.119f, 0.068f, 0.119f, 0.033f),
        ["unbroken"] = new(0.176f, 0.240f, 0.176f, 0.033f),
        ["quiver"] = new(0.096f, 0.068f, 0.096f, 0.033f),
    };

    /// <summary>The shipped <c>fx_shield</c> strip: its dome fills 0.762 x 0.492 of a 512-px frame.</summary>
    private static readonly ContentBox ShippedShield = new(0.119f, 0.215f, 0.119f, 0.293f);

    /// <summary>The regeneration order in §8 of the design: content at 0.91 of the frame, centred.</summary>
    private static readonly ContentBox OrderedShield = new(0.093f, 0.045f, 0.093f, 0.045f);

    private const int Native = 512;

    private static VisualBounds Hunter(string id)
        => new(VfxFigure.VisualRect(HuntScreen.ChampBox, Champions[id]), 1);

    private static VfxPlacement Barrier(string id, ContentBox art, bool clamp = false)
        => VfxResolver.Resolve(VfxProfiles.ShieldBarrier, Hunter(id), art, Native, Native, clamp);

    // ── §71: the two silhouettes the brief names, and the eight it does not ───────────────────────

    [Fact]
    public void test_the_seeker_and_the_magpie_draw_the_same_height_so_a_height_based_barrier_matches()
    {
        // The finding that reframes §71: these two are a 13 % width apart and IDENTICAL in height, so
        // proving the barrier on them proves the arithmetic and nothing about the spread.
        Assert.Equal(Hunter("seeker").Rect.Height, Hunter("magpie").Rect.Height);
        Assert.Equal(268, Hunter("seeker").Rect.Width);
        Assert.Equal(302, Hunter("magpie").Rect.Width);

        Assert.Equal(Barrier("seeker", OrderedShield).Content, Barrier("magpie", OrderedShield).Content);
    }

    [Fact]
    public void test_the_real_width_spread_is_oathbound_against_quiver()
    {
        var narrow = Hunter("oathbound").Rect.Width;
        var wide = Hunter("quiver").Rect.Width;
        Assert.Equal(162, narrow);
        Assert.Equal(373, wide);
        Assert.True(wide / (float)narrow > 2.2f, "the silhouettes differ by more than two to one");
    }

    [Fact]
    public void test_the_barrier_encloses_every_champion_with_room_to_spare()
    {
        // The enclosure clause. It replaces a runtime width-guard dial that would never have fired:
        // the binding case is QUIVER at 373 px inside a 424-px dome, and a dial that never fires is a
        // dormant dial. The constraint lives here and in the regeneration order instead.
        foreach (var id in Champions.Keys)
        {
            var hunter = Hunter(id);
            var dome = Barrier(id, OrderedShield).Content;
            Assert.True(dome.Width >= hunter.Rect.Width * 1.05f,
                        $"{id}: dome {dome.Width} does not enclose a {hunter.Rect.Width}px silhouette");
            Assert.True(dome.Left <= hunter.Rect.Left && dome.Right >= hunter.Rect.Right,
                        $"{id}: the hunter pokes out of his own shield");
        }
    }

    [Fact]
    public void test_the_barrier_is_the_brief_s_own_size_band_against_the_visible_hunter()
    {
        // §65: a shield barrier is 1.10–1.20x the hunter's VISUAL height. Not his box, which is 430
        // and identical for everyone, and not his texture, which is 512 and mostly empty sky.
        foreach (var id in Champions.Keys)
        {
            var hunter = Hunter(id);
            var dome = Barrier(id, OrderedShield).Content;
            var ratio = dome.Height / (float)hunter.Rect.Height;
            Assert.InRange(ratio, 1.10f, 1.20f);
            Assert.True(dome.Top < hunter.Rect.Top - 20,
                        $"{id}: the dome must clear the crown, not cut it");
        }
    }

    [Fact]
    public void test_the_barrier_is_not_a_tiny_sprite_in_the_torso_centre()
    {
        // The literal §106 clause, and the literal defect: 0.32x the hunter, from 39 % to 71 % down him.
        // Even against the SHIPPED strip — which the budget forces the renderer to under-draw — the
        // barrier now covers most of the body rather than a band across the ribs.
        var hunter = Hunter("seeker");
        var drawn = Barrier("seeker", ShippedShield, clamp: true).Content;
        Assert.True(drawn.Height / (float)hunter.Rect.Height > 0.70f);
        Assert.True(drawn.Width > hunter.Rect.Width);
        Assert.Equal(hunter.Rect.Center.X, drawn.Center.X);
    }

    // ── §106: four moments, one geometry ──────────────────────────────────────────────────────────

    [Fact]
    public void test_the_gain_the_absorb_and_undying_all_land_on_the_barrier_itself()
    {
        // "absorb impact lands on barrier" is only guaranteeable if the absorb IS the barrier's
        // rectangle. A ripple offset onto the dome's flank would be normalised to the CHAMPION's width,
        // which runs 162–373 px, so the same number would land somewhere different on every hunter.
        var barrier = Barrier("seeker", OrderedShield).Content;
        foreach (var p in new[] { VfxProfiles.ShieldGain, VfxProfiles.ShieldAbsorb, VfxProfiles.ShieldUndying })
        {
            var moment = VfxResolver.Resolve(p, Hunter("seeker"), OrderedShield, Native, Native).Content;
            Assert.Equal(barrier, moment);
        }
    }

    [Fact]
    public void test_the_break_bursts_outward_from_the_dome_it_replaces()
    {
        // fx_shield_break's own measured content box — it already fills 0.926 x 0.922 of its frame,
        // which is the shape fx_shield is being ordered to match.
        var breakArt = new ContentBox(0.037f, 0.043f, 0.037f, 0.035f);
        var barrier = Barrier("seeker", OrderedShield).Content;
        var burst = VfxResolver.Resolve(VfxProfiles.ShieldBreak, Hunter("seeker"), breakArt, Native, Native).Content;

        Assert.True(burst.Height > barrier.Height, "the break must read as the barrier coming apart");
        Assert.True(burst.Height - barrier.Height < barrier.Height * 0.10f, "...not as a separate picture");
        Assert.Equal(barrier.Center.Y, burst.Center.Y);
    }

    [Fact]
    public void test_the_barrier_follows_the_champion_through_his_whole_lunge()
    {
        // THE AUDITED BUG, as a regression test. The champion is drawn at ChampBox.X + push where push
        // reaches 40 px on a swing, and all ten champion-side effects used the un-pushed box — so during
        // every swing the barrier stood 40 px behind the man inside it.
        var box = HuntScreen.ChampBox;
        foreach (var push in new[] { 0, 20, 40 })
        {
            var drawn = new Rectangle(box.X + push, box.Y, box.Width, box.Height);
            var hunter = new VisualBounds(VfxFigure.VisualRect(drawn, Champions["seeker"]), 1);
            var dome = VfxResolver.Resolve(VfxProfiles.ShieldBarrier, hunter, OrderedShield, Native, Native).Content;
            Assert.Equal(hunter.Rect.Center.X, dome.Center.X);
            Assert.Equal(box.Center.X + push, dome.Center.X);
        }
        // ...and it is PINNED, which is what makes the renderer re-resolve it every frame rather than
        // once. A Detached barrier would pass the arithmetic above and still slide off the body.
        Assert.Equal(VfxFollow.Pinned, VfxProfiles.ShieldBarrier.Follow);
    }

    // ── LAW 16: the asset is wrong, and the contract says so in a number ──────────────────────────

    [Fact]
    public void test_the_shipped_shield_strip_is_over_the_asset_scale_budget()
    {
        // THE WHOLE OF LAW 16 IN ONE ASSERTION. To reach §65's band with today's art the renderer would
        // have to blow a 512-px frame up to 963 — which is `scale = 3.4` with a new name. The asset is
        // regenerated, not the number, and until it is the renderer draws the honest smaller dome and
        // the ratio reports why.
        var honest = Barrier("seeker", ShippedShield);
        Assert.Equal(VfxBudget.Verdict.Over, VfxBudget.Of(honest.NativeRatio));
        Assert.InRange(honest.NativeRatio, 1.87f, 1.89f);

        var drawn = Barrier("seeker", ShippedShield, clamp: true);
        Assert.Equal(VfxBudget.Max, drawn.NativeRatio, 3);
    }

    [Fact]
    public void test_the_regeneration_order_brings_every_shield_moment_into_budget()
    {
        // The order: 8 frames of 512, content at least 0.91 of the frame height, vertically centred,
        // aspect 0.85–0.95. These are the ratios that order produces — the evidence that the profile
        // numbers are achievable rather than aspirational.
        foreach (var id in Champions.Keys)
        {
            var ratio = Barrier(id, OrderedShield).NativeRatio;
            Assert.Equal(VfxBudget.Verdict.Ok, VfxBudget.Of(ratio));
        }
    }

    // ── §105: the same contract on creature-sized subjects ───────────────────────────────────────

    [Fact]
    public void test_a_creature_side_effect_scales_continuously_instead_of_snapping_to_five_buckets()
    {
        // EnemyScale returned Clamp((int)Round(H/140 * mult), 1, 5) — an INTEGER. So 104/208/312/416/520
        // px were the only enemy-side effect sizes the game could produce, and an armoured creature at
        // 404 px and a boss at 540 collapsed into adjacent buckets.
        var strike = new ContentBox(0.057f, 0.031f, 0.057f, 0.031f);   // fx_strike, measured
        var sizes = new List<int>();
        foreach (var height in new[] { 246, 343, 404, 440, 520 })
        {
            var creature = new VisualBounds(new Rectangle(0, 0, 260, height), -1);
            sizes.Add(VfxResolver.Resolve(VfxProfiles.CastStrike, creature, strike, Native, Native).Content.Height);
        }
        Assert.Equal(sizes.Count, sizes.Distinct().Count());
        Assert.True(sizes.SequenceEqual(sizes.OrderBy(v => v)), "a bigger body takes a bigger blow");
    }
}
