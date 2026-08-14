using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using ResonanceHunter.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

namespace ResonanceHunter.Core.Tests.Builds;

/// <summary>
/// The balance sweep: how far each mastery branch, each trait road and each Vow actually gets you.
/// </summary>
/// <remarks>
/// <para>
/// The trees and the Vows have never been measured against each other. Each was tuned on its own, and
/// "four opposed branches" is a claim about relative strength that nothing has ever checked.
/// </para>
/// <para>
/// Three rules from earlier work in this project are baked in, because breaking any of them produces a
/// confident number that means nothing:
/// </para>
/// <list type="bullet">
/// <item><b>MANY INDEPENDENT SEEDS.</b> Comparing two builds off one seeded stream is not a paired
///   comparison: a changed weight shifts how many draws each roll consumes and everything downstream
///   decorrelates. Each configuration gets its own fresh <see cref="Random"/> per seed, and the seeds
///   are the same set for every configuration.</item>
/// <item><b>THE TAIL, NOT THE MEAN.</b> A run has a floor — even a naked build clears the first waves —
///   so the mean is dominated by the part of the distribution every build shares. The 25th percentile
///   and the median say more about a build than its average does.</item>
/// <item><b>NEVER SATURATE THE METRIC.</b> A depth cap that builds actually reach turns the measurement
///   into "did it hit the cap", which is how six shape tests once "failed" while measuring nothing.
///   The cap here is far above anything these budgets reach, and the assertions check it was not hit.</item>
/// </list>
/// </remarks>
public class BalanceSweepTests
{
    private readonly ITestOutputHelper _out;

    public BalanceSweepTests(ITestOutputHelper output) => _out = output;

    /// <summary>Points a mid-career player has actually earned. Roughly six regions at depth 20.</summary>
    private const int Budget = 18;

    private const int Seeds = 40;
    private const int DepthCap = 400;

    private static EquippedSkill Sk(Form form, Source src = Source.Nature)
        => new(new WovenAbility { Name = form.ToString(), Source = src, Form = form, Vow = null },
               FormBehaviour.BaseCooldownMs(form));

    /// <summary>A four-skill build — the shape a real loadout has — with a given shape applied.</summary>
    private static Build BuildWith(SkillShape shape, BuildMods? mods = null, Vow? vow = null)
    {
        var b = new Build { PassiveMods = mods ?? BuildMods.None, Shape = shape };
        foreach (var f in new[] { Form.Strike, Form.Projectile, Form.Aura, Form.Mark })
            b.Weave(vow is null
                ? Sk(f)
                : new EquippedSkill(
                    new WovenAbility { Name = f.ToString(), Source = Source.Nature, Form = f, Vow = vow },
                    FormBehaviour.BaseCooldownMs(f)));
        return b;
    }

    /// <summary>
    /// A MID-CAREER hunter, trained the way a player with eighteen mastery points would be.
    /// </summary>
    /// <remarks>
    /// The first version of this fixture used <c>new Hunter()</c> — an untrained level-one character —
    /// and every branch scored an identical depth of 4, with zero variance across forty seeds. The
    /// metric was not saturated; it was FLOORED, which is the same failure upside down. A champion that
    /// dies on wave four measures nothing about a tree, and it biases hard toward the one branch that
    /// buys survival: ENDURE scored 12 against everyone else's 4, purely because the fixture was
    /// desperate rather than because Endure is three times a branch.
    ///
    /// Trained ranks put the character where the eighteen-point budget says they are, so a run lasts
    /// long enough for a damage branch and a defence branch to be compared at all.
    /// </remarks>
    private static Hunter MidCareerHunter()
    {
        var h = new Hunter();
        h.AddGleam(2_000_000);
        foreach (var stat in new[]
                 {
                     HunterStat.MaxHealth, HunterStat.AttackPower, HunterStat.Focus,
                     HunterStat.Vitality, HunterStat.Defense, HunterStat.ResonanceAffinity,
                 })
            for (var i = 0; i < 20; i++)
                h.Train(stat);
        return h;
    }

    /// <summary>
    /// One run, varied by RUN INDEX rather than by the injected Random.
    /// </summary>
    /// <remarks>
    /// The first version passed forty different <c>Random</c> seeds and got forty identical depths —
    /// p25, median and p75 all equal, for every configuration. The percentile machinery was decorative
    /// because there was nothing to take percentiles OF.
    ///
    /// The reason is in SoloExpedition: a wave's composition and affixes come from
    /// <c>Bands.Seed(region, wave, RunIndex)</c>, not from the injected stream. That is deliberate and
    /// correct — a restart must not re-roll the wave — but it means the knob that actually varies a run
    /// is RunIndex. The injected Random only jitters the resolution inside a wave, which is not enough
    /// to move the wave you die on.
    /// </remarks>
    private static int DepthOn(Build build, Hunter hunter, int runIndex, string region = "verdant_hollow",
                               int ceilingMs = 0, ExpeditionTuning? tune = null)
    {
        // The shared mint, NOT a fourth copy of it. This harness open-coded the pool at first and so
        // measured every build with a Vow's health price unpaid and every health multiplier discarded —
        // which is how RECKLESS OFFERING came out ahead: it was being measured for free.
        var hp = SoloBattle.ChampionHealth(build, hunter);
        var tuning = tune ?? (ceilingMs > 0
            ? ExpeditionTuning.Default with { TickCeilingMs = ceilingMs }
            : ExpeditionTuning.Default);
        var run = new SoloExpedition(build, new Champion { MaxHealth = hp, Health = hp }, hunter,
                                     enemyBaseHealth: 120f, enemyBaseDamage: 9f,
                                     tuning, enemySource: null,
                                     rng: new Random(9_000 + runIndex))
        {
            RegionId = region,
            RunIndex = runIndex,
        };
        while (!run.Over && run.Wave < DepthCap) run.PushWave();
        return run.Wave;
    }

    private readonly record struct Spread(int P25, int Median, int P75, double Mean, int Max)
    {
        public override string ToString() =>
            $"p25 {P25,3}  med {Median,3}  p75 {P75,3}  mean {Mean,6:0.0}";
    }

    private static Spread Measure(Build build, Hunter? hunter = null, string region = "verdant_hollow",
                                  int ceilingMs = 0, ExpeditionTuning? tune = null)
    {
        var h = hunter ?? MidCareerHunter();
        var depths = Enumerable.Range(0, Seeds)
            .Select(i => DepthOn(build, h, i, region, ceilingMs, tune)).OrderBy(d => d).ToArray();
        return new Spread(depths[depths.Length / 4], depths[depths.Length / 2],
                          depths[depths.Length * 3 / 4], depths.Average(), depths[^1]);
    }

    /// <summary>
    /// Spend a budget down ONE branch, cheapest-first, honouring prerequisites — which is how a player
    /// actually walks a branch and therefore the only spend that measures what the branch offers.
    /// </summary>
    private static SkillShape WalkBranch(Branch branch, int budget)
    {
        var taken = new MasteryTree();
        taken.SetEarned(budget);
        // Cheapest first: the tree funnels four minors into three notables into two greaters, so cost
        // order IS walk order. Repeated until nothing else is affordable, because taking one node can
        // unlock another of the same price.
        bool progress;
        do
        {
            progress = false;
            foreach (var n in MasteryCatalog.Nodes
                         .Where(n => n.Branch == branch && n.Link is null)
                         .OrderBy(n => n.Cost).ThenBy(n => n.Ring))
            {
                if (taken.IsTaken(n.Id) || !taken.CanTake(n.Id)) continue;
                taken.Take(n.Id);
                progress = true;
            }
        } while (progress);
        return taken.Shape();
    }

    // ── The sweeps ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Four branches, one point budget, and none of them allowed to be the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on a GEARED fixture, which is the whole reason this test says anything. A fixture
    /// wearing nothing is damage-starved: waves last forever, the champion eats every hit in the band,
    /// and the defence branch wins by default at 2.5x the worst — a fact about the fixture, not the
    /// tree. A player holding eighteen mastery points is, by construction, not naked. The ungeared
    /// spread is still printed, because watching it move is how you tell a real change from a fixture
    /// artefact, but it is not what the gate reads.
    /// </para>
    /// <para>
    /// The investigation that set the threshold is recorded where its conclusion lives —
    /// <c>ExpeditionTuning.EnemyDamageScaleBase</c> — and not re-run here. It swept the enemy scaling
    /// landscape, knocked out ENDURE's fields one at a time, and tightened the stall ceiling from 120s
    /// to 25s; only one of the three moved the ratio. A test that keeps running the two dead ends is
    /// paying for them on every commit.
    /// </para>
    /// </remarks>
    [Fact]
    public void test_no_mastery_branch_dominates_at_the_same_point_budget()
    {
        var bare = Enum.GetValues<Branch>()
            .ToDictionary(br => br, br => Measure(BuildWith(WalkBranch(br, Budget))));
        var bareBase = Measure(BuildWith(SkillShape.None));
        _out.WriteLine($"UNGEARED BASELINE             {bareBase}");
        foreach (var (b, sp) in bare) _out.WriteLine($"UNGEARED {b,-8} @ {Budget}     {sp}");

        _out.WriteLine("");
        var geared = new BuildMods(3.0f, 1.4f, 1f, 1f, 1f);
        var gearedBase = Measure(BuildWith(SkillShape.None, geared));
        var rows = Enum.GetValues<Branch>()
            .ToDictionary(br => br, br => Measure(BuildWith(WalkBranch(br, Budget), geared)));
        _out.WriteLine($"GEARED BASELINE               {gearedBase}");
        foreach (var (b, sp) in rows) _out.WriteLine($"GEARED {b,-8} @ {Budget}       {sp}");

        // PER REGION, because "four opposed branches answering four archetypes" is a claim about
        // SITUATIONAL strength. A branch built to answer Armoured bands is supposed to be unremarkable
        // where there are none — averaging over one region's band cycle would score exactly the branch
        // the design intends to be conditional as simply weak.
        _out.WriteLine("");
        _out.WriteLine($"{"REGION",-16}{"Weight",9}{"Spread",9}{"Tempo",9}{"Endure",9}   winner");
        foreach (var def in ResonanceHunter.Core.Encounters.Regions.All)
        {
            var per = Enum.GetValues<Branch>()
                .ToDictionary(br => br, br => Measure(BuildWith(WalkBranch(br, Budget), geared), null, def.Id).Median);
            var win = per.MaxBy(kv => kv.Value);
            _out.WriteLine($"{def.Name,-16}{per[Branch.Weight],9}{per[Branch.Spread],9}"
                           + $"{per[Branch.Tempo],9}{per[Branch.Endure],9}   {win.Key}");
        }

        Assert.True(rows.Values.All(s => s.Max < DepthCap),
                    "a build reached the depth cap — the metric is saturated and proves nothing");

        foreach (var (b, s) in rows)
            Assert.True(s.Median > gearedBase.Median,
                        $"{b} at {Budget} points is no better than an untouched tree");

        var best = rows.MaxBy(kv => kv.Value.Median);
        var worst = rows.MinBy(kv => kv.Value.Median);
        var ratio = best.Value.Median / (double)Math.Max(1, worst.Value.Median);
        _out.WriteLine("");
        _out.WriteLine($"SPREAD  best {best.Key} {best.Value.Median}  worst {worst.Key} {worst.Value.Median}  ratio {ratio:0.00}");

        // 1.40, against a measured 1.33. The slack is one wave of noise either way, not room to grow
        // into: the whole point is that the gap is small enough that WHICH branch is best depends on
        // the region and the build rather than on the branch.
        Assert.True(ratio <= 1.40,
                    $"{best.Key} reaches {ratio:0.00}x the depth of {worst.Key} on the same budget — "
                    + "'four opposed branches' is a claim about relative strength, and this is not four choices");
    }

    [Fact]
    public void test_a_builds_health_multiplier_reaches_the_champion()
    {
        // Pins the mint: the Hunter's pool, the build's health multipliers, and a Vow's health price,
        // composed once and in that order. The bug this guards against is not a wrong number, it is a
        // FOURTH open-coded copy of the expression — which is what went wrong last time, in the balance
        // harness below, where a missing term made health look worthless and RECKLESS OFFERING look free.
        // Asserted at the helper rather than through depth because a mint is exact and depth is noisy.
        var hunter = MidCareerHunter();
        var plain = BuildWith(SkillShape.None);
        var baseline = SoloBattle.ChampionHealth(plain, hunter);

        Assert.Equal(baseline, SoloBattle.ChampionHealth(BuildWith(SkillShape.None with { MaxHealth = 1f }), hunter));

        var tougher = SoloBattle.ChampionHealth(BuildWith(SkillShape.None with { MaxHealth = 1.20f }), hunter);
        Assert.Equal((int)MathF.Round(baseline * 1.20f), tougher);

        var halved = SoloBattle.ChampionHealth(BuildWith(SkillShape.None with { MaxHealth = 0.5f }), hunter);
        Assert.Equal((int)MathF.Round(baseline * 0.5f), halved);

        // And it COMPOSES with a Vow's health price rather than replacing it. Stated as a ratio against
        // the Vow measured alone, deliberately: RECKLESS OFFERING charges 15% per SKILL that wears it, so
        // this four-skill fixture pays 0.85^4 and not 0.85, and an assertion that hard-coded the single
        // price would be asserting the fixture's shape instead of the composition rule under test.
        var reckless = Weaving.Catalog.First(v => v.Id == "vow_reckless_offering");
        var vowOnly = SoloBattle.ChampionHealth(BuildWith(SkillShape.None, vow: reckless), hunter);
        var both = SoloBattle.ChampionHealth(
            BuildWith(SkillShape.None with { MaxHealth = 0.5f }, vow: reckless), hunter);
        Assert.InRange(both, (int)MathF.Round(vowOnly * 0.5f) - 1, (int)MathF.Round(vowOnly * 0.5f) + 1);
        Assert.True(vowOnly < baseline, "a Vow with a health price must cost health");
    }

    [Fact]
    public void test_a_health_pool_is_worth_something()
    {
        // The behavioural half of the pair above: depth must respond to the size of the pool AT ALL.
        // It read 39 at -5% and 39 at -30% while the harness was minting its own champion, which is the
        // signature of a multiplier going nowhere. Measured on WEIGHT, not ENDURE: the ENDURE fixture is
        // stall-bound and reports the same depth however much health you take off it, so it is blind to
        // exactly the failure under test.
        var geared = new BuildMods(3.0f, 1.4f, 1f, 1f, 1f);
        var weight = WalkBranch(Branch.Weight, Budget);

        var whole = Measure(BuildWith(weight, geared)).Median;
        var gutted = Measure(BuildWith(weight with { MaxHealth = 0.5f }, geared)).Median;
        _out.WriteLine($"WEIGHT full health {whole} · half health {gutted}");

        Assert.True(gutted < whole,
                    $"halving maximum health changed nothing (both {whole}) — the multiplier is not reaching the sim");
    }

    /// <summary>A build and a Hunter that actually satisfy <paramref name="v"/>'s demand, or null.</summary>
    /// <remarks>
    /// <para>
    /// PER VOW, not one shared fixture, and this is the correction that changed the answer. Measuring
    /// every Vow against one generic build concluded that five of them were worthless — of course it
    /// did: a conditional Vow pays NOTHING while its condition is unmet, so a four-Form build scores
    /// VOW OF THE SINGULAR at exactly zero and the harness reports a design problem that is really a
    /// fixture problem. A Vow can only be asked what it is worth while it is active.
    /// </para>
    /// <para>
    /// The demand is not assumed to be met, either — the caller re-derives the context through
    /// <c>SoloBattle.DescribeBuild</c> and asserts <c>Weaving.IsActive</c> before trusting the number,
    /// so a fixture that silently stops satisfying its Vow fails loudly instead of quietly measuring an
    /// inert one.
    /// </para>
    /// </remarks>
    private static (Build Build, Hunter Hunter)? SatisfyingFixture(Vow v)
    {
        var hunter = MidCareerHunter();
        var geared = new BuildMods(3.0f, 1.4f, 1f, 1f, 1f);
        var forms = new[] { Form.Strike, Form.Projectile, Form.Aura, Form.Mark };

        Build Weave(SkillShape shape, IEnumerable<Form> fs, Source source = Source.Nature)
        {
            var b = new Build { PassiveMods = geared, Shape = shape };
            foreach (var f in fs)
                b.Weave(new EquippedSkill(
                    new WovenAbility { Name = f.ToString(), Source = source, Form = f, Vow = v },
                    FormBehaviour.BaseCooldownMs(f)));
            return b;
        }

        switch (v.Demand)
        {
            // Static-cost Vows demand nothing, so any build measures them honestly.
            case VowDemand.None:
                return (Weave(SkillShape.None, forms), hunter);

            case VowDemand.SingleForm:
                return (Weave(SkillShape.None, new[] { Form.Strike }), hunter);

            case VowDemand.SingleSource:
                return (Weave(SkillShape.None, forms), hunter);

            case VowDemand.EveryWeaveFilled:
                return (Weave(SkillShape.None, forms), hunter);

            // Crit must sit at base, so the Hunter may not have trained FOCUS.
            case VowDemand.NoCritInvestment:
            {
                var h = new Hunter();
                h.AddGleam(2_000_000);
                foreach (var stat in new[]
                         {
                             HunterStat.MaxHealth, HunterStat.AttackPower,
                             HunterStat.Vitality, HunterStat.Defense, HunterStat.ResonanceAffinity,
                         })
                    for (var i = 0; i < 20; i++) h.Train(stat);
                return (Weave(SkillShape.None, forms), h);
            }

            case VowDemand.CadenceAtOrBelow:
                return (Weave(SkillShape.None with { SkillRate = 0.5f }, forms), hunter);

            case VowDemand.CadenceAtOrAbove:
                return (Weave(SkillShape.None with { SkillRate = 2.0f }, forms), hunter);

            // Defence must be ZERO, which no trained Hunter has — so this one is untrained by necessity
            // and its depths are not comparable with the rest. It is still measured against its OWN
            // control, which is the only comparison the test makes.
            case VowDemand.NoDefence:
            {
                var h = new Hunter();
                h.AddGleam(2_000_000);
                foreach (var stat in new[]
                         {
                             HunterStat.MaxHealth, HunterStat.AttackPower, HunterStat.Focus,
                             HunterStat.Vitality, HunterStat.ResonanceAffinity,
                         })
                    for (var i = 0; i < 20; i++) h.Train(stat);
                return (Weave(SkillShape.None, forms), h);
            }

            // The Hunter wears nothing in the fixture, so every slot is already bare.
            case VowDemand.SlotLeftBare:
                return (Weave(SkillShape.None, forms), hunter);

            // No keystones are socketed on a Build nobody socketed one into.
            case VowDemand.NoKeystone:
                return (Weave(SkillShape.None, forms), hunter);

            default:
                return null;
        }
    }

    [Fact]
    public void test_every_vow_is_worth_swearing_while_its_demand_is_met()
    {
        var skipped = new List<string>();
        var rows = new List<(Vow V, Spread S, Spread Without)>();

        foreach (var v in Weaving.Catalog)
        {
            if (SatisfyingFixture(v) is not { } fx) { skipped.Add(v.Name); continue; }

            // The control is the SAME fixture with the Vow removed, not a shared baseline. A Vow that
            // demands one Form is measured against a one-Form build, so the number is the Vow's
            // contribution rather than the cost of the demand.
            var bare = new Build { PassiveMods = fx.Build.PassiveMods, Shape = fx.Build.Shape };
            foreach (var s in fx.Build.Skills)
                bare.Weave(new EquippedSkill(
                    new WovenAbility { Name = s.Ability.Name, Source = s.Source, Form = s.Form, Vow = null },
                    FormBehaviour.BaseCooldownMs(s.Form)));

            // Prove the demand IS met before trusting the number.
            var ctx = SoloBattle.DescribeBuild(fx.Build, fx.Hunter);
            Assert.True(Weaving.IsActive(v, ctx),
                        $"the fixture for {v.Name} does not satisfy its own demand — the measurement "
                        + "would be of an inactive Vow");

            rows.Add((v, Measure(fx.Build, fx.Hunter), Measure(bare, fx.Hunter)));
        }

        foreach (var (v, s, w) in rows.OrderByDescending(r => r.S.Median - r.Without.Median))
            _out.WriteLine($"{v.Name,-26} x{Weaving.VowMultiplier(v, WeavingTuning.Default):0.00}  "
                           + $"with {s.Median,3}  without {w.Median,3}  gain {s.Median - w.Median,+3}");
        if (skipped.Count > 0) _out.WriteLine($"SKIPPED (no fixture): {string.Join(", ", skipped)}");

        Assert.True(rows.All(r => r.S.Max < DepthCap), "the metric is saturated");
        foreach (var (v, s, w) in rows)
            Assert.True(s.Median > w.Median,
                        $"{v.Name} is worth x{Weaving.VowMultiplier(v, WeavingTuning.Default):0.00} and "
                        + $"buys NOTHING — {s.Median} deep with it, {w.Median} without");
    }

    [Fact]
    public void test_the_four_trait_roads_are_worth_comparable_amounts()
    {
        // Roads are bought with the same points and two of them are unaffordable together, so the
        // choice is only real if they are worth roughly the same.
        var tree = new MemoryDustTree();
        var costs = Enum.GetValues<TraitRoad>()
            .Where(r => r != TraitRoad.Spine)
            .ToDictionary(r => r, r => tree.All.Where(u => u.Road == r).Sum(u => u.Cost));

        foreach (var (road, cost) in costs) _out.WriteLine($"{road,-9} {cost} pts");

        var min = costs.Values.Min();
        var max = costs.Values.Max();
        Assert.True(max - min <= 2,
                    $"the roads cost {min}..{max} points — a road that costs more must be worth more, "
                    + "and nothing in the design says which one is meant to be dearer");
    }
}
