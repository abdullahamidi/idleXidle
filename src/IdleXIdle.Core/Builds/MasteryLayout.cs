using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Builds;

/// <summary>A point in the mastery tree's own coordinate space. Zoom, pan and window know nothing of it.</summary>
public readonly record struct TreePoint(float X, float Y);

/// <summary>
/// Where every mastery node sits, in world units — a pure function of the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// <b>The path redesign (2026-09-06).</b> The tree used to be rings: every node of a ring fanned
/// around its branch's axis, and a node's "any of these" prerequisites were drawn as one wire to
/// the NEAREST (preferring a taken one), so buying a node re-routed lines the player had planned
/// against. The layout is authored now. A branch is a trunk on its axis, a fork, two named ROUTES
/// that run outward at a fixed angle either side of the axis, and a capstone on the axis past
/// them; a specialisation is a leaf hung further from the axis off its route's last notable; a
/// bridge sits at the midpoint angle between two branches. Every node's place is its
/// <see cref="MasteryNode.Route"/> and <see cref="MasteryNode.Step"/>, which are catalogue data.
/// </para>
/// <para>
/// <b>Nothing here reads the player's allocation.</b> <see cref="Edges"/> is the whole drawn graph —
/// one edge per (node, parent) — and it is the same list whatever is taken. The topology test holds
/// both facts.
/// </para>
/// </remarks>
public static class MasteryLayout
{
    /// <summary>The rim: past the farthest capstone, with room for the branch headers beyond it.</summary>
    public const float WorldRadius = 3420f;

    /// <summary>The trunk: its first node's radius, and the step between trunk nodes.</summary>
    public const float TrunkStart = 400f;
    public const float TrunkStep = 260f;

    /// <summary>A route: its first node's radius, and the step between route nodes.</summary>
    public const float RouteStart = 900f;
    public const float RouteStep = 325f;

    /// <summary>How far a route runs from its branch's axis, and how much further a specialisation leaf hangs.</summary>
    public const float RouteDegrees = 16f;
    public const float LeafDegrees = 10f;

    /// <summary>A bridge sits at the midpoint angle between its two branches, here.</summary>
    public const float BridgeRadius = 1100f;

    public static float AngleOf(Branch b) => b switch
    {
        Branch.Resonance => -90f,
        Branch.Tempo => 0f,
        Branch.Loot => 90f,
        _ => 180f,
    } * MathF.PI / 180f;

    /// <summary>A node's drawn radius in world units, with the 1.9 draw factor folded in.</summary>
    /// <remarks>
    /// A quarter larger than the sizes the ring tree drew (2026-09-06): the world grew from a 2000
    /// rim to 3420 so that a route reads as a road, and the whole-tree framing zooms out by the same
    /// amount — at the old sizes a minor was six pixels there. The spacing constants above were
    /// chosen against THESE radii (a step is a skill node, a notable and a hand's width); the layout
    /// test holds every pair apart.
    /// </remarks>
    public static float NodeWorldRadius(MasteryKind k) => 1.9f * k switch
    {
        MasteryKind.Start => 88f,
        MasteryKind.Mastery => 115f,
        MasteryKind.Specialisation => 85f,
        MasteryKind.SkillRoad => 78f,
        MasteryKind.Greater => 70f,
        MasteryKind.Bridge => 61f,
        MasteryKind.Notable => 58f,
        _ => 41f,
    };

    /// <summary>The world radius a fresh game opens on: the trunk's first node and nothing further.</summary>
    public static float FirstOpenRadius => TrunkStart + NodeWorldRadius(MasteryKind.Minor);

    public static TreePoint PositionOf(MasteryNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Kind == MasteryKind.Start) return new TreePoint(0f, 0f);

        if (node.Link is { } link)
        {
            var mid = (AngleOf(node.Branch) + AngleOf(link)) / 2f;
            if (MathF.Abs(AngleOf(node.Branch) - AngleOf(link)) > MathF.PI) mid += MathF.PI;
            return Polar(BridgeRadius, mid);
        }

        var axis = AngleOf(node.Branch);
        var side = node.Route == MasteryRoute.Left ? -1f : node.Route == MasteryRoute.Right ? 1f : 0f;
        return node.Route switch
        {
            MasteryRoute.Trunk => Polar(TrunkStart + TrunkStep * node.Step, axis),
            MasteryRoute.Capstone => Polar(RouteStart + RouteStep * node.Step, axis),
            _ => Polar(RouteStart + RouteStep * node.Step,
                       axis + side * (RouteDegrees + (node.Kind == MasteryKind.Specialisation ? LeafDegrees : 0f)) * MathF.PI / 180f),
        };
    }

    /// <summary>The drawn graph: one edge per (parent, node), from the catalogue alone.</summary>
    public static IReadOnlyList<(string From, string To)> Edges { get; } = BuildEdges();

    private static IReadOnlyList<(string From, string To)> BuildEdges()
    {
        var edges = new List<(string, string)>();
        foreach (var node in MasteryCatalog.Nodes)
            foreach (var pre in node.Prereqs.Concat(node.SecondPrereqs))
                edges.Add((pre, node.Id));
        return edges;
    }

    private static TreePoint Polar(float radius, float angle)
        => new(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
}
