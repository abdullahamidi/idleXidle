using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Abilities;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Prestige;

/// <summary>
/// What Memory Dust actually BUYS. The one place the tree turns into gameplay.
/// </summary>
/// <remarks>
/// <para>
/// The dust tree bought <b>nothing</b>. All 19 unlocks were Description strings that lit up a
/// constellation node: a grep for every unlock id returned zero hits outside the catalog itself, and
/// <see cref="MemoryDustTree.Owns"/> was called from exactly one file — the screen that DRAWS it.
/// Buying "+1 ROSTER SLOT" did not add a roster slot. Buying "DISMANTLE RETURNS 5% MORE" did not
/// change the dismantle rate; it stayed a hardcoded 0.4f. The whole prestige layer was a very pretty
/// progress bar.
/// </para>
/// <para>
/// This class exists so that can't recur. Every consumer asks THIS, and a test asserts every id in the
/// catalog is reachable from a method here — so a node that nothing reads fails the build rather than
/// shipping as a lie. If you add an unlock, you add its effect here, or the test tells you not to.
/// </para>
/// </remarks>
public static class DustEffects
{
    // ── The passive tree ──────────────────────────────────────────────────────────────────────
    //
    // What used to be here: SquadSlots and FarmSlots, reading HATCHERY I/II and EXPANDED/GRAND WARREN.
    // The squad is gone, so they are gone. test_nothing_is_wired_that_does_not_exist named all four the
    // moment the catalog changed — which is the entire reason that test exists.

    /// <summary>
    /// Everything the owned attribute nodes do to the character, combined.
    /// </summary>
    /// <remarks>
    /// Multiplicative, via <see cref="BuildMods.Sum"/> — additive stacking is how an idle game arrives
    /// at 400,000% and stops meaning anything.
    /// </remarks>
    public static BuildMods TreeMods(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return BuildMods.Sum(tree.All.Where(u => tree.Owns(u.Id)).Select(u => u.Mods));
    }

    /// <summary>
    /// The keystones the player has LEARNED and may socket — not the ones they are wearing.
    /// </summary>
    /// <remarks>
    /// The tree teaches; the build chooses. See <see cref="Builds.Build.KeystoneSlots"/> — this returns
    /// the menu, and the menu is deliberately longer than the plate.
    /// </remarks>
    public static IReadOnlyList<Keystone> LearnedKeystones(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.All
            .Where(u => u.GrantsKeystone is not null && tree.Owns(u.Id))
            .Select(u => Keystones.ById(u.GrantsKeystone!))
            .OfType<Keystone>()
            .ToList();
    }

    // ── Earn rates ────────────────────────────────────────────────────────────────────────────

    /// <summary>Region mastery rate: +5% per FASTER REGION MASTERY node, to +20%. Read by <c>RegionAutomation.RecordActiveKill</c>.</summary>
    public static float MasteryRate(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var rate = 1f;
        foreach (var id in new[] { "recall_1", "recall_2", "recall_3", "recall_4" })
            if (tree.Owns(id)) rate += 0.05f;
        return rate;
    }

    /// <summary>
    /// What a dismantle returns, as a fraction of sell value.
    /// </summary>
    /// <remarks>
    /// Deliberately still a loss at every tier. Dismantle is meant to be the answer to "I don't want
    /// this", not a better sell — if Dust ever pushed it past 1.0 it would become the strictly-correct
    /// action for every item in the game and delete selling outright.
    /// </remarks>
    public static float DismantleRate(MemoryDustTree tree, float baseRate)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var rate = tree.Owns("efficient_forge") ? baseRate * 1.15f : baseRate;
        return MathF.Min(0.95f, rate);
    }

    // ── Weaving components — the vision's "Resonance Weaving components" ──────────────────────

    /// <summary>
    /// Which Vow each study unlock teaches. Dust buys ABILITY OPTIONS, not just bigger numbers.
    /// </summary>
    /// <remarks>
    /// Gating the Vow catalog is what makes the prestige tree feed the game's titular system instead of
    /// sitting beside it. It also fixes a real onboarding problem: six Vows presented at once to a
    /// player with three creatures is a menu, not a decision.
    /// </remarks>
    private static readonly Dictionary<string, string[]> VowGrants = new()
    {
        // Repointed with the Vow rewrite: the old ids named run-state Vows (below 40% health, against a
        // boss, after ten seconds) that no longer exist. Ordered gentlest first — a player's FIRST VOW
        // should be one most builds already satisfy, and the ones that cost a gear slot come late.
        ["vow_study_1"] = new[] { "vow_complete", "vow_deliberate" },
        ["vow_study_2"] = new[] { "vow_pure", "vow_frantic" },
        ["vow_study_3"] = new[] { "vow_singular", "vow_bluntedge" },
        // BINDING VOWS teaches the three that cost a gear SLOT — its stats, its enchantment and its
        // affixes all at once, which is the harshest thing the catalogue asks and the most visible.
        ["vow_binding"] = new[] { "vow_barefoot", "vow_openhand", "vow_bareskull" },
        ["vow_sacrifice"] = new[]
        {
            "vow_fragility", "vow_reckless_offering", "vow_unguarded", "vow_unbound",
        },
    };

    /// <summary>Every Vow the player has learned. Empty until the first study is bought.</summary>
    public static IReadOnlyList<Vow> KnownVows(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var ids = VowGrants.Where(kv => tree.Owns(kv.Key)).SelectMany(kv => kv.Value).ToHashSet();
        return Weaving.Catalog.Where(v => ids.Contains(v.Id)).ToList();
    }

    public static bool KnowsVow(MemoryDustTree tree, string? vowId)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return vowId is not null && KnownVows(tree).Any(v => v.Id == vowId);
    }

    // ── Loot filters — the vision's "loot filters" ───────────────────────────────────────────

    /// <summary>
    /// Drops at or below this rarity are sold on sight. Null means keep everything.
    /// </summary>
    /// <remarks>
    /// Capped at Uncommon by design: an idle game's real currency is ATTENTION, and a bag of ninety
    /// Commons spends it on nothing — but a filter that could eat Rares would quietly auto-sell the
    /// item game, which is the thing the player is here for.
    /// </remarks>
    public static Rarity? AutoSellAtOrBelow(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (tree.Owns("filter_uncommon")) return Rarity.Uncommon;
        if (tree.Owns("filter_common")) return Rarity.Common;
        return null;
    }

    // ── Automation rules — the vision's "automation rules" ───────────────────────────────────

    /// <summary>Auto-merge fires after every expedition, unprompted.</summary>
    public static bool AutoMergeAfterRuns(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Owns("auto_merge");
    }

    // ── Convenience ──────────────────────────────────────────────────────────────────────────

    /// <summary>Show exact numbers on bars rather than just a fill.</summary>
    public static bool ShowExactNumbers(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Owns("ledger");
    }

    /// <summary>Preview a merge's outcome before committing to it.</summary>
    public static bool ShowMergePreview(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Owns("forge_insight");
    }

    /// <summary>The capstone: a quiet mark, and deliberately nothing more.</summary>
    public static bool TreeComplete(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Owns("attunement");
    }

    /// <summary>
    /// Every unlock id this class actually reads. The test's list of what is wired.
    /// </summary>
    /// <remarks>
    /// Hand-maintained on purpose: a reflection trick would pass forever without anyone reading it, and
    /// the failure this guards against is precisely "nobody noticed the wire was never connected".
    /// </remarks>
    /// <summary>Nodes wired by a hand-written <c>Owns("id")</c> check somewhere in this class.</summary>
    private static readonly HashSet<string> NamedWires = new()
    {
        "recall_1", "recall_2", "recall_3", "recall_4",
        "efficient_forge", "forge_insight", "auto_merge",
        "vow_study_1", "vow_study_2", "vow_study_3", "vow_binding", "vow_sacrifice",
        "filter_common", "filter_uncommon",
        "ledger", "attunement",
        "socket_2", "socket_3", "weave_5", "artifice_vows",
    };

    /// <summary>
    /// How many keystone sockets the character has. One to start; the spine sells the other two.
    /// </summary>
    /// <remarks>
    /// Three free sockets meant a player wore every keystone they had learned, so learning one was the
    /// only decision and wearing it was automatic. Starting at one makes the SOCKET the scarce thing and
    /// the keystone the choice — which is what the paths are for.
    /// </remarks>
    public static int KeystoneSockets(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return 1 + (tree.Owns("socket_2") ? 1 : 0) + (tree.Owns("socket_3") ? 1 : 0);
    }

    /// <summary>Skill slots — four to start, five once the spine buys the fifth weave.</summary>
    public static int SkillSlots(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return 4 + (tree.Owns("weave_5") ? 1 : 0);
    }

    /// <summary>VOWS PAY 25% MORE (artifice_vows) — every Vow sworn pays 25% more. Reaches the fight through <see cref="TreeShape"/>.</summary>
    /// <remarks>
    /// This wire used to be <c>UnboundVows</c> — "every Vow known may be carried at once" — and nothing
    /// read it: there was never a one-vow limit to lift, so the node promised a freedom the game already
    /// had and delivered nothing. The house failure mode, caught in the 2026-08-23 traits pass. A vow
    /// power multiplier is the one thing this node can honestly sell: SoloBattle already scales every
    /// vow's bonus by <see cref="SkillShape.VowPowerMultiplier"/> (the Oathbound's passive rides it).
    /// </remarks>
    public static float VowPowerMultiplier(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Owns("artifice_vows") ? 1.25f : 1f;
    }

    /// <summary>What the Dust tree adds to the build's shape — folded in at <c>PlayerLoadout.ToBuild</c>.</summary>
    public static SkillShape TreeShape(MemoryDustTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var vow = VowPowerMultiplier(tree);
        return vow == 1f ? SkillShape.None : new SkillShape { VowPowerMultiplier = vow };
    }

    public static IReadOnlySet<string> WiredIds { get; } = NamedWires
        // Keystone gates need no hand-written wire: LearnedKeystones reads them generically off the
        // node itself, so carrying an id there is the wire. (The attribute nodes that TreeMods used to
        // cover are gone — numbers belong to the skill tree now, and this tree sells only capacity,
        // keystones and behaviour.)
        .Concat(MemoryDustTree.Catalog
            .Where(u => u.Mods != BuildMods.None || u.GrantsKeystone is not null)
            .Select(u => u.Id))
        .ToHashSet();
}
