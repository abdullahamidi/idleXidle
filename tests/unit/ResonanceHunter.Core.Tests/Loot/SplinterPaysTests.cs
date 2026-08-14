using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Loot;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Loot;

/// <summary>
/// SPLINTER — "on kill, richer loot" — must end somewhere a player can see.
/// </summary>
/// <remarks>
/// <para>
/// It did not. Its only effect in the whole simulation was <c>bonus?.AddQuality(0.15f)</c>, which flowed
/// into <c>Haul.Quality</c>, was carried correctly to the end of the descent, and was then read by
/// NOTHING — the field had no consumer anywhere in the solution. Two code comments and a design doc all
/// described a payout that did not exist, and the audit that eventually found it noted the Rarity half
/// of the same channel had been fixed earlier by routing AROUND it rather than through it.
/// </para>
/// <para>
/// The cost of that was not abstract: REAPER is a keystone that charges -25% skill rate to grant
/// SPLINTER, so it was a socket you paid for and got nothing from. The Splinter weapon enchant is one of
/// three in the weapon pool, including a rarity-scaled magnitude that was never consulted. And THE
/// QUIVER grants it outright.
/// </para>
/// <para>
/// These tests walk the whole chain, because every individual link was already correct.
/// </para>
/// </remarks>
public class SplinterPaysTests
{
    private readonly ITestOutputHelper _out;
    public SplinterPaysTests(ITestOutputHelper o) => _out = o;

    private static Build BuildWith(params BuildTrigger[] triggers)
    {
        var b = new Build
        {
            PassiveMods = BuildMods.None,
            Shape = SkillShape.None,
            ExtraTriggers = new HashSet<BuildTrigger>(triggers),
        };
        foreach (var f in new[] { Form.Strike, Form.Projectile })
            b.Weave(new EquippedSkill(
                new WovenAbility { Name = f.ToString(), Source = Source.Nature, Form = f },
                FormBehaviour.BaseCooldownMs(f)));
        return b;
    }

    private static SoloExpedition Run(Build build, int seed)
    {
        var champ = new Champion { MaxHealth = 5_000_000, Health = 5_000_000 };
        return new SoloExpedition(build, champ, new Hunter(), 1f, 0f,
                                  ExpeditionTuning.Default, enemySource: null, rng: new Random(seed))
        { RegionId = "verdant_hollow", RunIndex = 0 };
    }

    [Fact]
    public void test_splinter_raises_the_quality_a_descent_carries_out()
    {
        // The first link, and the only one that was ever really in doubt: does killing with the trigger
        // move the number at all? Enemy health is 1 and damage 0, so every wave clears and the only
        // difference between the two runs is the trigger.
        var withIt = Run(BuildWith(BuildTrigger.Splinter), 5);
        var without = Run(BuildWith(), 5);
        for (var w = 0; w < 12; w++) { withIt.PushWave(); without.PushWave(); }

        _out.WriteLine($"carried quality — with SPLINTER {withIt.Carried.Quality:0.000} · without {without.Carried.Quality:0.000}");
        Assert.True(withIt.Carried.Quality > without.Carried.Quality,
                    $"SPLINTER must raise the descent's carried quality — {withIt.Carried.Quality} vs {without.Carried.Quality}");
    }

    [Fact]
    public void test_a_chest_remembers_the_run_that_earned_it()
    {
        var neutral = Chests.RollDrop(tier: 5, element: null, rng: new Random(3));
        var earned = Chests.RollDrop(tier: 5, element: null, rng: new Random(3), runTilt: 1.9f);

        Assert.Equal(1f, neutral.RunTilt);
        Assert.Equal(1.9f, earned.RunTilt);
        // The tilt must not leak into the GRADE — grade is rolled by tier, and quality buys rarer items
        // rather than a better box. Same seed, same tier, same grade.
        Assert.Equal(neutral.Rarity, earned.Rarity);
    }

    [Fact]
    public void test_a_chest_that_remembers_a_good_run_rolls_rarer_items()
    {
        // The last link. Averaged over many opens because a single roll proves nothing about a tilt —
        // and measured as MEAN RARITY rather than "did a Legendary appear", which is the same mistake
        // as testing a probability with one sample.
        const int opens = 400;
        var flat = MeanRarity(1f, opens);
        var earned = MeanRarity(2.5f, opens);
        _out.WriteLine($"mean rarity over {opens} opens — neutral chest {flat:0.000} · earned chest {earned:0.000}");

        Assert.True(earned > flat,
                    $"a chest carrying a run tilt must roll rarer on average — {earned:0.000} vs {flat:0.000}");
    }

    [Fact]
    public void test_a_chest_from_before_the_tilt_existed_opens_exactly_as_it_used_to()
    {
        // The migration case, and the reason the neutral value is 1 and not 0: a chest deserialised
        // without the field must roll identically to a neutral one, not be silently made worthless.
        var old = new Chest { Rarity = Rarity.Rare, Tier = 5 };
        Assert.Equal(1f, old.RunTilt);

        Assert.Equal(Open(new Chest { Rarity = Rarity.Rare, Tier = 5, RunTilt = 1f }, seed: 21),
                     Open(old, seed: 21));
    }

    private static float MeanRarity(float tilt, int opens)
    {
        var total = 0f;
        for (var i = 0; i < opens; i++)
            total += Open(new Chest { Rarity = Rarity.Rare, Tier = 8, RunTilt = tilt }, seed: 1000 + i);
        return total / opens;
    }

    /// <summary>Mean rarity of the items one open produced, as a number.</summary>
    private static float Open(Chest chest, int seed)
    {
        var reward = Chests.Open(chest, new Random(seed), LootTuning.Default, rarityBonus: 1f);
        return reward.Items.Count == 0 ? 0f : (float)reward.Items.Average(i => (int)i.Rarity);
    }
}
