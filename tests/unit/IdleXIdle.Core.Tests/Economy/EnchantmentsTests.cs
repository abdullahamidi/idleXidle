using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Loot;
using Xunit;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// Enchantments: effects that change what HAPPENS, which the data model could not previously express.
/// </summary>
/// <remarks>
/// This file owns the DATA MODEL — which item carries which enchant, its magnitude curve, its blurb. The
/// triggers firing INSIDE a fight are proven against the live single-champion engine in
/// <c>Builds/TriggerLivenessTests</c>, <c>Builds/SoloBattleTests</c> and <c>Builds/SoloExpeditionTests</c>
/// (UNDYING, HARVEST, SPLINTER, DESPERATION and every Form-combo), so they are not re-proven here through
/// the retired squad engine.
/// </remarks>
public class EnchantmentsTests
{
    private static ItemInstance Item(string id, ItemBaseType type, Rarity rarity = Rarity.Epic)
        => new() { InstanceId = id, BaseType = type, Rarity = rarity, SellValue = 82 };

    private static ItemInstance WeaponWith(EnchantKind kind, Rarity rarity = Rarity.Epic)
        => Enumerable.Range(0, 800)
            .Select(i => Item($"w_{i}", ItemBaseType.Weapon, rarity))
            .First(it => Enchantments.Of(it)?.Kind == kind);

    // ── The data model ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_an_item_can_finally_carry_an_element()
    {
        // ItemInstance had five fields and no room for an element — a grep for "Element" returned zero
        // hits in the whole repo. It reuses Source, so an item's fire is a creature's fire.
        var item = Item("w1", ItemBaseType.Weapon) with { Element = Source.Shadow };
        Assert.Equal(Source.Shadow, item.Element);
    }

    [Fact]
    public void test_an_item_without_an_element_has_no_element()
    {
        // Nullable, not defaulted: `?? 0` would make every pre-element save's loot Body-attuned.
        Assert.Null(Item("w1", ItemBaseType.Weapon).Element);
    }

    [Fact]
    public void test_an_enchantment_is_stable_for_a_given_item()
    {
        var first = Enchantments.Of(Item("weapon_xyz", ItemBaseType.Weapon));
        for (var i = 0; i < 30; i++)
            Assert.Equal(first, Enchantments.Of(Item("weapon_xyz", ItemBaseType.Weapon)));
    }

    [Fact]
    public void test_the_enchantment_does_not_correlate_with_the_trait()
    {
        // Sharing one hash would lock the two axes together forever: every HEAVY weapon in the game
        // would carry the same enchantment, and two axes would collapse back into one.
        var pairs = new HashSet<(GearTrait, EnchantKind)>();
        for (var i = 0; i < 400; i++)
        {
            // Minted the way loot mints: prefix from the RNG, enchantment from the id — two dice.
            var it = Item($"w_{i}", ItemBaseType.Weapon) with
            {
                TraitOverride = GearTraits.RollPrefix(ItemBaseType.Weapon, new Random(i)),
            };
            if (GearTraits.TraitOf(it) is { } t && Enchantments.Of(it) is { } e)
                pairs.Add((t, e.Kind));
        }

        // 4 traits x 3 weapon enchants = 12 combinations. Correlated hashes would yield at most 4.
        Assert.True(pairs.Count > 8, $"only {pairs.Count} trait/enchant pairings exist — the axes are correlated");
    }

    [Fact]
    public void test_commons_and_uncommons_are_never_enchanted()
    {
        for (var i = 0; i < 50; i++)
        {
            Assert.Null(Enchantments.Of(Item($"w_{i}", ItemBaseType.Weapon, Rarity.Common)));
            Assert.Null(Enchantments.Of(Item($"w_{i}", ItemBaseType.Weapon, Rarity.Uncommon)));
        }
        Assert.NotNull(Enchantments.Of(Item("w_1", ItemBaseType.Weapon, Rarity.Rare)));
    }

    [Fact]
    public void test_materials_and_cores_are_never_enchanted()
    {
        Assert.Null(Enchantments.Of(Item("m1", ItemBaseType.Material, Rarity.Legendary)));
        Assert.Null(Enchantments.Of(Item("c1", ItemBaseType.CreatureCore, Rarity.Legendary)));
        Assert.Null(Enchantments.Of(null));
    }

    [Fact]
    public void test_no_chance_based_enchantment_ever_reaches_certainty()
    {
        // A trigger that always fires is not a trigger, it is a stat — which is the thing this layer
        // exists to not be. The rarity curve runs to 7.00, so an undamped magnitude would be a 700%
        // chance.
        foreach (var kind in new[] { EnchantKind.Splinter, EnchantKind.Harvest, EnchantKind.Venom })
        {
            var m = Enchantments.MagnitudeFor(kind, Rarity.Legendary);
            Assert.InRange(m, 0.01f, 0.99f);
        }
    }

    [Fact]
    public void test_rarity_makes_an_enchantment_stronger()
    {
        Assert.True(Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Legendary)
                    > Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Rare));
    }

    [Fact]
    public void test_every_form_has_a_combo_enchantment()
    {
        // The combo axis is complete only if committing to ANY Form has an item that rewards it. A Form
        // with no combo enchant is a build path with nothing to chase — the gap this feature closed for
        // Strike, Trap and Transformation.
        foreach (var form in System.Enum.GetValues<IdleXIdle.Core.Abilities.Form>())
            Assert.Contains(System.Enum.GetValues<EnchantKind>(),
                k => new Enchantment(k, 1f).NeedsForm == form);
    }

    [Fact]
    public void test_an_enchantment_pool_matches_its_slot()
    {
        foreach (var (type, slot) in new[]
                 {
                     (ItemBaseType.Weapon, GearSlot.Weapon),
                     (ItemBaseType.Charm, GearSlot.Charm),
                     (ItemBaseType.AbilityFocus, GearSlot.Focus),
                 })
        {
            for (var i = 0; i < 100; i++)
                if (Enchantments.Of(Item($"i_{i}", type)) is { } e)
                    Assert.Contains(e.Kind, Enchantments.PoolFor(slot));
        }
    }

    [Fact]
    public void test_magnitude_of_an_absent_enchantment_is_zero()
    {
        var worn = Enchantments.Worn(WeaponWith(EnchantKind.Splinter));
        Assert.Equal(0f, Enchantments.MagnitudeOf(worn, EnchantKind.Undying));
    }

    [Fact]
    public void test_every_enchantment_is_claimed_by_a_test_that_proves_it_does_something()
    {
        // The enchant-axis twin of Builds/TriggerLivenessTests, and it exists because that guard could
        // not cover this. It indexes BuildTrigger, and the four keystone/Vow combos deliberately have no
        // BuildTrigger — they are magnitudes read straight off the worn items, so they could have been
        // added, shipped and read by nothing with every existing test still green. That is exactly how
        // VENOM shipped dead: a correct catalogue entry, a correct blurb, and no sim.
        //
        // Hand-maintained, for the same reason its twin is: no reflection can ask "does the fight read
        // this?". Adding an EnchantKind fails this test, and the failure names what you must go prove.
        var proved = new HashSet<EnchantKind>
        {
            EnchantKind.Splinter,    // Builds/TriggerLivenessTests.test_splinter_pays_out_on_a_kill
            EnchantKind.Harvest,     // Builds/TriggerLivenessTests.test_harvest_pays_out_on_a_kill
            EnchantKind.Venom,       // Builds/TriggerLivenessTests.test_venom_actually_poisons
            EnchantKind.Desperation, // Builds/SoloExpeditionTests.test_desperation_swells_the_haul_at_low_health
            EnchantKind.Undying,     // Builds/TriggerLivenessTests.test_undying_buys_exactly_one_death
            EnchantKind.Overdraw,    // Builds/SoloBattleTests.test_overdraw_adds_a_projectile_cast
            EnchantKind.Linger,      // Builds/SoloBattleTests.test_linger_stretches_the_mark_window
            EnchantKind.Radiance,    // Builds/SoloBattleTests.test_radiance_makes_an_aura_tick_more
            EnchantKind.Execute,     // Builds/SoloBattleTests.test_execute_speeds_the_kill_of_a_weakened_enemy
            EnchantKind.Coiled,      // Builds/SoloBattleTests.test_coiled_fires_the_trap_more_often
            EnchantKind.Siphon,      // Builds/SoloBattleTests.test_siphon_deepens_the_transformation_leech
            EnchantKind.Fervour,     // Builds/SoloBattleTests.test_fervour_steepens_bloodlust_and_is_dead_without_it
            EnchantKind.Reverb,      // Builds/SoloBattleTests.test_reverb_sharpens_echo_and_is_dead_without_it
            EnchantKind.Bulwark,     // Builds/SoloBattleTests.test_bulwark_steepens_zeal_and_is_dead_without_it
            EnchantKind.Tithe,       // Builds/SoloBattleTests.test_tithe_pays_per_sworn_vow_and_is_dead_without_one
        };

        foreach (var k in System.Enum.GetValues<EnchantKind>())
            Assert.True(proved.Contains(k),
                $"{k} has no test proving the fight reads it. This is how VENOM shipped dead.");
    }

    [Fact]
    public void test_every_combo_enchantment_names_what_it_needs()
    {
        // A combo whose requirement is null reads to the player as an unconditional effect, because the
        // Forge only draws the "NEEDS … IN YOUR BUILD" line when there is a requirement to draw. An
        // enchantment that is dead without a partner and does not SAY so is worse than one that is
        // simply weak — the player equips it and concludes the game is broken.
        foreach (var kind in new[]
                 {
                     EnchantKind.Overdraw, EnchantKind.Linger, EnchantKind.Radiance, EnchantKind.Execute,
                     EnchantKind.Coiled, EnchantKind.Siphon, EnchantKind.Fervour, EnchantKind.Reverb,
                     EnchantKind.Bulwark, EnchantKind.Tithe,
                 })
        {
            var need = new Enchantment(kind, 1f).Needs;
            Assert.True(need is not null, $"{kind} is a combo and names no requirement");
            Assert.False(string.IsNullOrWhiteSpace(need!.Label), $"{kind}'s requirement has no label");
            Assert.True(need.Form is not null || need.Keystone is not null || need.AnyVow,
                        $"{kind} has a label but no actual condition — the Forge would always call it live");
        }
    }

    [Fact]
    public void test_a_requirement_is_met_only_by_the_axis_it_names()
    {
        // The rule the Forge draws its COMBOS / NEEDS line from, and the FITS badge on the loot grid.
        // It used to live inside a Draw method, where no test could reach it.
        var noForms = System.Array.Empty<IdleXIdle.Core.Abilities.Form>();
        var noTriggers = System.Array.Empty<IdleXIdle.Core.Builds.BuildTrigger>();

        var form = new Enchantment(EnchantKind.Overdraw, 1f).Needs!;
        Assert.True(form.MetBy(new[] { IdleXIdle.Core.Abilities.Form.Projectile }, noTriggers, 0));
        Assert.False(form.MetBy(new[] { IdleXIdle.Core.Abilities.Form.Strike }, noTriggers, 0));

        var keystone = new Enchantment(EnchantKind.Fervour, 1f).Needs!;
        Assert.True(keystone.MetBy(noForms, new[] { IdleXIdle.Core.Builds.BuildTrigger.Bloodlust }, 0));
        Assert.False(keystone.MetBy(noForms, new[] { IdleXIdle.Core.Builds.BuildTrigger.Echo }, 0));

        var vow = new Enchantment(EnchantKind.Tithe, 1f).Needs!;
        Assert.True(vow.MetBy(noForms, noTriggers, swornVows: 1));
        Assert.False(vow.MetBy(noForms, noTriggers, swornVows: 0));

        // AND THE CROSS-AXIS CHECK, which is the one worth having. A keystone combo must not read as
        // live because the player happens to run a Form, and a Form combo must not read as live because
        // they happen to have sworn a Vow. Each requirement answers its own axis and ignores the rest.
        Assert.False(keystone.MetBy(
            new[] { IdleXIdle.Core.Abilities.Form.Projectile,
                    IdleXIdle.Core.Abilities.Form.Strike }, noTriggers, swornVows: 4));
        Assert.False(vow.MetBy(
            new[] { IdleXIdle.Core.Abilities.Form.Projectile },
            new[] { IdleXIdle.Core.Builds.BuildTrigger.Bloodlust }, swornVows: 0));
    }

    [Fact]
    public void test_every_enchantment_blurb_fits_the_forge_row()
    {
        // The Forge gives the blurb a fixed column, x=344 to the panel edge: 21 characters. Right-
        // aligning it instead let a long name meet the blurb in the middle and render as one word
        // ("SPLINTERON A KILL"), which is why the column is fixed and this cap exists.
        foreach (var kind in System.Enum.GetValues<EnchantKind>())
        {
            var blurb = new Enchantment(kind, Enchantments.MagnitudeFor(kind, Rarity.Legendary)).Blurb;
            Assert.False(string.IsNullOrWhiteSpace(blurb));
            Assert.True(blurb.Length <= 21, $"{kind}'s blurb is {blurb.Length} chars — the Forge column fits 21: \"{blurb}\"");
        }
    }
}
