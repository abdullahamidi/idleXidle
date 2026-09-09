using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Warrens;

namespace IdleXIdle.Core.Persistence;

/// <summary>A serializable snapshot of everything the player has earned.</summary>
public sealed record SaveGame
{
    /// <summary>Bumped whenever the shape changes. A save from the future must be refused, not guessed at.</summary>
    public const int CurrentVersion = 4;

    public int Version { get; init; } = CurrentVersion;

    // Version history: 2 = the item-system redesign (BaseType "Gem", nested SavedItem.Gems, and
    // TraitOverride's "NONE" sentinel). An older build's strict Enum.Parse would crash on "Gem", so
    // the bump turns that crash into the designed FromNewerVersion refusal.
    // 3 = SkillId becomes a woven skill's persisted identity (2026-08-31). Source/Form are still
    // written as a legacy echo for now; an older build reading a v3 file must refuse it as
    // FromNewerVersion rather than mis-resolve the build, which is what this bump buys.
    // 4 = keystones and Vows leave the trait tree (2026-09-03). Keystone knowledge, Vow knowledge and
    // keystone-socket capacity get fields of their own, seeded ONCE on the first v4 load from the old
    // MemoryDustUnlocks list (see LegacyTraitTree), and the fifth skill slot is removed so a five-row
    // build is unwoven to four on the way in. THE BUMP IS WHAT MAKES THAT SAFE: SaveStore's
    // SnapshotBeforeUpgrade guard is `if (fileVersion >= CurrentVersion) return null;`, so without a
    // bump no pre-change copy is taken and the ten-second autosave overwrites the only original.

    /// <summary>UTC epoch milliseconds. The basis of offline progression.</summary>
    public long SavedAtMs { get; init; }

    public int Gleam { get; init; }

    /// <summary>Salvaged materials — this is the SCRAP tier now (the legacy single stock). Defaults to 0.</summary>
    public int Materials { get; init; }

    /// <summary>The three higher material tiers. Default 0, so pre-tier saves load clean (all their stock is Scrap).</summary>
    public int Essence { get; init; }
    public int Core { get; init; }
    public int Crystal { get; init; }

    /// <summary>
    /// Single-use Forge charters, by name. Absent on every save written before they existed, which
    /// loads as "none" — the right answer, and no migration.
    /// </summary>
    /// <remarks>
    /// Keyed by the enum's NAME rather than its ordinal, so inserting a charter into the middle of the
    /// enum later cannot silently turn every player's REFORGE stock into SALVAGE.
    /// </remarks>
    public Dictionary<string, int> Charters { get; init; } = new();

    public Dictionary<string, int> TrainingRanks { get; init; } = new();

    /// <summary>Legacy single-region mastery (pre-multi-region saves). WRITTEN NO MORE (P14) —
    /// read only to fold into the home region when RegionFarms is absent.</summary>
    public float RegionMasteryPoints { get; init; }

    // RETIRED FIELDS (2026-08-24): AutomationStage, UnhatchedCores, Roster and AssignedCreatureIds
    // belonged to the creature/evolution/automation subsystem, which was removed. Old saves still
    // carry them as JSON members; System.Text.Json skips unknown members by default, so they load
    // clean (see the save-compat test in SaveSystemTests).

    public List<SavedItem> Inventory { get; init; } = new();

    public int MemoryDust { get; init; }

    /// <summary>
    /// LEGACY ONLY. The node ids an old save bought on the retired Memory tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read exactly once per load, by <see cref="LegacyTraitTree"/>, and NEVER written back — a save
    /// captured from here on carries an empty list, and one more load drops it for good. Normal runtime
    /// code does not know these ids exist; the migration layer is the only thing in the game that does.
    /// </para>
    /// <para>
    /// The field itself stays because deserialising an old file must not lose the ids before the
    /// migration reads them. It is a doorway, not a store.
    /// </para>
    /// </remarks>
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

    /// <summary>The deepest tier ever reached (2026-08-23) — the award and the trait points key off it. Absent
    /// in older saves: then the tier is the peak.</summary>
    public int CorruptionPeak { get; init; }

    /// <summary>
    /// Which character is being played. Empty or unknown falls back to the starter.
    /// </summary>
    public string ActiveCharacterId { get; init; } = "";

    /// <summary>
    /// The champions the player has earned, by id. Absent on every save written before the tiered
    /// roster (2026-08-26), which loads as empty and is seeded from <see cref="Characters.LegacyUnlocks"/>.
    /// </summary>
    /// <remarks>
    /// This used to be derived from conquest every frame and never saved, on the argument that a
    /// derived set cannot fall out of step with the world. It cannot — but it CAN fall out of step
    /// with the rules, and the tiered roster tightened them. A champion earned under the old gate has
    /// to survive the new one, so the set is banked. A new save always writes at least the starter, so
    /// "empty" is unambiguous: it means "written before the field existed", never "nothing earned".
    /// The field is additive, so an older build reading this save simply ignores it — no version bump.
    /// </remarks>
    public List<string> UnlockedCharacters { get; init; } = new();

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

    /// <summary>Chests opened over the whole career. Shown on STATS and feeds a quest goal.</summary>
    /// <remarks>
    /// The remark above claimed "the chests you opened are counted", and they were — in a field on the
    /// Forge screen that nothing saved. STATS printed it beside HIGHEST WAVE and MASTERY POINTS, both
    /// persisted, so a career counter read zero after every reload while its neighbours read true.
    /// (ChestsCredited, which paid this counter's delta to the CRAFTER evolution path, was retired
    /// with the creature subsystem on 2026-08-24; old saves carrying it load clean.)
    /// </remarks>
    public int ChestsOpened { get; init; }

    /// <summary>
    /// Bosses felled across the career (P10) — the deterministic counter THE MAGPIE's gate reads.
    /// Absent on an older save; the host seeds it from <see cref="ChestsOpened"/> as an honest
    /// floor (every opened chest was a felled boss).
    /// </summary>
    public int BossesFelled { get; init; }

    /// <summary>The player's woven build — four skills. Empty on a pre-solo-model save (keeps the starter).</summary>
    public List<SavedSkill> WovenSkills { get; init; } = new();

    /// <summary>Which discovered keystones are socketed. Ids into the keystone catalog.</summary>
    /// <remarks>
    /// Doubles as the FLOOR on <see cref="KeystoneSocketsEarned"/> at load: whatever a returning player
    /// was wearing, they keep wearing, whatever the derived rule would have said.
    /// </remarks>
    public List<string> SocketedKeystoneIds { get; init; } = new();

    /// <summary>
    /// Keystones this account has discovered. Additive (2026-09-03) — absent means a save that predates
    /// the field, and the world plus <see cref="MemoryDustUnlocks"/> re-derive it on that load.
    /// </summary>
    /// <remarks>
    /// A stored LATCH, unioned each frame with what the world derives — never the only source. Keystone
    /// knowledge now comes from conquest and region mastery, both of which only ever grow, so the stored
    /// half exists purely to carry a legacy grant across and can never take anything away.
    /// </remarks>
    public List<string> DiscoveredKeystoneIds { get; init; } = new();

    /// <summary>
    /// Vows this account has found, by keeping a rule once without the Vow sworn. Additive (2026-09-03).
    /// </summary>
    /// <remarks>
    /// Vow knowledge had NO field before: it was derived from the trait tree's study nodes, and that
    /// producer is going away. A discovery is an event, so it has to be latched — the same shape as
    /// QuestsDone and UnlockedCharacters. Unknown ids are dropped on read.
    /// </remarks>
    public List<string> DiscoveredVowIds { get; init; } = new();

    /// <summary>
    /// The high-water mark of keystone socket capacity. Additive (2026-09-03); 0 means never computed.
    /// </summary>
    /// <remarks>
    /// Sockets are otherwise derived from world progression, and this file's law is "derived, never
    /// stored". The exception is deliberate and narrow: a save could hold the old SECOND SOCKET node on
    /// a single conquest, and pure derivation would take that socket away — which is a capability
    /// removed by a refactor, and the one thing this migration is not allowed to do. It is a capacity
    /// latch of the same species as HighestMasteryAwarded, monotone by construction (it is only ever
    /// written as a maximum), not an activity gate.
    /// </remarks>
    public int KeystoneSocketsEarned { get; init; }

    /// <summary>
    /// What each skill has earned by being used, and what the player spent it on.
    /// </summary>
    /// <remarks>
    /// Keyed by skill id rather than by slot, because a skill's levels belong to the SKILL and not to
    /// where it happens to be woven — unequipping one keeps everything it earned, which is the rule
    /// that lets a player experiment without losing progress. A save that predates this simply
    /// carries no rows and every skill starts at its base line.
    /// </remarks>
    public List<SavedSkillProgress> SkillProgress { get; init; } = new();

    /// <summary>Mastery-tree nodes the player has taken (ids into MasteryCatalog).</summary>
    public List<string> MasteryTaken { get; init; } = new();

    /// <summary>
    /// Skills learned FOR GOOD (D7, 2026-08-31): catalogue ids latched when a road node is taken.
    /// Respec never removes one. Absent on an older save — the tree seeds the set from
    /// <see cref="MasteryTaken"/>'s roads, and the next save writes the union.
    /// </summary>
    public List<string> LearnedSkills { get; init; } = new();

    /// <summary>
    /// The set ladders (by Source name) whose five-piece completion has already been celebrated — the
    /// "MACHINE SET COMPLETE · PLATING ACTIVE" moment fires once per set (UI polish §52), not every time
    /// the fifth piece goes back on after a swap. Additive (2026-09-01): absent on an older save means
    /// none celebrated yet, and the next completion is the first.
    /// </summary>
    public List<string> CompletedSets { get; init; } = new();

    /// <summary>Total mastery points earned over the whole game.</summary>
    public int MasteryEarned { get; init; }

    /// <summary>
    /// The mastery tree's camera: zoom in screen pixels per world unit, and the world point at the centre
    /// of the view. Zoom 0 means the tree has never been opened, and the screen answers with its
    /// first-open framing (the centre node and ring 1).
    /// </summary>
    /// <remarks>
    /// "On later visits the player's zoom should be saved" (2026-08-27). Default 0 on purpose: an older
    /// save loads clean with no version bump and gets the first-open framing exactly once, like a new
    /// game does.
    /// </remarks>
    public float MasteryZoom { get; init; }
    public float MasteryPanX { get; init; }
    public float MasteryPanY { get; init; }

    /// <summary>
    /// First-run guide rungs the player closed by hand, as <c>TutorialStep</c> NAMES.
    /// </summary>
    /// <remarks>
    /// Names rather than ordinals, so reordering the enum can never silently dismiss a different
    /// lesson. Default-empty means every older save loads clean — no version bump. A dismissed rung is
    /// a DISPLAY choice only: the facts the guide derives its ladder from are untouched, the closed
    /// rung just never shows again.
    /// </remarks>
    public List<string> DismissedGuideRungs { get; init; } = new();

    /// <summary>Has the click-through intro been finished or skipped? False on every older save.</summary>
    /// <remarks>
    /// False alone does not mean "show it": a save from before the intro existed is false too, and
    /// <c>Onboarding.IntroDue</c> reads a cleared wave as having seen it. Default-false, no version bump.
    /// </remarks>
    public bool IntroSeen { get; init; }

    /// <summary>
    /// Screens whose first-open explanation the player has closed, as <c>Activity</c> NAMES — plus
    /// <c>SkillSlotN</c> entries for the skill-slot notes shown on the BUILD screen.
    /// </summary>
    /// <remarks>
    /// Names, not ordinals, for the same reason as <see cref="DismissedGuideRungs"/>. Default-empty
    /// means an older save loads clean; <c>Onboarding.SeedExplained</c> then treats every screen that
    /// was already open as read, so a returning player is not re-taught the game they have been playing.
    /// </remarks>
    public List<string> ExplainedScreens { get; init; } = new();

    /// <summary>
    /// Screens the rail has REVEALED, as <c>Activity</c> names — the minimal explicit onboarding state.
    /// </summary>
    /// <remarks>
    /// Everything else about the journey is derived from facts the save already carries; this list is
    /// the one thing that has to be remembered, because a reveal must be MONOTONE and one gate is not
    /// (GEAR reads items owned, and a bag can be salvaged empty). Default-empty means an older save
    /// loads clean and <c>Reveal.Restore</c> seeds it from the gates, so a returning player keeps every
    /// screen they had. Written by the host; read once, on load.
    /// </remarks>
    public List<string> RevealedScreens { get; init; } = new();

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

    /// <summary>
    /// Has the player set a gem into a socket yet? The FIRST setting is free (see
    /// <c>GemCraft.SocketCost</c>); this remembers that the free one has been spent.
    /// </summary>
    /// <remarks>
    /// Default-false, so every older save loads clean with no version bump. An old save that has
    /// already socketed gems but predates this field is caught by <see cref="SaveSystem.RestoreFreeSocketUsed"/>,
    /// which reads the gems sitting in its items — the gift is for a first gem, not a fifth.
    /// </remarks>
    public bool FreeSocketUsed { get; init; }

    /// <summary>The VAULT's keep-filter: chests below this tier arrive as a little Scrap instead. 0 = keep all.</summary>
    public int ChestKeepMinTier { get; init; }

    /// <summary>The keep-filter's one-slot lean. WRITTEN NO MORE (P14) — read only when the newer
    /// <see cref="ChestKeepSlots"/> list is absent.</summary>
    public string? ChestKeepSlot { get; init; }

    /// <summary>The keep-filter's wanted slots (2026-08-23, several at once). When absent, the older single
    /// <see cref="ChestKeepSlot"/> is the one wanted slot.</summary>
    public List<string> ChestKeepSlots { get; init; } = new();

    /// <summary>
    /// Has the player allowed the Warren's runners to SELL low-grade gear as a chest is opened?
    /// </summary>
    /// <remarks>
    /// Default false, and DELIBERATELY false for a save already past the facility level that unlocks
    /// it (2026-09-09). The capability used to switch itself on with a Warren upgrade bought for its
    /// output, with no off switch and no notice at the moment of sale — so a returning player is
    /// asked rather than assumed to have agreed. No version bump: an absent field reads as off, which
    /// is the answer this change wants for every old save.
    /// </remarks>
    public bool AutoSellOn { get; init; }

    /// <summary>The ISO week (year*100+week) whose trader stall the two fields below describe.</summary>
    /// <remarks>Zero on old saves — the host treats a mismatch with the CURRENT week as "new week,
    /// stall resets", so the migration is the rollover itself.</remarks>
    public int TraderWeekStamp { get; init; }

    /// <summary>Stall slots (0..3) already bought this week. One of each, per week, per hunter.</summary>
    public List<int> TraderBoughtSlots { get; init; } = new();

    // ── The Warren facility economy ──────────────────────────────────────────────────────────────
    /// <summary>Warren level. Defaults to 1 so pre-Warren saves load a fresh level-1 base — no version bump.</summary>
    public int WarrenLevel { get; init; } = 1;
    public int WarrenXp { get; init; }

    /// <summary>Facility levels, by FacilityKind name. Empty on a pre-Warren save → every facility loads at 1.</summary>
    public Dictionary<string, int> WarrenFacilities { get; init; } = new();

    // WarrenMasteryPool (the closed-loop INSIGHT pool) was retired 2026-08-31 (P12). The key in old
    // saves is skipped on load like any other unknown member; nothing needs migrating — the pool
    // bought only Warren upgrades, which now cost Gleam + Dust.

    // ── TRAITS (2026-09-03). Four ADDITIVE fields, no version bump: an older save carries none of
    //    them, loads with an empty trait account, and starts discovering honestly — which is exactly
    //    what §96 asks for ("do not automatically unlock all new Traits because the old tree was
    //    progressed"). Six traits DO awaken on an established save's first load, because they read
    //    BossesFelled, RunsWithVowKept, CompletedSets, ConqueredRegions, RegionFarms and RunLog —
    //    all of which are above this line and all of which every established save already carries.
    //    That is the explicit mapping §96 allows, and the host announces those six as ONE plate
    //    rather than six ceremonies in a second.
    //
    //    ALL FOUR MUST ALSO BE IN Game1.Save()'s `with` BLOCK. A field added here and forgotten there
    //    serialises at its default for ever, and autosave fires every ten seconds. ────────────────

    /// <summary>
    /// Every trait the ACCOUNT has awakened. Account-wide, monotone, never removed (§25, LAW 6).
    /// </summary>
    public List<string> DiscoveredTraits { get; init; } = new();

    /// <summary>
    /// The three traits each champion is wearing — the one piece of per-character state this refactor
    /// adds (§26, LAW 7).
    /// </summary>
    public List<SavedTraitLoadout> TraitLoadouts { get; init; } = new();

    /// <summary>
    /// The hidden accumulators, keyed by <c>TraitCounter</c> name.
    /// </summary>
    /// <remarks>
    /// ONE dictionary with a closed key set, not sixteen fields — §29's "do not store full combat
    /// history when a bounded counter is sufficient". A key this build does not know is dropped on
    /// load rather than carried, so the key set cannot quietly grow an untended member.
    /// </remarks>
    public Dictionary<string, double> TraitTally { get; init; } = new();

    /// <summary>Where each trait first awakened — one line of flavour (§33), never a history database.</summary>
    public List<SavedTraitFirst> TraitProvenance { get; init; } = new();
}

/// <summary>What one champion is wearing. At most three ids; the ledger re-clamps on the way in.</summary>
public sealed record SavedTraitLoadout
{
    public required string CharacterId { get; init; }
    public List<string> TraitIds { get; init; } = new();
}

/// <summary>Who first awakened a trait, where, and how deep. One row per discovered trait.</summary>
public sealed record SavedTraitFirst
{
    public required string TraitId { get; init; }
    public string CharacterId { get; init; } = "";
    public string RegionId { get; init; } = "";
    public int Wave { get; init; }
}

/// <summary>An unopened chest in the save — grade, loot tier, and region element, by primitive.</summary>
public sealed record SavedChest
{
    public required int Rarity { get; init; }
    public required int Tier { get; init; }
    public string? Element { get; init; }

    /// <summary>Where it was won, so its drop profile survives a save. Null on a pre-profile save.</summary>
    public string? Region { get; init; }

    /// <summary>The rarity tilt earned by the descent that dropped it. 1 on a save written before it existed.</summary>
    /// <remarks>
    /// Defaulted to 1 rather than 0 for the same reason <c>Chest.RunTilt</c> is: it multiplies into the
    /// loot roll, so an old chest deserialised without the field has to come back NEUTRAL. A 0 default
    /// would quietly make every chest a player was holding at upgrade time worthless, which is the kind
    /// of migration bug nobody reports because it looks like bad luck.
    /// </remarks>
    public float RunTilt { get; init; } = 1f;

    /// <summary>A gift chest's catalogue key (<c>GiftChests</c>), or null for a chest that rolls. Null on every older save.</summary>
    public string? Gift { get; init; }
}

/// <summary>One woven skill in the saved build — the skill's id, its element and its Vow.</summary>
public sealed record SavedSkill
{
    /// <summary>
    /// The skill's REAL identity since save v3 — a <c>SkillCatalogue</c> id. Null on every older
    /// save, where <see cref="Form"/> + <see cref="Passive"/> carry the identity instead and
    /// <c>LegacySkillForm</c> migrates them forward on load.
    /// </summary>
    public string? SkillId { get; init; }

    /// <summary>The woven element's name — the fallback until a chosen variation owns the Source.</summary>
    public string? Source { get; init; }

    /// <summary>The Form-era identity. WRITTEN NO MORE (P3-final) — read only to migrate a pre-v3 row.</summary>
    public string? Form { get; init; }

    public string? VowId { get; init; }

    /// <summary>
    /// Whether the player put this skill in a PASSIVE slot — which decides which of its style's two
    /// skills it actually is.
    /// </summary>
    /// <remarks>
    /// Defaults to false, so a save written before the slot rework loads with every skill claiming an
    /// active slot and the composer's spill rule sorts out the overflow, exactly as it did before this
    /// field existed. Nothing is lost on the way in.
    /// </remarks>
    public bool? Passive { get; init; }
}

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

    // ── 2026-08-31 (P13): enums travel by NAME. The bare ints above remain for saves written before,
    //    read through a frozen legacy table (RunLog.LegacyAffixByIndex) because the Affix enum has
    //    since changed shape. ──
    public string? OutcomeName { get; init; }
    public string? WallArchetypeName { get; init; }
    public List<string> WallAffixNames { get; init; } = new();

    public int WallCreatures { get; init; }
    public float AbsorbedFraction { get; init; }
    public float AverageHitSize { get; init; }
    public float TargetsPerActivation { get; init; }
    public float CreaturesPerWave { get; init; }
    public float HealthLostPerWaveFraction { get; init; }

    /// <summary>What SHIELD ate, as a share of everything the wave landed. Zero on a save from before shield existed.</summary>
    public float ShieldAbsorbedFraction { get; init; }

    /// <summary>Shield absorbed per wave as a share of the pool. Zero on a save from before shield existed.</summary>
    public float ShieldAbsorbedPerWaveFraction { get; init; }
    public float SecondsPerWave { get; init; }
    public int SampledWaves { get; init; }
}

/// <summary>A single region's progress record: mastery earned there, and its depth record.</summary>
/// <remarks>
/// The Stage and AssignedIds members retired with the creature subsystem (2026-08-24); old saves
/// carrying them load clean because unknown JSON members are skipped. (SavedCreature, the roster's
/// DTO, retired with them.)
/// </remarks>
public sealed record RegionFarmSave
{
    public required string Id { get; init; }
    public float MasteryPoints { get; init; }

    /// <summary>Deepest wave ever held here — the source of skill points. See Region.BestDepth.</summary>
    public int BestDepth { get; init; }

    /// <summary>The checkpoint chosen here (Checkpoints). 0 = start from the top; absent in older saves.</summary>
    public int StartWave { get; init; }
}

public sealed record SavedItem
{
    public required string InstanceId { get; init; }
    public required string BaseType { get; init; }
    public required int Rarity { get; init; }
    public required int SellValue { get; init; }

    /// <summary>The item's level. Defaulted to 1 so pre-ilvl saves load clean, no version bump needed.</summary>
    public int ItemLevel { get; init; } = 1;

    /// <summary>Refines taken (0..15). Pre-ladder saves default to 0 — a fresh ladder, not a crash.</summary>
    public int Upgrades { get; init; }

    /// <summary>
    /// A reforged trait / enchantment, by enum NAME, or null for an un-reforged item.
    /// </summary>
    /// <remarks>
    /// Stored by name, not index, and ignored at load if it no longer parses — the same rule the whole
    /// save uses for Vow ids: a catalog that gets re-ordered must never silently
    /// re-roll every player's crafted gear. Null-default means every pre-Reforge save loads clean, so
    /// no version bump is needed (the trait/enchant simply fall back to the id-derived roll).
    /// </remarks>
    public string? TraitOverride { get; init; }
    public string? EnchantOverride { get; init; }

    /// <summary>Socketed stat gems, as nested items. Empty for gem-less gear and every pre-gem save.</summary>
    public List<SavedItem> Gems { get; init; } = new();

    /// <summary>
    /// The item's region ELEMENT (a <see cref="Sources.Source"/> name), or null for inert loot.
    /// </summary>
    /// <remarks>
    /// This was FORGOTTEN — the field was live state (set at mint, carried through merges, read by the
    /// element-hoarding merge rule) but was never captured, so every item's element silently reset to null
    /// on reload and a saved matched-element trio became unmergeable. SavedChest already persisted its
    /// element; items simply missed the same treatment. Null-default so pre-fix saves load clean.
    /// </remarks>
    public string? Element { get; init; }

    /// <summary>
    /// The item's CLASS (an <see cref="Economy.ItemClass"/> name), or null for a piece minted before
    /// classes existed or on a universal slot.
    /// </summary>
    /// <remarks>
    /// Null-default, stored by name and ignored if it no longer parses — the same three rules the
    /// trait, enchant and element fields follow, for the same reason: a pre-class save must load
    /// clean, with every item wearable by everyone, and never take a worn helm off a player.
    /// </remarks>
    public string? Class { get; init; }

    /// <summary>A weapon's family index, or null to derive it from the id as every older weapon does.</summary>
    public int? Family { get; init; }
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
        Hunter hunter,
        IReadOnlyList<ItemInstance> inventory, long nowMs,
        Prestige.MemoryDustWallet? prestige = null, int highestMasteryAwarded = 0,
        Encounters.World? world = null, string activeRegion = "",
        Warren? warren = null)
        => new()
        {
            SavedAtMs = nowMs,
            WarrenLevel = warren?.Level ?? 1,
            WarrenXp = warren?.Xp ?? 0,
            WarrenFacilities = warren is null
                ? new Dictionary<string, int>()
                : warren.FacilityLevels.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            Gleam = hunter.Gleam,
            Materials = hunter.Materials,
            Charters = hunter.AllCharters.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
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
            // NOT WRITTEN. The retired Memory tree's ownership list is migration input, never output:
            // LegacyTraitTree reads an old file's ids once at load, keeps the structural capabilities
            // they stood for, discards the obsolete balance modifiers, and this capture drops them. A
            // save written from here on has no tree ownership in it at all.
            MemoryDustUnlocks = new List<string>(),
            HighestMasteryAwarded = highestMasteryAwarded,
            ConqueredRegions = world?.ConqueredIds.ToList() ?? new List<string>(),
            ActiveRegion = activeRegion,
            CorruptionTier = world?.CorruptionTier ?? 0,
            CorruptionPeak = world?.PeakCorruptionTier ?? 0,
            RegionFarms = world is null ? new List<RegionFarmSave>() : Encounters.Regions.All.Select(def =>
            {
                var f = world.RegionFarm(def.Id);
                return new RegionFarmSave
                {
                    Id = def.Id,
                    MasteryPoints = f.RegionMasteryPoints,
                    BestDepth = f.BestDepth,
                    StartWave = f.StartWave,
                };
            }).ToList(),
            TrainingRanks = Enum.GetValues<HunterStat>()
                .ToDictionary(s => s.ToString(), hunter.RankOf),
            Inventory = inventory.Select(ToSavedItem).ToList(),
        };

    /// <summary>One item to its saved form — recursive, so socketed gems ride inside their host.</summary>
    // INTERNAL, not private: ShareCodes reuses the exact same DTO round-trip, so a shared item
    // passes through the same lenient parsing and legacy migrations as a loaded save.
    internal static SavedItem ToSavedItem(ItemInstance i) => new()
    {
        InstanceId = i.InstanceId,
        BaseType = i.BaseType.ToString(),
        Rarity = (int)i.Rarity,
        SellValue = i.SellValue,
        ItemLevel = i.ItemLevel,
        Upgrades = i.Upgrades,
        // "NONE" for a genuinely plain item, so the loader can tell "rolled plain" from "written by
        // a pre-redesign build that never had this field" — the latter gets the legacy derivation.
        TraitOverride = i.TraitOverride?.ToString() ?? "NONE",
        EnchantOverride = i.EnchantOverride?.ToString(),
        Element = i.Element?.ToString(),
        Class = i.Class?.ToString(),
        Family = i.Family,
        Gems = i.Gems.Select(ToSavedItem).ToList(),
    };

    /// <summary>
    /// Can this saved item be restored at all? An unknown <see cref="SavedItem.BaseType"/> — a
    /// retired or future member — fails here and the item is DROPPED on load, the same lenient rule
    /// every other catalogue name in the save follows.
    /// </summary>
    /// <remarks>
    /// This gate replaced a strict <c>Enum.Parse</c> inside <see cref="FromSavedItem"/> that the
    /// host's load path reached with no try/catch around it: one unknown base-type name in an old or
    /// edited save crashed the game BEFORE THE WINDOW OPENED — the load-path failure shape no
    /// capture can see (2026-08-31 audit, step 0 of the refactor: load must never throw).
    /// </remarks>
    internal static bool CanRestore(SavedItem s) => Enum.TryParse<ItemBaseType>(s.BaseType, out _);

    /// <summary>The mirror of <see cref="ToSavedItem"/> — same recursion, same lenient enum parsing.</summary>
    internal static ItemInstance FromSavedItem(SavedItem s)
    {
        // Callers filter with CanRestore; the fallback below only exists so a direct call cannot
        // throw. default(ItemBaseType) is the enum's first member — an arbitrary but valid answer
        // to a question the filter should already have refused.
        var baseType = Enum.TryParse<ItemBaseType>(s.BaseType, out var bt) ? bt : default;
        return new()
        {
            InstanceId = s.InstanceId,
            BaseType = baseType,
            // CLAMPED: rarity is persisted as an ordinal, and an out-of-range value from a
            // hand-edited or future save would otherwise ride into colour-table indexing — the
            // same hole the share-code decoder already closed for pasted codes.
            Rarity = (Rarity)Math.Clamp(s.Rarity, (int)Rarity.Common, (int)Rarity.Legendary),
            SellValue = s.SellValue,
            ItemLevel = s.ItemLevel,
            Upgrades = s.Upgrades,
            TraitOverride = s.TraitOverride is null
                ? Economy.GearTraits.LegacyDerivedTrait(s.InstanceId, baseType)
                : Enum.TryParse<GearTrait>(s.TraitOverride, out var t) ? t : null,   // "NONE" -> null
            EnchantOverride = Enum.TryParse<EnchantKind>(s.EnchantOverride, out var e) ? e : null,
            Element = Enum.TryParse<Sources.Source>(s.Element, out var el) ? el : null,
            // Lenient, like every enum here: an unparseable class is a null class, which is "anyone".
            Class = Enum.TryParse<Economy.ItemClass>(s.Class, out var cl) ? cl : null,
            Family = s.Family is { } fam && fam >= 0 && fam < Economy.ItemNaming.WeaponFamilies.Length ? fam : null,
            Gems = s.Gems.Where(CanRestore).Select(FromSavedItem).ToList(),
        };
    }

    public static List<ItemInstance> RestoreInventory(SaveGame save)
        // Dedup by InstanceId: a save polluted with duplicate ids (see the regression test) collapses to one
        // item per id on load, so a single worn item never reads as many, and a merge trio is never three
        // copies of the same thing. Nothing is lost — same id means an identical, id-derived item.
        => save.Inventory
            .GroupBy(s => s.InstanceId).Select(g => g.First())
            .Where(CanRestore)          // an unknown BaseType is dropped, never a boot crash
            .Select(FromSavedItem).ToList();

    public static void RestoreHunter(SaveGame save, Hunter hunter)
    {
        hunter.AddGleam(save.Gleam);
        hunter.AddMaterials(save.Materials);   // SCRAP
        hunter.AddMaterial(Material.Essence, save.Essence);
        hunter.AddMaterial(Material.Core, save.Core);
        hunter.AddMaterial(Material.Crystal, save.Crystal);

        // RESTORE, not add: this method runs once per load, but "replace" is the honest verb for a
        // stock read off disk and it makes a double-call harmless rather than a duplication bug.
        hunter.RestoreCharters(save.Charters
            .Where(kv => Enum.TryParse<Charter>(kv.Key, out _))
            .Select(kv => new KeyValuePair<Charter, int>(Enum.Parse<Charter>(kv.Key), kv.Value)));

        // MERGE CHARTS retired with the manual merge tray (AUTO-MERGE takes no paper). Held ones
        // become SALVAGE CHARTS — also a breaking-down permission — instead of dead paper. Moved
        // here from the host (P14) so the fold is testable, and so Charter.Merge can one day leave
        // the enum with its migration standing beside it.
        while (hunter.SpendCharter(Charter.Merge)) hunter.AddCharter(Charter.Salvage);

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
    /// <summary>
    /// Restore the roster: which champion is active, which quests are done, and which champions are
    /// earned — seeding the earned set from the pre-tier rules when the save predates the field.
    /// </summary>
    /// <remarks>
    /// The seeding decision lives here, in Core, so a test can hand it an old save and watch a champion
    /// survive. The host's Refresh still re-derives every unlock the current rules grant on top.
    /// </remarks>
    public static void RestoreCharacters(SaveGame save, Characters.CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(state);
        var banked = save.UnlockedCharacters.Count > 0
            ? save.UnlockedCharacters
            : Characters.LegacyUnlocks.Seed(
                save.ConqueredRegions,
                save.QuestsDone,
                save.RegionFarms.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.Max(f => f.BestDepth)),
                save.RunsWithVowKept);
        state.Restore(save.ActiveCharacterId, save.QuestsDone, banked);
    }

    /// <summary>
    /// Has this save spent its free first gem? True if it says so — or, for a save written before the
    /// field existed, if any item in it already carries a gem: a player who has socketed before is not
    /// on their first gem, whatever an absent flag reads.
    /// </summary>
    public static bool RestoreFreeSocketUsed(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save.FreeSocketUsed || save.Inventory.Any(i => i.Gems.Count > 0);
    }

    /// <summary>
    /// Restore the world — conquered regions, corruption, and each region's farm — from a save.
    /// </summary>
    /// <remarks>
    /// Moved out of the host (2026-08-31) because the host's farm loop indexed the region table
    /// directly, so a renamed or removed region id inside <see cref="SaveGame.RegionFarms"/> was a
    /// KeyNotFoundException BEFORE THE WINDOW OPENED — the second load-path boot crash this
    /// project has shipped, and one no screenshot can reach. Unknown ids are dropped, like every
    /// other catalogue name in the save; living in Core, the rule is pinned by a test. A save with
    /// no per-region farms is a pre-multi-region save: its single mastery number folds into the
    /// home region, exactly as the host used to do.
    /// </remarks>
    public static void RestoreWorld(SaveGame save, Encounters.World world)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(world);
        world.RestoreConquered(save.ConqueredRegions);
        world.RestoreCorruption(save.CorruptionTier, save.CorruptionPeak);
        if (save.RegionFarms.Count > 0)
        {
            foreach (var rf in save.RegionFarms)
            {
                if (Encounters.Regions.Find(rf.Id) is null) continue;   // renamed/removed region: dropped
                var farm = world.RegionFarm(rf.Id);
                farm.RestoreMasteryPoints(rf.MasteryPoints);
                farm.RestoreBestDepth(rf.BestDepth);
                farm.RestoreStartWave(rf.StartWave);
            }
        }
        else
        {
            world.RegionFarm(Encounters.VerdantHollow.RegionId).RestoreMasteryPoints(save.RegionMasteryPoints);
        }
    }

    /// <param name="legacy">
    /// What the old trait tree had already bought. AUTO-SELL and AUTO-MERGE are Warren facility levels
    /// now, so a save that paid for them on the tree is floored up to the level that grants them here.
    /// </param>
    /// <remarks>
    /// The floor is safe by construction: <c>Warren.Restore</c> calls <c>SetLevel</c>, which mints no
    /// XP (only <c>Upgrade</c> does), so nothing can level the Warren sideways; <c>IsUnlocked</c>
    /// grandfathers any facility past level 1 open under any ramp, so a migrated SCAVENGER RUNS is
    /// reachable even for a player with one conquest; and the depth cap only gates further UPGRADES, so
    /// a migrated level above it simply cannot be raised until depth catches up — never lowered.
    /// </remarks>
    public static void RestoreWarren(SaveGame save, Warren warren, LegacyTraitGrants? legacy = null)
    {
        ArgumentNullException.ThrowIfNull(warren);
        var levels = new Dictionary<FacilityKind, int>();
        foreach (var (name, lvl) in save.WarrenFacilities)
            if (Enum.TryParse<FacilityKind>(name, out var kind)) levels[kind] = lvl;

        if (legacy is { } g)
        {
            levels[FacilityKind.ScavengerRuns] =
                Math.Max(levels.GetValueOrDefault(FacilityKind.ScavengerRuns, 1), g.ScavengerRunsLevel);
            levels[FacilityKind.HoardVaults] =
                Math.Max(levels.GetValueOrDefault(FacilityKind.HoardVaults, 1), g.HoardVaultsLevel);
        }
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

/// <summary>One skill's earned levels and spent choices, flattened for the save file.</summary>
public sealed record SavedSkillProgress
{
    public required string SkillId { get; init; }

    /// <summary>Waves cleared with it equipped.</summary>
    public int Uses { get; init; }

    /// <summary>Which of its two variations was taken, or null while unchosen.</summary>
    public string? Variation { get; init; }

    /// <summary>Which of that variation's reinforcements are bought.</summary>
    public List<string> Reinforcements { get; init; } = new();
}
