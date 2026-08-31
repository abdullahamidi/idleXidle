using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Economy;

/// <summary>
/// The four salvage materials, one tier per rarity band. Each has ONE use, so none piles up unspent.
/// </summary>
/// <remarks>
/// The game had a single undifferentiated "materials" count with two sinks (reforge, feed). The item
/// overhaul asked for a real crafting economy, and the economy research the design leaned on is blunt
/// about the failure to avoid: a currency with a faucet and no matching drain inflates into meaningless
/// numbers. So salvage yields a tier by what you break — a Legendary yields Crystal, a Common yields
/// Scrap — and every tier drains through its own verb:
/// <list type="bullet">
///   <item><b>Scrap</b> → UPGRADE (refine): +1 item level (raises its affixes), with Gleam. The bulk,
///         infinite sink.</item>
///   <item><b>Essence</b> → SOCKET: set a stat gem into a Rare-or-better item
///         (<c>GemCraft.SocketCost</c>). The trait re-roll this used to fund is gone — the prefix is
///         immutable now — and the socket took over its drain.</item>
///   <item><b>Core</b> → RE-ROLL THE ENCHANT on a Rare or Epic item (<c>Reforge.EnchantMaterial</c>).
///         A REFORGE CHART pays instead when one is held.</item>
///   <item><b>Crystal</b> → RE-ROLL THE ENCHANT on a Legendary, and GREATER UPGRADE: +5 item levels at
///         once, never slips. The premium, infinite sink that stays hungry after content is maxed — the
///         anti-inflation drain the research names.</item>
/// </list>
/// </remarks>
public enum Material { Scrap, Essence, Core, Crystal }

/// <summary>Which material a rarity of loot salvages into.</summary>
public static class MaterialTiers
{
    public static Material ForRarity(Rarity rarity) => rarity switch
    {
        Rarity.Common or Rarity.Uncommon => Material.Scrap,
        Rarity.Rare => Material.Essence,
        Rarity.Epic => Material.Core,
        _ => Material.Crystal,
    };

    /// <summary>The short player-facing name, matching the enum.</summary>
    public static string Name(Material m) => m switch
    {
        Material.Scrap => "SCRAP",
        Material.Essence => "ESSENCE",
        Material.Core => "CORE",
        _ => "CRYSTAL",
    };
}
