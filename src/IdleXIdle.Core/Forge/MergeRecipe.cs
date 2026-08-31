using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Forging;

/// <summary>
/// What a trio of items MAKES. Decided by what you put in — never rolled.
/// </summary>
/// <remarks>
/// <para>
/// The merge output used to be <c>inputs[rng.Next(inputs.Count)].BaseType</c>, under a comment
/// claiming "the player's choice of inputs steers the result". With three same-type inputs that line
/// is a no-op; with mixed inputs it is a <b>coin flip</b>. There was no hybrid, no family, and no
/// combination rule anywhere in the codebase — the design's "selected cross-family combinations can
/// create predictable hybrid equipment" had precisely nothing behind it.
/// </para>
/// <para>
/// <b>Predictable is the entire feature.</b> A random output makes the Forge a slot machine, where the
/// only correct move is to shovel in whatever you have; a decided output makes it a recipe book, where
/// what you feed it is a plan. Same three items, same result, every time — that is what lets a player
/// aim.
/// </para>
/// </remarks>
public static class MergeRecipe
{
    /// <summary>
    /// The output type: a MAJORITY rules, and a three-way split makes a HYBRID.
    /// </summary>
    /// <remarks>
    /// Two-of-a-kind is the ordinary path — feed it two weapons and a charm, you get a weapon, and the
    /// charm was the fuel. All three different is the deliberate one: the game's only route to a Focus
    /// that a player builds rather than finds.
    /// </remarks>
    public static ItemBaseType TypeOf(IReadOnlyList<ItemInstance> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0) throw new ArgumentException("A merge needs inputs.", nameof(inputs));

        var byType = inputs.GroupBy(i => i.BaseType).ToList();

        // A clear majority wins. Ordering by count then by the enum keeps it deterministic when two
        // types tie — a tie-break by hash or by rng would put the coin flip straight back.
        var top = byType.OrderByDescending(g => g.Count()).ThenBy(g => (int)g.Key).First();
        if (top.Count() > 1) return top.Key;

        // Every input a different type: the hybrid table decides, not chance.
        return HybridOf(inputs.Select(i => i.BaseType));
    }

    /// <summary>True when no two inputs share a type — the cross-family case.</summary>
    public static bool IsHybrid(IReadOnlyList<ItemInstance> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return inputs.Count > 1 && inputs.Select(i => i.BaseType).Distinct().Count() == inputs.Count;
    }

    /// <summary>
    /// The cross-family table: which mixture yields which hybrid.
    /// </summary>
    /// <remarks>
    /// Authored, not computed. A rule like "the rarest input wins" would be predictable and still
    /// pointless — the player would just learn to sort. These are chosen so each mixture makes a kind
    /// of sense: metal and a trinket sharpen into a WEAPON; a trinket and a catalyst bind into a
    /// CHARM; metal and a catalyst attune into a FOCUS.
    /// </remarks>
    private static readonly Dictionary<string, ItemBaseType> HybridTable = new()
    {
        [Key(ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.Material)] = ItemBaseType.Weapon,
        [Key(ItemBaseType.Weapon, ItemBaseType.AbilityFocus, ItemBaseType.Material)] = ItemBaseType.AbilityFocus,
        [Key(ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Material)] = ItemBaseType.Charm,
        [Key(ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus)] = ItemBaseType.AbilityFocus,
        [Key(ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.CreatureCore)] = ItemBaseType.Charm,
        [Key(ItemBaseType.Weapon, ItemBaseType.AbilityFocus, ItemBaseType.CreatureCore)] = ItemBaseType.Weapon,
        [Key(ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.CreatureCore)] = ItemBaseType.AbilityFocus,
        [Key(ItemBaseType.Weapon, ItemBaseType.Material, ItemBaseType.CreatureCore)] = ItemBaseType.Weapon,
        [Key(ItemBaseType.Charm, ItemBaseType.Material, ItemBaseType.CreatureCore)] = ItemBaseType.Charm,
        [Key(ItemBaseType.AbilityFocus, ItemBaseType.Material, ItemBaseType.CreatureCore)] = ItemBaseType.AbilityFocus,
    };

    /// <summary>
    /// Priority for the GENERIC hybrid rule — the most equippable input wins.
    /// </summary>
    /// <remarks>
    /// The authored table covers the original five-type mixtures with hand-picked flavour. It cannot
    /// scale to eight wearable slots (that is C(10,3)=120 mixtures, most of them meaningless "recipes"),
    /// so an unlisted mixture resolves by this order instead: a merge always upgrades TOWARD the best
    /// wearable you fed it, and only falls to Material/Core if you fed nothing else. Deterministic and
    /// always one of the inputs — predictable, never a coin flip — which is the whole point of the merge.
    /// </remarks>
    private static readonly ItemBaseType[] HybridPriority =
    {
        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus,
        ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
        ItemBaseType.Material, ItemBaseType.CreatureCore,
    };

    /// <summary>Is this mixture in the AUTHORED table (as opposed to resolving by the generic rule)?</summary>
    public static bool HasRecipe(IEnumerable<ItemBaseType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return HybridTable.ContainsKey(Key(types.Distinct()));
    }

    public static ItemBaseType HybridOf(IEnumerable<ItemBaseType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var set = types.Distinct().ToList();
        if (set.Count == 1) return set[0];

        // Authored flavour first; then the generic "most equippable input wins" rule. HybridPriority
        // lists every type, and the set is a subset, so First always finds an input — never the fallback.
        return HybridTable.TryGetValue(Key(set), out var hybrid)
            ? hybrid
            : HybridPriority.First(set.Contains);
    }

    /// <summary>
    /// The output's ELEMENT: a shared element carries through; a mixture is inert.
    /// </summary>
    /// <remarks>
    /// This is the reason to hoard by element rather than by rarity. Feeding three Shadow items yields
    /// a Shadow item, which then matters against a region the Source cycle says Shadow beats — so the
    /// Forge finally has an input the world map asks for. A mixed trio yields null (no element), which
    /// is a real cost, not a randomisation: mixing is how you get a hybrid TYPE, and it costs you the
    /// element to do it.
    /// </remarks>
    public static Source? ElementOf(IReadOnlyList<ItemInstance> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var elements = inputs.Select(i => i.Element).Distinct().ToList();
        return elements.Count == 1 ? elements[0] : null;
    }

    private static string Key(params ItemBaseType[] types) => Key(types.ToList());

    /// <summary>Order-independent key: {weapon, charm, material} and {charm, material, weapon} are one recipe.</summary>
    private static string Key(IEnumerable<ItemBaseType> types)
        => string.Join("|", types.Select(t => (int)t).OrderBy(i => i));
}
