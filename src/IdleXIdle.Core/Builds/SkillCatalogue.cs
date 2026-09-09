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
/// <param name="SwingPower">HARD HANDS, OPEN HAND/BROAD KNUCKLE, OPEN HAND/SLOW HANDS — how much harder the champion's own basic attack lands while this skill is woven. Gathered once per wave from the weave and read on the swing, never on a cast.</param>
/// <param name="DefenceBreakOnKill">HARDFACE, PLANISH/COLD SET — defence stripped from EVERY enemy still standing each time one dies, from any cause. Clamped by the definition's own DefenceBreakFloor.</param>
/// <param name="DefenceBreakOnAnyHit">HARDFACE/PLANISH/PEENING — the cast strips the enemy in front even when it kills nothing. A flag rather than a second depth, so it is inert on a branch whose depth is zero.</param>
/// <param name="HealGrowthPerPulse">HOLD FAST/DEEP ROOTS/TAPROOT — how much more each of this Field's heal pulses returns than the one before it, for the rest of the wave.</param>
/// <param name="BossPower">PAYING WORK, STRIPPED BARE/CLEANED OUT — how much harder this skill lands on a BOSS wave. The only dial in the game that reads the wave's own boss flag.</param>
/// <param name="PowerPerBiteAnswered">NARROWS, CHOKE/DEEP THORN — how much larger this Reaction's answer is for each bite THIS SLOT has already answered this wave. Read before its own increment, so the first answer is the plain one.</param>
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
    float BonusUnderHealthAmount = 0f,
    float SwingPower = 0f,
    float DefenceBreakOnKill = 0f,
    bool DefenceBreakOnAnyHit = false,
    float HealGrowthPerPulse = 0f,
    float BossPower = 0f,
    float PowerPerBiteAnswered = 0f)
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
    float DamagePerDeadEnemy = 0f,       // GRAVE SONG — damage rises with the enemies the wave has LOST.
                                         //   The mirror of DamagePerLivingEnemy, and read one line below
                                         //   it, so a wave's living and dead can be sold in two directions.
    float ShieldPerPulse = 0f,           // HOLD FAST — a Field that grants SHIELD instead of dealing.
                                         //   The catalogue's only producer of standing shield; the fight's
                                         //   Field fork tests it first and spares every shipped Field.

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
    SkillRules? Rules = null,

    // ── WHO THIS SKILL BELONGS TO. Null for the twelve SHARED skills, which any champion may weave
    //    once mastery allows it. A character id for a SIGNATURE, which only that champion may weave,
    //    ever, by any route (BRIEF sec.2-14, LAWS 1-2).
    //
    //    A FIELD, not a naming rule and not a lookup table somewhere else. Ownership decides what the
    //    composer will carry into a fight, so it has to live where the composer already looks; a
    //    convention over the id string would be a rule with no compiler behind it, and a side table
    //    would be a second place to forget.
    //
    //    Last and optional, so not one of the twelve shared skills changed a character, and
    //    tools/check_skill_doc.py — whose regex pins the first line of a definition — never saw it.
    string? OwnerCharacterId = null)
{
    /// <summary>Does this skill cost the champion its action?</summary>
    public bool TakesABeat => Kind == SkillKind.Active;

    /// <summary>The rules this skill turns on — never null at the point of use.</summary>
    public SkillRules Rule => Rules ?? SkillRules.None;
}

/// <summary>
/// The twelve SHARED skills — one active and one passive per style — and the TEN SIGNATURES.
/// </summary>
/// <remarks>
/// <para>
/// Authored 2026-08-30 and gated against the two laws in
/// <c>design/gdd/skill-slots-and-skill-trees.md</c> §8 and the mechanic ownership table in §6. Six
/// entries were cut for cause during that gate; §6 records each and the rule it broke.
/// </para>
/// <para>
/// The ten signatures arrived with the systems refactor's phase 2. A signature carries an
/// <see cref="SkillDef.OwnerCharacterId"/> and only that champion may ever weave it; the shared twelve
/// leave it null. Use <see cref="Shared"/> wherever a statement is about the twelve's shape — a style
/// owning exactly one active, a Source fielding a whole loadout, a mastery road teaching every skill.
/// </para>
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
                     d => d with { WaveStartShieldFraction = 0.05f })),
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
                V("SPLAY", "An arrow at every enemy, and never fewer than 5 arrows in all.",
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
                // GIVING UP THE WAVE HAS TO BUY SOMETHING. SUP keeps WILT's base break — which reaches
                // every enemy — and heals on top; SHRIVEL trades that reach for depth on one. On any
                // band with a crowd the trade is plainly worse, so it has to be plainly better on the
                // band it is FOR, and it was not: measured against a single Bruiser, SUP's 1% a pulse
                // out-healed everything the deeper break saved. A cripple that loses to a bandage on
                // the one target it was built for is not a choice.
                V("SHRIVEL", "The front enemy only: 25% a pulse, down to -90%.",
                    Source.Mind,
                    d => d with { FrontEnemyOnly = true, AttackBreakPerTick = 0.25f, AttackBreakFloor = -0.90f },
                    ("HOLLOW", "The break also reaches the second enemy, at half depth.",
                     d => d with { BreakSecondEnemy = 0.5f }),
                    ("SEIZED", "The break deepens 35% a pulse instead of 25%.",
                     d => d with { AttackBreakPerTick = 0.35f }),
                    ("GAUNT",  "The floor falls from -90% to -97%.",
                     d => d with { AttackBreakFloor = -0.97f })),
            },
            AttackBreakPerTick: 0.10f, AttackBreakFloor: -0.50f),

        // ── THE TEN SIGNATURES — one per champion, and only that champion may ever weave it.
        //
        //    They are NOT a thirteenth through twenty-second entry in the twelve's shape. No mastery
        //    node teaches one (BuildComposer adds the active character's signature to the taught set
        //    outright), no style "owns" one, and the Source matrix is the shared twelve's — a signature
        //    is exclusive, so it can never be one of the four a Source fields a whole loadout from.
        //    Everything a signature turns is a dial the fight already reads, or one of the eight added
        //    for this set, each with exactly one consumer in SoloBattle.
        //
        //    In ROSTER order, so this list and CharacterRoster.All read side by side. ────────────────

        // THE SEEKER — the champion with no lean, whose own hands are the build.
        new("sig_seeker_hard_hands", "HARD HANDS", Style.Hammer, SkillKind.Active, SkillEffect.Damage,
            "Heavy damage to one target every 6 beats. While it is woven, every basic attack you make lands 70% harder.",
            Beats: 6, IntervalMs: 0, On: ReactionOn.None, Targets: 1, BasePower: 400f,
            ClipKey: "strike", FxKey: "strike",
            Rules: new SkillRules(SwingPower: 0.70f),
            OwnerCharacterId: "seeker",
            Variations: new[]
            {
                V("OPEN HAND", "The blow gives its weight to your hands: it deals a third as much, and your basic attacks land 210% harder.",
                    Source.Body,
                    d => d with { DamageMultiplier = 0.35f, Rules = d.Rule with { SwingPower = 2.10f } },
                    ("BROAD KNUCKLE", "Your basic attacks land 330% harder instead of 210%.",
                     d => d with { Rules = d.Rule with { SwingPower = d.Rule.SwingPower * 1.57f } }),
                    ("SPLIT GRAIN", "Your basic attacks ignore defence.",
                     d => d with { SwingIgnoresArmour = true }),
                    // Was THROUGH GRAIN, a carry on the swing's own overkill: bounding it to one level
                    // needed a re-entry flag threaded through the sim's hottest method for a single
                    // purchase. This is the branch's own trade said once more — give the cast away,
                    // take the beats back — on two dials that are already live.
                    ("SLOW HANDS", "The blow comes round every 8 beats instead of 6, and your basic attacks land 400% harder instead of 210%.",
                     d => d with { Beats = 8, Rules = d.Rule with { SwingPower = d.Rule.SwingPower * 1.90f } })),
                V("SHUT FIST", "Your hands gain nothing more. The blow lands twice on the target, and it hits an enemy under half health 85% harder.",
                    Source.Mind,
                    d => d with { HitsPerTarget = 2, DamageMultiplier = 0.80f,
                                  Rules = d.Rule with { SwingPower = 0f,
                                                        BonusUnderHealth = 0.50f, BonusUnderHealthAmount = 0.85f } },
                    ("THIRD FALL", "The blow lands three times instead of twice.",
                     d => d with { HitsPerTarget = 3 }),
                    ("CARRY ON", "If the target dies, the blows it had left drive into the next enemy.",
                     d => d with { Rules = d.Rule with { RetargetOnDeath = true } }),
                    // The AMOUNT only. Buying the threshold too would open the window on OPEN HAND,
                    // which sets none — the same split WIDE SWEEP and BARBED SWEEP use.
                    ("LAST INCH", "The finishing bonus is 140% instead of 85%.",
                     d => d with { Rules = d.Rule with { BonusUnderHealthAmount = 1.40f } })),
            }),

        // THE ANVIL — what goes down leaves the next one softer.
        new("sig_anvil_hardface", "HARDFACE", Style.Hammer, SkillKind.Active, SkillEffect.Damage,
            "Heavy damage to one target every 6 beats. While it is woven, every enemy that dies strips 12 defence from every enemy still standing, for the rest of the wave, down to -48.",
            Beats: 6, IntervalMs: 0, On: ReactionOn.None, Targets: 1, BasePower: 470f,
            ClipKey: "strike", FxKey: "strike",
            DefenceBreakFloor: -48f,
            Rules: new SkillRules(DefenceBreakOnKill: 12f),
            OwnerCharacterId: "anvil",
            Variations: new[]
            {
                V("PLANISH", "The strip is 24 defence instead of 12, and defence can be driven down to -90.",
                    Source.Machine,
                    d => d with { DefenceBreakFloor = -90f, Rules = d.Rule with { DefenceBreakOnKill = 24f } },
                    ("BEDPLATE", "Defence can be driven down to -150 instead of -90.",
                     d => d with { DefenceBreakFloor = -150f }),
                    ("COLD SET", "The strip is 40 defence instead of 24.",
                     d => d with { Rules = d.Rule with { DefenceBreakOnKill = 40f } }),
                    // A BOOL, not a second depth: a flag that enables a strip of zero subtracts zero,
                    // which is what keeps it inert on the branch that sold the strip.
                    ("PEENING", "The blow strips the enemy in front even when it does not kill.",
                     d => d with { Rules = d.Rule with { DefenceBreakOnAnyHit = true } })),
                V("UPSET", "No defence stripping. The weight falls on every enemy, twice, at 45% each.",
                    Source.Body,
                    d => d with { Targets = WholeWave, HitsPerTarget = 2, DamageMultiplier = 0.45f,
                                  Rules = d.Rule with { DefenceBreakOnKill = 0f } },
                    ("THIRD DROP", "The weight falls three times instead of twice.",
                     d => d with { HitsPerTarget = 3 }),
                    ("RUNOUT", "If an enemy dies, the falls it had left drive into the next enemy.",
                     d => d with { Rules = d.Rule with { RetargetOnDeath = true } }),
                    ("UNDERFOOT", "The falls hit an enemy under half health 75% harder.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.50f, BonusUnderHealthAmount = 0.75f } })),
            }),

        // THE CHORUS — the mirror of MANY MOUTHS, read off the wave's dead instead of its living.
        new("sig_chorus_grave_song", "GRAVE SONG", Style.Field, SkillKind.Field, SkillEffect.Damage,
            "Damages every enemy every 2s, and deals 35% more for each enemy the wave has already lost.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: WholeWave, BasePower: 16f,
            ClipKey: "aura", FxKey: "aura",
            DamagePerDeadEnemy: 0.35f,
            OwnerCharacterId: "chorus",
            Variations: new[]
            {
                V("REQUIEM", "The song comes every 4s instead of every 2s, at more than twice the weight, and each enemy the wave has lost is worth 70%.",
                    Source.Shadow,
                    d => d with { IntervalMs = 4000, DamageMultiplier = 2.2f, DamagePerDeadEnemy = 0.70f },
                    ("DIRGE", "Each enemy the wave has lost is worth 110% instead of 70%.",
                     d => d with { DamagePerDeadEnemy = 1.10f }),
                    ("TOLLING", "The song comes every 3s instead of every 4s.",
                     d => d with { IntervalMs = 3000 }),
                    ("OPEN GRAVE", "The song hits an enemy under 45% health 90% harder.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.45f, BonusUnderHealthAmount = 0.90f } })),
                // The cap is 25%, not 45%: the read takes the LARGER of the two softenings, so a cap
                // the wave cannot reach decides nothing. At 10% a corpse, 25% binds from the third
                // death — which a five-creature band reaches while it is still being bitten.
                V("CHANTRY", "The song stops counting the dead for damage and counts them against the wave: it deals its plain weight, and the wave bites 14% softer for every enemy it has lost, down to -25%.",
                    Source.Spirit,
                    d => d with { DamageMultiplier = 1.6f, DamagePerDeadEnemy = 0f,
                                  Rules = d.Rule with { WeakenPerDeadEnemy = 0.14f, WeakenPerDeadCap = 0.25f } },
                    ("HUSH", "The softening may reach -45% instead of -25%.",
                     d => d with { Rules = d.Rule with { WeakenPerDeadCap = 0.45f } }),
                    ("BLACK VEIL", "The wave bites 22% softer for each enemy it has lost instead of 14%.",
                     d => d with { Rules = d.Rule with { WeakenPerDeadEnemy = 0.22f } }),
                    ("SHROUD", "The song ignores defence.",
                     d => d with { DefenceIgnore = true })),
            }),

        // THE METRONOME — the one Active counted in SECONDS. Nothing in the game hurries its clock.
        new("sig_metronome_clockwork", "CLOCKWORK", Style.Volley, SkillKind.Active, SkillEffect.Damage,
            "Fires on a clock, not on a count: three shots across the wave every 7s. No skill rate bonus makes the clock come sooner, and it still costs the action it lands on.",
            Beats: 0, IntervalMs: 7000, On: ReactionOn.None, Targets: 3, BasePower: 330f,
            ClipKey: "projectile", FxKey: "projectile",
            OwnerCharacterId: "metronome",
            Variations: new[]
            {
                // THE FORK IS REACH AGAINST CONCENTRATION, and the weights are what make it one. The
                // first draft had HELD NOTE both widening (three targets to the whole wave) AND
                // multiplying by six, so it beat ROLL by four to seventeen times in every band and
                // there was no problem ROLL answered. Now ROLL leads where the wave is one or two
                // creatures and HELD NOTE leads where it is a crowd.
                V("ROLL", "Every shot lands twice on two enemies instead of once on three, each a sixth heavier, and the clock is half a second shorter: four strikes every 6.5s.",
                    Source.Machine,
                    d => d with { IntervalMs = 6500, Targets = 2, HitsPerTarget = 2, DamageMultiplier = 1.15f },
                    ("RIM SHOT", "Every enemy is struck three times instead of twice.",
                     d => d with { HitsPerTarget = 3 }),
                    ("OFF BEAT", "If an enemy dies, the shots it had left drive into the next enemy.",
                     d => d with { Rules = d.Rule with { RetargetOnDeath = true } }),
                    ("STEADY HAND", "The shots ignore defence.",
                     d => d with { DefenceIgnore = true })),
                V("HELD NOTE", "One arrival every 9s instead of three shots every 7s, and it falls on the whole wave.",
                    Source.Mind,
                    d => d with { IntervalMs = 9000, Targets = WholeWave },
                    ("WHOLE BAR", "The arrival comes every 7s instead of every 9s.",
                     d => d with { IntervalMs = 7000 }),
                    ("LATE BEAT", "The arrival hits an enemy under 40% health 110% harder.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.40f, BonusUnderHealthAmount = 1.10f } }),
                    ("SPARE SHOT", "Against a thin wave the arrival doubles up: never fewer than 3 shots in all.",
                     d => d with { MinimumHits = 3 })),
            }),

        // THE UNBROKEN — the catalogue's only producer of standing SHIELD.
        new("sig_unbroken_hold_fast", "HOLD FAST", Style.Snare, SkillKind.Field, SkillEffect.Heal,
            "Every 2s you gain shield worth 2.5% of your maximum health. It never takes your action.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
            ClipKey: "trap", FxKey: "trap",
            ShieldPerPulse: 0.025f,
            OwnerCharacterId: "unbroken",
            Variations: new[]
            {
                V("BREASTWORK", "Twice the wall, half as often: shield worth 5.5% of your maximum health every 4s.",
                    Source.Machine,
                    d => d with { ShieldPerPulse = 0.055f, IntervalMs = 4000 },
                    ("COURSED STONE", "Each pulse is worth half again as much: 8.5% of your maximum health instead of 5.5%.",
                     d => d with { ShieldPerPulse = d.ShieldPerPulse * 1.55f }),
                    ("FOOTINGS", "The wall is rebuilt every 2.5s instead of every 4s.",
                     d => d with { IntervalMs = 2500 }),
                    // Was GROUNDWORK, which edited the field arrears gate's `ms == 0` clause — a clause
                    // the tick loop can never satisfy, since it opens at TickMs. This buys the same
                    // sentence on a site that fires: the wave opens with the plate already on.
                    ("GROUNDWORK", "The wall is already standing when the wave opens: every wave begins with shield worth 5.5% of your maximum health.",
                     d => d with { WaveStartShieldFraction = 0.03f })),
                V("DEEP ROOTS", "Half the wall. Each pulse also heals 1% of your maximum health.",
                    Source.Spirit,
                    d => d with { ShieldPerPulse = 0.012f, HealPerPulse = 0.010f },
                    ("WELLSPRING", "Each pulse heals 1.8% of your maximum health instead of 1%.",
                     d => d with { HealPerPulse = d.HealPerPulse * 1.8f }),
                    ("TAPROOT", "Each pulse heals 30% more than the one before it, for the rest of the wave.",
                     d => d with { Rules = d.Rule with { HealGrowthPerPulse = 0.30f } }),
                    ("HEARTWOOD", "Each pulse is worth 3% of your maximum health instead of 1.2%.",
                     d => d with { ShieldPerPulse = 0.030f })),
            }),

        // THE FALLING TOWER — a HAMMER field that lands real hits, on its own clock, on one creature.
        new("sig_tower_slow_fall", "SLOW FALL", Style.Hammer, SkillKind.Field, SkillEffect.Damage,
            "A stone falls on the front enemy every 3s. It never takes your action.",
            Beats: 0, IntervalMs: 3000, On: ReactionOn.None, Targets: 1, BasePower: 42f,
            ClipKey: "strike", FxKey: "strike",
            OwnerCharacterId: "tower",
            Variations: new[]
            {
                // THE FINISHING WINDOW IS THE VARIATION'S, and its two riders buy the two halves of it
                // — the split the shipped WIDE SWEEP / BARBED SWEEP pair uses. Written the other way
                // round, with one rider opening the window and the other widening it, the widener
                // alone multiplied an amount of zero and was a level that did nothing at all.
                V("COURSES", "The stones fall every 1.5s, each half as heavy, they ignore defence, and each stone hits an enemy under 45% health 55% harder.",
                    Source.Body,
                    d => d with { IntervalMs = 1500, DefenceIgnore = true,
                                  Rules = d.Rule with { BonusUnderHealth = 0.45f, BonusUnderHealthAmount = 0.55f } },
                    ("DRYSTONE", "The stones fall every 1s instead of every 1.5s.",
                     d => d with { IntervalMs = 1000 }),
                    ("PLUMBLINE", "The finishing bonus is 110% instead of 55%.",
                     d => d with { Rules = d.Rule with { BonusUnderHealthAmount = 1.10f } }),
                    ("HAIRLINE", "The finishing bonus reaches an enemy under 70% health instead of 45%.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.70f } })),
                V("ONE STONE", "One stone every 6s, on the two enemies in front, and it lands at 2.4 times the weight.",
                    Source.Nature,
                    d => d with { IntervalMs = 6000, TargetsBonus = 1, DamageMultiplier = 2.4f },
                    ("CAPSTONE", "The stone falls on the three enemies in front.",
                     d => d with { TargetsBonus = 2 }),
                    ("FULL COURSE", "The stone lands at 3.6 times the weight instead of 2.4.",
                     d => d with { DamageMultiplier = 3.6f }),
                    ("BEDDING IN", "The stone falls every 4s instead of every 6s.",
                     d => d with { IntervalMs = 4000 })),
            }),

        // THE QUIVER — the shot that never had a cooldown to spend. Revives ReactionOn.Kill.
        new("sig_quiver_backdraw", "BACKDRAW", Style.Volley, SkillKind.Reaction, SkillEffect.Damage,
            "When an enemy dies, two arrows fly at whatever is still standing. Rearms every 2.5s.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Kill, Targets: 2, BasePower: 175f, RearmMs: 2500,
            ClipKey: "projectile", FxKey: "projectile",
            OwnerCharacterId: "quiver",
            Variations: new[]
            {
                V("CLEAN SWEEP", "The volley reaches every living enemy instead of two, at two thirds the weight, and every arrow hits an enemy under half health 85% harder.",
                    Source.Mind,
                    d => d with { Targets = WholeWave, DamageMultiplier = 0.65f,
                                  Rules = d.Rule with { BonusUnderHealth = 0.50f, BonusUnderHealthAmount = 0.85f } },
                    ("WIDE SWEEP", "The finishing bonus reaches an enemy under three-quarters health.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.75f } }),
                    ("BARBED SWEEP", "The finishing bonus is 160% instead of 85%.",
                     d => d with { Rules = d.Rule with { BonusUnderHealthAmount = 1.60f } }),
                    ("SECOND STRING", "Every enemy is struck twice, and each strike is lighter.",
                     d => d with { HitsPerTarget = 2, DamageMultiplier = 0.42f })),
                V("ONE SHAFT", "One arrow instead of two, at six and a half times the weight, and it comes back only every 12s.",
                    Source.Shadow,
                    d => d with { Targets = 1, RearmMs = 12_000, DamageMultiplier = 6.5f },
                    ("HEAVY SHAFT", "The arrow lands at ten times the weight instead of six and a half.",
                     d => d with { DamageMultiplier = 10.0f }),
                    ("SECOND SHAFT", "The arrow comes back every 7s instead of every 12s.",
                     d => d with { RearmMs = 7_000 }),
                    ("BROADHEAD", "Your casts leave bleed worth 25% of what they deal.",
                     d => d with { BleedFromHits = 0.25f })),
            }),

        // THE THORNWALL — the only Reaction whose answer is its OWN number, growing with the count.
        new("sig_thornwall_narrows", "NARROWS", Style.Snare, SkillKind.Reaction, SkillEffect.Damage,
            "Every bite is answered by a fixed hit on the enemy in front, and the answer grows 22% for every bite this wall has already answered this wave. Rearms every 2.5s.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Bitten, Targets: 1, BasePower: 170f, RearmMs: 2500,
            ClipKey: "trap", FxKey: "trap",
            Rules: new SkillRules(PowerPerBiteAnswered: 0.22f),
            OwnerCharacterId: "thornwall",
            Variations: new[]
            {
                // The same split as COURSES, for the same reason.
                V("BRAMBLE", "The answer reaches every enemy instead of one, stops growing, and hits an enemy under half health 80% harder.",
                    Source.Nature,
                    d => d with { Targets = WholeWave, BasePower = 110f,
                                  Rules = d.Rule with { PowerPerBiteAnswered = 0f,
                                                        BonusUnderHealth = 0.50f, BonusUnderHealthAmount = 0.80f } },
                    ("UNDERGROWTH", "The finishing bonus reaches an enemy under three-quarters health.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.75f } }),
                    ("BLACK THORN", "The finishing bonus is 150% instead of 80%.",
                     d => d with { Rules = d.Rule with { BonusUnderHealthAmount = 1.50f } }),
                    ("BRIAR", "The answer is worth 170 instead of 110.",
                     d => d with { BasePower = 170f })),
                V("CHOKE", "The answer stays on the enemy in front, the wall answers every 2s, and the answer grows 34% for every bite it has already answered.",
                    Source.Machine,
                    d => d with { RearmMs = 2_000, Rules = d.Rule with { PowerPerBiteAnswered = 0.34f } },
                    ("DEEP THORN", "The answer grows half again as fast: 55% for every bite instead of 34%.",
                     d => d with { Rules = d.Rule with { PowerPerBiteAnswered = d.Rule.PowerPerBiteAnswered * 1.6f } }),
                    ("SNAPBACK", "The wall rearms 30% sooner still.",
                     d => d with { CooldownMultiplier = 0.70f }),
                    ("SECOND STAKE", "The answer reaches the two enemies in front.",
                     d => d with { Targets = 2 })),
            }),

        // THE OATHBOUND — the only amplifier whose price is being hit.
        new("sig_oathbound_oathmark", "OATHMARK", Style.Sign, SkillKind.Reaction, SkillEffect.Amplify,
            "Every bite you take opens the mark: all your damage +90% for 3s. Rearms every 4s.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Bitten, Targets: WholeWave, RearmMs: 4_000,
            ClipKey: "mark", FxKey: "mark",
            AmplifyPercent: 0.90f, AmplifyMs: 3_000, AmplifyWholeWave: true,
            OwnerCharacterId: "oathbound",
            Variations: new[]
            {
                // The depth is carried by AmplifyFrontFull rather than by AmplifyPercent, which is what
                // makes every depth purchase on this branch write a dial the sibling leaves at zero.
                // AmplifyPercent stays above zero because the window gate reads it.
                V("SEALED WORD", "The mark falls on the enemy in front only, and it is far deeper: +260% while it holds, for 1.8s.",
                    Source.Spirit,
                    d => d with { AmplifyWholeWave = false, AmplifyMs = 1_800,
                                  Rules = d.Rule with { AmplifyFrontFull = 2.60f } },
                    ("DEEP SEAL", "The mark is half again as deep: +390% instead of +260%.",
                     d => d with { Rules = d.Rule with { AmplifyFrontFull = d.Rule.AmplifyFrontFull * 1.5f } }),
                    ("LONG SEAL", "The mark holds 3s instead of 1.8s.",
                     d => d with { AmplifyMs = 3_000 }),
                    ("SHORTER OATH", "The mark reopens every 2.5s instead of every 4s.",
                     d => d with { RearmMs = 2_500 })),
                V("OPEN WORD", "The mark covers the whole wave but burns fast: +140% for 2s, and it reopens every 2.5s.",
                    Source.Mind,
                    d => d with { AmplifyPercent = 1.40f, AmplifyMs = 2_000, RearmMs = 2_500 },
                    ("WIDER WORD", "The mark is +230% instead of +140%.",
                     d => d with { AmplifyPercent = 2.30f }),
                    ("SAID AGAIN", "Each time the mark reopens it is deeper: +45% more each time, up to +180%.",
                     d => d with { AmplifyDeepenPerTick = 0.45f, AmplifyDeepenCap = 1.80f }),
                    ("FIRST WORD", "The enemy in front takes a +220% mark while the rest of the wave keeps the spread.",
                     d => d with { Rules = d.Rule with { AmplifyFrontFull = 2.20f } })),
            }),

        // THE MAGPIE — the only champion with no combat innate at all, aimed at the one wave it wants.
        new("sig_magpie_paying_work", "PAYING WORK", Style.Drain, SkillKind.Active, SkillEffect.Heal,
            "Heavy damage to one target every 5 beats, and you take 12% of what it deals back as health. Against a boss it lands 160% harder.",
            Beats: 5, IntervalMs: 0, On: ReactionOn.None, Targets: 1, BasePower: 250f,
            ClipKey: "transformation", FxKey: "transformation",
            Lifesteal: 0.12f,
            Rules: new SkillRules(BossPower: 1.60f),
            OwnerCharacterId: "magpie",
            Variations: new[]
            {
                V("STRIPPED BARE", "It takes nothing back, and against a boss it lands 340% harder instead of 160%.",
                    Source.Shadow,
                    d => d with { Lifesteal = 0f, Rules = d.Rule with { BossPower = 3.40f } },
                    ("CLEANED OUT", "Against a boss it lands 520% harder instead of 340%.",
                     d => d with { Rules = d.Rule with { BossPower = 5.20f } }),
                    ("PRISED OPEN", "The blow ignores defence.",
                     d => d with { DefenceIgnore = true }),
                    ("LONG JOB", "It comes every 6 beats instead of 5, and lands half again as hard.",
                     d => d with { Beats = 6, DamageMultiplier = 1.45f })),
                V("LIGHT FINGERS", "No boss bonus. It strikes every enemy in the wave for a third of the weight, and each strike hits an enemy under half health 80% harder.",
                    Source.Nature,
                    d => d with { Targets = WholeWave, DamageMultiplier = 0.35f,
                                  Rules = d.Rule with { BossPower = 0f,
                                                        BonusUnderHealth = 0.50f, BonusUnderHealthAmount = 0.80f } },
                    ("MANY POCKETS", "The finishing bonus reaches an enemy under 72% health instead of half.",
                     d => d with { Rules = d.Rule with { BonusUnderHealth = 0.72f } }),
                    ("SECOND HELPING", "Every enemy is struck twice, and each strike is lighter.",
                     d => d with { HitsPerTarget = 2, DamageMultiplier = 0.22f }),
                    ("FULL HANDS", "You take 38% of what it deals back instead of 12%.",
                     d => d with { Lifesteal = 0.38f })),
            }),
    };

    public static SkillDef ById(string id)
        => All.FirstOrDefault(s => s.Id == id)
           ?? throw new ArgumentOutOfRangeException(nameof(id), id, "No such skill.");

    public static SkillDef? Find(string? id) => id is null ? null : All.FirstOrDefault(s => s.Id == id);

    /// <summary>The twelve SHARED skills — everything any champion may learn on the mastery tree.</summary>
    /// <remarks>
    /// The ten signatures are excluded on purpose. "A style's active" is a statement about the shared
    /// catalogue's shape (one active and one passive per style); a signature belongs to a champion, not
    /// to a style's pair, and three styles now carry two of them.
    /// </remarks>
    public static IEnumerable<SkillDef> Shared => All.Where(s => s.OwnerCharacterId is null);

    /// <summary>A style's ACTIVE shared skill — the one that costs the champion an action.</summary>
    public static SkillDef ActiveOf(Style style) => Shared.First(s => s.Style == style && s.TakesABeat);

    /// <summary>A style's PASSIVE shared skill — the one that never costs an action.</summary>
    public static SkillDef PassiveOf(Style style) => Shared.First(s => s.Style == style && !s.TakesABeat);

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
