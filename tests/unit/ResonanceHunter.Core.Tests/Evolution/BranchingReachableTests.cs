using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Evolution;
using Xunit;

namespace ResonanceHunter.Core.Tests.Evolution;

/// <summary>
/// The branches a player can actually REACH.
/// </summary>
/// <remarks>
/// The old suite proved the evolution ENGINE was correct — and it was. What nothing asked was whether
/// any branch could fire in a real game. Two of three could not: THORNSTALKER wanted `part_break`
/// events whose only recorder had zero callers, and BULWARK MOSS wanted job ticks nothing recorded
/// plus a trait spelled "ward" against an enum member named Warding. The whole evolution system was
/// one line, Whelp to Sporeling, and every test passed.
/// </remarks>
public class BranchingReachableTests
{
    private static Creature Fresh(Source source = Source.Nature, Role role = Role.Attacker)
    {
        var c = Creature.Hatch($"c_{source}_{role}", source, role, 3);
        c.BeginEvolution(EvolutionTrees.For(source), new EvolutionProgress());
        return c;
    }

    // ── Every Source, every Role ──────────────────────────────────────────────────────────────

    [Fact]
    public void test_every_source_has_a_tree()
    {
        // AutomationScreen handed every creature the NatureTree regardless of Source, and only if it
        // was an Attacker — so a Machine Support had no tree at all.
        foreach (var source in System.Enum.GetValues<Source>())
        {
            var tree = EvolutionTrees.For(source);
            Assert.True(tree.HasNode(EvolutionTrees.RootIdFor(source)));
        }
    }

    [Fact]
    public void test_every_role_is_reachable_by_evolution()
    {
        // "allowing the same creature to become an attacker, defender, support unit, crafter, or
        // resource producer" — before, only Producer was reachable, and only from an Attacker.
        var tree = EvolutionTrees.For(Source.Nature);
        var root = tree.Node(EvolutionTrees.RootIdFor(Source.Nature))!;

        var reachable = root.Children
            .Select(e => tree.Node(e.TargetNodeId).Role)
            .ToHashSet();

        foreach (var role in System.Enum.GetValues<Role>())
            Assert.Contains(role, reachable);
    }

    [Fact]
    public void test_each_source_names_its_own_creatures()
    {
        // A Machine creature evolving into a "SPORELING" would be the trees not really existing.
        var nature = EvolutionTrees.For(Source.Nature);
        var machine = EvolutionTrees.For(Source.Machine);

        Assert.NotEqual(
            nature.Node("nature_atk").DisplayName,
            machine.Node("machine_atk").DisplayName);
    }

    // ── Branch A: bosses felled. Was: part_break, unrecordable. ───────────────────────────────

    [Fact]
    public void test_the_attacker_branch_can_actually_fire()
    {
        var c = Fresh();
        var tree = EvolutionTrees.For(Source.Nature);

        for (var i = 0; i < 30; i++) c.Evolution!.FeedMaterial(1);
        Assert.Null(c.TryEvolve(tree)); // materials alone are not enough

        for (var i = 0; i < 4; i++) c.Evolution!.RecordCombat(CombatTags.BossFelled);
        Assert.Equal("nature_atk", c.TryEvolve(tree));
    }

    // The crediting WIRE — that a run's bosses/waves tally onto every creature that fought, and a
    // creature with no tree is skipped rather than thrown — is proven for the LIVE recorder in
    // EvolutionCreditTests. It once lived here through the retired ExpeditionRecord, which also wrote the
    // now-dead Banked tag; that path is gone with the squad.

    // ── Branch B: job ticks + a WARDING charm. Was dead twice over. ───────────────────────────

    [Fact]
    public void test_the_defender_branch_can_actually_fire()
    {
        // REGRESSION: the Defender branch also demanded a WARDING charm EQUIPPED ON THE CREATURE, but the
        // solo game never writes EquippedTrait (there is no creature-gear UI), so the branch was unreachable
        // for every player. It is now the job-tick condition alone — which farming produces — so a diligent
        // worker can become a Defender with no charm.
        var c = Fresh();
        var tree = EvolutionTrees.For(Source.Nature);

        for (var i = 0; i < 20; i++) c.Evolution!.FeedMaterial(1);
        Assert.Null(c.TryEvolve(tree)); // fed enough, but the work ticks aren't in yet

        for (var i = 0; i < 40; i++) c.Evolution!.RecordWorkTick(Role.Attacker, healthy: true);
        Assert.Equal("nature_def", c.TryEvolve(tree));   // no charm needed any more
    }

    /// <summary>
    /// REGRESSION: a NON-Attacker worker can still become a Defender. The farm records job ticks under the
    /// CREATURE'S live role, but the edge used to read the root node's role (Attacker), so a Producer's 40
    /// shifts landed in a bucket the check never looked at — the "diligent worker becomes a Defender" path
    /// was dead for the ~4-in-5 creatures that hatch as anything but an Attacker.
    /// </summary>
    [Fact]
    public void test_the_defender_branch_fires_from_a_non_attacker_worker()
    {
        var c = Fresh(role: Role.Producer);   // hatched a Producer, never an Attacker
        var tree = EvolutionTrees.For(Source.Nature);

        for (var i = 0; i < 20; i++) c.Evolution!.FeedMaterial(1);
        // Ticks recorded under the creature's OWN role, exactly as RegionAutomation does it.
        for (var i = 0; i < 40; i++) c.Evolution!.RecordWorkTick(Role.Producer, healthy: true);

        Assert.Equal("nature_def", c.TryEvolve(tree));
    }

    [Fact]
    public void test_the_warding_trait_tag_matches_the_enum()
    {
        // THE BUG: the branch asked for "ward"; the enum member is Warding. A player who ground out
        // all forty ticks with the right charm on would have been refused by a string mismatch,
        // silently, forever — and both spellings compiled.
        Assert.Equal(GearTrait.Warding.ToString(), CombatTags.WardingTrait);
        Assert.Equal(CombatTags.WardingTrait, CombatTags.TraitTag(GearTrait.Warding));
    }

    [Fact]
    public void test_a_farm_actually_records_job_ticks()
    {
        // THE OTHER HALF: RegionAutomation summed everyone's work into one float for mastery and never
        // told a single creature, so HealthyWorkTicks stayed empty in every save ever written.
        var region = new Region("r", 40);
        var worker = Fresh(Source.Nature, Role.Producer);
        region.Assign(worker);

        // Long enough to be many whole ticks whatever the interval.
        region.Tick(worker.WorkTickIntervalSeconds * 12f, gleamPerKill: 8);

        Assert.True(worker.Evolution!.HealthyWorkTicks.GetValueOrDefault(Role.Producer) > 0,
            "the farm recorded no work ticks — the job-diligence branch is still dead");
    }

    [Fact]
    public void test_job_ticks_accrue_even_when_the_farm_ticks_faster_than_a_work_tick()
    {
        // The farm ticks once a second; a work tick takes many. Without carrying the remainder, every
        // tick floors to zero and the branch stays just as dead, in a subtler way.
        var region = new Region("r", 40);
        var worker = Fresh(Source.Nature, Role.Producer);
        region.Assign(worker);

        for (var i = 0; i < (int)(worker.WorkTickIntervalSeconds * 3) + 3; i++)
            region.Tick(1f, gleamPerKill: 8);

        Assert.True(worker.Evolution!.HealthyWorkTicks.GetValueOrDefault(Role.Producer) >= 2,
            "one-second ticks accrued no whole work ticks — the remainder is being dropped");
    }

    [Fact]
    public void test_an_unhealthy_creature_earns_no_diligence()
    {
        var region = new Region("r", 40);
        var worker = Fresh(Source.Nature, Role.Producer);
        worker.IsHealthy = false;
        region.Assign(worker);

        region.Tick(worker.WorkTickIntervalSeconds * 12f, gleamPerKill: 8);

        Assert.Equal(0, worker.Evolution!.HealthyWorkTicks.GetValueOrDefault(Role.Producer));
    }

    // ── Branches C/D/E ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_the_support_branch_rewards_endurance()
    {
        var c = Fresh();
        var tree = EvolutionTrees.For(Source.Nature);

        for (var i = 0; i < 25; i++) c.Evolution!.FeedMaterial(1);
        for (var i = 0; i < 40; i++) c.Evolution!.RecordCombat(CombatTags.WaveCleared);

        Assert.Equal("nature_sup", c.TryEvolve(tree));
    }

    [Fact]
    public void test_the_crafter_branch_rewards_opening_chests()
    {
        // Was "knowing when to stop" (banking), a mechanic the idle loop deleted; it is chests-opened now.
        var c = Fresh();
        var tree = EvolutionTrees.For(Source.Nature);

        for (var i = 0; i < 45; i++) c.Evolution!.FeedMaterial(1);
        for (var i = 0; i < 5; i++) c.Evolution!.RecordCombat(CombatTags.ChestOpened);

        Assert.Equal("nature_crf", c.TryEvolve(tree));
    }

    [Fact]
    public void test_the_producer_branch_is_always_reachable_by_feeding_alone()
    {
        // The patient path. It must never require anything a player could fail to have.
        var c = Fresh();
        var tree = EvolutionTrees.For(Source.Nature);

        for (var i = 0; i < 60; i++) c.Evolution!.FeedMaterial(1);
        Assert.Equal("nature_prd", c.TryEvolve(tree));
    }

    [Fact]
    public void test_no_branch_requires_a_tag_nothing_records()
    {
        // The guard. Every combat condition in every tree must name a tag the LIVE game records — in the
        // solo model that is EvolutionCredit (CreditWave writes BossFelled/WaveCleared, CreditChestOpened
        // writes ChestOpened). A branch keyed on a tag nothing produces is decoration — which is exactly
        // what part_break was, and then Banked, which is why Banked is gone from the trees.
        var recorded = new[] { CombatTags.BossFelled, CombatTags.WaveCleared, CombatTags.ChestOpened };

        foreach (var source in System.Enum.GetValues<Source>())
        {
            var tree = EvolutionTrees.For(source);
            var root = tree.Node(EvolutionTrees.RootIdFor(source))!;

            foreach (var edge in root.Children.Where(e => e.CombatTag is not null))
                Assert.Contains(edge.CombatTag!, recorded);
        }
    }

    [Fact]
    public void test_no_branch_requires_a_trait_that_does_not_exist()
    {
        var traits = System.Enum.GetValues<GearTrait>().Select(t => t.ToString()).ToHashSet();

        foreach (var source in System.Enum.GetValues<Source>())
        {
            var tree = EvolutionTrees.For(source);
            var root = tree.Node(EvolutionTrees.RootIdFor(source))!;

            foreach (var edge in root.Children.Where(e => e.RequiredEquipTrait is not null))
                Assert.Contains(edge.RequiredEquipTrait!, traits);
        }
    }
}
