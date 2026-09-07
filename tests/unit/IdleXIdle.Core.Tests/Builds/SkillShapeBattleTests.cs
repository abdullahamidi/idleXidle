using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using Xunit;

namespace IdleXIdle.Core.Tests.Builds;

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
    private static EquippedSkill Sk(string skillId) => TestBuilds.Skill(skillId, Source.Nature);

    private static Build BuildWith(SkillShape shape, params string[] skillIds)
    {
        var b = new Build { Shape = shape };
        foreach (var id in skillIds) b.Equip(Sk(id));
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
    private static WaveMetrics Fight(SkillShape shape, List<WaveCreature> creatures, params string[] skillIds)
        => Fight(shape, creatures, ExpeditionTuning.Default, skillIds);

    /// <summary>
    /// The basic attack switched off — for a probe that must isolate ONE node's effect on the skills.
    /// Since 2026-08-26 the swing is a third of a build's damage and can carry or muddy a kill on its own.
    /// </summary>
    private static readonly ExpeditionTuning NoSwing = ExpeditionTuning.Default with { AutoAttackDamage = 0f };

    private static WaveMetrics Fight(SkillShape shape, List<WaveCreature> creatures, ExpeditionTuning tuning, params string[] skillIds)
    {
        var metrics = new WaveMetrics();
        SoloBattle.ResolveWave(
            Champ(), BuildWith(shape, skillIds.Length == 0 ? new[] { "hammer_blow" } : skillIds), new Hunter(),
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

        // A player who walked the whole branch carries PURE, and PURE pays a CHOSEN source only
        // (Build.ChosenSingleSource, 2026-09-07): a skill still on its Source seed is a default, not a
        // decision. The build is posed the way that player's is — its variation taken — because the
        // old unchosen fixture was being paid PURE's +35% for agreeing with itself.
        WaveMetrics Fought(SkillShape shape)
        {
            var metrics = new WaveMetrics();
            var b = new Build { Shape = shape };
            b.Equip(TestBuilds.Chosen("hammer_blow", "FLATTEN"));
            SoloBattle.ResolveWave(Champ(), b, new Hunter(), armoured(), enemyIntervalMs: 900,
                                   ExpeditionTuning.Default, new Random(11), metrics: metrics);
            return metrics;
        }

        var neutral = Fought(SkillShape.None);
        var weight = Fought(WholeBranch(Branch.Resonance));

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

        // No swing: the basic attack ignores armour penetration (it is not a skill hit) and, since it
        // took five beats in six, it diluted the two builds' difference below the margin.
        var bare = Fight(SkillShape.None, plated(), NoSwing);
        var cut = Fight(SkillShape.None with { ArmourPenetration = 30f }, plated(), NoSwing);

        Assert.True(cut.DeliveredDamage > bare.DeliveredDamage * 1.05f,
            "Armour penetration changed nothing — SHARPENED and EXECUTIONER are both decoration.");
    }

    // ── TEMPO. ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A Tempo build front-loads: it clears the same wave in less time.</summary>
    [Fact]
    public void test_the_tempo_branch_clears_faster()
    {
        var wave = () => Wave(2, 1400f, 12f, archetype: Archetype.Caster);

        var neutral = Fight(SkillShape.None, wave(), "hammer_blow", "volley_spray");
        var tempo = Fight(WholeBranch(Branch.Tempo), wave(), "hammer_blow", "volley_spray");

        Assert.True(tempo.DurationMs < neutral.DurationMs,
            $"Tempo took {tempo.DurationMs}ms against {neutral.DurationMs}ms. The branch that exists to " +
            "kill a Caster before it acts is not faster than not taking it.");
    }

    /// <summary>PREPARATION removes the opening wait, so the first cast lands sooner.</summary>
    [Fact]
    public void test_preparation_frees_the_opening_cast()
    {
        // NOT JAWS: a Reaction fires only when bitten, so it never walks the cooldown path this node
        // changes. 200 health: one BLOW kills it, so the kill IS the opening cast — with PREPARATION on
        // the wave's first beat, without it on BLOW's own sixth (the beat model, 2026-08-27). At 300
        // both builds needed the same number of beats and the swing masked the opener.
        var wave = () => Wave(1, 200f, 5f);

        var waited = Fight(SkillShape.None, wave(), NoSwing, "hammer_blow");
        var ready = Fight(SkillShape.None with { FreeOpeningCast = true }, wave(), NoSwing, "hammer_blow");

        Assert.True(ready.DurationMs < waited.DurationMs,
            "The opening cast still waited a full cooldown — PREPARATION does nothing.");
    }

    /// <summary>ASSASSINATE kills outright, which a damage bonus cannot do.</summary>
    [Fact]
    public void test_assassinate_finishes_a_weakened_creature()
    {
        var weakened = Wave(1, 20000f, 10f);
        weakened[0].Health = 3000f;   // 15% — under the threshold

        var m = Fight(SkillShape.None with { AssassinateThreshold = 0.40f }, weakened, "hammer_blow");

        Assert.Equal(1, m.CreaturesKilled);
        // The opening cast waits one full cooldown — BLOW's own Beats, in beat time; the kill must be
        // THAT cast.
        Assert.True(m.DurationMs <= SkillCatalogue.ById("hammer_blow").Beats * SoloBattle.DefaultBeatMs + 200,
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

    /// <summary>LEECH turns damage dealt into health.</summary>
    [Fact]
    public void test_leech_returns_health()
    {
        var wave = () => Wave(3, 4000f, 120f);

        var dry = Fight(SkillShape.None, wave());
        var healing = Fight(SkillShape.None with { HealPerTargetStruck = 0.25f }, wave());

        Assert.True(healing.HealthLost < dry.HealthLost);
    }

    // ── THE ACCEPTANCE TEST FOR THE WHOLE DESIGN. ─────────────────────────────────────────────────

    // TWO TESTS RETIRED 2026-08-30, with the branches they defended. They asserted the WEIGHT /
    // SPREAD opposition — hit size against target count, one branch winning the Armoured wave and
    // stalling in the Swarm — and the re-axe deleted both branches for the very reason those tests
    // were the last defenders of: under the skill rework the enemy BANDS are answered by SKILLS
    // (HAMMER's defence break answers Armoured, VOLLEY's hit count and FIELD's area damage answer
    // Swarm), so a branch selling the same rule was charging for it twice.
    //
    // THE CLAIM DID NOT VANISH, IT MOVED. "A build shaped one way is worse when the content asks the
    // opposite question" is now carried by the skills, and VariationLivenessTests is what holds it:
    // SPLAY and CLUSTER are the same skill choosing reach or concentration, and each must change the
    // fight. If that ever stops being true, this file is not where it will be caught.

    // ── THE SIDE ROADS' TWO NEW FIELDS. Each one is a new read in the sim, so each gets the "does the
    //    fight actually read it" test that the rest of this file exists for. ──────────────────────

    /// <summary>
    /// THORNS — a creature that bites takes a share of its own bite back, so a big biter dies sooner.
    /// </summary>
    /// <remarks>
    /// The champion's own damage is identical in both runs (same skills, same seed), so any change in
    /// how long the creature lasts is the thorns and nothing else. 3,000 health, not more: a pool a
    /// lone BLOW build cannot empty inside the 120-second ceiling makes BOTH runs stall at the ceiling
    /// and the metric saturates — the same fixture trap the class remarks describe for health.
    /// </remarks>
    [Fact]
    public void test_thorns_return_a_share_of_every_bite()
    {
        var bruiser = () => Wave(1, 3_000f, 300f, archetype: Archetype.Bruiser);

        var plain = Fight(SkillShape.None, bruiser(), "hammer_blow");
        var thorns = Fight(SkillShape.None with { ReflectFraction = 0.20f }, bruiser(), "hammer_blow");

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

        var light = Fight(shape, Wave(1, 3_000f, 100f), "hammer_blow");
        var heavy = Fight(shape, Wave(1, 3_000f, 600f), "hammer_blow");

        Assert.True(light.DurationMs < ExpeditionTuning.Default.TickCeilingMs
                    && heavy.DurationMs < ExpeditionTuning.Default.TickCeilingMs,
            "A fixture that stalls at the ceiling measures the ceiling, not the node.");

        Assert.True(heavy.DurationMs < light.DurationMs,
            $"A 600-damage biter lasted {heavy.DurationMs}ms and a 100-damage biter {light.DurationMs}ms " +
            "under the same THORNS. The return is not reading the bite.");
    }
}
