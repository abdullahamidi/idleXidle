using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;

namespace ResonanceHunter.Core.Builds;

/// <summary>The four demands the content makes, and therefore the four branches that answer them.</summary>
/// <remarks>
/// Named for the ANSWER, not the enemy. A branch called "Anti-Armour" would be a lookup table; a branch
/// called WEIGHT is a way of fighting that happens to beat armour, and can be wrong somewhere else.
/// </remarks>
public enum Branch
{
    /// <summary>Few, enormous hits. Answers Armoured. Opposed to <see cref="Spread"/>.</summary>
    Weight,

    /// <summary>Many targets. Answers Swarm. Opposed to <see cref="Weight"/>.</summary>
    Spread,

    /// <summary>Front-loaded damage. Answers Caster. Opposed to <see cref="Endure"/>.</summary>
    Tempo,

    /// <summary>Health, leech, mitigation. Answers Bruiser. Opposed to <see cref="Tempo"/>.</summary>
    Endure,
}

/// <summary>
/// The catalogue: the fixed shape of the skill tree, identical for every player.
/// </summary>
/// <remarks>
/// <para>
/// FOUR BRANCHES, ONE PER DEMAND. The previous tree had six arms, one per Form, which sounds like the same
/// idea and is not: Form is what a skill IS, and the content asks about what a build DOES. Six Form arms
/// meant six flavours of the same question and no way to answer "five creatures at once" except by
/// happening to have picked Aura. The branches map onto the archetype engine directly — Weight answers
/// Armoured, Spread answers Swarm, Tempo answers Caster, Endure answers Bruiser — so a player who reads
/// their post-run report knows which branch it is pointing at.
/// </para>
/// <para>
/// The pairs are OPPOSED. Weight and Spread trade hit size against target count on nearly every node;
/// Tempo and Endure trade front-loading against lasting. A build that walks one axis to its end is
/// excellent in one band and stalls in the band that asks the opposite question, and that stall is the
/// game.
/// </para>
/// <para>
/// <b>Rings and prices.</b> 6 minors at 1, 5 notables at 3, 4 greaters at 5, 1 mastery at 8 — a full
/// branch is 49. Four bridges at 6 and six Form specialisations at 6 bring the tree to 256 against
/// roughly 66 points at full current content (six regions at depth 150 on the <see cref="MasteryPoints"/>
/// curve), which keeps the design's about-a-quarter-to-a-third reachable invariant (26%). Two complete
/// branches cost 98 and are therefore exactly, deliberately out of reach.
/// </para>
/// <para>
/// <b>The side road.</b> Each branch was 4 → 3 → 2 → 1 and a playtester was at a specialisation inside
/// fifteen minutes. One minor, one notable and one greater on every branch are not spread
/// across the fans — they form ONE SIDE ROAD: a spur minor that hangs off the outermost ring-1 minor
/// (so it needs that minor first), a notable that needs the spur, a greater that needs that notable,
/// and the capstone accepts the greater like any other. It is a longer way to the same rim, and it
/// carries the branch's identity end to end — Weight's road dents, hammers and shatters plate; Spread's
/// sweeps wider and feeds cooldowns from kills; Tempo's front-loads the wave's opening seconds and pays
/// in bites taken; Endure's thickens skin, turns bites back and pays in cooldowns.
/// </para>
/// <para>
/// <b>The 2026-08-27 pass.</b> "Add new nodes and lower the numbers on the current ones a little."
/// Every ring gained one node — a sixth minor, a fifth notable, a fourth greater per branch — each a
/// new SHAPE the sim reads (HEADLONG, STAGGER, RALLY, TIDE, BRISK, RHYTHM, OPENING VOLLEY, MENDING,
/// PAYBACK, REBOUND; HEFT and FAN reuse the per-Form fields), and every existing bonus came down by
/// roughly a fifth to a third so a full walk lands about where it did. The branch sweep
/// (<c>BalanceSweepTests</c>) was re-measured against the new numbers.
/// </para>
/// <para>
/// <b>No node is a bare multiplier.</b> Every entry below changes a shape — hit size, target count,
/// cooldown, threshold, trigger — or is an explicit trade. Even the connective minors move a shape rather
/// than a number, which is why they carry <see cref="SkillShape"/> and not <see cref="BuildMods"/>.
/// </para>
/// </remarks>
public static class MasteryCatalog
{
    public const string StartId = "start";

    /// <summary>Cost of a node at each ring. Index 0 is the free START.</summary>
    public static readonly IReadOnlyList<int> RingCost = new[] { 0, 1, 3, 5, 8 };

    /// <summary>What one bridge or one Form specialisation costs.</summary>
    public const int BridgeCost = 6;
    public const int SpecialisationCost = 6;

    public static IReadOnlyList<MasteryNode> Nodes { get; } = Build();

    public static MasteryNode? ById(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>Every point in the tree, if a player could somehow take all of it.</summary>
    public static int TotalCost => Nodes.Sum(n => n.Cost);

    /// <summary>What one complete branch costs — the design's affordability unit.</summary>
    public static int BranchCost(Branch branch)
        => Nodes.Where(n => n.Branch == branch && n.Kind is not MasteryKind.Bridge and not MasteryKind.Specialisation)
                .Sum(n => n.Cost);

    private static SkillShape S => SkillShape.None;

    private static IReadOnlyList<MasteryNode> Build()
    {
        var n = new List<MasteryNode>
        {
            new(StartId, MasteryKind.Start, Branch.Weight, 0, 0, "YOU", S, null, Array.Empty<string>()),
        };

        Weight(n);
        Spread(n);
        Tempo(n);
        Endure(n);
        Bridges(n);
        Specialisations(n);
        return n;
    }

    // ── WEIGHT — answers Armoured. Fewer, larger hits. ────────────────────────────────────────────

    private static void Weight(List<MasteryNode> n)
    {
        const Branch b = Branch.Weight;

        // The 2026-08-27 pass added one node to every ring and took every existing number down by a
        // fifth to a third, so a full walk lands roughly where it did with three more decisions in it.
        // The new node of each ring is listed FIRST so it sits on the inner shoulder of its fan and the
        // side road (spur, side notable, side greater) keeps the outer edge end to end.
        Minor(n, b, "heft", "HEFT — STRIKE AND TRAP HIT 20% HARDER",
            S with { FormPower = new Dictionary<Form, float> { [Form.Strike] = 1.20f, [Form.Trap] = 1.20f } });
        Minor(n, b, "heavy_hand", "HEAVY HAND — BIGGER HITS, SLOWER",
            S with { HitSize = 1.18f, SkillRate = 0.88f });
        Minor(n, b, "sharpened", "SHARPENED — HITS CUT 6 ARMOUR",
            S with { ArmourPenetration = 6f });
        Minor(n, b, "follow_through", "FOLLOW THROUGH — FIRST HIT ON A CREATURE +20%",
            S with { FirstHitMultiplier = 1.20f });
        Minor(n, b, "deliberate", "DELIBERATE — MUCH BIGGER HITS, MUCH SLOWER",
            S with { HitSize = 1.22f, SkillRate = 0.85f });
        // THE SIDE ROAD'S SPUR. A small SUNDER that a minor can afford: with SUNDER itself the threshold
        // takes the kinder 150 and the strips ADD, so the spur keeps paying after the notable — a
        // 1-point node that goes dead the moment its 3-point neighbour arrives is dormant content.
        Spur(n, b, "dent", "deliberate", "DENT — A HIT OVER 150 STRIPS 6 ARMOUR FOR THE WAVE",
            S with { SunderThreshold = 150f, SunderAmount = 6f });

        // HEADLONG is CULL's mirror: Weight puts its few big hits into a FULL pool.
        Notable(n, b, "headlong", "HEADLONG — +30% AGAINST CREATURES ABOVE HALF HEALTH",
            S with { FreshThreshold = 0.5f, FreshBonus = 0.30f },
            SpineMinors(n, b));
        // SUNDER strips armour permanently for the rest of the wave, so a Weight build gets STRONGER as an
        // Armoured wave goes on — the opposite of how flat mitigation normally works, and the reason the
        // branch beats Plated bands rather than merely surviving them.
        Notable(n, b, "sunder", "SUNDER — A HIT OVER 200 STRIPS 15 ARMOUR FOR THE WAVE",
            S with { SunderThreshold = 200f, SunderAmount = 15f },
            SpineMinors(n, b));
        // "HITS 8x A CREATURE'S ARMOUR IGNORE HALF OF IT" — a sentence with no verb where the reader
        // needs one, so it parsed as "hits eight times" rather than "a hit eight times the size of".
        Notable(n, b, "crush", "CRUSH — A HIT 8x BIGGER THAN THE ARMOUR IGNORES 40% OF IT",
            S with { CrushArmourMultiple = 8f, ArmourIgnoreFraction = 0.4f },
            SpineMinors(n, b));
        Notable(n, b, "breaker", "BREAKER — 40% OF ANY OVERKILL CARRIES TO THE NEXT CREATURE",
            S with { OverkillCarry = 0.4f },
            SpineMinors(n, b));
        // The side road's notable: the mirror of Tempo's ALPHA. Weight keeps hitting the SAME creature,
        // and every hit after the first is the bigger one — a single-target commitment stated as a
        // shape, which is why it is worth nothing to a build that touches each creature once.
        Notable(n, b, "second_blow", "SECOND BLOW — EVERY HIT AFTER THE FIRST ON A CREATURE +15%",
            S with { LaterHitMultiplier = 1.15f },
            new[] { "dent" });

        // The new greater pays in Weight's own currency, like SHATTER: slower, for a bite held off.
        Greater(n, b, "stagger", "STAGGER — A HIT OVER 200 PUSHES THE NEXT ENEMY BITE BACK HALF A SECOND, COOLDOWNS +15%",
            S with { StaggerThreshold = 200f, StaggerMs = 500, SkillRate = 0.87f },
            SpineNotables(n, b));
        Greater(n, b, "monolith", "MONOLITH — HALF AS MANY HITS, EACH TWICE AS LARGE",
            S with { HitSize = 2f, SkillRate = 0.5f },
            SpineNotables(n, b));
        Greater(n, b, "siege", "SIEGE — +35% TO ARMOURED, -20% TO EVERYTHING ELSE",
            S with { VsArmouredBonus = 0.35f, VsOtherPenalty = 0.20f },
            SpineNotables(n, b));
        // The side road's greater. With DENT and SUNDER the threshold drops to 120 and the strips sum to
        // 43 — a plated wave is bare after two swings — and the price is Weight's own currency: slower.
        Greater(n, b, "shatter", "SHATTER — A HIT OVER 120 STRIPS 22 ARMOUR FOR THE WAVE, COOLDOWNS +18%",
            S with { SunderThreshold = 120f, SunderAmount = 22f, SkillRate = 0.85f },
            new[] { "second_blow" });

        // THE BRANCH IN ONE NODE. Armour stops existing — and so does any build that delivers its damage
        // in small pieces, which is every Spread build in the game. It is the clearest statement in the
        // tree that the two axes are opposed.
        Mastery(n, b, "overwhelm", "OVERWHELM — HITS UNDER 60 DO NOTHING; HITS OVER 60 IGNORE ARMOUR",
            S with { OverwhelmFloor = 60f },
            Greaters(n, b));
    }

    // ── SPREAD — answers Swarm. Target count and action economy. ──────────────────────────────────

    private static void Spread(List<MasteryNode> n)
    {
        const Branch b = Branch.Spread;

        // Weight's other Form pulled onto the Spread axis, the way SWEEP pulls Strike.
        Minor(n, b, "fan", "FAN — TRAP REACHES ONE MORE CREATURE",
            S with { FormTargets = new Dictionary<Form, int> { [Form.Trap] = 1 } });
        Minor(n, b, "wide", "WIDE — PROJECTILE REACHES ONE MORE",
            S with { FormTargets = new Dictionary<Form, int> { [Form.Projectile] = 1 } });
        // The card used to say "AURA TICKS 25% FASTER" over a shape that touched every skill's rate and
        // hit size and never the Aura's tick. It says what it does now.
        Minor(n, b, "diffuse", "DIFFUSE — COOLDOWNS -8%, HITS -4%",
            S with { SkillRate = 1.08f, HitSize = 0.96f });
        Minor(n, b, "quick_hands", "QUICK HANDS — COOLDOWNS -10%",
            S with { SkillRate = 1.11f });
        Minor(n, b, "ricochet", "RICOCHET — 25% OF HITS STRIKE A SECOND CREATURE FOR HALF",
            S with { RicochetChance = 0.25f, RicochetFraction = 0.5f });
        // The side road's spur pulls Weight's own Form onto the Spread axis: a Strike that reaches two.
        Spur(n, b, "sweep", "ricochet", "SWEEP — STRIKE REACHES ONE MORE CREATURE",
            S with { FormTargets = new Dictionary<Form, int> { [Form.Strike] = 1 } });

        // RALLY is fed by kills, like MOMENTUM: a torrent in a Swarm, one rally against a Bruiser.
        Notable(n, b, "rally", "RALLY — AFTER A KILL, YOUR NEXT SKILL HITS 40% HARDER",
            S with { NextSkillAfterKillBonus = 0.40f },
            SpineMinors(n, b));
        Notable(n, b, "chain", "CHAIN — EVERY HIT STRIKES ONE MORE CREATURE AT 40%",
            S with { ChainFraction = 0.4f },
            SpineMinors(n, b));
        Notable(n, b, "cull", "CULL — +60% TO CREATURES UNDER 25% HEALTH",
            S with { CullThreshold = 0.25f, CullBonus = 0.60f },
            SpineMinors(n, b));
        Notable(n, b, "swarmbane", "SWARMBANE — +6% DAMAGE PER LIVING CREATURE",
            S with { PerCreatureBonus = 0.06f },
            SpineMinors(n, b));
        // The side road's notable: action economy FED BY KILLS. Eight refunds in a Swarm, one against a
        // Bruiser — the same node is a torrent in the band Spread answers and a trickle in the one it
        // does not, which is what makes it a Spread node rather than a rate node.
        Notable(n, b, "momentum", "MOMENTUM — EVERY KILL TAKES THREE-QUARTERS OF A SECOND OFF ALL COOLDOWNS",
            S with { CooldownRefundOnKillMs = 750 },
            new[] { "sweep" });

        // TIDE is SWARMBANE's cousin in the other currency — rate per creature rather than damage —
        // and it pays the same Swarm/Bruiser trade: fast in a crowd, a flat price against one.
        Greater(n, b, "tide", "TIDE — COOLDOWNS 4% SHORTER PER LIVING CREATURE, HITS -15%",
            S with { RatePerCreature = 0.04f, HitSize = 0.85f },
            SpineNotables(n, b));
        Greater(n, b, "cascade", "CASCADE — AFTER A KILL, THE NEXT SKILL STRIKES EVERYTHING",
            S with { CascadeOnKill = true },
            SpineNotables(n, b));
        Greater(n, b, "dispersal", "DISPERSAL — ALL FORMS +1 TARGET, HITS -25%",
            S with { ExtraTargets = 1, HitSize = 0.75f },
            SpineNotables(n, b));
        // The side road's greater. Worse than nothing against one creature (x1.09 x 0.85), better past
        // two, far better at six — the Swarm/Bruiser trade stated in one line, and the price is paid
        // exactly where Endure would have been the answer.
        Greater(n, b, "outnumbered", "OUTNUMBERED — +9% DAMAGE PER LIVING CREATURE, ALL HITS -15%",
            S with { PerCreatureBonus = 0.09f, DamageDealt = 0.85f },
            new[] { "momentum" });

        // Swarms evaporate. Anything with armour becomes nearly immune, because 40% of a small hit lands
        // under the flat mitigation floor — the exact mirror of what OVERWHELM does to Spread builds.
        Mastery(n, b, "everywhere", "EVERYWHERE — EVERY SKILL STRIKES EVERY CREATURE AT 40%",
            S with { StrikesEveryCreature = true, HitSize = 0.40f },
            Greaters(n, b));
    }

    // ── TEMPO — answers Caster. Kill it before it acts. ───────────────────────────────────────────

    private static void Tempo(List<MasteryNode> n)
    {
        const Branch b = Branch.Tempo;

        // The basic swing is a third of a build's damage and had no node; this is its Tempo node.
        Minor(n, b, "brisk", "BRISK — YOUR BASIC SWING COMES 20% SOONER",
            S with { AutoAttackRate = 1.20f });
        Minor(n, b, "opener", "OPENER — +25% ON THE FIRST HIT AGAINST EACH CREATURE",
            S with { FirstHitMultiplier = 1.25f });
        Minor(n, b, "hasten", "HASTEN — +15% SKILL RATE",
            S with { SkillRate = 1.15f });
        Minor(n, b, "preparation", "PREPARATION — YOUR FIRST CAST OF A WAVE COMES WITH NO WAIT",
            S with { FreeOpeningCast = true });
        Minor(n, b, "focus", "FOCUS — +8% CRITICAL CHANCE",
            S with { BonusCritPercent = 8f });
        // The side road's spur: the mastery's shape at a minor's size, with no price. The window fields
        // take the kinder value and the bonuses ADD, so it keeps paying under SURGE and FIRST STRIKE.
        // Two seconds, not one: the wave opens with a 700ms breath, so a one-second window would have
        // held the auto-swing and nothing else.
        Spur(n, b, "flash", "focus", "FLASH — +20% FOR THE FIRST TWO SECONDS OF EACH WAVE",
            S with { OpeningSeconds = 2f, OpeningBonus = 0.20f });

        // RHYTHM is the branch's promise as a clock: faster while nothing has bitten you, which is the
        // Caster band, and back to nothing in the Bruiser band where every bite lands.
        Notable(n, b, "rhythm", "RHYTHM — EACH CAST MAKES THE NEXT COME 8% SOONER, UP TO FIVE TIMES; A BITE RESETS IT",
            S with { CastRampPerCast = 0.08f, CastRampMax = 5 },
            SpineMinors(n, b));
        Notable(n, b, "alpha", "ALPHA — FIRST HIT x1.9, EVERY LATER HIT x0.8",
            S with { FirstHitMultiplier = 1.9f, LaterHitMultiplier = 0.80f },
            SpineMinors(n, b));
        Notable(n, b, "mark_mastery", "MARK MASTERY — THE WINDOW LASTS 60% LONGER AND HITS 30% HARDER",
            S with { MarkWindowMultiplier = 1.6f, MarkPowerBonus = 0.30f },
            SpineMinors(n, b));
        Notable(n, b, "interrupt", "INTERRUPT — DAMAGE IN THE RUN-UP TO AN ENEMY SWING +45%",
            S with { InterruptBonus = 0.45f },
            SpineMinors(n, b));
        // The side road's notable. Three seconds on purpose — the same window as FIRST STRIKE, so the
        // notable deepens the capstone's opening rather than stretching it, and the mastery's "-40%
        // after three seconds" stays true with SURGE taken.
        Notable(n, b, "surge", "SURGE — +40% FOR THE FIRST THREE SECONDS OF EACH WAVE",
            S with { OpeningSeconds = 3f, OpeningBonus = 0.40f },
            new[] { "flash" });

        // Per SKILL, not per creature or per second — the third kind of opening this branch sells, and
        // the only one that pays on every later cast rather than on the clock.
        Greater(n, b, "opening_volley", "OPENING VOLLEY — EACH SKILL'S FIRST CAST OF A WAVE HITS +50%, EVERY LATER CAST -10%",
            S with { FirstCastMultiplier = 1.5f, LaterCastMultiplier = 0.9f },
            SpineNotables(n, b));
        Greater(n, b, "blitz", "BLITZ — COOLDOWNS -30%, HITS -20%",
            S with { SkillRate = 1.43f, HitSize = 0.80f },
            SpineNotables(n, b));
        Greater(n, b, "assassinate", "ASSASSINATE — ONCE A WAVE, A CREATURE UNDER 30% DIES OUTRIGHT",
            S with { AssassinateThreshold = 0.30f },
            SpineNotables(n, b));
        // The side road's greater pays in the OPPOSED branch's currency: speed bought with bites taken.
        // A Caster that dies in the opening never bites; a Bruiser that does not now bites harder.
        Greater(n, b, "rush", "RUSH — COOLDOWNS -20%, EVERY BITE DEALS +15%",
            S with { SkillRate = 1.25f, DamageTaken = 1.15f },
            new[] { "surge" });

        // Every wave becomes an opener. Bruisers, which cannot be killed in three seconds, become the wall
        // — which is what makes this the mastery that Endure exists to be the answer to.
        Mastery(n, b, "first_strike", "FIRST STRIKE — +150% FOR THREE SECONDS, -40% AFTER",
            S with { OpeningSeconds = 3f, OpeningBonus = 1.50f, AfterOpeningPenalty = 0.40f },
            Greaters(n, b));
    }

    // ── ENDURE — answers Bruiser, and extends every other band. ───────────────────────────────────

    private static void Endure(List<MasteryNode> n)
    {
        const Branch b = Branch.Endure;

        // In-fight regeneration, under the heal ceiling — the third kind of heal this branch sells,
        // beside LEECH (per hit) and SECOND WIND (per clear).
        Minor(n, b, "mending", "MENDING — REGAIN 0.3% OF MAXIMUM HEALTH EVERY SECOND",
            S with { RegenFraction = 0.003f });
        Minor(n, b, "toughness", "TOUGHNESS — +15% MAXIMUM HEALTH",
            S with { MaxHealth = 1.15f });
        Minor(n, b, "leech", "LEECH — HEAL 1.5% OF DAMAGE DEALT",
            S with { Leech = 0.015f });
        Minor(n, b, "padding", "PADDING — EVERY BITE DEALS 5 LESS",
            S with { FlatDamageReduction = 5f });
        Minor(n, b, "second_wind", "SECOND WIND — HEAL 6% ON CLEARING A WAVE",
            S with { HealOnClear = 0.06f });
        // The side road's spur: more of PADDING's flat cut, which ADDS — nine off every bite with both,
        // and the flat cut is the one Endure number that erases a Swarm's nibbles outright.
        Spur(n, b, "thick_skin", "second_wind", "THICK SKIN — EVERY BITE DEALS 4 LESS",
            S with { FlatDamageReduction = 4f });

        // PAYBACK is RHYTHM's mirror across the axis: Endure builds up FROM being bitten.
        Notable(n, b, "payback", "PAYBACK — EVERY BITE MAKES YOUR NEXT SKILL HIT 15% HARDER, UP TO FOUR TIMES",
            S with { BiteFuelBonus = 0.15f, BiteFuelMax = 4 },
            SpineMinors(n, b));
        // The one node in the game that buys an exception to "health does not regenerate between waves".
        Notable(n, b, "recovery", "RECOVERY — REGAIN 15% OF HEALTH BETWEEN WAVES",
            S with { BetweenWaveRegen = 0.15f },
            SpineMinors(n, b));
        Notable(n, b, "absorb", "ABSORB — MITIGATION RISES AS HEALTH FALLS, TO -30%",
            S with { AbsorbAtLowHealth = 0.30f },
            SpineMinors(n, b));
        Notable(n, b, "fortify", "FORTIFY — THE FIRST BITE OF EACH WAVE DEALS NOTHING",
            S with { FirstBiteFree = true },
            SpineMinors(n, b));
        // The side road's notable: the Endure answer that SCALES WITH THE THREAT. A Bruiser's identity is
        // the size of its bite, so the harder it bites the more it pays back; a Swarm of nibblers pays
        // almost nothing per head. Read against the raw bite, so the rest of the branch cannot shrink it.
        Notable(n, b, "thorns", "THORNS — EVERY CREATURE THAT BITES YOU TAKES 15% OF ITS BITE BACK",
            S with { ReflectFraction = 0.15f },
            new[] { "thick_skin" });

        // REBOUND reads the bite that LANDED, after the branch's cuts, and pays in hit size like BULWARK.
        Greater(n, b, "rebound", "REBOUND — A FIFTH OF EVERY BITE COMES BACK AS HEALTH, DAMAGE DEALT -15%",
            S with { HealOnBiteFraction = 0.20f, DamageDealt = 0.85f },
            SpineNotables(n, b));
        Greater(n, b, "bulwark", "BULWARK — DAMAGE TAKEN -30%, DAMAGE DEALT -15%",
            S with { DamageTaken = 0.70f, DamageDealt = 0.85f },
            SpineNotables(n, b));
        Greater(n, b, "bastion", "BASTION — +1% DAMAGE PER 250 MAXIMUM HEALTH",
            S with { DamagePerMaxHealth = 0.01f / 250f },
            SpineNotables(n, b));
        // The side road's greater pays in the OPPOSED branch's currency: mitigation bought with time.
        // BULWARK pays in hit size; this pays in rate, so the two Endure greaters are different prices
        // for the same wall rather than one price twice.
        Greater(n, b, "brace", "BRACE — DAMAGE TAKEN -20%, COOLDOWNS +20%",
            S with { DamageTaken = 0.80f, SkillRate = 0.83f },
            new[] { "thorns" });

        // The depth-pusher's node: it converts a health POOL into a per-wave ALLOWANCE, which is strictly
        // better the deeper you go and strictly worse in any single hard wave.
        Mastery(n, b, "endless", "ENDLESS — FULL HEALTH EVERY WAVE; MAXIMUM HEALTH HALVED",
            S with { FullHealBetweenWaves = true, MaxHealth = 0.5f },
            Greaters(n, b));
    }

    // ── BRIDGES — hybridising, at a price that costs a mastery. ───────────────────────────────────

    private static void Bridges(List<MasteryNode> n)
    {
        Bridge(n, Branch.Weight, Branch.Tempo, "executioner",
            "EXECUTIONER — THE FIRST HIT ON EACH CREATURE +10% AND IT CUTS 9 ARMOUR",
            S with { FirstHitMultiplier = 1.10f, ArmourPenetration = 9f });
        Bridge(n, Branch.Tempo, Branch.Spread, "volley",
            "VOLLEY — +1 TARGET AND -20% COOLDOWN, HITS -25%",
            S with { ExtraTargets = 1, SkillRate = 1.25f, HitSize = 0.75f });
        Bridge(n, Branch.Spread, Branch.Endure, "feedback",
            "FEEDBACK — HEAL 1.2% OF MAXIMUM PER CREATURE STRUCK",
            S with { HealPerTargetStruck = 0.012f });
        Bridge(n, Branch.Endure, Branch.Weight, "anchor",
            "ANCHOR — +1% HIT SIZE PER 200 MAXIMUM HEALTH",
            S with { HitSizePerMaxHealth = 0.01f / 200f });
    }

    // ── FORM SPECIALISATIONS — the only triggers this tree grants. ────────────────────────────────

    /// <summary>
    /// One per Form, hanging off the axis its Form belongs to.
    /// </summary>
    /// <remarks>
    /// This is how the skill tree stops giving away what the trait tree sells. The old tree handed out
    /// general triggers for free while the Dust tree charged a permanent price for the same ones. Here the
    /// skill tree grants only the six FORM-COMBO triggers — each dead weight without its Form, so it is a
    /// specialisation rather than a gift — and every general trigger (Echo, Bloodlust, Undying, Venom,
    /// Splinter, Harvest, Desperation, Zeal, NoHealing) belongs to the trait tree alone.
    /// </remarks>
    private static void Specialisations(List<MasteryNode> n)
    {
        Spec(n, Branch.Weight, Form.Strike, "spec_strike", "STRIKE SPECIALIST — EXECUTE WEAKENED FOES",
            BuildTrigger.Execute, S with { CullThreshold = 0.35f, CullBonus = 0.40f });
        Spec(n, Branch.Weight, Form.Trap, "spec_trap", "TRAP SPECIALIST — TRAPS RE-ARM ON BEING HIT",
            BuildTrigger.Coiled, S);
        Spec(n, Branch.Spread, Form.Projectile, "spec_projectile", "VOLLEY SPECIALIST — ONE MORE SHOT, ONE MORE TARGET",
            BuildTrigger.Overdraw, S with { FormTargets = new Dictionary<Form, int> { [Form.Projectile] = 1 } });
        Spec(n, Branch.Spread, Form.Aura, "spec_aura", "AURA SPECIALIST — TICKS 30% FASTER",
            BuildTrigger.Radiance, S);
        Spec(n, Branch.Tempo, Form.Mark, "spec_mark", "MARK SPECIALIST — THE WINDOW STRETCHES",
            BuildTrigger.Linger, S with { MarkWindowMultiplier = 1.3f });
        // Grants SIPHON, whose two halves are read from HealTuning: the leech doubling, and the raised
        // per-wave healing limit that makes the doubling measurable under the heal ceiling.
        Spec(n, Branch.Endure, Form.Transformation, "spec_transformation",
            $"MORPH SPECIALIST — LEECH DOUBLED, HEAL LIMIT {HealTuning.Default.CeilingText(siphon: true)} A WAVE",
            BuildTrigger.Siphon, S);
    }

    // ── Construction helpers. Ring decides cost, so a node cannot be mispriced by hand. ───────────

    private static void Minor(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape)
        => n.Add(new(id, MasteryKind.Minor, b, 1, RingCost[1], label, shape, null, new[] { StartId }));

    /// <summary>
    /// A SPUR minor: ring 1 by price, but it hangs off <paramref name="parent"/> rather than off START,
    /// and the layout draws it off that parent's shoulder. See <see cref="MasteryNode.Spur"/>.
    /// </summary>
    private static void Spur(List<MasteryNode> n, Branch b, string id, string parent, string label,
                             SkillShape shape)
        => n.Add(new(id, MasteryKind.Minor, b, 1, RingCost[1], label, shape, null, new[] { parent })
        {
            Spur = true,
        });

    private static void Notable(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                string[] prereqs)
        => n.Add(new(id, MasteryKind.Notable, b, 2, RingCost[2], label, shape, null, prereqs));

    private static void Greater(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                string[] prereqs)
        => n.Add(new(id, MasteryKind.Greater, b, 3, RingCost[3], label, shape, null, prereqs));

    private static void Mastery(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                string[] prereqs)
        => n.Add(new(id, MasteryKind.Mastery, b, 4, RingCost[4], label, shape, null, prereqs));

    /// <summary>A bridge needs ring 2 of BOTH neighbours — hybridising is earned, not splashed.</summary>
    private static void Bridge(List<MasteryNode> n, Branch a, Branch c, string id, string label, SkillShape shape)
        => n.Add(new(id, MasteryKind.Bridge, a, 2, BridgeCost, label, shape, null, NotablesOf(n, a).ToArray())
        {
            Link = c,
            SecondPrereqs = NotablesOf(n, c).ToArray(),
        });

    private static void Spec(List<MasteryNode> n, Branch b, Form form, string id, string label,
                             BuildTrigger grant, SkillShape shape)
        => n.Add(new(id, MasteryKind.Specialisation, b, 3, SpecialisationCost, label, shape, grant,
                     NotablesOf(n, b).ToArray()) { Form = form });

    private static IEnumerable<string> NotablesOf(List<MasteryNode> n, Branch b)
        => n.Where(x => x.Branch == b && x.Kind == MasteryKind.Notable).Select(x => x.Id);

    // ── "Any of the ring below". A fan node accepts any SPINE node of the ring inside it — never a spur
    //    or a side-road node, which belong to the road and not the fan. Read at construction, so the
    //    list is exactly the nodes declared above the caller; a branch that declares its side road
    //    before its fan would wire the wrong prerequisites, and the road test is what would say so. ──

    /// <summary>Every spine minor of the branch declared so far (the spur excluded).</summary>
    private static string[] SpineMinors(List<MasteryNode> n, Branch b)
        => n.Where(x => x.Branch == b && x.Kind == MasteryKind.Minor && !x.Spur).Select(x => x.Id).ToArray();

    /// <summary>Every fan notable of the branch declared so far — those that hang off the spine minors.</summary>
    private static string[] SpineNotables(List<MasteryNode> n, Branch b)
        => n.Where(x => x.Branch == b && x.Kind == MasteryKind.Notable
                        && x.Prereqs.All(p => ById(n, p) is { Kind: MasteryKind.Minor, Spur: false }))
            .Select(x => x.Id).ToArray();

    /// <summary>Every greater of the branch — a capstone accepts any of them, side road included.</summary>
    private static string[] Greaters(List<MasteryNode> n, Branch b)
        => n.Where(x => x.Branch == b && x.Kind == MasteryKind.Greater).Select(x => x.Id).ToArray();

    private static MasteryNode? ById(List<MasteryNode> n, string id) => n.FirstOrDefault(x => x.Id == id);
}
