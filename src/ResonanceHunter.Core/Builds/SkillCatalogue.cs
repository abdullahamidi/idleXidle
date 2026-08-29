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
/// contradiction in it — Strike and Trap sat opposite each other on the hexagon (x0.45 against one
/// another) while <see cref="MasteryCatalog"/> put both of them in the Weight branch, so the tree
/// said they deepen together and the ring said they fight. The three oppositions below are semantic
/// and owe the mastery tree nothing, which matters because §9 moves the specialisations off the
/// branches entirely:
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

    /// <summary>You never close, and you touch everything.</summary>
    Field,

    /// <summary>You take part of the damage you deal back as health.</summary>
    Drain,
}

/// <summary>WHEN a skill acts. The three branches <see cref="SoloBattle"/> already has.</summary>
/// <remarks>
/// The fight loop branches three ways today — <c>IsPassive(form)</c> to the Aura block,
/// <c>FiresOnBeingHit(form)</c> to the Trap block, everything else to the cast block. This enum is
/// that same fork asked as a question about the SKILL instead of about the Form, which is why the
/// sim's shape does not change when it moves over.
///
/// <b>Only <see cref="Active"/> costs a beat.</b> A Field ticks on its own clock above the beat gate
/// and a Reaction answers an event, so neither can ever take the champion's action. That is the whole
/// reason two passive slots are free, and it is a structural guarantee rather than a discipline.
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

/// <summary>
/// One face of a skill — what it becomes when its tree's ring 0 is chosen.
/// </summary>
/// <remarks>
/// Ring 0 of every skill tree is a two-way fork: a move that takes the champion's turn, or a state
/// that never does. Both faces are the SAME skill, the same style and the same art; they disagree
/// only about when they act. Modelling that as two faces on one definition — rather than as two
/// catalogue entries — is what keeps the catalogue six rows long instead of twelve.
/// </remarks>
/// <param name="Name">Player-facing name of this face, e.g. BLOW or PRESS.</param>
/// <param name="Kind">When it acts. Only <see cref="SkillKind.Active"/> costs a beat.</param>
/// <param name="Effect">What it does.</param>
/// <param name="Beats">
/// For an Active face: beats between casts. Zero for a passive face. Counted in BEATS rather than
/// milliseconds so a fast build casts sooner in seconds but not in ACTIONS — the rule the beat model
/// was built on.
/// </param>
/// <param name="IntervalMs">
/// For a Field face: milliseconds between ticks. Must be a multiple of
/// <c>ExpeditionTuning.TickMs</c> (100) or the modulo the tick loop is gated on never lands.
/// </param>
/// <param name="On">For a Reaction face: which event fires it.</param>
/// <param name="Targets">How many creatures one activation reaches. <see cref="int.MaxValue"/> is the wave.</param>
public sealed record SkillFace(
    string Name,
    SkillKind Kind,
    SkillEffect Effect,
    int Beats,
    int IntervalMs,
    ReactionOn On,
    int Targets)
{
    /// <summary>Does this face cost the champion its action?</summary>
    public bool TakesABeat => Kind == SkillKind.Active;
}

/// <summary>
/// One skill: a style's expression, with the two faces its tree's ring 0 chooses between.
/// </summary>
/// <param name="Id">Stable identifier. Persisted; never renamed once a save has seen it.</param>
/// <param name="Name">Player-facing name of the skill itself.</param>
/// <param name="Style">The one style this skill belongs to — the ONLY field mastery and affinity read.</param>
/// <param name="LegacyForm">
/// The <see cref="Form"/> this skill replaces. A migration bridge and nothing more: it is what turns a
/// saved build's Form ordinal into a SkillId, and it is what lets the sim run on either model while
/// the rework lands stage by stage.
/// </param>
/// <param name="Active">The face taken when ring 0 chooses the move.</param>
/// <param name="Passive">The face taken when ring 0 chooses the state.</param>
/// <param name="ClipKey">Character clip this skill plays. Resolved through <c>Character.StripKeys</c>.</param>
/// <param name="FxKey">Effect strip this skill plays.</param>
public sealed record SkillDef(
    string Id,
    string Name,
    Style Style,
    Form LegacyForm,
    SkillFace Active,
    SkillFace Passive,
    string ClipKey,
    string FxKey)
{
    /// <summary>The face for a slot of this kind.</summary>
    public SkillFace Face(bool passive) => passive ? Passive : Active;
}

/// <summary>
/// The six skills, one per style.
/// </summary>
/// <remarks>
/// <para>
/// <b>Six rows, not sixty.</b> An earlier draft of this design proposed twelve fixed skills — an
/// active and a passive per style — and it was rejected: they were derived backwards from what the
/// sim could already do cheaply, which produces relabelled code paths rather than abilities.
/// Everything that would have been a separate skill is a NODE in that skill's tree instead.
/// </para>
/// <para>
/// <b>Nothing reads this yet.</b> Stage 1 of the rework is deliberately additive — the catalogue and
/// its shape land first, with <see cref="FormBehaviour"/> still driving the fight, so the whole
/// existing suite stays green while the data layer is proven. <c>SoloBattle</c> switches over in
/// stage 3. See <c>design/gdd/skill-slots-and-skill-trees.md</c> §11 for the staging.
/// </para>
/// </remarks>
public static class SkillCatalogue
{
    /// <summary>How many creatures a face that reaches the whole wave reports.</summary>
    public const int WholeWave = int.MaxValue;

    public static IReadOnlyList<SkillDef> All { get; } = new List<SkillDef>
    {
        // HAMMER — the heavy blow. PRESS is a weight that sits on the enemy: it plays no character
        // clip, which is Law 1 (a passive never touches the champion's animation channel). An earlier
        // passive, "every 4th basic attack becomes a hammer hit", was cut for exactly that reason —
        // transforming the swing is an animation change, and the whole rework exists so the swing plays.
        new(
            Id: "hammer", Name: "HAMMER", Style: Style.Hammer, LegacyForm: Form.Strike,
            Active:  new("BLOW",  SkillKind.Active,   SkillEffect.Damage, Beats: 6, IntervalMs: 0,    On: ReactionOn.None, Targets: 1),
            Passive: new("PRESS", SkillKind.Field,    SkillEffect.Damage, Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1),
            ClipKey: "strike", FxKey: "strike"),

        // SNARE — being attacked pays. JAWS is today's Trap and is the one style whose baseline was
        // already reactive; SNAP is the active face, a blow sized by the champion's own health pool.
        new(
            Id: "snare", Name: "SNARE", Style: Style.Snare, LegacyForm: Form.Trap,
            Active:  new("SNAP", SkillKind.Active,   SkillEffect.Damage, Beats: 5, IntervalMs: 0, On: ReactionOn.None,   Targets: 1),
            Passive: new("JAWS", SkillKind.Reaction, SkillEffect.Damage, Beats: 0, IntervalMs: 0, On: ReactionOn.Bitten, Targets: 1),
            ClipKey: "trap", FxKey: "trap"),

        // SIGN — deals nothing, multiplies everything else. Amplify is this style's alone (§6).
        new(
            Id: "sign", Name: "SIGN", Style: Style.Sign, LegacyForm: Form.Mark,
            Active:  new("CALL",  SkillKind.Active, SkillEffect.Amplify, Beats: 5, IntervalMs: 0,    On: ReactionOn.None, Targets: 1),
            Passive: new("BRAND", SkillKind.Field,  SkillEffect.Amplify, Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1),
            ClipKey: "mark", FxKey: "mark"),

        // VOLLEY — volume over weight. SPRAY fires five arrows split among the enemies; RAIN is an
        // arrow rain on its own clock. Both were written by the designer himself as the register the
        // rest of the catalogue had to match.
        new(
            Id: "volley", Name: "VOLLEY", Style: Style.Volley, LegacyForm: Form.Projectile,
            Active:  new("SPRAY", SkillKind.Active, SkillEffect.Damage, Beats: 4, IntervalMs: 0,    On: ReactionOn.None, Targets: 5),
            Passive: new("RAIN",  SkillKind.Field,  SkillEffect.Damage, Beats: 0, IntervalMs: 3000, On: ReactionOn.None, Targets: WholeWave),
            ClipKey: "projectile", FxKey: "projectile"),

        // FIELD — always on, touches everything. GLOW is today's Aura, and the held aura the renderer
        // already draws is the visual template every Field face follows.
        new(
            Id: "field", Name: "FIELD", Style: Style.Field, LegacyForm: Form.Aura,
            Active:  new("PULSE", SkillKind.Active, SkillEffect.Damage, Beats: 5, IntervalMs: 0,    On: ReactionOn.None, Targets: WholeWave),
            Passive: new("GLOW",  SkillKind.Field,  SkillEffect.Damage, Beats: 0, IntervalMs: 1000, On: ReactionOn.None, Targets: WholeWave),
            ClipKey: "aura", FxKey: "aura"),

        // DRAIN — takes back what it deals. The one style BLOOD MAGIC switches off entirely, which is
        // what makes that keystone a real refusal.
        new(
            Id: "drain", Name: "DRAIN", Style: Style.Drain, LegacyForm: Form.Transformation,
            Active:  new("DRINK", SkillKind.Active, SkillEffect.Heal, Beats: 5, IntervalMs: 0,    On: ReactionOn.None, Targets: 1),
            Passive: new("SEEP",  SkillKind.Field,  SkillEffect.Heal, Beats: 0, IntervalMs: 2000, On: ReactionOn.None, Targets: 1),
            ClipKey: "transformation", FxKey: "transformation"),
    };

    public static SkillDef ById(string id)
        => All.FirstOrDefault(s => s.Id == id)
           ?? throw new ArgumentOutOfRangeException(nameof(id), id, "No such skill.");

    public static SkillDef? Find(string? id) => id is null ? null : All.FirstOrDefault(s => s.Id == id);

    public static SkillDef Of(Style style) => All.First(s => s.Style == style);

    /// <summary>The migration bridge: which skill replaces a saved <see cref="Form"/>.</summary>
    public static SkillDef ForLegacy(Form form) => All.First(s => s.LegacyForm == form);

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
