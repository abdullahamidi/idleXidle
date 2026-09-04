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
/// <b>The placement arithmetic alone did not fix it, and this file is where that shows.</b> Sizing by
/// the CONTENT rather than the frame took the barrier from 132 px to 315, which is bigger and still
/// cuts the crown and the soles — the clause fails on 0.76x exactly as it fails on 0.32x. The wall was
/// the art: a hemisphere filling 0.492 of its frame cannot be enlarged to a whole-body shell without
/// the magnification LAW 16 forbids. So the strip was regenerated (2026-09-04) as a closed ring filling
/// its frame, and the same authored 1.15 now draws 474 px at 0.93 of native. Both content boxes are
/// pinned below: the retired one keeps the clamp under test, the shipped one carries the acceptance.
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

    /// <summary>
    /// The shipped <c>fx_shield</c> strip as regenerated 2026-09-04: a closed ring filling its frame.
    /// </summary>
    /// <remarks>
    /// Measured from <c>assets/art/VFX/shield/fx_shield_strip8_512.png</c> by
    /// <c>tools/asset-pipeline/fx_bounds.py</c>: <c>L 0.000 T 0.000 R 0.000 B 0.000 —
    /// content 1.000w x 1.000h</c>. The ring is inscribed in the square, so its drawn diameter IS the
    /// content box, which is what lets the barrier be a fraction of the hunter rather than of a margin.
    /// </remarks>
    private static readonly ContentBox ShippedShield = new(0f, 0f, 0f, 0f);

    /// <summary>
    /// The strip that shipped until 2026-09-04 — a hemisphere filling 0.762 x 0.492 of its frame.
    /// </summary>
    /// <remarks>
    /// Kept as a NAMED FIXTURE, not as history for its own sake: it is the only art in the repo that
    /// exercises the §73 clamp, and a clamp with no test is a branch nobody has run. It is what LAW 16
    /// looks like as numbers — the shape a regeneration order is written against.
    /// </remarks>
    private static readonly ContentBox RetiredDome = new(0.119f, 0.215f, 0.119f, 0.293f);

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

        Assert.Equal(Barrier("seeker", ShippedShield).Content, Barrier("magpie", ShippedShield).Content);
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
        // the binding case is QUIVER at 373 px inside a 474-px shell, and a dial that never fires is a
        // dormant dial. The constraint lives here and in the art's own framing instead.
        foreach (var id in Champions.Keys)
        {
            var hunter = Hunter(id);
            var dome = Barrier(id, ShippedShield).Content;
            Assert.True(dome.Width >= hunter.Rect.Width * 1.05f,
                        $"{id}: dome {dome.Width} does not enclose a {hunter.Rect.Width}px silhouette");
            Assert.True(dome.Left <= hunter.Rect.Left && dome.Right >= hunter.Rect.Right,
                        $"{id}: the hunter pokes out of his own shield");
        }
    }

    [Fact]
    public void test_the_barrier_clears_the_crown_and_the_soles_on_every_champion()
    {
        // THE CLAUSE THE VERIFIER FAILED THE CONTRACT ON: it is not enough that the barrier be large,
        // it has to close ABOVE the head and BELOW the feet, or it is a band across the body wearing a
        // bigger number. Both edges, all ten, with the ±0.02-height lift the profile carries — which is
        // why the two margins are not equal and both have to be asserted.
        foreach (var id in Champions.Keys)
        {
            var hunter = Hunter(id);
            var dome = Barrier(id, ShippedShield).Content;
            Assert.True(dome.Top <= hunter.Rect.Top - 20,
                        $"{id}: the shell closes {hunter.Rect.Top - dome.Top}px above the crown, which is not clear of it");
            Assert.True(dome.Bottom >= hunter.Rect.Bottom + 20,
                        $"{id}: the shell closes {dome.Bottom - hunter.Rect.Bottom}px below the soles, which is not clear of them");
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
            var dome = Barrier(id, ShippedShield).Content;
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
        // Measured against what the renderer ACTUALLY draws — clamp: true is the renderer's own call —
        // so this cannot be satisfied by a size the budget then refuses to honour.
        var hunter = Hunter("seeker");
        var drawn = Barrier("seeker", ShippedShield, clamp: true).Content;
        Assert.True(drawn.Height > hunter.Rect.Height, "the shell is taller than the man inside it");
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
        var barrier = Barrier("seeker", ShippedShield).Content;
        foreach (var p in new[] { VfxProfiles.ShieldGain, VfxProfiles.ShieldAbsorb, VfxProfiles.ShieldUndying })
        {
            var moment = VfxResolver.Resolve(p, Hunter("seeker"), ShippedShield, Native, Native).Content;
            Assert.Equal(barrier, moment);
        }
    }

    [Fact]
    public void test_the_break_bursts_outward_from_the_dome_it_replaces()
    {
        // fx_shield_break's own measured content box — it already fills 0.926 x 0.922 of its frame,
        // which is the shape fx_shield is being ordered to match.
        var breakArt = new ContentBox(0.037f, 0.043f, 0.037f, 0.035f);
        var barrier = Barrier("seeker", ShippedShield).Content;
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
            var dome = VfxResolver.Resolve(VfxProfiles.ShieldBarrier, hunter, ShippedShield, Native, Native).Content;
            Assert.Equal(hunter.Rect.Center.X, dome.Center.X);
            Assert.Equal(box.Center.X + push, dome.Center.X);
        }
        // ...and it is PINNED, which is what makes the renderer re-resolve it every frame rather than
        // once. A Detached barrier would pass the arithmetic above and still slide off the body.
        Assert.Equal(VfxFollow.Pinned, VfxProfiles.ShieldBarrier.Follow);
    }

    // ── LAW 16: the asset was wrong, and the contract said so in a number ─────────────────────────

    [Fact]
    public void test_the_retired_dome_was_over_the_budget_and_the_renderer_clamped_it()
    {
        // THE WHOLE OF LAW 16 IN ONE ASSERTION, kept live against the art it was written about. To
        // reach §65's band with the hemisphere the renderer would have had to blow a 512-px frame up to
        // 963 — `scale = 3.4` with a new name. It refused, drew the honest smaller dome, and the ratio
        // said why. That refusal is the branch this fixture exists to keep exercised: no SHIELD strip
        // the game ships is over the budget any more, so without a named over-budget shape nothing in
        // this file would run the clamp. (The arena still clamps two OTHER strips every frame — fx_press
        // at ratio 1.44 and fx_seeker_trap at 1.37, measured by RH_VFX_DUMP — but those are open art
        // orders in the ledger, not a shape a shield test may pin.)
        var honest = Barrier("seeker", RetiredDome);
        Assert.Equal(VfxBudget.Verdict.Over, VfxBudget.Of(honest.NativeRatio));
        Assert.InRange(honest.NativeRatio, 1.87f, 1.89f);

        var drawn = Barrier("seeker", RetiredDome, clamp: true);
        Assert.Equal(VfxBudget.Max, drawn.NativeRatio, 3);

        // ...and clamped is not the same as enough. THE DEFECT, as the number it was: 315 px of picture
        // across a 412-px hunter, cutting his crown and his soles both.
        var hunter = Hunter("seeker");
        var clamped = drawn.Content;
        Assert.True(clamped.Height < hunter.Rect.Height, "the retired dome could not reach the hunter's height");
        Assert.True(clamped.Top > hunter.Rect.Top, "...so it cut the crown");
        Assert.True(clamped.Bottom < hunter.Rect.Bottom, "...and it cut the soles");
    }

    [Fact]
    public void test_the_shipped_strip_brings_every_shield_moment_inside_the_budget()
    {
        // The regeneration landed: a closed ring on 8 frames of 512, filling its frame, so the SAME
        // authored 1.15 draws 474 px at 0.93 of native on every hunter. The evidence that the profile
        // numbers are achievable rather than aspirational — and that nothing had to be magnified to
        // get there.
        foreach (var id in Champions.Keys)
        {
            var placed = Barrier(id, ShippedShield);
            Assert.Equal(VfxBudget.Verdict.Ok, VfxBudget.Of(placed.NativeRatio));
            // Unclamped and clamped agree: the renderer is drawing the size the design asked for.
            Assert.Equal(placed.Content, Barrier(id, ShippedShield, clamp: true).Content);
        }

        foreach (var p in new[] { VfxProfiles.ShieldGain, VfxProfiles.ShieldAbsorb, VfxProfiles.ShieldUndying })
            Assert.Equal(VfxBudget.Verdict.Ok,
                         VfxBudget.Of(VfxResolver.Resolve(p, Hunter("quiver"), ShippedShield, Native, Native).NativeRatio));
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
