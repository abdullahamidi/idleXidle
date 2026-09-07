using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Sources;
using IdleXIdle.Core.Tests.Builds;
using IdleXIdle.Core.Traits;
using Xunit;
using Xunit.Abstractions;

namespace IdleXIdle.Core.Tests.Traits;

/// <summary>
/// THE DISCOVERY CURVE, MEASURED — how many traits an account actually owns at each age of play.
/// </summary>
/// <remarks>
/// <para>
/// The complaint this bench exists to hold a number against: <b>traits awaken too fast</b>. A player
/// sees a burst of reveals in the first sitting, which spends the mystery the whole system is built
/// on. That is a claim about PACING, and pacing is a curve — so this file measures the curve rather
/// than arguing about it.
/// </para>
/// <para>
/// <b>Real waves, not counter pokes.</b> Every number below comes out of the same engine the game
/// runs: a <see cref="SoloExpedition"/> per descent, a real <see cref="SoloBattle"/> inside each
/// wave, and a real <see cref="TraitWatch"/> folding each CLEARED wave into a real
/// <see cref="TraitLedger"/> through <see cref="TraitDiscovery.OnWaveCleared"/>. Hand-written
/// <c>ledger.Add</c> calls would only re-state the thresholds back to us; the whole question is what
/// the FIGHT feeds them, and only the fight can answer it.
/// </para>
/// <para>
/// <b>One career, six checkpoints.</b> Discovery is account-wide and every counter is monotone, so an
/// account's trait total is cumulative by construction — measuring six unrelated accounts would answer
/// a different question. The six profiles are therefore six STAGES of one continuous career sharing
/// one ledger: the FRESH row is the first sitting, and each later row is the same account after more
/// play, with the training, gear, region, weave and account facts an account of that age actually has.
/// </para>
/// <para>
/// <b>Deterministic.</b> No wall clock, no unseeded <see cref="Random"/>, no ambient state. Each
/// descent gets <c>new Random(RngSeedBase + runIndex)</c> and its own <c>RunIndex</c> — which, as
/// <c>BalanceSweepTests</c> records, is the knob that actually varies a run, since a wave's
/// composition comes from <c>Bands.Seed(region, wave, RunIndex)</c>. The whole career is a pure
/// function of this file. <see cref="test_trait_pacing_the_same_career_measures_the_same_curve_twice"/> asserts that.
/// </para>
/// <para>
/// <b>What the assertions mean.</b> Every count asserted in
/// <see cref="test_trait_pacing_one_career_awakens_the_measured_curve"/> is marked CURRENT — it encodes the too-fast
/// behaviour as measured today, so that the retune has a baseline to move and a test that fails
/// loudly when it does. They are NOT the target. The target sits beside each one in the same comment.
/// </para>
/// </remarks>
public class trait_pacing_benchmark_test
{
    private readonly ITestOutputHelper _out;

    public trait_pacing_benchmark_test(ITestOutputHelper output) => _out = output;

    /// <summary>Far above anything these builds reach, so no row is measuring "did it hit the cap".</summary>
    private const int DepthCap = 400;

    /// <summary>The one seed base of the whole file. Every stream is derived from it and a run index.</summary>
    private const int RngSeedBase = 41_000;

    /// <summary>The run log the game keeps, and therefore the only window WHAT KILLED YOU can read.</summary>
    private const int RunLogDepth = 10;

    private const string CharacterId = "seeker";

    // ── THE CAREER ───────────────────────────────────────────────────────────────────────────────

    /// <summary>One age of one account: what it is carrying, where it hunts, and for how many descents.</summary>
    /// <param name="Name">The row's label in the printed table.</param>
    /// <param name="Why">Why this shape honestly represents that age. Printed, so the table argues for itself.</param>
    /// <param name="Ranks">Training ranks bought in each of the six stats.</param>
    /// <param name="GearDamage">The damage multiplier the gear of that age carries (<c>BuildMods.Damage</c>).</param>
    /// <param name="GearHealth">The health multiplier of the same gear.</param>
    /// <param name="Weave">Which of the four weaves this age fights with — see <see cref="BuildFor"/>.</param>
    /// <param name="GearCritPercent">Critical chance the gear of this age carries, over what training gives.</param>
    /// <param name="Vows">Whether the build has sworn the two vows an account of that age can hold.</param>
    /// <param name="RegionIndex">Which region of the ladder is being hunted.</param>
    /// <param name="Runs">How many descents this age is worth.</param>
    /// <param name="Conquered">Regions conquered by the END of this age.</param>
    /// <param name="Mastered">Regions mastered by the END of this age.</param>
    /// <param name="Sets">Five-piece sets completed by the END of this age.</param>
    private sealed record Stage(
        string Name, string Why,
        int Ranks, float GearDamage, float GearHealth, Weave Weave, float GearCritPercent, bool Vows,
        int RegionIndex, int Runs, int Conquered, int Mastered, int Sets);

    /// <summary>
    /// The four shapes of build one account walks through, in the order it walks them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bench that fights all seven ages with one weave cannot awaken the shield, reflect or critical
    /// families at all, and would report their silence as a fact about the catalogue's pacing rather
    /// than about its own fixture. These six are the honest reading of "multiple builds": the one
    /// skill a new game hands you, that skill with its variation chosen, the first skill you pair with
    /// it, a spread of sources once matchups matter, a defensive rebuild, and a critical one.
    /// </para>
    /// <para>
    /// <b>The first three are the onboarding as shipped, not a simplification of it.</b> A champion
    /// starts with ONE slot and its signature in it (<c>PlayerLoadout.Starter</c>). The signature's own
    /// variation is choosable at level 1 — four waves — and it is the only Source decision a one-skill
    /// player can make. The second slot opens at wave 5, but every shared skill sits behind a mastery
    /// road that costs SEVEN points (three trunk minors and the road itself, <c>MasteryCatalog</c>), and
    /// depth pays points as <c>floor(0.9 * sqrt(depth))</c> per region: the first conquest (wave 20) is
    /// worth four, so the first road — and with it the first PAIR — arrives early in the SECOND region,
    /// after the first conquest, not at wave 5. This bench used to fight the FRESH age with a four-skill
    /// quartet, which meant the one state the PURE counter's semantics turn on — a build with nothing to
    /// have chosen between — was never posed at all, and the counter's "a lone skill agrees with itself"
    /// reading measured as pacing rather than as the fact bug it was.
    /// </para>
    /// <para>
    /// <b>The pair is CHOSEN, not merely woven.</b> The weave screen sets no Source; every slot is seeded
    /// BODY and the variation is the only Source lever there is. A pair with nothing chosen fights as
    /// BODY+BODY and is not a commitment (<c>Build.PureSource</c>, the rule VOW OF THE PURE's proof
    /// uses), so the FIRST CHOICE age takes both variations — HARD HANDS as OPEN HAND and BLOW as
    /// FLATTEN, both BODY — exactly as a player who has decided would.
    /// </para>
    /// </remarks>
    private enum Weave
    {
        /// <summary>The champion's own signature, alone and unchosen — the one skill a new game hands you.</summary>
        Starter,

        /// <summary>The signature alone, its variation chosen — the one Source decision a one-skill player can make.</summary>
        Signature,

        /// <summary>The signature and the first road's skill, both chosen to the same source — the first thing a player can commit to.</summary>
        FirstPair,

        /// <summary>Four elements across the quartet — the weave of a player who has learned matchups.</summary>
        Motley,

        /// <summary>The defensive rebuild: BANKED repay for shield, NET jaws for reflect.</summary>
        Defence,

        /// <summary>The critical rebuild: the motley quartet, on gear rolled for critical chance.</summary>
        Critical,
    }

    /// <summary>
    /// The seven ages, in the order one account lives them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wave counts are not invented: each stage's <c>Runs</c> is chosen so the CUMULATIVE descent
    /// count at each checkpoint matches what the brief describes, and the printed table reports the
    /// cleared-wave total each one actually produced, so the reasoning can be checked against the
    /// measurement rather than trusted.
    /// </para>
    /// <list type="bullet">
    /// <item><b>FRESH</b> — a first sitting. Nothing trained, no gear, the starting region, the one
    ///   skill the game hands you, and three descents, which is about what a first session is before
    ///   the player stops to spend anything.</item>
    /// <item><b>EARLY</b> — around the first region conquest. Ten ranks in the three stats a new player
    ///   buys first, the first gear worth wearing, still the starting region, the signature's variation
    ///   chosen (the only Source decision on offer), and the conquest itself granted at the end of the
    ///   age. Still ONE skill: the slots have opened, but the first road is seven points away.</item>
    /// <item><b>FIRST CHOICE</b> — the second region, the first road bought, its skill woven beside the
    ///   signature and its variation chosen to the SAME source. The first decision about sources an
    ///   account can make, and the earliest a PURE wave can honestly exist. Fifteen ranks, better gear,
    ///   twelve descents — about the stretch between the first road and the second.</item>
    /// <item><b>EARLY-MID</b> — "multiple regions and builds". The weave changes here: the player has
    ///   learned matchups and moves off one element onto four, which is a different account of the same
    ///   character, and hunts the second region.</item>
    /// <item><b>MID</b> — the account has vow capacity and a completed set, has mastered one region, and
    ///   is deep in the second/third. Two vows sworn, both kept.</item>
    /// <item><b>LATE</b> — the third region, four conquests, two masteries, and the DEFENSIVE rebuild:
    ///   the account that has been dying to the back half puts on a shield and a reflect.</item>
    /// <item><b>DEVELOPED</b> — near-full breadth: fourth region, five conquests, three masteries, the
    ///   CRITICAL rebuild on gear rolled for it, and the longest stretch of play in the file. This is
    ///   the row the "FULL/NEAR-FULL is substantial breadth of play" benchmark is about.</item>
    /// </list>
    /// <para>
    /// <b>Deliberately conservative.</b> No mastery-tree walk is applied at any age — the tree grants
    /// skill ACCESS only (every road, no minors or notables), and the only shapes carried are the
    /// seeker's own passive (EVEN HAND, +8%) and the critical chance the DEVELOPED row's gear rolls,
    /// which stands in for the crit affixes an endgame account wears (<c>BuildContext.CritPercent</c>
    /// reads exactly those three terms). A real account of each age has also spent mastery points.
    /// That makes every count below a LOWER BOUND on the real curve: the true pacing is at least this
    /// fast.
    /// </para>
    /// <para>
    /// <b>Every build is one the player can make</b> (<see cref="Reachable"/>): composed through the
    /// loadout, the progress and the composer, with every Source a CHOSEN variation's. The bench used to
    /// hand its quartets a Source per slot — a lever the weave screen retired — and three of their four
    /// skills were actives, which the composer's two-active budget refuses; the curve had been tuned
    /// against builds nobody could weave.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<Stage> Career() => new[]
    {
        new Stage("FRESH", "first sitting: untrained, no gear, the signature alone and unchosen, 3 descents",
                  Ranks: 0, GearDamage: 1f, GearHealth: 1f, Weave: Weave.Starter, GearCritPercent: 0f, Vows: false,
                  RegionIndex: 0, Runs: 3, Conquered: 0, Mastered: 0, Sets: 0),

        new Stage("EARLY", "around the first conquest: 10 ranks, first gear, still region 1, the signature alone with OPEN HAND chosen",
                  Ranks: 10, GearDamage: 1.2f, GearHealth: 1.1f, Weave: Weave.Signature, GearCritPercent: 0f, Vows: false,
                  RegionIndex: 0, Runs: 12, Conquered: 1, Mastered: 0, Sets: 0),

        new Stage("FIRST CHOICE", "the second region and the first road: BLOW as FLATTEN beside OPEN HAND, 15 ranks, 12 descents",
                  Ranks: 15, GearDamage: 1.35f, GearHealth: 1.15f, Weave: Weave.FirstPair, GearCritPercent: 0f, Vows: false,
                  RegionIndex: 1, Runs: 12, Conquered: 1, Mastered: 0, Sets: 0),

        new Stage("EARLY-MID", "multiple regions and builds: 20 ranks, the weave moves to four elements",
                  Ranks: 20, GearDamage: 1.5f, GearHealth: 1.2f, Weave: Weave.Motley, GearCritPercent: 0f, Vows: false,
                  RegionIndex: 1, Runs: 25, Conquered: 2, Mastered: 0, Sets: 0),

        new Stage("MID", "vow capacity and a completed set: 25 ranks, two vows sworn and kept, 1 mastery",
                  Ranks: 25, GearDamage: 1.8f, GearHealth: 1.25f, Weave: Weave.Motley, GearCritPercent: 0f, Vows: true,
                  RegionIndex: 1, Runs: 50, Conquered: 3, Mastered: 1, Sets: 1),

        new Stage("LATE", "the third region, and the defensive rebuild: 32 ranks, shield and reflect woven",
                  Ranks: 32, GearDamage: 2.4f, GearHealth: 1.35f, Weave: Weave.Defence, GearCritPercent: 0f, Vows: true,
                  RegionIndex: 2, Runs: 80, Conquered: 4, Mastered: 2, Sets: 1),

        new Stage("DEVELOPED", "near-full breadth: 40 ranks, the fourth region, the critical rebuild",
                  Ranks: 40, GearDamage: 3.0f, GearHealth: 1.4f, Weave: Weave.Critical, GearCritPercent: 20f, Vows: true,
                  RegionIndex: 3, Runs: 120, Conquered: 5, Mastered: 3, Sets: 2),
    };

    // ── THE FIXTURE ──────────────────────────────────────────────────────────────────────────────

    private static readonly HunterStat[] Six =
    {
        HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Vitality,
        HunterStat.Defense, HunterStat.ResonanceAffinity, HunterStat.Focus,
    };

    /// <summary>The six above plus the one the CRITICAL rebuild is entirely about.</summary>
    private static readonly HunterStat[] SixPlusCrit = Six.Append(HunterStat.CriticalChance).ToArray();

    /// <summary>The three stats a new player buys before they know what the others do.</summary>
    private static readonly HunterStat[] First3 =
    {
        HunterStat.AttackPower, HunterStat.MaxHealth, HunterStat.Vitality,
    };

    /// <summary>The seeker's own skill — the one a new game hands out, and the only one that needs no road.</summary>
    private const string Signature = "sig_seeker_hard_hands";

    /// <summary>The signature's BODY variation — the first Source decision a one-skill player can make.</summary>
    private const string SignatureVariation = "OPEN HAND";

    /// <summary>The first shared skill of the career: what the hammer road teaches, an active.</summary>
    private const string FirstShared = "hammer_blow";

    /// <summary>BLOW's BODY variation — chosen to match the signature, which is what makes the pair a commitment.</summary>
    private const string FirstSharedVariation = "FLATTEN";

    /// <summary>
    /// The four-source quartet: four variations CHOSEN to four different Sources, two actives and two
    /// fields — the slot split a four-slot account actually has.
    /// </summary>
    /// <remarks>
    /// BLOW as FLATTEN (Body) and CALL as STEADY (Spirit) — the two actives, CALL being the MARK caster
    /// THE LINGERING MARK reads — beside MIRE as NUMB (Nature) and BRAND as SPRAWL (Mind), the two
    /// fields. This used to be four skills handed a Source per slot — a lever the weave screen retired
    /// when variations took ownership of a skill's Source — and three of the four were actives, which
    /// the composer's two-active budget would have refused. A player cannot make that build; this one
    /// they can.
    /// </remarks>
    private static readonly Reachable.Slot[] Motley =
    {
        new("hammer_blow", "FLATTEN"), new("sign_call", "STEADY"), new("field_mire", "NUMB"), new("sign_brand", "SPRAWL"),
    };

    /// <summary>
    /// The defensive rebuild, still four sources: BANKED repay for shield (Machine) and NET jaws for
    /// reflect (Shadow), beside BLOW (Body) and MIRE (Nature) — two actives, a reaction and a field.
    /// </summary>
    /// <remarks>
    /// MIRE rather than a second SIGN field on purpose: a wave slowed by MIRE bites less and lasts
    /// longer, which is the shape the shield family's counters are fed by. With BRAND in that slot the
    /// career never reached STANDING PLATE at all — a fixture that could not pose the condition, not a
    /// fact about the catalogue.
    /// </remarks>
    private static readonly Reachable.Slot[] Defence =
    {
        new("hammer_blow", "FLATTEN"), new("snare_repay", "BANKED"), new("snare_jaws", "NET"), new("field_mire", "NUMB"),
    };

    private static Hunter Trained(Stage stage)
    {
        var ranksEach = stage.Ranks;
        var h = new Hunter();
        // A new player buys the three obvious stats; a settled one buys all six; and the CRITICAL
        // rebuild trains the stat its whole build is about. Nothing here is a knob invented for the
        // bench — CriticalChance is a trained stat like any other, and BuildContext.CritPercent is
        // the sum of exactly the trained stat, the gear affixes, and the set bonus.
        var stats = ranksEach <= 10 ? First3
                  : stage.Weave == Weave.Critical ? SixPlusCrit
                  : Six;
        foreach (var s in stats)
            for (var i = 0; i < ranksEach; i++)
            {
                h.AddGleam(h.NextRankCost(s));
                h.Train(s);
            }
        return h;
    }

    /// <summary>
    /// The build of one age: the quartet, its gear multipliers, its weave, and its promises.
    /// </summary>
    /// <remarks>
    /// VOW OF COMPLETION (every slot filled) and VOW OF FRAGILITY (a static cost, always on) are the
    /// pair chosen because both are KEPT by a four-element quartet — which is what
    /// <c>TraitCounter.ManyVowWaves</c> counts. A demand vow the weave breaks would count for nothing
    /// and would quietly measure zero, which is the fixture-manufactures-a-fault failure this project
    /// has paid for before.
    /// </remarks>
    private static Build BuildFor(Stage stage)
    {
        // THE ONBOARDING AS SHIPPED, then the builds a player can make. The champion's own signature
        // at the starter loadout's seed (unchosen); then the same skill with its variation taken; then
        // the first road's skill beside it, chosen to the SAME source (both actives — the first road
        // teaches an active, and two actives fit the four-slot budget the first conquest has opened by
        // then); then the four-source quartets, every Source a chosen variation's.
        var slots = stage.Weave switch
        {
            Weave.Starter => new[] { new Reachable.Slot(Signature) },
            Weave.Signature => new[] { new Reachable.Slot(Signature, SignatureVariation) },
            Weave.FirstPair => new[] { new Reachable.Slot(Signature, SignatureVariation), new Reachable.Slot(FirstShared, FirstSharedVariation) },
            Weave.Defence => Defence,
            _ => Motley,
        };
        if (stage.Vows)
            slots = slots.Select((s, i) => s with { VowId = i == 0 ? "vow_complete" : i == 1 ? "vow_fragility" : null }).ToArray();

        // COMPOSED THE WAY PLAY COMPOSES IT (see Reachable): the loadout, the progress, the vows, the
        // composer. A slot the shipped model could not fill throws here instead of fighting anyway.
        var build = Reachable.Compose(
            CharacterRoster.Get(CharacterId), slots,
            slotCapacity: stage.Conquered >= 1 ? Build.SkillSlots : slots.Length,
            knownVows: new[] { Vows.ById("vow_complete")!, Vows.ById("vow_fragility")! },
            vowCapacity: stage.Vows ? 2 : 1);

        // THE GEAR OF THE AGE rides on the composed build beside the character's own numbers.
        build.PassiveMods = build.PassiveMods.Combine(new BuildMods(stage.GearDamage, stage.GearHealth, 1f, 1f, 1f));
        if (stage.GearCritPercent > 0f)
            build.Shape = build.Shape with { BonusCritPercent = build.Shape.BonusCritPercent + stage.GearCritPercent };
        return build;
    }

    /// <summary>The enemy baseline of a region — the same arithmetic the economy bench uses.</summary>
    private static (float Health, float Damage) Baseline(int regionIndex)
    {
        var region = Regions.All[Math.Min(regionIndex, Regions.All.Count - 1)];
        var mod = RegionModifiers.For(region.Id);
        return (110f * RegionLadder.Health(regionIndex) * mod.EnemyHealthMult,
                9f * RegionLadder.Damage(regionIndex) * mod.EnemyDamageMult);
    }

    // ── THE MEASUREMENT ──────────────────────────────────────────────────────────────────────────

    /// <param name="Stage">The age.</param>
    /// <param name="Runs">Descents in this age.</param>
    /// <param name="Waves">Cleared waves in this age.</param>
    /// <param name="CumulativeWaves">Cleared waves in the career up to and including this age.</param>
    /// <param name="NewIds">The traits that awakened DURING this age, in the order they awakened.</param>
    /// <param name="Total">How many the account owns at the end of this age.</param>
    /// <param name="VowsKept">How many vows the build of this age is actually keeping — printed so a
    /// zero here is read as a broken fixture rather than as a fact about the design.</param>
    /// <param name="CritPercent">The build's critical chance, printed for the same reason: the
    /// HighCritWaves counter reads this number and nothing else.</param>
    /// <param name="PureWaves">The PURE accumulator at the END of this age — cumulative, like the
    /// ledger it is read from. Printed so the one-skill starter's zero and the first pair's climb are
    /// both visible, rather than inferred from which age THE SINGLE NOTE happened to land in.</param>
    private sealed record Checkpoint(
        Stage Stage, int Runs, int Waves, int CumulativeWaves,
        IReadOnlyList<string> NewIds, int Total, int VowsKept, float CritPercent, double PureWaves);

    /// <summary>
    /// Live one whole career, checkpointing at the end of each age.
    /// </summary>
    private static IReadOnlyList<Checkpoint> RunCareer()
    {
        var ledger = new TraitLedger();
        var watch = new TraitWatch(ledger) { CharacterId = CharacterId };
        var reports = new List<RunReport>();          // newest first, exactly as RunLog keeps it
        var bosses = 0;
        var runsWithVowKept = 0;
        var runIndex = 0;
        var cumulativeWaves = 0;

        var checkpoints = new List<Checkpoint>();
        foreach (var stage in Career())
        {
            var hunter = Trained(stage);
            var build = BuildFor(stage);
            var region = Regions.All[Math.Min(stage.RegionIndex, Regions.All.Count - 1)];
            var (enemyHealth, enemyDamage) = Baseline(stage.RegionIndex);
            var ctx = SoloBattle.DescribeBuild(build, hunter);
            var keptThisAge = Vows.KeptCount(build.Vows, ctx);

            var before = ledger.Discovered.ToHashSet(StringComparer.Ordinal);
            var newIds = new List<string>();
            var wavesThisAge = 0;

            // The account facts of this age. They are refreshed on the watch rather than copied into
            // the ledger, because TraitDiscovery reads them where they live.
            watch.RegionId = region.Id;
            watch.Account = watch.Account with
            {
                RegionsConquered = stage.Conquered,
                RegionsMastered = stage.Mastered,
                SetsCompleted = stage.Sets,
            };

            for (var r = 0; r < stage.Runs; r++, runIndex++)
            {
                var hp = SoloBattle.ChampionHealth(build, hunter);
                var run = new SoloExpedition(
                    build, new Champion { MaxHealth = hp, Health = hp }, hunter,
                    enemyBaseHealth: enemyHealth, enemyBaseDamage: enemyDamage,
                    ExpeditionTuning.Default, rng: new Random(RngSeedBase + runIndex))
                {
                    RegionId = region.Id,
                    RunIndex = runIndex,
                    EnemyBias = region.CombatBias,
                    TraitWatch = watch,
                };

                while (!run.Over && run.Wave < DepthCap)
                {
                    var waveBefore = run.Wave;
                    run.PushWave();
                    if (run.Wave == waveBefore) break;   // stalled — the same guard the balance bench uses
                    if (run.LastWaveWasBoss)
                    {
                        bosses++;
                        watch.Account = watch.Account with { BossesFelled = bosses };
                    }
                }

                wavesThisAge += run.Wave;

                // END OF DESCENT, exactly the two things the host does there: a vow kept over a whole
                // descent is banked, and the run log is what WHAT KILLED YOU reads a wall out of.
                if (keptThisAge > 0 && run.Wave > 0)
                {
                    runsWithVowKept++;
                    watch.Account = watch.Account with { RunsWithVowKept = runsWithVowKept };
                }

                reports.Insert(0, run.Report(isRecord: false));
                if (reports.Count > RunLogDepth) reports.RemoveRange(RunLogDepth, reports.Count - RunLogDepth);
                var (streak, _) = TraitDiscovery.WallStreakOf(reports);
                watch.Account = watch.Account with { WallStreak = streak };

                watch.Recheck(run.Wave);
                newIds.AddRange(watch.TakeAwakened().Where(id => before.Add(id)));
            }

            cumulativeWaves += wavesThisAge;
            checkpoints.Add(new Checkpoint(
                stage, stage.Runs, wavesThisAge, cumulativeWaves,
                newIds, ledger.DiscoveredCount, keptThisAge, ctx.CritPercent,
                ledger.Of(TraitCounter.PureWaves)));
        }

        return checkpoints;
    }

    private static string Name(string id) => TraitCatalogue.Find(id)?.Name ?? id;

    // ── THE BENCH ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_trait_pacing_one_career_awakens_the_measured_curve()
    {
        var curve = RunCareer();

        _out.WriteLine("THE DISCOVERY CURVE — one account, seven ages, real cleared waves");
        _out.WriteLine("");
        _out.WriteLine($"{"AGE",-13} {"WEAVE",-9} {"RUNS",4} {"WAVES",6} {"CUM.WAVES",9} {"VOWS",4} {"CRIT%",6} {"PURE",5} {"NEW",4} {"TOTAL",5}   OF {TraitCatalogue.All.Count}");
        foreach (var c in curve)
            _out.WriteLine($"{c.Stage.Name,-13} {c.Stage.Weave,-9} {c.Runs,4} {c.Waves,6} {c.CumulativeWaves,9} " +
                           $"{c.VowsKept,4} {c.CritPercent,6:0.0} {c.PureWaves,5:0} {c.NewIds.Count,4} {c.Total,5}");

        _out.WriteLine("");
        foreach (var c in curve)
        {
            _out.WriteLine($"── {c.Stage.Name} — {c.Stage.Why}");
            if (c.NewIds.Count == 0) _out.WriteLine("     (nothing awakened)");
            foreach (var id in c.NewIds)
            {
                var first = c.Stage.Name;
                _out.WriteLine($"     + {Name(id)}   [{id}]  first {first}");
            }
        }

        var never = TraitCatalogue.All.Select(t => t.Id)
            .Where(id => !curve[^1].NewIds.Contains(id) && curve.All(c => !c.NewIds.Contains(id)))
            .ToList();
        _out.WriteLine("");
        _out.WriteLine($"NEVER AWAKENED IN THE WHOLE CAREER ({never.Count}):");
        foreach (var id in never) _out.WriteLine($"     ? {Name(id)}   [{id}]");

        // ── THE ASSERTIONS ───────────────────────────────────────────────────────────────────────
        //
        // EVERY NUMBER BELOW ENCODES THE **CURRENT (TOO FAST)** BEHAVIOUR, NOT THE TARGET.
        // The retune's job is to move these down toward the target quoted beside each one, and to
        // rewrite this block to the new measurement. A failure here after a catalogue change is the
        // point of the file — it means the pacing moved and somebody has to say by how much.
        //
        //   AGE           CURRENT (asserted)      TARGET (the brief)
        //   FRESH         see below               0-1
        //   EARLY         see below               about 2-4 total
        //   FIRST CHOICE  see below               (the first road; the first sources decision)
        //   EARLY-MID     see below               about 5-8 total
        //   MID / LATE    see below               keeps unfolding gradually
        //   DEVELOPED     see below               substantial breadth, not reached early
        var by = curve.ToDictionary(c => c.Stage.Name, StringComparer.Ordinal);

        Assert.Equal(7, curve.Count);
        Assert.All(curve, c => Assert.True(c.Waves > 0, $"{c.Stage.Name}: cleared no waves — the fixture, not the design."));
        Assert.All(curve, c => Assert.True(c.Waves < DepthCap * c.Runs,
            $"{c.Stage.Name}: hit the depth cap, so this row measures the cap and not the career."));

        // ── FIXTURE GUARDS, not design assertions ────────────────────────────────────────────────
        //
        // A bench that cannot POSE a condition reports its own blind spot as a fact about the
        // catalogue. These three say the career really does reach the conditions the hardest rules
        // read, so a silence below is the design's silence. They must keep holding after any retune.
        Assert.Equal(0, by["FRESH"].VowsKept);
        Assert.Equal(2, by["MID"].VowsKept);
        Assert.Equal(2, by["DEVELOPED"].VowsKept);
        Assert.True(by["DEVELOPED"].CritPercent >= TraitDiscovery.HighCritPercent,
            $"the CRITICAL rebuild reads {by["DEVELOPED"].CritPercent}% — under the {TraitDiscovery.HighCritPercent}% "
            + "a wave must carry to count, so HighCritWaves would measure the fixture, not the design.");
        Assert.True(by["MID"].CritPercent < TraitDiscovery.HighCritPercent,
            "a MID account with no crit gear should NOT be banking critical waves.");

        // THE PURE COUNTER'S OWN SEMANTIC, posed at both ends. A build of ONE skill has never decided
        // anything about sources — not even with its own variation chosen — so the first two ages must
        // bank nothing. FRESH is the assertion that failed under the old "a lone skill agrees with
        // itself" reading, which had the starter banking a commitment from its first wave. And the first
        // PAIR, both variations chosen to one source, is the earliest honest commitment there is, so it
        // must bank every wave it clears from there on.
        Assert.Equal(TestBuilds.SourceOf(Signature, SignatureVariation), TestBuilds.SourceOf(FirstShared, FirstSharedVariation));
        Assert.Equal(0d, by["FRESH"].PureWaves);
        Assert.Equal(0d, by["EARLY"].PureWaves);
        Assert.Equal(by["FIRST CHOICE"].Waves, by["FIRST CHOICE"].PureWaves);

        // WHERE THE SINGLE NOTE LANDS — pinned by name, because the age totals below would hold for any
        // trio of traits and the whole point of this measurement was this one trait's placement.
        Assert.Equal(CurrentFirstChoicePure, by["FIRST CHOICE"].PureWaves);
        Assert.Contains("t_single_note", by[CurrentSingleNoteAge].NewIds);

        // CURRENT — the curve, as measured today. Each is a total, not a delta.
        Assert.Equal(CurrentFresh, by["FRESH"].Total);
        Assert.Equal(CurrentEarly, by["EARLY"].Total);
        Assert.Equal(CurrentFirstChoice, by["FIRST CHOICE"].Total);
        Assert.Equal(CurrentEarlyMid, by["EARLY-MID"].Total);
        Assert.Equal(CurrentMid, by["MID"].Total);
        Assert.Equal(CurrentLate, by["LATE"].Total);
        Assert.Equal(CurrentDeveloped, by["DEVELOPED"].Total);

        // CURRENT — the named traits of the first two ages. These are the burst the brief is about.
        Assert.Equal(CurrentFreshIds, by["FRESH"].NewIds.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal(CurrentEarlyIds, by["EARLY"].NewIds.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal(CurrentEarlyMidIds, by["EARLY-MID"].NewIds.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal(CurrentMidIds, by["MID"].NewIds.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    // ── THE CURVE (measured 2026-09-07, on builds the player can make) ───────────────────────────
    //
    // These constants are the ONLY place the discovery curve is written down. A retune edits exactly
    // this block and nothing else in the file, and a failure here means the pacing moved and somebody
    // has to say by how much.
    //
    //   AGE           RETUNE   PASS 14   NOW    TARGET
    //   FRESH           1        0        0     0-1        the honest starter (one skill) wastes too
    //                                                      little for THE SPILL, which moves one age on
    //   EARLY           3        2        2     about 2-4  THE SPILL, WHAT KILLED YOU — still one skill,
    //                                                      the signature with OPEN HAND chosen
    //   FIRST CHOICE    —        3        3                THE SINGLE NOTE lands here, and only here
    //   EARLY-MID       5        4        6     about 5-8  the reachable quartet clears 663 waves where
    //                                                      the impossible one cleared 503
    //   MID            13       12       13     gradual    MANY TONGUES lands here (700 CHOSEN four-source waves)
    //   LATE           22       22       22
    //   DEVELOPED      26       26       26     substantial breadth — 120 descents and 9,696 cleared waves
    //
    // WHAT MOVED THE ROWS, AND WHY IT IS THE FIXTURE. Pass 14 made the early ages honest (one skill
    // until the first road). This pass makes every quartet a build the player can make (Reachable):
    // four variations CHOSEN through the loadout and the composer, two actives and two passives, the
    // seeker's own passive on top. The old Motley quartet was three actives on unchosen per-slot
    // Sources — the composer would have refused its third active and the weave screen cannot set a
    // slot's Source at all. Against the pass-14 printout SEVEN traits changed age, every one by a
    // single age and every one for a fixture reason: the reachable quartet fights harder (663 waves
    // against 503 in EARLY-MID) and its variations do more of the things the counters read —
    //   CARRION WEIGHT     MID       -> EARLY-MID   (more waste per wave from FLATTEN and STEADY)
    //   LAST WORD          MID       -> EARLY-MID
    //   THE LINGERING MARK LATE      -> MID         (CALL is woven from EARLY-MID on; the old Defence
    //                                                age carried the only mark caster)
    //   PRACTISED FLESH    LATE      -> MID
    //   THE GIVEN HAND     DEVELOPED -> LATE
    //   LAST BREATH        MID       -> LATE        (a stronger build reaches the brink less often)
    //   THE THIN LINE      LATE      -> DEVELOPED   (likewise)
    // The other nineteen kept their age. No threshold was re-examined for any of this: 6 is inside
    // "about 5-8", MID and LATE are unchanged, and a stronger real build awakening a little sooner —
    // or a sturdier one reaching the brink a little later — is the design, not a defect.
    //
    // THE PURE COLUMN, which the FIRST CHOICE age exists for. Under the old "a lone skill agrees with
    // itself" reading the FRESH age banked PURE waves it had never decided on; under a reading that
    // counted RESOLVED sources a wave-5 pair banked on the BODY default before anyone had chosen
    // anything. Under Build.PureSource (two or more skills, each with its variation chosen, all one
    // source) FRESH and EARLY bank 0 and the FIRST CHOICE pair banks every wave it clears: THE SINGLE
    // NOTE (120) awakens 120 committed waves in, ten of the age's twelve descents — inside the stretch
    // between the first road and the second, on the build that earned it. The threshold stayed where
    // the retune put it: the correction moved the counter's START, and the age with it, not the count.
    // MOTLEY waves are CHOSEN four-source waves now (Build.DistinctChosenSources): the quartet ages
    // bank them because their four variations really are four Sources, not because four seeds differ.
    private const int CurrentFresh = 0;
    private const int CurrentEarly = 2;
    private const int CurrentFirstChoice = 3;
    private const int CurrentEarlyMid = 6;
    private const int CurrentMid = 13;
    private const int CurrentLate = 22;
    private const int CurrentDeveloped = 26;

    /// <summary>The PURE accumulator at the end of the FIRST CHOICE age — every wave that age cleared.</summary>
    private const double CurrentFirstChoicePure = 149;

    /// <summary>The age THE SINGLE NOTE awakens in.</summary>
    private const string CurrentSingleNoteAge = "FIRST CHOICE";

    /// <summary>
    /// The first sitting's discoveries: NONE. With the one skill a new game actually hands out, the
    /// first three descents awaken nothing at all — THE SPILL, the retune's one first-sitting
    /// discovery, moves one age on, into EARLY.
    /// </summary>
    private static readonly string[] CurrentFreshIds = Array.Empty<string>();

    /// <summary>EARLY's two discoveries, pinned by name so the prose above and the table cannot drift apart.</summary>
    private static readonly string[] CurrentEarlyIds = { "t_spill", "t_what_killed_you" };

    /// <summary>EARLY-MID's discoveries, pinned by name for the same reason — this is the row the reachable quartet moved.</summary>
    private static readonly string[] CurrentEarlyMidIds = { "t_carrion_weight", "t_deep_cut", "t_last_word" };

    /// <summary>MID's discoveries, pinned by name: MANY TONGUES lands here on CHOSEN four-source waves.</summary>
    private static readonly string[] CurrentMidIds =
    {
        "t_kept_word", "t_lingering_mark", "t_many_tongues", "t_practised_flesh",
        "t_spreading_fire", "t_unbroken_thread", "t_weight_of_vows",
    };

    // ── REACHABILITY ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_trait_pacing_every_age_is_a_build_the_player_can_make()
    {
        // BuildFor composes through Reachable, which THROWS for anything the shipped loadout model
        // cannot produce — so composing every age is the proof, and the reads below pin what each
        // age's build actually says about its sources, which is what the PURE and MOTLEY counters
        // measure.
        foreach (var stage in Career())
        {
            var build = BuildFor(stage);
            switch (stage.Weave)
            {
                case Weave.Starter:
                    Assert.Single(build.Skills);
                    Assert.Null(build.Skills[0].Variation);
                    Assert.Null(build.ChosenSingleSource);
                    break;
                case Weave.Signature:
                    Assert.Single(build.Skills);
                    Assert.NotNull(build.ChosenSingleSource);
                    Assert.Null(build.PureSource);           // one decision is not yet a commitment
                    break;
                case Weave.FirstPair:
                    Assert.Equal(2, build.Skills.Count);
                    Assert.Equal(TestBuilds.SourceOf(Signature, SignatureVariation), build.PureSource);
                    break;
                default:
                    Assert.Equal(Build.SkillSlots, build.Skills.Count);
                    Assert.All(build.Skills, s => Assert.NotNull(s.Variation));
                    Assert.Equal(TraitDiscovery.MotleySources, build.DistinctChosenSources);
                    Assert.Equal(2, build.ActiveCount);
                    Assert.Equal(2, build.PassiveCount);
                    break;
            }
            Assert.All(build.Skills, s => Assert.True(s.Variation is null || s.Variation.Source == s.Source,
                $"{stage.Name}: {s.Def.Name}'s Source is not its variation's — a Source was injected"));
        }
    }

    // ── DETERMINISM ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void test_trait_pacing_the_same_career_measures_the_same_curve_twice()
    {
        // The whole career is a pure function of this file: no wall clock, no ambient seed, no shared
        // static state. Two independent careers must agree on every cell of the table, or the numbers
        // above are opinions rather than measurements.
        static string Flatten(IReadOnlyList<Checkpoint> c) =>
            string.Join(" | ", c.Select(x =>
                $"{x.Stage.Name}:{x.Runs}:{x.Waves}:{x.Total}:{x.PureWaves}:{string.Join(",", x.NewIds)}"));

        Assert.Equal(Flatten(RunCareer()), Flatten(RunCareer()));
    }
}
