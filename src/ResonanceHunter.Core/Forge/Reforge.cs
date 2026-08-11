using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Forging;

/// <summary>
/// What it costs to re-roll an item's passives. Data-driven, so the numbers live here, not in the UI.
/// </summary>
/// <remarks>
/// <para>
/// Cost rises with rarity ON PURPOSE. The whole power fantasy rests on a Legendary being an event, and
/// a crafting system that let you casually perfect a Legendary for the same price as a Common would
/// quietly dissolve that — you would simply reforge the best base you own until it was ideal, and the
/// rarity curve would stop meaning anything. So a Legendary reforge is a real investment, measured in
/// several Legendaries' worth of dismantled materials.
/// </para>
/// <para>
/// The enchantment is dearer than the trait at every rarity: the trait is a numeric trade, but the
/// enchantment is the Form-combo — the build-defining roll that turns "I run Aura" into "my Aura build
/// works". You should churn traits freely and commit to an enchant reforge.
/// </para>
/// </remarks>
public sealed record ReforgeTuning
{
    /// <summary>Material cost to re-roll a TRAIT, indexed by <see cref="Rarity"/> (Common..Legendary).</summary>
    public IReadOnlyList<int> TraitCost { get; init; } = new[] { 8, 12, 20, 45, 100 };

    /// <summary>
    /// Material cost to re-roll an ENCHANTMENT, indexed by rarity. Zero below Rare — those items have no
    /// enchantment to re-roll, and the Forge never offers the button there.
    /// </summary>
    public IReadOnlyList<int> EnchantCost { get; init; } = new[] { 0, 0, 45, 110, 260 };

    public static ReforgeTuning Default { get; } = new();

    public int TraitCostFor(Rarity r) => TraitCost[(int)r];
    public int EnchantCostFor(Rarity r) => EnchantCost[(int)r];
}

/// <summary>The outcome of a reforge: the new item, what it cost, or why it was refused.</summary>
/// <remarks>
/// The product carries the SAME <see cref="ItemInstance.InstanceId"/> as the input — a reforge changes
/// what an item IS, not which item it is. So anything tracking it by id (the merge tray, worn slots)
/// keeps pointing at the reforged version rather than losing it.
/// </remarks>
public sealed record ReforgeResult
{
    public ItemInstance? Product { get; init; }

    /// <summary>Materials the caller must spend on success. Zero on a rejection.</summary>
    public int Cost { get; init; }

    public bool Success => Product is not null;
    public string? Rejection { get; init; }
}

/// <summary>
/// The Reforge: spend materials to re-roll an item's trait or enchantment to a new RANDOM one.
/// </summary>
/// <remarks>
/// <para>
/// Items were <b>frozen</b>: both the trait (<see cref="GearTraits"/>) and the enchantment
/// (<see cref="Enchantments"/>) are derived from the id and never stored, so a drop's passives were
/// fixed at the moment it minted. The playtest ask was the opposite — <i>"item properties are shallow,
/// I must be able to change/customise them"</i>. This is the verb that changes them.
/// </para>
/// <para>
/// It is a pure roller: given an item, an RNG and the tuning, it returns the new item and the price.
/// It does NOT touch the player's materials — the caller checks affordability and spends, exactly as
/// the Forge's merge is a pure function and the screen owns the inventory mutation. That keeps the
/// whole thing unit-testable without a Hunter or a stash.
/// </para>
/// <para>
/// <b>The re-roll never returns the current value.</b> Paying materials to land the same trait you
/// already had reads as a bug, not a gamble — so the roll is drawn from the slot pool MINUS what the
/// item carries now. Every reforge is therefore a real change.
/// </para>
/// </remarks>
public static class Reforge
{
    public static ReforgeResult ReforgeTrait(ItemInstance item, Random rng, ReforgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(tuning);

        if (Gear.SlotFor(item.BaseType) is not { } slot)
            return new ReforgeResult { Rejection = "ONLY WEAPONS, CHARMS AND FOCUSES HAVE A TRAIT." };

        var current = GearTraits.TraitOf(item);
        var picked = Roll(GearTraits.PoolFor(slot), current, rng);

        return new ReforgeResult
        {
            Product = item with { TraitOverride = picked },
            Cost = tuning.TraitCostFor(item.Rarity),
        };
    }

    public static ReforgeResult ReforgeEnchant(ItemInstance item, Random rng, ReforgeTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(tuning);

        if (Gear.SlotFor(item.BaseType) is not { } slot)
            return new ReforgeResult { Rejection = "ONLY WEAPONS, CHARMS AND FOCUSES HAVE AN ENCHANTMENT." };

        if (item.Rarity < Enchantments.MinimumRarity)
            return new ReforgeResult { Rejection = "ONLY RARE AND BETTER ITEMS CARRY AN ENCHANTMENT." };

        var current = Enchantments.Of(item)?.Kind;
        var picked = Roll(Enchantments.PoolFor(slot), current, rng);

        return new ReforgeResult
        {
            Product = item with { EnchantOverride = picked },
            Cost = tuning.EnchantCostFor(item.Rarity),
        };
    }

    /// <summary>Pick a pool member that is NOT <paramref name="current"/>. Falls back to any if the pool is a singleton.</summary>
    private static T Roll<T>(IReadOnlyList<T> pool, T? current, Random rng) where T : struct
    {
        var candidates = pool.Where(x => !x.Equals(current)).ToArray();
        // A pool of one (or a current value somehow absent from the pool) leaves nothing to change to;
        // roll the whole pool rather than throw. Every real pool here has >= 3 members, so this is a guard.
        return candidates.Length > 0 ? candidates[rng.Next(candidates.Length)] : pool[rng.Next(pool.Count)];
    }
}
