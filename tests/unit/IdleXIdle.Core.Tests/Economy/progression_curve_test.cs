using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Loot;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Economy;

/// <summary>
/// Does the player's power curve stay in step with the content curve?
/// </summary>
/// <remarks>
/// <para>
/// The playtest report was "gear power reached 90,000-something and hits land for 30–40k — excessive,
/// meaningless numbers". Big numbers are a symptom; the disease is two exponentials that do not match.
/// Enemy health compounds at 1.06 per wave. Player power compounds through a weapon multiplier that is
/// LINEAR in item level with no ceiling, on an upgrade the Forge documents as an "infinite sink". One of
/// those saturates and one does not, so past some depth the player stops being challenged and the only
/// thing that still grows is the size of the font needed to print the damage.
/// </para>
/// <para>
/// This file measures rather than argues. It prints the two curves side by side so a tuning pass has
/// numbers to aim at, and asserts the property that actually matters: <b>gear must not outrun the
/// content faster than the content grows.</b>
/// </para>
/// </remarks>
public class ProgressionCurveTest
{
    private readonly ITestOutputHelper _out;

    public ProgressionCurveTest(ITestOutputHelper output) => _out = output;

    private static readonly ExpeditionTuning T = ExpeditionTuning.Default;

    /// <summary>Enemy health at a wave, exactly as Game1 and WaveScaling compute it for region one.</summary>
    private static float EnemyHealth(int wave)
        => 110f * WaveScaling.EnemyScale(wave, T);

    /// <summary>
    /// A probe weapon. The InstanceId is FIXED so only the level varies.
    /// </summary>
    /// <remarks>
    /// It used to be $"probe_{rarity}_{itemLevel}", and that quietly ruined the comparison: affixes are
    /// rolled from the id, so every rung of the ladder was carrying a DIFFERENT set of rolls and the
    /// column read non-monotonic — iL1000 measured below iL200 purely on luck. A curve is only readable
    /// if the thing under test is the only thing changing.
    /// </remarks>
    private static ItemInstance Weapon(Rarity rarity, int itemLevel) => new()
    {
        InstanceId = "probe",
        BaseType = ItemBaseType.Weapon,
        Rarity = rarity,
        ItemLevel = itemLevel,
        SellValue = 100,
    };

    /// <summary>Damage a four-BLOW build (hammer_blow in every slot) lands per second with this weapon worn.</summary>
    private static float DamagePerSecond(ItemInstance? weapon)
    {
        var hunter = new Hunter();
        if (weapon is not null) hunter.Equip(weapon);

        var build = new Build();
        for (var i = 0; i < 4; i++)
            build.Equip(TestBuilds.Skill("hammer_blow", Source.Nature));

        // One unkillable target, so the window measures throughput rather than time-to-kill.
        const int windowMs = 30_000;
        var champ = new Champion { MaxHealth = 100_000_000, Health = 100_000_000 };
        var metrics = new WaveMetrics();
        SoloBattle.ResolveWave(
            champ, build, hunter,
            new[] { new WaveCreature { MaxHealth = 1e12f, Health = 1e12f, Damage = 0f } },
            enemyIntervalMs: 1_000_000,
            T with { TickCeilingMs = windowMs },
            new Random(4), metrics: metrics);

        return metrics.DeliveredDamage / (windowMs / 1000f);
    }

    [Fact]
    public void test_the_power_curve_and_the_content_curve_are_reported_together()
    {
        _out.WriteLine("CONTENT — enemy health by wave (region one, no corruption):");
        foreach (var wave in new[] { 1, 5, 10, 20, 30, 50, 75, 100 })
            _out.WriteLine($"   wave {wave,4}   {EnemyHealth(wave),14:N0} hp"
                           + (WaveScaling.IsBossWave(wave, T) ? "   (boss)" : ""));

        _out.WriteLine("");
        _out.WriteLine("POWER — one weapon, four BLOW skills, no tree and no training:");
        _out.WriteLine($"   {"weapon",-24} {"dmg/sec",12} {"weapon x",10} {"item power",12}");

        var rungs = new (string Label, ItemInstance? It)[]
        {
            ("(nothing)", null),
            ("Common iL1", Weapon(Rarity.Common, 1)),
            ("Rare iL1", Weapon(Rarity.Rare, 1)),
            ("Epic iL20", Weapon(Rarity.Epic, 20)),
            ("Legendary iL60", Weapon(Rarity.Legendary, 60)),
            ("Legendary iL200", Weapon(Rarity.Legendary, 200)),
            ("Legendary iL1000", Weapon(Rarity.Legendary, 1000)),
        };

        foreach (var (label, it) in rungs)
        {
            var dps = DamagePerSecond(it);
            var mult = Gear.WeaponDamageMultiplier(it);
            _out.WriteLine($"   {label,-24} {dps,12:N0} {mult,9:0.0}x");
        }
    }

    [Fact]
    public void test_refining_forever_does_not_multiply_power_forever()
    {
        // THE CLAIM UNDER TEST. Refine is documented as an infinite sink, and as an economy device that
        // is correct — an idle game needs a drain that never fills. But an infinite sink that pays
        // UNBOUNDED POWER is a different thing: it means the only ceiling on a player's damage is how
        // long they are willing to hold down a button, and every number on screen grows to match.
        //
        // The sink may stay infinite. The POWER it buys must not. Ten times the item level may not buy
        // anything close to ten times the weapon.
        var at60 = Gear.WeaponDamageMultiplier(Weapon(Rarity.Legendary, 60));
        var at600 = Gear.WeaponDamageMultiplier(Weapon(Rarity.Legendary, 600));

        _out.WriteLine($"Legendary weapon multiplier: iL60 = {at60:0.00}x, iL600 = {at600:0.00}x "
                       + $"({at600 / at60:0.00}x for ten times the levels)");

        Assert.True(at600 < at60 * 1.6f,
            $"ten times the item level bought {at600 / at60:0.0}x the weapon ({at60:0.0}x to {at600:0.0}x). "
            + "Item level is meant to refine a piece within its rarity tier, not to replace the tier "
            + "ladder with a grind.");
    }

    [Fact]
    public void test_rarity_still_beats_a_deeply_refined_lesser_item()
    {
        // The ladder the design states: "the ladder is still rarity-first, but the rungs have depth
        // now." If refining is unbounded that sentence stops being true — a Common ground high enough
        // overtakes a Legendary, and every rarity below the top becomes a placeholder for grinding.
        var legendaryFloor = Gear.WeaponDamageMultiplier(Weapon(Rarity.Legendary, 1));

        foreach (var lesser in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic })
        {
            var refined = Gear.WeaponDamageMultiplier(Weapon(lesser, 1000));
            _out.WriteLine($"   {lesser,-10} iL1000 = {refined,6:0.00}x   vs Legendary iL1 = {legendaryFloor:0.00}x");
            Assert.True(refined < legendaryFloor,
                $"a {lesser} refined to iL1000 reaches {refined:0.00}x, past a Legendary's floor of "
                + $"{legendaryFloor:0.00}x — rarity has stopped being the ladder.");
        }
    }
}
