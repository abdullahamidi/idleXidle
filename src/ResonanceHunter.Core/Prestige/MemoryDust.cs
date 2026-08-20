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

/// <summary>Which of the tree's five parts a node belongs to.</summary>
/// <remarks>
/// Declared on the node rather than inferred from its id or its prerequisite chain. The screen has to
/// group by road — a tree whose whole point is "one road, not two" cannot be presented as an
/// undifferentiated grid — and inferring the grouping from a naming convention would break silently the
/// first time somebody named a node badly.
/// </remarks>
public enum TraitRoad
{
    /// <summary>Cheap and structural: sockets, weaves, vows, filters, forge. Everyone walks most of it.</summary>
    Spine,

    /// <summary>Power bought with safety. GLASS CANNON -> BLOODLUST -> BLOOD MAGIC -> REAPER.</summary>
    Ruin,

    /// <summary>The wall. IRONCLAD -> JUGGERNAUT -> UNDYING -> TITAN.</summary>
    Aegis,

    /// <summary>The economy build. GREED -> DISCERNING EYE -> FORTUNE -> HOARDER.</summary>
    Avarice,

    /// <summary>Behaviour over numbers. ECHO -> VENOMANCER -> THE BOUND HAND -> WEAVER.</summary>
    Artifice,
}

public sealed record MemoryDustUnlock
{
    /// <summary>Which road this node sits on. See <see cref="TraitRoad"/>.</summary>
    public TraitRoad Road { get; init; } = TraitRoad.Spine;

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

    /// <summary>Total points required to buy the entire tree — the visible horizon.</summary>
    public int TotalTreeCost => _unlocks.Values.Sum(u => u.Cost);

    /// <summary>
    /// TRAIT POINTS. What this tree spends — and it is NOT Memory Dust.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dust is minted by the Warren, tick by tick, while the game is closed. A permanent tree bought with
    /// it is a permanent tree bought by WAITING — the same failure the skill tree had when it was funded
    /// by the Warren's mastery pool, and a worse one here, because these choices can never be taken back.
    /// A player who idled for a week would arrive at their identity without ever having made a decision.
    /// </para>
    /// <para>
    /// Trait points come from CONQUESTS, corruption tiers and region-mastery goals — things that only
    /// happen because someone descended. Dust remains what it always was for the Warren: a material.
    /// </para>
    /// <para>
    /// Derived by the host every frame from progress, exactly like the skill tree's, so it can never
    /// double-count across reloads and there is nothing to persist but which nodes were bought.
    /// </para>
    /// </remarks>
    public int Earned { get; private set; }

    public int Spent => _owned.Select(id => _unlocks.TryGetValue(id, out var u) ? u.Cost : 0).Sum();

    public int Available => Math.Max(0, Earned - Spent);

    /// <summary>Set the total trait points earned. See <see cref="Earned"/>.</summary>
    public void SetEarned(int earned) => Earned = Math.Max(0, earned);

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
        if (Available < unlock.Cost) return false;
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

        _owned.Add(id);   // the cost is charged by Spent, which reads the owned set
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
        // ══ THE SPINE ═══════════════════════════════════════════════════════════════════════════
        //
        // Cheap and structural. It grants CAPACITY — sockets, a fifth weave, vows, filters, forge
        // automation — and never a number. Every player walks most of it, which is exactly why it must
        // be cheap: a spine that competed with the paths for points would turn "what am I" into "can I
        // afford to be anything".
        //
        // The old tree's attribute rungs (+8% DAMAGE three times over, and the same for health, tempo,
        // haul and rarity) are GONE. Those were the bare multipliers the design forbids, they made this
        // tree read as the same screen as the skill tree in a different colour, and numbers are now the
        // skill tree's job. What is left here is what only a permanent tree can sell.

        new() { Id = "socket_2", Name = "SECOND SOCKET", Cost = 2, Effect = UnlockEffect.Expansion,
                Description = "A SECOND KEYSTONE SOCKET." },
        new() { Id = "weave_5", Name = "FIFTH WEAVE", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" }, Description = "A FIFTH SKILL SLOT." },
        new() { Id = "socket_3", Name = "THIRD SOCKET", Cost = 5, Effect = UnlockEffect.Expansion,
                Requires = new[] { "weave_5" }, Description = "A THIRD KEYSTONE SOCKET." },

        // Vows — ability OPTIONS rather than bigger numbers, which is the spine's whole character.
        new() { Id = "vow_study_1", Name = "FIRST VOW", Cost = 1, Effect = UnlockEffect.Expansion,
                Description = "LEARN COMPLETION AND THE DELIBERATE." },
        new() { Id = "vow_study_2", Name = "SECOND VOW", Cost = 2, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_1" }, Description = "LEARN THE PURE AND THE FRANTIC." },
        new() { Id = "vow_study_3", Name = "THIRD VOW", Cost = 2, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_2" }, Description = "LEARN THE SINGULAR AND THE BLUNT EDGE." },
        new() { Id = "vow_binding", Name = "BINDING VOWS", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" }, Description = "LEARN THE THREE VOWS THAT COST A GEAR SLOT." },
        new() { Id = "vow_sacrifice", Name = "SACRIFICIAL VOWS", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" }, Description = "LEARN FRAGILITY, RECKLESS OFFERING, THE UNGUARDED AND THE UNBOUND." },

        // Attention, not power. An idle game's real currency is ATTENTION, and a bag of ninety Commons
        // spends it on nothing.
        new() { Id = "ledger", Name = "HUNTER'S LEDGER", Cost = 1, Effect = UnlockEffect.Convenience,
                Description = "GROUNDWORK — OPENS THE FORGE & AUTO-SELL PATH." },
        new() { Id = "filter_common", Name = "SORTER'S EYE", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" }, Description = "AUTO-SELL COMMONS AS THEY DROP." },
        new() { Id = "filter_uncommon", Name = "SORTER'S DISCIPLINE", Cost = 3, Effect = UnlockEffect.Convenience,
                Requires = new[] { "filter_common" }, Description = "AUTO-SELL UNCOMMONS TOO." },
        new() { Id = "forge_insight", Name = "FORGE INSIGHT", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" }, Description = "OPENS THE FORGE'S EFFICIENCY & AUTO-MERGE UPGRADES." },
        new() { Id = "efficient_forge", Name = "EFFICIENT FORGE", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "forge_insight" }, Description = "DISMANTLE RETURNS 15% MORE (STILL A LOSS)." },
        new() { Id = "auto_merge", Name = "TIRELESS FORGE", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "forge_insight" }, Description = "AUTO-MERGE RUNS AFTER EVERY EXPEDITION." },

        new() { Id = "recall_1", Name = "SHARPENED RECALL I", Cost = 1, Effect = UnlockEffect.Amplifier,
                Description = "REGION MASTERY ACCRUES 5% FASTER." },
        new() { Id = "recall_2", Name = "SHARPENED RECALL II", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_1" }, Description = "REGION MASTERY ACCRUES A FURTHER 5% FASTER." },
        new() { Id = "recall_3", Name = "SHARPENED RECALL III", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_2" }, Description = "REGION MASTERY ACCRUES A FURTHER 5% FASTER." },
        new() { Id = "recall_4", Name = "PERFECT RECALL", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_3" }, Description = "REGION MASTERY ACCRUES A FINAL 5% FASTER." },

        // ══ THE FOUR PATHS ══════════════════════════════════════════════════════════════════════
        //
        // Three keystones and a terminal, at 4 / 6 / 8 / 12 — thirty points a path. Two full paths cost
        // sixty against roughly thirty-four earnable, so TWO TERMINALS ARE NOT AFFORDABLE. One terminal
        // plus most of a second path is, and the terminal you did not take is the permanent shape of
        // your character. There is no respec: that asymmetry against the skill tree is the whole reason
        // the two screens are different screens.
        //
        // Each path hangs off the SPINE rather than off the tree's root, so a player has already bought
        // the sockets they will need to wear what the path teaches.

        // ── RUIN — power bought with safety. A Ruin hunter lives at low health on purpose, which makes
        //    the skill tree's ENDURE branch nearly worthless to them: the two trees interact rather
        //    than stack, and that is what stops "take everything good" being a strategy. ────────────
        new() { Id = "ks_glass_cannon", Road = TraitRoad.Ruin, Name = "THE GLASS ROAD", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" }, GrantsKeystone = "glass_cannon",
                Description = "LEARN GLASS CANNON." },
        new() { Id = "ks_bloodlust", Road = TraitRoad.Ruin, Name = "THE EDGE", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_glass_cannon" }, GrantsKeystone = "bloodlust",
                Description = "LEARN BLOODLUST." },
        new() { Id = "ks_blood_magic", Road = TraitRoad.Ruin, Name = "THE RED ROAD", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_bloodlust" }, GrantsKeystone = "blood_magic",
                Description = "LEARN BLOOD MAGIC." },
        new() { Id = "ks_reaper", Road = TraitRoad.Ruin, Name = "THE HARVEST", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_blood_magic" }, GrantsKeystone = "reaper",
                Description = "LEARN REAPER — THE RUIN TERMINAL." },

        // ── THE CHARGE SPUR, one rung per road at the same cost (the roads must stay within two
        //    points of each other — enforced by test). Ruin spends the pool, Aegis winds it, Artifice
        //    bends it, Avarice holds it: a build that wants two of these walks two roads, and that
        //    split IS the min/max texture the playtest asked for. No spur rung sits on a terminal's
        //    path, so it competes with depth rather than gating it.
        new() { Id = "ks_rend", Road = TraitRoad.Ruin, Name = "THE STORED BLOW", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_glass_cannon" }, GrantsKeystone = "rend",
                Description = "LEARN REND." },

        // ── AEGIS — the wall. The only path that makes Bruiser bands routine, and the natural partner
        //    of the skill tree's ENDURE branch. ──────────────────────────────────────────────────
        new() { Id = "ks_ironclad", Road = TraitRoad.Aegis, Name = "THE IRON ROAD", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" }, GrantsKeystone = "ironclad",
                Description = "LEARN IRONCLAD." },
        new() { Id = "ks_juggernaut", Road = TraitRoad.Aegis, Name = "THE UNMOVED", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_ironclad" }, GrantsKeystone = "juggernaut",
                Description = "LEARN JUGGERNAUT." },
        new() { Id = "ks_undying", Road = TraitRoad.Aegis, Name = "THE LAST BREATH", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_juggernaut" }, GrantsKeystone = "undying",
                Description = "LEARN UNDYING." },
        new() { Id = "ks_titan", Road = TraitRoad.Aegis, Name = "THE TITAN ROAD", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_undying" }, GrantsKeystone = "titan",
                Description = "LEARN TITAN — THE AEGIS TERMINAL." },
        new() { Id = "ks_dynamo", Road = TraitRoad.Aegis, Name = "THE WOUND SPRING", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_ironclad" }, GrantsKeystone = "dynamo",
                Description = "LEARN DYNAMO." },
        new() { Id = "ks_lodestone", Road = TraitRoad.Avarice, Name = "THE KEPT COIL", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_greed" }, GrantsKeystone = "lodestone",
                Description = "LEARN LODESTONE." },
        new() { Id = "ks_capacitor", Road = TraitRoad.Artifice, Name = "THE DEEP WELL", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_echo" }, GrantsKeystone = "capacitor",
                Description = "LEARN CAPACITOR." },

        // ── AVARICE — the economy build. It buys no combat power at all, which is what makes it a real
        //    choice: it trades depth for the gear that eventually buys depth. HOARDER is what stops it
        //    being a dead end — the haul becomes force. ───────────────────────────────────────────
        new() { Id = "ks_greed", Road = TraitRoad.Avarice, Name = "THE GOLDEN ROAD", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ledger" }, GrantsKeystone = "greed",
                Description = "LEARN GREED." },
        new() { Id = "ks_discerning_eye", Road = TraitRoad.Avarice, Name = "THE NARROW EYE", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_greed" }, GrantsKeystone = "discerning_eye",
                Description = "LEARN DISCERNING EYE." },
        new() { Id = "ks_fortune", Road = TraitRoad.Avarice, Name = "THE GILDED ROAD", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_discerning_eye" }, GrantsKeystone = "fortune",
                Description = "LEARN FORTUNE." },
        new() { Id = "ks_hoarder", Road = TraitRoad.Avarice, Name = "THE FULL VAULT", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_fortune" }, GrantsKeystone = "hoarder",
                Description = "LEARN HOARDER — THE AVARICE TERMINAL." },

        // ── ARTIFICE — behaviour over numbers. The path for players who want their build to do
        //    something strange rather than something large. Its third rung is the SACRIFICIAL VOWS
        //    rather than a keystone: the Form-combo triggers it used to hold now belong to the skill
        //    tree, and vows are the purest "behaviour, not numbers" thing the game has. ───────────
        new() { Id = "ks_echo", Road = TraitRoad.Artifice, Name = "THE TWICE-SPOKEN", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_1" }, GrantsKeystone = "echo",
                Description = "LEARN ECHO." },
        new() { Id = "ks_venomancer", Road = TraitRoad.Artifice, Name = "THE SLOW ROAD", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_echo" }, GrantsKeystone = "venomancer",
                Description = "LEARN VENOMANCER." },
        // Requires only its own path, NOT the whole vow chain. Hanging it off vow_sacrifice made ARTIFICE
        // cost 38 against the other paths' 31-32 and put its terminal out of reach on its own — measured,
        // not guessed. A path that is more expensive than the others is not "flavourful", it is dead.
        new() { Id = "artifice_vows", Road = TraitRoad.Artifice, Name = "THE BOUND HAND", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_venomancer" },
                Description = "EVERY VOW YOU KNOW MAY BE CARRIED AT ONCE." },
        new() { Id = "ks_weaver", Road = TraitRoad.Artifice, Name = "THE DOUBLE THREAD", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "artifice_vows" }, GrantsKeystone = "weaver",
                Description = "LEARN WEAVER — THE ARTIFICE TERMINAL." },

        // ── The quiet mark. Requires one node from each path's FIRST rung, so it says "you have seen
        //    all four roads", not "you bought the tree" — which is impossible and meant to be. ─────
        new() { Id = "attunement", Name = "COMPLETE ATTUNEMENT", Cost = 4, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ks_glass_cannon", "ks_ironclad", "ks_greed", "ks_echo", "socket_3" },
                Description = "YOU HAVE STOOD AT THE HEAD OF ALL FOUR ROADS." },
    };
}
