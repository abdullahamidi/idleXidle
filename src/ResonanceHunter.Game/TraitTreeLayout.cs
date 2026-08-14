using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ResonanceHunter.Client;

/// <summary>
/// Where every trait node sits in the diagram, in world units.
/// </summary>
/// <remarks>
/// <para>
/// AUTHORED, not computed. The mastery tree derives its positions from branch and ring because it is a
/// regular shape — four identical arms of a cross — and a formula states that regularity better than a
/// table would. The trait tree is not regular: it is one spine of five unequal chains with four roads
/// hanging off three different anchors, and a generic graph layout would spend its effort making that
/// irregularity tidy instead of making it legible. Thirty-five authored positions are cheaper to read,
/// cheaper to tune, and cannot drift into a shape nobody chose.
/// </para>
/// <para>
/// The shape says the design out loud, which is the entire reason the grid had to go:
/// </para>
/// <list type="bullet">
/// <item>The SPINE runs left to right along y=0 — five chains, each hanging DOWNWARD. It is cheap,
///   structural, and everyone walks most of it, so it is the ground the diagram stands on.</item>
/// <item>The four ROADS climb UPWARD from the spine node that gates each one, so a road visibly
///   depends on the spine rather than floating beside it.</item>
/// <item>Every TERMINAL is at the top of its road, all four at the same height. Four things at the
///   same altitude, thirty points each, against a career that earns about thirty-four: the picture
///   itself is the argument that you get one.</item>
/// </list>
/// </remarks>
public static class TraitTreeLayout
{
    /// <summary>One rung — the diagram's unit of distance. Public so a wire can ask how long it is.</summary>
    public const float ChainStep = 1.45f;   // one rung down a spine chain
    private const float RoadStep = 1.45f;    // one rung up a road

    public static IReadOnlyDictionary<string, Vector2> Positions { get; } = Build();

    private static Dictionary<string, Vector2> Build()
    {
        var p = new Dictionary<string, Vector2>();

        // ── THE SPINE, along y = 0, chain by chain ────────────────────────────────────────────────
        // Capacity. The one chain that gates two roads, so it sits at the far left where both can
        // climb away from it without crossing anything else.
        p["socket_2"] = new Vector2(2.0f, 0f);
        p["weave_5"] = new Vector2(2.0f, ChainStep);
        p["socket_3"] = new Vector2(2.0f, ChainStep * 2);

        // Vows. Gates ARTIFICE, and forks at the end — two ways to spend the third study.
        p["vow_study_1"] = new Vector2(6.6f, 0f);
        p["vow_study_2"] = new Vector2(6.6f, ChainStep);
        p["vow_study_3"] = new Vector2(6.6f, ChainStep * 2);
        p["vow_binding"] = new Vector2(5.9f, ChainStep * 3);
        p["vow_sacrifice"] = new Vector2(7.3f, ChainStep * 3);

        // The ledger. Gates AVARICE, and forks immediately into filters and forge.
        p["ledger"] = new Vector2(9.8f, 0f);
        p["filter_common"] = new Vector2(9.0f, ChainStep);
        p["filter_uncommon"] = new Vector2(9.0f, ChainStep * 2);
        p["forge_insight"] = new Vector2(10.6f, ChainStep);
        p["efficient_forge"] = new Vector2(10.1f, ChainStep * 2);
        p["auto_merge"] = new Vector2(11.2f, ChainStep * 2);

        // Recall — a straight rung ladder, gating nothing. It looks like what it is.
        p["recall_1"] = new Vector2(13.2f, 0f);
        p["recall_2"] = new Vector2(13.2f, ChainStep);
        p["recall_3"] = new Vector2(13.2f, ChainStep * 2);
        p["recall_4"] = new Vector2(13.2f, ChainStep * 3);

        // The quiet mark: it wants one node from the head of every road, so it has long wires wherever
        // it stands. Parked at the bottom right, out of the roads' way — and the drawing shows only its
        // nearest prerequisite, which is what keeps a five-way node from becoming a spider.
        p["attunement"] = new Vector2(12.2f, ChainStep * 3);

        // ── THE FOUR ROADS, climbing ──────────────────────────────────────────────────────────────
        // RUIN and AEGIS share socket_2, so they fan apart as they climb — the fan IS the fact that
        // one gate opens two roads you cannot both walk.
        Road(p, new[] { "ks_glass_cannon", "ks_bloodlust", "ks_blood_magic", "ks_reaper" },
             from: p["socket_2"], dx: -0.55f);
        Road(p, new[] { "ks_ironclad", "ks_juggernaut", "ks_undying", "ks_titan" },
             from: p["socket_2"], dx: +0.55f);
        // The drifts are chosen so the four TERMINALS land evenly across the top. They carry the road
        // labels, and the first pass put AEGIS and ARTIFICE 1.2 units apart — the two names printed
        // through each other and read as "AEGARTIFICE".
        Road(p, new[] { "ks_echo", "ks_venomancer", "artifice_vows", "ks_weaver" },
             from: p["vow_study_1"], dx: +0.55f);
        Road(p, new[] { "ks_greed", "ks_discerning_eye", "ks_fortune", "ks_hoarder" },
             from: p["ledger"], dx: +0.75f);

        return p;
    }

    /// <summary>Four rungs climbing from a spine anchor, drifting sideways by <paramref name="dx"/> each.</summary>
    private static void Road(Dictionary<string, Vector2> p, string[] ids, Vector2 from, float dx)
    {
        for (var i = 0; i < ids.Length; i++)
            p[ids[i]] = new Vector2(from.X + dx * (i + 1), from.Y - RoadStep * (i + 1));
    }

    /// <summary>The diagram's extent, so a view can fit it without hard-coding one.</summary>
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
}
