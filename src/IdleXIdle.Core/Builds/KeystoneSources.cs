using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Progression;

namespace IdleXIdle.Core.Builds;

/// <summary>Which kind of world event teaches a keystone.</summary>
/// <remarks>
/// Four rungs, and the order is the order they are reached in: taking a region, knowing it, mastering
/// it, and — once — making the world worse. PERFECTED deliberately teaches nothing: it already pays
/// Memory Dust and the region's best loot tier, and a keystone behind a thousand waves in one place is
/// a keystone almost nobody would ever see.
/// </remarks>
public enum WorldRung
{
    /// <summary>Hold wave 20 in the region. The spine every player walks.</summary>
    Conquest,

    /// <summary>The region is PARTLY MASTERED — the first reason to farm a place you already own.</summary>
    PartlyMastered,

    /// <summary>The region is FULLY MASTERED — the deep rung.</summary>
    FullyMastered,

    /// <summary>The corruption has been deepened to <see cref="KeystoneSource.Tier"/>.</summary>
    Corruption,
}

/// <summary>Which world event teaches this keystone. <see cref="RegionId"/> is null for the corruption row.</summary>
public sealed record KeystoneSource(string KeystoneId, string? RegionId, WorldRung Rung, int Tier = 0);

public static partial class Keystones
{
    /// <summary>
    /// The nineteen keystones, and the world event that hands each one over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The world teaches the build.</b> Every keystone used to be a node on the trait tree, bought
    /// with the retired Memory tree's trait points — a full career earned 34 against a tree that cost 226, so a player
    /// could finish the game having never learned ECHO, which left the REVERB enchantment permanently
    /// dead for them with nothing on any screen able to fix it. Here they are free, they are not a
    /// choice, and all nineteen are reachable: what stays scarce is the SOCKET, which is the correct
    /// scarcity, because wearing one instead of another is the actual decision.
    /// </para>
    /// <para>
    /// The sentence a player can learn from this table: <i>every region teaches you three — one for
    /// taking it, one for knowing it, one for mastering it</i>. Six regions, eighteen keystones, and the
    /// nineteenth waits past the end of the world.
    /// </para>
    /// <para>
    /// <b>Two ordering rules hold this table together, and both are tested.</b>
    /// (A) A keystone that needs another keystone is never discovered first — DYNAMO alone fills a charge
    /// pool nothing reads and charges 15% more damage taken for it, so REND (region 4, conquest) is
    /// strictly earlier than DYNAMO (region 5, mastery). Regions unlock in a fixed chain, so "an earlier
    /// region's conquest" is a guarantee, while a region's own mastery rungs are not guaranteed to follow
    /// its conquest — a hundred waves accrue whether or not you ever hold wave 20.
    /// (B) A keystone the rest of the game depends on is a CONQUEST keystone: two of the five weapon
    /// enchantments need ECHO or BLOODLUST, so those cannot sit behind a hundred waves of farming one
    /// place. That is why ECHO and not IRONCLAD is the very first keystone in the game.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<KeystoneSource> Sources { get; } = new List<KeystoneSource>
    {
        // 1 — VERDANT HOLLOW (Nature). Where you learn to fight, to survive, and to farm.
        //     ECHO first because it VISIBLY changes the fight — two casts, not a bigger number.
        new("echo",           VerdantHollow.RegionId, WorldRung.Conquest),
        new("ironclad",       VerdantHollow.RegionId, WorldRung.PartlyMastered),
        new("greed",          VerdantHollow.RegionId, WorldRung.FullyMastered),

        // 2 — CINDERWORKS (Machine). Slow heavy blows: kill it before it swings, and pay in health.
        new("glass_cannon",   "cinderworks",   WorldRung.Conquest),
        new("blood_magic",    "cinderworks",   WorldRung.PartlyMastered),
        new("lodestone",      "cinderworks",   WorldRung.FullyMastered),

        // 3 — UMBRAL REACH (Shadow). The region bleeds you a cut at a time, so it teaches the doctrine
        //     of fighting from the edge.
        new("bloodlust",      "umbral_reach",  WorldRung.Conquest),
        new("venomancer",     "umbral_reach",  WorldRung.PartlyMastered),
        new("discerning_eye", "umbral_reach",  WorldRung.FullyMastered),

        // 4 — MARROW WASTES (Body). A slaughterhouse. REND is the body that gathers every blow and
        //     delivers one — and it is on the conquest row because DYNAMO and CAPACITOR need it.
        new("rend",           "marrow_wastes", WorldRung.Conquest),
        new("fortune",        "marrow_wastes", WorldRung.PartlyMastered),
        new("reaper",         "marrow_wastes", WorldRung.FullyMastered),

        // 5 — THE STILL ARCHIVE (Mind). A thousand precise cuts, and a pile that becomes the power.
        new("juggernaut",     "still_archive", WorldRung.Conquest),
        new("dynamo",         "still_archive", WorldRung.PartlyMastered),
        new("hoarder",        "still_archive", WorldRung.FullyMastered),

        // 6 — THE PALE CHOIR (Spirit). The end of the known world.
        new("undying",        "pale_choir",    WorldRung.Conquest),
        new("capacitor",      "pale_choir",    WorldRung.PartlyMastered),
        new("titan",          "pale_choir",    WorldRung.FullyMastered),

        // The nineteenth, for the player who chose to make the world worse.
        new("weaver",         null,            WorldRung.Corruption, Tier: 1),
    };

    /// <summary>Where a keystone comes from, or null if the id is not in the catalogue.</summary>
    public static KeystoneSource? SourceOf(string? keystoneId)
        => keystoneId is null ? null : Sources.FirstOrDefault(s => s.KeystoneId == keystoneId);

    /// <summary>Has this world event happened yet?</summary>
    /// <remarks>
    /// Every fact read here only ever grows — a conquered region is never un-conquered, region mastery
    /// points are only ever added to, and the corruption's PEAK tier never falls even when the player
    /// eases it back. That is what stops a discovery from being announced twice, which is the invariant
    /// <see cref="Unlocks"/> carries its own scar from.
    /// </remarks>
    public static bool IsReached(KeystoneSource source, World world)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(world);

        if (source.Rung == WorldRung.Corruption) return world.PeakCorruptionTier >= source.Tier;
        if (source.RegionId is not { } id || Regions.Find(id) is null) return false;

        return source.Rung switch
        {
            WorldRung.Conquest => world.IsConquered(id),
            WorldRung.PartlyMastered => world.RegionFarm(id).MasteryLevel >= MasteryLevel.PartiallyMastered,
            WorldRung.FullyMastered => world.RegionFarm(id).MasteryLevel >= MasteryLevel.FullyMastered,
            _ => false,
        };
    }

    /// <summary>
    /// Every keystone the world has taught this player, plus anything a legacy grant carries.
    /// </summary>
    /// <remarks>
    /// The menu, not the plate — see <see cref="Build.KeystoneSlots"/>. <paramref name="alsoKnown"/> is
    /// how a returning player keeps what they had already bought on the old trait tree: nobody
    /// re-conquers a region to get back a keystone they owned last week.
    /// </remarks>
    public static IReadOnlyList<Keystone> DiscoveredBy(World world, IEnumerable<string>? alsoKnown = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in Sources)
            if (IsReached(s, world)) ids.Add(s.KeystoneId);
        if (alsoKnown is not null)
            foreach (var id in alsoKnown) ids.Add(id);

        // Catalogue order, not discovery order, so the BUILD screen's list never reshuffles itself.
        return Catalog.Where(k => ids.Contains(k.Id)).ToList();
    }

    /// <summary>
    /// One line telling the player where a keystone they have not found yet is waiting.
    /// </summary>
    /// <remarks>
    /// Plain English, no abbreviations: the whole point of the move is that an item asking for BLOODLUST
    /// now points at a place on the map instead of at a menu the player cannot afford.
    /// </remarks>
    public static string WhereToFind(string? keystoneId)
    {
        if (SourceOf(keystoneId) is not { } s) return "";
        if (s.Rung == WorldRung.Corruption) return "DEEPEN THE CORRUPTION TO FIND IT.";
        if (s.RegionId is not { } id || Regions.Find(id) is not { } def) return "";

        return s.Rung switch
        {
            WorldRung.Conquest => $"CONQUER {def.Name} TO FIND IT.",
            WorldRung.PartlyMastered => $"PARTLY MASTER {def.Name} TO FIND IT.",
            WorldRung.FullyMastered => $"FULLY MASTER {def.Name} TO FIND IT.",
            _ => "",
        };
    }

    /// <summary>Which keystone grants this trigger, or null if none does.</summary>
    /// <remarks>
    /// Read by the Forge, so an unpaired combo enchantment can name the region that teaches its partner
    /// rather than naming a keystone the player has no route to.
    /// </remarks>
    public static Keystone? GrantingTrigger(BuildTrigger trigger)
        => Catalog.FirstOrDefault(k => k.Grants.Contains(trigger));

    /// <summary>
    /// What the player must DO for this rung, as an instruction — "CONQUER IT", "PARTLY MASTER IT".
    /// </summary>
    /// <remarks>
    /// The map's strip pairs it with the keystone that rung teaches. Written as an ask rather than as a
    /// state ("REND - CONQUERED") because the state form reads as a claim about the keystone itself,
    /// which is exactly the sentence a second-language reader should not have to unpick.
    /// </remarks>
    public static string RungAsk(WorldRung rung) => rung switch
    {
        WorldRung.Conquest => "CONQUER IT",
        WorldRung.PartlyMastered => "PARTLY MASTER IT",
        WorldRung.FullyMastered => "FULLY MASTER IT",
        WorldRung.Corruption => "DEEPEN THE CORRUPTION",
        _ => throw new ArgumentOutOfRangeException(nameof(rung), rung, null),
    };

    /// <summary>The rung's own name, for the reveal that announces one has been reached.</summary>
    public static string RungName(WorldRung rung) => rung switch
    {
        WorldRung.Conquest => "CONQUERED",
        WorldRung.PartlyMastered => "PARTLY MASTERED",
        WorldRung.FullyMastered => "FULLY MASTERED",
        WorldRung.Corruption => "CORRUPTION DEEPENED",
        _ => throw new ArgumentOutOfRangeException(nameof(rung), rung, null),
    };
}
