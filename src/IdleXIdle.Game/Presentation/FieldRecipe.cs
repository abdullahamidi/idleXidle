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
/// creature's body buckles for a moment; the hard beat, its hottest pixels a short accent on the arcs' pressing edge);
/// E the SETTLE (the arcs release and crumble,
/// the field is quiet again).
/// </para>
/// <para>
/// FOUR PARTS, composed at runtime (tools/asset-pipeline/v2/seeker_press.py): the field haze, the pressure front, the fold
/// (the front becoming the crush) and the clamp arc. All white or grey and tinted
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

    /// <summary>
    /// The pressure FRONT, a broad wall facing the enemy side, in three cells (PIXEL-HARD, authored at a fifth of its cell's
    /// resolution and upscaled NEAREST, in value bands; drawn axis-aligned on whole pixels, a rotated grid read as a
    /// serrated blade): <see cref="WaveBodyCell"/>, <see cref="WaveEdgeCell"/> (the light pass: the lit edge only) and
    /// <see cref="WaveDissolveCell"/> (losing cohesion as it gives way to a reaction). It has NO head, core, glowing centre
    /// or trailing copy (a pale accent at the middle of its edge gave the wall a projectile's head; the one-frame echo was a
    /// projectile's tail; launch contours tried behind it read as speed dashes).
    /// </summary>
    public string WaveKey { get; init; } = "fxp_seeker_press_wave";

    /// <inheritdoc cref="WaveKey"/>
    public const int WaveBodyCell = 0, WaveEdgeCell = 1, WaveDissolveCell = 2;

    /// <summary>
    /// THE FOLD: the front becoming the crush -- its wall, collapsing at the target's middle, curving round into ends bent
    /// over the target (drawn flipped below it too), over the arcs' OWN span (its back at their left end, its ends at their
    /// right end: reaching past them, the fold surged forward and the arcs snapped back). Two cells, body and
    /// <see cref="FoldEdgeCell"/> (its lit edge, in the light pass: lit like the front it is; unlit it was the phrase's
    /// dimmest frame); at the clamp's logical resolution and drawn at the clamp's scale.
    /// </summary>
    public string FoldKey { get; init; } = "fxp_seeker_press_fold";

    /// <inheritdoc cref="FoldKey"/>
    public Point FoldCell { get; init; } = new(512, 320);

    /// <inheritdoc cref="FoldKey"/>
    public const int FoldBodyCell = 0, FoldEdgeCell = 1;

    /// <summary>
    /// The fold's lit edge, a share of the arrival rim's light: its edge runs the whole C (wall and both ends), longer than
    /// the front's rim, and at the rim's own light the fold was the phrase's brightest frame (a flash by another name).
    /// </summary>
    public float FoldGlowShare { get; init; } = 0.72f;

    /// <summary>
    /// THE LEVEL AXIS the front propagates along: this share of the way from the FIELD's middle height (0) to the target's
    /// middle height (1). Level, never steered at the target; halfway, so the wall grows out of the field (at the target's
    /// height it was born under the Seeker's belt, below the field) and still spans the target it reaches.
    /// </summary>
    public float WaveAxisShare { get; init; } = 0.5f;

    /// <summary>
    /// The fold shows from the tick to this ms (one frame at 60 fps): the wavefront -> the contact -> the compression is ONE
    /// continuous transformation, not a wave that vanishes while a crush pops in; the arcs take over after it. A slow frame
    /// never skips it: the first frame drawn after the contact folds while the crush is still closing (a capture that
    /// dropped the +17 ms frame showed the front simply replaced by the arcs).
    /// </summary>
    public float FoldMs { get; init; } = 24f;

    /// <inheritdoc cref="WaveKey"/>
    public Point WaveCell { get; init; } = new(192, 512);

    /// <summary>The leading rim's x in a wave cell (at its middle): the point that travels to the target's front.</summary>
    public float WaveLeadX { get; init; } = 153.6f;

    /// <summary>
    /// The CRUSH arc, pressing down (drawn flipped below the target, pressing up), in five cells: whole, three pixel-chunk
    /// dissolve states for its release (it loses cohesion; a vector alpha-fade read as HD magic), and
    /// <see cref="ClampEdgeCell"/>.
    /// </summary>
    public string ClampKey { get; init; } = "fxp_seeker_press_clamp";

    /// <inheritdoc cref="ClampKey"/>
    public int ClampCells { get; init; } = 5;

    /// <summary>
    /// The clamp's EDGE cell, the only part the tick's heat lights (in the additive pass): the pressing edge as a dim line and
    /// a short PEAK accent at the contact. The arc's body stays violet (lit whole, the two arcs made the tick as bright as
    /// SPRAY's hit and twice HARD HANDS').
    /// </summary>
    public const int ClampEdgeCell = 4;

    /// <inheritdoc cref="ClampKey"/>
    public Point ClampCell { get; init; } = new(512, 160);

    /// <summary>The pressing edge's y in the clamp cell (at its middle).</summary>
    public float ClampPressY { get; init; } = 127.2f;

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
    public float ContractScale { get; init; } = 0.82f;

    /// <inheritdoc cref="ContractScale"/>
    public float ContractAlpha { get; init; } = 0.5f;

    // ── C · THE EMISSION ──

    /// <summary>The front leaves the Seeker here (ms before the tick) and ARRIVES on the tick.</summary>
    public float LaunchMs { get; init; } = -300f;

    /// <summary>
    /// The field RELEASES as it lets the front go: it SNAPS out past its size (<see cref="SpringScale"/>, reached in the first
    /// quarter of <see cref="SpringMs"/>) and settles; the start of the hard beat (a gentle spring read as soft).
    /// </summary>
    public float SpringScale { get; init; } = 1.10f;

    /// <inheritdoc cref="SpringScale"/>
    public float SpringMs { get; init; } = 110f;

    /// <summary>
    /// The front's height: on arrival, <see cref="WaveArriveShare"/> of the TARGET's drawn silhouette (it spans the
    /// creature, top to feet and past them); as it leaves the field, this share of that (a broad front from the start,
    /// never a bullet). It grows little on the way: one cell scaled from half the Seeker to its arrival drew its art pixel
    /// from ~2 to ~3 screen px, a material that changes in flight.
    /// </summary>
    public float WaveLaunchShare { get; init; } = 0.87f;

    /// <inheritdoc cref="WaveLaunchShare"/>
    public float WaveArriveShare { get; init; } = 1.7f;

    /// <summary>The front's opacity as it leaves and as it arrives (it gathers weight on the way).</summary>
    public float WaveLaunchAlpha { get; init; } = 0.6f;

    /// <inheritdoc cref="WaveLaunchAlpha"/>
    public float WaveArriveAlpha { get; init; } = 0.9f;

    /// <summary>
    /// The travel ACCELERATES into the contact (progress = t^this): the largest step is the last, so the front hits rather
    /// than lands (an ease-out arrived gently, the opposite of a hard beat).
    /// </summary>
    public float TravelEasePower { get; init; } = 1.8f;

    /// <summary>The front collapses into the crush over this long after arriving, losing cohesion in pixel chunks.</summary>
    public float WaveCollapseMs { get; init; } = 34f;

    /// <summary>
    /// The front's light: its rim, in the additive pass, as a share of its material opacity (a lavender LIGHT line; at 0.8
    /// the arrival rim alone lit as much as HARD HANDS' hit).
    /// </summary>
    public float WaveGlowShare { get; init; } = 0.6f;

    /// <summary>
    /// The rim's glow on the way, a share of <see cref="WaveGlowShare"/>, rising to whole on arrival: a hot white rim for the
    /// whole travel read as a sword beam and flattened the beat (the rim is whole only as it arrives, on the tick).
    /// </summary>
    public float WaveTravelGlow { get; init; } = 0.3f;

    // ── D · THE CRUSH (ms after the tick) ──

    /// <summary>
    /// THE CRUSH IS MADE OF THE FRONT (through <see cref="FoldKey"/>): the arcs start where the arriving crescent's tips are (half its arrival height from
    /// the target's middle, at the target's front) and fold over and under the target, pressing in until their edges
    /// bite into its drawn silhouette (<see cref="ClampShutShare"/>) as it BUCKLES (placed on the buckled body, they press it
    /// down with it and touch it; placed on the upright one, they hovered 35-40 px off a crouched creature). A wave that
    /// vanished while two horizontal arcs popped in read as two effects (the first review).
    /// </summary>
    public float ClampShutShare { get; init; } = 0.36f;

    /// <summary>
    /// The arcs' start is capped at this share of the target's height from its middle: the crescent is taller than the
    /// creature, and starting at its very tips the arcs crossed the health-bar row and the skill dock for a frame or two.
    /// </summary>
    public float ClampStartCap { get; init; } = 0.66f;

    /// <summary>
    /// The arcs' width as a share of the target's drawn width AT LAUNCH (its pose then, before any lunge of its own): the arcs'
    /// size and art pixel never follow the pose (sized from a lunge's long silhouette they grew coarser than the world's
    /// pixel); where they press is where the front stopped (<see cref="FoldBackShare"/>).
    /// </summary>
    public float ClampWidthShare { get; init; } = 0.85f;

    /// <summary>
    /// THE CRUSH FORMS WHERE THE FRONT STOPPED: this share of the fold's and the arcs' width lies BEHIND the front's stop (their
    /// back where the front's body was), the rest over the creature's head and shoulders -- latched on the first frame after
    /// the contact, whatever the frame phase and whatever the creature does next. Placed from the creature's silhouette, an
    /// early first frame used its launch pose and the next its pressed one: a forward lurch, then a snap back.
    /// </summary>
    public float FoldBackShare { get; init; } = 0.35f;

    /// <summary>The arcs press in over this long, hold, and let go (then fade by <see cref="EndMs"/>).</summary>
    public float CrushInMs { get; init; } = 30f;

    /// <inheritdoc cref="CrushInMs"/>
    public float CrushHoldMs { get; init; } = 60f;

    /// <inheritdoc cref="CrushInMs"/>
    public float CrushOutMs { get; init; } = 120f;

    /// <summary>The arcs' opacity at their peak (the most solid moment of the phrase; their heat is only a line on the edge).</summary>
    public float ClampPeakAlpha { get; init; } = 0.95f;

    /// <summary>The creature buckles under it: its drawn height and width at the deepest (feet stay on the floor).</summary>
    public float SquashY { get; init; } = 0.81f;

    /// <inheritdoc cref="SquashY"/>
    public float SquashX { get; init; } = 1.09f;

    /// <summary>The phrase ends here (ms after the tick): the arcs are gone and the field is at rest.</summary>
    public float EndMs { get; init; } = 300f;

    // ── COLOURS (the Seeker's Shadow family; no neon, no arcane blue) ──

    /// <summary>The field haze.</summary>
    public Color FieldColor { get; init; } = new(150, 112, 214);

    /// <summary>The pressure front's body and its rim.</summary>
    public Color WaveColor { get; init; } = new(176, 146, 238);

    /// <inheritdoc cref="WaveColor"/>
    public Color RimColor { get; init; } = new(236, 224, 255);

    /// <summary>
    /// The crush arcs: violet (<see cref="ClampColor"/>), cooling as they let go; the tick's heat is a pale LAVENDER on
    /// their pressing EDGE only (<see cref="ClampEdgeCell"/>, never the white of SPRAY's steel: near-white arcs read as
    /// blade strokes for a frame or two).
    /// </summary>
    public Color ClampHotColor { get; init; } = new(226, 208, 255);

    /// <summary>The pressing edge's heat in the additive pass, a share of the arcs' opacity (the PEAK is tiny, not bright).</summary>
    public float ClampGlowShare { get; init; } = 0.7f;

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

    /// <summary>The flattened front is gone by here (ms after the tick), before JAWS' fang crown thickens beside it.</summary>
    public float FlattenMs { get; init; } = 100f;

    /// <summary>
    /// QUIET while the champion performs an action (SPRAY, HARD HANDS), and on a tick an action's contact lands close to
    /// (<see cref="QuietBeforeMs"/> before it to <see cref="QuietAfterMs"/> after it: the contact can land before the
    /// action's performance has started drawing): the front and the crush at this share, so a passive field never
    /// outshines the action (JAWS' rule).
    /// </summary>
    public float QuietShare { get; init; } = 0.6f;

    /// <inheritdoc cref="QuietShare"/>
    public float QuietBeforeMs { get; init; } = 200f;

    /// <inheritdoc cref="QuietShare"/>
    public float QuietAfterMs { get; init; } = 350f;
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
