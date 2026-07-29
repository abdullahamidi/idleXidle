using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Builds;

namespace ResonanceHunter.Core.Prestige;

/// <summary>What an unlock affects. None of these ever DESTROY or RESET anything.</summary>
public enum UnlockEffect
{
    /// <summary>A small, permanent quality-of-life or convenience improvement.</summary>
    Convenience,

    /// <summary>Raises a soft ceiling — more roster slots, more team room. Additive, never a reset.</summary>
    Expansion,

    /// <summary>A modest, non-locked multiplier on an earn rate. Never touches a LOCKED tuning value.</summary>
    Amplifier,
}

public sealed record MemoryDustUnlock
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int Cost { get; init; }
    public required UnlockEffect Effect { get; init; }

    /// <summary>Prerequisite unlock ids. All must be owned first. Empty = a root, always available.</summary>
    public IReadOnlyList<string> Requires { get; init; } = Array.Empty<string>();

    // ── THIS TREE IS NOW THE CHARACTER'S PASSIVE TREE. ────────────────────────────────────────
    //
    // The roster is gone and the player has one character, so "prestige" and "the passive tree" are
    // the same object: a costed, prerequisite-gated graph that Dust buys. Rather than build a second
    // engine beside this one — cycle validation, affordability, ownership, all of it again — the
    // catalog changed and the engine stayed. Two engines would have drifted, and this codebase has
    // been bitten by parallel systems more than once (two ability systems; two palettes).
    //
    // Dust is one of the three things the player loots (gold, items, dust). This is what it buys.

    /// <summary>
    /// What this node does to the build's numbers. <see cref="BuildMods.None"/> for a pure gate.
    /// </summary>
    public BuildMods Mods { get; init; } = BuildMods.None;

    /// <summary>
    /// The keystone this node grants, if any — see <see cref="Keystones"/>.
    /// </summary>
    /// <remarks>
    /// Keystones are the reason the tree is a tree rather than a shopping list. An attribute node makes
    /// you 4% stronger and every player takes it; a keystone makes you a different character and most
    /// players must refuse it. They sit at the END of themed branches, so reaching one costs you the
    /// path to another.
    /// </remarks>
    public string? GrantsKeystone { get; init; }
}

/// <summary>
/// Memory Dust — the prestige layer where NOTHING RESETS.
/// </summary>
/// <remarks>
/// Most prestige systems ask you to say goodbye: a shining new multiplier in one hand, and in the
/// other, they take back the regions you mastered and the creatures you shaped. This one takes
/// nothing. Memory Dust accrues as a side effect of mastering regions — you never "prestige" in the
/// reset-the-world sense — and it buys from a FINITE, completable tree. A player can see the horizon:
/// a finite, completable set of nodes (the exact total is <see cref="TotalTreeCost"/>, computed live), then done — not trapped in an infinite
/// multiplier grind.
///
/// The invariants below are enforced by test, because they are the entire reason this layer respects
/// the game's other pillars instead of undermining them.
/// </remarks>
public sealed class MemoryDustTree
{
    private readonly Dictionary<string, MemoryDustUnlock> _unlocks;
    private readonly HashSet<string> _owned = new();

    public MemoryDustTree(IEnumerable<MemoryDustUnlock>? unlocks = null)
    {
        _unlocks = (unlocks ?? Catalog).ToDictionary(u => u.Id);
        Validate();
    }

    public int MemoryDust { get; private set; }
    public IReadOnlyCollection<MemoryDustUnlock> All => _unlocks.Values;
    public bool Owns(string id) => _owned.Contains(id);

    /// <summary>Total Dust required to buy the entire tree — the visible horizon.</summary>
    public int TotalTreeCost => _unlocks.Values.Sum(u => u.Cost);

    /// <summary>
    /// Award Dust earned from a region mastery milestone.
    /// </summary>
    /// <remarks>
    /// This is the ONLY faucet: Dust comes from mastering regions, never from a reset, never from
    /// grinding. A player who never opens this screen still earns it, harmlessly, in the background.
    /// </remarks>
    public void AwardFromMastery(int amount) => MemoryDust += Math.Max(0, amount);

    /// <summary>
    /// Spend Dust on a cost outside the unlock tree (a Warren facility upgrade). Spends nothing and returns
    /// false if unaffordable, so a caller can gate on it exactly like <see cref="Purchase"/>.
    /// </summary>
    public bool Spend(int amount)
    {
        if (amount < 0 || MemoryDust < amount) return false;
        MemoryDust -= amount;
        return true;
    }

    public bool CanUnlock(string id)
    {
        if (!_unlocks.TryGetValue(id, out var unlock)) return false;
        if (_owned.Contains(id)) return false;
        if (MemoryDust < unlock.Cost) return false;
        return unlock.Requires.All(_owned.Contains);
    }

    /// <summary>
    /// Buy an unlock. Permanent and one-directional — there is no respec and no refund.
    /// </summary>
    /// <remarks>
    /// Mirrors the Forge's and item schema's irreversibility: a choice you made is a choice you keep.
    /// That is what makes the tree a set of decisions rather than a slider you fiddle with.
    /// </remarks>
    public bool Purchase(string id)
    {
        if (!CanUnlock(id)) return false;

        MemoryDust -= _unlocks[id].Cost;
        _owned.Add(id);
        return true;
    }

    /// <summary>Restore owned unlocks and Dust from a save. Order-independent.</summary>
    public void Restore(int dust, IEnumerable<string> owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        MemoryDust = Math.Max(0, dust);
        _owned.Clear();
        foreach (var id in owned) if (_unlocks.ContainsKey(id)) _owned.Add(id);
    }

    public IReadOnlyList<string> OwnedIds => _owned.ToList();

    /// <summary>Is the whole tree bought? A player deserves to KNOW when they are finished.</summary>
    public bool IsComplete => _owned.Count == _unlocks.Count;

    private void Validate()
    {
        foreach (var unlock in _unlocks.Values)
        {
            foreach (var req in unlock.Requires)
                if (!_unlocks.ContainsKey(req))
                    throw new InvalidOperationException($"Unlock '{unlock.Id}' requires missing '{req}'.");

            if (unlock.Requires.Contains(unlock.Id))
                throw new InvalidOperationException($"Unlock '{unlock.Id}' requires itself.");
        }

        // No dependency cycle — every unlock must be reachable from an empty set.
        var reachable = new HashSet<string>();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var u in _unlocks.Values)
                if (!reachable.Contains(u.Id) && u.Requires.All(reachable.Contains))
                    changed = reachable.Add(u.Id);
        }

        if (reachable.Count != _unlocks.Count)
            throw new InvalidOperationException("The Memory Dust tree has an unreachable unlock (dependency cycle).");
    }


    /// <summary>
    /// The finite catalog: 36 nodes totalling 1,430 Memory Dust — the character's passive tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every entry is Convenience, Expansion, or Amplifier — none resets, de-masters, or reverts
    /// anything (A1), and none touches a value another system marks LOCKED (A4).
    /// </para>
    /// <para>
    /// <b>This catalog has now been rewritten twice for the same reason: most of it described a game
    /// that no longer existed.</b> The first pass cut "RESONANCE FILLS 5% FASTER FROM DEFENCE" and its
    /// neighbours — Resonance was manual combat's meter, and a player could spend 165 Dust across four
    /// nodes and change nothing, because there was nothing on the other end of the wire. This pass cuts
    /// HATCHERY I/II, EXPANDED WARREN and GRAND WARREN for exactly that reason: they bought squad and
    /// farm-team slots, and there is no squad. 150 Dust of wires to nowhere.
    /// </para>
    /// <para>
    /// <b>What replaced them is the point of the pivot.</b> The player has one character, so this tree
    /// IS that character's passive tree — there is no second engine and no second currency. Dust is one
    /// of the three things an expedition loots, and this is what it buys:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Attribute nodes</b> — small, honest multipliers. Nobody agonises over them, and they are
    ///   not meant to be agonised over: they are the PATH, and the path is what makes reaching a
    ///   keystone cost something other than Dust.</item>
    /// <item><b>Keystone gates</b> — these LEARN a keystone; they do not wear it. See
    ///   <see cref="Builds.Build.KeystoneSlots"/> for why that distinction is load-bearing rather than
    ///   fussy. Each sits at the end of a themed branch, so the branch you walked is a decision even
    ///   before the three sockets force another one.</item>
    /// <item><b>Vows, filters, forge and recall</b> — the systems that survived, untouched.</item>
    /// </list>
    /// <para>
    /// A node that cannot be wired does not belong in this tree; tests assert every id here is reachable
    /// from <see cref="DustEffects"/>, and that every keystone in <see cref="Builds.Keystones"/> is
    /// teachable by some node — the wire, checked from both ends. This codebase has shipped complete,
    /// tested, entirely uncalled systems six times. Not a seventh.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MemoryDustUnlock> Catalog { get; } = new List<MemoryDustUnlock>
    {
        // ── Roots ─────────────────────────────────────────────────────────────────────────────
        new() { Id = "recall_1", Name = "SHARPENED RECALL I", Cost = 10, Effect = UnlockEffect.Amplifier,
                Description = "REGION MASTERY ACCRUES 5% FASTER." },
        // Honest description: the game already prints exact numbers on the bars that carry them (HP, DPS,
        // stats), so this node's real job is being the ENTRY to the forge & auto-sell branch below it. Its
        // old "SHOW EXACT NUMBERS" copy promised an effect nothing wired (audit #3) — reworded, not gated,
        // because gating the readouts would be a new-player downgrade, not a fix.
        new() { Id = "ledger", Name = "HUNTER'S LEDGER", Cost = 20, Effect = UnlockEffect.Convenience,
                Description = "GROUNDWORK — OPENS THE FORGE & AUTO-SELL PATH." },

        // ── MIGHT. The plain road: hit harder, and at the end, hit MUCH harder and hope. ───────
        new() { Id = "might_1", Name = "STRONG ARM", Cost = 15, Effect = UnlockEffect.Amplifier,
                Mods = new BuildMods(1.08f, 1f, 1f, 1f, 1f), Description = "+8% DAMAGE." },
        new() { Id = "might_2", Name = "PRACTISED VIOLENCE", Cost = 30, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "might_1" }, Mods = new BuildMods(1.08f, 1f, 1f, 1f, 1f),
                Description = "+8% DAMAGE." },
        new() { Id = "ks_glass_cannon", Name = "THE GLASS ROAD", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "might_2" }, GrantsKeystone = "glass_cannon",
                Description = "LEARN GLASS CANNON." },

        // ── GRIT. Survive. Both keystones here buy life with something the fight wanted. ───────
        new() { Id = "grit_1", Name = "THICK HIDE", Cost = 15, Effect = UnlockEffect.Amplifier,
                Mods = new BuildMods(1f, 1.10f, 1f, 1f, 1f), Description = "+10% HEALTH." },
        new() { Id = "grit_2", Name = "SCAR TISSUE", Cost = 30, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "grit_1" }, Mods = new BuildMods(1f, 1.10f, 1f, 1f, 1f),
                Description = "+10% HEALTH." },
        new() { Id = "ks_ironclad", Name = "THE IRON ROAD", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "grit_2" }, GrantsKeystone = "ironclad",
                Description = "LEARN IRONCLAD." },
        new() { Id = "ks_undying", Name = "THE LAST BREATH", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "grit_2" }, GrantsKeystone = "undying",
                Description = "LEARN UNDYING." },
        // Off the health branch (grit_2), beside IRONCLAD and UNDYING — the fortress keystones. A gate must
        // hang off ATTRIBUTE nodes, never another gate, or the branch becomes a chain of free keystones.
        new() { Id = "ks_juggernaut", Name = "THE UNMOVED", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "grit_2" }, GrantsKeystone = "juggernaut",
                Description = "LEARN JUGGERNAUT." },

        // ── TEMPO. Cast more often. BLOOD MAGIC is here because speed's real price is safety. ──
        new() { Id = "tempo_1", Name = "QUICK HANDS", Cost = 15, Effect = UnlockEffect.Amplifier,
                Mods = new BuildMods(1f, 1f, 1.08f, 1f, 1f), Description = "SKILLS RETURN 8% FASTER." },
        new() { Id = "tempo_2", Name = "SECOND NATURE", Cost = 30, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "tempo_1" }, Mods = new BuildMods(1f, 1f, 1.08f, 1f, 1f),
                Description = "SKILLS RETURN 8% FASTER." },
        new() { Id = "ks_blood_magic", Name = "THE RED ROAD", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "tempo_2" }, GrantsKeystone = "blood_magic",
                Description = "LEARN BLOOD MAGIC." },
        new() { Id = "ks_echo", Name = "THE TWICE-SPOKEN", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "tempo_2" }, GrantsKeystone = "echo",
                Description = "LEARN ECHO." },

        // ── AVARICE. The branch that is not about the fight at all. ────────────────────────────
        new() { Id = "avarice_1", Name = "DEEP POCKETS", Cost = 15, Effect = UnlockEffect.Amplifier,
                Mods = new BuildMods(1f, 1f, 1f, 1.10f, 1f), Description = "+10% HAUL." },
        new() { Id = "avarice_2", Name = "PACK RAT", Cost = 30, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "avarice_1" }, Mods = new BuildMods(1f, 1f, 1f, 1.10f, 1f),
                Description = "+10% HAUL." },
        new() { Id = "ks_greed", Name = "THE GOLDEN ROAD", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "avarice_2" }, GrantsKeystone = "greed",
                Description = "LEARN GREED." },
        new() { Id = "ks_discerning_eye", Name = "THE NARROW EYE", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "avarice_2" }, GrantsKeystone = "discerning_eye",
                Description = "LEARN DISCERNING EYE." },

        // ── BLOOD. Gated behind MIGHT: every keystone here is a way of being punished for winning
        //    slowly. You must already have walked the offensive road to be offered them. ────────
        new() { Id = "blood_1", Name = "TASTE FOR IT", Cost = 20, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "might_2" }, Mods = new BuildMods(1.08f, 1f, 1f, 1f, 1f),
                Description = "+8% DAMAGE." },
        new() { Id = "ks_bloodlust", Name = "THE EDGE", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "blood_1" }, GrantsKeystone = "bloodlust",
                Description = "LEARN BLOODLUST." },
        new() { Id = "ks_reaper", Name = "THE HARVEST", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "blood_1" }, GrantsKeystone = "reaper",
                Description = "LEARN REAPER." },
        new() { Id = "ks_venomancer", Name = "THE SLOW ROAD", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "blood_1" }, GrantsKeystone = "venomancer",
                Description = "LEARN VENOMANCER." },

        // ── Weaving components. The vision's "Resonance Weaving components", and the reason the Vow
        //    catalog is gated: Dust buys ABILITY OPTIONS, not just bigger numbers. ───────────────
        new() { Id = "vow_study_1", Name = "FIRST VOW", Cost = 15, Effect = UnlockEffect.Expansion,
                Description = "LEARN THE VOW OF PATIENCE." },
        new() { Id = "vow_study_2", Name = "SECOND VOW", Cost = 30, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_1" }, Description = "LEARN THE VOW OF THE UNBROKEN." },
        new() { Id = "vow_study_3", Name = "THIRD VOW", Cost = 45, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_2" }, Description = "LEARN THE VOW OF THE BLOODIED." },
        new() { Id = "vow_binding", Name = "BINDING VOWS", Cost = 55, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" }, Description = "LEARN THE VOW OF THE BOUND." },
        new() { Id = "vow_sacrifice", Name = "SACRIFICIAL VOWS", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" }, Description = "LEARN FRAGILITY AND RECKLESS OFFERING." },

        // ── Recall ────────────────────────────────────────────────────────────────────────────
        new() { Id = "recall_2", Name = "SHARPENED RECALL II", Cost = 25, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_1" }, Description = "REGION MASTERY ACCRUES A FURTHER 5% FASTER." },
        new() { Id = "recall_3", Name = "SHARPENED RECALL III", Cost = 40, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_2" }, Description = "REGION MASTERY ACCRUES A FURTHER 5% FASTER." },
        new() { Id = "recall_4", Name = "PERFECT RECALL", Cost = 55, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_3" }, Description = "REGION MASTERY ACCRUES A FINAL 5% FASTER." },

        // ── Forge ─────────────────────────────────────────────────────────────────────────────
        // The merge result is already previewed for every player when three items are trayed, so this node's
        // real job is opening the forge-efficiency line below it. Reworded from the phantom "PREVIEW A MERGE"
        // (audit #2) — not gated, for the same new-player reason as HUNTER'S LEDGER.
        new() { Id = "forge_insight", Name = "FORGE INSIGHT", Cost = 25, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" }, Description = "OPENS THE FORGE'S EFFICIENCY & AUTO-MERGE UPGRADES." },
        new() { Id = "efficient_forge", Name = "EFFICIENT FORGE", Cost = 40, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "forge_insight" }, Description = "DISMANTLE RETURNS 15% MORE (STILL A LOSS)." },
        new() { Id = "auto_merge", Name = "TIRELESS FORGE", Cost = 35, Effect = UnlockEffect.Convenience,
                Requires = new[] { "forge_insight" }, Description = "AUTO-MERGE RUNS AFTER EVERY EXPEDITION." },

        // ── Loot filters. The vision's "loot filters" — an idle game's real currency is ATTENTION,
        //    and a bag of ninety Commons spends it on nothing. ──────────────────────────────────
        new() { Id = "filter_common", Name = "SORTER'S EYE", Cost = 25, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" }, Description = "AUTO-SELL COMMONS AS THEY DROP." },
        new() { Id = "filter_uncommon", Name = "SORTER'S DISCIPLINE", Cost = 45, Effect = UnlockEffect.Convenience,
                Requires = new[] { "filter_common" }, Description = "AUTO-SELL UNCOMMONS TOO." },

        // ── Deepened core roads (release variety) — a third rung on each attribute line. ─────────
        new() { Id = "might_3", Name = "RUINOUS FORCE", Cost = 45, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "might_2" }, Mods = new BuildMods(1.08f, 1f, 1f, 1f, 1f), Description = "+8% DAMAGE." },
        new() { Id = "grit_3", Name = "IRONHIDE", Cost = 45, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "grit_2" }, Mods = new BuildMods(1f, 1.10f, 1f, 1f, 1f), Description = "+10% HEALTH." },
        new() { Id = "tempo_3", Name = "FLOW STATE", Cost = 45, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "tempo_2" }, Mods = new BuildMods(1f, 1f, 1.08f, 1f, 1f), Description = "SKILLS RETURN 8% FASTER." },
        new() { Id = "avarice_3", Name = "HOARDER", Cost = 45, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "avarice_2" }, Mods = new BuildMods(1f, 1f, 1f, 1.10f, 1f), Description = "+10% HAUL." },

        // ── FORTUNE — the loot-rarity road. BuildMods.Rarity tilts the drop roll (see SoloExpedition). ──
        new() { Id = "fortune_1", Name = "LUCKY FIND", Cost = 15, Effect = UnlockEffect.Amplifier,
                Mods = new BuildMods(1f, 1f, 1f, 1f, 1.08f), Description = "+8% LOOT RARITY." },
        new() { Id = "fortune_2", Name = "TREASURE SENSE", Cost = 30, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "fortune_1" }, Mods = new BuildMods(1f, 1f, 1f, 1f, 1.08f), Description = "+8% LOOT RARITY." },
        new() { Id = "ks_fortune", Name = "THE GILDED ROAD", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "fortune_2" }, GrantsKeystone = "fortune", Description = "LEARN FORTUNE." },

        // ── TITAN — a fortress keystone hung off the deepened GRIT road. ─────────────────────────
        new() { Id = "ks_titan", Name = "THE TITAN ROAD", Cost = 60, Effect = UnlockEffect.Expansion,
                Requires = new[] { "grit_3" }, GrantsKeystone = "titan", Description = "LEARN TITAN." },

        // ── Capstone ──────────────────────────────────────────────────────────────────────────
        new() { Id = "attunement", Name = "COMPLETE ATTUNEMENT", Cost = 45, Effect = UnlockEffect.Convenience,
                Requires = new[] { "recall_4", "vow_sacrifice", "filter_uncommon", "blood_1" },
                Description = "THE TREE IS COMPLETE. A QUIET MARK THAT YOU HAVE SEEN ALL OF IT." },
    };
}
