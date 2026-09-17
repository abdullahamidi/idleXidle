using System;
using System.IO;
using System.Linq;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace IdleXIdle.Game.Tests;

/// <summary>
/// ARCHETYPE SCALE IS POPULATION-INDEPENDENT. A Bruiser is visually a Bruiser whether alone or in a
/// group; a Swarm creature stays small whether alone or in a group. Population decides the room a
/// row may occupy, where each creature stands and how they are spaced — never whether the archetype's
/// multiplier applies.
/// </summary>
/// <remarks>
/// <para>
/// Pure arithmetic over <see cref="HuntScreen.CreatureRow"/>, the one creature layout. The invariant is
/// tested as a RULE — the same multiplier for the same archetype at any count — and never as one pixel
/// size, so a change to the arena's room or spacing cannot fail it and a change to the rule cannot pass.
/// </para>
/// <para>
/// The authored table is read, not restated: Bruiser above the neutral enemy box, Swarm below it,
/// exactly as <see cref="HuntScreen.ArchetypeScale"/> says. Nothing here would be changed to make a
/// test pass — the table is the design's, and this file only proves it reaches every population.
/// </para>
/// </remarks>
public class archetype_scale_population_test
{
    private static readonly Point Neutral = new(436, 440);   // EnemyBox, the unscaled box a lone creature used to fill

    private static readonly Archetype[] Normal = Enum.GetValues<Archetype>();

    private static Rectangle Lone(Archetype a) => HuntScreen.CreatureRow(a, 1, 0f, 0f, 0f).Boxes[0];

    private static Rectangle[] Pack(Archetype a, int count) => HuntScreen.CreatureRow(a, count, 0f, 0f, 0f).Boxes;

    [Fact]
    public void test_archetype_scale_a_lone_creature_wears_the_same_box_as_each_creature_of_its_pack()
    {
        foreach (var a in Normal)
            foreach (var count in new[] { 2, 3, 5 })
            {
                var lone = Lone(a);
                var pack = Pack(a, count);

                Assert.All(pack, box => Assert.Equal(lone.Size, box.Size));
                Assert.Equal(HuntScreen.ArchetypeBox(a), lone.Size);
            }
    }

    [Fact]
    public void test_archetype_scale_the_same_multiplier_applies_at_every_population()
    {
        // The rule, not a pixel: the box height over the neutral height is the archetype's multiplier,
        // and it is the same number at one creature as at five.
        foreach (var a in Normal)
        {
            var expected = HuntScreen.ArchetypeScale(a);
            foreach (var count in new[] { 1, 2, 5 })
            {
                var box = Pack(a, count)[0];
                Assert.Equal(expected, box.Height / (float)Neutral.Y, 0.01);
                Assert.Equal(expected, box.Width / (float)Neutral.X, 0.01);
            }
        }
    }

    [Fact]
    public void test_archetype_scale_a_bruiser_is_larger_than_neutral_and_a_swarm_smaller_alone_or_packed()
    {
        // Read off the authored table through the layout, at both populations.
        foreach (var count in new[] { 1, 3 })
        {
            var bruiser = Pack(Archetype.Bruiser, count)[0];
            var swarm = Pack(Archetype.Swarm, count)[0];

            Assert.True(bruiser.Height > Neutral.Y && bruiser.Width > Neutral.X, $"a Bruiser of {count} is not larger than the neutral box");
            Assert.True(swarm.Height < Neutral.Y && swarm.Width < Neutral.X, $"a Swarm of {count} is not smaller than the neutral box");
            Assert.True(bruiser.Height > swarm.Height);
        }
        Assert.True(HuntScreen.ArchetypeScale(Archetype.Bruiser) > 1f);
        Assert.True(HuntScreen.ArchetypeScale(Archetype.Swarm) < 1f);
    }

    [Fact]
    public void test_archetype_scale_every_creature_stands_on_the_same_plane_at_rest()
    {
        // Grounding: bottom-anchored on the enemy box's floor whatever the archetype or the count. The
        // row's TopY is the resting top (the plane minus the box height); each figure then bobs about
        // it on its own phase, never more than the bob's seven pixels.
        const int Bob = 7;
        var plane = HuntScreen.CreatureRow(Archetype.Bruiser, 1, 0f, 0f, 0f).TopY + HuntScreen.ArchetypeBox(Archetype.Bruiser).Y;
        foreach (var a in Normal)
            foreach (var count in new[] { 1, 4 })
            {
                var row = HuntScreen.CreatureRow(a, count, 0f, 0f, 0f);
                Assert.Equal(plane, row.TopY + HuntScreen.ArchetypeBox(a).Y);
                Assert.All(row.Boxes, box => Assert.InRange(box.Bottom, plane - Bob, plane + Bob));
            }
    }

    /// <summary>
    /// THE WIRING, not just the function. <see cref="HuntScreen.CreatureRow"/> is pure and the tests
    /// above prove it, but the screen's dispatch is private — and a lone-creature path re-introduced
    /// beside it would pass every test in this file while the arena drew the old unscaled box: the
    /// project's named failure mode, built-tested-green code that does not run. So the dispatch is
    /// pinned in the source, the way the VFX contract pins its own seams. The measurement behind it is
    /// the six lone/pack dumps of hunt_geometry_fixtures.sh (Bruiser 403x471, Swarm 209x244, Armoured
    /// 331x387 — identical alone and packed, 2026-09-07).
    /// </summary>
    [Fact]
    public void test_archetype_scale_the_lone_creature_is_dispatched_through_the_one_row_layout()
    {
        var source = HuntScreenSource();
        var code = string.Join("\n", source.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));

        Assert.DoesNotContain("LayoutSingleEnemy", code);
        Assert.Contains("else LayoutComposition(comp.Count)", code);
        Assert.Contains("CreatureRow(WaveArchetype, count", code);
    }

    /// <summary>The screen's source, found by walking up from the test binary to the repo root (the VFX contract's own idiom).</summary>
    private static string HuntScreenSource()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "IdleXIdle.Game", "HuntScreen.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("src/IdleXIdle.Game/HuntScreen.cs not found above the test binary.");
    }

    [Fact]
    public void test_archetype_scale_population_changes_placement_and_spacing_not_size()
    {
        // What population IS allowed to change: where the creatures stand. Five Bruisers are compressed
        // into the arena's room; five Swarm creatures spread. Their sizes do not move.
        var five = Pack(Archetype.Bruiser, 5);
        var centres = five.Select(b => b.Center.X).ToArray();
        Assert.True(centres.Zip(centres.Skip(1)).All(p => p.Second > p.First), "a row must run left to right");
        Assert.Single(five.Select(b => b.Size).Distinct());
        Assert.NotEqual(Lone(Archetype.Bruiser).Center.X, five[0].Center.X);
    }
}
