using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// Every number an item shows says WHAT it modifies — through the one formatter, <see cref="ItemModifiers"/>.
/// </summary>
/// <remarks>
/// <para>
/// The item card printed the built-in bonus as <c>"GLOVES  +2%"</c> and <c>"BOOTS  +4%"</c>: the slot
/// word where the stat belongs, a line no player can finish without knowing the item generator's
/// slot-to-stat table (2026-09-07). The stat was always data on the item — <see cref="ItemFamilies.BonusOf"/>
/// returns a typed <see cref="AffixStat"/> — so the fix is to print that word, from the one place
/// every item line is printed. These tests hold every layer to it: the built-in, the rolled affixes,
/// both sides of the prefix's trade, the set gems, and the enchant sentences.
/// </para>
/// <para>
/// The guard at the end is the catalogue-level rule the brief asks for: <b>no live numeric item
/// modifier may format as only a slot name and a number.</b> It sweeps every wearable base type,
/// every family, every rarity and every prefix across a spread of ids and levels, and refuses any
/// line whose word is not a stat.
/// </para>
/// </remarks>
public class item_modifier_presentation_test
{
    private static readonly ItemBaseType[] Wearables =
    {
        ItemBaseType.Weapon, ItemBaseType.Charm, ItemBaseType.AbilityFocus, ItemBaseType.Helm,
        ItemBaseType.Chest, ItemBaseType.Gloves, ItemBaseType.Boots, ItemBaseType.Ring,
    };

    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity, int level = 10,
                                     GearTrait? prefix = null, int? family = null, EnchantKind? enchant = null)
        => new()
        {
            InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 10, ItemLevel = level,
            TraitOverride = prefix, Family = family, EnchantOverride = enchant,
        };

    /// <summary>The words that must never stand where a stat belongs: every slot word and every weapon family.</summary>
    private static IEnumerable<string> SlotAndShapeWords()
    {
        foreach (var t in Enum.GetValues<ItemBaseType>()) yield return ItemNaming.SlotWord(t);
        foreach (var f in ItemNaming.WeaponFamilies) yield return f.ToUpperInvariant();
        yield return "CHEST";   // the Gear screen's short slot word
    }

    // A modifier line: a signed figure, its unit, one space, the stat in capitals.
    private static readonly Regex LineShape = new(@"^[+-]\d+(\.\d+)?%? [A-Z][A-Z ]*$", RegexOptions.Compiled);

    [Fact]
    public void test_every_stat_has_a_word_a_unit_and_a_signed_value()
    {
        var units = new Dictionary<AffixStat, ModifierUnit>
        {
            [AffixStat.Damage] = ModifierUnit.Percent, [AffixStat.Health] = ModifierUnit.Percent,
            [AffixStat.SkillRate] = ModifierUnit.Percent, [AffixStat.Haul] = ModifierUnit.Percent,
            [AffixStat.Crit] = ModifierUnit.Points, [AffixStat.Defense] = ModifierUnit.Flat,
        };
        foreach (var stat in Enum.GetValues<AffixStat>())
        {
            var word = ItemModifiers.Word(stat);
            Assert.False(string.IsNullOrWhiteSpace(word), $"{stat} has no word");
            Assert.Equal(word.ToUpperInvariant(), word);
            Assert.DoesNotContain(word, SlotAndShapeWords());
            Assert.Equal(units[stat], ItemModifiers.UnitOf(stat));
            Assert.StartsWith("+", ItemModifiers.Value(stat, 0.5f));
            Assert.StartsWith("-", ItemModifiers.Value(stat, -0.5f));
        }

        // The unit, by stat: a share of a channel, percentage points, a flat amount.
        Assert.Equal("+4%", ItemModifiers.Value(AffixStat.Damage, 0.04f));
        Assert.Equal("+1.5%", ItemModifiers.Value(AffixStat.Crit, 1.5f));
        Assert.Equal("+8", ItemModifiers.Value(AffixStat.Defense, 8f));
        Assert.Equal("-20%", ItemModifiers.Value(AffixStat.SkillRate, -0.20f));
        Assert.Equal("+11.2%", ItemModifiers.Value(AffixStat.Damage, 0.112f, precise: true));
        Assert.Equal("+14.3", ItemModifiers.Value(AffixStat.Defense, 14.3f, precise: true));
        Assert.Equal("+1.53%", ItemModifiers.Value(AffixStat.Crit, 1.53f, precise: true));
    }

    [Fact]
    public void test_a_line_is_the_value_then_the_stat_it_moves()
    {
        Assert.Equal("+4% DAMAGE", ItemModifiers.Line(AffixStat.Damage, 0.04f));
        Assert.Equal("+8 DEFENCE", ItemModifiers.Line(AffixStat.Defense, 8f));
        Assert.Equal("+1.5% CRITICAL CHANCE", ItemModifiers.Line(AffixStat.Crit, 1.5f));
        Assert.Equal("-20% SKILL RATE", ItemModifiers.Line(AffixStat.SkillRate, -0.20f));
        Assert.Equal("+6% LOOT", ItemModifiers.Line(AffixStat.Haul, 0.06f));
    }

    [Fact]
    public void test_flat_and_percent_are_two_different_facts()
    {
        // +12 HEALTH and +12% HEALTH are not the same claim; the stat decides, never the caller.
        Assert.Equal("+12% HEALTH", ItemModifiers.Line(AffixStat.Health, 0.12f));
        Assert.Equal("+12 DEFENCE", ItemModifiers.Line(AffixStat.Defense, 12f));
        Assert.DoesNotContain("%", ItemModifiers.Line(AffixStat.Defense, 12f));
        Assert.Contains("%", ItemModifiers.Line(AffixStat.Crit, 12f));
    }

    [Fact]
    public void test_the_built_in_names_its_stat_never_its_slot()
    {
        // The line that read "GLOVES  +2%". The stat is real data on the item; the slot is not the effect.
        foreach (var type in Wearables)
        {
            var families = type == ItemBaseType.Weapon ? Enumerable.Range(0, ItemNaming.WeaponFamilies.Length) : new[] { 0 };
            foreach (var family in families)
            {
                var item = Item($"bi_{type}_{family}", type, Rarity.Common, level: 12, family: type == ItemBaseType.Weapon ? family : null);
                var builtIn = ItemModifiers.BuiltIn(item);
                var expected = ItemFamilies.BonusOf(item)!.Value;
                var m = Assert.Single(builtIn);
                Assert.Equal(ModifierLayer.BuiltIn, m.Layer);
                Assert.Equal(expected.Stat, m.Stat);
                Assert.Equal(expected.Magnitude, m.Magnitude);

                var line = ItemModifiers.Line(m);
                Assert.Matches(LineShape, line);
                Assert.EndsWith(" " + ItemModifiers.Word(expected.Stat), line);
                Assert.DoesNotContain(ItemNaming.SlotWord(type), line);
                Assert.DoesNotContain(ItemNaming.TypeWord(item), line);
            }
        }

        // The two lines the brief names, and the two units a slot can carry.
        Assert.EndsWith(" DAMAGE", ItemModifiers.Line(ItemModifiers.BuiltIn(Item("g", ItemBaseType.Gloves, Rarity.Common))[0]));
        Assert.EndsWith(" SKILL RATE", ItemModifiers.Line(ItemModifiers.BuiltIn(Item("b", ItemBaseType.Boots, Rarity.Common))[0]));
        var helm = ItemModifiers.Line(ItemModifiers.BuiltIn(Item("h", ItemBaseType.Helm, Rarity.Common))[0]);
        Assert.EndsWith(" DEFENCE", helm);
        Assert.DoesNotContain("%", helm);
        Assert.EndsWith(" CRITICAL CHANCE", ItemModifiers.Line(ItemModifiers.BuiltIn(Item("bow", ItemBaseType.Weapon, Rarity.Common, family: 1))[0]));
        Assert.Empty(ItemModifiers.BuiltIn(Item("mat", ItemBaseType.Material, Rarity.Common)));
    }

    [Fact]
    public void test_the_prefix_shows_both_sides_of_its_trade()
    {
        // HEAVY: "+12% ATTACK / -6% ATTACK SPEED" in the brief's words — here DAMAGE up, SKILL RATE down.
        var heavy = Item("heavy", ItemBaseType.Weapon, Rarity.Rare, level: 20, prefix: GearTrait.Heavy);
        var trade = ItemModifiers.Prefix(heavy);
        Assert.Equal(2, trade.Count);
        Assert.Contains(trade, m => m.Stat == AffixStat.Damage && m.Magnitude > 0f && !m.IsDrawback);
        Assert.Contains(trade, m => m.Stat == AffixStat.SkillRate && m.IsDrawback);
        Assert.Equal("-20% SKILL RATE", ItemModifiers.Line(trade.First(m => m.IsDrawback)));
        Assert.All(trade, m => Assert.Equal(ModifierLayer.Prefix, m.Layer));
        Assert.All(trade, m => Assert.Matches(LineShape, ItemModifiers.Line(m)));

        // Every trait with a drawback prints it; a no-downside trait prints no cost line.
        foreach (var trait in Enum.GetValues<GearTrait>())
        {
            var item = Item($"t_{trait}", ItemBaseType.Charm, Rarity.Rare, level: 20, prefix: trait);
            var mods = GearTraits.ModsFor(trait, Rarity.Rare, 20);
            var hasDrawback = mods.Damage < 1f || mods.Health < 1f || mods.Haul < 1f || mods.SkillRate < 1f;
            var lines = ItemModifiers.Prefix(item);
            Assert.NotEmpty(lines);
            Assert.Equal(hasDrawback, lines.Any(m => m.IsDrawback));
            Assert.Contains(lines, m => !m.IsDrawback);
        }

        // The older joined form is the same modifiers, in the same words — one owner, two shapes. The
        // equality is a refactor tripwire (EffectOf is a forward); the literal is the pin: the join's
        // separator and the number-first order are chosen, not accidental.
        Assert.Equal(ItemModifiers.Join(trade), GearTraits.EffectOf(heavy));
        Assert.Equal($"{ItemModifiers.Line(trade[0])}  ·  -20% SKILL RATE", GearTraits.EffectOf(heavy));
        Assert.Equal("", GearTraits.EffectOf(Item("plain", ItemBaseType.Weapon, Rarity.Rare)));
        Assert.Empty(ItemModifiers.Prefix(Item("plain2", ItemBaseType.Weapon, Rarity.Rare)));
    }

    [Fact]
    public void test_affixes_and_gems_read_through_the_same_formatter()
    {
        // Refactor tripwires: the older helpers are forwards to ItemModifiers now, so these equalities
        // hold by construction. The numbers themselves are held by the literals in the tests above
        // and by gem_craft_test; these notice a helper that stops forwarding.
        var item = Item("aff", ItemBaseType.Ring, Rarity.Legendary, level: 30);
        var affixes = ItemModifiers.Affixes(item);
        Assert.Equal(4, affixes.Count);
        foreach (var (a, m) in ItemAffixes.Of(item).Zip(affixes))
        {
            Assert.Equal(ModifierLayer.Affix, m.Layer);
            Assert.Equal(a.Stat, m.Stat);
            Assert.Equal(ItemAffixes.Describe(a), ItemModifiers.Line(m));
            Assert.Equal(ItemAffixes.GrantLabel(a.Stat, a.Magnitude), ItemModifiers.Value(m));
            Assert.Equal(ItemAffixes.GrantLabelPrecise(a.Stat, a.Magnitude), ItemModifiers.Value(m, precise: true));
        }

        for (var i = 0; i < 12; i++)
        {
            var gem = new ItemInstance { InstanceId = $"gem{i}", BaseType = ItemBaseType.Gem, Rarity = Rarity.Rare, SellValue = 30, ItemLevel = 4 };
            var m = ItemModifiers.Gem(gem);
            Assert.Equal(ModifierLayer.Gem, m.Layer);
            Assert.Equal(GemCraft.StatOf(gem), m.Stat);
            Assert.Equal(GemCraft.Grant(gem), ItemModifiers.Line(m));
        }

        // Set gems ride the item's own list.
        var host = Item("host", ItemBaseType.Chest, Rarity.Epic, level: 20);
        var g1 = new ItemInstance { InstanceId = "set1", BaseType = ItemBaseType.Gem, Rarity = Rarity.Rare, SellValue = 30, ItemLevel = 4 };
        var socketed = GemCraft.Socket(host, g1).Product!;
        Assert.Single(ItemModifiers.Gems(socketed));
        Assert.Equal(GemCraft.Grant(g1), ItemModifiers.Line(ItemModifiers.Gems(socketed)[0]));
    }

    [Fact]
    public void test_a_compound_tradeoff_item_reads_every_layer_and_names_every_number()
    {
        // A deep Legendary HEAVY blade with a gem set and a keystone-combo enchant: the item the brief
        // calls "multiple modifiers, tradeoff prefix, enchanted, longer label" in one.
        var blade = Item("hero", ItemBaseType.Weapon, Rarity.Legendary, level: 30,
                         prefix: GearTrait.Heavy, family: 0, enchant: EnchantKind.Fervour);
        var gem = new ItemInstance { InstanceId = "hero_gem", BaseType = ItemBaseType.Gem, Rarity = Rarity.Rare, SellValue = 30, ItemLevel = 6 };
        blade = GemCraft.Socket(blade, gem).Product!;

        var all = ItemModifiers.Of(blade);
        Assert.Equal(1 + 4 + 2 + 1, all.Count);
        Assert.Equal(ModifierLayer.BuiltIn, all[0].Layer);
        Assert.Equal(ModifierLayer.Gem, all[^1].Layer);
        Assert.Equal(new[] { ModifierLayer.BuiltIn, ModifierLayer.Affix, ModifierLayer.Prefix, ModifierLayer.Gem },
                     all.Select(m => m.Layer).Distinct());
        Assert.Contains(all, m => m.IsDrawback);   // the trade's cost is on the card

        foreach (var m in all)
        {
            var line = ItemModifiers.Line(m);
            Assert.Matches(LineShape, line);
            Assert.EndsWith(" " + ItemModifiers.Word(m.Stat), line);
        }

        // The enchant says what its number is of — not "BLOODLUST +50%" — and the number is what the
        // item is WORTH over the keystone alone, not the coefficient it adds to the keystone's slope.
        var ench = Enchantments.Of(blade)!;
        Assert.Equal(EnchantKind.Fervour, ench.Kind);
        Assert.Equal("BLOODLUST: UP TO +28% MORE DAMAGE AT LOW HEALTH", ench.Blurb);
    }

    [Fact]
    public void test_the_enchant_sentences_say_what_their_number_is_of()
    {
        string At(EnchantKind k, Rarity r = Rarity.Legendary) => new Enchantment(k, Enchantments.MagnitudeFor(k, r)).Blurb;

        // Pinned exactly: these are the words a player reads, and the test exists to notice a fragment
        // creeping back. Each names the mechanic the sim's own rule moves, and prints THE FIGURE THE
        // FIGHT READS (see Enchantment.Blurb): VENOM's share is floored by the sim at 50%, so a Rare
        // (magnitude 0.27) and an Epic (0.38) both say 50% and only a Legendary (0.62) says more;
        // FERVOUR's +0.5 on BLOODLUST's 0.8 slope is worth +28% over the keystone alone, not "+50%".
        Assert.Equal("ON WAVE CLEAR: 38% CHANCE OF A SPARE CORE", At(EnchantKind.Harvest));
        Assert.Equal("SKILL HITS POISON FOR 62% OF THE HIT", At(EnchantKind.Venom));
        Assert.Equal("SKILL HITS POISON FOR 50% OF THE HIT", At(EnchantKind.Venom, Rarity.Rare));
        Assert.Equal("SKILL HITS POISON FOR 50% OF THE HIT", At(EnchantKind.Venom, Rarity.Epic));
        Assert.Equal("BELOW A THIRD HEALTH: +76% LOOT", At(EnchantKind.Desperation));
        Assert.Equal("BLOODLUST: UP TO +28% MORE DAMAGE AT LOW HEALTH", At(EnchantKind.Fervour));
        Assert.Equal("BLOODLUST: UP TO +10% MORE DAMAGE AT LOW HEALTH", At(EnchantKind.Fervour, Rarity.Rare));
        Assert.Equal("ZEAL: UP TO +28% MORE DAMAGE AT FULL HEALTH", At(EnchantKind.Bulwark));
        // Every hit while ECHO is socketed, the basic swing included — the multiplier carries no skill gate.
        Assert.Equal("WITH ECHO: +30% DAMAGE", At(EnchantKind.Reverb));
        Assert.Equal("+10% DAMAGE PER SWORN VOW", At(EnchantKind.Tithe));
        Assert.Equal("VOLLEY FIRES ONE MORE TIME", At(EnchantKind.Overdraw));
        Assert.Equal("DRAIN HEALS TWICE AS MUCH, UP TO 60% HEALTH A WAVE", At(EnchantKind.Siphon));

        // The magnitude that reaches the eye is the one the sim reads: a Rare and a Legendary differ.
        Assert.NotEqual(At(EnchantKind.Tithe, Rarity.Rare), At(EnchantKind.Tithe));

        // And the figures are the sim's own, read from its constants rather than retyped here.
        var fervour = Enchantments.MagnitudeFor(EnchantKind.Fervour, Rarity.Legendary);
        Assert.Equal(28, (int)MathF.Round(fervour / (1f + IdleXIdle.Core.Builds.SoloBattle.BloodlustScale) * 100f));
        Assert.Equal(0.5f, IdleXIdle.Core.Builds.SoloBattle.VenomBasePoison);
    }

    [Fact]
    public void test_no_live_numeric_item_line_is_a_slot_or_shape_word_beside_a_number()
    {
        // THE CATALOGUE GUARD. Every wearable base type, every weapon family, every rarity, every
        // prefix its slot can roll, across a spread of ids and item levels — every line of every layer
        // must be a signed figure followed by a STAT word, and never a slot or family word.
        // WHAT IT GUARDS: the formatter, over the whole catalogue — every modifier a player can be
        // dealt is typed, prints in its stat's unit and ends in a stat word. WHAT IT CANNOT SEE: a
        // screen that composes a label of its own beside ItemModifiers.Value (the original bug was
        // exactly that, in ItemTooltip). The screens are held by the p17 captures at 100/125/150 and by
        // the rule that a screen prints Word beside Value or Line, never a word of its own.
        var statWords = Enum.GetValues<AffixStat>().Select(ItemModifiers.Word).ToHashSet(StringComparer.Ordinal);
        var forbidden = SlotAndShapeWords().ToHashSet(StringComparer.Ordinal);
        Assert.Empty(statWords.Intersect(forbidden));

        var lines = 0;
        foreach (var type in Wearables)
        foreach (var rarity in Enum.GetValues<Rarity>())
        foreach (var level in new[] { 1, 20, 60 })
        foreach (var prefix in new GearTrait?[] { null }.Concat(GearTraits.PoolFor(Gear.SlotFor(type)!.Value).Cast<GearTrait?>()))
        for (var id = 0; id < 6; id++)
        {
            var family = type == ItemBaseType.Weapon ? id % ItemNaming.WeaponFamilies.Length : (int?)null;
            var item = Item($"sweep_{type}_{rarity}_{level}_{prefix}_{id}", type, rarity, level, prefix, family);
            foreach (var m in ItemModifiers.Of(item))
            {
                var line = ItemModifiers.Line(m);
                lines++;
                Assert.True(LineShape.IsMatch(line), $"{item.InstanceId}: \"{line}\" is not a signed figure and a stat");
                var word = line[(line.IndexOf(' ') + 1)..];
                Assert.True(statWords.Contains(word), $"{item.InstanceId}: \"{line}\" names \"{word}\", which is not a stat");
                Assert.False(forbidden.Contains(word), $"{item.InstanceId}: \"{line}\" names the slot or the shape, not the stat");
                // The number half alone is never a line — a screen that prints it must print the word beside it.
                Assert.NotEqual(ItemModifiers.Value(m), line);
            }
        }
        Assert.True(lines > 5_000, $"the sweep read only {lines} lines — it is not sweeping the catalogue");
    }
}
