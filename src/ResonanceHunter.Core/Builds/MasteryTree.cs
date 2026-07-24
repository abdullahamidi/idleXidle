using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;

namespace ResonanceHunter.Core.Builds;

/// <summary>What a node is: the shape of the tree.</summary>
public enum MasteryKind
{
    /// <summary>The centre — always allocated, the root every path grows from.</summary>
    Start,

    /// <summary>A small stat node along a path. The steps you walk to reach a mastery.</summary>
    Minor,

    /// <summary>A named node with a real effect — a trigger or a big stat. The reason to walk THIS path.</summary>
    Notable,

    /// <summary>The corner: it sets your AFFINITY. Only one may be taken — you master ONE Form.</summary>
    Mastery,
}

/// <summary>
/// One node in the mastery tree: what it is, which Form-arm it sits on, how far out, what it grants,
/// and which nodes it hangs off.
/// </summary>
public sealed record MasteryNode(
    string Id,
    MasteryKind Kind,
    Form Arm,
    int Ring,
    string Label,
    BuildMods Mods,
    BuildTrigger? Grant,
    IReadOnlyList<string> Prereqs)
{
    /// <summary>
    /// For a BRIDGE node: the second arm it spans. Null for a normal node. A bridge sits BETWEEN two
    /// neighbouring arms and is reachable from either, so a build can splash a bit of both instead of
    /// pouring everything down one arm — the cross-links that turn a set of spokes into a real web.
    /// </summary>
    public Form? Link { get; init; }
}

/// <summary>
/// The catalogue: the fixed shape of the tree, the same for every player.
/// </summary>
/// <remarks>
/// Six arms, one per Form, radiating from a shared START. Each arm is a PATH — two Minor stat nodes,
/// then a Notable with a real effect, then the Mastery at the corner that claims your affinity. You spend
/// points walking outward, and because a node needs a neighbour already taken, reaching a corner MEANS
/// having invested the arm. The arms are themed so the choice is about identity, not just numbers: the
/// Strike arm is raw damage, Projectile is fire-rate, Aura is attrition, and so on. Only ONE Mastery can
/// be taken, so the tree asks the Nen question — which one Form are you?
/// </remarks>
public static class MasteryCatalog
{
    public const string StartId = "start";

    private static BuildMods Dmg(float pct) => BuildMods.None with { Damage = 1f + pct };
    private static BuildMods Hp(float pct) => BuildMods.None with { Health = 1f + pct };
    private static BuildMods Rate(float pct) => BuildMods.None with { SkillRate = 1f + pct };
    private static BuildMods Spoils(float pct) => BuildMods.None with { Haul = 1f + pct };

    public static IReadOnlyList<MasteryNode> Nodes { get; } = Build();

    private static IReadOnlyList<MasteryNode> Build()
    {
        var nodes = new List<MasteryNode>
        {
            new(StartId, MasteryKind.Start, Form.Strike, 0, "YOU", BuildMods.None, null, Array.Empty<string>()),
        };

        // Each arm is a specialisation ("class") reached by TWO routes that both converge on the notable and
        // the Mastery corner — an AGGRESSIVE path and a SUSTAIN path. This is what makes the tree branch to
        // the same end by different means: you can climb Strike through raw damage OR through toughness and
        // still arrive at STRIKE MASTERY. Which route you walk (and how far you splash the neighbours via the
        // bridges) is the build flexibility WITHIN the class.
        Arm(nodes, Form.Strike, "STRIKE",
            aggro: (Dmg(0.09f), "+9% DAMAGE", Dmg(0.10f), "+10% DAMAGE"),
            sustain: (Hp(0.10f), "+10% HEALTH", Dmg(0.06f), "+6% DAMAGE"),
            BuildTrigger.Bloodlust, "BERSERK — HIT HARDER THE LOWER YOUR HEALTH",
            Dmg(0.15f));
        Arm(nodes, Form.Projectile, "VOLLEY",
            aggro: (Rate(0.10f), "+10% SKILL RATE", Rate(0.10f), "+10% SKILL RATE"),
            sustain: (Dmg(0.08f), "+8% DAMAGE", Hp(0.08f), "+8% HEALTH"),
            BuildTrigger.Overdraw, "REPEATER — VOLLEY FIRES ONE MORE TIME",
            Rate(0.12f));
        Arm(nodes, Form.Aura, "AURA",
            aggro: (Dmg(0.08f), "+8% DAMAGE", Rate(0.08f), "+8% SKILL RATE"),
            sustain: (Hp(0.12f), "+12% HEALTH", Hp(0.10f), "+10% HEALTH"),
            BuildTrigger.Radiance, "CONFLAGRATION — AURA TICKS FASTER",
            Hp(0.15f));
        Arm(nodes, Form.Trap, "TRAP",
            aggro: (Dmg(0.10f), "+10% DAMAGE", Dmg(0.10f), "+10% DAMAGE"),
            sustain: (Hp(0.10f), "+10% HEALTH", Spoils(0.12f), "+12% SPOILS"),
            BuildTrigger.Desperation, "SPRINGLOAD — RICHER HAUL NEAR DEATH",
            Dmg(0.18f));
        Arm(nodes, Form.Mark, "MARK",
            aggro: (Dmg(0.09f), "+9% DAMAGE", Dmg(0.09f), "+9% DAMAGE"),
            sustain: (Spoils(0.15f), "+15% SPOILS", Hp(0.08f), "+8% HEALTH"),
            BuildTrigger.Linger, "BRAND — MARK LASTS FAR LONGER",
            Dmg(0.12f));
        Arm(nodes, Form.Transformation, "MORPH",
            aggro: (Rate(0.10f), "+10% SKILL RATE", Dmg(0.08f), "+8% DAMAGE"),
            sustain: (Hp(0.12f), "+12% HEALTH", Hp(0.12f), "+12% HEALTH"),
            BuildTrigger.Echo, "MIRROR — EVERY SKILL FIRES TWICE",
            Hp(0.18f));

        // ── BRIDGES: a node between each pair of neighbouring arms, reachable from EITHER. They are the
        //    cross-links — take a bit of two arms and grab the bridge between them, instead of committing
        //    everything to one spoke. Generic stat, so any pairing is worth it. ────────────────────────
        Bridge(nodes, Form.Strike, Form.Projectile, Dmg(0.06f), "+6% DAMAGE");
        Bridge(nodes, Form.Projectile, Form.Aura, Rate(0.06f), "+6% SKILL RATE");
        Bridge(nodes, Form.Aura, Form.Trap, Hp(0.08f), "+8% HEALTH");
        Bridge(nodes, Form.Trap, Form.Mark, Dmg(0.06f), "+6% DAMAGE");
        Bridge(nodes, Form.Mark, Form.Transformation, Spoils(0.10f), "+10% SPOILS");
        Bridge(nodes, Form.Transformation, Form.Strike, Hp(0.08f), "+8% HEALTH");

        return nodes;
    }

    /// <summary>The id stem for an arm — matches the short Form names the arms were built with.</summary>
    private static string ArmName(Form f) => f switch
    {
        Form.Projectile => "volley", Form.Transformation => "morph", _ => f.ToString().ToLowerInvariant(),
    };

    private static void Bridge(List<MasteryNode> nodes, Form a, Form c, BuildMods mods, string label)
    {
        var id = $"bridge_{ArmName(a)}_{ArmName(c)}";
        // Reachable once you've taken the first minor of EITHER arm — a genuine shortcut between them.
        nodes.Add(new(id, MasteryKind.Minor, a, 1, label, mods, null,
            new[] { $"{ArmName(a)}_1", $"{ArmName(c)}_1" }) { Link = c });
    }

    private static void Arm(
        List<MasteryNode> nodes, Form arm, string name,
        (BuildMods M1, string L1, BuildMods M2, string L2) aggro,
        (BuildMods M1, string L1, BuildMods M2, string L2) sustain,
        BuildTrigger notable, string notableLabel, BuildMods masteryMods)
    {
        var a = name.ToLowerInvariant();

        // The AGGRESSIVE route keeps the original ids (_1, _2), so bridges and saved allocations still line
        // up. The SUSTAIN route (_b1, _b2) is the second way up. Both feed the notable, which needs EITHER —
        // the "prereq is satisfied if ANY listed neighbour is taken" rule is exactly a MERGE of two paths.
        nodes.Add(new($"{a}_1", MasteryKind.Minor, arm, 1, aggro.L1, aggro.M1, null, new[] { StartId }));
        nodes.Add(new($"{a}_2", MasteryKind.Minor, arm, 2, aggro.L2, aggro.M2, null, new[] { $"{a}_1" }));
        nodes.Add(new($"{a}_b1", MasteryKind.Minor, arm, 1, sustain.L1, sustain.M1, null, new[] { StartId }));
        nodes.Add(new($"{a}_b2", MasteryKind.Minor, arm, 2, sustain.L2, sustain.M2, null, new[] { $"{a}_b1" }));
        nodes.Add(new($"{a}_n", MasteryKind.Notable, arm, 3, notableLabel, BuildMods.None, notable,
            new[] { $"{a}_2", $"{a}_b2" }));   // reachable from EITHER route — the two paths converge here
        nodes.Add(new($"{a}_m", MasteryKind.Mastery, arm, 4, $"{name} MASTERY", masteryMods, null, new[] { $"{a}_n" }));
    }

    public static MasteryNode? ById(string id) => Nodes.FirstOrDefault(n => n.Id == id);
}

/// <summary>
/// A player's progress through the <see cref="MasteryCatalog"/>: which nodes are taken, and the points
/// left to take more.
/// </summary>
/// <remarks>
/// Points are EARNED by playing (conquering regions, reaching new depths) and SPENT one per node. A node
/// can only be taken next to one you already have, so power is a path walked outward, not a checklist —
/// which is the whole reason a tree beats a list. Feeds a <see cref="Build"/> its extra mods, its affinity
/// (the one Mastery you took), and the triggers its notables grant.
/// </remarks>
public sealed class MasteryTree
{
    private readonly HashSet<string> _taken = new() { MasteryCatalog.StartId };

    /// <summary>Total points earned over the whole game. Spent = nodes taken beyond the free START.</summary>
    public int Earned { get; private set; }
    public int Spent => _taken.Count - 1;               // START is free
    public int Available => Math.Max(0, Earned - Spent);

    public IReadOnlyCollection<string> Taken => _taken;
    public bool IsTaken(string id) => _taken.Contains(id);

    /// <summary>
    /// Set the total points earned. The host DERIVES this from progress (depth + conquests) and calls it
    /// every frame, so it can never double-count across reloads — there is nothing to persist but which
    /// nodes were taken.
    /// </summary>
    public void SetEarned(int earned) => Earned = Math.Max(0, earned);

    public void RestoreTaken(IEnumerable<string> ids)
    {
        _taken.Clear();
        _taken.Add(MasteryCatalog.StartId);
        if (ids is null) return;
        foreach (var id in ids)
            if (MasteryCatalog.ById(id) is not null) _taken.Add(id);
    }

    /// <summary>
    /// Can this node be taken right now? Needs a point, an un-taken node, a taken neighbour, and — for a
    /// Mastery — that you haven't already mastered another Form.
    /// </summary>
    public bool CanTake(string id)
    {
        if (_taken.Contains(id)) return false;
        if (Available <= 0) return false;
        if (MasteryCatalog.ById(id) is not { } node) return false;
        if (!node.Prereqs.Any(_taken.Contains)) return false;
        if (node.Kind == MasteryKind.Mastery && MasteryForm() is not null) return false;   // one affinity only
        return true;
    }

    public bool Take(string id)
    {
        if (!CanTake(id)) return false;
        _taken.Add(id);
        return true;
    }

    /// <summary>
    /// Give every point back — un-take everything but START. Free and instant, because an idle build is
    /// meant to be REWORKED: the whole draw of the tree is trying a Trap build, then a Volley build, on
    /// the same character. A respec that cost anything would just discourage the experimenting it exists
    /// to enable.
    /// </summary>
    public void Respec()
    {
        _taken.Clear();
        _taken.Add(MasteryCatalog.StartId);
    }

    /// <summary>The Form you mastered — your affinity — or null if you haven't reached a corner yet.</summary>
    public Form? MasteryForm()
    {
        var m = _taken.Select(MasteryCatalog.ById).FirstOrDefault(n => n is { Kind: MasteryKind.Mastery });
        return m?.Arm;
    }

    /// <summary>Everything the taken nodes add up to, numerically.</summary>
    public BuildMods Mods() => BuildMods.Sum(_taken.Select(MasteryCatalog.ById).Where(n => n is not null).Select(n => n!.Mods));

    /// <summary>Every trigger the taken notables grant.</summary>
    public IReadOnlySet<BuildTrigger> Triggers()
        => _taken.Select(MasteryCatalog.ById).Where(n => n?.Grant is not null).Select(n => n!.Grant!.Value).ToHashSet();
}
