using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;

namespace ResonanceHunter.Core.Builds;

/// <summary>What a node is: the shape of the tree.</summary>
public enum MasteryKind
{
    /// <summary>The centre — always allocated, free, the root every branch grows from.</summary>
    Start,

    /// <summary>Ring 1, cost 1. Connective tissue, but each one still moves a SHAPE.</summary>
    Minor,

    /// <summary>Ring 2, cost 3. Where a branch starts to have an opinion.</summary>
    Notable,

    /// <summary>Ring 3, cost 5. A behaviour with a visible price attached.</summary>
    Greater,

    /// <summary>Ring 4, cost 8. The branch's identity, and always a trade.</summary>
    Mastery,

    /// <summary>Between two adjacent branches, cost 6. Needs ring 2 of BOTH.</summary>
    Bridge,

    /// <summary>A Form sub-branch, cost 6. Grants that Form's combo trigger.</summary>
    Specialisation,
}

/// <summary>
/// One node: what it is, which branch it sits on, how far out, what it costs, and what it changes.
/// </summary>
public sealed record MasteryNode(
    string Id,
    MasteryKind Kind,
    Branch Branch,
    int Ring,
    int Cost,
    string Label,
    SkillShape Shape,
    BuildTrigger? Grant,
    IReadOnlyList<string> Prereqs)
{
    /// <summary>For a BRIDGE: the second branch it spans.</summary>
    public Branch? Link { get; init; }

    /// <summary>For a SPECIALISATION: the Form it deepens.</summary>
    public Form? Form { get; init; }

    /// <summary>
    /// A SECOND prerequisite group. Any one of <see cref="Prereqs"/> AND any one of these.
    /// </summary>
    /// <remarks>
    /// Bridges need this and nothing else does. With a single any-of list, listing both branches' notables
    /// would mean ONE notable from either side unlocks the bridge — which turns a 6-point hybrid reward
    /// into a 3-point splash and quietly deletes the price the design put on hybridising.
    /// </remarks>
    public IReadOnlyList<string> SecondPrereqs { get; init; } = Array.Empty<string>();

    /// <summary>Is this node's prerequisite satisfied by the given allocation?</summary>
    public bool Unlocked(Func<string, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(isTaken);
        if (Prereqs.Count > 0 && !Prereqs.Any(isTaken)) return false;
        if (SecondPrereqs.Count > 0 && !SecondPrereqs.Any(isTaken)) return false;
        return true;
    }
}

/// <summary>
/// A player's progress through the <see cref="MasteryCatalog"/>: which nodes are taken, and the points
/// left to take more.
/// </summary>
/// <remarks>
/// <para>
/// Points come from FIRST-TIME DEPTH, per region, and from nothing else — see the host's SkillPointsEarned
/// and game-flow.md §3.5. They were once funded by the Warren's idle mastery pool, which meant the whole
/// tree was affordable before a player had descended once and no node in it was ever a choice.
/// </para>
/// <para>
/// <b>Nodes cost what their ring costs</b>, from 1 at the minors to 8 at a mastery. A flat one-point tree
/// cannot express "this is the branch's identity" — every node reads the same and the only decision is
/// breadth. Prices are what make ring 4 a commitment rather than a formality.
/// </para>
/// <para>
/// <b>Respec is free, instant and unlimited</b>, and that asymmetry against the permanent trait tree is
/// the entire distinction between the two. This tree is a workbench: you come back after every wall, move
/// six points and go again. If a respec ever costs anything, the two trees collapse into one screen with
/// different colours.
/// </para>
/// </remarks>
public sealed class MasteryTree
{
    private readonly HashSet<string> _taken = new() { MasteryCatalog.StartId };

    /// <summary>Total points earned over the whole game. Derived by the host from depth every frame.</summary>
    public int Earned { get; private set; }

    /// <summary>Points committed — the SUM OF COSTS, not the node count. START is free.</summary>
    public int Spent => _taken.Select(MasteryCatalog.ById).Where(n => n is not null).Sum(n => n!.Cost);

    public int Available => Math.Max(0, Earned - Spent);

    public IReadOnlyCollection<string> Taken => _taken;
    public bool IsTaken(string id) => _taken.Contains(id);

    /// <summary>
    /// Set the total points earned. The host DERIVES this from depth and calls it every frame, so it can
    /// never double-count across reloads — there is nothing to persist but which nodes were taken.
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
    /// Can this node be taken right now? Needs enough points, an un-taken node, its prerequisites, and —
    /// for a Mastery — that no other branch has already been mastered.
    /// </summary>
    public bool CanTake(string id)
    {
        if (_taken.Contains(id)) return false;
        if (MasteryCatalog.ById(id) is not { } node) return false;
        if (Available < node.Cost) return false;
        if (!node.Unlocked(_taken.Contains)) return false;
        if (node.Kind == MasteryKind.Mastery && MasteredBranch() is not null) return false;
        // ONE DISCIPLINE PER HUNTER. Affinity() reads the FIRST Specialisation taken, so a second
        // one could only ever add its trigger while its Form silently counted for nothing — the
        // player would read it as hybridising and the hexagon would ignore it. Attunement is an
        // identity, not a shopping list; the tree refuses instead of half-honouring.
        if (node.Kind == MasteryKind.Specialisation && Affinity() is not null) return false;
        return true;
    }

    public bool Take(string id)
    {
        if (!CanTake(id)) return false;
        _taken.Add(id);
        return true;
    }

    /// <summary>
    /// Give back one node, if nothing taken still depends on it.
    /// </summary>
    /// <remarks>
    /// Cheaper than a full respec for the common case — a player who wants to move the last three points
    /// should not have to rebuild thirty. Refuses to strand a node rather than silently cascading, because
    /// a click that quietly removed six other nodes would be indistinguishable from a bug.
    /// </remarks>
    public bool Refund(string id)
    {
        if (id == MasteryCatalog.StartId || !_taken.Contains(id)) return false;

        _taken.Remove(id);
        var stranded = _taken.Select(MasteryCatalog.ById)
            .Any(n => n is not null && n.Id != MasteryCatalog.StartId && !n.Unlocked(_taken.Contains));

        if (stranded) { _taken.Add(id); return false; }
        return true;
    }

    /// <summary>
    /// Give every point back. Free and instant — see the class remarks.
    /// </summary>
    public void Respec()
    {
        _taken.Clear();
        _taken.Add(MasteryCatalog.StartId);
    }

    /// <summary>The branch you mastered, or null if you haven't reached a ring 4 yet.</summary>
    public Branch? MasteredBranch()
    {
        var m = _taken.Select(MasteryCatalog.ById).FirstOrDefault(n => n is { Kind: MasteryKind.Mastery });
        return m?.Branch;
    }

    /// <summary>
    /// The Form this build has specialised in, which the sim reads as its AFFINITY.
    /// </summary>
    /// <remarks>
    /// It comes from the Form specialisations rather than from a branch, because a branch is a way of
    /// fighting and affinity is about which Form you carry. Taking two specialisations is legal and
    /// affinity then follows the first — a player who wants both is buying breadth, not two affinities.
    /// </remarks>
    public Form? Affinity()
        => _taken.Select(MasteryCatalog.ById)
                 .Where(n => n is { Kind: MasteryKind.Specialisation })
                 .Select(n => n!.Form)
                 .FirstOrDefault(f => f is not null);

    /// <summary>Everything the taken nodes add up to as a change of SHAPE.</summary>
    public SkillShape Shape()
        => SkillShape.Sum(_taken.Select(MasteryCatalog.ById).Where(n => n is not null).Select(n => n!.Shape));

    /// <summary>Every trigger the taken specialisations grant.</summary>
    public IReadOnlySet<BuildTrigger> Triggers()
        => _taken.Select(MasteryCatalog.ById).Where(n => n?.Grant is not null)
                 .Select(n => n!.Grant!.Value).ToHashSet();

    /// <summary>
    /// The numeric mods, kept for the call sites that still speak <see cref="BuildMods"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately NEUTRAL. The whole point of the rebuild is that this tree changes shapes rather than
    /// scalars; anything it did add here would be the bare multiplier the design forbids. It exists so
    /// Build.Resolve keeps its signature, not because the tree has numbers to contribute.
    /// </remarks>
    public BuildMods Mods() => BuildMods.None;
}
