using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION archetype; the production direction of 2026-09-27): the world's
/// answer to an enemy's bite, drawn on its own layer and never on the champion's figure. JAWS is the reference and it
/// is ONE SHADOW PIRANHA: when the fight resolves the reflected blow, a single small Shadow jaw-head appears in the
/// empty space in front of the creature that bit, moves in with its mouth open, closes on the creature's front edge,
/// holds shut so the eye can register the bite, and fades. A stylised reactive-damage phrase of ~200 ms in the family
/// of thorns-style retaliation: not a summoned pet, not a persistent creature, not a projectile, and not a mechanism
/// the player has to understand. ENEMY HITS SEEKER, A SHADOW PIRANHA COMES IN AND BITES THE ATTACKER, "−X JAWS", GONE.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). The answer (the reflected number, the creature's flash,
/// a kill's fall, the chomp cue) lands on the piranha's CLOSED frame; the fight resolved it at the bite (t 0).
/// </para>
/// <para>
/// VISUAL SILENCE (the owner, 2026-09-27, after the three-jaw hero chomp still read as "purple activity"): ONE object,
/// nothing else in its region. No secondary bites, no residue smear, no glint, no particles, no behind-the-target
/// layer. The piranha is STAGED IN NEGATIVE SPACE: it spawns clearly outside the creature's silhouette, on the side
/// that faces the Seeker, so its whole open head is seen against the arena for a frame or two before it overlaps the
/// target; it closes on the creature's outer front edge with its rear half still outside the body, so its silhouette
/// survives the bite; the target's flash is reduced for JAWS so it never erases the head. Flavour may be added back
/// only after the one object is accepted.
/// </para>
/// <para>
/// WHAT IS GONE FOR GOOD: the spring-loaded bear trap, its chain, its housing, the tether, the yank, the reel-in, the
/// bite glyphs on the champion, the mirrored snap, the swarm. Their art stays on disk as history and is never played.
/// The champion does nothing; SPRAY, HARD HANDS and the basic swing are never interrupted. REPAY keeps its own legacy
/// presentation (the lookup is by SKILL id). The dock's REARMING / READY state is Core's <c>ReactionArmed</c> report.
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART: one tiny source sprite in three states (tools/asset-pipeline/v2/seeker_piranha.py) ──

    /// <summary>The jaw-head with its mouth OPEN, facing LEFT (the mouth at the left edge), on a 64 x 40 canvas.</summary>
    public string OpenKey { get; init; } = "fxp_seeker_jaws_open";

    /// <summary>The same head with its mouth HALF closed (a deterministic mid-shape of the two), the same canvas and facing.</summary>
    public string HalfKey { get; init; } = "fxp_seeker_jaws_half";

    /// <summary>The same head with its mouth SHUT (the teeth met), the same canvas and facing.</summary>
    public string ShutKey { get; init; } = "fxp_seeker_jaws_shut";

    /// <summary>In the canvas (texture px): the mouth's bite point, the sprite's origin; the head turns about it.</summary>
    public Vector2 MouthPoint { get; init; } = new(7f, 24f);

    /// <summary>The canvas HEIGHT (the head fills ~31 of its 40 rows) and the head's visible WIDTH in the canvas (~49 of 64 columns).</summary>
    public float ArtHeight { get; init; } = 40f;

    /// <inheritdoc cref="ArtHeight"/>
    public float ArtHeadWidth { get; init; } = 49f;

    // ── SIZE ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The piranha's height as a share of the bitten creature's visible height, clamped: it bites PART of the body,
    /// never a mouth around it. <see cref="ReactionRecipes.SizeDial"/> scales it for a comparison film.
    /// </summary>
    public float JawBodyShare { get; init; } = 0.35f;

    /// <inheritdoc cref="JawBodyShare"/>
    public float JawMinPx { get; init; } = 26f;

    /// <inheritdoc cref="JawBodyShare"/>
    public float JawMaxPx { get; init; } = 60f;

    // ── PLACEMENT: the creature's outer FRONT edge, the rear half of the head outside the body ────

    /// <summary>
    /// WHERE it bites, as a share of the creature's visible body: x from the edge that faces the champion (just inside
    /// it, so the mouth overlaps the edge), y from the top (the upper-front). The silhouette probe refines x to the
    /// body's front-most opaque column when it can read it.
    /// </summary>
    public Vector2 BiteShare { get; init; } = new(0.06f, 0.40f);

    /// <summary>FROM which direction it arrives: a unit vector toward the bite point. From the Seeker's side (the left), a little from above.</summary>
    public Vector2 From { get; init; } = Vector2.Normalize(new Vector2(1f, 0.18f));

    /// <summary>The rows of the silhouette searched for the body's front-most point, which places the bite's x.</summary>
    public float ProbeFrom { get; init; } = 0.30f;

    /// <inheritdoc cref="ProbeFrom"/>
    public float ProbeTo { get; init; } = 0.60f;

    /// <summary>How far inside the silhouette's front edge the mouth closes (share of the body's width).</summary>
    public float ProbeInset { get; init; } = 0.04f;

    // ── STAGING AND TIMING (ms after the first frame; the phrase is one readable sentence) ─────────

    /// <summary>
    /// The piranha spawns this far outside the bite point, as a share of its OWN visible width (the head's, at its
    /// drawn size): in the empty space in front of the creature, its whole head against the arena for a frame or two.
    /// Only enough distance to establish the silhouette, never a trip across the arena.
    /// </summary>
    public float SpawnWidthShare { get; init; } = 0.75f;

    /// <summary>The mouth's HALF state begins at this share of the close (OPEN, HALF, SHUT: three display states).</summary>
    public float HalfAtShare { get; init; } = 0.70f;

    /// <summary>The MAIN CHOMP: the head arrives on the bite point and its mouth SHUTS here; the answer lands on this frame.</summary>
    public float ChompAtMs { get; init; } = 65f;

    /// <summary>The CHOMP's one-frame squash.</summary>
    public float SquashMs { get; init; } = 16f;

    /// <summary>The CLOSED HOLD: the shut head stays on the creature this long (the semantic pose, "the piranha is biting it").</summary>
    public float HoldMs { get; init; } = 65f;

    /// <summary>The exit: a small recoil away from the target, this far (px) over this long, fading as it goes.</summary>
    public float RecoilPx { get; init; } = 6f;

    /// <inheritdoc cref="RecoilPx"/>
    public float RecoilMs { get; init; } = 40f;

    /// <summary>Gone: the head has faded out (ms after the first frame). Nothing remains.</summary>
    public float GoneMs { get; init; } = 200f;

    /// <summary>The answer lands on the CLOSED frame: the number, the flash, the chomp cue's transient.</summary>
    public float AnswerAtMs => ChompAtMs;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => GoneMs;

    // ── SOUND, FLASH AND THE SCREEN'S GENERIC CUES ───────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue: a small supernatural CHOMP, one bite, no ticks. Played on the CLOSED frame
    /// (<see cref="AnswerAtMs"/>), so the ear hears the bite when the mouth closes.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_chomp", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.40f;

    /// <summary>How wide the cue is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the piranha, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for a comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>
    /// The bitten creature's flash for the reflected blow, a JAWS-specific override of the generic F2 (0.45 / 80 ms):
    /// REDUCED so the flash never erases the closed piranha on the very frame it lands (the two peaked together and the
    /// head vanished into the white). The generic default is untouched. <see cref="ReactionRecipes.FlashDial"/> films
    /// the alternatives (the full F2 a frame later, or none).
    /// </summary>
    public float TargetFlash { get; init; } = 0.28f;

    /// <inheritdoc cref="TargetFlash"/>
    public float TargetFlashMs { get; init; } = 80f;

    /// <summary>The flash begins this many ms after the chomp (0: on its frame; ~17: the rendered frame after).</summary>
    public float TargetFlashDelayMs { get; init; }

    /// <summary>The Seeker's anchor for the overlay that names his side of the exchange, his BELT (a share of his visible body).</summary>
    public Vector2 ChampionAnchor { get; init; } = new(0.60f, 0.56f);
}

/// <summary>The reaction recipes, looked up by the SKILL (never by its Form's shared clip or effect).</summary>
public static class ReactionRecipes
{
    /// <summary>RH_REACTION_CALLOUT=1: the authored reaction still says its callout (the with/without comparison).</summary>
    public static readonly bool CalloutOverride =
        Environment.GetEnvironmentVariable("RH_REACTION_CALLOUT") is "1" or "true";

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the piranha's size against the recipe's (a size comparison film). 1 otherwise.</summary>
    public static readonly float SizeDial =
        float.TryParse(Environment.GetEnvironmentVariable("RH_REACTION_SIZE"), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 1f;

    /// <summary>
    /// RH_REACTION_FLASH=A|B|OFF: the target-flash comparison the readability pass films. B (the build's default): the
    /// reduced JAWS flash on the chomp's frame. A: the full generic F2 (0.45) starting one rendered frame AFTER the chomp.
    /// OFF: no flash at all (the control). Null when not set.
    /// </summary>
    public static readonly string? FlashDial = Environment.GetEnvironmentVariable("RH_REACTION_FLASH")?.Trim().ToUpperInvariant() is { Length: > 0 } f ? f : null;

    /// <summary>THE SEEKER's JAWS (the REACTION reference): one Shadow piranha, bite for bite.</summary>
    public static readonly ReactionRecipe SeekerJaws = FlashDial switch
    {
        "A" => new() { Id = "seeker.jaws", TargetFlash = 0.45f, TargetFlashDelayMs = 17f },
        "OFF" => new() { Id = "seeker.jaws", TargetFlash = 0f },
        _ => new() { Id = "seeker.jaws" },
    };

    // THE LOOKUP (the accepted contract: skill-specific, deliberately shared, legacy). A reaction's presentation
    // belongs to its SKILL: JAWS and REPAY share the Snare Form's "trap" clip and effect, and REPAY (an Active) must
    // keep its own. So there is no shared tier for reactions: a skill with no entry here keeps its legacy presentation.
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
