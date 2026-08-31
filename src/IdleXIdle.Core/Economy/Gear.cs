using System;
using System.Linq;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Economy;

/// <summary>
/// The EIGHT things a Hunter can wear. Everything else loot drops is currency or material.
/// </summary>
/// <remarks>
/// It was three (Weapon/Charm/Focus). The player asked for a real ARPG loadout — "gloves, leggings, a
/// necklace" — so five armour/jewellery slots joined them. The original three keep their special identity
/// (a Weapon multiplies damage, a Charm hardens you, a Focus quickens skills); the five new slots each add
/// their rarity-scaled trait mods, so more slots means more places for a good drop to matter. Weapon,
/// Charm and Focus keep their names and save keys so no old save breaks.
/// </remarks>
public enum GearSlot { Weapon, Charm, Focus, Helm, Chest, Gloves, Boots, Ring }

/// <summary>
/// Loot you can WEAR — the Hunter's power axis.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the game had no power fantasy at all. Training the attack stat to its cap bought
/// ~1.9x damage against content that gets ~75x tankier across the tier range, and loot's only use was
/// being sold for the Gleam that funded that 1.9x. So the player got measurably *weaker* the further
/// they went, and every item they picked up was a coin in disguise. Playtest verdict: "I didn't feel
/// that I'm getting stronger."
/// </para>
/// <para>
/// The split is Monster Hunter's, and it keeps Pillar 1 intact: <b>GEAR decides WHAT you can fight,
/// skill decides HOW FAST you kill it.</b> Reading attacks correctly still earns every weak point and
/// every crit — gear just means a Umbral boss is a fight rather than a wall.
/// </para>
/// </remarks>
public static class Gear
{
    /// <summary>Which slot an item type occupies, or null if it isn't wearable (materials, cores).</summary>
    public static GearSlot? SlotFor(ItemBaseType type) => type switch
    {
        ItemBaseType.Weapon => GearSlot.Weapon,
        ItemBaseType.Charm => GearSlot.Charm,
        ItemBaseType.AbilityFocus => GearSlot.Focus,
        ItemBaseType.Helm => GearSlot.Helm,
        ItemBaseType.Chest => GearSlot.Chest,
        ItemBaseType.Gloves => GearSlot.Gloves,
        ItemBaseType.Boots => GearSlot.Boots,
        ItemBaseType.Ring => GearSlot.Ring,
        _ => null,
    };

    public static bool IsWearable(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return SlotFor(item.BaseType) is not null;
    }

    /// <summary>The item's class, or null for "any class" — a universal slot, or a piece from before classes.</summary>
    public static ItemClass? ClassOf(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Class;
    }

    /// <summary>
    /// Can this champion wear this item? Universal slots always; class-locked slots when the item is
    /// their class or has none. The rule itself lives in <see cref="ItemClasses.CanWear(ItemClass, ItemInstance)"/>.
    /// </summary>
    public static bool CanWear(Characters.Character character, ItemInstance item)
        => ItemClasses.CanWear(character, item);

    /// <summary>
    /// A rarity's power contribution. <b>This curve IS the power fantasy</b> — it is intentionally
    /// steep, so that finding a Legendary is an event rather than a rounding error.
    /// </summary>
    public static float RarityPower(Rarity rarity) => rarity switch
    {
        Rarity.Common => 0.20f,
        Rarity.Uncommon => 0.50f,
        Rarity.Rare => 1.20f,
        Rarity.Epic => 3.00f,
        Rarity.Legendary => 7.00f,
        _ => 0f,
    };

    /// <summary>
    /// How much ITEM LEVEL refines a piece within its rarity tier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rarity sets the TIER; item level refines WITHIN it. Without this, a wave-40 boss drop was barely
    /// stronger than a wave-5 drop of the same rarity — the probe measured a full iL1→iL60 loadout adding
    /// ~6% depth, so "push deeper for better loot" (the whole reason ilvl exists — a drop's depth is a stat)
    /// paid almost nothing. This scales the rarity-DIRECT bonuses (weapon multiplier, charm/focus) so a
    /// maxed piece is roughly <b>1.7×</b> its own tier's floor by iL60, while staying below the next rarity
    /// up — the ladder is still rarity-first, but the rungs have depth now. (Affixes already scale with iL;
    /// this closes the gap on the levers that did not.)
    /// </para>
    /// <para>
    /// <b>IT SATURATES.</b> It used to be linear — <c>1 + (iL-1) × 0.012</c>, unbounded — against a Refine
    /// the Forge deliberately documents as an <i>infinite sink</i>. An infinite sink is good economy
    /// design; an infinite sink that pays unbounded POWER is a different thing, and it is what the
    /// playtest hit. Measured on the old curve: a Legendary reached 13× at iL60, 25× at iL200 and
    /// <b>92× at iL1000</b>, which is where "gear power ninety thousand, hits for 30–40k" came from. Worse,
    /// it broke the sentence directly above this one: a Rare ground to iL1000 reached 16.6× and sailed
    /// past a Legendary's 8.0× floor, so rarity stopped being the ladder and grinding replaced it.
    /// </para>
    /// <para>
    /// The curve below keeps every number the old one produced up to the level it was tuned for — iL60
    /// lands on 1.70 either way — and then flattens instead of climbing, approaching 2.0 and never
    /// reaching it. The gold sink stays infinite; only the power it buys is bounded. Held to both
    /// properties by <c>ProgressionCurveTest</c>.
    /// </para>
    /// </remarks>
    public static float ItemLevelFactor(int itemLevel)
    {
        var levels = Math.Max(0, itemLevel - 1);

        // 1 + x/(x + 25.3): zero at iL1, 0.700 at iL60 (matching the old linear 59 × 0.012), and
        // asymptotic to 1 — so the factor spans 1.0 to 2.0 across every level a player can ever buy.
        return 1f + levels / (levels + 25.3f);
    }

    /// <summary>A worn weapon multiplies damage: x1 with nothing, up to x8 with a Legendary — and deeper with item level.</summary>
    public static float WeaponDamageMultiplier(ItemInstance? weapon)
        => weapon is null ? 1f : 1f + RarityPower(weapon.Rarity) * ItemLevelFactor(weapon.ItemLevel);

    /// <summary>A worn charm hardens you: flat Defense, scaled by rarity and item level.</summary>
    public static int CharmDefenseBonus(ItemInstance? charm)
        => charm is null ? 0 : (int)MathF.Round(RarityPower(charm.Rarity) * 12f * ItemLevelFactor(charm.ItemLevel));

    /// <summary>A worn charm also carries health, deeper with item level.</summary>
    public static int CharmHealthBonus(ItemInstance? charm)
        => charm is null ? 0 : (int)MathF.Round(RarityPower(charm.Rarity) * 25f * ItemLevelFactor(charm.ItemLevel));

    // FocusResonanceBonus was removed here: it computed a "+N resonance" from the worn Focus but had ZERO
    // call sites — the solo pivot re-pointed the Focus slot at skill RATE (see Hunter.FocusAttunement), and
    // this flat-resonance helper was left orphaned. The live Focus contribution is FocusAttunement.

    /// <summary>
    /// A single glanceable "how strong am I" number for the HUD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Growth you can SEE is the whole point — an invisible power curve is the same as no power curve.
    /// Which is exactly why it has to read what the SIM reads.
    /// </para>
    /// <para>
    /// It used to take the raw weapon multiplier and the raw attack-power stat, so worn ENCHANTMENTS —
    /// which multiply damage and health in every fight the game runs — moved it by nothing at all. A
    /// player equipping a Legendary enchant watched their "power" stay still, and the EQUIP BEST button
    /// that ranks by this number would happily hand back the weaker piece. It now takes the same two
    /// multipliers <see cref="SoloBattle"/> does.
    /// </para>
    /// <para>
    /// SKILL RATE was the second thing it did not read, and it inverted the number. Damage per second is
    /// hit size TIMES cadence; the rating counted only the size. Half the armour traits in the game buy
    /// cadence with hit size — SWIFT is x0.90 damage for x1.10 rate, FOCUSED x0.85 for x1.20 — so wearing
    /// them made the number go DOWN while making the character stronger. Measured against median depth
    /// over forty runs on a trained fixture:
    /// </para>
    /// <code>
    ///   worn                 old power   new power   depth
    ///   weapon only               1270        1370      41
    ///   + chest WARDING           1295        1385      43
    ///   + chest SWIFT             1186        1923      49
    ///   + chest FOCUSED           1187        1982      47
    ///   + chest VITAL             1419        1526      44
    ///   + full WARDING set        2201        2369      53
    /// </code>
    /// <para>
    /// The old column ranks the two BEST pieces last and the worst-but-one first; the new column matches
    /// the depth order outright, bar the SWIFT/FOCUSED pair which sit two waves apart inside the noise.
    /// This is what the player was reporting as "wearing armour lowers my power": the armour was fine,
    /// the number was lying about it.
    /// </para>
    /// </remarks>
    /// <param name="damageMultiplier">The build's REAL damage multiplier (Hunter.SquadDamageMultiplier).</param>
    /// <param name="skillRate">The build's REAL cadence (Hunter.SquadSkillRate). Damage per second, not per hit.</param>
    /// <param name="healthMultiplier">The build's REAL health multiplier (Hunter.SquadHealthMultiplier).</param>
    /// <param name="critFactor">
    /// Expected-damage multiplier from crit: <c>1 + chance * (multiplier - 1)</c>. 1 means no crit.
    /// </param>
    /// <remarks>
    /// <b>CRIT WAS MISSING, AND ITS ABSENCE READ AS A DIFFERENT BUG.</b> Crit is one of six rollable
    /// affix stats and it is fully live in the fight, but this function consumed only the damage, skill
    /// rate, health and defense channels — so two items of the same rarity and item level could show
    /// wildly different ITEM POWER purely on which affixes they rolled, and the crit-heavy one showed
    /// LESS while being worth more. Playtest read that scatter as an element correlation: "a Nature item
    /// shows higher in an all-Spirit setup". The element was never involved; the number was just blind
    /// to a third of what makes an item good.
    ///
    /// HAUL IS DELIBERATELY STILL EXCLUDED. It is loot volume, not combat power, and folding it in would
    /// make a Gleam-finding charm read as a weapon upgrade. That is a real omission from "how good is
    /// this item" but the right answer is a second number, not a dishonest first one.
    /// </remarks>
    public static int PowerRating(float damageMultiplier, float skillRate, float healthMultiplier,
                                  int defense, int maxHealth, float critFactor = 1f)
        // 100 x SQRT of the DPS multiplier, was 120 x the multiplier itself. The damage-and-cadence half
        // of the rating is a DPS multiplier against a bare champion, which at the top of the item system
        // runs into the thousands; linear, that printed a six- or seven-digit ITEM POWER beside a
        // four-digit hit (playtest: "1.5m item power veren silah var"). The rating is only ever compared
        // and differenced (EQUIP BEST, the tooltips' +N POWER), so its SHAPE is free as long as it stays
        // monotonic — and a root keeps the order, keeps small gains visible at the bottom (a dozen crit
        // trainings still move it), and lands a bare hunter near 150 and the endgame in four digits.
        // Two halves on the SAME root scale — offence (the DPS multiplier) and toughness (effective
        // health: the pool, its multiplier and defence as a soak) — so neither term can quietly become
        // linear-dominant: before this the rooted offence sat beside a linear `defense x 2`, and a charm
        // outranked the Legendary weapon on the strength of its defence affixes alone.
        => (int)MathF.Round(100f * MathF.Sqrt(damageMultiplier * MathF.Max(0.05f, skillRate)
                                                               * MathF.Max(1f, critFactor))
                            + 100f * MathF.Sqrt(MathF.Max(0.05f, healthMultiplier) * MathF.Max(1, maxHealth) / 100f
                                                * (1f + MathF.Max(0, defense) / 100f)));

}
