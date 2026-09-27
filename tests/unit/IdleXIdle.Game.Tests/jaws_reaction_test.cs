using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Sources;
using IdleXIdle.Game.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE REACTION CONTRACT (ADR-011, JAWS; the FINAL direction of 2026-09-27, SHADOW FANGS, with the mist polish): a
/// reaction is presented on its own layer, belongs to its SKILL, never owns the champion's figure and moves nothing; it
/// is EXACTLY TWO LAYERS, a soft Shadow MIST (supportive) and FOUR LARGE SIMPLE fang shapes (the semantic impact),
/// and nothing else (no creature, no eye, no glint, no residue, no particles, no additive light, no flash under them).
/// The mist pocket lies UNDER the figures on purpose (a pool the bitten creature stands in, never a veil over it); the
/// fangs are over everything. The mist materialises and contracts while the fangs emerge open OUTSIDE the creature's silhouette,
/// a rapid snap onto its outer edges at full opacity, a long hold with the body between the fangs while the mist stays
/// compressed and then loosens, the fangs fading before the mist evaporates; ~200 ms; ONE cue on the snap; the number
/// a few frames after the snap.
/// </summary>
/// <remarks>
/// The failures these stop were measured across the JAWS work: a row-wide rope ring no one could tie to the bite, five
/// generic sounds on one frame, a post-bite "lay a trap" clip that never played, a dock tile that looked the same armed
/// and rearming, a spring-loaded bear trap with a chain the owner rejected as a mechanism, three near-equal piranhas
/// that read as "purple activity", and then one piranha whose head, eye, mouth, teeth and tail were below the useful
/// perceptual budget at gameplay scale and became coloured motion at true speed.
/// </remarks>
public class JawsReactionTest
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is { Length: > 0 } && !File.Exists(Path.Combine(dir, "IdleXIdle.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.False(dir is null, "IdleXIdle.sln not found above the test binary.");
        return Path.Combine(new[] { dir! }.Concat(parts).ToArray());
    }

    private static readonly ReactionRecipe Jaws = ReactionRecipes.SeekerJaws;

    private sealed class Stage : IReactionStage
    {
        public Rectangle Champion = new(420, 451, 180, 400);
        public Rectangle Target = new(1050, 640, 160, 170);

        public bool TryActorFrame(string clipKey, int frame, out SpriteFrame drawn, out int frameSize)
        {
            drawn = default;
            frameSize = 0;
            return false;
        }

        public bool TryTargetBody(int slot, out Rectangle body)
        {
            body = Target;
            return true;
        }

        public bool TryCaughtBody(int slot, out Rectangle body)
        {
            body = Target;
            return true;
        }

        public Texture2D? Texture(string key) => null;
        public float CasterHeight => Champion.Height;

        public bool TryChampionBody(out Rectangle body)
        {
            body = Champion;
            return true;
        }

        public bool TryTargetFrame(int slot, out SpriteFrame frame)
        {
            frame = default;
            return false;
        }
    }

    private static ReactionPerformance Answer(params int[] targets) => new(Jaws, 3, 7000, targets.Length == 0 ? new[] { 0 } : targets, Color.MediumPurple);

    // ── IDENTITY: the skill's own, never its Form's ─────────────────────────────────────────────

    [Fact]
    public void test_the_jaws_recipe_belongs_to_the_skill_and_repay_keeps_its_own()
    {
        // JAWS and REPAY share the Snare Form's clip and effect words: a lookup by those would give REPAY the jaws
        var jaws = SkillCatalogue.ById("snare_jaws");
        var repay = SkillCatalogue.ById("snare_repay");
        Assert.Equal(jaws.ClipKey, repay.ClipKey);
        Assert.Equal(jaws.FxKey, repay.FxKey);

        Assert.Same(ReactionRecipes.SeekerJaws, ReactionRecipes.For("seeker", "snare_jaws"));
        Assert.Equal("seeker.jaws", ReactionRecipes.SeekerJaws.Id);
        Assert.Null(ReactionRecipes.For("seeker", "snare_repay"));
        Assert.Null(ReactionRecipes.For("magpie", "snare_jaws"));   // another champion keeps the legacy reaction
        // ...and neither is an ACTION: a reaction never owns the figure
        Assert.Null(ActionRecipes.For("seeker", jaws.Id, jaws.ClipKey, jaws.FxKey));
        Assert.Null(ActionRecipes.For("seeker", repay.Id, repay.ClipKey, repay.FxKey));
    }

    [Fact]
    public void test_a_presented_reaction_loads_no_lay_a_trap_clip_and_repay_still_does()
    {
        var seeker = CharacterRoster.Get("seeker");
        var into = new List<string>();
        ActorClips.ChampionStrips(seeker, new[] { new EquippedSkill(SkillCatalogue.ById("snare_jaws"), Source.Shadow, null) }, _ => true, into);
        Assert.DoesNotContain(into, k => k.Contains("trap", StringComparison.Ordinal));
        ActorClips.ChampionStrips(seeker, new[] { new EquippedSkill(SkillCatalogue.ById("snare_repay"), Source.Shadow, null) }, _ => true, into);
        Assert.Contains(into, k => k.Contains("trap", StringComparison.Ordinal));   // REPAY's own cast clip
    }

    // ── TWO LAYERS, MIST AND FANGS, AND NOTHING ELSE ─────────────────────────────────────────────

    [Fact]
    public void test_the_reaction_is_mist_and_four_fangs_and_nothing_else()
    {
        // no creature, no swarm, no residue, no glint, no particles, no behind layer, no light pass: the recipe has no such
        // dials and the layer has no such passes; its only art is the fang and the two mist wisps
        var props = typeof(ReactionRecipe).GetProperties().Select(pr => pr.Name).ToArray();
        foreach (var banned in new[] { "Jaw", "Piranha", "Head", "Mouth", "Eye", "Tail", "Residue", "Glint", "Behind", "Smear", "Spark", "Particle",
                                       "Trail", "Yank", "Chain", "Tether", "Whip", "Pivot", "Half", "Shut" })
            Assert.DoesNotContain(props, n => n.Contains(banned, StringComparison.Ordinal));
        var keys = props.Where(n => n.EndsWith("Key", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "FangKey", "MistKey", "WispKey" }, keys);   // one fang shape, two mist wisps
        var methods = typeof(ReactionPerformance).GetMethods().Select(m => m.Name).ToArray();
        Assert.DoesNotContain("DrawBehind", methods);
        Assert.DoesNotContain("DrawLight", methods);
        Assert.Contains("DrawMaterial", methods);
        Assert.False(Jaws.Callout);
    }

    [Fact]
    public void test_the_fangs_are_large_simple_geometry()
    {
        var r = Jaws;
        // each fang ~a third of the creature's visible height: the upper and lower fangs together are 60-75 % of it
        Assert.InRange(2f * r.FangHeightShare, 0.60f, 0.75f);
        Assert.InRange(r.FangMinPx, 24f, 40f);
        Assert.True(r.FangMaxPx >= 64f);
        // the source is one shape on a small canvas, its point at the bottom centre
        Assert.True(File.Exists(RepoFile("assets", "art", "VFX", "parts", r.FangKey + ".png")), $"{r.FangKey}.png is not filed");
        Assert.True(File.Exists(RepoFile("tools", "asset-pipeline", "v2", "seeker_fangs.py")));   // authored by hand, not generated
        Assert.InRange(r.TipPoint.X, r.ArtWidth * 0.4f, r.ArtWidth * 0.6f);
        Assert.True(r.TipPoint.Y >= r.ArtHeight - 2f);
        Assert.True(r.ArtWidth < r.ArtHeight, "a fang is taller than it is wide");
        // a pair is two fangs either side of the centre line, leaning a little inward
        Assert.InRange(r.PairSpreadShare, 0.4f, 0.8f);
        Assert.InRange(r.TiltDegrees, 5f, 20f);
    }

    [Fact]
    public void test_the_open_pose_is_outside_the_silhouette_and_the_snap_bites_its_outer_edge()
    {
        var r = Jaws;
        // OPEN: the points clearly outside the top and bottom edges, empty space between the fangs and the body
        Assert.InRange(r.OpenGapShare, 0.08f, 0.20f);
        Assert.Equal(-r.OpenGapShare, ReactionPerformance.PointShare(r, 0f));
        Assert.Equal(-r.OpenGapShare, ReactionPerformance.PointShare(r, r.OpenMs));           // the open pose stands
        // SNAPPED: the points a little INSIDE the edges (they bit the outer silhouette), never through the body
        Assert.InRange(r.BiteDepthShare, 0.10f, 0.25f);
        Assert.Equal(r.BiteDepthShare, ReactionPerformance.PointShare(r, r.SnapAtMs));
        Assert.True(2f * r.BiteDepthShare < 0.5f, "the body stays visibly between the fangs");
        // the close is rapid and only ever inward: upper down, lower up (the lower fangs mirror the share about the bottom edge)
        var last = -1f;
        for (var u = 0f; u <= r.SnapAtMs; u += 3f)
        {
            var c = ReactionPerformance.Close(r, u);
            Assert.True(c >= last, "the fangs only ever close");
            last = c;
        }
        Assert.Equal(0f, ReactionPerformance.Close(r, r.OpenMs));
        Assert.True(ReactionPerformance.Close(r, (r.OpenMs + r.SnapAtMs) * 0.5f) < 0.5f, "an ease-in: the last frames carry the motion");
        Assert.Equal(1f, ReactionPerformance.Close(r, r.SnapAtMs));
    }

    [Fact]
    public void test_the_snap_holds_long_enough_to_be_read_and_then_releases()
    {
        var r = Jaws;
        Assert.InRange(r.OpenMs, 17f, 34f);                                     // the open pose stands for a frame or two
        Assert.InRange(r.SnapAtMs, 50f, 60f);                                   // the snap
        Assert.InRange(r.HoldMs, 70f, 100f);                                    // where the readability comes from
        var holdEnd = r.SnapAtMs + r.HoldMs;
        Assert.Equal(holdEnd, r.ReleaseAtMs);
        Assert.Equal(1f, ReactionPerformance.FangOpacity(r, holdEnd - 1f));     // whole through the hold
        Assert.Equal(0f, ReactionPerformance.Release(r, holdEnd - 1f));         // and not moving off the creature
        Assert.True(ReactionPerformance.Release(r, holdEnd + r.ReleaseMs) is >= 4f and <= 8f, "a small 4-8 px release");
        Assert.True(ReactionPerformance.FangOpacity(r, holdEnd + 20f) < 1f, "then a quick fade");
        Assert.Equal(0f, ReactionPerformance.FangOpacity(r, r.GoneMs));
        Assert.InRange(r.EndMs, 180f, 220f);                                    // ~200: a short reaction, no lingering residue
    }

    [Fact]
    public void test_no_jaws_flash_and_the_number_comes_a_few_frames_after_the_snap()
    {
        var r = Jaws;
        Assert.Equal(0f, r.TargetFlash);                                        // the fangs ARE the hit feedback
        Assert.InRange(BitePresentation.UsualFlash.Peak, 0.40f, 0.50f);          // the global F2 is untouched
        Assert.InRange(r.NumberDelayMs, 30f, 50f);
        Assert.Equal(r.SnapAtMs + r.NumberDelayMs, r.AnswerAtMs);
        var p = Answer();
        var stage = new Stage();
        p.Update(7000, stage);
        Assert.False(p.Snapped);
        p.Update(7000 + r.SnapAtMs - 10f, stage);
        Assert.False(p.Snapped, "not on the open or closing frames");
        var step = p.Update(7000 + r.SnapAtMs, stage);
        Assert.True(step.Snapped && p.Snapped, "the cue and a kill's fall are presented on the snap");
        Assert.False(step.Answered, "the number is NOT on the snap frame");
        Assert.False(p.Update(7000 + r.SnapAtMs + 17f, stage).Answered);
        var later = p.Update(7000 + r.AnswerAtMs, stage);
        Assert.True(later.Answered && !later.Snapped && p.Answered, "the number on its own frame, after the fangs were seen");
        Assert.False(p.Finished(7000 + r.EndMs - 1f));
        Assert.True(p.Finished(7000 + r.EndMs));
    }

    // ── THE MIST: materialise, compress, loosen, evaporate (the polish pass) ────────────────────

    /// <summary>What seeker_mist.py measured of the art it wrote (keypose_sources/seeker_mist_spans.json).</summary>
    private static JsonElement MistArt(string key)
        => JsonDocument.Parse(File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "keypose_sources", "seeker_mist_spans.json"))).RootElement.GetProperty(key);

    private static float Num(JsonElement e, string name, int index = -1)
        => index < 0 ? e.GetProperty(name).GetSingle() : e.GetProperty(name)[index].GetSingle();

    /// <summary>The frames a 60 fps screen draws of one reaction (its origin is the first frame, so u = 0, 16.7, 33.3, ...).</summary>
    private static IEnumerable<float> Frames(ReactionRecipe r)
    {
        for (var n = 0; n * (1000f / 60f) < r.GoneMs; n++) yield return n * (1000f / 60f);
    }

    [Fact]
    public void test_the_fangs_emerge_from_the_mist_and_are_whole_and_crisp_at_the_snap()
    {
        var r = Jaws;
        Assert.InRange(ReactionPerformance.FangOpacity(r, 0f), 0.25f, 0.35f);          // never full on the first frame
        Assert.InRange(ReactionPerformance.FangOpacity(r, r.OpenMs), 0.65f, 0.75f);    // the open pose ends ~0.7
        Assert.Equal(1f, ReactionPerformance.FangOpacity(r, r.SnapAtMs));              // CRISP at the snap
        var last = 0f;
        for (var u = 0f; u <= r.SnapAtMs; u += 2f)
        {
            var a = ReactionPerformance.FangOpacity(r, u);
            Assert.True(a >= last, "the fangs only ever gain opacity until the snap");
            last = a;
        }
        // there is mist on the very first frame: the fangs never appear on bare floor before the Shadow they come from
        Assert.InRange(ReactionPerformance.MistOpacity(r, 0f), 0.02f, 0.08f);
        // until the release the fangs are always more opaque than the fog (and they are drawn over it: see the draw-order
        // test); at the release the brief wants the fangs to go FIRST, so from there the fog outlasts them on purpose
        foreach (var u in Frames(r).Where(u => u < r.ReleaseAtMs))
            Assert.True(ReactionPerformance.FangOpacity(r, u) > ReactionPerformance.MistOpacity(r, u), $"the fog out-weighs the fangs at {u:0} ms");
        // the fang art itself is never softened for this: the one shape keeps its hard edge (seeker_fangs.py has no blur)
        Assert.DoesNotContain("GaussianBlur", File.ReadAllText(RepoFile("tools", "asset-pipeline", "v2", "seeker_fangs.py")));
    }

    [Fact]
    public void test_the_mist_materialises_to_its_densest_at_the_snap()
    {
        var r = Jaws;
        Assert.InRange(ReactionPerformance.MistOpacity(r, 15f), 0.20f, 0.32f);         // ~0.25
        Assert.InRange(ReactionPerformance.MistOpacity(r, 30f), 0.42f, 0.56f);         // ~0.5
        Assert.InRange(r.MistPeak, 0.65f, 0.75f);
        Assert.Equal(r.MistPeak, ReactionPerformance.MistOpacity(r, r.SnapAtMs));      // the densest at the snap
        var peak = 0f;
        for (var u = 0f; u < r.GoneMs; u += 1f) peak = Math.Max(peak, ReactionPerformance.MistOpacity(r, u));
        Assert.Equal(r.MistPeak, peak);                                                // never denser than the snap
        // on screen: the main pocket's densest texel is the layer times the art's own peak, and where the second wisp lies
        // over it the two stack; both stay near 0.6 at most, so the stone round the creature stays well above the
        // creature's own black (never one dark blob)
        var main = r.MistPeak * Num(MistArt(r.MistKey), "peak_alpha");
        var second = r.MistPeak * Num(MistArt(r.WispKey), "peak_alpha") * r.WispOpacity;
        Assert.InRange(main, 0.45f, 0.60f);
        Assert.InRange(1f - (1f - main) * (1f - second), 0.50f, 0.62f);
    }

    [Fact]
    public void test_the_mist_contracts_as_the_fangs_close_then_loosens_and_evaporates_after_them()
    {
        var r = Jaws;
        // OPEN: wider and looser (uniformly); it contracts onto the creature as the fangs close, to 1 at the snap
        var open = ReactionPerformance.MistScale(r, 0f);
        Assert.InRange(open.X, 1.10f, 1.25f);
        Assert.InRange(open.Y, 1.10f, 1.25f);
        var snap = ReactionPerformance.MistScale(r, r.SnapAtMs);
        Assert.Equal(1f, snap.X, 3);
        Assert.Equal(1f, snap.Y, 3);
        for (var u = 0f; u < r.SnapAtMs; u += 2f)
            Assert.True(ReactionPerformance.MistScale(r, u + 2f).Y <= ReactionPerformance.MistScale(r, u).Y + 1e-5f, "it only tightens until the snap");
        // the pressure: compressed and dense for 30-40 ms after the snap
        Assert.InRange(r.MistDenseMs, 30f, 40f);
        Assert.Equal(r.MistPeak, ReactionPerformance.MistOpacity(r, r.SnapAtMs + r.MistDenseMs - 1f));
        Assert.Equal(1f, ReactionPerformance.MistScale(r, r.SnapAtMs + r.MistDenseMs - 1f).X, 3);
        // then it loosens outward; after the hold it grows 10-20 % MORE while it can still be seen
        var atRelease = ReactionPerformance.MistScale(r, r.ReleaseAtMs);
        var lastSeen = Frames(r).Last(u => ReactionPerformance.MistOpacity(r, u) * Num(MistArt(r.MistKey), "peak_alpha") >= 0.03f);
        var late = ReactionPerformance.MistScale(r, lastSeen);
        Assert.InRange(late.Y / atRelease.Y, 1.10f, 1.20f);
        Assert.InRange(late.X / atRelease.X, 1.10f, 1.22f);
        // the fangs fade over 45-60 ms from the release, FASTER than the mist (whose evaporation runs to the end of the phrase)
        Assert.InRange(r.FangFadeMs, 45f, 60f);
        Assert.True(r.FangFadeMs < r.GoneMs - r.ReleaseAtMs, "the fangs' fade is shorter than the mist's");
        // ...so a 60 fps screen shows a frame of faint but visible mist ALONE after the fangs (the fangs are not drawn at all
        // below FangVisibleFloor, the same threshold the draw uses). Frames are counted from the reaction's origin, which is
        // the first frame the screen drew it on, so at a steady 60 fps this is the grid the player sees.
        var peakAlpha = Num(MistArt(r.MistKey), "peak_alpha");
        var mistOnly = Frames(r).Count(u => ReactionPerformance.FangOpacity(r, u) < ReactionRecipe.FangVisibleFloor
                                            && ReactionPerformance.MistOpacity(r, u) * peakAlpha >= 0.08f);
        Assert.True(mistOnly >= 1, "no frame of faint mist after the fangs");
        Assert.True(ReactionPerformance.FangOpacity(r, r.ReleaseAtMs + r.FangFadeMs - 2f) < ReactionRecipe.FangVisibleFloor, "the fangs are gone at the end of their fade");
        Assert.Equal(0f, ReactionPerformance.MistOpacity(r, r.GoneMs));
        // it evaporates evenly: no step on the way down, and no stall at the release followed by a collapse (the drop per
        // 60 fps frame after the release stays within a factor of two of the drop per frame before it)
        var prev = ReactionPerformance.MistOpacity(r, r.SnapAtMs);
        for (var u = r.SnapAtMs; u <= r.GoneMs; u += 1f)
        {
            var a = ReactionPerformance.MistOpacity(r, u);
            Assert.True(a <= prev + 1e-5f && prev - a < 0.04f, $"a smooth dissolve at {u} ms");
            prev = a;
        }
        var perFrame = Frames(r).Where(u => u >= r.SnapAtMs + r.MistDenseMs).Select(u => ReactionPerformance.MistOpacity(r, u)).ToArray();
        var drops = perFrame.Zip(perFrame.Skip(1), (a, b) => a - b).Where(d => d > 0.005f).ToArray();
        Assert.True(drops.Max() <= 2.5f * drops.Min() + 0.02f, $"an uneven dissolve: per-frame drops {string.Join(", ", drops.Select(d => d.ToString("0.000")))}");
        Assert.InRange(r.EndMs, 180f, 205f);                                           // ~200: the phrase is not lengthened
    }

    [Fact]
    public void test_the_mist_is_one_soft_dark_pocket_on_the_target()
    {
        var r = Jaws;
        var art = MistArt(r.MistKey);
        // the art is dark (near-black at its core), feathered to nothing at every canvas edge, and filed; procedural, no PixelLab
        Assert.True(File.Exists(RepoFile("tools", "asset-pipeline", "v2", "seeker_mist.py")));
        foreach (var key in new[] { r.MistKey, r.WispKey })
        {
            Assert.True(File.Exists(RepoFile("assets", "art", "VFX", "parts", key + ".png")), $"{key}.png is not filed");
            var a = MistArt(key);
            Assert.Equal(0f, Num(a, "edge_alpha_max"));                                 // no rectangular edges
            Assert.True(Num(a, "core_mean_rgb", 0) + Num(a, "core_mean_rgb", 1) + Num(a, "core_mean_rgb", 2) < 3f * 20f, $"{key}'s core is not near-black");
            Assert.True(Num(a, "core_mean_rgb", 2) >= Num(a, "core_mean_rgb", 1), $"{key} has lost its violet cast");
        }
        // one pocket per target: the dense core covers ~70-90 % of the body's height at the snap; the soft fringe may run past
        // it, but across it stays inside the bitten creature's silhouette box (not the row)
        Assert.InRange(Num(art, "core_span", 1) * r.MistHeightShare, 0.70f, 0.90f);
        Assert.InRange(Num(art, "visible_span", 1) * r.MistHeightShare, 1.1f, 1.6f);
        Assert.True(Num(art, "visible_span", 0) * r.MistWidthShare <= 1.0f, "the pocket spills past the bitten creature's silhouette");
        Assert.InRange(r.WispOpacity, 0.2f, 0.5f);
        Assert.InRange(r.MistRaiseShare, 0.05f, 0.2f);
        Assert.InRange(r.MistDriftPx, 1f, 4f);                                        // a few px of drift, never a boil
        Assert.InRange(r.MistTurnDegrees, 0f, 6f);
        // material only: the layer has no light pass (no additive fog)
        Assert.DoesNotContain("DrawLight", typeof(ReactionPerformance).GetMethods().Select(m => m.Name));
    }

    [Fact]
    public void test_the_mist_lies_under_the_creatures_and_the_fangs_over_everything_six_sprites_a_target()
    {
        var src = File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "Presentation", "ReactionPerformance.cs"));
        string Method(string head)
        {
            var body = src[src.IndexOf(head, StringComparison.Ordinal)..];
            return body[..body.IndexOf("\n    }", StringComparison.Ordinal)];   // CRLF-safe: no trailing newline in the key
        }
        var under = Method("public void DrawUnder(");
        var over = Method("public void DrawMaterial(");
        var mist = Method("private void DrawMist(");
        Assert.Contains("DrawMist(b, stage, k, u, mist, second: false);", under);       // every wisp is drawn in the under pass,
        Assert.Contains("DrawMist(b, stage, k, u, wisp, second: true);", under);        // all main pockets first, then the second wisps
        Assert.DoesNotContain("DrawMist(", over);                                       // ...none over the figures (never a veil)
        Assert.Contains("stage.Texture(Recipe.FangKey)", over);                         // the fangs are drawn after the figures
        Assert.DoesNotContain("FangKey", under);
        Assert.Equal(1, mist.Split("b.Draw(").Length - 1);                              // one sprite per wisp: two per target...
        Assert.Equal(1, over.Split("b.Draw(").Length - 1);                              // ...and one fang draw, looped four times
        Assert.Contains("for (var f = 0; f < 4; f++)", over);
        Assert.Contains("if (FangOpacity(Recipe, u) < ReactionRecipe.FangVisibleFloor) return;", over);   // no invisible fang quads in the tail
        // one creature, never a lunge's canvas, and the body's own height
        Assert.Contains("Math.Min(body.Width, layout.Width)", mist);
        Assert.Contains("var height = body.Height * ReactionRecipes.SizeDial;", mist);          // the body's own height: the fangs sit on its edges
        Assert.Contains("(0.5f - Recipe.MistRaiseShare)", mist);                        // raised onto the body, off the feet
        // the screen: the mist pass runs before ANY creature is drawn (a boss or the pack), the fangs after the figures, and
        // the trace's allocation count covers both passes
        var hunt = Hunt();
        var underAt = hunt.IndexOf("foreach (var r in _reactions) r.DrawUnder(b, this, _playheadMs);", StringComparison.Ordinal);
        Assert.True(underAt > 0 && underAt < hunt.IndexOf("if (_isBossWave) DrawBoss(b, attacking);", StringComparison.Ordinal), "the mist pool is under the creatures");
        Assert.True(hunt.IndexOf("foreach (var r in _reactions) r.DrawMaterial(b, this, _playheadMs);", StringComparison.Ordinal) > underAt);
        Assert.Contains("_reactionAllocBytes += GC.GetAllocatedBytesForCurrentThread() - underAlloc;", hunt);
    }

    [Fact]
    public void test_one_cue_on_the_snap_and_it_is_neither_the_trap_nor_the_piranha()
    {
        Assert.Equal("sfx_seeker_jaws_fangs", Jaws.SnapCues[0]);
        Assert.DoesNotContain("sfx_seeker_jaws_snap", Jaws.SnapCues);    // the dry-steel clack belonged to the rejected trap
        Assert.DoesNotContain("sfx_seeker_jaws_chomp", Jaws.SnapCues);   // the chomp belonged to the rejected piranha
        Assert.InRange(Jaws.SnapVolume, 0.25f, 0.5f);                    // quiet enough to repeat
        Assert.True(File.Exists(RepoFile("assets", "audio", "combat", "sfx_seeker_jaws_fangs.wav")));
        // one bite in the ear: the generator has no secondary ticks
        var gen = File.ReadAllText(RepoFile("tools", "asset-pipeline", "make_action_sfx.py"));
        var fangs = gen[gen.IndexOf("def jaws_fangs(", StringComparison.Ordinal)..];
        fangs = fangs[..fangs.IndexOf("\ndef ", StringComparison.Ordinal)];
        Assert.DoesNotContain("for at, g in", fangs);
        Assert.DoesNotContain("thump(", fangs);                          // no pitch-dropping skull, no bone crunch
        Assert.DoesNotContain("crunch(", fangs);
    }

    [Fact]
    public void test_a_reaction_allocates_nothing_frame_to_frame()
    {
        var stage = new Stage();
        var p = Answer(0, 1);
        p.Update(7000, stage);   // the first frame pins each silhouette (a probe may allocate once)
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var ms = 17; ms < 220; ms += 17) p.Update(7000 + ms, stage);
        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // ── THE SCREEN (source law: HuntScreen needs a device to construct) ─────────────────────────

    private static string Hunt() => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", "HuntScreen.cs"));

    [Fact]
    public void test_a_presented_reaction_never_commits_the_post_bite_trap_clip()
    {
        var src = Hunt();
        var loop = src[src.IndexOf("float? lastTrap = null;", StringComparison.Ordinal)..];
        loop = loop[..loop.IndexOf("if (beatMs is null && lastTrap", StringComparison.Ordinal)];
        Assert.Contains("ReactionRecipes.For(Character.Id, _waveSkills[ri].Def.Id) is not null) continue;", loop);
    }

    [Fact]
    public void test_the_answer_is_scheduled_on_the_snap_and_the_bite_keeps_its_own_moment()
    {
        // PRESENTATION SCHEDULING ONLY. The fight resolved the reflected blow at the bite; on screen a kill's fall and the
        // cue wait for the fangs' snap and the number for its own frame after it; there is NO JAWS flash; the enemy's
        // bite keeps t 0
        var src = Hunt();
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("if (reactionHit && reactionHitPerf is { Answered: false } answering)", strike);
        Assert.Contains("ReactionEchoKind.Number", strike);
        Assert.Contains("if (!auraTick && reactionHit && reactionHitRecipe!.TargetFlash <= 0f)", strike);   // no flash under the fangs
        var down = src[src.IndexOf("case BattleEventKind.EnemyDown:", StringComparison.Ordinal)..];
        down = down[..down.IndexOf("case BattleEventKind.Charge:", StringComparison.Ordinal)];
        Assert.Contains("reactionHitPerf is { Snapped: false } killing", down);
        Assert.Contains("ReactionEchoKind.Death", down);
        Assert.Contains("_deathDeferred.Add(e.Slot)", down);
        // released on the snap (the number on its own frame), dropped on a rewind; a deferred-dead creature is drawn standing until then
        var update = src[src.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)..];
        update = update[..update.IndexOf("private long _reactionAllocBytes", StringComparison.Ordinal)];
        Assert.Contains("ReleaseEchoes(r, present: true, keepNumber: !step.Answered);", update);
        Assert.Contains("if (step.Answered && !step.Snapped) ReleaseEchoes(r, present: true);", update);
        Assert.Contains("Sound?.PlayFirst(r.Recipe.SnapCues", update);              // the cue's transient on the snap
        Assert.Contains("ReleaseEchoes(r, present: _playheadMs >= r.TriggerMs - 1f);", update);
        Assert.Contains("!_replay.CreatureAlive(i) && !_deathDeferred.Contains(i)", src);
        var release = src[src.IndexOf("private void ReleaseEchoes(", StringComparison.Ordinal)..];
        release = release[..release.IndexOf("private readonly List<ReactionPerformance> _reactions", StringComparison.Ordinal)];
        Assert.Contains("case ReactionEchoKind.Flash when r.Recipe.TargetFlash > 0f", release);   // a recipe without a flash flashes nothing
        var spawn = src[src.IndexOf("private ReactionPerformance? SpawnReaction(", StringComparison.Ordinal)..];
        spawn = spawn[..spawn.IndexOf("private void UpdateReactions()", StringComparison.Ordinal)];
        Assert.DoesNotContain("PlayFirst", spawn);                                  // never at the spawn
        // the enemy's own bite is untouched: its thud stays on its own frame, and no glyph is drawn on the champion
        var bite = src[src.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)..];
        bite = bite[..bite.IndexOf("break;", StringComparison.Ordinal)];
        Assert.DoesNotContain("Echo", bite);
        Assert.DoesNotContain("DrawBiteContact", src);
        // the reaction moves nothing and draws nothing behind or in the light pass
        Assert.DoesNotContain("YankOffsetX", src);
        Assert.DoesNotContain("_recoilPx", src);
        Assert.DoesNotContain("in _reactions) r.DrawBehind(", src);
        Assert.DoesNotContain("in _reactions) r.DrawLight(", src);
    }

    [Fact]
    public void test_a_presented_reaction_is_one_sentence_not_five_generic_cues()
    {
        var src = Hunt();
        var skill = src[src.IndexOf("case BattleEventKind.Skill:", StringComparison.Ordinal)..];
        skill = skill[..skill.IndexOf("case BattleEventKind.Heal:", StringComparison.Ordinal)];
        Assert.Contains("SpawnReaction(reactionRecipe, e, castSk.Source)", skill);
        Assert.Contains("if (!performed && reactionRecipe is null) PlaySkillVfx(", skill);                 // no row ring
        Assert.Contains("if (!performed && reactionRecipe is null) Sound?.Play(\"sfx_cast\"", skill);      // no cast breath
        Assert.Contains("if (isReaction && reactionRecipe is null) Sound?.Play(\"sfx_hit\"", skill);       // no reaction thud
        var strike = src[src.IndexOf("case BattleEventKind.Strike:", StringComparison.Ordinal)..];
        strike = strike[..strike.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)];
        Assert.Contains("!performedHit && !reactionHit) Sound?.Play(\"sfx_hit\"", strike);                // the answer's thud
        Assert.Contains("!performedHit && !reactionHit && (_strikeCount++ & 1) == 0", strike);             // ...and its puff
        // the enemy's own bite stays: it is the cause
        var bite = src[src.IndexOf("case BattleEventKind.EnemyStrike:", StringComparison.Ordinal)..];
        Assert.Contains("Sound?.Play(\"sfx_hit\", 0.30f, pitch: -0.25f", bite[..bite.IndexOf("break;", StringComparison.Ordinal)]);
    }

    [Fact]
    public void test_the_dock_reads_a_reactions_rearm_from_the_fight_and_never_rebuilds_it()
    {
        var src = Hunt();
        var timing = src[src.IndexOf("private SkillTiming Timing(int i, SkillDef def)", StringComparison.Ordinal)..];
        // the code, not its comments (which name the rule it refuses)
        var reaction = string.Join('\n', timing[..timing.IndexOf("var isPassiveSlot", StringComparison.Ordinal)]
                                             .Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        Assert.Contains("_replay.ReactionReadinessAt(_playheadMs, i, _run?.ReactionReadyAfterLastWave(i))", reaction);
        foreach (var rule in new[] { "RearmMs", "CooldownMultiplier", "CoiledCooldownFactor", "RailCooldownMs", "LastTrapBefore" })
            Assert.DoesNotContain(rule, reaction);
    }
}
