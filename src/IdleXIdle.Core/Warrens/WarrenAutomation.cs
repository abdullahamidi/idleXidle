using System;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Warrens;

/// <summary>
/// What the Warren does FOR you, as opposed to what it produces. The one place a facility level turns
/// into automation.
/// </summary>
/// <remarks>
/// <para>
/// Auto-sell and auto-merge were trait-tree purchases, which put the account's automation layer inside
/// a build-identity screen and charged a choice-currency for a convenience. Automation belongs to the
/// Warren: it already owns the idle layer, it already has its own currency and upgrade ladder, and its
/// facility levels are already persisted and already only ever rise — so this move added no save field.
/// </para>
/// <para>
/// Every method here must have a real consumer, and a test asserts it. That is the same discipline the
/// prestige layer's effects class was written under, and for the same reason: this project's signature
/// bug is a number that is computed, stored, shown — and never read by anything that runs.
/// </para>
/// </remarks>
public static class WarrenAutomation
{
    /// <summary>
    /// Drops at or below this rarity are sold for you when a chest is opened. Null means keep everything.
    /// </summary>
    /// <remarks>
    /// Capped at Uncommon by design, and the cap is not a tuning value: an idle game's real currency is
    /// ATTENTION, and a bag of ninety Commons spends it on nothing — but a rule that could eat a Rare
    /// would quietly auto-sell the item game, which is the thing the player is here for.
    /// </remarks>
    public static Rarity? AutoSellAtOrBelow(Warren warren)
    {
        ArgumentNullException.ThrowIfNull(warren);

        var level = warren.Facility(FacilityKind.ScavengerRuns).Level;
        if (level >= warren.Tuning.AutoSellUncommonLevel) return Rarity.Uncommon;
        if (level >= warren.Tuning.AutoSellCommonLevel) return Rarity.Common;
        return null;
    }

    /// <summary>Does the vault stack your spare items into better ones when a chest is opened?</summary>
    public static bool AutoMergeOnChestOpen(Warren warren)
    {
        ArgumentNullException.ThrowIfNull(warren);
        return warren.Facility(FacilityKind.HoardVaults).Level >= warren.Tuning.AutoMergeLevel;
    }

    /// <summary>
    /// The automation a facility gains at a given level, or "" if that level only changes output.
    /// </summary>
    /// <remarks>
    /// Drawn on the facility card beside its next-level output, so the ladder states its own rewards.
    /// The old trait cards said auto-sell fired "the moment they drop" and auto-merge "after every
    /// expedition"; both actually fire when a chest is OPENED, and this copy says so.
    /// </remarks>
    public static string NoteFor(Warren warren, FacilityKind kind, int level)
    {
        ArgumentNullException.ThrowIfNull(warren);
        var t = warren.Tuning;

        if (kind == FacilityKind.ScavengerRuns && level == t.AutoSellCommonLevel)
            return "YOUR RUNNERS WILL SELL COMMON ITEMS FOR YOU WHEN A CHEST IS OPENED.";
        if (kind == FacilityKind.ScavengerRuns && level == t.AutoSellUncommonLevel)
            return "YOUR RUNNERS WILL SELL UNCOMMON ITEMS WHEN A CHEST IS OPENED TOO. "
                   + "RARE AND BETTER ARE ALWAYS KEPT.";
        if (kind == FacilityKind.HoardVaults && level == t.AutoMergeLevel)
            return "THE VAULT WILL STACK YOUR SPARE ITEMS INTO BETTER ONES WHEN A CHEST IS OPENED.";
        return "";
    }

    /// <summary>What this facility already does for you, or "" when it only produces.</summary>
    public static string ActiveNoteFor(Warren warren, FacilityKind kind)
    {
        ArgumentNullException.ThrowIfNull(warren);

        if (kind == FacilityKind.ScavengerRuns)
            return AutoSellAtOrBelow(warren) switch
            {
                Rarity.Uncommon => "SELLS YOUR COMMON AND UNCOMMON ITEMS WHEN A CHEST IS OPENED.",
                Rarity.Common => "SELLS YOUR COMMON ITEMS WHEN A CHEST IS OPENED.",
                _ => "",
            };
        if (kind == FacilityKind.HoardVaults && AutoMergeOnChestOpen(warren))
            return "STACKS YOUR SPARE ITEMS INTO BETTER ONES WHEN A CHEST IS OPENED.";
        return "";
    }
}
