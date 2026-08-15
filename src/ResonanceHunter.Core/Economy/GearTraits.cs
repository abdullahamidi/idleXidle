using System;
using System.Collections.Generic;
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

    /// <summary>
    /// What a trait does <b>on this exact item</b>, in numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BlurbOf"/> can only ever be qualitative — "far harder hits, skills come slower" — because
    /// the real figures depend on the item's rarity AND its refine level, and a sentence written next to
    /// the enum knows neither. So the player was told the DIRECTION of every trait and never the size of
    /// one, which is the same as not being told: "harder hits" is a decision only once you know whether
    /// it means four percent or forty.
    /// </para>
    /// <para>
    /// This reads the item's own <see cref="ModsFor"/> result and prints every channel that moved, with
    /// its sign. It is generated, so it cannot drift from the trait it describes, and it answers the
    /// question the blurb raises rather than repeating it.
    /// </para>
    /// </remarks>
    public static string EffectOf(ItemInstance? item)
    {
        if (item is null || TraitOf(item) is not { } trait) return "";

        var m = ModsFor(trait, item.Rarity, item.ItemLevel);
        var parts = new List<string>(4);

        void Channel(string label, float value)
        {
            var pct = (value - 1f) * 100f;
            if (MathF.Abs(pct) < 0.5f) return;                    // a rounding artefact is not an effect
            parts.Add($"{(pct > 0 ? "+" : "")}{pct:0}% {label}");
        }

        Channel("DMG", m.Damage);
        Channel("HP", m.Health);
        Channel("SKILL", m.SkillRate);
        Channel("HAUL", m.Haul);

        return parts.Count == 0 ? "" : string.Join("  ", parts);
    }

    /// <summary>One line, player-facing. States the cost as plainly as the benefit.</summary>
    /// <remarks>The DIRECTION of the trade. <see cref="EffectOf"/> gives the size, per item.</remarks>
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
    /// <param name="itemLevel">
    /// The item's REFINE level. 1 leaves the trait exactly where rarity alone put it.
    /// </param>
    /// <remarks>
    /// Item level was ignored here for the whole of development, and it is the reason REFINE felt like
    /// nothing. A weapon's damage multiplier and a charm's defence and health all scale with level; a
    /// TRAIT did not. For a helm, boots, gloves or a ring — slots that contribute through their trait and
    /// their affixes and nothing else — that meant refining a COMMON one cost scrap and gold and changed
    /// literally zero, and refining any other rarity moved only half of what the item was worth.
    ///
    /// The same <see cref="Gear.ItemLevelFactor"/> the weapon and charm already used, applied to the
    /// rarity term, so a level-1 item is unchanged and the curves stay in the same family.
    /// </remarks>
    public static GearMods ModsFor(GearTrait trait, Rarity rarity, int itemLevel = 1)
    {
        // 0.20 (Common) → 7.00 (Legendary), damped so a Legendary is a big deal and not a 700% one,
        // then deepened by however much the player has poured into this particular piece.
        var up = 1f + 0.10f * Gear.RarityPower(rarity) * Gear.ItemLevelFactor(itemLevel);

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
        => TraitOf(item) is { } t && item is not null ? ModsFor(t, item.Rarity, item.ItemLevel) : GearMods.None;
}
