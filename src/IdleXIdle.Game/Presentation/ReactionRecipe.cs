using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION archetype; the FINAL direction of 2026-09-27): the world's answer
/// to an enemy's bite, drawn on its own layer and never on the champion's figure. JAWS is the reference and it is
/// SHADOW FANGS: when the fight resolves the reflected blow, a soft SHADOW MIST gathers round the creature that bit
/// and FOUR LARGE SIMPLE fang shapes emerge from it OPEN (two above it, two below it, clearly outside its silhouette
/// with empty space between them and the body), SNAP inward onto its outer silhouette, HOLD clamped so the eye gets
/// several frames of TARGET BETWEEN SHADOW FANGS, then release; the fangs fade first and the mist loosens and
/// evaporates after them. The mist lies UNDER the creatures (a pool the bitten one stands in), so it never veils it. A reactive-damage phrase of ~200 ms in the family of thorns: no creature to identify, no
/// trap, no mechanism. SHADOW GATHERS ROUND THE ATTACKER, JAWS SNAP ONTO IT, "−X JAWS", THE DARKNESS EVAPORATES.
/// Exactly TWO layers: the mist (supportive) and the fangs (the semantic impact, always the higher contrast).
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
/// no particles, no trails. Only the mist, the fangs, the sound and the delayed number. NO JAWS TARGET FLASH: the fangs ARE the
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

    // ── THE MIST: one soft Shadow pocket per target, two wisps (tools/asset-pipeline/v2/seeker_mist.py) ──

    /// <summary>
    /// The main MIST pocket: near-black soft fog (darker than the creatures' own black) with a very restrained violet
    /// fringe, feathered alpha, a compact core and a long soft fringe, zero at every canvas edge (176 x 120). Drawn in
    /// the normal alpha pass UNDER the figures, centred on the creature's drawn silhouette: a Shadow pool the creature
    /// stands in, so the fog never veils the creature (its eyes and rim stay exactly as they were) and what the player
    /// sees is the fringe gathering round its outline. It is supportive: it keeps the fangs from popping into
    /// existence, makes the four of them one Shadow phenomenon, and gives the phrase a graceful dissolve. Never
    /// additive, never a smoke explosion. (Drawn OVER the creatures the same fog greyed the bitten whelp's eyes by ~40 %,
    /// and a thin veil over them by ~11 %: the placement comparison of 2026-09-27.)
    /// </summary>
    public string MistKey { get; init; } = "fxp_seeker_mist_a";

    /// <summary>A second, looser wisp (144 x 104, a different blob layout), drawn a little offset and turned, also under the figures, so the pocket is not one pasted texture.</summary>
    public string WispKey { get; init; } = "fxp_seeker_mist_b";

    /// <summary>
    /// The mist canvas's size. HEIGHT: a share of the creature's drawn silhouette height (the fangs are placed on that
    /// silhouette's edges, so the pocket follows the body on a boss as on a whelp; <see cref="ReactionRecipes.SizeDial"/>
    /// scales it with the fangs). WIDTH: a share of the NARROWER of the drawn silhouette and the creature's canonical
    /// body: a lunge is drawn on a canvas up to twice the creature's width (ADR-012 decision 9), and the pocket must stay
    /// on the bitten creature, not spread under its neighbour once it is back on its feet. The wisp's dense core spans ~0.47 x 0.38 of its canvas and its
    /// visible fringe ~0.78 x 0.70 (seeker_mist_spans.json): the core covers ~73 % of the body's height, the fringe ~1.35.
    /// </summary>
    public float MistWidthShare { get; init; } = 1.10f;

    /// <inheritdoc cref="MistWidthShare"/>
    public float MistHeightShare { get; init; } = 1.95f;

    /// <summary>
    /// The pocket's centre, raised from the silhouette's centre by this share of its height: onto the torso and the
    /// back, where it wraps the body and reaches the upper fangs, and off the floor between the feet, where near-black
    /// fog beside near-black legs costs the silhouette its separation from the floor.
    /// </summary>
    public float MistRaiseShare { get; init; } = 0.10f;

    /// <summary>
    /// The second wisp's size against the main pocket's, its offset from the pocket's centre (a share of the body box,
    /// in screen space: creatures face LEFT, toward the Seeker, so a negative x is a little FORWARD; negative y is
    /// higher, round the back and the upper fangs, not stacked on the core), and its opacity against the pocket's.
    /// </summary>
    public float WispScale { get; init; } = 0.85f;

    /// <inheritdoc cref="WispScale"/>
    public Vector2 WispOffsetShare { get; init; } = new(-0.06f, -0.14f);

    /// <inheritdoc cref="WispScale"/>
    public float WispOpacity { get; init; } = 0.35f;

    /// <summary>
    /// The mist layer's opacity at its densest, the SNAP. It multiplies the wisps' own feathered alpha (peak 0.72), so
    /// the main pocket's densest texel is ~0.50 on screen and ~0.59 where the second wisp overlaps it: the stone round
    /// the creature stays well above the creature's own black (never one dark blob), and the fangs, drawn over
    /// everything, keep all their contrast.
    /// </summary>
    public float MistPeak { get; init; } = 0.70f;

    /// <summary>A breath of mist on the very first frame, so the fangs never appear on bare floor before the Shadow they come out of.</summary>
    public float MistAtSpawn { get; init; } = 0.06f;

    /// <summary>The mist's materialisation curve (an ease-out power): the layer is ~0.28 at 15 ms, ~0.47 at 30, the peak (0.70) at the snap.</summary>
    public float MistRisePower { get; init; } = 1.3f;

    /// <summary>OPEN, the mist is this much wider and looser (a uniform scale share); it CONTRACTS onto the creature as the fangs close, to 1 at the snap.</summary>
    public float MistOpenSpread { get; init; } = 0.16f;

    /// <summary>After the snap the mist stays COMPRESSED and dense this long (the pressure), then begins to loosen outward.</summary>
    public float MistDenseMs { get; init; } = 35f;

    /// <summary>
    /// How much of its density the mist gives up while it loosens through the rest of the hold (a share of the peak).
    /// From the release it then falls LINEARLY to nothing at <see cref="GoneMs"/>: an even evaporation, never a stall
    /// followed by a collapse. The thinning is large on purpose: the release frames are where the darkened floor beside a
    /// near-black creature costs its silhouette the most, so the fog there is a third lighter than a flat hold would be.
    /// </summary>
    public float MistHoldThinning { get; init; } = 0.40f;

    /// <summary>
    /// From the end of the dense part of the hold to the end of the phrase the mist LOOSENS OUTWARD by this much (a
    /// uniform scale share) along one smooth curve: gently through the rest of the hold, fastest across the release while
    /// the fog is still there to be seen (~10 % more from the release to the last frame that shows it), easing off as it
    /// evaporates. One curve, so the growth never jumps into a puff at the release.
    /// </summary>
    public float MistLoosenGrow { get; init; } = 0.25f;

    /// <summary>Each wisp drifts this far (px) over the phrase, the two in opposite directions, and they turn this far against each other (degrees): enough that the fog is not pasted on, never a boil.</summary>
    public float MistDriftPx { get; init; } = 2f;

    /// <inheritdoc cref="MistDriftPx"/>
    public float MistTurnDegrees { get; init; } = 3f;

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

    /// <summary>
    /// Gone: the MIST, the last of the phrase, has evaporated (ms after the first frame). Nothing remains. The 60 fps
    /// frame at +183 has the fangs already gone and a faint fog left (~0.08 on screen at the pocket's densest): fangs
    /// disappearing, faint mist remaining, mist gone.
    /// </summary>
    public float GoneMs { get; init; } = 200f;

    /// <summary>
    /// The fangs EMERGE from the mist instead of popping on: this opacity on their first frame, <see cref="FangOpacityAtOpen"/>
    /// when the open pose ends, whole at the snap (crisp: never blurred).
    /// </summary>
    public float FangOpacityAtSpawn { get; init; } = 0.30f;

    /// <inheritdoc cref="FangOpacityAtSpawn"/>
    public float FangOpacityAtOpen { get; init; } = 0.70f;

    /// <summary>The fangs stay whole through the hold and fade over this long from the release, FASTER than the mist, so the last frames are faint mist alone.</summary>
    public float FangFadeMs { get; init; } = 45f;

    /// <summary>The release begins (the end of the hold): the fangs retract and fade, the mist expands and evaporates.</summary>
    public float ReleaseAtMs => SnapAtMs + HoldMs;

    /// <summary>Below this opacity a fang is not drawn at all (the frames after the fangs have faded are the mist alone).</summary>
    public const float FangVisibleFloor = 0.01f;

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

    /// <summary>THE SEEKER's JAWS (the REACTION reference): Shadow mist and Shadow fangs, bite for bite.</summary>
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
