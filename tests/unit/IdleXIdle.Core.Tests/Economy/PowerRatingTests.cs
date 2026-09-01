using System;
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
/// POWER must move the same way the character does.
/// </summary>
/// <remarks>
/// <para>
/// A player reported that wearing armour LOWERED their power. It did, and the armour was not the
/// problem — the number was. <c>PowerRating</c> counted hit size and ignored cadence, and half the
/// armour traits in the game buy cadence with hit size (SWIFT is x0.90 damage for x1.10 rate, FOCUSED
/// x0.85 for x1.20). So the two pieces that made the character strongest were the two the HUD ranked
/// LAST, and EQUIP BEST — which sorts by this number — would have handed back the weaker one.
/// </para>
/// <para>
/// These tests measure DEPTH, which is the thing power is a proxy for, and require the proxy to agree
/// with it. That is slower than asserting an arithmetic identity and it is the only version worth
/// having: the old formula was arithmetically self-consistent and still wrong about the game.
/// </para>
/// </remarks>
public class PowerRatingTests
{
    private readonly ITestOutputHelper _out;
    public PowerRatingTests(ITestOutputHelper o) => _out = o;

    private static Hunter Trained()
    {
        var h = new Hunter();
        h.AddGleam(5_000_000);
        foreach (var s in new[] { HunterStat.MaxHealth, HunterStat.AttackPower, HunterStat.Focus,
                                  HunterStat.Vitality, HunterStat.Defense, HunterStat.ResonanceAffinity })
            for (var i = 0; i < 20; i++) h.Train(s);
        h.Equip(Piece(ItemBaseType.Weapon, GearTrait.Keen));
        return h;
    }

    private static ItemInstance Piece(ItemBaseType type, GearTrait trait)
        => new()
        {
            InstanceId = $"{type}-{trait}", BaseType = type,
            Rarity = Rarity.Epic, ItemLevel = 20, SellValue = 1, TraitOverride = trait,
        };

    private static Hunter Wearing(GearTrait chest)
    {
        var h = Trained();
        h.Equip(Piece(ItemBaseType.Chest, chest));
        return h;
    }

    private static int MedianDepth(Hunter h)
    {
        var build = new Build { PassiveMods = BuildMods.None, Shape = SkillShape.None };
        // The old Strike/Projectile/Aura/Mark spread by catalogue id — three actives plus the Field
        // the Aura slot resolved to. Cadence lives on each def now, so no cooldown is passed.
        foreach (var id in new[] { "hammer_blow", "volley_spray", "field_mire", "sign_call" })
            build.Equip(TestBuilds.Skill(id, Source.Nature));

        var depths = Enumerable.Range(0, 40).Select(i =>
        {
            var hp = SoloBattle.ChampionHealth(build, h);
            var run = new SoloExpedition(build, new Champion { MaxHealth = hp, Health = hp }, h,
                                         enemyBaseHealth: 120f, enemyBaseDamage: 9f,
                                         ExpeditionTuning.Default, rng: new Random(9_000 + i))
            { RegionId = "verdant_hollow", RunIndex = i };
            while (!run.Over && run.Wave < 400) run.PushWave();
            return run.Wave;
        }).OrderBy(d => d).ToArray();
        return depths[depths.Length / 2];
    }

    [Fact]
    public void test_a_cadence_trait_raises_power_because_it_raises_depth()
    {
        // THE REGRESSION TEST FOR THE PLAYER'S REPORT. SWIFT trades 10% hit size for 10% cadence and
        // the old rating, blind to cadence, read that as a straight loss.
        var bare = Trained();
        var swift = Wearing(GearTrait.Swift);

        _out.WriteLine($"bare  power {bare.PowerRating,6}  depth {MedianDepth(bare)}");
        _out.WriteLine($"SWIFT power {swift.PowerRating,6}  depth {MedianDepth(swift)}");

        Assert.True(MedianDepth(swift) > MedianDepth(bare), "the fixture no longer demonstrates the case");
        Assert.True(swift.PowerRating > bare.PowerRating,
                    $"a piece that takes the character deeper must not lower its power — "
                    + $"{swift.PowerRating} against {bare.PowerRating}");
    }

    [Fact]
    public void test_power_ranks_armour_the_way_depth_does()
    {
        // The whole table, because the failure was an ORDERING failure: the old formula ranked the two
        // best pieces last. Compared pairwise and allowing ties, since two pieces two waves apart are
        // inside the noise and the rating is not obliged to resolve them.
        var cases = new[] { GearTrait.Warding, GearTrait.Swift, GearTrait.Focused, GearTrait.Vital }
            .Select(t => (Trait: t, H: Wearing(t)))
            .Select(x => (x.Trait, Power: x.H.PowerRating, Depth: MedianDepth(x.H)))
            .ToList();

        foreach (var c in cases) _out.WriteLine($"{c.Trait,-10} power {c.Power,6}  depth {c.Depth}");

        foreach (var a in cases)
            foreach (var b in cases)
            {
                // Only pairs the sim separates clearly. 4, not 3: the flat wave-opening pause
                // (ExpeditionTuning.WaveOpeningMs) shifts every median by about a wave, so two
                // same-axis traits (Swift vs Focused, both cadence) can land three apart on pure
                // noise. The catastrophe this test exists for — the two best pieces ranked LAST —
                // is a 4+ wave separation everywhere it ever appeared.
                if (a.Depth - b.Depth < 4) continue;
                Assert.True(a.Power > b.Power,
                            $"{a.Trait} reaches depth {a.Depth} against {b.Trait}'s {b.Depth}, "
                            + $"but reads {a.Power} power against {b.Power}");
            }
    }

    [Fact]
    public void test_cadence_is_in_the_formula_at_all()
    {
        // The unit-level pin under the two measured tests above, so a refactor that drops the term
        // fails in milliseconds rather than in a forty-run sweep.
        var slow = Gear.PowerRating(damageMultiplier: 4f, skillRate: 1f, healthMultiplier: 1f,
                                    defense: 10, maxHealth: 200);
        var fast = Gear.PowerRating(damageMultiplier: 4f, skillRate: 2f, healthMultiplier: 1f,
                                    defense: 10, maxHealth: 200);
        Assert.True(fast > slow, "doubling cadence must double the damage half of the rating");
    }
}
