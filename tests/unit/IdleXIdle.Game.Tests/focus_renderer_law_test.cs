using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// THE FOCUS LIGHT'S LAWS, pinned in the source. A renderer needs a device and cannot run here, so
/// what it must never do — read pixels back, allocate a target or a batch per frame, hit-test a mask —
/// and what the host must do — light every surface through it, keep the click's rectangle, preserve
/// the canvas across the light's own targets — is read off the files, the way the attention and
/// input-gate laws are.
/// </summary>
public class focus_renderer_law_test
{
    private static string RepoFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(string.Join('/', parts) + " not found above the test binary.");
    }

    private static string Source(string file)
        => File.ReadAllText(RepoFile("src", "IdleXIdle.Game", file)).Replace("\r\n", "\n");

    private static string GameDir() => Path.GetDirectoryName(RepoFile("src", "IdleXIdle.Game", "Game1.cs"))!;

    /// <summary>Every partial of the host, so a count over "the host" cannot miss a file.</summary>
    private static string Host()
        => string.Concat(Directory.GetFiles(GameDir(), "Game1*.cs").OrderBy(p => p)
                                  .Select(p => File.ReadAllText(p).Replace("\r\n", "\n")));

    /// <summary>One member's text from its signature: a brace-matched body, or an expression body to its semicolon.</summary>
    private static string MemberOf(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"source no longer contains `{signature}`.");
        var open = source.IndexOf('{', at);
        var semi = source.IndexOf(';', at);
        if (open < 0 || (semi >= 0 && semi < open)) return source[at..(semi + 1)];
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[at..(i + 1)];
        }
        throw new InvalidOperationException($"`{signature}` never closes.");
    }

    // ── THE RENDERER ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_renderer_reads_no_pixels_back_and_allocates_nothing_per_frame()
    {
        foreach (var file in new[] { "FocusRenderer.cs", "FocusShape.cs" })
        {
            var src = Source(file);
            Assert.DoesNotContain("GetData(", src);
            Assert.DoesNotContain("new Color[", src);
            Assert.DoesNotContain("new SpriteBatch", src);
            Assert.DoesNotContain("new Texture2D", src);   // the soft plates come from UiKit's factories, once
            foreach (var line in src.Split('\n').Where(l => l.Contains("new RenderTarget2D", StringComparison.Ordinal)))
                Assert.True(line.Contains("??=", StringComparison.Ordinal), $"{file}: a render target is created outside a null-guarded lazy init: `{line.Trim()}`");
        }
        // The half-resolution targets preserve their contents, like the canvas they are built beside.
        var renderer = Source("FocusRenderer.cs");
        Assert.Equal(2, Regex.Matches(renderer, @"\?\?= new RenderTarget2D\([^;]*RenderTargetUsage\.PreserveContents").Count);
        // No shader: the erase is a blend state, and its factors are the same for colour and alpha.
        Assert.False(Regex.IsMatch(renderer, @"\bEffect\b|\.fx\b|EffectParameter"), "the renderer loads or binds a shader");
        var erase = MemberOf(renderer, "private static readonly BlendState Erase");
        Assert.Contains("ColorSourceBlend = Blend.Zero", erase);
        Assert.Contains("AlphaSourceBlend = Blend.Zero", erase);
        Assert.Contains("ColorDestinationBlend = Blend.InverseSourceAlpha", erase);
        Assert.Contains("AlphaDestinationBlend = Blend.InverseSourceAlpha", erase);
    }

    [Fact]
    public void test_the_renderers_textures_are_built_once_by_the_kit()
    {
        var ctor = MemberOf(Source("FocusRenderer.cs"), "public FocusRenderer(GraphicsDevice device)");
        Assert.Contains("UiKit.MakeSoftRounded(", ctor);
        Assert.Contains("UiKit.MakeSoftDisc(", ctor);
        var kit = Source("UiKit.cs");
        Assert.Contains("internal static Texture2D MakeSoftRounded(GraphicsDevice d, int radius, int feather, int mid, float line = 0f)", kit);
        Assert.Contains("internal static Texture2D MakeSoftDisc(GraphicsDevice d, int s, int feather)", kit);
    }

    // ── THE HOST ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_host_paints_no_brackets_and_no_band_scrim_anywhere()
    {
        var host = Host();
        Assert.DoesNotContain("TourBrackets(", host);
        Assert.DoesNotContain("DrawScrimAround(", host);
        Assert.DoesNotContain("TourOutline(", host);
    }

    [Fact]
    public void test_the_canvas_preserves_its_contents_across_the_lights_own_targets()
    {
        var game = Source("Game1.cs");
        var at = game.IndexOf("_canvas = new RenderTarget2D(", StringComparison.Ordinal);
        Assert.True(at >= 0, "the canvas is no longer created where it was");
        var statement = game[at..game.IndexOf(';', at)];
        Assert.Contains("RenderTargetUsage.PreserveContents", statement);
    }

    [Fact]
    public void test_the_three_surfaces_light_through_one_renderer_and_keep_their_rectangles()
    {
        var game = Source("Game1.cs");
        var opening = Source("Game1.Opening.cs");

        // The coach: its gate line and its holes exactly as they were, then the light, then the card.
        var coach = MemberOf(game, "private void DrawCoachSpotlight()");
        Assert.Contains("if (_coach.Showing is not { } id || !CoachLightsIt(id)) { _coachCard = Rectangle.Empty; return; }", coach);
        Assert.Contains("var holes = CoachHoles(id);", coach);
        Assert.Contains("FocusShapes(ScreenActivity(), OnboardingLessons.Target(id) ?? TourTarget.NavRail, holes, _focusShapes);", coach);
        Assert.Contains("DrawFocus(CoachScrimWeight *", coach);
        Assert.Contains("DrawCoachCard(id, holes);", coach);
        Assert.True(coach.IndexOf("FocusShapes(", StringComparison.Ordinal) < coach.IndexOf("DrawFocus(", StringComparison.Ordinal)
                    && coach.IndexOf("DrawFocus(", StringComparison.Ordinal) < coach.IndexOf("DrawCoachCard(", StringComparison.Ordinal),
                    "the coach resolves its shapes, lights them, then places its card on the holes");

        // A tour: the same resolver, the same card placement.
        var tour = MemberOf(game, "private void DrawTour()");
        Assert.Contains("var holes = TourSpotlights(_tourScreen, step.Target);", tour);
        Assert.Contains("FocusShapes(_tourScreen, step.Target, holes, _focusShapes);", tour);
        Assert.Contains("DrawFocus(TourScrimWeight);", tour);
        Assert.Contains("var card = TourCardRect(holes, avoid, width, height);", tour);

        // The opening: its holes, the light where there are holes, a PLAIN fill where there are none.
        var open = MemberOf(opening, "private void DrawOpening()");
        Assert.Contains("var holes = OpeningHoles();", open);
        Assert.Contains("FocusShapes(ScreenActivity(), step.Target ?? TourTarget.NavRail, holes, _focusShapes);", open);
        Assert.Contains("DrawFocus(scrim);", open);
        Assert.Contains("_ui.Fill(_batch, new Rectangle(0, 0, UiKit.Page.Width, UiKit.Page.Height), FocusRenderer.Ink * scrim);", open);
        Assert.Contains("DrawOpeningCard(step, holes);", open);

        // The forced click still reads the holes' rectangles, less the halo.
        Assert.Contains("OpeningHoles().Any(h => ClickableOf(h).Contains(ChromeMouse))", opening);
        Assert.Contains("lit.Inflate(-SpotlightHalo, -SpotlightHalo);", MemberOf(opening, "internal static Rectangle ClickableOf(Rectangle lit)"));

        // The resolver's own answers are unchanged: the halo for the eye, the sentinel for the unknown.
        var spots = MemberOf(game, "private Rectangle[] TourSpotlights(Activity screen, TourTarget target)");
        Assert.Contains("c.Inflate(SpotlightHalo, SpotlightHalo);", spots);
        Assert.Contains("if (own.Length == 0) return new[] { new Rectangle(0, 0, 1920, 1080) };", spots);
        Assert.Contains("if (screen == Activity.Hunt) return own;", spots);

        // One paint path: close the batch, build, bind the canvas, reopen — and never at weight zero.
        var focus = MemberOf(game, "private void DrawFocus(float weight)");
        Assert.Contains("if (weight > 0f && _focus.Stale(_focusShapes))", focus);
        Assert.Contains("_batch.End();", focus);
        Assert.Contains("_focus.Build(_batch, _focusShapes);", focus);
        Assert.Contains("GraphicsDevice.SetRenderTarget(_canvas);", focus);
        Assert.Contains("BeginCanvas(1);", focus);
        Assert.Contains("_focus.Draw(_batch, _focusShapes, weight);", focus);
        // The shape list is a field, not a per-frame allocation.
        Assert.Contains("private readonly List<FocusShape> _focusShapes = new();", game);
        Assert.DoesNotContain("new List<FocusShape>", Host().Replace("private readonly List<FocusShape> _focusShapes = new();", ""));
    }

    // ── THE ARENA ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_anim_sprite_draws_exactly_the_frame_resolve_frame_resolved()
    {
        var kit = Source("UiKit.cs");
        var arithmetic = new[] { "DrawScale(", "ResolveCrop(", "SidePadFraction(", "BottomPadFraction(", "% frames", "Math.Clamp(i" };

        // The plain overload forwards; the out-overload resolves, notes, draws; the resolver owns every number.
        var forward = MemberOf(kit, "public bool AnimSprite(SpriteBatch b, string stripKey, Rectangle box, float seconds, float fps, bool loop, Color tint, float topCrop = 0f, bool flip = false)");
        Assert.Contains("=> AnimSprite(b, stripKey, box, seconds, fps, loop, tint, topCrop, flip, out _);", forward);

        var draw = MemberOf(kit, "public bool AnimSprite(SpriteBatch b, string stripKey, Rectangle box, float seconds, float fps, bool loop, Color tint,\n                           float topCrop, bool flip, out SpriteFrame frame, string? placeAs = null, Vector2 squash = default)");
        // a field's crush buckles the placed frame through the resolver's own pure helper (feet on the floor), never here
        Assert.Contains("f = f with { Dest = Buckle(f.Dest, squash) };", draw);
        var buckle = MemberOf(kit, "public static Rectangle Buckle(Rectangle dest, Vector2 squash)");
        Assert.Contains("return new Rectangle(dest.Center.X - w / 2, dest.Bottom - h, w, h);", buckle);
        Assert.Contains("ResolveFrame(stripKey, box, seconds, fps, loop, topCrop, flip, placeAs)", draw);
        Assert.Contains("UiRasterLedger.Note(stripKey, f.Src.Width, f.Src.Height, f.Dest.Width, f.Dest.Height, \"UiKit.AnimSprite\");", draw);
        Assert.Contains("b.Draw(f.Texture, f.Dest, f.Src, tint, 0f, Vector2.Zero, f.Effects, 0f);", draw);
        foreach (var term in arithmetic) Assert.False(draw.Contains(term, StringComparison.Ordinal), $"AnimSprite computes `{term}` itself — a second owner of where a figure stands");

        var resolve = MemberOf(kit, "public SpriteFrame? ResolveFrame(string stripKey, Rectangle box, float seconds, float fps, bool loop, float topCrop = 0f,\n                                     bool flip = false, string? placeAs = null)");
        foreach (var term in new[] { "DrawScale(", "ResolveCrop(", "SidePadFraction(", "BottomPadFraction(", "Game1.ReducedMotion" })
            Assert.Contains(term, resolve);
        // the grounding is the resolver's own pure helper (ADR-011: an authored action is PLACED AS its idle)
        Assert.Contains("return new SpriteFrame(tex, src, PlaceFrame(box, srcW, srcH, sc, BottomPadFraction(placeAs ?? stripKey) * fw),", resolve);
        var place = MemberOf(kit, "public static Rectangle PlaceFrame(Rectangle box, int srcW, int srcH, float scale, float bottomPadTexels)");
        Assert.Contains("return new Rectangle(box.Center.X - w / 2, box.Bottom - drawnH + drop, w, drawnH);", place);
        Assert.DoesNotContain("b.Draw(", resolve);
    }

    [Fact]
    public void test_the_arena_records_the_frame_each_figure_drew_and_nothing_else()
    {
        var hunt = Source("HuntScreen.cs");
        var funnel = MemberOf(hunt, "private bool ActorSprite(SpriteBatch b, string stripKey, Rectangle box, float seconds, float fps,");
        Assert.Contains("out var frame", funnel);
        Assert.Contains("if (record is { } subject) _drawnFrames[subject] = frame;", funnel);

        var layout = MemberOf(hunt, "private void LayoutActors()");
        Assert.True(layout.IndexOf("_drawnFrames.Clear();", StringComparison.Ordinal) >= 0
                    && layout.IndexOf("_drawnFrames.Clear();", StringComparison.Ordinal) < layout.IndexOf("_actors.Publish(", StringComparison.Ordinal),
                    "last frame's figures must be forgotten before this frame's are laid out");

        // Every figure draw names its subject; the flash — the white mask through the same funnel — does not.
        foreach (var draw in new[] { "private void DrawChampion(SpriteBatch b, Rectangle box, bool dead)",
                                     "private void DrawComposition(SpriteBatch b, bool attacking, IReadOnlyList<WaveCreature> comp)",
                                     "private void DrawNormalEnemy(SpriteBatch b, bool attacking)",
                                     "private void DrawBoss(SpriteBatch b, bool attacking)",
                                     "private void DrawCreatureDeath(SpriteBatch b, int slot, Rectangle box, string? enemyKey, string? idleKey)" })
            Assert.Contains("record: VfxSubject.", MemberOf(hunt, draw));
        var flash = MemberOf(hunt, "private void FlashOver(SpriteBatch b, string? stripKey, Rectangle box, float seconds, float fps, bool loop, float strength, float crop,\n                           Vector2 squash = default)");
        Assert.DoesNotContain("record:", flash);
        Assert.DoesNotContain("VfxSubject", flash);

        // The record is preallocated and the arena answers the light through the interface.
        Assert.Contains("private readonly Dictionary<VfxSubject, SpriteFrame> _drawnFrames = new();", hunt);
        Assert.Contains("public sealed class HuntScreen : IFocusActors", hunt);
        Assert.Contains("internal bool TryDrawnFrame(VfxSubject subject, out SpriteFrame frame)", hunt);
        Assert.Contains("internal bool TryBody(VfxSubject subject, out Rectangle body)", hunt);
        Assert.Contains("internal int LaidOutCreatureCount => _creatureBoxes.Count;", hunt);
    }

    [Fact]
    public void test_no_hit_test_reads_a_mask_or_a_drawn_frame()
    {
        // The silhouette is for the eye. A pointer, a click, a hover or a forced step that consulted the
        // mask or the drawn frame would turn a reading into an alpha test, which no design here asked for.
        var signature = new Regex(@"^\s*(?:public|private|internal|protected)[^;(=]*\b(\w*(?:Hover|Hovers|Take|Input|Click|Hit|Contains|Clickable|Pointer)\w*)\s*\(", RegexOptions.Multiline);
        var forbidden = new[] { "Mask", "DrawnFrame", "_drawnFrames", "SpriteFrame" };
        var checkedMethods = 0;
        foreach (var path in Directory.GetFiles(GameDir(), "*.cs"))
        {
            var src = File.ReadAllText(path).Replace("\r\n", "\n");
            foreach (Match m in signature.Matches(src))
            {
                var body = MemberOf(src, m.Value.TrimStart());
                checkedMethods++;
                foreach (var term in forbidden)
                    Assert.False(body.Contains(term, StringComparison.Ordinal),
                                 $"{Path.GetFileName(path)}: `{m.Groups[1].Value}` reads `{term}` — an alpha-shaped hit test");
            }
        }
        Assert.True(checkedMethods >= 20, $"only {checkedMethods} input-shaped methods were found — the pattern lost the screens");
    }

    [Fact]
    public void test_the_rig_forwards_the_poses_a_filmstrip_needs()
    {
        var seq = File.ReadAllText(RepoFile("tools", "asset-pipeline", "capture_seq.sh"));
        foreach (var dial in new[] { "RH_SHOT_OPENING", "RH_SHOT_LESSON", "RH_SHOT_UISCALE" })
            Assert.Contains($"RH_ENV+=({dial}=", seq);
    }
}
