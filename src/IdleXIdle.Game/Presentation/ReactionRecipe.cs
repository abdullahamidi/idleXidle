using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION archetype; the FINAL direction of 2026-09-27): the world's answer
/// to an enemy's bite, drawn on its own layer and never on the champion's figure. JAWS is the reference and it is
/// SHADOW FANGS: when the fight resolves the reflected blow, FOUR LARGE SIMPLE fang shapes appear OPEN round the
/// creature that bit (two above it, two below it, clearly outside its silhouette with empty space between them and
/// the body), SNAP inward onto its outer silhouette, HOLD clamped so the eye gets several frames of TARGET BETWEEN
/// SHADOW FANGS, then release and fade. A reactive-damage phrase of ~200 ms in the family of thorns: no creature to
/// identify, no trap, no mechanism. ENEMY HITS SEEKER, SHADOW FANGS SNAP ONTO THE ATTACKER, "−X JAWS", GONE.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). On screen the answer is SCHEDULED: the cue and a kill's
/// fall land on the SNAP; the number a few frames after it, so the eye sees the fangs before it reads the number.
/// </para>
/// <para>
/// WHY SHAPES (the owner, 2026-09-27): a tiny creature carrying a head, an eye, a mouth, teeth and a tail is below the
/// useful perceptual budget at gameplay scale; at true speed it becomes coloured motion. Four strong triangles are
/// cheap to read. So: no piranha body, no eyes, no tails, no separate head, no glint, no residue, no secondary bites,
/// no particles, no trails. Only the fangs, the sound and the delayed number. NO JAWS TARGET FLASH: the fangs ARE the
/// hit feedback, and a white flash under them was the largest source of competition (the generic F2 for ordinary
/// blows is untouched).
/// </para>
/// <para>
/// WHAT IS GONE FOR GOOD: the spring-loaded bear trap, its chain, its housing, the tether, the yank, the reel-in, the
/// bite glyphs on the champion, the mirrored snap, the swarm, the piranha. Their art stays on disk as history and is
/// never played. The champion does nothing; SPRAY, HARD HANDS and the basic swing are never interrupted. REPAY keeps
/// its own legacy presentation (the lookup is by SKILL id). The dock's REARMING / READY state is Core's
/// <c>ReactionArmed</c> report.
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART: one hand-authored fang (tools/asset-pipeline/v2/seeker_fangs.py), drawn four times ──

    /// <summary>ONE fang: a broad, slightly curved triangle, dark Shadow body, strong violet edge, a small pale tip; its point DOWN, on a 40 x 72 canvas.</summary>
    public string FangKey { get; init; } = "fxp_seeker_fang";

    /// <summary>In the canvas (texture px): the fang's POINT, the sprite's origin; it is placed and tilted by its point.</summary>
    public Vector2 TipPoint { get; init; } = new(22f, 71f);

    /// <summary>The canvas size (texture px).</summary>
    public float ArtWidth { get; init; } = 40f;

    /// <inheritdoc cref="ArtWidth"/>
    public float ArtHeight { get; init; } = 72f;

    // ── SIZE: large, simple geometry ─────────────────────────────────────────────────────────────

    /// <summary>
    /// EACH fang's height as a share of the bitten creature's visible height, clamped. The upper and lower fangs
    /// together are ~60 % of the body: a meaningful portion of the enemy, much larger than the piranha head was.
    /// <see cref="ReactionRecipes.SizeDial"/> scales it for a comparison film.
    /// </summary>
    public float FangHeightShare { get; init; } = 0.34f;

    /// <inheritdoc cref="FangHeightShare"/>
    public float FangMinPx { get; init; } = 28f;

    /// <inheritdoc cref="FangHeightShare"/>
    public float FangMaxPx { get; init; } = 72f;

    /// <summary>The two fangs of a pair sit this far either side of the body's centre line, as a share of a fang's height.</summary>
    public float PairSpreadShare { get; init; } = 0.55f;

    /// <summary>Each fang leans INWARD toward the body's centre by this many degrees (the outer fang of a pair mirrors it).</summary>
    public float TiltDegrees { get; init; } = 12f;

    // ── PLACEMENT: outside the silhouette when OPEN, on its outer edge when SNAPPED ──────────────

    /// <summary>
    /// OPEN: the points sit this far OUTSIDE the creature's top and bottom edges (a share of its visible height), so
    /// there is visible empty space between the upper fangs, the body, and the lower fangs. The first thing the eye
    /// sees is something surrounding the creature; nothing begins inside it.
    /// </summary>
    public float OpenGapShare { get; init; } = 0.12f;

    /// <summary>
    /// SNAPPED: the points end this far INSIDE the creature's top and bottom edges (a share of its visible height):
    /// they bit its outer silhouette, they did not cross through it. The body stays visibly between the fangs.
    /// </summary>
    public float BiteDepthShare { get; init; } = 0.16f;

    // ── TIMING (ms after the first frame; the phrase is one readable sentence) ───────────────────

    /// <summary>The OPEN pose stands this long before the close begins (the frame that says "something surrounds it").</summary>
    public float OpenMs { get; init; } = 25f;

    /// <summary>THE SNAP: the points arrive on the silhouette here. The cue's transient and a kill's fall land on this frame.</summary>
    public float SnapAtMs { get; init; } = 55f;

    /// <summary>THE HOLD: the snapped shape stays this long. This is where the readability comes from: several frames of TARGET BETWEEN SHADOW FANGS.</summary>
    public float HoldMs { get; init; } = 85f;

    /// <summary>The release: the fangs retract outward this far (px) over this long, fading as they go.</summary>
    public float ReleasePx { get; init; } = 6f;

    /// <inheritdoc cref="ReleasePx"/>
    public float ReleaseMs { get; init; } = 35f;

    /// <summary>Gone: the fangs have faded out (ms after the first frame). Nothing remains.</summary>
    public float GoneMs { get; init; } = 200f;

    /// <summary>
    /// The reflected number is shown this long AFTER the snap, so the sequence is SNAP, the player sees the fangs,
    /// then "−X JAWS" (above the creature, where the screen stacks its numbers, clear of the fangs). Presentation
    /// scheduling only: the fight resolved the blow at the bite.
    /// </summary>
    public float NumberDelayMs { get; init; } = 40f;

    /// <summary>When the number lands (ms after the first frame).</summary>
    public float AnswerAtMs => SnapAtMs + NumberDelayMs;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => GoneMs;

    // ── SOUND, FLASH AND THE SCREEN'S GENERIC CUES ───────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue: a short dark, dry, sharp Shadow bite / thorn impact, its transient exactly on the SNAP
    /// (<see cref="SnapAtMs"/>). No secondary ticks, no metal trap, no swarm, no bone crunch.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_fangs", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.42f;

    /// <summary>How wide the cue is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the fangs, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for a comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>
    /// The bitten creature's flash for the reflected blow: NONE for JAWS (0). The Shadow fangs are the reaction's hit
    /// feedback; a grey/white flash under them competed with the shape. The generic F2 for ordinary blows is untouched.
    /// </summary>
    public float TargetFlash { get; init; }

    /// <inheritdoc cref="TargetFlash"/>
    public float TargetFlashMs { get; init; } = 80f;

    /// <summary>The Seeker's anchor for the overlay that names his side of the exchange, his BELT (a share of his visible body).</summary>
    public Vector2 ChampionAnchor { get; init; } = new(0.60f, 0.56f);
}

/// <summary>The reaction recipes, looked up by the SKILL (never by its Form's shared clip or effect).</summary>
public static class ReactionRecipes
{
    /// <summary>RH_REACTION_CALLOUT=1: the authored reaction still says its callout (the with/without comparison).</summary>
    public static readonly bool CalloutOverride =
        Environment.GetEnvironmentVariable("RH_REACTION_CALLOUT") is "1" or "true";

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the fangs' size against the recipe's (a size comparison film). 1 otherwise.</summary>
    public static readonly float SizeDial =
        float.TryParse(Environment.GetEnvironmentVariable("RH_REACTION_SIZE"), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 1f;

    /// <summary>THE SEEKER's JAWS (the REACTION reference): Shadow fangs, bite for bite.</summary>
    public static readonly ReactionRecipe SeekerJaws = new() { Id = "seeker.jaws" };

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
