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
public sealed record Reinforcement(string Name, string Line);

/// <summary>
/// One of the two ways a skill can be taken. Changes what the skill DOES, not how much of it.
/// </summary>
public sealed record SkillVariation(string Name, string Line, IReadOnlyList<Reinforcement> Reinforcements);

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
    float PaysBackDamageTaken = 0f)      // REPAY: multiple of the damage taken since its last cast
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

    private static SkillVariation V(string name, string line, params (string N, string L)[] rs)
        => new(name, line, rs.Select(r => new Reinforcement(r.N, r.L)).ToList());

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
                    ("TOLL",  "The defence the blow ignores is added to its damage."),
                    ("SHEAR", "The blow is 50% larger against a target whose defence it ignored."),
                    ("TRAIL", "The defence ignore also applies to your basic attack for 3s.")),
                V("FINISH", "Execute threshold: the blow kills a target under 15% health. Once per wave.",
                    ("BRINK", "The execute threshold rises to 25% health."),
                    ("TWICE", "The blow may execute twice each wave."),
                    ("SPUR",  "The blow deals 20% more for each enemy it has executed this wave.")),
            },
            LegacyForm: Form.Strike),

        new("hammer_press", "PRESS", Style.Hammer, SkillKind.Field, SkillEffect.Damage,
            "A weight sits on the front enemy: its defence drops 5 every 2s, down to -25.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
            ClipKey: "strike", FxKey: "strike",
            Variations: new[]
            {
                V("CRUSHING", "The defence drop is 10 every 2s instead of 5, down to -50.",
                    ("SETTLE",    "The floor falls from -50 to -90."),
                    ("SEIZE",     "When the front enemy dies, its broken defence carries to the next."),
                    ("UNDERMINE", "The weight breaks defence on the two front enemies instead of one.")),
                V("PIN", "1s stun on the front enemy every 6s.",
                    ("HOLD",   "The stun is 1.5s instead of 1s."),
                    ("BUCKLE", "Each stun strips 10 defence from the front enemy."),
                    ("SEAL",   "While an enemy is stunned the weight's defence drop comes every 1s.")),
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
                    ("GRUDGE",  "The total is not cleared when it pays; it halves instead."),
                    ("SCARRED", "Damage taken below half health counts double."),
                    ("BRUISED", "Damage the trap already reflected still counts toward the total.")),
                V("BANKED", "Instead of dealing it, the total becomes a shield of equal size.",
                    ("STANDING", "The shield does not expire; it holds until it is spent."),
                    ("CARRIED",  "Any shield left when a wave ends carries into the next."),
                    ("LINING",   "The shield is 50% larger.")),
            },
            LegacyForm: null, PaysBackDamageTaken: 2.0f),

        new("snare_jaws", "JAWS", Style.Snare, SkillKind.Reaction, SkillEffect.Damage,
            "Every bite returns 50% of it to the enemy that bit you. Rearms every 3s.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Bitten, Targets: 1,
            ClipKey: "trap", FxKey: "trap",
            Variations: new[]
            {
                V("NET", "The reflect returns 100% of the bite instead of 50%.",
                    ("MESH",   "The reflect grows 10% per bite taken this wave, up to +50%."),
                    ("RECOIL", "The reflect still lands at half strength while the trap rearms."),
                    ("SPITE",  "A reflect that kills the enemy that bit you rearms the trap at once.")),
                V("IRON", "No reflect: the trap stops a whole bite, but rearms every 6s.",
                    ("REPRISAL", "A stopped bite is returned to the enemy that made it, in full."),
                    ("BLUNT",    "The trap springs on two bites before it rearms."),
                    ("HARDEN",   "Each spring raises the reflect 20% for the wave, up to 40%.")),
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
                    ("OVERSPEND", "Amplify +100% more; the window falls to 1.5s."),
                    ("HERALD",    "The window opens on its own when a wave starts."),
                    ("AFTERGLOW", "When the window closes, +40% holds until the next cast.")),
                V("STEADY", "Each cast adds +40% amplify for the rest of the wave, up to +80%.",
                    ("REDOUBLE", "Each cast adds +70% instead of +40%."),
                    ("PILLAR",   "The cap rises from +80% to +160%."),
                    ("FOOTING",  "Half the amplify carries into the next wave.")),
            },
            LegacyForm: Form.Mark),

        new("sign_brand", "BRAND", Style.Sign, SkillKind.Field, SkillEffect.Amplify,
            "Your damage to the front enemy is +70%.",
            Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1,
            ClipKey: "mark", FxKey: "mark",
            Variations: new[]
            {
                V("SPRAWL", "The mark covers every enemy instead, at half strength.",
                    ("EVEN",   "Every enemy's mark rises from half to three-quarters strength."),
                    ("WINNOW", "A marked enemy's death deepens every other mark +10%, up to +50%."),
                    ("RIPPLE", "Each death deepens the other marks +20% instead of +10%.")),
                V("ETCH", "The mark deepens +50% every 2s to +170%, and keeps its depth when it moves.",
                    ("SINK",   "The mark deepens +80% each time instead of +50%."),
                    ("GRAVEN", "The cap rises from +170% to +250%."),
                    ("PACE",   "The mark deepens every 1s instead of every 2s.")),
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
                    ("TWIN",   "Every enemy takes 2 arrows instead of 1."),
                    ("NOCK",   "Cooldown falls 0.3s for each enemy beyond the first that it hits."),
                    ("FLIGHT", "Every enemy the cast hits bleeds for 20% of the arrow.")),
                V("CLUSTER", "All 5 arrows hit one enemy.",
                    ("DRIVE",    "Each arrow after the first into the same enemy deals +15%."),
                    ("RUPTURE",  "Each arrow after the first into the same enemy leaves bleed worth 20% of it."),
                    ("GROUPING", "When the target dies, the rest of the cast's arrows fire at the next enemy.")),
            },
            LegacyForm: Form.Projectile),

        new("volley_weep", "WEEP", Style.Volley, SkillKind.Reaction, SkillEffect.Damage,
            "When an enemy dies it leaves bleed on the wave worth 30% of its health.",
            Beats: 0, IntervalMs: 0, On: ReactionOn.Kill, Targets: WholeWave,
            ClipKey: "projectile", FxKey: "projectile",
            Variations: new[]
            {
                V("TORRENT", "The bleed deals its damage twice as fast.",
                    ("DRY",      "A kill made while nothing is bleeding leaves double bleed."),
                    ("SPILLWAY", "When the bleed kills, its next payment lands at once."),
                    ("EBB",      "The bleed's last payment is doubled.")),
                V("CARRION", "The bleed carries into the next wave instead of ending.",
                    ("DREGS",     "Bleed carried into a new wave pays double for its first 3s."),
                    ("ONSET",     "Bleed carried into a wave hits every enemy with its first payment."),
                    ("LAST DROP", "The kill that clears a wave doubles the standing bleed.")),
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
                    ("HORDE",   "The per-enemy bonus rises from 20% to 35%."),
                    ("PACKED",  "Above two living enemies the per-enemy bonus is doubled."),
                    ("CROWDED", "The bonus counts the wave's starting enemies, not the living ones.")),
                V("SHARE", "300% damage, split evenly between every living enemy.",
                    ("POOL",     "The split pool rises from 300% to 450%."),
                    ("NARROWED", "The pool is split four ways at most, however many enemies are alive."),
                    ("RECLAIM",  "An enemy killed by the pulse returns its share to the others.")),
            },
            LegacyForm: null),

        new("field_mire", "MIRE", Style.Field, SkillKind.Field, SkillEffect.Damage,
            "Damages every enemy every 1s, and slows their attacks by 25%.",
            Beats: 0, IntervalMs: 1000, On: ReactionOn.None, Targets: WholeWave,
            ClipKey: "aura", FxKey: "aura",
            Variations: new[]
            {
                V("NUMB", "The slow deepens 5% each second, up to 40%.",
                    ("DEEPEN",   "The slow deepens 8% a second instead of 5%."),
                    ("SEDIMENT", "The ceiling rises from 40% to 55%."),
                    ("SILT",     "At the ceiling the field also deals area damage every 1s.")),
                V("TEEMING", "Slows 6% for each living enemy on top of the 25%, up to 60%.",
                    ("CLOG",    "8% for each living enemy instead of 6%."),
                    ("BRIM",    "The per-enemy ceiling rises from 60% to 75%."),
                    ("REMNANT", "Enemies killed this wave still count toward the slow for 3s.")),
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
                    ("GREEDY",  "Lifesteal is 50% stronger against the enemy with the most health."),
                    ("PARCH",   "Lifesteal doubles again while below 50% health."),
                    ("TRICKLE", "Your basic attacks lifesteal too, at a quarter strength.")),
                V("GLUT", "No lifesteal. Damage rises with your current health, up to +150% at full.",
                    ("SURFEIT",    "The health scaling counts double."),
                    ("STOUT",      "Missing health costs you only half as much of the scaling."),
                    ("HIGH WATER", "The scaling reads the health you started the wave with.")),
            },
            LegacyForm: Form.Transformation),

        new("drain_wilt", "WILT", Style.Drain, SkillKind.Field, SkillEffect.Heal,
            "Attack break: every enemy's damage drops 10% a pulse, down to -50%.",
            Beats: 0, IntervalMs: 1000, On: ReactionOn.None, Targets: WholeWave,
            ClipKey: "transformation", FxKey: "transformation",
            Variations: new[]
            {
                V("SUP", "Each pulse also heals 1% of your maximum health.",
                    ("BROOK",   "The pulse heals twice as much while below 50% health."),
                    ("BALM",    "The healing pulse runs every 0.5s; the break keeps its 1s clock."),
                    ("RESERVE", "The pulse heals 1% more for every 10% the break has deepened.")),
                V("SHRIVEL", "The front enemy only: 20% a pulse, down to -80%.",
                    ("HOLLOW", "The break also reaches the second enemy, at half depth."),
                    ("SEIZED", "The front enemy starts each wave already fully broken."),
                    ("GAUNT",  "When the front enemy dies, its break carries to the next one.")),
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
