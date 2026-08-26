using System;
using System.Linq;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>The socket rules' knobs. Data-driven per the coding standard.</summary>
public sealed record SocketTuning
{
    public static readonly SocketTuning Default = new();

    /// <summary>Is the player's very first gem set for nothing? See <see cref="GemCraft.SocketCost(Rarity, bool, SocketTuning?)"/>.</summary>
    public bool FirstGemFree { get; init; } = true;
}

/// <summary>
/// STAT GEMS: socketable stones with a random stat and their own level.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, item-system redesign: sockets by rarity (Rare 1, Epic 2, Legendary 3), gems that grant a
/// random basic stat, socketed at the Forge, and DESTROYED there to free the slot — the gem is the
/// price of changing your mind, the item is never at risk.
/// </para>
/// <para>
/// A gem is an <see cref="ItemInstance"/> (BaseType <see cref="ItemBaseType.Gem"/>), so saving,
/// bagging and icon plumbing come for free. Its STAT is derived from its id (stable, nothing new to
/// persist); its LEVEL is the item level. Magnitudes ride the same <see cref="ItemAffixes"/> scale the
/// rolled affixes use, so a gem and an affix are comparable numbers on one ruler.
/// </para>
/// </remarks>
public static class GemCraft
{
    public static bool IsGem(ItemInstance? item) => item?.BaseType == ItemBaseType.Gem;

    /// <summary>Socket count by host rarity: nothing below Rare, then 1 / 2 / 3.</summary>
    public static int SocketCount(Rarity r) => r switch
    {
        Rarity.Rare => 1,
        Rarity.Epic => 2,
        Rarity.Legendary => 3,
        _ => 0,
    };

    /// <summary>The gem's stat — id-derived, stable for the gem's whole life.</summary>
    public static AffixStat StatOf(ItemInstance gem)
    {
        ArgumentNullException.ThrowIfNull(gem);
        var stats = Enum.GetValues<AffixStat>();
        return stats[(int)(Fnv1a(gem.InstanceId + "|gem") % (uint)stats.Length)];
    }

    /// <summary>What the gem grants — the affix scale, tilted by the gem's own level.</summary>
    public static float Magnitude(ItemInstance gem)
    {
        ArgumentNullException.ThrowIfNull(gem);
        // Same 4x saturation the affix and family curves carry — depth buys gem levels indefinitely.
        return ItemAffixes.BaseMagnitude(StatOf(gem)) * MathF.Min(0.7f + 0.08f * (gem.ItemLevel - 1), 4f);
    }

    /// <summary>The gem's plain-words name, from its stat.</summary>
    public static string NameOf(ItemInstance gem) => StatOf(gem) switch
    {
        AffixStat.Damage => "FURY GEM",
        AffixStat.Health => "LIFE GEM",
        AffixStat.SkillRate => "TEMPO GEM",
        AffixStat.Haul => "FORTUNE GEM",
        AffixStat.Crit => "EDGE GEM",
        _ => "WARD GEM",
    };

    /// <summary>What the gem GIVES, in the stat's own unit — "+18% DAMAGE", "+3 DEFENCE".</summary>
    /// <remarks>
    /// The unit is <see cref="ItemAffixes.GrantLabel"/>'s job and the word is
    /// <see cref="ItemAffixes.StatWord"/>'s, so a gem line and an affix line can never disagree about
    /// what a magnitude means. Critical chance reads as a percentage of hits; defence reads flat.
    /// </remarks>
    public static string Grant(ItemInstance gem)
    {
        ArgumentNullException.ThrowIfNull(gem);
        var stat = StatOf(gem);
        return $"{ItemAffixes.GrantLabel(stat, Magnitude(gem))} {ItemAffixes.StatWord(stat)}";
    }

    /// <summary>
    /// The whole gem in one plain line — "FURY GEM LEVEL 4 · +18% DAMAGE".
    /// </summary>
    /// <remarks>
    /// Every gem surface prints this, so a gem says what it does in the reveal, in the bag, in the
    /// tooltip and in the socket boxes with the same words. The Forge shipped gems as coloured diamonds
    /// with a name and no number anywhere: the player could see they owned six stones and not one of
    /// them said what it would add.
    /// </remarks>
    public static string Describe(ItemInstance gem)
    {
        ArgumentNullException.ThrowIfNull(gem);
        return $"{NameOf(gem)} LEVEL {gem.ItemLevel} · {Grant(gem)}";
    }

    /// <summary>Essence cost to SET a gem, by host rarity. The Essence sink the trait re-roll used to be.</summary>
    public static int SocketCost(Rarity host) => host switch
    {
        Rarity.Rare => 60,
        Rarity.Epic => 120,
        _ => 240,
    };

    /// <summary>
    /// What THIS player pays to set a gem: nothing for their first gem ever, the full price after.
    /// </summary>
    /// <param name="freeSocketUsed">Has the player set a gem before? Persisted as <c>SaveGame.FreeSocketUsed</c>.</param>
    /// <param name="tuning">The rule's knob; the default gives the first gem free.</param>
    /// <remarks>
    /// Playtest 2026-08-26: "When a gem drops, take the player to the socket screen and show setting
    /// the gem. Make it free." A first gem arrives long before the wallet holds sixty Essence, so the
    /// lesson it is meant to teach — a gem goes INTO an item — was a lesson the price refused. The
    /// first one is free so the lesson can be performed the moment it is taught; every later socket
    /// is the Essence sink it always was.
    /// </remarks>
    public static int SocketCost(Rarity host, bool freeSocketUsed, SocketTuning? tuning = null)
        => IsFirstGemFree(freeSocketUsed, tuning) ? 0 : SocketCost(host);

    /// <summary>Is the player's next gem their free first one?</summary>
    public static bool IsFirstGemFree(bool freeSocketUsed, SocketTuning? tuning = null)
        => (tuning ?? SocketTuning.Default).FirstGemFree && !freeSocketUsed;

    /// <summary>Mint a fresh gem at a loot tier. Level follows depth; the frame grade follows the level.</summary>
    public static ItemInstance MintGem(int tier, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var level = Math.Max(1, 1 + tier / 4);
        return new ItemInstance
        {
            InstanceId = $"gem_{rng.Next(int.MaxValue):x8}",
            BaseType = ItemBaseType.Gem,
            Rarity = (Rarity)Math.Clamp(1 + (level - 1) / 3, 1, 4),   // frame grade tracks level
            SellValue = 20 + 10 * level,
            ItemLevel = level,
        };
    }

    /// <summary>Socket a gem into a host. Pure — the caller spends the Essence and swaps the bag.</summary>
    public static (ItemInstance? Product, string? Rejection) Socket(ItemInstance host, ItemInstance gem)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(gem);
        if (!IsGem(gem)) return (null, "ONLY A STAT GEM FITS A SOCKET.");
        if (Gear.SlotFor(host.BaseType) is null) return (null, "ONLY WEARABLE GEAR CARRIES SOCKETS.");
        var slots = SocketCount(host.Rarity);
        if (slots == 0) return (null, "RARE AND BETTER GEAR CARRIES SOCKETS.");
        if (host.Gems.Count >= slots) return (null, "EVERY SOCKET IS FULL — CRUSH A GEM TO FREE ONE.");

        // A NEW list on the product, never a shared one — see the field's own remark.
        return (host with { Gems = host.Gems.Append(gem).ToList() }, null);
    }

    /// <summary>Crush the gem at <paramref name="index"/> — the gem is destroyed, the slot opens.</summary>
    public static (ItemInstance Product, ItemInstance Crushed)? Crush(ItemInstance host, int index)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (index < 0 || index >= host.Gems.Count) return null;
        var crushed = host.Gems[index];
        var rest = host.Gems.Where((_, i) => i != index).ToList();
        return (host with { Gems = rest }, crushed);
    }

    private static uint Fnv1a(string s)
    {
        var hash = 2166136261u;
        foreach (var ch in s) { hash ^= ch; hash *= 16777619u; }
        return hash;
    }
}
