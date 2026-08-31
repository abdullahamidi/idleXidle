using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace IdleXIdle.Core.Prestige;

/// <summary>
/// Where every trait node sits in the diagram, in WORLD units — pixels at a camera zoom of 1.
/// </summary>
/// <remarks>
/// <para>
/// AUTHORED FIRST, DERIVED FOR THE REST. The mastery tree derives its positions from branch and ring
/// because it is a regular shape; the trait tree is not regular — one spine of five unequal chains with
/// four roads climbing off three different anchors — so its shape is authored, node by node, in LANES
/// (columns) and ROWS (rungs). A table like that has one failure mode, and the tree shipped with it:
/// the CHARGE spur added four keystones to the catalogue and nobody added four rows here, so the
/// screen early-returned on a missing position and ks_rend, ks_dynamo, ks_lodestone and ks_capacitor
/// were invisible for as long as they existed — while the header counted them in its "0/39". Four
/// nodes a player could not buy because they could not see them.
/// </para>
/// <para>
/// So authoring is no longer the only source. The constructor ends by walking the catalogue, and any
/// node without an authored position is PLACED: beside its nearest positioned prerequisite if it has
/// one, in the first free cell, or along the bottom row if it is a root. Every catalogue node therefore
/// has a position by construction, and <see cref="Unauthored"/> names the ones that were placed by the
/// fallback so a debug overlay — and a reader — can see which still want a hand-chosen home.
/// </para>
/// <para>
/// The shape is A TREE, drawn as one: four boughs above a ground line, roots below it. The four
/// keystone strands stand at lanes 1, 4, 7 and 10, every gap equal, and each road's side strand
/// mirrors its partner's (RUIN|AEGIS outward around the shared trunk at 2.5; ARTIFICE|AVARICE around
/// 8.5).
/// </para>
/// <list type="bullet">
/// <item>THE ROOTS: every spine chain hangs below row 0 — the capacity trunk under the RUIN/AEGIS
///   fork, the recall taproot at dead centre, the vow and ledger trunks directly under the road
///   each one feeds. Cheap, structural, everyone grows them: they are drawn under the ground line
///   because that is what they are.</item>
/// <item>The four BOUGHS climb UPWARD from the root that gates each one. Each is TWO strands: the
///   keystone strand straight up from its anchor, and beside it the side strand — the CHARGE spur
///   on the first rung, the three small attribute nodes above — joined by short horizontal wires.
///   RUIN and AEGIS share one trunk and fork from it; ARTIFICE and AVARICE rise straight.</item>
/// <item>Every TERMINAL is the crown of its bough, all four at the same height, evenly spaced, with
///   the road's name, its one-line identity and its price above it.</item>
/// </list>
/// <para>
/// WORLD UNITS, NOT A FITTED SCALE. This table used to be in abstract lane/row units that the screen
/// squeezed into a fixed panel. The playtest of 2026-08-25 asked for the tree to be a free screen like
/// the mastery tree, so the units are now pixels at zoom 1 — a node is drawn at a fixed world size and
/// the camera decides how much of the world is on screen. Lanes are wider than rows because every node
/// prints its NAME under itself in short lines: <see cref="NodeWidth"/> and <see cref="NodeHeight"/> are
/// the room a node and its name are allowed, and a test holds every pair of nodes at least that far
/// apart, which is what "no two nodes overlap" means at any zoom. Lives in Core, with no engine types,
/// precisely so that test can exist.
/// </para>
/// </remarks>
public static class TraitTreeLayout
{
    /// <summary>One rung — the diagram's unit of height, in world units.</summary>
    public const float ChainStep = 190f;

    /// <summary>One lane — the diagram's unit of width, in world units. Wider than a rung, for the names.</summary>
    public const float LaneStep = 190f;

    /// <summary>
    /// The widest a node and its name plate may be drawn, in world units. Held under <see cref="LaneStep"/>
    /// so two nodes in neighbouring lanes never touch.
    /// </summary>
    public const float NodeWidth = 176f;

    /// <summary>
    /// The tallest a node and its name lines may be drawn, in world units — the largest frame plus three
    /// short lines of name. Held under <see cref="ChainStep"/> so a name never runs into the rung below.
    /// </summary>
    public const float NodeHeight = 184f;

    /// <summary>Every catalogue node's position, by id.</summary>
    public static IReadOnlyDictionary<string, Vector2> Positions { get; }

    /// <summary>
    /// Catalogue ids that had NO authored position and were placed by the fallback. Empty is the goal;
    /// non-empty is a to-do, not a bug — those nodes still draw.
    /// </summary>
    public static IReadOnlyList<string> Unauthored { get; }

    static TraitTreeLayout()
    {
        var authored = Author();
        Unauthored = Derive(authored, MemoryDustTree.Catalog);
        Positions = authored;
    }

    /// <summary>A lane/row pair in world units.</summary>
    private static Vector2 At(float lane, int row) => new(lane * LaneStep, row * ChainStep);

    private static Dictionary<string, Vector2> Author()
    {
        var p = new Dictionary<string, Vector2>();

        // ── THE ROOTS, below the ground line (rows 0..3) ──────────────────────────────────────────
        // The capacity trunk, at lane 2.5 — dead centre under the RUIN/AEGIS fork it feeds. The quiet
        // mark hangs at its foot: it wants one node from the head of every road plus the third socket,
        // so it has long wires wherever it stands; directly under socket_3, the one wire the drawing
        // shows is a short vertical, which is what keeps a five-way node from being a spider.
        p["socket_2"] = At(2.5f, 0);
        p["weave_5"] = At(2.5f, 1);
        p["socket_3"] = At(2.5f, 2);
        p["attunement"] = At(2.5f, 3);

        // Recall — the taproot. It gates nothing and everything else is symmetric around it, so it
        // takes the exact centre (the canopy's middle gap is above it) and runs straight down.
        p["recall_1"] = At(5.5f, 0);
        p["recall_2"] = At(5.5f, 1);
        p["recall_3"] = At(5.5f, 2);
        p["recall_4"] = At(5.5f, 3);

        // Vows, at lane 7 — directly under the ARTIFICE strand they gate, so the anchor wire is a
        // plain vertical. The chain forks at its end: GEAR-SLOT straight down, SACRIFICE a step aside.
        p["vow_study_1"] = At(7, 0);
        p["vow_study_2"] = At(7, 1);
        p["vow_study_3"] = At(7, 2);
        p["vow_binding"] = At(7, 3);
        p["vow_sacrifice"] = At(8, 3);

        // The ledger, at lane 10 — directly under AVARICE, and spreading like the root it is: the
        // filters branch a lane inward, the forge runs straight down and forks outward.
        p["ledger"] = At(10, 0);
        p["filter_common"] = At(9, 1);
        p["filter_uncommon"] = At(9, 2);
        p["forge_insight"] = At(10, 1);
        p["efficient_forge"] = At(10, 2);
        p["auto_merge"] = At(11, 2);

        // ── THE FOUR BOUGHS, climbing (rows -1..-4) ───────────────────────────────────────────────
        // Keystone strands at lanes 1, 4, 7, 10 — every gap equal, all four crowns level. Each side
        // strand carries the CHARGE spur on the first rung and the three attribute nodes above it,
        // each beside the rung it hangs off. The strands mirror in pairs: RUIN|AEGIS face outward
        // around their shared trunk, ARTIFICE|AVARICE around the plaza between them.
        Road(p, lane: 1, side: 0,
             new[] { "ks_glass_cannon", "ks_bloodlust", "ks_blood_magic", "ks_reaper" },
             new[] { "ks_rend", "ruin_edge_1", "ruin_edge_2", "ruin_edge_3" });
        Road(p, lane: 4, side: 5,
             new[] { "ks_ironclad", "ks_juggernaut", "ks_undying", "ks_titan" },
             new[] { "ks_dynamo", "aegis_skin_1", "aegis_skin_2", "aegis_skin_3" });
        Road(p, lane: 7, side: 6,
             new[] { "ks_echo", "ks_venomancer", "artifice_vows", "ks_weaver" },
             new[] { "ks_capacitor", "artifice_hands_1", "artifice_hands_2", "artifice_hands_3" });
        Road(p, lane: 10, side: 11,
             new[] { "ks_greed", "ks_discerning_eye", "ks_fortune", "ks_hoarder" },
             new[] { "ks_lodestone", "avarice_purse_1", "avarice_purse_2", "avarice_purse_3" });

        return p;
    }

    /// <summary>A road: four keystone rungs climbing from row -1, and a side node beside each rung.</summary>
    private static void Road(Dictionary<string, Vector2> p, float lane, float side, string[] rungs, string[] beside)
    {
        for (var i = 0; i < rungs.Length; i++) p[rungs[i]] = At(lane, -(i + 1));
        for (var i = 0; i < beside.Length; i++) p[beside[i]] = At(side, -(i + 1));
    }

    /// <summary>
    /// Give every catalogue node without an authored position one, and report which they were.
    /// </summary>
    /// <remarks>
    /// Beside the nearest positioned prerequisite, in the first free cell of a short ring around it —
    /// right, left, above, below, then the diagonals — or, for a root, along the bottom row past the
    /// last occupied lane. Deterministic, so the same catalogue always draws the same picture. Nodes
    /// whose prerequisites are themselves unplaced wait for a later pass; a node nothing can anchor
    /// (which the tree's own cycle check makes impossible) would fall to the bottom row.
    /// </remarks>
    private static IReadOnlyList<string> Derive(Dictionary<string, Vector2> p, IReadOnlyList<MemoryDustUnlock> catalog)
    {
        var placed = new List<string>();
        var pending = catalog.Where(u => !p.ContainsKey(u.Id)).ToList();
        if (pending.Count == 0) return placed;

        var taken = new HashSet<(int, int)>(p.Values.Select(Cell));

        // A few passes: each pass places everything whose prerequisite already has a home.
        for (var pass = 0; pass < 8 && pending.Count > 0; pass++)
        {
            foreach (var u in pending.ToList())
            {
                var anchor = u.Requires.Where(p.ContainsKey).Select(id => p[id]).Cast<Vector2?>().FirstOrDefault();
                if (anchor is null && u.Requires.Count > 0 && pass < 7) continue;   // wait for its prerequisite

                var spot = anchor is { } a ? FirstFreeAround(a, taken) : NextAlongTheBottom(taken);
                p[u.Id] = spot;
                taken.Add(Cell(spot));
                placed.Add(u.Id);
                pending.Remove(u);
            }
        }

        return placed;
    }

    private static (int, int) Cell(Vector2 v) => ((int)MathF.Round(v.X / LaneStep * 2f), (int)MathF.Round(v.Y / ChainStep * 2f));

    private static Vector2 FirstFreeAround(Vector2 a, HashSet<(int, int)> taken)
    {
        var ring = new[]
        {
            (1f, 0), (-1f, 0), (0f, -1), (0f, 1), (1f, -1), (-1f, -1), (1f, 1), (-1f, 1),
            (2f, 0), (-2f, 0), (2f, -1), (-2f, -1), (2f, 1), (-2f, 1),
        };
        foreach (var (dl, dr) in ring)
        {
            var v = new Vector2(a.X + dl * LaneStep, a.Y + dr * ChainStep);
            if (!taken.Contains(Cell(v))) return v;
        }
        // A neighbourhood this crowded does not exist in the catalogue; fall to the bottom row.
        return NextAlongTheBottom(taken);
    }

    private static Vector2 NextAlongTheBottom(HashSet<(int, int)> taken)
    {
        var bottom = taken.Count == 0 ? 0 : taken.Max(c => c.Item2) / 2;
        var lane = (taken.Count == 0 ? 0 : taken.Max(c => c.Item1) / 2) + 1;
        Vector2 v;
        do { v = new Vector2(lane * LaneStep, bottom * ChainStep); lane++; } while (taken.Contains(Cell(v)));
        return v;
    }

    /// <summary>
    /// Does every one of these ids have a position? True for the whole catalogue by construction;
    /// a screen drawing a custom tree (a test fixture) can ask before it trusts the table.
    /// </summary>
    public static bool Covers(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.All(Positions.ContainsKey);
    }

    /// <summary>The diagram's extent — node CENTRES, not their frames — so a view can fit it without hard-coding one.</summary>
    public static (Vector2 Min, Vector2 Max) Bounds()
    {
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var v in Positions.Values)
        {
            min = Vector2.Min(min, v);
            max = Vector2.Max(max, v);
        }
        return (min, max);
    }

    /// <summary>
    /// Do these two nodes keep clear of each other? True when they are at least a node's room apart on
    /// one axis — which, since every node is drawn inside <see cref="NodeWidth"/> by <see cref="NodeHeight"/>,
    /// is exactly what "they do not overlap" means at any zoom.
    /// </summary>
    public static bool KeepClear(Vector2 a, Vector2 b)
        => MathF.Abs(a.X - b.X) >= NodeWidth - 0.01f || MathF.Abs(a.Y - b.Y) >= NodeHeight - 0.01f;
}
