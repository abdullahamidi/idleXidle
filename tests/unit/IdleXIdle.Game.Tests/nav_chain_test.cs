using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Progression;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// A LOCKED RAIL TILE IS CHAINED SHUT ON TWO DIAGONALS, AND THE CHAINS SPRING OFF ALONG THEIR OWN RUNS.
/// </summary>
/// <remarks>
/// <para>
/// The playtest rejected a ring around the icon and then a straight belt across the tile, and what it
/// asked for instead is GEOMETRY: two diagonal runs on different slopes, crossing off-centre, spanning
/// the button. So this file pins geometry at all three density profiles, never a pixel coordinate a
/// retune would break — and it cannot say whether the tile LOOKS chained shut. That is what the capture
/// set in production/qa/evidence/chains/ is for; a passing run here is necessary, not sufficient.
/// </para>
/// </remarks>
public class NavChainTests
{
    /// <summary>The rail stacks eleven tiles on the 1080 px canvas (Game1.NavTileHeight).</summary>
    private const int TileHeight = 1080 / 11;

    /// <summary>What "clearly diagonal" means: well away from a belt (0°) and from a post (90°).</summary>
    private const float ShallowestDiagonal = 15f, SteepestDiagonal = 75f;

    private readonly ITestOutputHelper _out;

    public NavChainTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Profiles() => new[] { new object[] { 100 }, new object[] { 125 }, new object[] { 150 } };

    /// <summary>A rail tile as Game1.NavHexRect lays it out — slot 5 by default, so nothing passes by sitting at the canvas origin.</summary>
    private static Rectangle Tile(int slot = 5) => new(0, slot * TileHeight, Game1.NavRailWidth, TileHeight);

    private static List<NavChainPiece> Pose(Rectangle tile, float progress, bool reduced = false)
    {
        var pieces = new List<NavChainPiece>();
        NavChain.Compose(tile, NavChain.LinkLength, progress, reduced, pieces);
        return pieces;
    }

    private static NavChainRun RunOf(Rectangle tile, int run) => NavChain.Run(tile, NavChain.LinkLength, run);

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>Where two runs' lines meet.</summary>
    private static Vector2 Meet(NavChainRun a, NavChainRun b)
    {
        var d1 = a.End - a.Start;
        var d2 = b.End - b.Start;
        return a.Start + Cross(b.Start - a.Start, d2) / Cross(d1, d2) * d1;
    }

    /// <summary>The box a set of pieces covers, every piece's rotated extent included.</summary>
    private static (Vector2 Lo, Vector2 Hi) Bounds(IEnumerable<NavChainPiece> pieces)
    {
        var lo = new Vector2(float.MaxValue);
        var hi = new Vector2(float.MinValue);
        foreach (var p in pieces)
        {
            lo = Vector2.Min(lo, p.Centre - p.HalfExtent);
            hi = Vector2.Max(hi, p.Centre + p.HalfExtent);
        }
        return (lo, hi);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_a_locked_tile_is_bound_by_two_diagonal_runs_on_different_slopes(int percent)
    {
        UiMetrics.Apply(percent);
        var tile = Tile();
        var strong = RunOf(tile, 0);
        var other = RunOf(tile, 1);
        _out.WriteLine($"{percent}%: strong run {strong.SlopeDegrees:F1}°, other run {other.SlopeDegrees:F1}°, link {NavChain.LinkLength} px");

        Assert.True(NavChain.RunCount >= 2, "one run is a belt, not a binding");
        foreach (var run in new[] { strong, other })
            Assert.InRange(MathF.Abs(run.SlopeDegrees), ShallowestDiagonal, SteepestDiagonal);
        // The two diagonals of a door — top-left to lower-right, lower-left to upper-right — not two of the same one.
        Assert.True(strong.SlopeDegrees > 0f, $"the strong run should fall to the right ({strong.SlopeDegrees:F1}°)");
        Assert.True(other.SlopeDegrees < 0f, $"the other run should climb to the right ({other.SlopeDegrees:F1}°)");
        // ASYMMETRIC: a mirrored X is a logo, not a restraint.
        Assert.True(MathF.Abs(MathF.Abs(strong.SlopeDegrees) - MathF.Abs(other.SlopeDegrees)) >= 5f,
                    $"the runs mirror each other ({strong.SlopeDegrees:F1}° / {other.SlopeDegrees:F1}°)");

        var meet = Meet(strong, other);
        Assert.True(NavChain.OnTile(tile, meet), $"the runs cross off the tile, at {meet}");
        var offCentre = Vector2.Distance(meet, tile.Center.ToVector2());
        Assert.InRange(offCentre, 3f, tile.Width * 0.25f);   // slightly off-centre: not dead centre, not in a corner

        // Every link of a run lies ON its line and ALONG it, and face-on and edge-on links alternate.
        var pieces = Pose(tile, 0f);
        for (var r = 0; r < NavChain.RunCount; r++)
        {
            var line = RunOf(tile, r);
            var links = pieces.Where(p => p.Run == r).ToList();
            Assert.True(links.Count >= 6, $"run {r} holds {links.Count} links at {percent}%");
            foreach (var link in links)
            {
                Assert.True(MathF.Abs(Cross(link.Rest - line.Start, line.Direction)) < 0.5f, $"run {r}: a link at {link.Rest} is off its line");
                Assert.InRange(MathHelper.ToDegrees(link.Rotation) - line.SlopeDegrees, -5f, 5f);
            }
            Assert.Contains(links, l => l.Sprite == NavChainSprite.FaceLink);
            Assert.Contains(links, l => l.Sprite == NavChainSprite.EdgeLink);
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_runs_span_the_whole_tile_not_the_icon(int percent)
    {
        UiMetrics.Apply(percent);
        var tile = Tile();
        var links = Pose(tile, 0f).Where(p => p.Run >= 0).ToList();
        var (lo, hi) = Bounds(links);
        var wide = (hi.X - lo.X) / tile.Width;
        var tall = (hi.Y - lo.Y) / tile.Height;
        _out.WriteLine($"{percent}%: the chains cover {wide:P0} of the tile's width and {tall:P0} of its height");
        // The icon box is under a quarter of the tile's width; the binding is on the BUTTON.
        Assert.True(wide >= 0.85f, $"the chains cover only {wide:P0} of the tile's width at {percent}%");
        Assert.True(tall >= 0.75f, $"the chains cover only {tall:P0} of the tile's height at {percent}%");

        for (var r = 0; r < NavChain.RunCount; r++)
        {
            var (rlo, rhi) = Bounds(links.Where(p => p.Run == r));
            Assert.True((rhi.X - rlo.X) / tile.Width >= 0.6f, $"run {r} crosses only {(rhi.X - rlo.X) / tile.Width:P0} of the tile at {percent}%");
            // Each run reaches the tile's edges at both ends — within one link of an edge, entry and exit.
            var reach = NavChain.LinkLength;
            Assert.True(rlo.X - tile.Left <= reach || rlo.Y - tile.Top <= reach || tile.Bottom - rhi.Y <= reach,
                        $"run {r} stops short of the edge it enters by ({rlo})");
            Assert.True(tile.Right - rhi.X <= reach || tile.Bottom - rhi.Y <= reach || rlo.Y - tile.Top <= reach,
                        $"run {r} stops short of the edge it leaves by ({rhi})");
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_no_piece_lies_off_the_tile_at_rest(int percent)
    {
        UiMetrics.Apply(percent);
        foreach (var slot in new[] { 0, 5, 10 })   // the top tile, a middle one, the bottom one
        {
            var tile = Tile(slot);
            var pose = Pose(tile, 0f);
            Assert.NotEmpty(pose);
            foreach (var p in pose)
            {
                Assert.True(NavChain.OnTile(tile, p.Centre), $"{p.Sprite} centre {p.Centre} is off {tile}");
                var h = p.HalfExtent;
                // WHOLLY on it, not just its centre: a bound tile never paints on its neighbour.
                Assert.True(p.Centre.X - h.X >= tile.Left - 0.01f && p.Centre.X + h.X <= tile.Right + 0.01f
                            && p.Centre.Y - h.Y >= tile.Top - 0.01f && p.Centre.Y + h.Y <= tile.Bottom + 0.01f,
                            $"{p.Sprite} at {p.Centre} (half extent {h}) crosses the edge of {tile} at {percent}%");
                Assert.Equal(p.Rest, p.Centre);
                Assert.True(p.Alpha > 0.5f, $"a bound {p.Sprite} is drawn at {p.Alpha:F2}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_pieces_that_leave_the_tile_during_the_break_are_not_drawn(int percent)
    {
        UiMetrics.Apply(percent);
        var tile = Tile();
        var bound = Pose(tile, 0f).Count;
        var thrownOff = false;
        for (var step = 1; step < 100; step++)
        {
            var t = step / 100f;
            var pose = Pose(tile, t);
            foreach (var p in pose)
                Assert.True(NavChain.OnTile(tile, p.Centre), $"at {t:F2} a {p.Sprite} of run {p.Run} is drawn off the tile, at {p.Centre}");
            if (pose.Count < bound - NavChain.RunCount) thrownOff = true;
        }
        Assert.True(thrownOff, "the break never throws a piece off the tile — then it is not a break");
        Assert.Empty(Pose(tile, 1f));
        Assert.Empty(Pose(tile, 1f, reduced: true));
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_break_springs_each_half_back_along_its_own_run(int percent)
    {
        UiMetrics.Apply(percent);
        var tile = Tile();
        var dirs = new[] { RunOf(tile, 0).Direction, RunOf(tile, 1).Direction };

        // THE FIRST FRAME ALREADY SHOWS THE BREAK: both halves of both runs, and a gap opening between them.
        var first = Pose(tile, 0.03f);
        for (var r = 0; r < NavChain.RunCount; r++)
        {
            Assert.Contains(first, p => p.Run == r && p.Side < 0);
            Assert.Contains(first, p => p.Run == r && p.Side > 0);
        }

        foreach (var t in new[] { 0.03f, 0.1f, 0.25f, 0.5f, 0.75f })
        {
            var moved = 0;
            foreach (var p in Pose(tile, t))
            {
                var d = p.Centre - p.Rest;
                if (p.Run < 0)
                {
                    // THE PADLOCK only ever falls — straight down, out from under the crossing.
                    Assert.Equal(0f, d.X, 3);
                    Assert.True(d.Y >= 0f, $"at {t:F2} the padlock rises ({d})");
                    continue;
                }
                if (p.Side == 0)
                {
                    Assert.Equal(Vector2.Zero, d);   // the snapped link bursts where it was
                    continue;
                }
                Assert.True(d.Length() > 0f, $"at {t:F2} a piece of run {p.Run} has not moved");
                var along = Vector2.Dot(d, dirs[p.Run]);
                var across = MathF.Abs(Cross(d, dirs[p.Run]));
                Assert.True(across <= 0.01f * d.Length() + 1e-3f, $"at {t:F2} a piece of run {p.Run} moved {d}, off its run's vector {dirs[p.Run]}");
                // Toward ITS OWN edge: the half before the snap back the way the run came in, the rest on out.
                Assert.Equal(p.Side, Math.Sign(along));
                moved++;
            }
            if (t <= 0.25f) Assert.True(moved > 0, $"nothing is moving at {t:F2}");
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_reduced_motion_breaks_the_chain_without_moving_it(int percent)
    {
        UiMetrics.Apply(percent);
        var tile = Tile();
        var bound = Pose(tile, 0f, reduced: true);
        var last = float.MaxValue;
        foreach (var t in new[] { 0.03f, 0.5f, 0.9f })
        {
            var pose = Pose(tile, t, reduced: true);
            Assert.NotEmpty(pose);
            foreach (var p in pose) Assert.Equal(p.Rest, p.Centre);   // nothing moves, the padlock included
            // THE GAP IS THE EVENT: the snapped links are gone from the first frame, and nothing else is.
            Assert.DoesNotContain(pose, p => p.Run >= 0 && p.Side == 0);
            Assert.Equal(bound.Count - NavChain.RunCount, pose.Count);
            var brightest = pose.Max(p => p.Alpha);
            Assert.True(brightest < last, $"at {t:F2} the chain is not fading ({brightest:F2})");
            last = brightest;
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void test_the_padlock_is_small_and_hangs_from_the_crossing(int percent)
    {
        UiMetrics.Apply(percent);
        var tile = Tile();
        var meet = Meet(RunOf(tile, 0), RunOf(tile, 1));
        var clasp = Pose(tile, 0f).Single(p => p.Sprite == NavChainSprite.Clasp);
        // NOT A GIANT PADLOCK PRETENDING TO BE THE RESTRAINT: narrower than a link, under a third of the tile.
        Assert.True(clasp.Length < NavChain.LinkLength, $"the padlock is {clasp.Length:F0} px wide against a {NavChain.LinkLength} px link");
        Assert.True(clasp.Thickness <= tile.Height / 3f, $"the padlock is {clasp.Thickness:F0} px tall on a {tile.Height} px tile");
        // Its shackle reaches up round the crossing it holds.
        Assert.True(clasp.Centre.Y - clasp.Thickness / 2f <= meet.Y, "the padlock hangs below the crossing, not from it");
        Assert.True(Vector2.Distance(clasp.Centre, meet) <= clasp.Thickness, "the padlock is not at the crossing");

        var falling = Pose(tile, 0.3f).Single(p => p.Sprite == NavChainSprite.Clasp);
        Assert.True(falling.Centre.Y > clasp.Centre.Y, "the padlock does not drop when the chain snaps");
    }

    [Fact]
    public void test_every_piece_is_drawn_at_its_arts_own_aspect()
    {
        // NO STRETCHED CHAIN: a link is scaled whole, never pulled to fit a length — so every piece of one
        // kind has the same shape, the bursting link included.
        UiMetrics.Apply(100);
        var tile = Tile();
        foreach (var t in new[] { 0f, 0.05f, 0.4f })
            foreach (var p in Pose(tile, t))
            {
                var art = p.Sprite switch
                {
                    NavChainSprite.FaceLink => NavChain.FaceAspect,
                    NavChainSprite.EdgeLink => NavChain.EdgeAspect,
                    _ => NavChain.ClaspAspect,
                };
                Assert.Equal(art, p.Thickness / p.Length, 4);
            }
    }

    [Fact]
    public void test_the_break_is_armed_once_when_a_screen_opens_and_never_by_a_reload()
    {
        // Game1 arms the break in one place: for each activity Reveal.Newly returns on the frame its
        // gate opens. So "once per screen per career" is Reveal's promise, held here for the two
        // screens the opening breaks first.
        var revealed = Reveal.Restore(Array.Empty<string>(), new UnlockFacts());
        var facts = new UnlockFacts(WavesCleared: 5, DeepestWave: 5, ItemsOwned: 1, ChestsEverHeld: 1);

        var armed = Reveal.Newly(revealed, facts);
        Assert.Contains(Activity.Gear, armed);
        Assert.Contains(Activity.Vault, armed);
        Assert.Equal(armed.Count, armed.Distinct().Count());

        // The next frame arms nothing: one break, not one a frame.
        Assert.Empty(Reveal.Newly(revealed, facts));
        // A reload of a career whose screens are open replays nothing...
        var reloaded = Reveal.Restore(Reveal.Names(revealed), facts);
        Assert.Contains(Activity.Gear, reloaded);
        Assert.Contains(Activity.Vault, reloaded);
        Assert.Empty(Reveal.Newly(reloaded, facts));
        // ...and nor does a save from before the list, whose gates were already open: those tiles load unbound.
        Assert.Empty(Reveal.Newly(Reveal.Restore(Array.Empty<string>(), facts), facts));
    }
}
