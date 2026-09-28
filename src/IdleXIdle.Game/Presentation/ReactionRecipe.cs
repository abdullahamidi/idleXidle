using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION archetype): the world's answer to an enemy's bite, drawn on its
/// own layer and never on the champion's figure. JAWS is the reference and it is SHADOW FANGS MADE OF MIST (the owner,
/// 2026-09-28: "the teeth don't close; and what I meant was that the teeth themselves are like mist: instead of
/// appearing at once, they come like mist, bite, and vanish"). When the fight resolves the reflected blow, four drifts
/// of Shadow mist gather above and below the creature that bit, CONDENSE into four fangs as they come in, and the jaws
/// SNAP SHUT on it: the upper and the lower teeth CLOSE, their points passing each other across the creature's middle,
/// interlocking like a jaw. They hold the bite, then let go and DISSOLVE back into mist. ENEMY HITS SEEKER, SHADOW
/// GATHERS INTO JAWS, THE JAWS BITE IT, "−X JAWS", THE JAWS FALL BACK INTO MIST.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). On screen the answer is SCHEDULED: the cue and a kill's
/// fall land on the SNAP (the jaws shut); the number a few frames after it, so the eye sees the bite before it reads the
/// number.
/// </para>
/// <para>
/// ONE LAYER: the teeth ARE the mist. One source strip (tools/asset-pipeline/v2/seeker_mist_fangs.py) holds a fang in
/// sixteen condensation states, from a loose drift of violet-dark smoke to the condensed tooth with its violet edge and
/// pale point; the runtime plays it forward as the teeth gather and close and backward as they dissolve. No separate
/// fog, no particles, no glint, no trail, no residue, no additive light. NO JAWS TARGET FLASH: the teeth are the hit
/// feedback (the generic F2 for ordinary blows is untouched).
/// </para>
/// <para>
/// WHAT IS GONE FOR GOOD: the spring-loaded bear trap, its chain, its housing, the tether, the yank, the reel-in, the
/// bite glyphs on the champion, the mirrored snap, the swarm, the piranha, and the teeth that stopped on the creature's
/// outline without ever meeting. Their art stays on disk as history and is never played. The champion does nothing;
/// SPRAY, HARD HANDS and the basic swing are never interrupted. REPAY keeps its own legacy presentation (the lookup is
/// by SKILL id). The dock's REARMING / READY state is Core's <c>ReactionArmed</c> report.
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART: one misty fang in sixteen condensation states (tools/asset-pipeline/v2/seeker_mist_fangs.py) ──

    /// <summary>
    /// The strip: <see cref="FangStates"/> states of one fang, left to right, each <see cref="StateWidth"/> x
    /// <see cref="StateHeight"/>, from loose mist (0) to the condensed tooth (last), its point DOWN. ONE state is drawn
    /// per frame (two cross-faded "over" draws are not a blend: the tooth thinned between states and jumped at the snap).
    /// </summary>
    public string FangKey { get; init; } = "fxp_seeker_mistfang";

    /// <inheritdoc cref="FangKey"/>
    public int FangStates { get; init; } = 16;

    /// <inheritdoc cref="FangKey"/>
    public int StateWidth { get; init; } = 144;

    /// <inheritdoc cref="FangKey"/>
    public int StateHeight { get; init; } = 208;

    /// <summary>In each state (texture px): the tooth's POINT, the sprite's origin; it is placed and tilted by its point.</summary>
    public Vector2 TipPoint { get; init; } = new(72f, 178f);

    /// <summary>The condensed tooth's own height in the state (texture px), from its root to its point: the size reference.</summary>
    public float ToothArtHeight { get; init; } = 139f;

    // ── SIZE AND ARRANGEMENT: a jaw of four teeth, the rows interlocking ────────────────────────

    /// <summary>
    /// Each UPPER tooth's height as a share of the bitten creature's drawn silhouette height, clamped; the LOWER teeth
    /// are this share of an upper tooth (a jaw's lower teeth are shorter, and the lower row must stay above the skill dock
    /// at the arena's foot). <see cref="ReactionRecipes.SizeDial"/> scales both for a comparison film.
    /// </summary>
    public float UpperToothShare { get; init; } = 0.40f;

    /// <inheritdoc cref="UpperToothShare"/>
    public float LowerToothScale { get; init; } = 0.80f;

    /// <inheritdoc cref="UpperToothShare"/>
    public float FangMinPx { get; init; } = 24f;

    /// <inheritdoc cref="UpperToothShare"/>
    public float FangMaxPx { get; init; } = 110f;

    /// <summary>
    /// The teeth INTERLOCK like a shut mouth: the upper pair sits this far either side of the body's centre line and the
    /// lower pair this far (each in its own row's tooth heights), so when the jaws shut the upper points close down just
    /// OUTSIDE the lower ones, their facing edges a few px apart, and the lower pair never overlaps itself.
    /// </summary>
    public float UpperSpreadShare { get; init; } = 0.46f;

    /// <inheritdoc cref="UpperSpreadShare"/>
    public float LowerSpreadShare { get; init; } = 0.26f;

    /// <summary>Each tooth's POINT leans INWARD toward the body's centre line by this many degrees (a fang's curve into the bite).</summary>
    public float TiltDegrees { get; init; } = 8f;

    // ── WHERE THE POINTS ARE (shares of the silhouette height: the upper row from its TOP, the lower row from its
    //    BOTTOM, positive inside) ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The open jaw: the upper points this far inside the creature's top, the lower points this far inside its bottom.
    /// The jaw forms ON the creature's body, so the gathering mist stays (on the filmed whelp, by a few px) below the
    /// enemy's health bar above it and above the skill dock below it.
    /// </summary>
    public float UpperOpenShare { get; init; } = 0.20f;

    /// <inheritdoc cref="UpperOpenShare"/>
    public float LowerOpenShare { get; init; } = 0.25f;

    /// <summary>
    /// The jaws SHUT across the creature's LOWER middle: both rows' points reach this share of its height (below the
    /// middle, so the shut upper teeth sit on the body and not over the face and eyes)...
    /// </summary>
    public float MeetShare { get; init; } = 0.60f;

    /// <summary>...and pass each other by this much, so the upper points end below the lower ones: the teeth are CLOSED, interlocked.</summary>
    public float OverlapShare { get; init; } = 0.07f;

    /// <summary>
    /// While the mist gathers it comes IN from the sides: the teeth start this much farther apart (a spread) and this
    /// much larger (a scale), drawing together and tightening into the jaw as they condense.
    /// </summary>
    public float ArriveSpread { get; init; } = 2.40f;

    /// <inheritdoc cref="ArriveSpread"/>
    public float ArriveScale { get; init; } = 1.25f;

    // ── TIMING (ms after the first frame) ──────────────────────────────────────────────────────

    /// <summary>
    /// GATHER: the mist comes in and condenses toward teeth. The condensation EASES IN (to
    /// <see cref="CondenseAtGather"/>), so the loose smoke states are what the eye sees for most of the gathering; the
    /// arrival is an ease-in-out across the whole gathering, so the drift inward is seen, not spent in the first frame.
    /// </summary>
    public float GatherMs { get; init; } = 65f;

    /// <inheritdoc cref="GatherMs"/>
    public float CondenseAtGather { get; init; } = 0.65f;

    /// <summary>The teeth's opacity on the first frame, rising to whole over <see cref="OpacityRampMs"/> along an ease-in-out (~0.3, ~0.6, ~0.9 on the next frames): the mist grows out of nothing, it does not pop in.</summary>
    public float OpacityAtSpawn { get; init; } = 0.10f;

    /// <inheritdoc cref="OpacityAtSpawn"/>
    public float OpacityRampMs { get; init; } = 60f;

    /// <summary>
    /// THE SNAP: from the end of the gathering the jaws CLOSE, accelerating (an ease-in of this power), the teeth
    /// condensing fully, and shut here, interlocked across the creature. The cue's transient and a kill's fall land on
    /// this frame. On a 60 fps screen three frames show the jaws closing, each step larger than the last, before the one
    /// that shows them shut.
    /// </summary>
    public float SnapAtMs { get; init; } = 130f;

    /// <inheritdoc cref="SnapAtMs"/>
    public float CloseEasePower { get; init; } = 2.0f;

    /// <summary>THE BITE: the shut jaws hold on the creature this long, fully condensed.</summary>
    public float HoldMs { get; init; } = 60f;

    /// <summary>The release begins (the end of the bite): the jaws let go and dissolve back into mist.</summary>
    public float ReleaseAtMs => SnapAtMs + HoldMs;

    /// <summary>
    /// DISSOLVE: the teeth LOOSEN back into smoke over the first <see cref="LoosenShare"/> of it (an ease-in-out back
    /// through the strip: four frames of teeth coming apart), while the jaws open (the rows this far apart, px), the smoke
    /// RISES (this far, px) off the creature and spreads (this much larger), fading evenly to nothing at
    /// <see cref="GoneMs"/> (no last-frame cut).
    /// </summary>
    public float ReleaseDriftPx { get; init; } = 12f;

    /// <inheritdoc cref="ReleaseDriftPx"/>
    public float ReleaseRisePx { get; init; } = 26f;

    /// <inheritdoc cref="ReleaseDriftPx"/>
    public float LoosenShare { get; init; } = 0.6f;

    /// <inheritdoc cref="ReleaseDriftPx"/>
    public float ReleaseGrow { get; init; } = 0.45f;

    /// <inheritdoc cref="ReleaseDriftPx"/>
    public float GoneMs { get; init; } = 300f;

    /// <summary>Below this opacity a tooth is not drawn at all.</summary>
    public const float FangVisibleFloor = 0.01f;

    /// <summary>
    /// The reflected number is shown this long AFTER the snap, so the sequence is SNAP, the player sees the bite, then
    /// "−X JAWS" (above the creature, where the screen stacks its numbers). Presentation scheduling only: the fight
    /// resolved the blow at the bite.
    /// </summary>
    public float NumberDelayMs { get; init; } = 40f;

    /// <summary>When the number lands (ms after the first frame).</summary>
    public float AnswerAtMs => SnapAtMs + NumberDelayMs;

    /// <summary>How long the reaction is in the world, in ms after the first frame.</summary>
    public float EndMs => GoneMs;

    // ── SOUND, FLASH AND THE SCREEN'S GENERIC CUES ───────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue: a short dark, dry, sharp Shadow bite / thorn impact, its transient exactly on the SNAP
    /// (<see cref="SnapAtMs"/>, the jaws shut). No secondary ticks, no metal trap, no swarm, no bone crunch.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_fangs", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.42f;

    /// <summary>How wide the cue is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the jaws, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for a comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>
    /// The bitten creature's flash for the reflected blow: NONE for JAWS (0). The Shadow jaws are the reaction's hit
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

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the teeth's size against the recipe's (a size comparison film). 1 otherwise.</summary>
    public static readonly float SizeDial =
        float.TryParse(Environment.GetEnvironmentVariable("RH_REACTION_SIZE"), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 1f;

    /// <summary>THE SEEKER's JAWS (the REACTION reference): Shadow fangs made of mist, bite for bite.</summary>
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
