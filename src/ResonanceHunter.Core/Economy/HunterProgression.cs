using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Economy;

/// <summary>The canonical Hunter Stat Catalog. Nine stats, no more — combat-encounter-system A8.</summary>
public enum HunterStat
{
    AttackPower, Focus, Vitality, Engineering, Guile, ResonanceAffinity,
    MaxHealth, Defense, CriticalChance,
}

public sealed record ProgressionTuning
{
    public int BaseTrainingCost { get; init; } = 25;
    // 1.13. Second retune on direct playtest direction: "Stat maliyetini 1.13 falan yapabiliriz,
    // biraz zorlayıcı olsun. Idle oyun olduğu için uzun uzun oynanıp kasılması lazım." First rank
    // stays the tutorial's 25; rank 30 ≈ 855, rank 50 ≈ 9.9k, rank 59 ≈ 29k — ~294k per full stat,
    // ~2.65M for all nine (~250 hours at the slowest battle speed, ~47 at the fastest). The economy
    // test's ceiling was raised to 300h WITH this, quoting the same direction — the designer chose
    // the long grind, so the guard now guards against absurdity, not against length.
    public float TrainingGrowthRate { get; init; } = 1.13f;
    public int StatRankCap { get; init; } = 60;

    /// <summary>
    /// What RESET ALL TRAINING costs, in Crystal — the rarest forge material.
    /// </summary>
    /// <remarks>
    /// The refund side is deliberately 100%: the reset exists so a player can change their mind about
    /// where ~2.65M lifetime Gleam went, not to tax them for it. The price is therefore paid in the
    /// OTHER currency — one Crystal, which only Legendary salvage and deep waves mint — so a respec is
    /// a real decision without ever destroying training Gleam.
    /// </remarks>
    public int TrainingResetCrystalCost { get; init; } = 1;

    /// <summary>Per-rank stat gain. Defense is +2/rank to a cap of 120 — its curve is hyperbolic anyway.</summary>
    public IReadOnlyDictionary<HunterStat, float> GainPerRank { get; init; } =
        new Dictionary<HunterStat, float>
        {
            [HunterStat.AttackPower] = 2.0f,
            [HunterStat.Focus] = 2.0f,
            [HunterStat.Vitality] = 2.0f,
            [HunterStat.Engineering] = 2.0f,
            [HunterStat.Guile] = 2.0f,
            [HunterStat.ResonanceAffinity] = 2.0f,
            [HunterStat.MaxHealth] = 5.0f,
            [HunterStat.Defense] = 2.0f,
            [HunterStat.CriticalChance] = 0.25f,
        };

    public IReadOnlyDictionary<HunterStat, float> BaseValue { get; init; } =
        new Dictionary<HunterStat, float>
        {
            [HunterStat.AttackPower] = 10f,
            [HunterStat.Focus] = 10f,
            [HunterStat.Vitality] = 10f,
            [HunterStat.Engineering] = 10f,
            [HunterStat.Guile] = 10f,
            [HunterStat.ResonanceAffinity] = 10f,
            [HunterStat.MaxHealth] = 100f,
            [HunterStat.Defense] = 0f,     // NOTE: base ZERO. See vow_fragility's pricing.
            [HunterStat.CriticalChance] = 5f,
        };

    public static ProgressionTuning Default { get; } = new();
}

/// <summary>
/// The Hunter's power curve — and the only meaningful sink for Gleam.
/// </summary>
/// <remarks>
/// This system closes two defects at once. Until it existed, <b>nothing in the project levelled the
/// player</b>: combat-encounter-system defined a nine-stat catalog that every system read and no system
/// wrote. Meanwhile Gleam accumulated with almost nothing to spend it on. Making progression cost Gleam
/// turns the missing sink into the missing progression.
/// </remarks>
public sealed class Hunter
{
    private readonly ProgressionTuning _tuning;
    private readonly Dictionary<HunterStat, int> _ranks = new();

    public Hunter(ProgressionTuning? tuning = null)
    {
        _tuning = tuning ?? ProgressionTuning.Default;
        foreach (var stat in Enum.GetValues<HunterStat>()) _ranks[stat] = 0;
    }

    public int Gleam { get; private set; }

    /// <summary>
    /// Salvaged MATERIALS — what a dismantle actually pays, and what feeds a creature's evolution.
    /// </summary>
    /// <remarks>
    /// There was no material inventory at all, which is why "dismantled into materials" could not be
    /// built and why dismantle quietly paid Gleam instead — a strictly worse sell. This is the stock:
    /// lossy to fill (0.4x), but liquid, spendable on whichever creature you decide matters later.
    /// </remarks>
    private readonly Dictionary<Material, int> _mats =
        Enum.GetValues<Material>().ToDictionary(m => m, _ => 0);

    public int MaterialOf(Material m) => _mats[m];
    public void AddMaterial(Material m, int amount) => _mats[m] += Math.Max(0, amount);

    /// <summary>Spend one material tier. Returns false and takes nothing if you cannot afford it.</summary>
    public bool SpendMaterial(Material m, int amount)
    {
        if (amount <= 0 || _mats[m] < amount) return false;
        _mats[m] -= amount;
        return true;
    }

    /// <summary>
    /// SCRAP — the base material. The old single "materials" stock IS this now, so chests, Feed and every
    /// pre-tier caller keep working unchanged; only dismantle sorts by rarity into the higher tiers.
    /// </summary>
    public int Materials => _mats[Material.Scrap];
    public void AddMaterials(int amount) => AddMaterial(Material.Scrap, amount);
    public bool SpendMaterials(int amount) => SpendMaterial(Material.Scrap, amount);

    // ── CHARTERS — single-use papers that pay for one Forge operation ────────────────────────────
    private readonly Dictionary<Charter, int> _charters =
        Enum.GetValues<Charter>().ToDictionary(c => c, _ => 0);

    public int CharterCount(Charter c) => _charters[c];
    public void AddCharter(Charter c, int amount = 1) => _charters[c] += Math.Max(0, amount);

    /// <summary>
    /// Spend one charter. Returns false and takes nothing if you have none.
    /// </summary>
    /// <remarks>
    /// <b>Callers must validate the operation FIRST and spend this LAST.</b> A charter consumed before a
    /// rejection is a charter the player paid for a refusal — and because these drop rarely, that is far
    /// worse than the same mistake with a material. Every call site in the Forge is ordered
    /// validate-then-spend for exactly that reason.
    /// </remarks>
    public bool SpendCharter(Charter c)
    {
        if (_charters[c] <= 0) return false;
        _charters[c]--;
        return true;
    }

    /// <summary>Restore from a save. Replaces rather than adds, so a reload cannot double a stock.</summary>
    public void RestoreCharters(IEnumerable<KeyValuePair<Charter, int>> stock)
    {
        ArgumentNullException.ThrowIfNull(stock);
        foreach (var c in Enum.GetValues<Charter>()) _charters[c] = 0;
        foreach (var (c, n) in stock) _charters[c] = Math.Max(0, n);
    }

    /// <summary>Every charter and how many you hold — what the save writes.</summary>
    public IReadOnlyDictionary<Charter, int> AllCharters => _charters;

    /// <summary>Cosmetic only — never a gate. Gating on it would make it a second progression axis.</summary>
    public int HunterLevel => 1 + _ranks.Values.Sum() / 5;

    public int RankOf(HunterStat stat) => _ranks[stat];

    /// <summary>The rank ceiling every stat shares. Public so a screen can say MAXED without inferring it.</summary>
    /// <remarks>
    /// STATS was inferring it from `!CanTrain && Gleam >= cost`, which is only true when you can AFFORD
    /// the rank you cannot buy — so a capped stat read "NEED 45 GLEAM" to a poor player, offering to
    /// sell something that does not exist.
    /// </remarks>
    public int StatRankCap => _tuning.StatRankCap;

    public float ValueOf(HunterStat stat)
        => _tuning.BaseValue[stat] + _tuning.GainPerRank[stat] * _ranks[stat];

    /// <summary>What one rank of training adds to a stat. Public so a screen can say it without copying the tuning.</summary>
    public float GainPerRank(HunterStat stat) => _tuning.GainPerRank[stat];

    // ── Worn gear — the Hunter's power axis. See Gear.cs for why this exists. Eight slots now. ────
    private readonly Dictionary<GearSlot, ItemInstance?> _worn =
        Enum.GetValues<GearSlot>().ToDictionary(s => s, _ => (ItemInstance?)null);

    public ItemInstance? Worn(GearSlot slot) => _worn[slot];

    /// <summary>Wear an item. Returns whatever it displaced (back to your bag), or null.</summary>
    public ItemInstance? Equip(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Gear.SlotFor(item.BaseType) is not { } slot) return null; // materials/cores aren't wearable

        var displaced = _worn[slot];
        _worn[slot] = item;
        return displaced;
    }

    public ItemInstance? Unequip(GearSlot slot)
    {
        var was = _worn[slot];
        _worn[slot] = null;
        return was;
    }

    /// <summary>Restore worn gear from a save.</summary>
    public void RestoreWorn(GearSlot slot, ItemInstance? item) => _worn[slot] = item;

    /// <summary>Your weapon's damage multiplier — the headline of the power curve.</summary>
    public float GearDamageMultiplier => Gear.WeaponDamageMultiplier(_worn[GearSlot.Weapon]);

    public int Defense => (int)ValueOf(HunterStat.Defense) + Gear.CharmDefenseBonus(_worn[GearSlot.Charm])
                          + (int)MathF.Round(AffixTotal(AffixStat.Defense));
    public int MaxHealth => (int)ValueOf(HunterStat.MaxHealth) + Gear.CharmHealthBonus(_worn[GearSlot.Charm]);

    /// <summary>One glanceable number for "am I getting stronger". Shown in the HUD.</summary>
    public int PowerRating => Gear.PowerRating(SquadDamageMultiplier, SquadSkillRate, SquadHealthMultiplier,
                                              Defense, MaxHealth, CritFactor);

    /// <summary>
    /// The expected-damage multiplier crit is worth: <c>1 + chance * (multiplier - 1)</c>.
    /// </summary>
    /// <remarks>
    /// Computed here rather than in <c>SoloBattle</c> because Economy must not depend on Builds. It
    /// deliberately reads only the HUNTER's terms — trained Focus, trained crit chance, and gear affixes
    /// — and not a skill shape's bonus, because this is the number a GEAR page shows and a piece of gear
    /// does not change which skills you have woven. The cap mirrors the fight's.
    /// </remarks>
    public float CritFactor
    {
        get
        {
            var chance = Math.Clamp(
                (ValueOf(HunterStat.CriticalChance) + AffixTotal(AffixStat.Crit)) / 100f,
                0f, MaxCritChanceMirror);
            var multiplier = CritBaseMultiplierMirror
                             + ValueOf(HunterStat.Focus) * FocusCritDamagePerPointMirror;
            return 1f + chance * MathF.Max(0f, multiplier - 1f);
        }
    }

    // The fight's crit constants, mirrored because Economy must not depend on Builds. Pinned against
    // SoloBattle by test_the_crit_constants_match_the_fight — a silent divergence here would make the
    // gear page state a number the fight does not use, which is the drift this codebase keeps finding.
    public const float CritBaseMultiplierMirror = 1.5f;
    public const float MaxCritChanceMirror = 0.75f;
    public const float FocusCritDamagePerPointMirror = 0.01f;

    /// <summary>
    /// How much this item would add to <see cref="PowerRating"/> if worn in its slot — its marginal power.
    /// </summary>
    /// <remarks>
    /// This is the number the gear screen must compare with, NOT <see cref="Gear.ItemScore"/>. ItemScore is a
    /// rarity + affix heuristic that can rank an item BELOW what you wear while equipping it actually RAISES
    /// PowerRating (it weights crit/haul affixes that PowerRating never reads, and under-weights the weapon
    /// multiplier) — which reads to the player as "the item shows less power but my power went up". Defined as
    /// PowerRating with the item in its slot minus PowerRating with that slot empty, so a swap's shown delta
    /// (contribution(new) − contribution(old)) is EXACTLY the change to PowerRating; the two can never disagree.
    /// Pure: it momentarily swaps the worn slot to measure, then restores it (the game is single-threaded).
    /// </remarks>
    public int PowerContribution(ItemInstance? item)
    {
        if (item is null || Gear.SlotFor(item.BaseType) is not { } slot) return 0;
        var prev = _worn[slot];
        _worn[slot] = item;
        var with = PowerRating;
        _worn[slot] = null;
        var without = PowerRating;
        _worn[slot] = prev;
        return with - without;
    }

    // ── Commander stats — what training BUYS now that the Hunter no longer swings ─────────────────
    // The pivot to auto-battle orphaned every one of these: the squad fights, so the Hunter's personal
    // AttackPower/Defense/Health fed nothing, and Gleam had no sink at all. Rather than delete
    // training, it is re-pointed at the squad. The Hunter is now a commander.

    /// <summary>
    /// Everything the three worn items do to the squad, composed.
    /// </summary>
    /// <remarks>
    /// <b>Two of the three gear slots were dead.</b> The pivot pointed the squad's stats at the
    /// commander's <c>SquadDamageMultiplier</c>/<c>SquadHealthMultiplier</c>/<c>HaulMultiplier</c>, but
    /// Charm still fed <see cref="Defense"/>/<see cref="MaxHealth"/> and Focus still fed Resonance —
    /// the HUNTER'S OWN combat stats, which nothing has read since manual combat was deleted. So
    /// equipping a charm did literally nothing, which is exactly what the playtest reported: <i>"There
    /// is no charming feature in the game."</i> The Forge dutifully printed "+34 DEF" for a number that
    /// reached no fight. Same shape as the loot and conquest bugs — the pivot orphaned a system and the
    /// UI kept advertising it.
    /// </remarks>
    public GearMods WornMods
    {
        get
        {
            // Additive across slots with a ceiling (GearMods.Stack) — the fold of Combine that used to
            // stand here compounded eight slots into the "1.5m item power" weapon.
            var aff = AffixTotals();
            // Explicit affixes (and the family built-ins and gems that ride their channel) are additive
            // percentages; they join the trait stack as one more entry so EVERY bonus source in a channel
            // shares the one ceiling — otherwise the affix sum multiplied the capped trait stack and the
            // skill clock ran away again through the side door.
            var affixMods = new GearMods(
                1f + aff.GetValueOrDefault(AffixStat.Damage), 1f + aff.GetValueOrDefault(AffixStat.Health),
                1f + aff.GetValueOrDefault(AffixStat.Haul), 1f + aff.GetValueOrDefault(AffixStat.SkillRate));
            return GearMods.Stack(_worn.Values.Select(GearTraits.ModsOf).Append(affixMods));
        }
    }

    /// <summary>Every explicit affix on worn gear, summed per stat. The item system's contribution channel.</summary>
    private Dictionary<AffixStat, float> AffixTotals()
    {
        var totals = new Dictionary<AffixStat, float>();
        foreach (var item in _worn.Values)
        {
            foreach (var a in ItemAffixes.Of(item))
                totals[a.Stat] = totals.GetValueOrDefault(a.Stat) + a.Magnitude;

            // The weapon FAMILY's built-in identity — a blade hits harder, a bow crits. Summed here so
            // every downstream number (power, defense, crit, squad multipliers) sees it for free.
            if (ItemFamilies.BonusOf(item) is { } fam)
                totals[fam.Stat] = totals.GetValueOrDefault(fam.Stat) + fam.Magnitude;

            // Socketed STAT GEMS — the player-chosen layer on the same ruler.
            if (item is not null)
                foreach (var g in item.Gems)
                    totals[GemCraft.StatOf(g)] = totals.GetValueOrDefault(GemCraft.StatOf(g)) + GemCraft.Magnitude(g);
        }
        return totals;
    }

    /// <summary>Sum of one affix stat across all worn gear. Read by <c>Defense</c> and the fight (crit).</summary>
    public float AffixTotal(AffixStat stat) => AffixTotals().GetValueOrDefault(stat);

    /// <summary>
    /// The enchantments on the worn gear — the half of an item a multiplier cannot say.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="WornMods"/> on purpose. Mods are numbers the sim multiplies by;
    /// enchantments are triggers the sim ASKS about at specific moments ("did anything want to happen
    /// on this kill?"). Folding them together is what made GearMods a four-float struct that could only
    /// ever produce numbers, which is precisely why the design's enchantments were unbuildable.
    /// </remarks>
    public IReadOnlyList<Enchantment> WornEnchantments =>
        Enchantments.Worn(_worn.Values.ToArray());   // all eight slots now carry a live trigger, not just three

    /// <summary>What the squad hits for: worn gear × command training.</summary>
    public float SquadDamageMultiplier => GearDamageMultiplier * (1f + 0.010f * AttackPower) * WornMods.Damage;


    /// <summary>Squad toughness: Vitality, the worn charm, and whatever the charm's trait costs you.</summary>
    public float SquadHealthMultiplier =>
        (1f + 0.012f * ValueOf(HunterStat.Vitality)) * CharmToughness * WornMods.Health;

    /// <summary>
    /// The charm's own contribution to squad toughness, from the rarity curve.
    /// </summary>
    /// <remarks>
    /// Reuses <see cref="Gear.CharmHealthBonus"/>'s curve as a multiplier rather than the flat HP it was
    /// designed to add — the squad's fighters have their own health pools, so a flat "+175 HP" has
    /// nowhere to land.
    /// </remarks>
    // Note the explicit null check: `?? 0` would mean Rarity.Common, whose RarityPower is 0.20 — an
    // EMPTY slot would quietly pay out like a Common item.
    private float CharmToughness =>
        _worn[GearSlot.Charm] is { } c ? 1f + 0.06f * Gear.RarityPower(c.Rarity) * Gear.ItemLevelFactor(c.ItemLevel) : 1f;

    /// <summary>
    /// How fast skills come back: the Focus slot's job, and the reason the slot exists at all.
    /// </summary>
    /// <remarks>
    /// A rate, so 1.0 is normal and 1.3 means skills land 30% sooner. Focus used to feed "Resonance
    /// affinity" — ability power for the manual-combat ability system, which no longer exists. The
    /// auto-battler's equivalent of "your abilities are stronger" is "your skills come round more
    /// often", so it now drives the skill clock. It is the slot that talks to the skills layer.
    /// </remarks>
    /// <remarks>TEMPO (Engineering) is the trainable half; the worn Focus is the rolled half.</remarks>
    public float SquadSkillRate => FocusAttunement * WornMods.SkillRate * (1f + 0.006f * ValueOf(HunterStat.Engineering));

    private float FocusAttunement =>
        _worn[GearSlot.Focus] is { } f ? 1f + 0.05f * Gear.RarityPower(f.Rarity) * Gear.ItemLevelFactor(f.ItemLevel) : 1f;

    /// <summary>Everything the squad drags home, from Guile.</summary>
    public float HaulMultiplier => (1f + 0.010f * ValueOf(HunterStat.Guile)) * WornMods.Haul;
    public int AttackPower => (int)ValueOf(HunterStat.AttackPower);

    /// <summary>Cost of the NEXT rank in a stat. Geometric, so no stat is ever cheap to max.</summary>
    public int NextRankCost(HunterStat stat) => CostOfRank(_ranks[stat], _tuning);

    public static int CostOfRank(int currentRank, ProgressionTuning tuning)
        => (int)Math.Round(
            tuning.BaseTrainingCost * Math.Pow(tuning.TrainingGrowthRate, currentRank),
            MidpointRounding.AwayFromZero);

    public bool CanTrain(HunterStat stat)
        => _ranks[stat] < _tuning.StatRankCap && Gleam >= NextRankCost(stat);

    /// <summary>Buy one rank. Returns false rather than throwing — the UI calls this speculatively.</summary>
    public bool Train(HunterStat stat)
    {
        if (!CanTrain(stat)) return false;

        Gleam -= NextRankCost(stat);
        _ranks[stat]++;
        return true;
    }

    // ── RESET ALL TRAINING — the respec. Playtest: "There should be a stat reset for a certain
    //    resource." The refund is COMPUTED from the cost curve, never tracked: a tracked total is a
    //    second copy of the geometric formula, and this codebase keeps finding that the copy drifts. ──

    /// <summary>
    /// Every Gleam ever paid for the current ranks — the exact sum of each rank's geometric cost.
    /// </summary>
    /// <remarks>
    /// Rank r of a stat cost <c>CostOfRank(r)</c> when it was bought, so a stat at rank n was paid
    /// <c>Σ CostOfRank(0..n−1)</c> — the same formula <see cref="Train"/> charges, read from the same
    /// tuning, which is why this can promise a 100% refund without bookkeeping.
    /// </remarks>
    public int TrainingRefund()
        => _ranks.Values.Sum(rank => Enumerable.Range(0, rank).Sum(r => CostOfRank(r, _tuning)));

    /// <summary>How many Crystal a reset takes. Public so the screen can print the real price.</summary>
    public int TrainingResetCrystalCost => _tuning.TrainingResetCrystalCost;

    /// <summary>
    /// Whether RESET ALL TRAINING would do anything: there are ranks to refund AND the Crystal to pay.
    /// </summary>
    public bool CanResetTraining
        => TrainingRefund() > 0 && MaterialOf(Material.Crystal) >= _tuning.TrainingResetCrystalCost;

    /// <summary>
    /// Undo every trained rank: pay the Crystal price, return 100% of the Gleam the ranks cost, and
    /// set all nine stats back to rank zero. Returns the Gleam refunded — 0 means nothing happened.
    /// </summary>
    /// <remarks>
    /// Validate-then-spend, like every charter call site: the Crystal leaves the stock only when the
    /// reset actually happens. With no ranks trained it refuses even when a Crystal is held — a reset
    /// that takes the rarest material and gives nothing back would be a paid refusal.
    /// </remarks>
    public int ResetTraining()
    {
        var refund = TrainingRefund();
        if (refund <= 0) return 0;
        if (!SpendMaterial(Material.Crystal, _tuning.TrainingResetCrystalCost)) return 0;

        foreach (var stat in Enum.GetValues<HunterStat>()) _ranks[stat] = 0;
        AddGleam(refund);
        return refund;
    }

    public void AddGleam(int amount) => Gleam += Math.Max(0, amount);

    /// <summary>Spend Gleam (the Forge's REFINE costs it alongside materials). False and takes nothing if short.</summary>
    public bool SpendGleam(int amount)
    {
        if (amount <= 0 || Gleam < amount) return false;
        Gleam -= amount;
        return true;
    }

    /// <summary>
    /// Sell items for Gleam.
    /// </summary>
    /// <remarks>
    /// An item equipped to a creature is ineligible for EVERY Forge operation — sell, merge, dismantle,
    /// and feed. The gate lives here, once, rather than being re-checked in four places where one could
    /// be forgotten (and one WAS forgotten: the field existed in the schema and no consumer read it,
    /// leaving equipped charms fully sellable).
    /// </remarks>
    public int Sell(IEnumerable<ItemInstance> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var eligible = items.Where(i => i.EquippedToCreatureId is null).ToList();
        var gained = eligible.Sum(i => i.SellValue);

        AddGleam(gained);
        return gained;
    }

    /// <summary>Total Gleam to take every stat from 0 to the cap — the sink's full lifetime capacity.</summary>
    public static int TotalLifetimeSink(ProgressionTuning tuning)
    {
        var perStat = Enumerable.Range(0, tuning.StatRankCap).Sum(r => CostOfRank(r, tuning));
        return perStat * Enum.GetValues<HunterStat>().Length;
    }
}
