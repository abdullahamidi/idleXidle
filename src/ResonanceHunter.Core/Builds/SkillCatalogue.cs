using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;

namespace ResonanceHunter.Core.Builds;

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
    Func<SkillDef, SkillDef>? Modify = null);

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
/// <param name="LegacyForm">
/// The <see cref="Form"/> a saved build maps onto this skill through, or null for a skill no old save
/// can hold. A migration bridge and nothing more.
/// </param>
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
    Form? LegacyForm,
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

    // ── WHAT THE VARIATIONS TURN. Every one is zero or false at the base line, so a skill carries
    //    only the dials its own sentence needs and a variation is a DELTA on this record rather than
    //    a branch in the fight loop. Twenty-four variations would otherwise be twenty-four cases. ──
    bool DefenceIgnore = false,          // HAMMER/FLATTEN
    float ExecuteFraction = 0f,          // HAMMER/FINISH — kills a target under this share of health
    int StunMs = 0,                      // HAMMER/PIN
    bool ShieldInsteadOfDamage = false,  // SNARE/BANKED — the banked total becomes a shield
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

    // ── WHAT THE REINFORCEMENTS TURN. Ten, deliberately: a dial per reinforcement would be seventy
    //    of them, and most of what a reinforcement wants to say is "the same thing, more of it" —
    //    which the variation's own dials already express. These are the few shapes that recur. ────
    float DamageMultiplier = 1f,         // "hits harder" — also scales what BANKED turns into a shield
    float CooldownMultiplier = 1f,       // "comes back sooner", or pays for a bigger number
    int TargetsBonus = 0,                // "reaches one more"
    int ExecutesPerWave = 1,             // HAMMER/TWICE
    bool SwingIgnoresArmour = false,     // HAMMER/TRAIL — the plain attack borrows the blow's rule
    float SwingLifesteal = 0f,           // DRAIN/TRICKLE — and it borrows the drink's
    float ReflectGrowthPerBite = 0f,     // SNARE/MESH and HARDEN
    float ReflectGrowthCap = 0f,
    float BleedFromHits = 0f,            // VOLLEY/FLIGHT, RUPTURE, ONSET — the cast itself bleeds
    float BreakSecondEnemy = 0f)         // DRAIN/HOLLOW — SHRIVEL reaches past the front one
{
    /// <summary>Does this skill cost the champion its action?</summary>
    public bool TakesABeat => Kind == SkillKind.Active;
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

    private static SkillVariation V(string name, string line, Func<SkillDef, SkillDef> modify,
                                    params (string N, string L, Func<SkillDef, SkillDef> M)[] rs)
        => new(name, line, rs.Select(r => new Reinforcement(r.N, r.L, r.M)).ToList(), modify);

    public static IReadOnlyList<SkillDef> All { get; } = new List<SkillDef>
    {
        // ── HAMMER — defence break, defence ignore, stun, execute threshold ───────────────────────
        new("hammer_blow", "BLOW", Style.Hammer, SkillKind.Active, SkillEffect.Damage,
            "Heavy damage to one target.",
            Beats: 6, IntervalMs: 0, On: ReactionOn.None, Targets: 1,
            ClipKey: "strike", FxKey: "strike",
            Variations: new[]
            {
                V("FLATTEN", "Defence ignore: the blow ignores the target's defence.",
                    d => d with { DefenceIgnore = true },
                    ("TOLL",  "The blow deals 50% more damage.",
                     d => d with { DamageMultiplier = 1.5f }),
                    ("SHEAR", "The blow reaches a second target.",
                     d => d with { TargetsBonus = 1 }),
                    ("TRAIL", "Your basic attacks ignore defence too.",
                     d => d with { SwingIgnoresArmour = true })),
                V("FINISH", "Execute threshold: the blow kills a target under 15% health. Once per wave.",
                    d => d with { ExecuteFraction = 0.15f },
                    ("BRINK", "The execute threshold rises to 25% health.",
                     d => d with { ExecuteFraction = 0.25f }),
                    ("TWICE", "The blow may execute twice each wave, and the threshold rises to 20% health.",
                     d => d with { ExecutesPerWave = 2, ExecuteFraction = 0.20f }),
                    ("SPUR",  "Cooldown falls from 6 beats to 5, so it finds more executes.",
                     d => d with { Beats = 5 })),
            },
            LegacyForm: Form.Strike),

        new("hammer_press", "PRESS", Style.Hammer, SkillKind.Field, SkillEffect.Damage,
            "A weight sits on the front enemy: its defence drops 5 every 2s, down to -25.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
            ClipKey: "strike", FxKey: "press",
            Variations: new[]
            {
                V("CRUSHING", "The defence drop is 10 every 2s instead of 5, down to -50.",
                    d => d with { DefenceBreakPerTick = 10f, DefenceBreakFloor = -50f },
                    ("SETTLE",    "The floor falls from -50 to -90.",
                     d => d with { DefenceBreakFloor = -90f }),
                    ("SEIZE",     "The weight breaks the two front enemies instead of one.",
                     d => d with { TargetsBonus = 1 }),
                    ("UNDERMINE", "The weight works every 1s instead of every 2s.",
                     d => d with { IntervalMs = 1000 })),
                V("PIN", "1s stun on the front enemy every 6s.",
                    d => d with { DefenceBreakPerTick = 0f, StunMs = 1000, IntervalMs = 6000 },
                    ("HOLD",   "The stun is 1.5s instead of 1s.",
                     d => d with { StunMs = 1500 }),
                    ("BUCKLE", "Each stun strips 10 defence from the front enemy.",
                     d => d with { DefenceBreakPerTick = 10f, DefenceBreakFloor = -50f }),
                    ("SEAL",   "The stun comes every 4s instead of every 6s.",
                     d => d with { IntervalMs = 4000 })),
            },
            LegacyForm: null, DefenceBreakPerTick: 5f, DefenceBreakFloor: -25f),

        // ── SNARE — reflect, shield, damage taken. TAUNT was removed from this style's list: every
        //    creature already attacks the champion, so it named nothing, and what its nodes actually
        //    did was put a third writer on the enemy bite clock beside HAMMER's stun and FIELD's slow.
        new("snare_repay", "REPAY", Style.Snare, SkillKind.Active, SkillEffect.Damage,
            "Deals 200% of the damage you have taken since its last cast.",
            Beats: 5, IntervalMs: 0, On: ReactionOn.None, Targets: 1,
            ClipKey: "trap", FxKey: "trap",
            Variations: new[]
            {
                V("VENGEANCE", "350% instead of 200%, but only damage taken in the last 3s counts.",
                    d => d with { PaysBackDamageTaken = 3.5f },
                    ("GRUDGE",  "It pays back 500% instead of 350%.",
                     d => d with { PaysBackDamageTaken = 5.0f }),
                    ("SCARRED", "The payback deals 40% more damage.",
                     d => d with { DamageMultiplier = 1.4f }),
                    ("BRUISED", "Cooldown falls from 5 beats to 4, so less damage goes uncollected.",
                     d => d with { Beats = 4 })),
                V("BANKED", "Instead of dealing it, the total becomes a shield of equal size.",
                    d => d with { ShieldInsteadOfDamage = true },
                    ("STANDING", "The shield is 50% larger.",
                     d => d with { DamageMultiplier = 1.5f }),
                    ("CARRIED",  "Cooldown falls from 5 beats to 4, so the shield renews sooner.",
                     d => d with { Beats = 4 }),
                    ("LINING",   "It banks 300% of the damage taken instead of 200%.",
                     d => d with { PaysBackDamageTaken = 3.0f })),
            },
            LegacyForm: null, PaysBackDamageTaken: 2.0f),

        new("snare_jaws", "JAWS", Style.Snare, SkillKind.Reaction, SkillEffect.Damage,
            "Every bite returns 50% of it to the enemy that bit you. Rearms every 3s.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Bitten, Targets: 1,
            ClipKey: "trap", FxKey: "trap",
            Variations: new[]
            {
                V("NET", "The reflect returns 100% of the bite instead of 50%.",
                    d => d with { ReflectFraction = 1.0f },
                    ("MESH",   "The reflect grows 10% per bite taken this wave, up to +50%.",
                     d => d with { ReflectGrowthPerBite = 0.10f, ReflectGrowthCap = 0.50f }),
                    ("RECOIL", "The trap rearms a third sooner.",
                     d => d with { CooldownMultiplier = 0.66f }),
                    ("SPITE",  "The reflect returns 140% of the bite instead of 100%.",
                     d => d with { ReflectFraction = 1.4f })),
                V("IRON", "No reflect: the trap stops a whole bite, but rearms every 6s.",
                    d => d with { ReflectFraction = 0f, StopsWholeBite = true },
                    ("REPRISAL", "A stopped bite is returned to the enemy that made it, in full.",
                     d => d with { ReflectFraction = 1.0f }),
                    ("BLUNT",    "The trap rearms twice as fast.",
                     d => d with { CooldownMultiplier = 0.5f }),
                    ("HARDEN",   "Each spring raises the reflect 20% for the wave, up to 40%.",
                     d => d with { ReflectGrowthPerBite = 0.20f, ReflectGrowthCap = 0.40f })),
            },
            LegacyForm: Form.Trap),

        // ── SIGN — amplify only. It deals no damage itself. ───────────────────────────────────────
        new("sign_call", "CALL", Style.Sign, SkillKind.Active, SkillEffect.Amplify,
            "All your damage +60% for 6s.",
            Beats: 5, IntervalMs: 0, On: ReactionOn.None, Targets: 1,
            ClipKey: "mark", FxKey: "mark",
            Variations: new[]
            {
                V("SPEND", "The window is 2s and amplifies +200%.",
                    d => d with { AmplifyPercent = 2.0f, AmplifyMs = 2000 },
                    ("OVERSPEND", "Amplify +100% more; the window falls to 1.5s.",
                     d => d with { AmplifyPercent = 3.0f, AmplifyMs = 1500 }),
                    ("HERALD",    "Cooldown falls from 5 beats to 4, so the window opens more often.",
                     d => d with { Beats = 4 }),
                    ("AFTERGLOW", "The window holds 4s instead of 2s.",
                     d => d with { AmplifyMs = 4000 })),
                V("STEADY", "Each cast adds +40% amplify for the rest of the wave, up to +80%.",
                    d => d with { AmplifyPercent = 0f, AmplifyPerCast = 0.40f, AmplifyCap = 0.80f },
                    ("REDOUBLE", "Each cast adds +70% instead of +40%.",
                     d => d with { AmplifyPerCast = 0.70f }),
                    ("PILLAR",   "The cap rises from +80% to +160%.",
                     d => d with { AmplifyCap = 1.60f }),
                    ("FOOTING",  "Cooldown falls from 5 beats to 4, so it reaches the cap sooner.",
                     d => d with { Beats = 4 })),
            },
            LegacyForm: Form.Mark),

        new("sign_brand", "BRAND", Style.Sign, SkillKind.Field, SkillEffect.Amplify,
            "Your damage to the front enemy is +70%.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
            ClipKey: "mark", FxKey: "mark",
            Variations: new[]
            {
                V("SPRAWL", "The mark covers every enemy instead, at half strength.",
                    d => d with { AmplifyWholeWave = true, AmplifyPercent = 0.35f },
                    ("EVEN",   "Every enemy's mark rises from half to three-quarters strength.",
                     d => d with { AmplifyPercent = 0.525f }),
                    ("WINNOW", "The marks deepen +10% every 2s, up to +50%.",
                     d => d with { AmplifyDeepenPerTick = 0.10f, AmplifyDeepenCap = 0.50f }),
                    ("RIPPLE", "The marks refresh every 1s instead of every 2s.",
                     d => d with { IntervalMs = 1000 })),
                V("ETCH", "The mark deepens +50% every 2s to +170%, and keeps its depth when it moves.",
                    d => d with { AmplifyDeepenPerTick = 0.50f, AmplifyDeepenCap = 1.70f },
                    ("SINK",   "The mark deepens +80% each time instead of +50%.",
                     d => d with { AmplifyDeepenPerTick = 0.80f }),
                    ("GRAVEN", "The cap rises from +170% to +250%.",
                     d => d with { AmplifyDeepenCap = 2.50f }),
                    ("PACE",   "The mark deepens every 1s instead of every 2s.",
                     d => d with { IntervalMs = 1000 })),
            },
            LegacyForm: null),

        // ── VOLLEY — hit count, bleed, cooldown reduction, spread on kill ─────────────────────────
        new("volley_spray", "SPRAY", Style.Volley, SkillKind.Active, SkillEffect.Damage,
            "Fires 5 arrows at random enemies. With fewer enemies they are split between them.",
            Beats: 4, IntervalMs: 0, On: ReactionOn.None, Targets: 5,
            ClipKey: "projectile", FxKey: "projectile",
            Variations: new[]
            {
                V("SPLAY", "Fires an arrow at every enemy, and never fewer than 5 arrows.",
                    d => d with { Targets = WholeWave, MinimumHits = 5 },
                    ("TWIN",   "Every enemy takes 2 arrows instead of 1.",
                     d => d with { MinimumHits = 10 }),
                    ("NOCK",   "Every arrow deals 25% more.",
                     d => d with { DamageMultiplier = 1.25f }),
                    ("FLIGHT", "Your casts leave bleed worth 20% of what they deal.",
                     d => d with { BleedFromHits = 0.20f })),
                V("CLUSTER", "All 5 arrows hit one enemy.",
                    d => d with { Targets = 1 },
                    ("DRIVE",    "The cast deals 50% more damage.",
                     d => d with { DamageMultiplier = 1.5f }),
                    ("RUPTURE",  "Your casts leave bleed worth 30% of what they deal.",
                     d => d with { BleedFromHits = 0.30f }),
                    ("GROUPING", "The cast reaches a second enemy.",
                     d => d with { TargetsBonus = 1 })),
            },
            LegacyForm: Form.Projectile),

        new("volley_weep", "WEEP", Style.Volley, SkillKind.Reaction, SkillEffect.Damage,
            "When an enemy dies it leaves bleed on the wave worth 30% of its health.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Kill, Targets: WholeWave,
            ClipKey: "projectile", FxKey: "weep",
            Variations: new[]
            {
                V("TORRENT", "The bleed deals its damage twice as fast.",
                    d => d with { BleedRate = 2f },
                    ("DRY",      "A kill leaves 45% of the enemy's health as bleed instead of 30%.",
                     d => d with { BleedOnKillFraction = 0.45f }),
                    ("SPILLWAY", "The bleed pays out three times as fast instead of twice.",
                     d => d with { BleedRate = 3f }),
                    ("EBB",      "Your casts leave bleed worth 10% of what they deal.",
                     d => d with { BleedFromHits = 0.10f })),
                V("CARRION", "The bleed lingers, paying out over more than twice as long.",
                    d => d with { BleedCarriesWaves = true },
                    ("DREGS",     "A kill leaves 45% of the enemy's health as bleed instead of 30%.",
                     d => d with { BleedOnKillFraction = 0.45f }),
                    ("ONSET",     "Your casts leave bleed worth 10% of what they deal, and it carries too.",
                     d => d with { BleedFromHits = 0.10f }),
                    ("LAST DROP", "The carried bleed pays out 50% faster.",
                     d => d with { BleedRate = 1.5f })),
            },
            LegacyForm: null, BleedOnKillFraction: 0.30f),

        // ── FIELD — slow, area damage, scaling with the number of living enemies ──────────────────
        new("field_pulse", "PULSE", Style.Field, SkillKind.Active, SkillEffect.Damage,
            "Area damage to every enemy in the wave.",
            Beats: 5, IntervalMs: 0, On: ReactionOn.None, Targets: WholeWave,
            ClipKey: "aura", FxKey: "aura",
            Variations: new[]
            {
                V("THRONG", "Damage rises 20% for each living enemy.",
                    d => d with { DamagePerLivingEnemy = 0.20f },
                    ("HORDE",   "The per-enemy bonus rises from 20% to 35%.",
                     d => d with { DamagePerLivingEnemy = 0.35f }),
                    ("PACKED",  "The pulse deals 30% more damage.",
                     d => d with { DamageMultiplier = 1.3f }),
                    ("CROWDED", "Cooldown falls from 5 beats to 4.",
                     d => d with { Beats = 4 })),
                V("SHARE", "300% damage, split evenly between every living enemy.",
                    d => d with { SplitPool = 3.0f, SplitMaxWays = 99 },
                    ("POOL",     "The split pool rises from 300% to 450%.",
                     d => d with { SplitPool = 4.5f }),
                    ("NARROWED", "The pool is split four ways at most, however many enemies are alive.",
                     d => d with { SplitMaxWays = 4 }),
                    ("RECLAIM",  "The pulse comes back a beat sooner, 4 instead of 5.",
                     d => d with { Beats = 4 })),
            },
            LegacyForm: null),

        new("field_mire", "MIRE", Style.Field, SkillKind.Field, SkillEffect.Damage,
            "Damages every enemy every 1s, and slows their attacks by 25%.",
            Beats: 0, IntervalMs: 1000, On: ReactionOn.None, Targets: WholeWave,
            ClipKey: "aura", FxKey: "aura",
            Variations: new[]
            {
                V("NUMB", "The slow deepens 5% each second, up to 40%.",
                    d => d with { SlowDeepenPerTick = 0.05f, SlowCeiling = 0.40f },
                    ("DEEPEN",   "The slow deepens 10% a second instead of 5%, and its ceiling rises to 50%.",
                     d => d with { SlowDeepenPerTick = 0.10f, SlowCeiling = 0.50f }),
                    ("SEDIMENT", "The ceiling rises from 40% to 55%.",
                     d => d with { SlowCeiling = 0.55f }),
                    ("SILT",     "The field's damage rises 50%.",
                     d => d with { DamageMultiplier = 1.5f })),
                V("TEEMING", "Slows 6% for each living enemy on top of the 25%, up to 60%.",
                    d => d with { SlowPerEnemy = 0.06f, SlowCeiling = 0.60f },
                    ("CLOG",    "10% for each living enemy instead of 6%, and the ceiling rises to 78%.",
                     d => d with { SlowPerEnemy = 0.10f, SlowCeiling = 0.78f }),
                    ("BRIM",    "The slow starts at 40% instead of 25%, and its ceiling rises to 85%.",
                     d => d with { SlowFraction = 0.40f, SlowCeiling = 0.85f }),
                    ("REMNANT", "The field acts every 0.5s instead of every 1s.",
                     d => d with { IntervalMs = 500 })),
            },
            LegacyForm: Form.Aura, SlowFraction: 0.25f),

        // ── DRAIN — lifesteal, healing, attack break, scaling with health ─────────────────────────
        new("drain_drink", "DRINK", Style.Drain, SkillKind.Active, SkillEffect.Heal,
            "Heavy damage to one target; heals you for 50% of it.",
            Beats: 6, IntervalMs: 0, On: ReactionOn.None, Targets: 1,
            ClipKey: "transformation", FxKey: "transformation",
            Variations: new[]
            {
                V("THIRST", "Lifesteal doubles, and your per-wave healing limit doubles with it.",
                    d => d with { Lifesteal = HealTuning.Default.TransformationLeech },
                    ("GREEDY",  "Lifesteal is 50% stronger.",
                     d => d with { Lifesteal = d.Lifesteal * 1.5f }),
                    ("PARCH",   "The cast deals 30% more damage, so it drinks more.",
                     d => d with { DamageMultiplier = 1.3f }),
                    ("TRICKLE", "Your basic attacks lifesteal 3% of their damage too.",
                     d => d with { SwingLifesteal = 0.03f })),
                V("GLUT", "No lifesteal. Damage rises with your current health, up to +150% at full.",
                    d => d with { Lifesteal = 0f, DamagePerHealth = 1.5f },
                    ("SURFEIT",    "The health scaling counts double.",
                     d => d with { DamagePerHealth = 3.0f }),
                    ("STOUT",      "The cast deals 30% more damage.",
                     d => d with { DamageMultiplier = 1.3f }),
                    ("HIGH WATER", "Cooldown falls from 6 beats to 5.",
                     d => d with { Beats = 5 })),
            },
            LegacyForm: Form.Transformation),

        new("drain_wilt", "WILT", Style.Drain, SkillKind.Field, SkillEffect.Heal,
            "Attack break: every enemy's damage drops 10% a pulse, down to -50%.",
            Beats: 0, IntervalMs: 1000, On: ReactionOn.None, Targets: WholeWave,
            ClipKey: "transformation", FxKey: "wilt",
            Variations: new[]
            {
                V("SUP", "Each pulse also heals 1% of your maximum health.",
                    d => d with { HealPerPulse = 0.01f },
                    ("BROOK",   "The attack break reaches -70% instead of -50%.",
                     d => d with { AttackBreakFloor = -0.70f }),
                    ("BALM",    "The pulse runs every 0.5s instead of every 1s.",
                     d => d with { IntervalMs = 500 }),
                    ("RESERVE", "The attack break deepens 15% a pulse instead of 10%.",
                     d => d with { AttackBreakPerTick = 0.15f })),
                V("SHRIVEL", "The front enemy only: 20% a pulse, down to -80%.",
                    d => d with { FrontEnemyOnly = true, AttackBreakPerTick = 0.20f, AttackBreakFloor = -0.80f },
                    ("HOLLOW", "The break also reaches the second enemy, at half depth.",
                     d => d with { BreakSecondEnemy = 0.5f }),
                    ("SEIZED", "The break deepens 30% a pulse instead of 20%.",
                     d => d with { AttackBreakPerTick = 0.30f }),
                    ("GAUNT",  "The floor falls from -80% to -95%.",
                     d => d with { AttackBreakFloor = -0.95f })),
            },
            LegacyForm: null, AttackBreakPerTick: 0.10f, AttackBreakFloor: -0.50f),
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
    /// The skill a saved <see cref="Form"/> becomes, in the slot it lands in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The migration bridge. A saved build holds a Form, and every one of them must resolve or the
    /// player's build is silently unwoven on load.
    /// </para>
    /// <para>
    /// <b>A spill changes which SKILL is woven, not a flag on one.</b> Under the two-faces design an
    /// overflowing active took the passive face of the same skill; with distinct skills it becomes its
    /// style's passive skill instead — the same style and the same art, a different ability. AURA and
    /// TRAP were never actives, so they resolve to their styles' passives whichever slot is asked for.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Does this skill have to be TAUGHT by a mastery node before it can be woven?
    /// </summary>
    /// <remarks>
    /// Exactly the six with no <see cref="SkillDef.LegacyForm"/>. The other six are reachable by
    /// picking a Source and a Form on the weave screen, which is how a champion fights on its first
    /// wave with nothing learned; these six have no such door and the tree is it (design §5).
    /// </remarks>
    public static bool NeedsUnlock(SkillDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        return def.LegacyForm is null;
    }

    /// <summary>The six that a style road has to teach, in ring order.</summary>
    public static IReadOnlyList<SkillDef> Taught { get; } = All.Where(NeedsUnlock).ToList();

    public static SkillDef Resolve(Form form, bool passive)
    {
        // THE STYLE COMES FROM THE FORM; THE SLOT PICKS WHICH OF ITS TWO SKILLS THIS IS. It used to
        // return the Form's native skill whenever an active was asked for, which meant AURA and TRAP
        // resolved passive from either side — and FIELD's PULSE and SNARE's REPAY had no door at all.
        // Two of the twelve were unreachable, which is a catalogue entry nothing can select: the same
        // dead weight as a field nothing reads.
        var style = All.First(s => s.LegacyForm == form).Style;
        return passive ? PassiveOf(style) : ActiveOf(style);
    }

    /// <summary>
    /// Cyclic distance on the six-style ring, 0..3 — the affinity hexagon, unchanged in shape from
    /// <see cref="FormBehaviour"/>'s and corrected in ORDER (see <see cref="Style"/>).
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
