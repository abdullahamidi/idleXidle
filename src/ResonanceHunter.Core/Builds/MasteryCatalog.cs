using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Builds;

/// <summary>The four demands the content makes, and therefore the four branches that answer them.</summary>
/// <remarks>
/// Named for the ANSWER, not the enemy. A branch called "Anti-Armour" would be a lookup table; a branch
/// called WEIGHT is a way of fighting that happens to beat armour, and can be wrong somewhere else.
/// </remarks>
public enum Branch
{
    /// <summary>
    /// How strong every skill is BEFORE you choose one. Opposed to <see cref="Spread"/>.
    /// </summary>
    /// <remarks>
    /// Was WEIGHT — few, enormous hits, the answer to Armoured. The 2026-08-30 re-axe retired it:
    /// under the skill rework the four enemy bands are answered by SKILLS (HAMMER's defence break
    /// answers Armoured, VOLLEY's hit count answers Swarm), so a branch that also sold hit size was
    /// selling the same rule twice at two prices. What a band never asks about is how strong a skill
    /// is before any of them is picked — the Source matchup, the affinity lean, what a point of
    /// resonance is worth — and that is what this branch sells now. Never cooldown: VOLLEY owns it.
    /// See design/gdd/skill-slots-and-skill-trees.md §9.
    /// </remarks>
    Resonance,

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
            new(StartId, MasteryKind.Start, Branch.Resonance, 0, 0, "YOU", S, null, Array.Empty<string>()),
        };

        Resonance(n);
        Spread(n);
        Tempo(n);
        Endure(n);
        Bridges(n);
        Specialisations(n);
        return n;
    }

    // ── WEIGHT — answers Armoured. Fewer, larger hits. ────────────────────────────────────────────

    /// <summary>
    /// RESONANCE — skill power, the Source matchup, Vow strength (design §9b).
    /// </summary>
    /// <remarks>
    /// The minors pay in plain resonance and are not meant to be read; everything from notable upward
    /// is a named rule that turns one of the seven dials on <see cref="SkillShape"/>. The three that
    /// touch the Source matchup — KEYED, DISCORD and CHORD — deliberately turn the SAME number, so a
    /// build holding two of them cannot stack one promise twice.
    /// </remarks>
    private static void Resonance(List<MasteryNode> n)
    {
        const Branch b = Branch.Resonance;

        // ── Ring 1: plain resonance. The connective tissue a road is walked on. ──────────────────
        Minor(n, b, "chime", "CHIME — +6 RESONANCE", S, Stat(HunterStat.ResonanceAffinity, 6f));
        Minor(n, b, "hum", "HUM — +6 RESONANCE", S, Stat(HunterStat.ResonanceAffinity, 6f));
        Minor(n, b, "tone", "TONE — +8 RESONANCE", S, Stat(HunterStat.ResonanceAffinity, 8f));
        Minor(n, b, "steep", "STEEP — +6 RESONANCE, +5 HEALTH", S,
              Stat(HunterStat.ResonanceAffinity, 6f, HunterStat.MaxHealth, 5f));
        Minor(n, b, "clarity_res", "CLARITY — +8 RESONANCE", S, Stat(HunterStat.ResonanceAffinity, 8f));
        // The spur, and the head of the side road: the longer way to the rim.
        Spur(n, b, "timbre", "clarity_res", "TIMBRE — +6 RESONANCE, +4 ATTACK POWER", S,
             Stat(HunterStat.ResonanceAffinity, 6f, HunterStat.AttackPower, 4f));

        // ── Ring 2: the named rules. ─────────────────────────────────────────────────────────────
        Notable(n, b, "keyed", "KEYED — YOUR STRONG SOURCE MATCHUP PAYS 25% MORE",
                S with { StrongMatchupBonus = 0.25f }, SpineMinors(n, b));
        Notable(n, b, "pledge", "PLEDGE — VOWS PAY 30% MORE",
                S with { VowPowerMultiplier = 1.30f }, SpineMinors(n, b));
        Notable(n, b, "narrow", "NARROW — YOUR DISCIPLINE'S FORM +20%, EVERY OTHER FORM -10%",
                S with { AffinityStyleBonus = 0.20f, OffStylePenalty = 0.10f }, SpineMinors(n, b));
        Notable(n, b, "broad", "BROAD — HALF OF THE OPPOSITE FORM'S PENALTY IS GIVEN BACK",
                S with { OppositePenaltyRelief = 0.5f }, SpineMinors(n, b));
        // The side road's notable: it needs the spur, not the spine.
        Notable(n, b, "deep", "DEEP — +12 RESONANCE, AND RESONANCE IS WORTH 20% MORE",
                S with { ResonanceWorth = 0.20f }, new[] { "timbre" },
                Stat(HunterStat.ResonanceAffinity, 12f));

        // ── Ring 3. ──────────────────────────────────────────────────────────────────────────────
        Greater(n, b, "pure", "PURE — WHILE EVERY WOVEN SKILL SHARES ONE SOURCE, ALL SKILLS HIT 35% HARDER",
                S with { OneSourceBonus = 0.35f }, SpineNotables(n, b));
        Greater(n, b, "discord", "DISCORD — YOUR WEAK SOURCE MATCHUP NO LONGER WEAKENS YOU, SKILLS -10%",
                S with { WeakMatchupRelief = 1f, DamageDealt = 0.90f }, SpineNotables(n, b));
        Greater(n, b, "resonant", "RESONANT — SKILLS +25%, YOUR BASIC ATTACK -15%",
                S with { DamageDealt = 1.25f, AutoAttackDamage = 0.85f }, SpineNotables(n, b));
        // The side road's greater, off DEEP.
        Greater(n, b, "zealot", "ZEALOT — VOWS PAY 60% MORE, AND YOU TAKE 15% MORE DAMAGE",
                S with { VowPowerMultiplier = 1.60f, DamageTaken = 1.15f }, new[] { "deep" });

        // ── Ring 4: the capstone. Any greater opens it, the side road included. ──────────────────
        Mastery(n, b, "chord", "CHORD — EVERY SOURCE MATCHUP COUNTS AS STRONG, AND EVERY SKILL IS 15% SOFTER",
                S with { AllMatchupsStrong = true, DamageDealt = 0.85f }, Greaters(n, b));
    }

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
        Bridge(n, Branch.Resonance, Branch.Tempo, "executioner",
            "EXECUTIONER — THE FIRST HIT ON EACH CREATURE +10% AND IT CUTS 9 ARMOUR",
            S with { FirstHitMultiplier = 1.10f, ArmourPenetration = 9f });
        Bridge(n, Branch.Tempo, Branch.Spread, "volley",
            "VOLLEY — +1 TARGET AND -20% COOLDOWN, HITS -25%",
            S with { ExtraTargets = 1, SkillRate = 1.25f, HitSize = 0.75f });
        Bridge(n, Branch.Spread, Branch.Endure, "feedback",
            "FEEDBACK — HEAL 1.2% OF MAXIMUM PER CREATURE STRUCK",
            S with { HealPerTargetStruck = 0.012f });
        Bridge(n, Branch.Endure, Branch.Resonance, "anchor",
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
        Spec(n, Branch.Resonance, Form.Strike, "spec_strike", "STRIKE SPECIALIST — EXECUTE WEAKENED FOES",
            BuildTrigger.Execute, S with { CullThreshold = 0.35f, CullBonus = 0.40f });
        Spec(n, Branch.Resonance, Form.Trap, "spec_trap", "TRAP SPECIALIST — TRAPS RE-ARM ON BEING HIT",
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

    private static void Minor(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                              IReadOnlyDictionary<HunterStat, float>? stats = null)
        => n.Add(new(id, MasteryKind.Minor, b, 1, RingCost[1], label, shape, null, new[] { StartId })
        {
            Stats = stats,
        });

    /// <summary>One or two plain stats, for a node whose whole payment is stats.</summary>
    private static Dictionary<HunterStat, float> Stat(HunterStat a, float av,
                                                      HunterStat? c = null, float cv = 0f)
    {
        var d = new Dictionary<HunterStat, float> { [a] = av };
        if (c is { } second) d[second] = cv;
        return d;
    }

    /// <summary>
    /// A SPUR minor: ring 1 by price, but it hangs off <paramref name="parent"/> rather than off START,
    /// and the layout draws it off that parent's shoulder. See <see cref="MasteryNode.Spur"/>.
    /// </summary>
    private static void Spur(List<MasteryNode> n, Branch b, string id, string parent, string label,
                             SkillShape shape, IReadOnlyDictionary<HunterStat, float>? stats = null)
        => n.Add(new(id, MasteryKind.Minor, b, 1, RingCost[1], label, shape, null, new[] { parent })
        {
            Spur = true,
            Stats = stats,
        });

    private static void Notable(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                string[] prereqs, IReadOnlyDictionary<HunterStat, float>? stats = null)
        => n.Add(new(id, MasteryKind.Notable, b, 2, RingCost[2], label, shape, null, prereqs)
        {
            Stats = stats,
        });

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
