using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// THE ROWS A SCREEN DRAWS ARE THE ROWS CORE DECIDED — <see cref="ItemPresentation"/> owns every
/// player-facing row of an item, and this file holds those rows to naming what they modify.
/// </summary>
/// <remarks>
/// <para>
/// Pass 17 gave every item number one formatter and guarded the formatter; a screen could still
/// compose its own label beside the bare figure — exactly the original "GLOVES  +2%". The rows live
/// in Core now and the four live surfaces (the Gear and Forge inspectors, the item card, the trader's
/// compact card) walk them; these tests walk the same list, so a numeric row whose label is not a
/// stat cannot reach a screen without failing here first.
/// </para>
/// </remarks>
public class item_presentation_test
{
    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity, int level = 10,
                                     GearTrait? prefix = null, int? family = null, EnchantKind? enchant = null, ItemClass? cls = null)
        => new()
        {
            InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 10, ItemLevel = level,
            TraitOverride = prefix, Family = family, EnchantOverride = enchant, Class = cls,
        };

    private static readonly ItemBaseType[] Wearables =
    {
        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Helm,
        ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
    };

    private static IEnumerable<string> SlotAndShapeWords()
    {
        foreach (var t in Enum.GetValues<ItemBaseType>()) yield return ItemNaming.SlotWord(t);
        foreach (var f in ItemNaming.WeaponFamilies) yield return f.ToUpperInvariant();
        yield return "CHEST";
    }

    private static readonly Regex ModifierLine = new(@"^[+-]\d+(\.\d+)?%? [A-Z][A-Z ]*$", RegexOptions.Compiled);

    private static ItemDisplayRow Single(IReadOnlyList<ItemDisplayRow> rows, ItemRowKind kind) => Assert.Single(rows.Where(r => r.Kind == kind));

    [Fact]
    public void test_item_presentation_gloves_and_boots_rows_name_the_stat_not_the_slot()
    {
        // Arrange
        var gloves = Item("g", ItemBaseType.Gloves, Rarity.Common, level: 12);
        var boots = Item("b", ItemBaseType.Boots, Rarity.Common, level: 12);

        // Act
        var glovesBuiltIn = Single(ItemPresentation.Rows(gloves), ItemRowKind.BuiltIn);
        var bootsBuiltIn = Single(ItemPresentation.Rows(boots), ItemRowKind.BuiltIn);

        // Assert — the row that read "GLOVES  +2%" / "BOOTS  +4%".
        Assert.Equal("DAMAGE", glovesBuiltIn.Label);
        Assert.Equal("SKILL RATE", bootsBuiltIn.Label);
        Assert.Matches(@"^\+\d+%$", glovesBuiltIn.Value);
        Assert.Matches(ModifierLine, glovesBuiltIn.Line);
        Assert.Matches(ModifierLine, bootsBuiltIn.Line);
        Assert.DoesNotContain("GLOVES", glovesBuiltIn.Line);
        Assert.DoesNotContain("BOOTS", bootsBuiltIn.Line);
        Assert.Equal(ItemRowTone.Accent, glovesBuiltIn.Tone);
    }

    [Fact]
    public void test_item_presentation_a_multi_modifier_item_lists_every_roll_and_its_built_in()
    {
        var ring = Item("ring", ItemBaseType.Ring, Rarity.Legendary, level: 30);
        var rows = ItemPresentation.Rows(ring);

        var affixes = rows.Where(r => r.Kind == ItemRowKind.Affix).ToList();
        Assert.Equal(4, affixes.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, affixes.Select(r => r.Index));
        Assert.All(affixes, r =>
        {
            Assert.NotNull(r.Modifier);
            Assert.Equal(ItemModifiers.Word(r.Modifier!.Value.Stat), r.Label);
            Assert.Equal(ItemModifiers.Value(r.Modifier!.Value), r.Value);
            Assert.Equal(ItemRowTone.Bonus, r.Tone);
        });
        Assert.Equal("LOOT", Single(rows, ItemRowKind.BuiltIn).Label);
        // Card order: rolls, then the built-in, then the enchant.
        var list = rows.ToList();
        Assert.True(list.FindLastIndex(r => r.Kind == ItemRowKind.Affix) < list.FindIndex(r => r.Kind == ItemRowKind.BuiltIn));
    }

    [Fact]
    public void test_item_presentation_a_tradeoff_prefix_shows_both_sides_with_the_cost_marked()
    {
        var heavy = Item("heavy", ItemBaseType.Weapon, Rarity.Rare, level: 20, prefix: GearTrait.Heavy, family: 0);
        var rows = ItemPresentation.Rows(heavy);

        var head = Single(rows, ItemRowKind.Prefix);
        Assert.Equal("HEAVY", head.Label);
        Assert.Equal(GearTraits.BlurbOf(GearTrait.Heavy), head.Note);
        var sides = rows.Where(r => r.Kind == ItemRowKind.PrefixSide).ToList();
        Assert.Equal(2, sides.Count);
        Assert.Contains(sides, r => r.Label == "DAMAGE" && !r.IsDrawback && r.Tone == ItemRowTone.Accent);
        var cost = Assert.Single(sides, r => r.IsDrawback);
        Assert.Equal("SKILL RATE", cost.Label);
        Assert.Equal("-20%", cost.Value);
        Assert.Equal(ItemRowTone.Cost, cost.Tone);
        Assert.Equal("-20% SKILL RATE", cost.Line);
        // The sides follow their head, immediately.
        var at = rows.ToList().IndexOf(head);
        Assert.Equal(ItemRowKind.PrefixSide, rows[at + 1].Kind);
        // A weapon's built-in is followed by the family's line.
        Assert.Equal("A BLADE HITS HARDER.", Single(rows, ItemRowKind.Family).Label);
    }

    [Fact]
    public void test_item_presentation_the_enchant_row_carries_its_sentence_and_the_builds_verdict()
    {
        var blade = Item("ench", ItemBaseType.Weapon, Rarity.Legendary, level: 30, enchant: EnchantKind.Fervour, family: 0);
        var ench = Enchantments.Of(blade)!;

        var bare = Single(ItemPresentation.Rows(blade), ItemRowKind.Enchant);
        Assert.Equal("FERVOUR", bare.Label);
        Assert.Equal(ench.Blurb, bare.Note);
        Assert.Equal("BLOODLUST: UP TO +28% MORE DAMAGE AT LOW HEALTH", bare.Note);
        Assert.DoesNotContain(ItemPresentation.Rows(blade), r => r.Kind == ItemRowKind.EnchantNeed);   // no build facts, no verdict

        // With the weave known and the keystones known: BLOODLUST absent — dead, and said so.
        var woven = new[] { SkillCatalogue.ById("hammer_blow") };
        var dead = Single(ItemPresentation.Rows(blade, woven: woven, triggers: Array.Empty<BuildTrigger>(), swornVows: 0), ItemRowKind.EnchantNeed);
        Assert.Equal(ItemRowTone.Cost, dead.Tone);
        Assert.StartsWith("NEEDS BLOODLUST", dead.Label);
        var live = Single(ItemPresentation.Rows(blade, woven: woven, triggers: new[] { BuildTrigger.Bloodlust }, swornVows: 0), ItemRowKind.EnchantNeed);
        Assert.Equal(ItemRowTone.Bonus, live.Tone);
        Assert.Equal("WORKS WITH YOUR BUILD", live.Label);
        // A screen that passes the weave but not the sockets is judged as socketing none — never
        // gilded for a fact it did not bring (an item wrongly greyed is a player who looks again;
        // one wrongly gilded is a player who equips it and wonders why nothing changed).
        var unknown = Single(ItemPresentation.Rows(blade, woven: woven), ItemRowKind.EnchantNeed);
        Assert.Equal(ItemRowTone.Cost, unknown.Tone);
    }

    [Fact]
    public void test_item_presentation_forge_candidates_each_carry_their_effect_sentence()
    {
        var blade = Item("cand", ItemBaseType.Weapon, Rarity.Legendary, level: 30, enchant: EnchantKind.Fervour, family: 0);
        var woven = new[] { SkillCatalogue.ById("hammer_blow") };

        var candidates = ItemPresentation.EnchantCandidates(blade, woven, Array.Empty<BuildTrigger>(), 0);

        var pool = Enchantments.PoolFor(GearSlot.Weapon).Where(k => k != EnchantKind.Fervour).ToList();
        Assert.Equal(pool.Count, candidates.Count);
        Assert.DoesNotContain(candidates, c => c.Label == "FERVOUR");   // a re-roll never returns the current one
        foreach (var (kind, row) in pool.Zip(candidates))
        {
            var expected = new Enchantment(kind, Enchantments.MagnitudeFor(kind, blade.Rarity));
            Assert.Equal(expected.Name, row.Label);
            Assert.Equal(expected.Blurb, row.Note);
            Assert.False(string.IsNullOrWhiteSpace(row.Note));
            Assert.Equal(ItemRowKind.Enchant, row.Kind);
        }
        // REVERB needs ECHO, which this build lacks: its verdict says so; VENOM needs nothing and says nothing.
        Assert.StartsWith("NEEDS ECHO", candidates.Single(c => c.Label == "REVERB").Value);
        Assert.Equal("", candidates.Single(c => c.Label == "VENOM").Value);
        // A gem, an unwearable, offers nothing to re-roll.
        Assert.Empty(ItemPresentation.EnchantCandidates(Item("gem", ItemBaseType.Gem, Rarity.Rare), woven, Array.Empty<BuildTrigger>(), 0));
    }

    [Fact]
    public void test_item_presentation_power_and_verdict_rows_compare_against_what_is_worn()
    {
        var hunter = new Hunter();
        var worn = Item("worn", ItemBaseType.Gloves, Rarity.Rare, level: 10, cls: ItemClass.Wanderer);
        hunter.Equip(worn);
        var better = Item("better", ItemBaseType.Gloves, Rarity.Legendary, level: 40, cls: ItemClass.Wanderer);
        var rows = ItemPresentation.Rows(better, hunter, CharacterRoster.Get("seeker"));

        var power = Single(rows, ItemRowKind.Power);
        Assert.Equal("ITEM POWER", power.Label);
        Assert.Equal(hunter.PowerContribution(better).ToString("N0", System.Globalization.CultureInfo.InvariantCulture), power.Value);
        var verdict = Single(rows, ItemRowKind.Verdict);
        Assert.Equal("UPGRADE", verdict.Label);
        Assert.StartsWith("+", verdict.Value);
        Assert.Equal(ItemRowTone.Bonus, verdict.Tone);
        Assert.Equal($"REPLACES {ItemNaming.FullName(worn)}", verdict.Note);

        // The worn piece itself, and an empty slot, and a piece this champion cannot wear.
        Assert.Equal("WORN", Single(ItemPresentation.Rows(worn, hunter), ItemRowKind.Verdict).Label);
        var boots = Item("boots", ItemBaseType.Boots, Rarity.Rare, level: 10, cls: ItemClass.Wanderer);
        Assert.Equal("THE SLOT IS EMPTY", Single(ItemPresentation.Rows(boots, hunter), ItemRowKind.Verdict).Label);
        var foreign = Item("warden", ItemBaseType.Gloves, Rarity.Rare, level: 10, cls: ItemClass.Warden);
        var refusal = Single(ItemPresentation.Rows(foreign, hunter, CharacterRoster.Get("seeker")), ItemRowKind.Verdict);
        Assert.Equal("NOT FOR THE SEEKER", refusal.Label);
        Assert.EndsWith("CAN WEAR IT", refusal.Note);
        Assert.Equal(ItemRowTone.Cost, refusal.Tone);
        // No hunter, no power, no verdict, no set.
        Assert.DoesNotContain(ItemPresentation.Rows(better), r => r.Kind is ItemRowKind.Power or ItemRowKind.Verdict or ItemRowKind.Set);
    }

    [Fact]
    public void test_item_presentation_no_numeric_row_reaches_a_screen_without_naming_its_stat()
    {
        // THE ROW GUARD — the screens' own rows, not the leaf formatter. Every wearable base type,
        // every weapon family, every rarity, every prefix its slot can roll, with and without a hunter:
        // a row that states a number is a modifier row whose label is the stat, or one of the few
        // captions that are not modifiers at all (power, a verdict, a count, a set, an enchant's sentence).
        var statWords = Enum.GetValues<AffixStat>().Select(ItemModifiers.Word).ToHashSet(StringComparer.Ordinal);
        var forbidden = SlotAndShapeWords().ToHashSet(StringComparer.Ordinal);
        var captions = new HashSet<ItemRowKind> { ItemRowKind.Power, ItemRowKind.Verdict, ItemRowKind.GemCount, ItemRowKind.Set, ItemRowKind.Enchant, ItemRowKind.EnchantNeed };
        var hunter = new Hunter();
        var rows = 0;
        var gemRows = 0;
        foreach (var type in Wearables)
        foreach (var rarity in Enum.GetValues<Rarity>())
        foreach (var level in new[] { 1, 20, 60 })
        foreach (var prefix in new GearTrait?[] { null }.Concat(GearTraits.PoolFor(Gear.SlotFor(type)!.Value).Cast<GearTrait?>()))
        for (var id = 0; id < 4; id++)
        {
            var family = type == ItemBaseType.Weapon ? id % ItemNaming.WeaponFamilies.Length : (int?)null;
            var item = Item($"row_{type}_{rarity}_{level}_{prefix}_{id}", type, rarity, level, prefix, family);
            // A set gem where the rarity has a socket, so the gem layer is swept too (its row wears
            // the gem's name; its Line names the stat).
            if (GemCraft.SocketCount(rarity) > 0)
                item.Gems.Add(new ItemInstance { InstanceId = $"gem_{type}_{rarity}_{id}", BaseType = ItemBaseType.Gem, Rarity = Rarity.Rare, SellValue = 1, ItemLevel = level });
            foreach (var withHunter in new[] { false, true })
            foreach (var row in ItemPresentation.Rows(item, withHunter ? hunter : null))
            {
                rows++;
                Assert.False(forbidden.Contains(row.Label), $"{item.InstanceId}: a row labelled by the slot or the shape — \"{row.Label}\"");
                if (row.Modifier is { } m)
                {
                    if (row.Kind == ItemRowKind.Gem) { gemRows++; Assert.EndsWith(" GEM", row.Label); }
                    else Assert.Equal(ItemModifiers.Word(m.Stat), row.Label);
                    Assert.Equal(ItemModifiers.Value(m), row.Value);
                    Assert.Matches(ModifierLine, row.Line);
                }
                else if (row.IsNumeric)
                    Assert.True(captions.Contains(row.Kind), $"{item.InstanceId}: \"{row.Label}  {row.Value}\" states a number and names no stat");
                // The single-line form is never a slot word beside a figure.
                Assert.DoesNotMatch(@"^(" + string.Join("|", forbidden.Select(Regex.Escape)) + @")\s+[+-]?\d", row.Line);
            }
        }
        Assert.True(rows > 5_000, $"the sweep read only {rows} rows — it is not sweeping the catalogue");
        Assert.True(gemRows > 0, "the sweep socketed no gem — the gem layer is not swept");
    }
}
