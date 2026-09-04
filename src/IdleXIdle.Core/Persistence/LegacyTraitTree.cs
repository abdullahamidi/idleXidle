using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleXIdle.Core.Persistence;

/// <summary>What a save's trait-tree purchases are worth in the systems that replaced them.</summary>
/// <remarks>
/// Everything here is a FLOOR or a UNION, never an assignment, which is what makes reading it on every
/// load harmless: a second pass grants nothing a first pass did not.
/// </remarks>
public sealed record LegacyTraitGrants(
    int KeystoneSockets,
    IReadOnlyList<string> Keystones,
    IReadOnlyList<string> Vows,
    int ScavengerRunsLevel,
    int HoardVaultsLevel,
    bool OwnedAnyNode)
{
    /// <summary>A save that bought nothing on the tree — the answer for every file written from here on.</summary>
    public static LegacyTraitGrants None { get; } =
        new(0, Array.Empty<string>(), Array.Empty<string>(), 0, 0, false);
}

/// <summary>
/// The trait tree's structural purchases, translated once into the systems that own them now.
/// </summary>
/// <remarks>
/// <para>
/// <b>FROZEN, like <c>LegacyUnlocks</c> and <c>LegacySkillForm</c>.</b> These tables are a record of what
/// the trait tree used to sell on the day it stopped selling it. They must never change again; that is
/// the entire point of them. Holding their own copies of the two id maps — rather than calling into the
/// tree — is what lets the tree itself be deleted outright instead of living on forever as migration
/// scaffolding.
/// </para>
/// <para>
/// Read from <c>save.MemoryDustUnlocks</c>, on every load, before anything can be written back. An id
/// that is not in these tables is ignored, which is both the forward-compatibility guard and the
/// behaviour the tree's own restore already had.
/// </para>
/// <para>
/// <b>What is deliberately NOT here.</b> The fifth skill slot (<c>weave_5</c>) grants nothing: the fifth
/// slot is removed, and the load path unweaves the fifth row and says so out loud. The three rate nodes
/// (<c>efficient_forge</c>, <c>recall_1..4</c>, <c>artifice_vows</c>) and the twelve attribute minors
/// grant nothing either: a rate has no clean equivalent in a system that sells capability, and inventing
/// one would be exactly the meaningless one-to-one baggage the brief warns against. The two pure gates
/// (<c>ledger</c>, <c>forge_insight</c>) never did anything at all — nothing in the game ever read them.
/// </para>
/// </remarks>
public static class LegacyTraitTree
{
    /// <summary>The 19 keystone nodes, and the keystone each one taught. Frozen 2026-09-03.</summary>
    private static readonly Dictionary<string, string> KeystoneNodes = new(StringComparer.Ordinal)
    {
        ["ks_glass_cannon"] = "glass_cannon",
        ["ks_bloodlust"] = "bloodlust",
        ["ks_blood_magic"] = "blood_magic",
        ["ks_reaper"] = "reaper",
        ["ks_rend"] = "rend",
        ["ks_ironclad"] = "ironclad",
        ["ks_juggernaut"] = "juggernaut",
        ["ks_undying"] = "undying",
        ["ks_titan"] = "titan",
        ["ks_dynamo"] = "dynamo",
        ["ks_greed"] = "greed",
        ["ks_discerning_eye"] = "discerning_eye",
        ["ks_fortune"] = "fortune",
        ["ks_hoarder"] = "hoarder",
        ["ks_lodestone"] = "lodestone",
        ["ks_echo"] = "echo",
        ["ks_venomancer"] = "venomancer",
        ["ks_capacitor"] = "capacitor",
        ["ks_weaver"] = "weaver",
    };

    /// <summary>The five study nodes, and the Vows each one taught. Frozen 2026-09-03.</summary>
    private static readonly Dictionary<string, string[]> VowNodes = new(StringComparer.Ordinal)
    {
        ["vow_study_1"] = new[] { "vow_complete", "vow_deliberate" },
        ["vow_study_2"] = new[] { "vow_pure", "vow_frantic" },
        ["vow_study_3"] = new[] { "vow_singular", "vow_bluntedge" },
        ["vow_binding"] = new[] { "vow_barefoot", "vow_openhand", "vow_bareskull" },
        ["vow_sacrifice"] = new[]
        {
            "vow_fragility", "vow_reckless_offering", "vow_unguarded", "vow_unbound",
        },
    };

    /// <summary>
    /// Read a save's trait-tree purchases. An empty or unknown list grants nothing.
    /// </summary>
    /// <remarks>
    /// Safe to call on every load forever. Every value it returns is applied as a maximum or a union, so
    /// running it twice is the same as running it once — which is what stops this from being a one-shot
    /// migration with a failure mode nobody can reproduce.
    /// </remarks>
    public static LegacyTraitGrants Read(IEnumerable<string>? ownedNodeIds)
    {
        if (ownedNodeIds is null) return LegacyTraitGrants.None;

        var owned = ownedNodeIds.Where(id => id is not null).ToHashSet(StringComparer.Ordinal);
        if (owned.Count == 0) return LegacyTraitGrants.None;

        // SOCKET CAPACITY. Two nodes, and the third one implied the second.
        var sockets = 0;
        if (owned.Contains("socket_2")) sockets = 2;
        if (owned.Contains("socket_3")) sockets = 3;

        var keystones = KeystoneNodes.Where(kv => owned.Contains(kv.Key)).Select(kv => kv.Value).ToList();
        var vows = VowNodes.Where(kv => owned.Contains(kv.Key)).SelectMany(kv => kv.Value).Distinct().ToList();

        // AUTO-SELL AND AUTO-MERGE become Warren facility levels. SCAVENGER RUNS carries auto-sell and
        // HOARD VAULTS carries auto-merge — see WarrenAutomation for why round that way and not the
        // other. The floors here are the levels at which each automation switches on.
        var scavenger = 0;
        if (owned.Contains("filter_common")) scavenger = 2;
        if (owned.Contains("filter_uncommon")) scavenger = 4;
        var vaults = owned.Contains("auto_merge") ? 2 : 0;

        return new LegacyTraitGrants(sockets, keystones, vows, scavenger, vaults, OwnedAnyNode: true);
    }
}
