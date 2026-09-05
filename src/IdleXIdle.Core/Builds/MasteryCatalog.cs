using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Economy;

namespace IdleXIdle.Core.Builds;

/// <summary>The four demands the content makes, and therefore the four branches that answer them.</summary>
/// <remarks>
/// Named for the ANSWER, not the enemy. A branch called "Anti-Armour" would be a lookup table; a branch
/// called RESONANCE is a way of fighting that happens to beat armour, and can be wrong somewhere else.
/// </remarks>
public enum Branch
{
    /// <summary>How strong every skill is BEFORE you choose one. Opposed to <see cref="Loot"/>.</summary>
    Resonance,

    /// <summary>What you carry out of a run. Opposed to <see cref="Resonance"/>.</summary>
    Loot,

    /// <summary>Front-loaded damage. Answers Caster. Opposed to <see cref="Endure"/>.</summary>
    Tempo,

    /// <summary>Health, leech, mitigation. Answers Bruiser. Opposed to <see cref="Tempo"/>.</summary>
    Endure,
}

/// <summary>Where a node stands in its branch's drawing — the layout's only input besides the branch.</summary>
public enum MasteryRoute
{
    /// <summary>On the branch's axis before the fork. START is the trunk's root.</summary>
    Trunk,

    /// <summary>The route that runs anticlockwise of the axis.</summary>
    Left,

    /// <summary>The route that runs clockwise of the axis.</summary>
    Right,

    /// <summary>On the axis past both routes.</summary>
    Capstone,

    /// <summary>Between two branches — placed by <see cref="MasteryNode.Link"/>.</summary>
    Bridge,
}

/// <summary>
/// The catalogue: the fixed shape of the mastery tree, identical for every player.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PATH REDESIGN (2026-09-06).</b> The previous tree was six rings of "any of these"
/// prerequisites: every notable took any spine minor, every greater any spine notable, so a branch
/// was a fan rather than a road, its minors were stat soup ("+6 RESONANCE, +5 HEALTH"), the same
/// bundle repeated under three names, and the drawn wires moved when a node was bought. A branch is
/// now a TRUNK (two minors on the axis), a FORK, and two named ROUTES that each tell one build story
/// and rejoin at the branch's CAPSTONE. Every node has ONE explicit parent; a capstone accepts either
/// route's greater; a bridge needs one named notable on each side. See
/// design/gdd/mastery-tree-redesign-2026-09-06.md for the table this file is the code of.
/// </para>
/// <para>
/// <b>Prices.</b> MINOR 1, NOTABLE 3, SKILL 4, GREATER 5, SPECIALISATION 6, BRIDGE 6, CAPSTONE 8.
/// A route to its capstone is about thirty points; the whole tree is about 260 against roughly 66
/// points at full current content, so normal progression buys a quarter of it and never two capstones.
/// </para>
/// <para>
/// <b>No node is a bare multiplier.</b> Every entry changes a shape — hit size, target count, cooldown,
/// threshold, trigger — or is an explicit trade, or moves ONE stat the branch is named for.
/// </para>
/// </remarks>
public static class MasteryCatalog
{
    public const string StartId = "start";

    /// <summary>What each kind costs. Index by <see cref="MasteryKind"/> through <see cref="CostOf"/>.</summary>
    public static int CostOf(MasteryKind kind) => kind switch
    {
        MasteryKind.Start => 0,
        MasteryKind.Minor => 1,
        MasteryKind.Notable => 3,
        MasteryKind.SkillRoad => SkillRoadCost,
        MasteryKind.Greater => 5,
        MasteryKind.Specialisation => SpecialisationCost,
        MasteryKind.Bridge => BridgeCost,
        MasteryKind.Mastery => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Cost by ring, kept for the readers that price by ring: 0 START, 1 MINOR, 2 NOTABLE, 3 GREATER, 4 CAPSTONE.</summary>
    public static readonly IReadOnlyList<int> RingCost = new[] { 0, 1, 3, 5, 8 };

    public const int BridgeCost = 6;
    public const int SpecialisationCost = 6;
    public const int SkillRoadCost = 4;

    /// <summary>
    /// The name of a route — the build story it tells — for the fork label on the tree.
    /// </summary>
    public static string RouteName(Branch b, MasteryRoute route) => (b, route) switch
    {
        (Branch.Resonance, MasteryRoute.Left) => "THE ONE SOURCE",
        (Branch.Resonance, MasteryRoute.Right) => "THE SWORN",
        (Branch.Tempo, MasteryRoute.Left) => "THE OPENING",
        (Branch.Tempo, MasteryRoute.Right) => "THE RHYTHM",
        (Branch.Endure, MasteryRoute.Left) => "THICK SKIN",
        (Branch.Endure, MasteryRoute.Right) => "REPRISAL",
        (Branch.Loot, MasteryRoute.Left) => "THE CLEAN RUN",
        (Branch.Loot, MasteryRoute.Right) => "THE DEEP VEIN",
        _ => "",
    };

    public static IReadOnlyList<MasteryNode> Nodes { get; } = Build();

    public static MasteryNode? ById(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>Every point in the tree, if a player could somehow take all of it.</summary>
    public static int TotalCost => Nodes.Sum(n => n.Cost);

    /// <summary>A branch's own cost — trunk, both routes, leaves and capstone; bridges belong to no branch.</summary>
    public static int BranchCost(Branch branch)
        => Nodes.Where(n => n.Branch == branch && n.Kind != MasteryKind.Bridge).Sum(n => n.Cost);

    private static SkillShape S => SkillShape.None;

    private static IReadOnlyList<MasteryNode> Build()
    {
        var n = new List<MasteryNode>
        {
            new(StartId, MasteryKind.Start, Branch.Resonance, 0, 0, "YOU", S, null, Array.Empty<string>())
            {
                Route = MasteryRoute.Trunk, Step = 0,
            },
        };

        Resonance(n);
        Tempo(n);
        Endure(n);
        Loot(n);
        Bridges(n);
        return n;
    }

    // ── RESONANCE — how strong every skill is. Up. ───────────────────────────────────────────────

    private static void Resonance(List<MasteryNode> n)
    {
        const Branch b = Branch.Resonance;
        Trunk(n, b, "chime", "CHIME — +8 RESONANCE", StartId, 0, Stat(HunterStat.ResonanceAffinity, 8f));
        Trunk(n, b, "tone", "TONE — +8 RESONANCE", "chime", 1, Stat(HunterStat.ResonanceAffinity, 8f));

        // THE ONE SOURCE — commit every skill to one Source and be paid for the purity.
        const MasteryRoute a = MasteryRoute.Left;
        Minor(n, b, a, 0, "steep", "STEEP — +10 RESONANCE", "tone", Stat(HunterStat.ResonanceAffinity, 10f));
        Teaches(n, b, a, 1, "road_hammer", "steep", "hammer_blow");
        Notable(n, b, a, 2, "keyed", "KEYED — YOUR STRONG SOURCE MATCHUP PAYS 25% MORE",
                S with { StrongMatchupBonus = 0.25f }, "road_hammer");
        Notable(n, b, a, 3, "deep", "DEEP — +12 RESONANCE, AND RESONANCE IS WORTH 20% MORE",
                S with { ResonanceWorth = 0.20f }, "keyed", Stat(HunterStat.ResonanceAffinity, 12f));
        Teaches(n, b, a, 4, "road_hammer_2", "deep", "hammer_press");
        Greater(n, b, a, 5, "pure", "PURE — WHILE EVERY EQUIPPED SKILL SHARES ONE SOURCE, ALL SKILLS HIT 35% HARDER",
                S with { OneSourceBonus = 0.35f }, "road_hammer_2");
        Spec(n, b, a, 4, Style.Hammer, "spec_strike", "STRIKE SPECIALIST — EXECUTE WEAKENED FOES",
             BuildTrigger.Execute, S with { CullThreshold = 0.35f, CullBonus = 0.40f }, "deep");

        // THE SWORN — a promise kept pays, and pays more the narrower you make yourself.
        const MasteryRoute c = MasteryRoute.Right;
        Minor(n, b, c, 0, "clarity_res", "CLARITY — +10 RESONANCE", "tone", Stat(HunterStat.ResonanceAffinity, 10f));
        Teaches(n, b, c, 1, "road_snare", "clarity_res", "snare_jaws");
        Notable(n, b, c, 2, "pledge", "PLEDGE — VOWS PAY 30% MORE",
                S with { VowPowerMultiplier = 1.30f }, "road_snare");
        Notable(n, b, c, 3, "narrow", "NARROW — YOUR OWN STYLE +20%, EVERY OTHER STYLE -10%",
                S with { AffinityStyleBonus = 0.20f, OffStylePenalty = 0.10f }, "pledge");
        Teaches(n, b, c, 4, "road_snare_2", "narrow", "snare_repay");
        Greater(n, b, c, 5, "zealot", "ZEALOT — VOWS PAY 60% MORE, AND YOU TAKE 15% MORE DAMAGE",
                S with { VowPowerMultiplier = 1.60f, DamageTaken = 1.15f }, "road_snare_2");
        Spec(n, b, c, 4, Style.Snare, "spec_trap", "TRAP SPECIALIST — TRAPS RE-ARM ON BEING HIT",
             BuildTrigger.Coiled, S, "narrow");

        Capstone(n, b, "chord", "CHORD — EVERY SOURCE MATCHUP COUNTS AS STRONG, AND EVERY SKILL IS 15% SOFTER",
                 S with { AllMatchupsStrong = true, DamageDealt = 0.85f }, "pure", "zealot");
    }

    // ── TEMPO — hitting first and often. Right. ──────────────────────────────────────────────────

    private static void Tempo(List<MasteryNode> n)
    {
        const Branch b = Branch.Tempo;
        Trunk(n, b, "bite", "BITE — +4 ATTACK POWER", StartId, 0, Stat(HunterStat.AttackPower, 4f));
        Trunk(n, b, "swift", "SWIFT — +3 ENGINEERING", "bite", 1, Stat(HunterStat.Engineering, 3f));

        // THE OPENING — the wave's first seconds are where the damage is.
        const MasteryRoute a = MasteryRoute.Left;
        Minor(n, b, a, 0, "sharp", "SHARP — +1% CRITICAL CHANCE", "swift", Stat(HunterStat.CriticalChance, 1f));
        Teaches(n, b, a, 1, "road_sign", "sharp", "sign_call");
        Notable(n, b, a, 2, "surge", "SURGE — +40% FOR THE FIRST THREE SECONDS OF EACH WAVE",
                S with { OpeningSeconds = 3f, OpeningBonus = 0.40f }, "road_sign");
        Notable(n, b, a, 3, "alpha", "ALPHA — FIRST HIT x1.9, EVERY LATER HIT x0.8",
                S with { FirstHitMultiplier = 1.9f, LaterHitMultiplier = 0.80f }, "surge");
        Greater(n, b, a, 4, "opening_volley", "OPENING VOLLEY — EACH SKILL'S FIRST CAST OF A WAVE HITS +50%, EVERY LATER CAST -10%",
                S with { FirstCastMultiplier = 1.5f, LaterCastMultiplier = 0.9f }, "alpha");
        Spec(n, b, a, 4, Style.Sign, "spec_mark", "SIGN SPECIALIST — MARKS LAST LONGER",
             BuildTrigger.Linger, S with { AmplifyWindowMultiplier = 1.3f }, "alpha");

        // THE RHYTHM — cadence: the swing sooner, each cast hastening the next.
        const MasteryRoute c = MasteryRoute.Right;
        Minor(n, b, c, 0, "quick", "QUICK — +3 ENGINEERING", "swift", Stat(HunterStat.Engineering, 3f));
        Teaches(n, b, c, 1, "road_sign_2", "quick", "sign_brand");
        Notable(n, b, c, 2, "brisk", "BRISK — YOUR BASIC SWING COMES 20% SOONER",
                S with { AutoAttackRate = 1.20f }, "road_sign_2");
        Notable(n, b, c, 3, "rhythm", "RHYTHM — EACH CAST MAKES THE NEXT COME 8% SOONER, UP TO FIVE TIMES; A BITE RESETS IT",
                S with { CastRampPerCast = 0.08f, CastRampMax = 5 }, "brisk");
        Greater(n, b, c, 4, "blitz", "BLITZ — COOLDOWNS -30%, HITS -20%",
                S with { SkillRate = 1.43f, HitSize = 0.80f }, "rhythm");

        Capstone(n, b, "first_strike", "FIRST STRIKE — +150% FOR THREE SECONDS, -40% AFTER",
                 S with { OpeningSeconds = 3f, OpeningBonus = 1.50f, AfterOpeningPenalty = 0.40f },
                 "opening_volley", "blitz");
    }

    // ── ENDURE — outlasting the enemy. Left. ─────────────────────────────────────────────────────

    private static void Endure(List<MasteryNode> n)
    {
        const Branch b = Branch.Endure;
        Trunk(n, b, "hide", "HIDE — +12 MAXIMUM HEALTH", StartId, 0, Stat(HunterStat.MaxHealth, 12f));
        Trunk(n, b, "guard", "GUARD — +3 DEFENCE", "hide", 1, Stat(HunterStat.Defense, 3f));

        // THICK SKIN — take less, and mend between waves.
        const MasteryRoute a = MasteryRoute.Left;
        Minor(n, b, a, 0, "bulk", "BULK — +12 MAXIMUM HEALTH", "guard", Stat(HunterStat.MaxHealth, 12f));
        Teaches(n, b, a, 1, "road_drain", "bulk", "drain_drink");
        Notable(n, b, a, 2, "padding", "PADDING — EVERY BITE DEALS 5 LESS",
                S with { FlatDamageReduction = 5f }, "road_drain");
        Notable(n, b, a, 3, "recovery", "RECOVERY — REGAIN 15% OF HEALTH BETWEEN WAVES",
                S with { BetweenWaveRegen = 0.15f }, "padding");
        Greater(n, b, a, 4, "bulwark", "BULWARK — DAMAGE TAKEN -30%, DAMAGE DEALT -15%",
                S with { DamageTaken = 0.70f, DamageDealt = 0.85f }, "recovery");
        Spec(n, b, a, 4, Style.Drain, "spec_transformation",
             $"MORPH SPECIALIST — LEECH DOUBLED, HEAL LIMIT {HealTuning.Default.CeilingText(siphon: true)} A WAVE",
             BuildTrigger.Siphon, S, "recovery");

        // REPRISAL — every bite you take is turned back.
        const MasteryRoute c = MasteryRoute.Right;
        Minor(n, b, c, 0, "stand", "STAND — +3 DEFENCE", "guard", Stat(HunterStat.Defense, 3f));
        Teaches(n, b, c, 1, "road_drain_2", "stand", "drain_wilt");
        Notable(n, b, c, 2, "thorns", "THORNS — EVERY CREATURE THAT BITES YOU TAKES 15% OF ITS BITE BACK",
                S with { ReflectFraction = 0.15f }, "road_drain_2");
        Notable(n, b, c, 3, "payback", "PAYBACK — EVERY BITE MAKES YOUR NEXT SKILL HIT 15% HARDER, UP TO FOUR TIMES",
                S with { BiteFuelBonus = 0.15f, BiteFuelMax = 4 }, "thorns");
        Greater(n, b, c, 4, "rebound", "REBOUND — A FIFTH OF EVERY BITE COMES BACK AS HEALTH, DAMAGE DEALT -15%",
                S with { HealOnBiteFraction = 0.20f, DamageDealt = 0.85f }, "payback");

        Capstone(n, b, "endless", "ENDLESS — FULL HEALTH EVERY WAVE; MAXIMUM HEALTH HALVED",
                 S with { FullHealBetweenWaves = true, MaxHealth = 0.5f }, "bulwark", "rebound");
    }

    // ── LOOT — what you carry out. Down. ─────────────────────────────────────────────────────────

    private static void Loot(List<MasteryNode> n)
    {
        const Branch b = Branch.Loot;
        Trunk(n, b, "glean", "GLEAN — +6 GUILE", StartId, 0, Stat(HunterStat.Guile, 6f));
        Trunk(n, b, "pockets", "POCKETS — +8 GUILE", "glean", 1, Stat(HunterStat.Guile, 8f));

        // THE CLEAN RUN — a wave nothing bit you in pays more.
        const MasteryRoute a = MasteryRoute.Left;
        Minor(n, b, a, 0, "scavenge", "SCAVENGE — +6 GUILE", "pockets", Stat(HunterStat.Guile, 6f));
        Teaches(n, b, a, 1, "road_volley", "scavenge", "volley_spray");
        Notable(n, b, a, 2, "untouched", "UNTOUCHED — +3% HAUL FOR EACH SECOND OF THE WAVE NOTHING BIT YOU, UP TO +45%",
                S with { HaulPerCleanSecond = 0.03f, HaulCleanCap = 0.45f }, "road_volley");
        Notable(n, b, a, 3, "spotless", "SPOTLESS — +25% HAUL ON A WAVE YOU FINISH ABOVE 90% HEALTH",
                S with { HaulUntouchedWave = 0.25f }, "untouched");
        Teaches(n, b, a, 4, "road_volley_2", "spotless", "volley_weep");
        Greater(n, b, a, 5, "second_look", "SECOND LOOK — A QUIET WAVE RAISES CHEST QUALITY TOO, +0.04 A SECOND; HITS -8%",
                S with { RarityFromClean = 0.04f, HitSize = 0.92f }, "road_volley_2");
        Spec(n, b, a, 4, Style.Volley, "spec_projectile", "VOLLEY SPECIALIST — ONE MORE SHOT, ONE MORE TARGET",
             BuildTrigger.Overdraw, S with { StyleTargets = new Dictionary<Style, int> { [Style.Volley] = 1 } }, "spotless");

        // THE DEEP VEIN — pay in blood and depth, carry more out.
        const MasteryRoute c = MasteryRoute.Right;
        Minor(n, b, c, 0, "weigh", "WEIGH — +8 GUILE", "pockets", Stat(HunterStat.Guile, 8f));
        Teaches(n, b, c, 1, "road_field", "weigh", "field_mire");
        Notable(n, b, c, 2, "bloodprice", "BLOODPRICE — +35% HAUL ON A WAVE YOU END BELOW HALF HEALTH",
                S with { HaulWhenHurt = 0.35f }, "road_field");
        Notable(n, b, c, 3, "prospect", "PROSPECT — +1% HAUL FOR EVERY WAVE PAST DEPTH 20",
                S with { HaulPerWavePastDepth = 0.01f, HaulDepthFloor = 20 }, "bloodprice");
        Teaches(n, b, c, 4, "road_field_2", "prospect", "field_pulse");
        Greater(n, b, c, 5, "gamble", "GAMBLE — +70% HAUL, AND EVERY BITE HITS YOU 25% HARDER",
                S with { HaulWhenHurt = 0.70f, DamageTaken = 1.25f }, "road_field_2");
        Spec(n, b, c, 4, Style.Field, "spec_aura", "AURA SPECIALIST — TICKS 30% FASTER",
             BuildTrigger.Radiance, S, "prospect");

        Capstone(n, b, "prospector", "PROSPECTOR — +3% HAUL FOR EVERY WAVE PAST DEPTH 30, AND SKILLS -15%",
                 S with { HaulPerWavePastDepth = 0.03f, HaulDepthFloor = 30, DamageDealt = 0.85f },
                 "second_look", "gamble");
    }

    // ── BRIDGES — between neighbours, one named notable on each side. ────────────────────────────

    private static void Bridges(List<MasteryNode> n)
    {
        // Each bridge hangs between the two ROUTES THAT FACE IT — the route of each branch that runs
        // toward the other — so its two wires never cross a trunk. The layout puts it at the midpoint
        // angle; the notables named here are the ones on that side of each axis.
        Bridge(n, Branch.Resonance, Branch.Tempo, "executioner",
            "EXECUTIONER — THE FIRST HIT ON EACH CREATURE +10% AND IT CUTS 9 ARMOUR",
            S with { FirstHitMultiplier = 1.10f, ArmourPenetration = 9f }, "pledge", "surge");
        Bridge(n, Branch.Tempo, Branch.Loot, "volley",
            "VOLLEY — +1 TARGET AND -20% COOLDOWN, HITS -25%",
            S with { ExtraTargets = 1, SkillRate = 1.25f, HitSize = 0.75f }, "brisk", "untouched");
        Bridge(n, Branch.Loot, Branch.Endure, "feedback",
            "FEEDBACK — HEAL 1.2% OF MAXIMUM PER CREATURE STRUCK",
            S with { HealPerTargetStruck = 0.012f }, "bloodprice", "padding");
        Bridge(n, Branch.Endure, Branch.Resonance, "anchor",
            "ANCHOR — +1% HIT SIZE PER 200 MAXIMUM HEALTH",
            S with { HitSizePerMaxHealth = 0.01f / 200f }, "thorns", "keyed");
    }

    // ── The authoring helpers. Every node has ONE parent; Route and Step are its place. ──────────

    private static void Trunk(List<MasteryNode> n, Branch b, string id, string label, string parent, int step,
                              IReadOnlyDictionary<HunterStat, float> stats)
        => n.Add(new(id, MasteryKind.Minor, b, 1, CostOf(MasteryKind.Minor), label, S, null, new[] { parent })
        {
            Route = MasteryRoute.Trunk, Step = step, Stats = stats,
        });

    private static void Minor(List<MasteryNode> n, Branch b, MasteryRoute route, int step, string id, string label,
                              string parent, IReadOnlyDictionary<HunterStat, float> stats)
        => n.Add(new(id, MasteryKind.Minor, b, 1, CostOf(MasteryKind.Minor), label, S, null, new[] { parent })
        {
            Route = route, Step = step, Stats = stats,
        });

    private static void Notable(List<MasteryNode> n, Branch b, MasteryRoute route, int step, string id, string label,
                                SkillShape shape, string parent, IReadOnlyDictionary<HunterStat, float>? stats = null)
        => n.Add(new(id, MasteryKind.Notable, b, 2, CostOf(MasteryKind.Notable), label, shape, null, new[] { parent })
        {
            Route = route, Step = step, Stats = stats,
        });

    private static void Greater(List<MasteryNode> n, Branch b, MasteryRoute route, int step, string id, string label,
                                SkillShape shape, string parent)
        => n.Add(new(id, MasteryKind.Greater, b, 3, CostOf(MasteryKind.Greater), label, shape, null, new[] { parent })
        {
            Route = route, Step = step,
        });

    private static void Teaches(List<MasteryNode> n, Branch b, MasteryRoute route, int step, string id, string parent,
                                string skillId)
    {
        var def = SkillCatalogue.ById(skillId);
        n.Add(new(id, MasteryKind.SkillRoad, b, 3, CostOf(MasteryKind.SkillRoad),
                  $"{def.Name} — {def.Line.ToUpperInvariant()}", S, null, new[] { parent })
        {
            Route = route, Step = step, GrantsSkillId = skillId, Style = def.Style,
        });
    }

    private static void Spec(List<MasteryNode> n, Branch b, MasteryRoute route, int step, Style style, string id,
                             string label, BuildTrigger grant, SkillShape shape, string parent)
        => n.Add(new(id, MasteryKind.Specialisation, b, 3, CostOf(MasteryKind.Specialisation), label, shape, grant,
                     new[] { parent })
        {
            Route = route, Step = step, Style = style,
        });

    /// <remarks>
    /// GEOMETRY FOLLOWS ACTUAL CONTENT DEPTH (2026-09-06): the capstone stands ONE step past the
    /// deeper of its two greaters. It stood at step 7 on every branch for a symmetric rim, which left
    /// TEMPO and ENDURE — whose routes end at step 4 — a wire three steps long into nothing. The rim
    /// is asymmetric now, and the branches with the longer roads look like it.
    /// </remarks>
    private static void Capstone(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                 string leftGreater, string rightGreater)
    {
        var deepest = Math.Max(n.First(x => x.Id == leftGreater).Step, n.First(x => x.Id == rightGreater).Step);
        n.Add(new(id, MasteryKind.Mastery, b, 4, CostOf(MasteryKind.Mastery), label, shape, null,
                  new[] { leftGreater, rightGreater })
        {
            Route = MasteryRoute.Capstone, Step = deepest + 1,
        });
    }

    private static void Bridge(List<MasteryNode> n, Branch a, Branch c, string id, string label, SkillShape shape,
                               string notableOfA, string notableOfC)
        => n.Add(new(id, MasteryKind.Bridge, a, 2, CostOf(MasteryKind.Bridge), label, shape, null, new[] { notableOfA })
        {
            Link = c, Route = MasteryRoute.Bridge, SecondPrereqs = new[] { notableOfC },
        });

    private static Dictionary<HunterStat, float> Stat(HunterStat a, float av)
        => new() { [a] = av };
}
