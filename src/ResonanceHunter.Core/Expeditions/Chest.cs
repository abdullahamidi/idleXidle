using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Expeditions;

/// <summary>
/// A reward CONTAINER a boss drops, opened later for its contents.
/// </summary>
/// <remarks>
/// <para>
/// The player asked for chests: the farm is not a material source, materials come from monsters, from the
/// chests monsters drop, and from dismantling — and opening a chest is a mechanic players enjoy for its
/// own sake ("kutu açma… dopamin sağladığı için güzel"). It also answers a real problem the loot rework
/// created: as item count grows, a firehose of items straight into the bag is noise. A chest gates the
/// haul behind one satisfying click — the anticipation of the roll, not the roll itself.
/// </para>
/// <para>
/// A chest carries its <see cref="Rarity"/> (the grade you see BEFORE opening — the hook) and the
/// <see cref="Tier"/> + <see cref="Element"/> that decide what is inside. Contents are rolled at OPEN,
/// never at drop, so the reveal is a genuine uncertainty rather than a delayed reading of a known result.
/// </para>
/// </remarks>
public sealed record Chest
{
    /// <summary>The chest's grade — a richer grade means RARER items (never more of them). Rolled at drop.</summary>
    public required Rarity Rarity { get; init; }

    /// <summary>The loot tier its items roll at — depth folded in, exactly as a boss drop's power tier.</summary>
    public required int Tier { get; init; }

    /// <summary>The region's element, so the items inside are attuned like any other regional drop.</summary>
    public Source? Element { get; init; }

    /// <summary>
    /// WHERE this chest was won, so its contents lean toward what that place is known for.
    /// </summary>
    /// <remarks>
    /// A chest is opened long after the fight that dropped it, often in another region entirely, so the
    /// place has to travel WITH the chest — the same reason <see cref="RunTilt"/> exists. Null is a
    /// chest from before regions had drop profiles, and it rolls exactly as it always did.
    /// </remarks>
    public string? Region { get; init; }

    /// <summary>How well the descent that earned this chest was fought — a rarity tilt, 1.0 for neutral.</summary>
    /// <remarks>
    /// <para>
    /// THE CHEST REMEMBERS ITS RUN, and it has to, because a chest is opened long after the fight that
    /// dropped it. Without this the run's <c>Haul.Quality</c> had nowhere to go: it was accumulated
    /// correctly wave by wave, carried correctly to the end, and then read by nothing at all — the
    /// SPLINTER trigger's ONLY effect in the whole simulation, spent into a field with no consumer.
    /// </para>
    /// <para>
    /// That made everything granting SPLINTER pay nothing: the REAPER keystone, which costs -25% skill
    /// rate for it and was therefore a strictly negative socket; the Splinter weapon enchant, one of
    /// three in the weapon pool, including its rarity-scaled magnitude; and THE QUIVER's grant.
    /// </para>
    /// <para>
    /// Neutral is 1.0 rather than 0, because it multiplies through the same <c>buildTilt</c> path the
    /// build's own rarity uses — an old chest deserialised without the field must behave exactly as it
    /// did before, and 0 would silently make every one of them worthless.
    /// </para>
    /// </remarks>
    public float RunTilt { get; init; } = 1f;
}

/// <summary>What a chest paid out when opened: materials, and the items to land in the bag.</summary>
/// <summary>
/// What an opened chest paid. GEMS ride their own channel: the dossier promises "N items", and a gem
/// inflating that count (or breaking the rarity floor with its level-tracking frame grade) would make
/// the promise a lie.
/// </summary>
public sealed record ChestReward(int Materials, IReadOnlyList<ItemInstance> Items)
{
    public IReadOnlyList<ItemInstance> Gems { get; init; } = Array.Empty<ItemInstance>();
}

/// <summary>Knobs for what chests drop and what they pay. Data-driven so the numbers live in one place.</summary>
/// <remarks>
/// <b>Grade buys QUALITY, not QUANTITY.</b> Player direction: a better chest must mean VALUABLE items, not
/// MORE of them — "çok fazla çıkarsa oyuncu eşyalarla ve materyallerle boğulur". So the item count and the
/// material payout stay in a tight, roughly-constant band whatever the grade; only the item RARITY climbs
/// with the grade. The exact counts are a deliberate polish-pass knob, which is why they live here.
/// </remarks>
public sealed record ChestTuning
{
    /// <summary>Materials a chest pays before the small spread and depth nudge. GRADE never changes this.</summary>
    public int BaseMaterials { get; init; } = 12;

    /// <summary>Random spread on the payout: base + [0, this]. Keeps opens "mostly the same" (çoğunlukla aynı).</summary>
    public int MaterialsVariance { get; init; } = 6;

    /// <summary>One extra material per this many tiers of depth — a mild nudge for pushing, never a flood.</summary>
    public int TiersPerBonusMaterial { get; init; } = 4;

    /// <summary>
    /// How much the chest's grade tilts the RARITY of its items. This is the whole payoff of a good chest —
    /// valuable loot, not a bigger pile.
    /// </summary>
    public float QualityPerRarityPower { get; init; } = 0.14f;

    /// <summary>Items a chest always carries — the floor of the tight count.</summary>
    public int MinItems { get; init; } = 1;

    /// <summary>Chance of ONE extra item on top. Keeps it "mostly 1, sometimes 2" — never a pile, any grade.</summary>
    public float ExtraItemChance { get; init; } = 0.35f;

    /// <summary>Chance a chest also carries a STAT GEM (see <c>GemCraft</c>).</summary>
    public float GemChance { get; init; } = 0.30f;

    /// <summary>
    /// The MINIMUM item rarity each grade GUARANTEES (indexed Common..Legendary). A good chest cannot
    /// disappoint — a Legendary always yields at least an Epic, an Epic at least a Rare.
    /// </summary>
    /// <remarks>
    /// This is the "değerli eşyalar" promise made reliable, and it is what loot-box design calls BOUNDED
    /// unpredictability: you know a Legendary chest gives a great item, the only question is HOW great. An
    /// unbounded roll — where a Legendary chest can still cough up a Common — reads as a betrayal and
    /// breaks the loop. The quality tilt still pushes ABOVE this floor; the floor just removes the feel-bad.
    /// </remarks>
    public IReadOnlyList<Rarity> ItemFloorByGrade { get; init; } =
        new[] { Rarity.Common, Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic };

    /// <summary>Chest-grade weights at tier 0 (Common..Legendary). Common dominates a shallow drop.</summary>
    public IReadOnlyList<double> BaseRarityWeight { get; init; } = new[] { 100.0, 40.0, 15.0, 4.0, 0.5 };

    /// <summary>Per-tier shift added to each grade's weight — depth pushes the GRADE roll upward.</summary>
    public IReadOnlyList<double> TierRarityShift { get; init; } = new[] { -6.0, 1.0, 2.0, 1.2, 0.5 };

    /// <summary>
    /// Chance a felled boss actually drops a chest. LOW on purpose — a chest is an event, not a paycheck.
    /// </summary>
    /// <remarks>
    /// Every boss used to drop one, guaranteed, which buried the player in chests (playtest: "too many
    /// chests drop"). A drop RATE makes the drop mean something: most bosses give nothing, and the one that
    /// does is a moment. The chance rises a little with depth — farming a deeper region pays out somewhat
    /// more often, "seviyeye göre" — but is capped so it never becomes a guarantee again.
    /// </remarks>
    /// <remarks>
    /// <b>Retuned after counting.</b> 0.30 rising to 0.60 sounded like "sometimes" and measured as a
    /// metronome: 60% of bosses, and a boss every fifth wave, is a chest every eight waves — about one
    /// every twenty seconds at the speed waves actually die. The playtest's "still too many chests"
    /// was correct and the old numbers said so if anyone added them up.
    ///
    /// A chest is now roughly one boss in eight at the surface and one in three at depth. That keeps
    /// the promise the comment above always made — an event, not a paycheck — and it makes the depth
    /// nudge readable: farming deep genuinely pays out more often instead of both ends sitting at the
    /// cap.
    /// </remarks>
    /// <remarks>
    /// <b>ONE FLAT PERCENTAGE, no depth ramp.</b> Player direction after playing it: a chest should be
    /// "a percent chance to drop from a boss", not a number you have to reconstruct from a base, a
    /// per-tier slope and a cap. The ramp was invisible from inside the game — nobody can feel 12%
    /// creeping toward 35% — so it bought nothing except a rule the player could not hold in their head,
    /// and it quietly made deep farming the only sensible place to be.
    ///
    /// Depth still buys everything it should: the chest's GRADE, and the item tier inside it. What it no
    /// longer buys is how OFTEN, which is now the same promise everywhere in the game: one boss in five.
    /// </remarks>
    public float DropChance { get; init; } = 0.20f;

    public static ChestTuning Default { get; } = new();
}

/// <summary>Dropping chests, and cracking them open.</summary>
public static class Chests
{
    /// <summary>
    /// Does this chest pass the player's keep-filter?
    /// </summary>
    /// <remarks>
    /// Playtest: "Gereksiz chestleri almak istemezsem diye loot filtresi koyabilmem lazım... örneğin
    /// sadece tier 10 üstü, veya sadece ring atan kutuları al." Two axes: a tier floor, and a slot the
    /// chest's region must favour. A chest with NO regional lean can drop anything, so it passes every
    /// slot filter — filtering it away would throw out exactly the chests that might hold the wanted
    /// slot.
    /// </remarks>
    public static bool PassesKeepFilter(Chest chest, int minTier, ItemBaseType? slot)
        => PassesKeepFilter(chest, minTier, slot is { } one ? new[] { one } : Array.Empty<ItemBaseType>());

    /// <summary>
    /// The keep-filter with SEVERAL wanted slots (2026-08-23: "hem bot hem kolye arıyor olabilirim"):
    /// a chest passes the slot half when nothing is wanted, when its region has no lean, or when its
    /// lean favours ANY of the wanted slots. The tier floor applies as before.
    /// </summary>
    public static bool PassesKeepFilter(Chest chest, int minTier, IReadOnlyCollection<ItemBaseType> slots)
    {
        ArgumentNullException.ThrowIfNull(chest);
        ArgumentNullException.ThrowIfNull(slots);
        if (chest.Tier < minTier) return false;
        if (slots.Count == 0) return true;
        var favoured = ChestDossiers.For(chest).Favoured;
        if (favoured.Count == 0) return true;
        foreach (var wanted in slots)
            if (favoured.Contains(wanted)) return true;
        return false;
    }

    /// <summary>
    /// What a filtered-away chest pays instead of landing — modest Scrap, so the filter is a
    /// convenience, never a farm that outearns opening.
    /// </summary>
    public static int FilterCompensation(Chest chest)
    {
        ArgumentNullException.ThrowIfNull(chest);
        return 8 + chest.Tier;
    }

    /// <summary>
    /// Roll a chest's GRADE for a drop at a given tier. Deeper tiers push the odds toward the top.
    /// </summary>
    /// <remarks>
    /// Same philosophy as the item drop it replaces: depth buys RARITY, not quantity. A wave-40 boss
    /// drops a chest that is likelier to be Legendary, not a bigger pile of chests.
    /// </remarks>
    public static Rarity RollRarity(int tier, Random rng, ChestTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        tuning ??= ChestTuning.Default;

        var t = Math.Max(0, tier);
        var w = new double[5];
        for (var i = 0; i < 5; i++)
            w[i] = Math.Max(0.0, tuning.BaseRarityWeight[i] + t * tuning.TierRarityShift[i]);

        var total = w.Sum();
        if (total <= 0) return Rarity.Common;   // deep enough that Common fell to zero and rounding bit

        var roll = rng.NextDouble() * total;
        var acc = 0.0;
        for (var i = 0; i < 5; i++)
        {
            acc += w[i];
            if (roll < acc) return (Rarity)i;
        }
        return Rarity.Legendary;
    }

    /// <summary>How likely a felled boss is to drop a chest at all — one flat chance, everywhere.</summary>
    /// <remarks>
    /// Keeps the tier parameter it no longer reads. Callers pass a depth because a chest's GRADE and
    /// item TIER are still depth-driven, and removing it from this one signature would make the call
    /// sites disagree about whether depth matters here at all — it does, just not to the frequency.
    /// </remarks>
    public static float DropChance(int tier, ChestTuning? tuning = null)
        => (tuning ?? ChestTuning.Default).DropChance;

    /// <summary>Mint a chest a boss drops at this tier — grade rolled now, contents rolled at open.</summary>
    public static Chest RollDrop(int tier, Source? element, Random rng, ChestTuning? tuning = null,
                                 float runTilt = 1f, string? region = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return new Chest
        {
            Rarity = RollRarity(tier, rng, tuning),
            Tier = Math.Max(1, tier),
            Element = element,
            Region = region,
            RunTilt = MathF.Max(0.05f, runTilt),
        };
    }

    /// <summary>
    /// Crack a chest open: a TIGHT, roughly-constant amount of materials + items, rolled fresh.
    /// </summary>
    /// <remarks>
    /// <b>Grade buys rarity, not quantity.</b> The item count is a tight 1–2 (whatever
    /// <see cref="ExpeditionLoot.RollBoss"/> gives, the same for every grade) and materials sit in a small
    /// band nudged only by DEPTH — so a Legendary chest is exciting because its item is RARER, never
    /// because it buries you in loot. The grade feeds ONE thing: the quality tilt on that item.
    /// <para>
    /// Pure — it does not touch a stash or apply loot filters. The caller lands the items and spends the
    /// materials, exactly as the Forge's merge and reforge are pure and the screen owns the mutation.
    /// </para>
    /// </remarks>
    /// <param name="rarityBonus">
    /// The build's loot-quality tilt, from <see cref="Builds.BuildMods.Rarity"/> — 1 is neutral.
    /// </param>
    /// <remarks>
    /// REGRESSION FIX. Rarity was resolved from keystones, gear and the trait tree, carried through the
    /// expedition as Haul.Quality, summed across waves — and then read by nothing. Every node on the
    /// FORTUNE road, and every "+RARITY" affix, was inert. The tilt multiplies the chest grade's own
    /// quality so a rarity build makes GOOD chests better rather than making bad ones adequate.
    /// </remarks>
    /// <param name="favouredClass">
    /// The class of the champion opening it — four in five class-locked pieces inside are theirs.
    /// Null rolls uniformly across the five classes.
    /// </param>
    public static ChestReward Open(
        Chest chest, Random rng, LootTuning? loot = null, ChestTuning? tuning = null,
        float rarityBonus = 1f, ItemClass? favouredClass = null)
    {
        ArgumentNullException.ThrowIfNull(chest);
        ArgumentNullException.ThrowIfNull(rng);
        tuning ??= ChestTuning.Default;
        loot ??= LootTuning.Default;

        // Materials: a tight band, nudged only by depth. GRADE is deliberately absent — it must not pay
        // MORE, only rarer items below.
        var materials = tuning.BaseMaterials
                        + rng.Next(tuning.MaterialsVariance + 1)
                        + chest.Tier / Math.Max(1, tuning.TiersPerBonusMaterial);

        // Items: a tight, grade-independent count. RollBoss mints a variable pile (base + bonus rolls), so
        // the count is CAPPED here with Take — the grade must not leak into quantity. The grade feeds the
        // quality tilt (rarer on average) AND a guaranteed FLOOR (never below the grade's minimum), so a
        // good chest is reliably valuable — bounded reward, the loot-box rule for "generous, not exploitative".
        var quality = 1f + Gear.RarityPower(chest.Rarity) * tuning.QualityPerRarityPower;
        var itemCount = tuning.MinItems + (rng.NextDouble() < tuning.ExtraItemChance ? 1 : 0);
        var floor = tuning.ItemFloorByGrade[(int)chest.Rarity];

        // A chest's ITEMS are always GEAR. The chest already pays material CURRENCY above, so a Material-type
        // item would be redundant clutter and an anticlimactic reveal ("a Legendary chest gave me… a
        // material"). Material rolls are discarded; if the whole roll happened to be materials, one gear
        // piece is minted at the grade's floor so a chest is never "just materials".
        // The build's standing rarity AND how the run that won this chest was fought, multiplied: both
        // are "you earned better odds", and they compose the way every other pair of multipliers here
        // does. RunTilt defaults to 1, so a chest from before it existed rolls exactly as it always did.
        var gear = ExpeditionLoot.RollBoss(
                chest.Tier, quality, rng, loot, element: chest.Element,
                buildTilt: MathF.Max(0.05f, rarityBonus * MathF.Max(0.05f, chest.RunTilt)),
                region: chest.Region, favouredClass: favouredClass)
            .Where(i => i.BaseType is not ItemBaseType.Material)
            .ToList();
        if (gear.Count == 0)
            gear.Add(MintGear(floor, chest.Element, rng, loot, chest.Tier, favouredClass));

        var items = gear.Take(itemCount).Select(i => Elevate(i, floor, loot)).ToList();

        // STAT GEMS ride in chests — the socket system's supply line. On their OWN channel, outside
        // Elevate and the item-count promise: a gem's frame grade tracks its LEVEL, and the chest's
        // rarity floor must not repaint it.
        var gems = rng.NextDouble() < tuning.GemChance
            ? new[] { Economy.GemCraft.MintGem(chest.Tier, rng) }
            : Array.Empty<ItemInstance>();

        return new ChestReward(materials, items) { Gems = gems };
    }

    private static readonly ItemBaseType[] GearTypes =
    {
        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus,
        ItemBaseType.Helm, ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
    };

    /// <summary>Mint one wearable at a set rarity — the guaranteed-gear fallback when a roll was all materials.</summary>
    private static ItemInstance MintGear(Rarity rarity, Source? element, Random rng, LootTuning loot, int tier,
                                         ItemClass? favouredClass)
    {
        var type = GearTypes[rng.Next(GearTypes.Length)];
        var id = $"itm_{rng.Next(int.MaxValue):x8}";
        var prefix = Economy.GearTraits.RollPrefix(type, rng);   // the prefix is born here
        // Same class shape as LootSystem.Mint: class-locked slots roll one, the rest stay null, and a
        // weapon's family comes from its class's own shapes. Drawn after the id and prefix.
        var cls = ItemClasses.IsClassLocked(type) ? ItemClasses.Roll(favouredClass, rng, loot.ClassRoll) : (ItemClass?)null;
        return new()
        {
            InstanceId = id,
            BaseType = type,
            Rarity = rarity,
            SellValue = loot.RaritySellValue[(int)rarity],
            Element = element,
            ItemLevel = Math.Max(1, tier),
            TraitOverride = prefix,
            Class = cls,
            Family = type == ItemBaseType.Weapon && cls is { } c ? ItemClasses.RollFamily(c, rng) : null,
        };
    }

    /// <summary>Lift an item to the grade's rarity FLOOR if the roll came in below it — never lower it.</summary>
    /// <remarks>
    /// Rarity and sell value move together (sell value is a pure function of rarity), so both are bumped.
    /// Lifting rarity also grants the item its rarity-gated enchantment and stronger trait mods — a
    /// floor-elevated item is genuinely better, not just a recoloured one.
    /// </remarks>
    private static ItemInstance Elevate(ItemInstance item, Rarity floor, LootTuning loot)
        => item.Rarity >= floor
            ? item
            : item with { Rarity = floor, SellValue = loot.RaritySellValue[(int)floor] };
}
