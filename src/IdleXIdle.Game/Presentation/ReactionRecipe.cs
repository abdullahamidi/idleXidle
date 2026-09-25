using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION / TRAP archetype): the world's answer to an enemy's bite, drawn
/// on its own layer and never on the champion's figure. JAWS is the reference: a spring-loaded hunting clamp on a chain
/// that belongs to the Seeker. The bite fires it, its two clamp arms snap shut on the creature that bit, the chain takes
/// the strain and jerks the creature, and the mechanism is reeled back to his belt. "JAWS" names the mechanism's two
/// opposing clamp arms: the object is a trap, never a creature's head or mouth.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). The sentence, in ms after that frame: the TETHER FIRES and
/// the open clamp is at the creature (0), the ARMS SNAP SHUT, each rotating about its own hinge to a hard stop (by 16),
/// the CHAIN TAKES THE STRAIN and the target is JERKED toward the Seeker (from the clamp), and the MECHANISM RETRACTS
/// along its chain to his belt. Nothing stays in the world.
/// </para>
/// <para>
/// RIGID. The clamp is three parts on one canvas (the housing and the two arms); each arm only ROTATES about its own
/// authored hinge pin; the metal is never scaled to fake a snap (the first build pumped the whole sprite from 0.82 to a
/// 1.12 overshoot). MATERIAL vs SOURCE, as SPRAY and HARD HANDS: the iron is untinted; the Source is light only (the
/// teeth's glint at the clamp, a spark from each hinge at the stop, the tether's streak as it fires).
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART (tools/asset-pipeline/v2/seeker_jaws.py: every part on one 99 x 102 canvas) ─────────

    /// <summary>
    /// The HOUSING: the chain's shackle, the riveted spring box (its coil on top), the reinforced front plate and the
    /// two hinge brackets at its corners (drawn over the arms' roots). It never moves against the clamp's line.
    /// </summary>
    public string BaseKey { get; init; } = "prop_seeker_jaws_base";

    /// <summary>Clamp arm A, the upper crescent, drawn at its stop; it turns about <see cref="UpperPivot"/>.</summary>
    public string UpperKey { get; init; } = "prop_seeker_jaws_upper";

    /// <summary>Clamp arm B, the lower crescent, drawn at its stop; it turns about <see cref="LowerPivot"/>.</summary>
    public string LowerKey { get; init; } = "prop_seeker_jaws_lower";

    /// <summary>Each arm's teeth, white: the glint drawn through them at the clamp.</summary>
    public string UpperEdgeKey { get; init; } = "prop_seeker_jaws_upper_edge";

    /// <inheritdoc cref="UpperEdgeKey"/>
    public string LowerEdgeKey { get; init; } = "prop_seeker_jaws_lower_edge";

    /// <summary>The chain's dark metal body: a cross-section stretched along the tether.</summary>
    public string ChainBodyKey { get; init; } = "prop_seeker_chain_body";

    /// <summary>One chain link in two cells (face-on, edge-on): the link accents.</summary>
    public string ChainLinkKey { get; init; } = "prop_seeker_chain_link";

    /// <summary>The hinge sparks and the tether's streak (shared parts, white).</summary>
    public string SparkKey { get; init; } = "fxp_spark_dot";

    /// <inheritdoc cref="SparkKey"/>
    public string StreakKey { get; init; } = "fxp_trail_soft";

    /// <summary>
    /// In the canvas (texture px): the upper arm's hinge pin, in the bracket at the plate's top corner. Each arm has its
    /// OWN hinge (as a bear trap's jaws hinge at the two ends of its base): one shared pivot behind two jaws read as a
    /// head with an eye.
    /// </summary>
    public Vector2 UpperPivot { get; init; } = new(51f, 22.5f);

    /// <summary>In the canvas: the lower arm's hinge pin, in the bracket at the plate's bottom corner.</summary>
    public Vector2 LowerPivot { get; init; } = new(51f, 79.5f);

    /// <summary>In the canvas: where the chain runs through the shackle at the back of the housing.</summary>
    public Vector2 Eye { get; init; } = new(1.5f, 51f);

    /// <summary>In the canvas: the middle of the space the arms close around, the point that is placed ON the creature.</summary>
    public Vector2 BitePoint { get; init; } = new(66f, 51f);

    /// <summary>The clamp's length in the canvas, from its shackle to its arms' front: its on-screen size is a length.</summary>
    public float ClampArtLength { get; init; } = 88f;

    // ── SIZE AND PLACE ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The clamp's length (shackle to arms) as a share of the caught creature's visible height, clamped. It grips PART
    /// of the creature (a limb, the lower front), never a collar around half of it. The clamp is about as tall as it is
    /// long, so this share is lower than the old flat, long head's (0.53); chosen from the size film at play size.
    /// <see cref="ReactionRecipes.SizeDial"/> scales it for such a comparison.
    /// </summary>
    public float ClampBodyShare { get; init; } = 0.42f;

    /// <inheritdoc cref="ClampBodyShare"/>
    public float ClampMinPx { get; init; } = 48f;

    /// <inheritdoc cref="ClampBodyShare"/>
    public float ClampMaxPx { get; init; } = 88f;

    /// <summary>
    /// The clamp point when the silhouette cannot be read, as a share of the creature's visible body: x from the edge
    /// that faces the champion, y from its top. The front-lower part of the body, where a lunge plants itself.
    /// </summary>
    public Vector2 ClampFallback { get; init; } = new(0.20f, 0.72f);

    /// <summary>The rows of the silhouette searched for its front-most point (shares of the opaque height).</summary>
    public float ProbeFrom { get; init; } = 0.62f;

    /// <inheritdoc cref="ProbeFrom"/>
    public float ProbeTo { get; init; } = 0.90f;

    /// <summary>How far inside the silhouette's front edge the arms close (share of the body's width): on a limb, not beside it.</summary>
    public float ProbeInset { get; init; } = 0.07f;

    /// <summary>The Seeker's trap anchor, his BELT: a share of his visible body (x from his back, he faces right).</summary>
    public Vector2 ChampionAnchor { get; init; } = new(0.60f, 0.56f);

    /// <summary>The clamp follows its tether's line, within this many degrees of level (it never points at the sky).</summary>
    public float MaxTiltDeg { get; init; } = 30f;

    // ── THE ARMS (degrees each arm stands off its drawn stop, about its own hinge) ─────────────────

    /// <summary>Open, as the clamp arrives at the creature.</summary>
    public float OpenDeg { get; init; } = 30f;

    /// <summary>The HARD STOP: shut on a limb, the arms stand this far open (a trap stops on what it bites).</summary>
    public float StopDeg { get; init; } = 3f;

    /// <summary>OPEN to the stop, accelerating (the spring's slam): the closed pose is on screen by the next frame.</summary>
    public float CloseMs { get; init; } = 16f;

    /// <summary>The stop's recoil: the arms bounce this much further open, and settle back, over this long.</summary>
    public float ReboundDeg { get; init; } = 5f;

    /// <inheritdoc cref="ReboundDeg"/>
    public float ReboundMs { get; init; } = 50f;

    /// <summary>At the retract the arms unlock to this angle over <see cref="UnlockMs"/>.</summary>
    public float UnlockDeg { get; init; } = 14f;

    /// <inheritdoc cref="UnlockDeg"/>
    public float UnlockMs { get; init; } = 40f;

    // ── THE TETHER AND THE FORCE (ms after the first frame) ──────────────────────────────────────

    /// <summary>The tether fires: its streak from his belt to the clamp lives this long.</summary>
    public float WhipMs { get; init; } = 60f;

    /// <summary>The chain from its whip's curve to taut; then a small shiver.</summary>
    public float TautMs { get; init; } = 60f;

    /// <summary>The whip's curve at its middle, as a share of the chain's span (clamped).</summary>
    public float WhipSag { get; init; } = 0.10f;

    /// <inheritdoc cref="WhipSag"/>
    public float WhipSagMaxPx { get; init; } = 40f;

    /// <summary>The taut chain's shiver: amplitude (px), frequency (Hz) and decay (ms).</summary>
    public float ShiverPx { get; init; } = 3f;

    /// <inheritdoc cref="ShiverPx"/>
    public float ShiverHz { get; init; } = 24f;

    /// <inheritdoc cref="ShiverPx"/>
    public float ShiverDecayMs { get; init; } = 45f;

    /// <summary>
    /// THE YANK: from the clamp, the chain jerks the caught creature TOWARD the Seeker (the bite brought it in; the
    /// tether catches and pulls), peaking <see cref="YankPeakMs"/> after the clamp, settled by <see cref="YankMs"/>.
    /// A few pixels: a share of its width, clamped.
    /// </summary>
    public float YankShare { get; init; } = 0.06f;

    /// <inheritdoc cref="YankShare"/>
    public float YankMinPx { get; init; } = 4f;

    /// <inheritdoc cref="YankShare"/>
    public float YankMaxPx { get; init; } = 12f;

    /// <inheritdoc cref="YankShare"/>
    public float YankPeakMs { get; init; } = 30f;

    /// <inheritdoc cref="YankShare"/>
    public float YankMs { get; init; } = 150f;

    /// <summary>THE RETRACT: the arms unlock and the clamp is reeled back along its chain to the belt.</summary>
    public float RetractAtMs { get; init; } = 150f;

    /// <inheritdoc cref="RetractAtMs"/>
    public float RetractMs { get; init; } = 100f;

    /// <summary>The last share of the retract fades the clamp and its chain into the belt.</summary>
    public float RetractFadeShare { get; init; } = 0.35f;

    /// <summary>A caught creature that FALLS: the clamp holds this long after the snap, then lets go and retracts.</summary>
    public float DeathHoldMs { get; init; } = 50f;

    /// <summary>The champion falls on the same bite: after the snap the chain slackens and the clamp fades where it is.</summary>
    public float SlackFadeMs { get; init; } = 90f;

    // ── LIGHT (the Source) ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The teeth's glint at the clamp: its peak and its life (ms). One brief tooth-edge light, never the whole clamp
    /// outlined: at 0.9 the teeth flashed WHITE for three frames, a row of white points that leant toward a grin.
    /// </summary>
    public float GlintPeak { get; init; } = 0.6f;

    /// <inheritdoc cref="GlintPeak"/>
    public float GlintMs { get; init; } = 60f;

    /// <summary>
    /// Sparks squeezed out of the HINGES as the arms hit their stop (steel meets steel at the pins, not on the hide):
    /// one per hinge, the first <see cref="Sparks"/> of the two; how far (share of the clamp's length) and how long.
    /// NONE for JAWS: reviewed at play size with two (the identity pass), a hinge spark is a 3-4 px speck behind the
    /// teeth's glint that nobody sees; the mechanism's motion, the glint and the clack carry the snap.
    /// </summary>
    public int Sparks { get; init; }

    /// <inheritdoc cref="Sparks"/>
    public float SparkReach { get; init; } = 0.16f;

    /// <inheritdoc cref="Sparks"/>
    public float SparkMs { get; init; } = 70f;

    /// <summary>The tether's Source streak as it fires (brightest at the clamp), and its width.</summary>
    public float StreakPeak { get; init; } = 0.45f;

    /// <inheritdoc cref="StreakPeak"/>
    public float StreakPx { get; init; } = 3f;

    /// <summary>While the champion performs an action, the reaction's light plays at this share: it stays secondary.</summary>
    public float LightUnderAction { get; init; } = 0.55f;

    // ── THE CHAIN ────────────────────────────────────────────────────────────────────────────────

    /// <summary>The chain's body: how thick it is drawn (px) and in how many segments its curve is drawn.</summary>
    public float BodyPx { get; init; } = 5f;

    /// <inheritdoc cref="BodyPx"/>
    public int BodySegments { get; init; } = 14;

    /// <summary>
    /// The link ACCENTS, as shares of the chain from the belt (0) to the clamp (1): dense at both ends (the hardware at
    /// the belt and at the shackle), sparse across the middle. The dark body carries the rest: no gap, and never sixty-four
    /// equally loud stamps.
    /// </summary>
    public IReadOnlyList<float> LinkAt { get; init; } = new[] { 0.03f, 0.08f, 0.14f, 0.24f, 0.38f, 0.52f, 0.66f, 0.78f, 0.87f, 0.93f, 0.97f };

    /// <summary>One link's drawn length (px).</summary>
    public float LinkPx { get; init; } = 15f;

    // ── SOUND AND THE SCREEN'S GENERIC CUES ──────────────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue, most specific first. Played on the first frame; its strongest transient (the dry steel
    /// clack of the arms hitting their stop) sits ~18 ms into it, on the frame the arms shut.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_snap", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.46f;

    /// <summary>How wide the snap is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the clamp, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for the comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>The caught creature's hit flash for the reflected blow: soft, so the silhouette the arms grip stays readable.</summary>
    public float TargetFlash { get; init; } = 0.18f;

    /// <inheritdoc cref="TargetFlash"/>
    public float TargetFlashMs { get; init; } = 90f;
}

/// <summary>The reaction recipes, looked up by the SKILL (never by its Form's shared clip or effect).</summary>
public static class ReactionRecipes
{
    /// <summary>RH_REACTION_CALLOUT=1: the authored reaction still says its callout (the with/without comparison).</summary>
    public static readonly bool CalloutOverride =
        Environment.GetEnvironmentVariable("RH_REACTION_CALLOUT") is "1" or "true";

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the clamp's size against the recipe's (the size comparison film). 1 otherwise.</summary>
    public static readonly float SizeDial =
        float.TryParse(Environment.GetEnvironmentVariable("RH_REACTION_SIZE"), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 1f;

    /// <summary>THE SEEKER's JAWS (the REACTION / TRAP reference): bite for bite.</summary>
    public static readonly ReactionRecipe SeekerJaws = new() { Id = "seeker.jaws" };

    // THE LOOKUP (the accepted contract: skill-specific → deliberately shared → legacy). A reaction's presentation
    // belongs to its SKILL: JAWS and REPAY share the Snare Form's "trap" clip and effect, and REPAY (an Active) must
    // keep its own. So there is no shared tier for traps: a skill with no entry here keeps its legacy presentation.
    private static readonly Dictionary<(string Character, string Skill), ReactionRecipe> BySkill = new()
    {
        [("seeker", "snare_jaws")] = SeekerJaws,
    };

    /// <summary>
    /// The reaction recipe this champion presents for this skill (<paramref name="skillId"/>, a SkillDef.Id), or null:
    /// the skill keeps its legacy presentation. RH_ACTION_RECIPES=0 turns these off with the action recipes, and
    /// RH_REACTION_RECIPES=0 turns off these alone.
    /// </summary>
    public static ReactionRecipe? For(string characterId, string skillId)
        => ActionRecipes.Enabled && Enabled && BySkill.TryGetValue((characterId, skillId), out var own) ? own : null;

    /// <summary>
    /// RH_REACTION_RECIPES=0 presents every reaction the old way while the actions stay performed: the switch that
    /// proves the reaction layer changes nothing about SPRAY and HARD HANDS (the same seeded fight, on and off).
    /// </summary>
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("RH_REACTION_RECIPES") is not ("0" or "false");
}
