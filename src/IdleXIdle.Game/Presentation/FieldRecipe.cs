using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A PERSISTENT FIELD IS PRESENTED (ADR-011, the FIELD / AURA archetype): a skill that is always on and acts on its
/// own clock. PRESS is the reference (the owner's brief, 2026-09-28): "A persistent pressure field around the Seeker
/// periodically sends a force pulse outward. When the pulse reaches the front enemy, that enemy is visibly crushed /
/// compressed for a moment." One visual sentence: the Seeker's field pulses -> a wave goes out -> that enemy is crushed.
/// </summary>
/// <remarks>
/// <para>
/// FIVE STATES, all timed from the fight's own tick (u = playhead - the Aura event's ms, so the wave ARRIVES on the tick:
/// ADR-011's contact on the beat): A the quiet field (a soft pressure haze leaning toward the enemy side, never a ring
/// buff); B the COMPRESSION before the tick (the field draws in and gathers, <see cref="ContractFromMs"/> to
/// <see cref="LaunchMs"/>); C the EMISSION (the field springs back out and one broad pressure FRONT leaves the Seeker's
/// enemy side and travels to the front enemy, growing as it goes: Syndra E's motion grammar, a force wave, never a
/// bullet); D the CRUSH on the tick (the front resolves into two pressing arcs above and below the target and the
/// creature's body buckles for a moment; the brightest moment of the phrase); E the SETTLE (the arcs release and fade,
/// the field is quiet again).
/// </para>
/// <para>
/// THREE PARTS, composed at runtime (tools/asset-pipeline/v2/seeker_press.py): the field haze, the pressure front
/// (crisp and softened: the softened cell trails it as its afterimage) and the clamp arc. All white or grey and tinted
/// here along the Seeker's Shadow palette (lavender, violet, a pale hot edge), after the material discipline of JAWS.
/// </para>
/// <para>
/// The TARGET is the fight's: the creature the tick's Break event names (the front enemy, as the base PRESS affects one),
/// else the first creature still standing. Presentation only: Core decides the tick and what it does.
/// </para>
/// </remarks>
public sealed class FieldRecipe
{
    /// <summary>A name for traces and tests.</summary>
    public string Id { get; init; } = "";

    // ── THE PARTS (tools/asset-pipeline/v2/seeker_press.py; keypose_sources/seeker_press_spans.json pins the cells) ──

    /// <summary>The quiet field around the Seeker: a pressure haze leaning toward the enemy side.</summary>
    public string FieldKey { get; init; } = "fxp_seeker_press_field";

    /// <inheritdoc cref="FieldKey"/>
    public Point FieldCell { get; init; } = new(320, 448);

    /// <summary>The pressure FRONT, facing the enemy side: two cells side by side, crisp and softened.</summary>
    public string WaveKey { get; init; } = "fxp_seeker_press_wave";

    /// <inheritdoc cref="WaveKey"/>
    public Point WaveCell { get; init; } = new(192, 512);

    /// <summary>The leading rim's x in a wave cell (at its middle): the point that travels to the target's front.</summary>
    public float WaveLeadX { get; init; } = 153.6f;

    /// <summary>The CRUSH arc, pressing down (drawn flipped below the target, pressing up).</summary>
    public string ClampKey { get; init; } = "fxp_seeker_press_clamp";

    /// <inheritdoc cref="ClampKey"/>
    public Point ClampCell { get; init; } = new(512, 160);

    /// <summary>The pressing edge's y in the clamp cell (at its middle).</summary>
    public float ClampPressY { get; init; } = 113.2f;

    // ── A · THE QUIET FIELD ──

    /// <summary>The field's height as a share of the Seeker's visible height (it stands him up, head to feet).</summary>
    public float FieldHeightShare { get; init; } = 1.0f;

    /// <summary>
    /// Where the field is centred on his visible body (shares of its width and height): FORWARD of his middle, so its
    /// leaning edge stands just in front of his enemy-facing side, where it can be seen. Centred on him, drawn behind
    /// him, the rest state hid behind his body and cloak (the first review: "the field is active" did not come across).
    /// </summary>
    public Vector2 FieldCentre { get; init; } = new(0.62f, 0.55f);

    /// <summary>The field at rest: quiet, never competing with a blow (the brief: "should not dominate the screen"), but present.</summary>
    public float RestAlpha { get; init; } = 0.26f;

    /// <summary>The field's edge glowing faintly in the light pass, a share of its opacity (it reads on dark stone).</summary>
    public float FieldGlowShare { get; init; } = 0.3f;

    /// <summary>The front leaves from the field's leading edge: this share of the field's drawn width forward of its centre.</summary>
    public float FieldEdgeShare { get; init; } = 0.38f;

    /// <summary>A slow breath on the rest level (the wall clock, so it breathes between waves too).</summary>
    public float BreathAlpha { get; init; } = 0.04f;

    /// <inheritdoc cref="BreathAlpha"/>
    public float BreathHz { get; init; } = 0.45f;

    // ── B · THE COMPRESSION (ms before the tick) ──

    /// <summary>The field starts to draw in here, gathering (a small inward contraction and a little more density).</summary>
    public float ContractFromMs { get; init; } = -520f;

    /// <summary>The field's size and opacity at its tightest, just before it lets the wave go.</summary>
    public float ContractScale { get; init; } = 0.86f;

    /// <inheritdoc cref="ContractScale"/>
    public float ContractAlpha { get; init; } = 0.5f;

    // ── C · THE EMISSION ──

    /// <summary>The front leaves the Seeker here (ms before the tick) and ARRIVES on the tick.</summary>
    public float LaunchMs { get; init; } = -300f;

    /// <summary>The field springs back out past its size as it lets go (then settles to 1 by <see cref="SpringMs"/>).</summary>
    public float SpringScale { get; init; } = 1.06f;

    /// <inheritdoc cref="SpringScale"/>
    public float SpringMs { get; init; } = 160f;

    /// <summary>
    /// The front's height: as it leaves, a share of the SEEKER's visible height (a broad arc from his chest, never a
    /// bullet); on arrival, a share of the TARGET's drawn silhouette (it spans the creature, top to feet and past them).
    /// </summary>
    public float WaveLaunchShare { get; init; } = 0.5f;

    /// <inheritdoc cref="WaveLaunchShare"/>
    public float WaveArriveShare { get; init; } = 1.7f;

    /// <summary>The front's opacity as it leaves and as it arrives (it gathers weight on the way).</summary>
    public float WaveLaunchAlpha { get; init; } = 0.6f;

    /// <inheritdoc cref="WaveLaunchAlpha"/>
    public float WaveArriveAlpha { get; init; } = 0.9f;

    /// <summary>The travel's ease-out: fast from the Seeker, still moving as it lands (a force, not a thrown thing).</summary>
    public float TravelEasePower { get; init; } = 1.35f;

    /// <summary>The softened afterimage: how far behind the front it trails (ms of travel) and how strong it is.</summary>
    public float AfterimageLagMs { get; init; } = 34f;

    /// <inheritdoc cref="AfterimageLagMs"/>
    public float AfterimageAlpha { get; init; } = 0.45f;

    /// <summary>The front collapses into the crush over this long after arriving.</summary>
    public float WaveCollapseMs { get; init; } = 50f;

    /// <summary>The front's light: its rim, in the additive pass, as a share of its material opacity.</summary>
    public float WaveGlowShare { get; init; } = 0.8f;

    /// <summary>
    /// The rim's glow on the way, a share of <see cref="WaveGlowShare"/>, rising to whole on arrival: a hot white rim for the
    /// whole travel read as a sword beam and flattened the peak (the tick must be the brightest moment).
    /// </summary>
    public float WaveTravelGlow { get; init; } = 0.45f;

    /// <summary>The front's body glowing faintly in the light pass (its softened cell), a share of its opacity.</summary>
    public float WaveBodyGlowShare { get; init; } = 0.35f;

    // ── D · THE CRUSH (ms after the tick) ──

    /// <summary>
    /// THE CRUSH IS MADE OF THE FRONT: the arcs start where the arriving crescent's tips are (half its arrival height from
    /// the target's middle, at the target's front) and fold over and under the target, pressing in until their edges
    /// bite into its drawn silhouette (<see cref="ClampShutShare"/>). A wave that vanished while two horizontal arcs popped
    /// in read as two effects (the first review).
    /// </summary>
    public float ClampShutShare { get; init; } = 0.42f;

    /// <summary>
    /// The arcs' start is capped at this share of the target's height from its middle: the crescent is taller than the
    /// creature, and starting at its very tips the arcs crossed the health-bar row and the skill dock for a frame or two.
    /// </summary>
    public float ClampStartCap { get; init; } = 0.66f;

    /// <summary>The arcs' width as a share of the target's drawn width (the front creature only, never the next one).</summary>
    public float ClampWidthShare { get; init; } = 0.8f;

    /// <summary>Where the shut arcs are centred, from the target's front, as a share of its drawn width (its body, not its tail).</summary>
    public float ClampCentreShare { get; init; } = 0.42f;

    /// <summary>The arcs press in over this long, hold, and let go (then fade by <see cref="EndMs"/>).</summary>
    public float CrushInMs { get; init; } = 70f;

    /// <inheritdoc cref="CrushInMs"/>
    public float CrushHoldMs { get; init; } = 100f;

    /// <inheritdoc cref="CrushInMs"/>
    public float CrushOutMs { get; init; } = 140f;

    /// <summary>The arcs' opacity at their peak (the brightest moment of the whole phrase is the crush).</summary>
    public float ClampPeakAlpha { get; init; } = 0.95f;

    /// <summary>The creature buckles under it: its drawn height and width at the deepest (feet stay on the floor).</summary>
    public float SquashY { get; init; } = 0.85f;

    /// <inheritdoc cref="SquashY"/>
    public float SquashX { get; init; } = 1.07f;

    /// <summary>The phrase ends here (ms after the tick): the arcs are gone and the field is at rest.</summary>
    public float EndMs { get; init; } = 380f;

    // ── COLOURS (the Seeker's Shadow family; no neon, no arcane blue) ──

    /// <summary>The field haze.</summary>
    public Color FieldColor { get; init; } = new(150, 112, 214);

    /// <summary>The pressure front's body and its rim.</summary>
    public Color WaveColor { get; init; } = new(176, 146, 238);

    /// <inheritdoc cref="WaveColor"/>
    public Color RimColor { get; init; } = new(236, 224, 255);

    /// <summary>
    /// The crush arcs: pale and hot on the tick (a pale LAVENDER, never the white of SPRAY's steel: near-white arcs read
    /// as blade strokes for a frame or two), cooling to violet as they let go.
    /// </summary>
    public Color ClampHotColor { get; init; } = new(226, 208, 255);

    /// <inheritdoc cref="ClampHotColor"/>
    public Color ClampColor { get; init; } = new(196, 138, 250);

    /// <inheritdoc cref="ClampHotColor"/>
    public Color ClampCoolColor { get; init; } = new(118, 72, 186);

    /// <summary>
    /// GIVING WAY TO A REACTION (JAWS): on a tick a presented reaction also plays (triggered from
    /// <see cref="YieldBeforeMs"/> before the tick to <see cref="YieldAfterMs"/> after it), the crush draws no arcs above
    /// and below the creature: those two and JAWS' fang rows on the same creature read as ONE jaw closing (the first
    /// review). The front flattens against the creature's facing side instead and the body still buckles.
    /// </summary>
    public float YieldBeforeMs { get; init; } = 700f;

    /// <inheritdoc cref="YieldBeforeMs"/>
    public float YieldAfterMs { get; init; } = 400f;

    /// <summary>The flattened front's width against the creature's side, a share of its travelling width.</summary>
    public float FlattenShare { get; init; } = 0.5f;

    /// <summary>The flattened front is gone by here (ms after the tick), before JAWS' fang crown thickens beside it.</summary>
    public float FlattenMs { get; init; } = 100f;

    /// <summary>
    /// QUIET while the champion performs an action (SPRAY, HARD HANDS): the front and the crush at this share, so a
    /// passive field never outshines the action (JAWS' rule).
    /// </summary>
    public float QuietShare { get; init; } = 0.6f;
}

/// <summary>The field recipes, by character and skill (ADR-011's contract: a presentation belongs to its SKILL).</summary>
public static class FieldRecipes
{
    /// <summary>THE SEEKER's PRESS (the FIELD / AURA reference).</summary>
    public static readonly FieldRecipe SeekerPress = new() { Id = "seeker.press" };

    private static readonly Dictionary<(string Character, string Skill), FieldRecipe> BySkill = new()
    {
        [("seeker", "hammer_press")] = SeekerPress,
    };

    /// <summary>
    /// The field recipe this champion presents for this skill (a SkillDef.Id), or null: the field keeps the generic held
    /// aura. RH_ACTION_RECIPES=0 turns these off with the action recipes, and RH_FIELD_RECIPES=0 turns off these alone.
    /// </summary>
    public static FieldRecipe? For(string characterId, string skillId)
        => ActionRecipes.Enabled && Enabled && BySkill.TryGetValue((characterId, skillId), out var own) ? own : null;

    /// <summary>RH_FIELD_RECIPES=0 presents every field the old way (the with/without comparison).</summary>
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("RH_FIELD_RECIPES") is not ("0" or "false");
}
