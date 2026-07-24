using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;

namespace ResonanceHunter.Core.Evolution;

/// <summary>
/// One node in an evolution tree. A creature sits at exactly one node and advances to a child when
/// that child's conditions are all met.
/// </summary>
public sealed record EvolutionNode
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>The Role a creature takes ON REACHING this node. Role is evolution-gated, never freely set.</summary>
    public required Role Role { get; init; }

    /// <summary>Power-tier floor this node confers — evolving is a real power step, not just a rename.</summary>
    public int PowerTierBonus { get; init; }

    public List<EvolutionEdge> Children { get; init; } = new();
}

/// <summary>
/// A branch to a child node, gated by an AND across up to four independent conditions.
/// </summary>
/// <remarks>
/// It is an AND-gate, not a weighted score. A creature does not "lean toward" an evolution — it
/// either satisfies every condition on a branch or it does not. This is what makes an evolution
/// feel like something you *did*, rather than a die the game rolled on your behalf.
/// </remarks>
public sealed record EvolutionEdge
{
    public required string TargetNodeId { get; init; }

    /// <summary>Lower fires first when two sibling branches are simultaneously eligible. Must be unique among siblings.</summary>
    public required int BranchPriority { get; init; }

    /// <summary>evolution_progress (fed materials) must reach this. 0 = no material requirement.</summary>
    public int MaterialThreshold { get; init; }

    /// <summary>Healthy work ticks in the PARENT's Role must reach this. Job diligence.</summary>
    public int JobTicksThreshold { get; init; }

    /// <summary>A tallied combat behaviour (e.g. "part_break", "interrupt") must reach this count.</summary>
    public string? CombatTag { get; init; }
    public int CombatThreshold { get; init; }

    /// <summary>An equipped charm must carry this trait. Null = no equipment requirement.</summary>
    public string? RequiredEquipTrait { get; init; }

    /// <summary>A short, player-facing reason this branch exists — shown so a path is never a mystery.</summary>
    public required string Hint { get; init; }
}

/// <summary>The mutable progress a specific creature has accrued toward its next evolution.</summary>
public sealed class EvolutionProgress
{
    public int Materials { get; private set; }
    public readonly Dictionary<Role, int> HealthyWorkTicks = new();
    public readonly Dictionary<string, int> CombatTally = new();
    public string? EquippedTrait { get; set; }

    /// <summary>Feed material toward evolution.</summary>
    public void FeedMaterial(int amount) => Materials += Math.Max(0, amount);

    /// <summary>Job diligence accrues ONLY while healthy — a Blocked or Starving creature earns nothing.</summary>
    public void RecordWorkTick(Role role, bool healthy)
    {
        if (!healthy) return;
        HealthyWorkTicks[role] = HealthyWorkTicks.GetValueOrDefault(role) + 1;
    }

    public void RecordCombat(string tag) => CombatTally[tag] = CombatTally.GetValueOrDefault(tag) + 1;

    /// <summary>Rebuild progress from a save. Not for use during play — progress only ever accrues forward.</summary>
    public static EvolutionProgress Restore(
        int materials,
        IReadOnlyDictionary<Role, int> workTicks,
        IReadOnlyDictionary<string, int> combatTally,
        string? equippedTrait)
    {
        var p = new EvolutionProgress { EquippedTrait = equippedTrait };
        p.Materials = materials;
        foreach (var (role, n) in workTicks) p.HealthyWorkTicks[role] = n;
        foreach (var (tag, n) in combatTally) p.CombatTally[tag] = n;
        return p;
    }
}

/// <summary>An authored evolution tree, addressed by node id.</summary>
public sealed class EvolutionTree
{
    private readonly Dictionary<string, EvolutionNode> _nodes;

    public EvolutionTree(string rootId, IEnumerable<EvolutionNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes = nodes.ToDictionary(n => n.Id);

        if (!_nodes.ContainsKey(rootId))
            throw new ArgumentException($"Root '{rootId}' is not in the tree.", nameof(rootId));

        RootId = rootId;
        Validate();
    }

    public string RootId { get; }
    public EvolutionNode Node(string id) => _nodes[id];
    public bool HasNode(string id) => _nodes.ContainsKey(id);

    private void Validate()
    {
        foreach (var node in _nodes.Values)
        {
            // Every branch must point somewhere real, or a creature can strand itself mid-evolution.
            foreach (var edge in node.Children)
                if (!_nodes.ContainsKey(edge.TargetNodeId))
                    throw new InvalidOperationException($"Node '{node.Id}' branches to missing '{edge.TargetNodeId}'.");

            // Sibling priorities must be unique, or "which branch fires first" is undefined.
            var priorities = node.Children.Select(c => c.BranchPriority).ToList();
            if (priorities.Distinct().Count() != priorities.Count)
                throw new InvalidOperationException($"Node '{node.Id}' has duplicate sibling branch priorities.");
        }
    }

    /// <summary>Does this branch's every condition currently hold?</summary>
    public static bool EdgeSatisfied(EvolutionEdge edge, EvolutionNode from, EvolutionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(progress);

        if (progress.Materials < edge.MaterialThreshold) return false;

        // Job ticks are role-agnostic honest work, and the farm records them under the CREATURE'S live role
        // (RecordWorkTick(c.Role, …)) — not this branch's origin node. Reading from.Role asked for ticks in a
        // bucket the creature only fills if it already had that role, so the DEFENDER branch (off the Attacker
        // root) was unreachable for every non-Attacker creature. Sum every bucket: 40 shifts is 40 shifts,
        // whatever role worked them.
        if (edge.JobTicksThreshold > 0
            && progress.HealthyWorkTicks.Values.Sum() < edge.JobTicksThreshold)
            return false;

        if (edge.CombatTag is not null
            && progress.CombatTally.GetValueOrDefault(edge.CombatTag) < edge.CombatThreshold)
            return false;

        if (edge.RequiredEquipTrait is not null && progress.EquippedTrait != edge.RequiredEquipTrait)
            return false;

        return true;
    }

    /// <summary>
    /// The branch a creature would take right now, or null if none is eligible.
    /// </summary>
    /// <remarks>
    /// Deterministic: among all currently-eligible siblings, the lowest <see cref="EvolutionEdge.BranchPriority"/>
    /// wins. A creature that meets two paths at once does not get a coin flip — the tie-break is
    /// authored, so the same play always yields the same evolution. That determinism is what lets a
    /// player *aim* at a branch instead of hoping for one.
    /// </remarks>
    public EvolutionEdge? EligibleBranch(string currentNodeId, EvolutionProgress progress)
    {
        var node = _nodes[currentNodeId];

        return node.Children
            .Where(e => EdgeSatisfied(e, node, progress))
            .OrderBy(e => e.BranchPriority)
            .FirstOrDefault();
    }

    /// <summary>Every branch from a node, each flagged eligible or not — for showing the player their options.</summary>
    public IReadOnlyList<(EvolutionEdge Edge, bool Eligible)> Options(string currentNodeId, EvolutionProgress progress)
    {
        var node = _nodes[currentNodeId];
        return node.Children
            .OrderBy(e => e.BranchPriority)
            .Select(e => (e, EdgeSatisfied(e, node, progress)))
            .ToList();
    }
}
