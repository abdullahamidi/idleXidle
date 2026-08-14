using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;
using ResonanceHunter.Core.Characters;
using ResonanceHunter.Core.Economy;
using ResonanceHunter.Core.Expeditions;
using Xunit;
using Xunit.Abstractions;

// Aliased, not imported. `Source` lives in Core.Automation, not Core.Abilities where a skill's Source
// property would suggest, and this file's namespace sits under ...Tests.Characters where the test
// assembly's own sub-namespaces shadow the short name. The alias is the one spelling that survives both.
using CoreSource = ResonanceHunter.Core.Automation.Source;

namespace ResonanceHunter.Core.Tests.Characters;

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
    /// time and the ranking silently becomes a list of who saturated. Aura puts 18 damage a second into
    /// a single armoured target (see the throughput report below), so the 14,000-health boss takes it
    /// about thirteen minutes — real, measurable, and utterly invisible under a two-minute cap.
    /// </para>
    /// <para>
    /// Shrinking the fixture instead was the first attempt and it failed the other way: with a
    /// 1,200-health boss the whole gauntlet fits in a few hundred 100ms ticks, and THE SEEKER's +8%
    /// stopped registering at all because it no longer moved any creature's death across a tick
    /// boundary. The fixture has to be heavy enough to resolve the smallest passive and unbounded
    /// enough not to clip the slowest Form; lifting the ceiling is what buys both at once.
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
    /// how THE THORNWALL, carrying a ×1.35 aptitude on the Form it was running, came out below a
    /// no-character control.
    /// </para>
    /// <para>
    /// What actually separates them is how FAST the same health pool comes down. <see cref="Stalls"/>
    /// guards the other end: a wave that hits the tick ceiling pins the clock at 120s and saturates the
    /// metric, so a stalled run is reported rather than averaged into a ranking.
    /// </para>
    /// </remarks>
    private sealed record Score(string Id, string Name, int ClearMs, int Stalls, int HealthLost,
                                float Haul, float Rarity, bool DeathSave);

    /// <summary>
    /// Fold a character into a build exactly the way <c>PlayerLoadout.ToBuild</c> does.
    /// </summary>
    /// <remarks>
    /// Three channels and no fourth: Mods, TotalShape (passive shape combined with aptitude) and Grants.
    /// This mirrors the one seam in the game where a character reaches the simulation. If that seam ever
    /// grows a fourth channel, this harness stops measuring the whole character and the mirror has to be
    /// updated with it — which is why it is spelled out here rather than hidden behind a helper.
    /// </remarks>
    private static Build BuildFor(Character? c, Form form, Vow? vow)
    {
        var build = new Build
        {
            PassiveMods = c?.Mods ?? BuildMods.None,
            Shape = c?.TotalShape ?? SkillShape.None,
            ExtraTriggers = new HashSet<BuildTrigger>(c?.Grants ?? Array.Empty<BuildTrigger>()),
        };

        // Four skills, because the game gives four and several passives are per-cast.
        //
        // An AMPLIFIER Form is not played four times over. Mark "deals no damage at all" by the sim's own
        // comment — it opens a window in which other skills hit harder — so a four-Mark build swings
        // nothing but auto-attacks. The first run of this harness did exactly that and reported THE
        // OATHBOUND at 1,570 damage against a roster averaging 20,000, which reads as a broken character
        // and is really a broken fixture: it measured an amplifier with nothing to amplify. The rule is
        // taken from the sim's own predicate rather than special-cased by character id, so a second
        // amplifier Form would be handled without touching this file.
        var amplifier = FormBehaviour.IsAmplifier(form);
        for (var i = 0; i < 4; i++)
        {
            var f = amplifier && i > 0 ? Form.Strike : form;
            build.Weave(new EquippedSkill(
                new WovenAbility
                {
                    Name = $"S{i}", Source = CoreSource.Nature, Form = f, Vow = vow,
                },
                FormBehaviour.BaseCooldownMs(f)));
        }

        return build;
    }

    /// <summary>
    /// How many seeds each measurement averages over.
    /// </summary>
    /// <remarks>
    /// Not decoration. A single seed cannot rank these characters: a passive that kills marginally
    /// faster draws a different number of RNG samples from that point on, so two runs of the SAME build
    /// diverge, and the divergence is the same size as the effects being measured. The first version of
    /// this harness ran one seed and reported THE THORNWALL — which carries a ×1.35 aptitude on the very
    /// Form it was running — at 5% BELOW a no-character control, which is not a balance finding, it is
    /// noise wearing one. <see cref="test_the_measurement_noise_floor_is_below_the_effects_measured"/>
    /// keeps this number honest by measuring the floor rather than assuming it.
    /// </remarks>
    private const int Seeds = 40;

    /// <summary>Average the gauntlet over <see cref="Seeds"/> seeds.</summary>
    private static Score Measure(Character? c, Form form, Vow? vow)
    {
        double clear = 0, lost = 0;
        var stalls = 0;
        for (var s = 0; s < Seeds; s++)
        {
            var one = Run(c, form, vow, seed: 20260814 + s * 7919);
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
    private static Score Run(Character? c, Form form, Vow? vow, int seed)
    {
        var champ = new Champion { MaxHealth = ChampionHealth, Health = ChampionHealth };
        var build = BuildFor(c, form, vow);
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

    /// <summary>The Form a character is built to play — what a player actually equips.</summary>
    private static Form PlayedForm(Character c) => c.Aptitude ?? Form.Strike;

    [Fact]
    public void test_single_target_throughput_per_form_is_reported()
    {
        // NOT an assertion about balance — a measurement, printed, so the boss-wall question has a
        // number attached to it the next time anyone asks. It exists because the fixture above had to
        // be lightened twice, and the reason turned out to be a property of the Forms rather than of
        // the numbers I picked: against ONE creature, Aura and Projectile deliver a small fraction of
        // what Strike and Trap do. That is a legible trade for a spread Form — until it crosses the
        // line from "slower on bosses" to "cannot finish a boss", which is a dead end for a player who
        // built into it and is invisible from inside the game.
        //
        // Deliberately assertion-free: the roster balance pass this file serves has not been playtested,
        // and this project's own notes are explicit that the enemy curve must not be retuned blind.
        // A number in the log is what a later balance pass needs; a threshold guessed today is what
        // makes a gate start lying.
        var vow = Weaving.Catalog.First(v => v.Id == "vow_pure");
        const int windowMs = 60_000;

        _out.WriteLine("SINGLE-TARGET throughput — damage into one unkillable creature over 60s");
        _out.WriteLine("(the boss axis; contrast with the swarm column of the clearability table)");
        _out.WriteLine("");

        var perForm = new List<(Form Form, float Dps)>();
        foreach (var form in Enum.GetValues<Form>())
        {
            var champ = new Champion { MaxHealth = ChampionHealth, Health = ChampionHealth };
            var metrics = new WaveMetrics();
            // One creature with a health pool nothing can exhaust, so the run lasts the whole window
            // and the number is throughput rather than time-to-kill.
            var target = new List<WaveCreature>
            {
                new() { MaxHealth = 1e9f, Health = 1e9f, Damage = 0f, Defense = 40f },
            };
            SoloBattle.ResolveWave(champ, BuildFor(null, form, vow), new Hunter(), target,
                                   enemyIntervalMs: 900,
                                   ExpeditionTuning.Default with { TickCeilingMs = windowMs },
                                   new Random(20260814), metrics: metrics);
            perForm.Add((form, metrics.DeliveredDamage / (windowMs / 1000f)));
        }

        foreach (var (form, dps) in perForm.OrderByDescending(p => p.Dps))
            _out.WriteLine($"  {form,-16} {dps,10:N0} damage/sec into a single armoured target");

        var best = perForm.Max(p => p.Dps);
        var worst = perForm.Min(p => p.Dps);
        _out.WriteLine("");
        _out.WriteLine($"  spread: {best / MathF.Max(1f, worst):0.0}x between the best and worst Form " +
                       "on the single-target axis");
    }

    [Fact]
    public void test_the_gauntlet_is_clearable_by_every_form_it_measures()
    {
        // A STALL SATURATES THE CLOCK. A wave that runs out the 120-second tick ceiling reports that
        // ceiling as its duration, so two builds that both fail report identical times however far apart
        // they really are — and a build that fails by a hair reports slower than one that fails badly
        // but on a shorter earlier wave. Every Form the parity tests touch has to actually finish the
        // fixture, or the ranking silently becomes a ranking of who saturated first.
        var vow = Weaving.Catalog.First(v => v.Id == "vow_pure");

        var waves = new (string Name, Func<List<WaveCreature>> Make)[]
        {
            ("swarm", Swarm), ("pack", Pack), ("boss", Boss),
        };

        var failures = new List<string>();
        _out.WriteLine($"{"FORM",-16} {"swarm",12} {"pack",12} {"boss",12}");
        foreach (var form in Enum.GetValues<Form>())
        {
            var cells = new List<string>();
            foreach (var (name, make) in waves)
            {
                var champ = new Champion { MaxHealth = ChampionHealth, Health = ChampionHealth };
                var metrics = new WaveMetrics();
                var (outcome, _) = SoloBattle.ResolveWave(
                    champ, BuildFor(null, form, vow), new Hunter(), make(),
                    enemyIntervalMs: 900, Tuning, new Random(20260814), metrics: metrics);
                cells.Add(outcome == WaveOutcome.Cleared ? $"{metrics.DurationMs,10:N0}ms" : "     STALL");
                if (outcome != WaveOutcome.Cleared) failures.Add($"{form}/{name}");
            }

            _out.WriteLine($"{form,-16} {string.Join(" ", cells)}");
        }

        Assert.True(failures.Count == 0,
                    "the fixture is too heavy for the weakest Form it measures — " +
                    string.Join(", ", failures) + " ran out the 120s tick ceiling. A stalled wave " +
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
        var vow = Weaving.Catalog.First(v => v.Id == "vow_pure");

        foreach (var form in new[] { Form.Strike, Form.Trap, Form.Projectile })
        {
            var samples = Enumerable.Range(0, Seeds)
                                    .Select(s => (double)Run(null, form, vow, 20260814 + s * 7919).ClearMs)
                                    .ToList();
            var mean = samples.Average();
            var spread = mean <= 0 ? 0 : (samples.Max() - samples.Min()) / mean;

            _out.WriteLine($"{form,-12} mean clear {mean,9:N0} ms   spread across {Seeds} seeds {spread,6:0.0%}");

            Assert.True(spread < 0.02,
                        $"{form}: clear time varies {spread:0.0%} across seeds. The parity deltas are " +
                        "smaller than that, so they can no longer be read as effects. Average more seeds.");
        }
    }

    [Fact]
    public void test_no_character_is_dead_weight_on_every_axis()
    {
        var vow = Weaving.Catalog.First(v => v.Id == "vow_pure");
        var baseline = Measure(null, Form.Strike, vow);

        _out.WriteLine("ROSTER PARITY — each character on its own aptitude, against a no-character");
        _out.WriteLine("control running the SAME Form. Forms differ in raw throughput, so a shared");
        _out.WriteLine("baseline would rank the Form and call it the character.");
        _out.WriteLine("");
        _out.WriteLine($"{"CHARACTER",-20} {"FORM",-14} {"CLEAR ms",9} {"(ctl)",9} {"FASTER",8} " +
                       $"{"HP LOST",9} {"(ctl)",9} {"HAUL",6} {"RARITY",7}  CLAIMED AXIS");
        _out.WriteLine(new string('-', 128));
        _out.WriteLine($"{baseline.Name,-20} {"Strike",-14} {baseline.ClearMs,9:N0} {"—",9} {"—",8} " +
                       $"{baseline.HealthLost,9:N0} {"—",9} {1f,6:0.00} {1f,7:0.00}  (Strike reference)");

        var scores = new List<(Character C, Score S, Score Ctl)>();
        foreach (var c in CharacterRoster.All)
        {
            var form = PlayedForm(c);
            var control = Measure(null, form, vow);
            var score = Measure(c, form, vow);
            scores.Add((c, score, control));

            // Positive = clears faster than no character at all.
            var faster = score.ClearMs <= 0 ? 0f : 100f * ((float)control.ClearMs / score.ClearMs - 1f);
            var axis = ClaimedAxis(c);
            _out.WriteLine($"{c.Name,-20} {form,-14} {score.ClearMs,9:N0} {control.ClearMs,9:N0} " +
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
        // demand equality — an aptitude is SUPPOSED to beat no-character — it demands that the best
        // offensive character is not so far ahead that the other nine are a mistake to pick.
        var vow = Weaving.Catalog.First(v => v.Id == "vow_pure");
        var ratios = new List<(string Name, float Ratio)>();

        foreach (var c in CharacterRoster.All)
        {
            var form = PlayedForm(c);
            var control = Measure(null, form, vow);
            var score = Measure(c, form, vow);
            // Clear-time SPEEDUP: control time over character time, so >1 means faster.
            if (score.ClearMs > 0) ratios.Add((c.Name, (float)control.ClearMs / score.ClearMs));
        }

        var ranked = ratios.OrderByDescending(r => r.Ratio).ToList();
        _out.WriteLine("CLEAR-SPEED MULTIPLE over a no-character run of the same Form:");
        foreach (var (name, ratio) in ranked) _out.WriteLine($"  {name,-20} {ratio,6:0.00}x");

        var best = ranked.First();
        Assert.True(best.Ratio < 4.0f,
                    $"{best.Name} clears {best.Ratio:0.00}x faster than playing no character at all. " +
                    "A character worth that much is not a choice, it is a prerequisite.");
    }
}
