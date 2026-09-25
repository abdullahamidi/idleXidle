using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION / TRAP archetype): the world's answer to an enemy's bite, drawn
/// on its own layer and never on the champion's figure. JAWS is the reference: a tethered trap head that belongs to the
/// Seeker. The bite fires it, its jaws clamp the creature that bit, the chain takes the strain and jerks the creature,
/// and the mechanism is reeled back to his belt.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). The sentence, in ms after that frame: the TETHER FIRES and
/// the open head is at the creature (0), the JAWS CLAMP by rotating about their hinge to a hard stop (by 16), the CHAIN
/// TAKES THE STRAIN and the target is JERKED toward the Seeker (from the clamp), and the MECHANISM RETRACTS along its
/// chain to his belt. Nothing stays in the world.
/// </para>
/// <para>
/// RIGID. The head is three parts on one canvas (the hub and the two jaws) and only ROTATES about its authored pivot;
/// the metal is never scaled to fake a snap (the first build pumped the whole sprite from 0.82 to a 1.12 overshoot).
/// MATERIAL vs SOURCE, as SPRAY and HARD HANDS: the iron is untinted; the Source is light only (the teeth's glint at the
/// clamp, three small sparks, the tether's streak as it fires).
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART (tools/asset-pipeline/v2/seeker_jaws.py: every part on one 132 x 96 canvas) ─────────

    /// <summary>The hinge hub: the chain eye, the spring, the hub disk and its pivot bolt (drawn over the jaws' roots).</summary>
    public string BaseKey { get; init; } = "prop_seeker_jaws_base";

    /// <summary>Jaw A, the upper jaw, drawn closed.</summary>
    public string UpperKey { get; init; } = "prop_seeker_jaws_upper";

    /// <summary>Jaw B, the lower jaw, drawn closed.</summary>
    public string LowerKey { get; init; } = "prop_seeker_jaws_lower";

    /// <summary>Each jaw's teeth, white: the glint drawn through them at the clamp.</summary>
    public string UpperEdgeKey { get; init; } = "prop_seeker_jaws_upper_edge";

    /// <inheritdoc cref="UpperEdgeKey"/>
    public string LowerEdgeKey { get; init; } = "prop_seeker_jaws_lower_edge";

    /// <summary>The chain's dark metal body: a cross-section stretched along the tether.</summary>
    public string ChainBodyKey { get; init; } = "prop_seeker_chain_body";

    /// <summary>One chain link in two cells (face-on, edge-on): the link accents.</summary>
    public string ChainLinkKey { get; init; } = "prop_seeker_chain_link";

    /// <summary>The sparks and the tether's streak (shared parts, white).</summary>
    public string SparkKey { get; init; } = "fxp_spark_dot";

    /// <inheritdoc cref="SparkKey"/>
    public string StreakKey { get; init; } = "fxp_trail_soft";

    /// <summary>In the canvas (texture px): the pivot bolt both jaws rotate about.</summary>
    public Vector2 Pivot { get; init; } = new(30f, 48f);

    /// <summary>In the canvas: the chain's eye at the back of the hub.</summary>
    public Vector2 Eye { get; init; } = new(7.5f, 48f);

    /// <summary>In the canvas: the point between the jaws that closes ON the creature (the clamp point).</summary>
    public Vector2 BitePoint { get; init; } = new(84f, 48f);

    /// <summary>The head's length in the canvas, from its eye to its tips: its on-screen size is a length.</summary>
    public float HeadArtLength { get; init; } = 103.5f;

    // ── SIZE AND PLACE ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The head's length (eye to tips) as a share of the caught creature's visible height, clamped. It bites PART of
    /// the creature (a limb, the lower front), never a collar around half of it. Chosen from the size film (the first
    /// draft, 0.62, 85 % and 75 % of it at play size): 85 % reads as a compact clamp with legible teeth; 75 % shut is a
    /// bright dash. <see cref="ReactionRecipes.SizeDial"/> scales it for such a comparison.
    /// </summary>
    public float HeadBodyShare { get; init; } = 0.53f;

    /// <inheritdoc cref="HeadBodyShare"/>
    public float HeadMinPx { get; init; } = 56f;

    /// <inheritdoc cref="HeadBodyShare"/>
    public float HeadMaxPx { get; init; } = 104f;

    /// <summary>
    /// The clamp point when the silhouette cannot be read, as a share of the creature's visible body: x from the edge
    /// that faces the champion, y from its top. The front-lower part of the body, where a lunge plants itself.
    /// </summary>
    public Vector2 ClampFallback { get; init; } = new(0.20f, 0.72f);

    /// <summary>The rows of the silhouette searched for its front-most point (shares of the opaque height).</summary>
    public float ProbeFrom { get; init; } = 0.62f;

    /// <inheritdoc cref="ProbeFrom"/>
    public float ProbeTo { get; init; } = 0.90f;

    /// <summary>How far inside the silhouette's front edge the jaws close (share of the body's width): on a limb, not beside it.</summary>
    public float ProbeInset { get; init; } = 0.07f;

    /// <summary>The Seeker's trap anchor, his BELT: a share of his visible body (x from his back, he faces right).</summary>
    public Vector2 ChampionAnchor { get; init; } = new(0.60f, 0.56f);

    /// <summary>The head follows its tether's line, within this many degrees of level (it never points at the sky).</summary>
    public float MaxTiltDeg { get; init; } = 30f;

    // ── THE JAWS (degrees each jaw stands off the closed line) ───────────────────────────────────

    /// <summary>Open, as the head arrives at the creature.</summary>
    public float OpenDeg { get; init; } = 30f;

    /// <summary>The HARD STOP: shut on a limb, the jaws stand this far apart (a trap stops on what it bites).</summary>
    public float StopDeg { get; init; } = 3f;

    /// <summary>OPEN to the stop, accelerating (the spring's slam): the closed pose is on screen by the next frame.</summary>
    public float CloseMs { get; init; } = 16f;

    /// <summary>The stop's recoil: the jaws bounce this much further open, and settle back, over this long.</summary>
    public float ReboundDeg { get; init; } = 5f;

    /// <inheritdoc cref="ReboundDeg"/>
    public float ReboundMs { get; init; } = 50f;

    /// <summary>At the retract the jaws unlock to this angle over <see cref="UnlockMs"/>.</summary>
    public float UnlockDeg { get; init; } = 14f;

    /// <inheritdoc cref="UnlockDeg"/>
    public float UnlockMs { get; init; } = 40f;

    // ── THE TETHER AND THE FORCE (ms after the first frame) ──────────────────────────────────────

    /// <summary>The tether fires: its streak from his belt to the head lives this long.</summary>
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

    /// <summary>THE RETRACT: the jaws unlock and the head is reeled back along its chain to the belt.</summary>
    public float RetractAtMs { get; init; } = 150f;

    /// <inheritdoc cref="RetractAtMs"/>
    public float RetractMs { get; init; } = 100f;

    /// <summary>The last share of the retract fades the head and its chain into the belt.</summary>
    public float RetractFadeShare { get; init; } = 0.35f;

    /// <summary>A caught creature that FALLS: the head holds its clamp this long after the snap, then lets go and retracts.</summary>
    public float DeathHoldMs { get; init; } = 50f;

    /// <summary>The champion falls on the same bite: after the snap the chain slackens and the head fades where it is.</summary>
    public float SlackFadeMs { get; init; } = 90f;

    // ── LIGHT (the Source) ───────────────────────────────────────────────────────────────────────

    /// <summary>The teeth's glint at the clamp: its peak and its life (ms).</summary>
    public float GlintPeak { get; init; } = 0.9f;

    /// <inheritdoc cref="GlintPeak"/>
    public float GlintMs { get; init; } = 70f;

    /// <summary>Sparks thrown from the teeth at the stop, along the closing: how many, how far (share of the head), how long.</summary>
    public int Sparks { get; init; } = 3;

    /// <inheritdoc cref="Sparks"/>
    public float SparkReach { get; init; } = 0.30f;

    /// <inheritdoc cref="Sparks"/>
    public float SparkMs { get; init; } = 90f;

    /// <summary>The tether's Source streak as it fires (brightest at the head), and its width.</summary>
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
    /// The link ACCENTS, as shares of the chain from the belt (0) to the head (1): dense at both ends (the hardware at the
    /// belt and at the head), sparse across the middle. The dark body carries the rest: no gap, and never sixty-four
    /// equally loud stamps.
    /// </summary>
    public IReadOnlyList<float> LinkAt { get; init; } = new[] { 0.03f, 0.08f, 0.14f, 0.24f, 0.38f, 0.52f, 0.66f, 0.78f, 0.87f, 0.93f, 0.97f };

    /// <summary>One link's drawn length (px).</summary>
    public float LinkPx { get; init; } = 15f;

    // ── SOUND AND THE SCREEN'S GENERIC CUES ──────────────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue, most specific first. Played on the first frame; its strongest transient (the clamp) sits
    /// ~17 ms into it, on the frame the jaws hit their stop.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_snap", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.46f;

    /// <summary>How wide the snap is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the jaws, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for the comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>The caught creature's hit flash for the reflected blow: soft, so the silhouette the jaws bite stays readable.</summary>
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

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the trap head's size against the recipe's (the size comparison film). 1 otherwise.</summary>
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
