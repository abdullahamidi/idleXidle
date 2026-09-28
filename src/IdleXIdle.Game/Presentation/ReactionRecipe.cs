using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION archetype): the world's answer to an enemy's bite, drawn on its
/// own layer and never on the champion's figure. JAWS is the reference and it is a SHADOW MAW MADE OF MIST (the owner,
/// 2026-09-28: "not four triangles coming to the middle: a jaw that really opens and closes, a piranha jaw, a bite";
/// before that: "the teeth themselves should be like mist: they come like mist, bite, and vanish"). When the fight
/// resolves the reflected blow, loose Shadow smoke gathers in front of the creature that bit, from the Seeker's side,
/// and forms a nearly shut jaw: an upper and a lower jaw, each a tapering beak lined with sharp teeth, hinged at the
/// back corner of the mouth. It OPENS WIDE around the creature's head and holds it, SNAPS SHUT across its throat (the teeth interlocking,
/// biting past their rest for two frames), CLENCHES (a jolt into the creature and a swell), holds the bite tightening
/// slowly, then lets go and comes apart back into rising smoke.
/// ENEMY HITS SEEKER, A SHADOW MAW BITES IT, "−X JAWS", THE MAW FALLS BACK INTO MIST.
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
/// ONE LAYER: the jaw IS the mist. One source strip (tools/asset-pipeline/v2/seeker_maw.py) holds both jaw pieces in
/// sixteen condensation states, from a loose drift of violet smoke to the hardened jaw (a darkening Shadow veil with
/// smoke moving inside it, a soft lavender rim, smoky lavender teeth); the runtime climbs it as the maw gathers and
/// closes (the hardened states only on the bite) and plays it back down as it dissolves.
/// No separate fog, no particles, no glint, no trail, no residue, no additive light. NO JAWS TARGET FLASH: the maw is
/// the hit feedback (the generic F2 for ordinary blows is untouched).
/// </para>
/// <para>
/// WHAT IS GONE FOR GOOD: the spring-loaded bear trap, its chain, its housing, the tether, the yank, the reel-in, the
/// bite glyphs on the champion, the mirrored snap, the swarm, the whole piranha, the teeth that stopped on the creature's
/// outline without ever meeting, and the four misty teeth that closed like triangles rather than a jaw. Their art stays on disk as history and is never played. The champion does nothing;
/// SPRAY, HARD HANDS and the basic swing are never interrupted. REPAY keeps its own legacy presentation (the lookup is
/// by SKILL id). The dock's REARMING / READY state is Core's <c>ReactionArmed</c> report.
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART: a Shadow maw made of mist, two jaw pieces in sixteen condensation states (tools/asset-pipeline/v2/seeker_maw.py) ──

    /// <summary>
    /// The strip: <see cref="MawStates"/> states side by side, each cell <see cref="PieceWidth"/> x <see cref="PieceHeight"/>,
    /// the UPPER jaw in the top row and the LOWER jaw in the bottom row, from loose mist (0) to the condensed jaw (last).
    /// Each jaw is a tapering beak with a row of sharp teeth on its biting edge; the lower one juts a little past the upper
    /// and its teeth sit half a tooth along, so the rows interlock when shut. ONE state per piece is drawn per frame.
    /// </summary>
    public string MawKey { get; init; } = "fxp_seeker_maw";

    /// <inheritdoc cref="MawKey"/>
    public int MawStates { get; init; } = 16;

    /// <inheritdoc cref="MawKey"/>
    public int PieceWidth { get; init; } = 320;

    /// <inheritdoc cref="MawKey"/>
    public int PieceHeight { get; init; } = 176;

    /// <summary>In each piece's cell (texture px): the HINGE, the back corner of the mouth, where the biting edge starts; both pieces turn about it.</summary>
    public Vector2 Hinge { get; init; } = new(44f, 104f);

    /// <summary>The upper jaw's own length in the art (texture px), hinge to snout: the size reference.</summary>
    public float JawArtLength { get; init; } = 232f;

    // ── SIZE AND PLACE: the maw bites the HEAD of the creature that bit ───────────────────────────

    /// <summary>
    /// The jaw's length as a share of the creature's CANONICAL body width (not its drawn silhouette: a lunge is drawn on
    /// a canvas far wider than the creature), clamped. Big enough to read as a jaw at play speed, small enough to bite
    /// ONE creature and not the pack. <see cref="ReactionRecipes.SizeDial"/> scales it for a film.
    /// </summary>
    public float MawLengthShare { get; init; } = 0.58f;

    /// <inheritdoc cref="MawLengthShare"/>
    public float MawMinPx { get; init; } = 70f;

    /// <inheritdoc cref="MawLengthShare"/>
    public float MawMaxPx { get; init; } = 280f;

    /// <summary>
    /// The hinge sits this far (a share of the jaw's length) in FRONT of the creature's canonical body, toward the Seeker
    /// (whose side the maw comes from): about 70 % of the jaw lies over the creature, across its head and neck.
    /// </summary>
    public float HingeLeadShare { get; init; } = 0.30f;

    /// <summary>
    /// When one answer bites SEVERAL creatures, every maw but the FRONT one (the creature nearest the Seeker) is hinged this
    /// far in front of its own creature (none: at the creature's front edge), forms in place (no arrival from the Seeker's
    /// side) and is this much smaller, so it bites its own creature and never reaches over the one in front of it (a full
    /// lead put the rear maw across the front creature's head).
    /// </summary>
    public float SecondaryLeadShare { get; init; } = 0.0f;

    /// <inheritdoc cref="SecondaryLeadShare"/>
    public float SecondaryScale { get; init; } = 0.85f;

    /// <summary>
    /// The BITE LINE, a share of the creature's drawn height from its top: the shut seam crosses the creature here, where
    /// the teeth close: the THROAT, under the head, so the shut teeth read as a jaw holding the creature (shut just under
    /// its eyes, the band of teeth read as the creature's own grin; over its eyes, the face was lost), not at the hinge (a
    /// line set at the hinge and tilted down dropped the bite onto the chest). Read off the silhouette pinned from the pose
    /// the creature bit in (its drawn extent, not its canvas).
    /// </summary>
    public float BiteLineShare { get; init; } = 0.60f;

    /// <summary>
    /// Where along the jaw (a share of its length from the hinge) the teeth close: the shut seam crosses the bite line
    /// here. The tests hold it to the art's own tooth row (seeker_maw_spans.json).
    /// </summary>
    public float BiteSeamShare { get; init; } = 0.65f;

    /// <summary>The whole maw leans SNOUT-DOWN by this many degrees, so the shut seam crosses the creature on a slight diagonal.</summary>
    public float MawTiltDegrees { get; init; } = 4f;

    // ── THE GAPE (degrees; screen: + is clockwise; the upper jaw opens UP (negative), the lower jaw DOWN (positive)) ──

    /// <summary>The mist first condenses into a nearly SHUT jaw...</summary>
    public Vector2 GatherGape { get; init; } = new(-9f, 9f);

    /// <summary>
    /// ...which then visibly OPENS WIDE (its own motion, the rear before the bite) and HOLDS it: the upper jaw rises clear
    /// of the creature's eyes, so the creature's face sits INSIDE the open mouth (with the upper teeth across its eyes,
    /// the open jaw read as the creature's own grin), and the upper jaw has as far to sweep down as the lower has to rise...
    /// </summary>
    public Vector2 WideGape { get; init; } = new(-45f, 30f);

    /// <summary>
    /// ...snaps shut PAST its rest, held there two frames: as tight as the jaw goes, the teeth's points just reaching the
    /// other jaw's gum line (never further: shut all the way, each row's teeth passed through the other's gums and the
    /// rows looked inside-out)...
    /// </summary>
    public Vector2 Overshoot { get; init; } = new(-8.5f, 8.5f);

    /// <summary>
    /// ...settles to REST: a slightly open mouth, so the two jaws stay two distinct wedges (a dark mouth line even at the
    /// back) with the bitten creature showing between them while their teeth interlock across it about half their length
    /// (shut to a line, the jaws read as a zipper)...
    /// </summary>
    public Vector2 RestGape { get; init; } = new(-10.5f, 10.5f);

    /// <summary>...tightens slowly while it holds the bite (never a frozen pose)...</summary>
    public Vector2 TightGape { get; init; } = new(-9.5f, 9.5f);

    /// <summary>...and lets go to this as it dissolves.</summary>
    public Vector2 ReleaseGape { get; init; } = new(-22f, 24f);

    // ── TIMING (ms after the first frame; the owner allowed it longer, so the bite has its build-up). The visible
    // opening waits for the biting creature's own lunge drawing to end (~120-150 ms after its contact): opened over it,
    // violet on violet, the opening was never seen ──

    /// <summary>
    /// GATHER: the maw grows out of loose smoke (condensing only to <see cref="CondenseAtGather"/>, so smoke is what is
    /// seen), comes IN from the Seeker's side (<see cref="ArrivePx"/>, <see cref="ArriveScale"/>) as a nearly shut jaw.
    /// </summary>
    public float GatherMs { get; init; } = 83f;

    /// <inheritdoc cref="GatherMs"/>
    public float CondenseAtGather { get; init; } = 0.50f;

    /// <summary>
    /// FORMED: the nearly shut jaw waits, condensing a little more (to <see cref="CondenseFormed"/>), until here, and only
    /// then opens: the biting creature's own lunge drawing is up until ~120-150 ms after its contact, and a jaw opened
    /// over it (violet on violet) faded in already wide instead of being SEEN to open.
    /// </summary>
    public float OpenFromMs { get; init; } = 133f;

    /// <inheritdoc cref="OpenFromMs"/>
    public float CondenseFormed { get; init; } = 0.62f;

    /// <summary>How condensed the jaw is once it is wide open (its teeth clearly seen, not yet hardened)...</summary>
    public float CondenseOpened { get; init; } = 0.80f;

    /// <summary>...at the end of the wide hold, as the close begins...</summary>
    public float CondenseHeld { get; init; } = 0.86f;

    /// <summary>...and while it holds the bite after the hardened snap (the dissolve starts from here).</summary>
    public float CondenseHolding { get; init; } = 0.86f;

    /// <inheritdoc cref="GatherMs"/>
    public float ArrivePx { get; init; } = 40f;

    /// <inheritdoc cref="GatherMs"/>
    public float ArriveScale { get; init; } = 1.15f;

    /// <summary>The maw's opacity on the first frame, rising to whole over <see cref="OpacityRampMs"/> along an ease-in-out: it grows out of nothing, it does not pop in.</summary>
    public float OpacityAtSpawn { get; init; } = 0.10f;

    /// <inheritdoc cref="OpacityAtSpawn"/>
    public float OpacityRampMs { get; init; } = 60f;

    /// <summary>OPEN: the jaw opens wide from <see cref="OpenFromMs"/> to here (four frames, an ease-in-out, over the creature's own standing drawing), condensed enough by then that its teeth read (not yet hardened), and holds wide until <see cref="CloseFromMs"/>.</summary>
    public float OpenedAtMs { get; init; } = 200f;

    /// <inheritdoc cref="OpenedAtMs"/>
    public float CloseFromMs { get; init; } = 233f;

    /// <summary>
    /// THE SNAP: from <see cref="CloseFromMs"/> the jaws CLOSE, accelerating (an ease-in of this power: each 60 fps step
    /// larger than the last, two frames part-way, the LARGEST step on the snap frame itself, with the sound), and meet
    /// PAST their rest here, on a frame (18 × 16.67 ms, set a hair under it so the playhead's thirds of a millisecond land
    /// ON the shut pose). The cue's transient and a kill's fall land on this frame.
    /// </summary>
    public float SnapAtMs { get; init; } = 299.9f;

    /// <inheritdoc cref="SnapAtMs"/>
    public float CloseEasePower { get; init; } = 3.0f;

    /// <summary>
    /// THE CLENCH, the pronounced first bite: the jaws stay past their rest this long (two frames), then settle to it over
    /// <see cref="SettleMs"/>; the maw DRIVES into the creature along the jaw (part-way on the snap frame, its full
    /// <see cref="ShakePx"/> on the next, then easing back in a straight line to nothing at <see cref="ClenchMs"/>: one
    /// push, never an in-out buzz, never a bounce off the creature) and SWELLS (<see cref="ClenchPulse"/> on the snap frame and the next, then easing
    /// back in even steps over <see cref="SwellEaseMs"/>), its hardened teeth catching the light: the bite is the peak.
    /// </summary>
    public float OvershootHoldMs { get; init; } = 33f;

    /// <inheritdoc cref="OvershootHoldMs"/>
    public float SettleMs { get; init; } = 50f;

    /// <inheritdoc cref="OvershootHoldMs"/>
    public float ShakePx { get; init; } = 16f;

    /// <inheritdoc cref="OvershootHoldMs"/>
    public float ClenchMs { get; init; } = 100f;

    /// <inheritdoc cref="OvershootHoldMs"/>
    public float ClenchPulse { get; init; } = 0.16f;

    /// <inheritdoc cref="OvershootHoldMs"/>
    public float SwellHoldMs { get; init; } = 1000f / 60f;

    /// <inheritdoc cref="OvershootHoldMs"/>
    public float SwellEaseMs { get; init; } = 66f;

    /// <summary>
    /// THE BITE: the jaws hold the creature this long after the snap: past their rest, settling, then tightening slowly
    /// (<see cref="TightGape"/>) over the three frames left after the settle (with one frame, the tightening was an
    /// in-out flicker before the release).
    /// </summary>
    public float HoldMs { get; init; } = 133f;

    /// <summary>The release begins (the end of the bite): the jaws let go and dissolve back into mist.</summary>
    public float ReleaseAtMs => SnapAtMs + HoldMs;

    /// <summary>
    /// DISSOLVE: the jaws let go, OPENING (<see cref="ReleaseGape"/>), and come apart back into smoke state by state (one
    /// or two strip states a frame, nine frames and more), RISING away (px, an ease-out, so it is seen) and spreading a
    /// little (a share), fading in even steps to nothing at <see cref="GoneMs"/>.
    /// </summary>
    public float ReleaseRisePx { get; init; } = 28f;

    /// <inheritdoc cref="ReleaseRisePx"/>
    public float ReleaseGrow { get; init; } = 0.12f;

    /// <summary>The dissolve's own length, from the release (so a retuned hold never squeezes it).</summary>
    public float DissolveMs { get; init; } = 160f;

    /// <summary>The maw is gone: the release plus the dissolve.</summary>
    public float GoneMs => ReleaseAtMs + DissolveMs;

    /// <summary>Below this opacity the maw is not drawn at all.</summary>
    public const float VisibleFloor = 0.01f;

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

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the maw's length against the recipe's (a size comparison film). 1 otherwise.</summary>
    public static readonly float SizeDial =
        float.TryParse(Environment.GetEnvironmentVariable("RH_REACTION_SIZE"), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 1f;

    /// <summary>THE SEEKER's JAWS (the REACTION reference): a Shadow maw made of mist, bite for bite.</summary>
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
