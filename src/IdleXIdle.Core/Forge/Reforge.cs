using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Forging;

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
    // (TraitCost is gone with the trait re-roll: an item's PREFIX is rolled at mint and immutable —
    //  playtest: re-rolling it read as the weapon turning into a different weapon. The Reforge's one
    //  remaining verb is the enchantment.)

    /// <summary>
    /// Material cost to re-roll an ENCHANTMENT, indexed by rarity. Zero below Rare — those items have no
    /// enchantment to re-roll, and the Forge never offers the button there.
    /// </summary>
    public IReadOnlyList<int> EnchantCost { get; init; } = new[] { 0, 0, 45, 110, 260 };

    public static ReforgeTuning Default { get; } = new();

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

    // ── Who pays ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The material an enchant re-roll spends: CRYSTAL for a Legendary, CORE for everything below it.
    /// </summary>
    /// <remarks>
    /// A Legendary's re-roll is the premium roll, so it spends the premium tier — that is what gives
    /// Crystal (the Legendary-salvage tier) a sink of its own. Lived in the screen as a private helper
    /// until the payment moved here; the screen's price line, the enable check and the payment itself
    /// must all agree on this, and three copies of a one-line rule is how they stop agreeing.
    /// </remarks>
    public static Material EnchantMaterial(Rarity rarity)
        => rarity == Rarity.Legendary ? Material.Crystal : Material.Core;

    /// <summary>
    /// Can this hunter pay for an enchant re-roll — with a REFORGE CHART, or with the material?
    /// </summary>
    /// <remarks>
    /// <b>A chart is a payment.</b> The Forge screen's enable check read only the material, so a player
    /// holding a chart and no Core saw a DISABLED button for a press that would have been free; and the
    /// press itself silently spent the chart while the price line kept printing "COSTS 110 CORE" — the
    /// playtest's "I pressed many times and only on the 5th or 6th was 110 deducted." This is the one
    /// predicate the screen asks now, so the button, the label and the payment cannot disagree.
    /// </remarks>
    public static bool CanPay(Hunter hunter, Material tier, int cost)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        return hunter.CharterCount(Charter.Reforge) > 0 || hunter.MaterialOf(tier) >= cost;
    }

    /// <summary>
    /// Settle an enchant re-roll's bill: a REFORGE CHART if one is held (the roll is free), else
    /// <paramref name="cost"/> of <paramref name="tier"/>. Takes nothing — and says so — if neither is there.
    /// </summary>
    /// <remarks>
    /// Call this AFTER <see cref="ReforgeEnchant"/> has succeeded, per <see cref="Hunter.SpendCharter"/>'s
    /// contract: validate first, spend last, so a chart is never spent on a refusal. The chart is
    /// preferred over the material when both are held, exactly as the screen always did — the change is
    /// that the caller now learns WHICH one paid and can tell the player.
    /// </remarks>
    public static ReforgePayment PayWith(Hunter hunter, Material tier, int cost)
    {
        ArgumentNullException.ThrowIfNull(hunter);
        if (hunter.SpendCharter(Charter.Reforge)) return new ReforgePayment(UsedChart: true, MaterialSpent: 0);
        if (cost > 0 && !hunter.SpendMaterial(tier, cost)) return ReforgePayment.Refused;
        return new ReforgePayment(UsedChart: false, MaterialSpent: cost);
    }
}

/// <summary>What a re-roll was paid with: the chart, or the material — or nothing, because it could not be.</summary>
public readonly record struct ReforgePayment(bool UsedChart, int MaterialSpent)
{
    /// <summary>True when something was actually taken. False means the hunter's stock is untouched.</summary>
    public bool Paid { get; init; } = true;

    /// <summary>Nothing was taken — the hunter held neither a chart nor enough material.</summary>
    public static ReforgePayment Refused => new(false, 0) { Paid = false };
}
