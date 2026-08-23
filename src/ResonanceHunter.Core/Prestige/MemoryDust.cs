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
    /// The finite catalog — the character's passive tree. The exact count and total are computed live
    /// (<see cref="TotalTreeCost"/>) and pinned by test, so this line never has to lie about them.
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
    /// <item><b>Attribute nodes</b> — small, honest multipliers, each hung off a RUNG OF A ROAD rather
    ///   than sold from the root. They came back in the traits overhaul of 2026-08-23 (the playtest asked
    ///   for more traits, said in plainer words) in a shape that keeps them from being the shopping list
    ///   the earlier rewrite cut: you cannot buy RAZOR EDGE without having walked to BLOOD MAGIC, so the
    ///   minors REWARD a road instead of competing with it, none sits on a terminal's path (the
    ///   two-terminals invariant is untouched), and a test caps how much the whole set can multiply.</item>
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
        // haul and rarity) are GONE FROM THE SPINE. Those were bare multipliers sold from the root, they
        // made this tree read as the same screen as the skill tree in a different colour, and numbers
        // are mostly the skill tree's job. The spine sells only what a permanent tree can sell. (The
        // roads carry small multipliers again — see THE MINOR STRANDS below — but only as the reward for
        // walking a road, never as a thing you buy instead of walking one.)
        //
        // DESCRIPTIONS ARE SENTENCES, and they say what actually happens. The playtest of 2026-08-23
        // asked for traits that explain themselves, and the reader plays in English as a second
        // language: so no abbreviations, no genre words a dictionary would not have, the real number
        // where there is one, and nothing promised that no screen reads. MemoryDustText composes the
        // final text (this description, plus the keystone's own blurb, plus a generated line for the
        // numbers) — the catalogue holds only the part a sentence generator cannot write.

        new() { Id = "socket_2", Name = "SECOND SOCKET", Cost = 2, Effect = UnlockEffect.Expansion,
                Description = "A second keystone socket. You can wear two keystones at once instead of one." },
        new() { Id = "weave_5", Name = "FIFTH WEAVE", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" },
                Description = "A fifth skill slot. Your build carries five skills instead of four." },
        new() { Id = "socket_3", Name = "THIRD SOCKET", Cost = 5, Effect = UnlockEffect.Expansion,
                Requires = new[] { "weave_5" },
                Description = "A third keystone socket. You can wear three keystones at once." },

        // Vows — ability OPTIONS rather than bigger numbers, which is the spine's whole character.
        new() { Id = "vow_study_1", Name = "FIRST VOW", Cost = 1, Effect = UnlockEffect.Expansion,
                Description = "Learn two vows: COMPLETION and THE DELIBERATE. A vow is a rule you accept for extra power. It pays only while your build keeps the rule." },
        new() { Id = "vow_study_2", Name = "SECOND VOW", Cost = 2, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_1" },
                Description = "Learn two more vows: THE PURE and THE FRANTIC." },
        new() { Id = "vow_study_3", Name = "THIRD VOW", Cost = 2, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_2" },
                Description = "Learn two more vows: THE SINGULAR and THE BLUNT EDGE." },
        new() { Id = "vow_binding", Name = "BINDING VOWS", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" },
                Description = "Learn the three vows that each leave one gear slot empty: THE BAREFOOT, THE OPEN HAND and THE BARE SKULL. The hardest rule, the biggest reward." },
        // "VOWS OF SACRIFICE", not "SACRIFICIAL VOWS": the diagram prints every name under its node in
        // two short lines, and SACRIFICIAL is the one word in the catalogue too long for a line.
        new() { Id = "vow_sacrifice", Name = "VOWS OF SACRIFICE", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" },
                Description = "Learn four vows that always cost you something: FRAGILITY, RECKLESS OFFERING, THE UNGUARDED and THE UNBOUND." },

        // Attention, not power. An idle game's real currency is ATTENTION, and a bag of ninety Commons
        // spends it on nothing.
        new() { Id = "ledger", Name = "HUNTER'S LEDGER", Cost = 1, Effect = UnlockEffect.Convenience,
                Description = "A first step. Opens the way to auto-selling, the forge upgrades, and the Avarice road." },
        new() { Id = "filter_common", Name = "SORTER'S EYE", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" },
                Description = "Common items are sold the moment they drop, so your bag holds only what matters." },
        new() { Id = "filter_uncommon", Name = "SORTER'S DISCIPLINE", Cost = 3, Effect = UnlockEffect.Convenience,
                Requires = new[] { "filter_common" },
                Description = "Uncommon items are sold on sight too. Rare and better are always kept." },
        new() { Id = "forge_insight", Name = "FORGE INSIGHT", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" },
                Description = "Opens two forge upgrades: salvaging gives more material, and merging runs by itself." },
        new() { Id = "efficient_forge", Name = "EFFICIENT FORGE", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "forge_insight" },
                Description = "Salvaging an item returns 15% more material than before. Still less than selling it." },
        new() { Id = "auto_merge", Name = "TIRELESS FORGE", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "forge_insight" },
                Description = "The forge merges your spare items by itself after every expedition." },

        new() { Id = "recall_1", Name = "SHARPENED RECALL I", Cost = 1, Effect = UnlockEffect.Amplifier,
                Description = "Region mastery grows 5% faster." },
        new() { Id = "recall_2", Name = "SHARPENED RECALL II", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_1" },
                Description = "Region mastery grows another 5% faster — 10% in all." },
        new() { Id = "recall_3", Name = "SHARPENED RECALL III", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_2" },
                Description = "Region mastery grows another 5% faster — 15% in all." },
        new() { Id = "recall_4", Name = "PERFECT RECALL", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_3" },
                Description = "Region mastery grows a final 5% faster — 20% in all." },

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
        //
        // A keystone gate's Description says WHERE ON THE ROAD it stands; what the keystone DOES is the
        // keystone's own Blurb, which MemoryDustText reads off the catalogue — one sentence, one owner.

        // ── RUIN — power bought with safety. A Ruin hunter lives at low health on purpose, which makes
        //    the skill tree's ENDURE branch nearly worthless to them: the two trees interact rather
        //    than stack, and that is what stops "take everything good" being a strategy. ────────────
        new() { Id = "ks_glass_cannon", Road = TraitRoad.Ruin, Name = "THE GLASS ROAD", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" }, GrantsKeystone = "glass_cannon",
                Description = "Where the Ruin road begins." },
        new() { Id = "ks_bloodlust", Road = TraitRoad.Ruin, Name = "THE EDGE", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_glass_cannon" }, GrantsKeystone = "bloodlust",
                Description = "The second step down the Ruin road." },
        new() { Id = "ks_blood_magic", Road = TraitRoad.Ruin, Name = "THE RED ROAD", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_bloodlust" }, GrantsKeystone = "blood_magic",
                Description = "The third step down the Ruin road. No way back from here." },
        new() { Id = "ks_reaper", Road = TraitRoad.Ruin, Name = "THE HARVEST", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_blood_magic" }, GrantsKeystone = "reaper",
                Description = "The end of the Ruin road. Twelve points. Take it, and you can never finish another road." },

        // ── THE CHARGE SPUR, one rung per road at the same cost (the roads must stay within two
        //    points of each other — enforced by test). Ruin spends the pool, Aegis winds it, Artifice
        //    bends it, Avarice holds it: a build that wants two of these walks two roads, and that
        //    split IS the min/max texture the playtest asked for. No spur rung sits on a terminal's
        //    path, so it competes with depth rather than gating it.
        new() { Id = "ks_rend", Road = TraitRoad.Ruin, Name = "THE STORED BLOW", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_glass_cannon" }, GrantsKeystone = "rend",
                Description = "A side step off the Ruin road. The keystone that spends stored CHARGE." },

        // ── AEGIS — the wall. The only path that makes Bruiser bands routine, and the natural partner
        //    of the skill tree's ENDURE branch. ──────────────────────────────────────────────────
        new() { Id = "ks_ironclad", Road = TraitRoad.Aegis, Name = "THE IRON ROAD", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" }, GrantsKeystone = "ironclad",
                Description = "Where the Aegis road begins." },
        new() { Id = "ks_juggernaut", Road = TraitRoad.Aegis, Name = "THE UNMOVED", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_ironclad" }, GrantsKeystone = "juggernaut",
                Description = "The second step up the wall." },
        new() { Id = "ks_undying", Road = TraitRoad.Aegis, Name = "THE LAST BREATH", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_juggernaut" }, GrantsKeystone = "undying",
                Description = "The third step up the wall." },
        new() { Id = "ks_titan", Road = TraitRoad.Aegis, Name = "THE TITAN ROAD", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_undying" }, GrantsKeystone = "titan",
                Description = "The end of the Aegis road. Twelve points. Take it, and you can never finish another road." },
        new() { Id = "ks_dynamo", Road = TraitRoad.Aegis, Name = "THE WOUND SPRING", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_ironclad" }, GrantsKeystone = "dynamo",
                Description = "A side step off the Aegis road. The keystone that stores CHARGE when you are hit." },
        new() { Id = "ks_lodestone", Road = TraitRoad.Avarice, Name = "THE KEPT COIL", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_greed" }, GrantsKeystone = "lodestone",
                Description = "A side step off the Avarice road. The keystone that pays you for holding CHARGE." },
        new() { Id = "ks_capacitor", Road = TraitRoad.Artifice, Name = "THE DEEP WELL", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_echo" }, GrantsKeystone = "capacitor",
                Description = "A side step off the Artifice road. The keystone that makes the CHARGE pool deeper." },

        // ── AVARICE — the economy build. It buys no combat power at all, which is what makes it a real
        //    choice: it trades depth for the gear that eventually buys depth. HOARDER is what stops it
        //    being a dead end — the haul becomes force. ───────────────────────────────────────────
        new() { Id = "ks_greed", Road = TraitRoad.Avarice, Name = "THE GOLDEN ROAD", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ledger" }, GrantsKeystone = "greed",
                Description = "Where the Avarice road begins." },
        new() { Id = "ks_discerning_eye", Road = TraitRoad.Avarice, Name = "THE NARROW EYE", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_greed" }, GrantsKeystone = "discerning_eye",
                Description = "The second step along the Avarice road." },
        new() { Id = "ks_fortune", Road = TraitRoad.Avarice, Name = "THE GILDED ROAD", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_discerning_eye" }, GrantsKeystone = "fortune",
                Description = "The third step along the Avarice road." },
        new() { Id = "ks_hoarder", Road = TraitRoad.Avarice, Name = "THE FULL VAULT", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_fortune" }, GrantsKeystone = "hoarder",
                Description = "The end of the Avarice road. Twelve points. Take it, and you can never finish another road." },

        // ── ARTIFICE — behaviour over numbers. The path for players who want their build to do
        //    something strange rather than something large. Its third rung is THE BOUND HAND (every vow
        //    pays 25% more — DustEffects.VowPowerMultiplier) rather than a keystone: the Form-combo triggers it used to hold now belong to the skill
        //    tree, and vows are the purest "behaviour, not numbers" thing the game has. ───────────
        new() { Id = "ks_echo", Road = TraitRoad.Artifice, Name = "THE TWICE-SPOKEN", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_1" }, GrantsKeystone = "echo",
                Description = "Where the Artifice road begins." },
        new() { Id = "ks_venomancer", Road = TraitRoad.Artifice, Name = "THE SLOW ROAD", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_echo" }, GrantsKeystone = "venomancer",
                Description = "The second step along the Artifice road." },
        // Requires only its own path, NOT the whole vow chain. Hanging it off vow_sacrifice made ARTIFICE
        // cost 38 against the other paths' 31-32 and put its terminal out of reach on its own — measured,
        // not guessed. A path that is more expensive than the others is not "flavourful", it is dead.
        new() { Id = "artifice_vows", Road = TraitRoad.Artifice, Name = "THE BOUND HAND", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_venomancer" },
                Description = "The third step along the Artifice road. Every vow you swear pays 25% more." },
        new() { Id = "ks_weaver", Road = TraitRoad.Artifice, Name = "THE DOUBLE THREAD", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "artifice_vows" }, GrantsKeystone = "weaver",
                Description = "The end of the Artifice road. Twelve points. Take it, and you can never finish another road." },

        // ══ THE MINOR STRANDS — three small nodes beside every road ════════════════════════════
        //
        // Added 2026-08-23, when the playtest asked for MORE traits in PLAINER words. Each road carries
        // a side strand: the CHARGE spur beside its first rung (above), and beside the other three rungs
        // an attribute node — two minors and, beside the TERMINAL, a notable that is the reward for
        // walking a road to its end. Every one hangs off a rung of its OWN road, which is what keeps them
        // from being the shopping list an earlier rewrite cut:
        //   - none sits on a terminal's PATH, so what one terminal costs, and that two are out of reach,
        //     is exactly what it was (test_two_terminals_are_out_of_reach);
        //   - every road gains the same nine points, so the road-cost law holds without a thumb on it;
        //   - the numbers are small (+5 / +6 / +12 percent) and a test caps what the whole set can
        //     multiply, because multiplicative stacking is how an idle game arrives at 400,000%.
        // Each strand moves the number that IS its road: Ruin hits harder, Aegis has more health, Avarice
        // brings back more, Artifice casts sooner. The NUMBER lives in the Mods and nowhere else —
        // MemoryDustText writes the sentence from it, so the catalogue cannot disagree with the sim.
        // Ids deliberately do not start with "ks_": the path-walking test uses that prefix to find a
        // road's keystone chain, and these are beside the chain, not on it.

        // Ruin: the blade.
        new() { Id = "ruin_edge_1", Road = TraitRoad.Ruin, Name = "SHARP EDGE", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_bloodlust" }, Mods = new BuildMods(1.05f, 1f, 1f, 1f, 1f),
                Description = "The first sharpening of the blade." },
        new() { Id = "ruin_edge_2", Road = TraitRoad.Ruin, Name = "RAZOR EDGE", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_blood_magic" }, Mods = new BuildMods(1.06f, 1f, 1f, 1f, 1f),
                Description = "Sharper still." },
        new() { Id = "ruin_edge_3", Road = TraitRoad.Ruin, Name = "THE RED HARVEST", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_reaper" }, Mods = new BuildMods(1.12f, 1f, 1f, 1f, 1f),
                Description = "The reward for walking Ruin to its end." },

        // Aegis: the hide.
        new() { Id = "aegis_skin_1", Road = TraitRoad.Aegis, Name = "THICK SKIN", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_juggernaut" }, Mods = new BuildMods(1f, 1.05f, 1f, 1f, 1f),
                Description = "A thick hide that softens a blow." },
        new() { Id = "aegis_skin_2", Road = TraitRoad.Aegis, Name = "IRON SKIN", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_undying" }, Mods = new BuildMods(1f, 1.06f, 1f, 1f, 1f),
                Description = "Hammered flat and hard." },
        new() { Id = "aegis_skin_3", Road = TraitRoad.Aegis, Name = "TITAN'S BULK", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_titan" }, Mods = new BuildMods(1f, 1.12f, 1f, 1f, 1f),
                Description = "The reward for walking Aegis to its end." },

        // Avarice: the purse. Haul is how much comes back; Rarity is how good it is. The notable moves
        // both a little, because a road about loot should end on loot of both kinds.
        new() { Id = "avarice_purse_1", Road = TraitRoad.Avarice, Name = "A DEEPER PURSE", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_discerning_eye" }, Mods = new BuildMods(1f, 1f, 1f, 1.05f, 1f),
                Description = "Room for what the road drops." },
        new() { Id = "avarice_purse_2", Road = TraitRoad.Avarice, Name = "A KEENER EYE", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_fortune" }, Mods = new BuildMods(1f, 1f, 1f, 1f, 1.06f),
                Description = "You spot what others step over." },
        new() { Id = "avarice_purse_3", Road = TraitRoad.Avarice, Name = "HOARDER'S SHARE", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_hoarder" }, Mods = new BuildMods(1f, 1f, 1f, 1.08f, 1.05f),
                Description = "The reward for walking Avarice to its end." },

        // Artifice: the hands.
        new() { Id = "artifice_hands_1", Road = TraitRoad.Artifice, Name = "QUICK HANDS", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_venomancer" }, Mods = new BuildMods(1f, 1f, 1.05f, 1f, 1f),
                Description = "The weave comes easier." },
        new() { Id = "artifice_hands_2", Road = TraitRoad.Artifice, Name = "DEFT HANDS", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "artifice_vows" }, Mods = new BuildMods(1f, 1f, 1.06f, 1f, 1f),
                Description = "Your hands know the pattern." },
        new() { Id = "artifice_hands_3", Road = TraitRoad.Artifice, Name = "WEAVER'S PACE", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_weaver" }, Mods = new BuildMods(1f, 1f, 1.12f, 1f, 1f),
                Description = "The reward for walking Artifice to its end." },

        // ── The quiet mark. Requires one node from each path's FIRST rung, so it says "you have seen
        //    all four roads", not "you bought the tree" — which is impossible and meant to be. ─────
        new() { Id = "attunement", Name = "COMPLETE ATTUNEMENT", Cost = 4, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ks_glass_cannon", "ks_ironclad", "ks_greed", "ks_echo", "socket_3" },
                Description = "You have stood at the head of all four roads. A quiet mark, and nothing more." },
    };
}
