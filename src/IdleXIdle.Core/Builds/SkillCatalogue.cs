using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Core.Builds;

/// <summary>
/// The six STYLES — how a champion fights. The identity axis, and the only thing mastery reads.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of <see cref="Form"/> that was never behaviour. Form was doing four jobs at once:
/// identity (the affinity hexagon), mastery destination (the six specialisation nodes), behaviour
/// (cooldown, targets, passive-or-not) and damage. The first two are STYLE and live here; the last
/// two are SKILL and live in <see cref="SkillDef"/>. See
/// <c>design/gdd/skill-slots-and-skill-trees.md</c> §3.
/// </para>
/// <para>
/// <b>THE ENUM ORDER IS THE RING</b>, exactly as it was for Form: affinity distance is cyclic
/// distance, so three steps apart is opposite. The order is CORRECTED from Form's, which had a real
/// contradiction in it — Strike and Trap sat opposite each other on the hexagon while
/// <see cref="MasteryCatalog"/> put both of them in the Weight branch, so the tree said they deepen
/// together and the ring said they fight. The three oppositions are semantic and owe the mastery tree
/// nothing, which matters because §9 moves the style roads off the branches entirely:
/// </para>
/// <list type="bullet">
/// <item><b>HAMMER ↔ VOLLEY</b> — one enormous blow against many small hits.</item>
/// <item><b>SNARE ↔ FIELD</b> — waits and pays once, against never stops and touches everything.</item>
/// <item><b>SIGN ↔ DRAIN</b> — front-loaded burst against slow return.</item>
/// </list>
/// </remarks>
public enum Style
{
    /// <summary>You strike rarely, and every blow is enormous.</summary>
    Hammer,

    /// <summary>Being attacked works in your favour.</summary>
    Snare,

    /// <summary>You deal nothing yourself; you make everything else bigger.</summary>
    Sign,

    /// <summary>You strike often and reach several enemies at once.</summary>
    Volley,

    /// <summary>You hit the whole wave, all the time.</summary>
    Field,

    /// <summary>You take part of the damage you deal back as health.</summary>
    Drain,
}

/// <summary>WHEN a skill acts. The three branches <see cref="SoloBattle"/> already has.</summary>
/// <remarks>
/// The fight loop branches three ways — <c>Field</c> to the aura block, <c>Reaction</c> to the trap
/// block, <c>Active</c> to the cast block. This enum is that same fork asked as a question about the
/// SKILL, which is why the sim's shape did not change when it moved over.
///
/// <b>Only <see cref="Active"/> costs a beat.</b> A Field ticks on its own clock above the beat gate
/// and a Reaction answers an event, so neither can ever take the champion's action. That is the whole
/// reason two passive slots are free, and it is structural rather than a matter of discipline.
/// </remarks>
public enum SkillKind
{
    /// <summary>Takes the champion's action. One per beat, and only one skill may have it.</summary>
    Active,

    /// <summary>Always on, acting on its own millisecond clock. Never takes a beat.</summary>
    Field,

    /// <summary>Fires on an event — see <see cref="ReactionOn"/>. Never takes a beat.</summary>
    Reaction,
}

/// <summary>WHAT a skill does, kept separate from WHEN it does it.</summary>
/// <remarks>
/// Two axes rather than one, on purpose: a Field that amplifies (SIGN's BRAND) needs no new system,
/// it is a new setting of two boxes that already exist. Collapsing them would make every new
/// combination a new code path, which is how the Form table ended up as six price tags.
/// </remarks>
public enum SkillEffect
{
    Damage,
    Amplify,
    Heal,
}

/// <summary>The event a <see cref="SkillKind.Reaction"/> answers.</summary>
public enum ReactionOn
{
    None,
    Kill,
    Bitten,
    LowHealth,
    WaveStart,
}

/// <summary>One of the three nodes that strengthen a chosen <see cref="SkillVariation"/>.</summary>
/// <remarks>
/// A reinforcement belongs to its variation and must be worthless to the other one. That is what
/// makes the variation a real fork rather than a label on a shared upgrade path.
/// </remarks>
public sealed record Reinforcement(string Name, string Line, Func<SkillDef, SkillDef>? Modify = null);

/// <summary>
/// One of the two ways a skill can be taken. Changes what the skill DOES, not how much of it.
/// </summary>
/// <param name="Modify">
/// How the skill's dials change when this variation is taken.
/// </param>
/// <remarks>
/// <b>A DELTA ON THE DEFINITION, not a branch in the fight loop.</b> The variation says what the
/// skill BECOMES and the sim keeps reading one <see cref="SkillDef"/>, so a fight-loop that already
/// knows how to read a dial does not gain a case per variation — which is how the Form table turned
/// into six price tags with a switch behind each. It also means a variation and a reinforcement
/// compose by construction: each is a function on the definition, applied in order.
/// </remarks>
public sealed record SkillVariation(
    string Name,
    string Line,
    IReadOnlyList<Reinforcement> Reinforcements,
    Func<SkillDef, SkillDef>? Modify = null,
    Source Source = Source.Body);

/// <summary>
/// The RULES a variation's reinforcements turn on — one typed group rather than fifteen loose flags.
/// </summary>
/// <remarks>
/// <para>
/// The definition already carries some forty numeric dials, and the content rework needs about
/// fifteen more switches. Adding them to <see cref="SkillDef"/> one at a time is how a record becomes
/// a field list nobody can read, so they arrive here instead: one nested record, every member named
/// for the reinforcement that turns it, and every one of them read in exactly one place in the fight.
/// </para>
/// <para>
/// This is deliberately NOT a scripting layer. Each member is a plain value with a single consumer;
/// there is no dispatch, no ordering to remember and nothing to register. A rule with no consumer is
/// a bug, and the liveness tests exist to prove there is not one.
/// </para>
/// </remarks>
/// <param name="OverkillCarry">HAMMER/BREAKTHROUGH, HAMMER/CLEAN CUT, VOLLEY/PUNCH THROUGH — the share of THIS skill's direct overkill that carries into the next living enemy.</param>
/// <param name="TrailNextSwing">HAMMER/TRAIL — after this skill resolves, the NEXT basic attack ignores defence. One use, not a standing grant.</param>
/// <param name="AmplifyHits">SIGN/SPEND — the window empowers this many damaging hits and then closes, instead of running purely on a clock.</param>
/// <param name="CritKeepsAmplifyCharge">SIGN/PERFECT CLAUSE — a critical hit takes the amplification without spending one of those charges.</param>
/// <param name="AmplifyFrontFull">SIGN/ANCHOR — the front enemy is marked this deep while the rest of the wave keeps the weaker SPRAWL depth.</param>
/// <param name="PaybackBelowHealth">SNARE/SCARRED — the health fraction under which REPAY pays more. Reads HEALTH, never shield.</param>
/// <param name="PaybackBelowHealthBonus">SNARE/SCARRED — and how much more.</param>
/// <param name="ExtraBleedWhileBleeding">VOLLEY/FLOOD — extra kill bleed while bleed is already running, as a share of the ordinary contribution. Bounded, and never compounding.</param>
/// <param name="WeakenPerDeadEnemy">FIELD/REMNANT — how much softer the wave bites for each enemy it has already lost.</param>
/// <param name="WeakenPerDeadCap">FIELD/REMNANT — the floor that weakening cannot pass.</param>
/// <param name="HealCeilingBonus">DRAIN/BALM — how much wider this skill makes the wave's healing ceiling.</param>
/// <param name="HealthScalingAbove">DRAIN/RIPE — the health fraction above which the health-scaling ceiling rises.</param>
/// <param name="HealthScalingAboveBonus">DRAIN/RIPE — and by how much.</param>
/// <param name="AmplifyStartsPrimed">SIGN/FOUNDATION — the wave opens with one stack of the standing amplify already established.</param>
/// <param name="StunTimedToBite">HAMMER/INTERCEPT — PIN holds its stun until the wave's next bite is imminent instead of spending it on its own clock.</param>
/// <param name="SplitByHealth">FIELD/BALANCE — a split pool is distributed toward higher-health enemies first, so less of it is wasted as overkill.</param>
/// <param name="RetargetOnDeath">VOLLEY/PUNCH THROUGH — hits left over when a target dies continue into another living enemy instead of vanishing.</param>
/// <param name="BonusUnderHealth">VOLLEY/CLEANUP — the share of maximum health under which an enemy takes the finishing bonus.</param>
/// <param name="BonusUnderHealthAmount">VOLLEY/CLEANUP — how much harder a hit lands on an enemy under that share.</param>
public sealed record SkillRules(
    float OverkillCarry = 0f,
    bool TrailNextSwing = false,
    int AmplifyHits = 0,
    bool CritKeepsAmplifyCharge = false,
    float AmplifyFrontFull = 0f,
    float PaybackBelowHealth = 0f,
    float PaybackBelowHealthBonus = 0f,
    float ExtraBleedWhileBleeding = 0f,
    float WeakenPerDeadEnemy = 0f,
    float WeakenPerDeadCap = 0f,
    float HealCeilingBonus = 0f,
    float HealthScalingAbove = 0f,
    float HealthScalingAboveBonus = 0f,
    bool AmplifyStartsPrimed = false,
    bool StunTimedToBite = false,
    bool SplitByHealth = false,
    bool RetargetOnDeath = false,
    float BonusUnderHealth = 0f,
    float BonusUnderHealthAmount = 0f)
{
    /// <summary>The base line: a skill turns none of these on until a variation or reinforcement does.</summary>
    public static readonly SkillRules None = new();
}

/// <summary>
/// One skill: a distinct ability belonging to one style, unlocked by its own mastery node.
/// </summary>
/// <remarks>
/// <para>
/// <b>Distinct skills, not two faces of one.</b> An earlier design gave each style a single skill
/// woven either as a move that takes the champion's turn or as a state that never does. The designer
/// replaced it: that was a layer the player had to carry ("this is HAMMER, but its passive half").
/// Twelve separate abilities delete it, and a node per skill makes the catalogue growable for the
/// life of the game — a new skill is a new node and nothing else changes.
/// </para>
/// <para>
/// Depth is deliberately small: two variations, three reinforcements each, four purchases in all,
/// bought with levels the skill earns from being used. The previous design gave every skill a
/// five-ring twelve-node tree and was rejected as too clever to read.
/// </para>
/// </remarks>
/// <param name="Id">Stable identifier. Persisted; never renamed once a save has seen it.</param>
/// <param name="Style">The one style this skill belongs to — the ONLY field mastery and affinity read.</param>
/// <param name="Kind">When it acts. Only <see cref="SkillKind.Active"/> costs a beat.</param>
/// <param name="Beats">Beats between casts for an Active; 0 otherwise.</param>
/// <param name="IntervalMs">Milliseconds between ticks for a Field; 0 otherwise. Must be a multiple of 100.</param>
/// <param name="On">The event a Reaction answers.</param>
/// <param name="Targets">How many creatures one activation reaches. <see cref="int.MaxValue"/> is the wave.</param>
public sealed record SkillDef(
    string Id,
    string Name,
    Style Style,
    SkillKind Kind,
    SkillEffect Effect,
    string Line,
    int Beats,
    int IntervalMs,
    ReactionOn On,
    int Targets,
    string ClipKey,
    string FxKey,
    IReadOnlyList<SkillVariation> Variations,
    // ── WHAT ONE ACTIVATION IS WORTH — the number the Form table used to own. Per activation for
    //    an Active or a Reaction; PER SECOND for a Field's damage path (the loop scales it by the
    //    tick interval). Zero for a skill whose base line deals nothing (PRESS, WILT, the SIGNs,
    //    REPAY, WEEP) — their dials below are their whole sentence. The six skills that used to
    //    borrow a sibling's Form value have AUTHORED numbers at last: PULSE was casting the Aura
    //    tick value (12) and measured NET-NEGATIVE against bare hands on the bench. ─────────────
    float BasePower = 0f,
    // ── A Reaction's re-arm clock, in ms. JAWS said "3s" and IRON "6s" while both re-armed at an
    //    8,000 ms Form-table fallback; the card is the spec now. 0 for non-Reactions. ───────────
    int RearmMs = 0,
    // ── WHAT THE BASE LINE DOES, as numbers the sim reads. ────────────────────────────────────────
    //
    // Zero everywhere by default, so a skill carries only the dials its own sentence needs and the
    // fight can ask every skill about every dial without a switch on the id. Gameplay values are data
    // here rather than constants in the loop — the repo's standing rule, and what will let a variation
    // move one of these without touching another skill of the same style.
    float DefenceBreakPerTick = 0f,      // PRESS: defence stripped from the target each tick
    float DefenceBreakFloor = 0f,        // PRESS: how far defence may be driven, negative
    float SlowFraction = 0f,             // MIRE: how much every enemy's attack interval stretches
    float AttackBreakPerTick = 0f,       // WILT: fraction of its damage each enemy loses per tick
    float AttackBreakFloor = 0f,         // WILT: the deepest that break may go, negative
    float BleedOnKillFraction = 0f,      // WEEP: bleed left on a kill, as a fraction of max health
    float PaysBackDamageTaken = 0f,      // REPAY: multiple of the damage taken since its last cast
    int PaybackWindowMs = 0,             // REPAY/VENGEANCE: only damage this recent counts. 0 = all of it

    // ── WHAT THE VARIATIONS TURN. Every one is zero or false at the base line, so a skill carries
    //    only the dials its own sentence needs and a variation is a DELTA on this record rather than
    //    a branch in the fight loop. Twenty-four variations would otherwise be twenty-four cases. ──
    bool DefenceIgnore = false,          // HAMMER/FLATTEN
    float ExecuteFraction = 0f,          // HAMMER/FINISH — kills a target under this share of health
    int StunMs = 0,                      // HAMMER/PIN
    bool ShieldInsteadOfDamage = false,  // SNARE/BANKED — the banked total becomes a shield
    float ShieldFromStoppedBite = 0f,    // SNARE/IRON/PLATING — a stopped bite's share that becomes shield
    float WaveStartShieldFraction = 0f,  // SNARE/BANKED/CARRIED — shield at each wave's start
    float ReflectFraction = 0f,          // SNARE/JAWS base, deepened by NET
    bool StopsWholeBite = false,         // SNARE/IRON
    float AmplifyPercent = 0f,           // SIGN — how much everything else is multiplied by
    int AmplifyMs = 0,                   // SIGN/CALL — how long the window holds
    float AmplifyPerCast = 0f,           // SIGN/STEADY — added per cast
    float AmplifyCap = 0f,               // SIGN/STEADY — and its ceiling
    bool AmplifyWholeWave = false,       // SIGN/SPRAWL — the mark covers everything, at half
    float AmplifyDeepenPerTick = 0f,     // SIGN/ETCH
    float AmplifyDeepenCap = 0f,         // SIGN/ETCH
    float BleedRate = 1f,                // VOLLEY/TORRENT — multiplier on how fast bleed pays
    bool BleedCarriesWaves = false,      // VOLLEY/CARRION
    float DamagePerLivingEnemy = 0f,     // FIELD/THRONG
    float SplitPool = 0f,                // FIELD/SHARE — total damage split between the living
    int SplitMaxWays = 0,                // FIELD/SHARE — and the most ways it may split
    float SlowDeepenPerTick = 0f,        // FIELD/NUMB
    float SlowCeiling = 0f,              // FIELD/NUMB and TEEMING
    float SlowPerEnemy = 0f,             // FIELD/TEEMING
    float Lifesteal = 0f,                // DRAIN/DRINK base, doubled by THIRST
    float DamagePerHealth = 0f,          // DRAIN/GLUT — damage rises with health held
    float HealPerPulse = 0f,             // DRAIN/SUP — share of maximum health each pulse returns
    bool FrontEnemyOnly = false,         // DRAIN/SHRIVEL
    int MinimumHits = 0,                 // VOLLEY/SPLAY — arrows double up when the wave is small
    int HitsPerTarget = 1,               // VOLLEY/CLUSTER — arrows landed on EACH creature reached

    // ── WHAT THE REINFORCEMENTS TURN. Ten, deliberately: a dial per reinforcement would be seventy
    //    of them, and most of what a reinforcement wants to say is "the same thing, more of it" —
    //    which the variation's own dials already express. These are the few shapes that recur. ────
    float DamageMultiplier = 1f,         // "hits harder" — also scales what BANKED turns into a shield
    float CooldownMultiplier = 1f,       // "comes back sooner", or pays for a bigger number
    int TargetsBonus = 0,                // "reaches one more"
    int ExecutesPerWave = 1,             // HAMMER/TWICE
    int CrowdedBeats = 0,                // FIELD/CROWDED — living enemies needed for the faster cadence
    bool SwingIgnoresArmour = false,     // HAMMER/TRAIL — the plain attack borrows the blow's rule
    float SwingLifesteal = 0f,           // DRAIN/TRICKLE — and it borrows the drink's
    float ReflectGrowthPerBite = 0f,     // SNARE/MESH and HARDEN
    float ReflectGrowthCap = 0f,
    float BleedFromHits = 0f,            // VOLLEY/FLIGHT, RUPTURE, ONSET — the cast itself bleeds
    float BreakSecondEnemy = 0f,         // DRAIN/HOLLOW — SHRIVEL reaches past the front one

    // ── THE RULES A REINFORCEMENT TURNS. One typed group (see SkillRules) rather than fifteen more
    //    flags on this record, which is already at the width where a reader stops reading. ────────
    SkillRules? Rules = null)
{
    /// <summary>Does this skill cost the champion its action?</summary>
    public bool TakesABeat => Kind == SkillKind.Active;

    /// <summary>The rules this skill turns on — never null at the point of use.</summary>
    public SkillRules Rule => Rules ?? SkillRules.None;
}

/// <summary>
/// The twelve skills — one active and one passive per style.
/// </summary>
/// <remarks>
/// Authored 2026-08-30 and gated against the two laws in
/// <c>design/gdd/skill-slots-and-skill-trees.md</c> §8 and the mechanic ownership table in §6. Six
/// entries were cut for cause during that gate; §6 records each and the rule it broke.
/// </remarks>
public static class SkillCatalogue
{
    // THE CADENCE CEILING, and why only ONE skill may be a four-beat one.
    //
    // Two actives is the whole point of the rework, so the pair a player can build decides whether the
    // champion's own swing survives. Beat demand is the sum of 1/beats, and the swing only lands on
    // what is left over — so two four-beat skills demand exactly half the beats and the fix undoes
    // itself. The fastest skill is VOLLEY's SPRAY at four (volume is that style's whole identity) and
    // nothing else goes under five, which puts the worst pair a player can weave at 0.45.
    //
    // The catalogue arrived from its authoring pass with SPRAY and PULSE both at THREE, which is 0.67
    // — worse than the four-slot state this rework exists to fix. Caught by the beat-demand tests,
    // not by reading.

    /// <summary>How many creatures a skill that reaches the whole wave reports.</summary>
    public const int WholeWave = int.MaxValue;

    /// <summary>How strongly the trained RESONANCE stat feeds every skill's base power.</summary>
    /// <remarks>Moved from the retired WeavingTuning (0.018 since 2026-08-26, when RESONANCE took
    /// over the load MIGHT used to carry for skills). One coefficient for all twelve skills.</remarks>
    public const float ResonancePerPoint = 0.018f;

    /// <summary>A skill's base power at this resonance — the root of every skill hit.</summary>
    public static float PoweredBase(SkillDef def, float resonance)
        => def.BasePower * (1f + ResonancePerPoint * resonance);

    private static SkillVariation V(string name, string line, Source source, Func<SkillDef, SkillDef> modify,
                                    params (string N, string L, Func<SkillDef, SkillDef> M)[] rs)
        => new(name, line, rs.Select(r => new Reinforcement(r.N, r.L, r.M)).ToList(), modify, source);

    public static IReadOnlyList<SkillDef> All { get; } = new List<SkillDef>
    {
        // ── HAMMER — ONE HIT SHOULD MATTER. Heavy hit, defence ignore, defence break, stun,
        //    execute, overkill. Nothing else may borrow these verbs. ────────────────────────────────
        new("hammer_blow", "BLOW", Style.Hammer, SkillKind.Active, SkillEffect.Damage,
            "Heavy damage to one target.",
            Beats: 6, IntervalMs: 0, On: ReactionOn.None, Targets: 1, BasePower: 500f,
            ClipKey: "strike", FxKey: "strike",
            Variations: new[]
            {
                V("FLATTEN", "Defence ignore: the blow ignores the target's defence.",
                    Source.Body,
                    d => d with { DefenceIgnore = true },
                    ("TOLL",  "The blow deals 40% more damage.",
                     d => d with { DamageMultiplier = 1.4f }),
                    // Was SHEAR, "reaches a second target" — a VOLLEY verb on a HAMMER skill. The waste
                    // a single huge hit suffers in a crowd is OVERKILL, and this answers it as HAMMER.
                    ("BREAKTHROUGH", "Half of the blow's wasted overkill carries into the next enemy.",
                     d => d with { Rules = d.Rule with { OverkillCarry = 0.5f } }),
                    ("TRAIL", "After the blow, your next basic attack ignores defence too.",
                     d => d with { Rules = d.Rule with { TrailNextSwing = true } })),
                // SHADOW, not MACHINE: execution, excess and death are SHADOW's fantasy, and MACHINE
                // needs its two actives elsewhere for a whole mono-Source build to exist.
                V("FINISH", "Execute threshold: the blow kills a target under 15% health. Once per wave.",
                    Source.Shadow,
                    d => d with { ExecuteFraction = 0.15f },
                    ("BRINK", "The execute threshold rises to 25% health.",
                     d => d with { ExecuteFraction = 0.25f }),
                    ("TWICE", "The blow may execute twice each wave, and the threshold rises to 20% health.",
                     d => d with { ExecutesPerWave = 2, ExecuteFraction = 0.20f }),
                    // Was SPUR, a flat cooldown cut. The branch is about death and excess force, so its
                    // third purchase is what the excess does rather than how often the blow comes round.
                    ("CLEAN CUT", "When the blow executes, its wasted overkill carries into the next enemy.",
                     d => d with { Rules = d.Rule with { OverkillCarry = 1.0f } })),
            }),

        new("hammer_press", "PRESS", Style.Hammer, SkillKind.Field, SkillEffect.Damage,
            "A weight sits on the front enemy: its defence drops 5 every 2s, down to -25.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
            ClipKey: "strike", FxKey: "press",
            Variations: new[]
            {
                V("CRUSHING", "The defence drop is 10 every 2s instead of 5, down to -50.",
                    Source.Body,
                    d => d with { DefenceBreakPerTick = 10f, DefenceBreakFloor = -50f },
                    ("SETTLE",    "The floor falls from -50 to -90.",
                     d => d with { DefenceBreakFloor = -90f }),
                    ("SEIZE",     "The weight breaks the two front enemies instead of one.",
                     d => d with { TargetsBonus = 1 }),
                    ("UNDERMINE", "The weight works every 1s instead of every 2s.",
                     d => d with { IntervalMs = 1000 })),
                V("PIN", "1s stun on the front enemy every 6s.",
                    Source.Machine,
                    d => d with { DefenceBreakPerTick = 0f, StunMs = 1000, IntervalMs = 6000 },
                    ("HOLD",   "The stun is 1.5s instead of 1s.",
                     d => d with { StunMs = 1500 }),
                    ("BUCKLE", "Each stun strips 10 defence from the front enemy.",
                     d => d with { DefenceBreakPerTick = 10f, DefenceBreakFloor = -50f }),
                    // Was SEAL, a faster clock. A stun spent just after a bite is nearly wasted and one
                    // spent just before it delays the whole interval — so the machine WAITS for the bite
                    // instead of firing on a metronome. Deterministic: it reads the wave's own bite clock.
                    ("INTERCEPT", "The stun waits for the wave's next bite instead of its own clock.",
                     d => d with { Rules = d.Rule with { StunTimedToBite = true } })),
            },
            DefenceBreakPerTick: 5f, DefenceBreakFloor: -25f),

        // ── SNARE — THE ENEMY SHOULD REGRET HITTING YOU. Retaliation, reflect, damage taken,
        //    reactive protection, shield from retaliation. ─────────────────────────────────────────
        new("snare_repay", "REPAY", Style.Snare, SkillKind.Active, SkillEffect.Damage,
            "Deals 200% of the health damage you have taken since its last cast.",
            Beats: 5, IntervalMs: 0, On: ReactionOn.None, Targets: 1,
            ClipKey: "trap", FxKey: "trap",
            Variations: new[]
            {
                V("VENGEANCE", "350% instead of 200%, but only health damage taken in the last 3s counts.",
                    Source.Shadow,
                    d => d with { PaysBackDamageTaken = 3.5f, PaybackWindowMs = 3_000 },
                    ("GRUDGE",  "It pays back 500% instead of 350%.",
                     d => d with { PaysBackDamageTaken = 5.0f }),
                    // Was SCARRED-as-flat-damage. The window is the branch's whole price, so the
                    // reinforcement that SHARPENS the risk pays the most: less time counted, far more
                    // paid for it.
                    ("RAW NERVE", "Only the last 2s counts, but it pays back 700%.",
                     d => d with { PaybackWindowMs = 2_000, PaysBackDamageTaken = 7.0f }),
                    ("SCARRED", "Below 40% health it pays back 50% more.",
                     d => d with { Rules = d.Rule with { PaybackBelowHealth = 0.40f, PaybackBelowHealthBonus = 0.50f } })),
                V("BANKED", "Instead of dealing it, the total becomes SHIELD of equal size.",
                    Source.Machine,
                    d => d with { ShieldInsteadOfDamage = true },
                    ("LINING",   "It banks 300% of the health damage taken instead of 200%.",
                     d => d with { PaysBackDamageTaken = 3.0f }),
                    ("STANDING", "The shield is 50% larger.",
                     d => d with { DamageMultiplier = 1.5f }),
                    // Was CARRIED-as-cooldown. Shield is wave-local by law, so the branch cannot carry
                    // a shield between waves — it can arrive at each wave already wearing one.
                    ("CARRIED",  "Every wave begins with SHIELD worth 10% of your maximum health.",
                     d => d with { WaveStartShieldFraction = 0.10f })),
            },
            PaysBackDamageTaken: 2.0f),

        new("snare_jaws", "JAWS", Style.Snare, SkillKind.Reaction, SkillEffect.Damage,
            "Every bite returns 50% of it to the enemy that bit you. Rearms every 3s.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Bitten, Targets: 1,
            ClipKey: "trap", FxKey: "trap",
            Variations: new[]
            {
                V("NET", "The reflect returns 100% of the bite instead of 50%.",
                    Source.Shadow,
                    d => d with { ReflectFraction = 1.0f },
                    ("MESH",   "The reflect grows 10% per bite taken this wave, up to +50%.",
                     d => d with { ReflectGrowthPerBite = 0.10f, ReflectGrowthCap = 0.50f }),
                    ("SPITE",  "The reflect returns 140% of the bite instead of 100%.",
                     d => d with { ReflectFraction = 1.4f }),
                    ("RECOIL", "The trap rearms a third sooner.",
                     d => d with { CooldownMultiplier = 0.66f })),
                V("IRON", "No reflect: the trap stops a whole bite, but rearms every 6s.",
                    Source.Machine,
                    d => d with { ReflectFraction = 0f, StopsWholeBite = true, RearmMs = 6_000 },
                    ("BLUNT",    "The trap rearms twice as fast.",
                     d => d with { CooldownMultiplier = 0.5f }),
                    ("REPRISAL", "A stopped bite is returned to the enemy that made it, in full.",
                     d => d with { ReflectFraction = 1.0f }),
                    // Was HARDEN, a growing reflect — NET's verb on IRON's branch. A stopped bite
                    // becoming armour is what MACHINE does with prevention.
                    ("PLATING",  "A stopped bite also becomes SHIELD worth half of what it would have dealt.",
                     d => d with { ShieldFromStoppedBite = 0.5f })),
            },
            ReflectFraction: 0.5f, RearmMs: 3_000),

        // ── SIGN — amplification only. IT DEALS NO DAMAGE ITSELF. ────────────────────────────────
        new("sign_call", "CALL", Style.Sign, SkillKind.Active, SkillEffect.Amplify,
            "All your damage +60% for 6s.",
            Beats: 5, IntervalMs: 0, On: ReactionOn.None, Targets: 1,
            ClipKey: "mark", FxKey: "mark",
            AmplifyPercent: 0.60f, AmplifyMs: 6_000, AmplifyWholeWave: true,
            Variations: new[]
            {
                // Was a 2s window at +200% — a timing lottery an idle game's player cannot play, since
                // they do not choose when anything fires. It empowers a COUNT of hits now: the same
                // burst, spent on hits rather than on seconds.
                V("SPEND", "The next 3 damaging hits land +200% harder. It waits until they land.",
                    Source.Mind,
                    // No clock. A hunter who does not choose when anything fires cannot aim a
                    // six-second window, and with one beside the count the count never bound: three
                    // hits and five hits were the same six seconds. Held until spent, the number IS
                    // the promise, and COUNT is a purchase the player can feel.
                    d => d with { AmplifyPercent = 2.0f, AmplifyMs = 600_000,
                                  Rules = d.Rule with { AmplifyHits = 3 } },
                    ("OVERSPEND", "Each empowered hit lands +300% instead of +200%.",
                     d => d with { AmplifyPercent = 3.0f }),
                    ("COUNT",     "It empowers 5 hits instead of 3.",
                     d => d with { Rules = d.Rule with { AmplifyHits = 5 } }),
                    ("PERFECT CLAUSE", "A critical hit is empowered without spending one of them.",
                     d => d with { Rules = d.Rule with { CritKeepsAmplifyCharge = true } })),
                V("STEADY", "Each cast adds +40% amplify for the rest of the wave, up to +80%.",
                    Source.Spirit,
                    d => d with { AmplifyPercent = 0f, AmplifyPerCast = 0.40f, AmplifyCap = 0.80f },
                    ("REDOUBLE", "Each cast adds +70% instead of +40%.",
                     d => d with { AmplifyPerCast = 0.70f }),
                    ("PILLAR",   "The cap rises from +80% to +160%.",
                     d => d with { AmplifyCap = 1.60f }),
                    // Was FOOTING, a cooldown cut. The branch is about a swell that accumulates, so its
                    // third purchase is where the swell STARTS.
                    ("FOUNDATION", "Every wave begins with one stack already standing.",
                     d => d with { Rules = d.Rule with { AmplifyStartsPrimed = true } })),
            }),

        new("sign_brand", "BRAND", Style.Sign, SkillKind.Field, SkillEffect.Amplify,
            "Your damage to the front enemy is +70%.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
            ClipKey: "mark", FxKey: "mark",
            AmplifyPercent: 0.70f,
            Variations: new[]
            {
                V("SPRAWL", "The mark covers every enemy instead, at half strength.",
                    Source.Mind,
                    d => d with { AmplifyWholeWave = true, AmplifyPercent = 0.35f },
                    ("EVEN",   "Every enemy's mark rises from half to three-quarters strength.",
                     d => d with { AmplifyPercent = 0.525f }),
                    // Was RIPPLE, a faster refresh with a depth rider. ANCHOR is the coverage branch's
                    // real question answered: keep the spread AND get the focused mark back on the one
                    // enemy that matters.
                    ("ANCHOR", "The front enemy takes the full 70% mark; the rest keep the spread.",
                     d => d with { Rules = d.Rule with { AmplifyFrontFull = 0.70f } }),
                    ("WINNOW", "The marks deepen +10% every 2s, up to +50%.",
                     d => d with { AmplifyDeepenPerTick = 0.10f, AmplifyDeepenCap = 0.50f })),
                V("ETCH", "The mark deepens +50% every 2s to +170%, and keeps its depth when it moves.",
                    Source.Spirit,
                    d => d with { AmplifyDeepenPerTick = 0.50f, AmplifyDeepenCap = 1.70f },
                    ("SINK",   "The mark deepens +80% each time instead of +50%.",
                     d => d with { AmplifyDeepenPerTick = 0.80f }),
                    ("GRAVEN", "The cap rises from +170% to +250%.",
                     d => d with { AmplifyDeepenCap = 2.50f }),
                    ("PACE",   "The mark deepens every 1s instead of every 2s.",
                     d => d with { IntervalMs = 1000 })),
            }),

        // ── VOLLEY — MANY HITS. TARGET ECONOMY. ─────────────────────────────────────────────────
        new("volley_spray", "SPRAY", Style.Volley, SkillKind.Active, SkillEffect.Damage,
            "Fires 5 arrows across the wave.",
            Beats: 4, IntervalMs: 0, On: ReactionOn.None, Targets: 5, BasePower: 215f,
            ClipKey: "projectile", FxKey: "projectile",
            Variations: new[]
            {
                V("SPLAY", "Fires an arrow at every enemy, and never fewer than 5 arrows.",
                    Source.Mind,
                    d => d with { Targets = WholeWave, MinimumHits = 5 },
                    // Was TWIN, doubling every arrow — a damage multiplier wearing a target-economy
                    // name. Two more arrows is the same idea at the branch's own scale.
                    ("EXTRA STRING", "Two more arrows, 7 instead of 5.",
                     d => d with { MinimumHits = 7 }),
                    ("NOCK",   "Every arrow deals 25% more.",
                     d => d with { DamageMultiplier = 1.25f }),
                    // Was FLIGHT (bleed) — CLUSTER's verb. Arrows that would be wasted on a corpse are
                    // SPLAY's own problem, and this is target economy answered as target economy.
                    // SPLAY reaches the WHOLE wave, one arrow each, so it has no spare arrow to
                    // re-aim and no aiming order that changes anything — both of those readings are
                    // inert on this branch by construction. A cleanup here is the thing the word
                    // means: the arrows finish what is already nearly finished, which is what turns a
                    // spread from chip damage into kills, and a kill is a mouth that stops biting.
                    ("CLEANUP", "Arrows hit an enemy under half health 60% harder.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.5f, BonusUnderHealthAmount = 0.60f } })),
                // BODY, not SHADOW: five hits driven into one body is concentrated physical force, and
                // SHADOW needs its actives on FINISH and VENGEANCE.
                V("CLUSTER", "All 5 arrows hit one enemy.",
                    Source.Body,
                    d => d with { Targets = 1, HitsPerTarget = 5 },
                    ("DRIVE",    "The cast deals 50% more damage.",
                     d => d with { DamageMultiplier = 1.5f }),
                    ("RUPTURE",  "Your casts leave bleed worth 30% of what they deal.",
                     d => d with { BleedFromHits = 0.30f }),
                    // Was GROUPING, "reaches a second enemy" — which is the one thing CLUSTER exists not
                    // to do. It keeps concentrating; it just stops wasting arrows on a corpse.
                    ("PUNCH THROUGH", "If the target dies, the remaining arrows drive into the next enemy.",
                     d => d with { Rules = d.Rule with { RetargetOnDeath = true } })),
            }),

        new("volley_weep", "WEEP", Style.Volley, SkillKind.Reaction, SkillEffect.Damage,
            "When an enemy dies it leaves bleed on the wave worth 30% of its health.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Kill, Targets: WholeWave,
            ClipKey: "projectile", FxKey: "weep",
            Variations: new[]
            {
                // NATURE, not MIND: bleed that grows and spreads is growth and propagation.
                V("TORRENT", "The bleed deals its damage twice as fast.",
                    Source.Nature,
                    d => d with { BleedRate = 2f },
                    ("DRY",      "A kill leaves 45% of the enemy's health as bleed instead of 30%.",
                     d => d with { BleedOnKillFraction = 0.45f }),
                    ("SPILLWAY", "The bleed pays out three times as fast instead of twice.",
                     d => d with { BleedRate = 3f }),
                    // Was EBB, a cast-bleed rider — CARRION's ONSET in another hat. FLOOD is the
                    // propagation the branch is named for, and it is bounded: a flat extra share of the
                    // ordinary kill contribution, never a share of the standing pool.
                    ("FLOOD",    "While the bleed is already running, each further kill adds half again.",
                     d => d with { Rules = d.Rule with { ExtraBleedWhileBleeding = 0.5f } })),
                V("CARRION", "The bleed lingers, paying out over more than twice as long.",
                    Source.Shadow,
                    d => d with { BleedCarriesWaves = true },
                    ("DREGS",     "A kill leaves 45% of the enemy's health as bleed instead of 30%.",
                     d => d with { BleedOnKillFraction = 0.45f }),
                    ("ONSET",     "Your casts leave bleed worth 10% of what they deal, and it carries too.",
                     d => d with { BleedFromHits = 0.10f }),
                    ("LAST DROP", "The carried bleed pays out 50% faster.",
                     d => d with { BleedRate = 1.5f })),
            },
            BleedOnKillFraction: 0.30f),

        // ── FIELD — THE WHOLE WAVE MATTERS. ─────────────────────────────────────────────────────
        new("field_pulse", "PULSE", Style.Field, SkillKind.Active, SkillEffect.Damage,
            "Area damage to every enemy in the wave.",
            Beats: 5, IntervalMs: 0, On: ReactionOn.None, Targets: WholeWave, BasePower: 140f,
            ClipKey: "aura", FxKey: "aura",
            Variations: new[]
            {
                V("THRONG", "Damage rises 20% for each living enemy.",
                    Source.Nature,
                    d => d with { DamagePerLivingEnemy = 0.20f },
                    ("HORDE",   "The per-enemy bonus rises from 20% to 35%.",
                     d => d with { DamagePerLivingEnemy = 0.35f }),
                    ("PACKED",  "The pulse deals 30% more damage.",
                     d => d with { DamageMultiplier = 1.3f }),
                    // Was an unconditional beat off the cooldown, which spends the beat budget the
                    // basic attack needs. CROWDED is the same cadence bought with FIELD's own condition.
                    ("CROWDED", "While 4 or more enemies live, the pulse comes a beat sooner.",
                     d => d with { CrowdedBeats = 4 })),
                V("SHARE", "300% damage, split evenly between every living enemy.",
                    Source.Spirit,
                    d => d with { SplitPool = 3.0f, SplitMaxWays = 99 },
                    ("POOL",     "The split pool rises from 300% to 450%.",
                     d => d with { SplitPool = 4.5f }),
                    ("NARROWED", "The pool is split two ways at most, however many enemies are alive.",
                     d => d with { SplitMaxWays = 2 }),
                    // Was RECLAIM, a flat beat off. BALANCE is redistribution — SPIRIT's own verb — and
                    // it answers the split pool's real waste: shares poured into an almost-dead enemy.
                    ("BALANCE",  "The pool favours the enemies with the most health left, wasting less.",
                     d => d with { Rules = d.Rule with { SplitByHealth = true } })),
            }),

        new("field_mire", "MIRE", Style.Field, SkillKind.Field, SkillEffect.Damage,
            "Damages every enemy every 1s, and slows their attacks by 25%.",
            Beats: 0, IntervalMs: 1000, On: ReactionOn.None, Targets: WholeWave, BasePower: 12f,
            ClipKey: "aura", FxKey: "aura",
            Variations: new[]
            {
                V("NUMB", "The slow deepens 5% each second, up to 40%.",
                    Source.Nature,
                    d => d with { SlowDeepenPerTick = 0.05f, SlowCeiling = 0.40f },
                    ("DEEPEN",   "The slow deepens 10% a second instead of 5%, and its ceiling rises to 50%.",
                     d => d with { SlowDeepenPerTick = 0.10f, SlowCeiling = 0.50f }),
                    ("SEDIMENT", "The ceiling rises from 40% to 55%.",
                     d => d with { SlowCeiling = 0.55f }),
                    ("SILT",     "The field's damage rises 50%.",
                     d => d with { DamageMultiplier = 1.5f })),
                V("TEEMING", "Slows 6% for each living enemy on top of the 25%, up to 60%.",
                    Source.Spirit,
                    d => d with { SlowPerEnemy = 0.06f, SlowCeiling = 0.60f },
                    ("CLOG",    "10% for each living enemy instead of 6%, and the ceiling rises to 78%.",
                     d => d with { SlowPerEnemy = 0.10f, SlowCeiling = 0.78f }),
                    ("BRIM",    "The slow starts at 40% instead of 25%, and its ceiling rises to 85%.",
                     d => d with { SlowFraction = 0.40f, SlowCeiling = 0.85f }),
                    // Was a faster clock. REMNANT is SPIRIT's own idea: the wave's presence leaves an
                    // echo, so the enemies it has already lost still weigh on it — for this wave only.
                    // Counting the dead toward the SLOW is arithmetically empty — the mire's grip is
                    // the deepest it has taken hold at, and living plus dead is the number the wave
                    // opened with, so the maximum is already holding that depth. The dead buy the
                    // other consequence of the same idea instead: what the mire has drowned is still
                    // in it, and the survivors wade through it.
                    ("REMNANT", "The wave bites 5% softer for each enemy it has lost, down to -40%.",
                     d => d with { Rules = d.Rule with { WeakenPerDeadEnemy = 0.05f, WeakenPerDeadCap = 0.40f } })),
            },
            SlowFraction: 0.25f),

        // ── DRAIN — COMBAT RETURNS SOMETHING TO YOU. ────────────────────────────────────────────
        new("drain_drink", "DRINK", Style.Drain, SkillKind.Active, SkillEffect.Heal,
            "Heavy damage to one target; heals you back a share of it.",
            Beats: 6, IntervalMs: 0, On: ReactionOn.None, Targets: 1, BasePower: 260f,
            Lifesteal: HealTuning.Default.TransformationLeech,
            ClipKey: "transformation", FxKey: "transformation",
            Variations: new[]
            {
                // Renamed from THIRST and moved to MACHINE: a pump that returns the same share every
                // time is a mechanism, not an appetite. Old saves are migrated by name.
                V("SIPHON", "Lifesteal doubles, and your per-wave healing limit doubles with it.",
                    Source.Machine,
                    d => d with { Lifesteal = HealTuning.Default.TransformationLeech * 2f },
                    ("PUMP",    "Lifesteal is 50% stronger.",
                     d => d with { Lifesteal = d.Lifesteal * 1.5f }),
                    ("PARCH",   "The cast deals 30% more damage, so it drinks more.",
                     d => d with { DamageMultiplier = 1.3f }),
                    ("TRICKLE", "Your basic attacks lifesteal 3% of their damage too.",
                     d => d with { SwingLifesteal = 0.03f })),
                V("GLUT", "No lifesteal. Damage rises with your current health, up to +150% at full.",
                    Source.Nature,
                    d => d with { Lifesteal = 0f, DamagePerHealth = 1.5f },
                    ("SURFEIT",    "The health scaling counts double.",
                     d => d with { DamagePerHealth = 3.0f }),
                    ("STOUT",      "The cast deals 30% more damage.",
                     d => d with { DamageMultiplier = 1.3f }),
                    // Was HIGH WATER, a cooldown cut. RIPE pays the branch for what it actually asks of
                    // the player: stay biologically healthy, and shield does not count.
                    ("RIPE",       "Above 90% health the scaling reaches half again as far.",
                     d => d with { Rules = d.Rule with { HealthScalingAbove = 0.90f, HealthScalingAboveBonus = 0.5f } })),
            }),

        new("drain_wilt", "WILT", Style.Drain, SkillKind.Field, SkillEffect.Heal,
            "Attack break: every enemy's damage drops 10% a pulse, down to -50%.",
            Beats: 0, IntervalMs: 1000, On: ReactionOn.None, Targets: WholeWave,
            ClipKey: "transformation", FxKey: "wilt",
            Variations: new[]
            {
                // BODY, not SHADOW: standing in the wave and outlasting it is physical endurance.
                V("SUP", "Each pulse also heals 1% of your maximum health.",
                    Source.Body,
                    d => d with { HealPerPulse = 0.01f },
                    ("BROOK",   "The attack break reaches -70% instead of -50%.",
                     d => d with { AttackBreakFloor = -0.70f }),
                    // Was BALM-as-double-tick-rate, which doubled the heal AND the break AND the damage
                    // in one purchase. It buys the heal it is named for.
                    // The wave's healing ceiling is raised alongside the pulse, because without the
                    // room the bigger heal is not a bigger heal: 1% a pulse already fills the budget,
                    // so 1.5% healed the same number and the card was a lie.
                    ("BALM",    "Each pulse heals 1.5% instead of 1%, and you can be healed 40% more each wave.",
                     d => d with { HealPerPulse = 0.015f, Rules = d.Rule with { HealCeilingBonus = 0.40f } }),
                    ("RESERVE", "The attack break deepens 15% a pulse instead of 10%.",
                     d => d with { AttackBreakPerTick = 0.15f })),
                // MIND, not NATURE: picking the one enemy that matters and crippling it precisely is
                // manipulation and selection. NATURE's passives are TORRENT and NUMB.
                V("SHRIVEL", "The front enemy only: 20% a pulse, down to -80%.",
                    Source.Mind,
                    d => d with { FrontEnemyOnly = true, AttackBreakPerTick = 0.20f, AttackBreakFloor = -0.80f },
                    ("HOLLOW", "The break also reaches the second enemy, at half depth.",
                     d => d with { BreakSecondEnemy = 0.5f }),
                    ("SEIZED", "The break deepens 30% a pulse instead of 20%.",
                     d => d with { AttackBreakPerTick = 0.30f }),
                    ("GAUNT",  "The floor falls from -80% to -95%.",
                     d => d with { AttackBreakFloor = -0.95f })),
            },
            AttackBreakPerTick: 0.10f, AttackBreakFloor: -0.50f),
    };

    public static SkillDef ById(string id)
        => All.FirstOrDefault(s => s.Id == id)
           ?? throw new ArgumentOutOfRangeException(nameof(id), id, "No such skill.");

    public static SkillDef? Find(string? id) => id is null ? null : All.FirstOrDefault(s => s.Id == id);

    /// <summary>A style's ACTIVE skill — the one that costs the champion an action.</summary>
    public static SkillDef ActiveOf(Style style) => All.First(s => s.Style == style && s.TakesABeat);

    /// <summary>A style's PASSIVE skill — the one that never costs an action.</summary>
    public static SkillDef PassiveOf(Style style) => All.First(s => s.Style == style && !s.TakesABeat);

    /// <summary>
    /// Cyclic distance on the six-style ring, 0..3 — the affinity hexagon, unchanged in shape from
    /// the Form era's and corrected in ORDER (see <see cref="Style"/>).
    /// </summary>
    public static int RingDistance(Style a, Style b)
    {
        var ring = Enum.GetValues<Style>().Length;
        var raw = Math.Abs((int)a - (int)b);
        return Math.Min(raw, ring - raw);
    }

    /// <summary>The style directly opposite this one — three steps around the ring.</summary>
    public static Style Opposite(Style style)
        => (Style)(((int)style + Enum.GetValues<Style>().Length / 2) % Enum.GetValues<Style>().Length);
}
