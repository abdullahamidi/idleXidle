using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Expeditions;

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
    public static IReadOnlyList<ItemInstance> RollBoss(
        int powerTier, float quality, Random rng, LootTuning? lootTuning = null,
        ExpeditionLootTuning? tuning = null, Automation.Source? element = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        lootTuning ??= LootTuning.Default;
        tuning ??= ExpeditionLootTuning.Default;

        var ctx = new KillContext
        {
            PowerTier = Math.Max(1, powerTier),
            LootTiltPercent = Math.Clamp((quality - 1f) * tuning.QualityTiltScalar, 0f, tuning.MaxTiltPercent),
            Element = element,
        };

        var count = 1 + (rng.NextDouble() < 0.25 ? 1 : 0);   // one, occasionally two — never a pile
        var items = new List<ItemInstance>();
        for (var i = 0; i < count; i++)
            items.AddRange(LootSystem.Roll(ctx, rng, lootTuning).Where(it => it.BaseType != ItemBaseType.CreatureCore));

        return items;
    }
}
