using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>
/// An enchantment that changes what HAPPENS, not what a number is.
/// </summary>
/// <remarks>
/// <para>
/// The design asks for effects that change gameplay — "splitting projectiles, spawning a minion after
/// a kill, converting critical hits into poison, increasing production while at low health" — and the
/// codebase could not express a single one of them. Not because nobody had written them: because
/// <see cref="GearMods"/> is a four-float struct, so by construction every gear effect in the game
/// could only ever be a multiplier. The data foreclosed the design.
/// </para>
/// <para>
/// So an enchantment is NOT a number. It is a named trigger the battle asks about at a specific
/// moment: "did anything want to happen when this creature killed something?" A multiplier answers
/// "how much"; these answer "and then what".
/// </para>
/// </remarks>
public enum EnchantKind
{
    /// <summary>
    /// On a kill, the spoils splinter richer — a bump of loot quality on the wave's haul.
    /// </summary>
    /// <remarks>
    /// It was written as "a second, weaker blow at the next wave's enemy", but the single-champion, one-enemy-
    /// per-wave model can't express a blow at a NEXT enemy, so the effect is loot quality on the kill. The
    /// name and blurb now say what it does rather than what the squad model once let it do.
    /// </remarks>
    Splinter,

    /// <summary>On a kill, buds a spare core. The vision's "spawn a minion after a kill", as economy.</summary>
    Harvest,

    /// <summary>A skill cast also poisons: damage that lands over the following seconds.</summary>
    Venom,

    /// <summary>While the wearer is below a third health, the haul swells. Reward for the edge.</summary>
    Desperation,

    /// <summary>On taking a fatal blow, survive at 1 HP. Once per expedition.</summary>
    Undying,

    // ── Form-combo enchantments: these do NOTHING unless your BUILD runs the Form they name. ─────────
    // The whole point of the loot rework — an item is not "a bigger number", it is "this makes my Volley
    // build fire more" or "this makes my Mark build worth committing to". A player without that Form in
    // their skills feels the item is dead, and reaches for the one that fits the build they are building.

    /// <summary>VOLLEY (Projectile) fires one extra time. Dead unless you run Projectile.</summary>
    Overdraw,

    /// <summary>Your MARK's amplify window lasts far longer. Dead unless you run Mark.</summary>
    Linger,

    /// <summary>Your AURA ticks faster — more damage over a long fight. Dead unless you run Aura.</summary>
    Radiance,

    /// <summary>Your STRIKE executes a weakened enemy for far more. Dead unless you run Strike.</summary>
    Execute,

    /// <summary>Your TRAP re-arms far faster, so it punishes every bite. Dead unless you run Trap.</summary>
    Coiled,

    /// <summary>Your TRANSFORMATION leeches far more health. Dead unless you run Transformation.</summary>
    Siphon,
}

/// <summary>An enchantment on a specific item, at a specific strength.</summary>
public sealed record Enchantment(EnchantKind Kind, float Magnitude)
{
    public string Name => Kind.ToString().ToUpperInvariant();

    /// <summary>
    /// What it does, in the player's words. States the TRIGGER, because the trigger IS the effect.
    /// </summary>
    /// <remarks>
    /// Kept under ~21 characters — the Forge gives the blurb a fixed column from x=344 to the panel
    /// edge, and that is what fits. The first draft ("ON A KILL: 38% CHANCE OF A SPARE CORE.") ran
    /// clean off the panel and lost the half of the sentence that carried the meaning.
    /// </remarks>
    public string Blurb => Kind switch
    {
        EnchantKind.Splinter => "ON KILL: RICHER LOOT",
        EnchantKind.Harvest => $"ON KILL: {Magnitude * 100f:0}% CORE",
        EnchantKind.Venom => $"SKILLS POISON +{Magnitude * 100f:0}%",
        EnchantKind.Desperation => $"NEAR DEATH: HAUL +{Magnitude * 100f:0}%",
        EnchantKind.Overdraw => "VOLLEY FIRES +1",
        EnchantKind.Linger => "MARK LASTS LONGER",
        EnchantKind.Radiance => "AURA TICKS FASTER",
        EnchantKind.Execute => "STRIKE EXECUTES LOW",
        EnchantKind.Coiled => "TRAP RE-ARMS FAST",
        EnchantKind.Siphon => "TRANSFORM LEECHES 2X",
        _ => "SURVIVES A FATAL BLOW",
    };

    /// <summary>Which Form this enchantment needs to matter — null for the ones that always work.</summary>
    /// <remarks>The Forge greys the blurb when the worn build lacks this Form, so a dead combo reads as dead.</remarks>
    public Abilities.Form? NeedsForm => Kind switch
    {
        EnchantKind.Overdraw => Abilities.Form.Projectile,
        EnchantKind.Linger => Abilities.Form.Mark,
        EnchantKind.Radiance => Abilities.Form.Aura,
        EnchantKind.Execute => Abilities.Form.Strike,
        EnchantKind.Coiled => Abilities.Form.Trap,
        EnchantKind.Siphon => Abilities.Form.Transformation,
        _ => null,
    };
}

/// <summary>
/// Which enchantment an item carries, and how strong.
/// </summary>
/// <remarks>
/// Derived from the InstanceId like <see cref="GearTraits"/>, and for the same reasons: nothing to
/// store, no migration, and every item already in a stash grows one. A DIFFERENT hash salt from the
/// trait, though — sharing one would correlate the two axes permanently, so every HEAVY weapon in the
/// game would carry the same enchantment and two axes would collapse back into one.
/// </remarks>
public static class Enchantments
{
    /// <summary>Only rare and better are enchanted. A Common must stay a Common.</summary>
    public const Rarity MinimumRarity = Rarity.Rare;

    // Each slot leans a way: weapons hit, charms keep you alive, focuses SHAPE your skills — which is
    // where most of the Form-combo enchantments live, because that is exactly what a Focus is for.
    //
    // Every pool size is COPRIME TO 4 ON PURPOSE (here: 3, 3, 5). There are 4 traits, and the enchant is
    // picked with `hash % poolLen` off the same InstanceId as the trait (different salt). FNV's low bits
    // are weak, so a pool of 4 — sharing the factor 2 with the 4 traits — would make `%4` here track `%4`
    // there, collapsing the two axes into one (test_the_enchantment_does_not_correlate_with_the_trait
    // catches it). 3 and 5 are coprime to 4 and use more of the hash, so the axes stay independent. A pool
    // of 4 or 6 would re-correlate them — grow a pool to 5 or 7, never to an even number.
    private static readonly EnchantKind[] WeaponPool = { EnchantKind.Splinter, EnchantKind.Venom, EnchantKind.Harvest };
    // The CHARM keeps you alive, so it carries the sustain combo (SIPHON — Transformation's extra leech)
    // alongside the two survival triggers.
    private static readonly EnchantKind[] CharmPool = { EnchantKind.Undying, EnchantKind.Desperation, EnchantKind.Siphon };
    // The FOCUS shapes your SKILLS, so the skill-cadence combos live here — one per offensive Form
    // (VOLLEY/MARK/AURA/STRIKE/TRAP). That is what makes a Focus worth chasing: it is the slot that can
    // turn "I run Aura" into "my Aura build actually works".
    private static readonly EnchantKind[] FocusPool =
        { EnchantKind.Linger, EnchantKind.Radiance, EnchantKind.Overdraw, EnchantKind.Execute, EnchantKind.Coiled };

    public static EnchantKind[] PoolFor(GearSlot slot) => slot switch
    {
        GearSlot.Weapon => WeaponPool,
        // Jewellery keeps you going, like a charm; armour and the Focus shape your SKILLS' cadence.
        GearSlot.Charm or GearSlot.Ring => CharmPool,
        _ => FocusPool,   // Focus, Helm, Chest, Gloves, Boots — the skill-combo pool
    };

    public static Enchantment? Of(ItemInstance? item)
    {
        if (item is null || item.Rarity < MinimumRarity) return null;
        if (Gear.SlotFor(item.BaseType) is not { } slot) return null;

        // A REFORGE overrides the id-derived enchantment. The MinimumRarity gate above still runs first,
        // so a stray override on a sub-Rare item reads as no enchantment — which is correct, a Common
        // has none. The magnitude is NOT stored; it is always recomputed from rarity below.
        if (item.EnchantOverride is { } forced)
            return new Enchantment(forced, MagnitudeFor(forced, item.Rarity));

        var pool = PoolFor(slot);
        var hash = Fnv1a(item.InstanceId + "|ench");   // salted — see the class remarks
        var kind = pool[(int)(hash % (uint)pool.Length)];

        return new Enchantment(kind, MagnitudeFor(kind, item.Rarity));
    }

    /// <summary>
    /// How strong, by rarity. Chances stay well under certainty — a trigger that always fires is a stat.
    /// </summary>
    public static float MagnitudeFor(EnchantKind kind, Rarity rarity)
    {
        // Rare 1.20 → Legendary 7.00 on the shared curve, damped hard: these are not multipliers, they
        // are probabilities and fractions, and a 700% chance is nonsense.
        var t = Gear.RarityPower(rarity);

        return kind switch
        {
            EnchantKind.Splinter => Math.Min(0.60f, 0.15f + 0.05f * t),
            EnchantKind.Harvest => Math.Min(0.50f, 0.10f + 0.04f * t),
            EnchantKind.Venom => Math.Min(0.75f, 0.20f + 0.06f * t),
            EnchantKind.Desperation => Math.Min(1.00f, 0.20f + 0.08f * t),
            // UNDYING and the Form-combo enchantments are BINARY — the Volley either fires again or it
            // doesn't. Rarity buys them as drops (rarer = likelier to roll), not as a bigger percentage.
            _ => 1f,
        };
    }

    /// <summary>Every enchantment currently worn, across all three slots.</summary>
    public static IReadOnlyList<Enchantment> Worn(params ItemInstance?[] worn)
    {
        ArgumentNullException.ThrowIfNull(worn);
        return worn.Select(Of).OfType<Enchantment>().ToList();
    }

    /// <summary>Does the wearer carry this trigger, and at what strength? 0 when absent.</summary>
    public static float MagnitudeOf(IReadOnlyList<Enchantment> worn, EnchantKind kind)
    {
        ArgumentNullException.ThrowIfNull(worn);

        // Sum rather than max: two Harvest sources should both pay. Nothing in the pools can stack more
        // than three deep, so this cannot run away.
        return worn.Where(e => e.Kind == kind).Sum(e => e.Magnitude);
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
}
