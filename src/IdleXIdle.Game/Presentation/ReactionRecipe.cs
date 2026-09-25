using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION / TRAP archetype): the world's answer to an enemy's bite, drawn
/// on its own layer and never on the champion's figure. JAWS is the reference: the bite springs an iron trap on the
/// creature that bit, its chain snaps taut back to the Seeker's belt, the creature is yanked back, and it is gone.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything here is measured from the fight's own contact: the bite's millisecond, on which Core also
/// resolves the reaction and its answer. The phases, in ms after that contact: the SNAP (open to shut), the TENSION
/// (the chain from slack to taut, then a small decaying shiver), the RECOIL (the target pushed a few pixels away and
/// back), and the RELEASE (the chain slackens, the jaws loosen, drop and fade). Nothing stays in the world.
/// </para>
/// <para>
/// MATERIAL vs SOURCE (the SPRAY and HARD HANDS hierarchy): the jaws and the chain are drawn untinted, as iron; the
/// Source colour is only light: the tooth-edge glint, the snap flash, the sparks and a restrained tension accent.
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The open jaws (material).</summary>
    public string JawsOpenKey { get; init; } = "prop_seeker_jaws_open";

    /// <summary>The shut jaws (material): the same object, closed into a toothed ring.</summary>
    public string JawsShutKey { get; init; } = "prop_seeker_jaws_shut";

    /// <summary>The shut teeth, white: the emissive mask the Source glint is drawn through.</summary>
    public string JawsEdgeKey { get; init; } = "prop_seeker_jaws_shut_edge";

    /// <summary>One chain link in two cells (face-on, edge-on), side by side.</summary>
    public string ChainLinkKey { get; init; } = "prop_seeker_chain_link";

    /// <summary>The contact flash, the sparks and the tension accent (shared parts, white).</summary>
    public string FlashKey { get; init; } = "fxp_flash_soft";

    /// <inheritdoc cref="FlashKey"/>
    public string SparkKey { get; init; } = "fxp_spark_dot";

    /// <inheritdoc cref="FlashKey"/>
    public string AccentKey { get; init; } = "fxp_trail_soft";

    /// <summary>
    /// In the jaws' texture (px): the centre of the shut ring, which is placed ON the clamp point, and the chain's eye
    /// (the rings under the hinge). Both states share one framing, so one pivot serves both.
    /// </summary>
    public Vector2 JawsPivot { get; init; } = new(64f, 52f);

    /// <inheritdoc cref="JawsPivot"/>
    public Vector2 ChainEye { get; init; } = new(64f, 114f);

    /// <summary>Where the teeth meet when shut (texture px): the snap's flash and its sparks leave from here.</summary>
    public Vector2 BiteLine { get; init; } = new(64f, 16f);

    /// <summary>The jaws' drawn height in texture px (their opaque rows), which the on-screen size is a share of.</summary>
    public float JawsArtHeight { get; init; } = 107f;

    // ── SIZE AND PLACE ───────────────────────────────────────────────────────────────────────────

    /// <summary>The jaws' height as a share of the caught creature's visible height, clamped to a legible range.</summary>
    public float JawsBodyShare { get; init; } = 0.48f;

    /// <inheritdoc cref="JawsBodyShare"/>
    public float JawsMinPx { get; init; } = 56f;

    /// <inheritdoc cref="JawsBodyShare"/>
    public float JawsMaxPx { get; init; } = 130f;

    /// <summary>
    /// The clamp point when the silhouette cannot be read, as a share of the creature's visible body: x from the edge
    /// that faces the champion, y from its top. The front-lower part of the body, where a lunge plants itself.
    /// </summary>
    public Vector2 ClampFallback { get; init; } = new(0.20f, 0.72f);

    /// <summary>The rows of the silhouette searched for its front-most point (shares of the opaque height).</summary>
    public float ProbeFrom { get; init; } = 0.58f;

    /// <inheritdoc cref="ProbeFrom"/>
    public float ProbeTo { get; init; } = 0.86f;

    /// <summary>How far inside the silhouette's front edge the jaws close (share of the body's width): on it, not beside it.</summary>
    public float ProbeInset { get; init; } = 0.06f;

    /// <summary>The Seeker's trap anchor, his BELT: a share of his visible body (x from his back, he faces right).</summary>
    public Vector2 ChampionAnchor { get; init; } = new(0.60f, 0.56f);

    // ── TIMING (ms after the contact) ────────────────────────────────────────────────────────────

    /// <summary>OPEN to SHUT: the highest-energy instant. The jaws rise from below the clamp point and close.</summary>
    public float SnapMs { get; init; } = 55f;

    /// <summary>The share of the snap after which the shut jaws are drawn (the rest is the open jaws rising).</summary>
    public float ShutAt { get; init; } = 0.70f;

    /// <summary>The shut jaws' overshoot at the snap (a scale), settling over <see cref="SettleMs"/>.</summary>
    public float SnapOvershoot { get; init; } = 1.12f;

    /// <inheritdoc cref="SnapOvershoot"/>
    public float SettleMs { get; init; } = 70f;

    /// <summary>How far below the clamp point the open jaws start (share of their height).</summary>
    public float RiseFrom { get; init; } = 0.35f;

    /// <summary>The chain goes from slack to taut over this long (ease-out), then shivers and settles.</summary>
    public float TautMs { get; init; } = 95f;

    /// <summary>The slack chain's sag at its middle, as a share of its span (clamped to <see cref="SagMaxPx"/>).</summary>
    public float SlackSag { get; init; } = 0.16f;

    /// <inheritdoc cref="SlackSag"/>
    public float SagMaxPx { get; init; } = 70f;

    /// <summary>The taut chain's shiver: amplitude (px), frequency (Hz) and decay (ms).</summary>
    public float ShiverPx { get; init; } = 5f;

    /// <inheritdoc cref="ShiverPx"/>
    public float ShiverHz { get; init; } = 22f;

    /// <inheritdoc cref="ShiverPx"/>
    public float ShiverDecayMs { get; init; } = 60f;

    /// <summary>How long the chain takes to whip out from the jaws to the belt at the start (ms).</summary>
    public float WhipMs { get; init; } = 45f;

    /// <summary>The recoil: from the snap, over this long, peaking at <see cref="RecoilPeakAt"/> of it.</summary>
    public float RecoilMs { get; init; } = 190f;

    /// <inheritdoc cref="RecoilMs"/>
    public float RecoilPeakAt { get; init; } = 0.30f;

    /// <summary>How far the creature is pushed (share of its visible width, clamped): a few pixels, enough to sell force.</summary>
    public float RecoilShare { get; init; } = 0.07f;

    /// <inheritdoc cref="RecoilShare"/>
    public float RecoilMinPx { get; init; } = 5f;

    /// <inheritdoc cref="RecoilShare"/>
    public float RecoilMaxPx { get; init; } = 16f;

    /// <summary>The release begins here (ms after the contact) and takes <see cref="ReleaseMs"/>: the whole presence.</summary>
    public float ReleaseAtMs { get; init; } = 215f;

    /// <inheritdoc cref="ReleaseAtMs"/>
    public float ReleaseMs { get; init; } = 110f;

    /// <summary>A caught creature that FALLS: the release starts this soon after the snap (death is the stronger state).</summary>
    public float DeathReleaseAfterSnapMs { get; init; } = 20f;

    // ── LIGHT (the Source) ───────────────────────────────────────────────────────────────────────

    /// <summary>The snap flash: size (share of the jaws' height), life (ms), and how white it is (0 = Source).</summary>
    public float FlashSize { get; init; } = 0.55f;

    /// <inheritdoc cref="FlashSize"/>
    public float FlashMs { get; init; } = 60f;

    /// <inheritdoc cref="FlashSize"/>
    public float FlashWhite { get; init; } = 0.65f;

    /// <summary>The tooth-edge glint: its peak and how long it takes to die after the snap (ms).</summary>
    public float GlintPeak { get; init; } = 0.85f;

    /// <inheritdoc cref="GlintPeak"/>
    public float GlintMs { get; init; } = 160f;

    /// <summary>Sparks thrown off the bite line: how many, how far (share of the jaws' height), how long (ms).</summary>
    public int Sparks { get; init; } = 5;

    /// <inheritdoc cref="Sparks"/>
    public float SparkReach { get; init; } = 0.75f;

    /// <inheritdoc cref="Sparks"/>
    public float SparkMs { get; init; } = 150f;

    /// <summary>The tension accent along the chain: its peak brightness (a restrained line, never a laser) and width.</summary>
    public float AccentPeak { get; init; } = 0.30f;

    /// <inheritdoc cref="AccentPeak"/>
    public float AccentPx { get; init; } = 3f;

    /// <summary>While the champion performs an action, the reaction's light plays at this share: it stays secondary.</summary>
    public float LightUnderAction { get; init; } = 0.55f;

    // ── THE CHAIN ────────────────────────────────────────────────────────────────────────────────

    /// <summary>One link's drawn length (px) and how much the next overlaps it.</summary>
    public float LinkPx { get; init; } = 16f;

    /// <inheritdoc cref="LinkPx"/>
    public float LinkOverlap { get; init; } = 0.15f;

    /// <summary>The most links a chain draws; a longer chain draws bigger links (never gaps).</summary>
    public int MaxLinks { get; init; } = 64;

    // ── SOUND AND THE SCREEN'S GENERIC CUES ──────────────────────────────────────────────────────

    /// <summary>The snap, most specific first: one authored cue for the whole reaction phrase (BITE → CLACK).</summary>
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

    /// <summary>THE SEEKER's JAWS (the REACTION / TRAP reference, 2026-09-25): bite for bite.</summary>
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
