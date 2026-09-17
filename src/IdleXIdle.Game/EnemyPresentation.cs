using System;
using System.Collections.Generic;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Game;

/// <summary>
/// How one ordinary creature LOOKS: the region it haunts, the role it plays there, the art it wears and
/// the name it goes by. Presentation only — its health, bite, Source and role roll are Core's.
/// </summary>
/// <remarks>
/// The strip and still keys are built once, here, so the arena's per-frame lookups allocate nothing.
/// </remarks>
public sealed class EnemyLook
{
    /// <summary>Make a look; every derived key follows the arena art contract's naming.</summary>
    public EnemyLook(string regionId, Archetype archetype, string artKey, string name)
    {
        RegionId = regionId;
        Archetype = archetype;
        ArtKey = artKey;
        Name = name;
        IdleStrip = artKey + "_idle_strip8_512";
        AttackStrip = artKey + "_attack_strip8_512";
        DeathStrip = artKey + "_death_strip8_512";
        IdleStill = artKey + "_idle_01";
        AttackStill = artKey + "_attack_01";
    }

    /// <summary>The region id (<see cref="Regions"/>) this look belongs to.</summary>
    public string RegionId { get; }

    /// <summary>The combat role this look is drawn for.</summary>
    public Archetype Archetype { get; }

    /// <summary>The art family key: <c>&lt;region slug&gt;_&lt;archetype&gt;</c>, e.g. <c>verdant_swarm</c>.</summary>
    public string ArtKey { get; }

    /// <summary>What the creature is called on the page — short, plain, uppercase.</summary>
    public string Name { get; }

    /// <summary>The looping idle strip.</summary>
    public string IdleStrip { get; }

    /// <summary>The attack strip, played from the wind-up through the follow-through.</summary>
    public string AttackStrip { get; }

    /// <summary>The death strip, played once from the kill.</summary>
    public string DeathStrip { get; }

    /// <summary>The idle still (frame 0 of the idle), the arena's fallback when a strip is missing.</summary>
    public string IdleStill { get; }

    /// <summary>The attack still (frame 0 of the attack), the fallback while biting.</summary>
    public string AttackStill { get; }
}

/// <summary>How a region's boss looks: the art it wears and the name on its bar.</summary>
public sealed class BossLook
{
    /// <summary>Make a boss look; the strip keys follow the arena art contract's naming.</summary>
    public BossLook(string regionId, string artKey, string name)
    {
        RegionId = regionId;
        ArtKey = artKey;
        Name = name;
        IdleStrip = artKey + "_idle_strip8_512";
        AttackStrip = artKey + "_attack_strip8_512";
        DeathStrip = artKey + "_death_strip8_512";
    }

    /// <summary>The region id this boss guards.</summary>
    public string RegionId { get; }

    /// <summary>The boss's art family key, e.g. <c>thorn_regent</c>.</summary>
    public string ArtKey { get; }

    /// <summary>The name on the boss's bar.</summary>
    public string Name { get; }

    /// <summary>The looping idle strip.</summary>
    public string IdleStrip { get; }

    /// <summary>The attack strip.</summary>
    public string AttackStrip { get; }

    /// <summary>The death strip.</summary>
    public string DeathStrip { get; }
}

/// <summary>
/// THE ARENA'S CAST: which body a creature wears, chosen by REGION and ARCHETYPE — six regions by four
/// roles, twenty-four ordinary creatures — plus the six region bosses.
/// </summary>
/// <remarks>
/// <para>
/// Until 2026-09-16 the arena drew one body per SOURCE and told Swarm, Caster, Armoured and Bruiser
/// apart only by drawing that one picture bigger or smaller, so a region's four roles were four scales
/// of the same wisp. Now each region has a family, and each role in it reads from its silhouette: a
/// swarm is small, low and busy; a caster tall, thin or hovering; an armoured one closed and plated
/// with its weight low; a bruiser broad and dominant. <c>HuntScreen.ArchetypeScale</c> still sizes the
/// row, as supporting language, and is not retuned for any of this.
/// </para>
/// <para>
/// This is PRESENTATION and nothing else. A creature's health, bite, defence, Source and role roll are
/// Core's (<c>Archetypes.Compose</c>, <c>SoloExpedition</c>) and never read this table; the art is chosen
/// from the region and the role the run already rolled. The generation recipe for every row — prompts,
/// PixelLab ids, rejected candidates — is <c>tools/asset-pipeline/v2/spec.json</c> under
/// <c>enemy_matrix</c>, and <c>enemy_presentation_test</c> keeps the two in step.
/// </para>
/// <para>
/// A creature's SOURCE is not in its body. The arena says it where it matters: the enemy strip shows
/// the Sources actually present in the wave, and the inspector wears the hovered creature's own.
/// </para>
/// </remarks>
public static class EnemyPresentation
{
    private static readonly EnemyLook[] Rows =
    {
        // VERDANT HOLLOW — thorn, moss, root, rot, predatory forest life.
        new("verdant_hollow", Archetype.Swarm, "verdant_swarm", "BRIAR MITE"),
        new("verdant_hollow", Archetype.Caster, "verdant_caster", "BOG WEAVER"),
        new("verdant_hollow", Archetype.Armoured, "verdant_armoured", "BARK WARDEN"),
        new("verdant_hollow", Archetype.Bruiser, "verdant_bruiser", "THORN OGRE"),
        // CINDERWORKS — forge, slag, furnace, steam, industrial constructs.
        new("cinderworks", Archetype.Swarm, "cinder_swarm", "CINDER GNAT"),
        new("cinderworks", Archetype.Caster, "cinder_caster", "FURNACE PRIEST"),
        new("cinderworks", Archetype.Armoured, "cinder_armoured", "BOILER KNIGHT"),
        new("cinderworks", Archetype.Bruiser, "cinder_bruiser", "SLAG HULK"),
        // UMBRAL REACH — shadow, void, torn silhouettes, impossible dark predators.
        new("umbral_reach", Archetype.Swarm, "umbral_swarm", "GLOOM WHELP"),
        new("umbral_reach", Archetype.Caster, "umbral_caster", "HOLLOW SEER"),
        new("umbral_reach", Archetype.Armoured, "umbral_armoured", "NIGHT CARAPACE"),
        new("umbral_reach", Archetype.Bruiser, "umbral_bruiser", "DUSK APE"),
        // MARROW WASTES — bone, sinew, carrion, skeletal and visceral scavengers.
        new("marrow_wastes", Archetype.Swarm, "marrow_swarm", "MARROW TICK"),
        new("marrow_wastes", Archetype.Caster, "marrow_caster", "CARRION SHAMAN"),
        new("marrow_wastes", Archetype.Armoured, "marrow_armoured", "SHELL GHOUL"),
        new("marrow_wastes", Archetype.Bruiser, "marrow_bruiser", "FLAYED BRUTE"),
        // THE STILL ARCHIVE — crystal, runes, preserved thought, geometric arcane constructs.
        new("still_archive", Archetype.Swarm, "archive_swarm", "GLYPH MOTE"),
        new("still_archive", Archetype.Caster, "archive_caster", "LENS ARCHIVIST"),
        new("still_archive", Archetype.Armoured, "archive_armoured", "RUNE SENTRY"),
        new("still_archive", Archetype.Bruiser, "archive_bruiser", "TABLET GOLEM"),
        // THE PALE CHOIR — pale spirit, light, resonance, choral spectral beings.
        new("pale_choir", Archetype.Swarm, "choir_swarm", "CHOIR WISP"),
        new("pale_choir", Archetype.Caster, "choir_caster", "PALE CANTOR"),
        new("pale_choir", Archetype.Armoured, "choir_armoured", "HALO GUARD"),
        new("pale_choir", Archetype.Bruiser, "choir_bruiser", "BELL GIANT"),
    };

    private static readonly BossLook[] BossRows =
    {
        new("verdant_hollow", "thorn_regent", "THORN REGENT"),
        new("cinderworks", "forge_colossus", "FORGE COLOSSUS"),
        new("umbral_reach", "void_reaper", "VOID REAPER"),
        new("marrow_wastes", "spirit_matron", "SPIRIT MATRON"),
        new("still_archive", "crystal_lich", "CRYSTAL LICH"),
        new("pale_choir", "lumen_angel", "LUMEN ANGEL"),
    };

    private static readonly Dictionary<(string Region, Archetype Role), EnemyLook> ByCell = new();
    private static readonly Dictionary<string, BossLook> BossByRegion = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, BossLook> BossByKey = new(StringComparer.Ordinal);

    static EnemyPresentation()
    {
        foreach (var row in Rows) ByCell.Add((row.RegionId, row.Archetype), row);
        foreach (var boss in BossRows)
        {
            BossByRegion.Add(boss.RegionId, boss);
            BossByKey.Add(boss.ArtKey, boss);
        }
    }

    /// <summary>Every ordinary creature look, region by region, in role order.</summary>
    public static IReadOnlyList<EnemyLook> Normals => Rows;

    /// <summary>Every boss look, one per region.</summary>
    public static IReadOnlyList<BossLook> Bosses => BossRows;

    /// <summary>
    /// The region whose family stands in when a region id is unknown or not yet set: the first region
    /// the game opens on.
    /// </summary>
    public static string StartingRegion => Regions.All[0].Id;

    /// <summary>
    /// The look for a creature of <paramref name="archetype"/> in <paramref name="regionId"/>. An unknown
    /// or empty region draws the starting region's family rather than no creature at all.
    /// </summary>
    public static EnemyLook For(string? regionId, Archetype archetype)
        => regionId is not null && ByCell.TryGetValue((regionId, archetype), out var look)
            ? look
            : ByCell[(StartingRegion, archetype)];

    /// <summary>The boss that guards <paramref name="regionId"/>, or null for a region with none.</summary>
    public static BossLook? BossFor(string? regionId)
        => regionId is not null && BossByRegion.TryGetValue(regionId, out var boss) ? boss : null;

    /// <summary>A boss look by its art key (the capture rig pins one by key), or null.</summary>
    public static BossLook? BossByArtKey(string? artKey)
        => artKey is not null && BossByKey.TryGetValue(artKey, out var boss) ? boss : null;

    /// <summary>The region whose theme is <paramref name="source"/> — how the rig's Source pose picks a family.</summary>
    public static string? RegionOfTheme(Source source)
    {
        foreach (var region in Regions.All)
            if (region.Theme == source) return region.Id;
        return null;
    }
}
