using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Game.Vfx;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// The VFX contract as a set of promises about itself (brief §63, §66, §68, §105, §112).
/// </summary>
/// <remarks>
/// Two rules this project enforces on itself, mechanised. <b>No dial without a consumer</b> — every
/// enum member and every profile in the table must be reached by something that runs, because a
/// vocabulary with unused words is a vocabulary nobody can trust. And <b>no silence</b> — every effect
/// key must resolve, which is the failure that hid PRESS, WEEP and WILT drawing nothing for months.
/// </remarks>
public class VfxContractTests
{
    /// <summary>The screen's source, found by walking up from the test binary to the repo root.</summary>
    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(string.Join('/', parts) + " not found above the test binary.");
    }

    private static string HuntScreenSource() => Source("src", "IdleXIdle.Game", "HuntScreen.cs");

    /// <summary>
    /// The same source with its comments removed, for assertions about what the code DOES.
    /// </summary>
    /// <remarks>
    /// This screen documents its own history at length, so "EnemyScale is gone" is false against the raw
    /// text — a comment still explains what the wave-total bar used to be measured against, and another
    /// records that the wave boundary must NOT call <c>_vfx.Clear()</c>. Both should stay; an assertion
    /// that cannot tell prose from code would delete them to stay green.
    /// </remarks>
    private static string Code(string source)
    {
        var noBlocks = System.Text.RegularExpressions.Regex.Replace(source, @"/\*.*?\*/", "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(noBlocks, @"^[ \t]*//.*$", "",
            System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    // ── The table ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_profile_has_a_unique_id_and_a_usable_shape()
    {
        Assert.Equal(VfxProfiles.All.Count, VfxProfiles.All.Select(p => p.Id).Distinct().Count());
        foreach (var p in VfxProfiles.All)
        {
            Assert.True(p.RelativeScale > 0f, $"{p.Id}: an effect with no size is an effect nobody sees");
            Assert.True(p.Fps > 0f, $"{p.Id}: a strip with no frame rate never advances");
            Assert.True(p.Frames > 0, $"{p.Id}: the frame count is declared, never inferred");
            Assert.StartsWith("fx_", p.AssetKey, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void test_every_profile_is_named_by_a_live_spawn_site()
    {
        // The house failure mode is a dial nothing reads. A profile in the table that no code plays is
        // exactly that, one layer up: authored data describing an effect the game never draws.
        //
        // A profile is REACHED one of exactly two ways, and both are checked here rather than assumed.
        // Either the screen names it outright (VfxProfiles.ShieldBarrier), or a skill in the catalogue
        // routes to it through ForSkill — which the screen calls once, for every cast. An earlier draft
        // of this test accepted "the file mentions ForSkill somewhere" as proof for ANY profile, which
        // made it pass for a profile nothing could ever reach.
        var src = Code(HuntScreenSource());
        var byCast = SkillCatalogue.All.Select(VfxProfiles.ForSkill).Select(p => p.Id).ToHashSet();
        foreach (var p in VfxProfiles.All)
        {
            var member = string.Concat(p.Id.Split('.', '_')
                .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
            var named = src.Contains($"VfxProfiles.{member}", StringComparison.Ordinal);
            Assert.True(named || byCast.Contains(p.Id),
                        $"{p.Id} is in the table and nothing plays it");
        }
        // ...and the cast route really is wired: the screen resolves a skill's profile rather than
        // switching on its clip key by hand, which is the switch this contract deleted.
        Assert.Contains("VfxProfiles.ForSkill", src, StringComparison.Ordinal);
    }

    [Fact]
    public void test_the_debug_views_dev_key_is_not_already_bound_to_something_else()
    {
        // THE DESIGN SAID "F8 IS FREE" AND THE CODE SAID OTHERWISE. F8 has stepped the UI SCALE since
        // the density-profile work, so binding the overlay there toggled the view and changed the
        // layout it exists to measure, in one press. A dev key is only a door if it opens one thing.
        var src = Code(Source("src", "IdleXIdle.Game", "Game1.cs"));
        var bindings = System.Text.RegularExpressions.Regex
            .Matches(src, @"Pressed\(Keys\.(F\d+)\)")
            .Select(m => m.Groups[1].Value)
            .ToList();
        var vfxLine = src.Split('\n').Single(l => l.Contains("DevVfxDebug = !", StringComparison.Ordinal));
        var key = System.Text.RegularExpressions.Regex.Match(vfxLine, @"Keys\.(F\d+)").Groups[1].Value;
        Assert.NotEqual("", key);
        Assert.Equal(1, bindings.Count(k => k == key));
    }

    [Fact]
    public void test_every_word_in_the_anchor_vocabulary_has_a_profile_that_uses_it()
    {
        // §63: "Do not create dozens of anchors. Use only real consumers." ActorChest and Weapon/Hand
        // were both offered by the brief and both refused — the first is Center plus an offset the
        // normalized model already expresses, the second would be a lie about a rig that cannot supply
        // a hand. What survives has to earn its place, and this is where that is checked.
        AssertEveryMemberUsed<VfxAnchor>(p => p.Anchor);
        AssertEveryMemberUsed<VfxSubjectKind>(p => p.Subject);
        AssertEveryMemberUsed<VfxBasis>(p => p.Basis);
        AssertEveryMemberUsed<VfxLayer>(p => p.Layer);
        AssertEveryMemberUsed<VfxFacing>(p => p.Facing);
        AssertEveryMemberUsed<VfxFollow>(p => p.Follow);
        AssertEveryMemberUsed<VfxLifetime>(p => p.Lifetime);
        AssertEveryMemberUsed<VfxTravel>(p => p.Travel);
    }

    private static void AssertEveryMemberUsed<T>(Func<VfxProfile, T> of) where T : struct, Enum
    {
        var used = VfxProfiles.All.Select(of).ToHashSet();
        foreach (var member in Enum.GetValues<T>())
            Assert.True(used.Contains(member), $"{typeof(T).Name}.{member} has no profile — a dormant word");
    }

    // ── Layers, which are an ordering guarantee a screenshot cannot give ──────────────────────────

    [Fact]
    public void test_the_two_under_layers_draw_before_the_figures_and_the_other_two_after()
    {
        // §68: "Shield barrier must not accidentally render behind the background or over every HUD
        // element." Splitting one flat pass into two is what gives that sentence a mechanism.
        Assert.True(VfxPlayer.IsUnderLayer(VfxLayer.GroundUnder));
        Assert.True(VfxPlayer.IsUnderLayer(VfxLayer.BehindSubject));
        Assert.False(VfxPlayer.IsUnderLayer(VfxLayer.OnSubject));
        Assert.False(VfxPlayer.IsUnderLayer(VfxLayer.Overhead));
    }

    [Fact]
    public void test_the_arena_draws_the_under_pass_before_the_figures_and_the_over_pass_after()
    {
        var src = HuntScreenSource();
        var arena = src[src.IndexOf("private void DrawArena", StringComparison.Ordinal)..];
        var under = arena.IndexOf("_vfx.DrawUnder", StringComparison.Ordinal);
        var enemies = arena.IndexOf("DrawNormalEnemy(b, attacking)", StringComparison.Ordinal);
        var champion = arena.IndexOf("DrawChampion(b, _champDrawBox", StringComparison.Ordinal);
        var over = arena.IndexOf("_vfx.DrawOver", StringComparison.Ordinal);
        var callouts = arena.IndexOf("DrawCallouts", StringComparison.Ordinal);

        Assert.True(under > 0 && enemies > under, "the ground ring draws before the pack stands on it");
        Assert.True(champion > under, "the field draws behind the hunter it wraps");
        Assert.True(over > champion, "a blow draws over the body it landed on");
        Assert.True(callouts > over, "the numbers stay above the effects, as they always did");
    }

    [Fact]
    public void test_a_held_effect_follows_its_subject()
    {
        // A held effect is a STATE of a body. One that resolved once and stayed there would slide off
        // the body on the first lunge, which is the bug the whole contract was written to end.
        foreach (var p in VfxProfiles.All.Where(p => p.Lifetime == VfxLifetime.Held))
            Assert.Equal(VfxFollow.Pinned, p.Follow);
    }

    // ── Skills reach their art ────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_skill_in_the_catalogue_resolves_to_a_profile()
    {
        foreach (var def in SkillCatalogue.All)
        {
            var p = VfxProfiles.ForSkill(def);
            Assert.NotNull(p);
            Assert.Contains(p, VfxProfiles.All);
        }
    }

    [Fact]
    public void test_weep_takes_the_override_because_its_art_is_a_different_shape_from_its_clip()
    {
        // WEEP's ClipKey is `projectile` and its art is a 0.27-wide, 0.97-tall falling column. Played
        // through the bolt profile it would be thrown sideways across the arena. This is the one place
        // the override table earns its existence; everything else follows its clip.
        var weep = SkillCatalogue.All.Single(d => d.FxKey == "weep");
        Assert.Equal("projectile", weep.ClipKey);
        Assert.Equal(VfxProfiles.CastRain.Id, VfxProfiles.ForSkill(weep).Id);
        Assert.NotEqual(VfxProfiles.CastProjectile.Id, VfxProfiles.ForSkill(weep).Id);
    }

    [Fact]
    public void test_a_trap_belongs_to_the_row_and_a_mark_to_one_creature()
    {
        var trap = SkillCatalogue.All.First(d => d.ClipKey == "trap");
        Assert.Equal(VfxSubjectKind.EnemyRow, VfxProfiles.ForSkill(trap).Subject);
        Assert.Equal(VfxBasis.SubjectWidth, VfxProfiles.ForSkill(trap).Basis);

        var mark = SkillCatalogue.All.First(d => d.ClipKey == "mark");
        Assert.Equal(VfxSubjectKind.Creature, VfxProfiles.ForSkill(mark).Subject);
        Assert.Equal(VfxAnchor.Head, VfxProfiles.ForSkill(mark).Anchor);
    }

    [Fact]
    public void test_the_bolt_is_thrown_forward_and_crosses_to_its_target()
    {
        var bolt = VfxProfiles.CastProjectile;
        Assert.Equal(VfxTravel.ToTarget, bolt.Travel);
        Assert.Equal(VfxFacing.Forward, bolt.Facing);
        Assert.True(bolt.OffsetX > 0f, "it leaves from the leading edge of the silhouette, not the middle");
    }

    // ── The arithmetic that used to be in the spawn sites is gone from them ───────────────────────

    [Fact]
    public void test_no_spawn_site_computes_a_pixel_any_more()
    {
        // §62: no scattered `x += 17 / y -= 23 / scale = 0.42`. The four names below were the whole of
        // the old positioning model, and every one of them is a defect the audit measured: five integer
        // size buckets for creatures, a one-frame-stale creature table with a magic fallback, and a
        // 104-px "base unit" that could not be right for two differently sized figures at once.
        var src = Code(HuntScreenSource());
        foreach (var gone in new[] { "EnemyScale(", "EnemyPoint(", "PublishCreature(", "BaseUnitPx" })
            Assert.False(src.Contains(gone, StringComparison.Ordinal),
                         $"{gone} is back — placement has leaked out of the contract again");
    }

    [Fact]
    public void test_the_effects_pass_stays_unscissored()
    {
        // A documented regression, deliberately preserved: clipping the additive pass cut bursts flat
        // against an invisible rectangle and "gave the arena away". Both new passes inherit it, and both
        // restore the arena's own rasterizer so the callouts and the overlay keep their edge.
        var src = HuntScreenSource();
        Assert.Contains("_vfx.Rasterizer = null;", src);
        Assert.Contains("_vfx.RestoreRasterizer = ArenaRasterizer;", src);
    }

    [Fact]
    public void test_a_wave_boundary_still_leaves_the_field_standing()
    {
        // BeginWave deliberately does NOT clear the effects: clearing at the boundary erased the field
        // mid-flight twice a cycle, and waves are short enough that the gap was half the time on screen.
        var src = Code(HuntScreenSource());
        var beginWave = src[src.IndexOf("private void BeginWave", StringComparison.Ordinal)..];
        var end = beginWave.IndexOf("\n    private ", StringComparison.Ordinal);
        Assert.DoesNotContain("_vfx.Clear()", beginWave[..(end > 0 ? end : beginWave.Length)]);
    }

    [Fact]
    public void test_the_debug_view_is_given_the_unclamped_ratio_because_the_clamped_one_diagnoses_nothing()
    {
        // The renderer draws every over-budget effect at exactly VfxBudget.Max, so the ratio ON the
        // drawn placement is 1.25 for all of them — inside the band, by construction. A debug view fed
        // that number printed "ratio 1.25 OVER BUDGET": a figure contradicting its own verdict, with
        // the red branch of its colour rule unreachable. The honest, unclamped ratio is what the dump
        // prints and what the asset order is written from, so it is what the picture must print too.
        var figure = new VisualBounds(new Microsoft.Xna.Framework.Rectangle(0, 0, 200, 400), 1);
        var quarter = new ContentBox(0.375f, 0.375f, 0.375f, 0.375f);
        var profile = new VfxProfile("test", "fx_strike", VfxSubjectKind.Champion, VfxAnchor.Center, 0.5f);

        var clamped = VfxResolver.Resolve(profile, figure, quarter, 512, 512, clampToBudget: true);
        Assert.Equal(VfxBudget.Verdict.Ok, VfxBudget.Of(clamped.NativeRatio));   // it cannot tell
        var honest = VfxResolver.Resolve(profile, figure, quarter, 512, 512);
        Assert.Equal(VfxBudget.Verdict.Over, VfxBudget.Of(honest.NativeRatio));  // this can

        var player = Code(Source("src", "IdleXIdle.Game", "VfxPlayer.cs"));
        var row = player.Split('\n').Single(l => l.Contains("a.Placement.Anchor + drift", StringComparison.Ordinal));
        Assert.Contains("a.HonestRatio", row, StringComparison.Ordinal);
        Assert.DoesNotContain("a.Placement.NativeRatio", row, StringComparison.Ordinal);
    }

    // ── §112: nothing generated sits in the tree unreached ───────────────────────────────────────

    [Fact]
    public void test_the_three_effects_that_resolved_to_nothing_now_have_an_alias()
    {
        // PRESS, WEEP and WILT named FxKeys with no alias, so the resolver returned null and the player
        // returned silently. PRESS and WILT are two of the four Field skills, and a Field's art drives
        // the held field, so those builds had no field effect at all.
        var assets = Source("src", "IdleXIdle.Game", "AssetLibrary.cs");
        foreach (var key in new[] { "fx_press", "fx_weep", "fx_wilt" })
            Assert.Contains($"[\"{key}\"]", assets);

        // ...and the orphan alias is gone. fx_levelup was aliased, present on disk, and played by
        // nothing anywhere in the game.
        Assert.DoesNotContain("[\"fx_levelup\"]", assets);
    }

    [Fact]
    public void test_every_field_skill_names_art_the_alias_table_can_reach()
    {
        // The held field wears the Field skill's own FxKey. This is the assertion that generalises the
        // one above: a new Field skill with unaliased art would fail here rather than draw nothing.
        var assets = Source("src", "IdleXIdle.Game", "AssetLibrary.cs");
        foreach (var def in SkillCatalogue.All.Where(d => d.Kind == SkillKind.Field))
            Assert.Contains($"[\"fx_{def.FxKey}\"]", assets);
    }
}
