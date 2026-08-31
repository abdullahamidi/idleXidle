using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Expeditions;

/// <summary>Knobs for what an expedition brings home. See design/gdd/expedition-auto-battle.md §7.2.</summary>
public sealed record ExpeditionLootTuning
{
    /// <summary>
    /// Converts haul QUALITY into <see cref="KillContext.LootTiltPercent"/>.
    /// </summary>
    /// <remarks>
    /// Quality is 1.0 with no Crafter, +0.25 per Crafter, +0.10 per SALVAGE. At ×80 a single Crafter
    /// (1.25) reaches 20% tilt and two (1.5) hit the 40% ceiling — so the role pays visibly from the
    /// first one, without one composition trivialising rarity.
    /// </remarks>
    public float QualityTiltScalar { get; init; } = 80f;

    /// <summary>The ceiling the loot system's tilt was designed around.</summary>
    public float MaxTiltPercent { get; init; } = 40f;

    /// <summary>
    /// The BUILD's rarity tilt gets its own ceiling, stacked on top of the source's.
    /// </summary>
    /// <remarks>
    /// It cannot share <see cref="MaxTiltPercent"/>. A Rare chest already spends 22 of those 40 points on
    /// its own grade, so a build sharing the budget bought at most 18 — measured, a TRIPLED rarity stat
    /// moved average item rarity by 6%, which is indistinguishable from nothing across a play session.
    /// The whole FORTUNE road would have been technically wired and practically still decoration.
    ///
    /// Separate ceilings also keep the two honest about what they are: grade is what DEPTH pays out, and
    /// the tilt is what the BUILD pays for. Neither can cannibalise the other's range.
    /// </remarks>
    public float MaxBuildTiltPercent { get; init; } = 40f;

    public static ExpeditionLootTuning Default { get; } = new();
}

/// <summary>
/// Turns a finished expedition into items. <b>The only source of loot in the game.</b>
/// </summary>
/// <remarks>
/// <para>
/// The pivot to auto-battle replaced manual combat but left <see cref="LootSystem"/> wired only to the
/// old encounter results screen, whose update method stopped being called. Nothing else ever rolled an
/// item. The consequences ran deep and silent: the Forge's opener was gated on
/// <c>_encounter.Phase == Complete</c>, which could never become true, so the Forge could not be opened
/// at all; <c>Gear.WeaponDamageMultiplier</c> — a direct factor of the squad's damage — was pinned at 1×
/// forever; and the help screen went on advertising "F — FORGE — EQUIP".
/// </para>
/// <para>
/// It also left the Crafter as a pure trap. The role's only payoff is haul QUALITY, and quality was
/// computed every wave and then dropped on the floor unread. This class is where it finally spends.
/// </para>
/// </remarks>
public static class ExpeditionLoot
{
    /// <summary>
    /// One boss's worth of loot: a SMALL, fixed count — depth buys RARITY, not a bigger pile.
    /// </summary>
    /// <remarks>
    /// Playtest: "every second an item rains down." The idle loop was rolling loot scaled by wave count,
    /// so a deep boss dropped dozens at once and the Forge drowned. The fix is the ARPG rule the whole
    /// loot rework is built on: going deeper should make each drop BETTER, never more frequent. A boss
    /// gives one item (a lucky second sometimes); the boss's DEPTH is folded into <paramref name="powerTier"/>
    /// so a wave-40 boss drops a genuinely rarer item than a wave-5 one — a reason to push, not a firehose.
    /// </remarks>
    /// <param name="buildTilt">
    /// The player build's rarity multiplier (1 = neutral), clamped on its OWN ceiling and added to the
    /// source's tilt rather than folded into <paramref name="quality"/>.
    /// </param>
    /// <param name="region">
    /// Where this boss fell. Decides which slots the drop leans toward and adds the region's own rarity
    /// tilt — the difference between a map that is a difficulty ladder and one that is a set of places.
    /// </param>
    /// <param name="favouredClass">
    /// The active champion's item class — four in five class-locked drops are theirs. Null rolls
    /// uniformly across the five classes.
    /// </param>
    public static IReadOnlyList<ItemInstance> RollBoss(
        int powerTier, float quality, Random rng, LootTuning? lootTuning = null,
        ExpeditionLootTuning? tuning = null, Sources.Source? element = null, float buildTilt = 1f,
        string? region = null, Economy.ItemClass? favouredClass = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        lootTuning ??= LootTuning.Default;
        tuning ??= ExpeditionLootTuning.Default;

        var profile = Encounters.RegionDrops.For(region);

        var ctx = new KillContext
        {
            PowerTier = Math.Max(1, powerTier),
            LootTiltPercent =
                Math.Clamp((quality - 1f) * tuning.QualityTiltScalar, 0f, tuning.MaxTiltPercent)
                + Math.Clamp((buildTilt - 1f) * tuning.QualityTiltScalar, 0f, tuning.MaxBuildTiltPercent)
                + profile.RarityTilt,
            Element = element,
            FavouredTypes = profile.Favoured,
            FavouredClass = favouredClass,
        };

        // ONE ROLL, THEN TAKE. This used to run the loop `count` times and add EVERY item each roll
        // produced — and LootSystem.Roll (pre-P11b) returned a core plus DropCount of
        // them. So "one, occasionally two" was one-or-two ROLLS of about two items each, and a boss
        // actually handed over 2.55. Measured, not guessed: LootRateTest counted 65 items across a
        // hundred waves, which at two seconds a wave is nineteen items a minute.
        //
        // The docstring above was right and the code under it was not. Rolling once and taking the
        // intended count makes the number the comment already promised.
        var count = 1 + (rng.NextDouble() < 0.25 ? 1 : 0);   // one, occasionally two — never a pile
        // The CreatureCore filter died in P11b with the mint itself — the roller returns only
        // wearables now, so the take is the whole story.
        return LootSystem.Roll(ctx, rng, lootTuning)
                         .Take(count)
                         .ToList();
    }
}
