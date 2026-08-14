using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;
using ResonanceHunter.Core.Warrens;

namespace ResonanceHunter.Core.Persistence;

/// <summary>A serializable snapshot of everything the player has earned.</summary>
public sealed record SaveGame
{
    /// <summary>Bumped whenever the shape changes. A save from the future must be refused, not guessed at.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>UTC epoch milliseconds. The basis of offline progression.</summary>
    public long SavedAtMs { get; init; }

    public int Gleam { get; init; }

    /// <summary>Salvaged materials — this is the SCRAP tier now (the legacy single stock). Defaults to 0.</summary>
    public int Materials { get; init; }

    /// <summary>The three higher material tiers. Default 0, so pre-tier saves load clean (all their stock is Scrap).</summary>
    public int Essence { get; init; }
    public int Core { get; init; }
    public int Crystal { get; init; }

    public Dictionary<string, int> TrainingRanks { get; init; } = new();

    public float RegionMasteryPoints { get; init; }
    public int AutomationStage { get; init; } = 1;

    public int UnhatchedCores { get; init; }
    public List<SavedCreature> Roster { get; init; } = new();
    public List<string> AssignedCreatureIds { get; init; } = new();
    public List<SavedItem> Inventory { get; init; } = new();

    public int MemoryDust { get; init; }
    public List<string> MemoryDustUnlocks { get; init; } = new();

    /// <summary>Worn gear, by item InstanceId. Losing your Legendary on reload is not an option.</summary>
    public string? WornWeaponId { get; init; }
    public string? WornCharmId { get; init; }
    public string? WornFocusId { get; init; }

    /// <summary>The five armour/jewellery slots added with the 8-slot loadout. Null in pre-8-slot saves.</summary>
    public string? WornHelmId { get; init; }
    public string? WornChestId { get; init; }
    public string? WornGlovesId { get; init; }
    public string? WornBootsId { get; init; }
    public string? WornRingId { get; init; }

    /// <summary>Highest mastery level ever reached — so Dust milestones are awarded once, not re-awarded.</summary>
    public int HighestMasteryAwarded { get; init; }

    // ── The world of regions (multi-region progression) ────────────────────────────────────────
    public List<string> ConqueredRegions { get; init; } = new();
    public string ActiveRegion { get; init; } = "";
    public List<RegionFarmSave> RegionFarms { get; init; } = new();

    /// <summary>
    /// The last ten run reports, newest first.
    /// </summary>
    /// <remarks>
    /// Saved, because the report is the only place this game can teach and the player it is teaching is
    /// by definition not watching — an idle game's lesson has to survive being closed.
    /// </remarks>
    public List<RunReportSave> RunLog { get; init; } = new();

    /// <summary>The endgame corruption ratchet — how deep the fully-conquered world has been pushed.</summary>
    public int CorruptionTier { get; init; }

    /// <summary>
    /// Which character is being played. Empty or unknown falls back to the starter.
    /// </summary>
    /// <remarks>
    /// The ONLY roster field worth saving. Which characters are unlocked is derived from conquest every
    /// frame — the same rule both skill trees follow — so there is nothing here to fall out of step with
    /// the world, and a region id that changes migrates itself.
    /// </remarks>
    public string ActiveCharacterId { get; init; } = "";

    /// <summary>Quest ids finished.</summary>
    public List<string> QuestsDone { get; init; } = new();

    /// <summary>
    /// Descents finished with a Vow's demand still met. The one quest counter that must be saved.
    /// </summary>
    /// <remarks>
    /// Every other quest reads a fact that is still true when you look at it — the depth you reached is
    /// on the region, the chests you opened are counted. This one is an EVENT, and an event that is not
    /// latched when it happens cannot be proved afterwards: a run's Vow is gone the moment the run ends.
    /// </remarks>
    public int RunsWithVowKept { get; init; }

    /// <summary>Chests opened over the whole career. Shown on STATS, feeds CRAFTER and a quest goal.</summary>
    /// <remarks>
    /// The remark above claimed "the chests you opened are counted", and they were — in a field on the
    /// Forge screen that nothing saved. STATS printed it beside HIGHEST WAVE and MASTERY POINTS, both
    /// persisted, so a career counter read zero after every reload while its neighbours read true.
    /// </remarks>
    public int ChestsOpened { get; init; }

    /// <summary>
    /// How much of <see cref="ChestsOpened"/> the CRAFTER evolution path has already been paid for.
    /// </summary>
    /// <remarks>
    /// Persisted TOGETHER with the counter above, and it has to be: the host credits CRAFTER on the
    /// delta between the two. Saving the total alone would hand a restored career's entire chest count
    /// to the evolution path again on the next frame after every single load.
    /// </remarks>
    public int ChestsCredited { get; init; }

    /// <summary>The player's woven build — four skills. Empty on a pre-solo-model save (keeps the starter).</summary>
    public List<SavedSkill> WovenSkills { get; init; } = new();

    /// <summary>Which learned keystones are socketed. Ids into the keystone catalog.</summary>
    public List<string> SocketedKeystoneIds { get; init; } = new();

    /// <summary>The character's Form affinity (the Nen-hexagon identity). Legacy — the Mastery tree owns it now.</summary>
    public string Affinity { get; init; } = "";

    /// <summary>Mastery-tree nodes the player has taken (ids into MasteryCatalog).</summary>
    public List<string> MasteryTaken { get; init; } = new();

    /// <summary>Total mastery points earned over the whole game.</summary>
    public int MasteryEarned { get; init; }

    /// <summary>The champion's recent GLEAM-per-second, so it keeps earning while the game is closed.</summary>
    public float ChampionGleamRate { get; init; }

    /// <summary>
    /// Unopened chests — a boss's drop the player hasn't cracked yet. Empty on a pre-chest save.
    /// </summary>
    /// <remarks>
    /// Persisted because they ARE loot: losing an unopened Legendary chest on reload is losing the drop.
    /// Default-empty means pre-chest saves load clean, so no version bump.
    /// </remarks>
    public List<SavedChest> UnopenedChests { get; init; } = new();

    // ── The Warren facility economy ──────────────────────────────────────────────────────────────
    /// <summary>Warren level. Defaults to 1 so pre-Warren saves load a fresh level-1 base — no version bump.</summary>
    public int WarrenLevel { get; init; } = 1;
    public int WarrenXp { get; init; }

    /// <summary>Facility levels, by FacilityKind name. Empty on a pre-Warren save → every facility loads at 1.</summary>
    public Dictionary<string, int> WarrenFacilities { get; init; } = new();

    /// <summary>
    /// Total Mastery Points the Warren has produced over the game's life.
    /// </summary>
    /// <remarks>
    /// Mastery Earned is DERIVED each frame (depth + conquests), not accumulated, so facility-produced
    /// mastery cannot live in the tree — it lives here and the host adds it into SetEarned. Persisted, or a
    /// reload would silently erase every mastery point the Warren ever made.
    /// </remarks>
    public long WarrenMasteryPool { get; init; }
}

/// <summary>An unopened chest in the save — grade, loot tier, and region element, by primitive.</summary>
public sealed record SavedChest
{
    public required int Rarity { get; init; }
    public required int Tier { get; init; }
    public string? Element { get; init; }
}

/// <summary>One woven skill in the saved build — Source x Form x Vow, all by name/id so it survives.</summary>
public sealed record SavedSkill
{
    public required string Source { get; init; }
    public required string Form { get; init; }
    public string? VowId { get; init; }
}

/// <summary>A single region's farm: its mastery, automation stage, and which creatures are assigned to it.</summary>
/// <summary>One post-run report, flattened for the save file.</summary>
/// <remarks>
/// A separate shape rather than serialising <c>RunReport</c> directly: the live record carries an
/// affix LIST and an enum, and a save format that mirrors a gameplay type breaks the moment the gameplay
/// type gains a field. Flattening it here means an old save loads into a newer report.
/// </remarks>
public sealed record RunReportSave
{
    public required string RegionId { get; init; }
    public int Depth { get; init; }
    public bool IsRecord { get; init; }
    public int Outcome { get; init; }
    public int WallWave { get; init; }
    public int WallArchetype { get; init; }
    public List<int> WallAffixes { get; init; } = new();
    public int WallCreatures { get; init; }
    public float AbsorbedFraction { get; init; }
    public float AverageHitSize { get; init; }
    public float TargetsPerActivation { get; init; }
    public float CreaturesPerWave { get; init; }
    public float HealthLostPerWaveFraction { get; init; }
    public float SecondsPerWave { get; init; }
    public int SampledWaves { get; init; }
}

public sealed record RegionFarmSave
{
    public required string Id { get; init; }
    public float MasteryPoints { get; init; }

    /// <summary>Deepest wave ever held here — the source of skill points. See Region.BestDepth.</summary>
    public int BestDepth { get; init; }

    public int Stage { get; init; } = 1;
    public List<string> AssignedIds { get; init; } = new();
}

public sealed record SavedCreature
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Role { get; init; }
    public required int PowerTier { get; init; }
    public bool IsHealthy { get; init; } = true;

    /// <summary>
    /// The Vow this creature swore, or null. Nullable and defaulted, so pre-Weaving saves load clean.
    /// </summary>
    /// <remarks>
    /// Stored as the Vow's id rather than its index or its stats: a catalog that gets re-ordered must
    /// not silently re-swear everyone's creatures, and an id that no longer exists resolves to null via
    /// Weaving.ById rather than throwing.
    /// </remarks>
    public string? VowId { get; init; }

    // Evolution state. Persisted in FULL — half-saving it would silently discard a player's accrued
    // part-break and job progress on reload, which reads as the game forgetting what they did.
    public string EvolutionNodeId { get; init; } = "";
    public int EvolutionMaterials { get; init; }
    public Dictionary<string, int> EvolutionWorkTicks { get; init; } = new();
    public Dictionary<string, int> EvolutionCombatTally { get; init; } = new();
    public string? EvolutionEquippedTrait { get; init; }
}

public sealed record SavedItem
{
    public required string InstanceId { get; init; }
    public required string BaseType { get; init; }
    public required int Rarity { get; init; }
    public required int SellValue { get; init; }

    /// <summary>The item's level. Defaulted to 1 so pre-ilvl saves load clean, no version bump needed.</summary>
    public int ItemLevel { get; init; } = 1;

    public string? EquippedToCreatureId { get; init; }

    /// <summary>
    /// A reforged trait / enchantment, by enum NAME, or null for an un-reforged item.
    /// </summary>
    /// <remarks>
    /// Stored by name, not index, and ignored at load if it no longer parses — the same rule the whole
    /// save uses for Vow ids and creature Roles: a catalog that gets re-ordered must never silently
    /// re-roll every player's crafted gear. Null-default means every pre-Reforge save loads clean, so
    /// no version bump is needed (the trait/enchant simply fall back to the id-derived roll).
    /// </remarks>
    public string? TraitOverride { get; init; }
    public string? EnchantOverride { get; init; }

    /// <summary>
    /// The item's region ELEMENT (a <see cref="Automation.Source"/> name), or null for inert loot.
    /// </summary>
    /// <remarks>
    /// This was FORGOTTEN — the field was live state (set at mint, carried through merges, read by the
    /// element-hoarding merge rule) but was never captured, so every item's element silently reset to null
    /// on reload and a saved matched-element trio became unmergeable. SavedChest already persisted its
    /// element; items simply missed the same treatment. Null-default so pre-fix saves load clean.
    /// </remarks>
    public string? Element { get; init; }
}

/// <summary>Why a load failed. A corrupt save must never silently become a fresh game.</summary>
public enum LoadFailure
{
    None,
    Missing,
    Corrupt,
    FromNewerVersion,
}

public sealed record LoadResult
{
    public SaveGame? Save { get; init; }
    public LoadFailure Failure { get; init; }

    /// <summary>Real seconds elapsed since the save was written. Drives offline catch-up.</summary>
    public double OfflineSeconds { get; init; }

    public bool Ok => Save is not null && Failure == LoadFailure.None;
}

/// <summary>
/// Save/load, and the offline progression it makes possible.
/// </summary>
/// <remarks>
/// This is not optional plumbing for an idle game — it IS the idle game. Without persistence, closing
/// the window deletes the farm you spent hours earning, and "offline progression" is meaningless.
///
/// Serialization is deliberately kept away from the file system: <see cref="Serialize"/> and
/// <see cref="Deserialize"/> are pure string functions, so they can be unit-tested without touching
/// disk (the project's test standards forbid file I/O in unit tests). The host owns the file.
/// </remarks>
public static class SaveSystem
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return JsonSerializer.Serialize(save, Options);
    }

    /// <summary>
    /// Parse a save. Never throws — a corrupt file must produce a diagnosable failure, not a crash.
    /// </summary>
    /// <param name="nowMs">Current UTC epoch ms. Injected rather than read, so tests stay deterministic.</param>
    public static LoadResult Deserialize(string? json, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LoadResult { Failure = LoadFailure.Missing };

        SaveGame? save;
        try
        {
            save = JsonSerializer.Deserialize<SaveGame>(json, Options);
        }
        catch (JsonException)
        {
            // A corrupt save must NOT be silently replaced by a fresh game — that would delete a
            // player's progress and look like the game "forgot" them. Surface it and let the host decide.
            return new LoadResult { Failure = LoadFailure.Corrupt };
        }

        if (save is null) return new LoadResult { Failure = LoadFailure.Corrupt };

        if (save.Version > SaveGame.CurrentVersion)
            return new LoadResult { Failure = LoadFailure.FromNewerVersion };

        // Clock rollback (timezone change, manual clock edit, DST) must never produce negative time.
        // Floor at zero: the player simply gets no offline progress, rather than losing any.
        var elapsedMs = Math.Max(0L, nowMs - save.SavedAtMs);

        return new LoadResult
        {
            Save = save,
            OfflineSeconds = elapsedMs / 1000.0,
        };
    }

    public static string Explain(LoadFailure failure) => failure switch
    {
        LoadFailure.Missing => "NO SAVE FOUND. STARTING FRESH.",
        LoadFailure.Corrupt => "SAVE FILE IS DAMAGED AND WAS NOT LOADED. YOUR OLD FILE HAS NOT BEEN OVERWRITTEN.",
        LoadFailure.FromNewerVersion => "THIS SAVE WAS MADE BY A NEWER VERSION OF THE GAME.",
        _ => "",
    };

    // ── Capture / restore ─────────────────────────────────────────────────────────────────────

    public static SaveGame Capture(
        Hunter hunter, Region region, IReadOnlyList<Creature> roster,
        IReadOnlyList<ItemInstance> inventory, int unhatchedCores, long nowMs,
        Prestige.MemoryDustTree? prestige = null, int highestMasteryAwarded = 0,
        Encounters.World? world = null, string activeRegion = "",
        Warren? warren = null, long warrenMasteryPool = 0)
        => new()
        {
            SavedAtMs = nowMs,
            WarrenLevel = warren?.Level ?? 1,
            WarrenXp = warren?.Xp ?? 0,
            WarrenFacilities = warren is null
                ? new Dictionary<string, int>()
                : warren.FacilityLevels.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            WarrenMasteryPool = warrenMasteryPool,
            Gleam = hunter.Gleam,
            Materials = hunter.Materials,
            Essence = hunter.MaterialOf(Material.Essence),
            Core = hunter.MaterialOf(Material.Core),
            Crystal = hunter.MaterialOf(Material.Crystal),
            WornWeaponId = hunter.Worn(GearSlot.Weapon)?.InstanceId,
            WornCharmId = hunter.Worn(GearSlot.Charm)?.InstanceId,
            WornFocusId = hunter.Worn(GearSlot.Focus)?.InstanceId,
            WornHelmId = hunter.Worn(GearSlot.Helm)?.InstanceId,
            WornChestId = hunter.Worn(GearSlot.Chest)?.InstanceId,
            WornGlovesId = hunter.Worn(GearSlot.Gloves)?.InstanceId,
            WornBootsId = hunter.Worn(GearSlot.Boots)?.InstanceId,
            WornRingId = hunter.Worn(GearSlot.Ring)?.InstanceId,
            MemoryDust = prestige?.MemoryDust ?? 0,
            MemoryDustUnlocks = prestige?.OwnedIds.ToList() ?? new List<string>(),
            HighestMasteryAwarded = highestMasteryAwarded,
            ConqueredRegions = world?.ConqueredIds.ToList() ?? new List<string>(),
            ActiveRegion = activeRegion,
            CorruptionTier = world?.CorruptionTier ?? 0,
            RegionFarms = world is null ? new List<RegionFarmSave>() : Encounters.Regions.All.Select(def =>
            {
                var f = world.RegionFarm(def.Id);
                return new RegionFarmSave
                {
                    Id = def.Id,
                    MasteryPoints = f.RegionMasteryPoints,
                    BestDepth = f.BestDepth,
                    Stage = f.AutomationStage,
                    AssignedIds = f.Team.Select(c => c.Id).ToList(),
                };
            }).ToList(),
            TrainingRanks = Enum.GetValues<HunterStat>()
                .ToDictionary(s => s.ToString(), hunter.RankOf),
            RegionMasteryPoints = region.RegionMasteryPoints,
            AutomationStage = region.AutomationStage,
            UnhatchedCores = unhatchedCores,
            Roster = roster.Select(c => new SavedCreature
            {
                Id = c.Id,
                Source = c.Source.ToString(),
                Role = c.Role.ToString(),
                PowerTier = c.PowerTier,
                IsHealthy = c.IsHealthy,
                VowId = c.VowId,
                EvolutionNodeId = c.EvolutionNodeId,
                EvolutionMaterials = c.Evolution?.Materials ?? 0,
                EvolutionWorkTicks = c.Evolution?.HealthyWorkTicks.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
                                     ?? new Dictionary<string, int>(),
                EvolutionCombatTally = c.Evolution is null
                    ? new Dictionary<string, int>()
                    : new Dictionary<string, int>(c.Evolution.CombatTally),
                EvolutionEquippedTrait = c.Evolution?.EquippedTrait,
            }).ToList(),
            AssignedCreatureIds = region.Team.Select(c => c.Id).ToList(),
            Inventory = inventory.Select(i => new SavedItem
            {
                InstanceId = i.InstanceId,
                BaseType = i.BaseType.ToString(),
                Rarity = (int)i.Rarity,
                SellValue = i.SellValue,
                ItemLevel = i.ItemLevel,
                EquippedToCreatureId = i.EquippedToCreatureId,
                TraitOverride = i.TraitOverride?.ToString(),
                EnchantOverride = i.EnchantOverride?.ToString(),
                Element = i.Element?.ToString(),
            }).ToList(),
        };

    public static List<Creature> RestoreRoster(SaveGame save)
        => save.Roster.Select(s =>
        {
            var creature = new Creature
            {
                Id = s.Id,
                Source = Enum.Parse<Source>(s.Source),
                RoleSeed = Enum.Parse<Role>(s.Role),
                PowerTierSeed = s.PowerTier,
                IsHealthy = s.IsHealthy,
                VowId = s.VowId,
            };

            if (s.EvolutionNodeId.Length > 0)
            {
                var progress = Evolution.EvolutionProgress.Restore(
                    s.EvolutionMaterials,
                    s.EvolutionWorkTicks.ToDictionary(kv => Enum.Parse<Role>(kv.Key), kv => kv.Value),
                    s.EvolutionCombatTally,
                    s.EvolutionEquippedTrait);

                creature.RestoreEvolution(s.EvolutionNodeId, progress);
            }

            return creature;
        }).ToList();

    public static List<ItemInstance> RestoreInventory(SaveGame save)
        // Dedup by InstanceId: a save polluted with duplicate ids (see the regression test) collapses to one
        // item per id on load, so a single worn item never reads as many, and a merge trio is never three
        // copies of the same thing. Nothing is lost — same id means an identical, id-derived item.
        => save.Inventory
            .GroupBy(s => s.InstanceId).Select(g => g.First())
            .Select(s => new ItemInstance
        {
            InstanceId = s.InstanceId,
            BaseType = Enum.Parse<ItemBaseType>(s.BaseType),
            Rarity = (Rarity)s.Rarity,
            SellValue = s.SellValue,
            ItemLevel = s.ItemLevel,
            EquippedToCreatureId = s.EquippedToCreatureId,
            // A reforged passive that no longer parses is dropped, not guessed — the item falls back to
            // its id-derived roll rather than throwing on an unknown enum name from an older/newer build.
            TraitOverride = Enum.TryParse<GearTrait>(s.TraitOverride, out var t) ? t : null,
            EnchantOverride = Enum.TryParse<EnchantKind>(s.EnchantOverride, out var e) ? e : null,
            Element = Enum.TryParse<Automation.Source>(s.Element, out var el) ? el : null,
        }).ToList();

    public static void RestoreHunter(SaveGame save, Hunter hunter)
    {
        hunter.AddGleam(save.Gleam);
        hunter.AddMaterials(save.Materials);   // SCRAP
        hunter.AddMaterial(Material.Essence, save.Essence);
        hunter.AddMaterial(Material.Core, save.Core);
        hunter.AddMaterial(Material.Crystal, save.Crystal);

        // Re-buy each rank, but credit the Gleam first — otherwise restoring a maxed Hunter would
        // fail on affordability and silently drop their progress.
        foreach (var (name, rank) in save.TrainingRanks)
        {
            if (!Enum.TryParse<HunterStat>(name, out var stat)) continue;

            for (var i = 0; i < rank; i++)
            {
                var cost = hunter.NextRankCost(stat);
                hunter.AddGleam(cost);
                // If Train refuses (the saved rank exceeds a since-lowered cap, or a tampered save), it spends
                // nothing — so undo the credit we just made and stop, or the restore would gift free Gleam for
                // every over-cap rank while capping the stat below its saved value. Within cap this never trips.
                if (!hunter.Train(stat)) { hunter.SpendGleam(cost); break; }
            }
        }
    }

    /// <summary>Restore the Warren's level, XP, and facility levels from a save (crash-safe on unknown keys).</summary>
    public static void RestoreWarren(SaveGame save, Warren warren)
    {
        ArgumentNullException.ThrowIfNull(warren);
        var levels = new Dictionary<FacilityKind, int>();
        foreach (var (name, lvl) in save.WarrenFacilities)
            if (Enum.TryParse<FacilityKind>(name, out var kind)) levels[kind] = lvl;
        warren.Restore(save.WarrenLevel, save.WarrenXp, levels);
    }

    /// <summary>
    /// Cap on how much offline time is credited.
    /// </summary>
    /// <remarks>
    /// A player returning after six months should not be handed six months of farm output — that
    /// would trivialize the game and make the cap-per-hour on auto-sell pointless. 24 hours is
    /// generous enough that a normal absence loses nothing, while an extreme one is bounded.
    /// The Gleam-per-hour cap still applies on top of this.
    /// </remarks>
    public const double MaxOfflineSeconds = 24 * 3600;

    public static double CreditedOfflineSeconds(double elapsed)
        => Math.Clamp(elapsed, 0, MaxOfflineSeconds);
}
