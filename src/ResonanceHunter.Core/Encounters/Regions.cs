using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Combat;

namespace ResonanceHunter.Core.Encounters;

/// <summary>An authored region: its identity, its content, and what conquering it unlocks.</summary>
public sealed record RegionDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Source Theme { get; init; }

    /// <summary>The region that must be conquered before this one unlocks. Null = available from the start.</summary>
    public string? PrereqId { get; init; }

    /// <summary>Which attack tempo this region's creatures favour — how it feels to fight, not just look at.</summary>
    public AttackBias CombatBias { get; init; } = AttackBias.Balanced;

    public required IReadOnlyList<EncounterTemplate> Templates { get; init; }

    public EncounterTemplate Boss => Templates.First(t => t.IsBoss);
}

/// <summary>
/// The world: the ordered chain of regions to conquer.
/// </summary>
/// <remarks>
/// Regions are content, not code — each is the same encounter/automation machinery with a different
/// theme and difficulty band. The chain gives the game its progression spine: conquer a region's boss
/// to unlock the next. New regions beyond Verdant Hollow have no creature art yet, so they render as
/// the flat greybox fallback until sprites arrive — the systems work regardless.
/// </remarks>
public static class Regions
{
    public static IReadOnlyList<RegionDefinition> All { get; } = new List<RegionDefinition>
    {
        new()
        {
            // The starting region stays Balanced — the player learns the baseline before it's bent.
            Id = VerdantHollow.RegionId, Name = "VERDANT HOLLOW", Theme = Source.Nature,
            PrereqId = null, Templates = VerdantHollow.Templates, CombatBias = AttackBias.Balanced,
        },
        // Cinderworks grinds you down with slow, heavy industrial blows.
        BuildRegion("cinderworks", "CINDERWORKS", Source.Machine, prereq: VerdantHollow.RegionId,
            tierBase: 4, bossHealth: 3200, bossTier: 8, stdBaseHealth: new[] { 120, 95, 160 },
            bias: AttackBias.Heavy),
        // Umbral Reach harries you with fast, creeping strikes.
        BuildRegion("umbral_reach", "UMBRAL REACH", Source.Shadow, prereq: "cinderworks",
            tierBase: 9, bossHealth: 6000, bossTier: 14, stdBaseHealth: new[] { 180, 150, 240 },
            bias: AttackBias.Fast),
        // The back half of the world — one region per remaining Source, so all six get a home and the
        // Source matchup has somewhere to land every element. (An older comment here promised these were
        // "shorter to conquer"; they never were — the conquest bar is one constant for the whole world.
        // What actually separates them is RegionLadder, which prices each one at a fixed multiple of the
        // last, so the back half asks for gear rather than for more waves.) So
        // the journey is SPREAD across more, sharper places rather than three long grinds.
        // Marrow Wastes: BODY. Brutal, heavy blows — a slaughterhouse that grinds you down.
        BuildRegion("marrow_wastes", "MARROW WASTES", Source.Body, prereq: "umbral_reach",
            tierBase: 12, bossHealth: 10000, bossTier: 17, stdBaseHealth: new[] { 260, 220, 340 },
            bias: AttackBias.Heavy),
        // The Still Archive: MIND. Fast, precise psychic lances — death by a thousand cuts.
        BuildRegion("still_archive", "THE STILL ARCHIVE", Source.Mind, prereq: "marrow_wastes",
            tierBase: 15, bossHealth: 15000, bossTier: 19, stdBaseHealth: new[] { 360, 300, 460 },
            bias: AttackBias.Fast),
        // The Pale Choir: SPIRIT. The deepest reach — balanced, relentless, the end of the known world.
        BuildRegion("pale_choir", "THE PALE CHOIR", Source.Spirit, prereq: "still_archive",
            tierBase: 18, bossHealth: 24000, bossTier: 20, stdBaseHealth: new[] { 520, 440, 680 },
            bias: AttackBias.Balanced),
    };

    public static RegionDefinition Get(string id) => All.First(r => r.Id == id);

    /// <summary>Look up a region, or null if the id isn't in the catalog — the crash-safe form for load paths.</summary>
    public static RegionDefinition? Find(string id) => All.FirstOrDefault(r => r.Id == id);
    public static RegionDefinition? Next(string id)
    {
        var idx = All.ToList().FindIndex(r => r.Id == id);
        return idx >= 0 && idx + 1 < All.Count ? All[idx + 1] : null;
    }

    /// <summary>Every template across every region — the flat list an <see cref="EncounterSpawner"/> takes.</summary>
    public static IReadOnlyList<EncounterTemplate> AllTemplates => All.SelectMany(r => r.Templates).ToList();

    private static RegionDefinition BuildRegion(
        string id, string name, Source theme, string prereq, int tierBase, int bossHealth, int bossTier,
        int[] stdBaseHealth, AttackBias bias = AttackBias.Balanced)
    {
        var t = SpawnTuning.Default;
        var src = theme.ToString().ToLowerInvariant();

        EncounterTemplate Std(string variant, string role, int baseHealth, float weight)
        {
            var creature = new CreatureTemplate { Id = $"{src}_{role}_{variant}_std", BaseHealth = baseHealth };
            return new EncounterTemplate
            {
                TemplateId = $"enc_{id}_{variant}_std",
                RegionId = id,
                IsBoss = false,
                CreaturePool = new[] { creature },
                PowerTierBase = tierBase,
                SelectionWeight = weight,
                ParClearTimeSeconds = EncounterSpawner.DerivePar(baseHealth, tierBase, isBoss: false, t),
            };
        }

        var boss = new EncounterTemplate
        {
            TemplateId = $"enc_{id}_boss",
            RegionId = id,
            IsBoss = true,
            CreaturePool = new[] { new CreatureTemplate { Id = $"{src}_atk_{id}boss_boss", BaseHealth = bossHealth } },
            PowerTierBase = bossTier,
            SelectionWeight = 0,
            ParClearTimeSeconds = EncounterSpawner.DerivePar(bossHealth, bossTier, isBoss: true, t),
        };

        return new RegionDefinition
        {
            Id = id, Name = name, Theme = theme, PrereqId = prereq, CombatBias = bias,
            Templates = new[]
            {
                Std("warden", "atk", stdBaseHealth[0], 10),
                Std("drone", "sup", stdBaseHealth[1], 10),
                Std("bulwark", "def", stdBaseHealth[2], 8),
                boss,
            },
        };
    }
}

/// <summary>
/// The player's progress through the world: which regions are conquered, and a farm per region.
/// </summary>
/// <remarks>
/// A conquered region can be farmed independently, so each holds its own <see cref="Region"/>
/// (mastery, team, automation stage). The starting region is available immediately; every other
/// region unlocks when its prerequisite is conquered.
/// </remarks>
public sealed class World
{
    private readonly Dictionary<string, Region> _regions = new();
    private readonly HashSet<string> _conquered = new();

    public World(AutomationTuning? tuning = null)
    {
        foreach (var def in Regions.All)
            _regions[def.Id] = new Region(def.Id, FarmParSeconds(def), tuning);
    }

    /// <summary>
    /// The par time a region's farm is balanced against.
    /// </summary>
    /// <remarks>
    /// A farm auto-grinds the region's STANDARD encounters, never its boss, so its par is the average
    /// standard-encounter par — not the boss par. Keying the farm to the boss would make every
    /// conquered region's automated output crawl, defeating the point of conquering it.
    /// </remarks>
    private static int FarmParSeconds(RegionDefinition def)
    {
        var standard = def.Templates.Where(t => !t.IsBoss).Select(t => t.ParClearTimeSeconds).ToList();
        return standard.Count > 0 ? (int)Math.Round(standard.Average()) : def.Boss.ParClearTimeSeconds;
    }

    public Region RegionFarm(string id) => _regions[id];

    public bool IsConquered(string id) => _conquered.Contains(id);

    /// <summary>A region is unlocked if it is the start, is already conquered, or its prereq is conquered.</summary>
    public bool IsUnlocked(string id)
    {
        // An unknown/renamed id (e.g. a save from an older build after the region list changed) is simply
        // "not unlocked" rather than a crash — the load path then falls back to the home region.
        if (Regions.Find(id) is not { } def) return false;
        return def.PrereqId is null || _conquered.Contains(id) || _conquered.Contains(def.PrereqId);
    }

    /// <summary>Mark a region conquered (its boss was beaten). Returns the region it unlocks, if any.</summary>
    public RegionDefinition? Conquer(string id)
    {
        _conquered.Add(id);
        var next = Regions.Next(id);
        return next is not null && !_conquered.Contains(next.Id) ? next : null;
    }

    public IReadOnlyCollection<string> ConqueredIds => _conquered;
    public void RestoreConquered(IEnumerable<string> ids)
    {
        _conquered.Clear();
        foreach (var id in ids) if (_regions.ContainsKey(id)) _conquered.Add(id);
    }

    // ── Corruption: the endgame ratchet ─────────────────────────────────────────────────────────

    /// <summary>How deep into the corruption the world has been pushed. 0 = the base world.</summary>
    public int CorruptionTier { get; private set; }

    /// <summary>True once every region has been conquered — the precondition for deepening.</summary>
    public bool AllConquered => Regions.All.All(r => _conquered.Contains(r.Id));

    /// <summary>You may only deepen the corruption once the whole world is yours.</summary>
    public bool CanDeepenCorruption => AllConquered;

    /// <summary>
    /// Push the world one corruption tier deeper. Nothing resets — this is a pure difficulty/reward
    /// ratchet. Returns the new tier, or the unchanged tier if the world isn't fully conquered yet.
    /// </summary>
    public int DeepenCorruption()
    {
        if (CanDeepenCorruption) CorruptionTier++;
        return CorruptionTier;
    }

    /// <summary>Restore the corruption tier from a save (floored at 0).</summary>
    public void RestoreCorruption(int tier) => CorruptionTier = Math.Max(0, tier);
}
