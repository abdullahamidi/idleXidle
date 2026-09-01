using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Core.Prestige;

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
/// first time somebody named a node badly. Each road's printed name and one-line identity live in
/// <see cref="TraitRoads"/>, so the screen and the tests read the same sentence.
/// </remarks>
public enum TraitRoad
{
    /// <summary>Cheap and structural: sockets, slots, vows, auto-selling, forge. Everyone walks most of it.</summary>
    Spine,

    /// <summary>Hit harder, live closer to death. GLASS CANNON -> BLOODLUST -> BLOOD MAGIC -> REAPER.</summary>
    Ruin,

    /// <summary>Live longer, strike less often. IRONCLAD -> JUGGERNAUT -> UNDYING -> TITAN.</summary>
    Aegis,

    /// <summary>More loot and rarer loot, softer hits. GREED -> DISCERNING EYE -> FORTUNE -> HOARDER.</summary>
    Avarice,

    /// <summary>Skills act differently, vows pay more. ECHO -> VENOMANCER -> VOWS PAY 25% MORE -> WEAVER.</summary>
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
/// nothing. Memory Dust accrues from the Warren and from play — you never "prestige" in the
/// reset-the-world sense. The TREE is bought with TRAIT POINTS earned by conquest and mastery;
/// Dust itself fuels checkpoints and facility upgrades. A player can see the horizon:
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
    /// Credit Memory Dust — the wallet's one faucet method.
    /// </summary>
    /// <remarks>
    /// The callers are the real faucet list (the old remark claimed mastery milestones were the ONLY
    /// one, which was false on four counts): the Warren's Dust facilities, region-mastery milestones,
    /// conquests, and first-time corruption deepenings — the last three priced in
    /// <c>CorruptionScaling</c>, so a test can pin them.
    /// </remarks>
    public void AddDust(int amount) => MemoryDust += Math.Max(0, amount);

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
    ///   the earlier rewrite cut: you cannot buy HARDER HITS II without having walked to BLOOD MAGIC, so the
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
        // DESCRIPTIONS ARE AT MOST TWO SHORT SENTENCES, and they say what actually happens. The playtest
        // of 2026-08-23 asked for traits that explain themselves; the owner's note of 2026-08-26 asked
        // for them in PLAIN WORDS a twelve-year-old could read. So: the first sentence is what the node
        // does, with the real number in it; the second, only if there is one, is the one thing to know
        // ("Selling still pays more than salvaging."). No flavour, no metaphor — no "road", no "bargain",
        // no "edge" — and nothing a player cannot act on. A test pins the shape: two sentences, 140
        // characters, a banned-word list, the font gate. MemoryDustText adds the keystone's own blurb
        // after a gate's description; an attribute node's description must OPEN with the sentence
        // MemoryDustText.ModsSentence generates from its Mods, so the number here can never disagree
        // with the sim (the test holds them equal).
        //
        // NAMES SAY WHAT THE NODE DOES, EFFECT FIRST. The playtest of 2026-08-25 found players could
        // not connect the traits to the fight: "SHARP EDGE", "THE GLASS ROAD", "SORTER'S EYE" were
        // metaphors a player had to decode by reading the panel. Every name is now the effect in plain
        // words ("HARDER HITS I", "AUTO-SELL COMMON DROPS"), and a keystone gate is named after the
        // keystone it teaches ("KEYSTONE — GLASS CANNON") because teaching that keystone IS its whole
        // effect. A test pins the old metaphors as forbidden. IDS NEVER CHANGE — saves store them.

        new() { Id = "socket_2", Name = "KEYSTONE SOCKET II", Cost = 2, Effect = UnlockEffect.Expansion,
                Description = "You can wear two keystones at once instead of one." },
        new() { Id = "weave_5", Name = "FIFTH SKILL SLOT", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" },
                Description = "Your build carries five skills instead of four." },
        new() { Id = "socket_3", Name = "KEYSTONE SOCKET III", Cost = 5, Effect = UnlockEffect.Expansion,
                Requires = new[] { "weave_5" },
                Description = "You can wear three keystones at once instead of two." },

        // Vows — ability OPTIONS rather than bigger numbers, which is the spine's whole character.
        new() { Id = "vow_study_1", Name = "LEARN VOWS I", Cost = 1, Effect = UnlockEffect.Expansion,
                Description = "You can take two vows: COMPLETION and THE DELIBERATE. A vow is a rule you keep for extra power." },
        new() { Id = "vow_study_2", Name = "LEARN VOWS II", Cost = 2, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_1" },
                Description = "You can take two more vows: THE PURE and THE FRANTIC." },
        new() { Id = "vow_study_3", Name = "LEARN VOWS III", Cost = 2, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_2" },
                Description = "You can take two more vows: THE SINGULAR and THE BLUNT EDGE." },
        new() { Id = "vow_binding", Name = "GEAR-SLOT VOWS", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" },
                Description = "You can take THE BAREFOOT, THE OPEN HAND and THE BARE SKULL. Each one leaves a gear slot empty for extra power." },
        // "SACRIFICE VOWS", not "SACRIFICIAL VOWS": the diagram prints every name under its node in
        // short lines, and SACRIFICIAL is the one word in the catalogue too long for a line.
        new() { Id = "vow_sacrifice", Name = "SACRIFICE VOWS", Cost = 3, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_3" },
                Description = "You can take FRAGILITY, RECKLESS OFFERING, THE UNGUARDED and THE UNBOUND. Each one costs you something." },

        // Attention, not power. An idle game's real currency is ATTENTION, and a bag of ninety Commons
        // spends it on nothing.
        new() { Id = "ledger", Name = "OPENS AUTO-SELL AND FORGE", Cost = 1, Effect = UnlockEffect.Convenience,
                Description = "Does nothing by itself. It unlocks the auto-sell traits, the forge traits and KEYSTONE — GREED." },
        new() { Id = "filter_common", Name = "AUTO-SELL COMMON DROPS", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" },
                Description = "Common items are sold the moment they drop. Your bag keeps only what matters." },
        new() { Id = "filter_uncommon", Name = "AUTO-SELL UNCOMMON DROPS", Cost = 3, Effect = UnlockEffect.Convenience,
                Requires = new[] { "filter_common" },
                Description = "Uncommon items are sold the moment they drop too. Rare and better are always kept." },
        new() { Id = "forge_insight", Name = "OPENS THE FORGE UPGRADES", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ledger" },
                Description = "Does nothing by itself. It unlocks SALVAGE PAYS 15% MORE and AUTO-MERGE SPARE ITEMS." },
        new() { Id = "efficient_forge", Name = "SALVAGE PAYS 15% MORE", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "forge_insight" },
                Description = "Salvaging an item gives you 15% more material. Selling still pays more than salvaging." },
        new() { Id = "auto_merge", Name = "AUTO-MERGE SPARE ITEMS", Cost = 2, Effect = UnlockEffect.Convenience,
                Requires = new[] { "forge_insight" },
                Description = "The forge merges your spare items by itself after every expedition." },

        new() { Id = "recall_1", Name = "FASTER REGION MASTERY I", Cost = 1, Effect = UnlockEffect.Amplifier,
                Description = "Region mastery grows 5% faster." },
        new() { Id = "recall_2", Name = "FASTER REGION MASTERY II", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_1" },
                Description = "Region mastery grows another 5% faster. That is 10% in all." },
        new() { Id = "recall_3", Name = "FASTER REGION MASTERY III", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_2" },
                Description = "Region mastery grows another 5% faster. That is 15% in all." },
        new() { Id = "recall_4", Name = "FASTER REGION MASTERY IV", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "recall_3" },
                Description = "Region mastery grows another 5% faster. That is 20% in all." },

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
        // A keystone gate's Description says what it does — it lets you wear that keystone, and names the
        // Build screen as the place to do it. What the keystone DOES is the keystone's own Blurb, which
        // MemoryDustText reads off the catalogue — one sentence, one owner. A terminal's second sentence
        // is the one thing to know before spending twelve points: no second branch after this.

        // ── RUIN — power bought with safety. A Ruin hunter lives at low health on purpose, which makes
        //    the skill tree's ENDURE branch nearly worthless to them: the two trees interact rather
        //    than stack, and that is what stops "take everything good" being a strategy. ────────────
        new() { Id = "ks_glass_cannon", Road = TraitRoad.Ruin, Name = "KEYSTONE — GLASS CANNON", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" }, GrantsKeystone = "glass_cannon",
                Description = "Lets you wear the keystone GLASS CANNON on the Build screen." },
        new() { Id = "ks_bloodlust", Road = TraitRoad.Ruin, Name = "KEYSTONE — BLOODLUST", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_glass_cannon" }, GrantsKeystone = "bloodlust",
                Description = "Lets you wear the keystone BLOODLUST on the Build screen." },
        new() { Id = "ks_blood_magic", Road = TraitRoad.Ruin, Name = "KEYSTONE — BLOOD MAGIC", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_bloodlust" }, GrantsKeystone = "blood_magic",
                Description = "Lets you wear the keystone BLOOD MAGIC on the Build screen." },
        new() { Id = "ks_reaper", Road = TraitRoad.Ruin, Name = "KEYSTONE — REAPER", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_blood_magic" }, GrantsKeystone = "reaper",
                Description = "Lets you wear the keystone REAPER on the Build screen. After this you cannot afford to finish another branch of the tree." },

        // ── THE CHARGE SPUR, one rung per road at the same cost (the roads must stay within two
        //    points of each other — enforced by test). Ruin spends the pool, Aegis winds it, Artifice
        //    bends it, Avarice holds it: a build that wants two of these walks two roads, and that
        //    split IS the min/max texture the playtest asked for. No spur rung sits on a terminal's
        //    path, so it competes with depth rather than gating it.
        new() { Id = "ks_rend", Road = TraitRoad.Ruin, Name = "KEYSTONE — REND", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_glass_cannon" }, GrantsKeystone = "rend",
                Description = "Lets you wear the keystone REND on the Build screen." },

        // ── AEGIS — the wall. The only path that makes Bruiser bands routine, and the natural partner
        //    of the skill tree's ENDURE branch. ──────────────────────────────────────────────────
        new() { Id = "ks_ironclad", Road = TraitRoad.Aegis, Name = "KEYSTONE — IRONCLAD", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "socket_2" }, GrantsKeystone = "ironclad",
                Description = "Lets you wear the keystone IRONCLAD on the Build screen." },
        new() { Id = "ks_juggernaut", Road = TraitRoad.Aegis, Name = "KEYSTONE — JUGGERNAUT", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_ironclad" }, GrantsKeystone = "juggernaut",
                Description = "Lets you wear the keystone JUGGERNAUT on the Build screen." },
        new() { Id = "ks_undying", Road = TraitRoad.Aegis, Name = "KEYSTONE — UNDYING", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_juggernaut" }, GrantsKeystone = "undying",
                Description = "Lets you wear the keystone UNDYING on the Build screen." },
        new() { Id = "ks_titan", Road = TraitRoad.Aegis, Name = "KEYSTONE — TITAN", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_undying" }, GrantsKeystone = "titan",
                Description = "Lets you wear the keystone TITAN on the Build screen. After this you cannot afford to finish another branch of the tree." },
        new() { Id = "ks_dynamo", Road = TraitRoad.Aegis, Name = "KEYSTONE — DYNAMO", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_ironclad" }, GrantsKeystone = "dynamo",
                Description = "Lets you wear the keystone DYNAMO on the Build screen." },
        new() { Id = "ks_lodestone", Road = TraitRoad.Avarice, Name = "KEYSTONE — LODESTONE", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_greed" }, GrantsKeystone = "lodestone",
                Description = "Lets you wear the keystone LODESTONE on the Build screen." },
        new() { Id = "ks_capacitor", Road = TraitRoad.Artifice, Name = "KEYSTONE — CAPACITOR", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_echo" }, GrantsKeystone = "capacitor",
                Description = "Lets you wear the keystone CAPACITOR on the Build screen." },

        // ── AVARICE — the economy build. It buys no combat power at all, which is what makes it a real
        //    choice: it trades depth for the gear that eventually buys depth. HOARDER is what stops it
        //    being a dead end — the haul becomes force. ───────────────────────────────────────────
        new() { Id = "ks_greed", Road = TraitRoad.Avarice, Name = "KEYSTONE — GREED", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ledger" }, GrantsKeystone = "greed",
                Description = "Lets you wear the keystone GREED on the Build screen." },
        new() { Id = "ks_discerning_eye", Road = TraitRoad.Avarice, Name = "KEYSTONE — DISCERNING EYE", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_greed" }, GrantsKeystone = "discerning_eye",
                Description = "Lets you wear the keystone DISCERNING EYE on the Build screen." },
        new() { Id = "ks_fortune", Road = TraitRoad.Avarice, Name = "KEYSTONE — FORTUNE", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_discerning_eye" }, GrantsKeystone = "fortune",
                Description = "Lets you wear the keystone FORTUNE on the Build screen." },
        new() { Id = "ks_hoarder", Road = TraitRoad.Avarice, Name = "KEYSTONE — HOARDER", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_fortune" }, GrantsKeystone = "hoarder",
                Description = "Lets you wear the keystone HOARDER on the Build screen. After this you cannot afford to finish another branch of the tree." },

        // ── ARTIFICE — behaviour over numbers. The path for players who want their build to do
        //    something strange rather than something large. Its third rung is VOWS PAY 25% MORE (every vow
        //    pays 25% more — DustEffects.VowPowerMultiplier) rather than a keystone: the Form-combo triggers it used to hold now belong to the skill
        //    tree, and vows are the purest "behaviour, not numbers" thing the game has. ───────────
        new() { Id = "ks_echo", Road = TraitRoad.Artifice, Name = "KEYSTONE — ECHO", Cost = 4, Effect = UnlockEffect.Expansion,
                Requires = new[] { "vow_study_1" }, GrantsKeystone = "echo",
                Description = "Lets you wear the keystone ECHO on the Build screen." },
        new() { Id = "ks_venomancer", Road = TraitRoad.Artifice, Name = "KEYSTONE — VENOMANCER", Cost = 6, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_echo" }, GrantsKeystone = "venomancer",
                Description = "Lets you wear the keystone VENOMANCER on the Build screen." },
        // Requires only its own path, NOT the whole vow chain. Hanging it off vow_sacrifice made ARTIFICE
        // cost 38 against the other paths' 31-32 and put its terminal out of reach on its own — measured,
        // not guessed. A path that is more expensive than the others is not "flavourful", it is dead.
        new() { Id = "artifice_vows", Road = TraitRoad.Artifice, Name = "VOWS PAY 25% MORE", Cost = 8, Effect = UnlockEffect.Expansion,
                Requires = new[] { "ks_venomancer" },
                Description = "Every vow you take pays 25% more." },
        new() { Id = "ks_weaver", Road = TraitRoad.Artifice, Name = "KEYSTONE — WEAVER", Cost = 12, Effect = UnlockEffect.Expansion,
                Requires = new[] { "artifice_vows" }, GrantsKeystone = "weaver",
                Description = "Lets you wear the keystone WEAVER on the Build screen. After this you cannot afford to finish another branch of the tree." },

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
        // brings back more, Artifice casts sooner. The NUMBER lives in the Mods; the Description repeats
        // it in the exact words MemoryDustText.ModsSentence generates, and a test holds the two equal, so
        // retuning a multiplier without retyping the sentence fails the build instead of lying quietly.
        // Ids deliberately do not start with "ks_": the path-walking test uses that prefix to find a
        // road's keystone chain, and these are beside the chain, not on it.

        // Ruin: the blade.
        new() { Id = "ruin_edge_1", Road = TraitRoad.Ruin, Name = "HARDER HITS I", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_bloodlust" }, Mods = new BuildMods(1.05f, 1f, 1f, 1f, 1f),
                Description = "Your hits do 5% more damage." },
        new() { Id = "ruin_edge_2", Road = TraitRoad.Ruin, Name = "HARDER HITS II", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_blood_magic" }, Mods = new BuildMods(1.06f, 1f, 1f, 1f, 1f),
                Description = "Your hits do 6% more damage." },
        new() { Id = "ruin_edge_3", Road = TraitRoad.Ruin, Name = "HARDER HITS III", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_reaper" }, Mods = new BuildMods(1.12f, 1f, 1f, 1f, 1f),
                Description = "Your hits do 12% more damage." },

        // Aegis: the hide.
        new() { Id = "aegis_skin_1", Road = TraitRoad.Aegis, Name = "MORE HEALTH I", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_juggernaut" }, Mods = new BuildMods(1f, 1.05f, 1f, 1f, 1f),
                Description = "You have 5% more health." },
        new() { Id = "aegis_skin_2", Road = TraitRoad.Aegis, Name = "MORE HEALTH II", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_undying" }, Mods = new BuildMods(1f, 1.06f, 1f, 1f, 1f),
                Description = "You have 6% more health." },
        new() { Id = "aegis_skin_3", Road = TraitRoad.Aegis, Name = "MORE HEALTH III", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_titan" }, Mods = new BuildMods(1f, 1.12f, 1f, 1f, 1f),
                Description = "You have 12% more health." },

        // Avarice: the purse. Haul is how much comes back; Rarity is how good it is. The notable moves
        // both a little, because a road about loot should end on loot of both kinds.
        new() { Id = "avarice_purse_1", Road = TraitRoad.Avarice, Name = "MORE LOOT I", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_discerning_eye" }, Mods = new BuildMods(1f, 1f, 1f, 1.05f, 1f),
                Description = "You bring back 5% more loot." },
        new() { Id = "avarice_purse_2", Road = TraitRoad.Avarice, Name = "RARER FINDS I", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_fortune" }, Mods = new BuildMods(1f, 1f, 1f, 1f, 1.06f),
                Description = "Rare items drop 6% more often." },
        new() { Id = "avarice_purse_3", Road = TraitRoad.Avarice, Name = "MORE AND RARER LOOT", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_hoarder" }, Mods = new BuildMods(1f, 1f, 1f, 1.08f, 1.05f),
                Description = "You bring back 8% more loot. Rare items drop 5% more often." },

        // Artifice: the hands.
        new() { Id = "artifice_hands_1", Road = TraitRoad.Artifice, Name = "FASTER SKILLS I", Cost = 2, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_venomancer" }, Mods = new BuildMods(1f, 1f, 1.05f, 1f, 1f),
                Description = "Your skills come back 5% faster." },
        new() { Id = "artifice_hands_2", Road = TraitRoad.Artifice, Name = "FASTER SKILLS II", Cost = 3, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "artifice_vows" }, Mods = new BuildMods(1f, 1f, 1.06f, 1f, 1f),
                Description = "Your skills come back 6% faster." },
        new() { Id = "artifice_hands_3", Road = TraitRoad.Artifice, Name = "FASTER SKILLS III", Cost = 4, Effect = UnlockEffect.Amplifier,
                Requires = new[] { "ks_weaver" }, Mods = new BuildMods(1f, 1f, 1.12f, 1f, 1f),
                Description = "Your skills come back 12% faster." },

        // ── The quiet mark. Requires one node from each path's FIRST rung, so it says "you have seen
        //    all four roads", not "you bought the tree" — which is impossible and meant to be. ─────
        new() { Id = "attunement", Name = "A KEEPSAKE — NO EFFECT", Cost = 4, Effect = UnlockEffect.Convenience,
                Requires = new[] { "ks_glass_cannon", "ks_ironclad", "ks_greed", "ks_echo", "socket_3" },
                Description = "Does nothing in the fight. It only marks that you have started all four branches." },
    };
}
