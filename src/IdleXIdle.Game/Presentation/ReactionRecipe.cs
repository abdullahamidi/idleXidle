using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION / TRAP archetype): the world's answer to an enemy's bite, drawn
/// on its own layer and never on the champion's figure. JAWS is the reference: a spring-loaded BEAR TRAP on a chain that
/// belongs to the Seeker. The bite fires it, its two big jaws spring shut "( )" around the creature that bit, the chain
/// takes the strain and jerks the creature, and the trap is reeled back to his belt. "JAWS" names the trap's two
/// opposing jaws: the object is a trap, never a creature's head or mouth, and never a hook.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). The sentence, in ms after that frame: the TETHER FIRES and
/// the OPEN trap is at the creature (0); the JAWS SPRING SHUT, each about its own pin, slow for a frame, then violently,
/// to a hard stop at ~50 ms (the eye sees open, moving, shut: 16 ms was one open frame and one shut frame); on that
/// stop the answer lands (the number, the flash, the clack); the CHAIN TAKES THE STRAIN and the target is JERKED toward
/// the Seeker; and the TRAP RETRACTS along its chain to his belt. Nothing stays in the world. The fight resolved the
/// answer at the bite (t 0); only its PRESENTATION completes on the jaws' stop, 50 ms later.
/// </para>
/// <para>
/// RIGID. The trap is three parts on one canvas (the small base and the two jaws); each jaw only ROTATES about its own
/// authored pin; the metal is never scaled to fake a snap (the first build pumped the whole sprite from 0.82 to a 1.12
/// overshoot). The NEAR jaw is drawn BEHIND the caught creature and the far jaw in front, so the limb is between them.
/// MATERIAL vs SOURCE, as SPRAY and HARD HANDS: the iron is untinted; the Source is light only (a tooth-edge glint at the
/// stop, the tether's streak as it fires).
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART (tools/asset-pipeline/v2/seeker_jaws.py: every part on one 120 x 120 canvas) ────────

    /// <summary>
    /// The HOUSING, small: the base bar carrying the two pins, the trigger spring under it, the chain eye at its rear end
    /// (drawn over the jaws' roots). It never moves against the trap; the jaws are the silhouette.
    /// </summary>
    public string BaseKey { get; init; } = "prop_seeker_jaws_base";

    /// <summary>The NEAR jaw "(" (the Seeker's side), drawn shut; it turns about <see cref="NearPivot"/> and is drawn BEHIND the caught creature.</summary>
    public string NearKey { get; init; } = "prop_seeker_jaws_near";

    /// <summary>The FAR jaw ")" (the creature's side), drawn shut; it turns about <see cref="FarPivot"/>, in front of the creature.</summary>
    public string FarKey { get; init; } = "prop_seeker_jaws_far";

    /// <summary>Each jaw's teeth, white: the glint drawn through them at the stop.</summary>
    public string NearEdgeKey { get; init; } = "prop_seeker_jaws_near_edge";

    /// <inheritdoc cref="NearEdgeKey"/>
    public string FarEdgeKey { get; init; } = "prop_seeker_jaws_far_edge";

    /// <summary>The chain's dark metal body: a cross-section stretched along the tether.</summary>
    public string ChainBodyKey { get; init; } = "prop_seeker_chain_body";

    /// <summary>One chain link in two cells (face-on, edge-on): the link accents.</summary>
    public string ChainLinkKey { get; init; } = "prop_seeker_chain_link";

    /// <summary>The pin sparks (none for JAWS) and the tether's streak (shared parts, white).</summary>
    public string SparkKey { get; init; } = "fxp_spark_dot";

    /// <inheritdoc cref="SparkKey"/>
    public string StreakKey { get; init; } = "fxp_trail_soft";

    /// <summary>
    /// In the canvas (texture px): the near jaw's pin, on the base. Each jaw has its OWN pin, a compact pair 15 px apart at
    /// the base's middle (the identity pass's pins stood 57 px apart at a plate's two corners: a tall bracket).
    /// </summary>
    public Vector2 NearPivot { get; init; } = new(52.5f, 94.5f);

    /// <summary>In the canvas: the far jaw's pin, on the base.</summary>
    public Vector2 FarPivot { get; init; } = new(67.5f, 94.5f);

    /// <summary>In the canvas: where the chain hooks on, the eye at the base's rear end.</summary>
    public Vector2 Eye { get; init; } = new(19.5f, 97.5f);

    /// <summary>In the canvas: the jaws' common centre when shut, the point placed ON the caught limb.</summary>
    public Vector2 BitePoint { get; init; } = new(60f, 63f);

    /// <summary>The trap's HEIGHT in the canvas, from the jaws' top to the spring's foot: its on-screen size is a height.</summary>
    public float ClampArtHeight { get; init; } = 87f;

    // ── SIZE AND PLACE ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The trap's height (jaws' top to spring) as a share of the caught creature's visible height, clamped. It grips PART
    /// of the creature (a limb, the lower front), never a collar around half of it; chosen from the size film at play
    /// size. <see cref="ReactionRecipes.SizeDial"/> scales it for such a comparison.
    /// </summary>
    public float ClampBodyShare { get; init; } = 0.40f;

    /// <inheritdoc cref="ClampBodyShare"/>
    public float ClampMinPx { get; init; } = 52f;

    /// <inheritdoc cref="ClampBodyShare"/>
    public float ClampMaxPx { get; init; } = 96f;

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

    /// <summary>
    /// The trap stands upright and leans toward its tether by <see cref="TiltShare"/> of the chain's angle, never past
    /// this many degrees (a trap on a limb, not a projectile along a line).
    /// </summary>
    public float MaxTiltDeg { get; init; } = 12f;

    /// <inheritdoc cref="MaxTiltDeg"/>
    public float TiltShare { get; init; } = 0.5f;

    // ── THE JAWS (degrees each jaw stands off its drawn shut pose, about its own pin) ──────────────

    /// <summary>
    /// OPEN, as the trap arrives at the creature: 34 degrees each, a wide toothed cup (the space something is about to
    /// be caught in). The identity pass opened 30 degrees on a jaw a third this size and nobody could see it open.
    /// </summary>
    public float OpenDeg { get; init; } = 34f;

    /// <summary>The HARD STOP: shut on a limb, the jaws stand this far open (a trap stops on what it bites).</summary>
    public float StopDeg { get; init; } = 3f;

    /// <summary>
    /// OPEN to the stop. A spring trap does not close at an even speed: the travel goes as the SQUARE of the time, slow
    /// for the first frame and violent at the end, so most of it happens near contact. At 60 fps the eye gets open,
    /// still open (+17), half shut (+33), SHUT (+50): the 16 ms of the polish pass was one open frame and one shut frame.
    /// </summary>
    public float CloseMs { get; init; } = 50f;

    /// <summary>The stop's recoil, ONE small bounce (metal hitting resistance): up this far, settled over this long.</summary>
    public float ReboundDeg { get; init; } = 4f;

    /// <inheritdoc cref="ReboundDeg"/>
    public float ReboundMs { get; init; } = 30f;

    /// <summary>At the retract the jaws unlock to this angle over <see cref="UnlockMs"/>.</summary>
    public float UnlockDeg { get; init; } = 16f;

    /// <inheritdoc cref="UnlockDeg"/>
    public float UnlockMs { get; init; } = 40f;

    // ── THE TETHER AND THE FORCE (ms after the first frame) ──────────────────────────────────────

    /// <summary>The tether fires: its streak from his belt to the trap lives this long.</summary>
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

    /// <summary>
    /// THE RETRACT: the jaws unlock and the trap is reeled back along its chain to the belt. At 185 ms: the jaws hold
    /// ~135 ms after their stop, as they did after the polish pass's 16 ms stop.
    /// </summary>
    public float RetractAtMs { get; init; } = 185f;

    /// <inheritdoc cref="RetractAtMs"/>
    public float RetractMs { get; init; } = 100f;

    /// <summary>The last share of the retract fades the trap and its chain into the belt.</summary>
    public float RetractFadeShare { get; init; } = 0.35f;

    /// <summary>A caught creature that FALLS (on the stop, when the answer killed it): the jaws hold this long, then let go and retract.</summary>
    public float DeathHoldMs { get; init; } = 50f;

    /// <summary>The champion falls on the same bite: after the snap the chain slackens and the trap fades where it is.</summary>
    public float SlackFadeMs { get; init; } = 90f;

    // ── LIGHT (the Source) ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The teeth's glint at the stop: its peak and its life (ms). One brief tooth-edge light (the near jaw's only when it is
    /// not behind the creature), never the whole trap outlined, never white teeth.
    /// </summary>
    public float GlintPeak { get; init; } = 0.6f;

    /// <inheritdoc cref="GlintPeak"/>
    public float GlintMs { get; init; } = 60f;

    /// <summary>
    /// Sparks squeezed out of the PINS as the jaws hit their stop (steel meets steel at the pins, not on the hide):
    /// one per pin, the first <see cref="Sparks"/> of the two; how far (share of the trap's height) and how long.
    /// NONE for JAWS: reviewed at play size with two (the identity pass), a pin spark is a 3-4 px speck that nobody
    /// sees; the jaws' motion, the glint and the clack carry the snap.
    /// </summary>
    public int Sparks { get; init; }

    /// <inheritdoc cref="Sparks"/>
    public float SparkReach { get; init; } = 0.16f;

    /// <inheritdoc cref="Sparks"/>
    public float SparkMs { get; init; } = 70f;

    /// <summary>The tether's Source streak as it fires (brightest at the trap), and its width.</summary>
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
    /// The link ACCENTS, as shares of the chain from the belt (0) to the trap (1): dense at both ends (the hardware at
    /// the belt and at the trap's eye), sparse across the middle. The dark body carries the rest: no gap, and never sixty-four
    /// equally loud stamps.
    /// </summary>
    public IReadOnlyList<float> LinkAt { get; init; } = new[] { 0.03f, 0.08f, 0.14f, 0.24f, 0.38f, 0.52f, 0.66f, 0.78f, 0.87f, 0.93f, 0.97f };

    /// <summary>One link's drawn length (px).</summary>
    public float LinkPx { get; init; } = 15f;

    // ── SOUND AND THE SCREEN'S GENERIC CUES ──────────────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue, most specific first. Played on the first frame: a small latch and tether release at 0, and
    /// its strongest transient, the dry steel CLACK of the jaws hitting their stop, ~50 ms in, on the frame they shut.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_snap", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.46f;

    /// <summary>How wide the snap is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the trap, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for the comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>The caught creature's hit flash for the reflected blow (on the stop): soft, so the silhouette the jaws grip stays readable.</summary>
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

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the trap's size against the recipe's (the size comparison film). 1 otherwise.</summary>
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
