using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Progression;
using IdleXIdle.Core.Sources;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

/// <summary>
/// A Vow reveals itself when you have already kept its rule — without it, and with something to break.
/// </summary>
/// <remarks>
/// The trait tree was the only producer of all thirteen Vows: five study nodes taught them between
/// them, so the whole system was invisible to a player who spent their points elsewhere. The rule that
/// replaces it is proof before reward, and these tests are what stop it becoming a lottery.
/// </remarks>
public class VowDiscoveryTest
{
    /// <summary>Enough waves for the harshest proof in the catalogue, so a test can say "long enough".</summary>
    private static readonly int Plenty = Vows.Catalog.Max(v => v.ProofWaves);

    private static Dictionary<string, int> Proof(string id, int waves) =>
        new(StringComparer.Ordinal) { [id] = waves };

    /// <summary>Every temptation satisfied — a mid-career account that owned everything and refused it.</summary>
    private static VowTemptationFacts Tempted() => new(
        OwnsCritItem: true, OwnsSkillRateItem: true, OwnsDefenceItem: true, OwnsDamageItem: true,
        OwnsHealthItem: true, OwnsHaulItem: true, OwnsBoots: true, OwnsGloves: true, OwnsHelm: true,
        OwnsRing: true, OwnsCharm: true, EveryWovenSourceChosen: true, StylesInReach: 6,
        KeystonesKnown: 3);

    [Fact]
    public void test_every_vow_is_findable_and_exactly_one_is_simply_given()
    {
        // A Vow nobody can find is content that does not exist; and without one given openly, a player
        // can reach the end of the game without ever learning the system is there.
        Assert.Single(Vows.Granted);
        Assert.Equal("vow_complete", Vows.Granted[0].Id);
        Assert.Equal(Vows.Catalog.Count, Vows.Granted.Count + Vows.Discoverable.Count);

        foreach (var vow in Vows.Discoverable)
        {
            Assert.True(vow.ProofWaves > 0, $"{vow.Id} asks for no proof at all");
            Assert.False(string.IsNullOrWhiteSpace(vow.ProofLine), $"{vow.Id} never says what you did");
            Assert.False(string.IsNullOrWhiteSpace(vow.RevealLine), $"{vow.Id} reveals with no line");
        }
    }

    [Fact]
    public void test_a_proved_rule_reveals_the_vow()
    {
        foreach (var vow in Vows.Discoverable)
        {
            var found = Vows.Revealed(Proof(vow.Id, vow.ProofWaves), Array.Empty<string>(),
                                      Array.Empty<string>(), Tempted());
            Assert.Contains(found, v => v.Id == vow.Id);
        }
    }

    [Fact]
    public void test_one_wave_short_reveals_nothing()
    {
        foreach (var vow in Vows.Discoverable)
        {
            var found = Vows.Revealed(Proof(vow.Id, vow.ProofWaves - 1), Array.Empty<string>(),
                                      Array.Empty<string>(), Tempted());
            Assert.DoesNotContain(found, v => v.Id == vow.Id);
        }
    }

    [Fact]
    public void test_a_vow_you_were_wearing_proves_nothing()
    {
        // LAW 10's actual test. The reward is for keeping the rule BEFORE it promised you power; a
        // descent run under the Vow is the Vow paying out, not the player proving anything.
        foreach (var vow in Vows.Discoverable)
        {
            var found = Vows.Revealed(Proof(vow.Id, Plenty), new[] { vow.Id },
                                      Array.Empty<string>(), Tempted());
            Assert.DoesNotContain(found, v => v.Id == vow.Id);
        }
    }

    [Fact]
    public void test_a_vow_already_known_is_never_revealed_twice()
    {
        foreach (var vow in Vows.Discoverable)
        {
            var found = Vows.Revealed(Proof(vow.Id, Plenty), Array.Empty<string>(),
                                      new[] { vow.Id }, Tempted());
            Assert.Empty(found);
        }
    }

    [Fact]
    public void test_a_rule_you_could_not_have_broken_proves_nothing()
    {
        // Without this clause a brand-new hunter discovers ten of the eleven demand Vows on wave one,
        // because one slot, one skill, one Source, no crit, no defence, no keystone and no gear
        // satisfies almost the whole catalogue by simply not owning anything yet.
        var bare = new VowTemptationFacts();

        foreach (var vow in Vows.Discoverable.Where(v => v.Temptation != VowTemptation.None))
        {
            var found = Vows.Revealed(Proof(vow.Id, Plenty), Array.Empty<string>(),
                                      Array.Empty<string>(), bare);
            Assert.DoesNotContain(found, v => v.Id == vow.Id);
        }
    }

    [Fact]
    public void test_each_gear_vow_needs_its_own_slot_owned_and_not_another()
    {
        // A player who owns gloves and no boots has refused gloves, not boots.
        var glovesOnly = new VowTemptationFacts(OwnsGloves: true);
        var proof = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["vow_barefoot"] = Plenty, ["vow_openhand"] = Plenty, ["vow_bareskull"] = Plenty,
        };

        var found = Vows.Revealed(proof, Array.Empty<string>(), Array.Empty<string>(), glovesOnly)
            .Select(v => v.Id).ToList();

        Assert.Equal(new[] { "vow_openhand" }, found);
    }

    [Fact]
    public void test_discovery_is_deterministic_and_reads_no_randomness()
    {
        // Vow discovery must never be a roll, a proc or a drop. The same descent, asked twice, answers
        // the same — and the whole path is build facts and owned items, with no Random anywhere in it.
        var proof = Vows.Discoverable.ToDictionary(v => v.Id, v => v.ProofWaves, StringComparer.Ordinal);
        var first = Vows.Revealed(proof, Array.Empty<string>(), Array.Empty<string>(), Tempted());
        var again = Vows.Revealed(proof, Array.Empty<string>(), Array.Empty<string>(), Tempted());

        Assert.Equal(first.Select(v => v.Id), again.Select(v => v.Id));
        Assert.Equal(Vows.Discoverable.Count, first.Count);
    }

    // ── THE PROOF COUNTER — the reason it exists at all ──────────────────────────────────────────

    private static SoloExpedition Descent(Build build, Hunter hunter) => new(
        build, new Champion { MaxHealth = 400_000, Health = 400_000 }, hunter,
        enemyBaseHealth: 8f, enemyBaseDamage: 1f, rng: new Random(7));

    [Fact]
    public void test_a_cleared_wave_credits_the_rules_the_build_kept()
    {
        // One skill, one Source, no gear, no keystone: this build keeps a great many rules at once,
        // and every cleared wave credits each of them.
        var run = Descent(TestBuilds.Of("hammer_blow"), new Hunter());
        for (var i = 0; i < 3; i++) Assert.Equal(WaveOutcome.Cleared, run.PushWave());

        Assert.Equal(3, run.VowProofWaves["vow_singular"]);
        Assert.Equal(3, run.VowProofWaves["vow_barefoot"]);
        Assert.Equal(3, run.VowProofWaves["vow_unbound"]);
    }

    [Fact]
    public void test_a_vow_the_build_swore_is_never_credited()
    {
        var sworn = Vows.ById("vow_singular");
        var b = new Build();
        b.Equip(TestBuilds.Skill("hammer_blow", Source.Body, sworn));
        var run = Descent(b, new Hunter());
        for (var i = 0; i < 3; i++) run.PushWave();

        Assert.False(run.VowProofWaves.ContainsKey("vow_singular"));
        Assert.Equal(3, run.VowProofWaves["vow_barefoot"]);   // the unsworn ones still count
    }

    [Fact]
    public void test_a_mid_run_swap_cannot_fake_a_proof()
    {
        // THIS IS THE TEST THAT JUSTIFIES THE COUNTER EXISTING. Without it a player could clear
        // fourteen waves on a wide build, drop to one skill in the breath before wave fifteen, die, and
        // have the end-of-run context report one Style, one Source and three bare slots — six Vows
        // proved by a build that never fought.
        var wide = TestBuilds.Of("hammer_blow", "volley_spray");
        var run = Descent(wide, new Hunter());
        for (var i = 0; i < 3; i++) run.PushWave();
        Assert.False(run.VowProofWaves.ContainsKey("vow_singular"));

        run.ReplaceBuild(TestBuilds.Of("hammer_blow"));
        run.PushWave();

        // ONE wave of proof, from the one wave the narrow build actually fought — not four.
        Assert.Equal(1, run.VowProofWaves["vow_singular"]);
    }

    [Fact]
    public void test_a_checkpoint_start_buys_no_proof()
    {
        // Depth is purchasable: a checkpoint sets the wave number from a Memory Dust price, so counting
        // DEPTH would let a player buy a start at wave 40, die on 41, and claim a twenty-wave proof
        // having fought one wave. Cleared waves close that by construction.
        var run = Descent(TestBuilds.Of("hammer_blow"), new Hunter());
        run.StartAtWave(40);
        Assert.Equal(40, run.Wave);
        Assert.Empty(run.VowProofWaves);

        run.PushWave();
        Assert.Equal(1, run.VowProofWaves["vow_singular"]);
    }

    [Fact]
    public void test_the_wave_that_killed_you_does_not_count()
    {
        // A run that dies teaches nothing on its way out — the same rule the skill levels already run on.
        var run = new SoloExpedition(
            TestBuilds.Of("hammer_blow"), new Champion { MaxHealth = 1, Health = 1 }, new Hunter(),
            enemyBaseHealth: 5_000_000f, enemyBaseDamage: 500_000f, rng: new Random(7));

        Assert.NotEqual(WaveOutcome.Cleared, run.PushWave());
        Assert.Empty(run.VowProofWaves);
    }

    [Fact]
    public void test_no_reward_before_discovery()
    {
        // The composer refuses a Vow the account has not found, so an undiscovered Vow riding a slot —
        // from a share code, or a stale save — composes as no Vow and the fight pays nothing for it.
        var pick = new[] { new BuildComposer.SkillPick(Source.Body, "vow_singular", null, "hammer_blow") };
        var mastery = Taught.Everything();

        var unknown = BuildComposer.Compose(mastery, null, pick,
            Array.Empty<string>(), 4, null, Array.Empty<Keystone>(), Array.Empty<Vow>());
        Assert.Null(unknown.Skills[0].Vow);
        Assert.Empty(unknown.Vows);

        var known = BuildComposer.Compose(mastery, null, pick,
            Array.Empty<string>(), 4, null, Array.Empty<Keystone>(), new[] { Vows.ById("vow_singular")! });
        Assert.Equal("vow_singular", known.Skills[0].Vow!.Id);
    }
}
