using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using Xunit;

namespace ResonanceHunter.Core.Tests.Automation;

public class CreatureHatchTests
{
    /// <summary>
    /// Random hatching must be able to produce EVERY role — otherwise the farm's four-role composition
    /// puzzle (which needs a generator, a crafter, a defender, and a support) is literally unwinnable.
    /// This is a regression guard for the bug where every hatch was an Attacker.
    /// </summary>
    [Fact]
    public void test_random_hatching_can_produce_every_role()
    {
        var rng = new Random(1);
        var roles = new HashSet<Role>();
        for (var i = 0; i < 300; i++)
            roles.Add(Creature.HatchRandom($"c{i}", rng).Role);

        Assert.Equal(Enum.GetValues<Role>().Length, roles.Count);
    }

    /// <summary>A roster of random hatches can actually COMPLETE the composition chain to Optimized.</summary>
    [Fact]
    public void test_a_random_roster_can_complete_the_composition()
    {
        var rng = new Random(2);
        var roster = Enumerable.Range(0, 40).Select(i => Creature.HatchRandom($"c{i}", rng)).ToList();

        var region = new Region("verdant_hollow", 15);
        // Assign one creature of each chain role — proof the pieces exist to solve the puzzle.
        foreach (var role in new[] { Role.Attacker, Role.Crafter, Role.Defender, Role.Support })
            region.Assign(roster.First(c => c.Role == role));

        Assert.Equal(1f, region.CompositionCompleteness());
    }

    /// <summary>Deterministic for a seed — the roll is reproducible, so tests are stable.</summary>
    [Fact]
    public void test_hatching_is_deterministic_for_a_seed()
    {
        var a = Creature.HatchRandom("x", new Random(42));
        var b = Creature.HatchRandom("x", new Random(42));
        Assert.Equal(a.Role, b.Role);
        Assert.Equal(a.Source, b.Source);
        Assert.Equal(a.PowerTier, b.PowerTier);
    }
}
