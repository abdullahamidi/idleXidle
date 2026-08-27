using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Expeditions;
using Xunit;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The skill tree must reach the fight.
/// </summary>
/// <remarks>
/// <para>
/// This file exists because of a specific failure: <c>BuildMods.Rarity</c> was resolved from keystones,
/// gear and a whole trait-tree road, carried through the expedition, summed across waves — and read by
/// nothing at all. Every half of the chain was individually correct and no test noticed for the length of
/// development. A tree of forty nodes is forty chances to repeat that.
/// </para>
/// <para>
/// So these tests assert the END of the chain. Not "the shape has HitSize 1.25" — that is arithmetic —
/// but "a Weight build delivers more damage through armour than a neutral one", measured by the same sim
/// the game runs.
/// </para>
/// </remarks>
public class SkillShapeBattleTests
{
    private static EquippedSkill Sk(Form form)
        => new(new WovenAbility { Name = form.ToString(), Source = Source.Nature, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    private static Build BuildWith(SkillShape shape, params Form[] forms)
    {
        var b = new Build { Shape = shape };
        foreach (var f in forms) b.Weave(Sk(f));
        return b;
    }

    /// <summary>
    /// A deliberately enormous pool, because these tests measure DIFFERENCES in health lost.
    /// </summary>
    /// <remarks>
    /// The first version used 4000 and half the suite failed with "Endure lost 4000 and neutral lost 4000":
    /// both champions had simply died, so the measurement was pinned at the pool size and every mitigation
    /// node looked inert. A fixture that saturates its own metric cannot prove anything about the node it
    /// is testing.
    /// </remarks>
    private static Champion Champ(int hp = 200_000) => new() { MaxHealth = hp, Health = hp };

    private static List<WaveCreature> Wave(int count, float health, float damage, float defense = 0f,
                                           Archetype? archetype = null)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = health,
            Health = health,
            Damage = damage,
            Defense = defense,
            Archetype = archetype,
        }).ToList();

    /// <summary>Run one wave and return what it measured.</summary>
    private static WaveMetrics Fight(SkillShape shape, List<WaveCreature> creatures, params Form[] forms)
        => Fight(shape, creatures, ExpeditionTuning.Default, forms);

    /// <summary>
    /// The basic attack switched off — for a probe that must isolate ONE node's effect on the skills.
    /// Since 2026-08-26 the swing is a third of a build's damage and can carry or muddy a kill on its own.
    /// </summary>
    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static WaveMetrics Fight(SkillShape shape, List<WaveCreature> creatures, ExpeditionTuning tuning, params Form[] forms)
    {
        var metrics = new WaveMetrics();
        SoloBattle.ResolveWave(
            Champ(), BuildWith(shape, forms.Length == 0 ? new[] { Form.Strike } : forms), new Hunter(),
            creatures, enemyIntervalMs: 900, tuning, new Random(11),
            metrics: metrics);
        return metrics;
    }

    /// <summary>Every node of a branch, composed — what a player who walked it all the way carries.</summary>
    private static SkillShape WholeBranch(Branch branch)
        => SkillShape.Sum(MasteryCatalog.Nodes
            .Where(n => n.Branch == branch && n.Link is null && n.Kind != MasteryKind.Specialisation)
            .Select(n => n.Shape));

    // ── WEIGHT. ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>A Weight build gets more of its damage through flat armour.</summary>
    /// <remarks>
    /// The whole branch in one measurement. AbsorbedFraction is what the post-run report shows a player
    /// when armour is the wall, so if this does not move, the report is pointing at a lever that does not
    /// exist.
    /// </remarks>
    [Fact]
    public void test_the_weight_branch_gets_through_armour()
    {
        var armoured = () => Wave(2, 3000f, 40f, defense: 60f, archetype: Archetype.Armoured);

        var neutral = Fight(SkillShape.None, armoured());
        var weight = Fight(WholeBranch(Branch.Weight), armoured());

        Assert.True(weight.AbsorbedFraction < neutral.AbsorbedFraction,
            $"Armour ate {weight.AbsorbedFraction:P0} of the Weight build and {neutral.AbsorbedFraction:P0} " +
            "of a neutral one. The branch that exists to beat flat mitigation does not beat it.");
    }

    /// <summary>SHARPENED's armour cut reaches the hit.</summary>
    [Fact]
    public void test_armour_penetration_reaches_the_hit()
    {
        // A creature that outlives the fight, so the two builds are compared on what they DELIVER in
        // the same time — against 5000 health both simply killed it and delivered 5000.
        var plated = () => Wave(1, 5_000_000f, 30f, defense: 50f);

        var bare = Fight(SkillShape.None, plated());
        var cut = Fight(SkillShape.None with { ArmourPenetration = 30f }, plated());

        Assert.True(cut.DeliveredDamage > bare.DeliveredDamage * 1.05f,
            "Armour penetration changed nothing — SHARPENED and EXECUTIONER are both decoration.");
    }

    /// <summary>
    /// OVERWHELM does both of its halves: small hits land for nothing, large ones ignore armour.
    /// </summary>
    /// <remarks>
    /// The most consequential node in the tree, and the only one that can make a build deal literally
    /// zero. If only the upside were wired it would be a pure gift at 8 points; if only the downside
    /// were, it would be a trap. Both halves, or neither.
    /// </remarks>
    [Fact]
    public void test_overwhelm_erases_small_hits_and_ignores_armour_on_large_ones()
    {
        var shape = SkillShape.None with { OverwhelmFloor = 60f };

        // Aura ticks are tiny by construction — far below the floor. The comparison is against the same
        // build WITHOUT the mastery rather than against zero, because the background auto-attack is not a
        // skill and correctly ignores the floor; asserting a flat zero measured the auto-attack instead.
        var tiny = () => Wave(3, 900f, 20f);
        var unhindered = Fight(SkillShape.None, tiny(), NoSwing, Form.Aura);
        var floored = Fight(shape, tiny(), NoSwing, Form.Aura);

        Assert.True(floored.DeliveredDamage < unhindered.DeliveredDamage * 0.25f,
            $"Small hits delivered {floored.DeliveredDamage:F0} against {unhindered.DeliveredDamage:F0} " +
            "without the mastery. OVERWHELM is charging nothing for its upside.");
        Assert.True(floored.RawDamage > 0f, "The hits must be MADE and then erased, not simply never thrown.");

        // Trap hits are the largest in the game — comfortably over it, and armour stops mattering.
        var heavy = () => Wave(1, 40000f, 20f, defense: 200f);
        var plain = Fight(SkillShape.None, heavy(), Form.Trap);
        var over = Fight(shape, heavy(), Form.Trap);

        Assert.True(over.DeliveredDamage > plain.DeliveredDamage,
            "A large hit did not ignore armour — OVERWHELM is charging its price and paying nothing.");
    }

    /// <summary>SUNDER strips armour for the rest of the wave, so the build accelerates.</summary>
    [Fact]
    public void test_sunder_strips_armour_during_the_wave()
    {
        var creatures = Wave(1, 60000f, 20f, defense: 150f);
        Fight(SkillShape.None with { SunderThreshold = 1f, SunderAmount = 20f }, creatures, Form.Trap);

        Assert.True(creatures[0].Defense < 150f,
            "The creature's armour is untouched — SUNDER is a label on a node that does nothing.");
    }

    // ── SPREAD. ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>A Spread build reaches more creatures per cast.</summary>
    [Fact]
    public void test_the_spread_branch_reaches_more_creatures()
    {
        var swarm = () => Wave(6, 400f, 15f, archetype: Archetype.Swarm);

        var neutral = Fight(SkillShape.None, swarm(), Form.Projectile);
        var spread = Fight(WholeBranch(Branch.Spread), swarm(), Form.Projectile);

        Assert.True(spread.TargetsPerActivation > neutral.TargetsPerActivation,
            $"Reach was {spread.TargetsPerActivation:F2} for the Spread build and " +
            $"{neutral.TargetsPerActivation:F2} for a neutral one — the branch buys no action economy.");
    }

    /// <summary>EVERYWHERE strikes the whole wave with one cast.</summary>
    [Fact]
    public void test_everywhere_strikes_the_whole_wave()
    {
        // 40 000 health each: nothing dies during the fight, so every cast finds all five (a creature
        // that fell to the beat's heavier hits made the average read 4.4 of 5).
        var m = Fight(SkillShape.None with { StrikesEveryCreature = true, HitSize = 0.4f },
                      Wave(5, 40_000f, 10f), Form.Strike);

        Assert.True(m.TargetsPerActivation >= 4.5f,
            $"One cast reached {m.TargetsPerActivation:F1} creatures of five. EVERYWHERE reaches all of them.");
    }

    /// <summary>CHAIN reaches past the activation's own target count.</summary>
    [Fact]
    public void test_chain_strikes_beyond_the_target_count()
    {
        var swarm = () => Wave(5, 1200f, 10f);

        var plain = Fight(SkillShape.None, swarm(), Form.Strike);
        var chained = Fight(SkillShape.None with { ChainFraction = 0.5f }, swarm(), Form.Strike);

        Assert.True(chained.TargetsPerActivation > plain.TargetsPerActivation);
    }

    // ── TEMPO. ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A Tempo build front-loads: it clears the same wave in less time.</summary>
    [Fact]
    public void test_the_tempo_branch_clears_faster()
    {
        var wave = () => Wave(2, 1400f, 12f, archetype: Archetype.Caster);

        var neutral = Fight(SkillShape.None, wave(), Form.Strike, Form.Projectile);
        var tempo = Fight(WholeBranch(Branch.Tempo), wave(), Form.Strike, Form.Projectile);

        Assert.True(tempo.DurationMs < neutral.DurationMs,
            $"Tempo took {tempo.DurationMs}ms against {neutral.DurationMs}ms. The branch that exists to " +
            "kill a Caster before it acts is not faster than not taking it.");
    }

    /// <summary>PREPARATION removes the opening wait, so the first cast lands sooner.</summary>
    [Fact]
    public void test_preparation_frees_the_opening_cast()
    {
        // NOT Trap: Trap fires only when bitten, so it never walks the cooldown path this node changes.
        // 200 health: one Strike kills it, so the kill IS the opening cast — with PREPARATION on the
        // wave's first beat, without it on the second (the beat model, 2026-08-27). At 300 both builds
        // needed the same number of beats and the swing masked the opener.
        var wave = () => Wave(1, 200f, 5f);

        var waited = Fight(SkillShape.None, wave(), NoSwing, Form.Strike);
        var ready = Fight(SkillShape.None with { FreeOpeningCast = true }, wave(), NoSwing, Form.Strike);

        Assert.True(ready.DurationMs < waited.DurationMs,
            "The opening cast still waited a full cooldown — PREPARATION does nothing.");
    }

    /// <summary>ASSASSINATE kills outright, which a damage bonus cannot do.</summary>
    [Fact]
    public void test_assassinate_finishes_a_weakened_creature()
    {
        var weakened = Wave(1, 20000f, 10f);
        weakened[0].Health = 3000f;   // 15% — under the threshold

        var m = Fight(SkillShape.None with { AssassinateThreshold = 0.40f }, weakened, Form.Strike);

        Assert.Equal(1, m.CreaturesKilled);
        // The opening cast waits one cooldown (3000 ms since 2026-08-26); the kill must be THAT cast.
        Assert.True(m.DurationMs <= FormBehaviour.BaseCooldownMs(Form.Strike) + 200,
            "A creature under the threshold survived long enough that ordinary damage killed it — the " +
            "node is not firing, it is being overtaken.");
    }

    // ── ENDURE. ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>An Endure build loses less health to the same wave.</summary>
    [Fact]
    public void test_the_endure_branch_loses_less_health()
    {
        var bruiser = () => Wave(1, 9000f, 260f, archetype: Archetype.Bruiser);

        var neutral = Fight(SkillShape.None, bruiser());
        var endure = Fight(WholeBranch(Branch.Endure), bruiser());

        Assert.True(endure.HealthLost < neutral.HealthLost,
            $"Endure lost {endure.HealthLost} health and a neutral build lost {neutral.HealthLost}. " +
            "The branch that answers Bruisers does not answer them.");
    }

    /// <summary>PADDING is a flat cut, so it erases small bites entirely.</summary>
    [Fact]
    public void test_padding_erases_small_bites()
    {
        var nibbles = () => Wave(4, 6000f, 5f);

        var bare = Fight(SkillShape.None, nibbles());
        var padded = Fight(SkillShape.None with { FlatDamageReduction = 30f }, nibbles());

        Assert.True(bare.HealthLost > 0);
        Assert.Equal(0, padded.HealthLost);
    }

    /// <summary>FORTIFY eats the first bite of the wave and only the first.</summary>
    [Fact]
    public void test_fortify_eats_only_the_first_bite()
    {
        var heavy = () => Wave(1, 40000f, 400f);

        var bare = Fight(SkillShape.None, heavy());
        var fortified = Fight(SkillShape.None with { FirstBiteFree = true }, heavy());

        Assert.True(fortified.HealthLost < bare.HealthLost);
        Assert.True(fortified.HealthLost > 0,
            "Every bite was free — FORTIFY is meant to eat one, not to be immunity.");
    }

    /// <summary>LEECH turns damage dealt into health.</summary>
    [Fact]
    public void test_leech_returns_health()
    {
        var wave = () => Wave(3, 4000f, 120f);

        var dry = Fight(SkillShape.None, wave());
        var leeching = Fight(SkillShape.None with { Leech = 0.25f }, wave());

        Assert.True(leeching.HealthLost < dry.HealthLost);
    }

    // ── THE ACCEPTANCE TEST FOR THE WHOLE DESIGN. ─────────────────────────────────────────────────

    /// <summary>
    /// The opposed pairs are actually opposed.
    /// </summary>
    /// <remarks>
    /// EVERYTHING RESTS ON THIS. The tree's four branches, the archetype engine, the band cycles, the
    /// counter-band rule and the post-run report all assume that a build shaped one way is worse when the
    /// content asks the opposite question. If a Weight build were merely "a bit different" in a Swarm band
    /// rather than genuinely worse, there would be no wall to hit, nothing for the report to diagnose,
    /// and no reason to ever visit the tree a second time.
    /// </remarks>
    [Fact]
    public void test_weight_and_spread_win_and_lose_the_opposite_waves()
    {
        var weight = WholeBranch(Branch.Weight);
        var spread = WholeBranch(Branch.Spread);

        List<WaveCreature> Armoured() => Wave(2, 3000f, 40f, defense: 60f, archetype: Archetype.Armoured);
        List<WaveCreature> Swarm() => Wave(7, 2600f, 14f, archetype: Archetype.Swarm);

        var weightVsArmour = Fight(weight, Armoured(), Form.Strike, Form.Trap).DeliveredDamage;
        var spreadVsArmour = Fight(spread, Armoured(), Form.Strike, Form.Trap).DeliveredDamage;
        var weightVsSwarm = Fight(weight, Swarm(), Form.Strike, Form.Trap).DurationMs;
        var spreadVsSwarm = Fight(spread, Swarm(), Form.Strike, Form.Trap).DurationMs;

        Assert.True(weightVsArmour > spreadVsArmour,
            $"Against armour, Weight delivered {weightVsArmour:F0} and Spread {spreadVsArmour:F0}. " +
            "The pair is not opposed, so neither branch has a reason to exist.");

        Assert.True(spreadVsSwarm < weightVsSwarm,
            $"Against a swarm, Spread took {spreadVsSwarm}ms and Weight took {weightVsSwarm}ms. " +
            "A Weight build is supposed to STALL here — that stall is the game.");
    }

    // ── THE SIDE ROADS' TWO NEW FIELDS. Each one is a new read in the sim, so each gets the "does the
    //    fight actually read it" test that the rest of this file exists for. ──────────────────────

    /// <summary>
    /// MOMENTUM — a kill takes time off every cooldown, so a Strike clears a field of weaklings faster.
    /// </summary>
    /// <remarks>
    /// Measured on duration, not activations: both builds make the same six kills, so the count is the
    /// same; what the refund buys is the six kills arriving sooner. A Strike recovers in 2,000ms and a
    /// kill hands back 1,000, so with one kill per swing the second half of the wave should run at
    /// roughly double pace.
    /// </remarks>
    [Fact]
    public void test_momentum_refunds_cooldowns_on_every_kill()
    {
        // Six creatures a single Strike finishes each — one kill per swing, no overkill to muddy it.
        var field = () => Wave(6, 30f, 5f);

        // No swing: the basic attack finishing half the field on its own hid the refund behind timing noise.
        var plain = Fight(SkillShape.None, field(), NoSwing, Form.Strike);
        var momentum = Fight(SkillShape.None with { CooldownRefundOnKillMs = 1_000 }, field(), NoSwing, Form.Strike);

        Assert.Equal(6, plain.CreaturesKilled);
        Assert.Equal(6, momentum.CreaturesKilled);
        Assert.True(momentum.DurationMs < plain.DurationMs * 0.8f,
            $"With MOMENTUM the wave took {momentum.DurationMs}ms against {plain.DurationMs}ms without it. " +
            "The refund is not reaching the cooldown table — the node is dormant.");
    }

    /// <summary>
    /// MOMENTUM is worth more the more there is to kill — a torrent in a Swarm, a trickle against one.
    /// </summary>
    [Fact]
    public void test_momentum_pays_by_the_kill_not_by_the_wave()
    {
        var shape = SkillShape.None with { CooldownRefundOnKillMs = 1_000 };

        // One creature with the health of six: one kill, one refund, and nothing left to spend it on.
        var one = Fight(shape, Wave(1, 180f, 5f), Form.Strike);
        var onePlain = Fight(SkillShape.None, Wave(1, 180f, 5f), Form.Strike);
        Assert.Equal(onePlain.DurationMs, one.DurationMs);
    }

    /// <summary>
    /// THORNS — a creature that bites takes a share of its own bite back, so a big biter dies sooner.
    /// </summary>
    /// <remarks>
    /// The champion's own damage is identical in both runs (same skills, same seed), so any change in
    /// how long the creature lasts is the thorns and nothing else. 3,000 health, not more: a bare
    /// Strike lands roughly 28 a second, so a pool a lone Strike cannot empty inside the 120-second
    /// ceiling makes BOTH runs stall at the ceiling and the metric saturates — the same fixture trap
    /// the class remarks describe for health.
    /// </remarks>
    [Fact]
    public void test_thorns_return_a_share_of_every_bite()
    {
        var bruiser = () => Wave(1, 3_000f, 300f, archetype: Archetype.Bruiser);

        var plain = Fight(SkillShape.None, bruiser(), Form.Strike);
        var thorns = Fight(SkillShape.None with { ReflectFraction = 0.20f }, bruiser(), Form.Strike);

        Assert.True(thorns.DurationMs < plain.DurationMs,
            $"With THORNS the bruiser lasted {thorns.DurationMs}ms against {plain.DurationMs}ms without. " +
            "Nothing is being turned back — the node is dormant.");
    }

    /// <summary>THORNS scale with the bite: the same node punishes a heavy biter more than a light one.</summary>
    /// <remarks>
    /// This is what makes it an Endure node that ANSWERS Bruiser rather than a flat damage aura: the
    /// champion's pool is far too large for either bite to matter, so the only thing the bigger bite
    /// changes is how much comes back.
    /// </remarks>
    [Fact]
    public void test_thorns_scale_with_the_size_of_the_bite()
    {
        var shape = SkillShape.None with { ReflectFraction = 0.20f };

        var light = Fight(shape, Wave(1, 3_000f, 100f), Form.Strike);
        var heavy = Fight(shape, Wave(1, 3_000f, 600f), Form.Strike);

        Assert.True(light.DurationMs < ExpeditionTuning.Default.TickCeilingMs
                    && heavy.DurationMs < ExpeditionTuning.Default.TickCeilingMs,
            "A fixture that stalls at the ceiling measures the ceiling, not the node.");

        Assert.True(heavy.DurationMs < light.DurationMs,
            $"A 600-damage biter lasted {heavy.DurationMs}ms and a 100-damage biter {light.DurationMs}ms " +
            "under the same THORNS. The return is not reading the bite.");
    }
}
