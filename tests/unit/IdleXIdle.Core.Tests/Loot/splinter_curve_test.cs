using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using IdleXIdle.Core.Persistence;
using IdleXIdle.Core.Sources;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Loot;

/// <summary>
/// SPLINTER PAYS THE CURVE IT PROMISES — the authored rarity magnitude, and no second one.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SplinterPaysTests"/> proved the CHANNEL: quality leaves the fight, rides the chest as
/// <c>RunTilt</c> and buys rarer items. What it could not see is that the fight always sent the same
/// number. <c>Enchantments.MagnitudeFor</c> scaled SPLINTER by rarity, the item card was built from
/// that magnitude, and the wave paid a literal 0.15 — because <see cref="Build.Triggers"/> turns a worn
/// enchant into a BOOLEAN, and a boolean is all the payout could read. A Legendary SPLINTER (0.60)
/// delivered the bare keystone's 0.15: four rarity tiers of one enchant, one behaviour.
/// </para>
/// <para>
/// So these tests hold the two halves together at every live rarity: what the curve promises, what the
/// wave pays, and what the card says — one number, quoted three times. The FIXED half is kept apart
/// from the rarity half on purpose: a bare trigger (the REAPER keystone, THE QUIVER's grant) has no
/// item to set its strength and keeps <see cref="SoloBattle.SplinterBaseQuality"/>, and an item can
/// only ever raise that floor, never add to it.
/// </para>
/// <para>
/// WHAT "ONCE" MEANS HERE: the payout sits in the wave's clear handler, beside HARVEST's spare-core
/// roll — one payment per wave cleared, however many creatures stood in it. That is the shipped site
/// and this pass does not move it; the tests state it as it is.
/// </para>
/// </remarks>
public class splinter_curve_test
{
    private readonly ITestOutputHelper _out;
    public splinter_curve_test(ITestOutputHelper o) => _out = o;

    /// <summary>Every rarity that can carry an enchant at all — the live tiers, lowest to highest.</summary>
    private static readonly Rarity[] LiveRarities =
        Enum.GetValues<Rarity>().Where(r => r >= Enchantments.MinimumRarity).OrderBy(r => r).ToArray();

    private static ItemInstance SplinterWeapon(Rarity rarity, string id = "splinter_blade") => new()
    {
        InstanceId = id, BaseType = ItemBaseType.Weapon, Rarity = rarity,
        SellValue = 100, ItemLevel = 10, EnchantOverride = EnchantKind.Splinter,
    };

    private static Build Woven(params Keystone[] keystones)
    {
        var build = new Build { PassiveMods = BuildMods.None, Shape = SkillShape.None };
        build.Equip(TestBuilds.Skill("hammer_blow", Source.Nature));
        foreach (var k in keystones) build.Take(k);
        return build;
    }

    /// <summary>What one cleared wave actually paid into the haul's quality — the production number.</summary>
    private static float PaidByOneWave(Build build, Hunter hunter, int creatures = 1, int seed = 11)
    {
        var bonus = new WaveBonus();
        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var wave = Enumerable.Range(0, creatures)
                             .Select(_ => new WaveCreature { MaxHealth = 1f, Health = 1f, Damage = 0f })
                             .ToList();
        var (outcome, _) = SoloBattle.ResolveWave(champ, build, hunter, wave,
                                                  enemyIntervalMs: 5_000, ExpeditionTuning.Default,
                                                  new Random(seed), bonus);
        Assert.Equal(WaveOutcome.Cleared, outcome);
        return bonus.Quality;
    }

    // ── THE CONTRACT, AT EVERY LIVE RARITY ───────────────────────────────────────────────────────

    [Fact]
    public void test_splinter_pays_its_authored_rarity_magnitude_at_every_live_rarity()
    {
        Assert.Equal(new[] { Rarity.Rare, Rarity.Epic, Rarity.Legendary }, LiveRarities);

        var paid = new List<float>();
        foreach (var rarity in LiveRarities)
        {
            var promised = Enchantments.MagnitudeFor(EnchantKind.Splinter, rarity);
            var hunter = new Hunter();
            hunter.Equip(SplinterWeapon(rarity));
            Assert.Equal(EnchantKind.Splinter, Enchantments.Of(hunter.Worn(GearSlot.Weapon))!.Kind);

            var actual = PaidByOneWave(Woven(), hunter);
            _out.WriteLine($"{rarity,-10} promised {promised:0.000}  paid {actual:0.000}");
            Assert.Equal(promised, actual, 5);
            paid.Add(actual);
        }

        // The curve REACHES the payout: three tiers, three different numbers, each above the floor a
        // bare trigger keeps. Before this pass all three paid 0.15.
        Assert.Equal(paid.Count, paid.Distinct().Count());
        Assert.Equal(paid.OrderBy(v => v), paid);
        Assert.All(paid, v => Assert.True(v > SoloBattle.SplinterBaseQuality, $"{v} is not above the bare floor"));
    }

    [Fact]
    public void test_the_bare_trigger_keeps_its_base_and_an_item_only_raises_it()
    {
        // THE FIXED HALF. REAPER charges -25% skill rate to grant SPLINTER and wears no item, so the
        // floor is the whole of what it pays — losing it would make the keystone free of charge in the
        // other direction, and doubling it would pay the keystone twice.
        var reaper = Keystones.ById("reaper")!;
        Assert.Contains(BuildTrigger.Splinter, reaper.Grants);

        var bare = PaidByOneWave(Woven(reaper), new Hunter());
        Assert.Equal(SoloBattle.SplinterBaseQuality, bare, 5);

        // THE RARITY HALF, on the same keystone build: the item raises the floor to its own magnitude —
        // it does not add to it, and the keystone does not add to the item.
        var hunter = new Hunter();
        hunter.Equip(SplinterWeapon(Rarity.Legendary));
        var legendary = Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Legendary);

        var both = PaidByOneWave(Woven(reaper), hunter);
        Assert.Equal(legendary, both, 5);
        Assert.NotEqual(SoloBattle.SplinterBaseQuality + legendary, both);

        // And the item alone pays exactly what the item and the keystone together pay.
        Assert.Equal(both, PaidByOneWave(Woven(), hunter), 5);
    }

    [Fact]
    public void test_an_unequipped_splinter_pays_nothing()
    {
        var hunter = new Hunter();
        var inTheBag = SplinterWeapon(Rarity.Legendary);          // held, never worn
        Assert.Null(hunter.Worn(GearSlot.Weapon));
        Assert.DoesNotContain(BuildTrigger.Splinter, Woven().Triggers(hunter));

        Assert.Equal(0f, PaidByOneWave(Woven(), hunter));
        Assert.Equal(EnchantKind.Splinter, Enchantments.Of(inTheBag)!.Kind);   // the item is real; the slot is empty
    }

    [Fact]
    public void test_a_worn_splinter_pays_once_per_cleared_wave_and_never_twice()
    {
        var hunter = new Hunter();
        hunter.Equip(SplinterWeapon(Rarity.Epic));
        var magnitude = Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Epic);

        // ONE payment per cleared wave — the shipped site is the wave's clear, beside HARVEST's roll —
        // so a crowded wave pays the same as a lone one, and the figure is the magnitude, not a multiple.
        foreach (var creatures in new[] { 1, 4, 9 })
            Assert.Equal(magnitude, PaidByOneWave(Woven(), hunter, creatures), 5);

        // Two sources of the same trigger is still one payment (Build.Triggers is a set), and two waves
        // are two payments — the bonus accumulates across a descent, which is what the chest reads.
        var bonus = new WaveBonus();
        var champ = new Champion { MaxHealth = 100_000, Health = 100_000 };
        var build = Woven(Keystones.ById("reaper")!);
        for (var w = 0; w < 3; w++)
            SoloBattle.ResolveWave(champ, build, hunter, 1f, 0f, 5_000, ExpeditionTuning.Default, new Random(4), bonus);
        Assert.Equal(3f * magnitude, bonus.Quality, 5);
    }

    [Fact]
    public void test_rebuilding_the_build_does_not_accumulate_the_bonus()
    {
        // The magnitude is read off the worn items every time and never stored, so composing the build
        // again — the thing the loadout screen does on every edit — cannot make the enchant stronger.
        var hunter = new Hunter();
        hunter.Equip(SplinterWeapon(Rarity.Legendary));
        var magnitude = Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Legendary);

        for (var rebuild = 0; rebuild < 5; rebuild++)
        {
            var build = Woven(Keystones.ById("reaper")!);
            for (var t = 0; t < 3; t++) build.Triggers(hunter);          // asked repeatedly, as a screen asks
            Assert.Equal(magnitude, PaidByOneWave(build, hunter), 5);
        }

        // Nor does wearing it, taking it off and wearing it again.
        hunter.Unequip(GearSlot.Weapon);
        Assert.Equal(0f, PaidByOneWave(Woven(), hunter));
        hunter.Equip(SplinterWeapon(Rarity.Legendary));
        Assert.Equal(magnitude, PaidByOneWave(Woven(), hunter), 5);
    }

    // ── THE CARD SAYS THE FIGURE THE FIGHT PAYS ──────────────────────────────────────────────────

    [Fact]
    public void test_the_item_card_promises_the_figure_the_fight_pays()
    {
        foreach (var rarity in LiveRarities)
        {
            var weapon = SplinterWeapon(rarity);
            var ench = Enchantments.Of(weapon)!;
            var hunter = new Hunter();
            hunter.Equip(weapon);
            var paid = PaidByOneWave(Woven(), hunter);

            // The blurb is numeric now — a rarity that changes the payout must change the sentence, or
            // the card is decoration. ("ON KILL: RICHER LOOT" was the same words at every tier.)
            Assert.True(ench.BlurbIsNumeric, $"{rarity}: SPLINTER's sentence names no figure");
            var shown = int.Parse(Regex.Match(ench.Blurb, @"\d+").Value, CultureInfo.InvariantCulture);
            Assert.Equal((int)MathF.Round(paid * 100f), shown);
            _out.WriteLine($"{rarity,-10} card \"{ench.Blurb}\"  pays {paid:0.000}");

            // And the row a screen draws carries that same sentence, unaltered.
            var row = Assert.Single(ItemPresentation.Rows(weapon).Where(r => r.Kind == ItemRowKind.Enchant));
            Assert.Equal(ench.Blurb, row.Note);
        }
    }

    // ── THE LIVE CONSUMER, AND A RELOAD ──────────────────────────────────────────────────────────

    [Fact]
    public void test_a_rarer_splinter_carries_more_quality_out_of_a_descent()
    {
        // The consumer end, through the shipped descent (the same wave sim the offline catch-up drives):
        // a rarer weapon must leave the run with more quality, which is what the chest keeps as RunTilt.
        static float Carried(Rarity? rarity)
        {
            var hunter = new Hunter();
            if (rarity is { } r) hunter.Equip(SplinterWeapon(r));
            var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
            var run = new SoloExpedition(Woven(), champ, hunter, 1f, 0f, ExpeditionTuning.Default, rng: new Random(5))
            { RegionId = "verdant_hollow", RunIndex = 0 };
            for (var w = 0; w < 8; w++) run.PushWave();
            return run.Carried.Quality;
        }

        var none = Carried(null);
        var rare = Carried(Rarity.Rare);
        var legendary = Carried(Rarity.Legendary);
        _out.WriteLine($"carried quality — none {none:0.000} · Rare {rare:0.000} · Legendary {legendary:0.000}");

        Assert.True(rare > none, $"a Rare SPLINTER carried no more than none — {rare:0.000} vs {none:0.000}");
        Assert.True(legendary > rare, $"the rarity curve does not reach the descent — Legendary {legendary:0.000} vs Rare {rare:0.000}");

        // ...and the chest keeps it. (Chests.Open's use of the tilt is SplinterPaysTests' ground.)
        Assert.Equal(legendary, Chests.RollDrop(tier: 5, element: null, rng: new Random(3), runTilt: legendary).RunTilt, 5);
    }

    [Fact]
    public void test_a_saved_and_reloaded_splinter_pays_exactly_what_it_paid_before()
    {
        var hunter = new Hunter();
        var weapon = SplinterWeapon(Rarity.Epic, "saved_blade");
        hunter.Equip(weapon);
        var before = PaidByOneWave(Woven(), hunter);

        // The real round trip: capture, serialise, deserialise, restore the bag, wear the same id again.
        var save = SaveSystem.Capture(hunter, new[] { weapon }, nowMs: 1_700_000_000_000L);
        var json = SaveSystem.Serialize(save);
        var loaded = SaveSystem.Deserialize(json, 1_700_000_000_000L).Save!;
        var restored = SaveSystem.RestoreInventory(loaded)
                                 .Single(i => i.InstanceId == loaded.WornWeaponId);

        var reloaded = new Hunter();
        reloaded.Equip(restored);
        var after = PaidByOneWave(Woven(), reloaded);

        // The magnitude is never serialised — it is recomputed from the kind and the rarity, so the
        // reload preserves the semantics rather than a stored number that could drift from the curve.
        var ench = Enchantments.Of(restored)!;
        Assert.Equal(EnchantKind.Splinter, ench.Kind);
        Assert.Equal(Rarity.Epic, restored.Rarity);
        Assert.Equal(Enchantments.MagnitudeFor(EnchantKind.Splinter, Rarity.Epic), ench.Magnitude, 5);
        Assert.Equal(before, after, 5);
    }
}
