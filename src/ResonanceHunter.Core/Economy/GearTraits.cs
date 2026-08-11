using System;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>An item's innate quirk. Every one is a TRADE, never a free bonus.</summary>
public enum GearTrait { Keen, Heavy, Swift, Savage, Warding, Vital, Greedy, Attuned, Focused, Wild }

/// <summary>
/// What worn gear does to the squad, as multipliers. 1.0 in every field means "no effect".
/// </summary>
public readonly record struct GearMods(float Damage, float Health, float Haul, float SkillRate)
{
    public static readonly GearMods None = new(1f, 1f, 1f, 1f);

    /// <summary>Gear stacks multiplicatively — three slots must never add up to a flat runaway.</summary>
    public GearMods Combine(GearMods o)
        => new(Damage * o.Damage, Health * o.Health, Haul * o.Haul, SkillRate * o.SkillRate);
}

/// <summary>
/// Item passives: the reason to look at an item instead of its rarity colour.
/// </summary>
/// <remarks>
/// <para>
/// Playtest verdict: <i>"I just click the best weapon and press F. There is no min/maxing."</i> — and
/// that was the literal truth of the code. Every gear stat was <c>RarityPower(rarity) × constant</c>,
/// which is monotonic in exactly one variable, so "which item" had a strictly-correct answer you could
/// read off the colour. The same failure as "everybody puts the tank in front", wearing different armour.
/// </para>
/// <para>
/// So every trait is a <b>trade</b>: HEAVY hits far harder but drags the skill clock, SWIFT is the
/// reverse, GREEDY pays more and dies faster. That crosses the trait axis against the rarity axis, and
/// an Epic SWIFT genuinely beats a Legendary HEAVY on a skill-heavy squad — which is a decision, not a
/// sort. <b>The upside scales with rarity and the downside does not</b>: a Legendary should still feel
/// like an event (that curve is the whole power fantasy), so rarity decides HOW MUCH and the trait
/// decides WHAT KIND. Rarity still wins ties; it just no longer wins arguments.
/// </para>
/// <para>
/// The trait is <b>derived from the item's InstanceId</b>, never stored. It costs nothing in the save,
/// needs no migration, and every item already in a player's stash grows a trait the moment this ships.
/// It also means the trait cannot drift out of sync with the item — there is only one source of truth.
/// </para>
/// </remarks>
public static class GearTraits
{
    private static readonly GearTrait[] WeaponPool = { GearTrait.Keen, GearTrait.Heavy, GearTrait.Swift, GearTrait.Savage };
    private static readonly GearTrait[] CharmPool = { GearTrait.Warding, GearTrait.Vital, GearTrait.Greedy, GearTrait.Wild };
    private static readonly GearTrait[] FocusPool = { GearTrait.Attuned, GearTrait.Focused, GearTrait.Swift, GearTrait.Greedy };

    public static GearTrait[] PoolFor(GearSlot slot) => slot switch
    {
        GearSlot.Weapon => WeaponPool,
        GearSlot.Charm => CharmPool,
        _ => FocusPool,
    };

    /// <summary>
    /// The item's trait, derived from its id.
    /// </summary>
    /// <remarks>
    /// FNV-1a rather than <see cref="string.GetHashCode()"/>: .NET randomises string hashing per
    /// process, so an item's trait would silently change every time the game restarted.
    /// </remarks>
    public static GearTrait? TraitOf(ItemInstance? item)
    {
        if (item is null) return null;
        if (Gear.SlotFor(item.BaseType) is not { } slot) return null;

        // A REFORGE won this slot: the player spent materials to overrule the id-derived roll. It takes
        // precedence over the hash — that overruling IS the feature — but only exists on a wearable item,
        // so the null-slot guard above still runs first.
        if (item.TraitOverride is { } forced) return forced;

        var pool = PoolFor(slot);
        return pool[(int)(Fnv1a(item.InstanceId) % (uint)pool.Length)];
    }

    private static uint Fnv1a(string s)
    {
        var hash = 2166136261u;
        foreach (var ch in s)
        {
            hash ^= ch;
            hash *= 16777619u;
        }
        return hash;
    }

    public static string NameOf(GearTrait t) => t.ToString().ToUpperInvariant();

    /// <summary>One line, player-facing. States the cost as plainly as the benefit.</summary>
    public static string BlurbOf(GearTrait t) => t switch
    {
        GearTrait.Keen => "Cleaner hits. No drawback.",
        GearTrait.Heavy => "Far harder hits — skills come slower.",
        GearTrait.Swift => "Skills come faster — hits land softer.",
        GearTrait.Savage => "Brutal hits — the squad is frailer.",
        GearTrait.Warding => "The squad is tougher — and hits softer.",
        GearTrait.Vital => "The squad is tougher. No drawback.",
        GearTrait.Greedy => "A richer haul — the squad is frailer.",
        GearTrait.Attuned => "Skills come faster. No drawback.",
        GearTrait.Focused => "Skills far faster — hits land softer.",
        _ => "Harder hits, faster skills — a frail squad.",
    };

    /// <summary>
    /// A trait's effect at a given rarity.
    /// </summary>
    /// <remarks>
    /// <c>up</c> scales with the rarity curve; the downside is a flat constant. See the class remarks —
    /// this asymmetry is deliberate and is what keeps a Legendary feeling like a Legendary.
    /// </remarks>
    public static GearMods ModsFor(GearTrait trait, Rarity rarity)
    {
        // 0.20 (Common) → 7.00 (Legendary), damped so a Legendary is a big deal and not a 700% one.
        var up = 1f + 0.10f * Gear.RarityPower(rarity);

        return trait switch
        {
            // No-drawback traits are deliberately the WEAK ones. "Safe" should cost you the ceiling.
            GearTrait.Keen => new(1f + 0.5f * (up - 1f), 1f, 1f, 1f),
            GearTrait.Vital => new(1f, 1f + 0.5f * (up - 1f), 1f, 1f),
            GearTrait.Attuned => new(1f, 1f, 1f, 1f + 0.5f * (up - 1f)),

            GearTrait.Heavy => new(up * 1.15f, 1f, 1f, 0.80f),
            GearTrait.Swift => new(0.90f, 1f, 1f, up * 1.10f),
            GearTrait.Savage => new(up * 1.10f, 0.85f, 1f, 1f),
            GearTrait.Warding => new(0.90f, up * 1.15f, 1f, 1f),
            GearTrait.Greedy => new(1f, 0.85f, up * 1.20f, 1f),
            GearTrait.Focused => new(0.85f, 1f, 1f, up * 1.20f),

            // WILD: everything up, health hard down. A real glass cannon, and a real gamble.
            _ => new(up * 1.10f, 0.70f, 1f, up * 1.10f),
        };
    }

    public static GearMods ModsOf(ItemInstance? item)
        => TraitOf(item) is { } t && item is not null ? ModsFor(t, item.Rarity) : GearMods.None;
}
