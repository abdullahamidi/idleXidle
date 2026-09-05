using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Characters;
using IdleXIdle.Core.Economy;
using IdleXIdle.Core.Expeditions;
using IdleXIdle.Core.Prestige;
using Xunit;
using Xunit.Abstractions;

// Aliased, not imported. `Source` lives in Core.Sources, and this file's namespace sits under
// ...Tests.Characters where the test assembly's own sub-namespaces shadow the short name. The
// alias is the one spelling that survives both.
using CoreSource = IdleXIdle.Core.Sources.Source;

namespace IdleXIdle.Core.Tests.Characters;

/// <summary>
/// Are the ten characters worth the same?
/// </summary>
/// <remarks>
/// <para>
/// <c>characters_roster_test.cs</c> already proves each passive is WIRED — that it writes to a channel
/// the sim reads. It cannot prove any of them is worth PLAYING. Its own strongest check passes if any
/// one of four channels is non-default, which is why THE OATHBOUND sailed through it while half of its
/// passive did nothing. Wiring is a floor, not parity.
/// </para>
/// <para>
/// So this file measures. It runs every character through one identical gauntlet using the same sim the
/// game runs, and reports what each one actually delivered. The numbers are printed on every run
/// (<c>--logger "console;verbosity=detailed"</c>) so a balance pass reads real output instead of
/// re-deriving it, and the assertions below are drawn FROM that distribution rather than from an
/// impression of what the numbers ought to be — the lesson the animation gate already taught this
/// project: a threshold set from a guess is how a gate starts lying.
/// </para>
/// <para>
/// The central finding this harness exists to keep honest: <b>characters are not commensurable on one
/// axis.</b> THE MAGPIE's passive is pure loot and scores nothing on damage; THE UNBROKEN's is a death
/// save that a survivable fixture cannot see. Measuring the roster on damage alone would report both as
/// dead weight and invite "fixing" two characters that are working exactly as designed. Each axis below
/// is therefore scored separately, and a character passes by being real on the axis it CLAIMS.
/// </para>
/// </remarks>
public class RosterParityTest
{
    private readonly ITestOutputHelper _out;

    public RosterParityTest(ITestOutputHelper output) => _out = output;

    // ── The fixture ──────────────────────────────────────────────────────────────────────────────
    //
    // Three wave shapes, because the roster specialises against wave shape and a single one would
    // flatter whoever it happens to suit. THE CHORUS scales with head-count and evaporates in a boss
    // room; THE ANVIL only refunds overkill when hits overshoot small creatures. A gauntlet of one
    // shape would rank those two by the shape chosen rather than by their worth.
    private static List<WaveCreature> Swarm() => Wave(10, health: 320f, damage: 14f, defense: 0f);
    private static List<WaveCreature> Pack() => Wave(4, health: 1_600f, damage: 34f, defense: 45f);
    private static List<WaveCreature> Boss() => Wave(1, health: 14_000f, damage: 90f, defense: 70f);

    /// <summary>
    /// The game's tuning with the anti-hang ceiling lifted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ExpeditionTuning.TickCeilingMs"/> is 120 seconds in the real game, where its job is to
    /// stop a hopeless wave hanging the loop. As a MEASUREMENT boundary it is poison: a wave that hits
    /// it reports 120s whatever the truth was, so every build slower than the ceiling records the same
    /// time and the ranking silently becomes a list of who saturated. The Form-era fixture measured its
    /// slowest build at about 18 damage a second into a single armoured target, so the 14,000-health
    /// boss took it about thirteen minutes — real, measurable, and utterly invisible under a two-minute
    /// cap.
    /// </para>
    /// <para>
    /// Shrinking the fixture instead was the first attempt and it failed the other way: with a
    /// 1,200-health boss the whole gauntlet fits in a few hundred 100ms ticks, and THE SEEKER's +8%
    /// stopped registering at all because it no longer moved any creature's death across a tick
    /// boundary. The fixture has to be heavy enough to resolve the smallest passive and unbounded
    /// enough not to clip the slowest build; lifting the ceiling is what buys both at once.
    /// </para>
    /// </remarks>
    private static readonly ExpeditionTuning Tuning =
        ExpeditionTuning.Default with { TickCeilingMs = 1_800_000 };

    private static List<WaveCreature> Wave(int count, float health, float damage, float defense)
        => Enumerable.Range(0, count).Select(_ => new WaveCreature
        {
            MaxHealth = health,
            Health = health,
            Damage = damage,
            Defense = defense,
        }).ToList();

    /// <summary>
    /// Health large enough that the gauntlet does not kill anyone.
    /// </summary>
    /// <remarks>
    /// Deliberately generous, and the reason is a mistake this suite already made once elsewhere: a
    /// fixture whose champion dies pins HealthLost at the pool size, and every defensive passive then
    /// measures identical. A metric that saturates cannot rank the thing it is measuring. Survivability
    /// is read as health lost against a pool nobody exhausts.
    /// </remarks>
    private const int ChampionHealth = 400_000;

    /// <summary>One measured run of the whole gauntlet.</summary>
    /// <remarks>
    /// <para>
    /// <b>Offence is TIME, not damage.</b> The obvious metric — total damage delivered — is a trap here,
    /// and this harness fell into it first time out. The gauntlet holds 23,600 points of enemy health;
    /// every build that clears it delivers 23,600 plus a little overkill, however strong it is. Damage
    /// is bounded by the target, so a character twice as powerful scores the same and the ±5% actually
    /// being compared is overkill wastage. Worse, the builds too weak to clear scored LOWER, so the
    /// column ranked the strongest and the weakest builds by two different quantities at once — which is
    /// how THE THORNWALL, carrying a ×1.35 multiplier on the very style it was running, came out below
    /// a no-character control.
    /// </para>
    /// <para>
    /// What actually separates them is how FAST the same health pool comes down. <see cref="Stalls"/>
    /// guards the other end: a wave that hits the tick ceiling pins the clock at the ceiling and
    /// saturates the metric, so a stalled run is reported rather than averaged into a ranking.
    /// </para>
    /// </remarks>
    private sealed record Score(string Id, string Name, int ClearMs, int Stalls, int HealthLost,
                                float Haul, float Rarity, bool DeathSave);

    /// <summary>The plainest damage skill in the catalogue — every measured build's common chassis.</summary>
    private static readonly SkillDef Chassis = SkillCatalogue.ById("hammer_blow");

    /// <summary>A mastery tree that has learned the chassis and nothing else.</summary>
    /// <remarks>
    /// <see cref="MasteryTree.RestoreTaken"/> is the load path: it validates ids only, and a SkillRoad
    /// node "teaches, and changes nothing else" (see <c>MasteryCatalog</c>) — so this tree differs from
    /// a bare one by exactly one taught skill. No shape, no trigger, no affinity rides along to
    /// contaminate the measurement.
    /// </remarks>
    private static MasteryTree ChassisTaught()
    {
        var tree = new MasteryTree();
        tree.RestoreTaken(new[] { "road_hammer" }, repair: false);   // the node that teaches hammer_blow
        return tree;
    }

    /// <summary>The control: a character that is nothing but the same starting skill.</summary>
    /// <remarks>
    /// The composer's taught-skill gate only weaves what a champion knows, and "knows" is the mastery
    /// tree plus <see cref="Character.SignatureSkillId"/> — so the no-character control is a BLANK
    /// champion born with the measured skill: same skills, same slots, and not one non-default channel.
    /// </remarks>
    private static Character Blank(SkillDef skill) => new()
    {
        // THE ID IS THE OWNER'S when the measured skill is a signature, and "(none)" otherwise. The
        // control exists to carry NO passive — no Mods, no Shape, no Grants — so the delta between a
        // champion and it is the champion. But the composer refuses a signature to anybody but its
        // owner, so a control with a made-up id would weave nothing at all and the fixture would
        // measure bare hands and call it the skill. Borrowing the id (and nothing else) keeps the
        // control passive-free AND lets it hold the skill it is measuring.
        Id = skill.OwnerCharacterId ?? "(none)", Name = "NO CHARACTER",
        Blurb = "The control. The same skills, no passive.",
        Class = ItemClass.Wanderer, Tier = ClassTier.First,
        PassiveName = "NONE", PassiveText = "Nothing at all.",
        SignatureSkillId = skill.Id,
    };

    /// <summary>
    /// One slot's pick. Every pick is woven with the same Source, which is what keeps VOW OF THE PURE
    /// (single Source) met on every build — the vow channel is judged identically for everyone.
    /// </summary>
    private static BuildComposer.SkillPick Pick(SkillDef def, Vow? vow) =>
        new(CoreSource.Nature, vow?.Id, Passive: !def.TakesABeat, SkillId: def.Id);

    /// <summary>
    /// Compose a build the way the GAME composes one — through <see cref="BuildComposer.Compose"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The old harness hand-mirrored <c>PlayerLoadout.ToBuild</c>'s three channels and carried a warning
    /// that the mirror had to move whenever the seam did. The seam moved (P3c: a character's whole
    /// contribution is <c>.Mods</c>/<c>.Shape</c>/<c>.Grants</c>, and affinity belongs to the mastery
    /// tree), so the mirror is retired: this calls the same composer the game calls, and a new channel
    /// reaches this measurement the day it reaches the sim.
    /// </para>
    /// <para>
    /// <b>THE COMMON CHASSIS.</b> Every measured build carries the plainest damage active — HAMMER's
    /// BLOW — beside the skill being measured (unless it IS the skill), woven second so the measured
    /// skill wins beat ties. Two reasons, both learned the hard way. First, half the catalogue's base
    /// lines deal nothing by themselves: the SIGNs only amplify, WEEP needs a kill to bleed from, WILT
    /// only breaks the enemies' attack — the first run of the Form-era harness measured an amplifier
    /// with nothing to amplify and reported THE OATHBOUND at 1,570 damage against a roster averaging
    /// 20,000, a broken fixture reading as a broken character. Second, a build that cannot bring the
    /// boss down runs out the tick ceiling, and a stall saturates the clock (see the clearability
    /// gate). The chassis is identical for a champion and its control, so the delta between them is
    /// still the character and nothing else.
    /// </para>
    /// </remarks>
    private static Build BuildFor(Character? c, SkillDef skill, Vow? vow)
    {
        var picks = new List<BuildComposer.SkillPick> { Pick(skill, vow) };
        if (skill.Id != Chassis.Id) picks.Add(Pick(Chassis, vow));

        // EVERY VOW FOUND. The composer refuses a vow the account has not discovered, so a parity
        // fixture that swears one must say it is known — otherwise every champion measures with no vow
        // and the comparison is of bare hands. (This used to be a Memory tree with two study nodes
        // bought; the tree is gone and discovery is the account's own.)
        var build = BuildComposer.Compose(
            ChassisTaught(), c ?? Blank(skill),
            picks, Array.Empty<string>(), slotCapacity: 4, knownVows: Vows.Catalog);

        // A pick the composer's taught-gate silently refused would measure bare hands and report the
        // number as the skill's. The fixture fails loudly instead.
        Assert.True(build.Skills.Count == picks.Count,
                    $"the fixture wove {build.Skills.Count} of {picks.Count} picks for {skill.Id} — " +
                    "a refused weave measures bare hands and calls it the skill.");
        return build;
    }

    /// <summary>
    /// How many seeds each measurement averages over.
    /// </summary>
    /// <remarks>
    /// Not decoration. A single seed cannot rank these characters: a passive that kills marginally
    /// faster draws a different number of RNG samples from that point on, so two runs of the SAME build
    /// diverge, and the divergence is the same size as the effects being measured. The first version of
    /// this harness ran one seed and reported THE THORNWALL — which carries a ×1.35 multiplier on the
    /// very style it starts with — at 5% BELOW a no-character control, which is not a balance finding,
    /// it is noise wearing one. <see cref="test_the_measurement_is_deterministic"/>
    /// keeps this number honest by measuring the floor rather than assuming it.
    /// </remarks>
    private const int Seeds = 40;

    /// <summary>Average the gauntlet over <see cref="Seeds"/> seeds.</summary>
    private static Score Measure(Character? c, SkillDef skill, Vow? vow)
    {
        double clear = 0, lost = 0;
        var stalls = 0;
        for (var s = 0; s < Seeds; s++)
        {
            var one = Run(c, skill, vow, seed: 20260814 + s * 7919);
            clear += one.ClearMs;
            lost += one.HealthLost;
            stalls += one.Stalls;
        }

        var mods = c?.Mods ?? BuildMods.None;
        return new Score(c?.Id ?? "(none)", c?.Name ?? "NO CHARACTER",
                         (int)(clear / Seeds), stalls, (int)(lost / Seeds),
                         mods.Haul, mods.Rarity,
                         (c?.Grants ?? Array.Empty<BuildTrigger>()).Contains(BuildTrigger.Undying));
    }

    /// <summary>Run the three-wave gauntlet once and total how long it took to clear.</summary>
    private static Score Run(Character? c, SkillDef skill, Vow? vow, int seed)
    {
        var champ = new Champion { MaxHealth = ChampionHealth, Health = ChampionHealth };
        var build = BuildFor(c, skill, vow);
        var hunter = new Hunter();
        var clearMs = 0;
        var stalls = 0;
        var rng = new Random(seed);

        foreach (var wave in new[] { Swarm(), Pack(), Boss() })
        {
            var metrics = new WaveMetrics();
            var (outcome, _) = SoloBattle.ResolveWave(
                champ, build, hunter, wave,
                enemyIntervalMs: 900, Tuning, rng, metrics: metrics);
            clearMs += metrics.DurationMs;
            if (outcome != WaveOutcome.Cleared) stalls++;
        }

        var mods = c?.Mods ?? BuildMods.None;
        return new Score(c?.Id ?? "(none)", c?.Name ?? "NO CHARACTER",
                         clearMs, stalls, ChampionHealth - champ.Health,
                         mods.Haul, mods.Rarity,
                         (c?.Grants ?? Array.Empty<BuildTrigger>()).Contains(BuildTrigger.Undying));
    }

    /// <summary>The skill a character is born knowing — what a fresh champion actually has woven.</summary>
    private static SkillDef PlayedSkill(Character c)
        => c.SignatureSkillId is { } id ? SkillCatalogue.ById(id) : Chassis;

    [Fact]
    public void test_single_target_throughput_per_skill_is_reported()
    {
        // NOT an assertion about balance — a measurement, printed, so the boss-wall question has a
        // number attached to it the next time anyone asks. It exists because the fixture above had to
        // be lightened twice, and the reason turned out to be a property of the skills rather than of
        // the numbers I picked: against ONE creature, the wave tools (FIELD's, VOLLEY's spread lines)
        // deliver a small fraction of what HAMMER and SNARE do. That is a legible trade for a spread
        // style — until it crosses the line from "slower on bosses" to "cannot finish a boss", which is
        // a dead end for a player who built into it and is invisible from inside the game.
        //
        // Every build carries the common BLOW chassis (see BuildFor), so a support skill's row reads as
        // "what this skill adds beside the plainest blow" — compare it against the Hammer/BLOW row,
        // which is the chassis alone.
        //
        // Deliberately assertion-free: the roster balance pass this file serves has not been playtested,
        // and this project's own notes are explicit that the enemy curve must not be retuned blind.
        // A number in the log is what a later balance pass needs; a threshold guessed today is what
        // makes a gate start lying.
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");
        const int windowMs = 60_000;

        _out.WriteLine("SINGLE-TARGET throughput — damage into one unkillable creature over 60s");
        _out.WriteLine("(the boss axis; contrast with the swarm column of the clearability table)");
        _out.WriteLine("");

        var perSkill = new List<(SkillDef Def, float Dps)>();
        foreach (var def in SkillCatalogue.All)
        {
            var champ = new Champion { MaxHealth = ChampionHealth, Health = ChampionHealth };
            var metrics = new WaveMetrics();
            // One creature with a health pool nothing can exhaust, so the run lasts the whole window
            // and the number is throughput rather than time-to-kill.
            var target = new List<WaveCreature>
            {
                new() { MaxHealth = 1e9f, Health = 1e9f, Damage = 0f, Defense = 40f },
            };
            SoloBattle.ResolveWave(champ, BuildFor(null, def, vow), new Hunter(), target,
                                   enemyIntervalMs: 900,
                                   ExpeditionTuning.Default with { TickCeilingMs = windowMs },
                                   new Random(20260814), metrics: metrics);
            perSkill.Add((def, metrics.DeliveredDamage / (windowMs / 1000f)));
        }

        foreach (var (def, dps) in perSkill.OrderByDescending(p => p.Dps))
        {
            var label = $"{def.Style}/{def.Name}";
            _out.WriteLine($"  {label,-16} {dps,10:N0} damage/sec into a single armoured target");
        }

        var best = perSkill.Max(p => p.Dps);
        var worst = perSkill.Min(p => p.Dps);
        _out.WriteLine("");
        _out.WriteLine($"  spread: {best / MathF.Max(1f, worst):0.0}x between the best and worst skill " +
                       "on the single-target axis");
    }

    [Fact]
    public void test_the_gauntlet_is_clearable_by_every_skill_it_measures()
    {
        // A STALL SATURATES THE CLOCK. A wave that runs out the tick ceiling reports that ceiling as
        // its duration, so two builds that both fail report identical times however far apart they
        // really are — and a build that fails by a hair reports slower than one that fails badly but
        // on a shorter earlier wave. Every build the parity tests touch has to actually finish the
        // fixture, or the ranking silently becomes a ranking of who saturated first.
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");

        var waves = new (string Name, Func<List<WaveCreature>> Make)[]
        {
            ("swarm", Swarm), ("pack", Pack), ("boss", Boss),
        };

        var failures = new List<string>();
        _out.WriteLine($"{"SKILL",-16} {"swarm",12} {"pack",12} {"boss",12}");
        foreach (var def in SkillCatalogue.All)
        {
            var cells = new List<string>();
            foreach (var (name, make) in waves)
            {
                var champ = new Champion { MaxHealth = ChampionHealth, Health = ChampionHealth };
                var metrics = new WaveMetrics();
                var (outcome, _) = SoloBattle.ResolveWave(
                    champ, BuildFor(null, def, vow), new Hunter(), make(),
                    enemyIntervalMs: 900, Tuning, new Random(20260814), metrics: metrics);
                cells.Add(outcome == WaveOutcome.Cleared ? $"{metrics.DurationMs,10:N0}ms" : "     STALL");
                if (outcome != WaveOutcome.Cleared) failures.Add($"{def.Name}/{name}");
            }

            var label = $"{def.Style}/{def.Name}";
            _out.WriteLine($"{label,-16} {string.Join(" ", cells)}");
        }

        Assert.True(failures.Count == 0,
                    "the fixture is too heavy for the weakest build it measures — " +
                    string.Join(", ", failures) + " ran out the lifted tick ceiling. A stalled wave " +
                    "reports the ceiling as its duration, so the clock stops ranking and starts " +
                    "recording who saturated.");
    }

    [Fact]
    public void test_the_measurement_is_deterministic()
    {
        // Licenses every delta in this file. The sim turns out not to consume RNG in a way that moves
        // this fixture — 40 seeds produce one number — which is worth ASSERTING rather than relying on:
        // the day a passive starts rolling dice, a 5% delta stops being evidence and this test is what
        // notices. It is the same discipline the animation gate settled on: measure the known-good
        // distribution, do not assume its shape.
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");

        foreach (var skill in new[] { "hammer_blow", "snare_jaws", "volley_spray" }
                     .Select(SkillCatalogue.ById))
        {
            var samples = Enumerable.Range(0, Seeds)
                                    .Select(s => (double)Run(null, skill, vow, 20260814 + s * 7919).ClearMs)
                                    .ToList();
            var mean = samples.Average();
            var spread = mean <= 0 ? 0 : (samples.Max() - samples.Min()) / mean;

            _out.WriteLine($"{skill.Name,-12} mean clear {mean,9:N0} ms   spread across {Seeds} seeds {spread,6:0.0%}");

            Assert.True(spread < 0.02,
                        $"{skill.Name}: clear time varies {spread:0.0%} across seeds. The parity deltas are " +
                        "smaller than that, so they can no longer be read as effects. Average more seeds.");
        }
    }

    [Fact]
    public void test_no_character_is_dead_weight_on_every_axis()
    {
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");
        var baseline = Measure(null, Chassis, vow);

        _out.WriteLine("ROSTER PARITY — each character on its own starting skill, against a no-character");
        _out.WriteLine("control running the SAME skills. Skills differ in raw throughput, so a shared");
        _out.WriteLine("baseline would rank the skill and call it the character.");
        _out.WriteLine("");
        _out.WriteLine($"{"CHARACTER",-20} {"SKILL",-14} {"CLEAR ms",9} {"(ctl)",9} {"FASTER",8} " +
                       $"{"HP LOST",9} {"(ctl)",9} {"HAUL",6} {"RARITY",7}  CLAIMED AXIS");
        _out.WriteLine(new string('-', 128));
        _out.WriteLine($"{baseline.Name,-20} {Chassis.Name,-14} {baseline.ClearMs,9:N0} {"—",9} {"—",8} " +
                       $"{baseline.HealthLost,9:N0} {"—",9} {1f,6:0.00} {1f,7:0.00}  ({Chassis.Name} reference)");

        var scores = new List<(Character C, Score S, Score Ctl)>();
        foreach (var c in CharacterRoster.All)
        {
            var skill = PlayedSkill(c);
            var control = Measure(null, skill, vow);
            var score = Measure(c, skill, vow);
            scores.Add((c, score, control));

            // Positive = clears faster than no character at all.
            var faster = score.ClearMs <= 0 ? 0f : 100f * ((float)control.ClearMs / score.ClearMs - 1f);
            var axis = ClaimedAxis(c);
            _out.WriteLine($"{c.Name,-20} {skill.Name,-14} {score.ClearMs,9:N0} {control.ClearMs,9:N0} " +
                           $"{faster,7:+0.0;-0.0;0.0}% {score.HealthLost,9:N0} {control.HealthLost,9:N0} " +
                           $"{score.Haul,6:0.00} {score.Rarity,7:0.00}  {axis}");
        }

        _out.WriteLine("");

        // THE ASSERTION. Not "everyone clears at the same speed" — they demonstrably should not — but
        // that every character is measurably real on at least one axis. A character that moves nothing
        // is a character whose passive is decoration, which is the failure this roster already had once.
        foreach (var (c, s, ctl) in scores)
        {
            var offence = s.ClearMs > 0 && s.ClearMs < ctl.ClearMs * 0.98f;
            var defence = s.HealthLost < ctl.HealthLost * 0.98f;
            var loot = s.Haul > 1.001f || s.Rarity > 1.001f;
            var save = s.DeathSave;

            Assert.True(offence || defence || loot || save,
                        $"{c.Name} moved NOTHING measurable: clears in {s.ClearMs:N0} ms vs control " +
                        $"{ctl.ClearMs:N0} ms, health lost {s.HealthLost:N0} vs {ctl.HealthLost:N0}, " +
                        $"haul {s.Haul:0.00}, rarity {s.Rarity:0.00}, no death save. " +
                        "Its passive is decoration.");
        }
    }

    /// <summary>Which axis a character's passive text actually promises.</summary>
    private static string ClaimedAxis(Character c) => c.Id switch
    {
        "magpie" => "LOOT (no combat claim)",
        "unbroken" => "DEATH SAVE (invisible to a survivable fixture)",
        "thornwall" => "DEFENCE",
        "oathbound" => "VOWS",
        _ => "OFFENCE",
    };

    [Fact]
    public void test_no_character_dominates_the_offensive_axis()
    {
        // The Pillar-4 sin is a strictly dominant option, and the roster is the easiest place in the
        // game to commit it: ten passives, one of which quietly multiplies everything. This does not
        // demand equality — a passive is SUPPOSED to beat no character at all — it demands that the
        // best offensive character is not so far ahead that the other nine are a mistake to pick.
        var vow = Vows.Catalog.First(v => v.Id == "vow_pure");
        var ratios = new List<(string Name, float Ratio)>();

        foreach (var c in CharacterRoster.All)
        {
            var skill = PlayedSkill(c);
            var control = Measure(null, skill, vow);
            var score = Measure(c, skill, vow);
            // Clear-time SPEEDUP: control time over character time, so >1 means faster.
            if (score.ClearMs > 0) ratios.Add((c.Name, (float)control.ClearMs / score.ClearMs));
        }

        var ranked = ratios.OrderByDescending(r => r.Ratio).ToList();
        _out.WriteLine("CLEAR-SPEED MULTIPLE over a no-character run of the same skills:");
        foreach (var (name, ratio) in ranked) _out.WriteLine($"  {name,-20} {ratio,6:0.00}x");

        var best = ranked.First();
        Assert.True(best.Ratio < 4.0f,
                    $"{best.Name} clears {best.Ratio:0.00}x faster than playing no character at all. " +
                    "A character worth that much is not a choice, it is a prerequisite.");
    }
}
