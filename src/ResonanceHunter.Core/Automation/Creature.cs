using System;
using ResonanceHunter.Core.Evolution;

namespace ResonanceHunter.Core.Automation;

/// <summary>The six elemental Sources. Fixed for a creature's lifetime — only Role changes.</summary>
public enum Source { Body, Mind, Nature, Machine, Shadow, Spirit }

/// <summary>
/// The five Roles. A creature's Role is EVOLUTION-GATED, never freely reassignable.
/// </summary>
/// <remarks>
/// Free reassignment would let a creature's silhouette contradict its work-loop: the art bible ties
/// Role to mass distribution, so a Defender that "becomes" a Producer would be visibly lying about
/// what it does. Role changes by evolving, or not at all.
/// </remarks>
public enum Role { Attacker, Defender, Support, Crafter, Producer }

/// <summary>A bound creature — one you own and can put to work.</summary>
public sealed class Creature
{
    public required string Id { get; init; }
    public required Source Source { get; init; }

    /// <summary>
    /// The creature's Role. Set at construction and CHANGED ONLY BY EVOLUTION — never freely reassigned.
    /// </summary>
    /// <remarks>
    /// The <c>init</c> accessor lets a caller set the Role when creating the creature, but forbids
    /// reassignment afterwards; only <see cref="TryEvolve"/> may change it, via its private setter path.
    /// Locking Role to evolution is what gives "assigned job" real teeth: a creature's silhouette and
    /// its work-loop can never contradict each other, because the only way to change what it does is to
    /// change what it *is*.
    /// </remarks>
    public Role Role { get; private set; }

    public int PowerTier { get; private set; }

    // These `init`-only mirrors let construction set Role/PowerTier while the properties themselves stay
    // private-set, so nothing can reassign them at runtime — only TryEvolve mutates them. Internal so
    // the factory and the save-restore path (both in Core) can seed them; nothing outside can.
    internal Role RoleSeed { init => Role = value; }
    internal int PowerTierSeed { init => PowerTier = value; }

    /// <summary>The creature's current node in its evolution tree.</summary>
    public string EvolutionNodeId { get; private set; } = "";

    /// <summary>Accrued progress toward the next evolution. Never resets — a creature only moves forward.</summary>
    public EvolutionProgress? Evolution { get; private set; }

    /// <summary>An unhealthy creature contributes nothing. Automation is not free upkeep-wise.</summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// The Vow this creature has sworn, or null. The player's half of Resonance Weaving.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Source is fixed at hatch and Role is evolution-gated — so without this, a creature is entirely
    /// something that HAPPENS to you, and "optimize my team" has nothing to optimise. The Vow is the one
    /// axis the player authors directly: a restriction accepted for power, chosen per creature.
    /// </para>
    /// <para>
    /// Freely reassignable, unlike Role. A Vow is a promise, not an anatomy — and the whole point is
    /// re-weaving the squad between runs, which is the loop the auto-battler is built around.
    /// </para>
    /// </remarks>
    public string? VowId { get; set; }

    /// <summary>Attach an evolution tree so this creature can advance. Sets it to the tree's root.</summary>
    public void BeginEvolution(EvolutionTree tree, EvolutionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(progress);

        EvolutionNodeId = tree.RootId;
        Evolution = progress;

        // NOTE: this deliberately does NOT touch Role.
        //
        // It used to read `Role = tree.Node(tree.RootId).Role`, which was a silent no-op for as long as
        // only ATTACKERS were ever given a tree — every tree's root happened to be an Attacker. The
        // moment every creature got one (which is what "branching evolutions" requires), that line
        // turned every Support, Crafter and Producer in the roster into an Attacker the instant it
        // hatched, and the whole roster would have collapsed to one Role.
        //
        // Attaching a tree is not an event in a creature's life; it is bookkeeping. Role is set at
        // hatch and changed ONLY by TryEvolve — which is the rule this class already documents.
    }

    /// <summary>Restore evolution to a saved node with saved progress. Save/load only.</summary>
    public void RestoreEvolution(string nodeId, EvolutionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (nodeId.Length == 0) return;

        EvolutionNodeId = nodeId;
        Evolution = progress;
    }

    /// <summary>
    /// Advance to the eligible branch, if one exists. Returns the node evolved into, or null.
    /// </summary>
    /// <remarks>
    /// Evolving adopts the target node's Role and adds its power-tier bonus. This is the ONLY path that
    /// mutates <see cref="Role"/> after hatch.
    /// </remarks>
    public string? TryEvolve(EvolutionTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (Evolution is null || EvolutionNodeId.Length == 0) return null;

        var branch = tree.EligibleBranch(EvolutionNodeId, Evolution);
        if (branch is null) return null;

        var target = tree.Node(branch.TargetNodeId);

        EvolutionNodeId = target.Id;
        Role = target.Role;
        PowerTier = Math.Clamp(PowerTier + target.PowerTierBonus, 1, 20);

        return target.Id;
    }

    /// <summary>The Role's authored work cadence — how often it produces a work tick.</summary>
    public float WorkTickIntervalSeconds => Role switch
    {
        Role.Crafter => 8f,
        Role.Attacker => 15f,
        Role.Support => 15f,
        Role.Defender => 25f,
        Role.Producer => 40f,
        _ => 20f,
    };

    /// <summary>
    /// Mint a creature from a consumed <c>creature_core</c> — "Core Hatching".
    /// </summary>
    /// <remarks>
    /// This exists because rare-creature-capture is Vertical-Slice-deferred, which left MVP with
    /// <b>no way to acquire a workforce at all</b>. Every kill drops a core, so hatching is the one
    /// creature-acquisition path that idle play can never be locked out of (Pillar 3). A hatched
    /// creature starts at its evolution tree's root.
    /// </remarks>
    public static Creature Hatch(string id, Source source, Role role, int powerTier)
        => new() { Id = id, Source = source, RoleSeed = role, PowerTierSeed = Math.Clamp(powerTier, 1, 20) };

    /// <summary>
    /// Hatch a creature of a RANDOM Source and Role — the workforce-variety path.
    /// </summary>
    /// <remarks>
    /// Every hatch used to be an Attacker, which made the farm's four-role composition puzzle
    /// (generator / crafter / defender / support) unwinnable — you could never obtain a Crafter or a
    /// Support at all, so the farm's entire point was unreachable. Rolling a varied Role at hatch is what
    /// makes the puzzle solvable and makes each core worth cracking: you hatch to fill the roles you're
    /// missing. Deterministic given <paramref name="rng"/>, so it stays testable.
    /// </remarks>
    public static Creature HatchRandom(string id, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var source = (Source)rng.Next(Enum.GetValues<Source>().Length);
        var role = (Role)rng.Next(Enum.GetValues<Role>().Length);
        return Hatch(id, source, role, 1 + rng.Next(3));
    }
}
